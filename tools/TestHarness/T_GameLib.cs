using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The Game Grid — the parts that are decidable without owning the game.
///
/// The Battle.net case ("a title whose two ids differ") looks like it needs Hearthstone/Overwatch/D3
/// installed. It doesn't, quite: the whole fix is (a) the uid↔ProductId catalog and (b) ReadBattleNetProduct
/// choosing the ProductId for the URL and the catalog name over the folder name. A fabricated install dir —
/// a folder plus a <c>.product.db</c> whose leading ASCII token is the uid — drives the real scanner code
/// for every one of the seven titles. What that still does NOT prove is that the Battle.net client accepts
/// the URL; only a real install can say that.</summary>
internal static class T_GameLib
{
    private static Type GL => typeof(GameLibrary);

    public static void Run()
    {
        H.Group("Game library — Battle.net two-id fix, Playnite dedupe, FindGameExe bounds");

        BattleNetCatalog();
        BattleNetFixtures();
        MatchKeys();
        StorefrontInference();
        EditionDrift();
        Dedupe();
        RealPlayniteList();
        FindGameExeBounds();
        MachineRecord();
    }

    // ── the seven titles the checklist names ─────────────────────────────────────────────────────────
    private static readonly (string Uid, string Product, string Name)[] Divergent =
    {
        ("hs_beta",    "WTCG", "Hearthstone"),
        ("prometheus", "Pro",  "Overwatch 2"),
        ("diablo3",    "D3",   "Diablo III"),
        ("heroes",     "Hero", "Heroes of the Storm"),
        ("viper",      "VIPR", "Call of Duty: Black Ops 4"),
        ("odin",       "ODIN", "Call of Duty: Modern Warfare"),
        ("lazarus",    "LAZR", "Call of Duty: Modern Warfare 2 Campaign Remastered"),
    };

    private static void BattleNetCatalog()
    {
        foreach (var (uid, product, _) in Divergent)
        {
            H.Check($"Battle.net uid \"{uid}\" → launch alias \"{product}\"",
                    GameLibrary.BattleNetProductIdFor(uid) == product,
                    $"got \"{GameLibrary.BattleNetProductIdFor(uid)}\"");
            H.Check($"Battle.net alias \"{product}\" → on-disk uid \"{uid}\"",
                    GameLibrary.BattleNetUidFor(product) == uid,
                    $"got \"{GameLibrary.BattleNetUidFor(product)}\"");
        }
        // StarCraft is the one installed here and the one where the two ids agree — i.e. the reason this
        // class of bug survived. Worth asserting it still behaves.
        H.Check("StarCraft (uid == alias) unchanged", GameLibrary.BattleNetProductIdFor("s1") == "S1");
        // A code in neither space must pass through untouched, not be guessed at.
        H.Check("an unknown code passes through unchanged",
                GameLibrary.BattleNetProductIdFor("notaproduct") == "notaproduct");
    }

    private static void BattleNetFixtures()
    {
        var read = H.StaticMethod(GL, "ReadBattleNetProduct", 1);
        if (read is null) { H.Fail("ReadBattleNetProduct(installDir) not found", "renamed? scanner untested"); return; }

        var root = Path.Combine(Path.GetTempPath(), "radiata-harness", "bnet-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            foreach (var (uid, product, name) in Divergent)
            {
                // Deliberately folder-named the WRONG thing: the pre-fix code used this name.
                var dir = Path.Combine(root, uid == "hs_beta" ? "Hearthstone_Install" : uid.ToUpperInvariant() + "_Folder");
                Directory.CreateDirectory(dir);
                WriteProductDb(dir, uid);

                var g = (InstalledGame)read.Invoke(null, new object[] { dir });
                H.Check($"[{name}] scanned from a fixture install dir", g is not null);
                if (g is null) continue;
                H.Check($"[{name}] launch URL uses the ProductId, not the uid",
                        g.LaunchUrl == $"battlenet://{product}", $"got {g.LaunchUrl}");
                H.Check($"[{name}] name comes from the catalog, not the install folder",
                        g.Name == name, $"got \"{g.Name}\"");
            }

            // The client/agent get a .product.db too and must not become games.
            foreach (var notGame in new[] { "battle.net", "agent", "bts" })
            {
                var dir = Path.Combine(root, "svc-" + notGame.Replace('.', '-'));
                Directory.CreateDirectory(dir);
                WriteProductDb(dir, notGame);
                H.Check($"\"{notGame}\" is not treated as a game", read.Invoke(null, new object[] { dir }) is null);
            }

            // An unknown/new uid must fail OPEN — folder name, uid as its own alias, and say so.
            {
                var dir = Path.Combine(root, "Some New Blizzard Game");
                Directory.CreateDirectory(dir);
                WriteProductDb(dir, "zzz9");
                using var t = new H.TraceGrab();
                var g = (InstalledGame)read.Invoke(null, new object[] { dir });
                H.Check("an uncatalogued uid falls back to folder name + uid alias",
                        g is not null && g.Name == "Some New Blizzard Game" && g.LaunchUrl == "battlenet://zzz9",
                        g is null ? "null" : $"{g.Name} / {g.LaunchUrl}");
                H.Check("…and the fallback is traced", t.Saw("not in catalog"));
            }

            // No .product.db at all → not a game (this is how non-Blizzard folders under the root are rejected).
            {
                var dir = Path.Combine(root, "RandomFolder");
                Directory.CreateDirectory(dir);
                H.Check("a folder with no .product.db is skipped", read.Invoke(null, new object[] { dir }) is null);
            }
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>A stand-in .product.db: the scanner takes the first printable-ASCII run that matches
    /// <see cref="GameLibrary.BattleNetCodePattern"/>, so a real protobuf isn't needed — but the uid must be
    /// the FIRST such run, which is also true of the real file.</summary>
    private static void WriteProductDb(string dir, string uid)
    {
        var bytes = new List<byte> { 0x0A, (byte)uid.Length };
        bytes.AddRange(Encoding.ASCII.GetBytes(uid));
        bytes.AddRange(new byte[] { 0x00, 0x01, 0x12 });
        bytes.AddRange(Encoding.ASCII.GetBytes("enUS"));
        File.WriteAllBytes(Path.Combine(dir, ".product.db"), bytes.ToArray());
    }

    private static void MatchKeys()
    {
        // Our scan and the Playnite side must land on the SAME key for a divergent title — this is the half
        // of the bug that dropped the curated Playnite entry and re-added a folder-named copy.
        var ours = new InstalledGame("Diablo III", "battlenet://D3", "Battle.net") { InstallDir = @"C:\g\D3" };
        var pn   = new InstalledGame("Diablo III", "playnite://game/abc", "Battle.net")
                   { DirectLaunchUrl = "battlenet://D3", CoverPath = @"C:\art\d3.png" };
        H.Check("Battle.net: live scan and Playnite agree on MatchKey",
                GameLibrary.MatchKey(ours) == GameLibrary.MatchKey(pn),
                $"{GameLibrary.MatchKey(ours)} vs {GameLibrary.MatchKey(pn)}");

        // EA is name-keyed on purpose (our contentIDs and Playnite's Origin.OFR.* can never share a value).
        var ea1 = new InstalledGame("The Sims 4", "origin2://game/launch/?offerIds=1011164", "EA");
        var ea2 = new InstalledGame("The Sims 4", "playnite://game/xyz", "EA");
        H.Check("EA: name-keyed, so the two id spaces still match",
                GameLibrary.MatchKey(ea1) == GameLibrary.MatchKey(ea2),
                $"{GameLibrary.MatchKey(ea1)} vs {GameLibrary.MatchKey(ea2)}");

        // Different stores must never collide, whatever the name.
        var s1 = new InstalledGame("Bejeweled 3", "steam://rungameid/77120", "Steam");
        var e1 = new InstalledGame("Bejeweled 3", "origin2://game/launch/?offerIds=9", "EA");
        H.Check("same name, different store → different keys",
                GameLibrary.MatchKey(s1) != GameLibrary.MatchKey(e1));
    }

    /// <summary>StorefrontFromUrl must return exactly what the scanner that owns each store stamps on its
    /// games: GameArt keys the art cache and its ".miss" markers on Storefront + "_" + Name, so any drift
    /// gives one game two key sets and a slice re-downloads art the Game Grid already has.</summary>
    private static void StorefrontInference()
    {
        // Left side = a URL exactly as the named scanner builds it in GameLibrary/PlayniteLibrary.
        (string Url, string Store)[] cases =
        {
            ("steam://rungameid/77120",                                              "Steam"),
            ("goggalaxy://rungameid/1207658924",                                     "GOG"),
            ("com.epicgames.launcher://apps/ns:id:app?action=launch&silent=true",    "Epic"),
            ("battlenet://WTCG",                                                     "Battle.net"),
            ("uplay://launch/635/0",                                                 "Ubisoft"),
            ("origin2://game/launch/?offerIds=1011164",                              "EA"),
            ("amazon-games://play/amzn1.adg.product.abc",                            "Amazon"),
            ("itch://games/12345",                                                   "itch.io"),
            (@"shell:AppsFolder\Pub.App_8wekyb3d8bbwe!Game",                         "Xbox"),
        };
        foreach (var (url, store) in cases)
            H.Check($"storefront inferred from {url[..Math.Min(24, url.Length)]}… = {store}",
                    GameLibrary.StorefrontFromUrl(url) == store,
                    $"got '{GameLibrary.StorefrontFromUrl(url)}'");

        // A playnite:// URL routes to some real store we can't read off it, and a bare exe is nobody's
        // store — both must be the scanners' own unnamed-store value, never an invented one.
        foreach (var url in new[] { "playnite://playnite/start/abc", @"C:\Games\thing\thing.exe", "", null })
            H.Check($"unknown storefront for '{url ?? "(null)"}' is empty",
                    GameLibrary.StorefrontFromUrl(url) == "",
                    $"got '{GameLibrary.StorefrontFromUrl(url)}'");
    }

    /// <summary>NamesLikelySameGame — a bare substring match cannot tell an edition from a sequel. The
    /// asymmetry under test: an edition suffix is the same game, a sequel/expansion residue is not.</summary>
    private static void EditionDrift()
    {
        static bool Same(string a, string b) =>
            GameLibrary.NamesLikelySameGame(GameLibrary.NormalizeName(a), GameLibrary.NormalizeName(b));

        // The canonical counterexample, and the sequel family it stands for.
        H.Check("Portal vs Portal 2 are DIFFERENT games",          !Same("Portal", "Portal 2"));
        H.Check("Doom vs Doom Eternal are DIFFERENT games",        !Same("Doom", "Doom Eternal"));
        H.Check("The Witcher 3 vs its expansion residue differs",  !Same("The Witcher 3", "The Witcher 3 Wild Hunt"));

        // Edition drift — the case the guard exists FOR — must still collapse.
        H.Check("exact name matches",                              Same("Celeste", "celeste"));
        H.Check("Skyrim vs Skyrim Special Edition = same game",    Same("Skyrim", "Skyrim Special Edition"));
        H.Check("GOTY drift = same game",                          Same("Borderlands", "Borderlands Game of the Year Edition"));
        H.Check("Deluxe drift = same game, either direction",      Same("Cult of the Lamb Deluxe Edition", "Cult of the Lamb"));

        // Guard rails.
        H.Check("empty never matches anything",                    !Same("", "Portal"));
        H.Check("prefix + arbitrary residue is NOT drift",         !Same("Half-Life", "Half-Life Alyx"));
    }

    private static void Dedupe()
    {
        var dedupe = H.StaticMethod(GL, "DedupePlaynite", 1);
        if (dedupe is null) { H.Fail("DedupePlaynite(list) not found", "renamed? the fix is untested"); return; }

        IReadOnlyList<InstalledGame> D(params InstalledGame[] rows) =>
            (IReadOnlyList<InstalledGame>)dedupe.Invoke(null, new object[] { (IReadOnlyList<InstalledGame>)rows });

        // The exact shape found on this rig: two Playnite rows, same store, same name, same InstallDir.
        {
            using var t = new H.TraceGrab();
            var res = D(new InstalledGame("Bejeweled 3", "playnite://game/a", "EA") { InstallDir = @"C:\Program Files\EA Games\Bejeweled 3\" },
                        new InstalledGame("Bejeweled 3", "playnite://game/b", "EA") { InstallDir = @"C:\Program Files\EA Games\Bejeweled 3" });
            H.Check("two Playnite rows for one install collapse to one", res.Count == 1, $"{res.Count} rows");
            H.Check("…and the drop is traced", t.Saw("Playnite duplicate collapsed"));
        }

        // Trailing slash / case must not defeat the key.
        {
            var res = D(new InstalledGame("G", "playnite://game/a", "GOG") { InstallDir = @"C:\Games\G\" },
                        new InstalledGame("G", "playnite://game/b", "GOG") { InstallDir = @"c:\games\g" });
            H.Check("install-dir key is case- and trailing-slash-insensitive", res.Count == 1, $"{res.Count} rows");
        }

        // The illustrated row must win, whichever order they arrive in.
        {
            var res = D(new InstalledGame("G", "playnite://game/a", "EA") { InstallDir = @"C:\G" },
                        new InstalledGame("G", "playnite://game/b", "EA") { InstallDir = @"C:\G", CoverPath = @"C:\art\g.png" });
            H.Check("the row WITH cover art survives (arriving second)",
                    res.Count == 1 && res[0].CoverPath is not null, res.Count == 1 ? $"cover = {res[0].CoverPath}" : $"{res.Count} rows");
            res = D(new InstalledGame("G", "playnite://game/a", "EA") { InstallDir = @"C:\G", CoverPath = @"C:\art\g.png" },
                    new InstalledGame("G", "playnite://game/b", "EA") { InstallDir = @"C:\G" });
            H.Check("the row WITH cover art survives (arriving first)",
                    res.Count == 1 && res[0].CoverPath is not null);
        }

        // Storefront scoping: two stores recording the same folder must NOT merge.
        {
            var res = D(new InstalledGame("G", "playnite://game/a", "Steam") { InstallDir = @"C:\Shared" },
                        new InstalledGame("G", "playnite://game/b", "GOG")   { InstallDir = @"C:\Shared" });
            H.Check("same folder, two stores → NOT merged", res.Count == 2, $"{res.Count} rows");
        }

        // A ROM library: unrecognised plugins get Storefront = "", and every ROM of a system
        // reports the system's shared folder — the old dir key collapsed the whole library to one tile.
        {
            var res = D(new InstalledGame("Chrono Trigger", "playnite://game/a", "") { InstallDir = @"C:\roms\snes" },
                        new InstalledGame("Earthbound",     "playnite://game/b", "") { InstallDir = @"C:\roms\snes" },
                        new InstalledGame("Super Metroid",  "playnite://game/c", "") { InstallDir = @"C:\roms\snes" });
            H.Check("storefront-less rows sharing a folder (ROM library) stay distinct",
                    res.Count == 3, $"{res.Count} rows");
        }

        // Same store + same reported folder but INCOMPATIBLE names (a plugin reporting a shared library
        // dir): the directory alone must not merge them.
        {
            var res = D(new InstalledGame("Doom",         "playnite://game/a", "Steam") { InstallDir = @"C:\SteamLibrary" },
                        new InstalledGame("Doom Eternal", "playnite://game/b", "Steam") { InstallDir = @"C:\SteamLibrary" });
            H.Check("same store + folder, distinct names → NOT merged", res.Count == 2, $"{res.Count} rows");
        }

        // …while the same shape WITH compatible names (edition drift) still collapses — the Bejeweled
        // precedent this dedupe was built for survives the corroboration requirement.
        {
            var res = D(new InstalledGame("Skyrim",                 "playnite://game/a", "Steam") { InstallDir = @"C:\Games\Skyrim" },
                        new InstalledGame("Skyrim Special Edition", "playnite://game/b", "Steam") { InstallDir = @"C:\Games\Skyrim" });
            H.Check("same store + folder + edition-drift names → merged", res.Count == 1, $"{res.Count} rows");
        }

        // No InstallDir → falls back to MatchKey.
        {
            var res = D(new InstalledGame("Portal 2", "playnite://game/a", "Steam") { DirectLaunchUrl = "steam://rungameid/620" },
                        new InstalledGame("Portal 2", "playnite://game/b", "Steam") { DirectLaunchUrl = "steam://rungameid/620" });
            H.Check("no InstallDir → MatchKey collapses the pair", res.Count == 1, $"{res.Count} rows");
        }

        // Genuinely different games must survive untouched, in order.
        {
            var rows = new[]
            {
                new InstalledGame("A", "playnite://game/1", "Steam") { InstallDir = @"C:\A" },
                new InstalledGame("B", "playnite://game/2", "Steam") { InstallDir = @"C:\B" },
                new InstalledGame("C", "playnite://game/3", "GOG")   { InstallDir = @"C:\C" },
            };
            var res = D(rows);
            H.Check("distinct games are untouched and order is preserved",
                    res.Count == 3 && res.Select(r => r.Name).SequenceEqual(new[] { "A", "B", "C" }));
        }

        // Reconcile must run the dedupe BEFORE enrichment — otherwise both rows get the same DirectLaunchUrl
        // and become indistinguishable, which was the visible symptom.
        {
            // The install dir must really EXIST — Reconcile's first pass drops any Playnite row whose
            // recorded directory is missing (the ghost check), which would otherwise eat this fixture.
            var dir = Path.Combine(Path.GetTempPath(), "radiata-harness", "b3-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(dir);
            try
            {
                var playnite = new[]
                {
                    new InstalledGame("Bejeweled 3", "playnite://game/a", "EA") { InstallDir = dir },
                    new InstalledGame("Bejeweled 3", "playnite://game/b", "EA") { InstallDir = dir },
                };
                var live = new[] { new InstalledGame("Bejeweled 3", "origin2://game/launch/?offerIds=9", "EA") { InstallDir = dir } };
                var res = GameLibrary.Reconcile(playnite, live);
                H.Check("Reconcile emits ONE row for the duplicated install", res.Count == 1, $"{res.Count} rows");
                H.Check("…and it carries the direct launch URL",
                        res.Count == 1 && res[0].DirectLaunchUrl == live[0].LaunchUrl,
                        res.Count == 1 ? res[0].DirectLaunchUrl : null);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        // Ghost filter: an install dir that's gone (drive present) is dropped.
        {
            var gone = Path.Combine(Path.GetTempPath(), "radiata-harness-not-here-" + Guid.NewGuid().ToString("N")[..6]);
            using var t = new H.TraceGrab();
            var res = GameLibrary.Reconcile(
                new[] { new InstalledGame("Ghost", "playnite://game/g", "itch") { InstallDir = gone } },
                Array.Empty<InstalledGame>());
            H.Check("a Playnite row whose install dir is gone is dropped as a ghost", res.Count == 0, $"{res.Count} rows");
            H.Check("…and the drop is traced", t.Saw("ghost dropped"));
        }
    }

    private static void RealPlayniteList()
    {
        var dedupe = H.StaticMethod(GL, "DedupePlaynite", 1);
        if (dedupe is null) return;
        if (!PlayniteLibrary.IsAvailable) { H.Skip("real Playnite list replay", "games.db not present"); return; }

        var games = PlayniteLibrary.GetInstalledGames();
        if (games is null || games.Length == 0) { H.Skip("real Playnite list replay", "Playnite returned nothing (running? DB locked?)"); return; }

        using var t = new H.TraceGrab();
        var res = (IReadOnlyList<InstalledGame>)dedupe.Invoke(null, new object[] { (IReadOnlyList<InstalledGame>)games });
        int collapsed = games.Length - res.Count;
        Console.WriteLine($"        (record) real Playnite list: {games.Length} rows → {res.Count} after dedupe ({collapsed} collapsed)");
        foreach (var l in t.Lines.Where(l => l.Contains("collapsed"))) Console.WriteLine("        " + l);
        foreach (var grp in games.GroupBy(g => g.Storefront).OrderBy(g => g.Key))
            Console.WriteLine($"        (record) [{(grp.Key.Length == 0 ? "<unnamed>" : grp.Key)}] {grp.Count()}");
        H.Check("dedupe never invents or loses a store", res.Select(r => r.Storefront).Distinct().Count()
                                                          <= games.Select(r => r.Storefront).Distinct().Count());
    }

    private static void FindGameExeBounds()
    {
        var find = H.StaticMethod(GL, "FindGameExe", 2);
        if (find is null) { H.Fail("FindGameExe(root, name) not found", "renamed? the junction guard is untested"); return; }

        var root = Path.Combine(Path.GetTempPath(), "radiata-harness", "tree-" + Guid.NewGuid().ToString("N")[..6]);
        var game = Path.Combine(root, "Rainbow Six Siege");
        try
        {
            Directory.CreateDirectory(game);

            // The exe we want found, plus every excluded shape.
            File.WriteAllBytes(Path.Combine(game, "RainbowSix.exe"), new byte[2048]);
            foreach (var decoy in new[] { "unins000.exe", "vc_redist.x64.exe", "setup.exe", "CrashReporter.exe",
                                          "UplayInstaller.exe", "GameLauncher.exe", "DXSETUP.exe" })
                File.WriteAllBytes(Path.Combine(game, decoy), new byte[200_000]);   // all BIGGER than the real one

            // A deep tree (well past MaxRecursionDepth) with an exe at the bottom.
            var deep = game;
            for (int i = 0; i < 20; i++) { deep = Path.Combine(deep, "d" + i); Directory.CreateDirectory(deep); }
            File.WriteAllBytes(Path.Combine(deep, "TooDeep.exe"), new byte[5_000_000]);

            // Lots of siblings, to exercise the candidate cap.
            var many = Path.Combine(game, "many");
            Directory.CreateDirectory(many);
            for (int i = 0; i < 5000; i++) File.WriteAllBytes(Path.Combine(many, $"m{i}.exe"), new byte[64]);

            // A junction pointing back at the root: the unbounded-walk shape.
            var loop = Path.Combine(game, "loop");
            var mk = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{loop}\" \"{root}\"")
                     { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
            mk.WaitForExit(10_000);
            bool junction = Directory.Exists(loop);
            if (!junction) H.Skip("junction created", "mklink /J failed — the loop case is not covered");

            var sw = Stopwatch.StartNew();
            var exe = (string)find.Invoke(null, new object[] { game, "Rainbow Six Siege" });
            sw.Stop();

            H.Check("FindGameExe returns promptly on a pathological tree", sw.ElapsedMilliseconds < 15_000,
                    $"{sw.ElapsedMilliseconds} ms{(junction ? " (with a junction loop present)" : "")}");
            H.Check("FindGameExe found an exe", exe is not null, exe);
            H.Check("…and it's the folder-name match, not the biggest decoy",
                    exe is not null && Path.GetFileName(exe).Equals("RainbowSix.exe", StringComparison.OrdinalIgnoreCase),
                    exe is null ? null : Path.GetFileName(exe));

            // With the real exe removed it must fall back to the largest NON-excluded exe — and never to an
            // uninstaller/redist/launcher, however big.
            File.Delete(Path.Combine(game, "RainbowSix.exe"));
            var fallback = (string)find.Invoke(null, new object[] { game, "Rainbow Six Siege" });
            var name = fallback is null ? "" : Path.GetFileNameWithoutExtension(fallback).ToLowerInvariant();
            H.Check("fallback never picks an uninstaller / redist / launcher / crash handler",
                    fallback is null || !(name.StartsWith("unins") || name.Contains("setup") || name.Contains("install")
                                          || name.Contains("redist") || name.Contains("crash") || name.Contains("uplay")
                                          || name.Contains("report") || name.Contains("launcher")),
                    fallback is null ? "null" : Path.GetFileName(fallback));
            H.Check("fallback doesn't reach past the depth cap",
                    fallback is null || !Path.GetFileName(fallback).Equals("TooDeep.exe", StringComparison.OrdinalIgnoreCase),
                    fallback is null ? "null" : Path.GetFileName(fallback));
        }
        finally
        {
            // Remove the junction first, or the recursive delete follows it.
            try { Directory.Delete(Path.Combine(game, "loop")); } catch { }
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void MachineRecord()
    {
        var launchers = GameLibrary.InstalledLaunchers();
        var scannable = GameLibrary.ScannableStores();
        Console.WriteLine($"        (record) launchers detected: {string.Join(", ", launchers.OrderBy(x => x))}");
        Console.WriteLine($"        (record) scannable stores:   {string.Join(", ", scannable.OrderBy(x => x))}");

        // Capture the scanners' own trace: a scanner that fails is caught per-store and only says so via
        // Trace, so without this an absent store reads as "no games installed" — which is how a harness-only
        // problem (a missing native dependency, say) could be misreported as a product finding.
        using var st = new H.TraceGrab();
        var live = GameLibrary.Scan();
        var failures = st.Lines.Where(l => l.Contains("scan failed")).ToList();
        H.Check("no storefront scanner failed in this process", failures.Count == 0,
                failures.Count == 0 ? null : string.Join(" | ", failures));
        foreach (var l in st.Lines.Where(l => l.Contains("[Library]"))) Console.WriteLine("        " + l);
        Console.WriteLine($"        (record) live scan: {live.Length} games");
        foreach (var grp in live.GroupBy(g => g.Storefront).OrderBy(g => g.Key))
            Console.WriteLine($"        (record)   [{grp.Key}] {grp.Count()}");

        // The checklist's open storefront items are all about NAMES and URLS for specific stores, so print
        // them: Ubisoft (real title vs install-folder name), Amazon (the play URI), Battle.net, EA, itch.
        foreach (var g in live.Where(g => g.Storefront is "Ubisoft" or "Amazon" or "Battle.net" or "EA" or "itch.io"))
            Console.WriteLine($"        (record)   [{g.Storefront}] \"{g.Name}\" → {g.LaunchUrl}"
                              + (g.InstallDir is null ? "" : $"  @ {g.InstallDir}"));

        // itch is the one store whose games have NO other art source (Steam CDN is Steam-gated, Playnite's
        // covers need Playnite installed), so its cover_url is the whole art path — record the coverage and
        // fail only on a URL that isn't fetchable art.
        var itch = live.Where(g => g.Storefront == "itch.io").ToList();
        if (itch.Count > 0)
        {
            var withCover = itch.Where(g => !string.IsNullOrWhiteSpace(g.SourceCoverUrl)).ToList();
            Console.WriteLine($"        (record) itch covers from butler.db: {withCover.Count}/{itch.Count}");
            foreach (var g in withCover) Console.WriteLine($"        (record)   \"{g.Name}\" → {g.SourceCoverUrl}");
            H.Check("every itch cover URL is an https image URL",
                    withCover.All(g => g.SourceCoverUrl!.StartsWith("https://", StringComparison.OrdinalIgnoreCase)),
                    string.Join(" | ", withCover.Select(g => g.SourceCoverUrl).Where(u => !u!.StartsWith("https://"))));
        }

        // Every live entry must carry a launchable URL and a name — the two things a tile needs.
        H.Check("every scanned game has a non-empty name and launch URL",
                live.All(g => !string.IsNullOrWhiteSpace(g.Name) && !string.IsNullOrWhiteSpace(g.LaunchUrl)));
        H.Check("live scan has no duplicate launch URLs",
                live.Select(g => g.LaunchUrl).Distinct(StringComparer.OrdinalIgnoreCase).Count() == live.Length);

        // A soundtrack would arrive as its own Steam entry; none is installed here, so this only records.
        var suspicious = live.Where(g => g.Name.Contains("Soundtrack", StringComparison.OrdinalIgnoreCase)
                                      || g.Name.Contains("OST", StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"        (record) soundtrack-looking entries in the live scan: {suspicious.Count}");

        InstallDirSeparators(live);
    }

    /// <summary>Every InstallDir must be a canonical Windows path, because two features PREFIX-MATCH it
    /// against a real image path from Win32 (`DetectRunningGameName` for Text Chat, `AppVolumeMixer` for
    /// per-app volume) with an ordinal compare.
    ///
    /// <para>Guards a silent, wide-blast-radius trap: Steam writes its HKCU
    /// <c>SteamPath</c> with FORWARD slashes, <c>Path.Combine</c> joins with a backslash without normalising
    /// what it was given, so every Steam InstallDir would come out mixed
    /// (<c>c:/program files (x86)/steam\steamapps\common\…</c>). A '/' never equals a '\' under an ordinal
    /// compare, so both features would silently see zero Steam games and Text Chat
    /// would refuse with "no installed game in the foreground", which reads as "you aren't in a game".</para>
    ///
    /// <para>⚠ The existing `T_TextChat` checks cannot catch this class: they drive a STUB
    /// <c>IPlatformActions</c>, so they never touch a real path. Only a check against the live scan can.</para></summary>
    private static void InstallDirSeparators(InstalledGame[] live)
    {
        var withDirs = live.Where(g => !string.IsNullOrWhiteSpace(g.InstallDir)).ToArray();
        if (withDirs.Length == 0) { H.Skip("InstallDir separators", "no scanned game reported an install dir"); return; }

        var forward = withDirs.Where(g => g.InstallDir!.Contains('/')).ToList();
        H.Check("no InstallDir contains a forward slash", forward.Count == 0,
                forward.Count == 0
                    ? $"{withDirs.Length} install dir(s), all canonical"
                    : string.Join("; ", forward.Take(4).Select(g => $"[{g.Storefront}] {g.Name} → {g.InstallDir}"))
                      + "  → normalise in GameLibrary.NormalizeDir; a prefix match against a Win32 path "
                      + "will silently never hit");

        // The prefix match also assumes the dir is rooted and absolute — a relative one would match nothing
        // (or, worse, everything under a coincidental prefix).
        var unrooted = withDirs.Where(g => !Path.IsPathRooted(g.InstallDir!)).ToList();
        H.Check("every InstallDir is an absolute path", unrooted.Count == 0,
                unrooted.Count == 0 ? null
                    : string.Join("; ", unrooted.Take(4).Select(g => $"[{g.Storefront}] {g.Name} → {g.InstallDir}")));

        // The normaliser itself, against the exact shape Steam's registry hands us.
        var norm = H.StaticMethod(typeof(GameLibrary), "NormalizeDir", 1);
        if (norm is null) { H.Fail("GameLibrary.NormalizeDir(dir) not found", "renamed? the fix is untested"); return; }
        string got = (string)norm.Invoke(null, ["c:/program files (x86)/steam"]);
        H.Check("NormalizeDir fixes Steam's forward-slash registry value",
                !got.Contains('/') && got.EndsWith("steam", StringComparison.OrdinalIgnoreCase), got);
        H.Check("NormalizeDir drops a trailing separator",
                !((string)norm.Invoke(null, [@"C:\Games\Thing\"])).EndsWith('\\'));
    }
}
