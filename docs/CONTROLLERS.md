# Controllers, raw HID, and triggers

**Read this when touching:** `ControllerProfile`, `ControllerReader`, `HidOffsets`, `XInputInterop`,
`TriggerInterpreter`, `Core/TriggerConfig.cs`, or the Triggers builder in Customize.
For the isolation/cloak side see [INPUT-CAPTURE.md](INPUT-CAPTURE.md); for the full user-facing gesture
list see the in-app Help, also published at getradiata.app/help.

## Reference hardware and what's verified

The **Sony DualSense Edge over Bluetooth** is the reference device: its two front **Fn buttons** are the
wheel triggers, read via **raw HID** — not XInput, not a virtual pad — so toggling middleware never kills
the menu's input.

Test hardware (as of Aug 2026): the reference **Edge**, a genuine **Microsoft Xbox Series pad**
(`045E:0B12` USB / `0B13` BT — the workhorse of the Aug 27–29 capture passes), an **Xbox Core pad**
(Aug 29 2026), a genuine wired **Xbox 360 pad** (`045E:028E`), a **GameSir**
Xbox-compatible pad (wired + 2.4 GHz dongle), and a **DualShock 4**.

## The `IController` seam is built, not pending

`ControllerKind` = `DualSenseEdge` | `PlayStationOther` | `Xbox` | `ExtraButtonPad`. Kind is a
**functional grouping** (trigger set + glyphs), never a verified identity. Four raw-HID profiles are
**actively auto-detected in priority order** (`ControllerProfile.All`); Xbox is a separate XInput backend
rather than a raw-HID profile. `ControllerProfile.Staged` is empty — enabling a future profile is a
one-line move into `All`. Profiles carry a `Style`: `Sony` (the shared Sony-family shape, `ParseUsb`) or
`HatButtons16` (generic DInput-style: hat nibble + 8-bit axes + a 16-bit LE button field,
`ParseHatButtons`).

| Profile | VID/PID | Trigger | Notes |
| --- | --- | --- | --- |
| DualSense Edge | 054C / 0DF2 | rear **Fn** buttons | Reference device; first in priority so an Edge alongside another Sony pad keeps the Fn experience. Layout cross-verified vs SDL `hidapi_ps5`. |
| DualSense (non-Edge) | 054C / 0CE6 | chord / touchpad swipe | Identical report layout to the Edge. PID doesn't collide with the virtual pad. |
| DualShock 4 | 054C / 05C4 (v1), 09CC (v2) | chord / touchpad swipe | Different layout (buttons a few bytes earlier, analog L2/R2 at 8/9). |
| 8BitDo Ultimate 2C (BT) | 2DC8 / 301B | **L4/R4** extra buttons | `ExtraButtonPad` reference device, `HatButtons16` style, **Bluetooth-only identity** (BLE, event-driven — the stall watchdog is disabled via `StreamsContinuously=false`). Full report map characterized on hardware Aug 31 2026 — see the profile's doc comment. **Battery comes from Windows, not the report:** the declared battery byte is never populated (0x00 measured across 1014 reports), so `ReportsBattery=false`; the level is read instead from the devnode property the Bluetooth stack maintains — see `BluetoothBattery`. Over USB/dongle the same pad is a plain XUSB device (2DC8/310A) with **no** extra-button bits → XInput backend, `Kind=Xbox`. ⚠ An onboard remap (L4/R4 + button + the square key) REPLACES the native bit — a remapped paddle stops summoning. |
| Xbox-class | XInput | chord | No collision-free button exists on the pad itself; extra-button pads escape this only in their raw-HID mode (see above). |

**L4/R4 are OFFERED, never claimed by default.** Unlike the Edge's Fn pair, L4/R4 are ordinary remappable
buttons players commonly bind in games, so `DefaultFor(ExtraButtonPad)` stays the standard bumper chord —
picking L4/R4 is one dropdown away. They can be a lone press (the `fn` token, handled by
App's own Fn handlers) or a CHORD half: the builder offers `extra-*` tokens pairing L4/R4 with the
standard second buttons, for players who bind L4/R4 in games and need a deliberate two-button summon.
They ride `TriggerInterpreter` like every other chord, with L4/R4 in the role the bumper/trigger plays:
the per-hand side button for `extra-triggers`/`extra-bumpers`/`extra-home`/`extra-select-start`, the
shared hold for the stick and D-pad ones (where the click or direction names the wheel).

⚠ **The shoulder pairings are opposite-hand** (L4+R1/R2, R4+L1/L2), unlike the Bumper family's same-hand
squeeze — one hand holds the paddle, the other taps. The paddle still names the side, so L4 + a right
shoulder is the left group and opens the RIGHT wheel through the usual cross. The dropdown labels stay
bare "Bumper"/"Trigger"; `TriggerModes.Describe` carries the opposite-hand detail in prose.

⚠ **While such a chord is selected the LONE press must go dead**, or it opens a wheel on the chord's
first half and the chord can never be performed. The wizard drops `fn` from its live practice set
(`ApplyLiveModes`) and `App._oobeFnLive` carries that to the Fn handlers, which sit outside the
interpreter and so can't read its mode list. `T_Triggers` guards the whole catalog — an offered token
with no `SidesFor` case can never open a wheel and only traces once.

**Virtual-pad exclusion is load-bearing:** DS4 v1's PID `05C4` is also Radiata's own ViGEm virtual DS4, so
`Detect` and `GetHidInstanceIds` skip virtual devices via `ShouldSkipHidDevice`. Without that, a real DS4 v1 and
our own emulated pad are indistinguishable by VID/PID.

An Xbox-class pad over **Bluetooth** that emulates a DualShock 4 (most third-party pads, including GameSir)
enumerates as HID and is detected as `PlayStationOther` — it takes the Sony path with no flag, at the cost
of PlayStation button prompts.

## Bluetooth is the fiddliest parsing in the project

- Over BT a Sony pad needs a **Feature Report read** to enable full reporting, or the Fn buttons are
  invisible: `0x05` on the DualSense family, `0x02` on DS4 (`BtEnableFeatureId`).
- ⚠ **The PC-side Bluetooth stack can zombie the HID link: the devnode stays "OK" and opens succeed, but
  `GetFeature` fails (with Win32 error 0 — failure with no error code) and ZERO input reports flow, in
  both directions, to any process.** Seen live Aug 6 2026 (multiple episodes, one 15+ min). Nothing
  in-process clears it: stream reopens don't, and neither does a full capture reset (virtual pad released,
  cloak cycled, device held closed 12 s — `ControllerReader`'s `BtDeadOpenLimit`/`BtEscalateHoldMs`
  escalation, kept because it bounds the *other* wedge classes and correctly forces the teardown).
  **Power-cycling the pad is NOT enough** — after a pad-side reconnect the 0x05 read succeeded once and
  input was still dead. The reliable manual fix is cycling the **Windows Bluetooth radio** (Action Center
  → Bluetooth off/on), which rebuilds the stack's side of the link. Diagnosed with a HidSharp probe
  independent of Radiata, so it's not our capture stack. Mitigation: when a
  SECOND escalation is reached with zero live reports, `ControllerReader.BtLinkDead` fires and the
  app raises a safety-tier **corner card**, "Controller Signal Lost — toggle Bluetooth off and on"
  (wired in `App.StartController`; a tray balloon until Aug 29 2026, an overlay pill before Aug 8 —
  the pill was obtrusive over a game and outlived the reconnect, and the pad being off is usually
  deliberate). `BtLinkDead` is edge-triggered, so the card fires once per dead episode and the
  recovery edge needs no UI. Only the raw-HID BT path can
  raise it, so it's always a Sony pad — "press the PS button" is safe wording. Automated devnode/radio
  restart was considered and declined for now (standing elevation).
- ⚠ **An Xbox pad over Bluetooth wedges the same way, and nothing surfaces it.** Seen Sep 28 2026 on
  Windows 10 (Xbox Series pad), after a run of USB↔Bluetooth cable swaps. The pad blinked (searching), but
  every Bluetooth devnode stayed Present/OK, and XInput kept reporting slot 0 connected with a frozen
  packet counter (`[XI] slot=0 frames=64/sec lastPkt=1`, unchanged through a deliberate stick wiggle).
  Its stale Bluetooth listing also kept the multi-pad guard seeing two pads past its confirmation window.
  Turning the Windows Bluetooth radio off and on cleared it, as for Sony: the removal counts as a real
  departure, and the pad reconnects fresh. `BtLinkDead` lives on the raw-HID path, so the XInput path
  raises no card; a frozen packet counter through a wiggle is the triage signal.
- BT uses a CRC'd report distinct from USB: DualSense = report `0x31`, 78 bytes, data offset +1;
  DS4 = report `0x11`, the standard 78 bytes **zero-padded to 547 by Windows**, CRC at 74, data offset +2.
  USB is a clean 64-byte `0x01` on both.
- Offsets live per profile in `ControllerProfile`; `HidOffsets` (`%APPDATA%\Radiata\hid_offsets.json`,
  written atomically) holds the setup wizard's calibration overrides. Its **defaults must stay the
  verified Edge layout** — a wrong default only ever looked fine because the wizard's file overrode it, and
  wiping app-data for OOBE testing exposed dead Fn buttons.
- **Battery / touch offsets were cross-verified** against SDL hidapi, DS5Dongle firmware, and Linux
  `hid-playstation`. The DS4 battery byte was **corrected 12 → 30** (the old value sat in struct padding,
  so the reported level was garbage): level 0–11 in the low nibble, bit `0x10` = cable powered. **An
  on-device DS4 battery/touch sanity check is still outstanding.**

## Invocation gestures (the chord builder)

Tokens live in `Core/TriggerConfig.cs` (`TriggerModes`) and are stored per controller kind in
`SystemConfig.TriggerModes`, e.g. `{"DualSenseEdge":["fn","touchpad-swipe"]}`. The legacy single-string
form still reads. The Settings ▸ Customize ▸ **Triggers** builder composes each row from two dropdowns —
`TriggerPrimary` (Fn / Bumper / Trigger / Touchpad) × `TriggerModifier` (None / Swipe / Trigger / Home /
Stick / Back-Start / D-Pad) — through `TriggerModes.Compose`.

| Token | Gesture |
| --- | --- |
| `fn` | Edge rear Fn1/Fn2 |
| `touchpad-swipe` | finger entering from a touchpad edge, inward |
| `bumpers-triggers` | L1+L2 / R1+R2 |
| `bumpers-home` / `triggers-home` | L1/R1 or L2/R2 + Home (PS/Guide) |
| `bumpers-stick` / `triggers-stick` | bumper/trigger + L3/R3 (the clicked stick names the side) |
| `viewmenu-bumpers` / `viewmenu-triggers` | bumper/trigger + Select or Start (either one; the bumper/trigger **hand** picks the side, like Fn) |
| `bumpers-dpad` / `triggers-dpad` | bumper/trigger + D-Pad ◀/▶ (the direction names the side) |
| `viewmenu-stick` | **legacy** (View/Menu + L3/R3) — still honoured at runtime, no longer offered |

**Which wheel opens:** in Hold (default) the hand you squeeze opens the **opposite** wheel — Left Fn =
**Right Wheel** — so the free hand aims. Direct-side chords (L3/R3, D-Pad) name the wheel outright.
"Swap wheel sides" (Customize) reverses every case.

**Activation:** `system.triggerActivation` = `hold` (release fires) | `toggle` (trigger opens; ✕ confirms,
○ cancels).

## Wheel-flip vs the enable/disable chord — enable/disable WINS

The flip grace lets a lone shoulder re-open the other wheel, which is
indistinguishable from the FIRST button of a both-sides enable/disable chord: pressing L1 to start "both
bumpers + Select" flip-opened a wheel, and the enable/disable chord's own 250 ms pressed-together grace
(`EnableDisableGraceMs`, anchored to the invoke) then swallowed the rest — so the chord stopped working at
all. Fix lives in `TriggerInterpreter._gestureFromFlip`.

**Both-Fn chord** (or the both-trigger chord on chord-pads) toggles the wheels enabled/disabled. F1/F2/F3
are deliberately **not** registered as global hotkeys — they stay free for games; use the tray for
test-open / enable-disable.

## Controller-kind change announcements

`App.AnnounceControllerKind` (called from the `Connected` handler) detects change at **kind** granularity —
device instance ids are unreliable across a BT⇄USB transport change, so a same-kind reconnect says nothing.

⚠ **The PID changes with the transport, not just the instance id** — measured Aug 28 2026 on one physical
Xbox Series pad: **`045E:0B12` over USB, `045E:0B13` over Bluetooth.** Two ids, one device. And a paired,
powered-on pad **re-joins over BT on its own within ~2 s of a USB unplug**, so *unplugged* is not
*disconnected*: to actually remove it, power the pad down (hold the Xbox button ~6 s) or turn off the PC's
BT radio. This bites any test that assumes pulling the cable ends the session — and it silently changes
which cloak path is in play, since BT Xbox pads take the HIDClass route
([INPUT-CAPTURE.md](INPUT-CAPTURE.md) ▸ Bluetooth). More pad-identity traps — VID/PID proving nothing about
whether a pad is physical, and Steam presenting pads of its own — are collected there under
*Pad identity — what you cannot infer*.

- A swap mid-session, or a pad different from last run (`SystemConfig.LastControllerKind`), fires an
  info-tier corner card naming the pad + its summon gesture (info tier: it never evicts a live safety
  warning, and is dropped if one holds the slot).
- A kind **never seen before** (`SystemConfig.KnownControllerKinds`) fires a **clickable** card —
  clicking it runs setup.
- The first connect on a fresh config silently adopts the attached pad as the baseline, so an existing pad
  is never announced as "new"; onboarding completion seeds the known set.

**Still to build** (the "lightweight prompt" ideal, planned): instead of the whole onboarding flow, open
just the trigger-practice step scoped to the new kind and offer the matching emulation-toggle starter slice
(Xbox Mode vs DualShock Mode). Groundwork is reusable as-is — per-kind `SystemConfig.TriggerModes`,
`TriggerModes.For(kind)`, and the practice step's live-apply / sample-wheel / wheel-invoked hooks.

## Disconnect invariant

A controller drop-out must reset **all** input state and neutralize the virtual pad, or the UI soft-locks.
This covers every modality (wheel, edit mode, Game Grid). Fixed Jun 2026.

## Couch login

The app can't type the lock-screen PIN (secure desktop). Rely on Windows auto-login + no-wake-password.
