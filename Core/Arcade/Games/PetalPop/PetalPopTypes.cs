namespace ControllerWheel;

/// <summary>One of the paddles. <see cref="Pos"/>/<see cref="Vel"/> are the whole of its state: a paddle does
/// not squash on a ball strike — the ball does — so there is nothing transient to carry.</summary>
public sealed class PetalPopPaddle
{
    /// <summary>Arc length from the side's midpoint, positive toward corner B, clamped to ±<see cref="PetalPopLayout.PosMax"/>.</summary>
    public double Pos;
    /// <summary>Rail speed, playfield units per second.</summary>
    public double Vel;
}

public enum PetalPopBallState { Live = 0, Gutter = 1 }

public sealed class PetalPopBall
{
    public const int TrailLength = 14;

    public double X, Y, VX, VY;
    public PetalPopBallState State;
    /// <summary>The side whose rail this ball fell past. −1 while live.</summary>
    public int GutterSide = -1;
    /// <summary>+PUNCH: extra hits loaded on this ball, 0 .. <see cref="PetalPopTuning.PunchMax"/>. A slam adds
    /// one or two by its draw; every petal contact spends one (and any more the petal can absorb as damage);
    /// paddle contact spends none. The ball rebounds off a petal only when this reaches zero, so a charged
    /// ball plows through the cluster.</summary>
    public int Punch;

    // Presentation, transient.
    public double Squash;
    public double HitNX, HitNY;
    public double PrevX, PrevY;
    public readonly double[] TrailX = new double[TrailLength];
    public readonly double[] TrailY = new double[TrailLength];
    public int TrailHead, TrailCount;
    public double TrailTimer;

    public Vec2 Position => new(X, Y);
    public Vec2 Velocity => new(VX, VY);
    public double Speed => Math.Sqrt(VX * VX + VY * VY);

    public void SampleTrail()
    {
        TrailX[TrailHead] = X; TrailY[TrailHead] = Y;
        TrailHead = (TrailHead + 1) % TrailLength;
        if (TrailCount < TrailLength) TrailCount++;
    }

    /// <summary>Trail sample <paramref name="i"/> back from the newest (0 = newest).</summary>
    public Vec2 TrailAt(int i)
    {
        int idx = (TrailHead - 1 - i + TrailLength * 2) % TrailLength;
        return new(TrailX[idx], TrailY[idx]);
    }

    public void ClearTrail() { TrailHead = 0; TrailCount = 0; TrailTimer = 0; }
}

/// <summary>A fragment of a popped petal. Transient presentation.</summary>
public readonly record struct PetalPopShard(double X, double Y, double VX, double VY, double Rot, double Spin,
                                   double Size, int Ring, double Age);

/// <summary>One piece of the broken core, in flight. <see cref="Shape"/> indexes
/// <see cref="PetalPopCoreShatter.Pieces"/>; position is the piece's own centroid in playfield units, and the
/// outline is drawn about it at the core's radius. Transient presentation.</summary>
public readonly record struct PetalPopCoreShard(int Shape, double X, double Y, double VX, double VY, double Rot, double Spin, double Age);

/// <summary>The core breaks into seven pieces, and no two are the same shape. A hand-laid crack pattern, not
/// spokes from a hub: the cracks run between five interior junctions and eight rim points, so a piece can
/// be a rim sliver, a chunk that never touches the rim's far side, or a slab spanning most of the disc, and
/// the crack lines fork and bend instead of all meeting at the middle. Unit-disc coordinates, centred on the
/// core. ⚠ Every interior junction and rim point is shared by exactly the faces that meet there, so the
/// seven outlines tile the disc with no gap or overlap — move a vertex here and every piece using it moves
/// with it. Nothing is random; a shatter draws the same seven pieces every time.</summary>
public static class PetalPopCoreShatter
{
    // Rim points at screen angles (0 = 12 o'clock, clockwise): the arcs between them are 40°–50°, each
    // sampled at its midpoint so the assembled rim still reads as round.
    private static Vec2 R(double deg) => Vec2.FromScreenAngle(deg * Math.PI / 180);
    private static readonly Vec2 A = R(0), B = R(45), C = R(95), D = R(140), E = R(190), F = R(235), G = R(275), H = R(320);
    private static readonly Vec2 AB = R(22.5), BC = R(70), CD = R(117.5), DE = R(165), EF = R(212.5), FG = R(255), GH = R(297.5), HA = R(340);
    // Interior junctions. J5 is the only one near the middle, and only four of the seven pieces reach it.
    private static readonly Vec2 J1 = new(0.05, -0.30), J2 = new(0.35, 0.15), J3 = new(-0.05, 0.40),
                                        J4 = new(-0.35, 0.05), J5 = new(0.02, 0.02);

    public static readonly Vec2[][] Pieces =
    [
        [A, AB, B, J2, J5, J1],            // upper-right slab, reaching the middle
        [B, BC, C, J2],                    // right rim sliver
        [C, CD, D, J3, J5, J2],            // lower-right chunk
        [D, DE, E, J3],                    // bottom rim sliver
        [E, EF, F, J4, J5, J3],            // lower-left chunk, with a kink at J4
        [F, FG, G, GH, H, J1, J5, J4],     // the big left slab
        [H, HA, A, J1],                    // top rim sliver
    ];
    /// <summary>Each piece's centroid: where it is drawn about, and where the kick is measured from.</summary>
    public static readonly Vec2[] Centroids = [.. Pieces.Select(PetalPopLayout.Centroid)];
}

public enum PetalPopBurstKind { Split = 0, Pop = 1, Core = 2, Smash = 3, Chip = 4 }

/// <summary>A one-shot ring/flash at a point. Transient presentation.</summary>
public readonly record struct PetalPopBurst(double X, double Y, PetalPopBurstKind Kind, double Age);
