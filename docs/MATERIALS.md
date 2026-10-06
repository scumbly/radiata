# Materials (wheel looks)

A material is the wheel's visual style, stored as `system.sliceMaterial`. `Core/Materials.cs` is the single
source of the vocabulary; the looks themselves are drawn by `Overlay/RadialMenuControl*.cs`.

## The vocabulary lives in one place

The Customize tiles, the onboarding Look step and every consumer that depends on light or dark (glyph tinting,
the icon-well preview, the Game Grid tone) query `Materials` instead of testing material names inline, so a
new material cannot fall through a hardcoded `is "flat-dark" or "obsidian"` check.

`Materials.Normalize` is the one token resolver and lives in Core so `ConfigLoader.Sanitize` can canonicalize
on load. It is total: any stored token that is not a canonical material, including alternate spellings,
maps to a canonical one, and unknown input becomes Pearl. Nothing on disk is rewritten until the next save.

## The eight materials

In Customize tile order (a 4x2 grid):

| Token | Name | Look |
| --- | --- | --- |
| `flat-light` | Flat Light | Solid light |
| `pearl` | Pearl | Glossy light |
| `flat-dark` | Flat Dark | Solid dark (the default) |
| `obsidian` | Obsidian | Glossy dark |
| `kawaii` | Kawaii | Pastel wedges whose hue walks the ring, a cloud hub, confetti on fire |
| `salvage` | Salvage | Charcoal with a rusted-metal texture and stamped plate edges |
| `mesa` | Mesa | Cream hand-cut wedges over a terracotta backdrop |
| `reactor` | Reactor | Hollow outlined wedges; the armed slice fuses with the hub; parallax circuit boards and sparks |

The C# constants `GlossLight`, `GlossDark` and `Terra` hold the values `pearl`, `obsidian` and `mesa`. Match
the constants, never the token's shape (a `StartsWith("gloss")` test matches no token).

## Predicates

- `IsDark`: Flat Dark, Obsidian, Salvage, Reactor. It drives glyph-tint lightening, the editor's icon-well
  preview and the Game Grid tone. Kawaii and Mesa read light.
- `IsPremium` answers only for the four styled materials (Kawaii, Mesa, Salvage, Reactor), which have their own
  textures, hubs, dwell animations and sounds. It is not the same set as the Deluxe group in the pickers
  (those six add Pearl and Obsidian); the grouping lives in `OnboardingWindow.IsSimpleMaterial`.
- `SoundThemeFor` is the one copy of the material-to-sound pairing: Kawaii, Mesa, Salvage and Reactor each have
  their own set, Pearl is `digital`, Obsidian is `obsidian`, the flats are `physical`
  ([SOUND.md](SOUND.md)). Picking a material tile also sets `soundTheme` to `material`, so the sound follows
  the look.
- The in-wheel Add picker ignores the user's material: it always draws Flat Dark with the dark tint set.

## Rendering constraints

- The wheel body is opaque: every wedge, hub and disc is backed by a solid plate of its own shape.
- Resting slices are bitmap-baked (`RadialMenuControl.EnsureSliceBakes`). The bake key covers layout,
  material, wheel side, DPI, label mode and a per-slice style hash, so a new resting-slice visual must be
  derivable from that key or drawn in the live layer, or it freezes in the bake.
- On Pearl and Obsidian a guarded slice keeps full opacity and recolours toward the confirm red
  (`GlossDwellFill`); the two ramps in a pair must keep matching stop counts and offsets.
- Reactor's boards are cached per slice. `system.reduceMotion` pins the parallax and stops sparks, twinkles
  and confetti ([MOTION-INVENTORY.md](MOTION-INVENTORY.md)).
- Code-created `BitmapImage`s must use the full `pack://application:,,,/...` URI. A relative `UriSource`
  resolves against the exe's folder, not the compiled-in resources, and fails on every PC.
- Display faces: Mesa uses Jua, Reactor Share Tech, Kawaii Sour Gummy (all bundled, SIL OFL 1.1, listed in
  [../THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md)), and Salvage uses Bahnschrift SemiCondensed from
  Windows with a Segoe UI fallback. The bundled faces ship Regular only, so they are pinned to Regular
  (synthesized bold distorts them) and are built lazily, because `pack://` is not registered until the WPF
  `Application` exists.

## Game Grid

The Game Grid draws with the wheel material's own treatment: `App.ShowBrowserAsync` passes `SliceMaterial` to
`SetGamesMaterial`, and `GameBrowserControl.SetMaterial` has a per-material look.

## Faces and non-Latin languages

The display faces are Latin-only. When the UI language is Japanese or Arabic (`Loc.UsesSystemFace`), the
wheel's labels and hub (`LabelFaceFor`, `HubFace`) and the Customize previews (`ApplyPreviewLabelFace`)
use the language's system font chain (`LocWpf.LanguageFamily`) instead; the material keeps its surface,
colours, casing and motion and only the letterforms change. Bundled fonts stay Latin by rule: system fonts
only for other scripts.
