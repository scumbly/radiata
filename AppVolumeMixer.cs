using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace ControllerWheel;

/// <summary>
/// The application volume mixer behind D-pad ◀ ▶ while a wheel is open. The pair is resolved live from
/// what's actually running and playing audio, never picked manually:
///
///   left  — the running game (a process with an audio session whose exe lives under a known
///           installed game's folder); the frontmost one wins when several are running.
///   right — the running chat app (Discord); if none, the running music app (Spotify, Apple
///           Music/iTunes, MusicBee, foobar2000, Windows Media Player); if none, the running
///           browser (Chrome, Edge, Firefox, Brave, Opera, Vivaldi).
///
/// Balance applies PS5-style (both full at centre) to every audio session belonging to each side's
/// process name — multi-process apps (browsers, some games) route audio through same-named children, so
/// name-scoped application is what covers them.
/// </summary>
internal static class AppVolumeMixer
{
    internal sealed record Pair(string LeftLabel, string LeftProcess, string RightLabel, string RightProcess);

    private static readonly string[] ChatApps    = ["Discord"];
    private static readonly string[] MusicApps   = ["Spotify", "AppleMusic", "iTunes", "MusicBee", "foobar2000", "wmplayer"];
    private static readonly string[] BrowserApps = ["chrome", "msedge", "firefox", "brave", "opera", "vivaldi"];

    // Known-game install dirs (dir → display name); GameLibrary.Scan isn't free, so snapshot + reuse.
    private static (string dir, string name)[] _games = [];
    private static long _gamesAt;
    private const long GamesTtlMs = 60_000;

    /// <summary>Resolve the live pair, or null when there's nothing sensible to mix (no game with an
    /// audio session, or no second app). Call at mix-gesture start — cheap enough per-gesture.</summary>
    public static Pair? ResolvePair()
    {
        try
        {
            var sessions = SessionsByProcess();
            if (sessions.Count == 0) return null;
            RefreshGames();

            uint fgPid = ForegroundPid();
            (string label, string proc)? left = null;
            foreach (var (proc, s) in sessions)
            {
                if (GameNameFor(s.path) is not { } gameName) continue;
                bool isFg = s.pids.Contains(fgPid);
                if (left is null || isFg) left = (gameName, proc);
                if (isFg) break;
            }
            if (left is null) return null;

            var right = FirstOf(sessions, ChatApps,    left.Value.proc)
                     ?? FirstOf(sessions, MusicApps,   left.Value.proc)
                     ?? FirstOf(sessions, BrowserApps, left.Value.proc);
            if (right is null) return null;

            return new Pair(left.Value.label, left.Value.proc, right.Value.label, right.Value.proc);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Mixer] pair resolve failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Apply <paramref name="balance"/> (0..100; 50 = both full) to both sides' sessions.
    /// Returns whether each side had at least one live session to set.</summary>
    public static (bool okLeft, bool okRight) SetBalance(Pair pair, int balance)
    {
        float volL = balance <= 50 ? 1f : (100 - balance) / 50f;
        float volR = balance >= 50 ? 1f : balance / 50f;
        return (SetProcessSessions(pair.LeftProcess, volL), SetProcessSessions(pair.RightProcess, volR));
    }

    // ── Session plumbing ─────────────────────────────────────────────────────────

    private static Dictionary<string, (HashSet<uint> pids, string? path)> SessionsByProcess()
    {
        var map = new Dictionary<string, (HashSet<uint>, string?)>(StringComparer.OrdinalIgnoreCase);
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var mgr = device.AudioSessionManager;
        bool loggedPathFail = false;   // once per enumeration pass — a busy machine has many sessions
        for (int i = 0; i < mgr.Sessions.Count; i++)
        {
            var s = mgr.Sessions[i];
            uint pid = s.GetProcessID;
            if (pid == 0 || pid == (uint)Environment.ProcessId) continue;
            string? name = null, path = null;
            try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName; }
            catch { continue; }   // session outlived its process
            try { path = NativeMethods.ProcessImagePath((int)pid); }
            catch (Exception ex)
            {
                if (!loggedPathFail)
                {
                    loggedPathFail = true;
                    Trace.WriteLine($"[Mixer] image path unavailable for pid {pid} (further failures this pass suppressed): {ex.Message}");
                }
            }
            if (map.TryGetValue(name, out var cur)) { cur.Item1.Add(pid); if (cur.Item2 is null) map[name] = (cur.Item1, path); }
            else map[name] = ([pid], path);
        }
        return map;
    }

    private static bool SetProcessSessions(string processName, float volume)
    {
        bool any = false;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var mgr = device.AudioSessionManager;
            bool loggedSetFail = false;   // once per enumeration pass
            for (int i = 0; i < mgr.Sessions.Count; i++)
            {
                var s = mgr.Sessions[i];
                uint pid = s.GetProcessID;
                if (pid == 0) continue;
                string name;
                try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName; }
                catch { continue; }
                if (!name.Equals(processName, StringComparison.OrdinalIgnoreCase)) continue;
                try { s.SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f); any = true; }
                catch (Exception ex)
                {
                    if (!loggedSetFail)
                    {
                        loggedSetFail = true;
                        Trace.WriteLine($"[Mixer] volume set failed for '{processName}' pid {pid} (further failures this pass suppressed): {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Mixer] set '{processName}' failed: {ex.Message}"); }
        return any;
    }

    // ── Pair-side pickers ────────────────────────────────────────────────────────

    private static (string label, string proc)? FirstOf(
        Dictionary<string, (HashSet<uint> pids, string? path)> sessions, string[] candidates, string exclude)
    {
        foreach (var c in candidates)
            if (!c.Equals(exclude, StringComparison.OrdinalIgnoreCase) && sessions.ContainsKey(c))
                return (DisplayName(c), c);
        return null;
    }

    private static string DisplayName(string proc) => proc.ToLowerInvariant() switch
    {
        "msedge"     => "Edge",
        "chrome"     => "Chrome",
        "firefox"    => "Firefox",
        "wmplayer"   => "Media Player",
        "applemusic" => "Apple Music",
        "itunes"     => "iTunes",
        _            => char.ToUpperInvariant(proc[0]) + proc[1..],
    };

    private static string? GameNameFor(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        foreach (var (dir, name) in _games)
            if (exePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return name;
        return null;
    }

    private static int _gamesScanRunning;   // one scan in flight at a time
    private static void RefreshGames()
    {
        // The TTL must apply even when the last scan found nothing — a slow-to-scan machine with no
        // detected games must not pay the full 7-storefront scan on every gesture. The first scan (no
        // snapshot yet) runs inline; every refresh after that runs off-thread and the gesture uses the
        // previous snapshot immediately.
        if (_gamesAt != 0 && Environment.TickCount64 - _gamesAt < GamesTtlMs) return;
        bool firstScan = _gamesAt == 0;
        if (System.Threading.Interlocked.Exchange(ref _gamesScanRunning, 1) != 0) return;
        if (firstScan) ScanGames();
        else System.Threading.Tasks.Task.Run(ScanGames);
    }

    private static void ScanGames()
    {
        try
        {
            // Same prefix-match trap as text-chat's ScanChatGames: a storefront's own separators can't be
            // trusted, and an unnormalised prefix matches nothing at all rather than failing loudly.
            _games = [.. GameLibrary.Scan()
                .Where(g => !string.IsNullOrWhiteSpace(g.InstallDir))
                .Select(g => (GameLibrary.NormalizeDir(g.InstallDir!) + "\\", g.Name))];
        }
        catch (Exception ex) { Trace.WriteLine($"[Mixer] game-dir refresh failed: {ex.Message}"); }
        finally
        {
            _gamesAt = Environment.TickCount64;
            System.Threading.Interlocked.Exchange(ref _gamesScanRunning, 0);
        }
    }

    private static uint ForegroundPid()
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint pid);
            return pid;
        }
        catch { return 0; }
    }
}
