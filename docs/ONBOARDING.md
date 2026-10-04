# First-run onboarding (OOBE)

**Read this when touching:** `OnboardingWindow`, `DriverSetup`, starter-wheel seeding, or
`App.OobeCurrentVersion`.

**Status: BUILT and hardware-verified** (v1 end-to-end Jul 2 2026 on Windows 10 and a fresh Windows 11 install; v2 flow
Jul 15 2026). It auto-opens while `NeedsOnboarding`, and is re-runnable from Settings ▸ Advanced ▸ "Run
first-run setup" (or `--oobe`).

## The flow (six steps, `OnboardingWindow._steps`)

| Step title | What it does |
| --- | --- |
| **Welcome** | Controller detection (live + hot-plug) **and** both drivers on one page. The hardware-verified engine: detect → legacy cleanup (Legacinator for a legacy HidGuardian) → install/upgrade → elevated self-whitelist. "Why drivers" text sits behind a link into a shared full-width tip card. Next needs a pad; **Skip** lets a padless user pass, with a drivers-missing prompt on Next/Skip — and choosing **"Skip Drivers"** there is a **persisted opt-out**: it writes `SystemConfig.DriversDeclined`, which silences the missing-driver isolation warnings for the chosen state (cleared at startup once both drivers are detected installed, so a later install re-arms them). **When both drivers are installed and current, the whole drivers column collapses** and the controller box takes the full step, enlarged (`ApplyWelcomeLayout`; the column stays while an install runs or an update is offered — the button is the offer). A **situational controller caveat card** sits beside the pad status as a SIBLING card, not nested inside it (`PadSplit`, `WelcomeCaveat`, `ApplyWelcomeLayout`), whenever a caveat applies: DualSense (Edge or standard) caveats Adaptive Triggers + touchpad under a bold "DualSense Compatibility Note:" header, a DualShock 4 caveats the touchpad alone under "DualShock 4 Compatibility Note:" (`ControllerReader.HasAdaptiveTriggers` tells the two `PlayStationOther` pads apart), an ExtraButtonPad caveats L4/R4 (reusing the practice step's `O.PaddleCaveat` string) headed with its own model name; Xbox and no pad show none. No glyph — the header carries the model name instead. Whenever a caveat shows, `PadSplit` splits 1:1 in the padOnly layout (1:1.2 with the drivers column shown) with a 196px MinHeight in the padOnly layout; the caveat text is the Body tier there (13px / LineHeight 19, 16px header) and steps down to 11.5px / 16 (13px header) beside the drivers column; `PadSplit` also drops the enlarged centered presentation, in BOTH welcome layouts (drivers collapsed or shown); it updates live on hot-plug. **A pre-checked "Restart Steam" checkbox appears while steam.exe is running in this user session** (session-scoped check — another account's Steam never produces the offer): committed on leaving the step forward, it bounces Steam once the cloak is confirmed (`App.ArmUserSteamRestart`) so Steam's pre-cloak pad handle dies before the practice step needs the pad. |
| **Try It Out** | Live trigger practice, **on rails**: Next unlocks once every invocation option of the detected kind has been tried (no controller = nothing to compel). Every chord of that kind is live at once, with per-chord controller illustrations (an extra-button pad's L4/R4 chords each have their own `R4-L4+<half>` piece). Every `Controller*.png` bakes an English SIMPLE ... DISTINCT legend at the same 1362x881 coordinates; outside English `InitChordLegend`/`LayoutChordLegend` cover each word with a `UiWindowBg` rectangle and draw `UiText.Onboarding.LegendSimple`/`LegendDistinct` over it. Practises on the wheels pre-loaded by `PreloadStarterWheelsAsync`. |
| **Material** | Live look pick — **also writes `soundTheme` back to `"material"`** (follow the look; resolved via `Materials.SoundThemeFor` at play time). Moved directly after practice Jul 15 2026. **Pad navigation:** the d-pad moves the selection across the tiles as laid out on screen (Simple column of flat-light / flat-dark left of the Deluxe 3x2 grid pearl, mesa, kawaii / obsidian, salvage, reactor; clamped, no wrap) and applies it live; △ toggles the Accessibility panel (`ControllerDpad` / `ControllerTriangle`, routed from `App.ControllerInput.cs`). The d-pad does nothing on any other step, where it is an invocation chord. |
| **Assets** | Cover art (Option C: recommend both sources, SGDB key validated inline). Deliberately **before** Customize so add-game slices get rich SGDB logos; the art prefetch kicks off on leaving this step and warms under Customize. On the no-Playnite branch the intro is **rewritten per selection** (`SetSgdbIntro`) — the Playnite sentence only stands while the Playnite option is picked. The tile illustration also tracks the selection (`UpdateArtPanels`): `tile-logos-playnite.png` while "Playnite only" is picked, `tile-logo-null.png` for "No game art", `tile-logos.png` otherwise. The three options are **radio buttons** (`ArtOpt1..3`, read through `ArtSelectedIndex` 0/1/2; option 1 bold = Recommended; option 3 collapses when Playnite is present). The "Are you sure?" dialog here (and the Skip-drivers one on Welcome) is pad-answerable: while it is open (`_modalDialog`) ✕ = the proceed button, ○ = stay, and every other wizard pad input is swallowed. |
| **Customize** | Starter wheels applied **LIVE** + **informational, click-to-add storefront cards**. The Right wheel is seeded with a launcher for the single best-populated storefront, and the green "add your first game" box is the step's call to action. |
| **Roll Out** | Good-to-know cards + Finish (`MarkOnboarded`). The controller-specific caveat now lives on Welcome step 1, not here. The animated flower mark lives in the tray tip card's swatch: it blooms, the dark glyph fades in behind it, then a snapshot of it flies to the real tray icon, leaving the glyph behind (`TryHighlightTrayIcon`). A card that opens a screen in the app **underlines its bold lead-in** — that underline is the link affordance, so don't put one on a card without a click handler. |

Every step **except Roll Out** runs in preview mode (a "Preview" chip in the hub, no real firing).

**While the wizard is open, the whole standalone-notice class is suppressed**:
`ShowRectToast`, `ShowSentryAlert` and `FireIsolationWarning` all early-out — the last deliberately
*without* setting its per-reason latch — so a first-run user's opening minutes aren't a stack of
controller alarms competing with the wizard, which surfaces driver/capture state itself. The conditions
are never suppressed (tray text, traces, silent cures continue), and a `Closed` handler re-runs capture
so anything still true fires the moment onboarding ends.

## Language (Welcome step)

The Welcome step's header carries a language picker (`LanguageRow`, right of the title; the practice step's
Recommended|Custom toggle occupies that corner on its own step). Native names, with the language's flag
glyph beside the box — drawn geometry from `HelpEditorControl.BuildFlagGlyph`, pinned `LeftToRight` so a
right-to-left UI cannot mirror it; Arabic is represented by the Palestinian flag.

A pick writes `system.language` and **relaunches the app**; because `OobeStep` already names the Welcome
step, the wizard comes back on it in the new language. That is the one language change that is not
restart-*prompted* — the language is fixed for a run (`Loc.Init`), and a first-run user has nothing open to
lose. With no `language` key in config, startup follows Windows' display language when it is one the build
offers, else English (`Loc.Detect`).

**Which languages appear** is `Loc.Offered`: English always; another language only once its UI catalog is
complete, so a half-translated wizard is never offered. A Debug build lists every language so the relaunch
path can be exercised before translations exist.

## Seeding rules

- On a **fresh install**, the capability-gated starter layout is pre-loaded to the config in the background
  at OOBE start (`PreloadStarterWheelsAsync`), so the practice wheel shows real slices rather than shipped
  placeholders.
- **A customized install is never stomped.** A re-run merges rather than overwrites.
- **A re-run never seeds a slice the wheel already has.** Sameness is `Core/SliceIdentity.cs`, not
  field-for-field equality: it keys each action on the payload its TYPE actually reads, resolved the way
  `ActionExecutor` resolves it. That matters because slices carry residue — retyping a Sleep slice to Arcade
  leaves `Command="sleep"` behind, and since an unknown arcade id resolves to the Launcher, a raw
  type+command+url key read that slice as a different action and seeded a second Arcade Launcher next to it.
  Payload-free types (Settings, Game Grid, Exit App…) are singletons keyed on type alone; an unknown type
  falls back to every payload field, so a new type can only ever over-distinguish. The same identity guards
  the Customize step's game dropdown — picking a game the wheel already carries ticks that row instead of
  appending a twin. Two **different** installed games are not duplicates.
- Wheel writes go through `WriteWheels`, which runs `SliceThicknessRule` against the live wheels — a starter
  layout can push a wheel past the Thick limit.
- Onboarding completion **seeds `KnownControllerKinds`**, so an already-attached pad is never announced as
  "new" later.
- New slices are written with `ShowLabel = false`, like every other creation path.
- The Left wheel carries **Exit Current App** (`exit-app`, hold-to-confirm) unconditionally — closing the
  game you are in is the couch action with no keyboard-free alternative, so it is seeded rather than left to
  be found. It takes the same `RequireConfirm` a fresh `exit-app` slice gets from the Add picker.
- The Right wheel carries the **Arcade Launcher** (one `arcade` slice with a blank command, opening the
  cabinet picker — never individual games or a consented drop-in package), gated on `Arcade.Available`.

### The Customize step's checklist rows are not 1:1 with slices

A row's `Tag` is a slice **array**, read everywhere through `RowSlices`. Every arcade slice on a wheel
collapses into ONE **"Arcade Launcher"** row (`BuildRows`), so a single tick adds or removes the whole
set; the cap note counts slices, not rows. The starter wheel seeds exactly one arcade slice — the **Arcade
Launcher** (type `arcade`, blank command), which opens the cabinet picker — never individual games. On an
OOBE re-run the row pools the user's own arcade slices with the starter launcher they're missing, preferring
theirs, so a tick can't stomp a label or icon they set.

## Storefront opt-outs are NOT part of this flow

The include/exclude checkboxes left onboarding: the cards here are informational + click-to-add only.
Opt-outs are authored **from the Game Grid** (hold ☐ on a store's launcher card) — see
[GAME-LIBRARY.md](GAME-LIBRARY.md).

## Discord is not an onboarding step

Removed Jul 6 2026. Voice-channel credentials are set **on demand** from a Join/Leave Voice Channel slice —
its editor shows a "Configure Discord Integration" button (opening `DiscordSetupWindow`) until they exist,
and **firing** such a slice without them opens that same window straight from the wheel — or from
Settings ▸ Advanced ▸ Discord.

## Drivers are presented as an optional isolation tier

Kind-aware copy. Since Aug 2 2026 **every pad kind gets identical treatment** — same status, glyph, driver
recommendation, and skip warning — because ViGEm + HidHide are load-bearing for Xbox pads too. The earlier
XInput-specific copy ("shared input is a hard limit") was **corrected**: shared input is now a default-off
*choice*, not a platform limit. See [INPUT-CAPTURE.md](INPUT-CAPTURE.md).

**A foreign ViGEmBus gets ⚠, not ✅.** When the bus answering the probe is another program's fork (the
`ViGEmForeign` hook names it — HP OMEN Gaming Hub, Oculus), the ViGEmBus row shows the switch instructions
from Nefarius (disable that bus in Device Manager ▸ System devices, restart, Repair Drivers), the drivers
column does **not** collapse, and the engine's Install/Repair skips the ViGEmBus step rather than installing a
second bus. INPUT-CAPTURE.md ▸ *Foreign ViGEmBus forks* carries the why.

## `App.OobeCurrentVersion`

Gate: stored `SystemConfig.OobeVersion` < `OobeCurrentVersion` → the wizard auto-opens. Closing early doesn't
mark it done, so it comes back — and since Aug 10 2026 it comes back **where it was left**, and by **three**
routes rather than one:

| Route | Behaviour while `NeedsOnboarding` |
| --- | --- |
| App launch | Auto-opens (unchanged). |
| A second launch signalling the primary instance | Opens the wizard, not Settings (unchanged). |
| **Tray icon left-click** | Opens the wizard, not Settings. Settings on an un-onboarded install buries the very window the user just closed. |

`SystemConfig.OobeStep` is the resume point: `ShowStep` writes the landing index on every step change, and
`MarkOnboarded` clears it, so a stored value only ever describes an **incomplete** run. `ShowStep` clamps, so a
step index written by a build with more steps lands on the last one instead of out of range.

⚠ **`RunOnboarding` resets it to 0 when the flow is already complete.** A deliberate re-run ("Run first-run
setup") starts at the beginning; only an unfinished first run carries a resume point. Without that, abandoning
one re-run would drop the next one back into the middle of it.

**Bump it by one whenever `ReleaseMinor` bumps — and at no other time.** See
[BUILD-RELEASE.md](BUILD-RELEASE.md) ▸ the milestone ritual.

## Remaining polish (not blocking)

- Make the wizard **fully controller-driven** on the remaining steps (✕/○ already drive Next/Back, the Material
  step takes the d-pad and △, but Assets fields, Customize and Roll Out have no pad navigation), and draw real
  controller-button glyphs on the footer buttons instead of unicode.
- The "lightweight prompt" for a newly-seen controller kind — open just the scoped practice step rather than
  the whole flow. See [CONTROLLERS.md](CONTROLLERS.md).
