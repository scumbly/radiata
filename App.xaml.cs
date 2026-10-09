using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using NAudio.CoreAudioApi;

namespace ControllerWheel;

public partial class App : Application
{
    // ── Tray ──────────────────────────────────────────────────────────────────
    private NotifyIcon?     _trayIcon;

    // ── Single instance ─────────────────────────────────────────────────────────
    private const string InstanceMutexName = @"Local\Radiata.SingleInstance";
    private const string ShowEventName     = @"Local\Radiata.ShowSettings";
    // Signalled by the installer (radiata.iss PrepareToInstall) so a running tray exits through ExitApp
    // before any file is replaced. The name is duplicated there.
    private const string QuitEventName     = @"Local\Radiata.QuitRequest";
    private System.Threading.EventWaitHandle?    _quitRequest;
    private System.Threading.RegisteredWaitHandle? _quitWait;
    private System.Threading.Mutex?              _instanceMutex;
    private System.Threading.EventWaitHandle?    _showRequest;
    private System.Threading.RegisteredWaitHandle? _showWait;
    private SettingsWindow? _settingsWindow;
    private bool _shuttingDown;
    // Early-exit launches (duplicate instance, elevated one-shot) never initialize capture — their
    // TearDown must skip the emulator/HidHide teardown, whose empty-state Unhide would delete the
    // PRIMARY instance's live cloak-recovery file (cloaked-ids.txt).
    private bool _earlyExit;
    /// <summary>Set when this launch came from an autostart route ("logon task" / "Run key"); null for a
    /// manual launch. Decides whether a lost single-instance race may ask the primary to show itself.</summary>
    private string? _autostartRoute;

    // ── Config ────────────────────────────────────────────────────────────────
    private ConfigLoader   _config   = null!;
    // Folder / .zip paths this launch was handed (a drop onto Radiata.exe or a shortcut to it); the drop-in
    // package inventory itself lives in PackageInstallFlow.
    private IReadOnlyList<string> _launchPackagePaths = [];
    private System.Threading.EventWaitHandle?    _installRequest;
    private System.Threading.RegisteredWaitHandle? _installWait;
    private ActionExecutor _executor = null!;
    // Passed into _executor's constructor, and also called directly by App (foreground/game detection,
    // media keys, mic volume) — one instance backs both.
    private readonly WindowsPlatformActions _platform = new();

    // ── Controller ────────────────────────────────────────────────────────────
    private readonly ControllerReader _controller = new();

    // The always-on virtual pad (ViGEmBus): DualShock 4 in native mode, Xbox 360 in XBox Mode —
    // the XBox Mode slice only swaps the pad type, never turns emulation off.
    private readonly GamepadEmulator _emulator = new();

    // Hides the physical DualSense from other apps (HidHide) whenever a virtual pad is up, so games
    // see only the virtual DS4/X360 pad — the only reliable cure for double-input.
    private readonly HidHideManager _hidHide = new();
    // Instance IDs CONFIRMED written to HidHide's block list (outcome, not intent — Hide() reports
    // success). Empty ⇒ not cloaked. UpdateInputCapture re-attempts whenever the connected pad has ids
    // not in this set, so a pad that connects late / on a new transport still gets cloaked.
    private readonly HashSet<string> _cloakedIds = new(StringComparer.OrdinalIgnoreCase);
    // Subset of _cloakedIds that belongs to the EXPERIMENTAL XInput Xbox cloak (distinct from the Sony
    // ids also living in _cloakedIds) — tracked separately so a kind-switch mid-session (a Sony pad
    // preempts the Xbox pad, leaving DetectedControllerKind != Xbox) can un-cloak exactly the orphaned
    // Xbox ids without touching a Sony pad that's legitimately cloaked at the same time; otherwise the
    // orphaned Xbox pad stays cloaked with nothing forwarding its input (dead second pad).
    private readonly HashSet<string> _xboxCloakedIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _cloakFailTraced;   // one-shot latch: trace the cloak-failure → passthrough drop once, not every capture pass
    private bool _userEmulation;  // XBox Mode explicitly on (Xbox-360 pad); otherwise native = DualShock 4 pad
    private bool _submitHooked;   // StateChanged → Submit wired exactly once (never unhooked; Submit no-ops when inactive)
    private bool _rumbleHooked;   // RumbleReceived → SetRumble wired exactly once (SetRumble no-ops off the XInput backend)
    private int  _emuStartRetries;             // transient ViGEm connect failures get a timed retry
    private DispatcherTimer? _emuRetryTimer;
    private DispatcherTimer? _emuWatchdog;     // slow always-on dead-pad recovery (see StartEmulatorWatchdog)
    private bool _emuWatchdogDown;             // an outage episode is open (start/heartbeat/end trace latch)
    private bool _emuWatchdogDriverMissing;    // standing down: retries exhausted AND the ViGEm bus is absent
    private long _emuWatchdogBeat;             // TickCount64 of the last mid-outage heartbeat line
    // The last capture pass left the virtual pad down as a DECISION (multi-pad guard, XInputCapture off,
    // BT pad with no cloakable devnode), not a failure — the watchdog must not treat it as an outage.
    // Failures (cloak contended, emulator start failed) keep this false so the 10s retry still heals them.
    private bool _padDownByDesign;

    /// <summary>ViGEmBus was absent at the last virtual-pad start. While it is up the capture pass skips the
    /// cloak entirely: cloaking with no bus to carry a stand-in only produces a cloak → failed start →
    /// un-cloak cycle per pass, and device-notification bursts turn that into driver churn. Cleared by the
    /// read-only probe at the top of the pass, so recovery needs nothing but the next pass.</summary>
    private bool _busMissing;

    // Steam-first startup leak (see SteamSentry.cs): detection + silent bounce + the persistent alert.
    private readonly SteamSentry _steamSentry = new();
    private Window? _sentryAlert;              // persistent card; null = not showing
    private DispatcherTimer? _sentryPoll;      // while the alert is up: notices an external Steam restart
    private bool _sentryLastIsolated;          // last capture-pass isolation result, for the poll's re-check
    private SteamSentry.State _lastSentryState;   // tray refresh on episode edges (Detected ⇄ Cleared)
    private long _sentryUpdateQuietUntilMs;       // post-update window: the card is deferred, cures aren't
    private DispatcherTimer? _sentryQuietTimer;   // one-shot re-evaluation at the window's end
    private const int SentryUpdateQuietMs = 90_000;
    // Debounces the virtual pad's existence against physical-controller presence (a permanent virtual pad
    // with nothing plugged in can confuse other apps enumerating pads). A disconnect does NOT tear the pad
    // down immediately — Bluetooth stalls fire a transient disconnect→reconnect, and mid-game the pad
    // vanishing + coming back makes some games re-detect devices, which is worse than a stale idle pad for
    // a few seconds. Instead a ~10s grace timer starts on disconnect; if the controller is STILL absent
    // when it fires, UpdateInputCapture re-runs and wantPad naturally drops (no physical pad ⇒ nothing to
    // gate the cloak on either, so this one flag covers both). A reconnect within the window cancels the
    // timer outright.
    private DispatcherTimer? _padGraceTimer;
    private const int PadDisconnectGraceSeconds = 10;
    // Debounced view of "a physical controller is present" that the pad gate reads — true immediately on
    // connect, but only flips false when the grace timer above actually expires (see _padGraceTimer).
    // Starts false: on a cold launch with nothing plugged in, no pad should come up until first connect.
    private bool _controllerPresentForPad;
    private readonly ControllerCaptureContinuity _captureContinuity = new();

    // ── Overlay ───────────────────────────────────────────────────────────────
    private OverlayWindow? _overlay;
    private bool           _overlayVisible;
    private WheelSlice[]?  _activeSlices;
    // UI-thread working copies survive controller recovery/disconnect; never treat them as saved.
    private readonly Dictionary<ActiveWheel, WheelSlice[]> _pendingWheelEdits = [];
    private ActiveWheel    _activeWheel;
    // Which stick aims the active wheel — always the hand opposite the Fn button that
    // opened it (Left Fn → right stick, Right Fn → left stick). For hotkey/tray opens
    // with no physical button, falls back to the wheel default (A → right, B → left).
    private bool           _aimWithRightStick = true;
    private int            _previewArmed = -1;   // armed index whose toggle-state preview is shown
    // Guarded-hold narration bookkeeping (stage 3 detection): whether the LAST armed slice was guarded,
    // and whether its dwell completed (ConfirmReady) before the armed index moved on.
    private bool           _prevArmedGuarded;
    private bool           _confirmReadySeen;

    private enum ActiveWheel { A, B }

    /// <summary>The controller kind currently driving the app — picks which trigger option set the
    /// Settings dropdown shows and which gestures the interpreter honors. Reflects the profile the reader
    /// detected for the connected pad (Edge / DualSense / DualShock 4), or the Edge default when none is
    /// connected yet. Re-read on connect so swapping controllers re-applies the right trigger modes.</summary>
    private ControllerKind DetectedControllerKind => _controller.Kind;

    // ── Enable / disable (both-Fn chord) ──────────────────────────────────────
    private bool _wheelsEnabled = true;
    // Anticheat passthru mode (SystemConfig.CaptureSafeMode): no virtual pad, no cloak — see UpdateInputCaptureCore.
    private bool _safeMode;
    // Auto passthru mode (Settings ▸ Passthru Mode ▸ Always Use Passthru Mode): engaged by the process watcher while a listed game runs.
    // Runtime-only — NEVER persisted, so automation can't clobber the user's manual CaptureSafeMode toggle.
    private bool _autoSafeMode;
    private string? _autoSafeModeApp;          // the exe name that engaged it (trace / tray text)
    private DispatcherTimer? _safeModeWatch;   // polls the process list; only runs while exceptions exist

    // Manual passthru-mode OFF is PENDING while anything game-like holds the foreground: a running game has
    // live handles to the un-cloaked physical pad and they cannot be revoked (HidHide filters opens only
    // — see docs/INPUT-CAPTURE.md), so presenting the virtual pad beside them would double every input.
    // No target process is latched, and NOTHING is cloaked during the hold — cloaking mid-hold cuts an
    // XInput game off at once, so cloak and virtual pad land together at completion. Runtime-only: a
    // Radiata restart mid-pending comes up in full capture. The wheel slice that arms this most directly
    // (the overlay is WS_EX_NOACTIVATE, so the game keeps the foreground) is un-offered — docs/ACTIONS.md ▸
    // hidden but supported. ⚠ Whether the tray and Settings paths can also arm it is UNMEASURED: reaching
    // the tray from a fullscreen game raises a shell surface, but a windowed game keeps the taskbar
    // clickable and menus restore foreground on dismiss. Don't assume either way.
    private bool _safeModeOffPending;
    private long _safeModeOffPendingDeadlineMs;
    private DispatcherTimer? _safeModeOffPendingWatch;   // 2s poll: completes when no game-like app is frontmost

    /// <summary>The EFFECTIVE capture profile: safe when the manual toggle OR the Exceptions watcher says
    /// so. This (not _safeMode) is what gates the virtual pad + cloak.</summary>
    private bool SafeModeActive => _safeMode || _autoSafeMode;
    private bool _fnLeftDown;
    private bool _fnRightDown;
    // When the last Fn invocation began — the both-Fn enable/disable chord is only honoured pressed
    // TOGETHER (within this grace of the invoke); a held Fn can't be elevated into it once the wheel
    // has been up longer.
    private long _fnInvokeAt;

    // ── Wheel trigger (configurable) ──────────────────────────────────────────
    private TriggerInterpreter? _triggerInterpreter;   // owns the non-Fn trigger modes
    private bool _fnOpensWheels = true;                // mode == "fn": Fn buttons open/close wheels
    private bool _holdFires     = true;                // Fn/chord release fires the armed slice (hold activation)
    private bool _toggleStyle;                         // the OPEN wheel is toggle-style (set per-invoke from the gesture): ✕/○ close, re-trigger dismisses, either stick aims
    private readonly ToastPresenter _toasts;

    public App()
    {
        _toasts = new ToastPresenter(() => _config.Current.System.SliceMaterial,
                                     () => _onboardingWindow is { IsLoaded: true });
    }
    // ── Test hotkeys ──────────────────────────────────────────────────────────
    private HwndSource? _hotkeySource;
    private const int HkVolumeUp  = 3; // registered only while overlay is open
    private const int HkVolumeDown = 4;
    private const int HkWheelBack  = 5; // Esc — dismiss the open wheel/editor (registered while a wheel is up;
                                        // the Game Grid's own Esc is HkBack). Same key can't be registered twice,
                                        // so this no-ops over a browser-assign wheel where HkBack already owns Esc.
    // F1 / F2 / F3 are intentionally NOT registered as global hotkeys — they're left completely free for
    // games and apps; do not reintroduce. Test-open a wheel and enable/disable the wheels from the
    // tray menu instead; the both-Fn / both-trigger controller chord still toggles the wheels.
    // Game-browser keyboard fallbacks (registered only while the browser is open)
    private const int HkNavUp = 6, HkNavDown = 7, HkNavLeft = 8, HkNavRight = 9, HkLaunch = 10, HkBack = 11;
    private const int HkFilterPrev = 12, HkFilterNext = 13;   // browser launcher-filter cycle (L1/R1; PageUp/Down fallback)
    // ids 14/15 are unused — the D-Pad drives
    // BeginMixGesture/StepMixBalance directly (see _dpadMixDir).

    // ── Volume (D-pad / arrow keys while overlay open) ────────────────────────
    private DispatcherTimer? _volumeHideTimer;

    // ── Game browser ──────────────────────────────────────────────────────────
    private bool _browserOpen;

    // Centre of the last wheel opened, in primary-screen DIPs — where the arcade blooms from (the wheel is
    // gone by the time a fired slice opens it). Seeded to screen centre so a first-ever open can't land at
    // (0,0) if anything ever fires an arcade slice without a wheel (a hotkey, a future web surface).
    private double _lastWheelCx = double.NaN, _lastWheelCy = double.NaN;
    /// <summary>Which wheel opened last (0 = A on the left quarter, 2 = B on the right), the position the arcade
    /// launcher always takes and a game takes until the player has chosen one of the three.</summary>
    private int _lastWheelSide = 1;
    /// <summary>The round game window's position — 0 over the left wheel, 1 the screen centre, 2 over the right
    /// wheel — one setting for every game (ArcadeStore), and where the launcher sits this session.</summary>
    private int _arcadePos = 1, _arcadeLauncherPos = 1;
    private const int ArcadeSlideMs = 260;

    // ── Startup ───────────────────────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Command-line modes (App.Cli.cs) are decided before the single-instance claim.
        if (RunCommandLine(e.Args)) return;

        // Single instance: if one is already running, ask it to show Settings and exit instead
        // of launching a duplicate tray app.
        _launchPackagePaths = PackageInstallFlow.PathsFromArgs(e.Args);
        _instanceMutex = new System.Threading.Mutex(initiallyOwned: true, InstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // A launch carrying package paths hands them to the primary instead of showing Settings.
            // An autostart route that lost the race to the other route is not "the user launched it
            // again": exit without asking the primary to show anything.
            if (_launchPackagePaths.Count > 0 && PackageInstallFlow.HandOff(_launchPackagePaths))
                Trace.WriteLine($"[App] handed {_launchPackagePaths.Count} package path(s) to the running instance");
            else if (_autostartRoute is null)
                try
                {
                    if (System.Threading.EventWaitHandle.TryOpenExisting(ShowEventName, out var ev))
                        using (ev) ev.Set();
                }
                catch { /* best effort — still exit */ }
            _instanceMutex.Dispose();
            _instanceMutex = null;
            _earlyExit = true;   // never initialized capture — TearDown must not touch it
            Shutdown();
            return;
        }
        // Primary instance: a later launch signals us. Reopen the RIGHT surface: the setup wizard while
        // first-run setup is unfinished, Settings otherwise. Tray left-click deliberately stays Settings —
        // this redirect is only for "I launched the app again".
        _showRequest = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, ShowEventName);
        _showWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(
            _showRequest, (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            {
                Trace.WriteLine($"[App] show-request from a second launch → {(NeedsOnboarding ? "onboarding (setup unfinished)" : "settings")}");
                if (NeedsOnboarding) RunOnboarding(); else OpenSettings();
            })),
            null, -1, executeOnlyOnce: false);
        _quitRequest = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, QuitEventName);
        _quitWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(
            _quitRequest, (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_shuttingDown) return;
                Trace.WriteLine("[App] quit-request from the installer → ExitApp");
                ExitApp();
            })),
            null, -1, executeOnlyOnce: false);
        // A later launch handed package paths (a drop onto the exe) → install them here.
        if (PackageInstallFlow.Offered)
        {
            PackageInstallFlow.ClearRequests();
            _installRequest = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset,
                                                                   PackageInstallFlow.InstallEventName);
            _installWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(
                _installRequest, (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                    PackageInstallFlow.TakeRequests(_settingsWindow is { IsVisible: true } w ? w : null))),
                null, -1, executeOnlyOnce: false);
        }

        // Crash safety: HidHide's cloak persists in the driver across process death, so a crash or
        // Environment.Exit (which skip TearDown) must still un-cloak the pad. A hard kill / power loss
        // skips even these — the persisted cloak-state file (recovered in InitInputCaptureDeferred) is the
        // backstop for that. We don't mark exceptions handled, so the process still crashes/reports.
        // Un-cloak FIRST (the controller-safety half), then record the crash for the opt-in reporter.
        // CrashReporter.TryWritePending is one-shot and swallows its own failures, so it can neither run
        // twice when both hooks fire for one crash nor mask the original exception. ProcessExit is NOT a
        // crash signal — no report there.
        DispatcherUnhandledException               += (_, e) => { CrashSafeUncloak(); CrashReporter.TryWritePending(e.Exception); };          // UI-thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { CrashSafeUncloak(); CrashReporter.TryWritePending(e.ExceptionObject); };    // e.g. the HID reader thread
        AppDomain.CurrentDomain.ProcessExit        += (_, _) => CrashSafeUncloak();   // Environment.Exit / normal end
        // A faulted fire-and-forget task (`_ = SomethingAsync()`) is swallowed silently by the runtime; without
        // this the crash reporter never hears about that whole class. Observed, so it can't escalate; not a
        // cloak signal — the process stays up and the pad stays owned.
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Trace.WriteLine($"[App] unobserved task exception: {e.Exception}");
            CrashReporter.TryWritePending(e.Exception);
        };

        AppPaths.MigrateLegacyFolder();   // %APPDATA%\Capstan (or ControllerWheel) → Radiata (one-time, before config load)
        SetupFileTrace();                 // after migration (needs the final app-data dir), primary instance only

        // Ride-along watchdog: spawned once at startup (not lazily on first cloak) so it also covers a
        // kill inside Hide()'s persist-intent→driver-write window — the first cloak is minutes of code away
        // (InitInputCaptureDeferred's settle timer), so "after SetupFileTrace" costs no coverage and the
        // spawn trace actually lands in the log. Harmless when nothing is cloaked (its wake path no-ops on
        // an empty cloaked-ids.txt). Clean exit never kills it — TearDown deletes the record first, so the
        // woken watchdog exits silently on its own guards.
        SpawnWatchdog();
        // Drop-in packages: folders + scan run BEFORE the ConfigLoader is constructed, because
        // Sanitize normalizes unknown material tokens to Pearl on load — a consented custom
        // material must already be registered or the user's saved pick silently degrades. Only
        // packages with a recorded ACCEPT load here; new ones prompt after startup settles.
        PackageStore.EnsureFolders();
        PackageInstallFlow.LoadStartupScan(PackageStore.Scan());
        _config   = new ConfigLoader(System.Threading.SynchronizationContext.Current!, SecretField.ProtectBackup, SecretField.ReadBackup);
        // First thing after config and before ANY thread: UI culture is inherited at thread creation, so the
        // HID reader, the announcer timer and pool work all read the language from here on.
#if DEBUG
        // Dev builds: `--lang <code>` overrides the saved language for this run — with qps / qps-rtl that is how
        // layout is checked before a translation exists. Not compiled into Release.
        Loc.Init(Array.IndexOf(e.Args, "--lang") is int langIdx and >= 0 && langIdx + 1 < e.Args.Length
                 ? e.Args[langIdx + 1] : _config.Current.System.Language);
#else
        Loc.Init(_config.Current.System.Language);
#endif
        // The taxonomy display names are authored defaults too: an in-wheel Add copies one into the new slice.
        Loc.RegisterDefaultLabels(WheelEditorControl.Categories.SelectMany(c => c.Entries.SelectMany(e => new[] { e.Display }.Concat(e.Options.Select(o => o.Display)))));
        ActionTint.SetOverrides(_config.Current.ActionColors);
        ActionIcons.SetOverrides(_config.Current.ActionIcons);
        MigrateSgdbKeyToDpapi();          // one-time: legacy plaintext SteamGridDbKey → DPAPI blob
        GameArt.SteamGridDbKey = SecretField.ToPlaintext(_config.Current.System.SteamGridDbKey);
        GameArt.SteamGridDbStyle = _config.Current.System.SgdbStyle;
        GameArt.PreferPlaynite = _config.Current.System.PreferPlayniteCovers;
        Sfx.Enabled = _config.Current.System.SoundEffects;
        Sfx.Theme   = Sfx.ResolveSet(_config.Current.System.SoundTheme, _config.Current.System.SliceMaterial);
        _safeMode   = _config.Current.System.CaptureSafeMode;
        ConfigureSafeModeWatcher();   // start the Exceptions watcher if any auto-passthru-mode apps are configured
        _executor = new ActionExecutor(_platform, OpenSettings, OpenGameBrowser,
                                       ToggleEmulation, () => _userEmulation,
                                       ToggleDualShock, () => !_userEmulation,
                                       toggleWheels: HandleChord,
                                       obsSend: a => _obs.Send(a),
                                       // Arcade.Enabled false → the callback is never wired, so the executor's
                                       // "arcade" case traces and no-ops for any slice that survives from a
                                       // build where it was on (its type is never rewritten). See
                                       // Core/Arcade/Arcade.cs.
                                       openArcade: Arcade.Enabled ? OpenArcade : null,
                                       // Toggle Passthru Mode — wired only when the isolation drivers are
                                       // present, the SAME gate that decides whether Settings shows the
                                       // Passthru Mode tab and whether the slice editors offer this action.
                                       // Without them there is no cloak or virtual pad to stand down.
                                       toggleSafeMode: SafeModeAvailable
                                           ? () => { ToggleSafeMode(); return _safeMode; }
                                           : null,
                                       safeModeActive: SafeModeAvailable ? () => _safeMode : null);
        ConfigureObs(_config.Current.System);
        _mixBalance = _config.Current.System.MixBalance;
        RefreshMixPair();
        _config.Reloaded += OnConfigReloaded;
        ApplyGlyphSet();   // seed the button-glyph set from the saved override (before any pad connects)
        _config.StartWatching();

        if (_autostartRoute is not null) Trace.WriteLine($"[App] autostart launch via the {_autostartRoute}");
        StartupManager.MigrateLegacyName();   // Capstan (or ControllerWheel) → Radiata Run-key entry (one-time)
        if (StartupManager.EnsureCurrentFormat())
            Trace.WriteLine("[App] Run-key entry rewritten to the current format (--autostart)");
        InstallInfo.TraceDetermination();     // installed-vs-portable copy, decided once per process
        // Installed copies adopt a Run entry left by a portable copy: an entry pointing at a different
        // Radiata.exe is redirected to this one (present = enabled, so an absent entry stays absent).
        // The old portable folder itself is never touched.
        if (InstallInfo.IsInstalledCopy && StartupManager.RedirectToCurrentExe() is string oldRunTarget)
            Trace.WriteLine($"[Install] Run-key entry migrated to the installed copy (was: {oldRunTarget})");
        BuildTrayIcon();
        BuildOverlay();
        if (_overlay is not null)
        {
            _overlay.EditMaxSlices = MaxSlices;   // edit legend drops "add" at the cap
            _overlay.SetSliceThickness(_config.Current.System.SliceThickness);
            _overlay.SetShowSliceLabels(_config.Current.System.ShowSliceLabels);
            _overlay.SetSliceMaterial(_config.Current.System.SliceMaterial);
            _overlay.SetAlwaysShowHub(_config.Current.System.AlwaysShowHub);
        }
        // Reduce Motion is a product-wide policy (MotionPolicy): the saved setting OR the Windows
        // "show animations" preference, either reduces. Seed both inputs, then push the EFFECTIVE value.
        MotionPolicy.UserSetting          = _config.Current.System.ReduceMotion;
        MotionPolicy.SystemPrefersReduced = NativeMethods.SystemAnimationsDisabled();
        ApplyMotionPolicy();
        SeedNarrationFromNarrator();
        ApplyNarration(_config.Current.System);
        // The Game Grid speaks through the same coordinator (static hook — the grid is a single instance
        // and this keeps OverlayWindow out of the plumbing). Announcer.Enabled gates everything.
        GameBrowserControl.AnnounceHook = (text, kind) => _announcer.Announce(text, kind);
        // Settings' Narrator tip speaks straight through the sink — see SpeakSettingsTip.
        SystemEditorControl.SpeakHook = SpeakSettingsTip;
        // Refusals with no visual surface (see ActionExecutor.Announce) — Result-kind: they're outcomes,
        // and they must not be swallowed by the selection debounce.
        _executor.Announce = text => _announcer.Announce(text, AnnouncementKind.Result);
        SetMaterialFlags(_config.Current.System.SliceMaterial);
        // Re-fit the overlay to the screen when the resolution / monitor layout changes, so the
        // wheel canvas never stays sized to stale dimensions (which clips the wheel).
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;   // re-theme the tray flower
        StartController();
        _controller.ApplyOffsets(HidOffsets.Load());
        InitInputCaptureDeferred();   // bring the ViGEm/HidHide drivers up a beat later (see method note)
        StartEmulatorWatchdog();      // recover a dead/failed-revive virtual pad within seconds
        WindowsPlatformActions.WarmGameDetection();   // so a Passthru Mode OFF never runs the first library scan on the UI thread
        CreateHotkeySink();
        ApplyTrayMode();   // tray icon vs. normal-app (taskbar) presence
        InitUpdateAndCrashReporting();   // update-check schedule + the pending-crash consent offer
        // "Restart Steam" ticked on the update prompt outlives that process as a marker file — honour it
        // once this session's isolation is up (the capture init below is still pending at this point).
        bool steamRestartQueued = SteamRestartFlag.Consume();   // destructive read — observe exactly once
        if (steamRestartQueued) ArmUserSteamRestart();
        // Post-update launch: the silent update closes the tray app and relaunches it AFTER Steam, which
        // manufactures the exact pre-cloak-handle state the sentry warns about — a genuine condition, but
        // not the user's doing and (with the opt-in taken) already being cured. Quiet the sentry CARD for
        // a bounded window; it re-evaluates when the window ends, so an uncured leak still surfaces, just
        // not mid-swap. Signals, any of: the --updated flag the
        // installer's relaunch entry passes, the consumed Restart-Steam marker, or a config last saved by
        // a different build (covers updates shipped before --updated existed).
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--updated") >= 0
            || steamRestartQueued
            || ConfigLoader.VersionChangedAtLoad)
        {
            _sentryUpdateQuietUntilMs = Environment.TickCount64 + SentryUpdateQuietMs;
            Trace.WriteLine("[Sentry] post-update launch — sentry card quiet for "
                            + $"{SentryUpdateQuietMs / 1000}s (tray text and silent cures unaffected)");
        }
        // A deliberate drivers opt-out stands only while drivers are actually absent: once both are
        // detected installed, clear it so a driver that later vanishes warns again.
        if (_config.Current.System.DriversDeclined
            && _emulator.ProbeDriverInstalled() && DriverStatus.HidHideInstalled())
            WriteSystem(_config.Current.System with { DriversDeclined = false });
        // New (or content-changed) packages get the stern consent gate once the dispatcher is idle —
        // never during the startup sequence, and never for packages that already have a verdict. Package
        // paths this launch was handed (a drop onto the exe) install right after.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            PackageInstallFlow.PromptForNewPackages();
            PackageInstallFlow.Install(_launchPackagePaths, owner: null);
        }));
        // Version + origin: every ViGEmBus fork registers the same service name, and a fork's bus devnode
        // can hang off a vendor ACPI node instead of ROOT\SYSTEM (HP OMEN Gaming Hub) — the first thing to
        // read when a virtual pad is ever judged physical (docs/INPUT-CAPTURE.md ▸ Xbox / XInput pads).
        Trace.WriteLine($"[Drivers] ViGEmBus: {(_emulator.ProbeDriverInstalled()
                            ? $"installed {DriverStatus.ViGEmBusVersion() ?? "(version unreadable)"} ({DriverStatus.ViGEmBusOrigin()})"
                            : "MISSING")}  " +
                        $"HidHide: {DriverStatus.HidHideVersion() ?? "MISSING"}  " +
                        $"HidGuardian(legacy): {(DriverStatus.HidGuardianInstalled() ? "PRESENT — conflicts!" : "absent")}  " +
                        $"OobeVersion={_config.Current.System.OobeVersion}");
        // A foreign bus gets ONE notice per install (keyed on the origin string, so a different fork or a
        // re-enumerated bus notices again), and only once onboarding is done — the wizard's own drivers row
        // carries the same message while it runs. Nothing is changed on the PC; the user decides.
        if (_config.Current.System.OobeVersion >= OobeCurrentVersion
            && DriverStatus.ForeignViGEmBusName() is { } foreignBusName)
        {
            string originKey = DriverStatus.ViGEmBusOrigin();
            if (_config.Current.System.ForeignViGEmBusNoticed != originKey)
            {
                WriteSystem(_config.Current.System with { ForeignViGEmBusNoticed = originKey });
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
                {
                    Trace.WriteLine($"[Drivers] foreign ViGEmBus notice shown ({foreignBusName})");
                    ShowCornerToast(Loc.T(UiText.Toasts.ForeignBusTitle), Loc.F(UiText.Toasts.ForeignBusBody, foreignBusName),
                                    NoticeTier.Info, holdMs: 20_000);
                });
            }
        }
        // Confirms the bundled installers resolve beside the exe (esp. under single-file publish, where the
        // app dir is AppContext.BaseDirectory) — a "MISSING" here means the OOBE/repair install can't run.
        Trace.WriteLine($"[Drivers] bundled installers: " +
                        $"{(DriverSetup.Bundled(DriverSetup.ViGEmBusExe) && DriverSetup.Bundled(DriverSetup.HidHideExe) ? "found" : "MISSING")}" +
                        $" @ {DriverSetup.DriversDir}");

        // Warm the Playnite cache now (login time — Playnite usually isn't running yet, so its
        // DB is readable). Later browser opens use the cache when Playnite holds the DB locked.
        // Skipped entirely when Playnite isn't installed — then the browser uses GameLibrary.Scan().
        if (PlayniteLibrary.IsAvailable)
            _ = System.Threading.Tasks.Task.Run(() => PlayniteLibrary.GetInstalledGames());

        // Re-resolve any installed-game logos stripped by an older Settings save (one-shot, cache-first).
        _ = HealMissingGameLogosAsync();

        // First run (or a bumped OobeVersion): show the setup wizard once the tray/overlay are settled.
        // Deferred to idle so startup work (incl. the delayed driver init) isn't raced by wizard probes.
        // --oobe = developer switch: open the wizard regardless (UI iteration without re-arming config).
        if (NeedsOnboarding || Array.IndexOf(Environment.GetCommandLineArgs(), "--oobe") >= 0)
            // RunOnboarding (not OpenOnboarding) so the auto-open logs "[OOBE] wizard opened" like the
            // tray/Settings path — an unlogged wizard hides its summon suppression from the trace.
            Dispatcher.BeginInvoke(new Action(RunOnboarding), DispatcherPriority.ApplicationIdle);
        // --settings: come back with Settings open after a self-restart (Restore). Without it the app
        // reappears as a bare tray icon and the user has to go find it again, which reads like the restore
        // half-failed. Chained to the onboarding gate deliberately: if setup is unfinished the wizard wins,
        // so a Wipe-and-Reset restart can never bury the wizard under a Settings window.
        else if (Array.IndexOf(Environment.GetCommandLineArgs(), "--settings") >= 0)
            Dispatcher.BeginInvoke(new Action(OpenSettings), DispatcherPriority.ApplicationIdle);
    }

    // ── Narration (accessibility A1) ──────────────────────────────────────────
    // Radiata's own voice: the Announcer coordinates (Core, unit-testable), SpeechSink speaks (SAPI).
    // The sink is built lazily on first enable — SAPI init loads a voice, which narration-off users
    // shouldn't pay for. Narrator can't service the never-focused overlay window, so speech is Radiata's job.
    private readonly Announcer _announcer = new();
    private SpeechSink? _speech;

    /// <summary>First run only: a machine already running Windows Narrator gets Narration ON by default.
    /// Narrator up at install time is the strongest signal available that someone here needs speech —
    /// including the common case of a sighted person setting the PC up FOR a vision-impaired user, where
    /// defaulting to silence hides the feature from the person it was built for.
    /// <para>Gated on <c>OobeVersion == 0</c>, never merely on <see cref="NeedsOnboarding"/>: the latter is
    /// also true when a bumped OOBE version re-runs the wizard for an existing user, and a re-run must not
    /// reach in and flip a setting they already chose. This is a DEFAULT, not a lock — the wizard's
    /// accessibility row and Settings both override it.</para></summary>
    private void SeedNarrationFromNarrator()
    {
        var sys = _config.Current.System;
        if (sys.OobeVersion != 0 || sys.Narration) return;
        // Trace the NEGATIVE too: a seed that silently never fires looks exactly like the plain default,
        // so a broken probe (Narrator renamed on some future Windows) would pass for normal behaviour.
        if (!NarratorLauncher.IsRunning)
        {
            Trace.WriteLine("[Narration] first run, Windows Narrator not running — Narration stays off");
            return;
        }
        Trace.WriteLine("[Narration] first run with Windows Narrator running — defaulting Narration on");
        WriteSystem(sys with { Narration = true });
    }

    private void ApplyNarration(SystemConfig sys)
    {
        try
        {
            if (sys.Narration)
            {
                _speech ??= new SpeechSink(Loc.Lang);
                _speech.Volume = sys.NarrationVolume;
                _announcer.Sink = _speech;
                _announcer.Enabled = true;
            }
            else
            {
                _announcer.Enabled = false;
                _speech?.Stop();
            }
            // The Add picker's centre latch follows narration; refresh it here too, or a narration change through
            // a config reload is not seen by an edit session already open (BeginEdit reads it only once).
            if (_overlay is not null) _overlay.EditPickerLatchesAtCentre = _announcer.Enabled;
        }
        catch (Exception ex) { Trace.WriteLine($"[Narration] init failed: {ex.Message}"); }
    }

    /// <summary>Voice for the Settings ▸ Accessibility Narrator tip. Deliberately NOT routed through the
    /// Announcer: the tip fires the moment the checkbox is ticked — before the save that would flip
    /// <c>Announcer.Enabled</c> — and it is Settings speech, so the wheel's selection coalescing has no
    /// business dropping or interrupting it. Queues (never interrupts) behind anything the overlay is
    /// already saying. Building the sink here is what makes the tip audible on the very first tick.</summary>
    private void SpeakSettingsTip(string text)
    {
        try
        {
            _speech ??= new SpeechSink(Loc.Lang);
            _speech.Volume = _config.Current.System.NarrationVolume;
            _speech.Speak(text, interrupt: false);
        }
        catch (Exception ex) { Trace.WriteLine($"[Narration] tip speak failed: {ex.Message}"); }
    }

    // ── Config hot-reload ─────────────────────────────────────────────────────

    // obs-websocket client for the "obs" action type — configured from Settings ▸ Advanced ▸ Integrations
    // (the stored password is DPAPI-protected; decrypt only in memory, right before handing it over).
    private readonly ObsClient _obs = new();
    private void ConfigureObs(SystemConfig sys)
    {
        string? pw = null;
        try { pw = string.IsNullOrEmpty(sys.ObsPassword) ? null : LocalSecret.Unprotect(sys.ObsPassword); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Obs] password unprotect failed: {ex.Message}"); }
        _obs.Configure(sys.ObsPort, pw);
    }

    /// <summary>One-time startup migration: a legacy PLAINTEXT SteamGridDbKey in config.json is rewritten
    /// as a DPAPI blob. Detection: a real DPAPI blob round-trips through Unprotect; plaintext doesn't.
    /// No-op once migrated. Best-effort — a failed write just retries next launch.</summary>
    private void MigrateSgdbKeyToDpapi()
    {
        try
        {
            var cur = _config.Current;
            var stored = cur.System.SteamGridDbKey;
            if (string.IsNullOrEmpty(stored) || LocalSecret.IsProtected(stored)) return;
            if (!_config.WriteConfig(new AppConfig
            {
                WheelA       = cur.WheelA,
                WheelB       = cur.WheelB,
                ActionColors = cur.ActionColors,
                ActionIcons  = cur.ActionIcons,
                CustomColors = cur.CustomColors,
                System       = cur.System with { SteamGridDbKey = LocalSecret.Protect(stored) },
            })) return;
            Trace.WriteLine("[App] SteamGridDB key migrated to DPAPI at-rest protection");
        }
        catch (Exception ex) { Trace.WriteLine($"[App] SGDB key DPAPI migration failed: {ex.Message}"); }
    }

    private HashSet<string>? _lastLaunchPaths;

    private void OnConfigReloaded(AppConfig config)
    {
        // Ignore our own live-save writes while editing — the overlay owns the working copy and would
        // be clobbered by a SetSlices here.
        if (Editing)
        {
            // ConfigLoader has already logged "Reloaded from disk" by now — without this line the log
            // claims a reload applied when it was dropped on the floor.
            Trace.WriteLine("[Config] reload DISCARDED — in-wheel edit mode owns the working copy " +
                            "(external edits made now won't appear until the next reload)");
            return;
        }
        // The exe-icon cache is cleared only when a launch path actually changed. Every save raises this —
        // a material, Reduce Motion, an in-wheel live save — and each Clear made the next wheel open
        // re-extract every launch slice's icon through the shell, synchronously, on the way to its first frame.
        var launchPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in config.WheelA.Concat(config.WheelB))
            if (s?.Action is { Type: "launch", Path: { } lp }) launchPaths.Add(lp);
        if (_lastLaunchPaths is null || !_lastLaunchPaths.SetEquals(launchPaths)) IconCache.Clear();
        _lastLaunchPaths = launchPaths;
        ActionTint.SetOverrides(config.ActionColors);
        ActionIcons.SetOverrides(config.ActionIcons);
        GameArt.SteamGridDbKey = SecretField.ToPlaintext(config.System.SteamGridDbKey);
        GameArt.SteamGridDbStyle = config.System.SgdbStyle;
        GameArt.PreferPlaynite = config.System.PreferPlayniteCovers;
        Sfx.Enabled = config.System.SoundEffects;
        Sfx.Theme   = Sfx.ResolveSet(config.System.SoundTheme, config.System.SliceMaterial);
        ConfigureObs(config.System);
        // Capture profile may have changed externally (the Settings checkbox writes config, it doesn't
        // call ToggleSafeMode) — an ON→OFF flip needs the same pending hold as the direct toggle.
        bool wasSafeMode = _safeMode;
        _safeMode   = config.System.CaptureSafeMode;
        if (_safeMode) CancelPendingSafeModeOff();
        else if (wasSafeMode) BeginPendingSafeModeOff();
        ConfigureSafeModeWatcher();                    // the Exceptions list may have changed too
        UpdateInputCapture();   // the "capture controller" setting may have changed
        ApplyTrayMode();
        ApplyTriggerConfig();   // wheel-trigger mode / activation / swap may have changed
        ApplyGlyphSet();        // the "Button icons" override may have changed
        _overlay?.SetSliceThickness(config.System.SliceThickness);   // live slice-thickness change
        _overlay?.SetShowSliceLabels(config.System.ShowSliceLabels); // live "Show labels on" change
        _overlay?.SetSliceMaterial(config.System.SliceMaterial);     // live slice-material change
        _overlay?.SetAlwaysShowHub(config.System.AlwaysShowHub); // live "Always show hub" change
        MotionPolicy.UserSetting = config.System.ReduceMotion;   // live "Reduce Motion" change…
        ApplyMotionPolicy();                                     // …pushed to the wheel AND an open grid
        ApplyNarration(config.System);                           // live "Narrate wheel and Game Grid" change
        SetMaterialFlags(config.System.SliceMaterial);   // dark/Mesa → lighten built-in glyph/logo tints
        _mixBalance = config.System.MixBalance;   // re-seed (an external/Settings edit may have changed it)
        RefreshMixPair();                         // the configured mixer slice's devices may have changed
        if (_overlayVisible && _overlay is not null)
        {
            var slices = _activeWheel == ActiveWheel.A ? config.WheelA : config.WheelB;
            PopulateIcons(slices);
            _activeSlices = slices;
            _overlay.SetSlices(slices);
        }
    }

    // ── Overlay lifecycle ─────────────────────────────────────────────────────

    private void BuildOverlay()
    {
        _overlay = new OverlayWindow();
        _overlay.Parked += () => _passthruNoticeShown = false;
        _overlay.SliceClicked += idx =>
        {
            // In edit mode (and its Add picker) the wheel renders a working list / picker menu while
            // _activeSlices still holds the pre-edit snapshot — a mouse click would fire an unrelated
            // real slice (bypassing RequireConfirm) and destroy the edit session. Same for the browser.
            if (Editing || _browserOpen || _arcadeOpen) return;
            if (!_overlayVisible || _activeSlices is null || (uint)idx >= (uint)_activeSlices.Length) return;
            var slice = _activeSlices[idx];
            // A mouse click has no hold dwell, so a hold-to-confirm slice never fires from one — the
            // controller hold is its only route (same predicate as the release path and the dwell).
            if (slice.Action?.RequireConfirm == true)
            {
                Trace.WriteLine($"[Wheel] mouse click ignored on \"{slice.Label}\" — it's a hold-to-confirm slice");
                return;
            }
            // Fire AFTER the mouse-click event finishes; FireAction handles dismissal (and the
            // lingering readout for toggle actions).
            Dispatcher.BeginInvoke(new Action(() => FireAction(slice)));
        };

        // Game chosen (✕ or mouse double-click). In edit-mode game-pick: hand it back to the editor;
        // otherwise launch it.
        _overlay.GameChosen += game =>
        {
            if (!_browserOpen) return;
            if (_editPickGame) { PickGameForEdit(game); return; }
            GameMetadata.MarkLaunched(game);   // I2: recency stamp drives the grid's most-recent-first sort
            CloseGameBrowser($"Launching {game.Name}");
            // Prefer the direct storefront URL (Steam/GOG/Battle.net) over the playnite:// one, so
            // launching doesn't pop Playnite open as a middleman. Stores with no derivable direct URL
            // (Epic/Xbox/itch from Playnite) still go via playnite://.
            FireAction(new WheelSlice { Label = game.Name,
                Action = new ActionConfig { Type = "url", Url = game.DirectLaunchUrl ?? game.LaunchUrl } });
        };

        // Mouse double-click on a launcher chip opens that storefront's big-picture mode (not while picking
        // a game for the editor — there you can only choose a game).
        _overlay.LauncherChosen += key => { if (_browserOpen && !_editPickGame) FireLauncher(key); };

        // The bleed-through guard's △-hold override completed. The control has already dropped the card and
        // resumed; this is here so the decision is visible in the trace log users send with bug reports.
        _overlay.ArcadeOverrideGranted += () =>
            Trace.WriteLine($"[Arcade] play-anyway override accepted for this session (isolation: {_isolationNote})");

        // The control closed itself on a render-path throw. Without this the host keeps _arcadeOpen, the
        // mouse watch, the Esc hotkey and the neutral virtual pad — every button swallowed into a disc that
        // is no longer there, until the player happens to press ○.
        _overlay.ArcadeFailed += () => { if (_arcadeOpen) CloseArcade(); };
        // Cabinets → game: the window travels from the wheel's spot to the game's on the launch clock; game →
        // cabinets: back to the wheel on the shrink clock. The disc grows/shrinks on those same clocks.
        _overlay.ArcadeSurfaceChanging += toGame =>
        {
            if (!_arcadeOpen || _overlay is null) return;
            int target = toGame ? _arcadePos : _arcadeLauncherPos;
            int ms = (int)((toGame ? ArcadePickerTuning.LaunchSeconds : ArcadeTuning.DiscShrinkSeconds) * 1000);
            _overlay.SlideArcade(ArcadePosX(target), ArcadeCy(), ms);
        };

        // Hold □ on a storefront card hides that whole storefront. Config is App's to write: the grid
        // can't write it itself without racing the hot-reload watcher, so it raises this and we persist.
        // The grid has already dropped the store from its own view, so no reload/refresh is needed here.
        _overlay.StorefrontHideRequested += store =>
        {
            if (!_browserOpen || string.IsNullOrWhiteSpace(store)) return;
            var sys = _config.Current.System;
            if (sys.DisabledStorefronts.Contains(store, StringComparer.OrdinalIgnoreCase)) return;
            WriteSystem(sys with { DisabledStorefronts = [.. sys.DisabledStorefronts, store] });
            Trace.WriteLine($"[Grid] storefront hidden: {store}");
        };

        // A guarded slice ticks ONCE, when its hold dwell COMPLETES — that is the moment it's actually
        // armed. The arm cue on the input path is suppressed for these (see ArmedRequiresConfirm), so the
        // tick means the same thing on every slice: "this is armed, release to fire".
        _overlay.ConfirmReady += () =>
        {
            Sfx.SliceArmed();
            // Guarded stage 2 of 3: the hold completed — release now fires. Result-kind so the ready cue
            // can't be swallowed by selection debouncing.
            _confirmReadySeen = true;
            _announcer.Announce(Loc.T(UiText.Narration.ReadyReleaseToFire), AnnouncementKind.Result);
        };

        // Show the window once and park it idle (transparent + click-through). It's never
        // hidden again, so switching wheels never flashes the previous wheel's stale frame.
        _overlay.Show();
        _overlay.SetClickable(false);
        _overlay.HideWheel();
    }

    /// <summary>Map an invocation (left-handed vs right-handed gesture) to a wheel + aim stick. Two families:
    /// <b>Direct-side chords</b> (L3/R3, D-Pad — <see cref="IsDirectSideChord"/>) name the wheel
    /// outright (clicked stick / d-pad direction), the SAME in Hold and Toggle. Everything else
    /// (Fn, Bumper+Trigger, Bumper/Trigger+Home, Bumper/Trigger+Back/Start, touchpad) <b>crosses
    /// unconditionally</b> (left-hand → Right wheel — the free hand aims), in Hold AND Toggle alike;
    /// Toggle's uncrossed feel comes from the auto-ticked "Swap wheel sides", which reverses BOTH
    /// families. Aim follows the opening wheel's side (in Toggle either stick aims anyway).</summary>
    private (ActiveWheel wheel, bool aimRight) ResolveTrigger(bool leftHanded)
    {
        // Direct-side chords open the NAMED wheel — no Hold/Toggle crossing. leftHanded here already means
        // "the left group fired" (SidesFor puts the clicked stick / Back / d-pad-Left in the left group).
        // "Swap left/right" still reverses it in this case too; aim follows the opening side.
        if (IsDirectSideChord(_lastTrigger))
        {
            bool left = leftHanded;
            if (_config.Current.System.SwapFnButtons) left = !left;
            return (left ? ActiveWheel.A : ActiveWheel.B, aimRight: !left);
        }

        // The squeezing hand opens the OPPOSITE wheel (free hand aims) — for Hold AND Toggle alike.
        // Activation (Hold vs Toggle) never changes the side on its own; ONLY "Swap left/right"
        // reverses it. (Turning on Toggle ticks Swap for you, so switching activation still visibly flips
        // the sides — but via that box, not a hidden runtime rule.) The touchpad swipe is the exception:
        // its EDGE names the side directly (left edge → Left wheel), so it stays uncrossed regardless.
        bool touchpad = _lastTrigger == TriggerModes.TouchpadSwipe;
        bool toLeftWheel = touchpad ? leftHanded : !leftHanded;
        if (_config.Current.System.SwapFnButtons) toLeftWheel = !toLeftWheel;
        return (toLeftWheel ? ActiveWheel.A : ActiveWheel.B, leftHanded);
    }

    /// <summary>A chord whose gesture NAMES the wheel side directly (no Hold/Toggle crossing): the L3/R3
    /// stick-click or the D-Pad direction (see <see cref="ResolveTrigger"/>). Bumper/Trigger+Select/Start is
    /// NOT in this family: the bumper/trigger hand picks the side like Fn — Select/Start is just a shared
    /// modifier. (Legacy ViewMenuStick stays: its side is the clicked stick.)</summary>
    private static bool IsDirectSideChord(string? token) =>
        token is TriggerModes.ViewMenuStick or TriggerModes.BumpersStick or TriggerModes.TriggersStick
              or TriggerModes.BumpersDpad or TriggerModes.TriggersDpad
              or TriggerModes.ExtraStick or TriggerModes.ExtraDpad;

    /// <summary>Whether a wheel opened by <paramref name="token"/> behaves toggle-style (✕/○ close,
    /// either stick aims, re-trigger dismisses): the touchpad swipe always does (nothing to hold); every
    /// other gesture follows the Activation setting. Computed per-invoke so mixed hold/toggle gestures work.</summary>
    private bool ToggleStyleFor(string? token) =>
        token == TriggerModes.TouchpadSwipe || _config.Current.System.TriggerActivation == "toggle";

    // ── Throttled diagnostics ────────────────────────────────────────────────
    // Most "nothing happened" traces sit in per-press input handlers, so an unthrottled WriteLine would
    // bury the log under a held button. One line per distinct reason, re-logged only after a quiet
    // stretch. UI thread only (every caller is a dispatcher-marshalled handler), so the dictionary needs
    // no lock.
    private readonly Dictionary<string, long> _traceThrottle = [];
    private const long TraceThrottleMs = 30_000;

    private void TraceThrottled(string key, string message)
    {
        long now = Environment.TickCount64;
        if (_traceThrottle.TryGetValue(key, out long last) && now - last <= TraceThrottleMs) return;
        _traceThrottle[key] = now;
        Trace.WriteLine(message);
    }

    /// <summary>Trace a controller input that another open surface swallowed. Keyed per surface so an
    /// arcade session (minutes of held input) logs the first rejection and then stays quiet.</summary>
    private void TraceSurfaceSwallow(string surface, string what) =>
        TraceThrottled($"swallow:{surface}:{what}",
                       $"[Wheel] {what} did nothing — {surface} is open and owns controller input");

    /// <summary>The Fn-button summon, shared by both Fn handlers so each rejection is traced once rather
    /// than written out twice. Every early return here is a wheel that visibly did not appear.</summary>
    private void FnInvoke(bool leftHanded)
    {
        if (_browserOpen) { TraceSurfaceSwallow("the Game Grid", "Fn summon"); return; }   // add-to-wheel removed
        if (!_wheelsEnabled)
        {
            TraceThrottled("fn-disabled", "[Wheel] Fn summon ignored — the wheels are DISABLED " +
                                          "(the both-Fn chord turns them back on)");
            return;
        }
        // During practice the WIZARD's set is authoritative, not the saved config (same rule as
        // FnChord below): a saved "fn" would otherwise keep a lone press live while the user is
        // building an L4/R4 + button chord, opening a wheel on the chord's first half.
        if (!(_oobePracticeAll ? _oobeFnLive : _fnOpensWheels))
        {
            TraceThrottled("fn-not-a-trigger", "[Wheel] Fn summon ignored — the Fn buttons are not an enabled " +
                                               "summon gesture (Settings ▸ Customize ▸ Triggers)");
            return;
        }
        _lastTrigger = TriggerModes.FnButtons;
        _fnInvokeAt  = Environment.TickCount64;
        InvokeWheel(leftHanded);
    }

    /// <summary>The both-Fn enable/disable chord, shared by both Fn handlers (the second press to land runs
    /// it). Traces the two ways it declines, which are otherwise indistinguishable from a dead chord.</summary>
    private void FnChord()
    {
        long since = Environment.TickCount64 - _fnInvokeAt;
        if (_overlayVisible && since > TriggerInterpreter.EnableDisableGraceMs)
        {
            TraceThrottled("fn-chord-grace",
                           $"[Wheel] both-Fn chord swallowed — the second Fn landed {since}ms after the invoke, " +
                           $"past the {TriggerInterpreter.EnableDisableGraceMs}ms together-press grace (press both at once)");
            return;
        }
        if (_oobePracticeAll ? _oobeChordToken == TriggerModes.FnButtons : _fnOpensWheels) { HandleChord(); return; }
        TraceThrottled("fn-chord-off", "[Wheel] both-Fn chord ignored — the Fn buttons are not the enabled " +
                                       "enable/disable gesture");
    }

    /// <summary>Open (or, in toggle-style, dismiss) a wheel for an invocation. Shared by the Fn handlers
    /// and the TriggerInterpreter so the side/aim rule lives in one place.</summary>
    private void InvokeWheel(bool leftHanded)
    {
        if (_oobeInvokeLocked)
        {
            // Onboarding pre-practice: the wheels debut at the practice step. Log once per lock period —
            // a dead summon must never be silent in the trace.
            if (!_oobeSuppressLogged)
            {
                _oobeSuppressLogged = true;
                Trace.WriteLine("[Wheel] summon suppressed — onboarding wizard open (pre-practice)");
            }
            return;
        }
        // Re-trigger dismisses (open wheel's style) — the toggle-mode equivalent of letting go, not a cancel.
        if (_toggleStyle && _overlayVisible) { CancelOverlay(Loc.T(UiText.Narration.Released)); return; }
        _toggleStyle = ToggleStyleFor(_lastTrigger);                        // this wheel's style, from its gesture
        var (wheel, aimRight) = ResolveTrigger(leftHanded);
        _aimWithRightStick = aimRight;
        ShowOverlay(wheel);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Fires off the UI thread; marshal before touching the window.
        Dispatcher.BeginInvoke(new Action(() => _overlay?.RefreshScreenBounds()));
    }

    /// <summary>Push MotionPolicy's effective Reduce Motion to every animated surface — the wheel and the
    /// Game Grid (whose control-level setters stop running loops on a live enable). Call whenever either
    /// policy input changes; the grid push here is what makes a toggle land while the grid is OPEN
    /// (ShowGameBrowser's own push only covers open time).</summary>
    private void ApplyMotionPolicy()
    {
        _overlay?.SetReduceMotion(MotionPolicy.Reduce);
        _overlay?.SetGamesReduceMotion(MotionPolicy.Reduce);
        _overlay?.SetArcadeReduceMotion(MotionPolicy.Reduce);
    }

    private void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        // The OS "show animations" preference feeds MotionPolicy live. It arrives under Accessibility on
        // Win10/11 (General from some setters) — the probe is one SPI read, so just re-check on both.
        if (e.Category is Microsoft.Win32.UserPreferenceCategory.Accessibility
                       or Microsoft.Win32.UserPreferenceCategory.General)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MotionPolicy.SystemPrefersReduced = NativeMethods.SystemAnimationsDisabled();
                ApplyMotionPolicy();
            }));
        }
        // Light/dark theme switches arrive as a General change — re-render the tray flower to match.
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_trayIcon is null) return;
            // NotifyIcon doesn't dispose the icon it replaces, and TrayIcon() mints a fresh HICON each call,
            // so dispose the outgoing one — else every theme/DPI change leaks a GDI handle on this long-lived process.
            try { var old = _trayIcon.Icon; _trayIcon.Icon = FlowerIcon.TrayIcon(); old?.Dispose(); }
            catch { /* keep the current icon */ }
        }));
    }

    // ── Silent invoke (empty wheel = a DISABLED side) ─────────────────────────
    // An empty wheel deliberately draws NOTHING and captures NOTHING when invoked: its trigger is
    // effectively freed for the game (e.g. one side's chord left usable in-game). We only remember the
    // invocation so that clicking L3/R3 while the gesture is HELD resurrects the wheel via the editor's
    // Add picker (EnterEditFromSilent). Toggle-style gestures have no "held" state, so they get a short
    // window after the invoke instead.
    private ActiveWheel? _silentWheel;
    private long         _silentWheelAt;
    private const long   SilentEditWindowMs = 6000;   // toggle/touchpad: stick-click window after invoking
    // Trace throttle for the silent no-op (a summon that draws nothing looks exactly like a dead summon in
    // the log): re-log only for a different side or after a quiet stretch, not per press.
    private ActiveWheel? _silentLogWheel;
    private long         _silentLogAt;
    private const long   SilentLogThrottleMs = 30_000;

    /// <summary>The invocation for the pending silent (empty) wheel is still active: physically held for
    /// hold-style gestures, or within the post-invoke window for toggle-style ones.</summary>
    private bool SilentEditReady =>
        _silentWheel is not null && _wheelsEnabled
        && !_browserOpen && !_editPickGame && !Editing
        && (!ToggleStyleFor(_lastTrigger)
            // Hold-style gesture: still ready while the invoking control is physically down — a Fn button
            // OR a chord (either may be enabled, and both can be at once). Toggle-style (touchpad, or the
            // Toggle setting) has nothing to hold, so it gets a short post-invoke window instead.
            ? (_fnLeftDown || _fnRightDown || _triggerInterpreter?.GestureHeld == true)
            : Environment.TickCount64 - _silentWheelAt <= SilentEditWindowMs);

    private void ShowOverlay(ActiveWheel wheel)
    {
        // A fresh pull supersedes any in-flight fade/readout; PrepareIntro resets both layers.
        var slices = wheel == ActiveWheel.A ? _config.Current.WheelA : _config.Current.WheelB;
        // Onboarding "try it for a spin": if the user's real wheel is empty (they declined the starter
        // layout), stand in the shipped default layout so the gesture always shows a wheel to practise on.
        if (slices.Length == 0 && _oobeSampleWheels)
            slices = wheel == ActiveWheel.A ? AppConfig.Default.WheelA : AppConfig.Default.WheelB;
        if (slices.Length == 0)
        {
            // Empty = disabled: draw nothing, suppress nothing (the game keeps receiving this gesture's
            // buttons). Only remember the invoke so a stick click can reopen the wheel in the editor.
            _silentWheel   = wheel;
            _silentWheelAt = Environment.TickCount64;
            if (_silentLogWheel != wheel || _silentWheelAt - _silentLogAt > SilentLogThrottleMs)
            {
                _silentLogWheel = wheel;
                _silentLogAt    = _silentWheelAt;
                Trace.WriteLine($"[Wheel] summon on wheel {wheel} drew nothing — the wheel is empty " +
                                "(empty = a disabled side; click the aiming stick to reopen it in the editor)");
            }
            return;
        }
        _silentWheel = null;   // a real open supersedes any pending silent invoke

        // A wheel whose ONE slice is the Arcade Launcher has nothing to aim at: the launcher IS a menu, so
        // drawing a single wedge in front of it only adds a selection to make. The summon opens it outright.
        // Restricted to the LAUNCHER — a lone slice of any other kind, a named arcade game included, still
        // draws its wheel and keeps release-while-centred as the way to back out. Practice mode is exempt:
        // nothing on the wizard's wheel may run.
        if (!PracticeMode && slices.Length == 1 && Arcade.Available
            && string.Equals(slices[0].Action?.Type, "arcade", StringComparison.OrdinalIgnoreCase)
            && ArcadeCatalog.Resolve(slices[0].Action?.Command) is null)
        {
            // OpenArcade blooms the game where the wheel was, and reads that from the last wheel opened —
            // which this summon never becomes. Set the spot this wheel would have taken.
            var arcScr = NativeMethods.PrimaryScreenDips();
            _lastWheelCx   = arcScr.X + (wheel == ActiveWheel.A ? arcScr.Width / 4.0 : arcScr.Width * 3.0 / 4.0);
            _lastWheelCy   = arcScr.Y + arcScr.Height / 2.0;
            _lastWheelSide = wheel == ActiveWheel.A ? 0 : 2;
            Trace.WriteLine($"[Wheel] summon on wheel {wheel} opened the Arcade Launcher directly " +
                            "— it is that wheel's only slice");
            OpenArcade(null);
            return;
        }

        // The anchor line for every wheel session: without it the log jumps from gesture to [FIRE] with no
        // way to tell which gesture opened which side, and a successful open looked identical to no open.
        Trace.WriteLine($"[Wheel] opened wheel {wheel} via \"{_lastTrigger ?? "(none)"}\" — {slices.Length} slices, " +
                        $"aim={(_aimWithRightStick ? "R" : "L")} style={(_toggleStyle ? "toggle" : "hold")}");
        Sfx.WheelOpened();     // kawaii: rotate to the next melody and rewind it (silent for other themes)

        // Suppress FIRST, before any heavy open work (icon extraction, logo loads, layout) — gating late
        // leaves a live-input window of tens–hundreds of ms into the game while the wheel is appearing.
        _overlayVisible = true;
        UpdateInputCapture();
        InstallMouseWatch();   // click outside the wheel dismisses it

        SetMaterialFlags(_config.Current.System.SliceMaterial);   // lighten built-in tints on dark/Mesa
        PopulateIcons(slices);
        _activeSlices = slices;
        _activeWheel  = wheel;
        _overlay!.SetSlices(slices);
        _overlay.IsWheelB = wheel == ActiveWheel.B;   // salvage: each wheel has its own glyph-tilt table
        _overlay.Reset();

        var sys = _config.Current.System ?? new SystemConfig();
        _overlay.StickyMs = sys.StickyMs;

        // Position: each wheel centred 1/4 in from its nearest side, vertically centred — always on the
        // PRIMARY display (the overlay is primary-only by design; see NativeMethods.PrimaryScreenDips).
        _overlay.RefreshScreenBounds();   // re-fit to current resolution (cheap; guards a missed event)
        var scr = NativeMethods.PrimaryScreenDips();
        double cx = scr.X + (wheel == ActiveWheel.A ? scr.Width / 4.0 : scr.Width * 3.0 / 4.0);
        _overlay.CenterAt(cx, scr.Y + scr.Height / 2.0);
        // Remembered for the arcade, which blooms at the centre of the wheel it was fired from — by then the
        // wheel is already dismissed, so the position has to be captured here.
        _lastWheelCx = cx;
        _lastWheelCy = scr.Y + scr.Height / 2.0;
        _lastWheelSide = wheel == ActiveWheel.A ? 0 : 2;
        _previewArmed = -1;   // re-evaluate the toggle-state preview for this pull

        // Window is already shown (parked idle/transparent). Set the offset start state then
        // animate in — no Show() call, so there's no stale-frame flicker when switching wheels.
        // Bloom from the pressed Fn's side: scale up with the growth origin nudged toward that side
        // (Left Fn — _aimWithRightStick — anchors left, Right Fn anchors right), plus the drift/fade.
        double originX = _aimWithRightStick ? 0.22 : 0.78;
        _overlay.PrepareIntro(Math.Sign(scr.X + scr.Width / 2.0 - cx) * sys.DriftPx, 0, originX);
        _overlay.PlayIntro(sys.FadeMs);
        _overlay.SetClickable(true);

        // Isolation is only WORTH warning about when something is in front to receive the leaked input —
        // on the bare desktop the warning names a game that isn't there. Same gate the narration below uses.
        bool leaksToForeground = !_isolated && _platform.PeekFrontmostApp() is not null;

        // The hub is the ONLY capture-state channel that reaches a couch user: the tray tooltip needs a
        // mouse hover, and a corner card can't be counted on over every game surface. The notice is STICKY for
        // this wheel-open (UpdateArmedPreview restores it on disarm) and re-evaluated on every open — the
        // state it names is live NOW, and a warning seen an hour ago doesn't say it's still true.
        //  • Passthru Mode gets a calm named chip, not the alarm — the leak is a state the user chose, and a
        //    forgotten Passthru Mode left on silently is exactly what the chip prevents.
        //  • Otherwise an isolation failure over a real foreground app gets the alarm.
        // PracticeMode (below) still outranks both: the wizard's banner is load-bearing for onboarding, and
        // practice narration already announces the leak.
        //  • A pending passthru-mode-off keeps the calm chip too: behaviour is still passthru-mode-like by design,
        //    and the alarm would contradict the "ending when the game relaunches" toast the user just saw.
        //  • The Passthru Mode chip itself is NOT sticky: it is a transient chip on a hub that is already up
        //    (never a reason for the hub to appear), shown once per summon (see _passthruNoticeShown).
        _hubStickyNotice = SafeModeActive      ? null
                         : _safeModeOffPending ? Loc.T(UiText.Toasts.PassthruEnding)
                         : leaksToForeground  ? Loc.T(UiText.Overlay.GameSeesInput)
                         : null;
        if (_hubStickyNotice is { } notice)
            _overlay.ShowHubNotice(notice);
        else if (SafeModeActive && !_passthruNoticeShown)
        {
            _passthruNoticeShown = true;
            _overlay.ShowHubNotice(Loc.T(UiText.Overlay.PassthruMode), RadialMenuControl.NoticeKind.Transient);
        }
        // Post-onboarding edit-discovery hint: a hub reminder that clicking the aiming stick edits this
        // wheel — first wheel(s) after finishing the wizard only (see EditHintDue for the 10 s window).
        // Names the stick per the chosen glyph set — "L3" on PlayStation, "LS" on Xbox — like every other prompt.
        else if (EditHintDue())
            _overlay.ShowHubNotice(
                Loc.F(UiText.Overlay.ClickToEdit, ControllerButtons.Label(_aimWithRightStick ? PadButton.R3 : PadButton.L3)), RadialMenuControl.NoticeKind.EditHint);

        // Keyboard while a wheel is open: ▲/▼ = volume, Esc = dismiss (any summon). No ◀/▶ Volume Mixer
        // keyboard mirror — the D-Pad drives mixer balance.
        RegisterWheelHotkeys();

        // Onboarding practice: tell the wizard a wheel actually bloomed (and by which gesture), so it can
        // check off the one the user just tried, and stamp the hub "Practice" from the first frame
        // (UpdateArmedPreview keeps it there for the wheel's whole lifetime).
        if (PracticeMode) _overlay?.ShowHubNotice(PracticeNoticeToken, RadialMenuControl.NoticeKind.Practice);
        _oobeWheelInvoked?.Invoke(_lastTrigger);

        // Narration: which wheel, how many slices, and anything that changes what firing will do. Concise
        // by design — the doc forbids enumerating slices on open. Not isolated is included because it means
        // the app underneath is ALSO receiving this input, which a listener has no other way to know.
        _announcer.Announce(
            Loc.P(UiText.Narration.WheelOpenOne, UiText.Narration.WheelOpenOther, slices.Length,
                  Loc.T(wheel == ActiveWheel.A ? UiText.Narration.Left : UiText.Narration.Right))
            + (PracticeMode ? Loc.T(UiText.Narration.PracticeModeSuffix) : "")
            + (leaksToForeground ? Loc.T(UiText.Narration.NotIsolatedSuffix) : ""),
            AnnouncementKind.Context);
    }

    /// <summary>Practice is on while the onboarding wizard previews (<see cref="_oobePreview"/>) OR while
    /// the Settings window is open — a wheel summons/aims/arms normally but firing runs NOTHING, so the
    /// user can safely poke at wheels mid-configuration (e.g. the Test Wheel buttons) without launching
    /// games or sleeping the PC. Checked live per invoke/arm/fire, so closing Settings restores live
    /// wheels immediately.
    /// <para>The Settings half is opt-out via <see cref="SystemConfig.PracticeWhileSettingsOpen"/>
    /// (Developer Settings) — off means wheels stay FULLY LIVE with Settings open, so a fired slice really
    /// runs. Onboarding's preview always practices regardless.</para></summary>
    internal bool PracticeMode =>
        _oobePreview
        || (_settingsWindow is { IsLoaded: true } && _config.Current.System.PracticeWhileSettingsOpen);

    /// <summary>The hub-notice token for the current practice source: a bare "Practice" keeps the pill's
    /// default "Actions are Disabled" subtitle (onboarding); the "|" suffix swaps the subtitle in the
    /// Settings-open case (parsed by RadialMenuControl.DrawNotice).</summary>
    private string PracticeNoticeToken => _oobePreview ? "Practice" : "Practice|" + Loc.T(UiText.Overlay.WhileSettingsOpen);

    // ── Onboarding practice hooks (set by the wizard's "Take it for a spin" step) ──────────────────────
    /// <summary>While true (the whole wizard EXCEPT the final "Roll out" step), an invoked wheel shows a
    /// "Practice" hub chip and firing runs nothing — so the user tries gestures without triggering actions.
    /// The finish step turns this OFF (SetOobePreview(false)) so the wheels are fully live there.</summary>
    internal bool _oobePreview;
    /// <summary>While true, invoking an EMPTY wheel shows the shipped default layout as a practice sample
    /// instead of the silent/disabled no-op — so the trigger-practice step always has a wheel to try.</summary>
    internal bool _oobeSampleWheels;
    /// <summary>Fired (on the UI thread, from ShowOverlay) each time a wheel blooms, with the trigger
    /// token that invoked it — the wizard ticks off that gesture. Null except during the practice step.</summary>
    internal Action<string?>? _oobeWheelInvoked;
    /// <summary>While true (the wizard's pre-practice steps), wheel invocation is disabled entirely — the
    /// wheels first appear at the practice step, where the flow introduces them. Cleared when the wizard
    /// reaches practice or closes.</summary>
    internal bool _oobeInvokeLocked;
    /// <summary>One "[Wheel] summon suppressed" trace per lock period, not per press — reset whenever the
    /// lock flips (SetInvokeLocked / wizard close), so a re-lock logs again.</summary>
    private bool _oobeSuppressLogged;
    /// <summary>Practice mode: the wizard's practice set (its Recommended + Custom chords) is live at once,
    /// independent of the saved config. The live tokens themselves are pushed to the interpreter via the
    /// SetPracticeAll hook; this flag gates the host-side Fn handlers + practice behaviours.</summary>
    internal bool _oobePracticeAll;
    /// <summary>Whether the Fn/L4-R4 LONE press is part of the wizard's live practice set. Practice-all
    /// alone is not enough to answer that: the wizard also puts L4/R4 + a second button on offer, and a
    /// lone press must go dead while such a chord is selected or it would open a wheel before the chord
    /// could ever be completed. Meaningless unless <see cref="_oobePracticeAll"/>.</summary>
    internal bool _oobeFnLive;
    /// <summary>The trigger token behind the most recent invoke ("fn" from the Fn handlers; chord/swipe
    /// tokens from the TriggerInterpreter). Feeds the onboarding check-off (<see cref="_oobeWheelInvoked"/>)
    /// AND decides the opening wheel's toggle-style (<see cref="ToggleStyleFor"/>) — important with several
    /// gestures enabled at once, where a hold chord and a toggle-only touchpad swipe coexist.</summary>
    internal string? _lastTrigger;
    /// <summary>Fired when the enable/disable chord toggles the wheels — the wizard's chord-practice
    /// check-off. Null except during the practice step.</summary>
    internal Action? _oobeChordPracticed;
    /// <summary>Raw button up/down feed for the wizard's live Hold/Tap halves (J5). Null except during
    /// the practice step — the forwarding hot path checks null before marshalling.</summary>
    internal Action<string, bool>? _oobeChordButtons;
    /// <summary>The wizard's CHOSEN trigger token: during practice-all, only THIS token's enable/disable
    /// chord toggles the wheels (a user practicing a different pair than the card displays would learn the
    /// wrong chord). Null = unrestricted (practice off).</summary>
    internal string? _oobeChordToken;

    private void CloseOverlay()
    {
        // StickyArmedIndex keeps the last armed slice for a short grace window, so releasing
        // Fn and recentring the stick together still fires instead of cancelling.
        int armed = _overlay?.StickyArmedIndex ?? -1;
        // Past-the-end = cancel too: a config reload can shrink the visible wheel while a sticky index
        // from the larger layout is still latched (the state machine doesn't clamp on SetSlices).
        if (armed < 0 || _activeSlices is null || armed >= _activeSlices.Length)
        {
            // Two very different things land here and the user sees one symptom ("I released and it didn't
            // fire"): a genuine centred release, versus a sticky index left dangling by a wheel that shrank
            // under it. Only the second is a bug, so name it.
            if (armed >= 0)
                Trace.WriteLine($"[Wheel] release did NOT fire — armed slice {armed} is past the end of the " +
                                $"current wheel ({_activeSlices?.Length ?? 0} slices); a config reload shrank it mid-pull");
            CancelOverlay(Loc.T(UiText.Narration.Released));          // released while centred → let go (quick fade)
            return;
        }
        // Require-confirm slices fire on release ONLY if their hold dwell has completed while
        // still selected; otherwise the release just cancels them.
        bool needsConfirm = _activeSlices[armed].Action?.RequireConfirm == true;
        if (needsConfirm && !(_overlay?.ArmedConfirmReady ?? false))
        {
            Trace.WriteLine($"[Wheel] release did NOT fire \"{_activeSlices[armed].Label}\" — it's a " +
                            "hold-to-confirm slice and the dwell hadn't completed");
            CancelOverlay();
            return;
        }
        FireAction(_activeSlices[armed]);       // FireAction dismisses (or lingers for toggles)
    }

    /// <summary>Cancel (no slice fired): release input and quickly fade the whole wheel out.
    /// <para><paramref name="spoken"/> distinguishes the two ways this is reached, which mean different
    /// things to a listener. Simply letting go of the wheel is "Released": the session may have done real
    /// work on the way (a d-pad volume scrub completes and is NOT undone by letting go), so "Cancelled"
    /// would wrongly report that nothing happened. An explicit ○ / Esc, or abandoning a hold-to-confirm
    /// dwell, genuinely cancels an intent and keeps that word.</para></summary>
    private void CancelOverlay(string? spoken = null)
    {
        SoftReleaseInput();
        _overlay?.Reset();
        _overlay?.FadeOutCancel();
        // Context-kind: drops any pending selection announcement with it, so nothing stale speaks after
        // the wheel is gone.
        _announcer.Announce(spoken ?? Loc.T(UiText.Narration.Cancelled), AnnouncementKind.Context);
    }

    // ── Click-outside-to-dismiss ─────────────────────────────────────────────────
    // The overlay is click-through, so a global low-level mouse hook (installed only while a wheel is up)
    // is how we notice a click outside the wheel. A click outside the disc dismisses the wheel; the click
    // still passes through to whatever's underneath.
    private IntPtr _mouseHook = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _mouseProc;   // hold the delegate so it isn't GC'd
    /// <summary>Optional screen-pixel hit test whose hits do NOT dismiss an open wheel. The hook only ever
    /// knows a screen point (no hwnd), so a window that wants some of its own controls to be click-through
    /// for dismissal purposes has to describe them geometrically. Set/cleared by the onboarding wizard.</summary>
    private Func<int, int, bool>? _overlayClickExempt;

    private void InstallMouseWatch()
    {
        if (_mouseHook != IntPtr.Zero) return;
        _mouseProc ??= MouseHookProc;
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc,
            NativeMethods.GetModuleHandle(null), 0);
        if (_mouseHook == IntPtr.Zero)
            Trace.WriteLine($"[Input] mouse hook install failed (Win32 error {Marshal.GetLastWin32Error()}) - click-outside dismissal unavailable");
    }

    /// <summary>Windows silently drops a low-level hook whose installing thread misses the hook timeout and no
    /// API reports the drop, so the hook is re-armed on a cadence while any dismiss-on-click surface is up.
    /// Must run on the UI thread, which services the hook.</summary>
    private void RearmMouseWatch()
    {
        // A zero handle (a refused install) is retried here too, so one failure never costs the whole session.
        if (_mouseHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        InstallMouseWatch();
    }

    private void RemoveMouseWatch()
    {
        if (_mouseHook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                int x = data.ptX, y = data.ptY;
                Dispatcher.BeginInvoke(new Action(() => OnGlobalClick(x, y)));
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void OnGlobalClick(int screenX, int screenY)
    {
        if (_overlay is null) return;
        // Arcade up: a click outside the round window dismisses it, mirroring the wheel and the grid.
        if (_arcadeOpen)
        {
            if (_overlay.IsPointOutsideArcade(screenX, screenY)) CloseArcade();
            return;
        }
        // Game Grid up: a click outside the card dismisses it (edit-pick cancels back to edit — same as ○/Esc).
        if (_browserOpen)
        {
            if (_overlay.IsPointOutsideGamesCard(screenX, screenY)) CloseGameBrowser();
            return;
        }
        // Plain wheel (not edit): a click outside the disc dismisses it.
        if (!_overlayVisible || Editing) return;
        // …unless something has claimed this screen region as "clicking here shouldn't dismiss". Set only by
        // the onboarding Material step, over its material/thickness tiles: those apply LIVE to the open
        // wheel, so dismissing it on the click defeats the whole point of previewing them with the mouse.
        if (_overlayClickExempt?.Invoke(screenX, screenY) == true) return;
        if (_overlay.IsPointOutsideWheel(screenX, screenY))
        {
            // A stray click dismissing the wheel is otherwise indistinguishable in the log from a user
            // cancel — and on a couch rig the mouse gets bumped without anyone touching it.
            Trace.WriteLine($"[Wheel] dismissed by a click outside the disc at ({screenX},{screenY})");
            CancelOverlay();
        }
    }

    /// <summary>Stop consuming controller/hotkey input and mark the overlay inactive, without
    /// touching the wheel's visuals (so a fade-out can still play).</summary>
    private void SoftReleaseInput()
    {
        UnregisterWheelHotkeys();
        _volumeHideTimer?.Stop();
        StopDpadMicRepeat();
        ReleaseTaskSwitcher();   // wheel is going away: let Alt up, committing the highlighted window
        _overlay?.SetScrubber(false, 0);
        _overlay?.SetClickable(false);
        _overlayVisible = false;
        RemoveMouseWatch();
        UpdateInputCapture();
    }

    /// <summary>Bring up the ViGEm/HidHide drivers shortly AFTER startup rather than synchronously.
    /// Initializing the virtual pad the instant a prior instance was force-killed can race the bus
    /// driver's cleanup of that dead instance's handles → an intermittent ntdll heap-corruption crash
    /// at launch. A short settle window lets the kernel reclaim those handles first; a single retry
    /// covers a transient (catchable) connect failure. (True heap corruption can't be caught — the
    /// settle delay is the actual mitigation; the goal is to not start the pad into a half-torn-down
    /// driver state.) Nothing needs the pad in the first fraction of a second after launch.</summary>
    private void InitInputCaptureDeferred()
    {
        var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        settle.Tick += (_, _) =>
        {
            settle.Stop();
            try
            {
                // Defensive: clear any cloak a prior crash/kill may have left behind — both on the
                // currently-connected controller AND on anything a prior session recorded to disk (which
                // may be disconnected now and impossible to re-enumerate) — then (re)establish our own
                // cloak + the virtual pad.
                // Only lift ids WE recorded (cloaked-ids.txt) — a connected-controller sweep would strip
                // blocks that another tool (DS4Windows / reWASD / a manual HidHide setup) owns. Our own
                // strandings are fully covered by the recovery file, which Hide() rewrites on every cloak.
                var stale = HidHideManager.PersistedCloakedIds();
                bool recovered = stale.Count == 0 || _hidHide.Unhide(stale);
                // A failed release may leave owned blocks active. Preserve bookkeeping and the durable
                // recovery record; capture revalidates the driver state before treating it as isolated.
                if (recovered)
                {
                    _cloakedIds.Clear();
                    _xboxCloakedIds.Clear();
                }
                else Trace.WriteLine("[Capture] startup unhide failed — retaining recovery ownership");
                UpdateInputCapture();
                // Proactively catch the HidHide self-lockout (driver cloaking a device while our exe isn't
                // on the allow-list → we go blind to our own pad). Surfaces a one-click elevated fix instead
                // of leaving a dead-controller mystery. Post-heal: if the stale-cloak unhide above needed
                // elevation we didn't have, the cloak persists and this still fires. (gotcha #1)
                if (_hidHide.DetectSelfLockout()) NudgeHidHideLockout();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Capture] deferred init failed, retrying once: {ex.Message}");
                var retry = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                retry.Tick += (_, _2) =>
                {
                    retry.Stop();
                    try { UpdateInputCapture(); }
                    catch (Exception ex2) { Trace.WriteLine($"[Capture] retry failed: {ex2.Message}"); }
                };
                retry.Start();
            }
        };
        settle.Start();
    }

    /// <summary>Mirror Trace to %APPDATA%\Radiata\radiata-trace.log (timestamped, rotated at ~1 MB) so the
    /// [Capture]/[HidHide]/[Emu]/[Controller] diagnostics are visible in the field, not just under a
    /// debugger/DebugView. Best-effort: diagnostics must never block startup.</summary>
    private static void SetupFileTrace()
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            string path = System.IO.Path.Combine(AppPaths.AppDataDir, "radiata-trace.log");
            AdoptLegacyTraceGeneration(path);
            if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 1_000_000)
                RotateTraceGenerations(path);
            Trace.Listeners.Add(new TimestampTraceListener(path));
            Trace.AutoFlush = true;
            var v = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                    System.Reflection.Assembly.GetExecutingAssembly())?.InformationalVersion;
            Trace.WriteLine($"[App] ── session start {DateTime.Now:yyyy-MM-dd} — Radiata {v}");
        }
        catch (Exception ex) { Trace.WriteLine($"[App] file trace unavailable: {ex.Message}"); }
    }

    /// <summary>How many rotated trace logs to keep beside the live one: radiata-trace.log.1 (newest)
    /// through .3. Rotation only runs at session start, so a build-and-relaunch day rotates several
    /// times — multiple generations keep a morning fault recoverable by evening.</summary>
    private const int TraceGenerations = 3;

    /// <summary>Fold the legacy single-generation "<c>.old</c>" file into the numbered scheme so its
    /// contents survive the switch. Once .1 exists the new scheme is established and a leftover .old is
    /// older than every generation we keep, so it goes.</summary>
    private static void AdoptLegacyTraceGeneration(string path)
    {
        string legacy = path + ".old";
        if (!System.IO.File.Exists(legacy)) return;
        if (System.IO.File.Exists(path + ".1")) System.IO.File.Delete(legacy);
        else                                    System.IO.File.Move(legacy, path + ".1");
    }

    /// <summary>Shift the rotated logs down one slot (.2 → .3, .1 → .2, dropping the oldest) and move the
    /// live log into .1.</summary>
    private static void RotateTraceGenerations(string path)
    {
        System.IO.File.Delete($"{path}.{TraceGenerations}");   // no-op when absent
        for (int gen = TraceGenerations - 1; gen >= 1; gen--)
        {
            string from = $"{path}.{gen}";
            if (System.IO.File.Exists(from)) System.IO.File.Move(from, $"{path}.{gen + 1}", overwrite: true);
        }
        System.IO.File.Move(path, path + ".1", overwrite: true);
    }

    /// <summary>File trace listener that survives a failed open. ⚠ Don't go back to
    /// <c>TextWriterTraceListener(path)</c>: it opens lazily and a first open that throws nulls its writer
    /// and silently drops every later line for the whole process. This one opens eagerly; on failure it
    /// keeps retrying from a timer, holding a bounded backlog that is replayed, with the failure named, once
    /// an open succeeds — and a listener disposed while still shut makes one last synchronous open so that
    /// backlog lands rather than dying with the process.
    /// <para>The primary log keeps FileShare.Read (the "primary holds radiata-trace.log write-locked"
    /// contract the sibling logs exist for) and a 30 s retry. <paramref name="shortLived"/> is for the
    /// sibling files written by one-shot processes (watchdog / elevated workers / drivers): they exit long
    /// before 30 s, so their retry is 2 s, and they share the file ReadWrite so two one-shots appending to
    /// the same sibling log do not lock each other out (append mode keeps whole lines intact).</para></summary>
    private sealed class TimestampTraceListener : TraceListener
    {
        private const int BacklogMax = 200;
        private readonly string _path;
        private readonly int _retryMs;
        private readonly System.IO.FileShare _share;
        private readonly object _gate = new();
        private readonly System.Collections.Generic.Queue<string> _backlog = new();
        private readonly System.Text.StringBuilder _partial = new();
        private System.IO.StreamWriter? _w;
        private System.Threading.Timer? _retry;
        private string? _firstFailure;
        private string? _lastFailure;
        private int _attempts;
        private bool _disposed;

        public TimestampTraceListener(string path, bool shortLived = false)
        {
            _path = path;
            _retryMs = shortLived ? 2_000 : 30_000;
            _share = shortLived ? System.IO.FileShare.ReadWrite : System.IO.FileShare.Read;
            if (TryOpen(out var why)) return;
            _firstFailure = why;
            ArmRetry();
        }

        /// <summary>⚠ Keeps retrying, and never stops on its own. The window this listener opens in is
        /// LOGON — profile still mounting, anti-malware scanning the profile, Controlled Folder Access
        /// arbitrating — which is exactly when a file open transiently fails, and exactly the session whose
        /// diagnostics matter most (autostart, the cloak heal). A single retry that gives up costs the whole
        /// session's trace and says nothing about it, which reads as "the app never ran".</summary>
        private void ArmRetry()
        {
            if (_disposed) return;
            _retry = new System.Threading.Timer(_ => Retry(), null, _retryMs, System.Threading.Timeout.Infinite);
        }

        private bool TryOpen(out string? why)
        {
            try
            {
                var fs = new System.IO.FileStream(_path, System.IO.FileMode.Append,
                                                  System.IO.FileAccess.Write, _share);
                _w = new System.IO.StreamWriter(fs) { AutoFlush = true };
                why = null;
                return true;
            }
            catch (Exception ex) { why = ex.Message; return false; }
        }

        private void Retry()
        {
            lock (_gate)
            {
                _retry?.Dispose(); _retry = null;
                if (_disposed || _w is not null) return;
                _attempts++;
                // Still shut: keep the backlog (it is capped, so it cannot grow without bound) and try
                // again. Dropping it here is what turned one unlucky open into a silent session.
                if (!TryOpen(out var why)) { _lastFailure = why; ArmRetry(); return; }
                _w!.WriteLine($"{Stamp()} [App] file trace recovered after {_attempts} attempt(s) - first open "
                              + $"failed: {_firstFailure}"
                              + (_lastFailure is not null && _lastFailure != _firstFailure ? $"; last: {_lastFailure}" : "")
                              + $" ({_backlog.Count} line(s) replayed from backlog)");
                while (_backlog.Count > 0) _w.WriteLine(_backlog.Dequeue());
            }
        }

        private static string Stamp() => DateTime.Now.ToString("HH:mm:ss.fff");

        public override void Write(string? message)
        {
            lock (_gate) _partial.Append(message);
        }

        public override void WriteLine(string? message)
        {
            lock (_gate)
            {
                string line = $"{Stamp()} {Sanitize(_partial.Append(message).ToString())}";
                _partial.Clear();
                if (_w is not null) { try { _w.WriteLine(line); } catch { /* never block the caller */ } return; }
                if (_backlog.Count >= BacklogMax) _backlog.Dequeue();
                _backlog.Enqueue(line);
            }
        }

        public override void Flush() { lock (_gate) { try { _w?.Flush(); } catch { } } }

        protected override void Dispose(bool disposing)
        {
            // ⚠ _disposed, not just cancelling the timer: retries never give up on their own, so a retry
            // already waiting on the gate would otherwise re-open the file after shutdown.
            if (disposing) lock (_gate)
            {
                _disposed = true; _retry?.Dispose(); _retry = null;
                // Still shut with lines waiting: the short-lived decision listeners are disposed within
                // seconds of a failed first open, before any retry fires, and their backlog IS the evidence
                // of what a logon launch decided. One last open here is what makes those lines land.
                if (_w is null && _backlog.Count > 0 && TryOpen(out _))
                {
                    try
                    {
                        _w!.WriteLine($"{Stamp()} [App] file trace recovered at close - first open failed: "
                                      + $"{_firstFailure} ({_backlog.Count} line(s) replayed from backlog)");
                        while (_backlog.Count > 0) _w.WriteLine(_backlog.Dequeue());
                    }
                    catch { /* nothing left to try */ }
                }
                _w?.Dispose(); _w = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>Keep one traced event on one line. Plenty of what gets logged is data we didn't author -
        /// game names from Playnite/Steam/itch, install dirs, URLs - so a game called
        /// "Foo\n12:00:00.000 [HidHide] un-cloaked" would forge log lines that look native. The trace log is
        /// what users are asked to send when something breaks, so its integrity is the whole point;
        /// control characters are escaped rather than dropped so nothing silently disappears.</summary>
        private static string? Sanitize(string? message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            bool dirty = false;
            foreach (var c in message)
                if (c is '\r' or '\n' || char.IsControl(c) && c != '\t') { dirty = true; break; }
            if (!dirty) return message;

            var sb = new System.Text.StringBuilder(message.Length + 8);
            foreach (var c in message)
                if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (char.IsControl(c) && c != '\t') sb.Append($"\\x{(int)c:X2}");
                else sb.Append(c);
            return sb.ToString();
        }
    }

    /// <summary>Reconcile the virtual pad + cloak with the current mode. While a controller is present
    /// (and Passthru Mode is off) the game sees ONE stable virtual pad for the whole session:
    ///  • <b>XBox Mode</b> → a permanent Xbox-360 pad (universal XInput);
    ///  • <b>native</b> (default) → a permanent DualShock 4 pad standing in for the cloaked DualSense.
    /// The physical pad is cloaked exactly while a virtual pad stands in, and the virtual pad reports
    /// neutral while an overlay is open so the game gets nothing.
    /// <para><b>The wheels toggle is ROUTING-ONLY — it must never touch the pad or the cloak.</b>
    /// Wheels off = summons ignored and input forwarded 1:1 through the standing virtual pad; the game
    /// notices nothing. Two verified failure classes forbid making the toggle a device event: (1) games
    /// bind a pad at arrival and can latch a destroyed one (input dead until the game re-scans), and
    /// (2) HidHide filters device OPENS only — any un-cloak window lets Steam (which holds controller
    /// handles process-wide and never exits) or the game open the physical pad, and a later re-cloak can
    /// never take that handle back: permanent double input. See docs/INPUT-CAPTURE.md ▸ "The cloak blocks
    /// future opens only". The device-level release paths are Passthru Mode, app exit, and UncloakForReset —
    /// deliberate, rare, and understood to hand the pad to whatever is running.</para>
    /// Switching XBox⇄native swaps the pad type once. No ViGEmBus → no pad comes up and the raw controller
    /// passes straight through (the only un-captured fallback).</summary>
    private void UpdateInputCapture()
    {
        // Standalone pre-config launches (--install-drivers) reuse the driver engine before any capture
        // or config state exists — there is nothing to reconcile.
        if (_config is null) return;
        // Never let capture reconciliation throw to a caller (the Connected handler, the chord handler,
        // ToggleEmulation, overlay show/hide) — an unhandled dispatcher exception would crash the app AND
        // trip the racy crash-uncloak. The emulator start is already guarded internally; this also contains
        // a throw from _emulator.Stop() / GetHidInstanceIds() / the Suppressed setter. A caught partial
        // failure is re-reconciled on the next call (this method runs on nearly every state change).
        try { UpdateInputCaptureCore(); }
        catch (Exception ex)
        {
            // A failed pass must not inherit the last pass's "down by design" — the flag is only ever a
            // SUCCESSFUL pass's conclusion, and a stale true stands the watchdog down over a state nobody
            // decided (the re-reconcile below depends on the watchdog actually re-running capture).
            _padDownByDesign = false;
            Trace.WriteLine($"[Capture] update failed: {ex.Message}");
        }
    }

    /// <summary>Make the physical controller fully normal (un-cloak + release the virtual pad) ahead of a
    /// destructive reset that deletes %APPDATA%\Radiata — including the cloak-recovery record. The Wipe App
    /// Data flow calls this BEFORE deleting, so a hard-kill in the wipe→restart window can't strand the
    /// cloak with its recovery file already gone (TearDown covers the clean path; this closes the window).</summary>
    internal bool UncloakForReset()
    {
        // Explicit device-level release. This must NOT ride on the wheels flag: the chord is routing-only
        // and releases nothing (see UpdateInputCapture remarks).
        try
        {
            _emulator.Stop();
            if (!_hidHide.Unhide(HidHideManager.PersistedCloakedIds())) return false;
            _cloakedIds.Clear();
            _xboxCloakedIds.Clear();
            Trace.WriteLine("[Capture] uncloak-for-reset — virtual pad released, cloak lifted");
            return true;
        }
        catch (Exception ex) { Trace.WriteLine($"[Capture] uncloak-for-reset failed: {ex.Message}"); return false; }
    }

    private void UpdateInputCaptureCore()
    {
        // Every modal overlay surface counts, not just a wheel: while one is up the physical pad is cloaked
        // and the virtual pad is held NEUTRAL so the game underneath gets nothing. The arcade is the longest-
        // lived of the three by far (minutes, not the fraction of a second a wheel is up), which is exactly
        // why it must be in this OR — and why OpenArcade additionally refuses to run un-isolated over a game.
        bool overlay = _overlayVisible || _browserOpen || _arcadeOpen;
        // An Xbox pad read over XInput (Kind=Xbox) is CAPTURED like any Sony pad (default on). HidHide CAN
        // hide XInput pads — but only via the XUSB devnode under the XnaComposite/XboxComposite classes,
        // not the HID-class instance ids (feeding it those silently does nothing). An Xbox-class pad over
        // BLUETOOTH emulating a DS4 (Kind=PlayStationOther) takes the normal Sony path either way.
        // The reader clears its backend kind on loss. Preserve the last connected family until
        // a real replacement connects; absence must not release Xbox ownership or create a DS4.
        var captureKind = _captureContinuity.ResolveKind(DetectedControllerKind);
        bool xinputXbox = captureKind == ControllerKind.Xbox;
        // XInputCapture=false is the config escape hatch into permanent best-effort — a decision, not an
        // outage, so the watchdog must not fight it.
        bool padDownByDesign = xinputXbox && !SafeModeActive && !_config.Current.System.XInputCapture;

        // ViGEmBus absent: don't cloak at all. Cloaking succeeds, the virtual pad then fails to start, and
        // the un-cloak failsafe lifts it again — one driver cycle per pass, and a burst of device
        // notifications makes a burst of them. The latch is set by a failed start (below) and cleared by a
        // read-only probe HERE, before any cloak decision, so the cloak-before-pad ordering is never
        // inverted: the pass that finds the bus back does the normal cloak-then-start in full.
        // ⚠ The probe costs a throwaway ViGEmClient (which THROWS while the bus is missing), so it runs
        // only while the latch is up — a working machine never pays it.
        if (_busMissing && _emulator.ProbeDriverInstalled())
        {
            _busMissing = false;
            Trace.WriteLine("[Capture] ViGEmBus is back — resuming normal cloak + virtual pad");
        }
        // Not "by design": the cure is Settings ▸ Advanced ▸ Install/Repair Drivers, and the isolation note
        // below must keep saying so rather than degrading to the generic Xbox-fallback wording.
        bool busMissing = _busMissing;


        // Xbox capture: cloak the pad's WHOLE devnode tree (composite parent + children, per
        // nefarius/HidHide#39) BEFORE any virtual pad exists, and gate the pad on the cloak being
        // CONFIRMED for new opens. Existing handles can survive cloaking, so this does not prove that an
        // already-running game sees only one pad. This ordering is why an
        // untested pad family degrades to best-effort instead of breaking. Outcome-tracked like the Sony
        // path: re-enumerated every pass, so a replug/transport change (new ids) re-cloaks instead of
        // latching stale success. SystemConfig.XInputCapture=false forces best-effort permanently.
        bool xboxCloaked = false;
        // Set on the paths where the pad is ABSENT or its identity is unresolvable (loss in progress).
        // The un-cloak failsafe below must NOT lift the block-list there: entries are keyed on the device
        // instance path, which is stable across reconnects, so a retained entry means the pad re-enumerates
        // already cloaked and Steam never gets the device-arrival race (which it otherwise wins ~50/50 —
        // fresh process-wide handle, permanent double input; no re-cloak can evict it).
        bool padGoneRetainCloak = false;
        // A BT Xbox cloak awaiting its observed-isolation probe: retain the cloak (don't churn the
        // driver) and don't treat the momentarily-down pad as an outage. Normally resolves within ~1s;
        // hard-bounded by BtObserveTimeoutMs, past which the hold fails CLOSED into best-effort.
        bool btVerifyPending = false;
        // The multi-pad guard is the ONE uncloaked-Xbox cause with a nameable cure (unplug a pad), so it
        // gets its own isolation note — every other cause must fall to the generic wording. Mapping the
        // shared "Xbox fallback" note to the multi-pad cure tells a single-BT-pad user in deliberate
        // best-effort to unplug a second pad that doesn't exist.
        bool multiPadGuard = false;
        // Both cloak blocks also skip while a passthru-mode-off hold is pending: cloaking during the hold
        // cuts an XInput game off from the real pad instantly (XInput polls slots — no durable handle
        // to survive on), which was measured as a fully dead controller. Cloak + pad land together at
        // completion instead; the cost — wheel input bleeding to the game during the hold, and a fresh
        // pre-cloak handle being possible — merely continues the Passthru Mode state the user chose.
        bool padCountUnstable = false;
        // A connected Xbox source whose devnode hasn't enumerated yet (see the no-devnode branch):
        // inIdentityGap while in that state; identityHold while the bounded neutral hold covers it.
        bool inIdentityGap = false, identityHold = false;
        // A second Xbox-class pad listed for under MultiPadConfirmMs with a capture already established: held as
        // is (usually one pad mid-transport-switch); Neutral when the reader's source changed since it appeared.
        var multiPadHold = ControllerCaptureContinuity.MultiPadHold.None;
        if (xinputXbox && _config.Current.System.XInputCapture && !SafeModeActive && !_safeModeOffPending && !busMissing)
        {
            var tree   = XboxDeviceTree.GetPhysicalXboxInstanceIds(out int padCount, out bool treeEnumFailed);
            var btTree = XboxDeviceTree.GetBluetoothXboxInstanceIds(out int btPadCount, out bool btEnumFailed);
            bool sweepOk = !treeEnumFailed && !btEnumFailed;
            if (!treeEnumFailed) UpdateStuckRootEpisode(XboxDeviceTree.StuckRoots);
            bool multiListed = sweepOk && padCount + btPadCount > 1;
            if (multiListed)
            {
                long now = Environment.TickCount64;
                if (_multiPadSinceMs == 0) { _multiPadSinceMs = now; _multiPadConnectEpoch = _connectEpoch; }
                multiPadHold = ControllerCaptureContinuity.HoldForMultiPad(
                    _emulator.Active && _emulator.PadType == EmulatedPad.Xbox360,
                    _xboxCloakedIds.Count > 0 && _hidHide.VerifyCloak(_xboxCloakedIds.ToList()),
                    now - _multiPadSinceMs,
                    _controllerConnected, _connectEpoch == _multiPadConnectEpoch);
            }
            else if (sweepOk) _multiPadSinceMs = 0;
            // A failed sweep returns empty lists, which the breaker would read as "every pad gone" and
            // release a session hold on — only a complete sweep may feed it. A confirmation hold is not a
            // guard trip: counting it would latch the breaker after three ordinary transport switches.
            if (sweepOk)
                padCountUnstable = PadCountChurnBreaker(
                    multiListed && multiPadHold == ControllerCaptureContinuity.MultiPadHold.None, tree, btTree);
            if (treeEnumFailed || btEnumFailed)
            {
                // Empty because the SWEEP failed, not because nothing is connected: no new cloak is placed on
                // an identity that could not be read, but any retained entries stay (lifting them would hand
                // a present pad to whatever opens it first). "By design" here would stand the watchdog down
                // over a cloaked pad with no stand-in — leave it armed so capture retries within ~10s.
                TraceThrottled("xbox-enum-failed",
                               "[Capture] XInput capture on, but devnode enumeration FAILED — not cloaking, retained entries kept until a sweep succeeds (watchdog retries)");
                padGoneRetainCloak = true;
                padDownByDesign = !_controllerConnected;   // pad present + unenumerable = outage, not decision
            }
            else if (padCountUnstable)
            {
                // Held: no cloak, no stand-in, watchdog stood down. The un-cloak block below lifts whatever
                // is still hidden (same route as the multi-pad guard).
                padDownByDesign = true;
            }
            else if (multiPadHold != ControllerCaptureContinuity.MultiPadHold.None)
            {
                // Status quo: the owned cloak and the stand-in stay, and the new listing is NOT cloaked (it may
                // be a second player's pad, which must stay usable). The heartbeat re-runs capture so the guard
                // still trips at MultiPadConfirmMs if both stay listed.
                xboxCloaked = true;
                padGoneRetainCloak = true;
                ScheduleCaptureRecheck();
                TraceThrottled("multi-pad-hold",
                               $"[Capture] {padCount + btPadCount} Xbox-class pads listed — holding the current capture up to "
                               + $"{ControllerCaptureContinuity.MultiPadConfirmMs / 1000} s (a transport switch lists both transports briefly)"
                               + (multiPadHold == ControllerCaptureContinuity.MultiPadHold.Neutral
                                  ? "; input held neutral: the source reconnected since" : ""));
            }
            else if (padCount + btPadCount > 1)
            {
                // Multi-pad guard, counted across BOTH transports. Cloaking here is all-or-nothing across
                // every physical Xbox-class pad, but only the reader's slot gets forwarded to the virtual
                // pad — so cloaking two pads makes the second player's controller VANISH. Until
                // device→slot identity exists (V4 controller picker), a second pad means we don't cloak at
                // all: the wheel still reads and draws, both pads keep working, and the cost is the known
                // best-effort input leak while a wheel is open. Strictly better than eating a controller.
                TraceThrottled("multi-pad-guard",
                               $"[Capture] {padCount + btPadCount} physical Xbox-class pads present — NOT cloaking "
                               + "(can't tell which pad feeds the virtual one; best-effort mode keeps both usable)");
                padDownByDesign = true;
                multiPadGuard = true;
            }
            else if (tree.Count > 0)
            {
                var missing = _hidHide.VerifyCloak(tree) ? tree.Where(id => !_cloakedIds.Contains(id)).ToList() : tree.ToList();
                if (missing.Count == 0) xboxCloaked = true;
                else if (_hidHide.Hide(missing))
                {
                    foreach (var id in missing) { _cloakedIds.Add(id); _xboxCloakedIds.Add(id); }
                    xboxCloaked = true;
                    Trace.WriteLine($"[Capture] XInput pad cloaked ({tree.Count} devnodes)");
                }
                else Trace.WriteLine("[Capture] XInput cloak FAILED — staying in best-effort mode (no virtual pad)");
            }
            else if (btTree.Count > 0)
            {
                // Bluetooth path: the pad's game-visible devnode is a HIDClass entry (xinputhid.sys),
                // and blocking it starves XInput for non-allow-listed processes. Gate the virtual pad on
                // an OBSERVED isolation check, never on Hide() success: Radiata is allow-listed so its
                // own view proves nothing, and the helper probe (a different, non-allow-listed image)
                // must see ZERO pads before the stand-in comes up — see XboxBtIsolationProbe. Anything
                // short of an observed zero un-cloaks and stays best-effort (a blocked pad with no
                // virtual stand-in would be a DEAD controller, worse than bleed-through).
                string btKey = string.Join("|", btTree.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
                if (btKey != _btCloakKey)
                {
                    _btCloakKey = btKey;         // device set changed — the old observation doesn't transfer
                    _btObserved = BtObserved.None;
                    _btObserveDeadlineMs = 0;
                }
                if (_btObserved == BtObserved.Refused)
                {
                    padDownByDesign = true;      // observed (or unprovable) — best-effort until the set changes
                }
                else
                {
                    var missing = _hidHide.VerifyCloak(btTree) ? btTree.Where(id => !_cloakedIds.Contains(id)).ToList() : btTree.ToList();
                    bool blocked = missing.Count == 0;
                    if (!blocked && _hidHide.Hide(missing))
                    {
                        foreach (var id in missing) { _cloakedIds.Add(id); _xboxCloakedIds.Add(id); }
                        blocked = true;
                        // A re-cloak of a set this session already OBSERVED reuses the verdict (keyed to
                        // the exact id set) — say so, or the trace reads like the gate was skipped.
                        Trace.WriteLine(_btObserved == BtObserved.Verified
                            ? $"[Capture] BT Xbox pad re-blocked in HidHide ({btTree.Count} HID devnode(s)) — isolation verdict held from this session's observation"
                            : $"[Capture] BT Xbox pad blocked in HidHide ({btTree.Count} HID devnode(s)) — awaiting observed isolation");
                    }
                    if (!blocked)
                        Trace.WriteLine("[Capture] BT XInput cloak FAILED — staying in best-effort mode (no virtual pad)");
                    else if (_btObserved == BtObserved.Verified)
                        xboxCloaked = true;
                    // Not yet verified: the cross-branch guard below the branch chain owns the hold and
                    // the observation arm — it must run whichever branch cloaked (or would present) the pad.
                }
            }
            else
            {
                padGoneRetainCloak = true; // Keep exact owned IDs so a known transport can return cloaked.
                bool targetUp = _emulator.Active && _emulator.PadType == EmulatedPad.Xbox360;
                bool ownedVerified = _xboxCloakedIds.Count > 0 && _hidHide.VerifyCloak(_xboxCloakedIds.ToList());
                xboxCloaked = _captureContinuity.RetainNeutralXboxTarget(
                    _controllerConnected, _controllerPresentForPad, targetUp, ownedVerified);
                if (!xboxCloaked && _controllerConnected)
                {
                    // Connected, but its devnode hasn't enumerated yet. The stamp persists across passes
                    // so the hold is bounded from its first pass, not re-armed by each one.
                    inIdentityGap = true;
                    long now = Environment.TickCount64;
                    if (_identityHoldSinceMs == 0) _identityHoldSinceMs = now;
                    identityHold = _captureContinuity.HoldNeutralForIdentity(
                        _controllerConnected, targetUp, ownedVerified, now - _identityHoldSinceMs);
                    if (identityHold)
                    {
                        xboxCloaked = true;
                        ScheduleCaptureRecheck();
                    }
                }
                padDownByDesign = !xboxCloaked;
                // The hold gets its own throttle key: sharing the grace line's would swallow it whenever both
                // fire inside one throttle window, which is exactly when a transport switch produces them.
                if (identityHold)
                    TraceThrottled("xbox-identity-hold",
                        "[Capture] Xbox source connected before its devnode enumerated — holding the virtual pad neutral while identity resolves");
                else TraceThrottled("xbox-no-devnode", xboxCloaked
                    ? "[Capture] Xbox source absent — retaining neutral target during disconnect grace"
                    : "[Capture] XInput capture on, but no physical Xbox-class devnode found on any transport — best-effort mode");
            }

            // Cross-branch fail-closed guard: BT ids can sit cloaked-but-unverified while a DIFFERENT
            // branch grants xboxCloaked — on a USB→BT swap the wired devnodes are still enumerable, so
            // the wired branch runs and would present the pad over a cloak the probe never observed:
            // the gate's own branch is fail-closed, but nothing stops the others.
            // Whichever branch ran, an unverified cloaked BT id must hold the pad down — which is also
            // what guarantees the probe a clean window (_emulator.Active is what defeated it before).
            // Hard-bounded: a hold that cannot resolve by the deadline fails CLOSED into best-effort —
            // never a silently-unverified pad, and never a dead controller.
            if (btTree.Count > 0 && _btObserved is BtObserved.None or BtObserved.Pending
                && btTree.Any(id => _cloakedIds.Contains(id)))
            {
                if (_btObserveDeadlineMs == 0)
                {
                    _btObserveDeadlineMs = Environment.TickCount64 + BtObserveTimeoutMs;
                    // The deadline is only CHECKED inside a capture pass, and the hold stands the
                    // watchdog down (padDownByDesign) — so if the probe's own terminal paths never run
                    // one (e.g. a multi-pad interlude keeps a new probe from arming), nothing external
                    // is guaranteed to. This one-shot is the hold's own heartbeat: fire just past the
                    // deadline and let the pass above enforce it (a resolved hold makes it a no-op).
                    _btDeadlineTimer?.Stop();
                    _btDeadlineTimer = new DispatcherTimer
                        { Interval = TimeSpan.FromMilliseconds(BtObserveTimeoutMs + 500) };
                    _btDeadlineTimer.Tick += (_, _) => { _btDeadlineTimer?.Stop(); UpdateInputCapture(); };
                    _btDeadlineTimer.Start();
                }
                if (Environment.TickCount64 > _btObserveDeadlineMs)
                {
                    var lift = btTree.Where(id => _cloakedIds.Contains(id)).ToList();
                    _btObserved = BtObserved.Refused;
                    _btProbeGen++;   // a probe still in flight must not overwrite this Refused with a late Verified
                    Trace.WriteLine("[Capture] BT Xbox cloak was never OBSERVED before the deadline — un-cloaking, best-effort mode");
                    if (_hidHide.UnhideIds(lift))
                        foreach (var id in lift) { _cloakedIds.Remove(id); _xboxCloakedIds.Remove(id); }
                    padDownByDesign = true;
                }
                else
                {
                    xboxCloaked = false;    // never present a pad over an unverified BT cloak
                    btVerifyPending = true;
                    padDownByDesign = true;
                    // Pending with no probe in flight is stuck (a multi-pad interlude, a dropped
                    // lambda) — reset so a fresh observation can arm.
                    if (_btObserved == BtObserved.Pending && _btProbesInFlight == 0)
                        _btObserved = BtObserved.None;
                    // Arm only over a complete block — a partial set would just measure its own leak.
                    if (_btObserved == BtObserved.None && btTree.All(id => _cloakedIds.Contains(id)))
                        BeginBtIsolationObservation(btTree.ToList());
                }
            }
            else
            {
                _btObserveDeadlineMs = 0;   // nothing unverified is cloaked — next episode gets a fresh window
                _btDeadlineTimer?.Stop();
            }
        }
        // Kind-switch leak fix: once the active kind stops being Xbox (or the experimental flag is
        // switched off) any ids we cloaked for it are orphaned — nobody forwards its input anymore, so
        // left cloaked it's a dead physical pad. Un-cloak exactly those ids (never the whole _cloakedIds
        // set, which may simultaneously hold a legitimately-cloaked Sony pad) so it becomes an ordinary,
        // visible second controller again — the couch-multiplayer case.
        else if (_xboxCloakedIds.Count > 0) UnhideOrphanedXboxIds();

        if (!inIdentityGap) _identityHoldSinceMs = 0;   // left the gap: the next one gets a fresh bound

        // Sony (raw-HID) path: cloak FIRST, then present the pad — the pad must never come up while the
        // physical controller is visible to games, or they see two pads at once (double input — the exact
        // failure the stack exists to prevent). Outcome-tracked: _cloakedIds only gains ids Hide()
        // confirmed written, and every pass re-attempts any connected id not yet in the set (late connect /
        // reconnect / BT⇄USB transport change gets cloaked). A missing/blocked HidHide means NO virtual pad
        // and raw passthrough — the same graceful fallback as a missing ViGEmBus.
        bool sonyCloakOk = false;
        if (!xinputXbox && !SafeModeActive && !_safeModeOffPending && !busMissing)
        {
            // ⚠ "No devnode ids" is ambiguous: nothing plugged in, OR a CONNECTED pad whose devnodes
            // couldn't be resolved (enumeration threw / every candidate path failed to parse). Treating the
            // second as "no physical pad → nothing to double" fails open into exactly the double-input the
            // cloak-first ordering exists to prevent — so Unresolved must refuse the virtual pad.
            // TryGetHidInstanceIds separates the cases.
            switch (_controller.TryGetHidInstanceIds(out var ids))
            {
                case ControllerReader.PadIdentity.NoPhysicalPad:
                    sonyCloakOk = true;   // genuinely nothing connected → nothing to double
                    padGoneRetainCloak = true;
                    break;
                case ControllerReader.PadIdentity.Unresolved:
                    sonyCloakOk = false;  // pad open, devnodes unknown → refuse the pad, stay passthrough
                    padGoneRetainCloak = true;   // usually a disconnect in flight — protect the reconnect
                    break;
                default:
                    var missing = _hidHide.VerifyCloak(ids) ? ids.Where(id => !_cloakedIds.Contains(id)).ToList() : ids.ToList();
                    if (missing.Count == 0) sonyCloakOk = true;
                    else if (_hidHide.Hide(missing))   // contended-backoff lives inside Hide — see HidHideManager
                    {
                        foreach (var id in missing) _cloakedIds.Add(id);
                        sonyCloakOk = true;
                    }
                    // Roll back a partial driver write so no id leaks unowned — except on contention,
                    // where nothing was written and the rollback would just fail against the same holder.
                    else if (_hidHide.LastFailure != HidHideManager.CloakFailure.Contended)
                        _hidHide.Unhide();
                    break;
            }
            if (sonyCloakOk) _cloakFailTraced = false;
            else if (!_cloakFailTraced)
            {
                _cloakFailTraced = true;
                // Name the cause rather than listing the candidates: the old line offered three
                // possibilities on every failure, which reads as "we don't know" even when we do.
                Trace.WriteLine("[Capture] Sony cloak FAILED — no virtual pad, raw passthrough (cause: "
                    + _hidHide.LastFailure switch
                      {
                          HidHideManager.CloakFailure.Contended     => "HidHide's control device is held by another process",
                          HidHideManager.CloakFailure.DriverMissing => "HidHide is not installed/reachable",
                          HidHideManager.CloakFailure.Other         => "HidHide rejected the block-list write",
                          _                                          => "the pad's devnodes could not be resolved",
                      } + ")");
            }
        }

        // No virtual pad + no cloak when: a physical pad's cloak isn't CONFIRMED (Sony or XInput alike),
        // OR anticheat passthru mode (the user opted for a zero-kernel-footprint capture profile for
        // competitive AC titles). The wheel still reads input + draws the overlay in every case.
        // ⚠ _wheelsEnabled must NOT appear here — the chord is routing-only (see UpdateInputCapture
        // remarks). Wheels off keeps the pad up and forwarding; only Passthru Mode / exit release devices.
        // Also require a physical controller to be present (debounced — see _controllerPresentForPad):
        // a permanent pad with nothing plugged in has nothing to submit real input and just sits there
        // being polled by whatever else enumerates gamepads. The Sony/XInput
        // cloak branches above already no-op when GetHidInstanceIds()/GetPhysicalXboxInstanceIds() come
        // back empty, so this doesn't change cloak behavior — only whether the pad itself comes up.
        // A pending passthru-mode-off holds BOTH halves — the cloak branches above skip while pending, and
        // the pad is refused here — so the game keeps playing on the real controller for the whole hold.
        // Cloak + virtual pad land together when the hold completes (PollPendingSafeModeOff).
        bool wantPad = !SafeModeActive && !_safeModeOffPending && _controllerPresentForPad
            && (xinputXbox ? xboxCloaked : sonyCloakOk);
        // "Native" stand-in matches the PHYSICAL pad's family: a cloaked Xbox pad gets a virtual
        // Xbox 360 whatever the Xbox Mode toggle says (a virtual DS4 would flip its button prompts).
        // Extra-button pads are Xbox-layout hardware read over raw HID, so they get the same stand-in.
        // _ds4RefusedByBus: a foreign ViGEmBus (a 2018 fork) that would not create the DS4 target — the X360
        // stand-in is what it is known to do, so a Sony pad falls back to it for the session (Xbox prompts in
        // games rather than no pad at all).
        var  wantType = (_userEmulation || xinputXbox || captureKind == ControllerKind.ExtraButtonPad
                         || _ds4RefusedByBus)
            ? EmulatedPad.Xbox360 : EmulatedPad.DualShock4;

        // Present the virtual pad (XBox-360, or native DualShock 4) while enabled: bring it up or swap type.
        if (wantPad && (!_emulator.Active || _emulator.PadType != wantType))
        {
            try
            {
                _emulator.Start(wantType);                              // connect, or swap the pad type
                // Hook Submit exactly once — "was the emulator active" is NOT "already subscribed"
                // (a failed revive/type-swap drops Active with the handler still attached, so gating on
                // it stacked a duplicate handler per failure episode).
                if (!_submitHooked) { _controller.StateChanged += _emulator.Submit; _submitHooked = true; }
                _controller.ReplayLatestState(_emulator.Submit);
                if (!_rumbleHooked) { _emulator.RumbleReceived += (l, r) => _controller.SetRumble(l, r); _rumbleHooked = true; }
                _emuStartRetries = 0;
                Trace.WriteLine($"[Capture] virtual pad up ({wantType})");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Capture] emulator start failed: {ex.Message}");
                // A start that fails because the BUS ISN'T THERE latches, so the next pass skips the cloak
                // instead of repeating the cloak → failed start → un-cloak cycle. Asked of the driver, not
                // inferred from the exception type: a foreign ViGEmBus fork throws its own types, and the
                // question here is only "is a bus reachable at all".
                if (!_emulator.ProbeDriverInstalled() && !_busMissing)
                {
                    _busMissing = true;
                    Trace.WriteLine("[Capture] ViGEmBus is MISSING — holding the cloak off until it returns "
                                    + "(no virtual pad; the isolation card names the repair)");
                }
                // A DS4 target refused by a foreign bus is a capability gap, not a transient: switch the
                // stand-in type once and let the retry below bring up the X360 pad.
                if (wantType == EmulatedPad.DualShock4 && !_ds4RefusedByBus
                    && DriverStatus.ForeignViGEmBusName() is { } fork)
                {
                    _ds4RefusedByBus = true;
                    Trace.WriteLine($"[Capture] DS4 stand-in refused by {fork}'s ViGEmBus — using an Xbox 360 stand-in for this session (games show Xbox prompts)");
                }
                // Retry a transient ViGEm connect failure (e.g. the force-kill bus-cleanup race at launch)
                // on a short timer — waiting for the NEXT capture call could mean a wheel open minutes
                // later, where the pad + cloak appearing mid-game yanks the game's controller. Bounded:
                // a missing ViGEmBus driver mustn't churn forever.
                ScheduleEmulatorRetry();
            }
        }
        else if (!wantPad && _emulator.Active)
        {
            _emulator.Stop();   // release the virtual pad → the real controller is back, fully featured
            // Name the conjunct of wantPad that actually failed. The old line read "wheels disabled" for
            // every non-Xbox release, so a cloak failure, passthru mode and an absent pad all logged as a
            // deliberate user toggle — the one line that should explain an unexpected pad removal instead
            // misattributed it. Order matches wantPad's own evaluation order.
            Trace.WriteLine(xinputXbox
                ? "[Capture] XInput Xbox pad — virtual pad released; game reads the physical directly (best-effort, no cloak)"
                : SafeModeActive            ? "[Capture] passthru mode — virtual pad released, controller passed through"
                : _safeModeOffPending       ? "[Capture] passthru mode off pending — cloak and virtual pad both held until the game is left"
                : !_controllerPresentForPad ? "[Capture] no physical controller — virtual pad released"
                : "[Capture] cloak not confirmed — virtual pad released to avoid double input, controller passed through");
        }

        // Neutralise the pad while a wheel is up so the game gets nothing, and while a hold keeps it up for a
        // source nothing has verified yet (an identity hold, or a multi-pad hold whose source reconnected).
        _emulator.Suppressed = _emulator.Active
            && (overlay || identityHold || multiPadHold == ControllerCaptureContinuity.MultiPadHold.Neutral);

        // Un-cloak failsafe: if no virtual pad is standing in — passthru mode, cloak not wanted, OR the
        // emulator failed to start / died (ViGEm down) — the physical pad must be usable, so lift what WE
        // hid. Unhide() with no args removes only Radiata-owned ids; it never strips a block another tool
        // placed. The retry timer / watchdog re-runs capture, so a recovered emulator re-cloaks on the
        // next pass.
        // ⚠ EXCEPT while the pad is absent/unresolved (padGoneRetainCloak): there is no device to hand
        // back, and lifting the block-list is what loses the reconnect race to Steam (see the flag's
        // declaration). Passthru Mode overrides retention — it is the explicit "make the pad fully normal"
        // release and must work with the pad disconnected too.
        if ((!wantPad || !_emulator.Active) && _cloakedIds.Count > 0)
        {
            if ((padGoneRetainCloak || btVerifyPending) && !SafeModeActive && !_safeModeOffPending)
            {
                TraceThrottled("cloak-retained",
                               "[Capture] pad absent/unresolved — cloak entries retained so the reconnect "
                               + "is born cloaked (no device-arrival race)");
            }
            else if (_hidHide.Unhide())
            {
                _cloakedIds.Clear();
                // Keep the Xbox-owned subset in step with the set it's a subset OF. This is the path a
                // second pad arriving mid-session takes (the multi-pad guard above refuses to cloak, so
                // wantPad drops and the first pad's cloak is lifted here) — leaving stale ids behind would
                // have the kind-switch branch later "un-cloak" devnodes nobody is hiding.
                _xboxCloakedIds.Clear();
            }
        }

        _padDownByDesign = padDownByDesign;   // the watchdog reads this on its next tick

        // Tell the tray what we ended up with. "Isolated" means both halves are in place: the physical pad is
        // cloaked AND a virtual pad is standing in for it.
        NoteIsolationState(
            isolated: wantPad && _emulator.Active,
            note: SafeModeActive           ? UiText.IsolationNote.PassthruNoCloak
                : _safeModeOffPending       ? UiText.IsolationNote.PassthruEnding
                : !_controllerPresentForPad ? ""
                  // Tested ABOVE both cloak-failure branches, because skipping the cloak is what this state
                  // DOES: read in their order the note would degrade to the generic Xbox-fallback / cloak-
                  // failed wording, and the card that names the real cure ("the ViGEmBus driver is missing —
                  // reinstall from Settings ▸ Advanced ▸ Install/Repair Drivers") would be lost exactly
                  // when it is most needed.
                : busMissing                ? UiText.IsolationNote.NoVirtualPad
                : xinputXbox && padCountUnstable ? UiText.IsolationNote.PadCountUnstable
                : xinputXbox && multiPadGuard ? UiText.IsolationNote.MultiplePads
                : xinputXbox && !xboxCloaked ? UiText.IsolationNote.XboxFallback
                  // Contention is called out separately from a generic cloak failure all the way to the
                  // tray: the cure is "close the other app", and the generic wording sends people to
                  // driver repair, which can't fix a working driver someone else is holding.
                : !xinputXbox && !sonyCloakOk ? (_hidHide.LastFailure == HidHideManager.CloakFailure.Contended
                                                 ? UiText.IsolationNote.HidHideBusy
                                                 : UiText.IsolationNote.CloakFailed)
                : !_emulator.Active          ? UiText.IsolationNote.NoVirtualPad
                : "");

        // The one leak every check above calls "isolated": Steam's pre-cloak handle (the cloak blocks
        // future opens only). Evaluated after the state settles so the sentry sees the same truth the
        // tray does.
        EvaluateSteamSentry(isolated: wantPad && _emulator.Active);
    }

    private void UnhideOrphanedXboxIds()
    {
        if (_hidHide.UnhideIds(_xboxCloakedIds.ToList()))
        {
            foreach (var id in _xboxCloakedIds) _cloakedIds.Remove(id);
            _xboxCloakedIds.Clear();
        }
        Trace.WriteLine("[Capture] Xbox pad un-cloaked — no longer the active controller (visible as an ordinary pad)");
    }

    // ── Pad-count churn breaker ──────────────────────────────────────────────────────────────────────
    // Capture's own action (plugging the virtual pad) changes the input to its own decision (the physical
    // pad count) whenever a software bus is not recognised as one: the stand-in is counted as a second
    // physical pad, the multi-pad guard releases it, the count drops, capture re-cloaks and re-plugs — a
    // 3 s oscillation that reaches the user as Windows connect/disconnect toasts. PnpAncestry is the
    // classifier; this is the damping. PadCountChurnRule (Core) holds the rule: the guard tripping
    // ChurnTrips times inside ChurnWindowMs latches best-effort for ChurnHoldMs; a second latch in the
    // session holds until every Xbox-class pad is gone, Passthru toggles or the app restarts. This block
    // adds the trace lines, the transient-id naming and the timer that re-runs capture at a hold's expiry.
    private bool _ds4RefusedByBus;   // see wantType in UpdateInputCaptureCore

    private readonly PadCountChurnRule _padCountRule = new();
    private IReadOnlyList<string> _lastSinglePadIds = [];
    private DispatcherTimer? _padCountHoldTimer;

    /// <summary>Feed one capture pass's pad count to the breaker; true while the hold is in force.</summary>
    private bool PadCountChurnBreaker(bool guardOn, IReadOnlyList<string> tree, IReadOnlyList<string> btTree)
    {
        var verdict = _padCountRule.Observe(Environment.TickCount64, guardOn, tree.Count + btTree.Count);
        if (verdict.HasFlag(PadCountChurnRule.Verdict.Released))
        {
            Trace.WriteLine("[Capture] pad-count hold released — no Xbox-class pad present");
            _padCountHoldTimer?.Stop();
        }
        if (verdict.HasFlag(PadCountChurnRule.Verdict.Expired))
            Trace.WriteLine("[Capture] pad-count hold expired — retrying capture once");
        if (verdict.HasFlag(PadCountChurnRule.Verdict.Latched))
        {
            bool session = verdict.HasFlag(PadCountChurnRule.Verdict.ForSession);
            var transient = tree.Concat(btTree)
                                .Where(id => !_lastSinglePadIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                                .ToList();
            Trace.WriteLine($"[Capture] pad count flapped {PadCountChurnRule.ChurnTrips}× in {PadCountChurnRule.ChurnWindowMs / 1000} s — holding best-effort "
                            + (session ? "for the session (second latch)" : $"{PadCountChurnRule.ChurnHoldMs / 1000} s")
                            + $"; the transient pad(s): {(transient.Count > 0 ? string.Join(", ", transient) : "?")}"
                            + " — if one is a 045E:028E node under a software bus, it is our own virtual pad "
                            + "misclassified: see the [XboxTree] parent lines and docs/INPUT-CAPTURE.md");
            if (!session)
            {
                // The hold stands the watchdog down, so nothing external re-runs capture at expiry.
                _padCountHoldTimer?.Stop();
                _padCountHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PadCountChurnRule.ChurnHoldMs + 500) };
                _padCountHoldTimer.Tick += (_, _) => { _padCountHoldTimer?.Stop(); UpdateInputCapture(); };
                _padCountHoldTimer.Start();
            }
        }
        bool held = verdict.HasFlag(PadCountChurnRule.Verdict.Held);
        if (!guardOn && !held) _lastSinglePadIds = tree.Concat(btTree).ToList();
        return held;
    }

    /// <summary>Forget the breaker's history: a release, or Passthru Mode changing (the user chose a
    /// different capture profile — the next episode starts clean).</summary>
    private void ResetPadCountBreaker()
    {
        _padCountRule.Reset();
        _padCountHoldTimer?.Stop();
    }

    /// <summary>One pending retry of the virtual-pad start, ~2s out, at most 5 per failure episode
    /// (a successful start resets the budget; a missing ViGEmBus driver stops retrying).</summary>
    private void ScheduleEmulatorRetry()
    {
        if (SafeModeActive || _emulator.Active || _emuRetryTimer is not null) return;
        if (_emuStartRetries >= 5)
        {
            // Terminal for the session: the individual failures each logged, but giving up did not — and
            // from here on there is NO virtual pad, which is the difference between "isolated" and the game
            // reading the physical pad directly.
            TraceThrottled("emu-retries-exhausted",
                           "[Emu] virtual-pad start retries EXHAUSTED (5) — no virtual pad for this session; " +
                           "the un-cloak failsafe should hand the physical pad back to the game");
            return;
        }
        _emuStartRetries++;
        _emuRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _emuRetryTimer.Tick += (_, _) =>
        {
            _emuRetryTimer?.Stop();
            _emuRetryTimer = null;
            UpdateInputCapture();
        };
        _emuRetryTimer.Start();
    }

    // ── BT Xbox cloak: observed-isolation gate ────────────────────────────────────────────────────
    // The virtual pad may stand in for a Bluetooth Xbox pad only after a NON-allow-listed process has
    // been OBSERVED to lose the pad (XboxBtIsolationProbe). Keyed to the exact cloaked id set: a
    // replug/re-pair with different ids re-verifies. Verified persists for the session per id set;
    // Refused does too (covers both "observed still visible" and "couldn't observe" — fail closed).
    // While unverified (None/Pending) the cross-branch guard in UpdateInputCaptureCore withholds the
    // virtual pad — no branch may present one over an unverified BT cloak — and bounds the hold with
    // BtObserveTimeoutMs, whose expiry is Refused (un-cloak into best-effort, never a dead pad).
    private enum BtObserved { None, Pending, Verified, Refused }
    private BtObserved _btObserved;
    private string? _btCloakKey;
    private int  _btProbeGen;          // bumped per arm — a superseded probe must not write state
    private int  _btProbesInFlight;    // stuck-Pending detection: Pending with none in flight re-arms
    private long _btObserveDeadlineMs; // hard bound on the unverified hold — expiry fails CLOSED
    private DispatcherTimer? _btDeadlineTimer;   // the hold's heartbeat — see the arm site in the guard

    // Identity hold (the no-devnode branch) and multi-pad hold: when each began, 0 = not in that state. The
    // shared heartbeat re-runs capture so both end by their bound even when no device broadcast arrives;
    // 300 ms outlasts XboxDeviceTree's 250 ms sweep memo, so each re-run sees a fresh sweep.
    private long _identityHoldSinceMs;
    private long _multiPadSinceMs;
    private int  _multiPadConnectEpoch;   // _connectEpoch when the second pad appeared
    private int  _connectEpoch;           // bumped on every reader connect: tells a held source from a replacement
    private DispatcherTimer? _captureRecheckTimer;

    private void ScheduleCaptureRecheck()
    {
        if (_captureRecheckTimer is null)
        {
            _captureRecheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _captureRecheckTimer.Tick += (_, _) => { _captureRecheckTimer?.Stop(); UpdateInputCapture(); };
        }
        _captureRecheckTimer.Stop();
        _captureRecheckTimer.Start();
    }
    private const int BtObserveTimeoutMs = 15_000;

    /// <summary>Kick off (at most one in-flight) observation for the just-cloaked BT id set. Posted to
    /// the dispatcher so it runs AFTER the capture pass that queued it; that pass has already withheld
    /// the virtual pad (the cross-branch unverified guard), so the probe's count can't be polluted by
    /// our own pad. On completion, re-runs capture: Verified brings the pad up; anything else un-cloaks
    /// and settles into best-effort (a blocked pad with no stand-in is a dead controller).</summary>
    private void BeginBtIsolationObservation(List<string> btIds)
    {
        if (_btObserved != BtObserved.None) return;
        _btObserved = BtObserved.Pending;
        int gen = ++_btProbeGen;
        string keyAtLaunch = _btCloakKey!;
        _btProbesInFlight++;
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (_emulator.Active)
                {
                    // Should be unreachable now that the guard withholds the pad while unverified. If
                    // something still raced one up, don't probe a polluted count — reset and re-run
                    // capture, whose guard stops the pad and re-arms. The deadline bounds this loop
                    // (the old silent reset-and-return here is what let the gate ping-pong forever).
                    if (gen == _btProbeGen && _btObserved == BtObserved.Pending) _btObserved = BtObserved.None;
                    Trace.WriteLine("[Capture] BT isolation probe deferred — a virtual pad is up; re-running capture to clear the window");
                    UpdateInputCapture();
                    return;
                }
                bool presentAtStart = await Task.Run(() => XboxDeviceTree.AllPresent(btIds));
                int? count = await XboxBtIsolationProbe.CountPadsAnotherProcessSeesAsync();
                if (gen != _btProbeGen || _btCloakKey != keyAtLaunch)
                {
                    // Superseded by a re-arm, or the id set changed mid-probe: this result describes a
                    // cloak that no longer exists. The newest probe owns the state.
                    if (gen == _btProbeGen && _btObserved == BtObserved.Pending) _btObserved = BtObserved.None;
                    UpdateInputCapture();
                    return;
                }
                if (count is > 0)
                {
                    // ONE re-observation before refusing: the probe takes the max of 4 samples over
                    // ~600ms, and startup's un-cloak/re-cloak flicker can expose
                    // the pad for one of them — a single unlucky sample then cost a whole session's
                    // isolation (Refused is session-sticky per id set). A genuinely leaking cloak still
                    // refuses, one second later. EXACTLY one retry — more would let a flapping cloak
                    // livelock toward trust — and probe-unavailable (null) never retries: no second
                    // attempt can conjure a missing helper.
                    Trace.WriteLine($"[Capture] BT Xbox cloak observed {count} pad(s) — re-observing once after a 1s settle (startup churn can poison one sample)");
                    await Task.Delay(1000);
                    if (gen != _btProbeGen || _btCloakKey != keyAtLaunch)
                    {
                        if (gen == _btProbeGen && _btObserved == BtObserved.Pending) _btObserved = BtObserved.None;
                        UpdateInputCapture();
                        return;
                    }
                    count = await XboxBtIsolationProbe.CountPadsAnotherProcessSeesAsync();
                    if (gen != _btProbeGen || _btCloakKey != keyAtLaunch)
                    {
                        if (gen == _btProbeGen && _btObserved == BtObserved.Pending) _btObserved = BtObserved.None;
                        UpdateInputCapture();
                        return;
                    }
                }
                bool presentAtEnd = await Task.Run(() => XboxDeviceTree.AllPresent(btIds));
                var outcome = BtObservationGate.Resolve(
                    superseded:           gen != _btProbeGen,
                    stillPending:         _btObserved == BtObserved.Pending,
                    idSetChanged:         _btCloakKey != keyAtLaunch,
                    padPresentThroughout: presentAtStart && presentAtEnd,
                    idsStillCloaked:      btIds.All(_cloakedIds.Contains),
                    count:                count);
                if (outcome == BtObservationGate.Outcome.Discard)
                {
                    Trace.WriteLine("[Capture] BT isolation observation discarded — the deadline or a newer observation owns the verdict");
                }
                else if (outcome == BtObservationGate.Outcome.Reset)
                {
                    _btObserved = BtObserved.None;
                    Trace.WriteLine($"[Capture] BT isolation observation not counted — pad present at start={presentAtStart} end={presentAtEnd}, "
                                    + "ids still cloaked=" + btIds.All(_cloakedIds.Contains)
                                    + " (a count from an absent pad proves nothing); re-observing when it is back");
                }
                else if (outcome == BtObservationGate.Outcome.Verified)
                {
                    _btObserved = BtObserved.Verified;
                    Trace.WriteLine("[Capture] BT Xbox cloak verified for fresh probe (0 pads); existing application handles remain unverified; presenting the virtual pad");
                }
                else
                {
                    _btObserved = BtObserved.Refused;
                    Trace.WriteLine(count is null
                        ? "[Capture] BT Xbox cloak could not be OBSERVED (probe unavailable) — un-cloaking, best-effort mode"
                        : $"[Capture] BT Xbox cloak observed INEFFECTIVE — a non-allow-listed process still sees {count} pad(s) after the settle-and-retry; un-cloaking, best-effort mode");
                    if (_hidHide.UnhideIds(btIds))
                        foreach (var id in btIds) { _cloakedIds.Remove(id); _xboxCloakedIds.Remove(id); }
                }
                UpdateInputCapture();
            }
            catch (Exception ex)
            {
                // An unobserved exception here must not strand Pending forever — the pad is gated
                // on the verdict, so that would be a dead controller. None lets the guard re-arm; the
                // deadline turns persistent failure into Refused (fail closed).
                Trace.WriteLine($"[Capture] BT isolation observation failed: {ex.Message}");
                if (gen == _btProbeGen && _btObserved == BtObserved.Pending) _btObserved = BtObserved.None;
                UpdateInputCapture();
            }
            finally { _btProbesInFlight--; }
        });
    }

    /// <summary>Slow always-on safety watchdog for a dead virtual pad. A failed emulator revive (or an
    /// exhausted retry budget) leaves Active=false with no event back into capture, and UpdateInputCapture
    /// otherwise runs only on user gestures / config reloads / connect events — so the game could sit with
    /// the physical pad cloaked and NO pad at all until the next wheel open. This re-runs capture within
    /// ~10s instead: either the pad restarts, or (ViGEm still down) the un-cloak failsafe returns the
    /// physical pad to the game.</summary>
    private void StartEmulatorWatchdog()
    {
        _emuWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        // Only revive a pad that SHOULD be up — the pad is expected only when a controller is present,
        // outside Passthru Mode, with no pending passthru-mode-off hold, and when the last capture pass didn't
        // leave it down as a decision (multi-pad guard, XInputCapture off, BT best-effort). Treating any
        // of those as an outage re-ran capture every 10s indefinitely AND held the one-shot episode
        // latch open, so a REAL ViGEm failure starting mid-episode announced nothing.
        // The wheels flag deliberately does NOT gate this: the chord is routing-only, so the pad should
        // be up whenever a controller is present outside Passthru Mode.
        // ⚠ The 10s disconnect grace means "controller absent" and "pad torn down" are not simultaneous;
        // during the grace the emulator is still Active, so no false episode can open there either.
        _emuWatchdog.Tick += (_, _) =>
        {
            if (_overlayVisible || _browserOpen || _arcadeOpen) RearmMouseWatch();
            bool expected = !SafeModeActive && !_safeModeOffPending
                            && _controllerPresentForPad && !_padDownByDesign;
            if (_emulator.Active || !expected)
            {
                // Close the episode loudly: without this line, an outage's END is invisible and its
                // duration unrecoverable from the log. "Outage over" only when the pad actually came
                // back — standing down because the pad left (or a designed release began) must not
                // claim a recovery that never happened.
                if (_emuWatchdogDown)
                {
                    _emuWatchdogDown = false;
                    Trace.WriteLine(_emulator.Active
                        ? "[Emu] watchdog: virtual pad is back — outage over"
                        : "[Emu] watchdog: standing down — no virtual pad expected in the current state");
                }
                // A pad that came back through ANOTHER path (the device-arrival capture pass usually wins
                // the race with this 10s timer) must clear the driver-missing latch here too, or a later
                // outage in the same session stands down silently.
                _emuWatchdogDriverMissing = false;
                return;
            }
            // A MISSING bus is not an outage this loop can heal: with the start budget spent, every tick
            // would cloak the pad, fail the start and un-cloak again — every 10 s for the whole session,
            // blinking the controller out of the game each time. Stand down until the driver exists; the
            // probe each tick means a later install heals without a restart. Checked BEFORE the episode
            // latch so this state neither opens an outage episode nor beats "capture retries continue",
            // which would be a lie while we are deliberately not retrying.
            if (_emuStartRetries >= 5 && !_emulator.ProbeDriverInstalled())
            {
                if (!_emuWatchdogDriverMissing)
                {
                    _emuWatchdogDriverMissing = true;
                    _emuWatchdogDown = false;   // close any episode this state opened on an earlier tick
                    Trace.WriteLine("[Emu] watchdog: ViGEmBus driver missing — standing down until it is installed "
                                    + "(Settings ▸ Advanced ▸ Install/Repair Drivers); the physical pad stays with the game");
                }
                return;
            }
            if (_emuWatchdogDriverMissing)
            {
                _emuWatchdogDriverMissing = false;
                _emuStartRetries = 0;   // a fresh budget for the driver that just appeared
                Trace.WriteLine("[Emu] watchdog: ViGEmBus driver present again — resuming");
            }
            // One line per EPISODE, not per tick: a known-stuck outage (e.g. HidHide's control device held
            // by another app) re-runs capture every 10s for as long as it lasts, and a per-tick line would
            // be most of the log through exactly the stretch someone needs to read. A 5-min heartbeat keeps
            // a long outage visible in a tail without burying it.
            if (!_emuWatchdogDown)
            {
                _emuWatchdogDown = true;
                _emuWatchdogBeat = Environment.TickCount64;   // first heartbeat 5 min after the START
                Trace.WriteLine("[Emu] watchdog found no virtual pad while one should be up — re-running " +
                                "capture every 10s until it returns (further lines suppressed; 5-min heartbeat)");
            }
            else
            {
                long now = Environment.TickCount64;
                if (now - _emuWatchdogBeat >= 300_000)
                {
                    _emuWatchdogBeat = now;
                    Trace.WriteLine("[Emu] watchdog: still no virtual pad — capture retries continue");
                }
            }
            UpdateInputCapture();
        };
        _emuWatchdog.Start();
    }

    /// <summary>Cancel: release input and snap the wheel away immediately (no fade).</summary>
    private void DismissOverlay()
    {
        SoftReleaseInput();
        _hubStickyNotice = null;   // the wheel is gone; the next open re-evaluates the capture state fresh
        _overlay?.HideMixIndicator();   // defensive: never let a mixer readout outlive its wheel
        _overlay?.Reset();
        _overlay?.HideWheel();
        _announcer.Reset();   // the context is gone — no queued selection may speak after this
    }

    /// <summary>The controller dropped out (e.g. a dead battery mid-wheel). Tear every input surface down
    /// to a clean, neutral state so a disconnect can never soft-lock: clear the held-Fn flags, close the
    /// wheel / Game Grid / edit / assign, recompute capture so the virtual pad is un-suppressed, then force
    /// the virtual pad neutral so it isn't frozen on the last pre-death report (which Windows would read as
    /// a held stick/d-pad). Safe to call when nothing is open. On reconnect the reader re-baselines and
    /// per-report stick/state events resume, so everything self-heals.</summary>
    private void OnControllerLost()
    {
        // Brackets the teardown: [Controller] disconnected covers the pad DROP, but this same full teardown
        // is also what "Recover Controller" runs, and that path needs its own log line.
        Trace.WriteLine($"[Controller] input teardown — closing every surface " +
                        $"(wheel={_overlayVisible} edit={Editing} grid={_browserOpen} arcade={_arcadeOpen}) " +
                        "and neutralising the virtual pad");
        if (Editing && _overlay is not null)
            _pendingWheelEdits[_activeWheel] = _overlay.EditCurrentSlices.ToArray();
        _editPickGame  = false;   // don't bounce back into edit — fully tear down
        _silentWheel   = null;
        _fnLeftDown    = _fnRightDown = false;
        StopDpadVolumeRepeat();                 // a drop mid-hold sends no D-pad release — stop the repeat
        StopDpadMixRepeat();
        StopDpadMicRepeat();
        ReleaseTaskSwitcher();                  // never leave Alt stranded down
        _overlay?.HideMixIndicator();
        _triggerInterpreter?.ResetGesture();    // no releases will arrive for latched chord state
        _overlay?.ResetWheelLayer();
        if (_browserOpen) CloseGameBrowser();
        // The arcade must not survive a disconnect: with no pad there's nothing to play with, and leaving the
        // disc up would strand input behind a surface that swallows ○ (the disconnect invariant — every
        // modality resets, or the UI soft-locks). Closing also persists the snapshot, so a dead battery
        // mid-wave costs nothing.
        if (_arcadeOpen) CloseArcade();
        if (_overlayVisible || Editing) DismissOverlay();
        UpdateInputCapture();     // overlay closed → Suppressed=false, so the pad still works after reconnect
        _emulator.Neutralize();   // ...and isn't left holding the pre-death input in the meantime
    }

    /// <summary>Advanced-tab "Recover Controller": user-driven recovery from a wedged controller (frozen/stuck reports
    /// that neither the disconnect path nor the stall watchdog can detect). Tears every overlay/assign
    /// surface down + neutralises the virtual pad, then forces the HID stream to drop and reopen so the
    /// reader re-baselines.</summary>
    private void ResetInput()
    {
        Trace.WriteLine("[Controller] Recover Controller invoked (user-driven) — tearing down and forcing a HID reconnect");
        OnControllerLost();
        _controller.ForceReconnect();
        // The soft reset cannot cure a pad whose input is wedged at the device (frozen packet counter, see
        // docs/INPUT-CAPTURE.md); the card names the cure that does, so a silent pad after this press is not a dead end.
        ShowCornerToast(Loc.T(UiText.Toasts.RecoverTitle), Loc.T(UiText.Toasts.RecoverBody), holdMs: 12000);
    }

    private void ToggleOverlay(ActiveWheel wheel)
    {
        // Another surface owns the wheel/input: in edit, _overlayVisible is still true and CloseOverlay
        // would FIRE the edit-selected index against the stale pre-edit slices — save + exit instead.
        // Over the grid / a Replace wheel, opening would paint a second wheel under the grid card and
        // cross their input routing — ignore the toggle there.
        if (Editing) { ExitEdit(); return; }
        if (_browserOpen) { TraceSurfaceSwallow("the Game Grid", "Test Wheel / hotkey summon"); return; }
        if (_arcadeOpen) { TraceSurfaceSwallow("the Arcade", "Test Wheel / hotkey summon"); return; }
        if (_overlayVisible) CloseOverlay();
        else
        {
            // No physical Fn button here (hotkey/tray test) — use the wheel default.
            _aimWithRightStick = wheel == ActiveWheel.A;
            ShowOverlay(wheel);
        }
    }

    // ── Action execution ──────────────────────────────────────────────────────

    /// <summary>Show the armed slice's current toggle state in the hub (or clear it for
    /// non-toggle slices / nothing armed), so you can see the state before firing.</summary>
    private void UpdateArmedPreview(int armed)
    {
        // Onboarding practice: the hub says "Practice" the WHOLE time a wheel is up (not just after a
        // fire), so nothing on the practice wheel ever reads as a live action. Suppresses the armed
        // toggle-state readouts too — their real states aren't being toggled in preview anyway.
        if (PracticeMode) { _overlay?.ShowHubNotice(PracticeNoticeToken, RadialMenuControl.NoticeKind.Practice); AnnounceArmed(armed, state: null); return; }
        if (armed < 0 || _activeSlices is null || (uint)armed >= (uint)_activeSlices.Length)
        {
            // Disarm restores the wheel's standing hub notice (leak warning / passthru-ending chip) instead of
            // clearing to an empty hub — the state it names is still true, and a warning that vanishes on
            // the first arm defeats its purpose. Set at wheel open, nulled on dismiss/close.
            if (_hubStickyNotice is { } sticky) _overlay?.ShowHubNotice(sticky);
            else _overlay?.ClearPreview();
            return;
        }
        var slice = _activeSlices[armed];
        if (ActionExecutor.NeedsConfiguration(slice) || NeedsDiscordIntegration(slice))
        { _overlay?.ShowConfigureNotice(); AnnounceArmed(armed, Loc.T(UiText.Narration.NeedsSetup)); return; }
        var preview = _executor.PreviewStatus(slice);
        if (preview is null) _overlay?.ClearPreview();
        else                 _overlay?.ShowPreview(Loc.Readout(preview.Line1), Loc.Readout(preview.Line2), preview.Line2Next is { } next ? Loc.Readout(next) : null);
        // Narration: the slice's resolved Label — never the visible label (logo slices suppress that but
        // always carry the name), matching what editing and launching use.
        AnnounceArmed(armed, preview?.Line2);

        // OBS stream/record toggles (G13.B): the state lives in OBS, so it's fetched async (a one-shot
        // websocket query, ~100 ms local) and shown as the usual "current › next" readout if this
        // slice is STILL the armed one when the answer lands.
        if (slice.Action?.Type == "obs"
            && slice.Action.Command?.ToLowerInvariant() is "obs-toggle-stream" or "obs-toggle-record")
            _ = ShowObsTogglePreviewAsync(slice, armed);
    }

    /// <summary>Narrate the armed slice: resolved label, position, then the most useful third part —
    /// "hold to confirm" for a guarded slice, otherwise the toggle's current state when it has one.
    /// Selection-kind, so rapid stick sweeps coalesce to the slice they settle on.</summary>
    private void AnnounceArmed(int armed, string? state)
    {
        if (!_announcer.Enabled) return;
        var slices = _activeSlices;
        if (slices is null || (uint)armed >= (uint)slices.Length) return;
        string third = _overlay?.ArmedRequiresConfirm == true ? Loc.T(UiText.Narration.HoldToConfirm)
                     : !string.IsNullOrWhiteSpace(state) ? StateClause(slices[armed], state!)
                     : "";
        _announcer.Announce(
            Loc.F(UiText.Narration.SliceOf, Loc.DefaultLabel(slices[armed].Label), armed + 1, slices.Length) + (third.Length > 0 ? ", " + third : ""),
            AnnouncementKind.Selection);
    }

    /// <summary>Lower-case the first letter so a state word reads as part of the sentence ("Currently off"),
    /// but leave an ACRONYM alone — a second capital means the word isn't sentence-cased to begin with, and
    /// "oBS unavailable" is what naive lowering produces.</summary>
    private static string LowerFirst(string s) =>
        Loc.Lang != HelpLocalization.DefaultCode ? s :   // sentence-casing is an English convention (German nouns keep their capital; ja/ar have none)
        s.Length >= 2 && char.IsUpper(s[1]) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    /// <summary>Turn an armed slice's <c>PreviewStatus</c> state word into a clause that makes sense after
    /// the slice's name. <c>ActionExecutor</c>'s state vocabulary is closed but mixes THREE different ideas,
    /// and only the first is a state "Currently" can introduce:
    /// <list type="number">
    ///   <item><b>A toggle state</b> — On/Off/Muted/Unmuted/Focus Assist's profiles. "Currently off".</item>
    ///   <item><b>A no-function condition</b> — the feature is missing, not in a state. "Currently No
    ///   Device" / "Currently OBS unavailable" is nonsense, so these speak bare.</item>
    ///   <item><b>An object name</b> — <c>exit-app</c>'s state is the app that WOULD be closed, so it needs
    ///   a verb ("will close Notepad"), not a state clause.</item>
    /// </list>
    /// A word this doesn't recognise falls through to bare, which is the safe direction: an unlabelled
    /// condition is merely terse, while a wrong "Currently" is actively misleading.</summary>
    private static string StateClause(WheelSlice slice, string state)
    {
        if (string.Equals(slice.Action?.Type, "exit-app", StringComparison.OrdinalIgnoreCase))
            return state.Equals(UiText.Status.NothingToExit, StringComparison.OrdinalIgnoreCase)
                ? Loc.T(UiText.Narration.NothingToClose) : Loc.F(UiText.Narration.WillClose, state);

        return state switch
        {
            UiText.Status.On or UiText.Status.Off or UiText.Status.Muted or UiText.Status.Unmuted or "Priority Only" or "Alarms Only"
                            => Loc.F(UiText.Narration.Currently, LowerFirst(Loc.Readout(state))),
            UiText.Status.NoDevice     => Loc.T(UiText.Narration.NoDeviceAvailable),
            UiText.Status.Unavailable  => Loc.T(UiText.Narration.Unavailable),
            UiText.Status.NotInstalled => Loc.T(UiText.Narration.NotInstalled),
            UiText.Status.Failed       => Loc.T(UiText.Narration.Failed),
            _                          => LowerFirst(Loc.Readout(state)),   // e.g. "OBS unavailable" — already a phrase
        };
    }

    private async System.Threading.Tasks.Task ShowObsTogglePreviewAsync(WheelSlice slice, int armedToken)
    {
        try
        {
            bool? on = await _obs.QueryToggleStateAsync(slice.Action!).ConfigureAwait(true);
            if (on is null || _previewArmed != armedToken || !_overlayVisible) return;
            _overlay?.ShowPreview(Loc.DefaultLabel(slice.Label), Loc.T(on.Value ? UiText.Status.On : UiText.Status.Off), Loc.T(on.Value ? UiText.Status.Off : UiText.Status.On));
            // The OBS state arrives ~100 ms after arming, so the first announcement had no state to carry.
            AnnounceArmed(armedToken, on.Value ? UiText.Status.On : UiText.Status.Off);
        }
        catch { /* preview only — never let a query failure surface */ }
    }

    private void FireAction(WheelSlice slice)
    {
        Trace.WriteLine($"[FIRE] {slice.Label}  type={slice.Action?.Type}");
        // Alt-Tab d-pad mode: the switcher's Alt is held for the life of the wheel, and the release
        // normally rides SoftReleaseInput — which runs AFTER Execute below. Firing a slice with Alt still
        // synthetically down corrupts the action itself (a keypress slice's F5 lands as Alt+F5; exit-app
        // reads the foreground while the Alt-Tab switcher owns it), so let Alt up FIRST. This also commits
        // the highlighted window before the action runs, which is the honest ordering either way.
        ReleaseTaskSwitcher();
        Sfx.SliceFired();   // also during onboarding preview — the choose feedback is the point of practice

        // Onboarding practice: while the wizard is open the wheel is fully live for summoning/aiming/
        // arming, but firing must not actually RUN anything (the user is just trying gestures over the
        // wizard — sleeping the PC or launching a game mid-setup would be hostile). The hub shows a
        // "Practice" title chip with the slice's label where the action's readout would go.
        if (PracticeMode)
        {
            // Dry fire (onboarding practice OR the Settings Test-Wheel button): the "Practice" readout
            // lingers 2× as long as a real action's, so the user can read what they'd have triggered.
            // "was selected" under the slice's name: the name on its own would read as though
            // the action HAD run — the caption says what actually happened without shouting it.
            if (_overlayVisible)
                BeginStatusLinger(new ActionStatus(Loc.T(UiText.Overlay.Practice), slice.Label), lingerMs: 1000,
                                  caption: Loc.T(UiText.Narration.WasSelected));
            _announcer.Announce(Loc.F(UiText.Narration.PracticeNotRun, Loc.DefaultLabel(slice.Label)), AnnouncementKind.Result);
            return;
        }

        // An unconfigured wheel slice can't run — instead of a silent no-op, jump the user straight to it
        // in the Settings slice editor to finish setting it up. (Only for real wheel slices, which have an
        // index; synthetic fires from the Game Grid are always complete.)
        int idx = _activeSlices is null ? -1 : Array.IndexOf(_activeSlices, slice);
        // OBS slices count as unconfigured until the connection is set up (G13.A) — same
        // configure-on-fire flow as other config-requiring slices. Older configs that saved a
        // password before the ObsConfigured flag existed count as configured.
        bool obsUnconfigured = slice.Action?.Type == "obs"
            && !_config.Current.System.ObsConfigured
            && string.IsNullOrEmpty(_config.Current.System.ObsPassword);
        bool discordUnconfigured = NeedsDiscordIntegration(slice);
        if (idx >= 0 && (ActionExecutor.NeedsConfiguration(slice) || obsUnconfigured || discordUnconfigured))
        {
            if (_overlayVisible)
            {
                SoftReleaseInput();
                _overlay?.MarkFiringSlice(celebrate: false);   // jumping to Settings to configure — not a success
                _overlay?.Reset();
                _overlay?.FadeOutWheel();
            }
            _announcer.Announce(Loc.F(UiText.Narration.NeedsSetupOpening, Loc.DefaultLabel(slice.Label)), AnnouncementKind.Result);
            // The missing credentials come FIRST when both are missing: the slice editor replaces its URL
            // row with the same "Configure Discord Integration" prompt until they exist, so landing there
            // would only be a second click to reach this window. Once they're in, a still-incomplete slice
            // falls through to the editor for the channel URL.
            if (discordUnconfigured)
            {
                OpenDiscordIntegrationSetup();
                if (!ActionExecutor.NeedsConfiguration(slice)) return;
            }
            OpenSliceInSettings(_activeWheel, idx);
            return;
        }

        // Spawn the fire celebration BEFORE running the action: ActionExecutor.Execute is synchronous on
        // the UI thread, so a slow action (app launch, HDR toggle, an HTTP call) would otherwise sit
        // between the fire sound and its visual reward. The burst's clock starts on its first PAINTED
        // frame, so if the action does block the thread the animation still plays in full rather than
        // starting mid-flight.
        if (_overlayVisible) _overlay?.CelebrateFire();

        // Anticipate the one slow launch: a game that has to route through a storefront app that isn't
        // running yet. Probed BEFORE the launch (afterwards the storefront is already starting), and only
        // when it's genuinely cold — a warm storefront launches promptly, so nothing is shown and the hub
        // can never linger past a fast launch.
        var coldStore = slice.Action?.Type == "installed-game"
            ? StorefrontWarmup.PredictColdStart(slice.Action?.Url) : null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var status = _executor.Execute(slice);
        if (sw.ElapsedMilliseconds >= 30)
            Trace.WriteLine($"[FIRE] execute blocked the UI thread {sw.ElapsedMilliseconds}ms " +
                            $"(type={slice.Action?.Type}) — delays the fire animation by that much");
        // The OUTCOME, not just the attempt. A status-based failure ("Not Installed", "No Device", "Failed")
        // throws nothing, so ActionExecutor's exception trace never fires — without this line the user's
        // readout is the only record that anything went wrong.
        Trace.WriteLine($"[FIRE] {slice.Label} → {(status is null ? "(no status)" : $"{status.Line1} / {status.Line2}")}");

        if (coldStore is { } cs && _overlayVisible)
        {
            // Cold storefront: hold the hub (logo + "<Store> / Launching") for 1.5s over the gap where
            // nothing else is on screen yet. Fixed duration — it's a "this is happening" cue, not a
            // progress indicator, so it never waits on the game.
            SoftReleaseInput();
            _overlay?.MarkFiringSlice();
            _overlay?.Reset();
            _overlay?.FadeRingHoldCenter(cs.ShortName, Loc.T(UiText.Status.Launching),
                                         centreDelayMs: 1500, icon: cs.BrightGlyph());
            _announcer.Announce(Loc.F(UiText.Narration.Launching, Loc.DefaultLabel(slice.Label)), AnnouncementKind.Result);
        }
        else if (status is not null && _overlayVisible)
        {
            // Toggle confirmation: leave the wheel up (dimmed) with the new state shown in
            // its centre, then dismiss wheel + readout together when the linger expires.
            BeginStatusLinger(status);
            _announcer.Announce($"{Loc.Readout(status.Line1)}, {Loc.Readout(status.Line2)}", AnnouncementKind.Result);
        }
        else if (status is not null)
        {
            // Fired without an open wheel (mouse/browser path) — fall back to the toast, on the primary screen.
            var scr = NativeMethods.PrimaryScreenDips();
            double cx = scr.X + (_activeWheel == ActiveWheel.A ? scr.Width / 4.0 : scr.Width * 3.0 / 4.0);
            ShowStatusToast(Loc.Readout(status.Line1), Loc.Readout(status.Line2), cx, scr.Y + scr.Height / 2.0, status.LingerMs);
            _announcer.Announce($"{Loc.Readout(status.Line1)}, {Loc.Readout(status.Line2)}", AnnouncementKind.Result);
        }
        else if (_overlayVisible)
        {
            // Ordinary selection — fade the whole wheel out over ~0.5s (selected slice lingers).
            SoftReleaseInput();
            _overlay?.MarkFiringSlice();   // before Reset clears the armed state
            _overlay?.Reset();
            _overlay?.FadeOutWheel();
            // No ActionStatus = nothing visual to mirror, but a blind user still needs fired-vs-cancelled.
            // "Selected", not "fired"; launch types name the act instead. An action that narrated its own
            // outcome (a Set Volume slice reads the level it landed on) is left alone — appending the
            // generic line on top of it just talks over the part that carried the information.
            if (!_executor.SpokeThisExecution)
                _announcer.Announce(
                    IsLaunchType(slice.Action?.Type)
                        ? Loc.F(UiText.Narration.Launching, Loc.DefaultLabel(slice.Label))
                        : Loc.F(UiText.Narration.Selected, Loc.DefaultLabel(slice.Label)),
                    AnnouncementKind.Result);
        }
    }

    /// <summary>Hold-□ delete dwell with narration OFF — the same 500 ms the state machine defaults to.</summary>
    private const double BaseDeleteHoldMs = 500;

    /// <summary>How much longer a destructive hold must be held when narration is on. The spoken warning
    /// ("Hold to delete Steam") has to finish before the dwell completes, or the confirmation arrives after
    /// the thing it was confirming — for a listener that is the difference between a warning and a report.
    /// <para>Author-set figure. It applies to the WHEEL's hold-□ delete; the Game Grid's hold-□ hide has the
    /// same three-stage shape and should be checked against it.</para></summary>
    private const double NarrationDwellScale = 2.5;

    /// <summary>Action types whose narration names the ACT ("Launching Discord") rather than the pick
    /// ("Discord selected") — anything that puts an app or game on screen. Keep this in step with the
    /// launch-shaped cases in <see cref="ActionExecutor.Execute"/>.</summary>
    private static bool IsLaunchType(string? type) =>
        type is "installed-game" or "launcher" or "launch" or "discord-launch" or "discord-join";

    /// <summary>Toggle selection: fade the ring out while the centre lingers with the new state,
    /// then the centre fades after a short delay. Input is released immediately.
    /// <para>The status may ask for a longer hold than the 500 ms a state word needs
    /// (<see cref="ActionStatus.LingerMs"/>) — an explicit <paramref name="lingerMs"/> still wins, so
    /// callers with their own timing (onboarding practice) are unaffected.</para></summary>
    private void BeginStatusLinger(ActionStatus status, int? lingerMs = null, string? caption = null)
    {
        SoftReleaseInput();
        _overlay?.MarkFiringSlice();                              // before Reset clears armed state
        _overlay?.Reset();                                        // clear armed slice + aim state
        _overlay?.FadeRingHoldCenter(Loc.Readout(status.Line1), Loc.Readout(status.Line2),
                                     centreDelayMs: lingerMs ?? status.LingerMs ?? 500, caption: caption);
    }

    // ── Game browser ──────────────────────────────────────────────────────────

    private async void OpenGameBrowser()
    {
        // async void: contain failures (e.g. a storefront scan hitting a locked/dismounted library) —
        // an escaping exception would reach the dispatcher and take the whole app down.
        try
        {
            if (_browserOpen) return;
            if (_overlayVisible) DismissOverlay();
            await ShowBrowserAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Grid] open failed: {ex.Message}");
            if (_browserOpen) CloseGameBrowser();   // tear down the half-open surface so input isn't stranded
        }
    }

    /// <summary>Bring up the Game Grid (loading state, then populated) and route input to it. Does NOT
    /// dismiss the overlay — the edit-mode game pick keeps its suspended wheel alive underneath.</summary>
    private async System.Threading.Tasks.Task ShowBrowserAsync()
    {
        _browserOpen = true;
        InstallMouseWatch();   // click outside the grid card dismisses it (idempotent if a wheel installed it)

        var gridSys = _config.Current.System;
        // TEMPORARY: the grid always follows the WHEEL material — its own setting is disabled (no
        // Customize tiles; GameGridMaterial is kept in config and mirrored on save for a future re-enable).
        _overlay!.SetGamesMaterial(gridSys.SliceMaterial);
        _overlay.SetGamesReduceMotion(MotionPolicy.Reduce);   // effective policy, not the raw config bool
        _overlay.ShowGamesPanel();         // card appears immediately in a loading state
        _overlay.SetGamesPickMode(_editPickGame);   // footer hints: pick-for-edit vs normal browse
        _overlay.SetClickable(true);       // allow mouse + make the card visible
        UpdateInputCapture();              // HidHide cloak + Suppressed pad keep input out of the game
        RegisterBrowserHotkeys();

        var games = await System.Threading.Tasks.Task.Run(LoadGames).ConfigureAwait(true);
        if (_browserOpen) _overlay.SetGames(games, _config.Current.System.DisabledStorefronts);
        if (_browserOpen)
            _announcer.Announce(
                _editPickGame ? Loc.P(UiText.Narration.GridPickOne, UiText.Narration.GridPickOther, games.Count)
                              : Loc.P(UiText.Narration.GridOpenOne, UiText.Narration.GridOpenOther, games.Count),
                AnnouncementKind.Context);
        // First standalone Game Grid open → one-time cover-art tip (Select/Start). Persisted on close so it shows once.
        if (_browserOpen && !_editPickGame && !_config.Current.System.GameGridTipSeen)
        {
            _overlay.ShowGamesTip();
            _pendingGridTipPersist = true;
        }
        // Warm Select/Start cycle caches for the whole (possibly newly grown) library in the background, so
        // cycling doesn't spinner on first press. Already-warm games skip instantly.
        ArtPrefetcher.Kick(games, TimeSpan.FromMilliseconds(400));
        ControllerState? heldInput = null;
        _controller.ReplayLatestState(state => heldInput = state);
        if (heldInput is { } held) BrowserStickNav(held.LeftStickX, held.LeftStickY);
    }

    /// <summary>Build the grid's game list. With Playnite present we keep its list (unified names +
    /// local covers) but reconcile it against a fresh storefront scan, so games uninstalled while
    /// Playnite was closed don't linger as ghosts. Without Playnite the scan is already the truth.
    /// Storefronts the user opted out of (onboarding's storefront step) are dropped at the end.</summary>
    private IReadOnlyList<InstalledGame> LoadGames()
    {
        var all = LoadGamesUnfiltered();
        var off = _config.Current.System.DisabledStorefronts;
        return off.Count == 0 ? all
            : [.. all.Where(g => !off.Contains(g.Storefront, StringComparer.OrdinalIgnoreCase))];
    }

    private static IReadOnlyList<InstalledGame> LoadGamesUnfiltered()
    {
        var live = GameLibrary.Scan();
        if (!PlayniteLibrary.IsAvailable) return live;
        var playnite = PlayniteLibrary.GetInstalledGames();
        if (playnite is null) return live;

        var scannable  = GameLibrary.ScannableStores();
        var reconciled = GameLibrary.Reconcile(playnite, live, scannable);
        // The reconciler only FILTERS the Playnite list, so storefronts Playnite has no plugin for
        // (e.g. Battle.net) would never appear. Add the live-scan games for any such uncovered store —
        // PLUS any confirmed-scannable store (live is authoritative there, so a freshly-installed game
        // shows even before Playnite re-imports). Dedup by identity so a Playnite-matched game isn't
        // listed twice; covers for store-only games come from SteamGridDB-by-name.
        var covered = playnite.Select(g => g.Storefront).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var have    = reconciled.Select(GameLibrary.MatchKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // ADD-safe stores get live extras even though the "scannable" gate (which guards false DROPS from an
        // incomplete scan) doesn't cover them — adding can't drop anything, an incomplete scan just adds
        // fewer extras. So a game installed after Playnite's last import shows immediately instead of
        // waiting for a re-import. Steam/Ubisoft/Battle.net are id-keyed; itch/Epic/Xbox/EA are name-keyed
        // but their live scans are install-accurate (butler.db caves / Epic manifests / Xbox registry Root /
        // EA's per-install __Installer manifest), so their extras aren't ghosts either. GOG stays OUT — its
        // scan over-reports owned-but-uninstalled.
        var addSafe = new HashSet<string>(scannable, StringComparer.OrdinalIgnoreCase)
            { "Steam", "itch.io", "Epic", "Xbox", "EA" };
        // Name-keyed drift guard: exact-key dedup (have) misses the case where Playnite lists the SAME game
        // under a name that normalises differently (e.g. an edition suffix). Skip an extra whose normalised
        // name is edition-drift of a reconciled game of the same store, so it doesn't show twice.
        // ⚠ NOT bare substring matching: "portal2".Contains("portal"), so that would hide a
        // freshly-installed Portal whenever Playnite lists Portal 2 — defeating the very
        // freshly-installed-game case these extras exist for. NamesLikelySameGame only matches an
        // edition-suffix residue, never a sequel. And an extra whose key carries an exact native id
        // (Steam appid etc.) is skipped outright: identity there is unambiguous, `have` already deduped
        // it, and a name heuristic can only ever subtract real games.
        var pnNamesByStore = reconciled.GroupBy(g => g.Storefront, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(grp => grp.Key, grp => grp.Select(g => GameLibrary.NormalizeName(g.Name)).ToList(),
                          StringComparer.OrdinalIgnoreCase);
        bool DriftDuplicate(InstalledGame g)
        {
            if (GameLibrary.IsIdKey(GameLibrary.MatchKey(g))) return false;
            if (!pnNamesByStore.TryGetValue(g.Storefront, out var names)) return false;
            var n = GameLibrary.NormalizeName(g.Name);
            return names.Any(p => GameLibrary.NamesLikelySameGame(n, p));
        }
        // Path-keyed dedup: a live-scan game whose INSTALL FOLDER matches a reconciled Playnite entry is the
        // SAME game, even if the names differ (a stale itch receipt titled "Beethoven" vs Playnite "Beleth")
        // — drop the scan copy so the curated Playnite name/cover wins. No-op when either lacks a path
        // (falls back to the name/key dedup above), so it never wrongly drops a distinct game.
        static string NormDir(string? p) => string.IsNullOrWhiteSpace(p) ? "" : p.Trim().TrimEnd('\\', '/').ToLowerInvariant();
        var pnDirs = reconciled.Select(g => NormDir(g.InstallDir)).Where(d => d.Length > 0)
                               .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool SamePathAsPlaynite(InstalledGame g) => NormDir(g.InstallDir) is { Length: > 0 } d && pnDirs.Contains(d);
        var extras = live.Where(g => (!covered.Contains(g.Storefront) || addSafe.Contains(g.Storefront))
                                     && !DriftDuplicate(g)
                                     && !SamePathAsPlaynite(g)
                                     && have.Add(GameLibrary.MatchKey(g)));
        return [.. reconciled.Concat(extras).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private bool _pendingGridTipPersist;   // the one-time Game Grid tip fired this open; persist the flag on close

    /// <param name="spokenInstead">Replaces the "Game Grid closed" line. A close that happens BECAUSE
    /// something was chosen must announce the choice, not the close: both are context changes, so the
    /// close line would simply talk over the launch it caused.</param>
    private void CloseGameBrowser(string? spokenInstead = null)
    {
        if (!_browserOpen) return;
        RemoveMouseWatch();   // grid closing — drop the click-outside watch (a following wheel re-installs its own)
        if (_editPickGame) { CancelGamePickForEdit(); return; }   // edit-mode pick: cancel back to edit, clear the flag
        Trace.WriteLine("[Grid] Game Grid closed");
        _browserOpen = false;
        ResetBrowserStick();   // reset stick-nav so a held stick re-triggers next open
        UpdateInputCapture();
        UnregisterBrowserHotkeys();
        _overlay?.DismissGamesTip();
        _overlay?.SetClickable(false);
        _overlay?.HideGames();
        _announcer.Reset();   // pending grid-selection speech dies with the grid
        _announcer.Announce(spokenInstead ?? Loc.T(UiText.Narration.GameGridClosed), AnnouncementKind.Context);
        if (_pendingGridTipPersist)
        {
            _pendingGridTipPersist = false;
            var cur = _config.Current;
            _config.WriteConfig(new AppConfig
            {
                WheelA = cur.WheelA, WheelB = cur.WheelB,
                System = cur.System with { GameGridTipSeen = true },
                ActionColors = cur.ActionColors, ActionIcons = cur.ActionIcons, CustomColors = cur.CustomColors,
            });
        }
    }

    // ── Arcade ──────────────────────────────────────────────────────────────
    // The third modal overlay surface, built on the Game Grid's pattern above: one flag gates every
    // controller handler, the virtual pad goes neutral while it's up (see UpdateInputCaptureCore), and it
    // opens from an action type via a host callback. See Core/Arcade/Arcade.cs for the availability gate.

    private bool   _arcadeOpen;
    private string _arcadeGameId = "";
    /// <summary>This open came from the Arcade Launcher, so the cabinets are a layer ○ backs out to.</summary>
    private bool _arcadeFromLauncher;

    /// <summary>Fired from a slice (<c>arcade</c>). <paramref name="gameId"/> blank — or naming a game this
    /// build doesn't have — means "the Arcade picker"; until that exists it falls back to the single game,
    /// which is why an unknown id is never a dead end.</summary>
    private void OpenArcade(string? gameId)
    {
        try
        {
            // Unavailable (the harness conceals; no shipping build does) must no-op rather than rewrite an
            // existing arcade slice — docs/ARCADE.md's no-offer/no-rewrite contract.
            if (!Arcade.Available || _overlay is null) { Trace.WriteLine("[Arcade] slice fired while unavailable — ignored"); return; }
            if (_arcadeOpen) return;

            // Blank command = the Arcade LAUNCHER; an unknown id (a config from a newer build, or a removed
            // game) is treated the same rather than dead-ending. The launcher RESUMES the surface the arcade
            // was last dismissed on — the game you were playing, or the cabinets if that's where you left it
            // — and either way the cabinets stay a back-out layer under whatever comes up.
            var entry = ArcadeCatalog.Resolve(gameId);
            bool fromLauncher = entry is null;
            if (fromLauncher)
            {
                if (!string.IsNullOrWhiteSpace(gameId))
                    Trace.WriteLine($"[Arcade] unknown game '{gameId}' → launcher");
                entry = ArcadeCatalog.Resolve(ArcadeStore.LoadLastSurface());
            }
            _arcadeFromLauncher = fromLauncher;
            _arcadeGameId = entry?.Id ?? "";

            // The wheel it came from is on its way out; take its centre first so the game blooms exactly
            // where the wheel was.
            // No wheel has opened this session (a hotkey / future non-wheel caller) → centre of the display.
            var pscr = NativeMethods.PrimaryScreenDips();
            double cx = double.IsNaN(_lastWheelCx) ? pscr.X + pscr.Width  / 2.0 : _lastWheelCx;
            double cy = double.IsNaN(_lastWheelCy) ? pscr.Y + pscr.Height / 2.0 : _lastWheelCy;
            // The launcher sits where its wheel was; a game takes the position the player chose, or that same wheel's
            // until they have. The bloom starts at the wheel either way and slides if the game lives elsewhere.
            _arcadeLauncherPos = double.IsNaN(_lastWheelCx) ? 1 : _lastWheelSide;
            int storedPos = ArcadeStore.LoadWindowPosition();
            _arcadePos = storedPos >= 0 ? storedPos : _arcadeLauncherPos;

            _arcadeOpen = true;

            // ── The wheel doesn't just vanish ──
            // Its slices sweep in behind the hub, out from the one that was chosen, and the hub grows to sit
            // behind the playfield for the whole session. The game is shown when that finishes.
            //
            // _arcadeOpen is set BEFORE the sweep on purpose: it's what suppresses the virtual pad and routes
            // ○ to CloseArcade, so bailing out mid-animation is already handled rather than being a state the
            // sweep has to invent.
            //
            // ⚠ The wheel teardown must happen BEFORE the arcade's own input surfaces are installed.
            // SoftReleaseInput does RemoveMouseWatch + SetClickable(false) — it is written to tear a WHEEL
            // down — so installing the mouse watch first gets it immediately removed by the collapse, and
            // clicking outside the game then does nothing for the whole session.
            int armed = _overlay.ArmedIndex;
            bool sweeping = _overlayVisible && armed >= 0;
            if (sweeping)
            {
                SoftReleaseInput();
                _overlay.HideMixIndicator();
                _overlayVisible = false;   // dead to wheel input immediately; the visual lives on
            }
            else if (_overlayVisible) DismissOverlay();

            InstallMouseWatch();     // click outside the disc dismisses (idempotent if a wheel installed it)
            _overlay.SetClickable(true);
            UpdateInputCapture();    // cloak + a NEUTRAL virtual pad keep this session's input out of the game
            RegisterArcadeHotkeys(); // Esc dismisses, like every other surface

            if (sweeping)
            {
                // Sized so the hub's SMALLEST silhouette still clears the playfield — on kawaii the cloud's
                // troughs sit well inside its radius, so a flat 105% hid the scallops entirely. The
                // playfield is whichever surface comes up first: the cabinets' disc, or a game's larger one.
                double disc = string.IsNullOrEmpty(_arcadeGameId)
                    ? _overlay.ArcadeLauncherDiameterFor() : _overlay.ArcadeDiameterFor();
                double hubRadius = _overlay.ArcadeHubRadiusFor(disc / 2.0, OverlayWindow.ArcadeHubOversize);
                _overlay.BeginArcadeCollapse(armed, hubRadius, () => FinishOpenArcade(cx, cy));
                return;
            }

            FinishOpenArcade(cx, cy);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] open failed: {ex.Message}");
            if (_arcadeOpen) CloseArcade();   // never strand input on a half-open surface
        }
    }

    /// <summary>○ while a game is up: leave — or, with the pause menu open, back out of the menu.
    ///
    /// <para>No hidden hold gestures here (do not reintroduce) — Reset lives in the START pause menu,
    /// which is discoverable and names what it does.</para>
    ///
    /// <para>○ stays the one reserved button, deliberately NOT routed into <c>ArcadeInput</c>. A game that
    /// could observe it could also fail to hand it back, and "you can always get out with one button" would
    /// stop being true of the whole family.</para></summary>
    private void ArcadeCircle(bool pressed)
    {
        if (!pressed) return;
        // Back out of the arcade's own chrome first — the how-to card, then the pause menu — otherwise ○
        // would close the game from the very screens whose purpose is deciding what to do with it. The
        // control owns the precedence (ArcadeControl.BackOut); false means nothing there consumed it.
        if (_overlay?.ArcadeBackOut() == true) return;
        CloseArcade();
    }


    /// <summary>The second half of <see cref="OpenArcade"/>, run either immediately or once the wheel's
    /// collapse animation has finished. Guarded on <c>_arcadeOpen</c> because ○ / Esc / a controller drop can
    /// all close the arcade DURING the sweep — in which case this callback must do nothing rather than open a
    /// game onto a torn-down surface.</summary>
    private bool _arcadeReadOnlyWarned;

    private void FinishOpenArcade(double cx, double cy)
    {
        if (!_arcadeOpen || _overlay is null) return;
        var guard = EvaluateArcadeGuard();
        _overlay.ShowArcade(_arcadeGameId, cx, cy,
                            guard, overridable: guard?.Overridable ?? false, fromLauncher: _arcadeFromLauncher);
        _overlay.ArcadeWindowPosition = _arcadePos;
        _overlay.ArcadeSlideOnTriggers = HoldIsTrigger();
        if (!ArcadeStore.CanSave && !_arcadeReadOnlyWarned)
        {
            _arcadeReadOnlyWarned = true;
            ShowCornerToast("Arcade", Loc.T(UiText.Settings.ArcadeReadOnly));
        }
        // A game that lives at a different position than the wheel slides there out of the bloom; the cabinets
        // always stay on the wheel.
        if (!string.IsNullOrEmpty(_arcadeGameId) && _arcadePos != _arcadeLauncherPos)
            _overlay.SlideArcade(ArcadePosX(_arcadePos), ArcadeCy(), (int)(ArcadePickerTuning.LaunchSeconds * 1000));
        Trace.WriteLine($"[Arcade] open {_arcadeGameId}{(guard is null ? "" : " (guarded)")}");
    }

    /// <summary>Screen x of an arcade window position: the left wheel's quarter, the centre, the right wheel's.</summary>
    private static double ArcadePosX(int pos)
    {
        var s = NativeMethods.PrimaryScreenDips();
        return s.X + s.Width * (pos <= 0 ? 0.25 : pos >= 2 ? 0.75 : 0.5);
    }
    private static double ArcadeCy() { var s = NativeMethods.PrimaryScreenDips(); return s.Y + s.Height / 2.0; }

    /// <summary>The hold buttons move a GAME's window one position left or right — no wrap — and the choice is
    /// kept for every game. The cabinets never move.</summary>
    private void ArcadeSlide(int delta)
    {
        if (_overlay is null || !_arcadeOpen || _overlay.ArcadeIsPicker) return;
        int next = Math.Clamp(_arcadePos + delta, 0, 2);
        if (next == _arcadePos) return;
        _arcadePos = next;
        _overlay.ArcadeWindowPosition = next;
        ArcadeStore.SaveWindowPosition(next);
        _overlay.SlideArcade(ArcadePosX(next), ArcadeCy(), ArcadeSlideMs);
    }

    /// <summary>Whether the summon chord's HOLD is the trigger pair: then L2/R2 move the arcade window and the
    /// bumpers are left alone; a bumper hold, or anything else, puts the move on L1/R1.</summary>
    private bool HoldIsTrigger() =>
        TriggerModes.TryDecompose(_lastTrigger ?? "", out var primary, out _) && primary == TriggerPrimary.Trigger;

    private void CloseArcade()
    {
        if (!_arcadeOpen) return;
        Trace.WriteLine($"[Arcade] closed ({_arcadeGameId}) — snapshot persisted");
        _arcadeOpen = false;
        RemoveMouseWatch();
        UnregisterArcadeHotkeys();
        _overlay?.SetClickable(false);
        _overlay?.HideArcade();          // freezes + persists the snapshot, and ends the hub collapse
        // The wheel layer was left PAINTING behind the game (the grown hub) rather than hidden, and
        // _overlayVisible was already cleared when the sweep began — so nothing else will take it down.
        // Safe when no collapse ran: the wheel is hidden already and this is idempotent.
        _overlay?.HideWheel();
        UpdateInputCapture();            // un-suppress the pad so the game gets input again
    }

    /// <summary>
    /// THE BLEED-THROUGH GUARD. An arcade session holds the stick for minutes, not the fraction of a second a
    /// wheel is up — so if the physical pad isn't hidden with our virtual pad standing in, every one of those
    /// inputs also reaches the game underneath. When a GAME owns the foreground and we're not isolated, the
    /// window shows an explanation instead of the game.
    ///
    /// <para>Returns null to mean "play": either we're isolated, or nothing game-like is in front (bleeding
    /// into the desktop is harmless), or the user has already taken the △-hold override this session AND
    /// the current reason still offers one.</para>
    ///
    /// <para>Reuses App's existing isolation bookkeeping (<see cref="_isolated"/> /
    /// <see cref="_isolationNote"/>, maintained by <c>UpdateInputCaptureCore</c> and already surfaced in the
    /// tray tooltip) rather than re-deriving capture state — two derivations of the same thing would drift.</para>
    /// </summary>
    private Arcade.GuardCopy? EvaluateArcadeGuard()
    {
        if (_isolated && !_isolationUnverified) return null;
        // DetectRunningGameName is the same foreground-game check the Text Chat action gates on. Null means
        // nothing installed-and-game-like is in front, so there's nothing to bleed into.
        string? game = _platform.DetectRunningGameName();
        if (string.IsNullOrWhiteSpace(game)) return null;
        var copy = Arcade.Guard(Arcade.ReasonFromNote(_isolationNote), game);
        // The reason decides whether a stored override applies: an override taken under an involuntary
        // reason (cloak failed, no drivers) must NOT carry into a Passthru Mode block that begins mid-session
        // — Passthru Mode is deliberate and its card offers no bypass.
        if (copy.Overridable && _overlay?.ArcadeOverrideAccepted == true) return null;
        return copy;
    }

    /// <summary>Re-check the guard for an OPEN arcade. Called whenever capture state changes, so isolation
    /// lost mid-session freezes the game behind the card — and isolation regained lifts it.</summary>
    private void RefreshArcadeGuard()
    {
        if (!_arcadeOpen || _overlay is null) return;
        var guard = EvaluateArcadeGuard();
        if (guard is null) _overlay.ArcadeClearGuard();
        else if (!_overlay.ArcadeGuardShowing) _overlay.ArcadeShowGuard(guard, overridable: guard.Overridable);
    }

    private void RegisterArcadeHotkeys()
    {
        // Esc only. The arcade deliberately claims NO other keyboard keys: it's a controller surface, and the
        // wheel's ▲▼ volume keys would fight a game for them.
        UnregisterWheelHotkeys();
        RegisterBackKey("Arcade");
    }

    private void UnregisterArcadeHotkeys()
    {
        UnregisterBackKey();
        if (_overlayVisible) RegisterWheelHotkeys();   // hand Esc back if a wheel is somehow still up
    }

    // ── Esc for the Game Grid and the Arcade ──────────────────────────────────
    // Both surfaces are non-activating overlay windows, so the keyboard never reaches them as WPF key events.
    // Esc does exactly what ○ does on the surface: the LL keyboard hook is the primary path (it sees the key
    // wherever focus is, and swallows it so a game underneath never gets it); the RegisterHotKey claim is the
    // fallback for a hook Windows has dropped. RegisterHotKey is system-wide unique, so a refusal (another
    // program owns Esc) is traced rather than silent.
    private IntPtr _keyHook = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _keyProc;   // hold the delegate so it isn't GC'd
    private bool _escSwallowed;                             // the Esc key-down was consumed; eat its key-up too

    private void RegisterBackKey(string surface)
    {
        if (!NativeMethods.RegisterHotKey(_hotkeySource!.Handle, HkBack, NativeMethods.MOD_NONE, NativeMethods.VK_ESCAPE))
            Trace.WriteLine($"[{surface}] Esc hotkey refused (Win32 error {Marshal.GetLastWin32Error()}) - the keyboard hook is the only Esc path");
        if (_keyHook != IntPtr.Zero) return;
        _keyProc ??= KeyboardHookProc;
        _keyHook = NativeMethods.SetWindowsKeyboardHookEx(NativeMethods.WH_KEYBOARD_LL, _keyProc,
            NativeMethods.GetModuleHandle(null), 0);
        if (_keyHook == IntPtr.Zero)
            Trace.WriteLine($"[{surface}] Esc keyboard hook failed (Win32 error {Marshal.GetLastWin32Error()})");
    }

    private void UnregisterBackKey()
    {
        NativeMethods.UnregisterHotKey(_hotkeySource!.Handle, HkBack);
        if (_keyHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_keyHook); _keyHook = IntPtr.Zero; }
        _escSwallowed = false;
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (_browserOpen || _arcadeOpen))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (data.vkCode == NativeMethods.VK_ESCAPE && (data.flags & NativeMethods.LLKHF_ALTDOWN) == 0)
            {
                int msg = (int)wParam;
                if (msg == NativeMethods.WM_KEYDOWN)
                {
                    if (!_escSwallowed) Dispatcher.BeginInvoke(new Action(SurfaceBack));   // one action per press, not per auto-repeat
                    _escSwallowed = true;
                    return (IntPtr)1;
                }
                if (msg == NativeMethods.WM_KEYUP && _escSwallowed)
                {
                    _escSwallowed = false;
                    return (IntPtr)1;
                }
            }
        }
        return NativeMethods.CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    /// <summary>○ / Esc on the Game Grid or the Arcade. Mirrors <see cref="OnCircle"/>'s order: the arcade's
    /// own back-out layers first, then an edit-mode game pick, then a storefront hide-confirm, then close.</summary>
    private void SurfaceBack()
    {
        if (_arcadeOpen) { ArcadeCircle(true); return; }
        if (!_browserOpen) return;
        if (_editPickGame) { CancelGamePickForEdit(); return; }
        if (_overlay?.GamesCancelHideConfirm() != true) CloseGameBrowser();
    }

    private void LaunchSelectedGame()
    {
        // Browser ✕: the control filters (launcher strip) or raises GameChosen / LauncherChosen
        // (grid), which we handle below.
        _overlay?.BrowserActivate();
    }

    private void FireLauncher(string key)
    {
        // The wheel isn't open on this path, so FireAction's own launch line never fires — the grid's
        // closing announcement is the only chance to name what is being opened.
        string name = LauncherCatalog.Find(key)?.ShortName ?? key;
        CloseGameBrowser($"Launching {name}");
        FireAction(new WheelSlice
        {
            Label  = name,
            Action = new ActionConfig { Type = "launcher", Command = key },
        });
    }

    private void NavBrowser(int dx, int dy) { _overlay?.DismissGamesTip(); _overlay?.GamesMove(dx, dy); }

    // ── Left-stick navigation of the Game Grid (stick-as-d-pad: deadzone + initial-delay auto-repeat) ──
    private readonly StickNavigationRepeat _browserRepeat = new();
    private DispatcherTimer? _browserRepeatTimer;

    private void BrowserStickNav(float lx, float ly)
    {
        if (_browserRepeat.SetStick(lx, ly, Environment.TickCount64) is { } step)
            NavBrowser(step.X, step.Y);
        if (!_browserRepeat.Engaged) { _browserRepeatTimer?.Stop(); return; }
        if (_browserRepeatTimer is null)
        {
            _browserRepeatTimer = new DispatcherTimer(DispatcherPriority.Input)
                { Interval = TimeSpan.FromMilliseconds(16) };
            _browserRepeatTimer.Tick += (_, _) =>
            {
                if (!_browserOpen) { ResetBrowserStick(); return; }
                if (_browserRepeat.Tick(Environment.TickCount64) is { } repeat)
                    NavBrowser(repeat.X, repeat.Y);
            };
        }
        _browserRepeatTimer.Start();
    }

    private void ResetBrowserStick()
    {
        _browserRepeatTimer?.Stop();
        _browserRepeat.Reset();
    }

    // ── Assign a browsed game to a wheel (wheel invoke in the browser) ─────────────────────

    private static int MaxSlices => SystemConfig.MaxSlicesPerWheel;   // per-wheel cap — fixed at 12
    // Edit-mode "Add → Launch → Installed Game": the Game Grid is open over a SUSPENDED edit session
    // (the wheel stays in edit underneath, just hidden). Picking a game returns to edit carrying it.
    private bool _editPickGame;

    /// <summary>Build the wheel slice for an installed game: a direct storefront launch URL (avoiding
    /// the Playnite round-trip where possible) + a transparent logo for tinted rendering. Shared by the
    /// assign flows and the edit-mode game pick.</summary>
    private async System.Threading.Tasks.Task<WheelSlice> BuildGameSlice(InstalledGame game)
    {
        // Prefer a direct storefront URL: the GameId-derived one (Steam/GOG), else match against
        // GameLibrary.Scan() for Epic/Xbox/itch, else playnite://.
        string url = game.DirectLaunchUrl
                     ?? await System.Threading.Tasks.Task.Run(() => ResolveScanUrl(game)).ConfigureAwait(true)
                     ?? game.LaunchUrl;
        string? logoPath = await GameArt.GetSliceLogoPathAsync(game).ConfigureAwait(true);
        // G5: each added game gets a random swatch from the shared palette instead of the one fixed
        // installed-game blue — a wheel of games stops being a wall of one colour. Storing the BASE tone
        // flagged not-exact is exactly what a hand-picked swatch stores, so every material derives its
        // own variant at render time rather than us baking one in here.
        var swatch = ActionTint.Defaults[Random.Shared.Next(ActionTint.Defaults.Length)].Default;
        return new WheelSlice
        {
            Label     = game.Name,
            IconName  = "GamepadVariant",
            LogoPath  = logoPath,
            ShowLabel = false,   // A1: added slices default to no label
            IconColor      = ActionTint.ToHex(swatch),
            IconColorExact = false,   // a swatch BASE tone: every material derives its own variant
            Action    = new ActionConfig { Type = "installed-game", Url = url },
        };
    }

    // ── Self-heal: re-resolve logos for installed-game slices whose LogoPath was stripped ──────────
    // An older Settings save (before SliceEditModel preserved LogoPath) could null out an assigned
    // game's logo path. Re-derive it from the slice (Label + launch URL) — a cache hit for any logo
    // already fetched — and persist once so it sticks.

    private async System.Threading.Tasks.Task HealMissingGameLogosAsync()
    {
        try
        {
            var cur = _config.Current;
            string snapA = System.Text.Json.JsonSerializer.Serialize(cur.WheelA);
            string snapB = System.Text.Json.JsonSerializer.Serialize(cur.WheelB);
            var (a, ca) = await HealWheelLogosAsync(cur.WheelA).ConfigureAwait(true);
            var (b, cb) = await HealWheelLogosAsync(cur.WheelB).ConfigureAwait(true);
            if (!ca && !cb) return;
            // The logo fetches take seconds — a wheel write in that window (grid-assign, edit live-save,
            // onboarding) must not be clobbered by this startup snapshot. Abort instead; the un-healed
            // slice is re-healed at the next launch.
            var latest = _config.Current;
            if (System.Text.Json.JsonSerializer.Serialize(latest.WheelA) != snapA
                || System.Text.Json.JsonSerializer.Serialize(latest.WheelB) != snapB)
            {
                Trace.WriteLine("[Heal] wheels changed during the logo heal — skipping persist (re-heals next launch)");
                return;
            }
            PersistBothWheels(a, b);
        }
        catch { /* best effort — a failed heal just leaves the default glyph */ }
    }

    private static async System.Threading.Tasks.Task<(WheelSlice[] slices, bool changed)> HealWheelLogosAsync(WheelSlice[] slices)
    {
        WheelSlice[]? healed = null;
        for (int i = 0; i < slices.Length; i++)
        {
            var s = slices[i];
            // Heal an installed-game slice whose logo is MISSING only: never assigned (empty path) OR a
            // stale path to a file that's gone (e.g. a restore whose bundled logo didn't land, or a cleared
            // cache). Deliberately does NOT re-resolve a logo that resolves fine — the slice editor's ◀ ▶
            // arrows let the user pick any downloaded logo, including the grid's default and its Start
            // alternates, and re-resolving would silently overwrite that choice on the next launch.
            bool needsLogo = string.IsNullOrWhiteSpace(s.LogoPath) || !System.IO.File.Exists(s.LogoPath);
            if (s.Action?.Type != "installed-game" || !needsLogo) continue;
            var url = s.Action.Url;
            if (string.IsNullOrWhiteSpace(url)) continue;

            var game = new InstalledGame(s.Label, url, GameLibrary.StorefrontFromUrl(url));
            var logo = await GameArt.GetSliceLogoPathAsync(game).ConfigureAwait(true);
            if (string.Equals(logo, s.LogoPath, StringComparison.OrdinalIgnoreCase)) continue;   // no change
            if (string.IsNullOrWhiteSpace(logo)) continue;

            (healed ??= (WheelSlice[])slices.Clone())[i] = s.WithLogoPath(logo);
        }
        return (healed ?? slices, healed is not null);
    }

    private bool PersistBothWheels(WheelSlice[] a, WheelSlice[] b)
    {
        var cur = _config.Current;
        return TryWriteConfig(new AppConfig
        {
            WheelA       = a,
            WheelB       = b,
            System       = cur.System,
            ActionColors = cur.ActionColors,
            ActionIcons  = cur.ActionIcons,
            CustomColors = cur.CustomColors,
        });
    }

    // ── In-wheel editing (V2) ───────────────────────────────────────────────────
    private bool Editing => _overlay?.EditMode == true;

    /// <summary>Edit mode is active AND owning input — false while suspended behind the game-pick
    /// browser, so the edit-mode button handlers defer to the browser.</summary>
    private bool EditingNav => Editing && !_editPickGame;

    /// <summary>Enter controller-only edit mode on the open wheel (hold Fn + click the aiming stick).
    /// The wheel stays up after Fn releases; ○ saves and exits.</summary>
    private void EnterEdit()
    {
        if (_onboardingWindow is { IsLoaded: true } && _oobePreview) return;   // edit off during onboarding, but LIVE on the final step (preview off = wheels live)
        if (_overlay is null || !_overlayVisible || Editing || _browserOpen) return;
        if (_activeSlices is null) return;
        _overlay.ClearPreview();
        _previewArmed = -1;
        _overlay.EditMaxSlices = MaxSlices;   // current cap → legend hides "add" at it
        // A destructive dwell has to outlast the warning that describes it. Read live, so toggling narration
        // takes effect on the next edit session without a restart.
        _overlay.EditDeleteHoldMs = BaseDeleteHoldMs * (_announcer.Enabled ? NarrationDwellScale : 1.0);
        // A listener lets go of the stick after hearing an entry, so the picker keeps it armed only while narrating.
        _overlay.EditPickerLatchesAtCentre = _announcer.Enabled;
        _wheelEmptied = false;                // re-arm the final-slice-removed handling for this session
        bool resumedEdits = _pendingWheelEdits.TryGetValue(_activeWheel, out var pending);
        if (resumedEdits)
        {
            _activeSlices = pending!.ToArray();
            ShowCornerToast("Radiata", Loc.T(UiText.Settings.ResumeWheelEdits), holdMs: 12000);
        }
        _overlay.BeginEdit(_activeSlices);
        _overlay.SlideToCenter(_config.Current.System.FadeMs);   // bring the edited wheel to screen centre
        _overlay.ShowScrim(_config.Current.System.FadeMs);       // dim the whole screen behind it
        _editArmed = -1;   // fresh session: the first armed slice must announce itself
        int n = _overlay.EditCurrentSlices.Length;
        // Names the buttons: edit mode is controller-only with no on-screen text a narration user can read.
        _announcer.Announce(
            (resumedEdits ? Loc.T(UiText.Settings.ResumeWheelEdits) + " " : "") +
            (n == 0 ? Loc.T(UiText.Narration.EditModeEmpty)
                   : Loc.P(UiText.Narration.EditModeOne, UiText.Narration.EditModeOther, n,
                           ControllerButtons.Spoken(PadButton.Cross, capitalize: true), ControllerButtons.Spoken(PadButton.Triangle),
                           ControllerButtons.Spoken(PadButton.Square), ControllerButtons.Spoken(PadButton.Circle))),
            AnnouncementKind.Context);
        // Nothing to move/delete on an empty wheel — jump straight into the Add picker.
        if (_overlay.EditCurrentSlices.Length == 0) EditAdd();
    }

    /// <summary>Stick-click while a silent (empty-wheel) invocation is active: perform the visual open
    /// that <see cref="ShowOverlay"/> skipped — centred, since edit lives centred — then enter edit, which
    /// jumps straight into the Add picker for an empty wheel. This is the ONLY on-screen path for an empty
    /// wheel; a plain invoke stays invisible (the side acts disabled).</summary>
    private void EnterEditFromSilent()
    {
        if (_onboardingWindow is { IsLoaded: true } && _oobePreview) return;   // edit off during onboarding, but LIVE on the final step (preview off = wheels live)
        if (_silentWheel is not { } wheel || _overlay is null || Editing || _overlayVisible) return;
        _silentWheel = null;   // consumed

        var slices = wheel == ActiveWheel.A ? _config.Current.WheelA : _config.Current.WheelB;
        SetMaterialFlags(_config.Current.System.SliceMaterial);
        PopulateIcons(slices);
        _activeSlices = slices;
        _activeWheel  = wheel;
        _overlay.SetSlices(slices);
        _overlay.Reset();

        var sys = _config.Current.System ?? new SystemConfig();
        _overlay.StickyMs = sys.StickyMs;
        _overlay.RefreshScreenBounds();
        var scr = NativeMethods.PrimaryScreenDips();   // centre on the primary screen
        _overlay.CenterAt(scr.X + scr.Width / 2.0, scr.Y + scr.Height / 2.0);
        _previewArmed = -1;
        _overlay.PrepareIntro(0, 0, 0.5);
        _overlay.PlayIntro(sys.FadeMs);
        _overlay.SetClickable(true);
        _overlayVisible = true;
        UpdateInputCapture();   // the editor IS a real surface — suppress the game while it's up

        RegisterWheelHotkeys();

        EnterEdit();            // empty wheel → straight into the Add picker
    }

    // ── Edit / picker narration (accessibility A1) ────────────────────────────
    // Edit mode is controller-only and its state lives in glyphs and position, so narration has to carry
    // the phase, the focused item, and the result of every structural change. _editArmed tracks the last
    // announced focus so the per-frame stick handler only speaks on change.
    private int _editArmed = -1;
    private bool _deleteHeldTarget;   // a hold-□ delete dwell is running (for the abandoned/completed edge)

    /// <summary>Narrate the focused item in edit or picker mode. In Carrying the focus is the DROP SLOT,
    /// which is what the user is choosing, so it's phrased as a position rather than a name.</summary>
    private void AnnounceEditArmed()
    {
        if (!_announcer.Enabled || _overlay is null) return;
        bool carrying = _overlay.EditPhase == WheelStateMachine.EditPhase.Carrying;
        // While carrying, the thing that MOVES as you aim is the drop slot; ArmedIndex is pinned to the
        // slice's origin and would both name the wrong position and — because it never changes — speak
        // once and then go silent for the rest of the sweep.
        int armed = carrying ? _overlay.EditCarrySlot : _overlay.ArmedIndex;
        if (armed == _editArmed) return;
        _editArmed = armed;
        if (armed < 0) return;

        if (_overlay.EditPhase == WheelStateMachine.EditPhase.Picking)
        {
            var level = _pickerStack.Count > 0 ? _pickerStack.Peek() : null;
            if (level is null || (uint)armed >= (uint)level.Length) return;
            var node = level[armed];
            // "…, submenu" tells a listener the ellipsis they can't see: cross goes deeper, it doesn't add.
            _announcer.Announce(
                Loc.F(UiText.Narration.SliceOf, node.Label, armed + 1, level.Length) + (node.Children is not null ? Loc.T(UiText.Narration.SubmenuSuffix) : ""),
                AnnouncementKind.Selection);
            return;
        }

        var slices = _overlay.EditCurrentSlices;
        if ((uint)armed >= (uint)slices.Length) return;
        _announcer.Announce(
            carrying ? Loc.F(UiText.Narration.PositionOf, armed + 1, slices.Length)
                     : Loc.F(UiText.Narration.SliceOf, Loc.DefaultLabel(slices[armed].Label), armed + 1, slices.Length),
            AnnouncementKind.Selection);
    }

    /// <summary>✕ in edit mode: choose a picker entry, drop the carried slice, or pick up the armed one.</summary>
    private void EditCross()
    {
        if (_overlay is null) return;
        switch (_overlay.EditPhase)
        {
            case WheelStateMachine.EditPhase.Picking:  EditPickerSelect();        break;
            case WheelStateMachine.EditPhase.Carrying:
                // Read the slot BEFORE the drop — dropping clears the carry state. Must be the CARRY slot,
                // not ArmedIndex: the latter is pinned to where the slice came from, so it would confirm
                // the origin rather than the position the user just chose.
                int slot = _overlay.EditCarrySlot;
                _overlay.EditDrop();
                _editArmed = -1;
                _announcer.Announce(Loc.F(UiText.Narration.DroppedAt, slot + 1), AnnouncementKind.Result);
                break;
            default:
                int up = _overlay.ArmedIndex;
                var cur = _overlay.EditCurrentSlices;
                _overlay.EditPickUp();
                _editArmed = -1;
                if ((uint)up < (uint)cur.Length)
                    _announcer.Announce(
                        Loc.F(UiText.Narration.PickedUp, Loc.DefaultLabel(cur[up].Label), CarryHint()),
                        AnnouncementKind.Result);
                break;
        }
    }

    /// <summary>○ in edit mode: step back in the picker, cancel a carry, or save + exit.</summary>
    private void EditCircle()
    {
        if (_overlay is null) return;
        switch (_overlay.EditPhase)
        {
            case WheelStateMachine.EditPhase.Picking:  EditPickerBack();          break;
            case WheelStateMachine.EditPhase.Carrying:
                _overlay.EditCancelCarry();
                _editArmed = -1;
                _announcer.Announce(Loc.T(UiText.Narration.MoveCancelled), AnnouncementKind.Result);
                break;
            default:                                   ExitEdit();                break;
        }
    }

    /// <summary>△ in edit mode: open the category→type Add picker (Selecting phase only).</summary>
    private void EditAdd()
    {
        if (_overlay is null || _overlay.EditPhase != WheelStateMachine.EditPhase.Selecting) return;
        if (_overlay.EditCurrentSlices.Length >= MaxSlices)
        {
            // Announce, don't silently no-op: on screen the legend simply drops "add", which a narration user can't see.
            _announcer.Announce(Loc.F(UiText.Narration.WheelFull, MaxSlices), AnnouncementKind.Result);
            return;
        }
        _pickerStack.Clear();
        _pickerStack.Push(BuildAddMenu());
        ShowPickerLevel();
    }

    // ── Add picker (category → type radial sub-wheels; phase 4a) ─────────────────
    // A node is either a submenu (Children) or a leaf action (Leaf). Built from the SAME taxonomy the
    // Settings editor uses (WheelEditorControl.Categories) so the two never drift.
    private sealed record PickNode(string Label, string? Icon, PickNode[]? Children,
                                   WheelEditorControl.TypeOption? Leaf, string? Tint, string? LauncherKey = null);
    private readonly Stack<PickNode[]> _pickerStack = new();

    // Mirrors the Settings categories, with shortcuts: "Installed Game" AND "Game Grid" are promoted to
    // top-level leaves (dropped from the Games & Apps sub-wheel so they aren't listed twice), and the
    // "Radiata" category (Open Settings) is flattened to top-level leaves rather than a sub-wheel. The
    // Settings taxonomy (WheelEditorControl.Categories) is untouched. Other value-pick types (launcher,
    // switch-audio, volume-set) still land as "finish in Settings" placeholders for now.
    private static PickNode[] BuildAddMenu()
    {
        var cats = new List<PickNode>();

        // Installed Game first — it's the highlight of the Add flow — then Game Grid beside it.
        if (FindTypeOption("installed-game") is { } ig)
            cats.Add(new PickNode(Loc.T(ig.Display), null, null, ig, AddMenuIcons.CategoryColorHex("Games & Apps")));
        if (FindTypeOption("game-browser") is { } gb)
            cats.Add(new PickNode(Loc.T(gb.Display), null, null, gb, null));

        // key = identity (glyph/colour lookups, overrides); header = what the node shows.
        foreach (var (key, header, entries) in WheelEditorControl.Categories)
        {
            // The "Radiata" actions (Game Grid, Open Settings) are promoted to top-level leaves — no
            // submenu/ellipsis. (The Settings editor's Radiata tab is untouched — only this picker changes.)
            if (string.Equals(key, "Radiata", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var e in entries)
                {
                    if (e.Hidden) continue;   // e.g. Arcade in a build without it (Arcade.Available false) — must not surface here
                    // ⚠ Radiata's entries are flattened to leaves, but its GROUPS still get a sub-wheel,
                    // exactly as in every other category. Flattening a group would take Options[0] alone
                    // and silently drop the rest of it. Arcade is a category: the Launcher and the games
                    // under it are what you add.
                    if (WheelEditorControl.IsGroup(e))
                    {
                        // Hidden options stay unlisted here too — same rule the Settings sub-dropdown follows.
                        var groupLeaves = e.Options.Where(o => !o.Hidden)
                            .Select(o => new PickNode(Loc.T(o.Display), null, null, o, null)).ToArray();
                        if (groupLeaves.Length > 0)
                            cats.Add(new PickNode(Loc.T(e.Display), AddMenuIcons.GroupGlyph(e.GroupKey), groupLeaves,
                                                  null, AddMenuIcons.GroupColorHex(e.GroupKey, key)));
                    }
                    else if (e.Options[0].Type is not "game-browser")   // already a top-level leaf above (don't double-add)
                    {
                        cats.Add(new PickNode(Loc.T(e.Options[0].Display), null, null, e.Options[0], null));
                    }
                }
                continue;
            }

            string tint = AddMenuIcons.CategoryColorHex(key);
            var children = new List<PickNode>();
            foreach (var e in entries)
            {
                if (e.Hidden) continue;   // deliberately unlisted (e.g. Sequence); see WheelEditorControl.Categories
                if (WheelEditorControl.IsGroup(e))
                {
                    // Hidden OPTIONS (e.g. the nvidia-*/display-extend commands kept inside "Display"
                    // for safe re-selection — see WheelEditorControl.TypeOption.Hidden) must not show up here
                    // either; the in-wheel Add picker shares this exact taxonomy with the Settings dropdowns.
                    var leaves = e.Options.Where(o => !o.Hidden)
                        .Select(o => new PickNode(Loc.T(o.Display), null, null, o, null)).ToArray();
                    children.Add(new PickNode(Loc.T(e.Display), AddMenuIcons.GroupGlyph(e.GroupKey), leaves, null,
                                              AddMenuIcons.GroupColorHex(e.GroupKey, key)));
                }
                else if (e.Options[0].Type == "launcher")   // expand into a sub-wheel of the INSTALLED launchers
                {
                    var launchers = LauncherCatalog.All
                        .Where(l => LauncherCatalog.IsPresent(l.Key))   // games alone don't imply the launcher app
                        .Select(l => new PickNode(l.ShortName, l.Glyph, null, null, null, l.Key))
                        .ToArray();
                    children.Add(new PickNode(Loc.T(e.Options[0].Display), AddMenuIcons.LauncherMenuGlyph(), launchers,
                                              null, AddMenuIcons.LauncherMenuColorHex()));
                }
                // installed-game is promoted to a top-level leaf AND kept inside Games & Apps (users
                // browsing the category expect to find it there too).
                // game-browser stays top-level-only: it already appears twice otherwise (Radiata + top).
                else if (e.Options[0].Type is not "game-browser")
                {
                    children.Add(new PickNode(Loc.T(e.Options[0].Display), null, null, e.Options[0], null));
                }
            }
            if (children.Count > 0)
                cats.Add(new PickNode(header, AddMenuIcons.CategoryGlyph(key), children.ToArray(), null, tint));
        }
        return cats.ToArray();
    }

    /// <summary>Find a TypeOption in the Settings taxonomy by action type (first match).</summary>
    private static WheelEditorControl.TypeOption? FindTypeOption(string type)
    {
        foreach (var (_, _, entries) in WheelEditorControl.Categories)
            foreach (var e in entries)
                foreach (var o in e.Options)
                    if (o.Type == type) return o;
        return null;
    }


    /// <summary>Build the slice a chosen type creates — label + action + the same per-type defaults the
    /// Settings editor applies (Discord keybinds, Sleep/Reboot hold-to-confirm). Icon resolves from the
    /// action's type default via PopulateIcons. Free-text types land as placeholders to finish in Settings.
    /// <para>"installed-game" gets a RANDOM swatch off <see cref="ActionTint.Defaults"/>, mirroring
    /// <c>WheelEditorControl.BtnAdd_Click</c> (G5) — several games added one-by-one show a spread of
    /// colours, not a wall of the one fixed installed-game blue. Other types keep the plain type-derived
    /// colour, matching what Settings gives them too.</para></summary>
    /// <param name="display">True for the PICKER's own slice, which names the option (the launcher reads
    /// "Arcade Launcher" like the Settings dropdown); false for the slice that is actually added, whose label
    /// is the default slice label (see <see cref="AddedSliceLabel"/>).</param>
    private static WheelSlice MakeSliceForType(WheelEditorControl.TypeOption o, bool display = false)
    {
        // Same random-swatch pick BtnAdd_Click makes, kept in sync so "added in Settings" and "added
        // in-wheel" look the same: one call to Random.Shared per slice, storing the BASE tone flagged
        // not-exact. The flag has to be written EXPLICITLY: leaving it null on a slice that HAS an
        // IconColor reads as a hand-typed hex under the legacy rule (see ActionTint.IsExactColor),
        // which paints the dark base verbatim on a dark wheel.
        string? iconColor = null;
        bool?   iconColorExact = null;
        if (o.Type == "installed-game")
        {
            var swatch = ActionTint.Defaults[Random.Shared.Next(ActionTint.Defaults.Length)].Default;
            iconColor      = ActionTint.ToHex(swatch);
            iconColorExact = false;
        }
        return new WheelSlice
        {
            Label  = display ? o.Display : AddedSliceLabel(o),
            ShowLabel = false,   // A1: added slices default to no label
            IconColor      = iconColor,
            IconColorExact = iconColorExact,
            Action = new ActionConfig
            {
                Type    = o.Type,
                // text-chat has no Categories-supplied Command (its option is a plain Leaf, null Command) —
                // default a freshly-added one to "Try Game Default" mode so NeedsConfiguration only nags
                // for the missing message, not a missing mode too.
                Command = o.Type == "text-chat" ? "default" : o.Command,
                // exit-app guards by TYPE (its Command is null); closing someone's game deserves the
                // same dwell as the power actions.
                RequireConfirm = o.Type is "exit-app"
                    || o.Command is "sleep" or "reboot" or "shutdown" or "logout" or "hibernate" or "empty-recycle-bin",
            },
        };
    }

    /// <summary>The label an ADDED slice carries, where that differs from the picker entry's own wording.
    /// A picker entry names an option in a menu of options ("Toggle DualShock Mode"); a slice on the wheel
    /// names a thing ("DualShock Mode"), which is also what the Settings editor infers for the same type,
    /// so both add routes land on one label. Anything with no entry here keeps the option's display name.
    /// <para>⚠ These must be <see cref="UiText.DefaultLabels"/> constants: <c>Loc.DefaultLabel</c> resolves
    /// an untouched default against that set at DISPLAY time, and config always holds the English. A literal
    /// here would language-lock the slice.</para></summary>
    private static string AddedSliceLabel(WheelEditorControl.TypeOption o) => o.Type switch
    {
        "arcade" when o.Command is null => UiText.DefaultLabels.Arcade,   // the Launcher; a game names itself
        "xbox-emulation"                => UiText.DefaultLabels.XboxMode,
        "dualshock-emulation"           => UiText.DefaultLabels.DualShockMode,
        _                               => o.Display,
    };

    /// <summary>Copy of a picker slice with a trailing "…" on the label (marks a drill-in / further step).</summary>
    private static WheelSlice WithEllipsis(WheelSlice s) =>
        new() { Label = s.Label + "…", IconName = s.IconName, IconPath = s.IconPath,
                IconColor = s.IconColor, IconColorExact = s.IconColorExact,
                IconColorLight = s.IconColorLight,   // legacy field: carried so a clone isn't reinterpreted
                LogoPath = s.LogoPath, Action = s.Action };

    /// <summary>A launcher choice in the Add → Launcher sub-wheel: a "launcher" action keyed to that
    /// launcher, with its brand glyph + tint.</summary>
    private static WheelSlice MakeLauncherSlice(string key)
    {
        var info = LauncherCatalog.Find(key);
        return new WheelSlice
        {
            Label     = info?.ShortName ?? key,
            IconName  = info?.Glyph,
            ShowLabel = false,   // A1: added slices default to no label
            // NO IconColor — baking the brand colour in as an exact hex reads as a hand-typed colour:
            // it skips the material variant, keeps its dark brand tone on a dark wheel, and (since this
            // slice is the one carried into the wheel) sticks in config forever. Unset routes through
            // EffectiveColor("launcher", key), which resolves the same brand colour (or the user's Color
            // Default for that storefront) AND gets the material-appropriate variant.
            Action    = new ActionConfig { Type = "launcher", Command = key },
        };
    }

    // Drilling IN grows the level from the centre; coming back OUT just fades the parent in (quicker).
    private void ShowPickerLevel(bool grow = true)
    {
        var level  = _pickerStack.Peek();
        // Submenu entries get an ellipsis (they lead to another level) and the category accent tint.
        // "Installed Game" is a leaf but opens the Game Grid to pick a title — a further step — so it
        // gets the same trailing "…".
        var slices = level.Select(n =>
                            n.LauncherKey is { } key ? MakeLauncherSlice(key) :
                            n.Leaf is { Type: "installed-game" } o ? WithEllipsis(MakeSliceForType(o, display: true)) :
                            n.Leaf is { } o2         ? MakeSliceForType(o2, display: true) :
                            // A category / group node has no action type to derive a colour from, so its
                            // accent has to be carried on the slice — flagged NOT-exact so it takes the
                            // material variant like a swatch would. Leaving the flag null would read as
                            // a hand-typed exact colour and keep the dark accent on a dark wheel.
                            new WheelSlice { Label = n.Label + "…", IconName = n.Icon,
                                             IconColor = n.Tint, IconColorExact = false }).ToArray();
        // H3: the Add picker always renders in Flat Dark, whatever the user's material is — a menu of
        // choices needs readable uniformity, not the ornamental treatments, and one fixed backdrop keeps
        // the picker looking the same for everyone. The glyphs bake with the Dark tint set to match, so no
        // per-material colour variant leaks onto a wheel that isn't that material; slices the user has
        // actually placed still take their material's variant.
        // EndPicker/exit paths restore the configured material (ApplyConfig re-pushes it).
        PopulateIcons(slices, ActionTint.TintSet.Dark);
        _overlay!.SetSliceMaterial(Materials.FlatDark);
        _overlay!.ShowPicker(slices);
        int ms = _config.Current.System.FadeMs;
        if (grow) _overlay!.PickerDrillIn(ms);
        else      _overlay!.PickerFadeIn(FadeInMs(ms));
        _editArmed = -1;   // a new level: re-announce whatever is armed here
        // Depth is what tells a listener whether ○ backs out a level or leaves the picker entirely.
        _announcer.Announce(
            _pickerStack.Count > 1 ? Loc.P(UiText.Narration.AddPickerSubOne, UiText.Narration.AddPickerSubOther, level.Length)
                                   : Loc.P(UiText.Narration.AddPickerOne, UiText.Narration.AddPickerOther, level.Length),
            AnnouncementKind.Context);
    }

    // A quicker fade for backing-out (parent reappearing), still instant when animations are off.
    private static int FadeInMs(int fadeMs) => fadeMs <= 0 ? 0 : Math.Max(90, fadeMs * 2 / 3);

    private void EditPickerSelect()
    {
        if (_overlay is null) return;
        var level = _pickerStack.Peek();
        int i = _overlay.ArmedIndex;
        if ((uint)i >= (uint)level.Length) return;
        var node = level[i];
        if (node.Children is { } kids) { _pickerStack.Push(kids); ShowPickerLevel(); return; }
        if (node.LauncherKey is { } lkey)   // a launcher choice → make + carry its slice
        {
            var lslice = MakeLauncherSlice(lkey);
            PopulateIcons([lslice]);
            _pickerStack.Clear();
            _overlay.EndPicker(); _overlay.SetSliceMaterial(_config.Current.System.SliceMaterial);
            _overlay.EditAddAndCarry(lslice);
            AnnounceCarryingNew(lslice.Label);
            return;
        }
        if (node.Leaf is { } o)
        {
            // Installed Game needs a target — open the Game Grid to pick it, then resume the edit.
            if (o.Type == "installed-game") { BeginGamePickForEdit(); return; }

            var slice = MakeSliceForType(o);
            PopulateIcons([slice]);
            _pickerStack.Clear();
            _overlay.EndPicker(); _overlay.SetSliceMaterial(_config.Current.System.SliceMaterial);
            _overlay.EditAddAndCarry(slice);   // hand the new slice to the user to place
            AnnounceCarryingNew(slice.Label);
        }
    }

    /// <summary>Hold-□ delete in edit mode, with the three-stage narration a destructive act needs: name
    /// the target on press, say so if the hold is abandoned, and confirm the removal when the dwell
    /// completes (that completion is detected in <see cref="PersistEdited"/>, the one place a
    /// timer-driven delete surfaces).</summary>
    private void EditDeleteHeld(bool pressed)
    {
        if (_overlay is null) return;
        if (pressed && _overlay.EditPhase == WheelStateMachine.EditPhase.Selecting)
        {
            var sl = _overlay.EditCurrentSlices;
            int at = _overlay.ArmedIndex;
            if ((uint)at < (uint)sl.Length)
            {
                _deleteHeldTarget = true;
                _deleteHeldLabel  = sl[at].Label;
                _announcer.Announce(Loc.F(UiText.Narration.HoldToDelete, Loc.DefaultLabel(sl[at].Label)), AnnouncementKind.Result);
            }
        }
        else if (!pressed && _deleteHeldTarget)
        {
            // Released before the dwell completed — the completion path clears the flag first, so reaching
            // here always means abandoned.
            _deleteHeldTarget = false;
            _announcer.Announce(Loc.T(UiText.Narration.HoldCancelled), AnnouncementKind.Result);
        }
        _overlay.EditSetDeleteHeld(pressed);
    }

    private string? _deleteHeldLabel;

    /// <summary>L1/R1 undo/redo in edit mode. The wheel's own reflow is the only visual feedback, and a
    /// no-op (empty stack) looks identical to a successful one — so narration says which happened.</summary>
    private void EditUndoRedo(bool undo)
    {
        if (_overlay is null) return;
        bool changed = undo ? _overlay.EditUndo() : _overlay.EditRedo();
        if (!changed)
        {
            _announcer.Announce(Loc.T(undo ? UiText.Narration.NothingToUndo : UiText.Narration.NothingToRedo), AnnouncementKind.Result);
            return;
        }
        int n = _overlay.EditCurrentSlices.Length;
        _editArmed = -1;   // the list changed under the focus
        _announcer.Announce(undo ? Loc.P(UiText.Narration.UndoneOne, UiText.Narration.UndoneOther, n)
                                 : Loc.P(UiText.Narration.RedoneOne, UiText.Narration.RedoneOther, n),
                            AnnouncementKind.Result);
    }

    /// <summary>The "how to place what you're carrying" clause, shared by the pick-up and the just-added
    /// announcements so the two can't drift apart — and so the button names have ONE place to resolve.</summary>
    private static string CarryHint() =>
        Loc.F(UiText.Narration.CarryHint, ControllerButtons.Spoken(PadButton.Cross), ControllerButtons.Spoken(PadButton.Circle));

    /// <summary>A picker choice became a slice now IN HAND — the phase changed under the user, so say what
    /// they're holding and how to place it.</summary>
    private void AnnounceCarryingNew(string label)
    {
        _editArmed = -1;
        _announcer.Announce(Loc.F(UiText.Narration.Added, Loc.DefaultLabel(label), CarryHint()), AnnouncementKind.Result);
    }

    // ── Edit-mode "Add → Launch → Installed Game": pick the game from the Game Grid ──────────────
    // The wheel stays in edit mode underneath (just hidden); the grid is shown over it. ✕ picks a game
    // → resume edit carrying its slice; ○ cancels back to edit. △ assign / Fn assign are disabled here.

    private async void BeginGamePickForEdit()
    {
        try
        {
            if (_overlay is null) return;
            _pickerStack.Clear();
            _overlay.EndPicker(); _overlay.SetSliceMaterial(_config.Current.System.SliceMaterial);          // close the picker radial (edit state stays alive in the wheel)
            _editPickGame = true;
            await ShowBrowserAsync();      // shows the grid, hides the wheel — does NOT dismiss the overlay
            // A controller drop during the (possibly slow) storefront scan runs OnControllerLost, which
            // clears _editPickGame + tears down the edit session — but the grid is only NOW showing, so it
            // wasn't _browserOpen yet when OnControllerLost checked. Don't leave a grid stranded over a
            // gone edit session: if the pick flag was cleared mid-open, close the grid back down.
            if (!_editPickGame) { CloseGameBrowser(); return; }
        }
        catch (Exception ex)   // async void: a storefront-scan failure must not escape to the dispatcher
        {
            Trace.WriteLine($"[Grid] edit pick open failed: {ex.Message}");
            if (_editPickGame) EndGamePickBrowser();   // back to the suspended edit wheel
        }
    }

    private async void PickGameForEdit(InstalledGame game)
    {
        if (!_editPickGame || _overlay is null) return;
        try
        {
            var slice = await BuildGameSlice(game).ConfigureAwait(true);
            // Re-check after the await (the logo fetch can take seconds on first run): the user may have
            // cancelled with ○ — EndGamePickBrowser already ran, so committing now would double-teardown
            // (dropping controller exclusivity mid-edit) and pop the cancelled game into their hand.
            if (!_editPickGame || _overlay is null) return;
            PopulateIcons([slice]);
            EndGamePickBrowser();
            _overlay.EditAddAndCarry(slice);   // back in edit, carrying the chosen game to place
            AnnounceCarryingNew(slice.Label);
        }
        catch (Exception ex)   // async void: a failed slice build must not escape to the dispatcher
        {
            Trace.WriteLine($"[Grid] game pick failed: {ex.Message}");
            if (_editPickGame) EndGamePickBrowser();
        }
    }

    private void CancelGamePickForEdit()
    {
        if (!_editPickGame) return;
        EndGamePickBrowser();   // back to edit (Selecting), nothing added
        _editArmed = -1;
        _announcer.Announce(Loc.T(UiText.Narration.GamePickCancelled), AnnouncementKind.Context);
    }

    /// <summary>Tear the game-pick grid down and restore the suspended edit wheel.</summary>
    private void EndGamePickBrowser()
    {
        _editPickGame = false;
        _browserOpen  = false;
        ResetBrowserStick();
        UnregisterBrowserHotkeys();
        _overlay?.SetClickable(false);
        _overlay?.HideGames();
        _overlay?.ResumeWheelLayer(_config.Current.System.FadeMs);   // wheel reappears, still in edit
        UpdateInputCapture();
    }

    private void EditPickerBack()
    {
        if (_overlay is null) return;
        int ms = _config.Current.System.FadeMs;
        if (_pickerStack.Count > 1)
            _overlay.PickerDrillOut(ms, () => { _pickerStack.Pop(); ShowPickerLevel(grow: false); });   // shrink, then parent fades in
        else
            _overlay.PickerDrillOut(ms, () =>
            {
                _pickerStack.Clear(); _overlay!.EndPicker();
                // Restore the user's material. The picker deliberately renders in a FLAT variant
                // (ShowPickerLevel), and every OTHER way out of it pairs EndPicker with this line — the
                // two add paths and the game-pick hand-off. Without it, backing out of Add leaves
                // the user's own wheel drawn in Flat Dark / Flat Light until the wheel is dismissed.
                // EndPicker and the material restore belong together.
                _overlay!.SetSliceMaterial(_config.Current.System.SliceMaterial);
                _overlay!.PickerFadeIn(FadeInMs(ms));
                _editArmed = -1;
                // Leaving the picker with nothing added lands back in edit — say which surface owns input now.
                _announcer.Announce(Loc.T(UiText.Narration.AddCancelled), AnnouncementKind.Context);
            });
    }

    /// <summary>Leave edit mode: persist the final slices and fade the wheel out. Idempotent — a stacked
    /// call (second Fn press during the settle-hold, or the deferred AnnounceWheelEmptied racing a user
    /// exit) finds EditMode already false and no-ops instead of double-running EndEdit + persist.</summary>
    private void ExitEdit()
    {
        if (_overlay is null || !Editing) return;
        // Hold the wheel open for an in-flight placement settle (landing/reflow) so the user sees the
        // slice land before it despawns. One deferral is enough — the settle clocks expire by LandingMs.
        if (_overlay.EditSettling)
        {
            var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(WheelStateMachine.LandingMs) };
            hold.Tick += (_, _) => { hold.Stop(); ExitEdit(); };
            hold.Start();
            return;
        }
        var final = _overlay.EditCurrentSlices;
        if (!PersistWheel(_activeWheel, final)) return; // Keep the working set and editor available for retry.
        _overlay.EndEdit();
        _activeSlices = final;
        SoftReleaseInput();
        _overlay.Reset();
        _overlay.HideScrim(_config.Current.System.FadeMs);
        _overlay.FadeOutCancel();
        // Context (not Result): it also drops any pending focus announcement from the session just ended.
        _announcer.Announce(Loc.P(UiText.Narration.EditSavedOne, UiText.Narration.EditSavedOther, final.Length),
                            AnnouncementKind.Context);
        _editArmed = -1;
        _deleteHeldTarget = false;
    }

    private bool _wheelEmptied;   // guards the final-slice-removed handling from re-firing mid-teardown

    /// <summary>Persist the live edit working-set — fired on every structural change (live save).</summary>
    private void PersistEdited()
    {
        if (_overlay is null || !Editing) return;
        var slices = _overlay.EditCurrentSlices;
        if (!PersistWheel(_activeWheel, slices)) return;

        // A hold-□ delete completes on the state machine's own timer, so this structural callback is the
        // only place it surfaces. Clearing the flag first means the □ release can't also say "cancelled".
        if (_deleteHeldTarget)
        {
            _deleteHeldTarget = false;
            _announcer.Announce(Loc.P(UiText.Narration.DeletedLeftOne, UiText.Narration.DeletedLeftOther, slices.Length, Loc.DefaultLabel(_deleteHeldLabel)),
                                AnnouncementKind.Result);
            _editArmed = -1;   // focus lands on a different slice after the removal
        }

        // Removing the last slice empties the wheel: announce it, close the (now-empty) editor, and — if
        // BOTH wheels are now empty — open Settings so the user isn't stranded with no configured wheel.
        // Deferred so we don't tear the overlay down from inside its own render-timer structural callback.
        if (slices.Length == 0 && !_wheelEmptied)
        {
            _wheelEmptied = true;
            var emptied = _activeWheel;
            Dispatcher.BeginInvoke(new Action(() => AnnounceWheelEmptied(emptied)));
        }
    }

    /// <summary>The active wheel just lost its last slice. Close the empty editor, show a "wheel removed"
    /// status toast, and open Settings if both wheels are now empty (after the toast). Already persisted.</summary>
    private void AnnounceWheelEmptied(ActiveWheel emptied)
    {
        if (_overlay is null) return;
        // This runs DEFERRED — the user may have exited edit themselves in the gap (Fn/○/stick-click).
        // Only tear the editor down if it's still up; the toast + both-empty check below still apply.
        if (Editing)
        {
            // Close the empty editor immediately (mirrors ExitEdit's teardown minus the settle-hold + re-persist).
            _activeSlices = _overlay.EndEdit();
            SoftReleaseInput();
            _overlay.Reset();
            _overlay.HideScrim(_config.Current.System.FadeMs);
            _overlay.FadeOutCancel();
        }

        var scr = NativeMethods.PrimaryScreenDips();
        ShowStatusToast(Loc.T(emptied == ActiveWheel.A ? UiText.Toasts.LeftWheel : UiText.Toasts.RightWheel), Loc.T(UiText.Toasts.Removed),
                        scr.X + scr.Width / 2.0, scr.Y + scr.Height / 2.0);
        _editArmed = -1;
        bool bothEmpty = _config.Current.WheelA.Length == 0 && _config.Current.WheelB.Length == 0;
        // The last slice went: the editor closed itself, and an empty wheel acts as a DISABLED side — so
        // say what just became true of the side, not only that a slice was removed.
        _announcer.Announce(
            Loc.F(UiText.Narration.WheelEmptied, Loc.T(emptied == ActiveWheel.A ? UiText.Narration.Left : UiText.Narration.Right))
            + (bothEmpty ? Loc.T(UiText.Narration.BothEmptySuffix) : ""),
            AnnouncementKind.Context);

        if (bothEmpty)
        {
            var open = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };   // let the toast show first
            open.Tick += (_, _) => { open.Stop(); OpenSettings(); };
            open.Start();
        }
    }

    /// <summary>Write one wheel's slices to config, leaving the other wheel + settings untouched — except
    /// for slice THICKNESS, which <see cref="SliceThicknessRule"/> may move here: adding a 9th slice in the
    /// in-wheel editor has to demote Thick (and deleting back down has to restore it) exactly as the
    /// Settings save does, or the rule would depend on which editor you happened to use.</summary>
    private bool PersistWheel(ActiveWheel wheel, WheelSlice[] slices)
    {
        var cur = _config.Current;
        var wheelA = wheel == ActiveWheel.A ? slices : cur.WheelA;
        var wheelB = wheel == ActiveWheel.B ? slices : cur.WheelB;
        var newSys = SliceThicknessRule.Apply(cur.System, wheelA.Length, wheelB.Length);
        if (!TryWriteConfig(new AppConfig
        {
            WheelA       = wheelA,
            WheelB       = wheelB,
            System       = newSys,
            ActionColors = cur.ActionColors,
            ActionIcons  = cur.ActionIcons,
            CustomColors = cur.CustomColors,
        }))
        {
            _pendingWheelEdits[wheel] = slices.ToArray();
            return false;
        }
        _pendingWheelEdits.Remove(wheel);
        // Config reload leaves the editor's working set alone; apply committed thickness directly.
        if (newSys.SliceThickness != cur.System.SliceThickness)
            _overlay?.SetSliceThickness(newSys.SliceThickness);
        return true;
    }

    /// <summary>Step the wheel material to the next one in <see cref="Materials.All"/> and persist it.
    /// Sound theme moves with it, the same three-field write the Customize tiles do.</summary>
    private void CycleSliceMaterial()
    {
        var cur = _config.Current;
        var token = Materials.All[(Array.IndexOf(Materials.All, Materials.Normalize(cur.System.SliceMaterial)) + 1)
                                  % Materials.All.Length];
        var newSys = cur.System with
        {
            SliceMaterial   = token,
            HeldSliceMaterial = null, HeldGameGridMaterial = null,
            GameGridMaterial = token,
            SoundTheme      = "material",   // a material change snaps the sound pick back to follow-the-material
        };
        if (!TryWriteConfig(new AppConfig
        {
            WheelA       = cur.WheelA,
            WheelB       = cur.WheelB,
            System       = newSys,
            ActionColors = cur.ActionColors,
            ActionIcons  = cur.ActionIcons,
            CustomColors = cur.CustomColors,
        })) return;
        // Push to the renderer directly rather than waiting on the Reloaded handler: the whole point is that
        // the wheel you are looking at changes under you, and Reloaded early-returns while a wheel is open.
        SetMaterialFlags(token);
        // Glyph tints are BAKED into slice.Icon, so the open wheel needs a re-bake in the same beat as the
        // material swap — without it the wedges change and the icons keep the old material's variant.
        if (_activeSlices is { } open) PopulateIcons(open);
        _overlay?.SetSliceMaterial(token);
        _overlay?.SetGamesMaterial(token);
        Sfx.Theme = Sfx.ResolveSet(newSys.SoundTheme, token);   // heard immediately, same reason as above
        _announcer.Announce(Loc.F(UiText.Narration.Material, token.Replace('-', ' ')), AnnouncementKind.Context);
    }

    /// <summary>Find a direct storefront launch URL for a Playnite-sourced game by matching it
    /// (storefront + normalised name) against GameLibrary.Scan(), which produces direct URLs for
    /// every store it finds. Null if no confident match.</summary>
    private static string? ResolveScanUrl(InstalledGame game)
    {
        string key = GameLibrary.MatchKey(game);
        foreach (var g in GameLibrary.Scan())
            if (GameLibrary.MatchKey(g) == key)
                return g.LaunchUrl;
        return null;
    }

    /// <summary>The wheel's own keyboard hotkeys (volume ▲▼ + Esc). Registered while a wheel is on screen.
    /// Split out so the Game Grid can take the keys over — see <see cref="RegisterBrowserHotkeys"/>.</summary>
    private void RegisterWheelHotkeys()
    {
        NativeMethods.RegisterHotKey(_hotkeySource!.Handle, HkVolumeUp,   NativeMethods.MOD_NONE, NativeMethods.VK_UP);
        NativeMethods.RegisterHotKey(_hotkeySource.Handle,  HkVolumeDown, NativeMethods.MOD_NONE, NativeMethods.VK_DOWN);
        NativeMethods.RegisterHotKey(_hotkeySource.Handle,  HkWheelBack,  NativeMethods.MOD_NONE, NativeMethods.VK_ESCAPE);
    }

    private void UnregisterWheelHotkeys()
    {
        NativeMethods.UnregisterHotKey(_hotkeySource!.Handle, HkVolumeUp);
        NativeMethods.UnregisterHotKey(_hotkeySource.Handle,  HkVolumeDown);
        NativeMethods.UnregisterHotKey(_hotkeySource.Handle,  HkWheelBack);
    }

    /// <summary>Grid keyboard hotkeys. RegisterHotKey is per (window, key), so a key already held by the
    /// WHEEL's set can't be claimed here — and the grid opened from the in-wheel editor leaves that wheel
    /// alive underneath. The wheel owns ▲▼ (volume) and Esc, which are exactly the grid's nav-up/down and
    /// back keys, so those three silently failed to register and keyboard nav did nothing in pick mode.
    /// Dropping the wheel's set first is what makes them work; <see cref="ReleaseBrowserHotkeys"/> hands
    /// them back if a wheel is still up.</summary>
    private void RegisterBrowserHotkeys()
    {
        UnregisterWheelHotkeys();
        var h = _hotkeySource!.Handle;
        NativeMethods.RegisterHotKey(h, HkNavUp,    NativeMethods.MOD_NONE, NativeMethods.VK_UP);
        NativeMethods.RegisterHotKey(h, HkNavDown,  NativeMethods.MOD_NONE, NativeMethods.VK_DOWN);
        NativeMethods.RegisterHotKey(h, HkNavLeft,  NativeMethods.MOD_NONE, NativeMethods.VK_LEFT);
        NativeMethods.RegisterHotKey(h, HkNavRight, NativeMethods.MOD_NONE, NativeMethods.VK_RIGHT);
        NativeMethods.RegisterHotKey(h, HkLaunch,     NativeMethods.MOD_NONE, NativeMethods.VK_RETURN);
        RegisterBackKey("Grid");
        NativeMethods.RegisterHotKey(h, HkFilterPrev, NativeMethods.MOD_NONE, NativeMethods.VK_PRIOR);   // PageUp
        NativeMethods.RegisterHotKey(h, HkFilterNext, NativeMethods.MOD_NONE, NativeMethods.VK_NEXT);    // PageDown
    }

    private void UnregisterBrowserHotkeys()
    {
        var h = _hotkeySource!.Handle;
        foreach (var id in new[] { HkNavUp, HkNavDown, HkNavLeft, HkNavRight, HkLaunch, HkFilterPrev, HkFilterNext })
            NativeMethods.UnregisterHotKey(h, id);
        UnregisterBackKey();
        // Give ▲▼/Esc back to the wheel if one is still on screen (the edit-mode game pick keeps its wheel
        // suspended underneath the grid, and returns to it).
        if (_overlayVisible) RegisterWheelHotkeys();
    }

    /// <summary>Set the on-screen button glyphs (✕○□△ vs A/B/X/Y) from the Advanced "Button icons"
    /// override, or AUTO from the detected controller: Xbox glyphs for an XInput/Xbox-layout pad,
    /// PlayStation otherwise. A third-party pad in a Bluetooth/DS4-compatible mode is indistinguishable
    /// from a real DualShock, so AUTO shows PlayStation for it — the override is how the user forces Xbox
    /// (like Steam). Called at startup, on config reload, and on controller connect (kind may change).</summary>
    private void ApplyGlyphSet() =>
        ControllerButtons.Set = (_config.Current.System.ButtonGlyphs ?? "auto") switch
        {
            "xbox"        => ControllerGlyphSet.Xbox,
            "playstation" => ControllerGlyphSet.PlayStation,
            // "Best guess": PlayStation shapes only when a pad is actually present AND identified as Sony
            // (the raw-HID profiles are Sony VID-matched, so that IS the cue). Anything else — an Xbox pad,
            // or no pad at all — gets Xbox letters, the safer default for an unknown controller.
            // ⚠ CustomizeEditorControl.RefreshGlyphTiles previews this; change both together.
            _             => _controllerConnected && DetectedControllerKind
                                     is ControllerKind.DualSenseEdge or ControllerKind.PlayStationOther
                                 ? ControllerGlyphSet.PlayStation : ControllerGlyphSet.Xbox,
        };

    /// <summary>Push the current trigger settings to the interpreter + the Fn-handler gates. Called at
    /// startup and on every config reload, so switching gesture(s)/activation applies live. Several
    /// gestures can be enabled at once — the open wheel's toggle-vs-hold is decided per-invoke in
    /// <see cref="ToggleStyleFor"/>, so no single global toggle flag is set here.</summary>
    private void ApplyTriggerConfig()
    {
        var sys   = _config.Current.System;
        var modes = sys.TriggerModesFor(DetectedControllerKind);
        bool toggle = sys.TriggerActivation == "toggle";
        _fnOpensWheels = modes.Contains(TriggerModes.FnButtons);
        // The Fn path fires on release only in hold activation (Fn is never the touchpad); the interpreter
        // gets the same for its chord modes. Touchpad is toggle-style regardless, handled in EvalTouch.
        _holdFires = !toggle;
        _triggerInterpreter?.Configure(modes, holdFires: !toggle, toggleStyle: toggle);
    }

    /// <summary>On connect, notify the user when the controller KIND changed (a mid-session swap, or a
    /// different pad than last run) or is one never seen before — so the silent trigger-gesture + glyph
    /// switch that just happened isn't a mystery. A passive corner card names the pad and its summon
    /// gesture; a never-seen kind's card is ALSO clickable and opens setup (its live trigger
    /// practice). Kind is the unit of comparison on purpose — a device's instance id is unreliable across
    /// a BT⇄USB transport change, so a same-kind reconnect/flap deliberately says nothing.</summary>
    private void AnnounceControllerKind()
    {
        // The wizard owns the first-controller experience while it's open — just track the kind so we don't
        // re-announce the same pad the moment onboarding closes.
        if (_onboardingWindow is { IsLoaded: true }) { _activeKind = DetectedControllerKind; return; }

        var kind = DetectedControllerKind;
        var sys  = _config.Current.System;

        // First run of this feature (nothing ever recorded): silently adopt the attached pad as the baseline
        // so an existing user's daily controller isn't announced as "new".
        if (_activeKind is null && sys.LastControllerKind is null && sys.KnownControllerKinds.Count == 0)
        {
            _activeKind = kind;
            WriteSystem(sys with { LastControllerKind = kind.ToString(), KnownControllerKinds = [kind.ToString()] });
            return;
        }

        var prior      = _activeKind ?? ParseKind(sys.LastControllerKind);
        bool changed   = prior != kind;                                                   // swap / different-than-last-run
        bool firstSeen = !sys.KnownControllerKinds.Contains(kind.ToString(), StringComparer.OrdinalIgnoreCase);
        _activeKind = kind;
        if (!changed && !firstSeen) return;   // same controller as before + already known → nothing to say

        // Persist the change once. A same-kind reconnect/flap can't reach here, so there's no write storm.
        var known = new List<string>(sys.KnownControllerKinds);
        if (firstSeen) known.Add(kind.ToString());
        WriteSystem(sys with { LastControllerKind = kind.ToString(), KnownControllerKinds = known });

        var name    = FriendlyKind(kind);
        var gesture = TriggerModes.DescribeSet(sys.TriggerModesFor(kind), kind);

        // Self-drawn corner card, tray or no tray (a Windows notification must never be the only
        // channel — the portable ZIP has no shortcut/AUMID). Info tier: a controller change must never
        // evict a live safety warning. A never-seen kind gets a CLICKABLE card → run setup for it.
        if (firstSeen)
            ShowCornerToast(Loc.F(UiText.Toasts.NewController, name),
                Loc.F(UiText.Toasts.NewControllerBody, gesture),
                NoticeTier.Info, onClick: RunOnboarding, holdMs: 8000);
        else
            ShowCornerToast(Loc.T(UiText.Toasts.ControllerChanged),
                Loc.F(UiText.Toasts.ControllerChangedBody, name, gesture), NoticeTier.Info);
    }

    private static ControllerKind? ParseKind(string? s) =>
        Enum.TryParse<ControllerKind>(s, out var k) ? k : null;

    /// <summary>The open pad's transport for the user-facing readouts. The raw-HID backend answers for the
    /// Sony family from the report length it settled on at open; XInput exposes nothing at all, so an Xbox
    /// pad is resolved here from whether its XUSB devnode EXISTS: present = wired or dongle, absent =
    /// Bluetooth, because a BT Xbox pad enumerates under HIDClass via <c>xinputhid.sys</c> rather than the
    /// Xbox composite classes <see cref="XboxDeviceTree"/> scans.
    /// <para>Worth surfacing because the transports isolate differently: BT goes through the HID-entry
    /// cloak gated on the observed-isolation probe (best-effort when the probe can't confirm), so
    /// "(Bluetooth)" in the readout is the pointer to which path — and which failure mode — applies.</para></summary>
    private ControllerTransport PadTransport()
    {
        if (!_controllerConnected) return ControllerTransport.Unknown;
        if (DetectedControllerKind != ControllerKind.Xbox) return _controller.Transport;
        return XboxDeviceTree.GetPhysicalXboxInstanceIds().Count > 0
            ? ControllerTransport.Usb
            : ControllerTransport.Bluetooth;
    }

    /// <summary>User-facing name for a controller kind. "Controller" for a generic PlayStation-HID pad: a
    /// BT Xbox-compatible pad in PS mode is indistinguishable from a real DualShock, so we don't claim
    /// "PlayStation". Internal (not private) so <see cref="SystemEditorControl"/>'s "Current Controller"
    /// readout can reuse the same mapping instead of duplicating it.
    ///
    /// The bare "Controller" would tell the user nothing they can act on, so <paramref name="transport"/>
    /// qualifies it whenever the backend knows one ("Bluetooth Controller" / "USB Controller").
    ///
    /// <para>A NAMED pad is qualified too — "Xbox controller (Bluetooth)". The model already identifies the
    /// pad, but the transport is the thing that decides whether it can be input-isolated at all, so a user
    /// looking at a wheel that isn't isolating has the reason in front of them instead of in the trace log.
    /// Callers that don't know the transport pass nothing and get the bare name, unchanged.</para></summary>
    internal static string FriendlyKind(ControllerKind kind,
                                        ControllerTransport transport = ControllerTransport.Unknown)
    {
        string? named = kind switch
        {
            ControllerKind.DualSenseEdge => "DualSense Edge",
            ControllerKind.Xbox          => Loc.T(UiText.Names.XboxController),
            // The Sony-family bucket holds a real DualSense, a real DS4, AND every third-party pad in
            // DS4-compatible mode — those present Sony's own VID/PID (docs/CONTROLLERS.md ▸ Pad
            // identity), so the pad's model can never be asserted. The open profile does know which
            // LAYOUT is being parsed, so name that and hedge it: "DualShock 4 (or compatible)" is true
            // of a genuine DS4 and of a clone imitating one, where a bare "DualShock 4" would
            // confidently mis-name a large share of the market.
            // A profile on its vendor's OWN un-cloned VID/PID (ControllerProfile.HedgeName = false)
            // identifies the model outright and is named plainly.
            _ => (System.Windows.Application.Current as App)?._controller is { } reader
                 && !string.IsNullOrWhiteSpace(reader.ProfileName)
                     ? reader.ProfileNameHedged ? Loc.F(UiText.Names.OrCompatible, reader.ProfileName) : reader.ProfileName
                     : null,
        };
        if (named is null)
            return transport switch
            {
                ControllerTransport.Bluetooth => Loc.T(UiText.Names.BluetoothController),
                ControllerTransport.Usb       => Loc.T(UiText.Names.UsbController),
                _                             => Loc.T(UiText.Names.Controller),
            };
        return transport switch
        {
            ControllerTransport.Usb       => Loc.F(UiText.Names.Usb, named),
            ControllerTransport.Bluetooth => Loc.F(UiText.Names.Bluetooth, named),
            _                             => named,
        };
    }

    // ── Hotkey sink (hidden message window for the on-demand volume + Game-Grid hotkeys,
    //    and the PnP device-change notifications that drive capture re-evaluation) ───────────────

    private void CreateHotkeySink()
    {
        var p = new HwndSourceParameters("hotkey-sink")
        {
            WindowStyle         = 0x00800000,
            ExtendedWindowStyle = 0,
            Width = 0, Height = 0,
        };
        _hotkeySource = new HwndSource(p);
        _hotkeySource.AddHook(HotkeyWndProc);
        // No F1/F2/F3 registration — those keys stay free for games/apps. Volume + Game-Grid nav hotkeys
        // are registered on demand (see their Register* helpers), and use this same sink window.

        // Capture must re-evaluate when the DEVICE SET changes, not only on user gestures / config
        // reloads / the reader's connect event: a second Xbox-class pad arriving while a controller is
        // already connected raises no connect event, so the multi-pad guard otherwise runs minutes late
        // (on the next wheel open) and the new pad leaks into the game uncloaked the whole time.
        _deviceNotifyHandle = NativeMethods.RegisterForAllDeviceInterfaceNotifications(_hotkeySource.Handle);
        if (_deviceNotifyHandle == IntPtr.Zero)
            Trace.WriteLine("[Capture] device-change registration failed — hot-plug re-evaluation limited to DBT_DEVNODES_CHANGED broadcasts");
    }

    private IntPtr _deviceNotifyHandle;
    private DispatcherTimer? _deviceChangeDebounce;
    private readonly HashSet<Guid> _skippedIfaceGuids = new();

    private int  _deviceChangeNoops;         // capture passes since the last summary that changed nothing
    private long _deviceChangeNoopTracedAt;  // its own window - TraceThrottleMs (30s) is far too short here
    private const long DeviceChangeNoopTraceMs = 15 * 60_000;
    private long _deviceChangePendingSince;   // 0 = no window open; else first notification of this burst
    private const long DeviceChangeMaxWaitMs = 4_000;

    /// <summary>Everything a device-change pass could plausibly alter, as one comparable string, so the
    /// pass can log only when it actually DID something.</summary>
    private string CaptureSignature() =>
        $"cloaked={_cloakedIds.Count} pad={(_emulator.Active ? _emulator.PadType?.ToString() ?? "?" : "none")} "
        + $"isolated={_isolated} note='{_isolationNote}'";

    /// <summary>Debounced device-change → capture pass. Plug-in produces an enumeration storm (one
    /// message per interface of the composite), so the pass runs once, shortly after the LAST message —
    /// which also keeps it clear of the emulator's own slot-resolution window when the storm is our
    /// virtual pad connecting.</summary>
    private void OnDeviceSetChanged()
    {
        if (_config is null) return;   // pre-config launches have nothing to reconcile
        if (_deviceChangeDebounce is null)
        {
            _deviceChangeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _deviceChangeDebounce.Tick += (_, _) => RunDeviceChangePass();
        }
        // ⚠ The debounce MUST have a maximum wait. Restarting it on every notification starves the
        // pass outright when notifications arrive faster than the interval: a real box produced
        // DBT_DEVNODES_CHANGED about twice a second for minutes on end (368 notifications, largest
        // gap 1.075 s, against a 1.5 s interval), so the timer was re-armed forever and capture NEVER
        // re-evaluated — a second pad went unnoticed until the next wheel open, ~46 s, uncloaked the
        // whole time. Past the cap the pass runs at once and the next notification opens a fresh
        // window; keep the cap comfortably above the interval so ordinary plug storms still coalesce.
        long nowMs = Environment.TickCount64;
        if (_deviceChangePendingSince == 0) _deviceChangePendingSince = nowMs;
        _deviceChangeDebounce.Stop();
        if (nowMs - _deviceChangePendingSince >= DeviceChangeMaxWaitMs) RunDeviceChangePass();
        else _deviceChangeDebounce.Start();
    }

    /// <summary>The debounced capture re-evaluation. Runs on the UI thread from either the debounce
    /// tick or the max-wait cap in <see cref="OnDeviceSetChanged"/>.</summary>
    private void RunDeviceChangePass()
    {
        _deviceChangeDebounce?.Stop();
        _deviceChangePendingSince = 0;
        // The pass ALWAYS runs (hot-plug correctness, verified at G1); only the LOGGING is conditional.
        // Windows notifies us about every device: the registration is DEVICE_NOTIFY_ALL_INTERFACE_CLASSES
        // and DBT_DEVNODES_CHANGED is a machine-wide broadcast besides, so audio endpoints, Bluetooth
        // radio churn and USB selective suspend all reach this handler too — dozens of passes an hour
        // even with no pad attached, each a candidate trace line. So: log the passes that CHANGED
        // capture state, summarise the rest on a long window.
        var before = CaptureSignature();
        UpdateInputCapture();
        var after = CaptureSignature();
        if (before != after)
        {
            Trace.WriteLine($"[Capture] device set changed - {before} -> {after}");
            _deviceChangeNoops = 0;
            return;
        }
        _deviceChangeNoops++;
        long now = Environment.TickCount64;
        if (now - _deviceChangeNoopTracedAt < DeviceChangeNoopTraceMs) return;
        _deviceChangeNoopTracedAt = now;
        Trace.WriteLine($"[Capture] {_deviceChangeNoops} capture pass(es) changed nothing "
                        + "(unrelated device churn)");
        _deviceChangeNoops = 0;
    }

    private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if      (id == HkVolumeUp)  { AdjustVolume(+0.05f);         handled = true; }
            else if (id == HkVolumeDown){ AdjustVolume(-0.05f);         handled = true; }
            // Esc dismisses whatever wheel/editor is open, however it was summoned (Fn / chord / touchpad /
            // stick-click edit). Editing saves + exits (like ○ / Fn); a plain wheel cancels. The Game Grid's
            // Esc is HkBack (registered by the browser), so this only fires for a non-browser wheel.
            else if (id == HkWheelBack) { if (Editing) ExitEdit(); else if (_overlayVisible) CancelOverlay(); handled = true; }
            else if (id == HkNavUp)    { if (_browserOpen) NavBrowser(0, -1);    handled = true; }
            else if (id == HkNavDown)  { if (_browserOpen) NavBrowser(0, +1);    handled = true; }
            else if (id == HkNavLeft)  { if (_browserOpen) NavBrowser(-1, 0);    handled = true; }
            else if (id == HkNavRight) { if (_browserOpen) NavBrowser(+1, 0);    handled = true; }
            else if (id == HkLaunch)   { if (_browserOpen) LaunchSelectedGame(); handled = true; }
            // HkBack (Esc) is shared: whichever surface registered it owns it, and only one is ever up.
            else if (id == HkBack)     { SurfaceBack(); handled = true; }
            else if (id == HkFilterPrev) { if (_browserOpen) _overlay?.CycleLauncherFilter(-1); handled = true; }
            else if (id == HkFilterNext) { if (_browserOpen) _overlay?.CycleLauncherFilter(+1); handled = true; }
        }
        else if (msg == NativeMethods.WM_DEVICECHANGE)
        {
            int ev = wParam.ToInt32();
            // ⚠ DON'T filter these by interface class. Tried and REVERTED: skipping broadcasts whose
            // payload named a class other than HID/XUSB dropped a real Xbox 360 pad's arrival — the pad
            // announced itself under class 020BC73C-0DCA-4EE3-96D5-AB006ADA5938 alone, and capture did
            // not re-evaluate for 46 s (until the next wheel open). DBT_DEVNODES_CHANGED did NOT cover
            // the gap, so it is not the safety net it looks like. Every broadcast runs the pass; the
            // class is logged once per distinct GUID as diagnostics only.
            if (ev is NativeMethods.DBT_DEVICEARRIVAL
                   or NativeMethods.DBT_DEVICEREMOVECOMPLETE
                   or NativeMethods.DBT_DEVNODES_CHANGED)
            {
                if (ev != NativeMethods.DBT_DEVNODES_CHANGED
                    && !NativeMethods.IsPadInterfaceBroadcast(lParam, out var cls)
                    && _skippedIfaceGuids.Add(cls))
                    Trace.WriteLine($"[Capture] non-pad interface class {cls} (pass runs anyway)");
                // Checked BEFORE the pass, which may release the ids it matches against.
                if (ev == NativeMethods.DBT_DEVICEREMOVECOMPLETE
                    && NativeMethods.TryGetInterfacePath(lParam, out var link)
                    && HidInstanceId.FromInterfacePath(link) is { } goneId
                    && _cloakedIds.Contains(goneId))
                    OnCloakedDevnodeDeparted(goneId);
                OnDeviceSetChanged();
            }
        }
        else if (msg == NativeMethods.WM_CLOSE)
        {
            ExitApp();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ── Volume ────────────────────────────────────────────────────────────────

    private void AdjustVolume(float delta)
    {
        // MasterVolumeLevelScalar writes are silently ignored on this device;
        // send actual Windows media-key events instead — same as a keyboard volume key.
        byte vk = delta > 0 ? NativeMethods.VK_VOLUME_UP : NativeMethods.VK_VOLUME_DOWN;
        NativeMethods.keybd_event(vk, 0, 0, IntPtr.Zero);
        NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_KEYUP, IntPtr.Zero);

        // Read back current level for the scrubber overlay
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            float vol = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            _overlay?.SetScrubber(true, vol, "VolumeHigh");
            RestartVolumeHideTimer();
            AnnounceScrub(Loc.T(UiText.Narration.ScrubVolume), vol);
        }
        // Throttled: this runs on a ~80 ms D-pad auto-repeat, so a failing endpoint would flood the log.
        // The keypresses above still landed — only the scrubber readout and its narration are lost.
        catch (Exception ex)
        {
            TraceThrottled("volume-readback",
                           $"[Action] volume read-back failed: {ex.Message} — the scrubber and its narration " +
                           "will stay silent (the volume keys themselves still fired)");
        }
    }

    /// <summary>Narrate a scrubber level ("Volume 55 percent"). Scrub-kind on purpose: a held D-pad
    /// auto-repeats every ~80 ms, and the Announcer's longer scrub debounce collapses the whole sweep into
    /// the value it LANDS on — announcing each step would be unusable.
    /// <para>The number spoken is the level the system is ACTUALLY at, not one snapped to the step size: a
    /// listener is asking where the volume is, and an accurate answer beats a tidy one. Don't reintroduce
    /// rounding here.</para>
    /// <para>The <c>volume-set</c> action and the D-pad scrub share this: neither returns an ActionStatus
    /// (the level lives in the momentary hub slot, not the readout), so this is their only voice.</para></summary>
    private void AnnounceScrub(string what, float level01)
    {
        if (!_announcer.Enabled) return;
        int pct = (int)Math.Round(Math.Clamp(level01, 0f, 1f) * 100);
        _announcer.Announce(Loc.F(UiText.Narration.ScrubPercent, what, pct), AnnouncementKind.Scrub);
    }

    private void RestartVolumeHideTimer()
    {
        if (_volumeHideTimer is null)
        {
            _volumeHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _volumeHideTimer.Tick += (_, _) =>
            {
                _volumeHideTimer.Stop();
                _overlay?.SetScrubber(false, 0);
            };
        }
        _volumeHideTimer.Stop();
        _volumeHideTimer.Start();
    }

    // D-pad up/down volume AUTO-REPEAT. DPadChanged is edge-triggered (fires once per nibble change), so a
    // HELD direction would only bump the volume once. Instead: step once on press, then after a short delay
    // repeat quickly while held (classic key-repeat). _dpadVolDir: +1 up, -1 down, 0 = released/none.
    private DispatcherTimer? _dpadVolTimer;
    private int _dpadVolDir;

    private void StartDpadVolumeRepeat()
    {
        _dpadVolTimer ??= CreateDpadVolTimer();
        _dpadVolTimer.Interval = TimeSpan.FromMilliseconds(_config.Current.System.DpadVolumeRepeatDelayMs);   // hold delay (Developer Settings)
        _dpadVolTimer.Stop();
        _dpadVolTimer.Start();
    }

    private DispatcherTimer CreateDpadVolTimer()
    {
        var t = new DispatcherTimer();
        t.Tick += (_, _) =>
        {
            // Repeat only while the conditions that started it still hold; otherwise self-stop (the D-pad
            // release fires DPadChanged → StopDpadVolumeRepeat, but a wheel close mid-hold sends no event).
            if (_dpadVolDir == 0 || !_overlayVisible || Editing || _browserOpen
                || !_config.Current.System.DpadVolumeWhileOpen)
            { StopDpadVolumeRepeat(); return; }
            t.Interval = TimeSpan.FromMilliseconds(_config.Current.System.DpadVolumeRepeatIntervalMs);   // repeat rate (Developer Settings)
            AdjustVolume(_dpadVolDir * 0.05f);
        };
        return t;
    }

    private void StopDpadVolumeRepeat()
    {
        _dpadVolDir = 0;
        _dpadVolTimer?.Stop();
    }

    // ── Volume mixer balance (D-pad Left/Right while a wheel is open) ──────────
    // Mirrors the D-pad volume scrub above: edge-triggered DPadChanged steps once on press then arms an
    // auto-repeat timer (reusing the same Developer-Settings delay/interval knobs). The "active" mixer pair
    // is whichever volume-mixer slice (first found, Wheel A then Wheel B) has BOTH output devices configured;
    // with none configured, D-pad L/R is a no-op (existing behavior preserved).

    /// <summary>The active volume-mixer device pair, or null if no configured volume-mixer slice exists on
    /// either wheel. Cached; refreshed on config reload / at startup.</summary>
    private AppVolumeMixer.Pair? _mixPair;   // live app pair; re-resolved at each mix-gesture start
    private int _mixBalance = 50;   // 0 = full-left device, 100 = full-right device; seeded from SystemConfig.MixBalance

    // The mixer is a baked-in feature (Settings ▸ Advanced ▸ Volume Mixer), not a slice — its pair comes
    // from SystemConfig.MixEnabled + MixDeviceA/B.

    /// <summary>Re-seed the persisted balance on startup/config reload. The PAIR itself is live
    /// state, re-resolved at each mix-gesture start (see the D-pad handler) — never configured.</summary>
    private void RefreshMixPair()
    {
        _mixBalance = Math.Clamp(_config.Current.System.MixBalance, 0, 100);
    }

    /// <summary>Resolve the live app pair for a fresh mix gesture (game ↔ chat/music/browser). Shows
    /// the no-apps message when there's nothing to mix. Returns whether a pair is in hand.</summary>
    private bool BeginMixGesture()
    {
        _mixPair = AppVolumeMixer.ResolvePair();
        if (_mixPair is null)
        {
            _overlay?.ShowMixIndicator(0.5, Loc.T(UiText.Overlay.MixNoGame), Loc.T(UiText.Overlay.MixPlayingAudio), noDevice: true);
            _announcer.Announce(Loc.T(UiText.Narration.NothingMixing), AnnouncementKind.Result);
            // The success path's StepMixBalance restarts the hide timer on every step; this failure path
            // never steps, so without its own restart the "No Device" readout latches FOREVER — nothing
            // else hides the indicator, and it would re-draw under every wheel from then on, whatever the
            // D-pad ◀ ▶ mode.
            RestartMixHideTimer();
        }
        return _mixPair is not null;
    }

    private void StepMixBalance(int delta)
    {
        if (_mixPair is not { } pair) return;
        _mixBalance = Math.Clamp(_mixBalance + delta, 0, 100);

        // PS5-style balance: both apps full volume at centre; pushing right attenuates the LEFT app
        // (favors the RIGHT app) and vice versa. Applied to the apps' audio SESSIONS, not devices.
        var (okL, okR) = AppVolumeMixer.SetBalance(pair, _mixBalance);

        _overlay?.ShowMixIndicator(_mixBalance / 100.0, pair.LeftLabel, pair.RightLabel, noDevice: !okL && !okR);
        RestartMixHideTimer();
        RestartMixWriteTimer();   // debounced persist — don't hammer config.json while scrubbing
        // The indicator is a two-ended bar with the app names at its ends; spoken, that has to become a
        // direction. Centre is the meaningful landmark (both apps at full volume), so name it.
        if (_announcer.Enabled)
        {
            string where = _mixBalance == 50 ? Loc.T(UiText.Narration.MixCentred)
                         : _mixBalance < 50   ? Loc.F(UiText.Narration.MixToward, 50 - _mixBalance, pair.LeftLabel)
                         : Loc.F(UiText.Narration.MixToward, _mixBalance - 50, pair.RightLabel);
            _announcer.Announce(!okL && !okR ? Loc.T(UiText.Narration.MixUnavailable) : where,
                                AnnouncementKind.Selection);
        }
    }

    private DispatcherTimer? _mixHideTimer;
    private void RestartMixHideTimer()
    {
        if (_mixHideTimer is null)
        {
            _mixHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _mixHideTimer.Tick += (_, _) =>
            {
                _mixHideTimer!.Stop();
                _overlay?.HideMixIndicator();
            };
        }
        _mixHideTimer.Stop();
        _mixHideTimer.Start();
    }

    private DispatcherTimer? _mixWriteTimer;
    private void RestartMixWriteTimer()
    {
        if (_mixWriteTimer is null)
        {
            _mixWriteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _mixWriteTimer.Tick += (_, _) =>
            {
                _mixWriteTimer!.Stop();
                WriteSystem(_config.Current.System with { MixBalance = _mixBalance });
            };
        }
        _mixWriteTimer.Stop();
        _mixWriteTimer.Start();
    }

    // D-pad Left/Right AUTO-REPEAT — same edge-triggered-then-repeat pattern as the volume scrub's
    // _dpadVolTimer. _dpadMixDir: +1 right, -1 left, 0 = released/none.
    private DispatcherTimer? _dpadMixTimer;
    private int _dpadMixDir;

    private void StartDpadMixRepeat()
    {
        _dpadMixTimer ??= CreateDpadMixTimer();
        _dpadMixTimer.Interval = TimeSpan.FromMilliseconds(_config.Current.System.DpadVolumeRepeatDelayMs);
        _dpadMixTimer.Stop();
        _dpadMixTimer.Start();
    }

    private DispatcherTimer CreateDpadMixTimer()
    {
        var t = new DispatcherTimer();
        t.Tick += (_, _) =>
        {
            if (_dpadMixDir == 0 || !_overlayVisible || Editing || _browserOpen || _mixPair is null)
            { StopDpadMixRepeat(); return; }
            t.Interval = TimeSpan.FromMilliseconds(_config.Current.System.DpadVolumeRepeatIntervalMs);
            StepMixBalance(_dpadMixDir * 5);
        };
        return t;
    }

    private void StopDpadMixRepeat()
    {
        _dpadMixDir = 0;
        _dpadMixTimer?.Stop();
    }

    // ── D-pad ◀▶ "mic" mode: scrub the microphone level ────────────────────────────────────────────
    // Shares the hub scrubber (and its 800 ms hide timer) with the D-pad ▲▼ speaker volume, captioned with
    // a "Microphone" glyph (the speaker gets "VolumeHigh") so the two readouts are distinguishable at a
    // glance from the couch. Auto-repeats like the speaker scrub.
    private DispatcherTimer? _dpadMicTimer;
    private int _dpadMicDir;

    /// <summary>One mic nudge (±5%). Silently no-ops with no capture device — showing a 0% ring for a
    /// mic that isn't there would read as "your mic is muted". Returns whether a device answered, so
    /// callers can stop repeating against a machine with no mic.</summary>
    private bool StepMicVolume(int dir)
    {
        if (_platform.AdjustMicVolume(dir * 0.05f) is not { } level) return false;
        _overlay?.SetScrubber(true, level, "Microphone");
        RestartVolumeHideTimer();
        AnnounceScrub(Loc.T(UiText.Narration.ScrubMicrophone), level);
        return true;
    }

    private void StartDpadMicRepeat()
    {
        _dpadMicTimer ??= CreateDpadMicTimer();
        _dpadMicTimer.Interval = TimeSpan.FromMilliseconds(_config.Current.System.DpadVolumeRepeatDelayMs);
        _dpadMicTimer.Stop();
        _dpadMicTimer.Start();
    }

    private DispatcherTimer CreateDpadMicTimer()
    {
        var t = new DispatcherTimer();
        t.Tick += (_, _) =>
        {
            if (_dpadMicDir == 0 || !_overlayVisible || Editing || _browserOpen)
            { StopDpadMicRepeat(); return; }
            t.Interval = TimeSpan.FromMilliseconds(_config.Current.System.DpadVolumeRepeatIntervalMs);
            if (!StepMicVolume(_dpadMicDir)) StopDpadMicRepeat();   // device vanished mid-hold — stop hammering
        };
        return t;
    }

    private void StopDpadMicRepeat()
    {
        _dpadMicDir = 0;
        _dpadMicTimer?.Stop();
    }

    // ── D-pad ◀▶ "switcher" mode: Alt-Tab forward / backward ───────────────────────────────────────
    // Alt must stay DOWN between taps or each press would just bounce between the top two windows
    // instead of advancing, so we hold it from the first press and release it when the wheel goes away
    // (ReleaseTaskSwitcher, called from SoftReleaseInput / the controller-drop cleanup / TearDown) —
    // releasing Alt is also what COMMITS the highlighted window, which is the intended feel: advance
    // with the D-pad, let go of the wheel to land on it. A stranded Alt-down would be a genuinely nasty
    // bug (every keystroke becomes a menu accelerator), hence the belt-and-braces release sites.
    private bool _altTabHeld;

    private void StepTaskSwitcher(int dir)
    {
        if (!_altTabHeld)
        {
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, IntPtr.Zero);
            _altTabHeld = true;
        }
        bool back = dir < 0;
        if (back) NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0, 0, IntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_TAB, 0, 0, IntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_TAB, 0, NativeMethods.KEYEVENTF_KEYUP, IntPtr.Zero);
        if (back) NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0, NativeMethods.KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    /// <summary>Let Alt up, committing whatever the switcher has highlighted. Idempotent.</summary>
    private void ReleaseTaskSwitcher()
    {
        if (!_altTabHeld) return;
        _altTabHeld = false;
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    // ── Icon population ───────────────────────────────────────────────────────

    /// <summary>True when the current wheel material is dark — built-in glyph/logo tints get
    /// brightness-inverted so they read on a dark slice. Set wherever the material is applied.</summary>
    private static bool _darkSlices;
    /// <summary>Which built-in glyph/logo tint set the current material wants: Dark (near-white, dark
    /// materials), Terra (the in-between chalk-pastel set), or Light (base defaults). Labels/hub ink stay
    /// on <see cref="_darkSlices"/>.</summary>
    private static ActionTint.TintSet _tintSet = ActionTint.TintSet.Light;
    private static bool IsDarkMaterial(string? m) => Materials.IsDark(m);   // single source: Core\Materials

    /// <summary>Sets both <see cref="_darkSlices"/> (drives label/hub ink) and <see cref="_tintSet"/>
    /// (drives built-in glyph/logo tint) from the current SliceMaterial. Call this instead of assigning
    /// _darkSlices directly whenever the material is applied.</summary>
    private static void SetMaterialFlags(string? material)
    {
        _darkSlices = IsDarkMaterial(material);
        _tintSet = ActionTint.TintSetFor(material);
        // Drop-in theme sounds follow the wheel material, like everything else about the look. Decoded
        // buffers are cached per path in SfxEngine, so a config reload re-applying the same theme is
        // dictionary lookups, not decodes. Null on any built-in material — the theme pair rules.
        Sfx.Custom = Materials.CustomFor(Materials.Normalize(material)) is { Sounds: not null } pkg
            ? new Sfx.CustomSounds(
                LoadPkgSound(pkg, "armed"), LoadPkgSound(pkg, "fired"),
                LoadPkgSound(pkg, "enableWheels"), LoadPkgSound(pkg, "disableWheels"))
            : null;
        static float[]? LoadPkgSound(MaterialPackage pkg, string evt) =>
            pkg.Sounds!.TryGetValue(evt, out var f)
                ? SfxEngine.LoadFile(System.IO.Path.Combine(pkg.DirPath, f)) : null;
    }

    // PopulateIcons runs on EVERY wheel open; PackIconHelper.FromName builds a fresh ImageSource each
    // call, which gives slice.Icon a new identity per open and defeats the renderer's per-ImageSource
    // bake caches (erosion masks, reactor blurred shadows — re-baked every summon). Memoize per
    // slice instance: skip the rebuild when the inputs (icon name/glyph + tint set + colour) are unchanged.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WheelSlice, string> IconKeyCache = new();

    /// <summary><paramref name="tintSet"/> overrides the material's own set. Only the Add picker passes it:
    /// that wheel always renders Flat Dark regardless of the user's material, so its glyphs have to bake
    /// with the Dark set or they carry the user's material variant onto a wheel that isn't that material.
    /// Slices bound for the user's real wheel must leave it null.</summary>
    private static void PopulateIcons(WheelSlice[] slices, ActionTint.TintSet? tintSet = null)
    {
        var set = tintSet ?? _tintSet;
        foreach (var slice in slices)
        {
            slice.IconIsCover = false;
            slice.LogoImage = string.IsNullOrWhiteSpace(slice.LogoPath) ? null : GameArt.LoadFromFile(slice.LogoPath);
            // Cover-art image file (e.g. a game added from the browser) wins over everything.
            if (slice.IconPath is not null && GameArt.LoadFromFile(slice.IconPath) is { } cover)
            {
                slice.Icon = cover;
                slice.IconIsCover = true;
                continue;
            }
            // Explicit icon name wins next — tinted subtly by action type.
            if (slice.IconName is not null)
            {
                var tint = ActionTint.DisplayColor(slice, set);
                string key = $"n|{slice.IconName}|{set}|{tint}";
                if (slice.Icon is not null && IconKeyCache.TryGetValue(slice, out var prev) && prev == key) continue;
                slice.Icon = PackIconHelper.FromName(slice.IconName, ActionTint.BrushFor(slice, set))
                             ?? MonoIcons.ForSlice(slice);
                IconKeyCache.AddOrUpdate(slice, key);
                continue;
            }
            // Exe path → extract icon from disk; else a per-type default Material glyph; else shape.
            if (slice.Action?.Type is "launch" && slice.Action.Path is not null)
            {
                slice.Icon = IconCache.Get(slice.Action.Path) ?? MonoIcons.ForSlice(slice);
                continue;
            }
            var glyph = ActionIcons.Resolve(slice.Action);
            {
                var tint = ActionTint.DisplayColor(slice, set);
                string key = $"g|{glyph}|{set}|{tint}";
                if (slice.Icon is not null && IconKeyCache.TryGetValue(slice, out var prev) && prev == key) continue;
                slice.Icon = (glyph is not null ? PackIconHelper.FromName(glyph, ActionTint.BrushFor(slice, set)) : null)
                             ?? MonoIcons.ForSlice(slice);
                IconKeyCache.AddOrUpdate(slice, key);
            }
        }
    }

    // ── Tray ──────────────────────────────────────────────────────────────────

    private void BuildTrayIcon()
    {
        Icon icon;
        try { icon = FlowerIcon.TrayIcon(); }          // flower mark, themed to the taskbar
        catch { icon = SystemIcons.Application; }

        var menu = new ContextMenuStrip { RightToLeft = Loc.IsRtl ? System.Windows.Forms.RightToLeft.Yes : System.Windows.Forms.RightToLeft.No };
        // "Test Wheel" lives inside each wheel editor tab (SettingsWindow.TestWheelHook), not here —
        // ToggleOverlay(ActiveWheel.A/B) is still gated on _wheelsEnabled there.
        // Enable/disable mirrors the controller chord; the label follows the live state.
        var wheelsItem = new ToolStripMenuItem(Loc.T(UiText.Tray.DisableWheels), null, (_, _) => HandleChord());
        menu.Items.Add(wheelsItem);
        // Anticheat passthru mode (capture profile) — a checkable, persistent global toggle. The label doubles
        // as the capture-state indicator ("what's touching my controller" at a glance).
        var safeItem = new ToolStripMenuItem(Loc.T(UiText.Tray.PassthruMode), null, (_, _) => ToggleSafeMode())
        { ToolTipText = Loc.T(UiText.Tray.PassthruTip) };
        menu.Items.Add(safeItem);
        menu.Opening += (_, _) =>
        {
            wheelsItem.Text = Loc.T(_wheelsEnabled ? UiText.Tray.DisableWheels : UiText.Tray.EnableWheels);
            safeItem.Checked = _safeMode;   // the manual global toggle only
            // Capture-state indicator: show when the Exceptions watcher has passthru mode engaged (the check
            // mark stays the MANUAL toggle — unchecking it won't end an auto engagement).
            safeItem.Text = _autoSafeMode
                ? Loc.F(UiText.Tray.PassthruAuto, _autoSafeModeApp ?? "")
                : _safeModeOffPending
                ? Loc.T(UiText.Tray.PassthruEndingItem)
                : Loc.T(UiText.Tray.PassthruMode);
        };
        // The controller/driver tools (Controller Setup, Run First-Run Setup, Install/Repair Drivers,
        // HID Diagnostics, Recover Controller) live in Settings ▸ Advanced ▸ Controller & drivers —
        // the tray stays a short everyday menu.
        menu.Items.Add(new ToolStripSeparator());
        // Game Grid: the same path a fired game-browser slice takes (ActionExecutor is
        // handed this exact delegate) — self-guarding against an already-open grid and a visible wheel.
        menu.Items.Add(Loc.T(UiText.Tray.GameGrid), null, (_, _) => OpenGameBrowser());
        menu.Items.Add(Loc.T(UiText.Tray.Settings), null, (_, _) => OpenSettings());
        menu.Items.Add(Loc.T(UiText.Tray.Help), null, (_, _) => OpenHelp());
        menu.Items.Add(Loc.T(UiText.Tray.About), null, (_, _) => OpenAbout());
        if (ReleaseGates.TrayShowInExplorer)
            menu.Items.Add(Loc.T(UiText.Tray.ShowInExplorer), null, (_, _) => ShowExeInExplorer());
        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem(Loc.T(UiText.Tray.StartWithWindows))
        {
            CheckOnClick = true,
            Checked      = StartupManager.IsEnabled,
        };
        startupItem.CheckedChanged += (_, _) => StartupManager.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T(UiText.Tray.Exit), null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon             = icon,
            Text             = "Radiata",
            ContextMenuStrip = menu,
            Visible          = _config.Current.System.ShowTrayIcon,
        };
        // Single left-click opens Settings — or resumes first-run setup while that's unfinished, matching
        // the relaunch path. Settings on an un-onboarded install would bury the wizard the user closed.
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (NeedsOnboarding) RunOnboarding(); else OpenSettings();
        };
        // Warn once at startup if a peer controller tool (DS4Windows / reWASD / InputMapper) is running —
        // they share Radiata's ViGEmBus + HidHide drivers and cause double or blocked input (HidHide's
        // control device is single-owner). See DriverStatus.RunningControllerTools + the onboarding note.
        var peers = DriverStatus.RunningControllerTools();
        if (peers.Count > 0)
        {
            var names = string.Join(", ", peers);
            Trace.WriteLine($"[Conflict] peer controller tool(s) running: {names}");
            // Why it conflicts (shared ViGEmBus/HidHide, single-owner control device) is in the comment
            // above and in the [Conflict] trace line — the notice states the action, not the mechanism.
            ShowCornerToast(Loc.T(UiText.Toasts.DriverConflict),
                Loc.F(UiText.Toasts.DriverConflictBody, names), holdMs: 9000);
        }
    }

    /// <summary>Open Explorer with the running Radiata.exe selected.</summary>
    private static void ShowExeInExplorer()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !System.IO.File.Exists(exe))
        {
            Trace.WriteLine($"[Tray] Show in Explorer: exe path unavailable ('{exe}')");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exe}\"") { UseShellExecute = true });
    }

    private void UpdateTrayText(string text) =>
        _trayIcon!.Text = TrayText(text);

    /// <summary>The shell caps NotifyIcon text at 63 UTF-16 units. Elide on a character boundary rather than
    /// slicing: a cut inside a surrogate pair or before a combining mark ships a broken glyph.</summary>
    private static string TrayText(string text)
    {
        if (text.Length <= 63) return text;
        int cut = 62;
        while (cut > 0 && (char.IsLowSurrogate(text[cut]) || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text[cut]) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark)) cut--;
        return text[..cut] + "…";
    }

    /// <summary>Whether the physical pad is currently hidden from games with our virtual pad standing in
    /// ("isolated"), or we're in best-effort mode where the game also sees the user's input while a wheel is
    /// open. Set by <c>UpdateInputCaptureCore</c>, surfaced in the tray tooltip.</summary>
    private bool _isolated;
    /// <summary>Short reason we're NOT isolated, for the tooltip. Empty when isolated or when there's
    /// nothing to say (wheels off, no pad).</summary>
    private string _isolationNote = "";
    // "Isolated" would overclaim: the pad was present before the cloak landed, so a process running
    // then may hold a pre-cloak handle no readback can detect (the cloak blocks future opens only).
    // Tray-only honesty downgrade fed by SteamSentry — never a warning toast (usually harmless).
    private bool _isolationUnverified;
    private string? _isolationUnverifiedHolder;   // a KNOWN plausible holder's display name, if one matched
    // The isolation note the warning card last fired for — the latch is per distinct reason, so a
    // second, different degradation in the same session still warns. Empty = none fired yet;
    // cleared on recovery so a relapse warns again.
    private string _isolationWarnedNote = "";
    // The hub notice that should PERSIST for the current wheel-open (leak warning / passthru-ending chip):
    // UpdateArmedPreview restores it when the armed preview clears, instead of leaving an empty hub.
    // Assigned fresh on every wheel open; nulled on dismiss.
    private string? _hubStickyNotice;
    // The Passthru Mode chip was already shown during this summon. A summon runs from the first wheel
    // opening until the overlay parks fully hidden (OverlayWindow.Parked), so swapping to the other
    // wheel mid-summon does not repeat it.
    private bool _passthruNoticeShown;

    /// <summary>Compose the tray tooltip from connection + isolation state.
    ///
    /// <para>The tooltip is the always-available answer to "is the game also seeing my stick input" in
    /// best-effort mode; the warning card below fires per cause so the first such episode isn't silent.
    /// Tooltip text is capped at 63 chars by the shell, hence the terseness.</para></summary>
    private void RefreshTrayText()
    {
        if (_trayIcon is null) return;
        // An active sentry episode outranks every isolation claim: "isolated" is technically true of the
        // cloak+pad pair during a diagnosed Steam leak, but the tooltip's job is answering "is the game
        // also seeing my stick input" — and during the episode the answer is yes. Deliberately NOT
        // routed through IsolationUnverified: a diagnosed leak is a stronger claim than "unverified".
        string state = !_controllerConnected ? Loc.T(UiText.Tray.NoController)
                     : _steamSentry.Current is SteamSentry.State.Detected or SteamSentry.State.Remediating
                                             ? Loc.T(UiText.Tray.NotIsolatedSteam)
                     : _isolated && _isolationUnverified
                                             ? (_isolationUnverifiedHolder is null
                                                ? Loc.T(UiText.Tray.Unverified)
                                                : Loc.F(UiText.Tray.UnverifiedHolder, _isolationUnverifiedHolder))
                     : _isolated            ? Loc.T(UiText.Tray.Isolated)
                       // Passthru engaged by an exception names the app that did it: the tooltip answers
                       // "is the game seeing my stick input", and the follow-up is "why is this on" — which
                       // only matters when the user didn't turn it on themselves. Display only; the note
                       // stays PassthruNoCloak so Arcade.ReasonFromNote keeps matching one const.
                     : SafeModeActive && _autoSafeMode && _autoSafeModeApp is { Length: > 0 } autoApp
                                             ? Loc.F(UiText.IsolationNote.PassthruAutoApp, autoApp)
                     : _isolationNote.Length > 0 ? Loc.T(_isolationNote)
                     : Loc.T(UiText.Tray.Connected);
        UpdateTrayText(Loc.F(UiText.Tray.Title, state));
    }

    /// <summary>Record the capture outcome for the tray, and warn the first time a connected pad ends up
    /// in best-effort mode — the case where input reaches the game. Latched per distinct reason, and the
    /// latch clears on recovery, so a relapse warns again (a safety warning must track the current episode,
    /// not the session).</summary>
    private void NoteIsolationState(bool isolated, string note)
    {
        bool changed = isolated != _isolated || note != _isolationNote;
        _isolated = isolated;
        _isolationNote = note;
        if (isolated) _isolationWarnedNote = "";   // recovered — re-arm the warning for any later episode
        if (changed) RefreshTrayText();
        // An OPEN arcade re-evaluates its bleed-through guard on every capture change — isolation lost
        // mid-session (a second pad arriving, an auto-passthru-mode game launching) freezes the game behind the
        // card, and isolation regained lifts it. Cheap, and it's the only hook that sees every such change.
        if (changed) RefreshArcadeGuard();

        // A consented Steam restart waits here for its precondition: only a bounce under a confirmed
        // cloak actually strands Steam's pad handle (its re-open is denied). Expired = dropped, traced.
        if (isolated && _userSteamRestartPending)
        {
            if (!ExpireUserSteamRestart(Environment.TickCount64))
            {
                _userSteamRestartPending = false;
                _userSteamRestartExpiryTimer?.Stop();
                _ = UserSteamRestartAsync();
            }
        }

        // No _wheelsEnabled gate: with the routing-only chord, isolation is expected to HOLD while the
        // wheels are off — a broken cloak leaks the raw pad to games regardless of the wheels state.
        // A pending passthru-mode-off is an EXPECTED degraded state with its own toast at toggle time —
        // re-warning "isolation blocked" here would contradict the "ending when the game relaunches" story.
        if (isolated || !_controllerConnected || SafeModeActive || _safeModeOffPending) return;
        if (note.Length == 0) return;   // nothing to warn about (no pad, wheels off)
        // Latched per DISTINCT reason, not once per session: the reasons have different cures ("close the
        // HidHide client" vs "install ViGEmBus"), so a session that degrades one way, recovers, then
        // degrades another way must not be silent the second time because an unrelated warning already fired.
        if (note == _isolationWarnedNote) return;
        // CONFIRM the episode before warning: a BT stall's forced reopen (a pad turning off) produces one
        // transient "connected but devnodes unresolved" pass that reads as a cloak failure and heals within
        // ms — warning on it told a user who'd just switched their pad off to "close competing apps". A
        // real degradation is still degraded 3 s later; re-check then and only warn if it held.
        _isolationWarnNote = note;
        if (_isolationWarnTimer is null)
        {
            _isolationWarnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _isolationWarnTimer.Tick += (_, _) =>
            {
                _isolationWarnTimer?.Stop();
                if (!_isolated && _controllerConnected && !SafeModeActive && !_safeModeOffPending
                    && _isolationNote == _isolationWarnNote && _isolationNote != _isolationWarnedNote
                    && _isolationNote.Length > 0)
                    FireIsolationWarning(_isolationNote);
            };
        }
        // A pad count of two settles slower than a cloak: a transport switch enumerates the new link before
        // the old one drops, and a pad on cable AND its live dongle link is two devnodes for as long as both
        // stay up (the idle dongle alone enumerates nothing). Give that cause a longer look before naming it.
        _isolationWarnTimer.Interval = TimeSpan.FromSeconds(note == UiText.IsolationNote.MultiplePads ? 8 : 3);
        _isolationWarnTimer.Stop();
        _isolationWarnTimer.Start();
    }

    private DispatcherTimer? _isolationWarnTimer;
    private string _isolationWarnNote = "";

    /// <summary>The actual warning, fired only for a CONFIRMED degradation (see the deferral above).</summary>
    private void FireIsolationWarning(string note)
    {
        // Suppressed during onboarding WITHOUT setting the per-reason latch, so the wizard's Closed
        // handler re-runs capture and a condition that outlived onboarding still warns then. (The
        // shared card surface has its own onboarding gate; this one exists to keep the latch clean.)
        if (_onboardingWindow is { IsLoaded: true })
        {
            TraceThrottled("iso-warn-oobe", $"[Capture] isolation warning deferred during onboarding — \"{note}\"");
            return;
        }
        // Missing-driver causes are DIAGNOSABLE, so they can carry a cure — and when the user explicitly
        // declined the drivers, the missing driver is the state they chose, so the warning is suppressed
        // outright (DriversDeclined clears at startup once both drivers are detected installed). The
        // driver-present variants stay cureless: guessing sends people to driver repair for a driver
        // that may be fine.
        bool vigemMissing   = note == UiText.IsolationNote.NoVirtualPad && !_emulator.ProbeDriverInstalled();
        bool hidHideMissing = note == UiText.IsolationNote.CloakFailed
                              && _hidHide.LastFailure == HidHideManager.CloakFailure.DriverMissing;
        if (_config.Current.System.DriversDeclined && (vigemMissing || hidHideMissing))
        {
            TraceThrottled("iso-warn-declined",
                           $"[Capture] isolation warning suppressed — drivers were deliberately declined (\"{note}\")");
            return;
        }
        _isolationWarnedNote = note;
        // Contention has a concrete, user-doable cure, so it gets the holder's name and the instruction.
        // Every OTHER cause states what happened and names no culprit: we don't know the cure, and guessing
        // one sends people to driver repair for a driver that may be fine.
        // ⚠ Don't reintroduce a shared "close competing apps" fallback here. Holder names exist only for
        // contention; applying that wording to every other cause — most often the multi-pad guard, where
        // nothing is contending and the cure is to unplug a pad — sends users hunting an app that isn't
        // involved. Cases below are authored in UpdateInputCaptureCore's NoteIsolationState call;
        // keep the two in step (Core/Arcade/Arcade.cs ▸ ReasonFromNote switches on the same strings).
        var holders = _hidHide.LastFailure == HidHideManager.CloakFailure.Contended
                      ? DriverStatus.RunningHidHideTools() : [];
        // The self-drawn toast is the ONLY channel — it does not depend on the tray icon, app identity,
        // or Windows notification plumbing. A tray balloon here would DUPLICATE it on installs with a
        // Start-Menu shortcut (two notices for one event); don't add one back. A Windows notification
        // must also never become the only channel: the portable ZIP has no shortcut/AUMID, and Windows
        // drops shell notifications for such an exe silently.
        Trace.WriteLine($"[Capture] isolation toast — \"{note}\"" + (holders.Count > 0 ? $" (holder: {string.Join(", ", holders)})" : ""));
        // Holder names arrive article-first ("the HidHide Configuration Client"); the rectangular card
        // has room for the full prose, so keep the article.
        ShowIsolationToast(
              // A session-0 service can't be closed - the only cure is a restart; say so instead of "Close".
              holders.Contains(DriverStatus.WatchdogHolderName)
                                                     ? Loc.F(UiText.Toasts.IsoRestartHolder, string.Join(", ", holders))
            : holders.Count > 0                      ? Loc.F(UiText.Toasts.IsoCloseHolders, string.Join(", ", holders))
            : note == UiText.IsolationNote.HidHideBusy   ? Loc.T(UiText.Toasts.IsoHidHideBusy)
            : note == UiText.IsolationNote.MultiplePads  ? Loc.T(UiText.Toasts.IsoMultiplePads)
            : note == UiText.IsolationNote.PadCountUnstable ? Loc.T(UiText.Toasts.IsoPadCountUnstable)
            : note == UiText.IsolationNote.XboxFallback  ? Loc.T(UiText.Toasts.IsoXboxFallback)
            : vigemMissing                            ? Loc.T(UiText.Toasts.IsoViGEmMissing)
            : note == UiText.IsolationNote.NoVirtualPad ? Loc.T(UiText.Toasts.IsoNoVirtualPad)
            : hidHideMissing                          ? Loc.T(UiText.Toasts.IsoHidHideMissing)
            : note == UiText.IsolationNote.CloakFailed   ? Loc.T(UiText.Toasts.IsoCloakFailed)
            :                                           Loc.T(UiText.Toasts.IsoInactive));
    }

    private void OpenSettings()
    {
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_config, slice => _executor.Execute(slice), DetectedControllerKind, RunOnboarding)
            {
                // Controller/driver tools: Install/Recover are on
                // the Advanced tab; Controller Setup + HID Diagnostics surface in Developer Settings.
                ControllerSetupHook   = OpenWizard,
                InstallDriversHook    = InstallDriversInteractive,
                HidDiagnosticsHook    = OpenDiagnostics,
                RecoverControllerHook = ResetInput,
                // Quit Radiata: the exact same clean-exit path the tray "Exit" item
                // uses — TearDown (un-cloak + release the virtual pad) then Shutdown.
                QuitRequestedHook     = ExitApp,
                // "Test Wheel" button in each wheel editor: true = Right Wheel (WheelBEditor),
                // matching ActiveWheel.B.
                TestWheelHook = right => { if (_wheelsEnabled) ToggleOverlay(right ? ActiveWheel.B : ActiveWheel.A); },
            };
            // Wheels-disabled strip (F1): one-click re-enable without the controller chord. HandleChord
            // is the exact same path the chord takes (toast + capture swap included).
            _settingsWindow.EnableWheelsRequested = () => { if (!_wheelsEnabled) HandleChord(); };
            _settingsWindow.Closed += OnSettingsClosed;
            _settingsWindow.SetControllerConnected(_controllerConnected, PadTransport());   // seed the Current Controller readout
            _settingsWindow.SetBattery(_batteryPercent, _batteryCharging);                 // …and its battery row
        }
        _settingsWindow.SetWheelsEnabled(_wheelsEnabled);   // seed/refresh the disabled strip
        BringToFront(_settingsWindow);
    }

    /// <summary>Toggle XBox Mode: present an Xbox-360 pad instead of native mode's DualShock 4 pad.
    /// Returns the new state (true = on). Switching swaps the virtual pad type once (one device change
    /// the game sees). If ViGEmBus isn't installed (so no pad can come up at all), surfaces a corner
    /// notice and stays off.</summary>
    private bool ToggleEmulation()
    {
        if (_userEmulation)
        {
            _userEmulation = false;
            UpdateInputCapture();                // back to the native DualShock 4 pad (or raw if capture off)
            _controller.SetLightbar(0, 0, 48);   // faint-blue idle (all-zero is ignored by the pad)
            return false;
        }

        _userEmulation = true;
        UpdateInputCapture();                    // swap to the Xbox-360 pad
        if (_emulator.PadType != EmulatedPad.Xbox360)   // the Xbox pad didn't come up
        {
            // Only an actually-missing driver reverts with the notice. A TRANSIENT connect failure
            // lands in the same state (Start drops the DS4 pad before connecting the X360 one) and
            // must not be blamed on the driver — keep XBox Mode on and let the scheduled retry
            // bring the Xbox pad up.
            if (!_emulator.ProbeDriverInstalled())
            {
                _userEmulation = false;
                UpdateInputCapture();
                ShowCornerToast(Loc.T(UiText.Toasts.EmulationUnavailable),
                    Loc.T(UiText.Toasts.EmulationUnavailableBody));
                return false;
            }
        }
        _controller.SetLightbar(0, 255, 0);      // green = emulating
        return true;
    }

    /// <summary>Toggle DualShock Mode — the mirror of XBox Mode, for Xbox-controller users (present a
    /// DualShock 4 pad). Today native already IS a DS4, so this flips the same pad-type selection from
    /// the DualShock side: returns true ("On") when in native DualShock, false when in XBox Mode.</summary>
    private bool ToggleDualShock() => !ToggleEmulation();

    private void OpenAbout()
    {
        OpenSettings();
        _settingsWindow?.ShowAbout();
    }

    private void OpenHelp()
    {
        OpenSettings();
        _settingsWindow?.ShowHelpTopic();
    }

    /// <summary>Open Settings on the given wheel's editor with <paramref name="index"/> selected, so the
    /// user lands on the slice that needs configuring.</summary>
    private void OpenSliceInSettings(ActiveWheel wheel, int index)
    {
        OpenSettings();
        _settingsWindow?.EditSlice(wheel == ActiveWheel.B, index);
    }

    /// <summary>True when the slice drives Discord over RPC and the user's own Discord app credentials
    /// aren't set yet, so firing it can only fail silently. Three actions qualify: Join/Leave Voice Channel,
    /// Deafen and Mute Me. "Launch Discord" does not — it just starts the app.
    /// <para>Read by both the armed hub's needs-setup notice and the open-the-wizard-on-fire branch, so
    /// adding a type here is the whole host-side wiring for a new RPC action.</para></summary>
    private static bool NeedsDiscordIntegration(WheelSlice slice) =>
        slice.Action?.Type?.ToLowerInvariant() is "discord-join" or "discord-deafen" or "discord-mute"
        && !DiscordOAuth.IsConfigured;

    /// <summary>Open the Discord credentials wizard — the same window the slice editor and Settings ▸
    /// Advanced open. Brought to the front by hand: the caller is the click-through overlay, which has no
    /// foreground claim to pass on.</summary>
    private void OpenDiscordIntegrationSetup()
    {
        // Topmost from the start rather than through BringToFront: that helper Shows the window, and
        // ShowDialog refuses a window that is already visible. Dropped again once it has the foreground.
        // CenterScreen because there is no parent window here — CenterOwner would land it at the corner.
        var dlg = new DiscordSetupWindow
        {
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        dlg.Loaded += (_, _) => { dlg.Activate(); dlg.Topmost = false; };
        dlg.ShowDialog();
    }

    // ── First-run onboarding (OOBE) ─────────────────────────────────────────────
    // Gate: the stored OobeVersion vs OobeCurrentVersion. OnboardingWindow auto-opens at startup while
    // NeedsOnboarding; Settings ▸ Advanced ▸ "Run first-run setup" opens it again without resetting
    // OobeVersion (see RunOnboarding), so a re-run never counts as a first run.
    // BUMP RULE: this moves only on an explicit decision, never with a release or a ReleaseMinor change.
    // A bump re-runs first-run setup for every existing user on their next launch, so flow tweaks, copy
    // passes, step reorders and milestones alone do NOT bump it.
    private const int OobeCurrentVersion = 14;

    /// <summary>True when the first-run flow should run (stored OOBE version is behind the current).</summary>
    private bool NeedsOnboarding => _config.Current.System.OobeVersion < OobeCurrentVersion;

    /// <summary>Record that onboarding was completed at the current version (call from the flow's finish).
    /// Also adopt the pad the user just set up as the known baseline, so it's never later announced as
    /// "new" and a swap TO a different kind is correctly flagged.</summary>
    private bool MarkOnboarded()
    {
        var kind = DetectedControllerKind;
        var sys  = _config.Current.System;
        var known = new List<string>(sys.KnownControllerKinds);
        if (!known.Contains(kind.ToString(), StringComparer.OrdinalIgnoreCase)) known.Add(kind.ToString());
        if (!WriteSystem(sys with { OobeVersion = OobeCurrentVersion, OobeStep = 0,
                               LastControllerKind = kind.ToString(), KnownControllerKinds = known,
                               EditHintSeen = false })) return false;   // re-arm the edit-discovery hub hint
        _editHintWindowEnd = 0;                           // fresh 10 s window on the next wheel open
        _activeKind = kind;
        return true;
    }

    // ── Edit-discovery hint (post-onboarding) ─────────────────────────────────
    // The first wheel(s) opened after finishing the wizard get a center-hub reminder that clicking the
    // aiming stick edits the wheel. Shows for the wheel's whole lifetime, and again on any wheel opened
    // within 5 s of the first show; after that the flag persists and it never returns (until the wizard
    // is completed again — MarkOnboarded re-arms it).
    private long _editHintWindowEnd;   // TickCount64 deadline; 0 = hint not shown yet this arming

    private bool EditHintDue()
    {
        if (_config.Current.System.EditHintSeen || NeedsOnboarding) return false;
        long now = Environment.TickCount64;
        if (_editHintWindowEnd == 0) { _editHintWindowEnd = now + 5_000; return true; }
        if (now <= _editHintWindowEnd) return true;
        // Window over — persist so the hint never shows again (survives restarts).
        WriteSystem(_config.Current.System with { EditHintSeen = true });
        return false;
    }

    /// <summary>Launch the wizard, logging the open + driver state first. Tray / Settings hook AND the
    /// startup auto-open path (so a wizard that suppresses summons is visible in the trace).
    ///
    /// Deliberately does NOT write OobeVersion=0: re-arming the flow the instant the window opens would
    /// give anyone who opened "Run first-run setup" out of curiosity the wizard on EVERY launch afterwards.
    /// A genuine first run is unaffected — its stored version is still behind
    /// <see cref="OobeCurrentVersion"/>, so closing early re-opens next launch; that nagging is the
    /// FIRST-RUN contract, not something a re-run should opt you back into. Finishing calls
    /// <see cref="MarkOnboarded"/>, which stamps the current version either way.</summary>
    private void RunOnboarding()
    {
        // A DELIBERATE re-run of a completed flow starts at the beginning; only an unfinished first run
        // carries a resume point (SystemConfig.OobeStep). Without this, "Run first-run setup" after
        // abandoning an earlier re-run would drop you back into the middle of it.
        // …except a relaunch INTO the wizard (--oobe after a language pick), which must resume where it was.
        if (!NeedsOnboarding && _config.Current.System.OobeStep != 0
            && Array.IndexOf(Environment.GetCommandLineArgs(), "--oobe") < 0)
            WriteSystem(_config.Current.System with { OobeStep = 0 });
        Trace.WriteLine($"[OOBE] wizard opened (stored OobeVersion={_config.Current.System.OobeVersion}, current={OobeCurrentVersion}). " +
                        $"ViGEmBus={(_emulator.ProbeDriverInstalled() ? "installed" : "MISSING")} " +
                        $"HidHide={DriverStatus.HidHideVersion() ?? "MISSING"} " +
                        $"HidGuardian(legacy)={(DriverStatus.HidGuardianInstalled() ? "PRESENT" : "absent")}");
        OpenOnboarding();
    }

    private OnboardingWindow? _onboardingWindow;
    private bool _controllerConnected;   // live pad state (Connected event), read by the wizard
    private int  _batteryPercent = -1;   // last reported battery level (-1 = none), to seed the Settings readout
    private bool _batteryCharging;
    private ControllerKind? _activeKind; // kind we last announced/seeded this session (mid-run swap detect)

    /// <summary>Show the first-run wizard (single instance — a second call focuses it).</summary>
    private void OpenOnboarding()
    {
        if (_onboardingWindow is { IsLoaded: true }) { BringToFront(_onboardingWindow); return; }
        _onboardingWindow = new OnboardingWindow(new OnboardingWindow.Hooks(
            ControllerConnected: () => _controllerConnected,
            ControllerName:      () => FriendlyKind(DetectedControllerKind),
            Kind:                () => DetectedControllerKind,
            HasAdaptiveTriggers: () => _controller.HasAdaptiveTriggers,
            ViGEmInstalled:      () => _emulator.ProbeDriverInstalled(),
            ViGEmForeign:        DriverStatus.ForeignViGEmBusName,
            HidHideVersion:      DriverStatus.HidHideVersion,
            HidHideUpdateAvailable: HidHideUpdateAvailable,
            HidGuardianPresent:  DriverStatus.HidGuardianInstalled,
            RunningControllerTools: DriverStatus.RunningControllerTools,
            RunDriverEngine:     RunDriverEngineAsync,
            GetSystem:           () => _config.Current.System,
            WriteSystem:         WriteSystem,
            GetWheels:           () => (_config.Current.WheelA, _config.Current.WheelB),
            WriteWheels:         (a, b) =>
            {
                var cur = _config.Current;
                return TryWriteConfig(new AppConfig
                {
                    // Same Thick→Medium rule as the other two wheel-write paths (onboarding's starter
                    // layout + ticked add-ons can land more than 8 slices on a wheel).
                    WheelA = a, WheelB = b, System = SliceThicknessRule.Apply(cur.System, a.Length, b.Length),
                    ActionColors = cur.ActionColors, ActionIcons = cur.ActionIcons, CustomColors = cur.CustomColors,
                });
            },
            SetOobePreview:      on => _oobePreview = on,
            // The sample-wheels hook is true exactly for the practice step's lifetime, so it also gates
            // the in-hub which-stick practice art (OverlayWindow.SetOnboardingPracticeToast → RadialMenuControl).
            SetSampleWheels:     on => { _oobeSampleWheels = on; _overlay?.SetOnboardingPracticeToast(on); },
            SetWheelInvoked:     cb => _oobeWheelInvoked = cb,
            SetInvokeLocked:     locked =>
            {
                if (locked != _oobeInvokeLocked) _oobeSuppressLogged = false;   // fresh lock period → log once again
                _oobeInvokeLocked = locked;
            },
            SetPracticeAll:      (on, modes) =>
            {
                _oobePracticeAll = on;
                // The Fn handlers live here, not in the interpreter, so they need their own answer to
                // "is a lone press live?" — the wizard omits the token when it would pre-empt a chord.
                _oobeFnLive = on && modes?.Contains(TriggerModes.FnButtons) == true;
                _triggerInterpreter?.SetPracticeAllModes(on, modes);   // null-guarded + ignored when off inside SetPracticeAllModes
            },
            SetChordPracticed:   cb => _oobeChordPracticed = cb,
            SetChordButtonListener: cb => _oobeChordButtons = cb,
            SetPracticeChordToken: tok =>
            {
                _oobeChordToken = tok;                            // gates the both-Fn toggle below
                _triggerInterpreter?.SetPracticeChordMode(tok);   // gates the chord/touchpad toggles
            },
            EnsureWheelsEnabled: () => { _wheelsEnabled = true; UpdateInputCapture(); },   // restore pad if practice left it off
            OverlayVisible:      () => _overlayVisible,
            OpenWheel:           right =>
            {
                if (!_wheelsEnabled) { _wheelsEnabled = true; UpdateInputCapture(); }   // in case a practice chord left them off
                ToggleOverlay(right ? ActiveWheel.B : ActiveWheel.A);
            },
            SetOverlayClickExempt: test => _overlayClickExempt = test,
            BuildGameSlice:      BuildGameSlice,
            // Finish-step cards: open Settings, optionally jumped to a tab ("help" / "advanced" / null).
            OpenSettingsPage:    page =>
            {
                OpenSettings();
                if (page == "help") _settingsWindow?.ShowHelpTopic();
                else if (page == "advanced") _settingsWindow?.ShowAdvanced();
            },
            // Finish-step tray highlight: the wizard resolves the icon's screen rect itself
            // (Shell_NotifyIconGetRect needs the NotifyIcon's message hwnd + id).
            GetTrayIcon:         () => _trayIcon,
            SteamRunning:        SteamSentry.SteamRunning,
            RequestSteamRestart: ArmUserSteamRestart,
            RequestRestart:      () => SettingsWindow.RestartApp(reopenOnboarding: true),
            Finish:              MarkOnboarded));
        // The practice/finish hooks are live only while the wizard is open — clear them when it closes so a
        // stray invoke can't fire into a disposed window, leave sample-wheel/practice mode stuck on, or
        // leave the wheels locked out or disabled.
        _onboardingWindow.Closed += (_, _) =>
        {
            _oobePreview = false;
            _oobeSampleWheels = false;
            _oobeWheelInvoked = null;
            _oobeInvokeLocked = false;
            _oobeSuppressLogged = false;
            _oobeChordPracticed = null;
            _oobeChordToken = null;
            _overlayClickExempt = null;   // the predicate closes over wizard controls — don't outlive it
            _triggerInterpreter?.SetPracticeChordMode(null);
            _lastTrigger = null;
            if (_oobePracticeAll) { _oobePracticeAll = false; _oobeFnLive = false; _triggerInterpreter?.SetPracticeAllModes(false); }
            _wheelsEnabled = true;   // chord practice may have left the wheels toggled off
            // A practice wheel still up when the wizard closes would stay painted + suppressed (with a
            // hold-chord config there's no easy gesture to dismiss it) — tear it down with the hooks.
            if (_overlayVisible || Editing) DismissOverlay();
            UpdateInputCapture();    // restore the virtual pad + cloak if practice's disable-chord left it off
        };
        _onboardingWindow.Closed += (_, _) => _onboardingWindow = null;
        // Re-evaluate the deferred notices AFTER the null-assign above, and via the dispatcher so every
        // Closed handler has run: the onboarding gate suppresses the whole rectangular-notice class and
        // the sentry card, so any condition still live (a Steam leak, a confirmed isolation warning)
        // must get its chance to surface the moment the wizard is gone.
        _onboardingWindow.Closed += (_, _) => Dispatcher.InvokeAsync(UpdateInputCapture);
        // ShowSentryAlert's onboarding gate only declines to OPEN a card; the first capture pass beats
        // this window (auto-open is deferred to ApplicationIdle), so on a first run with Steam already
        // holding the pad the card is up before the wizard exists. Tear it down here: step 1 carries the
        // pre-checked "Restart Steam" opt-in — the designed cure — and the card's ▲/□ actions compete
        // with it for the same pad. The Closed handler above re-runs capture, so a leak that outlives
        // the wizard raises the card then.
        CloseSentryAlert();
        _onboardingWindow.Show();
        BringToFront(_onboardingWindow);
    }

    /// <summary>Install/repair the driver stack from the bundled installers, elevated. Sequenced: legacy
    /// cleanup (Legacinator) if the conflicting HidGuardian is present → ViGEmBus (run twice if an upgrade
    /// doesn't take on the first pass — a known ViGEmBus quirk) → HidHide. Interactive (UAC + WiX progress);
    /// reports a summary via the tray.</summary>
    private async void InstallDriversInteractive()
    {
        List<string> log;
        try { log = await RunDriverEngineAsync(); }
        // async void (a menu/button handler) — an escaping exception would take the process down.
        catch (Exception ex)
        {
            Trace.WriteLine($"[DriverSetup] engine failed: {ex.Message}");
            log = [Loc.T(UiText.Dialogs.DriverSetupFailedLog)];
        }
        // A MessageBox (not a tray balloon — those are unreliable/suppressed on Win10) so the result is
        // always visible. (The OOBE flow shows the same log inline instead — see OnboardingWindow.)
        System.Windows.MessageBox.Show(Loc.T(UiText.Dialogs.DriverSetupHeading) + "\n\n• " + string.Join("\n• ", log),
            Loc.T(UiText.Dialogs.DriverSetupCaption), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>The driver-setup ENGINE (no UI besides the installers' own): legacy cleanup → ViGEmBus →
    /// HidHide → elevated self-whitelist → re-capture. Returns the human-readable result log. Shared by the
    /// Advanced-tab "Install/Repair Drivers" (MessageBox) and the onboarding wizard (inline).</summary>
    private bool _driverEngineSucceeded;
    private async Task<List<string>> RunDriverEngineAsync()
    {
        _driverEngineSucceeded = false;
        var log = new List<string>();
        if (!DriverSetup.SupportedOS)
        {
            log.Add("Bundled driver setup requires native x64 Windows 10 or Windows 11.");
            return log;
        }

        // Resolve conflicts before probing or mutating the driver stack.
        var foreignBus = DriverStatus.ForeignViGEmBusName();
        if (foreignBus is not null)
        {
            log.Add($"Driver setup stopped: {foreignBus}'s ViGEmBus is installed. Resolve that driver conflict before retrying.");
            return log;
        }
        if (DriverStatus.HidGuardianInstalled())
        {
            log.Add(await DriverSetup.RunLegacinatorAsync()
                ? "Legacinator launched — remove HidGuardian, restart Windows, then re-run driver setup"
                : "Legacinator launch failed/declined — driver setup stopped");
            return log;
        }

        // Un-cloak before the install/upgrade rewrites the driver's lists. (We hold NO device handle
        // between calls — the wrapper opens per operation; see HidHideManager gotcha #2 — so there is no
        // files-in-use hold to drop, only cloak state to make safe.) Re-established at the end.
        if (!_hidHide.Release())
        {
            log.Add(Loc.T(UiText.Settings.RecoveryRequired));
            return log;
        }
        _emulator.Stop();
        _cloakedIds.Clear();
        _xboxCloakedIds.Clear();

        // A foreign ViGEmBus (HP OMEN Gaming Hub's fork, Oculus's) answers the client's probe as "present",
        // and Nefarius supports no coexistence: the bundled installer beside it yields two buses and a
        // stalled setup. Never install over one; name it and leave the switch to the user
        // (disable the fork in Device Manager first — docs/INPUT-CAPTURE.md ▸ Foreign ViGEmBus forks).
        if (!_emulator.ProbeDriverInstalled())
        {
            bool ok = await DriverSetup.InstallViGEmBusAsync();
            if (ok && !_emulator.ProbeDriverInstalled()) ok = await DriverSetup.InstallViGEmBusAsync();   // upgrade: 2nd run
            log.Add(_emulator.ProbeDriverInstalled() ? "ViGEmBus installed"
                    : ok ? "ViGEmBus installed (reboot may be needed)" : "ViGEmBus install incomplete");
        }
        else log.Add("ViGEmBus already present");

        if (!_emulator.ProbeDriverInstalled())
        {
            // Do not mutate the remaining stack after a failed/cancelled prerequisite or pending reboot.
            UpdateInputCapture();
            return log;
        }
        bool hidHideChanged = false;   // fresh install or upgrade this run → the class filter needs a device restart
        if (!DriverStatus.HidHideInstalled())
        {
            hidHideChanged = true;
            log.Add(await DriverSetup.InstallHidHideAsync() && DriverStatus.HidHideInstalled()
                    ? "HidHide installed" : "HidHide install incomplete (reboot may be needed)");
        }
        else if (HidHideUpdateAvailable())
        {
            // In-place UPGRADE to the bundled version (the WiX bundle handles it; our exclusive control-
            // device hold was Release()d above — a stray HidHideClient.exe can still block, which shows
            // up as "incomplete"). Older HidHide WORKS, so a failed upgrade is not fatal.
            hidHideChanged = true;
            log.Add(await DriverSetup.InstallHidHideAsync() && !HidHideUpdateAvailable()
                    ? $"HidHide updated to v{DriverStatus.HidHideVersion()}"
                    : $"HidHide update incomplete — still v{DriverStatus.HidHideVersion()} (close HidHide's config app / reboot and retry)");
        }
        else log.Add($"HidHide up to date (v{DriverStatus.HidHideVersion()})");

        // One-time elevated self-whitelist so cloaking never hides the pad from US (rename-lockout fix).
        // The same elevated one-shot also RESTARTS the pad devnodes so a freshly-installed HidHide class
        // filter attaches immediately (clean-install gotcha #3 — otherwise the cloak is configured but
        // filters nothing until a Windows reboot). WhitelistSelf re-establishes capture at its end; if
        // HidHide isn't present, re-establish here.
        if (DriverStatus.HidHideInstalled())
        {
            bool whitelistOk = await WhitelistSelfAsync(sweepPadsIfDisconnected: hidHideChanged);
            _driverEngineSucceeded = whitelistOk && _emulator.ProbeDriverInstalled() && !HidHideUpdateAvailable();
            log.Add(whitelistOk
                ? "HidHide access and logon recovery configured; controller isolation is checked separately"
                : "HidHide access or logon recovery setup failed — resolve the error and retry driver setup");
        }
        else
            UpdateInputCapture();

        Trace.WriteLine("[DriverSetup] " + string.Join("; ", log));
        return log;
    }

    /// <summary>The `--install-drivers` flow behind <see cref="DriverInstallWindow"/>: the shared driver
    /// engine, then Shutdown with 0 on success (drivers present afterwards) or 1 on an incomplete run.
    /// This instance is pre-config (no `_config`), so the engine's capture re-establish steps no-op —
    /// see the guard in <see cref="UpdateInputCapture"/>.</summary>
    private async Task RunInstallDriversAsync(DriverInstallWindow win)
    {
        int code = 1;
        try
        {
            var log = await RunDriverEngineAsync();
            bool ok = _driverEngineSucceeded && _emulator.ProbeDriverInstalled() && DriverStatus.HidHideInstalled();
            code = ok ? 0 : 1;
            win.ShowResult(log, ok);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DriverSetup] --install-drivers failed: {ex.Message}");
            win.ShowResult(["Driver setup failed."], ok: false);
        }
        await win.WaitForCloseAsync();
        Shutdown(code);
    }

    /// <summary>Whether this process runs elevated (the --install-drivers self-relaunch gate).</summary>
    private static bool IsProcessElevated()
    {
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>True when HidHide is installed but OLDER than the bundled installer (upgrade on offer).</summary>
    private static bool HidHideUpdateAvailable() =>
        Version.TryParse(DriverStatus.HidHideVersion(), out var installed)
        && installed < DriverSetup.BundledHidHideVersion;

    /// <summary>Startup nudge for the HidHide self-lockout (<see cref="HidHideManager.DetectSelfLockout"/>):
    /// a clickable corner card that runs the elevated self-whitelist. The always-available manual cure is
    /// Settings ▸ Advanced ▸ Install/Repair Drivers (its engine also whitelists).</summary>
    private void NudgeHidHideLockout()
    {
        Trace.WriteLine("[HidHide] self-lockout detected at startup (cloaked, exe not whitelisted) — nudging fix");
        ShowCornerToast(Loc.T(UiText.Toasts.NotDetected),
            Loc.T(UiText.Toasts.NotDetectedBody),
            onClick: () => WhitelistSelf(), holdMs: 10000);
    }

    /// <summary>Fire-and-forget wrapper for callers with no await point (the notice's click handler).</summary>
    private void WhitelistSelf(bool sweepPadsIfDisconnected = false) =>
        _ = WhitelistSelfAsync(sweepPadsIfDisconnected);

    /// <summary>ELEVATED self-registration: add Radiata.exe to HidHide's allow-list so cloaking never hides
    /// the pad from us (the rename-lockout fix, gotcha #1). Releases our hold on the exclusive control
    /// device first (so the elevated helper can open it), relaunches self elevated to do the write, waits,
    /// then re-establishes capture. Returns true on success.
    /// The helper also restarts pad devnodes so the class filter attaches (gotcha #3) — the PHYSICAL pad's
    /// ids are passed explicitly so the game's VIRTUAL pad is never restarted (it shares Sony's VID, so a
    /// VID sweep would yank it mid-game). With no pad connected, the VID sweep runs only when
    /// <paramref name="sweepPadsIfDisconnected"/> (HidHide was just installed/upgraded).</summary>
    private async Task<bool> WhitelistSelfAsync(bool sweepPadsIfDisconnected = false)
    {
        var exe = Environment.ProcessPath;
        if (!DriverStatus.HidHideInstalled() || string.IsNullOrEmpty(exe)) return false;

        var padIds = _controller.GetHidInstanceIds();
        string restartArg = padIds.Count > 0
            ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', padIds)))
            : sweepPadsIfDisconnected ? "sweep" : "none";

        if (!_hidHide.Release()) return false; // Keep recovery ownership if the driver rejected release.
        _cloakedIds.Clear();     // so UpdateInputCapture re-cloaks afterward
        _xboxCloakedIds.Clear();
        bool ok = false;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, $"--hidhide-whitelist --owner-sid {RecoveryTask.OwnerSid} --restart-pads {restartArg}")
            { UseShellExecute = true, Verb = "runas" });
            // Awaited, not blocked: this runs on the UI thread during first-run setup (see
            // DriverSetup.RunElevatedAsync) — a blocking wait freezes the wizard behind the UAC prompt.
            if (p is not null) { await p.WaitForExitAsync().ConfigureAwait(true); ok = p.ExitCode == 0; }
        }
        catch (Exception ex) { Trace.WriteLine($"[HidHide] self-whitelist relaunch failed: {ex.Message}"); }  // e.g. UAC declined
        UpdateInputCapture();   // re-cloak + re-present the virtual pad per current state
        return ok;
    }

    /// <summary>Report a failed durable save without discarding the caller's working state.</summary>
    private bool TryWriteConfig(AppConfig config)
    {
        if (_config.WriteConfig(config)) return true;
        string message = Loc.F(UiText.Settings.SaveFailed, _config.LastWriteError ?? "");
        if (_onboardingWindow is { IsVisible: true } wizard)
            System.Windows.MessageBox.Show(wizard, message, "Radiata",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        else ShowCornerToast("Radiata", message, holdMs: 12000);
        _announcer.Announce(message, AnnouncementKind.Result);
        return false;
    }

    /// <summary>Persist a modified SystemConfig, leaving wheels / colours / icons untouched.</summary>
    private bool WriteSystem(SystemConfig system)
    {
        var cur = _config.Current;
        return TryWriteConfig(new AppConfig
        {
            WheelA       = cur.WheelA,
            WheelB       = cur.WheelB,
            System       = system,
            ActionColors = cur.ActionColors,
            ActionIcons  = cur.ActionIcons,
            CustomColors = cur.CustomColors,
        });
    }

    private void OnSettingsClosed(object? sender, EventArgs e)
    {
        // Practice mode just ended (PracticeMode follows _settingsWindow) — if a wheel is still up with a
        // slice ARMED, the hub pill would keep saying "Practice" while releasing now fires the REAL action
        // (the preview only re-evaluates when the armed index changes). Force a re-evaluate so the
        // indicator can never lag behind the real fire behaviour.
        if (_overlayVisible)
        {
            int armed = _previewArmed;
            _previewArmed = -1;
            UpdateArmedPreview(armed);
        }
        // In normal-app mode (tray icon off) the Settings window IS the app's only presence,
        // so closing it quits — like a normal windowed app.
        if (!_shuttingDown && !_config.Current.System.ShowTrayIcon)
            ExitApp();
    }

    /// <summary>Tray on → tray icon, no taskbar entry. Tray off → no tray icon; the Settings
    /// window is the app's normal taskbar presence (ensured shown; closing it exits the app).</summary>
    private void ApplyTrayMode()
    {
        bool showTray = _config.Current.System.ShowTrayIcon;
        if (_trayIcon is not null) _trayIcon.Visible = showTray;
        if (!showTray && (_settingsWindow is null || !_settingsWindow.IsLoaded))
            OpenSettings();   // guarantee a taskbar window exists
    }

    private HidWizardWindow? _wizardWindow;
    private void OpenWizard()
    {
        if (_wizardWindow is null || !_wizardWindow.IsLoaded)
            _wizardWindow = new HidWizardWindow(_controller);
        BringToFront(_wizardWindow);
    }

    private DiagnosticWindow? _diagnosticWindow;
    private void OpenDiagnostics()
    {
        if (_diagnosticWindow is null || !_diagnosticWindow.IsLoaded)
            _diagnosticWindow = new DiagnosticWindow(_controller);
        BringToFront(_diagnosticWindow);
    }

    // Activate() alone won't steal focus when the caller has none (click-through overlay).
    // Briefly going Topmost lets Windows bypass the foreground-lock restriction.
    private static void BringToFront(System.Windows.Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Topmost = true;
        window.Activate();
        window.Topmost = false;
    }

    // ── Chord toggle ─────────────────────────────────────────────────────────

    private void HandleChord()
    {
        if (_oobeInvokeLocked) return;   // onboarding pre-practice: mirror the summon lock — no state flips before the wheels debut
        if (_editPickGame)
        {
            // Logged BEFORE the return: a chord eaten by the edit-pick modal must not read as a dead chord.
            Trace.WriteLine("[Chord] chord consumed by the game picker — cancelling the pick, wheels unchanged");
            CancelGamePickForEdit();
            return;
        }
        Trace.WriteLine($"[Chord] toggling — currently {(_wheelsEnabled ? "enabled" : "disabled")}");
        // Kill any open wheel or in-flight fade/readout without firing its armed action
        if (_overlayVisible) DismissOverlay();
        else _overlay?.HideWheel();

        _wheelsEnabled = !_wheelsEnabled;
        // ROUTING-ONLY: the chord never touches the virtual pad or the cloak — disabled just means
        // summons are ignored while input keeps flowing 1:1 through the standing pad. The reconcile
        // call below is a cheap no-op in the steady state; it exists to self-heal drift (e.g. a cloak
        // that failed earlier and can be retried now). Device-level release lives in Passthru Mode and exit
        // only — see UpdateInputCapture remarks for the two failure classes that forbid it here.
        UpdateInputCapture();
        Sfx.Wheels(_wheelsEnabled);
        ShowStatusToast(_wheelsEnabled);
        // The flower toast is wordless — this is the state's only text surface.
        _announcer.Announce(Loc.T(_wheelsEnabled ? UiText.Narration.WheelsEnabled : UiText.Narration.WheelsDisabled), AnnouncementKind.Result);
        if (_settingsWindow is { IsLoaded: true } sw) sw.SetWheelsEnabled(_wheelsEnabled);   // F1 strip
        _oobeChordPracticed?.Invoke();   // practice step: check off the enable/disable chord
    }

    // ── Steam-first leak sentry (SteamSentry.cs holds the detection + relaunch; this region owns the UX) ──

    /// <summary>Run on every capture pass, after the isolation state settles. Detection → the silent
    /// startup bounce when nothing is in front (the 90% case), else the
    /// persistent alert. Cleared states tear the alert down.</summary>
    private void EvaluateSteamSentry(bool isolated)
    {
        _sentryLastIsolated = isolated;
        var state = _steamSentry.Evaluate(
            padPresent: _controllerPresentForPad,
            cloakConfirmedWithPad: isolated,
            safeMode: SafeModeActive);
        if (state != _lastSentryState)
        {
            _lastSentryState = state;
            RefreshTrayText();   // the tray ranks an active episode above "isolated" — see its branch
        }

        // The honesty downgrade rides the same evaluation: while pre-cloak handles can't be ruled out,
        // the tray says "isolation unverified" (naming a known plausible holder when one matched)
        // instead of asserting "isolated". Tooltip-only — no toast, no card: usually harmless, and a
        // per-session alarm for it would be tuned out (the Steam episode keeps its loud UX below).
        bool unverified = _steamSentry.IsolationUnverified;
        string? holder  = _steamSentry.UnverifiedHolder;
        if (unverified != _isolationUnverified || holder != _isolationUnverifiedHolder)
        {
            _isolationUnverified = unverified;
            _isolationUnverifiedHolder = holder;
            RefreshTrayText();
            RefreshArcadeGuard();
        }

        // Any state but Detected stands the card down — including Idle, which is what Passthru suspends an
        // episode to however it was engaged (tray, slice, auto-app, the card's own button).
        if (state != SteamSentry.State.Detected) { CloseSentryAlert(); return; }

        // Two gates, both required before the silent bounce (killing Steam kills its launched games):
        // DesktopIsIdle is a REAL idle test — PeekFrontmostApp's null also covered the taskbar and
        // Radiata's own windows, and reusing it here killed a running game (a tray click made Radiata
        // foreground, which read as "nothing in the foreground"). GameProcessRunning catches what no
        // foreground test can: a game that is minimised, on another monitor, or alt-tabbed away.
        if (_steamSentry.MayAutoRelaunch
            && _config.Current.System.SteamAutoRelaunch
            && WindowsPlatformActions.DesktopIsIdle()
            && !_platform.GameProcessRunning())
        {
            Trace.WriteLine("[Sentry] desktop idle, no game process — relaunching Steam silently");
            _ = AutoRelaunchSteamAsync();
            return;
        }
        ShowSentryAlert();
    }

    private async Task AutoRelaunchSteamAsync()
    {
        bool ok = await _steamSentry.RelaunchSteamAsync();
        if (ok)
        {
            CloseSentryAlert();
            ShowCornerToast(Loc.T(UiText.Toasts.SteamRestarted), Loc.T(UiText.Toasts.SteamHeldBody), NoticeTier.Info);
            RefreshSentryAfterBounce();
        }
        else ShowSentryAlert();   // couldn't bounce it — fall back to telling the user
    }

    /// <summary>A successful bounce changes the sentry's state with no capture pass to report it, and the
    /// card's poll stops with the card, so the tray would keep reading "not isolated (Steam)" until some
    /// unrelated pass. Success paths only: re-evaluating after a failed bounce could fire the automatic
    /// cure right behind a user-opted one.</summary>
    private void RefreshSentryAfterBounce()
    {
        if (!_shuttingDown) EvaluateSteamSentry(_sentryLastIsolated);
    }

    // ── User-opted Steam restart (OOBE step 1 checkbox; the update prompt via SteamRestartFlag) ──

    private bool _userSteamRestartPending;
    private long _userSteamRestartDeadline;   // TickCount64; pending must not fire hours later
    private DispatcherTimer? _userSteamRestartExpiryTimer;

    /// <summary>Queue a consented Steam bounce for the moment isolation is up — restarting it any earlier
    /// would just let it re-open the physical pad. Fires immediately if the cloak is already confirmed;
    /// otherwise NoteIsolationState fires it on the next isolated edge, within a 10-minute window (past
    /// that, a surprise Steam restart is worse than a stale unfulfilled opt-in).</summary>
    private void ArmUserSteamRestart()
    {
        if (!SteamSentry.SteamRunning()) return;
        if (_isolated) { _ = UserSteamRestartAsync(); return; }
        _userSteamRestartPending = true;
        _userSteamRestartDeadline = Environment.TickCount64 + 10 * 60_000;
        // Consent expires even if isolation never succeeds. The update quiet timer and visible-card
        // poll have different lifetimes and cannot own this request's deadline.
        _userSteamRestartExpiryTimer ??= CreateSteamRestartExpiryTimer();
        _userSteamRestartExpiryTimer.Stop();
        _userSteamRestartExpiryTimer.Interval = TimeSpan.FromMinutes(10);
        _userSteamRestartExpiryTimer.Start();
        Trace.WriteLine("[Sentry] user-opted Steam restart armed — waiting for isolation");
    }

    private bool ExpireUserSteamRestart(long now)
    {
        if (!_userSteamRestartPending || now < _userSteamRestartDeadline) return false;
        _userSteamRestartPending = false;
        _userSteamRestartExpiryTimer?.Stop();
        Trace.WriteLine("[Sentry] user-opted Steam restart expired — isolation took over 10 min");
        return true;
    }

    private DispatcherTimer CreateSteamRestartExpiryTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_shuttingDown || !_userSteamRestartPending) return;
            if (!ExpireUserSteamRestart(Environment.TickCount64))
            {
                // Dispatcher scheduling must not shorten consent if this tick arrives early.
                timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1,
                    _userSteamRestartDeadline - Environment.TickCount64));
                timer.Start();
                return;
            }
            // Reuse detection and onboarding/update deferrals. Expiry neither diagnoses a leak
            // nor grants a Steam restart; the existing automatic-cure policy remains separate.
            EvaluateSteamSentry(_sentryLastIsolated);
        };
        return timer;
    }

    private async Task UserSteamRestartAsync()
    {
        if (!SteamSentry.SteamRunning())
        {
            Trace.WriteLine("[Sentry] user-opted Steam restart skipped — Steam is no longer running");
            return;
        }
        // SteamSentry's own relaunch line is shared with the automatic cure; this is what tells them apart.
        Trace.WriteLine("[Sentry] user-opted Steam restart — firing (isolation is up)");
        bool ok = await _steamSentry.RestartSteamForUserAsync();
        if (ok)
        {
            ShowCornerToast(Loc.T(UiText.Toasts.SteamRestarted), Loc.T(UiText.Toasts.SteamNoLongerBlocking), NoticeTier.Info);
            RefreshSentryAfterBounce();
        }
        else
        {
            ShowCornerToast(Loc.T(UiText.Toasts.CouldntRestartSteam), Loc.T(UiText.Toasts.RestartSteamYourself));
            // The armed opt-in was deferring the sentry card; the cure failed, so a still-live episode
            // may now show it. Gated on Detected — a failed bounce must never fabricate an episode.
            if (_steamSentry.Current == SteamSentry.State.Detected) ShowSentryAlert();
        }
    }

    /// <summary>The persistent card: stays up until the pad
    /// disconnects or the user picks one of the two button actions — no auto-close, no ✕. It deliberately
    /// does NOT suppress the virtual pad: while this alert exists, ALL physical input already reaches
    /// Steam Input games, so consuming △/□ here can't stop the leak — one extra press is noise.</summary>
    private void ShowSentryAlert()
    {
        if (_sentryAlert is not null) return;
        // Two bounded deferrals — the LEAK is never suppressed (tray text, traces and the silent cures
        // all continue), only the card:
        // 1. Onboarding: a brand-new user's first minutes must not open with a persistent alarm whose
        //    button actions compete with the wizard — which already carries the pre-checked "Restart
        //    Steam" opt-in, the designed cure for this exact condition. The wizard's Closed handler
        //    re-runs capture, so a leak that survives onboarding raises the card then.
        if (_onboardingWindow is { IsLoaded: true })
        {
            TraceThrottled("sentry-oobe", "[Sentry] alert deferred — onboarding is open (re-evaluated when it closes)");
            return;
        }
        // 2. The post-update window / an armed consented restart: the silent update relaunches Radiata
        //    after Steam, manufacturing this exact condition — and when the update prompt's Restart
        //    Steam opt-in was taken, the cure is already queued. Re-evaluate once the window ends (the
        //    one-shot timer below), so an uncured leak still surfaces after the swap settles.
        if (Environment.TickCount64 < _sentryUpdateQuietUntilMs || _userSteamRestartPending)
        {
            TraceThrottled("sentry-quiet", "[Sentry] alert deferred — post-update quiet window / consented restart pending");
            if (_sentryQuietTimer is null && Environment.TickCount64 < _sentryUpdateQuietUntilMs)
            {
                _sentryQuietTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(
                        Math.Max(1000, _sentryUpdateQuietUntilMs - Environment.TickCount64 + 500)),
                };
                _sentryQuietTimer.Tick += (_, _) =>
                {
                    _sentryQuietTimer?.Stop();
                    _sentryQuietTimer = null;
                    EvaluateSteamSentry(_sentryLastIsolated);
                };
                _sentryQuietTimer.Start();
            }
            return;
        }

        // The text column sits beside the 58px mark (44 + its 14 margin); the option row below spans the full
        // card, under the mark too. 474 = three 150px tiles plus their 8px gutters, so the row holds one
        // line in every language — and the text column is what is left of that width beside the mark.
        const double CardContentWidth = 474, MarkColumn = 58;
        var text = new System.Windows.Controls.StackPanel { MaxWidth = CardContentWidth - MarkColumn };
        text.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Loc.T(UiText.Toasts.UnblockController),
            FontWeight = FontWeights.Bold, FontSize = 14,
            Foreground = System.Windows.Media.Brushes.White,
        });
        foreach (var line in new[]
                 {
                     Loc.T(UiText.Toasts.SentryLead),
                     Loc.T(UiText.Toasts.SentryChoose) + " " + Loc.T(UiText.Toasts.SentryStillWorks),
                 })
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 1, 0, 0),
            };
            InlineMarkup.Fill(tb, line, InlineMarkup.Card);   // the lines carry **bold** markup
            text.Children.Add(tb);
        }

        // The three cures as tiles rather than prose: the two that Radiata can perform are BUTTONS, and a
        // button that only a pad can press is a button half the users can't reach — so each carries its
        // drawn chip AND takes a mouse click, running the same handler. The reconnect tile is inert by
        // design: nothing here can unplug a controller, so it must not look pressable.
        // ⚠ Chips are DRAWN (ControllerButton → the shared ControllerButtons renderer), never font glyphs —
        // Segoe UI's □ sits low and small next to △ (the same fudge the glyph-set tiles route around).
        var options = new System.Windows.Controls.WrapPanel { Margin = new Thickness(0, 10, 0, 0) };

        System.Windows.Controls.Border OptionTile(PadButton? chip, string label, string? sub, Action? go,
                                                 double labelSize = 12.5)
        {
            var stack = new System.Windows.Controls.StackPanel
                { VerticalAlignment = VerticalAlignment.Center };
            var head  = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            if (chip is { } b)
                head.Children.Add(new ControllerButton
                    { Button = b, Height = 16, Margin = new Thickness(0, 0, 6, 0),
                      VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = label, TextWrapping = TextWrapping.Wrap, FontSize = labelSize,
                TextAlignment = TextAlignment.Center,
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = chip is null ? 130 : 108,   // the tile is 150 wide less its 9px padding; a chip takes 22
            });
            stack.Children.Add(head);
            if (sub is not null)
                stack.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = sub, TextWrapping = TextWrapping.Wrap, FontSize = 10.5,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                                     System.Windows.Media.Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
                    Margin = new Thickness(0, 2, 0, 0),
                });

            var idle  = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            var hover = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            var tile = new System.Windows.Controls.Border
            {
                Background      = idle,
                BorderBrush     = new System.Windows.Media.SolidColorBrush(
                                      System.Windows.Media.Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(9, 7, 9, 7),
                Margin          = new Thickness(0, 0, 8, 0),
                // FIXED, not MinWidth: a translation wider than its English would grow the tile until
                // the three no longer fit the row and the last one wrapped to a line of its own
                // A fixed cell makes the row's width a constant every language
                // meets, and a long label wraps DOWN inside its tile instead of pushing sideways.
                Width           = 150,
                Child           = stack,
            };
            if (go is not null)
            {
                tile.Cursor = System.Windows.Input.Cursors.Hand;
                tile.MouseEnter += (_, _) => tile.Background = hover;
                tile.MouseLeave += (_, _) => tile.Background = idle;
                // The card is ShowActivated=false and never takes focus, so the click arrives without
                // stealing the foreground from a game.
                tile.MouseLeftButtonUp += (_, _) => go();
                System.Windows.Automation.AutomationProperties.SetName(tile, sub is null ? label : $"{label}. {sub}");
            }
            return tile;
        }

        // A point smaller than the action tiles: this label is the longest of the three and has no chip to
        // share its line, so at the shared size it ran to three lines against their two.
        options.Children.Add(OptionTile(null, Loc.T(UiText.Toasts.SentryOptReconnect), null, null, labelSize: 11.5));
        options.Children.Add(OptionTile(PadButton.Triangle, Loc.T(UiText.Toasts.SentryOptRelaunch),
                                        Loc.T(UiText.Toasts.SentryOptRelaunchSub), SentryChoseRelaunch));
        options.Children.Add(OptionTile(PadButton.Square, Loc.T(UiText.Toasts.SentryOptPassthru),
                                        Loc.T(UiText.Toasts.SentryOptPassthruSub), SentryChoseSafeMode));

        // The white Radiata mark on the left names the sender — a bare topmost card gives the user no clue
        // which app is talking. Code-drawn (FlowerMarkControl); Play() respects Reduce Motion.
        var mark = new FlowerMarkControl(44) { MarkBrush = System.Windows.Media.Brushes.White };

        // A padlock badge on the mark's lower-right: the card's whole subject is a LOCKED controller, and
        // the badge says so before a word is read. Drawn, not a font glyph, and sat on its own dark disc —
        // an orange line straight over the white mark would break up against the petals.
        var lockInk = new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromArgb(0xFF, 0xF0, 0x92, 0x1E));
        var lockArt = new System.Windows.Controls.Canvas { Width = 12, Height = 12 };
        lockArt.Children.Add(new System.Windows.Shapes.Path
        {
            Data = System.Windows.Media.Geometry.Parse("M3.7,5.6 V4.2 A2.3,2.3 0 0 1 8.3,4.2 V5.6"),
            Stroke = lockInk, StrokeThickness = 1.5,
            StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
            StrokeEndLineCap   = System.Windows.Media.PenLineCap.Round,
        });
        lockArt.Children.Add(new System.Windows.Shapes.Path
        {
            Data = System.Windows.Media.Geometry.Parse(
                "M2.7,5.5 H9.3 A1.1,1.1 0 0 1 10.4,6.6 V10.3 A1.1,1.1 0 0 1 9.3,11.4 H2.7 A1.1,1.1 0 0 1 1.6,10.3 V6.6 A1.1,1.1 0 0 1 2.7,5.5 Z"),
            Fill = lockInk,
        });
        var badge = new System.Windows.Controls.Grid
        {
            Width = 19, Height = 19,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -3, -1),
        };
        badge.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Fill = new System.Windows.Media.SolidColorBrush(
                       System.Windows.Media.Color.FromArgb(0xFF, 0x20, 0x20, 0x20)),
        });
        badge.Children.Add(new System.Windows.Controls.Viewbox
            { Width = 13, Height = 13, Child = lockArt });

        var markLayers = new System.Windows.Controls.Grid { Width = 44, Height = 44 };
        markLayers.Children.Add(mark);
        markLayers.Children.Add(badge);

        var markHost = new System.Windows.Controls.Border
        {
            Width  = 44, Height = 44,
            Margin = new Thickness(0, 2, MarkColumn - 44, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child  = markLayers,
        };

        var head = new System.Windows.Controls.StackPanel
            { Orientation = System.Windows.Controls.Orientation.Horizontal };
        head.Children.Add(markHost);
        head.Children.Add(text);

        var row = new System.Windows.Controls.StackPanel { MaxWidth = CardContentWidth };
        row.Children.Add(head);
        row.Children.Add(options);

        var card = new System.Windows.Controls.Border
        {
            Background      = new System.Windows.Media.SolidColorBrush(
                                  System.Windows.Media.Color.FromArgb(0xF0, 0x20, 0x20, 0x20)),
            // The same orange as the padlock badge, so the card's two accents are one colour.
            BorderBrush     = lockInk,
            BorderThickness = new Thickness(1.5),
            CornerRadius    = new CornerRadius(12),
            Padding         = new Thickness(18, 14, 18, 14),
            Child           = row,
        };

        string body = Loc.F(UiText.Toasts.SentrySpoken, ControllerButtons.Spoken(PadButton.Triangle), ControllerButtons.Spoken(PadButton.Square));

        var wa = SystemParameters.WorkArea;
        var win = new Window
        {
            FlowDirection = LocWpf.Flow,
            WindowStyle         = WindowStyle.None,
            AllowsTransparency  = true,
            Background          = System.Windows.Media.Brushes.Transparent,
            Topmost             = true,
            ShowActivated       = false,
            ShowInTaskbar       = false,
            SizeToContent       = SizeToContent.WidthAndHeight,
            Content             = card,
        };
        // SizeToContent means the real size exists only after layout — a guessed offset bled the card off
        // the right edge. Pin the bottom-right corner from the MEASURED size, and re-pin if wrapping
        // changes it. (Captures the local, not the field — the field is nulled on close.)
        win.SizeChanged += (_, _) =>
        {
            // Top-right, same corner as the capture-state toast: Steam pops its
            // own controller add/remove cards bottom-right on exactly the events this card is about, and
            // this card would sit on top of them there.
            win.Left = wa.Right - win.ActualWidth - 24;
            win.Top  = wa.Top + 24;
        };
        _sentryAlert = win;
        _sentryAlert.Show();
        mark.Play(forward: true);   // Reduce Motion: appears fully formed
        Trace.WriteLine($"[Sentry] alert shown — waiting for a pad power-cycle, {ControllerButtons.Spoken(PadButton.Triangle)} relaunch, or {ControllerButtons.Spoken(PadButton.Square)} Passthru Mode");
        _announcer.Announce(body, AnnouncementKind.Context);

        // The two non-button cures happen outside our input handlers (the user restarts Steam themselves,
        // or Steam exits) — poll so the card stands down without requiring a capture pass.
        _sentryPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _sentryPoll.Tick += (_, _) => EvaluateSteamSentry(_sentryLastIsolated);
        _sentryPoll.Start();
    }

    /// <summary>A devnode this session holds cloaked left the device tree — the pad physically departed
    /// (unplug, power-off, Bluetooth drop, devnode restart), so every handle to it died, Steam's and any
    /// other pre-cloak holder's alike. The reconnect is born cloaked (the ids are retained).</summary>
    private void OnCloakedDevnodeDeparted(string instanceId)
    {
        Trace.WriteLine($"[Capture] cloaked devnode removed: {instanceId}");
        _steamSentry.OnPadDeparted();
        CloseSentryAlert();
        RefreshTrayText();
    }

    private void CloseSentryAlert()
    {
        _sentryPoll?.Stop();
        _sentryPoll = null;
        if (_sentryAlert is null) return;
        try { _sentryAlert.Close(); } catch { /* already closed by shutdown */ }
        _sentryAlert = null;
        Trace.WriteLine("[Sentry] alert closed");
    }

    private async void SentryChoseRelaunch()
    {
        CloseSentryAlert();
        ShowCornerToast(Loc.T(UiText.Toasts.RelaunchingSteam), Loc.T(UiText.Toasts.WaitingSteamExit));
        bool ok = await _steamSentry.RelaunchSteamAsync();
        // The outcome supersedes the progress card above, whose safety hold would otherwise drop it.
        _toasts.ReleaseSafetyHold();
        // Not "isolation is clean": the restart cures Steam's handle only, and the tray may still (rightly)
        // read "unverified" for another holder that predates the session.
        if (ok)
        {
            ShowCornerToast(Loc.T(UiText.Toasts.SteamRestarted), Loc.T(UiText.Toasts.SteamHeldBody), NoticeTier.Info);
            RefreshSentryAfterBounce();
        }
        else
        {
            ShowCornerToast(Loc.T(UiText.Toasts.SteamDidntRestart), Loc.T(UiText.Toasts.ReconnectInstead));
            ShowSentryAlert();
        }
    }

    private void SentryChoseSafeMode()
    {
        CloseSentryAlert();
        if (!_safeMode) ToggleSafeMode();   // the sentry suspends the episode while Passthru lasts
    }

    /// <summary>Whether Anticheat Passthru Mode is offerable at all: it only means anything when the isolation
    /// drivers are installed, since without them there is no virtual pad and no cloak to stand down.
    /// <para>THE one gate — <see cref="SettingsWindow"/> uses it for the Passthru Mode tab's visibility, the two
    /// slice editors use it to decide whether to offer the "Toggle Passthru Mode" action, and App uses it to
    /// decide whether to wire the executor's callback. Read once at startup like the tab's check: a driver
    /// install mid-session already needs a restart to take effect.</para></summary>
    internal static bool SafeModeAvailable =>
        DriverStatus.HidHideInstalled() && DriverStatus.ViGEmBusInstalled();

    /// <summary>Toggle anticheat passthru mode (the capture profile): SAFE = no virtual pad, no cloak — a
    /// zero-kernel-footprint mode for competitive kernel-anticheat games; FULL = the always-on pad + cloak.
    /// Persists to config, re-applies capture immediately, and confirms via a corner notice. The wheels stay
    /// usable either way (raw HID + overlay are anticheat-inert); passthru mode just accepts input bleed-through
    /// while a wheel is open, in exchange for showing the game no emulated/hidden device.</summary>
    private void ToggleSafeMode()
    {
        _safeMode = !_safeMode;
        ResetPadCountBreaker();   // a new capture profile starts with a clean churn history
        WriteSystem(_config.Current.System with { CaptureSafeMode = _safeMode });
        if (_safeMode) CancelPendingSafeModeOff();   // re-engaging makes any pending completion moot
        else BeginPendingSafeModeOff();              // may hold cloak + pad until the game is left
        UpdateInputCapture();   // release/re-present the pad + drop/restore the cloak to match
        Trace.WriteLine($"[Capture] anticheat passthru mode → {(_safeMode ? "ON (no pad/cloak)" : "OFF (full capture)")}"
                        + (_safeModeOffPending ? " — PENDING until the game is left" : ""));
        // A pending hold has ALREADY drawn the "Passthru Mode Ending" corner card in BeginPendingSafeModeOff,
        // and a second card here would repeat it almost word for word — this one covers the two states
        // nothing else announces (ON, and OFF with nothing pending). Info tier: a state confirmation
        // must never evict a live safety warning.
        // Either direction swaps the device under a running game, and games don't re-scan for a new pad —
        // so when any game is running (not just the foreground one: the tray and Settings take focus), the
        // body names it instead.
        if (!_safeModeOffPending)
        {
            // The running-game sweep walks the whole process table, so it runs off the dispatcher and the
            // toast follows it; this toggle fires from the tray, a card and a slice, all on the UI thread.
            bool on = _safeMode;
            _ = System.Threading.Tasks.Task.Run(() => _platform.RunningGameName()).ContinueWith(t =>
            {
                string? game = t.IsCompletedSuccessfully ? t.Result : null;
                Dispatcher.InvokeAsync(() =>
                {
                    // "Input isolation restored" is false while the Steam card is up — leaving Passthru can
                    // hand the pad back to Steam, and the card that says so is the one notice to keep.
                    if (!on && game is null
                        && _steamSentry.Current is SteamSentry.State.Detected or SteamSentry.State.Remediating)
                        return;
                    ShowCornerToast(
                        Loc.T(on ? UiText.Toasts.PassthruOn : UiText.Toasts.PassthruOff),
                        game is not null ? Loc.F(UiText.Toasts.PassthruRebindGame, game)
                        : on             ? Loc.T(UiText.Toasts.PassthruOnBody)
                                         : Loc.T(UiText.Toasts.PassthruOffBody),
                        NoticeTier.Info);
                });
            });
        }
    }

    // ── Pending passthru-mode-off hold ────────────────────────────────────────────
    // No latched target — don't reintroduce a PID guessed from the foreground: reaching the tray from a
    // fullscreen game hands the foreground to a shell surface (SearchApp), and for a Steam title the pad
    // handles are held by Steam, not the game, so a latched PID that never exits would be a PERMANENTLY
    // dead controller. The hold instead completes when nothing game-like is foregrounded (2s poll): a
    // wrong classification finishes the toggle early or late, never waits forever. Invariants:
    // time-bounded (PendingSafeModeOffTimeoutMs), cancelled by re-engaging Passthru Mode, and the game
    // keeps the REAL pad for the whole hold — cloak and virtual pad land TOGETHER at completion, because
    // cloaking mid-hold cuts an XInput game off instantly (XInput polls slots rather than holding a handle).
    private const int PendingSafeModeOffTimeoutMs = 120_000;

    /// <summary>Start the pending passthru-mode-off hold — unless nothing game-like is in front, in which
    /// case there is no swap to protect and the toggle simply completes on this capture pass.</summary>
    private void BeginPendingSafeModeOff()
    {
        if (_platform.DetectRunningGameName() is null) return;
        _safeModeOffPending = true;
        _safeModeOffPendingDeadlineMs = Environment.TickCount64 + PendingSafeModeOffTimeoutMs;
        _safeModeOffPendingWatch?.Stop();
        _safeModeOffPendingWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _safeModeOffPendingWatch.Tick += (_, _) => PollPendingSafeModeOff();
        _safeModeOffPendingWatch.Start();
        ShowCornerToast(Loc.T(UiText.Toasts.PassthruEnding), Loc.T(UiText.Toasts.PassthruEndingBody));
    }

    private void PollPendingSafeModeOff()
    {
        bool timedOut = Environment.TickCount64 > _safeModeOffPendingDeadlineMs;
        if (!timedOut && _platform.DetectRunningGameName() is not null) return;
        Trace.WriteLine(timedOut
            ? "[Capture] passthru mode off COMPLETE — hold deadline reached with a game still in front; applying cloak + virtual pad"
            : "[Capture] passthru mode off COMPLETE — no game in the foreground; applying cloak + virtual pad");
        CancelPendingSafeModeOff();
        UpdateInputCapture();
    }

    private void CancelPendingSafeModeOff()
    {
        _safeModeOffPendingWatch?.Stop();
        _safeModeOffPendingWatch = null;
        _safeModeOffPending = false;
    }

    // ── Automatic passthru mode (Settings ▸ Passthru Mode ▸ Always Use Passthru Mode) ─────

    /// <summary>Start/stop the Exceptions process watcher to match the configured list (called at startup
    /// and on every config reload). A light 2 s poll — nothing runs at all with an empty list. Matching is
    /// HidHide-style by full exe path (see <see cref="SafeModeApp"/>). The watcher drives ONLY the runtime
    /// _autoSafeMode flag; the persisted CaptureSafeMode toggle is never written by automation.</summary>
    private void ConfigureSafeModeWatcher()
    {
        if (_config.Current.System.SafeModeApps.Count == 0)
        {
            _safeModeWatch?.Stop();
            _safeModeWatch = null;
            if (_autoSafeMode)   // list emptied while engaged → restore full capture now
            {
                _autoSafeMode = false; _autoSafeModeApp = null;
                Trace.WriteLine("[Passthru] auto OFF — exceptions list emptied");
                UpdateInputCapture();
            }
            return;
        }
        if (_safeModeWatch is null)
        {
            _safeModeWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _safeModeWatch.Tick += (_, _) => PollSafeModeApps();
            _safeModeWatch.Start();
        }
        PollSafeModeApps();   // the list just (re)loaded — apply immediately, don't wait a tick
    }

    /// <summary>One watcher pass: is any configured exception running (or frontmost, per entry)? Two entry
    /// kinds (see <see cref="SafeModeApp"/>): an EXE entry matches by exe name, confirmed by image path
    /// when readable (an unreadable path — elevated/protected anticheat process — lets the name match
    /// stand); a FOLDER entry matches any process whose image path is INSIDE the game's install dir.
    /// Engage/disengage _autoSafeMode on a state CHANGE only (UpdateInputCapture swaps the pad + cloak, so
    /// it isn't free). Wrongly ENGAGING passthru mode is the low-harm direction.</summary>
    private void PollSafeModeApps()
    {
        string? hit = null;
        try
        {
            var apps = _config.Current.System.SafeModeApps;
            if (apps.Count == 0) return;   // ConfigureSafeModeWatcher owns teardown

            // Foreground pid, only resolved if some entry needs it.
            int fgPid = 0;
            if (apps.Any(a => a.FrontmostOnly))
            {
                NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint pid);
                fgPid = (int)pid;
            }

            // EXE entries keyed by exe name (cheap name pre-filter); FOLDER entries need each process's
            // image path (normalized to end with a separator so a prefix test can't match a sibling
            // "…\GameTwo" against "…\Game").
            var exeByName = new Dictionary<string, SafeModeApp>(StringComparer.OrdinalIgnoreCase);
            var folders   = new List<(string dir, SafeModeApp app)>();
            foreach (var a in apps)
            {
                if (string.IsNullOrEmpty(a.Path)) continue;
                if (a.MatchFolder)
                {
                    // A folder entry of a DRIVE ROOT ("C:\") normalizes to "C:\", which prefix-matches every
                    // process on that drive — passthru mode would latch on permanently and never release. That
                    // can only come from a hand-edited config, and it's never what anyone means.
                    var dir = a.Path.TrimEnd('\\') + "\\";
                    if (dir.Length <= 3)
                    {
                        Trace.WriteLine($"[Passthru] ignoring folder entry '{a.Path}' — a drive root matches every process");
                        continue;
                    }
                    folders.Add((dir, a));
                }
                else if (System.IO.Path.GetFileNameWithoutExtension(a.Path) is { Length: > 0 } n)
                    exeByName[n] = a;
            }

            var procs = Process.GetProcesses();   // one snapshot per pass; Process objects hold handles
            try
            {
                foreach (var p in procs)
                {
                    bool nameHit = exeByName.TryGetValue(p.ProcessName, out var exeApp);
                    // Image path: needed to CONFIRM an exe name-hit and to test any folder entry. Skip the
                    // (cheap-ish) lookup only when this process can't match anything.
                    if (!nameHit && folders.Count == 0) continue;
                    string? image = NativeMethods.ProcessImagePath(p.Id);

                    if (nameHit && (!exeApp!.FrontmostOnly || p.Id == fgPid)
                        && (image is null || string.Equals(image, exeApp.Path, StringComparison.OrdinalIgnoreCase)))
                    { hit = p.ProcessName; break; }

                    if (image is not null)
                        foreach (var (dir, app) in folders)
                        {
                            if (app.FrontmostOnly && p.Id != fgPid) continue;
                            if (image.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                            { hit = app.Name ?? new System.IO.DirectoryInfo(app.Path).Name; break; }
                        }
                    if (hit is not null) break;
                }
            }
            finally { foreach (var p in procs) p.Dispose(); }
        }
        catch (Exception ex) { Trace.WriteLine($"[Passthru] watcher poll failed: {ex.Message}"); }

        bool want = hit is not null;
        if (want == _autoSafeMode) { if (hit is not null) _autoSafeModeApp = hit; return; }
        _autoSafeMode    = want;
        _autoSafeModeApp = hit;
        Trace.WriteLine(want
            ? $"[Passthru] auto ON — exception running: {hit}"
            : "[Passthru] auto OFF — no exception running");
        UpdateInputCapture();
    }

    // Wheels enabled/disabled (chord / tray) — centred on screen. No text: the Radiata flower mark blooms in
    // for On, collapses (time-reversed) for Off, coloured to the wheel material (white on dark, black on
    // light). The toast circle itself scales in from centre before an On bloom, and shrinks to centre
    // after an Off collapse.
    // ⚠ This bloom STAYS CIRCULAR — it is exempt from the "standalone toasts are roundrect"
    // rule: a deliberate brand moment built around the round FlowerMarkControl. Don't convert it, and
    // don't "tidy" it later for consistency with the rectangular notices.
    private void ShowStatusToast(bool on)
    {
        if (_toasts.SafetyHoldActive) return;   // safety warning holds the slot
        var flower = new FlowerMarkControl(RadialMenuControl.InnerRadius * 1.2);
        var scr = NativeMethods.PrimaryScreenDips();   // centred on the primary screen
        // Mesa (Materials.Terra) draws the mark in its terracotta backdrop tone rather than the toast's
        // default ink, which reads as a foreign colour on the cream. Mark only; body text keeps the ink.
        // Raw token compare is safe — ConfigLoader.Sanitize canonicalizes SliceMaterial on load.
        bool terraMark = _config.Current.System.SliceMaterial == Materials.Terra;
        var (_, sc) = _toasts.BuildAndShowToast(ink =>
            { flower.MarkBrush = terraMark ? RadialMenuControl.TerraBlobFill : ink; return flower; },
            scr.X + scr.Width / 2.0, scr.Y + scr.Height / 2.0,
            initialScale: on ? 0 : 1);   // On starts collapsed (scale in before the bloom); Off starts full

        const double circleMs = 150;
        double flowerMs = flower.PlayDurationMs;
        if (on)   // circle scales in from nothing, THEN the flower blooms
        {
            ScaleToast(sc, 0, 1, 0, circleMs, System.Windows.Media.Animation.EasingMode.EaseOut, amplitude: 0.9);
            flower.Play(true, circleMs);
            _toasts.StartToastClose(circleMs + flowerMs + 450);
        }
        else      // flower collapses, THEN the circle shrinks to nothing
        {
            flower.Play(false, 0);
            ScaleToast(sc, 1, 0, flowerMs, circleMs, System.Windows.Media.Animation.EasingMode.EaseIn);
            _toasts.StartToastClose(flowerMs + circleMs + 60);
        }
    }

    private static void ScaleToast(System.Windows.Media.ScaleTransform sc, double from, double to,
                                   double delayMs, double moveMs, System.Windows.Media.Animation.EasingMode mode,
                                   double amplitude = 0.3)
    {
        // A spring overshoot — scale-in bounces past full, shrink-out winds up before collapsing.
        var ease = new System.Windows.Media.Animation.BackEase { EasingMode = mode, Amplitude = amplitude };
        System.Windows.Media.Animation.DoubleAnimation A() =>
            new(from, to, TimeSpan.FromMilliseconds(moveMs))
            { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = ease };
        sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, A());
        sc.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, A());
    }

    /// <summary>Status toast at a point. Wheel-anchored callers keep the round material-themed card;
    /// a STANDALONE caller (no wheel on screen) passes <paramref name="standalone"/> and gets the
    /// roundrect card instead — the shape is the caller's call because it depends on whether the toast
    /// is wheel-anchored, which this method cannot know.</summary>
    private void ShowStatusToast(string line1, string line2, double centerX, double centerY,
                                 int? holdMs = null, bool standalone = false)
    {
        if (_toasts.SafetyHoldActive) return;   // safety warning holds the slot
        if (standalone)
        {
            if (_toasts.ShowRectToast(line1, line2, centerX, centerY, NoticeTier.Info))
                _toasts.StartToastClose(holdMs ?? 1400);
            return;
        }
        _toasts.BuildAndShowToast(ink => new System.Windows.Controls.TextBlock
        {
            Text                = $"{line1}\n{line2}",
            FontSize            = 17,
            FontWeight          = FontWeights.SemiBold,
            TextAlignment       = System.Windows.TextAlignment.Center,
            Foreground          = ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        }, centerX, centerY);
        _toasts.StartToastClose(holdMs ?? 1400);
    }

    /// <summary>Self-drawn "controller not isolated" warning — the ONLY channel for this event, and it
    /// must stay self-drawn: Windows notifications depend on app identity (an exe with no Start-Menu
    /// shortcut/AUMID gets them dropped silently — the portable ZIP has neither), so a Windows
    /// notification must never be the only channel. The hub chip only shows while a wheel is open.</summary>
    // The stuck-root episode (docs/INPUT-CAPTURE.md ▸ Stuck root): one trace line and one card per episode,
    // keyed on the root id; the latch resets when the root gains children or leaves the tree.
    private string? _stuckRootEpisodeId;
    internal bool StuckRootEpisodeActive => _stuckRootEpisodeId is not null;

    private void UpdateStuckRootEpisode(IReadOnlyList<XboxStuckRootTracker.Entry> stuck)
    {
        if (stuck.Count == 0)
        {
            if (_stuckRootEpisodeId is null) return;
            _stuckRootEpisodeId = null;
            Dispatcher.BeginInvoke(() =>
            {
                // Only ours: another notice may have taken the slot since.
                if (!_stuckRootCardUp) return;
                _stuckRootCardUp = false;
                _toasts.CloseToasts();
                _toasts.ReleaseSafetyHold();
            });
            return;
        }
        var first = stuck[0];
        if (string.Equals(_stuckRootEpisodeId, first.Id, StringComparison.OrdinalIgnoreCase)) return;
        _stuckRootEpisodeId = first.Id;
        Trace.WriteLine($"[Capture] Xbox root {first.Id} has no input child after {first.ChildlessMs / 1000}s — controller needs a power-cycle; not cloaked");
        Dispatcher.BeginInvoke(() =>
        {
            if (_stuckRootEpisodeId is null) return;   // cleared before the card could show
            _stuckRootCardUp = true;
            ShowCornerToast(Loc.T(UiText.Toasts.ControllerNeedsRestart), Loc.T(UiText.Toasts.ControllerNeedsRestartBody),
                            holdMs: 30_000);
        });
    }
    private bool _stuckRootCardUp;

    private void ShowIsolationToast(string line2) => ShowCornerToast(Loc.T(UiText.Toasts.IsolationBlocked), line2);

    /// <summary>Top-right capture-state toast (isolation warnings, pending passthru-mode-off) — never
    /// centre-screen, a notice must not sit on top of the game's action / a wheel; and not bottom-right,
    /// where Steam pops its controller add/remove cards on exactly these events. No entrance animation:
    /// the notice is essential, so it appears fully formed and only the shared toast close moves
    /// (kept under Reduce Motion — opacity-only).
    /// <para>Standalone notices are ROUNDRECT: toasts that appear independently of an
    /// on-screen wheel use the sentry card's chrome; wheel-anchored status toasts stay round.</para></summary>
    private void ShowCornerToast(string line1, string line2, NoticeTier tier = NoticeTier.Safety,
                                 Action? onClick = null, int holdMs = 6000)
    {
        if (!_toasts.ShowRectToast(line1, line2, centerX: null, centerY: null, tier, onClick)) return;
        // Spoken as well as drawn: a corner notice is the only word the user gets for a controller that
        // stopped answering or a Steam bounce in progress, and a narration user was getting silence.
        // ⚠ AFTER the ShowRectToast guard, never before — a notice suppressed during onboarding, or an
        // info notice yielding to a live safety hold, never appeared and must not be announced either.
        _announcer.Announce($"{line1}, {line2}", AnnouncementKind.Context);
        // Only a safety notice holds the slot — an info hold would block a real warning for 6s.
        if (tier == NoticeTier.Safety) _toasts.HoldForSafety(holdMs);
        _toasts.StartToastClose(holdMs);
    }

    // ── Updates & crash reporting (mechanics live in UpdateService / CrashReporter) ──────────────

    /// <summary>Wire the update checker's hooks + schedule (nothing runs for ~60 s — off the launch
    /// critical path), and stage the pending-crash consent offer if last run left a report behind.</summary>
    private void InitUpdateAndCrashReporting()
    {
        UpdateService.GetSystem     = () => _config.Current.System;
        UpdateService.WriteSystem   = system => { WriteSystem(system); };
        UpdateService.RunWhenQuiet  = a => RunWhenQuiet(TimeSpan.FromSeconds(2), a);
        UpdateService.CanExitForUpdate = () =>
        {
            if (!Editing && _pendingWheelEdits.Count == 0) return true;
            System.Windows.MessageBox.Show(Loc.T(UiText.Settings.SaveWheelBeforeUpdate), "Radiata",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        };
        UpdateService.Notify        = NotifyUser;
        UpdateService.ExitForUpdate = ExitApp;   // the NORMAL shutdown path: TearDown (cloak lift) + Shutdown
        UpdateService.Start();

        // Crash-time driver one-liner: already-held fields only — never probe hardware in a crash handler.
        CrashReporter.DriverStateProvider = () =>
            $"isolated={_isolated} passthru={SafeModeActive} padPresent={_controllerPresentForPad} " +
            $"wheel={_overlayVisible} grid={_browserOpen} arcade={_arcadeOpen}";

        // A crash last run left a report: offer it once the tray settles — never modally at launch, and
        // only at a quiet moment (the consent window must never appear over a game or the overlay).
        if (_config.Current.System.CrashPromptEnabled && System.IO.File.Exists(CrashReporter.PendingPath))
            RunWhenQuiet(TimeSpan.FromSeconds(20), OfferPendingCrashReport);
    }

    private void OfferPendingCrashReport()
    {
        if (!_config.Current.System.CrashPromptEnabled) return;   // Settings may have opted out mid-wait
        var report = CrashReporter.ReadPending();
        if (report is null) return;
        if (_config.Current.System.CrashAutoSend)
        {
            Trace.WriteLine("[Crash] auto-sending last run's pending report");
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try { if (await CrashReporter.SendAsync(report)) CrashReporter.DeletePending(); }
                catch (Exception ex) { Trace.WriteLine($"[Crash] auto-send failed: {ex.Message}"); }
            });
            return;
        }
        Trace.WriteLine("[Crash] offering last run's pending report");
        new CrashReportWindow(report,
            disablePrompting: () => WriteSystem(_config.Current.System with { CrashPromptEnabled = false }),
            enableAutoSend: () => WriteSystem(_config.Current.System with { CrashAutoSend = true }))
            .Show();
    }

    /// <summary>Non-interrupting notification: the self-drawn corner card with an optional one-shot
    /// click action. Info tier — it must never displace a live safety warning, and dropping it is fine
    /// (Settings ▸ Advanced carries the persistent update line either way).</summary>
    private void NotifyUser(string title, string text, Action? onClick) =>
        ShowCornerToast(title, text, NoticeTier.Info, onClick, holdMs: 10000);

    /// <summary>Whether a prompt surface would land somewhere it must not: any modal overlay surface
    /// (wheel / edit / grid / arcade), the first-run wizard, or a game owning the foreground.</summary>
    private bool PromptSurfaceBusy()
    {
        if (_overlayVisible || Editing || _browserOpen || _arcadeOpen) return true;
        if (_onboardingWindow is { IsLoaded: true }) return true;
        try { return _platform.DetectRunningGameName() is not null; }
        catch { return false; }   // detection failure must not block the prompt forever
    }

    /// <summary>Run <paramref name="action"/> at the next quiet moment: first look after
    /// <paramref name="initialDelay"/>, then re-poll every 30 s until <see cref="PromptSurfaceBusy"/>
    /// clears. Used for the update notice and the crash consent window.</summary>
    private void RunWhenQuiet(TimeSpan initialDelay, Action action)
    {
        var timer = new DispatcherTimer { Interval = initialDelay };
        timer.Tick += (_, _) =>
        {
            if (PromptSurfaceBusy() || _pendingWheelEdits.Count != 0) { timer.Interval = TimeSpan.FromSeconds(30); return; }
            timer.Stop();
            action();
        };
        timer.Start();
    }

    // ── Teardown ──────────────────────────────────────────────────────────────

    private void ExitApp()
    {
        // Neutralize and close controller surfaces before entering a nested dialog message loop.
        // This also retains the current edit snapshot, if any.
        if (_pendingWheelEdits.Count != 0 || Editing) OnControllerLost();
        if (_pendingWheelEdits.Count != 0 &&
            System.Windows.MessageBox.Show(Loc.T(UiText.Settings.DiscardWheelEdits), "Radiata",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        // Every path here is a deliberate close inside this logon session; the marker keeps the late
        // Run-key autostart from undoing it. It expires with the session, so an update's exit-and-relaunch
        // (which never passes --autostart) is unaffected.
        QuitMarker.Write();
        TearDown();
        Shutdown();
    }

    /// <summary>The stranded-cloak recovery: lift the persisted cloak ids when the main app ISN'T running (it
    /// owns the cloak and the cloaked-ids.txt record). Two signals, either meaning "hands off": the
    /// single-instance mutex, and a live Radiata process by exe path — at logon this can run in the ms
    /// before an autostart primary claims the mutex, and the mutex alone would wrongly un-cloak the
    /// just-launched primary and delete its state file. Best effort; a failed recovery leaves the next
    /// launch's self-heal to finish the job.</summary>
    private static void RunUncloakOneShot()
    {
        try
        {
            bool primaryRunning;
            bool byMutex = System.Threading.Mutex.TryOpenExisting(InstanceMutexName, out var m);
            if (byMutex) { m?.Dispose(); primaryRunning = true; }
            else primaryRunning = AnotherRadiataRunning();
            var ids = HidHideManager.PersistedCloakedIds();
            // ⚠ Both halves, always: "primary running" is the one answer that makes this no-op, and a
            // lingering watchdog sharing the exe path can produce it wrongly — at which point the cloak
            // stays up and the user's pad stays invisible with nothing said. Unhide traces its own outcome.
            Trace.WriteLine($"[Uncloak] one-shot — primaryRunning={primaryRunning} (mutex={byMutex}) "
                            + $"persistedIds={ids.Count}");
            if (!primaryRunning) new HidHideManager().Unhide(ids);
        }
        catch (Exception ex) { Trace.WriteLine($"[Uncloak] one-shot failed: {ex.Message}"); }
    }

    // ── Uninstaller (elevated worker; the --uninstall-run entry is CliUninstall in App.Cli.cs) ──────────

    /// <summary>Remove Radiata's Windows footprint (via <see cref="RunCleanupCore"/>), then schedule the
    /// app folder to delete itself once this (elevated) process exits. Portable copies only — an
    /// installer-managed copy's files belong to the Inno uninstaller (--uninstall-cleanup).</summary>
    private bool RunUninstall(bool drivers, bool appData)
    {
        var (cleanupOk, driversMsg) = RunCleanupCore(drivers, appData);
        if (!cleanupOk)
        {
            System.Windows.MessageBox.Show(driversMsg, Loc.T(UiText.Dialogs.UninstallCaption), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Only auto-delete the folder if it's safely a Radiata install (never a drive/system root — a
        // user who extracted the ZIP's contents to C:\ must not have the drive wiped). Otherwise tell
        // them to delete it themselves.
        var appDir = AppContext.BaseDirectory;
        bool canDelete = IsDeletableAppFolder(appDir);
        System.Windows.MessageBox.Show(
            Loc.T(UiText.Dialogs.Uninstalled) + driversMsg
            + (canDelete ? "" : "\n\n" + Loc.T(UiText.Dialogs.DeleteFolderYourself) + "\n" + appDir),
            Loc.T(UiText.Dialogs.UninstallCaption), MessageBoxButton.OK, MessageBoxImage.Information);

        if (canDelete && !ScheduleFolderDelete(appDir))
        {
            System.Windows.MessageBox.Show(Loc.T(UiText.Dialogs.DeleteFolderYourself) + "\n" + appDir,
                Loc.T(UiText.Dialogs.UninstallCaption), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    /// <summary>The Windows-state cleanup shared by the portable uninstall (--uninstall-run) and the
    /// installer's cleanup hook (--uninstall-cleanup-run): stops the tray app, lifts the cloak and the
    /// HidHide allow-list entry, removes the Run-key entry, the logon recovery task, and the Arcade
    /// helper's stage-copy dirs; optionally removes the SHARED drivers and %APPDATA%\Radiata. NEVER
    /// touches application files — file deletion stays with each caller. Best-effort throughout.
    /// Returns whether the driver removal succeeded (true when not requested) plus the user-facing
    /// sentence describing what actually happened to the drivers.</summary>
    private (bool DriversOk, string DriversMsg) RunCleanupCore(bool drivers, bool appData)
    {
        if (!KillOtherRadiata()) return (false, Loc.T(UiText.Settings.RecoveryRequired));

        // A Radiata from ANOTHER folder survives that by design (KillOtherRadiata matches the exe path, so a
        // dev build or a kept ZIP copy beside an install is left alone) — and it owns a LIVE cloak it
        // believes in. So everything SHARED is left to it: lifting the driver's block list machine-wide
        // would leave that copy trusting its own record while games read the physical pad alongside the
        // virtual one — silent double input until its next capture pass — and pulling the drivers out from
        // under it breaks it outright. Only this install's per-path footprint goes.
        string? otherCopy = ForeignRadiataPath();
        if (otherCopy is not null)
            Trace.WriteLine($"[Uninstall] another Radiata is running from {otherCopy} — leaving the cloak, "
                            + "the block list and the shared drivers to it");

        bool cloakRecovered = otherCopy is null;
        // Undo the controller footprint: recover the complete block list and drop our
        // HidHide allow-list entry, then release the exclusive handle for the (optional) driver uninstall.
        try
        {
            var hh = new HidHideManager();
            if (otherCopy is null)
            {
                cloakRecovered = hh.UnhideForUninstall();
                if (!cloakRecovered) return (false, Loc.T(UiText.Settings.RecoveryRequired));
            }
            // Per-path, so it never touches the other copy's own entry.
            hh.DewhitelistApplication(Environment.ProcessPath ?? "");
            hh.Release();
        }
        catch (Exception ex) { Trace.WriteLine($"[Uninstall] HidHide cleanup: {ex.Message}"); return (false, Loc.T(UiText.Settings.RecoveryRequired)); }

        try { StartupManager.SetEnabled(false); } catch (Exception ex) { Trace.WriteLine($"[Uninstall] run-key: {ex.Message}"); }
        if (!RecoveryTask.Remove()) return (false, Loc.T(UiText.Settings.RecoveryRequired));
        // The Arcade helper's stage-copies live OUTSIDE the app folder (C:\Radiata-ArcadeHost-<hash>),
        // so neither the manifest self-delete nor the Inno uninstaller ever sees them.
        ScriptSessionCoordinator.DeleteOwnedStagedCopy();

        // driversMsg is appended to the completion dialog: describes what ACTUALLY happened to the drivers.
        string driversMsg;
        bool driversOk = true;
        if (otherCopy is not null)
        {
            // Both drivers are machine-wide and that copy is using them right now: HidHide's filter is what
            // hides its pad and ViGEmBus is what carries its virtual one, so removing either mid-session is
            // a dead controller for whoever is playing. Say which copy, so the cure is obvious.
            driversMsg = "\n\n" + Loc.F(UiText.Dialogs.DriversLeftOtherCopy, otherCopy);
        }
        else if (drivers)
        {
            // The driver uninstallers (WiX bundles) fail with an access/in-use error if the pad devnode or
            // the HidHide filter is still attached — which lingers briefly after the tray app is killed, and
            // can persist until reboot on an install that never re-enumerated (see the HidHide gotcha #3).
            // Wait for the pad/cloak to actually release before uninstalling, and REPORT the real uninstall
            // result rather than discarding it.
            WaitForControllerDevicesToRelease();
            var hh = DriverSetup.UninstallHidHide();    // control device freed above (+ the tray app was killed)
            var vg = DriverSetup.UninstallViGEmBus();
            driversOk = DriverSetup.NeedsNoUserAction(hh) && DriverSetup.NeedsNoUserAction(vg);
            driversMsg = "\n\n" + DriverOutcomeLine("HidHide", hh) + "\n\n" + DriverOutcomeLine("ViGEmBus", vg);
        }
        else
        {
            driversMsg = "\n\n" + Loc.T(UiText.Dialogs.DriversLeft);
        }

        // Gated on a TRUE recovery: with another copy running, %APPDATA%\Radiata holds that copy's live recovery
        // record, so the wipe is skipped even when opted in.
        if (appData && cloakRecovered)
        {
            // This process's own elevated trace lives INSIDE the folder being wiped and is held open by its
            // listener, so the recursive delete fails on it and leaves the folder behind. Say goodbye, close
            // the listener, then delete; the driver outcome above is already on disk in the other log.
            Trace.WriteLine("[Uninstall] app data wipe requested — closing this trace first");
            DetachElevatedTrace();
            try { if (System.IO.Directory.Exists(AppPaths.AppDataDir)) System.IO.Directory.Delete(AppPaths.AppDataDir, recursive: true); }
            catch (Exception ex) { Trace.WriteLine($"[Uninstall] app data: {ex.Message}"); return (false, driversMsg + "\n\n" + Loc.F(UiText.Settings.SaveFailed, ex.Message)); }
        }

        // A cloak left to another running copy is handled, not failed: that copy owns it and driversMsg already says
        // so. The only unrecovered-cloak failure is UnhideForUninstall() returning false, which returned above.
        bool cloakHandled = cloakRecovered || otherCopy is not null;
        if (!cloakHandled)
            driversMsg += "\n\n" + Loc.T(UiText.Settings.RecoveryRequired);
        return (driversOk && cloakHandled, driversMsg);
    }

    /// <summary>The user-facing sentence for one driver's removal outcome (see
    /// <see cref="DriverSetup.UninstallOutcome"/>). A foreign copy is reported as left in place, not as a
    /// failure — it belongs to whatever installed it.</summary>
    private static string DriverOutcomeLine(string driver, DriverSetup.UninstallOutcome outcome) => outcome switch
    {
        DriverSetup.UninstallOutcome.Removed or DriverSetup.UninstallOutcome.PendingReboot
            => Loc.F(UiText.Dialogs.DriverRemoved, driver),
        DriverSetup.UninstallOutcome.NotInstalled  => Loc.F(UiText.Dialogs.DriverNotInstalled, driver),
        DriverSetup.UninstallOutcome.NoUninstaller => Loc.F(UiText.Dialogs.DriverForeign, driver),
        _                                          => Loc.F(UiText.Dialogs.DriverNotRemoved, driver),
    };

    /// <summary>After the tray app is killed, its ViGEm virtual pad devnode and HidHide filter take a moment
    /// to detach in the kernel — and the driver uninstallers fail with an access/in-use error if they run
    /// while those are still attached. Give them up to ~3 s to clear (no other Radiata process AND the
    /// HidHide control device openable) before the uninstall proceeds. Best-effort; never throws.</summary>
    private static void WaitForControllerDevicesToRelease()
    {
        for (int i = 0; i < 15; i++)   // ~3 s max (15 × 200 ms)
        {
            if (AnotherRadiataRunning()) { System.Threading.Thread.Sleep(200); continue; }
            System.Threading.Thread.Sleep(200);   // let ViGEmBus reclaim the virtual PDO after the owner died
            break;
        }
    }

    /// <summary>True if another instance of THIS installed Radiata.exe is running (same exe path, not this
    /// process). Used by the --uncloak recovery one-shot to avoid touching the cloak while the primary is
    /// live but hasn't yet claimed the single-instance mutex (logon startup race).</summary>
    private static bool AnotherRadiataRunning()
    {
        int me = Environment.ProcessId;
        var mine = Environment.ProcessPath;
        if (string.IsNullOrEmpty(mine)) return false;
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Radiata"))
        {
            try
            {
                if (p.Id != me && !IsWatchdogProcess(p.Id)
                    && string.Equals(p.MainModule?.FileName, mine, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { /* MainModule denied / process gone → ignore */ }
            finally { p.Dispose(); }
        }
        return false;
    }

    // ── Ride-along watchdog (primary side; the child is CliWatchdog in App.Cli.cs) ──────────────────────

    private static string WatchdogMutexName(int pid) => @"Local\RadiataWatchdog-" + pid;

    /// <summary>True if the given pid is a ride-along watchdog (holds its per-pid identity mutex). A
    /// watchdog IS a Radiata.exe at this install's path, so without this check the logon --uncloak
    /// recovery task would read a lingering watchdog as "primary running" and wrongly no-op — re-stranding
    /// the very cloak the watchdog exists to lift. KillOtherRadiata deliberately does NOT skip watchdogs:
    /// uninstall should take them down too, and RunUninstall lifts the persisted cloak right after.</summary>
    private static bool IsWatchdogProcess(int pid)
    {
        try
        {
            if (System.Threading.Mutex.TryOpenExisting(WatchdogMutexName(pid), out var m)) { m.Dispose(); return true; }
        }
        catch { /* access denied on the mutex → not one of ours */ }
        return false;
    }

    private System.Diagnostics.Process? _watchdog;
    private int _watchdogRespawns;
    private const int WatchdogRespawnBudget = 3;

    /// <summary>Launch (or relaunch) the ride-along watchdog: `Radiata.exe --watchdog &lt;pid&gt;
    /// &lt;startFileTimeUtc&gt;`. Best-effort — on failure the crash hooks, startup self-heal, and logon
    /// recovery task still stand; the watchdog only narrows the hard-kill window from "next sign-in" to
    /// seconds.</summary>
    private void SpawnWatchdog()
    {
        if (_shuttingDown) return;
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            long start = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToFileTimeUtc();
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                exe, $"--watchdog {Environment.ProcessId} {start}")
            { UseShellExecute = false, CreateNoWindow = true });
            if (p is null) { Trace.WriteLine("[Watchdog] spawn returned no process"); return; }
            _watchdog = p;
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) => Dispatcher.BeginInvoke(new Action(OnWatchdogExited));
            Trace.WriteLine($"[Watchdog] spawned pid {p.Id}");
        }
        catch (Exception ex) { Trace.WriteLine($"[Watchdog] spawn failed: {ex.Message}"); }
    }

    /// <summary>The watchdog should outlive us, not the reverse — if it dies mid-session (killed, crashed),
    /// respawn it a few times with a backoff, then give up loudly: the hard-kill safety net degrades to
    /// the logon recovery task, which still covers the user at next sign-in.</summary>
    private void OnWatchdogExited()
    {
        _watchdog?.Dispose();
        _watchdog = null;
        if (_shuttingDown) return;
        if (++_watchdogRespawns > WatchdogRespawnBudget)
        {
            Trace.WriteLine("[Watchdog] gave up respawning — hard-kill un-cloak degraded to logon recovery");
            return;
        }
        Trace.WriteLine($"[Watchdog] exited unexpectedly — respawn {_watchdogRespawns}/{WatchdogRespawnBudget} in 10 s");
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        t.Tick += (_, _) => { t.Stop(); SpawnWatchdog(); };
        t.Start();
    }

    /// <summary>Route an elevated one-shot's trace to <c>radiata-trace-elevated.log</c>. The primary holds
    /// <c>radiata-trace.log</c> write-locked, so a one-shot that does not attach this writes nowhere at all.
    /// Every elevated branch (whitelist, both uninstall workers, the cleanup parent's failure path) must
    /// call it. Never throws.</summary>
    private static void AttachElevatedTrace()
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            _elevatedListener = new TimestampTraceListener(
                System.IO.Path.Combine(AppPaths.AppDataDir, "radiata-trace-elevated.log"), shortLived: true);
            Trace.Listeners.Add(_elevatedListener);
            Trace.AutoFlush = true;
        }
        catch { /* diagnostics must never block the work */ }
    }

    private static TimestampTraceListener? _elevatedListener;

    /// <summary>Close the elevated trace so the file it holds can be deleted (the app-data wipe). Nothing
    /// written after this reaches disk.</summary>
    private static void DetachElevatedTrace()
    {
        try
        {
            if (_elevatedListener is null) return;
            Trace.Flush();
            Trace.Listeners.Remove(_elevatedListener);
            _elevatedListener.Dispose();
            _elevatedListener = null;
        }
        catch { /* best-effort */ }
    }


    /// <summary>The exe path of a running Radiata from a DIFFERENT folder, or null. That copy survives
    /// <see cref="KillOtherRadiata"/> deliberately — it is not this install — and it still owns a live cloak,
    /// its own allow-list entry and a virtual pad, so the uninstall leaves every SHARED thing to it.
    /// Watchdogs are skipped: one shares its primary's path, so it can never be the foreign copy anyway, and
    /// reading it as one would be a second way to get the same wrong answer.</summary>
    private static string? ForeignRadiataPath()
    {
        int me = Environment.ProcessId;
        var mine = Environment.ProcessPath;
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Radiata"))
        {
            try
            {
                if (p.Id == me || IsWatchdogProcess(p.Id)) continue;
                // Same fallback as KillOtherRadiata: MainModule is denied across an elevation boundary, and
                // this worker is the elevated side of one.
                string? path;
                try { path = p.MainModule?.FileName; }
                catch { path = QueryProcessImagePath(p.Id); }
                if (!string.IsNullOrEmpty(path) && !string.Equals(path, mine, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            catch { /* can't read (already gone, path unreadable) → not something we can name */ }
            finally { p.Dispose(); }
        }
        return null;
    }
    /// <summary>Hard-stop every OTHER Radiata process (the running tray app) so its file/handle/device locks
    /// release. A kill skips its un-cloak hooks, but <see cref="RunUninstall"/> lifts the persisted cloak
    /// right after.</summary>
    private static bool KillOtherRadiata()
    {
        int me = Environment.ProcessId;
        string? mine = Environment.ProcessPath;
        using var self = Process.GetCurrentProcess();
        var targets = new List<Process>();
        try
        {
            foreach (var process in Process.GetProcessesByName("Radiata"))
            {
                bool retained = false;
                try
                {
                    if (process.Id == me) continue;
                    string? path = QueryProcessImagePath(process.Id);
                    if (path is null)
                    {
                        if (!process.HasExited) return false;
                        continue;
                    }
                    if (!string.Equals(path, mine, StringComparison.OrdinalIgnoreCase)) continue;
                    // A second account/session using this exact binary must keep its process and drivers.
                    if (process.SessionId != self.SessionId || ProcessOwnerSid(process) != RecoveryTask.CurrentSid)
                        return false;
                    targets.Add(process);
                    retained = true;
                }
                finally { if (!retained) process.Dispose(); }
            }
            foreach (var process in targets)
            {
                if (process.HasExited) continue;
                process.Kill();
                if (!process.WaitForExit(5000)) return false;
            }
            return true;
        }
        catch (Exception ex) { Trace.WriteLine($"[Uninstall] could not stop this installation: {ex.Message}"); return false; }
        finally { foreach (var process in targets) process.Dispose(); }
    }

    private static string? ProcessOwnerSid(Process process)
    {
        if (!NativeMethods.OpenProcessToken(process.Handle, 8, out var token)) return null; // TOKEN_QUERY
        try
        {
            using var identity = new System.Security.Principal.WindowsIdentity(token);
            return identity.User?.Value;
        }
        finally { NativeMethods.CloseHandle(token); }
    }

    private static string? QueryProcessImagePath(int pid)
    {
        const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        nint h = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == 0) return null;
        try
        {
            var sb = new System.Text.StringBuilder(1024);
            uint len = (uint)sb.Capacity;
            return NativeMethods.QueryFullProcessImageNameW(h, 0, sb, ref len) ? sb.ToString(0, (int)len) : null;
        }
        finally { NativeMethods.CloseHandle(h); }
    }

    /// <summary>Whether <paramref name="dir"/> is safe for the uninstaller to recursive-delete: it must
    /// actually CONTAIN Radiata.exe and must NOT be a drive root or a system/profile root. Without this,
    /// a user who extracts the ZIP's CONTENTS (not the folder) to C:\, their profile, or Documents would
    /// have that whole location wiped by the self-delete.
    /// <para>Not the only line of defense — <see cref="ScheduleFolderDelete"/> deletes a manifest
    /// rather than recursing — but kept as defense-in-depth.</para></summary>
    private static bool IsDeletableAppFolder(string dir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            var full = System.IO.Path.GetFullPath(dir).TrimEnd('\\', '/');
            if (full.Length == 0) return false;
            if (!System.IO.File.Exists(System.IO.Path.Combine(full, "Radiata.exe"))) return false;   // must be OUR folder
            var root = System.IO.Path.GetPathRoot(full)?.TrimEnd('\\', '/') ?? "";
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return false;                  // never a drive root
            foreach (var sf in new[]
            {
                Environment.SpecialFolder.Windows,        Environment.SpecialFolder.System,
                Environment.SpecialFolder.ProgramFiles,   Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.UserProfile,    Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.MyDocuments,    Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.CommonApplicationData,
            })
            {
                var p = Environment.GetFolderPath(sf)?.TrimEnd('\\', '/');
                if (!string.IsNullOrEmpty(p) && full.Equals(p, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }
        catch { return false; }
    }

    /// <summary>The entries Radiata puts in its own install folder — the ZIP's whole payload (see
    /// docs/BUILD-RELEASE.md: exe + loose <c>drivers\</c> + uninstaller + README), plus the .pdb a local
    /// Debug/Release build leaves beside the exe. This is the DELETE MANIFEST; see
    /// <see cref="ScheduleFolderDelete"/> for why uninstall works from a manifest and not a recursive wipe.</summary>
    private static readonly string[] OwnedFiles = PortableUninstall.Files.ToArray();
    private static readonly string[] OwnedDirs = []; // No recursively owned directories.

    /// <summary>Spawn a detached, hidden cmd that waits for THIS process to exit (releasing the exe lock),
    /// then removes the files Radiata owns and finally itself. Runs from %TEMP% so it never holds the folder
    /// open. Best-effort.
    ///
    /// <para>⚠ This deletes a MANIFEST (<see cref="OwnedFiles"/> / <see cref="OwnedDirs"/>) and then removes
    /// the folder with a non-recursive <c>rd</c>, which succeeds only if nothing else is left.
    /// <see cref="IsDeletableAppFolder"/> guards against a wholesale <c>rmdir /s /q</c> on the whole install
    /// directory — drive roots, the profile, Documents — but it cannot block the realistic failure: extract
    /// the ZIP's CONTENTS into a folder that already holds other things (<c>C:\Games</c> is the obvious one)
    /// and Radiata.exe sits beside them, satisfying every guard, so a wholesale wipe would take the lot. A
    /// manifest has no such failure mode: anything we didn't ship simply survives, and the folder stays
    /// behind holding it. The guard is kept as defense-in-depth.</para></summary>
    private static bool ScheduleFolderDelete(string appDir)
    {
        if (!IsDeletableAppFolder(appDir))
        {
            Trace.WriteLine($"[Uninstall] refusing to self-delete unsafe path: {appDir}");
            return false;
        }
        try
        {
            using var owner = Process.GetCurrentProcess();
            string script = PortableUninstall.Script(appDir, owner.Id, owner.StartTime.ToUniversalTime().Ticks);
            string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            if (encoded.Length > 30000) throw new System.IO.IOException("Portable uninstall command exceeds its safe size bound.");
            // Encoded command avoids a writable script file and cmd's percent expansion / AutoRun.
            var powershell = System.IO.Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            using var worker = Process.Start(new ProcessStartInfo(powershell)
            {
                Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + encoded,
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Environment.SystemDirectory,
            });
            return worker is not null;
        }
        catch (Exception ex) { Trace.WriteLine($"[Uninstall] folder-delete schedule failed: {ex.Message}"); return false; }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CrashReporter.MarkShuttingDown();
        TearDown();
        base.OnExit(e);
    }

    /// <summary>Last-ditch un-cloak for crash / Environment.Exit paths that skip <see cref="TearDown"/>.
    /// HidHide's block list persists in the driver across process death, so without this a crash could
    /// leave the DualSense hidden from every app. Idempotent and best-effort; skipped after a clean exit
    /// (TearDown already un-cloaked). The persisted cloak-state file backstops a hard kill that skips even
    /// this handler.</summary>
    private int _crashUncloakGuard;   // one-shot: the three hooks can fire on multiple threads at once
    private void CrashSafeUncloak()
    {
        if (_shuttingDown) return;                                                    // clean exit already un-cloaked
        if (System.Threading.Interlocked.Exchange(ref _crashUncloakGuard, 1) != 0) return;   // run exactly once
        // Unhide (not Release): un-cloak without Release's GC.Collect/WaitForPendingFinalizers, which could
        // hang a ProcessExit handler. Un-cloaking doesn't need the exclusive-handle release that GC forces.
        try { _hidHide.Unhide(); } catch { /* dying anyway — best effort */ }
    }

    private void TearDown()
    {
        // Early-exit instances (duplicate launch, elevated one-shot) never initialized capture: their
        // HidHideManager is empty, and disposing it would run an empty-state Unhide whose PersistHidden
        // deletes the PRIMARY instance's live cloaked-ids.txt — the crash-recovery record. Marking
        // _shuttingDown keeps the ProcessExit hook (CrashSafeUncloak) off the same empty Unhide path.
        if (_earlyExit) { _shuttingDown = true; return; }
        _shuttingDown = true;   // stop OnSettingsClosed from re-triggering ExitApp during teardown
        CrashReporter.MarkShuttingDown();
        ReleaseTaskSwitcher();  // exiting mid-Alt-Tab must not strand Alt down on the user's desktop
        ResetBrowserStick();
        _toasts.StopCloseTimer();
        _userSteamRestartExpiryTimer?.Stop();
        _toasts.CloseToasts();
        // Freeze an open arcade before anything is disposed — quitting with a game up should cost the same as
        // dismissing it (see ArcadeStore: dismissal IS the save checkpoint, so exit has to be one too).
        if (_arcadeOpen) { _arcadeOpen = false; _overlay?.HideArcade(); } else _overlay?.ArcadePersist();
        ArcadeShots.WaitPending(300);   // bounded: the write is atomic, so a timeout only keeps the older shot
        // Script-game helper processes die here (kill + job-handle close; kill-on-job-close makes the
        // crash path free — a dead Radiata takes its helpers with it without this ever running).
        ScriptSessionCoordinator.ShutdownAll();
        SfxEngine.Shutdown();   // release the audio endpoint (normally already idle-closed)

        // Hide the tray icon first — prevents ghost icon if the process is killed
        // before Dispose() completes.
        if (_trayIcon is not null) _trayIcon.Visible = false;

        // The registration is bound to the sink's window, so it is released before the window goes;
        // zeroed so a repeated teardown is a no-op.
        if (_deviceNotifyHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterDeviceNotification(_deviceNotifyHandle);
            _deviceNotifyHandle = IntPtr.Zero;
        }
        if (_hotkeySource is not null)
        {
            _hotkeySource.Dispose();   // no F1/F2/F3 to unregister; disposing the sink drops any on-demand hotkeys
            _hotkeySource = null;
        }
        _config?.Dispose();    // null if we exited early as a duplicate instance
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _controller.Dispose();
        _hidHide.Dispose();    // un-cloak the DualSense (never leave it hidden after we exit)
        _emulator.Dispose();   // disconnect the virtual pad if emulation was on
        _trayIcon?.Dispose();

        _showWait?.Unregister(null);
        _quitWait?.Unregister(null);
        _showRequest?.Dispose();
        _installWait?.Unregister(null);
        _installRequest?.Dispose();
        if (_instanceMutex is not null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { /* not owned (shouldn't happen on primary) */ }
            _instanceMutex.Dispose();
        }
    }
}
