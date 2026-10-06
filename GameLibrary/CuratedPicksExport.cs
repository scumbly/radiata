using System.IO;
using System.Linq;
using System.Text;

namespace ControllerWheel;

/// <summary>Bakes saved Game Grid art choices into a second shipped <see cref="CuratedArt"/> table,
/// alongside the ~100 hand-picked entries the Radiata Picker produces (see <see cref="RadiataPicker"/>).
///
/// <para>The picks live in <c>%APPDATA%\Radiata\cover-overrides.json</c>, which is per-machine and holds
/// LOCAL CACHE PATHS, not portable URLs. This walks the live library, maps each saved pick back to the
/// SteamGridDB URL it came from using the on-disk candidate-URL caches, and emits
/// <c>CuratedArtPicks.g.cs</c>: the shipping fallback table <see cref="CuratedArt.Get"/> consults after
/// <c>Table</c>.</para>
///
/// <para>Deliberately OFFLINE and PICKS-ONLY. A game left on its auto-resolved default contributes
/// nothing: pinning today's default would freeze one SteamGridDB ranking for every future user, and the
/// resolution chain already produces it. Only a deliberate Select cover / Start logo / logo-off choice is baked.
/// (The Picker pins defaults because there the "pick" was a curation decision made in a review UI.)</para>
///
/// <para>Run: <c>Radiata.exe --export-mypicks</c> → <c>%APPDATA%\Radiata\CuratedArtPicks.g.cs</c>, then copy
/// it over the repo's copy — the same ritual as <c>--export-picker</c>.</para></summary>
internal static class CuratedPicksExport
{
    private sealed record Row(string Name, string Norm, string Store,
                              string? CoverUrl, string? FlatCover, string CoverFrom,
                              string? LogoUrl, bool HideLogo, string LogoFrom);

    /// <summary>Scan the library, resolve every saved pick back to a URL, and write the table.
    /// Returns (games with at least one bakeable pick, unresolvable picks skipped, covers whose legacy POSITION
    /// had drifted and were re-matched by artwork, output path, requested names that matched nothing). Every
    /// baked URL is byte-verified against the cached pick, so the re-matched count is information, not a
    /// caveat: it says how far SteamGridDB's ranking has moved since those files were cached.
    ///
    /// <para><paramref name="onlyNames"/> scopes the bake to specific games (matched on
    /// <see cref="GameLibrary.NormalizeName"/>, so punctuation and case don't matter). A cover cycle leaves a
    /// saved pick behind for every game it was ever pressed on — including presses that were only browsing —
    /// so baking the lot would freeze accidents as curated defaults for every user.</para>
    ///
    /// <para>⚠ <b>A scoped run MERGES onto <see cref="CuratedArt.ShippedPicksEntries"/></b>; only the named
    /// games are re-resolved and every other shipped entry is carried through untouched. It has to:
    /// <c>cover-overrides.json</c> holds only picks that still exist on this machine — reset it, or cycle a
    /// game back to its default, and the pick that produced a shipped entry is gone. Emitting just the scoped
    /// rows would have deleted 38 entries to add 4. An UNSCOPED run is a deliberate full rebuild from the
    /// current picks and still replaces the table wholesale, reporting anything it drops.</para></summary>
    public static async Task<(int Baked, int Skipped, int Legacy, string Path, List<string> Unmatched,
                              int Carried, List<string> Dropped)> ExportAsync(
        IReadOnlyCollection<string>? onlyNames = null)
    {
        var rows = new List<Row>();
        int skipped = 0;
        // norm → how to label a carried-forward entry. The shipped table stores only the picks, so a
        // carried entry's game name comes from the live library; one that's since been uninstalled keeps
        // its key as the label rather than losing the row.
        var labels = new Dictionary<string, (string Name, string Store)>(StringComparer.Ordinal);
        // Requested name → normalized; anything still unmatched at the end is reported rather than silently
        // baking nothing (a typo'd title would otherwise look exactly like a game with no picks).
        var wanted = onlyNames?.ToDictionary(n => n, GameLibrary.NormalizeName, StringComparer.Ordinal);
        var matched = new HashSet<string>(StringComparer.Ordinal);
        // Deduped by normalized name: the table is name-keyed, so two storefronts' copies of one game would
        // collide. First pick wins (Scan order), which is stable run to run.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var g in GameLibrary.Scan())
        {
            string norm = GameLibrary.NormalizeName(g.Name);
            if (norm.Length == 0 || !seen.Add(norm)) continue;
            labels[norm] = (g.Name, g.Storefront ?? "");
            if (wanted is not null)
            {
                if (!wanted.ContainsValue(norm)) continue;
                matched.Add(norm);
            }
            // The shipped Picker table wins: it was curated deliberately, entry by entry.
            if (CuratedArt.IsPickerCurated(norm)) continue;

            var (coverUrl, flatCover, coverFrom, coverLost) = await ResolveCoverAsync(g).ConfigureAwait(false);
            var (logoUrl, hideLogo, logoFrom, logoLost)     = await ResolveLogoAsync(g).ConfigureAwait(false);
            skipped += (coverLost ? 1 : 0) + (logoLost ? 1 : 0);

            if (coverUrl is null && flatCover is null && logoUrl is null && !hideLogo) continue;   // nothing picked
            rows.Add(new Row(g.Name, norm, g.Storefront ?? "",
                             coverUrl, flatCover, coverFrom, logoUrl, hideLogo, logoFrom));
        }

        int baked = rows.Count;
        var fresh = rows.Select(r => r.Norm).ToHashSet(StringComparer.Ordinal);
        var missing = CuratedArt.ShippedPicksEntries.Keys.Where(k => !fresh.Contains(k)).ToList();
        int carried = 0;
        if (wanted is not null)
        {
            // Scoped: everything this run didn't re-resolve rides through exactly as it shipped.
            foreach (var key in missing)
            {
                var e = CuratedArt.ShippedPicksEntries[key];
                var (name, store) = labels.TryGetValue(key, out var l) ? l : (key, "not installed");
                rows.Add(new Row(name, key, store, e.CoverUrl, e.FlatCoverHex, "carried",
                                 e.LogoUrl, e.HideLogo, "carried"));
                carried++;
            }
        }

        rows = [.. rows.OrderBy(r => r.Norm, StringComparer.Ordinal)];   // stable diffs between runs
        string outPath = Path.Combine(AppPaths.AppDataDir, "CuratedArtPicks.g.cs");
        File.WriteAllText(outPath, Source(rows, onlyNames), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var unmatched = wanted is null
            ? new List<string>()
            : [.. wanted.Where(kv => !matched.Contains(kv.Value)).Select(kv => kv.Key)];
        return (baked, skipped, rows.Count(r => r.CoverFrom == "pick-rematched"), outPath, unmatched,
                carried, wanted is null ? missing : []);
    }

    /// <summary>Map a saved Select cover pick back to its SteamGridDB URL. <c>lost</c> = there IS a pick but it
    /// couldn't be resolved (its candidate-URL cache is gone, or a legacy position-keyed file's artwork is no
    /// longer among the candidates) — reported rather than silently guessed at.</summary>
    private static async Task<(string? Url, string? Flat, string From, bool Lost)> ResolveCoverAsync(InstalledGame g)
    {
        if (GameArt.SavedCoverPath(g) is not { Length: > 0 } saved) return (null, null, "default", false);
        var file = Path.GetFileName(saved);
        if (file.StartsWith("flat_", StringComparison.OrdinalIgnoreCase))
            return (null, Path.GetFileNameWithoutExtension(file)["flat_".Length..], "flat", false);

        var urls = ReadUrlList(GameArt.PickerCoverUrlCacheFile(g));
        if (urls is not null)
        {
            // Current naming: sgdbalt_<id>_<sha256-of-url[..16]>.jpg — hash each candidate and compare, the
            // same match CandidateIndexOf makes. Pins the pick to the ARTWORK, immune to re-ranking, and needs
            // no verification: the filename IS the URL's fingerprint.
            foreach (var u in urls)
                if (string.Equals(GameArt.CandidateCachePath(g, u), saved, StringComparison.OrdinalIgnoreCase))
                    return (u, null, "pick", false);

            // Legacy naming: the trailing component is the candidate's POSITION in the list.
            // That mapping is NOT trustworthy on its own — SteamGridDB re-ranks and the list is refetched every
            // 7 days, so position N today need not be the image cached as N months ago. So we VERIFY against
            // the bytes actually on disk: try the positional candidate first, then every other candidate, and
            // accept only an exact byte match. Baking an unverified guess would ship the wrong artwork as a
            // curated default for every future user, which is worse than shipping nothing for this game.
            if (ReadBytes(saved) is { } want)
            {
                var order = new List<string>();
                var m = System.Text.RegularExpressions.Regex.Match(file, @"_(\d{1,3})\.[A-Za-z]+$");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int ix) && ix < urls.Count) order.Add(urls[ix]);
                order.AddRange(urls.Where(u => order.Count == 0 || u != order[0]));
                foreach (var u in order)
                    if (await GameArt.FetchImageBytesAsync(u).ConfigureAwait(false) is { } got
                        && got.AsSpan().SequenceEqual(want))
                        return (u, null, order.Count > 0 && u == order[0] ? "pick-legacy-verified" : "pick-rematched", false);
            }
        }
        return (null, null, "unresolved", true);
    }

    private static byte[]? ReadBytes(string path)
    {
        try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
        catch { return null; }
    }

    /// <summary>Map a saved Start logo choice back to a URL. Logo-off is portable as-is (HideLogo); index 0
    /// means the default, which isn't baked.</summary>
    private static async Task<(string? Url, bool Hide, string From, bool Lost)> ResolveLogoAsync(InstalledGame g)
    {
        // Effective state, not just the user's pick: a game whose hide came from PicksTable has no
        // local pick to read, so asking CoverHidesLogo here would drop the hide on every re-bake. An
        // explicit un-hide still reads through (LogoUnhidden), so this stays idempotent both ways.
        if (GameMetadata.LogoHiddenFor(g)) return (null, true, "hidden", false);
        int idx = GameMetadata.LogoIndexFor(g);
        string key = GameMetadata.SavedLogoKey(g);
        if (idx <= 0 && key.Length == 0) return (null, false, "default", false);
        if (ReadUrlList(GameArt.PickerLogoUrlCacheFile(g)) is not { } urls) return (null, false, "unresolved", true);

        // Fingerprinted pick: the saved key IS the URL's identity, so a match needs no verification and
        // survives any re-ranking between the press and this bake.
        if (key.Length > 0)
            foreach (var u in urls)
                if (string.Equals(key, GameArt.UrlFingerprint(u), StringComparison.OrdinalIgnoreCase))
                    return (u, false, "pick", false);

        // Legacy POSITION-keyed pick (logoalt_<id>_<n>.png), same hazard the cover side already handles:
        // the list refetches weekly and SteamGridDB re-ranks, so position N today need not be the artwork
        // cached as N back then. Verify against the bytes on disk — try the positional candidate first,
        // then every other — and accept only an exact match. Baking an unverified guess would ship the
        // wrong logo as a curated default for every future user.
        if (idx > 0 && ReadBytes(GameArt.LegacyLogoCandidatePath(g, idx - 1)) is { } want)
        {
            var order = new List<string>();
            if (idx - 1 < urls.Count) order.Add(urls[idx - 1]);
            order.AddRange(urls.Where(u => order.Count == 0 || u != order[0]));
            foreach (var u in order)
                if (await GameArt.FetchImageBytesAsync(u).ConfigureAwait(false) is { } got
                    && got.AsSpan().SequenceEqual(want))
                    return (u, false, u == order[0] ? "pick-legacy-verified" : "pick-rematched", false);
        }
        return (null, false, "unresolved", true);
    }

    private static List<string>? ReadUrlList(string file)
    {
        try
        {
            return File.Exists(file)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) : null;
        }
        catch { return null; }
    }

    private static string Source(List<Row> rows, IReadOnlyCollection<string>? onlyNames)
    {
        static string S(string? v) => v is null ? "null" : $"\"{v}\"";   // URLs/hex are plain ASCII
        string scope = onlyNames is null ? "" : $" --only=\"{string.Join(";", onlyNames)}\"";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated> RADIATA CURATED PICKS — do not hand-edit. Regenerate with");
        sb.AppendLine($"// `Radiata.exe --export-mypicks{scope}`, then copy %APPDATA%\\Radiata\\CuratedArtPicks.g.cs over this file.");
        if (onlyNames is not null)
            sb.AppendLine("// That run re-resolved only the scoped games; rows marked (carried) came through from the\n" +
                          "// table the running build shipped, so re-running it needs that same build, not a clean one.");
        sb.AppendLine("// Data: curated Game Grid art choices, pinned as explicit SteamGridDB URLs —");
        sb.AppendLine("// additions to the Picker's Top-100 table in CuratedArt.g.cs. See CuratedPicksExport.cs. </auto-generated>");
        sb.AppendLine("namespace ControllerWheel;");
        sb.AppendLine();
        sb.AppendLine("internal static partial class CuratedArt");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Consulted only when <c>Table</c> has no entry — see <see cref=\"Get\"/>.</summary>");
        sb.AppendLine("    private static readonly Dictionary<string, Entry> PicksTable = new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var r in rows)
        {
            // The grid's wide-wordmark logo is NOT a valid wheel-slice logo (near-square box), and the grid
            // cycle can't express a slice pick — so SliceLogoUrl stays null and slice art keeps auto-resolving.
            // A carried row's provenance labels aren't recoverable from the compiled table — the
            // entry itself is unchanged, which is what the label needs to say.
            sb.AppendLine(r.CoverFrom == "carried"
                ? $"        // {r.Name}  [{r.Store}]  (carried through unchanged)"
                : $"        // {r.Name}  [{r.Store}]  (cover: {r.CoverFrom}, logo: {r.LogoFrom})");
            sb.AppendLine($"        [{S(r.Norm)}] = new({S(r.CoverUrl)}, {S(r.FlatCover)}, {S(r.LogoUrl)}, {(r.HideLogo ? "true" : "false")}, null),");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
