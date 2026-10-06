using System.Numerics;

namespace ControllerWheel;

/// <summary>Auditable outcome of the deterministic validation solver.
///
/// <para>Trailing fields have defaults so every positional consumer keeps compiling: <paramref name="Stalls"/>
/// counts the points where tiers 1-4 ran dry and the exact pass had to decide; <paramref name="MineCountNeeded"/>
/// says the global bee count was required at least once; <paramref name="Undecided"/> means an exact pass hit
/// a work cap and gave up without emitting anything, so a board reported unsolved-and-undecided may still be
/// solvable; <paramref name="UnsoundFacts"/> must be zero — a derived fact that contradicted the truth stops
/// the solve and is counted here rather than swallowed.</para></summary>
public sealed record KabloomSolveReport(
    bool Solved,
    int OpeningRevealed,
    int OpeningForcedMoves,
    int MaximumTechnique,
    int MaximumDerivationDepth,
    int[] TechniqueFacts,
    int SolverWorkUnits,
    int RemainingUnknown,
    int Stalls = 0,
    bool MineCountNeeded = false,
    int LargestComponentBoxes = 0,
    int LargestComponentCells = 0,
    bool Undecided = false,
    int UnsoundFacts = 0,
    int Guesses = 0,
    double WorstGuessProbability = 0);

/// <summary>
/// No-guess solver used only to validate boards. Tiers 1-4 are the cheap human patterns: R1 direct saturation,
/// R2 subset difference, R3 exact overlap bounds, R4 chained derived constraints. Tier 5 is the exact pass: the
/// frontier is decomposed into witnesses (revealed clues) and boxes (unknown cells with identical witness
/// sets), each connected component is enumerated over box totals with per-witness pruning, and the global bee
/// count bounds what every component may hold — including the interior cells no clue touches. A cell is
/// decided only when every surviving distribution agrees. The pass never emits from a partial enumeration:
/// exceeding a cap reports <see cref="KabloomSolveReport.Undecided"/> instead.
///
/// <para>Cells hold 0..<c>capacity</c> bees; a clue is the sum over its neighbours. State is a [Lo, Hi] bound
/// per cell rather than a tri-state, so capacity 1 is the classic model and higher capacities need no second
/// code path. The solver reads the true bee counts only to compute the clues, to take the first-click
/// cascade, and to assert that a derived fact is sound.</para>
/// </summary>
public static class KabloomSolver
{
    private sealed record Constraint(int[] Vars, int Mines, int Depth, int Tier)
    {
        public string Signature => $"{string.Join(',', Vars)}={Mines}";
    }

    /// <summary>Exact count deduced for a cell, with the constraint that forced it (for the explanation trace).</summary>
    private readonly record struct Fact(int Value, int Tier, int Depth, string Because);

    /// <summary>Explain a position: run the deduction from an already revealed set (no first-click cascade) and
    /// write one line per applied fact to <paramref name="trace"/> — the cell, what it was proven to hold, the
    /// tier, and the constraint or pass that forced it. For reading a live board back to the player, never for
    /// certification.</summary>
    public static KabloomSolveReport Explain(IReadOnlyList<int[]> neighbours, IReadOnlyList<byte> bees,
                                              IReadOnlyList<bool> revealed, int capacity, IList<string> trace)
    {
        int count = neighbours.Count;
        if (bees.Count != count || revealed.Count != count || capacity < 1 || capacity > 3) return Failed(count);
        var state = new SolverState(neighbours, bees, capacity, 5) { Trace = trace };
        for (int i = 0; i < count; i++) if (revealed[i] && bees[i] == 0) state.MarkRevealed(i);
        int guard = count * 12;
        while (!state.Complete && guard-- > 0 && state.UnsoundFacts == 0)
        {
            if (state.ApplyConstraintFact()) continue;
            state.Stalls++;
            trace.Add($"-- stall {state.Stalls}: the human patterns are out of moves; exact pass --");
            if (state.ApplyExactPass()) continue;
            break;
        }
        return new KabloomSolveReport(state.Complete, 0, 0, state.MaximumTechnique, state.MaximumDepth,
            state.TechniqueFacts, state.WorkUnits, state.RemainingUnknown, state.Stalls, state.MineCountNeeded,
            state.LargestComponentBoxes, state.LargestComponentCells, state.Undecided, state.UnsoundFacts);
    }

    public static KabloomSolveReport Solve(KabloomGrid grid, IReadOnlyList<bool> mines, int firstCell,
                                            int maximumTechnique)
    {
        var bees = new byte[mines.Count];
        for (int i = 0; i < bees.Length; i++) bees[i] = mines[i] ? (byte)1 : (byte)0;
        return Solve(grid, bees, firstCell, maximumTechnique, 1);
    }

    public static KabloomSolveReport Solve(KabloomGrid grid, IReadOnlyList<byte> bees, int firstCell,
                                            int maximumTechnique, int capacity)
    {
        var neighbours = new int[grid.Cells.Count][];
        for (int i = 0; i < neighbours.Length; i++) neighbours[i] = [.. grid.Cells[i].Neighbors];
        return Solve(neighbours, bees, firstCell, maximumTechnique, capacity);
    }

    /// <summary>Topology-only entry: any graph, for pattern regression on synthetic grids.</summary>
    public static KabloomSolveReport Solve(IReadOnlyList<int[]> neighbours, IReadOnlyList<byte> bees, int firstCell,
                                            int maximumTechnique, int capacity)
        => Run(neighbours, bees, firstCell, maximumTechnique, capacity, countGuesses: false, guessLog: null);

    /// <summary>The reference path for a board that is not no-guess: play every deduction, and at a stall the
    /// exact pass cannot break, resolve the safest unknown cell (exact probability of holding a bee; lowest id
    /// on ties) as one guess — revealing it if it is empty, recording its count if not — and continue. The
    /// guess count is exact and order-independent because the game, not the player, chooses the cell; it is
    /// the definition a charge power-up adopts. <paramref name="guessLog"/> receives the cells in order.</summary>
    public static KabloomSolveReport SolveCountingGuesses(KabloomGrid grid, IReadOnlyList<byte> bees, int firstCell,
                                                           int capacity, IList<int>? guessLog = null)
    {
        var neighbours = new int[grid.Cells.Count][];
        for (int i = 0; i < neighbours.Length; i++) neighbours[i] = [.. grid.Cells[i].Neighbors];
        return Run(neighbours, bees, firstCell, 5, capacity, countGuesses: true, guessLog);
    }

    public static KabloomSolveReport SolveCountingGuesses(IReadOnlyList<int[]> neighbours, IReadOnlyList<byte> bees,
                                                           int firstCell, int capacity, IList<int>? guessLog = null)
        => Run(neighbours, bees, firstCell, 5, capacity, countGuesses: true, guessLog);

    private static KabloomSolveReport Run(IReadOnlyList<int[]> neighbours, IReadOnlyList<byte> bees, int firstCell,
                                          int maximumTechnique, int capacity, bool countGuesses, IList<int>? guessLog)
    {
        int count = neighbours.Count;
        if (bees.Count != count || firstCell < 0 || firstCell >= count || capacity < 1 || capacity > 3)
            return Failed(count);
        for (int i = 0; i < count; i++) if (bees[i] > capacity) return Failed(count);

        var state = new SolverState(neighbours, bees, capacity, maximumTechnique);
        if (bees[firstCell] != 0) return Failed(count);
        state.Reveal(firstCell);
        int opening = state.RevealedSafeCount;
        int openingForced = state.CountImmediateR1Facts();

        int guard = count * 12;
        while (!state.Complete && guard-- > 0 && state.UnsoundFacts == 0)
        {
            if (state.ApplyConstraintFact()) continue;
            if (state.AllowedTechnique >= 5)
            {
                state.Stalls++;
                if (state.ApplyExactPass()) continue;
                // A guess is only ever taken from a fully enumerated position: an undecided component would
                // make the probabilities, and so the count, meaningless.
                if (countGuesses && !state.Undecided && state.UnsoundFacts == 0 && state.GuessSafest(guessLog)) continue;
            }
            break;
        }

        return new KabloomSolveReport(state.Complete, opening, openingForced, state.MaximumTechnique,
            state.MaximumDepth, state.TechniqueFacts, state.WorkUnits, state.RemainingUnknown,
            state.Stalls, state.MineCountNeeded, state.LargestComponentBoxes, state.LargestComponentCells,
            state.Undecided, state.UnsoundFacts, state.Guesses, state.WorstGuessProbability);
    }

    private sealed class SolverState
    {
        public IReadOnlyList<int[]> Neighbours { get; }
        public IReadOnlyList<byte> Bees { get; }
        public int Capacity { get; }
        public byte[] Clues { get; }
        public int AllowedTechnique { get; }
        public bool[] Revealed { get; }
        /// <summary>Per-cell bee-count bounds. Revealed cells are [0,0]; an unknown cell starts at
        /// [0, Capacity]; a decided cell has Lo == Hi.</summary>
        public byte[] Lo { get; }
        public byte[] Hi { get; }
        public int RevealedSafeCount { get; private set; }
        public int MaximumTechnique { get; private set; }
        public int MaximumDepth { get; private set; }
        public int[] TechniqueFacts { get; } = new int[6];
        public int WorkUnits { get; private set; }
        public int Stalls { get; set; }
        public bool MineCountNeeded { get; private set; }
        public int LargestComponentBoxes { get; private set; }
        public int LargestComponentCells { get; private set; }
        public bool Undecided { get; private set; }
        public int UnsoundFacts { get; private set; }
        public int Guesses { get; private set; }
        public double WorstGuessProbability { get; private set; }
        /// <summary>Explanation sink; null in certification runs, which never pay for strings.</summary>
        public IList<string>? Trace { get; init; }

        /// <summary>Take a cell as already open, without the cascade: for explaining a saved position.</summary>
        public void MarkRevealed(int cell)
        {
            if (Revealed[cell] || Bees[cell] != 0) return;
            Revealed[cell] = true;
            Lo[cell] = 0; Hi[cell] = 0;
            RevealedSafeCount++;
        }

        private readonly int _beeTotal;
        private readonly int _beeCells;

        public SolverState(IReadOnlyList<int[]> neighbours, IReadOnlyList<byte> bees, int capacity, int allowedTechnique)
        {
            Neighbours = neighbours;
            Bees = bees;
            Capacity = capacity;
            AllowedTechnique = Math.Clamp(allowedTechnique, 1, 5);
            int n = neighbours.Count;
            Clues = new byte[n];
            Revealed = new bool[n];
            Lo = new byte[n];
            Hi = new byte[n];
            for (int cell = 0; cell < n; cell++)
            {
                int sum = 0;
                foreach (int nb in neighbours[cell]) sum += bees[nb];
                Clues[cell] = (byte)sum;
                Hi[cell] = (byte)capacity;
                _beeTotal += bees[cell];
                if (bees[cell] > 0) _beeCells++;
            }
        }

        public bool Complete => RevealedSafeCount == Neighbours.Count - _beeCells;

        public int RemainingUnknown
        {
            get { int r = 0; for (int i = 0; i < Lo.Length; i++) if (!Revealed[i] && Lo[i] < Hi[i]) r++; return r; }
        }

        private bool IsVariable(int cell) => !Revealed[cell] && Lo[cell] < Hi[cell];
        private int Domain(int cell) => Hi[cell] - Lo[cell];

        private int Cap(int[] vars)
        {
            int c = 0;
            foreach (int v in vars) c += Domain(v);
            return c;
        }

        public void Reveal(int start)
        {
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                if (Revealed[cell] || Bees[cell] != 0) continue;
                Revealed[cell] = true;
                Lo[cell] = 0; Hi[cell] = 0;
                RevealedSafeCount++;
                if (Clues[cell] != 0) continue;
                // A zero clue certifies every neighbour empty; the cascade is the same reveal a player gets.
                foreach (int neighbor in Neighbours[cell])
                    if (!Revealed[neighbor] && Bees[neighbor] == 0) queue.Enqueue(neighbor);
            }
        }

        public int CountImmediateR1Facts() => SaturatedFacts(BuildBaseConstraints()).Count;

        // ── Tiers 1-4 ────────────────────────────────────────────────────────

        public bool ApplyConstraintFact()
        {
            List<Constraint> constraints = BuildBaseConstraints();
            if (ApplyLowestFacts(SaturatedFacts(constraints))) return true;
            if (AllowedTechnique < 2 || constraints.Count < 2) return false;

            var signatures = constraints.ToDictionary(c => c.Signature, c => c);
            int cursor = 0;
            while (cursor < constraints.Count && constraints.Count < 320)
            {
                Constraint a = constraints[cursor++];
                int compareCount = constraints.Count;
                for (int j = 0; j < compareCount; j++)
                {
                    Constraint b = constraints[j];
                    if (ReferenceEquals(a, b)) continue;
                    WorkUnits++;

                    if (AllowedTechnique >= 2 && IsProperSubset(a.Vars, b.Vars))
                    {
                        int[] difference = Difference(b.Vars, a.Vars);
                        int depth = Math.Max(a.Depth, b.Depth) + 1;
                        int tier = a.Depth == 0 && b.Depth == 0 ? 2 : 4;
                        AddConstraint(difference, b.Mines - a.Mines, depth, tier, constraints, signatures);
                    }

                    if (AllowedTechnique >= 3 && a.Vars.Length > 1 && b.Vars.Length > 1)
                        AddOverlapDerivations(a, b, constraints, signatures);
                }

                if (ApplyLowestFacts(SaturatedFacts(constraints))) return true;
                if (cursor >= 96 && constraints.Count > 192) break;
            }
            return false;
        }

        private List<Constraint> BuildBaseConstraints()
        {
            var result = new List<Constraint>();
            var seen = new HashSet<string>();
            for (int cell = 0; cell < Revealed.Length; cell++)
            {
                if (!Revealed[cell]) continue;
                int known = 0;
                var vars = new List<int>();
                foreach (int neighbor in Neighbours[cell])
                {
                    known += Lo[neighbor];
                    if (IsVariable(neighbor)) vars.Add(neighbor);
                }
                if (vars.Count == 0) continue;
                vars.Sort();
                int[] v = [.. vars];
                var constraint = new Constraint(v, Clues[cell] - known, 0, 1);
                if (constraint.Mines < 0 || constraint.Mines > Cap(v)) continue;
                if (seen.Add(constraint.Signature)) result.Add(constraint);
            }
            return result;
        }

        private Dictionary<int, Fact> SaturatedFacts(IEnumerable<Constraint> constraints)
        {
            var facts = new Dictionary<int, Fact>();
            foreach (Constraint constraint in constraints)
            {
                if (constraint.Tier > AllowedTechnique) continue;
                bool full;
                if (constraint.Mines == 0) full = false;
                else if (constraint.Mines == Cap(constraint.Vars)) full = true;
                else continue;
                foreach (int variable in constraint.Vars)
                {
                    if (!IsVariable(variable)) continue;
                    int value = full ? Hi[variable] : Lo[variable];
                    if (!facts.TryGetValue(variable, out var old) || constraint.Tier < old.Tier)
                        facts[variable] = new Fact(value, constraint.Tier, constraint.Depth, constraint.Signature);
                }
            }
            return facts;
        }

        private bool ApplyLowestFacts(Dictionary<int, Fact> facts)
        {
            if (facts.Count == 0) return false;
            int tier = facts.Values.Min(f => f.Tier);
            if (tier > AllowedTechnique) return false;
            var batch = facts.Where(pair => pair.Value.Tier == tier).OrderBy(pair => pair.Key).ToArray();
            foreach (var (cell, fact) in batch)
            {
                // The solver's answer access ends here: an assertion protecting the generator from an unsound
                // derivation, never evidence used to choose a fact.
                if (Bees[cell] != fact.Value) { UnsoundFacts++; return false; }
                Lo[cell] = (byte)fact.Value; Hi[cell] = (byte)fact.Value;
                Trace?.Add($"tier {tier}: cell {cell} = {(fact.Value == 0 ? "empty" : fact.Value + " bee(s)")}  from {fact.Because}");
                TechniqueFacts[tier]++;
                MaximumTechnique = Math.Max(MaximumTechnique, tier);
                MaximumDepth = Math.Max(MaximumDepth, fact.Depth);
            }
            foreach (var (cell, fact) in batch)
                if (fact.Value == 0) Reveal(cell);
            return true;
        }

        private void AddOverlapDerivations(Constraint a, Constraint b, List<Constraint> constraints,
                                           Dictionary<string, Constraint> signatures)
        {
            int[] intersection = Intersect(a.Vars, b.Vars);
            if (intersection.Length == 0 || intersection.Length == a.Vars.Length || intersection.Length == b.Vars.Length)
                return;
            int[] aOnly = Difference(a.Vars, intersection);
            int[] bOnly = Difference(b.Vars, intersection);
            int lower = Math.Max(0, Math.Max(a.Mines - Cap(aOnly), b.Mines - Cap(bOnly)));
            int upper = Math.Min(Cap(intersection), Math.Min(a.Mines, b.Mines));
            if (lower != upper) return;

            int depth = Math.Max(a.Depth, b.Depth) + 1;
            int tier = a.Depth == 0 && b.Depth == 0 ? 3 : 4;
            AddConstraint(intersection, lower, depth, tier, constraints, signatures);
            AddConstraint(aOnly, a.Mines - lower, depth, tier, constraints, signatures);
            AddConstraint(bOnly, b.Mines - lower, depth, tier, constraints, signatures);
        }

        private void AddConstraint(int[] vars, int mines, int depth, int tier, List<Constraint> constraints,
                                   Dictionary<string, Constraint> signatures)
        {
            if (vars.Length == 0 || mines < 0 || mines > Cap(vars) || tier > AllowedTechnique) return;
            var candidate = new Constraint(vars, mines, depth, tier);
            if (signatures.ContainsKey(candidate.Signature)) return;
            signatures[candidate.Signature] = candidate;
            constraints.Add(candidate);
        }

        // ── Tier 5: the exact pass ───────────────────────────────────────────

        private sealed class Box
        {
            public int Id;
            public int[] Cells = [];
            public int Domain;                 // bees each cell may still take, above its Lo
            public int Cap;                    // Cells.Length * Domain
            public int[] Witnesses = [];
            public int Component;
            /// <summary>Ways to spread t bees over the box's cells (each at most Domain), and over all but one
            /// of them; only filled when the pass wants probabilities.</summary>
            public BigInteger[] WaysAll = [];
            public BigInteger[] WaysMinusOne = [];
        }

        private sealed class Witness
        {
            public int Cell;
            public int Residual;
            public List<int> Boxes = [];
        }

        /// <summary>Per component, per total-bees-in-component: what every surviving distribution agreed on.</summary>
        private sealed class Component
        {
            public int[] Boxes = [];
            public int CellCount;
            public int MaxTotal;
            public bool[] Present = [];        // indexed by m
            public bool[][] AnyNonZero = [];   // [m][box index within component]
            public bool[][] AnyNotFull = [];
            public int[][] MinT = [];
            public int[][] MaxT = [];
            public bool Decided;
            /// <summary>Weighted solution count per m, and per m and box the weight of (solution, cell in that box
            /// holding no bee) pairs — the numerator of a cell's probability of being empty.</summary>
            public BigInteger[] Count = [];
            public BigInteger[][] ZeroWeight = [];
        }

        /// <summary>One exact pass's decomposition of the current position.</summary>
        private sealed class Pass
        {
            public List<Witness> Witnesses = [];
            public List<Box> Boxes = [];
            public List<Component> Components = [];
            public List<int> Interior = [];
            public int ResidualBees;
            public int InteriorCap;
            public bool UndecidedThisPass;
            public bool AllDecided => Components.All(c => c.Decided);
        }

        public bool ApplyExactPass()
        {
            Pass pass = BuildPass(wantCounts: false);
            if (pass is null) return false;
            var components = pass.Components;
            var boxes = pass.Boxes;
            var interior = pass.Interior;
            int residualBees = pass.ResidualBees;
            int interiorCap = pass.InteriorCap;

            // Local facts: agreed over every distribution of the component, no counting.
            var facts = new Dictionary<int, (int Lo, int Hi)>();
            foreach (var comp in components)
            {
                if (!comp.Decided) continue;
                CollectBoxFacts(comp, boxes, comp.Present, facts);
            }
            if (ApplyBoundFacts(facts, counting: false)) return true;
            if (UnsoundFacts > 0) return false;

            if (!pass.AllDecided)
            {
                Undecided |= pass.UndecidedThisPass;
                return false;
            }

            // Counting facts: only the totals the global bee budget allows, and the interior with them.
            int k = components.Count;
            int width = residualBees + 1;
            var prefix = new bool[k + 1][];
            var suffix = new bool[k + 1][];
            prefix[0] = UnitSet(width);
            suffix[k] = UnitSet(width);
            for (int i = 0; i < k; i++) prefix[i + 1] = Convolve(prefix[i], components[i].Present, width);
            for (int i = k - 1; i >= 0; i--) suffix[i] = Convolve(components[i].Present, suffix[i + 1], width);

            facts.Clear();
            for (int i = 0; i < k; i++)
            {
                var comp = components[i];
                bool[] others = Convolve(prefix[i], suffix[i + 1], width);
                var feasible = new bool[comp.Present.Length];
                bool any = false;
                for (int m = 0; m < comp.Present.Length; m++)
                {
                    if (!comp.Present[m] || m > residualBees) continue;
                    for (int o = 0; o + m <= residualBees; o++)
                    {
                        if (!others[o]) continue;
                        int rest = residualBees - m - o;
                        if (rest >= 0 && rest <= interiorCap) { feasible[m] = true; any = true; break; }
                    }
                }
                if (!any) continue;   // the truth is always feasible; an empty set here means a cap-free slip upstream
                CollectBoxFacts(comp, boxes, feasible, facts);
            }
            if (interior.Count > 0)
            {
                bool[] all = prefix[k];
                int minRest = int.MaxValue, maxRest = int.MinValue;
                for (int s = 0; s <= residualBees; s++)
                {
                    if (!all[s]) continue;
                    int rest = residualBees - s;
                    if (rest < 0 || rest > interiorCap) continue;
                    minRest = Math.Min(minRest, rest);
                    maxRest = Math.Max(maxRest, rest);
                }
                if (minRest <= maxRest)
                    foreach (int cell in interior)
                    {
                        int d = Domain(cell);
                        int hi = Math.Min(d, maxRest);
                        int lo = Math.Max(0, minRest - (interiorCap - d));
                        if (lo > 0 || hi < d) facts[cell] = (Lo[cell] + lo, Lo[cell] + hi);
                    }
            }
            if (ApplyBoundFacts(facts, counting: true)) return true;
            Undecided |= pass.UndecidedThisPass;
            return false;
        }

        /// <summary>Resolve the safest unknown cell as one guess. Exact probabilities: every component's weighted
        /// distribution is convolved with the others' and with the interior's ways, so the global count is in the
        /// odds. Returns false when the position is not fully enumerated or nothing is left to guess.</summary>
        public bool GuessSafest(IList<int>? log)
        {
            Pass pass = BuildPass(wantCounts: true);
            if (pass is null || !pass.AllDecided) return false;
            var comps = pass.Components;
            var boxes = pass.Boxes;
            int R = pass.ResidualBees;
            int width = R + 1;

            // Interior ways: bees over the interior cells, each within its own domain; and the same with one
            // named cell removed, for that cell's own odds.
            BigInteger[] interiorWays = WaysOver(pass.Interior.Select(Domain).ToArray(), width);

            int k = comps.Count;
            var prefix = new BigInteger[k + 1][];
            var suffix = new BigInteger[k + 1][];
            prefix[0] = UnitWeights(width);
            suffix[k] = interiorWays;
            for (int i = 0; i < k; i++) prefix[i + 1] = ConvolveWeights(prefix[i], comps[i].Count, width);
            for (int i = k - 1; i >= 0; i--) suffix[i] = ConvolveWeights(comps[i].Count, suffix[i + 1], width);
            BigInteger total = BigInteger.Zero;
            for (int s = 0; s <= R; s++) total += prefix[k][s] * interiorWays[R - s];
            if (total.IsZero) return false;

            int bestCell = -1;
            BigInteger bestNum = BigInteger.Zero, bestDen = BigInteger.One;   // probability of being empty
            void Offer(int cell, BigInteger zeroWeight, BigInteger den)
            {
                if (den.IsZero) return;
                // Prefer the largest chance of being empty; ties by lowest cell id.
                int cmp = (zeroWeight * bestDen).CompareTo(bestNum * den);
                if (bestCell < 0 || cmp > 0 || (cmp == 0 && cell < bestCell)) { bestCell = cell; bestNum = zeroWeight; bestDen = den; }
            }

            for (int i = 0; i < k; i++)
            {
                var comp = comps[i];
                BigInteger[] others = ConvolveWeights(prefix[i], suffix[i + 1], width);
                // Weight of every configuration with this component at total m: Count[m] × others[R − m].
                for (int j = 0; j < comp.Boxes.Length; j++)
                {
                    Box box = boxes[comp.Boxes[j]];
                    BigInteger zero = BigInteger.Zero;
                    for (int m = 0; m < comp.Present.Length && m <= R; m++)
                    {
                        if (!comp.Present[m]) continue;
                        zero += comp.ZeroWeight[m][j] * others[R - m];
                    }
                    foreach (int cell in box.Cells) Offer(cell, zero, total);
                }
            }
            if (pass.Interior.Count > 0)
            {
                BigInteger[] frontier = prefix[k];
                var domains = pass.Interior.Select(Domain).ToArray();
                for (int idx = 0; idx < pass.Interior.Count; idx++)
                {
                    var without = domains.Where((_, q) => q != idx).ToArray();
                    BigInteger[] waysWithout = WaysOver(without, width);
                    BigInteger zero = BigInteger.Zero;
                    for (int s = 0; s <= R; s++) zero += frontier[s] * waysWithout[R - s];
                    Offer(pass.Interior[idx], zero, total);
                }
            }
            if (bestCell < 0) return false;

            double beeProbability = 1 - (double)bestNum / (double)bestDen;
            Guesses++;
            WorstGuessProbability = Math.Max(WorstGuessProbability, beeProbability);
            log?.Add(bestCell);
            int truth = Bees[bestCell];
            Lo[bestCell] = (byte)truth; Hi[bestCell] = (byte)truth;
            if (truth == 0) Reveal(bestCell);
            return true;
        }

        private Pass? BuildPass(bool wantCounts)
        {
            var pass = new Pass();
            // Witnesses and boxes, in id order everywhere: the pass must be deterministic.
            int n = Neighbours.Count;
            var witnesses = new List<Witness>();
            var witnessIndexOfCell = new int[n];
            Array.Fill(witnessIndexOfCell, -1);
            for (int cell = 0; cell < n; cell++)
            {
                if (!Revealed[cell]) continue;
                int residual = Clues[cell];
                bool anyVar = false;
                foreach (int nb in Neighbours[cell]) { residual -= Lo[nb]; if (IsVariable(nb)) anyVar = true; }
                if (!anyVar) continue;
                witnessIndexOfCell[cell] = witnesses.Count;
                witnesses.Add(new Witness { Cell = cell, Residual = residual });
            }

            var boxes = new List<Box>();
            var boxIndex = new Dictionary<string, int>();
            var boxOfCell = new int[n];
            Array.Fill(boxOfCell, -1);
            var boxCells = new List<List<int>>();
            var interior = new List<int>();
            for (int cell = 0; cell < n; cell++)
            {
                if (!IsVariable(cell)) continue;
                var ws = new List<int>();
                foreach (int nb in Neighbours[cell])
                    if (witnessIndexOfCell[nb] >= 0) ws.Add(witnessIndexOfCell[nb]);
                if (ws.Count == 0) { interior.Add(cell); continue; }
                ws.Sort();
                string key = $"{Domain(cell)}|{string.Join(',', ws)}";
                if (!boxIndex.TryGetValue(key, out int b))
                {
                    b = boxes.Count;
                    boxIndex[key] = b;
                    boxes.Add(new Box { Id = b, Domain = Domain(cell), Witnesses = [.. ws] });
                    boxCells.Add([]);
                    foreach (int w in ws) witnesses[w].Boxes.Add(b);
                }
                boxCells[b].Add(cell);
                boxOfCell[cell] = b;
            }
            if (boxes.Count == 0 && interior.Count == 0) return null;
            for (int b = 0; b < boxes.Count; b++)
            {
                boxes[b].Cells = [.. boxCells[b]];
                boxes[b].Cap = boxes[b].Cells.Length * boxes[b].Domain;
            }

            // Components: union-find over boxes that share a witness.
            var parent = new int[boxes.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            foreach (var w in witnesses)
                for (int i = 1; i < w.Boxes.Count; i++)
                {
                    int a = Find(w.Boxes[0]), b = Find(w.Boxes[i]);
                    if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
                }
            var components = new List<Component>();
            var componentOfRoot = new Dictionary<int, int>();
            var membership = new List<List<int>>();
            for (int b = 0; b < boxes.Count; b++)
            {
                int root = Find(b);
                if (!componentOfRoot.TryGetValue(root, out int c))
                {
                    c = components.Count;
                    componentOfRoot[root] = c;
                    components.Add(new Component());
                    membership.Add([]);
                }
                membership[c].Add(b);
                boxes[b].Component = c;
            }

            for (int c = 0; c < components.Count; c++)
            {
                var comp = components[c];
                comp.Boxes = OrderByWitnessWalk(membership[c], boxes);
                comp.CellCount = comp.Boxes.Sum(b => boxes[b].Cells.Length);
                comp.MaxTotal = comp.Boxes.Sum(b => boxes[b].Cap);
                LargestComponentBoxes = Math.Max(LargestComponentBoxes, comp.Boxes.Length);
                LargestComponentCells = Math.Max(LargestComponentCells, comp.CellCount);
                if (comp.Boxes.Length > KabloomTuning.ExactBoxCap) { pass.UndecidedThisPass = true; continue; }
                if (wantCounts)
                    foreach (int b in comp.Boxes)
                    {
                        Box box = boxes[b];
                        box.WaysAll = WaysOver(Enumerable.Repeat(box.Domain, box.Cells.Length).ToArray(), box.Cap + 1);
                        box.WaysMinusOne = WaysOver(Enumerable.Repeat(box.Domain, box.Cells.Length - 1).ToArray(), box.Cap + 1);
                    }
                comp.Decided = Enumerate(comp, boxes, witnesses, wantCounts);
                if (!comp.Decided) pass.UndecidedThisPass = true;
            }

            pass.ResidualBees = _beeTotal;
            for (int cell = 0; cell < n; cell++) pass.ResidualBees -= Lo[cell];
            foreach (int cell in interior) pass.InteriorCap += Domain(cell);
            pass.Witnesses = witnesses;
            pass.Boxes = boxes;
            pass.Components = components;
            pass.Interior = interior;
            return pass;
        }

        /// <summary>Boxes in a walk that always takes the lowest-id box sharing a witness with the boxes already
        /// placed: every witness closes as early as possible, which is what makes the enumeration prune.</summary>
        private static int[] OrderByWitnessWalk(List<int> members, List<Box> boxes)
        {
            members.Sort();
            var placed = new bool[boxes.Count];
            var order = new List<int>(members.Count);
            var witnessSeen = new HashSet<int>();
            while (order.Count < members.Count)
            {
                int pick = -1;
                foreach (int b in members)
                {
                    if (placed[b]) continue;
                    if (order.Count == 0 || boxes[b].Witnesses.Any(witnessSeen.Contains)) { pick = b; break; }
                }
                if (pick < 0) foreach (int b in members) if (!placed[b]) { pick = b; break; }
                placed[pick] = true;
                order.Add(pick);
                foreach (int w in boxes[pick].Witnesses) witnessSeen.Add(w);
            }
            return [.. order];
        }

        /// <summary>Enumerate every admissible assignment of totals to the component's boxes. Returns false when a
        /// work cap was hit; nothing is recorded then.</summary>
        private bool Enumerate(Component comp, List<Box> boxes, List<Witness> witnesses, bool wantCounts)
        {
            int nb = comp.Boxes.Length;
            int width = comp.MaxTotal + 1;
            comp.Present = new bool[width];
            comp.AnyNonZero = new bool[width][];
            comp.AnyNotFull = new bool[width][];
            comp.MinT = new int[width][];
            comp.MaxT = new int[width][];
            if (wantCounts)
            {
                comp.Count = new BigInteger[width];
                comp.ZeroWeight = new BigInteger[width][];
                for (int m = 0; m < width; m++) comp.ZeroWeight[m] = new BigInteger[nb];
            }

            // Per witness: running sum and the capacity still unassigned among its boxes in this component.
            var sum = new int[witnesses.Count];
            var remaining = new int[witnesses.Count];
            foreach (int b in comp.Boxes)
                foreach (int w in boxes[b].Witnesses) remaining[w] += boxes[b].Cap;

            var t = new int[nb];
            long nodes = 0;
            bool capped = false;

            void Recurse(int i, int total)
            {
                if (capped) return;
                if (++nodes > KabloomTuning.ExactWorkUnitCap) { capped = true; return; }
                if (i == nb)
                {
                    if (!comp.Present[total])
                    {
                        comp.Present[total] = true;
                        comp.AnyNonZero[total] = new bool[nb];
                        comp.AnyNotFull[total] = new bool[nb];
                        comp.MinT[total] = new int[nb];
                        comp.MaxT[total] = new int[nb];
                        Array.Fill(comp.MinT[total], int.MaxValue);
                        Array.Fill(comp.MaxT[total], int.MinValue);
                    }
                    for (int j = 0; j < nb; j++)
                    {
                        if (t[j] > 0) comp.AnyNonZero[total][j] = true;
                        if (t[j] < boxes[comp.Boxes[j]].Cap) comp.AnyNotFull[total][j] = true;
                        if (t[j] < comp.MinT[total][j]) comp.MinT[total][j] = t[j];
                        if (t[j] > comp.MaxT[total][j]) comp.MaxT[total][j] = t[j];
                    }
                    if (wantCounts)
                    {
                        // Weight of this line = Π ways to place each box's total among its cells; the zero weight
                        // for box j swaps its own factor for "one named cell empty, the total among the rest".
                        BigInteger weight = BigInteger.One;
                        for (int j = 0; j < nb; j++) weight *= boxes[comp.Boxes[j]].WaysAll[t[j]];
                        comp.Count[total] += weight;
                        for (int j = 0; j < nb; j++)
                        {
                            Box bj = boxes[comp.Boxes[j]];
                            if (bj.WaysAll[t[j]].IsZero) continue;
                            comp.ZeroWeight[total][j] += weight / bj.WaysAll[t[j]] * bj.WaysMinusOne[t[j]];
                        }
                    }
                    return;
                }
                Box box = boxes[comp.Boxes[i]];
                foreach (int w in box.Witnesses) remaining[w] -= box.Cap;
                for (int v = 0; v <= box.Cap; v++)
                {
                    bool ok = true;
                    foreach (int w in box.Witnesses)
                    {
                        int s = sum[w] + v;
                        if (s > witnesses[w].Residual || s + remaining[w] < witnesses[w].Residual) { ok = false; break; }
                    }
                    if (!ok) continue;
                    foreach (int w in box.Witnesses) sum[w] += v;
                    t[i] = v;
                    Recurse(i + 1, total + v);
                    foreach (int w in box.Witnesses) sum[w] -= v;
                    if (capped) break;
                }
                foreach (int w in box.Witnesses) remaining[w] += box.Cap;
            }

            Recurse(0, 0);
            WorkUnits += (int)Math.Min(int.MaxValue, nodes);
            return !capped;
        }

        private void CollectBoxFacts(Component comp, List<Box> boxes, bool[] allowed,
                                     Dictionary<int, (int Lo, int Hi)> facts)
        {
            for (int j = 0; j < comp.Boxes.Length; j++)
            {
                Box box = boxes[comp.Boxes[j]];
                bool anyNonZero = false, anyNotFull = false;
                int minT = int.MaxValue, maxT = int.MinValue;
                for (int m = 0; m < comp.Present.Length; m++)
                {
                    if (!comp.Present[m] || !allowed[m]) continue;
                    anyNonZero |= comp.AnyNonZero[m][j];
                    anyNotFull |= comp.AnyNotFull[m][j];
                    minT = Math.Min(minT, comp.MinT[m][j]);
                    maxT = Math.Max(maxT, comp.MaxT[m][j]);
                }
                if (minT > maxT) continue;
                int d = box.Domain;
                int hi = anyNonZero ? Math.Min(d, maxT) : 0;
                int lo = anyNotFull ? Math.Max(0, minT - (box.Cap - d)) : d;
                if (lo == 0 && hi == d) continue;
                foreach (int cell in box.Cells)
                {
                    var bound = (Lo[cell] + lo, Lo[cell] + hi);
                    if (!facts.TryGetValue(cell, out var old)) facts[cell] = bound;
                    else facts[cell] = (Math.Max(old.Lo, bound.Item1), Math.Min(old.Hi, bound.Item2));
                }
            }
        }

        /// <summary>Tighten bounds. Only cells whose bounds actually move count as progress; an exact zero is
        /// revealed, an exact count is recorded, a narrowed range stays a variable.</summary>
        private bool ApplyBoundFacts(Dictionary<int, (int Lo, int Hi)> facts, bool counting)
        {
            if (facts.Count == 0) return false;
            var cells = facts.Keys.Order().ToArray();
            bool progress = false;
            var reveal = new List<int>();
            foreach (int cell in cells)
            {
                var (lo, hi) = facts[cell];
                lo = Math.Max(lo, Lo[cell]);
                hi = Math.Min(hi, Hi[cell]);
                if (lo == Lo[cell] && hi == Hi[cell]) continue;
                if (lo > hi || Bees[cell] < lo || Bees[cell] > hi) { UnsoundFacts++; return false; }
                Lo[cell] = (byte)lo; Hi[cell] = (byte)hi;
                Trace?.Add($"tier 5: cell {cell} in [{lo},{hi}]{(lo == hi ? (lo == 0 ? " = empty" : $" = {lo} bee(s)") : "")}  from {(counting ? "every arrangement that also fits the remaining bee count" : "every arrangement of its frontier component")}");
                progress = true;
                if (lo == hi)
                {
                    TechniqueFacts[5]++;
                    MaximumTechnique = 5;
                    MaximumDepth = Math.Max(MaximumDepth, 1);
                    if (hi == 0) reveal.Add(cell);
                }
            }
            if (!progress) return false;
            if (counting) MineCountNeeded = true;
            foreach (int cell in reveal) Reveal(cell);
            return true;
        }

        private static bool[] UnitSet(int width) { var u = new bool[width]; u[0] = true; return u; }

        private static BigInteger[] UnitWeights(int width)
        {
            var u = new BigInteger[width];
            u[0] = BigInteger.One;
            return u;
        }

        private static BigInteger[] ConvolveWeights(BigInteger[] a, BigInteger[] b, int width)
        {
            var result = new BigInteger[width];
            for (int i = 0; i < a.Length && i < width; i++)
            {
                if (a[i].IsZero) continue;
                for (int j = 0; j < b.Length && i + j < width; j++)
                    if (!b[j].IsZero) result[i + j] += a[i] * b[j];
            }
            return result;
        }

        /// <summary>Ways to place t bees (index) over cells with the given per-cell domains, for t below
        /// <paramref name="width"/>. No cells ⇒ one way to place zero bees.</summary>
        private static BigInteger[] WaysOver(int[] domains, int width)
        {
            var ways = UnitWeights(width);
            foreach (int d in domains)
            {
                var next = new BigInteger[width];
                for (int t = 0; t < width; t++)
                {
                    if (ways[t].IsZero) continue;
                    for (int v = 0; v <= d && t + v < width; v++) next[t + v] += ways[t];
                }
                ways = next;
            }
            return ways;
        }

        private static bool[] Convolve(bool[] a, bool[] b, int width)
        {
            var result = new bool[width];
            for (int i = 0; i < a.Length && i < width; i++)
            {
                if (!a[i]) continue;
                for (int j = 0; j < b.Length && i + j < width; j++)
                    if (b[j]) result[i + j] = true;
            }
            return result;
        }
    }

    private static bool IsProperSubset(int[] a, int[] b) => a.Length < b.Length && Difference(a, b).Length == 0;

    private static int[] Difference(int[] a, int[] b)
    {
        var bSet = new HashSet<int>(b);
        return a.Where(v => !bSet.Contains(v)).ToArray();
    }

    private static int[] Intersect(int[] a, int[] b)
    {
        var bSet = new HashSet<int>(b);
        return a.Where(bSet.Contains).ToArray();
    }

    private static KabloomSolveReport Failed(int remaining) =>
        new(false, 0, 0, 0, 0, new int[6], 0, remaining);
}
