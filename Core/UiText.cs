namespace ControllerWheel;

/// <summary>Marks a <see cref="UiText"/> const that never reaches a shipping build, so
/// <see cref="Loc.SourceStrings"/> leaves it out of the translation catalog. A dev-only string carries no
/// translation, and without this an untranslated dev string makes every language incomplete — which
/// withdraws it from <see cref="Loc.Offered"/> and silently costs every non-English user their language.
/// <para>⚠ Only for a string gated out of a <c>-Public</c> build (<see cref="ReleaseGates"/>) or behind
/// <c>#if DEBUG</c>. Anything a user of any build can see must stay in the catalog.</para></summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class DevOnlyStringAttribute : Attribute { }

/// <summary>The C#-side UI strings: every user-visible string that code composes, interpolates, or shares
/// with XAML lives here as a <c>public const string</c>, grouped by surface in nested classes, and reaches
/// the screen through <see cref="Loc.T"/> / <see cref="Loc.F"/> / <see cref="Loc.P"/>. <see cref="Loc.SourceStrings"/>
/// reflects over these constants, so a string that lives here is in the translation catalog by
/// construction — no parser, nothing to drift. A <c>Loc.T("literal")</c> anywhere else is a string the
/// checker cannot see; keep them here. (Strings with markup or shared with XAML bind as
/// <c>{x:Static ui:UiText.X.Y}</c>, which keeps the XAML and C# keys byte-identical.)
/// <para>A const is inlined at every use site by the compiler — that is fine, because the const is the
/// key and translation is a runtime lookup on it, never an override of the field.</para>
/// <para>Composite strings use <c>{0}</c> placeholders (<see cref="Loc.F"/>); a counted sentence is a
/// <c>One</c>/<c>Other</c> pair (<see cref="Loc.P"/>), never a <c>{0} thing{(n == 1 ? "" : "s")}</c> ternary,
/// which has nowhere to put a third form.</para></summary>
public static class UiText
{
    /// <summary>Settings window chrome, status line, dialogs, backup and reset.</summary>
    public static class Settings
    {
        public const string Saved            = "Saved.";
        public const string SaveFailed       = "Settings were not saved: {0}";
        public const string SaveWheelBeforeUpdate = "Save or discard your wheel edits before installing an update.";
        public const string ResumeWheelEdits = "Unsaved wheel edits restored. Saving replaces this wheel's saved layout. To discard these edits, quit Radiata and confirm.";
        public const string DiscardWheelEdits = "Quit and discard unsaved wheel edits? Choose No, then reopen the wheel editor to save them.";
        public const string KeyUnavailable = "The saved key cannot be decrypted by this Windows account. Enter a replacement to reconnect SteamGridDB.";
        public const string ArcadeReadOnly = "Existing Arcade data cannot be read by this version. The original file is preserved, but progress in this session will not be saved.";
        public const string ControllerSelectionPaused = "Controller selection paused";
        public const string ControllerSelectionPausedBody = "Automatic input selection is paused because Radiata cannot distinguish its virtual Xbox output from a replacement input. Wait briefly for reconnection. If input does not recover, quit and reopen Radiata.";
        public const string RecoveryRequired = "Operation stopped: controller visibility could not be restored. Close other HidHide tools and try again. Your recovery record has been kept.";
        public const string RestoreFailed    = "Couldn't restore that backup. Check that Radiata's data folder can be written to, then try again.";
        public const string UnsavedChanges   = "Unsaved changes…";
        public const string ResetToDefaults  = "Reset to defaults.";
        public const string TestLeftWheel    = "Test Left Wheel";
        public const string TestRightWheel   = "Test Right Wheel";
        public const string UninstallerFailed = "Couldn't start the uninstaller.";
        public const string LicensesTitle    = "Open-source licenses & attributions";
        public const string GplLicenseTitle  = "Radiata license (GNU GPL v3)";

        public const string LogSaved         = "Log saved to Desktop as {0} — attach it to the email.";
        public const string LogPackageFailed = @"Couldn't package the log — grab radiata-trace.log from %APPDATA%\Radiata by hand.";
        public const string CacheClearedOne  = "Cover art cache cleared ({0} file).";
        public const string CacheClearedOther = "Cover art cache cleared ({0} files).";
        public const string NoMissingCovers  = "No missing covers to retry.";
        public const string WillRetryOne     = "Will retry {0} missing cover on next browse.";
        public const string WillRetryOther   = "Will retry {0} missing covers on next browse.";

        public const string BackupTitle      = "Back Up Radiata Settings";
        public const string BackupFilter     = "Radiata backup|*.zip";
        public const string BackedUp         = "Settings backed up.";
        public const string BackupArtNote    = "Settings backed up. To keep the backup small, game artwork above the size limit will be re-downloaded after a restore.";
        public const string BackupSizeNote   = "Settings backed up. The file is large because it includes your game artwork.";
        public const string BackupFailed     = "Couldn't save the backup. Check that the location can be written to and has free space, then try again.";
        public const string RestoreTitle     = "Restore Radiata Settings";
        public const string RestoreFilter    = "Radiata backup|*.zip;*.json";
        public const string RestoreTooLarge  = "That file is {0} MB — too large to be a Radiata settings backup.";
        public const string RestoreCantRead  = "Couldn't read that file. It may be damaged, or it isn't a Radiata settings backup.";
        public const string RestoreNotBackup = "That file isn't a valid Radiata settings backup.";
        public const string RestorePrompt    = "Replace ALL current settings (wheels, colors, Advanced, Game Grid art choices, language) with this backup?";
        public const string RestoreArtNoteOne   = "Note: {0} image in this backup can't be restored (size limits). Missing game covers re-download on demand.";
        public const string RestoreArtNoteOther = "Note: {0} images in this backup can't be restored (size limits). Missing game covers re-download on demand.";
        public const string WillRestart      = "Radiata will restart.";
        public const string RestoreCaption   = "Restore Settings";
        public const string ResetPrompt      = "Reset Radiata to factory defaults?\n\nThis erases your wheels, colors and Advanced settings, and can't be undone. Use “Back Up Settings” first if you want to keep them.";
        public const string ResetCaption     = "Reset All Settings";
        public const string WipePrompt       = "Wipe all Radiata data and reset to factory defaults?\n\nEverything in %APPDATA%\\Radiata is permanently deleted, including credentials and automatic backups. Backups must be moved outside that folder if you want to keep them. \n\nRadiata restarts after a successful reset. A controller-visibility recovery failure stops the reset.";
        public const string WipeCaption      = "Wipe App Data and Reset";
        public const string RestartFailed    = "Radiata couldn't restart itself — please start it again manually.";
        public const string LanguageRestartPrompt = "Restart Radiata now to apply the language change?";
        public const string LanguageCaption  = "Language";
        public const string LanguageTip      = "Takes effect the next time Radiata starts";
        public const string Yes = "Yes";
        public const string No  = "No";
        public const string LeftWheelSlices  = "Left wheel slices";
        public const string RightWheelSlices = "Right wheel slices";
        public const string LogSubject       = "Radiata log ({0})";
        public const string LogBodyAttach    = "Please attach the file just saved to your Desktop: {0}";
        public const string LogBodyWhat      = "What happened (what you did, what you expected, what you saw):";
        public const string SupportEnglishOnly = "Support can only reply in English. If possible please write in English.";
    }

    /// <summary>The Advanced tab's code-composed readouts and dialogs.</summary>
    public static class Advanced
    {
        public const string CheckForUpdates   = "Check for Updates";
        public const string CheckForUpdatesTip = "Ask getradiata.app/update right now";
        public const string InstallUpdate     = "Install Update";
        public const string InstallUpdateTip  = "See what's new and update";
        public const string Checking          = "Checking…";
        public const string UpdateCheckFailed = "Couldn't check for updates. Check your connection and try again.";
        public const string NarratorFailed    = "Windows Narrator could not be started on this PC.";
        public const string NarratorCaption   = "Narrator";

        public const string BatteryWired   = "Battery: Wired";
        public const string BatteryEmpty   = "Battery: needs charge";
        public const string BatteryLow     = "Battery: low";
        public const string BatteryMedium  = "Battery: medium";
        public const string BatteryFull    = "Battery: full";
        public const string BatteryPercent = "Battery: {0}%";
        public const string Charging       = "Charging";

        public const string NothingHidden          = "Nothing was hidden.";
        public const string RestoredGamesOne       = "Restored {0} hidden game to the Game Grid.";
        public const string RestoredGamesOther     = "Restored {0} hidden games to the Game Grid.";
        public const string RestoredStoresOne      = "Restored {0} hidden storefront to the Game Grid.";
        public const string RestoredStoresOther    = "Restored {0} hidden storefronts to the Game Grid.";
        public const string ResetHiddenCaption     = "Reset Hidden Games";
        public const string EditObs          = "Edit OBS";
        public const string ConfigureObs     = "Configure OBS Integration…";
        public const string EditDiscord      = "Edit Discord";
        public const string ConfigureDiscord = "Configure Discord Integration…";
        public const string UpToDate         = "Radiata is up to date ({0}).";
        public const string Available        = "Radiata {0} is available.";
        public const string NarratorTip      = "Speaks the Wheel and Game Grid. To also narrate Settings, Onboarding, and other OS-owned elements, enable Windows' system-wide Narrator.";
        public const string BatteryCoarse    = "Xbox-compatible pads report four coarse levels, not a percentage.";
        public const string BatterySteps     = "Reported by the controller in 10% steps.";
    }

    /// <summary>The Customize tab's tile captions and mixer readout, the material names among them.</summary>
    public static class Customize
    {
        public const string FlatLight = "Flat\nLight";
        public const string FlatDark  = "Flat\nDark";
        // Material names, kept short — they sit on 60-px tiles. Localized, each translation fitted to the same tiles.
        public const string Pearl = "Pearl";  public const string Mesa = "Mesa";      public const string Kawaii = "Kawaii";
        public const string Obsidian = "Obsidian"; public const string Salvage = "Salvage"; public const string Reactor = "Reactor";
        public const string FlatLightSp = "Flat Light"; public const string FlatDarkSp = "Flat Dark";   // the onboarding tiles' one-line form
        public const string Thin = "Thin"; public const string Medium = "Medium"; public const string Thick = "Thick";
        public const string BestGuess = "Best Guess";
        public const string Themed    = "Themed";
        public const string Digital   = "Digital";
        public const string Physical  = "Physical";
        public const string Silent    = "Silent";
        public const string NowMixingNothing     = "Now mixing: nothing — needs a running game with audio, plus a chat / music / browser app.";
        public const string NowMixingPair        = "Now mixing: {0}  ↔  {1}";
        public const string NowMixingUnavailable = "Now mixing: (unavailable)";
        public const string SlicesOf         = "{0} slices";
        public const string BestGuessIcons   = "Best guess button icons";
        public const string PlayStationIcons = "PlayStation button icons";
        public const string XboxIcons        = "Xbox button icons";
        public const string ThemedSfx        = "Themed sound effects — match the selected material";
        public const string DigitalSfx       = "Digital sound effects";
        public const string PhysicalSfx      = "Physical sound effects";
        public const string SilentSfx        = "Silent — no sound effects";
        public const string MaterialName     = "{0} material";
        public const string IncludesSounds   = ", includes its own sounds";
        public const string UninstallTheme   = "Uninstall theme…";
        public const string UninstallThemeCaption = "Uninstall theme";
        public const string UninstallThemeConfirm = "Uninstall the \"{0}\" theme?\n\nMoves theme to the Recycle Bin. Restore it from there and restart Radiata to get it back.";
        public const string UninstallThemeActive  = "\n\nIt's the material you're using now, so the wheel switches to Pearl.";
        public const string UninstallThemeFailed  = "Couldn't uninstall the \"{0}\" theme: {1}";
        public const string RemoveTrigger    = "Remove this trigger";
        public const string RemoveTriggerName = "Remove trigger";
    }

    /// <summary>The Passthru Mode tab's app-picker prompts and row labels.</summary>
    public static class Passthru
    {
        public const string ScanningGames  = "Scanning installed games…";
        public const string ScanFailed     = "Couldn't scan installed games";
        public const string NoGamesFound   = "No installed games found";
        public const string AddInstalledGame = "Add installed game…";
        public const string RemoveException = "Remove this exception";
        public const string RemoveExceptionName = "Remove exception: {0}";
        public const string AddAppTitle    = "Add application — Radiata enters Passthru Mode while it runs";
        public const string AppsFilter     = "Applications (*.exe)|*.exe|All files (*.*)|*.*";
        public const string InstallFolder  = "install folder";
        public const string WhenEngages    = "When Passthru Mode engages";
        public const string WhileRunning   = "While Running";
        public const string WhileFrontmost = "While Frontmost";
    }

    /// <summary>The slice editor's dialogs and the labels it swaps at runtime.</summary>
    public static class Editor
    {
        public const string NoAppChosen      = "(no app chosen)";
        public const string InstalledApp     = "{0}  (installed app)";
        public const string PowerPlanCurrent = "{0}  (current)";
        public const string ThisSlice        = "this slice";
        public const string SelectExe        = "Select executable or script";
        public const string ExeFilter        = "Executable / Script|*.exe;*.ahk;*.ps1;*.bat;*.cmd|All files|*.*";
        public const string NoInstalledGames = "(no installed games found)";
        public const string NoDevicesFound   = "No devices found";
        public const string VolumeRange      = "Volume must be a whole number from 0 to 100.";
        public const string DiscordLinkHint  = "Paste a Discord channel link (right-click the channel → Copy Link).";
        public const string BadKeyCombo      = "Unrecognized key combo. Try e.g. Win+D, Ctrl+Shift+Esc, VolUp.";
        public const string BadSteamKey      = "Unrecognized key combo. Enter the key you bound in Steam, e.g. F13 or Ctrl+Shift+K.";
        public const string BadChatKey       = "Unrecognized key. Try a single key like T, Y, Enter, or /.";
        public const string LogoMissing      = "That file no longer exists.";
        public const string LogoEmpty        = "That file is empty.";
        public const string LogoTooBig       = "That image is {0} MB — the limit is {1} MB.";
        public const string LogoNotImage     = "That file isn't an image Radiata can read.";
        public const string LogoNoSize       = "That image has no size.";
        public const string LogoTooManyPixels = "That image is {0} × {1} — too many pixels to open safely ({2} megapixels; the limit is {3}).";
        public const string LogoNoTransparency = "That image has no transparent background, so it would draw as a solid rectangle.\n\nSlice logos need **transparency** around the artwork. Try a PNG with a transparent background.";
        public const string WheelFull         = "Added {0} of {1} — this wheel is full ({2} slices max).";
        public const string WheelFullCaption  = "Wheel full";
        public const string SaveChangesPrompt = "Save changes to “{0}”?";
        public const string UnsavedCaption    = "Unsaved slice";
        public const string IncompletePrompt  = "“{0}” isn't finished, so it can't be saved. Discard your changes?";
        public const string IncompleteCaption = "Incomplete slice";
        public const string BrowseForScript   = "Browse for Script…";
        public const string BrowseForApp      = "Browse for App…";
        public const string DiscordUrl        = "Discord URL";
        public const string Uri               = "URI";
        public const string SteamVoiceHotkey  = "Steam Voice Hotkey";
        public const string KeyCombo          = "Key Combo";
        public const string SceneName         = "Scene Name";
        public const string AudioSourceName   = "Audio Source Name";
        public const string StoredUrl         = "(stored: {0})";
        public const string SteamHotkeyHint    = "Sends this key. Steam ships its voice hotkeys UNBOUND — first bind one in Steam's Friends & Chat window → gear → Voice (Push-to-Mute, or the mute toggle in Open Microphone mode), then enter the same key here.";
        /// <summary>Also the XAML default of KeysHint — one key, so the two never drift.</summary>
        public const string KeyComboHint       = "e.g. Win+D | Ctrl+Shift+Esc | Alt+F4 | VolUp | PlayPause";
        public const string SceneHint          = "Exactly as it appears in OBS's Scenes list.";
        public const string AudioSourceHint    = "Exactly as it appears in OBS's Audio Mixer (e.g. Mic/Aux).";
    }

    /// <summary>Icon picker result count; {0} is the match count, {1} the catalog size (both grouped for the run's culture).</summary>
    public static class IconPicker
    {
        public const string Count   = "{0} icons";
        public const string CountOf = "{0} of {1} icons";
        public const string CustomLogo = "Custom Logo";
        public const string Edit       = "Edit";
    }

    /// <summary>The default slice labels — what a new slice is named when the user hasn't typed one, and
    /// what the starter wheels are seeded with. ⚠ These are written to config in English, always: a label
    /// still matching one of them is translated on display, so switching language is lossless and a label
    /// the user typed passes through untouched. Never wrap one of these in <see cref="Loc.T"/> at the point it
    /// is written; the wheel's draw path resolves them against this set and only this set (a game named
    /// "Control" must not collide with a UI string).</summary>
    public static class DefaultLabels
    {
        public const string NewSlice = "New Slice";
        public const string Launch = "Launch";
        public const string GameGrid = "Game Grid";
        public const string Arcade = "Arcade";
        public const string ExitCurrentApp = "Exit Current App";
        public const string DisableWheels = "Disable Wheels";
        public const string PassthruMode = "Passthru Mode";
        public const string XboxMode = "Xbox Mode";
        public const string DualShockMode = "DualShock Mode";
        public const string Settings = "Settings";
        public const string Discord = "Discord";
        public const string VoiceChat = "Voice Chat";
        public const string Mute = "Mute";
        public const string Deafen = "Deafen";
        public const string SteamMic = "Steam Mic";
        public const string OpenUri = "Open URI";
        public const string TextChat = "Text Chat";
        public const string RunScript = "Run Script";
        public const string Sequence = "Sequence";
        public const string Sleep = "Sleep";
        public const string Hibernate = "Hibernate";
        public const string Reboot = "Reboot";
        public const string ShutDown = "Shut Down";
        public const string LogOut = "Log Out";
        public const string MicMute = "Mic Mute";
        public const string PlayPause = "Play / Pause";
        public const string NextTrack = "Next Track";
        public const string PreviousTrack = "Previous Track";
        public const string MediaMute = "Media Mute";
        public const string Lock = "Lock";
        public const string ShowDesktop = "Show Desktop";
        public const string Hdr = "HDR";
        public const string DoNotDisturb = "Do Not Disturb";
        public const string Volume = "Volume";
        public const string ExtendCloneDisplay = "Extend/Clone Display";
        public const string SteamChat = "Steam Chat";
        public const string PartyChat = "Party Chat";
        public const string PartyMic = "Party Mic";
        public const string GameBar = "Game Bar";
        public const string GameBarScreenshot = "Game Bar Screenshot";
        public const string GameBarRecording = "Game Bar Recording";
        public const string RecordLast30Sec = "Record Last 30 Sec";
        public const string GameBarMic = "Game Bar Mic";
        public const string PowerPlan = "Power Plan";
        public const string RecycleBin = "Recycle Bin";
        public const string ToggleStreaming = "Toggle Streaming";
        public const string ToggleRecording = "Toggle Recording";
        public const string SaveReplayBuffer = "Save Replay Buffer";
        public const string SwitchScene = "Switch Scene";
        public const string ToggleSourceMute = "Toggle Source Mute";
        public const string Obs = "OBS";
        public const string XboxPad = "Xbox Pad";
        public const string ToggleHdr = "Toggle HDR";
        public const string SwitchAudio = "Switch Audio";
        public const string MuteMic = "Mute Mic";
    }

    /// <summary>Chord and button phrases composed by TriggerModes (Core/TriggerConfig.cs) for the Settings
    /// trigger builder, toasts, onboarding and the Help {invoke}/{disable} tokens. Bare hardware identifiers
    /// (L4/R4, Fn1/Fn2, L3/R3, the em dash) stay literal in the code: they are not words.</summary>
    public static class Chords
    {
        public const string Bumper = "Bumper";
        public const string Trigger = "Trigger";
        public const string Touchpad = "Touchpad";
        public const string None = "(none)";
        public const string EdgeSwipe = "Edge Swipe";
        public const string Home = "Home";
        public const string SelectStart = "Select/Start";
        public const string DpadLeftRight = "D-Pad L/R";
        public const string TouchpadSwipe = "Touchpad Swipe";
        public const string BumperHome = "Bumper + Home";
        public const string TriggerHome = "Trigger + Home";
        public const string BumperTrigger = "Bumper + Trigger";
        public const string SelectStartStick = "Select/Start + L3/R3";
        public const string BumperSelectStart = "Bumper + Select/Start";
        public const string TriggerSelectStart = "Trigger + Select/Start";
        public const string BumperStick = "Bumper + L3/R3";
        public const string TriggerStick = "Trigger + L3/R3";
        public const string BumperDpad = "Bumper + D-Pad L/R";
        public const string TriggerDpad = "Trigger + D-Pad L/R";
        public const string ExtraTrigger = "L4/R4 + opposite Trigger";
        public const string ExtraBumper = "L4/R4 + opposite Bumper";
        public const string ExtraHome = "L4/R4 + Home";
        public const string ExtraStick = "L4/R4 + L3/R3";
        public const string ExtraSelectStart = "L4/R4 + Select/Start";
        public const string ExtraDpad = "L4/R4 + D-Pad L/R";
        public const string BothTouchpad = "two fingers from both edges";
        public const string BothBumpersTriggers = "both Bumpers + both Triggers";
        public const string BothBumpersHome = "both Bumpers + Home";
        public const string BothTriggersHome = "both Triggers + Home";
        public const string BothSticksBumper = "both Sticks + a Bumper";
        public const string BothSticksTrigger = "both Sticks + a Trigger";
        public const string BothBumpersSelectStart = "both Bumpers + Select/Start";
        public const string BothTriggersSelectStart = "both Triggers + Select/Start";
        public const string BothSticksSelectStart = "both Sticks + Select/Start";
        public const string BothBumpersDpad = "both Bumpers + D-Pad Up/Down";
        public const string BothTriggersDpad = "both Triggers + D-Pad Up/Down";
        public const string ExtraBothTriggers = "L4 + R4 + both Triggers";
        public const string ExtraBothBumpers = "L4 + R4 + both Bumpers";
        public const string ExtraBothHome = "L4 + R4 + Home";
        public const string BothSticksExtra = "both Sticks + L4/R4";
        public const string ExtraBothSelectStart = "L4 + R4 + Select/Start";
        public const string ExtraBothDpad = "L4 + R4 + D-Pad Up/Down";
        public const string TheChord = "the chord";
        public const string PlusMore = "{0} +{1}";
        public const string YourInvocationChord = "your invocation chord";
        public const string YourChosenChords = "your chosen chords";
        public const string BothSidesOfAny = "the \"both sides\" form of any of your chosen chords";
    }

    /// <summary>ActionExecutor's status words. ⚠ These are identity tokens as well as text: <c>Opposite</c>
    /// and the narration's <c>StateClause</c> compare them, so the executor always returns the English const
    /// and the consumers (hub readout, status toast, announcer) translate at display.</summary>
    public static class Status
    {
        public const string On             = "On";
        public const string Off            = "Off";
        public const string Muted          = "Muted";
        public const string Unmuted        = "Unmuted";
        public const string NoDevice       = "No Device";
        public const string Unavailable    = "Unavailable";
        public const string NothingToExit  = "Nothing to exit";
        public const string ExitApp        = "Exit App";
        public const string NotInstalled   = "Not Installed";
        public const string ObsUnavailable = "OBS unavailable";
        public const string Failed         = "Failed";
        public const string Launching      = "Launching";
        public const string CloseRequested = "Close requested";
        public const string NoChatKey      = "No chat key default found. Configure in Settings";
    }

    /// <summary>What ActionExecutor says aloud on its own (outcomes the readout cannot carry).</summary>
    public static class Spoken
    {
        public const string ArcadeUnavailable    = "Arcade is not available in this build";
        public const string PassthruNeedsDrivers = "Passthru Mode needs the isolation drivers";
        public const string NotAvailable         = "{0} is not available in this version";
        public const string VolumePercent        = "Volume {0} percent";
        public const string ChatCooldownOne      = "Chat is on cooldown for {0} more second. Nothing was typed";
        public const string ChatCooldownOther    = "Chat is on cooldown for {0} more seconds. Nothing was typed";
        public const string TextChatNeedsGame    = "Text chat needs a game in the foreground. Nothing was typed";
        public const string NoChatKeyFor         = "No chat key default found for {0}. Configure in Settings. Nothing was typed";
        public const string NoReadableWindow     = "No readable window in front. Nothing was typed";
        public const string WindowChangedOpening = "The window changed while chat was opening. Nothing was typed";
        public const string WindowChangedMid     = "The window changed mid-message. It was not sent";
        public const string MessageSent          = "Message sent";
        public const string TextChatFailed       = "Text chat failed";
    }

    /// <summary>The announcer's own phrasing (App.xaml.cs): armed-state clauses and result lines.</summary>
    public static class Narration
    {
        public const string Launching         = "Launching {0}";
        public const string NeedsSetup        = "needs setup";
        public const string NothingToClose    = "nothing to close";
        public const string WillClose         = "will close {0}";
        public const string Currently         = "Currently {0}";
        public const string NoDeviceAvailable = "no device available";
        public const string Unavailable       = "unavailable";
        public const string NotInstalled      = "not installed";
        public const string Failed            = "failed";
        public const string HoldToConfirm     = "hold to confirm";
        public const string SliceOf           = "{0}, {1} of {2}";
        public const string PositionOf        = "Position {0} of {1}";
        public const string SubmenuSuffix     = ", submenu";
        public const string ReadyReleaseToFire = "Ready, release to fire";
        public const string Cancelled         = "Cancelled";
        public const string Released          = "Released";
        public const string Left              = "Left";
        public const string Right             = "Right";
        public const string WheelOpenOne      = "{1} wheel, {0} action";
        public const string WheelOpenOther    = "{1} wheel, {0} actions";
        public const string PracticeModeSuffix = ", practice mode";
        public const string NotIsolatedSuffix = ", input not isolated from the game";
        public const string WasSelected       = "was selected";
        public const string PracticeNotRun    = "Practice, {0} not run";
        public const string NeedsSetupOpening = "{0} needs setup, opening Settings";
        public const string Selected          = "{0} selected";
        public const string GridOpenOne       = "Game Grid, {0} game";
        public const string GridOpenOther     = "Game Grid, {0} games";
        public const string GridPickOne       = "Game Grid, choose a game to add, {0} game";
        public const string GridPickOther     = "Game Grid, choose a game to add, {0} games";
        public const string GameGridClosed    = "Game Grid closed";
        public const string EditModeEmpty     = "Edit mode, wheel is empty";
        public const string EditModeOne       = "Edit mode, {0} slice. {1} picks up, {2} adds, holding {3} deletes, {4} saves";
        public const string EditModeOther     = "Edit mode, {0} slices. {1} picks up, {2} adds, holding {3} deletes, {4} saves";
        public const string DroppedAt         = "Dropped at position {0}";
        public const string PickedUp          = "Picked up {0}. Aim to a new position, {1}";
        public const string CarryHint         = "{0} drops, {1} cancels";
        public const string MoveCancelled     = "Move cancelled";
        public const string WheelFull         = "Wheel is full, {0} slices maximum";
        public const string AddPickerOne      = "Add picker, {0} choice";
        public const string AddPickerOther    = "Add picker, {0} choices";
        public const string AddPickerSubOne   = "Add picker, submenu, {0} choice";
        public const string AddPickerSubOther = "Add picker, submenu, {0} choices";
        public const string HoldToDelete      = "Hold to delete {0}";
        public const string HoldCancelled     = "Hold cancelled";
        public const string NothingToUndo     = "Nothing to undo";
        public const string NothingToRedo     = "Nothing to redo";
        public const string UndoneOne         = "Undone, {0} slice";
        public const string UndoneOther       = "Undone, {0} slices";
        public const string RedoneOne         = "Redone, {0} slice";
        public const string RedoneOther       = "Redone, {0} slices";
        public const string Added             = "Added {0}. Aim to a position, {1}";
        public const string GamePickCancelled = "Game pick cancelled, edit mode";
        public const string AddCancelled      = "Add cancelled, edit mode";
        public const string EditSavedOne      = "Edit saved, {0} slice";
        public const string EditSavedOther    = "Edit saved, {0} slices";
        public const string DeletedLeftOne    = "{1} deleted, {0} slice left";
        public const string DeletedLeftOther  = "{1} deleted, {0} slices left";
        public const string WheelEmptied      = "{0} wheel is now empty and will not open";
        public const string BothEmptySuffix   = ". Both wheels are empty, opening Settings";
        public const string Material          = "Material {0}";
        public const string MovedTo           = "{0} moved to position {1} of {2}";
        public const string PreviousTrack     = "Previous track";
        public const string NextTrack         = "Next track";
        public const string ScrubVolume       = "Volume";
        public const string ScrubMicrophone   = "Microphone";
        public const string ScrubPercent      = "{0} {1} percent";
        public const string NothingMixing     = "Nothing is playing audio to mix";
        public const string MixCentred        = "Mix centred";
        public const string MixToward         = "Mix {0} percent toward {1}";
        public const string MixUnavailable    = "Mix unavailable";
        public const string WheelsEnabled     = "Wheels enabled";
        public const string WheelsDisabled    = "Wheels disabled";
    }

    /// <summary>Text drawn on the overlay itself (RadialMenuControl / OverlayWindow): hub notices and the
    /// edit-mode legend. The legend is resolved once per run into RadialMenuControl.Leg — never call Loc.T
    /// from a draw method.</summary>
    public static class Overlay
    {
        public const string PassthruMode        = "Passthru Mode";
        public const string ClickToEdit         = "Click {0} to edit";
        public const string ConfigureInSettings = "Configure in Settings";
        public const string ActionsDisabled     = "Actions are Disabled";
        public const string Practice            = "Practice";
        public const string Placing             = "Placing";
        public const string Moving              = "Moving";
        public const string Aim                 = "Aim →";
        public const string Place               = "Place";
        public const string Cancel              = "Cancel";
        public const string Add                 = "Add";
        public const string Move                = "Move";
        public const string Remove              = "Remove";
        public const string Back                = "Back";
        public const string Choose              = "Choose";
        public const string Done                = "Done";
        public const string Edit                = "Edit";
        public const string Undo                = "Undo";
        public const string Redo                = "Redo";
        public const string WhileSettingsOpen   = "While Settings is Open";
        public const string MixNoGame           = "No game";
        public const string MixPlayingAudio     = "playing audio";
        public const string GameSeesInput       = "Game Sees This Input";
    }

    /// <summary>The self-drawn corner and status toasts (App.xaml.cs).</summary>
    public static class Toasts
    {
        public const string UpdateClickBody = "Click for details — updating takes under a minute.";
        public const string LeftWheel   = "Left Wheel";
        public const string RightWheel  = "Right Wheel";
        public const string Removed     = "Removed";
        public const string SignalLost      = "Controller Signal Lost";
        public const string SignalLostBody  = "Toggle Bluetooth off and on (Win + A), then press the PS button.";
        public const string NewController     = "New Controller: {0}";
        public const string NewControllerBody = "Wheels now open with {0}. Click to run setup.";
        public const string ControllerChanged     = "Controller Changed";
        public const string ControllerChangedBody = "{0} — wheels now open with {1}.";
        public const string DriverConflict     = "Driver Conflict";
        public const string DriverConflictBody = "Radiata conflicts with {0}. Close it while using Radiata.";
        public const string IsolationBlocked  = "Input Isolation Blocked";
        public const string ControllerNeedsRestart     = "Controller needs a restart";
        public const string ControllerNeedsRestartBody = "Windows sees the controller but not its input. Turn the controller off (hold the Xbox button until its light goes out), turn it back on, then plug it in.";
        public const string RecoverTitle = "Recover Controller";
        public const string RecoverBody  = "Press a button on the controller. If it stays silent, turn the controller off (hold its home button until the light goes out), turn it back on, and reconnect it.";
        public const string ForeignBusTitle   = "Controller driver notice";
        public const string ForeignBusBody    = "{0} supplies this PC's ViGEmBus driver, not Radiata's setup. Radiata runs on it as is. To switch to the Nefarius driver: Device Manager ▸ System devices ▸ disable \"Virtual Gamepad Emulation Bus\", restart, then Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers.";
        public const string IsoCloseHolders   = "Close {0} to restore.";
        public const string IsoRestartHolder  = "{0} is holding HidHide. Restart the PC to restore.";
        public const string IsoHidHideBusy    = "Another app is using HidHide — close it to restore.";
        public const string IsoMultiplePads   = "More than one Xbox-class pad is connected (a pad linked by cable and by its wireless dongle at once counts twice) — unplug one to restore.";
        public const string IsoPadCountUnstable = "A controller keeps appearing and disappearing — isolation is paused until it settles.";
        public const string IsoXboxFallback   = "Running without isolation — input also reaches the game while a wheel is open.";
        public const string IsoViGEmMissing   = "The ViGEmBus driver is missing — reinstall from Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers.";
        public const string IsoNoVirtualPad   = "The virtual controller could not be created.";
        public const string IsoHidHideMissing = "The HidHide driver is missing — reinstall from Settings ▸ Advanced ▸ Install/Repair Drivers.";
        public const string IsoCloakFailed    = "The controller cloak could not be applied.";
        public const string IsoInactive       = "Input isolation is not active.";
        public const string EmulationUnavailable     = "Controller Emulation Unavailable";
        public const string EmulationUnavailableBody = "Driver not found. Install ViGEmBus to use Xbox360 emulation.";
        public const string NotDetected     = "Controller Not Detected";
        public const string NotDetectedBody = "HidHide needs to re-register Radiata: click to fix.";
        public const string SteamRestarted        = "Steam Restarted";
        public const string SteamHeldBody         = "Steam held the controller from before startup.";
        public const string SteamNoLongerBlocking = "No longer blocking Radiata.";
        public const string CouldntRestartSteam   = "Couldn't Restart Steam";
        public const string RestartSteamYourself  = "Restart Steam yourself so it lets go of your controller.";
        public const string RelaunchingSteam      = "Relaunching Steam";
        public const string WaitingSteamExit      = "Waiting for Steam to exit…";
        public const string SteamDidntRestart     = "Steam Didn't Restart";
        public const string ReconnectInstead      = "Disconnect and reconnect the controller instead.";
        public const string PassthruOn        = "Passthru Mode ON";
        public const string PassthruOff       = "Passthru Mode off";
        public const string PassthruOnBody    = "Radiata in Passthru Mode: in-wheel input will be seen by games.";
        public const string PassthruOffBody   = "Input isolation restored.";
        public const string PassthruRebindGame = "{0} may need to be relaunched to re-bind controller";
        public const string PassthruEnding     = "Passthru Mode Ending";
        public const string PassthruEndingBody = "Passthru Mode will turn off when you leave the game.";
        public const string UnblockController = "Unblock Your Controller";
        public const string SentryLead       = "Steam Input is blocking your controller.";
        public const string SentryChoose     = "Please do **any** of the following to resolve.";
        public const string SentryStillWorks = "Steam Input will still work.";
        // The three option tiles. Each action tile pairs a label with the consequence of taking it — the
        // two cures cost something (a quit, a leak) and the card must say so before the press, not after.
        public const string SentryOptReconnect    = "Disconnect & reconnect your controller";
        public const string SentryOptRelaunch     = "Relaunch Steam";
        public const string SentryOptRelaunchSub  = "open games will quit";
        public const string SentryOptPassthru     = "Passthru Mode";
        public const string SentryOptPassthruSub  = "input will bleed through";
        public const string SentrySpoken     = "Steam Input is blocking your controller. To fix, do any one of the following: disconnect and reconnect your controller, press {0} to relaunch Steam, or press {1} to continue with controller bleed-through in Passthru Mode.";
    }

    /// <summary>Spoken controller button names (ControllerButtons.Spoken). "the A button" keeps its article on
    /// purpose: SAPI reads a bare leading "A" as the article.</summary>
    public static class Buttons
    {
        public const string TheA            = "the A button";
        public const string TheB            = "the B button";
        public const string TheX            = "the X button";
        public const string TheY            = "the Y button";
        public const string LeftStickClick  = "left stick click";
        public const string RightStickClick = "right stick click";
        public const string Cross           = "cross";
        public const string Circle          = "circle";
        public const string Square          = "square";
        public const string Triangle        = "triangle";
    }

    /// <summary>The tray menu and its tooltip. Built once at startup (the language is fixed per run).</summary>
    public static class Tray
    {
        public const string DisableWheels      = "Disable Wheels";
        public const string EnableWheels       = "Enable Wheels";
        public const string PassthruMode       = "Passthru Mode";
        public const string PassthruTip        = "Games see only your real controller";
        public const string PassthruAuto       = "Passthru Mode (auto: {0})";
        public const string PassthruEndingItem = "Passthru Mode (turning off when you leave the game)";
        public const string GameGrid           = "Game Grid";
        public const string Settings           = "Settings";
        public const string Help               = "Help";
        public const string About              = "About Radiata";
        public const string ShowInExplorer     = "Show in Explorer";
        public const string StartWithWindows   = "Start with Windows";
        public const string Exit               = "Exit";
        public const string Title              = "Radiata — {0}";
        public const string NoController       = "no controller";
        public const string NotIsolatedSteam   = "not isolated (Steam)";
        public const string Unverified         = "isolation unverified";
        public const string UnverifiedHolder   = "unverified ({0})";
        public const string Isolated           = "isolated";
        public const string Connected          = "connected";
    }

    /// <summary>The capture layer's isolation notes. ⚠ Identity tokens as well as tray text: the isolation
    /// toast, the Arcade guard (Arcade.ReasonFromNote) and the warning latch compare them, so the producer
    /// (App.UpdateInputCaptureCore) always stores the English const and the tray translates at display.</summary>
    public static class IsolationNote
    {
        public const string PassthruNoCloak = "PassThru Mode (manual)";
        /// <summary>⚠ Tray display only — never stored as a note. The identity token for an auto engagement
        /// stays <see cref="PassthruNoCloak"/>, so the Arcade guard keeps matching one const.</summary>
        public const string PassthruAutoApp = "PassThru Mode (auto: {0})";
        public const string PassthruEnding  = "PassThru Mode ending";
        public const string MultiplePads    = "not isolated (multiple pads)";
        /// <summary>The churn breaker's hold: the pad count flapped, capture stopped chasing it.</summary>
        public const string PadCountUnstable = "not isolated (pad count unstable)";
        public const string XboxFallback    = "not isolated (Xbox fallback)";
        public const string HidHideBusy     = "not isolated (HidHide busy)";
        public const string CloakFailed     = "not isolated (cloak failed)";
        public const string NoVirtualPad    = "not isolated (no virtual pad)";
    }

    /// <summary>Controller display names composed by App.FriendlyKind. Product names (DualSense Edge, a
    /// profile's own name) stay literal; the generic words and transport suffixes translate.</summary>
    public static class Names
    {
        public const string XboxController      = "Xbox controller";
        public const string OrCompatible        = "{0} (or compatible)";
        public const string BluetoothController = "Bluetooth Controller";
        public const string UsbController       = "USB Controller";
        public const string Controller          = "Controller";
        public const string Usb                 = "{0} (USB)";
        public const string Bluetooth           = "{0} (Bluetooth)";
    }

    /// <summary>The Game Grid's code-composed text: the footer's mode caption, the storefront-hide confirm,
    /// and what it narrates. Its fixed captions live in GameBrowserControl.xaml as {loc:T ...}.</summary>
    public static class Grid
    {
        public const string AddToWheel       = "Add to Wheel";
        public const string Select           = "Select";
        public const string All              = "All";
        public const string StorefrontSuffix = ", storefront";
        public const string FavoriteSuffix   = ", favorite";
        public const string FilterGamesOne   = "{1}, {0} game";
        public const string FilterGamesOther = "{1}, {0} games";
        public const string HideStoreTitle   = "Hide {0} and all its games in Radiata?";
        public const string HideStore        = "Hide {0}";
        public const string HideStoreSpoken  = "Hide {0} and all its games? {1} confirm, {2} cancel";
        public const string HoldToHide       = "Hold to hide {0}";
        public const string Hidden           = "{0} hidden";
        public const string Favorited        = "{0} favorited";
        public const string Unfavorited      = "{0} unfavorited";
        public const string Favorite         = "Favorite";
        public const string Unfavorite       = "Unfavorite";
        public const string OpenLauncher     = "Open {0}";
    }

    /// <summary>The Arcade: the host's pause menu, prompts and footers (ArcadeControl / ArcadeChrome), the two
    /// shipped games' how-to cards, pause options, stage names and end-card plaques, and the isolation guard
    /// card. Authored in the arcade's own capitals; a translation may use whatever case its script has. Button
    /// glyphs are never spelled here — the code composes {glyph}  {verb} through ControllerButtons.Text.
    /// Game names (Kabloom, Connate) stay literal in the code.</summary>
    public static class Arcade
    {
        public const string Resume        = "RESUME";
        public const string HowToPlay     = "HOW TO PLAY";
        /// <summary>The bezel hint once the how-to card has been seen: START opens the menu.</summary>
        public const string Menu          = "MENU";
        public const string Reset         = "RESET";
        public const string Back          = "BACK";
        public const string Paused        = "PAUSED";
        public const string Choose        = "CHOOSE";
        public const string Confirm       = "CONFIRM";
        public const string StartResume   = "START  RESUME";
        public const string StartBack     = "START  BACK";
        public const string EndsRun       = "THIS ENDS THE RUN IN PROGRESS";
        public const string Yes           = "YES";
        public const string No            = "NO";
        public const string ArcadeTitle   = "ARCADE";
        public const string NoGames       = "NO GAMES INSTALLED";
        public const string Play          = "Play";
        public const string Close         = "Close";
        public const string HoldToPlayAnyway = "hold to play anyway";
        public const string Ready         = "READY";
        public const string GameOver      = "GAME OVER";
        public const string Best          = "BEST {0}";
        public const string Begin         = "BEGIN";
        public const string Again         = "AGAIN";
        /// <summary>The end screen's second prompt, beside <see cref="Again"/>: ○ leaves the arcade outright.
        /// Capitalised to sit in that row — <see cref="Close"/> is the sentence-case one the chrome uses.</summary>
        public const string ExitGame      = "EXIT";
        public const string BoardClear    = "BOARD CLEAR";
        public const string BoundaryCrossed = "BOUNDARY CROSSED";
        public const string Collected     = "COLLECTED";
        public const string AllClear      = "ALL CLEAR";
        public const string BeesDelta     = "{0} BEES";
        public const string PatchGrows    = "PATCH GROWS";
        public const string BeesIncrease  = "BEE POPULATION INCREASES";
        public const string FieldReady    = "FIELD IS READY";
        /// <summary>The bees-per-petal interstitial. {0} is the new capacity; {square} resolves to the pad glyph.</summary>
        public const string CapacityTitle = "BEES CAN NOW\nSHARE PETALS";
        public const string CapacityBody  = "From here a petal can hide up to {0} bees. All bees are included in counts.";
        public const string CapacityFlags = "{square} flags one bee - press again for more. Hold {square} for a question mark.";
        public const string Continue      = "CONTINUE";
        public const string Stung         = "STUNG";
        public const string YouWin        = "YOU WIN";
        public const string FallBack      = "FALL BACK";
        public const string ReplayFinalField = "REPLAY FINAL FIELD";
        public const string StartNewRun   = "START A NEW RUN?";
        public const string RadialControls = "RADIAL CONTROLS";
        /// <summary>The host adds this row to every game that has a music bed (ArcadeMusic); off by default.</summary>
        public const string Music         = "MUSIC";
        public const string Off           = "OFF";
        public const string On            = "ON";
        public const string StartingSize  = "STARTING SIZE";
        public const string Easy          = "EASY";
        public const string Medium        = "MEDIUM";
        public const string Hard          = "HARD";
        public const string StageGermination = "GERMINATION";
        public const string StageSprout   = "SPROUT";
        public const string StageRosette  = "ROSETTE";
        public const string StageCanopy   = "CANOPY";
        public const string StageNightBloom = "NIGHT BLOOM";
        public const string StageFullBloom = "FULL BLOOM";
        public const string ConnateHow1   = "Move with the analog stick and tap {cross} to lob your orb. Hold and release {cross} to send it with more force.";
        public const string ConnateHow2   = "Stars of either color snap into star gaps to make orbs.";
        public const string ConnateHow3   = "Identical colors & numbers merge together. Blended orbs can merge into matching numbers of any color, including other blended orbs.";
        public const string ConnateHow4   = "Merge combos charge your bomb. Bomb a high-scoring piece to collect points from it.";
        public const string ConnateHow5   = "If the central cluster presses past the boundary, your run ends.";
        public const string KabloomHow1   = "Uncover every safe petal to pass the level, but avoid bees!";
        public const string KabloomHow2   = "An uncovered petal counts how many bees are adjacent.";
        public const string KabloomHow3   = "Analog or D-pad selects, and {cross} uncovers a petal.";
        public const string KabloomHow4   = "{square} flags a petal; press again to raise the count, hold for a question mark.";
        public const string KabloomHow5   = "The first petal is always safe. Every field is solvable.";
        public const string PetalPopHow1  = "You control all paddles with your analog stick.";
        public const string PetalPopHow2  = "Use the paddles to keep the ball in play and pop enough petals to reach the gold core.";
        public const string PetalPopHow3  = "Some petals will take multiple hits to pop. The gold core always requires a charged hit to pass the level.";
        public const string PetalPopHow4  = "Popping a turquoise petal triggers a multiball.";
        public const string PetalPopHow5  = "Hold {cross} to charge a paddle slam. A ball charged by a slam punches harder.";
        public const string Control       = "CONTROL";
        public const string Spring        = "SPRING";
        public const string Rail          = "RAIL";
        public const string Combo         = "COMBO ×{0}";
        public const string StageLevel    = "LEVEL {0}-{1}";
        public const string ExtraLife     = "+1 LIFE";
        public const string InternodeHow1     = "Steer with the analog stick, collecting tokens.";
        public const string InternodeHow2     = "Swerve and jump with {cross} to clear gaps or avoid mines.";
        public const string InternodeHow3     = "Collect a group of tokens without missing any and the final token will become a 10x gold token.";
        public const string InternodeHow4     = "Crossing a checkpoint with enough tokens banks them to your score. Insufficient tokens means re-running the current level.";
        public const string InternodeHow5     = "Earn increasing bonus tokens and multipliers by beating levels on the first pass and without missing any tokens.";
        public const string CameraRoll    = "CAMERA ROLL";
        /// <summary>Dev-only pause row (ArcadeDebug.PhraseTester): the bank in order, three of each to a free gate.</summary>
        [DevOnlyString] public const string PhraseTester  = "PHRASE TESTER";
        public const string Checkpoint    = "CHECKPOINT";
        /// <summary>The HUD row a banked gate slides in; CHECKPOINT is the short-hand twin.</summary>
        public const string LevelUp       = "LEVEL UP!";
        /// <summary>The HUD's bottom row when the stage changes: down after a fall or a mine on an empty hand, up
        /// when a gate ends a stage.</summary>
        public const string Demoted       = "DEMOTED";
        public const string Promoted      = "PROMOTED";
        /// <summary>Bank toasts: {0} is the multiplier ("2", "2.5") or the bonus count.</summary>
        public const string NailedIt          = "Nailed it:";
        public const string NailedItStreak    = "Nailed it streak:";
        /// <summary>The multiplier drawn after the two above, highlighted: "2x", "2.5x".</summary>
        public const string Multiplier        = "{0}x";
        public const string NoMissBonus       = "No-Miss Bonus";
        public const string NoMissStreakBonus = "No-Miss Streak Bonus";
        public const string Pass          = "PASS";
        public const string ShortBy       = "SHORT BY {0}";
        public const string StageN        = "STAGE {0}";
        public const string Ouch          = "OUCH";
        public const string TokensOfQuota = "{0} / {1}";
        public const string Score         = "SCORE {0}";
        public const string KabloomBlurb  = "Floret deduction";
        public const string ConnateBlurb  = "Number-orb merging";
        public const string PetalPopBlurb = "Polygon petal breaker";
        public const string InternodeBlurb    = "Half-pipe token run";
        public const string DropInBlurb   = "Drop-in game";
        public const string TheGameYoureIn = "the game you're in";
        public const string GuardPassthruTitle = "Arcade Disabled by Passthru Mode";
        public const string GuardPassthruBody  = "While {0} is running in controller Passthru Mode, Arcade is disabled to prevent duplicate input.";
        public const string GuardPassthruFix   = "Arcade will still work normally at the desktop, or when Radiata and {0} are not set to Passthru Mode.";
        public const string GuardNotIsolatedTitle = "Controller isn't isolated yet";
        public const string GuardNoPadBody     = "Radiata is not isolated, so {0} would get controller bleed-thru.";
        public const string GuardNoPadFix      = "Install the optional drivers at Settings ▸ Advanced ▸ Troubleshooting.";
        public const string GuardCloakBody     = "Radiata is not isolated, so {0} would get controller bleed-thru.";
        public const string GuardCloakFix      = "Check Input Isolation in Settings ▸ Advanced — the drivers may need installing, or Radiata may need re-adding to HidHide's allow list.";
        public const string GuardManyTitle     = "Too many controllers";
        public const string GuardManyBody      = "With more than one controller connected, Radiata can't tell which one it's standing in for, so it leaves them all visible to {0}.";
        public const string GuardManyFix       = "Unplug or turn off the spare controller and try again.";
        public const string GuardUnstableBody  = "A controller keeps appearing and disappearing, so Radiata has paused standing in for it and {0} still sees your input.";
        public const string GuardUnstableFix   = "Unplug the spare controller or wireless dongle; if it keeps happening, restart Radiata.";
        public const string GuardUnknownBody   = "Radiata can't currently keep your input out of {0}, so playing in here would move you in the game too.";
        public const string GuardUnknownFix    = "Check Input Isolation in Settings ▸ Advanced.";
    }

    /// <summary>Button captions several windows share.</summary>
    public static class Common
    {
        public const string Close  = "Close";
        public const string Cancel = "Cancel";
    }

    /// <summary>The code-built dialog windows: package consent, crash report, update, uninstall, driver setup.
    /// ⚠ The consent dialog's name / author / folder values are package-supplied and render as plain Runs —
    /// they are never keys here and never reach InlineMarkup.</summary>
    public static class Dialogs
    {
        public const string UninstallCaption    = "Radiata Uninstall";
        public const string UninstallNeedsAdmin = "Uninstall needs administrator access (to remove the sign-in task and, if chosen, the drivers). Nothing was changed — run it again and approve the prompt.";
        public const string CleanupNeedsAdmin   = "Radiata's cleanup needs administrator access (to remove the sign-in task and, if chosen, the drivers). Nothing was changed.";
        public const string CleanupFinished     = "Radiata's cleanup finished.";
        public const string Uninstalled         = "Radiata's Windows integration cleanup is complete. Review any file-removal instructions below.";
        public const string DeleteFolderYourself = "Delete this folder yourself to finish removing Radiata:";
        // One line per driver; {0} is the driver's name. The cleanup dialog is the only place the driver
        // outcome is reported (the installer shows no driver line of its own), so each line must stand alone.
        public const string DriverRemoved       = "{0} was removed. Windows finishes removing it on the next restart — nothing to do by hand.";
        public const string DriverNotInstalled  = "{0} was not installed.";
        public const string DriverForeign       = "{0} on this PC was installed by another program (HP OMEN Gaming Hub, Oculus and Virtual Desktop ship their own copies), not by Radiata's setup, so it was left in place. Nothing to do.";
        public const string DriverNotRemoved    = "⚠ {0} could NOT be removed — Windows usually reports this when it's still in use or a restart is pending from installing it. Restart Windows, then remove it from Windows Settings ▸ Apps (it's a shared driver, so other controller tools may still use it).";
        public const string DriversLeft         = "The ViGEmBus and HidHide drivers were left installed (other tools may use them — remove them via Windows Settings ▸ Apps if you want).";
        /// <summary>{0} is the other copy's exe path. Shown instead of the driver-removal outcome when a
        /// Radiata from another folder is still running: the drivers and the cloak are shared, so removing
        /// them would break that copy.</summary>
        public const string DriversLeftOtherCopy = "Another copy of Radiata is running from {0}, so the shared drivers and the controller cloak were left in place — removing them would break that copy. Close it and run this uninstall again if you want them gone.";
        public const string DriverSetupCaption  = "Radiata — Driver setup";
        public const string DriverSetupHeading  = "Driver setup:";
        public const string DriverSetupFailedLog = "Driver setup failed — see radiata-trace.log";
        public const string PackageTitle       = "Radiata — third-party package found";
        public const string PackageApproval    = "A {0} package needs your approval";
        public const string PackageName        = "Name: ";
        public const string PackageAuthor      = "Author (self-reported): ";
        public const string PackageFolder      = "Folder: ";
        public const string PackageFingerprint = "Fingerprint: ";
        public const string PackageCodeWarning = "Arcade packages contain executable code that did not come with Radiata. It was placed in Radiata's packages folder by you or by another process on this PC, and Radiata's developer has not reviewed it.\n\nRadiata runs game scripts inside a sandbox with no access to your files, network, or other programs, but for security you should still only run code you got from a source you trust.";
        public const string PackageDataWarning = "This package did not come with Radiata. It was placed in Radiata's packages folder by you or by another process on this PC, and Radiata's developer has not reviewed it.\n\nOnly load packages from a source you trust.";
        public const string PackageDontLoad    = "Don't load it";
        public const string PackageLoad        = "Load this package";
        public const string PackageAcknowledgeCode = "I chose to install this code, I trust where it came from, and I take full responsibility for running it.";
        public const string InstallCaption     = "Install package";
        public const string InstallRejected    = "Couldn't install \"{0}\": {1}.";
        public const string InstallFailed      = "Couldn't install \"{0}\": {1}";
        public const string InstallReplace     = "\"{0}\" is already installed.\n\nReplace it with the one you dropped? The installed copy goes to the Recycle Bin.";
        public const string InstallRestart     = "\"{0}\" is installed. Restart Radiata now to use the updated version?";
        public const string InstallSummary     = "Installed {0} of {1} packages.";
        public const string PackageAcknowledgeData ="I chose to install this package, I trust where it came from, and I take full responsibility for using it.";

        public const string CrashTitle   = "Radiata — crash report";
        public const string CrashIntro   = "Radiata crashed last time. You can send this report to the developer — this is exactly what would be sent:";
        public const string CrashRemember = "Remember this choice";
        public const string CrashDontSend = "Don't send";
        public const string CrashCopy    = "Copy to clipboard";
        public const string CrashCopied  = "Copied.";
        public const string CrashCopyFailed = "Couldn't copy — the clipboard is in use.";
        public const string CrashSend    = "Send";

        public const string UpdateTitle     = "Radiata — update available";
        public const string UpdateHeading   = "Radiata {0} is available";
        public const string UpdateWhatsNew  = "What's new in this version";
        public const string UpdateRestartSteam = "Restart Steam to unblock your controller (recommended)";
        public const string UpdateSkip      = "Skip This Version";
        public const string UpdateLater     = "Later";
        public const string UpdateNow       = "Update Now";
        public const string UpdateDownloading = "Downloading Radiata {0}…";
        public const string UpdateDownloadFailed = "The download failed.";
        public const string UpdateInstalling = "Verified. Installing — Radiata will restart itself…";
        public const string UpdateInstallerFailed = "The installer couldn't be started. The downloaded file is at:\n{0}";

        public const string UninstallTitle   = "Uninstall Radiata";
        public const string Uninstalling     = "Radiata is being uninstalled.";
        public const string UninstallBody    = "This removes Radiata and undoes its Windows changes: the start-with-Windows entry, the controller cloak/allow-list, the sign-in recovery task, and this folder.";
        public const string UninstallDrivers = "Also uninstall the ViGEmBus and HidHide drivers";
        public const string UninstallDriversNote = "Leave off unless you're sure. Other controller tools (DS4Windows, reWASD) use the same drivers. You can always remove them later via Windows Settings ▸ Apps.";
        public const string UninstallSettings = "Also delete my Radiata settings and cover-art cache";
        public const string UninstallContinue = "Continue";
        public const string UninstallGo       = "Uninstall";

        public const string DriverTitle     = "Radiata — Driver Setup";
        public const string DriverSettingUp = "Setting up the controller drivers (ViGEmBus + HidHide)…";
        public const string DriverApprove   = "Approve any Windows prompts that appear.";
        public const string DriverDone      = "Done — you can close this window.";
        public const string DriverFailed    = "Driver setup did not complete — see radiata-trace-drivers.log in %APPDATA%\\Radiata.";
    }

    /// <summary>The Discord, OBS and controller-setup wizards' status lines. The HID Diagnostics window is
    /// deliberately absent: its readouts (backend, stick values, byte indices) exist for the developer to read
    /// in a support thread, and support is English-only.</summary>
    public static class Wizards
    {
        public const string DiscordNeedBoth     = "Both the Client ID and Client Secret are needed.";
        public const string DiscordChecking     = "Checking the credentials with Discord…";
        public const string DiscordCheckFailed  = "Couldn't check the credentials — try again.";
        public const string DiscordWriteFailed  = "Couldn't write the credentials — try again.";
        public const string ObsTesting          = "Testing the connection to OBS…";
        public const string ObsUnreachable      = "Couldn't reach OBS — check the port and that OBS is running.";

        public const string HidWaiting          = "Waiting for controller…";
        public const string HidConnect          = "Connect your DualSense Edge via USB or Bluetooth.";
        public const string HidCalibrating      = "Calibrating";
        public const string HidHoldNaturally    = "Hold the controller naturally and tilt it around gently.";
        public const string HidNoButtons        = "Don't press any buttons. (Moving it flags sensor bytes as noise so they're excluded during detection.)";
        public const string HidCollecting       = "Collecting baseline…";
        public const string HidCollectingPct    = "Collecting baseline… {0}%";
        public const string HidStep1            = "Step 1 of 3";
        public const string HidStep2            = "Step 2 of 3";
        public const string HidStep3            = "Step 3 of 3";
        public const string HidPressLeftFn      = "Press and hold the LEFT Fn button.";
        public const string HidLeftFnWhere      = "The small button below the left stick.";
        public const string HidPressRightFn     = "Press and hold the RIGHT Fn button.";
        public const string HidRightFnWhere     = "The small button below the right stick.";
        public const string HidPressDpadUp      = "Press and hold D-pad Up.";
        public const string HidDpadUpWhere      = "The directional pad pointing upward.";
        public const string HidListening        = "Listening…";
        public const string HidComplete         = "Complete";
        public const string HidDetectedMask     = "Detected byte {0}, mask 0x{1} — release the button.";
        public const string HidDetected         = "Detected byte {0} — release the button.";
        public const string HidCandidate        = "Candidate byte {0} — hold…  ({1}/{2})";
        public const string HidReview           = "Review the results, then click Save and apply.";
        public const string HidSaved            = "Saved. Controller is ready.";
        public const string HidSkipped          = "skipped — keeping default";
        public const string HidByteMask         = "byte {0},  mask 0x{1}";
        public const string HidByte             = "byte {0}";

        public const string DiscordVerified     = "These credentials work — click Save to keep them.";
        public const string DiscordRejected     = "Discord rejected these — double-check the Client ID and Secret in the Developer Portal.";
        public const string ObsVerified         = "OBS is reachable and the password works — click Save to keep it.";
        public const string ObsRefusedPassword  = "Connected, but OBS refused the password.";
        public const string ObsNoResponse       = "No response — is OBS running with the WebSocket server enabled?";
        public const string ObsConnectFailed    = "Couldn't connect: {0}";
        public const string ActionTest          = "Test";
        public const string ActionSave          = "Save";
    }

    /// <summary>The first-run wizard (OnboardingWindow). Its step titles, prompts, driver/art/store status
    /// lines and practice-step copy; the cards' prose lives in OnboardingWindow.xaml as LocRich.Source.</summary>
    public static class Onboarding
    {
        public const string Hold = "Hold";
        public const string Tap  = "Tap";
        public const string Recommended = "Recommended";
        public const string StepWelcome     = "Welcome";
        public const string StepPractice    = "Test it out!";
        public const string StepMaterial    = "Material";
        public const string StepAssets      = "Assets";
        public const string StepCustomize   = "Customize";
        public const string StepFinish      = "Training wheels are off!";
        public const string StepCounter     = "Step {0} of {1}";
        public const string Finish          = "Finish";
        public const string Next            = "Next";
        public const string Back            = "Back";
        public const string Skip            = "Skip";
        public const string SkipTesting     = "Skip Testing";

        public const string SkipDriversTitle = "Skip drivers?";
        public const string SkipDriversBody  = "Without these Radiata still works, but games will get unfiltered controller input while a wheel is up (bleed-through: not ideal). If you don't install them now, you can do it later from Settings.";
        public const string InstallDrivers   = "Install Drivers";
        public const string SkipDrivers      = "Skip Drivers";
        public const string AreYouSure       = "Are you sure?";
        public const string SkipArtBody      = "A SteamGridDB connection is free and supplies robust game art and icons to improve your experience. This step is enthusiastically recommended, but not required.";
        public const string Configure        = "Configure";

        public const string PadConnected     = "{0} connected";
        public const string NoController     = "No controller detected";
        public const string WelcomeCaveatHeader = "{0} Compatibility Note:";
        public const string WelcomeCaveatAdaptiveTouch = "Due to driver limitations, games cannot see **Adaptive Triggers** or the **touchpad** through input isolation. Use **Passthru Mode** for games that require those features.";
        public const string WelcomeCaveatTouchOnly = "Games cannot see the **touchpad** through input isolation. Switch on **Passthru Mode** any time to hand games your real controller and get it back; no need to quit Radiata.";
        public const string DriversIntro     = "ViGEmBus and HidHide are signed, open-source Windows drivers. Installing them changes Windows' controller stack and device visibility; it does not flash controller firmware or modify game files. ViGEmBus creates a virtual controller and HidHide hides selected devices to reduce double input. Radiata also works without them, with possible input leakage into games. Removing shared drivers can affect other controller tools.";
        public const string ViGEmOk          = "Installed and responding.";
        public const string ViGEmForeign     = "Present, but it is {0}'s own copy of the driver, an old fork Nefarius does not support. Radiata runs on it as is. Installing the Nefarius driver beside it causes conflicts, so Install skips it. To switch: Device Manager ▸ System devices ▸ disable \"Virtual Gamepad Emulation Bus\", restart, then Repair Drivers.";
        public const string ViGEmMissing     = "Not installed — required for input forwarding while HidHide is active.";
        public const string HidHideMissing   = "Not installed — prevents input bleed-through.";
        public const string HidHideUpdate    = "Installed (v{0}) — an update is bundled; Install below upgrades it in place.";
        public const string HidHideInstalled = "Installed (v{0}).";
        public const string PeerToolWarn     = "{0} is running and may remap or hide controllers. Check its configuration if input doubles or becomes unavailable.";
        public const string And              = " and ";
        public const string InstallIsolationDrivers = "Install Isolation Drivers";
        public const string UpdateDrivers    = "Update Drivers";
        public const string RepairDrivers    = "Repair Drivers";
        public const string IsolationOn      = "on";
        public const string IsolationOff     = "off — shared input while a wheel is open";
        public const string NotDetected      = "not currently detected";
        public const string SummonWith       = "Summon with: {0}";
        public const string ControllerIs     = "Controller: {0}";
        public const string InputIsolation   = "Input isolation: {0}";
        public const string Installing       = "Installing — please ALLOW Windows access prompts...";
        public const string DriverSetupFailed = "Driver setup failed — see Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers to retry.";

        public const string GameOne          = "{0} game";
        public const string GameOther        = "{0} games";
        public const string Installed        = "installed";
        public const string InstalledNoGames = "installed, no games yet";
        public const string NotInstalledSuffix = "(not installed)";

        public const string ArtIntroPlaynite = "Playnite is installed and can provide default artwork. Adding a free SteamGridDB key brings rich artwork and game logo support. (1 minute sign-in)";
        public const string ArtBoth          = "Use Playnite + SteamGridDB (Recommended)";
        public const string ArtPlayniteOnly  = "Playnite only";
        public const string ArtSgdb          = "Set up SteamGridDB (Recommended)";
        public const string ArtSgdbPlaynite  = "Set up SteamGridDB and Playnite";
        public const string ArtNone          = "No game art";
        public const string SgdbIntro        = "For games, auto-fetching artwork gives you the best experience. Connecting to **SteamGridDB** (free, 1 minute sign-in) will cover most games.";
        public const string SgdbIntroPlaynite = "For a full-featured game launcher that can fill in any gaps, **Playnite** (free, 15 min install+setup) is supported too.";
        public const string KeySaved         = "A key is already saved.";
        public const string KeyNotSaved      = "Key not saved — Test & Save to enable.";
        public const string PasteKey         = "Paste your API key first.";
        public const string CheckingKey      = "Checking the key…";
        public const string KeyWorks         = "✅ Key works — saved.";
        public const string KeyRejected      = "❌ Key rejected (or SteamGridDB unreachable) — verify you copied the whole key.";
        public const string KeyCheckFailed   = "Couldn't check the key — try again.";

        public const string AddFirstGame     = "Add your first game:";
        public const string AddMoreGames     = "Add additional games:";
        public const string NoGamesFound     = "No games found (yet) — the starter layout still covers the system side.";
        public const string PracticeReminder = "Wheels are still in practice mode. Use {0} to preview your choices.";
        public const string OverCap          = "More than {0} ticked — only the first {0} apply (each wheel caps at {0}).";
        public const string ArcadeRow        = "Arcade Launcher";

        public const string LegendSimple     = "Simple";
        public const string LegendDistinct   = "Distinct";
        public const string PracticeLead     = "Wheels are in practice mode.";
        public const string AdviceCustom     = "More distinct chords are harder to open accidentally, “Simpler” ones can be easier on the hands. Take a little time to see what feels right.";
        public const string AdviceFn         = "Hold one Fn button, aim with the opposite thumb, and release Fn.";
        public const string AdviceBumper     = "Hold a bumper and tap select or start. Then aim with the opposite thumb, and release the bumper.";
        public const string PaddleCaveat     = "Due to hardware limitations, **un-remapped** L4/R4 will work over Bluetooth only.";
        public const string RecFn            = "Fn (below thumbsticks)";
        public const string RecBumper        = "Bumper + Select/Start";
        public const string TryDisabling     = "Try Disabling Wheels";
        public const string TryEnabling      = "Try Enabling Wheels";
        public const string ChordBumpersDpad = "both Bumpers + D-Pad {0}";
        public const string ChordTriggersDpad = "both Triggers + D-Pad {0}";
        public const string ChordExtraDpad   = "L4 + R4 + D-Pad {0}";
        public const string DpadDown         = "Down";
        public const string DpadUp           = "Up";
        public const string YourChord        = "your chord";
        public const string ContinueWith     = "Continue with {0}";

        public const string SoundTip         = "Sound effects on/off";
        public const string SoundOn          = "Sound On";
        public const string SoundOff         = "Sound Off";
        public const string OpenTipJar       = "Open the Tip Jar";
        public const string AccessibilityIntro = "Radiata's defaults are best for most people, but everyone deserves to play. Choose what works for you.";
        public const string AxIgnoreOpposite = "Wheel ignores opposite stick";
        public const string AxIgnoreOppositeTip = "Aim with the wheel's own stick only";
        public const string AxToggle         = "Wheels toggle on/off";
        public const string AxToggleTip      = "Wheels stay open until dismissed";
        public const string AxSwap           = "Swap left/right";
        public const string AxSwapTip        = "Swap which wheel each trigger opens";
        public const string AxMotion         = "Reduce motion";
        public const string AxMotionTip      = "Calm animation and motion effects";
        public const string AxHub            = "Always show hub";
        public const string AxHubTip         = "Always draw the centre hub";
        public const string AxNarration      = "Narration";
        public const string AxNarrationTip   = "Speak selections, holds, and results aloud";
    }
}
