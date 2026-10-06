using System;
using System.Collections.Generic;
using System.Linq;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Searching Help in all three languages, not just reading it.
///
/// What this can and can't settle: <see cref="HelpLocalization.Search"/> IS the search the pane runs, and
/// <see cref="HelpLocalization.Chrome"/> going through <c>Text(code, …)</c> is how the pane's own labels
/// translate — so the keyword-union claim and the chrome claim are both testable here. What's NOT covered
/// is whether the XAML binds to these (it could translate correctly and render in the wrong font, or a
/// label could be a XAML literal that never reaches this map). Treat a pass as "the strings are right".</summary>
internal static class T_Help
{
    private static readonly string[] Langs = { "es", "de", "ja", "ar" };

    public static void Run()
    {
        H.Group("Help — search + chrome in es / de / ja / ar");

        var en = HelpLocalization.Topics("en");
        H.Check("English topic set is non-trivial", en.Count > 10, $"{en.Count} topics");

        foreach (var lang in Langs)
        {
            var name = HelpLocalization.Language(lang).EnglishName;
            var topics = HelpLocalization.Topics(lang);

            H.Check($"[{lang}] same topic COUNT as English — no topic lost in translation",
                    topics.Count == en.Count, $"{topics.Count} vs {en.Count}");

            H.Check($"[{lang}] same topic IDS as English",
                    topics.Select(t => t.Id).OrderBy(x => x).SequenceEqual(en.Select(t => t.Id).OrderBy(x => x)));

            // Titles should actually BE translated (ja/es/de), not silently falling back to English.
            int translatedTitles = topics.Count(t => t.Title != en.First(e => e.Id == t.Id).Title);
            H.Check($"[{lang}] most titles differ from English (i.e. translated)",
                    translatedTitles >= en.Count * 0.6,
                    $"{translatedTitles}/{en.Count} titles translated");

            // ── An ENGLISH term still finds the topic while the pane is in another language. The union is
            //    built over the KEYWORD sets (HelpLocalization.Topics line ~97), so every English keyword is
            //    the contract — an English TITLE word is not, and is measured separately below.
            int kwHits = 0, kwTried = 0;
            var kwMisses = new List<string>();
            foreach (var e in en)
            {
                foreach (var kw in e.Keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (kw.Length < 4) continue;
                    kwTried++;
                    if (HelpLocalization.Search(lang, kw).Any(t => t.Id == e.Id)) kwHits++;
                    else if (kwMisses.Count < 5) kwMisses.Add($"{e.Id}:\"{kw}\"");
                }
            }
            H.Check($"[{lang}] every ENGLISH KEYWORD still finds its topic (the documented union)",
                    kwTried > 0 && kwHits == kwTried,
                    kwHits == kwTried ? $"{kwHits} keyword terms checked"
                                      : $"{kwHits}/{kwTried} — misses: {string.Join(", ", kwMisses)}");

            // ── An English TITLE word is a weaker claim: titles are replaced, not unioned. Recorded rather
            //    than asserted, because it's a design question (does a Spanish user typing the English tab
            //    name find the topic?) rather than a regression.
            int titleHits = 0, titleTried = 0;
            var titleMisses = new List<string>();
            foreach (var e in en)
            {
                var term = FirstDistinctiveWord(e.Title);
                if (term is null) continue;
                titleTried++;
                if (HelpLocalization.Search(lang, term).Any(t => t.Id == e.Id)) titleHits++;
                else if (titleMisses.Count < 6) titleMisses.Add($"\"{term}\" ({e.Id})");
            }
            Console.WriteLine($"        (record) [{lang}] English TITLE word finds its topic: {titleHits}/{titleTried}" +
                              $"{(titleMisses.Count > 0 ? " — e.g. " + string.Join(", ", titleMisses) : "")}");

            // ── A term from the TRANSLATED title finds the topic too.
            int nativeHits = 0, nativeTried = 0;
            foreach (var t in topics)
            {
                var term = lang == "ja" ? FirstJapaneseRun(t.Title)
                         : lang == "ar" ? FirstArabicRun(t.Title)
                         : FirstDistinctiveWord(t.Title);
                if (term is null) continue;
                nativeTried++;
                if (HelpLocalization.Search(lang, term).Any(x => x.Id == t.Id)) nativeHits++;
            }
            H.Check($"[{lang}] a word from the TRANSLATED title finds its topic",
                    nativeTried > 0 && nativeHits == nativeTried,
                    $"{nativeHits}/{nativeTried}");

            // ── Results come back in the picked language (titles are the localized ones).
            var sample = HelpLocalization.Search(lang, FirstDistinctiveWord(en[0].Title) ?? "wheel").ToList();
            H.Check($"[{lang}] results carry LOCALIZED titles, not English",
                    sample.Count == 0 || sample.All(s => s.Title == topics.First(t => t.Id == s.Id).Title));

            // ── The pane's own chrome follows the language (string level).
            var chrome = new (string Name, string En)[]
            {
                (nameof(HelpLocalization.Chrome.Contents),    HelpLocalization.Chrome.Contents),
                (nameof(HelpLocalization.Chrome.NoMatch),     HelpLocalization.Chrome.NoMatch),
                (nameof(HelpLocalization.Chrome.Language),    HelpLocalization.Chrome.Language),
                (nameof(HelpLocalization.Chrome.SearchTip),   HelpLocalization.Chrome.SearchTip),
                (nameof(HelpLocalization.Chrome.LanguageTip), HelpLocalization.Chrome.LanguageTip),
                (nameof(HelpLocalization.Chrome.BackTip),     HelpLocalization.Chrome.BackTip),
                (nameof(HelpLocalization.Chrome.TopicLinkTip),HelpLocalization.Chrome.TopicLinkTip),
            };
            var untranslated = chrome.Where(c => HelpLocalization.Text(lang, c.En) == c.En).Select(c => c.Name).ToList();
            H.Check($"[{lang}] Help-pane chrome is translated",
                    untranslated.Count == 0,
                    untranslated.Count == 0 ? $"all {chrome.Length} strings" : "still English: " + string.Join(", ", untranslated));

            // ── The machine-translation notice shows for translated languages and not for English.
            H.Check($"[{lang}] machine-translation notice present",
                    HelpLocalization.TranslationNotice(lang) is not null, name);
        }

        H.Check("English shows NO machine-translation notice",
                HelpLocalization.TranslationNotice("en") is null);

        // Search hygiene: a blank query lists everything; nonsense matches nothing (→ the NoMatch chrome).
        H.Check("blank query returns all topics", HelpLocalization.Search("es", "   ").Count() == en.Count);
        H.Check("nonsense query returns nothing", !HelpLocalization.Search("de", "zzzqqxnotaword").Any());

        // --check-help-locales reports "2 STILL ENGLISH" per language without naming them. Name them: an
        // untranslated proper noun is fine, an untranslated sentence is a gap.
        foreach (var lang in Langs)
        {
            var topics = HelpLocalization.Topics(lang);
            var same = new List<string>();
            foreach (var t in topics)
            {
                var e = en.First(x => x.Id == t.Id);
                if (t.Title == e.Title) same.Add($"{t.Id} TITLE: \"{t.Title}\"");
                for (int i = 0; i < e.Body.Length; i++)
                {
                    // The notice is prepended to the first topic in translated languages, so body indices
                    // shift by one there — compare by content, not position.
                    var body = t.Body.Select(b => b.Text).ToList();
                    if (!body.Contains(HelpLocalization.Text(lang, e.Body[i].Text))) continue;
                    if (HelpLocalization.Text(lang, e.Body[i].Text) == e.Body[i].Text
                        && e.Body[i].Text.Trim().Length > 0)
                        same.Add($"{t.Id} BODY: \"{Trim(e.Body[i].Text)}\"");
                }
            }
            Console.WriteLine($"        (record) [{lang}] strings identical to English: {same.Count}");
            foreach (var s in same.Distinct().Take(6)) Console.WriteLine($"        (record)     {s}");
        }

        // Multi-term search is AND, per the implementation.
        var two = HelpLocalization.Search("en", "wheel slice").ToList();
        Console.WriteLine($"        (record) \"wheel slice\" (AND) → {two.Count} topics");

        PublicReleaseGate();
    }

    /// <summary>The Help side of <see cref="ReleaseGates"/>. The harness
    /// runs against a Debug build, so the catalog and package gates (build consts) are out of reach here; the
    /// Help set is the one surface with a runtime preview, and it must drop every gated topic AND every gated
    /// block, in every language, then come back — while the translation source set keeps every string.</summary>
    private static void PublicReleaseGate()
    {
        H.Group("Help — public-release gate (ReleaseGates.PreviewPublic)");

        string[] gatedIds = { "arcade-internode", "custom-arcade-games", "custom-materials", "workshop", "workshop-sharing" };
        const string fourGames  = "[[arcade-petalpop|Petalpop]] and [[arcade-internode|Internode]]";
        const string threeGames = "[[arcade-connate|Connate]] and [[arcade-petalpop|Petalpop]]";
        const string ownGames   = "**You can write your own games**";
        const string ownTheme   = "**Make your own material**";
        const string explorer   = "Show in Explorer";

        static string ArcadeBody(string lang) =>
            string.Join("\n", HelpLocalization.Topics(lang).First(t => t.Id == "arcade").Body.Select(b => b.Text));
        static string CustomizeBody(string lang) =>
            string.Join("\n", HelpLocalization.Topics(lang).First(t => t.Id == "customize").Body.Select(b => b.Text));
        static string TrayBody(string lang) =>
            string.Join("\n", HelpLocalization.Topics(lang).First(t => t.Id == "tray-and-settings").Body.Select(b => b.Text));

        // A public-flavour build compiles the gated features off, so the dev posture does not exist there.
        const string noDev = "public-flavour build: the dev flavour's features are compiled off";
        static void DevCheck(string what, bool ok)
        {
            if (ReleaseGates.PublicRelease) H.Skip(what, noDev); else H.Check(what, ok);
        }

        // Dev posture first: everything offered.
        DevCheck("dev posture: every gated topic is offered",
                gatedIds.All(id => HelpContent.Topics.Any(t => t.Id == id)));
        DevCheck("dev posture: the four-game line shows, the three-game line does not",
                ArcadeBody("en").Contains(fourGames) && !ArcadeBody("en").Contains(threeGames));
        DevCheck("dev posture: both pointer bullets show",
                ArcadeBody("en").Contains(ownGames) && CustomizeBody("en").Contains(ownTheme));
        DevCheck("dev posture: exactly one tray line shows, and it names Show in Explorer",
                TrayBody("en").Contains(explorer) && TrayBody("en").Split("**Tray icon:**").Length == 2);

        var source = HelpLocalization.SourceStrings().ToHashSet(StringComparer.Ordinal);
        H.Check("translation source set holds BOTH game-list variants (gated strings stay translatable)",
                source.Any(s => s.Contains(fourGames)) && source.Any(s => s.Contains(threeGames)));
        H.Check("translation source set holds the gated topics' titles",
                gatedIds.All(id => source.Contains(HelpContent.AllAuthored.First(t => t.Id == id).Title)));

        try
        {
            ReleaseGates.PreviewPublic = true;

            H.Check("public posture: every gated topic is absent",
                    gatedIds.All(id => !HelpContent.Topics.Any(t => t.Id == id)));
            H.Check("public posture: the Workshop category is empty, so neither Help surface shows its heading",
                    !HelpContent.Topics.Any(t => t.Category == "Workshop") && !HelpContent.ExportTopics.Any(t => t.Category == "Workshop"));
            H.Check("public posture: the export set drops them too",
                    gatedIds.All(id => !HelpContent.ExportTopics.Any(t => t.Id == id)));
            H.Check("public posture: the three-game line shows, the four-game line does not",
                    ArcadeBody("en").Contains(threeGames) && !ArcadeBody("en").Contains(fourGames));
            H.Check("public posture: neither pointer bullet shows",
                    !ArcadeBody("en").Contains(ownGames) && !CustomizeBody("en").Contains(ownTheme));
            H.Check("public posture: exactly one tray line shows, and it omits Show in Explorer",
                    !TrayBody("en").Contains(explorer) && TrayBody("en").Split("**Tray icon:**").Length == 2);
            H.Check("public posture: searching \"package\" returns nothing (English)",
                    !HelpLocalization.Search("en", "package").Any());
            var packageWord = new Dictionary<string, string> { ["es"] = "paquete", ["de"] = "paket", ["ja"] = "パッケージ", ["ar"] = "حزمة" };
            foreach (var (pl, word) in packageWord)
                H.Check($"[{pl}] public posture: searching \"{word}\" and \"package\" returns nothing",
                        !HelpLocalization.Search(pl, word).Any() && !HelpLocalization.Search(pl, "package").Any());
            H.Check("public posture: no remaining topic cross-links to a gated topic",
                    !HelpContent.Topics.SelectMany(t => t.Body).Any(b => gatedIds.Any(id => b.Text.Contains($"[[{id}")))
                    && !HelpContent.Topics.Any(t => t.Keywords.Contains("internode", StringComparison.OrdinalIgnoreCase)));

            foreach (var lang in Langs)
            {
                var topics = HelpLocalization.Topics(lang);
                H.Check($"[{lang}] public posture follows (cache rebuilt, not latched)",
                        gatedIds.All(id => !topics.Any(t => t.Id == id))
                        && !ArcadeBody(lang).Contains("arcade-internode") && !CustomizeBody(lang).Contains("custom-materials"));
            }
        }
        finally { ReleaseGates.PreviewPublic = false; }

        DevCheck("back to dev posture: the gated topics return",
                gatedIds.All(id => HelpContent.Topics.Any(t => t.Id == id))
                && ArcadeBody("es").Contains("arcade-internode"));
    }

    private static string Trim(string s) => s.Length <= 70 ? s : s[..70] + "...";

    /// <summary>A word from a title that's long enough not to match half the corpus by accident.</summary>
    private static string FirstDistinctiveWord(string title)
    {
        foreach (var w in title.Split(' ', '/', '(', ')', ',', '—', '-', ':'))
        {
            var t = w.Trim();
            if (t.Length >= 5 && t.All(char.IsLetter)) return t;
        }
        return null;
    }

    /// <summary>Japanese doesn't space-separate, so take a run of CJK/kana characters instead.</summary>
    private static string FirstJapaneseRun(string title)
    {
        var best = "";
        var cur = "";
        foreach (var ch in title)
        {
            bool cjk = (ch >= 0x3040 && ch <= 0x30FF) || (ch >= 0x4E00 && ch <= 0x9FFF);
            if (cjk) { cur += ch; if (cur.Length > best.Length) best = cur; }
            else cur = "";
        }
        return best.Length >= 2 ? best : null;
    }

    /// <summary>Arabic is space-separated but its words are short (three or four letters is normal), so the
    /// Latin five-letter threshold finds nothing; take the first run of Arabic-block letters of three or more.</summary>
    private static string FirstArabicRun(string title)
    {
        var best = "";
        var cur = "";
        foreach (var ch in title)
        {
            bool ar = ch >= 0x0620 && ch <= 0x064A;   // letters only — skips harakat (064B–0652) and digits
            if (ar) { cur += ch; if (cur.Length > best.Length) best = cur; }
            else cur = "";
        }
        return best.Length >= 3 ? best : null;
    }
}
