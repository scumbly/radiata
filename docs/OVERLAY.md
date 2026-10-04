# Overlay, z-order, and wheel interaction

**Read this when touching:** `OverlayWindow`, `RadialMenuControl`, `Core/WheelStateMachine.cs`, the hub
readout, in-wheel edit mode, or anything about where the overlay draws.
Material-specific rendering lives in [MATERIALS.md](MATERIALS.md).

## Window model

A WPF **topmost, layered, non-activating, click-through** window. Input is raw HID, so it never needs
focus — it paints over the running game without the game losing focus or minimising. `HWND_TOPMOST` is
re-asserted on a short timer (otherwise the last topmost window wins).

- **No WPF key event ever reaches it**, so keyboard access to the Game Grid and the Arcade is global by
  design: while either is up, `App.RegisterBackKey` arms a `WH_KEYBOARD_LL` hook that swallows **Esc** and runs
  `App.SurfaceBack` - the same routing as ○ (Arcade: card/menu, then game, then out; Grid: hide-confirm or
  edit-pick cancel, then close). `RegisterHotKey` on Esc stays as the fallback if Windows drops the hook. The
  wheel's own Esc is a plain `RegisterHotKey` (`HkWheelBack`). Nothing holds Esc once the surface closes.
- **Reliably on top of:** the desktop, normal apps, and **borderless-windowed** games.
- **NOT** reliably over true exclusive-fullscreen (it bypasses the compositor) → **run games
  borderless-windowed** (negligible perf cost on Win10).
- **Uncoverable by design** (Windows security): UAC elevation prompts and the lock/login screen — the
  login case is mooted by auto-login.
- **Do NOT pursue DirectX injection / hooking.** Kernel anticheat (Vanguard, EAC, BattlEye) can flag it as
  tampering and ban the account. Virtual-pad emulation is fine for Game Pass; flag it for competitive AC.

## Reading direction — the overlay never mirrors

`OverlayWindow`, `RadialMenuControl`, `ArcadeControl`, `ControllerButton` and `WheelMiniature` all pin
`FlowDirection = LeftToRight` in their own constructors (or the window's root), and `TestHarness loc`
fails if any of them stops. The reason is not taste: a slice's position **is** the physical stick
direction, and the stick→slice aim math lives in `Core/WheelStateMachine`, which knows nothing about WPF
mirroring. Under a right-to-left ancestor the drawing would flip while the aim did not — push right, arm the
slice that appears left — and the Xbox face letters would render backwards. A right-to-left language
shapes its **text runs** inside the drawing code instead; the geometry is never mirrored. The same rule
covers the Help illustrations (`HelpEditorControl.BuildFigure` pins its canvas) and the language-picker
flag. Don't "fix" an RTL layout by removing a pin.

## Display targeting — PRIMARY DISPLAY ONLY

Jul 16 2026, tester-driven. The overlay window covers exactly the primary display and everything — wheels,
Game Grid, toasts, edit scrim — draws there (`CoverPrimaryScreen`, `NativeMethods.PrimaryScreenDips`).
Follow-the-game-to-another-monitor was **removed** after a tester's mixed-DPI rig put the wheels
off-screen/bottom-centre and mis-scaled the grid. Two root causes, both dead by construction now:

1. The active-screen DIP math divided a secondary monitor's physical rect by the **primary's** scale.
2. A virtual-desktop-spanning window gets **one** DWM DPI assignment, so DWM could bitmap-stretch the
   whole overlay.

The app stays **System-DPI-aware**, which is exact on the primary by definition (system DPI == primary's
DPI), and primary-only also matches what Remote Play streams capture by default.

**Guidance for multi-monitor users: make the gaming display the Windows primary.** This retires most of the
mixed-DPI problem. PerMonitorV2 + physical-pixel positioning would only be needed if per-monitor
following ever returns — and don't flip the manifest alone, that regresses the single-TV path.

## Selection model

`Core/WheelStateMachine.cs` owns stick → armed / deadzone / sticky / confirm-dwell, plus the in-wheel edit
logic and animation state.

- **Tilt past the centre deadzone** to arm the nearest slice; **release the trigger** to fire; **release
  while centred = cancel** (the zero-thought escape).
- **Both sticks aim, in every activation style** — App merges them before feeding `UpdateStick`, and the
  more-deflected one wins. `system.wheelIgnoresOppositeStick` is the Accessibility opt-out that confines
  aiming (and the edit-mode stick click) to the wheel's own side, `App._aimWithRightStick`. It exists for
  the cancel guarantee: a worn pad's resting drift on the second stick can arm a slice unaided, which
  turns a centred release from a cancel into a fire. Arbitration is plain max-magnitude; if that flickers
  on hardware, the designed upgrade is **takeover-with-hysteresis** (the idle stick may steal aim only by
  crossing the deadzone while the active one is centred), **not** blending.
- `Deadzone` = **0.38** (widened twice, to make accidental arming harder). An EMA
  (alpha 0.20) damps single-frame spikes to ≤0.20, comfortably under the deadzone, while an intentional
  hold crosses it within ~2 frames (~16 ms @ 125 Hz).
- `system.stickyMs` is the grace window after the stick recentres where a release still fires.
- **Guarded slices** ("Hold to confirm") need a short dwell (~0.8 s, red fill ring) before firing rather
  than firing on release — defaulted on for `sleep` / `reboot` / `shutdown` / `logout` / `hibernate`. In
  edit mode a guarded slice arms like any other; the dwell only applies to real firing.

## Slice count and thickness

**Hard cap 12 slices per wheel, no user setting** (`SystemConfig.MaxSlicesPerWheel`; the 4/8/12 `maxSlices`
picker was removed Jul 31 2026 and old config values are ignored — `ConfigLoader` keeps a separate
paranoia cap of 32 on the read path). ~6–8 is still ideal for reliable angular selection.

The one thing the low caps really bought — keeping **Thick** legible — is now automatic.
`SliceThicknessRule` (Core, beside `SystemConfig`) demotes Thick→Medium when either wheel passes
`ThickSliceLimit` = **8**, and restores Thick when both drop back — unless the user has picked a thickness
themselves since (`SystemConfig.ThickAutoDemoted` records the demotion and is cleared by any manual pick).
`ThickAllowed` also hides the Thick tile in Customize when it couldn't apply.

It runs on **the config read path** (`ConfigLoader`, which covers every write this process didn't make)
**and** all three wheel-write paths — `SettingsWindow.Save`, `App.PersistWheel` (in-wheel editor), and
onboarding's `WriteWheels` — so the rule can't depend on which editor was used.

## Slice labels

Global setting with a per-slice escape hatch (Aug 3 2026). `SystemConfig.ShowSliceLabels` =
`all-except-logos` (default) / `all` / `none` / `selected`, interpreted in one place —
`SliceLabelRule` (Core). **The stored tokens are NOT the UI wording**: Settings ▸ Customize ▸ **Show labels
on** (under the D-Pad selector — `CustomizeEditorControl` owns `ShowSliceLabels`) offers *Icons, not
Logos / All Slices / No Slices / Slices I Choose*, in that token order. The display names were changed and
the tokens deliberately were not, so nothing on disk migrates — expect the mismatch.
The default is **glyph-only**: a slice is labelled only when it carries neither a `LogoPath` nor an
`IconPath`, so game logos, cover art, and user-picked PNGs are all unlabelled (widened from logos-only on
Aug 3 2026 — `SliceLabelRule.IsGlyphOnly`). Only `selected` consults a slice's own `WheelSlice.ShowLabel`, and **every
newly-created slice is written with `ShowLabel = false`** (onboarding starters, game picks, Settings
Add/drag-drop, in-wheel Add picker), so that mode starts from an unlabelled wheel. Unknown tokens normalise
back to the default on read.

**Edit mode is exempt:** non-logo slices always show labels while rearranging.

**A labelled LOGO slice shares the wedge.** `all` labels artwork slices too, and so does a ticked slice under
`selected` — the renderer's logo branch measures the label first, takes its band out of the logo's height
budget (floored at 45%, so a two-line name shrinks the artwork rather than erasing it), and centres the pair
on the slice centre. Only the EDIT-MODE force-on clause skips logo slices; the mode itself never does.

## The hub

Toggle-like actions (volume/mic mute, HDR) show live state in the centre hub when armed and after firing.

**One momentary readout slot, one shared 800 ms hide timer** — `SetScrubber` owns both, and setting either
form clears the other:

- The **scrubber**: a % ring + number, with a MaterialDesign glyph *under* the number naming which level —
  `VolumeHigh` for D-pad ▲▼ volume, `Microphone` for D-pad ◀▶ mic mode. (The old "MIC" word caption is
  gone.)
- A single **large glyph** (`RadialMenuControl.HubGlyph`) for something with no level to show — currently
  `SkipBackward` / `SkipForward` on a D-pad ◀▶ track skip.

Hub visibility is per-material by default; `SystemConfig.AlwaysShowHub` forces it on every material
(Settings ▸ Advanced ▸ Accessibility).

Otherwise the hub carries the **battery glyph**, anchored a fixed gap above the hub's bottom edge (so it
still reads as "near the bottom" on medium/thin, where the hub grows). Kawaii lifts it 10px further —
`KawaiiBatteryLiftPx` — because its cloud hub's scalloped lower edge doesn't follow the circle that gap is
measured from.

**The tilt dot is gone.** A faint ghost dot used to track the aiming stick inside an
otherwise-empty hub; Mesa dropped it Jul 31 2026 and every other material followed. `DrawStick` and its
"only over an empty hub" guard were deleted rather than gated — don't reintroduce it as a per-material
option. `WheelStateMachine.StickX/Y` are unaffected: they still drive arming, Reactor's parallax, and the
Game Grid.

### Readout text and the language

`ActionExecutor` returns its state words (`UiText.Status`: On / Off / Muted / Unmuted / No Device …) as
ENGLISH identity tokens — `Opposite` derives the "current › next" preview from them and the narration's
`StateClause` switches on them — and the three consumers translate at display: the hub preview and linger
(`ShowPreview` / `FadeRingHoldCenter`), the status toast, and the announcer, each through `Loc.Readout` (a
status word or an authored default translates; a game or app name passes through). Every announcer
sentence is one `UiText.Narration` string (`Loc.F`/`Loc.P`), with the spoken button names from
`UiText.Buttons`. The edit-mode legend words are resolved once per run into
`RadialMenuControl.Leg`, never looked up in a draw method; the hub notices ("Configure in Settings", the
"Click L3 to edit" hint, the Practice badge's subtitle) go through `UiText.Overlay`. The overlay's layout
never mirrors (above); only its text follows the language.

## D-pad while a wheel is open

- **▲▼** scrubs system volume (`system.dpadVolumeWhileOpen`, with auto-repeat delay/interval knobs).
- **◀▶** follows `SystemConfig.DpadHorizontalMode`: `switcher` (Alt-Tab forward/back, Alt held until the
  wheel closes so the highlighted window commits — the default) | `desktop` (Win+Ctrl+◀/▶) | `song`
  (prev/next track, flashes the big skip glyph) | `mic` (microphone level, auto-repeats).

**"Audio Mixer" (`mixer`) was pulled from the picker Aug 2 2026** — it didn't work on device. The plumbing
is deliberately intact (executor branch, `AppVolumeMixer`, the balance bar, the "Now mixing" readout, and
`mixer` is still the unknown-token fallback), so un-commenting the one `ComboBoxItem` in
`CustomizeEditorControl.xaml` is the whole fix if the behaviour is ever repaired — but **the pull is final and
repairing it is not planned**. Leave the code where it is; don't rip it out.

## Select / Start while a plain wheel is open

- **Select** steps the wheel material to the next one in `Materials.All` and persists it —
  `SliceMaterial` + `GameGridMaterial` + the paired `SoundTheme`, the same three-field write the Customize
  tiles do (`App.CycleSliceMaterial`). The open wheel is repainted and its glyphs **re-baked in the same
  beat**, because tints are baked into `WheelSlice.Icon` and the renderer's reload path early-returns while a
  wheel is open. ⚠ **Withheld from a public release** (`ReleaseGates.WheelMaterialCycle`): there Select on a
  plain wheel does nothing; Start is ungated.
- **Start** opens Settings.
- ⚠ **A press that belongs to a Select/Start summon chord goes to the chord, not to these.** The gate is
  MOMENTARY, not config-level (`TriggerInterpreter.SelectStartChordEngaged`): it holds only while a
  `viewmenu-*` gesture owns the open wheel or an enabled `viewmenu-*` mode's primary (bumper / trigger /
  stick click) is physically held. A plain Select/Start press works under **every** gesture configuration —
  the old config-level gate (`App.SelectStartIsSummonChord`, removed Aug 2026) killed both buttons outright
  whenever any `viewmenu-*` gesture was enabled; don't reintroduce it. Both reader backends raise
  Select/Start **after** every chord primary within a report so the gate reads settled state.
- They also defer to the surfaces that already own those buttons: the Game Grid's cover/logo cycling and an
  open arcade game.

## An EMPTY wheel acts as a disabled side

Invoking it draws nothing and captures nothing, so that side's gesture stays usable in-game. To bring it
back: invoke it and **click the aiming stick** — it opens straight into the Add picker
(`EnterEditFromSilent`). Toggle-style gestures have no "held" state, so they get a short post-invoke window
instead of the hold.

## A wheel holding ONLY the Arcade Launcher opens it outright

The launcher is itself a menu, so a single wedge in front of it is one selection with nothing to select
between: `ShowOverlay` runs `OpenArcade(null)` and never draws the wheel. The arcade still blooms where that
wheel would have been (`_lastWheelCx/Cy/Side` are set for it, since no wheel session sets them). Restricted
to the **Launcher** — `ArcadeCatalog.Resolve(command) is null`. A lone slice of any other kind, a named
arcade game included, still draws its wheel and keeps release-while-centred as the way to back out. Practice
mode is exempt: nothing on the wizard's wheel may run. ⚠ That wheel has no in-wheel edit route any more (no
wheel is drawn to click the stick on) — it is edited from Settings.

## Diagnostics — why a summon did nothing

Aug 9 2026. Suppression used to be **completely silent**: a wheel that declined to open logged nothing, so
"I did the gesture and nothing happened" was undiagnosable from `%APPDATA%\Radiata\radiata-trace.log`. The
whole decision chain now traces under two tags:

- **`[Trigger]`** (`TriggerInterpreter.cs`, previously zero logs) — the live gesture set on every
  `Configure`, and each rejection: wheels disabled, another surface Busy, an unknown/stale trigger token
  (logged **once per token** — that gesture can never fire), the enable/disable chord latching, and the
  chord swallowed by the together-press grace (the Aug 2 2026 "chord stopped working" shape).
- **`[Wheel]`** (`App.xaml.cs`) — one line per **successful open** (side, token, slice count, aim, style),
  which anchors everything else in the log; plus onboarding suppression, the empty-wheel silent no-op, Fn
  presses eaten by edit mode / the arcade / the picker / the Game Grid, and the two ways a release
  **downgrades to a cancel** (sticky index past the end after a reload shrank the wheel; an incomplete
  hold-to-confirm dwell).

`[FIRE]` now logs the action's **outcome** too, not just the attempt — a status-based failure
("Not Installed", "No Device") throws nothing, so it previously left no trace at all.

**Keep these low-noise.** They sit in per-press handlers, so everything above is either once-per-state-change
or throttled 30 s per distinct reason (`App.TraceThrottled`, `TriggerInterpreter.TraceReject`). Add new
input-rejection logging the same way — an unthrottled line on a held button becomes most of the log.

## In-wheel edit mode (V2 — shipped)

Controller-only. Click either stick while a wheel is open to enter; **the trigger may then be
released — the wheel stays on screen.**

- **Select:** either stick (the more-deflected one wins). **D-pad:** reposition the selected slice. **✕:** pick up / place.
  **☐ (hold ~500 ms):** delete. **L1/R1:** undo/redo. **△:** open the Add picker.
  **○ or an Fn button:** save + exit (changes are live-saved).
- **△ Add picker** is a category→type radial. Its first entry, **Installed Game**, opens the Game Grid in
  pick mode; ✕ picks and carries the game back to place. The picker **always renders Flat Dark** regardless of
  the user's material, and its glyphs bake untransformed dark tints — see
  [MATERIALS.md](MATERIALS.md).
- **Entry:** entering edit mode keeps the slice that was armed at the click selected until the stick moves to
  another slice.
- **Centre-latch:** the edit wheel keeps its armed slice when the stick recentres (the d-pad reorder shares
  a thumb with the aiming stick). The Add picker latches **only while Narration is on**; otherwise it arms
  like a live wheel — stick centred = nothing armed, so ✕ at centre does nothing.
- New slices auto-get the action type's default glyph + tint, so no glyph picker is needed on the couch.
- **Remaining polish:** an on-screen keyboard for renaming; inline value-pickers for Launcher / Cycle Audio
  / Set Volume (those still land as Settings placeholders).
