namespace ControllerWheel;

/// <summary>One playable pentagon. Neighbors are shared-edge only and are ordered by polygon edge.</summary>
public sealed record KabloomCell(
    int Id,
    int FlowerQ,
    int FlowerR,
    int Petal,
    Vec2 Center,
    Vec2[] Vertices,
    int[] NeighborsByEdge)
{
    /// <summary>Materialised once: every hot path — reveal cascades, clue recounts, chords, and the solver's
    /// generation loop — enumerates this, and a LINQ iterator per enumeration was millions of tiny
    /// allocations per generation.</summary>
    public int[] Neighbors { get; } = NeighborsByEdge.Where(n => n >= 0).ToArray();
    public int Degree => Neighbors.Length;
}

/// <summary>A fully surrounded six-petal opening and the exact playable cells touching its collectible.</summary>
public sealed record KabloomCenterCap(Vec2 Center, int[] TouchingCells)
{
    public int VisiblePetals => TouchingCells.Length;
}

/// <summary>
/// Deterministic 6-fold pentille (floret) patch, cropped to whole cells inside a disc. The construction uses
/// a type-5 pentagon with one 60 degree angle, four 120 degree angles, two long edges and three short edges.
/// Six petals share their long edges around each flower center; short edges join neighboring flowers.
/// </summary>
public sealed class KabloomGrid
{
    private const double EdgeQuantization = 1_000_000.0;
    private static readonly double Unit = Math.Sqrt(75 * 75 + 26 * 26);
    private static readonly Vec2 P = new(75 / Unit, -26 / Unit);
    private static readonly Vec2 Q = new(60 / Unit, 52 / Unit);
    private static readonly Vec2 R = new(-15 / Unit, 78 / Unit);

    public IReadOnlyList<KabloomCell> Cells { get; }
    public IReadOnlyList<KabloomCenterCap> CenterCaps { get; }
    public double Radius { get; }
    public int RequestedCellCount { get; }

    private KabloomGrid(List<KabloomCell> cells, List<KabloomCenterCap> caps, double radius, int requested)
    {
        Cells = cells;
        CenterCaps = caps;
        Radius = radius;
        RequestedCellCount = requested;
    }

    private sealed record RawCell(int Q, int R, int Petal, Vec2 Center, Vec2[] Vertices)
    {
        public int[] NeighborsByEdge { get; } = [-1, -1, -1, -1, -1];
        public double FurthestVertex => Vertices.Max(v => v.Length);
    }

    private readonly record struct EdgeKey(long Ax, long Ay, long Bx, long By)
    {
        public static EdgeKey From(Vec2 a, Vec2 b)
        {
            long ax = (long)Math.Round(a.X * EdgeQuantization);
            long ay = (long)Math.Round(a.Y * EdgeQuantization);
            long bx = (long)Math.Round(b.X * EdgeQuantization);
            long by = (long)Math.Round(b.Y * EdgeQuantization);
            return ax < bx || (ax == bx && ay <= by)
                ? new EdgeKey(ax, ay, bx, by)
                : new EdgeKey(bx, by, ax, ay);
        }
    }

    /// <summary>A crop is a pure function of its target cell count (~50 distinct values across the campaign)
    /// and is immutable once validated, so it is built once per count and shared, rather than rebuilt from
    /// scratch — dozens of candidate radii, each pruned and BFS-checked per peel — on the UI thread on every
    /// level load, including the dismiss path (a mine hit's fallback level) right before the cabinet shot.</summary>
    public static KabloomGrid Create(int requestedCellCount)
    {
        lock (Crops)
        {
            if (Crops.TryGetValue(requestedCellCount, out var cached)) return cached;
        }
        var built = Build(requestedCellCount);
        lock (Crops) { Crops[requestedCellCount] = built; }
        return built;
    }

    private static readonly Dictionary<int, KabloomGrid> Crops = new();

    private static KabloomGrid Build(int requestedCellCount)
    {
        int target = Math.Clamp(requestedCellCount, 19, 240);
        double tileArea = PolygonArea(BasePentagon());
        double idealRadius = Math.Sqrt(target * tileArea / Math.PI);
        List<RawCell> raw = GeneratePatch(idealRadius * 1.48 + 5.0);
        LinkSharedEdges(raw);

        HashSet<int>? best = null;
        double bestRadius = 0;
        int bestDelta = int.MaxValue;

        // Whole-cell circular crops are quantized. Search around the equal-area estimate and choose the
        // nearest valid crop after low-degree pruning and connected-component selection.
        double[] candidateRadii = raw.Select(cell => Math.Round(cell.FurthestVertex, 8))
            .Where(radius => radius >= idealRadius * 0.58 && radius <= idealRadius * 1.62)
            .Distinct().Order().ToArray();
        foreach (double radius in candidateRadii)
        {
            var kept = new HashSet<int>(Enumerable.Range(0, raw.Count)
                .Where(index => raw[index].FurthestVertex <= radius + 1e-8));
            PruneLowDegree(raw, kept);
            KeepCentralComponent(raw, kept);
            if (kept.Count == 0) continue;
            if (kept.Count > target) TrimBoundaryToTarget(raw, kept, target);

            int delta = Math.Abs(kept.Count - target);
            if (delta < bestDelta || (delta == bestDelta && kept.Count > (best?.Count ?? 0)))
            {
                best = kept;
                bestRadius = radius;
                bestDelta = delta;
            }
        }

        if (best is null || best.Count == 0)
            throw new InvalidOperationException("Kabloom could not form a valid circular floret crop.");

        int[] remap = Enumerable.Repeat(-1, raw.Count).ToArray();
        int next = 0;
        foreach (int old in best.OrderBy(i => raw[i].Center.Length).ThenBy(i => raw[i].Q)
                     .ThenBy(i => raw[i].R).ThenBy(i => raw[i].Petal))
            remap[old] = next++;

        var cells = new KabloomCell[best.Count];
        foreach (int old in best)
        {
            RawCell source = raw[old];
            int id = remap[old];
            int[] neighbors = source.NeighborsByEdge
                .Select(n => n >= 0 && best.Contains(n) ? remap[n] : -1).ToArray();
            cells[id] = new KabloomCell(id, source.Q, source.R, source.Petal, source.Center,
                [.. source.Vertices], neighbors);
        }

        var caps = cells.GroupBy(c => (c.FlowerQ, c.FlowerR))
            .Select(group => new KabloomCenterCap(group.First().Vertices[0],
                [.. group.OrderBy(cell => cell.Petal).Select(cell => cell.Id)]))
            .Where(cap => cap.VisiblePetals == 6
                          && cap.Center.Length < bestRadius + KabloomTuning.CenterCapRadiusInGridUnits)
            .OrderBy(cap => cap.Center.Length)
            .ToList();

        double actualRadius = cells.SelectMany(c => c.Vertices).Max(v => v.Length);
        var grid = new KabloomGrid([.. cells], caps, actualRadius, requestedCellCount);
        grid.Validate();
        return grid;
    }

    public void Validate()
    {
        if (Cells.Count == 0) throw new InvalidOperationException("Kabloom grid is empty.");
        foreach (KabloomCell cell in Cells)
        {
            if (cell.Id < 0 || cell.Id >= Cells.Count || cell.Vertices.Length != 5 || cell.NeighborsByEdge.Length != 5)
                throw new InvalidOperationException("Kabloom cell data is malformed.");
            if (cell.Degree < 2 || cell.Degree > 5)
                throw new InvalidOperationException($"Kabloom cell {cell.Id} has invalid degree {cell.Degree}.");
            if (cell.Neighbors.Distinct().Count() != cell.Degree || cell.Neighbors.Contains(cell.Id))
                throw new InvalidOperationException($"Kabloom cell {cell.Id} has duplicate or self adjacency.");
            foreach (int neighbor in cell.Neighbors)
                if (!Cells[neighbor].Neighbors.Contains(cell.Id))
                    throw new InvalidOperationException($"Kabloom adjacency {cell.Id}<->{neighbor} is asymmetric.");
        }

        var visited = new HashSet<int> { 0 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
            foreach (int neighbor in Cells[queue.Dequeue()].Neighbors)
                if (visited.Add(neighbor)) queue.Enqueue(neighbor);
        if (visited.Count != Cells.Count) throw new InvalidOperationException("Kabloom crop is disconnected.");
    }

    private static Vec2[] BasePentagon() =>
    [
        new(0, 0),
        R * 2,
        R * 2 + Q,
        Q * 2 + R,
        Q * 2,
    ];

    private static List<RawCell> GeneratePatch(double patchRadius)
    {
        var result = new List<RawCell>();
        double flowerSpacing = 364 / Unit;
        int extent = (int)Math.Ceiling(patchRadius / flowerSpacing) + 3;
        for (int qIndex = -extent; qIndex <= extent; qIndex++)
        for (int rIndex = -extent; rIndex <= extent; rIndex++)
        {
            double cx = 315.0 / Unit * qIndex;
            double cy = -364.0 / Unit * rIndex + (qIndex % 2 != 0 ? 182.0 / Unit : 0);
            var flower = new Vec2(cx, cy);
            if (flower.Length > patchRadius + flowerSpacing) continue;

            Vec2[][] petals =
            [
                [flower, flower + R * 2, flower + R * 2 + Q, flower + Q * 2 + R, flower + Q * 2],
                [flower, flower + Q * 2, flower + Q * 2 + P, flower + P * 2 + Q, flower + P * 2],
                [flower, flower + P * 2, flower + P * 2 - R, flower - R * 2 + P, flower - R * 2],
                [flower, flower - R * 2, flower - R * 2 - Q, flower - Q * 2 - R, flower - Q * 2],
                [flower, flower - Q * 2, flower - Q * 2 - P, flower - P * 2 - Q, flower - P * 2],
                [flower, flower - P * 2, flower - P * 2 + R, flower + R * 2 - P, flower + R * 2],
            ];

            for (int petal = 0; petal < petals.Length; petal++)
            {
                Vec2[] vertices = petals[petal];
                var center = new Vec2(vertices.Average(v => v.X), vertices.Average(v => v.Y));
                result.Add(new RawCell(qIndex, rIndex, petal, center, vertices));
            }
        }
        return result;
    }

    private static void LinkSharedEdges(List<RawCell> cells)
    {
        var edges = new Dictionary<EdgeKey, List<(int Cell, int Edge)>>();
        for (int cell = 0; cell < cells.Count; cell++)
        for (int edge = 0; edge < 5; edge++)
        {
            EdgeKey key = EdgeKey.From(cells[cell].Vertices[edge], cells[cell].Vertices[(edge + 1) % 5]);
            if (!edges.TryGetValue(key, out var owners)) edges[key] = owners = [];
            owners.Add((cell, edge));
        }

        foreach (List<(int Cell, int Edge)> owners in edges.Values)
        {
            if (owners.Count != 2) continue; // outside the deliberately oversized source patch
            (int a, int ae) = owners[0];
            (int b, int be) = owners[1];
            cells[a].NeighborsByEdge[ae] = b;
            cells[b].NeighborsByEdge[be] = a;
        }
    }

    private static void PruneLowDegree(List<RawCell> cells, HashSet<int> kept)
    {
        var degree = new Dictionary<int, int>(kept.Count);
        var queue = new Queue<int>();
        foreach (int cell in kept)
        {
            int value = cells[cell].NeighborsByEdge.Count(kept.Contains);
            degree[cell] = value;
            if (value < 2) queue.Enqueue(cell);
        }
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            if (!kept.Contains(cell) || degree[cell] >= 2) continue;
            kept.Remove(cell);
            foreach (int neighbor in cells[cell].NeighborsByEdge)
            {
                if (neighbor < 0 || !kept.Contains(neighbor)) continue;
                degree[neighbor]--;
                if (degree[neighbor] == 1) queue.Enqueue(neighbor);
            }
        }
    }

    private static void KeepCentralComponent(List<RawCell> cells, HashSet<int> kept)
    {
        if (kept.Count == 0) return;
        int seed = kept.MinBy(i => cells[i].Center.Length);
        var component = new HashSet<int> { seed };
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        while (queue.Count > 0)
            foreach (int neighbor in cells[queue.Dequeue()].NeighborsByEdge)
                if (neighbor >= 0 && kept.Contains(neighbor) && component.Add(neighbor)) queue.Enqueue(neighbor);
        kept.IntersectWith(component);
    }

    /// <summary>
    /// Radial floret crops arrive in coarse symmetry tiers. Peel individually safe rim petals so adjacent
    /// campaign levels can add a few real cells while retaining a connected graph with degree at least two.
    /// </summary>
    private static void TrimBoundaryToTarget(List<RawCell> cells, HashSet<int> kept, int target)
    {
        while (kept.Count > target)
        {
            var degree = kept.ToDictionary(cell => cell,
                cell => cells[cell].NeighborsByEdge.Count(kept.Contains));
            int removable = kept
                .Where(cell => cells[cell].NeighborsByEdge.Any(neighbor => neighbor < 0 || !kept.Contains(neighbor)))
                .Where(cell => degree[cell] >= 2)
                .Where(cell => cells[cell].NeighborsByEdge
                    .Where(kept.Contains).All(neighbor => degree[neighbor] >= 3))
                .OrderByDescending(cell => cells[cell].Center.Length)
                .ThenBy(cell => cells[cell].Q)
                .ThenBy(cell => cells[cell].R)
                .ThenBy(cell => cells[cell].Petal)
                .FirstOrDefault(cell => ConnectedWithout(cells, kept, cell), -1);
            if (removable < 0) break;
            kept.Remove(removable);
        }
    }

    private static bool ConnectedWithout(List<RawCell> cells, HashSet<int> kept, int removed)
    {
        int seed = kept.FirstOrDefault(cell => cell != removed, -1);
        if (seed < 0) return false;
        var visited = new HashSet<int> { seed };
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        while (queue.Count > 0)
        {
            foreach (int neighbor in cells[queue.Dequeue()].NeighborsByEdge)
                if (neighbor >= 0 && neighbor != removed && kept.Contains(neighbor) && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
        }
        return visited.Count == kept.Count - 1;
    }

    private static double PolygonArea(IReadOnlyList<Vec2> p)
    {
        double twice = 0;
        for (int i = 0; i < p.Count; i++)
            twice += p[i].X * p[(i + 1) % p.Count].Y - p[(i + 1) % p.Count].X * p[i].Y;
        return Math.Abs(twice) * 0.5;
    }
}
