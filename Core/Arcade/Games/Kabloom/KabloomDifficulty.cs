namespace ControllerWheel;

/// <summary>A generated level's authored targets. Tile count and reasoning tier are independent axes.</summary>
public sealed record KabloomLevelProfile(
    int Level,
    int Chapter,
    string ChapterName,
    int TargetCells,
    double MineOccupancy,
    int MinimumTechnique,
    int MaximumTechnique,
    double OpeningRevealMinimum,
    double OpeningRevealMaximum,
    int BeeCapacity = 1,
    double DoubledFraction = 0)
{
    private static readonly string[] ChapterNames =
    [
        UiText.Arcade.StageGermination, UiText.Arcade.StageSprout, UiText.Arcade.StageRosette, UiText.Arcade.StageCanopy, UiText.Arcade.StageNightBloom, UiText.Arcade.StageFullBloom,
    ];

    /// <summary>Levels on the authored curve. The campaign is one longer than this — see
    /// <see cref="ForLevel"/>'s intro level.</summary>
    public const int CurveLevels = 48;

    /// <summary>Total campaign length: the intro plus the authored curve.</summary>
    public const int TotalLevels = CurveLevels + 1;

    public static KabloomLevelProfile ForLevel(int requestedLevel)
    {
        int level = Math.Clamp(requestedLevel, 1, TotalLevels);

        // Level 1 is an introduction: the gentlest settings the curve reaches, and no special text or
        // chapter — it must read as the first level, not as a tutorial to get past.
        //
        // ⚠ 27 cells is below the 54 the comment below calls a minimum. That figure is the smallest crop
        // whose boundary cells all reach degree three; a 27-cell crop is still complete, connected and on
        // target, it just has a few degree-two edge cells. Nothing in the solver or generator requires
        // degree three — only the regular look does.
        if (level == 1)
            return new KabloomLevelProfile(1, 1, ChapterNames[0], 27, 0.150,
                MinimumTechnique: 1, MaximumTechnique: 1, OpeningRevealMinimum: 0.30,
                OpeningRevealMaximum: 0.55);

        // Everything past it indexes the authored curve, shifted by one: campaign level 2 is curve level 1.
        level -= 1;
        int chapter = (level - 1) / 8 + 1;
        int withinChapter = (level - 1) % 8;

        // Spatial progression: the same disc holds progressively more whole cells. 54 is the smallest
        // complete, connected floret crop whose boundary never drops below degree three; +4 petals per level
        // advances density alongside the independent occupancy and reasoning curves, up to the 240-cell
        // readability cap.
        int targetCells = Math.Min(240, 54 + (level - 1) * 4);

        // Cell occupancy (share of petals holding at least one bee): 16% → 18% across chapter 1, 18% → 20%
        // across chapters 2-3, and 18% → 20% again across chapters 4-6 while stacking takes over the bee count.
        // ⚠ The ~20% ceiling is measured, not provisional: on this pentagonal crop random no-guess boards all
        // but vanish above it at every capacity (census in docs/ARCADE.md ▸ Kabloom levels 11+), so past it the
        // bee count comes from bees per petal, not from more mined petals. Raising this only makes the bake
        // report uncovered cells; it cannot make boards exist.
        double occupancy = chapter switch
        {
            1 => 0.160 + 0.020 * withinChapter / 7.0,
            2 or 3 => 0.180 + 0.020 * Math.Clamp((level - 9) / 16.0, 0, 1),
            4 or 5 => 0.180 + 0.020 * Math.Clamp((level - 25) / 16.0, 0, 1),
            _ => 0.190,
        };
        // Bees per petal: single through Rosette; doubles enter with Canopy and the doubled share climbs;
        // Full Bloom triples. Measured to cost little certification at these densities (level 49 at 18%
        // cells: 20 of 300 boards certify at capacity 1, 23 at capacity 3 with three quarters tripled).
        int beeCapacity = chapter switch { <= 3 => 1, 4 or 5 => 2, _ => 3 };
        double doubledFraction = chapter switch
        {
            4 => 0.25 + 0.25 * withinChapter / 7.0,
            5 => 0.50 + 0.25 * withinChapter / 7.0,
            6 => 0.50 + 0.25 * withinChapter / 7.0,
            _ => 0,
        };

        int maximumTechnique = chapter switch
        {
            1 when level < 7 => 1,
            1 => 2,
            2 => 2,
            3 => 3,
            4 or 5 => 4,
            _ => 5,
        };
        int minimumTechnique = level switch
        {
            <= 6 => 1,
            <= 8 => 2,
            <= 16 => 2,
            <= 24 => 3,
            <= 40 => 4,
            _ => 5,
        };

        (double openingMin, double openingMax) = chapter switch
        {
            1 => (0.24, 0.48),
            2 => (0.20, 0.40),
            3 => (0.16, 0.34),
            4 => (0.13, 0.29),
            5 => (0.10, 0.25),
            _ => (0.08, 0.22),
        };

        // Reported as the campaign number (+1 undoes the shift above), so the HUD counts straight through the
        // intro without a gap.
        return new KabloomLevelProfile(level + 1, chapter, ChapterNames[chapter - 1], targetCells, occupancy,
            minimumTechnique, maximumTechnique, openingMin, openingMax, beeCapacity, doubledFraction);
    }
}
