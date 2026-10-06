# Controllers, raw HID, and triggers

How Radiata recognizes a controller, reads it, and turns button gestures into wheel summons. The cloak and
virtual-pad side is [INPUT-CAPTURE.md](INPUT-CAPTURE.md). Code: `Input/ControllerProfile.cs`,
`Input/ControllerReader.cs`, `Input/HidOffsets.cs`, `Input/XInputInterop.cs`, `Input/TriggerInterpreter.cs`,
`Core/IController.cs`, `Core/TriggerConfig.cs`.

## Profiles and kinds

`ControllerKind` (`DualSenseEdge`, `PlayStationOther`, `Xbox`, `ExtraButtonPad`) is a functional grouping
(trigger set and glyphs), not a verified identity. Sony and extra-button pads are read over raw HID through
`ControllerProfile.All`, in priority order; Xbox-class pads use the XInput backend.

| Profile | VID / PID | Summon trigger |
| --- | --- | --- |
| DualSense Edge | 054C / 0DF2 | The two rear Fn buttons |
| DualSense | 054C / 0CE6 | Chord or touchpad swipe |
| DualShock 4 | 054C / 05C4, 09CC | Chord or touchpad swipe |
| 8BitDo Ultimate 2C (Bluetooth) | 2DC8 / 301B | L4/R4 extra buttons |
| Xbox-class (XInput) | n/a | Chord |

The Edge is first in priority so an Edge next to another Sony pad keeps the Fn buttons. L4/R4 are offered as
triggers but never claimed by default: they are ordinary remappable buttons, so `TriggerModes.DefaultFor`
keeps the bumper chord (`viewmenu-bumpers`) for every kind except the Edge.

## Pad identity

- **VID/PID never proves a pad is physical.** DS4 v1 shares its PID `05C4` with Radiata's own virtual DS4, and
  the virtual Xbox 360 pad is byte-identical to a genuine one at `045E:028E` apart from its instance id.
  `ControllerProfile.ShouldSkipHidDevice` and `PnpAncestry` decide by device ancestry.
- A third-party pad in DS4-compatible mode presents Sony's own VID/PID and reads as a Sony pad, so
  user-facing text about the identified pad must hedge.
- Transport is read from ancestry too: a Bluetooth pad's device node has a `BTH...` ancestor, a wired pad's
  has a USB one.

## Bluetooth parsing

- A Sony pad needs a feature-report read over Bluetooth to enable full reporting, or the Fn buttons are
  invisible (`BtEnableFeatureId`: `0x05` on the DualSense family, `0x02` on the DualShock 4).
- Bluetooth reports carry a CRC and differ from USB: DualSense is report `0x31` (78 bytes, data offset +1),
  DualShock 4 is report `0x11` (78 bytes that Windows zero-pads to 547, CRC at `BtReportLength - 4`, data
  offset +2). USB is a plain 64-byte `0x01` report on both.
- Offsets live per profile. `HidOffsets` (`%APPDATA%\Radiata\hid_offsets.json`) holds the setup wizard's
  calibration overrides, so its defaults must stand alone as the verified Edge layout.
- A Bluetooth link can stay open with no input flowing while the device node still reports OK. After the
  reader exhausts in-app recovery, `ControllerReader.BtLinkDead` (edge-triggered, raw-HID path only) raises
  the "Controller Signal Lost" card, which tells the user to toggle Bluetooth off and on.

## Invocation gestures

Tokens live in `TriggerModes` and are stored per kind in `system.triggerModes`; the Settings builder composes a
row from a primary (Fn, Bumper, Trigger, Touchpad) and a modifier through `TriggerModes.Compose`. The token
list is in [../CONFIG.md](../CONFIG.md).

- In Hold activation the hand you squeeze opens the opposite wheel, so the free hand aims. Direct-side chords
  (L3/R3, D-pad) name the wheel outright. `system.swapFnButtons` reverses every case.
- `system.triggerActivation` is `hold` (release fires) or `toggle` (open, then confirm or cancel).
- The both-sides chord toggles the wheels enabled or disabled. Enable/disable outranks wheel-flipping: a flip
  from a lone shoulder press is exempt from the 250 ms together-press grace (`_gestureFromFlip` in
  `TriggerInterpreter`), or the first half of the chord would swallow the rest.

## Kind-change announcements

`App.AnnounceControllerKind` compares kinds, not device instances, so a same-kind reconnect says nothing. A
swap, or a different pad than last run (`SystemConfig.LastControllerKind`), shows an info card; a kind never
seen before (`KnownControllerKinds`) shows a clickable card that opens setup.

## Disconnect invariant

A controller drop must reset all input state and neutralize the virtual pad, for every modality (wheel, edit
mode, Game Grid, arcade), or the UI soft-locks.
