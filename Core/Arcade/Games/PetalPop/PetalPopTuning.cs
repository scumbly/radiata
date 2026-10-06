namespace ControllerWheel;

/// <summary>Petalpop's feel and rule knobs. Mutable statics (never consts) so
/// <see cref="ArcadeTuning.LoadOverrides"/> can rewrite them from <c>arcade-tuning.json</c> on every open.
///
/// <para>Units: distances are fractions of the playfield radius (centre 0, rim 1), speeds are radii per
/// second, angles say <c>Deg</c> where they are degrees. Anything without a unit in its name is a plain
/// factor or a count.</para></summary>
public static class PetalPopTuning
{
    // ── Playfield geometry ────────────────────────────────────────────────────
    /// <summary>Where the pentagon's corners sit. Short of 1.0 so a corner touches the bezel rather than
    /// vanishing under it.</summary>
    public static double CornerRadius = 0.94;
    /// <summary>How far each side bows at its midpoint, in playfield units: negative bows outward (away from
    /// the centre — the corners stay put and the rail between them bellies toward the rim), positive bows
    /// inward, zero is a straight rail. The rail the paddles ride, the drawn rail and the lost-ball boundary
    /// are all the same arc, so one knob moves all three. The layout floors the magnitude at 0.0005 so the arc
    /// formulas stay finite; at that value the rail is straight to within a fiftieth of a ball radius.
    /// ⚠ Inward bows made corner pockets a ball rattled in; keep this at or below zero. −0.045 is an
    /// experiment — set 0 to walk it back to straight rails.</summary>
    public static double ArcSagitta = -0.045;
    /// <summary>The first stage's side count and the last's. A stage with N sides has N levels; beating the
    /// last level of the last stage wins the run.</summary>
    public static int    SidesStart = 4;
    public static int    SidesEnd = 8;
    /// <summary>Where a gutter ball pops. The playfield's own edge.</summary>
    public static double RimRadius = 1.0;
    /// <summary>Paddle half-length at 4-1 and at 8-8; it shrinks linearly across the run's 30 levels, so the
    /// early square is forgiving and the octagon is not.</summary>
    public static double PaddleHalfLengthFirst = 0.16;
    public static double PaddleHalfLengthLast = 0.09;
    /// <summary>Half the paddle's depth. ⚠ The collider reaches this far off the spine too, so the drawn slab
    /// and the thing the ball bounces off are one and the same — don't give the renderer a thickness of its own.</summary>
    public static double PaddleHalfThickness = 0.022;
    /// <summary>The paddle is straight but leans: it turns this fraction of the way from facing straight out
    /// of its rail to facing the middle of the board. A paddle at its side's midpoint already faces the
    /// middle and so never tilts; one pushed toward a corner leans hardest, which is what stops a ball
    /// rattling in the corner — both paddles there turn their faces the same way and the wedge reads as a
    /// backboard aimed inward. ⚠ Keep it well under 1: a paddle pointing straight at the centre would
    /// return every ball back down its own approach line.</summary>
    public static double PaddleTiltFraction = 0.35;
    /// <summary>Hard cap on that lean, whatever the fraction works out to.</summary>
    public static double PaddleTiltMaxDeg = 22;
    public static double BallRadius = 0.034;
    /// <summary>Apothem of the flower's outer edge with one ring and with every ring: the footprint grows gently
    /// across a stage while the gold core inside it shrinks, so a stage's first level offers a big target without
    /// the flower filling the board, and its last is all petals around the smallest core.</summary>
    public static double FlowerApothemFirst = 0.22;
    public static double FlowerApothem = 0.31;
    /// <summary>The smallest the core gets: its apothem when every ring is present.</summary>
    public static double CoreApothem = 0.10;
    /// <summary>Radial thickness of one ring — how deep a petal is — when every ring is present. With fewer
    /// rings each is thickened by <see cref="RingPitchBoost"/> per missing ring, so a sparse flower's petals
    /// read at couch distance; whatever the rings do not take, the core does.</summary>
    public static double RingPitch = 0.035;
    public static double RingPitchBoost = 0.15;
    /// <summary>Rings per level within a stage: level k has k rings, up to this many and up to what fits
    /// inside the rails less <see cref="RailClearance"/>.</summary>
    public static int    MaxRings = 6;
    /// <summary>Room kept between the outermost petals and the closest point of a rail.</summary>
    public static double RailClearance = 0.14;
    /// <summary>Petals per side are chosen so each is about this wide along the side.</summary>
    public static double BrickTargetWidth = 0.17;
    /// <summary>A ring may cover only part of the circumference, as a rotationally symmetric pattern. The
    /// first level of a stage is always a plain full flower; from this level on, rings start taking patterns.</summary>
    public static int    PatternFromLevel = 2;
    /// <summary>Chance in a hundred that a given ring is patterned, per level past <see cref="PatternFromLevel"/>,
    /// and the ceiling on it. ⚠ Ring 0 is never patterned whatever these say — see
    /// <see cref="PetalPopFlower.Present"/>.</summary>
    public static int    PatternChancePerLevel = 25;
    public static int    PatternChanceMax = 70;

    // ── Control ───────────────────────────────────────────────────────────────
    /// <summary>0 = spring (stick direction is where every paddle sits; release springs home), 1 = rail
    /// (stick drives paddles along their rails; release leaves them). The pause menu's row persists the
    /// player's pick per game; this is only the compiled default.</summary>
    public static int    ControlMode = 0;
    public static double Deadzone = 0.12;

    // ── Corner bumpers ────────────────────────────────────────────────────────
    /// <summary>Square levels only: a bracket of two paddle-depth bars sits in each corner, one along each rail,
    /// each this fraction of a side's length at the stage's first level, shrinking by <see cref="BumperFractionStep"/>
    /// per level until it is gone. It bounces the ball like a paddle that never moves, so a beginner does not
    /// lose balls to the corners before the paddles make sense; the rest of each side stays open.</summary>
    public static int    BumperSides = 4;
    public static double BumperFractionFirst = 0.15;
    public static double BumperFractionStep = 0.05;
    /// <summary>Response curve on the relieved stick magnitude (1 = linear). Above 1 a half tilt asks for less
    /// than half the travel while a full tilt still reaches the same extreme, so fine positioning lives in the
    /// middle of the throw and the corners are still one hard push away.</summary>
    public static double StickExponent = 1.8;
    /// <summary>Multiplier on the per-shape rail gain (1/sin(π/N)) that lets a push aimed at a corner put both
    /// adjacent paddles fully into it. 1 = exactly saturating at a full push toward the corner; above 1 gives
    /// slack for an imprecise thumb.</summary>
    public static double CornerGain = 1.15;
    /// <summary>Spring model: natural frequency of the critically damped follow. Higher is snappier.</summary>
    public static double SpringHz = 6.0;
    /// <summary>Rail model: top slide speed along the rail, and how fast the paddle reaches it.</summary>
    public static double RailSpeed = 2.4;
    public static double RailAccelPerSec = 28;

    // ── Ball ──────────────────────────────────────────────────────────────────
    // Speed ramps on the overall level ordinal (4 + 5 + 6 + 7 + 8 = 30 levels across the run).
    public static double BallSpeedBase = 0.55;
    public static double BallSpeedPerLevel = 0.015;
    public static double BallSpeedCapBase = 0.85;
    public static double BallSpeedCapPerLevel = 0.012;
    public static double BallSpeedCapMax = 1.10;
    public static double SpeedPerPaddleHit = 0.02;
    /// <summary>Extra deflection from where the ball met the paddle (centre = none, tip = this much), on top
    /// of the scoop's geometry. Positive throws a tip hit toward that tip, Breakout-style; the default is
    /// zero because on this board that sends a cornered ball deeper into the corner — the scoop is the
    /// english now.</summary>
    public static double MaxEnglishDeg = 0;
    /// <summary>Deflection per unit of paddle rail speed at the moment of contact. ⚠ Keep the product
    /// with <see cref="RailSpeed"/> near ten degrees: a sliding paddle is drawn in the same place as a still
    /// one, so every degree it adds is a degree the player could not read off the screen. Enough to aim with,
    /// never enough to make a bounce surprising.</summary>
    public static double RailEnglishDegPerUnit = 6;
    public static double MaxTotalEnglishDeg = 15;
    /// <summary>Smallest inward component a ball may leave a paddle with, so it can never skate along a rail.
    /// The exit is expressed in the paddle's own frame, so this is exactly the cosine of the widest exit
    /// angle: 0.50 caps a bounce at sixty degrees off the face, and that is the ceiling on how far any input
    /// — english, or a glancing arrival — may bend a shot away from the normal.</summary>
    public static double MinInwardNormal = 0.50;
    public static int    MaxBalls = 9;
    public static double SplitSpreadDeg = 20;
    /// <summary>A lost ball's flight through the gutter is never slower than this, so the pop lands on a beat.</summary>
    public static double GutterMinSpeed = 0.6;
    /// <summary>The serve is aimed by sliding the serving paddle: its rail speed steers the launch angle at this
    /// many radians per unit of rail speed per second, and the angle then eases back toward straight out of
    /// the paddle at <see cref="ServeReturnPerSec"/> (an exponential rate — 0.5 is a ~1.4 s half-life, slow
    /// enough that a slide-then-release still holds most of its aim through the launch). The return stops
    /// at the edge of a deadzone of ±<see cref="ServeDeadzoneDeg"/> about the normal, never at the normal
    /// itself, and a fresh serve starts at a random angle inside that deadzone — so the resting aim is
    /// near-straight without ever being exactly straight up a centred paddle.</summary>
    public static double ServeSteerPerUnit = 1.6;
    public static double ServeReturnPerSec = 0.5;
    public static double ServeDeadzoneDeg = 10;
    /// <summary>The furthest a serve can be steered off the paddle normal, either way.</summary>
    public static double ServeAngleMaxDeg = 55;
    /// <summary>Sim substeps per fixed step. 1 is enough at the shipped speeds (a step moves the ball well
    /// under a paddle's thickness); raise this before adding swept tests if the cap ever climbs.</summary>
    public static int    PhysicsSubsteps = 1;

    // ── Smash (✕ as the ball meets a paddle) ──────────────────────────────────
    /// <summary>How long a ✕ press keeps the paddles armed. Early-press only: a press after the bounce misses.</summary>
    public static double SmashWindowSeconds = 0.12;
    public static double SmashCooldownSeconds = 0.35;
    /// <summary>How far every paddle surges off its rail toward the field on a ✕ press — a real move, so the
    /// slam connects with a ball it would otherwise have missed — easing back over the armed window and then
    /// settling at <see cref="LungeReturnPerSec"/> once the window closes or the hit lands.</summary>
    public static double SmashLungeDepth = 0.05;
    /// <summary>How hard the paddles bow toward the field through the slam's surge, as a multiple of the
    /// lunge fraction (1 = a full-draw slam bows the middle as far forward as a full draw bowed it back).
    /// Drawing only — see <see cref="PetalPopLayout.PaddleBend"/>.</summary>
    public static double SlamBowIn = 1.0;
    /// <summary>The slingshot: holding ✕ draws every paddle this far behind its rail over <see cref="DrawSeconds"/>;
    /// releasing springs them forward by <see cref="SmashLungeDepth"/> × the draw. A release below
    /// <see cref="MinDraw"/> is a twitch, not a smash.</summary>
    public static double DrawDepth = 0.10;
    /// <summary>Room kept between a drawn-back paddle's outer edge and the rim. ⚠ The gutter is a lens — deep
    /// at a side's midpoint, pinched to nothing at the corners — so the draw is bounded per paddle by
    /// <see cref="PetalPopLayout.MaxDrawDepth"/> rather than globally: a paddle at the middle of its rail
    /// takes the whole windup, one jammed into a corner has nowhere to go and barely moves.</summary>
    public static double DrawRimMargin = 0.02;
    /// <summary>How fast the released slingshot sweeps forward, playfield units per second. Fast enough to read
    /// as a snap; ⚠ never fast enough to move the paddle face a whole ball-and-paddle reach in one 120 Hz step,
    /// which is what let a paddle land on the far side of a ball and throw it out of bounds.</summary>
    public static double SpringPerSec = 3.2;
    public static double DrawSeconds = 0.375;
    public static double MinDraw = 0.25;
    /// <summary>Top rail speed, playfield units per second, while a swing is in progress — the windup and the
    /// armed window both. The paddles crawl rather than stand still: a slam still lands close to where it was
    /// aimed, but a late correction is possible and a paddle that stops dead reads as a dropped input.
    /// ⚠ Keep it well under <see cref="RailSpeed"/>. This is the cost of the swing, and a crawl fast enough to
    /// reposition across the rail during the windup removes it.</summary>
    public static double DrawCrawlSpeed = 0.3;
    public static double LungeReturnPerSec = 0.5;
    public static double SmashSpeedMultiplier = 1.35;
    /// <summary>How much of a smash's exit direction comes from the paddle's face rather than from the angle
    /// the ball arrived at. 0 leaves a smash reflecting like any other hit — only faster — which reads as no
    /// swing at all; 1 sends every smash straight down the face. The mirror term is scaled by what is left,
    /// so the aiming inputs (where the paddle sits, and so which way its face leans) survive.</summary>
    public static double SmashNormalPull = 0.8;
    /// <summary>A smash adds at least this much speed, so a slow ball still visibly bursts off the paddle.</summary>
    public static double SmashMinBoost = 0.25;
    /// <summary>A smashed ball may exceed the level cap by this factor, bleeding back over
    /// <see cref="SmashDecaySeconds"/>.</summary>
    public static double SmashCapMultiplier = 1.45;
    public static double SmashDecaySeconds = 1.2;
    /// <summary>Most +punch a single slam can add: the draw at release picks the tier in even steps, so a
    /// half draw adds one and a full draw adds two (<see cref="PetalPop.PunchForDraw"/>).</summary>
    public static int    SmashPunch = 2;
    /// <summary>Ceiling on the +punch one ball can carry, however many slams it collects.
    ///
    /// <para>+punch is the ball's stock of extra hits. A petal contact always deals at least one damage and
    /// spends one +punch; a petal tough enough to absorb more takes the rest of the stock as extra damage in
    /// the same contact. The ball rebounds only once the stock is empty, so a charged ball plows. Paddle
    /// contact never spends +punch, and a multiball child inherits its parent's stock.</para></summary>
    public static int    PunchMax = 6;
    public static int    SmashScore = 40;

    // ── Multiball petals ──────────────────────────────────────────────────────
    /// <summary>Share of a level's petals marked as multiball: popping one splits the ball that did it. Seeded
    /// evenly around the flower and deterministic per level, so a level's marks are part of its identity
    /// rather than a fresh roll each attempt.</summary>
    public static double MultiballFraction = 0.10;
    public static int    MultiballMin = 1;
    public static int    MultiballMax = 5;
    /// <summary>When a seeding leaves two multiball petals touching (side by side in a ring, or overlapping
    /// across neighbouring rings), the level is re-rolled with this percent chance, up to this many extra
    /// rolls; the remaining chance keeps the touching pair. Both draws come from the level's own hash, so the
    /// outcome is still deterministic in (sides, level).</summary>
    public static int    MultiballRerollPercent = 75;
    public static int    MultiballRerollMax = 12;

    // ── Pacing ────────────────────────────────────────────────────────────────
    /// <summary>Serve fires itself after this long, so a frozen-and-resumed game can never stall on a ball
    /// nobody launches. The renderer flashes the parked ball through the final <see cref="ServeWarnSeconds"/>.</summary>
    public static double ServeAutoSeconds = 10.0;
    public static double ServeWarnSeconds = 1.0;
    public static double LevelClearSeconds = 1.6;
    /// <summary>The interlude when the board changes shape. Longer than a plain level clear because the whole
    /// playfield turns once through 360° across it — see <see cref="PetalPop.StageSpinDegrees"/>.</summary>
    public static double StageClearSeconds = 1.6;
    /// <summary>Sim time scale while the last ball flies the gutter.</summary>
    public static double BallLostTimeScale = 0.30;
    public static double HitStopSeconds = 0.09;
    public static double ToughHitStopSeconds = 0.05;
    public static double SmashHitStopSeconds = 0.06;
    /// <summary>Sim time scale during a hit-stop. Not zero: a frozen frame reads as a hitch, a crawl reads as impact.</summary>
    public static double HitStopScale = 0.15;
    public static int    PlayerLives = 3;
    /// <summary>Lives granted on graduating to a new side count, and the most a player can hold.</summary>
    public static int    LivesPerStage = 1;
    public static int    MaxLives = 7;
    /// <summary>The toughness budget grows by this per level within a stage, plus <see cref="ToughPerStage"/>
    /// per stage past the first; a ring at depth k from the core takes <c>1 + floor(budget − k · ToughRingStep)</c>
    /// hits, up to <see cref="MaxHp"/>. Level 4-1 is all one-hit.</summary>
    public static double ToughPerLevel = 0.5;
    public static double ToughPerStage = 0.25;
    public static double ToughRingStep = 1.0;
    public static int    MaxHp = 3;

    // ── Score ─────────────────────────────────────────────────────────────────
    public static int    BrickBaseScore = 10;
    public static int    BrickChipScore = 5;
    public static int    CoreScore = 500;
    public static int    LevelClearBonus = 250;
    /// <summary>Petals popped without touching a paddle before the combo is shouted.</summary>
    public static int    ComboShoutAt = 3;
    public static double ShoutSeconds = 1.1;

    // ── Kinetic layer ─────────────────────────────────────────────────────────
    /// <summary>Disc twist on a life lost or a smash. The disc twists, never slides — a linear shake in a
    /// round window reads as the window breaking.</summary>
    public static double TwistImpulseDeg = 7;
    public static double TwistSmashDeg = 2.5;
    public static double TwistPaddleDeg = 0.4;
    public static double TwistDecayPerSec = 6;
    /// <summary>Scale kick of the whole disc: a popped petal, a chipped one, a smash, a life, the core.</summary>
    public static double PunchBrick = 1.012;
    public static double PunchTough = 1.025;
    public static double PunchBig = 1.06;
    public static double PunchDecayPerSec = 0.6;
    /// <summary>How long the core's bell wobble rings for after a cold ball bounces off it
    /// (<see cref="PetalPop.CoreRing"/>).</summary>
    public static double CoreRingSeconds = 0.25;
    /// <summary>Squash timers (paddle and ball) run 1 → 0 at this rate.</summary>
    public static double SquashDecayPerSec = 7;
    public static double ScorePulseDecayPerSec = 3.5;
    public static double ShardSeconds = 0.6;
    /// <summary>How long the broken core's seven pieces fly before they fade. ⚠ Keep it under the point in
    /// <see cref="LevelClearSeconds"/> where the renderer grows the next core back (72%), or the new core
    /// rises through the wreckage of the old.</summary>
    public static double CoreShardSeconds = 0.9;
    public static double BurstSeconds = 0.5;
    /// <summary>Ball trail sampling interval. The trail is presentation state the sim carries so the renderer
    /// stays a pure function of the state it is handed.</summary>
    public static double TrailSampleSeconds = 1.0 / 60.0;
}
