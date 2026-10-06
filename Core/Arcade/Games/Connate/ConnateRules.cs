namespace ControllerWheel;

/// <summary>Exact arithmetic shared by the simulation and headless verification probe.</summary>
public static class ConnateRules
{
    // Rank is the canonical stored value. Ranks 0/1 are the special 1 and 2; rank 2 is 3; later ranks double.
    // Sixty is a practical serialization/shift ceiling, not an expected reachable gameplay rank.
    public const int MaximumRank = 60;

    public static long ValueForRank(int rank) => rank switch
    {
        0 => 1,
        1 => 2,
        >= 2 and <= MaximumRank => 3L << (rank - 2),
        _ => throw new ArgumentOutOfRangeException(nameof(rank)),
    };

    /// <summary>Inverse of <see cref="ValueForRank"/> for the doubling range, so callers can say "the 24 tile"
    /// instead of hard-coding a rank that shifts if the ladder ever changes. Returns −1 for a value that isn't
    /// on the ladder.</summary>
    public static int RankForValue(long value)
    {
        if (value == 1) return 0;
        if (value == 2) return 1;
        for (int rank = 2; rank <= MaximumRank; rank++)
            if (ValueForRank(rank) == value) return rank;
        return -1;
    }

    public static int MergeResultRank(int a, int b)
    {
        // Threes-style exception: 1 and 2 join each other but never themselves.
        if ((a == 0 && b == 1) || (a == 1 && b == 0)) return 2;
        if (a >= 2 && a == b && a < MaximumRank) return a + 1;
        return -1;
    }

    /// <summary>Arithmetic compatibility alone. ⚠ Not sufficient to merge — see the hue-aware overload.
    /// Kept public because the arithmetic is worth testing on its own.</summary>
    public static bool Compatible(int a, int b) => MergeResultRank(a, b) >= 0;

    // ── Hue ──────────────────────────────────────────────────────────────────

    /// <summary>The two tile families. Every numbered tile carries one; only same-family tiles merge, so a
    /// board can be arithmetically ripe and still stuck, which is the whole point of the mechanic.
    /// Garbage and bombs are <see cref="None"/> — bombs belong to nobody and act on everything equally.</summary>
    public const int HueNone = -1;
    public const int HueAzure = 0;
    public const int HueEmber = 1;
    /// <summary>A blended tile, carrying both families at once. It matches
    /// any tile of the same number regardless of family, so colour simply stops being a constraint for it.
    /// Two ways to get one: merge a 1 and a 2 of opposite families, or reach the auto-blend value
    /// (<see cref="BlendThresholdValue"/>), above which every tile blends automatically.</summary>
    public const int HueBlend = 2;

    /// <summary>The 1+2 pair — the one place arithmetic joins two different numbers, and therefore the one
    /// place a cross-family merge makes sense. Every other merge is a number meeting its twin.</summary>
    public static bool IsBasePair(int rankA, int rankB) =>
        (rankA == 0 && rankB == 1) || (rankA == 1 && rankB == 0);

    /// <summary>At or above this value a tile is automatically blended.
    ///
    /// <para>⚠ 256 is not on Connate's ladder (1, 2, then 3·2ⁿ — 3, 6, 12, 24, 48, 96, 192, 384). The
    /// threshold is deliberately expressed as a value and resolved to the first rank that meets it (384), so
    /// the intent survives if the ladder ever changes, rather than a hard-coded rank silently pointing
    /// somewhere else.</para></summary>
    public const long BlendThresholdValue = 256;

    /// <summary>First rank worth <see cref="BlendThresholdValue"/> or more. Computed from the ladder, not
    /// written down.</summary>
    public static readonly int BlendThresholdRank = FirstRankAtLeast(BlendThresholdValue);

    private static int FirstRankAtLeast(long value)
    {
        for (int rank = 0; rank <= MaximumRank; rank++)
            if (ValueForRank(rank) >= value) return rank;
        return MaximumRank;
    }

    /// <summary>Force the auto-blend at and above the threshold. Applied wherever a tile's rank is
    /// decided — merge results and the feed — so a big tile can never exist un-blended.</summary>
    public static int WithAutoBlend(int rank, int hue) =>
        hue != HueNone && rank >= BlendThresholdRank ? HueBlend : hue;

    /// <summary>The merge gate: arithmetically compatible and family-compatible. Every merge decision goes
    /// through this — physics reservation, physics resolution, and the △ assist highlight — so the rule can't
    /// drift between what the game does and what it shows you.
    ///
    /// <para>Family compatibility has three routes: identical families, either side blended, or the
    /// 1+2 base pair, which is allowed across families precisely so blended tiles have an early source.</para></summary>
    public static bool Compatible(int rankA, int hueA, int rankB, int hueB)
    {
        if (!Compatible(rankA, rankB)) return false;
        if (hueA == HueNone || hueB == HueNone) return false;   // garbage and bombs belong to nobody
        if (IsBasePair(rankA, rankB)) return true;
        return hueA == hueB || hueA == HueBlend || hueB == HueBlend;
    }

    /// <summary>Family a merge result inherits.
    ///
    /// <para>Blend is recessive against colour and dominant against nothing: blended + coloured adopts
    /// the colour, blended + blended stays blended, and a cross-family 1+2 produces one. Auto-blend is
    /// applied last, so a result at or above the threshold blends whatever its parents were.</para></summary>
    public static int MergedHue(int rankA, int hueA, int rankB, int hueB, int resultRank)
    {
        int hue;
        if (IsBasePair(rankA, rankB)) hue = hueA == hueB ? hueA : HueBlend;
        else if (hueA == HueBlend && hueB != HueBlend) hue = hueB;
        else if (hueB == HueBlend && hueA != HueBlend) hue = hueA;
        else hue = hueA;
        return WithAutoBlend(resultRank, hue);
    }

    // Scores remain monotonic at pathological ranks instead of wrapping negative and corrupting a save.
    public static long SaturatingAdd(long a, long b) =>
        a >= long.MaxValue - b ? long.MaxValue : a + b;

    public static int HostScore(long fieldSum) => fieldSum >= int.MaxValue ? int.MaxValue : (int)Math.Max(0, fieldSum);
}
