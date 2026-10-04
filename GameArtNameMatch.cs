using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControllerWheel;

/// <summary>
/// Resolves a game's title to a SteamGridDB game id: query-variant generation (article restoration,
/// bracket/qualifier/year stripping, camelCase splitting) and the exact-match compare that keeps a fuzzy
/// SteamGridDB search from attaching the wrong game's art. Pure title matching — no caching, no downloads;
/// <see cref="GameArt"/> uses the resolved id to fetch and cache the actual images.
/// </summary>
internal static class GameArtNameMatch
{
    internal static async Task<int?> SgdbSearchIdAsync(string name, string key)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        // Try the title verbatim first, then progressively looser variants, stopping at the first match:
        //  • raw — a title already on SGDB ("StarCraft", "Brawlhalla", a real "… Demo") matches as-is.
        //  • camelCase-split — Ubisoft/itch name a game by its concatenated install folder ("RabbidsCoding"
        //    → "Rabbids Coding"); SGDB stores the spaced title.
        //  • qualifier dropped — a release/trial SKU ("… Demo", "… Open Beta", "… Early Access") usually
        //    isn't its own SGDB entry, so fall back to the base game's art. Only after the full-name queries
        //    miss, so a title that genuinely IS a "… Demo"/"… Beta" on SGDB still wins.
        foreach (var q in SearchVariants(name))
            if (await SgdbSearchOnceAsync(q, key).ConfigureAwait(false) is { } id)
                return id;
        return null;
    }

    /// <summary>Ordered, de-duplicated SteamGridDB query variants for a title (see <see cref="SgdbSearchIdAsync"/>).
    /// Full-title forms come first (verbatim + de-camelCased + article-restored), then their base-game forms
    /// with a trailing release/trial qualifier dropped — so a title that genuinely IS a "… Demo"/"… Beta" on
    /// SGDB wins, and only a miss falls back to the base game's art. Every form has trademark symbols
    /// stripped.
    ///
    /// <para><b>Each variant is a separate QUERY whose results still have to match that variant exactly</b>
    /// (<see cref="SgdbSearchOnceAsync"/>). That's what keeps adding forms safe: a new variant can only ever
    /// turn "no art" into "art", never "right art" into "wrong art". Never loosen the compare itself —
    /// that's what produces wrong-game art (the Beleth → Beethoven class of mismatch).</para></summary>
    private static IEnumerable<string> SearchVariants(string name)
    {
        var spaced   = SpaceCamelCase(name);
        var article  = RestoreLeadingArticle(name);
        var noBrack  = StripBracketed(name);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // The trailing-year form is LAST: a game renamed with a year suffix ("Hunt: Showdown 1896") is
        // still catalogued under its original title on SGDB, but a year can also be integral to the name
        // ("Anno 1800"), so it only fires after every full-title form has missed.
        foreach (var v in new[] { name, spaced, article, noBrack, RestoreLeadingArticle(noBrack),
                                  StripTrailingQualifier(name), StripTrailingQualifier(spaced),
                                  StripTrailingQualifier(article),
                                  StripTrailingYear(name), StripTrailingYear(spaced) })
        {
            var q = CleanSymbols(v);
            if (!string.IsNullOrWhiteSpace(q) && seen.Add(q))
                yield return q;
        }
    }

    /// <summary>Move a trailing article back to the front: <c>"Witcher 3: Wild Hunt, The"</c> →
    /// <c>"The Witcher 3: Wild Hunt"</c>, and the mid-title form <c>"Legend of Zelda, The: Breath of the
    /// Wild"</c> → <c>"The Legend of Zelda: Breath of the Wild"</c>. Unchanged when there's no such article.
    ///
    /// <para>GOG genuinely catalogues titles this way and SteamGridDB does not, so without this the
    /// exact-match compare rejects every candidate and those games get NO art at all. Both shapes are
    /// Playnite's (<c>GameNameMatcher.ToGameKey</c>, MIT); "A"/"An" follow the same convention.</para></summary>
    private static string RestoreLeadingArticle(string s)
    {
        var t = s.TrimEnd();
        // "<title>, The"  → "The <title>"
        var tail = Regex.Match(t, @",\s*(the|a|an)\s*$", RegexOptions.IgnoreCase);
        if (tail.Success)
            return $"{tail.Groups[1].Value} {t[..tail.Index]}".Trim();
        // "<title>, The: <subtitle>" → "The <title>: <subtitle>"
        var mid = Regex.Match(t, @",\s*(the|a|an):\s*", RegexOptions.IgnoreCase);
        if (mid.Success)
            return $"{mid.Groups[1].Value} {t[..mid.Index]}: {t[(mid.Index + mid.Length)..]}".Trim();
        return s;
    }

    /// <summary>Drop bracketed metadata: <c>"The Witcher 3 [GOTY]"</c> → <c>"The Witcher 3"</c>. Returns the
    /// input UNCHANGED if stripping would empty it — a game legitimately titled "(Description)" must keep a
    /// searchable name (Playnite's <c>RemoveUnlessThatEmptiesTheString</c> guard, MIT).
    /// <para>Distinct from <see cref="TrailingQualifierRe"/>, which only knows a fixed vocabulary of
    /// edition/trial words and only at the END; this removes any bracketed run wherever it sits.</para></summary>
    private static string StripBracketed(string s)
    {
        var outp = Regex.Replace(s, @"\[[^\]]*\]|\([^)]*\)", " ");
        outp = Regex.Replace(outp, @"\s{2,}", " ").Trim();
        return string.IsNullOrWhiteSpace(outp) ? s : outp;
    }

    /// <summary>Drop a trailing standalone year ("Hunt: Showdown 1896" → "Hunt: Showdown"). Fallback-only
    /// (see <see cref="SearchVariants"/>) — never applied when the full title matched something.</summary>
    private static string StripTrailingYear(string s) =>
        Regex.Replace(s, @"[\s_:\-]+(1[89]\d{2}|20\d{2})\s*$", "").Trim();

    // Trailing qualifiers SteamGridDB doesn't catalogue as their own entry — dropped to fall back to the
    // base game's art. Two groups: release/trial markers (Demo, Open Beta, Early Access, …) and edition
    // SKUs (Deluxe/GOTY/… Edition, Director's Cut). Longer phrases first so "Open Beta" beats "Beta" and
    // "<type> Edition" beats a bare type. Fallback-only (the full title is tried first), so this can only
    // ADD art. NOT included: "Remastered"/"Remake" (usually distinct SGDB entries with their own art, and
    // the bare name can collide with a different game). Separator/bracket required before the token,
    // optional bracket/space after, so "Game (Beta)" / "Game: Deluxe Edition" match but a word merely
    // ending in these letters never does.
    // A BARE trailing "Enhanced" is included as well ("Quake II Enhanced" → "Quake II"): storefronts ship
    // that form while SGDB catalogues only the base title, so without it the game gets no logo at all.
    private static readonly Regex TrailingQualifierRe = new(
        @"[\s_:\-\[(]+(?:" +
        @"open\s+beta|closed\s+beta|public\s+test|early\s+access|free\s+trial|playtest|preview|demo|beta|alpha|trial|ptr|enhanced" +
        @"|(?:deluxe|gold|ultimate|standard|complete|definitive|premium|special|anniversary|enhanced|legacy|collector(?:['’])?s|game\s+of\s+the\s+year|goty)\s+edition" +
        @"|game\s+of\s+the\s+year|goty|director(?:['’])?s\s+cut" +
        @")[\s\])]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Drop one or more trailing release/trial qualifiers ("Rabbids Coding Demo" → "Rabbids Coding",
    /// "Game Open Beta" → "Game"). Loops so stacked qualifiers ("… Beta Demo") all come off.</summary>
    private static string StripTrailingQualifier(string s)
    {
        string prev;
        do { prev = s; s = TrailingQualifierRe.Replace(s, "").Trim(); } while (s != prev && s.Length > 0);
        return s;
    }

    /// <summary>Remove trademark/copyright symbols that silently break a search (e.g. "Rainbow Six® Siege").</summary>
    private static string CleanSymbols(string s) =>
        Regex.Replace(s, @"[™®©]", "").Replace("  ", " ").Trim();

    private static async Task<int?> SgdbSearchOnceAsync(string name, string key)
    {
        using var doc = await GameArt.SgdbJsonAsync(
            $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(name)}", key)
            .ConfigureAwait(false);
        if (doc is null || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;
        // SteamGridDB's autocomplete is FUZZY (searching "Beleth" returns "Beethoven" as the top hit), so
        // never take data[0] blindly — that hands a game the WRONG game's art. Accept only a result whose
        // name matches the query (letters/digits only, case-insensitive); an unrelated fuzzy hit yields NO
        // art here — a clean default glyph, which SearchVariants' looser forms may still fill, and which is
        // far better than a confidently-wrong cover.
        string want = NormalizeTitle(name);
        if (want.Length == 0) return null;
        foreach (var el in data.EnumerateArray())
        {
            if (!el.TryGetProperty("id", out var id) || !id.TryGetInt32(out int v)) continue;
            var got = el.TryGetProperty("name", out var nm) ? NormalizeTitle(nm.GetString()) : "";
            if (got == want) return v;
        }
        // Second pass: SGDB often catalogues sequels under ROMAN numerals ("Helldivers II",
        // "Red Dead Redemption II") where storefronts use arabic — an exact compare rejects every hit
        // and the game gets no art at all. Compare again with roman numerals folded to digits on BOTH
        // sides; exact matches always win above, so this can only rescue an otherwise-artless game.
        string wantNum = NormalizeTitle(FoldRomanNumerals(name));
        foreach (var el in data.EnumerateArray())
        {
            if (!el.TryGetProperty("id", out var id) || !id.TryGetInt32(out int v)) continue;
            var got = el.TryGetProperty("name", out var nm) ? NormalizeTitle(FoldRomanNumerals(nm.GetString() ?? "")) : "";
            if (got == wantNum) return v;
        }
        return null;
    }

    /// <summary>Replace standalone roman-numeral words (II–X, plus a trailing V — "GTA V") with digits,
    /// for numeral-insensitive title comparison. "I" and non-trailing "V"/"X" stay: they're far more
    /// often real words or letters ("Mega Man X" mid-title) than sequel numbers.</summary>
    private static string FoldRomanNumerals(string s)
    {
        s = Regex.Replace(s, @"\b(ii|iii|iv|vi|vii|viii|ix)\b",
            m => m.Value.ToLowerInvariant() switch
            {
                "ii" => "2", "iii" => "3", "iv" => "4", "vi" => "6",
                "vii" => "7", "viii" => "8", "ix" => "9", _ => m.Value,
            }, RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\b(v|x)\s*$", m => m.Value.Trim().ToLowerInvariant() == "v" ? "5" : "10",
            RegexOptions.IgnoreCase);
        return s;
    }

    /// <summary>Reduce a title to letters+digits, lower-case, for a punctuation/spacing-insensitive name
    /// compare against SteamGridDB search results (rejects fuzzy hits like "Beleth" → "Beethoven").
    /// Decomposes to NFD and drops combining marks first, so an accented Latin letter ("Pokémon") still
    /// matches its unaccented form ("Pokemon") instead of being stripped to nothing; any other letter or
    /// digit (CJK, Cyrillic, …) survives as itself.</summary>
    private static string NormalizeTitle(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>Insert spaces at camelCase and letter→digit boundaries ("RabbidsCoding" → "Rabbids Coding",
    /// "Anno1800" → "Anno 1800"). Used only as a SteamGridDB search fallback for concatenated folder names.</summary>
    private static string SpaceCamelCase(string s) =>
        Regex.Replace(Regex.Replace(s, "(?<=[a-z0-9])(?=[A-Z])", " "), "(?<=[A-Za-z])(?=[0-9])", " ");
}
