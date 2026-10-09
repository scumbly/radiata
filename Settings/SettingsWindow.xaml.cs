using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ControllerWheel;

public partial class SettingsWindow : Window
{
    private readonly ConfigLoader _config;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _statusHideTimer;   // auto-hides the status badge a few seconds after a transient message
    private readonly Action<WheelSlice>? _onTest;
    private readonly Action? _runOnboarding;   // App hook for the Advanced "Run first-run setup" button
    private string _version = "";              // "v0.x.y…" for the Email Log subject (shown only in the title bar)

    /// <summary>App hooks for the controller/driver tools — set right after construction (OpenSettings).
    /// Install/Recover/Controller Setup/HID Diagnostics all live on the Advanced tab's Troubleshooting
    /// dropdown. Null = that button is a silent no-op.</summary>
    public Action? ControllerSetupHook   { get; set; }
    public Action? InstallDriversHook    { get; set; }
    public Action? HidDiagnosticsHook    { get; set; }
    public Action? RecoverControllerHook { get; set; }
    /// <summary>App's clean-exit path — must be the SAME path the tray "Exit" item uses
    /// (App.ExitApp: TearDown then Shutdown), not a bare Application.Shutdown call, so Quit Radiata
    /// releases the virtual pad and un-cloaks the controller exactly like every other quit route.</summary>
    public Action? QuitRequestedHook     { get; set; }
    /// <summary>Host hook for the wheel editors' "Test Wheel" button — invoked with true for the Right
    /// Wheel editor (WheelBEditor), false for the Left (WheelAEditor). Null = a silent no-op.</summary>
    public Action<bool>? TestWheelHook { get; set; }
    private ControllerKind _detectedKind;   // which controller's trigger options the Customize tab shows (live — see SetDetectedKind)
    private bool _restarting;   // set when wiping+relaunching, to skip the unsaved-changes close prompt

    public SettingsWindow(ConfigLoader config, Action<WheelSlice>? onTest = null,
                          ControllerKind detectedKind = ControllerKind.DualSenseEdge,
                          Action? runOnboarding = null)
    {
        InitializeComponent();
        LocWpf.ApplyTo(this);
        TranslationNote.Visibility = Loc.Lang == HelpLocalization.DefaultCode ? Visibility.Collapsed : Visibility.Visible;
        // Always centred on the PRIMARY display, never CenterScreen — see docs/SETTINGS-UI.md ▸ Placement.
        // WorkArea is the primary display's working area in DIPs, matching Left/Top's units.
        var work = SystemParameters.WorkArea;
        Left = work.Left + Math.Max(0, (work.Width  - Width)  / 2);
        Top  = work.Top  + Math.Max(0, (work.Height - Height) / 2);
        _config = config;
        _onTest = onTest;
        _runOnboarding = runOnboarding;
        _detectedKind = detectedKind;

        // Full version (0.<minor>.<build>.<YYMMDD>) lives in InformationalVersion; strip any +suffix.
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var ver = string.IsNullOrEmpty(info) ? "" : "v" + info.Split('+')[0];
        _version = ver;   // for the Email Log subject
        // No version in the title: Narrator reads the whole bar on focus, and the version already
        // lives in About. The Title is a {loc:T} label in the XAML.
        AboutVersionText.Text = ver;
        // ShowAbout selects the last tab — keep About last in the XAML.

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };

        // The status badge auto-hides a few seconds after a transient message (e.g. "Saved.") — persistent
        // states like "Unsaved changes…" pass autoHide: false so they stick until the next SetStatus call.
        _statusHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _statusHideTimer.Tick += (_, _) => { _statusHideTimer.Stop(); StatusBadge.Visibility = Visibility.Collapsed; };

        // Ctrl+S saves immediately
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => { _saveTimer.Stop(); Save(); }),
            new KeyGesture(Key.S, ModifierKeys.Control)));

        WheelAEditor.Changed += OnEditorChanged;
        WheelBEditor.Changed += OnEditorChanged;
        CustomizeEditor.Changed += OnEditorChanged;
        SystemEditor.Changed += OnEditorChanged;
        ExceptionsEditor.Changed += OnEditorChanged;
        // Drag-and-drop package install, anywhere in the window. Tunnelling handlers see the drag before
        // any child (the wheel slice list refuses non-.exe/.lnk drops, text boxes swallow file drops) and
        // claim it ONLY when every path is a folder or a .zip, so every other drag keeps its meaning.
        if (PackageInstallFlow.Offered)
        {
            AllowDrop = true;
            PreviewDragOver += (_, e) =>
            {
                if (DroppedPackagePaths(e) is null) return;
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
            };
            PreviewDrop += (_, e) =>
            {
                if (DroppedPackagePaths(e) is not { } paths) return;
                e.Handled = true;
                PackageInstallFlow.Install(paths, this);
            };
        }
        // Contextual "Learn more" links jump into the Help tab at the matching topic.
        CustomizeEditor.HelpRequested  += ShowHelpTopic;
        ExceptionsEditor.HelpRequested += ShowHelpTopic;
        SystemEditor.HelpRequested     += ShowHelpTopic;
        WheelAEditor.HelpRequested     += ShowHelpTopic;
        WheelBEditor.HelpRequested     += ShowHelpTopic;
        // An unconfigured OBS slice's "Configure OBS Integration…" opens the Advanced tab's pane in place —
        // no tab switch, so the user doesn't lose the slice they were editing.
        WheelAEditor.ObsSetupRequested += (_, _) => SystemEditor.OpenObsSetup();
        WheelBEditor.ObsSetupRequested += (_, _) => SystemEditor.OpenObsSetup();
        SystemEditor.ObsConfigChanged  += (_, _) =>
            WheelAEditor.ObsConfigured = WheelBEditor.ObsConfigured = SystemEditor.ObsConfigured;

        // Passthru Mode only matters when the input-isolation drivers are present; otherwise the tab is
        // redundant. This tab and the tray item are the only surfaces that offer the toggle — the
        // "Toggle Passthru Mode" slice action is un-offered (docs/ACTIONS.md ▸ hidden but supported).
        SafeModeTab.Visibility = App.SafeModeAvailable ? Visibility.Visible : Visibility.Collapsed;
        HelpEditor.BackRequested       += (_, _) => HelpBack();
        SystemEditor.LanguageChanged   += (_, code) => OnLanguageChanged(code);
        // Clicking the Help tab directly = no back affordance (only Help-chip deep-links get one).
        Tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, Tabs)) return;   // child ComboBox/ListBox events bubble here too
            // Browsing off a wheel tab settles that tab's unsaved slice draft there and then — the prompt
            // belongs to the edit being left, not to closing the window, which can be minutes and several
            // tabs later. Cancel puts the tab back: TabControl has already switched by the time this
            // fires, so the only way to refuse is to reselect, guarded against the re-entrant event.
            // A Help chip inside the slice editor is exempt (_helpViaLink): that jump is ABOUT the field
            // being edited and returns to this tab, so demanding a save first would answer the wrong question.
            if (!_tabRevert && !_helpViaLink && (e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null) is TabItem left
                && left.Content is WheelEditorControl leftEditor && !leftEditor.TryCommitOrDiscard())
            {
                _tabRevert = true;
                try { Tabs.SelectedItem = left; } finally { _tabRevert = false; }
                return;
            }
            if (ReferenceEquals(Tabs.SelectedItem, HelpTab) && !_helpViaLink)
                HelpEditor.SetBackVisible(false);
        };
        // Help re-renders when the glyph set flips (PS ✕○□△ ⇄ Xbox A/B/X/Y) so its button prompts match.
        ControllerButtons.Changed += ReloadHelp;
        Closed += (_, _) => ControllerButtons.Changed -= ReloadHelp;
        SystemEditor.RunOobeRequested += (_, _) => _runOnboarding?.Invoke();
        SystemEditor.InstallDriversRequested    += (_, _) => InstallDriversHook?.Invoke();
        SystemEditor.RecoverControllerRequested += (_, _) => RecoverControllerHook?.Invoke();
        SystemEditor.HidDiagnosticsRequested += (_, _) => HidDiagnosticsHook?.Invoke();
        SystemEditor.QuitRequested           += (_, _) => QuitRequestedHook?.Invoke();
        SystemEditor.ControllerSetupRequested += (_, _) => ControllerSetupHook?.Invoke();

        // Keep both wheel editors' slice-list column the same width.
        WheelAEditor.ListWidthChanged += (_, w) => WheelBEditor.SetListWidth(w);
        WheelBEditor.ListWidthChanged += (_, w) => WheelAEditor.SetListWidth(w);

        WheelAEditor.TestRequested += (_, slice) => { FlushPendingSave(); _onTest?.Invoke(slice); };
        WheelBEditor.TestRequested += (_, slice) => { FlushPendingSave(); _onTest?.Invoke(slice); };
        WheelAEditor.TestWheelRequested += (_, _) => { FlushPendingSave(); TestWheelHook?.Invoke(false); };
        WheelBEditor.TestWheelRequested += (_, _) => { FlushPendingSave(); TestWheelHook?.Invoke(true); };
        WheelAEditor.TestWheelLabel = Loc.T(UiText.Settings.TestLeftWheel);
        WheelBEditor.TestWheelLabel = Loc.T(UiText.Settings.TestRightWheel);
        WheelAEditor.SliceListName  = Loc.T(UiText.Settings.LeftWheelSlices);
        WheelBEditor.SliceListName  = Loc.T(UiText.Settings.RightWheelSlices);

        SystemEditor.BackupRequested += (_, _) => BackUpSettings();
        SystemEditor.RestoreRequested += (_, _) => RestoreSettings();
        SystemEditor.ResetRequested  += (_, _) => ResetAllSettings();
        SystemEditor.WipeRequested   += (_, _) => WipeAppDataAndReset();
        SystemEditor.ClearArtCacheRequested += (_, _) => ClearArtCache();
        SystemEditor.EmailLogRequested += (_, _) => EmailLog();
        // Uninstall. Installer-managed copy: hand off to the Inno uninstaller — it owns the files, and
        // its [UninstallRun] step calls back into `Radiata.exe --uninstall-cleanup` for the Windows-state
        // cleanup. Portable copy: spawn our own exe in --uninstall mode (the same flow the app-folder
        // "Uninstall Radiata.cmd" runs) — it shows the confirm dialog, then coexists with and kills
        // this running instance before doing the elevated work.
        SystemEditor.UninstallRequested += (_, _) =>
        {
            try
            {
                if (InstallInfo.IsInstalledCopy && InstallInfo.UninstallerPath is string uninstaller)
                    Process.Start(new ProcessStartInfo(uninstaller) { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? "", "--uninstall") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Settings] uninstall launch failed: {ex.Message}");
                SetStatus(Loc.T(UiText.Settings.UninstallerFailed));
            }
        };
        SystemEditor.RetryMissedArtRequested += (_, _) => RetryMissedArt();

        // Don't let the window close with an unsaved slice draft silently lost.
        Closing += (_, ev) =>
        {
            if (_restarting) return;   // wipe+restart in progress — don't prompt
            if (!WheelAEditor.TryCommitOrDiscard()) { ev.Cancel = true; return; }
            if (!WheelBEditor.TryCommitOrDiscard()) { ev.Cancel = true; return; }
            if (_saveTimer.IsEnabled || _saveFailed)
            {
                _saveTimer.Stop();
                if (!Save()) ev.Cancel = true;
            }
        };

        Loaded += (_, _) => { LoadFromConfig(_config.Current); ApplyPendingEdit(); FitTabStrip(); };

        // Keep the wheel editors in sync with external config changes (the in-wheel editor live-saves a
        // wheel while Settings is open) so a later Settings save doesn't revert them.
        _config.Reloaded += OnConfigReloadedExternal;
        Closed += (_, _) => _config.Reloaded -= OnConfigReloadedExternal;
    }

    /// <summary>The config changed on disk while Settings is open (e.g. the in-wheel editor live-saved a
    /// wheel). Refresh the wheel editors from it so a later Settings save doesn't revert those changes —
    /// but only while Settings has nothing unsaved of its own (else the in-progress Settings edit wins, the
    /// rarer two-editors-at-once conflict). Editors whose data already matches are left alone, so this never
    /// disrupts on Settings' own writes.</summary>
    private void OnConfigReloadedExternal(AppConfig cfg)
    {
        if (!IsLoaded || _saveTimer.IsEnabled || _saveFailed) return;
        // Refresh the SystemConfig baseline the two tabs layer onto at Save. Without this, App-side writes
        // that land while Settings is open (controller-kind memory, GameGridTipSeen, OobeVersion) would be
        // reverted by the next Settings save, which layers ApplyTo onto the STALE window-open snapshot.
        // The _saveTimer guard above means a pending Settings/Developer edit is never clobbered — this only
        // runs when nothing is dirty (including right after our own save's self-echo, which is idempotent).
        // The tabs' controls are refreshed only when they disagree with the reloaded config on a field they
        // own: layering the controls' ApplyTo chain onto the reloaded SystemConfig reproduces it exactly when
        // they already show it (Settings' own write echoing back, or an App-side write to a field no control
        // owns), and differs when the file was changed outside the window (a hand edit). Fields an outside
        // writer also owns (the tray's Passthru Mode item, the setup wizard, the material cycle) are written
        // by their control only when the user moved it, so ApplyTo keeps the reloaded value for them and the
        // chain can't see the change; each editor's ChangedOutside reports those. Their Load paths are
        // _loading-guarded, so the refresh raises no Changed and no save.
        // Without it the stale controls would also be folded back over the new values by the next Save.
        bool controlsStale = ExceptionsEditor.ChangedOutside(cfg.System) ||
            SystemEditor.ChangedOutside(cfg.System) || CustomizeEditor.ChangedOutside(cfg.System) ||
            System.Text.Json.JsonSerializer.Serialize(cfg.System) !=
            System.Text.Json.JsonSerializer.Serialize(
                ExceptionsEditor.ApplyTo(SystemEditor.ApplyTo(CustomizeEditor.ApplyTo(cfg.System))));
        _systemBase   = cfg.System;
        ReconcileOrAppend(WheelAEditor, cfg.WheelA);
        ReconcileOrAppend(WheelBEditor, cfg.WheelB);
        if (controlsStale)
        {
            _actionColors = cfg.ActionColors is null ? new() : new(cfg.ActionColors);
            _actionIcons  = cfg.ActionIcons  is null ? new() : new(cfg.ActionIcons);
            LoadSystemTabs(cfg.System);
        }
        // An in-wheel edit that crossed the Thick slice limit just rewrote SliceThickness — without this the
        // Customize tab keeps showing Thick selected (tile visible, no note) against a config that says
        // Medium. Same pair of calls Save() makes for its own writes.
        CustomizeEditor.ReloadThickness(cfg.System);
        PushSliceCountsToCustomize();
    }

    /// <summary>No unsaved edits in this editor → full reconcile (keeps list position). With unsaved edits,
    /// still surface a PURE APPEND (a slice added via the in-wheel editor) without reloading — so the new
    /// slice shows up immediately while the in-progress draft on another slice is preserved untouched.</summary>
    private static void ReconcileOrAppend(WheelEditorControl editor, WheelSlice[] slices)
    {
        if (editor.HasPendingEdits) editor.TryAppendExternal(slices);
        else                        ReconcileWheel(editor, slices);
    }

    private static void ReconcileWheel(WheelEditorControl editor, WheelSlice[] slices)
    {
        if (System.Text.Json.JsonSerializer.Serialize(editor.GetSlices()) ==
            System.Text.Json.JsonSerializer.Serialize(slices)) return;   // unchanged (incl. our own write)
        int keep = editor.SelectedIndex;   // don't yank the user's list position on an external refresh
        editor.Load(slices);
        if (keep > 0 && slices.Length > 0) editor.SelectSlice(Math.Min(keep, slices.Length - 1));
    }

    /// <summary>The detected controller KIND changed while Settings is open (pad swapped mid-session) —
    /// retarget the Customize tab's trigger chord-builder at the new kind, so the user edits the pad
    /// actually in their hands. Skipped while a Settings edit is pending (the reload would clobber it —
    /// same rule as the external-reload reconcile); the builder then stays on the old kind until the
    /// window is reopened, which is harmless (per-kind trigger saves never drop other kinds' entries).</summary>
    // ── Wheels-disabled strip ────────────────────────────────────────────────────
    /// <summary>Set by App: fired when the user clicks the strip's Enable button.</summary>
    internal Action? EnableWheelsRequested;

    /// <summary>Show/hide the bottom "wheels are disabled" strip. Called by App on open and on every
    /// enable/disable flip while this window is up. The tab area shrinks to keep content clear of it.</summary>
    public void SetWheelsEnabled(bool enabled)
    {
        WheelsDisabledStrip.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        Tabs.Margin = enabled ? new Thickness(4) : new Thickness(4, 4, 4, 30);
    }

    private void EnableWheelsBtn_Click(object sender, RoutedEventArgs e) => EnableWheelsRequested?.Invoke();

    public void SetDetectedKind(ControllerKind kind)
    {
        if (kind == _detectedKind) return;
        _detectedKind = kind;
        if (!IsLoaded) return;
        ReloadHelp();   // read-only — safe regardless of pending edits; {invoke}/{disable} follow the kind
        SystemEditor.SetControllerKind(kind, _controllerConnected, _controllerTransport);   // read-only readout — safe regardless of pending edits
        if (_saveTimer.IsEnabled) return;
        CustomizeEditor.ControllerConnected = _controllerConnected;
        CustomizeEditor.Load(_config.Current.System, kind);
    }

    private bool _controllerConnected;   // live pad-present state (App pushes it), for the Current Controller readout
    private ControllerTransport _controllerTransport;   // …and how it's attached, for the same readout

    /// <summary>Host tells us whether a controller is currently seen (and how it's attached) — drives the
    /// Current Controller glyph's colour (grey when not connected), its label, and the button-glyph
    /// "Best guess" tile, which resolves to Xbox with no pad present.</summary>
    public void SetControllerConnected(bool connected, ControllerTransport transport = ControllerTransport.Unknown)
    {
        _controllerConnected = connected;
        _controllerTransport = transport;
        CustomizeEditor.ControllerConnected = connected;
        if (!IsLoaded) return;
        SystemEditor.SetControllerKind(_detectedKind, connected, transport);
        if (!_saveTimer.IsEnabled) CustomizeEditor.Load(_config.Current.System, _detectedKind);
    }

    private int  _batteryPercent = -1;   // last battery level App pushed (-1 = none reported yet)
    private bool _batteryCharging;

    /// <summary>Host pushes the pad's battery level for the Current Controller card. Cached so the readout
    /// survives the window's own load pass (which re-seeds it) — a pad only re-reports on a change, so a
    /// dropped push would leave the row blank until the level next moved.</summary>
    public void SetBattery(int percent, bool charging)
    {
        _batteryPercent  = percent;
        _batteryCharging = charging;
        if (IsLoaded) SystemEditor.SetBattery(percent, charging);
    }

    /// <summary>Select the About tab (kept last in the XAML). Used by the tray "About" item.</summary>
    public void ShowAbout() => Tabs.SelectedIndex = Tabs.Items.Count - 1;

    /// <summary>Select the Advanced tab — the "open Settings at Advanced" entry point (onboarding's
    /// finish-step D-Pad card lands here). By name, not index: SafeMode's tab position isn't load-bearing.</summary>
    public void ShowAdvanced() => Tabs.SelectedItem = AdvancedTab;

    /// <summary>Open the Help tab, optionally jumped to a topic — the tray "Help" item and the in-app
    /// "Learn more" links land here.</summary>
    public void ShowHelpTopic(string? topicId = null)
    {
        // A topic deep-link (a Help chip) remembers the tab it came from and shows the Help pane's
        // ◀ back button; a plain open (tray Help item / clicking the Help tab) shows no back button.
        bool viaLink = topicId is not null && !ReferenceEquals(Tabs.SelectedItem, HelpTab);
        if (viaLink) _helpReturnTab = Tabs.SelectedItem;
        _helpViaLink = true;                       // suppress the SelectionChanged reset for this switch
        Tabs.SelectedItem = HelpTab;
        _helpViaLink = false;
        HelpEditor.SetBackVisible(viaLink);
        if (topicId is not null) HelpEditor.SelectTopic(topicId);
        if (viaLink) HelpEditor.FocusTopic();   // land the reader on the content, not the pane chrome
    }

    private object? _helpReturnTab;   // the tab a Help chip jumped from (◀ returns here)
    private bool _helpViaLink;        // true while ShowHelpTopic switches tabs programmatically
    private bool _tabRevert;          // true while a refused tab switch is being put back (re-entrant guard)

    private void HelpBack()
    {
        HelpEditor.SetBackVisible(false);
        if (_helpReturnTab is not null) Tabs.SelectedItem = _helpReturnTab;
    }

    /// <summary>Re-render Help with the live token bindings, in the language the Advanced tab shows (a pick not yet
    /// saved must still win here, or a controller-kind change would re-render in the old language).</summary>
    private void ReloadHelp() => HelpEditor.Load(ResolveHelpToken, SystemEditor.SelectedLanguage ?? Loc.Lang);

    /// <summary>The user picked a language on the Advanced tab (already marked dirty through its Changed). Help
    /// follows at once; the rest of the app is fixed per run (Loc.Init), so offer the restart now, saving first so
    /// the debounced write cannot lose the pick to the restart.</summary>
    private void OnLanguageChanged(string code)
    {
        HelpEditor.SetLanguage(code);
        if (code != Loc.Lang && LanguageRestartWindow.Ask(this, code))
        {
            if (!Save()) return;
            RestartApp(reopenSettings: true);
        }
    }

    /// <summary>Live values for the Help topics' {tokens}: the user's ACTUAL invocation chord for the
    /// detected controller, its enable/disable chord, and face-button glyphs in the active set (PS symbols
    /// vs Xbox letters). Unknown tokens → null (HelpEditorControl falls back to the generic doc values).
    /// <para>The chord tokens read the whole enabled SET, not the first entry: with several chords
    /// configured, naming one would tell the user the others don't work — see
    /// <see cref="TriggerModes.DescribeChoice"/>.</para></summary>
    private string? ResolveHelpToken(string token)
    {
        bool xbox = ControllerButtons.Set == ControllerGlyphSet.Xbox;
        return token switch
        {
            "invoke"   => TriggerModes.DescribeChoice(_config.Current.System.TriggerModesFor(_detectedKind)),
            "disable"  => TriggerModes.EnableDisableChoice(_config.Current.System.TriggerModesFor(_detectedKind)),
            "cross"    => xbox ? "A" : "✕",
            "circle"   => xbox ? "B" : "○",
            "square"   => xbox ? "X" : "☐",
            "triangle" => xbox ? "Y" : "△",
            _          => null,
        };
    }

    private (bool Right, int Index)? _pendingEdit;

    /// <summary>Jump to a wheel's editor (tab 0 = Left/A, 1 = Right/B) with a specific slice selected —
    /// used when firing an unconfigured wheel slice sends the user here to finish setting it up. Deferred
    /// until the editors have loaded their slices (LoadFromConfig runs on the window's Loaded event).</summary>
    public void EditSlice(bool rightWheel, int sliceIndex)
    {
        _pendingEdit = (rightWheel, sliceIndex);
        if (IsLoaded) ApplyPendingEdit();
    }

    private void ApplyPendingEdit()
    {
        if (_pendingEdit is not { } p) return;
        _pendingEdit = null;
        Tabs.SelectedIndex = p.Right ? 1 : 0;
        (p.Right ? WheelBEditor : WheelAEditor).SelectSlice(p.Index);
    }

    /// <summary>The dropped paths when EVERY one is package-shaped (a folder or a .zip); otherwise null, and
    /// the drag belongs to whatever control is under the pointer.</summary>
    private static string[]? DroppedPackagePaths(System.Windows.DragEventArgs e) =>
        e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
        && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] { Length: > 0 } paths
        && paths.All(PackageInstaller.IsCandidate)
            ? paths : null;

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        // Restart the debounce window on every change
        _saveTimer.Stop();
        _saveTimer.Start();
        SetStatus(Loc.T(UiText.Settings.UnsavedChanges), autoHide: false);
        // Adding/removing slices can cross the Thick limit, so keep the Customize tab's thickness picker in
        // step with the live counts (it hides Thick and demotes a Thick selection past the limit) — not just
        // on window open. Save() re-applies the rule authoritatively either way.
        PushSliceCountsToCustomize();
    }

    private void PushSliceCountsToCustomize() =>
        CustomizeEditor.SetSliceCounts(WheelAEditor.GetSlices().Length, WheelBEditor.GetSlices().Length);

    /// <summary>Shows (or hides, for an empty string) the bottom-right status badge. Transient messages
    /// (the default) auto-hide after a few seconds via <see cref="_statusHideTimer"/>; persistent states
    /// like "Unsaved changes…" pass autoHide: false so they stick until the next call replaces them.</summary>
    private void SetStatus(string text, bool autoHide = true)
    {
        _statusHideTimer.Stop();
        StatusText.Text = text;
        StatusBadge.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        if (autoHide && !string.IsNullOrEmpty(text)) _statusHideTimer.Start();
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    private void LoadFromConfig(AppConfig cfg)
    {
        CustomColorStore.Load(cfg.CustomColors);   // shared picker palette (before editors load)
        WheelAEditor.Load(cfg.WheelA);
        WheelBEditor.Load(cfg.WheelB);
        _systemBase   = cfg.System;   // pass-through baseline; the editor tabs layer their fields onto it (see Save)
        LoadSystemTabs(cfg.System);
        PushSliceCountsToCustomize();   // after both wheel editors have loaded — drives the Thick tile
        // ActionColors/ActionIcons: no editor writes these — just hold whatever an existing config carries
        // so Save round-trips it unchanged. See the fields' declaration below.
        _actionColors = cfg.ActionColors is null ? new() : new(cfg.ActionColors);
        _actionIcons  = cfg.ActionIcons  is null ? new() : new(cfg.ActionIcons);
        SetStatus("");
    }

    /// <summary>Push a SystemConfig into every control that owns part of it (Customize, Advanced,
    /// Exceptions) and the wheel editors' system-derived settings.</summary>
    private void LoadSystemTabs(SystemConfig sys)
    {
        PushObsConfigured(sys);   // drives the slice editors' one-time-setup prompt (G13.A)
        CustomizeEditor.ControllerConnected = _controllerConnected;
        CustomizeEditor.Load(sys, _detectedKind);
        SystemEditor.Load(sys);
        SystemEditor.SetControllerKind(_detectedKind, _controllerConnected, _controllerTransport);
        SystemEditor.SetBattery(_batteryPercent, _batteryCharging);
        ExceptionsEditor.Load(sys);
        ReloadHelp();
        WheelAEditor.MaxSlices = WheelBEditor.MaxSlices = SystemConfig.MaxSlicesPerWheel;
        WheelAEditor.SliceMaterial = WheelBEditor.SliceMaterial = sys.SliceMaterial;   // icon-well preview tone
        // The slice editors' "Show label" checkbox only exists in the per-slice mode — pushed like
        // SliceMaterial above, and re-pushed in Save so a Customize change takes effect without a reopen.
        WheelAEditor.ShowSliceLabelsMode = WheelBEditor.ShowSliceLabelsMode = sys.ShowSliceLabels;
    }

    /// <summary>Tell both slice editors whether the shared OBS connection is set up — an unset one shows the
    /// one-time-setup prompt instead of nothing. A legacy config that saved a password before ObsConfigured
    /// existed counts as configured, matching App's configure-on-fire gate.</summary>
    private void PushObsConfigured(SystemConfig cfg) =>
        WheelAEditor.ObsConfigured = WheelBEditor.ObsConfigured =
            cfg.ObsConfigured || !string.IsNullOrEmpty(cfg.ObsPassword);

    private bool _saveFailed;

    /// <summary>Write any debounced edit now. The Test buttons hand the host a request that reads the live
    /// config (the wheel's slices, the executor's settings), so they must not run ahead of the 500 ms save
    /// timer — a slice deleted a moment ago would still be drawn.</summary>
    private void FlushPendingSave()
    {
        if (!_saveTimer.IsEnabled) return;
        _saveTimer.Stop();
        Save();
    }

    private bool Save()
    {
        var wheelA = WheelAEditor.GetSlices();
        var wheelB = WheelBEditor.GetSlices();
        // Layer this save onto the LIVE SystemConfig, not the window-open snapshot. OnConfigReloadedExternal
        // refreshes _systemBase for us, but ONLY while nothing is dirty — so an App-side write that lands
        // while a Settings edit is pending (the 2 s save timer running: controller-kind memory, OobeVersion,
        // GameGridTipSeen, the tray's Passthru Mode toggle, an onboarding re-run writing SystemConfig) was
        // dropped, and this save then reverted it. Every tab's ApplyTo is a `with` over the fields IT owns,
        // so starting from Current keeps those foreign writes and still lets the user's visible edits win.
        _systemBase = _config.Current.System;
        var updated = new AppConfig
        {
            WheelA = wheelA,
            WheelB = wheelB,
            // The Customize + Advanced + Exceptions tabs each fold their own fields onto the loaded baseline
            // (via `with`), so pass-through fields (Developer-window knobs, OobeVersion, controller-kind
            // memory, …) survive.
            // The shared OBS connection is the Advanced tab's (Integrations owns its setup pane).
            // …then the Thick→Medium rule has the last word, from the slice counts being written right here.
            // The picker already applies it in the UI, but this covers a wheel edited on another tab.
            // (Restored backups and hand-edited files are covered separately, on the READ path —
            // ConfigLoader.Sanitize applies the same rule.)
            System = SliceThicknessRule.Apply(
                         ExceptionsEditor.ApplyTo(SystemEditor.ApplyTo(CustomizeEditor.ApplyTo(_systemBase))),
                         wheelA.Length, wheelB.Length),
            ActionColors = _actionColors,
            ActionIcons  = _actionIcons,
            CustomColors = CustomColorStore.ToHex(),
        };
        if (!_config.WriteConfig(updated))
        {
            _saveFailed = true;
            SetStatus(Loc.F(UiText.Settings.SaveFailed, _config.LastWriteError ?? ""), autoHide: false);
            return false;
        }
        _saveFailed = false;
        // Each editor re-bases the fields the user just wrote; a field it left alone stays reported by
        // ChangedOutside until the echo's refresh re-Loads the control.
        ExceptionsEditor.NoteSaved(updated.System);
        SystemEditor.NoteSaved(updated.System);
        CustomizeEditor.NoteSaved(updated.System);
        PushObsConfigured(updated.System);   // an OBS slice's setup prompt clears without a reopen
        // Live-refresh the Wheel tabs' icon-well material so a Customize material change shows without a
        // window reopen (LoadFromConfig only pushes it at open).
        WheelAEditor.SliceMaterial = WheelBEditor.SliceMaterial = updated.System.SliceMaterial;
        WheelAEditor.ShowSliceLabelsMode = WheelBEditor.ShowSliceLabelsMode = updated.System.ShowSliceLabels;
        WheelAEditor.MaxSlices    = WheelBEditor.MaxSlices    = SystemConfig.MaxSlicesPerWheel;
        // The rule above may have moved thickness (or restored Thick) behind the picker's back — re-seed it
        // so the tiles show what was actually saved. Load() is guarded by its own _loading flag, so this
        // can't loop back into another save.
        CustomizeEditor.ReloadThickness(updated.System);
        PushSliceCountsToCustomize();
        // Help's {invoke}/{disable} tokens name the configured chord(s), which the Customize tab can change
        // — and the external-reload path skips our own saves, so re-render here or Help would keep showing
        // the window-open chord set (including the singular-vs-plural phrasing) until Settings was reopened.
        ReloadHelp();
        // Auto-hides a few seconds after showing; the next edit flips it to "Unsaved changes…" sooner anyway.
        SetStatus(Loc.T(UiText.Settings.Saved));
        return true;
    }

    // ActionColors/ActionIcons: per-action-type icon/tint OVERRIDES an existing config may carry. Nothing
    // edits these maps; SettingsWindow holds whatever was loaded and writes it straight back on Save so an
    // upgraded install doesn't silently lose a prior override (ActionTint.SetOverrides/ActionIcons.SetOverrides
    // still apply them at load — see App.xaml.cs). ActionTint.Defaults / SystemCommandDefaults /
    // AddMenuIcons / MonoIcons.DefaultMaterialGlyph are the sole source of truth.
    private Dictionary<string, string> _actionColors = new();
    private Dictionary<string, string> _actionIcons  = new();
    // The loaded SystemConfig; the Customize + Advanced tabs layer their fields onto it on Save.
    private SystemConfig _systemBase = new();

    // ── Button handlers ───────────────────────────────────────────────────────

    /// <summary>About-tab "Open-source licenses" link: show the embedded THIRD-PARTY-LICENSES.md in a simple
    /// read-only viewer (embedded so it's present in the single-file exe).</summary>
    private void OpenLicenses_Click(object sender, RoutedEventArgs e) =>
        ShowEmbeddedText("THIRD-PARTY-LICENSES.md", Loc.T(UiText.Settings.LicensesTitle));

    /// <summary>About-tab "view full license" link: the embedded repo-root LICENSE (GPL-3 plus the §7 terms).</summary>
    private void OpenGplLicense_Click(object sender, RoutedEventArgs e) =>
        ShowEmbeddedText("LICENSE", Loc.T(UiText.Settings.GplLicenseTitle));

    private void ShowEmbeddedText(string resourceName, string title)
    {
        string text;
        try
        {
            using var s = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName);
            if (s is null) { Trace.WriteLine($"[About] {resourceName} resource missing"); return; }
            using var r = new System.IO.StreamReader(s);
            text = r.ReadToEnd();
        }
        catch (Exception ex) { Trace.WriteLine($"[About] licenses read failed: {ex.Message}"); return; }

        var box = new System.Windows.Controls.TextBox
        {
            Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12,
            Padding = new Thickness(16),
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };
        new System.Windows.Window
        {
            Title = title, Owner = this,
            Width = 700, Height = 640, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White, Content = box,
        }.ShowDialog();
    }

    /// <summary>Open a hyperlink's target in the default browser (the About tab's links).</summary>
    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { Trace.WriteLine($"[About] link open failed: {ex.Message}"); }
        e.Handled = true;
    }

    /// <summary>Clicking anywhere in the About tab's tip-jar blurb opens the Tip Jar. The inner Hyperlink
    /// marks its own click Handled, so this never double-fires.</summary>
    private void TipJarCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        try { Process.Start(new ProcessStartInfo(Loc.TipUrl) { UseShellExecute = true }); }
        catch (Exception ex) { Trace.WriteLine($"[About] tip jar open failed: {ex.Message}"); }
        e.Handled = true;
    }

    /// <summary>"Email Log to Developer…": zip the diagnostic logs to the user's Desktop (the live log is
    /// write-locked by our own trace listener, so copy with FileShare.ReadWrite), reveal the zip in
    /// Explorer, and open a pre-addressed mail draft — mailto can't attach files, so the body tells the
    /// user to attach the zip that was just revealed.</summary>
    private void EmailLog()
    {
        try
        {
            var zipPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"Radiata-log-{DateTime.Now:yyMMdd-HHmm}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            // Every generation the rotation keeps (App.TraceGenerations), plus the legacy ".old" name from
            // before the app's rename. A fault reported today often started in an earlier session, so the
            // rotated generations are the point of the bundle.
            string[] names =
            [
                "radiata-trace.log",
                "radiata-trace.log.1", "radiata-trace.log.2", "radiata-trace.log.3",
                "radiata-trace.log.old",
                "radiata-trace-elevated.log",
            ];
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                foreach (var name in names)
                {
                    var p = Path.Combine(AppPaths.AppDataDir, name);
                    if (!File.Exists(p)) continue;
                    using var src = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var dst = zip.CreateEntry(name).Open();
                    src.CopyTo(dst);
                }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });
            var mailto = "mailto:contact@jesseholden.com"
                + "?subject=" + Uri.EscapeDataString(Loc.F(UiText.Settings.LogSubject, _version))
                + "&body=" + Uri.EscapeDataString(
                    Loc.F(UiText.Settings.LogBodyAttach, Path.GetFileName(zipPath)) + "\r\n\r\n"
                    + Loc.T(UiText.Settings.LogBodyWhat) + "\r\n\r\n"
                    // Support is English-only: say so where a non-English user would
                    // otherwise write in their own language.
                    + (Loc.Lang == HelpLocalization.DefaultCode ? "" : Loc.T(UiText.Settings.SupportEnglishOnly) + "\r\n\r\n"));
            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            SetStatus(Loc.F(UiText.Settings.LogSaved, Path.GetFileName(zipPath)));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Settings] email log failed: {ex.Message}");
            SetStatus(Loc.T(UiText.Settings.LogPackageFailed));
        }
    }

    /// <summary>Flush the downloaded cover-art cache so covers re-fetch (and missed ones retry). Images the
    /// USER dropped onto a slice are passed through as keepers — they can't be re-downloaded, so clearing
    /// the cache must not destroy them (orphaned ones, no longer on any slice, still go).</summary>
    private void ClearArtCache()
    {
        var inUse = _config.Current.WheelA.Concat(_config.Current.WheelB)
                          .Select(s => s.LogoPath)
                          .Where(p => !string.IsNullOrWhiteSpace(p))
                          .Select(p => p!)
                          .ToList();
        int n = GameArt.ClearCache(inUse);
        SetStatus(Loc.P(UiText.Settings.CacheClearedOne, UiText.Settings.CacheClearedOther, n));
    }

    /// <summary>Retry only the previously-missed covers, keeping cached + manually-chosen art.</summary>
    private void RetryMissedArt()
    {
        int n = GameArt.RetryMissedArt();
        SetStatus(n == 0 ? Loc.T(UiText.Settings.NoMissingCovers)
                       : Loc.P(UiText.Settings.WillRetryOne, UiText.Settings.WillRetryOther, n));
    }

    /// <summary>The default backup folder: Documents\Radiata Backups — deliberately OUTSIDE
    /// %APPDATA%\Radiata so "Wipe App Data and Reset" never destroys the backups. Falls back to
    /// Documents if the subfolder can't be created.</summary>
    private static string BackupDefaultDir()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir  = Path.Combine(docs, "Radiata Backups");
        try { Directory.CreateDirectory(dir); return dir; }
        catch { return docs; }
    }

    /// <summary>Flush pending edits, then bundle config.json + the Game Grid art/logo picks
    /// (cover-overrides.json) into a single .zip the user chooses. Restore reads this back (and still
    /// accepts a legacy bare-config .json).</summary>
    private void BackUpSettings()
    {
        _saveTimer.Stop();
        if (!Save()) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = Loc.T(UiText.Settings.BackupTitle),
            Filter   = Loc.T(UiText.Settings.BackupFilter),
            // Timestamp the default name so successive backups don't overwrite each other.
            FileName = $"Radiata-settings-backup-{DateTime.Now:yyyy-MM-dd_HHmmss}.zip",
            // Default OUTSIDE %APPDATA%\Radiata so "Wipe App Data and Reset" can never take the
            // backups with it.
            InitialDirectory = BackupDefaultDir(),
        };
        if (dlg.ShowDialog(this) != true) return;
        // Build the zip at a SIBLING temp path and move it into place only after it closes cleanly —
        // writing straight to the destination would let a full disk / IO error / mid-write kill destroy
        // the previous good backup. Move-with-overwrite on the same volume is atomic enough that the
        // destination is only ever the old complete file or the new complete file. Not AtomicFile: the
        // archive is written incrementally to the temp path by ZipArchive, not built as an in-memory
        // byte buffer first.
        var tmpZip = dlg.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        int artSkipped = 0;
        long artBytes = 0;
        try
        {
            using (var zip = System.IO.Compression.ZipFile.Open(tmpZip, System.IO.Compression.ZipArchiveMode.Create))
            {
                if (File.Exists(ConfigLoader.ConfigPath)) zip.CreateEntryFromFile(ConfigLoader.ConfigPath, "config.json");
                if (File.Exists(GameMetadata.OverridePath))    zip.CreateEntryFromFile(GameMetadata.OverridePath, "cover-overrides.json");
                // Bundle the actual cover/logo IMAGE files, so a restore — even after a Wipe or onto another
                // machine — brings the art back, not just the (soon-stale) paths in config.json /
                // cover-overrides.json. Two rules:
                //   • The archive is budgeted to the RESTORE caps (256 MB total / 32 MB per file), so a
                //     backup that reports success can always be restored in full. Slice logos (irreplaceable
                //     user picks — the cache ones can't all be re-downloaded either, e.g. usericon_*) are
                //     added FIRST; the regenerable cache art fills whatever budget remains.
                //   • Two different files sharing a leaf filename don't silently collapse to one entry:
                //     the collision gets a content-hash-prefixed entry name, and art-manifest.json maps each
                //     slice LogoPath to its archived name so restore repoints to the right image.
                artSkipped = BundleBackupArt(zip, out artBytes);
            }
            File.Move(tmpZip, dlg.FileName, overwrite: true);   // the zip is closed and complete — commit it
            SetStatus(Loc.T(UiText.Settings.BackedUp));
            // No counts in this dialog — the number of skipped images is noise; say only what happens to
            // what was left out. Shown only AFTER the move above: this copy says "backed up", so it must
            // not appear on a run the catch below calls a failure.
            // A backup that skipped nothing but carries at least half the restore art budget is the
            // other reason the file is big; say so, so the size isn't a surprise.
            if (artSkipped > 0)
                System.Windows.MessageBox.Show(this,
                    Loc.T(UiText.Settings.BackupArtNote),
                    "Radiata", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (artBytes >= LargeBackupArtBytes)
                System.Windows.MessageBox.Show(this,
                    Loc.T(UiText.Settings.BackupSizeNote),
                    "Radiata", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Backup] failed: {ex}");
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { /* best-effort */ }
            System.Windows.MessageBox.Show(this, Loc.T(UiText.Settings.BackupFailed), "Radiata",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Add the art payload to a backup zip under the restore budget. Slice logos first (the
    /// user's own picks), then the cache art; leaf-name collisions between DIFFERENT files get a
    /// content-hash-prefixed entry name instead of being dropped. Writes art-manifest.json mapping each
    /// slice LogoPath → its archived entry leaf (restore's repointing consults it before falling back to
    /// plain leaf matching). Returns how many images were skipped for budget reasons.</summary>
    private int BundleBackupArt(System.IO.Compression.ZipArchive zip, out long archivedBytes)
    {
        int skipped = 0;
        archivedBytes = 0;
        try
        {
            // Candidates in priority order, deduped by full path: slice logos, then the cache.
            var candidates = new List<string>();
            var seenPaths  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddCandidate(string? p)
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                try { p = Path.GetFullPath(p); } catch { return; }
                if (File.Exists(p) && seenPaths.Add(p)) candidates.Add(p);
            }
            foreach (var s in _config.Current.WheelA) AddCandidate(s.LogoPath);
            foreach (var s in _config.Current.WheelB) AddCandidate(s.LogoPath);
            if (Directory.Exists(GameArt.CacheDirectory))
                foreach (var f in Directory.EnumerateFiles(GameArt.CacheDirectory))
                {
                    var ext = Path.GetExtension(f);   // images only — skip the regenerable url caches + ".miss" markers
                    if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                        AddCandidate(f);
                }

            var entryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // source path → entry leaf
            var leafTaken  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var f in candidates)
            {
                long len;
                try { len = new FileInfo(f).Length; } catch { skipped++; continue; }
                if (len > MaxRestoredArtEntryBytes) { skipped++; Trace.WriteLine($"[Backup] skipped '{f}' (over the {MaxRestoredArtEntryBytes / 1024 / 1024} MB per-file restore cap)"); continue; }
                if (total + len > MaxRestoredArtTotalBytes) { skipped++; continue; }   // budget spent — keep counting

                string leaf = Path.GetFileName(f);
                if (!leafTaken.Add(leaf))
                {
                    // Same leaf, different file: disambiguate with a short content hash so neither image is lost.
                    string prefixed;
                    try
                    {
                        using var fs = File.OpenRead(f);
                        prefixed = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs))[..8].ToLowerInvariant() + "-" + leaf;
                    }
                    catch { skipped++; continue; }
                    if (!leafTaken.Add(prefixed)) continue;   // identical content already archived — true dedupe
                    leaf = prefixed;
                }
                try { zip.CreateEntryFromFile(f, "art/" + leaf); }
                catch (Exception ex) { skipped++; Trace.WriteLine($"[Backup] couldn't add '{f}': {ex.Message}"); leafTaken.Remove(leaf); continue; }
                total += len;
                archivedBytes = total;
                entryNames[f] = leaf;
            }

            // Manifest: only slice LogoPaths matter for repointing — keyed by the path exactly as the
            // config stores it, so restore can look it up without normalization guesswork.
            var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void MapSlices(WheelSlice[] slices)
            {
                foreach (var s in slices)
                    if (!string.IsNullOrWhiteSpace(s.LogoPath))
                        try
                        {
                            if (entryNames.TryGetValue(Path.GetFullPath(s.LogoPath), out var leaf))
                                manifest[s.LogoPath] = leaf;
                        }
                        catch (Exception ex) { Trace.WriteLine($"[Backup] art-manifest mapping skipped '{s.LogoPath}': {ex.Message}"); }
            }
            MapSlices(_config.Current.WheelA);
            MapSlices(_config.Current.WheelB);
            if (manifest.Count > 0)
            {
                var entry = zip.CreateEntry("art-manifest.json");
                using var w = new StreamWriter(entry.Open());
                w.Write(System.Text.Json.JsonSerializer.Serialize(manifest));
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Backup] art bundle failed: {ex.Message}"); }
        return skipped;
    }

    // ── Reading the art/ payload out of a backup zip, treating the zip as HOSTILE ────────────────────
    // A "backup" is a file the user was handed — off a forum, a friend, a Discord DM — so every field in
    // it is attacker-controlled. Two traps:
    //
    // (1) ZipArchiveEntry.Name is NOT a bare filename. It splits FullName using a separator chosen by the
    //     archive's own "version made by" platform byte: a UNIX-made zip splits only on '/', so backslash
    //     paths (`art/..\..\…\Startup\x.lnk`) survive in Name, and Path.Combine DISCARDS its base whenever
    //     the second part is rooted (`art/C:\Windows\Temp\x.dll` writes straight to C:). Forward-slash
    //     traversal is NOT the risky form — stripping only "../" misses this entirely.
    //     Defence: derive the leaf ourselves with Path.GetFileName (Windows semantics, so BOTH separators
    //     count), then re-verify the resolved path really is inside the art cache.
    // (2) The read happens BEFORE the user's confirmation prompt, so merely picking a file allocates
    //     whatever the zip declares. Hence the per-entry, total, and count budgets.

    private const long MaxRestoredArtEntryBytes = 32L * 1024 * 1024;    // one image
    private const long MaxRestoredArtTotalBytes = 256L * 1024 * 1024;   // the whole art/ payload
    private const long LargeBackupArtBytes      = MaxRestoredArtTotalBytes / 2;   // art payload that earns the size note
    private const int  MaxRestoredArtEntries    = 4000;                 // ~8x a real 498-file cache
    private const long MaxRestoredJsonBytes     = 8L  * 1024 * 1024;    // config/cover-overrides (real ones are KBs)

    /// <summary>Read a zip entry as UTF-8 text with BOTH the declared and the actual decompressed size
    /// capped at <see cref="MaxRestoredJsonBytes"/> — an uncapped JSON entry would let a crafted "backup"
    /// force an OOM before the user even confirmed. Null = over budget (the caller treats it as
    /// entry-absent).</summary>
    private static string? ReadBoundedText(System.IO.Compression.ZipArchiveEntry e)
    {
        if (e.Length > MaxRestoredJsonBytes)
        {
            Trace.WriteLine($"[Restore] skipped '{e.FullName}' ({e.Length / 1024 / 1024} MB declared — not a settings file)");
            return null;
        }
        using var es = e.Open();
        using var ms = new MemoryStream();
        var buf = new byte[81920];
        int read;
        while ((read = es.Read(buf, 0, buf.Length)) > 0)
        {
            if (ms.Length + read > MaxRestoredJsonBytes)
            {
                Trace.WriteLine($"[Restore] skipped '{e.FullName}' (decompressed past the {MaxRestoredJsonBytes / 1024 / 1024} MB cap)");
                return null;
            }
            ms.Write(buf, 0, read);
        }
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;   // System.Text.Json rejects a BOM
    }

    private static readonly string[] RestorableArtExts = [".png", ".jpg", ".jpeg"];

    /// <summary>Bundled art/ images from a backup zip, keyed by SAFE leaf filename. Anything that tries to
    /// escape the art cache, isn't an image, or blows a size budget is skipped with a trace line rather
    /// than aborting the whole restore — one hostile or corrupt entry shouldn't cost the user their
    /// settings. <paramref name="skipped"/> counts every entry not restored (size caps, refused names),
    /// so the caller can tell the user instead of truncating silently.</summary>
    private static Dictionary<string, byte[]>? ReadBundledArt(System.IO.Compression.ZipArchive zip, out int skipped)
    {
        skipped = 0;
        int artEntriesTotal = zip.Entries.Count(e =>
            e.FullName.StartsWith("art/", StringComparison.OrdinalIgnoreCase) && e.Length > 0);
        Dictionary<string, byte[]>? artFiles = null;
        string cacheRoot = Path.GetFullPath(GameArt.CacheDirectory)
                               .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long total = 0;
        int count = 0;

        foreach (var e in zip.Entries)
        {
            if (!e.FullName.StartsWith("art/", StringComparison.OrdinalIgnoreCase)) continue;

            // Never trust e.Name — take the leaf from FullName ourselves, which treats '\' as a separator
            // too, so a UNIX-made zip can't smuggle a relative path through.
            string leaf = Path.GetFileName(e.FullName.Replace('/', Path.DirectorySeparatorChar));
            if (leaf.Length == 0) continue;                       // a directory entry
            if (leaf is "." or "..") { skipped++; Trace.WriteLine($"[Restore] skipped art entry '{e.FullName}' (not a file)"); continue; }
            if (!RestorableArtExts.Contains(Path.GetExtension(leaf), StringComparer.OrdinalIgnoreCase))
            {
                skipped++;
                Trace.WriteLine($"[Restore] skipped art entry '{e.FullName}' (not a .png/.jpg image)");
                continue;
            }

            // Belt-and-braces: the leaf should be inert by now, but prove the destination is inside the
            // cache rather than assuming it. Catches any Path oddity (reserved names, trailing dots).
            string resolved;
            try { resolved = Path.GetFullPath(Path.Combine(GameArt.CacheDirectory, leaf)); }
            catch (Exception ex) { skipped++; Trace.WriteLine($"[Restore] skipped art entry '{e.FullName}' ({ex.GetType().Name})"); continue; }
            if (!resolved.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                Trace.WriteLine($"[Restore] REFUSED art entry '{e.FullName}' — resolves outside the art cache ({resolved})");
                continue;
            }

            if (++count > MaxRestoredArtEntries)
            {
                skipped += Math.Max(1, artEntriesTotal - (artFiles?.Count ?? 0) - skipped);   // everything not yet loaded
                Trace.WriteLine($"[Restore] stopped after {MaxRestoredArtEntries} art entries");
                break;
            }
            // Declared length first (cheap and usually honest); the copy below is bounded regardless.
            if (e.Length > MaxRestoredArtEntryBytes)
            {
                skipped++;
                Trace.WriteLine($"[Restore] skipped art entry '{e.FullName}' ({e.Length / 1024 / 1024} MB declared)");
                continue;
            }

            byte[] bytes;
            try
            {
                using var es = e.Open();
                using var ms = new MemoryStream();
                // Bounded copy: a zip bomb's declared length can lie, so cap what we actually read rather
                // than trusting e.Length.
                var buf = new byte[81920];
                int read;
                while ((read = es.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + read > MaxRestoredArtEntryBytes) { ms.SetLength(0); break; }
                    ms.Write(buf, 0, read);
                }
                if (ms.Length == 0) { skipped++; Trace.WriteLine($"[Restore] skipped art entry '{e.FullName}' (empty or over the per-file cap)"); continue; }
                bytes = ms.ToArray();
            }
            catch (Exception ex) { skipped++; Trace.WriteLine($"[Restore] skipped art entry '{e.FullName}' ({ex.Message})"); continue; }

            total += bytes.Length;
            if (total > MaxRestoredArtTotalBytes)
            {
                skipped += Math.Max(1, artEntriesTotal - (artFiles?.Count ?? 0) - skipped);   // this one + everything after it
                Trace.WriteLine($"[Restore] stopped — art payload exceeded {MaxRestoredArtTotalBytes / 1024 / 1024} MB");
                break;
            }
            (artFiles ??= new(StringComparer.OrdinalIgnoreCase))[leaf] = bytes;
        }
        return artFiles;
    }

    /// <summary>Load settings from a chosen backup file (validated with the live config's parser),
    /// replace the current config, then restart. Pairs with Back Up so a wipe/OOBE test is reversible.</summary>
    private void RestoreSettings()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = Loc.T(UiText.Settings.RestoreTitle),
            Filter = Loc.T(UiText.Settings.RestoreFilter),
            InitialDirectory = BackupDefaultDir(),
        };
        if (dlg.ShowDialog(this) != true) return;

        AppConfig? parsed;
        string? coverJson = null;   // Game Grid art/logo picks from a bundle — applied after the user confirms
        Dictionary<string, byte[]>? artFiles = null;   // bundled slice logo files — restored after confirm
        Dictionary<string, string>? artManifest = null;   // slice LogoPath → archived entry leaf
        int artSkipped = 0;
        // Wait cursor over both heavy phases: reading a bundle decompresses up to the 256 MB art budget,
        // and applying one writes it back out — both on the UI thread, so a big backup reads as a hang.
        // A full fix would move this off-thread with progress/cancel.
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            if (dlg.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(dlg.FileName);
                string? json = zip.GetEntry("config.json") is { } cfgEntry ? ReadBoundedText(cfgEntry) : null;
                parsed = json is not null && LooksLikeRadiataBackup(json) ? ConfigLoader.TryParse(json) : null;
                if (zip.GetEntry("cover-overrides.json") is { } cov) coverJson = ReadBoundedText(cov);
                // Read the bundled slice logo files into memory now (the zip closes before the user confirms);
                // they're written into the art cache after confirmation.
                artFiles = ReadBundledArt(zip, out artSkipped);
                artManifest = ReadArtManifest(zip);
            }
            else
            {
                // Legacy bare-config backup (pre-bundle .json). Size-checked first: a real config is a few
                // KB, and this is a user-chosen file that may not be one at all.
                var fi = new FileInfo(dlg.FileName);
                if (fi.Length > MaxRestoredArtEntryBytes)
                {
                    System.Windows.MessageBox.Show(this,
                        Loc.F(UiText.Settings.RestoreTooLarge, fi.Length / 1024 / 1024),
                        "Radiata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var json = SecretField.ReadBackup(File.ReadAllText(dlg.FileName));
                // TryParse deserializes ANY JSON object into a default-populated AppConfig — "{}" would
                // "restore" a factory-blank config — so also require the top-level shape every
                // Radiata-written config has before accepting the file as a backup.
                parsed = LooksLikeRadiataBackup(json) ? ConfigLoader.TryParse(json) : null;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Restore] couldn't read '{dlg.FileName}': {ex}");
            System.Windows.MessageBox.Show(this, Loc.T(UiText.Settings.RestoreCantRead), "Radiata",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = null; }   // normal cursor for the dialogs below
        if (parsed is null)
        {
            System.Windows.MessageBox.Show(this, Loc.T(UiText.Settings.RestoreNotBackup), "Radiata",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string prompt = Loc.T(UiText.Settings.RestorePrompt);
        if (artSkipped > 0)
            prompt += "\n\n" + Loc.P(UiText.Settings.RestoreArtNoteOne, UiText.Settings.RestoreArtNoteOther, artSkipped);
        prompt += "\n\n" + Loc.T(UiText.Settings.WillRestart);
        var choice = System.Windows.MessageBox.Show(this, prompt,
            Loc.T(UiText.Settings.RestoreCaption), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes) return;

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        _saveTimer.Stop();
        try
        {
            using (var restoreFiles = new FileRestoreTransaction(AppPaths.AppDataDir))
            {
                if (artFiles is not null)
                {
                    string cacheRoot = Path.GetFullPath(GameArt.CacheDirectory)
                        .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    foreach (var (name, bytes) in artFiles)
                    {
                        string dest = Path.GetFullPath(Path.Combine(cacheRoot, name));
                        if (!dest.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("An artwork destination is outside the cache.");
                        restoreFiles.WriteBytes(dest, bytes);
                    }
                }
                if (coverJson is not null)
                    restoreFiles.WriteText(GameMetadata.OverridePath, RepointRestoredCovers(coverJson, artManifest));

                parsed = new AppConfig
                {
                    WheelA = RepointRestoredLogos(parsed.WheelA, artManifest),
                    WheelB = RepointRestoredLogos(parsed.WheelB, artManifest),
                    ActionColors = parsed.ActionColors,
                    ActionIcons = parsed.ActionIcons,
                    CustomColors = parsed.CustomColors,
                    System = parsed.System with
                    {
                        OobeVersion = Math.Max(parsed.System.OobeVersion, _config.Current.System.OobeVersion),
                    },
                };
                if (!_config.WriteConfig(parsed))
                    throw new IOException(_config.LastWriteError ?? "Could not write settings.");
                restoreFiles.Commit();
            }
            _saveFailed = false;
            _restarting = true;
            RestartApp(reopenSettings: true);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Restore] failed: {ex.Message}");
            System.Windows.MessageBox.Show(this, Loc.T(UiText.Settings.RestoreFailed),
                "Radiata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = null; }
    }

    private static string RepointRestoredCovers(string json, Dictionary<string, string>? manifest)
    {
        var map = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject
            ?? throw new InvalidDataException("Cover selections must be a JSON object.");
        foreach (var item in map.ToList())
        {
            if (item.Value is System.Text.Json.Nodes.JsonValue legacy && legacy.TryGetValue<string>(out var path))
                map[item.Key] = RestoredArtPath(path, manifest);
            else if (item.Value is System.Text.Json.Nodes.JsonObject pick)
                foreach (var field in pick.ToList())
                    if (field.Key.Equals("Path", StringComparison.OrdinalIgnoreCase)
                        && field.Value is System.Text.Json.Nodes.JsonValue value
                        && value.TryGetValue<string>(out var oldPath))
                        pick[field.Key] = RestoredArtPath(oldPath, manifest);
        }
        return map.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string RestoredArtPath(string original, Dictionary<string, string>? manifest)
    {
        string leaf = manifest is not null && manifest.TryGetValue(original, out var mapped)
            ? mapped : Path.GetFileName(original);
        if (string.IsNullOrEmpty(leaf) || Path.GetFileName(leaf) != leaf) return original;
        string candidate = Path.Combine(GameArt.CacheDirectory, leaf);
        return File.Exists(candidate) ? candidate : original;
    }


    /// <summary>Repoint each slice's LogoPath at THIS machine's art cache when a restored logo file of the
    /// same name is present there — so a backup made under another user profile (or after a wipe that cleared
    /// the cache the original absolute path pointed at) still shows its custom logo. The backup's manifest
    /// (LogoPath → archived leaf) is consulted first, so a logo whose leaf name collided at backup time —
    /// and was archived under a hash-prefixed name — repoints to the RIGHT image; plain leaf matching is
    /// the fallback for manifest-less (older) backups. A no-op when the path already resolves (same
    /// machine) or no matching file was restored (then the startup self-heal re-fetches).</summary>
    private static WheelSlice[] RepointRestoredLogos(WheelSlice[] slices, Dictionary<string, string>? manifest = null)
    {
        var dir = GameArt.CacheDirectory;
        var result = (WheelSlice[])slices.Clone();
        for (int i = 0; i < result.Length; i++)
        {
            var s = result[i];
            if (string.IsNullOrWhiteSpace(s.LogoPath)) continue;
            string leaf = manifest is not null && manifest.TryGetValue(s.LogoPath, out var mapped)
                ? mapped : Path.GetFileName(s.LogoPath);
            var candidate = Path.Combine(dir, leaf);
            if (!string.Equals(candidate, s.LogoPath, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                result[i] = s.WithLogoPath(candidate);
        }
        return result;
    }

    /// <summary>Parse art-manifest.json from a backup zip (slice LogoPath → archived entry leaf). The zip is
    /// hostile input, so every VALUE must be a bare image leaf name — anything with separators, traversal,
    /// or a non-image extension is dropped (the keys are only ever used as lookup keys, so they're inert).
    /// Null when absent/unreadable (older backups) — the caller falls back to leaf matching.</summary>
    private static Dictionary<string, string>? ReadArtManifest(System.IO.Compression.ZipArchive zip)
    {
        try
        {
            if (zip.GetEntry("art-manifest.json") is not { } entry) return null;
            var json = ReadBoundedText(entry);
            if (json is null) return null;
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw is null) return null;
            var safe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in raw)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                string leaf = Path.GetFileName(value.Replace('/', Path.DirectorySeparatorChar));
                if (!string.Equals(leaf, value, StringComparison.Ordinal)) continue;   // had separators — hostile
                if (!RestorableArtExts.Contains(Path.GetExtension(leaf), StringComparer.OrdinalIgnoreCase)) continue;
                safe[key] = leaf;
            }
            return safe.Count > 0 ? safe : null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Restore] art-manifest read failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>True when the JSON has the top-level properties a Radiata config always serializes with
    /// (the wheel arrays + system object — WriteConfig emits all three even when empty/default), so an
    /// arbitrary .json can't sail past Restore's "valid backup" check and wipe everything. Property-name
    /// match is case-insensitive, like the config parser itself.</summary>
    private static bool LooksLikeRadiataBackup(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return false;
            bool wheelA = false, wheelB = false, system = false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if      (prop.Name.Equals("wheelA", StringComparison.OrdinalIgnoreCase)) wheelA = true;
                else if (prop.Name.Equals("wheelB", StringComparison.OrdinalIgnoreCase)) wheelB = true;
                else if (prop.Name.Equals("system", StringComparison.OrdinalIgnoreCase)) system = true;
            }
            return wheelA && wheelB && system;
        }
        catch { return false; }
    }

    /// <summary>Restore factory defaults (wheels/colours/settings). Destructive — confirmed first.</summary>
    private void ResetAllSettings()
    {
        var choice = System.Windows.MessageBox.Show(this,
            Loc.T(UiText.Settings.ResetPrompt),
            Loc.T(UiText.Settings.ResetCaption), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes) return;

        _saveTimer.Stop();
        if (!_config.WriteConfig(AppConfig.Default))
        {
            _saveFailed = true;
            SetStatus(Loc.F(UiText.Settings.SaveFailed, _config.LastWriteError ?? ""), autoHide: false);
            return;
        }
        LoadFromConfig(AppConfig.Default);
        SetStatus(Loc.T(UiText.Settings.ResetToDefaults));
    }

    /// <summary>Full clean-install wipe: delete everything in %APPDATA%\Radiata, then recreate a
    /// default config. For true first-run/OOBE testing. Very destructive — confirmed first.</summary>
    private void WipeAppDataAndReset()
    {
        var choice = System.Windows.MessageBox.Show(this,
            Loc.T(UiText.Settings.WipePrompt),
            Loc.T(UiText.Settings.WipeCaption), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes) return;

        _saveTimer.Stop();

        // Un-cloak the controller BEFORE deleting %APPDATA%\Radiata — the delete removes the cloak-recovery
        // record (cloaked-ids.txt), so make the pad fully normal first. Otherwise a hard-kill in the wipe→
        // restart window could strand the cloak with no record to self-heal from. (TearDown also un-cloaks
        // on the clean shutdown path; this closes the gap.)
        if (Application.Current is not App app || !app.UncloakForReset())
        {
            SetStatus(Loc.T(UiText.Settings.RecoveryRequired), autoHide: false);
            return;
        }

        // Best-effort delete of the folder's contents (keep the folder — the config watcher holds a
        // handle on it; locked files are skipped). The relaunched instance recreates a default
        // config on first run, so we don't write one here.
        try
        {
            var dir = AppPaths.AppDataDir;
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir))      { try { File.Delete(f); }              catch { } }
                foreach (var d in Directory.GetDirectories(dir)) { try { Directory.Delete(d, true); } catch { } }
            }
        }
        catch { /* best-effort */ }

        _restarting = true;   // suppress the unsaved-changes prompt on the shutdown-driven close
        RestartApp();
    }

    /// <summary>Shrink the tab HEADERS until the strip fits on one row. The window is a fixed width and Spanish (and
    /// the pseudo-locales) push the seven headers onto a second row at full size, which misaligns the strip and
    /// steals height from the content. ⚠ Never set TabItem.FontSize for this: a TabItem's content inherits it, so
    /// the whole page would shrink with the header. Each header string is wrapped in its own TextBlock and only
    /// that TextBlock's size and the header padding step down, from the styled values, until every header shares
    /// the first one's row.</summary>
    private Dictionary<TabItem, (TextBlock Header, double Font, Thickness Pad)>? _tabBase;
    private void FitTabStrip()
    {
        var tabs = Tabs.Items.OfType<TabItem>().ToList();
        if (tabs.Count < 2) return;
        _tabBase ??= tabs.ToDictionary(t => t, t =>
        {
            var tb = t.Header as TextBlock ?? new TextBlock { Text = t.Header?.ToString() ?? "" };
            t.Header = tb;
            return (tb, t.FontSize, t.Padding);
        });
        foreach (double k in new[] { 1.0, 0.93, 0.86, 0.79, 0.72 })
        {
            foreach (var t in tabs)
            {
                var (header, font, pad) = _tabBase[t];
                header.FontSize = font * k;
                t.Padding = new Thickness(pad.Left * k, pad.Top, pad.Right * k, pad.Bottom);
            }
            Tabs.UpdateLayout();
            // One row = every header shares the first one's BOTTOM edge. Tops differ on a single row by design: the two
            // wheel tabs are taller than the bottom-aligned short ones, so a top comparison read every layout as wrapped.
            double Bottom(TabItem t) => t.TranslatePoint(new Point(0, t.ActualHeight), Tabs).Y;
            double first = Bottom(tabs[0]);
            if (tabs.All(t => Math.Abs(Bottom(t) - first) < 1)) return;
        }
    }
    /// <summary>Relaunch Radiata: a detached helper waits for THIS process to fully exit (releasing the
    /// single-instance lock), then starts a fresh instance; we shut down immediately.
    /// <paramref name="reopenSettings"/> passes <c>--settings</c> so the new instance comes back with this
    /// window open rather than as a bare tray icon; <paramref name="reopenOnboarding"/> reopens the wizard (a
    /// language pick made inside it) — needed because a completed onboarding would otherwise not return.
    /// <para>If the helper can't even be LAUNCHED, say so before shutting down — the user was just promised
    /// "Radiata will restart", and vanishing without explanation reads as the restore/wipe having destroyed
    /// the app. We still shut down either way: the on-disk state is already rewritten, and a stale instance
    /// running against it is worse than no instance. A helper that launches but then fails is beyond reach
    /// by design (we're gone before it acts).</para></summary>
    internal static void RestartApp(bool reopenSettings = false, bool reopenOnboarding = false)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("own exe path unknown");
            // The path is interpolated into a single-quoted PowerShell string — double any apostrophe
            // (PowerShell's escape inside '…') or an install path like C:\Users\O'Brien\… terminates
            // the quote and the restart breaks. '"' can't appear in a Windows path.
            var exeEsc = exe.Replace("'", "''");
            // -FilePath, NOT -LiteralPath: Start-Process has no -LiteralPath parameter — the helper dies
            // silently in a hidden child process and the app never comes back. -FilePath does not glob,
            // so it is literal already.
            Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -WindowStyle Hidden -Command \"Wait-Process -Id {Environment.ProcessId} " +
                $"-ErrorAction SilentlyContinue; Start-Process -FilePath '{exeEsc}'"
                + (reopenOnboarding ? " -ArgumentList '--oobe'" : reopenSettings ? " -ArgumentList '--settings'" : "") + "\"")
                { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Settings] restart helper failed to launch: {ex.Message}");
            System.Windows.MessageBox.Show(
                Loc.T(UiText.Settings.RestartFailed),
                "Radiata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        System.Windows.Application.Current.Shutdown();
    }
}

file sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? _) => true;
    public void Execute(object? p) => execute(p);
}
