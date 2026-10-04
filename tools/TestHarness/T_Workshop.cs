using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The Workshop's sample packages (packaging\sample-*-package) and the claims the Workshop guide makes
/// about the script sandbox (packaging\webhost\workshop\, the Arcade Games README, the custom-arcade-games Help
/// topic).
///
/// A sample passes only through <see cref="PackageStore.Inspect"/> — the scanner a user's install runs — and a
/// sample game only by PLAYING it: the real <c>Radiata.ArcadeHost.exe</c> out of the Debug output, spoken to
/// over its own pipe protocol, every frame's buffer run through the host's <see cref="ScriptDrawValidator"/>.
/// The helper runs unjailed here (no AppContainer, no Job Object); the jail is ArcadePoc's subject, not this
/// group's. The downloadable zips on the site must hold exactly the sample folders' files, at the zip root, so
/// Windows' Extract All yields a loadable folder named after the zip.
///
/// <para>Everything that reads the sample folders or the site's zips runs only in the development repository
/// (<see cref="H.DevRepoOnly"/>): the public snapshot carries neither. The sandbox claims need only the built
/// helper and run everywhere.</para></summary>
internal static class T_Workshop
{
    public static void Run()
    {
        H.Group("Workshop — sample packages load and play; the guide's sandbox claims hold");
        var root = H.RepoRoot();
        if (root is null) { H.Fail("repo root not found"); return; }
        var helper = Path.Combine(root, "bin", "Debug", "net8.0-windows", "Radiata.ArcadeHost.exe");
        var zips = Path.Combine(root, "packaging", "webhost", "workshop", "samples");

        SamplePackages(root, helper, zips);
        CabinetFields(root);
        DropInstall(root, zips);
        GuideClaims(helper);
    }

    /// <summary>The sample packages scan clean, the sample game plays, and the site's download zips match the
    /// sample folders. Development repository only: the public snapshot carries neither the samples nor the
    /// site docroot that holds the zips.</summary>
    private static void SamplePackages(string root, string helper, string zips)
    {
        if (!H.DevRepoOnly("Workshop sample packages: scan, play, and the site's download zips", root)) return;

        // The samples the site offers for download. A sample added or retired changes this list, the zips
        // (tools\build-workshop-samples.ps1) and the download cards on all five /workshop/ pages together.
        foreach (var (kind, folder, expected) in new[] { ("material", "sample-material-package", "ember,starter"),
                                                         ("arcade", "sample-arcade-package", "firefly") })
        {
            var dirs = Directory.GetDirectories(Path.Combine(root, "packaging", folder));
            var names = string.Join(",", dirs.Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
            H.Check($"{kind}: the samples are exactly {expected}", names == expected, names);
            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                var d = PackageStore.Inspect(dir, kind);
                bool ok = d.Error is null && (kind == "material" ? d.Material is not null : d.ArcadeGame is not null);
                H.Check($"{kind} sample '{name}' passes the package scan", ok, d.Error);
                ZipMatches(Path.Combine(zips, name + ".zip"), dir, name);
                if (ok && kind == "arcade") Play(helper, name, File.ReadAllText(Path.Combine(dir, d.ArcadeGame.EntryFile)));
            }
        }

        var zipNames = Directory.Exists(zips) ? Directory.GetFiles(zips, "*.zip").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal) : Enumerable.Empty<string>();
        H.Check("site: no download zip without a sample folder", string.Join(",", zipNames) == "ember,firefly,starter", string.Join(",", zipNames));
    }

    // ── Drag-and-drop install (PackageInstaller): staging, ZIP rules, caps, commit ──────────────

    /// <summary>docs/PACKAGES.md ▸ Installing and removing. Runs against the harness's isolated app-data
    /// folder, so the Packages folders here are throwaway. Development repository only: it installs the
    /// sample packages and their site zips, which the public snapshot does not carry.</summary>
    private static void DropInstall(string root, string zips)
    {
        if (!H.DevRepoOnly("drag-and-drop install of the sample packages (PackageInstaller)", root)) return;

        static bool Any(string kind) => true;
        var work = Path.Combine(Path.GetTempPath(), "radiata-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var starterDir = Path.Combine(root, "packaging", "sample-material-package", "starter");
        var starterJson = File.ReadAllText(Path.Combine(starterDir, "material.json"));
        try
        {
            H.Check("drop: a folder is a candidate", PackageInstaller.IsCandidate(starterDir));
            H.Check("drop: a .zip is a candidate", PackageInstaller.IsCandidate(Path.Combine(zips, "starter.zip")));
            H.Check("drop: a loose .json is not", !PackageInstaller.IsCandidate(Path.Combine(starterDir, "material.json")));

            // Every downloadable sample installs, from its folder and from its zip.
            foreach (var (kind, folder) in new[] { ("material", "sample-material-package"), ("arcade", "sample-arcade-package") })
                foreach (var dir in Directory.GetDirectories(Path.Combine(root, "packaging", folder)))
                {
                    var name = Path.GetFileName(dir);
                    var fromFolder = PackageInstaller.Stage(dir, Any, out var e1);
                    H.Check($"drop: sample folder '{name}' stages as {kind}", fromFolder is { } s1 && s1.Kind == kind && s1.FolderName == name, e1);
                    if (fromFolder is not null) PackageInstaller.Discard(fromFolder);
                    var fromZip = PackageInstaller.Stage(Path.Combine(zips, name + ".zip"), Any, out var e2);
                    H.Check($"drop: sample zip '{name}.zip' stages as {kind}, named after the zip",
                        fromZip is { } s2 && s2.Kind == kind && s2.FolderName == name, e2);
                    if (fromZip is not null) PackageInstaller.Discard(fromZip);
                }

            string Zip(string name, params (string Entry, byte[] Bytes)[] entries)
            {
                var path = Path.Combine(work, name + ".zip");
                using var z = ZipFile.Open(path, ZipArchiveMode.Create);
                foreach (var (entry, bytes) in entries)
                    using (var s = z.CreateEntry(entry).Open()) s.Write(bytes);
                return path;
            }
            var manifest = System.Text.Encoding.UTF8.GetBytes(starterJson);

            var nested = PackageInstaller.Stage(Zip("wrapped", ("Lava/material.json", manifest), ("Lava/deep/x.txt", [1])), Any, out var e3);
            H.Check("drop: a zip holding one top folder installs as that folder", nested?.FolderName == "Lava", e3);
            H.Check("drop: …and nothing deeper than the package's top level is copied",
                nested is not null && !Directory.Exists(Path.Combine(nested.StageRoot, "Lava", "deep")));
            if (nested is not null) PackageInstaller.Discard(nested);

            H.Check("drop: a zip holding two top folders is refused",
                PackageInstaller.Stage(Zip("two", ("A/material.json", manifest), ("B/material.json", manifest)), Any, out var e4) is null
                && e4!.Contains("more than one folder"), e4);
            H.Check("drop: a package with both manifests is refused",
                PackageInstaller.Stage(Zip("both", ("material.json", manifest), ("game.json", [1])), Any, out var e5) is null
                && e5!.Contains("both"), e5);
            H.Check("drop: a zip with no manifest is refused",
                PackageInstaller.Stage(Zip("none", ("readme.txt", [1])), Any, out var e6) is null
                && e6!.Contains("isn't a Radiata package"), e6);
            H.Check("drop: a kind the build doesn't offer is refused",
                PackageInstaller.Stage(starterDir, k => k != "material", out var e7) is null && e7!.Contains("doesn't offer"), e7);

            // Hostile names: traversal never lands outside staging; an alternate-data-stream name is refused.
            var escaped = Path.Combine(PackageStore.PackagesDir, "escaped.txt");
            var trav = PackageInstaller.Stage(Zip("trav", ("material.json", manifest), ("../escaped.txt", [1]),
                                                         ("..\\..\\escaped.txt", [1]), ("/escaped.txt", [1])), Any, out var e8);
            H.Check("drop: traversal entries never write outside staging",
                !File.Exists(escaped) && !File.Exists(Path.Combine(PackageInstaller.StagingDir, "escaped.txt"))
                && !File.Exists(Path.Combine(work, "escaped.txt")), e8);
            if (trav is not null) PackageInstaller.Discard(trav);
            H.Check("drop: a colon (stream) entry name is refused",
                PackageInstaller.Stage(Zip("ads", ("material.json", manifest), ("material.json:evil", [1])), Any, out var e9) is null
                && e9!.Contains("unsafe name"), e9);

            // Caps on the bytes that actually arrive: 5 MB of zeros compresses to almost nothing.
            H.Check("drop: a file that inflates past 4 MB is refused",
                PackageInstaller.Stage(Zip("bomb", ("material.json", manifest), ("big.png", new byte[5 * 1024 * 1024])), Any, out var e10) is null
                && e10!.Contains("over 4 MB"), e10);
            var many = new List<(string, byte[])> { ("material.json", manifest) };
            for (int i = 0; i < 32; i++) many.Add(($"f{i}.txt", [1]));
            H.Check("drop: over 32 files is refused",
                PackageInstaller.Stage(Zip("many", [.. many]), Any, out var e11) is null && e11!.Contains("over 32"), e11);

            // A source already inside Packages is refused (dragging an installed package onto Settings).
            Directory.CreateDirectory(PackageStore.MaterialsDir);
            var inside = Path.Combine(PackageStore.MaterialsDir, "already");
            Directory.CreateDirectory(inside);
            File.WriteAllText(Path.Combine(inside, "material.json"), starterJson);
            H.Check("drop: a folder already inside Packages is refused",
                PackageInstaller.Stage(inside, Any, out var e12) is null && e12!.Contains("already inside"), e12);
            Directory.Delete(inside, recursive: true);

            // Commit, then the same package again reports the installed folder as its conflict.
            var first = PackageInstaller.Stage(starterDir, Any, out var e13);
            var installed = first is null ? null : PackageInstaller.Commit(first, out e13);
            H.Check("drop: commit moves the package into the Materials folder and it inspects clean",
                installed is { Error: null, Material: not null } && Path.GetDirectoryName(installed.FolderPath) == PackageStore.MaterialsDir, e13);
            H.Check("drop: the committed folder passes the Recycle-Bin guard",
                installed is not null && PackageStore.IsPackageDir(installed.FolderPath));
            var again = PackageInstaller.Stage(starterDir, Any, out _);
            H.Check("drop: the same package again conflicts with the installed folder",
                again is not null && installed is not null && PackageInstaller.FindConflicts(again, [installed]) is [var only] && only == installed.FolderPath);
            H.Check("drop: the content hash is the same staged and installed (approval carries over)",
                again is not null && installed is not null && again.Package.ContentHash == installed.ContentHash);
            if (again is not null) PackageInstaller.Discard(again);

            // The same id under a different folder name (a replace dropped as "ember2") is still a conflict,
            // found from a scan of the disk; a second replaced copy on disk is reported too.
            var renamed = Path.Combine(work, "starter2");
            Directory.CreateDirectory(renamed);
            foreach (var f in Directory.GetFiles(starterDir)) File.Copy(f, Path.Combine(renamed, Path.GetFileName(f)));
            var second = PackageInstaller.Stage(renamed, Any, out var e14);
            H.Check("drop: the same id under another folder name conflicts with the installed folder (disk scan)",
                second is not null && installed is not null
                && PackageInstaller.FindConflicts(second, PackageStore.Scan()) is [var viaScan] && viaScan == installed.FolderPath, e14);
            var twin = second is null ? null : PackageInstaller.Commit(second, out _);
            var third = PackageInstaller.Stage(starterDir, Any, out _);
            H.Check("drop: every installed folder of the id is a conflict, so a replace leaves one",
                twin is not null && third is not null && PackageInstaller.FindConflicts(third, PackageStore.Scan()).Count == 2);
            if (third is not null) PackageInstaller.Discard(third);
            if (twin is not null) try { Directory.Delete(twin.FolderPath, recursive: true); } catch { }
            H.Check("drop: the Recycle-Bin guard refuses the Materials folder itself", !PackageStore.IsPackageDir(PackageStore.MaterialsDir));
            H.Check("drop: …and a folder outside Packages", !PackageStore.IsPackageDir(work));

            H.Check("drop: staging is empty after every path above",
                !Directory.Exists(PackageInstaller.StagingDir) || Directory.GetFileSystemEntries(PackageInstaller.StagingDir).Length == 0,
                Directory.Exists(PackageInstaller.StagingDir) ? string.Join(",", Directory.GetFileSystemEntries(PackageInstaller.StagingDir)) : "");

            H.Check("drop: folder names lose invalid characters", PackageInstaller.SanitizeFolderName("a<b>c?") == "abc");
            H.Check("drop: folder names lose leading/trailing dots and spaces", PackageInstaller.SanitizeFolderName("  .hidden. ") == "hidden");
            H.Check("drop: DOS device names are suffixed", PackageInstaller.SanitizeFolderName("CON") == "CON-package"
                                                         && PackageInstaller.SanitizeFolderName("lpt1.zip") == "lpt1.zip-package");
            H.Check("drop: folder names are capped at 64", PackageInstaller.SanitizeFolderName(new string('x', 100)).Length == 64);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { }
        }
    }

    // ── The cabinet fields: tint and seeded preview ──────────────────────────────

    private static void CabinetFields(string root)
    {
        static ScriptGameManifest Parse(string extra, out string error) =>
            ScriptGameManifest.TryParse("{\"format\":1,\"id\":\"t\",\"title\":\"T\",\"entry\":\"t.js\"" + extra + "}", "t", "", out error);

        var ok = Parse(",\"tint\":\"#5B8DEF\",\"preview\":\"preview.png\"", out var e0);
        H.Check("manifest: tint and preview are read", ok?.TintHex == "#5B8DEF" && ok.PreviewFile == "preview.png", e0);
        H.Check("manifest: #RGB tint normalises to #RRGGBB", Parse(",\"tint\":\"#abc\"", out _)?.TintHex == "#AABBCC");
        H.Check("manifest: a tint's alpha pair is dropped", Parse(",\"tint\":\"#805B8DEF\"", out _)?.TintHex == "#5B8DEF");
        H.Check("manifest: both fields are optional", Parse("", out _) is { TintHex: null, PreviewFile: null });
        H.Check("manifest: a named colour tint is rejected", Parse(",\"tint\":\"blue\"", out var e1) is null && e1.Contains("tint"), e1);
        H.Check("manifest: a preview path is rejected", Parse(",\"preview\":\"..\\\\x.png\"", out var e2) is null && e2.Contains("preview"), e2);
        H.Check("manifest: a non-PNG/JPG preview is rejected", Parse(",\"preview\":\"p.gif\"", out var e3) is null && e3.Contains("preview"), e3);
        H.Check("manifest: badge and glyph are read", Parse(",\"badge\":\"b.png\",\"glyph\":\"g.png\"", out _) is { BadgeFile: "b.png", GlyphFile: "g.png" });
        H.Check("manifest: a JPG badge is rejected (it needs transparency)", Parse(",\"badge\":\"b.jpg\"", out var e4) is null && e4.Contains("badge"), e4);
        H.Check("manifest: a JPG glyph is rejected (its alpha is the shape)", Parse(",\"glyph\":\"g.jpg\"", out var e5) is null && e5.Contains("glyph"), e5);

        // The scanner rejects a manifest naming a preview that isn't in the folder, like any missing file.
        var tmp = Path.Combine(Path.GetTempPath(), "radiata-harness-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            File.WriteAllText(Path.Combine(tmp, "t.js"), "function tick(dt){} function draw(){}");
            File.WriteAllText(Path.Combine(tmp, "game.json"), "{\"format\":1,\"id\":\"t\",\"title\":\"T\",\"entry\":\"t.js\",\"preview\":\"gone.png\"}");
            var d = PackageStore.Inspect(tmp, "arcade");
            H.Check("scan: a missing preview file rejects the package", d.ArcadeGame is null && (d.Error ?? "").Contains("preview"), d.Error);
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }

        // Firefly demonstrates both fields; its preview is square, the shape the cabinet screen expects.
        // Development repository only: the public snapshot does not carry the sample packages.
        if (!H.DevRepoOnly("Firefly sample cabinet fields (packaging/sample-arcade-package)", root)) return;
        var firefly =PackageStore.Inspect(Path.Combine(root, "packaging", "sample-arcade-package", "firefly"), "arcade").ArcadeGame;
        H.Check("firefly: sets a cabinet tint, a preview, a badge and a glyph",
                firefly is { TintHex: not null, PreviewFile: not null, BadgeFile: not null, GlyphFile: not null });

        // A package glyph travels as a NAME (no path in config) and resolves through the same icon pipeline as an
        // MDI glyph; a game whose glyph is gone falls back to the shared drop-in glyph rather than to nothing.
        if (firefly is not null)
        {
            ArcadeCatalog.RegisterScripts([firefly]);
            try
            {
                var entry = ArcadeCatalog.Find(firefly.Token);
                H.Check("catalog: Firefly's glyph is its package glyph name", entry?.Glyph == ArcadeCatalog.PackageGlyphPrefix + firefly.Token, entry?.Glyph);
                H.Check("icons: the package glyph name resolves to an image", PackIconHelper.FromName(entry?.Glyph) is not null);
                H.Check("icons: an unregistered package glyph falls back to the drop-in glyph",
                        PackIconHelper.FromName(ArcadeCatalog.PackageGlyphPrefix + "pkg-nothing-here") is not null);
            }
            finally { ArcadeCatalog.RegisterScripts([]); }
        }
        if (firefly?.PreviewFile is { } pf)
        {
            using var fs = File.OpenRead(Path.Combine(firefly.DirPath, pf));
            var frame = System.Windows.Media.Imaging.BitmapDecoder.Create(fs, System.Windows.Media.Imaging.BitmapCreateOptions.None,
                                                                          System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            H.Check("firefly: the preview is square", frame.PixelWidth == frame.PixelHeight && frame.PixelWidth >= 256,
                    $"{frame.PixelWidth}x{frame.PixelHeight}");
        }
    }

    // ── The downloadable zips ─────────────────────────────────────────────────

    private static void ZipMatches(string zipPath, string dir, string name)
    {
        if (!File.Exists(zipPath)) { H.Fail($"site zip {name}.zip exists", "run tools\\build-workshop-samples.ps1"); return; }
        using var zip = ZipFile.OpenRead(zipPath);
        var files = Directory.GetFiles(dir).ToDictionary(Path.GetFileName, f => File.ReadAllBytes(f), StringComparer.Ordinal);
        bool rootOnly = zip.Entries.All(e => !e.FullName.Contains('/') && !e.FullName.Contains('\\'));
        bool same = zip.Entries.Count == files.Count && zip.Entries.All(e =>
        {
            if (!files.TryGetValue(e.FullName, out var want)) return false;
            using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms);
            return ms.ToArray().AsSpan().SequenceEqual(want);
        });
        H.Check($"site zip {name}.zip: files at the zip root", rootOnly, string.Join(",", zip.Entries.Select(e => e.FullName)));
        H.Check($"site zip {name}.zip matches the sample folder byte for byte", same, same ? null : "stale - run tools\\build-workshop-samples.ps1");
    }

    // ── Playing a game through the real helper ───────────────────────────────

    private sealed class Helper : IDisposable
    {
        private readonly NamedPipeServerStream _pipe;
        private readonly Process _proc;

        public Helper(string exe)
        {
            string pipeName = "radiata-harness-" + Guid.NewGuid().ToString("N");
            // Explicit buffer sizes: with the default 0-byte buffers the host's write can block (ScriptGameSession).
            _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                                              PipeOptions.Asynchronous, 1 << 20, 1 << 20);
            _proc = Process.Start(new ProcessStartInfo(exe, pipeName) { UseShellExecute = false, CreateNoWindow = true });
            if (!_pipe.WaitForConnectionAsync().Wait(10_000)) throw new TimeoutException("helper never connected");
        }

        public ScriptResponse Send(ScriptRequest req)
        {
            using var cts = new CancellationTokenSource(10_000);
            ScriptProtocol.WriteMessageAsync(_pipe, req, cts.Token).GetAwaiter().GetResult();
            var raw = ScriptProtocol.ReadMessageAsync(_pipe, ScriptProtocol.MaxResponseBytes, cts.Token).GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<ScriptResponse>(raw, ScriptProtocol.Json);
        }

        public ScriptResponse Load(string script) =>
            Send(new ScriptRequest { type = "load", script = script, seed = 0x5EED, kv = new Dictionary<string, string>() });

        public ScriptResponse Frame(long id, ScriptInput input, double ms = 1000.0 / 60) =>
            Send(new ScriptRequest { type = "frame", frameId = id, elapsedMs = ms, input = input });

        public void Dispose()
        {
            try { _pipe.Dispose(); } catch { }
            try { if (!_proc.WaitForExit(2000)) _proc.Kill(); } catch { }
            _proc.Dispose();
        }
    }

    private static void Play(string exe, string name, string script)
    {
        if (!File.Exists(exe)) { H.Fail($"{name}: Radiata.ArcadeHost.exe in the Debug output", "build Radiata first"); return; }
        H.Try($"{name}: 45 s of scripted play", () =>
        {
            using var h = new Helper(exe);
            var ready = h.Load(script);
            H.Check($"{name}: loads", ready.type == "ready", ready.message);
            if (ready.type != "ready") return;

            int faults = 0, violations = 0, empty = 0;
            string problem = null;
            var texts = new HashSet<string>(StringComparer.Ordinal);
            var cues = new HashSet<string>(StringComparer.Ordinal);
            var kv = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int f = 0; f < 60 * 45; f++)
            {
                // Sweep the stick round and in and out, and press every button the sandbox exposes now and then.
                var input = new ScriptInput { stickX = Math.Sin(f * 0.031) * Math.Cos(f * 0.007), stickY = Math.Cos(f * 0.023) };
                int b = 0;
                if (f % 90 == 0) b |= ScriptInput.CrossPressed | ScriptInput.CrossDown;
                if (f % 90 is > 0 and < 20) b |= ScriptInput.CrossDown;
                if (f % 240 == 5) b |= ScriptInput.SquarePressed | ScriptInput.SquareDown;
                if (f % 300 == 7) b |= ScriptInput.DRight;
                if (f % 310 == 9) b |= ScriptInput.DLeft;
                if (f % 320 == 11) b |= ScriptInput.DUp;
                if (f % 330 == 13) b |= ScriptInput.DDown;
                input.buttons = b;

                var r = h.Frame(f, input);
                if (r.type == "fault") { faults++; problem ??= $"frame {f}: {r.message}"; continue; }
                if (ScriptDrawValidator.Validate(r) is { } bad) { violations++; problem ??= $"frame {f}: {bad}"; }
                if (r.commands is null || r.commands.Count == 0) empty++;
                foreach (var c in r.commands ?? []) if (c.t == "text") texts.Add(c.s);
                foreach (var c in r.cues ?? []) cues.Add(c);
                foreach (var (k, v) in r.kvWrites ?? []) kv[k] = v;
            }
            H.Check($"{name}: no script fault in 2,700 frames", faults == 0, problem);
            H.Check($"{name}: every frame passes the host's draw validator", violations == 0, problem);
            H.Check($"{name}: draws every frame", empty == 0, $"{empty} empty frames");
            H.Check($"{name}: plays sounds", cues.Count > 0);
            if (name == "firefly")
            {
                H.Check("firefly: the 30-second round ends (TIME! drawn)", texts.Contains("TIME!"));
                H.Check("firefly: a hiscore, when written, is a whole number", !kv.TryGetValue("hiscore", out var hs) || int.TryParse(hs, out _), kv.GetValueOrDefault("hiscore"));
            }
        });
    }

    // ── The sandbox facts the guide states ────────────────────────────────────

    private static ScriptResponse OneFrame(string exe, string script, ScriptInput input = null, double ms = 1000.0 / 60)
    {
        using var h = new Helper(exe);
        var ready = h.Load(script);
        return ready.type != "ready" ? ready : h.Frame(1, input ?? new ScriptInput(), ms);
    }

    private static void GuideClaims(string exe)
    {
        if (!File.Exists(exe)) { H.Fail("guide claims: Radiata.ArcadeHost.exe in the Debug output", "build Radiata first"); return; }

        H.Try("guide: an out-of-range draw call is rejected by the host", () =>
        {
            var r = OneFrame(exe, "function tick(dt){} function draw(){ dot(0.5, 0, 0.6, 0xFFFFFFFF); }");
            H.Check("guide: an out-of-range draw call is rejected by the host (treated as a broken game)",
                    r.type == "draw" && ScriptDrawValidator.Validate(r) is not null, r.message);
        });
        H.Try("guide: strict mode", () =>
        {
            var r = OneFrame(exe, "function tick(dt){ undeclared = 1; } function draw(){}");
            H.Check("guide: strict mode - assigning an undeclared variable is an error", r.type == "fault", r.type);
        });
        H.Try("guide: no console", () =>
        {
            var r = OneFrame(exe, "function tick(dt){} function draw(){ console.log('x'); }");
            H.Check("guide: there is no console", r.type == "fault", r.type);
        });
        H.Try("guide: signed colours", () =>
        {
            var r = OneFrame(exe, "function tick(dt){} function draw(){ dot(0,0,0.1, 0xFF000000 | 0x112233); dot(0,0,0.1, (0xFF000000 | 0x112233) >>> 0); }");
            H.Check("guide: a colour built with | draws as nothing; >>> 0 fixes it",
                    r.type == "draw" && r.commands.Count == 2 && r.commands[0].c == 0 && r.commands[1].c == 0xFF112233u,
                    r.type == "draw" ? string.Join(",", r.commands.Select(c => c.c.ToString("X8"))) : r.message);
        });
        H.Try("guide: pressed flags", () =>
        {
            var r = OneFrame(exe,
                "var seen = 0; function tick(dt){ if (crossPressed) seen++; } function draw(){ text(0,0,0.1,String(seen),0xFFFFFFFF); seen = 0; }",
                new ScriptInput { buttons = ScriptInput.CrossPressed | ScriptInput.CrossDown });
            H.Check("guide: at 60 Hz both ticks of a frame see the same crossPressed",
                    r.type == "draw" && r.commands.FirstOrDefault()?.s == "2", r.type == "draw" ? r.commands.FirstOrDefault()?.s : r.message);
        });
        H.Try("guide: text is centred and ring edges", () =>
        {
            var r = OneFrame(exe, "function tick(dt){} function draw(){ ring(0.5, 0.1, 0xFF00FF00, 0); }");
            H.Check("guide: ring(r, width, colour, 0) is an arc from r-width/2 to r+width/2 with no edge",
                    r.type == "draw" && r.commands.Count == 1 && r.commands[0].t == "arc"
                    && Math.Abs(r.commands[0].r - 0.45) < 1e-9 && Math.Abs(r.commands[0].r2 - 0.55) < 1e-9 && (r.commands[0].sc ?? 0) == 0,
                    r.message);
        });
    }
}
