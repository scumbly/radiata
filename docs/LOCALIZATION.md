# Localization

The whole app (Settings, dialogs, onboarding, overlay, narration, tray, arcade, Game Grid) and the Help topics
are available in English, Spanish, German, Japanese and Arabic. Translations are produced by a language model
and the app says so; English is authoritative. Code: `Core/HelpContent.cs`, `Core/HelpLocalization.cs`,
`Core/HelpText{Es,De,Ja,Ar}.cs`, `Core/Loc.cs`, `Core/UiText*.cs`, `Core/UiStrings.g.cs`.

An untranslated string renders in English.

## 1. What is translated

- Help topics, headings, keywords and illustration labels: `HelpContent.cs` and `HelpFigures.cs` (English,
  canonical) plus the four `HelpText*.cs` maps. The Help pane's own chrome is `HelpLocalization.Chrome`.
- Everything else (tabs, buttons, wheel labels, onboarding, tray, toasts, dialogs): `Core/UiText.cs` constants and
  `Core/UiStrings.g.cs` (XAML), with the four `Core/UiText*.cs` maps (section 3).
- The Markdown export of the Help (`--export-controls`) is English only.

The language is `system.language` (null follows the Windows display language when the build offers it), sanitized
on read through `HelpLocalization.Normalize`. Non-English Help carries a translation notice as its first
block (`HelpLocalization.TranslationNotice`).

**Maps are keyed by the English text itself.** An English edit changes the key, so the string falls back to
English instead of showing a translation of the old wording. Topic ids, categories, order and block kinds always
come from the English source, so a translation can never reshape the Help tab. Search keywords are the union of
the translated and English sets.

Conventions for every new string: UI names appear as the user sees them, taken from the UI catalog, with no
English gloss; markup and tokens (`**bold**`, `` `code` ``, `[[topic-id|Label]]`, `{invoke}`, `{cross}` and the
like) survive verbatim, and only the label after `|` in a cross-link is translated; chord tokens stay at the end
of the sentence; informal address and plain wording (`TestHarness locregress` enforces the register); numbers,
key names and file formats are not localized; figure labels are short, and button names inside a figure are
`Literal` in `HelpFigures`.

### Direction glyphs in right-to-left text

Glyphs that point along the reading direction are flipped; glyphs that name a physical direction are not. The
D-pad arrow pair names physical buttons, so every language stores it in English order and the renderers keep it
left to right (`InlineMarkup.IsolateArrows`). Translation values must contain no bidi control characters, no
Arabic-Indic digits and no Arabic presentation forms.

## 2. Keeping translations current

A translation pass starts from the parity reports:

```
Radiata.exe --check-help-locales help-locales.txt
Radiata.exe --check-locales locales.txt
```

`MISSING` is a source string with no translation (new, or the edited form of a string whose old key is now
`STALE`); `STALE` is a translation whose English key does not exist. A finished pass reads `0 missing, 0 stale`;
`tests.yml` fails on `MARKUP`, `PLURAL` or `STALE`.

- `--dump-help-locale <code> [path]` and `--dump-ui-locale <code> [path]` regenerate a map as C# source in
  source order, keeping translations whose English key still exists and writing blank `TODO` values for the rest.
  Use them when a map has drifted far enough that patching by hand is error-prone.
- Copy the English C# literal across as the key, escapes included; it must match byte for byte. Translate the
  keyword line too, and a new category name in all four maps.
- The reports cover every authored string (`HelpLocalization.SourceStrings` walks `HelpContent.AllAuthored`).
- `help-locales.txt` and `locales.txt` are scratch output; do not commit them.

### Web Help site contract

`Radiata.exe --export-help-html [dir] [--css-version=N]` renders the Help for the website in every language
through `Core/HelpHtmlExport.cs`, from the release build's staged exe. The page contract the exporter reproduces:

- The shared site navigation first in `<body>`, with the language switcher inside it, and the shared footer.
- One `rel="alternate" hreflang` link per language plus `x-default`; `dir="rtl"` on the Arabic page, an explicit
  `direction` on every SVG label, and the side-specific CSS rules flipped.
- The sticky nav's offset on every anchor target and on the contents rail; the shared favicon file (never an
  inline data URI) and the shared Ko-fi loader.
- No glyph from the Supplemental Arrows-C, Geometric Shapes Extended or Symbols for Legacy Computing blocks:
  `HelpHtmlExport.ForWeb` swaps the D-pad arrows for plain arrows and throws on anything else from those blocks.

`TestHarness locregress` checks these against the site pages when they are present.

## 3. Localizing the whole app: the `Loc` layer

- **The language is fixed for the run.** `Loc.Init` runs once, before any thread exists, and sets
  `CurrentUICulture`. A language change in Settings prompts a restart; onboarding is the one exception. There is
  no change event: the maps are read-only, so `Loc.T` is safe from any thread and the wheel's slice bakes can
  never hold a stale language. `CurrentCulture` is untouched, so config, paths and hex parse invariantly.
- **Drawn surfaces never flip** ([OVERLAY.md](OVERLAY.md)). Right-to-left languages shape their text runs; the
  geometry stays.
- `Loc.T(english)`, `Loc.F(english, args)` and `Loc.P(one, other, n, extra...)` are the lookups (a malformed
  translated format falls back to English). XAML uses `{loc:T Some label}` for short labels and
  `loc:LocRich.Source` for prose with inline markup; labels containing `,`, `=`, braces or an apostrophe use
  `LocRich.Source`. `InlineMarkup` is the one markup vocabulary. `UiText` holds every C#-side string as a
  `const`, and `Loc.SourceStrings()` reflects over it. `UiStrings.g.cs` is generated by `tools/ExtractStrings`.
- `Loc.DefaultLabel` translates an authored default slice label on display while config stores the English;
  `Loc.Readout` translates an authored default or an executor status word and passes free text through.
- Lookups must not sit in a draw path: resolve a string once per run or per state change, not per frame.
- `SpeechSink` picks an installed SAPI voice for the language; with none installed, narration is suppressed
  rather than spoken by another language's voice.

### Which languages are offered

`Loc.Offered(code)` controls the pickers and Windows-language detection: English always, another language only
once every string in `Loc.SourceStrings()` has a non-blank translation in its UI map. Help being translated is
not enough, so a half-translated app is never offered.

### Layout hardening: direction and faces

- Windows follow the language's direction through `LocWpf.ApplyTo(window)`, called once after
  `InitializeComponent` in every UI window; drawn surfaces and anything that draws geometry pin `LeftToRight`.
  Scale pinned geometry with a `LayoutTransform` or `Viewbox`, never a `RenderTransform`.
- Drawn text uses `CultureInfo.InvariantCulture` and `FlowDirection.LeftToRight` in every `FormattedText`, so
  digits stay Western and a right-to-left draw does not move the origin.
- The material display faces are Latin-only; for Japanese and Arabic (`Loc.UsesSystemFace`) the wheel, previews
  and arcade chrome use the language's system font chain ([MATERIALS.md](MATERIALS.md)).
- The tray tooltip is limited to 63 characters by the shell and is elided on a character boundary.

### What stays English

Config keys, action-type tokens, material tokens, `"category:"` and `"group:"` override keys, trace-log text,
anything a user can put in a config file, and the Markdown export of the Help. Other apps' menus (Steam,
Windows, OBS) are named as those apps label them.
