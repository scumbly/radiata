# Code atlas — every file, one line each

**Read this when:** you need to find *which file* owns a behaviour, or you're adding/moving/renaming a file
and want the right neighbourhood. [ARCHITECTURE.md](ARCHITECTURE.md) explains the *shape* (Core ↔ shell
boundary, config recovery); this doc is the flat map underneath it.

**Keep it updated:** when you add, delete, rename, or significantly repurpose a file, update its row here in
the same commit. A quick drift check: `git log --oneline --diff-filter=AR --name-only -20` and confirm new
files appear below.

*Last full review: 2026-09-30 (0.17 line).*

---

## Remediation additions

| File / tool | Responsibility |
| --- | --- |
| `Core/AtomicFile.cs` | Shared temp-write-then-move helper (`WriteAllText`/`WriteAllBytes`) for the plain atomic-replace pattern; sites needing a disk flush before the move keep their own `FileStream` implementation instead. |
| `Core/ControllerCaptureContinuity.cs` | Last connected family across transport loss; neutral Xbox retention bounded by existing disconnect grace and verified cloak; the bounded identity hold (`HoldNeutralForIdentity`, `IdentityHoldMs`) for a connected source whose devnode has not enumerated yet; the multi-pad confirmation window (`HoldForMultiPad`, `MultiPadConfirmMs`) before the guard un-cloaks. |
| `Core/CoalescedInput.cs` | Latest analog sample per dispatch segment; seals queued samples at digital/touch/connection boundaries. |
| `Core/FileRestoreTransaction.cs` | Journaled application-data replacements, in-process rollback, retained originals if rollback fails; no automatic crash-journal replay. |
| `Core/IHidHideService.cs` | Driver adapter boundary used by isolated ownership/failure tests. |
| `Core/OwnedPayloadCleanup.cs` | Containment-checked, hash-matched staged-helper cleanup; preserves unknown/modified files. |
| `Core/XboxStuckRootTracker.cs` | Judges a childless USB Xbox composite root on an injected clock (`Observe(nowMs, roots)` → Healthy / Settling / Stuck; `SettleMs` = 10 s from the first childless sighting). `XboxDeviceTree.StuckRoots` publishes the stuck ones; `App.UpdateStuckRootEpisode` owns the once-per-episode trace line and card. |
| `Core/PadCountChurnRule.cs` | The pad-count churn breaker's rule on an injected clock (`Observe(nowMs, guardOn, padCount)`, `ChurnTrips`/`ChurnWindowMs`/`ChurnHoldMs`, `Reset`): guard-ON edges inside the window latch a hold, a second latch holds for the session, every pad gone releases that one. No tracing, no timer; `App.PadCountChurnBreaker` adds the trace lines, the transient-id naming and the expiry timer. |
| `Core/PortableUninstall.cs` | Literal-path portable removal worker and validated hashed helper manifest. |
| `Core/StickNavigationRepeat.cs` | Clock-driven held-stick navigation independent of packet changes. |
| `Core/XInputSourceSelection.cs` | Legacy slot ordering with own-output exclusion and narrow ambiguity reporting; does not establish PnP identity. |
| `Core/XInputSlotObservation.cs` | Complete connect-window arrival/churn observation; unique arrival remains an inference. |
| `tools/TestHarness/T_Remediation.cs` | Isolated file, controller, ownership, persistence, audio, and dispatch failure regressions. |
| `tools/TestHarness/T_RpcFaults.cs` | Local named-pipe fragmentation, nonce, allocation-bound, read/write cancellation tests. |
| `tools/TestHarness/T_Payload.cs` | Opt-in isolated copy of the published helper, exact-file cleanup, unknown-file preservation and manifest-tamper refusal. |
| `tools/ConnateBenchmark` | Fixed-seed CPU/allocation and final-state digest comparison (development repository only, not in the public source). |

## Solution layout

| Project | Output / TFM | Notes |
| --- | --- | --- |
| `ControllerWheel.csproj` (root) | `Radiata.exe`, net8.0-windows, WPF+WinForms | The shell. `Compile Remove` excludes `Core\**` and `tools\**` from its glob. Version = `0.<ReleaseMinor>.<build.minor>.<YYMMDD>`; `build.minor` is bumped by the (untracked) `.git\hooks\pre-commit` hook and only *read* by MSBuild. |
| `Core\ControllerWheel.Core.csproj` | `Radiata.Core.dll`, net8.0 (not -windows) | **Zero package/project references — BCL only, deliberately.** All OS work goes through `IPlatformActions` / `IController`. Namespace is flat `ControllerWheel` regardless of folder. |
| `ArcadeHost\ArcadeHost.csproj` | `Radiata.ArcadeHost.exe`, net8.0, x64 | **The ONLY production project referencing Jint** (pinned 4.16.0). The jailed drop-in-game interpreter: Radiata launches it inside an AppContainer under a Job Object and talks over an ACL'd pipe — the app never links against it (`ReferenceOutputAssembly=false`; a copy target puts the exe + Jint/Acornima beside `Radiata.exe`). Compiles `Core\Arcade\Script\ScriptProtocol.cs` as shared SOURCE so the wire format can't drift. Second duty: `--xinput-count` (`XInputCount.cs`) is the observed-isolation probe for the BT Xbox cloak — a separate, non-HidHide-allow-listed image whose XInput view is exactly a game's (launched directly, no jail/pipe). |
| `tools\*` (21 projects) | see [Tools](#tools) | Probes, smoke tests, and standalone hosts; none ship. Three are published with the source (`TestHarness`, `SettingsSmokeProbe`, `ExtractStrings`); the rest are development repository only, not in the public source. |

Root-project NuGet: HidSharp 2.6.4, LiteDB 4.1.4 (pinned to read Playnite's v4 DB; NU1904 documented
unreachable), MahApps.Metro.IconPacks.Material, Microsoft.Data.Sqlite (+ SQLitePCLRaw bundle pulled forward
past its advisory), NAudio, Nefarius HidHide + ViGEm clients, ProtectedData (DPAPI).

---

## Core/ — `Radiata.Core.dll`

### Config model & persistence

| File | Type(s) | Purpose |
| --- | --- | --- |
| `AppConfig.cs` | `AppConfig` | Root config record: WheelA/WheelB, `SystemConfig`, per-action-type colour/icon override maps, custom palette, `SavedByVersion`. |
| `SystemConfig.cs` | `SystemConfig`, `SafeModeApp`, `SliceThicknessRule`, `SliceLabelRule` | Global behaviour settings plus the two shared slice-interpretation rules renderer and editors must agree on. |
| `WheelSlice.cs` | `WheelSlice` | One radial slot: label (null/length-guarded in the accessor — crash-loop fix), icon, tint, `ActionConfig`. |
| `ActionConfig.cs` | `ActionConfig` | The per-slice payload — a wide union of optional fields keyed by `Type`. |
| `SliceIdentity.cs` | `SliceIdentity` | "Do these two slices run the same thing?" — a per-type equality key over the payload the type actually reads, so a seeding path can't add a duplicate of a slice carrying residue from an earlier type. |
| `ConfigLoader.cs` | `ConfigLoader` | Load/create/atomic-write `config.json`, known-good `.bak`, versioned backups, debounced `Reloaded`. **`Sanitize` is the canonicalization choke point** (materials, help language, clamps, overrides). |
| `AppPaths.cs` | `AppPaths` | Single source of truth for `%APPDATA%\Radiata`; migrates legacy Capstan/ControllerWheel folders. |
| `TriggerConfig.cs` | `ControllerKind`, `TriggerPrimary`, `TriggerModifier`, `TriggerModes` | The summon-gesture vocabulary + token catalog shared by Settings, onboarding, and the interpreter. |
| `TriggerModesConverter.cs` | `TriggerModesConverter` | JSON converter tolerating the legacy scalar trigger form. |
| `SequenceFormat.cs` | `SequenceFormat` | Lossless text ⇄ `ActionConfig[]` for the sequence editor; unknown keys ignored for forward compat. |

### Wheel interaction & input abstraction

| File | Type(s) | Purpose |
| --- | --- | --- |
| `WheelStateMachine.cs` | `WheelStateMachine`, `WheelTickResult` | Platform-agnostic selection: deadzone + EMA smoothing → armed slice, sticky grace, confirm dwell, in-wheel edit + assign dwell. |
| `IController.cs` | `IController` | Seam for a physical pad: connection, summon triggers, stick/button events, battery. |
| `ControllerState.cs` | `ControllerState` | Flat one-frame snapshot of every axis/button. |
| `MotionPolicy.cs` | `MotionPolicy` | Product-wide Reduce Motion answer (Radiata setting OR Windows preference) with live `Changed`. |
| `Narration.cs` | `AnnouncementKind`, `ISpeechSink`, `Announcer` | Accessibility goal A1's WPF-free coordination layer: prioritises Context/Result/Selection/Scrub announcements, debounces and coalesces rapid selection changes, and drops stale speech once its context is gone. The voice itself is `ISpeechSink` (`SpeechSink` in the app project). |

### Action execution

| File | Type(s) | Purpose |
| --- | --- | --- |
| `ActionExecutor.cs` | `ActionExecutor`, `ActionStatus` | Dispatches a slice's action by `Type` through `IPlatformActions` + host delegates; unknown types trace and no-op. |
| `IPlatformActions.cs` | `IPlatformActions` | The OS contract: launch/focus/toggle/kill, URLs, storefronts, keys, audio, HDR/display, Discord. |
| `ObsClient.cs` | `ObsClient` | BCL-only obs-websocket v5 client; bounded queue, never throws. |
| `GameChatButtons.cs` | `GameChatButtons` | Generated game → chat-key lookup (500 verified entries) for the `text-chat` action's "Try Game Default", plus the known-no-text-chat set that makes it send nothing. |
| `UpdateFeed.cs` | `UpdateFeed` | Validated update-feed entry (latest.json): hostile-input parse (null+reason, 64 KB cap, https-only), numeric 0.R.B triplet compare, safe download-file-name derivation. No HTTP — the shell (`UpdateService`) fetches. |
| `CrashReport.cs` | `CrashReport` | Pure crash-report text rules: compose one block, scrub the user profile to `~`, hold the pending file to the 64 KB POST budget (oldest content dropped first). The shell's `CrashReporter` does the I/O. |
| `HidInstanceId.cs` | `HidInstanceId` | Vid/pid extraction across USB/BT instance-id shapes, and the one device-interface-link → instance-id converter (`FromInterfacePath`): the reader's cloak ids, the ViGEm skip in `ControllerProfile`, the removal sentry's match against owned cloak ids, and — with `requireFullLink` (`\\?\` prefix + interface-class segment) — the elevated `pnputil` restart sweep in `HidHideManager`. Same-model matching is not ownership and is not used to adopt foreign cloak entries. |
| `BtObservationGate.cs` | `BtObservationGate` | What one finished Bluetooth isolation observation may write: Verified/Refused only from the still-pending observation for the current id set, with the pad present and still cloaked across the probe; otherwise Reset (re-observe) or Discard. |

### Presentation vocabulary, Help, localization

| File | Type(s) | Purpose |
| --- | --- | --- |
| `Materials.cs` | `Materials` | The eight material tokens, `IsDark`/premium predicates, `Normalize` (the single legacy-alias resolver, run at load), and the per-run custom-theme registry (`RegisterCustom`/`CustomFor`; see docs/PACKAGES.md). |
| `Packages\MaterialPackage.cs` | `MaterialPackage` | Drop-in theme manifest: parse+validate (palette/system-font/sound only — the format can't express code, files, or network); null+reason, never throws. |
| `Packages\PackageStore.cs` | `PackageStore` | The two drop-in folders (create + README, incl. the Arcade JS API reference), the startup-only scan, the `IsPackageDir` Recycle-Bin guard, whole-folder SHA-256 content hashing + shape caps for BOTH kinds; arcade packages parse `game.json` and verify the entry `.js` (≤256 KB). `Inspect(dir, kind)` validates one folder exactly as the scan does (the harness checks the Workshop samples through it). The READMEs carry a GOTCHAS section that must agree with the Workshop guide. **The single gate for both package features**: a kind `ReleaseGates` withholds is neither created nor scanned. |
| `ReleaseGates.cs` | `ReleaseGates` | The public-release feature gate — the ONLY reader of `PUBLIC_RELEASE` (`build-installer.ps1 -Public`). Build consts `Internode` / `ArcadePackages` / `MaterialPackages` (false in a public build) plus the harness-only `PreviewPublic` flip and `*Offered` properties the Help set reads. |
| `Packages\PackageInstaller.cs` | `PackageInstaller` | Drag-and-drop install, WPF-free: stages a dropped folder or `.zip` (top-level files only, caps counted on actual bytes, ZIP entry names treated as hostile) into `Packages\.staging`, validates it through `PackageStore.Inspect` under its final folder name, finds conflicts, commits with a same-volume move. `IsPackageDir` in `PackageStore` is its Recycle-Bin guard. docs/PACKAGES.md ▸ Installing and removing. |
| `Packages\PackageConsent.cs` | `PackageConsent` | Per-package consent ledger (`packages-consent.json`): kind+folder+hash identity, declines remembered, content change re-prompts. |
| `HelpContent.cs` | `HelpContent`, `HelpTopic`, `HelpBlock` | Canonical English Help source — renders in Settings ▸ Help *and* generates the controls reference (`--export-controls`). A topic or block carries an optional `When` (`ReleaseGates`); `Topics`/`ExportTopics` drop what it denies, `AllAuthored` is the ungated set the translation tooling walks. |
| `HelpFigures.cs` | `HelpFigures`, `HelpFigure`, `FigPart` | The Help topics' illustrations, declared as shape data (Core stays WPF-free); `HelpEditorControl` draws them, labels translate like prose. |
| `HelpLocalization.cs` | `HelpLocalization`, `HelpLanguage` | Translation keyed by the English string itself (edits fall back to English, never show stale); `--check-help-locales`; `--dump-help-locale` regenerates one map in `SourceStrings()` order. `HelpLanguage.Rtl` drives the pane's reading direction. |
| `HelpTextEs/De/Ja/Ar.cs` | `HelpTextEs/De/Ja/Ar` | The four string maps; refreshed only in a batched pass, by regenerating with `--dump-help-locale` rather than patching. Entry order follows `SourceStrings()`. |
| `HelpHtmlExport.cs` | `HelpHtmlExport` | The whole Help system as a static site for getradiata.app/help (`--export-help-html`) — one page per language, figures drawn as inline SVG, meshed with the site's own `styles.css`. |
| `Loc.cs` | `Loc` | App-wide UI strings keyed by the English text: `T` / `F` / `P` (plurals on the `one|other` pair, per-language form counts), `Init` once at startup (UI culture for every thread; the language is fixed for the run), `SourceStrings()` (reflects `UiText` + `UiStrings.All`), `CheckReport`. |
| `UiText.cs` | `UiText` | The C#-side UI strings as `public const string` in nested classes per surface — in the catalog by construction. |
| `UiTextEs.cs` | `UiTextEs` | The Spanish UI map: English key → Spanish, in catalog order, regenerated with `--dump-ui-locale es` (never patched by hand); machine-translated, fixed one entry at a time. `UiTextDe/Ja/Ar.cs` follow the same shape as they land. |
| `UiStrings.g.cs` | `UiStrings` | **Generated** by `tools/ExtractStrings` from every root `*.xaml` (`{loc:T …}`, `LocRich.Source`); committed. Never hand-edit. |

### Arcade framework (`Core\Arcade\`)

| File | Type(s) | Purpose |
| --- | --- | --- |
| `Arcade.cs` | `Arcade` | The build-time `Enabled` const (always true — no Arcade-off build ships; deliberately not config) + the harness-only concealment seam; guard-card copy. |
| `ArcadeGame.cs` | `IArcadeGame`, `ArcadeInput`, `ArcadeHowTo` | The game contract (fixed-step, hostile-input snapshot/restore), the one-frame input budget, and the how-to-play card as data. |
| `ArcadeDebug.cs` | `ArcadeDebug` | Dev switches compiled OUT of a public release via `ReleaseGates.PublicRelease`: `LevelSkip` lets Select step Internode, Petalpop and Kabloom one level forward. |
| `ArcadeCatalog.cs` | `ArcadeCatalog` | The single game registry (id/title/glyph/tint/factory); unknown id resolves to the picker. |
| `ArcadeAim.cs` | `ArcadeAim` | Shared rim-steering model so every circumference game agrees on "left". |
| `ArcadeStore.cs` | `ArcadeStore` | Frozen state, high scores + the per-game how-to-seen flag in `arcade-state.json` — deliberately not in `config.json`; also the `arcade-shots\` path helper for the picker's screenshots. |
| `ArcadeSfxTuning.cs` | `ArcadeSfxTuning` | Family-level sound knobs (arcade master + per-game gains, ladder shape, host throttle intervals) as mutable statics in `TuningTypes`, so `arcade-tuning.json` tunes them live; names deliberately distinct from every other tuning class. |
| `ArcadeMath.cs` | `Vec2`, `ArcadeMath`, `ArcadeRng` | `Vec2`: the one 2-D point/vector type every sim's geometry shares. Plus the one copy of the sims' shared scalar helpers (smoothstep, angle wraps, toward-zero step, finite check) and the xorshift64* generator every sim seeds and snapshots; each game's private helper delegates here. |
| `ArcadeTuning.cs` | `ArcadeTuning` | Frame-pump and disc numbers as mutable statics (120 Hz step, catch-up cap, ready beat, disc scale, HUD scales), and the dev-only `arcade-tuning.json` override, which reflects over every class in `TuningTypes` (this, Connate, Kabloom, Petalpop, Internode, the picker, `ArcadeSfxTuning`). |
| `ArcadePickerTuning.cs` | `ArcadePickerTuning` | Every layout/feel number of the picker carousel: ring geometry, swing spring, screen-ellipse and marquee fractions (measured against the 817×904 cabinet art), steering thresholds + auto-repeat, backdrop grid. |
| `ArcadeCarouselNav.cs` | `ArcadeCarouselNav` | The picker's steering state machine: horizontal stick with arm/release hysteresis or d-pad level → one step per flick, auto-repeat while held. Pure, clock-free, harness-drivable. |
| `Script\ScriptProtocol.cs` | `ScriptProtocol`, `ScriptRequest/Response`, `ScriptDrawCommand`, `ScriptDrawValidator` | The Radiata ↔ `Radiata.ArcadeHost.exe` wire format (length-prefixed JSON, hard caps: 1024 commands / 2 MB request / 1 MB response / 64-char strings / kv 4 KB) and the host-side validation of everything the untrusted helper returns. **Compiled into BOTH Radiata.Core and ArcadeHost as shared source.** |
| `Script\ScriptGameManifest.cs` | `ScriptGameManifest` | `game.json` parse+validate for Arcade Games drop-in packages (MaterialPackage-grade: bounded strings, bare-`.js`-filename entry, null+reason). Token = `pkg-<id>`, so a package can never shadow a built-in game id. |
| `Script\ScriptArcadeGame.cs` | `ScriptArcadeGame` | The `IArcadeGame` adapter for a consented script game: aggregates input per rendered frame, mirrors the helper's KV store (**the only state surviving a dismiss** — `hiscore` is a reserved key), drains validated cues. Construction is headless; the session lives shell-side. |

### Arcade games (`Core\Arcade\Games\`)

| Game | Files | Purpose |
| --- | --- | --- |
| Kabloom | `Kabloom\Kabloom.cs`, `KabloomGrid/Board/Generator/Solver/Difficulty/Tuning.cs` | Circular pentagonal-floret Minesweeper: geometry, minefield rules, incremental no-guess board generation certified by a deterministic human-style solver, six-chapter difficulty curve. |
| Connate | `Connate\Connate.cs`, `ConnateBody/Physics/Rules/Tuning.cs` | Radial number-orb merging: deterministic centre-gravity physics, exact rank↔value ladder shared with the headless probe. |
| Petalpop | `PetalPop\PetalPop.cs`, `PetalPopGeometry/Collision/Types/PaddleControl/Tuning.cs` | Pentagonal brick-clearer: arc rails + paddle capsules + petal quads derived from the layout, discrete contact kit, spring/rail control strategies, sim-owned twist/punch/hit-stop clocks, v1 snapshot. |
| Internode | `Internode\Internode.cs`, `InternodePhysics/Phrases/Sequencer/Reachability/Types/Tuning.cs` | Half-pipe runner: pendulum steering + jump integrator shared with the probe-time reachability oracle, authored phrase bank (rim-safe and time-locked flags) + tier sequencer, sim-owned 2-D bends and twists (no physics effect) and rim closures (which do reach the physics), double jump, quota derived from section content, one-section memory model, seed-rebuilt v7 snapshot. |

> **Venturi was DELETED Aug 8 2026** — sim, renderer, palette, its three tool projects and its tunnel
> spec. It had been parked as `Hidden` since Aug 6. Nothing else depended on it: the rim-steering model it
> shared with Connate lives in `ArcadeAim`, and Connate's matching speed constant was deliberately kept
> separate rather than shared, which is exactly why removing the game cost Connate nothing. Recoverable from
> the development repository's git history if it is ever wanted back.

---

## Root — the WPF shell (`Radiata.exe`)

### App orchestration

| File | Type(s) | Purpose |
| --- | --- | --- |
| `App.xaml` / `.xaml.cs` | `App` | The ~7,300-line orchestrator: tray + single-instance, config hot-reload, overlay lifecycle, enable/disable chord + trigger resolution (`ResolveTrigger` is the one place chord→wheel-side lives), volume/mic/Alt-Tab d-pad modes, Game Grid, Arcade host, in-wheel edit + Add picker, OOBE gate, auto passthru mode watcher, **the notice system** (`ShowCornerToast`/`ShowStatusToast` over `ToastPresenter`, the sentry card — every user-facing notice; no tray balloons exist), teardown + the uninstall worker. `App.xaml` also holds the app-wide chrome palette (`Ui*`, `HeadingInk`). |
| `ToastPresenter.cs` | `ToastPresenter`, `NoticeTier` | The single-occupancy toast slot App owns: `ShowRectToast` (the roundrect standalone notice card, suppressed during onboarding), `BuildAndShowToast` (the round toast mirroring the wheel material, Kawaii stars included), `ToastBrushes`/`BuildToastRim`, the close timer and the two-tier safety hold. |
| `App.Cli.cs` | `App` (partial) | The command-line modes `OnStartup` runs before the single-instance claim: `CliCommands` is the ordered switch table (first match wins), `RunCommandLine` the dispatcher, one `Cli*` handler per mode (autostart decision, `--watchdog`, `--uncloak`, the exports and locale checks, `--hidhide-whitelist`, `--install-drivers`, the uninstall entries). |
| `App.ControllerInput.cs` | `App` (partial) | `StartController`: wires every `_controller`/`TriggerInterpreter` event to a named handler (stick-seal boundaries, connect/disconnect + pad-grace, battery, Fn press/release, per-button routing across arcade/edit/browser/onboarding, the trigger-interpreter callbacks, onboarding chord forwarding). One handler per button/axis; `StartController` itself is wiring only. |
| `GlobalUsings.cs` | (aliases) | Resolves WPF/WinForms same-name ambiguities project-wide. |

### Overlay + wheel rendering

| File | Type(s) | Purpose |
| --- | --- | --- |
| `OverlayWindow.xaml(.cs)` | `OverlayWindow` | Transparent non-activating topmost full-screen canvas with a topmost-reassert timer. |
| `RadialMenuControl.cs` | `RadialMenuControl` (partial) | The wheel itself — one of the two largest files: draws slices, owns a Core `WheelStateMachine`, feeds it stick input on a render timer. Material-specific draw helpers stay here beside `DrawSlice`/`DrawHubContent`, which call them; the split-out partials below hold geometry construction only. |
| `RadialMenuControl.Geometry.cs` | `RadialMenuControl` (partial) | Wedge/ring/arc/wobble geometry builders shared across materials (`BuildWedge`, `BuildOrganicWedge`, `BuildArc`, `Polar`, the corner-rounding and hand-cut-edge primitives), plus the `Frozen`/`FrozenPen` freeze helpers. |
| `RadialMenuControl.Kawaii.cs` | `RadialMenuControl` (partial) | Kawaii's wedge shape and the geometry/token wrappers hosts outside this control use to match the hub's cloud outline and ambient twinkle. |
| `RadialMenuControl.Salvage.cs` | `RadialMenuControl` (partial) | Salvage's blocky/stamped wedge geometry and its edge-cell cache. |
| `RadialMenuControl.Mesa.cs` | `RadialMenuControl` (partial) | Mesa's (identifiers: Terra) organic hand-cut wedge geometry. |
| `RadialMenuControl.Reactor.cs` | `RadialMenuControl` (partial) | Reactor's fused hub+wedge keyhole silhouette (`BuildFusedKeyhole`/`BuildReactorFusion`), drawn instead of a separate wedge while a slice is armed. |
| `RadialMenuPeer.cs` | `RadialMenuPeer` | UI Automation peer for `RadialMenuControl` (accessibility A1): exposes the ring as a selection container with one named child per slice so an external UIA tool can inspect it, since the never-focused overlay window otherwise has no automation tree and Narrator won't service it regardless. Invoke is deliberately unimplemented — a slice fires on trigger release, not on a click. |
| `WheelMiniature.cs` | `WheelMiniature` | Tiny radial diagram per row in the Settings slice list. |
| `ArcadeControl.cs` | `ArcadeControl` | The round arcade window host: frame pump, input funnel, freeze/resume, bleed-through guard, pause menu + how-to card; owns the picker carousel (`ArcadePickerRenderer`) and captures a screenshot whenever a game is frozen. |
| `GlossDarkPalette.cs` | `GlossDarkPalette` | Muted-teal tones shared by wheel slices and the Game Grid card. |

### Input capture, controllers, emulation

| File | Type(s) | Purpose |
| --- | --- | --- |
| `ControllerReader.cs` | `ControllerReader : IController` | One read loop, two backends: raw HID for Sony pads (profile-driven, USB + CRC'd BT), XInput for Xbox; Sony wins when both present. |
| `ControllerProfile.cs` | `ControllerProfile` | Per-device HID report description (VID/PID + byte offsets + report `Style`) — Edge / DualSense / DS4 / 8BitDo Ultimate 2C. |
| `BluetoothBattery.cs` | `BluetoothBattery` | Battery percent for a BT pad whose report carries none, read off the devnode's `DEVPKEY_Bluetooth_Battery` via CfgMgr32 (no WinRT / no TFM bump). Fails soft to unknown. |
| `XInputInterop.cs` | `XInputInterop` | Minimal xinput1_4 P/Invoke incl. ordinal #100 for the Guide button. |
| `TriggerInterpreter.cs` | `TriggerInterpreter` | Configured summon gesture (chords, touchpad edge-swipe) → open/dismiss callbacks. |
| `HidOffsets.cs` | `HidOffsets` | Persisted Fn/D-pad byte+mask offsets from the setup wizard; defaults = verified Edge layout. |
| `GamepadEmulator.cs` | `GamepadEmulator`, `EmulatedPad` | Virtual Xbox 360 / DS4 pad via ViGEmBus, driven from physical input. |
| `HidHideManager.cs` | `HidHideManager` | Ownership-scoped cloak operations with durable intent, checked release results, live driver verification, and retained recovery records on failure. Uncloak is best-effort when the driver is unavailable. |
| `XboxDeviceTree.cs` | `XboxDeviceTree` | Enumerates XUSB devnode subtrees (wired/dongle) AND Bluetooth Xbox HID entries (`IG_` marker + BTH ancestry) so HidHide can block physical Xbox pads; virtual pads excluded via `PnpAncestry`, never VID/PID; trace lines name each pad's parent and the matched fingerprint. |
| `PnpAncestry.cs` | `PnpAncestry` | The one virtual-pad fingerprint both capture paths share: ViGEmBus service / `VIGEM` id / `ROOT\`-or-`SWD\` parent / `ROOT\SYSTEM` ancestor, via CfgMgr32; fails safe to "physical" and says why. |
| `XboxBtIsolationProbe.cs` | `XboxBtIsolationProbe` | Observed-isolation gate for the BT Xbox cloak: runs `Radiata.ArcadeHost.exe --xinput-count` (a non-allow-listed image = a game's XInput view); the virtual pad comes up only on an observed zero. |
| `KeypressSender.cs` | `KeypressSender` | Strict shortcut parsing, foreground-layout scan codes, held-modifier ownership, Unicode text, and cleanup of only the successfully injected partial prefix. |
| `ControllerButtons.cs` / `ControllerButton.cs` | `ControllerButtons`, `ControllerButton` | On-screen button-prompt renderer (PS glyphs / Xbox letters) + its XAML element. |

### Drivers, diagnostics, setup wizards

| File | Type(s) | Purpose |
| --- | --- | --- |
| `DriverStatus.cs` | `DriverStatus` | No-admin registry detection of HidHide / legacy HidGuardian / ViGEmBus for onboarding, plus the ViGEmBus file version and origin (Nefarius vs a named foreign fork — HP OMEN Gaming Hub, Oculus). |
| `DriverSetup.cs` | `DriverSetup` | Launches the bundled Nefarius installers elevated and interprets exit codes; runs the drivers' registered uninstallers and reports an `UninstallOutcome` per driver (a foreign fork = `NoUninstaller`, left in place). |
| `DriverInstallWindow.cs` | `DriverInstallWindow` | Minimal code-built progress window for `--install-drivers` (the installer's elevated post-install task); plain status lines + close-on-done, no Settings resources. |
| `HidWizardWindow.xaml(.cs)` | `HidWizardWindow` | "Controller Setup": baseline capture then guided Fn/D-pad presses to discover HID offsets. |
| `DiagnosticWindow.xaml(.cs)` | `DiagnosticWindow` | Live raw-HID byte grid; changed bytes flash against a baseline. |
| `HdrProbe.cs` | `HdrProbe` | `--hdr-probe` diagnostic: snapshot → set HDR → guaranteed auto-revert. |

### Settings window + editors

| File | Type(s) | Purpose |
| --- | --- | --- |
| `SettingsWindow.xaml(.cs)` | `SettingsWindow` | Tab host: owns the `ConfigLoader`, debounced save, status badge, App hooks. |
| `SettingsTheme.xaml` | ResourceDictionary | Settings-only look, merged per-window. ⚠ See the DynamicResource rule in [SETTINGS-UI.md](SETTINGS-UI.md). |
| `WheelEditorControl.xaml(.cs)` | `WheelEditorControl` | Wheels tab: slice list + per-slice action editor, drag-drop, `.lnk` drops (~2,600 lines). |
| `CustomizeEditorControl.xaml(.cs)` | `CustomizeEditorControl` | Customize tab: material tiles (Simple/Deluxe group boxes; consented custom themes append to Deluxe), sound + glyph tiles, thickness, D-Pad mode, Show-labels, chord builder. |
| `PackageInstallFlow.cs` | `PackageInstallFlow` | The run's package inventory and registration (startup scan, the startup approval prompt, `RegistryChanged` for an open Customize tab), drag-and-drop install (Settings window drops; paths handed to `Radiata.exe`, including the second-launch `.requests` forwarding), Replace/Uninstall via the Recycle Bin. Owns the rule that a token's content never changes within a run. |
| `PackageConsentWindow.cs` | `PackageConsentWindow` | The stern per-package consent gate (checkbox-armed accept, decline default); built in code, no XAML resources — immune to the StaticResource parse crash class. `runsCode: true` (arcade packages) swaps in the sterner "this is CODE that will run" copy. |
| `SystemEditorControl.xaml(.cs)` | `SystemEditorControl` | Advanced tab: **the Language picker** (owns `SystemConfig.Language`; the restart prompt is SettingsWindow's), controller, integrations, Game Grid options, **Accessibility**, backup/restore, Quit. |
| `ExceptionsEditorControl.xaml(.cs)` | `ExceptionsEditorControl` | Exceptions tab: Anticheat Passthru Mode + per-app auto-passthru-mode list. |
| `HelpEditorControl.xaml(.cs)` | `HelpEditorControl` | Help tab: renders Core `HelpContent` with host-resolved chord/glyph tokens; read-only. Follows the app language (no picker of its own); owns the drawn flag swatches (`BuildFlagGlyph`, `FlagToolTip`) the Advanced tab and onboarding use. **Owns `RenderTopicInto`, the app's single topic renderer** — anything showing a Help topic goes through it. |
| `HelpTopicWindow.cs` | `HelpTopicWindow` | One Help topic in a modal pop-up, for (?) links on surfaces with no Help tab (the onboarding Accessibility card). Renders via `HelpEditorControl.RenderTopicInto`, so it can't drift from Help. |
| `LanguageRestartWindow.cs` | `LanguageRestartWindow` | The language-change prompt: the question in the chosen language, then the current one in italics, Yes/No in both words. Code-built (no XAML); opened by SettingsWindow after an Advanced-tab pick. |
| `SliceEditModel.cs` | `SliceEditModel` | Mutable flat per-slice model the editor binds to. |
| `SelectableText.cs` | `SelectableText` | Makes plain TextBlocks copyable via WPF's internal TextEditor; fail-soft. |
| `InlineMarkup.cs` | `InlineMarkup`, `InlineMarkup.Look` | The one inline-markup vocabulary rendered to WPF inlines (`**bold**`, `*italic*`, `` `code` ``, `[[topic|Label]]`, `[Label](url)`, literal `\n`); Help, cards and toasts share it. Also owns `IsolateArrows`, which renders the D-pad pair 🡄 🡆 as a left-to-right island in RTL text (docs/LOCALIZATION.md ▸ *Direction glyphs*). Only authored strings reach it. |
| `LocExtension.cs` | `TExtension`, `LocRich`, `LocWpf` | `{loc:T …}` for short labels on real dependency properties; `loc:LocRich.Source` attached property for prose with markup (Inlines is not a DP). Both resolve once — the language is fixed for the run. `LocWpf.ApplyTo(window)` gives a UI window the language reading direction once; `LocWpf.LanguageFamily` is the language font chain the wheel and previews draw in for non-Latin scripts. |

### Pickers & dialogs

| File | Type(s) | Purpose |
| --- | --- | --- |
| `IconColorPickerControl.xaml(.cs)` | `IconColorPickerControl` | Shared glyph + tint editor; host confirms. |
| `ColorNamer.cs` | `ColorNamer` | Human colour names for narration ("dark blue color") — UIA names for text-less swatches; hex lives in the tooltip. |
| `DefaultPickerWindow.xaml(.cs)` | `DefaultPickerWindow` | Modal wrapper for Color Defaults; surfaces "Fetch Logo". |
| `IconPickerWindow.xaml(.cs)` | `IconPickerWindow` | Searchable Material icon grid in the Settings look; the whole catalog in a row-virtualized list, opened unfiltered and scrolled to the current glyph. |
| `AppPickerWindow.xaml(.cs)` | `AppPickerWindow` | Installed-app picker from the shell AppsFolder; icons stream in off-thread. |
| `DiscordSetupWindow.xaml(.cs)` | `DiscordSetupWindow` | Bring-your-own-credentials wizard; "Test" becomes "Save" once the pair validates. Shares `SetupDialogFlow` + `SetupDialogTheme.xaml` with the OBS pane. |
| `ObsSetupWindow.xaml(.cs)` | `ObsSetupWindow` | OBS WebSocket port + password pane (Advanced ▸ Integrations, and from an unconfigured OBS slice). Persists nothing itself — hands the values back for `SystemEditorControl.ApplyTo`. Shares `SetupDialogFlow` + `SetupDialogTheme.xaml` with `DiscordSetupWindow`: the same one-button "Test" → "Save" flow once OBS answers. |
| `SetupDialogTheme.xaml` | ResourceDictionary | `StepNum`/`StepText` styles shared by the OBS and Discord setup dialogs; each merges it at its own Window level. Deliberately separate from `SettingsTheme.xaml` — these are independent top-level windows, not hosted inside `SettingsWindow`. |
| `SetupDialogFlow.cs` | `SetupDialogFlow` | The Test⇄Save button state machine shared by `ObsSetupWindow` and `DiscordSetupWindow`. |
| `UninstallDialog.cs` | `UninstallDialog` | `--uninstall` confirmation with two off-by-default opt-ins. |
| `OnboardingWindow.xaml(.cs)` | `OnboardingWindow` | The OOBE wizard (~3,700 lines); re-runnable from the tray. See [ONBOARDING.md](ONBOARDING.md). |

### Sound

| File | Type(s) | Purpose |
| --- | --- | --- |
| `SfxEngine.cs` | `SfxEngine` | The one NAudio mixing bus: overlapping voices, `MasterLimiter`, voice cap, idle close. ⚠ Short-read source-drop trap — see [SOUND.md](SOUND.md). |
| `Sfx.cs` | `Sfx` | Semantic layer (armed/fired/on-off) over the digital/physical/kawaii WAV themes; fail-quiet. |
| `KawaiiXylophone.cs` | `KawaiiXylophone` | Kawaii's melodic armed-tick sequencer; resamples C6–C7 WAVs for accidentals. |
| `ArcadeSfx.cs` | `ArcadeSfx` | The Arcade's sound layer: a sample `Bank` per role (take rotation, pitch jitter, ladders on sim facts, layered big moments, host throttles), one bank set per game plus the shared chrome and the nine-name generic vocabulary; fail-quiet when the licensed WAVs are absent. See [SOUND.md](SOUND.md) ▸ Arcade. |
| `ArcadeMusic.cs` | `ArcadeMusic` | The Arcade's music beds: which track each game plays and how it cycles (loop / playlist / keyed to a progression number), synced once per frame and ducked under menus and cards. On by default, switchable per game, and host-owned, so no sim knows music exists. See [SOUND.md](SOUND.md) ▸ Arcade music. |
| `AudioDeviceSwitcher.cs` | `AudioDeviceSwitcher` | Default render/capture endpoint switch via undocumented IPolicyConfig. |
| `AppVolumeMixer.cs` | `AppVolumeMixer` | D-pad ◀▶ game-vs-chat balance, PS5-style, by process name from live audio sessions. |
| `SpeechSink.cs` | `SpeechSink` | The narration voice: Windows' SAPI synthesizer on the default output device, independent of `SfxEngine`'s bus (quiet chimes + loud speech is supported). Lazily constructed at the call site — SAPI init costs ~100 ms and loads a voice. Implements `Core`'s `ISpeechSink` for `Announcer` (`Narration.cs`). |

### Game library, art, Game Grid

| File | Type(s) | Purpose |
| --- | --- | --- |
| `GameLibrary.cs` | `InstalledGame`, `GameLibrary` | Live storefront scan (registry, SQLite, manifests) into a unified record. |
| `PlayniteLibrary.cs` | `PlayniteLibrary` | Playnite LiteDB read (names, covers, `playnite://`), JSON cache for when Playnite holds the DB. |
| `UninstallRegistry.cs` | `UninstallEntry` + enumerator | Four-pass sweep of the uninstall hives (HKLM/HKCU × 64/32). |
| `InstalledApps.cs` | `InstalledApp`, `InstalledApps` | Shell AppsFolder enumeration (Store apps without exe paths). STA-sensitive. |
| `LauncherCatalog.cs` | `LauncherInfo`, `LauncherCatalog` | The storefronts the launcher action can open: key, name, glyph, tint. |
| `StorefrontWarmup.cs` | `StorefrontWarmup` | Predicts the cold-storefront slow case so the fire path can hold a "Launching" hub. |
| `GameArt.cs` | `GameArt` | Key-art resolution + disk cache: Playnite cover → Steam CDN → SteamGridDB, with `.miss` markers. Per-game overrides live in `GameMetadata`; SteamGridDB title matching lives in `GameArtNameMatch` — callers use both directly rather than through a `GameArt` forwarding wrapper. |
| `GameMetadata.cs` | `GameMetadata` | Per-game Game Grid personalization and art-pick overrides (cover/logo choice, favorite, hidden, last-launched), persisted to `cover-overrides.json`; the identity-resolution that survives a Playnite import/rename or an id⇄name key flip. |
| `GameArtNameMatch.cs` | `GameArtNameMatch` | SteamGridDB title-to-id resolution: query-variant generation (article/bracket/qualifier/year forms, camelCase splitting) and the exact-match compare that keeps a fuzzy SGDB search from attaching the wrong game's art. |
| `ArtPrefetcher.cs` | `ArtPrefetcher` | Background sweep warming the grid's alternate-art caches. |
| `GameBrowserControl.xaml(.cs)` | `GameBrowserControl` + tile VMs/converters | The Game Grid: 3-column controller-navigable tile browser with art cycling, search/sort, storefront chips. |

### Curated art + the dev-only Radiata Picker

| File | Type(s) | Purpose |
| --- | --- | --- |
| `CuratedArt.cs` | `CuratedArt` (partial) | Shipping lookup for hand-curated default art picks (explicit SGDB URLs); user overrides win. |
| `CuratedArt.g.cs` / `CuratedArtOwner.g.cs` | (generated partials) | **Checked-in generated files — no MSBuild step.** Regenerate with `Radiata.exe --export-picker` / `--export-mypicks` and copy from `%APPDATA%\Radiata`. Never hand-edit. |
| `OwnerPicksExport.cs` | `OwnerPicksExport` | Offline generator that emits `CuratedArtOwner.g.cs` from saved local overrides. |
| `RadiataPicker.cs` | `RadiataPicker` | Dev-only `--export-picker`: the top-100 seed list and the export of its recorded art picks to `picker-export.json` + `CuratedArt.g.cs`. **Carries the full removal map for the whole Picker feature.** |
| `SliceLogoPickerWindow.xaml(.cs)` | `SliceLogoPickerWindow` | Dev-only mouse picker for top-100 default slice logos. Nothing opens it. |

### Icons, tints, visual-asset helpers

| File | Type(s) | Purpose |
| --- | --- | --- |
| `MonoIcons.cs` | `MonoIcons` | Built-in frozen monochrome glyphs + the per-action-type default glyph map. |
| `PackIconHelper.cs` | `PackIconHelper` | Cached frozen DrawingImages from MahApps Material icon names, plus three names outside the MDI set: the Radiata flower, the Arcade joystick, and `ArcadePackage:pkg-<id>` — a drop-in game's own glyph PNG, its alpha filled with the slice brush (falls back to the shared drop-in glyph). |
| `IconCache.cs` | `IconCache` | Exe/shell icon extraction + cache; cleared on config hot-reload. |
| `ActionIcons.cs` / `ActionTint.cs` | `ActionIcons`, `ActionTint` | User-overridable default glyph and tint per action type (tints seed the swatch palette; glyphs only, never extracted icons). |
| `AddMenuIcons.cs` | `AddMenuIcons` | Glyph + accent per Add-picker category/group. |
| `CustomColorStore.cs` | `CustomColorStore` | The 16-slot custom-colour palette shared by every ColorDialog, persisted in config. |
| `FlowerIcon.cs` / `FlowerMark.cs` / `FlowerMarkControl.cs` / `AboutFlowerControl.cs` | — | The Radiata flower mark: path data + authored palette embedded from `Assets\flower-mark-color-unique.svg` (leaves + blossom as separate geometries), animatable regions (petal wedges), the on/off toast animation, and the full-colour About animation. Also reachable as PackIconHelper name `RadiataFlower` (Quit button, Add-picker Radiata category). |

### System integration & platform actions

| File | Type(s) | Purpose |
| --- | --- | --- |
| `WindowsPlatformActions.cs` | `WindowsPlatformActions : IPlatformActions` | The Windows half of action execution: CoreAudio, P/Invoke, process launch/focus, storefronts, Discord IPC. Also home of the session-scoped process helpers (`GetSessionProcessesByName`/`AnySessionProcess`) every "is X running?" check must use — machine-wide enumeration counts another account's apps as running here. |
| `NativeMethods.cs` | `NativeMethods` | Shared P/Invoke surface (SendInput, `ForceForeground`, window/shell interop). |
| `HdrState.cs` | `HdrState` | Display HDR read/toggle via the CCD API across generations (24H2 vs legacy, ACM-aware). |
| `PowerPlans.cs` | `PowerPlans` | `powercfg /list` enumeration for the editor's picker (activation goes through `IPlatformActions`). |
| `StartupManager.cs` | `StartupManager` | Per-user Run-key entry (`"exe" --autostart`, `Format`/`ParseTarget`) — the single ON/OFF switch both autostart routes read (docs/INSTALLER.md ▸ Autostart); reads Windows' `StartupApproved` toggle for the logon task; migrates legacy names and the pre-switch format, and installed copies adopt a portable copy's entry. The installer seeds the identical value on fresh installs (`radiata.iss` `[Registry]`; `TestHarness package` guards the match). |
| `QuitMarker.cs` | `QuitMarker` | Records a deliberate quit for the current logon session (token authentication id) so the late Run-key autostart stays closed; self-expiring, never written on a crash. |
| `InstallInfo.cs` | `InstallInfo` | Installed-vs-portable detection: reads the Inno HKCU uninstall key, compares InstallLocation to the app dir; exposes `IsInstalledCopy` + `UninstallerPath` (cached per process). |
| `NarratorLauncher.cs` | `NarratorLauncher` | Starts Windows' system-wide Narrator for the Settings ▸ Accessibility tip. One-way by design — no stop path, no auto-start registry write. |
| `RecoveryTask.cs` | `RecoveryTask` | COM-registered logon recovery task scoped by account SID and executable path; validates action/principal/trigger before update, migration, or removal. Narrow elevation preserves the originating SID. |
| `ShortcutResolver.cs` | `ShortcutResolver` | Resolves a dropped `.lnk` to its real target exe. |
| `UpdateService.cs` | `UpdateService`, `UpdateCheckStatus` | The update checker/downloader: feed fetch (60 s after startup + 24 h + the manual Settings check), skip-list handling, quiet-moment prompting, and download → SHA-256 verify → silent install per docs/INSTALLER.md (exits via App.ExitApp so the cloak lifts). Failures are trace-only. |
| `UpdateWindow.cs` | `UpdateWindow` | Code-built update prompt (no XAML resources): current → new version, notes link, Update Now / Later / Skip This Version, inline download progress + cancel, and a pre-checked "Restart Steam after updating" opt-in (shown while steam.exe runs; arms `SteamRestartFlag` for the relaunched instance). |
| `tools/TestHarness/T_SteamConsent.cs` | `T_SteamConsent` | Isolated actual-WPF consent and dispatcher-deadline regression; no Steam restart, app startup or update installation. |
| `tools/TestHarness/T_Sentry.cs` | `T_Sentry` | Group `sentry`: what ends an isolation claim (departure vs Steam restart vs logical dips, probe failures) on an injected process probe and bounce, the Bluetooth verdict gate, and interface-path parsing. No process or driver touched. |
| `CrashReporter.cs` | `CrashReporter` | Opt-in crash reporter I/O: the crash-time one-shot append to `pending-crash.txt` (exception-safe, runs beside CrashSafeUncloak) and the ≤64 KB text/plain POST to crash.php. |
| `CrashReportWindow.cs` | `CrashReportWindow` | Code-built consent window: the FULL pending report in a read-only box, Send / Copy to clipboard / Don't send (both dismiss at once; Send runs detached), "Remember this choice" → `crashAutoSend` true with Send, `crashPromptEnabled` false with Don't send. Never shown over a game/overlay (App defers it). |

### Discord & secrets

| File | Type(s) | Purpose |
| --- | --- | --- |
| `DiscordIpc.cs` | `DiscordIpc` | Voice-channel toggle over the Discord client's local IPC pipe, with silent token refresh. |
| `SteamSentry.cs` | `SteamSentry`, `SteamRestartFlag` | Steam-first startup-leak sentry: detects Steam's pre-cloak pad handle (heuristic, not handle enumeration), silently relaunches Steam at startup (`steamAutoRelaunch`), and backs the persistent Unlock Controller alert (UX in `App.EvaluateSteamSentry`/`ShowSentryAlert`). Also the user-opted restart path (`RestartSteamForUserAsync` — never fabricates sentry state) and the cross-restart marker file behind the update prompt's opt-in (`pending-steam-restart.txt`). Two latches: a Steam restart ends Steam's episode only; an observed departure of a cloaked devnode (`App.OnCloakedDevnodeDeparted`) severs every handle and also lifts the generic "unverified" downgrade. |
| `DiscordOAuth.cs` | `DiscordOAuth` | User's own Client ID + Secret at `%APPDATA%\Radiata\discord-oauth.json`; no secrets ship. |
| `LocalSecret.cs` | `LocalSecret`, `SecretField` | DPAPI at-rest protection for small local secrets + one-time plaintext migration. |

### Arcade renderers (`Arcade\` at root — WPF side)

Simulations live in `Core\Arcade`; this folder is rendering only (renderers read public sim state, drawing
never affects outcome).

| File | Type(s) | Purpose |
| --- | --- | --- |
| `ArcadeRenderer.cs` | `IArcadeRenderer`, `ArcadeRenderers` | The render contract (incl. how-to illustrations) + the `For` registry lookup. |
| `ArcadePalette.cs` | `ArcadePalette` | The drawing-side helpers every game palette and renderer shares — `Frozen`, `Fill` (a frozen brush from `(r, g, b, a)` bytes), `RoundPen`, the quantised brush/pen caches (`Solid`, `Stroke`, `Faded`, `WhiteBrush`, `WhitePen`), alpha-aware `Lerp`, opaque `LerpRgb`, `Polar` (0 = 12 o'clock, clockwise); the per-game wrappers delegate here. |
| `ArcadeChrome.cs` | `ArcadeChrome` | Shared bezel/type-scale/HUD/guard-card frame, independent of wheel materials. |
| `ArcadePickerRenderer.cs` | `ArcadePickerRenderer` | The picker: 2.5-D rotary carousel of per-game cabinet art (depth-dimmed over a silhouette plate, screenshot squashed into the screen ellipse, title printed only on the blank cabinet, inside its nameplate band in a condensed face beside a drop-in's `badge`, centred on its ink and shrunk/wrapped to fit; a drop-in's `tint` colours that cabinet) over a perspective floor grid + starfield; critically damped swing, Reduce Motion snap. |
| `ArcadeShots.cs` | `ArcadeShots`, `ArcadeShot`, `ShotKind` | Picker screenshots: capture of the frozen game into a 512² disc (script games from their last validated buffer), in-memory cache, off-thread atomic PNG write to `%APPDATA%\Radiata\arcade-shots\`, fallback chain live → seeded still (a package's `preview`, or the bundled one) → placeholder. |
| `ArcadeArt.cs` | `ArcadeArt` | Bitmap plumbing for the picker: packed-resource and on-disk PNG decode (frozen, failure-latched, edge-bounded), the shared frozen `ImageBrush` per bitmap, the cabinet art, the blank cabinet tinted per drop-in `tint` (greys multiplied, built once per colour), and a package's own pictures (`PackageImage`: badge, latched, capped). |
| `ArcadeSprites.cs` | `ArcadeSprites`, `ArcadeSprites.Slot`, `Fit` | Replaceable in-game art: the named slot list, override-then-packed resolution (latched, re-read per arcade open), numbered animation cycles on a wall clock, and the draw helpers — aspect-preserving fit, standing-on-a-floor fit, clip-to-shape overlay, tint-through-alpha wash. Every slot optional; a miss leaves the renderer's vector body. Artist contract: `Assets\arcade\sprites\README.md`. |
| `KabloomRenderer.cs` / `ConnateRenderer.cs` / `PetalPopRenderer.cs` / `InternodeRenderer.cs` | — | Per-game renderers (Kabloom ~2,300, Connate ~2,300, Petalpop ~1,100 lines; Internode ~2,900, the pseudo-3D one: `1/(1+kz)` perspective, checkered rings, 2-D vanishing-point swing, twist + roll rotation). |
| `KabloomPalette.cs` / `ConnatePalette.cs` / `PetalPopPalette.cs` / `InternodePalette.cs` | — | Frozen per-game palettes (no per-frame brush churn). Every colour is built from `(r, g, b, a)` bytes, never an eight-digit hex literal, so the `#AARRGGBB` trap (docs/ARCADE.md) can't recur. |
| `ScriptHost\ScriptGameRenderer.cs` | `ScriptGameRenderer` | The ONE renderer every drop-in script game shares: pumps the jailed session once per rendered frame, replays the last VALIDATED polar command buffer (all trig host-side), draws the gave-up guard-style card. |
| `ScriptHost\ScriptGameSession.cs` | `ScriptGameSession` | One helper process: ACL'd pipe (explicit 64 KB buffers — 0-byte default wedges the host's WRITE), non-blocking pump, miss counter, **2 s wall-clock kill (the only reliable stop for catastrophic regex)**, response validation. |
| `ScriptHost\ScriptSessionCoordinator.cs` | `ScriptSessionCoordinator` | Session lifecycle: lazy launch, ≤3 restarts per open, teardown on dismiss/shutdown, and the AppContainer launch-path handling (**a user-profile dev path can't host the helper** — stage-copied to `C:\Radiata-ArcadeHost-<hash>` with RX for the SID). |
| `ScriptHost\ScriptAppContainer.cs` | `ScriptAppContainer`, `ScriptContainerLauncher` | Zero-capability AppContainer profile + suspended-launch/assign/resume plumbing (jailed before the first instruction). |
| `ScriptHost\ScriptJobObject.cs` | `ScriptJobObject` | 128 MB process-memory cap, active-process 1 (**the actual child-spawn denial**), kill-on-job-close, die-on-unhandled-exception. |

### `Controller\` — chord illustration art

Eighteen PNGs of per-chord controller line art (the lone Fn, R4-L4 and Touchpad pieces, the Bumper/Trigger family, and one R4-L4+X piece per extra-button chord), embedded as WPF resources; filenames map 1:1 from
`TriggerModes` tokens via `OnboardingWindow.ChordImageFile()`. Fallback is `Assets\controls.png`. The art is
the presentation half of the chord set `TriggerInterpreter` detects.

---

## Tools

Run with `dotnet run --project tools\<Name>` unless noted. None ship in a release.

### Published with the source

| Project | What it proves |
| --- | --- |
| `SettingsSmokeProbe` | Realizes the Settings visual tree headlessly to catch runtime-only XAML resource failures (**the mandatory post-Settings-XAML check**, exit 0 = pass). |
| `TestHarness` | The main suite (`T_Actions`, `T_Config`, `T_Cloak`, `T_Help`, `T_Restore`, `T_Glyphs`, …); loads `Radiata.dll` via a custom `AssemblyLoadContext` resolver. |
| `TestHarness ▸ T_ArcadeSfx` (`sfx`) | Every `ArcadeSfx` bank take is an embedded `Assets\sfx\arcade\` resource, none empty or over 2 s, and no shipped arcade WAV is unreferenced; SKIPS in a build that carries none (the public mirror). Also proves every music track a bed names is embedded, skipping the same way. |
| `TestHarness ▸ T_Triggers` | Summon-gesture catalog consistency: every chord the builder OFFERS has display copy, an enable/disable phrase, and a `TriggerInterpreter.SidesFor` case (a missing case = a gesture that silently never fires). |
| `TestHarness ▸ T_WheelStateMachine` (`wheelsm`) | `WheelStateMachine` scenario coverage: deadzone boundary, nearest-slice + 0°/360° wrap across n = 1/2/3/8/12, release-to-fire vs release-while-centred cancel, the sticky-grace window and the edit-mode reflow, ghost and landing timings (on the machine's pinned millisecond clock, `PinnedClockMs`), the hold-to-confirm and edit-mode delete-hold dwells (Tick-driven), the armed slice surviving entry to edit mode, pick-up/carry/drop, nudge wrap, undo/redo empty-stack bounds, and a hidden `Tick`. Pure Core, no pad, no window. |
| `TestHarness ▸ T_StuckRoot` (`stuckroot`, in the default set) | `XboxStuckRootTracker` on a hand-stepped clock: settling inside 10 s, stuck at the window, healthy on a child, a fresh window after children are lost or the root leaves. Pure Core. |
| `TestHarness ▸ T_Churn` (`churn`, in the default set) | `PadCountChurnRule` on a hand-stepped clock: three guard-ON edges inside 20 s latch 60 s; the window is inclusive and aged trips drop; a fourth edge in the hold is ignored; expiry reports once and the first guard-on after it counts as a new trip; a second latch holds for the session (`long.MaxValue`) and only every pad gone releases it; `Reset` clears everything. Pure Core, no pad, no timer. |
| `TestHarness ▸ T_Hygiene` (`hygiene`) | Mechanical guards with no machine state, no UI, no config write: every sibling `SystemEditorControl.xaml`/`CustomizeEditorControl.xaml` `CheckBox` wires both `Checked` and `Unchecked`; the development repository's process ledgers carry no `✅`/checked box/closed-status heading past their header, and the generated controls export (the `--export-controls` output) matches `HelpContent.ExportControlsMarkdown()` (both skip in the public snapshot through `H.DevRepoOnly`); the Settings/onboarding help chip's `"?"` glyph nudge is the value measured for that surface; no comment in product source (root `*.cs`/`*.xaml`, `Core\`, `Arcade\`, `ArcadeHost\`) carries a dated decision, a maintainer attribution, or was/used-to-be narration; and no `///` `<summary>` block is immediately followed by another with no member between them. |
| `TestHarness ▸ T_Consistency` (`pairs`, in the default set) | Facts kept in two places, each parsed from its far side and compared with the built assemblies: the `radiata.iss` `[Registry]` Run entry (subkey, value name, value data with `{app}` resolved) equals what `StartupManager.Format` writes; `release.yml`'s feed `url` and `docs\INSTALLER.md` (its feed example and the sentence naming it) sit under `UpdateFeed.TrustedDownloadPrefix`; every file of the newest `publish\installer-staging*` folder is covered by `PortableUninstall.Files` or the helper hash manifest, every shipped manifest entry exists in it, and the manifest names every loose development-layout helper file `ScriptSessionCoordinator` stages (skips when no staging folder exists: `tools\build-installer.ps1 -StageOnly` makes one); `ConfigLoader`'s `ObsPort` and `MixBalance` clamps (read by feeding the real sanitiser values past both ends) equal `ObsSetupWindow`'s port range and the D-pad mixer's two clamps in `App.xaml.cs`. Everything except the range check reads development-only files and skips in the public snapshot through `H.DevRepoOnly`. |
| `TestHarness ▸ T_Passthru` | The "Safe Mode" → "Passthru Mode" rename, desk-free half: no English `UiText` const or Help title still says "Safe Mode"; Help search finds `passthru-mode` under `safe mode` / `passthru` / `passthrough`; `HelpLocalization.MarkupProblems` is empty for English; `Arcade.ReasonFromNote(IsolationNote.PassthruNoCloak)` is the non-overridable block and every other block stays overridable; the frozen `captureSafeMode` / `safeModeApps` keys and the `safe-mode` action type round-trip through `ConfigLoader`. Rendering is checked by eye. |
| `TestHarness ▸ T_Loc` | Localization invariants: the drawn surfaces (`RadialMenuControl`, `ArcadeControl`, `ControllerButton`, `WheelMiniature`, `OverlayWindow`) stay pinned left-to-right — the aim math in `Core` never mirrors, so an RTL ancestor flipping the drawing would invert aiming — and the language table's `Rtl` set matches what ships. |
| `TestHarness ▸ T_LocRegress` | Translation-style regressions (`locregress`, in the default set): `Loc.T` is identity under English for every `UiText` const; every es/de/ja/ar UI-catalog and Help-map value keeps the `{n}` and `{token}` set of its English key; `App.LowerFirst` sentence-cases English only (German nouns keep their capital); no formal address (`usted`, mid-sentence `Sie`/`Ihnen`/`Ihr…`) in the Spanish/German catalogs or Help maps; Arabic carries no fatha/damma/kasra/sukun and writes numeric ranges as `من X إلى Y`; Japanese spaces every kana/kanji to Latin/digit boundary; the Game Grid `OrderForGrid` sorts with `Loc.Culture` (accented titles interleave); the Restore prompt names `language`; `Loc.Pseudoize` keeps every placeholder, pad chip and link token of the real catalog strings; and every page of the `packaging\webhost` site snapshot (development repository only, not in the public source; root, `download/`, `tip/`, `workshop/`, `help/`) carries six `hreflang` alternates, the right `lang`/`dir`, and one shared `styles.css?v=NN`. Language-dependent checks flip `Loc.Lang` by reflection and restore it. |
| `TestHarness ▸ T_Workshop` | The Workshop samples and guide (`workshop`, in the default set): every folder under `packaging\sample-material-package` / `sample-arcade-package` (development repository only, not in the public source) passes `PackageStore.Inspect`; each site zip holds exactly its folder's files at the root; each sample game PLAYS 45 s through the real `Radiata.ArcadeHost.exe` (unjailed, over its own pipe protocol) with no fault and every frame passing `ScriptDrawValidator`; the `drop:` checks exercise `PackageInstaller` (samples install from folder and zip, ZIP layout rules, hostile entry names, byte caps, conflict, commit, staging cleanup); and the sandbox facts the guide states hold (out-of-range draw rejected, strict mode, no `console`, `|`-built colours draw nothing, both 60 Hz ticks see one `crossPressed`, `ring` geometry). Needs the Debug build's helper exe. |
| `TestHarness ▸ T_SiteShots` | Not a test, not in the default set: `launcher-shot` renders the Arcade Launcher for the site straight from the app's drawing code (`ArcadeChrome.DrawFrame` + `ArcadePickerRenderer.Draw`, as `ArcadeControl.RenderCore` does), Firefly registered from its sample folder and in front, Internode left out of the ring, onto a transparent 720² PNG at `packaging\webhost\assets\screenshots\workshop-arcade-launcher.png`. Repoints `Application.ResourceAssembly` to Radiata.dll so `pack://` art resolves in the harness. `launcher-shot-long` renders the same scene with Firefly retitled to a long two-word and a long one-word name, to `%TEMP%\radiata-launcher-long{1,2}.png`, to check the nameplate fitting. |
| `TestHarness ▸ T_Render` | The pixel-identity guard for drawing refactors, on demand and not in the default set. `render:snapshot <dir>` renders the whole matrix offscreen to one PNG per entry plus `manifest.json` (entry → SHA-256 of the raw Pbgra32 buffer); `render:compare <baselineDir> [outDir]` re-renders and FAILS every entry that differs or is missing, with its differing-pixel count and largest channel delta, writing `.actual.png` / `.diff.png` to outDir (default `%TEMP%\radiata-render-compare`). Pins `RadialMenuControl.PinnedClockUtc`, `FxRng` and `ArcadeSprites.PinnedTime` per entry; sets the render-without-display switch so a disconnected session still rasterises. ⚠ Baselines are machine-specific: compare only against a snapshot taken on the same machine and build configuration. |
| `TestHarness ▸ T_RenderWheel` | The wheel half of the render matrix: all eight materials plus the two sample drop-in themes × rest (every count × thickness), armed icon / logo / cover / long label, label modes, guarded dwell mid and done, every hub readout (status, preview, caption, cold-start, scrubber, skip glyph, notice, practice, edit hint, battery, mix bar), armed arcade slices, a firing linger, the held arcade collapse, edit mode (select, undo row, carry, carry moved, add, delete hold, ghost, Add picker), and the Reduce Motion frames that differ; then clock-driven effects at pinned instants (hub appear/disappear, collapse, glitter, slosh, flicker, twinkle, circuit sparks, confetti, spark bloom), degenerate 1- and 2-slice rings, an empty edit session, and the Settings preview bakes (`RenderPreviewIcon/Logo`, `RenderTintedLogo`, `RenderEdgedIcon`, `RenderOuterEdgedIcon`, the tile fills and decorations). Drives a real `RadialMenuControl` through its own API and `OnRender`. |
| `TestHarness ▸ T_RenderArcade` | The arcade half of the render matrix: each catalog game, fresh and after a scripted fixed-step input sequence, through `ArcadeControl.RenderCore` — play, △/START hint, window-move cues, ready beat, how-to card, pause menu, confirm prompt, game over where a run can end, the guard card — then the launcher (every cabinet in front, a swing with floor sparks, mid-launch, mid-return, a drop-in cabinet) and each game's `ArcadeShots.Capture` cabinet shot. Host layers are set on the control's fields, since its entry points play sounds. Drop-in script games are out: they run in the out-of-process helper. |
| `TestHarness ▸ T_RenderScreens` | Not a test, not in the default set, never part of the matrix: `render:screens <dir>` renders each arcade game's text screens — titles, end cards (Kabloom STUNG / campaign complete, Connate and PetalPop game over, PetalPop win), in-play shouts and notices (Kabloom growth banner and capacity card, Connate combo / board clear, PetalPop combo / +1 life / level, Internode pass / stage / short-by / ouch, checkpoint / level-up and promoted / demoted rows) — one PNG each through `ArcadeControl.RenderCore`, for art direction. States are reached by scripted input or by setting the game's own fields by reflection. ⚠ Replaces `ArcadeChrome`'s cached Share Tech typeface with one loaded from `Assets\fonts`, because the harness resolves the `pack://` font URI to the fallback family. |
| `TestHarness ▸ T_RenderToast` | The toast pieces the render matrix can reach without a window or a constructed App: `ToastPresenter.ToastBrushes` per material, `ToastPresenter.BuildToastRim`, and the ring and star geometry `RadialMenuControl` lends the toasts. The composed cards (`BuildAndShowToast`, `ShowRectToast`) are not covered — they are built and `Show()`n in one method. |
| `TestHarness ▸ T_Narration` | `Announcer` coordination (accessibility A1) against a fake `ISpeechSink` — no SAPI voice, so it runs headless on a machine with no audio endpoints: priority between `AnnouncementKind`s, selection-change debounce/coalescing, and that speech queued for a context which has since gone away never lands. |
| `ExtractStrings` | Rewrites `Core/UiStrings.g.cs` from the XAML, parsing it as XML (entities, property-element forms, wrapped attributes); fails on an unquoted `{loc:T}` carrying `, = { }` or a value XML normalization would alter. `--convert <file> [--dry]` rewrites one file's mechanical literals to `{loc:T …}` line by line and lists what a human must rebuild. Run from the repo root after any XAML string change. `XamlStrings.cs` holds the enumeration and the `{loc:T}` quoting rules and is compiled into `copy-deck` as well, so the two tools cannot disagree about which XAML attributes are strings. |
| `build-installer.ps1` | Builds `Radiata-<ver>-setup.exe` + `publish\latest.json`: staged Release publish (Radiata single-file, ArcadeHost self-contained in an `ArcadeHost\` subfolder), ISCC compile of `packaging\radiata.iss`, SHA-256. One-shot by hand; `-StageOnly` / `-FromStaging <dir>` split it at the signing point for `release.yml` (the staging folder carries a `staging-version.txt` record so both halves name the same version and flavour). Run by hand and by `release.yml` (with `-Public`, the public posture `Core\ReleaseGates.cs` reads); see [INSTALLER.md](INSTALLER.md). |
| `verify-signature.ps1` | Fails unless a file's Authenticode signature is Valid AND carries an RFC 3161 timestamp (an untimestamped Artifact Signing signature stops validating when its short-lived certificate expires); run by `release.yml` after signing, and by hand on a downloaded release asset. `-ExpectSubject` also pins the signer. |
| `public.gitignore` | The public repository's own ignore rules, published at the repository root as `.gitignore`. The development repository keeps a separate `.gitignore`, so a rule meant for the public repository is edited here. |

### Development repository only (not published)

Development repository only, not in the public source. They are listed because the published code is what they produce, check or constrain, and the traps below still apply to it.

- `UiaSpike` — A1 accessibility spike: does Narrator speak UIA events from an overlay-style window? Manual, Narrator on.
- `HidProbe` — characterizes an unknown controller (no `ControllerProfile` yet): enumerates a vendor's HID interfaces (default 8BitDo `2DC8`), dumps report descriptors, live-streams input reports with changed bytes bracketed. `--vid XXXX --all --seconds N --log file --enum-only`.
- `XInputProbe` — polls all XInput slots at ~250 Hz to verify what a game sees under the cloak (exactly 1 pad).
- `CloseProbe` — fake app that refuses close requests, proving the Toggle slice's graceful-close-then-kill sequence.
- `check-doc-links.ps1` — every relative Markdown link under a root resolves; point it at an exported mirror or a fresh clone after every export (exit 0 = clean).
- `ConnateProbe` / `KabloomProbe` / `PetalPopProbe` / `InternodeProbe` — executable contract suites (exact arithmetic, pacing, geometry, containment, snapshot posture) in place of a test framework. Core-only references; exit 0 = pass.
- `InternodeFrameProbe` — draws the real `InternodeRenderer` offline (a phrase-tester section parked at chosen depths, saved as PNGs) for before/after comparison of track drawing without launching the app; `PROBE_DUMP`, `PROBE_TIME`, `PROBE_TREE` and `PROBE_GPU` switch on diagnostics. ⚠ References the Debug `Radiata.dll` and copies it at build — rebuild the probe after Radiata.
- `ConnateBenchmark` — fixed-seed CPU/allocation and final-state digest comparison.
- `KabloomBake` — offline board bake for Kabloom levels 11+: random fields at the authored density, solved from every zero-clue cell, a covering set kept per level, hardest first. Rewrites `Core\Arcade\Games\Kabloom\KabloomBakedBoards.Data.cs`; `--report` prints the ramp table. Re-run after any crop or occupancy change.
- `ArcadePoc` — proof-of-concept for the Arcade script engine (Jint in an AppContainer'd, Job-Object-limited helper over a named pipe). `--all` = benchmarks + adversarial suite, `--phase2` = containment probes; exit 0 = pass. Gate cleared on Windows 10 and 11 in Aug 2026; superseded in production by `ArcadeHost\` (M1, Aug 16 2026) but kept as the adversarial/benchmark rig. ⚠ Jint lives ONLY here and in `ArcadeHost\`; keep it out of `ControllerWheel.csproj`.
- `LogoPromo` — screen-recording stage: the About-screen flower bloom (`AboutFlowerControl`) alone on a flat or transparent backdrop, borderless, with replay / loop / fullscreen keys. Promo footage only; runs nothing else from the app.
- `SfxIngest` — manifest-driven conversion of the licensed sound-pack picks into `Assets\sfx\arcade\*.wav` (licensed samples, not in the public source; trim, fades, peak, speed, reverse → 44.1 kHz stereo 16-bit) plus the `Assets\SFX-Edit\08 Arcade` audition copies and their README table; `manifest.json` beside it is the record of what every sample is. Idempotent; `--force`, `--only`, `--dry`.
- `MusicIngest` — re-encodes the Arcade's music beds for shipping: `Assets\music\source\*.mp3` (the creator's files, kept, not embedded) → `Assets\music\*.m4a`, mono AAC 44.1 kHz 64 kbps, through Windows Media Foundation (no MP3 sink writer exists on every Windows edition, and AAC sounds better at the size). Idempotent on file stamps; `--force`. Run after a new track lands in `source\`.
- `SpriteTemplates` — writes the blank guide PNG per sprite slot into `Assets\arcade\sprites\templates-unfinished\` (not published, and never ships), tracing each crop and footprint out of `Core`'s own geometry so a guide line is the line the renderer cuts on. Re-run after any change to petal, core, paddle or craft geometry.
- `SpriteShadowBake` — bakes a soft cast shadow from a sprite's ALPHA, one output frame per input frame, onto a padded canvas so the blur is not clipped by the source frame (`kabloom-bee` → `kabloom-bee-shadow`, sigma 20, pad 1.5×). Replaces a four-layer opacity-mask stack in the renderer that cost 54.7 ms/frame against 17.9 ms. ⚠ The output is DERIVED: re-run it after any change to the source frames, or the shadow animates under a body that has moved. Reports each frame's centroid drift, which is the check that matters.
- `il-identity.ps1` — proves an edit is comment-only: hashes `Radiata.dll` / `Radiata.Core.dll` / `Radiata.ArcadeHost.dll` from a deterministic, PDB-less Release build with a pinned build number, no commit hash (the SDK otherwise stamps HEAD's SHA into Core's and ArcadeHost's version string) and mapped source paths (so checkouts in different folders hash alike). Comments in `.cs` and `.xaml` never reach the IL or BAML, so runs on either side of a comment-only edit match; any code change moves a hash. Same calendar day only (the version carries the build date).
- `export-public.ps1` — builds the public mirror in a folder beside the repository from an **allow-list**: a file publishes only because `$include` names it, and `$publishAs` renames the few files that publish under another name (`public.gitignore` → `.gitignore`). Six guards cover host, identity (every address in the development history other than the public one is private; none is spelled out), a dirty tree, a moved remote, private content in published text (addresses, co-author trailers, citations of unpublished files, machine names), and whether the mirror actually compiles.
- `build-workshop-samples.ps1` — builds the Workshop's download zips (`packaging\webhost\workshop\samples\<name>.zip`) from `packaging\sample-material-package\*` and `packaging\sample-arcade-package\*`: files at the zip ROOT (Extract All yields a loadable folder named after the zip), fixed entry timestamps. Re-run after touching a sample; `TestHarness workshop` fails on a stale zip.
- `pack-arcade-assets.ps1` — zips the licensed Arcade sound set (`Assets\sfx\arcade\`, stored at its repo-relative path; the music is mirrored, not zipped) for `release.yml`'s private fetch, and prints the SHA-256 for the `ARCADE_SFX_SHA256` repo variable.
- `make-radiata-ico.ps1` — regenerates `Assets\radiata.ico` from `Assets\flower-mark-color-unique.png`.
- `make-wizard-bmps.ps1` — regenerates the installer wizard bitmaps in `packaging\assets\` from the flower mark on the OOBE cream; see [INSTALLER.md](INSTALLER.md).
- `copy-deck` — the copy-pass round trip ([LOCALIZATION.md](LOCALIZATION.md) §2 ▸ *Copy pass*): `export` writes one JSON deck of every authored English string (Help, `UiText`, XAML, the installer's `english.*` keys, the shell-side catalog sets) and every paragraph of the published prose and the three English marketing pages, each row carrying its exact source span and a per-file hash; `index.html` beside it is the editing page (single file, no server, opened from disk); `import` writes the edited rows back, re-encoding only what changed, and removes the four translations of a reworded string into `docs/TRANSLATION-WORKLIST.json` for the batched translate pass (a row ticked "typo only" is re-keyed instead; the installer's language lines are kept and listed as stale). `verify` proves a flagged deck still has its exported rows; `selftest` round-trips the encoders and proves an unedited import is byte-identical. Decks live in the gitignored `decks\`. ⚠ The importer refuses the whole import if any file changed since export, if a source span no longer decodes to the row's text, or if an edited row fails its kind's rules; it writes nothing on refusal.
- `md2rtf.ps1` — converts a Markdown checklist to RTF.
- `set-material.ps1` — plants legacy material tokens to exercise the rename migration.
- `watchdog-state.ps1` — read-only watchdog/cloak state probe for the ride-along watchdog tests; `-KillPrimary` / `-KillWatchdog` drive the hard-kill sequences.
- `test-uninstall.ps1` — two-phase check of the uninstall contract in [INSTALLER.md](INSTALLER.md) around the interactive uninstall: `-Phase pre` snapshots state, `-Phase post` verifies the Windows/controller footprint is gone while `%APPDATA%\Radiata` and the shared drivers survive. Read-only.
- `help-flip.pl` — rewrites a Help map so every bold UI path reads as the translated labels (from the UI catalog), and strips any leftover English gloss — [LOCALIZATION.md](LOCALIZATION.md) convention #1. ⚠ The gloss pass also drops a parenthetical that is merely a catalog value, like "(HidHide)"; read its diff. Idempotent; run per language after a UI label changes.
- `help-flip-labels.pl` — the same convention for what `help-flip.pl` skips: link labels that are a whole UI path, plain paths of two or more catalog segments, quoted readouts that are a whole catalog string, and "Settings" / "Game Grid" alone; then drops asides that only repeat a flip and fixes the Spanish article and the Japanese spacing round it. Leaves Steam's and Windows' menu paths alone. Dry run by default (`-old`/`+new`); `--write` applies. Run after `help-flip.pl`, then hand-fix single labels in prose.

---

## Everything else at root

| Where | What |
| --- | --- |
| `Assets\` | App art, fonts (Jua, ShareTech, SourGummy), `sfx\` WAVs (+ `sfx\xylophone\`), material images (Salvage set, `terra-ground.png`), `radiata.ico`, the `flower-mark-*-unique` brand art (SVG masters + PNG), `tile-logos*.png` (the Assets step's before/after tile row, plus its Playnite-only variant). |
| `Assets\arcade\` | `<gameId>-preview.png` per arcade game: the fallback picture behind the player's own captured shot (via `ArcadeShots`) for both the wheel hub and the picker's cabinet screen. Path built from the `ArcadeCatalog` id, so a new game needs a file, not a code change. The picker's **cabinet art** lives one level down in `sprites\sprites-finished\` as `cabinet-<gameId>.png` (+ `cabinet-blank.png`), read by `ArcadeArt.CabinetFor` — packed by that folder's wildcard, and deliberately not `ArcadeSprites` slots. **All cabinet art shares one 386×720 frame, silhouette and screen position**; the picker's `Screen*Frac` and `ArcadePickerRenderer.Outline` are measured against it. |
| `Assets\arcade\sprites\` | Maintainer-supplied in-game art, one PNG per `ArcadeSprites.Slot` name (numbered `-1`, `-2`… for an animation cycle). Shipped by a `Resource` glob, so new art needs a file and a rebuild, not a code change; `%APPDATA%\Radiata\arcade-sprites\` overrides it without one. `README.md` beside them is the artist-facing slot table — frames, fit and what each replacement drops. |
| `Assets\sfx\arcade\` | The Arcade's bespoke sample set, generated by `tools\SfxIngest` (development repository only) from purchased packs. **Licensed, not GPL, never mirrored** (THIRD-PARTY-LICENSES.md §4c); the release build fetches it privately (`release.yml`, `tools\pack-arcade-assets.ps1`). `.ingest\` holds the tool's fingerprints. |
| `Assets\music\` | The Arcade's music beds as shipped — `*.m4a`, mono AAC 64 kbps, written by `tools\MusicIngest` (development repository only) from the creator's originals in `source\` (kept in the repo, not embedded: the glob is non-recursive) — streamed track-at-a-time by `ArcadeMusic`. **Dylan Ribb, CC BY-SA 4.0, not GPL; the `*.m4a` are in the public mirror** (THIRD-PARTY-LICENSES.md §4d). |
| `xylophone\` (development repository only, not in the public source) | The *source* C6–C7 WAV samples; shipped copies live under `Assets\sfx\xylophone\`. |
| `drivers\` | Bundled signed Nefarius installers: ViGEmBus, HidHide, Legacinator; copied loose beside the published exe by the `CopyDriversToPublish` target. |
| `packaging\` | `README-Install-Radiata.txt` (ships in the ZIP) + `artifact-signing-setup.md` (code-signing setup steps; development repository only, not in the public source). Mechanics live in the csproj and [BUILD-RELEASE.md](BUILD-RELEASE.md). `radiata.iss` is the Inno Setup installer definition (version injected via ISCC /D by `tools\build-installer.ps1`; **fixed AppId GUID — never change it**; contracts in [INSTALLER.md](INSTALLER.md)). `webhost\` (development repository only, not in the public source, apart from `latest.json.sample`) is a snapshot of the live getradiata.app docroot plus its deployment kit: the hand-maintained marketing pages (root, `download\`, `tip\` — each English page with es/de/ja/ar siblings), `workshop\` (the package-authoring guide in five languages plus `workshop.css` and the `samples\` zips — held back with the package features: `noindex`, every nav link to it commented out), `styles.css`, `assets\`, the live `update\` (crash.php POST inbox, its `.htaccess`, the current `latest.json`; `crash-inbox\` and the `download\*.zip` installers are gitignored), the kit copies of crash.php and the `update\` `.htaccess` at the folder root, `docroot.htaccess` for the site root, latest.json.sample, DEPLOY.md, and `help\` — the GENERATED five-language Help site, re-exported with `--export-help-html`, never hand-edited. `sample-arcade-package\` and `sample-material-package\` (development repository only, not in the public source) hold the Workshop's samples: the sample game (`firefly\`, a short, complete, fully commented game — the reference for the script API) and two sample themes (`starter\`, format 1 commented; `ember\`, every format 2-3 block plus generated texture/WAV files). They are the source of the site's download zips (`tools\build-workshop-samples.ps1`) and are checked by `TestHarness workshop`. |
| `.github\` | The public repo's furniture, all exported: `workflows\tests.yml` (build + the three desk gates on every push/PR) and `workflows\release.yml` (tag → installer build, signing, draft release — [INSTALLER.md](INSTALLER.md)), `dependabot.yml` (keeps the SHA-pinned actions current), `FUNDING.yml` — the Sponsor button (GitHub Sponsors `scumbly`, Ko-fi `getradiata`; keep it in step with the Tip Jar links in `SettingsWindow.xaml` / `OnboardingWindow.xaml`, which point at `getradiata.app/tip`), `ISSUE_TEMPLATE\bug_report.yml` + `config.yml` (the bug form asks for the fields CONTRIBUTING.md names; the contact link points at the Help site), and `PULL_REQUEST_TEMPLATE.md` (carries the CLA agreement sentence as a checkbox). |
| `docs\` | The published topic docs: [ARCHITECTURE.md](ARCHITECTURE.md), [CODE-ATLAS.md](CODE-ATLAS.md), [INPUT-CAPTURE.md](INPUT-CAPTURE.md), [CONTROLLERS.md](CONTROLLERS.md), [OVERLAY.md](OVERLAY.md), [MATERIALS.md](MATERIALS.md), [SOUND.md](SOUND.md), [GAME-LIBRARY.md](GAME-LIBRARY.md), [ACTIONS.md](ACTIONS.md), [ARCADE.md](ARCADE.md), [PACKAGES.md](PACKAGES.md), [SETTINGS-UI.md](SETTINGS-UI.md), [ONBOARDING.md](ONBOARDING.md), [BUILD-RELEASE.md](BUILD-RELEASE.md), [INSTALLER.md](INSTALLER.md), [LOCALIZATION.md](LOCALIZATION.md), [MOTION-INVENTORY.md](MOTION-INVENTORY.md), [PC_GAME_TEXT_CHAT_BUTTONS.md](PC_GAME_TEXT_CHAT_BUTTONS.md) (the table `Core/GameChatButtons.cs` is generated from), and [README.md](README.md), the index. The development repository also keeps process ledgers (decision log, test checklists, work queue) that are not published. |
| Legal set (root) | `LICENSE` (GPLv3 + the §7 attribution/trademark supplement), `THIRD-PARTY-LICENSES.md` (**embedded resource** — the About tab's licenses viewer reads it, so it must stay at root), `TRADEMARK.md`, `CLA.md`, `CONTRIBUTING.md`, `SECURITY.md` (where to report a vulnerability privately; what is in scope), `PRIVACY.md` (public privacy statement: version-only update checks, ~30-day server logs, opt-in shown-in-full crash reports, nothing else). All seven publish to the mirror, as does `CONFIG.md` (the config.json schema, linked from the README; it may cite only published docs); the generated controls reference does not (the in-app Help, also published at getradiata.app/help, is the public reference). `export-public.ps1` (development repository only) copies only **git-tracked** files that its allow-list names, so a new root doc publishes only once it is both tracked and added to `$include`. |
| `build.minor` | The commit-bumped build counter (see Solution layout). ⚠ The pre-commit hook lives in `.git\hooks\` and is **not version-controlled** — a fresh clone silently stops bumping. |

---

## Weight map & traps

- **Five files hold ~41% of shell code:** `RadialMenuControl.cs` (~7.3k), `App.xaml.cs` (~7.3k),
  `OnboardingWindow.xaml.cs` (~3.7k), `WheelEditorControl.xaml.cs` (~2.6k), `GameLibrary.cs` (~1.9k). Expect to
  navigate by region banner, not by scrolling.
- **File-header doc comments are the best primary source** — most state the *why*, with dated
  decisions and incident references. Read the header before assuming a file's role from its name.
- **Generated, checked-in, no build step:** `CuratedArt.g.cs`, `CuratedArtOwner.g.cs` (via `--export-picker`
  / `--export-mypicks`), `CONTROLS.md` (via `--export-controls`), `packaging\webhost\help\*.html` (via
  `--export-help-html`), `GameChatButtons.cs`.
- **Dev-only:** the Radiata Picker export (`--export-picker`; `RadiataPicker.cs` holds the removal map
  spanning `GameArt.cs`, `App.xaml.cs`, `SliceLogoPickerWindow`).
