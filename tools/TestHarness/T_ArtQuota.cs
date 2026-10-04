using System;
using System.IO;
using System.Linq;

namespace Radiata.TestHarness;

/// <summary>The art cache stays bounded.
///
/// <para>Browsing the Game Grid can only ever prove the <i>bound</i> — and only if the library happens to be
/// big enough to cross it. On the author's rig a full browse landed at 425.9 MB against a 512 MB quota, so the
/// trim never ran and the half that matters (does it trim, and does it trim the RIGHT things) was
/// unprovable that way.</para>
///
/// <para>So this drives the real <c>ReserveCacheSpace</c> / <c>TrimOldestArt</c> after pointing
/// <c>GameArt.CacheDir</c> at a temp folder full of synthetic files. That buys the things the live cache can't
/// give: an eviction order that can be asserted exactly, and the chance to check what must SURVIVE a trim —
/// <c>.miss</c> markers (deleting them would cause the re-fetch storm the trim exists to avoid), <c>.tmp</c>
/// files owned by a concurrent writer, and the dotfile state markers including <c>.normalized-v2</c>, which
/// the oversized-art fix depends on.</para>
///
/// <para>Nothing here touches the real cache. If the field can't be redirected the group SKIPS rather than
/// falling back to the live folder — a test that deletes the tester's art to prove deletion works is not a
/// trade worth making.</para></summary>
internal static class T_ArtQuota
{
    private static Type ArtT => H.AppType("GameArt");

    private const long Quota = 512L * 1024 * 1024;

    public static void Run()
    {
        H.Group("Art cache quota — the trim fires, and spares what it must");

        var artT = ArtT;
        if (artT is null) { H.Fail("GameArt type is present", "not found in the app assembly"); return; }

        // The quota is a compile-time constant in the app; read it back rather than trusting the copy above,
        // so a change there turns into a visible mismatch instead of a test that quietly checks nothing.
        var quotaField = H.StaticField(artT, "CacheQuotaBytes");
        long quota = quotaField is not null ? (long)quotaField.GetRawConstantValue() : Quota;
        H.Check("the quota constant is still ~512 MB", quota == Quota,
                $"CacheQuotaBytes = {quota / (1024 * 1024)} MB");

        var real = (string)H.GetStatic(artT, "CacheDir");
        var temp = Path.Combine(Path.GetTempPath(), "radiata-artquota-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);

        if (!H.TrySetStatic(artT, "CacheDir", temp))
        {
            H.Skip("art-cache trim", "GameArt.CacheDir could not be redirected — refusing to run a destructive "
                                   + $"trim against the real cache. Reason: {H.SetStaticError ?? "read-back mismatch"}");
            try { Directory.Delete(temp, true); } catch { }
            return;
        }

        try
        {
            H.Check("the cache directory was redirected away from the real one",
                    !string.Equals(temp, real, StringComparison.OrdinalIgnoreCase)
                    && (string)H.GetStatic(artT, "CacheDir") == temp,
                    $"real cache untouched at {real}");

            UnderQuotaDoesNotTrim(artT, temp);
            ReplacementsDoNotRatchet(artT, temp, quota);
            OverQuotaTrimsOldestFirst(artT, temp, quota);
            TrimSparesMarkers(artT, temp, quota);
            NothingEvictable(artT, temp);
        }
        finally
        {
            // Put the real path back before anything else in the process can write art.
            H.TrySetStatic(artT, "CacheDir", real);
            H.Check("the real cache directory was restored",
                    (string)H.GetStatic(artT, "CacheDir") == real, real);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    // ── scenarios ────────────────────────────────────────────────────────────────────────────────────

    private static void UnderQuotaDoesNotTrim(Type artT, string dir)
    {
        Reset(artT, dir);
        Write(dir, "cover_a.jpg", 4 * MB, DateTime.UtcNow.AddDays(-9));
        Write(dir, "cover_b.jpg", 4 * MB, DateTime.UtcNow.AddDays(-8));

        using var t = new H.TraceGrab();
        bool ok = Reserve(artT, 1 * MB);
        H.Check("a write that stays under quota is allowed", ok);
        H.Check("…and nothing is trimmed", !t.Saw("trimmed"), "no trim trace");
        H.Check("…and both files survive", File.Exists(Path.Combine(dir, "cover_a.jpg"))
                                       && File.Exists(Path.Combine(dir, "cover_b.jpg")));
    }

    /// <summary>Audit #9 regression: refreshing files in place used to book the FULL incoming size every
    /// time while disk usage never grew, so the estimate ratcheted up until every write was refused for
    /// the rest of the session. Replacements must book only the net change.</summary>
    private static void ReplacementsDoNotRatchet(Type artT, string dir, long quota)
    {
        Reset(artT, dir);

        // Near quota (480 of 512 MB), then refresh-in-place well past the point the old accounting
        // would have blown through it: 50 x 40 MB of "incoming" = 2 GB booked the old way, net 0 now.
        for (int i = 12; i >= 1; i--)
            Write(dir, $"art_{i:00}.jpg", 40 * MB, DateTime.UtcNow.AddDays(-i));

        using var t = new H.TraceGrab();
        bool allOk = true;
        for (int i = 0; i < 50; i++) allOk &= Reserve(artT, 40 * MB, replacing: 40 * MB);

        H.Check("50 same-size replacements near quota are ALL allowed", allOk);
        H.Check("…without ever trimming (disk usage never grew)", !t.Saw("trimmed"),
                string.Join(" | ", t.Lines.Where(l => l.Contains("[Art]")).Select(l => l.Trim())));
        H.Check("…and no file was evicted", Directory.GetFiles(dir, "art_*.jpg").Length == 12);
    }

    private static void OverQuotaTrimsOldestFirst(Type artT, string dir, long quota)
    {
        Reset(artT, dir);

        // 13 files x 40 MB = 520 MB, already over quota. Ages are strictly ordered so the eviction order is
        // unambiguous — oldest is age 13 days, newest is age 1 day.
        for (int i = 13; i >= 1; i--)
            Write(dir, $"art_{i:00}.jpg", 40 * MB, DateTime.UtcNow.AddDays(-i));

        long before = Size(dir);
        H.Check("the synthetic cache starts over quota", before > quota,
                $"{before / MB} MB vs {quota / MB} MB");

        using var t = new H.TraceGrab();
        bool ok = Reserve(artT, 8 * MB);
        long after = Size(dir);

        H.Check("a write that crosses the quota is still allowed, after trimming", ok);
        H.Check("the trim announced itself", t.Saw("trimmed"),
                string.Join(" | ", t.Lines.Where(l => l.Contains("[Art]")).Select(l => l.Trim())));

        // Trims to 80% of quota minus the incoming write, so a full cache doesn't evict on every write.
        long target = (long)(quota * 0.8) - 8 * MB;
        H.Check("it trimmed down to the 80%-of-quota target, not just barely under the cap",
                after <= target, $"{after / MB} MB, target {target / MB} MB (cap is {quota / MB} MB)");
        H.Check("…and it did not empty the cache", after > 0, $"{after / MB} MB left");

        // Oldest-first: whatever survives must be the NEWEST files, contiguously.
        var survivors = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        var evicted = Enumerable.Range(1, 13).Select(i => $"art_{i:00}.jpg").Except(survivors).ToArray();
        bool contiguousOldest = evicted.Length > 0
            && evicted.All(n => int.Parse(n.Substring(4, 2)) > survivors.Max(s => int.Parse(s.Substring(4, 2))));
        H.Check("eviction was oldest-first (survivors are the newest, contiguously)", contiguousOldest,
                $"evicted {string.Join(",", evicted.OrderBy(x => x))} | kept {string.Join(",", survivors)}");
    }

    private static void TrimSparesMarkers(Type artT, string dir, long quota)
    {
        Reset(artT, dir);

        // The three things that must outlive a trim, all back-dated to be the OLDEST files present — so if the
        // skip logic were missing, they would be the very first casualties.
        var ancient = DateTime.UtcNow.AddDays(-365);
        Write(dir, "steam_12345.miss", 64, ancient);
        Write(dir, "cover_half_written.tmp", 8 * MB, ancient);
        Write(dir, ".normalized-v2", 0, ancient);
        Write(dir, ".some-other-state", 16, ancient);

        for (int i = 13; i >= 1; i--)
            Write(dir, $"art_{i:00}.jpg", 40 * MB, DateTime.UtcNow.AddDays(-i));

        Reserve(artT, 8 * MB);

        H.Check("a .miss marker survives the trim", File.Exists(Path.Combine(dir, "steam_12345.miss")),
                "deleting these would cause the re-fetch storm the trim exists to relieve");
        H.Check("a .tmp file owned by a concurrent writer survives",
                File.Exists(Path.Combine(dir, "cover_half_written.tmp")));
        H.Check("the .normalized-v2 marker survives", File.Exists(Path.Combine(dir, ".normalized-v2")),
                "losing it would re-run the whole normalise sweep on every launch");
        H.Check("other dotfile state markers survive", File.Exists(Path.Combine(dir, ".some-other-state")));
        H.Check("…while ordinary art was still evicted", Directory.GetFiles(dir, "art_*.jpg").Length < 13,
                $"{Directory.GetFiles(dir, "art_*.jpg").Length} of 13 art files left");
    }

    private static void NothingEvictable(Type artT, string dir)
    {
        Reset(artT, dir);

        // A cache over quota made ENTIRELY of unevictable things. The write must be refused rather than the
        // quota silently exceeded.
        for (int i = 0; i < 14; i++)
            Write(dir, $"m_{i:00}.miss", 40 * MB, DateTime.UtcNow.AddDays(-i - 1));

        using var t = new H.TraceGrab();
        bool ok = Reserve(artT, 8 * MB);
        H.Check("a write is REFUSED when the cache is at quota and nothing is evictable", !ok);
        H.Check("…and it says so", t.Saw("nothing evictable"),
                string.Join(" | ", t.Lines.Where(l => l.Contains("[Art]")).Select(l => l.Trim())));
        H.Check("…and it deleted none of the unevictable files", Directory.GetFiles(dir, "*.miss").Length == 14);
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────

    private const long MB = 1024 * 1024;

    private static bool Reserve(Type artT, long incoming, long replacing = 0)
        => (bool)H.InvokeStatic(artT, "ReserveCacheSpace", incoming, replacing);

    /// <summary>Fresh directory + drop the app's cached size estimate, so each scenario measures from scratch.</summary>
    private static void Reset(Type artT, string dir)
    {
        foreach (var f in Directory.GetFiles(dir)) { try { File.Delete(f); } catch { } }
        H.InvokeStatic(artT, "InvalidateCacheSizeEstimate");
    }

    private static void Write(string dir, string name, long bytes, DateTime utc)
    {
        var path = Path.Combine(dir, name);
        // Sparse-ish: set the length rather than writing real bytes, so 520 MB of fixture costs no real IO.
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write)) fs.SetLength(bytes);
        File.SetLastWriteTimeUtc(path, utc);
    }

    private static long Size(string dir)
        => Directory.GetFiles(dir).Sum(f => new FileInfo(f).Length);
}
