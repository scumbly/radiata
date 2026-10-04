# Localization

**Read this when touching:** `Core/HelpContent.cs`, `Core/HelpText{Es,De,Ja}.cs`, `HelpLocalization`,
`HelpEditorControl`, or regenerating the Markdown export of the Help (`--export-controls`).

What is translated, how the translation layers work, and how to keep them current. The **whole app** (the
Settings window, dialogs, onboarding, overlay, narration, tray, Arcade and Game Grid) and the **Help topics**
are available in **Spanish, German, Japanese, and Arabic** alongside English; §3 covers the app, §1–2 Help.

> **The rule in one line:** English Help edits ship on their own; the es/de/ja/ar maps are refreshed in a
> **batched pass, run when the maintainer asks for one**. The fallback means a missed string renders in
> English, not broken.
> Commands below run from the repo root.

---

## 1. What ships today

| Surface | Languages | Source |
| --- | --- | --- |
| Settings ▸ Help topics (titles, body, category headings, search keywords) | en, es, de, ja, ar | `Core/HelpContent.cs` (English, canonical) + `Core/HelpTextEs.cs` / `HelpTextDe.cs` / `HelpTextJa.cs` / `HelpTextAr.cs` |
| The labels drawn inside the Help illustrations | en, es, de, ja, ar | `Core/HelpFigures.cs` (English, canonical) + the same four maps |
| The Help pane's own chrome (Contents, Language, "No topics match.", its tooltips) | en, es, de, ja, ar | `HelpLocalization.Chrome` (keys) + the same four maps |
| Everything else (tabs, buttons, wheel labels, onboarding, tray, toasts, dialogs) | en, es, de, ja, ar | `Core/UiText.cs` consts + `Core/UiStrings.g.cs` (XAML), with the `Core/UiText{Es,De,Ja,Ar}.cs` maps — see §3 |
| The Markdown export of the Help (`--export-controls`; the in-app Help is also published at getradiata.app/help) | English only | `HelpContent.ExportControlsMarkdown()` |
| `packaging\webhost\help\` (the getradiata.app/help site; the maintainer's site snapshot, development repository only, not in the public source) | en, es, de, ja, ar | `HelpHtmlExport.Write()` — emits `dir="rtl"` and `hreflang` alternates |

The picked language is stored in config as `system.language` (`"en"` default), sanitised on read
(`ConfigLoader.Sanitize` → `HelpLocalization.Normalize`), so an unknown code can never leave the picker
blank. Non-English languages get a **machine-translation notice** injected as the first block of the first
topic (`HelpLocalization.TranslationNotice`): the text was translated by an AI language model, may be
imperfect, the developer takes no responsibility for mistakes in it, English is authoritative, and support
can only answer messages written in English.

### How the layer works

`HelpLocalization` maps each authored English string to a translation, **keyed by the English text
itself** — not by topic id + block index. That choice is the whole safety story:

- **An English edit changes the key**, so the string falls back to English instead of silently showing a
  translation of the *old* wording. Untranslated is safe; wrongly translated is not.
- **Staleness is mechanically checkable** — `Radiata.exe --check-help-locales` diffs the source strings
  against each map (see §2).
- **Structure can't drift.** Topic ids, categories, order, block kinds, indents, and `LiveOnly` flags
  always come from the English source; a translation only supplies text. A translation can therefore
  never reshape the Help tab, drop a bullet, or break a cross-link target.
- **Search keeps working in English.** The localized keyword haystack is the *union* of the translated and
  English keyword sets, so a German-speaking user searching `HidHide`, `Game Grid`, or `Passthru Mode` still
  finds the topic.

### Translation conventions (follow these for every new string)

1. **UI names appear translated, with no English gloss.** The interface is translated, so a Help sentence names a
   control the way the user sees it: `**Ajustes ▸ Avanzado ▸ Integraciones**`. Take the translated label from the
   UI catalog (`Core/UiText<Xx>.cs`) — never invent a second translation of a label. Support is English-only,
   so the translation notice tells the reader to switch Radiata to English for a moment when following
   a support answer. Config keys, file paths, key names and product names stay exactly as on screen
   (`%APPDATA%Radiata`, `Ctrl+S`, Steam). `tools/help-flip.pl <Xx>` (development repository only, not in the
   public source) applies this mechanically to a whole map:
   every bold path whose segments are all UI-catalog strings becomes the translated path, and any leftover gloss
   is stripped; anything else (a Chrome menu path, emphasis, an authored parenthetical) is left alone.
   ⚠ Its gloss pass also strips a parenthetical that is only a catalog value, such as "(HidHide)", so read the
   diff. `tools/help-flip-labels.pl <Xx>` (same status) then covers link labels, plain paths, quoted readouts and the
   bare window names (dry run by default; `--write` applies). Neither can see a single label inside prose ("Add
   picker"), so finish with a grep of the map for English labels.
2. **Markup and tokens survive verbatim:** `**bold**`, `` `code` ``, `[[topic-id|Label]]`,
   `[Label](url)`, `{invoke}`, `{disable}`, `{cross}`, `{circle}`, `{square}`, `{triangle}`. A cross-link's
   **topic id is an identifier** — translate only the label after the `|`. Code examples stay verbatim,
   except an illustrative URL may translate its human-readable placeholder while keeping the scheme, host,
   and fixed path unchanged: `https://twitch.tv/yourchannel` → Spanish `https://twitch.tv/sucanal` or
   German `https://twitch.tv/ihrkanal`.
3. **Chord tokens resolve to English phrases** (from `TriggerModes.DescribeChoice`) and may be plural
   ("your chosen chords"), so keep them at the END of the sentence as an object in every language — the
   same rule the English source follows.
4. **Informal address, plain wording; no colloquialisms, no idioms, no humour that depends on wordplay.**
   German *du*, Spanish neutral *tú* (prefer imperatives and impersonal phrasing), Japanese です・ます without
   heavy keigo, Arabic Modern Standard Arabic with direct imperatives. This matches consumer and gaming
   software conventions; Microsoft's style guides reserve *Sie* / *usted* for enterprise and Office-type
   products. `TestHarness locregress` enforces it. Japanese keeps full-width punctuation around English
   terms kept half-width.
5. **Don't localize numbers, key names, or file formats** (`Ctrl+S`, `4455`, `.zip`, `12`).
**Direction glyphs in right-to-left text** follow the common RTL standard: glyphs that point along the reading direction are mirrored, glyphs that name a physical direction are not, and embedded left-to-right content is isolated with markup, never with control characters.
- **Path chevrons point along the reading direction.** A Radiata menu path is written in its **on-screen Arabic names**, taken from the Arabic UI map, with ◂ (`الإعدادات ◂ تخصيص ◂ إيماءات الاستدعاء`), so Help names exactly what the window shows. ▸ is right only inside an **English island**, meaning a path whose segments are English on both sides of the chevron. The display engine lays such a run out left to right, and a ◂ there would point backwards. Use an English island only for UI that has no Arabic form to name (the SteamGridDB site, a browser's download buttons, a third-party path whose Arabic labels cannot be verified) or for a deliberate English gloss in parentheses. Windows' own menu paths use the labels Windows shows in that language (Arabic: `الإعدادات ◂ النظام ◂ العرض`), as es/de/ja already do. `TestHarness locregress` fails on any ▸ that touches Arabic text. The map keys stay English with ▸.
- **The D-pad arrow pair 🡄 🡆 names physical buttons, so it is never mirrored.** Every language stores it in English order. The renderers keep it left to right: `InlineMarkup.IsolateArrows` wraps it in a left-to-right `Span` in the app (Help body, Help titles and the Customize heading), and `HelpHtmlExport` wraps it in `<span dir="ltr">`. Without that island the two neutral glyphs take the direction of their neighbours, reversing next to Arabic words but not next to Latin ones, so **no stored order is correct in every context**. The retired workaround stored the pair swapped, and it showed reversed inside English glosses and titles. `TestHarness locregress` fails on a swapped pair in any locale.
Register: informal everywhere (German du, Spanish tú, Japanese です/ます prose with noun-form labels, Arabic imperative instructions with noun-form commands). Japanese puts a half-width space at every kana/kanji ↔ Latin/digit boundary outside code spans and tokens. Arabic carries no short-vowel marks (shadda and tanween stay) and writes numeric ranges as `من X إلى Y`. Terminology follows Microsoft (German App not Anwendung; Spanish predeterminado not por defecto).
Glossary: **Passthru Mode** is translated: Modo directo (es), Direktmodus (de), パススルーモード (ja), الوضع المباشر (ar). So are the material names.
Material and thickness names are catalog keys (`UiText.Customize`) and translate on the tiles; keep them short. The tip-jar link resolves to the language's page through `Loc.TipUrl`.
Figures: a figure laid out in reading order sets `Sequence = true` in `HelpFigures` and is mirrored in right-to-left languages by both renderers. Reading order means steps read one after another (the edit move, the startup order, the package edit loop), a time or progress axis (a progress bar fills from the right, 0 on the right), and an indented outline (the folder tree indents from the right, as File Explorer does in Arabic). Spatial figures are never mirrored: hardware, the wheel, the input path, a key combo in the order it is written, and any coordinate system a script uses (the Arcade playfield's angles really do run clockwise). Four rules keep a figure correct in both directions. **Literal** labels (key, file and code names, `90°`, `dot(0.6, 120, …)`) render left to right in every language, because under a right-to-left paragraph the bidi algorithm moves their trailing punctuation to the front; the HTML export writes `direction` on every label of an RTL page for the same reason, since an SVG label otherwise inherits the page's `dir="rtl"`. A **TopLeft** label hugs the edge of its box nearest what it labels, the authored left edge or the right edge once mirrored, whatever its script. **Units and decimal marks are translatable**, so a chart tick such as a duration is never `Literal` (`0,8 s`, `0.8 秒`, `0.8 ث`), and digits stay Western. **No direction glyph (→) inside a label**: a drawn arrow mirrors with its figure, while a neutral glyph in translated text does not.
Figure size: the Help reading column is about 375 px, and a figure only scales down, so lay figures out at about 400 units wide or less and put a wide idea vertically.
6. **Figure labels are prose, not layout.** A label wraps inside a fixed canvas, so a translation that runs
   long wraps rather than overflowing — but keep them short anyway; three lines where English took one will
   collide with the label below it. Button and key names inside a figure (`L1 + L2`, `Ctrl`, `▲ ▼`) are marked
   `Literal` in `HelpFigures` and never reach a translator. Captions are ordinary body text and translate
   like any other block.
7. **Help-pane chrome translates like every other label**, but lives in `HelpLocalization.Chrome` rather than
   the UI catalog because the Help pane can render in a language other than the app (the Advanced tab's picker re-renders Help
   live; the rest of the app follows at the next start). Keep a new Help-pane label there, never as a XAML
   literal, so it is checked with the Help maps.

---

## 2. Keeping translations current (the maintenance loop)

**Rule: translations are refreshed in batches, on request — not per edit.** Editing English Help text does
NOT oblige a translation pass in the same change. Untranslated strings simply render in English, so drift is
safe and visible.

- **Don't re-translate as you go.** Land the English edit and stop.
- **The pass runs when the maintainer asks for it** ("update the localized help text", or equivalent).
- **Drift is not a release gate.** Packaging goes ahead over stale locales ([BUILD-RELEASE.md](BUILD-RELEASE.md)).

When the batched pass is running, every string added or reworded since the last pass is in scope. Start by
rebuilding and reading the report:

```bash
dotnet build ControllerWheel.csproj -c Debug && bin/Debug/net8.0-windows/Radiata.exe --check-help-locales help-locales.txt
```

The report lists, per language:

- `MISSING` — a source string with no translation. Either **new** or the **edited** version of a string
  whose old key is now reported as `STALE`. Add the entry to each `Core/HelpText*.cs`.
- `STALE` — a translation whose English key no longer exists. Pair it with the matching `MISSING` line
  (usually the same sentence, lightly reworded), move the translation over, and delete the old entry.

Then re-run the check until all four read `N/N translated, 0 missing, 0 stale`, and regenerate the
English Markdown export of the Help as usual (`CONTROLS.md`, committed in the development repository only):

```bash
bin/Debug/net8.0-windows/Radiata.exe --export-controls CONTROLS.md
```

**Then re-export the web Help** (maintainer process: the site is deployed from the maintainer's snapshot,
`packaging\webhost\`, development repository only, not in the public source), which carries all four languages
and would otherwise ship the drift the pass just fixed. ⚠ **Export from a PUBLIC build, never the Debug exe:**
the site is the public product's Help, and the export follows the build's `ReleaseGates` (Internode and the
Workshop category are withheld from a public build). A Debug export would publish them.
`tools\build-installer.ps1 -Public` leaves its staging folder (`publish\installer-staging-<guid>`, a new one
per run) in place:

```bash
publish/installer-staging-<guid>/Radiata.exe --export-help-html packaging\webhost\help
```

The report and the maps, by contrast, cover EVERY authored string in every flavour
(`HelpLocalization.SourceStrings` walks `HelpContent.AllAuthored`), so a withheld topic's translations stay
current while it is dormant and nothing is re-translated when it returns.

### Web Help site contract

The five `/help/` pages are the only generated pages on getradiata.app; everything else is hand-maintained.
`Core/HelpHtmlExport.cs` therefore reproduces the site's chrome, and a re-export must never regress it.
The reference for every string and path below is the hand-maintained `/download/<lang>.html`
(`packaging\webhost\download\`, in the maintainer's snapshot of the live site; see *The marketing site* below).

- **Shared nav first in `<body>`**, four items + the language switcher as the last child of its `.wrap`,
  labels and `aria-label`s localised, hrefs root-absolute with the language suffix (`/download/es.html`).
  No page-local `.langs` CSS — the shared sheet owns `.sitenav .langs`.
- **The nav is sticky**, so `.toc` `top` / `max-height` and `.topic` `scroll-margin-top` are
  `calc(var(--nav-h) + 18px)`-based. A flat `18px` hides headings behind the nav.
- **Footer** identical to the site's: `© 2026 Jesse Holden` · localised Home link · GitHub **profile**.
- **Favicon** = `/assets/favicon.svg` (the real six-petal mark), never an inline data: URI. **Logo** =
  `/assets/flower-mark-color-unique.png`; `assets/radiata/` no longer exists anywhere on the site.
- **`<title>` = `Radiata — <Help>`** localised, no language named in English; `<h1>`, subtitle and
  `<meta description>` localised too. Subtitle menu paths use the app's own segments; Arabic uses `◂`.
- **Callouts** (`.note.tip` / `.note.warn`) stroke all four edges in their own colour at uneven widths
  (7px bottom, 4.5px inline-end, 2px top and inline-start) with **logical** edge properties so the RTL
  page mirrors itself. Don't even the widths or revert to left/right.
- **Ko-fi loader** `<script src="/assets/kofi.js" defer>` is the last element before `</body>`.
- **No glyph from Supplemental Arrows-C / Geometric Shapes Extended / Symbols for Legacy Computing** may
  reach the HTML: the export swaps the D-pad arrows U+1F844–47 for `← ↑ → ↓` and throws on anything else
  from those blocks (`HelpHtmlExport.ForWeb`). The app keeps its heavy arrows; only the web copy changes.
- **`styles.css?v=` tracks the site**: sniffed from the site root when exporting into it, or passed with
  `--css-version=N`. The harness `locregress` group checks all of the above against `packaging\webhost\`
  (the check skips itself outside the development repository, which alone carries that folder).

⚠ **If the maps are so far out that most entries are stale, rebuild the files rather than patching them.**
Pairing a few hundred `STALE`/`MISSING` lines by hand is where mistakes get made; regenerating each map
from `HelpLocalization.SourceStrings()` in source order makes a stale entry structurally impossible, and
the entry order then matches the Help tab's own reading order. `Radiata.exe --dump-help-locale <code> [path]` writes
exactly that file: every current key in source order, carrying the existing translation where its English key
still exists and a blank `// TODO translate` value where it doesn't. Fill the TODOs, copy the result over
`Core/HelpText<Xx>.cs`, rebuild, re-check. The dump walks every authored topic, gated or not, so a map regenerated from any build keeps the withheld topics and the public-only block variants.

Notes for whoever does the edit:

- Copy the English **C# literal** across as the key, escapes and all (`\"`, `C:\\Games\\game.exe`) — the
  key must match byte for byte or the check reports it as missing.
- Translate the **keyword line** too; it is a search haystack, so native synonyms matter more than a
  literal rendering. English keywords are added automatically — don't repeat them.
- A **new category** needs its name translated in all four files (it is a key like any other).
- **Deferring is the default between passes:** ship the English string, leave the report's `MISSING` lines, and
  the affected lines simply render in English next to translated ones.
- `help-locales.txt` is a scratch artifact — don't commit it (write it outside the repo, or delete it).

### Copy pass (the maintainer's read-through of every string)

A whole-product copy pass runs through `tools/copy-deck` (development repository only, not in the public
source), never through a maintained document — a second copy of a string is a second thing to keep in step.

1. `dotnet run --project tools/copy-deck -- export tools/copy-deck/decks/<id>.json` writes one JSON deck
   from the source: every Help string, every `UiText` const, every XAML `{loc:T}` / `LocRich.Source` value,
   the installer's `english.*` keys, the shell-side catalog sets, and every paragraph of the published
   prose files and the three English marketing pages. Each row carries its exact source span; each file its
   hash. Generated strings (`CONTROLS.md`, `UiStrings.g.cs`) and verbatim license text are not rows.
2. Optionally, an assistant pre-pass adds per-row `flags` (typo, consistency, fact, markup…) and nothing
   else; `-- verify` proves the exported rows are untouched.
3. The maintainer edits in `tools/copy-deck/index.html` (a single file opened from disk; the draft survives
   closing the page) and saves the deck.
4. `-- import <deck.json>` writes the edits back. **It refuses the whole import, writing nothing, if any
   file changed since the export, if a span no longer decodes to its row, or if an edited row breaks its
   kind's rules** (markup balance, unknown tokens, a changed `{n}` set, a cross-link to no topic, bidi
   controls, the XAML newline/tab rule).
5. **A reworded string loses its four translations.** The importer deletes the entries from the
   `UiText<Xx>` / `HelpText<Xx>` maps and records old English, new English and the four old values in
   `docs/TRANSLATION-WORKLIST.json` (tracked, not published), so the next `--check-locales` reads MISSING for
   exactly those keys and 0 STALE. A row ticked **typo only, keep translations** is re-keyed instead. The
   installer's `spanish.`/`german.`/`japanese.`/`arabic.` lines are kept whatever the tick — a removed
   `[Messages]` override falls back to Inno's stock wording for that language, not to English — and are
   listed in the worklist as stale. A marketing-page row lists its four hand-edited siblings.
6. Then the carry-through: `tools/ExtractStrings` (XAML edits regenerate `UiStrings.g.cs`), build,
   `--check-locales`, `--export-controls CONTROLS.md`, `SettingsSmokeProbe`, `TestHarness loc locregress
   help hygiene`.

⚠ **Run the translate pass straight after the import, and cut no build in between.** `Loc.Offered` only
offers a language whose UI catalog is complete, so after a UI rewording a Release or `-Public` build lists
no non-English language until the pass lands, and `TestHarness loc` is red for the same reason. The pass
takes each worklist entry's old translation as its base, and the entry is removed once the key is translated
again.

### Ownership / review

Translations are LLM-produced by design, and the in-app notice says so. If a native speaker ever reports a
wording problem, fix the single entry in the relevant `HelpText*.cs` — no other file is involved, and no
rebuild of the other languages is needed.

---

## 3. Localizing the whole app — the `Loc` layer (infrastructure shipped; UI strings land slice by slice)

The rest of the UI localizes the way Help does — **keyed by the English text itself**, English fallback,
LLM-produced translations behind the same notice — with two differences that decide most of the design:

- **The language is fixed for the run.** `Loc.Init(cfg.System.Language)` runs once, first thing after config
  is read and before any thread exists, and sets `CurrentUICulture` for the default and current threads.
  Changing `system.language` in Settings prompts a restart; onboarding is the one exception and rebuilds its
  own window. There is no change event: the maps are read-only, so `Loc.T` is safe from any thread, the
  wheel's per-slice bitmap bakes can never hold a stale language, and nothing has to subscribe or unsubscribe.
  `CurrentCulture` is deliberately untouched — config, paths and hex parse with `InvariantCulture`.
- **The drawn surfaces never mirror.** `OverlayWindow`, `RadialMenuControl`, `ArcadeControl`,
  `ControllerButton` and `WheelMiniature` pin `FlowDirection = LeftToRight` (`TestHarness loc` asserts it):
  a slice's position is the physical stick direction and the aim math in `Core/WheelStateMachine` knows
  nothing about mirroring. Right-to-left languages shape their text runs; the geometry stays.

### The pieces

| Piece | Where | What |
| --- | --- | --- |
| `Loc.T(english)` / `Loc.F(english, args)` / `Loc.P(one, other, n)` | `Core/Loc.cs` | The lookup, composite formatting (a malformed translated format falls back to English — narration formats on a pool thread), and plurals keyed on the English pair `one|other` with per-language form counts (ja 1, en/es/de 2, ar 6; hand-rolled, .NET has no plural-category API). |
| `{loc:T Some label}` | `LocExtension.cs` (`TExtension`) | XAML form for short labels on real dependency properties. ⚠ Its grammar splits on `,` `=` and nests on `{ }`; a label carrying those, or an apostrophe, uses `LocRich.Source` instead. |
| `loc:LocRich.Source="**Bold** and `code`…"` | `LocExtension.cs` (`LocRich`) | Prose with inline markup on a `TextBlock` — an attached property because `TextBlock.Inlines` is not a dependency property. **One string per sentence, or per complete clause on its own line; never per styling span.** ⚠ Never wrap the attribute across source lines (XML normalizes the whitespace and the runtime key stops matching). |
| `InlineMarkup` | `InlineMarkup.cs` | The one markup vocabulary rendered to WPF inlines: `**bold**`, `*italic*`, `` `code` ``, `[[topic-id|Label]]`, `[Label](url)`, and the literal two characters `\n` as a line break. Help, cards and toasts share it. Only AUTHORED strings reach it — user or package text renders plain. |
| `UiText` | `Core/UiText.cs` | Every C#-side string as a `public const string` in nested classes per surface; `Loc.SourceStrings()` reflects over it, so a string that lives there is in the catalog by construction. Strings shared with XAML bind as `{x:Static ui:UiText.X.Y}`. |
| `Loc.DefaultLabel(label)` | `Core/Loc.cs` | A slice label as DISPLAYED. Config always stores the English default (`UiText.DefaultLabels`, `AppConfig.Default`, the onboarding seeder, an in-wheel Add's taxonomy display name); a label still matching one translates at display time, anything the user typed passes through. The set is deliberately narrow — `UiText.DefaultLabels` plus what the shell registers at startup (`Loc.RegisterDefaultLabels`, the taxonomy displays) — never the whole catalog, or a game called *Control* would be replaced by a UI string. Product names (Steam, Playnite, Moonlight) are not in it. |
| `UiText.Chords` + `TriggerModes` | `Core/TriggerConfig.cs` | Chord and button phrases (`Describe`, `EnableDisableLabel`, the builder's `PrimaryLabel`/`ModifierLabel`, the Help `{invoke}`/`{disable}` tokens) resolve through `Loc.T`; bare hardware identifiers (`L4/R4`, `Fn1/Fn2`, `L3/R3`) stay literal. |
| `UiText.Status` / `UiText.Spoken` / `UiText.Narration` / `UiText.Overlay` / `UiText.Toasts` | `Core/ActionExecutor.cs`, `App.xaml.cs`, `RadialMenuControl.cs` | The executor's state words are identity tokens (compared by `Opposite` and the narration's `StateClause`) and stay English inside; the hub readout, status toast and announcer translate them at display. The overlay's edit legend is resolved once per run (`RadialMenuControl.Leg`) — no `Loc.T` in a draw method (R3). Toast titles and bodies are consts; app and holder names are format arguments. |
| `Loc.Readout(line)` | `Core/Loc.cs` | A hub-readout line as displayed: translates only an authored default label or an executor status word, and passes free text (a game name, the frontmost app an Exit slice would close) through. The status consumers in `App.xaml.cs` use it, never `Loc.T`. |
| `Loc.P(one, other, n, extra…)` | `Core/Loc.cs` | A counted sentence with more than the count in it: `{0}` is the count, `{1}…` the extras ("{1} deleted, {0} slices left"), so a translation can reorder them. |
| `{pad:Triangle}` | `InlineMarkup.cs` | A controller-button chip inside an authored sentence (the Steam sentry card). The site supplies `Look.Chip` to draw the glyph; `Flatten` speaks it by name (`ControllerButtons.Spoken`), so one string serves the eye and the ear. UI-only — Help keeps its own `{cross}`/`{triangle}` face-glyph tokens. |
| `UiText.Buttons` | `ControllerButtons.cs` | The spoken button names ("the A button", "cross", "left stick click"). The printed labels (`A`, `LB`, `L1`) are identifiers and stay literal. |
| `UiText.Tray` / `UiText.IsolationNote` / `UiText.Names` / `UiText.Dialogs` | `App.xaml.cs` | The WinForms tray menu is built once at startup in the run's language, with `ContextMenuStrip.RightToLeft` following `Loc.IsRtl` (WPF's `FlowDirection` does not reach it). The capture layer's isolation notes are identity tokens (the isolation toast, `Arcade.ReasonFromNote` and the warning latch compare them) stored as the English const and translated only in the tray tooltip. Controller names keep product names literal and translate the generic words and transport suffixes. |
| The log e-mail | `SettingsWindow.xaml.cs` | Subject and body instructions translate; in a non-English UI the body ends with the English-only support line, so a user is told before they write in their own language. The attached log and crash payloads are trace text and stay English by design. |
| `UiText.Grid` | `GameBrowserControl.xaml(.cs)` | The Game Grid's fixed captions are `{loc:T …}` in the XAML; its mode caption, storefront-hide confirm and narration are consts. Game and storefront names are format arguments, never keys. The headline face (`Franklin Gothic Medium, Arial Black, Segoe UI`) has no CJK glyphs of its own and relies on WPF's font fallback — a layout item for the pseudo-localization pass. |
| `UiStrings.g.cs` | `Core/UiStrings.g.cs`, from `tools/ExtractStrings` | The XAML strings, extracted by parsing the XAML as XML (never a line regex). Committed. The tool fails on an unquoted `{loc:T}` value carrying grammar characters, and on any value XML normalization would alter. |
| `--check-locales [path]` | `App.xaml.cs` | Both parity reports in one file: Help (`--check-help-locales`) and UI — MISSING / STALE / PLURAL form counts / `|` in prose — with the string sets only the shell can enumerate (`ColorNamer.AllKeys()`, the slice taxonomy) passed in. |
| `--dump-help-locale <code>` | `App.xaml.cs` | Regenerates a Help map in source order (§2). `--dump-ui-locale <code> [path]` does the same for a UI map (`Core/UiText<Xx>.cs`), in catalog order. |
| Voice | `SpeechSink` | Picks an installed SAPI voice for the language; with none installed, narration is suppressed rather than spoken by a voice for another language. |

### The Setup wizard

The installer speaks English, Spanish, German, Japanese and Arabic through `[Languages]` (Inno official translations) with `ShowLanguageDialog=yes` (the picker always shows, and never in a silent update); Arabic.isl is right-to-left, so Inno mirrors the wizard itself. Every `[Messages]` override and user-facing string in `packaging/radiata.iss` is per-language (`{cm:…}` / `CustomMessage`) — see docs/INSTALLER.md ▸ Wizard languages.

### The marketing site (getradiata.app)

The four pages are translated as flat sibling files, the way `help/` already is: `/es.html`, `/de.html`, `/ja.html`, `/ar.html` beside `index.html`, and `download/<code>.html`, `tip/<code>.html` (and the held-back `workshop/<code>.html`) beside each `index.html`. Every page's nav carries the Workshop link commented out, in the slot after Help; the generated `/help/` pages emit the same comment (`HelpHtmlExport.NavWorkshop`). Each home page also holds a commented-out Workshop version of its **Expandable** card, translated, directly below the live one. Every page (English included) carries `hreflang` alternates plus `x-default`, and a language bar under the site nav (`.langbar` / `.langs` in `styles.css`, cache-busted with the site's current `?v=`; the Help export reads that number from the site root, see `HelpHtmlExport.ResolveStylesVersion`). The Arabic pages set `dir="rtl"`; the shared sheet mirrors its few directional rules under `[dir=rtl]`, and the download page mirrors its numbered-step gutter in its own style block. Links inside a translated page point at the same-language siblings (`/help/es.html`, `/download/es.html`). The 404 page stays English — Apache serves one `ErrorDocument`. The site is maintainer process: its source of record is `packaging\webhost\`, a snapshot of the live docroot kept in the development repository's git (development repository only, not in the public source; uploaded by SFTP; the setup zips under `download/` are gitignored). Hand edits to the marketing pages are made there and uploaded; the Help export writes straight into its `help/`, which also makes the `styles.css?v=` sniff read the snapshot's root `index.html`. When the English copy changes, the four siblings are edited by hand — there is no generator.

### Which languages are offered

Status: all four UI catalogs (es, de, ja, ar) are complete and every shipped language is offered; a new UI string is MISSING in all four until its four map entries land, which `--check-locales` reports.

`Loc.Offered(code)` gates both the pickers and Windows-language detection: English always; another language
only once every string in `Loc.SourceStrings()` has a non-blank translation in its UI map. Help alone being
translated must never surface a language — a user would land in a half-translated app. `system.language`
absent or null means "follow Windows": `Loc.Init(null)` takes the display language when it is offered, else
English, and never writes it back; a pick stores an explicit code. The first-run wizard's picker is the one
unprompted change: it writes the pick and relaunches, and the wizard resumes on the same step (see
docs/ONBOARDING.md ▸ Language). Debug builds list every language in that picker so the path can be
exercised before the maps exist.

### Layout hardening: direction, faces, pseudo-locales

- **Windows follow the language's direction; drawn surfaces never do.** `LocWpf.ApplyTo(window)` runs once
  after `InitializeComponent` in every UI window (Settings, onboarding, the pickers, the wizards, the
  code-built dialogs and toast cards). The overlay, wheel, Arcade, controller chips and wheel miniatures pin
  `LeftToRight` themselves (docs/OVERLAY.md), and so does anything inside a mirrored window that draws
  geometry or carries hand-computed layout: the flag swatches, the onboarding practice filmstrip and
  watermark, the flower marks, the colour picker's `#` + hex row, the HID Diagnostics byte grid. ⚠ A pinned
  child inside a mirrored parent is flipped about its arranged slot, and WPF composes that flip with a
  `RenderTransform` so the content lands OUTSIDE the slot (the corner-card flower mark rendered over its
  Arabic text): scale pinned geometry with a `LayoutTransform` or a `Viewbox`, never a `RenderTransform`
  (`TestHarness loc` asserts it for the flower marks). The Game
  Grid lives in the overlay window and stays left-to-right; its text still shapes right-to-left inside
  each run. The WinForms tray menu has its own `RightToLeft`, set from `Loc.IsRtl`.
- **Drawn text keeps `CultureInfo.InvariantCulture` and `FlowDirection.LeftToRight` in every
  `FormattedText`.** Two reasons, both decided: an Arabic culture makes WPF substitute Arabic-Indic digits
  (counts and percentages must stay Western), and a right-to-left `FormattedText` moves its drawing origin to
  the top-right, which would mirror every hand-positioned readout. A run of Arabic letters still renders in
  reading order under the Unicode bidi algorithm; mixed Latin-and-Arabic punctuation order is the accepted
  limitation, as is the edit legend's English segment order.
- **Faces.** The material display faces (Jua, Sour Gummy, Bahnschrift, Share Tech) are Latin-only. When
  `Loc.UsesSystemFace` (ja, ar), the wheel's labels and hub (`LabelFaceFor`, `HubFace`), the Customize
  tab's material previews (`ApplyPreviewLabelFace`) and the language table's chain (`LocWpf.LanguageFamily`)
  take over; the Arcade chrome face carries a `Segoe UI, Tahoma, Arial` fallback. docs/MATERIALS.md records
  it per material. Bundled fonts stay Latin — system fonts only, never a fourth embedded face.
- **Pseudo-locales, dev builds only.** `qps` and `qps-rtl` are real language rows in Debug builds and do
  not exist in Release (`Normalize` rejects them, so a config cannot name them). **Neither picker ever
  lists them** — not the Settings row, not the onboarding Welcome step, in either build: they are a layout
  gate reached with `--lang`, not a language anyone chooses. `Loc.Pseudoize` brackets,
  accents and pads every catalog string by ~30% while keeping `{0}`, markup markers, `[[id|`, `(url)` and
  `{pad:X}` intact — a plain English string on screen is then a missed extraction, a clipped bracket is a
  layout that cannot take German, and `qps-rtl` mirrors every window. Run them with `Radiata.exe --lang
  qps-rtl` (Debug) or `dotnet run --project tools\SettingsSmokeProbe -- --lang qps` (the probe also runs per
  language in the verify loop: `en`, `qps`, `qps-rtl`). Help renders English under both.
- **Fixed budgets translators must respect** (recorded here until a width gate exists): the Settings
  window is `MaxWidth=690` with a wrapping tab strip — the seven tab headers together must fit ~640 px at
  13.5 px semibold, so keep each header near its English length ("Left Wheel", "Right Wheel", "Customize",
  "Passthru Mode", "Advanced", "Help", "About"); onboarding is a fixed 680×555 with 24 px card title rows;
  the tray tooltip is 63 chars (elided automatically); the mixer names cut at 12 chars.
- **The Help Markdown export is generated in English**: the `--export-controls` CLI block runs before
  `Loc.Init`, so a contributor's saved language cannot leak into the committed file (`CONTROLS.md`, kept in
  the development repository only).
- **Changing the language in Settings ▸ Help** re-renders Help live and offers a restart for the rest of
  the app (the language is fixed per run); declining leaves the pick saved for the next start. The
  Restore prompt names language among what a backup replaces.

### What stays English, by decision

Config keys, action-type tokens, material tokens, the `"category:<key>"` / `"group:<key>"` override keys
(the taxonomy's `Key` is the identity; only its `Header`/`Display` is text), trace-log text, anything a
user can put in a config file, and the `--export-controls` Markdown export (the Help *site* carries the translations). Material
*tokens* stay; the material *names* on the tiles translate, and Help follows them. Other apps' menus
(Steam, Windows, OBS) are named as those apps label them, never with Radiata's catalog.

### Order of work

1. Structural blockers cleared first, English-only: identity/display keys, typed hub notices, layout out of
   the strings (hub legend gutters, arcade prompt rows, whole-phrase colour names), bidi-safe package
   sanitizers, the tray/how-to/card-packer measures.
2. This layer, English-only, no visible change.
3. Extraction, one slice per commit, each buildable: Settings tabs → satellite dialogs → onboarding (plus its
   language picker) → taxonomy and default labels → overlay → narration → tray/dialogs → Arcade → Game Grid.
4. Pseudo-localization (`qps`, `qps-rtl` — dev builds only) before a word is translated.
5. The translation pass, per language, gated: a language is offered in the picker (and eligible for
   first-run auto-detect) only when its UI coverage is complete.

### Arabic (right-to-left)

Arabic is Modern Standard Arabic (`ar`), the register every literate Arabic speaker reads; the language table
marks it `Rtl: true`. The Help pane follows that flag for its **text and layout** (`FlowDirection` on the
pane), while the flag swatch and the illustration canvases pin themselves back to left-to-right — a flag and
a diagram of the hardware are not text, and `L1` is physically on the left; a reading-order figure mirrors
through `HelpFigure.PartsFor`, never through `FlowDirection`. Translated figure labels carry the language's
direction individually and `Literal` ones stay left to right (§1 ▸ *Figures*). The HTML export emits
`dir="rtl"` on the page, an explicit `direction` on every SVG label, and mirrors the three side-specific CSS
rules.

Translation values must contain **no bidi control characters** (U+200E/F, U+061C, U+202A–E, U+2066–9), **no
Arabic-Indic digits** (convention #5 — numbers stay Western), and **no presentation forms** (U+FB50–FEFC — the file's own U+FEFF BOM on line 1 is expected);
they are invisible in a diff and break tokens, markers and search. Scan for them before committing a pass.
