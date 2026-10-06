namespace ControllerWheel;

/// <summary>One Help-tab display language. <paramref name="FontFamily"/> is a WPF font-fallback list —
/// Latin scripts ride the Settings window's own face, Japanese names a CJK-capable chain so the topic
/// pane can't fall back to a tofu box on a machine without the default UI font's CJK coverage. <paramref name="Rtl"/>
/// flips the pane's reading direction for right-to-left scripts — text and layout only, never the illustrations.</summary>
public sealed record HelpLanguage(string Code, string NativeName, string EnglishName, string FontFamily, bool Rtl = false);

/// <summary>Help-topic localization. <see cref="HelpContent"/> stays the single canonical (English)
/// source — this layer maps each authored English string to a translation, keyed by the English text
/// itself rather than by position.
///
/// <para><b>Why keyed by text.</b> An English edit changes the key, so the affected string simply falls
/// back to English instead of silently displaying a translation of the old wording — a wrong translation
/// is worse than an untranslated line. It also makes staleness mechanically checkable:
/// <c>Radiata.exe --check-help-locales</c> lists every English string with no translation (new/edited)
/// and every translation whose English key no longer exists (stale). See docs/LOCALIZATION.md.</para>
///
/// <para><b>Translation conventions</b> (docs/LOCALIZATION.md has the full list): the app's UI is English, so
/// every UI label, tab name, button caption, chord name, config key, and file path stays in English —
/// spelled exactly as it appears on screen — with a translated gloss in parentheses the first time it
/// appears in a topic. Markup (<c>**bold**</c>, <c>`code`</c>, <c>[[topic-id|Label]]</c>,
/// <c>[Label](url)</c>) and <c>{tokens}</c> must survive translation verbatim; a cross-link's topic-id
/// is an identifier and is never translated, only its label is.</para></summary>
public static class HelpLocalization
{
    /// <summary>Display order of the Help tab's language picker; the first entry is the canonical source.</summary>
    public static readonly HelpLanguage[] Languages =
    [
        new("en", "English",  "English",  "Segoe UI"),
        new("es", "Español",  "Spanish",  "Segoe UI"),
        new("de", "Deutsch",  "German",   "Segoe UI"),
        new("ja", "日本語",     "Japanese", "Segoe UI, Yu Gothic UI, Meiryo, MS Gothic"),
        // Modern Standard Arabic. Segoe UI carries Arabic on Win10/11; Tahoma is the long-standing Windows
        // Arabic UI face. Rtl flips the Help pane's reading direction — text only, never the illustrations.
        new("ar", "العربية",   "Arabic",   "Segoe UI, Tahoma, Arial", Rtl: true),
#if DEBUG
        // Pseudo-localization, dev builds only: the UI catalog comes back bracketed and ~30% longer (Loc.Pseudoize);
        // qps-rtl additionally mirrors the windows. Help stays English under both. Not in Release, so a config
        // cannot name them (Normalize rejects unknown codes).
        new("qps",     "Pseudo (LTR)", "Pseudo", "Segoe UI"),
        new("qps-rtl", "Pseudo (RTL)", "Pseudo RTL", "Segoe UI", Rtl: true),
#endif
    ];

    public const string DefaultCode = "en";

    public static bool IsKnown(string? code) =>
        code is not null && Languages.Any(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>An unknown / null / differently-cased code becomes a usable one (config is a text file a
    /// user can edit, and a backup can come from a newer build with a language this one doesn't have).</summary>
    public static string Normalize(string? code) =>
        Languages.FirstOrDefault(l => l.Code.Equals(code ?? "", StringComparison.OrdinalIgnoreCase))?.Code
        ?? DefaultCode;

    public static HelpLanguage Language(string? code) =>
        Languages.First(l => l.Code == Normalize(code));

    /// <summary>The per-language English→translation map, or null for English (and unknown codes).</summary>
    private static IReadOnlyDictionary<string, string>? MapFor(string code) => Normalize(code) switch
    {
        "es" => HelpTextEs.Map,
        "de" => HelpTextDe.Map,
        "ja" => HelpTextJa.Map,
        "ar" => HelpTextAr.Map,
        _    => null,
    };

    /// <summary>Translate one authored string; untranslated text passes through in English.</summary>
    public static string Text(string code, string english) =>
        MapFor(code) is { } map && map.TryGetValue(english, out var t) && !string.IsNullOrWhiteSpace(t)
            ? t : english;

    /// <summary>A category heading in the picked language (falls back to the English name).</summary>
    public static string Category(string code, string englishCategory) => Text(code, englishCategory);

    // ── Localized topic set (built once per language, then cached) ───────────────

    private static readonly Dictionary<string, IReadOnlyList<HelpTopic>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _gate = new();
    /// <summary>The <see cref="HelpContent.Topics"/> instance <see cref="_cache"/> was built from. A different
    /// instance means the topic set changed (a feature-gated topic came or went) and every language's build is
    /// stale — see the comment in <see cref="Topics"/>.</summary>
    private static IReadOnlyList<HelpTopic>? _cacheSource;

    /// <summary>Every topic with its title, keywords, and body text in the requested language. Structure
    /// (topic ids, categories, block kinds, indents, LiveOnly flags, order) always comes from the English
    /// source, so a translation can never reshape the Help tab — only re-word it.
    /// <para>Search keywords are the union of the translated and English keyword sets: the UI itself is
    /// English, so a German-speaking user searching "HidHide" or "Game Grid" must still find the topic.</para>
    /// <para>Non-English languages get the machine-translation notice prepended to the first topic —
    /// see <see cref="TranslationNotice"/>.</para></summary>
    public static IReadOnlyList<HelpTopic> Topics(string? code)
    {
        var lang = Normalize(code);
        var source = HelpContent.Topics;
        if (lang == DefaultCode) return source;
        lock (_gate)
        {
            // ⚠ The cache is keyed on the language but built from HelpContent.Topics, which is not fixed
            // for the life of the process — a feature-gated topic (Arcade) appears and disappears at
            // runtime, so caching on the language alone would serve a stale set. Comparing the source
            // object identity is enough, because HelpContent.Topics hands back the same cached instance
            // until its own gate moves.
            if (!ReferenceEquals(_cacheSource, source)) { _cache.Clear(); _cacheSource = source; }
            if (_cache.TryGetValue(lang, out var cached)) return cached;
            var built = new List<HelpTopic>(source.Count);
            bool first = true;
            foreach (var t in source)
            {
                var body = t.Body.Select(b => b with { Text = Text(lang, b.Text) }).ToList();
                if (first && TranslationNotice(lang) is { } notice) body.Insert(0, notice);
                first = false;
                var keywords = Text(lang, t.Keywords);
                built.Add(new HelpTopic(
                    t.Id,
                    t.Category,                                    // grouping key stays English; display is localized
                    Text(lang, t.Title),
                    keywords == t.Keywords ? t.Keywords : $"{keywords} {t.Keywords}",
                    [.. body]));
            }
            _cache[lang] = built;
            return built;
        }
    }

    /// <summary>Case-insensitive search over the localized title, keyword union, and body text.</summary>
    public static IEnumerable<HelpTopic> Search(string? code, string query)
    {
        var topics = Topics(code);
        if (string.IsNullOrWhiteSpace(query)) return topics;
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return topics.Where(t => terms.All(term =>
            t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            || t.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase)
            || t.Body.Any(b => b.Text.Contains(term, StringComparison.OrdinalIgnoreCase))));
    }

    // ── The machine-translation notice ──────────────────────────────────────────

    /// <summary>Key the notice text is stored under, so each language file translates it like any other
    /// string. English shows no notice (nothing was translated).</summary>
    internal const string NoticeKey =
        "**These help pages and Radiata's interface were translated by an AI language model.** The translation "
        + "may be imperfect or incomplete. The developer is not responsible for mistakes in the translated text; "
        + "the English version is the authoritative one. Support requests can only be answered in English — "
        + "messages in other languages will not receive a reply. A support answer names buttons, tabs and settings by "
        + "their English labels; switch Radiata to English for a moment to follow one.";

    // ── Help-pane chrome ────────────────────────────────────────────────────────

    /// <summary>The Help pane's own labels and tooltips. They live here — not as XAML literals — so they
    /// translate through the same map (and the same parity check) as the topics. This is the only UI chrome
    /// that follows the Help language; every other tab stays English, which is why the topics themselves
    /// still name buttons and settings in English.</summary>
    public static class Chrome
    {
        public const string Contents      = "Contents";
        public const string NoMatch       = "No topics match.";
        public const string Language      = "Language";
        public const string SearchTip     = "Search help topics";
        public const string LanguageTip   = "Help topics language";
        public const string BackTip       = "Back to where you were";
        public const string TopicLinkTip  = "Open this Help topic";

        internal static readonly string[] All =
            [Contents, NoMatch, Language, SearchTip, LanguageTip, BackTip, TopicLinkTip];
    }

    /// <summary>The notice block for a non-English language (null for English).</summary>
    public static HelpBlock? TranslationNotice(string? code)
    {
        var lang = Normalize(code);
        return lang == DefaultCode ? null : new HelpBlock(HelpBlockKind.Warning, Text(lang, NoticeKey));
    }

    // ── Maintenance checks (`Radiata.exe --check-help-locales`) ──────────────────

    /// <summary>Every English string the Help tab needs translated: category names, the notice, each topic's
    /// title, keywords, and block texts (LiveOnly blocks included — they render in-app), and the labels drawn
    /// inside the illustrations. Figure labels that are button or key names are excluded — they stay English
    /// like the rest of the UI (see <see cref="FigPart.Literal"/>).
    /// <para>Walks <see cref="HelpContent.AllAuthored"/>, not the gated <see cref="HelpContent.Topics"/>: a
    /// topic or block withheld from a public release (<see cref="ReleaseGates"/>) is still authored, and its
    /// translations must be kept current so the feature can return without a re-translation.</para></summary>
    public static IEnumerable<string> SourceStrings()
    {
        yield return NoticeKey;
        foreach (var c in Chrome.All) yield return c;
        foreach (var c in HelpContent.CategoryOrder) yield return c;
        foreach (var t in HelpContent.AllAuthored)
        {
            yield return t.Title;
            yield return t.Keywords;
            foreach (var b in t.Body) yield return b.Text;
        }
        foreach (var label in HelpFigures.TranslatableText()) yield return label;
    }

    /// <summary>Per-language parity report: strings awaiting translation, and translations whose English
    /// key no longer exists (i.e. the source was edited or removed — the entry is dead weight and the line
    /// is currently showing in English).</summary>
    public static string CheckReport()
    {
        var source = SourceStrings().Distinct().ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Help localization check — {source.Count} translatable strings "
                      + "(Core/HelpContent.cs topics + figure labels + the notice + HelpLocalization.Chrome)");
        foreach (var problem in FigureWiringProblems()) sb.AppendLine($"  FIGURE   {problem}");
        foreach (var lang in Languages.Where(l => l.Code != DefaultCode && !l.Code.StartsWith("qps", StringComparison.Ordinal)))
        {
            var map = MapFor(lang.Code) ?? new Dictionary<string, string>();
            var missing = source.Where(s => !map.ContainsKey(s) || string.IsNullOrWhiteSpace(map[s])).ToList();
            var stale   = map.Keys.Where(k => !source.Contains(k)).ToList();
            sb.AppendLine();
            sb.AppendLine($"── {lang.EnglishName} ({lang.Code}): {source.Count - missing.Count}/{source.Count} translated, "
                          + $"{missing.Count} missing, {stale.Count} stale");
            // Build the localized set too — this is the path the Help tab renders, so the report doubles as
            // a smoke test that a language file can't break topic assembly (and confirms the notice landed).
            var topics = Topics(lang.Code);
            bool notice = topics.Count > 0 && topics[0].Body.Length > 0
                          && topics[0].Body[0].Kind == HelpBlockKind.Warning;
            sb.AppendLine($"   built {topics.Count} topics ({HelpContent.Topics.Count} in source), "
                          + $"translation notice on first topic: {(notice ? "yes" : "NO — expected on a non-English language")}");
            // Rendered-coverage check, from the other end: walk what the pane would actually draw and
            // count anything still in English. "0 missing keys" only proves every source string has an
            // entry; this proves none of them resolved back to English (a blank/whitespace value, or a
            // translation accidentally left as the English sentence).
            int blocks = 0, english = 0;
            foreach (var t in topics)
            {
                if (t.Title == HelpContent.Topics.First(s => s.Id == t.Id).Title) english++;
                blocks++;
                foreach (var b in t.Body)
                {
                    blocks++;
                    var src = HelpContent.Topics.First(s => s.Id == t.Id).Body
                                         .FirstOrDefault(sb2 => sb2.Text == b.Text);
                    if (src is not null) english++;   // block text identical to an authored English block
                }
            }
            sb.AppendLine($"   rendered coverage: {blocks - english}/{blocks} title+body strings translated"
                          + (english > 0 ? $" — {english} STILL ENGLISH" : ""));
            foreach (var problem in MarkupProblems(topics)) sb.AppendLine($"  MARKUP   {problem}");
            foreach (var problem in FigureTokenProblems(lang.Code)) sb.AppendLine($"  MARKUP   {problem}");
            foreach (var m in missing) sb.AppendLine($"  MISSING  {Excerpt(m)}");
            foreach (var s in stale)   sb.AppendLine($"  STALE    {Excerpt(s)}");
        }
        return sb.ToString();
    }

    /// <summary>Structural check on the illustrations, independent of language: a Figure block naming an id
    /// that doesn't exist would render as a bare caption, and a figure nothing references would silently
    /// demand translations for labels no user can reach.</summary>
    private static IEnumerable<string> FigureWiringProblems()
    {
        var placed = HelpContent.Topics
                                .SelectMany(t => t.Body)
                                .Where(b => b.Kind == HelpBlockKind.Figure)
                                .Select(b => b.Figure ?? "")
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in placed.Where(id => HelpFigures.ById(id) is null))
            yield return $"topic block names unknown figure '{id}'";
        foreach (var f in HelpFigures.All.Where(f => !placed.Contains(f.Id)))
            yield return $"figure '{f.Id}' is defined but no topic places it";
    }

    /// <summary>Glyph tokens inside translated figure labels — a label is drawn into a fixed canvas, so a
    /// mangled {token} shows as literal braces in the picture with no prose around it to recover from.</summary>
    private static IEnumerable<string> FigureTokenProblems(string code)
    {
        string[] known = ["cross", "circle", "square", "triangle"];
        foreach (var english in HelpFigures.TranslatableText())
        {
            var text = Text(code, english);
            foreach (var m in System.Text.RegularExpressions.Regex.Matches(text, @"\{([A-Za-z]+)\}")
                                                                  .Cast<System.Text.RegularExpressions.Match>())
                if (!known.Contains(m.Groups[1].Value))
                    yield return $"[figure] unknown token '{{{m.Groups[1].Value}}}' — {Excerpt(text)}";
            if (CountOf(english, "{") != CountOf(text, "{"))
                yield return $"[figure] token count changed in translation — {Excerpt(text)}";
        }
    }

    /// <summary>Inline-markup sanity over a built topic set — the failure mode a translation can actually
    /// introduce, since the renderer shows an unterminated marker literally and a mistyped cross-link id
    /// makes a dead link. Checks bold/code markers pair up, `[[…]]` is balanced, every cross-link target is
    /// a real topic id, and every {token} is one the resolver knows.</summary>
    private static IEnumerable<string> MarkupProblems(IReadOnlyList<HelpTopic> topics)
    {
        var ids = HelpContent.Topics.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] knownTokens = ["invoke", "disable", "cross", "circle", "square", "triangle"];
        foreach (var t in topics)
            foreach (var text in t.Body.Select(b => b.Text).Prepend(t.Title))
            {
                if (CountOf(text, "**") % 2 != 0)      yield return $"[{t.Id}] unpaired ** — {Excerpt(text)}";
                if (text.Count(c => c == '`') % 2 != 0) yield return $"[{t.Id}] unpaired ` — {Excerpt(text)}";
                if (CountOf(text, "[[") != CountOf(text, "]]"))
                    yield return $"[{t.Id}] unbalanced [[ ]] — {Excerpt(text)}";
                foreach (var m in System.Text.RegularExpressions.Regex.Matches(text, @"\[\[([^\]|]+)(?:\|[^\]]*)?\]\]")
                                                                      .Cast<System.Text.RegularExpressions.Match>())
                    if (!ids.Contains(m.Groups[1].Value))
                        yield return $"[{t.Id}] cross-link to unknown topic id '{m.Groups[1].Value}'";
                foreach (var m in System.Text.RegularExpressions.Regex.Matches(text, @"\{([A-Za-z]+)\}")
                                                                      .Cast<System.Text.RegularExpressions.Match>())
                    if (!knownTokens.Contains(m.Groups[1].Value))
                        yield return $"[{t.Id}] unknown token '{{{m.Groups[1].Value}}}'";
            }
    }

    private static int CountOf(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    private static string Excerpt(string s)
    {
        // Back the cut off a surrogate pair — a lone half is unencodable, and the report is written with
        // throwing UTF-8, so splitting an emoji kills the whole run rather than mangling one line.
        int cut = 107;
        if (s.Length > 110)
        {
            if (char.IsLowSurrogate(s[cut])) cut--;
            s = s[..cut] + "…";
        }
        return s.Replace("\r", " ").Replace("\n", " ");
    }

    // ── Map regeneration (`Radiata.exe --dump-help-locale <code> [path]`) ─────────

    /// <summary>One language's map as C# source, in <see cref="SourceStrings"/> order: every key the Help tab
    /// needs, carrying the existing translation where its English key still exists and an empty TODO value
    /// where it doesn't. Regenerating the whole file is the maintenance path when many entries are
    /// MISSING/STALE — a stale entry becomes structurally impossible and the entry order matches the Help
    /// tab's reading order (docs/LOCALIZATION.md §2). A repeated English string is emitted once, so the
    /// initializer can never carry a duplicate key.
    /// <para>Walks <see cref="HelpContent.AllAuthored"/>, the same set as <see cref="SourceStrings"/>: every
    /// gated topic and block variant (the Arcade set, the <see cref="ReleaseGates"/> withheld features, the
    /// public-only variants a dev build hides) must keep its entry, or regenerating a map from any one build
    /// silently deletes the translations of whatever that build doesn't show.</para></summary>
    public static string Skeleton(string code)
    {
        var lang = Language(code);
        if (lang.Code == DefaultCode) throw new ArgumentException("English is the source, not a map.", nameof(code));
        var map  = MapFor(lang.Code) ?? new Dictionary<string, string>();
        var cls  = "HelpText" + char.ToUpperInvariant(lang.Code[0]) + lang.Code[1..];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sb   = new System.Text.StringBuilder(256 * 1024);

        sb.AppendLine("namespace ControllerWheel;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>{lang.EnglishName.ToUpperInvariant()} Help-tab strings. Keys are the EXACT English text authored in");
        sb.AppendLine("/// <see cref=\"HelpContent\"/> — copy the C# literal across unchanged (escapes included) when adding an");
        sb.AppendLine("/// entry, and let anything not listed here fall through to English. Run");
        sb.AppendLine("/// <c>Radiata.exe --check-help-locales</c> after editing HelpContent.cs to see what needs work, and");
        sb.AppendLine($"/// <c>Radiata.exe --dump-help-locale {lang.Code}</c> to regenerate this file in source order.");
        sb.AppendLine("/// Conventions (see docs/LOCALIZATION.md): a UI path or label reads in this language, bold, with no English gloss");
        sb.AppendLine("/// (tools/help-flip.pl applies this; the notice sends support-seekers to English instead); markup (<c>**</c>,");
        sb.AppendLine("/// <c>`</c>, <c>[[id|label]]</c>, <c>[label](url)</c>) and <c>{tokens}</c> are");
        sb.AppendLine("/// preserved verbatim, a cross-link's topic id is never translated, and product names stay as they are.");
        sb.AppendLine("/// <para>Entry ORDER follows <c>HelpLocalization.SourceStrings()</c> — the notice, the Help-pane chrome,");
        sb.AppendLine("/// the category names, then each topic's title/keywords/blocks, then the figure labels.</para></summary>");
        sb.AppendLine($"internal static class {cls}");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>");
        sb.AppendLine("    {");

        void Section(string name) { sb.AppendLine(); sb.AppendLine($"        // ── {name} ──"); }
        void Entry(string keyExpr, string english)
        {
            if (!seen.Add(english)) return;
            map.TryGetValue(english, out var t);
            sb.AppendLine($"        [{keyExpr}] =");
            sb.AppendLine(string.IsNullOrWhiteSpace(t) ? "            \"\",   // TODO translate" : $"            {Lit(t)},");
        }

        Section("notice");   Entry("HelpLocalization.NoticeKey", NoticeKey);
        Section("chrome");   foreach (var c in Chrome.All) Entry(Lit(c), c);
        Section("category"); foreach (var c in HelpContent.CategoryOrder) Entry(Lit(c), c);
        foreach (var t in HelpContent.AllAuthored)
        {
            Section("topic:" + t.Id);
            Entry(Lit(t.Title), t.Title);
            Entry(Lit(t.Keywords), t.Keywords);
            foreach (var b in t.Body) Entry(Lit(b.Text), b.Text);
        }
        Section("figure");   foreach (var l in HelpFigures.TranslatableText()) Entry(Lit(l), l);

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>A C# string literal for <paramref name="s"/> — the exact form a map key must take.</summary>
    private static string Lit(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\r").Replace("\n", "\n").Replace("\t", "\t") + "\"";
}
