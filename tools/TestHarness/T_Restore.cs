using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Radiata.TestHarness;

/// <summary>Restoring from an unrelated or hostile ZIP, and backup &amp; restore.
///
/// The guards under test are private statics on SettingsWindow — <c>ReadBundledArt</c>,
/// <c>ReadBoundedText</c>, <c>LooksLikeRadiataBackup</c>, <c>ReadArtManifest</c> — reached by reflection so
/// this exercises the shipping guard, not a copy. <c>RestoreSettings()</c> itself is an instance method
/// behind a file dialog and a MessageBox, so the dialog flow is NOT covered here; what's covered is every
/// decision it delegates, which is where both live bugs (zip-slip, pre-confirmation OOM) lived.
///
/// Hostile zips are built with raw <see cref="ZipArchive"/> writes so entry names can carry backslashes and
/// <c>..</c> — <see cref="ZipFile.CreateFromDirectory"/> would sanitize them and the test would prove
/// nothing.</summary>
internal static class T_Restore
{
    private static Type Sw => H.AppType("SettingsWindow");
    private static string Tmp;

    public static void Run()
    {
        H.Group("Restore — hostile / unrelated ZIPs (SettingsWindow guards)");

        if (Sw is null) { H.Fail("SettingsWindow type not found", "assembly reference wrong?"); return; }
        Tmp = Path.Combine(Path.GetTempPath(), "radiata-harness", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Tmp);

        var readBundledArt = H.StaticMethod(Sw, "ReadBundledArt", 2);
        var readBounded    = H.StaticMethod(Sw, "ReadBoundedText", 1);
        var looksLike      = H.StaticMethod(Sw, "LooksLikeRadiataBackup", 1);
        var readManifest   = H.StaticMethod(Sw, "ReadArtManifest", 1);

        if (readBundledArt is null) H.Fail("ReadBundledArt(zip, out skipped) not found", "renamed? guard untested");
        if (readBounded is null)    H.Fail("ReadBoundedText(entry) not found", "renamed? guard untested");
        if (looksLike is null)      H.Fail("LooksLikeRadiataBackup(json) not found", "renamed? guard untested");
        if (readManifest is null)   H.Fail("ReadArtManifest(zip) not found", "renamed? guard untested");

        // ── "Is this even our backup?" ───────────────────────────────────────────────────────────────
        if (looksLike is not null)
        {
            bool Looks(string json) => (bool)looksLike.Invoke(null, new object[] { json });
            // All THREE top-level properties are required (WriteConfig always emits wheelA + wheelB + system).
            H.Check("a real config IS recognised as a backup",
                    Looks("{\"wheelA\":[{\"label\":\"x\"}],\"wheelB\":[],\"system\":{\"fadeMs\":200}}"));
            H.Check("a config missing wheelB is REFUSED (all three keys required)",
                    !Looks("{\"wheelA\":[],\"system\":{}}"));
            H.Check("key matching is case-insensitive, like the parser",
                    Looks("{\"WHEELA\":[],\"WheelB\":[],\"SYSTEM\":{}}"));
            H.Check("arbitrary JSON is REFUSED (can't wipe settings)", !Looks("{\"a\":1,\"b\":[2,3]}"));
            H.Check("an empty object is REFUSED", !Looks("{}"));
            H.Check("a JSON array is REFUSED", !Looks("[{\"wheelA\":[]}]"));
            H.Check("HTML is REFUSED", !Looks("<html></html>"));
            // A hostile file that *names* our keys but carries nothing usable still has to parse to something.
            var parsed = ControllerWheel.ConfigLoader.TryParse("{\"wheelA\":null,\"system\":null}");
            H.Check("keys-present-but-null parses to a usable config (defaults)", parsed is not null);
        }

        // ── zip-slip, both separators + a drive-absolute name ────────────────────────────────────────
        if (readBundledArt is not null)
        {
            var slip = Path.Combine(Tmp, "slip.zip");
            using (var fs = File.Create(slip))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddRaw(zip, @"art\..\..\..\..\Windows\Temp\radiata-evil.png", PngBytes());
                AddRaw(zip, "art/../../../../Windows/Temp/radiata-evil2.png", PngBytes());
                AddRaw(zip, @"art\C:\Windows\Temp\radiata-evil3.png", PngBytes());
                AddRaw(zip, "art/legit.png", PngBytes());
            }

            using var t = new H.TraceGrab();
            var (art, skipped) = Bundled(readBundledArt, slip);
            H.Check("zip-slip: nothing resolves outside the art cache",
                    art is not null && art.Keys.All(k => !k.Contains("..") && !k.Contains(":")),
                    art is null ? "returned null" : "kept: " + string.Join(", ", art.Keys));
            H.Check("zip-slip: the legit entry still comes through",
                    art is not null && art.Keys.Any(k => k.EndsWith("legit.png", StringComparison.OrdinalIgnoreCase)));
            H.Check("zip-slip: nothing was written to disk outside the cache",
                    !File.Exists(@"C:\Windows\Temp\radiata-evil.png")
                    && !File.Exists(@"C:\Windows\Temp\radiata-evil2.png")
                    && !File.Exists(@"C:\Windows\Temp\radiata-evil3.png"));
            // How the traversal is defeated matters for reading the trace: the leaf-name derivation
            // NEUTRALIZES a "art/../../x.png" entry (it lands in the cache as x.png) rather than refusing it,
            // so a clean run shows no REFUSED line. Separately, an entry using BACKSLASH separators
            // ("art\..\x.png") doesn't match the "art/" prefix at all, so its art is ignored entirely — worth
            // knowing before reading a backslash-style archive's restore as a success.
            H.Check("forward-slash traversal is neutralised to a leaf inside the cache",
                    art is not null && art.Keys.Any(k => k.Equals("radiata-evil2.png", StringComparison.OrdinalIgnoreCase)),
                    "landed as a bare leaf, not at the traversal target");
            H.Check("backslash-separator art entries are ignored (don't match the art/ prefix)",
                    art is not null && !art.Keys.Any(k => k.Contains("evil.png") || k.Contains("evil3.png")));
            Console.WriteLine($"        (record) skipped={skipped}; [Restore] lines: " +
                              (t.Lines.Any(l => l.Contains("[Restore]"))
                               ? string.Join(" | ", t.Lines.Where(l => l.Contains("[Restore]")).Take(4))
                               : "(none — neutralised by leaf derivation, not refused)"));
        }

        // ── non-image extensions in art/ ─────────────────────────────────────────────────────────────
        if (readBundledArt is not null)
        {
            var exe = Path.Combine(Tmp, "exe-in-art.zip");
            using (var fs = File.Create(exe))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddRaw(zip, "art/payload.exe", new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
                AddRaw(zip, "art/payload.dll", new byte[] { 0x4D, 0x5A });
                AddRaw(zip, "art/payload.png.exe", new byte[] { 0x4D, 0x5A });
                AddRaw(zip, "art/ok.jpg", PngBytes());
            }
            var (art, _) = Bundled(readBundledArt, exe);
            H.Check("only .png/.jpg are taken from art/",
                    art is not null && art.Keys.All(k => k.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                                      || k.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)),
                    art is null ? "null" : "kept: " + string.Join(", ", art.Keys));
        }

        // ── entry-count cap (4000) ───────────────────────────────────────────────────────────────────
        if (readBundledArt is not null)
        {
            var many = Path.Combine(Tmp, "4001.zip");
            using (var fs = File.Create(many))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                for (int i = 0; i < 4600; i++) AddRaw(zip, $"art/f{i}.png", PngBytes());

            using var t = new H.TraceGrab();
            var (art, _) = Bundled(readBundledArt, many);
            H.Check("art entry count capped at 4000", art is not null && art.Count <= 4000, $"{art?.Count} kept");
            H.Check("…and the stop is traced", t.Saw("stopped after 4000"));
        }

        // ── per-entry size cap (32 MB) ───────────────────────────────────────────────────────────────
        if (readBundledArt is not null)
        {
            var big = Path.Combine(Tmp, "big-entry.zip");
            using (var fs = File.Create(big))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddRaw(zip, "art/huge.png", new byte[40L * 1024 * 1024]);   // zeros: ~40 KB on disk
                AddRaw(zip, "art/small.png", PngBytes());
            }
            using var t = new H.TraceGrab();
            var (art, skipped) = Bundled(readBundledArt, big);
            H.Check("a 40 MB art entry is skipped, the small one survives",
                    art is not null && art.Count == 1 && skipped >= 1,
                    $"kept {art?.Count}, skipped {skipped}");
            H.Check("…and the skip is traced", t.Saw("MB declared") || t.Saw("over the"));
        }

        // ── total art budget (256 MB) ────────────────────────────────────────────────────────────────
        if (readBundledArt is not null)
        {
            var total = Path.Combine(Tmp, "over-budget.zip");
            using (var fs = File.Create(total))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                for (int i = 0; i < 12; i++) AddRaw(zip, $"art/b{i}.png", new byte[30L * 1024 * 1024]);

            using var t = new H.TraceGrab();
            var (art, _) = Bundled(readBundledArt, total);
            long kept = art?.Values.Sum(v => (long)v.Length) ?? 0;
            H.Check("art payload stops at the 256 MB budget", kept <= 256L * 1024 * 1024 + 30L * 1024 * 1024,
                    $"{kept / 1024 / 1024} MB kept of 360 MB offered");
            H.Check("…and the stop is traced", t.Saw("exceeded"));
        }

        // ── JSON entry caps: declared size AND actual decompressed size (the OOM bug) ────────────────
        if (readBounded is not null)
        {
            var jsonZip = Path.Combine(Tmp, "json-caps.zip");
            using (var fs = File.Create(jsonZip))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddRaw(zip, "small.json", Encoding.UTF8.GetBytes("{\"wheelA\":[]}"));
                AddRaw(zip, "declared-big.json", new byte[9L * 1024 * 1024]);      // over the 8 MB cap
                AddRaw(zip, "bomb.json", new byte[400L * 1024 * 1024]);            // ~400 MB from ~400 KB
            }
            using var t = new H.TraceGrab();
            using var z = ZipFile.OpenRead(jsonZip);
            string Read(string name) => (string)readBounded.Invoke(null, new object[] { z.GetEntry(name) });

            H.Check("a small JSON entry reads back", Read("small.json") == "{\"wheelA\":[]}");
            H.Check("a 9 MB JSON entry is refused", Read("declared-big.json") is null);
            var before = GC.GetTotalAllocatedBytes();
            var bomb = Read("bomb.json");
            var alloc = (GC.GetTotalAllocatedBytes() - before) / 1024 / 1024;
            H.Check("a 400 MB decompression bomb is refused", bomb is null, $"allocated ~{alloc} MB while refusing");
            H.Check("…and both refusals are traced", t.Saw("skipped"));
        }

        // ── art-manifest ────────────────────────────────────────────────────────────────────────────
        if (readManifest is not null)
        {
            var man = Path.Combine(Tmp, "manifest.zip");
            using (var fs = File.Create(man))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                AddRaw(zip, "art-manifest.json",
                       Encoding.UTF8.GetBytes("{\"C:\\\\old\\\\usericon_abc.png\":\"usericon_abc.png\"}"));
            using var z = ZipFile.OpenRead(man);
            var dict = (Dictionary<string, string>)readManifest.Invoke(null, new object[] { z });
            H.Check("art-manifest.json parses to the LogoPath→entry map", dict is not null && dict.Count == 1,
                    dict is null ? "null" : string.Join(", ", dict.Select(kv => kv.Key + " → " + kv.Value)));

            var bad = Path.Combine(Tmp, "manifest-bad.zip");
            using (var fs = File.Create(bad))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                AddRaw(zip, "art-manifest.json", Encoding.UTF8.GetBytes("not json at all"));
            using var z2 = ZipFile.OpenRead(bad);
            H.Try("a corrupt art-manifest doesn't throw", () =>
            {
                var d = readManifest.Invoke(null, new object[] { z2 });
                H.Pass("a corrupt art-manifest is tolerated", d is null ? "returned null" : "returned a map");
            });
        }

        // ── an unrelated / non-zip file: the outer OpenRead must be the thing that fails ─────────────
        {
            var notZip = Path.Combine(Tmp, "random.zip");
            File.WriteAllBytes(notZip, Enumerable.Range(0, 300_000).Select(i => (byte)(i * 7)).ToArray());
            try
            {
                using var z = ZipFile.OpenRead(notZip);
                H.Fail("a random file renamed .zip should not open as a zip");
            }
            catch (InvalidDataException)
            {
                H.Pass("a random file renamed .zip throws InvalidDataException",
                       "RestoreSettings wraps the OpenRead in try/catch → \"couldn't read\" path");
            }
            catch (Exception ex) { H.Fail("unexpected exception type from a non-zip", ex.GetType().Name); }
        }

        // ── a zip with no config at all (a stranger's archive) ──────────────────────────────────────
        if (readBounded is not null)
        {
            var foreign = Path.Combine(Tmp, "foreign.zip");
            using (var fs = File.Create(foreign))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddRaw(zip, "holiday/IMG_1234.jpg", PngBytes());
                AddRaw(zip, "notes.txt", Encoding.UTF8.GetBytes("hello"));
            }
            using var z = ZipFile.OpenRead(foreign);
            H.Check("a foreign zip has no config.json entry to read", z.GetEntry("config.json") is null);
        }

        try { Directory.Delete(Tmp, true); } catch { }
    }

    /// <summary>Write an entry with the EXACT name given — including backslashes and <c>..</c>, which the
    /// convenience APIs would normalise away.</summary>
    private static void AddRaw(ZipArchive zip, string fullName, byte[] data)
    {
        var e = zip.CreateEntry(fullName, CompressionLevel.Fastest);
        using var s = e.Open();
        s.Write(data, 0, data.Length);
    }

    private static (Dictionary<string, byte[]> art, int skipped) Bundled(MethodInfo m, string zipPath)
    {
        using var z = ZipFile.OpenRead(zipPath);
        var args = new object[] { z, 0 };
        var art = (Dictionary<string, byte[]>)m.Invoke(null, args);
        return (art, (int)args[1]);
    }

    /// <summary>A real 1x1 PNG, so anything that sniffs content rather than extension is satisfied.</summary>
    private static byte[] PngBytes() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");
}
