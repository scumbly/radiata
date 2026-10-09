using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>The boss-block encounter's drawing: the block falling in, the ring and dust of its landing, the
/// cracks each bomb opens, and the fragments of its break. Read-only over <see cref="ConnateBossBlock"/>.
///
/// <para>⚠ Gold is reserved for value on its way to the counter. The block's crystals are the owner-approved
/// exception, and only the crystals: the ring, the dust, the cracks and the flash are white, ink and sage.</para></summary>
internal sealed partial class ConnateRenderer
{
    /// <summary>The boss sprite's lump is 512 px of a 704 px canvas, so the box is that much wider than a
    /// garbage lump's for the lump to match an ordinary blob of the same radius.</summary>
    private const double BossArtReach = GarbageArtReach * 704.0 / 512.0;

    /// <summary>The identity the vector lump and the fragments are cut from. Any value; fixed so the block
    /// always breaks the same way.</summary>
    private const long BossLumpId = 0x0B055;

    /// <summary>How far past the field centre the landing ring is drawn before it has faded out, in field radii.</summary>
    private const double BossRingFadeRadius = 1.25;

    private static void DrawBossBlock(DrawingContext dc, Point c, double world, double fieldRadius, Connate game)
    {
        ConnateBossBlock boss = game.BossBlock;
        if (!boss.Active) return;
        double radius = boss.DrawRadius * world;
        Point centre = Screen(c, world, boss.X, boss.Y);
        double shake = Math.Clamp(boss.ImpactLeft / Math.Max(0.05, ConnateTuning.BossLandShakeSeconds), 0, 1);
        if (shake > 0)
            centre = new Point(centre.X + Math.Sin(game.PhaseTime * 168) * fieldRadius * 0.014 * shake,
                               centre.Y + Math.Cos(game.PhaseTime * 138) * fieldRadius * 0.014 * shake);

        if (boss.Phase == ConnateBossPhase.Break)
        {
            DrawBossBreak(dc, c, world, fieldRadius, boss);
            return;
        }

        double opacity = boss.Opacity;
        bool fade = opacity < 0.999;
        if (fade) dc.PushOpacity(opacity);
        DrawPieceShadow(dc, centre, radius);
        DrawBossBody(dc, centre, radius, boss.Rotation, game.PhaseTime);
        if (fade) dc.Pop();

        if (boss.Phase == ConnateBossPhase.Fight && boss.ImpactLeft > 0)
        {
            double flash = Math.Clamp(boss.ImpactLeft / Math.Max(0.05, ConnateTuning.BossHitFlashSeconds), 0, 1);
            dc.DrawEllipse(ConnatePalette.WhiteBrush((byte)(190 * flash)), null, centre, radius * 0.92, radius * 0.92);
        }
        DrawBossCracks(dc, centre, radius, boss);
        if (boss.Phase == ConnateBossPhase.Land) DrawBossLanding(dc, c, world, fieldRadius, boss);
    }

    /// <summary>The block itself: the owner sprite when one is loaded, else the garbage lump with its crystals.
    /// Shared by the board and the how-to card, so the card shows the thing the board draws.</summary>
    private static void DrawBossBody(DrawingContext dc, Point centre, double radius, double rotation, double time)
    {
        if (ArcadeSprites.Get(ArcadeSprites.Slot.ConnateBossBlock) is { } art)
        {
            dc.PushTransform(new RotateTransform(rotation * 180 / Math.PI, centre.X, centre.Y));
            ArcadeSprites.Draw(dc, art, ArcadeSprites.Box(centre, radius * BossArtReach));
            dc.Pop();
        }
        else
        {
            DrawGarbage(dc, centre, radius, BossLumpId, time, rotation);
            DrawBossCrystals(dc, centre, radius, rotation);
        }
    }

    /// <summary>Jagged fractures, three per hit, fixed on the block and turning with it. A white rim under the
    /// ink keeps them readable on the junk's dark metal.</summary>
    private static void DrawBossCracks(DrawingContext dc, Point centre, double radius, ConnateBossBlock boss)
    {
        int count = boss.Cracks * 3;
        if (count <= 0 || radius < 4) return;
        double ink = Math.Max(1.5, radius * 0.022);
        Pen rim = ConnatePalette.WhitePen(255, ink * 2.4, round: true);
        Pen line = ConnatePalette.Cel(ink);
        for (int i = 0; i < count; i++)
        {
            double a = boss.Rotation + i * 2.39996;
            double reach = radius * (0.40 + 0.10 * (i % 3));
            Point p1 = new(centre.X + Math.Cos(a) * reach * 0.15, centre.Y + Math.Sin(a) * reach * 0.15);
            Point p2 = new(centre.X + Math.Cos(a + 0.27) * reach * 0.65, centre.Y + Math.Sin(a + 0.27) * reach * 0.65);
            Point p3 = new(centre.X + Math.Cos(a) * reach, centre.Y + Math.Sin(a) * reach);
            dc.DrawLine(rim, p1, p2);
            dc.DrawLine(rim, p2, p3);
            dc.DrawLine(line, p1, p2);
            dc.DrawLine(line, p2, p3);
        }
    }

    /// <summary>The landing: dust thrown from the block's rim for the first moments, and the ring that runs
    /// out across the field from it — the same ring the sim tests against the loaded piece.</summary>
    private static void DrawBossLanding(DrawingContext dc, Point c, double world, double fieldRadius,
                                        ConnateBossBlock boss)
    {
        double wave = boss.WaveRadius;
        if (wave < BossRingFadeRadius)
        {
            double life = Math.Clamp(1 - wave / BossRingFadeRadius, 0, 1);
            double ringRadius = wave * world;
            double width = Math.Max(2, fieldRadius * 0.016);
            dc.PushOpacity(0.35 + 0.65 * life);
            dc.DrawEllipse(null, ConnatePalette.Cel(width * 1.7), c, ringRadius + width * 0.7, ringRadius + width * 0.7);
            dc.DrawEllipse(null, ConnatePalette.WhitePen(255, width), c, ringRadius, ringRadius);
            dc.Pop();
        }

        const double dustSeconds = 0.45;
        double t = boss.Time;
        if (t >= dustSeconds) return;
        double distance = (boss.Radius * 0.75 + t * 0.8) * world;
        double size = world * 0.027 * (1 + 4 * t) * (1 - t / dustSeconds);
        if (size < 0.5) return;
        for (int i = 0; i < 14; i++)
        {
            double a = i * Math.PI * 2 / 14;
            dc.DrawEllipse(i % 3 == 0 ? ConnatePalette.Garbage : ConnatePalette.Aim, null,
                new Point(c.X + Math.Cos(a) * distance, c.Y + Math.Sin(a) * distance), size, size);
        }
    }

    /// <summary>The broken block: the sprite (or the vector lump) thrown apart through the garbage break, with
    /// gold crystal shards flying out among the pieces. The break's own clock runs the garbage clear's life.</summary>
    private static void DrawBossBreak(DrawingContext dc, Point c, double world, double fieldRadius,
                                      ConnateBossBlock boss)
    {
        double t = Math.Clamp(boss.Time / Math.Max(0.05, ConnateTuning.BossBreakSeconds), 0, 1);
        var clear = new ConnateGarbageClear(boss.X, boss.Y, boss.Radius, BossLumpId,
            t * ConnateTuning.GarbageClearSeconds, boss.Rotation);
        DrawGarbageClear(dc, c, world, clear, ArcadeSprites.Get(ArcadeSprites.Slot.ConnateBossBlock), BossArtReach);

        Point centre = Screen(c, world, boss.X, boss.Y);
        double radius = boss.Radius * world;
        double travel = 1 - Math.Pow(1 - t, 2.4);
        Geometry shard = CrystalShard(radius * 0.30, out Geometry facet);
        for (int i = 0; i < 9; i++)
        {
            double heading = i * 2.39996 + 0.5;
            double reach = (fieldRadius + radius) * (0.95 + 0.16 * (i % 3)) * travel;
            double spin = (i * 53 % 360) + 540 * travel * (i % 2 == 0 ? 1 : -1);
            var m = Matrix.Identity;
            m.Rotate(spin);
            m.Translate(centre.X + Math.Cos(heading) * reach, centre.Y + Math.Sin(heading) * reach);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawGeometry(ConnatePalette.Mote, ConnatePalette.MoteEdge, shard);
            dc.DrawGeometry(ConnatePalette.MoteHot, null, facet);
            dc.Pop();
        }
    }

    // ── Crystals ───────────────────────────────────────────────────────────────

    /// <summary>The vector fallback's crystals: seven large ones jutting from the lump's rim and six small ones
    /// on its face, as fractions of the block's radius — angle, distance of the base from the centre, length,
    /// half-width.</summary>
    private static readonly (double Angle, double Base, double Length, double Width)[] CrystalLayout =
    [
        (0.25, 0.82, 0.55, 0.20), (1.05, 0.82, 0.42, 0.18), (1.90, 0.82, 0.60, 0.21), (2.75, 0.82, 0.38, 0.17),
        (3.60, 0.82, 0.50, 0.20), (4.45, 0.82, 0.62, 0.22), (5.30, 0.82, 0.40, 0.18),
        (0.65, 0.45, 0.22, 0.11), (1.50, 0.50, 0.20, 0.10), (2.30, 0.42, 0.24, 0.12),
        (3.20, 0.48, 0.20, 0.10), (4.10, 0.44, 0.22, 0.11), (4.90, 0.50, 0.21, 0.10),
    ];

    private readonly record struct CrystalSet(Geometry Body, Geometry Facet);

    private static readonly Dictionary<int, CrystalSet> CrystalSets = [];
    private static readonly Dictionary<int, CrystalSet> CrystalShards = [];

    /// <summary>Quantises a size to 2.5% steps, so a block shrinking through its drop reuses a few dozen
    /// cached sets instead of building one per pixel.</summary>
    private static int SizeKey(double size) => (int)Math.Round(Math.Log(Math.Max(1, size)) * 40);
    private static double SizeFromKey(int key) => Math.Exp(key / 40.0);

    private static void DrawBossCrystals(DrawingContext dc, Point centre, double radius, double rotation)
    {
        if (radius < 3) return;
        int key = SizeKey(radius);
        if (!CrystalSets.TryGetValue(key, out CrystalSet set))
        {
            if (CrystalSets.Count >= 96) CrystalSets.Clear();
            set = CrystalSets[key] = BuildCrystalCluster(SizeFromKey(key));
        }
        var m = Matrix.Identity;
        m.Rotate(rotation * 180 / Math.PI);
        m.Translate(centre.X, centre.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawGeometry(ConnatePalette.Mote, ConnatePalette.MoteEdge, set.Body);
        dc.DrawGeometry(ConnatePalette.MoteHot, null, set.Facet);
        dc.Pop();
    }

    private static CrystalSet BuildCrystalCluster(double radius)
    {
        var bodies = new List<PathFigure>(CrystalLayout.Length);
        var facets = new List<PathFigure>(CrystalLayout.Length);
        foreach ((double angle, double baseAt, double length, double width) in CrystalLayout)
            AddKite(bodies, facets, angle, radius * baseAt, radius * length, radius * width);
        var body = new PathGeometry(bodies); body.Freeze();
        var facet = new PathGeometry(facets); facet.Freeze();
        return new CrystalSet(body, facet);
    }

    /// <summary>A single crystal shard of the given length, pointing along +X, for the break.</summary>
    private static Geometry CrystalShard(double length, out Geometry facet)
    {
        int key = SizeKey(length);
        if (!CrystalShards.TryGetValue(key, out CrystalSet set))
        {
            if (CrystalShards.Count >= 96) CrystalShards.Clear();
            double size = SizeFromKey(key);
            var bodies = new List<PathFigure>(1);
            var facets = new List<PathFigure>(1);
            AddKite(bodies, facets, 0, -size * 0.5, size, size * 0.34);
            var body = new PathGeometry(bodies); body.Freeze();
            var lit = new PathGeometry(facets); lit.Freeze();
            set = CrystalShards[key] = new CrystalSet(body, lit);
        }
        facet = set.Facet;
        return set.Body;
    }

    /// <summary>One faceted crystal: a five-point body standing on a base at <paramref name="baseAt"/> from the
    /// origin along <paramref name="angle"/>, and a lit triangle on its leading side.</summary>
    private static void AddKite(List<PathFigure> bodies, List<PathFigure> facets, double angle, double baseAt,
                                double length, double width)
    {
        double ux = Math.Cos(angle), uy = Math.Sin(angle), nx = -uy, ny = ux;
        Point At(double along, double across) =>
            new((baseAt + along) * ux + across * nx, (baseAt + along) * uy + across * ny);
        Point[] body = [At(0, width * 0.85), At(length * 0.55, width), At(length, 0), At(length * 0.55, -width),
                        At(0, -width * 0.85)];
        bodies.Add(Figure(body));
        facets.Add(Figure([At(length * 0.55, width), At(length, 0), At(length * 0.3, width * 0.1)]));
    }

    private static PathFigure Figure(Point[] points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        for (int i = 1; i < points.Length; i++) figure.Segments.Add(new LineSegment(points[i], true));
        return figure;
    }
}
