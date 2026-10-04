using System.Diagnostics;
using System.Windows;

namespace ControllerWheel;

// Command-line modes: the switches OnStartup tests before the single-instance claim.
public partial class App
{
    /// <summary>The command-line modes in the order they are tested. An entry matches when any of its
    /// switches is present; the first match runs, and the handler receives the index of the first listed
    /// switch found. A handler returns true when it has ended startup (it called Shutdown, or left a mode
    /// running that shuts down later) and false to let startup continue to the next entry.</summary>
    private (string[] Switches, Func<string[], int, bool> Run)[] CliCommands() =>
    [
        (["--logon", StartupManager.AutostartSwitch], CliAutostart),
        (["--uncloak"],                                CliUncloak),
        (["--watchdog"],                               CliWatchdog),
        (["--export-controls"],                        CliExportControls),
        (["--export-help-html"],                       CliExportHelpHtml),
        (["--check-help-locales"],                     CliCheckHelpLocales),
        (["--dump-help-locale"],                       CliDumpHelpLocale),
        (["--dump-ui-locale"],                         CliDumpUiLocale),
        (["--check-locales"],                          CliCheckLocales),
        (["--scan-library"],                           CliScanLibrary),
        (["--export-picker"],                          CliExportPicker),
        (["--export-mypicks"],                         CliExportMyPicks),
        (["--hdr-probe"],                              CliHdrProbe),
        (["--remove-recovery-task"],                   CliRemoveRecoveryTask),
        (["--hidhide-whitelist"],                      CliHidHideWhitelist),
        (["--install-drivers"],                        CliInstallDrivers),
        (["--uninstall", "--uninstall-run"],           CliUninstall),
        (["--uninstall-cleanup", "--uninstall-cleanup-run"], CliUninstallCleanup),
    ];

    /// <summary>Runs the command-line modes in table order. True = startup is over; OnStartup returns.</summary>
    private bool RunCommandLine(string[] args)
    {
        foreach (var (switches, run) in CliCommands())
        {
            int at = -1;
            foreach (var s in switches)
                if ((at = Array.IndexOf(args, s)) >= 0) break;
            if (at >= 0 && run(args, at)) return true;
        }
        return false;
    }

    // Autostart launches, two routes (docs/INSTALLER.md ▸ Autostart): `--logon` is the logon scheduled
    // task (RecoveryTask), fired seconds after sign-in; `--autostart` is the Run-key entry, which
    // Explorer reaches minutes later. Both defer to the Run key as the ON/OFF switch and to Windows'
    // own Startup-apps toggle (Explorer honours it for the Run key; the task must ask). A launch that
    // finds autostart off, a deliberate quit earlier in this logon session (QuitMarker), or a primary
    // already up exits before tracing or the mutex, with NO show-request — a second boot-time launch
    // must never pop Settings. All of this is decided before the single-instance claim.
    private bool CliAutostart(string[] args, int at)
    {
        bool fromTask = Array.IndexOf(args, "--logon") >= 0;
        // ⚠ Every branch below exits before the primary's tracing exists, so a wrong decision here —
        // including the stranded-cloak heal silently not happening — leaves no evidence at all, on a
        // path whose failure costs the user an invisible controller. Trace to a SIBLING file: a live
        // primary holds radiata-trace.log write-locked, the same reason the watchdog has its own.
        // ⚠ Held so it can be REMOVED again: this listener covers the startup DECISION only. Left
        // attached, a launch that goes on to start the app mirrors that whole session — every
        // [Emu] line of it — into this file for the process's lifetime.
        TimestampTraceListener? decisionLog = null;
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            decisionLog = new TimestampTraceListener(
                System.IO.Path.Combine(AppPaths.AppDataDir, "radiata-trace-logon.log"), shortLived: true);
            Trace.Listeners.Add(decisionLog);
            Trace.AutoFlush = true;
        }
        catch { /* diagnostics must never block a startup decision */ }

        bool runKeyOn = StartupManager.IsEnabled, windowsOff = StartupManager.WindowsStartupDisabled;
        bool autostartOn = runKeyOn && !windowsOff;
        Trace.WriteLine($"[Autostart] {(fromTask ? "logon task" : "Run key")} launch — runKey={runKeyOn} "
                        + $"windowsStartupOff={windowsOff} → autostart {(autostartOn ? "ON" : "OFF")}");
        // Every branch closes the decision listener itself: a process that exits inside the listener's
        // retry window would otherwise take an unflushed backlog with it, and the heal branch is the one
        // whose evidence matters most.
        void CloseDecisionLog()
        {
            if (decisionLog is null) return;
            Trace.Listeners.Remove(decisionLog);
            decisionLog.Dispose();
            decisionLog = null;
        }
        if (!autostartOn)
        {
            // The task is also the stranded-cloak safety net, so with autostart off it still heals.
            _earlyExit = true;
            if (fromTask) RunUncloakOneShot();
            else Trace.WriteLine("[Autostart] Run-key launch, autostart off — exiting; the heal is the task's job");
            CloseDecisionLog();
            Shutdown(0);
            return true;
        }
        bool primaryUp = false;
        try { if (System.Threading.Mutex.TryOpenExisting(InstanceMutexName, out var m)) { m.Dispose(); primaryUp = true; } }
        catch { /* treat as absent — the mutex claim below still arbitrates */ }
        bool quitMarker = QuitMarker.MatchesCurrentSession();
        if (primaryUp || quitMarker)
        {
            // ⚠ This branch does NOT heal a stranded cloak: a primary that is up owns its own, and a
            // deliberate quit un-cloaked on the way out. Logged because from the outside it looks
            // identical to the healing branch — nothing starts either way.
            _earlyExit = true;
            Trace.WriteLine($"[Autostart] exiting without starting — primaryUp={primaryUp} quitMarker={quitMarker}");
            CloseDecisionLog();
            Shutdown(0);
            return true;
        }
        Trace.WriteLine("[Autostart] starting the app");
        // Decision made and the app is going to run: hand tracing back to the session log
        // (SetupFileTrace) and close this one, or it mirrors the entire session into it.
        CloseDecisionLog();
        _autostartRoute = fromTask ? "logon task" : "Run key";
        return false;
    }

    // Orphan-cloak recovery one-shot: lift any cloak a prior session left stranded, no-op if the app
    // is running. Kept for logon tasks registered before `--logon` existed; those re-register on the
    // next elevated whitelist pass. HidHide 1.5 list writes work unelevated.
    private bool CliUncloak(string[] args, int at)
    {
        _earlyExit = true;
        RunUncloakOneShot();
        Shutdown(0);
        return true;
    }

    // Ride-along watchdog one-shot, self-spawned by the primary as `--watchdog <pid> <startFileTimeUtc>`:
    // wait for the primary to die, then lift any cloak it left stranded. Covers the one hole the
    // in-process crash hooks can't — a hard kill (Task Manager, OOM) fires no hook, and without this
    // the pad stays cloaked until the next Radiata launch or sign-in. Unprivileged (HidHide 1.5 list
    // writes) and deliberately dumb: it detects DEATH, not malfunction — wedged-state detection (zombie
    // input links, stuck emulation) needs HID access and stays in the primary. Runs before the
    // single-instance claim; never claims the mutex. Dev escape hatch if ever needed: gate the spawn
    // site on a RADIATA_NO_WATCHDOG env var (deliberately not implemented — no config kill-switch).
    private bool CliWatchdog(string[] args, int wdIdx)
    {
        _earlyExit = true;   // TearDown must never touch the primary's cloaked-ids.txt from this instance
        if (wdIdx + 2 >= args.Length
            || !int.TryParse(args[wdIdx + 1], out int parentPid)
            || !long.TryParse(args[wdIdx + 2], out long parentStart))
        { Shutdown(1); return true; }

        // The primary holds radiata-trace.log write-locked — trace to a sibling file.
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            Trace.Listeners.Add(new TimestampTraceListener(
                System.IO.Path.Combine(AppPaths.AppDataDir, "radiata-trace-watchdog.log"), shortLived: true));
            Trace.AutoFlush = true;
        }
        catch { /* diagnostics must never block the safety net */ }

        // Per-pid identity mutex: how AnotherRadiataRunning tells a watchdog from a primary. Without
        // it a lingering watchdog (same exe path) would make the logon --uncloak task wrongly no-op
        // as "primary running" and re-strand the cloak. Held for the process lifetime.
        var identity = new System.Threading.Mutex(initiallyOwned: true, WatchdogMutexName(Environment.ProcessId));

        ShutdownMode = ShutdownMode.OnExplicitShutdown;   // no windows — only the explicit Shutdown ends us
        var wd = new System.Threading.Thread(() =>
        {
            try
            {
                try
                {
                    // pid + start time pins the identity: a recycled pid has a different start time.
                    // Once GetProcessById succeeds, WaitForExit holds a real handle — no recycle risk.
                    using var parent = System.Diagnostics.Process.GetProcessById(parentPid);
                    if (parent.StartTime.ToFileTimeUtc() == parentStart) parent.WaitForExit();
                }
                catch { /* parent already gone (or unreadable) → treat as dead */ }

                // Settle so a restart-in-place successor can claim the mutex and the kernel finishes
                // tearing down the dead instance's driver handles (same rationale as the settle in
                // InitInputCaptureDeferred). Then the same guard ladder as the --uncloak one-shot.
                System.Threading.Thread.Sleep(1500);

                if (System.Threading.Mutex.TryOpenExisting(InstanceMutexName, out var m))
                { m.Dispose(); Trace.WriteLine("[Watchdog] primary mutex live — exiting"); }
                else if (AnotherRadiataRunning())
                    Trace.WriteLine("[Watchdog] a live primary process exists — exiting");
                else if (HidHideManager.PersistedCloakedIds() is not { Count: > 0 } ids)
                    Trace.WriteLine("[Watchdog] nothing cloaked — exiting");
                else
                {
                    Trace.WriteLine($"[Watchdog] primary died with {ids.Count} cloaked id(s) — lifting the cloak");
                    try { new HidHideManager().Unhide(ids); }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[Watchdog] un-cloak failed: {ex.Message} — the startup " +
                                        "self-heal / logon recovery task will finish the job");
                    }
                }
            }
            finally
            {
                GC.KeepAlive(identity);   // the identity mutex must outlive every guard check above
                Dispatcher.Invoke(() => Shutdown(0));
            }
        }) { IsBackground = true, Name = "RadiataWatchdog" };
        wd.Start();
        return true;
    }

    // Dev/doc mode: regenerate CONTROLS.md from the in-app Help content (Core/HelpContent.cs is the
    // canonical source; the doc is a build artifact). `Radiata.exe --export-controls [path]` — path
    // defaults to CONTROLS.md in the current directory. Exits immediately; no tray app.
    private bool CliExportControls(string[] args, int exportIdx)
    {
        _earlyExit = true;
        int code = 0;
        try
        {
            var path = exportIdx + 1 < args.Length && !args[exportIdx + 1].StartsWith("--")
                ? args[exportIdx + 1] : "CONTROLS.md";
            System.IO.File.WriteAllText(path, HelpContent.ExportControlsMarkdown());
        }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // Dev/doc mode: the whole Help system as an upload-ready static site, one page per language.
    // `Radiata.exe --export-help-html [dir] [--css-version=N]` (default site-help\). Same source as
    // --export-controls, a different surface — see Core/HelpHtmlExport.cs and docs/LOCALIZATION.md.
    // Without --css-version the stylesheet cache-buster is read from the site's own root index.html
    // when the target sits one level under it.
    private bool CliExportHelpHtml(string[] args, int htmlIdx)
    {
        _earlyExit = true;
        int code = 0;
        try
        {
            var dir = htmlIdx + 1 < args.Length && !args[htmlIdx + 1].StartsWith("--")
                ? args[htmlIdx + 1] : "site-help";
            int? css = null;
            foreach (var a in args)
                if (a.StartsWith("--css-version=", StringComparison.Ordinal) && int.TryParse(a["--css-version=".Length..], out var n)) css = n;
            HelpHtmlExport.Write(dir, css);
        }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // Dev/doc mode: Help-translation parity report. `Radiata.exe --check-help-locales [path]` writes
    // the report (default help-locales.txt in the current directory) listing, per language, the strings
    // still awaiting translation and the stale entries whose English source has changed or gone. Run it
    // after every HelpContent.cs edit — see docs/LOCALIZATION.md. Exits immediately; no tray app.
    private bool CliCheckHelpLocales(string[] args, int checkIdx)
    {
        _earlyExit = true;
        int code = 0;
        try
        {
            var path = checkIdx + 1 < args.Length && !args[checkIdx + 1].StartsWith("--")
                ? args[checkIdx + 1] : "help-locales.txt";
            System.IO.File.WriteAllText(path, HelpLocalization.CheckReport());
        }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // Dev/doc mode: one language's Help map regenerated as C# source in SourceStrings() order, keeping
    // every translation whose English key still exists and leaving a blank TODO where it doesn't.
    // `Radiata.exe --dump-help-locale <code> [path]` (default help-locale-<code>.cs in the current
    // directory; copy over Core\HelpText<Xx>.cs once filled). UTF-8 with BOM, like the maps it replaces.
    // The regeneration path for a heavily drifted map — see docs/LOCALIZATION.md §2.
    private bool CliDumpHelpLocale(string[] args, int dumpIdx)
    {
        _earlyExit = true;
        int code = 0;
        try
        {
            var lang = dumpIdx + 1 < args.Length && !args[dumpIdx + 1].StartsWith("--")
                ? args[dumpIdx + 1] : throw new ArgumentException("--dump-help-locale needs a language code");
            var path = dumpIdx + 2 < args.Length && !args[dumpIdx + 2].StartsWith("--")
                ? args[dumpIdx + 2] : $"help-locale-{lang}.cs";
            System.IO.File.WriteAllText(path, HelpLocalization.Skeleton(lang), new System.Text.UTF8Encoding(true));
        }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // --dump-ui-locale <code> [path]: the UI map skeleton for one language (Core/UiText<Xx>.cs), in catalog
    // order, carrying existing translations and TODO for the rest — the Help dump's twin (docs/LOCALIZATION.md §2).
    private bool CliDumpUiLocale(string[] args, int uiDumpIdx)
    {
        _earlyExit = true;
        int code = 0;
        try
        {
            var lang = uiDumpIdx + 1 < args.Length && !args[uiDumpIdx + 1].StartsWith("--")
                ? args[uiDumpIdx + 1] : throw new ArgumentException("--dump-ui-locale needs a language code");
            var path = uiDumpIdx + 2 < args.Length && !args[uiDumpIdx + 2].StartsWith("--")
                ? args[uiDumpIdx + 2] : $"ui-locale-{lang}.cs";
            var appSide = ColorNamer.AllKeys()
                .Concat(WheelEditorControl.Categories.SelectMany(c => new[] { c.Header }
                    .Concat(c.Entries.SelectMany(en => new[] { en.Display }.Concat(en.Options.Select(o => o.Display))))));
            System.IO.File.WriteAllText(path, Loc.Skeleton(lang, appSide), new System.Text.UTF8Encoding(true));
        }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // Dev/doc mode: BOTH parity reports — the Help maps and the UI maps — in one file (default
    // locales.txt in the current directory). The UI half needs the string sets only the shell can
    // enumerate; they are passed in here. Exits immediately; no tray app. docs/LOCALIZATION.md §2.
    private bool CliCheckLocales(string[] args, int locIdx)
    {
        _earlyExit = true;
        int code = 0;
        try
        {
            var path = locIdx + 1 < args.Length && !args[locIdx + 1].StartsWith("--")
                ? args[locIdx + 1] : "locales.txt";
            var appSide = ColorNamer.AllKeys()
                .Concat(WheelEditorControl.Categories.SelectMany(c => new[] { c.Header }
                    .Concat(c.Entries.SelectMany(en => new[] { en.Display }.Concat(en.Options.Select(o => o.Display))))));
            System.IO.File.WriteAllText(path, HelpLocalization.CheckReport() + Environment.NewLine + Loc.CheckReport(appSide));
        }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // Dev/diagnostic mode: `Radiata.exe --scan-library [path]` writes what the storefront scanners
    // actually see on this machine (default library-scan.txt in the current directory) — resolved
    // launcher exes, per-store game lists with launch URLs and install dirs, and the Playnite side.
    // A scanner is only as correct as the machine it ran on, so this is how a scanner change gets
    // checked against a real library instead of by opening the Game Grid and squinting.
    // Exits immediately; no tray app, no capture engine.
    private bool CliScanLibrary(string[] args, int scanIdx)
    {
        _earlyExit = true;
        int scanCode = 0;
        try
        {
            var path = scanIdx + 1 < args.Length && !args[scanIdx + 1].StartsWith("--")
                ? args[scanIdx + 1] : "library-scan.txt";
            // Capture the scanners' own Trace output into the report: the interesting cases are the
            // ones that DON'T produce a game (a skipped soundtrack, a stale GOG entry, an itch DB
            // query that failed and fell back, an unresolved ms-resource name), and each of those is a
            // trace line. This mode runs long before the normal log listener is installed.
            var traced = new System.IO.StringWriter();
            var listener = new TextWriterTraceListener(traced);
            Trace.Listeners.Add(listener);
            string report;
            try { report = GameLibrary.DiagnosticReport(); }
            finally { Trace.Flush(); Trace.Listeners.Remove(listener); }
            report += Environment.NewLine + "Trace output" + Environment.NewLine
                    + "------------" + Environment.NewLine
                    + (traced.ToString() is { Length: > 0 } t ? t : "(none)" + Environment.NewLine);
            System.IO.File.WriteAllText(path, report);
            Console.WriteLine(report);
            Console.WriteLine($"(written to {System.IO.Path.GetFullPath(path)})");
        }
        catch (Exception ex) { Console.WriteLine("Library scan failed: " + ex); scanCode = 1; }
        Shutdown(scanCode);
        return true;
    }

    // RADIATA PICKER (dev-only, removal map in RadiataPicker.cs): `Radiata.exe --export-picker` writes
    // picker-export.json and CuratedArt.g.cs to the app-data folder. Loads just enough config for the
    // SGDB key/style, no tray app, no capture engine.
    private bool CliExportPicker(string[] args, int at)
    {
        _earlyExit = true;
        int pickerCode = 0;
        try
        {
            var cfg = new ConfigLoader(System.Threading.SynchronizationContext.Current!, SecretField.ProtectBackup, SecretField.ReadBackup);
            GameArt.SteamGridDbKey   = SecretField.ToPlaintext(cfg.Current.System.SteamGridDbKey);
            GameArt.SteamGridDbStyle = cfg.Current.System.SgdbStyle;
            var (n, path) = RadiataPicker.ExportAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Exported {n} game pick(s) to: {path}");
        }
        catch (Exception ex) { Console.WriteLine("Export failed: " + ex.Message); pickerCode = 1; }
        Shutdown(pickerCode);
        return true;
    }

    // OWNER PICKS (dev-only, see OwnerPicksExport.cs): `Radiata.exe --export-mypicks [--only="A;B"]` —
    // bakes the author's own Game Grid Select/Start choices for games OUTSIDE the Picker's Top-100 into
    // CuratedArtOwner.g.cs, so they ship as defaults alongside it. `--only` scopes it to named games
    // (semicolon-separated, punctuation/case-insensitive); without it EVERY saved pick is baked, which
    // includes cycles that were only browsing. Offline: it maps saved picks back to URLs from the on-disk
    // candidate-URL caches and never calls SteamGridDB (hence no key/style setup here).
    private bool CliExportMyPicks(string[] args, int at)
    {
        _earlyExit = true;
        int mineCode = 0;
        try
        {
            var only = args.FirstOrDefault(a => a.StartsWith("--only=", StringComparison.OrdinalIgnoreCase))
                             ?["--only=".Length..]
                              .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var (n, skipped, rematched, path, unmatched, carried, dropped) =
                OwnerPicksExport.ExportAsync(only).GetAwaiter().GetResult();
            Console.WriteLine($"Baked {n} owner pick(s) to: {path}");
            if (carried > 0)
                Console.WriteLine($"({carried} shipped entry/entries carried through unchanged.)");
            if (unmatched.Count > 0)
                Console.WriteLine($"(no installed game matched: {string.Join(", ", unmatched)})");
            // An unscoped rebuild emits only what's picked TODAY, so a trimmed cover-overrides.json
            // silently deletes shipped entries. Name them: this is the one output that can lose work.
            if (dropped.Count > 0)
                Console.WriteLine($"⚠ DROPPED {dropped.Count} shipped entry/entries with no current pick: "
                                  + string.Join(", ", dropped)
                                  + "\n  Re-run with --only=\"<games>\" to re-bake just those and keep the rest.");
            if (rematched > 0)
                Console.WriteLine($"({rematched} legacy cover(s) had drifted out of position and were " +
                                  "re-matched by artwork.)");
            if (skipped > 0)
                Console.WriteLine($"({skipped} pick(s) skipped — the artwork couldn't be verified against any " +
                                  "current candidate, or there's no cached candidate-URL list; open the Game " +
                                  "Grid to let the art prefetch repopulate it, then re-run.)");
            Console.WriteLine("Copy that file over the repo's CuratedArtOwner.g.cs to ship it.");
        }
        catch (Exception ex) { Console.WriteLine("Export failed: " + ex.Message); mineCode = 1; }
        Shutdown(mineCode);
        return true;
    }

    // HDR diagnostic (G11): `Radiata.exe --hdr-probe [on [modeset] [seconds] | off]` — a
    // self-reverting joint-test harness for the TV-blackout bug; see HdrProbe. Exits immediately.
    private bool CliHdrProbe(string[] args, int hdrIdx)
    {
        _earlyExit = true;
        int hdrCode;
        try { hdrCode = HdrProbe.Run(args, hdrIdx); }
        catch { hdrCode = 1; }
        Shutdown(hdrCode);
        return true;
    }

    private bool CliRemoveRecoveryTask(string[] args, int at)
    {
        _earlyExit = true;
        int code;
        try { code = RecoveryTask.Remove(allowElevation: false, ownerSid: RecoveryTask.OwnerSid) ? 0 : 1; }
        catch { code = 1; }
        Shutdown(code);
        return true;
    }

    // Elevated one-shot: add Radiata.exe to HidHide's allow-list, then exit. Runs BEFORE the
    // single-instance check (this is a short-lived elevated sibling of the running app spawned by
    // WhitelistSelf, not a duplicate tray app). The caller releases the exclusive device first.
    private bool CliHidHideWhitelist(string[] args, int at)
    {
        _earlyExit = true;   // this instance never initializes capture — TearDown must not touch it
        // The primary holds radiata-trace.log write-locked, so the elevated one-shot's [HidHide]
        // diagnostics (whitelist result, per-device restart exit codes) go to the sibling file.
        AttachElevatedTrace();
        int code = 1;
        try
        {
            code = new HidHideManager().WhitelistApplication(Environment.ProcessPath ?? "") ? 0 : 1;
            // A freshly installed HidHide CLASS filter only enters a device's stack when that device
            // (re)starts (installer runs /norestart) — restart pads while we're elevated so the cloak
            // works immediately instead of silently doing nothing until a Windows reboot.
            // The caller says WHAT to restart via "--restart-pads <base64 ids | sweep | none>":
            // explicit ids = just the physical pad — never the game's VIRTUAL pad (restarting it is a
            // guaranteed mid-game disconnect); "sweep" = VID-0x054C fallback, sent only on a fresh
            // HidHide install/upgrade with no pad connected; "none"/absent = skip.
            int ri = Array.IndexOf(args, "--restart-pads");
            if (ri >= 0 && ri + 1 < args.Length && args[ri + 1] != "none")
            {
                IReadOnlyList<string>? ids = null;
                if (args[ri + 1] != "sweep")
                    try
                    {
                        ids = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(args[ri + 1]))
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    }
                    catch (FormatException) { throw new ArgumentException("Invalid restart-pad list; no devices restarted."); }
                HidHideManager.RestartPadDevices(ids);
            }
            // We're elevated here (a logon trigger needs admin to create) — (re)register the logon task:
            // the autostart route that predates Steam, and the stranded-cloak heal when autostart is
            // off. Idempotent; best-effort.
            if (!RecoveryTask.EnsureRegistered(Environment.ProcessPath ?? "")) code = 1;
        }
        // Best-effort, but NOT silent: this is the elevated pass that heals a stranded cloak at sign-in,
        // and a failure here means a hard-killed session can leave the physical pad hidden with nothing
        // scheduled to fix it — the worst failure mode the app has.
        catch (Exception ex)
        {
            code = 1;
            Trace.WriteLine($"[Recovery] elevated pad-restart / logon-task registration FAILED: {ex.Message} " +
                            "— a stranded cloak may not self-heal at next sign-in");
        }
        Shutdown(code);
        return true;
    }

    // Installer post-install task (`Radiata.exe --install-drivers`, run elevated by the setup
    // wizard's optional task): the first-run driver engine behind a minimal standalone progress
    // window, no tray app. Also runnable by hand — relaunches itself elevated when it isn't
    // (driver installs + the recovery-task registration need admin; one UAC prompt up front).
    // Must work on a machine Radiata has never run on: nothing here reads or creates config.
    // Exit codes: 0 = drivers current/installed, 1 = incomplete/failed, 3 = elevation declined.
    private bool CliInstallDrivers(string[] args, int at)
    {
        _earlyExit = true;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // A primary instance may hold radiata-trace.log write-locked — trace to a sibling file.
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            Trace.Listeners.Add(new TimestampTraceListener(
                System.IO.Path.Combine(AppPaths.AppDataDir, "radiata-trace-drivers.log"), shortLived: true));
            Trace.AutoFlush = true;
        }
        catch { /* diagnostics must never block driver setup */ }

        if (!IsProcessElevated())
        {
            int relayCode = 1;
            try
            {
                // WorkingDirectory: not the install folder (a child holding it as CWD blocks uninstall)
                // and never user-writable — an ELEVATED child's CWD is on the DLL search path, so %TEMP%
                // here would let same-user code ride a UAC approval to admin (DLL planting).
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    Environment.ProcessPath ?? "", $"--install-drivers --owner-sid {RecoveryTask.OwnerSid}")
                    { UseShellExecute = true, Verb = "runas", WorkingDirectory = Environment.SystemDirectory });
                if (p is not null) { p.WaitForExit(); relayCode = p.ExitCode; }
            }
            catch (Exception ex)   // UAC declined / elevation unavailable
            {
                Trace.WriteLine($"[DriverSetup] --install-drivers elevation declined/failed: {ex.Message}");
                relayCode = 3;
            }
            Shutdown(relayCode);
            return true;
        }

        // Both drivers already current (and no conflicting legacy driver) → no install to do or show. Still
        // do the elevated half of the engine's work while this process holds the elevation: the drivers are
        // machine-wide and survive a Radiata uninstall, so a reinstall always lands here, and this is the
        // only elevated pass a fresh install gets — skipping it leaves no logon task (a logon trigger needs
        // admin to create) and so no stranded-cloak heal at sign-in.
        if (_emulator.ProbeDriverInstalled() && DriverStatus.HidHideInstalled()
            && !HidHideUpdateAvailable() && !DriverStatus.HidGuardianInstalled()
            && DriverSetup.SupportedOS && DriverStatus.ForeignViGEmBusName() is null)
        {
            Trace.WriteLine("[DriverSetup] --install-drivers: ViGEmBus + HidHide already current — nothing to install");
            string exe = Environment.ProcessPath ?? "";
            bool recoveryReady = false;
            try
            {
                bool listed = new HidHideManager().WhitelistApplication(exe);
                bool task   = RecoveryTask.EnsureRegistered(exe);
                recoveryReady = listed && task;
                Trace.WriteLine($"[DriverSetup] elevated pass: HidHide allow-list {(listed ? "ok" : "FAILED")}, " +
                                $"logon task {(task ? "registered" : "FAILED — a stranded cloak may not self-heal at next sign-in")}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DriverSetup] elevated pass failed: {ex.Message} — a stranded cloak may not self-heal at next sign-in");
            }
            Shutdown(recoveryReady ? 0 : 1);
            return true;
        }

        var driverWin = new DriverInstallWindow();
        driverWin.Show();
        _ = RunInstallDriversAsync(driverWin);   // ends with Shutdown(code)
        return true;
    }

    // Portable uninstall: show opt-ins before modifying Windows state or scheduling file removal.
    // Keep per-user cleanup in the invoking account. Driver uninstallers and task removal
    // elevate only their own privileged operation; there is no whole-app elevated cleanup child.
    private bool CliUninstall(string[] args, int at)
    {
        _earlyExit = true;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        bool direct = Array.IndexOf(args, "--uninstall-run") >= 0;
        var choice = direct
            ? (Drivers: Array.IndexOf(args, "--drivers") >= 0, AppData: Array.IndexOf(args, "--appdata") >= 0)
            : UninstallDialog.Prompt();
        int code = 0;
        if (choice is { } c)
        {
            AttachElevatedTrace();
            try { code = RunUninstall(c.Drivers, c.AppData) ? 0 : 5; }
            catch (Exception ex) { Trace.WriteLine($"[Uninstall] failed: {ex.Message}"); code = 1; }
        }
        Shutdown(code);
        return true;
    }

    // Inno waits for this process itself. A cleanup failure must abort before its file pass.
    private bool CliUninstallCleanup(string[] args, int at)
    {
        _earlyExit = true;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        bool silent = Array.IndexOf(args, "--silent") >= 0;
        bool direct = Array.IndexOf(args, "--uninstall-cleanup-run") >= 0;
        var choice = direct
            ? (Drivers: Array.IndexOf(args, "--drivers") >= 0, AppData: Array.IndexOf(args, "--appdata") >= 0)
            : silent ? (Drivers: false, AppData: false)
            : UninstallDialog.Prompt(cleanupOnly: true) ?? (Drivers: false, AppData: false);
        AttachElevatedTrace();
        int code = 1;
        try
        {
            var (ok, message) = RunCleanupCore(choice.Drivers, choice.AppData);
            code = ok ? 0 : 5;
            if (!silent && (!ok || choice.Drivers))
                System.Windows.MessageBox.Show((ok ? Loc.T(UiText.Dialogs.CleanupFinished) : "") + message,
                    Loc.T(UiText.Dialogs.UninstallCaption), MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { Trace.WriteLine($"[Uninstall] cleanup failed: {ex.Message}"); }
        Shutdown(code);
        return true;
    }
}
