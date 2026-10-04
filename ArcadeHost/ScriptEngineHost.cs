using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Jint;

namespace ControllerWheel.ArcadeHost;

/// <summary>Statement budget aggregated per RENDERED frame, never per fixed step — a per-step
/// budget would be a 12× budget under the arcade's catch-up loop. Jint calls Reset() at the start
/// of every Invoke (i.e. every tick), so Reset() must be a no-op; only the per-request
/// ResetFrame() refills the budget.</summary>
internal sealed class FrameBudgetConstraint(int budget) : Jint.Constraint
{
    private int _used;
    public void ResetFrame() => _used = 0;
    public override void Check()
    {
        if (++_used > budget)
            throw new ScriptBudgetException($"frame statement budget exceeded ({budget})");
    }
    public override void Reset() { /* deliberate no-op — see class summary */ }
}

/// <summary>Wall-clock deadline per rendered frame, armed at request receipt. Checked at Jint
/// constraint boundaries, so one expensive built-in can overrun it — the HOST's wall-clock kill is
/// the authoritative stop (POC finding: RegexTimeout does not fire on catastrophic backtracking).</summary>
internal sealed class FrameDeadlineConstraint : Jint.Constraint
{
    private long _deadlineTicks = long.MaxValue;
    public void Arm(double ms) => _deadlineTicks = Stopwatch.GetTimestamp() + (long)(ms / 1000.0 * Stopwatch.Frequency);
    public override void Check()
    {
        if (Stopwatch.GetTimestamp() > _deadlineTicks)
            throw new ScriptBudgetException("frame deadline exceeded");
    }
    public override void Reset() { /* no-op: the deadline spans all fixed steps of one frame */ }
}

internal sealed class ScriptBudgetException(string message) : Exception(message);

internal static class ScriptEngineHost
{
    private const int FrameStatementBudget = 2_000_000;
    private const double FrameDeadlineMs = 8.0;
    // The same cap the host validates against: a game that overdraws is truncated here, benignly, instead of
    // shipping a frame the host must read as a lying helper (kill + restart, three times, then the card).
    private const int HelperCommandCap = ScriptProtocol.MaxCommands;

    public static int Run(string pipeName)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous);
        pipe.Connect(10_000);

        var budget = new FrameBudgetConstraint(FrameStatementBudget);
        var deadline = new FrameDeadlineConstraint();
        var commands = new List<ScriptDrawCommand>();
        var cues = new List<string>();
        var kv = new Dictionary<string, string>();
        var kvWrites = new Dictionary<string, string>();
        int kvBytes = 0;
        ulong rngState = 0x9E3779B97F4A7C15;

        var engine = new Engine(o =>
        {
            o.Strict();
            o.LimitRecursion(64);
            o.LimitMemory(32L * 1024 * 1024);        // burst tripwire ONLY — the Job Object is the cap
            o.DisableStringCompilation();             // no eval / Function-from-string
            o.Constraints.MaxArraySize = 1_000_000;
            o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(50);
            o.Constraint(budget);
            o.Constraint(deadline);
            // NO AllowClr. The global object stays empty except the bindings below.
        });

        void Append(ScriptDrawCommand c) { if (commands.Count < HelperCommandCap) commands.Add(c); }
        static uint Argb(double v)
            => double.IsFinite(v) && v is >= 0 and <= uint.MaxValue ? (uint)v : 0u;

        engine.SetValue("arc", new Action<double, double, double, double, double>((r0, r1, a0, a1, col) =>
            Append(new ScriptDrawCommand { t = "arc", r = r0, r2 = r1, a = a0, a2 = a1, c = Argb(col) })));
        engine.SetValue("dot", new Action<double, double, double, double>((r, th, size, col) =>
            Append(new ScriptDrawCommand { t = "dot", r = r, a = th, w = size, c = Argb(col) })));
        engine.SetValue("line", new Action<double, double, double, double, double, double>((r0, th0, r1, th1, width, col) =>
            Append(new ScriptDrawCommand { t = "line", r = r0, a = th0, r2 = r1, a2 = th1, w = width, c = Argb(col) })));
        engine.SetValue("poly", new Action<double[], double>((pts, col) =>
            Append(new ScriptDrawCommand { t = "poly", p = pts, c = Argb(col) })));
        engine.SetValue("text", new Action<double, double, double, string, double>((r, th, size, str, col) =>
            Append(new ScriptDrawCommand { t = "text", r = r, a = th, w = size, s = str, c = Argb(col) })));
        engine.SetValue("ring", new Action<double, double, double, double>((r, width, col, strokeCol) =>
            Append(new ScriptDrawCommand
            {
                t = "arc", r = Math.Max(0, r - width / 2), r2 = Math.Min(1, r + width / 2),
                a = 0, a2 = 360, c = Argb(col), sc = Argb(strokeCol), sw = 0,
            })));

        engine.SetValue("cue", new Action<string>(name =>
        {
            if (cues.Count < ScriptProtocol.MaxCuesPerFrame && !string.IsNullOrEmpty(name) && name.Length <= 32)
                cues.Add(name);
        }));
        engine.SetValue("kvGet", new Func<string, string?>(k => kv.GetValueOrDefault(k)));
        engine.SetValue("kvSet", new Action<string, string>((k, v) =>
        {
            if (string.IsNullOrEmpty(k) || k.Length > 64 || v is null || v.Length > 1024)
                throw new InvalidOperationException("kv key/value out of bounds");
            int delta = Encoding.UTF8.GetByteCount(k) + Encoding.UTF8.GetByteCount(v)
                        - (kv.TryGetValue(k, out var old)
                            ? Encoding.UTF8.GetByteCount(k) + Encoding.UTF8.GetByteCount(old) : 0);
            if (kvBytes + delta > ScriptProtocol.KvTotalCapBytes)
                throw new InvalidOperationException("kv store full (4 KB cap)");
            kv[k] = v; kvBytes += delta;
            if (kvWrites.Count < ScriptProtocol.MaxKvWritesPerFrame || kvWrites.ContainsKey(k))
                kvWrites[k] = v;
        }));
        // Deterministic xorshift64*, seeded by the host — a script cannot observe real entropy.
        engine.SetValue("rand", new Func<double>(() =>
        {
            rngState ^= rngState >> 12; rngState ^= rngState << 25; rngState ^= rngState >> 27;
            return ((rngState * 0x2545F4914F6CDD1D) >> 11) * (1.0 / (1ul << 53));
        }));

        void SetInput(ScriptInput input)
        {
            engine.SetValue("stickX", input.stickX);
            engine.SetValue("stickY", input.stickY);
            engine.SetValue("buttons", input.buttons);
            engine.SetValue("crossDown", (input.buttons & ScriptInput.CrossDown) != 0);
            engine.SetValue("squareDown", (input.buttons & ScriptInput.SquareDown) != 0);
            engine.SetValue("crossPressed", (input.buttons & ScriptInput.CrossPressed) != 0);
            engine.SetValue("squarePressed", (input.buttons & ScriptInput.SquarePressed) != 0);
            engine.SetValue("dpadUp", (input.buttons & ScriptInput.DUp) != 0);
            engine.SetValue("dpadRight", (input.buttons & ScriptInput.DRight) != 0);
            engine.SetValue("dpadDown", (input.buttons & ScriptInput.DDown) != 0);
            engine.SetValue("dpadLeft", (input.buttons & ScriptInput.DLeft) != 0);
        }

        var ct = CancellationToken.None;
        bool loaded = false;

        while (true)
        {
            byte[] raw;
            try { raw = ScriptProtocol.ReadMessageAsync(pipe, ScriptProtocol.MaxRequestBytes, ct).GetAwaiter().GetResult(); }
            catch (EndOfStreamException) { return 0; }   // host went away — normal teardown
            catch (IOException) { return 0; }
            // A request over MaxRequestBytes. The cap is set to fit any manifest-legal script, so reaching
            // here means a host bug or a hostile host, not an oversized game — either way it exits on its own
            // code rather than escaping to Main's catch, which reported the truncated read ("EndOfStream")
            // and sent the reader looking for a torn pipe.
            catch (ScriptProtocolViolationException ex) { Trace.WriteLine($"[helper] oversized request: {ex.Message}"); return 4; }

            ScriptRequest req;
            try { req = JsonSerializer.Deserialize<ScriptRequest>(raw, ScriptProtocol.Json)!; }
            catch { return 3; }

            if (req.type == "load")
            {
                ScriptResponse resp;
                try
                {
                    if (req.kv is not null)
                        foreach (var (k, v) in req.kv)
                        {
                            if (k.Length > 64 || v.Length > 1024) continue;
                            int add = Encoding.UTF8.GetByteCount(k) + Encoding.UTF8.GetByteCount(v);
                            if (kvBytes + add > ScriptProtocol.KvTotalCapBytes) break;
                            kv[k] = v; kvBytes += add;
                        }
                    rngState = req.seed == 0 ? 0x9E3779B97F4A7C15 : req.seed;

                    // Script arrives as TEXT (never a path); parsed ONCE, functions reused per frame.
                    var prepared = Engine.PrepareScript(req.script!, strict: true);
                    budget.ResetFrame();
                    deadline.Arm(1000);   // generous load-time deadline
                    engine.Execute(prepared);
                    // JIT warm-up under the load deadline: without it, cold Jint paths blow the
                    // 8 ms frame deadline on the first 2-3 real frames (POC carry-over #3).
                    try
                    {
                        SetInput(new ScriptInput());
                        for (int i = 0; i < 3; i++)
                        {
                            engine.Invoke("tick", ScriptProtocol.StepSeconds);
                            engine.Invoke("draw");
                            commands.Clear(); cues.Clear();
                        }
                    }
                    catch { /* warm-up faults are the script's problem, reported on frame 1 */ }
                    commands.Clear(); cues.Clear(); kvWrites.Clear();
                    loaded = true;
                    resp = new ScriptResponse { type = "ready" };
                }
                catch (Exception ex)
                {
                    resp = new ScriptResponse { type = "fault", message = Trim(ex) };
                }
                try { ScriptProtocol.WriteMessageAsync(pipe, resp, ct).GetAwaiter().GetResult(); }
                catch (IOException) { return 0; }
                continue;
            }

            // frame
            ScriptResponse frameResp;
            if (!loaded)
            {
                frameResp = new ScriptResponse { type = "fault", frameId = req.frameId, message = "no script loaded" };
            }
            else
            {
                // Zero steps is legal and means "draw only" — the arcade's pause/ready/guard
                // states keep rendering a genuinely frozen sim.
                int steps = Math.Clamp((int)Math.Round(req.elapsedMs / (ScriptProtocol.StepSeconds * 1000.0)), 0,
                    ScriptProtocol.MaxStepsPerFrame);
                SetInput(req.input ?? new ScriptInput());

                commands.Clear(); cues.Clear(); kvWrites.Clear();
                budget.ResetFrame();      // ONE budget for all N steps + draw of this rendered frame
                deadline.Arm(FrameDeadlineMs);
                try
                {
                    for (int i = 0; i < steps; i++)
                        engine.Invoke("tick", ScriptProtocol.StepSeconds);
                    engine.Invoke("draw");
                    frameResp = new ScriptResponse
                    {
                        type = "draw", frameId = req.frameId, commands = commands,
                        kvWrites = kvWrites.Count > 0 ? new Dictionary<string, string>(kvWrites) : null,
                        cues = cues.Count > 0 ? new List<string>(cues) : null,
                    };
                }
                catch (Exception ex)
                {
                    frameResp = new ScriptResponse { type = "fault", frameId = req.frameId, message = Trim(ex) };
                }
            }
            try { ScriptProtocol.WriteMessageAsync(pipe, frameResp, ct).GetAwaiter().GetResult(); }
            catch (IOException) { return 0; }
        }
    }

    private static string Trim(Exception ex)
    {
        string m = $"{ex.GetType().Name}: {ex.Message}";
        return m.Length > 300 ? m[..300] : m;
    }
}
