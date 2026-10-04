using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace ControllerWheel;

/// <summary>Windows implementation of <see cref="IPlatformActions"/> — NAudio (CoreAudio), Win32
/// P/Invoke, Process launching, and Discord IPC. The platform-specific half of action execution;
/// the orchestration lives in Core's ActionExecutor.</summary>
public sealed class WindowsPlatformActions : IPlatformActions
{
    public void LaunchOrFocus(string path, string? processName = null)
    {
        var exeName = !string.IsNullOrWhiteSpace(processName)
            ? processName!.Trim()
            : Path.GetFileNameWithoutExtension(path);

        var existing = Process.GetProcessesByName(exeName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        if (existing is not null) { NativeMethods.ForceForeground(existing.MainWindowHandle); return; }
        // Name miss → also try a window from anything running under the app's own install folder: a
        // stub-launched app runs under a different exe name (see the install-folder section below).
        if (WindowUnderAppDir(path) is { } wnd && wnd != IntPtr.Zero)
        {
            Trace.WriteLine($"[Action] focus: '{exeName}' not found by name; focused a window from its install folder");
            NativeMethods.ForceForeground(wnd);
            return;
        }
        if (string.IsNullOrWhiteSpace(path)) return;

        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);   // let the app we're about to start foreground itself
        Process? started = null;
        try { started = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { return; }
        _ = FocusWhenReadyAsync(started, exeName);
    }

    /// <summary>Poll briefly for a just-launched app's main window and force it foreground — many apps don't
    /// self-foreground from a shell launch, and the no-activate overlay otherwise keeps focus. Then hands
    /// over to the new-window watcher: launcher→child chains outlive both the 4 s and the name.</summary>
    private static async System.Threading.Tasks.Task FocusWhenReadyAsync(Process? started, string exeName)
    {
        var baseline = NativeMethods.WindowedPids();   // snapshot BEFORE the app's window exists
        for (int i = 0; i < 40; i++)   // up to ~4 s
        {
            await System.Threading.Tasks.Task.Delay(100).ConfigureAwait(false);
            try
            {
                var p = started is { HasExited: false } && started.MainWindowHandle != IntPtr.Zero
                    ? started
                    : Process.GetProcessesByName(exeName).FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);
                if (p?.MainWindowHandle is { } h && h != IntPtr.Zero) { NativeMethods.ForceForeground(h); break; }
            }
            catch { /* process exit/access races — keep polling */ }
        }
        WatchForNewAppWindow(baseline);
    }

    // ── Game-launch foreground watcher ──────────────────────────────────────────
    // The named poll can't cover URI launches (steam:// / goggalaxy:// / battlenet — no process name) or
    // launcher→child chains (the game window appears long after 4 s). Snapshot the pids owning visible
    // top-level windows at launch, then push each NEW windowed pid frontmost as it appears (splash →
    // launcher → game). Generation-guarded so a newer launch supersedes an older watcher.
    private static int _fgWatchGen;

    private static void WatchForNewAppWindow(Dictionary<uint, IntPtr>? baseline = null, int watchSeconds = 45)
    {
        var seen = baseline ?? NativeMethods.WindowedPids();
        int gen = System.Threading.Interlocked.Increment(ref _fgWatchGen);
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            int pushes = 0;
            for (int i = 0; i < watchSeconds * 2 && pushes < 3; i++)
            {
                await System.Threading.Tasks.Task.Delay(500).ConfigureAwait(false);
                if (gen != _fgWatchGen) return;   // a newer launch owns the foreground now
                try
                {
                    foreach (var (pid, hwnd) in NativeMethods.WindowedPids())
                    {
                        if (seen.ContainsKey(pid) || pid == (uint)Environment.ProcessId) continue;
                        seen[pid] = hwnd;          // push each new windowed process once
                        NativeMethods.ForceForeground(hwnd);
                        pushes++;
                        Trace.WriteLine($"[Focus] pushed new app window to front (pid {pid})");
                    }
                }
                catch { /* window churn — keep watching */ }
            }
        });
    }

    public void RunFile(string path)
    {
        // Public seam: a stale/moved/bad path throws Win32Exception. ActionExecutor catches it upstream
        // ("Failed"), but a direct caller must not crash.
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Trace.WriteLine($"[Action] RunFile failed for '{path}': {ex.Message}"); }
    }

    public ProcessToggleResult ToggleProcess(string process, string? launchPath)
    {
        var byName = MatchingProcessIds(process, launchPath);
        if (byName.Count > 0)
        {
            Trace.WriteLine($"[Action] toggle: closing {byName.Count} '{process}' process(es) in this session");
            return RequestClose(byName);
        }
        // Name miss → the install-folder fallback below: narrow (the exe's own directory only) and
        // refused outright when unrelated software shares that folder.
        var byDir = ProcessIdsUnderAppDir(launchPath);
        if (byDir.Count > 0)
        {
            Trace.WriteLine($"[Action] toggle: '{process}' not found by name; closing {byDir.Count} process(es) under its install folder");
            return RequestClose(byDir);
        }
        if (string.IsNullOrWhiteSpace(launchPath)) return ProcessToggleResult.Unavailable;
        using var launched = Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true });
        return launched is null ? ProcessToggleResult.Unavailable : ProcessToggleResult.Started;
    }

    /// <summary>Pids of processes named <paramref name="name"/> that this slice may legitimately close.
    ///
    /// <para>⚠ Two filters, both mandatory — never reduce this to a bare
    /// <c>Process.GetProcessesByName(name).Kill()</c>:</para>
    /// <list type="bullet">
    ///   <item><b>Session.</b> Only this logon session — an elevated Radiata on a multi-user box would
    ///   otherwise kill another signed-in user's copy of the same app.</item>
    ///   <item><b>Image path.</b> With a launch path on the slice, the running exe must BE that exe; a
    ///   generic or hand-edited action ("process": "java", "node", "python") would otherwise terminate
    ///   whatever unrelated thing shares the name. Returning nothing is not a dead end — the caller falls
    ///   through to the install-folder match.</item>
    /// </list>
    /// A path is only enforced when it's rooted: an unrooted/empty path gives nothing to check against,
    /// and refusing to act on it would break every path-less Toggle slice.</summary>
    private static List<int> MatchingProcessIds(string name, string? launchPath)
    {
        var hits = new List<int>();
        if (string.IsNullOrWhiteSpace(name)) return hits;
        string? wanted = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(launchPath) && Path.IsPathRooted(launchPath))
                wanted = NormalizeDirForMatch(Path.GetFullPath(launchPath));
        }
        catch { /* malformed path in config → treat as "no path to check against" */ }

        int mySession;
        try { using var me = Process.GetCurrentProcess(); mySession = me.SessionId; }
        catch { return hits; }

        Process[] all;
        try { all = Process.GetProcessesByName(name); } catch { return hits; }
        foreach (var p in all)
        {
            try
            {
                if (p.SessionId != mySession) continue;
                if (wanted is not null)
                {
                    var img = NativeMethods.ProcessImagePath(p.Id);
                    if (img is null || NormalizeDirForMatch(img) != wanted) continue;
                }
                hits.Add(p.Id);
            }
            catch { /* exited mid-enumeration, or SessionId/path denied → not ours to kill */ }
            finally { p.Dispose(); }
        }
        return hits;
    }

    /// <summary>Request an orderly close. Applications retain control of unsaved-work prompts;
    /// background processes without a main window are left running.</summary>
    private static ProcessToggleResult RequestClose(List<int> pids)
    {
        bool requested = false;
        foreach (var pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.CloseMainWindow()) requested = true;
            }
            catch (Exception ex) { Trace.WriteLine($"[Action] toggle: close request for pid {pid} failed: {ex.Message}"); }
        }
        return requested ? ProcessToggleResult.CloseRequested : ProcessToggleResult.Unavailable;
    }

    public bool IsProcessRunning(string process, string? exePath = null)
    {
        // Asked on every armed-slice change from the wheel's arm path; the install-folder fallback below is a
        // whole-process-table sweep and runs exactly when the app is closed (the common case). Memoised for a
        // moment per (process, path) so sweeping the stick across a wheel pays it once, not once per slice.
        string key = process + "|" + exePath;
        long now = Environment.TickCount64;
        lock (RunningMemo)
        {
            if (RunningMemo.TryGetValue(key, out var memo) && now - memo.StampMs < RunningMemoMs) return memo.Result;
        }
        // Must keep the SAME match rules as ToggleProcess: the "On/Off" readout has to describe the
        // processes the toggle would actually act on, or a process in another session (or an unrelated exe
        // sharing the name) reads as "On" and the toggle looks stuck rather than scoped.
        bool running = MatchingProcessIds(process, exePath).Count > 0
            // Name miss → install-folder fallback, so a stub-launched app reads as running instead of "Off".
            || ProcessIdsUnderAppDir(exePath).Count > 0;
        lock (RunningMemo)
        {
            if (RunningMemo.Count > 256) RunningMemo.Clear();
            RunningMemo[key] = (now, running);
        }
        return running;
    }

    private static readonly Dictionary<string, (long StampMs, bool Result)> RunningMemo = new(StringComparer.OrdinalIgnoreCase);
    private const int RunningMemoMs = 1_000;

    // ── "Is this app running?" by INSTALL FOLDER, not just process name ──────────
    //
    // The name check stays first (common case, free). Its two blind spots are documented in the app-slices
    // Help topic:
    //   • a STUB/updater-launched app runs under a different exe name — covered here by matching any
    //     process whose IMAGE PATH sits inside the app's own install folder;
    //   • a Store/UWP app's window belongs to ApplicationFrameHost — unfixable this way, since a UWP app
    //     has no browsable exe for a slice to point at.
    //
    // Image paths come from QueryFullProcessImageName (NativeMethods.ProcessImagePath), NOT
    // Process.MainModule: MainModule throws across a 32/64-bit boundary and on access-denied — precisely
    // the population being inspected.

    /// <summary>Directories too widely shared to identify ONE app. An exe sitting directly in any of these
    /// makes folder-matching meaningless (every installed program would match), so it's refused — the name
    /// check still applies. `steamapps\common` counts: it's the parent of every Steam game.</summary>
    private static readonly string[] TooBroadForFolderMatch =
    [
        "%PROGRAMFILES%", "%PROGRAMFILES(X86)%", "%PROGRAMDATA%", "%WINDIR%", @"%WINDIR%\System32",
        "%LOCALAPPDATA%", @"%LOCALAPPDATA%\Programs", "%APPDATA%", "%USERPROFILE%",
        @"%USERPROFILE%\Desktop", @"%USERPROFILE%\Downloads", @"%USERPROFILE%\Documents",
    ];

    /// <summary>The install folder to match a slice's app by, or null when there isn't a usable one: no path,
    /// a <c>shell:AppsFolder\…</c> AUMID rather than an exe, a relative path, a missing folder, a drive root,
    /// or one of <see cref="TooBroadForFolderMatch"/>.</summary>
    private static string? AppInstallDirFor(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        try
        {
            if (!Path.IsPathRooted(exePath)) return null;                  // AUMIDs, bare names, URIs
            var dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return null;

            var norm = NormalizeDirForMatch(dir);
            if (norm.Length == 0) return null;
            var root = Path.GetPathRoot(dir);
            if (!string.IsNullOrEmpty(root) && NormalizeDirForMatch(root) == norm) return null;   // drive root
            if (norm.EndsWith(@"\steamapps\common", StringComparison.OrdinalIgnoreCase)) return null;

            foreach (var broad in TooBroadForFolderMatch)
            {
                var expanded = Environment.ExpandEnvironmentVariables(broad);
                if (expanded.StartsWith('%')) continue;                    // variable not set on this box
                if (NormalizeDirForMatch(expanded) == norm) return null;
            }
            return dir;
        }
        catch { return null; }
    }

    private static string NormalizeDirForMatch(string dir) =>
        dir.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();

    /// <summary>Pids of running processes whose executable lives inside the app's install folder. Empty when
    /// the folder isn't usable (see <see cref="AppInstallDirFor"/>) or nothing matches.</summary>
    private static List<int> ProcessIdsUnderAppDir(string? exePath)
    {
        var hits = new List<int>();
        if (AppInstallDirFor(exePath) is not { } dir) return hits;
        // Trailing separator so "C:\Fallout" can't match "C:\Fallout 2\…".
        var prefix = NormalizeDirForMatch(dir) + '\\';
        Process[] all;
        try { all = Process.GetProcesses(); } catch { return hits; }
        foreach (var p in all)
        {
            try
            {
                if (p.SessionId == 0) continue;    // services / session 0 are never the user's app
                if (NativeMethods.ProcessImagePath(p.Id) is { } img
                    && NormalizeDirForMatch(img).StartsWith(prefix, StringComparison.Ordinal))
                    hits.Add(p.Id);
            }
            catch { /* exited mid-enumeration, or SessionId denied */ }
            finally { p.Dispose(); }
        }
        return hits;
    }

    /// <summary>The first window handle belonging to a process under the app's install folder, or
    /// IntPtr.Zero. Used to FOCUS a stub-launched app whose process name we'd never have matched.</summary>
    private static IntPtr WindowUnderAppDir(string? exePath)
    {
        foreach (var pid in ProcessIdsUnderAppDir(exePath))
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle;
            }
            catch { /* exited between the scan and here */ }
        }
        return IntPtr.Zero;
    }

    public void OpenUrl(string url)
    {
        // Snapshot BEFORE launching, on every launch shape this method serves: the watcher must not mistake
        // a pre-existing window for the launched app's.
        var baseline = NativeMethods.WindowedPids();
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
        if (TryLaunchBattleNet(url)) { WatchForNewAppWindow(baseline); return; }
        if (TryLaunchGogGalaxy(url)) { WatchForNewAppWindow(baseline); return; }
        var psi = new ProcessStartInfo(url) { UseShellExecute = true };
        // Direct-exe "URLs" must run from the exe's OWN folder, not Radiata's cwd (System32 when
        // auto-started) — relative-path asset loads break otherwise.
        if (Path.IsPathRooted(url) && File.Exists(url))
            psi.WorkingDirectory = Path.GetDirectoryName(url);
        Process.Start(psi);
        WatchForNewAppWindow(baseline);
    }

    // A shell-executed "goggalaxy://rungameid/<id>" URI only opens/focuses the Galaxy client — it does NOT
    // start the game. Only the client's own command line launches it:
    // "GalaxyClient.exe /command=runGame /gameId=<id>". The URI stays the slice's stored URL (it's the
    // reconciliation key) and is translated here. Returns true if the url was a goggalaxy:// rungameid one
    // (handled or not).
    private static bool TryLaunchGogGalaxy(string url)
    {
        const string scheme = "goggalaxy://rungameid/";
        if (!url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return false;

        var id = url[scheme.Length..].Trim('/', ' ');
        // Galaxy product ids are plain digits; this lands on a command line, so nothing else passes.
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
        {
            Trace.WriteLine($"[Exec] gog: refusing malformed game id '{id}'");
            return true;
        }
        var client = GogGalaxyExePath();
        if (client is null) { Trace.WriteLine("[Exec] gog: GalaxyClient.exe not found"); return true; }
        try
        {
            Process.Start(new ProcessStartInfo(client, $"/command=runGame /gameId={id}")
                { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(client)! });
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] gog runGame '{id}' failed: {ex.Message}"); }
        return true;
    }

    // ── Storefront client executables (ONE resolver per store) ──────────────────
    // Every one is asked BOTH by GameLibrary.InstalledLaunchers (whether to OFFER the store) and by
    // LaunchStorefront (to open it). Keep it that way — "offered" and "launchable" drifting apart gives a
    // slice that only ever logs "not found". REGISTRY first, fixed Program Files paths only as fallback: a
    // launcher installed to another drive is invisible to a hardcoded path.

    public static string? GogGalaxyExePath()
    {
        foreach (var p in new[] { @"%PROGRAMFILES(X86)%\GOG Galaxy\GalaxyClient.exe",
                                  @"%PROGRAMFILES%\GOG Galaxy\GalaxyClient.exe" })
        {
            var full = Environment.ExpandEnvironmentVariables(p);
            if (File.Exists(full)) return full;
        }
        try   // non-default install dir: Galaxy records its client folder in the registry
        {
            foreach (var key in new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths",
                                        @"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\GalaxyClient\paths" })
                if (Registry.GetValue(key, "client", null) is string dir && !string.IsNullOrWhiteSpace(dir))
                {
                    var p = Path.Combine(dir, "GalaxyClient.exe");
                    if (File.Exists(p)) return p;
                }
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] gog: client-path registry probe failed: {ex.Message}"); }
        return null;
    }

    /// <summary>Steam's client exe. The registry is the primary source (Steam records SteamPath per-user and
    /// InstallPath machine-wide), so a Steam installed outside Program Files still resolves.</summary>
    public static string? SteamExePath()
    {
        try
        {
            foreach (var (key, value) in new[]
            {
                (@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam",              "SteamPath"),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",             "InstallPath"),
            })
                if (Registry.GetValue(key, value, null) is string dir && !string.IsNullOrWhiteSpace(dir))
                {
                    var p = Path.Combine(dir.Replace('/', '\\'), "steam.exe");
                    if (File.Exists(p)) return p;
                }
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] steam: client-path registry probe failed: {ex.Message}"); }
        return FirstExisting(@"%PROGRAMFILES(X86)%\Steam\steam.exe", @"%PROGRAMFILES%\Steam\steam.exe");
    }

    /// <summary>The Epic Games Launcher exe. The install root is recorded in the uninstall registry (a
    /// non-default location is invisible to fixed paths), and the **Win32** binary must be preferred — an
    /// install carrying only Win32 otherwise reads as "Epic not installed".</summary>
    public static string? EpicLauncherExePath()
    {
        static string? Under(string root)
        {
            foreach (var rel in new[] { @"Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
                                        @"Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe" })
            {
                var p = Path.Combine(root, rel);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        try
        {
            if (UninstallRegistry.InstallLocationOf("Epic Games Launcher") is { } root
                && Under(root) is { } fromReg) return fromReg;
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] epic: client-path registry probe failed: {ex.Message}"); }
        foreach (var root in new[] { @"%PROGRAMFILES(X86)%\Epic Games", @"%PROGRAMFILES%\Epic Games" })
            if (Under(Environment.ExpandEnvironmentVariables(root)) is { } p) return p;
        return null;
    }

    /// <summary>Ubisoft Connect's client exe. Registered as "Ubisoft Connect" in the uninstall hive (its
    /// install root), and the modern exe name is UbisoftConnect.exe — upc.exe is the legacy one, kept as a
    /// fallback so an older install still resolves.</summary>
    public static string? UbisoftConnectExePath()
    {
        static string? Under(string root)
        {
            foreach (var exe in new[] { "UbisoftConnect.exe", "upc.exe" })
            {
                var p = Path.Combine(root, exe);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        try
        {
            if (UninstallRegistry.InstallLocationOf("Ubisoft Connect") is { } root
                && Under(root) is { } fromReg) return fromReg;
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] ubisoft: uninstall-registry probe failed: {ex.Message}"); }
        try   // Connect also records its own folder under its Launcher key
        {
            foreach (var key in new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ubisoft\Launcher",
                                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Ubisoft\Launcher" })
                if (Registry.GetValue(key, "InstallDir", null) is string dir && !string.IsNullOrWhiteSpace(dir)
                    && Under(dir.Replace('/', '\\')) is { } p) return p;
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] ubisoft: Launcher-key probe failed: {ex.Message}"); }
        foreach (var root in new[] { @"%PROGRAMFILES(X86)%\Ubisoft\Ubisoft Game Launcher",
                                     @"%PROGRAMFILES%\Ubisoft\Ubisoft Game Launcher" })
            if (Under(Environment.ExpandEnvironmentVariables(root)) is { } p) return p;
        return null;
    }

    /// <summary>The Amazon Games client exe. Registered as "Amazon Games" in the uninstall hive, and its
    /// default location is per-user (%LOCALAPPDATA%), not Program Files.</summary>
    public static string? AmazonGamesExePath()
    {
        try
        {
            if (UninstallRegistry.InstallLocationOf("Amazon Games") is { } root)
            {
                var p = Path.Combine(root, "Amazon Games.exe");
                if (File.Exists(p)) return p;
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] amazon: uninstall-registry probe failed: {ex.Message}"); }
        return FirstExisting(@"%LOCALAPPDATA%\Amazon Games\App\Amazon Games.exe",
                             @"%PROGRAMFILES%\Amazon Games\Amazon Games.exe",
                             @"%PROGRAMFILES(X86)%\Amazon Games\Amazon Games.exe");
    }

    /// <summary>First of <paramref name="paths"/> (env-vars expanded) that exists, or null.</summary>
    private static string? FirstExisting(params string[] paths)
    {
        foreach (var p in paths)
        {
            try
            {
                var full = Environment.ExpandEnvironmentVariables(p);
                if (File.Exists(full)) return full;
            }
            catch (Exception ex) { Trace.WriteLine($"[Exec] path probe failed for '{p}': {ex.Message}"); }
        }
        return null;
    }

    // A "battlenet://<code>" URI is NOT launchable by shell-execute — the client silently ignores it. A
    // game launches via "Battle.net.exe --exec=launch <code>". The battlenet://<code> form stays the
    // slice's stored URL (it doubles as the reconciliation key) and is translated here. Returns true if the
    // url was a battlenet:// one (handled or not).
    private static bool TryLaunchBattleNet(string url)
    {
        const string scheme = "battlenet://";
        if (!url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return false;

        var code   = url[scheme.Length..].Trim('/', ' ');
        if (code.Length == 0) return true;
        // The code lands on Battle.net.exe's command line below, so it must be a product code and nothing
        // else — it can arrive from a Playnite GameId (third-party data), where "s1 --another-flag" would
        // become extra argv entries. Same pattern as the scanner: one definition of "a code".
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, GameLibrary.BattleNetCodePattern))
        {
            Trace.WriteLine($"[Exec] battlenet: refusing malformed product code '{code}'");
            return true;
        }
        // Battle.net has two id spaces and the two launch mechanisms want DIFFERENT ones (see the catalog
        // note in GameLibrary): "--exec=launch" takes the ProductId alias, while the game exe's "-launch
        // -uid" takes the on-disk uid. The incoming code may be either, so resolve both explicitly — never
        // pass one word to both.
        var alias  = GameLibrary.BattleNetProductIdFor(code);
        var info   = GameLibrary.BattleNetLaunchInfo(code);
        var client = BattleNetExePath();

        // Client already up (and logged in): launch the game exe directly the way the client itself
        // does ("<gameExe> -launch -uid <code>") — reliable regardless of UI state, unlike "--exec".
        if (BattleNetRunning())
        {
            if (info is { } g)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(g.Exe, $"-launch -uid {g.Uid}")
                        { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(g.Exe)! });
                    return true;
                }
                catch (Exception ex) { Trace.WriteLine($"[Exec] battlenet exe launch '{code}' failed: {ex.Message}"); }
            }
            // Exe unresolved (a game whose exe != folder name — WoW, Diablo III): defer to the running
            // client's dispatcher.
            if (client is not null) StartBattleNetExec(client, alias);
            return true;
        }

        // Client closed: a game can't launch through it yet, and "--exec=launch" cold is unreliable even
        // with auto-login. Start the client, wait for its window + auto-login to settle, THEN fire the
        // direct exe — in the background so the UI returns at once.
        if (client is null) { Trace.WriteLine("[Exec] battlenet: Battle.net.exe not found"); return true; }
        if (info is { } cg) StartBattleNetColdLaunch(client, cg.Exe, cg.Uid);
        else StartBattleNetExec(client, alias);
        return true;
    }

    /// <summary>Ask a running Battle.net client to launch a product: <c>--exec="launch &lt;ProductId&gt;"</c>.
    ///
    /// <para>The QUOTES are load-bearing: "launch S1" is ONE argument to --exec, so an unquoted
    /// <c>--exec=launch S1</c> reaches the client as two argv entries and isn't the command it asked for.
    /// The id must be the ProductId alias, not the on-disk uid.</para></summary>
    private static void StartBattleNetExec(string clientExe, string productId)
    {
        try
        {
            Process.Start(new ProcessStartInfo(clientExe, $"--exec=\"launch {productId}\"")
                { UseShellExecute = false });
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] battlenet --exec '{productId}' failed: {ex.Message}"); }
    }

    /// <summary>Cold launch: start the client, wait for its window, then fire the game exe directly at
    /// 4/7/10/15s — the spread rides out auto-login finishing. Background, so the caller returns at once.
    /// Without auto-login the client stops at the sign-in screen and the attempts lapse harmlessly.</summary>
    /// <param name="uid">The ON-DISK uid (not the ProductId alias) — that's what "-launch -uid" takes.</param>
    private static void StartBattleNetColdLaunch(string clientExe, string gameExe, string uid)
    {
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(clientExe) { UseShellExecute = true });

                for (int i = 0; i < 75 && !BattleNetWindowUp(); i++)
                    await System.Threading.Tasks.Task.Delay(1000);
                if (!BattleNetWindowUp()) { Trace.WriteLine("[Exec] battlenet cold: client window never appeared"); return; }

                // Stop as soon as the game process shows up, so a landed launch isn't fired twice.
                var proc = Path.GetFileNameWithoutExtension(gameExe);
                int prev = 0;
                foreach (var atSec in new[] { 4, 7, 10, 15 })
                {
                    await System.Threading.Tasks.Task.Delay((atSec - prev) * 1000);
                    prev = atSec;
                    if (AnySessionProcess(proc)) return;   // already launched (in this session)
                    try
                    {
                        Process.Start(new ProcessStartInfo(gameExe, $"-launch -uid {uid}")
                            { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(gameExe)! });
                    }
                    catch (Exception ex) { Trace.WriteLine($"[Exec] battlenet cold exe launch failed: {ex.Message}"); }
                }
            }
            catch (Exception ex) { Trace.WriteLine($"[Exec] battlenet cold launch failed: {ex.Message}"); }
        });
    }

    /// <summary>Session-scoped "is X running" enumeration. Process.GetProcessesByName is MACHINE-WIDE:
    /// with fast user switching, another account's Steam/Discord would otherwise count as "running here",
    /// producing a false sentry leak read and a false Restart-Steam offer. Every caller of this asks about
    /// interactive user apps, so processes outside this session (session-0 services included) are excluded
    /// by design. Reading SessionId across a session boundary can throw under restricted rights — a
    /// per-process failure skips that process, never voids the enumeration. Callers own disposal of the
    /// returned processes.</summary>
    internal static Process[] GetSessionProcessesByName(string name)
    {
        var all = Process.GetProcessesByName(name);
        var mine = new List<Process>(all.Length);
        foreach (var p in all)
        {
            bool keep = false;
            try { keep = p.SessionId == CurrentSessionId; } catch { }
            if (keep) mine.Add(p); else p.Dispose();
        }
        return [.. mine];
    }

    private static readonly int CurrentSessionId = OwnSessionId();
    private static int OwnSessionId() { using var me = Process.GetCurrentProcess(); return me.SessionId; }

    /// <summary>True when at least one process of that name is running in THIS user session.</summary>
    internal static bool AnySessionProcess(string name)
    {
        var ps = GetSessionProcessesByName(name);
        foreach (var p in ps) p.Dispose();
        return ps.Length > 0;
    }

    /// <summary>True once any Battle.net process has a top-level window (the client UI is up).</summary>
    private static bool BattleNetWindowUp()
    {
        var ps = GetSessionProcessesByName("Battle.net");
        bool up = false;
        foreach (var p in ps) { if (p.MainWindowHandle != IntPtr.Zero) up = true; p.Dispose(); }
        return up;
    }

    /// <summary>Whether the Battle.net client is running in this session. The client is multi-process —
    /// several long-lived Battle.net.exe — so any match means it's up (none = fully closed/tray-exited).</summary>
    private static bool BattleNetRunning() => AnySessionProcess("Battle.net");

    /// <summary>Path to Battle.net.exe — from the registered battlenet:// handler command, else the
    /// default install location. Null if Battle.net isn't installed.</summary>
    internal static string? BattleNetExePath()
    {
        if (Registry.GetValue(@"HKEY_CLASSES_ROOT\battlenet\shell\open\command", null, null) is string cmd)
        {
            var m = Regex.Match(cmd, "\"([^\"]+Battle\\.net\\.exe)\"", RegexOptions.IgnoreCase);
            if (m.Success && File.Exists(m.Groups[1].Value)) return m.Groups[1].Value;
        }
        foreach (var p in new[]
                 {
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Battle.net\Battle.net.exe"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Battle.net\Battle.net.exe"),
                 })
            if (File.Exists(p)) return p;
        return null;
    }

    /// <summary>Path to the EA app's launcher exe (<c>EALauncher.exe</c> — the thin bootstrapper the Start
    /// Menu shortcut and the <c>origin2://</c> handler both run; it starts or focuses EADesktop.exe).
    /// <para>Registry FIRST: EA's real install is a VERSIONED folder
    /// (<c>…\EA Desktop\13.759.2.6273\EA Desktop\</c>) behind a sibling DIRECTORY JUNCTION named
    /// <c>EA Desktop</c> that EA retargets on every update. The unversioned path through the junction is
    /// stable enough to keep as a fallback, but only the registry survives a non-default drive.</para>
    /// Null if the EA app isn't installed. This is the SINGLE presence signal: <see
    /// cref="GameLibrary.InstalledLaunchers"/> calls it too, so "offered" and "launchable" can't drift
    /// apart.</summary>
    internal static string? EaAppExePath()
    {
        foreach (var hive in new[]
                 {
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\Electronic Arts\EA Desktop",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop",
                 })
        foreach (var name in new[] { "LauncherAppPath", "ClientPath", "DesktopAppPath" })
            if (Registry.GetValue(hive, name, null) is string p && !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                return p;
        foreach (var p in new[]
                 {
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe"),
                 })
            if (File.Exists(p)) return p;
        return null;
    }

    public bool LaunchStorefront(string key)
    {
        switch (key.ToLowerInvariant())
        {
            case "steam":     OpenUrl("steam://open/bigpicture"); return true;   // true Big Picture
            case "playnite":  return LaunchFirstExisting(                          // dedicated fullscreen exe
                                  [@"%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe"], null);
            case "xbox":      LaunchXboxMaximized(); return true;                 // no Win10 FSE → maximize window
            // Shared resolvers (registry first — see the block above them), so the store OFFERED is the
            // store that opens. The protocol URI is the last resort.
            case "gog":       return LaunchResolved(GogGalaxyExePath(),      "goggalaxy://");
            case "epic":      return LaunchResolved(EpicLauncherExePath(),   "com.epicgames.launcher://");
            case "ubisoft":   return LaunchResolved(UbisoftConnectExePath(), "uplay://");
            case "amazon":    return LaunchResolved(AmazonGamesExePath(),    "amazon-games://");
            case "itch":      return LaunchFirstExisting(BuildItchPaths(), "itch://");
            case "battlenet":
                // Registry protocol handler + standard paths — the same probe the game-launch path uses.
                // Hardcoded paths alone miss real installs.
                if (BattleNetExePath() is { } bn)
                {
                    try { Process.Start(new ProcessStartInfo(bn) { UseShellExecute = true }); return true; }
                    catch (Exception ex) { Trace.WriteLine($"[Exec] battlenet client launch failed: {ex.Message}"); return false; }
                }
                Trace.WriteLine("[Exec] battlenet storefront: client not installed");
                return false;
            case "ea":
                // Registry-discovered exe, no protocol fallback: origin2:// is registered but its handler
                // expects a game argument, so a bare scheme launch won't just open the client.
                if (EaAppExePath() is { } ea)
                {
                    try { Process.Start(new ProcessStartInfo(ea) { UseShellExecute = true }); return true; }
                    catch (Exception ex) { Trace.WriteLine($"[Exec] EA app launch failed: {ex.Message}"); return false; }
                }
                Trace.WriteLine("[Exec] ea storefront: client not installed");
                return false;
            default:          Trace.WriteLine($"[Exec] Unknown launcher: '{key}'"); return false;
        }
    }

    /// <summary>Launch an already-resolved client exe; if it's null (or won't start), fall back to
    /// <paramref name="fallbackUri"/> — a protocol the launcher registers, which opens it without knowing
    /// where it lives. False when neither worked, so the hub can say "Not Installed" rather than no-op.</summary>
    private bool LaunchResolved(string? exe, string? fallbackUri)
    {
        if (exe is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex) { Trace.WriteLine($"[Exec] launcher '{exe}' failed to start: {ex.Message}"); }
        }
        if (!string.IsNullOrWhiteSpace(fallbackUri))
        {
            try { OpenUrl(fallbackUri); return true; }
            catch (Exception ex) { Trace.WriteLine($"[Exec] launcher fallback '{fallbackUri}' failed: {ex.Message}"); }
        }
        Trace.WriteLine("[Exec] launcher: no exe found and no fallback URI");
        return false;
    }

    /// <summary>Launch the first of <paramref name="paths"/> (env-vars expanded) that exists; if none
    /// exist, fall back to <paramref name="fallbackUri"/> (a protocol the launcher registers). Returns
    /// false when nothing could be launched, so the hub can say so instead of silently no-opping.</summary>
    private bool LaunchFirstExisting(string[] paths, string? fallbackUri)
    {
        foreach (var p in paths)
        {
            var full = Environment.ExpandEnvironmentVariables(p);
            if (File.Exists(full))
            {
                Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
                return true;
            }
        }
        if (!string.IsNullOrWhiteSpace(fallbackUri))
        {
            try { OpenUrl(fallbackUri); return true; }
            catch (Exception ex) { Trace.WriteLine($"[Exec] launcher fallback '{fallbackUri}' failed: {ex.Message}"); return false; }
        }
        Trace.WriteLine("[Exec] launcher: no exe found and no fallback URI");
        return false;
    }

    /// <summary>itch installs into versioned <c>%LOCALAPPDATA%\itch\app-&lt;version&gt;\</c> folders — there's
    /// no unversioned <c>app\</c> dir — so resolve the newest <c>app-*</c> dir containing <c>itch.exe</c>
    /// (same glob <see cref="GameLibrary.InstalledLaunchers"/> probes) and try it first, ahead of the
    /// legacy unversioned paths kept here as a fallback.</summary>
    private static string[] BuildItchPaths()
    {
        var paths = new List<string>();
        try
        {
            var itchRoot = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\itch");
            if (Directory.Exists(itchRoot))
            {
                var newest = Directory.EnumerateDirectories(itchRoot, "app-*")
                    .Where(d => File.Exists(Path.Combine(d, "itch.exe")))
                    .OrderByDescending(d => d)
                    .FirstOrDefault();
                if (newest != null) paths.Add(Path.Combine(newest, "itch.exe"));
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] itch: install-folder probe failed: {ex.Message}"); }
        paths.Add(@"%LOCALAPPDATA%\itch\app\itch.exe");
        paths.Add(@"%APPDATA%\itch\app\itch.exe");
        return [.. paths];
    }

    /// <summary>The Xbox PC app has no Win10 Big-Picture mode, so launch it and maximize its window.
    /// Found by the owning PROCESS IMAGE (GamingApp.exe, via the frame window's child CoreWindow — the
    /// top-level window belongs to ApplicationFrameHost). Never match on the window TITLE: that is
    /// English-only and any process can name a window "Xbox"; GamingApp.exe never localizes.</summary>
    private static void LaunchXboxMaximized()
    {
        Process.Start(new ProcessStartInfo(
            @"shell:AppsFolder\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App") { UseShellExecute = true });

        System.Threading.Tasks.Task.Run(() =>
        {
            for (int i = 0; i < 60; i++)   // poll up to ~15s for the window to appear
            {
                System.Threading.Thread.Sleep(250);
                var h = NativeMethods.FindWindowByProcessExe("GamingApp");
                if (h != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(h, NativeMethods.SW_MAXIMIZE);
                    NativeMethods.SetForegroundWindow(h);
                    break;
                }
            }
        });
    }

    public bool SendKeys(string keys) => KeypressSender.TrySend(keys);

    public string? SwitchAudioDevice(string? nameFilter, bool capture) =>
        AudioDeviceSwitcher.Switch(nameFilter, capture ? DataFlow.Capture : DataFlow.Render);

    /// <summary>Gracefully close the frontmost app: WM_CLOSE to the foreground window's root owner —
    /// the same as clicking its ✕, so the app can prompt to save. Skips the desktop/shell (closing
    /// Explorer would be hostile) and our own process (the overlay never takes focus anyway).</summary>
    public string? CloseFrontmostApp()
    {
        var (hwnd, name) = FindEligibleFrontmost();
        if (hwnd == IntPtr.Zero) return null;
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return name;
    }

    public string? PeekFrontmostApp()
    {
        var (hwnd, name) = FindEligibleFrontmost();
        return hwnd == IntPtr.Zero ? null : name;
    }

    /// <summary>The root window + process name an Exit Current App would act on right now — the same
    /// eligibility rules as the close itself (never the desktop/shell/taskbar, never Radiata), so the
    /// armed preview and the fire always agree on the target.</summary>
    private static (IntPtr hwnd, string name) FindEligibleFrontmost()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return default;
        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER);
        if (root != IntPtr.Zero) hwnd = root;

        // A process with no VISIBLE top-level window is never a legitimate target: a background
        // service's transient window can momentarily hold the foreground (GameInputSvc has), and
        // Exit Current App / the sentry's foreground gate must never act on it.
        if (!NativeMethods.IsWindowVisible(hwnd)) return default;

        var cls = new System.Text.StringBuilder(64);
        NativeMethods.GetClassName(hwnd, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return default;   // the desktop/taskbar

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId) return default;

        string name;
        try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName; }
        catch { name = "app"; }
        return (hwnd, name);
    }

    public bool? ToggleMute(bool capture) => Mute(capture, set: true);
    public bool? GetMute(bool capture)    => Mute(capture, set: false);

    /// <summary>Tries each role since e.g. a mic often has only a default Communications endpoint
    /// (GetDefaultAudioEndpoint throws for a role with no default).</summary>
    private static bool? Mute(bool capture, bool set)
    {
        var flow = capture ? DataFlow.Capture : DataFlow.Render;
        foreach (var role in new[] { Role.Multimedia, Role.Communications, Role.Console })
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device     = enumerator.GetDefaultAudioEndpoint(flow, role);
                if (!set) return device.AudioEndpointVolume.Mute;
                bool newMute = !device.AudioEndpointVolume.Mute;
                device.AudioEndpointVolume.Mute = newMute;
                return newMute;
            }
            catch { /* try next role */ }
        }
        return null;
    }

    /// <summary>Nudge the default microphone's level and report the result. Walks the same role fallback as
    /// <see cref="Mute"/> (a mic often has only a Communications default, and asking for a role with no
    /// default throws). There are no mic media keys, so the endpoint scalar IS the only mechanism — no
    /// keybd_event fallback exists. Un-mutes on a raise, so turning the mic up off zero is audible.</summary>
    public float? AdjustMicVolume(float delta)
    {
        foreach (var role in new[] { Role.Multimedia, Role.Communications, Role.Console })
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role);
                var vol   = device.AudioEndpointVolume;
                float now = Math.Clamp(vol.MasterVolumeLevelScalar + delta, 0f, 1f);
                vol.MasterVolumeLevelScalar = now;
                // Once the WRITE has landed, later failures must NOT fall through to the next role: the
                // roles usually resolve to the SAME physical mic, so a retry applies the delta twice (a
                // throwing Mute setter would turn one 5% tap into 10-15%). Un-mute and read-back are
                // best-effort extras on top of a delta that already happened.
                try { if (delta > 0 && now > 0 && vol.Mute) vol.Mute = false; } catch { }
                try { return vol.MasterVolumeLevelScalar; }   // read back — the device may have quantized it
                catch { return now; }
            }
            catch { /* no default endpoint for this role — try the next (the write can't have landed) */ }
        }
        return null;
    }

    public void SetVolume(float level)
    {
        // Set the endpoint scalar directly. The media-key walk below stays a LAST-RESORT fallback — don't
        // promote it back to the primary path: a mistranslated scancode types letters into the focused app
        // (media keys are E0-extended), it spams the Windows volume OSD, and a racing volume change lands
        // it on the wrong level.
        level = Math.Clamp(level, 0f, 1f);
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            // Some devices ignore scalar writes outright — read back and fall through to the key walk if
            // the write didn't take.
            if (Math.Abs(device.AudioEndpointVolume.MasterVolumeLevelScalar - level) <= 0.03f) return;
            Trace.WriteLine("[Action] volume-set: scalar write ignored — falling back to media keys");
        }
        catch (Exception ex) { Trace.WriteLine($"[Action] volume-set scalar failed ({ex.Message}) — falling back to media keys"); }

        // Fallback: walk the level with volume media keys via keybd_event (plain-VK — NOT the SendInput
        // scancode path, whose mistranslation types letters into the focused app).
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            float current = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            int steps = (int)Math.Round(Math.Abs(level - current) / 0.02f);
            byte vk   = level > current ? NativeMethods.VK_VOLUME_UP : NativeMethods.VK_VOLUME_DOWN;
            for (int i = 0; i < steps; i++)
            {
                NativeMethods.keybd_event(vk, 0, 0, IntPtr.Zero);
                NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_KEYUP, IntPtr.Zero);
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Action] volume-set fallback failed: {ex.Message}"); }
    }

    /// <summary>Send a standard media key (play-pause / next / prev / mute) — same mechanism as a
    /// keyboard's media keys, so the OS routes it to whatever player owns media focus.</summary>
    public void MediaKey(string key)
    {
        byte vk = key switch
        {
            "play-pause" => NativeMethods.VK_MEDIA_PLAY_PAUSE,
            "next"       => NativeMethods.VK_MEDIA_NEXT_TRACK,
            "prev"       => NativeMethods.VK_MEDIA_PREV_TRACK,
            "mute"       => NativeMethods.VK_VOLUME_MUTE,
            _            => 0,
        };
        if (vk == 0) { Trace.WriteLine($"[Action] unknown media key '{key}'"); return; }
        NativeMethods.keybd_event(vk, 0, 0, IntPtr.Zero);
        NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    public void Sleep()     => NativeMethods.SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
    /// <summary>Restart — with Automatic Restart Sign-On, then a plain restart if that's refused.
    ///
    /// <para>Radiata CANNOT type a lock-screen PIN (it runs on the user desktop, not the secure desktop
    /// that hosts the sign-in screen), so a bare <c>shutdown /r</c> strands a controller-only user at the
    /// sign-in screen. <c>/g</c> is the same restart plus ARSO: Windows signs the user back in and restores registered
    /// apps, matching the Start-menu restart and what the auto-login setup expects.</para>
    ///
    /// <para>ARSO can be disabled by policy or forbidden by BitLocker/credential settings, in which case
    /// /g fails immediately; the /r fallback keeps the action working.</para></summary>
    public void Reboot(bool signBackIn = true)
    {
        // signBackIn false = the user UNCHECKED "Log In after Reboot": a traditional restart, no ARSO —
        // lands wherever a Start-menu reboot without ARSO would (usually the sign-in screen).
        if (!signBackIn)
        {
            Trace.WriteLine("[Action] reboot: traditional restart ('/r') requested — no sign-back-in");
            TryStart("shutdown.exe", "/r /t 0");
            return;
        }
        if (TryStartChecked("shutdown.exe", "/g /t 0")) return;
        Trace.WriteLine("[Action] reboot: '/g' (restart + sign back in) refused — falling back to '/r'");
        TryStart("shutdown.exe", "/r /t 0");
    }

    public void Lock()      => NativeMethods.LockWorkStation();
    public void Shutdown()  => TryStart("shutdown.exe", "/s /t 0");
    public void Logout()    => TryStart("shutdown.exe", "/l");                      // /l can't take /t
    public void Hibernate() => NativeMethods.SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false);

    /// <summary>Toggle Show Desktop by sending Win+D — the OS's own toggle gesture, which restores windows
    /// to their previous state on the second press. Don't go back to the Shell.Application ToggleDesktop
    /// COM call: from Radiata's process context it completes exception-free and does nothing (the same
    /// call works from a plain PowerShell process). Win+D depends on KeypressSender's extended-key (E0)
    /// scancode flags.</summary>
    public void ToggleShowDesktop()
    {
        if (!KeypressSender.TrySend("Win+D"))
            Trace.WriteLine("[Action] show-desktop: Win+D failed to send");
        else
            Trace.WriteLine("[Action] show-desktop: Win+D sent");
    }

    public void SetDisplayMode(string mode) => TryStart("DisplaySwitch.exe", "/" + mode);

    /// <summary>display-toggle: read the CURRENT topology via QueryDisplayConfig(QDC_DATABASE_CURRENT) and
    /// flip Extend⇄Clone. Indeterminate cases (query failed, single monitor, Internal/External-only, where
    /// "the opposite" isn't defined) fall back to Extend — a display action must DO something visible
    /// rather than no-op — but report null so the hub doesn't claim a state it isn't sure of.</summary>
    public string? ToggleDisplayTopology()
    {
        uint topology = 0;
        try
        {
            int sizeErr = NativeMethods.GetDisplayConfigBufferSizes(
                NativeMethods.QDC_DATABASE_CURRENT, out uint pathCount, out uint modeCount);
            if (sizeErr != 0) { Trace.WriteLine($"[Action] display-toggle: GetDisplayConfigBufferSizes failed ({sizeErr})"); }
            else
            {
                var paths = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];
                int qErr = NativeMethods.QueryDisplayConfig(
                    NativeMethods.QDC_DATABASE_CURRENT, ref pathCount, paths, ref modeCount, modes, out topology);
                if (qErr != 0) { Trace.WriteLine($"[Action] display-toggle: QueryDisplayConfig failed ({qErr})"); topology = 0; }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Action] display-toggle: topology query threw: {ex.Message}"); topology = 0; }

        Trace.WriteLine($"[Action] display-toggle: currentTopology=0x{topology:X}");
        if (topology == NativeMethods.DISPLAYCONFIG_TOPOLOGY_EXTEND) { SetDisplayMode("clone"); return "Clone"; }
        if (topology == NativeMethods.DISPLAYCONFIG_TOPOLOGY_CLONE)  { SetDisplayMode("extend"); return "Extend"; }
        // 0 (query failed), INTERNAL, or EXTERNAL — no well-defined "opposite"; bring a second display in
        // (always safe and visible), but don't claim a specific readout.
        SetDisplayMode("extend");
        return null;
    }

    public void EmptyRecycleBin()
    {
        try
        {
            // rootPath null = all drives; flags suppress the confirm dialog, progress UI, and sound.
            NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null,
                NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
        }
        catch (Exception ex) { Trace.WriteLine($"[Action] empty-recycle-bin failed: {ex.Message}"); }
    }

    public string? SetPowerPlan(string? planGuid)
    {
        if (string.IsNullOrWhiteSpace(planGuid) || !Guid.TryParse(planGuid.Trim(), out var g))
        {
            Trace.WriteLine($"[Action] power-plan: no/invalid plan GUID '{planGuid}'");
            return null;
        }
        try
        {
            // The Power API, not powercfg.exe: the same current-user activation with no UAC prompt, but it
            // answers at once instead of spawning a console tool three times and waiting on each on the UI
            // thread. Confirmed by reading the active scheme back rather than trusting the return code; the
            // friendly name from the same API is what the hub shows ("Balanced", "High performance").
            uint rc = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref g);
            if (rc != 0) { Trace.WriteLine($"[Action] power-plan: PowerSetActiveScheme returned {rc}"); return null; }
            if (ActiveScheme() is not { } now || now != g)
            {
                Trace.WriteLine($"[Action] power-plan: scheme {g:D} not active after PowerSetActiveScheme");
                return null;
            }
            string name = SchemeFriendlyName(g) ?? g.ToString("D");
            Trace.WriteLine($"[Action] power-plan: now '{name}'");
            return name;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Action] power-plan '{planGuid}' failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>The active scheme's GUID from the Power API — microseconds, no process, no UI-thread wait.
    /// <c>PowerPlans.List()</c> (a <c>powercfg /list</c> spawn) stays the editor's picker, off-thread.</summary>
    public string? ActivePowerPlanGuid() => ActiveScheme()?.ToString("D");

    private static Guid? ActiveScheme()
    {
        IntPtr p = IntPtr.Zero;
        try
        {
            if (NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out p) != 0 || p == IntPtr.Zero) return null;
            return System.Runtime.InteropServices.Marshal.PtrToStructure<Guid>(p);
        }
        catch (Exception ex) { Trace.WriteLine($"[Action] PowerGetActiveScheme failed: {ex.Message}"); return null; }
        finally { if (p != IntPtr.Zero) NativeMethods.LocalFree(p); }
    }

    private static string? SchemeFriendlyName(Guid g)
    {
        try
        {
            uint size = 0;
            NativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, null, ref size);
            if (size == 0 || size > 4096) return null;
            var buf = new byte[size];
            if (NativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, buf, ref size) != 0) return null;
            return System.Text.Encoding.Unicode.GetString(buf).TrimEnd('\0');
        }
        catch { return null; }
    }

    /// <summary>Guarded fire-and-forget shell-out for system ops: a locked-down PATH or a Windows edition
    /// missing the exe (e.g. DisplaySwitch.exe) throws Win32Exception — never let it crash a caller.</summary>
    private static void TryStart(string exe, string args)
    {
        try { Process.Start(exe, args); }
        catch (Exception ex) { Trace.WriteLine($"[Action] '{exe} {args}' failed: {ex.Message}"); }
    }

    /// <summary>TryStart, but reports whether the command was ACCEPTED — used where a refused option has a
    /// working fallback (see <see cref="Reboot"/>). "Accepted" = exited 0, or still running after a short
    /// grace (shutdown.exe rejects a bad/forbidden switch instantly with a non-zero code). Bounded at 2 s
    /// because this runs on the UI thread from a slice fire, and a timeout counts as SUCCESS — retrying a
    /// shutdown already underway would be worse.</summary>
    private static bool TryStartChecked(string exe, string args)
    {
        try
        {
            using var p = Process.Start(exe, args);
            if (p is null) return false;
            if (!p.WaitForExit(2000)) return true;      // still running → assume it took
            if (p.ExitCode == 0) return true;
            Trace.WriteLine($"[Action] '{exe} {args}' exited {p.ExitCode}");
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Action] '{exe} {args}' failed: {ex.Message}");
            return false;
        }
    }

    public bool? HdrIsEnabled() => HdrState.IsEnabled();

    public bool? HdrToggle() => HdrState.Toggle();

    public void JoinDiscordVoice(string url) => DiscordIpc.JoinVoiceChannel(url);

    public void ToggleDiscordVoiceSetting(bool deafen) => DiscordIpc.ToggleVoiceSetting(deafen);

    public void LaunchDiscord()
    {
        // Focus a running Discord, else launch via its Update stub (handles the versioned app folder).
        var existing = Process.GetProcessesByName("Discord").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        if (existing is not null) { NativeMethods.ForceForeground(existing.MainWindowHandle); return; }

        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
        var update = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Discord\Update.exe");
        if (File.Exists(update))
            Process.Start(new ProcessStartInfo(update, "--processStart Discord.exe") { UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo("discord://") { UseShellExecute = true });   // protocol fallback
        _ = FocusWhenReadyAsync(null, "Discord");   // Discord spawns its own process; find its window by name
    }

    public bool SendText(string text) => KeypressSender.SendText(text);

    // ── Running-game detection (text-chat "Try Game Default" mode) ───────────────────────────────
    // Same "is the foreground process a game" test AppVolumeMixer uses (exe under a known installed game's
    // InstallDir), anchored to the FOREGROUND window instead of an audio session — a chat slice fires from
    // whatever's in front, audio or not. Deliberately its own cache, not shared with AppVolumeMixer:
    // GameLibrary.Scan() walks up to 7 storefronts, so it must not run on every fire.
    private static (string dir, string name)[] _chatGames = [];
    private static long _chatGamesAt;
    private const long ChatGamesTtlMs = 60_000;
    private static int _chatGamesScanRunning;

    /// <summary>Fill the game-dir cache off-thread ahead of the first <see cref="DetectRunningGameName"/>
    /// call — that first call otherwise scans every storefront INLINE on whatever thread it lands on,
    /// and the pending-passthru-mode-off poll lands it on the dispatcher, where a cold scan is a visible
    /// UI stall. Until the warm-up completes, a caller racing it sees the empty snapshot ("no game
    /// detected") rather than blocking — acceptable for every current caller.</summary>
    public static void WarmGameDetection() =>
        System.Threading.Tasks.Task.Run(RefreshChatGames);

    private static void RefreshChatGames()
    {
        if (_chatGamesAt != 0 && Environment.TickCount64 - _chatGamesAt < ChatGamesTtlMs) return;
        bool firstScan = _chatGamesAt == 0;
        if (System.Threading.Interlocked.Exchange(ref _chatGamesScanRunning, 1) != 0) return;
        if (firstScan) ScanChatGames();          // first call: scan inline so the very first fire can match
        else System.Threading.Tasks.Task.Run(ScanChatGames);   // later refreshes: off-thread, use the stale snapshot meanwhile
    }

    private static void ScanChatGames()
    {
        try
        {
            // Normalised again here, not just at the scanner: this is a PREFIX MATCH against a Win32 image
            // path, so a single stray forward slash from any storefront silently matches nothing.
            _chatGames = [.. GameLibrary.Scan()
                .Where(g => !string.IsNullOrWhiteSpace(g.InstallDir))
                .Select(g => (GameLibrary.NormalizeDir(g.InstallDir!) + "\\", g.Name))];
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] text-chat: game-dir refresh failed: {ex.Message}"); }
        finally
        {
            _chatGamesAt = Environment.TickCount64;
            System.Threading.Interlocked.Exchange(ref _chatGamesScanRunning, 0);
        }
    }

    public string? DetectRunningGameName()
    {
        try
        {
            RefreshChatGames();
            NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint pid);
            if (pid == 0) return null;
            string? path;
            try { path = NativeMethods.ProcessImagePath((int)pid); } catch { return null; }
            if (string.IsNullOrWhiteSpace(path)) return null;
            foreach (var (dir, name) in _chatGames)
                if (path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return name;
            // The near-miss is the interesting case: a bare "no installed game in the foreground" trace
            // reads as "you aren't in a game" when it can just as easily mean the prefixes are malformed.
            // Name the path and the candidate count.
            Trace.WriteLine($"[Exec] text-chat: foreground '{path}' matched none of {_chatGames.Length} game dir(s)");
            return null;
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] text-chat: game detect failed: {ex.Message}"); return null; }
    }

    /// <summary>The sentry's desktop-idle test: true ONLY when there is no foreground window at all or
    /// the foreground is the actual desktop (Progman/WorkerW). Deliberately NOT PeekFrontmostApp — that
    /// answers "what would Exit Current App close?", and its null also covers the taskbar, invisible
    /// shell surfaces, and Radiata's own windows, each of which means the user is actively driving
    /// something (the opposite of idle). Reusing this as an is-it-safe-to-bounce-Steam gate is unsafe: a
    /// tray click makes Radiata itself the foreground, which this test reads as idle, and bouncing Steam
    /// then would take a running game with it.</summary>
    internal static bool DesktopIsIdle()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return true;
        var cls = new System.Text.StringBuilder(64);
        NativeMethods.GetClassName(hwnd, cls, cls.Capacity);
        return cls.ToString() is "Progman" or "WorkerW";
    }

    /// <summary>True when any process in THIS session runs from under a scanned game's install dir —
    /// the sentry's second bounce gate, independent of foreground: killing Steam takes every
    /// Steam-launched game with it, and no foreground test can prove no game is running (minimised,
    /// on another monitor, alt-tabbed). Fails CLOSED: if the sweep itself fails, report a game so the
    /// bounce is withheld — the card is the safe fallback, a killed game is not. Per-process failures
    /// (elevation boundaries, processes exiting mid-scan) skip that process only.</summary>
    public bool GameProcessRunning()
    {
        try
        {
            if (FindRunningGame() is not { } hit) return false;
            Trace.WriteLine($"[Sentry] game process running: {hit.Name} ({hit.Exe}) — Steam must not be bounced");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Sentry] game-process sweep failed ({ex.Message}) — treating as a running game");
            return true;
        }
    }

    /// <summary>Name of any scanned game with a process running in this session, independent of the
    /// foreground — so it still answers while Radiata's tray menu or Settings holds focus. Null when none is
    /// running, and also when the sweep fails: callers use it for advisory copy, where silence is the safe
    /// fallback (unlike <see cref="GameProcessRunning"/>, which fails closed).</summary>
    public string? RunningGameName()
    {
        try { return FindRunningGame()?.Name; }
        catch (Exception ex) { Trace.WriteLine($"[Exec] running-game sweep failed: {ex.Message}"); return null; }
    }

    /// <summary>The first process in this session whose image lives under a scanned game's install dir.
    /// Throws only if the process table itself can't be read; per-process failures (elevation boundaries,
    /// processes exiting mid-scan) skip that process.</summary>
    private (string Name, string Exe)? FindRunningGame()
    {
        RefreshChatGames();
        var dirs = _chatGames;
        if (dirs.Length == 0) return null;   // no scanned library ⇒ nothing prefix-matchable
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.SessionId != CurrentSessionId) continue;
                    var path = NativeMethods.ProcessImagePath(p.Id);
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    foreach (var (dir, name) in dirs)
                        if (path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                            return (name, System.IO.Path.GetFileName(path));
                }
                catch { /* exited mid-scan / access denied — skip this one */ }
            }
        }
        return null;
    }

    /// <summary>Foreground identity as (hwnd, pid) packed into one long. Both halves matter: a game that
    /// recreates its window keeps the pid but changes the hwnd, and a pid can be recycled. 0 when there is
    /// no foreground window or its pid can't be read, which never equals a later valid token.</summary>
    public long ForegroundToken()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return 0;
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return 0;
            return ((long)(uint)hwnd.ToInt64() << 32) | pid;
        }
        catch { return 0; }
    }
}
