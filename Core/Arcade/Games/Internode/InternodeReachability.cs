namespace ControllerWheel;

/// <summary>Result of one oracle run. <see cref="FailingEvent"/> names the first plane (or "exit") with no
/// survivors; <see cref="MinSurvivors"/> is the smallest state count after any plane filter.</summary>
public sealed record InternodeReachabilityResult(bool Pass, string? FailingEvent, int MinSurvivors, int ExitBuckets);

/// <summary>The lattice reachability oracle for the phrase bank. Probe-time only: nothing at runtime calls
/// this. It lives in Core so it consumes the same bank, bands and <see cref="InternodePhysics.Integrate"/> as the
/// game; an oracle with its own integrator proves nothing about the game.
///
/// <para>A cloud of runner states is advanced in control holds of <see cref="HoldSubsteps"/> real steps under
/// every control (steer −1/0/+1 × jump × hold-or-release — a release clips the climb, so press duration is a
/// control and omitting it would report false failures), reduced to two states per lattice bucket after each hold (the ω
/// extremes, so the reachable envelope survives), filtered at every event plane by the game's own
/// <see cref="InternodePhysics.Collides"/> with a safety margin, and finally by the exit band. Resolution
/// constants are consts, not knobs: coarser buckets would let a false pass through. The integrator is driven at
/// the base rim; that is sound because the sequencer closes the pipe only over phrases marked rim-safe, each of
/// which the probe has passed with flight forbidden.</para>
///
/// <para>Wall grip rides along in the state (the integrator is the game's, so the lattice runs the true
/// physics) but is not part of the bucket key: dropping a state only shrinks the cloud, so a pass stays a sound
/// proof. Every start has no grip banked (the pull is live at entry); one held control hold restores full grip,
/// so this is the conservative entry.</para></summary>
public static class InternodeReachability
{
    private const int ThetaBuckets = 144;
    private const int OmegaBuckets = 17;
    private const int HeightBuckets = 12;
    private const int HoldSubsteps = 4;
    private const double StepSeconds = 1.0 / 120.0;
    /// <summary>Tokens are caught in a window this much narrower, mines avoided by a window this much wider, degrees.</summary>
    private const double OracleMarginDeg = 2.0;
    private const int MinExitBuckets = 3;

    /// <summary>Five starts per entry class: the band centre and its four corners, grounded, no grip banked.</summary>
    public static InternodeRunnerState[] StartStates(InternodePos entry)
    {
        var b = InternodePhrases.Band(entry);
        double d = InternodePhysics.Deg;
        InternodeRunnerState S(double th, double om) => new() { Theta = th * d, Omega = om * d };
        return
        [
            S((b.ThetaMinDeg + b.ThetaMaxDeg) / 2, (b.OmegaMinDeg + b.OmegaMaxDeg) / 2),
            S(b.ThetaMinDeg, b.OmegaMinDeg), S(b.ThetaMinDeg, b.OmegaMaxDeg),
            S(b.ThetaMaxDeg, b.OmegaMinDeg), S(b.ThetaMaxDeg, b.OmegaMaxDeg),
        ];
    }

    /// <param name="forbidTop">Kill every state that leaves the surface over the open top. A pass under this proves the
    /// phrase never needs the rim, so the course may close the pipe over it at any rim from the base up: a trajectory
    /// that stays inside ±RimDeg is identical under every wider closure.</param>
    /// <param name="branch">Which route through a choice phrase to prove. Planes on another branch are dropped
    /// entirely — an orb the player did not choose to go for is neither a requirement nor an obstacle, so it must
    /// not kill a state. ⚠ A choice phrase is only proven when every one of its branches passes; a run at branch 0
    /// alone would only prove the shared spine, and a run over all branches at once would demand a player be in two
    /// places on one plane and could never pass. The probe drives that; this only serves one route at a time.</param>
    public static InternodeReachabilityResult Check(InternodePhrase phrase, InternodeRunnerState start, double timeScale, bool forbidTop = false,
                                                    int branch = 0)
    {
        double margin = OracleMarginDeg * InternodePhysics.Deg;
        // The base rim: the sequencer closes the pipe only over rim-safe phrases (proven with forbidTop), so no
        // phrase's proof depends on the rim moving. The one exception is a phrase that demands the tube: the
        // sequencer guarantees a full closure over its whole span, so it is proven at rim π and nowhere else.
        double rim = phrase.Closed ? Math.PI : InternodePhysics.RimRad;
        double length = phrase.Length * timeScale;
        var planes = phrase.Events.Where(e => e.Branch == 0 || e.Branch == branch)
                                  .Select(e => (Z: e.Dz * timeScale, Theta: InternodePhysics.Wrap(e.ThetaDeg * InternodePhysics.Deg), e.Kind, e.Height))
                                  .OrderBy(e => e.Z).ToArray();
        // The phrase's own holes on the same clock as its planes. These drive Integrate's `floorHere` exactly as
        // Internode does, so a proof here is a proof about the floor the player actually gets.
        // A swept hole's edges run linearly across its span; they are interpolated at the state's own depth below.
        var holes = phrase.Holes.Select(h => (Z0: h.Dz * timeScale, Z1: (h.Dz + h.Seconds) * timeScale,
                                              Lo: h.ThetaMinDeg * InternodePhysics.Deg,
                                              Hi: h.ThetaMaxDeg * InternodePhysics.Deg,
                                              LoEnd: h.MinEndDeg * InternodePhysics.Deg,
                                              HiEnd: h.MaxEndDeg * InternodePhysics.Deg)).ToArray();
        double jumpWindow = InternodeTuning.JumpSeconds + HoldSubsteps * StepSeconds;

        var cloud = new List<InternodeRunnerState> { start };
        var next = new List<InternodeRunnerState>();
        // Bucket → the indices in `next` of its lowest-ω and highest-ω states. Two representatives per bucket
        // keep the reachable envelope intact (one representative erodes the fast frontier a little every
        // hold), and the rule is left/right symmetric, which the mirrored bank relies on: keeping the first
        // arrival would bias the cloud toward whichever control is tried first.
        var seen = new Dictionary<long, (int Lo, int Hi)>();
        int minSurvivors = int.MaxValue;
        double t = 0;
        int nextPlane = 0;
        double[] steers = [-1, 0, 1];

        while (t < length - 1e-9)
        {
            // A jump is only worth trying when a plane falls inside its airtime; pruning it elsewhere makes the
            // oracle stricter, never looser.
            bool jumpUseful = false;
            for (int i = nextPlane; i < planes.Length; i++)
            {
                if (planes[i].Z > t + jumpWindow) break;
                jumpUseful = true;
            }
            // ⚠ A hole ahead is a reason to jump too, whether or not an orb sits inside the airtime: a hole
            // longer than the window minus the landing clearance has its first orb past the window, and with
            // only orbs counted the search never pressed, walked in, and reported the figure unclearable when
            // the player simply jumps at the lip. A hole already under the runner counts for the coyote press.
            if (!jumpUseful)
                for (int hh = 0; hh < holes.Length; hh++)
                    if (holes[hh].Z0 <= t + jumpWindow && holes[hh].Z1 > t) { jumpUseful = true; break; }

            next.Clear();
            seen.Clear();
            double holdEnd = t;
            foreach (var s0 in cloud)
            {
                foreach (double steer in steers)
                {
                    for (int jump = 0; jump < 2; jump++)
                    for (int held = 1; held >= 0; held--)
                    {
                        // A press is tried from the ground, and in the air only while a runner who stepped off a lip still
                        // has the jump in hand (coyote time) — the game's own rule, in InternodePhysics.
                        bool canPress = s0.Air == InternodeAir.Grounded || (s0.Air == InternodeAir.Jumping && !s0.Launched && s0.CoyoteLeft > 0);
                        if (jump == 1 && (!jumpUseful || !canPress)) continue;
                        // Release is a control, not an omission: the clip in Integrate pins Vh to JumpTapFraction of the
                        // impulse at whatever height the runner has reached, so press duration buys a continuum of
                        // apexes between the hop and the full arc. Exploring holds alone would under-approximate the
                        // player and report false failures — sound for a pass, worthless for a verdict of unclearable.
                        // The branch is pruned where the flag cannot change the trajectory: the clip only bites on an
                        // airborne runner still rising above it.
                        double clip = InternodeTuning.JumpImpulse * Math.Clamp(InternodeTuning.JumpTapFraction, 0, 1);
                        if (held == 0 && jump == 0 && !(s0.Air == InternodeAir.Jumping && s0.Vh > clip)) continue;
                        var s = s0;
                        double tt = t;
                        bool alive = true;
                        for (int k = 0; k < HoldSubsteps && alive; k++)
                        {
                            double tPrev = tt;
                            // ⚠ The floor is a function of where the state is, so it is read per substep from the
                            // state about to be advanced — not once per hold. A hole the runner has steered
                            // over between substeps has to open under them on the substep they are over it.
                            bool floorHere = true;
                            for (int hh = 0; hh < holes.Length; hh++)
                                if (tPrev > holes[hh].Z0 && tPrev < holes[hh].Z1)
                                {
                                    var hole = holes[hh];
                                    double u = (tPrev - hole.Z0) / (hole.Z1 - hole.Z0);
                                    double lo = hole.Lo + (hole.LoEnd - hole.Lo) * u, hi = hole.Hi + (hole.HiEnd - hole.Hi) * u;
                                    if (s.Theta >= lo && s.Theta <= hi) { floorHere = false; break; }
                                }
                            s = InternodePhysics.Integrate(s, steer, jump == 1 && k == 0, held == 1, StepSeconds, rim, floorHere, out _);
                            tt = tPrev + StepSeconds;
                            // The game's own commit test, verbatim (Internode.Step): below the floor, no longer rising, and
                            // either the jump spent or the coyote window over — fallen. A sink inside the window survives.
                            if (s.H < 0 && s.Vh <= 0 && (s.Launched || s.CoyoteLeft <= 0)) { alive = false; break; }
                            if (forbidTop && s.Air == InternodeAir.OverTop) { alive = false; break; }
                            for (int i = nextPlane; i < planes.Length; i++)
                            {
                                var p = planes[i];
                                if (p.Z <= tPrev) continue;
                                if (p.Z > tt) break;
                                bool hit = InternodePhysics.Collides(p.Kind, p.Theta, p.Height, s, margin);
                                if (p.Kind == InternodeEventKind.Mine ? hit : !hit) { alive = false; break; }
                            }
                            if (tt >= length - 1e-9) break;
                        }
                        holdEnd = tt;
                        if (!alive) continue;
                        long key = Bucket(s);
                        if (!seen.TryGetValue(key, out var pair)) { seen[key] = (next.Count, next.Count); next.Add(s); }
                        else if (s.Omega < next[pair.Lo].Omega)
                        {
                            if (pair.Lo == pair.Hi) { seen[key] = (next.Count, pair.Hi); next.Add(s); }
                            else next[pair.Lo] = s;
                        }
                        else if (s.Omega > next[pair.Hi].Omega)
                        {
                            if (pair.Lo == pair.Hi) { seen[key] = (pair.Lo, next.Count); next.Add(s); }
                            else next[pair.Hi] = s;
                        }
                    }
                }
            }

            // Report survivors at each plane crossed during this hold.
            while (nextPlane < planes.Length && planes[nextPlane].Z <= holdEnd + 1e-12)
            {
                if (next.Count == 0)
                    return new(false, Describe(phrase, nextPlane, planes[nextPlane].Kind, branch), 0, 0);
                minSurvivors = Math.Min(minSurvivors, next.Count);
                nextPlane++;
            }
            if (next.Count == 0) return new(false, "in flight", 0, 0);

            (cloud, next) = (next, cloud);
            t = holdEnd;
        }

        var band = InternodePhrases.Band(phrase.Exit);
        var exitBuckets = new HashSet<int>();
        int exitCount = 0;
        foreach (var s in cloud)
        {
            if (s.Air != InternodeAir.Grounded) continue;
            if (!band.Contains(s.Theta / InternodePhysics.Deg, s.Omega / InternodePhysics.Deg)) continue;
            exitCount++;
            exitBuckets.Add(ThetaBucket(s.Theta));
        }
        if (exitCount == 0) return new(false, "exit band", minSurvivors == int.MaxValue ? cloud.Count : minSurvivors, 0);
        minSurvivors = Math.Min(minSurvivors, exitCount);
        bool pass = exitBuckets.Count >= MinExitBuckets;
        return new(pass, pass ? null : "exit spread", minSurvivors, exitBuckets.Count);
    }

    // ⚠ `index` counts the planes on this route, not the phrase's whole event array, so the branch has to
    // be named or the number points at the wrong orb when a choice phrase fails.
    private static string Describe(InternodePhrase phrase, int index, InternodeEventKind kind, int branch) =>
        branch == 0 ? $"{phrase.Id} event {index} ({kind})"
                    : $"{phrase.Id} branch {branch} event {index} ({kind})";

    private static int ThetaBucket(double theta)
    {
        int b = (int)Math.Floor((theta + Math.PI) / (2 * Math.PI) * ThetaBuckets);
        return Math.Clamp(b, 0, ThetaBuckets - 1);
    }

    private static long Bucket(in InternodeRunnerState s)
    {
        double wMax = InternodeTuning.MaxAngularSpeedDeg * InternodePhysics.Deg;
        int tb = ThetaBucket(s.Theta);
        int ob = Math.Clamp((int)Math.Floor((s.Omega + wMax) / (2 * wMax) * OmegaBuckets), 0, OmegaBuckets - 1);
        int hb = 0, vs = 0;
        if (s.Air == InternodeAir.Jumping)
        {
            double apex = Math.Max(1e-9, InternodeTuning.JumpMaxApex);
            hb = Math.Clamp((int)Math.Floor(s.H / apex * (HeightBuckets - 1)), 0, HeightBuckets - 1);
            vs = s.Vh >= 0 ? 1 : 2;
        }
        // Whether the flight's jump is spent is part of the key: a runner who stepped off a lip with it in hand
        // reaches further than one who launched, and merging the two would drop that frontier.
        return (long)(uint)tb | ((long)ob << 8) | ((long)(int)s.Air << 16) | ((long)hb << 20) | ((long)vs << 28) | ((long)(s.Launched ? 1 : 0) << 30);
    }
}
