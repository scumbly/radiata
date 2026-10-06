using System.Text.Json.Serialization;

namespace ControllerWheel;

/// <summary>Global behaviour settings, edited on the Settings "Advanced" tab. A record so callers can copy
/// a single field with `with` (e.g. bumping <see cref="OobeVersion"/>).</summary>
public sealed record SystemConfig
{
    /// <summary>Wheel fade-in duration in milliseconds (0 = instant).</summary>
    public int    FadeMs   { get; init; } = 150;

    /// <summary>How far (px) the wheel drifts in from toward screen centre (0 = no drift).</summary>
    public double DriftPx  { get; init; } = 150;

    /// <summary>Grace window (ms) where the last armed slice still fires after the stick recentres.</summary>
    public int    StickyMs { get; init; } = 150;

    /// <summary>Maximum action slices per wheel — fixed at 12, no user setting. The legacy <c>maxSlices</c>
    /// config key is ignored; <see cref="SliceThicknessRule"/> adapts the ring to the actual slice count.
    /// <c>ConfigLoader</c> keeps its own, higher hard cap against a hand-edited file.</summary>
    public const int MaxSlicesPerWheel = 12;

    /// <summary>Radial thickness of the slice ring: "thick" (full), "medium" (default, half), or "thin"
    /// (quarter). The wheel's outer edge is fixed; thinner only moves the slices' inner edge outward (the
    /// centre hub is unchanged). May be moved off "thick" automatically — see
    /// <see cref="SliceThicknessRule"/>.</summary>
    public string SliceThickness { get; init; } = "medium";

    /// <summary>True when <see cref="SliceThicknessRule"/> — not the user — moved
    /// <see cref="SliceThickness"/> off "thick" because a wheel grew past
    /// <see cref="SliceThicknessRule.ThickSliceLimit"/> slices. It's the permission to put "thick" back
    /// automatically once both wheels fit again. Any manual thickness pick clears it, ending the
    /// automatic restore.</summary>
    public bool   ThickAutoDemoted { get; init; } = false;

    /// <summary>Resting-slice fill material. Eight tokens ("flat-light", "pearl", "flat-dark" (default),
    /// "obsidian", "kawaii", "salvage", "mesa", "reactor"). <see cref="Materials"/> (Core) is the
    /// source of truth for the full vocabulary, display order, and dark/light classification — consult it
    /// rather than hardcoding a token list. Dark materials flip slice/hub text to light ink. Only the
    /// resting fill changes; armed/confirm states keep their colours. Legacy tokens are migrated
    /// by <see cref="Materials.Normalize"/>, which <c>ConfigLoader.Sanitize</c> runs on load, so the value
    /// seen anywhere in the app is canonical and the next save writes the new spelling back. An unknown
    /// or unregistered token normalises to Pearl, not to this default.</summary>
    public string SliceMaterial { get; init; } = Materials.FlatDark;

    /// <summary>Game Grid panel material — same vocabulary as <see cref="SliceMaterial"/>. Temporarily
    /// disabled as an independent setting: the grid always follows <see cref="SliceMaterial"/> instead, so
    /// this field is not read for rendering. Kept for config compatibility and a future re-enable.</summary>
    public string GameGridMaterial { get; init; } = "obsidian";

    /// <summary>The stored <c>custom-&lt;id&gt;</c> token that load could not resolve (its package is absent,
    /// unapproved, or withheld from this build) and rendered as Pearl instead. Never serialized: the save
    /// writes it back in place of "pearl" while the material is still Pearl, so the stored choice survives a
    /// run that could not show it. Any deliberate material write - a pick, the cycle, an Uninstall - clears
    /// it. Set only by <c>ConfigLoader.Sanitize</c>.</summary>
    [JsonIgnore] public string? HeldSliceMaterial { get; init; }

    /// <summary>Same as <see cref="HeldSliceMaterial"/>, for <see cref="GameGridMaterial"/>.</summary>
    [JsonIgnore] public string? HeldGameGridMaterial { get; init; }

    /// <summary>On-screen button-glyph set: "auto" ("Best guess" in the picker — Xbox A/B/X/Y for an
    /// XInput/Xbox-layout pad, PlayStation ✕○□△ otherwise), or "playstation" / "xbox" to force it. Auto
    /// can't tell a third-party pad in a Bluetooth/DS4-compatible mode from a real DualShock (it spoofs
    /// Sony's VID/PID), so the override is how to get Xbox glyphs there — the same trade-off Steam has.</summary>
    public string ButtonGlyphs { get; init; } = "auto";

    /// <summary>The app's language — a <see cref="HelpLocalization"/> code ("en" default; es / de / ja / ar).
    /// Help follows it live; the rest of the UI reads it once at startup (<see cref="Loc.Init"/>), so a change
    /// in Settings prompts a restart. Sanitised on read: an unknown code from a hand-edited file or a newer
    /// build's backup falls back to English rather than leaving the picker blank.</summary>
    public string? Language { get; init; }   // null = follow Windows (Loc.Init); a code once picked

    /// <summary>Force the wheel's centre hub circle visible on every material. Default false: hub
    /// visibility is uniform across materials — every material (Reactor included, whose hub exists only
    /// fused into the armed slice) draws it only when it carries information: a toggle-state readout, the
    /// volume scrubber, edit mode, or a notice box (onboarding's persistent "Practice" pill alone does not
    /// count as a reason — see <c>RadialMenuControl.DrawCenter</c>).
    /// <para>No material is exempt, Mesa included — this setting is the whole of the resting-hub rule; the
    /// only other way a hub appears is a per-frame reason in <c>RadialMenuControl.HubDemanded</c>.</para></summary>
    public bool   AlwaysShowHub { get; init; } = false;

    /// <summary>Reduce Motion (accessibility goal A2): the saved input to the product-wide
    /// <c>MotionPolicy</c>. The effective policy is this OR the Windows "show animations" preference
    /// (SPI_GETCLIENTAREAANIMATION) — either reduces; this setting wins in the reducing direction only
    /// (it can never force motion back on while the OS says reduce). Under the policy: no wheel intro drift/scale,
    /// no picker zoom, no edit reflow/landing/slide travel, no Reactor parallax/sparks/magnet-lock/shake,
    /// no Kawaii twinkles/confetti, no Salvage spark bloom or dwell sweep, no pour dwells (all guarded
    /// dwells use the static confirm arc), hub presence snaps. Opacity-only fades remain. New animations
    /// must check <c>MotionPolicy</c> rather than adding their own knob. Default false.</summary>
    public bool   ReduceMotion { get; init; } = false;

    /// <summary>Narration (accessibility goal A1): "Narrate wheel and Game Grid". Radiata speaks
    /// selection changes, guarded-hold stages, action results, and context changes through its own SAPI
    /// voice — the overlay's never-focused window is invisible to Narrator,
    /// so this is the primary channel, not a supplement. Default false: spoken output must never surprise
    /// a user who didn't ask for it.</summary>
    public bool   Narration { get; init; } = false;

    /// <summary>The Settings ▸ Accessibility "enable Windows Narrator" tip has been dismissed. Cleared
    /// whenever the user turns <see cref="Narration"/> back on: re-ticking the box is the moment the tip's
    /// advice is relevant again, so a dismissal only ever silences the tip for the run of narration it was
    /// dismissed in.</summary>
    public bool   NarratorTipDismissed { get; init; } = false;

    private readonly int _narrationVolume = 80;
    /// <summary>Speech volume 0–100 (SAPI's own scale), independent of <c>SoundEffects</c>/SfxEngine —
    /// quiet chimes with loud speech is a legitimate combination. Clamped on load.</summary>
    public int NarrationVolume
    {
        get => _narrationVolume;
        init => _narrationVolume = Math.Clamp(value, 0, 100);
    }

    /// <summary>Whether opening the Settings window puts the wheels in practice mode — summon/aim/arm
    /// normally, but firing runs nothing, so poking at a wheel mid-configuration can't launch a game or
    /// sleep the PC. Default false. The checkbox (Settings ▸ Advanced ▸ System) is Collapsed but its
    /// control and save path remain, so re-enabling is a one-attribute change. Onboarding's own practice
    /// preview ignores it either way.</summary>
    public bool   PracticeWhileSettingsOpen { get; init; } = false;

    /// <summary>Which slices draw their text label (Settings ▸ Customize ▸ "Show labels on").
    /// Four tokens; unknown/blank falls back to the default. The stored tokens are not the UI wording —
    /// the UI name follows each token in brackets:
    /// <list type="bullet">
    /// <item><c>"all-except-logos"</c> ["Icons, not Logos"] — the default. Only slices showing a built-in
    /// glyph are labelled; any slice carrying artwork (game logo, cover art, or a user-picked PNG) is not,
    /// since the artwork already carries the name.</item>
    /// <item><c>"all"</c> ["All Slices"] — every slice is labelled, artwork slices included.</item>
    /// <item><c>"none"</c> ["No Slices"] — no slice is labelled.</item>
    /// <item><c>"selected"</c> ["Slices I Choose"] — per-slice: honour each slice's own
    /// <c>WheelSlice.ShowLabel</c>. The only mode in which the slice editor's "Show label" checkbox is
    /// shown.</item>
    /// </list>
    /// Resolve with <see cref="SliceLabelRule.ShouldShow"/> — never re-implement the switch. The in-wheel
    /// editor and its Add picker still force labels on for non-logo slices whatever this says, so slices
    /// stay tellable apart while being rearranged.</summary>
    public string ShowSliceLabels { get; init; } = "all-except-logos";

    /// <summary>Anticheat passthru mode (capture profile). Default false = full capture: the always-on virtual
    /// pad + HidHide cloak. True = safe: Radiata presents no virtual pad and cloaks nothing, so a
    /// kernel-anticheat game (Valorant/EAC/BattlEye) never sees an emulated device or a hidden controller —
    /// only raw HID read + the topmost overlay remain, which are anticheat-inert. The wheel still works;
    /// the trade-off is that input bleeds through to the game while a wheel is open (single input, the same
    /// best-effort mode an XInput pad already runs in). Persists across launches. Toggle from the tray or
    /// Settings ▸ Passthru Mode (the manual, global switch — per-app automation is
    /// <see cref="SafeModeApps"/>).
    /// <para>The UI name is Passthru Mode; the <c>captureSafeMode</c> key spelling is frozen — renaming
    /// it orphans every existing config.</para></summary>
    public bool CaptureSafeMode { get; init; }

    /// <summary>Full capture for XInput/Xbox-class pads — on by default, so an Xbox pad isolates exactly
    /// like the Sony family. Radiata cloaks the pad's whole XUSB devnode tree (<c>XboxDeviceTree</c> —
    /// HidHide filters XnaComposite/XboxComposite, not just HIDClass) and only brings up the virtual Xbox
    /// 360 stand-in once the cloak is confirmed, so a game can never see both. Rumble forwards back to the
    /// physical pad.
    /// <para>Fail-safe by construction: if the tree won't cloak — an untested pad family, HidHide missing,
    /// a peer mapper holding the device — <c>Hide()</c> doesn't confirm, no virtual pad is presented, and
    /// the pad degrades to best-effort mode (wheel works; input bleeds through while a wheel is open).
    /// Setting this false forces that mode permanently: the escape hatch for a pad that misbehaves.
    /// No UI — config.json only.</para>
    /// <para>Not yet verified end to end on hardware: deliberate coexistence with Steam Input — the fail-safe
    /// above is what makes shipping that unknown acceptable. The Bluetooth cloak path is verified.</para></summary>
    public bool XInputCapture { get; init; } = true;

    /// <summary>The Steam-first startup leak's silent cure. When steam.exe predates Radiata's session with
    /// the pad already attached, Steam holds a pre-cloak handle the cloak can't revoke (it blocks future
    /// opens only) and forwards physical input to Steam Input games alongside the virtual pad — double
    /// input. With this on (default) and nothing in the foreground, Radiata relaunches Steam once at
    /// startup (<c>-shutdown</c>, wait, <c>-silent</c>) so its fresh pad-open is denied by the live cloak.
    /// False keeps detection and the alert but never touches Steam. No UI — config.json only.
    /// The caller also requires an idle desktop and no detected running game. A foreground Steam
    /// window therefore defers the automatic cure; user-consented restart paths are separate.</summary>
    public bool SteamAutoRelaunch { get; init; } = true;

    /// <summary>Whether a dead Bluetooth link raises the "Controller Signal Lost" card. On by default: the
    /// reader has exhausted in-app recovery by then, so without the card the pad simply stops answering and
    /// the only fix (cycling the PC's radio) is unguessable. Off is for someone whose pad drops often enough
    /// that the card is noise rather than news. Settings ▸ Advanced ▸ Current Controller, and the checkbox
    /// is shown only while a pad reports Bluetooth — a setting for a transport you aren't on is clutter.</summary>
    public bool BluetoothDropAlert { get; init; } = true;

    /// <summary>Post-onboarding edit-discovery hint: false (default) = the first wheel(s) opened after
    /// finishing the setup wizard show a centre-hub reminder that clicking the aiming stick edits the
    /// wheel, for a ~10 s discovery window; set true when that window closes so it never shows again.
    /// Re-armed to false by MarkOnboarded (completing the wizard again re-teaches it).</summary>
    public bool EditHintSeen { get; init; }

    /// <summary>Apps/games that engage anticheat passthru mode automatically (Settings ▸ Passthru Mode). While
    /// any entry matches a running (or frontmost, per entry) process, capture behaves as if
    /// <see cref="CaptureSafeMode"/> were on — via a runtime flag, never by writing this config, so
    /// automation can't clobber the user's manual toggle. Empty (default): the process watcher doesn't run
    /// at all.
    /// <para>The UI name is Passthru Mode; the <c>safeModeApps</c> key spelling is frozen — renaming it
    /// drops every user's exception list.</para></summary>
    public List<SafeModeApp> SafeModeApps { get; init; } = new();

    /// <summary>Swap which side opens which wheel. Default: Left side = Wheel A, Right side = Wheel B.
    /// Applies to Fn buttons and to the chord trigger modes (flips the side→wheel mapping).</summary>
    public bool   SwapFnButtons { get; init; } = false;

    /// <summary>Which controller gesture(s) summon the wheels, stored per controller kind
    /// (<see cref="ControllerKind"/>.ToString() → the set of enabled gesture tokens). More than one may be
    /// enabled at once (e.g. Fn buttons and touchpad swipe). Edited only for the currently-detected kind;
    /// other kinds' choices are preserved for when V4 reads them. Missing/empty falls back to that kind's
    /// default. The converter tolerates the legacy scalar form (a single string).</summary>
    [JsonConverter(typeof(TriggerModesConverter))]
    public Dictionary<string, List<string>> TriggerModes { get; init; } = new();

    /// <summary>Wheel activation style: "hold" (default — wheel is up only while the trigger is held,
    /// release fires) or "toggle" (trigger opens the wheel; ✕ confirms, ○ cancels). Touchpad-swipe always
    /// behaves toggle-style regardless of this setting.</summary>
    public string TriggerActivation { get; init; } = "hold";

    /// <summary>Accessibility opt-out from either-stick aiming. Default false — either thumbstick aims and
    /// arms slices (the more-deflected one wins) under Hold, Toggle and in-wheel edit alike, and either
    /// stick's click enters edit mode. True confines all of that to the wheel's own aiming stick.
    /// <para>It exists for the cancel guarantee: "release while centred = cancel" only holds if the sticks
    /// it watches are trustworthy, and a worn pad's resting drift on the second stick can arm a slice by
    /// itself, turning a cancel into a fire. Settings ▸ Advanced ▸ Accessibility; deliberately independent
    /// of "Wheels toggle on/off" — no soft link.</para></summary>
    public bool   WheelIgnoresOppositeStick { get; init; } = false;

    /// <summary>Show the system-tray icon. Default true. When false the app runs with no tray icon
    /// (the wheel and hotkeys still work); relaunch the EXE to open Settings.</summary>
    public bool   ShowTrayIcon { get; init; } = true;

    /// <summary>While a wheel is open, d-pad up/down adjusts system volume (with the scrubber arc in
    /// the hub). Off frees the d-pad while a wheel is up — no volume change. Default true.</summary>
    public bool   DpadVolumeWhileOpen { get; init; } = true;

    /// <summary>D-pad-volume auto-repeat: how long a held d-pad up/down waits before it starts repeating
    /// (ms). Default 400 (classic key-repeat hold delay).</summary>
    public int    DpadVolumeRepeatDelayMs { get; init; } = 400;

    /// <summary>D-pad-volume auto-repeat: the interval between volume steps once repeating (ms); lower =
    /// faster. Default 80.</summary>
    public int    DpadVolumeRepeatIntervalMs { get; init; } = 80;

    /// <summary>What D-pad ◀ ▶ does while a wheel is open: "switcher" (Alt-Tab forward/backward — Alt is
    /// held down until the wheel closes, which commits the highlighted window — the default, and first in
    /// the Advanced-tab list), "mic" (microphone level, shown in the hub scrubber under a Microphone glyph),
    /// "desktop" (next/previous Windows virtual desktop via Win+Ctrl+←/→), "song" (previous/next track
    /// media keys, with a large skip glyph in the hub), or "mixer" (the application Volume Mixer). Unknown
    /// tokens fall back to "mixer".
    /// <para>"mixer" is not selectable — broken on device, so it's absent from the Advanced-tab picker.
    /// Everything behind it is intact and still honours an existing config that says "mixer"; restoring the
    /// picker entry is all that's needed once it's fixed.</para></summary>
    public string DpadHorizontalMode { get; init; } = "switcher";

    // MixEnabled / MixDeviceA / MixDeviceB: unused by the device mixer (the mixer is app-session
    // based and resolves its pair live — see AppVolumeMixer). Properties kept so old configs still
    // deserialize; nothing reads them.
    public bool    MixEnabled { get; init; } = false;
    public string? MixDeviceA { get; init; }
    public string? MixDeviceB { get; init; }
    /// <summary>Volume Mixer balance (0..100; 50 = both sides full). Persisted so the mix survives
    /// restarts.</summary>
    public int    MixBalance { get; init; } = 50;

    /// <summary>obs-websocket server port (OBS ▸ Tools ▸ WebSocket Server Settings). Default 4455.</summary>
    public int    ObsPort { get; init; } = 4455;

    /// <summary>The user has set up the OBS connection (saved the fields or passed a Test). Gates the
    /// configure-on-fire flow: firing an OBS slice before setup opens it in the editor. Older
    /// configs with a saved password count as configured (see App's check).</summary>
    public bool   ObsConfigured { get; init; } = false;

    /// <summary>obs-websocket server password, DPAPI-protected (same at-rest posture as the Discord
    /// credentials — never stored in plain text). Null/empty = OBS integration unconfigured.</summary>
    public string? ObsPassword { get; init; }

    /// <summary>Play UI sound effects: slice armed/fired, wheels on/off, grid selection. Default true.
    /// Edited (together with <see cref="SoundTheme"/>) via the four-tile "Sound effects" picker on
    /// Settings ▸ Customize — Themed / Digital / Physical / Silent, where Silent = this flag off (the
    /// last theme is preserved).</summary>
    public bool   SoundEffects { get; init; } = true;

    /// <summary>Which sound set the wheels use: "material" (default; the picker's "Themed" tile — follow
    /// the selected material's own
    /// set, resolved via <c>Materials.SoundThemeFor</c>: kawaii/mesa/salvage/reactor ship their own,
    /// Pearl/Obsidian take digital, the flats physical), "digital" (the original slice-armed/slice-fired),
    /// or "physical" (tap/selected) as explicit overrides. Set by the Customize "Sound effects" tile
    /// picker (see <see cref="SoundEffects"/>); read into <c>Sfx.Theme</c> after resolution (Sfx also
    /// tolerates the legacy stored tokens "kawaii"/"sparkle"/"tap"/"classic").</summary>
    public string SoundTheme { get; init; } = "material";

    // An explicit digital/physical pick holds only until the next material change, which snaps the picker
    // back to "material" — there is deliberately no manual latch.

    /// <summary>Optional user-supplied SteamGridDB API key. When set, game-browser tiles for stores
    /// with no built-in art source (Epic/GOG/Xbox/itch) fetch portrait cover art from SteamGridDB.
    /// Never bundled — the user provides their own key. Null/blank = feature off. Stored as a DPAPI
    /// blob (the WPF shell's SecretField protects/unprotects; legacy plaintext values are migrated at
    /// startup) — so the key deliberately doesn't survive a backup restored under another Windows
    /// user/machine and must be re-entered there.</summary>
    public string? SteamGridDbKey { get; init; }

    /// <summary>Preferred SteamGridDB grid style token (e.g. "no_logo", "alternate", "white_logo",
    /// "material", "blurred"), or null/blank for any. Only affects the SteamGridDB art source.</summary>
    public string? SgdbStyle { get; init; }

    /// <summary>When both Playnite and a SteamGridDB key are configured: prefer Playnite's own cover
    /// (Playnite → SteamGridDB → Steam). Off (default) = SteamGridDB → Playnite → Steam. Sources that
    /// aren't configured are skipped either way; Steam CDN is the last-resort fallback.</summary>
    public bool PreferPlayniteCovers { get; init; }

    /// <summary>Check getradiata.app/update for a newer version (~60 s after startup and every 24 h while
    /// resident). Default true. The check sends only the app version — the query string on the static
    /// latest.json fetch (PRIVACY.md; no other telemetry rides along).
    /// Off = no automatic network contact at all; the Settings ▸ Advanced "Check for Updates" button
    /// still works on demand. Updates are always prompt-only — this flag gates the check, nothing installs
    /// without the user pressing Update Now.</summary>
    public bool UpdateCheckEnabled { get; init; } = true;

    /// <summary>Feed version the user chose "Skip This Version" on ("" = none). Automatic checks stop
    /// prompting for exactly this version; a still-newer feed version clears it and prompts again, and the
    /// manual Settings check ignores it entirely. Written by the update prompt window, not by any Settings
    /// tab.</summary>
    public string UpdateSkippedVersion { get; init; } = "";

    /// <summary>Feed version the user pressed "Later" on in the update prompt ("" = none). The prompt
    /// offers "Skip This Version" only when this equals the version it is showing. Written by the update
    /// prompt window, not by any Settings tab.</summary>
    public string UpdateDeferredVersion { get; init; } = "";

    /// <summary>Offer to send a crash report on the first startup after a crash. Default true. The offer is
    /// consent UI only — nothing sends without the user pressing Send, and the full report text is shown
    /// first (PRIVACY.md). False (the consent window's Don't send with "Remember this choice" ticked) = pending-crash.txt is left
    /// alone and no window appears; the crash-time write still happens so a report can be sent later if the
    /// user re-enables this.</summary>
    public bool CrashPromptEnabled { get; init; } = true;

    /// <summary>Send the pending crash report without showing the consent window. Default false. Set by the
    /// consent window's Send with "Remember this choice" ticked; the text sent is the same report the window
    /// would have shown (PRIVACY.md). Failure keeps pending-crash.txt for the next launch.</summary>
    public bool CrashAutoSend { get; init; } = false;

    /// <summary>Onboarding (OOBE) version the user has completed; 0 = never onboarded. The app runs the
    /// first-run flow when this is below the current OOBE version (which is bumped when the flow changes,
    /// so a meaningfully-updated onboarding can re-run). Set by the flow on completion.</summary>
    public int OobeVersion { get; init; }

    /// <summary>The user explicitly chose to go without the isolation drivers (the onboarding "Skip
    /// Drivers" choice). While set, a missing driver is the chosen state and must not raise the
    /// missing-driver isolation warnings — a durable opt-out, distinct from "drivers happen to be
    /// missing" (which genuinely warrants a warning when they vanish unexpectedly). Cleared at startup
    /// once both drivers are detected installed, so a later install re-arms the warnings.</summary>
    public bool DriversDeclined { get; init; }

    /// <summary>The ViGEmBus origin string (program name + bus devnode ids) the once-per-install "another
    /// program supplies this driver" notice was last shown for; null = never. A different fork or a
    /// re-enumerated bus changes the string and notices again. Written by the app at startup, not a user
    /// setting.</summary>
    public string? ForeignViGEmBusNoticed { get; init; }

    /// <summary>The onboarding step the user had reached when the wizard was last closed unfinished
    /// (0-based; 0 = the first step). Written on every step change while the flow is incomplete and cleared
    /// when it completes, so closing the window part-way and coming back resumes where it left off instead
    /// of restarting at Welcome. Clamped by the flow — a value from a build with more steps must not land
    /// past the end.</summary>
    public int OobeStep { get; init; }

    /// <summary>Whether the one-time Game Grid cover-art tip (Select/Start to fine-tune covers/logos) has been
    /// shown. Set true after the first Game Grid open so the tip appears only once.</summary>
    public bool GameGridTipSeen { get; init; }

    /// <summary>Storefronts the user opted out of (InstalledGame.Storefront names, e.g. "Epic") — their
    /// games are excluded from the Game Grid and from onboarding's starter wheels; empty = include
    /// everything found. Authored from the Game Grid: holding the hide button on a storefront's
    /// "Open &lt;store&gt;" card appends to this list, and Settings ▸ Advanced ▸ Reset Hidden Games
    /// clears it.</summary>
    public List<string> DisabledStorefronts { get; init; } = new();

    /// <summary>Controller kinds Radiata has already introduced to the user (<see cref="ControllerKind"/>
    /// .ToString()). A kind not in this list is treated as never-seen: connecting one shows a one-time
    /// actionable notification (its summon gesture + an offer to run setup). Appended the first time each
    /// kind is seen and when onboarding completes. Empty on a fresh config (the first connect silently
    /// adopts whatever's attached as the baseline, so an existing pad isn't announced as "new").</summary>
    public List<string> KnownControllerKinds { get; init; } = new();

    /// <summary>The controller kind that was active at the last change (<see cref="ControllerKind"/>
    /// .ToString()), or null if none recorded. Compared on the first connect of a session so a pad swapped
    /// between runs is announced just like a mid-session swap; updated whenever the active kind changes.</summary>
    public string? LastControllerKind { get; init; }

    /// <summary>The active trigger token set for a controller kind — the per-kind stored choices, or a
    /// single-element list with that kind's default when unset/empty.</summary>
    public IReadOnlyList<string> TriggerModesFor(ControllerKind kind) =>
        TriggerModes.TryGetValue(kind.ToString(), out var list) && list is { Count: > 0 }
            ? list
            : [ControllerWheel.TriggerModes.DefaultFor(kind)];

    /// <summary>The primary (first) trigger token for a controller kind — for callers that still want a
    /// single gesture. Multiple gestures can be enabled; see <see cref="TriggerModesFor"/>.</summary>
    public string TriggerModeFor(ControllerKind kind) => TriggerModesFor(kind)[0];
}

/// <summary>One automatic passthru-mode exception (Settings ▸ Passthru Mode): a game/app whose presence switches
/// Radiata into anticheat passthru mode (see <see cref="SystemConfig.CaptureSafeMode"/> for what passthru mode
/// does). Two flavours, both matched by the running process's image path (case-insensitive):
/// <list type="bullet">
///   <item>An exe entry (<see cref="MatchFolder"/> = false, added via "Add Application…"): HidHide-style —
///   <see cref="Path"/> is a full .exe path; a running process matches by exact image path, or by exe name
///   when its image path can't be read (an elevated/protected anticheat process).</item>
///   <item>A folder entry (<see cref="MatchFolder"/> = true, added from the installed-games dropdown):
///   <see cref="Path"/> is a game's install directory; any running process whose image path is inside that
///   directory matches — robust for storefront games whose launcher spawns an exe we can't predict.</item>
/// </list>
/// Wrongly engaging passthru mode is the low-harm direction, so ambiguous cases resolve toward a match.</summary>
public sealed record SafeModeApp
{
    /// <summary>The .exe path (an exe entry) or the install-directory path (a folder entry — see
    /// <see cref="MatchFolder"/>).</summary>
    public string Path { get; init; } = "";

    /// <summary>False (default, recommended): passthru mode engages while the app is running at all — kernel
    /// anticheat watches from launch and stays resident, and this avoids a controller re-detect on every
    /// alt-tab. True: engage only while the app's window is frontmost (focused).</summary>
    public bool FrontmostOnly { get; init; }

    /// <summary>True = <see cref="Path"/> is a game install directory: match any process running from
    /// inside it (added from the installed-games dropdown). False (default) = <see cref="Path"/> is a
    /// single .exe.</summary>
    public bool MatchFolder { get; init; }

    /// <summary>Friendly display name (a game's title, for folder entries). Null = derive from
    /// <see cref="Path"/> (the exe/folder name).</summary>
    public string? Name { get; init; }
}

/// <summary>The automatic Thick→Medium slice-thickness rule.
///
/// Thick fills the whole ring, which reads well up to <see cref="ThickSliceLimit"/> slices and turns into
/// an unreadable solid band beyond it. Growing a wheel past the limit moves both wheels to Medium and
/// records <see cref="SystemConfig.ThickAutoDemoted"/>; shrinking back under it restores Thick (the
/// demotion was the rule's, not the user's). A manual thickness pick clears the flag, ending the automatic
/// restore.
///
/// Lives in Core and takes the whole <see cref="SystemConfig"/> in and out so the two write paths that can
/// change a slice count — the Settings save and the in-wheel editor's live save — share one implementation.</summary>
public static class SliceThicknessRule
{
    /// <summary>The most slices a wheel can hold and still render Thick.</summary>
    public const int ThickSliceLimit = 8;

    /// <summary>Fold the rule into <paramref name="sys"/> for the given per-wheel slice counts. Returns
    /// <paramref name="sys"/> unchanged whenever nothing needs to move (the common case), so callers can
    /// pass the result straight to a config write without comparing.</summary>
    public static SystemConfig Apply(SystemConfig sys, int wheelACount, int wheelBCount)
    {
        int most = Math.Max(wheelACount, wheelBCount);
        if (most > ThickSliceLimit)
            return sys.SliceThickness == "thick"
                ? sys with { SliceThickness = "medium", ThickAutoDemoted = true }
                : sys;
        // Back within the limit: undo the rule's own demotion only.
        return sys.ThickAutoDemoted
            ? sys with { SliceThickness = "thick", ThickAutoDemoted = false }
            : sys;
    }

    /// <summary>Whether Thick can be offered at all for these counts — the Settings picker hides the tile
    /// when it can't apply (an option that would be reverted on save isn't an option).</summary>
    public static bool ThickAllowed(int wheelACount, int wheelBCount) =>
        Math.Max(wheelACount, wheelBCount) <= ThickSliceLimit;
}

/// <summary>The one place <see cref="SystemConfig.ShowSliceLabels"/> is interpreted. The per-slice flag
/// (<see cref="WheelSlice.ShowLabel"/>) is honoured only in the "selected" mode.
///
/// Lives in Core beside the mode it reads so the wheel renderer, the Settings editor (which shows the
/// per-slice checkbox only in "selected"), and any future preview surface all agree.</summary>
public static class SliceLabelRule
{
    /// <summary>The default mode — also the fallback for a blank or unrecognised token.</summary>
    public const string Default = "all-except-logos";

    /// <summary>Every legal token, in the order the Settings picker offers them.</summary>
    public static readonly string[] Modes = ["all-except-logos", "all", "none", "selected"];

    /// <summary>Canonical form of a stored/UI token: trimmed, lower-cased, and coerced to
    /// <see cref="Default"/> if it isn't one of <see cref="Modes"/>.</summary>
    public static string Normalize(string? mode)
    {
        string m = mode?.Trim().ToLowerInvariant() ?? Default;
        return Array.IndexOf(Modes, m) >= 0 ? m : Default;
    }

    /// <summary>Whether <paramref name="slice"/> draws its text label under <paramref name="mode"/>.
    /// <para>Does not account for the in-wheel editor's force-labels-on override — that's a render-time
    /// concern layered on top by the caller (see RadialMenuControl), deliberately not folded in here so
    /// this stays a pure function of the setting and the slice.</para></summary>
    public static bool ShouldShow(WheelSlice slice, string? mode) => Normalize(mode) switch
    {
        "all"      => true,
        "none"     => false,
        "selected" => slice.ShowLabel ?? DefaultShowLabel(slice),
        _          => IsGlyphOnly(slice),   // "all-except-logos" → "Icons, not Logos" (default)
    };

    /// <summary>Whether a slice carries only a built-in Material Design glyph — no game logo
    /// (<see cref="WheelSlice.LogoPath"/>) and no image file of any kind (<see cref="WheelSlice.IconPath"/>:
    /// cover art and a user-picked PNG both land there). This is what the default mode labels — artwork
    /// already names the thing it depicts, so labelling it double-titles the slice.
    /// <para>The asymmetry with "selected" is deliberate: its null-default fallback in
    /// <see cref="ShouldShow"/> is <see cref="DefaultShowLabel"/>, standing in for a slice written before
    /// <c>ShowLabel</c> was seeded on creation, so it must keep reproducing that appearance rather than
    /// track this mode.</para>
    /// <para>The Arcade Launcher glyph carries its own wordmark, so it counts as artwork here.</para></summary>
    private static bool IsGlyphOnly(WheelSlice slice) =>
        string.IsNullOrWhiteSpace(slice.LogoPath) && string.IsNullOrWhiteSpace(slice.IconPath)
        && !IsLauncherGlyph(slice);

    /// <summary>A slice's label visibility when <see cref="WheelSlice.ShowLabel"/> is unset: off for a custom
    /// logo and for the Arcade Launcher's wordmark glyph, on otherwise.</summary>
    public static bool DefaultShowLabel(WheelSlice slice) =>
        string.IsNullOrWhiteSpace(slice.LogoPath) && !IsLauncherGlyph(slice);

    private static bool IsLauncherGlyph(WheelSlice slice) =>
        ArcadeCatalog.IsLauncherGlyphSlice(slice.IconName, slice.Action?.Type, slice.Action?.Command);
}
