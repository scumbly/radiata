using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControllerWheel;

/// <summary>Detects the Steam-first startup leak and drives its remediation. The HidHide cloak blocks
/// FUTURE opens only (docs/INPUT-CAPTURE.md ▸ "The cloak blocks future opens only"): when steam.exe was
/// already running before Radiata's cloak landed, it holds a pre-cloak handle to the physical pad and
/// forwards it to Steam Input games alongside our virtual pad — double input that every cloak-success
/// readback calls "isolated". Nothing can revoke the handle; the cures are killing it (pad power-cycle,
/// Steam relaunch) or not presenting a second pad (Passthru Mode).
///
/// Detection is a deliberate heuristic, not handle enumeration — "who holds this device" has no clean
/// user-mode API, and a system-wide handle scan reads like malware to EDR. The flag is:
/// Steam's process started BEFORE this session AND the pad was already present at startup AND the cloak
/// is confirmed with the virtual pad up. A pad that connects mid-session is protected by cloak-first
/// ordering + the retained cloak, so it never flags. Errs quiet: a missed leak still surfaces through the
/// user noticing double input; a false alarm here would bounce Steam for nothing.</summary>
public sealed class SteamSentry
{
    public enum State { Idle, Detected, Remediating, Cleared }

    private readonly DateTime _sessionStart = DateTime.Now;   // Process.StartTime is local time
    private bool _padPresentAtLaunch;
    private bool _padLaunchWindowClosed;
    private bool _autoRelaunchSpent;   // the silent bounce fires at most once per session
    // Two latches because the cures differ in reach. Steam exiting or being restarted kills STEAM'S
    // pre-cloak handle only; the pad's devnode departing kills every process's. The generic
    // "unverified" downgrade reads only the second — a Steam restart does nothing about Discord.
    private bool _steamEpisodeOver;    // Steam's handle is gone: its restart, its exit, or the pad's departure
    private bool _handlesSevered;      // the pad's devnode departed: every pre-cloak handle is gone
    private bool _decisionTraced;      // the one-shot decision dump has been written
    private string? _steamProbeError;  // last StartTime failure inside SteamPredatesSession, for that dump

    private readonly Func<string, bool?>? _predatesProbe;
    private readonly Func<Task<bool>> _bounce;

    /// <summary>Every argument exists for the harness: <paramref name="predatesProbe"/> answers "did a
    /// process of this name start before this session" (null = the probe failed),
    /// <paramref name="bounce"/> stands in for the real Steam restart, and
    /// <paramref name="predatesMemoMs"/> shortens the per-name memo. Defaults = the real ones.</summary>
    public SteamSentry(Func<string, bool?>? predatesProbe = null, Func<Task<bool>>? bounce = null,
                       int predatesMemoMs = 5_000)
    {
        _predatesProbe = predatesProbe;
        _bounce = bounce ?? BounceSteamAsync;
        _predatesMemoMs = predatesMemoMs;
    }

    public State Current { get; private set; } = State.Idle;

    // ── Beyond Steam: honest degradation instead of a false "isolated" ─────────────────────────────
    // Steam is only the holder that ALWAYS autostarts — any process that opened the pad before the
    // cloak landed keeps its handle the same way (the cloak blocks future opens only). Steam keeps the
    // loud path (silent bounce / the Unlock Controller card) because its pad-grab is verified,
    // process-wide and unconditional; for everything else, "started before us" is only a plausible
    // sign, so the response is honesty, not an alarm: the tray reports "isolation unverified" (naming
    // a known plausible holder when one matches) rather than asserting "isolated".
    private static readonly (string Proc, string Display)[] KnownPadConsumers =
    [
        ("Discord",    "Discord"),
        ("GalaxyClient", "GOG Galaxy"),
        ("RTSS",       "RivaTuner"),
        ("DS4Windows", "DS4Windows"),
    ];

    /// <summary>Whether "isolated" would overclaim right now: the pad was present before the cloak
    /// landed and nothing has severed pre-cloak handles since (no pad power-cycle, no cure). Steam's
    /// own detected/remediating episode is not "unverified" — it is a diagnosed leak with its own UX.</summary>
    public bool IsolationUnverified { get; private set; }

    /// <summary>Display name of a KNOWN plausible holder backing <see cref="IsolationUnverified"/>
    /// (a listed process that predates this session), or null when the downgrade is generic.</summary>
    public string? UnverifiedHolder { get; private set; }

    private bool _unverifiedTraced;

    /// <summary>Re-evaluated on every capture pass. Latches pad-present-at-launch during the first
    /// 30 s, then reports whether the leak condition currently holds.</summary>
    public State Evaluate(bool padPresent, bool cloakConfirmedWithPad, bool safeMode)
    {
        if (!_padLaunchWindowClosed)
        {
            if (padPresent) _padPresentAtLaunch = true;
            if ((DateTime.Now - _sessionStart).TotalSeconds > 30) _padLaunchWindowClosed = true;
        }

        EvaluateUnverified(cloakConfirmedWithPad, safeMode);

        if (_steamEpisodeOver || Current == State.Remediating)
            return Current;

        // Passthru hands the pad to every process by choice: no episode to report while it lasts, and not
        // a cure — Steam can hold the pad again once the cloak returns, so detection resumes after it.
        if (safeMode)
        {
            if (Current == State.Detected) Current = State.Idle;
            return Current;
        }
        // A dip in the cloak + virtual-pad pair (a Bluetooth verification hold, a disconnect grace) says
        // nothing about Steam's handle: no transition either way.
        if (!cloakConfirmedWithPad)
            return Current;

        // One-shot decision dump on the first fully-formed pass: a leak that ISN'T flagged is otherwise
        // indistinguishable from no leak, and a StartTime failure inside SteamPredatesSession must not read
        // as "no Steam". Log the inputs before anyone guesses.
        if (!_padPresentAtLaunch)
        {
            if (!_decisionTraced)
            {
                _decisionTraced = true;
                Trace.WriteLine("[Sentry] first decision: padAtLaunch=False cloak=True passthru=False → leak=False");
            }
            return Current;
        }

        bool? predates = SteamPredatesSession();
        if (!_decisionTraced)
        {
            _decisionTraced = true;
            Trace.WriteLine($"[Sentry] first decision: padAtLaunch=True cloak=True passthru=False "
                            + $"steamPredates={(predates is null ? "unknown" : predates.ToString())} "
                            + $"(steam err: {_steamProbeError ?? "none"}) → leak={predates == true}");
        }

        // The probe failed: unknown is not "Steam gone", so nothing changes this pass.
        if (predates is null) return Current;

        if (predates == false)
        {
            // Steam exited or restarted since the session began: its old handle died with its process and
            // the new one was denied by the live cloak. Steam's episode is over; other holders are not.
            if (Current == State.Detected)
            {
                _steamEpisodeOver = true;
                Current = State.Cleared;
                Trace.WriteLine("[Sentry] Steam no longer predates this session — its episode is over");
            }
            return Current;
        }

        if (Current == State.Idle)
        {
            Current = State.Detected;
            Trace.WriteLine("[Sentry] Steam predates this session with the pad already attached — its "
                            + "pre-cloak handle bleeds physical input to Steam Input games (double input)");
        }
        return Current;
    }

    /// <summary>The tray-facing downgrade. Preconditions mirror the leak flag's, minus the who: any
    /// process running before the cloak landed MAY hold a pre-cloak handle, and no readback can prove
    /// otherwise (handle enumeration reads like malware to EDR — see the type remarks). Cleared only by
    /// the pad's departure, which kills every handle: a Steam restart cures Steam's handle alone.</summary>
    private void EvaluateUnverified(bool cloakConfirmedWithPad, bool safeMode)
    {
        if (_handlesSevered || !_padPresentAtLaunch || !cloakConfirmedWithPad || safeMode
            || Current is State.Detected or State.Remediating)
        {
            IsolationUnverified = false;
            UnverifiedHolder = null;
            return;
        }

        IsolationUnverified = true;
        UnverifiedHolder = null;
        foreach (var (proc, display) in KnownPadConsumers)
        {
            if (ProcessPredatesSession(proc) != true) continue;
            UnverifiedHolder = display;
            break;
        }
        if (!_unverifiedTraced)
        {
            _unverifiedTraced = true;
            Trace.WriteLine("[Sentry] pad was present before the cloak landed — pre-cloak handles can't be "
                            + "ruled out; isolation reported as unverified"
                            + (UnverifiedHolder is null ? "" : $" (plausible holder running: {UnverifiedHolder})"));
        }
    }

    /// <summary>The pad's devnode left the device tree: every handle to it died, Steam's included, and
    /// its re-open on reconnect hits the retained cloak and is denied. Call only on an observed
    /// departure — a reader's logical disconnect (a yield to another backend, a failed read, a manual
    /// reset) leaves the devnode and every handle to it alive.</summary>
    public void OnPadDeparted()
    {
        if (_handlesSevered) return;
        if (Current is State.Detected or State.Remediating)
            Trace.WriteLine("[Sentry] pad departed — Steam's handle died with it; leak cleared");
        else if (IsolationUnverified)
            Trace.WriteLine("[Sentry] pad departed — pre-cloak handles died with it; isolation verified from here");
        _steamEpisodeOver = true;
        _handlesSevered = true;
        IsolationUnverified = false;
        UnverifiedHolder = null;
        Current = State.Cleared;
    }

    /// <summary>Whether the silent startup bounce may fire: detection stands, it hasn't been spent, and
    /// the session is still young. The bounce's charter is fixing the STARTUP order (SystemConfig
    /// documents it as "relaunched once at startup"); a leak first diagnosed mid-session — Passthru Mode
    /// toggled off an hour in — gets the card instead, where a silent Steam exit out of nowhere would
    /// read as a crash. The caller layers the idle/game gates on top (a Steam relaunch kills
    /// Steam-launched games — see App.EvaluateSteamSentry).</summary>
    public bool MayAutoRelaunch => Current == State.Detected && !_autoRelaunchSpent
                                   && DateTime.Now - _sessionStart < AutoRelaunchWindow;
    private static readonly TimeSpan AutoRelaunchWindow = TimeSpan.FromMinutes(10);

    /// <summary>Cleanly relaunch Steam: `-shutdown`, wait out its exit, start it `-silent` (tray only, no
    /// window thrown at the user). Steam's fresh pad-open is denied by the live cloak, so isolation is
    /// genuinely clean afterwards. Returns false if Steam couldn't be found or wouldn't die in time —
    /// the caller falls back to the alert.</summary>
    /// <remarks>Awaited without ConfigureAwait(false): the state writes after the bounce must land on the
    /// caller's (UI) thread, where every capture pass reads them.</remarks>
    public async Task<bool> RelaunchSteamAsync()
    {
        _autoRelaunchSpent = true;
        Current = State.Remediating;
        bool ok = await _bounce();
        if (ok) { _steamEpisodeOver = true; Current = State.Cleared; }
        else Current = State.Detected;
        return ok;
    }

    /// <summary>User-opted restart (the OOBE checkbox, the post-update marker): the same bounce, but it
    /// must not fabricate sentry state — a failure here would otherwise read as a Detected leak and raise
    /// the "Unlock Controller" card for a condition that was never diagnosed. Success closes Steam's
    /// episode, not the generic downgrade: the bounce cures Steam's handle alone.</summary>
    public async Task<bool> RestartSteamForUserAsync()
    {
        bool ok = await _bounce();
        if (ok) { _steamEpisodeOver = true; Current = State.Cleared; }
        return ok;
    }

    private static async Task<bool> BounceSteamAsync()
    {
        try
        {
            var exe = WindowsPlatformActions.SteamExePath();
            if (exe is null || !File.Exists(exe))
            {
                Trace.WriteLine("[Sentry] steam.exe not found — cannot relaunch");
                return false;
            }

            Trace.WriteLine("[Sentry] relaunching Steam (-shutdown, then -silent)");
            using (var stop = Process.Start(new ProcessStartInfo(exe, "-shutdown") { UseShellExecute = false }))
            { /* the stub exits immediately; the running client does the shutdown */ }

            // Steam's shutdown drains downloads/cloud sync; give it a real budget.
            for (int waited = 0; SteamRunning(); waited += 500)
            {
                if (waited >= 45_000)
                {
                    Trace.WriteLine("[Sentry] Steam did not exit within 45 s — leaving it alone");
                    return false;
                }
                await Task.Delay(500).ConfigureAwait(false);
            }

            using (Process.Start(new ProcessStartInfo(exe, "-silent") { UseShellExecute = false })) { }
            Trace.WriteLine("[Sentry] Steam relaunched — its fresh pad-open is denied by the live cloak");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Sentry] Steam relaunch failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private bool? SteamPredatesSession() => ProcessPredatesSession("steam", recordProbeError: true);

    // Session-scoped on purpose: with fast user switching, ANOTHER account's Steam predating this
    // session made steamPredates=True in a session where Steam holds nothing — a fabricated leak with
    // the loud card and a pointless bounce. Only this session's processes can hold this session's pad.
    // null = the probe could not answer; never memoised, so the next pass asks again.
    private bool? ProcessPredatesSession(string processName, bool recordProbeError = false)
    {
        // Every capture pass asks this for each known holder plus Steam — up to five whole process-table
        // snapshots per pass, ~72 a minute in the idle isolated state, for an answer that cannot change within
        // seconds. Memoised for a few seconds per name; a process exiting shows up on the next expiry.
        long now = Environment.TickCount64;
        if (_predates.TryGetValue(processName, out var memo) && now - memo.StampMs < _predatesMemoMs)
            return memo.Result;
        bool? result = _predatesProbe is { } probe ? probe(processName) : ProbePredatesSession(processName, recordProbeError);
        if (result is { } known) _predates[processName] = (now, known);
        return result;
    }

    private readonly Dictionary<string, (long StampMs, bool Result)> _predates = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _predatesMemoMs;

    private bool? ProbePredatesSession(string processName, bool recordProbeError)
    {
        bool failed = false;
        foreach (var p in WindowsPlatformActions.GetSessionProcessesByName(processName))
        {
            using (p)
            {
                try { if (p.StartTime < _sessionStart) return true; }
                catch (Exception ex)
                {
                    // Exited between enumeration and StartTime, or access denied. Unknown, not "absent":
                    // for Steam a swallowed failure would end a real episode as "Steam gone".
                    failed = true;
                    if (recordProbeError) _steamProbeError = $"{ex.GetType().Name}: {ex.Message}";
                }
            }
        }
        return failed ? null : false;
    }

    /// <summary>Whether steam.exe is up in THIS session right now. Also the visibility gate for the
    /// user-facing "Restart Steam" opt-ins (OOBE step 1, the update prompt) — there is nothing to offer
    /// otherwise, and another account's Steam (fast user switching) is not ours to offer restarting.</summary>
    public static bool SteamRunning() => WindowsPlatformActions.AnySessionProcess("steam");
}

/// <summary>A user-opted Steam restart that has to survive the process dying: the update prompt's
/// checkbox is answered in the OUTGOING process, but the restart can only happen after the incoming one
/// has its cloak up, so the request is a marker file rather than in-memory state.
///
/// Validate-on-read like every other local file (the payload is ignored entirely — presence IS the
/// request), and consumed by deleting it, so a crash between install and cloak costs one lost restart
/// rather than a bounce on every future launch.</summary>
internal static class SteamRestartFlag
{
    private static string Path_ => Path.Combine(AppPaths.AppDataDir, "pending-steam-restart.txt");

    public static void Arm()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDir);
            File.WriteAllText(Path_, "1");
            Trace.WriteLine("[Sentry] Steam restart armed for after the update");
        }
        catch (Exception ex) { Trace.WriteLine($"[Sentry] couldn't arm the Steam restart: {ex.Message}"); }
    }

    /// <summary>True exactly once per armed request; clears the marker as it reports.</summary>
    public static bool Consume()
    {
        try
        {
            if (!File.Exists(Path_)) return false;
            File.Delete(Path_);
            Trace.WriteLine("[Sentry] consuming the armed post-update Steam restart");
            return true;
        }
        catch (Exception ex)
        {
            // A marker that can't be deleted must not be honoured — it would bounce Steam every launch.
            Trace.WriteLine($"[Sentry] couldn't consume the Steam restart marker: {ex.Message}");
            return false;
        }
    }
}
