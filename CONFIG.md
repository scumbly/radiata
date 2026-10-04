# Config schema: `config.json`

The overlay reads `%APPDATA%\Radiata\config.json`. Changes saved from the Settings window apply
immediately. **External edits** (hand-editing the file) are picked up unreliably, so restart the app
after editing it by hand. JSON keys are camelCase (matched case-insensitively on read). Keys the app does
not recognise are ignored, and are not written back on the next save.

## Top level

```jsonc
{
  "wheelA": [ /* up to 12 slices, opened by one trigger side */ ],
  "wheelB": [ /* up to 12 slices, opened by the other side */ ],
  "system": { /* global behaviour */ },
  "actionColors": { /* per-action-type icon TINT overrides */ },
  "actionIcons":  { /* per-action-type default-GLYPH overrides (same keys as actionColors) */ },
  "customColors": [ /* saved colour-picker palette */ ],
  "savedByVersion": "0.17.1923.260930"  // written by the app on every save; leave it alone
}
```

Which side opens which wheel is controlled by `system.swapFnButtons`; the invocation gesture itself
(Fn buttons / chords / touchpad swipe, and Hold vs Toggle activation) by `system.triggerModes` +
`system.triggerActivation`. An empty wheel (`[]`) is valid and acts as a **disabled side**: invoking it
draws nothing and doesn't capture input, so that side's gesture stays usable in-game. To bring it back,
invoke it (hold the gesture) and **click the aiming stick**: the wheel opens straight into the editor's
Add picker.

The editor holds up to 12 slices per wheel; a hand-edited file is trimmed to its first 32 (see
[Validation on load](#validation-on-load)). When `savedByVersion` differs from the running version at
startup, the app first copies the existing file into `backups\` (the newest 10 copies are kept).

## Slice

```jsonc
{
  "label": "Mute Mic",          // text shown on the slice. An authored default (starter slices, in-wheel Add) is stored
                                // in English and translated on display in a non-English UI; your own text shows as typed
  "iconName": "VolumeMute",     // MahApps Material icon name (optional)
  "iconPath": "C:\\...\\art.jpg", // image file to draw instead of a glyph (optional; cover art)
  "logoPath": "C:\\...\\logo.png", // transparent game logo for assigned-game slices (optional; drawn tinted)
  "iconColor": "#3CA850",       // per-slice icon tint override (optional; "#RRGGBB")
  "iconColorExact": true,       // true = iconColor is a HAND-TYPED hex and paints verbatim on every
                                // material. Absent/false = iconColor is a swatch base tone, so the
                                // material may tune it (e.g. mesa brightens them). Authored by the slice
                                // editor: typing a Hex sets it, picking a swatch clears it
  "iconColorLight": "#7ED89A",  // read-only: the light half of an older paired-palette pick. Honoured for slices
                                // that carry it, and (when iconColorExact is absent) its presence marks the
                                // colour as a typed hex rather than a swatch. Never written
  "showLabel": true,            // draw this slice's text label (optional). Only consulted when
                                // system.showSliceLabels is "selected". Slices created by the editor or the
                                // in-wheel Add picker are written with it false, so that mode starts unlabelled.
                                // Absent = shown, except on a slice with a custom game logo or the Arcade launcher glyph
  "action": { /* see below */ }
}
```

Icon resolution order: `iconPath` (image) → `iconName` (Material glyph) → for a `launch` slice with a
`path`, the extracted .exe icon → the action type's default glyph (`actionIcons` can override it) → a
built-in monochrome shape. Glyph icons are tinted by action type (or `iconColor`); extracted .exe icons
are never tinted.

## Action

`action.type` selects the behaviour; only the fields relevant to that type are used.

| `type`            | Fields used | Notes |
|-------------------|-------------|-------|
| `launch`          | `path`, `process?`, `quitIfRunning?` | Launch the app; if already running, **focus** it. With `quitIfRunning` true (Toggle behavior, shows On/Off) firing a running app **requests a close** instead (the app can still prompt to save). `process` overrides the exe name used to detect and act on the running instance; it is honoured when present, but the editor derives the name from the exe and does not author it. |
| `script`          | `path` | Run a file (.ahk/.ps1/.bat/…) via shell. Not offered for new slices; existing slices and `sequence` `script:` steps run normally |
| `toggle`          | `process`, `path?` | If `process` is running, request a close; otherwise launch `path`. The same behaviour as `launch` with `quitIfRunning`. Not offered for new slices (use `launch`); still used by the `sequence` step text form |
| `system`          | `command` (+ `level` for `volume-set`, `powerPlan`/`powerPlanB` for `power-plan`, `logInAfterReboot?` for `reboot`) | See system commands below |
| `keypress`        | `keys` | One key with optional modifiers joined by `+`, e.g. `Win+D`, `Ctrl+Shift+Esc`, `VolUp`, `PlayPause`. `Ctrl+Alt+Del` cannot be sent and is rejected |
| `switch-audio`    | `device`, `micDevice` | Partial name match. Both blank = cycle the output; `device` set = switch to that output; `micDevice` set = switch the microphone (a mic-only slice leaves the output alone); set both to switch both. The readout names the device switched to |
| `discord-launch`  | none | Launch the Discord desktop app, or focus it if already running |
| `discord-join`    | `url` | `discord://discord.com/channels/<guild>/<channel>` (the editor auto-normalizes pasted links). Needs Discord app credentials, see below |
| `discord-mute`    | none | Toggle Discord's own self-mute over the local RPC session. Needs Discord app credentials, see below. Not offered for new slices (the system `mic-mute` command is the offered mute); existing slices keep working. A `keys` value on an older slice is accepted and ignored |
| `discord-deafen`  | none | Toggle Discord's own self-deafen over the local RPC session. Needs Discord app credentials, see below. A `keys` value on an older slice is accepted and ignored |
| `url`             | `url` | Open any URI: `https://`, `steam://`, `discord://`, … |
| `installed-game`  | `url` | Launch a game by its storefront URL (e.g. `steam://rungameid/<id>`) |
| `launcher`        | `command` | Open a storefront's client (UI label: **Storefront**): `steam` (Big Picture), `playnite` (fullscreen), `xbox` (maximized window), `gog`, `epic`, `ubisoft`, `amazon`, `itch`, `battlenet`, `ea`. Shows "Not Installed" when the client can't be found |
| `exit-app`        | none | Close the app or game currently in the foreground, as if you clicked its ✕ (so it can prompt to save). The armed readout names the app |
| `steam-mute`      | `keys` | Steam voice: sends the key combo you bound in Steam to mic mute / push-to-talk (UI label: **Mic Mute / Push-to-Talk**). Steam ships those hotkeys unbound, so the slice does nothing until `keys` is set. Not offered for new slices |
| `obs`             | `command`, `obsTarget?` | obs-websocket: `obs-toggle-stream`, `obs-toggle-record`, `obs-save-replay`, `obs-set-scene` (`obsTarget` = scene name, required), `obs-toggle-mic` (`obsTarget` = audio source name, default `Mic/Aux`). Needs `system.obsPort` / `obsPassword`, set in Settings ▸ Advanced ▸ Integrations; firing an unconfigured OBS slice opens its editor, which offers that same setup pane |
| `text-chat`       | `command`, `keys?`, `chatText?` | Open the running game's in-game **text** chat, type `chatText`, then press Enter. `command` is the mode: `default` looks up the chat key for the game that owns the foreground in a built-in table (`Core/GameChatButtons.cs`) on every fire, and refuses unless that game is a confirmed entry; `custom` sends `keys` instead. At most one send every 15 seconds |
| `game-browser`    | none | Open the Game Grid (all-games browser overlay) |
| `arcade`          | `command?` | Open a little game in the round arcade window. `command` is a game id: `kabloom`, `connate`, `petalpop`, plus `internode` and drop-in `pkg-<id>` games (both not available in public releases). **Blank/absent `command` = the Arcade picker**, and an unknown id resolves the same way, so a config naming a game this build lacks still works. Saved games, high scores and arcade preferences live in `arcade-state.json`, not here |
| `xbox-emulation`  | none | Toggle Xbox Mode: virtual Xbox-360 pad instead of the native virtual DS4. Shows On/Off |
| `dualshock-emulation` | none | Toggle DualShock Mode: the same pad-type switch viewed from the DS4 side (for Xbox-pad users; On = native DS4) |
| `disable-wheels`  | none | Turn the wheels off (the same state as the both-trigger chord): summons are ignored and the controller passes straight through to games. The virtual pad and cloak stay as they are. Re-enable with the chord or the tray menu (a wheel slice can't, because its own wheel is off) |
| `safe-mode`       | none | Toggle Anticheat Passthru Mode, the same flip as the Settings ▸ Passthru Mode checkbox and the tray item. Shows On/Off. Not offered in the slice pickers; an existing slice resolves and works. Toggle Passthru Mode from the tray or Settings ▸ Passthru Mode instead |
| `sequence`        | `steps` | Run several actions in order (see below). Not offered for new slices; existing sequence slices keep working |
| `settings`        | none | Open the Settings window |

Any action may also set:

- `"requireConfirm": true` guards a destructive action: it fires only after the stick is held on
  the slice ~0.8s (a red fill-ring), not on release. Labeled **Hold to confirm** in the editor. The
  editor and the in-wheel Add picker turn it on for new `exit-app` slices and for a `system` command of
  `sleep`, `reboot`, `shutdown`, `logout`, `hibernate`, or `empty-recycle-bin`; a hand-written slice
  defaults to off.
  (In edit mode a guarded slice arms like any other; the dwell only applies to real firing.)

### Discord RPC setup (`discord-join`, `discord-deafen`, `discord-mute`)

These three actions speak to the Discord desktop client over its local RPC pipe, so each needs your own
Discord application's OAuth credentials. None are bundled (a desktop app can't keep a client secret secret,
and Discord's token endpoint requires one). One-time setup:

1. Create an app at the Discord Developer Portal → **OAuth2**. Add redirect URI `http://localhost`.
2. Copy the **Client ID** and **Client Secret**.
3. Enter them through **Settings ▸ Advanced ▸ Integrations ▸ Configure Discord Integration…**, or the same
   button a Join/Leave Voice Channel, Deafen or Mute Me slice shows in its editor. They're saved to
   `%APPDATA%\Radiata\discord-oauth.json`, **DPAPI-encrypted at rest** (only your Windows user on this
   machine can read them), so the file is an opaque blob, not the JSON below.

The *decrypted* contents are simply:

```jsonc
{
  "clientId": "your-client-id",
  "clientSecret": "your-client-secret",
  "redirectUri": "http://localhost"   // optional; defaults to http://localhost
}
```

You can hand-place a plaintext `discord-oauth.json` in that shape if you prefer: Radiata reads it
once and rewrites it encrypted. Without the file all three actions show the setup prompt instead of
firing. The OAuth access/refresh tokens Discord issues are cached, **also DPAPI-encrypted**, in
`discord-token.json` (delegated account access; never written to a log, and `discord-debug.txt`
redacts token material).

⚠ **Deafen and Mute Me need the `rpc.voice.read` and `rpc.voice.write` scopes as well as `rpc`.** A token
cached with the `rpc` scope alone is replaced: the first Deafen or Mute Me you fire re-runs the Discord
approval dialog once and caches a token with all three. Join/Leave is unaffected either way.

### System commands (`type: "system"`)

| Group | Commands |
|---|---|
| **Power** | `sleep`, `hibernate`, `reboot` (optional `logInAfterReboot`, default **true**: true/absent = restart with Automatic Restart Sign-On, `shutdown /g`; **false** = a traditional `shutdown /r`, landing at the sign-in screen when one is due), `shutdown`, `logout`, `lock`, `power-plan` (UI label: **Toggle Power Plan**; uses `powerPlan` + `powerPlanB`, both Windows power-scheme GUIDs; firing toggles: active == `powerPlan` → activate `powerPlanB`, else → `powerPlan`. `powerPlanB` absent (a single-plan config) = plain "activate `powerPlan`"; the editor loads such a slice with the stored plan as A and defaults B to Balanced. Reports the activated plan's name, or `Failed`, because `powercfg` can no-op silently) |
| **Audio** | `volume` (toggle mute), `mic-mute`, `volume-set` (uses `level` 0.0–1.0) |
| **Media keys** | `media-play-pause`, `media-next`, `media-prev`, `media-mute`: fire-and-forget keystrokes to whatever player owns media focus; no readout. (`media-mute` works but isn't offered in the editor.) |
| **Display** | `display-toggle` (reads the CURRENT topology and flips Extend⇄Clone; if the topology can't be determined it switches to Extend and shows no state), `hdr-toggle` |
| **Windows** | `show-desktop` (toggles minimize/restore of all windows, like the taskbar corner), `empty-recycle-bin` |
| **Game Bar** | `gamebar-open`, `gamebar-screenshot`, `gamebar-record` (start/stop), `gamebar-record-last` (last 30 s), `gamebar-mic`: the OS-standard hotkeys; Game Bar must be enabled in Windows Settings |
| **Chat UIs** | `steam-chat-open` (`steam://open/friends`) |

Toggle-like commands (`mic-mute`, `volume`, `hdr-toggle`) show their state in the hub when armed
and after firing; HDR reads `Unavailable` when the display state can't be queried (e.g. over RDP).

> **Retired types and commands.** A slice carrying one of these still loads; firing it traces and does
> nothing, via the executor's unknown-type / unknown-command fallback.
> Types: `powershell`, `mumble-*`, `ts3-join`, `wol`, `http`.
> System commands: `display-extend`, `display-clone`, `display-external`, `display-internal` (replaced by
> `display-toggle`), the NVIDIA and AMD capture and overlay commands (`nvidia-*`, `amd-*`), `focus-assist`
> (Do Not Disturb), `xbox-party-open`, `xbox-party-mute`.
>
> Not offered for new slices but still fully supported: types `script` (**must** stay, since `sequence`'s
> `script:` steps use the same path), `toggle`, `sequence`, `steam-mute`, `discord-mute`, `safe-mode`, and
> the `media-mute` command.

### Sequence (`type: "sequence"`)

Runs `steps` (an array of actions) in order. Each step is a normal action object and may carry
`"delayMs"`: milliseconds to wait *before* that step runs. At most 64 steps run per fire and a single
delay is capped at 60 seconds.

```jsonc
{
  "type": "sequence",
  "steps": [
    { "type": "toggle", "process": "DS4Windows" },
    { "type": "launch", "path": "C:\\Games\\game.exe", "delayMs": 500 },
    { "type": "keypress", "keys": "Win+D" }
  ]
}
```

In the Settings editor a sequence is written one step per line as `type: value` (the value is that
type's main field), with `wait: 500` lines inserting delays:

```
toggle: DS4Windows
wait: 500
launch: C:\Games\game.exe
keypress: Win+D
```

A step that carries more than its main field appends the rest after a `|` as `key=value` pairs separated
by `;`, for example `launch: C:\Games\game.exe | process=game.exe; quit=true` or
`system: volume-set | level=0.4`. The keys are `process` and `quit` (launch), `path` (toggle), `mic`
(switch-audio), `level`, `plan`, `planb` and `login` (system), `target` (obs), and `keys` and `text`
(text-chat). The value is `path` for `launch` and `script`; `process` for `toggle`; `command` for `system`,
`launcher`, `obs`, `text-chat` and `arcade`; `keys` for `keypress` and `steam-mute`; `url` for `url`,
`installed-game` and `discord-join`; and `device` for `switch-audio`. The remaining types (`exit-app`,
`discord-launch`, `discord-mute`, `discord-deafen`, `sequence`, `settings`, `game-browser`, `disable-wheels`,
`xbox-emulation`, `dualshock-emulation`, `safe-mode`) carry no payload, so they are a bare type name.

Nested sequences are ignored (no recursion).

## `system`

Global behaviour. Most keys have a Settings control; the ones that don't are marked "config only".

```jsonc
"system": {
  "fadeMs": 150,                // wheel fade/scale-in duration (ms), 0-2000; 0 = instant. Config only
  "driftPx": 150,               // how far the wheel starts offset toward centre before sliding in (px), 0-400. Config only
  "stickyMs": 150,              // grace window after the stick recentres where a release still fires (ms), 0-1000. Config only
  "sliceThickness": "medium",   // slice ring: "thick" | "medium" (default) | "thin" (outer edge fixed).
                                // "thick" only reads well up to 8 slices, so crossing that on EITHER wheel
                                // auto-switches to "medium" and sets thickAutoDemoted
  "thickAutoDemoted": false,    // true = Radiata (not the user) moved thickness off "thick" for slice
                                // count, so dropping both wheels back to 8 or fewer restores it. Any
                                // manual thickness pick clears this
  "sliceMaterial": "flat-dark", // wheel material: "flat-light" | "pearl" | "flat-dark" (default) | "obsidian"
                                //   | "kawaii" | "salvage" | "mesa" | "reactor". Matched case-insensitively.
                                //   Older spellings are accepted on load and rewritten on the next save:
                                //   flat / flat-white → flat-light, gloss-light → pearl,
                                //   gloss-dark / gloss-black-* → obsidian, frost-light / frost-dark / sparkle → kawaii,
                                //   stencil → salvage, terra / paper → mesa.
                                //   Also accepts "custom-<id>" for a drop-in theme package you have approved
                                //   (not available in public releases). Any other value, and a custom-<id>
                                //   that cannot be resolved (package missing, unapproved, or unsupported in
                                //   this build), draws as "pearl" (a fallback, not the default; an unknown token
                                //   never becomes flat-dark). An unresolved custom-<id> stays in the file, so
                                //   it applies again once the package is available
  "gameGridMaterial": "obsidian",   // Game Grid card material, same values as sliceMaterial. Not used for rendering:
                                    // the Game Grid always draws with sliceMaterial's own treatment. Settings
                                    // keeps this equal to sliceMaterial on every save
  "alwaysShowHub": false,       // true = always draw the centre hub, on every material. false = the hub appears only
                                // when it has something to show (a toggle-state readout, the volume
                                // scrubber, edit mode, or a notice). Settings ▸ Advanced ▸ Accessibility
  "showSliceLabels": "all-except-logos", // which slices draw their text label on the wheel. Settings ▸ Customize ▸ "Show labels on".
                                // The UI names differ from the stored tokens: "all-except-logos" (default, shown as
                                // "Icons, not Logos": built-in-glyph slices only; a game logo, cover art, or
                                // your own PNG all suppress the label) | "all" ("All Slices") | "none"
                                // ("No Slices") | "selected" ("Slices I Choose": each slice's own showLabel
                                // decides). Unknown values normalise back to the default on read
  "practiceWhileSettingsOpen": false, // false = wheels stay live while the Settings window is open. true = while it is
                                // open, wheels aim/arm but firing runs NOTHING (a "Practice" pill shows in the
                                // hub). No Settings control. The first-run wizard always practices regardless
  "reduceMotion": false,        // product-wide Reduce Motion policy (accessibility): stops decorative motion
                                // across the wheel AND the Game Grid: intro drift/scale, picker zoom, edit
                                // reflow travel, Reactor parallax/sparks/shake, Kawaii twinkles/confetti,
                                // Salvage sparks, material dwell effects (all fall back to the static confirm
                                // arc). Opacity fades remain. Effective policy = this OR the Windows "show
                                // animations" accessibility preference (either reduces). Settings ▸ Advanced ▸ Accessibility
  "narration": false,           // "Narrate wheel and Game Grid" (accessibility): Radiata speaks selections,
                                // guarded-hold stages, and action results through its own SAPI voice, because
                                // Windows Narrator cannot read the overlay's never-focused window. Settings ▸
                                // Advanced ▸ Accessibility. Default false, EXCEPT on a first run (oobeVersion 0)
                                // with Windows Narrator already running, which starts it true: that machine
                                // has someone who needs speech on it, often a sighted person setting the PC up
                                // for a vision-impaired user. A default, not a lock: the wizard's accessibility
                                // row and Settings both override it, and re-running the wizard never re-seeds it
  "narratorTipDismissed": false,// the ✕ on the Settings ▸ Accessibility tip pointing at Windows' own
                                // system-wide Narrator (Radiata's voice reaches the wheel and Game Grid
                                // only). Persists across restarts, but turning "narration" off and back
                                // on clears it, so a re-tick makes the advice relevant again.
  "narrationVolume": 80,        // speech volume 0-100 (SAPI's scale), independent of soundEffects. Config only
  "buttonGlyphs": "auto",       // on-screen button symbols (Settings ▸ Customize): "auto" (follow the pad) | "playstation" | "xbox".
                                // Auto can't tell a third-party pad in DS4-compatible mode from a real
                                // DualShock (it spoofs Sony's VID/PID), so the override is how you get Xbox
                                // glyphs there, the same trade-off Steam has
  "language": null,             // UI language (Settings ▸ Advanced). null/absent = follow Windows' display language when it is
                                // one this build offers (else "en"); "en" | "es" | "de" | "ja" | "ar" once picked. Read once at
                                // startup: Settings prompts a restart, the first-run wizard relaunches. Unknown codes fall back to "en"
  "swapFnButtons": false,       // swap which SIDE opens which wheel (applies to Fn and chord triggers)
  "triggerModes": {},           // per-controller-kind SET of invocation gesture tokens, e.g.
                                // {"DualSenseEdge":["fn","touchpad-swipe"]} (a single string instead of an array
                                // also reads). Settings ▸ Customize ▸ Triggers. Kinds and tokens: see below
  "triggerActivation": "hold",  // "hold" (release fires) | "toggle" (trigger opens; ✕ confirms, ○ cancels).
                                // A touchpad swipe always behaves toggle-style
  "wheelIgnoresOppositeStick": false, // Accessibility opt-OUT. False (the default) = EITHER stick aims and arms
                                // (the more-deflected one wins) under Hold, Toggle and edit alike, and either
                                // stick's click enters edit mode. True = each wheel is aimed only by its own
                                // stick. Ticking it guards the cancel guarantee on a pad with resting drift.
                                // Settings ▸ Advanced ▸ Accessibility; independent of triggerActivation
  "showTrayIcon": true,         // false: no tray icon; the Settings window becomes the taskbar app and closing it
                                // quits Radiata. Config only
  "dpadVolumeWhileOpen": true,  // d-pad up/down scrubs system volume while a wheel is open; false frees the d-pad. Config only
  "dpadVolumeRepeatDelayMs": 400,   // held d-pad volume: delay before auto-repeat kicks in (ms), 100-2000. Config only
  "dpadVolumeRepeatIntervalMs": 80, // held d-pad volume: interval between repeated steps (ms), 20-1000. Config only
  "dpadHorizontalMode": "switcher", // what d-pad left/right does while a wheel is open (Settings ▸ Customize):
                                // "switcher" (default: Alt-Tab forward/back; Alt is held until the wheel closes,
                                //   which commits the highlighted window) |
                                // "desktop" (Win+Ctrl+Left/Right virtual desktops) |
                                // "song" (prev/next track; flashes a big skip glyph in the hub) |
                                // "mic" (microphone level, hub-scrubbed under a mic glyph, auto-repeats) |
                                // "mixer" (app Volume Mixer balance, see mixBalance; not offered in the Settings
                                //   picker, still honoured if set by hand, and where an unrecognised value lands)
  "soundEffects": true,         // UI sounds on/off; with soundTheme = the Customize "Sound effects"
                                // tiles (Themed / Digital / Physical / Silent; Silent = this flag off, theme kept)
  "soundTheme": "material",     // sound set: "material" (default, the "Themed" tile: follow sliceMaterial's own set: kawaii/
                                //   mesa/salvage/reactor each ship one, pearl/obsidian resolve to digital,
                                //   the flats to physical) | "digital" | "physical" as explicit overrides.
                                // A material change writes this back to "material"; an explicit pick holds
                                // only until then. The stored tokens "kawaii"/"sparkle"/"tap"/"classic"
                                // are also accepted and resolve to their sets
  "captureSafeMode": false,     // Anticheat Passthru Mode (GLOBAL, manual): no virtual pad, no cloak, so a
                                // strict kernel-anticheat game sees neither an emulated device nor a hidden
                                // controller. The wheel still works; input bleeds through while it's open.
                                // Toggled from the tray or Settings ▸ Passthru Mode
  "safeModeApps": [],           // apps/games that engage passthru mode AUTOMATICALLY (Settings ▸ Passthru Mode).
                                // Each entry: { "path", "frontmostOnly": false, "matchFolder": false,
                                // "name": null }. path is a full .exe or, with matchFolder, a game's install
                                // DIRECTORY (any process inside it matches). frontmostOnly = engage only while
                                // its window is focused. Automation drives a RUNTIME flag and never writes
                                // captureSafeMode. Entries with a blank path are dropped on load. Empty = the
                                // watcher doesn't run at all
  "xInputCapture": true,        // full capture for XInput/Xbox-class pads: cloak the whole XUSB devnode
                                // tree (wired/dongle) or the pad's HID entry (Bluetooth, gated on the
                                // observed-isolation probe), + a virtual Xbox 360 stand-in. Config only.
                                // Set false to force best-effort mode permanently (the escape hatch if a pad misbehaves)
  "steamAutoRelaunch": true,    // Steam-first leak cure: when Steam was already running with the pad attached
                                // before Radiata started (its pre-cloak pad handle = double input in Steam Input
                                // games), silently relaunch Steam once at startup, only when nothing is in the
                                // foreground and no game is running. False = detect and alert but never touch Steam. Config only
  "bluetoothDropAlert": true,   // raise the "Controller Signal Lost" card when a Bluetooth link dies and
                                // in-app recovery is exhausted. False = the link still dies, silently.
                                // Settings ▸ Advanced ▸ Current Controller shows the checkbox only while a
                                // raw-HID pad (not an XInput Xbox pad, which can't raise the card) reports
                                // Bluetooth; the stored value is left alone either way
  "updateCheckEnabled": true,   // check getradiata.app/update for a newer version (~60 s after startup,
                                // then every 24 h). The check sends ONLY the app version: the ?v= query
                                // on the static latest.json fetch (see PRIVACY.md). Updates stay prompt-only:
                                // nothing downloads or installs without pressing Update Now. False = no
                                // automatic checks at all; Settings ▸ Advanced ▸ System ▸ "Check for
                                // Updates" still works on demand
  "updateSkippedVersion": "",   // feed version the user pressed "Skip This Version" on ("" = none).
                                // Silences the automatic prompt for exactly that version; cleared
                                // automatically when a still-newer version appears, and ignored by the
                                // manual Settings check. Written by the update prompt window, never by a
                                // Settings tab
  "crashPromptEnabled": true,   // offer to send a crash report on the first startup after a crash
                                // (the consent window shows the FULL report; nothing sends without
                                // pressing Send, see PRIVACY.md). False = no offer; pending-crash.txt is left
                                // in place, not sent. No Settings control: the consent window's Don't send
                                // with "Remember this choice" ticked sets it
  "crashAutoSend": false,       // send the pending crash report without showing the consent window. Set
                                // by the window's Send with "Remember this choice" ticked. Failure keeps
                                // pending-crash.txt for the next launch. Needs crashPromptEnabled true:
                                // prompting off = nothing is offered or sent. No Settings control
  "obsPort": 4455,              // obs-websocket port (OBS ▸ Tools ▸ WebSocket Server Settings), 1-65535. Written
                                // only by Settings ▸ Advanced ▸ Integrations ▸ Configure OBS Integration…
  "obsPassword": null,          // obs-websocket password, DPAPI-encrypted at rest (like the Discord
                                // credentials). Null/empty = OBS unconfigured
  "obsConfigured": false,       // the OBS connection has been saved. Gates the configure-on-fire flow:
                                // firing an OBS slice before setup opens its editor, which carries a
                                // one-time-setup button alongside the slice's own fields
  "editHintSeen": false,        // the post-onboarding "click the stick to edit" hub reminder has had its
                                // ~10 s discovery window. Re-armed to false by completing the wizard again
  "mixBalance": 50,             // Volume Mixer balance 0..100 for the "mixer" d-pad mode (50 = both sides full),
                                // persisted. (mixEnabled / mixDeviceA / mixDeviceB are retired: they still
                                // parse, nothing reads them)
  "steamGridDbKey": null,       // optional SteamGridDB API key → cover art for non-Steam games; null = off. Stored
                                // DPAPI-protected: a plain-text key written by hand is encrypted at the next
                                // startup, and the stored value only decrypts for the same Windows user on the
                                // same machine (re-enter it after restoring a backup elsewhere)
  "sgdbStyle": null,            // preferred SGDB grid style ("no_logo", "alternate", …); null = any
  "preferPlayniteCovers": false,// true: Playnite → SteamGridDB → Steam; false: SGDB → Playnite → Steam
  "disabledStorefronts": [],    // storefronts excluded from the Game Grid and the first-run starter wheels.
                                // Authored from the Game Grid: hold ☐ on a store's "Open <store>" launcher
                                // card. Entries are storefront display names ("Steam", "Epic", "itch.io"),
                                // not the lower-case `launcher` command keys ("steam"). Settings ▸ Advanced ▸
                                // Game Grid ▸ Reset Hidden Games clears it along with the hidden games
  "gameGridTipSeen": false,     // the one-time Game Grid art tip has been shown (set after first grid open)
  "knownControllerKinds": [],   // controller kinds already introduced to the user (DualSenseEdge, PlayStationOther,
                                // Xbox, ExtraButtonPad); a kind not listed shows a one-time notification when it
                                // first connects. On a fresh config the first connect silently adopts whatever
                                // is attached as the baseline
  "lastControllerKind": null,   // kind active at the last change; compared at startup so a pad swapped
                                // between runs is announced like a mid-session swap
  "oobeVersion": 0,             // onboarding version completed; 0 = never (first-run flow will run)
  "oobeStep": 0,                // 0-based step an UNFINISHED first-run flow was closed on; it resumes there
  "driversDeclined": false,     // the user explicitly chose "Skip Drivers" in onboarding: missing-driver
                                // isolation warnings stay quiet for the chosen state. Cleared at startup
                                // once both drivers are detected installed, so a later install re-arms them.
  "foreignViGEmBusNoticed": null // app-written: the ViGEmBus origin string (program + bus devnodes) the
                                // one-time "another program supplies this driver" notice was shown for.
                                // A different origin notices again. Not a setting.
}
```

`steamGridDbKey` is your own free key from steamgriddb.com (never bundled). With it set, game-browser
tiles for stores that have no built-in art source (Epic/GOG/Xbox/itch) fetch portrait cover art from
SteamGridDB; Steam already has art without a key. Edited on the **Advanced** tab.

### `triggerModes` kinds and tokens

Keys are controller kinds: `DualSenseEdge`, `PlayStationOther` (other PlayStation pads and DS4-compatible
pads), `Xbox` (XInput pads), `ExtraButtonPad` (Xbox-layout pads with dedicated extra buttons, L4/R4). A
kind with no entry, or an empty list, uses its default: `fn` on `DualSenseEdge`, `viewmenu-bumpers` on
the others. More than one token may be enabled at once.

| Token | Gesture |
|---|---|
| `fn` | The dedicated extra-button pair alone (Fn1/Fn2 on the DualSense Edge, L4/R4 on an extra-button pad) |
| `touchpad-swipe` | A finger entering a touchpad edge, moving inward (pads with a touchpad) |
| `bumpers-triggers` | L1+L2 or R1+R2 |
| `bumpers-home`, `triggers-home` | Bumper or trigger + Home (PS/Guide) |
| `viewmenu-bumpers`, `viewmenu-triggers` | Bumper or trigger + Back/Start (either) |
| `bumpers-stick`, `triggers-stick` | Bumper or trigger + L3/R3 |
| `bumpers-dpad`, `triggers-dpad` | Bumper or trigger + D-pad left/right |
| `viewmenu-stick` | Back/Start + L3/R3. Honoured when present, not offered in the builder |
| `extra-triggers`, `extra-bumpers` | L4/R4 + a shoulder on the opposite hand (L4 + R2, R4 + L1, …) |
| `extra-home`, `extra-stick`, `extra-select-start`, `extra-dpad` | L4/R4 + Home, L3/R3, Select/Start, or D-pad left/right |

### Validation on load

Out-of-range values are clamped and traced rather than rejected: `fadeMs` 0-2000, `driftPx` 0-400,
`stickyMs` 0-1000, `dpadVolumeRepeatDelayMs` 100-2000, `dpadVolumeRepeatIntervalMs` 20-1000,
`mixBalance` 0-100, `obsPort` 1-65535, `narrationVolume` 0-100. An unknown `language` becomes `"en"`,
`sliceMaterial` and `gameGridMaterial` are canonicalised (see above), and `showSliceLabels` falls back to its
default. Null entries in a wheel are dropped, a wheel is trimmed to its first 32 slices, a slice label is cut
to 200 characters, and `safeModeApps` entries with a blank `path` are dropped. The slice-thickness rule
(`sliceThickness` / `thickAutoDemoted`) is applied against the loaded slice counts. If the file can't be
parsed at all, it is copied aside, the last good copy is loaded when one exists (otherwise the defaults),
and nothing is overwritten until the next save.

## `actionColors`, `actionIcons`, and `customColors`

```jsonc
"actionColors": { "launch": "#23558C", "system:sleep": "#264C80" }, // per-type tint overrides ("#RRGGBB")
"actionIcons":  { "keypress": "KeyboardVariant" },                  // per-type default-glyph overrides
"customColors": [ "#23558C", "#166274" ]                            // saved swatches for the colour pickers
```

Keys: an action `type`, `launcher:<key>`, `system:<command>`, `category:<header>`, `group:<name>`, or
`launcher-menu` (the Add picker's Storefront node). An entry replaces the built-in default colour or glyph
for that key. No Settings control writes these two dictionaries: entries already in the file are applied
at startup and kept on every save. Per-slice overrides live on the slice as `iconColor` / `iconName`.

## Other files in `%APPDATA%\Radiata`

`hid_offsets.json` (setup-wizard HID calibration) · `cover-overrides.json` (per-game cover + logo picks) ·
`arcade-state.json` (Arcade saved games, high scores and preferences) ·
`discord-oauth.json` and `discord-token.json` (both DPAPI-encrypted) · `config.json.bak` (a copy of the last
`config.json` that parsed cleanly, loaded automatically at startup if the live file can't be parsed),
`config-corrupt-<timestamp>.json` (an unreadable `config.json` kept aside) and `backups\` (the
per-version snapshots described above), all three DPAPI-protected like the secrets, so they open only under
the same Windows account · the game-art cache (`artcache\`) · `radiata-trace.log` (timestamped, rotates at
~1 MB, the three previous logs are kept as `.1`-`.3`) · `pending-crash.txt` (a crash report awaiting the
opt-in send offer; deleted on Send-success or "Don't send") · `Packages\` and `packages-consent.json`
(drop-in packages and the record of the ones you approved; not available in public releases).

## Versioning

`<ReleaseMajor>.<ReleaseMinor>.<build.minor>.<YYMMDD>`. `ReleaseMajor` and `ReleaseMinor` are set in the project file;
`build.minor` is a counter in the tracked `build.minor` file that the build reads but never increments
(the maintainer's commit hook bumps it once per commit); `<YYMMDD>` is the build date. The app reports
this string as its version, and `savedByVersion` records it.
