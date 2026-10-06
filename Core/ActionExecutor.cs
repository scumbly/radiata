using System.Diagnostics;

namespace ControllerWheel;

/// <summary>A toggle action's state for the on-screen readout (Line1 = label, Line2 = state).
/// <see cref="Line2Next"/> is the predicted post-fire state, set only for the armed preview so the
/// hub can show "current › next"; null after firing (then Line2 alone is the resulting state).
/// <para><see cref="LingerMs"/> overrides how long the host holds the post-fire readout. Null = the
/// host's default, which is tuned for a state word ("On", "Muted") that reads at a glance. A readout
/// carrying a whole sentence has to ask for longer or it's gone before it can be read.</para></summary>
public sealed record ActionStatus(string Line1, string Line2, string? Line2Next = null, int? LingerMs = null);

/// <summary>
/// Orchestrates a <see cref="WheelSlice"/> action. OS-specific operations go through
/// <see cref="IPlatformActions"/> so this stays portable. Safe to call on the UI thread.
/// An unknown action type (e.g. a removed one surviving in an old config) traces and no-ops.
/// </summary>
public sealed class ActionExecutor
{
    private readonly IPlatformActions _platform;
    private readonly Action _openSettings;
    private readonly Action _openGameBrowser;
    private readonly Func<bool>? _toggleEmulation;   // XBox Mode: returns the new state (true = on); null if unsupported
    private readonly Func<bool>? _emulationActive;   // XBox Mode current state, for the armed preview
    private readonly Func<bool>? _toggleDualShock;   // DualShock Mode toggle (for Xbox-controller users); true = on
    private readonly Func<bool>? _dualShockActive;   // DualShock Mode current state, for the armed preview
    // Anticheat Passthru Mode, the same toggle as Settings ▸ Passthru Mode's global checkbox and the tray item.
    // Null when the host didn't wire it, which is what "the isolation drivers aren't installed" looks like
    // from in here — the slice then no-ops rather than pretending, exactly like an unwired XBox Mode.
    private readonly Func<bool>? _toggleSafeMode;    // returns the new state (true = passthru mode on)
    private readonly Func<bool>? _safeModeActive;    // current state, for the armed preview

    public ActionExecutor(IPlatformActions platform, Action openSettings, Action openGameBrowser,
                          Func<bool>? toggleEmulation = null, Func<bool>? emulationActive = null,
                          Func<bool>? toggleDualShock = null, Func<bool>? dualShockActive = null,
                          Action? toggleWheels = null, Action<ActionConfig>? obsSend = null,
                          Action<string?>? openArcade = null,
                          Func<bool>? toggleSafeMode = null, Func<bool>? safeModeActive = null)
    {
        _toggleSafeMode   = toggleSafeMode;
        _safeModeActive   = safeModeActive;
        _openArcade       = openArcade;
        _platform         = platform;
        _openSettings     = openSettings;
        _openGameBrowser  = openGameBrowser;
        _toggleEmulation  = toggleEmulation;
        _emulationActive  = emulationActive;
        _toggleDualShock  = toggleDualShock;
        _dualShockActive  = dualShockActive;
        _toggleWheels     = toggleWheels;
        _obsSend          = obsSend;
    }

    private readonly Action? _toggleWheels;
    private readonly Action<ActionConfig>? _obsSend;   // OBS websocket client (host-owned); null = unavailable

    /// <summary>Narration hook (accessibility A1): host-set, called for outcomes that have no visual
    /// surface — the refusals that only ever reached the trace log, plus the couple of results whose value
    /// lands in the hub's momentary scrubber slot rather than the readout. Deliberately narration-only
    /// rather than promoted to an <see cref="ActionStatus"/>: some fire asynchronously (long after Execute
    /// returned, so there's no status to return), and giving them hub readouts would change what sighted
    /// users see, which is outside this goal. Null = no narration wired.
    /// <para>A refusal a user can act on says why. "Nothing happened" is the failure this closes: with no
    /// hub text and no sound, a blind user can't distinguish a refused action from a dropped input.</para></summary>
    public Action<string>? Announce { get; set; }

    /// <summary>True when the just-finished <see cref="Execute"/> narrated its own outcome. The host must
    /// check this before adding a generic "&lt;label&gt; selected" line: a specific readout ("Volume 30
    /// percent") and the generic one are both true, and speaking both buries the useful half.</summary>
    public bool SpokeThisExecution { get; private set; }

    private void Spoken(string text) { SpokeThisExecution = true; Announce?.Invoke(text); }
    /// <summary>Opens an arcade game (arg = game id, or null/blank for the picker). Null when the host didn't
    /// wire it — which is what <c>Arcade.Enabled == false</c> looks like from in here.</summary>
    private readonly Action<string?>? _openArcade;

    /// <summary>The current state of a toggle slice (label + state word), for showing in the hub
    /// while the slice is armed — before it's fired. Null for non-toggle slices.</summary>
    public ActionStatus? PreviewStatus(WheelSlice slice)
    {
        var a = slice.Action;
        if (a is null) return null;
        string? state = a.Type?.ToLowerInvariant() switch
        {
            // Launch in Toggle mode (QuitIfRunning) reads as a running/stopped toggle; Run mode has no readout.
            // a.Path lets the platform fall back to matching by install folder when the process name
            // doesn't (an app launched via a stub runs under a different name) — see IsProcessRunning.
            "launch" => a.QuitIfRunning ? (_platform.IsProcessRunning(ResolveProcessName(a), a.Path) ? UiText.Status.On : UiText.Status.Off) : null,
            "toggle" => string.IsNullOrWhiteSpace(a.Process) ? null
                        : (_platform.IsProcessRunning(a.Process, a.Path) ? UiText.Status.On : UiText.Status.Off),
            "system" => a.Command?.ToLowerInvariant() switch
            {
                "volume"     => _platform.GetMute(capture: false) is bool rv ? (rv ? UiText.Status.Muted : UiText.Status.Unmuted) : UiText.Status.NoDevice,
                "mic-mute"   => _platform.GetMute(capture: true)  is bool cv ? (cv ? UiText.Status.Muted : UiText.Status.Unmuted) : UiText.Status.NoDevice,
                "hdr-toggle" => _platform.HdrIsEnabled() is bool h ? (h ? UiText.Status.On : UiText.Status.Off) : UiText.Status.Unavailable,
                // display-toggle: no armed-hub "current state" readout — deliberate. IPlatformActions only
                // exposes ToggleDisplayTopology(), which sets the topology; a read-only peek would need a
                // new platform method purely for the preview. Add IPlatformActions.PeekDisplayTopology()
                // later if the armed-hub readout is wanted.
                _ => null,
            },
            "xbox-emulation"      => _emulationActive is { } f ? (f() ? UiText.Status.On : UiText.Status.Off) : null,
            "dualshock-emulation" => _dualShockActive is { } d ? (d() ? UiText.Status.On : UiText.Status.Off) : null,
            "safe-mode"           => _safeModeActive is { } s ? (s() ? UiText.Status.On : UiText.Status.Off) : null,
            // Names the app the fire would close (the wheel never takes focus, so the frontmost app
            // is the one underneath). Opposite() has no entry for an app name, so no "› next" arrow.
            "exit-app" => _platform.PeekFrontmostApp() ?? UiText.Status.NothingToExit,
            _ => null,
        };
        // Line2Next = predicted post-fire state, so the armed hub shows "current › next".
        // exit-app's Line2 is already an app name, so the heading stays short to leave it room.
        bool isExit = string.Equals(a.Type, "exit-app", StringComparison.OrdinalIgnoreCase);
        return state is null ? null : new ActionStatus(isExit ? UiText.Status.ExitApp : slice.Label, state, Opposite(state));
    }

    /// <summary>True when a slice's action is missing a required value the user must still enter (e.g. a
    /// Join Voice Channel with no URL), so firing it would do nothing. The host shows a "configure in
    /// Settings" hint. Optional-by-design fields (switch-audio's cycle, sequence steps' delays) don't count.</summary>
    public static bool NeedsConfiguration(WheelSlice slice)
    {
        var a = slice.Action;
        if (a is null) return true;
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        return a.Type?.ToLowerInvariant() switch
        {
            "url" or "discord-join" or "installed-game" => Blank(a.Url),
            // Steam ships its voice hotkeys unbound (unlike Discord's Ctrl+Shift+M default), so a fresh
            // steam-mute slice genuinely does nothing until the user binds a key in Steam and mirrors it here.
            "steam-mute" => Blank(a.Keys),
            "obs"          => string.Equals(a.Command, "obs-set-scene", StringComparison.OrdinalIgnoreCase) && Blank(a.ObsTarget),
            // "Try Game Default" (Command != "custom") needs only the message — the chat key is resolved
            // at fire time from whatever game is running (refusing outright if it isn't a confirmed entry).
            // "Custom" additionally needs a key. Core (this file) is a separate, WPF/Windows-free project from the shell that owns
            // KeypressSender, so — same as "keypress"/"steam-mute" above — this only checks for blank, not
            // whether it parses; the editor's ValidateCurrent (WheelEditorControl, shell-side) does the
            // stronger CanParse check at edit time, so a slice can't be saved with an unparseable key.
            "text-chat"    => Blank(a.ChatText)
                               || (string.Equals(a.Command, "custom", StringComparison.OrdinalIgnoreCase) && Blank(a.Keys)),
            "launch"     => Blank(a.Path) && Blank(a.Process),   // Execute no-ops when both are blank
            "toggle"     => Blank(a.Process),                    // Execute no-ops without a process name
            "keypress"   => Blank(a.Keys),
            "launcher"   => Blank(a.Command),
            "script"     => Blank(a.Path),
            "sequence"   => a.Steps is null || a.Steps.Length == 0,
            "system"     => a.Command?.ToLowerInvariant() switch
            {
                "volume-set" => a.Level is null,
                "power-plan" => Blank(a.PowerPlan),
                _            => false,
            },
            _            => false,
        };
    }

    /// <summary>Process name to detect/act on for a launch/toggle action: the explicit Process field
    /// if set, otherwise the executable's filename (no extension).</summary>
    private static string ResolveProcessName(ActionConfig a) =>
        !string.IsNullOrWhiteSpace(a.Process) ? a.Process!
        : System.IO.Path.GetFileNameWithoutExtension(a.Path ?? "");

    /// <summary>The flipped state of a two-state toggle, for the "current › next" preview. Null for
    /// non-toggleable readouts (No Device / Unavailable) so they show just the current word.</summary>
    private static string? Opposite(string state) => state switch
    {
        UiText.Status.On      => UiText.Status.Off,
        UiText.Status.Off     => UiText.Status.On,
        UiText.Status.Muted   => UiText.Status.Unmuted,
        UiText.Status.Unmuted => UiText.Status.Muted,
        _         => null,
    };

    /// <summary>Runs the slice. Returns an <see cref="ActionStatus"/> (label + new state) for
    /// toggle-type actions so the caller can show a status readout; null for everything else.</summary>
    private static string ToggleStatus(ProcessToggleResult result) => result switch
    {
        ProcessToggleResult.Started => UiText.Status.Launching,
        ProcessToggleResult.CloseRequested => UiText.Status.CloseRequested,
        _ => UiText.Status.Unavailable,
    };

    public ActionStatus? Execute(WheelSlice slice)
    {
        var action = slice.Action;
        SpokeThisExecution = false;
        if (action is null) return null;

        try
        {
            switch (action.Type.ToLowerInvariant())
            {
                case "launch":
                    if (string.IsNullOrWhiteSpace(action.Path) && string.IsNullOrWhiteSpace(action.Process)) return null;
                    if (action.QuitIfRunning)   // Toggle requests a close; the target may keep an unsaved-work prompt open.
                        return new ActionStatus(slice.Label,
                            ToggleStatus(_platform.ToggleProcess(ResolveProcessName(action), action.Path)));
                    _platform.LaunchOrFocus(action.Path ?? "", action.Process);   // Run: focus when up, launch when down
                    return null;
                case "script":
                    if (!string.IsNullOrWhiteSpace(action.Path)) _platform.RunFile(action.Path);
                    return null;
                case "toggle":
                    return string.IsNullOrWhiteSpace(action.Process)
                        ? null
                        : new ActionStatus(slice.Label, ToggleStatus(_platform.ToggleProcess(action.Process, action.Path)));
                case "system":
                    { var s = ExecuteSystem(action); return s is null ? null : new ActionStatus(slice.Label, s); }
                case "url":
                case "installed-game":
                    if (!string.IsNullOrWhiteSpace(action.Url)) _platform.OpenUrl(action.Url);
                    return null;
                case "launcher":
                    if (string.IsNullOrWhiteSpace(action.Command)) return null;
                    // "Not Installed" readout instead of a silent no-op when the launcher app is gone
                    // (e.g. Battle.net uninstalled but its Agent DB — and old slices — survive).
                    return _platform.LaunchStorefront(action.Command)
                        ? null
                        : new ActionStatus(slice.Label, UiText.Status.NotInstalled);
                case "exit-app":
                    // Readout names what was closed (or that nothing eligible was in front).
                    return new ActionStatus(slice.Label, _platform.CloseFrontmostApp() ?? UiText.Status.NothingToExit);
                case "keypress":
                    if (!_platform.SendKeys(action.Keys ?? ""))
                        Trace.WriteLine($"[Exec] keypress: unrecognised key combo '{action.Keys}'");
                    return null;
                case "switch-audio":
                    // Readout = the device actually switched to, so the hub confirms where the sound went.
                    return ExecuteSwitchAudio(action) is { } device
                        ? new ActionStatus(slice.Label, device)
                        : new ActionStatus(slice.Label, UiText.Status.NoDevice);
                case "discord-launch":
                    _platform.LaunchDiscord();
                    return null;
                case "discord-join":
                    if (!string.IsNullOrWhiteSpace(action.Url)) _platform.JoinDiscordVoice(action.Url);
                    return null;
                case "discord-mute":
                case "discord-deafen":
                    // Discord's own switch over RPC, never a keystroke: Ctrl+Shift+M / Ctrl+Shift+D are
                    // Discord defaults that fire only while Discord is focused, so the old keystroke route
                    // did nothing mid-game and handed the combo to the game as well. The credentials gate is
                    // the host's (App.NeedsDiscordIntegration), the same one Join Voice Channel uses.
                    // ⚠ A stored Keys from an older config is tolerated and ignored — the hidden-but-
                    // supported contract in docs/ACTIONS.md — so a downgrade keeps working.
                    _platform.ToggleDiscordVoiceSetting(string.Equals(action.Type, "discord-deafen", StringComparison.OrdinalIgnoreCase));
                    return null;
                case "steam-mute":
                    // Convenience: send the app's keybind for this control (the user binds it in
                    // Steam ▸ Friends & Chat ▸ Voice). Steam has no default — it ships its voice hotkeys
                    // unbound — so NeedsConfiguration gates a blank Keys to "Configure in Settings".
                    if (!_platform.SendKeys(action.Keys ?? ""))
                        Trace.WriteLine($"[Exec] {action.Type}: unrecognised/empty key combo '{action.Keys}'");
                    return null;
                case "obs":
                    // Sent to the host's obs-websocket client (fire-and-forget; it traces failures).
                    if (_obsSend is null) return new ActionStatus(slice.Label, UiText.Status.ObsUnavailable);
                    _obsSend(action);
                    return null;
                case "sequence":
                    RunSequence(action);
                    return null;
                case "text-chat":
                    return RunTextChat(slice, action);
                case "settings":
                    _openSettings();
                    return null;
                case "game-browser":
                    _openGameBrowser();
                    return null;
                case "arcade":
                    // Command = a game id, or blank for the Arcade Launcher (the cabinet picker). An unknown
                    // id passes through too: a config from a newer build naming a game this one doesn't
                    // have gets the launcher rather than silence. The host owns that resolution.
                    //
                    // With Arcade.Enabled false the host callback is never wired, so this traces and no-ops.
                    if (_openArcade is null)
                    {
                        Trace.WriteLine("[Exec] arcade: unavailable in this build");
                        Spoken(Loc.T(UiText.Spoken.ArcadeUnavailable));
                        return null;
                    }
                    _openArcade(action.Command);
                    return null;
                case "disable-wheels":
                    // Same behaviour as the controller's enable/disable chord: the host dismisses the
                    // open wheel and toggles (its flower toast is the feedback). Re-enable via the
                    // chord / the tray menu — a wheel slice can't re-enable what it lives on.
                    _toggleWheels?.Invoke();
                    return null;
                case "xbox-emulation":
                    return _toggleEmulation is null
                        ? null
                        : new ActionStatus(slice.Label, _toggleEmulation() ? UiText.Status.On : UiText.Status.Off);
                case "dualshock-emulation":
                    return _toggleDualShock is null
                        ? null
                        : new ActionStatus(slice.Label, _toggleDualShock() ? UiText.Status.On : UiText.Status.Off);
                case "safe-mode":
                    // Anticheat Passthru Mode — the same flip as the Settings ▸ Passthru Mode checkbox and the tray
                    // item, host-owned so all three share one path (config write + re-apply capture +
                    // balloon). Unwired = the drivers aren't there, so there's nothing to turn off. The "safe-mode"
                    // type string is frozen — renaming it orphans slices in existing configs.
                    if (_toggleSafeMode is null)
                    {
                        Spoken(Loc.T(UiText.Spoken.PassthruNeedsDrivers));
                        return null;
                    }
                    return new ActionStatus(slice.Label, _toggleSafeMode() ? UiText.Status.On : UiText.Status.Off);
                default:
                    Trace.WriteLine($"[Exec] Unknown action type: '{action.Type}'");
                    // A removed type surviving in an old config: the slice draws and arms normally, so
                    // silence here is indistinguishable from a dropped button press.
                    Spoken(Loc.F(UiText.Spoken.NotAvailable, Loc.DefaultLabel(slice.Label)));
                    return null;
            }
        }
        catch (Exception ex)
        {
            // Surface the failure in the hub instead of silently doing nothing.
            Trace.WriteLine($"[Exec] {action.Type} failed: {ex.Message}");
            return new ActionStatus(slice.Label, UiText.Status.Failed);
        }
    }

    /// <summary>Returns a state word for toggle-like commands (for a readout), or null otherwise.</summary>
    private string? ExecuteSystem(ActionConfig action)
    {
        switch (action.Command?.ToLowerInvariant())
        {
            case "sleep":     _platform.Sleep();     return null;
            // "Log In after Reboot" (default true) = the ARSO restart (/g); unchecked = a traditional
            // restart (/r) that lands wherever a Start-menu reboot without ARSO would.
            case "reboot":    _platform.Reboot(action.LogInAfterReboot ?? true); return null;
            case "lock":      _platform.Lock();      return null;
            case "shutdown":  _platform.Shutdown();  return null;
            case "logout":    _platform.Logout();    return null;
            case "hibernate": _platform.Hibernate(); return null;
            case "show-desktop": _platform.ToggleShowDesktop(); return null;
            case "volume":   return _platform.ToggleMute(capture: false) is bool rv ? (rv ? UiText.Status.Muted : UiText.Status.Unmuted) : UiText.Status.NoDevice;
            case "mic-mute":
                return _platform.ToggleMute(capture: true) is bool cv ? (cv ? UiText.Status.Muted : UiText.Status.Unmuted) : UiText.Status.NoDevice;
            case "hdr-toggle":
                // Toggle via the CCD display API directly — the Win+Alt+B hotkey isn't honoured on every
                // machine (SendInput "succeeds" but the OS ignores it), so drive the advanced-colour state
                // ourselves. Returns the new state, or null if there's no HDR-capable display / it failed.
                var hdrNew = _platform.HdrToggle();
                Trace.WriteLine($"[Exec] hdr-toggle: newState={(hdrNew?.ToString() ?? "null (no HDR display / set failed)")}");
                return hdrNew is bool hdrOn ? (hdrOn ? UiText.Status.On : UiText.Status.Off) : UiText.Status.Unavailable;
            // display-toggle reads the current topology and flips Extend⇄Clone (see
            // WindowsPlatformActions.ToggleDisplayTopology). A legacy slice still carrying
            // display-extend/clone/external/internal falls to the "Unknown system command" default below
            // and silently no-ops; it stays selectable/editable in Settings without its Command being
            // rewritten (see WheelEditorControl.Categories' Hidden options).
            case "display-toggle":
                var topo = _platform.ToggleDisplayTopology();
                Trace.WriteLine($"[Exec] display-toggle: newMode={(topo ?? "null (indeterminate — defaulted to Extend)")}");
                return topo;
            // No status word: the level lands in the hub's momentary scrubber slot, not the readout — so
            // narration is its only voice (rounded to whole percent, matching what the ring shows).
            case "volume-set":
                float setTo = Math.Clamp(action.Level ?? 0.5f, 0f, 1f);
                _platform.SetVolume(setTo);
                Spoken(Loc.F(UiText.Spoken.VolumePercent, (int)Math.Round(setTo * 100)));
                return null;
            // Steam: steam://open/friends is the one reliable chat URI in the post-2018 client (no
            // URI/hotkey/API exists for joining or leaving a voice channel — a Steam platform limit,
            // documented in the steam-xbox-voice Help topic).
            case "steam-chat-open":     _platform.OpenUrl("steam://open/friends"); return null;
            // Xbox Game Bar — the OS-standard hotkeys (Game Bar must be enabled in Windows Settings). Win+G
            // over the ms-gamingoverlay: URI because a missing Game Bar then just no-ops instead of popping
            // the "You'll need a new app" dialog.
            case "gamebar-open":        _platform.SendKeys("Win+G");         return null;
            case "gamebar-screenshot":  _platform.SendKeys("Win+Alt+PrtSc"); return null;
            case "gamebar-record":      _platform.SendKeys("Win+Alt+R");     return null;   // start / stop
            case "gamebar-record-last": _platform.SendKeys("Win+Alt+G");     return null;   // record last 30 sec
            case "gamebar-mic":         _platform.SendKeys("Win+Alt+M");     return null;   // toggle mic in recording
            case "empty-recycle-bin":   _platform.EmptyRecycleBin();         return null;
            // Toggle Power Plan: with both plans configured, firing flips between them — active == plan A
            // → activate plan B, anything else → plan A. A single-plan config (no PowerPlanB) is a plain
            // "set plan A". Reports what actually happened: the activated plan's name on success, "Failed"
            // when the switch didn't take — powercfg can no-op silently (policy-locked scheme, missing
            // exe), and a power-plan slice otherwise gives no feedback at all.
            case "power-plan":
            {
                string? target = action.PowerPlan;
                if (!string.IsNullOrWhiteSpace(action.PowerPlanB))
                {
                    var active = _platform.ActivePowerPlanGuid();
                    if (active is not null
                        && string.Equals(active.Trim(), action.PowerPlan?.Trim(), StringComparison.OrdinalIgnoreCase))
                        target = action.PowerPlanB;
                }
                return _platform.SetPowerPlan(target) ?? UiText.Status.Failed;
            }
            // GPU-vendor capture/overlay commands (nvidia-*/amd-*) are unsupported, with no migration — a
            // slice naming one falls to the "Unknown system command" default below and silently no-ops.
            // Standard media keys — fire-and-forget keystrokes to whatever player has media focus
            // (no readable state, so no hub readout).
            case "media-play-pause": _platform.MediaKey("play-pause"); return null;
            case "media-next":       _platform.MediaKey("next");       return null;
            case "media-prev":       _platform.MediaKey("prev");       return null;
            case "media-mute":       _platform.MediaKey("mute");       return null;
            default:
                Trace.WriteLine($"[Exec] Unknown system command: '{action.Command}'");
                return null;
        }
    }

    /// <summary>Runs the switch and returns the friendly name of the device switched to, for the hub
    /// readout — the output device when one was switched, else the mic (mic-only slices). Null when
    /// nothing switched (no devices / no name match).</summary>
    private string? ExecuteSwitchAudio(ActionConfig action)
    {
        bool hasMic = !string.IsNullOrWhiteSpace(action.MicDevice);
        bool hasOut = !string.IsNullOrWhiteSpace(action.Device);
        string? outName = null, micName = null;

        // Set/cycle the output, unless this is a mic-only slice (blank output + a mic target).
        if (hasOut || !hasMic)
        {
            outName = _platform.SwitchAudioDevice(action.Device, capture: false);
            Trace.WriteLine($"[Exec] switch-audio out: target='{action.Device ?? "(cycle)"}' → '{outName ?? "(none)"}'");
        }
        if (hasMic)
        {
            micName = _platform.SwitchAudioDevice(action.MicDevice, capture: true);
            Trace.WriteLine($"[Exec] switch-audio mic: target='{action.MicDevice}' → '{micName ?? "(none)"}'");
        }
        return outName ?? micName;
    }

    // ── Sequence ──────────────────────────────────────────────────────────────

    /// <summary>Run each step in order, waiting its optional DelayMs first. Fire-and-forget; resumes
    /// on the calling (UI) thread so steps that touch UI stay safe. Errors per step are logged.</summary>
    private const int MaxSequenceSteps   = 64;
    private const int MaxSequenceDelayMs = 60_000;

    private async void RunSequence(ActionConfig action)
    {
        // Whole body guarded: this is `async void`, so an exception anywhere outside the per-step try
        // (e.g. a cancelled Task.Delay, or a fault on the resumed context) would otherwise surface as an
        // unobserved exception on the UI dispatcher and take the process down.
        try
        {
            if (action.Steps is null) return;
            // Bounded at the consumer: Steps and DelayMs are the one part of a slice ConfigLoader.Sanitize never
            // looks inside, and a restored backup is a stranger's file. A step without a delay never yields.
            int run = 0;
            foreach (var step in action.Steps)
            {
                if (step is null) continue;
                if (++run > MaxSequenceSteps) { Trace.WriteLine($"[Exec] sequence cut at {MaxSequenceSteps} steps"); break; }
                if (step.DelayMs is int d && d > 0) await System.Threading.Tasks.Task.Delay(Math.Min(d, MaxSequenceDelayMs));
                if (string.Equals(step.Type, "sequence", StringComparison.OrdinalIgnoreCase)) continue; // no nesting
                try { Execute(new WheelSlice { Action = step }); }
                catch (Exception ex) { Trace.WriteLine($"[Exec] sequence step '{step.Type}' failed: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Exec] sequence aborted: {ex.Message}"); }
    }

    // ── Text Chat ─────────────────────────────────────────────────────────────

    /// <summary>How long to wait after opening chat before typing, so the game's chat box has finished
    /// animating/focusing before keystrokes land in it. Tunable — 150ms comfortably covers every game
    /// checked while authoring this (most open instantly), without adding a noticeable pause to the fire.</summary>
    private const int ChatBoxOpenDelayMs = 150;

    /// <summary>Resolve which key opens the running game's chat box, then hand off to
    /// <see cref="TypeChatAsync"/> to actually send. Synchronous so it can return an
    /// <see cref="ActionStatus"/> — the "no chat key for this game" refusal is the one outcome here with a
    /// hub readout, because it's the one the user can fix (pick a Custom key in Settings).
    ///
    /// <para>⚠ This is the one action that types arbitrary text plus Enter into the general input stream, so
    /// it is deliberately the most conservative about where that lands. Guards, all load-bearing:</para>
    /// <list type="bullet">
    ///   <item>"Try Game Default" only injects when an installed game owns the foreground — a null
    ///   <see cref="IPlatformActions.DetectRunningGameName"/> means the message could land in a browser, a
    ///   DM, a terminal, or a sign-in field.</item>
    ///   <item>It refuses again unless that game is a confirmed entry in Core/GameChatButtons.cs's Map —
    ///   no fallback key is guessed for an unmapped or known-chatless game. A wrong guess presses whatever
    ///   that game's real Enter binding is before the message follows it in as raw keystrokes, which is
    ///   worse than typing nothing.</item>
    ///   <item>The foreground identity is captured before the chat key goes out and re-checked before the
    ///   text and again before Enter. Anything can steal focus inside <see cref="ChatBoxOpenDelayMs"/> —
    ///   an updater window, a Steam popup, the user alt-tabbing — and the message must not follow it.</item>
    /// </list>
    /// <para>The message body is never traced: the trace log is the file users are asked to send, and chat
    /// text is user content, not diagnostics. Length only.</para></summary>
    private ActionStatus? RunTextChat(WheelSlice slice, ActionConfig action)
    {
        // Anti-spam gate, checked first so a throttled fire does no detection work and touches no input.
        // A wheel slice is a single button press, so without this the action is a held-button chat flood —
        // which is a bannable nuisance in most multiplayer titles and not something Radiata should make easy.
        if (_lastChatCommitAt != 0 && Environment.TickCount64 - _lastChatCommitAt < ChatThrottleMs)
        {
            int waitS = (int)Math.Ceiling((ChatThrottleMs - (Environment.TickCount64 - _lastChatCommitAt)) / 1000.0);
            Trace.WriteLine($"[Exec] text-chat: throttled — {waitS}s of the {ChatThrottleMs / 1000}s cooldown left");
            Spoken(Loc.P(UiText.Spoken.ChatCooldownOne, UiText.Spoken.ChatCooldownOther, waitS));
            return new ActionStatus(slice.Label, $"Cooldown, {waitS}s", LingerMs: NoChatKeyLingerMs);
        }

        string chatKey;
        if (string.Equals(action.Command, "custom", StringComparison.OrdinalIgnoreCase))
        {
            chatKey = action.Keys ?? "";
        }
        else
        {
            // "Try Game Default": re-detect what's running on every fire (never cached across fires —
            // the whole point is that switching games mid-session switches the key with no re-config).
            var game = _platform.DetectRunningGameName();
            if (game is null)
            {
                // No installed game owns the foreground → refuse. See the guard note above; typing a
                // message + Enter into an unknown window is worse than doing nothing. Narration-only:
                // with nothing game-like in front there's no game to name, so a hub readout would only
                // be able to restate the refusal the missing message already implies.
                Trace.WriteLine("[Exec] text-chat: no installed game in the foreground — not injecting");
                Spoken(Loc.T(UiText.Spoken.TextChatNeedsGame));
                return null;
            }
            var mapped = GameChatButtons.Lookup(game);
            if (mapped is not null)
            {
                chatKey = mapped;
                Trace.WriteLine($"[Exec] text-chat: game='{game}' key='{chatKey}'");
            }
            else
            {
                // No key to send: either known-chatless (GameChatButtons.NoTextChat) or simply not
                // verified yet (the source doc's "Needs re-verification" list). Both send nothing, and
                // both are a configuration problem the user can fix by picking a key themselves — so
                // unlike the refusals above this one gets a hub readout naming the game, held long
                // enough to read. It deliberately does not open Settings: the wheel was fired at a game,
                // and yanking the user out to a window mid-session is a bigger interruption than the
                // message they lost. HasNoTextChat is checked only to say why in the trace/narration —
                // the visible outcome is identical either way.
                bool known = GameChatButtons.HasNoTextChat(game);
                Trace.WriteLine($"[Exec] text-chat: '{game}' has no chat key " +
                                $"({(known ? "known to have no text chat" : "not in table — unconfirmed")}) — not injecting");
                Spoken(Loc.F(UiText.Spoken.NoChatKeyFor, game));
                return new ActionStatus(game, UiText.Status.NoChatKey,
                                        LingerMs: NoChatKeyLingerMs);
            }
        }

        // Stamp on commit, not on a successful post. Two reasons, and the second is the important one:
        // an aborted send may still have typed text somewhere, so it deserves the cooldown; and
        // TypeChatAsync is async-void, so stamping here is what stops rapid fires from interleaving two
        // sends inside the chat-box delay. Success-only stamping would leave that hole open.
        _lastChatCommitAt = Environment.TickCount64;
        TypeChatAsync(chatKey, action);
        return null;
    }

    /// <summary>Minimum gap between Text Chat sends. Deliberately a constant, not a setting: it is an abuse
    /// guardrail rather than a preference, and a configurable one would just be a spam switch. Long enough
    /// that a held/repeated slice can't flood a game's chat, short enough for real back-and-forth.</summary>
    private const long ChatThrottleMs = 15_000;

    /// <summary>TickCount64 when a send was last committed (0 = never this session). Not persisted: the
    /// throttle guards a live conversation, and a restart is not a plausible spam vector.</summary>
    private long _lastChatCommitAt;

    /// <summary>How long the hub holds the "no chat key default found" readout. Much longer than a state
    /// word's default linger — this one is a two-clause sentence plus a game name, and it's the only
    /// feedback that the message the user just fired went nowhere.</summary>
    private const int NoChatKeyLingerMs = 2600;

    /// <summary>The injecting half of <see cref="RunTextChat"/>: open the chat box, wait for it, type the
    /// message, press Enter. Async-void on purpose (same pattern as <see cref="RunSequence"/>) so the
    /// wheel's release handler never blocks on the delay. Split from the key resolution above because
    /// that half runs synchronously and can therefore return an <see cref="ActionStatus"/> for the hub;
    /// once we're past it and committed to sending, the remaining refusals are narration-only.</summary>
    private async void TypeChatAsync(string chatKey, ActionConfig action)
    {
        try
        {
            // Capture the target before opening chat, so the check below spans the whole delay.
            long target = _platform.ForegroundToken();
            if (target == 0)
            {
                Trace.WriteLine("[Exec] text-chat: no readable foreground window — not injecting");
                Spoken(Loc.T(UiText.Spoken.NoReadableWindow));
                return;
            }

            if (!_platform.SendKeys(chatKey))
            {
                Trace.WriteLine("[Exec] text-chat: opening key failed — aborting before typing");
                Spoken(Loc.T(UiText.Spoken.TextChatFailed));
                return;
            }

            await System.Threading.Tasks.Task.Delay(ChatBoxOpenDelayMs);

            if (_platform.ForegroundToken() != target)
            {
                Trace.WriteLine("[Exec] text-chat: foreground changed while the chat box was opening — aborting");
                Spoken(Loc.T(UiText.Spoken.WindowChangedOpening));
                return;
            }

            Trace.WriteLine($"[Exec] text-chat: sending {(action.ChatText ?? "").Length} chars");
            if (!_platform.SendText(action.ChatText ?? ""))
            {
                Trace.WriteLine("[Exec] text-chat: text injection incomplete — not sending Enter");
                Spoken(Loc.T(UiText.Spoken.TextChatFailed));
                return;
            }

            // Re-check once more: SendText can take a while for a long message, and Enter is the
            // irreversible half — it's what actually posts the text wherever it ended up.
            if (_platform.ForegroundToken() != target)
            {
                Trace.WriteLine("[Exec] text-chat: foreground changed mid-message — not sending Enter");
                // The worst case to leave silent: the text was typed somewhere, it just wasn't posted.
                Spoken(Loc.T(UiText.Spoken.WindowChangedMid));
                return;
            }
            if (!_platform.SendKeys("Enter"))
            {
                Trace.WriteLine("[Exec] text-chat: Enter injection failed — aborting confirmation");
                Spoken(Loc.T(UiText.Spoken.TextChatFailed));
                return;
            }
            Spoken(Loc.T(UiText.Spoken.MessageSent));   // the only confirmation this action has ever had
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Exec] text-chat aborted: {ex.Message}");
            Spoken(Loc.T(UiText.Spoken.TextChatFailed));
        }
    }

}
