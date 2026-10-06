# First-run onboarding

The first-run wizard: `Setup/OnboardingWindow.xaml(.cs)`, with `Setup/DriverSetup.cs` for the driver step. It
opens automatically while `App.NeedsOnboarding` is true and can be re-run from Settings or with `--oobe`.

## Steps

The steps are built in `OnboardingWindow` from `UiText` titles, in this order:

| Step | What it does |
| --- | --- |
| Welcome | Controller detection (live, including hot-plug) and both drivers on one page: detect, install or upgrade, then the elevated self-registration. Includes the language picker. |
| Test it out! | Live trigger practice on the real wheel. Next unlocks once every invocation option of the detected kind has been tried; with no controller there is nothing to compel. |
| Material | Live look pick; also sets `soundTheme` to `material`. |
| Assets | Cover-art sources (SteamGridDB key, Playnite). Placed before Customize so game slices created there get rich logos. |
| Customize | Starter wheels applied live, plus informational, click-to-add storefront cards. |
| Training wheels are off! | Good-to-know cards and Finish (`MarkOnboarded`). |

Every step except the last runs in preview mode (a "Preview" chip in the hub, no real firing). While the
wizard is open, standalone notices (`ShowRectToast`, `ShowSentryAlert`, `FireIsolationWarning`) are suppressed,
the last one without setting its per-reason latch, and a `Closed` handler re-runs capture so anything still
true fires when onboarding ends.

## Language

A pick in the Welcome step writes `system.language` and relaunches the app; `OobeStep` already names the
Welcome step, so the wizard returns to it in the new language. `Loc.Offered` decides which languages appear: a
language is listed only once its UI catalog is complete.

## Resuming

`SystemConfig.OobeVersion` below `App.OobeCurrentVersion` makes the wizard auto-open. Closing it early does not
mark it done: `SystemConfig.OobeStep` records the step landed on (`ShowStep` writes it and clamps it to the
current step count), and `MarkOnboarded` clears it, so a stored step only ever describes an unfinished run.
`RunOnboarding` resets it to 0 when the flow is already complete. While onboarding is needed, app launch, a
second launch and a tray click all open the wizard instead of Settings.

## Seeding rules

- On a fresh install the capability-based starter layout is written to the config at wizard start
  (`PreloadStarterWheelsAsync`), so practice shows real slices.
- A customized install is never overwritten, and a re-run never seeds a slice the wheel already has.
  Sameness is `Core/SliceIdentity.cs`, which keys each action on the payload its type actually reads
  (payload-free types are singletons keyed on type alone); comparing raw fields would miss slices that carry
  stale payload from an earlier type.
- Wheel writes go through `WriteWheels`, which runs `SliceThicknessRule` against the live wheels.
- New slices are written with `ShowLabel = false`, like every other creation path, and completion seeds
  `KnownControllerKinds` so an attached pad is not announced as new later.
- The Left wheel always carries Exit Current App with hold-to-confirm. The Right wheel carries one Arcade
  Launcher slice (a blank-command `arcade` slice, never individual games).
- In the Customize step a checklist row maps to a slice array, not one slice: every arcade slice on a wheel
  collapses into one "Arcade Launcher" row, so one tick adds or deletes the whole set.

## Drivers

Every controller kind gets the same status, driver recommendation and skip warning, because ViGEmBus and
HidHide are load-bearing for Xbox pads too ([INPUT-CAPTURE.md](INPUT-CAPTURE.md)). A ViGEmBus supplied by another
program shows a warning with the switch steps instead of a check mark, and the drivers column stays open.

## `App.OobeCurrentVersion`

Raising it re-runs first-run setup for every existing user, so it moves only on purpose, never as a side
effect of a release or a flow tweak.
