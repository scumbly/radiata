# Input capture and emulation

Radiata hides the physical controller from other programs with the HidHide filter driver and presents a
virtual pad through ViGEmBus, so the game underneath receives nothing while a wheel is open. Both drivers
are Nefarius products; their installers are bundled in `drivers\`. The logic lives in `Input/`
(`HidHideManager`, `GamepadEmulator`, `XboxDeviceTree`, `SteamSentry`), `Platform/RecoveryTask.cs`,
`Setup/DriverSetup.cs`, and `UpdateInputCaptureCore` in `App.xaml.cs`.

- The virtual pad is a DualShock 4 by default and an Xbox 360 pad in Xbox Mode. A captured Xbox-class pad
  always gets an Xbox 360 stand-in, so button prompts do not change.
- While any overlay (wheel, Game Grid, editor, arcade) is up, the virtual pad is held neutral.
- HidHide is what makes the isolation real: Steam and `Windows.Gaming.Input` read through Raw Input, which a
  file-handle lock cannot block. Radiata allow-lists its own exe so it keeps reading the physical pad.
- Without ViGEmBus there is no virtual pad and input passes through unchanged.

## Invariants

- **Cloak first.** The virtual pad comes up only after the cloak is confirmed:
  `wantPad = !SafeModeActive && !_safeModeOffPending && _controllerPresentForPad && (xinputXbox ? xboxCloaked : sonyCloakOk)`.
  A game must never see two pads.
- **Track outcome, not intent.** `HidHideManager.Hide` reports which ids were actually blocked and only
  confirmed ids are recorded in `_cloakedIds`. Every capture pass retries ids that are connected but not
  cloaked.
- **"No ids" is not "no pad".** An empty list can mean a pad that could not be resolved to a device node. Use
  `TryGetHidInstanceIds`; only `NoPhysicalPad` may skip the cloak.
- **Two or more physical Xbox pads: no cloak.** XInput hands out slots, not device identities, and only one
  pad is forwarded. The guard waits `ControllerCaptureContinuity.MultiPadConfirmMs` before un-cloaking an
  established capture, because a USB/Bluetooth switch lists both transports briefly. `PadCountChurnRule`
  latches best-effort mode after 3 guard trips inside 20 s.
- **The cloak is retained while the pad is absent** (`padGoneRetainCloak`), so a reconnecting pad is born
  cloaked.
- **Wheels disabled is routing-only.** Summons are ignored and input is forwarded through the standing
  virtual pad; the pad and the cloak do not churn, because games cannot survive a device change.
- **Passthru Mode** (`SystemConfig.CaptureSafeMode`, plus `SafeModeApps` for per-app automation) creates no
  pad and no cloak. Automation drives a runtime flag and never writes the manual setting.

## The cloak outlives the process

The virtual pad vanishes with the process; only the HidHide block list persists in the driver. Every exit
path must lift it: a clean quit un-cloaks, managed crashes run `CrashSafeUncloak`, a ride-along watchdog
(`Radiata.exe --watchdog <pid> <startTime>`) lifts it after a hard kill, and the next launch or the logon
task (`--logon`, `RecoveryTask`) lifts a cloak stranded by power loss. The cloak record is persisted before
the driver is written.

## HidHide traps

1. The allow-list is keyed by exe path. A cloaked pad with Radiata off the list yields zero HID reports.
   The elevated `--hidhide-whitelist` pass registers the exe; `DetectSelfLockout` reports the condition.
2. The HidHide control device is exclusive, and Radiata holds it only per call. While another tool (the
   HidHide configuration client, a stuck `HidHideCLI`) holds it, every cloak call throws; `Hide()` classifies
   that as `CloakFailure.Contended` and backs off 15 s. Match the typed exception, never message text.
3. A fresh HidHide install filters nothing until each device re-enumerates.
4. HidHide's global inverse mode is system-wide configuration; Radiata never switches it.

## The cloak blocks future opens only

HidHide consults its block list only when a file is opened. It never revokes a handle that is already open,
so a process that held the pad before the cloak keeps reading it until it closes the handle or the device
node restarts. A successful `Hide` therefore does not prove isolation, and "isolated" must not be inferred
from it. `SteamSentry` detects a Steam process that started before Radiata (with the pad present at launch
and the cloak confirmed) and drives the remediation; other likely holders are reported as "isolation
unverified". This is also why the wheels toggle never touches the cloak.

XInput has no durable per-process handle, so hiding the device node cuts an XInput game off immediately.

## Xbox / XInput pads

`XboxDeviceTree` walks the Xbox setup classes and returns each physical pad's whole subtree; a pad needs its
composite parent and children blocked. Bluetooth Xbox pads enumerate under HIDClass with an `IG_` marker and
Bluetooth ancestry and take their own path.

- **Virtual pads are told apart by PnP ancestry (`PnpAncestry`), never by VID/PID.** Radiata's Xbox 360
  stand-in has the same VID/PID as a Microsoft pad.
- **Bluetooth isolation is observed, not assumed.** After the block, `XboxBtIsolationProbe` runs
  `Radiata.ArcadeHost.exe --xinput-count` (a different image that is not allow-listed; exit code 10 plus the
  pad count) and the stand-in comes up only on an observed zero. `BtObservationGate` accepts that verdict
  only while the cloaked pad is present. The unverified hold ends after `BtObserveTimeoutMs` (15 s) in
  best-effort mode.
- `SystemConfig.XInputCapture` defaults to true; false forces best-effort mode.
- If cloaking cannot be confirmed, the pad is left uncloaked with no stand-in: the wheel works and input
  bleeds through, which is better than a blocked pad with nothing in its place.

## Stuck root

A wired Xbox pad's composite root normally grows its input children within about a second. A root that stays
childless is not a pad: `XboxStuckRootTracker` treats it as settling for 10 s and then as stuck. A stuck root
is not cloaked or counted, and a card asks the user to power-cycle the controller.

## Foreign ViGEmBus forks

Other programs ship their own ViGEmBus builds under the same service name and interface GUID, so presence
probes cannot tell them apart. Radiata detects and names a fork (`DriverStatus.ForeignViGEmBusName`; a file
version above the final Nefarius release also counts as foreign), never installs over it and never
uninstalls it: driver setup stops with a message naming the program, onboarding shows the switch steps instead
of a check mark, and uninstall reports the bus as left in place (`DriverSetup.UninstallOutcome.NoUninstaller`).
The pad classifier does not depend on the bus's shape.

## Diagnostics

`%APPDATA%\Radiata\radiata-trace.log` carries `[Capture]`, `[HidHide]`, `[Emu]`, `[Controller]` and
`[XboxTree]` lines. The `[Capture] ... virtual pad released` line names which `wantPad` condition failed;
keep it attributing the real cause.
