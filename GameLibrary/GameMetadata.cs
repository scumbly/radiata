using System.IO;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// Per-game Game Grid personalization and art-pick overrides — cover/logo choice, favorite, hidden,
/// last-launched — persisted to cover-overrides.json. <see cref="GameArt"/> exposes these as its own
/// public API (thin forwards) so callers are unaffected by where the storage lives.
/// </summary>
internal static class GameMetadata
{
    /// <summary>A saved per-game art pick. <see cref="Path"/> = the chosen cover image ("" = the
    /// auto-resolved default); <see cref="HideLogo"/> = the logo/title overlay is toggled OFF (Start);
    /// <see cref="LogoIndex"/> = which logo the overlay shows (0 = the default resolution, 1.. = the
    /// nth SteamGridDB alternate). Cover and logo are independent — Select edits Path, Start edits the rest.</summary>
    internal sealed class CoverPick
    {
        public string Path { get; set; } = "";
        public bool   HideLogo { get; set; }
        public int    LogoIndex { get; set; }
        /// <summary>The user turned the overlay back ON for a game whose CURATED entry hides it. Needed
        /// because <see cref="HideLogo"/> false is also the no-opinion default, which can't outvote the
        /// curated choice — without this flag those games could never be un-hidden.</summary>
        public bool   LogoUnhidden { get; set; }
        /// <summary>Fingerprint (<see cref="GameArt.UrlFingerprint"/>) of the chosen logo's URL — the durable
        /// half of the pick. <see cref="LogoIndex"/> is only the cycle's POSITION, and positions move when
        /// SteamGridDB re-ranks; empty on a pre-fingerprint entry and at stop 0 (the default resolution).</summary>
        public string LogoKey { get; set; } = "";
        // Game Grid personalization: pinned favorite, hidden-from-grid, and the last time Radiata launched
        // the game (UTC ticks; 0 = never) for recency sorting.
        public bool   Favorite { get; set; }
        public bool   Hidden { get; set; }
        public long   LastLaunched { get; set; }
        // Identity bundle — stamped on save, and enriched on the first exact-key hit of a legacy entry.
        // Lets a lookup re-find the pick after its CoverKey (MatchKey) flips — a Playnite import/rename,
        // an id⇄name key change, or a Playnite/scan source swap — by matching a STABLE component instead:
        // the install folder, or storefront+name. Empty on pre-fix entries (they match by exact key only
        // until re-picked). Stored normalised (NormalizeName / NormDir) so compares are direct.
        public string Store { get; set; } = "";
        public string Name  { get; set; } = "";
        public string Dir   { get; set; } = "";
    }

    private static Dictionary<string, CoverPick>? _overrides;
    private static readonly object _overridesLock = new();   // Select cover-cycle (UI) vs art resolution (bg threads)
    // internal (not private) so Back Up / Restore Settings can bundle the user's Game Grid art/logo picks.
    internal static readonly string OverridePath = Path.Combine(AppPaths.AppDataDir, "cover-overrides.json");

    // Caller must hold _overridesLock (Dictionary isn't thread-safe; background tile resolution reads while
    // the Select button mutates on the UI thread).
    private static Dictionary<string, CoverPick> Overrides()
    {
        if (_overrides is null)
        {
            try
            {
                _overrides = new();
                if (File.Exists(OverridePath))
                {
                    // Lenient load: old format stored a bare path string; new format an object.
                    var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(OverridePath)) ?? new();
                    foreach (var (k, v) in raw)
                        _overrides[k] = v.ValueKind == JsonValueKind.String
                            ? new CoverPick { Path = v.GetString() ?? "" }
                            : v.Deserialize<CoverPick>() ?? new CoverPick();
                }
            }
            catch (Exception ex)
            {
                _overrides = new();
                // Corrupt/unreadable overrides file: copy it aside (timestamped) BEFORE the next
                // SaveOverrides() writes an empty {} over it and loses EVERY art pick (mirrors
                // ConfigLoader.BackUpUnreadableConfig). Best-effort; the empty dict is the in-memory fallback.
                try
                {
                    if (File.Exists(OverridePath))
                    {
                        var bak = Path.Combine(AppPaths.AppDataDir, $"cover-overrides-corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                        File.Copy(OverridePath, bak, overwrite: true);
                        System.Diagnostics.Trace.WriteLine($"[Art] UNREADABLE cover-overrides.json → {bak}; starting empty ({ex.Message}).");
                    }
                }
                catch { /* best-effort */ }
            }
        }
        return _overrides;
    }

    // PRIMARY key for a saved cover pick: the reconciliation identity (id-based for Steam/GOG/Battle.net/
    // Ubisoft; normalised name otherwise). But MatchKey still FLIPS for the same installed game — Playnite
    // imports/renames a name-keyed title, a Steam/GOG entry gains/loses its native id, or the grid reads
    // from the scan vs Playnite — which orphaned the pick (art reset to default while the tile stayed).
    // So this is only the fast-path key now: <see cref="Resolve"/> falls back to the pick's identity bundle
    // (install folder / storefront+name) and re-homes it under the current key when the key has flipped.
    // The "_" form keeps Steam keys identical to the old scheme, so existing Steam picks stay a fast hit.
    private static string CoverKey(InstalledGame game) => GameLibrary.MatchKey(game).Replace('|', '_');

    /// <summary>Normalised install folder for identity matching ("" when unknown).</summary>
    private static string NormDir(string? p) => string.IsNullOrWhiteSpace(p) ? "" : p.Trim().TrimEnd('\\', '/').ToLowerInvariant();

    /// <summary>Stamp the pick with the game's current identity (store + normalised name + install folder).
    /// Returns true if anything changed, so the caller persists. Caller holds <see cref="_overridesLock"/>.</summary>
    private static bool StampIdentity(CoverPick p, InstalledGame game)
    {
        var name = GameLibrary.NormalizeName(game.Name);
        var dir  = NormDir(game.InstallDir);
        bool changed = !string.Equals(p.Store, game.Storefront, StringComparison.OrdinalIgnoreCase)
                       || p.Name != name || p.Dir != dir;
        p.Store = game.Storefront; p.Name = name; p.Dir = dir;
        return changed;
    }

    /// <summary>Whether a saved pick belongs to <paramref name="game"/> by a STABLE, UNIQUE identity — its
    /// install folder — used to re-find a pick whose CoverKey flipped (Playnite import/rename, id⇄name).
    /// ONLY the install folder is used: a store+name fallback is unsafe because two DISTINCT games can share
    /// a normalized name on one storefront (a game + its playtest/demo, two same-titled entries), and would
    /// steal + delete each other's pick during a plain browse. Games with no folder (a bare live-scan Steam
    /// entry) simply fall back to exact-key match — which is stable for those id-keyed stores anyway.</summary>
    private static bool MatchesIdentity(CoverPick p, InstalledGame game)
    {
        var dir = NormDir(game.InstallDir);
        return p.Dir.Length > 0 && dir.Length > 0 && string.Equals(p.Dir, dir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The saved pick for a game, resilient to a flipped <see cref="CoverKey"/> (Playnite
    /// import/rename, id⇄name, source swap): exact key first; else re-find by a stable identity component
    /// and RE-HOME it under the current key (fast next time + the file converges). A legacy entry hit by
    /// exact key gets its identity stamped so a FUTURE flip is survivable. <paramref name="create"/> mints a
    /// fresh pick, keyed now, when nothing matches. Caller holds <see cref="_overridesLock"/>.</summary>
    private static CoverPick? Resolve(InstalledGame game, bool create)
    {
        var map = Overrides();
        var key = CoverKey(game);
        if (map.TryGetValue(key, out var exact))
        {
            // Persist an identity stamp / re-home ONLY on a write. A read (create:false — CoverOverride,
            // CoverHidesLogo, LogoIndexFor, tile rendering) must be pure: merely browsing must not rewrite
            // cover-overrides.json, and a bug in dir/store detection during a read must not move a user's
            // pick to a new key. Reads still RETURN the identity-matched pick; the next write re-homes it.
            if (create && StampIdentity(exact, game)) SaveOverrides();
            return exact;
        }
        string? hitKey = null; CoverPick? hit = null;
        foreach (var (k, p) in map)
            if (MatchesIdentity(p, game)) { hitKey = k; hit = p; break; }
        if (hit is not null)
        {
            if (create)
            {
                map.Remove(hitKey!);
                StampIdentity(hit, game);
                map[key] = hit;              // re-home under the current key (write path only)
                SaveOverrides();
            }
            return hit;
        }
        if (!create) return null;
        var fresh = new CoverPick();
        StampIdentity(fresh, game);
        map[key] = fresh;               // caller sets fields + SaveOverrides()
        return fresh;
    }

    internal static CoverPick? CoverOverride(InstalledGame game)
    {
        lock (_overridesLock) return Resolve(game, create: false);
    }

    /// <summary>Whether the game's logo/title overlay is toggled OFF by the USER's own Start pick.
    /// ⚠ User-only on purpose — a surface deciding whether to DRAW the overlay asks
    /// <see cref="LogoHiddenFor"/>, which also consults the curated picks.</summary>
    internal static bool CoverHidesLogo(InstalledGame game) => CoverOverride(game) is { HideLogo: true };

    /// <summary>Whether the overlay should be OFF for this game, curated entries included — what the tile
    /// asks. A curated <c>HideLogo</c> means "this game reads best with nothing over its cover", and that
    /// has to suppress the whole overlay: suppressing only the logo IMAGE leaves the tile's title-TEXT
    /// fallback drawing a wordmark over art curated to have none.
    ///
    /// <para>The user's Start press wins in both directions — off via <see cref="CoverPick.HideLogo"/>, back on via
    /// <see cref="CoverPick.LogoUnhidden"/>. A flat-colour cover still forces the overlay on regardless
    /// (see <see cref="CoverIsFlat"/>); that's the caller's check, not this one's.</para></summary>
    internal static bool LogoHiddenFor(InstalledGame game)
    {
        var pick = CoverOverride(game);
        if (pick is { HideLogo: true }) return true;
        if (pick is { LogoUnhidden: true }) return false;
        return CuratedArt.Get(game.Name)?.HideLogo == true;
    }

    /// <summary>The game's saved cover pick is one of the flat-colour stops. Flat covers ALWAYS render
    /// with the logo/title overlay (a bare colour tile reads as broken), overriding a saved logo-off
    /// choice for as long as the flat cover is active.</summary>
    internal static bool CoverIsFlat(InstalledGame game) =>
        CoverOverride(game)?.Path is { Length: > 0 } p
        && Path.GetFileName(p).StartsWith("flat_", StringComparison.OrdinalIgnoreCase);

    /// <summary>The user explicitly turned the overlay back ON for a curated-hidden game.</summary>
    internal static bool LogoUnhiddenFor(InstalledGame game) => CoverOverride(game) is { LogoUnhidden: true };

    /// <summary>Which logo the game's overlay shows: 0 = the default resolution, 1.. = the nth
    /// SteamGridDB alternate (Start cycling).</summary>
    internal static int LogoIndexFor(InstalledGame game) => CoverOverride(game)?.LogoIndex ?? 0;

    /// <summary>Fingerprint of the game's saved logo pick ("" = none / the default stop).</summary>
    internal static string SavedLogoKey(InstalledGame game) => CoverOverride(game)?.LogoKey ?? "";

    /// <summary>Persist the user's chosen COVER (Select cycling); the logo state (Start) is preserved.</summary>
    internal static void SetCoverPath(InstalledGame game, string path)
    {
        lock (_overridesLock)
        {
            var pick = Resolve(game, create: true)!;   // re-homes a key-flipped pick, never duplicates it
            pick.Path = path;
            SaveOverrides();
        }
    }

    /// <summary>Revert the game's COVER to the auto-resolved default (Select cycle stop 0); the logo state
    /// (Start) and grid personalization are preserved. Drops the whole record when everything is back at
    /// defaults.</summary>
    internal static void ClearCoverPath(InstalledGame game)
    {
        lock (_overridesLock)
        {
            var p = Resolve(game, create: false);   // re-homes under CoverKey(game) if the key had flipped
            if (p is null) return;
            p.Path = "";
            if (IsPrunable(p)) Overrides().Remove(CoverKey(game));
            SaveOverrides();
        }
    }

    /// <summary>Persist the game's LOGO overlay state (Start cycling): shown/hidden + which logo. The cover
    /// pick (Select) is preserved. Drops the whole record when everything is back at defaults.
    /// <para><paramref name="logoUrl"/> is the URL of the stop just landed on (null at stop 0 / hidden). It
    /// is what actually pins the pick — see <see cref="CoverPick.LogoKey"/>.</para></summary>
    internal static void SetLogoState(InstalledGame game, bool hideLogo, int logoIndex, string? logoUrl = null)
    {
        lock (_overridesLock)
        {
            var pick = Resolve(game, create: true)!;   // re-homes under CoverKey(game); never duplicates
            pick.HideLogo  = hideLogo;
            pick.LogoIndex = logoIndex;
            // Hiding keeps the key: the saved logo resumes when the overlay comes back on.
            if (!hideLogo) pick.LogoKey = logoUrl is { Length: > 0 } u ? GameArt.UrlFingerprint(u) : "";
            // Turning the overlay back on for a CURATED-hidden game is a real choice, not a return to the
            // default — record it, or the curated hide re-applies on the next tile draw and the stop the
            // user just landed on is invisible.
            pick.LogoUnhidden = !hideLogo && CuratedArt.Get(game.Name)?.HideLogo == true;
            if (IsPrunable(pick))
                Overrides().Remove(CoverKey(game));
            SaveOverrides();
        }
    }

    /// <summary>Nothing left worth keeping in a pick: cover, logo state AND grid personalization all at their
    /// defaults. The one test every prune site uses, so resetting one facet never drops another.</summary>
    private static bool IsPrunable(CoverPick p) =>
        p.Path.Length == 0 && !p.HideLogo && !p.LogoUnhidden && p.LogoIndex == 0
        && !p.Favorite && !p.Hidden && p.LastLaunched == 0;

    // ── Game Grid personalization (favorite / hidden / recency) ─────────────────────────────────────

    internal static bool IsFavorite(InstalledGame game)
    {
        lock (_overridesLock) return Resolve(game, create: false)?.Favorite == true;
    }

    internal static void SetFavorite(InstalledGame game, bool on)
    {
        lock (_overridesLock)
        {
            var pick = Resolve(game, create: true)!;
            pick.Favorite = on;
            SaveOverrides();
        }
    }

    internal static bool IsHidden(InstalledGame game)
    {
        lock (_overridesLock) return Resolve(game, create: false)?.Hidden == true;
    }

    internal static void SetHidden(InstalledGame game, bool on)
    {
        lock (_overridesLock)
        {
            var pick = Resolve(game, create: true)!;
            pick.Hidden = on;
            SaveOverrides();
        }
    }

    /// <summary>Stamp "Radiata launched this game now" for recency sorting (our own launches; external
    /// launch times aren't reliably knowable across storefronts).</summary>
    internal static void MarkLaunched(InstalledGame game)
    {
        lock (_overridesLock)
        {
            var pick = Resolve(game, create: true)!;
            pick.LastLaunched = DateTime.UtcNow.Ticks;
            SaveOverrides();
        }
    }

    internal static long LastLaunchedTicks(InstalledGame game)
    {
        lock (_overridesLock) return Resolve(game, create: false)?.LastLaunched ?? 0;
    }

    /// <summary>Clear the hidden flag on EVERY game (Settings ▸ Advanced ▸ Reset Hidden Games).
    /// Returns how many were un-hidden.</summary>
    internal static int ResetHiddenAll()
    {
        lock (_overridesLock)
        {
            int n = 0;
            foreach (var p in Overrides().Values)
                if (p.Hidden) { p.Hidden = false; n++; }
            if (n > 0) SaveOverrides();
            return n;
        }
    }

    /// <summary>Reset every saved COVER pick to the auto-resolved default (the art cache backing them was
    /// just cleared); logo choices (Start) and grid personalization are preserved. Drops a record left at
    /// all-defaults.</summary>
    internal static void ClearAllCoverPaths()
    {
        try
        {
            lock (_overridesLock)
            {
                var map = Overrides();
                foreach (var k in map.Keys.ToList())
                {
                    map[k].Path = "";
                    if (IsPrunable(map[k])) map.Remove(k);
                }
                SaveOverrides();
            }
        }
        catch { }
    }

    private static void SaveOverrides()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDir);
            // Atomic write (temp + move) so an interrupted write can't truncate cover-overrides.json and
            // lose every art pick — same pattern as ConfigLoader / PlayniteLibrary.
            AtomicFile.WriteAllText(OverridePath, JsonSerializer.Serialize(_overrides));
        }
        catch { /* best effort */ }
    }
}
