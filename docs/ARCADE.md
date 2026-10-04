# Arcade — little games in the round window

**Read this when touching:** `Core/Arcade/**`, `ArcadeControl.cs`, `Arcade/**`, or the `arcade` action type.

An **Arcade** slice opens a miniature game in a **circular window with the wheel's own footprint**, dismissed
with **○**, with state **frozen** between invocations. The use case is narrow and specific: the dead time a
couch player already spends on a loading bar or in a lobby queue, controller in hand, unwilling to alt-tab.

Two constraints define the whole thing:

- **Natively round.** Every game must use the disc in a way that has no rectangular equivalent. A rectangular
  concept cropped to a circle is a failed design, not a compromise.
- **One studio.** The set shares a frame, a type scale, HUD conventions and transitions
  (`Arcade/ArcadeChrome.cs`), so games differ by accent and motion language, not by re-deciding the furniture.

**Four games ship: Kabloom, Connate, Petalpop and Internode** — ⚠ **Internode only in dev and tester builds.**
A public release (`build-installer.ps1 -Public`) omits its `ArcadeCatalog` entry through
`Core\ReleaseGates.cs`, so the picker shows three cabinets, the Add picker and slice editor don't offer it, its
Help topic and every cross-link to it are dropped, and a slice that still names it opens the Launcher. Its code,
assets and strings stay compiled; `Arcade Launcher`'s sky (`InternodeSky`) and the shared `internode-newbest`
chime are used by the other games. The same gate class withholds drop-in script games
([PACKAGES.md](PACKAGES.md)). The first two were built independently against the same
`IArcadeGame`/`IArcadeRenderer` seam and folded into the catalog once approved;
Petalpop and Internode were built in-repo against the same seam (Sep 2026). Each has its own full design
specification, kept in the development repository only (not in the public source), and
this doc stays the FRAMEWORK's record rather than duplicating them. **The Well** and **Venturi** were both
deleted outright.

## Two things to know before anything else

### 1. One gate: the build const

**Arcade left beta Aug 26 2026** — the tray's **Arcade (Beta)** opt-in and its `SystemConfig.ArcadeEnabled`
key were removed, and Arcade is on out of the box. ⚠ **There is no Arcade-off build**:
every release ships the feature, so don't scope work around a release that hides it, and don't call
`Arcade.Enabled` a kill switch.

What the const still buys is a **testable unavailable posture**. Everything asks **`Arcade.Available`**,
never `Arcade.Enabled` directly, and the harness conceals through `Arcade.ConcealedForHarness` (a const
can't be flipped at runtime). Concealed, the feature is hidden **entirely** — no slice types offered, no
Add-picker category, no Help topic, no Settings section, no empty tab — while nothing is deleted and nothing
migrates in either direction. That contract is the point: it's what keeps an authored arcade slice loadable
and un-rewritten wherever the feature isn't offered.

| Surface | When not `Available` |
| --- | --- |
| `WheelEditorControl.Categories` | The Arcade group always lives under the **Radiata** category (there is no Arcade tab); when not `Available` the one group entry flips to `Hidden` — not offered anywhere, but its options still resolve per docs/ACTIONS.md's no-offer/no-rewrite contract. A **property**, cached against `Arcade.Available` — not a static field, because the harness flips availability at runtime. |
| The slice-editor tab strip | `SyncCategoryTabHeaders` **collapses** the surplus `TabItem`. Collapsed, not removed, because the gate goes both ways — a deleted tab couldn't come back. Index alignment holds because the placeholders are last and a collapsed tab can't be selected. |
| `HelpContent.Topics` | Every id in `HelpContent.ArcadeTopicIds` (the `arcade` topic and `custom-arcade-games`, the package-authoring one) is filtered out, so they leave the Help tab and its search index. Also a cached property. **A new Arcade topic must be added to that array.** |
| ~~Advanced ▸ Arcade~~ | **The section was REMOVED Aug 8 2026** — nothing to gate. It had emptied out: radial aiming left Aug 6 for Connate's pause menu, and the size dropdown was pinned to 100% and hidden Aug 8, leaving a heading that described the feature rather than configuring it. `SetArcadeVisible` and `HelpArcade_Click` went with it. Arcade Help keeps its own topic in Settings ▸ Help. |
| `ActionExecutor` `case "arcade"` | Wired; `App.OpenArcade` traces `slice fired while unavailable` and no-ops. (The callback is passed only when `Arcade.Enabled`; with the const false the executor traces `unavailable in this build` instead — an untaken path in every shipping release.) |

`Arcade.Enabled` is a code const and **not** a config key, on purpose — the *build's* answer must not appear in
a shipped `config.json` or in a backup.

⚠ **The hidden leaves are load-bearing, not tidiness.** A slice authored while Arcade was on must still *resolve* in `Categories`, or
`SelectTypeOption` finds nothing, drops to tab 0, and the next auto-save's `ReadEditorInto` **rewrites that
slice's type outright**. Same no-offer/no-rewrite contract as `sequence` and `script` — see
[ACTIONS.md](ACTIONS.md). `TestHarness.exe arcade` asserts exactly this, both ways round.

**Nothing toggles availability at runtime any more** — the opt-in's reaction plumbing
(`App.ApplyArcadeOptIn`, `SettingsWindow.RefreshArcadeGate`, `WheelEditorControl.RefreshTaxonomy`) was
removed with it. The caches still key off `Arcade.Available` because the harness flips
`Arcade.ConcealedForHarness` to prove they rebuild rather than latch.

⚠ **`HelpLocalization.Topics` caches per language and must invalidate when the topic SET changes** — it compares
`HelpContent.Topics` by reference. Without that, a gate flip left the localized Help tab missing the new topic
(caught by the harness, not by eye).

⚠ **The controls export (`Radiata.exe --export-controls`, written to `CONTROLS.md`; the same text is the in-app
Help, also published at getradiata.app/help) follows `Arcade.Enabled`, not `Arcade.Available`**
(`HelpContent.ExportTopics`). The generated doc is a generic build artifact — the same reason the export resolves chord tokens to generic
defaults — so the harness's runtime concealment must never reach it.

### 2. It refuses to run un-isolated over a game

An arcade session holds the stick for **minutes**, not the fraction of a second a wheel is up. If the physical
pad isn't cloaked with our virtual pad standing in, every one of those inputs also reaches the game
underneath.

**The rule:** a game owns the foreground (`DetectRunningGameName()`) **and** we're not isolated → the window
opens showing an explanation *instead of* the game. On the desktop it just plays; there's nothing to bleed
into. Copy lives in `Arcade.Guard`, so the card and the Help topic can't drift.

`App.EvaluateArcadeGuard` reads the **existing** isolation bookkeeping — `_isolated` / `_isolationNote`, set by
`UpdateInputCaptureCore` and already surfaced in the tray tooltip. `Arcade.ReasonFromNote` maps those note
strings to a cause; **the note strings are the coupling** — change one in `App` and change it here.

- **Live:** `NoteIsolationState` calls `RefreshArcadeGuard`, so isolation lost mid-session freezes the game
  behind the card and isolation regained lifts it.
- **Passthru Mode gets its own copy that does NOT nudge the user to turn it off — and NO override.** Passthru Mode is a feature working
  correctly — it exists so an anticheat never sees a virtual pad — and it's a *deliberate user choice*, so a
  card whose copy says "that's the safe choice" must not offer a bypass in the same breath. Carried on
  `GuardCopy.Overridable`, decided per-reason in `Arcade.Guard`.
- **Override: hold △ ~1 s**, session-scoped, never persisted (`ArcadeControl._overrideAccepted`) — offered for
  the *involuntary* reasons only (no drivers, cloak failed, too many pads). The user is the one who knows
  whether the foreground "game" is a paused single-player title where a leaked stick is harmless; without it
  the card is a dead end. A stored override does NOT carry into a Passthru Mode block that begins mid-session
  (`EvaluateArcadeGuard` consults `Overridable` before honouring it).

## Architecture

Follows the **Game Grid** precedent exactly — the third modal, controller-owned, non-wheel overlay surface.

| Layer | Where | Rule |
| --- | --- | --- |
| Sim | `Core/Arcade/` | Pure, deterministic, serializable. No WPF, no clock, no rendering, no audio. |
| Shell | `ArcadeControl.cs`, `Arcade/` | Frame pump, input funnel, drawing. |
| Host | `App.xaml.cs` | `_arcadeOpen` gates every controller handler; `OpenArcade` / `CloseArcade`. |

- `_arcadeOpen` joins `bool overlay` in `UpdateInputCaptureCore`, so the virtual pad is **held neutral** while
  a game is up. That's the same mechanism the wheel and grid use.
- **○ is checked before every other consumer** and never reaches a game (`ArcadeInput`'s remarks). That's what
  makes "one button always gets you out" true of every game in the set, present and future.
- **Input budget: one stick + ✕ / □** (and the d-pad — see below). The bumpers, Select and the triggers
  explicitly no-op while a game is up — the wheel's d-pad modes (volume, Alt-Tab, track skip) would otherwise
  fire under a game using the same hand, and a live summon chord could disable the wheels out from under an
  open game.
- **○ and △ are HOST buttons and never reach a game.** ○ dismisses; **△ opens the how-to card** (Aug 9 2026).
  ⚠ `ArcadeInput.TriangleDown` / `TrianglePressed` therefore read **false always** — they are kept on the
  record only because every probe constructs it positionally. **Do not bind a new game's mechanic to △**:
  it would silently never fire.
- **Disconnect invariant:** a controller drop closes the arcade (and persists). Leaving a surface up that
  swallows ○ with no pad to press it is exactly the soft-lock the invariant exists to prevent.
- **Zero cost when closed:** the `CompositionTarget.Rendering` hook is attached in `Open` and detached in
  `Close`, never left ticking. (Cautionary tale: the removed `MenuB`'s 30 ms timer ran for the life of the
  process for a control that was never rendered.)
- **No XAML, no resource dictionary** anywhere in the arcade. A runtime resource miss is this repo's
  most-repeated crash class, and this surface paints 60 times a second over someone's game.
- **A throw on the frame pump closes the arcade** rather than reaching the dispatcher and taking the app down.

### Fixed timestep

Sim steps at a fixed 120 Hz (`ArcadeTuning.StepSeconds`), accumulated from real frame time. Determinism is
what makes a tuning number mean the same thing on every machine.

- **Button edges are consumed by the first step of a frame only** — otherwise one tap registers on every step
  in a backlog (up to 12 shots).
- **A backlog past `MaxStepsPerFrame` is dropped, not simulated.** After the overlay is starved (a game
  finishing a shader compile), catching up honestly fast-forwards the player through enemies they never saw.
- **Cues are read once per rendered frame** (`Kabloom.TakeCues`), so several steps coalesce into one sound. At
  120 Hz the alternative pushes a stack of identical samples into the mixer's 16-voice cap, after which every
  later sound is silently dropped — see [SOUND.md](SOUND.md).
- **The render is NOT interpolated** (a deviation from the original plan): it draws the latest state. At 120 Hz
  vs a 60 Hz display that's ≤8 ms stale, which isn't visible, and interpolation would mean keeping and blending
  a previous state for every entity. Revisit only if a game needs sub-frame smoothness.

### Geometry and sizing

- Base diameter is `RadialMenuControl.OuterRadius * 2` — **derived from the wheel, not its own number**, so the
  "wheel dissolves, game blooms in its place" conceit can't drift. That base is the **LAUNCHER's** disc; a
  **game's disc is `ArcadeTuning.GameDiscScale` (1.30) times it.** The control is laid out at the game's size
  (`OverlayWindow.ArcadeDiameterFor`; `ArcadeLauncherDiameterFor` is that over the scale, so the ratio holds
  when the screen cap bites) and draws its disc at a factor that eases between the two: up over
  `LaunchSeconds` as a cabinet's screen grows into its game, down over `DiscShrinkSeconds` when ○ returns to the cabinets — during which the board just
  left shrinks into the front cabinet's screen and fades onto the shot already there
  (`ArcadePickerRenderer.DrawArrival`, drawn after `Draw` so it lands on that frame's screen). The wheel hub behind it is
  re-sized from the same value every frame (`ArcadeControl.DiscRadiusChanged` →
  `RadialMenuControl.SetArcadeHubRadius`), and `App.OpenArcade` grows it to whichever surface comes up first.
- **Overlaid UI is scaled up, play is not.** Every readout, shout, prompt, card and the launcher's own text
  goes through `ArcadeChrome.Ui(size)` (× `ArcadeTuning.HudTextScale`, 1.75) and the pictograms beside them
  — Connate's bomb meter and spares, Kabloom's nectar gem, Petalpop's life pearls, Internode's HUD token, the
  bezel lives — through `ArcadeChrome.UiArt(size)` (× `HudArtScale`, 2.0). Nothing in play is touched:
  petals, tiles, balls, paddles, runners, cores, bombs in flight keep their sizes. **Nor is a shout the
  player is still playing under**: Connate's COMBO and ×n merge counts, every Petalpop shout, every Internode
  shout keep their own size and sit at 0.50–0.55 of the radius above centre, off the pieces being played.
  Only a shout over an empty field (Connate's BOARD CLEAR) takes the HUD scale. ⚠ The how-to card has its
  own `HowToTextScale` (1.15): five illustrated bullets must fit one disc with no scrolling, and
  `TestHarness arcade` ▸ HowToFits (now measured at a 338 px game disc) is the gate — Connate's card is the
  tightest and sets the number. A new HUD element takes `Ui`/`UiArt`, never a bare fraction of the field.
- **Button prompts wear a badge.** A string of the form `"<button>  <verb>"` (a face button's text, two
  spaces, the verb — the form every prompt is already written in) is recognised by `ArcadeChrome.TryPrompt`,
  and `DrawCentered` / `DrawCenteredRow` set the button on a disc contrasting the ink, ringed in the ink,
  with the verb after it (`DrawPrompt`). Kabloom's plaques and the bezel how-to hint draw their own plates
  and call `PromptSize` / `DrawPrompt` inside them. Tokens inside how-to sentences (`{cross}` mid-line) get
  the same badge INLINE: `ReserveBadges` swaps each token for a no-break run of blanks wide enough for the
  badge (so it can never straddle a wrap), the line lays out as usual, and `DrawInlineBadges` draws the badge
  over wherever the wrap put that run (`FormattedText.BuildHighlightGeometry`). The badge is taller than the
  line and overlaps its neighbours' line boxes; the line height is never grown for it.
- The arcade window's size is a fixed 100% of the wheel's own footprint — `OverlayWindow.ArcadeScalePercent`, not a
  config key. `SystemConfig.ArcadeScale` is REMOVED; an old config carrying it still loads (unknown
  properties are ignored) and the next save drops it. Restoring a user-facing size choice means adding a new
  key and a new Settings ▸ Advanced control, not un-hiding anything.
- Clamped to the primary display's short edge, and the centre is clamped so a wheel near a screen edge still
  yields a fully on-screen disc.
- `App._lastWheelCx/Cy` is captured in `InvokeWheel` because the wheel is already dismissed by the time a fired
  slice opens the arcade.

### Where the window sits

A GAME's round window has three positions — centred on the left wheel (¼ of the way across the primary
display), the screen centre, or the right wheel (¾) — and the player moves it with the summon chord's HOLD
pair: LB/RB when the hold is the bumpers, LT/RT when it is the triggers (`App.HoldIsTrigger` reads the token
that opened the wheel), and LB/RB for any other primary. Left moves left, right moves right, no wrap at the
ends. The choice is ONE setting for every game, kept in `arcade-state.json` (`ArcadeStore.SaveWindowPosition`;
−1 until chosen). Until the player has chosen, a game opens over the wheel that launched it — or over the
wheel that opened the launcher it was picked from (`App._lastWheelSide`).

**The Arcade Launcher never moves**: it always sits over the wheel that fired it, and the bumpers do nothing
on the cabinets (`ArcadeControl.IsPicker`). Its own footer — `✕ Play  ○ Close` — sits on a plaque of the same
style as the START plaque described below, so the row doesn't land bare on whichever cabinet has rolled to
the front.

The move is **advertised on the bezel**: a small plaque either side of the START plaque, running out from
behind it, carrying an arrowhead and the shoulder's label — L1/R1, LB/RB on the Xbox glyph set, or L2/R2
(LT/RT) when the chord's hold is the trigger pair (`ArcadeControl.DrawSlideCues`, fed `WindowPosition` /
`SlideOnTriggers` by the host). They are **level and top-aligned** with the plaque, not rotated onto the ring:
the bezel falls away fast enough at that width that a plaque following the curve loses its outer corners off
the bottom of the disc. A side clamped at an end **darkens** — plate, rim, arrow and chip together — rather
than disappearing, so the pair never shifts. The arrowheads are drawn geometry, not Unicode: the embedded face
doesn't cover them.

Every move is a slide — `OverlayWindow.SlideArcade` animates the disc's canvas position with a cubic ease in
and out over `App.ArcadeSlideMs` (260 ms), carrying the grown hub with it while the collapse holds; Reduce
Motion snaps. The transitions travel too: picking a cabinet slides from the launcher's spot to the game's on
the launch clock (`ArcadePickerTuning.LaunchSeconds`), ○ back to the cabinets slides home on
`ArcadeTuning.DiscShrinkSeconds` (`ArcadeControl.SurfaceChanging` tells the shell which way), and a game
slice whose position differs from its wheel blooms at the wheel and slides across as the disc grows.

### Freeze / resume

- **In-process:** re-opening the same game keeps the live object. Nothing is reloaded.
- **Ready beat:** `ArcadeTuning.ResumeReadySeconds` (0.375 s) rendered-but-not-simulated on resume, so you
  don't die to an enemy that was one pixel away when you left — long enough to read the board, short enough
  not to feel like a stall on a re-open you make constantly.
  The scrim and the word **fade out over the tail** of it — `ArcadeTuning.ResumeReadyFadeFrac` (0.75), so the
  beat holds at full for its first quarter and fades across the rest. `ArcadeChrome.DrawReady` owns the ramp;
  before this it ignored `secondsLeft` entirely and the scrim popped off at zero.
- **On disk:** `%APPDATA%\Radiata\arcade-state.json` — **its own file, not `config.json`.** Config is user
  settings (hand-edited, backed up, version-snapshotted); game state churns on every dismiss and is worth
  nothing if lost. Atomic write (unique temp + single `File.Move`), same as `ConfigLoader`.
- **Written on dismiss and at teardown, not on a timer.** Dismissing *is* the checkpoint and is rate-limited by
  a human pressing ○. A hard kill mid-game loses that session's progress — accepted.
- **Every read path treats the file as hostile.** A missing, truncated, hand-mangled, NaN-carrying or
  newer-version file yields a fresh game — never a crash, never a prompt. A **future** version's file is
  ignored rather than deleted, so downgrading for one session doesn't destroy progress.
- A finished run saves a **null snapshot with its high score**, so the next open starts clean instead of
  re-showing GAME OVER.
- **Never resume into a game-over screen.** `ArcadeControl.RestartIfFinished` calls
  `IArcadeGame.Restart()` whenever it makes a finished game live again. The disk path above already handled
  this; what it missed is the **in-process** case — a game dismissed while dead keeps its live object for the
  rest of the session and used to come back still dead. Kabloom's `IsGameOver` is permanently false (a failed
  level is a level to retry, not a run to discard), so the host never calls its `Restart()`; the method still
  zeroes the diamond score and the per-level entry scores for a fresh campaign, it is simply unreachable from here.
- **Dismissing an outcome screen acknowledges it.** `ArcadeControl.SettleOutcome` runs before the cabinet
  shot and the snapshot on every freeze (dismiss, ○ back to the cabinets from a launcher-opened game, and
  the game swap in `EnsureGame`): a dead run
  `Restart()`s, and a game whose "game over" is a step in a continuing run takes that step via
  `IArcadeGame.AcknowledgeOutcome()` — Kabloom falls back / advances / reopens its last field with no
  transition and its cues cleared, Internode lands its demotion. So the shot and the resume are always the
  next playable board, never STUNG or a runner mid-fall. Default no-op for games with no such screen.

### Tuning

Every feel constant is a named mutable field in `ArcadeTuning` — no magic numbers in a sim. Optionally, a
**dev-only** `%APPDATA%\Radiata\arcade-tuning.json` overrides them by name (reflection over the public static
double/int fields), turning a playtest round from "edit, rebuild, relaunch" into "edit the file, re-open the
wheel". **Absent by default, never written, every failure swallowed.**

Two details the "edit and re-open" promise depends on, both harness-covered (`TestHarness.exe arcade`):

- **Re-read on EVERY open**, from `ArcadeControl.Open` — *not* from a game's constructor. Resuming a game keeps
  its live object, so a constructor hook meant the file was read once per process and every later edit needed a
  full restart.
- **Each load restores the compiled defaults first**, so the file is the whole truth and a key you *delete*
  goes back to the built-in value instead of lingering. A partial file is therefore the normal case — list only
  what you're changing.

### Sound

Through `SfxEngine` like everything else — nothing in the app gets its own audio path. Arcade deliberately does
**not** follow the wheel material or sound theme. **Every game has its own sample bank and the
host chrome shares one** — `ArcadeSfx` (shell), with family gains, ladders and throttles in
`Core\Arcade\ArcadeSfxTuning` (live via `arcade-tuning.json`). The full design — banks, take rotation, jitter,
pitch ladders driven by sim facts, layered big moments, the ingest pipeline and the **licence boundary** (the
WAVs are purchased and never mirrored) — is in [SOUND.md](SOUND.md) ▸ Arcade. Gains are held low on purpose:
the games play over someone else's audio.

**Music is separate and switchable.** Each game can play a looping bed (`ArcadeMusic`), **on by default** and
one choice for the whole arcade; the switch is a **MUSIC** row the HOST appends to that game's pause menu,
flipping a single top-level flag in `ArcadeStore`. Beds STREAM through a music channel on the same bus (a track is ~90 MB
decoded), never count against the voice cap, hold the device open while playing, and duck under any menu,
card, prompt or the READY beat. A game with no track is offered no row. Per-game cycling, the streaming
contract and the licence posture are in [SOUND.md](SOUND.md) ▸ Arcade music.

## The games

| Id | Title | One line | Sim |
| --- | --- | --- | --- |
| `kabloom` | Kabloom | Minesweeper deduction on a circular floret grid; every board provably guess-free | `Games/Kabloom/` |
| `connate` | Connate | Fire number orbs into a centre-gravity cluster; 1+2=3, like values double | `Games/Connate/` |
| `petalpop` | Petalpop | A paddle on every side of a polygon's inward-bowed rails, one stick moves them all; pop the flower of petals to its gold core; gems split the ball, ✕ smashes it; square → octagon, 30 levels, 8-8 wins | `Games/PetalPop/` |
| `internode` | Internode | Half-pipe runner seen from behind: fixed speed, the stick swings the runner around the pipe under pendulum gravity, ✕ jumps (hold for the full arc, tap for a small hop); collect tokens, dodge mines, make each gate's quota; the course bends and corkscrews | `Games/Internode/` |

### Two games have been removed

**The Well was RETIRED Aug 6 2026** — one idea shared with a better tunnel — and later deleted
outright: `Games/Well.cs`, `WellRenderer.cs`, its `ArcadeRenderers.For` mapping, and every host reference to it
are gone. Recoverable from git history if it is ever wanted back.

**Venturi was DELETED Aug 8 2026** — sim, renderer, palette, its three tool projects and its design specification.
It had been parked as `Hidden` since Aug 6. Recoverable from git history if it is ever wanted back.

⚠ **Deleting a catalog Entry does NOT breach the no-offer/no-rewrite contract** in
[ACTIONS.md](ACTIONS.md). That contract protects a slice's **TYPE** leaf (`arcade`), which still exists and
still resolves; a game id lives in `Command`, a free-text payload nothing rewrites. So an existing
`arcade`/`well` or `arcade`/`venturi` slice keeps its type and resolves to the **picker** — `Resolve` returns
null for an unknown id and the host opens the menu — rather than dead-ending. That was chosen
over a migration for the Well, and the same reasoning carried to Venturi.

Integration notes for the adopted games:

- They came in already conforming to `IArcadeGame` / `IArcadeRenderer`, built against this doc's seam in a
  standalone harness ("Arcade Lab") that ran them outside Radiata. Registration was one `ArcadeCatalog` entry
  + one `ArcadeRenderers.For` case each. **The lab was retired Aug 6 2026** as no longer useful —
  the games are in the app now, and their headless probes cover the contracts it was checking by eye. Its
  `StandaloneRenderer` / `XInputPad` / `SonyHidPad` lived in the Venturi standalone project and were
  deleted with it Aug 8 2026 — nothing else had ever used them.
- **Kabloom's `IsGameOver` is deliberately always false** — it's a persistent campaign, so the host always
  freezes it rather than clearing the snapshot after a "finished run".
- **Kabloom's floret geometry derives from Simon Tatham's Puzzle Collection (MIT)** —
  `Games/Kabloom/NOTICE.md`, mirrored in the MIT appendix of [THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md).
  Keep both if the file moves.
- **Cue → sound mapping lives in `ArcadeControl.PlayGameCues`**, one `Play<Game>` method per game, landing on
  that game's `ArcadeSfx` bank. Cues group into FAMILIES where the heaviest speaks (a game over is not also a
  life lost). A sim may expose read-only FACTS beside a cue (cascade size, merge rank, combo, catch streak) for
  the host to pitch on; it never knows audio exists. Deliberately silent: `Kabloom.Focus`, `Connate.Move` (per frame while
  steering — a metronome), `Internode.Land`, `Internode.Miss` and `Internode.OverTopLand`. Drop-in script games keep the nine-name generic
  vocabulary (`ArcadeSfx.Generic`).
- **The input budget is stick + ✕/□ + the D-PAD** (the d-pad was added Aug 6 2026,
  reversing the original stick-only rule — Kabloom's grid cursor wanted it; △ left the budget Aug 9 2026 for
  the how-to card). It reaches a game only as EDGES: `ArcadeControl.SetDPad` converts the controller's raw
  nibble to per-direction presses, so a game never reimplements press-detection, and auto-repeat stays each
  game's own business. The wheel's d-pad modes remain suppressed while a game is up, which is what makes
  forwarding safe.

## Connate C3–C10

- **C3 —** centre-cluster rotation doubled, `ClockwiseSwirlPerSec2` 1.16 → 2.32. The heap's spin is emergent
  (a perpendicular *acceleration* proportional to radius), so the settled rate roughly doubles but the outer
  bodies gain most. Inherent to the model, not a bug.
- **C4 —** bomb immunity lasts until the next tile is fired **or** `BombImmunityMinimumSeconds` (2.5), whichever
  is **longer** — hence `SizeLimitImmune || _immunityFloorLeft > 0`. It used to end the instant the follow-up
  shot left the rim, i.e. a fraction of a second, so the relief a bomb exists to give barely happened.
- **C5 — blended tiles.** A tile worth `BlendThresholdValue` (256) or more is automatically `HueBlend`, which
  matches any tile of the same number regardless of family.
  ⚠ **Connate's ladder is 1, 2, then 3·2ⁿ — 3, 6, 12, 24, 48, 96, 192, 384 — so 256 is not on it.** The
  threshold is stored as a *value* and resolved to the first rank that meets it (**384**), so the intent
  survives a ladder change instead of a hard-coded rank silently pointing elsewhere.
- **C6 —** cross-family **1 + 2** is the one legal cross-family merge and produces a **blended 3**. Blend is
  recessive against colour: blended + coloured adopts the colour, blended + blended stays blended. This gives
  blended tiles an early earnable source rather than only appearing at the threshold. Drawn as a slowly
  turning yin-yang of both family fills — it genuinely *is* both, and a third colour would read as a third
  family.
- **C7/C8 — bombs are earned, not scheduled.** The shot-count cadence is gone. A shot causing more than one
  merge is a **combo**, and combos pay charge on a **doubling curve** — a ×2 pays 1, a ×3 pays 2, a ×4 pays 4,
  a ×5 pays 8 — against a cost of `BombChargePerBomb` (**8**; it has been 5, 10 and 5) **plus
  `BombChargePerStage` (1) per stage index** — `Connate.BombChargeCost`, 8 at stage 1 up to 13 at stage 6. A
  stage change moves the line, never the charge already earned; the meter re-slices to the live cost.
  A full charge displaces the loaded tile and gifts a bomb; the displaced tile is handed straight back after
  the bomb fires.
  ⚠ **Which combo depth tips the meter is derived, not fixed.** Because the awards double, raising the cost
  moves the crossing point — ConnateProbe (development repository only, not in the public source) accumulates until the running total crosses the constant rather than
  asserting "the ×4 does it", which is exactly the assertion the 5 → 8 change broke.
  The gift is **automatic** — making the push-back a button needs a spare one, and a reward you must
  remember to collect mid-cascade is one most players never
  see. Nothing is lost by it happening for you.
  ⚠ **The schedule's state outlived the schedule.** `TurnsUntilBomb`, `_nextBombTurn`, `DrawBombInterval` and
  `BombInterval{Minimum,Maximum}` survived C7 with no live reader — the property existed only so the
  snapshot's `Turns`/`NextBomb` fields had one, and those fields existed only to feed the property. All of it
  went **Aug 9 2026** (snapshot 7 → 8), along with `Restore`'s `Turns < 0` check and the `_turnsFired` counter
  behind it. Worth knowing as a shape rather than an incident: retiring a mechanic leaves *serialized* state
  that a round-trip keeps alive, and it reads as load-bearing because something genuinely does reference it.
- **C9 — the score is on screen during play**, at 12 o'clock in the annulus between the clump limit and the
  craft's orbit, the only band nothing permanently occupies. A bombed tile throws motes that accelerate into
  the counter, and the counter climbs as they land. Connate deliberately hid its score until the round ended;
  the combo economy is what makes showing it work, since a combo without a visible reward is invisible.

### Connate — the charged launch, and the craft snapping back

- **A tap LOBS, a held release SHOOTS.** Launch speed ramps from `TapLaunchFraction` (0.18) of `LaunchSpeed`
  at zero charge to the whole of it at `FullChargeSeconds` (0.5), read off `Connate.SinceHold` on the release
  edge. This **replaces** the original design's "no charge timing or power meter"; the Connate design
  specification (development repository only) is updated to match.
  - ⚠ **Gravity puts a floor under the effect and no tuning gets past it.** Gravity is a centre *spring*
    (`GravityPerSec2` × radius) with `LinearDragPerSec` opposing, so an orb launched at almost nothing still
    arrives near the heap under its own fall. Measured at r = 0.40, a typical heap edge: a tap arrives at
    **1.63**, a half-charge at 2.10, a full charge at **3.02** — so a tap lands with about **29%** of a full
    shot's kinetic energy, and dropping `TapLaunchFraction` below ~0.15 buys nothing. Anyone who wants a
    tap floatier than that is asking about the gravity term, not this one.
  - **A DEADLINE shot is not a release.** The shot clock spending the piece launches at full force —
    `Fire()`'s parameter defaults to infinity for exactly that. Reading a forced shot as a zero-charge tap
    would quietly nerf every piece the player let expire.
  - **Bombs ignore the ramp.** A bomb's value is entirely in *where* it lands; one lobbed short is a wasted
    earn, not a soft touch.
  - **A whole tap reported in one frame charges nothing.** `_sinceHold` only starts on a down edge, so such a
    tap has no clock of its own and must not inherit the last real hold's number.
- **Releasing snaps the craft straight to its rest frame.** `ConnateCraftShot` is now `[2]`, the rest pose
  alone. Played back up as 1 → 2 → 3, the middle frame read as the craft *re-cocking itself* after a shot it
  had already taken.
  ⚠ That array's length used to double as the grips' let-go window. It is now
  `ArcadeSprites.Slot.ConnateGripReleaseSeconds` (0.125) — **don't derive one from the other again**; the
  grips still need a beat visibly ajar or the shot reads as passing through a closed clamp.
- **The launcher is a BOW.** Holding ✕ pulls the craft, its payload and their clamps
  OUTWARD together; releasing snaps the assembly forward through rest to one short overshoot and settles.
  `ConnateRenderer.CraftDraw` is the single source, read by all three the way `CraftBreath` is — they are one
  assembly, and a term that moves one has to move all three or the grip comes apart.
  - **The draw depth IS the charge.** It tracks `FullChargeSeconds`, not the wind-up sprite's 0.125 s, so full
    draw and full launch speed arrive together. **This is the only tell for how charged a shot is** — the
    wound frame says "charging" and nothing said "charged". Don't retime it to the sprite frames.
  - Measured: +0.045 world units out at full draw, crossing rest ~0.09 s after release, overshooting to
    −0.0115 at ~0.20 s, exactly 0 by `CraftSpringSeconds` 0.28. One push, no ringing — an oscillation reads as
    a loose mount rather than a string. A tap moves it ±0.002, i.e. visibly not a draw.
  - ⚠ **The depth is modest because the round window is a HARD clip** (`ArcadeControl`). A deeper draw eats
    the craft's tail against the bezel. `CraftDrawDepth` / `CraftSpringDepth` are the two knobs.
  - ⚠ **The spring reads `Connate.ShotDrawTension`, never the hold clock.** `SinceHold` merely freezes at its
    last value, so a DEADLINE shot would spring from a full draw the craft was never at and jerk the assembly
    outward on the frame the clock spent the piece. The sim records what the bow was actually at — 0 for any
    forced shot — and that property exists for exactly this.
  - A release the sim REFUSES (cooldown, blocked muzzle) still snaps back in one frame: nothing fired, so
    `SinceFire` is old and indistinguishable from long-settled. Matches the craft sprite, which snaps back
    with no shot too. Animating it needs a refusal clock the sim does not keep.
  - **A quick tap moves the bow NOT AT ALL.** `CraftDrawTapDeadzone` 0.18 — a hold under
    ~90 ms — is a hard gate on both the draw and the spring, not a small pull: the couple of pixels a stab
    used to buy read as the craft twitching, and the release then sprang off it. Above the gate the draw ramps
    from zero across what is left, so the threshold has no step in it.
  - **The shot clock WINDS THE LAUNCHER for you.** `Connate.AutoWindCharge` runs 0→1
    across the clock's last `FullChargeSeconds`, ✕ untouched, so an automatic shot takes as long to wind as a
    manual full draw and leaves on a bow genuinely at full tension. The craft's wound SPRITE follows the same
    term — a hull at rest beside a visibly drawn launcher reads as a bug — fed the wind-up's own elapsed time
    so the frames step at their authored rate instead of stretching over the clock.
    ⚠ `Fire` takes `max(player charge, AutoWindCharge)` as the tension, read BEFORE it resets the clock. This
    **reverses** the note above: a deadline shot is no longer undrawn, it is fully drawn, and springs.
    Zero while a bomb is loaded for free, since a bomb is off the shot clock entirely.
- **The loaded piece sits `PayloadForward` further along the craft** and does **not** breathe.
  ⚠ `PayloadForward` (0.0152, 4 DIP on the 100% disc) is **world units, not pixels**. On that disc the
  playfield radius 267 DIP less the 5.5% bezel is 252.3 px, and `WorldToFieldScale` 1.04 makes one world
  unit 262.4 px there. Re-derive from that chain rather than measuring on whatever window is open — a pixel constant would
  drift with disc size and DPI. The piece's own 2.5 Hz squash is gone (it read as alive rather than clamped,
  and fought the craft's rock); squash stays a *parameter* because the heap still uses it for merge
  anticipation and jelly.
- **The loaded piece turns WITH the craft.** Same `CraftFacing` the hull and grips read, blend swirl included.
  It used to be pinned upright on its own slow clock, which read as a piece idling in a holder rather than one
  clamped into a launcher that is itself turning. The numeral is unaffected — `DrawOrb` draws labels outside
  the rotation, so it stays upright wherever the craft points.

### Connate's colour rules

- **The ground is blue-grey and stays that way.** `ConnatePalette.Well` (`#32364D`) is the safe zone inside
  the limit ring; the outfield beyond it is near-black (`Outfield`).
  ⚠ **A GREEN safe-zone disc was built and rejected.** The idea was that hue alone would
  answer "am I inside the ring"; the board reads better with hue reserved for the tiles, and the limit ring
  plus the outfield's darkness already carry the boundary. Don't re-propose it. The green disc also forced
  `GravityLine` up to 60% alpha — that went back to 40% with the disc.
- **The tile families are BLUE and RED** — `AzureFamily` and `EmberFamily`, keyed by `ConnateRules.HueAzure` /
  `HueEmber`. The blue ladder's deep end stays *saturated* on purpose: the safe zone and the field beyond it
  are both blue-**greys**, so a washed-out rank-8 blue sinks into them. That's the pairing to check first if
  these values move.
- **Gold is reserved** — see the payout below.

### The payout — the game's biggest moment

Collecting is the **only** way Connate scores, so the mote burst gets the loudest treatment on the board.
`Connate.SpawnScoreMotes` pays a destroyed tile out as **gold nuggets**, scaled three ways by its rank: more
of them (`ScoreMotesMinimum + rank × ScoreMotesPerRank`, capped), bigger (`ScoreMoteSizePerRank`, capped),
and spread over a longer stream (`ScoreMoteStaggerSeconds`).

- **Gold is reserved.** Nothing else on this board is gold — not a tile family, not the bomb, not a rule ring
  — so a burst of it can only mean "you are being paid". ⚠ Don't spend the colour elsewhere.
- **Nuggets are faceted crystals, never coins.** The board is already made of round tokens; a round payout
  would read as tiles leaving rather than as treasure. They're glassed and cel-outlined like everything else so
  the payout belongs to the same drawing.
- **The stream is the point.** Each mote carries its own `Delay`, so arrival is `Delay + ScoreMoteFlightSeconds`
  and a big tile ticks the counter over and over instead of once. Size and delay both climb across the burst,
  so it ends on the biggest nugget and the biggest tick. ⚠ Zeroing the stagger collapses the whole effect back
  into one jump.
- **Value follows size**, split by *area* from a running rounded cumulative total. It must re-add to the tile's
  value exactly — under the collected-motes-only rule a rounding loss is a scoring bug, not a cosmetic one.
- **The counter takes the hit.** Each arrival kicks `Connate.ScorePulse`, which swells and warms the number and
  throws an expanding ring. A number that merely changes on its own is the weakest possible ending for the
  game's only scoring event.
- **`Cue.Collect`** fires per arrival, throttled in the *sim* (`ScoreCollectCueMinimumSeconds`) rather than at
  the mixer — at 120 Hz an unthrottled burst is a buzz instead of a till.
- Motes stay **deterministic off the body id**: an RNG draw here would perturb the feed, so an explosion would
  change which tile you are handed next.
- **C10 —** fired **1s and 2s** spin, damped as they settle. Only those two: they're a wedge and a mouth, so
  rotation reads on them; on a complete circle it would be invisible. `ConnateBody.Rotation` is **sim** state,
  so a frozen tile resumes at its angle rather than snapping.

**Snapshot 6 → 7**, deliberately discarding older boards rather than migrating: merge legality itself changed
and bombs moved from a schedule to a charge, so an old board would resume under rules it was never played by.
High scores live outside the snapshot and survive.

**Snapshot 7 → 8** (Aug 9 2026) is the opposite call: **a v7 board still loads.** The change is purely
subtractive — the dead `Turns`/`NextBomb` fields left the record, no rule moved, and every surviving field
keeps its name, so a v7 document deserializes into the v8 record with the two dropped properties ignored.
`Connate.ReadableVersions` is the accept-list; anything off it (a future version included) still yields a
fresh game rather than a crash, the same posture `ArcadeStore` takes with the file itself. Discarding those
boards would have cost a player their run to delete a counter nothing read.

## Connate's cel-shaded pass

- **Every element carries a flat black outline** (`ConnatePalette.Cel(thickness)`), width scaled per element —
  a hairline on a big tile beside a slab on a small one reads as two art styles. ⚠ **The bomb is the one
  exception**: its body is near-black, so a black stroke would erase its silhouette. It keeps a cream edge.
- **The heap turns like a record platter.** `ConnateTuning.PlatterDegreesPerSecond` applies a **rigid**
  rotation to every body's position, velocity *and* drawn rotation each step. Rigid means relative positions
  are untouched, so contacts, merges and the clump envelope see exactly what they saw before.
  ⚠ This is **not** the emergent swirl (`ClockwiseSwirlPerSec2`). That applies a tangential *acceleration*,
  which gravity and collision damping absorb almost entirely — doubling it twice never produced rotation
  anyone could see. Both now push the same way: the swirl supplies loose unequal drift between neighbours,
  the platter supplies the turn.
- **The 1 is a five-pointed star; the 2 is a disc of the 3's size with a star-shaped hole.** They visibly slot
  together into a 3, which two arcs of one circle never did — a broken thing rather than two that fit. The
  hole is exactly the 1's drawn size (`StarCutoutFraction` tracks `StarRadiusFraction`), so the fit is true.
  ⚠ **Both colliders are now plain circles**, and the whole convex-sector/SAT path for these ranks is gone —
  along with the rotation coupling it needed. The 2's hole is decorative (a 1 meeting a 2 *merges*, it never
  nests), and the 1's star points overhang its circle slightly, which is the right trade: a star collider
  would catch by a point and read as sticky.
  ⚠ **The two are ONE SIZE since Sep 8 2026**, `StarPairFraction` 0.76 of the 3's radius — between
  the two they used to be and nearer the smaller, so the 1 grew ~27% and the 2 shrank ~24%. The old split
  existed so the star could drop into the socket's hole; against the current art, which draws each shape as a
  **face inside its own rim** rather than as a silhouette, that left the 1 looking small for no visible
  reason. `StarRadiusFraction` survives as the drawn star FACE only — the vector fallback and
  `tools/SpriteTemplates` (development repository only, not in the public source) — and no longer touches a collider.
  ⚠ **The nesting claim above is now art, not geometry.** The two PNGs were authored at the old size ratio,
  so the star face renders larger than the socket's hole. If "these two slot together" has to read exactly,
  it wants an art pass, not a tuning change.
  ⚠ `ConnateBody.AreaFraction` is **derived for BOTH ranks, neither literal** — each divides out the shared
  footprint so `mass(1) + mass(2) == mass(3)` still holds exactly. A fraction above 1 (the 2's is ~1.15) is
  correct, not a bug: it says the piece packs its share of a 3 into a footprint too small to hold it at unit
  density. That identity is what keeps a merge conserving momentum instead of nudging the heap, and
  ConnateProbe asserts it — writing either fraction as a number breaks it silently the next time the
  footprint moves.
- **Tiles wear a plain NUMERAL, `rank + 1`**, so the 3-tile shows 3. The value still exists — it's what a
  bomb pays out — but "can these two merge" is a same-count question, and matching 3 against 3 beats matching
  "384" against "384". (The dice-pip face this section once described is gone; the numeral is what ships.) Counts
  hold to nine (a 9-tile is worth 12,288);
  past that it falls to two concentric rings rather than inventing a tenth die face.
- The 1 and 2 carry **no** pips — their shapes are the label.

## Kabloom K3–K4

- **K3 — the level transition.** One sim clock (`Kabloom.TransitionElapsed`) drives all three parts: the board
  pulls away on a single sine (so it returns to exactly 1.0 rather than drifting), and each petal flips on a
  delay proportional to its distance from the middle. That delay *is* the centre-out wave, and it also means
  the new garden's outer ring arrives last — the "new tiles drawn at the edge" half of the effect.
  ⚠ The petal wears the **cleared** face until its own midpoint. The board you just finished is already gone
  from the simulation by then (`LoadLevel` replaced it), so the memory of it is *drawn*, not read.
  ⚠ Not animated for the constructor or `Restore`: opening the arcade is not a level change, and replaying
  the flip on every dismiss-and-return would turn a reward into a tax.
- **K4 — the hazard is a bee.** The MaterialDesign `Bee` glyph via MahApps `PackIconMaterial`, the same pack
  the wheel's icons come from, so it matches the app's vocabulary instead of being a hand-rolled lookalike.
  Resolved with `Enum.TryParse` and cached frozen; a pack build without the name falls soft to the old spiked
  silhouette. The bee gets a pale halo behind it — it's a solid dark shape on dark covered petals and would
  otherwise read as a hole.
  **Only the bee you actually set off is angry** (bolts orbiting it, driven off the presentation clock rather
  than particles, since the sim has stopped by then). Making every revealed bee buzz would bury the one cell
  the player needs to learn from.

## Kabloom levels 11+ play PRE-BAKED boards

**Why.** On this pentagonal crop, boards that solve with no guessing all but vanish above ~20% occupancy: at
the old curve's 23.7% on 186 cells the runtime generator found 0 in 2,880 tries and fell to its "final band",
a **one-mine board** — the level-35 report that started this. The solver was not at fault: an opened region
walls itself off behind deduced mines and the rest is 50/50 pairs. Hex geometry (six neighbours, degree-three
rim) does this far more readily than a square grid, so the authored 26% ceiling never existed. Measured
solvable rates at authored densities, 400 boards each: level 10 fine; level 20 (21%) ~1 in 360, technique 1
only; levels 35 and 49 (24-26%) zero.

**What.** `tools\KabloomBake` (development repository only, not in the public source) searches offline (60k candidates per level), solves every candidate from every
zero-clue cell (one solve per cascade region), keeps the boards that solve without guessing, and selects a set
whose certified start cells **cover the crop** — hardest boards first, taking one whenever it adds an uncovered
cell, then topping up to a minimum for variety. Output is `KabloomBakedBoards.Data.cs` (GENERATED; bees at two
bits per cell, starts at one bit, base64 over cell ids; each level carries its capacity and bee total). At the first reveal `Kabloom.BeginGeneration` asks `KabloomBakedBoards.TryPick(level, cells,
firstCell, seed)` for a board on which the clicked cell is a certified start and plants it at once, so the first
click is safe, opens, and leads to a no-guess board — the same promise the early levels get from the generator.
The pick mixes the campaign seed with the cell, so replays and fall-backs see different boards.

**Difficulty.** Cell occupancy stays at the measured ceiling (16→18% in chapter 1, 18→20% across chapters 2-3
and again across 4-6; `KabloomLevelProfile`). The bee COUNT then comes from stacking: capacity 2 from Canopy
with the doubled share climbing 25%→75%, capacity 3 in Full Bloom (`BeeCapacity`, `DoubledFraction`). The
census that set those knobs (`tools\KabloomBake -- --census --plain`, 300 boards per cell, one start each,
Windows 11, Release) — boards certified no-guess out of 300:

| Level (cells) | 18% cap 1 | 18% cap 2, ¾ doubled | 18% cap 3, ¾ tripled | 20% cap 1 | 20% cap 2, ¾ | 20% cap 3, ¾ | 22% any |
|---|---|---|---|---|---|---|---|
| 20 (126) | 63 (23 bees) | 52 (40) | 64 (57) | 25 (25) | 34 (44) | 26 (63) | ≤7 |
| 30 (166) | 45 (30) | 34 (52) | 36 (74) | 15 (33) | 13 (58) | 13 (83) | ≤2 |
| 40 (206) | 46 (37) | 31 (65) | 34 (93) | 19 (41) | 16 (72) | 20 (103) | ≤3 |
| 49 (240) | 20 (43) | 13 (75) | 23 (107) | 5 (48) | 3 (84) | 8 (120) | ≤1 |

Stacking costs little certification because what seals a board is the mined CELLS, not the bees in them;
22% cells is the wall at every capacity. Past density, difficulty comes from **selection**: each board is
scored on stalls (tiers 1-4 exhausted), deductions above tier 1, whether the global count was needed, the
largest enumerated component and a small opening; the kept set per level is the hardest covering set. The
arc rises with level through density, stacking, size and selection together; individual levels are not
forced monotone (the arc matters, not level-over-level order). `--report` prints the per-level table.

**The solver decides everything that is deducible.** `KabloomSolver`'s tiers 1-4 are the cheap human patterns
(saturation, subset difference, exact overlap, chains); its exact pass decomposes the frontier into witnesses
and boxes, enumerates every component, and applies the global bee count to frontier and interior alike (the
"mine counting" endgame). A cap on the pass reports `Undecided` and emits nothing — never a wrong fact — and
every applied fact is asserted against the truth (`UnsoundFacts` must be 0). Measured in a Debug build: the
complete engine certifies ~3× the boards the pattern-only solver did at every density (level 15: 17.9% of
random boards at 20% cells, 6.4% at 22%, 1.6% at 24%; level 45: 6.4%, 0.9%, 0.1%). It moves the ceiling by a
few points, not to 30%: a sealed opening with 50/50 pairs beyond is a real guess, and no engine removes it.
Specification: §6 of the Kabloom design specification (development repository only).

**Contracts.**
- ⚠ The bake is keyed to the crop `KabloomGrid.Create` builds for the level's target cell count. `TryPick`
  refuses a bake whose cell count no longer matches and the game falls back to runtime generation, so a stale
  bake degrades to *slow*, never to *wrong*. **Re-run the tool after touching the crop or the occupancy curve.**
- Levels 1-10 keep the runtime generator; it finds boards within a second at those sizes and densities.
- The runtime generator's one-mine final band still exists as the last-resort fallback for an unbaked level.
- `KabloomProbe` (development repository only, not in the public source) verifies every baked level: cell count matches the crop, every board solves from every
  claimed start with first-cell protection intact, and the starts cover the crop.

## The Aug 8 2026 pass

### Connate — the board clear

Emptying the field of every tile **and** every garbage blob multiplies the run's score by
`BoardClearMultiplier` (1.5) and shouts about it. The only route there is a bomb taking the last numbered tile
— which sweeps all garbage with it — so this is something to aim for rather than an accident, and it paid
nothing before beyond the motes that tile happened to carry.

⚠ **The base is the BANKED total (`ExplodedValue`), which includes motes still in flight.** The clear is
triggered by the very explosion that launched them, so a base of `CollectedScore` alone would be momentarily
near zero and pay almost nothing at exactly the moment the player did the hardest thing in the game. The bonus
banks too (so it counts toward the stage and survives `Restore`, which derives the score from the bank) and is
collected at once; adding it while motes travel is safe because arrival only ever adds.

### Connate — stages: the pace follows the banked score

Six stages, entered at a banked total of 100 / 250 / 450 / 700 / 1,000 (`ConnateTuning.StageThresholds`,
inclusive); every per-stage value lives in `ConnateTuning`. What a stage changes:
the shot deadline (3.00 s − 0.15 s per stage index) and how fast the garbage countdown is consumed (`dt /
scale`, 1.00 down to 0.55); the 10–30 s draw itself never changes. From stage 2, the last 8 s of every 48 s of
playing-phase time is **relief**: garbage pauses and the deadline gets 0.30 s back, capped at 3.00 s.

- **The stage is derived, never stored.** `Connate.DifficultyIndex` reads `ExplodedValue` through
  `ConnateTuning.StageIndexFor` every time, so no snapshot field, no migration, and one payout can skip stages.
  `ExplodedValue` is the bank, not just the bomb ledger: a board-clear bonus goes into it as well.
- ⚠ **A deadline change RESCALES the elapsed clock** (`SyncShotClockDeadline`): `elapsed × new / old`, so a
  clock half-used stays half-used. Shrinking the deadline under the elapsed time would otherwise fire the piece
  on the frame the stage ticked over. It is called before the clock is spent each step and again after physics
  and the board-clear check, so the readout and the next step agree within the frame.
- **Relief PAUSES the garbage countdown.** The stall argument against pausing it for a held bomb does not apply:
  relief ends on the clock whatever the player does. `ReliefActive` is on the phase clock (`PhaseTime` in
  `Playing`), which pause, the card and the resume beat already stop and which a breach leaves.
- **A bomb costs one more charge per stage index** (`Connate.BombChargeCost` = `BombChargePerBomb` +
  `BombChargePerStage` × index, 8 → 13). Earned charge carries across a stage change; `Restore` clamps the
  stored charge against the cost of the stage it restores at, which is why the clamp follows the bank.
- **The readout is a caption under the score row** (`ConnateRenderer.StageText`): Internode's `STAGE {0}`
  string, so every locale already has it, in the plate's dim ink, switching to sage (`ConnatePalette.Aim`)
  while relief is on. The HUD plate grows down by the caption's height; the score anchor and the meter do not
  move. The charge meter slices to the live cost. `ShotClockUrgency` and `AutoWindCharge` are fractions of the
  live deadline (`Connate.ShotClockDeadline`), so the arc and the self-wind simply run shorter.
- `ConnateProbe ▸ CheckDifficultyStages` holds the ladder, both deadline tables, the rescale at the top of the
  clock, the relief window edges, the countdown scale and pause, and a restore at stage 3.

⚠ **It latches** (`_boardOccupied`). An empty board stays empty for as long as the next shot takes to land,
and without the latch every one of those frames would pay again — compounding the whole score at 120 Hz.
`Restore` derives the latch from the restored board, so resuming an already-empty snapshot pays nothing.

The banner shares one routine with the combo banner (`DrawShout`) rather than imitating it, so the game has
exactly one way of shouting and this reads as the biggest instance of something the player already knows.

### Connate — glass

Tiles, the 1/2 pieces, garbage and the bomb all take the wheel's **Obsidian/Pearl** vocabulary: a bowed-horizon
gloss lit 16° off vertical, an inner edge that shades where the light lands and brightens on the far rim, and a
hard specular. `ConnateRenderer.DrawGlass` takes a **geometry**, so one routine serves a disc, a star, a
star-socket and a garbage lump — the rim traces whatever silhouette it is handed.

⚠ **No transparency**, deliberately. Overlapping tiles are the normal state of this board and a stack of
see-through discs is unreadable; every layer here is a light or a shade painted *on* an opaque fill.
⚠ **Call it before the cel outline.** The outline is the drawing's ink and has to be last; a gloss over it
would soften the exact edge the cel look depends on.

### Kabloom — the cleared board no longer stops

**No confirmation card and no replay offer.** A cleared level runs its completion and then advances itself:
the bees leave, the gems come in, the board holds for `CompletionHoldSeconds` (0.60) and the transition takes
over. The card was a keypress standing between the player and the reward they had just earned, and its only
other offer — replaying a board you had just solved — is not something anyone wants mid-campaign.

⚠ The hold is measured from when the presentation went **quiet**, not from when the board cleared: a
completion's length varies with how many bees and gems the level had. Without it the flip began on the exact
step the last gem landed, so the reward and the thing that wipes it away happened on the same frame — the card
used to supply that beat as a side effect of waiting for input.
⚠ This is the change most likely to break a probe. Anything that steps a cleared board is now stepping into
the *next* level; KabloomProbe stops the moment `Board.Phase` goes `Cleared`.

`Stage.CampaignComplete` keeps its card — that one really is the end of something.

### Kabloom — bees leave, gems present themselves

- **A bee that is done here flies off**: +75% (`BeeDepartureSwell`), outward from the middle of the crop with a
  per-bee bias, wandering across its own heading on a sine. One routine (`DrawDepartingBee`) serves both the
  cleared board's bees and the single bee you set off — the failure was meant to be "the same" as
  the completion, and making it literally the same code is what guarantees it.
  ⚠ `MineFinaleSeconds` went 0.34 → 0.95 to give the flight room. That constant also gates gem release and
  `PresentationActive`, so it lengthens the whole completion beat — intended, not a side effect.
- **The bee you set off** additionally gets **white** lightning orbiting it, each bolt blinking on its own
  phase. The old zigzags were the failure *black*, which made the angriest thing on screen the least visible
  now that these bees sit on a dark revealed petal.
- **A freed gem spins up and swells to 3×** over `DiamondSwellSeconds` (1.0) before flying. The collection was
  one gesture; splitting it in two gives the pickup a beat of its own, and it is the only moment the facets are
  big enough to see. The flight hands off at that size and shrinks back to normal, so the gem visibly recedes.

### Connate — garbage waits for a bomb, and the feed opens up a little

- **The ready/payload slot draws nothing while `BombDelivering` is true** (`ConnateRenderer`). The bomb
  appears in the ready spot only when its fly-in from the HUD charge meter lands, so there is exactly one bomb
  on screen during the delivery — drawing the slot early gives the player a bomb that is simultaneously
  arriving and already arrived.
- **No garbage arrives while a bomb is loaded.** A bomb is a plan, and it already has no shot clock, so the
  player is deliberately being given time to read the board — a blob dropping into that changes the answer to
  a question they were still working out.
  ⚠ **Deferred, not paused.** The countdown keeps running and the arrival retries a second later. Pausing it
  was the obvious reading of "no new garbage" and it opens a full stall: with a bomb held there is no
  auto-fire *and* no garbage, so a player who simply never fires is never threatened by anything again.
- **The feed leans less hard on the bottom of the ladder.** All four knobs move together —
  `AdvancedChancePerRank` 0.08 → 0.10, `AdvancedChanceMaximum` 0.40 → 0.50, `AdvancedRankWeightDecay`
  0.58 → 0.46 (flatter), `TopTierWeightMultiplier` 0.09 → 0.14. The first pair decides how often you draw from
  the advanced pool at all and the second what that pool hands you; leaning on either alone changes the
  character rather than the mix. The bias is unchanged in kind — low tiles are what a heap is built from.
- **The board's top tile is never handed back.** Above value 6 the advanced pool stops **one rank short** of
  the highest numbered body, so the biggest thing on the board can only grow by being built up to. Being
  gifted its own partner was the cheapest scoring line in the game, and it arrived by luck rather than play.
  **1, 2, 3 and 6 are exempt** (`ConnateTuning.FeedTopMatchExemptRank` = rank 3): they are the base bag's own
  range plus the first double, and an early board gated any harder has nothing legal left to feed.
  ⚠ The **base bag is unaffected** — 1/2/3 stay legal at all times, as they always were. Only the advanced
  injection is capped, and `HighestRank` still excludes garbage.

### Kabloom — a little warmth on each covered petal

A subtle brownish-red radial falloff from `KabloomCell.Vertices[0]` — the same near-centre vertex
`PetalVertices` cuts toward and `KabloomCenterCap.Center` is built from, so "the hub side" is literally the
point a diamond animates toward. Anchored so it is essentially gone by **three-quarters** of the tile's own extent (widened from half to
three-quarters), then held transparent — a falloff across the *whole* shape would have read as a gradient;
this reads as one edge of the material catching a little colour and the rest staying plain. Covered petals only: a revealed tile is
already the flat dark "cleared" fill, and shading it would muddy the numeral.

### Kabloom — covered petals and resting gems stand off the board

Every covered petal and every resting nectar gem is drawn raised: its face shifted up the screen by
`KabloomTuning.TileThicknessInGridUnits` (0.238 grid units, ~4 px at the 512 px cabinet still) and the gap below
filled as the tile's side. A petal's side is one `KabloomPalette.PetalSide` fill, the convex hull of the face and
its footprint (`SweepDown`), cached per petal at full thickness. A gem's side is one wall per downward-facing
edge (`NectarWalls`), each shaded by which way it faces from `GemSideLeft` to `GemSideRight` with a
`GemSideSeam` between walls, so the stone reads faceted. Cleared petals are flat.

- ⚠ **Draw order is load-bearing:** all flat petals first, then the raised ones **top of the screen down**.
  Raised after flat, or a cleared petal above a covered one paints over the face standing into it; top-down,
  so a lower petal's face is drawn in front of the side of the petal above it, which is what makes the thickness read.
  **Resting gems join that pass in the same top-down order, keyed on their hub** (`DrawRestingGem`): petals below
  a hub cover the gem's walls; the gem covers the petals above. Freed gems still draw over everything.
- A revealing petal **sinks** with its reveal progress (`PetalLifts`), so the punch-through lands on a petal
  already flat. A level-flip petal still showing its cleared face is flat; once it turns covered it is raised.
- Everything that sits on a covered petal rides with it: the focus outline and its sparks, bee flags and
  question marks. A freed gem starts its swell from the raised spot, so it doesn't drop as it frees.

**Colour sweep.** Covered petals also carry a faint board-wide tint (`PetalTint`, one absolute-mapped radial
brush): honey (`PetalTintCentre`) over the middle fading to clear at half radius, then violet (`PetalTintRim`)
building to the rim. Drawn over the petal art, under its re-stroke.

### Kabloom — STUNG is poster word art, not a plaque

The failure shout draws through `DrawShout`, not `DrawPlaque`, in **one hue** — the ember coral of `FailureInk`:
a bold geometric sans (`ShoutFace`: Century Gothic, else Futura, else Segoe UI) with a solid coral face, a thin
cream rule parting it from the extrusion, and the extrusion drawn **only as dark-coral hatch lines** down-left —
the hatching carries the depth, with no fill behind it. Speed lines in the same dark coral trail off the end
away from the parked bee. No gradient, no heavy outline, no border or blob shadow. It carries its own contrast,
so it is the one string over the crop with no plate.

- ⚠ **It must clear the parked bee.** Centred on the card column, then pulled back so its face and extrusion stop
  `StungShoutReach` short of centre on the bee's side: 0.12 field radii (the bee's drawn head is at ~0.18),
  less `StruckBeeStackDx` per extra bee, since a stacked petal's bees park nearer the card.
- Built once per string, size, side and DPI into a frozen `Drawing` — the footprint is a union of a dozen offset
  glyph outlines, far too much to redo per frame. YOU WIN and the button prompts stay on plaques.


### Kabloom — flags carry a count; □ press steps it, □ hold is the question mark

A petal holds 0..`KabloomBoard.Capacity` bees (1 on every level until the census-driven curve says otherwise)
and a clue is the SUM over its neighbours. □ **released** on a covered petal steps `CycleFlag`: clear → 1 →
… → capacity → clear; □ **held** `KabloomTuning.QuestionHoldSeconds` (0.35 s) toggles the question mark
instead and the release that follows does nothing (`Kabloom.Step` tracks the press in flight; a press never
outlives a freeze). Both marks are HELD — no reveal, cascade or chord passes through them until cleared. A
chord fires when the flagged SUM equals the clue. The "n BEES" line is `BeeTotal − FlagSum`; an increment that
would push flagged bees past the bees on the board is **refused and the cycle wraps to clear**, so the
readout never goes negative, and `RestoreState` rejects a save claiming otherwise. A flag above one draws that
many bee glyphs sharing the ring — two side by side, three in a triangle — rather than a numeral, which does
not read at petal size (`DrawBeeFlag`); clue inks run to 15 (`KabloomPalette.Clues`).

**The bees-per-petal card.** Whenever a level raises the capacity over the level before (26 and 42 on the
current curve), `LoadLevel` raises `Kabloom.CapacityNotice` and the renderer draws an interstitial over the
unplanted board: title, "up to N bees", the □ press/hold line, and `✕ CONTINUE`. The card swallows every press;
✕ dismisses it and does NOT open a petal. It is **locked** for `KabloomTuning.CapacityNoticeMinSeconds` (5 s,
continue line dimmed) only the first time the run meets that capacity (`_noticeSeenCapacity`, saved); a
fall-back and climb shows the reminder without the wait. A freeze with the card up resumes with the card up
and, if it was locked, the five seconds start over. Strings: `UiText.Arcade.CapacityTitle/Body/Flags/Continue`. The remaining-bee count is shown at **every level**: the certification uses the total (the mine-counting
endgame — every remaining bee accounted for on the frontier, so the clue-less petals are safe), so a player
denied the total would face a forced guess on a board certified no-guess. Hiding it past level 10 was tried
and reversed on exactly such a position (level 25, four bees left in four pairs, eleven clue-less petals
that only the count proves empty). `KabloomSolver.Explain` reads a saved position back as the chain of
deductions, one line per fact with the constraint that forced it; ALL CLEAR still shows on a cleared board.

Snapshot **version 4** writes `Capacity`, `Bees` and `Flags` as byte arrays; a version-3 save (`Mines`/`Flagged` bools) is
folded to counts on read and the next save is version 4. Baked data packs bees at **two bits per cell**, starts at
one, and each level carries the capacity it was baked at. Help: `UiText.KabloomHow4`, the Kabloom line in
`HelpContent`.

### Kabloom — the flag is a bee

The padlock is gone. It was a leftover from when the hazard was a thorn and the mark meant "locked"; marking a
cell with the thing you think is in it needs no explaining, and the padlock was the one glyph on the board
belonging to no other part of its vocabulary. Same glyph as the hazard, opposite treatment — dark ink on the
pale covered petal, where a revealed bee is white on a dark one. A guess and a fact must not look alike.

### Kabloom — text over the crop

Every string drawn on the playfield now goes through `DrawPlaque`: larger, on a near-opaque dark plate with a
pale rule. ⚠ It measures the text's **ink geometry** and derives both the plate and the glyph position from
that one box. The first cut used `TextAlignment.Center` and sized the plate from `FormattedText.Width` — a
centre-aligned line centres inside `MaxTextWidth`, which is not that number, so the plate and the glyphs were
positioned off two different measurements and the text hung off the left of its own plate. Same fix
`DrawInkCentered` already applies elsewhere in the renderer. The crop runs from near-white covered petals to a near-black revealed face, so a single ink was
crisp on one board and nearly gone on the next — and the growth notices land *during* the level flip, when the
ground beneath them is actively changing. The plate makes legibility a property of the readout instead of a
coincidence of where it fell. Sized to the text, so a short label doesn't drag a bar across the board.

## ⚠ Hex colours in the arcade palettes are `#AARRGGBB` — alpha FIRST

`ColorConverter.ConvertFromString` reads an eight-digit literal as **`#AARRGGBB`**, not the CSS-style
`#RRGGBBAA`. Every translucent brush in `KabloomPalette` and `ConnatePalette` was written the wrong way round,
and all of them were corrected Aug 8 2026. (`VenturiPalette` had it too; that game was deleted the same day.)

**It never fails loudly**, which is what makes it expensive — the value parses and you get a plausible colour
at a wildly wrong opacity. What it actually cost:

| Written | Meant | Was really |
| --- | --- | --- |
| `#151827F2` (Kabloom `Plaque`) | dark navy, 95% | bright blue, **8%** — the reported "very transparent" plate |
| `#00000066` (Connate `Shadow`) | black, 40% | alpha `0x00` — **every Connate drop shadow drew nothing, ever** † |
| `#81B29A66` (Connate `GravityLine`) | sage, 40% | brown, 51% |
| `#3C405BDD` (both `Scrim`s) | field colour, 87% | saturated blue, 24% |

† **Connate's tile shadows were then REMOVED outright**, not retuned. The board had been
designed against a flat cel scene for weeks precisely because the shadow never drew; making it work was a
regression in look, not a fix. The `Shadow` brush is deleted rather than left unused — keeping it would be a
loaded gun for whoever next wants "a bit of depth", when this board's depth comes from the flat black outline
and the glass rim, not from lighting. The bomb lost its shadow too: it is a projectile rather than a tile, but
it rides the rim beside them and the charge meter draws it twice more, so it would have been the single
shadowed object on a flat board.

⚠ **It had been misdiagnosed before.** A now-deleted game's design note recorded its tunnel seams "reading as
bright yellow because they were cream at low alpha" — they were in fact *95%-opaque yellow*, the same bug
wearing a different symptom, and the replacement colour was balanced against that wrong reading. Worth knowing
because it is the failure mode to expect: the symptom presents as a colour problem, so it gets solved as one.

`ArcadeChrome` was never affected: its helper takes explicit `(r, g, b, a)` bytes, which is the shape to prefer
for anything new.

## ⚠ A brush that is a FILL must not also be an INK

Kabloom's **STUNG** card once drew black text on the near-black plaque.

`KabloomPalette.Failure` is `#000000` because it is the **fill of the bee tile you set off** — the one revealed
petal darker than all the others. It was also being handed to `DrawPlaque` as the ink for STUNG and for an
over-flagged seed count. That was fine while those strings sat bare on a board of near-white covered petals; the
Aug 8 plates put every string on `Plaque` (`#FF151827`), and black-on-near-black is what a black fill becomes
when you call it an ink.

Split into `Failure` (the tile) and **`FailureInk`** (`#E07A5F`, ember coral, shared with Connate's ember
family). Not the gold `Warning`: gold already means "this got harder", and a run that has *ended* must not wear
the difficulty notice's colour.

A full sweep of both games Aug 9 2026 found no others — every other ink in either renderer is cream, sage, gold
or white, and Connate additionally outlines its shouts in cel black, so its text carries its own contrast
wherever it lands. The rule going forward: **name the role, not the colour.** A brush used in both roles will
eventually be wrong in one of them, and it fails silently, exactly as the `#AARRGGBB` trap above does.

## Replaceable sprite art (`ArcadeSprites`)

Named **slots** let supplied PNGs stand in for a game's vector drawing, one object at a time.
`Arcade/ArcadeSprites.cs` owns the resolution and the drawing helpers; the slot names, frame counts and
what each is fitted to are the artist-facing contract in **`Assets/arcade/sprites/README.md`** — keep that
file, `ArcadeSprites.Slot` and the renderers' call sites in step.

- **Every slot is optional and the vector body is the fallback.** A renderer asks for art and, on null,
  draws exactly what it drew before. With no PNGs present the five renderers are pixel-identical to their
  pre-sprite selves — that is the invariant, and it is why nothing here throws or logs on a miss.
- **Finished art and guides live in SEPARATE folders**: `sprites-finished/` ships,
  `templates-unfinished/` (development repository only, not in the public source) holds a guide per slot nobody has drawn yet and never ships. Which folder a slot
  sits in is what records whether it is done, and `tools\SpriteTemplates` skips files that exist, so
  re-running it just fills the gaps.
  ⚠ **`ArcadeSprites.PackedDir` and the csproj `Resource` glob must name the same folder.** That string is
  spliced into a `pack://` URI, so a mismatch is not a build error — every slot silently misses and all four
  games quietly fall back to vector, which looks like the art was never delivered. Verify a folder rename by
  enumerating the built assembly's `.g.resources` keys, not by a green build.
- **Two homes, override first.** `%APPDATA%\Radiata\arcade-sprites\<slot>.png` beats packed
  `Assets/arcade/sprites/sprites-finished/<slot>.png`. `ArcadeControl.Open` calls `ArcadeSprites.Reload()`, so the override
  folder is re-read per open — the same "edit, reopen" loop `arcade-tuning.json` has. **Nothing probes disk
  from the render pump**; a resolved slot, a miss included, is latched until the next open.
- **Animation is numbered files** (`<slot>-1.png` up, stopping at the first gap), played three ways:
  `Frame` loops and cuts, `Blend` cross-fades round the loop for a throb, `Once` plays out and back a single
  time from a clock counting up out of an event. Callers pass a stable per-object `phase` so a row of tokens
  does not blink in lockstep.
- ⚠ **An idle animation runs off `ArcadeSprites.Time`, a wall clock — an EVENT one may not.** The games do
  not share a presentation clock and a spinning tyre is decoration, so idling on wall time is fine. Anything
  keyed to something that HAPPENED must read a clock the sim owns and snapshots, or a frozen game stops
  repainting the same frame: Connate's craft plays its launch off `Connate.SinceFire`, a capped seconds-since
  counter added to the sim and its snapshot for exactly this. Reach for a renderer-side timer and that
  invariant is gone.
- **Art that repeats across a board is drawn ONCE and turned per instance.** Kabloom's petal is one wedge
  serving all five petals of every floret, rotated so its up runs hub-outward; the box it fills is measured
  off the six outline points rather than by rotating and re-bounding the geometry, because that path runs per
  cell on a crop that can carry a couple of hundred of them and must not allocate. The how-to card's petals
  go through the same helper, so the card can't show a different board from the board.
- **A shape the sim deforms takes a STAGE SET, not one bitmap.** (Petalpop no longer takes art at all — its
  ball, paddle and bend-stage slots are gone and the renderer is pure vector; the pattern stands for the next game
  that does.) Petalpop's paddle bows with the slingshot
  draw, so its slots are flat / half / full and the renderer cross-fades between the two nearest on `g.Draw`
  — a bitmap held rigid over a flexing slab is the thing this avoids. The box art fills is read off the slab
  geometry rotated into the paddle's own frame, never recomputed from the spine, so the extent can't drift
  from the shape being drawn. Any deforming slot added later should follow the same shape.
- **Art may be bigger than the collider.** Paddle art is matched to the slab's LENGTH and hung from its
  field-facing edge, then allowed to overrun downward — a paddle is drawn as a body behind a hitting face,
  and only the face collides. That is why it is not clipped and why it replaces the slab rather than layering
  on it: the cel outline traces the collider and would cut a line across a tall drawing. The tier COLOUR goes
  with the body it replaces; the charge glow reaches outside the slab and survives.
- **Three techniques, pick by what the object's colour means.** A free-standing sprite *replaces* the body
  (Internode's bike and mine, Kabloom's gem, Connate's bomb, craft and tiles — the tile families are the
  merge rule, so the artist draws one face PER HUE rather than the board tinting one). A **clipped overlay** — centred art
  cropped by the vector shape — is for anything whose silhouette or fill still has a job: Petalpop's core
  and paddles, Kabloom's petals (the grid outline is how a crop reads as cells at all, and the clue numbers
  are a later pass) and Connate's garbage lumps (the outline is also the collider). A **tint wash through the art's own alpha**
  (`ArcadeSprites.Tint`) carries a state the vector fill used to carry — a token's height hue, gold, the
  drained grey of a miss, and Internode's darken-with-distance.
- **The templates are generated, never hand-drawn.** `tools\SpriteTemplates` traces every crop and footprint
  out of `Core` into `Assets\arcade\sprites\templates-unfinished\`, so a guide line is the line the renderer cuts on.
  **Re-run it when petal, core, paddle or craft geometry moves**, and keep templates in that SIBLING folder — the
  shipping glob is `Assets\arcade\sprites\sprites-finished\*.png` and does not recurse, which is the only thing stopping a
  template being compiled in and drawn as real art.
- **A slot may be ADDITIVE — art with no vector original.** Connate's payload clamps are drawn only if the
  artist supplies them; there is no clamp in the vector board, so there is nothing to fall back to and
  inventing one would change the game for everyone who ships no sprites. Such a slot is placed in the frame
  of whatever HOLDS it (the craft) rather than what it sits on (the spinning tile), or it slides around the
  thing it is meant to grip.
- ⚠ **Art cannot carry a cue the sim needs and the code has stopped drawing** — and the answer is sometimes
  that an object gets no slot at all. Kabloom draws its bee from one shared glyph in two inks: white on a
  revealed petal is a **fact**, dark on a covered one is the player's **guess**. Only the fact takes art;
  the marker keeps the vector glyph, because art on both would make a guess and a fact identical. Adding a
  slot means deciding this first, and saying so in its doc comment and in the README.

## Editing the games' on-screen text

**The code is the only source of truth.** Every player-visible string is a `UiText.Arcade` const (`Core/UiText.cs`);
the renderers, each game's `HowTo` card and pause options, and the host's pause menu, confirm prompt, footers
and `READY` all read it through `Loc.T`, so one edit changes every screen and the translation catalog at once.
Game NAMES (Kabloom, Connate) stay literal. The chrome face is `Share Tech` with a `Segoe UI, Tahoma,
Arial` fallback chain, so a translated caption in a script Share Tech lacks still draws instead of collapsing the
layout stack (docs/LOCALIZATION.md §3).

To edit the copy as a whole, **generate a fresh copy deck from the code** and edit it in the deck page:
`tools/copy-deck` (development repository only, not in the public source) exports every authored string
with its key and where it appears, and its importer writes the edits back into the code and retires the
affected translations. The Arcade rows are the `UI: Arcade` surface. See docs/LOCALIZATION.md §2 ▸ *Copy pass*.

⚠ **Do not check a deck in.** `docs/ARCADE-STRINGS.md` was exactly this list as a tracked Markdown table
and was **deleted Aug 9 2026**: a second copy of every string is a second thing to keep in step, and it had
already drifted — it never carried the pause-menu rows, the confirm prompt or `READY`, and its
`stage.firstReveal` entry outlived the string it described. Regenerating from the code takes seconds and
cannot be stale.

### ⚠ Never spell a button glyph out

Use `ControllerButtons.Text(PadButton.X)`. A literal `✕` / `○` / `□` / `△` names the **wrong pad** for every
player on the Xbox glyph set, and the setting can change while a game sits frozen — the arcade renderers get
this right for free because they redraw every frame.

Two places had it wrong until Aug 9 2026, both footers: the **Arcade picker** (`✕ Play  ○ Close`) and
the **bleed-through guard card** (`○ Close  △ hold to play anyway`). The guard card was the worst of them —
the one screen whose entire job is explaining a blocked feature, naming two buttons the reader doesn't have.

**`TestHarness.exe glyphs` now enforces this** (in the default `all` set). It scans the source — not the
assembly, because a correct call and a hard-coded string are indistinguishable by the time they reach a
`DrawText` — and asserts in two tiers:

- **The arcade may not spell out a glyph at all.** No allow-list, and none can be added: `Arcade\**` and
  `ArcadeControl.cs` draw nothing but prompts and game text.
- **Everywhere else, the file must be on a written allow-list** (the resolver itself, the Settings preview,
  the two Settings remove-buttons, and the Help prose + translations about the Button icons setting). A
  third check fails on *stale* entries, so the list can't outlive what it excuses and sit open as a licence.

It decodes `\uXXXX` inside literals, so writing the escape is not a way around it, and it strips comments —
this repo's comments discuss these glyphs constantly. ⚠ Its first run failed on two XAML **comments**;
allow-listing those files would have been the easy fix and the wrong one, since `GameBrowserControl` draws
the Game Grid and a real prompt added to it later would then have passed.

## △ explains the game

**△ opens a how-to card** — a title and about five illustrated bullets — over the frozen game, and the pause
menu carries a **HOW TO PLAY** row to the same card. That is what let the games stop printing button prompts
on their own playfields: a prompt spends board space on every frame of every session to teach one thing once,
where the card is one button away and can afford to explain the **rules** rather than name the keys.

- **The content is DATA, not a card each game draws** (`ArcadeHowTo` / `ArcadeHowToLine` in `Core`). The host
  owns the layout, so every game's card has the same shape and the same way out, and a new game gets all of
  that by writing five strings. Same argument `ArcadePauseOption` makes for the pause menu.
- **The pictures come from the game's own renderer** (`IArcadeRenderer.DrawHowToArt`): the sim names a picture
  with a string key, the shell draws it into a box it is handed. That keeps `Core` WPF-free and means a
  Kabloom bullet is drawn in Kabloom's palette with Kabloom's shapes. Connate's art calls the board's *real*
  `DrawOrb`/`DrawBomb`/`DrawCraft` — its rules are carried by shape and colour, so a stand-in would teach the
  wrong thing. An unknown key draws **nothing** rather than throwing; the call is wrapped, because this is the
  render pump.
- ⚠ **`HowTo` returning null means neither route is offered** — no △ hint, no menu row. A button that
  sometimes does nothing is worse than one that isn't advertised.
- ⚠ **The card is a LAYER, not a mode.** Opened from the pause menu it backs out *to the pause menu*; opened
  from play it backs out to play behind the usual ready beat. `ArcadeControl.BackOut` owns that precedence in
  one place, and `App.ArcadeCircle` just asks it — the host used to query `ArcadePaused` and decide for
  itself, which would have needed a second flag query for every layer added after.
- ⚠ **△ must TOGGLE.** `ShowHowTo` consumes input on the way in (`ClearInput`), so an open-only △ would eat
  its own press and leave the card with no exit through the button that opened it. ✕ closes it too — a card
  with one exit is a card people press every button on. **START** drops the card and then does its normal job,
  so from the card over the menu it lands on the board and from the card over play it lands on the menu —
  which is what "I pressed pause" should give you either way. ○ is the one that walks back a layer at a time.

### The one surviving prompt

A **`△ HOW TO PLAY`** pill, drawn by the **host** in the **bezel** at 6 o'clock, until the card is opened on that
game — **per game** (tracked separately in each game). From then on the same pill reads **`START MENU`**
(the START word badges as a ringed pill, `ArcadeChrome.TryPrompt`), so the bezel always names the one button that
leads somewhere without naming a play control.

- **Host-side** so a new game inherits both the hint and its disappearance without implementing anything.
- **In the bezel** because it is the only band a round window leaves free: both games occupy the bottom of the
  *playfield* (Kabloom's seed count, Connate's craft orbit), and covering the HUD to advertise a card would be
  a worse trade than the prompts this replaced.
- **Persisted in `ArcadeStore`**, beside the high score rather than in a game's settings blob — it is a fact
  about the player, not the run, and it must outlive both a finished run and a Reset. `Slot.HowToSeen` is a
  **defaulted** field so an older `arcade-state.json` still loads (the file version deliberately did **not**
  bump — bumping it would make every existing saved campaign unreadable to teach a hint).
- **Written the moment the card opens**, not at the next dismiss: the whole promise is that it goes away and
  stays away, and a hard kill in between must not bring it back. `Save` carries the flag through and has no
  parameter for it, so a routine freeze can never resurrect a dismissed hint.

**No during-play prompt names a button.** Kabloom's first-reveal line (now `FIELD IS READY`) is a *state
readout* — a crop of blank petals doesn't otherwise show that nothing is planted — and Connate has none at
all. **The end and intro cards keep theirs** (`✕ FALL BACK`, `□ REPLAY FINAL FIELD`, `✕ BEGIN`, `✕ AGAIN`, and
Connate's `△ HOW TO PLAY` — the intro points at the card rather than compressing the rules into a tagline):
on a stopped screen the button *is* the content, and dropping it leaves a dead end rather than a cleaner
board. ⚠ That is the one rule to re-check when the copy is revised — a footer that loses its `{…}` token
turns a terminal card into a label.

### Both games lost their held-△ aid

Kabloom's **lens** (hold △ to lift the focused cell's neighbourhood) and Connate's **merge assist** (hold △ to
ring every tile the held piece could merge with) are **gone** — deliberately not rehomed. Both were teaching
aids you had to discover, and the card teaches the same things by stating the rule once. Connate's assist in
particular answered "what can this merge with" for *one board*; the card's "only same-colour pieces merge —
1 + 2 is the exception" answers it for all of them.

⚠ `DrawOrb`'s `compatible` parameter was **deleted**, not passed `false` — the same reasoning that deleted the
unused `Shadow` brush. A flag nothing can ever set is a loaded gun for whoever next wants to highlight a tile,
and it would have them reviving a feature instead of designing one.

## ○ leaves; START pauses

**START opens a pause menu** over the frozen game — the sim genuinely stops, it isn't a frozen-looking
overlay. Rows: **Resume**, **How to play**, **Reset**, then whatever the game contributes. ○ backs out of the
menu rather than closing the game, since the menu is the one screen whose entire purpose is deciding what to
do with it.

⚠ **The fixed-row count is DERIVED, not a constant.** It was `const int PauseFixedRows = 2` and every index in
the menu was measured against it; the how-to row only exists for a game that *has* a card, so hard-coding 3
would have silently mapped RESET onto a missing row for any future game without one. `ArcadeControl.FixedRows`
returns the actual list and the menu indexes that.

⚠ A **hold-○-to-restart** gesture lived here first and was replaced the same day. A hold is a hidden binding
that does exactly one thing and can never say so; the menu is discoverable, names what it does, and has room
for per-game settings that would otherwise need a trip to Settings.

⚠ ○ is still **not** routed into `ArcadeInput`. A game that could observe it could also fail to hand it back,
and "you can always get out with one button" would stop being true of the whole family.

**Games contribute their own rows** through `IArcadeGame.PauseOptions` / `ApplyPauseOption` — a default
interface implementation, so adding a game never forces you to think about the menu, and the two games that
have settings declare them next to the state they change rather than the menu growing a switch over game ids.

| Game | Row | Notes |
| --- | --- | --- |
| Connate | **Radial controls** on/off | ⚠ `Connate.RadialAiming`, **per game and persisted**. The Settings ▸ Advanced checkbox that used to drive this was REMOVED — it reassigned the global on every config reload, so it silently stomped the pause-menu choice. Defaults off. |
| Kabloom | **Starting size** easy / medium / hard | Easy starts at level 1; Medium and Hard start on the first level of each bees-per-petal step — **26** (up to 2 a petal) and **42** (up to 3) — read off `KabloomLevelProfile.BeeCapacity` by `StartLevelFor` rather than written down, so they track the curve. The capacity card shows on arrival. Jumps immediately (a choice you can't see means nothing) and resets the run score, so starting further in is not a way to bank points. **Asks first** — see below. |

The cursor moves on the **d-pad or the stick** — the stick is read as edges past a deep threshold, so a resting
thumb cannot walk the menu on its own. ✕ chooses, and on an option row also cycles it, so the menu works without
discovering left/right; the chevrons beside a choice point only where a step can go. **Reset asks first** (the
same THIS ENDS THE RUN prompt a destructive game option uses) and then restarts the run, never the high score.
The footer shows ✕ and a START pill only. **HOW TO PLAY sits between Resume and Reset**, so a thumb walking down from the landing row meets
the harmless one first.

### Reset means the RUN

⚠ **`Kabloom.Restart()` was a no-op**, on the reasoning that `IsGameOver` is permanently false so the host's
"never resume into a game-over screen" rule would never call it. True — but the **Reset row calls it too**, so
for Kabloom the pause menu's most destructive option silently did nothing. It now restarts the campaign at
`StartLevelFor(StartingSize)`, zeroes the score and rerolls the seed. Replaying a single board is what falling
back after a bee already does and needs no menu.

### A destructive option asks first

`IArcadeGame.ConfirmPauseOption(key, choice)` returns a question or null. Kabloom returns
**"START A NEW RUN?"** when the starting size would actually change *and* there is a run to lose (a planted
board, a level past this size's own first, or any banked score) — and null otherwise, so the same choice is
free on a fresh board.

⚠ **Asking has no side effect.** Nothing is applied until the prompt is answered YES: the option row still
shows the live value while the question is up, so backing out at any point leaves the run exactly as it was.
The host replaces the menu rows with the prompt rather than floating it over them — a dialog over a still
readable list invites answering the list — and lands on **NO**.

Reset is deliberately **not** gated behind the prompt: it is an explicitly labelled destructive row the player
chose on purpose, where the starting size is a value you can wander into with a d-pad.

## The picker is the "Arcade Launcher" slice

**The picker is offered as a slice action again**, named **Arcade Launcher**, the FIRST option of the Arcade
group in both the Settings slice editor and the in-wheel Add picker; each game follows as its own option. It
stores a **blank `Command`**, which `ActionExecutor` passes straight to the host. An **unknown id is treated
the same** — a config from a newer build has to be able to reach something playable.

**The launcher RESUMES, it does not always land on the cabinets.** `App.OpenArcade` reads
`ArcadeStore.LoadLastSurface()` — the surface the arcade was dismissed on, written by `ArcadeControl.Close` —
and opens that game directly, or the cabinets when that is where it was left. The field is a defaulted
addition to `arcade-state.json`, so an older file reads null and gets the cabinets. **The cabinets resume
on the cabinet that was frontmost at the dismiss** (`ArcadeStore.LoadLastCabinet()`, a second defaulted
field), not the first catalogue entry and not the in-process frozen game; ○ back from a game still lands on
that game.

⚠ **Only a launcher session writes that surface.** Closing inside a game opened by its **own** slice leaves it
untouched: that was a trip to one game, not a move around the arcade, and letting it redirect the launcher
would send the next launcher press somewhere the user never navigated to.

**The cabinets are a back-out LAYER under a launcher-opened game.** `OpenArcade` passes `fromLauncher`
through `ShowArcade` into `ArcadeControl.Open`, and `BackOut` then returns ○ to the picker (freezing and
capturing the board on the way, so its cabinet screen is current) instead of falling through to the host.
The next ○ leaves. ⚠ A game opened by its **own** slice sets the flag false and ○ leaves on the first press —
there is no launcher beneath it to return to, and inventing one would strand the user a press away from
where they meant to be.

⚠ **The launcher's `TypeOption` must stay first and must never be deleted.** It is the fallback
`SelectTypeOption` resolves for any command naming no catalogued game — blank, the edit model's filler, or an
id from a newer build. Without it those slices match nothing, which per [ACTIONS.md](ACTIONS.md) makes the
next auto-save **rewrite the slice's type outright**. (It was `Hidden` from Aug 6 to Sep 5 2026, when the
picker was a plain ring of three titles; the cabinet carousel is a destination worth a slice.)

## Arming an arcade slice always shows the hub

On every material, whatever "Always show hub" says. Implemented in `RadialMenuControl.HubDemanded`, **not** in
`HideCenter` — ⚠ `HideCenter` keys the wheel's drop-shadow *shape* (soft disc vs donut) and is required to be
a material/setting-level fact that cannot change while a wheel is up; a value flipping mid-wheel would visibly
change the shadow under the whole wheel.

**And it carries a still of the game.** `RadialMenuControl.DrawArcadePreview` draws
`Assets\arcade\<gameId>-preview.png` centred in the hub at `ArcadePreviewOfHub` (**0.92**) of its diameter and
`ArcadePreviewOpacity` (**0.80**), so the hub's own rim stays visible as a ring around it and its material
reads *through* the still — at full size and full opacity the preview stops looking like something the hub is
showing and starts looking like the hub.
Because the hub was already guaranteed present and sized whenever `ArmedIsArcade` is true, this landed as a
draw call rather than a visibility rule — it didn't move the layout.

- **The art is a round playfield on a transparent square**, so an aspect-preserving fit into a square box
  puts the game's disc concentric with the hub's — no clipping geometry on a round hub.
- **A SCALLOPED hub fills instead, clipped to its own outline** (`ArcadePreviewOutline`). A rim whose troughs
  come in further than the 0.92 inset leaves no ring to read: the disc spills out between the scallops while
  the peaks stand outside it, which is the picture failing to fill the hub and overflowing it at once. Those
  hubs take the picture edge to edge (`ArcadePreviewOfHub` → 1.0) and clip it to `HubSilhouette` — the one
  builder `DrawHubDisc` also fills, so the clip can never describe an edge nothing draws. The test is the
  MEASURED `HubCoverageFraction` against the inset, not a material list: kawaii's cloud is **0.836** and
  fills; mesa (**0.993**) and salvage (**0.991**) keep their ring, and every round hub is 1.0. The launcher's
  cabinet plate follows the same rule. Coverage numbers are pinned by harness group `arcade`.
- **The path is built from the catalog id**, so a new game needs a PNG, not a code change. A missing file
  caches null and draws nothing; the hub then carries whatever it otherwise would.
- Drawn **ahead of reactor's armed-slice label**: there the armed arcade slice fuses into the hub, and a still
  of the game says more about it than its own name does.
- `ArmedArcadeGameId` is the same test returning the id — and returning **null for an UNKNOWN id**, since that
  slice opens the picker rather than a game, so there is no single game to preview.

## A wheel holding ONLY the Arcade Launcher opens it outright

No wheel is drawn: `App.ShowOverlay` sees one slice, type `arcade`, `ArcadeCatalog.Resolve(command) is null`,
and calls `OpenArcade(null)`. The launcher is itself a menu, so a single wedge in front of it is a selection
with nothing to select between. The arcade blooms where that wheel would have been — `ShowOverlay` sets
`_lastWheelCx/Cy/Side` first, because no wheel session will. Restricted to the **Launcher**: a lone named-game
slice still draws its wheel and keeps release-while-centred as the way to back out. Practice mode is exempt.
⚠ That wheel then has no in-wheel edit route (no wheel to click the stick on) — it is edited from Settings.

## The wheel → playfield transition

Choosing an arcade slice no longer dismisses the wheel. `RadialMenuControl.BeginArcadeCollapse`:

1. Each slice sweeps in behind the hub, delayed by its **ring distance from the chosen slice measured the
   short way** — so the motion radiates outward in *both* directions from what the player picked.
2. Each slice leans slightly **out** before it goes in (anticipation), and stretches along its travel axis
   while pinching across it — the squash-and-stretch is applied by rotating to the slice's own radial axis,
   scaling, and rotating back.
3. At `CollapseHubStartFraction` (0.62) of the sweep the **hub grows to 105% of the playfield radius**
   (`OverlayWindow.ArcadeHubOversize`) and stays there, painting behind the game for the whole session.

The whole transition is deliberately brisk — it sits between the player's press and their game, so it reads
as a wipe, not a cutscene: `CollapseSliceSeconds` 0.15, `CollapseStaggerSeconds` 0.0225 per ring step,
`CollapseHubSeconds` 0.13, `CollapseGlowFadeSeconds` 0.10 out and `CollapseGlowRiseSeconds` 0.125 back in.
Keep the stagger a small fraction of the slice time or the radiating order stops reading as one wave.

⚠ **105% is of the hub's SMALLEST silhouette, not its nominal radius.** Every shaped hub is MEASURED, not
derived — `RadialMenuControl.RingCoverage` bisects the geometry the material actually draws, so it can't drift
from it — and `OverlayWindow.ArcadeHubRadiusFor` divides the target by that coverage. Measured (harness group
`arcade`, which prints them):

| Material | Coverage | Extra growth |
| --- | --- | --- |
| Kawaii | 0.836 | +19.7% |
| Salvage | 0.991 | +0.94% |
| Mesa | 0.993 | +0.74% |
| Everything else (plain disc) | 1.000 | none |

⚠ **Mesa and salvage were left at 1.0 until Aug 8 2026** on the reasoning that their wobbled/stepped rims sit
within a couple of pixels of their radius, well inside the 5% margin. The arithmetic was right and the
conclusion was wrong: *clearing the edge* is not the same as *being visible*, and a wobble whose peaks clear by
2px more than its troughs reads as a plain circle. They go through the same measurement now — the harness
prints all three so a future material can't be argued about instead of measured.

⚠ **This is a small correction on Mesa (0.74%, about 2px).** If the collar still reads thin, the knob is
`OverlayWindow.ArcadeHubOversize` (the 105%), not this — but that one moves every material at once.

Details that matter:

- **The hub goes OPAQUE for the collapse.** Most fills carry a little alpha — the gloss
  ramps run 242-252, the flat fills 235 — which is invisible over a game but not over a dozen slices sweeping
  behind the hub, where a bright slice still read through at 92%. `DrawHubDisc` paints a backing **plate of
  the same geometry** under the fill, ramped in over 0.12s. A plate rather than an opaque copy of each fill:
  the fills are gradients as often as solids, and rebuilding every material's ramp at full alpha would mean
  maintaining a second copy of each. The plate matches the material's own end of the scale (`DarkMaterial`),
  so the ~5% shift it causes is imperceptible. Applied on **every** material — a no-op where the fill is
  already opaque, and a list of "the translucent ones" is one more thing to forget when a ninth lands.
- **Slices never fade.** They travel the whole way to dead centre at full opacity, and the hub — opaque, drawn
  *after* the ring layer — occludes them for real. `BeginArcadeCollapse` also cancels and pins
  `RingOpacity` / `CenterOpacity` / `SelectedSliceOpacity` to 1, because firing a slice normally starts the
  wheel's dissolve and the sweep would otherwise play out inside an already-fading wheel.
- **Reduce Motion gets a compliant path.** A dozen objects accelerating across the screen with
  squash-and-stretch is exactly what the policy exists to suppress, so under it the hub is simply already
  grown, the slices are simply already away, and the callback fires one dispatcher turn later. Same outcome,
  no travel.
- The collapse is a wrapper (`DrawCollapsingSlice`) around `DrawSlice`, not another branch inside it.
  `DrawSlice` already threads six materials' worth of dwell/arm/edit state and the collapse needs none of it.
- `OverlayWindow.ShowArcade` does **not** zero `Menu.Opacity` while collapsing — that hub is the backdrop. It
  also re-centres the wheel on the disc's final position, since the disc is clamped to stay on screen and near
  an edge it won't land on the wheel's centre.
- `App` sets `_arcadeOpen` **before** the sweep and clears `_overlayVisible` immediately: the wheel is dead to
  input from the first frame of the animation, and ○ / Esc / a controller drop during it route to
  `CloseArcade` as normal. `FinishOpenArcade` re-checks `_arcadeOpen` for exactly that reason.
- **Esc does what ○ does** (`App.SurfaceBack` → `ArcadeCircle`: how-to card / pause menu first, then the game to
  the launcher, then out). The window is non-activating, so no WPF key event ever arrives: `App.RegisterBackKey`
  arms a `WH_KEYBOARD_LL` hook that swallows Esc while the arcade (or Game Grid) is up, with `RegisterHotKey`
  as the fallback for a hook Windows drops. Both are released in `UnregisterArcadeHotkeys`; a refused
  registration is traced (`Esc hotkey refused`), never silent.
- `CloseArcade` calls `HideWheel()` as well, because the wheel layer was left painting rather than hidden and
  nothing else would take it down.

## Steering: relative, not absolute (A1, Aug 6 2026)

Every rim-riding avatar asks **`ArcadeAim`** rather than reading the stick itself, so the games cannot
disagree about what "left" means.

- **Shipping model: relative.** Stick left/right drives the rim at a rate, spinner-style. Only the HORIZONTAL
  axis is read, so a diagonal push never fights the turn.
- **Absolute** (the stick's *angle* is a position on the rim) was a **per-game** choice, default off.
  ⚠ It was a global setting — Settings ▸ Advanced ▸ Arcade ▸ Radial aiming, pushed into `ArcadeAim.Absolute`
  at startup *and on every config reload*. That reload is what killed it: the per-game pause choice was
  silently overwritten the moment anything touched settings. The checkbox is gone and
  `SystemConfig.ArcadeRadialAiming` is REMOVED (an existing config carrying it just loses the key on the
  next save; nothing ever read it). Connate owns its own `RadialAiming` instead. `ArcadeAim.Absolute` itself
  is deleted — nothing needed it any more.
- **Speeding up is INSTANT and tilt-proportional; only the release has a clock** (`ArcadeAim.StepVelocity`,
  exponential bleed over `BrakeSeconds` = 15 ms). An acceleration ramp was tried and felt like driving a bus.
  Reversing the stick brakes rather than snapping, so whipping it the other way costs the slide.
- ⚠ **The sign is anchored to SIX o'clock**: push right at the bottom of the circle and
  the avatar moves right *on screen*. Screen angle increases clockwise, so that means stick-right maps to a
  *decreasing* angle — hence the negation in `ArcadeAim.Rate`. Six o'clock is the anchor because it's where a
  rim-riding avatar spends most of its time in front of the player. The top half necessarily reads mirrored;
  that's the accepted half of the trade. **Don't "fix" it** by flipping the sign above the horizon — a control
  that reverses mid-lap is far worse than one consistently mirrored at the top.

⚠ **Speeds tuned for absolute aiming are too fast for relative steering.** Absolute aiming only ever reached
top speed in short darts toward a named angle; relative holds it for as long as the stick is held, so the same
number that felt crisp under one model is uncontrollable under the other. Connate's `PlayerDegPerSec` went
**420 → 235** for exactly this reason. Any future rim-riding game inherits the same trap: pick the speed under
the model you actually ship.

## The picker

A plain "Arcade" slice (blank `Command`) — and any UNKNOWN game id — opens the picker in the same round
window: a **rotary carousel of arcade cabinets** (`Arcade\ArcadePickerRenderer.cs`, one 386×720 PNG per game
under `Assets/arcade/`) standing on a perspective floor grid under a starfield, one cabinet per
`ArcadeCatalog.Games` entry in registry order. A game without art of its own stands in a blank cabinet — tinted when its package names a `tint`
— and only that one has its title printed, inside the cabinet's own nameplate band.
Every cabinet's round screen shows its game's screenshot, turned 5° counter-clockwise and masked above the
control panel's back edge (`ScreenCut*Frac`, the safe area marked up on the cabinet template); the front one
lights up — art at full strength, screen glowing in its game's accent, the bezel and the horizon glow following it — while the others stand dimmed with their screens
darkened 35 % and desaturated 90 % (`RearScreenDarken` / `RearScreenDesaturate` — a greyscale copy under the
colour at the surviving opacity, since a drawing context has no saturation filter). ✕ plays: the chosen cabinet's screen picture lifts off the monitor,
straightens and grows to fill the disc while the disc grows to a game's size (`LaunchSeconds`,
`ArcadePickerRenderer.DrawDeparture`), then the game appears behind its ready beat where the picture landed.
No fade to black — the board is what carries the player in, and ○ runs the same motion backwards
(`DrawArrival`). Instant under Reduce Motion. ○ closes. Under the front cabinet: the game's blurb
(`ArcadeCatalog.Entry.Subtitle`) and stored best.

- **Steering is relative, not absolute aim.** The stick's HORIZONTAL axis or the d-pad steps the ring one
  cabinet per flick (arm at 0.55, release at 0.30 deflection), auto-repeats while held (0.42 s, then every
  0.18 s), and wraps. **Pushing RIGHT brings the cabinet on the RIGHT to the front**, so the ring turns and
  every cabinet slides left. The vertical component does nothing. `Core/Arcade/ArcadeCarouselNav.cs` is the
  pure state machine; ✕ is honoured mid-swing because the index is authoritative the instant a step lands.
- **Screenshots** (`Arcade\ArcadeShots.cs`): a live capture of the player's own board is written to
  `%APPDATA%\Radiata\arcade-shots\<id>.png` whenever a game is frozen — on dismiss and on the swap inside
  `EnsureGame` — but never for a finished run (the previous shot stands, matching the snapshot rule) and never
  from `Persist()` itself, which also runs on pause-menu changes. A script game is captured from its last
  validated buffer (`ScriptGameRenderer.DrawLast`), never by pumping the helper. Fallback chain: the game's
  seeded still — a drop-in package's `preview` file (`ArcadeCatalog.Entry.PreviewPath`, decoded capped like a
  live shot), or the bundled `Assets/arcade/<id>-preview.png` for a built-in — then a placeholder plate with the
  game's initial. Shots decode at
  open, never on the render pump; a corrupt file costs a picture, not a frame. **The wheel hub shows the same
  shot** when a game's slice is armed (`RadialMenuControl.DrawArcadePreview` reads `ArcadeShots` too).
- **The Arcade Launcher's slice gets a hub too** — a shot of the carousel as it was last left
  (`ArcadeShots.LauncherId`, taken by `ArcadeControl.CaptureLauncherShot` when a cabinet is opened and on
  dismiss, drawn at rest so no half-faded zoom frame is kept), the way a game's slice shows its board. Until
  the launcher has been opened once there is no shot, and the hub falls back to the blank cabinet on the
  arcade's dark field (`DrawArcadeLauncherPreview`). ⚠ `RadialMenuControl.ArmedIsArcade` must stay true for an arcade slice whose
  command names no game, not just for a resolvable one: it is what `HubDemanded` holds the hub open for, and
  the arcade collapse sweeps the slices in behind that hub. Gate it on a resolved game and the launcher's
  slices animate out behind nothing.
- **The sky is Internode's own starfield sprite**, tiled and drifting slowly on a fixed heading
  (`SkyTileFrac`, `SkyDriftPerSec`, `SkyDriftAngleDeg`), under a flat black wash (`SkyDarken`, 0.40) so it
  reads dimmer here than in the game. Read through `ArcadeSprites`, so a player who
  replaces that sprite gets the same sky in both places. The tile is wider than the disc, so only a
  section is ever on screen. The brush stays frozen and the DRAWING is translated, over a rect inflated by
  one tile so the slide never exposes an edge. Still under Reduce Motion.
- **The sky is clipped to above the horizon, and the ground is painted opaque under the grid.** The floor is
  a surface, not a window onto the same sky — the grid lines and their shade alone leave it transparent.
- **The floor carries the Reactor material's sparks** — white-hot heads with tapering tails in the wheel's own
  charge colours (`RadialMenuControl.ReactorSparkHead` / `ReactorSparkTailColor`), riding the fan columns
  toward the viewer and the drifting rows sideways. Decorative: gone under Reduce Motion.
- **Every offered game is a cabinet**: the four built-ins and every consented drop-in package, in
  `ArcadeCatalog.Games` order. A package accepted mid-session re-registers the catalog, and the carousel
  re-seats itself the next frame it sees a different count.
- **The swing** is a critically damped spring (`ArcadePickerTuning.SwingSpringHz`). Under **Reduce Motion**
  the ring snaps and the floor grid's drift stops (`ArcadeControl.ReduceMotion`, pushed by
  `App.ApplyMotionPolicy`); see [MOTION-INVENTORY.md](MOTION-INVENTORY.md).
- **Each game brings its own cabinet art**, `Assets/arcade/sprites/sprites-finished/cabinet-<gameId>.png`,
  falling back to `cabinet-blank.png` for a game that ships none — every drop-in package, and any built-in
  still waiting on art. `ArcadeArt.CabinetFor` builds the path from the `ArcadeCatalog` id, so new art is a
  file, not a code change; the folder's existing csproj wildcard packs it. ⚠ The cabinets share that folder
  with the replaceable in-game sprites but are **not `ArcadeSprites` slots** — that class resolves art by
  slot name and never enumerates, so the two sit side by side without colliding. **Nothing recolours a
  game's own art** — depth dims it and that is all. The blank cabinet alone takes a colour: a drop-in package's
  `tint` multiplies its greys (`ArcadeArt.CabinetFor(id, tint)`, built once per colour), so its shading survives.
- **The title is printed only on a machine wearing the BLANK cabinet** (`ArcadeArt.HasOwnCabinet`). Art of
  its own names the game on its marquee; the blank cannot, and without the printed title a drop-in game would
  be unnamed everywhere on this screen — the blurb line above says only "Drop-in game". The title sits inside
  the blank art's own nameplate band (`Nameplate*Frac`, measured on `cabinet-blank.png`), with no plate of its
  own, in Bahnschrift Bold Condensed (the tall narrow caps of the built-ins' painted names; ships with Windows 10
  and 11, Segoe UI stands in without it), dark ink unless the tint leaves the band dark
  (`ArcadeArt.NameplateColor`). A package's `badge` PNG floats at the band's left end and overflows it, like the
  built-ins' illustrations (`NameplateBadge*Frac`), and the title centres in what is left — on its glyphs' ink
  bounds both ways, not its line box, whose ascender/descender space would sit all-caps low. A title too wide for
  the lane shrinks on one line down to 60 % of full size, then wraps between words in tighter-set lines, shrinking
  until the widest word fits the lane and the block the band (`FitNameplateTitle`, cached per cabinet size); only
  past the smallest size does a word split or the block end in an ellipsis.
- ⚠ **`FormattedText` trims with an ellipsis by default** (`Trimming = CharacterEllipsis`) whenever
  `MaxTextWidth` is set, and its geometry is then the TRIMMED text's — a width check on it passes a word it cut
  short. Set `Trimming = None` wherever text is measured to fit.
- ⚠ **Every cabinet PNG shares the template's frame, silhouette and screen position.** The picker's screen
  ellipse and the dark plate behind the art are fixed geometry (`ArcadePickerTuning`'s `Screen*Frac`,
  `ArcadePickerRenderer.Outline`), measured against the 386×720 template. Art that moves its screen loses the
  screenshot; art that changes its outline loses its backing plate or gains a dark halo.
- **Per-frame cost is the art `DrawImage` plus the screen mask** — the art over a silhouette plate that stops
  the cabinet behind showing through wherever the art is transparent, and the cabinet's screen ellipse clip, which
  is cached per size (`ArcadePickerRenderer.ScreenMask`) rather than rebuilt per frame.
- Every layout and feel number lives in `Core/Arcade/ArcadePickerTuning.cs`, reachable from the dev
  `arcade-tuning.json`.

Selecting a different game **freezes the current one to disk first** (`EnsureGame` persists before
swapping), so the picker can never silently discard a run. The guard card covers the picker exactly as it
covers a game — picker input bleeds the same way. An empty catalog draws the room and `NO GAMES INSTALLED`
with only the ○ footer.

**The in-wheel △ Add picker offers Arcade as a sub-wheel of games**, matching the Settings slice editor —
Arcade is a category, not a destination.

⚠ It didn't, until Aug 9 2026. `App.BuildAddMenu` flattened the Radiata category to `Options[0]` of every
entry — correct while they were all single-option leaves, but Arcade is a GROUP whose `Options[0]` is the
**hidden picker option**. So adding Arcade in-wheel authored a slice with a blank `Command`, which the
executor traces and no-ops: **a dead slice**. The fix is the one this doc already prescribed — let a Group
entry build its own sub-wheel there (the way Audio/Display/Power always have) rather than special-casing
Arcade. Hidden options stay unlisted in that sub-wheel, so the picker is still never offered.

## Adding a game

1. Sim in `Core/Arcade/Games/<Game>/`, implementing `IArcadeGame`. Pure; feel constants into its own
   `<Game>Tuning.cs` of mutable public statics, and that class added to `ArcadeTuning.TuningTypes` so the
   dev-only `arcade-tuning.json` override reaches it (a bare key resolves through the list in order; a
   `Type.Field` key names one class outright).
2. Renderer in `Arcade/`, implementing `IArcadeRenderer`; register it in `ArcadeRenderers.For`.
3. One entry in `ArcadeCatalog.Games` — id (**shipped, never change it**), title, picker subtitle, glyph,
   tint. That single entry is what puts it in the slice editor, the Add picker, the Arcade picker and the
   executor. Registry order is the picker's display order. **Tints come from the swatch bases the rest of the
   app already uses**, so an arcade slice can't be the one colour on a wheel that belongs to nothing else:
   Kabloom `#3C6E74` (the teal swatch base, `ActionTint`'s `switch-audio`) and Connate `#8C3A3A` (the brick-red
   swatch base, `ActionTint`'s `exit-app`). `ActionTint.EffectiveColor(type, command)` resolves `TintHex`
   for an arcade slice naming a game — the same catalog `MonoIcons` takes the glyph from, so colour and
   glyph can't drift. There is **no `arcade:<id>` override key**; per-game recolouring is a per-slice colour.
   A blank/unknown command is the picker slice and keeps the Arcade *type* colour. A bundled
   `Assets/arcade/<id>-preview.png` still is optional (a drop-in package names its own with `preview`): without
   one the picker's cabinet shows a placeholder plate until the game has been played once (the live capture then
   takes over).
4. Cues: a built-in game's own cue enum plays through its `ArcadeSfx` family; a script game's nine-name cues
   go through `ArcadeSfx.Generic.Cue` (both from `ArcadeControl.PlayGameCues`).
   Never map a per-frame or per-obstacle cue — see the `Connate.Move` note above.
5. A `HowTo` card — about five bullets, each naming an art key, plus a `DrawHowToArt` arm per key. Skipping
   it is legal (the base returns null) and costs the game its △ button and its pause-menu row.
6. Help: add it to the `arcade` topic's body (English only — the es/de/ja translations are refreshed
   separately, see [LOCALIZATION.md](LOCALIZATION.md)), then re-export the controls reference
   (`Radiata.exe --export-controls`).
7. Any derived code: a NOTICE next to the source plus an entry in
   [THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md) (the Kabloom/Tatham precedent).
8. Re-run `TestHarness.exe arcade` — the leaf count it asserts is derived from `ArcadeCatalog.Games`, so it
   follows automatically, but a mis-registered renderer won't.

**Retiring** a game means marking its leaf `Hidden`, never deleting it — the no-rewrite contract above.

## Petalpop (`petalpop`, Sep 2026)

A polygonal brick-clearer: a paddle rides each **straight edge** of the polygon (the
inward-bowing arcs the game was first built with made corner pockets and are now a knob, `ArcSagitta`,
defaulting to 0 with a 0.0005 floor so the arc formulas stay finite), one stick moves them all, and the
flower of petals in the middle is popped down to its gold core. The round-native argument rests on the
gutters: the lenses between each edge and the rim are where a ball dies and where the HUD lives. Full record
in the Petalpop design specification (development repository only); the framework-level facts:

- **The rails are the polygon's edges** (`PetalPopLayout`, arcs of near-infinite radius under the sagitta
  floor), sampled by the renderer from the same functions the sim bounces off, so the drawn rail IS the
  collision rail. The band between a rail and the rim is the **gutter**: a ball that gets past a paddle
  flies it and pops at the rim, and the HUD lives in those lenses (score at 6 o'clock, lives one side round
  to the left, level one to the right).
- **The stick is a screen direction, not a rim position.** Every paddle slides toward the pushed direction
  along its own rail (target = stick · rail chord), so pushing right moves every paddle right and the
  six-o'clock mirroring question `ArcadeAim` has to answer never arises. Petalpop deliberately does NOT use
  `ArcadeAim`. Two models sit behind `IPetalPopPaddleControl`: **spring** (default; release brings all five
  home) and **rail** (release leaves them), switched by the pause menu's CONTROL row and persisted as a
  per-game setting.
- **A run is STAGES of side count**: a square with four levels, then a pentagon with
  five, up to an octagon with eight; clearing the octagon's eighth level (labelled 5-8) **wins** (`Stage.Won`, reported through `IsGameOver` so the
  host clears the snapshot like any finished run). Each graduation pays a life (`LivesPerStage`, capped at
  `MaxLives`) and shouts `+1 LIFE`. `PetalPopLayout.For(N)` is the playfield per side count — the paddle
  array is rebuilt to N, and the layout is oriented so a **flat side always sits at 6 o'clock**
  (`Layout.ServeSide`): a square stands upright, a pentagon carries a corner at 12, a hexagon has flat top
  and bottom. Ball speed ramps on `LevelOrdinal` (1..30 across the run).
- **The flower is per (stage, level)** (`PetalPopFlower.For(sides, level)`): its OUTER edge grows with the
  ring count from `FlowerApothemFirst` (0.22) to `FlowerApothem` (0.31, or the rails less `RailClearance` if
  that is less), so the footprint swells gently across a stage while the core inside it shrinks. Level k has k rings (never more than `MaxRings`), each `RingPitch` deep when
  all are present and thickened `RingPitchBoost` per missing ring so a sparse flower's petals still read; the
  core takes the rest and never drops below `CoreApothem` (0.10). ⚠ A stage's first level must
  offer a big core — a small core on 2-1 and 3-1 was too hard to hit — but a FIXED footprint made that core
  too big, so the footprint splits the difference. A toughness budget of `ToughPerLevel` per level plus `ToughPerStage` per stage gives
  the ring at depth k `1 + floor(budget − k)` hits up to `MaxHp` (3); 4-1 is all one-hit. Alternate rings
  are staggered, and a staggered ring's corner petal WRAPS the corner as one bent petal with a rounded tip —
  its collider is two convex halves sharing one hit count, because the inner corner makes the whole shape
  reflex. The `Hp` array is re-sized on every refill and the snapshot validates it against the flower for
  its own stage and level.
- **The ball bounces off the petal you see.** The petal outline (inset quad, outer edge bowed by
  `PetalPopFlower.Bulge`) lives in the geometry layer; the collider samples that same curve and the renderer
  draws the true curve through the same control point. ⚠ Don't reintroduce a flat-quad collider under a
  curved drawing — the mismatch is obvious in play.
- **Serves are aimed by the player.** A fresh serve starts at a random angle inside a ±10° deadzone about the
  paddle normal (`ServeDeadzoneDeg`, RNG-drawn so it is in the snapshot); sliding the paddle while the ball
  waits steers `ServeAngle` (`ServeSteerPerUnit`), and only the excess beyond the deadzone eases back, to its
  edge (`ServeReturnPerSec`, ~1.4 s half-life), so the aim never settles on the exact normal and a
  slide-then-release keeps its aim through the launch. The 10 s shot clock (`ServeAutoSeconds`) still fires
  the ball on its own.
- ⚠ **ONE exit rule for every paddle contact**, expressed in the paddle's own frame
  (`PaddleFaceNormal` × `PaddleAlong`): the ball's normal component always leaves **inward** — a reflection
  for a ball that arrived, a shove for one the paddle overtook — and its along-paddle component carries
  through with its sign. `PetalPopContact.Engulfed` decides only whether a contact counts and how far the
  ball is pushed clear; it must **never** reach the direction. The player cannot see whether the paddle met
  the ball or swept onto it, so that difference cannot change where the ball goes.
  ⚠ Two defects this replaced, both worth not repeating: mirroring an *overtaken* ball aimed the result at
  the gutter, and the inward clamp then flung it at **72.5° to the swing**; and branching the direction on
  the engulf flag made two visually identical hits behave differently.
- **A SMASH is face-directed** (`SmashNormalPull`, 0.8): the arrival term is scaled down by the pull, so the
  swing decides the direction rather than the bounce, while the english still aims. At 0 a smash reflects
  like any other hit, only faster, which reads as no swing at all. PetalPopProbe (development repository only, not in the public source) fires sixteen arrival
  directions through both contact kinds, armed and not, and pins the guarantee, the carried-through sideways
  sign, the smash's face-directedness, and that the two contact kinds agree to within a degree.
- **Paddles CRAWL along their rails for the whole swing** (`PetalPop.PaddlesCrawling`): from the windup
  until the smash lands or its window lapses they are held to `PetalPopTuning.DrawCrawlSpeed` (0.3), so a
  slam still lands near where it was aimed (a crawl cannot cross the rail inside a windup) while a late
  correction stays possible — a paddle that stopped dead read as the game having dropped the stick. Full
  speed returns the instant the smash connects, since landing it zeroes the armed window.
- ⚠ **A paddle NEVER sends a ball away from the middle**, and it is a property of the geometry rather than of
  case analysis: `PetalPopLayout.PaddleFaceNormal` is the spine's field-facing normal, the collision reports
  it (not the "away from the spine" normal) for a ball the paddle is standing on top of, and `PaddleResponse`
  re-clamps every exit direction against it. **The bug that forced this:** the released slingshot teleported
  from behind the rail to in front of it in one step, leaving the ball behind its own paddle; the mirror about
  the away-from-spine normal then flung it out of bounds *through* the paddle. Two independent guards now —
  the sweep (`SpringPerSec`, which may not cross a ball's reach in one step) and the face clamp — and
  PetalPopProbe pins the clamp at both a swept and a teleporting spring rate.
- **The slam is a SLINGSHOT**: HOLDING ✕ draws every paddle `DrawDepth` (0.10, a windup
  more than four times the paddle's own thickness) behind its rail over `DrawSeconds` (`PetalPop.Draw` 0..1);
  RELEASING springs them `SmashLungeDepth × draw` forward and
  opens the armed window, so the hit that follows is the smash. A release under `MinDraw` is a twitch (small
  surge, no smash). `PetalPop.Lunge` — negative while drawn, positive on the spring — is fed to both the
  collider and the drawing, and the loss test moves with it so a ball is never judged lost while a
  drawn-back paddle could still catch it.
  ⚠ **The draw is bounded PER PADDLE** by `PetalPopLayout.MaxDrawDepth`, and `PaddleSpine` applies that bound
  itself so collider, renderer and loss test cannot disagree. The gutter is a lens — deepest at a side's
  midpoint, pinched to nothing where a corner meets the bezel — so a paddle mid-rail takes the whole windup
  (the square has room for all of it, the octagon's shallower gutter clamps to ~0.068) and one jammed into a
  corner barely moves. Winding up is something the middle of a rail can do and a corner cannot; without the
  bound a single depth either wasted the roomy shapes or pushed corner paddles through the bezel, where the
  playfield clip cuts them in half. The
  ball itself stays a circle in ordinary play; only a ball carrying +punch stretches and squashes, so the
  deformation is the slam's signature. Petal density is half the first cut (`BrickTargetWidth` 0.17).
- **Rings need not circle the whole shape**: from `PatternFromLevel` on, a ring may be cut
  into `Symmetry[ring]` evenly spaced arcs of petals — the orders are the divisors of the side count from 2 up,
  since only those close on themselves. Chosen from a hash of (sides, level, ring), never the game RNG: the
  flower is cached and the snapshot trusts it to rebuild identically, and a level should keep its identity
  across attempts. ⚠ **Ring 0 is never patterned** — the core is exposed by breaking a petal that touches it,
  so a gap there would let the opening shot fly through and end the level. An absent slot simply starts with
  no hit points, which is the same state as a popped petal, so gaps need no other machinery.
- **Multiball petals replace the collectible gem**: about a tenth of a level's petals
  (`MultiballFraction`, 1 to 5) are marked, and popping one splits the ball that did it. Seeded evenly through
  the level's PRESENT petals and deterministic per level, so a mark can be seen and played toward rather than
  drifting out of a broken petal and away. They keep the gem's mint colour, and the colour is the whole cue (no glyph on top). The gem machinery — drift, pickup, the pity counter, `Powerups` in the
  snapshot — is deleted rather than left dead; ⚠ don't reintroduce a floating pickup.
- **+PUNCH is a stock the ball carries, and slams ADD to it**: `PetalPop.PunchForDraw`
  steps the draw at release in even tiers up to `SmashPunch` (2) — a HALF draw adds 1, a FULL draw adds 2, and
  nothing short of a saturated draw reaches the top tier. A slam adds its tier to `PetalPopBall.Punch`, capped
  at `PunchMax` (6), so catching a still-charged ball and slamming it again keeps building: full slam → 2, pop
  a petal → 1, half slam → 2, full slam → 4. ⚠ The stock does NOT expire on a timer and paddle contact does
  NOT spend it — the earlier "hot for N seconds, worth N per strike" model is gone, so don't reintroduce a
  heat clock. Split children inherit their parent's stock: they came off the same swing. The paddles' arming
  rim steps by tier rather than ramping, so the charge visibly clicks up (`PetalPop.SmashCharge`), while the
  body wears the tier as colour — yellow-orange at +1, a pulsing red-orange at +2 — and bends like a drawn bow
  with the draw (ends fixed, middle pulled back from the field, thickness constant), so the worth is read off the paddle before letting go; the ball then escalates with the stock it carries — halo, aura ring, and one
  orbiting spark per +punch, all off one 0..1 charge in the renderer.
- **A petal contact spends one +punch, and the ball can PLOW**: `HitBrick` deals `min(hp, 1 + Punch)` damage,
  spends `max(1, dealt - 1)`, and rebounds unless the ball ARRIVED with +punch and the petal died. So a cold
  ball ALWAYS bounces off the first petal it meets; a surviving petal always stops the ball; and against one-hit
  petals a charged ball plows through one more than the +punch it carried. `BrickPass` therefore loops while the ball is
  plowing instead of stopping at the first contact. ⚠ The one-petal-per-bounce rule still holds for a cold
  ball, which is what keeps a ball sitting on a seam from popping two petals and reflecting twice.
- **The stick is curved, the paddle slab is half-rounded**: `PetalPopControls.Relieve`
  raises the relieved magnitude to `StickExponent` (1.8), so a half tilt places a paddle well short of halfway
  while a full tilt still saturates — fine control lives in the middle of the throw and a corner is still one
  hard push away. The paddle is drawn by `PetalPopRenderer.PaddleShape` (shared with the how-to card): the two
  corners toward the field are full quarter-circles, the two toward the rail are square. The BEND is sim
  geometry: `PetalPopLayout.PaddleSpine` takes the draw as `bow` and pulls its middle back parabolically
  (`PaddleBend`, a third of a thickness at a full draw); the collider and the renderer both sample that one spine, so
  the bent paddle hits where it is drawn. Only the DRAW is bounded by `MaxDrawDepth`; the bend may overlap the
  rim — ⚠ clamping it per sample flattened a cornered paddle's middle while its ends kept bending. Only the
  corner rounding is presentation.
- **Corner bumpers on the square stage**: `PetalPop.BuildBumpers` puts a right triangle in
  each corner of a `BumperSides`-sided board, legs `BumperFraction(sides, level)` of a side (15% → 10% → 5% →
  none across the four levels). They are rebuilt with the flower in `FillBricks`, so a restored game grows them
  back from stage and level and nothing is serialized. Collision is the petal `Polygon` test with a plain
  reflection: no english, no combo reset, no +punch spent — furniture, not a strike. Drawn in the paddles'
  brass before the petals.
- **Level labels count the stage from 1**: `PetalPop.StageOrdinal` (`Sides - SidesStart + 1`)
  is what the HUD, the level shout and the cards show, so the run reads 1-1 .. 5-8; `Sides` stays the geometry.
- **A fresh ball shows its aim**: the serve angle is drawn when the ball is SERVED rather
  than at launch, and a row of dots runs from the ball along `PetalPop.ServeAim` — the same property `Launch`
  fires along, so the picture cannot promise a direction the launch does not take. ⚠ `Restore` must call
  `SpawnServeBall(aim: false)`: drawing a fresh angle there would advance the RNG past the snapshot and the
  restored game would diverge from the frozen one.
- **Paddles shrink across the run**: half-length runs `PaddleHalfLengthFirst` (0.16 at
  4-1) to `PaddleHalfLengthLast` (0.09 at 8-8) linearly over the 30 level ordinals, and the layout cache is
  keyed on (N, half-length) because `PosMax` and the paddle's angular footprint follow it. Thickness is a
  flat `PaddleHalfThickness` (0.022), half its first-cut value. ⚠ The collider reaches that far off
  the spine as well, so thinning the paddle thins what the ball hits — the renderer must never carry a
  thickness of its own.
- **The paddle is STRAIGHT and LEANS toward the middle** (replacing the scalloped paddle
  that solved the same problem: balls getting stuck in the corners). `PetalPopLayout.PaddleTilt` turns the
  paddle `PaddleTiltFraction` (0.35, capped by `PaddleTiltMaxDeg`) of the way from facing straight out of its
  rail to facing the board's middle — **zero at the side's midpoint**, where it already faces the middle, and
  growing as it slides toward a corner. Both paddles meeting at a corner therefore turn their faces the same
  way, so the wedge that rattled a ball reads as a backboard aimed inward, and the lean self-scales with the
  side count (a square's deep corners lean about 13°, an octagon's about 6°). `PaddleSpine` is a straight
  segment rotated about the paddle's centre, so one end leads into the field and the other trails toward the
  gutter; the collider samples that spine and the renderer widens the same one. ⚠ Never the full angle — a
  paddle pointing straight at the centre would return every ball down its own approach line.
  `MaxEnglishDeg` (Breakout's tip-ward deflection) stays 0: on this board it sends a cornered ball deeper in.
- **A push at a corner SEALS it.** The rail target is the stick's projection onto the
  chord times a per-shape gain `CornerGain / sin(π/N)` (`PetalPopControls.RailTarget`): the bare projection
  from a corner direction is only sin(π/N), so on a square a diagonal push got each adjacent paddle 71% of
  the way and they never met. With the gain both land on the corner exactly, and a push at a side's midpoint
  still leaves that side's paddle centred. Both control models use it.
- ⚠ **The paddle's travel limit is derived so its END lands on the corner exactly** (`PosMax = r(φ − β)`).
  Two neighbours pushed to a shared corner seal it. PetalPopProbe asserts the identity bitwise, which is why
  the spring snaps to its target below 1e-6 rather than converging forever.
- **Disc twist AND punch are an interface seam** — `IArcadeGame.DiscTwist` / `DiscPunch` defaults, applied by
  `ArcadeControl.OnRender` as one rotate + scale about the centre. Both values are sim-owned and decay to
  EXACTLY 0 / 1, so a still game pushes no transform.
- **Hit-stop is a sim time scale, never a frame freeze.** `PetalPop.TimeScale` is DERIVED (0.15 while
  `HitStopLeft` runs, 0.3 while the last ball flies the gutter, else 1), so it is exactly 1 the instant the
  timer ends and a snapshot mid-impact resumes mid-impact. Paddles and phase clocks step in REAL time, so
  controls stay crisp through slow-mo.
- **One petal per ball per substep.** A ball centred on a seam would otherwise pop two petals and reflect
  twice, straight back the way it came. Discrete overlap tests suffice: at the smash ceiling a step moves
  the ball a third of a paddle's thickness. Raise `PhysicsSubsteps` before adding swept tests.
- **Smash (✕ during play) is early-press only.** It arms every paddle for `SmashWindowSeconds`, then a
  cooldown; a contact while armed multiplies speed (with an absolute floor, `SmashMinBoost`, so a slow ball
  still bursts), adds **+punch** to the ball (see below), pays `SmashScore`, and kicks
  the disc. A whiff costs only the cooldown. ✕ in Serve launches instead — the phases never overlap.
- **Cues are flags drained once per rendered frame**, as everywhere — nine balls on five paddles can all
  bounce in one frame, and the flags collapse that into one tick.
- **Presentation clocks are sim state, transient**: ball trails (a ring buffer per ball), shards, bursts,
  squash timers, the score pulse, the shout. None serialize; `Restore` zeroes them. Gem rolls and the
  life-lost twist sign are the ONLY RNG consumers, so a pop can never change the next drop.
- **Snapshot version 3** (version 1 was the fixed four-ring pentagon, version 2 the single-shape per-level flower) discards, never
  migrates, on a version it doesn't know; validation runs before any mutation (finite, ranges, `Sides` within
  the stage range and `Level ≤ Sides`, `Hp` sized and bounded by that stage's flower, ≤ 9 balls with a live
  speed inside 1.5× the smash ceiling, N
  paddles within ±PosMax, `Rng ≠ 0`). ⚠ The stuck-live-ball check applies only to phases that KEEP their
  balls; a Serve snapshot's parked ball is legitimately still and is respawned rather than restored.
- Tint `#966E1E` (the amber swatch base), glyph `FlowerPoppy`; `Assets\arcade\petalpop-preview.png` is the
  supplied hub still. Probe: `dotnet run --project tools\PetalPopProbe`.

## Internode (`internode`, Sep 2026)

A half-pipe runner seen from behind, the Sonic 2 special stage's shape with none of its theming (kept neutral
and abstract for now; theming may return). Full record in the Internode design specification (development repository only); the
framework-level facts:

- **Terms.** The player character is the **bike**; **runner** means the same thing (the code says `_runner`,
  `DrawRunner`, `RunnerArt`). The collectables are **tokens**. "Orb" is retired: it survives only inside
  identifiers (`_orbsHeld`, `OrbRadius`, `BonusMinOrbs`, `MaxBurstOrbs`, the `orb-*` phrase ids), and in older
  design records (not in the public source), where it means a token.

- **The disc IS the pipe's mouth.** The cross-section is concentric with the playfield; the runner's state is
  an angle θ around it (0 = bottom, +right = screen right at 6 o'clock), a height above the surface, and a
  forward distance. Steering is a **pendulum torque model**, not a rate — Internode does NOT use `ArcadeAim`
  (same call Petalpop made): gravity pulls toward the bottom, holding a wall never holds you there, and
  enough angular speed carries the runner **over the rim (±150° at the base) and across the open top** as an
  angular wrap with constant ω. Damping is release-scaled (`ReleaseDampingPerSec`): a released stick stops
  coasting, a held one keeps the crossover. There are no events past the base rim.
- **The pipe at rest is a true half-pipe** (rim 100°: the lower 180° plus a small curl-in lip, which is what tilts
  a launch inward so a fast runner arcs across the top), and the course only ever CLOSES it, from stage 2. Crossing
  phrases charge from the floor up the whole wall — there is no room high on a wall to build the speed.
- **The opening closes along the course — but the half-pipe is the common case.** A sim-owned per-section **rim
  profile** (`InternodeRim` closures, `RimAt(zAhead)`: a plateau at one of three tunnel designs, 125°/150°/180°,
  between two smoothstep ramps) raises the rim from the base 100° toward 180° (a closed tube, no rim, θ wraps),
  and the probe holds the closed share of the ride from stage 2 inside 30–50%. This one DOES reach the physics: `InternodePhysics.Integrate` takes the rim at
  the runner's z as a parameter, and a runner a shrinking rim overtakes is set down on it with ω unchanged
  (never bonked, launched or teleported). The sequencer places a closure only in a stretch no phrase that NEEDS the open top touches (plus
  0.5 s either side) — every other phrase is marked `RimSafe` and has passed the oracle with flight forbidden,
  which is exactly what lets the oracle keep integrating at the base rim; the gate is always approached at the base. The renderer's ring arcs, seams
  and gate ring follow `RimAt` per station.
- **z is seconds of travel and there is no speed knob.** Forward speed is 1 s/s by definition, so a phrase
  authored at `Dz = 2.0` arrives in two seconds on every machine; difficulty compresses phrase spacing
  (`PhraseTimeScaleStart` 0.5 at stage 1, floor `PhraseTimeScaleMin` 0.45), never speed. The renderer owns the one depth-per-second constant.
- **Formations are an authored phrase bank** (`InternodePhrases`), chained by entry/exit position class and
  tier-sequenced by `InternodeSequencer` from the run RNG. `InternodeReachability` is a lattice oracle that proves
  every phrase collectible and every mine avoidable from every entry state at both time scales — **it runs in
  the probe only**, and it calls the game's own `InternodePhysics.Integrate`. ⚠ Venturi's validator integrated
  with a different constant than its craft; don't let a second integrator appear.
- **Only one section lives in memory.** The snapshot carries `(Stage, Section, SectionSeed)` and rebuilds the
  section's events, bends and twists on restore, marking passed planes; it stays under 1 KB.
- **The quota derives from content**, `round(QuotaShare(stage) × PerfectValue(section))` per section
  (`PerfectValue` is the tokens on offer plus the extra payout of every gold prize), cumulative
  on both sides so carry-over is automatic and the bank can change without a table going stale. A mine costs
  held tokens (floored and capped, see **A loss is floored and capped** below) with **no stun** — steering is never taken away.
- **Bends and twists are sim data with no physics effect.** `BendAt(z)` (2-D) and `TwistAt(z)` are read by
  the renderer, which integrates the bend twice (heading, then displacement, in the runner's frame, so the ring underfoot never moves) into a vanishing-point swing and rotates each ring by the twist
  relative to the runner's depth (the corkscrew ahead), plus the whole world by the twist at the runner (the
  screen rolls through a twist, as in the reference). Gravity is always toward the pipe's own bottom.
- **Wall grip.** A held stick banks 1.3 s of grip; released, gravity stays off for 0.6 s and then eases in,
  so the runner stands on the wall and slides. It holds a GROUNDED runner only and a jump zeroes it, so a leap from either wall falls toward the bottom; the release damping is grounded-only too, since in the air it braked a jump into a hover (`GripSeconds`,
  `GripFadeSeconds`, `GripHoldThreshold`; `GripLeft` is runner state, snapshot version 4).
- **The open top is AIR, and the ring is a one-sided wall.** Leaving the surface is unconditional; across the top
  the runner is a free particle under `FlightGravity`, so a runner who barely clears the rim falls straight through
  the middle to 6 o'clock, while one with speed presses against the ring and slides over it to the far wall. The
  crossing threshold is emergent, not a constant. ⚠ Do not reintroduce an angular model up there: a bead on the
  ring has almost no pull at 180° and floats.
- **Tokens may sit in the gap.** Up there height means nothing, so a token is collected on angle alone and a
  formation can pay for the flight; mines stay on the surface, since one in the gap would be unavoidable.
- **The airborne pull grows with |θ|, not sin θ.** `AirHomingDegPerSec2` is what stops a jump taken high on a wall
  landing back on the same spot: the pendulum's own term fades above 90°, so a wall jump had nothing bringing it
  home. It is angular only, so jump height is identical everywhere on the pipe, and it does not act in the gap.
  ⚠ It is a spring, so the air needs its own near-critical damping (`AirDampingPerSec`) — undamped it flings the
  runner past the bottom to the far wall instead of settling them at it. ⚠ A held stick relieves
  `AirControlRelief` of BOTH the spring and its damping (the pull stops passive drift; it does not veto a
  commitment), which is what buys real air control: hands off is unchanged, a hard hold crosses the pipe. ⚠ The
  relief FADES across the descent to a balance DERIVED from the steer torque and the pull at the runner's own
  angle, so by the end of the arc a full stick buys no more speed — early commitment travels, late commitment does
  not. ⚠ An airborne arc can only drift INWARD;
  a formation that climbs the wall in mid-air is not reachable.
- **Gaps in the floor rewind the section.** A break spanning every angle; crossing it on the ground costs the mine
  share of tokens, plays the drop with Z frozen, and puts the runner back at the start of the SAME layout with every
  token restored (no gate was passed, so quota and colour stand). ⚠ Gaps are RESERVED in the phrase chain at a return
  to Centre with clearance either side — a dense section has no natural hole — so jumping one never costs a token.
- **The open top is a free fall, not a pull.** Across the gap the runner is a free particle under
  `FlightGravity` with the ring as a one-sided wall — no homing term at all, so the crossing threshold is
  emergent: barely clear the rim and you drop through the middle, carry speed and you press the ring and slide
  to the far wall. A crossing needs speed (about 180°/s at the base rim). ⚠ **Nothing stalls up there** —
  gravity acts on every step of the case, and the harness sweeps 51 crossings from a crawl at the lip to full
  ω, hands-off and under a full stick each way, and every one comes down (the slowest reaches 179.9° and still
  resolves in 1.14 s). Don't reintroduce a homing share to "stop floaters": there are none to stop, and a
  share strong enough to matter makes the top a wall no crossing can pass. ⚠ No extra damping up there either
  — it would sap the speed a crossing was earned with — and the high-wall entry bands are tight on outward
  speed because a runner sliding off the rim slowly now falls back before any crossing phrase pays.
- **One jump, and a tap is a small hop** (the double jump is gone). A held press reaches
  `JumpApex` (0.728 radii, the height the old stack reached) over `JumpSeconds` (0.88 s); releasing while still rising
  clips the climb to `JumpTapFraction`, so the same button gives a 0.08-radii hop or the full arc. Every jump gets
  the jet burst and the one jump sound. A press in the air is the landing buffer, except for `CoyoteSeconds` (0.07)
  after stepping off a lip. The oracle explores held presses only — a tap is strictly less reach, so a pass stays
  sound. ⚠ The shortest hop must keep clearing `MineClearHeight`, or a tap becomes a trap; the probe asserts it.
- **Mines can be a WALL.** `InternodePhrases.Ring` spans the surface at a spacing narrower than a mine's own width, so
  the blocked bands overlap and jumping is the only way past; cutting a window out of a ring leaves one corridor to
  thread. ⚠ Each event kind is bounded by its OWN half-width (a mine may sit closer to the rim than a token) —
  that is what lets a ring actually close.
- **There is no failure state.** A missed quota holds the run at the same section — the quota stands, the layout
  re-rolls, and the player keeps collecting until they pass — so `IsGameOver` is permanently false and the host
  freezes the game rather than clearing its snapshot (the same posture as Kabloom's campaign). ⚠ The quota must
  never rise on a retry. Passing a gate advances `PipeTheme`, and the renderer takes the next of six checker
  pairs from it, so progress reads in the ground itself.
- **Turns occlude.** Bends are axis-aligned turns (1..2 per section, the `Turn*` knobs); the renderer clips
  everything beyond a ring boundary to that boundary's disc, so the pipe bends out of view and tokens behind a corner
  stay hidden until it straightens. Twist groups appear in `TwistChance` (0.3) of sections; each of a section's two closure slots fills with
  probability `RimClosureChance` (0.8) where a crossing leaves room.
- **Mine walls rotate and stack from stage 3.** `InternodePhrases.Rings` lays rings with the door turned further
  each ring: `doorway` stacks three rings too close to jump as one, `helix` turns the door round, `vortex` (tier 4)
  does both. The oracle proves every one from every entry.
- **The sky is black where the pipe vanishes** and lightens outward, centred on the far station, so the far end of
  the pipe is continuous with the sky instead of a floating disc.
  ⚠ **That gradient is painted OVER the tiled sky art, so its outer stops have to fade to transparent** —
  fixed Sep 8 2026, having never worked. `SkyDeep`/`SkyEdge` come from `Rgb()`, which is fully opaque, so the
  wash covered every pixel of the disc: `internode-sky.png` was resolved, tiled, panned by the camera turn
  and then buried, and the symptom was a sky that simply never rendered. Only the black core is load-bearing
  (it is what sinks the vanishing point); outward of it the gradient is a wash and must let the art through.
  With no art the same gradient IS the sky and stays opaque, which is why the stops are chosen per frame off
  `_skyArt`. **A palette colour used as a wash over something else has to be given an alpha at draw time** —
  `Rgb()` never carries one.
- **Every live token and mine carries a GLOW**: blue behind an ordinary token
  (`TokenGlow`, the token's own cyan), gold behind a gold one (`BonusGlow`), red behind a mine (`MineGlow`, the
  colour of its tips, not its pearl body). It is the LONG-RANGE channel — what is coming, from far enough
  away to act on it — where the sprite is the short-range one that says what shape it is.
  - ⚠ **It BRIGHTENS on approach, and the ramp axis is SECONDS REMAINING — not screen position.** The first
    cut lifted opacity with the sprite's own darkening, so it sat at 0.98 four seconds out and then *fell* to
    0.55 on arrival: full blast at exactly the distance a hint was wanted (too bright at
    far distances). Now the token halo runs `TokenGlowFar` 0.06 → `TokenGlowNear` 0.38 across `GlowRampSeconds`
    5.0, curved by `GlowRampExponent` so the EARLY approach stays quiet; the mine halo runs the OTHER way,
    `MineGlowFar` 0.78 → `MineGlowNear` 0.07 (`MineGlowRampExponent` 1.15), because a hazard's warning is only
    actionable early. Current numbers live beside `Glow` in `InternodeRenderer`.
  - ⚠ **Screen position is unusable as that axis**, and it is worth knowing why rather than rediscovering it:
    perspective crushes it, so the share of the way from vanishing point to runner stays under 0.05 from four
    seconds out to one second out and then races to 1. A ramp on that reads as a glow doing nothing and then
    popping. Seconds are what a player experiences an approach in, and they are linear.
  - **Spread is per KIND**: `TokenGlowSpread` 1.7 HUGS the token — the original shared 2.6
    read as a light source sitting on the track rather than as the token being lit — while `MineGlowSpread`
    sits at 2.15, because a hazard wants its warning to arrive before its outline does.
  - **The token glow is not the token's turquoise** (now WHITE, `TokenGlowColour` `#FFFFFF`,
    carried at a fraction of the alpha a colour would need). Navy `#2E4FB8` was the first step away:
    borrowing `OrbColour` put a cyan halo round a cyan token and the pair washed together into one bright smear,
    and the deeper hue also sits further from the checker tones, so it reads as light cast on the track rather
    than as more track. ⚠ Don't go darker — it has to stay luminous against a near-black far pipe, and a true
    navy stops reading as a glow at all.
  - ⚠ `GlowMinPixels` (5.0) is a screen FLOOR under the halo's radius, so it stops shrinking with its sprite.
    It BINDS EARLIER for tokens now that they hug: below about a 3 px sprite the floor takes over, so a
    distant token's halo is proportionally wider than a near one's. That is the floor doing its job, not the
    hug failing — and the opacity ramp has those distances at 0.1–0.25, so it buys a faint smudge rather than
    a wide bright ring.
  - ⚠ The floor made glows POP IN at the far plane, which the sprites never did — they were tiny there and
    eased in by growing. So the halo takes the **gate's** horizon fade, `(ZFar − zv) / FadeSeconds`, easing
    in across the far quarter of the draw distance.
  - Drawn BEFORE `Contact`, so the contact shadow still reads on top: that shadow is how a player tells where
    a body sits on the wall, and washing a halo over it trades a gameplay read for a decorative one.
  - **A MISSED token gets none** — it is spent and drained to grey, and a halo saying "collect this" around
    something uncollectable is worse than no halo. A **gold-candidate** token keeps the BLUE glow: its body is
    still blue and the gold ring is what marks it, so a gold halo would claim a prize it has not yet become.
  - One frozen `RelativeToBoundingBox` brush per colour serves every size; up to `MaxSprites` (576) can be in
    view, and a per-sprite absolute gradient would churn a brush per sprite per frame.
- **Gold means a perfect pattern, and nothing else on the board is gold but the gate.** The last token of a phrase of
  four or more (shown ahead with a gold ring) TURNS gold when every token before it was caught (`TurnLastGold`), and pays ten (`BonusTokenValue`);
  the sequencer never places gold. Elevated tokens shift cyan → violet with height, never toward gold. A catch pops a
  small ring at the runner; a token that gets past is greyed as it flies by (`Missed`, `Cue.Miss`, silent), so a
  miss is read on the token rather than deduced from the count.
- **The banked tokens fly to the counter and are swallowed by it.** `BankOrbStartScale` now
  0.5 × (the 3.6 × "fly BIG" take was pulled back; the measurements below are from that take) — originally 3.6 ×
  the readout's own pictogram leaving the runner, down to exactly 1 × on arrival — 21 px to 5.8 px radius on
  the 100% disc, against a flight that used to run 6.4 px → 4.7 px and read as a few specks crossing the disc
  rather than a hand of tokens being cashed in. `BankOrbShrinkExponent` 2.6 lags the shrink behind the flight,
  so the collapse happens AT the readout: still ~18.5 px at the flight's halfway point, 8 px at 85%. A linear
  shrink spends it on the way over and the token arrives as an afterthought.
- **Gates bank.** Held tokens (`Tokens`) reset at every gate and join `Score`, with a staggered flight of tokens into the
  readout during the empty section lead; the quota is per section and asks for a share (`QuotaShare`: 60% at stage 1,
  rising linearly to 98% at stage 100) of the tokens PLUS nine per gold prize on offer (`PerfectValue`), so a stretch full of
  prizes asks for more than its token count. A mine or a fall costs held tokens only; the score never goes down.
- **A loss is floored and capped, and an empty hand costs the stage.** A mine costs `MineLossShare` of the held tokens,
  never under `MineLossMinTokens` (10) nor over `MineLossCapTokens` (40), clamped at zero; the lost tokens spray in a
  fan, settle on the track and are carried off screen at full size. A break in the floor is a SINK for a runner who STEPPED off its lip: for `CoyoteSeconds` they
  can still jump out. A runner who comes down inside a break with the jump spent falls at once — a mistimed jump is
  not survivable. A mine with
  nothing held, or a committed fall, DEMOTES: back one section (2-3 → 2-2, 2-1 → 1-3; clamped at 1-1) with the hand empty, onto a fresh section;
  the score never moves. In a fall the camera skids to a stop over `FallCoastSeconds` while the bike keeps its speed,
  so it shoots on down the pipe, shrinking, as it drops through the floor, drawn behind the track; then it demotes.
- **The ride inches faster per stage.** `ViewSpeedNow` adds `ViewSpeedPerStage` (0.6) per stage to the perceived
  speed, capped at `ViewSpeedMax`; arrival times are still the sim's. The runner sits near the mouth (`LeadKz` 0.35).
- **Sprites draw one ring late.** A sprite standing on band i reaches outward into the annulus of the nearer band,
  which is painted after it, so drawing a band's sprites right after its own ring left far tokens "clipped" by the
  floor in front of them; each ring now paints and then the sprites of the band BEHIND it. A runner falling through
  a gap is drawn in that same depth order, at full strength, so nearer track slices occlude them instead of the
  runner sitting on top of the floor they fell through. Contact shadows are crisp dark blobs cast straight onto the
  nearest wall under the runner, every token and every mine.
- **Screen rotation composes two sources — 1/6 of θ (shaped to 0 through the rim wrap) and the course
  twist — and one pause row, CAMERA ROLL, removes both. Until the player picks it, it is OFF under Reduce Motion and ON otherwise**, and the drawn
  roll is rate-limited to `RollMaxDegPerSec` so a fall through the centre cannot flick the frame.
  Reduce Motion reaches this row only as its default (MOTION-INVENTORY §Arcade); the row is the game's own
  accommodation.
- **Perceived speed is a renderer constant.** z is seconds of travel and arrivals never change, so the checker
  `InternodeTuning.ViewSpeed` (9) compresses the depth axis, `ViewHorizonSeconds` (6 s) sets draw distance INDEPENDENTLY of it (a fast board still sees far), `ViewBandSeconds` the checker rate; the sim compresses arrivals from `PhraseTimeScaleStart` (0.5). ⚠ **Perceived speed, arrival rate and the player'"'"'s own quickness are three independent knobs, and the oracle ties the last two together**: at the shipped angular constants 0.5 is the tightest arrival rate the authored bank proves at, and 0.35 fails 51 of 310 runs. Compress arrivals further only by scaling the angular physics with it or re-authoring the bank; a real
  speed change is a separate decision. Thin-ring collapse (rings under 1.5 px skipped, sectors under 2 px
  drawn as one annulus) keeps the extra far rings free.
- Tint `#56528C` (the night-indigo swatch base, the blue/violet family the other three don't use); the hub still
  is `Assets\arcade\internode-preview.png` when supplied. Probe: `dotnet run --project tools\InternodeProbe` (development repository only, not in the public source).

## Drop-in script games (engine M1, Aug 16 2026)

Consented packages from `%APPDATA%\Radiata\Packages\Arcade Games\` play in the same round window as the
built-ins. Security posture and package format: [PACKAGES.md](PACKAGES.md); the folder's README.txt is the
author-facing JS API reference (kept current by `PackageStore.EnsureFolders`).

**The sim runs OUT OF PROCESS.** `Radiata.ArcadeHost.exe` (Jint, strict, no CLR, string compilation
disabled) runs inside a zero-capability AppContainer under a Job Object (128 MB, one process,
kill-on-job-close); Radiata talks to it over a named pipe whose ACL names exactly the derived container
SID + the current user. (The exe has one unrelated second duty: `--xinput-count` is the read-only
observed-isolation probe for the Bluetooth Xbox cloak — launched directly by Radiata, no jail, no pipe,
no script engine; see [INPUT-CAPTURE.md](INPUT-CAPTURE.md). It exists because the helper is a separate,
non-HidHide-allow-listed image, so its XInput view is exactly a game's.) One request per RENDERED frame carries the input snapshot and the elapsed sim
time; the helper runs the fixed 1/120 s steps internally (≤12, zero = draw-only, which is what keeps
pause/ready/guard genuinely frozen) and returns a draw-command buffer that the host validates completely
before a pixel lands. The statement budget aggregates per rendered frame — never per step.

How it slots into the machinery in this doc:

- **`ScriptArcadeGame` (Core) is the `IArcadeGame`.** Catalog id = `pkg-<manifest id>` (never collides
  with a built-in), registered per run by `ArcadeCatalog.RegisterScripts` — same startup-only immutability
  as `Materials.RegisterCustom`. Construction is headless (the harness can instantiate entries); the
  helper launches lazily on the first rendered frame, from `Arcade\ScriptHost\ScriptSessionCoordinator`.
- **`ScriptGameRenderer` is the one renderer for all of them** (`ArcadeRenderers.For` matches the token
  prefix). It pumps the session once per rendered frame and replays the LAST VALIDATED buffer —
  polar→cartesian, the circular clip and every bit of trig are host-side, so a script never learns where
  the disc is.
- **Script games inherit every house rule for free:** ○ dismisses and △ opens the how-to card without the
  script ever seeing either; the input budget is stick + ✕/□ + d-pad edges; the guard card covers them;
  the disconnect invariant closes them; a frame-pump throw closes the arcade.
- **Freeze/resume is the KV store and nothing else.** On dismiss the host persists the kv mirror through
  the normal `ArcadeStore` slot (`Serialize()` IS the kv JSON) and the helper dies; reopening starts a
  fresh session and replays load + kv restore. Scripts are deliberately stateless across dismiss beyond
  kv — the README tells authors to design for it. The reserved key `hiscore` feeds the picker's best.
- **Cues are a fixed nine-name vocabulary** (`fire tick good denied kill zap hurt clear gameover`) mapped
  in `ArcadeControl.PlayGameCues` onto the `ArcadeSfx.Generic` bank via `Generic.Cue`; unknown names are silent. Boundary
  caps: 8 cues + 16 kv writes per frame, validated with the draw buffer.
- **Misbehaviour never reaches the player as a crash.** A script fault keeps the session (last buffer
  stays up, fault traced); a protocol violation, miss storm (30 consecutive) or 2 s silent helper —
  the host wall-clock kill, the only reliable stop for a catastrophic regex — kills and restarts the
  helper with kv intact, up to 3 times per open; after that the renderer draws a guard-style card
  (plain wording, ○ closes, no nudge). Teardown on dismiss and app exit; kill-on-job-close reclaims
  helpers even if Radiata dies hard.
- ⚠ **A dev build can't launch the helper from the repo path.** An AppContainer token is access-checked
  against every ancestor directory, and a path under the user profile (`%USERPROFILE%`) fails; the coordinator detects a user-profile base
  dir and stage-copies the helper + Jint to `C:\Radiata-ArcadeHost-<hash>` with RX for the SID (traced,
  cleaned up at exit). An installed build under Program Files launches in place.

## Not built yet, deliberately

- **The starter-wheel slice.** `BuildStarterWheels` could seed the Arcade picker on wheel B now that there's
  a real set behind it. The old objection is gone — Arcade left beta Aug 26 2026 and is on out of the box —
  so this is now purely a question of what belongs on a starter wheel.
- **More games.** Concepts still held, each needing a round-native hook before it's worth building:
  Super-Hexagon-style dodge, flappy-style, virtual pet, idle clicker, WarioWare-style reaction timer,
  par-1 minigolf. The tube flyer was built once as Venturi (deleted Aug 8 2026) and the half-pipe runner is now
  **Internode**; git history still has Venturi's tunnel generator and reachability validator if a pure tunnel wants them.
- No leaderboards, no achievements, no cloud anything.
