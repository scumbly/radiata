namespace ControllerWheel;

/// <summary>The authored formation bank. Phrases chain on position class: a phrase may follow another when its
/// <see cref="InternodePhrase.Entry"/> equals the previous <see cref="InternodePhrase.Exit"/>. Every over-the-top
/// phrase lands inside itself and exits on a surface class, so a breather can always follow.
///
/// <para>Mines are the primary obstacle. A <see cref="Ring"/> of them spans the surface with overlapping bands,
/// which is the one figure that can only be jumped; cutting a window out of a ring leaves a single corridor to
/// thread instead. They start in tier 1 — stage 1 draws from tier 1 alone, and a bank
/// whose first tier is all tokens shows the player nothing to dodge for their first minute. Only
/// <see cref="Straight"/> and the breathers are clean.</para>
///
/// <para>A spiral needs no closed pipe: enough angular speed carries the runner over the rim and across the open
/// top (<see cref="InternodePhysics"/>), so a phrase whose θ marches one way through ±180 reads as a roll around the
/// pipe. Tokens still obey |θ| ≤ RimDeg − TokenHalfWidthDeg, which is what keeps them on a surface the runner
/// can be standing on.</para>
///
/// <para>⚠ Authoring rules InternodeProbe enforces: unique ids; <c>Dz</c> sorted and ≥ 0; |θ| ≤ RimDeg −
/// TokenHalfWidthDeg for a mine, ≤ 180 for an orb; one orb per plane per route; orb spacing ≥
/// MinTokenSpacingSeconds after the time-scale floor, again per route; a mine and an orb on one plane
/// separated by both half-widths plus margin; elevated heights within (GroundGrabHeight + ElevatedBandHalf,
/// JumpMaxApex]; branches numbered 1..K with no gaps; a hole inside the phrase's own span and never under a
/// mine; every class has a tier-1 breather; every reachable class at every tier cap has an entering phrase
/// and a route back to Centre; every phrase carries a row in the short-name reference table (checked only where that table is present); and the
/// reachability oracle passes every phrase — every route of a choice — from five starts at both time scales
/// (<see cref="InternodeTuning.PhraseTimeScaleStart"/> and the floor). Left/right pairs go
/// through <see cref="Mirror"/> so they cannot drift.</para>
///
/// <para>A phrase may also cut its own floor (<see cref="InternodeGapSpec"/>), which is what lets a formation
/// be a piece of track — a strip to hold, a plank that sweeps, islands to hop — rather than objects scattered
/// on flat ground. ⚠ Those holes are proven: the oracle drives <c>Integrate</c>'s real <c>floorHere</c> from
/// them and kills a state that commits to a fall, and <c>CheckOracleSeesHoles</c> asserts that it does, so a
/// gap figure cannot pass by being invisible to the proof.</para></summary>
public static class InternodePhrases
{
    private const InternodeEventKind Tok = InternodeEventKind.Token;
    private const InternodeEventKind Mine = InternodeEventKind.Mine;
    private const InternodeEventKind Elev = InternodeEventKind.ElevatedToken;

    // ⚠ Lifted orbs come only as a rising-and-falling run along one jump's arc: never a single lifted orb
    // between two ground orbs. Each carries its own drawn height; the catch itself ignores height — off the
    // ground on the line is a catch (InternodePhysics.Collides) — so the heights are the shape the eye reads.

    /// <summary>Entry/exit bands, in a half-pipe with a small lip (rim 100°): the ground is a little over the lower
    /// 180°, so "high on the wall" is the 60s to 90. Every angle in this bank was authored for a 300° pipe and scaled
    /// by two thirds into this one; the oracle proves the result. The ω ranges are what a phrase's opening beat can absorb from a runner
    /// arriving the wrong way. On the high wall both limits are tight: outward, because a runner sliding toward the
    /// rim at speed leaves the surface and — the top pulling like air — falls back before any crossing phrase can
    /// pay off; inward, because the net climb near 90° is only the torque minus almost the whole pendulum pull.</summary>
    public static InternodeBand Band(InternodePos pos) => pos switch
    {
        InternodePos.Centre    => new(-13, 13, -60, 60),
        InternodePos.LowLeft   => new(-43, -20, -70, 70),
        InternodePos.LowRight  => new(20, 43, -70, 70),
        InternodePos.HighLeft  => new(-90, -57, -40, 40),
        InternodePos.HighRight => new(57, 90, -40, 40),
        _ => new(-13, 13, -60, 60),
    };

    /// <summary>The mirror image of a class across the bottom centre line.</summary>
    public static InternodePos Mirrored(InternodePos pos) => pos switch
    {
        InternodePos.LowLeft => InternodePos.LowRight,
        InternodePos.LowRight => InternodePos.LowLeft,
        InternodePos.HighLeft => InternodePos.HighRight,
        InternodePos.HighRight => InternodePos.HighLeft,
        _ => pos,
    };

    /// <summary>Same phrase with every angle negated and both classes mirrored.</summary>
    /// <remarks>⚠ A mirrored gap swaps its bounds as well as negating them: reflecting [lo, hi] gives
    /// [−hi, −lo], and leaving them in the authored order would produce an empty arc — a hole that renders
    /// and drops nobody, on the -right twin only.</remarks>
    public static InternodePhrase Mirror(InternodePhrase p, string id) =>
        new(id, p.Tier, Mirrored(p.Entry), Mirrored(p.Exit), p.Length,
            [.. p.Events.Select(e => e with { ThetaDeg = -e.ThetaDeg })], p.RimSafe, p.TimeLocked,
            p.Holes.Length == 0 ? null
                : [.. p.Holes.Select(h => h with { ThetaMinDeg = -h.ThetaMaxDeg, ThetaMaxDeg = -h.ThetaMinDeg,
                                                   ThetaMinEndDeg = -h.ThetaMaxEndDeg, ThetaMaxEndDeg = -h.ThetaMinEndDeg })],
            p.Closed, p.MinStage, p.Diabolical);

    private static InternodePhrase P(string id, int tier, InternodePos entry, InternodePos exit, double length, params InternodeEvent[] events) =>
        new(id, tier, entry, exit, length, events);

    /// <summary>A phrase that needs the open top: the course never closes the pipe over it. A time-locked one is
    /// spaced in real seconds, because the flight it is built on is as long as the physics makes it.</summary>
    private static InternodePhrase Crossing(string id, int tier, InternodePos entry, InternodePos exit, double length, bool timeLocked, params InternodeEvent[] events) =>
        new(id, tier, entry, exit, length, events, RimSafe: false, TimeLocked: timeLocked);

    /// <summary>Mines right across the surface at a spacing narrower than their own width, so the blocked bands
    /// overlap into a wall: the only way past a full ring is over it. A gap window leaves one corridor instead —
    /// the safe angles are the window minus a mine half-width at each end.</summary>
    private static InternodeEvent[] Ring(double dz, double gapCentreDeg = 0, double gapWidthDeg = 0)
    {
        double max = InternodeTuning.RimDeg - InternodeTuning.MineHalfWidthDeg;
        double step = 2 * max / (RingMines - 1);
        var ring = new List<InternodeEvent>(RingMines);
        for (int i = 0; i < RingMines; i++)
        {
            double a = -max + i * step;
            if (gapWidthDeg > 0 && Math.Abs(a - gapCentreDeg) <= gapWidthDeg / 2) continue;
            ring.Add(M(dz, a));
        }
        return [.. ring];
    }

    /// <summary>Several <see cref="Ring"/>s in a row, each door turned <paramref name="turnDeg"/> further than the
    /// last: stacked close they are a wall no single jump clears, so the door has to be threaded; turned ring by ring
    /// the wall reads as rotating and the door has to be followed round.</summary>
    private static InternodeEvent[] Rings(double dz0, int count, double spacing, double doorCentreDeg, double doorWidthDeg, double turnDeg)
    {
        var all = new List<InternodeEvent>();
        for (int i = 0; i < count; i++) all.AddRange(Ring(dz0 + i * spacing, doorCentreDeg + i * turnDeg, doorWidthDeg));
        return [.. all];
    }

    /// <summary>Mines in a full ring. Set so the gap between neighbours lands just inside the width that
    /// blocks: at <see cref="InternodeTuning.RimDeg"/> 100 and <see cref="InternodeTuning.MineHalfWidthDeg"/>
    /// 15.2 the step is 28.27°, against the 30.4° a runner needs to slip between two — 2.1° of margin.
    /// ⚠ Lowering this count, or lowering the mine width, reopens the wall: the ring is a wall by design and
    /// the door is the only way through it.
    ///
    /// <para>⚠ Two margins pull in opposite directions and both are checked in InternodeProbe: fewer mines
    /// opens the wall below the 2.1° slip margin; more mines overlaps the mine art — two mines only clear
    /// each other at a step of <c>2·asin(MineRadius)</c>, 24.1° for the current art, against the 28.27° this
    /// count gives, a 4.2° clearance.</para></summary>
    private const int RingMines = 7;

    /// <summary>A phrase built from groups, so a <see cref="Ring"/> can sit among hand-placed events.</summary>
    private static InternodePhrase PR(string id, int tier, InternodePos entry, InternodePos exit, double length, params InternodeEvent[][] groups) =>
        new(id, tier, entry, exit, length, [.. groups.SelectMany(g => g).OrderBy(e => e.Dz)]);

    private static InternodeEvent T(double dz, double theta) => new(dz, theta, Tok);
    private static InternodeEvent M(double dz, double theta) => new(dz, theta, Mine);
    /// <summary>An elevated orb at its own height, for an arc that has to be followed rather than a single hop.</summary>
    private static InternodeEvent E(double dz, double theta, double height) => new(dz, theta, Elev, height);

    /// <summary>An orb on route <paramref name="branch"/> of a choice phrase (1, 2, …) rather than on every
    /// route. ⚠ Spelled out rather than an overload of <see cref="T"/>: a bare third number there would read
    /// as a height, which is what the third argument means everywhere else in this file.</summary>
    private static InternodeEvent TB(double dz, double theta, int branch) => new(dz, theta, Tok, 0, branch);

    // ── Authoring a phrase's own track ────────────────────────────────────────
    // ⚠ Authored seconds are compressed by the stage scale, so a hole plays about half as long as it reads
    // here. The real limits these are written against: a full-width hole must stay inside
    // GapMaxSeconds (0.6 s) because that is the "one press clears it" promise the whole gap vocabulary rests
    // on — so about 1.2 s authored; a ribbon runs PlankMinSeconds..PlankMaxSeconds (0.9–1.6 s real, so
    // 1.8–3.2 s authored); a void between ribbons PlankVoidMin..Max (0.25–0.5 s real). None of it is taken on
    // trust: InternodeProbe's oracle drives Integrate's own `floorHere` from these and kills a state that
    // commits to a fall, so a phrase here is proven against the floor the player actually gets.
    private static InternodeGapSpec G(double dz, double secs, double lo, double hi) => new(dz, secs, lo, hi);

    /// <summary>A strip of floor left at <paramref name="centre"/> ± <paramref name="half"/>; everything else
    /// at that moment is gone. Two gaps, one to each rim — on a closed pipe they meet over the top, on an
    /// open one they run out at the opening, and either way the strip is the only floor.</summary>
    private static InternodeGapSpec[] Ribbon(double dz, double secs, double centre, double half) =>
        [G(dz, secs, -180, centre - half), G(dz, secs, centre + half, 180)];

    /// <summary>The whole floor gone: the figure that can only be jumped. Keep it under 1.2 s authored.</summary>
    private static InternodeGapSpec[] Hole(double dz, double secs) => [G(dz, secs, -180, 180)];

    /// <summary>A <see cref="Ribbon"/> whose centre may sit anywhere round the pipe, the top included: the strip
    /// is wrapped into (−180, 180], so a strip over 12 o'clock is one gap covering everything but it, rather
    /// than the inverted second gap <see cref="Ribbon"/> would emit. ⚠ A strip past the base rim is only floor
    /// on a closed phrase; on an open one it is sky, and the oracle will say so.</summary>
    private static InternodeGapSpec[] Strip(double dz, double secs, double centre, double half) => Strips(dz, secs, half, centre);

    private static double Norm(double deg) => ((deg + 180) % 360 + 360) % 360 - 180;

    /// <summary>Several strips of floor at once, each <paramref name="half"/> wide about one of
    /// <paramref name="centres"/>, as the gaps that are the complement of their union round the pipe. ⚠ Two
    /// <see cref="Strip"/>s at one moment cannot do this: gaps are unioned by the physics and the renderer, so
    /// two strips' gaps together leave only where the strips overlap. A fork, a braid or a pair of antipodal
    /// planks must be authored through this.</summary>
    private static InternodeGapSpec[] Strips(double dz, double secs, double half, params double[] centres)
    {
        var spans = new List<(double A, double B)>();
        foreach (double c0 in centres)
        {
            double c = Norm(c0), a = c - half, b = c + half;
            if (a < -180) { spans.Add((a + 360, 180)); spans.Add((-180, b)); }
            else if (b > 180) { spans.Add((a, 180)); spans.Add((-180, b - 360)); }
            else spans.Add((a, b));
        }
        spans.Sort((x, y) => x.A.CompareTo(y.A));
        var merged = new List<(double A, double B)>();
        foreach (var (a, b) in spans)
            if (merged.Count > 0 && a <= merged[^1].B + 1e-9) merged[^1] = (merged[^1].A, Math.Max(merged[^1].B, b));
            else merged.Add((a, b));
        var gaps = new List<InternodeGapSpec>();
        double cursor = -180;
        foreach (var (a, b) in merged)
        {
            if (a - cursor > 1e-6) gaps.Add(G(dz, secs, cursor, a));
            cursor = Math.Max(cursor, b);
        }
        if (180 - cursor > 1e-6) gaps.Add(G(dz, secs, cursor, 180));
        return [.. gaps];
    }

    /// <summary>Two strips curving at once — a fork that diverges, a braid that crosses — as
    /// <paramref name="steps"/> chords of swept gaps (<see cref="SweptStrips"/>), each strip's centre running
    /// straight from one chord end to the next. Where the two centres meet the floor is one strip; where they
    /// part it is two. Linear, not eased: a braid is authored as several calls whose rates are meant to match.</summary>
    private static InternodeGapSpec[] Braid(double dz, double secs, int steps, double half,
                                            double fromA, double toA, double fromB, double toB)
    {
        var all = new List<InternodeGapSpec>();
        double dt = secs / steps;
        for (int i = 0; i < steps; i++)
        {
            double u0 = (double)i / steps, u1 = (double)(i + 1) / steps;
            all.AddRange(SweptStrips(dz + i * dt, dt, half, half,
                                     (fromA + (toA - fromA) * u0, fromA + (toA - fromA) * u1),
                                     (fromB + (toB - fromB) * u0, fromB + (toB - fromB) * u1)));
        }
        return [.. all];
    }

    /// <summary>Strips of floor whose centres run from one angle to another over <paramref name="secs"/>, as
    /// swept gaps that are the complement of their union. The span is split wherever an edge crosses ±180 or
    /// two edges meet (strips touching, parting or crossing), so inside each piece the edges keep one order and
    /// the complement's own edges are straight lines the gap type can carry. The pieces butt exactly, sharing
    /// their angles to the last digit, so the floor is continuous across them. The strips are
    /// <paramref name="half"/> wide at the start and <paramref name="halfTo"/> at the end (NaN = the same), so a
    /// strip can taper; a tapering edge is still a straight line.</summary>
    private static InternodeGapSpec[] SweptStrips(double dz, double secs, double half, double halfTo, params (double From, double To)[] centres)
    {
        int n = centres.Length;
        if (double.IsNaN(halfTo)) halfTo = half;
        double C(int i, double u) => centres[i].From + (centres[i].To - centres[i].From) * u;
        double H(double u) => half + (halfTo - half) * u;
        // Every edge is linear in u: side 0 is c − h, side 1 is c + h.
        double Edge(int i, int side, double u) => C(i, u) + (side == 0 ? -H(u) : H(u));
        var cuts = new SortedSet<double> { 0, 1 };
        // A linear function with values fa at u = 0 and fb at u = 1 crosses zero inside the span: cut there.
        void Root(double fa, double fb)
        {
            if (fa * fb >= 0) return;
            double u = fa / (fa - fb);
            if (u > 1e-9 && u < 1 - 1e-9) cuts.Add(u);
        }
        for (int i = 0; i < n; i++)
            for (int side = 0; side < 2; side++)
            {
                double a = Edge(i, side, 0), b = Edge(i, side, 1);
                // Seam crossings: the edge passes 180 + 360k for every k the run reaches.
                double kLo = Math.Ceiling((Math.Min(a, b) - 180) / 360), kHi = Math.Floor((Math.Max(a, b) - 180) / 360);
                for (double k = kLo; k <= kHi; k++) Root(a - (180 + 360 * k), b - (180 + 360 * k));
                // Meetings with every edge of every other strip, in every frame the pair's separation passes
                // through. ⚠ The range is taken from the separation itself, never a turn or two either side:
                // a helix unwinds its centres as far as the author writes them, so two strands ending a full
                // turn apart in opposite directions are 720° apart here, and a fixed window misses the frame
                // they actually meet in. A missed meeting merges two strips that never touched, which deletes
                // the floor between them.
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    for (int sj = 0; sj < 2; sj++)
                    {
                        double d0 = a - Edge(j, sj, 0), d1 = b - Edge(j, sj, 1);
                        double wLo = Math.Floor(Math.Min(d0, d1) / 360), wHi = Math.Ceiling(Math.Max(d0, d1) / 360);
                        for (double w = wLo; w <= wHi; w++) Root(d0 - 360 * w, d1 - 360 * w);
                    }
                }
            }
        var all = new List<InternodeGapSpec>();
        var us = cuts.ToArray();
        for (int p = 0; p + 1 < us.Length; p++)
        {
            double u0 = us[p], u1 = us[p + 1], mid = (u0 + u1) / 2;
            // Each strip in the frame where its middle is on the pipe. No edge crosses the seam inside the
            // piece, so a strip either sits wholly in [−180, 180] or straddles the seam for the whole piece
            // (one edge past ±180), which the complement below handles by wrapping round it.
            var lo0 = new double[n]; var lo1 = new double[n]; var hi0 = new double[n]; var hi1 = new double[n];
            for (int i = 0; i < n; i++)
            {
                double shift = Norm(C(i, mid)) - C(i, mid);
                lo0[i] = Edge(i, 0, u0) + shift; lo1[i] = Edge(i, 0, u1) + shift;
                hi0[i] = Edge(i, 1, u0) + shift; hi1[i] = Edge(i, 1, u1) + shift;
            }
            // ⚠ Every strip is first cut at the seam into pieces that lie inside [−180, 180]. A strip whose
            // centre is near 12 o'clock has one edge past ±180 for the whole piece, and the part beyond it
            // belongs at the other end of the range; a cut edge is the constant ±180, which is as straight a
            // line as any other, so the split costs the sweep nothing. Without this the part beyond the seam
            // would be dropped, leaving a wedge of missing floor beside 12 o'clock — and with two strips over
            // the top at once (the double helix's crossing) both straddle together, which no single-straddler
            // special case can express.
            var parts = new List<(double A0, double A1, double B0, double B1)>();
            for (int i = 0; i < n; i++)
            {
                if ((hi0[i] + hi1[i]) / 2 > 180)
                {
                    parts.Add((lo0[i], lo1[i], 180, 180));
                    parts.Add((-180, -180, hi0[i] - 360, hi1[i] - 360));
                }
                else if ((lo0[i] + lo1[i]) / 2 < -180)
                {
                    parts.Add((-180, -180, hi0[i], hi1[i]));
                    parts.Add((lo0[i] + 360, lo1[i] + 360, 180, 180));
                }
                else parts.Add((lo0[i], lo1[i], hi0[i], hi1[i]));
            }
            // Merge overlaps by their order at the middle; the order holds across the piece.
            parts.Sort((x, y) => (x.A0 + x.A1).CompareTo(y.A0 + y.A1));
            var merged = new List<(double A0, double A1, double B0, double B1)>();
            foreach (var p2 in parts)
            {
                if (merged.Count > 0 && (p2.A0 + p2.A1) / 2 <= (merged[^1].B0 + merged[^1].B1) / 2 + 1e-9)
                {
                    var m = merged[^1];
                    if ((p2.B0 + p2.B1) / 2 > (m.B0 + m.B1) / 2) merged[^1] = (m.A0, m.A1, p2.B0, p2.B1);
                }
                else merged.Add(p2);
            }
            double z0 = dz + secs * u0, len = secs * (u1 - u0);
            double cur0 = -180, cur1 = -180;
            void Emit(double a0, double a1, double b0, double b1)
            {
                if (b0 - a0 < 1e-6 && b1 - a1 < 1e-6) return;
                all.Add(new InternodeGapSpec(z0, len, a0, Math.Max(a0, b0), a1, Math.Max(a1, b1)));
            }
            foreach (var (a0, a1, b0, b1) in merged)
            {
                Emit(cur0, cur1, a0, a1);
                cur0 = Math.Max(cur0, b0); cur1 = Math.Max(cur1, b1);
            }
            Emit(cur0, cur1, 180, 180);
        }
        return [.. all];
    }

    /// <summary>A row of mines hung across the surface at one height, the same seven angles as a floor
    /// <see cref="Ring"/>: over a floor row it takes the hop away, so the row has to be walked through its
    /// door or cleared at the top of a full jump.</summary>
    private static InternodeEvent[] Hung(double dz, double height)
    {
        double max = InternodeTuning.RimDeg - InternodeTuning.MineHalfWidthDeg;
        double step = 2 * max / (RingMines - 1);
        var row = new InternodeEvent[RingMines];
        for (int i = 0; i < RingMines; i++) row[i] = ME(dz, -max + i * step, height);
        return row;
    }

    /// <summary>A floor wall across the bottom with no door: five mines at a 25° step, under the 30.4° a
    /// runner needs to slip between two, so it can only be jumped — see <see cref="MineWall"/>.</summary>
    private static InternodeEvent[] Wall(double dz) => [M(dz, -50), M(dz, -25), M(dz, 0), M(dz, 25), M(dz, 50)];

    /// <summary>A group-built phrase (<see cref="PR"/>) from a stage on, diabolical or not.</summary>
    private static InternodePhrase PRX(string id, int tier, InternodePos entry, InternodePos exit, double length,
                                       int minStage, bool diabolical, params InternodeEvent[][] groups) =>
        new(id, tier, entry, exit, length, [.. groups.SelectMany(g => g).OrderBy(e => e.Dz)], MinStage: minStage, Diabolical: diabolical);

    /// <summary>A curving plank: one strip whose centre runs from <paramref name="from"/> to <paramref name="to"/>
    /// degrees over <paramref name="secs"/>, as <paramref name="steps"/> chords of swept gaps that meet exactly, so
    /// the edge is a continuous line rather than a staircase. <paramref name="eased"/> shapes the run as a
    /// smoothstep (still at both ends, fastest in the middle), which also joins C1 to a straight or another eased
    /// bend either side; a helix passes <c>false</c> for a constant rate, where the chord count is moot. The
    /// centre may run through ±180 on a <c>Closed</c> phrase. <paramref name="halfTo"/> tapers the strip's
    /// width linearly over the run (NaN = constant).</summary>
    private static InternodeGapSpec[] Curve(double dz, double secs, int steps, double from, double to, double half,
                                            bool eased = true, double halfTo = double.NaN, bool ramped = false)
    {
        var all = new List<InternodeGapSpec>();
        double dt = secs / steps;
        if (double.IsNaN(halfTo)) halfTo = half;
        double At(double u) { u = ramped ? SweepAt(u) : eased ? u * u * (3 - 2 * u) : u; return from + (to - from) * u; }
        double Half(double u) => half + (halfTo - half) * u;
        for (int i = 0; i < steps; i++)
        {
            double u0 = (double)i / steps, u1 = (double)(i + 1) / steps;
            all.AddRange(SweptStrips(dz + i * dt, dt, Half(u0), Half(u1), (At(u0), At(u1))));
        }
        return [.. all];
    }

    /// <summary>A mine hung above the floor at <paramref name="height"/>: passed under on the ground or over
    /// near the apex, struck by a hop that comes level with it. Over a floor mine at the same plane the pair
    /// closes the hop as an answer.</summary>
    private static InternodeEvent ME(double dz, double theta, double height) => new(dz, theta, Mine, height);

    /// <summary>A phrase with its own track that also needs the full tube, from a stage on; see the flags on
    /// <see cref="InternodePhrase"/>.</summary>
    private static InternodePhrase PGX(string id, int tier, InternodePos entry, InternodePos exit, double length,
                                       InternodeGapSpec[][] gaps, bool closed, int minStage, bool diabolical, params InternodeEvent[] events) =>
        new(id, tier, entry, exit, length, events, Gaps: [.. gaps.SelectMany(g => g)], Closed: closed, MinStage: minStage, Diabolical: diabolical);

    /// <summary>An object-only phrase from a stage on, diabolical or not.</summary>
    private static InternodePhrase PX(string id, int tier, InternodePos entry, InternodePos exit, double length,
                                      int minStage, bool diabolical, params InternodeEvent[] events) =>
        new(id, tier, entry, exit, length, events, MinStage: minStage, Diabolical: diabolical);

    /// <summary>A hole through the middle, floor surviving on both walls: the runner has to be off the bottom
    /// before it opens, and either wall will do.</summary>
    private static InternodeGapSpec[] Slot(double dz, double secs, double half) => [G(dz, secs, -half, half)];

    /// <summary>One side gone, from <paramref name="from"/> outward to its rim. Negative cuts the left.</summary>
    private static InternodeGapSpec[] Side(double dz, double secs, double from) =>
        from >= 0 ? [G(dz, secs, from, 180)] : [G(dz, secs, -180, from)];

    /// <summary>A phrase that cuts its own floor. <paramref name="gaps"/> are concatenated in order.</summary>
    private static InternodePhrase PG(string id, int tier, InternodePos entry, InternodePos exit, double length,
                                      InternodeGapSpec[][] gaps, params InternodeEvent[] events) =>
        new(id, tier, entry, exit, length, events, Gaps: [.. gaps.SelectMany(g => g)]);

    // Every phrase opens with a beat of empty pipe (first event ≥ 0.8 s authored) so a runner arriving at a band
    // corner can turn round; direction changes are ≥ 0.6 s apart. An elevated orb needs ≥ 1.2 s of run-up and
    // ≥ 0.6 s after it: that is the one jump timing that fits inside the jump's airtime. A mine one beat ahead of
    // a token at the same angle is the bank's workhorse figure — it forces a swerve and a return.

    // ── Tier 1: the opening vocabulary, and where mines are learnt ─────────────
    private static readonly InternodePhrase Straight =
        P("straight", 1, InternodePos.Centre, InternodePos.Centre, 3.2, T(0.8, 0), T(1.4, 0), T(2.0, 0), T(2.6, 0));
    private static readonly InternodePhrase Sway =
        P("sway", 1, InternodePos.Centre, InternodePos.Centre, 4.4,
          T(0.8, -12), M(1.4, -12), T(2.0, 12), M(2.6, 12), T(3.2, -12), T(3.8, 0));
    /// <summary>Two mines flanking a gap the token sits in: the first thing the player reads as a corridor.</summary>
    private static readonly InternodePhrase MineSlot =
        P("mine-slot", 1, InternodePos.Centre, InternodePos.Centre, 4.2,
          M(0.9, -27), M(0.9, 27), T(1.6, 0), M(2.3, -27), M(2.3, 27), T(3.0, 0), T(3.6, 0));
    /// <summary>A mine on the line you are holding, so the lane itself has to change.</summary>
    private static readonly InternodePhrase MineShift =
        P("mine-shift", 1, InternodePos.Centre, InternodePos.LowLeft, 4.2,
          M(0.9, 0), T(1.5, -17), M(2.1, -17), T(2.7, -38), M(3.3, -12), T(3.7, -33));
    private static readonly InternodePhrase LeanLeft =
        P("lean-left", 1, InternodePos.Centre, InternodePos.LowLeft, 3.8,
          T(0.8, -8), T(1.5, -17), M(2.0, 0), T(2.4, -27), T(3.1, -33));
    private static readonly InternodePhrase ReturnLeft =
        P("return-left", 1, InternodePos.LowLeft, InternodePos.Centre, 3.8,
          T(0.8, -30), M(1.4, -41), T(2.0, -19), M(2.6, -30), T(3.2, 0));
    private static readonly InternodePhrase BreatheCentre =
        P("breathe-centre", 1, InternodePos.Centre, InternodePos.Centre, 2.4, T(1.2, 0));
    private static readonly InternodePhrase BreatheLowLeft =
        P("breathe-lowleft", 1, InternodePos.LowLeft, InternodePos.LowLeft, 2.4, T(1.2, -31));
    private static readonly InternodePhrase BreatheHighLeft =
        P("breathe-highleft", 1, InternodePos.HighLeft, InternodePos.HighLeft, 2.4, T(1.2, -73));

    // ── Tier 1 token shapes: what level 1 is made of ──────────────────────────
    // All orbs, no mines, no holes: these teach steering without an obstacle, which is what the first level
    // needs. Spacing follows the tier-1 idiom — a beat of empty pipe, then orbs 0.7-0.8 s apart moving no
    // more than about 10° between them, the gentle rate `lean-left` and `sway` establish.

    /// <summary>A shallow arc out and back: the first figure that asks for a lean and a return in one breath.</summary>
    private static readonly InternodePhrase OrbArc =
        P("orb-arc", 1, InternodePos.Centre, InternodePos.Centre, 3.8,
          T(0.8, 0), T(1.4, -14), T(2.0, -22), T(2.6, -14), T(3.2, 0));

    /// <summary>A weave across the bottom line: four crossings, none of them far. Teaches that the stick is
    /// an accelerator and not a position — the runner has to start turning before the next orb.</summary>
    private static readonly InternodePhrase OrbWeave =
        P("orb-weave", 1, InternodePos.Centre, InternodePos.Centre, 4.6,
          T(0.8, -11), T(1.6, 11), T(2.4, -11), T(3.2, 11), T(4.0, 0));

    /// <summary>Orbs stepping out to a low wall, one lean held all the way. The token-only twin of
    /// <c>lean-left</c>, which does the same journey with a mine in the middle of it.</summary>
    private static readonly InternodePhrase OrbStairLeft =
        P("orb-stair-left", 1, InternodePos.Centre, InternodePos.LowLeft, 4.2,
          T(0.8, -6), T(1.5, -14), T(2.2, -22), T(2.9, -30), T(3.6, -34));

    /// <summary>The route home, on orbs alone.
    ///
    /// <para>⚠ Keep a mine-free route home at tier 1: every other tier-1 phrase leaving a low wall for Centre
    /// carries two mines, so losing this one would force a mine figure on every return to the bottom.</para></summary>
    private static readonly InternodePhrase OrbHomeLeft =
        P("orb-home-left", 1, InternodePos.LowLeft, InternodePos.Centre, 4.2,
          T(0.8, -32), T(1.5, -24), T(2.2, -16), T(2.9, -8), T(3.6, 0));

    /// <summary>Four orbs along a low wall, dipping and rising: something to do while out there, where the
    /// only tier-1 option was a single orb and a wait.</summary>
    private static readonly InternodePhrase OrbHoldLeft =
        P("orb-hold-left", 1, InternodePos.LowLeft, InternodePos.LowLeft, 3.8,
          T(0.8, -30), T(1.6, -36), T(2.4, -30), T(3.2, -26));

    // ── Tier 2: the walls, the first crossing, and mines that shape a route ────
    private static readonly InternodePhrase ClimbLeft =
        P("climb-left", 2, InternodePos.Centre, InternodePos.LowLeft, 3.4, T(0.8, -13), T(1.4, -25), M(1.9, -8), T(2.4, -37));
    private static readonly InternodePhrase WallRideLeft =
        P("wallride-left", 2, InternodePos.LowLeft, InternodePos.HighLeft, 3.8,
          T(0.8, -37), T(1.5, -48), M(2.0, -35), T(2.4, -60), T(2.9, -72));
    private static readonly InternodePhrase HighHoldLeft =
        P("highhold-left", 2, InternodePos.HighLeft, InternodePos.HighLeft, 3.6,
          T(0.8, -70), M(1.4, -59), T(1.8, -79), T(2.2, -79), T(2.9, -70));
    private static readonly InternodePhrase DropInLeft =
        P("dropin-left", 2, InternodePos.HighLeft, InternodePos.Centre, 4.0,
          T(0.8, -67), T(1.5, -47), M(2.0, -31), T(2.6, -6), T(3.0, 6), M(3.4, 19));
    private static readonly InternodePhrase Slalom =
        P("slalom", 2, InternodePos.Centre, InternodePos.Centre, 4.2,
          T(0.8, 0), M(1.3, 0), T(1.9, 19), M(2.4, 19), T(3.0, -7), M(3.4, -7));
    /// <summary>Mines alternating sides with the orbs in the gaps: a rhythm you read rather than react to.</summary>
    private static readonly InternodePhrase MineComb =
        P("mine-comb", 2, InternodePos.Centre, InternodePos.Centre, 4.6,
          M(0.9, -23), T(1.4, 23), M(1.9, 23), T(2.4, -23), M(2.9, -23), T(3.4, 23), T(4.0, 0));
    /// <summary>The early crossing, charged from the bottom: in a half-pipe there is no room high on a wall to build
    /// the speed a flight over the top needs, so the run-up starts at the floor, climbs the whole left wall, flies
    /// the open top and comes down the right. Orbs sit in the gap as well as on the ground — above the rim there is
    /// no surface and an orb is collected on angle alone, so the flight itself is paid for. Time-locked and laid
    /// along the flight a full stick produces from rest at Centre, so one stick timing collects every orb through the
    /// landing (the probe proves it); the stage tempo never compresses it. ⚠ Not paced to a runner already at the speed
    /// cap: that would overshoot for everyone else.</summary>
    private static readonly InternodePhrase ArchLeft =
        Crossing("arch-left", 2, InternodePos.Centre, InternodePos.Centre, 3.2, true,
          T(0.5, -16), T(0.7, -56), T(0.9, -111), T(1.1, -158), T(1.3, 168), T(1.5, 137), T(1.7, 98), T(2.0, 55), T(2.4, 15));
    /// <summary>Mines one step down-wall from every orb, so the climb can never be given up on.</summary>
    private static readonly InternodePhrase MineStairLeft =
        P("mine-stair-left", 2, InternodePos.LowLeft, InternodePos.HighLeft, 4.6,
          T(0.9, -33), M(1.5, -19), T(1.9, -47), M(2.5, -32), T(2.9, -60), M(3.5, -45), T(3.9, -73));

    /// <summary>A wall with no way through it: the ring's bands overlap, so this figure is the game saying jump.</summary>
    private static readonly InternodePhrase MineWall =
        PR("mine-wall", 2, InternodePos.Centre, InternodePos.Centre, 4.6, [T(0.8, 0)], Ring(2.2), [T(3.4, 0), T(4.0, 0)]);

    // ── Tier 3: crossings under pressure, and the spiral ──────────────────────
    /// <summary>A wall of mines with no gap: the one figure that can only be jumped.
    ///
    /// <para>Three mines at a 25° step, spanning ±40.2°: inside the 30.4° a runner needs to slip between two,
    /// so the band stays a single overlapping wall. ⚠ Both margins are live at once: wider spacing opens a
    /// hole to thread; narrower re-overlaps the mine art. InternodeProbe checks the first.</para></summary>
    private static readonly InternodePhrase MineGate =
        P("minegate", 3, InternodePos.Centre, InternodePos.Centre, 3.8,
          T(0.8, 0), M(1.6, -25), M(1.6, 0), M(1.6, 25), T(2.4, 0), T(3.0, 0));
    private static readonly InternodePhrase WallMinesLeft =
        P("wallmines-left", 3, InternodePos.HighLeft, InternodePos.HighLeft, 4.2,
          T(0.8, -73), M(1.5, -73), T(2.2, -83), M(2.9, -83), T(3.6, -62));
    /// <summary>One continuous roll with orbs in the gap, charged from the bottom: up the left wall, across the open
    /// top, down the right, through the bottom and out to the low left. Time-locked to the full-stick flight like the
    /// arch. The tail is empty on purpose — the runner arrives fast and has to arrest to finish in the band.</summary>
    private static readonly InternodePhrase SpiralLeft =
        Crossing("spiral-left", 3, InternodePos.Centre, InternodePos.LowLeft, 3.4, true,
          T(0.5, -16), T(0.7, -56), T(0.9, -111), T(1.1, -158), T(1.3, 168), T(1.5, 137), T(1.7, 98), T(1.9, 51),
          T(2.1, -2));
    /// <summary>An arc off the ground: run the first orb down, then jump to follow the rest through the air. The
    /// heights are a jump parabola sampled at the plane spacing, so the figure reads as the jump it asks for;
    /// the catch itself only needs the runner airborne on each plane, so the launch window is the whole stretch
    /// that keeps them off the ground through the last lifted orb.</summary>
    private static readonly InternodePhrase ArcHop =
        P("arc-hop", 2, InternodePos.Centre, InternodePos.Centre, 3.8,
          T(0.8, 0), E(1.6, 0, 0.51), E(2.0, 0, 0.66), E(2.4, 0, 0.51), T(3.2, 0));
    /// <summary>The same arc thrown off a wall: run the orb down high on the left, then follow the arc as the
    /// airborne pull swings it in toward the bottom. ⚠ It drifts inward by necessity — the homing pull grows with
    /// |θ|, so an airborne arc climbing further up the wall is not something the physics allows.</summary>
    private static readonly InternodePhrase ArcDropLeft =
        P("arc-drop-left", 3, InternodePos.LowLeft, InternodePos.Centre, 4.4,
          T(0.8, -35), E(1.6, -29, 0.51), E(2.0, -19, 0.66), E(2.4, -7, 0.51), T(3.2, 4));

    /// <summary>A ring with one corridor cut out of it, and the orb sitting where the corridor lets out.</summary>
    private static readonly InternodePhrase MineEyeLeft =
        PR("mine-eye-left", 3, InternodePos.Centre, InternodePos.Centre, 4.8,
           [T(0.8, 0)], Ring(2.1, -27, 36), [T(3.1, -23), T(3.8, -7)]);
    /// <summary>Two walls close enough that one committed jump clears both — or two taps, for anyone who reads it
    /// that way.</summary>
    private static readonly InternodePhrase MineDouble =
        PR("mine-double", 3, InternodePos.Centre, InternodePos.Centre, 5.4, [T(0.8, 0)], Ring(2.2), Ring(3.4), [T(4.6, 0)]);

    // ── Tier 4 ────────────────────────────────────────────────────────────────
    /// <summary>⚠ Each orb sits on the line the next mine arrives on, never behind the mine it follows: an orb
    /// at a mine's own angle a beat behind it is hidden until the mine sweeps past
    /// (<see cref="InternodeTuning.MinMineShadowLeadSeconds"/>), and this shape is the one that fails that rule
    /// first when the step tightens. So the figure is collect-then-cross: take the orb, the mine lands where
    /// you stood, and the next orb is already across.</summary>
    private static readonly InternodePhrase Zigzag =
        P("zigzag", 4, InternodePos.Centre, InternodePos.Centre, 4.0,
          T(0.8, 12), M(1.1, 12), T(1.5, -12), M(1.8, -12), T(2.2, 12), M(2.5, 12), T(2.9, -12), T(3.6, 0));
    /// <summary>Three mine rings stacked too close to jump as one (0.35 s apart at the stage-1 tempo, against a
    /// 0.7 s first jump), with the same two-mine doorway in each: the only way through is to line up and thread it.</summary>
    private static readonly InternodePhrase Doorway =
        PR("doorway", 3, InternodePos.Centre, InternodePos.Centre, 5.4, [T(0.8, 0)], Rings(1.8, 3, 0.7, 5.7, 18, 0), [T(4.4, 0)]);
    /// <summary>A wall of mines whose door turns 22° further each ring — a rotating wall, followed round to the left.</summary>
    private static readonly InternodePhrase HelixLeft =
        PR("helix-left", 3, InternodePos.Centre, InternodePos.Centre, 6.4, [T(0.8, 0)], Rings(1.8, 4, 0.9, 0, 30, -22), [T(5.6, -20)]);
    /// <summary>The two combined: a stacked wall with a two-mine door that turns 20° per ring.</summary>
    private static readonly InternodePhrase VortexLeft =
        PR("vortex-left", 4, InternodePos.Centre, InternodePos.Centre, 6.4, [T(0.8, 0)], Rings(1.8, 3, 0.8, 5.7, 18, -20), [T(5.4, -20)]);



    // ── Track figures: the floor is the challenge ─────────────────────────────
    // Twenty phrases built on holes rather than on rows of mines with a door in them. Mines appear, but as
    // seasoning on a geometry problem: the question a player answers here is "where is there floor, and can I
    // be on it", not "which slot is open".
    //
    // ⚠ Every one is rim-safe, and that is load-bearing rather than incidental: a floor puzzle has to be
    // solvable on foot, so the probe proves each with flight forbidden. If one of these ever needs the open
    // top to survive, it has stopped being a track figure and become a crossing.

    // ── Tier 1: the vocabulary, one idea at a time ────────────────────────────
    /// <summary>One hole, an orb either side of it. The whole grammar of the set in its simplest form: floor,
    /// no floor, floor.</summary>
    private static readonly InternodePhrase GapStep =
        PG("gap-step", 1, InternodePos.Centre, InternodePos.Centre, 4.6,
           [Hole(1.7, 1.0)],
           T(1.0, 0), T(3.1, 0), T(3.8, 0));

    /// <summary>Two holes with one beat of floor between them: land and go again, which is a different skill
    /// from clearing one hole and the reason the pair is authored rather than left to two phrases meeting.</summary>
    private static readonly InternodePhrase GapTwoHop =
        PG("gap-twohop", 1, InternodePos.Centre, InternodePos.Centre, 5.4,
           [Hole(1.6, 0.9), Hole(3.3, 0.9)],
           T(1.0, 0), T(2.9, 0), T(4.6, 0), T(5.0, 0));

    /// <summary>Half the floor gone, the orbs on the half that is left: the answer is to stand somewhere else,
    /// not to jump. The gentlest way to teach that a hole has a shape.</summary>
    private static readonly InternodePhrase GapShoulderLeft =
        PG("gap-shoulder-left", 1, InternodePos.Centre, InternodePos.LowRight, 5.0,
           [Side(1.4, 2.4, -8)],
           T(0.9, 12), T(2.0, 30), T(2.9, 34), T(3.8, 30));

    // ── Tier 2: shapes that have to be held ───────────────────────────────────
    /// <summary>A strip down the middle and nothing either side: hold the bottom for a second and a half.
    /// Easy to describe, and the first figure that asks for a steady hand rather than a press.</summary>
    private static readonly InternodePhrase RibbonHold =
        PG("ribbon-hold", 2, InternodePos.Centre, InternodePos.Centre, 5.6,
           [Ribbon(1.5, 2.4, 0, 24)],
           T(1.0, 0), T(2.2, 0), T(3.0, 0), T(3.7, 0), T(4.8, 0));

    /// <summary>The strip, but off to one side, so holding it is a climb held rather than a rest at the
    /// bottom — the pendulum is pulling the whole time.</summary>
    private static readonly InternodePhrase RibbonLeanLeft =
        PG("ribbon-lean-left", 2, InternodePos.Centre, InternodePos.LowLeft, 5.8,
           [Ribbon(1.6, 2.4, -34, 22)],
           T(1.0, -14), T(2.3, -34), T(3.1, -34), T(3.9, -34), T(5.0, -32));

    /// <summary>Left half, then right half, then left: a slalom made of holes. Nothing to jump — the runner
    /// crosses the bottom three times because the floor keeps moving out from under them.</summary>
    private static readonly InternodePhrase GapSlalom =
        PG("gap-slalom", 2, InternodePos.Centre, InternodePos.Centre, 6.4,
           [Side(1.2, 1.3, -6), Side(2.8, 1.3, 6), Side(4.4, 1.3, -6)],
           T(1.6, 26), T(3.2, -26), T(4.8, 26), T(6.0, 0));

    /// <summary>Three holes, each one longer than the last, with the floor between them getting shorter: the
    /// same press with less and less room to set up for it. A difficulty ramp inside one phrase.
    ///
    /// <para>⚠ Every orb sits at least 0.45 s of authored floor after a hole closes: a ground orb needs the
    /// runner already landed, not still rising out of the hole. 0.4 s is the shortest recovery the passing
    /// figures in this set use — treat it as the floor for one after a hole.</para></summary>
    private static readonly InternodePhrase GapLadder =
        PG("gap-ladder", 2, InternodePos.Centre, InternodePos.Centre, 7.5,
           [Hole(1.5, 0.8), Hole(3.4, 0.9), Hole(5.2, 1.0)],
           T(1.0, 0), T(2.85, 0), T(4.75, 0), T(6.7, 0), T(7.1, 0));

    // ── Tier 3: the floor moves ───────────────────────────────────────────────
    /// <summary>A plank that rotates: the strip of surviving floor marches from one low wall across the bottom
    /// to the other in steps the runner rides, never jumps. The steps butt in z and their floors share an
    /// 18° corridor, so a runner leaning into the sweep is standing on both at the seam.
    ///
    /// <para>⚠ Ribbons must never overlap in z. Gaps are unioned, so two ribbons sharing a stretch leave only
    /// the intersection of their floors — a sliver, or nothing at all once the shift exceeds the width — and
    /// the renderer draws both outlines over it, so the plank reads as continuous exactly where it is not.
    /// A ridable hand-over is butt-joined steps whose floors overlap in θ; a forced-jump hand-over is
    /// <see cref="PlankHopLeft"/>. Nothing in between.</para>
    ///
    /// <para>The three holds are joined by short swept moves rather than butted: butting two ribbons in z
    /// shares a renderer band at the seam, where the union of their gaps leaves only the sliver of floor
    /// common to both, so the plank would read as three pads with hairline bridges. The swept joins keep the
    /// full width throughout.</para></summary>
    private static readonly InternodePhrase PlankSweepLeft =
        PG("plank-sweep-left", 3, InternodePos.Centre, InternodePos.LowRight, 7.4,
           [Strip(1.4, 1.0, -36, 24), Curve(2.4, 0.6, 2, -36, -6, 24), Strip(3.0, 1.0, -6, 24), Curve(4.0, 0.6, 2, -6, 24, 24), Strip(4.6, 2.0, 24, 24)],
           T(1.0, -18), T(2.2, -36), T(3.6, -6), T(5.2, 24), T(6.4, 28));

    /// <summary>A plank that ends: the strip stops, a short void, and the next strip stands on the far side
    /// of the bottom with no floor shared between them. The hand-over is a jump with a sideways move in it,
    /// the authored twin of the sequencer's staggered plank. The 52° shift against a 40° strip is what
    /// forces the jump; the void is what makes it readable.</summary>
    private static readonly InternodePhrase PlankHopLeft =
        PG("plank-hop-left", 3, InternodePos.Centre, InternodePos.LowRight, 6.6,
           [Ribbon(1.4, 1.6, -30, 20), Hole(3.0, 0.4), Ribbon(3.4, 1.6, 22, 20)],
           T(1.0, -14), T(2.2, -30), T(4.0, 22), T(4.6, 22), T(5.6, 28));

    /// <summary>Two strips of floor at once, one low on each side, and their breaks alternate: whichever one
    /// the runner is on gives way, so the phrase is a sequence of hops between two moving footholds.</summary>
    private static readonly InternodePhrase TwinRibbon =
        PG("twin-ribbon", 3, InternodePos.Centre, InternodePos.Centre, 7.0,
           [
               // Both strips standing: floor at -44..-14 and 14..44, nothing elsewhere.
               [G(1.3, 1.5, -180, -44), G(1.3, 1.5, -14, 14), G(1.3, 1.5, 44, 180)],
               // The left strip goes; only the right is standable.
               [G(2.9, 1.2, -180, 14), G(2.9, 1.2, 44, 180)],
               // …and now the right one goes instead.
               [G(4.2, 1.2, -180, -44), G(4.2, 1.2, -14, 180)],
               // Both back, to land on.
               [G(5.5, 1.0, -180, -44), G(5.5, 1.0, -14, 14), G(5.5, 1.0, 44, 180)],
           ],
           T(1.0, -28), T(2.0, -28), T(3.6, 28), T(4.8, -28), T(6.0, 28));

    /// <summary>Islands: short pads of floor with nothing between them, each at a different angle, so every
    /// jump has to be aimed as well as timed.</summary>
    private static readonly InternodePhrase IslandHop =
        PG("island-hop-left", 3, InternodePos.Centre, InternodePos.Centre, 7.2,
           [
               [G(1.3, 1.0, -180, -46), G(1.3, 1.0, -16, 180)],   // a pad low-left
               Hole(2.3, 0.9),
               [G(3.2, 1.0, -180, 16), G(3.2, 1.0, 46, 180)],     // a pad low-right
               Hole(4.2, 0.9),
               [G(5.1, 1.0, -180, -18), G(5.1, 1.0, 18, 180)],    // a pad at the bottom
           ],
           T(1.0, -20), T(1.9, -30), T(3.8, 30), T(5.6, 0), T(6.6, 0));

    /// <summary>A strip with a clean break across the middle of it: hold the line, jump without losing it,
    /// carry on. The break is full width, so there is no running round.</summary>
    private static readonly InternodePhrase RibbonBreak =
        PG("ribbon-break", 3, InternodePos.Centre, InternodePos.Centre, 7.0,
           [Ribbon(1.4, 1.8, 0, 22), Hole(3.2, 0.9), Ribbon(4.1, 1.8, 0, 22)],
           T(1.0, 0), T(2.2, 0), T(2.9, 0), T(4.5, 0), T(5.4, 0), T(6.4, 0));

    /// <summary>A strip with a mine standing on it: the limited mine content this set carries, placed to make
    /// the geometry harder rather than to be the challenge itself. The floor is a corridor and something is
    /// parked in it, so the swerve has to happen inside the corridor's width.</summary>
    private static readonly InternodePhrase MineRibbon =
        PG("mine-ribbon", 3, InternodePos.Centre, InternodePos.Centre, 6.8,
           [Ribbon(1.4, 3.0, 0, 30)],
           T(1.0, 0), T(2.1, -18), M(2.9, 0), T(3.7, 18), T(4.4, 0), T(5.8, 0));

    // ── Tier 4: geometry under pressure ───────────────────────────────────────
    /// <summary>The floor gone but for a band high on one wall: a genuine wall ride, held over a drop, with
    /// the pendulum pulling the runner off it the whole way.</summary>
    private static readonly InternodePhrase WallRunLeft =
        PG("wallrun-left", 4, InternodePos.LowLeft, InternodePos.LowLeft, 6.6,
           [Ribbon(1.3, 2.8, -68, 20)],
           T(1.0, -66), T(2.2, -66), T(3.1, -70), T(4.0, -66), T(5.4, -44));   // the approach orb is already at the band's height

    /// <summary>Three holes at nearly the full jumpable length with barely a beat between: the set's endurance
    /// figure. Nothing clever, just no room to breathe.</summary>
    private static readonly InternodePhrase ChasmChain =
        PG("chasm-chain", 4, InternodePos.Centre, InternodePos.Centre, 7.0,
           [Hole(1.4, 1.1), Hole(3.0, 1.1), Hole(4.6, 1.1)],
           T(1.0, 0), T(2.8, 0), T(4.4, 0), T(6.0, 0), T(6.5, 0));

    /// <summary>A needle: a strip half the usual width, held for a long moment. The margin is the challenge.</summary>
    private static readonly InternodePhrase NeedleHold =
        PG("needle-hold", 4, InternodePos.Centre, InternodePos.Centre, 6.4,
           [Ribbon(1.5, 3.0, 0, 14)],
           T(1.0, 0), T(2.2, 0), T(3.0, 0), T(3.8, 0), T(4.6, 0), T(5.9, 0));

    /// <summary>Two strips that swap sides under the runner: the pair from <see cref="TwinRibbon"/>, but each
    /// break hands over to the far one rather than to its neighbour, so every hop crosses the bottom. A short
    /// full-width void sits between each strip and the next: the jump is forced by the 40° of missing floor
    /// between them either way, and the void is the lip that tells the player so. ⚠ The strips never overlap
    /// in z — see <see cref="PlankSweepLeft"/> for why.</summary>
    private static readonly InternodePhrase TwinRibbonCross =
        PG("twin-ribbon-cross", 4, InternodePos.Centre, InternodePos.Centre, 7.6,
           [
               Ribbon(1.3, 1.2, -35, 15),   // the left strip alone
               Hole(2.5, 0.3),
               Ribbon(2.8, 1.2, 35, 15),    // the right strip alone
               Hole(4.0, 0.3),
               Ribbon(4.3, 1.2, -35, 15),   // left again
               Hole(5.5, 0.3),
               Ribbon(5.8, 1.2, 35, 15),    // right again
           ],
           T(1.0, -34), T(2.0, -34), T(3.4, 34), T(4.9, -34), T(6.4, 34), T(7.3, 34));

    // ── Choice phrases: two routes, take either ───────────────────────────────
    // ⚠ These are the only phrases whose orbs sit on <see cref="InternodeEvent.Branch"/> above 0, and the
    // whole point is that a player collects one branch. Everything that counts orbs has to know:
    //   · the gate's demand counts branch 0 plus the best branch, never the sum (InternodeSection);
    //   · they never pay gold, because "every orb of the phrase" is unsatisfiable (Internode.IndexPhrases);
    //   · the oracle proves each branch separately, and every branch must pass (InternodeProbe);
    //   · one-orb-per-plane and orb spacing are per route, so the branches deliberately share planes.
    // Keep them symmetric left/right and they need no -left/-right twin: the mirror of the phrase is the
    // phrase, with its two branches swapped.

    /// <summary>A row down each low wall: commit left or commit right, then rejoin at the bottom. The gentlest
    /// form of the idea — both rows sit inside the low bands, so either is a hold rather than a climb, and the
    /// shared orb at the end puts every route back in the Centre band for whatever follows.</summary>
    private static readonly InternodePhrase Split =
        P("split", 2, InternodePos.Centre, InternodePos.Centre, 4.6,
          TB(1.0, -38, 1), TB(1.0, 38, 2),
          TB(1.8, -38, 1), TB(1.8, 38, 2),
          TB(2.6, -38, 1), TB(2.6, 38, 2),
          T(3.8, 0));

    /// <summary>The same choice up on the walls, where holding the line costs real stick. ⚠ Both rows are at
    /// 62°, inside the high bands (57–90) — a route here is a climb held for over a second, which is why this
    /// is a tier above <see cref="Split"/> rather than a wider version of it.</summary>
    private static readonly InternodePhrase SplitWide =
        P("split-wide", 3, InternodePos.Centre, InternodePos.Centre, 5.0,
          TB(1.2, -62, 1), TB(1.2, 62, 2),
          TB(2.1, -62, 1), TB(2.1, 62, 2),
          TB(3.0, -62, 1), TB(3.0, 62, 2),
          T(4.2, 0));

    /// <summary>An X over the open top: one route rolls left and over, the other rolls right and over, and the
    /// two cross near 12 o'clock. Time-locked to the full-stick flight, like the arch and the spiral — the
    /// timing is what the physics makes it, and the stage tempo must not compress it.
    ///
    /// <para>⚠ The branches are not decoration here, they are the two directions. A player charges one way
    /// from the bottom and rides that side of the X; there is no route that takes both, and the crossing
    /// check proves each direction collects its own branch through the landing.</para></summary>
    private static readonly InternodePhrase CrossoverX =
        Crossing("crossover-x", 4, InternodePos.Centre, InternodePos.Centre, 3.6, true,
          TB(0.5, -16, 1), TB(0.5, 16, 2),
          TB(0.7, -56, 1), TB(0.7, 56, 2),
          TB(0.9, -111, 1), TB(0.9, 111, 2),
          TB(1.1, -158, 1), TB(1.1, 158, 2),
          TB(1.3, 168, 1), TB(1.3, -168, 2),
          TB(1.5, 137, 1), TB(1.5, -137, 2),
          TB(1.7, 98, 1), TB(1.7, -98, 2),
          TB(1.9, 51, 1), TB(1.9, -51, 2),
          T(2.1, 0));   // One shared last orb ends both routes, so the gold candidate is the same orb whichever way you rolled

    // ── Curving planks (from stage 20) and the diabolical set (from stage 50, ramping to 100) ────────────
    // ⚠ Every strip here is a swept strip (Curve / Braid / SweptStrips): its edges run continuously, so the
    // eye reads a bend, and tokens sit on the strip's centre at their moment. A Closed phrase rides over the
    // top: the sequencer forces a full tube round it, and the oracle proves it at rim π. The over-the-top
    // sweeps run fast, near 120°/s authored (roughly two thirds of the steering cap at the floor time scale),
    // so a full-stick swing keeps pace with the strip rather than overshooting a slow one.

    // Widths and stages ramp gradually: a 60° bend at 20, the same bend at 44° from 45, then the serpent and
    // the fork, and only past 55 anything over the top — a level's hardest figure must have had its build-up.

    /// <summary>A wide strip that bends out to the low wall and back: the first plank to steer along rather
    /// than hold. 6° steps under a 60° strip, wider than the sequencer's own planks.</summary>
    private static readonly InternodePhrase BendPlankLeft =
        PGX("bend-plank-left", 3, InternodePos.Centre, InternodePos.Centre, 6.2,
            [Curve(1.2, 1.8, 6, 0, -36, 30), Curve(3.0, 1.8, 6, -36, 0, 30)], closed: false, minStage: 20, diabolical: false,
            T(0.9, 0), T(1.8, -14), T(2.7, -32), T(3.6, -30), T(4.4, -12), T(5.4, 0));

    /// <summary>The same bend on a 44° strip, once the wide one is familiar.</summary>
    private static readonly InternodePhrase BendPlankNarrowLeft =
        PGX("bend-plank-narrow-left", 4, InternodePos.Centre, InternodePos.Centre, 6.2,
            [Curve(1.2, 1.8, 6, 0, -36, 22), Curve(3.0, 1.8, 6, -36, 0, 22)], closed: false, minStage: 45, diabolical: false,
            T(0.9, 0), T(1.8, -14), T(2.7, -32), T(3.6, -30), T(4.4, -12), T(5.4, 0));

    /// <summary>Three bends in a row on a 52° strip, the second crossing the bottom at 10° a step.</summary>
    private static readonly InternodePhrase SerpentPlank =
        PGX("serpent-plank", 4, InternodePos.Centre, InternodePos.Centre, 7.4,
            [Curve(1.2, 1.8, 6, 0, -30, 26), Curve(3.0, 1.8, 6, -30, 30, 26), Curve(4.8, 1.8, 6, 30, 0, 26)], closed: false, minStage: 35, diabolical: false,
            T(0.9, 0), T(1.9, -12), T(2.8, -28), T(3.9, 0), T(4.7, 28), T(5.6, 15), T(6.5, 0));

    /// <summary>One continuous turn round a closed tube, up one wall, through 12 o'clock and down the other.
    /// ⚠ Never a hold at the top: the strip is at its fastest there, and a pause is the one thing that makes
    /// this figure hard instead of fast. The strip is 68° wide at the foot for a forgiving entry and tightens
    /// to 52° as it goes. The first over-the-top figure, so it ramps in with the late set.
    ///
    /// <para><see cref="SweepSeconds"/> is the shared pace of every over-the-top sweep in the bank.</para></summary>
    private static readonly InternodePhrase SkyPlank = OverTopTurn("sky-plank", 34, 26, 55, true);

    /// <summary>The same full turn on a 40° strip that does not widen at the foot, so the entry has to be aimed
    /// as well as ridden. Sky Plank is the wide teacher of this shape.</summary>
    private static readonly InternodePhrase HelixPlankLeft = OverTopTurn("helix-plank-left", 20, 20, 70, true);

    /// <summary>How long a sweep takes to carry a strip the whole way round the pipe, authored seconds. The
    /// stage time scale is at its floor (<see cref="InternodeTuning.PhraseTimeScaleMin"/>, 0.45) everywhere
    /// these phrases are offered, so 360° in this many authored seconds is 360/(this × 0.45) degrees a second
    /// on the clock the player lives on: 276°/s here, peaking at 345°/s through the middle on
    /// <see cref="SweepAt"/>'s flat top — 86% of <see cref="InternodeTuning.MaxAngularSpeedDeg"/> (400).
    ///
    /// <para>⚠ It may not be pushed to the cap. The oracle's worst-plane survivor count for a single-strand
    /// turn falls off a cliff as the peak approaches it: 29 here, 23 at 2.75, and one at 2.6, where the peak
    /// is the cap itself and the only way round is frame-perfect. A strip the runner cannot out-steer leaves
    /// no authority to correct with, and the two strands of a double helix are the exception only because they
    /// merge into one wide strip at the bottom and again at the crossing.</para></summary>
    private const double SweepSeconds = 2.9;

    /// <summary>The share of a sweep spent winding up, and the same again easing out.</summary>
    private const double SweepRamp = 0.2;

    /// <summary>How far through its turn a ramped sweep is at <paramref name="u"/> of its span: the rate rises
    /// linearly over the first <see cref="SweepRamp"/>, holds flat through the middle, and falls over the last.
    ///
    /// <para>⚠ Not a smoothstep. A smoothstep's rate peaks at 1.5× its mean in the middle — which is over the
    /// top, exactly where the strip must not out-run the stick — and reaches zero at each end, so two of them
    /// back to back stall where they meet. This shape's peak is 1.25× its mean, it never pauses between its
    /// ends, and its slow moment is at the foot where the runner is still winding up off the bottom.</para></summary>
    private static double SweepAt(double u)
    {
        double r = SweepRamp, v = 1 / (1 - r);
        return u < r ? v * u * u / (2 * r)
             : u > 1 - r ? 1 - v * (1 - u) * (1 - u) / (2 * r)
             : v * (u - r / 2);
    }

    /// <summary>A full turn round a closed tube on one continuous sweep: up a wall, through 12 o'clock and down
    /// the other side, back to the bottom to get off. The strip is <paramref name="half"/> wide at the foot and
    /// <paramref name="halfTo"/> by the end, so a wider entry can be the forgiving part of an otherwise tight
    /// figure. Tokens ride the strip's own centre at fixed fractions of the sweep, so they stay on it whatever
    /// the pace.</summary>
    private static InternodePhrase OverTopTurn(string id, double half, double halfTo, int minStage, bool diabolical)
    {
        const double at = 1.2;
        double end = at + SweepSeconds;
        var events = new List<InternodeEvent> { T(0.9, 0) };
        foreach (double u in new[] { 0.125, 0.325, 0.525, 0.725, 0.925 })
            events.Add(T(at + SweepSeconds * u, Norm(-360 * SweepAt(u))));
        events.Add(T(end + 0.5, 0));
        events.Add(T(end + 1.0, 0));
        return PGX(id, 4, InternodePos.Centre, InternodePos.Centre, end + 1.4,
                   [Curve(at, SweepSeconds, 12, 0, -360, half, halfTo: halfTo, ramped: true)],
                   closed: true, minStage: minStage, diabolical: diabolical, [.. events]);
    }

    /// <summary>A strip that forks: one plank becomes two that part to ±42°, hold, and rejoin. A choice with
    /// its own floor — the branch you did not take is sky by the time you could change your mind.</summary>
    private static readonly InternodePhrase ForkPlank =
        PGX("fork-plank", 4, InternodePos.Centre, InternodePos.Centre, 7.8,
            [Strip(1.2, 0.9, 0, 24), Braid(2.1, 1.8, 6, 24, 0, -42, 0, 42), Braid(3.9, 0.9, 1, 24, -42, -42, 42, 42),
             Braid(4.8, 1.8, 6, 24, -42, 0, 42, 0), Strip(6.6, 0.9, 0, 24)], closed: false, minStage: 45, diabolical: false,
            T(0.9, 0), T(1.7, 0), TB(2.9, -20, 1), TB(2.9, 20, 2), TB(3.8, -40, 1), TB(3.8, 40, 2),
            TB(4.6, -42, 1), TB(4.6, 42, 2), TB(5.5, -22, 1), TB(5.5, 22, 2), T(6.4, 0), T(7.2, 0));

    /// <summary>Two planks that part from the bottom, climb opposite walls, cross at the top of a closed tube
    /// and come down the other side to rejoin: a double helix, each strand a route. Half a turn each, the
    /// crossing at the same pace as every other over-the-top sweep (<see cref="SweepSeconds"/>). ⚠ One rate
    /// from the moment the strands part to the moment they rejoin — the parting, the crossing and the rejoin are
    /// three legs of a single turn, so nothing speeds up or slows down under the runner. Built at two widths:
    /// the 36° strands are the late set's, the 72° strands (<see cref="DoubleHelixWide"/>) the same figure as a
    /// mid-game introduction to the closed tube.</summary>
    private static InternodePhrase DoubleHelixOf(string id, double half, int minStage, bool diabolical)
    {
        // Each strand turns a full 360° at 360/SweepSeconds degrees an authored second: a quarter turn apart, a
        // half turn crossing, a quarter turn back together.
        double part = SweepSeconds / 4, cross = SweepSeconds / 2;
        double a = 1.8, b = a + part, c = b + cross, d = c + part;
        double Theta(double t) => Norm(-(t - a) * 360 / SweepSeconds);
        var events = new List<InternodeEvent> { T(0.9, 0) };
        for (double t = a + 0.25; t < d; t += 0.4)
        {
            events.Add(TB(t, Theta(t), 1));
            events.Add(TB(t, -Theta(t), 2));
        }
        events.Add(T(d + 0.4, 0));
        events.Add(T(d + 1.1, 0));
        return PGX(id, 4, InternodePos.Centre, InternodePos.Centre, d + 1.6,
            [Strip(1.2, 0.6, 0, half), Braid(a, part, 2, half, 0, -90, 0, 90), Braid(b, cross, 4, half, -90, -270, 90, 270),
             Braid(c, part, 2, half, -270, -360, 270, 360), Strip(d, 0.6, 0, half)],
            closed: true, minStage: minStage, diabolical: diabolical, [.. events]);
    }
    private static readonly InternodePhrase DoubleHelix = DoubleHelixOf("double-helix", 18, 80, true);
    private static readonly InternodePhrase DoubleHelixWide = DoubleHelixOf("double-helix-wide", 36, 45, false);

    /// <summary>A bend on a 24° strip: the margin is the challenge, and it moves.</summary>
    private static readonly InternodePhrase NeedleBend =
        PGX("needle-bend", 4, InternodePos.Centre, InternodePos.Centre, 6.2,
            [Curve(1.2, 1.8, 6, 0, -24, 12), Curve(3.0, 1.8, 6, -24, 0, 12)], closed: false, minStage: 65, diabolical: true,
            T(0.9, 0), T(1.9, -9), T(2.8, -22), T(3.7, -16), T(4.5, -3), T(5.4, 0));

    /// <summary>A bending strip with a mine on it at each bend: leap the mine and land back on the strip,
    /// which has moved under you. Each mine sits at its step's own centre, 4.8° inside the hole rule.</summary>
    private static readonly InternodePhrase MinePlank =
        PGX("mine-plank", 4, InternodePos.Centre, InternodePos.Centre, 6.4,
            [Curve(1.2, 1.8, 6, 0, -30, 24), Curve(3.0, 1.8, 6, -30, 0, 24)], closed: false, minStage: 60, diabolical: true,
            T(0.9, 0), T(1.8, -14), M(2.25, -17.5), T(3.5, -22), M(4.05, -12.5), T(5.3, 0));

    /// <summary>Two holes at the whole length of a full-strength jump, an orb on each landing. ⚠ 1.7 s authored
    /// is 0.77 s real at the floor scale, the only scale a stage-50 phrase is ever played at, against a held
    /// jump of 0.88 s; the oracle proves it there. 1.9 s could not be flown from any start.</summary>
    private static readonly InternodePhrase LongLeap =
        PGX("long-leap", 4, InternodePos.Centre, InternodePos.Centre, 7.6,
            [Hole(1.5, 1.6), Hole(4.6, 1.6)], closed: false, minStage: 55, diabolical: true,
            T(0.9, 0), T(3.8, 0), T(4.2, 0), T(6.9, 0), T(7.3, 0));

    /// <summary>A hole whose landing is fenced: a mine either side of the touchdown at ±24°, leaving 17.6° to
    /// land in, twice over.</summary>
    private static readonly InternodePhrase MineLanding =
        PGX("mine-landing", 4, InternodePos.Centre, InternodePos.Centre, 7.2,
            [Hole(1.4, 1.3), Hole(3.9, 1.3)], closed: false, minStage: 60, diabolical: true,
            T(0.9, 0), M(2.8, -24), M(2.8, 24), T(3.3, 0), M(5.3, -24), M(5.3, 24), T(5.8, 0), T(6.5, 0));

    /// <summary>A floor ring with a door under a hung row at 0.40: hopping the ring meets the row, so the door
    /// is walked, and the second door has moved. ⚠ The orb after each stack sits 1.15 s behind it: at the
    /// fastest ride a hung mine projects onto the floor a third of a second behind it, so a nearer orb is
    /// hidden under the very row the door is cut in.</summary>
    private static readonly InternodePhrase MineStack =
        PRX("mine-stack", 4, InternodePos.Centre, InternodePos.Centre, 7.0, 50, true,
            [T(0.9, 0)], Ring(1.8, 0, 40), Hung(1.8, 0.80), [T(2.95, 0)], Ring(3.9, 40, 40), Hung(3.9, 0.80),
            [T(5.05, 40)], [T(5.7, 20)], [T(6.4, 0)]);

    /// <summary>A floor wall under a hung row at 0.60, above the reach of a jump's apex: a walk meets the wall
    /// and a jump at its top meets the row, so the wall is cleared low, in the window between the floor mines'
    /// clear height and the row's band — twice.</summary>
    private static readonly InternodePhrase HopStack =
        PRX("hop-stack", 4, InternodePos.Centre, InternodePos.Centre, 7.0, 55, true,
            [T(0.9, 0)], Wall(2.0), Hung(2.0, 0.60), [T(3.2, 0)], Wall(4.4), Hung(4.4, 0.60), [T(5.6, 0)], [T(6.4, 0)]);

    public static readonly InternodePhrase[] All =
    [
        Straight, Sway, MineSlot, MineShift, Mirror(MineShift, "mine-shift-right"),
        LeanLeft, Mirror(LeanLeft, "lean-right"), ReturnLeft, Mirror(ReturnLeft, "return-right"),
        BreatheCentre, BreatheLowLeft, Mirror(BreatheLowLeft, "breathe-lowright"),
        BreatheHighLeft, Mirror(BreatheHighLeft, "breathe-highright"),
        // Tier 1 token shapes: what level 1 is made of.
        OrbArc, OrbWeave, OrbStairLeft, Mirror(OrbStairLeft, "orb-stair-right"),
        OrbHomeLeft, Mirror(OrbHomeLeft, "orb-home-right"),
        OrbHoldLeft, Mirror(OrbHoldLeft, "orb-hold-right"),
        ClimbLeft, Mirror(ClimbLeft, "climb-right"),
        WallRideLeft, Mirror(WallRideLeft, "wallride-right"), HighHoldLeft, Mirror(HighHoldLeft, "highhold-right"),
        DropInLeft, Mirror(DropInLeft, "dropin-right"), Slalom, MineComb, MineWall,
        ArchLeft, Mirror(ArchLeft, "arch-right"), MineStairLeft, Mirror(MineStairLeft, "mine-stair-right"),
        ArcHop, ArcDropLeft, Mirror(ArcDropLeft, "arc-drop-right"),
        BendPlankLeft, Mirror(BendPlankLeft, "bend-plank-right"),
        BendPlankNarrowLeft, Mirror(BendPlankNarrowLeft, "bend-plank-narrow-right"), SerpentPlank, SkyPlank, ForkPlank, DoubleHelixWide,
        HelixPlankLeft, Mirror(HelixPlankLeft, "helix-plank-right"), DoubleHelix, NeedleBend, MinePlank,
        LongLeap, MineLanding, MineStack, HopStack,
        MineGate, MineDouble, MineEyeLeft, Mirror(MineEyeLeft, "mine-eye-right"),
        WallMinesLeft, Mirror(WallMinesLeft, "wallmines-right"),
        SpiralLeft, Mirror(SpiralLeft, "spiral-right"),
        Zigzag,
        Doorway, HelixLeft, Mirror(HelixLeft, "helix-right"), VortexLeft, Mirror(VortexLeft, "vortex-right"),
        Split, SplitWide, CrossoverX,
        // Track figures: the floor is the challenge.
        GapStep, GapTwoHop, GapShoulderLeft, Mirror(GapShoulderLeft, "gap-shoulder-right"),
        RibbonHold, GapSlalom, RibbonLeanLeft, Mirror(RibbonLeanLeft, "ribbon-lean-right"), GapLadder,
        PlankSweepLeft, Mirror(PlankSweepLeft, "plank-sweep-right"),
        PlankHopLeft, Mirror(PlankHopLeft, "plank-hop-right"), TwinRibbon, IslandHop, Mirror(IslandHop, "island-hop-right"), RibbonBreak,
        MineRibbon,
        // The endurance figure sits in the hardest few percent of the bank by feel: held for the late run.
        WallRunLeft, Mirror(WallRunLeft, "wallrun-right"), ChasmChain with { MinStage = 60 }, NeedleHold,
        // The tightest figure outside the late set (12 worst-plane survivors against a bank at 40+): held back
        // until the wide planks have taught the strip, or it is the early-game spike the sweep exists to find.
        TwinRibbonCross with { MinStage = 30 },
    ];

    /// <summary>The class's tier-1 same-class phrase: the sequencer's fallback when nothing else fits.</summary>
    public static InternodePhrase Breather(InternodePos pos) => pos switch
    {
        InternodePos.LowLeft => BreatheLowLeft,
        InternodePos.LowRight => All.First(p => p.Id == "breathe-lowright"),
        InternodePos.HighLeft => BreatheHighLeft,
        InternodePos.HighRight => All.First(p => p.Id == "breathe-highright"),
        _ => BreatheCentre,
    };

    /// <summary>How far a class is from the bottom centre: the return-to-centre step only takes phrases that lower this.</summary>
    public static int Rank(InternodePos pos) => pos switch
    {
        InternodePos.Centre => 0,
        InternodePos.LowLeft or InternodePos.LowRight => 1,
        _ => 2,
    };

    public static InternodePhrase? Find(string id) => Array.Find(All, p => p.Id == id);
}
