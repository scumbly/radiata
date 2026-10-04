namespace ControllerWheel;

/// <summary>One side of the polygon and the inward-bowing arc that replaces it. Corner A → corner B runs
/// clockwise on screen; <see cref="Chord"/> points A → B and is the paddle's positive rail direction.</summary>
public sealed class PetalPopSide
{
    public int Index { get; init; }
    public Vec2 CornerA { get; init; }
    public Vec2 CornerB { get; init; }
    public Vec2 Mid { get; init; }
    /// <summary>Unit A → B.</summary>
    public Vec2 Chord { get; init; }
    /// <summary>Unit normal of the side pointing away from the disc centre.</summary>
    public Vec2 Outward { get; init; }
    /// <summary>Unit normal pointing at the disc centre.</summary>
    public Vec2 Inward => -Outward;
    /// <summary>+1 when the arc bows inward (its centre lies outside the rim and the field is outside the
    /// circle), −1 when it bows outward (its centre lies toward the disc centre and the field is inside the
    /// circle). Every formula below is written once, in terms of this sign and <see cref="Bulge"/>.</summary>
    public int Bow { get; init; } = 1;
    /// <summary>The unit direction from the arc's centre to the arc's midpoint: <see cref="Inward"/> for an
    /// inward bow, <see cref="Outward"/> for an outward one.</summary>
    public Vec2 Bulge => Bow >= 0 ? Inward : Outward;
    /// <summary>The arc's centre: outside the rim for an inward bow, toward the disc centre for an outward one.</summary>
    public Vec2 ArcCentre { get; init; }
    public double ArcRadius { get; init; }
    /// <summary>Half the arc's angular span, so its parameter runs −HalfAngle (corner A) .. +HalfAngle (corner B).</summary>
    public double HalfAngle { get; init; }

    /// <summary>Point on the arc at parameter <paramref name="alpha"/>.</summary>
    public Vec2 PointAt(double alpha) =>
        ArcCentre + (Bulge * Math.Cos(alpha) + Chord * Math.Sin(alpha)) * ArcRadius;
    /// <summary>Unit tangent in the direction of increasing parameter (toward corner B).</summary>
    public Vec2 TangentAt(double alpha) => Bulge * -Math.Sin(alpha) + Chord * Math.Cos(alpha);
    /// <summary>Unit normal pointing into the playfield: away from the arc's centre for an inward bow, toward
    /// it for an outward one.</summary>
    public Vec2 NormalAt(double alpha) => (Bulge * Math.Cos(alpha) + Chord * Math.Sin(alpha)) * Bow;
    /// <summary>Signed depth of a point relative to the arc: positive inside the playfield, negative in the
    /// gutter beyond this side.</summary>
    public double Depth(Vec2 p) => ((p - ArcCentre).Length - ArcRadius) * Bow;
    /// <summary>The arc parameter of the ray from the arc centre through <paramref name="p"/>.</summary>
    public double ParameterOf(Vec2 p)
    {
        var d = p - ArcCentre;
        return Math.Atan2(d.Dot(Chord), d.Dot(Bulge));
    }
}

/// <summary>The playfield for one side count: the N sides with their arcs, the paddle's angular footprint and
/// travel limit, and the polygon helpers every ring of petals shares. Built once per (N, geometry knobs).
/// The petals themselves are per level — see <see cref="PetalPopFlower"/>.
///
/// <para>Orientation: a flat side always sits at 6 o'clock, so there is a bottom paddle to serve from at
/// every N — a square stands upright, a pentagon carries a corner at 12 o'clock, a hexagon has flat top and
/// bottom. <see cref="ServeSide"/> names that side.</para></summary>
public sealed class PetalPopLayout
{
    public const int MinSides = 3, MaxSides = 12;

    public int Sides { get; }
    /// <summary>Screen angle of corner 0; corner k is at this + 2πk/N.</summary>
    public double Corner0Angle { get; }
    public PetalPopSide[] Side { get; }
    /// <summary>The side whose midpoint is at 6 o'clock.</summary>
    public int ServeSide { get; }
    public double CornerRadius { get; }
    public double ArcRadius { get; }
    public double HalfAngle { get; }
    /// <summary>Half the paddle's angular footprint on its arc.</summary>
    public double PaddleHalfAngle { get; }
    /// <summary>Paddle travel limit along the rail in arc length, ±. At the limit the paddle's end sits on the
    /// corner exactly, so two neighbours pushed to a shared corner seal it.</summary>
    public double PosMax { get; }
    /// <summary>Distance from the centre to a rail's midpoint — the closest a rail comes.</summary>
    public double RailMidRadius { get; }
    /// <summary>The bow's magnitude actually used, after the floor and cap; <see cref="Bow"/> carries its sign.</summary>
    public double Sagitta { get; }
    /// <summary>+1 = the rails bow inward, −1 = outward. See <see cref="PetalPopSide.Bow"/>.</summary>
    public int Bow { get; }
    /// <summary>Outward unit normal of side j — shared by every ring, since all are parallel.</summary>
    public Vec2[] SideNormal { get; }

    /// <summary>The paddle half-length this layout was built for.</summary>
    public double PaddleHalfLength { get; }

    private static readonly Dictionary<(int, double), PetalPopLayout> Cache = [];
    private static (double, double, double, double) _cacheKey;

    /// <summary>The layout for <paramref name="sides"/> and a paddle of <paramref name="paddleHalfLength"/>
    /// (negative = the run's first-level length) under the current tuning; cached per (N, half-length) and
    /// dropped wholesale when a geometry knob changes. The half-length is part of the layout because the
    /// paddle's angular footprint and its travel limit follow it.</summary>
    public static PetalPopLayout For(int sides, double paddleHalfLength = -1)
    {
        var key = (PetalPopTuning.CornerRadius, PetalPopTuning.ArcSagitta,
                   PetalPopTuning.PaddleHalfLengthFirst, PetalPopTuning.PaddleHalfLengthLast);
        if (key != _cacheKey) { Cache.Clear(); _cacheKey = key; }
        sides = Math.Clamp(sides, MinSides, MaxSides);
        if (paddleHalfLength <= 0) paddleHalfLength = PetalPopTuning.PaddleHalfLengthFirst;
        paddleHalfLength = Math.Round(Math.Clamp(paddleHalfLength, 0.02, 0.5), 6);
        if (!Cache.TryGetValue((sides, paddleHalfLength), out var l))
            Cache[(sides, paddleHalfLength)] = l = new PetalPopLayout(sides, paddleHalfLength);
        return l;
    }

    private PetalPopLayout(int sides, double paddleHalfLength)
    {
        PaddleHalfLength = paddleHalfLength;
        Sides = sides;
        double step = 2 * Math.PI / sides;
        // Put a side midpoint at 6 o'clock: mid_k = corner0 + π/N + k·2π/N = π for some k.
        Corner0Angle = ((Math.PI - Math.PI / sides) % step + step) % step;
        if (Corner0Angle > step - 1e-9) Corner0Angle = 0;
        ServeSide = (int)Math.Round((Math.PI - Corner0Angle - Math.PI / sides) / step) % sides;

        CornerRadius = Math.Clamp(PetalPopTuning.CornerRadius, 0.5, 1.0);
        // The sign says which way the rail bows (negative = outward, toward the rim); the magnitude is floored,
        // never zero: a straight rail is an arc of enormous radius here, and at 0.0005 the deviation from the
        // chord is a fiftieth of a ball radius while every arc quantity stays well-conditioned. ⚠ An outward
        // bow may not reach the rim: the magnitude is capped so the rail's midpoint keeps a gutter behind it.
        int bow = PetalPopTuning.ArcSagitta < 0 ? -1 : 1;
        double apothem = CornerRadius * Math.Cos(Math.PI / sides);
        double maxOut = Math.Max(0.0005, 1.0 - apothem - 0.06);
        double sagitta = Math.Clamp(Math.Abs(PetalPopTuning.ArcSagitta), 0.0005, bow < 0 ? maxOut : 0.3);
        double h = CornerRadius * Math.Sin(Math.PI / sides);            // chord half-length
        ArcRadius = (h * h + sagitta * sagitta) / (2 * sagitta);
        HalfAngle = Math.Atan2(h, ArcRadius - sagitta);
        PaddleHalfAngle = Math.Asin(Math.Clamp(paddleHalfLength / ArcRadius, 0, 0.9));
        PosMax = Math.Max(0, ArcRadius * (HalfAngle - PaddleHalfAngle));
        RailMidRadius = apothem - bow * sagitta;
        Sagitta = sagitta;
        Bow = bow;

        Side = new PetalPopSide[sides];
        SideNormal = new Vec2[sides];
        for (int i = 0; i < sides; i++)
        {
            var a = Corner(CornerRadius, i);
            var b = Corner(CornerRadius, (i + 1) % sides);
            var mid = Vec2.Lerp(a, b, 0.5);
            var outward = mid.Normalized();
            var chord = (b - a).Normalized();
            Side[i] = new PetalPopSide
            {
                Index = i, CornerA = a, CornerB = b, Mid = mid, Chord = chord, Outward = outward, Bow = bow,
                ArcCentre = mid + outward * (bow * (ArcRadius - sagitta)), ArcRadius = ArcRadius, HalfAngle = HalfAngle,
            };
            SideNormal[i] = Vec2.FromScreenAngle(CornerAngle(i) + Math.PI / sides);
        }
    }

    /// <summary>Screen angle of corner <paramref name="k"/>.</summary>
    public double CornerAngle(int k) => Corner0Angle + 2 * Math.PI * k / Sides;
    /// <summary>Screen angle of side <paramref name="j"/>'s midpoint.</summary>
    public double SideAngle(int j) => CornerAngle(j) + Math.PI / Sides;

    /// <summary>Corner <paramref name="k"/> of the concentric N-gon with corner radius <paramref name="radius"/>.</summary>
    public Vec2 Corner(double radius, int k) => Vec2.FromScreenAngle(CornerAngle(k)) * radius;

    /// <summary>Corner <paramref name="k"/> of the concentric N-gon whose apothem is <paramref name="apothem"/>.</summary>
    public Vec2 ApothemCorner(double apothem, int k) => Corner(apothem / Math.Cos(Math.PI / Sides), k);

    /// <summary>Polygon "radius" of a point: the largest projection onto a side normal. A point is inside the
    /// N-gon of apothem <c>a</c> iff this is ≤ <c>a</c>.</summary>
    public double Apothem(Vec2 p)
    {
        double best = double.NegativeInfinity;
        for (int j = 0; j < Sides; j++) best = Math.Max(best, p.Dot(SideNormal[j]));
        return best;
    }

    /// <summary>Which side a point is nearest to, and the runner-up (for the corner seams).</summary>
    public (int Best, int Second) SidesOf(Vec2 p)
    {
        int best = 0, second = 1;
        double bd = double.NegativeInfinity, sd = double.NegativeInfinity;
        for (int j = 0; j < Sides; j++)
        {
            double d = p.Dot(SideNormal[j]);
            if (d > bd) { second = best; sd = bd; best = j; bd = d; }
            else if (d > sd) { second = j; sd = d; }
        }
        return (best, second);
    }

    public static Vec2 Centroid(Vec2[] poly)
    {
        double x = 0, y = 0;
        foreach (var p in poly) { x += p.X; y += p.Y; }
        return new(x / poly.Length, y / poly.Length);
    }

    /// <summary>The paddle's two end-cap centres on side <paramref name="side"/> at rail position
    /// <paramref name="pos"/> (arc length from the side's midpoint, positive toward corner B). The tilt swings
    /// them off the rail — see <see cref="PaddleSpine"/>, which these are the ends of.</summary>
    public (Vec2 A, Vec2 B) PaddleEnds(int side, double pos) =>
        (PaddleSpine(side, pos, -1), PaddleSpine(side, pos, 1));

    /// <summary>Segments the paddle spine is sampled into, for the collider and the drawing alike. Two is
    /// exact for the straight spine it is now; a curved paddle would need many more.</summary>
    public const int PaddleSegments = 6;

    /// <summary>How far the paddle's middle is pulled off its straight spine for a bow of −1..1: positive bows
    /// away from the field (the drawn slingshot), negative bows toward it (the renderer's slam surge — the
    /// collider only ever passes the draw, so a negative bow never moves what the ball hits). A bow of 0..1
    /// (the slingshot draw) pulls the spine's middle back from the field: a third of a paddle thickness at a
    /// full draw (a flex, not an arc). The ends stay put.</summary>
    public static double PaddleBend(double bow) => PetalPopTuning.PaddleHalfThickness * (2.0 / 3) * Math.Clamp(bow, -1, 1);

    /// <summary>How far the paddle at <paramref name="pos"/> is turned off its rail, in radians, signed in the
    /// (inward normal → tangent) frame.
    ///
    /// <para>The paddle is straight but leans: it turns
    /// <see cref="PetalPopTuning.PaddleTiltFraction"/> of the way from facing straight out of its rail to
    /// facing the middle of the board. A paddle at its side's midpoint already faces the middle, so the angle
    /// there is zero and the lean grows the further it slides toward a corner. That is what breaks the corner
    /// trap: both paddles meeting at a corner turn their faces the same way, so the wedge two straight paddles
    /// would otherwise form reads as a backboard aimed inward.</para>
    ///
    /// <para>⚠ Deliberately a fraction, and capped by <see cref="PetalPopTuning.PaddleTiltMaxDeg"/>. A paddle
    /// pointing straight at the centre would send every ball back down its own approach line.</para></summary>
    public double PaddleTilt(int side, double pos)
    {
        var s = Side[side];
        double ac = pos / ArcRadius;
        var centre = s.PointAt(ac);
        double reach = centre.Length;
        if (reach < 1e-9) return 0;
        var toMiddle = centre * (-1.0 / reach);
        double full = Math.Atan2(toMiddle.Dot(s.TangentAt(ac)), toMiddle.Dot(s.NormalAt(ac)));
        double max = Math.Clamp(PetalPopTuning.PaddleTiltMaxDeg, 0, 60) * Math.PI / 180;
        return Math.Clamp(full * Math.Clamp(PetalPopTuning.PaddleTiltFraction, 0, 1), -max, max);
    }

    /// <summary>A point on the paddle's straight spine at <paramref name="offset"/> ∈ −1..1 (corner-A end to
    /// corner-B end). The spine's centre rides the rail and the whole segment is turned by
    /// <see cref="PaddleTilt"/>, so one end leads into the field and the other trails toward the gutter.
    /// Offsets past ±1 give the round end caps' centres. <paramref name="lunge"/> lifts the whole paddle off
    /// its rail toward the field — the smash surge, real for collision and for drawing alike.</summary>
    public Vec2 PaddleSpine(int side, double pos, double offset, double lunge = 0, double bow = 0)
    {
        var s = Side[side];
        double ac = pos / ArcRadius;
        double tilt = PaddleTilt(side, pos);
        var railNormal = s.NormalAt(ac);
        var railTangent = s.TangentAt(ac);
        double cos = Math.Cos(tilt), sin = Math.Sin(tilt);
        var normal = railNormal * cos + railTangent * sin;
        var along = railTangent * cos - railNormal * sin;
        offset = Math.Clamp(offset, -1, 1);
        // The bend is parabolic in the offset: zero at both ends, PaddleBend at the middle, pulled away from
        // the field. Collider and renderer both sample this, so the bent paddle hits where it is drawn.
        double bend = PaddleBend(bow) * (1 - offset * offset);
        // The draw is bounded by this paddle's own room in the gutter lens; a forward surge is not. ⚠ The bend is
        // not bounded: clamping it per sample flattened the middle of a cornered paddle against the rim while
        // its ends kept bending, which read as a broken paddle. A slab overlapping the rim a little is fine.
        if (lunge < 0) lunge = -Math.Min(-lunge, MaxDrawDepth(side, pos));
        double shift = lunge - bend;
        return s.PointAt(ac) + along * (offset * PaddleHalfLength) + normal * shift;
    }

    /// <summary>The spine's field-facing unit normal — the one direction a paddle is ever allowed to send a
    /// ball. Constant along the paddle, since the paddle is straight.
    ///
    /// <para>⚠ Load-bearing: the contact normal alone is "away from the spine", which points at the gutter for
    /// a ball the paddle is standing on top of (a released slingshot sweeping through one). Mirroring about
    /// that flung the ball out of bounds through its own paddle. Every paddle response clamps its exit
    /// against this axis instead, so "a smash never sends the ball away from the middle" is a property of the
    /// geometry rather than of the case analysis.</para></summary>
    public Vec2 PaddleFaceNormal(int side, double pos)
    {
        var s = Side[side];
        double ac = pos / ArcRadius;
        double tilt = PaddleTilt(side, pos);
        return s.NormalAt(ac) * Math.Cos(tilt) + s.TangentAt(ac) * Math.Sin(tilt);
    }

    /// <summary>The paddle's own long axis, perpendicular to <see cref="PaddleFaceNormal"/> and pointing
    /// toward corner B. The face and this are the frame every paddle response is expressed in.</summary>
    public Vec2 PaddleAlong(int side, double pos)
    {
        var s = Side[side];
        double ac = pos / ArcRadius;
        double tilt = PaddleTilt(side, pos);
        return s.TangentAt(ac) * Math.Cos(tilt) - s.NormalAt(ac) * Math.Sin(tilt);
    }

    /// <summary>How far either end of the tilted paddle stands off its rail — the collider's search slack.</summary>
    public double PaddleSwing(int side, double pos) => PaddleHalfLength * Math.Abs(Math.Sin(PaddleTilt(side, pos)));

    /// <summary>How far the paddle at <paramref name="pos"/> may be drawn back before its outer edge would
    /// reach the rim.
    ///
    /// <para>⚠ Per paddle, not per shape: the gutter is a lens, deepest at a side's midpoint and pinched to
    /// nothing where the corners meet the bezel (they sit at <see cref="CornerRadius"/>, a hair inside the
    /// rim). So a paddle at the middle of its rail takes the whole windup while one jammed into a corner has
    /// nowhere to go — winding up is something the middle of a rail can do and a corner cannot. Without this
    /// a single <c>DrawDepth</c> either wasted the roomy shapes or pushed corner paddles through the bezel,
    /// where the playfield clip cuts them in half.</para></summary>
    public double MaxDrawDepth(int side, double pos)
    {
        double limit = PetalPopTuning.RimRadius - PetalPopTuning.PaddleHalfThickness
                     - Math.Max(0, PetalPopTuning.DrawRimMargin);
        if (limit <= 0) return 0;
        // Solve |p + back·t| = limit for the furthest-out sample: t² + 2t(p·back) + |p|² − limit² = 0.
        var back = PaddleFaceNormal(side, pos) * -1;
        double worst = double.PositiveInfinity;
        for (int i = 0; i <= PaddleSegments; i++)
        {
            var p = PaddleSpine(side, pos, -1 + 2.0 * i / PaddleSegments);
            double pn = p.Dot(back);
            double disc = pn * pn - p.Dot(p) + limit * limit;
            worst = Math.Min(worst, disc <= 0 ? 0 : -pn + Math.Sqrt(disc));
        }
        return Math.Max(0, worst);
    }

    /// <summary>Deepest gutter intrusion of a point across all sides: negative means it is past a rail.</summary>
    public (double Depth, int Side) MinDepth(Vec2 p)
    {
        double best = double.PositiveInfinity; int side = 0;
        for (int i = 0; i < Sides; i++)
        {
            double d = Side[i].Depth(p);
            if (d < best) { best = d; side = i; }
        }
        return (best, side);
    }
}

/// <summary>One petal's drawn silhouette: an inner polyline (two points for a petal on one side, three for
/// a petal that wraps a corner), then a straight run out to <see cref="OuterStart"/>, one quadratic Bezier
/// through <see cref="Ctrl"/> to <see cref="OuterEnd"/>, and a straight run back to the first inner point.</summary>
public sealed record PetalPopOutline(Vec2[] Inner, Vec2 OuterStart, Vec2 Ctrl, Vec2 OuterEnd)
{
    public Vec2 Centroid
    {
        get
        {
            double x = OuterStart.X + OuterEnd.X, y = OuterStart.Y + OuterEnd.Y;
            foreach (var p in Inner) { x += p.X; y += p.Y; }
            int n = Inner.Length + 2;
            return new(x / n, y / n);
        }
    }
    /// <summary>Point on the bowed edge at <paramref name="t"/> (0 = OuterStart, 1 = OuterEnd).</summary>
    public Vec2 Curve(double t)
    {
        double u = 1 - t;
        return OuterStart * (u * u) + Ctrl * (2 * u * t) + OuterEnd * (t * t);
    }
}

/// <summary>The flower for one level of one stage: rings of petals around the fixed core, how each ring is
/// cut, how many hits each takes, and the petal shapes themselves. A stage is a side count N (4 → 8) and has
/// N levels; the level within the stage adds rings (as many as the rails leave room for, capped by
/// <see cref="PetalPopTuning.MaxRings"/>) and hardens the inner rings.
///
/// <para>Alternate rings are staggered by half a petal, and a staggered ring's petal at each corner wraps the
/// corner as one bent petal with a rounded tip rather than two halves meeting at a seam. Its collider is two
/// convex halves that share one hit count.</para>
///
/// <para>The petal outline — inset, with the outer edge bowed outward — lives here, and the collider samples
/// that same curve, so the ball bounces off exactly the petal it sees.</para></summary>
public sealed class PetalPopFlower
{
    /// <summary>Petals are shrunk toward their centroid by this factor so neighbours read as separate petals.</summary>
    public const double Inset = 0.93;
    /// <summary>How far the bowed outer edge's apex stands off its chord, as a fraction of the chord.</summary>
    public const double Bulge = 0.14;
    /// <summary>Segments the collider samples a bowed edge into.</summary>
    public const int OuterSamples = 6;

    public PetalPopLayout Layout { get; }
    public int Sides => Layout.Sides;
    public int Level { get; }
    public double CoreApothem { get; }
    public double RingPitch { get; }
    public int Rings { get; }
    /// <summary>Petals per side in each ring. In a staggered ring the last one on each side wraps the corner.</summary>
    public int[] CountPerSide { get; }
    public bool[] Staggered { get; }
    public int[] RingOffset { get; }
    /// <summary>Hits a fresh petal of each ring takes, 0 = the ring touching the core.</summary>
    public int[] MaxHp { get; }
    /// <summary>The number of petal slots — a patterned ring leaves some of its own empty, so this is not the
    /// petal count. <see cref="Present"/> answers which slots carry a petal.</summary>
    public int BrickCount { get; }
    /// <summary>Rotational symmetry order of each ring's coverage: the side count for a full ring, else the
    /// number of evenly spaced arcs of petals it is cut into.</summary>
    public int[] Symmetry { get; }
    /// <summary>How far a bowed edge or corner tip can reach beyond its ring's outer apothem — the broad
    /// phase's slack.</summary>
    public double BulgeMargin { get; }

    private readonly Vec2[]?[] _polys;
    private readonly PetalPopOutline?[] _outlines;
    private readonly Vec2[][]?[] _colliders;
    // Per ring: the repeating sector's slot count, how many of them carry a petal, and where the run starts.
    private readonly int[] _sectorSlots;
    private readonly int[] _keep;
    private readonly int[] _patternOffset;
    private readonly bool[] _multiball;

    private static readonly Dictionary<(int, int), PetalPopFlower> Cache = [];
    private static (double, double, double, double, double, double, int, int, int, int, int, int,
                    double, int, int, double, double, double, int, int) _cacheKey;

    /// <summary>The flower for level <paramref name="level"/> of the <paramref name="sides"/>-sided stage
    /// under the current tuning; cached and dropped wholesale when a knob changes.</summary>
    public static PetalPopFlower For(int sides, int level)
    {
        var key = (PetalPopTuning.CoreApothem, PetalPopTuning.RingPitch, PetalPopTuning.BrickTargetWidth,
                   PetalPopTuning.ToughPerLevel, PetalPopTuning.ToughPerStage, PetalPopTuning.RailClearance,
                   PetalPopTuning.MaxHp, PetalPopTuning.MaxRings, PetalPopTuning.SidesStart,
                   PetalPopTuning.PatternFromLevel, PetalPopTuning.PatternChancePerLevel,
                   PetalPopTuning.PatternChanceMax, PetalPopTuning.MultiballFraction,
                   PetalPopTuning.MultiballMin, PetalPopTuning.MultiballMax,
                   PetalPopTuning.FlowerApothem, PetalPopTuning.RingPitchBoost, PetalPopTuning.FlowerApothemFirst,
                   PetalPopTuning.MultiballRerollPercent, PetalPopTuning.MultiballRerollMax);
        if (key != _cacheKey) { Cache.Clear(); _cacheKey = key; }
        var L = PetalPopLayout.For(sides);
        level = Math.Clamp(level, 1, 99);
        if (!Cache.TryGetValue((L.Sides, level), out var f)) Cache[(L.Sides, level)] = f = new PetalPopFlower(L, level);
        return f;
    }

    private PetalPopFlower(PetalPopLayout L, int level)
    {
        Layout = L;
        Level = level;
        // The flower's outer edge grows gently with the ring count, from FlowerApothemFirst to FlowerApothem (as far
        // as the rails allow), and the level decides how that footprint is shared between petals and core. Rings:
        // one per level within the stage, as many as fit between the smallest core and the full outer edge.
        double basePitch = Math.Clamp(PetalPopTuning.RingPitch, 0.02, 0.2);
        double coreMin = Math.Clamp(PetalPopTuning.CoreApothem, 0.04, 0.5);
        double outerMax = L.RailMidRadius - Math.Max(0, PetalPopTuning.RailClearance);
        double full = Math.Clamp(PetalPopTuning.FlowerApothem, coreMin + basePitch, Math.Max(coreMin + basePitch, outerMax));
        int maxRings = Math.Max(1, PetalPopTuning.MaxRings);
        int roomFor = Math.Max(1, (int)Math.Floor((full - coreMin) / basePitch + 1e-9));
        Rings = Math.Clamp(level, 1, Math.Min(maxRings, roomFor));
        double first = Math.Clamp(PetalPopTuning.FlowerApothemFirst, coreMin + basePitch, full);
        double flower = maxRings > 1 ? first + (full - first) * (Rings - 1) / (double)(maxRings - 1) : full;
        // Fewer rings → thicker petals, but never so thick that the core drops below its minimum.
        double pitch = basePitch * (1 + Math.Max(0, PetalPopTuning.RingPitchBoost) * (maxRings - Rings));
        RingPitch = Math.Min(pitch, (flower - coreMin) / Rings);
        CoreApothem = flower - Rings * RingPitch;

        CountPerSide = new int[Rings];
        Staggered = new bool[Rings];
        RingOffset = new int[Rings];
        MaxHp = new int[Rings];
        Symmetry = new int[Rings];
        _sectorSlots = new int[Rings];
        _keep = new int[Rings];
        _patternOffset = new int[Rings];
        double toughness = (level - 1) * Math.Max(0, PetalPopTuning.ToughPerLevel)
                         + Math.Max(0, L.Sides - PetalPopTuning.SidesStart) * Math.Max(0, PetalPopTuning.ToughPerStage);
        int maxHp = Math.Clamp(PetalPopTuning.MaxHp, 1, 5);
        double targetWidth = Math.Clamp(PetalPopTuning.BrickTargetWidth, 0.03, 0.3);
        double bulge = 0;
        int total = 0;
        for (int k = 0; k < Rings; k++)
        {
            double mid = InnerApothem(k) + RingPitch / 2;
            double sideLength = 2 * mid * Math.Tan(Math.PI / L.Sides);
            int n = Math.Max(2, (int)Math.Round(sideLength / targetWidth));
            Staggered[k] = k % 2 == 1;
            CountPerSide[k] = n;
            MaxHp[k] = 1 + Math.Clamp((int)Math.Floor(toughness - k * Math.Max(0, PetalPopTuning.ToughRingStep)), 0, maxHp - 1);
            RingOffset[k] = total;
            total += L.Sides * n;
            ChoosePattern(L.Sides, level, k, n);
            // The widest chord any bowed edge in this ring can have: a wrapped corner petal's tip spans two
            // half-edges around the corner.
            double outerEdge = 2 * OuterApothem(k) * Math.Tan(Math.PI / L.Sides) / n * Inset;
            bulge = Math.Max(bulge, Bulge * outerEdge * 1.6);
        }
        BrickCount = total;
        BulgeMargin = bulge;
        _polys = new Vec2[]?[total];
        _outlines = new PetalPopOutline?[total];
        _colliders = new Vec2[][]?[total];
        _multiball = SeedMultiball(L.Sides, level, total);
    }

    /// <summary>Which petals are multiball petals: popping one splits the ball that did it.
    ///
    /// <para>Spread evenly through the level's present petals rather than rolled per pop, so they can be seen
    /// and played toward, and so a patterned ring's gaps can never be marked. Deterministic in (sides, level)
    /// like the coverage patterns, which keeps a level's identity stable across attempts and lets the
    /// snapshot rebuild the board without storing it.</para></summary>
    private bool[] SeedMultiball(int sides, int level, int total)
    {
        var marked = new bool[total];
        var present = new List<int>();
        for (int i = 0; i < total; i++) if (Present(i)) present.Add(i);
        if (present.Count == 0) return marked;

        int want = Math.Clamp((int)Math.Round(present.Count * Math.Max(0, PetalPopTuning.MultiballFraction)),
                              Math.Max(0, PetalPopTuning.MultiballMin),
                              Math.Max(0, PetalPopTuning.MultiballMax));
        want = Math.Min(want, present.Count);
        if (want <= 0) return marked;

        // Roll, and if two marks touch, re-roll with MultiballRerollPercent chance — each roll and each
        // re-roll decision from the level's own hash under a different salt, so the whole chain is a pure
        // function of (sides, level) and the snapshot can still rebuild the board from those two numbers.
        int rerollMax = Math.Max(0, PetalPopTuning.MultiballRerollMax);
        int rerollPct = Math.Clamp(PetalPopTuning.MultiballRerollPercent, 0, 100);
        for (int attempt = 0; ; attempt++)
        {
            Array.Clear(marked);
            ulong h = Mix(sides, level, 0x5EED + attempt);
            int start = (int)(h % (ulong)present.Count);
            for (int i = 0; i < want; i++)
                marked[present[(start + i * present.Count / want) % present.Count]] = true;
            if (attempt >= rerollMax || !AnyMarkedTouching(marked)) return marked;
            if ((int)(Mix(sides, level, 0x7E11 + attempt) % 100) >= rerollPct) return marked;
        }
    }

    /// <summary>True when any two marked petals share an edge: neighbours in the same ring (the wrap included),
    /// or petals in adjacent rings whose spans along the perimeter overlap.</summary>
    private bool AnyMarkedTouching(bool[] marked)
    {
        var idx = new List<int>();
        for (int i = 0; i < marked.Length; i++) if (marked[i]) idx.Add(i);
        for (int a = 0; a < idx.Count; a++)
            for (int b = a + 1; b < idx.Count; b++)
                if (Touching(idx[a], idx[b])) return true;
        return false;
    }

    private bool Touching(int i, int j)
    {
        var (ri, _, _) = Decode(i);
        var (rj, _, _) = Decode(j);
        int dr = Math.Abs(ri - rj);
        if (dr > 1) return false;
        var (s0, e0) = PerimeterSpan(i);
        var (s1, e1) = PerimeterSpan(j);
        const double eps = 1e-9;
        // Compare on the circle: the second span shifted by a whole turn either way.
        for (int k = -1; k <= 1; k++)
        {
            double a = s1 + k, b = e1 + k;
            bool overlap = a < e0 - eps && b > s0 + eps;
            bool abut = Math.Abs(a - e0) < eps || Math.Abs(b - s0) < eps;
            if (dr == 0 ? overlap || abut : overlap) return true;
        }
        return false;
    }

    /// <summary>Where a petal sits along the flower's perimeter, as a fraction of one full turn: the same
    /// parametrisation <see cref="Polygon"/> uses, so a wrapped corner petal runs past its side's end.</summary>
    private (double Start, double End) PerimeterSpan(int index)
    {
        var (ring, side, m) = Decode(index);
        int n = CountPerSide[ring], N = Layout.Sides;
        double t0, t1;
        if (Staggered[ring]) { t0 = (m + 0.5) / n; t1 = (m + 1.5) / n; }
        else { t0 = (double)m / n; t1 = (m + 1.0) / n; }
        return ((side + t0) / N, (side + t1) / N);
    }

    /// <summary>Popping this petal splits the ball that did it. Always a petal the level actually has.</summary>
    public bool Multiball(int index) => _multiball[index];

    /// <summary>Fractions of a sector a pattern may keep. Each leaves a real gap, so the ring reads as arcs.</summary>
    private static readonly double[] KeepFractions = [0.5, 0.6, 0.7, 0.8];

    /// <summary>Decide ring <paramref name="ring"/>'s coverage: the whole circumference, or
    /// <see cref="Symmetry"/> evenly spaced arcs of petals with gaps between them.
    ///
    /// <para>⚠ Ring 0 is never patterned. The core is exposed by breaking a petal that touches it, so a gap
    /// there would let the opening shot fly straight through and end the level.</para>
    ///
    /// <para>⚠ Derived from a hash of (sides, level, ring), never from the game's RNG. The flower is cached
    /// and the snapshot trusts it to rebuild identically, and a level should keep its own identity across
    /// attempts rather than being reshuffled each time it is reached.</para></summary>
    private void ChoosePattern(int sides, int level, int ring, int perSide)
    {
        Symmetry[ring] = sides;
        _sectorSlots[ring] = perSide;
        _keep[ring] = perSide;
        _patternOffset[ring] = 0;
        if (ring == 0 || level < PetalPopTuning.PatternFromLevel) return;

        ulong h = Mix(sides, level, ring);
        int chance = Math.Min(PetalPopTuning.PatternChanceMax,
                              Math.Max(0, PetalPopTuning.PatternChancePerLevel)
                              * (level - PetalPopTuning.PatternFromLevel + 1));
        if ((int)(h % 100) >= chance) return;

        // Symmetry orders are the divisors of the side count from 2 up: a pattern has to close on itself all
        // the way round, so only those repeat cleanly.
        var orders = new List<int>();
        for (int d = 2; d <= sides; d++) if (sides % d == 0) orders.Add(d);
        if (orders.Count == 0) return;

        int order = orders[(int)((h >> 7) % (ulong)orders.Count)];
        int sector = sides / order * perSide;
        int keep = Math.Clamp((int)Math.Round(sector * KeepFractions[(int)((h >> 17) % (ulong)KeepFractions.Length)]),
                              1, Math.Max(1, sector - 1));
        Symmetry[ring] = order;
        _sectorSlots[ring] = sector;
        _keep[ring] = keep;
        _patternOffset[ring] = (int)((h >> 27) % (ulong)sector);
    }

    private static ulong Mix(int a, int b, int c)
    {
        ulong h = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)(a * 73856093) ^ (ulong)(uint)(b * 19349663) ^ (ulong)(uint)(c * 83492791);
        h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33;
        return h;
    }

    /// <summary>Whether slot <paramref name="index"/> carries a petal at all. A patterned ring leaves gaps, so
    /// the flower reads as petals rather than as concentric walls, and a ball can slip through to a deeper
    /// ring. Absent slots are simply filled with no hit points, so nothing draws or collides with them.</summary>
    public bool Present(int index)
    {
        var (ring, side, m) = Decode(index);
        int sector = _sectorSlots[ring];
        if (_keep[ring] >= sector) return true;
        int slot = (side * CountPerSide[ring] + m) % sector;
        return ((slot - _patternOffset[ring]) % sector + sector) % sector < _keep[ring];
    }

    public double InnerApothem(int ring) => CoreApothem + ring * RingPitch;
    public double OuterApothem(int ring) => CoreApothem + (ring + 1) * RingPitch;
    /// <summary>The outermost petal edge (before the bulge) — nothing of the flower lies beyond it plus <see cref="BulgeMargin"/>.</summary>
    public double FlowerApothem => OuterApothem(Rings - 1);

    /// <summary>Flat petal index from (ring, side, index along the side).</summary>
    public int BrickIndex(int ring, int side, int m) => RingOffset[ring] + side * CountPerSide[ring] + m;

    /// <summary>Inverse of <see cref="BrickIndex"/>.</summary>
    public (int Ring, int Side, int M) Decode(int index)
    {
        int ring = Rings - 1;
        while (ring > 0 && RingOffset[ring] > index) ring--;
        int local = index - RingOffset[ring];
        return (ring, local / CountPerSide[ring], local % CountPerSide[ring]);
    }

    /// <summary>True for the petal that wraps corner (side + 1) in a staggered ring.</summary>
    public bool WrapsCorner(int index)
    {
        var (ring, _, m) = Decode(index);
        return Staggered[ring] && m == CountPerSide[ring] - 1;
    }

    /// <summary>The raw petal polygon, inner edge first running along the side(s), then the outer edge back.
    /// Four points for a petal on one side; six for a wrapped corner petal (inner corner and outer corner
    /// included). Cached; treat as read-only.</summary>
    public Vec2[] Polygon(int index)
    {
        if (_polys[index] is { } p) return p;
        var L = Layout;
        var (ring, side, m) = Decode(index);
        int n = CountPerSide[ring];
        double ai = InnerApothem(ring), ao = OuterApothem(ring);
        int next = (side + 1) % L.Sides;
        var innerA = L.ApothemCorner(ai, side); var innerB = L.ApothemCorner(ai, next);
        var outerA = L.ApothemCorner(ao, side); var outerB = L.ApothemCorner(ao, next);

        if (Staggered[ring] && m == n - 1)
        {
            // Wraps corner (side + 1): the tail half-petal of this side and the head half-petal of the next.
            int after = (side + 2) % L.Sides;
            var innerC = L.ApothemCorner(ai, after); var outerC = L.ApothemCorner(ao, after);
            double tTail = (n - 0.5) / n, tHead = 0.5 / n;
            return _polys[index] =
            [
                Vec2.Lerp(innerA, innerB, tTail), innerB, Vec2.Lerp(innerB, innerC, tHead),
                Vec2.Lerp(outerB, outerC, tHead), outerB, Vec2.Lerp(outerA, outerB, tTail),
            ];
        }

        double t0, t1;
        if (Staggered[ring]) { t0 = (m + 0.5) / n; t1 = (m + 1.5) / n; }
        else { t0 = (double)m / n; t1 = (m + 1.0) / n; }
        return _polys[index] =
        [
            Vec2.Lerp(innerA, innerB, t0), Vec2.Lerp(innerA, innerB, t1),
            Vec2.Lerp(outerA, outerB, t1), Vec2.Lerp(outerA, outerB, t0),
        ];
    }

    public Vec2 Centroid(int index) => PetalPopLayout.Centroid(Polygon(index));

    /// <summary>The drawn petal, inset toward its centroid, its outer edge bowed. Cached; treat as read-only.</summary>
    public PetalPopOutline Outline(int index)
    {
        if (_outlines[index] is { } o) return o;
        var raw = Polygon(index);
        var c = PetalPopLayout.Centroid(raw);
        Vec2 P(int i) => c + (raw[i] - c) * Inset;
        var (petalRing, _, _) = Decode(index);
        Vec2[] inner; Vec2 outerStart, outerEnd, apex;
        if (raw.Length == 6)
        {
            inner = [P(0), P(1), P(2)];
            outerStart = P(3); outerEnd = P(5);
            // The tip rounds the outer corner: the curve's apex sits a little beyond where the corner was.
            var corner = P(4);
            apex = corner + (corner - c).Normalized() * ((outerStart - outerEnd).Length * Bulge * 0.35);
        }
        else
        {
            inner = [P(0), P(1)];
            outerStart = P(2); outerEnd = P(3);
            var mid = Vec2.Lerp(outerStart, outerEnd, 0.5);
            apex = mid + (mid - c).Normalized() * ((outerStart - outerEnd).Length * Bulge);
        }
        // The innermost ring closes onto a round core, so its inner edge is the core's own arc rather than the
        // polygon chord it was cut from — that chord left the polygon's corners standing empty once the core
        // stopped being a polygon.
        if (petalRing == 0) inner = CoreArc(inner[0], inner[^1]);
        // A quadratic Bezier passes through (S + 2·Ctrl + E) / 4 at its middle; solve for the control point
        // that puts the middle on the apex.
        var ctrl = apex * 2 - (outerStart + outerEnd) * 0.5;
        return _outlines[index] = new PetalPopOutline(inner, outerStart, ctrl, outerEnd);
    }

    /// <summary>Convex collider parts for the petal: one sampled polygon for a straight petal, two halves split
    /// at the tip for a wrapped corner petal (the inner corner makes the whole shape reflex). Cached; treat as
    /// read-only.</summary>
    public Vec2[][] Collider(int index)
    {
        if (_colliders[index] is { } k) return k;
        var o = Outline(index);

        // ⚠ A ring-0 petal has an arc inner edge, which makes it an annular sector — not convex, and the
        // polygon test builds "inside" out of per-edge half-planes. Fanned into one convex quad per arc
        // segment instead, each running from the arc out to the matching stretch of the outer curve.
        var (petalRing, _, _) = Decode(index);
        if (petalRing == 0)
        {
            int m = o.Inner.Length - 1;
            var fan = new Vec2[m][];
            for (int i = 0; i < m; i++)
            {
                // ⚠ The outer curve is sampled backwards. The inner run and the outer run go opposite ways
                // round the petal — OuterStart neighbours the last inner point, OuterEnd the first — so
                // pairing them forwards would cross the quad over itself.
                var b0 = o.Curve(1 - (double)i / m);
                var b1 = o.Curve(1 - (double)(i + 1) / m);
                fan[i] = [o.Inner[i], o.Inner[i + 1], b1, b0];
            }
            return _colliders[index] = fan;
        }

        if (o.Inner.Length == 2)
        {
            var poly = new Vec2[3 + OuterSamples];
            poly[0] = o.Inner[0]; poly[1] = o.Inner[1]; poly[2] = o.OuterStart;
            for (int s = 1; s < OuterSamples; s++) poly[2 + s] = o.Curve((double)s / OuterSamples);
            poly[2 + OuterSamples] = o.OuterEnd;
            return _colliders[index] = [poly];
        }
        // Wrapped: [inner tail-point, inner corner, apex, curve back to outer tail] and its mirror.
        int half = OuterSamples / 2;
        var tip = o.Curve(0.5);
        var a = new Vec2[3 + half];
        a[0] = o.Inner[0]; a[1] = o.Inner[1]; a[2] = tip;
        for (int s = 1; s <= half; s++) a[2 + s] = o.Curve(0.5 + 0.5 * s / half);   // toward OuterEnd (the tail side)
        var b = new Vec2[3 + half];
        b[0] = o.Inner[1]; b[1] = o.Inner[2]; b[2] = o.OuterStart;
        for (int s = 1; s <= half; s++) b[2 + s] = o.Curve(0.5 * s / half);           // from OuterStart up to the tip
        return _colliders[index] = [a, b];
    }

    /// <summary>Samples per petal along the core arc. Also the number of convex parts a ring-0 petal's
    /// collider is fanned into, so it buys smoothness and costs collision work in step.</summary>
    public const int CoreArcSamples = 6;

    /// <summary>The core circle's radius: the apothem the rings are measured from, so the core is the disc
    /// inscribed in the flower's full polygon outline, never reaching past where that sat. Ring 0 closes onto
    /// it with <see cref="CoreArc"/>.</summary>
    public double CoreRadius => CoreApothem;

    /// <summary>An arc ON the core circle spanning the same angles as the chord it replaces. Sampled rather
    /// than curved because both the drawn petal and its collider consume points, and one description of the
    /// edge is the only way they can agree.</summary>
    private Vec2[] CoreArc(Vec2 from, Vec2 to)
    {
        double a0 = Math.Atan2(from.Y, from.X), a1 = Math.Atan2(to.Y, to.X);
        // The short way round: a corner-wrapping petal spans the seam, where the raw difference is a whole
        // turn out and would sweep the arc the wrong way round the flower.
        double d = a1 - a0;
        while (d > Math.PI) d -= Math.Tau;
        while (d < -Math.PI) d += Math.Tau;
        var pts = new Vec2[CoreArcSamples + 1];
        for (int i = 0; i <= CoreArcSamples; i++)
        {
            double a = a0 + d * i / CoreArcSamples;
            pts[i] = new Vec2(Math.Cos(a) * CoreRadius, Math.Sin(a) * CoreRadius);
        }
        return pts;
    }
}
