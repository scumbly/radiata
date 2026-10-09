namespace ControllerWheel;

/// <summary>The stages a new Connate game may start at from the pause menu, and which of them the player has
/// unlocked. Persisted in the game's own settings blob (<see cref="Connate.SerializeSettings"/>), beside the best
/// score and outliving a finished run; none of it belongs to a run, so none of it rides the run's snapshot.
///
/// <para>The start stages are 1 and the multiples of five up to the cap. Reaching a stage for the first time
/// unlocks starting there, silently, and everything below it comes with it, so <see cref="Highest"/> alone says
/// what is on offer.</para></summary>
public sealed class ConnateStarts
{
    /// <summary>Every stage a game can start at, ascending. The last is the cap stage.</summary>
    public static readonly int[] Stages = [1, 5, 10, 15, 20];

    public static bool IsStart(int stage) => Array.IndexOf(Stages, stage) >= 0;

    /// <summary>The highest start at or below <paramref name="stage"/>; 1 for anything under 5.</summary>
    public static int HighestStartAtOrBelow(int stage)
    {
        int best = 1;
        foreach (int start in Stages) if (start <= stage) best = start;
        return best;
    }

    /// <summary>The highest start a banked total has already reached.</summary>
    public static int HighestStartFor(long banked) =>
        HighestStartAtOrBelow(ConnateTuning.StageIndexFor(Math.Max(0, banked)) + 1);

    /// <summary>The highest unlocked start: 1, 5, 10, 15 or 20.</summary>
    public int Highest { get; private set; } = 1;

    /// <summary>The settings carried an explicit unlock. Until one has, <see cref="Highest"/> is derived from the
    /// stored best score, once.</summary>
    private bool _explicit;

    /// <summary>The starts the pause menu offers: every entry of <see cref="Stages"/> up to <see cref="Highest"/>.</summary>
    public int[] Offered() => [.. Stages.Where(stage => stage <= Highest)];

    /// <summary>The player has reached <paramref name="stage"/> in a run. Returns whether the highest unlocked
    /// start moved.</summary>
    public bool Reach(int stage)
    {
        int start = HighestStartAtOrBelow(stage);
        if (start <= Highest) return false;
        Highest = start;
        return true;
    }

    /// <summary>Derives the unlock for a player whose settings predate unlocks: the highest start their stored
    /// best score already reached. A no-op once the settings have carried an explicit unlock.</summary>
    public void SeedFromBest(long best)
    {
        if (_explicit) return;
        int derived = HighestStartFor(best);
        if (derived > Highest) Highest = derived;
    }

    /// <summary>Loads the stored value, treating it as hostile. <paramref name="unlocked"/> null means the
    /// settings never recorded one (keep what <see cref="SeedFromBest"/> derived); any recorded value that is not
    /// one of <see cref="Stages"/> reads as 1.</summary>
    public void Load(int? unlocked)
    {
        if (unlocked is not { } stored) return;
        _explicit = true;
        Highest = IsStart(stored) ? stored : 1;
    }
}
