namespace ControllerWheel;

/// <summary>Shipped default art picks for ~100 popular PC games, pinned as EXPLICIT SteamGridDB image
/// URLs — never "whatever SGDB ranks first today", which shifts as the community votes. A curated entry
/// is the auto-resolution chain's default; the user's own Select/Start cycle overrides still win.
///
/// The table lives in CuratedArt.g.cs (regenerate via `Radiata.exe --export-picker` and copy the emitted
/// file over it). This class is SHIPPING code even though the Picker (RadiataPicker.cs) is a dev tool: if
/// the Picker goes, keep this and its .g.cs.
///
/// <para><c>PicksTable</c> in CuratedArtPicks.g.cs (`--export-mypicks`, see CuratedPicksExport) is a
/// FALLBACK, not a merge — the Picker's hand-curated entry always wins.</para></summary>
internal static partial class CuratedArt
{
    /// <summary>One game's curated picks. At most one of <paramref name="CoverUrl"/> /
    /// <paramref name="FlatCoverHex"/> is set (both null = no cover pick); <paramref name="LogoUrl"/> null
    /// with <paramref name="HideLogo"/> false = no logo pick. <paramref name="SliceLogoUrl"/> is the
    /// WHEEL-SLICE logo — a near-square box, a different pick from the grid's wide wordmark.</summary>
    internal sealed record Entry(string? CoverUrl, string? FlatCoverHex, string? LogoUrl, bool HideLogo,
                                 string? SliceLogoUrl);

    /// <summary>Curated picks for a game, keyed by <see cref="GameLibrary.NormalizeName"/> of its title
    /// (punctuation/case-insensitive, so it matches across storefronts). Null when not curated.</summary>
    public static Entry? Get(string gameName)
    {
        var key = GameLibrary.NormalizeName(gameName);
        return Table.TryGetValue(key, out var e) ? e
             : PicksTable.TryGetValue(key, out var o) ? o
             : null;
    }

    /// <summary>Whether the PICKER's table (not the <c>PicksTable</c> fallback) already covers this normalized
    /// name. Lets <see cref="CuratedPicksExport"/> skip what the Top-100 curation already owns.</summary>
    internal static bool IsPickerCurated(string norm) => Table.ContainsKey(norm);

    /// <summary>The <c>PicksTable</c> entries AS THIS BUILD SHIPS THEM. <see cref="CuratedPicksExport"/> merges onto them
    /// for a scoped re-bake: a saved pick only survives in <c>cover-overrides.json</c> until that game is
    /// cycled again or the file is reset, so regenerating from the live picks alone would silently drop
    /// every entry baked from a pick that's since gone.</summary>
    internal static IReadOnlyDictionary<string, Entry> ShippedPicksEntries => PicksTable;
}
