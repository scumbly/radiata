namespace ControllerWheel;

public sealed record KabloomGenerationCandidate(
    bool[] Mines,
    int Attempt,
    int MineReduction,
    ulong CandidateSeed,
    KabloomSolveReport Report);

/// <summary>
/// Incremental deferred-placement session. A few deterministic candidates are tested per fixed step, keeping
/// generation off any independent clock and allowing the renderer to show a planting pulse.
/// </summary>
public sealed class KabloomGenerationSession
{
    private readonly KabloomGrid _grid;
    private readonly KabloomLevelProfile _profile;
    private readonly int _firstCell;
    private readonly ulong _campaignSeed;
    private KabloomGenerationCandidate? _bestSolved;
    private int _attemptInBand;

    public int Attempts { get; private set; }
    public int MineReduction { get; private set; }
    public KabloomGenerationCandidate? Accepted { get; private set; }
    public bool IsComplete => Accepted is not null;

    public KabloomGenerationSession(KabloomGrid grid, KabloomLevelProfile profile, int firstCell,
                                    ulong campaignSeed)
    {
        _grid = grid;
        _profile = profile;
        _firstCell = firstCell;
        _campaignSeed = campaignSeed == 0 ? 0xC6BC279692B5CC83UL : campaignSeed;
    }

    public void Advance(int candidateBudget)
    {
        for (int i = 0; i < Math.Max(1, candidateBudget) && Accepted is null; i++)
        {
            int attempt = Attempts++;
            int bandAttempt = _attemptInBand++;
            ulong seed = Hash(_campaignSeed, (ulong)_profile.Level, (ulong)_firstCell,
                (ulong)attempt, (ulong)MineReduction);
            KabloomGenerationCandidate candidate = GenerateCandidate(seed, attempt);
            if (candidate.Report.Solved)
            {
                if (_bestSolved is null || Rank(candidate) < Rank(_bestSolved)) _bestSolved = candidate;
                if (MeetsProfile(candidate))
                {
                    Accepted = candidate;
                    return;
                }
            }

            if (bandAttempt + 1 < KabloomTuning.GenerationProfileAttempts) continue;

            // Keep the no-guess promise authoritative. Opening-shape and exact difficulty targets are ranking
            // goals, so a fully solved candidate is a valid deterministic fallback when a profile is sparse.
            if (_bestSolved is not null)
            {
                Accepted = _bestSolved;
                return;
            }

            _attemptInBand = 0;
            MineReduction++;
            if (MineReduction <= KabloomTuning.GenerationFallbackReductions) continue;

            // A very low-density final band is preferable to ever accepting an unverified board.
            MineReduction = Math.Max(MineReduction, BaseMineCount() - 1);
        }
    }

    private KabloomGenerationCandidate GenerateCandidate(ulong seed, int attempt)
    {
        var protectedCells = new HashSet<int>(_grid.Cells[_firstCell].Neighbors) { _firstCell };
        int[] eligible = Enumerable.Range(0, _grid.Cells.Count).Where(i => !protectedCells.Contains(i)).ToArray();
        var rng = new XorShift64(seed);
        for (int i = eligible.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
        }

        int mineCount = Math.Clamp(BaseMineCount() - MineReduction, 1, Math.Max(1, eligible.Length - 1));
        var mines = new bool[_grid.Cells.Count];
        for (int i = 0; i < mineCount; i++) mines[eligible[i]] = true;
        KabloomSolveReport report = KabloomSolver.Solve(_grid, mines, _firstCell, _profile.MaximumTechnique);
        return new KabloomGenerationCandidate(mines, attempt, MineReduction, seed, report);
    }

    private bool MeetsProfile(KabloomGenerationCandidate candidate)
    {
        KabloomSolveReport report = candidate.Report;
        int safeCells = candidate.Mines.Count(m => !m);
        double openingRatio = safeCells == 0 ? 1 : report.OpeningRevealed / (double)safeCells;
        return report.Solved
               && report.OpeningForcedMoves > 0
               && report.OpeningRevealed < safeCells
               && report.MaximumTechnique >= _profile.MinimumTechnique
               && openingRatio >= _profile.OpeningRevealMinimum
               && openingRatio <= _profile.OpeningRevealMaximum;
    }

    private double Rank(KabloomGenerationCandidate candidate)
    {
        KabloomSolveReport report = candidate.Report;
        int safeCells = candidate.Mines.Count(m => !m);
        double opening = safeCells == 0 ? 1 : report.OpeningRevealed / (double)safeCells;
        double centre = (_profile.OpeningRevealMinimum + _profile.OpeningRevealMaximum) * 0.5;
        double techniquePenalty = Math.Abs(_profile.MinimumTechnique - report.MaximumTechnique) * 5;
        double openingPenalty = Math.Abs(opening - centre) * 10;
        double noMovePenalty = report.OpeningForcedMoves == 0 ? 8 : 0;
        return techniquePenalty + openingPenalty + noMovePenalty + candidate.MineReduction * 2;
    }

    private int BaseMineCount() => Math.Max(1, (int)Math.Round(_grid.Cells.Count * _profile.MineOccupancy));

    private static ulong Hash(params ulong[] values)
    {
        ulong z = 0x9E3779B97F4A7C15UL;
        foreach (ulong value in values)
        {
            z ^= value + 0x9E3779B97F4A7C15UL + (z << 6) + (z >> 2);
            z ^= z >> 30; z *= 0xBF58476D1CE4E5B9UL;
            z ^= z >> 27; z *= 0x94D049BB133111EBUL;
            z ^= z >> 31;
        }
        return z == 0 ? 0xD1B54A32D192ED03UL : z;
    }

    private struct XorShift64
    {
        private ulong _state;
        public XorShift64(ulong seed) => _state = seed == 0 ? 0xD1B54A32D192ED03UL : seed;
        public int Next(int exclusiveMaximum) => (int)(ArcadeRng.Next(ref _state) % (uint)exclusiveMaximum);
    }
}
