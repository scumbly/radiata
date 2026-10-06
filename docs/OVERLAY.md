# Overlay, z-order, and wheel interaction

The overlay window, how a slice gets armed and fired, and the surfaces drawn on it. Code:
`Overlay/OverlayWindow.xaml.cs`, `Overlay/RadialMenuControl*.cs`, `Core/WheelStateMachine.cs`. The per-material
looks are in [MATERIALS.md](MATERIALS.md).

## Window model

`OverlayWindow` is a topmost, layered, non-activating, click-through WPF window. Input is read from the
controller, never from the window, so it never takes focus and the game keeps the foreground.

- `HWND_TOPMOST` is re-asserted on a 400 ms timer and on every show path.
- It sits reliably over the desktop, normal apps and borderless-windowed games. It is not reliable over true
  exclusive fullscreen, and it cannot cover UAC prompts or the lock screen.
- No WPF key event reaches it, so Esc is global while the Game Grid or the arcade is up: `App.RegisterBackKey`
  arms a `WH_KEYBOARD_LL` hook that routes Esc through `App.SurfaceBack`, the same path as the circle button.
  `RegisterHotKey` on Esc is the fallback, and the wheel's own Esc is `HkWheelBack`.
- Never inject into or hook a game's graphics API: kernel anticheat can read it as tampering.

## Reading direction

The overlay never flips for right-to-left languages. `OverlayWindow`, `RadialMenuControl`, `ArcadeControl`,
`ControllerButton` and `WheelMiniature` pin `FlowDirection = LeftToRight`, and `TestHarness loc` fails if one
stops. A slice's position is the physical stick direction and the aim math in `WheelStateMachine` knows
nothing about WPF flow direction, so a flipped drawing would arm the slice on the opposite side. Right-to-left languages shape
their text runs inside the drawing code; the geometry is never flipped. Keep every pin when fixing an RTL
layout.

## Display targeting

The overlay covers the primary display only (`CoverPrimaryScreen`, `NativeMethods.PrimaryScreenDips`), and
everything (wheels, Game Grid, toasts, edit scrim) draws there. The app is System-DPI-aware, which is exact on
the primary display. Multi-monitor setups should make the gaming display the Windows primary.

## Selection model

`WheelStateMachine` owns stick to armed slice, deadzone, sticky grace and confirm dwell.

- Tilt past the centre deadzone (`Deadzone` = 0.38, smoothed with an EMA of alpha 0.20) to arm the nearest
  slice; release to fire; release while centred is a cancel.
- Either stick aims, and the more-deflected one wins. `system.wheelIgnoresOppositeStick` confines aiming to
  the wheel's own side, so resting drift on the second stick cannot arm a slice unaided.
- `system.stickyMs` is the grace window after the stick recentres in which a release still fires.
- A guarded slice (`requireConfirm`) fires after an 800 ms dwell with a fill ring instead of on release.
- Hard cap of 12 slices per wheel (`SystemConfig.MaxSlicesPerWheel`); `ConfigLoader` trims a hand-edited file
  to 32. `SliceThicknessRule` moves both wheels from Thick to Medium past 8 slices and restores Thick when
  they shrink, on the config read path and on every wheel-write path.
- `SliceLabelRule` is the one place `system.showSliceLabels` is interpreted. The stored tokens
  (`all-except-logos`, `all`, `none`, `selected`) differ from the UI wording. Edit mode always shows labels.

## The hub

Toggle-like actions (mute, HDR) show live state in the centre hub. There is one momentary readout slot and one
shared 800 ms hide timer: `SetScrubber` owns the level ring and the large glyph, and setting either clears
the other. `system.alwaysShowHub` forces the hub on every material.

`ActionExecutor` returns state words (On, Off, Muted, ...) as English identity tokens; the hub, the status
toast and the announcer translate them at display time through `Loc.Readout`.

## D-pad and Select/Start while a wheel is open

- Up/down scrubs system volume (`system.dpadVolumeWhileOpen`).
- Left/right follows `system.dpadHorizontalMode`: `switcher` (Alt-Tab, Alt held until the wheel closes),
  `desktop` (Win+Ctrl+Left/Right), `song` (back/next track) or `mic` (microphone level).
- Start opens Settings. A press that belongs to a Select/Start summon chord goes to the chord:
  `TriggerInterpreter.SelectStartChordEngaged` is a momentary check, not a config-level one, so a plain
  Select or Start press works under every gesture configuration.

## Empty and single-surface wheels

An empty wheel acts as a disabled side: invoking it draws and captures nothing. Click the aiming stick while
invoking it to open the Add picker (`EnterEditFromSilent`). A wheel holding only the Arcade Launcher opens the
launcher directly (`ShowOverlay` calls `OpenArcade(null)`); any other lone slice still draws its wheel.

## Diagnostics

A summon that does nothing must be diagnosable from `%APPDATA%\Radiata\radiata-trace.log`.

- `[Trigger]` (`TriggerInterpreter`): the live gesture set on every `Configure`, and each rejection.
- `[Wheel]` (`App`): one line per successful open, plus the silent no-ops and the ways a release downgrades to
  a cancel. `[FIRE]` records the action's outcome as well as the attempt.

Keep these low-noise: each line is once per state change or throttled per distinct reason
(`App.TraceThrottled`, `TriggerInterpreter.TraceReject`).

## In-wheel edit mode

Controller-only. Click either stick while a wheel is open; the trigger can then be released and the wheel
stays up. Select a slice with either stick; the d-pad repositions it; the cross button picks up or places; hold
the square button to delete; L1/R1 undo and redo; the triangle button opens the Add picker; the circle button
or an Fn button saves and exits. Changes are saved as they are made. The Add picker always draws in Flat Dark,
whatever the user's material.
