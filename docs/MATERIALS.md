# Materials (wheel looks)

**Read this when touching:** `Core/Materials.cs`, `RadialMenuControl`'s render path, the Customize material
tiles, the onboarding Material step, or `GameBrowserControl`'s per-material treatment.

## The vocabulary lives in one place

`Core/Materials.cs` is the single source of truth for the material vocabulary (config value
`SystemConfig.SliceMaterial`). The renderer's looks live in `RadialMenuControl`, but the tiles, the
onboarding step, and every dark/light-dependent consumer (glyph tinting, icon-well preview, Game Grid tone)
**query `Materials`** so a new material can't silently fall through someone's hardcoded
`is "flat-dark" or "obsidian"` check.

Beyond the eight built-ins, `Materials` also carries the per-run registry of **consented drop-in theme
packages** (`custom-<id>` tokens; palette-only, rendered through the flat default paths) — see
[PACKAGES.md](PACKAGES.md). `IsValid`/`IsDark`/`SoundThemeFor` consult the registry, so everything
downstream is custom-aware by construction; an unregistered custom token degrades to Pearl exactly like
a legacy alias. ⚠ In a **public release** the registry is always empty: `PackageStore` never scans the
Materials folder there (`Core\ReleaseGates.MaterialPackages`), so the Customize "Custom" group never shows and
the Workshop Help topics (including `custom-materials`) are dropped.

`Materials.Normalize` is the one legacy-alias resolver and lives in Core — not the WPF shell — so
`ConfigLoader.Sanitize` can canonicalize **on load**. That's what makes the dozens of raw `mat is "mesa"`
comparisons downstream correct by construction. Unknown input falls back to Pearl (total function, no
null-checks needed at call sites). Nothing on disk is rewritten until the next save.

## The eight, in Customize tile order (a 4×2 grid)

| Token | Display name | Notes |
| --- | --- | --- |
| `flat-light` | Flat Light | classic solid light |
| `pearl` | **Pearl** | glossy light (bowed gradient "glass" ramp) |
| `flat-dark` | Flat Dark | classic solid dark |
| `obsidian` | **Obsidian** | glossy dark |
| `kawaii` | Kawaii | pastel "dream sky" wedges (hue walks the ring), cloud hub, confetti fire |
| `salvage` | Salvage | charcoal + rusted-metal texture, stamped stepped-plate edge, armed icon glow + cast shadow |
| `mesa` | **Mesa** | cream hand-cut wedges over a warm terracotta backdrop |
| `reactor` | Reactor | hollow outlined wedges; the armed slice fuses with the hub (ripple), parallax circuit board, sparks |

Pearl/Obsidian are the glossy light/dark pair; Flat Light/Flat Dark are their solid counterparts.

On **Pearl and Obsidian** a guarded ("hold to confirm") slice keeps FULL opacity for the dwell and
recolours instead: `GlossDwellFill` lerps the resting gloss ramp to the confirm-red ramp stop-for-stop as
the hold fills. The red progress arc still rings the slice — only the shared arming-transparency ramp is
displaced, which is why the flag sits outside `ownDwell` in `DrawSlice`. The two ramps in each pair must
keep matching stop counts and offsets or the lerp mismatches.

**Renamed Aug 3 2026:** `gloss-light` → `pearl`, `gloss-dark` → `obsidian`, `terra` → `mesa`. The
**tokens** changed, not just the labels. The C# member names deliberately still read `GlossLight` /
`GlossDark` / `Terra` so the file stays greppable against the old vocabulary — only the values moved.

**Legacy aliases resolved forward on load:** `gloss-light`, `gloss-dark` (+ `gloss-black-a/b/c`), `terra`,
`paper`, `stencil` (Salvage's pre-rename token), `sparkle`, `frost-light`, `frost-dark`.
`IsDark` and `IsPremium` also accept the legacy spellings, because a material can arrive from somewhere that
never went through `Sanitize` (a raw `SystemConfig` built in code, a test).

## Predicates worth knowing

- **`IsDark`** — Flat Dark, Obsidian, Salvage, Reactor (+ legacy `stencil`, `gloss-dark`). Drives glyph-tint
  lightening, the slice editor's icon-well preview, and the Game Grid tone. Kawaii and Mesa read light.
  Getting this wrong on an un-migrated config gives dark wedges with light-material glyph tints.

  ⚠ **The in-wheel Add picker is exempt from the material entirely** — it always draws **Flat Dark**, whatever
  the user's material, and its glyphs bake with `ActionTint.TintSet.Dark` so no per-material colour variant is
  applied to them. The picker is a system surface rather than the user's wheel, and a styled material's
  transform (Kawaii's pastel walk especially) makes the category glyphs hard to tell apart. User-placed slices
  still take their material's variant; `App.PopulateIcons` takes an optional `tintSet` override used **only**
  on the picker path.
- **`IsPremium`** — the four **styled** materials: Kawaii, Mesa, Salvage, Reactor. The 2026 looks with their
  own textures, hubs, dwell animations, and sound pairings, as opposed to the four classic flat/gloss ones.
  Their preview tiles share a treatment (5 px coloured bottom edge, 3 px lifted label), so anything that
  should apply to "the styled four" asks here rather than re-listing them.

  ⚠ **The predicate's name is narrower than the product term.** The user-facing group is called
  **Deluxe** (renamed from "Premium" Aug 9 2026, alongside "Basic" → **Simple**) and it holds
  **six**: Kawaii, Mesa, Salvage, Reactor **plus Pearl and Obsidian** (since Aug 3 2026; the glossy
  pair used to be grouped with the flats). `IsPremium` still answers the FOUR, because what it actually
  drives is the styled-look tile treatment and per-material render branches — Pearl and Obsidian have
  neither. The **Simple** group is the two flats, and the grouping predicate lives in
  `OnboardingWindow.IsSimpleMaterial` (deliberately wider than `IsPremium`; Settings ▸ Customize mirrors the
  same two group boxes). Read `IsPremium` as "has a styled look", not "is in the Deluxe group".
- **`SoundThemeFor`** — the sound SET a material resolves to when `soundTheme` is `"material"` (the
  default): Kawaii/Mesa/Salvage/Reactor each ship their own set, Pearl → `digital`, Obsidian → `obsidian`
  (Digital pitched down 0.84), the flats → `physical` (see [SOUND.md](SOUND.md)). Returns a resolved set
  name, not a storable `soundTheme` token.
  This lives in Core because both the onboarding Material step and Settings ▸ Customize used to keep a
  private copy of the pairing — one of which **silently inverted** during the Aug 3 2026 rename, because
  its test was a `StartsWith("gloss")` prefix and no canonical token starts with "gloss" any more.
  Matching the `GlossLight`/`GlossDark` constants means the next rename breaks the **build**, not the
  behaviour.

`soundTheme` snaps back to `"material"` (follow the material) when `sliceMaterial` changes; an explicit
digital/physical pick holds only until then. (The old `soundFxManual` latch that made a manual pick
permanent was removed Jul 2026; the key is ignored if still present.)

## The Game Grid follows the wheel material

`SystemConfig.GameGridMaterial` is **temporarily ignored**: its Customize tiles were
removed, and `App.ShowBrowserAsync` passes `SliceMaterial` straight to `SetGamesMaterial`. The field is
kept in config and mirrored on save for a future re-enable. The grid is not "mapped" onto a classic
material — `GameBrowserControl.SetMaterial` has its own per-material treatment (Kawaii sparkle field and
thicker select ring, Mesa lift + card edge, Salvage layer opacity + select glow, Reactor's animated board
layer instead of a brush).

## Render-cost notes

- **The wheel body is opaque** — every wedge/hub/disc/blob is backed by a solid plate
  of its own shape; gaps and the empty centre stay transparent.
- **Resting slices are bitmap-baked** (`RadialMenuControl.EnsureSliceBakes`): steady-state frames blit ≤12
  frozen bitmaps and draw only the armed slice, dome, hub, and fx live. The bake key covers layout,
  material, wheel side, DPI, label mode, and a per-slice style hash (icon/logo identity, label, colour) —
  a new resting-slice visual must be derivable from that key or drawn in the live layer, or it will freeze
  in the bake. Edit/assign/collapse bypass the bakes.

- Reactor's circuit boards are cached **per slice** (~4 ms each, built on the UI thread) with a second
  plane behind them deepening the parallax stack. `SystemConfig.ReduceMotion` pins the parallax still —
  in the wheel and in the Game Grid.
- Salvage's rivets are four per slice, one inset from each wedge corner; hole spots per slice-count are
  computed once, on the render thread only.
- The wheel drop-shadow variant is keyed on `HideCenter` alone — that fixed the shadow changing when a hub
  appeared.
- **Per-material typography.** Three bundled display faces, all OFL 1.1 and embedded
  — see [THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md) §7 — plus one Windows system font:

  | Material | Face | Applies to |
  | --- | --- | --- |
  | Mesa | **Jua** | slice labels + preview tile |
  | Reactor | **Share Tech** | hub text + slice labels + preview tile |
  | Kawaii | **Sour Gummy** | slice labels + hub text + preview tile |
  | Salvage | Bahnschrift SemiCondensed (system, falls back to Segoe UI SemiBold if absent) | slice labels (upper-cased) + preview tile |

  The **preview-tile** column is one shared helper, `RadialMenuControl.ApplyPreviewLabelFace`, called by both
  the Customize grid and the onboarding Look step so a tile always samples its own look. It runs **after** the
  `TextBlock` initializer because it must override the tiles' `SemiBold`: all three bundled families ship
  **Regular only**, and WPF's synthesized Bold wrecks them (Share Tech's hard corners smear, Jua's and Sour
  Gummy's rounded terminals go blobby) — so every one is pinned to Regular. Sour Gummy additionally ships as
  the upstream **variable** font, so the family also advertises Thin…Black and an Oblique set; the weight and
  style are named explicitly to keep WPF off them.
  Those tile names are **+10% size and pushed 3px down** (Salvage 4 — upper-cased, so no descenders to
  balance) to match the plain tiles' Segoe UI baseline. ⚠ **`ApplyPreviewLabelLift` is the sole owner of the
  label's `RenderTransform`** and re-runs on every selection change, so the drop has to be folded in *there*,
  net of the premium 3px lift — putting it only in `ApplyPreviewLabelFace` looked correct, built correctly,
  and was silently reverted the first time a tile was selected (i.e. always, since `Load` selects one).
  All three bundled typefaces are built **lazily**: `pack://` isn't registered until WPF's `Application`
  exists, so a static field initializer would throw `TypeInitializationException` and take
  `RadialMenuControl` down with it.
- ⚠ **Code-created `BitmapImage`s must use the FULL `pack://application:,,,/...` URI.** A relative
  `UriSource` (`new Uri("Assets/x.jpg", UriKind.Relative)`) resolves against the **site of origin** — the
  exe's folder on disk — not the compiled-in resources, and the assets have no loose copy under `bin`, so
  the load throws on every machine. That plus a silent `catch` is exactly how the Salvage preview tile
  shipped bare from Aug 2–6 2026 while the wheel (whose `LoadFrozenBitmap` always used the pack form)
  stayed textured. XAML `Source="..."` attributes are unaffected — the BAML parser supplies a BaseUri.
- **Kawaii's slice labels are inked in each slice's own authored colour** (`ActionTint.AuthoredColor` — the
  swatch pick or the typed hex, with *none* of the material's pastel transform), cached per colour. That is a
  deliberate look, not an accessible default: it replaced the ≥7:1 deep-plum `KawaiiInk`, which stays as the
  fallback for anything with no slice behind it (the hub readout). Contrast on the milky wedge is whatever the
  authored colour gives.
- Neither Sour Gummy nor Share Tech has a `LabelSizeMul` correction yet (Mesa's Jua does, at 1.08) — if
  Kawaii's or Reactor's labels read small or large against the others, that knob is where it belongs.
- Per [LOCALIZATION.md](LOCALIZATION.md) phase 4: the premium display fonts have **no CJK glyphs**, so per-material fonts need a
  documented fallback before the UI is ever translated. Jua covers Latin + Hangul but no kana/kanji, so it
  does not narrow that gap — Mesa labels in Japanese will still need the fallback.

## Retired looks (don't reintroduce as new)

`frosted` → replaced by **Kawaii** (Jul 2026); `stencil` → rebuilt as **Salvage**; `paper` → `terra` →
**Mesa**. Their tokens migrate forward; the looks themselves are gone.

## Faces and non-Latin languages

Every styled material draws its labels and hub in a Latin-only face: Mesa in Jua, Kawaii in Sour Gummy,
Salvage in Bahnschrift SemiCondensed, Reactor in Share Tech; a custom package may name a face too. None
carries Japanese or Arabic. When the app language is one of those (`Loc.UsesSystemFace`), the wheel's
`LabelFaceFor` / `HubFace` and the Customize tab's `ApplyPreviewLabelFace` draw in the language's own system
chain (`HelpLocalization.Language(code).FontFamily`, via `LocWpf.LanguageFamily`) instead — the material keeps
its surface, colours, caps rule and motion, and only the letterforms change. This is a per-material constraint,
not a fallback WPF is trusted to make: a missing glyph would otherwise resolve to whatever font Windows picks,
differently per material. Bundled fonts stay Latin by decision (system fonts only, never a fourth embedded
face). See [LOCALIZATION.md](LOCALIZATION.md) §3 ▸ Layout hardening.
