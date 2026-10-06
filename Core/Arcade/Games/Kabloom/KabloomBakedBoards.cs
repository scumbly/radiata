namespace ControllerWheel;

/// <summary>The pre-baked no-guess boards Kabloom plays from <see cref="FirstBakedLevel"/> up.
///
/// <para>Hex crops stop yielding no-guess boards at random well below the densities the later levels want,
/// so those levels do not search at play time: <c>tools\KabloomBake</c> searches offline, certifies each board
/// from every zero-clue cell it can be opened on, and keeps a set whose starts cover the crop. At the first
/// reveal the game picks a board on which the cell the player chose is a certified start, so the first click
/// is safe, opens, and leads to a board solvable without guessing — the same promise the runtime generator
/// makes for the early levels.</para>
///
/// <para>⚠ The data half is generated (<c>KabloomBakedBoards.Data.cs</c>) and keyed to the crop
/// <see cref="KabloomGrid.Create"/> builds for the level's target cell count. A crop change invalidates it:
/// <see cref="TryPick"/> refuses a board whose cell count no longer matches and the game falls back to the
/// runtime generator, so a stale bake degrades to slow rather than to wrong. Re-run the tool after touching
/// the crop or the occupancy curve.</para>
///
/// <para>Bee counts are packed two bits per cell (0..3), starts one bit per cell, both least-significant bit
/// first, base64. A level carries the capacity its boards were baked at.</para></summary>
public static partial class KabloomBakedBoards
{
    /// <summary>First campaign level that plays baked boards. Levels below it are small enough for the
    /// runtime generator to find no-guess boards within a second, and keep it.</summary>
    public const int FirstBakedLevel = 11;

    private sealed record Board(string BeesPacked, string StartsPacked, double Score)
    {
        private byte[]? _bees;
        private bool[]? _starts;
        public byte[] Bees(int count) => _bees ??= UnpackCounts(BeesPacked, count);
        public bool[] Starts(int count) => _starts ??= UnpackBits(StartsPacked, count);
    }

    private sealed record Level(int Number, int CellCount, int Capacity, int BeeTotal, double Occupancy, Board[] Boards);

    /// <summary>Does a bake exist for this level that fits this crop?</summary>
    public static bool Has(int level, int cellCount) => Find(level, cellCount) is { Boards.Length: > 0 };

    /// <summary>A baked board for <paramref name="level"/> on which <paramref name="firstCell"/> is a certified
    /// no-guess start, chosen among the candidates by <paramref name="seed"/>, with the capacity it was baked
    /// at; null when the level has no bake, the bake does not fit the crop, or no board covers that cell.</summary>
    public static (byte[] Bees, int Capacity)? TryPick(int level, int cellCount, int firstCell, ulong seed)
    {
        if (Find(level, cellCount) is not { } bake || firstCell < 0 || firstCell >= cellCount) return null;
        var covering = new List<Board>();
        foreach (var board in bake.Boards)
            if (board.Starts(cellCount)[firstCell]) covering.Add(board);
        if (covering.Count == 0) return null;
        // Mixed with the cell so the same campaign seed does not map every start to the same board index.
        ulong mix = seed ^ ((ulong)firstCell * 0x9E3779B97F4A7C15UL);
        mix ^= mix >> 31; mix *= 0xBF58476D1CE4E5B9UL; mix ^= mix >> 29;
        return ((byte[])covering[(int)(mix % (ulong)covering.Count)].Bees(cellCount).Clone(), bake.Capacity);
    }

    private static Level? Find(int level, int cellCount)
    {
        foreach (var l in Data)
            if (l.Number == level) return l.CellCount == cellCount ? l : null;
        return null;
    }

    private static bool[] UnpackBits(string packed, int count)
    {
        var bits = new bool[count];
        byte[] bytes;
        try { bytes = Convert.FromBase64String(packed); }
        catch (FormatException) { return bits; }
        for (int i = 0; i < count && (i >> 3) < bytes.Length; i++)
            bits[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        return bits;
    }

    private static byte[] UnpackCounts(string packed, int count)
    {
        var counts = new byte[count];
        byte[] bytes;
        try { bytes = Convert.FromBase64String(packed); }
        catch (FormatException) { return counts; }
        for (int i = 0; i < count && (i >> 2) < bytes.Length; i++)
            counts[i] = (byte)((bytes[i >> 2] >> ((i & 3) * 2)) & 3);
        return counts;
    }

    /// <summary>Every baked level, decoded — for the bake tool and the probe.</summary>
    public static IEnumerable<BakedLevel> All()
    {
        foreach (var l in Data)
            yield return new BakedLevel(l.Number, l.CellCount, l.Capacity, l.BeeTotal, l.Occupancy,
                [.. l.Boards.Select(b => new BakedBoard(b.Bees(l.CellCount), b.Starts(l.CellCount), b.Score))]);
    }

    public sealed record BakedBoard(byte[] Bees, bool[] Starts, double Score);
    public sealed record BakedLevel(int Level, int CellCount, int Capacity, int BeeTotal, double Occupancy, BakedBoard[] Boards);
}
