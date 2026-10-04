# Motion Inventory — Reduce Motion (A2) classification

The repository-wide inventory behind the Reduce Motion policy (accessibility item A2):
every animation and continuously changing visual, classified as **essential** (information must survive,
motion may not), **transition** (may become instant or opacity-only), or **decorative** (stops entirely
under Reduce Motion). "Gate" says how the policy is applied. **Update this table when adding motion** —
new effects must be classified here and must check `MotionPolicy` rather than adding their own knob.

The policy itself: `Core/MotionPolicy.cs` — effective Reduce = saved `SystemConfig.ReduceMotion` OR the
Windows "show animations" preference (`NativeMethods.SystemAnimationsDisabled`, re-probed on
`UserPreferenceChanged`). App pushes the effective value everywhere (`ApplyMotionPolicy`).

## Wheel (`RadialMenuControl` + `OverlayWindow`)

| Effect | Driver | Class | Under Reduce Motion |
| --- | --- | --- | --- |
| Intro fade + drift + 0.85→1 scale | BeginAnimation, one-shot | transition | Opacity-only fade in place (`ReducedIntro`) |
| Cancel / fire fade-outs | BeginAnimation, one-shot | transition | Kept — opacity-only |
| Selected-slice linger (160 ms hold, then fade) | BeginAnimation, one-shot | transition | Kept — a hold + opacity fade, no travel |
| Picker drill-in/out zoom (0.45↔1) | BeginAnimation, one-shot | transition | Opacity-only (`PickerDrillIn`/`Out` gates) |
| Edit slide-to-centre | BeginAnimation, one-shot | transition | Instant reposition (`SlideToCenter` gate) |
| Edit scrim fade (0→0.35) | BeginAnimation, one-shot | transition | Kept — opacity-only |
| Edit ring reflow (200 ms) + landing settle (220 ms) + ghost fade + pop-in | 30 ms confirm timer | transition | Clocks never start — structural edits are instant (`WheelStateMachine.BeginReflowClock`/`StartLanding`) |
| Hold-□ delete dwell fill | 30 ms confirm timer | **essential** | Kept — stationary progress fill |
| Standard guarded-dwell arc + red fill + body-opacity ramp | 30 ms confirm timer | **essential** | Kept — the stationary progress treatment everything falls back to |
| Pearl/Obsidian guarded-dwell glass recolour (resting → confirm red) | 30 ms confirm timer | decorative (info duplicated by the arc) | Replaced by the standard body-opacity ramp (`ownDwellAllowed` gate) |
| Kawaii/Mesa pour dwells, Salvage crescent grow, Reactor magnet-lock split/swirl + 240 ms snap shake | 30 ms confirm timer | decorative (info duplicated by the arc) | Replaced by the standard arc (`ownDwellAllowed` gate) |
| Kawaii ambient hub twinkles (1.5 s loop) | 30 ms confirm timer | decorative | Stopped, and stops invalidating (`TickKawaiiFx` + `DrawHubTwinkle` gates) |
| Kawaii arm glitter — a 300 ms sparkle sweep across a freshly armed wedge | per-render, wall clock | decorative | Never drawn (`DrawKawaiiGlitter` call gated) |
| Salvage crescent glow flicker — the "cheap fluorescent" dip, continuous | per-render, wall clock | decorative | Held at full (`SalvageGlowFlicker` returns 1.0) |
| Salvage tube noise — nine grain dots crawling along the crescent | per-render, wall clock | decorative | Grain drawn hashed in place, no crawl (`DrawSalvageTubeNoise` freezes its clock) |
| Reactor circuit sparks (while armed) | 30 ms confirm timer | decorative | Stopped, and stops invalidating (`TickKawaiiFx` gate) |
| Reactor board parallax + armed-glyph drift | per-render from stick | decorative | Pinned at zero offset (boards still draw) |
| Kawaii fire confetti / Salvage spark bloom | 30 ms confirm timer, one-shot | decorative | Never spawned (`SpawnFireFx` gate); live enable clears in-flight particles; confetti-hold park poll never starts |
| Hub appear/disappear scale (170/85 ms) | 30 ms confirm timer | transition | Snaps (`HubPresence` gate) |
| Mesa/Kawaii armed lift+grow, Reactor fuse ripple | per-render, state-driven | **essential** (selection emphasis) | Kept — static armed treatments, no continuous motion |
| Custom theme armed lift+grow (package `armed` block, clamped ≤24px/1.15×) | per-render, state-driven | **essential** (selection emphasis) | Kept — same class as the Mesa/Kawaii lift above |
| Arcade collapse: slices sweep behind the hub (150 ms each, 22.5 ms ring stagger) + the hub grows to the playfield (130 ms) | 16 ms render timer (`BeginArcadeCollapse`) | transition | No travel — hub already grown, slices already away, callback one turn later |
| …and the wheel-wide backglow ducking out over 100 ms with it, back in over 125 ms once the game is up — re-radiused to the GROWN HUB, the disc it now sits under | same timer (`CollapseGlowFadeSeconds` / `CollapseGlowRiseSeconds`) | decorative | Never ducks; the glow simply moves to the hub radius with everything else |
| Arcade resume ready beat: the board renders but does not simulate for 375 ms | `ArcadeTuning.ResumeReadySeconds` | **essential** (fairness — you'd resume mid-collision) | Kept; it is a hold, not motion |
| Topmost re-assert 400 ms timer | timer, non-visual | n/a | Kept |

## Game Grid (`GameBrowserControl`)

| Effect | Driver | Class | Under Reduce Motion |
| --- | --- | --- | --- |
| Kawaii sparkle field (12 stars, forever Storyboard) | Storyboard | decorative | Not built / stopped live (`BuildKawaiiSparkles` gate + live setter) |
| Reactor selection sparks (33 ms comet timer) | DispatcherTimer | decorative | Stopped (`RetargetSelectionSpark` gate + live setter) |
| Reactor board parallax (260 ms eased shear) | BeginAnimation | decorative | Pinned centred; hard-set, no eased travel (`UpdateBoardParallax`) |
| Focus shear travel (`SetShear` animate) | BeginAnimation | transition | Hard-set |
| Cover-cycle loading spinner | XAML Storyboard (forever while loading) | **essential** (loading state) / decorative (rotation) | Scrim + arc still show; rotation gated off (`GameTileVM.Spin`) — live flip refreshes mid-fetch tiles |
| Navigation dot row hold-then-fade | 33 ms timer, self-terminating | transition | Kept — opacity-only fade, stationary |
| Prefetch / tip auto-hide / hide-hold timers | timers, non-visual or stationary fill | essential / n/a | Kept (hide-hold fill is stationary progress) |

## Onboarding (`OnboardingWindow`)

The practice step drives the REAL wheel through hooks, so every wheel row above applies there automatically.

| Effect | Driver | Class | Under Reduce Motion |
| --- | --- | --- | --- |
| Tray-flash arrow bob (±10 px forever) + opacity pulse | BeginAnimation forever, 4.2 s window | decorative (the pointer itself is essential) | Static arrow for the same 4.2 s (`ShowTrayFlashWindow` gates) |
| CardTray colour-mark bloom, then the dark glyph rising 0 → full (1.4 s, eased) once the flight lands | `AboutFlowerControl.Play` / `BeginAnimation`, one-shot (`FadeTrayGlyphTo`) | decorative | Neither runs: the colour mark never appears and the card wears its dark glyph at full from the start (`TryHighlightTrayIcon` gates) |
| Tray-flash FLIGHT — the CardTray colour flower peels off (0.5 s hold, swelling to 1.18×), then an 0.85 s bowed arc down to the tray icon, shrinking to fit | `MatrixAnimationUsingPath` + two keyframed `ScaleTransform` animations, once (`FlyVisualToTray`) | decorative (the flash landing on the tray is the essential part) | No flight at all: a static red ring on the tray icon (`TryHighlightTrayIcon` gates) |
| "Add your first game" border breathing pulse | BeginAnimation forever | decorative (CTA outline is essential) | Steady outline (`ApplyAddGamePulse` gate) |
| Enable-card chord pulse (wheels disabled mid-round-trip) | BeginAnimation forever | decorative (state is essential) | Static half-strength success green (`StartChordPulse` gate) |
| Chord illustration cycle (1 s hold + 0.5 s cross-fade, loops) | DispatcherTimer | decorative | Not started — art holds still (`StartChordCycle` gate) |
| Practice filmstrip slide + toggle-thumb glide (300 ms) | BeginAnimation, one-shot | transition | Snap (`SetPracticeState` coerces `animate` off) |
| Advice-card cross-fade | BeginAnimation, one-shot | transition | Skipped with the slide (non-animate path sets text directly) |
| Status/preview poll timers (1 s / 0.5 s) | timers, non-visual | n/a | Kept |

## Settings & chrome

| Effect | Driver | Class | Under Reduce Motion |
| --- | --- | --- | --- |
| Material preview tiles / `WheelMiniature` | static | n/a | Already still |
| Icon-picker fetch-logo spin (650 ms forever while busy) | BeginAnimation | decorative (✓ confirms completion) | No spin (`FetchLogo_Click` gate) |
| About flower bloom/wilt (staggered spring scales) | BeginAnimation, one-shot | decorative | Appears/clears fully formed (`AboutFlowerControl.Play` gate) |
| Wheels-toggle flower toast bloom (`FlowerMarkControl.Play`) | BeginAnimation, one-shot | decorative (the toast itself is the essential feedback) | Mark shows fully formed; toast fade kept |
| Steam-sentry alert card's flower mark bloom (`ShowSentryAlert`, same `FlowerMarkControl.Play`) | BeginAnimation, one-shot | decorative (the card and its text are the essential warning) | Mark appears fully formed (`FlowerMarkControl.Play`'s own gate) |
| Isolation-lost warning card (`App.ShowIsolationToast` → the roundrect `ShowRectToast`, all `ShowCornerToast` notices) | card itself: none on entry; its flower mark blooms (`FlowerMarkControl.Play`); shared toast close | essential (safety warning); the mark is decorative | Card unchanged — appears fully formed by design; mark shows fully formed (`FlowerMarkControl.Play`'s own gate); opacity-only close kept. ⚠ `Play()` is **required, not decoration** — every piece is built with a 0×0 `ScaleTransform`, so a mark that is never played renders **nothing at all**. Don't "simplify" the call away to honour the no-entrance-animation contract; `Play()` already honours Reduce Motion itself |
| Discord-setup poll timer | timer, non-visual | n/a | Kept |

## Arcade

What a GAME draws on `ArcadeControl`'s `CompositionTarget.Rendering` loop is game content — explicitly out
of scope (the policy does not change game animations or any content rendered by the foreground application).
The arcade's own chrome is not exempt: the picker gates on `ArcadeControl.ReduceMotion`, pushed by
`App.ApplyMotionPolicy` through `OverlayWindow.SetArcadeReduceMotion`.

| Effect | Driver | Class | Under Reduce Motion |
| --- | --- | --- | --- |
| Picker carousel swing (critically damped spring, ~0.3 s to settle) | render loop, `ArcadePickerRenderer.Step` | transition | Snaps to the target cabinet every frame, so a live enable also stops a swing in flight |
| Picker floor-grid rows drifting toward the viewer | same | decorative | Stopped; the grid still draws |
| Picker vanishing-point parallax during a swing | derived from the swing's remaining travel | decorative | Pinned at zero (falls out of the instant swing) |
| Picker floor sparks riding the grid lines (the Reactor charge look, `ArcadePickerRenderer.DrawSparks`) | same | decorative | Neither advanced nor drawn |
| Picker sky drifting on its heading (Internode's starfield, tiled) | same | decorative | Held still; the sky still draws |
| Picker launch (✕): the chosen cabinet's screen picture lifts off, straightens and grows to fill the disc over `LaunchSeconds`, fading in over the first quarter from the screen's own picture; the game appears where it lands, no fade to black (`ArcadePickerRenderer.DrawDeparture`) | `ArcadeControl` launch timer | transition | Skipped — the game is made live on the frame ✕ lands |
| Leaving a game for the cabinets (○): the board just left shrinks from the whole disc into the front cabinet's screen, taking on the screen's lean, and fades over the last quarter onto the same shot already showing there (`ArcadePickerRenderer.DrawArrival`, `DiscShrinkSeconds`) | `ArcadeControl.StepPicker` on the render loop | transition | Not drawn — the cabinets are simply there |
| The disc grows from the launcher's size to a game's (×`ArcadeTuning.GameDiscScale`) over `LaunchSeconds`, and shrinks back over `DiscShrinkSeconds` when ○ returns to the cabinets; the wheel hub behind it follows frame-for-frame (`ArcadeControl.DiscRadiusChanged` → `RadialMenuControl.SetArcadeHubRadius`) | `ArcadeControl.StepDisc` on the render loop | transition | Snaps to the destination size, hub included (`SetDisc` still raises the event) |

- **Petalpop's shape-change spin** (the playfield turns 360° across the 1.6 s interlude when the board gains
  a side, the flower counter-turning inside it) is game content and exempt, like the rest of what a game
  draws. It has no opt-out of its own.
- **Internode's screen rotation** (1/6 of the runner's angle plus the course twist) is the one arcade motion with
  its own opt-in: the per-game pause row **CAMERA ROLL** (`Internode.CameraRoll`), **default = OFF under Reduce Motion, ON otherwise** until the player picks it, ON = screen
  rotation from both sources. Reduce Motion sets only the default — the row is the game's own accommodation, not Reduce
  Motion.

## Known remaining items

- The onboarding wizard's own decorative loops read the policy at their START sites; a Reduce Motion flip
  landing mid-loop (config-watcher echo, ~1 s) stops them only on the next step/state change. The wheel and
  Game Grid stop live.
- `FireFxReducedLifeMs` (confetti's 500 ms reduced life) is unreachable — superseded by no-spawn — and
  kept only as the fallback should confetti ever be spawned under Reduce Motion again.
