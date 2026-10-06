using System.IO;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>RADIATA PICKER — a dev-only curation export, reached only by <c>Radiata.exe --export-picker</c>.
/// For a fixed seed list of 100 popular PC games on the fake "Picker" storefront (MatchKeys
/// "picker|name|*", which no real game can collide with), it writes picker-export.json and CuratedArt.g.cs
/// to %APPDATA%\Radiata: each game's cover, logo and slice-logo choice as SteamGridDB source URLs. Picks are
/// read from cover-overrides.json entries under "picker_name_*" keys and from picker-slice-logos.json; a
/// game with no pick gets today's default resolved from SteamGridDB and pinned. Nothing in the app records
/// new picks (<see cref="SliceLogoPickerWindow"/> has no caller).
///
/// To remove the Picker completely:
///  1. Delete this file and SliceLogoPickerWindow.xaml(.cs).
///  2. App.Cli.cs: delete CliExportPicker and its `--export-picker` entry in CliCommands.
///  3. GameArt.cs: delete the three PickerDefault*UrlAsync resolvers. PickerCoverUrlCacheFile and
///     PickerLogoUrlCacheFile stay while CuratedPicksExport reads them.
///  4. Docs: the `--export-picker` row in docs/BUILD-RELEASE.md, and any file map that lists the Picker files.
///  The checked-in CuratedArt.g.cs is shipping data and stays, frozen. Leftover "picker_name_*" entries in
///  cover-overrides.json are inert (they never match a real game).</summary>
internal static class RadiataPicker
{
    /// <summary>The fake storefront — makes every MatchKey "picker|name|&lt;norm&gt;", isolated from
    /// real libraries, with SteamGridDB-by-name as the art source (no native id, no launcher).</summary>
    public const string Storefront = "Picker";

    /// <summary>Top 100 PC games by popularity — a fixed seed list, taken as-is.</summary>
    private static readonly string[] Top100 =
    [
        "Roblox", "Fortnite", "League of Legends", "Counter-Strike 2", "Minecraft",
        "Dota 2", "Valorant", "Call of Duty", "PUBG: BATTLEGROUNDS", "Palworld",
        "Grand Theft Auto V", "EA SPORTS FC 26", "Apex Legends", "Rust", "Path of Exile",
        "Marvel Rivals", "The Sims 4", "Tom Clancy’s Rainbow Six Siege", "Dead by Daylight", "Warframe",
        "Battlefield 6", "Overwatch 2", "Escape from Tarkov", "World of Warcraft", "Diablo IV",
        "Rocket League", "Final Fantasy XIV Online", "Black Desert", "Old School RuneScape", "Genshin Impact",
        "Delta Force", "Path of Exile 2", "Destiny 2", "Project Zomboid", "Stardew Valley",
        "War Thunder", "Team Fortress 2", "Slay the Spire 2", "Baldur’s Gate 3", "Cyberpunk 2077",
        "DayZ", "VRChat", "BeamNG.drive", "Hearts of Iron IV", "Football Manager 26",
        "Euro Truck Simulator 2", "Farming Simulator 25", "Arena Breakout: Infinite", "Garry’s Mod", "ARC Raiders",
        "Red Dead Redemption 2", "Terraria", "Elden Ring", "RimWorld", "The Elder Scrolls V: Skyrim Special Edition",
        "ARK: Survival Ascended", "HELLDIVERS 2", "Mount & Blade II: Bannerlord", "Total War: WARHAMMER III", "Left 4 Dead 2",
        "Forza Horizon 6", "NARAKA: BLADEPOINT", "Don’t Starve Together", "Zenless Zone Zero", "Warhammer 40,000: Space Marine 2",
        "Crusader Kings III", "Valheim", "Satisfactory", "Factorio", "Hunt: Showdown 1896",
        "ARK: Survival Evolved", "The Witcher 3: Wild Hunt", "Age of Empires II: Definitive Edition", "NBA 2K26", "Where Winds Meet",
        "Arma Reforger", "THE FINALS", "Kingdom Come: Deliverance II", "EVE Online", "Guild Wars 2",
        "The Elder Scrolls Online", "Lost Ark", "Monster Hunter Wilds", "Monster Hunter: World", "Wuthering Waves",
        "Magic: The Gathering Arena", "No Man’s Sky", "Sid Meier’s Civilization VI", "Sid Meier’s Civilization VII", "Fallout 76",
        "Dark and Darker", "Albion Online", "MapleStory", "Yu-Gi-Oh! Master Duel", "Brawlhalla",
        "World of Tanks", "World of Warships", "Among Us", "Phasmophobia", "Black Myth: Wukong",
    ];

    /// <summary>The 100 seed entries, on the Picker storefront with an empty LaunchUrl.</summary>
    public static IReadOnlyList<InstalledGame> SeedGames() =>
        [.. Top100.Select(n => new InstalledGame(n, "", Storefront))];

    private sealed record PickRow(string Name, string Norm, bool Done,
        string? CoverUrl, string CoverFrom, string? FlatCover,
        bool HideLogo, int LogoIndex, string? LogoUrl, string LogoFrom,
        string? SliceLogoUrl, string SliceFrom);

    /// <summary>Write ALL 100 games to %APPDATA%\Radiata\picker-export.json as portable data: game
    /// name + normalized name (the cross-machine match key), the chosen cover's SGDB source URL (or
    /// flat-colour hex), and the logo choice (hidden / URL). Cycled picks resolve offline from the
    /// on-disk candidate-URL caches; a game left on its DEFAULT cover/logo gets that default's source
    /// URL resolved from SteamGridDB NOW and written explicitly — "default" is a ranking-dependent
    /// pick, and SGDB rankings shift, so the export pins the actual image, not the slot. coverFrom /
    /// logoFrom say how each URL was chosen ("pick" = cycled, "default" = pinned today's default,
    /// "flat" / "hidden" / "none" = no URL). Also writes CuratedArt.g.cs beside it — the same picks as
    /// the C# table the app ships (copy over the repo's CuratedArt.g.cs to re-bake). Returns (count, path).</summary>
    public static async Task<(int Exported, string Path)> ExportAsync()
    {
        Dictionary<string, JsonElement> raw = new();
        if (File.Exists(GameMetadata.OverridePath))
            raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(GameMetadata.OverridePath)) ?? new();

        // Slice-logo picks from the mouse-driven Slice Logo Picker window (norm-keyed, URLs already
        // explicit). Games without a pick get the DEFAULT slice resolution pinned below, like covers.
        Dictionary<string, JsonElement> slicePicks = new();
        string slicePath = Path.Combine(AppPaths.AppDataDir, "picker-slice-logos.json");
        if (File.Exists(slicePath))
            slicePicks = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(slicePath)) ?? new();

        var rows = new List<PickRow>();
        foreach (var g in SeedGames())
        {
            // CoverKey convention: MatchKey with '|' → '_' (e.g. "picker_name_roblox").
            string key = GameLibrary.MatchKey(g).Replace('|', '_');
            JsonElement el = default;
            bool hasOverride = raw.TryGetValue(key, out el) && el.ValueKind == JsonValueKind.Object;

            string path   = hasOverride && el.TryGetProperty("Path",      out var p) ? p.GetString() ?? "" : "";
            bool   hide   = hasOverride && el.TryGetProperty("HideLogo",  out var h) && h.ValueKind == JsonValueKind.True;
            int    logoIx = hasOverride && el.TryGetProperty("LogoIndex", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 0;
            bool   done   = hasOverride && el.TryGetProperty("Favorite",  out var f) && f.ValueKind == JsonValueKind.True;

            string? coverUrl = null, flatCover = null;
            string coverFrom = "default";
            if (path.Length > 0)
            {
                var file = Path.GetFileName(path);
                if (file.StartsWith("flat_", StringComparison.OrdinalIgnoreCase))
                {
                    flatCover = Path.GetFileNameWithoutExtension(file)["flat_".Length..];
                    coverFrom = "flat";
                }
                else
                {
                    // Cover alternates are cached as "sgdbalt_<id>_<index>.jpg" — the index maps straight
                    // into the candidate-URL list the cycle used (urls_covers_<id>.json).
                    var m = System.Text.RegularExpressions.Regex.Match(file, @"_(\d+)\.[A-Za-z]+$");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int ix)
                        && ReadUrlList(GameArt.PickerCoverUrlCacheFile(g)) is { } covers && ix < covers.Count)
                    { coverUrl = covers[ix]; coverFrom = "pick"; }
                }
            }
            // On the default cover (or an unresolvable alternate) → pin TODAY'S default explicitly,
            // so a future SteamGridDB re-ranking can't change the pairing.
            if (coverUrl is null && flatCover is null)
            {
                coverUrl = await GameArt.PickerDefaultCoverUrlAsync(g).ConfigureAwait(false);
                coverFrom = coverUrl is null ? "none" : "default";
            }

            string? logoUrl = null;
            string logoFrom;
            if (hide) logoFrom = "hidden";
            else if (logoIx > 0
                && ReadUrlList(GameArt.PickerLogoUrlCacheFile(g)) is { } logos && logoIx - 1 < logos.Count)
            { logoUrl = logos[logoIx - 1]; logoFrom = "pick"; }
            else
            {
                // Default logo (or an unresolvable alternate) → same pinning as the cover.
                logoUrl = await GameArt.PickerDefaultLogoUrlAsync(g).ConfigureAwait(false);
                logoFrom = logoUrl is null ? "none" : "default";
            }

            string norm = GameLibrary.NormalizeName(g.Name);
            string? sliceUrl = null;
            string sliceFrom;
            if (slicePicks.TryGetValue(norm, out var sp) && sp.ValueKind == JsonValueKind.Object
                && sp.TryGetProperty("Url", out var su) && su.GetString() is { Length: > 0 } surl)
            { sliceUrl = surl; sliceFrom = "pick"; }
            else
            {
                sliceUrl = await GameArt.PickerDefaultSliceLogoUrlAsync(g).ConfigureAwait(false);
                sliceFrom = sliceUrl is null ? "none" : "default";
            }

            rows.Add(new PickRow(g.Name,
                norm,                                // cross-machine match key (any store)
                done,                                // △ star = "I finished picking this one"
                coverUrl, coverFrom, flatCover,
                hide, logoIx, logoUrl, logoFrom,
                sliceUrl, sliceFrom));
        }

        string outPath = Path.Combine(AppPaths.AppDataDir, "picker-export.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions
        { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        File.WriteAllText(Path.Combine(AppPaths.AppDataDir, "CuratedArt.g.cs"), CuratedSource(rows));
        return (rows.Count, outPath);
    }

    /// <summary>Render the picks as the CuratedArt table source the app ships (see CuratedArt.cs).</summary>
    private static string CuratedSource(List<PickRow> rows)
    {
        static string S(string? v) => v is null ? "null" : $"\"{v}\"";   // URLs/hex are plain ASCII, no escaping needed
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// <auto-generated> RADIATA PICKER — do not hand-edit. Regenerate with");
        sb.AppendLine("// `Radiata.exe --export-picker`, then copy %APPDATA%\\Radiata\\CuratedArt.g.cs over this file.");
        sb.AppendLine("// Data: curated Radiata Picker choices, pinned as explicit SteamGridDB URLs. </auto-generated>");
        sb.AppendLine("namespace ControllerWheel;");
        sb.AppendLine();
        sb.AppendLine("internal static partial class CuratedArt");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Dictionary<string, Entry> Table = new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var r in rows)
        {
            if (r.CoverUrl is null && r.FlatCover is null && r.LogoUrl is null && !r.HideLogo && r.SliceLogoUrl is null) continue;   // nothing to bake
            sb.AppendLine($"        // {r.Name}  (cover: {r.CoverFrom}, logo: {r.LogoFrom}, slice: {r.SliceFrom})");
            sb.AppendLine($"        [{S(r.Norm)}] = new({S(r.CoverUrl)}, {S(r.FlatCover)}, {S(r.LogoUrl)}, {(r.HideLogo ? "true" : "false")}, {S(r.SliceLogoUrl)}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static List<string>? ReadUrlList(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) : null;
        }
        catch { return null; }
    }
}
