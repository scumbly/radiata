namespace ControllerWheel;

public enum KabloomBoardPhase { Unplanted, Playing, Cleared, Failed }
public enum KabloomActionKind { None, Revealed, Flagged, Unflagged, Chorded, Guarded, Failed, Cleared, Marked, Unmarked }

public readonly record struct KabloomActionResult(KabloomActionKind Kind, int ChangedCells = 0);

/// <summary>Bee-field truth and player-visible state. All rule adjacency comes from the grid's shared edges.
///
/// <para>A cell holds 0..<see cref="Capacity"/> bees and a clue is the sum over its neighbours. Flags carry a
/// count the same way, so a chord compares the flagged sum against the clue and the remaining-bee readout is
/// <see cref="BeeTotal"/> − <see cref="FlagSum"/>. Capacity 1 is classic minesweeper.</para></summary>
public sealed class KabloomBoard
{
    private readonly byte[] _bees;
    private readonly bool[] _revealed;
    private readonly byte[] _flags;
    /// <summary>Question marks: "not sure". Protected from reveal and cascade exactly like a flag, but not
    /// counted against the bees and not counted by a chord. Never set on a flagged cell.</summary>
    private readonly bool[] _marked;
    private readonly byte[] _clues;

    public KabloomGrid Grid { get; }
    public KabloomBoardPhase Phase { get; private set; } = KabloomBoardPhase.Unplanted;
    public int FirstCell { get; private set; } = -1;
    /// <summary>Most bees one cell may hold on this board.</summary>
    public int Capacity { get; private set; } = 1;
    /// <summary>Bees on the board, counting stacks.</summary>
    public int BeeTotal { get; private set; }
    /// <summary>Cells holding at least one bee: the cells a player must never open.</summary>
    public int BeeCellCount { get; private set; }
    public int RevealedSafeCount { get; private set; }
    /// <summary>Flagged bees, counting stacks. Never exceeds <see cref="BeeTotal"/>.</summary>
    public int FlagSum { get; private set; }
    public IReadOnlyList<byte> Bees => _bees;
    public IReadOnlyList<bool> Revealed => _revealed;
    public IReadOnlyList<byte> Flags => _flags;
    public IReadOnlyList<bool> Marked => _marked;
    public IReadOnlyList<byte> Clues => _clues;

    public KabloomBoard(KabloomGrid grid)
    {
        Grid = grid;
        _bees = new byte[grid.Cells.Count];
        _revealed = new bool[grid.Cells.Count];
        _flags = new byte[grid.Cells.Count];
        _marked = new bool[grid.Cells.Count];
        _clues = new byte[grid.Cells.Count];
    }

    public bool IsMine(int cell) => Valid(cell) && _bees[cell] > 0;
    public int BeeCount(int cell) => Valid(cell) ? _bees[cell] : 0;
    public bool IsRevealed(int cell) => Valid(cell) && _revealed[cell];
    public bool IsFlagged(int cell) => Valid(cell) && _flags[cell] > 0;
    public int FlagCount(int cell) => Valid(cell) ? _flags[cell] : 0;
    public bool IsMarked(int cell) => Valid(cell) && _marked[cell];
    /// <summary>Either mark: the cell is held back from every reveal path until it is cleared.</summary>
    private bool Held(int cell) => _flags[cell] > 0 || _marked[cell];
    public int Clue(int cell) => Valid(cell) ? _clues[cell] : 0;

    /// <summary>Classic planting: one bee in each listed cell, capacity 1.</summary>
    public void Plant(IEnumerable<int> mines, int firstCell)
    {
        var bees = new byte[Grid.Cells.Count];
        foreach (int mine in mines)
        {
            if (!Valid(mine)) throw new ArgumentOutOfRangeException(nameof(mines));
            bees[mine] = 1;
        }
        Plant(bees, firstCell, 1);
    }

    public void Plant(IReadOnlyList<byte> bees, int firstCell, int capacity)
    {
        if (!Valid(firstCell)) throw new ArgumentOutOfRangeException(nameof(firstCell));
        if (bees.Count != Grid.Cells.Count) throw new ArgumentException("bee counts do not fit the crop", nameof(bees));
        if (capacity < 1 || capacity > 3) throw new ArgumentOutOfRangeException(nameof(capacity));
        for (int i = 0; i < bees.Count; i++)
            if (bees[i] > capacity) throw new ArgumentOutOfRangeException(nameof(bees), "a cell holds more bees than the capacity");

        Array.Clear(_revealed);
        Array.Clear(_flags);
        Array.Clear(_marked);
        for (int i = 0; i < _bees.Length; i++) _bees[i] = bees[i];

        if (_bees[firstCell] > 0 || Grid.Cells[firstCell].Neighbors.Any(n => _bees[n] > 0))
            throw new InvalidOperationException("Kabloom first-cell protection was violated.");

        Capacity = capacity;
        Recount();
        FirstCell = firstCell;
        RecomputeClues();
        RevealedSafeCount = 0;
        FlagSum = 0;
        Phase = KabloomBoardPhase.Playing;
    }

    public KabloomActionResult Reveal(int cell)
    {
        if (Phase != KabloomBoardPhase.Playing || !Valid(cell) || Held(cell) || _revealed[cell])
            return new KabloomActionResult(KabloomActionKind.Guarded);

        if (_bees[cell] > 0)
        {
            _revealed[cell] = true;
            Phase = KabloomBoardPhase.Failed;
            return new KabloomActionResult(KabloomActionKind.Failed, 1);
        }

        int changed = RevealSafeRegion(cell);
        return FinishOr(new KabloomActionResult(KabloomActionKind.Revealed, changed));
    }

    /// <summary>Step a covered cell's flag count: clear → 1 → … → <see cref="Capacity"/> → clear. A question
    /// mark is replaced by a flag of 1. ⚠ An increment that would make flagged bees outnumber the bees on the
    /// board is refused and the cycle wraps to clear instead — the remaining-bee readout never goes negative.</summary>
    public KabloomActionResult CycleFlag(int cell)
    {
        if (Phase != KabloomBoardPhase.Playing || !Valid(cell) || _revealed[cell])
            return new KabloomActionResult(KabloomActionKind.Guarded);
        if (_marked[cell])
        {
            _marked[cell] = false;
            if (FlagSum + 1 > BeeTotal) return new KabloomActionResult(KabloomActionKind.Unmarked, 1);
            _flags[cell] = 1; FlagSum++;
            return new KabloomActionResult(KabloomActionKind.Flagged, 1);
        }
        int current = _flags[cell];
        if (current >= Capacity || FlagSum + 1 > BeeTotal)
        {
            FlagSum -= current;
            _flags[cell] = 0;
            return new KabloomActionResult(current > 0 ? KabloomActionKind.Unflagged : KabloomActionKind.Guarded, current > 0 ? 1 : 0);
        }
        _flags[cell] = (byte)(current + 1); FlagSum++;
        return new KabloomActionResult(KabloomActionKind.Flagged, 1);
    }

    /// <summary>Toggle the question mark. Setting it clears any flag on the cell.</summary>
    public KabloomActionResult ToggleQuestion(int cell)
    {
        if (Phase != KabloomBoardPhase.Playing || !Valid(cell) || _revealed[cell])
            return new KabloomActionResult(KabloomActionKind.Guarded);
        if (_marked[cell])
        {
            _marked[cell] = false;
            return new KabloomActionResult(KabloomActionKind.Unmarked, 1);
        }
        FlagSum -= _flags[cell];
        _flags[cell] = 0;
        _marked[cell] = true;
        return new KabloomActionResult(KabloomActionKind.Marked, 1);
    }

    public KabloomActionResult Chord(int cell)
    {
        if (Phase != KabloomBoardPhase.Playing || !Valid(cell) || !_revealed[cell] || _bees[cell] > 0)
            return new KabloomActionResult(KabloomActionKind.Guarded);

        int[] neighbors = Grid.Cells[cell].Neighbors.ToArray();
        int flagged = 0;
        foreach (int n in neighbors) flagged += _flags[n];
        if (flagged != _clues[cell])
            return new KabloomActionResult(KabloomActionKind.Guarded);

        int changed = 0;
        foreach (int neighbor in neighbors)
        {
            if (_revealed[neighbor] || Held(neighbor)) continue;
            if (_bees[neighbor] > 0)
            {
                _revealed[neighbor] = true;
                Phase = KabloomBoardPhase.Failed;
                return new KabloomActionResult(KabloomActionKind.Failed, changed + 1);
            }
            changed += RevealSafeRegion(neighbor);
        }
        return FinishOr(new KabloomActionResult(KabloomActionKind.Chorded, changed));
    }

    public void Retry()
    {
        if (BeeTotal <= 0 || FirstCell < 0) return;
        Array.Clear(_revealed);
        Array.Clear(_flags);
        Array.Clear(_marked);
        RevealedSafeCount = 0;
        FlagSum = 0;
        Phase = KabloomBoardPhase.Playing;
        RevealSafeRegion(FirstCell);
        FinishOr(default);
    }

    /// <param name="marked">Question marks; null (a snapshot from before they existed) restores none.</param>
    public bool RestoreState(byte[] bees, bool[] revealed, byte[] flags, bool[]? marked, int firstCell,
                             KabloomBoardPhase phase, int capacity)
    {
        int n = Grid.Cells.Count;
        if (bees.Length != n || revealed.Length != n || flags.Length != n) return false;
        if (capacity < 1 || capacity > 3) return false;
        marked ??= new bool[n];
        if (marked.Length != n) return false;
        for (int i = 0; i < n; i++) if (bees[i] > capacity || flags[i] > capacity) return false;
        if (phase == KabloomBoardPhase.Unplanted)
        {
            if (bees.Any(b => b > 0) || revealed.Any(r => r) || flags.Any(f => f > 0) || marked.Any(m => m)) return false;
            Array.Clear(_bees); Array.Clear(_revealed); Array.Clear(_flags); Array.Clear(_marked); Array.Clear(_clues);
            Capacity = capacity;
            BeeTotal = BeeCellCount = RevealedSafeCount = FlagSum = 0; FirstCell = -1; Phase = phase;
            return true;
        }
        if (!Valid(firstCell)) return false;
        if (revealed.Where((value, i) => value && (flags[i] > 0 || marked[i])).Any()) return false;
        if (flags.Where((value, i) => value > 0 && marked[i]).Any()) return false;
        if (bees[firstCell] > 0 || Grid.Cells[firstCell].Neighbors.Any(x => bees[x] > 0)) return false;

        bees.CopyTo(_bees, 0);
        revealed.CopyTo(_revealed, 0);
        flags.CopyTo(_flags, 0);
        marked.CopyTo(_marked, 0);
        Capacity = capacity;
        FirstCell = firstCell;
        Recount();
        RevealedSafeCount = Enumerable.Range(0, n).Count(i => _revealed[i] && _bees[i] == 0);
        FlagSum = _flags.Sum(f => f);
        // More flagged bees than bees cannot be placed in play; a save claiming it is not one this board wrote.
        if (FlagSum > BeeTotal) return false;
        RecomputeClues();

        int safeCount = n - BeeCellCount;
        if (phase == KabloomBoardPhase.Cleared && RevealedSafeCount != safeCount) return false;
        if (phase == KabloomBoardPhase.Playing && RevealedSafeCount >= safeCount) return false;
        if (phase != KabloomBoardPhase.Failed && Enumerable.Range(0, n).Any(i => _revealed[i] && _bees[i] > 0))
            return false;
        Phase = phase;
        return true;
    }

    private void Recount()
    {
        BeeTotal = 0; BeeCellCount = 0;
        foreach (byte b in _bees) { BeeTotal += b; if (b > 0) BeeCellCount++; }
    }

    private int RevealSafeRegion(int start)
    {
        if (_bees[start] > 0 || _revealed[start] || Held(start)) return 0;
        int changed = 0;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            if (_revealed[cell] || Held(cell) || _bees[cell] > 0) continue;
            _revealed[cell] = true;
            RevealedSafeCount++;
            changed++;
            if (_clues[cell] != 0) continue;
            foreach (int neighbor in Grid.Cells[cell].Neighbors)
                if (!_revealed[neighbor] && !Held(neighbor) && _bees[neighbor] == 0) queue.Enqueue(neighbor);
        }
        return changed;
    }

    private KabloomActionResult FinishOr(KabloomActionResult result)
    {
        if (Phase == KabloomBoardPhase.Playing && RevealedSafeCount == Grid.Cells.Count - BeeCellCount)
        {
            Phase = KabloomBoardPhase.Cleared;
            return new KabloomActionResult(KabloomActionKind.Cleared, result.ChangedCells);
        }
        return result;
    }

    private void RecomputeClues()
    {
        for (int cell = 0; cell < _clues.Length; cell++)
        {
            int sum = 0;
            foreach (int n in Grid.Cells[cell].Neighbors) sum += _bees[n];
            _clues[cell] = (byte)sum;
        }
    }

    private bool Valid(int cell) => cell >= 0 && cell < Grid.Cells.Count;
}
