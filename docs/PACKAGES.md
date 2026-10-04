# Drop-in packages (custom Material themes; Arcade script games)

> ⚠ **Withheld from the PUBLIC release** (both kinds) through `Core\ReleaseGates.cs`: a build made with
> `build-installer.ps1 -Public` neither creates nor scans the folders below, so nothing downstream ever sees
> a package, the Customize "Custom" group stays collapsed, and the Workshop Help category (four topics) is dropped. A build made
> without `-Public` (a development build, or one built from source) keeps everything here as written.

The package system: everything built-in ships with the
app; **drop-in packages add content on top**, as conservatively as practical. Two folders exist from first
run (in builds that offer the feature):

```
%APPDATA%\Radiata\Packages\Materials\      ← live: data-only theme packages
%APPDATA%\Radiata\Packages\Arcade Games\   ← live: sandboxed script games (engine M1, Aug 16 2026)
```

Each folder carries a README.txt documenting its format — `PackageStore.EnsureFolders` keeps it current
on every launch (it's Radiata's documentation, not user content; edits are overwritten).

## Authoring guide and samples (the Workshop)

Held back with the features themselves until a later release. Four surfaces describe the formats and must agree:

- **The folder READMEs** (`PackageStore` — the short reference, including a GOTCHAS section).
- **The Help tab's Workshop category** (`HelpContent`: `workshop`, `custom-materials`, `custom-arcade-games`, `workshop-sharing`), translated like all Help.
- **The site's `/workshop/` pages** (`packaging\webhost\workshop\`; development repository only, not in the public source): the long-form guide, in five hand-written languages with one shared `workshop.css`. `noindex` and every nav link to it commented out until launch; its screenshots are `assets\screenshots\workshop-*.png`.
- **The cabinet and slice fields**: a game's optional `tint` colours its blank launcher cabinet, `preview` seeds its
  screen until the player's own capture exists, `badge` floats an illustration on its nameplate
  ([ARCADE.md](ARCADE.md) ▸ the picker), and `glyph` gives its Arcade slices their own icon — carried as the
  glyph NAME `ArcadePackage:pkg-<id>`, which `PackIconHelper` resolves to the package PNG as an alpha mask
  tinted like any MDI glyph, so no file path reaches config. Firefly demonstrates all four.
- **The samples**: `packaging\sample-material-package\{starter,ember}` and `packaging\sample-arcade-package\firefly`, zipped for download by `tools\build-workshop-samples.ps1` (all development repository only, not in the public source).

`TestHarness workshop` loads every sample through `PackageStore.Inspect`, plays each game through the real helper, checks the zips against their folders, and asserts the sandbox facts the guide states. **A change to a format limit, a sandbox budget or the script API updates all four surfaces and that group's guide checks.** Launching the Workshop: flip the package gates, uncomment the nav link on every site page and in `HelpHtmlExport.NavWorkshop`'s emitted line, swap the five home pages' **Expandable** card for the commented Workshop version beneath it (it adds the Workshop sentence and links the card), drop the pages' `noindex`, re-export Help from a `-Public` build, upload `workshop\`.

## Security posture — the load-bearing decisions

- **Material packages are data-only, by format — no code, ever.** `material.json` can express colors,
  gradients, a system font NAME, motion/glyph parameters, and (format 3) bare-filename-referenced
  images/sounds inside the package folder — never a code path. The parser
  (`Core\Packages\MaterialPackage.TryParse`) bounds every string, clamps every number, validates every
  color, and returns null+reason instead of throwing; a bad package is traced and skipped, never a crash.
- **Arcade packages are interpreter-run scripts, never assemblies — and the interpreter runs in a
  restricted helper process.** Modern .NET has no in-process sandbox (CAS/sandboxed AppDomains are gone),
  so a DLL could never be constrained, and no managed interpreter can enforce a hard memory cap or survive
  its own defects from inside Radiata.exe. The shipped engine (M1) embeds Jint exposing ONLY the Arcade
  surface (polar draw / input / size-capped KV store) — no file, network, process, or reflection
  primitives exist in the sandbox — inside a separate AppContainer'd, Job-Object-limited
  `Radiata.ArcadeHost.exe`, with a statement budget aggregated per RENDERED frame (the Arcade loop can run
  12 catch-up steps per frame — a per-step budget is a 12× budget). Engine detail:
  [ARCADE.md](ARCADE.md) ▸ Drop-in script games.
- **Stern consent, per package, per content.** `Core\Packages\PackageConsent` keys consent on
  kind+folder+SHA-256(manifest). First discovery → `PackageConsentWindow` (checkbox-armed accept;
  decline is default/Esc/Enter; topmost; one heading, "A {kind} package needs your approval", serves a new
  and a changed package alike). ANY content change re-prompts. Declines are remembered —
  a declined package re-prompts only when its content changes. Ledger: `packages-consent.json`
  (plain JSON, atomic writes, unreadable → nothing consented).
- **Scan is startup-only.** `PackageStore.Scan()` runs once in `App.OnStartup` BEFORE the ConfigLoader
  is constructed (Sanitize normalizes unknown tokens to Pearl — a consented theme must be registered
  first or the saved pick degrades), and `PackageInstallFlow` holds the run's inventory from then on.
  After startup the registries (`Materials.RegisterCustom`, `ArcadeCatalog.RegisterScripts`) change
  only by an approval (the startup prompt, or a drag-and-drop install) or an Uninstall, and **a token's
  content never changes within a run** — the renderer's resting-slice bake key and the Arcade caches
  key on the token string. Editing a package in place needs an app restart; the READMEs say so.

## Installing and removing

- **Drag-and-drop install** (`Core\Packages\PackageInstaller` + the shell's `PackageInstallFlow`). A
  folder or `.zip` dropped anywhere on the Settings window (window-level `PreviewDragOver`/`PreviewDrop`,
  claimed only when EVERY dropped path is package-shaped, so slice reorders, `.exe`/`.lnk` drops onto a
  wheel and the icon well keep their meaning), or handed to `Radiata.exe` on the command line (a drop
  onto the exe or a shortcut to it; the installer makes no desktop shortcut and Start-menu entries take
  no drops). A second launch carrying paths writes them to `Packages\.requests\<guid>.txt` and sets
  `Local\Radiata.InstallRequest` instead of the show-Settings signal; a cold start installs them right
  after the startup approval prompt.
- **Staging.** Top-level files only are copied into `Packages\.staging\<guid>\<folder>` (packages are
  flat by format), then `PackageStore.Inspect` validates them exactly as a scan would. Caps are counted
  on the bytes that arrive — 32 files, 4 MB each, 16 MB total, the `HashFolder` numbers — never on a
  ZIP's declared sizes. A ZIP holds its files at the root or under ONE top folder (`__MACOSX` ignored);
  deeper entries are skipped. Entry leaves come from `FullName` split on both separators and must be
  plain file names (no `..`, no `:`, nothing rooted), and each output path is re-checked to be inside
  staging. A source already inside `Packages\` is refused. `EnsureFolders` empties `.staging` at
  startup. Covered by `TestHarness workshop` (the `drop:` checks).
- **Approve before commit.** The consent window runs on the staged copy, already under its final folder
  name, so its folder + hash identity is the installed one. Decline → staging deleted and nothing
  recorded (a later manual drop-in still asks). Accept → moved into the kind folder, recorded Accepted.
  A package with an Accepted record for the same folder + hash installs without asking.
- **Conflicts** (same folder name, or ANY package on disk with the same token - found by a fresh scan at
  drop time, never the run's startup inventory) ask to Replace; every such folder goes to the Recycle
  Bin, so one id never has two folders. The installed folder keeps the DROPPED folder's name (consent is
  keyed on kind+folder+hash, and the name is what shows on disk). A token already registered this run
  with DIFFERENT content is installed on disk but not registered (the content-per-token rule): the user
  is told to restart, and the approval is already recorded; a further replace in the same run still
  finds and replaces that on-disk copy.
- **Uninstall** (materials only): right-click a Custom tile ▸ Uninstall theme. The folder goes to the
  Recycle Bin (`PackageInstallFlow.Recycle`, refused unless `PackageStore.IsPackageDir` — directly under
  a kind folder, not a reparse point); if it was the live material the pick moves to Pearl before the
  token is unregistered. The approval record is kept, so restoring the unchanged folder loads it
  without a prompt. Arcade games have no Uninstall surface (delete the folder, restart).

## How a custom theme renders

Token = `custom-<id>` (the prefix can't collide with built-ins or legacy aliases). The theme renders
through the **flat default paths** with substituted brushes — it can never reach a styled material's
procedural branches. Touch points, all additive:

- `RadialMenuControl.CustomTheme.For` bakes and caches (per token, for the run) a frozen brush/pen/
  texture set from the spec — decoded once regardless of how many call sites ask for it.
- `ShapeFill` / `ArmedFillFor` / `ConfirmFillFor` / `RestingPenFor` / `InkLabel` / `LabelFaceFor` /
  `PreviewFill` / the glyph-treatment flags / the armed-lift flags consult it first; absent → existing
  behavior byte-for-byte.
- `Materials.IsValid/IsDark/SoundThemeFor` are registry-aware, so ConfigLoader, glyph tinting
  (`ActionTint.TintSetFor`), and the sound pairing follow automatically.
- **`soundTheme` takes any sound SET or any BUILT-IN MATERIAL** (`MaterialPackage.ResolveSoundTheme`).
  Naming a set — `physical` / `digital` / `kawaii` / `mesa` / `salvage` / `reactor` / `obsidian` — pins
  it; naming a material (`mesa`, or `pearl` for digital, `flat-dark` for physical) borrows whatever that
  built-in currently pairs with, so the theme tracks the look it was styled after. Resolved AT PARSE, so
  the stored value is always a set name. Custom tokens are rejected: a theme cannot pair off another
  theme. ⚠ Validate before resolving — `Materials.SoundThemeFor` is total and answers "digital" for junk.
- A theme that ships sound files is **badged** on its Customize tile (bottom-right: white volume glyph
  on a black disc), and selecting it **takes over the Sound-effects picker** — the Digital/Physical
  override tiles go inactive (the badge is what explains why); **Themed** (the theme's own pairing —
  what plays for any event the theme doesn't override; its glyph shows a palette for a custom theme)
  and **Silent** stay live. Silent is a TOGGLE there (un-muting returns to Themed), because with the
  overrides disabled it would otherwise be a one-way door into silence.
- Settings ▸ Customize gets a third **"Custom"** group below Simple/Deluxe, collapsed when empty
  (`CustomizeEditorControl`). Each tile's button face is `PreviewFill` (the theme's resting fill, with
  its `tile.texture` composited over it if set — `CustomTheme.TileFill`); its decoration (bottom edge /
  sheen / lifted label) comes from the `tile` block, and its right-click menu holds Uninstall theme.
  The group is rebuilt whenever the registry changes (`PackageInstallFlow.RegistryChanged`), so an
  install lands in an open Settings window. Onboarding never offers custom themes.
- The Game Grid follows the theme's dark flag (flat card pair) instead of falling to Obsidian.
- An unregistered custom token (package removed / declined / unconsented) hits `Materials.Normalize`'s
  fallback and degrades to Pearl — same path as a legacy alias, no error surface. The stored token is NOT
  rewritten: `ConfigLoader.Sanitize` keeps it in `SystemConfig.HeldSliceMaterial`/`HeldGameGridMaterial`
  (never serialized) and the save writes it back while the material is still Pearl, so a public build (or
  a run with the folder missing) leaves the choice for a build that can resolve it. Any deliberate
  material write (a pick, the cycle, Uninstall, onboarding) releases it.

## Licensing

`LICENSE`'s additional-terms section c) (GPLv3 §7 additional permission): packages loaded through the
documented interfaces are not covered works — package authors (including the maintainer) may use their own
terms. Added while sole author, deliberately.

## Format roadmap — the expressiveness ladder

**Principle: flexibility through a rich HOST vocabulary, never package code.** The app implements each
effect/behavior once, in C#; packages select and parameterize from that menu. The ceiling of what a
package can express deliberately stays below the hand-built materials.

**Format 2 — rich brushes, motion params, glyph treatments (zero decoder surface):**
- Fills as `{type: solid|bowed|linear, stops[]}` per state — Pearl/Obsidian are already just bowed
  ramps fed to `BowedGloss()`, so this is exposure, not invention.
- Kawaii-style per-slice hue walk as `{sat, light, hueOffset}` — the walk is host code, the numbers are
  the package's.
- Armed motion: `{liftPx (clamped ≤24), northPx (±12), scale (≤1.15)}` — the same transform as
  Terra/Kawaii's lift. Reduce Motion classification matches theirs ([MOTION-INVENTORY.md](MOTION-INVENTORY.md): essential,
  **kept** — a static state treatment, not continuous motion).
- Pens (width/color/dash per state), inter-slice gap, corner rounding; label casing / size-mul /
  spacing / drop.
- Glyph treatments: `{edge: inner|outer|none + color/width, glow{color,strength}, castShadow,
  armedWash}` — parameterizes the existing generic icon-edge/blur bake infra (`DrawInnerEdged`/
  `DrawOuterEdged`/`RimMask`) and the Salvage glow/shadow passes. The one fiddly renderer change: the
  seven-predicate shadow/wash block in `DrawSlice` (~5232–5334, duplicated across the logo and icon
  branches) becomes parameter-driven.

**Format 3 — textures + sounds (SHIPPED Aug 2026; first decoder surface, contained):**
- Textures (slice/hub/backdrop): PNG/JPEG only, ≤4 MB file, header-probed then decoded with a 2048px
  cap only when oversized (decompression-bomb guard without upscaling small tiles), decode-once +
  Freeze, fail-quiet per file (theme still loads, texture skipped, traced). File references are **bare
  file names by grammar** (`MaterialPackage.FileNameIsValid`) — traversal is unrepresentable, not
  checked-for. Risk framing: `GameArt` already feeds internet-fetched covers to the same WIC codecs —
  consented local files are not a new risk class. slice/hub textures paint over the fill (the Salvage
  grain pattern); backdrop draws circle-clipped behind the wheel (the Terra blob pattern).
- Sounds as an event map (`armed`/`fired`/`enableWheels`/`disableWheels`): WAV only, ≤1 MB at scan,
  **decoded + resampled to the mix bus format at LOAD, truncated at 3 s of samples**
  (`SfxEngine.LoadFile`, the same chain as `LoadResource`) — a complete pre-decoded buffer can't
  short-read, which is the `MixingSampleProvider` kill-the-bus hazard. A broken wav decodes to an
  empty buffer and the THEME's sound plays instead (`Sfx.Custom`, applied in `SetMaterialFlags`).
  Melody sequencing (the Kawaii xylophone) stays host-only.
- The consent content-hash covers **every top-level file** in the package (sorted name+bytes,
  `PackageStore.HashFolder`) — a texture or wav swap re-prompts exactly like a manifest edit — and the
  resting-slice bake key carries the hash. Folder shape caps: ≤32 files, ≤4 MB each, ≤16 MB total.
- Manifests may carry a UTF-8 BOM (Notepad/PowerShell write one); the scanner strips it.

**Never in packages:** fonts as files (font parsing is the nastiest parser family on Windows and the app
has zero existing exposure — system-font NAMES only, permanently); dwell-replacement programs, particle
systems, generated wedge silhouettes, the circuit board (programs, not parameters). The migration path
for behaviors: the host generalizes one (e.g. "bottom-up pour" as a selectable dwell style with a color
param) and adds it to the menu.

**Versioning:** `format: 2` etc.; new blocks optional with today's behavior as defaults; an older app
rejecting a newer format is the correct, explicit failure (TryParse already rejects unknown formats).

## The Arcade engine milestone — **M1 SHIPPED (Aug 16 2026)**

The M1 build follows this design point for point.

**What M1 ships:** `ArcadeHost\` (the Jint helper, the only production project referencing Jint, pinned
4.16.0), `Core\Arcade\Script\` (wire protocol + validator, `game.json` manifest, the `ScriptArcadeGame`
adapter), `Arcade\ScriptHost\` (AppContainer + Job Object jail, ACL'd-pipe session with the 2 s wall-clock
kill and ≤3 restarts, the shared script renderer), package scan/consent activation for the Arcade Games
folder (whole-folder hash, code-worded consent copy), the folder README carrying the complete JS API, and
a committed reference package (Orbit; retired Sep 22 2026 — the Workshop's Firefly sample, above, is the reference game now). Script games appear beside
Kabloom/Connate in the picker and slice editor only when `Arcade.Available`; kv (4 KB, host-mediated) is
the only state surviving a dismiss.

**Deferred past M1 (deliberately):** LPAC; a Job Object CPU-rate cap (the wall-clock kill stands in);
long-soak testing; melody/sound FILES in packages (the 9-name cue vocabulary only); per-game pause-menu
options; localized consent copy; script-authored how-to art (text bullets only). The installer ships the
helper **self-contained** in its own `ArcadeHost\` subfolder, so script games need no .NET runtime on the
machine; only a dev build carries the loose framework-dependent copy beside the exe (INSTALLER.md ▸ payload
layout). ⚠ Unverified on a machine with no .NET installed.

The original design points, for the record:

- **Runtime: Jint** (JavaScript, pure managed, no `AllowClr`; license entry required in
  THIRD-PARTY-LICENSES.md). ⚠ Jint is referenced only by `ArcadeHost\`; never add it to `ControllerWheel.csproj`.
- **Boundary: a separate `Radiata.ArcadeHost.exe`** in an AppContainer under a Job Object (memory limit,
  one process, kill-on-job-close). The Job Object is the memory cap — Jint's `LimitMemory` is only a
  burst tripwire. Helper death of any kind never touches the process holding the cloak and virtual pad.
- **Protocol:** one bounded named-pipe request per rendered frame (input snapshot in, validated
  draw-command buffer out); the helper runs the fixed steps internally. The WPF UI/render path never
  blocks on the helper; repeated misses restart it into a guard card.
- **Budget:** instruction/time allowance aggregated per RENDERED frame, never per fixed step (12 catch-up
  steps/frame otherwise silently 12× the budget). Runaway = game paused with a message, never a frozen app.
- **Games get** draw/input/tick and a per-game size-capped KV store only; every boundary value validated
  (NaN/range/length/count/depth caps); consent gate identical to materials but worded for code;
  `game.json` hashed for consent identity (PackageStore already probes it).
- **Gate CLEARED (Aug 2026):** the POC (`tools\ArcadePoc`, development repository only, not in the public
  source; `--all` + `--phase2`) passed on Windows 10 AND Windows 11 — p99 < 5 ms at 4× expected load, 11/11
  adversarial cases contained, all 9 AppContainer denial
  probes denied on both OSes; full numbers in its `RESULTS.md`. Production carry-overs from
  the POC: explicit pipe buffer sizes (0-byte default can block the HOST's write); host wall-clock kill
  is the only reliable stop for catastrophic regex (Jint's `RegexTimeout` didn't fire); JIT warm-up pass
  at game load or the first frames blow the deadline; the helper must be installed under a path readable
  by the AppContainer SID (Program Files is; user-profile paths are NOT); the pipe ACL names exactly the
  derived container SID; child-process denial rests on the Job Object (System32 is container-readable).
