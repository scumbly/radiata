using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Translation-style regressions: the desk-free half of the localization checks (strings through Loc,
/// pseudo-loc and direction, the setup wizard's languages, and the marketing site).
///
/// The rules under test are data conventions as much as code (docs/LOCALIZATION.md; one register per
/// language): English is the catalog key so translation under "en" must be identity; every translation keeps
/// its placeholders and tokens; German and Spanish read informal (du / tú) in both the UI catalogs and the Help
/// maps; Arabic carries no short vowels and writes numeric ranges as words; Japanese spaces its kana/kanji ↔
/// Latin boundaries; sentence-casing of spliced state words is English-only; the Game Grid sorts with the UI
/// culture. The getradiata.app snapshot under packaging\webhost is checked structurally.
/// <c>Loc.Lang</c> is fixed per run by design, so the language-dependent checks set it by reflection and
/// restore it; a rename of that property or of a private seam reads as MISSING / SKIP, never as a pass.</summary>
internal static class T_LocRegress
{
    private static readonly string[] Shipped = { "es", "de", "ja", "ar" };
    private static string SiteRoot => RepoRoot() is { } r ? Path.Combine(r, "packaging", "webhost") : null;

    public static void Run()
    {
        H.Group("Localization regressions: register, tokens, Arabic/Japanese conventions, sort, site structure");

        EnglishIdentity();
        PlaceholderParity();
        GermanCasing();
        InformalRegister();
        ArabicConventions();
        JapaneseBoundarySpacing();
        CultureAwareSort();
        RestorePromptNamesLanguage();
        MarketingSite();
        PseudoKeepsTokens();
        BidiControlCharacters();
    }

    // ── 11. Bidi control characters ─────────────────────────────────────────────

    // U+200E/F (LRM/RLM), U+061C (Arabic Letter Mark), U+202A-E (embedding/override), U+2066-9 (isolates).
    // Invisible in a diff and in most editors; docs/LOCALIZATION.md forbids them in a locale value.
    private static readonly (int Lo, int Hi)[] BidiRanges =
        { (0x200E, 0x200F), (0x061C, 0x061C), (0x202A, 0x202E), (0x2066, 0x2069) };

    /// <summary>No locale map value carries a bidi control character — a source-text scan rather than a map
    /// walk, so it also covers the English base files (which should never carry one either) and needs no
    /// reflection. A bidi mark is invisible in a diff and in most editors, so only a scan catches one.</summary>
    private static void BidiControlCharacters()
    {
        var coreDir = Path.Combine(RepoRoot(), "Core");
        var files = Directory.EnumerateFiles(coreDir, "UiText*.cs")
            .Concat(Directory.EnumerateFiles(coreDir, "HelpText*.cs"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        H.Check("the bidi scan found the UiText*/HelpText* locale files", files.Count >= 8, $"{files.Count} files: " + string.Join(", ", files.Select(Path.GetFileName)));

        var hits = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
                foreach (var ch in lines[i])
                    if (BidiRanges.Any(r => ch >= r.Lo && ch <= r.Hi))
                        hits.Add($"{Path.GetFileName(file)}:{i + 1} U+{(int)ch:X4}");
        }
        H.Check("no bidi control character (U+200E/F, U+061C, U+202A-E, U+2066-9) in any UiText*/HelpText* value",
                hits.Count == 0, hits.Count == 0 ? null : string.Join(", ", hits.Take(10)));
    }

    // ── 1. English identity ─────────────────────────────────────────────────────

    /// <summary>English is the source: with the run language at "en", T(value) == value for every UiText const,
    /// and F formats through unchanged.</summary>
    private static void EnglishIdentity()
    {
        if (Loc.Lang != HelpLocalization.DefaultCode) { H.Skip("English identity", $"Loc.Lang is already '{Loc.Lang}' in this process"); return; }
        var consts = UiTextConsts().ToList();
        var broken = consts.Where(c => Loc.T(c.Value) != c.Value).Select(c => c.Name).ToList();
        H.Check($"Loc.T is identity for all {consts.Count} UiText consts under \"en\"", consts.Count > 100 && broken.Count == 0,
                broken.Count == 0 ? null : $"{broken.Count} differ: " + string.Join(", ", broken.Take(5)));
        H.Check("Loc.F round-trips a placeholder under \"en\"",
                Loc.F(UiText.Settings.RestoreTooLarge, 7) == "That file is 7 MB — too large to be a Radiata settings backup."
                && Loc.F("{1} then {0}", "a", "b") == "b then a");
        H.Check("Backup/restore failure dialogs carry no exception-text placeholder",
                new[] { UiText.Settings.BackupFailed, UiText.Settings.RestoreCantRead, UiText.Settings.RestoreFailed }
                    .All(s => !Positional.IsMatch(s)));
    }

    // ── 2. Placeholder parity ───────────────────────────────────────────────────

    private static readonly Regex Positional = new(@"\{\d+\}", RegexOptions.Compiled);
    private static readonly Regex NamedToken = new(@"\{[A-Za-z][A-Za-z0-9:_-]*\}", RegexOptions.Compiled);

    /// <summary>Every translated value carries exactly the positional placeholders of its English key (a lost {0}
    /// formats wrong, an invented {2} throws and falls back), and the Help maps keep every named {token}.</summary>
    private static void PlaceholderParity()
    {
        foreach (var code in Shipped)
        {
            var ui = UiMap(code);
            if (ui is null) { H.Skip($"{code}: UI placeholder parity", "Loc.MapFor seam missing or the catalog is absent"); continue; }
            var bad = new List<string>();
            foreach (var (k, v) in ui)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (k.StartsWith("plural:", StringComparison.Ordinal))
                {
                    if (!v.Split('|').All(f => f.Contains("{0}"))) bad.Add(k);
                    continue;
                }
                if (!SameSet(Positional.Matches(k), Positional.Matches(v)) || !SameSet(NamedToken.Matches(k), NamedToken.Matches(v))) bad.Add(k);
            }
            H.Check($"{code}: UI catalog keeps every {{n}} / {{token}} of its English key ({ui.Count} rows)", bad.Count == 0,
                    bad.Count == 0 ? null : $"{bad.Count} mismatched: " + Excerpts(bad));

            var help = HelpMap(code);
            if (help is null) { H.Skip($"{code}: Help placeholder parity", "HelpLocalization.MapFor seam missing"); continue; }
            var badHelp = help.Where(e => !string.IsNullOrWhiteSpace(e.Value)
                                          && (!SameSet(Positional.Matches(e.Key), Positional.Matches(e.Value))
                                              || !SameSet(NamedToken.Matches(e.Key), NamedToken.Matches(e.Value))))
                              .Select(e => e.Key).ToList();
            H.Check($"{code}: Help map keeps every {{n}} / {{token}} of its English key ({help.Count} rows)", badHelp.Count == 0,
                    badHelp.Count == 0 ? null : $"{badHelp.Count} mismatched: " + Excerpts(badHelp));
        }
    }

    private static bool SameSet(MatchCollection a, MatchCollection b)
    {
        var sa = a.Select(m => m.Value).OrderBy(s => s, StringComparer.Ordinal);
        var sb = b.Select(m => m.Value).OrderBy(s => s, StringComparer.Ordinal);
        return sa.SequenceEqual(sb);
    }

    // ── 3. German casing ────────────────────────────────────────────────────────

    /// <summary>App.LowerFirst sentence-cases a spliced state word in English only: "Currently muted" but the German
    /// noun keeps its capital ("Derzeit Stumm" would otherwise become "Derzeit stumm", a spelling error).</summary>
    private static void GermanCasing()
    {
        var app = H.AppType("App");
        var m = app is null ? null : H.StaticMethod(app, "LowerFirst", 1);
        if (m is null) { H.Fail("App.LowerFirst missing", "the narration sentence-casing seam was renamed or removed"); return; }
        string Lower(string s) => (string)m.Invoke(null, new object[] { s });

        H.Check("en: LowerFirst lowers a plain state word", Lower("Muted") == "muted" && Lower("Off") == "off");
        H.Check("en: LowerFirst leaves an acronym-led phrase alone", Lower("OBS unavailable") == "OBS unavailable" && Lower("HDR on") == "HDR on");
        using (SetLang("de"))
            H.Check("de: LowerFirst keeps a German noun's capital", Lower("Stumm") == "Stumm" && Lower("Aus") == "Aus" && Lower("Ton an") == "Ton an");
        using (SetLang("es"))
            H.Check("es: LowerFirst leaves the word untouched (casing rule is English-only)", Lower("Silenciado") == "Silenciado");
        using (SetLang("ja"))
            H.Check("ja: LowerFirst is a no-op on kana", Lower("ミュート") == "ミュート");
        H.Check("Loc.Lang restored to English after the casing checks", Loc.Lang == HelpLocalization.DefaultCode);
    }

    // ── 4. Informal register ────────────────────────────────────────────────────

    // Standalone formal pronouns. German "Sie" at a sentence start can also be "they/she", so a hit there is
    // inspected by hand; mid-sentence "Sie"/"Ihnen"/"Ihre…" is the formal address the style pass removed.
    private static readonly Regex SpanishFormal = new(@"\b[Uu]sted(es)?\b", RegexOptions.Compiled);
    private static readonly Regex GermanFormal  = new(@"(?<![.!?:]\s)(?<!^)\b(Sie|Ihnen|Ihr|Ihre|Ihrer|Ihren|Ihrem|Ihres)\b", RegexOptions.Compiled);

    private static void InformalRegister()
    {
        ScanRegister("es", SpanishFormal, "usted");
        ScanRegister("de", GermanFormal, "Sie / Ihnen / Ihr…");

        // A second, softer signal for Spanish: the formal imperative ("haga", "pulse", "use") against the familiar
        // "haz", "pulsa", "usa". Reported as a count so a stray one is visible without failing on prose where the
        // same forms are subjunctive.
        var formalImperatives = new Regex(@"\b(pulse|haga|use|abra|elija|seleccione|mantenga|suelte|active|desactive|reinicie)\b", RegexOptions.Compiled);
        int hits = AllValues("es").Sum(v => formalImperatives.Matches(v).Count);
        int familiar = AllValues("es").Sum(v => Regex.Matches(v, @"\b(pulsa|haz|usa|abre|elige|selecciona|mantén|suelta|activa|desactiva|reinicia)\b").Count);
        H.Check("es: familiar imperatives outnumber formal-looking ones by a wide margin", familiar > hits * 5,
                $"familiar {familiar}, formal-looking {hits} (the latter includes subjunctive uses)");
    }

    private static void ScanRegister(string code, Regex formal, string what)
    {
        var ui = UiMap(code); var help = HelpMap(code);
        if (ui is null || help is null) { H.Skip($"{code}: informal register", "catalog seam missing"); return; }
        var uiHits   = ui.Where(e => formal.IsMatch(e.Value)).Select(e => e.Key).ToList();
        var helpHits = help.Where(e => formal.IsMatch(e.Value)).Select(e => e.Key).ToList();
        H.Check($"{code}: UI catalog has no formal address ({what})", uiHits.Count == 0,
                uiHits.Count == 0 ? $"{ui.Count} rows scanned" : $"{uiHits.Count} hits: " + Excerpts(uiHits));
        H.Check($"{code}: Help map has no formal address ({what})", helpHits.Count == 0,
                helpHits.Count == 0 ? $"{help.Count} rows scanned" : $"{helpHits.Count} hits: " + Excerpts(helpHits));
    }

    // ── 5. Arabic conventions ───────────────────────────────────────────────────

    // Stripped: fatha U+064E, damma U+064F, kasra U+0650, sukun U+0652. Kept by decision: tanween U+064B to U+064D, shadda U+0651.
    private static readonly Regex ShortVowels = new("[\u064E\u064F\u0650\u0652]", RegexOptions.Compiled);
    private static readonly Regex CodeSpan    = new("`[^`]*`", RegexOptions.Compiled);
    private static readonly Regex DigitRange  = new(@"(?<![\w.])\d{1,3}\s*[-\u2013\u2014]\s*\d{1,3}(?![\w.])", RegexOptions.Compiled);
    private static readonly Regex WordedRange = new(@"من\s+\d+\s+إلى\s+\d+", RegexOptions.Compiled);

    private static void ArabicConventions()
    {
        var ui = UiMap("ar"); var help = HelpMap("ar");
        if (ui is null || help is null) { H.Skip("ar: conventions", "catalog seam missing"); return; }

        var vowelUi   = ui.Where(e => ShortVowels.IsMatch(e.Value)).Select(e => e.Key).ToList();
        var vowelHelp = help.Where(e => ShortVowels.IsMatch(e.Value)).Select(e => e.Key).ToList();
        H.Check("ar: UI catalog carries no short vowels (fatha/damma/kasra/sukun)", vowelUi.Count == 0,
                vowelUi.Count == 0 ? $"{ui.Count} rows" : $"{vowelUi.Count} rows: " + Excerpts(vowelUi));
        H.Check("ar: Help map carries no short vowels (fatha/damma/kasra/sukun)", vowelHelp.Count == 0,
                vowelHelp.Count == 0 ? $"{help.Count} rows" : $"{vowelHelp.Count} rows: " + Excerpts(vowelHelp));
        H.Check("ar: shadda and tanween are still allowed (they were kept on purpose)",
                help.Values.Any(v => v.Contains('\u0651')) || help.Values.Any(v => Regex.IsMatch(v, "[\u064B\u064C\u064D]")),
                "no shadda or tanween anywhere would mean a blanket strip, not the decided one");

        // A hyphenated numeric range reverses inside right-to-left text, so Arabic prose writes "من X إلى Y".
        // Code spans and the English key's own ranges are exempt; only ranges the English key ALSO has are
        // considered a range (a lone "045E-0B12" style id has letters and is not matched).
        var ranged = help.Concat(ui)
                         .Where(e => DigitRange.IsMatch(CodeSpan.Replace(e.Value, "")) && DigitRange.IsMatch(CodeSpan.Replace(e.Key, "")))
                         .Select(e => e.Key).ToList();
        H.Check("ar: no digit-hyphen-digit range survives in Arabic prose", ranged.Count == 0,
                ranged.Count == 0 ? null : $"{ranged.Count}: " + Excerpts(ranged));
        int worded = help.Values.Concat(ui.Values).Count(v => WordedRange.IsMatch(v));
        int englishRanges = help.Keys.Concat(ui.Keys).Count(k => DigitRange.IsMatch(CodeSpan.Replace(k, "")));
        H.Check("ar: the English ranges were written as \"من X إلى Y\"", englishRanges == 0 || worded > 0,
                $"{englishRanges} English keys carry a numeric range; {worded} Arabic values use the worded form");

        // Direction glyphs (docs/LOCALIZATION.md ▸ Direction glyphs in right-to-left text).
        // The D-pad pair names physical buttons: stored in English order in every language, and kept
        // left-to-right at render time by InlineMarkup.IsolateArrows / HelpHtmlExport. A swapped pair is the
        // retired workaround, which only displays right where the pair happens to touch Arabic text.
        var swapped = new List<string>();
        foreach (var lang in Shipped)
            foreach (var e in (HelpMap(lang) ?? new Dictionary<string, string>()).Concat(UiMap(lang) ?? new Dictionary<string, string>()))
                if (e.Value.Contains("🡆 🡄")) swapped.Add($"{lang}: {e.Key}");
        H.Check("the D-pad arrow pair is stored in English order (🡄 🡆) in every locale value", swapped.Count == 0,
                swapped.Count == 0 ? null : $"{swapped.Count}: " + Excerpts(swapped));

        // A path chevron points along the reading direction. A ▸ is right only inside an English island —
        // both nearest letters Latin, where the engine lays the path out left to right. Anywhere else the
        // path reads right to left and takes ◂; a Radiata menu path belongs in its on-screen Arabic names.
        var stray = new List<string>();
        foreach (var e in help.Concat(ui))
            for (int i = e.Value.IndexOf('▸'); i >= 0; i = e.Value.IndexOf('▸', i + 1))
                if (!IsLatin(NearestLetter(e.Value, i, -1)) || !IsLatin(NearestLetter(e.Value, i, +1)))
                    { stray.Add(e.Key); break; }
        H.Check("ar: every ▸ sits inside an English island; Arabic paths use ◂", stray.Count == 0,
                stray.Count == 0 ? null : $"{stray.Count}: " + Excerpts(stray));

        static char NearestLetter(string s, int from, int step)
        {
            for (int j = from + step; j >= 0 && j < s.Length; j += step) if (char.IsLetter(s[j])) return s[j];
            return '\0';
        }
        static bool IsLatin(char c) => c != '\0' && c < 'ɐ';
    }

    // ── 6. Japanese boundary spacing ────────────────────────────────────────────

    // Kana/kanji directly against an ASCII letter or digit, after code spans, tokens, link ids and URLs are removed.
    private static readonly Regex JaBoundary = new(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}][A-Za-z0-9]|[A-Za-z0-9][\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]", RegexOptions.Compiled);
    private static readonly Regex Markup = new(@"`[^`]*`|\{[^}]*\}|\[\[[^|\]]+\||\]\([^)]*\)|https?://\S+", RegexOptions.Compiled);

    private static void JapaneseBoundarySpacing()
    {
        var ui = UiMap("ja"); var help = HelpMap("ja");
        if (ui is null || help is null) { H.Skip("ja: boundary spacing", "catalog seam missing"); return; }
        var bad = new List<string>();
        int scanned = 0;
        foreach (var (k, v) in help.Concat(ui))
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            scanned++;
            if (JaBoundary.IsMatch(Markup.Replace(v, " "))) bad.Add(k);
        }
        H.Check($"ja: every kana/kanji ↔ Latin/digit boundary carries a space ({scanned} rows)", bad.Count == 0,
                bad.Count == 0 ? null : $"{bad.Count} rows: " + Excerpts(bad));
        // The convention is applied in the data, so make sure the scan itself can see a violation.
        H.Check("ja: the boundary scan detects an unspaced boundary", JaBoundary.IsMatch("設定1つ") && !JaBoundary.IsMatch("設定 1 つ") && !JaBoundary.IsMatch(Markup.Replace("`code`と{0}", " ")));
    }

    // ── 7. Culture-aware Game Grid sort ─────────────────────────────────────────

    private static void CultureAwareSort()
    {
        var culture = typeof(Loc).GetProperty("Culture");
        if (culture is null) { H.Fail("Loc.Culture missing", "the Game Grid comparer's culture seam is gone"); return; }

        var source = File.ReadAllText(Path.Combine(RepoRoot(), "GameBrowserControl.xaml.cs"));
        H.Check("the Game Grid comparer is built on Loc.Culture, case-insensitive",
                source.Contains("StringComparer.Create(Loc.Culture, ignoreCase: true)", StringComparison.Ordinal));

        var titles = new[] { "Zelda", "Émile", "emile", "ärger", "Arger", "Baldur", "Übel", "ubel", "Ökonom" };
        var de = StringComparer.Create(CultureInfo.GetCultureInfo("de"), ignoreCase: true);
        var sorted = titles.OrderBy(t => t, de).ToList();
        H.Check("de: accented titles interleave with their plain spellings",
                Adjacent(sorted, "Émile", "emile") && Adjacent(sorted, "ärger", "Arger") && Adjacent(sorted, "Übel", "ubel")
                && sorted.IndexOf("ärger") < sorted.IndexOf("Baldur") && sorted.IndexOf("Ökonom") < sorted.IndexOf("Zelda"),
                string.Join(" < ", sorted));
        var ordinal = titles.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        H.Check("ordinal (the old comparer) would have sunk them past Zelda",
                ordinal.IndexOf("ärger") > ordinal.IndexOf("Zelda") && ordinal.IndexOf("Émile") > ordinal.IndexOf("Zelda"),
                string.Join(" < ", ordinal));
        foreach (var code in Shipped)
        {
            var c = StringComparer.Create(CultureInfo.GetCultureInfo(code), ignoreCase: true);
            var s = titles.OrderBy(t => t, c).ToList();
            H.Check($"{code}: comparer interleaves Émile/emile and ärger/Arger", Adjacent(s, "Émile", "emile") && Adjacent(s, "ärger", "Arger"), string.Join(" < ", s));
        }

        // The real OrderForGrid, with the run culture pointed at German: same interleave through the actual seam.
        var grid = H.AppType("GameBrowserControl");
        var order = grid is null ? null : H.StaticMethod(grid, "OrderForGrid", 1);
        if (order is null) { H.Fail("GameBrowserControl.OrderForGrid missing", "the grid ordering seam was renamed"); return; }
        var before = culture.GetValue(null);
        try
        {
            culture.SetValue(null, CultureInfo.GetCultureInfo("de"));
            H.Try("OrderForGrid interleaves accented titles under de", () =>
            {
                var games = new[] { "Zelda", "ärger", "Arger", "Émile", "emile" }
                    .Select(n => new InstalledGame(n, "steam://rungameid/0", "Steam")).ToList();
                var result = ((IEnumerable<InstalledGame>)order.Invoke(null, new object[] { games })).Select(g => g.Name).ToList();
                // Favourites / last-launched come from GameArt state; synthetic titles carry none, so name order decides.
                H.Check("OrderForGrid under de: ärger beside Arger, Émile beside emile, none after Zelda",
                        result.Count == games.Count && Adjacent(result, "ärger", "Arger") && Adjacent(result, "Émile", "emile") && result.Last() == "Zelda",
                        string.Join(" < ", result));
            });
        }
        finally { culture.SetValue(null, before); }
    }

    private static bool Adjacent(List<string> list, string a, string b) => Math.Abs(list.IndexOf(a) - list.IndexOf(b)) == 1;

    // ── 8. Restore prompt ───────────────────────────────────────────────────────

    private static void RestorePromptNamesLanguage()
    {
        H.Check("the Restore prompt lists \"language\" among what a backup replaces",
                UiText.Settings.RestorePrompt.Contains("language", StringComparison.Ordinal), UiText.Settings.RestorePrompt);
        foreach (var code in Shipped)
        {
            var ui = UiMap(code);
            if (ui is null) continue;
            H.Check($"{code}: the Restore prompt is translated", ui.TryGetValue(UiText.Settings.RestorePrompt, out var v) && !string.IsNullOrWhiteSpace(v) && v != UiText.Settings.RestorePrompt);
        }
    }

    // ── 9. Marketing site ───────────────────────────────────────────────────────

    private static readonly Regex Hreflang = new(@"<link\s+rel=""alternate""\s+hreflang=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StylesVersion = new(@"styles\.css\?v=(\d+)", RegexOptions.Compiled);
    private static readonly string[] SiteLangs = { "en", "es", "de", "ja", "ar" };

    /// <summary>The marketing pages and the exported Help pages under packaging\webhost, in all five languages.
    /// Development repository only: the public snapshot carries none of the site docroot.</summary>
    private static void MarketingSite()
    {
        if (!H.DevRepoOnly("marketing site structure (packaging/webhost pages in five languages)", RepoRoot())) return;

        var siteRoot = SiteRoot;
        if (siteRoot is null || !Directory.Exists(siteRoot)) { H.Fail("marketing site structure", "packaging\\webhost not found above the harness binary"); return; }

        var versions = new Dictionary<string, string>();
        foreach (var folder in new[] { "", "download", "tip", "workshop" })
        {
            foreach (var lang in SiteLangs)
            {
                var file = Path.Combine(siteRoot, folder, lang == "en" ? "index.html" : lang + ".html");
                var label = (folder == "" ? "/" : "/" + folder + "/") + (lang == "en" ? "" : lang + ".html");
                if (!File.Exists(file)) { H.Fail($"site {label} exists", "missing"); continue; }
                var html = File.ReadAllText(file);
                var alternates = Hreflang.Matches(html).Select(m => m.Groups[1].Value).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var want = SiteLangs.Append("x-default").OrderBy(s => s, StringComparer.Ordinal).ToList();
                H.Check($"site {label}: six hreflang alternates incl. x-default", alternates.SequenceEqual(want), string.Join(",", alternates));
                H.Check($"site {label}: <html lang=\"{lang}\">", Regex.IsMatch(html, $@"<html\s[^>]*lang=""{lang}""", RegexOptions.IgnoreCase));
                bool rtl = Regex.IsMatch(html, @"<html\s[^>]*dir=""rtl""", RegexOptions.IgnoreCase);
                H.Check($"site {label}: dir=\"rtl\" {(lang == "ar" ? "present" : "absent")}", rtl == (lang == "ar"));
                var v = StylesVersion.Match(html);
                if (v.Success) versions[label] = v.Groups[1].Value; else H.Fail($"site {label}: links styles.css?v=NN", "no versioned stylesheet link");
            }
        }
        var distinct = versions.Values.Distinct().ToList();
        H.Check("site: every marketing page links the same styles.css version", distinct.Count == 1,
                distinct.Count == 1 ? $"?v={distinct[0]} on {versions.Count} pages" : string.Join("; ", versions.GroupBy(kv => kv.Value).Select(g => $"v={g.Key}: {string.Join(",", g.Select(kv => kv.Key))}")));

        // The exported Help pages share the site's stylesheet through ../styles.css, so their cache-buster must move
        // with the site's or a browser that cached an older copy keeps it on /help/ only.
        var helpVersions = new Dictionary<string, string>();
        foreach (var lang in SiteLangs)
        {
            var file = Path.Combine(siteRoot, "help", lang == "en" ? "index.html" : lang + ".html");
            if (!File.Exists(file)) { H.Fail($"site /help/{(lang == "en" ? "index" : lang)}.html exists", "missing"); continue; }
            var html = File.ReadAllText(file);
            var alternates = Hreflang.Matches(html).Select(m => m.Groups[1].Value).OrderBy(s => s, StringComparer.Ordinal).ToList();
            H.Check($"site /help/{lang}: six hreflang alternates incl. x-default", alternates.Count == 6 && alternates.Contains("x-default") && SiteLangs.All(alternates.Contains));
            if (lang == "ar") H.Check("site /help/ar.html reads right-to-left", Regex.IsMatch(html, @"<html\s[^>]*dir=""rtl""", RegexOptions.IgnoreCase));
            var v = StylesVersion.Match(html);
            if (v.Success) helpVersions[lang] = v.Groups[1].Value;

            // The site contract the exporter reproduces (docs/LOCALIZATION.md ▸ Web Help site contract): shared nav
            // with the switcher inside it, shared favicon + flattened asset paths, Ko-fi loader, and no glyph
            // from the blocks browsers can't draw.
            H.Check($"site /help/{lang}: shared site nav present", html.Contains("<nav class=\"sitenav\"", StringComparison.Ordinal));
            H.Check($"site /help/{lang}: language switcher inside the nav", Regex.IsMatch(html, @"<nav class=""sitenav""[\s\S]*?<nav class=""langs""[\s\S]*?</nav>\s*</div>\s*</nav>"));
            // The cache-buster on the favicon and the Ko-fi loader moves with the site, so both are matched
            // with an optional ?v=N rather than a fixed string — see HelpHtmlExport.SiteFaviconVersion.
            H.Check($"site /help/{lang}: shared favicon file", Regex.IsMatch(html, @"href=""/assets/favicon\.svg(\?v=\d+)?""") && !html.Contains("data:image/svg+xml", StringComparison.Ordinal));
            H.Check($"site /help/{lang}: no dead assets/radiata/ path", !html.Contains("assets/radiata/", StringComparison.Ordinal));
            H.Check($"site /help/{lang}: Ko-fi loader", Regex.IsMatch(html, @"<script src=""/assets/kofi\.js(\?v=\d+)?"" defer></script>"));
            H.Check($"site /help/{lang}: rail and anchors clear the sticky nav", html.Contains("scroll-margin-top:calc(var(--nav-h)", StringComparison.Ordinal) && html.Contains("top:calc(var(--nav-h)", StringComparison.Ordinal));
            H.Check($"site /help/{lang}: title follows Radiata — <page>", Regex.IsMatch(html, @"<title>Radiata — [^<(]+</title>"));
            var tofu = new List<string>();
            for (int i = 0; i + 1 < html.Length; i++)
                if (char.IsHighSurrogate(html[i]))
                {
                    int cp = char.ConvertToUtf32(html[i], html[i + 1]);
                    if (cp is >= 0x1F780 and <= 0x1F8FF or >= 0x1FB00 and <= 0x1FBFF) tofu.Add($"U+{cp:X}");
                    i++;
                }
            H.Check($"site /help/{lang}: no glyphs from font-less Unicode blocks", tofu.Count == 0, string.Join(",", tofu.Distinct()));
        }
        if (distinct.Count == 1 && helpVersions.Count > 0)
            H.Check("site: the exported Help pages link the same styles.css version as the marketing pages",
                    helpVersions.Values.All(v => v == distinct[0]),
                    $"help ?v={string.Join("/", helpVersions.Values.Distinct())} vs site ?v={distinct[0]} (re-export the Help pages, or pass --css-version=N; HelpHtmlExport.ResolveStylesVersion sniffs the site root)");
    }

    // ── 10. Pseudo-localization keeps tokens ─────────────────────────────────────

    private static void PseudoKeepsTokens()
    {
#if DEBUG
        var tokenish = new Regex(@"\{[^}]*\}|\[\[[^|\]]+\||\]\([^)]*\)", RegexOptions.Compiled);
        var samples = Loc.SourceStrings().Where(s => !s.StartsWith("plural:", StringComparison.Ordinal) && tokenish.IsMatch(s)).ToList();
        var padChips = samples.Where(s => s.Contains("{pad:", StringComparison.Ordinal)).ToList();
        var links    = samples.Where(s => s.Contains("[[", StringComparison.Ordinal) || s.Contains("](http", StringComparison.Ordinal)).ToList();
        // ⚠ Pad chips are NOT required of the catalog. InlineMarkup still parses {pad:X}, but no shipped
        // string carries one: the Steam sentry card, its last user, now places ControllerButton elements
        // itself because its tiles are laid out side by side, which inline flow can't express. The
        // pseudoizer's chip handling is covered by the synthetic case below instead.
        H.Check("real catalog strings with {n} and [[link]] tokens exist to test against",
                samples.Count >= 10 && links.Count > 0, $"{samples.Count} tokenised, {padChips.Count} with pad chips, {links.Count} with links");
        var broken = new List<string>();
        foreach (var s in samples)
        {
            var ps = Loc.Pseudoize(s);
            foreach (Match m in tokenish.Matches(s))
                if (!ps.Contains(m.Value, StringComparison.Ordinal)) { broken.Add(s); break; }
        }
        H.Check($"Pseudoize keeps every placeholder, pad chip and link token intact ({samples.Count} real strings)", broken.Count == 0,
                broken.Count == 0 ? null : $"{broken.Count}: " + Excerpts(broken));
        // Synthetic, because the catalog no longer ships a pad chip (see above). The unit under test is
        // Loc.Pseudoize, not the catalog: a chip token and an emphasis run must both survive verbatim, so
        // the markup still works the day a string wants one again.
        const string chipLine = "Press {pad:Triangle} to relaunch Steam (*games will quit*)";
        H.Check("Pseudoize keeps {pad:Triangle} and *emphasis* intact",
                Loc.Pseudoize(chipLine).Contains("{pad:Triangle}") && Loc.Pseudoize(chipLine).Contains("*"));
#else
        H.Skip("Pseudoize keeps tokens", "Loc.Pseudoize is compiled into Debug builds only");
#endif
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static IEnumerable<(string Name, string Value)> UiTextConsts()
    {
        foreach (var t in Walk(typeof(UiText)))
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string)) yield return (t.Name + "." + f.Name, (string)f.GetRawConstantValue());

        static IEnumerable<Type> Walk(Type t)
        {
            yield return t;
            foreach (var n in t.GetNestedTypes()) foreach (var d in Walk(n)) yield return d;
        }
    }

    private static IReadOnlyDictionary<string, string> UiMap(string code)
    {
        var m = H.StaticMethod(typeof(Loc), "MapFor", 1);
        return m?.Invoke(null, new object[] { code }) as IReadOnlyDictionary<string, string>;
    }

    private static IReadOnlyDictionary<string, string> HelpMap(string code)
    {
        var m = H.StaticMethod(typeof(HelpLocalization), "MapFor", 1);
        return m?.Invoke(null, new object[] { code }) as IReadOnlyDictionary<string, string>;
    }

    private static IEnumerable<string> AllValues(string code) =>
        (UiMap(code)?.Values ?? Enumerable.Empty<string>()).Concat(HelpMap(code)?.Values ?? Enumerable.Empty<string>());

    /// <summary>Point Loc.Lang at another language for one check. The property's setter is private because the app
    /// fixes the language per run; the harness is the one caller allowed to move it, and it always moves it back.</summary>
    private static IDisposable SetLang(string code)
    {
        var p = typeof(Loc).GetProperty("Lang") ?? throw new MissingMemberException("Loc.Lang");
        var before = p.GetValue(null);
        p.SetValue(null, code);
        return new Restore(() => p.SetValue(null, before));
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _undo;
        public Restore(Action undo) { _undo = undo; }
        public void Dispose() => _undo();
    }

    private static string Excerpts(IEnumerable<string> keys) =>
        string.Join(" | ", keys.Take(5).Select(k => k.Length <= 70 ? k : k[..70] + "…"));

    // Throws (unlike H.RepoRoot()'s null): every caller here builds a path from it immediately with no
    // null check, so a missing repo would otherwise surface as a confusing downstream NullReferenceException.
    private static string RepoRoot() =>
        H.RepoRoot() ?? throw new InvalidOperationException("repo root not found from " + AppContext.BaseDirectory);
}
