using O = ControllerWheel.UiText.Onboarding;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
// WinForms is also referenced (tray icon), so disambiguate the controls we construct in code
// (Button is already aliased app-wide in GlobalUsings).
using CheckBox    = System.Windows.Controls.CheckBox;
using Ellipse     = System.Windows.Shapes.Ellipse;

namespace ControllerWheel;

/// <summary>First-run setup wizard (the OOBE flow's desktop half — mouse/keyboard, like Settings).
/// Steps (see the _steps list): Welcome (controller detection + drivers, merged) → Try it out (live
/// trigger practice) → Material (look pick; also sets the sound theme) → Assets (cover-art source) →
/// Customize (informational storefront cards + starter wheels, applied LIVE) → Roll out (finish +
/// good-to-know caveats). (Discord voice-channel setup is not an onboarding step — it's configured
/// on demand from a voice-channel slice, or in Settings ▸ Advanced.)
/// Auto-shown at startup while <c>App.NeedsOnboarding</c>; re-run from the tray. Closing without
/// finishing leaves the gate armed, so it shows again on the next launch AND on a tray left-click, and
/// <see cref="SystemConfig.OobeStep"/> brings it back to the step it was closed on; Finish records the
/// OOBE version and clears the resume point.</summary>
public partial class OnboardingWindow : Window
{
    /// <summary>Everything the wizard needs from App, as delegates — keeps the window free of app state.
    /// All are invoked on the UI thread. (Static services — GameLibrary, LauncherCatalog, GameArt,
    /// AudioDeviceSwitcher, HdrState, PlayniteLibrary — are called directly.)</summary>
    public sealed record Hooks(
        Func<bool>         ControllerConnected,
        Func<string>       ControllerName,
        Func<ControllerKind> Kind,
        Func<bool>         HasAdaptiveTriggers,   // Welcome step's caveat card: distinguishes DualSense from DualShock 4 (both PlayStationOther)
        Func<bool>         ViGEmInstalled,
        Func<string?>      ViGEmForeign,             // the bus is another program's fork, by name (HP OMEN Gaming Hub…); null = Nefarius's or absent
        Func<string?>      HidHideVersion,
        Func<bool>         HidHideUpdateAvailable,   // installed but older than the bundled installer
        Func<bool>         HidGuardianPresent,
        Func<IReadOnlyList<string>> RunningControllerTools,   // peer tools (DS4Windows/reWASD/…) running now
        Func<Task<List<string>>> RunDriverEngine,
        Func<SystemConfig> GetSystem,
        Func<SystemConfig, bool> WriteSystem,
        Func<(WheelSlice[] a, WheelSlice[] b)> GetWheels,
        Func<WheelSlice[], WheelSlice[], bool> WriteWheels,
        Action<bool>       SetOobePreview,       // true on every step except the finish: preview hub + no real fire
        Action<bool>       SetSampleWheels,      // practice step: show a stand-in wheel when the real one is empty
        Action<Action<string?>?> SetWheelInvoked, // practice step: fired when a wheel blooms, with the trigger token
        Action<bool>       SetInvokeLocked,      // pre-practice steps: wheel invocation disabled entirely
        Action<bool, IReadOnlyList<string>?> SetPracticeAll,   // practice step: make these gesture tokens live at once (modes ignored when off; the wizard always passes an explicit list when on)
        Action<Action?>    SetChordPracticed,    // practice step: fired when the enable/disable chord toggles
        Action<Action<string, bool>?> SetChordButtonListener,   // practice: raw button up/down feed ("l1","ps","dpad",…) for the live Hold/Tap halves
        Action<string?>    SetPracticeChordToken, // practice: ONLY this token's enable/disable chord is live (null = unrestricted)
        Action             EnsureWheelsEnabled,  // leaving practice: undo a chord left in the disabled state
        Func<bool>         OverlayVisible,       // practice step: a wheel is currently up
        Action<bool>       OpenWheel,            // Material step Preview button: open a wheel (true = Right)
        Action<Func<int, int, bool>?> SetOverlayClickExempt,   // screen-px hit test whose hits DON'T dismiss an open wheel (null = none)
        Func<InstalledGame, Task<WheelSlice>> BuildGameSlice,   // Customize step: build a slice for a picked game
        Action<string?>    OpenSettingsPage,     // finish-step cards: open Settings (null), or at "help"/"advanced"
        Func<System.Windows.Forms.NotifyIcon?> GetTrayIcon,   // finish-step tray highlight: the app's tray icon (for its screen rect)
        Func<bool>         SteamRunning,        // welcome step: gates the "Restart Steam" checkbox
        Action             RequestSteamRestart, // welcome step: bounce Steam once the cloak is up (App.ArmUserSteamRestart)
        Action             RequestRestart,     // Welcome step: relaunch after a language pick (the wizard resumes on that step)
        Func<bool>         Finish);

    private sealed record Step(string Title, FrameworkElement Body, Func<bool> CanNext, bool Skippable, Action? OnEnter = null);

    private readonly Hooks _hooks;
    private readonly Step[] _steps;
    private readonly DispatcherTimer _poll;   // live status refresh (controller hot-plug, driver probes)
    // Material step only: Preview button enable/disable. Constructed in the field initializer (not the
    // ctor body) because ShowStep(0) runs before the ctor's timer setup and already touches it.
    private readonly DispatcherTimer _previewPoll = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly bool _discordInstalled;
    private readonly bool _playnitePresent;
    private int _index;
    private bool _installing;
    private bool _artLoading;                                  // guard radio Checked events during setup
    private Task<InstalledGame[]>? _scan;   // one scan shared by Customize (stores + wheels), the starter preload, and the art prefetch
    private (WheelSlice[] a, WheelSlice[] b)? _starter;        // built preview, applied on Next if opted in
    private readonly bool _startedCustomized;                  // wheels were user-customized at OOBE start (never pre-load/stomp)

    public OnboardingWindow(Hooks hooks)
    {
        InitializeComponent();
        // The practice filmstrip stays left-to-right (its slide math depends on it); the CARD columns inside it read
        // in the run's language, so the Hold/Tap halves and the custom chord row lay out right-to-left in Arabic.
        RecColStack.FlowDirection    = LocWpf.Flow;
        CustomColStack.FlowDirection = LocWpf.Flow;
        LocWpf.ApplyTo(this);
        InitChordLegend();
        AccessibilityIntroRun.Text = Loc.T(O.AccessibilityIntro);
        InitLanguagePicker();
        // Same circled-plus joiner as the Customize card's Hold/Tap dropdowns (BuildCustomChordRow) — set
        // here rather than in XAML since PackIconHelper.FromName is a plain method, not a markup extension.
        RecPlus.Source = PackIconHelper.FromName("PlusCircle",
            new SolidColorBrush((Color)System.Windows.Application.Current.Resources["UiMutedInkColor"]));
        // Footer "Accessibility..." button carries the MDI wheelchair glyph left of its text — same
        // set-in-code pattern as RecPlus above.
        AccessibilityGlyph.Source = PackIconHelper.FromName("WheelchairAccessibility",
            new SolidColorBrush((Color)System.Windows.Application.Current.Resources["UiBodyInkColor"]));
        _hooks = hooks;
        _discordInstalled = DetectDiscord();
        _playnitePresent  = PlayniteLibrary.IsAvailable;

        // Flow order: Welcome (controller + drivers) → Try it out (learn the invocation gesture) →
        // Material (look + sound theme) → Assets (game art — BEFORE Customize so the SGDB key exists when
        // Customize's add-game builds slices → rich logos, and the art prefetch warms underneath
        // Customize) → Customize (storefronts + starter slices) → Roll out.
        var steps = new List<Step>
        {
            // Welcome: controller detection AND the two drivers on one page. Next needs a pad (Skip lets a
            // padless user pass); a drivers-missing prompt fires on Next/Skip.
            new(Loc.T(O.StepWelcome), StepWelcome,    () => _hooks.ControllerConnected(), Skippable: true),
        };
        // Practice is ON RAILS (Next unlocks once every invocation option has been tried; no controller =
        // nothing to compel). The wheels it practises on are pre-loaded by PreloadStarterWheelsAsync.
        steps.Add(new(Loc.T(O.StepPractice), StepPractice,
            () => PracticeSatisfied || !_hooks.ControllerConnected(), Skippable: true, OnEnter: EnterPractice));
        steps.Add(new(Loc.T(O.StepMaterial), StepLook,   () => true, Skippable: false, OnEnter: EnterLook));
        steps.Add(new(Loc.T(O.StepAssets), StepArt,        ArtStepValid, Skippable: false, OnEnter: EnterArt));
        // Customize = storefronts + starting slices merged onto one step (EnterInitialSlices runs both scans).
        steps.Add(new(Loc.T(O.StepCustomize), StepWheels,     () => true, Skippable: false, OnEnter: EnterInitialSlices));
        // The final step's Title doubles as its sign-off heading (it renders through the shared StepTitle
        // block, beside the flower mark) — hence a sentence rather than a "Roll Out"-style label.
        steps.Add(new(Loc.T(O.StepFinish), StepFinish, () => true, Skippable: false, OnEnter: EnterCaveats));
        _steps = [.. steps];

        BuildDots();
        BuildTrayIconPreview();
        // Resume where an unfinished run left off (see SystemConfig.OobeStep). ShowStep clamps, so a step
        // index written by a build with more steps lands on the last one rather than out of range.
        ShowStep(_hooks.GetSystem().OobeStep);

        // Fresh install (wheels still the shipped defaults): pre-load the capability-gated starter layout in
        // the background so the practice step (and any early peek) shows the appropriate slices, not the
        // shipped placeholders. Captured BEFORE the pre-load write so the Customize step still treats a fresh
        // install as fresh (pre-ticked). A customized install is never stomped.
        _startedCustomized = WheelsAreCustomized();
        PreloadStarterWheelsAsync();

        // Testers copy driver logs / status lines / instructions out of the wizard — make its text
        // selectable (fail-soft; interactive labels are skipped). Re-run per step in ShowStep.
        Loaded += (_, _) => SelectableText.EnableWithin(this);

        // 1s poll keeps the controller + driver statuses live (hot-plugging a pad mid-step just works).
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _poll.Tick += (_, _) => RefreshStatus();
        _poll.Start();

        // Separate, faster tick for two overlay-visibility watchers — the 1s status poll is too coarse for
        // either. Material step: the Preview button's enabled state, re-enabled promptly once the previewed
        // wheel is dismissed. Practice step: the up→down edge that reveals the enable/disable card.
        _previewPoll.Tick += (_, _) => { SyncPreviewButton(); PollWheelRelease(); };

        Closed += (_, _) => { _poll.Stop(); _previewPoll.Stop(); };
    }

    private bool DriversSatisfied() => _hooks.ViGEmInstalled() && _hooks.HidHideVersion() is not null;

    // Welcome-step controller glyphs (Material "controller" / "controller-off"), tinted by state and built
    // once. FromName returns null if the icon name is missing — the Image just shows nothing then.
    private ImageSource? _padGlyphOn, _padGlyphOff;
    private ImageSource? PadGlyphOn   => _padGlyphOn   ??= PackIconHelper.FromName("Controller",    FreezeBrush(Color.FromRgb(0x89, 0xB2, 0x9A)));
    private ImageSource? PadGlyphOff  => _padGlyphOff  ??= PackIconHelper.FromName("ControllerOff", FreezeBrush(Color.FromRgb(0x88, 0x88, 0x93)));

    // The tray tip card's swatch holds TWO flowers in one footprint: the dark glyph that matches the other
    // card icons, and the colour-sampled animated mark stacked exactly over it. The step's arrival sequence
    // blooms the colour mark, fades the dark glyph in behind it (hidden by it), then flies the colour mark
    // down to the real tray icon — leaving the dark glyph as the card's resting state.
    private Viewbox? _trayGlyphBox;
    private AboutFlowerControl? _trayFlower;

    /// <summary>Fill the finish step's tray-icon swatch: the dark flower glyph (starting invisible) with the
    /// animated colour mark over it.</summary>
    private void BuildTrayIconPreview()
    {
        _trayGlyphBox = new Viewbox
        {
            Margin = new Thickness(2), Opacity = 0,
            Child = new System.Windows.Shapes.Path
            {
                Data = FlowerIcon.Geometry(),
                Fill = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),   // dark ink, like the emoji glyphs
            },
        };
        // Hidden until the arrival sequence reveals it, so Reduce Motion — which skips that sequence — never
        // shows a colour mark it has no way to animate away. Hidden, not Collapsed: it must keep its layout
        // slot, since both the flight's start rect and its snapshot are measured off this element.
        _trayFlower = new AboutFlowerControl { Margin = new Thickness(2), Visibility = Visibility.Hidden };
        // The glyph is strictly BEHIND the mark — the two silhouettes don't match exactly, and dark ink
        // drawn over the colour would read as grime on it. Declaration order already says so; the explicit
        // ZIndex keeps it true if anything is ever added to this Grid.
        System.Windows.Controls.Panel.SetZIndex(_trayGlyphBox, 0);
        System.Windows.Controls.Panel.SetZIndex(_trayFlower,   1);
        TrayIconPreview.Child = new Grid { Children = { _trayGlyphBox, _trayFlower } };
    }

    private static bool DetectDiscord()
    {
        try
        {
            return File.Exists(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Discord\Update.exe"))
                   || WindowsPlatformActions.AnySessionProcess("Discord");
        }
        catch { return false; }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] open url failed: {ex.Message}"); }
    }

    /// <summary>The one storefront scan shared by the storefront + starter-wheel steps.</summary>
    private Task<InstalledGame[]> EnsureScan() => _scan ??= Task.Run(GameLibrary.Scan);

    private Task<int>? _playniteCount;
    /// <summary>Playnite's installed-game count (cached; 0 when Playnite isn't present or unreadable). Read
    /// off the UI thread — GetInstalledGames copies + parses the LiteDB library.</summary>
    private Task<int> EnsurePlayniteCount() =>
        _playniteCount ??= Task.Run(() => PlayniteLibrary.IsAvailable ? (PlayniteLibrary.GetInstalledGames()?.Length ?? 0) : 0);

    // ── Step navigation ───────────────────────────────────────────────────────

    private void ShowStep(int index)
    {
        _index = Math.Clamp(index, 0, _steps.Length - 1);
        // Persist the landing step so closing the window part-way resumes here rather than at Welcome.
        // Finishing clears it (App.MarkOnboarded), so this only ever describes an INCOMPLETE run.
        if (_hooks.GetSystem() is { } sysStep && sysStep.OobeStep != _index)
            _hooks.WriteSystem(sysStep with { OobeStep = _index });
        for (int i = 0; i < _steps.Length; i++)
            _steps[i].Body.Visibility = i == _index ? Visibility.Visible : Visibility.Collapsed;

        StepTitle.Text   = _steps[_index].Title;
        // The finish step reads through the SAME title block as every other step (its Title is the sign-off
        // line) — only the flower mark and the live status line are step-specific. Keeping one title block
        // is what guarantees the heading size and the dots below it don't shift on this step. The flower
        // auto-replays when it becomes visible (AboutFlowerControl.IsVisibleChanged → Play).
        bool onFinish = _steps[_index].Body == StepFinish;
        FinishSummary.Visibility = onFinish ? Visibility.Visible : Visibility.Collapsed;
        // The "start with Windows" checkbox lives in the footer button row but only makes sense on the
        // last step (it's read/committed in BtnNext_Click's Finish branch).
        StartupCheck.Visibility = onFinish ? Visibility.Visible : Visibility.Collapsed;
        StepCounter.Text = Loc.F(O.StepCounter, _index + 1, _steps.Length);
        BtnBack.Visibility = _index > 0 ? Visibility.Visible : Visibility.Hidden;
        // Wheel watermark on every step EXCEPT practice (its controller illustration owns that corner).
        WheelWatermark.Visibility = _steps[_index].Body == StepPractice ? Visibility.Collapsed : Visibility.Visible;
        // From the practice step on, ✕/○ on the controller also drive Next/Back (unlabelled).
        BtnNext.Content = Loc.T(_index == _steps.Length - 1 ? O.Finish : O.Next);
        BtnBack.Content = Loc.T(O.Back);
        // The practice step's Skip skips the on-rails gesture try-out specifically — say so. Its
        // Recommended|Custom segmented toggle (header, right of the title) exists only there.
        BtnSkip.Content = Loc.T(_steps[_index].Body == StepPractice ? O.SkipTesting : O.Skip);
        PracticeModeToggle.Visibility = _steps[_index].Body == StepPractice ? Visibility.Visible : Visibility.Collapsed;
        LanguageRow.Visibility = _steps[_index].Body == StepWelcome ? Visibility.Visible : Visibility.Collapsed;
        BtnReset.Visibility = _steps[_index].Body == StepWheels ? Visibility.Visible : Visibility.Collapsed;
        // The Material step gets a Preview button (opens the Right wheel so the look pick shows instantly)
        // and, to its LEFT, the Accessibility button (reveals the checkbox row under the thickness tiles).
        BtnPreviewWheel.Visibility = _steps[_index].Body == StepLook ? Visibility.Visible : Visibility.Collapsed;
        BtnAccessibility.Visibility = _steps[_index].Body == StepLook ? Visibility.Visible : Visibility.Collapsed;
        // Material step only: clicking a material/thickness tile must NOT dismiss a previewed wheel, since
        // those tiles apply live to it — clicking anywhere else still does. Cleared on every other step so
        // the exemption can't linger where those tiles aren't even on screen.
        _hooks.SetOverlayClickExempt(_steps[_index].Body == StepLook ? IsPointOverLookTiles : null);
        // Preview starts each visit enabled and its debounce clock clear; poll only while it's on screen.
        _previewReadyAt = DateTime.MinValue;
        _previewWasVisible = false;
        BtnPreviewWheel.IsEnabled = true;
        // The fast tick also drives the practice step's wheel-release watch, so it runs there too.
        _practiceWheelWasUp = false;
        if (BtnPreviewWheel.Visibility == Visibility.Visible || _steps[_index].Body == StepPractice)
            _previewPoll.Start();
        else
            _previewPoll.Stop();
        UpdateDots();
        // The wheels DEBUT at the practice step: every earlier step keeps invocation locked entirely
        // (fresh users shouldn't stumble into a wheel before the flow introduces it).
        int practiceIdx = Array.FindIndex(_steps, s => s.Body == StepPractice);
        _hooks.SetInvokeLocked(_index < practiceIdx);
        // Preview mode (hub "Preview" chip + no real fire) on every step EXCEPT the final "Roll out": there
        // the wheels are fully live so the user leaves setup with a working, real wheel.
        _hooks.SetOobePreview(!onFinish);
        // The practice step's live hooks (sample wheel + invoke check-off + all-gestures mode + chord
        // callback) belong to that step only — turn them off for every other step (EnterPractice turns
        // them back on), and undo a chord that left the wheels disabled.
        if (_steps[_index].Body != StepPractice)
        {
            _hooks.SetSampleWheels(false);
            _hooks.SetWheelInvoked(null);
            _hooks.SetChordPracticed(null);
            _hooks.SetChordButtonListener(null);
            _hooks.SetPracticeAll(false, null);
            StopChordCycle();   // the on-arrival illustration cycle is practice-only
        }
        // Re-enable the wheels on EVERY step change: the practice step's enable/disable chord can leave
        // them off, and moving to the next OR previous step should always restore them (they can still be
        // toggled again within the practice step). Harmless on pre-practice steps (invocation stays locked).
        _hooks.EnsureWheelsEnabled();
        _steps[_index].OnEnter?.Invoke();
        StepScroller.ScrollToTop();   // each step starts at the top (a tall step could leave it scrolled down)
        RefreshStatus();
        SelectableText.EnableWithin(this);   // catch this step's code-built text (practice rows, tiles)
    }

    // Navigation debounce: a double-click (or a bouncy ✕) must not skip through two steps.
    private long _lastNavTick;
    private bool NavDebounced()
    {
        long now = Environment.TickCount64;
        if (now - _lastNavTick < 450) return true;
        _lastNavTick = now;
        return false;
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        // _addingGame matches Next's guard: an in-flight add-game slice build must finish on ITS step,
        // not mutate the wheel list from underneath a different one.
        if (_installing || _addingGame || NavDebounced()) return;
        ShowStep(_index - 1);
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        if (_installing || NavDebounced()) return;
        if (_steps[_index].Body == StepWelcome && !ConfirmLeaveDriversStep()) return;
        if (_steps[_index].Body == StepWelcome) CommitSteamRestartChoice();
        // Skip Testing on the practice step commits the chord shown in the footer ("Use {…}" = ActiveChord)
        // even if nothing was performed, so skipping still adopts the on-screen selection rather than
        // silently leaving the prior/default trigger in place.
        if (_steps[_index].Body == StepPractice && !SavePracticeChord(ActiveChord)) return;
        ShowStep(_index + 1);
    }

    /// <summary>Leaving the Get-ready step without the isolation drivers: true = proceed. The drivers are
    /// an OPTIONAL tier, so this is one honest heads-up, not a warning gate. It applies to EVERY pad kind —
    /// Xbox capture is on by default, so ViGEm + HidHide are load-bearing for XInput pads too.
    /// <para>The proceed button reads "Skip Drivers" from BOTH entry points (Next and Skip): the dialog
    /// asks "Skip drivers?", so a "Continue" button would answer a question it wasn't asked.</para></summary>
    private bool ConfirmLeaveDriversStep()
    {
        if (DriversSatisfied()) return true;
        bool proceed = ConfirmChoice(Loc.T(O.SkipDriversTitle), Loc.T(O.SkipDriversBody),
            stayLabel: Loc.T(O.InstallDrivers), proceedLabel: Loc.T(O.SkipDrivers), showHeading: true);
        // An explicit "Skip Drivers" is a durable opt-out: the missing-driver isolation warnings stay
        // quiet for the chosen state (cleared at startup once both drivers are detected installed).
        if (proceed && !_hooks.WriteSystem(_hooks.GetSystem() with { DriversDeclined = true })) return false;
        return proceed;
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_installing || _addingGame || NavDebounced()) return;

        var body = _steps[_index].Body;

        // Proceeding past the combined welcome/drivers step without the isolation drivers gets one honest
        // heads-up. Skip shares it.
        if (body == StepWelcome && !ConfirmLeaveDriversStep()) return;
        if (body == StepWelcome) CommitSteamRestartChoice();

        // Leaving the art step with no SteamGridDB key = most games get no art. Picking "Configure" on the
        // prompt switches the drop-down to the SGDB (Use both) option so its key field is shown.
        if (body == StepArt && string.IsNullOrWhiteSpace(_hooks.GetSystem().SteamGridDbKey)
            && !ConfirmChoice(Loc.T(O.AreYouSure), Loc.T(O.SkipArtBody),
                   stayLabel: Loc.T(O.Configure), proceedLabel: Loc.T(O.Skip)))
        {
            ArtSelectedIndex = 0;   // "Configure" → the SGDB (Use both) option
            return;
        }

        // Steps whose choices commit when the user moves FORWARD (Back never commits).
        if (body == StepArt) { if (!ApplyArtChoice()) return; _ = KickArtPrefetchAsync(); }
        else if (body == StepWheels && !ApplyWheelsChoice()) return;
        // Practice commits the chord the footer is PROMISING ("Continue with {ActiveChord}") — the same
        // thing Skip commits. Both sides may have been practised; the visible one is the user's answer.
        else if (body == StepPractice && !SavePracticeChord(ActiveChord)) return;

        if (_index == _steps.Length - 1)
        {
            if (!_hooks.Finish()) return; // Keep the wizard open if completion could not be saved.
            StartupManager.SetEnabled(StartupCheck.IsChecked == true);
            Close();
            return;
        }
        ShowStep(_index + 1);
    }

    /// <summary>On the practice step, the face buttons ARE the wheel's aim/fire/cancel — they must not
    /// double as Back/Next until practice is cleared (both the chosen invocation AND its enable/disable
    /// chord performed). Blocks ✕ AND ○ from navigating while the step is unsatisfied; once cleared they
    /// resume driving Next/Back like every other step.</summary>
    private bool PracticeNavBlocked => _steps[_index].Body == StepPractice && !PracticeSatisfied;

    /// <summary>Controller ✕ (A on Xbox) pressed with no overlay/grid consuming it: acts as Next on EVERY
    /// step (goal: the whole flow is doable pad-only). Runs the SAME handler, so commits/alerts/debounce
    /// all apply.</summary>
    public void ControllerNext()
    {
        if (_modalDialog is not null) { _modalProceed?.Invoke(); return; }
        if (PracticeNavBlocked) return;
        if (BtnNext.IsEnabled) BtnNext_Click(BtnNext, new RoutedEventArgs());
    }

    /// <summary>Controller ○ (B on Xbox) pressed with no overlay/grid consuming it: acts as Back on every step.</summary>
    public void ControllerBack()
    {
        if (_modalDialog is not null) { _modalStay?.Invoke(); return; }
        if (PracticeNavBlocked) return;
        if (_index > 0) BtnBack_Click(BtnBack, new RoutedEventArgs());
    }

    /// <summary>Controller △ pressed with nothing else consuming it: toggles the Accessibility panel on the
    /// Material step; no-op elsewhere and while a confirm dialog is up.</summary>
    public void ControllerTriangle()
    {
        if (_modalDialog is not null) return;
        if (_steps[_index].Body != StepLook) return;
        BtnAccessibility_Click(BtnAccessibility, new RoutedEventArgs());
    }

    // Material tiles as laid out on screen: column 0 = the Simple group, columns 1-3 = the Deluxe grid.
    private static readonly string?[][] MaterialNavGrid =
    [
        ["flat-light", "pearl",    "mesa",    "kawaii"],
        ["flat-dark",  "obsidian", "salvage", "reactor"],
    ];

    /// <summary>D-pad press edge (dx/dy each -1, 0 or +1): moves the selected material across the on-screen
    /// tile layout and applies it live, clamped at the edges. Only the Material step reacts; on the practice
    /// step the d-pad is an invocation chord.</summary>
    public void ControllerDpad(int dx, int dy)
    {
        if (_modalDialog is not null) return;
        if (_steps[_index].Body != StepLook) return;
        var current = _hooks.GetSystem().SliceMaterial;
        int row = 0, col = 0;
        for (int r = 0; r < MaterialNavGrid.Length; r++)
            for (int c = 0; c < MaterialNavGrid[r].Length; c++)
                if (MaterialNavGrid[r][c] == current) { row = r; col = c; }
        row = Math.Clamp(row + dy, 0, MaterialNavGrid.Length - 1);
        col = Math.Clamp(col + dx, 0, MaterialNavGrid[0].Length - 1);
        var token = MaterialNavGrid[row][col];
        if (token is not null && token != current) SelectMaterialTile(token, apply: true);
    }

    private bool _startupInitialized;      // seed StartupCheck to checked exactly once (don't stomp a user toggle)
    private bool _tipsExpanded;            // "More Tips…" clicked — stays expanded for the rest of the session

    private void EnterCaveats()
    {
        CardSteam.Visibility = Visibility.Collapsed;   // permanently hidden

        // The rest of the pool stays collapsed until "More Tips…" is clicked (once expanded, re-entering
        // this step via Back/Next should not re-collapse it).
        if (!_tipsExpanded)
        {
            CardAnticheat.Visibility = Visibility.Collapsed;
            CardVolume.Visibility = Visibility.Collapsed;
            CardSettings.Visibility = Visibility.Collapsed;
            CardHelp.Visibility = Visibility.Collapsed;
        }
        ApplyCardSizing();
        BalanceCaveatColumns();

        // Default the "start with Windows" box to CHECKED on first entry (onboarding = first run, so the
        // Run key normally doesn't exist yet). Only seed once so re-entering this step (Back/Next) never
        // overwrites a choice the user already made in this session.
        if (!_startupInitialized)
        {
            StartupCheck.IsChecked = true;
            _startupInitialized = true;
        }

        // Point at the tray flower the CardTray copy describes — once per session, fail-soft.
        // Deferred to Loaded priority: the highlight now STARTS on the card's own flower swatch, and
        // PointToScreen on it is meaningless until this step's layout pass (which BalanceCaveatColumns
        // above has only just invalidated) has actually run.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(TryHighlightTrayIcon));
    }

    /// <summary>Distribute the VISIBLE good-to-know cards across the two finish columns so they end up
    /// roughly equal height (a greedy "add to the shorter column" pass, weighted by each card's text
    /// length). Order is preserved within each column. Collapsed cards (Steam) return to the pool. Safe to
    /// call repeatedly — it detaches from wherever each card currently sits first.</summary>
    private void BalanceCaveatColumns()
    {
        var cards = new[] { CardEdit, CardAnticheat, CardSteam, CardBorderless, CardTray, CardVolume, CardSettings, CardHelp };
        foreach (var c in cards) (c.Parent as System.Windows.Controls.Panel)?.Children.Remove(c);
        (MoreTipsCard.Parent as System.Windows.Controls.Panel)?.Children.Remove(MoreTipsCard);
        CaveatLeftCol.Children.Clear();
        CaveatMidCol.Children.Clear();
        CaveatRightCol.Children.Clear();
        foreach (var c in cards.Where(c => c.Visibility != Visibility.Visible)) CaveatPool.Children.Add(c);

        // Two columns on arrival, three once the pool is revealed (the middle column is zero-width while
        // collapsed, so the outer two still split the row in half).
        var cols = _tipsExpanded
            ? new[] { CaveatLeftCol, CaveatMidCol, CaveatRightCol }
            : new[] { CaveatLeftCol, CaveatRightCol };
        // ActualWidth is 0 the first time through (the step hasn't been laid out yet) — fall back to the
        // fixed content width: 680 window − chrome − the 28px page margins, plus this Grid's -4 bleed.
        double gridW = CaveatGrid.ActualWidth > 40 ? CaveatGrid.ActualWidth : 616;
        double colW = gridW / cols.Length;
        var used = new double[cols.Length];

        // Explicitly placed cards; the greedy pass below skips them.
        var pinned = new List<Border>();
        void Place(Border card, int col)
        {
            cols[col].Children.Add(card);
            used[col] += CardHeight(card, colW);
            pinned.Add(card);
        }

        // Pinned tops: Edit leads the first column, Tray the second (both always visible).
        Place(CardEdit, 0);
        Place(CardTray, 1);

        // Two more placements are pinned rather than balanced: Borderless joins the LEFT column in both
        // states, and Settings takes the MIDDLE column once the pool is revealed.
        Place(CardBorderless, 0);
        if (_tipsExpanded) Place(CardSettings, 1);   // cols[1] is the middle column only when expanded

        // Everything else visible: tallest-first into whichever column is currently shortest (LPT bin
        // packing) — with three columns this is what keeps the newly-revealed tips from all landing in one.
        foreach (var c in cards.Where(c => !pinned.Contains(c) && c.Visibility == Visibility.Visible)
                                .OrderByDescending(c => CardHeight(c, colW)))
        {
            int i = Array.IndexOf(used, used.Min());
            cols[i].Children.Add(c);
            used[i] += CardHeight(c, colW);
        }

        // "More Tips…" sits at the foot of the right column until clicked, then it's gone for good.
        if (!_tipsExpanded) CaveatRightCol.Children.Add(MoreTipsCard);
        else CaveatPool.Children.Add(MoreTipsCard);
    }

    /// <summary>Estimated rendered height of a card in a column `colW` wide. Wrapping is what actually
    /// drives a card's height, so this counts LINES (chars ÷ chars-per-line at the current tier's font),
    /// not raw characters — a raw char count treats a wide column and a narrow one alike and packs the
    /// columns badly once there are three of them.</summary>
    private double CardHeight(DependencyObject card, double colW)
    {
        double glyphW = _tipsExpanded ? 30 : 54;    // glyph (or tray preview) width + its right margin
        double pad    = _tipsExpanded ? 9 : 12;
        double marg   = _tipsExpanded ? 3 : 4;
        double font   = _tipsExpanded ? 10.5 : 15;  // must track CardText{Medium,Big}
        double lineH  = _tipsExpanded ? 14 : 20;
        double textW  = Math.Max(40, colW - marg * 2 - pad * 2 - glyphW);

        double chars = 0;
        foreach (var tb in LogicalTextBlocks(card))
        {
            // Skip the glyph TextBlock: every glyph is a single emoji (1-2 UTF-16 units), never body copy.
            double n = TextLength(tb);
            if (n > 2) chars += TextAdvance(tb);
        }
        double lines = Math.Max(1, Math.Ceiling(chars * font / textW));   // chars is already in em units
        return lines * lineH + pad * 2 + marg * 2;
    }

    /// <summary>Copy width in em units — ~0.5em per Latin glyph, a full em per CJK/full-width glyph. A raw
    /// character count under-measures Japanese by about half and packs the columns badly.</summary>
    private static double TextAdvance(System.Windows.Controls.TextBlock tb)
    {
        static double Em(string? s) { double e = 0; foreach (var c in s ?? "") e += HelpHtmlExport.IsWide(c) ? 1.0 : 0.5; return e; }
        double runs = 0;
        foreach (var inline in tb.Inlines)
            if (inline is System.Windows.Documents.Run r) runs += Em(r.Text);
        return Math.Max(Em(tb.Text), runs);
    }

    /// <summary>Character count of a TextBlock's copy. ⚠ `TextBlock.Text` does NOT surface copy authored as
    /// `&lt;Run&gt;` inlines (it reads back empty), so the inlines are summed as well and the larger count
    /// wins — trusting `.Text` alone makes Run-built cards measure as one line and packs the columns badly.</summary>
    private static double TextLength(System.Windows.Controls.TextBlock tb)
    {
        double runs = 0;
        foreach (var inline in tb.Inlines)
            if (inline is System.Windows.Documents.Run r) runs += r.Text?.Length ?? 0;
        return Math.Max(tb.Text?.Length ?? 0, runs);
    }

    /// <summary>The four headline cards arrive Big; clicking "More Tips…" shrinks them (and sizes the
    /// newly-revealed pool cards) to Medium — a size step down, but still above the original baseline.</summary>
    private void ApplyCardSizing()
    {
        var glyphStyle = (Style)FindResource(_tipsExpanded ? "CardGlyphMedium" : "CardGlyphBig");
        var textStyle  = (Style)FindResource(_tipsExpanded ? "CardTextMedium"  : "CardTextBig");
        CardEditGlyph.Style = glyphStyle;         CardEditText.Style = textStyle;
        CardBorderlessGlyph.Style = glyphStyle;   CardBorderlessText.Style = textStyle;
        CardTrayText.Style = textStyle;
        TrayIconPreview.Width = TrayIconPreview.Height = _tipsExpanded ? 24 : 44;
        TrayIconPreview.Margin = new Thickness(0, 0, _tipsExpanded ? 6 : 10, 0);

        // Three narrow columns need tighter boxes than two wide ones (the numbers here must track the
        // pad/marg constants in CardHeight, which sizes the columns from them).
        var pad  = _tipsExpanded ? new Thickness(9, 8, 9, 8) : new Thickness(12, 10, 12, 10);
        var marg = _tipsExpanded ? new Thickness(3) : new Thickness(4);
        foreach (var c in new[] { CardEdit, CardAnticheat, CardSteam, CardBorderless,
                                  CardTray, CardVolume, CardSettings, CardHelp, MoreTipsCard })
        {
            c.Padding = pad;
            c.Margin  = marg;
        }

        // Open the middle column only in the expanded (three-column) state.
        CaveatMidColDef.Width = _tipsExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void MoreTipsCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_tipsExpanded) return;
        _tipsExpanded = true;
        CardAnticheat.Visibility = Visibility.Visible;
        CardVolume.Visibility = Visibility.Visible;
        CardSettings.Visibility = Visibility.Visible;
        CardHelp.Visibility = Visibility.Visible;
        ApplyCardSizing();
        BalanceCaveatColumns();
    }

    // ── Finish-step card shortcuts into Settings ────────────────────────────────────────────────────
    // "Settings offers deeper…" opens Settings; "An in-app Help system…" lands on the Help tab;
    // "D-Pad controls…" lands on the Advanced tab (where the D-Pad modes live).

    private void CardSettings_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _hooks.OpenSettingsPage(null);

    private void CardHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _hooks.OpenSettingsPage("help");

    private void CardVolume_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _hooks.OpenSettingsPage("advanced");

    // ── Finish-step tray-icon highlight ────────────────────────────────────────────────────────────
    // On reaching Roll Out, the colour flower mark blooms in the CardTray tip card's swatch, then peels off
    // and flies down to the real tray icon; the dark glyph rises in the footprint it left, and only once
    // the flight has landed. That flight is what points the user at the icon the card describes.
    //
    // Fallbacks, in order: if the shell won't place the icon (collapsed into the hidden overflow), a bobbing
    // red down-arrow over the tray clock corner; under Reduce Motion, a static red ring on the icon and no
    // flight at all (the colour mark simply stays on the card). Once per wizard session; everything is
    // fail-soft — a miss just skips the flash rather than breaking the finish step over a decoration.

    private bool _trayHighlightShown;

    private void TryHighlightTrayIcon()
    {
        if (_trayHighlightShown) return;
        _trayHighlightShown = true;

        var icon = TryGetVisibleTrayIcon();
        var iconRect = icon is null ? null : ResolveTrayIconRectDips(icon);

        // Reduce Motion: no bloom, no lift, no flight — so the colour mark never appears at all and the card
        // simply wears its dark glyph, the state every other path ends in. Only the ring points at the icon.
        if (MotionPolicy.Reduce)
        {
            FadeTrayGlyphTo(1, 0);
            if (iconRect is { } r) ShowTrayFlashWindow(NewFlashRing(), Inflate(r, 7), bob: false);
            else if (icon is not null) ShowTrayFlashArrow();
            return;
        }

        // The card's mark blooms straight away — it's the thing the eye should be on.
        if (_trayFlower is not null)
        {
            _trayFlower.Visibility = Visibility.Visible;
            _trayFlower.AutoPlayDelayMs = 0;
            _trayFlower.PlayNow();
        }

        if (iconRect is null)
        {
            // Nothing placeable to fly to. The mark stays on the card, so the glyph goes all the way up
            // behind it — there's no later moment to finish the job. The arrow points at the corner.
            FadeTrayGlyphTo(1, TrayGlyphFadeMs);
            if (icon is not null) ShowTrayFlashArrow();
            return;
        }
        // Bloom → the mark lifts off and flies → the glyph creeps up in the footprint it left. The glyph
        // stays at ZERO for the whole first half: the two silhouettes don't match exactly, so anything
        // visible under the mark fringes the colour with dark ink. Each hand-off is its own one-shot timer
        // rather than one long BeginTime chain, because the flight has to re-measure the card's swatch
        // (layout may have shifted under "More Tips…") at the moment it launches.
        var landing = SquareCentred(iconRect.Value);
        After(_trayFlower?.BloomDurationMs ?? 0, () =>
        {
            if (!FlyCardFlowerToTray(landing)) { FadeTrayGlyphTo(1, TrayGlyphFadeMs); return; }
            // Not when the mark peels off but when it LANDS — the card must not pull the eye back while the
            // flight it launched is still crossing the screen.
            After(TrayFlightHoldMs + TrayFlightMs, () => FadeTrayGlyphTo(1, TrayGlyphRiseMs));
        });
    }

    private static Rect Inflate(Rect r, double by) =>
        new(r.X - by, r.Y - by, r.Width + by * 2, r.Height + by * 2);

    /// <summary>Where the flying mark should come to rest on the tray icon. Two corrections to the shell's
    /// rect, both needed to make the landed mark read as the same object as the icon:
    /// SQUARE, because the flower scales per-axis and the rect is taller than it is wide in a standard
    /// taskbar band; and shrunk to <see cref="TrayIconFillRatio"/>, because that rect is the icon's CELL —
    /// the glyph itself only occupies the middle of it.
    /// The result is then trimmed from its BOTTOM-RIGHT only (top-left held), which is where the landed mark
    /// overhangs the icon.</summary>
    private static Rect SquareCentred(Rect r)
    {
        double s = Math.Min(r.Width, r.Height) * TrayIconFillRatio;
        return new Rect(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2,
                        s * (1 - TrayLandingTrim), s * (1 - TrayLandingTrim));
    }

    /// <summary>How much of its shell-reported cell a tray glyph actually fills (16 px drawn in a ~24 px
    /// cell at 100%, and the pair scale together with DPI).</summary>
    private const double TrayIconFillRatio = 0.7;

    /// <summary>Trimmed off the landing square's bottom-right, the mark's own overhang past the icon. The
    /// rect's top-left is left alone, so the flight (which seats on the rect's CENTRE) pulls up and left by
    /// half of this rather than shrinking about the middle.</summary>
    private const double TrayLandingTrim = 0.05;

    private System.Windows.Forms.NotifyIcon? TryGetVisibleTrayIcon()
    {
        try
        {
            var icon = _hooks.GetTrayIcon();
            return icon is not null && icon.Visible ? icon : null;   // tray icon off — nothing to point at
        }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] tray icon lookup failed: {ex.Message}"); return null; }
    }

    /// <summary>The dark glyph's plain fade-in, for the paths where no flight ever runs.</summary>
    private const double TrayGlyphFadeMs = 100;

    /// <summary>The slow climb to full once the mark is gone and landed.</summary>
    private const double TrayGlyphRiseMs = 1400;

    /// <summary>Animate the dark glyph's opacity (an instant set when <paramref name="ms"/> is 0). Animating
    /// from its CURRENT value, not a fixed start, so each leg of the sequence continues the last.</summary>
    private void FadeTrayGlyphTo(double to, double ms)
    {
        if (_trayGlyphBox is null) return;
        if (ms <= 0)
        {
            _trayGlyphBox.BeginAnimation(UIElement.OpacityProperty, null);
            _trayGlyphBox.Opacity = to;
            return;
        }
        _trayGlyphBox.BeginAnimation(UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(to, new Duration(TimeSpan.FromMilliseconds(ms)))
            { EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                               { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut } });
    }

    private static void After(double ms, Action run)
    {
        if (ms <= 0) { run(); return; }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); run(); };
        timer.Start();
    }

    /// <summary>The tray icon's on-screen rect in DIPs, or null when it can't be trusted. WinForms hides
    /// the NOTIFYICONDATA identity, so the private id + message window are reflected (".NET 8 names, then
    /// the Framework-era ones as a hedge). The rect is only accepted when it sits in the visible taskbar
    /// band — outside the work area but on the primary screen — because an icon collapsed into the hidden
    /// overflow flyout still answers with a rect, and ringing an invisible spot beats nothing but loses
    /// to the fallback arrow.</summary>
    private Rect? ResolveTrayIconRectDips(System.Windows.Forms.NotifyIcon icon)
    {
        static object? Field(object o, string name) =>
            o.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o);

        // The id's declared type has drifted across WinForms versions — accept either integer width.
        int id;
        switch (Field(icon, "_id") ?? Field(icon, "id"))
        {
            case int i:  id = i; break;
            case uint u: id = unchecked((int)u); break;
            default: return null;
        }
        if ((Field(icon, "_window") ?? Field(icon, "window")) is not System.Windows.Forms.NativeWindow win
            || win.Handle == IntPtr.Zero) return null;

        if (NativeMethods.TrayIconRectPx(win.Handle, id) is not { } rc) return null;

        // Physical px → DIPs via this window's presentation source (system-DPI-aware, so one transform).
        if (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice is not { } t) return null;
        var tl = t.Transform(new Point(rc.Left, rc.Top));
        var br = t.Transform(new Point(rc.Right, rc.Bottom));
        var r  = new Rect(tl, br);

        var centre = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        if (!NativeMethods.PrimaryScreenDips().Contains(centre)) return null;   // off-screen (overflow flyout)
        if (SystemParameters.WorkArea.Contains(centre)) return null;            // not in the taskbar band (auto-hide, hidden icon)
        return r;
    }

    private static readonly Color TrayFlashRed = Color.FromRgb(0xE0, 0x2D, 0x2D);

    private static Ellipse NewFlashRing() =>
        new() { Stroke = new SolidColorBrush(TrayFlashRed), StrokeThickness = 3, IsHitTestVisible = false };

    /// <summary>Peel the card's colour mark off the swatch and fly it to the tray icon. A frozen SNAPSHOT
    /// of the bloomed mark flies, not the control itself: a second live control would re-bloom in mid-air,
    /// and the real one has to stay put long enough to be measured. If the swatch can't be located the
    /// mark stays on the card and the flash falls back to a ring on the icon. Returns whether it flew.</summary>
    private bool FlyCardFlowerToTray(Rect end)
    {
        if (_trayFlower is null || TrayPreviewRectDips() is not { } from || SnapshotTrayFlower() is not { } shot)
        {
            ShowTrayFlashWindow(NewFlashRing(), Inflate(end, 7), bob: false);
            return false;
        }
        _trayFlower.Visibility = Visibility.Hidden;   // …leaving the dark glyph in its footprint
        FlyVisualToTray(new System.Windows.Controls.Image { Source = shot, Stretch = Stretch.Fill }, from, end);
        return true;
    }

    /// <summary>The card's colour mark rendered as it currently stands (fully bloomed), or null if it isn't
    /// laid out. Rendered at the window's device scale so the flying copy isn't soft on a HiDPI display.</summary>
    private System.Windows.Media.Imaging.BitmapSource? SnapshotTrayFlower()
    {
        if (_trayFlower is null || _trayFlower.ActualWidth <= 0 || _trayFlower.ActualHeight <= 0) return null;
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        if (scale <= 0) scale = 1.0;
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(_trayFlower.ActualWidth  * scale)),
            Math.Max(1, (int)Math.Ceiling(_trayFlower.ActualHeight * scale)),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(_trayFlower);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>The CardTray tip card's flower swatch in screen DIPs, or null when it isn't laid out /
    /// visible yet. Same device→DIP transform as <see cref="ResolveTrayIconRectDips"/>, so both ends of the
    /// flight are measured in one coordinate space.</summary>
    private Rect? TrayPreviewRectDips()
    {
        // The MARK's own box, not its 44 px host: the flying copy is a snapshot of exactly this element, so
        // measuring anything larger would launch it at the wrong size and land it at the wrong one.
        FrameworkElement el = _trayFlower ?? (FrameworkElement)TrayIconPreview;
        if (!el.IsVisible || el.ActualWidth <= 0 || el.ActualHeight <= 0) return null;
        if (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice is not { } t) return null;
        var tl = t.Transform(el.PointToScreen(new Point(0, 0)));
        return new Rect(tl.X, tl.Y, el.ActualWidth, el.ActualHeight);
    }

    private const double TrayFlightHoldMs = 500;    // the beat spent on the card before it launches
    private const double TrayFlightMs     = 850;
    private const double TrayLiftScale    = 1.18;   // how far the visual swells off the card during the hold

    /// <summary>Fly <paramref name="visual"/> from <paramref name="start"/> (the card's swatch) to
    /// <paramref name="end"/> (the tray icon) along a bowed arc, shrinking to fit, then hand over to the
    /// usual pulse. The hold before launch swells it slightly, so it reads as lifting off the card rather
    /// than sliding along it. One click-through window spanning both ends plus the arc's bulge.</summary>
    private void FlyVisualToTray(FrameworkElement visual, Rect start, Rect end)
    {
        static Point Centre(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
        var p0 = Centre(start);
        var p1 = Centre(end);

        // A quadratic control point pushed off the straight line turns the move into a thrown arc rather
        // than a slide. Always bow UPWARD (negative Y): the card sits above the tray, so lifting before the
        // drop reads as an arc, while bowing the other way reads as a sag.
        var d = new Vector(p1.X - p0.X, p1.Y - p0.Y);
        double len = d.Length;
        var perp = len < 1 ? new Vector(0, -1) : new Vector(-d.Y / len, d.X / len);
        if (perp.Y > 0) perp = -perp;
        var ctrl = new Point((p0.X + p1.X) / 2 + perp.X * len * 0.22,
                             (p0.Y + p1.Y) / 2 + perp.Y * len * 0.22);

        // Window bounds: everything the visual can touch — both end rects and the arc's bulge — with the
        // start size as slack, since the visual is centred on the path and swells during the lift.
        double slack = Math.Max(start.Width, start.Height) * TrayLiftScale;
        double minX = Math.Min(Math.Min(start.X, end.X), ctrl.X - slack) - slack;
        double minY = Math.Min(Math.Min(start.Y, end.Y), ctrl.Y - slack) - slack;
        double maxX = Math.Max(Math.Max(start.Right, end.Right), ctrl.X + slack) + slack;
        double maxY = Math.Max(Math.Max(start.Bottom, end.Bottom), ctrl.Y + slack) + slack;
        var bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        var origin = new Vector(bounds.X, bounds.Y);

        visual.Width  = start.Width;
        visual.Height = start.Height;
        visual.IsHitTestVisible = false;
        // Pinned left-to-right: the path below is computed in screen coordinates, and a mirrored canvas would reflect it
        // about the window's centre (bottom-left landing for a bottom-right tray).
        var canvas = new Canvas { IsHitTestVisible = false, FlowDirection = System.Windows.FlowDirection.LeftToRight };
        // Seat the visual's CENTRE on the canvas origin, so the path animation's offset places the centre.
        Canvas.SetLeft(visual, -start.Width / 2);
        Canvas.SetTop(visual, -start.Height / 2);
        var scale = new ScaleTransform(1, 1, start.Width / 2, start.Height / 2);
        var move  = new MatrixTransform();
        visual.RenderTransform = new TransformGroup { Children = { scale, move } };
        canvas.Children.Add(visual);

        var figure = new PathFigure { StartPoint = p0 - origin, IsClosed = false };
        figure.Segments.Add(new QuadraticBezierSegment(ctrl - origin, p1 - origin, isStroked: false));
        var path = new PathGeometry();
        path.Figures.Add(figure);
        path.Freeze();

        var begin = TimeSpan.FromMilliseconds(TrayFlightHoldMs);
        var dur   = new Duration(TimeSpan.FromMilliseconds(TrayFlightMs));
        var ease  = new System.Windows.Media.Animation.QuadraticEase
        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };

        var fly = new System.Windows.Media.Animation.MatrixAnimationUsingPath
        { PathGeometry = path, Duration = dur, BeginTime = begin };
        // Lift then shrink, in one keyframe run per axis: swell to TrayLiftScale across the hold (the peel
        // off the card), then ease down to the tray icon's size as it travels.
        var shrinkX = LiftThenShrink(end.Width  / start.Width,  ease);
        var shrinkY = LiftThenShrink(end.Height / start.Height, ease);

        var win = CreateFlashWindow(canvas, bounds);
        // Arrival hands over to the looping opacity pulse below — the flight replaces the flash's ENTRANCE,
        // not the attention-getter it becomes once it lands.
        fly.Completed += (_, _) => visual.BeginAnimation(UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1, 0.25, new Duration(TimeSpan.FromMilliseconds(450)))
            { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        win.Show();
        // Seat it on the start point immediately: without this the visual would sit at the canvas origin
        // (the window's top-left) for the whole hold, then snap onto the card as the path animation begins.
        move.Matrix = new Matrix(1, 0, 0, 1, p0.X - origin.X, p0.Y - origin.Y);
        move.BeginAnimation(MatrixTransform.MatrixProperty, fly);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkX);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkY);
        CloseAfter(win, TrayFlightHoldMs + TrayFlightMs + TrayFlashHoldMs);
    }

    /// <summary>One scale track for the whole flight: 1 → <see cref="TrayLiftScale"/> across the hold, then
    /// down to <paramref name="landed"/> over the travel.</summary>
    private static System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames LiftThenShrink(
        double landed, System.Windows.Media.Animation.IEasingFunction ease)
    {
        var k = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        k.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            TrayLiftScale, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(TrayFlightHoldMs)),
            new System.Windows.Media.Animation.QuadraticEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }));
        k.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            landed, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(TrayFlightHoldMs + TrayFlightMs)), ease));
        return k;
    }

    /// <summary>Fallback: a red downward arrow hovering above the tray clock corner (bottom-right of the
    /// primary work area — where a standard bottom taskbar keeps its clock + icons).</summary>
    private void ShowTrayFlashArrow()
    {
        var work = SystemParameters.WorkArea;
        const double w = 34, h = 44, bobRise = 10;
        var arrow = new System.Windows.Shapes.Path
        {
            Fill = new SolidColorBrush(TrayFlashRed), Stretch = Stretch.Fill, IsHitTestVisible = false,
            Data = Geometry.Parse("M 11,0 L 23,0 L 23,20 L 34,20 L 17,40 L 0,20 L 11,20 Z"),
        };
        // bobRise of headroom above the resting spot so the float animation never clips at the top.
        ShowTrayFlashWindow(arrow, new Rect(work.Right - 90, work.Bottom - h - 6 - bobRise, w, h + bobRise), bob: true);
    }

    /// <summary>Host a flash visual in a tiny topmost, non-activating, click-through window for ~4 s.
    /// The visual pulses; <paramref name="bob"/> adds the arrow's vertical float.</summary>
    private static void ShowTrayFlashWindow(FrameworkElement visual, Rect dips, bool bob)
    {
        // Reduce Motion: the flash is pointing AT something (essential), the bob and pulse are not — the
        // visual shows static for the same 4.2 s instead.
        if (MotionPolicy.Reduce) bob = false;
        var host = bob
            ? new Grid { Children = { visual } }
            : (FrameworkElement)visual;
        if (bob)
        {
            visual.VerticalAlignment = VerticalAlignment.Bottom;
            var tt = new TranslateTransform();
            visual.RenderTransform = tt;
            tt.BeginAnimation(TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, -10, new Duration(TimeSpan.FromMilliseconds(600)))
                {
                    AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut },
                });
        }
        var win = CreateFlashWindow(host, dips);
        win.Show();
        if (!MotionPolicy.Reduce)   // the opacity pulse is an oscillating loop — suppressed under Reduce Motion
            visual.BeginAnimation(UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, 0.25, new Duration(TimeSpan.FromMilliseconds(450)))
                { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        CloseAfter(win, TrayFlashHoldMs);
    }

    /// <summary>How long the flash stays up once it has ARRIVED (the flight's hold + travel is added on
    /// top for the flying ring, so both variants pulse at the tray for the same length of time).</summary>
    private const double TrayFlashHoldMs = 4200;

    /// <summary>The shared flash host: a tiny topmost, non-activating, click-through window at
    /// <paramref name="dips"/>. Not shown — the caller decides when, so animations can be seated first.</summary>
    private static Window CreateFlashWindow(FrameworkElement content, Rect dips)
    {
        var win = new Window
        {
            FlowDirection = LocWpf.Flow,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = true, Focusable = false,
            IsHitTestVisible = false,
            Left = dips.X, Top = dips.Y, Width = dips.Width, Height = dips.Height,
            Content = content,
        };
        // Click-through + non-activating at the HWND level too — seconds over the tray must never eat a click.
        win.SourceInitialized += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(win).Handle;
            int ex = NativeMethods.GetWindowLong(h, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(h, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
        };
        return win;
    }

    private static void CloseAfter(Window win, double ms)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); try { win.Close(); } catch { /* already gone */ } };
        timer.Start();
    }

    private static IEnumerable<System.Windows.Controls.TextBlock> LogicalTextBlocks(DependencyObject node)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(node))
            if (child is System.Windows.Controls.TextBlock tb) yield return tb;
            else if (child is DependencyObject d)
                foreach (var t in LogicalTextBlocks(d)) yield return t;
    }

    /// <summary>A wizard-styled two-choice dialog. True = proceed (the risky choice); false = stay.</summary>
    /// <param name="showHeading">Repeat <paramref name="title"/> as a bold first line inside the dialog.
    /// The title bar alone is easy to miss, so the one dialog whose title carries the actual question
    /// ("Skip drivers?") opts in.</param>
    private bool ConfirmChoice(string title, string body, string stayLabel, string proceedLabel,
                               bool showHeading = false)
    {
        bool proceed = false;
        var dlg = new Window
        {
            FlowDirection = LocWpf.Flow,
            Title = title, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, Background = System.Windows.Media.Brushes.White,
        };
        var text = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, MaxWidth = 380, FontSize = 13 };
        // Enter, Esc, and the title-bar X all mean "stay" — the risky choice takes a deliberate click.
        var stay = new Button { Content = stayLabel, Padding = new Thickness(16, 7, 16, 7), FontWeight = FontWeights.SemiBold, IsDefault = true, IsCancel = true };
        var skip = new Button { Content = proceedLabel, Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(8, 0, 0, 0) };
        Action stayAction = () => dlg.Close();
        Action proceedAction = () => { proceed = true; dlg.Close(); };
        stay.Click += (_, _) => stayAction();
        skip.Click += (_, _) => proceedAction();
        _modalDialog = dlg;
        _modalProceed = proceedAction;
        _modalStay = stayAction;
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(stay);
        buttons.Children.Add(skip);
        var root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        if (showHeading)
            root.Children.Add(new TextBlock
            {
                Text = title, FontSize = 14, FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap, MaxWidth = 380, Margin = new Thickness(0, 0, 0, 8),
            });
        root.Children.Add(text);
        root.Children.Add(buttons);
        dlg.Content = root;
        try { dlg.ShowDialog(); }
        finally { _modalDialog = null; _modalProceed = null; _modalStay = null; }
        return proceed;
    }

    // The open ConfirmChoice dialog and its two button actions. While set, the pad drives the dialog
    // (✕ = proceed, ○ = stay) and every other wizard pad input is swallowed. The opening ✕ is a press
    // edge already consumed by ControllerNext before the dialog exists, so its release reaches nothing.
    private Window? _modalDialog;
    private Action? _modalProceed, _modalStay;

    /// <summary>"Which controllers are supported?" link on the Welcome step — shows the help text in the
    /// shared full-width tip card. (Framed around compatible MODES — Bluetooth being the most common —
    /// not "Bluetooth fixes it", so it stays accurate for third-party pads + future USB-HID support.)</summary>
    private void PadHelp_Click(object sender, RoutedEventArgs e) => ToggleWelcomeTip(showPad: true);

    /// <summary>"About these drivers" link — shows the driver explainer in the shared tip card.</summary>
    private void DriversInfo_Click(object sender, RoutedEventArgs e) => ToggleWelcomeTip(showPad: false);

    /// <summary>Both Welcome-step tips share ONE full-width card below the columns: clicking a link shows
    /// its content there (a click on the OTHER link takes the card over); re-clicking the visible tip's
    /// link closes the card.</summary>
    private void ToggleWelcomeTip(bool showPad)
    {
        UIElement target = showPad ? PadHelp : DriversIntro;
        bool alreadyShowing = WelcomeTipCard.Visibility == Visibility.Visible && target.Visibility == Visibility.Visible;
        if (alreadyShowing) { WelcomeTipCard.Visibility = Visibility.Collapsed; return; }
        PadHelp.Visibility      = showPad ? Visibility.Visible : Visibility.Collapsed;
        DriversIntro.Visibility = showPad ? Visibility.Collapsed : Visibility.Visible;
        WelcomeTipCard.Visibility = Visibility.Visible;
    }

    /// <summary>Refresh the live status readouts + the Next/Skip gating for the current step.</summary>
    private void RefreshStatus()
    {
        if (_steps[_index].Body == StepWelcome)
        {
            bool on = _hooks.ControllerConnected();
            var kind = _hooks.Kind();
            // XInput pads are first-class: Xbox capture is on by default (cloak the XUSB devnode tree +
            // stand in with a virtual X360), so they isolate like the Sony family and get the same status,
            // glyph, and driver recommendation (see docs/INPUT-CAPTURE.md). Capture is fail-safe: a pad
            // whose tree won't cloak silently falls back to shared input.
            if (on)
                PadStatus.Text = Loc.F(O.PadConnected, _hooks.ControllerName());
            else
                PadStatus.Text = Loc.T(O.NoController);
            // Glyph above the text: a controller (green = connected) or controller-off (grey) when none.
            PadGlyph.Source = on ? PadGlyphOn : PadGlyphOff;
            PadWaiting.Visibility = on ? Visibility.Collapsed : Visibility.Visible;

            // ── Drivers (same combined first step) ── an OPTIONAL isolation tier, not a prerequisite.
            // ONE message for every pad kind: with Xbox capture on by default the drivers are what isolates
            // an XInput pad too — don't single those users out with "you can skip these".
            // No "Without these Radiata still works…" caption here: it duplicated the "About these drivers"
            // tip card, and that sentence lives in ConfirmLeaveDriversStep, which reaches the user who
            // skipped the tip. The haptics caveat lives in the "Which controllers are supported?" tip.
            DriversIntro.Text = Loc.T(O.DriversIntro);
            bool vigem = _hooks.ViGEmInstalled();
            // A foreign fork (HP OMEN Gaming Hub's) answers as "installed" but is not a bus Radiata can
            // install beside or remove; it gets a warning row with the switch instructions, and the drivers
            // column stays open so the row is seen (docs/INPUT-CAPTURE.md ▸ Foreign ViGEmBus forks).
            string? foreignBus = vigem ? _hooks.ViGEmForeign() : null;
            string? hh = _hooks.HidHideVersion();
            bool hhUpdate = hh is not null && _hooks.HidHideUpdateAvailable();
            ViGEmGlyph.Text    = !vigem ? "⬇️" : foreignBus is not null ? "⚠️" : "✅";
            ViGEmDetail.Text   = !vigem ? Loc.T(O.ViGEmMissing)
                               : foreignBus is not null ? Loc.F(O.ViGEmForeign, foreignBus)
                               : Loc.T(O.ViGEmOk);
            HidHideGlyph.Text  = hh is null ? "⬇️" : hhUpdate ? "🔄" : "✅";
            HidHideDetail.Text = hh is null ? Loc.T(O.HidHideMissing)
                               : Loc.F(hhUpdate ? O.HidHideUpdate : O.HidHideInstalled, hh);
            HidGuardianWarn.Visibility = _hooks.HidGuardianPresent() ? Visibility.Visible : Visibility.Collapsed;
            // Peer controller tools share our drivers → double/blocked input; tell the user to close them.
            var peers = _hooks.RunningControllerTools();
            PeerToolWarn.Text = peers.Count > 0
                ? Loc.F(O.PeerToolWarn, string.Join(Loc.T(O.And), peers))
                : "";
            PeerToolWarn.Visibility = peers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnInstallDrivers.IsEnabled = !_installing;
            if (!_installing)
                BtnInstallDrivers.Content = Loc.T(!DriversSatisfied() ? O.InstallIsolationDrivers
                                          : hhUpdate            ? O.UpdateDrivers
                                          :                       O.RepairDrivers);
            // Drivers installed and current → nothing on the right column is actionable; drop it and give
            // the controller box the whole step. Kept visible while an install is in flight so its log
            // line doesn't vanish mid-run, and while an update is offered (the button is the offer).
            ApplyWelcomeLayout(padOnly: DriversSatisfied() && !hhUpdate && !_installing && foreignBus is null);
            // Only offered while Steam is actually up — there is nothing to restart otherwise. The 1 s
            // poll adds/removes it live; the checked state is the user's and is never reset here.
            SteamRestartCheck.Visibility = _hooks.SteamRunning() ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (_steps[_index].Body == StepPractice)
        {
            // Pad hot-plug/drop-out mid-step flips the Next gating (all gestures required vs nothing to
            // compel — see the step's CanNext). The tip card shows either way.
            bool pad = _hooks.ControllerConnected();
            PracticeAdviceCard.Visibility = Visibility.Visible;   // tip shows in BOTH states, pad or not
            // Connecting/disconnecting flips whether Fn/touchpad are offered — rebuild the step's options.
            if (pad != _pConnected) EnterPractice();
        }
        else if (_steps[_index].Body == StepFinish)
        {
            bool pad = _hooks.ControllerConnected(), drv = DriversSatisfied();
            // "Input isolation: on" reflects EFFECT, not installation. Xbox capture is on by default and
            // isolates XInput pads like any other, so the drivers are the only thing that gates this line.
            string iso = Loc.T(drv ? O.IsolationOn : O.IsolationOff);
            FinishSummary.Text = string.Join("   ·   ",
                Loc.F(O.SummonWith, ChordLabel(CurrentChord())),
                Loc.F(O.ControllerIs, pad ? Loc.F(O.PadConnected, _hooks.ControllerName()) : Loc.T(O.NotDetected)),
                Loc.F(O.InputIsolation, iso));
        }

        bool canNext = _steps[_index].CanNext();
        BtnNext.IsEnabled  = canNext && !_installing && !_addingGame;
        BtnSkip.Visibility = _steps[_index].Skippable && !canNext ? Visibility.Visible : Visibility.Collapsed;
        BtnBack.IsEnabled  = !_installing;
    }

    // ── Drivers step ──────────────────────────────────────────────────────────

    private bool _welcomePadOnly;
    private string? _welcomeCaveatKey;      // the caveat's English UiText key currently shown (null = none) — its own change detector, since a hot-plug kind swap can flip it while padOnly stays put
    private bool _steamRestartCommitted;   // once per wizard run — Back + Next must not arm a second bounce

    /// <summary>The situational controller caveat for the connected pad, as (model family name for the
    /// header, English UiText key for the body) — null when there's nothing to say (no pad, or an Xbox
    /// pad). DualSense (Edge or standard) caveats Adaptive Triggers + touchpad under the "DualSense" name
    /// (the Edge counts as a DualSense for this purpose); the DualShock 4 shares
    /// <see cref="ControllerKind.PlayStationOther"/> but has no adaptive triggers, so it caveats the
    /// touchpad alone (<see cref="Hooks.HasAdaptiveTriggers"/> tells the two apart) under "DualShock 4".
    /// ExtraButtonPad reuses the practice step's L4/R4 transport caveat verbatim, headed with
    /// <see cref="Hooks.ControllerName"/> (called with no transport, so it's the bare model — "8BitDo U2C"
    /// today — never qualified "(Bluetooth)"). Model names are literal product names, never translated.</summary>
    private (string model, string key)? WelcomeCaveat()
    {
        if (!_hooks.ControllerConnected()) return null;
        return _hooks.Kind() switch
        {
            ControllerKind.DualSenseEdge                                     => ("DualSense", O.WelcomeCaveatAdaptiveTouch),
            ControllerKind.PlayStationOther when _hooks.HasAdaptiveTriggers() => ("DualSense", O.WelcomeCaveatAdaptiveTouch),
            ControllerKind.PlayStationOther                                  => ("DualShock 4", O.WelcomeCaveatTouchOnly),
            ControllerKind.ExtraButtonPad                                    => (_hooks.ControllerName(), O.PaddleCaveat),
            _                                                                 => null,   // Xbox — nothing to caveat
        };
    }

    /// <summary>Welcome-step layout: with the drivers column collapsed the controller box is the step, so
    /// it doubles in presence — full width, centered, glyph at 3× — instead of hugging its old third. That
    /// enlarged, centered presentation is dropped whenever a caveat card is showing (regardless of
    /// <paramref name="padOnly"/>): PadSplit — the transparent container the connected card and the caveat
    /// card sit in as SIBLINGS — splits 1:1 (padOnly) or 1:1.2 (drivers shown) instead, with the same 20px gap
    /// as the step's other boxes, so the two never fight for the same space and the caveat never nests
    /// inside the connected card's own border.</summary>
    private void ApplyWelcomeLayout(bool padOnly)
    {
        var caveat = WelcomeCaveat();
        if (padOnly == _welcomePadOnly && caveat?.key == _welcomeCaveatKey) return;
        _welcomePadOnly = padOnly;
        _welcomeCaveatKey = caveat?.key;

        HidHideBox.Visibility = ViGEmBox.Visibility = DriversLinkCol.Visibility
            = padOnly ? Visibility.Collapsed : Visibility.Visible;

        bool showCaveat = caveat is not null;
        WelcomeCaveatCard.Visibility = showCaveat ? Visibility.Visible : Visibility.Collapsed;
        if (showCaveat)
        {
            WelcomeCaveatText.Inlines.Clear();
            // padOnly: the card is half the step, so the Body tier (13 / LineHeight 19, header 16) fills it.
            // Drivers shown: the card shares a ~280px column with the pad status, so it steps down to
            // 11.5 / 16 (header 13) to keep the longest caveat from outgrowing the drivers column.
            WelcomeCaveatText.FontSize   = padOnly ? 13 : 11.5;
            WelcomeCaveatText.LineHeight = padOnly ? 19 : 16;
            // Header reads as a heading, not body copy, in HeadingInk.
            // TryFindResource: a code-side miss must not throw (docs/SETTINGS-UI.md resource trap).
            var header = new System.Windows.Documents.Run(Loc.F(O.WelcomeCaveatHeader, caveat!.Value.model))
                { FontWeight = FontWeights.Bold, FontSize = padOnly ? 16 : 13 };
            if (TryFindResource("HeadingInk") is Brush ink) header.Foreground = ink;
            WelcomeCaveatText.Inlines.Add(header);
            WelcomeCaveatText.Inlines.Add(new System.Windows.Documents.LineBreak());
            WelcomeCaveatText.Inlines.Add(new System.Windows.Documents.LineBreak());   // small gap before the body
            foreach (var inline in InlineMarkup.Parse(Loc.T(caveat.Value.key), InlineMarkup.Card))
                WelcomeCaveatText.Inlines.Add(inline);
        }
        PadSplitCol0.Width = new GridLength(1, GridUnitType.Star);
        PadSplitGap.Width  = showCaveat ? new GridLength(20) : new GridLength(0);
        PadSplitCol2.Width = showCaveat ? new GridLength(padOnly ? 1 : 1.2, GridUnitType.Star) : new GridLength(0);

        PadSplit.SetValue(Grid.ColumnSpanProperty, padOnly ? 3 : 1);
        bool bigCentered = padOnly && !showCaveat;
        PadSplit.HorizontalAlignment = bigCentered ? System.Windows.HorizontalAlignment.Center
                                             : System.Windows.HorizontalAlignment.Stretch;
        PadSplit.MinWidth  = bigCentered ? 470 : 0;
        // 196 only in the padOnly layout while a caveat shows: it holds the 13px caveat copy with room to
        // spare at the half-width column and keeps the row from shrinking with the glyph; the drivers-shown
        // layout stays content-sized.
        PadSplit.MinHeight = bigCentered ? 230 : padOnly && showCaveat ? 196 : 0;
        PadBox.Padding   = bigCentered ? new Thickness(28, 24, 28, 24) : new Thickness(14, 12, 14, 12);
        // Beside a caveat in the padOnly layout the glyph is 138 and the margins centre the controller ART
        // between the card's top edge and the status text's cap line. The "Controller" pack icon carries ~25% clear
        // space above the art and ~19% below it, so the two margins are unequal: 12 padding + 19 above
        // the box and 35 below it give ~65 on each side of the art. The drivers-shown column is too
        // narrow for it and keeps 46.
        bool sideBySide = padOnly && showCaveat;
        PadGlyph.Width   = PadGlyph.Height = bigCentered ? 138 : sideBySide ? 138 : 46;
        PadGlyph.Margin  = new Thickness(0, sideBySide ? 19 : 0, 0, 0);
        PadStatus.Margin = new Thickness(0, sideBySide ? 35 : 8, 0, 0);
        PadStatus.FontSize = bigCentered ? 19 : 14;
        PadLinkCol.SetValue(Grid.ColumnSpanProperty, padOnly ? 3 : 1);
    }

    /// <summary>Leaving the welcome step forward with the Steam box ticked hands the restart to App —
    /// the bounce itself waits for the cloak, so it may land mid-wizard (that's the point: before the
    /// practice step needs the pad clean).</summary>
    private void CommitSteamRestartChoice()
    {
        if (_steamRestartCommitted) return;
        if (SteamRestartCheck.Visibility != Visibility.Visible || SteamRestartCheck.IsChecked != true) return;
        _steamRestartCommitted = true;
        _hooks.RequestSteamRestart();
    }

    private async void BtnInstallDrivers_Click(object sender, RoutedEventArgs e)
    {
        if (_installing) return;
        _installing = true;
        RefreshStatus();
        InstallStatus.Text = Loc.T(O.Installing);
        try
        {
            // Let the status text paint, then run the engine — same (hardware-verified) sequence and same
            // UI thread as the tray path, but its waits on the elevated installers are AWAITED rather than
            // blocking (DriverSetup.RunElevatedAsync), so this window keeps pumping instead of going
            // "Not Responding" for the length of the install.
            await Dispatcher.Yield(DispatcherPriority.Background);
            var log = await _hooks.RunDriverEngine();
            InstallStatus.Text = string.Join("\n", log);
        }
        catch (Exception ex)   // async void — contain (a failure here must not take the app down)
        {
            Trace.WriteLine($"[OOBE] driver setup failed: {ex.Message}");
            InstallStatus.Text = Loc.T(O.DriverSetupFailed);
        }
        finally
        {
            _installing = false;
            RefreshStatus();
        }
    }

    // ── Storefronts step ──────────────────────────────────────────────────────

    // (Launcher-presence detection lives in GameLibrary.InstalledLaunchers — shared with Settings.)

    /// <summary>The merged Storefronts + starting-slices step's entry: populate the storefront cards AND
    /// build the starter wheels (both read the one shared scan). Called as the step's OnEnter.</summary>
    private void EnterInitialSlices() { EnterStores(); EnterWheels(); }

    /// <summary>On a FRESH install (wheels still the shipped defaults), write the capability-gated starter
    /// layout to the real config in the background — so the wheel the user practises on (and any early peek)
    /// shows the appropriate slices instead of the shipped placeholders, well before the Customize step. A
    /// customized install is left untouched (never stomp real wheels). Uses the same builder the Customize
    /// step does, so the two agree; the Customize step keys its pre-tick on <see cref="_startedCustomized"/>
    /// (captured before this write), so it still treats a fresh install as fresh.</summary>
    private async void PreloadStarterWheelsAsync()
    {
        if (_startedCustomized) return;
        var initialWheels = _hooks.GetWheels();
        string initialA = JsonSerializer.Serialize(initialWheels.Item1);
        string initialB = JsonSerializer.Serialize(initialWheels.Item2);
        try
        {
            // Clean installs start on Flat Dark. Gated on OobeVersion == 0 (never onboarded before), not on
            // the material's value: Flat Dark is now SystemConfig's own default, so a value-equality check
            // could no longer tell a fresh install from an existing user who already chose Flat Dark and a
            // different SoundTheme — that user's pick would otherwise be stomped by any later OOBE re-run
            // their still-default wheels qualify for. Mirrors App.SeedNarrationFromNarrator's gate for the
            // same reason. Writes the same three fields as SelectMaterialTile (SliceMaterial,
            // GameGridMaterial, SoundTheme) — keep the two in step.
            var sysNow = _hooks.GetSystem();
            if (sysNow.OobeVersion == 0)
                _hooks.WriteSystem(sysNow with
                {
                    SliceMaterial    = ControllerWheel.Materials.FlatDark,
                    GameGridMaterial = ControllerWheel.Materials.FlatDark,
                    SoundTheme       = "material",
                });

            var games = await EnsureScan();
            var off   = _hooks.GetSystem().DisabledStorefronts;
            var enabled = games.Where(g => !off.Contains(g.Storefront, StringComparer.OrdinalIgnoreCase)).ToList();
            int playniteCount = _playnitePresent && !off.Contains("Playnite", StringComparer.OrdinalIgnoreCase)
                ? await EnsurePlayniteCount() : 0;
            var currentWheels = _hooks.GetWheels();
            if (JsonSerializer.Serialize(currentWheels.Item1) != initialA ||
                JsonSerializer.Serialize(currentWheels.Item2) != initialB)
            {
                Trace.WriteLine("[OOBE] wheels changed during starter scan — keeping current choices");
                return;
            }
            var (a, b) = BuildStarterWheels(BestStarterLauncherKey(enabled, playniteCount));
            _hooks.WriteWheels(a, b);
        }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] preload starter wheels failed: {ex.Message}"); }
    }

    private async void EnterStores()
    {
        try
        {
            var games = await EnsureScan();
            if (_steps[_index].Body != StepWheels) return;   // user moved on while scanning (now the merged step)

            StoresScanning.Visibility = Visibility.Collapsed;
            StoresFound.Items.Clear();
            _storeChecks.Clear();   // rebuilt with the cards below

            // Playnite (the aggregator) gets its own card when present — first, since it usually holds the
            // fullest library. Its game count comes from the Playnite DB, not the live storefront scan.
            if (_playnitePresent)
            {
                int pn = await EnsurePlayniteCount();
                if (_steps[_index].Body != StepWheels) return;
                AddStoreRow("Playnite", pn > 0 ? Loc.P(O.GameOne, O.GameOther, pn) : Loc.T(O.Installed));
            }

            var byStore  = games.GroupBy(g => g.Storefront, StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(g => g.Count()).ToList();
            foreach (var grp in byStore)
                AddStoreRow(grp.Key, Loc.P(O.GameOne, O.GameOther, grp.Count()));

            var found = byStore.Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (_playnitePresent) found.Add("Playnite");   // don't also list Playnite as an installed-empty store

            // Launchers that are INSTALLED but currently have zero games still list as found (we must not
            // steer the user to install an app they already have).
            foreach (var store in GameLibrary.InstalledLaunchers().Where(s => !found.Contains(s)).OrderBy(s => s))
                AddStoreRow(store, Loc.T(O.InstalledNoGames));

            SelectableText.EnableWithin(StoresFound);   // cards arrive after the step's own walk
            ReconcileStoreChecks();   // async build — checks derive from the wheel list, incl. the seeded launcher
        }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] storefront scan failed: {ex.Message}"); }
    }

    /// <summary>Add a found-storefront CARD (6 per row via the UniformGrid panel): the brand glyph on the
    /// left, the storefront name (bold) over its game count on the right. Clicking a card toggles that
    /// store's LAUNCHER slice on the Right wheel (2px black outline = it's on, per ReconcileStoreChecks).
    /// Storefront opt-outs are authored in the GAME GRID (filter to a storefront with L1/R1 and hold ☐ on
    /// its card to hide it); SystemConfig.DisabledStorefronts is the persisted list this step filters on.</summary>
    private void AddStoreRow(string store, string countLine)
    {
        // Glyph/launcher by storefront name, falling back to a launcher-key match so Playnite (whose
        // Storefront is null — it aggregates) still resolves. Null = no launcher → card isn't clickable.
        LauncherInfo? li = LauncherCatalog.FindByStorefront(store) ?? LauncherCatalog.Find(store);

        // A storefront's GAMES being present doesn't mean its LAUNCHER app is (e.g. GOG games without
        // Galaxy) — the "installed, no games yet" rows (from GameLibrary.InstalledLaunchers) are present
        // by definition, so this only ever demotes a games-found card. When the launcher is missing, the
        // card stays visible (its games still matter) but can't toggle a dead launcher slice.
        bool launcherPresent = li is null || LauncherCatalog.IsPresent(li.Value.Key);
        if (li is not null && !launcherPresent)
            countLine += " " + Loc.T(O.NotInstalledSuffix);

        // Name (bold) over game count, RIGHT-ALIGNED against the card's right edge and free to run back
        // OVER the glyph when it's too long for the space beside it — the glyph is decoration, so a long
        // storefront name overlapping it beats clipping the name. Hence NoWrap: wrapping would break the
        // name onto a second line instead of letting it reach left across the icon.
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var titleBlock = new TextBlock
        {
            Text = store, FontSize = 12, FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.NoWrap, TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        // "Battle.net" clips at its right edge in the 6-per-row cell. Display-only fix, keyed on the
        // storefront name so every other card is untouched: drop the dot and tighten slightly with a
        // horizontal ScaleTransform — TextBlock has no letter-spacing property, but a LayoutTransform
        // scale tightens the glyph run AND shrinks the measured width. The underlying `store` string
        // (used as the lookup/Tag elsewhere in this method) is left exactly as scanned.
        if (string.Equals(store, "Battle.net", StringComparison.OrdinalIgnoreCase))
        {
            titleBlock.Text = "BattleNet";
            titleBlock.LayoutTransform = new ScaleTransform(0.94, 1.0);
        }
        textStack.Children.Add(titleBlock);
        textStack.Children.Add(new TextBlock
        {
            Text = countLine, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
            Margin = new Thickness(0, 0, 0, 0), TextWrapping = TextWrapping.NoWrap, LineHeight = 13,
            TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Right,
        });

        // A single-cell GRID, not a horizontal StackPanel: the glyph pins to the cell's left and the text
        // to its right IN THE SAME CELL, so over-long text simply extends left across the glyph rather
        // than being squeezed by it.
        var top = new Grid { Margin = new Thickness(8, 4.5, 8, 4.5), ClipToBounds = true };
        if (li?.BrightGlyph() is { } glyph)
            top.Children.Add(new System.Windows.Controls.Image
            {
                Source = glyph, Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0),
            });
        top.Children.Add(textStack);

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0A, 0, 0, 0)),
            // 2px black outline = this storefront's launcher is on the Right wheel. Toggled by
            // ReconcileStoreChecks below.
            BorderBrush = Brushes.Black, BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8), Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Stretch, Child = top,
        };
        // Click toggles this storefront's launcher slice on the Right wheel (only when a launcher maps AND
        // it's actually present — a card for an absent launcher is informational-only: still shown because
        // its games matter, but not wired to add a slice for an app that isn't there).
        // The card is registered for ReconcileStoreChecks — its outline must ALWAYS derive from the actual
        // wheel list, never from click bookkeeping (direct-set desyncs).
        if (li is { } launcher && launcherPresent)
        {
            card.Cursor = System.Windows.Input.Cursors.Hand;
            card.MouseLeftButtonUp += (_, _) => ToggleStoreLauncher(launcher);
            _storeChecks[launcher.Key] = card;
        }
        StoresFound.Items.Add(card);
    }

    // Storefront cards by launcher key, for ReconcileStoreChecks (rebuilt with the cards).
    private readonly Dictionary<string, Border> _storeChecks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Storefront-card click: toggle this store's launcher on the Right wheel. Unlike unticking a
    /// row directly IN the wheel list (which just unticks — the row stays for re-ticking), the card is an
    /// explicit add/remove gesture: clicking a TICKED card's storefront REMOVES its row outright. An
    /// existing but UNTICKED row is just re-ticked.</summary>
    private void ToggleStoreLauncher(LauncherInfo li)
    {
        var existing = WheelBList.Items.OfType<CheckBox>().FirstOrDefault(
            c => RowSlices(c).Any(s => s.Action?.Type == "launcher"
              && string.Equals(s.Action.Command, li.Key, StringComparison.OrdinalIgnoreCase)));
        if (existing is { IsChecked: true })
            WheelBList.Items.Remove(existing);
        else if (existing is not null)
        {
            existing.IsChecked = true;   // handlers run UpdateWheelsCapNote + ApplyWheelsLive
            return;
        }
        else
        {
            var slice = new WheelSlice { Label = li.ShortName, Action = new ActionConfig { Type = "launcher", Command = li.Key } };
            WheelBList.Items.Add(MakeSliceCheck(slice, isChecked: true));
        }
        UpdateWheelsCapNote();
        ApplyWheelsLive();
    }

    private static readonly Thickness StoreCardOutlineOn  = new(2);
    private static readonly Thickness StoreCardOutlineOff = new(0);

    /// <summary>Sync every storefront card's outline to the ACTUAL Right wheel: a 2px black border iff a
    /// TICKED launcher row for that store exists. Runs from UpdateWheelsCapNote (i.e. after every list
    /// mutation, incl. direct row unticks) and after the async card build — so the seeded launcher's card
    /// is outlined from first render and Back/Next re-entry can't desync (cards rebuild; the wheel list
    /// persists).</summary>
    private void ReconcileStoreChecks()
    {
        foreach (var (key, card) in _storeChecks)
            card.BorderThickness = WheelBList.Items.OfType<CheckBox>().Any(
                c => c.IsChecked == true && RowSlices(c).Any(s => s.Action?.Type == "launcher"
                  && string.Equals(s.Action.Command, key, StringComparison.OrdinalIgnoreCase)))
                ? StoreCardOutlineOn : StoreCardOutlineOff;
    }

    // ── Cover-art step ────────────────────────────────────────────────────────

    // The Assets step's three illustrations: tiles carrying rich logos, the set Playnite alone supplies, and
    // the same tiles struck out for "No game art". Held frozen so the swap on every selection change
    // doesn't re-decode.
    private static readonly System.Windows.Media.Imaging.BitmapImage ArtExampleLogos    = LoadArtExample("tile-logos.png");
    private static readonly System.Windows.Media.Imaging.BitmapImage ArtExamplePlaynite = LoadArtExample("tile-logos-playnite.png");
    private static readonly System.Windows.Media.Imaging.BitmapImage ArtExampleNull     = LoadArtExample("tile-logo-null.png");

    private static System.Windows.Media.Imaging.BitmapImage LoadArtExample(string file)
    {
        var bmp = new System.Windows.Media.Imaging.BitmapImage(
            new Uri($"pack://application:,,,/{typeof(OnboardingWindow).Assembly.GetName().Name};component/Assets/{file}"));
        bmp.Freeze();
        return bmp;
    }

    private void EnterArt()
    {
        _artLoading = true;
        try
        {
            if (_playnitePresent)
            {
                SetArtIntro(Loc.T(O.ArtIntroPlaynite), null, null);
                ArtOpt1.Content = Loc.T(O.ArtBoth);
                ArtOpt2.Content = Loc.T(O.ArtPlayniteOnly);
                // With Playnite installed there's always art available, so "No game art" is not offered.
                // Uncheck it first: a hidden checked radio would leave the group reading the missing choice.
                if (ArtSelectedIndex == 2) ArtSelectedIndex = 0;
                ArtOpt3.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Intro body is selection-dependent here — UpdateArtPanels writes it (and rewrites it on
                // every selection change), so the Playnite sentence only stands while Playnite is picked.
                ArtOpt1.Content = Loc.T(O.ArtSgdb);
                ArtOpt2.Content = Loc.T(O.ArtSgdbPlaynite);
                ArtOpt3.Content = Loc.T(O.ArtNone);
                ArtOpt3.Visibility = Visibility.Visible;
            }
            if (ArtSelectedIndex < 0) ArtSelectedIndex = 0;

            var key = SecretField.ToPlaintext(_hooks.GetSystem().SteamGridDbKey);
            if (!string.IsNullOrWhiteSpace(key))
            {
                SgdbKeyBox.Text    = key;
                SgdbKeyStatus.Text = Loc.T(O.KeySaved);
            }
            else
            {
                // No saved key (ApplyArtChoice drops it when an opt-out option commits) — a leftover
                // "✅ Key works — saved." would contradict the Next gating, so reset the status.
                SgdbKeyStatus.Text = SgdbKeyBox.Text.Trim().Length > 0
                    ? Loc.T(O.KeyNotSaved) : "";
            }
        }
        finally { _artLoading = false; }
        UpdateArtPanels();
    }

    /// <summary>Set the Assets step's intro as up to three runs — plain, BOLD, plain — since one branch of
    /// the copy bolds a word mid-sentence. Written through Inlines rather than Text: assigning Text on a
    /// TextBlock that already holds Inlines replaces them, and the two branches alternate on every visit.</summary>
    private void SetArtIntro(string lead, string? bold, string? tail)
    {
        ArtIntro.Inlines.Clear();
        ArtIntro.Inlines.Add(new System.Windows.Documents.Run(lead));
        if (bold is not null) ArtIntro.Inlines.Add(new System.Windows.Documents.Run(bold) { FontWeight = FontWeights.Bold });
        if (tail is not null) ArtIntro.Inlines.Add(new System.Windows.Documents.Run(tail));
    }

    /// <summary>No-Playnite branch intro. The Playnite sentence is an ANSWER to the drop-down, not standing
    /// advice: it only appears while the Playnite option is selected, so the other two choices aren't
    /// pitched a second launcher they just declined.</summary>
    private void SetSgdbIntro(bool playnitePicked)
    {
        ArtIntro.Inlines.Clear();
        foreach (var inline in InlineMarkup.Parse(Loc.T(O.SgdbIntro), InlineMarkup.Card)) ArtIntro.Inlines.Add(inline);
        if (!playnitePicked) return;
        ArtIntro.Inlines.Add(new System.Windows.Documents.Run(" "));
        foreach (var inline in InlineMarkup.Parse(Loc.T(O.SgdbIntroPlaynite), InlineMarkup.Card)) ArtIntro.Inlines.Add(inline);
    }

    /// <summary>The checked cover-art radio as 0/1/2 (-1 = none). Setting an index checks that radio, which
    /// raises <see cref="ArtOption_Changed"/> exactly as a selection change would.</summary>
    private int ArtSelectedIndex
    {
        get => ArtOpt1.IsChecked == true ? 0 : ArtOpt2.IsChecked == true ? 1 : ArtOpt3.IsChecked == true ? 2 : -1;
        set
        {
            var target = value switch { 0 => ArtOpt1, 1 => ArtOpt2, 2 => ArtOpt3, _ => null };
            if (target is not null) target.IsChecked = true;
            else { ArtOpt1.IsChecked = ArtOpt2.IsChecked = ArtOpt3.IsChecked = false; }
        }
    }

    private void ArtOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_artLoading) return;
        UpdateArtPanels();
        RefreshStatus();   // Next gating: SGDB-dependent options need a saved key
    }

    /// <summary>Next is gated while an option that DEPENDS on SteamGridDB is selected but no key has
    /// been saved — the user must Test &amp; Save a key or pick an option that doesn't need one.</summary>
    private bool ArtStepValid()
    {
        bool needsKey = _playnitePresent
            ? ArtSelectedIndex == 0                                          // "Use both" needs the key
            : ArtSelectedIndex == 0 || ArtSelectedIndex == 1;   // both non-Playnite setups include SGDB
        return !needsKey || !string.IsNullOrWhiteSpace(_hooks.GetSystem().SteamGridDbKey);
    }

    private void UpdateArtPanels()
    {
        bool sgdb = _playnitePresent ? ArtSelectedIndex == 0
                                     : ArtSelectedIndex == 0 || ArtSelectedIndex == 1;
        // Item 1 on the no-Playnite branch is the Playnite option; the Playnite-installed branch's intro is
        // written once by EnterArt and doesn't move with the selection.
        if (!_playnitePresent) SetSgdbIntro(ArtSelectedIndex == 1);
        SgdbSetup.Visibility   = sgdb ? Visibility.Visible : Visibility.Collapsed;
        PlayniteGet.Visibility = !_playnitePresent && ArtSelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        // The illustration tracks what the selection actually buys: "No game art" swaps in the struck-out
        // set so the picture stops promising artwork the user just declined, and "Playnite only" (item 2 on
        // the Playnite-installed branch) shows what Playnite alone supplies. (Item 3 only exists on the
        // no-Playnite branch.)
        ArtExample.Source =
            ArtSelectedIndex == 2 ? ArtExampleNull
            : _playnitePresent && ArtSelectedIndex == 1 ? ArtExamplePlaynite
            : ArtExampleLogos;
        // Warn before ApplyArtChoice discards a key the user already saved on this step — otherwise the
        // deletion is silent and the key just reads empty in Settings ▸ Integrations later.
        SgdbDropNotice.Visibility =
            !sgdb && !string.IsNullOrWhiteSpace(_hooks.GetSystem().SteamGridDbKey)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Commit the cover-art choice when leaving the step forward. The SGDB key itself is saved by
    /// Test &amp; Save; this sets the source-order/opt-out flags.</summary>
    private bool ApplyArtChoice()
    {
        var sys = _hooks.GetSystem();
        bool preferPlaynite = _playnitePresent
            ? ArtSelectedIndex is 0 or 1        // Playnite curated covers win per-game
            : ArtSelectedIndex == 1;            // will win once Playnite is set up
        string? key = ArtSelectedIndex == 2 || (_playnitePresent && ArtSelectedIndex == 1)
            ? null                                                     // opted out of SGDB → drop the key
            : sys.SteamGridDbKey;
        if (sys.PreferPlayniteCovers != preferPlaynite || sys.SteamGridDbKey != key)
            return _hooks.WriteSystem(sys with { PreferPlayniteCovers = preferPlaynite, SteamGridDbKey = key });
        return true;
    }

    /// <summary>Start warming the Game Grid's cover/logo cycle caches once the cover-art choice is made
    /// (the SGDB key, if any, is saved by now). HEAVILY rate-limited — onboarding continues over it, and
    /// a first-run sweep of a big library shouldn't hammer SteamGridDB.</summary>
    private async Task KickArtPrefetchAsync()
    {
        try
        {
            var games = await EnsureScan();
            var off   = _hooks.GetSystem().DisabledStorefronts;
            ArtPrefetcher.Kick(
                [.. games.Where(g => !off.Contains(g.Storefront, StringComparer.OrdinalIgnoreCase))],
                TimeSpan.FromSeconds(2.5));
        }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] art prefetch kick failed: {ex.Message}"); }
    }

    private void BtnSgdbSite_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://www.steamgriddb.com/profile/preferences/api");

    private void BtnGetPlaynite_Click(object sender, RoutedEventArgs e) => OpenUrl("https://playnite.link");

    private async void BtnSgdbTest_Click(object sender, RoutedEventArgs e)
    {
        var key = SgdbKeyBox.Text.Trim();
        if (key.Length == 0) { SgdbKeyStatus.Text = Loc.T(O.PasteKey); return; }
        BtnSgdbTest.IsEnabled = false;
        SgdbKeyStatus.Text = Loc.T(O.CheckingKey);
        try
        {
            bool ok = await GameArt.TestKeyAsync(key);
            if (ok)
            {
                if (!_hooks.WriteSystem(_hooks.GetSystem() with { SteamGridDbKey = SecretField.ToStored(key) }))
                {
                    SgdbKeyStatus.Text = ""; // The host reports the save error; keep the entered key for retry.
                    return;
                }
                SgdbKeyStatus.Text = Loc.T(O.KeyWorks);
                RefreshStatus();   // a saved key unblocks Next for the SGDB-dependent options
            }
            else SgdbKeyStatus.Text = Loc.T(O.KeyRejected);
        }
        catch (Exception ex)   // async void — contain
        {
            Trace.WriteLine($"[OOBE] SGDB key test failed: {ex.Message}");
            SgdbKeyStatus.Text = Loc.T(O.KeyCheckFailed);
        }
        finally { BtnSgdbTest.IsEnabled = true; }
    }

    // ── Starter-wheels step ───────────────────────────────────────────────────

    private List<InstalledGame> _dropdownGames = new();
    private string _wheelsSummaryBase = "";   // summary sans the over-cap note (see UpdateWheelsCapNote)
    private bool _addingGame;                 // a dropdown add's slice build is in flight — gates Next like _installing
    private bool _gameAdded;                  // at least one game added via the dropdown → relabel + drop the green outline

    /// <summary>Label/outline for the add-game box: green-outlined "Add your first game:" until one is
    /// added, then a plain "Add additional games:" (persists across Back/Next via <see cref="_gameAdded"/>).</summary>
    private void ApplyAddGameState()
    {
        AddGameLabel.Text        = Loc.T(_gameAdded ? O.AddMoreGames : O.AddFirstGame);
        AddGameBox.BorderThickness = new Thickness(_gameAdded ? 0 : 2);
        AddGameBox.Padding         = _gameAdded ? new Thickness(0, 6, 0, 0) : new Thickness(10, 8, 10, 8);
        ApplyAddGamePulse();
    }

    /// <summary>The green outline breathes slowly to transparent and back while it's the step's call to
    /// action, and goes still once a game has been added. The brush is animated
    /// per-instance (a fresh unfrozen SolidColorBrush, since the XAML literal is frozen), and the animation
    /// is cleared rather than left running when the outline is dropped.</summary>
    private void ApplyAddGamePulse()
    {
        if (AddGameBox.BorderBrush is not SolidColorBrush { IsFrozen: false } brush)
        {
            brush = new SolidColorBrush(Color.FromRgb(0x89, 0xB2, 0x9A));
            AddGameBox.BorderBrush = brush;
        }
        // Done, or Reduce Motion: a steady outline carries the call-to-action without the breathing loop.
        if (_gameAdded || MotionPolicy.Reduce)
        {
            brush.BeginAnimation(SolidColorBrush.OpacityProperty, null);
            brush.Opacity = 1;
            return;
        }
        brush.BeginAnimation(SolidColorBrush.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1, To = 0, Duration = new Duration(TimeSpan.FromSeconds(1.6)),
            AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut },
        });
    }

    private async void EnterWheels()
    {
        try
        {
            var games = await EnsureScan();
            if (_steps[_index].Body != StepWheels) return;

            var off = _hooks.GetSystem().DisabledStorefronts;
            var enabled = games.Where(g => !off.Contains(g.Storefront, StringComparer.OrdinalIgnoreCase)).ToList();
            // The storefront cards below show per-store counts; only the no-games note is needed here.
            _wheelsSummaryBase = enabled.Count > 0
                ? ""
                : Loc.T(O.NoGamesFound);

            // Playnite (if present and not opted out) is a candidate for the Right wheel's starter launcher.
            int playniteCount = _playnitePresent && !off.Contains("Playnite", StringComparer.OrdinalIgnoreCase)
                ? await EnsurePlayniteCount() : 0;
            if (_steps[_index].Body != StepWheels) return;

            // The lists are built ONCE per wizard session (first entry) — Back/Next re-entry keeps the live
            // items, so the user's ticks and dropdown-added games survive. Nothing invalidates _starter
            // in-session.
            if (_starter is null)
            {
                // Seed the Right wheel with a launcher for the user's single best-populated storefront.
                _starter = BuildStarterWheels(BestStarterLauncherKey(enabled, playniteCount));

                if (!_startedCustomized)
                {
                    // Fresh install: the starter layout, all ticked — doing nothing applies it wholesale.
                    // (Keyed on the OOBE-start snapshot: PreloadStarterWheelsAsync writes the starter to the
                    // live config on a fresh install, which would otherwise flip WheelsAreCustomized() true.)
                    PopulateWheelList(WheelAList, _starter.Value.a, checkedDefault: true);
                    PopulateWheelList(WheelBList, _starter.Value.b, checkedDefault: true);
                }
                else
                {
                    // CUSTOMIZED install (OOBE re-run): seed from the user's CURRENT wheels (ticked) plus
                    // any starter slice not already present (unticked add-on). The commit rewrites a wheel
                    // with its full ticked set — and runs LIVE on every tick — so the user's slices must BE
                    // in that set, or one tick silently replaces the whole customized wheel.
                    var (curA, curB) = _hooks.GetWheels();
                    PopulateWheelListMerged(WheelAList, curA, _starter.Value.a);
                    PopulateWheelListMerged(WheelBList, curB, _starter.Value.b);
                }
            }

            // If the Right wheel already carries an installed-game slice (e.g. a customized-install
            // re-run merged in the user's existing games), skip the "Add your first game" green prompt
            // in favour of the plain "Add additional games" mode — there's already at least one.
            if (!_gameAdded && WheelBList.Items.OfType<CheckBox>().Any(c =>
                    c.IsChecked == true && RowSlices(c).Any(s => s.Action?.Type == "installed-game")))
            {
                _gameAdded = true;
            }

            // Game dropdown: pick a game to append to the Right Wheel (refreshed every entry — the
            // storefront opt-outs may have changed what's offered).
            _dropdownGames = enabled.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
            GameDropdown.ItemsSource   = _dropdownGames.Select(g => g.Name).ToList();
            GameDropdown.SelectedIndex = -1;
            GameDropdown.IsEnabled     = _dropdownGames.Count > 0 && !_addingGame;
            WheelsPreviewReminder.Text = Loc.F(O.PracticeReminder, ChordLabel(CurrentChord()));
            ApplyAddGameState();
            UpdateWheelsCapNote();
        }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] starter-wheel build failed: {ex.Message}"); }
    }

    /// <summary>Customize step "Reset": discard whatever this step has become — untickings, dropdown-added
    /// games, and (on an OOBE re-run) the user's own merged-in slices — and put the suggested starter layout
    /// back, fully ticked. Applies LIVE like every other change on this step, so an immediate wheel preview
    /// shows the restored layout. No-op while the library scan that produces the starter layout is still
    /// running (nothing to restore yet).</summary>
    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        // _addingGame: an in-flight dropdown add's continuation appends its slice + re-applies AFTER this
        // reset repopulates the lists, silently undoing it — same guard Back/Next carry for the same reason.
        if (_steps[_index].Body != StepWheels || _addingGame || NavDebounced()) return;
        if (_starter is not { } starter) return;
        PopulateWheelList(WheelAList, starter.a, checkedDefault: true);
        PopulateWheelList(WheelBList, starter.b, checkedDefault: true);
        // A reset wheel B holds only starter slices, so the "add your first game" call to action is live
        // again unless the starter itself carries an installed game.
        _gameAdded = starter.b.Any(s => s.Action?.Type == "installed-game");
        GameDropdown.SelectedIndex = -1;
        ApplyAddGameState();
        // MakeSliceCheck attaches its handlers AFTER IsChecked, so seeding fires no Checked events —
        // the cap note and the live apply have to be driven by hand here.
        UpdateWheelsCapNote();
        ApplyWheelsLive();
    }

    private CheckBox MakeSliceCheck(WheelSlice slice, bool isChecked) =>
        MakeGroupCheck(Loc.DefaultLabel(slice.Label ?? ""), [slice], isChecked);

    /// <summary>One checklist row. A row normally carries a single slice; the arcade row carries the whole
    /// arcade set, so one tick adds or removes every game. Tag is ALWAYS a slice array — the commit, the
    /// cap count and the launcher/game probes read it through <see cref="RowSlices"/> and never need to
    /// know which kind of row they're looking at.</summary>
    private CheckBox MakeGroupCheck(string label, WheelSlice[] slices, bool isChecked)
    {
        var cb = new CheckBox
        {
            Content = label, Tag = slices, IsChecked = isChecked, FontSize = 14, Margin = new Thickness(0, 2, 0, 2),
        };
        // Every tick change moves the count against the MaxSlices cap AND applies live (the step invites
        // previewing the wheel mid-step — the ticks must be what the preview shows).
        cb.Checked   += (_, _) => { UpdateWheelsCapNote(); ApplyWheelsLive(); };
        cb.Unchecked += (_, _) => { UpdateWheelsCapNote(); ApplyWheelsLive(); };
        return cb;
    }

    /// <summary>Apply the checklist to the live wheels IMMEDIATELY (same commit as Next) so a mid-step
    /// wheel invoke previews the current choices — the step's copy promises exactly that. Only while the
    /// Customize step is showing (list rebuilds on other steps must not write). This step is the exception
    /// to "Back never commits": changes are visibly live, so keeping them on Back matches what the user
    /// just saw (fresh installs that touch nothing are still never written).</summary>
    private void ApplyWheelsLive()
    {
        if (_steps[_index].Body != StepWheels) return;
        ApplyWheelsChoice();
    }


    private static bool IsArcade(WheelSlice s) =>
        string.Equals(s.Action?.Type, "arcade", StringComparison.OrdinalIgnoreCase);

    /// <summary>The slices a checklist row commits — one for an ordinary row, the whole set for the arcade
    /// row.</summary>
    private static WheelSlice[] RowSlices(CheckBox c) => (WheelSlice[])c.Tag!;

    /// <summary>The checklist row already carrying <paramref name="slice"/>'s action, ticked or not, or null.
    /// Identity is <see cref="SliceIdentity"/> — see <see cref="PopulateWheelListMerged"/>.</summary>
    private static CheckBox? FindRow(ItemsControl list, WheelSlice slice)
    {
        string key = SliceIdentity.Key(slice);
        return list.Items.OfType<CheckBox>().FirstOrDefault(
            c => RowSlices(c).Any(s => string.Equals(SliceIdentity.Key(s), key, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Build one pass of checklist rows. Arcade slices don't get a row each: they collapse into a
    /// single "Arcade Launcher" row carrying <paramref name="arcadeRow"/>, drawn where the first
    /// arcade slice falls so wheel order is preserved. Only the pass that <paramref name="ownsArcadeRow"/>
    /// draws it; the other pass drops its arcade slices, since one wheel gets one arcade row however its
    /// games are split between the user's layout and the starter's.</summary>
    private List<CheckBox> BuildRows(IEnumerable<WheelSlice> slices, bool isChecked,
                                     WheelSlice[] arcadeRow, bool ownsArcadeRow)
    {
        var rows = new List<CheckBox>();
        bool drawn = false;
        foreach (var s in slices)
        {
            if (IsArcade(s))
            {
                if (drawn || !ownsArcadeRow || arcadeRow.Length == 0) continue;
                drawn = true;
                rows.Add(MakeGroupCheck(Loc.T(O.ArcadeRow), arcadeRow, isChecked));
                continue;
            }
            rows.Add(MakeSliceCheck(s, isChecked));
        }
        return rows;
    }

    /// <summary>Customized-install seeding: the user's current slices (ticked) followed by starter slices
    /// not already on the wheel (unticked add-ons). Identity is <see cref="SliceIdentity"/>, not raw field
    /// equality — a renamed slice still matches its starter twin, and so does one carrying stale payload
    /// from an earlier action type, instead of the starter seeding a second copy of it.</summary>
    private void PopulateWheelListMerged(ItemsControl list, WheelSlice[] current, WheelSlice[] starter)
    {
        list.Items.Clear();
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in current) have.Add(SliceIdentity.Key(s));
        var extra = starter.Where(s => have.Add(SliceIdentity.Key(s))).ToArray();
        // One arcade row per wheel, pooling the user's own arcade slices with the starter games they're
        // missing — their slice wins for a game they already have, so a re-run can't stomp its label or
        // icon. Ticked (and drawn in place) when the wheel already carries one; otherwise it joins the
        // unticked add-ons.
        bool userHasArcade = current.Any(IsArcade);
        WheelSlice[] arcadeRow = [.. current.Where(IsArcade), .. extra.Where(IsArcade)];
        foreach (var cb in BuildRows(current, isChecked: true,  arcadeRow, ownsArcadeRow: userHasArcade))  list.Items.Add(cb);
        foreach (var cb in BuildRows(extra,   isChecked: false, arcadeRow, ownsArcadeRow: !userHasArcade)) list.Items.Add(cb);
    }

    private void PopulateWheelList(ItemsControl list, WheelSlice[] slices, bool checkedDefault)
    {
        list.Items.Clear();
        foreach (var cb in BuildRows(slices, checkedDefault, [.. slices.Where(IsArcade)], ownsArcadeRow: true))
            list.Items.Add(cb);
    }

    /// <summary>Keep the summary honest about the slice cap: ApplyWheelsChoice commits only the first
    /// MaxSlices ticks per wheel (dropdown-added games can push Wheel B past it), so say so instead of
    /// silently truncating on Next.</summary>
    private void UpdateWheelsCapNote()
    {
        int cap = SystemConfig.MaxSlicesPerWheel;
        // Slices, not rows — the arcade row stands for several, and the cap counts slices.
        int aCount = WheelAList.Items.OfType<CheckBox>().Where(c => c.IsChecked == true).Sum(c => RowSlices(c).Length);
        int bCount = WheelBList.Items.OfType<CheckBox>().Where(c => c.IsChecked == true).Sum(c => RowSlices(c).Length);
        int most = Math.Max(aCount, bCount);
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_wheelsSummaryBase)) parts.Add(_wheelsSummaryBase);
        if (most > cap) parts.Add(Loc.F(O.OverCap, cap));
        WheelsSummary.Text = string.Join("\n", parts);
        WheelsSummary.Visibility = parts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        // Games append to Wheel B — once it's at the cap, more can't commit, so hide the add-game box.
        AddGameBox.Visibility = bCount >= cap ? Visibility.Collapsed : Visibility.Visible;
        // This runs after EVERY list mutation (ticks, adds) — keep the storefront-card checks in step.
        ReconcileStoreChecks();
    }

    private async void GameDropdown_Changed(object sender, SelectionChangedEventArgs e)
    {
        int i = GameDropdown.SelectedIndex;
        if (i < 0 || i >= _dropdownGames.Count) return;
        var game = _dropdownGames[i];
        GameDropdown.SelectedIndex = -1;   // reset — the dropdown is an "add this" action, not a persistent value
        GameDropdown.IsEnabled = false;
        _addingGame = true;   // Next must wait for the slice — a commit mid-build would drop the picked game
        RefreshStatus();
        try
        {
            var slice = await _hooks.BuildGameSlice(game);   // resolves the launch URL + fetches the logo
            slice = slice.WithLabelHidden();   // first-run games seed label-off (explicit false, even for a logo game)
            // The wheel may already carry this exact game (a re-run merged the user's own slices in, or the
            // dropdown was used twice). Tick the row that's already there rather than appending a twin —
            // two slices launching one game is the duplicate; two DIFFERENT games are not.
            if (FindRow(WheelBList, slice) is { } existing) existing.IsChecked = true;
            else WheelBList.Items.Add(MakeSliceCheck(slice, isChecked: true));
            _gameAdded = true;
            ApplyAddGameState();
            UpdateWheelsCapNote();
            ApplyWheelsLive();   // the added game shows on the very next wheel invoke
        }
        catch (Exception ex) { Trace.WriteLine($"[OOBE] add-game slice failed: {ex.Message}"); }
        finally
        {
            _addingGame = false;
            GameDropdown.IsEnabled = _dropdownGames.Count > 0;
            RefreshStatus();
        }
    }

    /// <summary>Commit the wheels choice — the individually-ticked items. Runs LIVE on every checklist
    /// change while the Customize step shows (see <see cref="ApplyWheelsLive"/>) and again on Next
    /// (idempotent backstop). An empty selection for a wheel leaves that wheel untouched — so a fresh
    /// install that touches nothing gets the full (all-ticked) starter layout, and a customized setup
    /// that touches nothing keeps its wheels.</summary>
    private bool ApplyWheelsChoice()
    {
        var a = WheelAList.Items.OfType<CheckBox>().Where(c => c.IsChecked == true).SelectMany(RowSlices).ToArray();
        var b = WheelBList.Items.OfType<CheckBox>().Where(c => c.IsChecked == true).SelectMany(RowSlices).ToArray();
        int cap = SystemConfig.MaxSlicesPerWheel;
        var (curA, curB) = _hooks.GetWheels();
        var writeA = a.Length > 0 ? a.Take(cap).ToArray() : curA;
        var writeB = b.Length > 0 ? b.Take(cap).ToArray() : curB;
        if (!ReferenceEquals(writeA, curA) || !ReferenceEquals(writeB, curB))
            return _hooks.WriteWheels(writeA, writeB);
        return true;
    }

    /// <summary>True when the current wheels differ from the shipped defaults (i.e. the user has real
    /// customizations that the starter layout would stomp). Deliberately-emptied wheels count too — an
    /// empty wheel is a real state (it acts as a disabled side), so an OOBE re-run must not default to
    /// overwriting it.</summary>
    private bool WheelsAreCustomized()
    {
        var (a, b) = _hooks.GetWheels();
        return JsonSerializer.Serialize(a) != JsonSerializer.Serialize(AppConfig.Default.WheelA)
            || JsonSerializer.Serialize(b) != JsonSerializer.Serialize(AppConfig.Default.WheelB);
    }

    // Launchers with a true fullscreen / big-picture mode — the tiebreaker when two storefronts have the
    // same game count (Steam Big Picture, Playnite Fullscreen; Xbox only maximizes, so it's not counted).
    private static readonly HashSet<string> FullscreenLaunchers =
        new(StringComparer.OrdinalIgnoreCase) { "steam", "playnite" };

    /// <summary>The launcher key for the user's single best-populated storefront, to seed the Right starter
    /// wheel. Playnite (if present, with its library count) competes with the live-scanned stores; the
    /// biggest library wins, ties broken toward a storefront with a true fullscreen mode (Steam/Playnite).
    /// Candidates are filtered to storefronts whose launcher app is actually present
    /// (<see cref="LauncherCatalog.IsPresent"/>) — a storefront's games can be installed without its launcher
    /// (e.g. GOG games without Galaxy), and seeding a slice for a missing launcher would be a dead button.
    /// Returns null when there's nothing to add.</summary>
    private static string? BestStarterLauncherKey(List<InstalledGame> enabled, int playniteCount)
    {
        var candidates = new List<(string key, int count, bool fullscreen)>();
        foreach (var grp in enabled.GroupBy(g => g.Storefront, StringComparer.OrdinalIgnoreCase))
            if (LauncherCatalog.FindByStorefront(grp.Key) is { } li && LauncherCatalog.IsPresent(li.Key))
                candidates.Add((li.Key, grp.Count(), FullscreenLaunchers.Contains(li.Key)));
        if (playniteCount > 0)
            candidates.Add(("playnite", playniteCount, true));
        if (candidates.Count == 0) return null;
        return candidates
            .OrderByDescending(c => c.count)
            .ThenByDescending(c => c.fullscreen)
            .ThenBy(c => c.key, StringComparer.OrdinalIgnoreCase)
            .First().key;
    }

    /// <summary>The fixed starter layout, gated by capabilities: Wheel A = system/comms, Wheel B =
    /// launch/play (Game Grid + a launcher for the single best-populated storefront + Discord). Slices a
    /// capability rules out simply don't appear. Icons/tints resolve from the per-type defaults at render
    /// time.</summary>
    private (WheelSlice[] a, WheelSlice[] b) BuildStarterWheels(string? bestLauncherKey)
    {
        int cap = SystemConfig.MaxSlicesPerWheel;   // preview and ApplyWheelsChoice must agree on the cap

        // Capability-gated slices (untickable per-item on the starter-wheels step itself).
        bool wantHdr     = HdrState.IsEnabled() is not null;
        bool wantAudio   = AudioDeviceSwitcher.OutputCount() > 1;
        bool wantDiscord = _discordInstalled;
        // XBox Mode switches the virtual pad between a virtual DS4 and a virtual Xbox 360. Omit the slice
        // for an XInput Xbox pad (Kind=Xbox) — it would do nothing: a captured Xbox pad is ALWAYS given an
        // Xbox 360 stand-in regardless of the toggle (a virtual DS4 would flip its button prompts), see
        // App.UpdateInputCaptureCore's wantType.
        bool wantPad     = _hooks.Kind() is not (ControllerKind.Xbox or ControllerKind.ExtraButtonPad);

        var a = new List<WheelSlice>
        {
            new() { Label = UiText.DefaultLabels.Sleep, Action = new ActionConfig { Type = "system", Command = "sleep", RequireConfirm = true } },
            // Closing the game you're in is the couch action with no keyboard-free alternative, so it is
            // seeded rather than left to be discovered. Hold-to-confirm, the same guard a fresh exit-app
            // slice gets from the Add picker — closing someone's game deserves the dwell.
            new() { Label = UiText.DefaultLabels.ExitCurrentApp, Action = new ActionConfig { Type = "exit-app", RequireConfirm = true } },
        };
        if (wantHdr)
            a.Add(new WheelSlice { Label = UiText.DefaultLabels.ToggleHdr, Action = new ActionConfig { Type = "system", Command = "hdr-toggle" } });
        a.Add(new WheelSlice { Label = UiText.DefaultLabels.Settings, Action = new ActionConfig { Type = "settings" } });
        a.Add(new WheelSlice { Label = UiText.DefaultLabels.MuteMic, Action = new ActionConfig { Type = "system", Command = "mic-mute" } });
        if (wantAudio)
            a.Add(new WheelSlice { Label = UiText.DefaultLabels.SwitchAudio, Action = new ActionConfig { Type = "switch-audio" } });
        if (wantPad)
            a.Add(new WheelSlice { Label = UiText.DefaultLabels.XboxMode, Action = new ActionConfig { Type = "xbox-emulation" } });

        var b = new List<WheelSlice>
        {
            new() { Label = UiText.DefaultLabels.GameGrid, Action = new ActionConfig { Type = "game-browser" } },
        };
        // Launcher for the single best-populated storefront (Playnite counts; fullscreen wins ties).
        if (bestLauncherKey is { } key && LauncherCatalog.Find(key) is { } li)
            b.Add(new WheelSlice { Label = li.ShortName, Action = new ActionConfig { Type = "launcher", Command = key } });
        if (wantDiscord)
            b.Add(new WheelSlice { Label = UiText.DefaultLabels.Discord, Action = new ActionConfig { Type = "discord-launch" } });
        // The arcade ships on the Right wheel as the Arcade Launcher — one slice, blank command, which opens
        // the cabinet picker over every game. Individual games are never seeded: the picker is the way in,
        // and a consented drop-in package is the user's own addition. Glyph and tint resolve from the type.
        if (Arcade.Available)
            b.Add(new WheelSlice { Label = UiText.DefaultLabels.Arcade, Action = new ActionConfig { Type = "arcade" } });

        // First-run seeded slices start with their label OFF, stored explicitly — WithLabelHidden sets
        // ShowLabel=false unconditionally, even for a logo slice, so "Select Manually" mode shows a
        // definite unchecked box rather than a null/indeterminate one.
        return ([.. a.Take(cap).Select(s => s.WithLabelHidden())],
                [.. b.Take(cap).Select(s => s.WithLabelHidden())]);
    }

    // ── Trigger-practice step (choices apply LIVE — the user tries them on the real wheel) ──────────

    private bool _practiceLoading;   // guard Checked/Selection events while (re)selecting programmatically
    private static Brush FreezeBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    private static readonly Brush CardNeutral = FreezeBrush(Color.FromArgb(0, 0, 0, 0));        // transparent (the hairline outline shows)
    private static readonly Brush CardDone    = FreezeBrush(Color.FromRgb(0xD8, 0xE5, 0xDE));  // performed → light tint of the app green #89B29A

    private ControllerKind _pKind = ControllerKind.DualSenseEdge;
    private bool _pConnected;                                // connected-state the current options were built for
    private string _recToken    = TriggerModes.FnButtons;    // the Recommended chord for this kind
    private string _customToken = TriggerModes.FnButtons;    // the Custom dropdowns' current chord
    private ComboBox? _custPrimary, _custMod;                // the Custom section's two dropdowns
    private bool _recDone, _customDone;                      // each invocation performed at least once
    private bool _recReleased;                               // …and the first recommended summon has been LET GO of
    private bool _edEnabled = true;                          // live wheels-enabled state (flips as the chord is practised)
    private string _chordTestedToken = "";                   // the chord the disable HALF was performed with
    // The kept trigger is simply ActiveChord: whatever the visible state shows is what Continue saves.
    // Every chord whose enable/disable ROUND TRIP has been completed. Keyed by token rather than one
    // shared bool so the two states keep their own progress: the Recommended and Custom columns practise
    // different chords, so "done" is a property of the chord, not of the step.
    private readonly HashSet<string> _chordDoneTokens = new(StringComparer.Ordinal);

    /// <summary>Has the currently-shown state's enable/disable round trip been completed?</summary>
    private bool ChordDone => _chordDoneTokens.Contains(ActiveChord);

    // ── Practice step states ──────────────────────────────────────────────────────────────────────
    // false = "Recommended" (default): only the Recommended + Enable/Disable cards, the illustration
    // pinned to the recommended chord, learning-focused tip. true = "Custom": the chord builder card
    // replaces Recommended, the illustration cycles/follows selection, pick-what-feels-right tip. The
    // header's Recommended|Custom segmented toggle slides between them.
    private bool _practiceCustom;

    /// <summary>Pick the half that was clicked (a segmented control selects, it doesn't flip) rather than
    /// inverting the state — clicking "Recommended" while already on Recommended must be a no-op, not a jump
    /// to Custom. Re-picking the current half also skips <see cref="NavDebounced"/>, so an idle click can't
    /// eat the debounce window for a real switch that follows it.</summary>
    private void PracticeModeToggle_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        RequestPracticeState(e.GetPosition(PracticeToggleGrid).X > PracticeToggleGrid.ActualWidth / 2);

    /// <summary>Keyboard parity with the Button this replaced (it was tab-focusable and space/enter-activated).
    /// ← / → select a specific side, which is what the visual invites.</summary>
    private void PracticeModeToggle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case System.Windows.Input.Key.Left:  RequestPracticeState(false); break;
            case System.Windows.Input.Key.Right: RequestPracticeState(true);  break;
            case System.Windows.Input.Key.Space:
            case System.Windows.Input.Key.Enter: RequestPracticeState(!_practiceCustom); break;
            default: return;
        }
        e.Handled = true;
    }

    private void RequestPracticeState(bool custom)
    {
        if (_steps[_index].Body != StepPractice) return;
        if (custom == _practiceCustom) return;
        if (NavDebounced()) return;
        SetPracticeState(custom, animate: true);
    }

    /// <summary>Keep the thumb under the selected half when the track is (re)measured — it's laid out after
    /// the first ApplyPracticeToggleVisual, when the half-width was still 0.</summary>
    private void PracticeToggleGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged) return;
        PracticeToggleSlide.BeginAnimation(TranslateTransform.XProperty, null);
        PracticeToggleSlide.X = _practiceCustom ? PracticeToggleGrid.ActualWidth / 2 : 0;
    }

    private static readonly Brush ToggleInkOn  = new SolidColorBrush(Color.FromRgb(0x3D, 0x40, 0x5B));
    private static readonly Brush ToggleInkOff = new SolidColorBrush(Color.FromArgb(0x8A, 0x00, 0x00, 0x00));

    /// <summary>Slide the thumb onto the selected half and swap which label reads as current. The 300 ms /
    /// QuadraticEase pairing matches the filmstrip glide in <see cref="SetPracticeState"/> exactly, so the
    /// toggle and the content it controls move as one gesture.</summary>
    private void ApplyPracticeToggleVisual(bool custom, bool animate)
    {
        PracticeToggleRecLabel.Foreground    = custom ? ToggleInkOff : ToggleInkOn;
        PracticeToggleCustomLabel.Foreground = custom ? ToggleInkOn  : ToggleInkOff;
        double target = custom ? PracticeToggleGrid.ActualWidth / 2 : 0;
        if (!animate)
        {
            PracticeToggleSlide.BeginAnimation(TranslateTransform.XProperty, null);
            PracticeToggleSlide.X = target;
            return;
        }
        PracticeToggleSlide.BeginAnimation(TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(target, new Duration(TimeSpan.FromMilliseconds(300)))
            { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut } });
    }

    /// <summary>Material step Preview button: open the Right wheel so the just-picked look shows at once.
    /// Clicks are DROPPED while a preview wheel is still on screen (or mid-fade): OpenWheel routes through
    /// ToggleOverlay, so a second click would close and instantly re-open the wheel, which reads as the
    /// preview flickering rather than as a toggle. <see cref="PreviewCooldownMs"/> covers the fade, during
    /// which the host already reports the overlay as gone.</summary>
    private void BtnPreviewWheel_Click(object sender, RoutedEventArgs e)
    {
        if (!PreviewReady()) return;
        _hooks.OpenWheel(true);
        SyncPreviewButton();
    }

    private const int PreviewCooldownMs = 350;
    private DateTime _previewReadyAt = DateTime.MinValue;
    private bool _previewWasVisible;

    private bool PreviewReady() => !_hooks.OverlayVisible() && DateTime.UtcNow >= _previewReadyAt;

    /// <summary>Grey the Preview button out for as long as clicks are being dropped, so the button's state
    /// matches what it will actually do. Driven off the wizard's poll (and the click itself).</summary>
    private void SyncPreviewButton()
    {
        if (BtnPreviewWheel.Visibility != Visibility.Visible) { _previewWasVisible = false; return; }
        bool up = _hooks.OverlayVisible();
        if (_previewWasVisible && !up)   // the wheel just went away — hold briefly through the fade
            _previewReadyAt = DateTime.UtcNow.AddMilliseconds(PreviewCooldownMs);
        _previewWasVisible = up;
        BtnPreviewWheel.IsEnabled = PreviewReady();
    }

    private bool _practiceWheelWasUp;

    /// <summary>Practice step: watch the overlay for the up→down edge that means the user LET GO of the
    /// wheel they just summoned. The enable/disable card is revealed on that edge rather than on the
    /// invocation itself — a card appearing mid-hold competes with the wheel the user is still aiming.
    /// Only the first release matters; the flag belongs to the Recommended state and
    /// survives a toggle to Custom and back — it is cleared only when the step rebuilds for a different
    /// controller (EnterPractice).</summary>
    private void PollWheelRelease()
    {
        if (_steps[_index].Body != StepPractice) { _practiceWheelWasUp = false; return; }
        bool up = _hooks.OverlayVisible();
        if (_practiceWheelWasUp && !up && _recDone && !_recReleased)
        {
            _recReleased = true;
            RefreshPracticeVisuals();
        }
        _practiceWheelWasUp = up;
    }

    /// <summary>True when a screen point lands on the Material step's material or thickness tiles. Those
    /// picks apply LIVE to an open preview wheel, so the host exempts them from click-to-dismiss — otherwise
    /// previewing materials with the mouse would kill the wheel on the very click that changed it. Anything
    /// else on this window (including the rest of the step) still dismisses, as before.</summary>
    private bool IsPointOverLookTiles(int screenPxX, int screenPxY) =>
        PointInside(MaterialTiles, screenPxX, screenPxY) || PointInside(MaterialTilesPremium, screenPxX, screenPxY)
        || PointInside(ThicknessTiles, screenPxX, screenPxY);

    /// <summary>Screen-pixel hit test against an element's own bounds. PointFromScreen does the DPI work, so
    /// this stays exact on any scale; a non-rendered element (or a bad transform mid-layout) is a miss.</summary>
    private static bool PointInside(FrameworkElement el, int screenPxX, int screenPxY)
    {
        try
        {
            if (!el.IsVisible || el.ActualWidth <= 0 || el.ActualHeight <= 0) return false;
            var p = el.PointFromScreen(new Point(screenPxX, screenPxY));
            return p.X >= 0 && p.Y >= 0 && p.X <= el.ActualWidth && p.Y <= el.ActualHeight;
        }
        catch { return false; }
    }

    /// <summary>Leave the wheels ENABLED when switching states. Progress survives a toggle (a finished
    /// Recommended run must survive a look at Custom and back), but a half-finished enable/disable round
    /// trip can't: the user is mid-trip with the wheels switched OFF, and carrying that across the slide
    /// would leave the other state's card pulsing for a re-enable the user never asked for. So the wheels
    /// come back on and only the dangling HALF is dropped — completed round trips stay in
    /// <see cref="_chordDoneTokens"/>.</summary>
    private void SettlePracticeWheels()
    {
        _chordTestedToken = "";
        if (_edEnabled) return;
        _edEnabled = true;
        _chordPulsing = false;
        _hooks.EnsureWheelsEnabled();
    }

    // Filmstrip geometry: how far to slide left to reveal the Customize column (= Recommended column
    // width + the inter-panel gap). Recomputed from the measured layout in PracticeViewport_SizeChanged.
    private double _slideOffset = 254;

    /// <summary>Position the filmstrip for the current state's target X (0 = Recommended shown, −offset =
    /// Customize shown), sizing the shared illustration to fill what's left of the viewport.</summary>
    private void PracticeViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The viewport is a Canvas, so its width never feeds back from the strip content — ActualWidth is
        // the stable stretched width from the step grid.
        double vw = PracticeViewport.ActualWidth;
        if (vw <= 0) return;
        double col = RecColStack.ActualWidth > 0 ? RecColStack.ActualWidth : 238;
        _slideOffset = col + 16;   // card column + the 16px inter-panel gap
        IllustHost.Width = Math.Max(0, vw - _slideOffset);
        Trace.WriteLine($"[OOBE] strip layout: vw={vw:F0} col={col:F0} illust={IllustHost.Width:F0} " +
                        $"custom@{col + 16 + IllustHost.Width + 16:F0} widthChanged={e.WidthChanged} custom={_practiceCustom}");
        // Only a WIDTH change re-anchors (killing any in-flight slide): height changes fire this too —
        // e.g. the enable-card text reflowing at the START of a toggle — and snapping X there ate the slide.
        if (!e.WidthChanged) return;
        PracticeSlide.BeginAnimation(TranslateTransform.XProperty, null);
        PracticeSlide.X = _practiceCustom ? -_slideOffset : 0;
    }

    /// <summary>Switch the practice step's state. The whole filmstrip slides left/right so the
    /// Recommended column exits left and the Customize column enters from the right (the illustration is
    /// the shared middle).</summary>
    private void SetPracticeState(bool custom, bool animate)
    {
        animate &= !MotionPolicy.Reduce;   // Reduce Motion: the filmstrip + toggle thumb snap into place
        _practiceCustom = custom;
        // Each state keeps its own progress (a completed Recommended run survives a trip to Custom and
        // back, and Next stays unlocked on return) — a toggle only settles the live wheel state.
        if (animate) SettlePracticeWheels();
        ApplyPracticeState();   // content/live-modes/illustration + card sync (both columns always present)
        ApplyPracticeToggleVisual(custom, animate);

        double target = custom ? -_slideOffset : 0;
        if (!animate)
        {
            SetPracticeAdviceText(custom);
            PracticeSlide.BeginAnimation(TranslateTransform.XProperty, null);
            PracticeSlide.X = target;
            return;
        }
        // The tip card FADES between the two states' copy while the strip slides: fade out, swap the text
        // at the midpoint, fade back in.
        var tipOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(140)));
        tipOut.Completed += (_, __) =>
        {
            SetPracticeAdviceText(custom);
            PracticeAdviceCard.BeginAnimation(UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(160))));
        };
        PracticeAdviceCard.BeginAnimation(UIElement.OpacityProperty, tipOut);
        // One smooth 300 ms glide — under NavDebounced's 450 ms window, which blocks a second toggle
        // landing mid-animation.
        PracticeSlide.BeginAnimation(TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(target, new Duration(TimeSpan.FromMilliseconds(300)))
            { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut } });
    }

    /// <summary>Apply the current state's layout/content: card visibilities + spacing, footer-button
    /// label, tip text, and the illustration policy (pinned recommended vs cycle/follow).</summary>
    private void ApplyPracticeState()
    {
        bool custom = _practiceCustom;   // both columns are always present now — the strip slide reveals one
        // (Toggle visuals are driven by ApplyPracticeToggleVisual, called from SetPracticeState alongside
        // this — not here, so a non-toggle refresh of this method doesn't re-animate the thumb.)
        // (Tip text is set by SetPracticeState — instantly on entry, via a FADE on an animated toggle.)
        // Only the recommended chord is live in the Recommended state; the full builder set in Custom.
        ApplyLiveModes();

        if (custom)
        {
            // Every switch INTO Custom restarts the option cycle — it runs until the user performs a
            // custom chord (OnWheelTried) or picks one in the dropdowns (OnCustomChordChanged), both of
            // which StopChordCycle and freeze on the selection.
            StartChordCycle();
        }
        else
        {
            StopChordCycle();
            UpdateChordImage(_recToken);   // pinned to the recommended invocation
        }

        // Toggling states changes the kept chord (it IS ActiveChord), so re-persist here too — otherwise
        // the saved trigger would lag the screen until the user hits Next. No-ops until something has
        // actually been performed, so a look around can't stomp a previously-saved gesture.
        ApplyPracticeConfig();

        // Keep the cards + footer in sync with the current progress and the ActiveChord — this is also how
        // entering Custom populates the Enable/Disable card with the selected custom chord's pair.
        RefreshPracticeVisuals();
        UpdateEnableDisableCard();
        UpdatePracticeNextLabel();
        RefreshStatus();
    }

    /// <summary>Paint the tip card for the given state — and paint the OTHER state's copy into the hidden
    /// ghost behind it, so the card measures to the taller of the two and keeps a fixed height across the
    /// toggle. Without that the card resized mid-transition and the whole strip below it (illustration
    /// included) slid up or down — see the ghost's remarks in the XAML.</summary>
    private void SetPracticeAdviceText(bool custom)
    {
        FillPracticeAdvice(PracticeAdviceText,  custom);
        FillPracticeAdvice(PracticeAdviceGhost, !custom);
    }

    /// <summary>The per-state tip copy (shared bold lead-in + the state's body).</summary>
    private void FillPracticeAdvice(TextBlock target, bool custom)
    {
        target.Inlines.Clear();
        // Bold lead-in one size up (12 → 14.4, +20%), then a line break before the per-state body.
        target.Inlines.Add(new System.Windows.Documents.Run(Loc.T(O.PracticeLead))
        { FontWeight = FontWeights.Bold, FontSize = 14.4 });
        target.Inlines.Add(new System.Windows.Documents.LineBreak());
        // The Recommended body names the actual recommended chord: the Edge's single Fn button, or the
        // bumper + Select/Start pair every other pad gets.
        string body = custom
            ? Loc.T(O.AdviceCustom)
            : Loc.T(_recToken == TriggerModes.FnButtons ? O.AdviceFn : O.AdviceBumper);
        target.Inlines.Add(new System.Windows.Documents.Run(body));

        // The Custom screen is where L4/R4 is picked, so its transport caveat is APPENDED to the usual
        // advice there rather than replacing it. "Un-remapped" is the load-bearing word: an onboard remap
        // makes the paddle send some other button, and Radiata stops seeing it on any transport.
        if (custom && _pKind == ControllerKind.ExtraButtonPad)
        {
            target.Inlines.Add(new System.Windows.Documents.Run(" "));
            foreach (var inline in InlineMarkup.Parse(Loc.T(O.PaddleCaveat), InlineMarkup.Card)) target.Inlines.Add(inline);
        }
    }

    /// <summary>The chord the step is currently practising — the CUSTOM dropdown selection in the Custom
    /// state (even before it's performed), the recommended chord in the Recommended state. Drives the
    /// Enable/Disable card + which enable/disable chord is live, so entering Custom immediately suggests
    /// the enable/disable pair for the selected custom chord. It is ALSO the kept
    /// trigger: the footer reads "Continue with {this}" and Next/Skip persist exactly this, so what the
    /// user is looking at is what they leave with, whichever side they practised more recently.</summary>
    private string ActiveChord => _practiceCustom ? _customToken : _recToken;

    /// <summary>Practice is satisfied once the chosen chord AND the enable/disable chord have each been
    /// performed (Next unlocks; Skip stays available; no pad = nothing to compel).</summary>
    private bool PracticeSatisfied => (_recDone || _customDone) && ChordDone;

    private void EnterPractice()
    {
        _practiceLoading = true;
        try
        {
            var kind = _hooks.Kind();
            bool pad0 = _hooks.ControllerConnected();
            // The options belong to a specific (kind, connected) state: the kind changed (pad swapped
            // mid-onboarding) OR the pad came/went (which flips whether Fn/touchpad are offered). Either
            // way tear down and rebuild the dropdowns, recommended chord, and performed-state fresh.
            if (_custPrimary is not null && (kind != _pKind || pad0 != _pConnected))
            {
                CustomChordRow.Content = null;
                _custPrimary = null; _custMod = null;
                _recDone = _customDone = _recReleased = false;
                _chordDoneTokens.Clear();
                _chordTestedToken = "";
            }
            _pKind = kind;
            _pConnected = pad0;
            PracticeAdviceCard.Visibility = Visibility.Visible;   // tip shows in BOTH states, pad or not

            // Recommended chord: the connected kind's own default — a dedicated button where the pad has
            // one (the Edge's Fn, an extra-button pad's L4/R4), Bumper+Back/Start otherwise. With NO pad we
            // can't assume any of that, so the universal chord. Deferring to TriggerModes.DefaultFor keeps
            // the recommendation and the never-onboarded default from drifting apart.
            _recToken = pad0 ? TriggerModes.DefaultFor(_pKind) : TriggerModes.ViewMenuBumpers;
            RecChordText.Text = _recToken == TriggerModes.FnButtons
                ? (_pKind == ControllerKind.ExtraButtonPad ? "L4/R4" : Loc.T(O.RecFn))
                : Loc.T(O.RecBumper);
            UpdateRecCardShape();   // split Hold/Tap halves for a two-button recommended chord

            // Custom dropdowns: built + seeded to the kind's default ONCE, then left as the user leaves them
            // (so returning to the step keeps their selection + progress).
            if (_custPrimary is null)
            {
                BuildCustomChordRow();
                // Seed Custom to Trigger + Back/Start so it starts DIFFERENT from the Recommended chord
                // (Bumper + Back/Start on non-Edge, Fn on the Edge). Falls back to the kind default if
                // Trigger somehow isn't offered.
                var dp = TriggerPrimary.Trigger; var dm = TriggerModifier.BackStart;
                if (!PracticePrimaries().Contains(dp)) (dp, dm) = TriggerModes.DefaultCombo(_pKind);
                SetCustomSelection(dp, dm);
                _customToken = TriggerModes.Compose(dp, dm) ?? _recToken;
            }
        }
        finally { _practiceLoading = false; }

        _edEnabled = true;                     // ShowStep re-enabled the wheels before this OnEnter
        ApplyPracticeConfig();                 // persist the chosen chord (only once one's actually performed)
        _hooks.SetSampleWheels(true);
        _hooks.SetWheelInvoked(OnWheelTried);
        _hooks.SetChordPracticed(OnChordTried);
        _hooks.SetChordButtonListener(OnPracticeButton);   // live green on the Hold/Tap halves
        ApplyLiveModes();                      // Recommended + Custom are both live to try
        RefreshPracticeVisuals();
        UpdateEnableDisableCard();
        UpdatePracticeNextLabel();
        // Land in the state the user last had OPEN (_practiceCustom survives leaving and re-entering the
        // step; it starts false, so a first visit opens on Recommended). ApplyPracticeState owns the
        // illustration policy: pinned recommended art in the Recommended state; the cycle-until-performed
        // behaviour in the Custom state.
        SetPracticeState(_practiceCustom, animate: false);
        // A hot-plug re-entry rebuilds CustomChordRow's content without passing through ShowStep — re-run
        // the selectable-text attach so the rebuilt Hold:/Tap: labels stay mouse-selectable.
        SelectableText.EnableWithin(this);
    }

    /// <summary>The primary-button options offered in the practice builder. With NO controller connected we
    /// can't know the pad, so device-specific Fn (Edge-only) and Touchpad are dropped — only Bumper/Trigger,
    /// which every pad has. Connected: the detected kind's full option set.</summary>
    private IEnumerable<TriggerPrimary> PracticePrimaries()
    {
        var all = TriggerModes.PrimariesFor(_pKind);
        return _hooks.ControllerConnected()
            ? all
            : all.Where(p => p != TriggerPrimary.Fn && p != TriggerPrimary.Touchpad);
    }

    /// <summary>Build the Custom section's [button] + [combined-with] dropdown pair — the same options as
    /// Settings ▸ Advanced ▸ Triggers (no "+ Add" here).</summary>
    private void BuildCustomChordRow()
    {
        _custPrimary = new ComboBox { Width = 83, VerticalAlignment = VerticalAlignment.Center };   // fits "Touchpad" closed
        foreach (var p in PracticePrimaries())
            _custPrimary.Items.Add(new ComboBoxItem { Content = TriggerModes.PrimaryLabel(p, _pKind), Tag = p });
        _custMod = new ComboBox { Width = 98, VerticalAlignment = VerticalAlignment.Center };   // fits "Select/Start" closed
        _custPrimary.SelectionChanged += (_, __) => { if (_practiceLoading) return; FillCustomModifiers(TriggerModifier.None); OnCustomChordChanged(); };
        _custMod.SelectionChanged     += (_, __) => { if (!_practiceLoading) OnCustomChordChanged(); };

        // Hold:/Tap: column headers (section-header typeface), left-edge aligned with their dropdowns.
        // A 2-row Grid keeps the labels in row 0 and the dropdowns + joiner in row 1, so the circled-plus
        // sits vertically centred BETWEEN the two dropdowns (not floating at the card bottom).
        var holdLabel = new TextBlock { Text = Loc.T(UiText.Onboarding.Hold) + ":", FontWeight = FontWeights.SemiBold, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
        var tapLabel  = new TextBlock { Text = Loc.T(UiText.Onboarding.Tap) + ":", FontWeight = FontWeights.SemiBold, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
        var plus = new System.Windows.Controls.Image
        {
            Source = PackIconHelper.FromName("PlusCircle",
                new SolidColorBrush((Color)System.Windows.Application.Current.Resources["UiMutedInkColor"])),
            Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3.5, 0, 3.5, 0),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // labels
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // dropdowns + joiner
        Grid.SetColumn(holdLabel, 0);   Grid.SetRow(holdLabel, 0);
        Grid.SetColumn(tapLabel, 2);    Grid.SetRow(tapLabel, 0);
        Grid.SetColumn(_custPrimary, 0); Grid.SetRow(_custPrimary, 1);
        Grid.SetColumn(plus, 1);        Grid.SetRow(plus, 1);
        Grid.SetColumn(_custMod, 2);    Grid.SetRow(_custMod, 1);
        grid.Children.Add(holdLabel);
        grid.Children.Add(tapLabel);
        grid.Children.Add(_custPrimary);
        grid.Children.Add(plus);
        grid.Children.Add(_custMod);
        CustomChordRow.Content = grid;
    }

    private void SetCustomSelection(TriggerPrimary p, TriggerModifier m)
    {
        bool prev = _practiceLoading; _practiceLoading = true;
        _custPrimary!.SelectedItem = _custPrimary.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (TriggerPrimary)i.Tag! == p) ?? _custPrimary.Items[0];
        FillCustomModifiers(m);
        _practiceLoading = prev;
    }

    /// <summary>Make sure the primary dropdown carries <paramref name="p"/> before selecting it. The list is
    /// built from <see cref="PracticePrimaries"/>, which omits device-specific primaries (Fn, Touchpad) when
    /// no pad is connected — but the host's Fn handler opens a wheel during practice regardless, so a user
    /// CAN perform a chord the list doesn't offer. Appending it is strictly better than refusing to display
    /// the chord they just used.</summary>
    private void EnsurePrimaryOffered(TriggerPrimary p)
    {
        if (_custPrimary is null) return;
        if (_custPrimary.Items.Cast<ComboBoxItem>().Any(i => (TriggerPrimary)i.Tag! == p)) return;
        bool prev = _practiceLoading; _practiceLoading = true;
        _custPrimary.Items.Add(new ComboBoxItem { Content = TriggerModes.PrimaryLabel(p, _pKind), Tag = p });
        _practiceLoading = prev;
    }

    private void FillCustomModifiers(TriggerModifier want)
    {
        bool prev = _practiceLoading; _practiceLoading = true;
        _custMod!.Items.Clear();
        var p = (TriggerPrimary)((ComboBoxItem)_custPrimary!.SelectedItem).Tag!;
        foreach (var m in TriggerModes.ModifiersFor(p, _pKind))
            _custMod.Items.Add(new ComboBoxItem { Content = TriggerModes.ModifierLabel(m, p), Tag = m });
        _custMod.SelectedItem = _custMod.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (TriggerModifier)i.Tag! == want) ?? _custMod.Items[0];
        _custMod.IsEnabled = _custMod.Items.Count > 1;
        _practiceLoading = prev;
    }

    /// <summary>A Custom dropdown changed: recompute the custom token, un-confirm it (must be performed
    /// again), make the new chord live, and (if it was the current keeper) fall the choice back.</summary>
    private void OnCustomChordChanged()
    {
        var p = (TriggerPrimary)((ComboBoxItem)_custPrimary!.SelectedItem).Tag!;
        var m = (TriggerModifier)((ComboBoxItem)_custMod!.SelectedItem).Tag!;
        _customToken = TriggerModes.Compose(p, m) ?? _recToken;
        _customDone = false;   // the newly-picked chord hasn't been performed yet
        ApplyLiveModes();
        ApplyPracticeConfig();
        RefreshPracticeVisuals();
        UpdateEnableDisableCard();
        UpdatePracticeNextLabel();
        StopChordCycle();                 // a manual dropdown pick ends the on-arrival cycle…
        UpdateChordImage(_customToken);   // …and shows the chord the user just picked (about to try it)
        RefreshStatus();
    }

    /// <summary>Make EVERY invocation the Custom builder can produce for this kind live at once — the full
    /// (primary × modifier) set, NOT the curated recommended list (`TriggerModes.For(kind)`), which omits the
    /// Back/Start and D-Pad variants on Sony pads (and D-Pad on every kind). The interpreter also always
    /// evaluates Fn + touchpad. The user can perform ANY of them in any order; <see cref="OnWheelTried"/>
    /// flips the Custom dropdown to whichever they last did (unless it was the Recommended one).</summary>
    private void ApplyLiveModes()
    {
        // Recommended state: ONLY the recommended chord opens a wheel — nothing else is live, so the user
        // can't accidentally summon with another gesture while learning it.
        if (!_practiceCustom) { _hooks.SetPracticeAll(true, new List<string> { _recToken }); return; }
        var all = new List<string>();
        foreach (var p in PracticePrimaries())
            foreach (var mod in TriggerModes.ModifiersFor(p, _pKind))
                if (TriggerModes.Compose(p, mod) is { } tok && !all.Contains(tok))
                    all.Add(tok);
        // While the user is BUILDING an L4/R4 + button chord, a lone L4/R4 press must do nothing: it is
        // itself the "fn" gesture, so leaving it live opens a wheel on the first half and the chord can
        // never be performed (nor its card ticked) — and the open would adopt "fn" into the dropdowns,
        // silently undoing the selection the user just made.
        if (TriggerModes.IsExtraChord(_customToken) && all.Remove(TriggerModes.FnButtons))
            Trace.WriteLine($"[OOBE] practice: lone L4/R4 held dead while \"{_customToken}\" is selected " +
                            "— it would open a wheel on the chord's first half");
        _hooks.SetPracticeAll(true, all);
    }

    /// <summary>Persist the kept chord — only once one has actually been PERFORMED. Merely entering the
    /// step, skipping it, or fiddling the Custom dropdowns must never replace a previously-saved trigger.
    /// Activation + "reverse wheels" are not part of this step — leave their config values alone.</summary>
    private void ApplyPracticeConfig()
    {
        if (_practiceLoading || !(_recDone || _customDone)) return;
        SavePracticeChord(ActiveChord);
    }

    /// <summary>Persist a specific chord as the kept trigger for this controller kind. Used both by the
    /// "performed a chord" path (via ApplyPracticeConfig) and by Skip Testing, which commits the chord
    /// currently shown in the footer ("Use {…}") even when nothing was tested.</summary>
    private bool SavePracticeChord(string token)
    {
        var sys = _hooks.GetSystem();
        var modes = new Dictionary<string, List<string>>(sys.TriggerModes) { [_pKind.ToString()] = [token] };
        return _hooks.WriteSystem(sys with { TriggerModes = modes });
    }

    /// <summary>The Controller\ illustration for a chord token, or null when no art exists for it
    /// (only the legacy Select/Start+stick token now) — null falls back to the generic labeled
    /// line-art (Assets\controls.png). The dedicated-button token draws whichever buttons the connected
    /// pad actually has: the Edge's Fn pair, or an extra-button pad's L4/R4.</summary>
    private string? ChordImageFile(string token) => token switch
    {
        TriggerModes.FnButtons        => _pKind == ControllerKind.ExtraButtonPad ? "R4-L4" : "Fn",
        // Each L4/R4 chord draws the paddles plus its second half.
        TriggerModes.ExtraTriggers    => "R4-L4+Trigger",
        TriggerModes.ExtraBumpers     => "R4-L4+Bumper",
        TriggerModes.ExtraHome        => "R4-L4+Home",
        TriggerModes.ExtraStick       => "R4-L4+Sticks",
        TriggerModes.ExtraSelectStart => "R4-L4+Select-Start",
        TriggerModes.ExtraDpad        => "R4-L4+Dpad",
        TriggerModes.TouchpadSwipe    => "Touchpad",
        TriggerModes.BumpersHome      => "Bumper+Home",
        TriggerModes.ViewMenuBumpers  => "Bumper+Select-Start",
        TriggerModes.BumpersStick     => "Bumper+Sticks",
        TriggerModes.BumpersTriggers  => "Bumper+Trigger",
        TriggerModes.BumpersDpad      => "Bumper+Dpad",
        TriggerModes.TriggersHome     => "Trigger+Home",
        TriggerModes.ViewMenuTriggers => "Trigger+Select-Start",
        TriggerModes.TriggersStick    => "Trigger+Sticks",
        TriggerModes.TriggersDpad     => "Trigger+Dpad",
        _                             => null,
    };

    /// <summary>Swap the practice illustration to <paramref name="token"/>'s art. Called wherever the
    /// "current" chord changes: step entry (the keeper), a Custom dropdown change (the chord the user is
    /// about to try), and a performed invocation. Fail-soft: a missing resource keeps the current image.</summary>
    private void UpdateChordImage(string token)
    {
        if (_chordCycleTimer is not null) return;   // the on-arrival cycle owns the image until a chord is performed
        if (!_practiceCustom) token = _recToken;    // Recommended state: only the recommended invocation's art
        var uri = ChordImageFile(token) is { } f
            ? $"pack://application:,,,/Controller/{f}.png"
            : "pack://application:,,,/Assets/controls.png";
        SetChordImageSource(PracticeChordImage, uri);
        SetChordLegend(ChordImageFile(token) is not null);
    }

    // -- Localized chord-art legend --
    // Every Controller\*.png is 1362x881 and bakes the English words SIMPLE (x 411-508) and DISTINCT
    // (x 853-974) into the same band (y 788-819), with the five scale dots between them (x 528-833).
    // Outside English each word is covered by a window-coloured rectangle and redrawn translated.
    // Positions are fractions of the image, so they track the rendered size. Assets\controls.png has no legend.
    private const double LegendArtW = 1362, LegendArtH = 881;
    private const double LegendCoverTop = 780, LegendCoverBottom = 827, LegendWordMidY = 803.5;
    private const double LegendCoverLeftL = 403, LegendWordEdgeL = 508, LegendCoverRightL = 516;
    private const double LegendCoverLeftR = 845, LegendWordEdgeR = 853, LegendCoverRightR = 982;
    private const double LegendCapHeight = 32;   // image px of the baked caps; the redrawn word matches it
    private bool _legendShown;

    private void InitChordLegend()
    {
        if (Loc.Lang == HelpLocalization.DefaultCode) return;   // English shows the art untouched
        LegendWordL.Text = Legend(Loc.T(O.LegendSimple));
        LegendWordR.Text = Legend(Loc.T(O.LegendDistinct));
        var face = Loc.UsesSystemFace ? LocWpf.LanguageFamily
                                      : new System.Windows.Media.FontFamily("Bahnschrift SemiBold Condensed, Segoe UI Semibold");
        LegendWordL.FontFamily = LegendWordR.FontFamily = face;
        PracticeChordImage.SizeChanged += (_, __) => LayoutChordLegend();
    }

    // The art draws capitals; scripts without case keep their own form.
    private static string Legend(string s) => s.ToUpper(Loc.Culture);

    /// <summary>Show the legend overlay only over art that carries the legend.</summary>
    private void SetChordLegend(bool artHasLegend)
    {
        _legendShown = artHasLegend && Loc.Lang != HelpLocalization.DefaultCode;
        ChordLegend.Visibility = _legendShown ? Visibility.Visible : Visibility.Collapsed;
        if (_legendShown) LayoutChordLegend();
    }

    private void LayoutChordLegend()
    {
        if (!_legendShown) return;
        double w = PracticeChordImage.ActualWidth, h = PracticeChordImage.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double sx = w / LegendArtW, sy = h / LegendArtH;
        ChordLegend.Width = w; ChordLegend.Height = h;

        void Cover(System.Windows.Shapes.Rectangle r, double x0, double x1)
        {
            Canvas.SetLeft(r, x0 * sx); Canvas.SetTop(r, LegendCoverTop * sy);
            r.Width = (x1 - x0) * sx;   r.Height = (LegendCoverBottom - LegendCoverTop) * sy;
        }
        Cover(LegendCoverL, LegendCoverLeftL, LegendCoverRightL);
        Cover(LegendCoverR, LegendCoverLeftR, LegendCoverRightR);

        // Word boxes are wider than the covers so a long translation grows away from the dots.
        double fs = LegendCapHeight * sy / 0.7 * (Loc.UsesSystemFace ? 0.85 : 1);
        double boxW = 480 * sx;
        foreach (var (tb, left) in new[] { (LegendWordL, LegendWordEdgeL * sx - boxW), (LegendWordR, LegendWordEdgeR * sx) })
        {
            tb.FontSize = fs; tb.Width = boxW;
            tb.Measure(new Size(boxW, double.PositiveInfinity));
            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, LegendWordMidY * sy - tb.DesiredSize.Height / 2);
        }
    }

    private static void SetChordImageSource(System.Windows.Controls.Image img, string uri)
    {
        try { img.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(uri, UriKind.Absolute)); }
        catch { /* missing/renamed art — never break the wizard over an illustration */ }
    }

    // ── On-arrival illustration cycle ──────────────────────────────────────────────────────────────
    // Before the user performs any summon chord, the illustration crossfades through every chord's art:
    // hold 1 s, 0.5 s fade to the next, looping. The first successful summon (OnWheelTried) stops it, as
    // does leaving the step. Two Image layers (PracticeChordImage / …Alt) alternate the fade.
    private System.Windows.Threading.DispatcherTimer? _chordCycleTimer;
    private List<string> _chordCycleUris = new();
    private int _chordCycleIndex;
    private bool _chordCycleTopActive;   // true = PracticeChordImage is the layer currently shown
    private const double ChordArtOpacity = 0.9;

    private void StartChordCycle()
    {
        StopChordCycle();
        if (!_practiceCustom) return;   // cycling is Custom-state behaviour; Recommended pins its own art
        if (MotionPolicy.Reduce) return;   // Reduce Motion: no perpetual slideshow — hold the current art
        // Distinct art for every chord the practice builder offers this pad (Fn/Touchpad drop off with no
        // pad connected). Deduped by file so shared illustrations don't repeat back-to-back.
        var seen = new HashSet<string>();
        _chordCycleUris = new List<string>();
        foreach (var p in PracticePrimaries())
            foreach (var mod in TriggerModes.ModifiersFor(p, _pKind))
                if (TriggerModes.Compose(p, mod) is { } tok && ChordImageFile(tok) is { } f && seen.Add(f))
                    _chordCycleUris.Add($"pack://application:,,,/Controller/{f}.png");
        if (_chordCycleUris.Count <= 1) return;   // nothing to fade between — leave the static image be

        _chordCycleIndex     = 0;
        _chordCycleTopActive = true;
        SetChordImageSource(PracticeChordImage, _chordCycleUris[0]);
        SetChordLegend(true);
        PracticeChordImage.Opacity    = ChordArtOpacity;
        PracticeChordImageAlt.Opacity = 0;

        // Period = 1 s hold + 0.5 s fade; the fade begins on each tick, so each image sits fully opaque
        // for the 1 s between the fade completing and the next tick.
        _chordCycleTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _chordCycleTimer.Tick += (_, __) => AdvanceChordCycle();
        _chordCycleTimer.Start();
    }

    private void AdvanceChordCycle()
    {
        _chordCycleIndex = (_chordCycleIndex + 1) % _chordCycleUris.Count;
        var incoming = _chordCycleTopActive ? PracticeChordImageAlt : PracticeChordImage;
        var outgoing = _chordCycleTopActive ? PracticeChordImage : PracticeChordImageAlt;
        SetChordImageSource(incoming, _chordCycleUris[_chordCycleIndex]);
        var dur = new Duration(TimeSpan.FromMilliseconds(500));
        incoming.BeginAnimation(UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, ChordArtOpacity, dur));
        outgoing.BeginAnimation(UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(ChordArtOpacity, 0, dur));
        _chordCycleTopActive = !_chordCycleTopActive;
    }

    private void StopChordCycle()
    {
        if (_chordCycleTimer is null) return;
        _chordCycleTimer.Stop();
        _chordCycleTimer = null;
        // Release the opacity animations so a later static Source shows at full; hide the Alt layer.
        PracticeChordImage.BeginAnimation(UIElement.OpacityProperty, null);
        PracticeChordImageAlt.BeginAnimation(UIElement.OpacityProperty, null);
        PracticeChordImage.Opacity    = ChordArtOpacity;
        PracticeChordImageAlt.Opacity = 0;
    }

    /// <summary>Paint the cards: only the LAST-tried side (Recommended vs Custom) carries the green check +
    /// tint — that's the trigger being kept — so the two are mutually exclusive. The enable/disable card
    /// checks only after a full disable→re-enable round trip; while the wheels sit DISABLED mid-trip its
    /// background slowly PULSES toward the success green instead.</summary>
    // The enable/disable card is duplicated per column (Rec + Custom) so it slides with its state; these
    // resolve to the CURRENTLY-shown state's controls. The off-screen twin is kept neutral in Refresh.
    private Border    ActiveEnableCard  => _practiceCustom ? CustomEnableCard  : RecEnableCard;
    private TextBlock ActiveEnableTitle => _practiceCustom ? CustomEnableTitle : RecEnableTitle;
    private TextBlock ActiveEnableText  => _practiceCustom ? CustomEnableText  : RecEnableText;
    private UIElement ActiveEnableCheck => _practiceCustom ? CustomEnableCheck : RecEnableCheck;

    private void RefreshPracticeVisuals()
    {
        // Tint AND check both track that side's OWN completion. There is no "keeper" to point at: the
        // visible state IS the kept chord (see ActiveChord), so each card simply reports whether its own
        // invocation was done.
        RecCard.Background     = _recDone ? CardDone : CardNeutral;
        RecCheck.Visibility    = _recDone ? Visibility.Visible : Visibility.Collapsed;
        // Once the chord landed, both halves stay green; before that OnPracticeButton paints them live
        // per-button. And the Disable card only appears after the open is achieved (Recommended state —
        // the reveal is the "what's next" cue).
        if (_recDone) { RecHoldHalf.Background = CardDone; RecTapHalf.Background = CardDone; }
        // …and only once that first wheel has been RELEASED, not merely invoked: revealing it mid-hold
        // puts a new card on screen while the user is still aiming the wheel they just opened.
        // _recReleased is set by PollWheelRelease on the overlay's up→down edge.
        RecEnableCard.Visibility = _recReleased ? Visibility.Visible : Visibility.Collapsed;
        CustomCard.Background   = _customDone ? CardDone : CardNeutral;
        CustomCheck.Visibility  = _customDone ? Visibility.Visible : Visibility.Collapsed;
        // Keep the off-screen enable card neutral; paint only the active one.
        var idle = _practiceCustom ? RecEnableCard : CustomEnableCard;
        idle.Background = CardNeutral;
        (_practiceCustom ? RecEnableCheck : CustomEnableCheck).Visibility = Visibility.Collapsed;
        bool chordDone = ChordDone;
        if (!_edEnabled && !chordDone) StartChordPulse();
        else { _chordPulsing = false; ActiveEnableCard.Background = chordDone ? CardDone : CardNeutral; }
        ActiveEnableCheck.Visibility = chordDone ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Split Hold/Tap card with live per-button green (Recommended state only) ─────────────────

    private readonly HashSet<string> _liveButtons = new(StringComparer.Ordinal);

    /// <summary>Single vs split card for the recommended chord: two-button chords show live Hold/Tap
    /// halves; Fn (single-button) keeps the plain text card.</summary>
    private void UpdateRecCardShape()
    {
        bool split = _recToken != TriggerModes.FnButtons
                     && TriggerModes.TryDecompose(_recToken, out var p, out var m)
                     && m is not TriggerModifier.None and not TriggerModifier.Swipe;
        RecChordText.Visibility = split ? Visibility.Collapsed : Visibility.Visible;
        RecSplitGrid.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        if (split && TriggerModes.TryDecompose(_recToken, out var p2, out var m2))
        {
            RecHoldText.Text = TriggerModes.PrimaryLabel(p2, _pKind);
            RecTapText.Text  = TriggerModes.ModifierLabel(m2, p2);
        }
        RecHoldHalf.Background = _recDone ? CardDone : CardNeutral;
        RecTapHalf.Background  = _recDone ? CardDone : CardNeutral;
    }

    /// <summary>Raw button forwarding from the host: paint each half green WHILE its button is held —
    /// until the chord is achieved, after which both stay green (RefreshPracticeVisuals).</summary>
    private void OnPracticeButton(string token, bool pressed)
    {
        if (pressed) _liveButtons.Add(token); else _liveButtons.Remove(token);
        if (_recDone || _practiceCustom || RecSplitGrid.Visibility != Visibility.Visible) return;
        if (!TriggerModes.TryDecompose(_recToken, out var p, out var m)) return;
        RecHoldHalf.Background = PrimaryDown(p)  ? CardDone : CardNeutral;
        RecTapHalf.Background  = ModifierDown(m) ? CardDone : CardNeutral;
    }

    private bool PrimaryDown(TriggerPrimary p) => p switch
    {
        TriggerPrimary.Bumper  => _liveButtons.Contains("l1") || _liveButtons.Contains("r1"),
        TriggerPrimary.Trigger => _liveButtons.Contains("l2") || _liveButtons.Contains("r2"),
        _ => false,
    };

    private bool ModifierDown(TriggerModifier m) => m switch
    {
        TriggerModifier.Home      => _liveButtons.Contains("ps"),
        TriggerModifier.Stick     => _liveButtons.Contains("l3") || _liveButtons.Contains("r3"),
        TriggerModifier.BackStart => _liveButtons.Contains("create") || _liveButtons.Contains("options"),
        TriggerModifier.Dpad      => _liveButtons.Contains("dpad"),
        TriggerModifier.Trigger   => _liveButtons.Contains("l2") || _liveButtons.Contains("r2"),
        _ => false,
    };

    private bool _chordPulsing;   // an animated brush is on the enable card — don't restart it every refresh

    /// <summary>Slow breathing pulse on the enable/disable card while the wheels sit disabled mid-round-trip:
    /// a transparent→opaque fade of the SAME success green (no hue shift), auto-reversing forever. Ends by
    /// simply assigning a static brush over the animated one (RefreshPracticeVisuals' else branch).</summary>
    private void StartChordPulse()
    {
        if (_chordPulsing) return;
        _chordPulsing = true;
        // Reduce Motion: the "wheels are disabled mid-round-trip" state is essential, the breathing isn't —
        // hold the same success green statically at half strength instead of pulsing to it.
        if (MotionPolicy.Reduce)
        {
            ActiveEnableCard.Background = new SolidColorBrush(Color.FromArgb(0x80, 0xD8, 0xE5, 0xDE));
            return;
        }
        var brush = new SolidColorBrush(Color.FromArgb(0x00, 0xD8, 0xE5, 0xDE));
        ActiveEnableCard.Background = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(
                Color.FromArgb(0x00, 0xD8, 0xE5, 0xDE), Color.FromRgb(0xD8, 0xE5, 0xDE),
                new Duration(TimeSpan.FromMilliseconds(550)))
            {
                AutoReverse    = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut },
            });
    }

    /// <summary>ShowOverlay fired during practice: the user summoned a wheel with <paramref name="token"/>.
    /// In the RECOMMENDED state only the recommended chord is live, so it just marks that card done. In the
    /// CUSTOM state ANY performed chord — INCLUDING the recommended one — is adopted into the builder
    /// dropdowns, so "Custom" always reflects the last thing the user did.</summary>
    private void OnWheelTried(string? token)
    {
        if (token is null) return;
        // Recommended state: ONLY the recommended chord counts (nothing else should even be live — see
        // ApplyLiveModes + EvalTouch's practice gate). A stray other token here (defensive) is ignored
        // rather than silently adopted into the hidden Custom builder + persisted.
        if (!_practiceCustom && token != _recToken) return;
        StopChordCycle();            // a real summon happened — freeze on its illustration below
        // Arm the release watch from the invoke itself: the wheel is up NOW, and a hold shorter than the
        // 150 ms poll would otherwise never be SEEN up, so its release edge would be missed entirely.
        _practiceWheelWasUp = true;
        if (!_practiceCustom && token == _recToken) { _recDone = true; }
        else
        {
            // Custom state: adopt whatever was performed (the recommended chord is treated like any other
            // here — it selects itself in the dropdowns). The card must always follow what was performed —
            // an unlisted primary is added to the dropdown rather than silently dropped; rendering one
            // chord while the state claims another ticks the card green next to a chord the user never did.
            if (TriggerModes.TryDecompose(token, out var p, out var m))
            {
                EnsurePrimaryOffered(p);
                SetCustomSelection(p, m);
                _customToken = token;
                _customDone  = true;
            }
            else
            {
                // No builder representation at all (only the legacy viewmenu-stick token, which shouldn't
                // even be live during practice). Don't adopt it — the card physically cannot show it, and
                // claiming it would re-create the mismatch above. Return before the visual refreshes below:
                // nothing changed, and repointing the illustration at a chord the card rejected would be a
                // milder version of the same card/state disagreement.
                Trace.WriteLine($"[OOBE] practice: '{token}' has no builder form — not adopted as the custom chord");
                return;
            }
        }
        ApplyPracticeConfig();
        RefreshPracticeVisuals();
        UpdateEnableDisableCard();   // the chosen chord changed → its enable/disable chord did too
        UpdatePracticeNextLabel();
        UpdateChordImage(token);     // illustrate what was just performed
        RefreshStatus();             // Next gating follows PracticeSatisfied
    }

    /// <summary>The enable/disable chord fired — flip the tracked wheel state and retitle the card. The
    /// card only CHECKS (and Next only unlocks) after the full round trip: disable, then RE-ENABLE with
    /// the displayed chord. While disabled, the card pulses toward the success green as a "finish the
    /// round trip" cue (a check on the first half would read as done-when-disabled).</summary>
    private void OnChordTried()
    {
        _edEnabled = !_edEnabled;
        if (!_edEnabled)
        {
            _chordTestedToken = ActiveChord;               // first half performed with this chord…
        }
        else if (_chordTestedToken == ActiveChord)
        {
            _chordDoneTokens.Add(ActiveChord);             // …and the re-enable completes it, for THAT chord
        }
        RefreshPracticeVisuals();
        UpdateEnableDisableCard();
        RefreshStatus();
    }

    /// <summary>Retitle the Enable/Disable card to the EXACT chord for the current selection + wheel state,
    /// e.g. "Disable Wheels: Fn1 + Fn2". D-pad chords are DIRECTIONAL (Up = enable, Down = disable), so the
    /// card shows the direction the current state needs, not a single toggle. Also tells the host that ONLY
    /// this chord's enable/disable is live — practicing a different pair than the one displayed would teach
    /// the wrong muscle memory.</summary>
    private void UpdateEnableDisableCard()
    {
        _hooks.SetPracticeChordToken(ActiveChord);
        ActiveEnableTitle.Text = Loc.T(_edEnabled ? O.TryDisabling : O.TryEnabling);
        ActiveEnableText.Text = Cap(ActiveChord switch
        {
            TriggerModes.BumpersDpad  => Loc.F(O.ChordBumpersDpad,  Loc.T(_edEnabled ? O.DpadDown : O.DpadUp)),
            TriggerModes.TriggersDpad => Loc.F(O.ChordTriggersDpad, Loc.T(_edEnabled ? O.DpadDown : O.DpadUp)),
            TriggerModes.ExtraDpad    => Loc.F(O.ChordExtraDpad,    Loc.T(_edEnabled ? O.DpadDown : O.DpadUp)),
            _                         => EnableDisableChordLabel(ActiveChord),
        });
    }

    /// <summary>Capitalize the first letter only (the chord line reads as a sentence-cased phrase).</summary>
    private static string Cap(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;

    /// <summary>The buttons that toggle the wheels for a given (non-D-pad) invocation chord — matches
    /// TriggerInterpreter.EnableDisableFires.</summary>
    private string EnableDisableChordLabel(string token) => TriggerModes.EnableDisableLabel(token, _pKind);

    /// <summary>Label the practice-step Next button with the kept chord, e.g. "Use Bumper + Back/Start".</summary>
    private void UpdatePracticeNextLabel()
    {
        if (_steps[_index].Body != StepPractice) return;
        // The Fn parenthetical is dropped here — the button is cramped and the illustration already
        // shows where Fn lives.
        BtnNext.Content = Loc.F(O.ContinueWith, ActiveChord == TriggerModes.FnButtons
            ? (_pKind == ControllerKind.ExtraButtonPad ? "L4/R4" : "Fn") : ChordLabel(ActiveChord));
    }

    /// <summary>The invocation kept for the current controller kind — reminded on the Customize, Material,
    /// and Roll-out steps. A saved trigger wins; otherwise mirror the practice step's recommendation
    /// exactly (Fn ONLY for a CONNECTED Edge — with no pad we can't assume an Edge, so Bumper +
    /// Back/Start), keeping the later steps in agreement with the step-2 recommendation.</summary>
    private string CurrentChord()
    {
        var sys = _hooks.GetSystem();
        var kind = _hooks.Kind();
        if (sys.TriggerModes.TryGetValue(kind.ToString(), out var list) && list is { Count: > 0 })
            return list[0];
        // With a pad connected its kind's own default applies (Fn on the Edge, L4/R4 on an extra-button
        // pad); with none we can't assume dedicated buttons, so the universal chord.
        return _hooks.ControllerConnected() ? TriggerModes.DefaultFor(kind) : TriggerModes.ViewMenuBumpers;
    }

    /// <summary>Human label for a chord token, e.g. "Fn (below thumbsticks)", "Bumper + Back/Start".</summary>
    private string ChordLabel(string token)
    {
        if (token == TriggerModes.FnButtons)
            return _hooks.Kind() == ControllerKind.ExtraButtonPad ? "L4/R4" : Loc.T(O.RecFn);
        if (!TriggerModes.TryDecompose(token, out var p, out var m)) return Loc.T(O.YourChord);
        return $"{TriggerModes.PrimaryLabel(p, _pKind)} + {TriggerModes.ModifierLabel(m, p)}";
    }

    // ── Wheel-look step (material applies LIVE) ─────────────────────────────────

    // GRID ORDER in the 4-across MaterialTiles UniformGrid: 0-3 = row 1, 4-7 = row 2. Keep identical to
    // CustomizeEditorControl.MaterialTiles — the two surfaces must not drift.
    private static readonly (string Token, string Name)[] Materials =
    [
        // Row 1
        ("flat-light",  "Flat Light"),
        ("pearl",       "Pearl"),
        ("mesa",        "Mesa"),
        ("kawaii",      "Kawaii"),
        // Row 2
        ("flat-dark",   "Flat Dark"),   // default
        ("obsidian",    "Obsidian"),
        ("salvage",     "Salvage"),
        ("reactor",     "Reactor"),
    ];
    private const int MaterialColumns = 4;   // top-row-shadow cut: the first tile of EACH group's top row

    /// <summary>Which of this step's two labelled groups a material tile belongs to: the two flat
    /// materials are "Simple", everything else is "Deluxe".
    /// ⚠ The Deluxe group is deliberately WIDER than <see cref="ControllerWheel.Materials.IsPremium"/>,
    /// which counts only the four styled looks — here Pearl and Obsidian sit in the Deluxe box too.</summary>
    private static bool IsSimpleMaterial(string? token) =>
        token is not null && token.StartsWith("flat", StringComparison.Ordinal);
    private static readonly Brush TileSelectedPen = new SolidColorBrush(Color.FromRgb(0x1C, 0xA8, 0xC9));   // bright teal — unmistakably "chosen"
    private static readonly Brush TileRestPen     = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0));

    // Slice thickness options, thin→thick, with the mini-wheel preview's INNER radius (outer is fixed
    // at 20 in the 48×48 drawing — mirroring the real wheel, where thinner only moves the inner edge out).
    private static readonly (string Token, string Name, double Inner)[] Thicknesses =
    [
        ("thin",   "Thin",   17),
        ("medium", "Medium", 14),
        ("thick",  "Thick",   8),
    ];

    private void EnterLook()
    {
        // Invocation reminder — commented out along with the LookTryText block in OnboardingWindow.xaml;
        // uncomment both together to restore.
        //LookTryText.Text = $"👉 Preview instantly by bringing up a wheel using {ChordLabel(CurrentChord())}.";
        // Canonical by the time it gets here (ConfigLoader.Sanitize normalizes SliceMaterial on load),
        // which is what lets SelectMaterialTile match it against the tiles' canonical Tag values — a
        // legacy config saying "gloss-light"/"terra" would otherwise highlight NO tile at all.
        var mat = _hooks.GetSystem().SliceMaterial ?? ControllerWheel.Materials.GlossLight;
        if (MaterialTiles.Children.Count == 0 && MaterialTilesPremium.Children.Count == 0)
            for (int mi = 0; mi < Materials.Length; mi++)
            {
                var (token, name) = Materials[mi];
                // A rounded swatch filled with the real material brush; click to select + apply live.
                var label = new TextBlock
                {
                    // 18.2 = the Settings tiles' 14 × 1.3 — onboarding-only bump; the styled faces still
                    // scale off this via PreviewFaceSizeMulFor.
                    Text = Loc.T(name), FontSize = 18.2, FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = RadialMenuControl.PreviewIsDark(token)
                        ? new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2))
                        : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                    // Lifts the name off the textured materials; row 1 takes it at 75%, same as Settings.
                    Effect = RadialMenuControl.PreviewLabelShadow(token,
                        mi < MaterialColumns ? CustomizeEditorControl.MaterialTopRowShadowScale : 1.0),
                };
                // …in the material's own display face where it has one (Mesa/Salvage/Kawaii/Reactor), same
                // helper Settings ▸ Customize uses. After the initializer — it overrides FontWeight.
                RadialMenuControl.ApplyPreviewLabelFace(label, token);
                // The four styled materials carry their signature on the swatch (Mesa's terracotta card
                // edge, kawaii star, rust grain, circuit traces) — same factory the Settings tiles use, so
                // the two surfaces can't drift apart. EXCEPT Salvage: the shared renderer's tile crop
                // reads as an unreadable smear at swatch size, so it goes through
                // CustomizeEditorControl.SalvageTileDecoration instead — a tighter ~40% crop + an
                // armed-style lighten wash, kept in ONE place (that file) so this step and Settings ▸
                // Customize can't drift apart on the fix either.
                UIElement swatchContent = label;
                var deco = token == "salvage"
                    ? CustomizeEditorControl.SalvageTileDecoration(10)
                    : RadialMenuControl.PreviewDecoration(token, cornerRadius: 10);
                if (deco is { })
                {
                    var grid = new Grid();
                    grid.Children.Add(deco);
                    grid.Children.Add(label);
                    swatchContent = grid;
                }
                var swatch = new Border
                {
                    Height = 64, CornerRadius = new CornerRadius(10), Margin = new Thickness(4),
                    Background = RadialMenuControl.PreviewFill(token),
                    BorderBrush = TileRestPen, BorderThickness = new Thickness(1),
                    Tag = token, Cursor = System.Windows.Input.Cursors.Hand, Child = swatchContent,
                };
                swatch.MouseLeftButtonUp += MaterialTile_Click;
                (IsSimpleMaterial(token) ? MaterialTiles : MaterialTilesPremium).Children.Add(swatch);
            }
        SelectMaterialTile(mat, apply: false);

        if (ThicknessTiles.Children.Count == 0)
        {
            foreach (var (token, name, inner) in Thicknesses)
            {
                // Name to the RIGHT of the wheel preview, vertically centred against it; Medium carries a
                // smaller, non-bold "Recommended" line under its name.
                var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                textCol.Children.Add(new TextBlock
                {
                    Text = Loc.T(name), FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x48)),
                });
                if (token == "medium")
                    textCol.Children.Add(new TextBlock
                    {
                        Text = Loc.T(O.Recommended), FontSize = 10, FontWeight = FontWeights.Normal,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x48)),
                    });
                var content = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                };
                // Ring ink is set for real by RefreshThicknessMiniWheels below, once the live material and
                // selection are known — grey here is just a placeholder for the initial build.
                content.Children.Add(BuildMiniWheel(inner, CustomizeEditorControl.MiniWheelInk(null, false)));
                content.Children.Add(textCol);
                var swatch = new Border
                {
                    // 60 = the 48px mini-wheel plus a slim breathing margin.
                    Height = 60, CornerRadius = new CornerRadius(10), Margin = new Thickness(4),
                    Background = Brushes.White,
                    BorderBrush = TileRestPen, BorderThickness = new Thickness(1),
                    Tag = token, Cursor = System.Windows.Input.Cursors.Hand, Child = content,
                };
                swatch.MouseLeftButtonUp += ThicknessTile_Click;
                ThicknessTiles.Children.Add(swatch);
            }
            // Fourth tile: the sound on/off toggle — Settings' Sound Effects section collapsed into one
            // 2-state button. Selected (teal ring) = sound ON with the CURRENT material's paired theme;
            // deselected = off. Same tile chrome as its three neighbours.
            _soundTile = new Border
            {
                Height = 60, CornerRadius = new CornerRadius(10), Margin = new Thickness(10, 4, 4, 4),
                Background = Brushes.White,
                BorderBrush = TileRestPen, BorderThickness = new Thickness(1),
                Tag = SoundTileTag, Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = Loc.T(O.SoundTip),
            };
            _soundTile.MouseLeftButtonUp += SoundTile_Click;
            // Wrapped in its own Grid cell rather than added straight to the UniformGrid, so hiding the
            // Thick tile still re-flows the row cleanly (UniformGrid skips collapsed children).
            var soundCell = new Grid();
            soundCell.Children.Add(_soundTile);
            ThicknessTiles.Children.Add(soundCell);
        }
        SelectThicknessTile(_hooks.GetSystem().SliceThickness ?? "medium", apply: false);   // also retints the mini-wheels
        RefreshSoundTile();
        // Hide Thick past the slice limit, same as Settings — an option that would be reverted on the next
        // wheel write isn't an option. Re-checked on every step entry: a Back from Customize can return
        // here with different slice counts (the starter layout can exceed the limit on its own).
        var (wa, wb) = _hooks.GetWheels();
        bool thickOk = SliceThicknessRule.ThickAllowed(wa.Length, wb.Length);
        foreach (var tile in ThicknessTiles.Children.OfType<Border>())
            if ((string?)tile.Tag == "thick")
                tile.Visibility = thickOk ? Visibility.Visible : Visibility.Collapsed;

        // The accessibility panel (if the user revealed it) re-reads the live config on every entry —
        // a Back from Customize could return here after another surface changed these values.
        if (AccessibilityPanel.Visibility == Visibility.Visible) LoadAccessibilityRow();
    }

    // ── Material-step accessibility panel (footer "Accessibility..." button) ───────────────────────
    // The SAME options Settings ▸ Customize ▸ Accessibility carries (read its Load/ApplyTo for the keys),
    // shown as one row of checkboxes INSIDE the free-tip card (they replace its text while up). Edits
    // write straight to SystemConfig via the wizard's WriteSystem hook, so the two surfaces can't diverge
    // on where the values live.

    private CheckBox? _axToggle, _axSwap, _axHub, _axMotion, _axIgnoreOppositeStick, _axNarration;
    private bool _axLoading;   // guard Checked events while (re)loading programmatically
    // The two soft links, mirroring SystemEditorControl's exactly: Toggle auto-ticks Swap (Toggle opens
    // the SAME hand, so re-crossing keeps the sides stable) and Reduce motion auto-ticks Always show hub
    // (a calm wheel wants the hub standing still) — unless the user changed the child independently, in
    // which case their choice wins and that link stops. This row is flat, so the indented trees Settings
    // draws have no counterpart here; the behaviour must still match box-for-box.
    // ⚠ "Wheel ignores opposite stick" is on NEITHER link and must not be added to one (see SystemConfig).
    private bool _axSwapUserSet, _axSwapAutoSet;
    private bool _axHubUserSet, _axHubAutoSet;
    private bool _axSyncing;   // a programmatic (auto-link) checkbox change is in progress

    private void BtnAccessibility_Click(object sender, RoutedEventArgs e)
    {
        if (AccessibilityPanel.Visibility == Visibility.Visible)
        {
            AccessibilityPanel.Visibility = Visibility.Collapsed;   // the button toggles the panel
            UpdateLookTipCard();
            return;
        }
        BuildAccessibilityRow();
        LoadAccessibilityRow();
        AccessibilityPanel.Visibility = Visibility.Visible;
        UpdateLookTipCard();
    }

    /// <summary>The accessibility panel and the free-tip text SHARE the one tip card: while the panel is
    /// up it replaces the text; otherwise the card follows the Deluxe-material rule.</summary>
    private void UpdateLookTipCard()
    {
        bool ax = AccessibilityPanel.Visibility == Visibility.Visible;
        PremiumHintText.Visibility = ax ? Visibility.Collapsed : Visibility.Visible;
        PremiumHintCard.Visibility = ax || !IsSimpleMaterial(_hooks.GetSystem().SliceMaterial)
            ? Visibility.Visible : Visibility.Collapsed;
        PremiumHintCard.Cursor = ax ? null : System.Windows.Input.Cursors.Hand;
        PremiumHintCard.ToolTip = ax ? null : Loc.T(O.OpenTipJar);
    }

    /// <summary>Clicking anywhere in the tip card opens the Tip Jar — but only while the tip text is the
    /// card's content. The inner Hyperlink marks its own click Handled, so this never double-fires.</summary>
    private void TipCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled || PremiumHintText.Visibility != Visibility.Visible) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Loc.TipUrl) { UseShellExecute = true }); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Onboarding] tip jar open failed: {ex.Message}"); }
        e.Handled = true;
    }

    /// <summary>The accessibility card's (?) — opens Help's "accessibility" topic in a pop-up. The wizard
    /// has no Help tab to send the user to, and rewriting the feature descriptions here would guarantee the
    /// card and Help drift apart the next time a feature lands.</summary>
    private void AccessibilityHelp_Click(object sender, RoutedEventArgs e) =>
        HelpTopicWindow.Show(this, "accessibility", languageCode: Loc.Lang);

    private CheckBox MakeAccessibilityCheck(string label, string tooltip, Action? onChanged = null)
    {
        var changed = onChanged ?? ApplyAccessibility;
        var cb = new CheckBox
        {
            Content = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.Bold },
            Margin = new Thickness(8, 0, 8, 4), VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tooltip,
        };
        cb.Checked   += (_, _) => changed();
        cb.Unchecked += (_, _) => changed();
        return cb;
    }

    /// <summary>Build the row once. Labels + tooltips mirror SystemEditorControl's Accessibility
    /// section box-for-box: ToggleActivationBox / SwapFnBox / IgnoreOppositeStickBox / AlwaysShowHubBox /
    /// ReduceMotionBox / NarrationBox.
    /// <para>⚠ <b>A new accessibility feature belongs in BOTH places:</b>
    /// adding a checkbox to <c>SystemEditorControl.xaml</c> means adding it here, to
    /// <see cref="LoadAccessibilityRow"/> and <see cref="ApplyAccessibility"/>, and as a bullet in
    /// <c>HelpContent</c>'s "accessibility" topic (which the card's (?) pop-up renders).</para></summary>
    private void BuildAccessibilityRow()
    {
        if (_axToggle is not null) return;
        _axIgnoreOppositeStick = MakeAccessibilityCheck(Loc.T(O.AxIgnoreOpposite),
            Loc.T(O.AxIgnoreOppositeTip));
        _axToggle = MakeAccessibilityCheck(Loc.T(O.AxToggle),
            Loc.T(O.AxToggleTip),
            AxToggleChanged);
        _axSwap = MakeAccessibilityCheck(Loc.T(O.AxSwap),
            Loc.T(O.AxSwapTip),
            () => AxLinkedManualChange(ref _axSwapUserSet, ref _axSwapAutoSet));
        _axMotion = MakeAccessibilityCheck(Loc.T(O.AxMotion),
            Loc.T(O.AxMotionTip),
            AxMotionChanged);
        _axHub = MakeAccessibilityCheck(Loc.T(O.AxHub),
            Loc.T(O.AxHubTip),
            () => AxLinkedManualChange(ref _axHubUserSet, ref _axHubAutoSet));
        _axNarration = MakeAccessibilityCheck(Loc.T(O.AxNarration),
            Loc.T(O.AxNarrationTip));
        // Row order mirrors Settings ▸ Advanced top-to-bottom: the unparented opt-out, then each
        // parent immediately followed by the child it auto-ticks.
        foreach (var cb in new[] { _axIgnoreOppositeStick, _axToggle, _axSwap })
            AccessibilityRow.Children.Add(cb);
        foreach (var cb in new[] { _axMotion, _axHub, _axNarration })
            AccessibilityRow2.Children.Add(cb);
    }

    /// <summary>"Wheels toggle on/off" changed: run the Toggle ⇄ Swap soft link (the same contract as
    /// SystemEditorControl.ToggleActivation_Changed — auto-ticks are undone when the parent comes back
    /// off, but only the ticks WE added), then commit the row.</summary>
    private void AxToggleChanged()
    {
        if (_axLoading) return;
        AxLink(_axSwap!, _axToggle!.IsChecked == true, ref _axSwapUserSet, ref _axSwapAutoSet);
        ApplyAccessibility();
    }

    /// <summary>"Reduce motion" changed: the same contract, for the Always-show-hub child.</summary>
    private void AxMotionChanged()
    {
        if (_axLoading) return;
        AxLink(_axHub!, _axMotion!.IsChecked == true, ref _axHubUserSet, ref _axHubAutoSet);
        ApplyAccessibility();
    }

    private void AxLink(CheckBox box, bool parentOn, ref bool userSet, ref bool autoSet)
    {
        if (userSet) return;   // the user took control of this box — respect it
        if (parentOn && box.IsChecked != true)
        {
            _axSyncing = true; box.IsChecked = true; _axSyncing = false;
            autoSet = true;
        }
        else if (!parentOn && autoSet)   // only undo the tick WE added
        {
            _axSyncing = true; box.IsChecked = false; _axSyncing = false;
            autoSet = false;
        }
    }

    /// <summary>A linked child (Swap / Always show hub) changed by hand: stop auto-linking it, then commit.</summary>
    private void AxLinkedManualChange(ref bool userSet, ref bool autoSet)
    {
        if (_axLoading || _axSyncing) return;   // ignore programmatic (auto-link/load) changes
        userSet = true;
        autoSet = false;
        ApplyAccessibility();
    }

    /// <summary>Re-read the live config into the row (guarded so the sets don't fire ApplyAccessibility).</summary>
    private void LoadAccessibilityRow()
    {
        if (_axToggle is null) return;
        _axLoading = true;
        // Fresh (re)load: both auto-links may only undo ticks they add from here on — the same reset
        // Settings' Load performs on its LinkState pair.
        _axSwapUserSet = false; _axSwapAutoSet = false;
        _axHubUserSet  = false; _axHubAutoSet  = false;
        try
        {
            var sys = _hooks.GetSystem();
            _axToggle.IsChecked     = sys.TriggerActivation == "toggle";
            _axSwap!.IsChecked      = sys.SwapFnButtons;
            _axHub!.IsChecked       = sys.AlwaysShowHub;
            _axMotion!.IsChecked    = sys.ReduceMotion;
            _axIgnoreOppositeStick!.IsChecked = sys.WheelIgnoresOppositeStick;
            _axNarration!.IsChecked  = sys.Narration;
        }
        finally { _axLoading = false; }
    }

    /// <summary>Commit the row to config — the exact fields Customize's ApplyTo writes for these boxes.</summary>
    private void ApplyAccessibility()
    {
        if (_axLoading || _axSyncing || _axToggle is null) return;   // mid-link writes wait for the final commit
        var sys = _hooks.GetSystem();
        if (!_hooks.WriteSystem(sys with
        {
            TriggerActivation   = _axToggle.IsChecked == true ? "toggle" : "hold",
            SwapFnButtons       = _axSwap!.IsChecked == true,
            AlwaysShowHub       = _axHub!.IsChecked == true,
            ReduceMotion        = _axMotion!.IsChecked == true,
            WheelIgnoresOppositeStick = _axIgnoreOppositeStick!.IsChecked == true,
            Narration           = _axNarration!.IsChecked == true,
        })) LoadAccessibilityRow();
    }

    /// <summary>A simplified generic wheel for a thickness tile: a 6-slice ring whose OUTER edge is fixed
    /// and whose inner edge sits at <paramref name="inner"/> — same rule as the real wheel. Slice gaps are
    /// drawn as tile-background spokes over the ring.</summary>
    private static UIElement BuildMiniWheel(double inner, Color ring0)
    {
        const double c = 24, outer = 20;
        var canvas = new Canvas { Width = 48, Height = 48 };
        var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
        ring.Children.Add(new EllipseGeometry(new Point(c, c), outer, outer));
        ring.Children.Add(new EllipseGeometry(new Point(c, c), inner, inner));
        ring.Freeze();
        canvas.Children.Add(new System.Windows.Shapes.Path
        { Data = ring, Fill = new SolidColorBrush(ring0) });
        for (int i = 0; i < 6; i++)   // slice separators, drawn in the tile background colour
        {
            double ang = (i * 60 - 90) * Math.PI / 180;
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = c + (inner - 1.5) * Math.Cos(ang), Y1 = c + (inner - 1.5) * Math.Sin(ang),
                X2 = c + (outer + 1.0) * Math.Cos(ang), Y2 = c + (outer + 1.0) * Math.Sin(ang),
                Stroke = Brushes.White, StrokeThickness = 2.5,
            });
        }
        return canvas;
    }

    private void ThicknessTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Border { Tag: string token } && token != SoundTileTag) SelectThicknessTile(token, apply: true);
    }

    // ── Sound on/off tile (rides in the thickness row) ─────────────────────────
    // Settings' Sound Effects section collapsed into one 2-state toggle: ON writes SoundEffects=true and
    // SoundTheme="material" (follow the look — the same write the material tiles make); OFF writes
    // SoundEffects=false and leaves SoundTheme alone, exactly like Settings' "Silent" tile preserving
    // the last theme.
    private const string SoundTileTag = "sound-toggle";
    private Border? _soundTile;

    private void SoundTile_Click(object sender, RoutedEventArgs e)
    {
        var sys = _hooks.GetSystem();
        bool on = !sys.SoundEffects;
        if (!_hooks.WriteSystem(on ? sys with { SoundEffects = true, SoundTheme = "material" }
                              : sys with { SoundEffects = false })) return;
        RefreshSoundTile();
        if (on) Sfx.PreviewFire(Sfx.ResolveSet("material",   // hear the pick, same as Settings' sound tiles
            sys.SliceMaterial ?? ControllerWheel.Materials.GlossLight));
    }

    /// <summary>Re-read SoundEffects into the tile: teal selected ring + volume-high glyph when on,
    /// rest ring + volume-off when muted. Called on step entry and after every click.</summary>
    private void RefreshSoundTile()
    {
        if (_soundTile is null) return;
        bool on = _hooks.GetSystem().SoundEffects;
        _soundTile.BorderBrush     = on ? TileSelectedPen : TileRestPen;
        _soundTile.BorderThickness = new Thickness(on ? 3 : 1);
        var content = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        if (PackIconHelper.FromName(on ? "VolumeHigh" : "VolumeOff",
                new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x48))) is { } glyph)
            content.Children.Add(new System.Windows.Controls.Image
            {
                Source = glyph, Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Center,
            });
        content.Children.Add(new TextBlock
        {
            Text = Loc.T(on ? O.SoundOn : O.SoundOff), FontSize = 12, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x48)),
        });
        _soundTile.Child = content;
    }

    /// <summary>Recolour the thickness tiles' mini-wheels: the SELECTED tile takes an accent tied to the
    /// current material, the rest stay grey. Shares Settings ▸ Customize's mapping via
    /// <see cref="CustomizeEditorControl.MiniWheelInk"/>, so picking a material here retints the
    /// illustrations exactly as it does there. Called on step entry and after every thickness or material
    /// pick.</summary>
    private void RefreshThicknessMiniWheels()
    {
        var sys = _hooks.GetSystem();
        string selected = sys.SliceThickness ?? "medium";
        foreach (var b in ThicknessTiles.Children.OfType<Border>())
        {
            if ((string?)b.Tag is not { } tag || tag == SoundTileTag) continue;
            int idx = Array.FindIndex(Thicknesses, t => t.Token == tag);
            if (idx < 0 || b.Child is not StackPanel sp || sp.Children.Count == 0) continue;
            // UIElementCollection's indexer only assigns into an EMPTY slot — replacing throws.
            sp.Children.RemoveAt(0);
            sp.Children.Insert(0, BuildMiniWheel(Thicknesses[idx].Inner,
                CustomizeEditorControl.MiniWheelInk(sys.SliceMaterial, tag == selected)));
        }
    }

    /// <summary>Highlight the chosen thickness tile and (when <paramref name="apply"/>) write it live.
    /// A tile click is a MANUAL thickness pick, so it clears ThickAutoDemoted — same contract as the
    /// Settings picker: the user has expressed a new preference, so the auto Thick-restore stops. The
    /// write also runs through SliceThicknessRule against the live wheels (the starter layout can land
    /// more than 8 slices, and picking Thick over that would just be reverted on the next wheel write —
    /// better it never sticks in the first place; the tile visuals re-sync to what was actually kept).</summary>
    private void SelectThicknessTile(string token, bool apply)
    {
        if (apply)
        {
            var sys = _hooks.GetSystem();
            if (sys.SliceThickness != token || sys.ThickAutoDemoted)
            {
                var (a, b) = _hooks.GetWheels();
                var applied = SliceThicknessRule.Apply(
                    sys with { SliceThickness = token, ThickAutoDemoted = false }, a.Length, b.Length);
                if (!_hooks.WriteSystem(applied)) return;
                token = applied.SliceThickness;   // highlight what was KEPT, not what was asked
            }
        }
        foreach (var b in ThicknessTiles.Children.OfType<Border>())
        {
            if ((string?)b.Tag == SoundTileTag) continue;   // the sound toggle's ring shows its OWN state
            bool sel = (string?)b.Tag == token;
            b.BorderBrush     = sel ? TileSelectedPen : TileRestPen;
            b.BorderThickness = new Thickness(sel ? 3 : 1);
        }
        RefreshThicknessMiniWheels();
    }

    private void MaterialTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Border { Tag: string token }) SelectMaterialTile(token, apply: true);
    }

    /// <summary>Highlight the chosen swatch and (when <paramref name="apply"/>) write it live. During
    /// onboarding the pick also sets the GAME GRID's material to match AND the SOUND THEME to "material"
    /// (follow the look — same write as Settings ▸ Customize's material tiles). Digital/Physical stay
    /// pickable afterwards in Settings ▸ Customize, until the next material change snaps back.</summary>
    private void SelectMaterialTile(string token, bool apply)
    {
        if (apply)
        {
            var sys = _hooks.GetSystem();
            // "material" follows the selected look at play time.
            if (sys.SliceMaterial != token || sys.GameGridMaterial != token || sys.SoundTheme != "material"
                || sys.HeldSliceMaterial is not null || sys.HeldGameGridMaterial is not null)
                if (!_hooks.WriteSystem(sys with
                {
                    SliceMaterial = token, GameGridMaterial = token, SoundTheme = "material",
                    HeldSliceMaterial = null, HeldGameGridMaterial = null,
                })) return;
        }
        foreach (var b in MaterialTiles.Children.OfType<Border>().Concat(MaterialTilesPremium.Children.OfType<Border>()))
        {
            bool sel = (string?)b.Tag == token;
            b.BorderBrush     = sel ? TileSelectedPen : TileRestPen;
            b.BorderThickness = new Thickness(sel ? 3 : 1);
            RadialMenuControl.ApplyPreviewLabelLift(b, sel);   // selected material label rides 2px north
        }
        // The donation nudge belongs to the Premium group only — it's the thank-you for picking one.
        // (Unless the accessibility panel currently occupies the card — UpdateLookTipCard arbitrates.)
        bool axUp = AccessibilityPanel.Visibility == Visibility.Visible;
        PremiumHintCard.Visibility = axUp || !IsSimpleMaterial(token) ? Visibility.Visible : Visibility.Collapsed;
        RefreshThicknessMiniWheels();   // the selected thickness tile's wheel takes the new material's accent
    }

    // ── Progress dots ─────────────────────────────────────────────────────────

    private void BuildDots()
    {
        for (int i = 0; i < _steps.Length; i++)
            StepDots.Items.Add(new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 7, 0) });
    }

    private void UpdateDots()
    {
        for (int i = 0; i < StepDots.Items.Count; i++)
            if (StepDots.Items[i] is Ellipse dot)
                dot.Fill = new SolidColorBrush(i == _index
                    ? Color.FromRgb(0x3D, 0x40, 0x5B)          // current — brand navy
                    : i < _index
                        ? Color.FromRgb(0x81, 0xB2, 0x9A)      // done — brand sage
                        : Color.FromArgb(0x30, 0x00, 0x00, 0x00));
    }

    // ── Language (Welcome step header) ────────────────────────────────────────

    private bool _loadingLanguage;

    /// <summary>The Welcome step's language picker: native names, with the language's own flag glyph
    /// beside the box (drawn, not emoji — see HelpEditorControl.BuildFlagGlyph). A pick writes
    /// system.language and relaunches the app; the wizard resumes on this step (SystemConfig.OobeStep) in
    /// the new language. That relaunch is the only language change that is not restart-PROMPTED: the
    /// language is fixed for a run (Loc.Init), and a first-run user has nothing open to lose.
    /// <para>Release builds offer English plus the languages whose UI catalog is complete (Loc.Offered), so
    /// a half-translated UI is never offered; a Debug build offers every language so the relaunch path can
    /// be exercised before the translations exist. The pseudo-locales are never listed in either build —
    /// they are a layout gate, reached with <c>--lang qps</c> / <c>--lang qps-rtl</c>, not a language anyone
    /// picks.</para></summary>
    private void InitLanguagePicker()
    {
        _loadingLanguage = true;
        LanguageBox.Items.Clear();
        foreach (var lang in HelpLocalization.Languages)
        {
            if (lang.Code.StartsWith("qps", StringComparison.Ordinal)) continue;
#if !DEBUG
            if (!Loc.Offered(lang.Code)) continue;
#endif
            LanguageBox.Items.Add(new ComboBoxItem
            {
                Content = lang.NativeName, Tag = lang.Code, FontFamily = new FontFamily(lang.FontFamily),
            });
        }
        foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
            if ((string?)item.Tag == Loc.Lang) LanguageBox.SelectedItem = item;
        LanguageFlagHost.Child   = HelpEditorControl.BuildFlagGlyph(Loc.Lang);
        LanguageFlagHost.ToolTip = HelpEditorControl.FlagToolTip(Loc.Lang);
        LanguageNote.Visibility = Loc.Lang == HelpLocalization.DefaultCode ? Visibility.Collapsed : Visibility.Visible;
        _loadingLanguage = false;
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingLanguage) return;
        var picked = HelpLocalization.Normalize((LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string);
        if (picked == Loc.Lang) return;
        LanguageFlagHost.Child   = HelpEditorControl.BuildFlagGlyph(picked);
        LanguageFlagHost.ToolTip = HelpEditorControl.FlagToolTip(picked);
        // WriteSystem writes the file before returning (ConfigLoader.WriteConfig is atomic and synchronous),
        // so the relaunched process reads the pick; OobeStep already names this step.
        if (!_hooks.WriteSystem(_hooks.GetSystem() with { Language = picked }))
        {
            InitLanguagePicker();
            return;
        }
        _hooks.RequestRestart();
    }
}
