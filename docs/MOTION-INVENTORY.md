# Motion inventory (Reduce Motion)

Every animation or continuously changing visual is classified as one of:

- **essential**: the information must survive; the motion may not (it becomes a static or stationary form).
- **transition**: may become instant or opacity-only.
- **decorative**: stops entirely.

**New motion must be classified here and must ask `MotionPolicy` instead of adding its own switch.**

## The policy

`Core/MotionPolicy.cs` is the one answer to "may decorative motion run right now?". The effective value is
`MotionPolicy.Reduce`: the saved `system.reduceMotion` setting or the Windows "show animations" preference
(`NativeMethods.SystemAnimationsDisabled`), either one reduces. `App.ApplyMotionPolicy` pushes the value to every
surface. A consumer that caches the value or holds running animations must subscribe to `MotionPolicy.Changed`
so a live flip stops loops already running. `MotionPolicy.TransitionMs` is for movement, scale and travel only;
opacity-only fades keep their duration and are not run through it.

## Wheel (`Overlay/`, `Core/WheelStateMachine.cs`)

| Effect | Class | Under Reduce Motion |
| --- | --- | --- |
| Intro fade, drift and scale | transition | Opacity-only fade in place (`ReducedIntro`) |
| Cancel and fire fade-outs, selected-slice linger | transition | Kept, opacity-only |
| Add-picker drill-in and drill-out zoom | transition | Opacity-only (`PickerDrillIn`) |
| Edit slide-to-centre, ring reflow, landing settle | transition | Instant (`SlideToCenter`, `BeginReflowClock`) |
| Hub appear and disappear scale | transition | Snaps (`HubPresence`) |
| Guarded-dwell arc, fill ring and body-opacity ramp; hold-to-delete fill | essential | Kept as a stationary progress fill |
| Material-specific dwell animations (glass recolour, pours, crescent, magnet-lock) | decorative | Replaced by the standard arc (`ownDwellAllowed`) |
| Kawaii hub twinkles, arm glitter, fire confetti; Reactor sparks and board parallax; Salvage glow flicker, tube-noise crawl and fire sparks | decorative | Stopped or never drawn (`TickKawaiiFx`, `DrawKawaiiGlitter`, `SpawnFireFx`, `SalvageGlowFlicker`) |
| Armed-slice lift and grow (Mesa, Kawaii), Reactor hub fuse | essential | Kept as static treatments |
| Arcade collapse (slices sweep behind the hub, hub grows) | transition | No travel (`BeginArcadeCollapse`) |
| Arcade resume READY beat (375 ms, rendered but not simulated) | essential | Kept; it is a hold |

## Game Grid (`GameLibrary/GameBrowserControl.xaml.cs`)

| Effect | Class | Under Reduce Motion |
| --- | --- | --- |
| Kawaii sparkle field, Reactor selection sparks | decorative | Not built or stopped, live (`BuildKawaiiSparkles`, `RetargetSelectionSpark`) |
| Reactor board parallax | decorative | Pinned centred (`UpdateBoardParallax`) |
| Focus shear travel | transition | Hard-set |
| Cover-cycle loading spinner | essential (the loading state), decorative (the rotation) | Scrim and arc stay; rotation off (`GameTileVM.Spin`) |

## Onboarding (`Setup/OnboardingWindow.xaml.cs`)

The practice step drives the real wheel, so every wheel row applies there too. The decorative loops (tray-flash
arrow bob, "add your first game" pulse, enable-card chord pulse, chord illustration cycle) are static or not
started (`ShowTrayFlashWindow`, `ApplyAddGamePulse`, `StartChordCycle`); the practice filmstrip slide and
toggle glide snap. Loops read the policy at their start, so a flip mid-loop stops them at the next step or
state change.

## Settings and chrome

The material preview tiles are static. The icon-picker fetch-logo spin (`FetchLogo_Click`), the About flower
bloom (`AboutFlowerControl.Play`) and the flower-mark blooms on toast and alert cards (`FlowerMarkControl.Play`)
are decorative and appear fully formed under Reduce Motion; the card or toast itself, which carries the
information, is unchanged.

## Arcade

What a game draws on `ArcadeControl`'s `CompositionTarget.Rendering` loop is game content and is out of scope;
the policy does not change game animations. The arcade's own chrome is not exempt: the picker reads
`ArcadeControl.ReduceMotion`, pushed through `OverlayWindow.SetArcadeReduceMotion`.

| Effect | Class | Under Reduce Motion |
| --- | --- | --- |
| Picker carousel swing (damped spring) | transition | Snaps to the target cabinet every frame |
| Picker floor-grid drift, vanishing-point parallax, floor sparks, sky drift | decorative | Stopped; the picker still draws |
| Picker launch and return zoom, disc grow and shrink, window slide | transition | Instant |
