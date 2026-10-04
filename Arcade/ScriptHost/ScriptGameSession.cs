using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>One live helper process serving one script game: pipe, Job Object, AppContainer
/// launch, non-blocking frame pump. The UI thread never waits on the helper — a late response
/// reuses the last validated draw buffer and counts a miss; 30 consecutive misses, a 2 s silent
/// helper (the host wall-clock kill — the only reliable stop for a catastrophic regex), or any
/// protocol violation kills the helper. The coordinator decides whether to restart.</summary>
internal sealed class ScriptGameSession : IDisposable
{
    public const int MissRestartThreshold = 30;
    public const double StallKillMs = 2000;
    /// <summary>An unbroken run of fault responses with no successful draw between them (~2 s at
    /// 60 fps) is killed like a stall. Faults are individually survivable by design, but a script
    /// that faults every frame — a constraint-interrupted <c>while(true)</c> answers each frame
    /// with a prompt fault, so the silence-based wall-clock kill never fires — leaves the player a
    /// black disc with no explanation and floods the trace. One good draw resets the streak, so a
    /// game with an occasional bad frame is untouched.</summary>
    public const int ConsecutiveFaultLimit = 120;

    private readonly Process _proc;
    private readonly ScriptJobObject _job;
    private readonly NamedPipeServerStream _pipe;
    private Task<ScriptResponse>? _pending;
    private long _pendingSentAt;
    private long _nextFrameId = 1;
    private int _consecutiveFaults;
    private bool _dead;

    public List<ScriptDrawCommand> LastBuffer { get; private set; } = [];
    public bool IsDead => _dead;
    public int ConsecutiveMisses { get; private set; }
    public string? LastFault { get; private set; }

    private ScriptGameSession(Process proc, ScriptJobObject job, NamedPipeServerStream pipe)
    { _proc = proc; _job = job; _pipe = pipe; }

    /// <summary>Spawns and jails the helper, then loads the script (text + seed + kv snapshot).
    /// Returns (session, error) — error non-null means no session (already disposed).
    /// ⚠ Blocks for up to ~6.5 s (5 s connect + 1.5 s load budgets) — call only from a threadpool
    /// thread (ScriptSessionCoordinator.BeginStart does), never the UI thread.</summary>
    public static (ScriptGameSession? Session, string? Error) Start(
        string helperExe, ScriptAppContainer ac, string scriptText, ulong seed,
        IReadOnlyDictionary<string, string> kvSnapshot)
    {
        string pipeName = "radiata-arcade-" + Guid.NewGuid().ToString("N");

        // ⚠ Explicit buffer sizes are load-bearing: with the default 0-byte buffers a pipe write
        // blocks until the peer reads, so a stalled or hostile helper would wedge the host's send
        // path, not just the response path.
        // The ACL is part of containment: grant only the derived AppContainer SID + the current
        // user — deliberately not "ALL APPLICATION PACKAGES", which would open the pipe to every
        // sandboxed process on the machine.
        var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            inBufferSize: 64 * 1024, outBufferSize: 64 * 1024,
            pipeSecurity: BuildPipeSecurity(ac));

        var job = new ScriptJobObject();
        Process proc;
        try
        {
            proc = ScriptContainerLauncher.Launch(helperExe, $"\"{helperExe}\" {pipeName}", ac, job);
        }
        catch (Exception ex)
        {
            job.Dispose(); pipe.Dispose();
            return (null, $"helper launch failed: {ex.Message}");
        }

        var session = new ScriptGameSession(proc, job, pipe);
        string stage = "connect";
        try
        {
            using var cts = new CancellationTokenSource(5_000);
            pipe.WaitForConnectionAsync(cts.Token).GetAwaiter().GetResult();

            stage = "load";
            var load = new ScriptRequest
            {
                type = "load", script = scriptText, seed = seed,
                kv = kvSnapshot.Count > 0 ? new Dictionary<string, string>(kvSnapshot) : null,
            };
            // 1.5 s covers parse + top-level run + the JIT warm-up pass; a script that pins the
            // helper inside one built-in during load must be detected inside the 2 s budget.
            using var cts2 = new CancellationTokenSource(1_500);
            ScriptProtocol.WriteMessageAsync(pipe, load, cts2.Token).GetAwaiter().GetResult();
            byte[] raw = ScriptProtocol.ReadMessageAsync(pipe, ScriptProtocol.MaxResponseBytes, cts2.Token)
                .GetAwaiter().GetResult();
            var resp = JsonSerializer.Deserialize<ScriptResponse>(raw, ScriptProtocol.Json)!;
            if (resp.type != "ready")
            {
                string err = resp.message ?? "unknown load fault";
                session.Dispose();
                return (null, err);
            }
            return (session, null);
        }
        catch (Exception ex)
        {
            session.Dispose();
            return (null, $"[{stage}] {ex.GetType().Name}: {ex.Message}");
        }
    }

    public enum PumpStatus { Ok, Fault, Violation, Dead }

    /// <summary>One rendered-frame iteration. Never blocks. On a fresh validated response the kv
    /// writes and cues are applied to <paramref name="game"/>; late responses reuse
    /// <see cref="LastBuffer"/>. Violation/Dead mean the helper was killed — the coordinator
    /// decides on a restart.</summary>
    public PumpStatus Pump(ScriptArcadeGame game, double elapsedMs, ScriptInput input)
    {
        if (_dead) return PumpStatus.Dead;

        if (_pending is { IsCompleted: true } completed)
        {
            _pending = null;
            ScriptResponse resp;
            try { resp = completed.GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                Kill($"pipe: {ex.GetType().Name}: {ex.Message}");
                return PumpStatus.Dead;
            }
            ConsecutiveMisses = 0;

            if (resp.type == "fault")
            {
                LastFault = resp.message;
                _consecutiveFaults++;
                // First fault of a streak and every ~second thereafter — never per frame, or a
                // perpetually-faulting script writes thousands of identical lines a minute.
                if (_consecutiveFaults == 1 || _consecutiveFaults % 60 == 0)
                    Trace.WriteLine($"[Arcade] script fault (streak {_consecutiveFaults}): {resp.message}");
                if (_consecutiveFaults >= ConsecutiveFaultLimit)
                {
                    Kill($"faulted {ConsecutiveFaultLimit} consecutive frames with no draw: {resp.message}");
                    return PumpStatus.Violation;   // the coordinator's restart ladder → give-up card
                }
                SendNext(elapsedMs, input);
                return PumpStatus.Fault;
            }
            _consecutiveFaults = 0;
            if (resp.type != "draw")
            {
                Kill($"unexpected message type '{resp.type}'");
                return PumpStatus.Violation;
            }
            string? bad = ScriptDrawValidator.Validate(resp);
            if (bad is not null)
            {
                Kill(bad);
                return PumpStatus.Violation;
            }
            LastBuffer = resp.commands!;
            game.ApplyKvWrites(resp.kvWrites);
            game.EnqueueCues(resp.cues);
            SendNext(elapsedMs, input);
            return PumpStatus.Ok;
        }

        if (_pending is null)
        {
            SendNext(elapsedMs, input);
            return PumpStatus.Ok;
        }

        // Response still outstanding — a miss.
        ConsecutiveMisses++;
        if (_proc.HasExited) { Kill("helper exited"); return PumpStatus.Dead; }
        double waitedMs = (Stopwatch.GetTimestamp() - _pendingSentAt) * 1000.0 / Stopwatch.Frequency;
        if (waitedMs > StallKillMs)
        {
            // The authoritative stop: constraints can't interrupt one expensive built-in —
            // RegexTimeout does not fire on catastrophic backtracking.
            Kill($"helper silent for {waitedMs:F0} ms");
            return PumpStatus.Violation;
        }
        if (ConsecutiveMisses >= MissRestartThreshold)
        {
            Kill($"{MissRestartThreshold} consecutive misses");
            return PumpStatus.Violation;
        }
        return PumpStatus.Ok;   // reuse LastBuffer this frame
    }

    private void SendNext(double elapsedMs, ScriptInput input)
    {
        var req = new ScriptRequest { type = "frame", frameId = _nextFrameId++, elapsedMs = elapsedMs, input = input };
        _pendingSentAt = Stopwatch.GetTimestamp();
        _pending = SendReceiveAsync(req);
    }

    private async Task<ScriptResponse> SendReceiveAsync(ScriptRequest req)
    {
        await ScriptProtocol.WriteMessageAsync(_pipe, req, CancellationToken.None).ConfigureAwait(false);
        byte[] raw = await ScriptProtocol.ReadMessageAsync(_pipe, ScriptProtocol.MaxResponseBytes, CancellationToken.None)
            .ConfigureAwait(false);
        // The literal payload `null` deserialises to null for a reference type; it must take the same
        // kill-and-restart path as every other hostile message, not an NRE on the UI thread.
        return JsonSerializer.Deserialize<ScriptResponse>(raw, ScriptProtocol.Json)
               ?? throw new ScriptProtocolViolationException("null response");
    }

    private static PipeSecurity BuildPipeSecurity(ScriptAppContainer ac)
    {
        var sec = new PipeSecurity();
        var self = WindowsIdentity.GetCurrent().User!;
        sec.AddAccessRule(new PipeAccessRule(self, PipeAccessRights.FullControl, AccessControlType.Allow));
        sec.AddAccessRule(new PipeAccessRule(ac.Identifier,
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        return sec;
    }

    /// <summary>Terminates the helper and marks the session dead. Idempotent, never throws.</summary>
    public void Kill(string reason)
    {
        if (_dead) return;
        _dead = true;
        // Overwrite, never ??= : a survivable script fault earlier in the session must not mask the
        // terminal kill reason in the restart trace and the give-up card.
        LastFault = reason;
        Trace.WriteLine($"[Arcade] script helper killed: {reason}");
        try { if (!_proc.HasExited) _proc.Kill(entireProcessTree: true); } catch { }
    }

    public void Dispose()
    {
        Kill("session disposed");
        _job.Dispose();       // KILL_ON_JOB_CLOSE backstop
        try { _pipe.Dispose(); } catch { }
        try { _proc.WaitForExit(2000); } catch { }
        try { _proc.Dispose(); } catch { }
        _pending?.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        _pending = null;
    }
}
