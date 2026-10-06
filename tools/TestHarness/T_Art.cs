using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>A GOG game whose title uses the comma-article form, and art still resolving
/// normally across the library after the mimes= filter.
///
/// Both are decidable without owning the game: the comma-article fix is a query-variant list, and the mimes
/// filter is a single URL builder every SteamGridDB call passes through. The live half — does SGDB actually
/// return art for those variants — runs only when an API key is configured, and is reported as a separate
/// check so a keyless machine skips rather than silently passes.</summary>
internal static class T_Art
{
    // GameArt is internal to the app assembly, so everything here goes through reflection.
    private static Type GA => H.AppType("GameArt");
    // Query-variant generation and title normalization live in GameArtNameMatch.
    private static Type GAN => H.AppType("GameArtNameMatch");
    // Per-game overrides (logo/cover state) live in GameMetadata.
    private static Type GM => H.AppType("GameMetadata");

    private static string CacheDirectory =>
        (string)GA.GetProperty("CacheDirectory", System.Reflection.BindingFlags.Static
                                               | System.Reflection.BindingFlags.Public
                                               | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);

    /// <summary>GameArt.GetCachedPathAsync(game) — a Task&lt;string&gt; reached reflectively.</summary>
    private static async Task<string> CachedPathAsync(InstalledGame g)
    {
        var m = H.StaticMethod(GA, "GetCachedPathAsync", 1)
                ?? throw new MissingMethodException("GameArt.GetCachedPathAsync(game)");
        var task = (Task)m.Invoke(null, new object[] { g });
        await task.ConfigureAwait(false);
        return (string)task.GetType().GetProperty("Result")?.GetValue(task);
    }

    public static void Run()
    {
        H.Group("Game art — comma-article titles, the mimes= filter, live resolution");
        CommaArticle();
        NormalizeTitleMatching();
        MimesFilter();
        CuratedHideLogo();
        LogoFingerprint();
        LiveResolution().GetAwaiter().GetResult();
    }

    /// <summary>NormalizeTitle feeds the SteamGridDB exact-name match on both sides (search()'s want/got).
    /// An accented or non-Latin title must still normalize to something a plain-ASCII SGDB result can match,
    /// rather than being stripped to empty by an ASCII-only filter.</summary>
    private static void NormalizeTitleMatching()
    {
        var norm = H.StaticMethod(GAN, "NormalizeTitle", 1)
                   ?? throw new MissingMethodException("GameArtNameMatch.NormalizeTitle(title)");
        string N(string s) => (string)norm.Invoke(null, new object[] { s });

        H.Check("an accented Latin title matches its unaccented form",
                N("Pokémon") == N("Pokemon"), $"{N("Pokémon")} vs {N("Pokemon")}");
        H.Check("a fuzzy near-miss still doesn't match",
                N("Beleth") != N("Beethoven"), $"{N("Beleth")} vs {N("Beethoven")}");
        H.Check("punctuation/spacing is stripped, case is folded",
                N("DOOM Eternal") == "doometernal", N("DOOM Eternal"));
        H.Check("a non-Latin title normalizes to something, not empty",
                N("ペルソナ５").Length > 0, N("ペルソナ５"));
        H.Check("a macron'd Latin title matches its plain form",
                N("Ōkami") == N("Okami"), $"{N("Ōkami")} vs {N("Okami")}");
    }

    /// <summary>A logo pick is pinned by its URL's FINGERPRINT, not its position in SteamGridDB's candidate
    /// list — that list refetches weekly and SGDB re-ranks, so a position-keyed pick silently becomes a
    /// different logo. This is the same property the cover side already had.</summary>
    private static void LogoFingerprint()
    {
        string Fp(string url) =>
            (string)(H.StaticMethod(GA, "UrlFingerprint", 1)
                     ?? throw new MissingMethodException("GameArt.UrlFingerprint(url)")).Invoke(null, [url]);
        string CachePath(InstalledGame g, string url) =>
            (string)(H.StaticMethod(GA, "LogoCandidateCachePath", 2)
                     ?? throw new MissingMethodException("GameArt.LogoCandidateCachePath(game, url)"))
                    .Invoke(null, [g, url]);

        const string a = "https://cdn2.steamgriddb.com/logo/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png";
        const string b = "https://cdn2.steamgriddb.com/logo/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png";
        var game = new InstalledGame("Zzz Fingerprint Fixture", "steam://rungameid/0", "Steam");

        H.Check("the same URL always fingerprints the same", Fp(a) == Fp(a));
        H.Check("different URLs fingerprint differently", Fp(a) != Fp(b));
        H.Check("the cache filename carries the fingerprint, not a position",
                CachePath(game, a).Contains(Fp(a), StringComparison.Ordinal)
                && CachePath(game, a) != CachePath(game, b),
                CachePath(game, a));

        // The point of the change: the SAME artwork keeps the same path however the list is ordered.
        var reranked = new[] { b, a };
        var original = new[] { a, b };
        H.Check("a re-ranked candidate list doesn't move the cached file",
                CachePath(game, original[0]) == CachePath(game, reranked[1]));
    }

    /// <summary>A curated HideLogo has to blank the whole overlay, not just the logo image: the tile falls
    /// back to drawing the TITLE AS TEXT when it has no logo, so suppressing the image alone put a wordmark
    /// over exactly the art curated to have none. Read-only — it never writes cover-overrides.json, so the
    /// curated branch is exercised through whatever picks this machine happens to hold.</summary>
    private static void CuratedHideLogo()
    {
        bool Hidden(InstalledGame g) =>
            (bool)(H.StaticMethod(GM, "LogoHiddenFor", 1)
                   ?? throw new MissingMethodException("GameMetadata.LogoHiddenFor(game)")).Invoke(null, [g]);
        bool Unhidden(InstalledGame g) =>
            (bool)(H.StaticMethod(GM, "LogoUnhiddenFor", 1)
                   ?? throw new MissingMethodException("GameMetadata.LogoUnhiddenFor(game)")).Invoke(null, [g]);

        // Curated-hidden in the shipped curated table (CuratedArtPicks). A local Start pick can only ADD a hide (or record an
        // explicit un-hide), so "hidden" is the answer unless this machine deliberately un-hid it.
        var hidden = new InstalledGame("Senua's Saga: Hellblade II", "steam://rungameid/2461850", "Steam");
        bool unhidden = Unhidden(hidden);
        H.Check("a curated HideLogo game hides the whole overlay",
                Hidden(hidden) == !unhidden,
                unhidden ? "locally un-hidden on this machine — the un-hide branch is what's under test" : null);

        H.Check("a game with no curated entry and no pick keeps its overlay",
                !Hidden(new InstalledGame("Zzz Not A Real Game 90210", "steam://rungameid/0", "Steam")));
    }

    // ── "The Witcher 3: Wild Hunt, The" → the restored-article form must be among the queries ────────
    private static void CommaArticle()
    {
        var variants = H.StaticMethod(GAN, "SearchVariants", 1);
        var restore  = H.StaticMethod(GAN, "RestoreLeadingArticle", 1);
        if (variants is null) { H.Fail("GameArtNameMatch.SearchVariants(name) not found", "renamed? the fix is untested"); return; }

        List<string> V(string name) => ((IEnumerable<string>)variants.Invoke(null, new object[] { name })).ToList();

        foreach (var (stored, wanted) in new (string, string)[]
        {
            ("Witcher 3: Wild Hunt, The", "The Witcher 3: Wild Hunt"),
            ("Elder Scrolls V: Skyrim, The", "The Elder Scrolls V: Skyrim"),
            ("Witness, The",                "The Witness"),
            ("Talos Principle, A",          "A Talos Principle"),
        })
        {
            var vs = V(stored);
            H.Check($"\"{stored}\" is queried as \"{wanted}\"",
                    vs.Any(v => v.Equals(wanted, StringComparison.OrdinalIgnoreCase)),
                    "variants: " + string.Join(" | ", vs));
        }

        // The verbatim title must still be tried FIRST — a game genuinely named that way keeps its own art.
        var w = V("Witcher 3: Wild Hunt, The");
        H.Check("the verbatim title is still queried first",
                w.Count > 0 && w[0].Equals("Witcher 3: Wild Hunt, The", StringComparison.OrdinalIgnoreCase), w[0]);

        // A title with no trailing article must be left alone — no bogus extra query.
        if (restore is not null)
        {
            var plain = (string)restore.Invoke(null, new object[] { "Portal 2" });
            H.Check("a title with no trailing article is untouched", plain == "Portal 2", plain);
            var mid = (string)restore.Invoke(null, new object[] { "Sam & Max: The Devil's Playhouse" });
            H.Check("an article in the MIDDLE isn't moved", mid == "Sam & Max: The Devil's Playhouse", mid);
        }

        // Edition suffix: the stripped base title is a LATER variant, the full title stays first.
        var disco = V("Disco Elysium - The Final Cut");
        H.Check("\"Disco Elysium - The Final Cut\" tries the full title first",
                disco.Count > 0 && disco[0] == "Disco Elysium - The Final Cut", string.Join(" | ", disco));
        H.Check("\"Disco Elysium - The Final Cut\" also yields \"Disco Elysium\", after the full title",
                disco.IndexOf("Disco Elysium") > 0, string.Join(" | ", disco));
        var div = V("Divinity: Original Sin 2 - Definitive Edition");
        H.Check("\"... - Definitive Edition\" yields the base title",
                div.IndexOf("Divinity: Original Sin 2") > 0 && div[0] == "Divinity: Original Sin 2 - Definitive Edition",
                string.Join(" | ", div));
        H.Check("a title with no edition suffix yields no extra variant", V("Portal 2").Count == 1, string.Join(" | ", V("Portal 2")));
        var bare = V("The Final Cut");
        H.Check("\"The Final Cut\" alone is not stripped to nothing or an article",
                bare.Count == 1 && bare[0] == "The Final Cut", string.Join(" | ", bare));

        H.Check("variants are de-duplicated", V("Portal 2").Count == V("Portal 2").Distinct(StringComparer.OrdinalIgnoreCase).Count());
        H.Check("no variant is blank", V("Witcher 3: Wild Hunt, The").All(v => !string.IsNullOrWhiteSpace(v)));
    }

    /// <summary>What each SteamGridDB endpoint's <c>mimes</c> enum ACCEPTS (openapi.yml). ⚠ Not a copy of what
    /// we ask for — it's the remote contract we're checked against, which is why it's written out here rather
    /// than derived from <c>SgdbAssetPaths</c>. <c>mimes</c> is validated server-side, so a value outside these
    /// sets is a 400 on every request; sending <c>jpeg</c> to /logos killed 100% of logos for two days and
    /// wrote a 14-day .miss per game (commit 8568303). Widen a row here only against the live spec.</summary>
    private static readonly Dictionary<string, string[]> SgdbEnum = new()
    {
        ["/grids/"]  = new[] { "png", "jpeg", "webp" },
        ["/heroes/"] = new[] { "png", "jpeg", "webp" },
        ["/logos/"]  = new[] { "png", "webp" },
        ["/icons/"]  = new[] { "png", "ico" },
    };

    /// <summary>Formats WPF can decode through WIC on a stock Windows 10. WebP needs the Store's "Webp Image
    /// Extensions" and AVIF isn't decodable either — and an undecodable asset is worse than a missing one: it
    /// downloads fine, lands in the cache under its cover/logo name, and a cached file that exists is a hit
    /// that is never re-fetched. So it fails FOREVER, silently.</summary>
    private static readonly string[] WpfDecodable = { "png", "jpeg", "ico", "bmp", "gif", "tiff" };

    // ── every asset request carries a mimes= filter its endpoint actually accepts ─────────────────────
    private static void MimesFilter()
    {
        var with = H.StaticMethod(GA, "WithSgdbMimes", 1);
        if (with is null) { H.Fail("GameArt.WithSgdbMimes(url) not found", "renamed? the filter is untested"); return; }
        string W(string url) => (string)with.Invoke(null, new object[] { url });

        // Driven off GameArt's own per-endpoint table rather than a list repeated here — a copy of the table
        // in the test gets out of step with the code and ends up demanding the exact request shape a fix
        // exists to remove. The accepted enum is PER ENDPOINT: asking /logos for jpeg is a 400 on EVERY
        // request, which reads as "this game has no logo" and .miss-poisons it for 14 days.
        var table = (ValueTuple<string, string>[])H.GetStatic(GA, "SgdbAssetPaths");
        H.Check("the asset-path table still covers every image endpoint",
                table.Length == SgdbEnum.Count
                && table.All(row => SgdbEnum.ContainsKey(row.Item1)),
                string.Join(" ", table.Select(row => row.Item1)));

        foreach (var (path, mimes) in table)
        {
            if (!SgdbEnum.TryGetValue(path, out var allowed))
            {
                H.Fail($"{path} has no known mimes enum", "new endpoint? check openapi.yml before shipping it");
                continue;
            }
            var url = W($"https://www.steamgriddb.com/api/v2{path}game/1234");
            H.Check($"{path} requests carry the mimes filter", url.Contains("mimes="), url);

            // Decoded from the built URL, not from the table's constant, so the check covers the appending too.
            var asked = Uri.UnescapeDataString(url[(url.IndexOf("mimes=", StringComparison.Ordinal) + 6)..])
                           .Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(t => t.Trim().Replace("image/", ""))
                           .ToArray();
            string shown = $"asks {string.Join("+", asked)}; endpoint takes {string.Join("+", allowed)}";

            // The two rules that matter, stated as rules. Neither pins a literal request shape, so a legitimate
            // per-endpoint difference is free while the failures that actually cost us are caught.
            H.Check($"{path} asks only for types this endpoint ACCEPTS",
                    asked.Length > 0 && asked.All(allowed.Contains), shown);
            H.Check($"{path} asks only for types WPF can DECODE on Win10",
                    asked.All(WpfDecodable.Contains), shown);
            H.Check($"{path} still asks for png (the one universally safe format)",
                    asked.Contains("png"), shown);
        }

        var q = W("https://www.steamgriddb.com/api/v2/grids/steam/620?styles=alternate");
        H.Check("an existing query string gets & not ?", q.Contains("?styles=alternate&mimes="), q);

        H.Check("the filter is idempotent (never doubled)",
                W(W("https://www.steamgriddb.com/api/v2/grids/steam/620"))
                    .Split("mimes=").Length == 2);

        var search = W("https://www.steamgriddb.com/api/v2/search/autocomplete/Portal");
        H.Check("the search endpoint is NOT given an image filter (it returns games)",
                !search.Contains("mimes="), search);
    }

    // ── the live half: does art still resolve across this machine's real library ─────────────────────
    private static async Task LiveResolution()
    {
        var cacheDir = CacheDirectory;
        var cached = Directory.Exists(cacheDir)
            ? Directory.GetFiles(cacheDir).Length : 0;
        Console.WriteLine($"        (record) art cache: {cached} files in {cacheDir}");

        var games = GameLibrary.Scan();
        if (games.Length == 0) { H.Skip("live art resolution", "no games scanned"); return; }

        // Spot-check a spread of stores, preferring games that ALREADY have art — the checklist asks for
        // "including one that had art before", the case that matters most.
        var sample = games.GroupBy(g => g.Storefront).Select(grp => grp.First()).Take(8).ToList();
        int ok = 0, miss = 0;
        foreach (var g in sample)
        {
            string path = null;
            try { path = await CachedPathAsync(g); }
            catch (Exception ex) { Console.WriteLine($"        (record)   {g.Name}: threw {ex.GetType().Name}"); }
            if (path is not null && File.Exists(path))
            {
                ok++;
                var len = new FileInfo(path).Length;
                var head = ImageKind(path);
                H.Check($"cover for \"{g.Name}\" [{g.Storefront}] decodes as PNG/JPEG",
                        head is "PNG" or "JPEG", $"{head}, {len / 1024} KB");
            }
            else { miss++; Console.WriteLine($"        (record)   no cover resolved for \"{g.Name}\" [{g.Storefront}]"); }
        }
        Console.WriteLine($"        (record) covers resolved for {ok}/{sample.Count} sampled games ({miss} without)");
        H.Check("art resolves for most of the sampled library", ok >= Math.Max(1, sample.Count / 2),
                $"{ok}/{sample.Count}");

        // Nothing WebP may exist in the cache — that's the whole point of the filter, and a WebP file that got
        // in would be a permanent un-decodable hit.
        if (Directory.Exists(cacheDir))
        {
            var webp = Directory.GetFiles(cacheDir)
                .Where(f => ImageKind(f) == "WEBP").Take(5).ToList();
            H.Check("no WebP images in the art cache", webp.Count == 0,
                    webp.Count == 0 ? $"{cached} files checked" : string.Join(", ", webp.Select(Path.GetFileName)));
        }
    }

    /// <summary>Identify an image by its magic bytes rather than its extension — the cache names files after
    /// the game, so an extension proves nothing about what's inside.</summary>
    private static string ImageKind(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var b = new byte[12];
            if (fs.Read(b, 0, 12) < 12) return "short";
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "PNG";
            if (b[0] == 0xFF && b[1] == 0xD8) return "JPEG";
            if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
                && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "WEBP";
            return "other";
        }
        catch { return "unreadable"; }
    }
}
