using System.IO;
using System.Text.Json;
using LiteDB;

namespace ControllerWheel;

/// <summary>
/// Sources installed games from Playnite's library DB: unified name + local cover art + a single
/// launch URI (playnite://playnite/start/&lt;id&gt;, which routes to the right storefront).
///
/// Playnite opens games.db EXCLUSIVELY while it runs, so we can't read it then. We parse it
/// whenever it's readable (Playnite closed — e.g. at login) and cache the result to JSON; callers
/// get the cache when the DB is locked. Cover-art image files under library\files are never
/// locked, so art still displays from the cached paths. Returns null only when there's neither a
/// readable DB nor a cache, so callers can fall back to GameLibrary.Scan.
/// </summary>
public static class PlayniteLibrary
{
    /// <summary>Playnite library-plugin GUID → the storefront name its games carry. Every BUILT-IN plugin
    /// belongs here: a game whose PluginId we don't recognise gets <c>Storefront = ""</c>, and an unnamed
    /// storefront has no filter chip and — since storefront hiding moved to the Game Grid — no way to be
    /// hidden either. GUIDs read from JosefNemec/PlayniteExtensions (each plugin's own
    /// <c>Guid.Parse</c> in its <c>*Library.cs</c>, MIT).
    /// <para>Radiata does not live-scan Rockstar / Humble — their games arrive ONLY through
    /// Playnite. They also have no <see cref="LauncherCatalog"/> entry, so there's no "Open &lt;store&gt;"
    /// card to hold ☐ on — hiding those is per-game.</para></summary>
    private static readonly Dictionary<Guid, string> Stores = new()
    {
        [new("cb91dfc9-b977-43bf-8e70-55f46e410fab")] = "Steam",
        [new("aebe8b7c-6dc3-4a66-af31-e7375c6b5e9e")] = "GOG",
        [new("00000002-dbd1-46c6-b5d0-b1ba559d10e4")] = "Epic",
        [new("7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287")] = "Xbox",
        [new("85dd7072-2f20-4e76-a007-41035e390724")] = "EA",
        [new("c2f038e5-8b92-4877-91f1-da9094155fc5")] = "Ubisoft",
        [new("00000001-ebb2-4eec-abcb-7c89937a42bb")] = "itch.io",
        [new("e3c26a3d-d695-4cb7-a769-5ff7612c7edd")] = "Battle.net",  // Playnite's Battle.net library plugin
        [new("402674cd-4af6-4886-b6ec-0e695bfa0688")] = "Amazon",
        [new("88409022-088a-4de8-805a-fdbac291f00a")] = "Rockstar",
        [new("96e8c4bc-ec5c-4c8b-87e7-18ee5a690626")] = "Humble",
    };

    private static string LibraryDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "library");

    private static string CacheFile => Path.Combine(AppPaths.AppDataDir, "playnite-cache.json");

    /// <summary>Whether Playnite is installed on this machine AND has a library we can read.
    /// Callers gate on this so a machine without Playnite skips the read entirely and uses
    /// GameLibrary.Scan() directly — Playnite is an optional enhancement, never required.
    /// <para>⚠ <b>Both halves are required.</b> Playnite's uninstaller leaves the whole of
    /// <c>%APPDATA%\Playnite</c> behind, games.db included, so a games.db-only test reports a long-gone
    /// Playnite as present — onboarding offers Playnite artwork that can't arrive, Advanced hides its
    /// Install button, and the storefront launcher opens an exe that isn't there. The install probe alone
    /// is equally wrong: a fresh install has no library to read.</para></summary>
    public static bool IsAvailable => IsInstalled && File.Exists(Path.Combine(LibraryDir, "games.db"));

    private static bool? _installed;
    private static long  _installedAt;

    /// <summary>Whether a Playnite executable is actually on disk. Probed from the default per-user install
    /// location first, then the uninstall hives (a custom or all-users location), so a non-standard install
    /// still counts. Cached ~30 s like <see cref="LauncherCatalog"/>'s storefront probes — this is called
    /// per Game Grid open and per Settings render, and a registry sweep is not free.</summary>
    public static bool IsInstalled
    {
        get
        {
            if (_installed is { } cached && Environment.TickCount64 - _installedAt <= 30_000) return cached;
            bool present = ProbeInstalled();
            _installed   = present;
            _installedAt = Environment.TickCount64;
            return present;
        }
    }

    private static bool ProbeInstalled()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (HasPlayniteExe(Path.Combine(local, "Playnite"))) return true;
            return UninstallRegistry.Entries().Any(e =>
                e.DisplayName?.StartsWith("Playnite", StringComparison.OrdinalIgnoreCase) == true
                && HasPlayniteExe(e.InstallLocation?.Trim('"')));
        }
        catch { return false; }
    }

    private static bool HasPlayniteExe(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            return File.Exists(Path.Combine(dir, "Playnite.DesktopApp.exe"))
                || File.Exists(Path.Combine(dir, "Playnite.FullscreenApp.exe"));
        }
        catch { return false; }
    }

    /// <summary>Installed games from a fresh DB read (refreshing the cache) when possible,
    /// otherwise the cached snapshot, otherwise null.</summary>
    public static InstalledGame[]? GetInstalledGames()
    {
        var fresh = TryReadDb();
        if (fresh is not null) { SaveCache(fresh); return fresh; }
        return LoadCache();
    }

    private static InstalledGame[]? TryReadDb()
    {
        var dbPath = Path.Combine(LibraryDir, "games.db");
        if (!File.Exists(dbPath)) return null;

        // Unique name per call: the startup warm read and the first grid open can overlap, and a
        // fixed name would hit the copy the other call's LiteDatabase still holds open.
        var tmp = Path.Combine(Path.GetTempPath(), $"cw_playnite_{Path.GetRandomFileName()}.db");
        try
        {
            File.Copy(dbPath, tmp, overwrite: true);  // fails (and we fall back to cache) if Playnite has it open
            var filesDir = Path.Combine(LibraryDir, "files");
            var games = new List<InstalledGame>();

            using (var db = new LiteDatabase($"Filename={tmp}"))
            {
                foreach (var doc in db.GetCollection("Game").FindAll())
                try   // isolate each row: a single malformed document (e.g. a non-Guid _id/PluginId) must
                {     // not throw out of the loop and discard the ENTIRE installed-games list (→ stale cache).
                    // IsInstalled is the ONLY filter, deliberately: Playnite's "Hidden" flag is NOT
                    // honoured — Radiata owns its own grid visibility (hold ☐ in the grid to hide).
                    // Not a gap or a TODO: don't add a Hidden filter.
                    if (!doc.TryGetValue("IsInstalled", out var inst) || !inst.AsBoolean) continue;

                    var name = doc.TryGetValue("Name", out var n) && !n.IsNull ? n.AsString : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var id = doc["_id"].AsGuid;

                    string? cover = null;
                    if (doc.TryGetValue("CoverImage", out var ci) && !ci.IsNull && !string.IsNullOrWhiteSpace(ci.AsString))
                    {
                        // CoverImage is meant to be relative to Playnite's library\files. It's third-party
                        // data, though, and Path.Combine keeps a ROOTED second part verbatim (dropping the
                        // base) while "..\" walks out of it — so an entry could point anywhere on disk and we
                        // would cache and display that file. Only accept paths that stay inside filesDir.
                        var full = Path.GetFullPath(Path.Combine(filesDir, ci.AsString));
                        var root = Path.GetFullPath(filesDir).TrimEnd(Path.DirectorySeparatorChar)
                                   + Path.DirectorySeparatorChar;
                        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            System.Diagnostics.Trace.WriteLine(
                                $"[Playnite] ignoring CoverImage outside library\\files: {ci.AsString}");
                        else if (File.Exists(full)) cover = full;
                    }

                    var store = doc.TryGetValue("PluginId", out var pid) && !pid.IsNull
                                && Stores.TryGetValue(pid.AsGuid, out var s) ? s : "";

                    // Storefront-native id (Steam appid, GOG productId, …) → a direct launch URL
                    // for the assign-to-wheel feature, so slices don't have to route via Playnite.
                    var gameId = doc.TryGetValue("GameId", out var gid) && !gid.IsNull ? gid.AsString : null;

                    // Install folder — lets the merge dedupe a mis-named live-scan game (e.g. a stale itch
                    // receipt) against this curated entry for the SAME install (App.LoadGamesUnfiltered).
                    var installDir = doc.TryGetValue("InstallDirectory", out var idir) && !idir.IsNull ? idir.AsString : null;

                    games.Add(new InstalledGame(name, $"playnite://playnite/start/{id}", store)
                    {
                        CoverPath       = cover,
                        DirectLaunchUrl = DirectUrl(store, gameId),
                        InstallDir      = installDir,
                    });
                }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Playnite] skipped a malformed game row: {ex.Message}"); }
            }

            return games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch { return null; }
        finally { try { File.Delete(tmp); } catch { /* best-effort */ } }
    }

    /// <summary>Build a direct storefront launch URL from the Playnite-native game id, for the
    /// stores whose URL scheme is unambiguous from the id alone. Others return null (the slice
    /// then falls back to the playnite:// URL, which still launches correctly).</summary>
    private static string? DirectUrl(string store, string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return null;
        return store switch
        {
            "Steam"      => $"steam://rungameid/{gameId}",
            "GOG"        => $"goggalaxy://rungameid/{gameId}",
            "Battle.net" => $"battlenet://{gameId}",        // GameId is the product code (e.g. "s1")
            "Ubisoft"    => $"uplay://launch/{gameId}/0",   // GameId is the uplay numeric id (e.g. 16382)
            // GameId is the Amazon product id (amzn1.adg.product.…), the same value our own scan reads
            // from the client's GameInstallInfo.sqlite. The direct URL keeps Playnite out of the launch
            // and is what GameLibrary.MatchKey parses the id from to dedupe against the live scan.
            "Amazon"     => $"amazon-games://play/{gameId}",
            _            => null,
        };
    }

    private static void SaveCache(InstalledGame[] games)
    {
        // Unique temp + atomic move: File.WriteAllText straight to CacheFile is not atomic, and a
        // concurrent LoadCache (startup warm read vs first grid open) could read a torn file.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            AtomicFile.WriteAllText(CacheFile, System.Text.Json.JsonSerializer.Serialize(games));
        }
        catch { /* best-effort */ }
    }

    private static InstalledGame[]? LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            var games = System.Text.Json.JsonSerializer.Deserialize<InstalledGame[]>(File.ReadAllText(CacheFile));
            return games is { Length: > 0 } ? games : null;
        }
        catch { return null; }
    }
}
