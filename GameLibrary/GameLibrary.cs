using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ControllerWheel;

public sealed record InstalledGame(string Name, string LaunchUrl, string Storefront)
{
    public string DisplayName => $"{Name}  [{Storefront}]";

    /// <summary>Local cover-art file path (Playnite-sourced games); null = use other art sources.</summary>
    public string? CoverPath { get; init; }

    /// <summary>Cover-art URL the STOREFRONT'S OWN metadata gave us, to be downloaded and cached like any
    /// other remote art (itch's butler.db; null elsewhere). Distinct from <see cref="CoverPath"/>, which is
    /// a file already on disk. Display-only, same as every fetched image.</summary>
    public string? SourceCoverUrl { get; init; }

    /// <summary>Install folder, when the source knows it (itch scan, Playnite). Used to dedupe a live-scan
    /// game against a Playnite entry for the SAME install even when their names differ (e.g. a stale itch
    /// receipt title vs the curated Playnite name) — see App.LoadGamesUnfiltered. Null when unknown.</summary>
    public string? InstallDir { get; init; }

    /// <summary>Direct storefront launch URL (e.g. steam://rungameid/ID), bypassing Playnite. Set
    /// for Playnite-sourced games where it's derivable; null otherwise. The non-Playnite scanner's
    /// <see cref="LaunchUrl"/> is already direct, so callers use <c>DirectLaunchUrl ?? LaunchUrl</c>.</summary>
    public string? DirectLaunchUrl { get; init; }
}

public static class GameLibrary
{
    public static InstalledGame[] Scan()
    {
        // Each storefront scanner is guarded independently: one flaky source (a permissions error on a
        // Steam library folder, a dismounted drive, a locked DB) must cost only ITS games, not throw out
        // of Scan() — several callers reach here from async-void UI paths where an escape would crash.
        var games = new List<InstalledGame>();
        void Add(string store, Func<IEnumerable<InstalledGame>> scan)
        {
            try { games.AddRange(scan()); }
            catch (Exception ex) { Trace.WriteLine($"[Library] {store} scan failed: {ex.Message}"); }
        }
        Add("Steam",      ScanSteam);
        Add("Epic",       ScanEpic);
        Add("GOG",        ScanGog);
        Add("Xbox",       ScanXbox);
        Add("itch",       ScanItch);
        Add("Battle.net", ScanBattleNet);
        Add("Ubisoft",    ScanUbisoft);
        Add("EA",         ScanEa);
        Add("Amazon",     ScanAmazon);
        return [.. games
            .DistinctBy(g => g.LaunchUrl)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)];
    }

    // ── Reconciliation (Playnite list ↔ live storefront scan) ───────────────────
    //
    // Playnite gives unified names + local cover art, but its "IsInstalled" flag only refreshes
    // when Playnite itself runs — so a storefront uninstall while Playnite is closed leaves a
    // ghost. Reconcile() keeps Playnite as the list (covers/curation intact) but drops any entry
    // the live storefront scan contradicts.

    /// <summary>A storefront-scoped identity for matching the same game across the Playnite list
    /// and the live scan. Steam/GOG/Battle.net/Ubisoft key on the native id embedded in the (direct)
    /// launch URL — exact and safe. Epic/Xbox/itch (and unknown stores) key on (storefront +
    /// normalised name), since Playnite exposes no shared id for them (mirrors the long-standing
    /// ResolveScanUrl convention).</summary>
    public static string MatchKey(InstalledGame g)
    {
        // Playnite games carry the direct URL on DirectLaunchUrl (Steam/GOG only); the live scan
        // carries it on LaunchUrl. Either way the id lives in "<scheme>/rungameid/<id>".
        var direct = g.DirectLaunchUrl ?? g.LaunchUrl;
        if (g.Storefront.Equals("Steam", StringComparison.OrdinalIgnoreCase)
            && RunGameId(direct, "steam://rungameid/") is { } sid)
            return $"steam|{sid}";
        if (g.Storefront.Equals("GOG", StringComparison.OrdinalIgnoreCase)
            && RunGameId(direct, "goggalaxy://rungameid/") is { } gid)
            return $"gog|{gid}";
        if (g.Storefront.Equals("Battle.net", StringComparison.OrdinalIgnoreCase)
            && RunGameId(direct, "battlenet://") is { } bid)
            return $"battlenet|{bid}";
        // Ubisoft keys on the Uplay id, not the name: the live scan names a game by its install-FOLDER
        // (e.g. "RainbowSix") while Playnite uses the real title, so a name key never matches and the
        // store stays un-enforced — leaving uninstalled ghosts. Both sides carry uplay://launch/<id>/0.
        if (g.Storefront.Equals("Ubisoft", StringComparison.OrdinalIgnoreCase)
            && RunGameId(direct, "uplay://launch/") is { } uid)
            return $"uplay|{uid.Split('/')[0]}";
        // Amazon CAN be id-keyed (unlike EA): our scan reads the id straight out of the client's own
        // GameInstallInfo.sqlite, which is the same value Playnite's Amazon plugin stores as its GameId.
        if (g.Storefront.Equals("Amazon", StringComparison.OrdinalIgnoreCase)
            && RunGameId(direct, "amazon-games://play/") is { } aid)
            return $"amazon|{aid}";
        return $"{g.Storefront.ToLowerInvariant()}|name|{NormalizeName(g.Name)}";
    }

    /// <summary>Whether a <see cref="MatchKey"/> carries an exact native id, as opposed to the
    /// "&lt;store&gt;|name|&lt;normalised-name&gt;" fallback (NormalizeName strips everything but
    /// letters/digits, so "|name|" can only come from the fallback format itself).</summary>
    internal static bool IsIdKey(string key) => !key.Contains("|name|", StringComparison.Ordinal);

    /// <summary>Best-effort <see cref="InstalledGame.Storefront"/> for a bare launch URL, when the real
    /// scanned game isn't at hand (a slice carries only its URL). THE ONE implementation — every caller
    /// must use it, because <c>GameArt</c> keys its art cache and its ".miss" markers on
    /// <c>Storefront + "_" + Name</c>: two inference maps meant one game could hold up to three separate
    /// cache-key sets, so a slice re-fetched art the Game Grid already had.
    /// <para>Every value here must be spelled exactly as the scanner that owns that store spells it
    /// (<see cref="Scan"/> / <see cref="PlayniteLibrary"/>) or the keys diverge again.</para>
    /// <para>Unknown returns <c>""</c> — the same "store we can't name" value the scanners use. A
    /// <c>playnite://</c> URL lands there deliberately: it routes to some real storefront we can't read
    /// off the URL, and Playnite is an aggregator, not a storefront (no <see cref="LauncherCatalog"/>
    /// storefront either). Inventing a "Playnite" store would key those games away from their own art.
    /// An itch game usually launches by bare exe path, so it lands there too.</para></summary>
    public static string StorefrontFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        bool Scheme(string s) => url.StartsWith(s, StringComparison.OrdinalIgnoreCase);
        return Scheme("steam:")                 ? "Steam"
             : Scheme("goggalaxy:")             ? "GOG"
             : Scheme("battlenet:")             ? "Battle.net"
             : Scheme("uplay:")                 ? "Ubisoft"
             : Scheme("origin2:")               ? "EA"
             : Scheme("amazon-games:")          ? "Amazon"
             : Scheme("itch:")                  ? "itch.io"
             : Scheme("shell:AppsFolder")       ? "Xbox"
             : url.Contains("epicgames", StringComparison.OrdinalIgnoreCase) ? "Epic"
             : "";
    }

    private static string? RunGameId(string url, string scheme) =>
        url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) && url.Length > scheme.Length
            ? url[scheme.Length..] : null;

    /// <summary>Punctuation/case-insensitive name key for cross-source matching.</summary>
    public static string NormalizeName(string s) =>
        new string([.. s.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

    /// <summary>Are two NORMALIZED names (see <see cref="NormalizeName"/>) plausibly the same game whose
    /// title drifted between sources — i.e. equal, or differing only by a recognised EDITION suffix
    /// ("skyrim" vs "skyrimspecialedition")?
    ///
    /// Never weaken this to a bare substring test: "portal2" CONTAINS "portal", which silently hides a
    /// freshly-installed Portal whenever Playnite lists Portal 2. Sequels are ruled out by construction:
    /// the residue after the shorter title must be composed ENTIRELY of edition words, and "2",
    /// "eternal", "wildhunt" are not edition words. The word list errs conservative — a miss means a
    /// briefly-doubled tile (self-heals when Playnite re-imports); a false hit means a hidden game,
    /// the strictly worse failure.</summary>
    public static bool NamesLikelySameGame(string normA, string normB)
    {
        if (normA.Length == 0 || normB.Length == 0) return false;
        if (normA == normB) return true;
        var (shortN, longN) = normA.Length < normB.Length ? (normA, normB) : (normB, normA);
        if (!longN.StartsWith(shortN, StringComparison.Ordinal)) return false;
        // Greedy word-consume over the residue; "the" only leads a phrase ("thegameoftheyearedition"
        // is covered because "gameoftheyear" and "edition" are words and "the" is too).
        var rest = longN.AsSpan(shortN.Length);
        while (rest.Length > 0)
        {
            string? hit = null;
            foreach (var w in EditionWords)
                if (rest.StartsWith(w, StringComparison.Ordinal) && (hit is null || w.Length > hit.Length))
                    hit = w;   // longest match, so "gameoftheyear" beats a leading "the"+dead-end
            if (hit is null) return false;
            rest = rest[hit.Length..];
        }
        return true;
    }

    /// <summary>Normalized-form words that mark an EDITION of the same game rather than a different game.
    /// Deliberately short: every addition must be a word that never begins a legitimate sequel/spin-off
    /// title of its base game.</summary>
    private static readonly string[] EditionWords =
    [
        "edition", "deluxe", "goty", "gameoftheyear", "remastered", "definitive", "ultimate",
        "complete", "anniversary", "enhanced", "directorscut", "special", "legendary", "premium",
        "gold", "standard", "digital", "the",
    ];

    /// <summary>Filter the Playnite list down to what the live storefront scan confirms is still
    /// installed — removing ghosts left by Playnite's stale IsInstalled flag — while preserving
    /// Playnite covers/names. Fails safe: a store is only enforced if at least one Playnite entry
    /// of that store matched a live identity by exact id (proof the scanner sees the store AND the
    /// id keying lines up) or the store carries a launcher-level signal (confirmedStores). Name-keyed
    /// stores (Epic/Xbox/itch/EA) are never match-enforced: one title's name lining up proves nothing
    /// about a sibling whose Playnite name drifted from the live manifest name. A store we don't scan,
    /// an empty/blind scan, or a key-format
    /// mismatch all leave that store's entries untouched, so we never hide a genuinely installed
    /// game. The trade-off: uninstalling the *last* game of a store can't be detected (nothing
    /// left to prove the store), so that lone ghost lingers until Playnite next refreshes —
    /// UNLESS the entry recorded an install directory, in which case the name-proof
    /// <see cref="InstallDirGone"/> check below catches it regardless of store keying.</summary>
    public static IReadOnlyList<InstalledGame> Reconcile(
        IReadOnlyList<InstalledGame> playnite, IReadOnlyList<InstalledGame> liveScan,
        IReadOnlySet<string>? confirmedStores = null)
    {
        // Name-proof ghost check first: a Playnite entry whose recorded InstallDirectory is MISSING while
        // its drive root is present was uninstalled behind Playnite's back — a filesystem fact, immune to
        // the name-drift problem that blocks match-enforcement for the name-keyed stores (itch/Epic),
        // whose ghosts would otherwise linger until Playnite re-imports.
        var kept = new List<InstalledGame>(playnite.Count);
        foreach (var g in playnite)
        {
            if (InstallDirGone(g))
                Trace.WriteLine($"[Library] ghost dropped (install dir gone): {g.Name} [{g.Storefront}] @ {g.InstallDir}");
            else
                kept.Add(g);
        }
        playnite = DedupePlaynite(kept);

        var live = new HashSet<string>(liveScan.Select(MatchKey), StringComparer.OrdinalIgnoreCase);

        // Only id-keyed matches (Steam/GOG/Battle.net/Ubisoft — see MatchKey) may enforce a store:
        // an id match proves the keying lines up for EVERY entry of that store. A name-keyed match
        // would turn one lucky name into store-wide enforcement and drop an installed sibling game
        // whose Playnite name drifted (rename, metadata edition-suffix) from the live manifest name.
        var enforce = playnite
            .Where(g => { var k = MatchKey(g); return IsIdKey(k) && live.Contains(k); })
            .Select(g => g.Storefront)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A store whose launcher is confirmed installed (see ScannableStores) is authoritative even with
        // ZERO live games, so uninstalling the LAST game of the store drops its ghost instead of leaving
        // it un-enforced. Without this, the lone remaining ghost lingers (nothing to "prove" the store).
        if (confirmedStores is not null) enforce.UnionWith(confirmedStores);

        // Direct-launch enrichment: Playnite carries a direct URL only for Steam/GOG; without this,
        // Epic / Xbox / itch entries launch through playnite:// (popping Playnite open as a middleman)
        // even when our own live scan has the direct form. Where the live scan has the same game
        // (MatchKey), copy its launch URL onto the Playnite entry's DirectLaunchUrl. Callers prefer
        // DirectLaunchUrl ?? LaunchUrl, so the playnite:// form remains the stored identity/fallback.
        var liveByKey = new Dictionary<string, InstalledGame>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in liveScan) liveByKey.TryAdd(MatchKey(g), g);

        return [.. playnite
            .Where(g => !enforce.Contains(g.Storefront) || live.Contains(MatchKey(g)))
            .Select(g => g.DirectLaunchUrl is null && liveByKey.TryGetValue(MatchKey(g), out var lv)
                             && !string.IsNullOrWhiteSpace(lv.LaunchUrl)
                         ? g with { DirectLaunchUrl = lv.LaunchUrl }
                         : g)];
    }

    /// <summary>Collapse duplicate rows WITHIN the Playnite list. Playnite can hold two rows for one
    /// install (plugin re-import, Origin→EA-app migration) differing only by its own game GUID, and
    /// everything downstream assumes the list is internally unique — <see cref="Reconcile"/> only filters
    /// and enriches, and App.LoadGamesUnfiltered's dedupe guards protect the LIVE-scan side only.
    ///
    /// Key: the normalised install directory when the row records one AND names a real storefront, else
    /// <see cref="MatchKey"/>. The directory is the stronger signal (it still matches when the two rows
    /// carry drifted names) but is only ever CORROBORATION, not proof: a directory hit still requires the
    /// names to agree per <see cref="NamesLikelySameGame"/>, and storefront-less rows (unrecognised
    /// PluginId — ROM/emulator plugins, manual adds, which routinely share one folder per system) never
    /// dir-key at all. Both key forms are STOREFRONT-SCOPED, so two stores that happen to record the same
    /// folder can never collapse into one entry.
    ///
    /// Order is preserved, and within a duplicate group the row carrying COVER ART wins — dropping the
    /// illustrated row for a bare one would be a visible grid regression. Drops are traced, never silent.</summary>
    private static IReadOnlyList<InstalledGame> DedupePlaynite(IReadOnlyList<InstalledGame> playnite)
    {
        static string NormDir(string? p) =>
            string.IsNullOrWhiteSpace(p) ? "" : p.Trim().TrimEnd('\\', '/').ToLowerInvariant();

        // Dir-keying needs a REAL storefront: rows whose PluginId we don't recognise all carry
        // Storefront = "" — every ROM/emulator plugin and every manually-added game. Emulated libraries
        // routinely report one shared folder per system, so dir-keying them collapses an entire ROM
        // library into a single tile. Storefront-less rows key on MatchKey (their name) instead:
        // same-folder is only proof of same-game when a launcher owns the folder layout.
        static string DedupeKey(InstalledGame g) =>
            g.Storefront.Length > 0 && NormDir(g.InstallDir) is { Length: > 0 } d
                ? $"{g.Storefront.ToLowerInvariant()}|dir|{d}"
                : MatchKey(g);

        var firstAt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // key → index in result
        var result  = new List<InstalledGame>(playnite.Count);
        foreach (var g in playnite)
        {
            var key = DedupeKey(g);
            if (!firstAt.TryGetValue(key, out int at))
            {
                firstAt[key] = result.Count;
                result.Add(g);
                continue;
            }

            // A shared directory alone isn't proof either even WITH a storefront — some launcher plugins
            // report a common parent/library dir — so the names must corroborate: equal, or
            // edition drift of each other. Distinct-named cohabitants stay, re-keyed by name so a later
            // TRUE duplicate of either still collapses onto it.
            if (!NamesLikelySameGame(NormalizeName(result[at].Name), NormalizeName(g.Name)))
            {
                var nameKey = $"{key}|{NormalizeName(g.Name)}";
                if (!firstAt.TryGetValue(nameKey, out at))
                {
                    firstAt[nameKey] = result.Count;
                    result.Add(g);
                    continue;
                }
            }

            // Duplicate. Keep whichever row has cover art; on a tie the first one stays.
            var incumbent = result[at];
            bool takeNew = string.IsNullOrWhiteSpace(incumbent.CoverPath)
                           && !string.IsNullOrWhiteSpace(g.CoverPath);
            Trace.WriteLine($"[Library] Playnite duplicate collapsed: {g.Name} [{g.Storefront}] @ {g.InstallDir}"
                            + $" — kept the {(takeNew ? "later" : "first")} row (key {key})");
            if (takeNew) result[at] = g;
        }
        return result;
    }

    /// <summary>True when a Playnite entry's recorded install folder is verifiably gone: the path is
    /// rooted, its DRIVE ROOT exists (an unplugged external/network drive must never read as
    /// "uninstalled"), but the folder itself doesn't. Fails SAFE — no recorded dir, relative path, any
    /// IO surprise → false (keep the entry). Xbox/UWP entries are excluded outright: their
    /// WindowsApps install dirs sit behind deny-ACLs where Directory.Exists can report false for a
    /// present folder, which would false-drop installed games.</summary>
    private static bool InstallDirGone(InstalledGame g)
    {
        if (g.Storefront.Equals("Xbox", StringComparison.OrdinalIgnoreCase)) return false;
        var dir = g.InstallDir;
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            if (!Path.IsPathRooted(dir)) return false;
            var root = Path.GetPathRoot(dir);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return false;   // offline drive → keep
            return !Directory.Exists(dir);
        }
        catch { return false; }
    }

    /// <summary>Storefronts whose launcher is confirmed installed, so an EMPTY live scan for them means
    /// "installed, zero games" (authoritative) rather than "couldn't read". <see cref="Reconcile"/> enforces
    /// these even with no match — so uninstalling the LAST game of the store drops its ghost (and its filter
    /// chip), and installing one later brings them back via the live scan. Only stores with a reliable local
    /// presence signal AND exact-id matching belong here (name-keyed stores risk false-dropping on key drift).</summary>
    public static IReadOnlySet<string> ScannableStores()
    {
        var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ubisoft Connect installed → the scan (registry Installs key ∪ the uninstall-hive fallback for
        // legacy titles, each entry dir-checked in ScanUbisoft) is authoritative, so empty = zero games.
        using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher"))
            if (k?.GetValue("InstallDir") is string dir && !string.IsNullOrWhiteSpace(dir))
                s.Add("Ubisoft");

        // Battle.net installed → the Agent's product.db IS the authoritative installed list
        // (ScanBattleNet reads it), so an empty parse = genuinely zero games.
        var bnetDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Battle.net", "Agent", "product.db");
        if (File.Exists(bnetDb)) s.Add("Battle.net");

        // NOT added: Steam — games span multiple library folders that can be on offline/external
        // drives, so an empty scan may be incomplete (would false-drop the whole library).
        // GOG — QueryGogDatabase is install-filtered (InstalledBaseProducts), but adding it here is
        // deferred until verified against a live Galaxy install. Epic/Xbox/itch/EA — name-keyed (no exact
        // id), so enforcing risks false-dropping a game whose live folder-name differs from its
        // Playnite title. (EA in particular cannot be id-keyed: our scan identifies a game by its
        // installerdata.xml contentIDs while Playnite's EA plugin keys on an Origin.OFR.* offer id, so
        // the two sides never produce a common id — see MatchKey.)
        return s;
    }

    /// <summary>Storefront LAUNCHERS present on this PC (regardless of installed games) — resolved through
    /// the very same exe-path helpers <c>WindowsPlatformActions.LaunchStorefront</c> launches, plus the
    /// registry-backed <see cref="ScannableStores"/> set. Used by onboarding's storefront checklist, the
    /// Game Grid's "Open …" cards and the Settings storefront toggles, so an installed-but-empty launcher
    /// lists as present (not "get this app") — and so anything OFFERED is actually LAUNCHABLE.</summary>
    public static HashSet<string> InstalledLaunchers()
    {
        var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { s.UnionWith(ScannableStores()); } catch { }
        // ScannableStores answers "is this store's GAME LIST readable", which is not the same question
        // as "is the launcher app installed". Battle.net is where they diverge: the Agent's product.db
        // (and the games) outlive an uninstalled client, which would offer a storefront slice that can
        // only log "Battle.net.exe not found". Presence for Battle.net = the client exe, via the same
        // discovery the launch path uses.
        s.Remove("Battle.net");
        try { if (WindowsPlatformActions.BattleNetExePath() is not null) s.Add("Battle.net"); } catch { }

        // EA IS live-scanned (ScanEa) but is deliberately NOT in ScannableStores — it's
        // name-keyed, so an empty scan can't be treated as authoritative (see the note there). Presence
        // here is therefore "is the client installed", which is also what gives the store an "Open EA" card
        // in the Game Grid (and therefore a way to hide it). Registry-discovered like Battle.net:
        // EA installs into a versioned folder behind a junction it retargets on every update.
        try { if (WindowsPlatformActions.EaAppExePath() is not null) s.Add("EA"); } catch { }

        // Steam / Epic / GOG / Ubisoft: resolved through the SAME exe-path helpers the launch path uses
        // (WindowsPlatformActions), each of which asks the registry BEFORE falling back to fixed Program
        // Files paths — hardcoded paths miss launchers installed to another drive and Epic's
        // Win32-binary-only installs. Sharing the helper with the launcher keeps "offered == launchable"
        // true by construction.
        void ProbeVia(string store, Func<string?> resolve)
        {
            try { if (resolve() is not null) s.Add(store); } catch { }
        }
        ProbeVia("Steam",   WindowsPlatformActions.SteamExePath);
        ProbeVia("Epic",    WindowsPlatformActions.EpicLauncherExePath);
        ProbeVia("GOG",     WindowsPlatformActions.GogGalaxyExePath);
        ProbeVia("Ubisoft", WindowsPlatformActions.UbisoftConnectExePath);
        ProbeVia("Amazon",  WindowsPlatformActions.AmazonGamesExePath);
        // (Battle.net and EA are resolved above — same helpers, different comment trail.)

        // itch installs its binary into versioned %LOCALAPPDATA%\itch\app-<version>\ folders — no
        // unversioned app\ dir exists — so probe by glob rather than a fixed exe path.
        try
        {
            var itchRoot = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\itch");
            if (Directory.Exists(itchRoot)
                && Directory.EnumerateDirectories(itchRoot, "app-*")
                    .Any(d => File.Exists(Path.Combine(d, "itch.exe"))))
                s.Add("itch.io");
        }
        catch (Exception ex) { Trace.WriteLine($"[Library] itch.io store scan failed: {ex.Message}"); }
        return s;
    }

    /// <summary>Plain-text dump of what the scanners see on THIS machine: which launchers were found (and at
    /// which exe), then every scanned game with its storefront, launch URL and install dir. Written by
    /// <c>Radiata.exe --scan-library [path]</c>.
    ///
    /// <para>Exists because a storefront scanner is only ever as right as the machine it ran on, and the
    /// alternative to a dump is launching the app, opening the Game Grid and squinting. It makes the
    /// answerable questions answerable in one command: is the redist tile gone, did the Xbox name resolve,
    /// is Epic's silent flag on the URL, did that store's exe resolve from the registry.</para></summary>
    public static string DiagnosticReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Radiata library scan");
        sb.AppendLine("====================");
        sb.AppendLine();

        sb.AppendLine("Launcher executables (the resolver shared by presence + launch):");
        foreach (var (name, resolve) in new (string, Func<string?>)[]
        {
            ("Steam",      WindowsPlatformActions.SteamExePath),
            ("Epic",       WindowsPlatformActions.EpicLauncherExePath),
            ("GOG",        WindowsPlatformActions.GogGalaxyExePath),
            ("Ubisoft",    WindowsPlatformActions.UbisoftConnectExePath),
            ("Battle.net", WindowsPlatformActions.BattleNetExePath),
            ("EA",         WindowsPlatformActions.EaAppExePath),
            ("Amazon",     WindowsPlatformActions.AmazonGamesExePath),
        })
        {
            string result;
            try { result = resolve() ?? "(not found)"; }
            catch (Exception ex) { result = "ERROR: " + ex.Message; }
            sb.AppendLine($"  {name,-11} {result}");
        }
        sb.AppendLine();

        try { sb.AppendLine("InstalledLaunchers(): " + string.Join(", ", InstalledLaunchers().OrderBy(x => x))); }
        catch (Exception ex) { sb.AppendLine("InstalledLaunchers() threw: " + ex.Message); }
        try { sb.AppendLine("ScannableStores():    " + string.Join(", ", ScannableStores().OrderBy(x => x))); }
        catch (Exception ex) { sb.AppendLine("ScannableStores() threw: " + ex.Message); }
        sb.AppendLine();

        InstalledGame[] games;
        try { games = Scan(); }
        catch (Exception ex) { sb.AppendLine("Scan() threw: " + ex); return sb.ToString(); }

        sb.AppendLine($"Live scan: {games.Length} game(s)");
        foreach (var grp in games.GroupBy(g => g.Storefront).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine($"  [{(grp.Key.Length == 0 ? "(unnamed)" : grp.Key)}] {grp.Count()}");
            foreach (var g in grp.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"    {g.Name}");
                sb.AppendLine($"      url: {g.LaunchUrl}");
                if (g.InstallDir is { Length: > 0 }) sb.AppendLine($"      dir: {g.InstallDir}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Playnite available: {PlayniteLibrary.IsAvailable}");
        if (PlayniteLibrary.IsAvailable)
        {
            var pn = PlayniteLibrary.GetInstalledGames();
            sb.AppendLine($"Playnite games: {(pn is null ? "unreadable (no DB, no cache)" : pn.Length.ToString())}");
            if (pn is not null)
                foreach (var grp in pn.GroupBy(g => g.Storefront)
                                      .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"  [{(grp.Key.Length == 0 ? "(unnamed)" : grp.Key)}] {grp.Count()}");
        }
        return sb.ToString();
    }

    // ── Shared file reads ─────────────────────────────────────────────────────

    /// <summary>Read a third-party launcher's file as text, tolerating the launcher having it OPEN.
    ///
    /// <para>Two things this does that <see cref="File.ReadAllText(string)"/> doesn't. (1) It opens with
    /// <c>FileShare.ReadWrite</c>: the default share mode is <c>Read</c>, which fails outright if the owning
    /// process holds the file with write access — Steam does exactly that to an <c>.acf</c> mid-update, and
    /// losing <c>libraryfolders.vdf</c> to one such collision costs the WHOLE Steam scan via
    /// <see cref="Scan"/>'s per-store guard, not one game. (2) It retries briefly, because the loser of a
    /// rename/replace race just needs to look again. Both behaviours are lifted from Playnite, which opens
    /// these same files with ReadWrite sharing and wraps manifest reads in a retry
    /// (<c>Playnite/Common/FileSystem.cs</c> ReadFileAsStringSafe, MIT).</para>
    ///
    /// <para>Returns null instead of throwing — every caller's answer to "couldn't read it" is to skip.</para></summary>
    private static string? ReadTextShared(string path, int attempts = 3)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);
                return sr.ReadToEnd();
            }
            catch (IOException) when (i < attempts - 1) { Thread.Sleep(120); }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Library] read failed ({ex.GetType().Name}): {path}");
                return null;
            }
        }
        return null;
    }

    /// <summary>True when <paramref name="dir"/> is VERIFIABLY gone: rooted, its drive root present, but the
    /// folder itself absent. Fails SAFE — an unrooted path, an offline/unplugged drive root, or any IO
    /// surprise answers false ("keep it"), so a game on a detached external never reads as uninstalled.
    /// Shared by the Playnite ghost check and the GOG registry scan so both use one definition.</summary>
    private static bool PathVerifiablyGone(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            if (!Path.IsPathRooted(dir)) return false;
            var root = Path.GetPathRoot(dir);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return false;   // offline drive → keep
            return !Directory.Exists(dir);
        }
        catch { return false; }
    }

    /// <summary>Whether a launcher-supplied relative folder name is safe to combine with a root: a single
    /// path segment, no separators, no drive spec, no traversal. Manifest values are third-party text, and
    /// <see cref="Path.Combine"/> silently honours a rooted second part while "..\" walks out of the base.</summary>
    private static bool IsPlainFolderName(string s) =>
        s.Length > 0 && s != "." && s != ".."
        && s.IndexOfAny(['\\', '/', ':']) < 0
        && s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    // ── Steam ─────────────────────────────────────────────────────────────────

    /// <summary>Steam appids that are installed like games but are not games. 228980 = "Steamworks Common
    /// Redistributables": fully installed on most machines with a real name and valid appid, so it passes
    /// every other filter and would draw a launchable Game Grid tile. Playnite's Steam plugin drops the
    /// same id (<c>SteamLocalService.cs</c>, MIT).</summary>
    private static readonly HashSet<string> SteamNonGameAppIds = new(StringComparer.Ordinal) { "228980" };

    private static IEnumerable<InstalledGame> ScanSteam()
    {
        var steamPath = GetSteamPath();
        if (steamPath == null) yield break;

        foreach (var libraryPath in GetSteamLibraries(steamPath))
        {
            var appsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(appsDir)) continue;

            foreach (var acf in Directory.EnumerateFiles(appsDir, "appmanifest_*.acf"))
            {
                var game = ParseAcf(acf, appsDir);
                if (game != null) yield return game;
            }
        }
    }

    /// <summary>Steam's install root, with separators NORMALISED to backslashes.
    /// <para>⚠ The HKCU value Steam writes uses FORWARD slashes (<c>c:/program files (x86)/steam</c>).
    /// <see cref="Path.Combine"/> joins with a backslash without normalising what it was handed, so every
    /// derived InstallDir came out mixed — <c>c:/program files (x86)/steam\steamapps\common\Overwatch</c> —
    /// and any ordinal <c>StartsWith</c> against a real Win32 image path silently never matched. That broke
    /// game detection for EVERY Steam title (39 of 51 here): Text Chat refused with "no installed game in
    /// the foreground", and per-app volume missed the same games. Normalise here, at the one source, rather
    /// than in each consumer.</para></summary>
    private static string? GetSteamPath()
    {
        if (Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) is string u)
            return NormalizeDir(u);
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is string m)
            return NormalizeDir(m);
        return null;
    }

    /// <summary>Canonical Windows form of a directory a storefront handed us: backslash separators, no
    /// trailing separator. Storefront registry values and manifests are not consistent about either, and a
    /// path only used for display survives that — one used for prefix MATCHING does not.</summary>
    internal static string NormalizeDir(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return dir;
        string s = dir.Trim().Replace('/', '\\');
        try { s = Path.GetFullPath(s); } catch { /* not a rooted/legal path — the replace above still helps */ }
        return s.TrimEnd('\\');
    }

    private static IEnumerable<string> GetSteamLibraries(string steamPath)
    {
        yield return steamPath;

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) yield break;

        // Shared read: Steam holds this file open while it rewrites it, and an exception here loses EVERY
        // library folder but the primary — i.e. most of the library — for that scan. See ReadTextShared.
        var text = ReadTextShared(vdfPath);
        if (text is null) yield break;
        foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
        {
            var path = m.Groups[1].Value.Replace(@"\\", @"\");
            if (!path.Equals(steamPath, StringComparison.OrdinalIgnoreCase))
                yield return path;
        }
    }

    private static InstalledGame? ParseAcf(string path, string appsDir)
    {
        try
        {
            var text  = ReadTextShared(path);
            if (text is null) return null;
            var appId = ExtractVdfValue(text, "appid");
            var name  = ExtractVdfValue(text, "name");
            var flags = ExtractVdfValue(text, "StateFlags");

            if (appId == null || name == null) return null;
            if (int.TryParse(flags, out int f) && (f & 4) == 0) return null;
            if (SteamNonGameAppIds.Contains(appId)) return null;

            // The ACF's "installdir" is the folder name under steamapps\common — EXCEPT for SOUNDTRACKS,
            // which Steam installs under steamapps\music instead. A soundtrack is not launchable, so it's
            // dropped here rather than drawn as a grid tile (Playnite's Steam plugin makes the same call).
            // A game whose folder is in NEITHER place still lists with a null InstallDir: the manifest says
            // it's installed and steam://rungameid still works, so only the folder-match features (the
            // Exceptions auto-passthru-mode path match) lose anything.
            string? installDir = null;
            if (ExtractVdfValue(text, "installdir") is { Length: > 0 } sub && IsPlainFolderName(sub))
            {
                var inCommon = Path.Combine(appsDir, "common", sub);
                if (Directory.Exists(inCommon)) installDir = inCommon;
                else if (Directory.Exists(Path.Combine(appsDir, "music", sub))) return null;   // soundtrack
            }

            return new InstalledGame(name, $"steam://rungameid/{appId}", "Steam") { InstallDir = installDir };
        }
        catch { return null; }
    }

    private static string? ExtractVdfValue(string text, string key)
    {
        var m = Regex.Match(text, $"\"(?i:{Regex.Escape(key)})\"\\s+\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ── Epic Games ────────────────────────────────────────────────────────────

    private static IEnumerable<InstalledGame> ScanEpic()
    {
        var manifestDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifestDir)) yield break;

        var appLocations = EpicInstalledAppLocations();
        foreach (var file in Directory.EnumerateFiles(manifestDir, "*.item"))
        {
            var game = ParseEpicManifest(file, appLocations);
            if (game != null) yield return game;
        }
    }

    /// <summary>AppName → InstallLocation from Epic's own <c>LauncherInstalled.dat</c>. A game's <c>.item</c>
    /// manifest can carry a STALE InstallLocation — Epic leaves the old value behind when an install is
    /// re-pointed by hand (the documented workaround for moving a game to another drive) — while this list
    /// stays correct, so it WINS wherever both exist. Playnite's Epic plugin prefers it for the same reason
    /// (<c>EpicLibrary.cs</c>: "App list seems to have correct location so it should be preferred", MIT).
    /// Empty dictionary on any failure: the manifest value is then used exactly as before.</summary>
    private static Dictionary<string, string> EpicInstalledAppLocations()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var listPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
            if (!File.Exists(listPath)) return map;
            if (ReadTextShared(listPath) is not { } json) return map;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("InstallationList", out var list)
                || list.ValueKind != JsonValueKind.Array) return map;
            foreach (var el in list.EnumerateArray())
            {
                var app = el.TryGetProperty("AppName", out var a) ? a.GetString() : null;
                var loc = el.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
                if (!string.IsNullOrWhiteSpace(app) && !string.IsNullOrWhiteSpace(loc))
                    map[app!] = loc!;
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Library] Epic: LauncherInstalled.dat unreadable: {ex.Message}"); }
        return map;
    }

    private static InstalledGame? ParseEpicManifest(string path, Dictionary<string, string> appLocations)
    {
        try
        {
            if (ReadTextShared(path) is not { } text) return null;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("bIsIncompleteInstall", out var inc) && inc.GetBoolean())
                return null;

            var name    = root.TryGetProperty("DisplayName",      out var dn)  ? dn.GetString()  : null;
            var appName = root.TryGetProperty("AppName",          out var an)  ? an.GetString()  : null;
            var ns      = root.TryGetProperty("CatalogNamespace", out var cns) ? cns.GetString() : null;
            var itemId  = root.TryGetProperty("CatalogItemId",    out var cid) ? cid.GetString() : null;

            if (name == null || appName == null || ns == null || itemId == null) return null;
            if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.Contains('/'))
                return null;

            // Epic writes a .item manifest for EVERY installed app: DLC carries MainGameAppName !=
            // AppName (the field Legendary/Playnite filter on), and non-game installs (Unreal Engine)
            // carry AppCategories without "games"/"applications". Neither belongs in the grid.
            if (root.TryGetProperty("MainGameAppName", out var mga)
                && mga.GetString() is { Length: > 0 } mainApp
                && !mainApp.Equals(appName, StringComparison.OrdinalIgnoreCase))
                return null;
            if (root.TryGetProperty("AppCategories", out var cats) && cats.ValueKind == JsonValueKind.Array
                && !cats.EnumerateArray().Any(c => c.ValueKind == JsonValueKind.String
                    && (c.GetString()!.Equals("games",        StringComparison.OrdinalIgnoreCase)
                     || c.GetString()!.Equals("applications", StringComparison.OrdinalIgnoreCase))))
                return null;

            // Epic's own install list wins over the manifest's (possibly stale) path — see
            // EpicInstalledAppLocations. Either source is accepted only if the folder is actually there.
            var installLoc = appLocations.TryGetValue(appName, out var listed) ? listed : null;
            if (string.IsNullOrWhiteSpace(installLoc) || !Directory.Exists(installLoc))
                installLoc = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
            if (string.IsNullOrWhiteSpace(installLoc) || !Directory.Exists(installLoc)) installLoc = null;

            // silent=true keeps the Epic Games Launcher's own window from opening on top of the game —
            // it's the mask Playnite uses (EpicLauncher.GameLaunchUrlMask, MIT), and on a couch/TV that
            // window otherwise takes the screen and the controller focus while the game loads behind it.
            return new InstalledGame(name,
                $"com.epicgames.launcher://apps/{ns}:{itemId}:{appName}?action=launch&silent=true",
                "Epic") { InstallDir = installLoc };
        }
        catch { return null; }
    }

    // ── GOG Galaxy ────────────────────────────────────────────────────────────

    private static IEnumerable<InstalledGame> ScanGog()
    {
        // Legacy GOG 1.x: games in registry. Collected rather than streamed so an EMPTY result (a key
        // that exists but holds only stale entries) still falls through to the Galaxy 2.0 database below.
        var fromRegistry = ScanGogRegistry();
        if (fromRegistry.Count > 0)
        {
            foreach (var g in fromRegistry) yield return g;
            yield break;
        }

        // GOG Galaxy 2.0: query galaxy-2.0.db
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GOG.com", "Galaxy", "storage", "galaxy-2.0.db");
        if (!File.Exists(dbPath)) yield break;

        foreach (var g in QueryGogDatabase(dbPath)) yield return g;
    }

    private static List<InstalledGame> ScanGogRegistry()
    {
        var games = new List<InstalledGame>();
        using var regRoot = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games")
                            ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");
        if (regRoot is null) return games;

        // Galaxy client presence gates whether goggalaxy:// is usable — a game's presence in this
        // registry key does NOT imply Galaxy is installed (e.g. GOG offline installers).
        bool galaxy = InstalledLaunchers().Contains("GOG");
        foreach (var id in regRoot.GetSubKeyNames())
        {
            // GOG "packs" (bundles) get a registry entry but are not launchable titles of their own.
            // Playnite's GOG plugin skips the same GOGPACK prefix.
            if (id.StartsWith("GOGPACK", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var sub = regRoot.OpenSubKey(id);
                if (sub == null) continue;
                var name   = sub.GetValue("gameName") as string ?? sub.GetValue("GAMENAME") as string;
                var gameId = sub.GetValue("gameID")   as string ?? id;
                var dir    = sub.GetValue("path")     as string ?? sub.GetValue("PATH") as string;

                // A GOG uninstall can leave this key behind, and the entry then reads as an installed game
                // forever — a ghost tile that launches nothing. Drop it, but only when the folder is
                // VERIFIABLY gone (drive present, folder absent): an unplugged external must not un-install
                // the whole library. Playnite requires Directory.Exists outright; PathVerifiablyGone is the
                // same check with our offline-drive escape, matching the Playnite-ghost rule below.
                if (PathVerifiablyGone(dir))
                {
                    Trace.WriteLine($"[Library] GOG: stale registry entry (folder gone) skipped: {name ?? id} @ {dir}");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) dir = null;

                var exe = sub.GetValue("exe") as string ?? sub.GetValue("EXE") as string;
                string? exePath = null;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    exePath = Path.IsPathRooted(exe) ? exe : (dir != null ? Path.Combine(dir, exe) : null);
                    if (exePath == null || !File.Exists(exePath)) exePath = null;
                }
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Galaxy present → prefer the client (richer features). Galaxy absent → launch the game
                // exe directly if we found one (same mechanism the itch scan uses: a direct exe LaunchUrl).
                // Galaxy absent + no usable exe → best-effort fall back to the (dead) goggalaxy:// URL.
                var launchUrl = galaxy || exePath == null ? $"goggalaxy://rungameid/{gameId}" : exePath;
                games.Add(new InstalledGame(name, launchUrl, "GOG") { InstallDir = dir });
            }
            catch { /* unreadable entry — skip just this game */ }
        }
        return games;
    }

    /// <summary>Open a third-party SQLite database read-only and shared, so a scan never takes a lock the
    /// owning app (Galaxy, itch, the Amazon client) needs for its own writes.</summary>
    private static SqliteConnection OpenReadOnlySqlite(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode       = SqliteOpenMode.ReadOnly,
            Cache      = SqliteCacheMode.Shared,
        }.ToString();
        var conn = new SqliteConnection(cs);
        conn.Open();
        return conn;
    }

    private static List<InstalledGame> QueryGogDatabase(string dbPath)
    {
        // InstalledBaseProducts (not Builds — that table also carries owned-but-uninstalled
        // titles) is Galaxy's installed list; LimitedDetails supplies the display title.
        const string sql = """
            SELECT ibp.productId, ld.title
            FROM InstalledBaseProducts ibp
            INNER JOIN LimitedDetails ld ON ld.productId = ibp.productId
            WHERE ld.title IS NOT NULL AND ld.title != ''
            GROUP BY ibp.productId
            """;
        var results = new List<InstalledGame>();
        try
        {
            using var conn = OpenReadOnlySqlite(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var productId = reader.GetInt64(0);
                var title     = reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(title))
                    results.Add(new InstalledGame(title, $"goggalaxy://rungameid/{productId}", "GOG"));
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Library] GOG scan failed: {ex.Message}"); }
        return results;
    }

    // ── Xbox / Game Pass ──────────────────────────────────────────────────────

    private static IEnumerable<InstalledGame> ScanXbox()
    {
        const string regPath = @"SOFTWARE\Microsoft\GamingServices\PackageRepository\Root";
        using var root = Registry.LocalMachine.OpenSubKey(regPath);
        if (root == null) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyName in root.GetSubKeyNames())
        {
            // GUID keys hold the actual installation records; PFN keys are for save tracking
            if (!keyName.StartsWith("{")) continue;

            using var guidKey = root.OpenSubKey(keyName);
            if (guidKey == null) continue;

            foreach (var subName in guidKey.GetSubKeyNames())
            {
                using var pkgKey = guidKey.OpenSubKey(subName);
                if (pkgKey == null) continue;

                var fullPkg = pkgKey.GetValue("Package") as string;
                if (string.IsNullOrWhiteSpace(fullPkg) || !seen.Add(fullPkg)) continue;

                var pkgRoot = pkgKey.GetValue("Root") as string;   // actual install dir (\\?\-prefixed)
                var game = ParseXboxPackage(fullPkg, pkgRoot);
                if (game != null) yield return game;
            }
        }
    }

    private static InstalledGame? ParseXboxPackage(string fullPkgName, string? pkgRoot)
    {
        try
        {
            // The registry entry's "Root" value carries the real install dir (e.g.
            // \\?\D:\XboxGames\<Game>\Content) — modern "full file access" titles and second-drive
            // installs never live under WindowsApps, so only fall back there when Root is absent.
            var dir = !string.IsNullOrWhiteSpace(pkgRoot)
                ? (pkgRoot.StartsWith(@"\\?\", StringComparison.Ordinal) ? pkgRoot[4..] : pkgRoot)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                               "WindowsApps", fullPkgName);
            var manifestPath = Path.Combine(dir, "AppxManifest.xml");
            if (!File.Exists(manifestPath)) return null;

            var xdoc = XDocument.Load(manifestPath);

            var displayName = xdoc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "DisplayName"
                                  && e.Parent?.Name.LocalName == "Properties")
                ?.Value;

            var appId = xdoc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Application")
                ?.Attribute("Id")?.Value;

            if (string.IsNullOrWhiteSpace(appId)) return null;

            // A localized package declares its DisplayName as an "ms-resource:" REFERENCE rather than text
            // (common for publisher-localized titles). Resolve it against the package's own resource map;
            // only a resolve FAILURE skips — skipping the reference outright would make the game invisible
            // in the Game Grid with no trace.
            if (displayName is { Length: > 0 } && displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                var identityName = xdoc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Identity")
                    ?.Attribute("Name")?.Value;
                var resolved = ResolveIndirectString(fullPkgName, identityName, displayName);
                if (string.IsNullOrWhiteSpace(resolved))
                {
                    Trace.WriteLine($"[Library] Xbox: could not resolve '{displayName}' for {fullPkgName} — skipping");
                    return null;
                }
                Trace.WriteLine($"[Library] Xbox: resolved '{displayName}' → '{resolved}'");
                displayName = resolved;
            }
            if (string.IsNullOrWhiteSpace(displayName)) return null;

            // Derive PackageFamilyName: Publisher.AppName_PublisherHash
            // Full name format: Publisher.AppName_Version_Arch__PublisherHash
            var doubleSplit = fullPkgName.Split(["__"], StringSplitOptions.None);
            if (doubleSplit.Length < 2) return null;
            var pkgBase      = doubleSplit[0].Split('_')[0]; // Publisher.AppName
            var publisherHash = doubleSplit[1];
            var familyName   = $"{pkgBase}_{publisherHash}";

            return new InstalledGame(displayName,
                $"shell:AppsFolder\\{familyName}!{appId}", "Xbox")
                { InstallDir = Directory.Exists(dir) ? dir : null };
        }
        catch { return null; }
    }

    /// <summary>Resolve an MSIX <c>ms-resource:</c> display name to real text via
    /// <c>SHLoadIndirectString</c>. Ported from Playnite's <c>Playnite/Common/Resources.cs</c>
    /// (<c>GetIndirectResourceString</c>, MIT — see THIRD-PARTY-LICENSES.md).
    ///
    /// <para>The three input shapes and the retry are the whole trick, and none of them are guessable:
    /// a manifest may carry a full <c>ms-resource://Package/Path/Key</c> URI, a slash-bearing relative
    /// path, or a BARE key — and a bare key lives under <c>/resources/</c> in most packages but at the
    /// root in others, which is why a failed lookup is retried without that segment.</para>
    ///
    /// <para>Returns null on any failure; the caller then skips the package rather than naming it
    /// with the raw reference.</para></summary>
    private static string? ResolveIndirectString(string fullPkgName, string? packageName, string resource)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packageName)) return null;

            string source;
            if (resource.StartsWith("ms-resource://", StringComparison.OrdinalIgnoreCase))
                source = $"@{{{fullPkgName}? {resource}}}";
            else if (resource.Contains('/'))
                source = $"@{{{fullPkgName}? ms-resource://{packageName}/"
                         + resource.Replace("ms-resource:", "", StringComparison.OrdinalIgnoreCase).Trim('/') + "}";
            else
                source = $"@{{{fullPkgName}? ms-resource://{packageName}/resources/{LastUriSegment(resource)}}}";

            if (Load(source) is { } first) return first;

            // Bare key that isn't under /resources/ — try the package root.
            return Load($"@{{{fullPkgName}? ms-resource://{packageName}/{LastUriSegment(resource)}}}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Library] Xbox: indirect-string resolve threw: {ex.Message}");
            return null;
        }

        static string? Load(string source)
        {
            var sb = new System.Text.StringBuilder(1024);
            return NativeMethods.SHLoadIndirectString(source, sb, sb.Capacity, IntPtr.Zero) == 0
                   && sb.Length > 0
                ? sb.ToString() : null;
        }

        // "ms-resource:AppDisplayName" → "AppDisplayName"; also tolerates a trailing-slash form.
        static string LastUriSegment(string s)
        {
            var bare = s.Replace("ms-resource:", "", StringComparison.OrdinalIgnoreCase).Trim('/');
            int slash = bare.LastIndexOf('/');
            return slash >= 0 ? bare[(slash + 1)..] : bare;
        }
    }

    // ── itch.io ───────────────────────────────────────────────────────────────
    // butler.db is the authority for TITLE and CLASSIFICATION as well as install path: caves.game_id
    // joins to a games table carrying (title, classification, cover_url). Don't read titles from each
    // install's .itch/receipt.json[.gz] instead — the receipt is written at INSTALL time and never
    // updated, so a renamed game keeps its stale title forever (which then matches the wrong SteamGridDB
    // art). `classification` (game / tool / assets / book / comic / soundtrack / other) keeps asset packs
    // and soundtracks out of the grid; Playnite's itch plugin filters on the same field.
    //
    // The receipt read stays as the fallback for the no-DB path (and for a cave whose games row is missing).
    // caves.verdict supplies the install path and the relative path to the launchable exe.

    /// <summary>itch classifications that represent something a user can actually LAUNCH. Anything else in
    /// the library (assets, books, comics, soundtracks, tools) is not a game and doesn't belong in the grid.
    /// An unknown/NULL classification is ALLOWED through — a new itch category must not silently empty
    /// someone's library.</summary>
    private static readonly HashSet<string> ItchLaunchableClassifications =
        new(StringComparer.OrdinalIgnoreCase) { "game", "tool" };

    private static IEnumerable<InstalledGame> ScanItch()
    {
        foreach (var install in QueryItchInstalls())
        {
            // "tool" is kept (some itch games ship classified as tools) but assets/books/soundtracks are not.
            if (install.Classification is { Length: > 0 } cls
                && !ItchLaunchableClassifications.Contains(cls))
            {
                Trace.WriteLine($"[Library] itch: skipping non-game '{install.Title ?? install.GameDir}' (classification: {cls})");
                continue;
            }

            // DB title + a launchable target → no need for the receipt at all.
            if (!string.IsNullOrWhiteSpace(install.Title)
                && ItchLaunchUrl(install.GameDir, install.ExeRelPath, install.GameId) is { } url)
            {
                yield return new InstalledGame(install.Title!, url, "itch.io")
                             { InstallDir = install.GameDir, SourceCoverUrl = install.CoverUrl };
                continue;
            }

            var game = ReadItchReceipt(install.GameDir, install.ExeRelPath);
            if (game != null) yield return game;
        }
    }

    /// <summary>One installed itch cave as butler.db describes it.</summary>
    private readonly record struct ItchInstall(
        string GameDir, string? ExeRelPath, string? Title, string? Classification, long? GameId,
        string? CoverUrl);

    /// <summary>Where an itch game launches from: the verdict's candidate exe if there is one, else the
    /// client's own game page. Null when neither is available (the caller then tries the receipt).</summary>
    private static string? ItchLaunchUrl(string gameDir, string? exeRelPath, long? gameId)
    {
        if (!string.IsNullOrWhiteSpace(exeRelPath))
            return Path.Combine(gameDir, exeRelPath.Replace('/', Path.DirectorySeparatorChar));
        return gameId is { } id ? $"itch://games/{id}" : null;
    }

    private static List<ItchInstall> QueryItchInstalls()
    {
        var results = new List<ItchInstall>();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "itch", "db", "butler.db");

        if (File.Exists(dbPath))
        {
            try
            {
                // verdict is a JSON blob: {"basePath":"...","candidates":[{"path":"rel/Game.exe",...}]}.
                // LEFT JOIN, not INNER: a cave whose games row hasn't been cached must still list (it then
                // falls back to the receipt) rather than vanish from the library.
                //
                // still_cover_url before cover_url: an itch cover may be an animated GIF, and still_cover_url
                // is the static frame itch itself publishes for it — WPF can't animate one anyway.
                // ⚠ NULLIF is load-bearing: butler stores an ABSENT still_cover_url as '' (not NULL), and a
                // bare COALESCE takes the empty string — which reads as "this game has no cover" for the
                // ~every game that isn't animated.
                //
                // ⚠ This query stays scoped to caves + games. The same DB holds profiles.api_key, the user's
                // itch API credential; we have no business reading it, so never widen the FROM clause.
                const string sql = """
                    SELECT
                        json_extract(c.verdict, '$.basePath')            AS gameDir,
                        json_extract(c.verdict, '$.candidates[0].path')  AS exePath,
                        g.title                                          AS title,
                        g.classification                                 AS classification,
                        c.game_id                                        AS gameId,
                        COALESCE(NULLIF(g.still_cover_url, ''),
                                 NULLIF(g.cover_url, ''))                AS coverUrl
                    FROM caves c
                    LEFT JOIN games g ON g.id = c.game_id
                    WHERE (c.morphing IS NULL OR c.morphing = 0)
                      AND json_extract(c.verdict, '$.basePath')          IS NOT NULL
                    """;
                using var conn = OpenReadOnlySqlite(dbPath);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var dir = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var exe = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var ttl = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var cls = reader.IsDBNull(3) ? null : reader.GetString(3);
                    long? gid = reader.IsDBNull(4) ? null : reader.GetInt64(4);
                    var cov = reader.IsDBNull(5) ? null : reader.GetString(5);
                    if (!string.IsNullOrWhiteSpace(dir))
                        results.Add(new ItchInstall(dir, exe, ttl?.Trim(), cls?.Trim(), gid, cov?.Trim()));
                }
            }
            catch (Exception ex) { Trace.WriteLine($"[Library] itch scan failed: {ex.Message}"); }
        }

        // Fallback: scan default apps dir if DB gave nothing
        if (results.Count == 0)
        {
            var defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "itch", "apps");
            if (Directory.Exists(defaultDir))
                foreach (var d in Directory.EnumerateDirectories(defaultDir))
                    results.Add(new ItchInstall(d, null, null, null, null, null));
        }

        return results;
    }

    private static InstalledGame? ReadItchReceipt(string gameDir, string? exeRelPath)
    {
        try
        {
            var itchDir  = Path.Combine(gameDir, ".itch");
            var gzPath   = Path.Combine(itchDir, "receipt.json.gz");
            var jsonPath = Path.Combine(itchDir, "receipt.json");

            string? json = null;
            if (File.Exists(gzPath))
            {
                using var fs = File.OpenRead(gzPath);
                using var gs = new GZipStream(fs, CompressionMode.Decompress);
                using var sr = new StreamReader(gs);
                json = sr.ReadToEnd();
            }
            else if (File.Exists(jsonPath))
            {
                json = File.ReadAllText(jsonPath);
            }

            if (json == null) return null;

            using var doc = JsonDocument.Parse(json);
            var gameEl    = doc.RootElement.GetProperty("game");
            var id        = gameEl.GetProperty("id").GetInt64();
            var title     = gameEl.GetProperty("title").GetString();

            if (string.IsNullOrWhiteSpace(title)) return null;

            // The verdict's exe if there is one, else the game's page in the itch client.
            if (ItchLaunchUrl(gameDir, exeRelPath, id) is not { } launchUrl) return null;

            // The receipt embeds the same game object butler caches, so this path gets a cover too — the
            // JSON spells the fields camelCase where the DB columns are snake_case.
            string? cover = gameEl.TryGetProperty("stillCoverUrl", out var sc) ? sc.GetString() : null;
            if (string.IsNullOrWhiteSpace(cover))
                cover = gameEl.TryGetProperty("coverUrl", out var cu) ? cu.GetString() : null;

            return new InstalledGame(title, launchUrl, "itch.io")
                   { InstallDir = gameDir, SourceCoverUrl = cover?.Trim() };
        }
        catch { return null; }
    }

    // ── Ubisoft Connect (Uplay) ─────────────────────────────────────────────────
    // Installed games are listed in the registry under HKLM\...\Ubisoft\Launcher\Installs\<gameId>,
    // each with an InstallDir. Launch via the registered uplay://launch/<gameId>/0 URI (the same
    // scheme Playnite's Uplay plugin uses).
    //
    // NAMING: Connect's Installs key carries no title, and the install-FOLDER name ("RainbowSix" instead
    // of "Tom Clancy's Rainbow Six Siege") is also what would reach SteamGridDB — no cover or logo. The
    // real title is in the uninstall hive, where Connect registers each game with its InstallLocation and
    // a proper DisplayName; we match on the install path, so the key name doesn't matter.
    //
    // (Playnite instead parses Connect's protobuf+YAML configuration cache — richer, also yields official
    // art URLs, but needs two parsers over undocumented layouts. Left unbuilt deliberately; see the
    // Tier 3 notes.)

    private static IEnumerable<InstalledGame> ScanUbisoft()
    {
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titles   = UninstallTitlesByLocation();

        using (var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs")
                          ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs"))
        {
            if (root is not null)
                foreach (var gameId in root.GetSubKeyNames())
                {
                    var game = ReadUbisoftInstall(root, gameId, seenDirs, titles);
                    if (game != null) yield return game;
                }
        }

        // FALLBACK: legacy Ubisoft titles (verified case: the InnoSetup-era "Flashback (Demo)") never
        // register in Connect's Installs key — or anywhere else Connect-owned; their ONLY footprint is a
        // generic Windows uninstall entry. Sweep both uninstall hives for entries installed under a
        // \Ubisoft\ path (or published by Ubisoft) and launch their game exe directly (no Uplay id
        // exists to build a uplay:// URL from). Installs-sourced dirs are deduped via seenDirs, so a
        // properly-registered game never lists twice.
        foreach (var game in ScanUbisoftUninstallEntries(seenDirs)) yield return game;
    }

    private static InstalledGame? ReadUbisoftInstall(
        RegistryKey root, string gameId, HashSet<string> seenDirs, Dictionary<string, string> titles)
    {
        try
        {
            using var sub = root.OpenSubKey(gameId);
            if (sub?.GetValue("InstallDir") is not string dir || string.IsNullOrWhiteSpace(dir)) return null;

            var path = dir.Replace('/', '\\').TrimEnd('\\');   // registry stores forward slashes
            if (!Directory.Exists(path)) return null;          // skip stale/uninstalled entries
            seenDirs.Add(path);

            // Real title from the uninstall hive when this install path is registered there; the
            // install-folder name ("RainbowSix") only as a last resort.
            var name = titles.TryGetValue(NormalizeDirKey(path), out var t) && !string.IsNullOrWhiteSpace(t)
                ? t
                : new DirectoryInfo(path).Name;
            if (string.IsNullOrWhiteSpace(name)) return null;

            return new InstalledGame(name, $"uplay://launch/{gameId}/0", "Ubisoft") { InstallDir = path };
        }
        catch { return null; }
    }

    /// <summary>Install location → DisplayName for every uninstall entry that has both. Lets a scanner
    /// recover a game's REAL title from the path it already knows (see ScanUbisoft). Locations are keyed
    /// through <see cref="NormalizeDirKey"/> so separator/case/trailing-slash differences still match.</summary>
    private static Dictionary<string, string> UninstallTitlesByLocation()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var e in UninstallRegistry.Entries())
            {
                if (string.IsNullOrWhiteSpace(e.InstallLocation) || string.IsNullOrWhiteSpace(e.DisplayName))
                    continue;
                var key = NormalizeDirKey(e.InstallLocation!.Trim('"'));
                if (key.Length > 0) map.TryAdd(key, e.DisplayName!.Trim());
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Library] uninstall-title sweep failed: {ex.Message}"); }
        return map;
    }

    /// <summary>Canonical key for comparing two directory paths from different registry writers.</summary>
    private static string NormalizeDirKey(string dir) =>
        dir.Replace('/', '\\').TrimEnd('\\', ' ').ToLowerInvariant();

    private static IEnumerable<InstalledGame> ScanUbisoftUninstallEntries(HashSet<string> seenDirs)
    {
        // Both hives (HKLM and HKCU) and both registry views — see UninstallRegistry; HKLM alone misses
        // per-user Ubisoft installs.
        foreach (var entry in UninstallRegistry.Entries())
        {
            InstalledGame? game = null;
            try
            {
                var loc = entry.InstallLocation?.Trim('"').TrimEnd('\\');
                if (string.IsNullOrWhiteSpace(loc)) continue;
                var publisher = entry.Publisher ?? "";
                bool ubisoft = loc.Contains(@"\Ubisoft\", StringComparison.OrdinalIgnoreCase)
                               || publisher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase);
                if (!ubisoft) continue;
                // Not games: the launcher/Connect itself; Steam-managed installs belong to Steam.
                if (loc.Contains("Ubisoft Game Launcher", StringComparison.OrdinalIgnoreCase)) continue;
                if (loc.Contains("steamapps", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Directory.Exists(loc) || !seenDirs.Add(loc)) continue;

                // This source DOES carry a title — prefer it over the install-folder name.
                var folder = new DirectoryInfo(loc).Name;
                var name   = string.IsNullOrWhiteSpace(entry.DisplayName) ? folder : entry.DisplayName!.Trim();
                var exe    = FindGameExe(loc, folder);   // exe matching keys off the FOLDER name
                if (exe is null) continue;
                game = new InstalledGame(name, exe, "Ubisoft") { InstallDir = loc };   // no Uplay id → launch the exe directly
            }
            catch { /* skip unreadable entries */ }
            if (game != null) yield return game;
        }
    }

    /// <summary>Bounds on the <see cref="FindGameExe"/> sweep. This walks a whole game install — a
    /// third-party directory tree of unknown shape — on the way to opening the Game Grid, so it gets explicit
    /// limits rather than trusting the tree to be sane. Playnite guards the same class of hazard in its own
    /// recursive walks (<c>FileSystem.IsDirectorySubdirSafeToRecurse</c>), having hit a directory name that
    /// sent the traversal back to the parent and looped forever.</summary>
    private const int MaxGameExeDepth = 8;
    private const int MaxGameExeCandidates = 4000;

    /// <summary>Best-guess game executable under an install dir: prefer one whose filename appears in the
    /// game's folder name ("Flashback.exe" under "Flashback (Demo)"), else the largest exe — always
    /// skipping uninstallers/installers/redists/crash handlers. Null when no plausible exe exists.</summary>
    private static string? FindGameExe(string root, string name)
    {
        try
        {
            // The enumeration is deliberately fenced on three axes:
            //   • IgnoreInaccessible — one admin-ACL'd subfolder must cost only its own subtree, not abort
            //     the whole game via the method-level catch.
            //   • ReparsePoint skipped — .NET's recursive enumerator follows junctions and directory
            //     symlinks, and a game folder containing one that points at (or above) itself is an
            //     unbounded walk. Skipping them removes the loop at the source; a real game exe living
            //     ONLY behind a junction is a case we're happy to miss.
            //   • MaxRecursionDepth + a candidate cap — belt and braces for a pathological tree, and it
            //     keeps the cost of a grid open bounded no matter what's on disk.
            var exes = Directory.EnumerateFiles(root, "*.exe",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible    = true,
                        AttributesToSkip      = FileAttributes.Hidden | FileAttributes.System
                                                | FileAttributes.ReparsePoint,
                        MaxRecursionDepth     = MaxGameExeDepth,
                    })
                .Take(MaxGameExeCandidates)
                .Where(f =>
                {
                    var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    return !n.StartsWith("unins") && !n.Contains("setup") && !n.Contains("install")
                           && !n.Contains("redist") && !n.Contains("crash") && !n.Contains("uplay")
                           && !n.Contains("report") && !n.Contains("launcher");
                })
                .ToList();
            if (exes.Count == 0) return null;

            var folderKey = NormalizeName(name);
            var match = exes.FirstOrDefault(f =>
            {
                var stem = NormalizeName(Path.GetFileNameWithoutExtension(f));
                return stem.Length >= 4 && folderKey.Contains(stem);
            });
            return match ?? exes.OrderByDescending(f => new FileInfo(f).Length).First();
        }
        catch { return null; }
    }

    // ── Battle.net (Blizzard) ───────────────────────────────────────────────────
    // Battle.net has no per-game install manifest like Steam's .acf. The Agent's product.db
    // (%ProgramData%\Battle.net\Agent\product.db — a protobuf) is the authoritative installed list:
    // it embeds each product's install path. We pull the absolute paths out of the blob (path
    // strings survive protobuf field-number drift far better than a hand-rolled message decode),
    // then read each game's product code from the .product.db Blizzard drops in the install dir
    // (its leading token, e.g. "s1"), which doubles as the battlenet://<code> launch alias. The
    // install-folder name ("StarCraft") is the display name; the launcher/agent are filtered by code.

    private static IEnumerable<InstalledGame> ScanBattleNet()
    {
        var agentDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Battle.net", "Agent", "product.db");
        if (!File.Exists(agentDb)) yield break;

        HashSet<string> dirs;
        try { dirs = BattleNetInstallDirs(ReadBlobCapped(agentDb)); }
        catch { yield break; }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs)
        {
            var game = ReadBattleNetProduct(dir);
            if (game != null && seen.Add(game.LaunchUrl)) yield return game;
        }
    }

    /// <summary>Distinct existing directories referenced by absolute paths inside the Agent
    /// product.db blob (exe paths are mapped to their containing folder).</summary>
    private static HashSet<string> BattleNetInstallDirs(byte[] blob)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in AsciiRuns(blob, 4))
        {
            // Absolute Windows path anywhere in the run (runs may carry a leading protobuf byte
            // like '!' or a space, e.g. "!C:/Program Files (x86)/Battle.net").
            var m = Regex.Match(run, @"[A-Za-z]:[\\/].*$");
            if (!m.Success) continue;
            var path = m.Value.Replace('/', '\\').TrimEnd('\\', ' ');
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                path = Path.GetDirectoryName(path) ?? path;
            if (Directory.Exists(path)) dirs.Add(path);
        }
        return dirs;
    }

    // ── Battle.net has TWO id spaces, and they are NOT interchangeable ──────────
    //
    // Catalog ported from Playnite's Battle.net plugin (JosefNemec/PlayniteExtensions, BattleNetGames.cs,
    // MIT — see THIRD-PARTY-LICENSES.md). Never collapse the distinction:
    //
    //   • Uid  ("internal id") — what Blizzard writes on disk: the leading token of a game's .product.db,
    //     the "--uid=" in its uninstall string, and the "-launch -uid <x>" the client passes the game exe.
    //   • ProductId — the launch ALIAS: what "battlenet://<x>" and the client's own
    //     "--exec=launch <x>" expect, and what Playnite stores as its GameId.
    //
    // For most titles they're the same word (s1, s2, wow, w3, osi…), which hides the bug in testing. For
    // several they differ — Hearthstone is uid "hs_beta" / ProductId "WTCG", Overwatch "prometheus" /
    // "Pro", Diablo III "diablo3" / "D3" — and emitting battlenet://<uid> for those launches nothing,
    // misses the catalog lookup (keyed on ProductId, so the game falls back to an install-folder name),
    // and makes MatchKey disagree with the Playnite side (whose GameId is the ProductId), so Reconcile
    // drops the curated Playnite entry as a ghost.
    //
    // CLASSIC titles from their catalog (D2/D2X/W3C/W3CX) stay OMITTED: they aren't Agent-managed (never
    // appear in product.db) and launch by executable, not battlenet:// — outside this scanner's model.

    /// <summary>One Battle.net product: the on-disk uid, the launch-alias ProductId, and the canonical name.</summary>
    private readonly record struct BattleNetProduct(string Uid, string ProductId, string Name);

    private static readonly BattleNetProduct[] BattleNetCatalog =
    [
        // Uid (on disk)   ProductId (launch)  Canonical name
        new("wow",         "WoW",   "World of Warcraft"),
        new("diablo3",     "D3",    "Diablo III"),
        new("s2",          "S2",    "StarCraft II"),
        new("s1",          "S1",    "StarCraft"),
        new("hs_beta",     "WTCG",  "Hearthstone"),
        new("heroes",      "Hero",  "Heroes of the Storm"),
        new("prometheus",  "Pro",   "Overwatch 2"),
        new("viper",       "VIPR",  "Call of Duty: Black Ops 4"),
        new("odin",        "ODIN",  "Call of Duty: Modern Warfare"),
        new("w3",          "W3",    "Warcraft III: Reforged"),
        new("lazarus",     "LAZR",  "Call of Duty: Modern Warfare 2 Campaign Remastered"),
        new("zeus",        "ZEUS",  "Call of Duty: Black Ops Cold War"),
        new("wlby",        "WLBY",  "Crash Bandicoot 4"),
        new("osi",         "OSI",   "Diablo II: Resurrected"),
        new("rtro",        "RTRO",  "Blizzard Arcade Collection"),
        new("fore",        "FORE",  "Call of Duty: Vanguard"),
        new("anbs",        "ANBS",  "Diablo Immortal"),
        new("auks",        "AUKS",  "Call of Duty: Modern Warfare II"),
        new("Fen",         "Fen",   "Diablo IV"),
        new("D1",          "D1",    "Diablo"),
        new("w1r",         "W1R",   "Warcraft: Remastered"),
        new("w2r",         "W2R",   "Warcraft II: Remastered"),
        new("w1",          "W1",    "Warcraft: Orcs & Humans"),
        new("w2",          "W2",    "Warcraft II: Battle.net Edition"),
        // Titles newer than Playnite's catalog: no uid on record, so the ProductId doubles as the uid
        // (correct whenever they match, and the resolver falls through harmlessly when they don't).
        new("gry",         "GRY",   "Warcraft Rumble"),
        new("aris",        "ARIS",  "Doom: The Dark Ages"),
        new("scor",        "SCOR",  "Sea of Thieves"),
        new("ark",         "ARK",   "The Outer Worlds 2"),
        new("lbra",        "LBRA",  "Tony Hawk's Pro Skater 3 + 4"),
        new("pnta",        "PNTA",  "Call of Duty: Modern Warfare III"),
        new("aqua",        "AQUA",  "Avowed"),
    ];

    /// <summary>Look up a Battle.net product by EITHER of its ids (case-insensitive) — a code reaching us
    /// can be an on-disk uid (our own scan) or a ProductId (a Playnite GameId, or a stored slice URL from
    /// before this distinction existed). Null when the code is in neither space, in which case callers must
    /// pass it through unchanged rather than guessing.</summary>
    private static BattleNetProduct? BattleNetLookup(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        foreach (var p in BattleNetCatalog)
            if (p.Uid.Equals(code, StringComparison.OrdinalIgnoreCase)
             || p.ProductId.Equals(code, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    /// <summary>The launch ALIAS (ProductId) for a code in either id space — what battlenet:// URLs and the
    /// client's --exec=launch take. Unknown codes are returned unchanged (best effort: a product newer than
    /// the catalog is more likely to have uid == ProductId than to be helped by a guess).</summary>
    public static string BattleNetProductIdFor(string code) => BattleNetLookup(code)?.ProductId ?? code;

    /// <summary>The ON-DISK uid for a code in either id space — what <c>-launch -uid</c> takes, and what a
    /// game's <c>.product.db</c> holds. Unknown codes are returned unchanged.</summary>
    public static string BattleNetUidFor(string code) => BattleNetLookup(code)?.Uid ?? code;

    private static InstalledGame? ReadBattleNetProduct(string installDir)
    {
        try
        {
            var uid = BattleNetUid(installDir);
            if (string.IsNullOrWhiteSpace(uid)) return null;

            // The launcher/agent get a .product.db too — they're not games.
            if (uid.Equals("battle.net", StringComparison.OrdinalIgnoreCase)
                || uid.Equals("agent", StringComparison.OrdinalIgnoreCase)
                || uid.Equals("bts",   StringComparison.OrdinalIgnoreCase)) return null;

            // Canonical name + the LAUNCH-ALIAS id from the catalog. An unknown/new uid fails OPEN: the
            // install-folder name, and the uid used as its own alias (right whenever the two spaces agree,
            // which they do for most products).
            var product = BattleNetLookup(uid);
            var name    = product?.Name ?? new DirectoryInfo(installDir).Name;
            var alias   = product?.ProductId ?? uid;
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (product is null)
                Trace.WriteLine($"[Library] Battle.net: uid '{uid}' not in catalog — using folder name and uid-as-alias");

            return new InstalledGame(name, $"battlenet://{alias}", "Battle.net") { InstallDir = installDir };
        }
        catch { return null; }
    }

    /// <summary>What a Battle.net product code may contain. Shared with the LAUNCH seam
    /// (WindowsPlatformActions.LaunchBattleNet) so a code that reaches the client's command line is held
    /// to the same shape wherever it came from — including a code arriving via Playnite's GameId.</summary>
    public const string BattleNetCodePattern = "^[A-Za-z0-9._-]{2,32}$";

    /// <summary>A Battle.net install dir's ON-DISK uid — the leading token of its .product.db (e.g. "s1",
    /// "hs_beta"). This is the internal id, NOT the battlenet:// launch alias; see the catalog note above.
    /// Null if absent/unreadable.</summary>
    private static string? BattleNetUid(string installDir)
    {
        var pdb = Path.Combine(installDir, ".product.db");
        if (!File.Exists(pdb)) return null;
        try { return AsciiRuns(ReadBlobCapped(pdb), 2).FirstOrDefault(t => Regex.IsMatch(t, BattleNetCodePattern)); }
        catch { return null; }
    }

    /// <summary>Resolve how to launch an installed Battle.net product. <paramref name="code"/> may be in
    /// EITHER id space (a battlenet:// alias / Playnite GameId, or an on-disk uid) — it is resolved through
    /// the catalog before matching. Returns the game exe plus the ON-DISK UID, because the client runs games
    /// as "&lt;gameExe&gt; -launch -uid &lt;uid&gt;" (verified by capturing a live Playnite launch; Blizzard's
    /// own uninstall strings use the same "--uid=&lt;uid&gt;" spelling). The exe is named after the install
    /// folder, preferring the 64-bit build. Null lets callers fall back to the client's own
    /// "Battle.net.exe --exec=&quot;launch &lt;ProductId&gt;&quot;".</summary>
    /// <remarks>NOT all Blizzard titles launch identically — only StarCraft Remastered is verified
    /// end-to-end. The Playnite plugin distinguishes modern (client) vs CLASSIC executables (Diablo II,
    /// Warcraft III), which may take different args. The "&lt;folder&gt;.exe" heuristic also misses titles
    /// whose exe differs from the folder name (e.g. WoW → Wow.exe, Diablo III → "Diablo III64.exe");
    /// those return null and degrade to the --exec fallback. Revisit per-title if a launch fails.</remarks>
    public static (string Exe, string Uid)? BattleNetLaunchInfo(string code)
    {
        try
        {
            var agentDb = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Battle.net", "Agent", "product.db");
            if (!File.Exists(agentDb)) return null;

            // Accept either id space: compare the on-disk uid against BOTH the catalog's uid and its
            // ProductId for the code we were given.
            var wantUid   = BattleNetUidFor(code);
            var wantAlias = BattleNetProductIdFor(code);

            foreach (var dir in BattleNetInstallDirs(ReadBlobCapped(agentDb)))
            {
                var dirUid = BattleNetUid(dir);
                if (dirUid is null) continue;
                if (!dirUid.Equals(wantUid,   StringComparison.OrdinalIgnoreCase)
                 && !dirUid.Equals(wantAlias, StringComparison.OrdinalIgnoreCase)) continue;
                var name = new DirectoryInfo(dir).Name;   // game exe is named after the install folder
                foreach (var rel in new[] { $@"x86_64\{name}.exe", $@"x86\{name}.exe", $"{name}.exe" })
                {
                    var full = Path.Combine(dir, rel);
                    if (File.Exists(full)) return (full, dirUid);
                }
            }
        }
        catch { /* fall back to --exec */ }
        return null;
    }

    /// <summary>Cap on a Battle.net blob we string-scan. A real product.db is well under a megabyte; the
    /// file lives in %ProgramData% and is third-party, so reading it whole with no ceiling made its size
    /// our memory budget. Reading only the first slice is fine for a scan that just looks for install paths
    /// and product codes.</summary>
    private const int MaxBattleNetBlobBytes = 16 * 1024 * 1024;

    /// <summary>Read at most <see cref="MaxBattleNetBlobBytes"/> from a file. Opened with ReadWrite sharing:
    /// the Agent service keeps product.db open, and File.OpenRead's default (FileShare.Read) fails outright
    /// against a writer that hasn't granted it — see ReadTextShared for the same reasoning.</summary>
    private static byte[] ReadBlobCapped(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int len = (int)Math.Min(fs.Length, MaxBattleNetBlobBytes);
        var buf = new byte[len];
        fs.ReadExactly(buf, 0, len);
        return buf;
    }

    /// <summary>Longest ASCII run we'll accumulate. A run is a candidate path or product code — hundreds of
    /// chars at most — but a corrupt or mostly-printable product.db would otherwise build one StringBuilder
    /// spanning the whole file, i.e. ~2x its size in UTF-16 on top of the byte array already in memory.
    /// Over-long runs are truncated at this length rather than dropped, so a real path in a damaged file
    /// still has a chance of matching.</summary>
    private const int MaxAsciiRunLength = 4096;

    /// <summary>Printable-ASCII runs of at least <paramref name="minLen"/> chars from a binary blob —
    /// the cheap way to lift strings (codes, paths) out of Battle.net's protobuf files without a schema.</summary>
    private static IEnumerable<string> AsciiRuns(byte[] blob, int minLen)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in blob)
        {
            if (b >= 0x20 && b < 0x7f)
            {
                if (sb.Length < MaxAsciiRunLength) sb.Append((char)b);
                continue;
            }
            if (sb.Length >= minLen) yield return sb.ToString();
            sb.Clear();
        }
        if (sb.Length >= minLen) yield return sb.ToString();
    }

    // ── Amazon Games ────────────────────────────────────────────────────────────
    // The cheapest scanner of the lot: the client keeps its installed-games list in a plain SQLite file,
    // %LOCALAPPDATA%\Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite, one row per game in a table called
    // DbSet with Id / ProductTitle / InstallDirectory / Installed. Same file and query Playnite's Amazon
    // plugin uses (AmazonGamesLibrary.cs, MIT), and `Id` is the SAME id Playnite stores as its GameId — so
    // Amazon can be id-keyed in MatchKey, unlike EA.
    //
    // Launch is amazon-games://play/<id> (Playnite's play action). The client does NOT need to be running
    // for the URI to launch the game (verified on hardware, cold and warm), so no start-client-then-retry
    // dance is needed here (unlike Battle.net). LaunchStorefront's Amazon case is for the "open Amazon
    // Games" slice, which is a different action and does not feed this path.

    private static IEnumerable<InstalledGame> ScanAmazon()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Amazon Games", "Data", "Games", "Sql", "GameInstallInfo.sqlite");
        if (!File.Exists(dbPath)) yield break;

        foreach (var g in QueryAmazonDatabase(dbPath)) yield return g;
    }

    private static List<InstalledGame> QueryAmazonDatabase(string dbPath)
    {
        var results = new List<InstalledGame>();
        try
        {
            using var conn = OpenReadOnlySqlite(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, ProductTitle, InstallDirectory FROM DbSet WHERE Installed = 1";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id    = reader.IsDBNull(0) ? null : reader.GetString(0);
                var title = reader.IsDBNull(1) ? null : reader.GetString(1);
                var dir   = reader.IsDBNull(2) ? null : reader.GetString(2);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title)) continue;
                // The id lands in a URI; keep it to the shape an Amazon product id actually has.
                if (!Regex.IsMatch(id, @"^[A-Za-z0-9._:-]{2,128}$"))
                {
                    Trace.WriteLine($"[Library] Amazon: refusing malformed game id '{id}'");
                    continue;
                }
                // "Installed = 1" is the client's own flag and can outlive a folder deleted behind its back
                // — same ghost class as GOG's registry, same fails-safe check.
                if (PathVerifiablyGone(dir))
                {
                    Trace.WriteLine($"[Library] Amazon: stale entry (folder gone) skipped: {title} @ {dir}");
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) dir = null;

                results.Add(new InstalledGame(title.Trim(), $"amazon-games://play/{id}", "Amazon")
                    { InstallDir = dir });
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Library] Amazon scan failed: {ex.Message}"); }
        return results;
    }

    // ── EA (EA app, ex-Origin) ──────────────────────────────────────────────────
    // EA's own installed-games index (%ProgramData%\EA Desktop\<sha256>\IS) is ENCRYPTED with a
    // machine-derived key, so it's off the table. What IS plain is the per-game installer manifest EA
    // drops INSIDE each install: <installDir>\__Installer\installerdata.xml — a UTF-16 XML carrying the
    // game's <title> (per locale) and, crucially, its <contentID> list. Reading it from the install
    // folder makes presence self-proving: the manifest can't be a stale entry for an uninstalled game
    // the way a central index can (the failure mode Battle.net's product.db and Ubisoft's registry key
    // both have), so no dir-existence cross-check is needed beyond finding the file.
    //
    // Install roots come from EA's own configured download dir (machine.ini's
    // machine.downloadinplacedir, default C:\Program Files\EA Games\), plus the two default EA Games
    // folders, plus a sweep of the Windows uninstall hives for Electronic Arts entries — that last one
    // catches a game the user pointed at a different drive.
    //
    // Launch is origin2://game/launch/?offerIds=<all contentIDs, comma-separated>. The ids-are-the
    // -contentIDs detail is the load-bearing one and it is NOT guessable: passing a single platform id
    // (or the Origin.SFT.* software id from %ProgramData%\EA Desktop\InstallData) makes the EA app open
    // its own window and launch NOTHING. Big titles legitimately carry a dozen ids (Dead Space, BF4) and
    // want them ALL. Established by lutris/lutris#4996, where exactly this bug was diagnosed and fixed.

    /// <summary>Guard on the manifest read: real manifests run to ~1 MB (mostly per-locale &lt;exclude&gt;
    /// lists), so anything past this is corrupt or not what we think it is. Keeps one bad file from
    /// pulling tens of MB into memory during a grid open.</summary>
    private const long MaxEaManifestBytes = 8L * 1024 * 1024;

    private static IEnumerable<InstalledGame> ScanEa()
    {
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in EaInstallRoots())
        {
            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(root); }
            catch { continue; }                       // unreadable/missing root costs only itself
            foreach (var dir in subdirs)
            {
                var game = ReadEaGame(dir, seenDirs);
                if (game != null) yield return game;
            }
        }

        // Custom install locations: EA registers each game in the uninstall hive with its real
        // InstallLocation, so this reaches a game installed outside the configured root. Deduped by
        // directory against the root sweep above, so a normally-placed game never lists twice.
        foreach (var loc in EaUninstallLocations())
        {
            var game = ReadEaGame(loc, seenDirs);
            if (game != null) yield return game;
        }
    }

    /// <summary>Folders that directly contain EA game directories: EA's configured download dir first
    /// (the user can move it, and machine.ini is where the EA app records it), then the two defaults.</summary>
    private static IEnumerable<string> EaInstallRoots()
    {
        var roots = new List<string>();
        void AddRoot(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            var full = Environment.ExpandEnvironmentVariables(p!).Trim().TrimEnd('\\', '/');
            if (full.Length > 0 && Directory.Exists(full)
                && !roots.Contains(full, StringComparer.OrdinalIgnoreCase))
                roots.Add(full);
        }

        try
        {
            var ini = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EA Desktop", "machine.ini");
            if (File.Exists(ini))
                foreach (var line in File.ReadLines(ini))
                {
                    // machine.downloadinplacedir=C:\Program Files\EA Games\
                    var m = Regex.Match(line, @"^\s*machine\.downloadinplacedir\s*=\s*(.+?)\s*$",
                                        RegexOptions.IgnoreCase);
                    if (m.Success) { AddRoot(m.Groups[1].Value); break; }
                }
        }
        catch { /* fall through to the defaults */ }

        AddRoot(@"%ProgramFiles%\EA Games");
        AddRoot(@"%ProgramFiles(x86)%\EA Games");
        return roots;
    }

    /// <summary>InstallLocation of every Electronic Arts entry in the uninstall registry — all four passes
    /// (HKLM + HKCU × both views, see <see cref="UninstallRegistry"/>; HKLM alone misses per-user
    /// installs). Deliberately loose (publisher OR an \EA Games\ path): whether a game
    /// qualifies is decided by <see cref="ReadEaGame"/> finding an EA installer manifest, not by this
    /// filter.</summary>
    private static IEnumerable<string> EaUninstallLocations()
    {
        foreach (var entry in UninstallRegistry.Entries())
        {
            string? loc = null;
            try
            {
                loc = entry.InstallLocation?.Trim('"').TrimEnd('\\', '/');
                if (string.IsNullOrWhiteSpace(loc)) { loc = null; continue; }
                var publisher = entry.Publisher ?? "";
                bool ea = publisher.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase)
                          || loc.Contains(@"\EA Games\", StringComparison.OrdinalIgnoreCase);
                // A Steam-managed EA title belongs to Steam (its own scanner already has it).
                if (!ea || loc.Contains("steamapps", StringComparison.OrdinalIgnoreCase)) loc = null;
            }
            catch { loc = null; }
            if (loc != null) yield return loc;
        }
    }

    /// <summary>An EA game at <paramref name="dir"/>, or null if it isn't one (no installer manifest, no
    /// content ids, already seen). <paramref name="seenDirs"/> dedupes across the discovery sources.</summary>
    private static InstalledGame? ReadEaGame(string dir, HashSet<string> seenDirs)
    {
        try
        {
            var manifest = Path.Combine(dir, "__Installer", "installerdata.xml");
            if (!File.Exists(manifest)) return null;

            var path = dir.TrimEnd('\\', '/');
            if (!seenDirs.Add(path)) return null;

            var text = ReadEaManifest(manifest);
            if (text is null) return null;

            // Every contentID, in document order — see the section note: the EA app needs the whole set.
            var ids = Regex.Matches(text, @"<contentID>\s*([^<\s]+)\s*</contentID>")
                           .Select(m => m.Groups[1].Value)
                           .Distinct(StringComparer.Ordinal)
                           .ToList();
            if (ids.Count == 0)
            {
                Trace.WriteLine($"[Library] EA: no contentID in {manifest} — cannot build a launch URL, skipping");
                return null;
            }

            var name = EaTitle(text) ?? new DirectoryInfo(path).Name;
            if (string.IsNullOrWhiteSpace(name)) return null;

            var url = "origin2://game/launch/?offerIds=" + string.Join(",", ids);
            return new InstalledGame(name, url, "EA") { InstallDir = path };
        }
        catch { return null; }
    }

    /// <summary>The manifest as text, or null if it's absent/oversized/unreadable. It declares
    /// <c>encoding="utf-16"</c> and ships a BOM, so the framework's detection gets it right.</summary>
    private static string? ReadEaManifest(string manifest)
    {
        try
        {
            if (new FileInfo(manifest).Length > MaxEaManifestBytes)
            {
                Trace.WriteLine($"[Library] EA: manifest oversized, skipping {manifest}");
                return null;
            }
            // Shared read (the file declares utf-16 and ships a BOM, which StreamReader detects).
            return ReadTextShared(manifest);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Library] EA: manifest unreadable ({ex.Message}): {manifest}");
            return null;
        }
    }

    /// <summary>Display title from the manifest: the en_US locale block's if there is one (the manifest
    /// carries a &lt;title&gt; per shipped locale and English is what the art lookups key on), else the
    /// first title present. Trademark marks are stripped — EA writes "Bejeweled® 3", which would other-
    /// wise reach SteamGridDB verbatim and match nothing.</summary>
    private static string? EaTitle(string text)
    {
        var enUs = Regex.Match(text,
            @"<localeInfo\s+locale=""en_US""\s*>.*?<title>(.*?)</title>",
            RegexOptions.Singleline);
        var title = enUs.Success
            ? enUs.Groups[1].Value
            : Regex.Match(text, @"<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value;
        title = Regex.Replace(title, @"[®™©]", "").Trim();
        // Collapse the double space a stripped mark can leave ("Game ® 3" → "Game 3").
        title = Regex.Replace(title, @"\s{2,}", " ");
        return title.Length > 0 ? System.Net.WebUtility.HtmlDecode(title) : null;
    }
}
