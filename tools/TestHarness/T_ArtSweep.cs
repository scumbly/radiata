using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Radiata.TestHarness;

/// <summary>Runs the art cache's normalise sweep and proves it leaves nothing above the READ budget.
///
/// The bug this exists for: <c>NormalizeArtBytes</c> declined its own re-encode whenever
/// the output wasn't smaller in BYTES. That mercy was meant for files just over the 16 MP normalise
/// threshold, but byte size doesn't track pixel count — a hugely compressible image (flat colour, mostly
/// transparent) is enormous in pixels and tiny on disk, so it declined too and landed in the cache above
/// <c>LoadFrozen</c>'s 64 MP read budget. There it was refused on every read, and since the file existed it
/// was a cache hit that never re-fetched: the logo was gone permanently.
///
/// <para>The sweep normally piggy-backs on the art prefetcher, which is kicked when the Game Grid opens.
/// Calling it directly is the same code on the same thread kind, and lets the fix be verified without a
/// controller in hand.</para></summary>
internal static class T_ArtSweep
{
    private static Type GA => H.AppType("GameArt");

    private static string CacheDir =>
        (string)GA.GetProperty("CacheDirectory", System.Reflection.BindingFlags.Static
                                               | System.Reflection.BindingFlags.Public
                                               | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);

    /// <summary>The read budget the loader enforces — read from the app rather than hardcoded, so this test
    /// can't drift away from the constant it is checking.</summary>
    private static long ReadBudget =>
        (long)GA.GetField("UserLogoMaxPixels", System.Reflection.BindingFlags.Static
                                             | System.Reflection.BindingFlags.NonPublic
                                             | System.Reflection.BindingFlags.Public).GetValue(null);

    public static void Run(bool force)
    {
        H.Group("Art cache — nothing may sit above the decode budget");

        var dir = CacheDir;
        if (dir is null || !Directory.Exists(dir)) { H.Skip("art cache sweep", "no cache directory"); return; }
        long budget = ReadBudget;
        Console.WriteLine($"        (record) cache={dir}");
        Console.WriteLine($"        (record) read budget = {budget / 1_000_000} MP");

        var before = OverBudget(dir, budget);
        Console.WriteLine($"        (record) BEFORE: {before.Count} file(s) over budget");
        foreach (var f in before) Console.WriteLine($"        (record)   {f.Name}  {f.Mp} MP  {f.Kb} KB");

        // The one-shot marker is what stops the sweep re-running; a fix to the normalise RULE is invisible on
        // a cache that already swept, so allow clearing it here.
        if (force)
        {
            foreach (var m in Directory.GetFiles(dir, ".normalized-v*"))
            {
                File.Delete(m);
                Console.WriteLine($"        (record) cleared marker {Path.GetFileName(m)}");
            }
        }

        using (var t = new H.TraceGrab())
        {
            H.InvokeStatic(GA, "NormalizeOversizedCacheOnce");
            foreach (var l in t.Lines.Where(l => l.Contains("[Art]"))) Console.WriteLine("        " + l);
        }

        var after = OverBudget(dir, budget);
        Console.WriteLine($"        (record) AFTER: {after.Count} file(s) over budget");
        foreach (var f in after) Console.WriteLine($"        (record)   {f.Name}  {f.Mp} MP  {f.Kb} KB");

        H.Check("no cached image exceeds the decode budget", after.Count == 0,
                after.Count == 0 ? $"{before.Count} repaired" : "still over: " + string.Join(", ", after.Select(f => $"{f.Name} ({f.Mp} MP)")));
        H.Check("every previously over-budget file now decodes",
                before.All(f => Decodes(Path.Combine(dir, f.Name))),
                string.Join(", ", before.Where(f => !Decodes(Path.Combine(dir, f.Name))).Select(f => f.Name)) is { Length: > 0 } bad
                    ? "still undecodable: " + bad : $"{before.Count} checked");
        H.Check("the sweep marker is written so it won't re-run every session",
                Directory.GetFiles(dir, ".normalized-v*").Length > 0);
    }

    private record Over(string Name, long Mp, long Kb);

    private static System.Collections.Generic.List<Over> OverBudget(string dir, long budget)
    {
        var list = new System.Collections.Generic.List<Over>();
        foreach (var f in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(f);
            if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var fs = File.OpenRead(f);
                var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (dec.Frames.Count == 0) continue;
                long px = (long)dec.Frames[0].PixelWidth * dec.Frames[0].PixelHeight;
                if (px > budget)
                    list.Add(new Over(Path.GetFileName(f), px / 1_000_000, new FileInfo(f).Length / 1024));
            }
            catch
            {
                // A header we can't even read is exactly as unusable as an over-budget one, and the loader
                // treats it the same way (WithinPixelBudget returns false), so count it.
                list.Add(new Over(Path.GetFileName(f) + " (header unreadable)", -1, new FileInfo(f).Length / 1024));
            }
        }
        return list;
    }

    /// <summary>Does the loader's own gate accept this file now? That's the question the user feels.</summary>
    private static bool Decodes(string path)
    {
        var m = H.StaticMethod(GA, "WithinPixelBudget", 1);
        if (m is null) return false;
        try { return (bool)m.Invoke(null, new object[] { path }); } catch { return false; }
    }
}
