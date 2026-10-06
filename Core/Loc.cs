using System.Globalization;

namespace ControllerWheel;

/// <summary>App-wide UI strings in the picked language. Same model as <see cref="HelpLocalization"/>: the
/// English text is the key, a missing translation renders in English, and structure never comes from a
/// translation. Two things differ from Help:
/// <list type="bullet">
/// <item><b>The language is fixed for the run.</b> <see cref="Init"/> runs once at startup, before any
/// long-lived thread exists; changing the setting prompts a restart (onboarding is the exception and
/// rebuilds its own window). There is no change event and no mutable state after Init, so <see cref="T"/>
/// is safe from any thread — the maps are read-only dictionaries.</item>
/// <item><b>Culture is set for the UI only.</b> <c>CurrentUICulture</c> follows the language (default and
/// current thread, so pool threads and the HID reader inherit it); <c>CurrentCulture</c> is left alone —
/// config, paths and hex parse with <see cref="CultureInfo.InvariantCulture"/> and stay that way.</item>
/// </list>
/// <para>Plurals: <see cref="P"/> keys on <c>plural:one|other</c> (see <see cref="PluralKey"/>); a language supplies as many
/// '|'-separated forms as its rules need (ja 1, en/es/de 2, ar 6) and <see cref="PluralIndex"/> picks one.
/// Hand-rolled on purpose: .NET exposes no plural-category API and <c>Core</c> takes no packages.</para>
/// <para>Numbers formatted here use Western digits regardless of language (<see cref="Count"/>).</para>
/// See docs/LOCALIZATION.md.</summary>
public static class Loc
{
    public static string Lang { get; private set; } = HelpLocalization.DefaultCode;
    public static bool   IsRtl => HelpLocalization.Language(Lang).Rtl;
    private static bool _initialised;

    /// <summary>The run's UI culture: what user-visible text is sorted and compared with. Never the thread's
    /// CurrentCulture, which stays invariant so config, paths and hex parse the same everywhere.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

    /// <summary>Set the run's language. Once, first thing after config is read — every thread created
    /// afterwards inherits the UI culture; threads created before it would not.</summary>
    public static void Init(string? code)
    {
        if (_initialised) throw new InvalidOperationException("Loc.Init runs exactly once, at startup.");
        _initialised = true;
        Lang = code is null ? Detect() : HelpLocalization.Normalize(code);
        var ui = CultureFor(Lang);
        Culture = ui;
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        Thread.CurrentThread.CurrentUICulture     = ui;
    }

    private static CultureInfo CultureFor(string code)
    {
        try { return CultureInfo.GetCultureInfo(code); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }   // a code the OS lacks must not kill startup
    }

    /// <summary>No language chosen (config <c>system.language</c> null): Windows' display language when it is
    /// one this build offers, else English. Read before <see cref="Init"/> changes the thread culture.</summary>
    private static string Detect()
    {
        string os = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Offered(os) ? os : HelpLocalization.DefaultCode;
    }

    /// <summary>Whether a language may be picked or auto-detected: English always; another language only
    /// once its UI catalog is complete, so a half-translated UI is never offered. The Help catalog is not
    /// consulted — Help alone was translated long before the UI, and that state must not surface a language.</summary>
    public static bool Offered(string code) =>
        code == HelpLocalization.DefaultCode
        || (HelpLocalization.Languages.Any(l => l.Code == code) && MapFor(code) is { } map
            && SourceStrings().All(s => map.TryGetValue(s, out var v) && !string.IsNullOrWhiteSpace(v)));

    /// <summary>The per-language English→translation map, or null for English (and any language whose UI
    /// map hasn't landed — the language then renders in English while its Help is translated).</summary>
    private static IReadOnlyDictionary<string, string>? MapFor(string code) => code switch
    {
        "es" => UiTextEs.Map,
        "de" => UiTextDe.Map,
        "ja" => UiTextJa.Map,
        "ar" => UiTextAr.Map,
#if DEBUG
        "qps" or "qps-rtl" => Pseudo.Value,
#endif
        _ => null,
    };

    /// <summary>The wheel, hub and Arcade draw in display faces (Jua, Sour Gummy, Bahnschrift, Share Tech) that
    /// carry Latin only. A language outside Latin script draws in its own system chain instead
    /// (<see cref="HelpLanguage.FontFamily"/>) — see docs/MATERIALS.md.</summary>
    public static bool UsesSystemFace => Lang is "ja" or "ar";

#if DEBUG
    /// <summary>Dev-only pseudo-localization (<c>qps</c> / <c>qps-rtl</c>): every catalog string comes back
    /// bracketed, accented and ~30% longer, so a hard-coded string stands out as the only plain English on
    /// screen and a layout that cannot take German's length or Arabic's direction shows before a translation
    /// exists. Placeholders, markup markers, cross-link ids, URLs and chip tokens pass through untouched.
    /// Compiled out of Release: a shipped config can never name it (Normalize does not know the codes).</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Pseudo = new(() =>
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in SourceStrings())
            map[s] = s.StartsWith(PluralPrefix, StringComparison.Ordinal)
                ? string.Join("|", s[PluralPrefix.Length..].Split('|').Select(Pseudoize))   // one pseudo form per English form
                : Pseudoize(s);
        return map;
    });

    public static string Pseudoize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length * 2);
        int letters = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // Pass a {placeholder}/{pad:X}/{token} through whole.
            if (c == '{') { int e = s.IndexOf('}', i); if (e > i) { sb.Append(s, i, e - i + 1); i = e; continue; } }
            // [[topic-id|Label]]: keep the id, pseudoize the label.
            if (c == '[' && i + 1 < s.Length && s[i + 1] == '[')
            {
                int bar = s.IndexOf('|', i), end = s.IndexOf("]]", i, StringComparison.Ordinal);
                int stop = bar >= 0 && (end < 0 || bar < end) ? bar : end >= 0 ? end : -1;
                if (stop > i) { sb.Append(s, i, stop - i + (stop == bar ? 1 : 0)); i = stop == bar ? bar : stop - 1; continue; }
            }
            // [label](url): keep the url.
            if (c == '(' && i > 0 && s[i - 1] == ']') { int e = s.IndexOf(')', i); if (e > i) { sb.Append(s, i, e - i + 1); i = e; continue; } }
            if (c == '\\' && i + 1 < s.Length && s[i + 1] == 'n') { sb.Append("\\n"); i++; continue; }
            sb.Append(Accent(c));
            if (char.IsLetter(c)) letters++;
        }
        // Length stress: ~30% more, as trailing filler the eye reads as padding rather than text.
        int pad = Math.Max(letters >= 3 ? 1 : 0, letters * 3 / 10);
        return "[" + sb + new string('~', pad) + "]";
    }

    private static char Accent(char c) => c switch
    {
        'a' => 'á', 'e' => 'é', 'i' => 'í', 'o' => 'ó', 'u' => 'ú', 'y' => 'ý', 'c' => 'ç', 'n' => 'ñ', 's' => 'š', 'z' => 'ž',
        'A' => 'Å', 'E' => 'É', 'I' => 'Í', 'O' => 'Ø', 'U' => 'Ü', 'C' => 'Ç', 'N' => 'Ñ', 'S' => 'Š', 'Z' => 'Ž',
        _ => c,
    };
#endif

    /// <summary>Translate one authored UI string; untranslated text passes through in English. The whitespace
    /// guard is the fallback promise: a blank value degrades to English, never to nothing.</summary>
    public static string T(string english) =>
        MapFor(Lang) is { } map && map.TryGetValue(english, out var t) && !string.IsNullOrWhiteSpace(t) ? t : english;

    /// <summary>Translate into a named language rather than the run's: the language-change prompt shows its question in
    /// the language being chosen as well as the current one. Same fallback promise as <see cref="T"/>.</summary>
    public static string In(string code, string english) =>
        MapFor(HelpLocalization.Normalize(code)) is { } map && map.TryGetValue(english, out var t) && !string.IsNullOrWhiteSpace(t) ? t : english;

    /// <summary>The tip-jar page in the run's language: the site keeps flat siblings (/tip/es.html) beside the English index.</summary>
    public static string TipUrl =>
        Lang == HelpLocalization.DefaultCode ? "https://getradiata.app/tip/" : $"https://getradiata.app/tip/{Lang}.html";

    /// <summary>Composite-format a translated string. Placeholders are positional so a translation may
    /// reorder them. ⚠ A malformed translated format string must not throw — narration formats on a pool
    /// thread where an exception ends the process — so a bad one falls back to the English format.</summary>
    public static string F(string english, params object[] args)
    {
        try { return string.Format(CultureInfo.InvariantCulture, T(english), args); }
        catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, english, args); }
    }

    /// <summary>A counted sentence. The key is the English singular and plural joined with '|'; the value is
    /// one form per plural category of the language, '|'-separated, in <see cref="PluralIndex"/> order. A
    /// short row degrades to its last form rather than throwing.</summary>
    public static string P(string one, string other, long n, params object[] extra)
    {
        string key = PluralKey(one, other);
        string[]? forms = MapFor(Lang) is { } map && map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.Split('|') : null;
        string form = forms is { Length: > 0 }
            ? forms[Math.Min(PluralIndex(Lang, n), forms.Length - 1)]
            : (n == 1 ? one : other);
        if (extra.Length == 0) return form.Replace("{0}", Count(n));
        // {0} is the count; {1}… are the extras — a translation may reorder them. Same fail-soft as F.
        var args = new object[extra.Length + 1];
        args[0] = Count(n);
        extra.CopyTo(args, 1);
        try { return string.Format(CultureInfo.InvariantCulture, form, args); }
        catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, n == 1 ? one : other, args); }
    }

    /// <summary>A hub-readout line as displayed. <c>ActionStatus</c> lines mix three things — an authored
    /// default label, an executor state word, and free text (a game name, the frontmost app an Exit slice
    /// would close) — so this translates only the first two and passes free text through. ⚠ Never route a
    /// readout line through <see cref="T"/>: an app called <i>Settings</i> would come back translated.</summary>
    public static string Readout(string? line) =>
        line is not null && (Defaults.Contains(line) || StatusWords.Contains(line)) ? T(line) : line ?? "";

    private static HashSet<string>? _statusWords;
    private static HashSet<string> StatusWords => _statusWords ??= ConstsOf(typeof(UiText.Status));

    private static HashSet<string> ConstsOf(Type t)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            if (f.IsLiteral && f.GetRawConstantValue() is string s) set.Add(s);
        return set;
    }

    /// <summary>The plural-form index for <paramref name="n"/> in <paramref name="lang"/> — CLDR categories,
    /// integers only (every count in this app is a whole number). Arabic: zero is n == 0 only, and few/many
    /// are on n % 100, so 103 is few and 111 is many.</summary>
    public static int PluralIndex(string lang, long n) => lang switch
    {
        "ja" => 0,
        "ar" => n == 0 ? 0 : n == 1 ? 1 : n == 2 ? 2
              : n % 100 is >= 3 and <= 10 ? 3
              : n % 100 is >= 11 and <= 99 ? 4
              : 5,
        _    => n == 1 ? 0 : 1,          // en, es, de: one / other
    };

    /// <summary>How many forms a language's plural value must supply — what makes a missing form a checkable
    /// error rather than a silent clamp.</summary>
    public static int PluralFormCount(string lang) => lang switch { "ja" => 1, "ar" => 6, _ => 2 };

    /// <summary>The map key of a plural entry: a marker no prose starts with, then the English pair. Prose may
    /// legitimately contain '|' (a keyboard hint does), so the marker — not the bar — is what identifies a
    /// plural row to the checker.</summary>
    internal const string PluralPrefix = "plural:";
    internal static string PluralKey(string one, string other) => PluralPrefix + one + "|" + other;

    /// <summary>Western digits in every language — counts of slices and games, not prose.</summary>
    private static string Count(long n) => n.ToString(CultureInfo.InvariantCulture);

    // ── Default slice labels: translate on display ─────────────────────────────

    private static HashSet<string>? _defaultLabels;

    /// <summary>The shell registers the labels it authors and writes into config as defaults (the action
    /// taxonomy's display names, which an in-wheel Add copies into the new slice). Once, at startup, before
    /// any label is displayed; <c>Core</c>'s own defaults (<see cref="UiText.DefaultLabels"/> and
    /// <see cref="AppConfig.Default"/>) are always in the set.</summary>
    public static void RegisterDefaultLabels(IEnumerable<string> labels)
    {
        var set = Defaults;
        foreach (var l in labels) if (!string.IsNullOrWhiteSpace(l)) set.Add(l);
    }

    /// <summary>A slice label as it should be displayed: a label still matching an authored default
    /// translates; anything the user typed (and any game or app name) passes through untouched. Config always
    /// holds the English default, so switching language is lossless. ⚠ The set is deliberately narrow — only
    /// authored defaults, never the whole catalog — because a game called <i>Control</i> or <i>Journey</i>
    /// would otherwise be silently replaced by an unrelated UI string.</summary>
    public static string DefaultLabel(string? label) =>
        label is not null && Defaults.Contains(label) ? T(label) : label ?? "";

    public static bool IsDefaultLabel(string? label) => label is not null && Defaults.Contains(label);

    private static HashSet<string> Defaults => _defaultLabels ??= BuildDefaults();

    private static HashSet<string> BuildDefaults() => ConstsOf(typeof(UiText.DefaultLabels));

    // ── The catalog ──────────────────────────────────────────────────────────────

    /// <summary>Every UI string the app can ask for: the XAML strings the extractor found
    /// (<see cref="UiStrings.All"/>, generated) and every <c>public const string</c> on <see cref="UiText"/>
    /// and its nested classes (reflected — no parser, cannot drift). Strings reached through a variable are
    /// enumerated by their owners and passed to <see cref="CheckReport"/> by the caller.
    /// <para>A const marked <see cref="DevOnlyStringAttribute"/> is left out: it never reaches a shipping
    /// build, so requiring a translation for it would withdraw every language from <see cref="Offered"/>.</para></summary>
    public static IEnumerable<string> SourceStrings()
    {
        foreach (var s in UiStrings.All) yield return s;
        foreach (var t in Walk(typeof(UiText)))
        {
            var consts = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                          .Where(f => f.IsLiteral && f.FieldType == typeof(string)
                                      && !f.IsDefined(typeof(DevOnlyStringAttribute), false))
                          .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);
            foreach (var (name, value) in consts)
            {
                // A counted sentence is authored as an XOne / XOther pair and looked up by Loc.P on the joined
                // plural key — so the catalog carries the plural key, never the two halves on their own.
                if (name.EndsWith("One", StringComparison.Ordinal) && consts.TryGetValue(name[..^3] + "Other", out var other))
                    yield return PluralKey(value, other);
                else if (name.EndsWith("Other", StringComparison.Ordinal) && consts.ContainsKey(name[..^5] + "One"))
                    continue;
                else
                    yield return value;
            }
        }

        static IEnumerable<Type> Walk(Type t)
        {
            yield return t;
            foreach (var n in t.GetNestedTypes()) foreach (var d in Walk(n)) yield return d;
        }
    }

    /// <summary>A UI map skeleton for one language, in catalog order — the file <c>Core/UiText&lt;Xx&gt;.cs</c>
    /// is regenerated from this, never patched by hand (docs/LOCALIZATION.md §2). Existing translations whose
    /// English key still exists are carried; everything else is <c>TODO</c>. Plural rows carry one
    /// <c>|</c>-separated form per <see cref="PluralFormCount"/>. <paramref name="appSide"/> is the shell's
    /// registered set, as passed to <see cref="CheckReport"/>.</summary>
    public static string Skeleton(string code, IEnumerable<string> appSide)
    {
        var lang = HelpLocalization.Language(code);
        var existing = MapFor(code) ?? new Dictionary<string, string>();
        var sb = new System.Text.StringBuilder();
        string cls = "UiText" + char.ToUpperInvariant(code[0]) + code[1..].Replace("-", "");
        sb.AppendLine("namespace ControllerWheel;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>{lang.EnglishName} UI strings, keyed by the English source text (docs/LOCALIZATION.md).");
        sb.AppendLine($"/// Regenerate with <c>Radiata.exe --dump-ui-locale {code}</c>; entry order = <c>Loc.SourceStrings()</c> + the");
        sb.AppendLine("/// shell-registered set. A plural row (<c>plural:one|other</c>) carries its language's form count, '|'-separated.</summary>");
        sb.AppendLine($"internal static class {cls}");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        int forms = PluralFormCount(code);
        foreach (var key in SourceStrings().Concat(appSide).Distinct())
        {
            string value = existing.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v
                         : key.StartsWith(PluralPrefix, StringComparison.Ordinal) ? string.Join("|", Enumerable.Repeat("TODO", forms))
                         : "TODO";
            sb.AppendLine($"        [{Lit(key)}] = {Lit(value)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Lit(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    /// <summary>Per-language parity report for the UI maps, shaped like <see cref="HelpLocalization.CheckReport"/>:
    /// MISSING (no translation), STALE (a translation whose English key is gone), blank values, and plural
    /// rows (marked by <see cref="PluralPrefix"/>) with the wrong number of forms or a lost {0}.
    /// <paramref name="appSide"/> carries the strings only the shell can enumerate.</summary>
    public static string CheckReport(IEnumerable<string> appSide)
    {
        var source = SourceStrings().Concat(appSide).Distinct().ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"UI localization check — {source.Count} translatable strings (UiStrings.g.cs + UiText + shell-registered sets)");
        foreach (var lang in HelpLocalization.Languages.Where(l => l.Code != HelpLocalization.DefaultCode && !l.Code.StartsWith("qps", StringComparison.Ordinal)))   // pseudo-locales are not translation targets
        {
            var map = MapFor(lang.Code) ?? new Dictionary<string, string>();
            var missing = source.Where(s => !map.TryGetValue(s, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
            var stale   = map.Keys.Where(k => !source.Contains(k)).ToList();
            sb.AppendLine();
            sb.AppendLine($"── {lang.EnglishName} ({lang.Code}): {source.Count - missing.Count}/{source.Count} translated, "
                          + $"{missing.Count} missing, {stale.Count} stale");
            foreach (var (k, v) in map)
            {
                bool plural = k.StartsWith(PluralPrefix, StringComparison.Ordinal);
                if (plural)
                {
                    int forms = v.Split('|').Length, want = PluralFormCount(lang.Code);
                    if (forms != want) sb.AppendLine($"  PLURAL   {forms} of {want} forms — {Excerpt(k)}");
                    if (!v.Split('|').All(f => f.Contains("{0}"))) sb.AppendLine($"  PLURAL   a form lost {{0}} — {Excerpt(k)}");
                }
            }
            foreach (var m in missing) sb.AppendLine($"  MISSING  {Excerpt(m)}");
            foreach (var s in stale)   sb.AppendLine($"  STALE    {Excerpt(s)}");
        }
        return sb.ToString();
    }

    private static string Excerpt(string s) => s.Length <= 100 ? s : s[..100] + "…";
}
