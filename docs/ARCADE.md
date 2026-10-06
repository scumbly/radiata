# Arcade

An Arcade slice opens a small game in a circular window with the wheel's own footprint, dismissed with the
circle button, with its state frozen between visits. Three games ship: **Kabloom** (deduction on a circular
floret grid; every board is solvable without guessing), **Connate** (fire number orbs into a gravity cluster;
equal values merge) and **Petalpop** (a paddle on every side of a polygon; pop the flower to its core).

Code: `Core/Arcade/` (pure simulations, `ArcadeCatalog`, `ArcadeStore`, tuning), `Arcade/` (the WPF side:
`ArcadeControl`, renderers, `ArcadeChrome`, `ArcadeSprites`, `ArcadeSfx`, `ArcadeMusic`) and the wiring in
`App.xaml.cs` (`OpenArcade`, `EvaluateArcadeGuard`). The set shares one frame, type scale and HUD vocabulary
(`Arcade/ArcadeChrome.cs`).

## Architecture

| Layer | Where | Rule |
| --- | --- | --- |
| Simulation | `Core/Arcade/` | Pure, deterministic, serializable. No WPF, no clock, no rendering, no audio. |
| Shell | `Arcade/` | Frame pump, input funnel, drawing. |
| Host | `App.xaml.cs` | `_arcadeOpen` guards every controller handler; `OpenArcade` and `CloseArcade`. |

- `_arcadeOpen` joins the overlay flag in capture, so the virtual pad is held neutral while a game is up.
- The circle button is checked before every other consumer and never reaches a game, so one button always
  leaves. The triangle button is also a host button (the how-to card), so `ArcadeInput.TriangleDown` and
  `TrianglePressed` are always false: do not bind a mechanic to it. The bumpers, Select and triggers do nothing
  while a game is up. START opens a pause menu over the stopped game (Resume, How to play, Reset, then rows
  the game adds through `IArcadeGame.PauseOptions`); Reset restarts the run, never the high score.
- A controller drop closes the arcade and persists it.
- No XAML and no resource dictionary anywhere in the arcade: a resource miss is a runtime crash on a surface
  that paints 60 times a second over someone's game. The `CompositionTarget.Rendering` hook is attached in
  `Open` and detached in `Close`, and a throw on the frame pump closes the arcade instead of reaching the
  dispatcher.
- The simulation steps at a fixed 120 Hz (`ArcadeTuning.StepSeconds`). A button edge is consumed by the first
  step of a frame only, a backlog past `MaxStepsPerFrame` is dropped rather than simulated, and the render draws
  the latest state without interpolation.

## The no-offer / no-rewrite contract

An arcade slice always loads and is never rewritten by an auto-save. A blank or unknown `command` opens the
picker, and every catalog entry resolves in the slice editor ([ACTIONS.md](ACTIONS.md)). The Arcade Launcher
option stays first in its group.

## It refuses to run un-isolated over a game

An arcade game holds the stick for minutes, so if a PC game owns the foreground and capture is not isolating the pad,
every input would also reach the game. The window then opens an explanation card instead of the game
(`App.EvaluateArcadeGuard` reads the existing isolation state; `Arcade.Guard` holds the copy and
`Arcade.ReasonFromNote` maps isolation notes to a cause, so those note strings are the coupling). The card
tracks isolation live. Passthru Mode gets its own copy and no override; for the involuntary causes (no drivers,
cloak failed, too many pads) holding the triangle button for about a second overrides it until the arcade closes.

## Where the window sits

A game's window has three positions: over the left wheel, the screen centre, or over the right wheel. The summon
chord's hold pair slides it (L1/R1, or L2/R2 when the hold is the triggers). The choice is one setting for every
game, stored in `arcade-state.json` (`ArcadeStore.SaveWindowPosition`). The launcher never moves. The window is
`GameDiscScale` (1.30) times the wheel's footprint, derived from `RadialMenuControl.OuterRadius`, and is clamped
to the primary display.

## Saved state

`%APPDATA%\Radiata\arcade-state.json` is its own file, not `config.json`: it changes on every dismiss and is worth
nothing if lost. It is written atomically on dismiss and at teardown, and every read path treats it as hostile
(a missing, truncated, mangled or newer-version file yields a fresh game, never a crash). Reopening a game in the
same process keeps the live object, with a short READY beat (`ResumeReadySeconds`) that renders without
simulating. A finished run saves a null snapshot with its high score, so a game never resumes into GAME OVER.

## The picker states

A blank-command Arcade slice opens the picker: a rotary carousel of cabinets (`ArcadePickerRenderer`), one per
`ArcadeCatalog.Games` entry. The stick's horizontal axis or the d-pad steps the ring one cabinet per flick with
hysteresis (`ArcadeCarouselNav`, pure Core), auto-repeats while held and wraps; the vertical axis does nothing.
The cross button plays and the circle button closes. Each cabinet's screen shows the player's last board
(`ArcadeShots`, written when a game freezes and never for a finished run), falling back to a bundled preview and
then a placeholder. The launcher resumes the surface it was last left on, and the cabinets are a back-out layer
under a launcher-opened game. A wheel holding only the Arcade Launcher opens it directly ([OVERLAY.md](OVERLAY.md)).

## Kabloom levels 11+

Levels 11 and up play pre-baked boards (`KabloomBakedBoards.Data.cs`, generated, never hand-edited): no-guess
boards all but vanish above about 20% cell occupancy on this crop at every bee capacity, so the generator cannot
find them at play time, and higher levels add bees per petal instead. At the first reveal
`KabloomBakedBoards.TryPick` picks a board on which the clicked cell is a certified start. The data is keyed to the
crop `KabloomGrid.Create` builds; after a crop change `TryPick` refuses the stale board and the game falls back
to the runtime generator.

## Rendering constraints

- **Hex colours in the arcade palettes are `#AARRGGBB`, alpha first.** `ColorConverter` reads an eight-digit
  literal that way, not as CSS `#RRGGBBAA`; a swapped literal parses to a plausible colour at the wrong opacity
  and never fails loudly. Prefer helpers that take explicit `(r, g, b, a)` bytes.
- **A brush that is a fill must not also be an ink.** Name the role, not the colour (`Failure` for a tile,
  `FailureInk` for text on a plate).
- **Overlaid UI is scaled, play is not.** Readouts, prompts and cards go through `ArcadeChrome.Ui(size)`
  (`HudTextScale`) and pictograms through `UiArt` (`HudArtScale`); a new HUD element takes one of them, never a
  bare fraction of the field.

## Replaceable sprite art

`ArcadeSprites` resolves named slots to PNGs one object at a time. Every slot is optional and the vector drawing
is the fallback, so a missing file is silent. `%APPDATA%\Radiata\arcade-sprites\<slot>.png` beats the packed
`Assets/arcade/sprites/sprites-finished/<slot>.png` and is re-read on every open. Animation is numbered files
(`<slot>-1.png` and up), played by `Frame`, `Blend` or `Once`. An idle animation runs off `ArcadeSprites.Time`;
an animation keyed to an event must read a clock the simulation owns and snapshots, or a frozen game stops
repainting the same frame. `ArcadeSprites.PackedDir` and the csproj `<Resource>` glob must name the same folder:
it is spliced into a `pack://` URI, so a mismatch is not a build error and every slot silently falls back to
vector.

## Editing the games' on-screen text

The code is the only source of truth. Every player-visible string is a `UiText.Arcade` constant
(`Core/UiText.cs`); the renderers, each game's `HowTo` card and pause options, and the host's pause menu,
confirm prompt, footers and READY read it through `Loc.T`, so one edit changes every screen and the translation
catalog at once. Game names stay literal. Keep how-to bullets short: the card must fit one disc without
scrolling (`TestHarness arcade` measures it). Never spell a button glyph out: use
`ControllerButtons.Text(PadButton.X)`, because a literal cross or circle names the wrong pad for Xbox-glyph
players; `TestHarness glyphs` scans the source and enforces it.

## Adding a game

1. A simulation in `Core/Arcade/Games/<Game>/` implementing `IArcadeGame`, with its feel constants in a
   `<Game>Tuning.cs` added to `ArcadeTuning.TuningTypes`.
2. A renderer in `Arcade/` implementing `IArcadeRenderer`, registered in `ArcadeRenderers.For`.
3. One entry in `ArcadeCatalog.Games` (id, which never changes once shipped, title, subtitle, glyph, and a tint
   from the shared swatch bases). That entry puts the game in the slice editor, the Add picker, the picker and
   the executor.
4. A `HowTo` card of about five bullets, sound cues through the game's `ArcadeSfx` family, and the Help topic.
5. Derived code gets a NOTICE next to the source and an entry in
   [../THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md).
6. Run `TestHarness arcade`.
