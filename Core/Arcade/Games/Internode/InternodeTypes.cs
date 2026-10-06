namespace ControllerWheel;

/// <summary>Run phases. <c>Intro</c> steers with the pipe frozen; <c>Checkpoint</c> is the banner beat after a
/// gate, passed or missed (the runner keeps moving, the next section is already built); <c>Falling</c> is the drop
/// through a gap in the floor, which rewinds the section. There is no failure phase: a missed quota holds the run
/// at the same section rather than ending it.</summary>
public enum InternodePhase { Intro, Running, Checkpoint, Falling }

public enum InternodeEventKind { Token, Mine, ElevatedToken }

/// <summary>Position classes the phrase bank chains on. Each names a band of (θ, ω) a phrase may enter or
/// leave in; see <see cref="InternodePhrases.Band"/>.</summary>
public enum InternodePos { Centre, LowLeft, LowRight, HighLeft, HighRight }

/// <summary><c>OverTop</c> is flight across the open top: a free particle under gravity that cannot pass outward
/// through the ring, landing where the ring is surface. Orbs there collect on angle alone; mines never do.</summary>
public enum InternodeAir { Grounded, Jumping, OverTop }

public enum InternodeShout { None, Pass, Stage, ShortBy, Ouch }
/// <summary>The row the HUD plaque grows at a gate.</summary>
public enum InternodeGateNotice { None, Checkpoint, LevelUp }
/// <summary>The plaque's bottom row: the stage went down (a fall, or a mine on an empty hand) or up (a gate
/// that ended a stage). Distinct from <see cref="InternodeGateNotice"/>, which is the top row and speaks for
/// every gate; this one speaks only when the stage number moves.</summary>
public enum InternodeStageNotice { None, Demoted, Promoted }

/// <summary>One authored event inside a phrase. <see cref="Dz"/> is seconds from the phrase start, before
/// the stage's time scale; <see cref="ThetaDeg"/> is the pipe angle (0 bottom, positive right);
/// <see cref="Height"/> is the elevated token's height above the surface in pipe radii (unused otherwise).
///
/// <para><see cref="Branch"/> names which route through a choice phrase this event belongs to. 0 is the
/// default and means "on every route" — an ordinary phrase is all zeroes. A phrase that offers a choice puts
/// its alternatives on 1, 2, … and a player takes exactly one of them, so the branches may share a plane and
/// need no spacing from each other. ⚠ Everything downstream is branch-aware because of that and has to
/// stay so: the reachability oracle proves each route separately (a token on another route is neither a
/// requirement nor an obstacle), the gate's demand counts only the best single route, and the probe applies
/// the one-token-per-plane and spacing rules per route rather than across the phrase.</para></summary>
public readonly record struct InternodeEvent(double Dz, double ThetaDeg, InternodeEventKind Kind, double Height = 0,
                                             int Branch = 0);

/// <summary>A break in the floor authored inside a phrase, at <see cref="Dz"/> seconds from its start (before
/// the stage's time scale, like <see cref="InternodeEvent.Dz"/>) and spanning the arc
/// [<see cref="ThetaMinDeg"/>, <see cref="ThetaMaxDeg"/>].
///
/// <para>⚠ This is a different thing from the gaps and planks the sequencer reserves between phrases. Those
/// are laid on empty pipe because nothing proves them: the reachability oracle works one phrase at a time,
/// so a hole outside a phrase can only be made safe by clearing the ground around it. A hole authored here
/// is inside the oracle's reach — it drives <c>InternodePhysics.Integrate</c>'s <c>floorHere</c> exactly as
/// the game does, and a state that commits to a fall dies — so the floor can be cut wherever a route through
/// it can be proven, and orbs may sit beside, between and over the holes.</para>
///
/// <para>That is what makes a phrase able to be a piece of track rather than a scatter of objects on flat
/// floor: rotating planks, parallel ribbons with alternating breaks, staircases of holes.</para>
///
/// <para>A hole may sweep: its edges run linearly from [<see cref="ThetaMinDeg"/>, <see cref="ThetaMaxDeg"/>] at
/// its start to [<see cref="ThetaMinEndDeg"/>, <see cref="ThetaMaxEndDeg"/>] at its end, so a bending strip is one
/// hole per side rather than a staircase of them. NaN end angles mean the edges hold still. Every reader of a hole's
/// angles evaluates them at a depth through <see cref="MinAt"/> and <see cref="MaxAt"/>.</para></summary>
public readonly record struct InternodeGapSpec(double Dz, double Seconds, double ThetaMinDeg, double ThetaMaxDeg,
                                               double ThetaMinEndDeg = double.NaN, double ThetaMaxEndDeg = double.NaN)
{
    public double MinEndDeg => double.IsNaN(ThetaMinEndDeg) ? ThetaMinDeg : ThetaMinEndDeg;
    public double MaxEndDeg => double.IsNaN(ThetaMaxEndDeg) ? ThetaMaxDeg : ThetaMaxEndDeg;
    public bool Swept => MinEndDeg != ThetaMinDeg || MaxEndDeg != ThetaMaxDeg;
    /// <summary>The low edge, degrees, at <paramref name="dz"/> seconds from the phrase start (clamped to the span).</summary>
    public double MinAt(double dz) => Lerp(ThetaMinDeg, MinEndDeg, dz);
    public double MaxAt(double dz) => Lerp(ThetaMaxDeg, MaxEndDeg, dz);
    private double Lerp(double a, double b, double dz)
    {
        if (a == b || Seconds <= 0) return a;
        double u = Math.Clamp((dz - Dz) / Seconds, 0, 1);
        return a + (b - a) * u;
    }
}

/// <summary>An authored formation. <see cref="Length"/> is its duration in seconds at scale 1; the next phrase
/// starts there. Entry and exit are the classes the oracle validates against.
/// <para><see cref="RimSafe"/>: the phrase never needs the open top, so the course may close the pipe over it (the
/// probe proves this with the oracle forbidding flight). <see cref="TimeLocked"/>: the spacing is real seconds and
/// the stage tempo never compresses it, because the timing is dictated by a flight the physics fixes.</para></summary>
public sealed record InternodePhrase(string Id, int Tier, InternodePos Entry, InternodePos Exit, double Length, InternodeEvent[] Events,
                                     bool RimSafe = true, bool TimeLocked = false, InternodeGapSpec[]? Gaps = null,
                                     bool Closed = false, int MinStage = 1, bool Diabolical = false)
{
    // Closed: the phrase needs a full tube — its floor runs over the top — so the sequencer places a closure
    // spanning it (ramps included), keeps the random closures off it, and the oracle proves it at rim π rather
    // than the base rim. Implies RimSafe. MinStage: never picked below this stage. Diabolical: weighted by the
    // late-run ramp in InternodeTuning rather than by tier alone.
    /// <summary>The holes this phrase cuts in its own floor; empty for the phrases that only place objects.
    /// See <see cref="InternodeGapSpec"/> for why these are proven where the sequencer's are not.</summary>
    public InternodeGapSpec[] Holes => Gaps ?? [];
}

/// <summary>Angular and angular-speed band of a position class, degrees.</summary>
public readonly record struct InternodeBand(double ThetaMinDeg, double ThetaMaxDeg, double OmegaMinDeg, double OmegaMaxDeg)
{
    public bool Contains(double thetaDeg, double omegaDeg) =>
        thetaDeg >= ThetaMinDeg && thetaDeg <= ThetaMaxDeg && omegaDeg >= OmegaMinDeg && omegaDeg <= OmegaMaxDeg;
}

/// <summary>An event placed in a section at absolute section time <see cref="Z"/>. <see cref="Theta"/> is
/// radians. <see cref="Taken"/> flips once, on the step whose plane crossing hit it.</summary>
public sealed class InternodePlacedEvent
{
    public InternodePlacedEvent(double z, double theta, InternodeEventKind kind, double height)
    {
        Z = z; Theta = theta; Kind = kind; Height = height;
    }
    public double Z { get; }
    public double Theta { get; }
    public InternodeEventKind Kind { get; }
    public double Height { get; }
    public bool Taken { get; set; }
    /// <summary>A gold orb worth <see cref="InternodeTuning.BonusTokenValue"/>. Earned, not placed: the last orb of a
    /// long phrase turns gold when every orb before it in that phrase was caught. Counts as one toward the quota's
    /// offer, so the bonus is a reward on top of the gate, never a requirement for it.</summary>
    public bool Bonus { get; set; }
    /// <summary>The last orb of a phrase long enough to pay gold, while the pattern is still clean: drawn with a gold
    /// ring from the start so the prize is visible ahead; cleared the moment an orb of that phrase gets past.</summary>
    public bool GoldCandidate { get; set; }
    /// <summary>Index into the section's phrase list of the phrase that placed this event; −1 for mines the course
    /// added itself (an armed gap). The gold rule counts a phrase's orbs by it.</summary>
    public int Phrase { get; init; } = -1;
    /// <summary>Which route through a choice phrase this orb sits on; 0 = every route. See
    /// <see cref="InternodeEvent.Branch"/>.</summary>
    public int Branch { get; init; }
    /// <summary>The plane passed without a catch. Presentation only: the renderer greys the orb as it flies past.</summary>
    public bool Missed { get; set; }
    /// <summary>The route indices this event sits on, resolved once when the section is indexed; an orb's
    /// routes never change afterwards. Replaces an iterator per lookup on the miss and catch paths.</summary>
    public int[] Routes { get; set; } = [];
}

/// <summary>A catch, for the renderer's pop: where the runner was (<see cref="Theta"/> radians, <see cref="H"/>
/// height) and what it was worth. Transient presentation.</summary>
public readonly record struct InternodeCollect(double Theta, double H, double Age, int Value);

/// <summary>The instant a pattern's last orb turned gold, drawn at that orb — which is still ahead of the runner, so
/// it carries the orb's own plane (<see cref="Z"/>), angle and height rather than a screen point. Transient
/// presentation.</summary>
public readonly record struct InternodeGoldFlash(double Z, double Theta, double Height, double Age);

/// <summary>One orb of the flight from the runner to the score readout when a gate banks the held orbs. Transient
/// presentation; <see cref="Seed"/> spreads the flight. <see cref="Gold"/> orbs are spaced evenly through the
/// flight, one per gold orb the runner was holding, so the payout shows what was cashed in.</summary>
public readonly record struct InternodeBankOrb(double Age, int Seed, bool Gold = false);

/// <summary>A lateral/vertical swing of the pipe: <c>Amp·sin²(π(z−Z0)/Seconds)</c> on each axis inside the
/// span, zero outside. Zero value and slope at both ends keep seams smooth.</summary>
public readonly record struct InternodeBend(double Z0, double Seconds, double AmpX, double AmpY);

/// <summary>A corkscrew of the pipe about its axis: a smoothstep from 0 to <see cref="TotalRad"/> across the
/// span, then held.</summary>
public readonly record struct InternodeTwist(double Z0, double Seconds, double TotalRad);

/// <summary>Where a break in the floor came from, which decides what promises it answers to.
///
/// <para>Reserved: the sequencer laid it on empty pipe between phrases. Nothing proves it, so it is
/// made safe by clearance instead — bounded length, orbs held away either side, a budget per section.</para>
///
/// <para>Plank: a flank of one of the sequencer's ribbon reservations. Longer, paired over one span,
/// and carrying the ribbon between them, so it answers to the hand-over rules instead.</para>
///
/// <para>Authored: cut by a phrase (<see cref="InternodeGapSpec"/>). ⚠ It answers to the reachability
/// oracle and to nothing else: the oracle drives the real <c>floorHere</c> from it and proves a route through,
/// which is exactly why an authored hole may be long, may sit under orbs, and may come in tens per section
/// where a reserved one may not. Counting these against the reserved budget is wrong and was the first thing
/// that broke when phrases learned to cut floor.</para></summary>
public enum InternodeGapOrigin { Reserved, Plank, Authored }

/// <summary>A break in the floor over an arc of the surface: the runner has to be off the ground, or off the arc,
/// to cross it, and falling in rewinds the section. A full gap runs rim to rim; a half-gap or a slot leaves floor
/// to run round on. Placed clear of every orb, so jumping one never costs a collection.
///
/// <para><see cref="InternodeGapOrigin.Plank"/> marks a gap that is one flank of a plank — a ribbon of floor with
/// a hole down either side, ridden rather than jumped. It changes nothing about how the gap behaves; it is there
/// because the two kinds answer to different promises and something has to be able to tell them apart. An
/// ordinary gap is short enough to hop and stands clear of every orb; a flank runs long, shares its span with its
/// partner, and deliberately has orbs down the ribbon between them. Left to be inferred from geometry, every one
/// of those invariants would have to be weakened to a guess.</para></summary>
public readonly record struct InternodeGap(double Z0, double Seconds, double ThetaMinDeg, double ThetaMaxDeg,
                                           InternodeGapOrigin Origin = InternodeGapOrigin.Reserved,
                                           double ThetaMinEndDeg = double.NaN, double ThetaMaxEndDeg = double.NaN)
{
    public double MinEndDeg => double.IsNaN(ThetaMinEndDeg) ? ThetaMinDeg : ThetaMinEndDeg;
    public double MaxEndDeg => double.IsNaN(ThetaMaxEndDeg) ? ThetaMaxDeg : ThetaMaxEndDeg;
    public bool Swept => MinEndDeg != ThetaMinDeg || MaxEndDeg != ThetaMaxDeg;
    /// <summary>The low edge, degrees, at section depth <paramref name="z"/> (clamped to the span, so a reader
    /// that tests a whole band against a hole starting inside it gets the edge held flat past the end).</summary>
    public double MinAt(double z) => Lerp(ThetaMinDeg, MinEndDeg, z);
    public double MaxAt(double z) => Lerp(ThetaMaxDeg, MaxEndDeg, z);
    private double Lerp(double a, double b, double z)
    {
        if (a == b || Seconds <= 0) return a;
        double u = Math.Clamp((z - Z0) / Seconds, 0, 1);
        return a + (b - a) * u;
    }
    /// <summary>The clamped edges (<see cref="MinAt"/>, <see cref="MaxAt"/>), radians. ⚠ Never continued past the
    /// span along the slope: a swept plank's chords butt, and a continued edge runs into its neighbour's stretch.</summary>
    public (double Lo, double Hi) RadAt(double z) => (MinAt(z) * Math.PI / 180, MaxAt(z) * Math.PI / 180);
    /// <summary>The depths inside the span where an edge passes through <paramref name="thetaRad"/>; zero, one
    /// or two of them, written to <paramref name="z"/> and counted.</summary>
    public int EdgeCrossings(double thetaRad, Span<double> z)
    {
        double deg = thetaRad * 180 / Math.PI;
        int n = 0;
        n = Cross(ThetaMinDeg, MinEndDeg, deg, z, n);
        n = Cross(ThetaMaxDeg, MaxEndDeg, deg, z, n);
        return n;
    }
    private int Cross(double a, double b, double deg, Span<double> z, int n)
    {
        if (a == b) return n;
        double u = (deg - a) / (b - a);
        if (u > 1e-9 && u < 1 - 1e-9) z[n++] = Z0 + u * Seconds;
        return n;
    }
    /// <summary>A flank of one of the sequencer's ribbon reservations — what CheckPlanks groups on.</summary>
    public bool Plank => Origin == InternodeGapOrigin.Plank;
    /// <summary>Cut by a phrase, so it is the oracle's to prove and not the reserved budget's to bound.</summary>
    public bool Authored => Origin == InternodeGapOrigin.Authored;

    /// <summary>Is this pipe angle over the hole at depth <paramref name="z"/>? The depth is not range-tested
    /// here; callers test the span themselves, and past it the edges are held at their end values.</summary>
    public bool Covers(double thetaRad, double z)
    {
        double deg = thetaRad * 180 / Math.PI;
        return deg >= MinAt(z) && deg <= MaxAt(z);
    }
}

/// <summary>A closing of the pipe's opening: the rim rises from the base <see cref="InternodeTuning.RimDeg"/> to
/// <see cref="TargetDeg"/> over a smoothstep ramp of <see cref="InternodeTuning.RimRampSeconds"/> at each end of the
/// span and holds the target between them (C1 at both ends), so a closure reads as a distinct tunnel design rather
/// than a passing bump. Unlike bends and twists this reaches the physics: the rim the integrator sees is the profile's value at
/// the runner's z.</summary>
public readonly record struct InternodeRim(double Z0, double Seconds, double TargetDeg);

/// <summary>One built section: the only one in memory. Rebuilt from (stage, section, seed) on restore, so
/// none of this is in the snapshot.</summary>
public sealed class InternodeSection
{
    public InternodeSection(double length, double timeScale, InternodePlacedEvent[] events, InternodeBend[] bends,
                        InternodeTwist[] twists, InternodeRim[] rims, InternodeGap[] gaps, string[] phraseIds, double[] phraseStarts)
    {
        Length = length; TimeScale = timeScale; Events = events; Bends = bends; Twists = twists; Rims = rims; Gaps = gaps;
        PhraseIds = phraseIds; PhraseStarts = phraseStarts;
        // ⚠ The offer is what one player can actually collect, not how many orbs were placed. The two differ
        // wherever a choice phrase put two routes on the table: a player follows one of them, so counting both
        // would set a gate demand no route can meet. Per phrase: every branch-0 orb (on every route) plus the
        // best single branch. Unbranched phrases are all branch 0, so this collapses to a simple orb count for
        // them.
        var routeless = new Dictionary<int, int>();          // phrase → orbs on every route
        var perBranch = new Dictionary<(int Phrase, int Branch), int>();
        int loose = 0;                                       // orbs the course placed outside any phrase
        foreach (var e in events)
        {
            if (e.Kind == InternodeEventKind.Mine) continue;
            if (e.Phrase < 0) { loose++; continue; }
            if (e.Branch == 0) routeless[e.Phrase] = routeless.GetValueOrDefault(e.Phrase) + 1;
            else perBranch[(e.Phrase, e.Branch)] = perBranch.GetValueOrDefault((e.Phrase, e.Branch)) + 1;
        }
        var best = new Dictionary<int, int>();               // phrase → orbs on its richest branch
        foreach (var ((phrase, _), count) in perBranch)
            best[phrase] = Math.Max(best.GetValueOrDefault(phrase), count);

        int tokens = loose;
        int min = Math.Max(2, InternodeTuning.BonusMinOrbs);
        foreach (int phrase in routeless.Keys.Union(best.Keys))
        {
            int onRoute = routeless.GetValueOrDefault(phrase) + best.GetValueOrDefault(phrase);
            tokens += onRoute;
            // ⚠ One prize per phrase even where a choice offers a ring on each of its branches: a player runs
            // one route, so only one of those rings can ever be collected on, and charging the gate for both
            // would be charging for a reward nobody can win. The gold rule counts per route
            // (Internode.IndexPhrases); the gate's demand counts per phrase, at the best route's length.
            if (onRoute >= min) PrizesOffered++;
        }
        TokensOffered = tokens;
    }

    /// <summary>Where the gate stands, seconds.</summary>
    public double Length { get; }
    /// <summary>The phrase time scale this section was built at.</summary>
    public double TimeScale { get; }
    /// <summary>Sorted by <see cref="InternodePlacedEvent.Z"/>.</summary>
    public InternodePlacedEvent[] Events { get; }
    public InternodeBend[] Bends { get; }
    public InternodeTwist[] Twists { get; }
    /// <summary>Sorted by <see cref="InternodeRim.Z0"/>; never overlapping.</summary>
    public InternodeRim[] Rims { get; }
    public InternodeGap[] Gaps { get; }
    public string[] PhraseIds { get; }
    public double[] PhraseStarts { get; }
    /// <summary>Tokens (ground and elevated) in the section; the quota increment derives from it.</summary>
    public int TokensOffered { get; }
    /// <summary>Phrases long enough to pay gold, so every one is a prize on the table. The gate's demand counts each at
    /// <see cref="InternodeTuning.BonusTokenValue"/> − 1 orbs on top of itself: gold pays ten, so a section full of prizes is worth far
    /// more than its orb count and the gate has to ask for more than the orbs alone.</summary>
    public int PrizesOffered { get; }
}

/// <summary>The runner in the pipe's own frame. θ radians (0 bottom, positive right, normalised to
/// (−π, π]); ω radians per second; <see cref="H"/> height above the surface in pipe radii;
/// <see cref="JumpBuffer"/> seconds left on a press made in the air that fires on landing;
/// <see cref="GripLeft"/> seconds of wall grip left before the pendulum pull returns to a released stick
/// (banked to <see cref="InternodeTuning.GripSeconds"/> while the stick is held).</summary>
public struct InternodeRunnerState
{
    public double Theta;
    public double Omega;
    public double H;
    public double Vh;
    public double JumpBuffer;
    public double GripLeft;
    /// <summary>Velocity in the pipe's cross-section while <see cref="InternodeAir.OverTop"/>, pipe radii per second
    /// (x to the runner's right, y up); zero otherwise. Across the open top the runner is a free particle, and
    /// <see cref="Theta"/>/<see cref="H"/> are derived from its position each step.</summary>
    public double FlyVx;
    public double FlyVy;
    /// <summary>The flight's one jump has been spent. False on a runner who stepped off a lip, who may still
    /// launch while <see cref="CoyoteLeft"/> lasts. Cleared on every landing.</summary>
    public bool Launched;
    /// <summary>Coyote time left after stepping off a lip; transient (not snapshotted — at most CoyoteSeconds).</summary>
    public double CoyoteLeft;
    public InternodeAir Air;

    public static InternodeRunnerState AtRest => default;
}

/// <summary>What one <see cref="InternodePhysics.Integrate"/> call did, for the game to turn into cues.</summary>
[Flags]
public enum InternodeStepFlags
{
    None = 0,
    Jump = 1,
    Land = 2,
    RimExit = 4,
    OverTopLand = 16,
}

/// <summary>An orb scattered by a mine hit. Spawned at the runner (<see cref="Theta"/> radians,
/// <see cref="Radial"/> height); the renderer derives the scatter from <see cref="Seed"/> and
/// <see cref="Age"/>. Transient presentation.</summary>
public readonly record struct InternodeBurst(double Theta, double Radial, double Age, int Seed, bool Gold = false);
