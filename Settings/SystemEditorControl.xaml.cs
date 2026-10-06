using System.Linq;
using System.Windows;
using System.Windows.Controls;   // bare Button/TextBox/ItemsControl resolve to WPF here
using System.Windows.Media;
using NAudio.CoreAudioApi;

namespace ControllerWheel;

/// <summary>The Settings "Advanced" tab. Left column: Current Controller + Integrations (cover-art /
/// SteamGridDB key + Discord credentials + the shared OBS WebSocket connection) + Game Grid
/// (clear/retry cover art, reset hidden games — which un-hides storefronts too — and
/// Prefer-Playnite-covers). Right column: Accessibility (this
/// tab owns TriggerActivation / SwapFnButtons / WheelIgnoresOppositeStick / AlwaysShowHub / ReduceMotion /
/// Narration — Customize writes none of them) and
/// System — Backup/Restore, Start-with-Windows, a hidden-but-wired Practice-mode checkbox (see
/// <see cref="PracticeSettingsBox"/>'s Visibility), a Troubleshooting dropdown (first-run setup /
/// install-repair drivers / HID Diagnostics / Controller Setup / email log / a divider / reset-all / wipe /
/// uninstall), Updates (auto-check toggle, manual check with inline result, the persistent
/// version-available line, and the crash-report-prompt toggle — mechanics in UpdateService/CrashReporter),
/// and Quit Radiata.
/// <para>There is no Storefronts list: hiding a storefront is a Game Grid gesture (hold Square on its card),
/// and the only DisabledStorefronts write left here is the reset — see ResetHiddenGamesBtn_Click.</para>
/// Edits its own slice of <see cref="SystemConfig"/> via <see cref="ApplyTo"/>; visual pickers + triggers
/// live in the Customize tab (<see cref="CustomizeEditorControl"/>).</summary>
public partial class SystemEditorControl : UserControl
{
    public event EventHandler? Changed;
    /// <summary>The user picked a language (code). Raised after <see cref="Changed"/>; the host re-renders Help
    /// live and offers the restart that applies it everywhere else (the language is fixed per run).</summary>
    public event EventHandler<string>? LanguageChanged;
    public event EventHandler? BackupRequested;
    public event EventHandler? RestoreRequested;
    public event EventHandler? ResetRequested;        // "Reset All Settings…"
    /// <summary>Help-chip jumps (topic id) — the host (SettingsWindow) opens the Help tab on that topic.</summary>
    public event Action<string>? HelpRequested;
    public event EventHandler? WipeRequested;
    public event EventHandler? ClearArtCacheRequested;
    public event EventHandler? RetryMissedArtRequested;
    public event EventHandler? RunOobeRequested;
    public event EventHandler? EmailLogRequested;
    public event EventHandler? UninstallRequested;
    // Controller/driver actions — App supplies the actions.
    public event EventHandler? InstallDriversRequested;
    public event EventHandler? RecoverControllerRequested;
    /// <summary>The host (SettingsWindow) forwards this to the App.OpenDiagnostics hook.</summary>
    public event EventHandler? HidDiagnosticsRequested;
    /// <summary>The host forwards this to the controller button-mapping wizard hook.</summary>
    public event EventHandler? ControllerSetupRequested;
    /// <summary>The host must forward this to the same clean-exit path the tray "Exit" item uses
    /// (App.ExitApp: TearDown then Shutdown), never a bare Application.Shutdown.</summary>
    public event EventHandler? QuitRequested;

    private bool _loading;
    private bool _resettingTroubleshootingBox;   // guards TroubleshootingBox's self-reset from re-firing
    private SystemConfig _loadedCfg = new();   // last-loaded snapshot — the reset button's storefront count

    // ── Current Controller card: glyph brushes per connection/kind state ────────
    private static readonly Brush ControllerDisconnectedBrush = FreezeBrush(0x9A, 0x9A, 0x9A);
    private static readonly Brush ControllerXboxBrush         = FreezeBrush(0x43, 0xA0, 0x47);
    private static readonly Brush ControllerDualSenseEdgeBrush = FreezeBrush(0x2F, 0x72, 0xD8);
    private static readonly Brush ControllerOtherBrush        = FreezeBrush(0xD2, 0x54, 0x2E);
    private static readonly Brush RefreshIconBrush            = FreezeBrush(0x44, 0x44, 0x44);
    // Transport glyph beside the pad name: the same quiet grey as the battery glyph, so it reads as an
    // annotation on the name rather than competing with the kind-coloured controller icon.
    private static readonly Brush TransportIconBrush          = FreezeBrush(0x55, 0x55, 0x55);
    // Battery glyph: quiet grey normally, the same red the tab's error text uses once it's nearly flat.
    private static readonly Brush BatteryInkBrush             = FreezeBrush(0x55, 0x55, 0x55);
    private static readonly Brush BatteryLowInkBrush          = FreezeBrush(0xC4, 0x2B, 0x1C);

    private static Brush FreezeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public SystemEditorControl()
    {
        InitializeComponent();
        ControllerGlyph.Source = PackIconHelper.FromName("Controller", ControllerDisconnectedBrush);
        RecoverControllerIcon.Source = PackIconHelper.FromName("Refresh", RefreshIconBrush);
        // The app's own flower mark (FlowerIcon), via its PackIconHelper name.
        QuitRadiataIcon.Source = PackIconHelper.FromName(PackIconHelper.RadiataMarkName, Brushes.Black);
        // MDI wheelchair-accessibility beside the Accessibility heading — the same glyph the onboarding
        // footer's "Accessibility…" button carries, in the heading ink.
        AccessibilityHeadingGlyph.Source = PackIconHelper.FromName(
            "WheelchairAccessibility", (Brush)System.Windows.Application.Current.Resources["HeadingInk"]);
        InitLanguagePicker();
        RefreshDiscordButton();
        RefreshObsButton();
    }

    /// <summary>Owned SystemConfig.Language: null = follow Windows, never rewritten to a code until the user picks.</summary>
    private string? _language;
    public string? SelectedLanguage => _language;

    /// <summary>Release builds offer English plus every language whose UI catalog is complete (Loc.Offered), so a
    /// half-translated UI is never offered; Debug offers all so the restart path can be exercised first.
    /// The pseudo-locales are never listed in either build — they are a layout gate, reached with
    /// <c>--lang qps</c> / <c>--lang qps-rtl</c>, not a language anyone picks.</summary>
    private void InitLanguagePicker()
    {
        _loading = true;
        foreach (var lang in HelpLocalization.Languages)
        {
            if (lang.Code.StartsWith("qps", StringComparison.Ordinal)) continue;
#if !DEBUG
            if (!Loc.Offered(lang.Code)) continue;
#endif
            LanguageBox.Items.Add(new ComboBoxItem { Content = lang.NativeName, Tag = lang.Code, FontFamily = new FontFamily(lang.FontFamily) });
        }
        LanguageBox.ToolTip = Loc.T(UiText.Settings.LanguageTip);
        System.Windows.Automation.AutomationProperties.SetName(LanguageBox, Loc.T(UiText.Settings.LanguageCaption));
        ShowLanguage(Loc.Lang);
        _loading = false;
    }

    private void ShowLanguage(string code)
    {
        foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
            if ((string?)item.Tag == code) LanguageBox.SelectedItem = item;
        LanguageFlagHost.Child   = HelpEditorControl.BuildFlagGlyph(code);
        LanguageFlagHost.ToolTip = HelpEditorControl.FlagToolTip(code);
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var picked = HelpLocalization.Normalize((LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string);
        if (picked == (_language ?? Loc.Lang)) return;
        _language = picked;
        ShowLanguage(picked);
        Changed?.Invoke(this, EventArgs.Empty);
        LanguageChanged?.Invoke(this, picked);
    }

    /// <summary>Open the same Discord credentials wizard the slice editor offers (it persists via
    /// DiscordOAuth itself, so no config plumbing is needed here).</summary>
    private void ConfigureDiscordBtn_Click(object sender, RoutedEventArgs e)
    {
        new DiscordSetupWindow { Owner = Window.GetWindow(this) }.ShowDialog();
        RefreshDiscordButton();
    }

    // ── OBS connection: this tab is the ONLY writer of ObsPort/ObsPassword/ObsConfigured ──────────
    // An OBS slice's editor offers the same window (ObsSetupRequested → SettingsWindow → OpenObsSetup),
    // so there is one owner of the setting and one place it folds into config (ApplyTo below).
    private int     _obsPort = 4455;
    private string? _obsStoredPassword;   // DPAPI blob, never shown
    private bool    _obsConfigured;
    private bool    _obsDirty;            // only a dirty copy folds in, so a save can't clobber a live change

    /// <summary>Raised after the OBS setup window saves — the host re-pushes the configured state to the
    /// slice editors so their "one-time setup" prompt clears without a reopen.</summary>
    public event EventHandler? ObsConfigChanged;

    /// <summary>True once the connection has been saved (or a legacy config carried a password before the
    /// ObsConfigured flag existed) — the same reading App uses for the configure-on-fire gate.</summary>
    public bool ObsConfigured => _obsConfigured || !string.IsNullOrEmpty(_obsStoredPassword);

    private void ConfigureObsBtn_Click(object sender, RoutedEventArgs e) => OpenObsSetup();

    /// <summary>Run the OBS connection pane and take its values. Public because a slice editor routes its
    /// own "Configure OBS Integration…" button here.</summary>
    public void OpenObsSetup()
    {
        var dlg = new ObsSetupWindow(_obsPort, _obsStoredPassword) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        _obsPort = dlg.ResultPort;
        _obsStoredPassword = dlg.ResultStoredPassword;
        _obsConfigured = true;
        _obsDirty = true;
        RefreshObsButton();
        Changed?.Invoke(this, EventArgs.Empty);
        ObsConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Connection already saved → circled tick + "Edit OBS"; otherwise the "Configure" prompt.</summary>
    private void RefreshObsButton()
    {
        bool configured = ObsConfigured;
        ObsConfiguredCheck.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        ConfigureObsText.Text = Loc.T(configured ? UiText.Advanced.EditObs : UiText.Advanced.ConfigureObs);
        System.Windows.Automation.AutomationProperties.SetName(ConfigureObsBtn, ConfigureObsText.Text);
    }

    /// <summary>Credentials already saved → circled tick + "Edit"; otherwise the plain "Configure" prompt.</summary>
    private void RefreshDiscordButton()
    {
        bool configured = DiscordOAuth.IsConfigured;
        DiscordConfiguredCheck.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        ConfigureDiscordText.Text = Loc.T(configured ? UiText.Advanced.EditDiscord : UiText.Advanced.ConfigureDiscord);
        System.Windows.Automation.AutomationProperties.SetName(ConfigureDiscordBtn, ConfigureDiscordText.Text);
    }

    // Help chips — the per-section tips live in these Help topics, not in inline captions.
    private void HelpIntegrations_Click(object sender, RoutedEventArgs e) => HelpRequested?.Invoke("integrations");
    private void HelpPlaynite_Click(object sender, RoutedEventArgs e)     => HelpRequested?.Invoke("playnite");
    private void HelpObs_Click(object sender, RoutedEventArgs e)          => HelpRequested?.Invoke("obs-studio");

    /// <summary>Open the Playnite download page (shown only when Playnite isn't installed).</summary>
    private void InstallPlayniteBtn_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://playnite.link") { UseShellExecute = true }); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Settings] Playnite link open failed: {ex.Message}"); }
    }
    /// <summary>Open a hyperlink's target in the default browser (the SteamGridDB API Key heading).</summary>
    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Settings] link open failed: {ex.Message}"); }
        e.Handled = true;
    }
    private void HelpSystem_Click(object sender, RoutedEventArgs e)      => HelpRequested?.Invoke("system-actions");
    /// <summary>The Accessibility section's Help chip.</summary>
    private void HelpAccessibility_Click(object sender, RoutedEventArgs e) => HelpRequested?.Invoke("accessibility");
    private void HelpGameGrid_Click(object sender, RoutedEventArgs e)    => HelpRequested?.Invoke("game-grid-options");
    // ⚠ This tab has no Arcade section and nothing here gates Arcade. Availability is Arcade.Available (the
    // build switch), which the slice editors and Help consult; Arcade Help keeps its own
    // topic in Settings ▸ Help.

    public void Load(SystemConfig cfg)
    {
        _loading = true;
        _loadedCfg = cfg;
        _language = cfg.Language;
        ShowLanguage(_language ?? Loc.Lang);
        SgdbKeyBox.Text = SecretField.ToPlaintext(cfg.SteamGridDbKey) ?? "";   // stored as a DPAPI blob
        // Key-test state: the loaded key counts as already accepted — no test until it CHANGES.
        _lastTestedSgdbKey = SgdbKeyBox.Text.Trim();
        _hadSgdbKeyAtLoad  = _lastTestedSgdbKey.Length > 0;
        bool keyUnavailable = cfg.SteamGridDbKey is { Length: > 0 } storedKey
            && LocalSecret.IsProtected(storedKey) && LocalSecret.Unprotect(storedKey) is null;
        SgdbKeyError.Text = keyUnavailable ? Loc.T(UiText.Settings.KeyUnavailable) : "";
        SgdbKeyError.Visibility = keyUnavailable ? Visibility.Visible : Visibility.Collapsed;
        _lastTestedSgdbKeyOk = _hadSgdbKeyAtLoad;
        RefreshSgdbKeyCheck();
        PreferPlayniteBox.IsChecked = cfg.PreferPlayniteCovers;
        UpdatePreferPlayniteVisibility();

        // OBS connection: seeded, never edited inline — the button opens ObsSetupWindow.
        _obsPort = cfg.ObsPort;
        _obsStoredPassword = cfg.ObsPassword;
        _obsConfigured = cfg.ObsConfigured;
        _obsDirty = false;
        RefreshObsButton();

        // Registry-backed, not config — seeded here but deliberately excluded from ApplyTo/Changed.
        StartWithWindowsBox.IsChecked = StartupManager.IsEnabled;

        // Practice mode's checkbox is hidden in XAML, but the seed + the ApplyTo write stay live on purpose.
        // AlwaysShowHub/ReduceMotion belong to Customize ▸ Accessibility — don't seed them here.
        PracticeSettingsBox.IsChecked = cfg.PracticeWhileSettingsOpen;
        BluetoothDropBox.IsChecked = cfg.BluetoothDropAlert;

        // ── Accessibility ────────────────────────────────────────────────────────────────────────────
        // Fresh session: both soft links may only undo ticks they add from HERE on.
        _swapLink = new(); _hubLink = new();
        IgnoreOppositeStickBox.IsChecked = cfg.WheelIgnoresOppositeStick;   // unchecked = either stick aims
        ToggleActivationBox.IsChecked = cfg.TriggerActivation == "toggle";   // unchecked = Hold (default)
        SwapFnBox.IsChecked        = cfg.SwapFnButtons;
        AlwaysShowHubBox.IsChecked = cfg.AlwaysShowHub;
        ReduceMotionBox.IsChecked  = cfg.ReduceMotion;
        NarrationBox.IsChecked     = cfg.Narration;
        NarratorTipText.Text       = NarratorTipCopy;
        _narratorTipDismissed      = cfg.NarratorTipDismissed;
        UpdateNarratorTip();

        // (D-Pad + "Show labels on" live on Customize — that tab owns both fields.)

        // ── Updates + crash reporting ────────────────────────────────────────────────────────────
        UpdateCheckBox.IsChecked = cfg.UpdateCheckEnabled;
        CrashPromptBox.IsChecked = cfg.CrashPromptEnabled;
        CrashAutoSendBox.IsChecked = cfg.CrashAutoSend;
        RefreshUpdateAvailability();

        _loading = false;
    }

    // ── Updates section ─────────────────────────────────────────────────────────
    private bool _updateCheckRunning;

    /// <summary>While the checker knows of a newer version (found automatically or by the button,
    /// skipped or not — Settings stays truthful; the skip only silences the balloon), the check button
    /// becomes "Install Update" and the grey line under it names the full version.</summary>
    private void RefreshUpdateAvailability()
    {
        if (UpdateService.Available is { } feed)
        {
            CheckUpdatesBtn.Content = Loc.T(UiText.Advanced.InstallUpdate);
            CheckUpdatesBtn.ToolTip = Loc.T(UiText.Advanced.InstallUpdateTip);
            ShowUpdateResult(Loc.F(UiText.Advanced.Available, feed.Version), failed: false);
        }
        else
        {
            CheckUpdatesBtn.Content = Loc.T(UiText.Advanced.CheckForUpdates);
            CheckUpdatesBtn.ToolTip = Loc.T(UiText.Advanced.CheckForUpdatesTip);
        }
    }

    /// <summary>Manual check: bypasses the skip list by contract and reports its result inline
    /// (up to date / version available / check failed) — the automatic check's silent-failure rule
    /// applies to the background path only. With an update already known, the button is
    /// "Install Update" and opens the prompt instead.</summary>
    private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateService.Available is not null) { UpdateService.OpenPromptWindow(); return; }
        if (_updateCheckRunning) return;
        _updateCheckRunning = true;
        CheckUpdatesBtn.IsEnabled = false;
        ShowUpdateResult(Loc.T(UiText.Advanced.Checking), failed: false);

        var status = await UpdateService.CheckAsync(manual: true);

        _updateCheckRunning = false;
        CheckUpdatesBtn.IsEnabled = true;
        switch (status)
        {
            case UpdateCheckStatus.UpToDate:
                ShowUpdateResult(Loc.F(UiText.Advanced.UpToDate, UpdateService.CurrentVersion), failed: false);
                break;
            case UpdateCheckStatus.UpdateAvailable:
                ShowUpdateResult(Loc.F(UiText.Advanced.Available, UpdateService.Available?.Version ?? ""), failed: false);
                break;
            default:
                ShowUpdateResult(Loc.T(UiText.Advanced.UpdateCheckFailed), failed: true);
                break;
        }
        RefreshUpdateAvailability();
    }

    private void ShowUpdateResult(string text, bool failed)
    {
        UpdateResultText.Text = text;
        UpdateResultText.Foreground = failed
            ? UpdateFailInkBrush
            : System.Windows.Application.Current?.TryFindResource("UiMutedInk") as Brush ?? UpdateMutedInkBrush;
        UpdateResultText.Visibility = Visibility.Visible;
    }

    // Same red as the tab's SGDB error text; the muted brush is only the TryFindResource fallback.
    private static readonly Brush UpdateFailInkBrush  = FreezeBrush(0xC4, 0x2B, 0x1C);
    private static readonly Brush UpdateMutedInkBrush = FreezeBrush(0x55, 0x58, 0x66);

    // ── Accessibility: the two soft links ──────────────────────────────────────────────────────────
    // "Wheels toggle on/off" → Swap left/right, and "Reduce motion" → Always show hub. Ticking a parent
    // auto-ticks its child; unticking undoes ONLY the tick we added; a child the user sets by hand stops
    // being auto-linked for the rest of the session. The drawn trees in XAML state each relationship.
    //   • Toggle ⇄ Swap: Toggle activation opens a wheel on the SAME hand (uncrossed) where Hold opens the
    //     opposite (crossed), so we re-cross rather than let the sides flip out from under the user.
    //   • Reduce motion ⇄ Always show hub: a calm wheel wants the hub standing still rather than appearing
    //     and vanishing with what it has to say.
    // ⚠ "Wheel ignores opposite stick" is on NEITHER link — either stick aims in every activation style
    // now, so nothing above it has a claim on it. Don't adopt it into a tree.
    private LinkState _swapLink = new();
    private LinkState _hubLink  = new();

    /// <summary>One parent→child auto-tick relationship's session state.</summary>
    private sealed class LinkState
    {
        public bool UserSet;   // the user set the child by hand → auto-linking stops
        public bool AutoSet;   // the child's current state was set BY the link (so we may undo it)
        public bool Syncing;   // a programmatic child change is in progress (not a user action)
    }

    /// <summary>Apply a parent's new state to its linked child, honouring the user's own choice.</summary>
    private static void ApplyLink(LinkState link, bool parentOn, System.Windows.Controls.CheckBox child)
    {
        if (link.UserSet) return;   // the user took control of this child — respect it
        if (parentOn && child.IsChecked != true)
        {
            link.Syncing = true; child.IsChecked = true; link.Syncing = false;
            link.AutoSet = true;
        }
        else if (!parentOn && link.AutoSet)   // only undo the tick WE added
        {
            link.Syncing = true; child.IsChecked = false; link.Syncing = false;
            link.AutoSet = false;
        }
    }

    /// <summary>A linked child changed: swallow our own programmatic writes, and let a hand-set value end
    /// the linking. Returns false when the change was programmatic and the caller should do nothing.</summary>
    private bool LinkedChildChanged(LinkState link)
    {
        if (_loading || link.Syncing) return false;   // ignore programmatic (auto-link) changes
        link.UserSet = true;
        link.AutoSet = false;
        return true;
    }

    private void ToggleActivation_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ApplyLink(_swapLink, ToggleActivationBox.IsChecked == true, SwapFnBox);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SwapFn_Changed(object sender, RoutedEventArgs e)
    {
        if (LinkedChildChanged(_swapLink)) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ReduceMotion_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ApplyLink(_hubLink, ReduceMotionBox.IsChecked == true, AlwaysShowHubBox);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AlwaysShowHub_Changed(object sender, RoutedEventArgs e)
    {
        if (LinkedChildChanged(_hubLink)) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void IgnoreOppositeStick_Changed(object sender, RoutedEventArgs e) => Toggle_Changed(sender, e);
    private void Narration_Changed(object sender, RoutedEventArgs e)
    {
        Toggle_Changed(sender, e);
        bool on = NarrationBox.IsChecked == true;
        // A user re-tick revives a dismissed tip: the advice is newly relevant, so a past dismissal must
        // not outlive the narration session it belonged to. Guarded on _loading so seeding never clears
        // a dismissal the user is entitled to keep.
        if (on && !_loading) _narratorTipDismissed = false;
        bool shown = UpdateNarratorTip();
        // Speak the tip only on the USER's tick, never on Load — a seeded checkbox would otherwise make
        // Radiata talk every time Settings opens. Spoken through the host's sink rather than the
        // Announcer: this is a Settings utterance, so the wheel's coalescing and the not-yet-saved
        // Announcer.Enabled flag both have nothing to say about it. Tied to `shown` so the voice never
        // recites advice the card itself decided not to give.
        if (shown && !_loading) SpeakHook?.Invoke(NarratorTipCopy);
    }

    /// <summary>Single owner of the tip's visibility; returns whether it ended up shown. Three gates, all
    /// of which must pass: Narration is on, the tip hasn't been dismissed, and Windows Narrator isn't
    /// ALREADY running — a user who has system narration needs no pointer to it, and hearing Radiata
    /// recommend a thing already talking to them would read as a bug.</summary>
    private bool UpdateNarratorTip()
    {
        bool show = NarrationBox.IsChecked == true && !_narratorTipDismissed && !NarratorLauncher.IsRunning;
        NarratorTip.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        return show;
    }

    /// <summary>Persisted through <see cref="ApplyTo"/>, so the tip stays gone across restarts — until
    /// Narration is turned off and on again (see <see cref="Narration_Changed"/>).</summary>
    private bool _narratorTipDismissed;

    private void DismissNarratorTipBtn_Click(object sender, RoutedEventArgs e)
    {
        _narratorTipDismissed = true;
        NarratorTip.Visibility = Visibility.Collapsed;
        Changed?.Invoke(this, EventArgs.Empty);   // the dismissal is config — mark the window dirty
    }

    // ── Windows Narrator tip ─────────────────────────────────────────────────────
    /// <summary>Read aloud AND shown in <c>NarratorTip</c> — one string so the spoken and printed forms
    /// can't drift. Sentence-shaped rather than UI-clipped because it has to work as speech.</summary>
    private static string NarratorTipCopy => Loc.T(UiText.Advanced.NarratorTip);

    /// <summary>Host-set (App) voice for the tip. Null = print it and stay silent; Settings must open
    /// with or without a speech engine.</summary>
    public static Action<string>? SpeakHook;

    private void LaunchNarratorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (NarratorLauncher.Launch())
        {
            // The advice has been taken, so the card has nothing left to say — hide it WITHOUT setting
            // the dismissed flag: the next open re-decides from whether Narrator is actually running, so
            // quitting Narrator later brings the tip back rather than leaving the user with no pointer.
            NarratorTip.Visibility = Visibility.Collapsed;
            return;
        }
        // Narrator absent or policy-blocked: say so rather than leave the user waiting for a voice.
        string failed = Loc.T(UiText.Advanced.NarratorFailed);
        System.Windows.MessageBox.Show(Window.GetWindow(this), failed, Loc.T(UiText.Advanced.NarratorCaption),
                                       MessageBoxButton.OK, MessageBoxImage.Warning);
        SpeakHook?.Invoke(failed);
    }

    /// <summary>Updates the "Current Controller" readout — called from <see cref="Load"/> and again whenever
    /// the host detects a live controller-kind change (SettingsWindow.SetDetectedKind), so it tracks the pad
    /// in the user's hands without a Settings reopen.</summary>
    public void SetControllerKind(ControllerKind kind, bool connected,
                                  ControllerTransport transport = ControllerTransport.Unknown)
    {
        // The transport rides as an icon here, so the name is asked for WITHOUT it (elsewhere —
        // the New-Controller card, OOBE — FriendlyKind still spells the transport out in words).
        CurrentControllerText.Text = connected ? App.FriendlyKind(kind) : Loc.T(UiText.Onboarding.NoController);
        SetTransportIcon(connected ? transport : ControllerTransport.Unknown);
        // The Bluetooth-drop toggle governs the "Controller Signal Lost" card, which only the raw-HID reader's
        // Bluetooth path can raise (ControllerReader.BtLinkDead). XInput moves its packet counter only on input,
        // so an idle Xbox pad and a wedged link look identical and that backend raises nothing: the box is
        // offered exactly when a raw-HID pad is KNOWN to be on Bluetooth. Its stored value is untouched either
        // way (see ApplyTo).
        BluetoothDropBox.Visibility = connected && transport == ControllerTransport.Bluetooth
                                      && kind != ControllerKind.Xbox
                                      ? Visibility.Visible : Visibility.Collapsed;
        Brush glyphBrush = !connected
            ? ControllerDisconnectedBrush
            : kind switch
              {
                  ControllerKind.Xbox           => ControllerXboxBrush,
                  ControllerKind.DualSenseEdge  => ControllerDualSenseEdgeBrush,
                  _                             => ControllerOtherBrush,
              };
        ControllerGlyph.Source = PackIconHelper.FromName("Controller", glyphBrush);
        _kind = kind;
        _connected = connected;
        RefreshBattery();
    }

    /// <summary>Show the pad's transport as an icon beside its name — Bluetooth or a USB port. Hidden for
    /// <see cref="ControllerTransport.Unknown"/> (nothing connected, or an XInput pad whose transport the
    /// backend can't answer), since a wrong guess here reads as fact. The word rides along as the tooltip
    /// AND the automation name, so replacing the text with a glyph doesn't cost a screen reader the
    /// information.</summary>
    private void SetTransportIcon(ControllerTransport transport)
    {
        var (icon, word) = transport switch
        {
            ControllerTransport.Bluetooth => ("Bluetooth", "Bluetooth"),
            ControllerTransport.Usb       => ("UsbPort",   "USB"),
            _                             => (null,        null),
        };
        if (icon is null)
        {
            ControllerTransportIcon.Visibility = Visibility.Collapsed;
            ControllerTransportIcon.Source = null;
            return;
        }
        ControllerTransportIcon.Source = PackIconHelper.FromName(icon, TransportIconBrush);
        ControllerTransportIcon.ToolTip = word;
        System.Windows.Automation.AutomationProperties.SetName(ControllerTransportIcon, word);
        ControllerTransportIcon.Visibility = Visibility.Visible;
    }

    // ── Battery readout ─────────────────────────────────────────────────────────
    private ControllerKind _kind;
    private bool _connected;
    private int  _batteryPercent = -1;   // -1 = never reported (hide the row)
    private bool _batteryCharging;

    /// <summary>Host pushes the pad's battery level (0–100%, and whether it's charging); percent &lt; 0 means
    /// "unknown" and hides the row. Same feed as the wheel hub's battery glyph.</summary>
    public void SetBattery(int percent, bool charging)
    {
        _batteryPercent  = percent;
        _batteryCharging = charging;
        RefreshBattery();
    }

    /// <summary>Repaint the battery row. The two backends report at different resolutions and the label has
    /// to stay honest about it: XInput exposes four coarse levels only (empty/low/medium/full, plus a
    /// "wired" answer for mains-powered pads), so an Xbox pad reads in words; Sony pads report over raw HID
    /// in 10% steps, so those read as a percentage.</summary>
    private void RefreshBattery()
    {
        if (!_connected || _batteryPercent < 0)
        {
            BatteryPanel.Visibility = Visibility.Collapsed;
            return;
        }

        bool coarse = _kind == ControllerKind.Xbox;
        string label;
        if (coarse)
            // XInput reports "wired" as a battery type, not a charge level — the pad is on mains power.
            label = _batteryCharging
                ? Loc.T(UiText.Advanced.BatteryWired)
                : _batteryPercent switch
                  {
                      <= 10 => Loc.T(UiText.Advanced.BatteryEmpty),
                      <= 35 => Loc.T(UiText.Advanced.BatteryLow),
                      <= 70 => Loc.T(UiText.Advanced.BatteryMedium),
                      _     => Loc.T(UiText.Advanced.BatteryFull),
                  };
        else
            label = Loc.F(UiText.Advanced.BatteryPercent, _batteryPercent) + (_batteryCharging ? " " + Loc.T(UiText.Advanced.Charging) : "");

        BatteryText.Text = label;
        BatteryText.ToolTip = coarse
            ? Loc.T(UiText.Advanced.BatteryCoarse)
            : Loc.T(UiText.Advanced.BatterySteps);
        BatteryPanel.ToolTip = BatteryText.ToolTip;
        BatteryGlyph.Source = PackIconHelper.FromName(BatteryGlyphName(_batteryPercent, _batteryCharging),
                                                     !_batteryCharging && _batteryPercent <= 10
                                                         ? BatteryLowInkBrush : BatteryInkBrush);
        BatteryPanel.Visibility = Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(BatteryPanel, label);
    }

    private static string BatteryGlyphName(int pct, bool charging) =>
        charging    ? "MicrosoftXboxControllerBatteryCharging"
        : pct <= 10 ? "MicrosoftXboxControllerBatteryAlert"
        : pct <= 35 ? "MicrosoftXboxControllerBatteryLow"
        : pct <= 70 ? "MicrosoftXboxControllerBatteryMedium"
        :             "MicrosoftXboxControllerBatteryFull";

    /// <summary>Fold this tab's fields into <paramref name="cfg"/> (a `with` copy — every field this tab
    /// doesn't own passes through untouched, so first-run fields survive a save).</summary>
    public SystemConfig ApplyTo(SystemConfig cfg) => cfg with
    {
        Language = _language,
        // DPAPI at rest; ToStoredStable reuses the existing blob when the key text didn't change, so a
        // save doesn't churn the config with a fresh (always-different) encryption of the same secret.
        SteamGridDbKey       = SecretField.ToStoredStable(
                                   string.IsNullOrWhiteSpace(SgdbKeyBox.Text) ? null : SgdbKeyBox.Text.Trim(),
                                   cfg.SteamGridDbKey),
        PreferPlayniteCovers = PreferPlayniteBox.IsChecked == true,
        // OBS connection (this tab's, since Integrations owns the setup pane). Only a dirty copy writes, so
        // a save can't revert a change made elsewhere while Settings was open.
        ObsPort              = _obsDirty ? _obsPort           : cfg.ObsPort,
        ObsPassword          = _obsDirty ? _obsStoredPassword : cfg.ObsPassword,
        ObsConfigured        = _obsDirty || cfg.ObsConfigured,
        // Storefront opt-outs are authored in the Game Grid, so this tab only ever CLEARS them, and only
        // when the user pressed Reset Hidden Games. Any other save MUST pass the live list straight through,
        // or a Settings save silently un-hides a storefront the grid hid a moment ago.
        DisabledStorefronts  = TakeStorefrontReset(cfg.DisabledStorefronts),
        // ⚠ DpadHorizontalMode / ShowSliceLabels belong to Customize and must NOT be written here: this
        // `with` runs LAST in SettingsWindow.Save, so writing them would clobber the user's pick.
        // The six Accessibility fields below ARE this tab's — CustomizeEditorControl writes none of them.
        TriggerActivation    = ToggleActivationBox.IsChecked == true ? "toggle" : "hold",
        SwapFnButtons        = SwapFnBox.IsChecked == true,
        WheelIgnoresOppositeStick = IgnoreOppositeStickBox.IsChecked == true,
        AlwaysShowHub        = AlwaysShowHubBox.IsChecked == true,
        ReduceMotion         = ReduceMotionBox.IsChecked == true,
        Narration            = NarrationBox.IsChecked == true,
        NarratorTipDismissed = _narratorTipDismissed,
        PracticeWhileSettingsOpen = PracticeSettingsBox.IsChecked == true,
        // Read even while the box is hidden (no Bluetooth pad connected): it still holds the value Load
        // seeded, so saving from a USB session can't silently reset the choice.
        BluetoothDropAlert   = BluetoothDropBox.IsChecked == true,
        // Updates section (this tab owns both toggles; UpdateSkippedVersion is the prompt window's
        // field and passes through untouched).
        UpdateCheckEnabled   = UpdateCheckBox.IsChecked == true,
        CrashPromptEnabled   = CrashPromptBox.IsChecked == true,
        CrashAutoSend        = CrashAutoSendBox.IsChecked == true,
    };

    // ── Storefront hides: this tab's only job is the reset ──────────────────────
    /// <summary>Set by <see cref="ResetHiddenGamesBtn_Click"/>, consumed by the next <see cref="ApplyTo"/>.
    /// A one-shot: cleared as soon as it's honoured, so a LATER save (or a storefront hidden from the grid
    /// while Settings is still open) isn't wiped by a reset the user pressed minutes ago.</summary>
    private bool _resetStorefrontHides;

    private List<string> TakeStorefrontReset(List<string> current)
    {
        if (!_resetStorefrontHides) return current;
        _resetStorefrontHides = false;
        return new List<string>();
    }

    // "Prefer Playnite covers" only matters when BOTH sources exist; without Playnite the "Install
    // Playnite…" button takes its place.
    private void UpdatePreferPlayniteVisibility()
    {
        bool playnite = PlayniteLibrary.IsAvailable;
        PreferPlayniteBox.Visibility = playnite && !string.IsNullOrWhiteSpace(SgdbKeyBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        InstallPlaynitePanel.Visibility = playnite ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Field_Changed(object sender, TextChangedEventArgs e)
    {
        UpdatePreferPlayniteVisibility();   // typing/clearing the key reveals/hides the toggle
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ("Show labels on" and the D-Pad picker live on Customize — their helpers are there too.)

    // ── SteamGridDB key test ────────────────────────────────────────────────────
    /// <summary>The last key value <see cref="GameArt.TestKeyAsync"/> ran on (or the key as loaded, which
    /// counts as accepted) — commits of an UNCHANGED key never re-test.</summary>
    private string _lastTestedSgdbKey = "";
    /// <summary>Whether the config had a key when this tab loaded — the auto "Retry Missing Game Art" kick
    /// fires only on the FIRST successful entry of a key where none was set before.</summary>
    private bool _hadSgdbKeyAtLoad;
    private bool _sgdbRetryArtKicked;   // one-shot per Settings session
    /// <summary>Whether <see cref="_lastTestedSgdbKey"/> was accepted — drives the tick.</summary>
    private bool _lastTestedSgdbKeyOk;

    /// <summary>The tick shows only while the box still holds the accepted key; an edit clears it until the
    /// commit-time test comes back.</summary>
    private void RefreshSgdbKeyCheck()
    {
        string key = SgdbKeyBox.Text.Trim();
        SgdbKeyCheck.Visibility = _lastTestedSgdbKeyOk && key.Length > 0
                                  && string.Equals(key, _lastTestedSgdbKey, StringComparison.Ordinal)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SgdbKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshSgdbKeyCheck();
        Field_Changed(sender, e);
    }

    /// <summary>Commit-time key test on focus loss, deliberately NOT per keystroke: a changed, non-empty key
    /// gets one cheap authenticated call. The first success on a previously key-less config kicks "Retry
    /// Missing Game Art". Async and non-blocking — the UI never waits on the network.</summary>
    private async void SgdbKeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        string key = SgdbKeyBox.Text.Trim();
        if (key.Length == 0)
        {
            SgdbKeyError.Visibility = Visibility.Collapsed;
            _lastTestedSgdbKey = ""; _lastTestedSgdbKeyOk = false; RefreshSgdbKeyCheck();
            return;
        }
        if (string.Equals(key, _lastTestedSgdbKey, StringComparison.Ordinal)) return;   // unchanged — don't re-test
        _lastTestedSgdbKey = key;
        _lastTestedSgdbKeyOk = false;
        RefreshSgdbKeyCheck();
        SgdbKeyError.Visibility = Visibility.Collapsed;

        bool ok = await GameArt.TestKeyAsync(key);
        // The user may have edited the box while the test was in flight — only report a still-current result.
        if (!string.Equals(SgdbKeyBox.Text.Trim(), key, StringComparison.Ordinal)) return;

        if (!ok) { SgdbKeyError.Visibility = Visibility.Visible; return; }
        _lastTestedSgdbKeyOk = true;
        RefreshSgdbKeyCheck();
        if (!_hadSgdbKeyAtLoad && !_sgdbRetryArtKicked)
        {
            _sgdbRetryArtKicked = true;
            RetryMissedArtRequested?.Invoke(this, EventArgs.Empty);   // same path as the button
        }
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Button handlers (each raises an event the host handles) ─────────────────
    private void ClearArtCacheBtn_Click(object sender, RoutedEventArgs e)     => ClearArtCacheRequested?.Invoke(this, EventArgs.Empty);
    private void RetryMissedArtBtn_Click(object sender, RoutedEventArgs e)    => RetryMissedArtRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Un-hide everything the Game Grid is hiding: the per-game flags AND the hidden STOREFRONTS.
    /// The two halves reset differently — the game flags are GameArt's own cover-overrides.json (written
    /// immediately), while the storefront list is config, which only the host writes. The clear is staged on
    /// <see cref="_resetStorefrontHides"/> and lands with the next save; raising <see cref="Changed"/> is
    /// what makes that "next save" seconds away rather than whenever the user next edits something.</summary>
    private void ResetHiddenGamesBtn_Click(object sender, RoutedEventArgs e)
    {
        int games = GameMetadata.ResetHiddenAll();
        // Count off the config as last LOADED — Load() re-runs on an external config reload, so a storefront
        // hidden from the grid while this window is open is already reflected.
        int stores = _loadedCfg.DisabledStorefronts.Count;
        if (stores > 0)
        {
            _resetStorefrontHides = true;
            Changed?.Invoke(this, EventArgs.Empty);   // marks dirty → the host's debounced save applies it
        }

        string msg = (games, stores) switch
        {
            (0, 0) => Loc.T(UiText.Advanced.NothingHidden),
            (_, 0) => Loc.P(UiText.Advanced.RestoredGamesOne,  UiText.Advanced.RestoredGamesOther,  games),
            (0, _) => Loc.P(UiText.Advanced.RestoredStoresOne, UiText.Advanced.RestoredStoresOther, stores),
            // Two sentences, not one with two counts: a single sentence would need every combination of
            // plural forms, and no message-format system reconstructs that.
            _      => Loc.P(UiText.Advanced.RestoredGamesOne,  UiText.Advanced.RestoredGamesOther,  games) + " "
                    + Loc.P(UiText.Advanced.RestoredStoresOne, UiText.Advanced.RestoredStoresOther, stores),
        };
        System.Windows.MessageBox.Show(msg, Loc.T(UiText.Advanced.ResetHiddenCaption), MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void RecoverControllerBtn_Click(object sender, RoutedEventArgs e) => RecoverControllerRequested?.Invoke(this, EventArgs.Empty);
    private void QuitRadiataBtn_Click(object sender, RoutedEventArgs e)       => QuitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Troubleshooting dropdown: fires the matching *Requested event for the picked item, then
    /// snaps the combo back to the "Troubleshooting…" placeholder. That reset re-selection would re-enter
    /// this handler (SelectionChanged fires on ANY change, including one this method makes) —
    /// <see cref="_resettingTroubleshootingBox"/> guards it, and <see cref="_loading"/> guards Load()
    /// touching the control before the config is fully seeded. The Separator ahead of the destructive items
    /// isn't a ComboBoxItem, so the `as` cast below yields null → tag "" → early return.</summary>
    private void TroubleshootingBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _resettingTroubleshootingBox) return;
        string tag = (TroubleshootingBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        if (tag.Length == 0) return;   // placeholder re-selected (or nothing selected) — nothing to run

        switch (tag)
        {
            case "oobe":            RunOobeRequested?.Invoke(this, EventArgs.Empty);           break;
            case "drivers":         InstallDriversRequested?.Invoke(this, EventArgs.Empty);    break;
            case "hid":             HidDiagnosticsRequested?.Invoke(this, EventArgs.Empty);    break;
            case "controllersetup": ControllerSetupRequested?.Invoke(this, EventArgs.Empty);   break;
            case "emaillog":        EmailLogRequested?.Invoke(this, EventArgs.Empty);          break;
            case "backup":          BackupRequested?.Invoke(this, EventArgs.Empty);            break;
            case "restore":         RestoreRequested?.Invoke(this, EventArgs.Empty);           break;
            case "reset":           ResetRequested?.Invoke(this, EventArgs.Empty);             break;
            case "wipe":            WipeRequested?.Invoke(this, EventArgs.Empty);              break;
            case "uninstall":       UninstallRequested?.Invoke(this, EventArgs.Empty);         break;
        }

        _resettingTroubleshootingBox = true;
        TroubleshootingBox.SelectedIndex = 0;   // back to "Troubleshooting…"
        _resettingTroubleshootingBox = false;
    }

    /// <summary>"Start with Windows" is a registry setting (StartupManager), not part of AppConfig — applied
    /// directly on toggle rather than folded into ApplyTo, and deliberately does NOT raise <see cref="Changed"/>
    /// so it never marks the Settings window's config dirty.</summary>
    private void StartWithWindowsBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        StartupManager.SetEnabled(StartWithWindowsBox.IsChecked == true);
    }
}
