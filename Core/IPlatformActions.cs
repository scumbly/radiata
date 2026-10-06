namespace ControllerWheel;

public enum ProcessToggleResult { Unavailable, Started, CloseRequested }

/// <summary>
/// Platform operations an <see cref="ActionExecutor"/> needs but that are OS-specific (audio,
/// process/app launch, key sending, HDR, display, Discord). The Windows shell implements this with
/// NAudio / Win32 / Process; a future macOS shell implements it with CoreAudio etc. Keeping it
/// behind this seam is what lets ActionExecutor live in the portable Core.
/// </summary>
public interface IPlatformActions
{
    /// <summary>Launch the app at <paramref name="path"/>, or focus it if already running. The running
    /// instance is found by <paramref name="processName"/> when given, else the path's exe filename.</summary>
    void LaunchOrFocus(string path, string? processName = null);

    /// <summary>Run a file (script/exe) via the shell, no focus handling.</summary>
    void RunFile(string path);

    /// <summary>Request a graceful close when running, otherwise launch the configured path.
    /// A close request is not a confirmed exit; unsaved-work prompts remain under user control.</summary>
    ProcessToggleResult ToggleProcess(string process, string? launchPath);

    /// <param name="exePath">The slice's launch path, when it has one. Lets the implementation fall back to
    /// matching by install folder when the process name doesn't match — an app started through an
    /// updater/launcher stub runs under a different exe name, so a name-only check reports "not running"
    /// for something plainly on screen.</param>
    bool IsProcessRunning(string process, string? exePath = null);

    /// <summary>Open a URL / custom URI scheme (https://, steam://, …).</summary>
    void OpenUrl(string url);

    /// <summary>Launch a storefront/launcher into its big-picture/fullscreen mode (or, where none
    /// exists, just open it). <paramref name="key"/> is a LauncherCatalog key (steam, playnite, …).
    /// Returns false when the launcher isn't installed / nothing could be started, so the hub can
    /// report it instead of silently doing nothing.</summary>
    bool LaunchStorefront(string key);

    /// <summary>Send a key combo (e.g. "Win+D"). Returns false if the combo wasn't recognised.</summary>
    bool SendKeys(string keys);

    /// <summary>Set the default audio device for output (capture=false) or input (capture=true) to the
    /// first device whose name contains <paramref name="nameFilter"/>; null filter = cycle devices.
    /// Returns the friendly name of the device switched to (for the hub readout), or null if nothing
    /// was switched (no devices / no match).</summary>
    string? SwitchAudioDevice(string? nameFilter, bool capture);

    /// <summary>Gracefully close the frontmost application (WM_CLOSE to its root window; never the
    /// desktop/shell or the caller itself). Returns the closed app's name for the hub readout, or null
    /// when nothing eligible was in front.</summary>
    string? CloseFrontmostApp();

    /// <summary>The app <see cref="CloseFrontmostApp"/> would close right now, without closing it —
    /// drives the armed preview so the user sees what's about to be exited. Null when nothing
    /// eligible is in front.</summary>
    string? PeekFrontmostApp();

    /// <summary>Toggle mute on the default output (capture=false) / input (capture=true). Returns the
    /// new mute state, or null if there's no such device.</summary>
    bool? ToggleMute(bool capture);

    /// <summary>Current mute state of the default output/input device, or null.</summary>
    bool? GetMute(bool capture);

    /// <summary>Set the default output volume to <paramref name="level"/> (0..1).</summary>
    void SetVolume(float level);

    /// <summary>Nudge the default input (microphone) volume by <paramref name="delta"/> (±0..1) and return
    /// the resulting level, or null if there's no capture device / the write failed. Unlike the speaker
    /// path there are no mic media keys, so this writes the endpoint scalar directly (the only route).</summary>
    float? AdjustMicVolume(float delta);

    /// <summary>Send a standard media key: "play-pause" | "next" | "prev" | "mute". Fire-and-forget —
    /// the OS routes it to whatever player owns media focus; there's no readable state.</summary>
    void MediaKey(string key);

    void Sleep();
    /// <summary>Restart the machine. <paramref name="signBackIn"/> true (the default) = restart with
    /// Automatic Restart Sign-On where Windows allows it (the couch-safe path — see the Windows
    /// implementation); false = a traditional restart that lands at the sign-in screen when one is due.</summary>
    void Reboot(bool signBackIn = true);
    void Lock();
    /// <summary>Full power-off.</summary>
    void Shutdown();
    /// <summary>Log the current user off.</summary>
    void Logout();
    /// <summary>Hibernate (falls back to sleep if hibernate is disabled on the system).</summary>
    void Hibernate();

    /// <summary>Toggle Show Desktop: minimizes all open windows, or restores them if already shown
    /// (same behaviour as clicking the taskbar's Show Desktop corner). Works on Windows 10 and 11.</summary>
    void ToggleShowDesktop();

    /// <summary>Switch display mode: "extend" | "clone" | "external" | "internal".</summary>
    void SetDisplayMode(string mode);

    /// <summary>display-toggle: reads the current display topology and flips it between Extend and Clone
    /// (via <see cref="SetDisplayMode"/>). Returns the new mode word ("Extend" or "Clone") for the hub's fire readout,
    /// or null when the topology is indeterminate for this action — single monitor (nothing to toggle
    /// between) or an Internal/External-only topology (neither is extend or clone, so "the opposite" isn't
    /// well-defined) — in which case the implementation still falls back to setting Extend (a safe, visible
    /// default) but reports the readout as unavailable rather than claim a specific new state.</summary>
    string? ToggleDisplayTopology();

    /// <summary>Empty the Windows Recycle Bin on all drives, silently (no confirm/progress/sound UI).</summary>
    void EmptyRecycleBin();

    /// <summary>Activate a Windows power scheme by GUID. Returns the plan's friendly name once it's
    /// confirmed active (the hub shows it as the fire readout), or null if the switch didn't happen —
    /// a blank/malformed GUID, powercfg missing or refusing, or the scheme still not active afterwards.
    /// Confirmed rather than assumed: powercfg can exit 0 having done nothing on a machine where the
    /// scheme is policy-locked.</summary>
    string? SetPowerPlan(string? planGuid);

    /// <summary>GUID of the currently-active Windows power scheme, or null when it can't be read.
    /// Drives the Toggle Power Plan action's "which of the two plans is next" decision.</summary>
    string? ActivePowerPlanGuid();

    /// <summary>Whether HDR/advanced-colour is currently enabled, or null if it can't be read.</summary>
    bool? HdrIsEnabled();

    /// <summary>Toggle HDR/advanced-colour on the first capable display. Returns the new state, or null if
    /// there's no HDR-capable display or the change failed.</summary>
    bool? HdrToggle();

    void JoinDiscordVoice(string url);

    /// <summary>Toggle Discord's own Deafen (<paramref name="deafen"/> true) or Mute (false) over the local
    /// RPC session — the same one Join/Leave Voice Channel uses, so both need the user's Discord application
    /// credentials. Fire-and-forget: the host gates the slice on the credentials before it gets here
    /// (App.NeedsDiscordIntegration), and the visible result is Discord's own indicator.
    /// <para>⚠ Never a keystroke. Discord's Ctrl+Shift+M / Ctrl+Shift+D defaults fire only while Discord is
    /// focused, so a slice sending them did nothing mid-game and passed the combo to the game as well.</para></summary>
    void ToggleDiscordVoiceSetting(bool deafen);

    /// <summary>Launch the Discord desktop client, or focus it if already running.</summary>
    void LaunchDiscord();

    /// <summary>Type arbitrary Unicode text into whatever control currently has keyboard focus (e.g. an
    /// in-game chat box). Fire-and-forget — see the Windows implementation for why there's no return value.</summary>
    bool SendText(string text);

    /// <summary>Best-effort name of the installed game currently running (foreground window's process, if
    /// its exe lives under a known installed game's folder — the same "is this process a game" test
    /// <see cref="AppVolumeMixer"/> uses for its D-pad mixer pair), or null when nothing eligible is running
    /// or foreground. Used by a "text-chat" slice in "Try Game Default" mode to look up the game's chat key
    /// (see Core/GameChatButtons.cs) — re-resolved on every fire, never cached across fires, so it always
    /// reflects whatever's actually in front right now.</summary>
    string? DetectRunningGameName();

    /// <summary>Opaque identity of the window that currently owns keyboard focus, for callers that inject
    /// keystrokes after a delay: capture it before the delay, re-check it after, and abort if it changed.
    /// Without that check an injection aimed at a game's chat box lands in whatever the user (or a popping
    /// updater/installer window) put in front during the gap. 0 = nothing focused / couldn't be read; two
    /// zero tokens must never compare as "still the same target".</summary>
    long ForegroundToken();
}
