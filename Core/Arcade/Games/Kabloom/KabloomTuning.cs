namespace ControllerWheel;

/// <summary>Kabloom's independently tunable feel and generation limits. These live beside the game while it
/// remains unregistered; moving stable values into the shared Arcade tuning surface is an integration
/// task.</summary>
public static class KabloomTuning
{
    // The stick drives a free reticle; the d-pad steps one cell per press.
    /// <summary>Below this deflection the reticle doesn't move at all. Low, because a pointer should answer a
    /// nudge.</summary>
    public static double PointerDeadzone = 0.16;
    /// <summary>Top speed in board radii per second, so the feel is the same on a 40-cell crop and a 240-cell
    /// one. A full-tilt sweep crosses the diameter in about 1.2 s.</summary>
    public static double PointerRadiiPerSecond = 1.683;
    /// <summary>How fast the reticle moves on the last level relative to level 1, eased in linearly by level.
    /// The speed above is in board radii, so a late crop already covers more cells per second; this pulls
    /// that back so the later, denser boards aim finer.</summary>
    public static double PointerFinalLevelSpeedFraction = 0.5;
    /// <summary>How far out the reticle may travel, as a fraction of the board radius. Slightly over 1 so the
    /// outermost ring of cells is comfortably reachable rather than needing a pixel-perfect stop.</summary>
    public static double PointerReachFraction = 1.06;


    /// <summary>Laps per second the two focus sparks make around the selected cell. Keep it slow — this is
    /// ambient life, not an attention-grab competing with the reveal animations.</summary>
    public static double FocusSparkRevolutionsPerSecond = 0.35;
    /// <summary>Radius of a spark head, in board units (the renderer scales it with the cell).</summary>
    public static double FocusSparkRadius = 2.6;
    /// <summary>Spark opacity. Kept low so they don't read as a second selection indicator competing with the
    /// cursor on the same tile.</summary>
    public static double FocusSparkOpacity = 0.25;
    public static double CompletionInputDelaySeconds = 0.450;
    /// <summary>How long a cleared board sits finished before the level transition takes over. Without it the
    /// flip begins on the exact step the last gem lands.</summary>
    public static double CompletionHoldSeconds = 0.60;
    public static double RevealHopDelaySeconds = 0.052;
    public static double RevealMaximumDelaySeconds = 0.62;
    public static double RevealBurstSeconds = 0.36;
    public static double FlagPopSeconds = 0.30;
    /// <summary>How long □ is held before the press becomes a question mark instead of a flag step.</summary>
    public static double QuestionHoldSeconds = 0.35;
    /// <summary>How long the bees-per-petal card refuses ✕ the first time a run meets a capacity.</summary>
    public static double CapacityNoticeMinSeconds = 5.0;
    /// <summary>How long one bee takes to grow and fly off the board.
    ///
    /// <para>⚠ Gates more than the animation: <c>ScheduleAvailableDiamonds</c> won't release a gem until the
    /// bees touching it have finished, and <c>PresentationActive</c> holds the level open for the same span,
    /// so lengthening this lengthens the whole completion beat.</para></summary>
    public static double MineFinaleSeconds = 0.95;

    /// <summary>How long the bee that stung you takes to fly off its cell and park beside the results card.
    /// ⚠ Its own value, not a share of <see cref="MineFinaleSeconds"/>: that one gates PresentationActive for
    /// the bees leaving a cleared board, which in turn gates input, so slowing the sting through it held up
    /// the good ending as well and pushed the whole presentation past its budget.</summary>
    public static double StungBeeFlightSeconds = 3.5;
    public static double MineFinaleStaggerSeconds = 0.036;
    /// <summary>How much bigger a departing bee gets. Applies to the hit bee on the failure screen too.</summary>
    public static double BeeDepartureSwell = 0.75;
    /// <summary>How far a departing bee travels, in board radii. Must clear the rim, or the bee reads as
    /// having got stuck.</summary>
    public static double BeeDepartureReach = 1.45;
    /// <summary>Wiggle amplitude across the flight path, in board radii, and how many full waves it makes —
    /// the wiggle is most of what makes the departure read as an insect.</summary>
    public static double BeeWiggleAmplitude = 0.34;
    public static double BeeWiggleWaves = 3.6;
    public static double DiamondReleaseDelaySeconds = 0.07;
    /// <summary>How long a freed gem spends spinning up and swelling before it flies to the counter.</summary>
    public static double DiamondSwellSeconds = 1.0;
    /// <summary>Peak size of a gem at the end of its swell, as a multiple of its resting size. Kept well under
    /// a size that spans several cells, which reads as a drawing error rather than as a reward.
    /// ⚠ The flight's shrink starts from this number too (the gem hands off from the swell at exactly the
    /// size it ended on), so the two stay in step from the one knob; don't give the flight its own.</summary>
    public static double DiamondSwellScale = 2.7;
    public static double DiamondFlightSeconds = 0.72;
    /// <summary>How fast a resting gem turns, in degrees per second. Keep it slow — the glitter comes from
    /// facets crossing a fixed light as the stone turns, so the flashes don't need fast rotation.</summary>
    public static double GemDegreesPerSecond = 16.0;

    // ── Level transition ──
    // The board pulls away, the cleared petals flip back to covered in a wave running centre → edge, and the
    // new garden's outer ring arrives as the wave reaches it.
    /// <summary>Total length of the transition. Derived from one clock in the sim; the renderer owns the
    /// geometry.</summary>
    public static double LevelTransitionSeconds = 1.05;
    /// <summary>How long the wave takes to travel from the centre cell to the rim.</summary>
    public static double LevelTransitionWaveSeconds = 0.55;
    /// <summary>How long one petal spends flipping. Its face changes at the halfway point, edge-on.</summary>
    public static double LevelTransitionFlipSeconds = 0.34;
    /// <summary>Peak zoom-out, as a fraction of board scale.</summary>
    public static double LevelTransitionZoom = 0.14;

    /// <summary>How long the difficulty-growth notice stays up. Must outlast the flip, so the banner is
    /// readable once the board has stopped moving.</summary>
    public static double GrowthNoticeSeconds = 2.6;

    public static int GenerationAttemptsPerStep = 2;
    public static int GenerationProfileAttempts = 360;
    public static int GenerationFallbackReductions = 7;
    /// <summary>The exact pass gives up on a frontier component with more boxes than this, or once one stall has
    /// visited more enumeration nodes than the work cap. Giving up reports Undecided and emits NO facts: a cap
    /// can make the solver say "unknown", never "safe".</summary>
    public static int ExactBoxCap = 64;
    public static long ExactWorkUnitCap = 4_000_000;

    public static double CenterCapRadiusInGridUnits = 0.420;

    /// <summary>How far a covered petal and a resting nectar gem stand off the board, in grid units. The face
    /// is drawn this far up the screen and the gap below it is filled as the tile's side.</summary>
    public static double TileThicknessInGridUnits = 0.238;
}
