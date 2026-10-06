using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace ControllerWheel;

public partial class WheelEditorControl : UserControl
{
    public event EventHandler? Changed;

    /// <summary>Help-chip jumps (topic id) — the host (SettingsWindow) opens the Help tab on that topic.</summary>
    public event Action<string>? HelpRequested;
    private void HelpAppSlices_Click(object sender, System.Windows.RoutedEventArgs e)   => HelpRequested?.Invoke("app-slices");
    private void HelpUri_Click(object sender, System.Windows.RoutedEventArgs e)         => HelpRequested?.Invoke(CurrentType() == "discord-join" ? "discord-setup" : "open-uri");
    private void HelpSwitchAudio_Click(object sender, System.Windows.RoutedEventArgs e) => HelpRequested?.Invoke("switch-audio");
    private void HelpTextChat_Click(object sender, System.Windows.RoutedEventArgs e)    => HelpRequested?.Invoke("text-chat");
    private void HelpDiscord_Click(object sender, System.Windows.RoutedEventArgs e)     => HelpRequested?.Invoke("discord-setup");
    private void HelpObs_Click(object sender, System.Windows.RoutedEventArgs e)         => HelpRequested?.Invoke("obs-studio");
    private void HelpColorLabel_Click(object sender, System.Windows.RoutedEventArgs e)  => HelpRequested?.Invoke("editor-desktop");
    private void HelpKeyCombo_Click(object sender, System.Windows.RoutedEventArgs e)    => HelpRequested?.Invoke("key-combo");

    private readonly ObservableCollection<SliceEditModel> _slices = [];   // committed truth (bound to the list)
    private SliceEditModel? _committed;               // the committed slice currently being edited
    private SliceEditModel? _draft;                   // its working copy — edits land here until "Save Slice"
    // Snapshot of the committed slice's values taken the moment it was SELECTED (see LoadIntoEditor). For an
    // EXISTING slice (auto-save mode) _committed is overwritten on every keystroke, so Revert can't target
    // "the committed slice" directly — it targets this snapshot instead. Unused for a NEW (unsaved) slice,
    // which keeps the plain Save/Revert-to-committed behavior.
    private SliceEditModel? _selectionSnapshot;
    private bool _dirty;                              // draft differs from committed
    private bool _revertingSelection;                 // guard while undoing a slice-selection change
    private bool _loading;
    private bool _gamesLoaded;
    private bool _autoLabeling;                       // re-entrancy guard for auto-label
    private bool _labelInferred;                      // current label was auto-inferred (overwriteable) vs user-typed
    // The label field shows a default label in the run's language; config keeps the English. Unchanged text
    // writes the raw label back, so switching language is lossless (Loc.DefaultLabel).
    private string? _labelRaw, _labelShown;
    private const string NewSliceLabel = UiText.DefaultLabels.NewSlice; // default label; auto-filled until the user edits it
    // Dark red flag for the slice COUNT when a saved wheel exceeds the 12-slice cap — only reachable from a
    // hand-edited config now that the cap is fixed (ConfigLoader's hard ceiling is 32); the count is what's
    // wrong, not the "/ 12" limit.
    private static readonly Brush OverCapBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B));

    // ── Action type options ───────────────────────────────────────────────────
    // A concrete action: a type, an optional system command, and its display name.
    // (internal so the in-wheel Add picker can reuse the same taxonomy — single source of truth.)
    // Hidden: mirrors TypeEntry.Hidden below, but at the OPTION level — for a command removed from a
    // multi-option GROUP where hiding the whole entry isn't right, only one option in it.
    // SyncSubTypeBox skips it when populating SubTypeBox.Items;
    // EnsureOptionInSubTypeBox re-adds it just for an existing slice's exact-match selection (same pattern
    // as EnsureEntryInTypeBox). Without this, a legacy slice using an unoffered option would fall through
    // SelectTypeOption's "first entry of matching type" fallback and get its Command silently REWRITTEN the
    // next time ReadEditorInto runs (auto-save) — a legacy slice must silently no-op, not get corrupted.
    // Alias: the EXACT OPPOSITE of Hidden — offered here, but never resolved TO here. It's a second listing
    // of an action whose canonical home is another category (OS mic mute, offered inside each chat group
    // whose own app-specific mute is Hidden). SelectTypeOption's exact-match pass skips aliases, so loading
    // an existing slice always lands on the one canonical entry instead of on whichever duplicate the scan
    // reached first — which, since the scan is positional, would otherwise be decided by category order.
    internal sealed record TypeOption(string Type, string? Command, string Display,
                                      bool Hidden = false, bool Alias = false);

    // A primary-dropdown entry. A leaf has a single Option; a group (e.g. Audio/Display/Power) has
    // several, surfaced in the secondary "sub-option" dropdown.
    // Hidden: excluded from what's OFFERED (PopulateTypeBoxForCurrentTab skips it, so neither the Settings
    // type dropdown nor the in-wheel Add picker lists it), but the entry stays in Categories so an EXISTING
    // slice already using that type still loads/edits/type-locks correctly (SelectTypeOption searches
    // Categories directly, unfiltered, and EnsureEntryInTypeBox below re-adds it just for that selection).
    // Key: the stable identity of a GROUP — what AddMenuIcons and the persisted "group:<key>" overrides are keyed
    // by. Defaults to the display name while the UI is English; once display text is translated the key stays
    // English forever, or every saved glyph/colour override for the group would be orphaned.
    internal sealed record TypeEntry(string Display, TypeOption[] Options, bool Group = false, bool Hidden = false,
                                     string? Key = null)
    {
        public string GroupKey => Key ?? Display;
    }

    private static TypeEntry Leaf(string type, string? command, string display, bool hidden = false) =>
        new(display, [new TypeOption(type, command, display)], Hidden: hidden);

    // Surfaces a secondary dropdown when multi-option, or when explicitly flagged a group (so a
    // one-option group like "Controller" still reads as expandable / reserved for more).
    internal static bool IsGroup(TypeEntry e) => e.Group || e.Options.Length > 1;

    // Action entries grouped into category tabs. The primary dropdown lists entries; selecting a
    // multi-option group reveals the secondary dropdown with that group's choices.
    // internal: shared with the in-wheel Add picker (App.BuildAddMenu).
    //
    // A PROPERTY keyed on Arcade.Available, not a static readonly field: the harness conceals Arcade through
    // Arcade.ConcealedForHarness to prove the no-offer/no-rewrite contract both ways, so the taxonomy must
    // rebuild when Available moves. Cached against it, so the common case is still one array read — this is
    // consulted on every tab switch and by every Add-picker build, so it must not allocate per access.
    // Key is the category's stable identity (AddMenuIcons dictionaries, "category:<key>" overrides, the Chat width
    // cap); Header is what the tab shows. Equal today; only Header may ever be translated.
    internal static (string Key, string Header, TypeEntry[] Entries)[] Categories
    {
        get
        {
            if (_categories is not null && _categoriesGate == Arcade.Available && ReferenceEquals(_categoriesGames, ArcadeCatalog.Games)) return _categories;
            _categoriesGate = Arcade.Available;
            _categoriesGames = ArcadeCatalog.Games;
            return _categories = BuildCategories();
        }
    }

    private static (string Key, string Header, TypeEntry[] Entries)[]? _categories;
    private static bool _categoriesGate;
    private static ArcadeCatalog.Entry[]? _categoriesGames;   // ArcadeCatalog.Games is a new array whenever RegisterScripts runs

    private static (string Key, string Header, TypeEntry[] Entries)[] BuildCategories() =>
    [
        ("Games & Apps", "Games & Apps", [
            Leaf("installed-game", null, "Installed Game"),
            Leaf("game-browser",   null, "Game Grid"),
            Leaf("launcher",       null, "Storefront"),
            Leaf("launch",         null, "Launch App"),
            Leaf("exit-app",       null, "Exit Current App"),
            // Game Bar lives with games — it captures/records gameplay, so users look for it here,
            // not under System.
            new("Game Bar", [
                new("system", "gamebar-open",        "Open Game Bar"),
                new("system", "gamebar-screenshot",  "Screenshot"),
                new("system", "gamebar-record",      "Start / Stop Recording"),
                new("system", "gamebar-record-last", "Record Last 30 sec"),
                new("system", "gamebar-mic",         "Toggle Mic While Recording"),
            ]),
        ]),
        // Chat & Streaming = every "talk to people / broadcast to people" action in one place. Steam offers
        // NO Discord-style IPC, so its group carries only what the platform allows: open-the-UI + mic mute.
        // Join/leave voice and deafen are platform impossibilities (steam-xbox-voice Help topic) — don't add
        // them; the Steam CEF-debug backdoor is ruled out (security posture). Xbox Party Chat is not offered.
        // NOTE: AddMenuIcons.cs keys CategoryGlyphs/CategoryColors by the
        // category KEY (first tuple element) — rename the dictionary key alongside any key rename; the
        // header is free to change or translate.
        ("Chat & Streaming", "Chat & Streaming", [
            // ⚠ ALL THREE app-specific MIC MUTES stay OUT of the offer. The OS mute is one switch that
            // actually silences the microphone, needs no setup, and reads its true state back into the armed
            // hub; an app-specific mute silences you in that app alone. Each group carries the OS mute as an
            // Alias instead, so it's still one pick away from where people look. `discord-mute` and
            // `steam-mute` stay as Hidden options, NOT deleted — an existing slice must keep resolving
            // (docs/ACTIONS.md's no-offer/no-rewrite contract) and the executor still runs both. Deafen is
            // offered: it is not a mic mute, and no OS switch does it.
            new("Discord", [
                new("discord-launch",  null, "Launch Discord"),
                new("discord-join",    null, "Join/Leave Voice Channel"),
                new("system", "mic-mute", "Mic Mute", Alias: true),
                new("discord-mute",    null, "Mute Me", Hidden: true),
                new("discord-deafen",  null, "Deafen"),
            ]),
            new("Steam Chat", [
                new("system",     "steam-chat-open", "Open Steam Chat"),
                new("system", "mic-mute", "Mic Mute", Alias: true),
                new("steam-mute", null,   "Mic Mute / Push-to-Talk", Hidden: true),
            ]),
            // Xbox Party Chat: removed from the offer entirely, executor cases included — a surviving slice
            // no-ops via the unknown-command default. The whole group stays Hidden, not deleted, so an
            // existing slice keeps its type instead of being rewritten (docs/ACTIONS.md ▸ The hidden-but-supported contract).
            new("Xbox Party Chat", [
                new("system", "xbox-party-open", "Open Chat"),
                new("system", "xbox-party-mute", "Mic Mute (party only)"),
            ], Hidden: true),
            new("OBS Studio", [
                new("obs", "obs-toggle-stream", "Toggle Streaming"),
                new("obs", "obs-toggle-record", "Toggle Recording"),
                new("obs", "obs-save-replay",   "Save Replay Buffer"),
                new("obs", "obs-set-scene",     "Switch Scene…"),
                new("obs", "obs-toggle-mic",    "Toggle Source Mute…"),
            ]),
            // Text Chat: opens the running game's in-game TEXT chat (not voice — Discord/Steam/
            // Xbox above cover that) and types a preset message. "Try Game Default" resolves the chat key
            // from Core/GameChatButtons.cs at FIRE time (re-detected every fire); "Custom" sends a
            // user-chosen key instead — see Core/ActionExecutor.cs "text-chat" case.
            Leaf("text-chat", null, "Text Chat"),
        ]),
        ("System", "System", [
            new("Controller", [
                new("xbox-emulation",      null, "Toggle Xbox Mode"),
                new("dualshock-emulation", null, "Toggle DualShock Mode"),
            ]),
            new("Audio", [
                new("switch-audio", null,         "Switch Audio Output"),
                new("system",       "volume",     "Toggle Mute"),
                new("system",       "mic-mute",   "Mic Mute"),
                new("system",       "volume-set", "Set Volume To…"),
                new("system",       "media-play-pause", "Play / Pause"),
                new("system",       "media-next",       "Next Track"),
                new("system",       "media-prev",       "Previous Track"),
                // media-mute is executor-supported but never offered: a Hidden option, so a legacy slice
                // resolves here instead of falling to the first "system" entry and having its Command rewritten.
                new("system",       "media-mute",       "Mute (media key)", Hidden: true),
            ]),
            new("Display", [
                // display-toggle reads the CURRENT topology and flips it
                // (WindowsPlatformActions.ToggleDisplayTopology).
                new("system", "display-toggle",   "Toggle Extend/Clone"),
                new("system", "hdr-toggle",       "Toggle HDR"),
                // The options below aren't offered (the executor no-ops any surviving slice) but
                // stay as Hidden options: SelectTypeOption's exact-match loop (which reads Categories
                // unfiltered) must still find a legacy slice's (type, command) pair, or its Command gets
                // silently REWRITTEN by the next auto-save — see TypeOption.Hidden.
                new("system", "display-extend",   "Extend",              Hidden: true),
                new("system", "display-clone",    "Clone",               Hidden: true),
                new("system", "display-external", "External Only",      Hidden: true),
                new("system", "display-internal", "Internal Only",      Hidden: true),
                new("system", "nvidia-overlay",   "NVIDIA Stats Overlay", Hidden: true),
                new("system", "amd-overlay",      "AMD Metrics Overlay",  Hidden: true),
            ]),
            // "Recording": not offered — Hidden: true keeps the whole group out of the dropdown while
            // leaving its options in place for a legacy slice's exact-match selection (same "no offer,
            // no rewrite" contract as the Display options above). No executor cases exist, so any
            // surviving slice silently no-ops via the unknown-command default.
            new("Recording", [
                new("system", "nvidia-record",      "NVIDIA: Record"),
                new("system", "nvidia-replay-save", "NVIDIA: Save Instant Replay"),
                new("system", "nvidia-screenshot",  "NVIDIA: Screenshot"),
                new("system", "amd-record",         "AMD: Record"),
                new("system", "amd-replay-save",    "AMD: Save Replay"),
            ], Hidden: true),
            // "Windows" group: window/shell-management actions, not power states — the System tab's home
            // for "OS chrome" actions.
            new("Windows", [
                new("system", "show-desktop",      "Show Desktop"),
                // Do Not Disturb is REMOVED — no offer, no executor case. Windows moved the setting to the
                // `windows.data.donotdisturb.*` CloudStore state and orphaned the Focus Assist WNF state the
                // action wrote, which still accepts writes that change nothing. Don't reintroduce it without a
                // mechanism the shell actually reads. Hidden, not deleted, so a surviving slice keeps its type.
                new("system", "focus-assist",      "Do Not Disturb", Hidden: true),
                new("system", "empty-recycle-bin", "Empty Recycle Bin"),
            ]),
            new("Power", [
                new("system", "sleep",      "Sleep"),
                new("system", "hibernate",  "Hibernate"),
                new("system", "reboot",     "Reboot"),
                new("system", "shutdown",   "Shut Down"),
                new("system", "logout",     "Log Out"),
                new("system", "lock",       "Lock Screen"),
                // Flips between two chosen plans.
                new("system", "power-plan", "Power Plan"),
            ]),
        ]),
        // HTTP Request / Send WoL / PowerShell are gone entirely — the executor's unknown-type
        // fallback no-ops any surviving slices.
        // ⚠ "Custom" is a DISPLAY NAME only — the slice editor's tab strip and the in-wheel Add picker
        // (one shared tree), and it keys AddMenuIcons. Nothing persists it. Not to be confused with the
        // Settings ▸ Advanced TAB, which is a different thing entirely.
        ("Custom", "Custom", [
            Leaf("url",        null, "Open URI"),
            // "Run Script" is not offered, but an existing SLICE of type "script" can exist. A type with
            // NO Categories entry at all would make SelectTypeOption's fallback loop find nothing whose
            // Options[0].Type == "script" and drop to tab 0 with nothing selected — the NEXT auto-save's
            // ReadEditorInto would then REWRITE the slice's type outright. Hidden: true keeps it
            // selectable-but-not-offered, so a legacy script slice still loads/type-locks/auto-saves
            // correctly. The EXECUTOR'S "script" case is also kept — the hidden-but-working "sequence"
            // type's `script:` steps call through the same RunFile path (see Core/ActionExecutor.cs
            // "script" case) — removing it would silently break every sequence with a script step.
            Leaf("script",     null, "Run Script", hidden: true),
            // "toggle" is the older spelling of Launch App with Toggle behaviour. The executor and the sequence
            // step text form still use it; the editor never authors it. Without this entry a stored toggle
            // slice matches nothing and the next auto-save rewrites its type to tab 0's first entry.
            Leaf("toggle",     null, "Toggle App", hidden: true),
            Leaf("keypress",   null, "Key Combo"),
            // Sequence stays fully wired (executor, SequenceFormat, editor fields, Help topic) so any
            // EXISTING sequence slice keeps working, but it's not OFFERED for new slices — hidden: true
            // drops it from the populated dropdown (see TypeEntry.Hidden). Re-offer by removing the flag.
            Leaf("sequence",   null, "Sequence", hidden: true),
        ]),
        ("Radiata", "Radiata", [
            Leaf("game-browser",   null, "Game Grid"),   // also under Games & Apps
            Leaf("settings",       null, "Open Settings"),
            Leaf("disable-wheels", null, "Disable Wheels"),
            // Toggle Passthru Mode: never offered. A wheel slice is the ONLY path that flips Passthru Mode off
            // with the game still foregrounded (the overlay is WS_EX_NOACTIVATE), so it is also the only
            // path that arms the pending passthru-mode-off hold — the tray and Settings paths hand the
            // foreground to a shell surface and complete on the spot. Un-offering the slice keeps the
            // hold off every user-reachable path. Kept selectable per the no-offer/no-rewrite contract in
            // docs/ACTIONS.md so an existing slice resolves instead of having its type rewritten by the
            // next auto-save; the executor case and App.SafeModeAvailable wiring stay with it.
            Leaf("safe-mode", null, "Toggle Passthru Mode", hidden: true),
            // ARCADE lives HERE, as a child group of Radiata, not as its own tab. One entry, two states:
            //  • Arcade AVAILABLE → an offered group ("Arcade Launcher" first, then a sub-option per game).
            //  • Arcade UNAVAILABLE (see Core/Arcade/Arcade.cs) → the SAME entry, Hidden.
            //    Hidden = not offered anywhere — but the options still RESOLVE, per the no-offer/no-rewrite
            //    contract in docs/ACTIONS.md: without them SelectTypeOption would find nothing for an arcade
            //    slice authored while the feature was on, and the next auto-save would rewrite that slice's
            //    type outright. TestHarness.exe arcade asserts both ways.
            ArcadeEntry(),
        ]),
    ];

    /// <summary>The Arcade group under Radiata: the launcher first, then one option per game. The entry's
    /// Hidden tracks <see cref="Arcade.Available"/> (the Categories cache is keyed off it and off the catalogs Games array).
    ///
    /// <para>⚠ The launcher option (blank Command) must stay FIRST and must never be deleted: it is the
    /// fallback that <c>SelectTypeOption</c> resolves for ANY command naming no catalogued game — a blank
    /// one, the edit model's filler, or a game id from a newer build. Without it those slices match nothing,
    /// which per docs/ACTIONS.md makes the next auto-save rewrite the slice's type outright.</para></summary>
    private static TypeEntry ArcadeEntry() =>
        new("Arcade",
            [new TypeOption("arcade", null, "Arcade Launcher"),
             .. ArcadeCatalog.Games.Select(g => new TypeOption("arcade", g.Id, g.Title))],
            Group: true, Hidden: !Arcade.Available);

    private bool _switchingTab;           // guards the programmatic dropdown repopulate on tab switch

    public WheelEditorControl()
    {
        InitializeComponent();
        SyncCategoryTabHeaders();         // headers come from Categories — see the method's remarks
        PopulateComboBoxes();
        MoveBodyToSelectedTab();          // body starts in tab 0; load tab 0's options
        PopulateTypeBoxForCurrentTab();
        SliceList.ItemsSource = _slices;
        // PathBox is hidden storage (browse-only) — mirror it into the visible readout.
        PathBox.TextChanged += (_, _) =>
            PathReadout.Text = string.IsNullOrWhiteSpace(PathBox.Text) ? Loc.T(UiText.Editor.NoAppChosen)
                // shell:AppsFolder\<AUMID> (Installed Apps pick) → show the app's display name, not the raw id
                : InstalledApps.NameFor(PathBox.Text) is { } appName ? Loc.F(UiText.Editor.InstalledApp, appName)
                : PathBox.Text;
        InlineIconPicker.Changed         += (_, _) => OnInlineIconChanged();
        InlineIconPicker.FetchLogoClicked += (_, _) => OnInlineFetchLogo();
        InlineIconPicker.ResetRequested  += (_, _) => ResetLabelToDefault();
        InlineIconPicker.LogoCycled      += (_, path) => OnInlineLogoCycled(path);
        InlineIconPicker.AllowLogoDrop    = true;   // the slice editor accepts a dropped image as the logo
        InlineIconPicker.LogoFileDropped += (_, path) => OnInlineLogoFileDropped(path);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>The wheel's slice material (a canonical config value like "obsidian" — the host pushes
    /// <c>cfg.System.SliceMaterial</c>; <c>ConfigLoader.Sanitize</c> normalizes legacy aliases on load) —
    /// drives the inline icon picker's preview-well background AND which colours the preview shows (the
    /// wheel gives built-in tints and palette picks a material variant — lighter on dark materials — and
    /// the preview must match). Set by the host. This changes only what the well DISPLAYS: the colour the
    /// picker hands back for saving is material-neutral.</summary>
    public string SliceMaterial
    {
        set
        {
            if (value == _sliceMaterial) return;
            _sliceMaterial = value;
            _isDarkMaterial = Materials.IsDark(value);
            InlineIconPicker.SetMaterial(value);   // matches the Customize material tile's exact swatch
            if (_draft is not null) SyncIconPicker();   // re-show the material-matched variant
        }
    }
    private string? _sliceMaterial;
    private bool _isDarkMaterial;

    /// <summary>The global slice-label mode (<see cref="SystemConfig.ShowSliceLabels"/>), pushed in by the
    /// host exactly like <see cref="SliceMaterial"/>. The per-slice "Show label" checkbox is only MEANINGFUL
    /// in the <c>"selected"</c> mode — in the other three the wheel ignores the flag entirely — so the box
    /// is hidden outside it rather than left there lying about what it controls. The stored value is
    /// untouched while hidden, so switching back to Select Manually restores every per-slice choice.</summary>
    public string ShowSliceLabelsMode
    {
        get => _showSliceLabelsMode;
        set
        {
            var norm = SliceLabelRule.Normalize(value);
            if (norm == _showSliceLabelsMode) return;
            _showSliceLabelsMode = norm;
            UpdateShowLabelEnabled();
        }
    }
    private string _showSliceLabelsMode = SliceLabelRule.Default;

    private bool _modalOpen;   // an icon/colour or file dialog is up, holding a reference to the current draft

    /// <summary>True while an uncommitted slice draft is being edited (changes not yet folded into the
    /// list) OR a modal picker dialog is open over it — so a host reloading external config changes can
    /// skip clobbering in-progress edits (a reconcile mid-dialog would orphan the dialog's target draft).</summary>
    public bool HasPendingEdits => _dirty || _modalOpen;

    public void Load(WheelSlice[] slices)
    {
        // Reset draft state first so clearing/reselecting during a (re)load can't trip the
        // unsaved-changes prompt.
        _dirty = false; _committed = null; _draft = null; _selectionSnapshot = null;
        SetDirtyButtons(false);

        _slices.Clear();
        foreach (var s in slices)
            _slices.Add(SliceEditModel.FromSlice(s));
        RenumberAndRefresh(0);
        UpdateSliceButtons();
        // Loading EMPTY: the list-clear's SelectionChanged no-ops (null == null committed), so without
        // this the editor form stays visible/enabled showing the previous slice's values as a phantom.
        if (_slices.Count == 0) LoadIntoEditor(null);
    }

    public WheelSlice[] GetSlices() => [.. _slices.Select(m => m.ToSlice())];

    /// <summary>Surface a PURE APPEND from an external config write (e.g. a slice added via the in-wheel
    /// editor) WITHOUT reloading. Appends straight to the bound collection — no list refresh, no reselect —
    /// so it can never fire SelectionChanged and clobber the selected slice's unsaved draft. No-op unless
    /// the external list is exactly this editor's current slices plus extra ones at the end (any other
    /// change waits until the draft is saved/reverted). Returns true if it appended.</summary>
    public bool TryAppendExternal(WheelSlice[] external)
    {
        var current = GetSlices();
        if (external.Length <= current.Length) return false;
        for (int i = 0; i < current.Length; i++)
            if (JsonSerializer.Serialize(current[i]) != JsonSerializer.Serialize(external[i]))
                return false;   // the shared prefix differs → not a clean append; leave the draft alone
        for (int i = current.Length; i < external.Length; i++)
            _slices.Add(SliceEditModel.FromSlice(external[i]));   // ObservableCollection.Add appends without touching selection
        for (int i = 0; i < _slices.Count; i++) { _slices[i].Index = i; _slices[i].Total = _slices.Count; }
        UpdateSliceButtons();
        return true;
    }

    /// <summary>Index of the slice selected in the list, or -1. (For hosts preserving position across a Load.)</summary>
    public int SelectedIndex => SliceList.SelectedIndex;

    /// <summary>Select a slice by index and scroll it into view (loads it into the editor via the list's
    /// SelectionChanged). Used when the wheel jumps the user here to finish an unconfigured slice.</summary>
    public void SelectSlice(int index)
    {
        if (index < 0 || index >= _slices.Count) return;
        SliceList.SelectedIndex = index;
        SliceList.UpdateLayout();
        SliceList.ScrollIntoView(SliceList.SelectedItem);
    }

    /// <summary>Raised with the new slice-list column width when the user drags the splitter, so the
    /// other wheel editor can match it.</summary>
    public event EventHandler<double>? ListWidthChanged;

    /// <summary>Raised when "Test Action" is clicked, with the selected slice (current settings), so the
    /// host can fire it through the same executor the wheel uses.</summary>
    public event EventHandler<WheelSlice>? TestRequested;

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        if (_draft is null) return;
        // An RPC-driven Discord action can't run without the user's Discord app credentials: take the same
        // needs-setup route as firing it from a wheel (the wizard) instead of tracing and doing nothing.
        // Mirrors App.NeedsDiscordIntegration.
        if (CurrentType() is "discord-join" or "discord-mute" or "discord-deafen" && !DiscordOAuth.IsConfigured)
        {
            BtnConfigureDiscord_Click(sender, e);
            return;
        }
        ReadEditorInto(_draft);   // test exactly what's shown, including unsaved edits
        TestRequested?.Invoke(this, _draft.ToSlice());
    }

    /// <summary>Raised when "Test Wheel" is clicked — the host opens this whole wheel's overlay, the same
    /// way the controller invocation gesture would.</summary>
    public event EventHandler? TestWheelRequested;

    /// <summary>The Test-Wheel button caption — set per instance by the host ("Test Left/Right Wheel").</summary>
    public string TestWheelLabel { set => BtnTestWheel.Content = value; }

    /// <summary>UIA name for the slice list — set per instance by the host ("Left/Right wheel slices").
    /// The ListBox has no visible caption to derive one from, so without this Narrator introduces an
    /// anonymous list.</summary>
    public string SliceListName { set => System.Windows.Automation.AutomationProperties.SetName(SliceList, value); }

    private void BtnTestWheel_Click(object sender, RoutedEventArgs e) => TestWheelRequested?.Invoke(this, EventArgs.Empty);

    private void ListSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        ListWidthChanged?.Invoke(this, ListColumn.ActualWidth);

    /// <summary>Match the slice-list column width to another editor's (no event echo — only a user
    /// drag raises <see cref="ListWidthChanged"/>).</summary>
    public void SetListWidth(double width)
    {
        if (width > 0) ListColumn.Width = new GridLength(width);
    }

    /// <summary>Stamp each model's wheel position + total (for the slice-position indicator),
    /// re-render the list, and restore the given selection.</summary>
    private void RenumberAndRefresh(int selectIndex)
    {
        for (int i = 0; i < _slices.Count; i++) { _slices[i].Index = i; _slices[i].Total = _slices.Count; }
        SliceList.Items.Refresh();
        if (_slices.Count > 0)
            SliceList.SelectedIndex = Math.Clamp(selectIndex, 0, _slices.Count - 1);
    }

    /// <summary>Renumber + refresh for a REORDER (drag / Up / Down): a reorder keeps the SAME slice open,
    /// so it must NOT re-run the unsaved-changes prompt. Items.Refresh() transiently nulls the selection,
    /// which would re-enter SliceList_SelectionChanged and (for a dirty draft) pop Save/Discard — reading
    /// as "I have to save before I can reorder." The guard suppresses that; the draft stays intact.</summary>
    private void ReorderRefresh(int selectIndex)
    {
        for (int i = 0; i < _slices.Count; i++) { _slices[i].Index = i; _slices[i].Total = _slices.Count; }
        _revertingSelection = true;
        SliceList.Items.Refresh();
        if (_slices.Count > 0)
            SliceList.SelectedIndex = Math.Clamp(selectIndex, 0, _slices.Count - 1);
        _revertingSelection = false;
        UpdateLabelSliceIndicator();   // the edited slice's index moved
    }

    // ── List operations ───────────────────────────────────────────────────────

    private int _maxSlices = SystemConfig.MaxSlicesPerWheel;
    /// <summary>Per-wheel slice cap (always <see cref="SystemConfig.MaxSlicesPerWheel"/> now; set by
    /// SettingsWindow). Updates the add button + count.</summary>
    public int MaxSlices { get => _maxSlices; set { _maxSlices = value; UpdateSliceButtons(); } }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_slices.Count >= MaxSlices) return;
        if (!TryCommitOrDiscard()) return;   // settle the current slice's unsaved edits first
        // New slices start as Installed Game — the most common thing to add — with a random swatch.
        // The swatch's BASE tone goes in, flagged NOT exact, so every material renders its own variant —
        // the same thing a hand-clicked palette chip stores. IconColorExact must be an explicit false,
        // not null: null would be read as an exact typed colour (see ActionTint.IsExactColor's legacy rule).
        var swatch = ActionTint.Defaults[Random.Shared.Next(ActionTint.Defaults.Length)].Default;
        var model = new SliceEditModel
        {
            Label = NewSliceLabel, ActionType = "installed-game", IsNew = true, ShowLabel = false,
            IconColor = ActionTint.ToHex(swatch),
            IconColorExact = false,
        };
        _slices.Add(model);
        RenumberAndRefresh(_slices.Count - 1);
        UpdateSliceButtons();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (SliceList.SelectedIndex < 0 || _slices.Count == 0) return;   // an empty wheel is now valid
        int idx = SliceList.SelectedIndex;
        _dirty = false;                              // the slice (and its draft) are being deleted — no prompt
        SetDirtyButtons(false);
        _slices.RemoveAt(idx);
        RenumberAndRefresh(Math.Min(idx, _slices.Count - 1));
        UpdateSliceButtons();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        int idx = SliceList.SelectedIndex;
        if (idx <= 0) return;
        _slices.Move(idx, idx - 1);
        ReorderRefresh(idx - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void BtnDown_Click(object sender, RoutedEventArgs e)
    {
        int idx = SliceList.SelectedIndex;
        if (idx < 0 || idx >= _slices.Count - 1) return;
        _slices.Move(idx, idx + 1);
        ReorderRefresh(idx + 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Drag-to-reorder ─────────────────────────────────────────────────────────

    private Point          _dragStart;
    private SliceEditModel? _dragItem;
    // Index the carried slice started at, for the cancel path (see SliceList_PreviewMouseMove). -1 means
    // "no drag in flight, or the drop was accepted" — SliceList_Drop sets it to -1 on commit.
    private int _dragOriginalIndex = -1;
    private AdornerLayer?    _adornerLayer;
    private InsertionAdorner? _insertion;

    private void SliceList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem  = ItemModelFrom(e.OriginalSource as DependencyObject);
        System.Diagnostics.Trace.WriteLine(
            $"[SliceDrag] down on {( _dragItem?.Label ?? "(no row)")} src={e.OriginalSource?.GetType().Name}");
    }

    private void SliceList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Tooltips OFF for the drag's duration: a tooltip that pops up mid-drag becomes the window under
        // the cursor, which isn't a drop target. ToolTipService.IsEnabled inherits, so the list covers
        // every row and child.
        ToolTipService.SetIsEnabled(SliceList, false);
        // Remembered so a CANCELLED drag (Esc, or released outside every drop surface) can undo the live
        // reordering the DragOver handler has been doing all along — SliceList_Drop clears it to -1 to
        // signal "accepted, keep the new order".
        _dragOriginalIndex = _slices.IndexOf(_dragItem);
        var carried = _dragItem;
        DragDropEffects result;
        try { result = DragDrop.DoDragDrop(SliceList, _dragItem, DragDropEffects.Move); }
        finally { ToolTipService.SetIsEnabled(SliceList, true); }
        System.Diagnostics.Trace.WriteLine($"[SliceDrag] drag ended: {result}");
        if (_dragOriginalIndex >= 0)
        {
            // Never dropped on a surface that accepted it → put the slice back where it started.
            int now = _slices.IndexOf(carried);
            if (now >= 0 && now != _dragOriginalIndex)
            {
                _slices.Move(now, Math.Clamp(_dragOriginalIndex, 0, _slices.Count - 1));
                ReorderRefresh(_dragOriginalIndex);
            }
            _dragOriginalIndex = -1;
        }
        RemoveInsertionLine();   // external-file drags still use the line
        _dragItem = null;
    }

    private void SliceList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SliceEditModel)) is SliceEditModel dragged)   // internal drag-to-reorder
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            // LIVE REORDER: instead of parking an insertion LINE between rows
            // and only accepting a drop there, the carried slice is moved through the bound collection as
            // the pointer crosses each row's midline — the other slices shift to open a gap exactly where
            // it will land, and the drop is then a no-op commit. This also removes the whole "where is a
            // valid drop?" question: every point over the column is valid, because the item is already
            // wherever you're hovering. _slices is an ObservableCollection, so Move() reflows the ListBox
            // on its own (a positional jump per crossed row, not a tweened animation).
            int from = _slices.IndexOf(dragged);
            if (from < 0) return;                       // not ours (shouldn't happen) — leave the list alone
            var (gap, _) = GetDropGap(e);
            int to = gap > from ? gap - 1 : gap;        // the gap index counts the dragged row itself
            to = Math.Clamp(to, 0, _slices.Count - 1);
            if (to != from)
            {
                _slices.Move(from, to);
                // Keep the row the user is carrying selected/visible as it moves under the cursor; the
                // full renumber (Index/Total for the mini-wheels) waits for the drop so we're not
                // rebuilding every row's content on each midline crossing.
                SliceList.SelectedItem = dragged;
            }
            return;
        }
        // external .exe / .lnk drop (or a Start-menu Store-app drag) → add launch slice(s)
        if (LaunchableFiles(e).Count > 0 || DroppedInstalledApps(e).Count > 0)
        {
            e.Effects = _slices.Count < MaxSlices ? DragDropEffects.Copy : DragDropEffects.None;
            if (e.Effects != DragDropEffects.None) { var (_, y) = GetDropGap(e); ShowInsertionLine(y); }
            e.Handled = true; return;
        }
        e.Effects = DragDropEffects.None; e.Handled = true;
    }

    private void SliceList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // This handler is attached to BOTH the ListBox and its DockPanel parent (the panel catches a
        // reorder released a few px past the list's edge). Drop is a bubbling routed event, so a drop
        // ON the list reaches both attachments — without Handled, one dropped .lnk added TWO slices.
        e.Handled = true;
        RemoveInsertionLine();

        if (e.Data.GetData(typeof(SliceEditModel)) is SliceEditModel dragged)   // internal reorder
        {
            // The live reorder in SliceList_DragOver already put the slice where the pointer is, so the
            // drop just COMMITS: renumber the rows (Index/Total drive each row's mini-wheel) and tell the
            // host to save. _dragOriginalIndex is cleared so the cancel path in SliceList_PreviewMouseMove
            // knows this drag was accepted and must not roll the move back.
            int landed = _slices.IndexOf(dragged);
            System.Diagnostics.Trace.WriteLine(
                $"[SliceDrag] drop committed at index={landed} (was {_dragOriginalIndex})");
            bool moved = landed >= 0 && landed != _dragOriginalIndex;
            _dragOriginalIndex = -1;
            if (!moved) return;                       // dropped back where it started — nothing to save
            ReorderRefresh(landed);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        // External file drop: add a "Launch app" slice per dropped .exe / .lnk, at the cursor position.
        var files = LaunchableFiles(e);
        if (files.Count == 0) files = DroppedInstalledApps(e);   // Store/UWP app dragged from the Start menu
        if (files.Count == 0) return;
        if (!TryCommitOrDiscard()) return;   // settle the current slice's unsaved edits first

        var (gapIdx, _) = GetDropGap(e);
        int insertAt = Math.Clamp(gapIdx, 0, _slices.Count);
        int added = 0;
        foreach (var path in files)
        {
            if (_slices.Count >= MaxSlices) break;
            _slices.Insert(insertAt + added, BuildLaunchModel(path));
            added++;
        }
        if (added == 0) return;

        RenumberAndRefresh(insertAt);        // select the first slice we added
        UpdateSliceButtons();
        Changed?.Invoke(this, EventArgs.Empty);

        if (added < files.Count)             // hit the cap before placing them all
            System.Windows.MessageBox.Show(
                Loc.F(UiText.Editor.WheelFull, added, files.Count, MaxSlices),
                Loc.T(UiText.Editor.WheelFullCaption), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static readonly string[] LaunchableExts = { ".exe", ".lnk" };

    /// <summary>The dropped paths that are launchable apps (.exe / .lnk), in drop order. Empty if the
    /// drag isn't a file drop or holds none.</summary>
    private static List<string> LaunchableFiles(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
            return [];
        return paths.Where(p => LaunchableExts.Contains(
            System.IO.Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Store/UWP apps dragged from the Start menu, as shell:AppsFolder launch paths. Such a drag
    /// carries no usable file path (Kando hits the same edge), so the payload's text/filename is matched by
    /// NAME against the installed-apps list. Empty unless every candidate resolves — a near-miss would
    /// silently make a slice for the wrong app, and the Installed Apps… button is the reliable route.</summary>
    private static List<string> DroppedInstalledApps(System.Windows.DragEventArgs e)
    {
        var names = new List<string>();
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) &&
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            names.AddRange(paths.Select(System.IO.Path.GetFileNameWithoutExtension).OfType<string>());
        else if (e.Data.GetDataPresent(System.Windows.DataFormats.UnicodeText) &&
                 e.Data.GetData(System.Windows.DataFormats.UnicodeText) is string text)
            names.AddRange(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var apps = InstalledApps.Scan();
        var result = new List<string>();
        foreach (var name in names)
        {
            var match = apps.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.CurrentCultureIgnoreCase));
            if (match is null) return [];   // all-or-nothing (see remarks)
            result.Add(match.LaunchPath);
        }
        return result;
    }

    /// <summary>A "Launch app" slice for a dropped file. For an .lnk we resolve the target exe (so
    /// focus/toggle keys off the real process) but keep the shortcut's friendly name as the label.
    /// The launch icon is auto-extracted from the path by SliceEditModel.Preview.</summary>
    private static SliceEditModel BuildLaunchModel(string path)
    {
        // shell:AppsFolder\<AUMID> (Store app drop) — there's no filename to derive a label from, and no
        // .lnk target to resolve; the app's display name is the label and the path is already final.
        if (InstalledApps.NameFor(path) is { } appName)
            return new SliceEditModel { ActionType = "launch", Path = path, Label = appName, IsNew = true, ShowLabel = false };

        string label = System.IO.Path.GetFileNameWithoutExtension(path);
        string target = path;
        if (string.Equals(System.IO.Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
            target = ShortcutResolver.ResolveTarget(path) ?? path;   // fall back to the .lnk if unresolved
        return new SliceEditModel { ActionType = "launch", Path = target, Label = label, IsNew = true, ShowLabel = false };
    }

    /// <summary>Insertion gap under the cursor: gap index (0..count) and the line's Y in SliceList
    /// coordinates (top of the item to insert before, or bottom of the last item).</summary>
    private (int gap, double y) GetDropGap(System.Windows.DragEventArgs e)
    {
        int count = _slices.Count;
        var p = e.GetPosition(SliceList);
        for (int i = 0; i < count; i++)
        {
            if (SliceList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem c) continue;
            double top = c.TranslatePoint(new Point(0, 0), SliceList).Y;
            if (p.Y < top + c.ActualHeight / 2) return (i, top);   // insert before item i
        }
        if (count > 0 && SliceList.ItemContainerGenerator.ContainerFromIndex(count - 1) is ListBoxItem last)
            return (count, last.TranslatePoint(new Point(0, 0), SliceList).Y + last.ActualHeight);
        return (count, 0);
    }

    private void ShowInsertionLine(double y)
    {
        _adornerLayer ??= AdornerLayer.GetAdornerLayer(SliceList);
        if (_adornerLayer is null) return;
        if (_insertion is null) { _insertion = new InsertionAdorner(SliceList); _adornerLayer.Add(_insertion); }
        _insertion.SetY(y);
    }

    private void RemoveInsertionLine()
    {
        if (_insertion is not null) _adornerLayer?.Remove(_insertion);
        _insertion = null;
    }

    /// <summary>A horizontal "drop here" line drawn over the slice list while dragging to reorder.</summary>
    private sealed class InsertionAdorner : Adorner
    {
        private static readonly Pen LinePen = MakePen();
        private double _y;

        public InsertionAdorner(UIElement adorned) : base(adorned) { IsHitTestVisible = false; }

        public void SetY(double y) { _y = y; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            double w = (AdornedElement as FrameworkElement)?.ActualWidth ?? 0;
            dc.DrawLine(LinePen, new Point(3, _y), new Point(w - 3, _y));
            dc.DrawEllipse(LinePen.Brush, null, new Point(4, _y), 3, 3);
        }

        private static Pen MakePen()
        {
            var b = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0xE0)); b.Freeze();
            var p = new Pen(b, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            return p;
        }
    }

    /// <summary>Walk up the visual tree from a hit-test source to the owning ListBoxItem's model.</summary>
    private static SliceEditModel? ItemModelFrom(DependencyObject? src)
    {
        // A press can land on a ContentElement (e.g. a Run inside a TextBlock), which has no VISUAL
        // parent — VisualTreeHelper.GetParent throws/misses there, killing the drag before it starts.
        // Hop ContentElements via the logical tree instead.
        while (src is not null and not ListBoxItem)
            src = src is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(src)
                : LogicalTreeHelper.GetParent(src);
        return (src as ListBoxItem)?.DataContext as SliceEditModel;
    }

    // ── Editor form ───────────────────────────────────────────────────────────

    private void SliceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_revertingSelection) return;
        var newSel = SliceList.SelectedItem as SliceEditModel;
        if (ReferenceEquals(newSel, _committed)) return;   // unchanged target (e.g. after a reorder/refresh)

        // Leaving a slice with unsaved edits → Save / Discard / Cancel.
        bool refreshOld = false;
        if (_dirty && _committed is not null)
        {
            var choice = PromptUnsaved(CurrentLabelOr(_committed.Label), _actionValid);
            if (choice == LeaveChoice.Stay)
            {
                _revertingSelection = true;
                SliceList.SelectedItem = _committed;   // stay put
                _revertingSelection = false;
                return;
            }
            if (choice == LeaveChoice.Save)
            {
                var old = _committed;   // CommitDraft targets the slice being left
                CommitDraft();
                KickFirstSaveLogoFetch(old);   // same first-save fetch Save Slice performs
                refreshOld = true;
            }
            // Discard → drop the draft (just proceed). An invalid edit only ever reaches here as Discard.
            else if (_committed.IsNew) DropNeverSavedSlice(_committed, newSel);
        }

        LoadIntoEditor(newSel);
        if (refreshOld) RefreshKeepingSelection();   // show the just-saved old item's new label/icon
    }

    private void LoadIntoEditor(SliceEditModel? committed)
    {
        _committed = committed;
        _draft     = committed?.Clone();
        // Snapshot taken NOW, at selection time — Revert on an existing slice restores this, not
        // whatever _committed has drifted to via auto-save.
        _selectionSnapshot = committed?.Clone();

        _loading = true;
        try
        {
            bool hasModel = _draft is not null;
            EmptyHint.Visibility    = hasModel ? Visibility.Collapsed : Visibility.Visible;
            EditorFields.Visibility = hasModel ? Visibility.Visible   : Visibility.Collapsed;
            EditorPanel.IsEnabled   = hasModel;

            if (_draft is null) return;
            var model = _draft;

            _labelRaw   = model.Label;
            _labelShown = Loc.DefaultLabel(model.Label);
            LabelBox.Text    = _labelShown;
            ShowLabelCheck.IsChecked = model.ShowLabel;
            SelectTypeOption(model.ActionType, model.Command);
            PathBox.Text     = model.Path;
            ObsTargetBox.Text  = model.ObsTarget;
            SelectByTag(BehaviorBox, model.QuitIfRunning ? "toggle" : "run");
            UrlBox.Text      = model.Url;
            KeysBox.Text     = model.Keys;
            // (These two are plain TextBoxes — no ItemsSource, no selection/text duality, so nothing
            // here can clobber the value after it's written. See the DeviceRow XAML comment.)
            DeviceBox.Text   = model.Device;
            MicDeviceBox.Text = model.MicDevice;
            LevelBox.Text    = model.Level;
            SequenceBox.Text = model.SequenceText;
            ChatTextBox.Text = model.ChatText;
            ChatCustomKeyBox.Text = model.Keys;
            if (model.ActionType == "text-chat")
                SelectByTag(ChatButtonBox, string.Equals(model.Command, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "default");
            ConfirmCheck.IsChecked = model.RequireConfirm;
            RebootLoginCheck.IsChecked = model.LogInAfterReboot;
            UpdateFieldVisibility(model.ActionType);
            UpdateIconColorDisplay(model);
            // Storefront combo is set AFTER UpdateFieldVisibility shows its row (LauncherRow) — and, on the
            // first Settings open (control not yet in a rendered tree), re-applied once layout has run.
            // See ApplyLauncherSelection: otherwise the ComboBox hasn't realized its containers, the
            // selection fails to stick, and the SelectedIndex<0 default shows the first entry (Steam).
            if (model.ActionType == "launcher") ApplyLauncherSelection(model.Command);

            // Decide whether this label is a TYPE DEFAULT (follows a later type change) or USER content
            // (left alone). It's a default if it's still "New Slice" OR it exactly matches what the current
            // type would infer; anything the user typed/edited won't match, so it's preserved.
            var inferredNow = InferLabel(model.ActionType);
            _labelInferred = string.Equals(model.Label, NewSliceLabel, StringComparison.Ordinal)
                             || (inferredNow is not null && string.Equals(model.Label, inferredNow, StringComparison.Ordinal));

            // A slice is type-locked once it's been SAVED (IsNew cleared on commit): change its category by
            // deleting + re-adding. A freshly-added, not-yet-saved slice keeps the full category picker.
            ApplyTypeLock(!model.IsNew);
        }
        finally { _loading = false; }

        _dirty = false;
        SetDirtyButtons(false);
        UpdateLabelSliceIndicator();
    }

    /// <summary>Point the editor's wheel-position indicator (left of the Label field) at the current slice
    /// — same look as the slice-list rows. Updated on load and after a reorder (which moves the index).</summary>
    private void UpdateLabelSliceIndicator()
    {
        if (_committed is null) return;
        LabelSliceIndicator.SliceCount     = _committed.Total;
        LabelSliceIndicator.HighlightIndex = _committed.Index;
    }

    // The active action = the selected primary entry, narrowed by the secondary dropdown when the
    // entry is a multi-option group.
    private TypeEntry?  CurrentEntry()  => (TypeBox.SelectedItem as ComboBoxItem)?.Tag as TypeEntry;
    private TypeOption? CurrentOption()
    {
        var entry = CurrentEntry();
        if (entry is null) return null;
        if (!IsGroup(entry)) return entry.Options[0];
        return (SubTypeBox.SelectedItem as ComboBoxItem)?.Tag as TypeOption ?? entry.Options[0];
    }
    private string?  CurrentType()    => CurrentOption()?.Type;
    private string?  CurrentCommand() => CurrentOption()?.Command;

    /// <summary>Move the shared body panel (dropdown + confirm + fields) into the selected tab so it
    /// renders inside that tab's outline. Exactly one tab holds it at a time. No-ops while type-locked
    /// (the body lives in <c>LockedBody</c> then — see <see cref="ApplyTypeLock"/>).</summary>
    private void MoveBodyToSelectedTab()
    {
        if (_typeLocked) return;
        int idx = Math.Clamp(CatTabs.SelectedIndex, 0, CatTabs.Items.Count - 1);
        if (CatTabs.Items[idx] is not TabItem target || ReferenceEquals(target.Content, TabBody)) return;
        if (ReferenceEquals(LockedBody.Child, TabBody)) LockedBody.Child = null;   // reclaim from the locked host
        foreach (var obj in CatTabs.Items)
            if (obj is TabItem ti && ReferenceEquals(ti.Content, TabBody)) { ti.Content = null; break; }
        target.Content = TabBody;
    }

    private bool _typeLocked;   // an EXISTING slice: category tabs + type dropdown hidden (see ApplyTypeLock)

    /// <summary>EXISTING slices are type-locked: the category tabs and type dropdown are hidden and only the
    /// current type's fields show (a static type label replaces the dropdown; a group's sub-option dropdown
    /// stays). Changing category = delete the slice + add a new one. NEW slices (added this session via +
    /// or an exe drop) keep the full picker. TabBody is reparented into a plain bordered host while locked
    /// so the hidden TabControl doesn't own it.</summary>
    private void ApplyTypeLock(bool locked)
    {
        _typeLocked = locked;
        CatTabs.Visibility        = locked ? Visibility.Collapsed : Visibility.Visible;
        LockedBody.Visibility     = locked ? Visibility.Visible   : Visibility.Collapsed;
        TypeBox.Visibility        = locked ? Visibility.Collapsed : Visibility.Visible;
        TypeLockedText.Visibility = locked ? Visibility.Visible   : Visibility.Collapsed;
        // locked == an EXISTING slice (saved at least once) → auto-save mode: hide "Save Slice", it has
        // nothing to do (edits commit immediately). A NEW slice keeps the button until its first save.
        BtnSave.Visibility        = locked ? Visibility.Collapsed : Visibility.Visible;
        // Revert only makes sense once there's a saved snapshot to revert to (EXISTING slices); a NEW
        // slice has never been saved, so Revert stays hidden until the first save locks the type.
        BtnRevert.Visibility      = locked ? Visibility.Visible   : Visibility.Collapsed;
        // The Installed-Game field label duplicates the locked type readout — drop it when locked.
        GameRowLabel.Visibility   = locked ? Visibility.Collapsed : Visibility.Visible;
        if (locked)
        {
            foreach (var obj in CatTabs.Items)
                if (obj is TabItem ti && ReferenceEquals(ti.Content, TabBody)) { ti.Content = null; break; }
            LockedBody.Child = TabBody;
            TypeLockedText.Text = CurrentEntry() is { } lockedEntry ? Loc.T(lockedEntry.Display) : "";
            RemeasureActionRow();   // the static readout replaces the dropdown at a different width
        }
        else
        {
            if (ReferenceEquals(LockedBody.Child, TabBody)) LockedBody.Child = null;
            MoveBodyToSelectedTab();
        }
    }

    /// <summary>Fill the primary dropdown with the selected tab's entries (default to the first), then
    /// sync the secondary dropdown. Programmatic, so SelectionChanged is suppressed via
    /// <see cref="_switchingTab"/>.</summary>
    private void PopulateTypeBoxForCurrentTab()
    {
        int idx = Math.Clamp(CatTabs.SelectedIndex, 0, Categories.Length - 1);
        bool prev = _switchingTab;
        _switchingTab = true;
        try
        {
            TypeBox.Items.Clear();
            foreach (var entry in Categories[idx].Entries)
                if (!entry.Hidden) TypeBox.Items.Add(new ComboBoxItem { Content = Loc.T(entry.Display), Tag = entry });
            if (TypeBox.Items.Count > 0) TypeBox.SelectedIndex = 0;
        }
        finally { _switchingTab = prev; }
        ApplyTypeBoxWidths(Categories[idx].Key);
        SyncSubTypeBox();
    }

    /// <summary>Chat &amp; Streaming's two dropdowns are narrower than the shared 150 px floor (−20% on
    /// the group box, −40% on the option box) — its group names are short, and the row has to leave "Hold to
    /// confirm" its space. Set as a hard Width, not a MinWidth: the point is a CAP, and this category's
    /// longest option ("Join/Leave Voice Channel") exceeds it and is clipped to the box by design. Every
    /// other category goes back to auto-sizing off the 150 floor.</summary>
    private void ApplyTypeBoxWidths(string categoryKey)
    {
        bool chat    = categoryKey == "Chat & Streaming";   // the KEY, never the (translatable) header
        bool radiata = categoryKey == "Radiata";            // both boxes 15% under the floor (Arcade's is the only group)
        TypeBox.Width    = chat ? TypeBoxFloorPx * 0.80 : radiata ? TypeBoxFloorPx * 0.85 : double.NaN;
        SubTypeBox.Width = chat ? TypeBoxFloorPx * 0.60 : radiata ? TypeBoxFloorPx * 0.85 : double.NaN;
        RemeasureActionRow();
    }

    /// <summary>The MinWidth both dropdowns carry in XAML — the width the narrowed pair is measured against.
    /// ⚠ Keep in step with WheelEditorControl.xaml.</summary>
    private const double TypeBoxFloorPx = 150.0;

    // ── "Hold to confirm": second row when the first can't hold it ──────────────
    // Its column is Auto, so it only loses space once the whole row overflows — and what a squeezed Auto
    // column does is CLIP, which takes the label off the right edge and leaves a nameless box. It moves to
    // its own row instead, still right-aligned, whenever the inline layout wouldn't fit.
    //
    // The natural widths are cached rather than measured from the SizeChanged handler: measuring there
    // re-dirties layout on every pass, and a squeezed element's DesiredSize is the squeezed width anyway, so
    // it could never answer "would it fit if I put it back".
    private bool _confirmWrapped;
    private double _natType, _natSub, _natHelp, _natConfirm;

    /// <summary>Re-take the action row's natural widths and re-decide the wrap. Call whenever anything in the
    /// row changes size: the dropdowns repopulating, the locked type readout's text, the audio help chip
    /// appearing.</summary>
    private void RemeasureActionRow()
    {
        static double Natural(FrameworkElement el)
        {
            if (el.Visibility != Visibility.Visible) return 0;
            el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return el.DesiredSize.Width + el.Margin.Left + el.Margin.Right;
        }
        _natType    = Natural(TypeBox) + Natural(TypeLockedText);   // exactly one of the pair is ever visible
        _natSub     = Natural(SubTypeBox);
        _natHelp    = Natural(AudioHelpLink);
        _natConfirm = Natural(ConfirmCheck);
        // The measures above left the row dirty; let layout settle, then judge against the real width.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(ApplyConfirmWrap));
    }

    private void ActionRow_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyConfirmWrap();

    private void ApplyConfirmWrap()
    {
        double avail = ActionRow.ActualWidth;
        if (avail <= 0 || _natConfirm <= 0) return;
        bool wrap = _natType + _natSub + _natHelp + _natConfirm > avail;
        if (wrap == _confirmWrapped) return;   // only touch the tree on an actual flip — no layout loop
        _confirmWrapped = wrap;
        Grid.SetRow(ConfirmCheck, wrap ? 1 : 0);
        Grid.SetColumn(ConfirmCheck, wrap ? 0 : 3);
        Grid.SetColumnSpan(ConfirmCheck, wrap ? 4 : 1);
        ConfirmCheck.HorizontalAlignment = wrap ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        // ⚠ Keep the unwrapped thickness in step with the XAML's — this assignment OVERWRITES it, so a margin
        // edited there alone is inert. It is also counted by RemeasureActionRow (Natural adds margins), which
        // makes a wide gutter here padding that pushes the checkbox onto the next line by itself.
        ConfirmCheck.Margin = wrap ? new Thickness(0, 6, 2, 0) : new Thickness(4, 0, 0, 0);
    }

    /// <summary>Populate + show the secondary dropdown for a multi-option group, or hide it for a
    /// leaf entry. Programmatic, so the SelectionChanged is suppressed via <see cref="_switchingTab"/>.</summary>
    private void SyncSubTypeBox()
    {
        var entry = CurrentEntry();
        if (entry is not null && IsGroup(entry))
        {
            bool prev = _switchingTab;
            _switchingTab = true;
            try
            {
                SubTypeBox.Items.Clear();
                foreach (var opt in entry.Options)
                    if (!opt.Hidden) SubTypeBox.Items.Add(new ComboBoxItem { Content = Loc.T(opt.Display), Tag = opt });
                if (SubTypeBox.Items.Count > 0) SubTypeBox.SelectedIndex = 0;
            }
            finally { _switchingTab = prev; }
            SubTypeBox.Visibility = Visibility.Visible;
        }
        else
        {
            SubTypeBox.Items.Clear();
            SubTypeBox.Visibility = Visibility.Collapsed;
        }
        RemeasureActionRow();
    }

    /// <summary>Stamp the category tab headers from <see cref="Categories"/> so the tab strip can't drift
    /// from the taxonomy. The TabItems are declared in XAML (tab 0 carries the shared TabBody; the rest
    /// are empty shells the body is reparented into). Tab index ↔ Categories index is load-bearing —
    /// PopulateTypeBoxForCurrentTab and SelectTab index straight into the array — so the headers derive
    /// from that same array. A count mismatch traces loudly rather than silently mislabelling tabs.</summary>
    private void SyncCategoryTabHeaders()
    {
        var tabs = CatTabs.Items.OfType<TabItem>().ToList();
        var cats = Categories;
        // SURPLUS tabs are COLLAPSED, not removed. The taxonomy's length is not fixed — it follows
        // Arcade.Available — so the strip carries a placeholder and this hides whatever the taxonomy didn't
        // claim. Collapsed rather than deleted because the gate goes both ways: a removed TabItem couldn't
        // come back without rebuilding the strip. A left-VISIBLE spare would be a blank, clickable, empty
        // tab, which is exactly the evidence a gated-off Arcade must not leave.
        //
        // Index alignment is what makes this safe: the placeholders are last, and every other lookup keys off
        // CatTabs.SelectedIndex into Categories. A collapsed tab can't be selected, so no index can point at
        // one.
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].Visibility = i < cats.Length ? Visibility.Visible : Visibility.Collapsed;
        if (tabs.Count < cats.Length)
            System.Diagnostics.Trace.WriteLine(
                $"[SliceEditor] category tab count ({tabs.Count}) < Categories ({cats.Length}) — " +
                "add a TabItem in WheelEditorControl.xaml to match the taxonomy");
        for (int i = 0; i < tabs.Count && i < cats.Length; i++)
            tabs[i].Header = Loc.T(cats[i].Header);
    }

    /// <summary>A Hidden entry (see TypeEntry.Hidden, e.g. legacy "sequence" slices) is deliberately left
    /// out of PopulateTypeBoxForCurrentTab's normal fill, so it's never OFFERED when picking a type — but
    /// an existing slice already using it must still select correctly (type-locked display, ReadEditorInto,
    /// auto-save all read CurrentEntry()/CurrentType() off TypeBox.SelectedItem). Re-adds it just for that
    /// one selection; it's not reachable from the dropdown's normal open-and-browse list.</summary>
    private void EnsureEntryInTypeBox(TypeEntry entry)
    {
        if (!entry.Hidden) return;
        foreach (var obj in TypeBox.Items)
            if (obj is ComboBoxItem ci && ReferenceEquals(ci.Tag, entry)) return;
        TypeBox.Items.Add(new ComboBoxItem { Content = Loc.T(entry.Display), Tag = entry });
    }

    /// <summary>Same re-add-for-selection trick as <see cref="EnsureEntryInTypeBox"/>, one level down: a
    /// Hidden OPTION (see TypeOption.Hidden, e.g. a legacy nvidia-record/display-extend command) isn't in
    /// SubTypeBox.Items after the normal SyncSubTypeBox fill, so an existing slice using it needs it added
    /// back just for its own selection.</summary>
    private void EnsureOptionInSubTypeBox(TypeOption opt)
    {
        if (!opt.Hidden) return;
        foreach (var obj in SubTypeBox.Items)
            if (obj is ComboBoxItem ci && ReferenceEquals(ci.Tag, opt)) return;
        SubTypeBox.Items.Add(new ComboBoxItem { Content = Loc.T(opt.Display), Tag = opt });
    }

    /// <summary>Select the tab + dropdown(s) for a stored action (used when loading a slice).
    /// For "system" actions the command disambiguates which entry/sub-option to pick.</summary>
    private void SelectTypeOption(string? type, string? command)
    {
        for (int i = 0; i < Categories.Length; i++)
            foreach (var entry in Categories[i].Entries)
                foreach (var opt in entry.Options)
                {
                    if (opt.Alias) continue;   // a second listing of an action owned by another category
                    bool typeMatch = string.Equals(opt.Type, type, StringComparison.OrdinalIgnoreCase);
                    bool cmdMatch;
                    if (string.Equals(type, "system", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(type, "obs", StringComparison.OrdinalIgnoreCase))
                        cmdMatch = string.Equals(opt.Command, command, StringComparison.OrdinalIgnoreCase);
                    else if (string.Equals(type, "arcade", StringComparison.OrdinalIgnoreCase))
                        // arcade matches by command (a multi-option group — otherwise every arcade slice
                        // would select the group's FIRST option regardless of which game it stored). A
                        // command that names no catalogued game — blank/absent (the picker's own storage),
                        // the edit model's "sleep" filler, or an id from a newer build — matches the PICKER
                        // option, exactly how the executor resolves it. It must match SOMETHING here:
                        // falling through to the no-exact-match loop would leave a hidden entry unselected
                        // and let the next auto-save rewrite the slice's type (the docs/ACTIONS.md contract).
                        cmdMatch = string.Equals(opt.Command, command, StringComparison.OrdinalIgnoreCase)
                                   || (opt.Command is null && ArcadeCatalog.Find(command) is null);
                    else
                        cmdMatch = true;
                    if (typeMatch && cmdMatch)
                    {
                        SelectTab(i);
                        EnsureEntryInTypeBox(entry);            // a Hidden entry (e.g. legacy "sequence")
                                                                 // isn't in TypeBox.Items unless re-added here
                        SelectByRef(TypeBox, entry);            // fires TypeBox change → SyncSubTypeBox
                        if (IsGroup(entry))
                        {
                            EnsureOptionInSubTypeBox(opt);       // a Hidden option (e.g. legacy nvidia-record)
                                                                 // isn't in SubTypeBox.Items unless re-added here
                            SelectByRef(SubTypeBox, opt);
                        }
                        return;
                    }
                }
        // No exact match (e.g. unknown system command) → first entry of the matching type, else tab 0.
        for (int i = 0; i < Categories.Length; i++)
            foreach (var entry in Categories[i].Entries)
                if (string.Equals(entry.Options[0].Type, type, StringComparison.OrdinalIgnoreCase))
                {
                    SelectTab(i);
                    SelectByRef(TypeBox, entry);
                    return;
                }
        SelectTab(0);
    }

    /// <summary>Select a tab, ensuring the body + dropdown options follow even when the index is
    /// unchanged (so a same-tab reload still has the right options populated).</summary>
    private void SelectTab(int index)
    {
        if (CatTabs.SelectedIndex == index) { MoveBodyToSelectedTab(); PopulateTypeBoxForCurrentTab(); }
        else CatTabs.SelectedIndex = index;   // fires CatTabs_SelectionChanged → move + populate
    }

    // Tab switch: relocate the body + load that tab's options, and fold the change into the draft.
    // (Bubbled combo SelectionChanged also reaches here; we act only on the TabControl's own change.)
    private void CatTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabBody is null) return;   // fires once during InitializeComponent, before the body exists
        if (e.OriginalSource is not System.Windows.Controls.TabControl) return;   // ignore bubbled combos
        MoveBodyToSelectedTab();
        PopulateTypeBoxForCurrentTab();
        if (!_loading && !_switchingTab) ClearPayloadOnTypeChange(CurrentType());
        UpdateFieldVisibility(CurrentType());
        if (!_loading && !_switchingTab) MarkDirtyFromEditor();
    }

    // Primary dropdown pick: resync the secondary dropdown, then fold into the draft.
    // Handled here so it doesn't bubble to CatTabs.
    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
        if (TabBody is null) return;
        SyncSubTypeBox();
        if (_loading || _switchingTab) return;
        ClearPayloadOnTypeChange(CurrentType());
        ApplyDefaultConfirm();
        UpdateFieldVisibility(CurrentType());
        MarkDirtyFromEditor();
    }

    // Secondary (sub-option) dropdown pick.
    private void SubTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
        if (_loading || _switchingTab) return;
        ClearPayloadOnTypeChange(CurrentType());
        ApplyDefaultConfirm();
        UpdateFieldVisibility(CurrentType());
        MarkDirtyFromEditor();
    }

    /// <summary>A deliberate change of action TYPE wipes every payload field: the previous type's path, URI,
    /// key combo, device, target, message and steps mean nothing to the new type, yet the hidden controls
    /// keep their text and <see cref="ReadEditorInto"/> reads them back into the saved slice. Runs only when
    /// the type actually changes — a sub-option pick inside the same type, and a load, leave stored values
    /// (hidden-but-supported ones included) alone. <c>_loading</c> keeps the clears from folding into the
    /// draft one keystroke at a time; the caller's MarkDirtyFromEditor folds them once.</summary>
    private void ClearPayloadOnTypeChange(string? newType)
    {
        if (_draft is null || newType is null || string.Equals(_draft.ActionType, newType, StringComparison.Ordinal)) return;
        bool prev = _loading;
        _loading = true;
        try
        {
            PathBox.Text = ""; UrlBox.Text = ""; KeysBox.Text = ""; ChatCustomKeyBox.Text = ""; ChatTextBox.Text = "";
            DeviceBox.Text = ""; MicDeviceBox.Text = ""; ObsTargetBox.Text = ""; SequenceBox.Text = "";
            SelectByTag(BehaviorBox, "run");
            _draft.Path = ""; _draft.Url = ""; _draft.Keys = ""; _draft.ChatText = ""; _draft.Device = ""; _draft.MicDevice = "";
            _draft.ObsTarget = ""; _draft.SequenceText = ""; _draft.ProcessName = ""; _draft.QuitIfRunning = false;
        }
        finally { _loading = prev; }
    }

    /// <summary>On a deliberate action change, default the Hold-to-confirm guard for disruptive power
    /// actions. The user can still tick/untick it afterward.</summary>
    private void ApplyDefaultConfirm() => ConfirmCheck.IsChecked = DefaultConfirm(CurrentType(), CurrentCommand());

    private static bool DefaultConfirm(string? type, string? command) =>
        // exit-app guards by TYPE (G8) — closing someone's game deserves the same dwell as power actions.
        type == "exit-app"
        || (type == "system" && command is "sleep" or "reboot" or "shutdown" or "logout" or "hibernate" or "empty-recycle-bin");

    // A legacy slice carrying an unoffered NVIDIA/AMD overlay/record command shows no Keys row (harmless —
    // the executor sends it no preset keys, so the stored Keys value is inert).

    private void LauncherBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => MarkDirtyFromEditor();

    private void EditorField_Changed(object sender, EventArgs e)
    {
        if (_autoLabeling) return;   // ignore the programmatic auto-label write
        // A real keystroke in the label box marks it user-entered, so auto-label leaves it alone.
        if (!_loading && ReferenceEquals(sender, LabelBox)) _labelInferred = false;
        MarkDirtyFromEditor();
    }

    private void ShowLabelCheck_Changed(object sender, RoutedEventArgs e) { if (!_loading && !_showLabelSync) MarkDirtyFromEditor(); }

    /// <summary>For a Discord: Join Voice Channel slice, normalise a pasted channel link to the
    /// discord:// deep-link form when the URL field loses focus.</summary>
    private void UrlBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || CurrentType() != "discord-join") return;
        var norm = NormalizeDiscordUrl(UrlBox.Text);
        if (!string.Equals(norm, UrlBox.Text, StringComparison.Ordinal))
            UrlBox.Text = norm;   // TextChanged → MarkDirtyFromEditor folds it into the draft
    }

    /// <summary>https://discord.com/channels/… or discord.com/channels/… → discord://discord.com/channels/…
    /// (also strips http:// and a leading www.). Already-correct or unrecognised URLs pass through.</summary>
    private static string NormalizeDiscordUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        var s = url.Trim();
        if (s.StartsWith("discord://", StringComparison.OrdinalIgnoreCase)) return s;

        var body = s;
        foreach (var scheme in new[] { "https://", "http://" })
            if (body.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) { body = body[scheme.Length..]; break; }
        if (body.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) body = body[4..];

        return body.StartsWith("discord.com/", StringComparison.OrdinalIgnoreCase)
            ? "discord://" + body
            : url;   // not a recognised discord.com link — leave as typed
    }

    private void ConfirmCheck_Changed(object sender, RoutedEventArgs e) => MarkDirtyFromEditor();

    // ── Inline icon/color editor (always open, below Label, above Action; shares Save Slice) ─────────
    private bool _iconPanelConfiguring;   // suppress the picker's Changed while we (re)seed it
    private Color _iconPanelDefColor;
    private string?  _iconPanelDefIcon;
    private string?  _iconPanelBaselineIcon;   // icon at last sync (to detect a real icon change → drop logo)

    /// <summary>Point the always-open inline picker at the draft's current icon/color, recomputing the
    /// action-type defaults. Called on load AND whenever the action changes, so the preview + palette track
    /// the new default (the whole reason the big preview must update when the Action changes). Never dirties
    /// the draft (Configure runs under the picker's own suppression).</summary>
    private void SyncIconPicker()
    {
        var model = _draft;
        if (model is null) return;
        // Kawaii's slice hue walks the ring, so the well shows THIS slice's own colour. Driven off the
        // live list position + count — the same inputs the wheel lays slices out from — so it tracks
        // reorders and add/remove. No-ops on every other material.
        InlineIconPicker.SetSlicePosition(SliceList.SelectedIndex, _slices.Count);
        var slice = model.ToSlice();
        // Everything handed to the picker is AUTHORED and MATERIAL-NEUTRAL; the picker applies the material
        // variant itself for display (ActionTint.MaterialVariant), and hands the authored value straight back
        // for saving. That's the whole point: SelectedColor round-trips into config, so a display transform
        // baked in here would re-boost the stored colour on every save — and, worse, a material-dependent
        // default would make the "equals the default → store null (inherit)" comparison below material-
        // dependent too, so the same typed hex would collapse to inherit on one material and persist as an
        // override on another.
        var defColor = ActionTint.EffectiveColor(slice.Action?.Type?.ToLowerInvariant(), slice.Action?.Command);
        bool hasCustom = slice.IconColor is not null && ActionTint.TryParseHex(slice.IconColor, out var authored);
        var curColor = hasCustom ? authored : defColor;
        var defIcon  = ActionIcons.Resolve(slice.Action);
        var curIcon  = model.IconName ?? defIcon;
        // Exactness comes from the shared legacy-aware helper, never re-derived here — a legacy slice's
        // legacy IconColorLight pairing is what tells us it was a swatch pick.
        bool exact = hasCustom && ActionTint.IsExactColor(slice);

        _iconPanelDefColor     = defColor;
        _iconPanelDefIcon      = defIcon;
        _iconPanelBaselineIcon = curIcon;

        // The refresh/fetch button only appears once the slice has been SAVED as an installed-game slice
        // (i.e. the COMMITTED slice is installed-game with a URL) — not on a fresh/unsaved one, where the
        // logo is pulled automatically on game selection anyway.
        bool savedGame = _committed?.ActionType == "installed-game" && !string.IsNullOrWhiteSpace(_committed.Url);

        // A launch slice with no icon name of its own wears the app's OWN icon on the wheel; the well shows
        // that bitmap rather than the glyph the action type would otherwise default to. The decision is
        // SliceEditModel.WearsAppIcon, shared with the preview list, so the two never disagree.
        var appIcon = SliceEditModel.WearsAppIcon(model.LogoPath, model.IconName, model.ActionType, model.Path)
                      ? IconCache.Get(model.Path)
                      : null;

        _iconPanelConfiguring = true;
        InlineIconPicker.Configure(curColor, curIcon, defColor, defIcon,
            showFetchLogo: savedGame,
            logoPlaceholder: !string.IsNullOrWhiteSpace(model.LogoPath),
            exact: exact,
            appIcon: appIcon);
        SyncLogoCycle(model);
        _iconPanelConfiguring = false;

        UpdateShowLabelEnabled();
    }

    /// <summary>Point the well at the draft's logo and, when there's more than one logo on disk for this
    /// game, at the ◀ ▶ cycle over them. Deliberately narrow: the cycle is offered ONLY for a logo the art
    /// cache owns, so an image the user supplied themselves is previewed but can't be cycled away from
    /// (there'd be no way back to it). A logo that fails to load leaves the old placeholder in place.</summary>
    private void SyncLogoCycle(SliceEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.LogoPath) || GameArt.LoadFromFile(model.LogoPath) is not { } img)
        {
            InlineIconPicker.SetLogo(null);
            return;
        }
        if (!GameArt.IsCachedLogo(model.LogoPath))
        {
            InlineIconPicker.SetLogo(img);   // preview it, no arrows
            return;
        }
        var set = GameArt.GetDownloadedLogos(GameForLogo(model));
        int idx = -1;
        for (int i = 0; i < set.Count; i++)
            if (string.Equals(set[i], model.LogoPath, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
        // Not in the set = this exact file was de-duplicated away (an identical copy under another name won
        // the slot). Anchor on the winner so the arrows still have somewhere to step from.
        if (idx < 0 && set.Count > 0 && SameLogoContent(model.LogoPath!, set)is { } dup) idx = dup;
        InlineIconPicker.SetLogo(img, set, idx);
    }

    /// <summary>Index of the set entry whose bytes match <paramref name="path"/>, or null.</summary>
    private static int? SameLogoContent(string path, IReadOnlyList<string> set)
    {
        try
        {
            var want = System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path));
            for (int i = 0; i < set.Count; i++)
                if (System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(set[i]))
                        .AsSpan().SequenceEqual(want)) return i;
        }
        catch { /* unreadable — no anchor, arrows stay hidden */ }
        return null;
    }

    /// <summary>◀ ▶ in the well: adopt the chosen downloaded logo. Auto-saves like every other slice edit.</summary>
    private void OnInlineLogoCycled(string path)
    {
        var model = _draft;
        if (model is null || _iconPanelConfiguring) return;
        model.LogoPath = path;
        RecomputeDirty();     // existing slice → commits + refreshes the list row
        SyncIconPicker();     // repaint the well with the newly chosen logo
    }

    /// <summary>An image dropped on the icon well becomes this slice's logo. Works on ANY slice, not just a
    /// game — the wheel renders LogoPath for all of them — so a launcher or script slice can carry its own
    /// artwork. The file is COPIED into the art cache (see GameArt.ImportUserLogo), so moving or deleting
    /// the original later doesn't blank the slice.</summary>
    private void OnInlineLogoFileDropped(string path)
    {
        var model = _draft;
        if (model is null) return;

        var result = GameArt.ImportUserLogo(path);
        if (result.Error is { } err)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(this), err, "Radiata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (result.Path is null) return;

        model.LogoPath = result.Path;
        // A dropped image supersedes a glyph choice, exactly like a fetched logo does.
        RecomputeDirty();
        SyncIconPicker();
    }

    /// <summary>A game slice with a custom logo always hides its text label (the logo carries the name),
    /// so the Show Label toggle is greyed out and non-interactive while a logo is set.</summary>
    private bool _showLabelSync;   // suppress ShowLabelCheck_Changed while we set its state programmatically

    private void UpdateShowLabelEnabled()
    {
        if (ShowLabelCheck is null) return;   // the host can set ShowSliceLabelsMode before the template exists
        // The checkbox only appears in the per-slice mode (see ShowSliceLabelsMode). The state work below
        // still runs while it's hidden, deliberately: ReadEditorInto reads model.ShowLabel back OFF this
        // checkbox, so leaving it unsynced would write the wrong flag on the next auto-save and silently lose
        // the user's per-slice choices the moment they switched away from "Slices I Choose".
        ShowLabelCheck.Visibility = SliceLabelRule.Normalize(_showSliceLabelsMode) == "selected"
            ? Visibility.Visible : Visibility.Collapsed;

        bool enabled = _draft is null || string.IsNullOrWhiteSpace(_draft.LogoPath);
        _showLabelSync = true;
        ShowLabelCheck.IsEnabled = enabled;
        if (enabled)
        {
            ShowLabelCheck.IsChecked = _draft?.ShowLabel ?? true;
            ShowLabelCheck.Opacity   = 1.0;
        }
        else
        {
            // Custom-logo game: the label is always hidden, so the toggle is n/a — show the dash mark
            // (IsChecked = null → the template's dash) and fade the whole control (box + text).
            ShowLabelCheck.IsChecked = null;
            ShowLabelCheck.Opacity   = 0.5;
        }
        _showLabelSync = false;
    }

    /// <summary>A live edit in the inline picker — fold it into the draft and refresh (shares Save Slice).</summary>
    private void OnInlineIconChanged()
    {
        if (_iconPanelConfiguring) return;
        var model = _draft;
        if (model is null) return;

        // ONE authored colour + one exactness flag. _iconPanelDefColor is the material-NEUTRAL type
        // default, so "equals the default" means the same thing on every material — matching it stores
        // null (inherit) and the wheel derives the default's own variant. IconColorExact is written
        // EXPLICITLY rather than left null for a swatch pick: null on a slice with no legacy IconColorLight
        // reads as EXACT (the legacy interpretation — see ActionTint.IsExactColor), so omitting it would
        // silently make every swatch pick paint verbatim on dark materials.
        bool inherits = InlineIconPicker.SelectedColor == _iconPanelDefColor;
        model.IconColor      = inherits ? null : ActionTint.ToHex(InlineIconPicker.SelectedColor);
        model.IconColorExact = inherits ? null : InlineIconPicker.IsExactColor;
        model.IconName  = string.IsNullOrEmpty(InlineIconPicker.SelectedIcon)
                          || string.Equals(InlineIconPicker.SelectedIcon, _iconPanelDefIcon, StringComparison.OrdinalIgnoreCase)
                          ? null : InlineIconPicker.SelectedIcon;
        // If the slice carried a fetched logo and the user actually CHANGED the icon (not just the colour),
        // drop the logo so their chosen glyph shows on the wheel. Colour-only edits keep + retint the logo.
        bool logoDropped = !string.IsNullOrWhiteSpace(model.LogoPath)
            && !string.Equals(InlineIconPicker.SelectedIcon ?? "", _iconPanelBaselineIcon ?? "", StringComparison.OrdinalIgnoreCase);
        if (logoDropped) model.LogoPath = null;
        RecomputeDirty();
        if (logoDropped) SyncIconPicker();   // logo gone → clear the placeholder, show the chosen glyph
    }

    /// <summary>Fetch Logo in the inline picker: pull the game's transparent logo (SGDB, the Game Grid's
    /// source) into the draft — it supersedes any glyph choice.</summary>
    private async void OnInlineFetchLogo()
    {
        var model = _draft;
        if (model is null) return;
        await FetchLogoIntoDraftAsync(model);
        RecomputeDirty();
        await InlineIconPicker.FinishLogoBusyAsync();   // stop the spin → checkmark → back to ↻
    }

    /// <summary>Reset to Default also restores the slice's LABEL to its type default. A free-text type
    /// has no inferred default, so its label is left untouched.</summary>
    private void ResetLabelToDefault()
    {
        var model = _draft;
        if (model is null) return;
        var inferred = InferLabel(model.ActionType);
        if (string.IsNullOrWhiteSpace(inferred)) return;
        _autoLabeling = true;
        try { LabelBox.Text = inferred; _labelInferred = true; }
        finally { _autoLabeling = false; }
        MarkDirtyFromEditor();   // fold the label into the draft + recompute dirty
    }

    /// <summary>Refresh the inline picker's preview to the slice's effective icon/color — the always-open
    /// picker is the only preview; it shows the effective glyph tinted, and the wheel itself renders any
    /// fetched logo.</summary>
    private void UpdateIconColorDisplay(SliceEditModel model) => SyncIconPicker();

    // ── Draft model: edits land in _draft; nothing persists to the slice until "Save Slice" ─────

    /// <summary>Read the editor controls into a model (field values + type/command). Does not touch
    /// IconName/IconColor (those live on the draft, set by the picker).</summary>
    private void ReadEditorInto(SliceEditModel model)
    {
        model.Label       = _labelShown is not null && string.Equals(LabelBox.Text, _labelShown, StringComparison.Ordinal) ? _labelRaw ?? LabelBox.Text : LabelBox.Text;
        model.ActionType  = CurrentType() ?? "launch";
        model.Path        = PathBox.Text;
        model.QuitIfRunning = SelectedTag(BehaviorBox) == "toggle";
        // model.ProcessName is deliberately NOT written here. There is no "Process Name" control any more,
        // but the executor still HONOURS a stored process override — leaving the draft's value untouched
        // is what preserves it: assigning from a removed control would silently blank it on the next save.
        model.Command     = CurrentType() switch
        {
            "launcher"      => SelectedTag(LauncherBox) ?? "steam",
            "system" or "obs" => CurrentCommand() ?? "sleep",
            "text-chat"     => SelectedTag(ChatButtonBox) ?? "default",
            // The selected game's id (or "" = the picker) — SliceEditModel.ToSlice persists it, so it
            // must reflect the pick.
            "arcade"        => CurrentCommand() ?? "",
            _               => model.Command,
        };
        model.Url         = model.ActionType == "installed-game"
            ? (((GameBox.SelectedItem as ComboBoxItem)?.Tag as InstalledGame) is { } pick
                   ? pick.DirectLaunchUrl ?? pick.LaunchUrl : model.Url)
            : UrlBox.Text;
        // text-chat stores its custom key in the same Keys field the generic keypress types use — it just
        // reads from ChatCustomKeyBox instead of KeysBox (only one of which is ever visible). "Use Game
        // Default" mode writes "" here; the fire-time lookup never touches Keys in that mode anyway.
        model.Keys        = CurrentType() == "text-chat"
            ? (SelectedTag(ChatButtonBox) == "custom" ? ChatCustomKeyBox.Text : "")
            : KeysBox.Text;
        model.Device      = DeviceBox.Text;
        model.MicDevice   = MicDeviceBox.Text;
        model.ObsTarget   = ObsTargetBox.Text;
        model.ChatText    = ChatTextBox.Text;
        model.Level       = LevelBox.Text;
        if (CurrentType() == "system" && CurrentCommand() == "power-plan")
        {
            model.PowerPlan  = (PowerPlanBox.SelectedItem  as ComboBoxItem)?.Tag as string ?? model.PowerPlan;
            model.PowerPlanB = (PowerPlanBoxB.SelectedItem as ComboBoxItem)?.Tag as string ?? model.PowerPlanB;
        }
        if (CurrentType() == "system" && CurrentCommand() == "reboot")
            model.LogInAfterReboot = RebootLoginCheck.IsChecked != false;   // indeterminate never happens; be safe
        model.SequenceText = SequenceBox.Text;
        model.RequireConfirm = ConfirmCheck.IsChecked == true;
        // `?? model.ShowLabel`, not `== true`: the checkbox goes INDETERMINATE (IsChecked null) for a
        // custom-logo slice, where the toggle is n/a — `== true` would silently rewrite a stored true to
        // false there. Null means "no answer, keep what's stored": a control that isn't answering must
        // never be the source of the saved value.
        model.ShowLabel      = ShowLabelCheck.IsChecked ?? model.ShowLabel;
    }

    /// <summary>An editor control changed: fold it into the draft, refresh the live preview/hint, and
    /// recompute whether "Save Slice" should light up. Does NOT persist anything.</summary>
    private void MarkDirtyFromEditor()
    {
        if (_loading || _draft is null) return;
        MaybeAutoLabel();              // may rewrite LabelBox → re-enters here; harmless (idempotent)
        ReadEditorInto(_draft);
        UpdateIconColorDisplay(_draft);
        UpdateValidationHint();
        RecomputeDirty();
    }

    private void RecomputeDirty()
    {
        bool rawDirty = _committed is not null && _draft is not null && !SameSlice(_draft, _committed);

        // EXISTING slice (already saved once) → auto-save: fold the edit into the committed slice through
        // the SAME commit path "Save Slice" uses (CommitDraft), reusing its Changed event so the host's
        // debounced window-level save still fires. Gated on _actionValid so an incomplete edit (e.g. a
        // required field just cleared) doesn't get committed.
        // A NEW (never-saved) slice is untouched: it keeps the explicit Save/Discard flow below.
        if (rawDirty && _committed is not null && !_committed.IsNew && _actionValid)
        {
            CommitDraft();
            _draft = _committed.Clone();
            rawDirty = false;
            RefreshKeepingSelection();   // keep the list row's label/icon in sync with the auto-saved edit
        }

        _dirty = rawDirty;
        UpdateButtonStates();
    }

    /// <summary>Save lights up only for a NEW, not-yet-saved slice (existing slices auto-save and hide the
    /// button — see <see cref="ApplyTypeLock"/>). Revert for an EXISTING slice reflects whether the
    /// committed slice has drifted from the selection-time snapshot; for a NEW slice it mirrors dirty,
    /// same as before.</summary>
    private void UpdateButtonStates()
    {
        bool existing = _committed is not null && !_committed.IsNew;
        bool revertEnabled = existing
            ? _committed is not null && _selectionSnapshot is not null && !SameSlice(_committed, _selectionSnapshot)
            : _dirty;
        SetDirtyButtons(_dirty, revertEnabled);
    }

    /// <summary>Value-equality of two edit models via their serialized slice form.</summary>
    private static bool SameSlice(SliceEditModel a, SliceEditModel b) =>
        JsonSerializer.Serialize(a.ToSlice()) == JsonSerializer.Serialize(b.ToSlice());

    /// <summary>True while the current slice has edits not yet committed via "Save Slice".</summary>
    public bool HasUnsavedChanges => _dirty;

    private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveSlice();

    /// <summary>For a NEW (never-saved) slice: discard the draft's unsaved edits, reloading the editor from
    /// the committed slice (unchanged behavior). For an EXISTING slice (auto-save mode, where _committed is
    /// overwritten on every keystroke): restore the slice to its state at SELECTION time — the snapshot
    /// taken when it was last picked in the list — and auto-save that restored state through the same
    /// Changed path every other edit uses.</summary>
    private void BtnRevert_Click(object sender, RoutedEventArgs e)
    {
        if (_committed is null) return;
        if (!_committed.IsNew && _selectionSnapshot is not null)
        {
            _selectionSnapshot.CopyValuesInto(_committed);
            LoadIntoEditor(_committed);              // rebuild draft + a fresh snapshot from the restored state
            RefreshKeepingSelection();                // list row (label/icon) reflects the restored values
            Changed?.Invoke(this, EventArgs.Empty);   // persist the revert — same auto-save Changed path
        }
        else
        {
            LoadIntoEditor(_committed);   // new slice: rebuilds the draft from committed + clears dirty
        }
    }

    /// <summary>Commit the draft into the committed slice + persist (debounced by the host).</summary>
    private void SaveSlice()
    {
        if (!_dirty || _committed is null) return;
        CommitDraft();
        RefreshKeepingSelection();
        _draft = _committed.Clone();
        // This is the slice's FIRST save (new → existing): rebase the selection snapshot to the just-saved
        // values, so a later Revert (now in auto-save mode) targets "as of first save," not the blank
        // pre-edit state captured when the still-new slice was selected.
        _selectionSnapshot = _committed.Clone();
        SetDirtyButtons(false);
        ApplyTypeLock(true);   // saving locks the slice's type (change category = delete + re-add)
        SyncIconPicker();   // now committed — reveal the fetch/refresh button for a saved installed-game slice
        KickFirstSaveLogoFetch(_committed);
    }

    /// <summary>First save of an Installed Game slice: fetch its transparent logo (same SteamGridDB
    /// path the Game Grid uses) in the background and persist it when it lands. Shared by Save Slice
    /// and the leave-slice prompt's Yes branch so both commit paths behave identically.</summary>
    private void KickFirstSaveLogoFetch(SliceEditModel committed)
    {
        if (committed.ActionType == "installed-game"
            && string.IsNullOrWhiteSpace(committed.LogoPath)
            && !string.IsNullOrWhiteSpace(committed.Url))
            _ = FetchLogoIntoAsync(committed);
    }

    /// <summary>Dirty-state buttons. Save (NEW slices only — existing ones auto-save and hide it) commits
    /// the draft and additionally requires the action to VALIDATE (required fields populated — see
    /// UpdateValidationHint). Revert: for a NEW slice it just needs dirt (this 1-arg overload); for an
    /// EXISTING slice its enablement is "committed differs from the selection snapshot" — computed by
    /// <see cref="UpdateButtonStates"/>, which passes it via the 2-arg overload. Save's label goes bold
    /// while it's active as an extra "there's something to save" cue; Revert stays Normal.</summary>
    private void SetDirtyButtons(bool dirty) => SetDirtyButtons(dirty, dirty);

    private void SetDirtyButtons(bool dirty, bool revertEnabled)
    {
        _lastDirty = dirty;
        if (BtnSave   is not null) { BtnSave.IsEnabled = dirty && _actionValid; BtnSave.FontWeight = dirty && _actionValid ? FontWeights.Bold : FontWeights.Normal; }
        if (BtnRevert is not null) { BtnRevert.IsEnabled = revertEnabled; BtnRevert.FontWeight = FontWeights.Normal; }
    }
    private bool _lastDirty;      // last dirty state passed in — lets a validity change re-gate Save
    private bool _actionValid = true;   // ValidateCurrent's verdict (empty required fields ⇒ false)

    // ── Installed-game logo fetch (SteamGridDB — the SQUAREST candidate, not the grid's pick) ───────

    /// <summary>The game identity to fetch art for: the GameBox's selected game when it matches the
    /// model's URL (carries the true storefront), else one rebuilt from the model (storefront inferred
    /// from the launch URL's scheme — enough for the art lookup's cache key + Steam-CDN gate).</summary>
    private InstalledGame GameForLogo(SliceEditModel m) =>
        (GameBox.SelectedItem as ComboBoxItem)?.Tag is InstalledGame g
            && (g.LaunchUrl == m.Url || g.DirectLaunchUrl == m.Url)
        ? g
        : new InstalledGame(m.Label, m.Url, GameLibrary.StorefrontFromUrl(m.Url));

    /// <summary>Fetch the game logo for a COMMITTED slice in the background and persist it when it
    /// lands. Skips silently when no source has a logo; never overwrites a logo set meanwhile.</summary>
    private async Task FetchLogoIntoAsync(SliceEditModel target)
    {
        try
        {
            var game = GameForLogo(target);            // resolve on the UI thread, before any await
            var path = await GameArt.GetSliceLogoPathAsync(game);
            if (path is null || !string.IsNullOrWhiteSpace(target.LogoPath)) return;
            target.LogoPath = path;
            if (ReferenceEquals(target, _committed) && _draft is not null && string.IsNullOrWhiteSpace(_draft.LogoPath))
            {
                _draft.LogoPath = path;                // keep the open draft in sync (not a user edit)
                UpdateIconColorDisplay(_draft);
            }
            SliceList.Items.Refresh();
            Changed?.Invoke(this, EventArgs.Empty);    // persist the fetched logo
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Editor] logo fetch failed: {ex.Message}"); }
    }

    /// <summary>"Fetch Logo" from the icon picker: fetch into the DRAFT (commits with Save Slice).</summary>
    private async Task FetchLogoIntoDraftAsync(SliceEditModel draft)
    {
        try
        {
            var game = GameForLogo(draft);
            var path = await GameArt.GetSliceLogoPathAsync(game);
            if (!ReferenceEquals(draft, _draft)) return;   // user moved to another slice meanwhile
            if (path is null) return;                       // no logo — leave the current icon/color as-is
            draft.LogoPath = path;
            UpdateIconColorDisplay(draft);
            RecomputeDirty();
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Editor] logo fetch failed: {ex.Message}"); }
    }

    /// <summary>Copy the draft's values onto the committed slice and fire Changed (persist). No UI
    /// side-effects — safe to call mid-selection-change.</summary>
    private void CommitDraft()
    {
        if (_committed is null || _draft is null) return;
        ReadEditorInto(_draft);                 // capture any last edits
        _draft.CopyValuesInto(_committed);
        _committed.IsNew = _draft.IsNew = false;   // committed once → type-locked from now on (s2)
        _dirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshKeepingSelection()
    {
        var keep = SliceList.SelectedItem;
        _revertingSelection = true;
        SliceList.Items.Refresh();
        SliceList.SelectedItem = keep;
        _revertingSelection = false;
    }

    /// <summary>The name currently typed in the editor (what the slice will be called if saved),
    /// falling back to <paramref name="fallback"/> when the field is blank.</summary>
    private string CurrentLabelOr(string? fallback)
    {
        var t = LabelBox.Text?.Trim();
        return string.IsNullOrEmpty(t) ? (fallback ?? Loc.T(UiText.Editor.ThisSlice)) : t;
    }

    private enum LeaveChoice { Save, Discard, Stay }

    /// <summary>Prompt when leaving a slice with unsaved edits. A VALID edit offers Save / Discard / Stay.
    /// An INVALID one (a required field empty/malformed — <paramref name="valid"/> false) offers NO save
    /// path: an incomplete slice must never be committed to disk (the visible Save button already blocks
    /// it, so this closes the leave/close back door), so the choice is only Discard / Stay.</summary>
    private static LeaveChoice PromptUnsaved(string label, bool valid)
    {
        if (valid)
        {
            var r = System.Windows.MessageBox.Show(
                Loc.F(UiText.Editor.SaveChangesPrompt, label),
                Loc.T(UiText.Editor.UnsavedCaption), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return r == MessageBoxResult.Yes ? LeaveChoice.Save
                 : r == MessageBoxResult.No  ? LeaveChoice.Discard
                 :                             LeaveChoice.Stay;
        }
        var d = System.Windows.MessageBox.Show(
            Loc.F(UiText.Editor.IncompletePrompt, label),
            Loc.T(UiText.Editor.IncompleteCaption), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        return d == MessageBoxResult.OK ? LeaveChoice.Discard : LeaveChoice.Stay;
    }

    /// <summary>Used by the host when leaving the whole editor (tab switch / window close). Returns
    /// false only if the user chose Cancel (caller should abort the navigation).</summary>
    public bool TryCommitOrDiscard()
    {
        if (!_dirty || _committed is null) return true;
        var choice = PromptUnsaved(CurrentLabelOr(_committed.Label), _actionValid);
        if (choice == LeaveChoice.Stay) return false;
        if (choice == LeaveChoice.Save) SaveSlice();   // valid-only (an invalid edit never returns Save)
        else if (_committed.IsNew)                     // discard of a never-saved slice: nothing to revert to
        {
            DropNeverSavedSlice(_committed, null);
            LoadIntoEditor(SliceList.SelectedItem as SliceEditModel);
        }
        else LoadIntoEditor(_committed);               // discard: revert the editor to the committed slice
        return true;
    }

    /// <summary>Remove a slice that was added but never saved (Discard on its unsaved-changes prompt) so no
    /// empty placeholder is left on the wheel. <paramref name="reselect"/> is the slice the user was moving
    /// to, if any; otherwise the neighbour at the same index is selected. Guarded so the list mutation
    /// can't re-enter <see cref="SliceList_SelectionChanged"/>.</summary>
    private void DropNeverSavedSlice(SliceEditModel model, SliceEditModel? reselect)
    {
        int idx = _slices.IndexOf(model);
        if (idx < 0) return;
        _dirty = false;
        SetDirtyButtons(false);
        _revertingSelection = true;
        try
        {
            _slices.RemoveAt(idx);
            RenumberAndRefresh(Math.Min(idx, _slices.Count - 1));
            if (reselect is not null) SliceList.SelectedItem = reselect;
        }
        finally { _revertingSelection = false; }
        _committed = null; _draft = null; _selectionSnapshot = null;
        UpdateSliceButtons();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>If the label is still the untouched default, infer one from the chosen action type
    /// and its sub-options. Only overwrites "New Slice" — once the user types anything, we never
    /// touch it. The programmatic write is guarded so it doesn't recurse through MarkDirtyFromEditor.</summary>
    private void MaybeAutoLabel()
    {
        if (_autoLabeling) return;
        // Overwrite the untouched default OR a previously-inferred label, but never a user-typed one.
        if (!_labelInferred && !string.Equals(LabelBox.Text, NewSliceLabel, StringComparison.Ordinal)) return;
        var inferred = InferLabel(CurrentType());
        if (string.IsNullOrWhiteSpace(inferred)) return;
        _autoLabeling = true;
        try { LabelBox.Text = inferred; _labelInferred = true; }
        finally { _autoLabeling = false; }
    }

    /// <summary>Inferred label for EVERY selectable action type, from the current editor selections —
    /// the single default-label map (extend here, not in a parallel switch, when a new type is added).
    /// A type with no entry falls through to null, which leaves the label untouched.</summary>
    private string? InferLabel(string? type) => type switch
    {
        "launcher"       => LauncherCatalog.Find(SelectedTag(LauncherBox))?.ShortName,
        "system"         => SystemCommandLabel(CurrentCommand()),
        "obs"            => ObsCommandLabel(CurrentCommand()),
        // No game/app chosen yet → "Launch" (F7): browsing types leaves the previous type's inferred
        // label behind otherwise, which reads as a mislabeled slice until a pick is made.
        "installed-game" => (GameBox.SelectedItem as ComboBoxItem)?.Tag is InstalledGame g ? g.Name : UiText.DefaultLabels.Launch,
        "game-browser"   => UiText.DefaultLabels.GameGrid,
        // A specific game names itself; the picker (blank command) is just "Arcade".
        "arcade"         => ArcadeCatalog.Find(CurrentCommand())?.Title ?? UiText.DefaultLabels.Arcade,
        "exit-app"       => UiText.DefaultLabels.ExitCurrentApp,
        "disable-wheels" => UiText.DefaultLabels.DisableWheels,
        "safe-mode"      => UiText.DefaultLabels.PassthruMode,
        "xbox-emulation" => UiText.DefaultLabels.XboxMode,
        "dualshock-emulation" => UiText.DefaultLabels.DualShockMode,
        "settings"       => UiText.DefaultLabels.Settings,
        "discord-launch" => UiText.DefaultLabels.Discord,
        "discord-join"   => UiText.DefaultLabels.VoiceChat,
        "discord-mute"   => UiText.DefaultLabels.Mute,
        "discord-deafen" => UiText.DefaultLabels.Deafen,
        "steam-mute"     => UiText.DefaultLabels.SteamMic,
        "launch"         => InstalledApps.NameFor(PathBox.Text)                                          // Installed Apps pick → display name
                            ?? Nz(System.IO.Path.GetFileNameWithoutExtension(PathBox.Text)) ?? UiText.DefaultLabels.Launch,   // F7
        "switch-audio"   => Nz(DeviceBox.Text),
        "keypress"       => Nz(KeysBox.Text),
        "url"            => UiText.DefaultLabels.OpenUri,
        "text-chat"      => UiText.DefaultLabels.TextChat,
        "script"         => UiText.DefaultLabels.RunScript,
        "sequence"       => UiText.DefaultLabels.Sequence,
        _                => null,
    };

    private static string? SystemCommandLabel(string? cmd) => cmd switch
    {
        "sleep"               => UiText.DefaultLabels.Sleep,
        "hibernate"           => UiText.DefaultLabels.Hibernate,
        "reboot"              => UiText.DefaultLabels.Reboot,
        "shutdown"            => UiText.DefaultLabels.ShutDown,
        "logout"              => UiText.DefaultLabels.LogOut,
        "volume"              => UiText.DefaultLabels.Mute,
        "mic-mute"            => UiText.DefaultLabels.MicMute,
        "media-play-pause"    => UiText.DefaultLabels.PlayPause,
        "media-next"          => UiText.DefaultLabels.NextTrack,
        "media-prev"          => UiText.DefaultLabels.PreviousTrack,
        "media-mute"          => UiText.DefaultLabels.MediaMute,
        "lock"                => UiText.DefaultLabels.Lock,
        "show-desktop"        => UiText.DefaultLabels.ShowDesktop,
        "hdr-toggle"          => UiText.DefaultLabels.Hdr,
        "focus-assist"        => UiText.DefaultLabels.DoNotDisturb,
        "volume-set"          => UiText.DefaultLabels.Volume,
        "display-toggle"      => UiText.DefaultLabels.ExtendCloneDisplay,
        // No labels for display-extend/clone/external/internal or the nvidia-*/amd-* commands: they're out
        // of the taxonomy (see Categories). A legacy slice using one keeps its saved LABEL — this map only
        // feeds new/inferred ones.
        "steam-chat-open"     => UiText.DefaultLabels.SteamChat,
        "xbox-party-open"     => UiText.DefaultLabels.PartyChat,
        "xbox-party-mute"     => UiText.DefaultLabels.PartyMic,
        "gamebar-open"        => UiText.DefaultLabels.GameBar,
        "gamebar-screenshot"  => UiText.DefaultLabels.GameBarScreenshot,
        "gamebar-record"      => UiText.DefaultLabels.GameBarRecording,
        "gamebar-record-last" => UiText.DefaultLabels.RecordLast30Sec,
        "gamebar-mic"         => UiText.DefaultLabels.GameBarMic,
        "power-plan"          => UiText.DefaultLabels.PowerPlan,
        "empty-recycle-bin"   => UiText.DefaultLabels.RecycleBin,
        _                     => null,
    };

    private static string? ObsCommandLabel(string? cmd) => cmd switch
    {
        "obs-toggle-stream" => UiText.DefaultLabels.ToggleStreaming,
        "obs-toggle-record" => UiText.DefaultLabels.ToggleRecording,
        "obs-save-replay"   => UiText.DefaultLabels.SaveReplayBuffer,
        "obs-set-scene"     => UiText.DefaultLabels.SwitchScene,
        "obs-toggle-mic"    => UiText.DefaultLabels.ToggleSourceMute,
        _                   => UiText.DefaultLabels.Obs,
    };

    private static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void UpdateFieldVisibility(string? type)
    {
        bool isLaunch        = type is "launch";
        bool isScript        = type is "script";
        bool isUrl           = type is "url";
        bool isKeys          = type is "keypress";
        bool isDiscordVoice  = type is "discord-mute" or "discord-deafen";   // Discord's own switch, over RPC
        bool isSteamKey      = type is "steam-mute";
        bool isAudio         = type is "switch-audio";
        bool isDiscordJoin   = type is "discord-join";
        bool isInstalledGame = type is "installed-game";
        bool isSequence      = type is "sequence";
        bool isLauncher      = type is "launcher";
        bool isObs           = type is "obs";
        bool isTextChat      = type is "text-chat";

        // Every RPC-driven Discord action needs the user's own credentials first. For Join/Leave that swaps
        // the URL row for the prompt; Deafen and Mute Me have no field of their own, so the prompt is the
        // whole editor body until the credentials are in.
        bool discordNeedsSetup = (isDiscordJoin || isDiscordVoice) && !DiscordOAuth.IsConfigured;

        PathRow.Visibility     = isLaunch || isScript ? Visibility.Visible : Visibility.Collapsed;
        BtnBrowsePath.Content  = Loc.T(isScript ? UiText.Editor.BrowseForScript : UiText.Editor.BrowseForApp);
        BtnPickApp.Visibility  = isScript ? Visibility.Collapsed : Visibility.Visible;   // apps only — scripts are files
        BehaviorRow.Visibility = isLaunch             ? Visibility.Visible : Visibility.Collapsed;
        UrlRow.Visibility     = isUrl || (isDiscordJoin && !discordNeedsSetup) ? Visibility.Visible : Visibility.Collapsed;
        DiscordConfigRow.Visibility = discordNeedsSetup ? Visibility.Visible : Visibility.Collapsed;
        UrlLabel.Text         = Loc.T(isDiscordJoin ? UiText.Editor.DiscordUrl : UiText.Editor.Uri);
        KeysRow.Visibility    = isKeys || isSteamKey ? Visibility.Visible : Visibility.Collapsed;
        KeysLabel.Text        = Loc.T(isSteamKey ? UiText.Editor.SteamVoiceHotkey : UiText.Editor.KeyCombo);
        // The (?) chip is keypress-only: the shared Steam key field carries its own hint/topic.
        KeysHelpLink.Visibility = isKeys ? Visibility.Visible : Visibility.Collapsed;
        KeysHint.Text         = Loc.T(isSteamKey ? UiText.Editor.SteamHotkeyHint : UiText.Editor.KeyComboHint);
        DeviceRow.Visibility  = isAudio                           ? Visibility.Visible : Visibility.Collapsed;
        // (No device-list preload any more — the ▾ buttons enumerate on click.)
        if (isAudio && _draft is not null) SeedCurrentAudioDevices(_draft);
        GameRow.Visibility    = isInstalledGame                   ? Visibility.Visible : Visibility.Collapsed;
        SequenceRow.Visibility = isSequence                       ? Visibility.Visible : Visibility.Collapsed;
        LauncherRow.Visibility = isLauncher                       ? Visibility.Visible : Visibility.Collapsed;
        AudioHelpLink.Visibility = isAudio                        ? Visibility.Visible : Visibility.Collapsed;
        RemeasureActionRow();   // the help chip joining/leaving the action row changes what fits on it
        DiscordUrlTip.Visibility = isDiscordJoin && !discordNeedsSetup ? Visibility.Visible : Visibility.Collapsed;
        bool isPowerPlan = type is "system" && CurrentCommand() is "power-plan";
        PowerPlanRow.Visibility = isPowerPlan                     ? Visibility.Visible : Visibility.Collapsed;
        if (isPowerPlan) SelectPowerPlan(_draft?.PowerPlan, _draft?.PowerPlanB);
        // Log In after Reboot — reboot slices only.
        RebootRow.Visibility = type is "system" && CurrentCommand() is "reboot"
            ? Visibility.Visible : Visibility.Collapsed;

        // Some actions make no sense to fire from the Settings desk — testing "Open Settings" is a no-op
        // here, Text Chat would type into this very window, Exit Current App would close Settings itself,
        // and the Power actions would sleep/reboot/lock the machine mid-edit. HIDDEN, not disabled.
        bool hideTest = type is "settings" or "text-chat" or "exit-app"
            || (type is "system" && CurrentCommand()
                    is "sleep" or "hibernate" or "reboot" or "shutdown" or "logout" or "lock" or "power-plan");
        BtnTest.Visibility = hideTest ? Visibility.Collapsed : Visibility.Visible;

        UpdateKeysStyling(isKeys);
        bool obsNeedsTarget = isObs && CurrentCommand() is "obs-set-scene" or "obs-toggle-mic";
        ObsTargetRow.Visibility = obsNeedsTarget                  ? Visibility.Visible : Visibility.Collapsed;
        // The connection lives in Advanced ▸ Integrations; prompt only while it's unset (G13.A).
        ObsConfigRow.Visibility = isObs && !_obsConfigured        ? Visibility.Visible : Visibility.Collapsed;
        if (obsNeedsTarget)
        {
            bool scene = CurrentCommand() is "obs-set-scene";
            ObsTargetLabel.Text = Loc.T(scene ? UiText.Editor.SceneName : UiText.Editor.AudioSourceName);
            ObsTargetHint.Text  = Loc.T(scene ? UiText.Editor.SceneHint : UiText.Editor.AudioSourceHint);
        }
        if (isLauncher && LauncherBox.SelectedIndex < 0) LauncherBox.SelectedIndex = 0;
        if (isLaunch && BehaviorBox.SelectedIndex < 0) BehaviorBox.SelectedIndex = 0;

        ChatButtonRow.Visibility = isTextChat ? Visibility.Visible : Visibility.Collapsed;
        ChatTextRow.Visibility   = isTextChat ? Visibility.Visible : Visibility.Collapsed;
        if (isTextChat)
        {
            if (ChatButtonBox.SelectedIndex < 0) ChatButtonBox.SelectedIndex = 0;   // default: Try Game Default
            bool custom = SelectedTag(ChatButtonBox) == "custom";
            ChatCustomKeyBox.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }


        if (isInstalledGame && !_gamesLoaded)
            _ = LoadGamesAsync();
        else if (isInstalledGame)
        {
            var sel = SliceList.SelectedItem as SliceEditModel;
            if (!string.IsNullOrEmpty(sel?.Url)) SelectGameByUrl(sel.Url);
        }

        UpdateLevelVisibility(type);
        UpdateValidationHint();
    }

    private void UpdateLevelVisibility(string? type = null)
    {
        type ??= CurrentType();
        bool isVolumeSet = type is "system" && CurrentCommand() is "volume-set";
        LevelRow.Visibility = isVolumeSet ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Power plan picker (system:power-plan) ───────────────────────────────────
    private bool _powerPlansLoaded;
    private string? _activePowerPlanGuid;

    /// <summary>Populate BOTH Toggle Power Plan dropdowns once, lazily, when a power-plan slice is first
    /// shown. The powercfg shell-out runs OFF the dispatcher (a hung/slow child must not freeze the
    /// editor); <paramref name="whenLoaded"/> runs on the UI thread after the items exist — immediately
    /// when they already do. The active scheme is flagged in its label.</summary>
    private void EnsurePowerPlansLoaded(Action? whenLoaded = null)
    {
        if (_powerPlansLoaded) { whenLoaded?.Invoke(); return; }
        _powerPlansLoaded = true;
        Task.Run(PowerPlans.List).ContinueWith(t =>
        {
            var plans = t.IsCompletedSuccessfully ? t.Result : Array.Empty<PowerPlans.Plan>();
            bool prev = _switchingTab; _switchingTab = true;
            try
            {
                PowerPlanBox.Items.Clear();
                PowerPlanBoxB.Items.Clear();
                foreach (var plan in plans)
                {
                    if (plan.Active) _activePowerPlanGuid = plan.Guid;
                    // Two combos need two ComboBoxItem instances — a WPF element can't have two parents.
                    PowerPlanBox.Items.Add(new ComboBoxItem
                    {
                        Content = plan.Active ? Loc.F(UiText.Editor.PowerPlanCurrent, plan.Name) : plan.Name,
                        Tag     = plan.Guid,
                    });
                    PowerPlanBoxB.Items.Add(new ComboBoxItem
                    {
                        Content = plan.Active ? Loc.F(UiText.Editor.PowerPlanCurrent, plan.Name) : plan.Name,
                        Tag     = plan.Guid,
                    });
                }
            }
            finally { _switchingTab = prev; }
            whenLoaded?.Invoke();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Select the stored plan pair. Plan A defaults to the currently-active plan (else the
    /// first), so a legacy single-plan slice's stored plan lands in A. Plan B defaults to Balanced — the
    /// "sensible other half" for a legacy single-plan config — else the first plan that isn't A.
    /// Programmatic — suppressed via <see cref="_switchingTab"/>.</summary>
    private void SelectPowerPlan(string? guid, string? guidB)
    {
        EnsurePowerPlansLoaded(() =>
        {
            bool prev = _switchingTab; _switchingTab = true;
            try
            {
                var targetA = string.IsNullOrWhiteSpace(guid) ? _activePowerPlanGuid : guid;
                SelectPlanIn(PowerPlanBox, targetA, fallbackFirst: true);

                var targetB = string.IsNullOrWhiteSpace(guidB) ? PowerPlans.BalancedGuid : guidB;
                if (!SelectPlanIn(PowerPlanBoxB, targetB, fallbackFirst: false))
                {
                    // Balanced (or the stored B) isn't in the list — pick the first plan that isn't A,
                    // else just the first, so the toggle always has a concrete second choice.
                    string? aSel = (PowerPlanBox.SelectedItem as ComboBoxItem)?.Tag as string;
                    ComboBoxItem? other = null;
                    foreach (ComboBoxItem it in PowerPlanBoxB.Items)
                        if (!string.Equals(it.Tag as string, aSel, StringComparison.OrdinalIgnoreCase)) { other = it; break; }
                    PowerPlanBoxB.SelectedItem = other ?? (PowerPlanBoxB.Items.Count > 0 ? PowerPlanBoxB.Items[0] : null);
                }
            }
            finally { _switchingTab = prev; }
        });
    }

    /// <summary>Select <paramref name="guid"/> in <paramref name="box"/>; true when it matched. With
    /// <paramref name="fallbackFirst"/>, a miss selects the first item (and still returns false).</summary>
    private static bool SelectPlanIn(ComboBox box, string? guid, bool fallbackFirst)
    {
        foreach (ComboBoxItem it in box.Items)
            if (string.Equals(it.Tag as string, guid, StringComparison.OrdinalIgnoreCase))
            { box.SelectedItem = it; return true; }
        if (fallbackFirst && box.Items.Count > 0) box.SelectedIndex = 0;
        return false;
    }

    // Shared by both plan combos (A and B) — the handler only folds the pick into the draft.
    private void PowerPlanBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _switchingTab) return;
        MarkDirtyFromEditor();
    }

    private void RebootLoginCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        MarkDirtyFromEditor();
    }

    // ── Key Combo presentation (Advanced ▸ Key Combo only) ───────────────

    /// <summary>The KEYPRESS type's Key Combo box is a fixed, narrower width with enlarged text. The
    /// Discord/Steam hotkey fields share the same box, so ClearValue restores their default stretch and
    /// inherited font size when the type isn't KEYPRESS. All combo text renders in the normal ink.</summary>
    private void UpdateKeysStyling(bool isKeypress)
    {
        if (isKeypress)
        {
            KeysBox.HorizontalAlignment = HorizontalAlignment.Left;
            KeysBox.Width    = 210;    // narrower than the full form-column width
            KeysBox.FontSize = 21.6;   // enlarged for legibility at that width
        }
        else
        {
            KeysBox.ClearValue(WidthProperty);
            KeysBox.ClearValue(HorizontalAlignmentProperty);
            KeysBox.ClearValue(FontSizeProperty);
        }
    }

    /// <summary>"Configure Discord Integration" (shown on a voice-channel slice until credentials exist):
    /// run the mini setup wizard; on save, credentials are written and the URL field takes over.</summary>
    private void BtnConfigureDiscord_Click(object sender, RoutedEventArgs e)
    {
        _modalOpen = true;   // don't let a background reconcile swap the draft while the dialog is up
        try
        {
            var dlg = new DiscordSetupWindow { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) UpdateFieldVisibility(CurrentType());
        }
        finally { _modalOpen = false; }
    }

    private void BtnBrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = Loc.T(UiText.Editor.SelectExe),
            Filter = Loc.T(UiText.Editor.ExeFilter),
        };
        _modalOpen = true;   // see HasPendingEdits — don't let a reconcile swap the draft mid-dialog
        try { if (dlg.ShowDialog() == true) PathBox.Text = dlg.FileName; }
        finally { _modalOpen = false; }
    }

    /// <summary>"Installed Apps…" — pick from the shell AppsFolder (desktop + Store apps). Stores the
    /// shell:AppsFolder\&lt;AUMID&gt; launch path in PathBox; icon + readout + label inference all
    /// understand that shape (IconCache.ExtractShellIcon / InstalledApps.NameFor).</summary>
    private void BtnPickApp_Click(object sender, RoutedEventArgs e)
    {
        _modalOpen = true;   // see HasPendingEdits — don't let a reconcile swap the draft mid-dialog
        try
        {
            var dlg = new AppPickerWindow { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.Selected is { } app)
            {
                PathBox.Text = app.LaunchPath;   // TextChanged runs the readout + MaybeAutoLabel pass
            }
        }
        finally { _modalOpen = false; }
    }

    // ── Game picker ───────────────────────────────────────────────────────────

    private async Task LoadGamesAsync()
    {
        _gamesLoaded = false;

        // Save/restore (not set/clear) the guard: this prefix runs synchronously inside LoadIntoEditor's
        // own _loading=true (via SelectTypeOption → UpdateFieldVisibility), and clearing the shared flag
        // here would let change handlers fire mid-load against the previous slice's field values.
        bool prev = _loading;
        _loading = true;
        try
        {
            GameBox.Items.Clear();
            GameBox.Items.Add(new ComboBoxItem { Content = Loc.T(UiText.Passthru.ScanningGames), IsEnabled = false });
            GameBox.SelectedIndex = 0;
            GameLaunchUrl.Text = "";
        }
        finally { _loading = prev; }

        var games = await Task.Run(GameLibrary.Scan).ConfigureAwait(true);
        _gamesLoaded = true;

        _loading = true;
        try
        {
            GameBox.Items.Clear();
            if (games.Length == 0)
            {
                GameBox.Items.Add(new ComboBoxItem { Content = Loc.T(UiText.Editor.NoInstalledGames), IsEnabled = false });
                GameBox.SelectedIndex = 0;
            }
            else
            {
                foreach (var g in games)
                    GameBox.Items.Add(new ComboBoxItem { Content = g.DisplayName, Tag = g });
            }
        }
        finally { _loading = false; }

        var model = SliceList.SelectedItem as SliceEditModel;
        if (model?.ActionType == "installed-game" && !string.IsNullOrEmpty(model.Url))
            SelectGameByUrl(model.Url);
    }

    private void SelectGameByUrl(string url)
    {
        bool prev = _loading;   // nest-safe: may run synchronously inside LoadIntoEditor's _loading=true
        _loading = true;
        try
        {
            foreach (ComboBoxItem item in GameBox.Items)
            {
                // Match either stored form: older slices carry LaunchUrl (playnite:// for
                // Playnite-listed games); newer ones carry the direct storefront URL (K2).
                if (item.Tag is InstalledGame g && (g.LaunchUrl == url || g.DirectLaunchUrl == url))
                {
                    GameBox.SelectedItem = item;
                    GameLaunchUrl.Text = g.DirectLaunchUrl ?? g.LaunchUrl;
                    return;
                }
            }
            GameLaunchUrl.Text = Loc.F(UiText.Editor.StoredUrl, url);
        }
        finally { _loading = prev; }
    }

    private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if ((GameBox.SelectedItem as ComboBoxItem)?.Tag is InstalledGame game)
        {
            GameLaunchUrl.Text = game.LaunchUrl;
            // ⚠ Retitles the slice even over a HAND-TYPED label — the one place a custom label is
            // overwritten, and deliberate. This label names a specific game, not a generic action, so a
            // slice reading "Elden Ring" that now launches Hades is a worse outcome than a custom label the
            // user retypes. (MaybeAutoLabel can't do it: a typed label clears _labelInferred, which is
            // exactly the rule being excepted here.)
            _autoLabeling = true;
            try { LabelBox.Text = game.Name; _labelInferred = true; }
            finally { _autoLabeling = false; }
        }
        MarkDirtyFromEditor();
        // Re-resolve the Custom Logo for the game just picked, so adding or CHANGING a game doesn't require
        // the refresh button. The previous game's logo is cleared first, unconditionally: a game with no
        // logo of its own must fall back to the glyph rather than keep its predecessor's art.
        // ⚠ Deliberately NOT gated on a blank IconName. Every game slice built by the wheel/Game Grid
        // carries IconName "GamepadVariant" as its fallback glyph (App.BuildGameSlice), so that gate — meant
        // to respect a hand-picked glyph — matched almost every real game slice and left the old game's
        // logo in place.
        if (_draft is { ActionType: "installed-game" } d)
        {
            d.LogoPath = null;
            UpdateIconColorDisplay(d);
            if (!string.IsNullOrWhiteSpace(d.Url)) _ = FetchLogoIntoDraftAsync(d);
        }
    }

    private void BtnRefreshGames_Click(object sender, RoutedEventArgs e)
    {
        _gamesLoaded = false;
        _ = LoadGamesAsync();
    }

    private void UpdateSliceButtons()
    {
        BtnAdd.IsEnabled    = _slices.Count < MaxSlices;
        BtnRemove.IsEnabled = _slices.Count > 0;   // allow emptying the wheel (empty is valid)
        // Over cap (a hand-edited config past the fixed 12 — the old lower Max Slices settings are gone) — the count alone flags
        // red; "/ 8" stays neutral, since the LIMIT isn't what's wrong. Plain Text can't colour part of
        // the string, so this is Inlines instead — only rebuilt when the string is, not every ApplyTo.
        SliceCountText.Inlines.Clear();
        bool overCap = _slices.Count > MaxSlices;
        var countRun = new System.Windows.Documents.Run($"{_slices.Count}");
        if (overCap) { countRun.Foreground = OverCapBrush; countRun.FontWeight = FontWeights.Bold; }
        SliceCountText.Inlines.Add(countRun);   // unset Foreground/Weight inherit the TextBlock's own (#888, normal)
        SliceCountText.Inlines.Add(new System.Windows.Documents.Run($" / {MaxSlices}"));
        // An empty wheel = that side disabled in-game — say so where the slices would be.
        EmptyWheelNote.Visibility = _slices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PopulateComboBoxes()
    {
        // TypeBox is (re)populated per category tab by PopulateTypeBoxForCurrentTab.
        // Only OFFER storefronts whose launcher app is actually installed (games alone don't imply it).
        foreach (var l in LauncherCatalog.All)
            if (LauncherCatalog.IsPresent(l.Key))
                LauncherBox.Items.Add(new ComboBoxItem { Content = l.ShortName, Tag = l.Key });
    }

    private static void SelectByRef(ComboBox box, object tag)
    {
        foreach (ComboBoxItem item in box.Items)
            if (ReferenceEquals(item.Tag, tag)) { box.SelectedItem = item; return; }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static void SelectByTag(ComboBox box, string? tag)
    {
        foreach (ComboBoxItem item in box.Items)
            if (item.Tag as string == tag) { box.SelectedItem = item; return; }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static string? SelectedTag(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string;

    /// <summary>Select the storefront in <see cref="LauncherBox"/> by its key, working around a first-open
    /// race: on the very first Settings open this control isn't in a rendered visual tree yet, so the
    /// ComboBox hasn't realized its item containers and a SelectedItem set can silently fail to stick
    /// (leaving the first entry — Steam — showing, and Save would then persist it). Set it now; if the
    /// control isn't loaded, re-apply once at Loaded priority — guarded by _loading so it never marks the
    /// slice dirty, and re-reading _draft so a fast slice switch before layout settles can't misapply.</summary>
    private void ApplyLauncherSelection(string? command)
    {
        // A saved slice may reference a launcher that's since been uninstalled (filtered from the offered
        // list) — surface it anyway so the existing slice stays viewable/editable rather than silently
        // snapping to Steam.
        if (!string.IsNullOrWhiteSpace(command)
            && LauncherCatalog.Find(command) is { } saved
            && !LauncherBox.Items.Cast<ComboBoxItem>().Any(i => i.Tag as string == saved.Key))
            LauncherBox.Items.Add(new ComboBoxItem { Content = saved.ShortName + "  " + Loc.T(UiText.Onboarding.NotInstalledSuffix), Tag = saved.Key });
        SelectByTag(LauncherBox, command);
        if (IsLoaded) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (_draft?.ActionType != "launcher") return;
            _loading = true;
            try { SelectByTag(LauncherBox, _draft.Command); } finally { _loading = false; }
        }));
    }

    // ── OBS connection prompt — the setting itself lives in Advanced ▸ Integrations ───────────────
    // This editor only shows whether it's set up and routes the button there (ObsSetupRequested), so
    // SystemEditorControl stays the single writer of ObsPort/ObsPassword.

    /// <summary>Pushed by SettingsWindow: false makes an OBS slice show the one-time setup prompt.</summary>
    public bool ObsConfigured
    {
        get => _obsConfigured;
        set
        {
            if (_obsConfigured == value) return;
            _obsConfigured = value;
            if (CurrentType() is "obs") UpdateFieldVisibility(CurrentType());
        }
    }
    private bool _obsConfigured;

    /// <summary>"Configure OBS Integration…" on an unconfigured OBS slice — the host switches to Advanced ▸
    /// Integrations and opens the setup pane there.</summary>
    public event EventHandler? ObsSetupRequested;

    private void BtnConfigureObs_Click(object sender, RoutedEventArgs e) => ObsSetupRequested?.Invoke(this, EventArgs.Empty);

    // ── switch-audio device dropdowns (F6) ──────────────────────────────────────

    /// <summary>Live device names for the ▾ pick buttons, read fresh on each click — a device list is
    /// exactly the thing that changes while Settings is open (headset plugged in, monitor woken), and this
    /// is a click-time enumeration of a handful of endpoints, so there's nothing to cache. Empty on
    /// failure, which just means the menu shows one disabled "no devices found" line.</summary>
    private static List<string> LiveAudioDeviceNames(DataFlow flow)
    {
        try
        {
            using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(flow, NAudio.CoreAudioApi.DeviceState.Active).ToList();
            try { return devices.Select(d => d.FriendlyName).ToList(); }
            finally { foreach (var d in devices) d.Dispose(); }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Settings] audio device list failed: {ex.Message}");
            return [];
        }
    }

    private void DevicePickBtn_Click(object sender, RoutedEventArgs e)    => ShowDevicePicker(DeviceBox, DataFlow.Render);
    private void MicDevicePickBtn_Click(object sender, RoutedEventArgs e) => ShowDevicePicker(MicDeviceBox, DataFlow.Capture);

    /// <summary>Drop a menu of the live devices under the ▾ button; picking one writes the name into the
    /// TextBox, which fires TextChanged → the same MarkDirtyFromEditor path a typed name uses. A menu (not
    /// an editable ComboBox) is the whole point — see the DeviceRow comment in the XAML.</summary>
    // System.Windows.Controls.TextBox spelled out: WinForms is referenced app-wide (tray icon), so a bare
    // "TextBox" is ambiguous here — the same disambiguation other Settings controls do at their usings.
    private void ShowDevicePicker(System.Windows.Controls.TextBox target, DataFlow flow)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var names = LiveAudioDeviceNames(flow);
        if (names.Count == 0)
            menu.Items.Add(new MenuItem { Header = Loc.T(UiText.Editor.NoDevicesFound), IsEnabled = false });
        foreach (var name in names)
        {
            var item = new MenuItem { Header = name };
            var picked = name;
            item.Click += (_, _) => target.Text = picked;   // TextChanged folds it into the draft
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>A brand-new switch-audio slice defaults to the CURRENT Windows devices (F6) — a
    /// concrete starting point beats empty fields whose blank semantics differ per field.</summary>
    private void SeedCurrentAudioDevices(SliceEditModel model)
    {
        if (!model.IsNew || !string.IsNullOrWhiteSpace(model.Device) || !string.IsNullOrWhiteSpace(model.MicDevice))
            return;
        _loading = true;
        try
        {
            DeviceBox.Text    = AudioDeviceSwitcher.CurrentDefaultName(DataFlow.Render)  ?? "";
            MicDeviceBox.Text = AudioDeviceSwitcher.CurrentDefaultName(DataFlow.Capture) ?? "";
            model.Device    = DeviceBox.Text;
            model.MicDevice = MicDeviceBox.Text;
        }
        finally { _loading = false; }
    }


    private void BehaviorBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => MarkDirtyFromEditor();

    // Default ⇄ Custom toggles whether ChatCustomKeyBox is shown next to the dropdown.
    private void ChatButtonBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        UpdateFieldVisibility(CurrentType());
        MarkDirtyFromEditor();
    }

    // ── Edit-time validation cues ───────────────────────────────────────────────

    /// <summary>Re-validate the current action: EMPTY required fields disable Save silently (no red
    /// borders, no nag — the Help chips explain each field); MALFORMED non-empty input shows the quiet
    /// grey hint. Purely advisory beyond the Save gate — the executor is crash-safe anyway.</summary>
    private void UpdateValidationHint()
    {
        if (SliceList.SelectedItem is null || EditorFields.Visibility != Visibility.Visible)
        {
            ValidationHint.Visibility = Visibility.Collapsed;
            _actionValid = true;
            UpdateButtonStates();
            return;
        }

        var (valid, msg) = ValidateCurrent();
        _actionValid = valid;
        ValidationHint.Text       = msg ?? "";
        ValidationHint.Visibility = msg is null ? Visibility.Collapsed : Visibility.Visible;
        SetDirtyButtons(_lastDirty);
    }

    /// <summary>Whether the current action's fields are usable, plus a hint message for MALFORMED input
    /// only (empty-required cases return (false, null): Save disables with no visible nag).</summary>
    private (bool valid, string? msg) ValidateCurrent() => CurrentType() switch
    {
        // A launch slice needs a path. A legacy slice holding only a stored process name still EXECUTES
        // via that override; this editor just can't create one.
        "launch" when string.IsNullOrWhiteSpace(PathBox.Text)
            => (false, null),
        "script" when string.IsNullOrWhiteSpace(PathBox.Text)
            => (false, null),
        "system" when CurrentCommand() == "volume-set" && !IsValidVolume(LevelBox.Text)
            => (false, string.IsNullOrWhiteSpace(LevelBox.Text) ? null : Loc.T(UiText.Editor.VolumeRange)),
        "url" when !IsValidUrl(UrlBox.Text)
            => (false, null),   // scheme examples live behind the URI Help chip
        // Only nag for a channel link once Discord is configured — before that the URL field is hidden
        // behind the "Configure Discord Integration…" prompt, so the hint would point at nothing.
        "discord-join" when DiscordOAuth.IsConfigured && !IsValidUrl(NormalizeDiscordUrl(UrlBox.Text))
            => (false, string.IsNullOrWhiteSpace(UrlBox.Text) ? null : Loc.T(UiText.Editor.DiscordLinkHint)),
        "keypress" when string.IsNullOrWhiteSpace(KeysBox.Text)
            => (false, null),
        "keypress" when !KeypressSender.CanParse(KeysBox.Text)
            => (false, Loc.T(UiText.Editor.BadKeyCombo)),
        // Steam ships its voice hotkeys unbound, so a blank field is the expected first state — the
        // field's hint explains the Steam-side binding; no key = the slice arms as "Configure in Settings".
        "steam-mute" when string.IsNullOrWhiteSpace(KeysBox.Text)
            => (false, null),
        "steam-mute" when !KeypressSender.CanParse(KeysBox.Text)
            => (false, Loc.T(UiText.Editor.BadSteamKey)),
        "text-chat" when string.IsNullOrWhiteSpace(ChatTextBox.Text)
            => (false, null),
        "text-chat" when SelectedTag(ChatButtonBox) == "custom" && string.IsNullOrWhiteSpace(ChatCustomKeyBox.Text)
            => (false, null),
        "text-chat" when SelectedTag(ChatButtonBox) == "custom" && !KeypressSender.CanParse(ChatCustomKeyBox.Text)
            => (false, Loc.T(UiText.Editor.BadChatKey)),
        _ => (true, null),
    };

    private static bool IsValidVolume(string? s) =>
        int.TryParse(s?.Trim(), out int v) && v is >= 0 and <= 100;

    private static bool IsValidUrl(string? s) =>
        !string.IsNullOrWhiteSpace(s) && Uri.TryCreate(s.Trim(), UriKind.Absolute, out _);
}
