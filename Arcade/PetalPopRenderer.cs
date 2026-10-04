using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Stateless, code-only renderer for Petalpop. Every animation it draws is read off a clock the
/// sim owns (twist, punch, squash, shards, trails, the level flip), so drawing can never change an outcome
/// and a frozen game paints the same frame forever.
///
/// <para>Draw order is load-bearing: gutters and rail glow, the polygon ground, rails, petals, core,
/// shards, gems, trails, balls, paddles, bursts, HUD, shouts, then the modal cards.</para></summary>
internal sealed class PetalPopRenderer : IArcadeRenderer
{
    public static readonly PetalPopRenderer Instance = new();
    public Brush Accent => PetalPopPalette.Accent;

    private const int ArcSamples = 22;

    public void Draw(DrawingContext dc, Point c, double field, IArcadeGame game, double ppd)
    {
        if (game is not PetalPop g) return;
        var L = g.Layout;
        double S = field;   // one playfield unit in pixels

        // A change of shape turns the whole board once about its centre. It is a transform over the drawn
        // playfield only — the HUD, the shout and the modal cards stay upright, and the sim's geometry never
        // moves, so nothing the ball touches is affected.
        double spin = g.StageSpinDegrees;
        if (spin != 0) dc.PushTransform(new RotateTransform(spin, c.X, c.Y));
        DrawGround(dc, c, S, L, g);
        DrawBumpers(dc, c, S, g);
        // The flower turns the other way. Nested inside the board's turn, so the counter-rotation is twice
        // the board's angle and the net is -spin: the petals and core come round once against the board's once.
        if (spin != 0) dc.PushTransform(new RotateTransform(-2 * spin, c.X, c.Y));
        DrawPetals(dc, c, S, g);
        DrawCore(dc, c, S, g);
        if (spin != 0) dc.Pop();
        DrawShards(dc, c, S, g);
        DrawServeAim(dc, c, S, g);
        DrawBalls(dc, c, S, g);
        DrawPaddles(dc, c, S, L, g);
        DrawBursts(dc, c, S, g);
        if (spin != 0) dc.Pop();
        DrawHud(dc, c, S, L, g, ppd);
        DrawShoutIfAny(dc, c, S, g, ppd);

        if (g.Phase == PetalPop.Stage.Intro) DrawIntro(dc, c, S, ppd);
        else if (g.Phase == PetalPop.Stage.GameOver) DrawGameOver(dc, c, S, g, ppd);
        else if (g.Phase == PetalPop.Stage.Won) DrawWin(dc, c, S, g, ppd);
    }

    // ── Ground ────────────────────────────────────────────────────────────────

    private static void DrawGround(DrawingContext dc, Point c, double S, PetalPopLayout L, PetalPop g)
    {
        dc.DrawEllipse(PetalPopPalette.Gutter, null, c, S, S);

        // The rail glow is stroked wide under the ground fill, so only its gutter half survives — the rails
        // breathe outward into the dark, never inward over the board.
        var polygon = ArcPolygon(c, S, L);
        double breathe = 0.75 + 0.25 * Math.Sin(g.PresentationTime * 1.7);
        dc.DrawGeometry(null, PetalPopPalette.Stroke(PetalPopPalette.RailGlow, S * 0.05, 0.22 * breathe), polygon);
        dc.DrawGeometry(null, PetalPopPalette.Stroke(PetalPopPalette.RailGlow, S * 0.018, 0.35 * breathe), polygon);

        // A gutter that just swallowed the last ball flushes red and fades with the life.
        if (g.LivesFlash > 0 && g.LastLostSide >= 0 && g.LastLostSide < L.Sides)
            dc.DrawGeometry(PetalPopPalette.Solid(PetalPopPalette.GutterLost, 0.38 * g.LivesFlash), null,
                            GutterLens(c, S, L, g.LastLostSide));

        dc.DrawGeometry(PetalPopPalette.Ground, null, polygon);
        // A soft vignette inside the polygon so the flower sits in a bowl rather than on a card.
        var bowl = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 0.45),
                new GradientStop(Color.FromArgb(0x55, 0, 0, 0), 1.0),
            },
        };
        bowl.Freeze();
        dc.PushClip(polygon);
        dc.DrawEllipse(bowl, null, c, S * L.CornerRadius, S * L.CornerRadius);
        dc.Pop();
        dc.DrawGeometry(null, PetalPopPalette.RailPen, polygon);
    }

    /// <summary>The N inward arcs as one closed figure, sampled from the sim's own arc functions so the drawn
    /// rail is exactly the rail the ball bounces off.</summary>
    private static Geometry ArcPolygon(Point c, double S, PetalPopLayout L)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(W(c, S, L.Side[0].CornerA), true, true);
            for (int i = 0; i < L.Sides; i++)
            {
                var s = L.Side[i];
                for (int k = 1; k <= ArcSamples; k++)
                    ctx.LineTo(W(c, S, s.PointAt(-s.HalfAngle + 2 * s.HalfAngle * k / ArcSamples)), true, true);
            }
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>The lens between one rail and the rim.</summary>
    private static Geometry GutterLens(Point c, double S, PetalPopLayout L, int side)
    {
        var s = L.Side[side];
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(W(c, S, s.CornerA), true, true);
            for (int k = 1; k <= ArcSamples; k++)
                ctx.LineTo(W(c, S, s.PointAt(-s.HalfAngle + 2 * s.HalfAngle * k / ArcSamples)), true, true);
            double a0 = L.CornerAngle(side + 1), a1 = L.CornerAngle(side);
            for (int k = 0; k <= ArcSamples; k++)
            {
                double a = a0 + (a1 - a0) * k / ArcSamples;
                ctx.LineTo(new Point(c.X + Math.Sin(a) * S * 1.02, c.Y - Math.Cos(a) * S * 1.02), true, true);
            }
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>The corner brackets, one bar per rail in the paddles' wood so they read as fixed paddles: the
    /// same thing the ball bounces off, just one that never moves.</summary>
    private static void DrawBumpers(DrawingContext dc, Point c, double S, PetalPop g)
    {
        // The sim lists two convex bars per corner (its collider wants convex pieces); the picture is one L
        // per corner, traced as a single outline: a sharp outer corner, each tip rounded on its field-facing
        // corner with the paddles' own quarter circle, and a fillet of the same radius in the crook so the
        // inside of the bend is not a square join.
        var bars = g.Bumpers;
        for (int i = 0; i + 1 < bars.Count; i += 2)
        {
            var l = Bracket(bars[i], bars[i + 1]);
            dc.DrawGeometry(PetalPopPalette.PaddleFill, null, l);
            DrawGlass(dc, l);
            dc.DrawGeometry(null, PetalPopPalette.CelPen, l);
        }

        // A bar quad is [gutter-start, gutter-end, field-end, field-start]; both bars start at the corner.
        Geometry Bracket(Vec2[] q1, Vec2[] q2)
        {
            const int arc = 6;
            var corner = Vec2.Lerp(q1[0], q1[3], 0.5);
            double h = (q1[3] - q1[0]).Length / 2;
            var n1 = (q1[3] - q1[0]).Normalized(); var a1 = (q1[1] - q1[0]).Normalized(); var end1 = Vec2.Lerp(q1[1], q1[2], 0.5);
            var n2 = (q2[3] - q2[0]).Normalized(); var a2 = (q2[1] - q2[0]).Normalized(); var end2 = Vec2.Lerp(q2[1], q2[2], 0.5);
            // The bars meet at whatever angle the two rails make at the corner — a right angle on straight
            // rails, obtuse when the rails bow outward — so the outer corner and the crook fillet's centre are
            // found as line intersections rather than assumed square: the point at signed offset k·h from
            // both rails along their normals.
            Vec2 Offset(double k)
            {
                double c1 = (corner + n1 * (k * h)).Dot(n1), c2 = (corner + n2 * (k * h)).Dot(n2);
                double det = n1.X * n2.Y - n1.Y * n2.X;
                if (Math.Abs(det) < 1e-9) return corner + (n1 + n2) * (k * h);
                return new Vec2((c1 * n2.Y - c2 * n1.Y) / det, (n1.X * c2 - n2.X * c1) / det);
            }
            // The crook fillet: radius h, tangent to both field edges, centred one radius in from each.
            var fc = Offset(2);
            // The fillet sweeps from the foot on edge 1 (−n1) to the foot on edge 2 (−n2), the short way round.
            double f0 = Math.Atan2(-n1.Y, -n1.X), f1 = Math.Atan2(-n2.Y, -n2.X);
            double fSweep = f1 - f0;
            while (fSweep > Math.PI) fSweep -= Math.Tau;
            while (fSweep < -Math.PI) fSweep += Math.Tau;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                // Outer corner, sharp — the two gutter edges meet in a point.
                ctx.BeginFigure(W(c, S, Offset(-1)), true, true);
                // Out along bar 1's gutter edge, square up to spine level, then a quarter circle of radius h
                // onto its field edge. The arc is centred one radius short of the tip, so the drawn bar ends
                // exactly where the collider's does.
                var t1 = end1 - a1 * h;
                ctx.LineTo(W(c, S, end1 - n1 * h), true, true);
                ctx.LineTo(W(c, S, end1), true, true);
                for (int k = 1; k <= arc; k++)
                {
                    double th = Math.PI / 2 * k / arc;
                    ctx.LineTo(W(c, S, t1 + a1 * (h * Math.Cos(th)) + n1 * (h * Math.Sin(th))), true, true);
                }
                // Back along bar 1's field edge into the crook, round the fillet, out along bar 2's field edge.
                for (int k = 0; k <= arc; k++)
                {
                    double th = f0 + fSweep * k / arc;
                    ctx.LineTo(W(c, S, fc + new Vec2(Math.Cos(th), Math.Sin(th)) * h), true, true);
                }
                // Bar 2's tip: quarter circle off its field edge down to spine level, square down to the gutter edge.
                var t2 = end2 - a2 * h;
                for (int k = arc; k >= 0; k--)
                {
                    double th = Math.PI / 2 * k / arc;
                    ctx.LineTo(W(c, S, t2 + a2 * (h * Math.Cos(th)) + n2 * (h * Math.Sin(th))), true, true);
                }
                ctx.LineTo(W(c, S, end2 - n2 * h), true, true);
                // The close runs bar 2's gutter edge home to the sharp corner.
            }
            geo.Freeze();
            return geo;
        }
    }

    // ── The flower ────────────────────────────────────────────────────────────

    private static void DrawPetals(DrawingContext dc, Point c, double S, PetalPop g)
    {
        var F = g.Flower;
        double flip = g.LevelClearProgress;
        var hp = g.Hp;
        // ⚠ Outermost ring first, so the inner ones land on top of it. Index order runs inward-out, and drawn
        // that way each ring overlapped the one it grows from — the flower read as opening outward from
        // underneath. Draw order only; nothing the ball touches moves.
        for (int i = Math.Min(F.BrickCount, hp.Count) - 1; i >= 0; i--)
        {
            if (hp[i] <= 0) continue;
            int ring = F.Decode(i).Ring;
            double scale = 1;
            if (g.Phase == PetalPop.Stage.LevelClear)
            {
                // The new flower opens from the core outward, each ring a beat behind the last.
                double t = (flip - (0.30 + 0.11 * ring)) / 0.30;
                if (t <= 0) continue;
                scale = EaseOutBack(Math.Min(1, t));
            }
            DrawPetal(dc, c, S, F, i, ring, hp[i], scale, g.PresentationTime);
        }
    }

    /// <summary>One petal, drawn from the same outline the sim collides with — the bowed edge the ball
    /// bounces off is the one on screen.</summary>
    private static void DrawPetal(DrawingContext dc, Point c, double S, PetalPopFlower F, int index, int ring,
                                  int hp, double scale, double time = 0)
    {
        var o = F.Outline(index);
        var centroid = o.Centroid;
        Vec2 Sc(Vec2 v) => centroid + (v - centroid) * scale;
        var shape = PetalGeometry(c, S, o, centroid, scale);
        // A multiball petal wears the same mint as the collectible gem, so the colour still reads as
        // "this splits your ball" — and it breathes, so it is findable at couch distance. The colour is the
        // whole cue; ⚠ don't add a glyph on top of it.
        bool multiball = F.Multiball(index);
        double breathe = 0.5 + 0.5 * Math.Sin(time * 3.4 + index * 0.7);
        Color fill = multiball
            ? PetalPopPalette.Lerp(PetalPopPalette.Gem, PetalPopPalette.BallHot, 0.15 + 0.25 * breathe)
            : hp >= 2 ? PetalPopPalette.ToughColour(ring, hp) : PetalPopPalette.PetalColour(ring);
        if (multiball)
        {
            var halo = W(c, S, centroid);
            double hr = (o.OuterStart - o.OuterEnd).Length * S * (0.85 + 0.15 * breathe);
            dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Gem, 0.35 + 0.3 * breathe), null, halo, hr, hr);
        }
        dc.DrawGeometry(PetalPopPalette.Solid(fill), null, shape);
        DrawGlass(dc, shape, PetalGloss);

        // The middle of the inner run, however many points it has — a ring-0 petal follows the core arc and
        // carries a whole sampled span here.
        int im = o.Inner.Length / 2;
        var innerMid = o.Inner.Length % 2 == 1 ? o.Inner[im] : Vec2.Lerp(o.Inner[im - 1], o.Inner[im], 0.5);
        var outerMid = o.Curve(0.5);
        var across = (o.OuterStart - o.OuterEnd) * 0.18;
        if (hp >= 2)
        {
            // One vein per extra hit, fanned across the petal: the same hue, thicker in the hand.
            var veinPen = PetalPopPalette.Stroke(PetalPopPalette.PetalVein, Math.Max(1, S * 0.006), 0.42);
            int veins = hp - 1;
            for (int v = 0; v < veins; v++)
            {
                double lane = veins == 1 ? 0 : (v - (veins - 1) / 2.0);
                var a = W(c, S, Sc(Vec2.Lerp(innerMid, outerMid, 0.15) + across * lane));
                var b = W(c, S, Sc(Vec2.Lerp(innerMid, outerMid, 0.85) + across * lane));
                dc.DrawLine(veinPen, a, b);
            }
        }
        if (hp < F.MaxHp[ring])
        {
            // Chipped: cracks from the middle toward the corners, seeded off the index so they hold still.
            var mid = W(c, S, centroid);
            var pen = PetalPopPalette.Stroke(PetalPopPalette.Crack, Math.Max(1, S * 0.005), 0.75);
            Vec2[] corners = [o.Inner[0], o.Inner[^1], o.OuterStart, o.OuterEnd];
            int cracks = 1 + (F.MaxHp[ring] - hp);
            for (int k = 0; k < cracks; k++)
            {
                var corner = W(c, S, Sc(corners[(index * 7 + k * 2) % 4]));
                var elbow = Lerp(mid, corner, 0.45);
                var side = new Point(elbow.X + (corner.Y - mid.Y) * 0.18, elbow.Y - (corner.X - mid.X) * 0.18);
                dc.DrawLine(pen, mid, side);
                dc.DrawLine(pen, side, Lerp(mid, corner, 0.85));
            }
        }
        dc.DrawGeometry(null, PetalPopPalette.FineCel, shape);
    }

    /// <summary>The petal silhouette: the inner polyline, out to the bowed edge, one quadratic back.</summary>
    // ⚠ Takes the centroid and scale rather than a projection delegate: converting the local function to a
    // Func here allocated a closure and a delegate per petal per frame, and cost an indirect call per vertex.
    /// <summary>The petal outlines are fixed for a level, and Core already caches them; the screen geometry
    /// is a pure function of (outline, centre, size) while the flower is at rest, so it is cached on that key
    /// and rebuilt only while the level-clear bloom animates <paramref name="scale"/>. Ninety-six
    /// <c>StreamGeometry</c> builds a frame on a late board went to zero at rest.</summary>
    private static readonly Dictionary<(PetalPopOutline, int, int, int), Geometry> PetalGeometries = new();

    private static Geometry PetalGeometry(Point c, double S, PetalPopOutline o, Vec2 centroid, double scale)
    {
        if (Math.Abs(scale - 1) > 1e-6) return BuildPetalGeometry(c, S, o, centroid, scale);
        var key = (o, (int)Math.Round(c.X * 4), (int)Math.Round(c.Y * 4), (int)Math.Round(S * 4));
        if (PetalGeometries.TryGetValue(key, out var hit)) return hit;
        var built = BuildPetalGeometry(c, S, o, centroid, 1);
        if (PetalGeometries.Count >= 1024) PetalGeometries.Clear();   // a new level's outlines are new keys
        PetalGeometries[key] = built;
        return built;
    }

    private static Geometry BuildPetalGeometry(Point c, double S, PetalPopOutline o, Vec2 centroid, double scale)
    {
        Vec2 sc(Vec2 v) => centroid + (v - centroid) * scale;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(W(c, S, sc(o.Inner[0])), true, true);
            for (int i = 1; i < o.Inner.Length; i++) ctx.LineTo(W(c, S, sc(o.Inner[i])), true, true);
            ctx.LineTo(W(c, S, sc(o.OuterStart)), true, true);
            ctx.QuadraticBezierTo(W(c, S, sc(o.Ctrl)), W(c, S, sc(o.OuterEnd)), true, true);
        }
        geo.Freeze();
        return geo;
    }


    private static void DrawCore(DrawingContext dc, Point c, double S, PetalPop g)
    {
        var F = g.Flower;
        double scale = 1, alpha = 1;
        if (g.Phase == PetalPop.Stage.LevelClear)
        {
            // The struck core is gone at once — its seven pieces (DrawCoreShards) are what the player sees fly
            // — and the next one grows back from nothing in the last stretch of the interlude.
            double p = g.LevelClearProgress;
            if (p < 0.72) return;
            scale = EaseOutBack(Math.Min(1, (p - 0.72) / 0.28));
        }
        bool exposed = g.CoreExposed && g.Phase != PetalPop.Stage.LevelClear;
        if (exposed)
        {
            double pulse = 0.5 + 0.5 * Math.Sin(g.PresentationTime * 4.2);
            double hr = S * F.CoreApothem * (1.9 + 0.35 * pulse);
            dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Core, 0.55 + 0.35 * pulse), null, c, hr, hr);
        }
        // A cold ball rings the core rather than breaking it: a decaying bell wobble on the radius, plus one
        // ring of its own travelling outward. The wobble is small on purpose — it says "that did nothing",
        // and a big one would read as damage.
        double ring = g.CoreRing;
        if (ring > 0)
        {
            double wobble = ring * Math.Sin(g.PresentationTime * 46) * 0.05;
            scale *= 1 + wobble;
            double rr = S * F.CoreRadius * (1 + (1 - ring) * 1.1);
            dc.DrawEllipse(null, PetalPopPalette.Stroke(PetalPopPalette.CoreHot, Math.Max(1, S * 0.008), ring * 0.55),
                           c, rr, rr);
        }
        // A circle, at the apothem the rings are measured from — the disc inscribed in the core's outline,
        // which is what ring 0 closes onto with its arc.
        var shape = Disc(c, S * F.CoreRadius * scale);
        Color fill = exposed
            ? PetalPopPalette.Lerp(PetalPopPalette.Core, PetalPopPalette.CoreHot, 0.5 + 0.5 * Math.Sin(g.PresentationTime * 4.2))
            : PetalPopPalette.Core;
        dc.DrawGeometry(PetalPopPalette.Solid(fill, alpha), null, shape);
        if (alpha >= 0.99)
        {
            DrawGlass(dc, shape);
            // A smaller disc within — the flower's eye.
            var eye = Disc(c, S * F.CoreRadius * scale * 0.42);
            dc.DrawGeometry(PetalPopPalette.Solid(PetalPopPalette.CoreHot, 0.55), null, eye);
        }
        dc.DrawGeometry(null, PetalPopPalette.Stroke(PetalPopPalette.CelInk, PetalPopPalette.CelWidth, alpha), shape);
    }

    /// <summary>The broken core's seven pieces: each its own outline from <see cref="PetalPopCoreShatter"/>,
    /// drawn about its centroid at the core's radius, in the core's gold with the cel stroke and glass the
    /// whole core wore, so the wreckage reads as that core in pieces and not as generic debris.</summary>
    private static void DrawCoreShards(DrawingContext dc, Point c, double S, PetalPop g)
    {
        if (g.CoreShards.Count == 0) return;
        double r = g.Flower.CoreRadius;
        foreach (var s in g.CoreShards)
        {
            double life = 1 - s.Age / Math.Max(1e-3, PetalPopTuning.CoreShardSeconds);
            if (life <= 0) continue;
            // Solid the whole way: the pieces shrink to nothing rather than fading, holding full size for the
            // first stretch of the flight and then closing down on an ease-in so the vanish reads as a
            // dwindle, not a pop.
            const double hold = 0.35;
            double shrink = Math.Min(1, life / (1 - hold));
            double size = shrink * shrink;
            if (size <= 1e-3) continue;
            double tail = 1;
            var outline = PetalPopCoreShatter.Pieces[s.Shape];
            var centroid = PetalPopCoreShatter.Centroids[s.Shape];
            var at = W(c, S, new Vec2(s.X, s.Y));
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                Point P(int i)
                {
                    var v = (outline[i] - centroid) * (r * size);
                    return new Point(at.X + v.X * S, at.Y + v.Y * S);
                }
                ctx.BeginFigure(P(0), true, true);
                for (int i = 1; i < outline.Length; i++) ctx.LineTo(P(i), true, true);
            }
            geo.Transform = new RotateTransform(s.Rot * 180 / Math.PI, at.X, at.Y);
            geo.Freeze();
            var fill = PetalPopPalette.Lerp(PetalPopPalette.Core, PetalPopPalette.CoreHot, 0.25 + 0.25 * Math.Sin(s.Rot * 2 + s.Shape));
            dc.DrawGeometry(PetalPopPalette.Solid(fill, tail), null, geo);
            if (tail >= 0.99) DrawGlass(dc, geo);
            dc.DrawGeometry(null, PetalPopPalette.Stroke(PetalPopPalette.CelInk, PetalPopPalette.CelWidth, tail), geo);
        }
    }

    private static void DrawShards(DrawingContext dc, Point c, double S, PetalPop g)
    {
        DrawCoreShards(dc, c, S, g);
        foreach (var s in g.Shards)
        {
            double life = 1 - s.Age / Math.Max(1e-3, PetalPopTuning.ShardSeconds);
            if (life <= 0) continue;
            double size = s.Size * S * (0.6 + 0.4 * life);
            var centre = W(c, S, new Vec2(s.X, s.Y));
            var rect = new RectangleGeometry(new Rect(centre.X - size / 2, centre.Y - size * 0.3, size, size * 0.6));
            rect.Transform = new RotateTransform(s.Rot * 180 / Math.PI, centre.X, centre.Y);
            rect.Freeze();
            // Ring −1 is gold confetti (the extra-life shower); everything else is a petal fragment in its ring's colour.
            var colour = s.Ring < 0 ? PetalPopPalette.Lerp(PetalPopPalette.Core, PetalPopPalette.CoreHot, 0.5 + 0.5 * Math.Sin(s.Rot * 3))
                                    : PetalPopPalette.PetalColour(s.Ring);
            var fill = PetalPopPalette.Solid(colour, Math.Min(1, life * 1.4));
            dc.DrawGeometry(fill, PetalPopPalette.Stroke(PetalPopPalette.CelInk, 1, Math.Min(1, life * 1.4)), rect);
        }
    }

    // ── Gems, balls, paddles ──────────────────────────────────────────────────

    /// <summary>The aim dots on a ball waiting to be served: where it will go, drawn along the sim's own
    /// <see cref="PetalPop.ServeAim"/>, so the picture cannot promise a direction the launch does not take.
    /// The shimmer runs outward, which reads as "this way" rather than as a static line.</summary>
    private static void DrawServeAim(DrawingContext dc, Point c, double S, PetalPop g)
    {
        if (g.Phase != PetalPop.Stage.Serve || g.Balls.Count == 0) return;
        var aim = g.ServeAim;
        var from = W(c, S, g.Balls[0].Position);
        double R = PetalPopTuning.BallRadius * S;
        for (int i = 1; i <= 6; i++)
        {
            var p = new Point(from.X + aim.X * R * 2.1 * i, from.Y + aim.Y * R * 2.1 * i);
            double wave = 0.55 + 0.45 * Math.Sin(g.PhaseTime * 7.0 - i * 0.9);
            double alpha = (1 - (i - 1) / 7.0) * wave;
            double size = R * (0.30 - 0.022 * i);
            dc.DrawEllipse(PetalPopPalette.Solid(PetalPopPalette.AccentInk, alpha), null, p, size, size);
        }
    }

    /// <summary>A ball's +punch as 0..1, so every charged effect scales off one number.</summary>
    private static double Charge(PetalPopBall b) =>
        Math.Clamp(b.Punch / (double)Math.Max(1, PetalPopTuning.PunchMax), 0, 1);

    private static void DrawBalls(DrawingContext dc, Point c, double S, PetalPop g)
    {
        double R = PetalPopTuning.BallRadius * S;
        double cap = g.SpeedCap(g.LevelOrdinal);
        bool parked = g.Phase == PetalPop.Stage.Serve;

        // Trails first, under every ball, so a crossing ball rides over another's comet.
        foreach (var b in g.Balls)
        {
            if (b.TrailCount < 2 || b.Speed < 0.05) continue;
            double ch = Charge(b);
            bool hot = ch > 0;
            double reach = Math.Clamp(b.Speed / cap, 0.3, 1.6);
            int n = (int)Math.Min(b.TrailCount, Math.Max(3, PetalPopBall.TrailLength * (hot ? 0.7 + 0.6 * ch : 0.55 * reach)));
            for (int i = n - 1; i >= 0; i--)
            {
                double t = (double)i / n;
                var p = W(c, S, b.TrailAt(i));
                double rr = R * (1 - t) * (hot ? 0.95 + 0.25 * ch : 0.8);
                double alpha = (1 - t) * (hot ? 0.55 + 0.35 * ch : 0.45);
                dc.DrawEllipse(PetalPopPalette.Solid(hot ? PetalPopPalette.TrailHot : PetalPopPalette.Trail, alpha), null, p, rr, rr);
            }
        }

        foreach (var b in g.Balls)
        {
            var p = W(c, S, b.Position);
            double ch = Charge(b);
            bool hot = ch > 0;
            if (hot)
            {
                // The charge reads three ways at once, so the stock is legible at a glance and at speed: the
                // halo swells, an aura ring tightens around it, and one spark orbits per loaded +punch.
                dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Trail, 0.6 + 0.4 * ch), null, p,
                               R * (2.1 + 1.7 * ch), R * (2.1 + 1.7 * ch));
                double ring = R * (1.7 + 0.7 * ch);
                dc.DrawEllipse(null, PetalPopPalette.Stroke(PetalPopPalette.TrailHot, Math.Max(1, S * 0.004 * (1 + 2 * ch)),
                                                            0.35 + 0.45 * ch), p, ring, ring);
                double spin = g.PresentationTime * (2.2 + 3.5 * ch);
                double spark = R * (0.20 + 0.14 * ch);
                for (int k = 0; k < b.Punch; k++)
                {
                    double a = spin + k * Math.Tau / Math.Max(1, b.Punch);
                    var sp = new Point(p.X + Math.Cos(a) * ring, p.Y + Math.Sin(a) * ring);
                    dc.DrawEllipse(PetalPopPalette.Solid(PetalPopPalette.TrailHot, 0.95), null, sp, spark, spark);
                }
            }
            if (parked)
            {
                // A quiet ring around the parked ball says "this is about to go", with no words.
                double pulse = 0.5 + 0.5 * Math.Sin(g.PhaseTime * 5.0);
                dc.DrawEllipse(null, PetalPopPalette.Stroke(PetalPopPalette.AccentInk, Math.Max(1, S * 0.006), 0.35 + 0.35 * pulse),
                               p, R * (1.8 + 0.5 * pulse), R * (1.8 + 0.5 * pulse));
            }

            var group = new TransformGroup();
            double speed = b.Speed;
            // The pearl stays a circle in ordinary play; only a smashed ball deforms — stretched along its
            // travel and squashed into what it hits — so the deformation reads as the slam's signature.
            if (hot && speed > 1e-6)
            {
                // Stretched along its travel by speed; the pearl leans into where it is going.
                double k = Math.Clamp(speed / cap, 0, 1.6) * (0.18 + 0.16 * ch);
                double ang = Math.Atan2(b.VY, b.VX) * 180 / Math.PI;
                group.Children.Add(new RotateTransform(-ang, p.X, p.Y));
                group.Children.Add(new ScaleTransform(1 + k, 1 / (1 + k), p.X, p.Y));
                group.Children.Add(new RotateTransform(ang, p.X, p.Y));
            }
            if (hot && b.Squash > 0)
            {
                // Squashed into whatever it just hit, springing back as the timer runs down.
                double e = b.Squash * b.Squash * (0.7 + 0.3 * ch);
                double ang = Math.Atan2(b.HitNY, b.HitNX) * 180 / Math.PI;
                group.Children.Add(new RotateTransform(-ang, p.X, p.Y));
                group.Children.Add(new ScaleTransform(1 - 0.38 * e, 1 + 0.30 * e, p.X, p.Y));
                group.Children.Add(new RotateTransform(ang, p.X, p.Y));
            }
            group.Freeze();
            var shape = new EllipseGeometry(p, R, R) { Transform = group };
            shape.Freeze();
            // The body's shade says how much +punch it carries: pearl cold, a touch of orange at one, and
            // deepening toward the charge colour as the stock climbs. Through the shot clock's last second a
            // parked ball flashes the warning orange; the rest of the wait it looks exactly as it does in play.
            Color bodyColour = hot
                ? PetalPopPalette.Lerp(PetalPopPalette.Ball, PetalPopPalette.BallCharge, 0.60 + 0.40 * ch)
                : PetalPopPalette.Ball;
            if (parked)
            {
                double left = PetalPopTuning.ServeAutoSeconds - g.PhaseTime;
                if (left <= PetalPopTuning.ServeWarnSeconds)
                {
                    double flash = 0.5 + 0.5 * Math.Sin(g.PhaseTime * 28);
                    bodyColour = PetalPopPalette.Lerp(bodyColour, PetalPopPalette.BallWarn, 0.35 + 0.65 * flash);
                }
            }
            dc.DrawGeometry(PetalPopPalette.Solid(bodyColour), null, shape);
            DrawGlass(dc, shape);
            dc.DrawGeometry(null, PetalPopPalette.FineCel, shape);
        }
    }

    private static void DrawPaddles(DrawingContext dc, Point c, double S, PetalPopLayout L, PetalPop g)
    {
        // Tension shows while the slingshot is drawn and through the armed window after the release.
        double armed = Math.Max(g.Draw, g.SmashArmedLeft / Math.Max(1e-6, PetalPopTuning.SmashWindowSeconds));
        var paddles = g.Paddles;
        for (int i = 0; i < L.Sides && i < paddles.Count; i++)
        {
            var paddle = paddles[i];
            var s = L.Side[i];
            double ac = paddle.Pos / L.ArcRadius;
            var centre = W(c, S, s.PointAt(ac));
            var n = s.NormalAt(ac);

            // Body: the tilted spine the ball actually collides with, as a slab whose field-facing corners are
            // rounded and whose rail-facing corners are square.
            // Cloned: the shared shape comes frozen, and the squash below needs a transform on it.
            // Kept in both forms: the transformed one is what is drawn, the untransformed one is what
            // replacement art is measured against — art is drawn inside the same transform, so measuring the
            // already-transformed shape would apply the squash twice.
            // Drawing: the slab bows back while the slingshot is drawn, and bows forward — middle toward the
            // centre — through the slam's surge, in proportion to how far the paddles have lunged, to sell the
            // speed of the release. Presentation only: the collider is fed the draw alone, never this.
            double bow = g.Draw > 0 ? g.Draw
                : g.Lunge > 0 ? -Math.Clamp(g.Lunge / Math.Max(1e-6, PetalPopTuning.SmashLungeDepth) * PetalPopTuning.SlamBowIn, 0, 1)
                : 0;
            var plain = PaddleShape(c, S, L, i, paddle.Pos, g.Lunge, 0, bow);
            var capsule = plain.Clone();

            // Tense when armed: the slab bulges toward the field, ready to hit. An ordinary ball strike does not
            // deform the paddle — the ball takes the squash — so the slab is otherwise drawn as it is.
            double scaleN = 1 + 0.28 * armed, scaleT = 1 - 0.10 * armed;
            double ang = Math.Atan2(n.Y, n.X) * 180 / Math.PI;
            var group = new TransformGroup();
            group.Children.Add(new RotateTransform(-ang, centre.X, centre.Y));
            group.Children.Add(new ScaleTransform(scaleN, scaleT, centre.X, centre.Y));
            group.Children.Add(new RotateTransform(ang, centre.X, centre.Y));
            group.Freeze();
            capsule.Transform = group;
            capsule.Freeze();

            // The body wears its charge tier: brass at rest, warming toward the armed cream as the draw begins,
            // yellow-orange once a release would add one +punch, and a pulsing red-orange at two — so the
            // player reads what the windup is worth off the paddle itself, before letting go.
            int charge = g.SmashCharge;
            Color body;
            if (charge >= 2)
            {
                double pulse = 0.5 + 0.5 * Math.Sin(g.PresentationTime * 14);
                body = PetalPopPalette.Lerp(PetalPopPalette.PaddleCharge2, PetalPopPalette.PaddleCharge2Hot, pulse);
            }
            else if (charge == 1) body = PetalPopPalette.PaddleCharge1;
            else body = armed > 0 ? PetalPopPalette.Lerp(PetalPopPalette.Paddle, PetalPopPalette.PaddleArmed, armed) : PetalPopPalette.Paddle;
            // ⚠ No charge glow behind the slab. It pulsed at the top tier and read as a halo the paddle did
            // not need; what the windup is worth is said by the bow, which the stage art already carries.
            dc.DrawGeometry(PetalPopPalette.Solid(body), null, capsule);
            // Wood grain: three dark lines and a light streak along the slab, clipped to the body, faint
            // enough to leave the charge tiers legible. The lines run along the paddle (the tangent), spaced
            // across its thickness in the same normal-scaled frame the slab itself bulges in.
            double thick = PetalPopTuning.PaddleHalfThickness * S * scaleN;
            double half = g.PaddleHalfLength * S * 1.15;
            var tAxis = new Vector(n.Y, -n.X);
            var nAxis = new Vector(n.X, n.Y);
            dc.PushClip(capsule);
            double grainAlpha = 0.28 * (1 - 0.6 * Math.Min(1, charge));
            foreach (var (off, w, light) in new[] { (-0.55, 0.9, false), (-0.10, 1.4, false), (0.45, 0.8, false), (0.18, 1.0, true) })
            {
                var o = nAxis * (off * thick);
                var a = new Point(centre.X - tAxis.X * half + o.X, centre.Y - tAxis.Y * half + o.Y);
                var b = new Point(centre.X + tAxis.X * half + o.X, centre.Y + tAxis.Y * half + o.Y);
                var pen = PetalPopPalette.Stroke(light ? PetalPopPalette.PaddleGrainLight : PetalPopPalette.PaddleGrain,
                                                 Math.Max(0.8, S * 0.0045 * w), light ? grainAlpha * 0.8 : grainAlpha);
                dc.DrawLine(pen, a, b);
            }
            dc.Pop();
            DrawGlass(dc, capsule);
            dc.DrawGeometry(null, PetalPopPalette.CelPen, capsule);
        }
    }



    private static void DrawBursts(DrawingContext dc, Point c, double S, PetalPop g)
    {
        foreach (var b in g.Bursts)
        {
            double t = Math.Clamp(b.Age / Math.Max(1e-3, PetalPopTuning.BurstSeconds), 0, 1);
            double fade = 1 - t;
            var p = W(c, S, new Vec2(b.X, b.Y));
            switch (b.Kind)
            {
                case PetalPopBurstKind.Split:
                    Ring(dc, p, S * (0.03 + 0.17 * EaseOut(t)), S * 0.016 * fade, PetalPopPalette.Gem, 0.9 * fade);
                    Ring(dc, p, S * (0.02 + 0.10 * EaseOut(t)), S * 0.008 * fade, PetalPopPalette.BallHot, 0.7 * fade);
                    break;
                case PetalPopBurstKind.Pop:
                    Ring(dc, p, S * (0.02 + 0.09 * EaseOut(t)), S * 0.010 * fade, PetalPopPalette.GutterLost, 0.8 * fade);
                    break;
                case PetalPopBurstKind.Core:
                    dc.DrawEllipse(PetalPopPalette.Solid(PetalPopPalette.CoreHot, 0.35 * fade * fade), null, p, S, S);
                    Ring(dc, p, S * (0.10 + 0.70 * EaseOut(t)), S * 0.03 * fade, PetalPopPalette.Core, 0.9 * fade);
                    Ring(dc, p, S * (0.05 + 0.45 * EaseOut(t)), S * 0.014 * fade, PetalPopPalette.CoreHot, 0.8 * fade);
                    break;
                case PetalPopBurstKind.Smash:
                    Ring(dc, p, S * (0.03 + 0.22 * EaseOut(t)), S * 0.018 * fade, PetalPopPalette.ShoutSmash, 0.9 * fade);
                    Ring(dc, p, S * (0.02 + 0.13 * EaseOut(t)), S * 0.010 * fade, PetalPopPalette.Trail, 0.8 * fade);
                    break;
                case PetalPopBurstKind.Chip:
                    Ring(dc, p, S * (0.01 + 0.05 * EaseOut(t)), S * 0.006 * fade, PetalPopPalette.PetalVein, 0.7 * fade);
                    break;
            }
        }
    }

    private static void Ring(DrawingContext dc, Point p, double radius, double width, Color colour, double alpha) =>
        ArcadeChrome.DrawRing(dc, p, radius, width, colour, alpha);

    // ── HUD ───────────────────────────────────────────────────────────────────

    private static void DrawHud(DrawingContext dc, Point c, double S, PetalPopLayout L, PetalPop g, double ppd)
    {
        // The HUD lives in the gutter lenses: score under the serving paddle, lives one side round to the
        // left, level one side round to the right. The lens midpoint radius follows the side count — a square's
        // gutters are deep, an octagon's shallow.
        double lensR = (L.RailMidRadius + 1.0) / 2;
        int serve = L.ServeSide;
        double pulse = Math.Clamp(g.ScorePulse, 0, 1);
        var scoreAt = Polar(c, S * lensR, L.SideAngle(serve));
        var ink = pulse <= 0.02 ? PetalPopPalette.Ink
            : PetalPopPalette.Solid(PetalPopPalette.Lerp(PetalPopPalette.InkColour, PetalPopPalette.Core, pulse));
        double lensDepth = 1.0 - L.RailMidRadius;
        var score = ArcadeChrome.Text(g.Score.ToString("N0"), ArcadeChrome.Ui(Math.Max(9, S * Math.Min(0.075, lensDepth * 0.42))) * (1 + 0.22 * pulse), ink, ppd, TextAlignment.Left);
        ArcadeChrome.DrawInkCentered(dc, score, scoreAt);
        if (pulse > 0.05)
            Ring(dc, scoreAt, S * (0.045 + (1 - pulse) * 0.06), S * 0.010 * pulse, PetalPopPalette.Core, 0.6 * pulse);

        // Lives as small pearls in the lens to the left of the serve side.
        double livesAngle = L.SideAngle((serve + 1) % L.Sides);
        var livesAt = Polar(c, S * lensR, livesAngle);
        double gain = Math.Clamp(g.LivesGain, 0, 1);
        double pip = ArcadeChrome.UiArt(Math.Max(2.5, S * 0.016)) * (1 + 0.35 * gain);
        var along = new Point(Math.Cos(livesAngle), Math.Sin(livesAngle));   // tangent of the rim at that angle
        int lives = g.Lives;
        double flash = g.LivesFlash;
        int shown = flash > 0 ? lives + 1 : lives;
        for (int i = 0; i < shown; i++)
        {
            double off = (i - (shown - 1) / 2.0) * pip * 2.6;
            var p = new Point(livesAt.X + along.X * off, livesAt.Y + along.Y * off);
            // The pearl just earned rings outward in gold while the gain clock runs.
            if (gain > 0 && i == lives - 1)
                Ring(dc, p, pip * (1 + 3.5 * (1 - gain)), Math.Max(1, pip * 0.6 * gain), PetalPopPalette.CoreHot, gain);
            if (i >= lives)
            {
                // The pearl just spent flickers out.

                Ring(dc, p, pip * (1 + 2 * (1 - flash)), pip * 0.5 * flash, PetalPopPalette.GutterLost, flash);
                continue;
            }
            if (gain > 0 && i == lives - 1)
                dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Gem, gain), null, p, pip * 3, pip * 3);
            dc.DrawEllipse(PetalPopPalette.BallFill, PetalPopPalette.FineCel, p, pip, pip);
        }

        // Level in the lens to the right of the serve side.
        var levelAt = Polar(c, S * lensR, L.SideAngle((serve - 1 + L.Sides) % L.Sides));
        var level = ArcadeChrome.Text(Loc.F(UiText.Arcade.StageLevel, g.StageOrdinal, g.Level), ArcadeChrome.Ui(Math.Max(8, S * Math.Min(0.042, lensDepth * 0.25))),
                                      PetalPopPalette.InkDim, ppd, TextAlignment.Left);
        ArcadeChrome.DrawInkCentered(dc, level, levelAt);
    }

    private static void DrawShoutIfAny(DrawingContext dc, Point c, double S, PetalPop g, double ppd)
    {
        if (g.Shout == PetalPop.ShoutKind.None || g.ShoutLeft <= 0) return;
        double total = Math.Max(1e-3, PetalPopTuning.ShoutSeconds);
        double age = total - g.ShoutLeft;
        double pop = age < 0.16 ? 1.35 - 0.35 * (age / 0.16) : 1.0 + Math.Sin(age * Math.PI) * 0.05;
        double alpha = Math.Clamp(g.ShoutLeft / 0.34, 0, 1);
        double rise = S * 0.10 * (age / total);
        (string text, Color ink, double size) = g.Shout switch
        {
            PetalPop.ShoutKind.Combo => (Loc.F(UiText.Arcade.Combo, g.ShoutValue), PetalPopPalette.ShoutCombo, 0.11),
            PetalPop.ShoutKind.ExtraLife => (Loc.T(UiText.Arcade.ExtraLife), PetalPopPalette.CoreHot, 0.15),
            _ => (Loc.F(UiText.Arcade.StageLevel, g.ShoutValue, g.ShoutValue2), PetalPopPalette.ShoutLevel, 0.11),
        };
        // Play never stops for a shout here (the ball is live through combos and stage changes), so it keeps
        // its own size, not the HUD scale, and sits up between the petals and the top rail rather than over
        // the flower.
        ArcadeChrome.DrawShout(dc, new Point(c.X, c.Y - S * 0.55 - rise), S, text, ink, age, pop, alpha, size, ppd,
                               ArcadeChrome.ShoutPaint.Quantised);
    }

    // ── Cards ─────────────────────────────────────────────────────────────────

    private static void DrawIntro(DrawingContext dc, Point c, double field, double ppd)
    {
        dc.DrawEllipse(PetalPopPalette.Scrim, null, c, field, field);
        ArcadeChrome.DrawCentered(dc, "PETALPOP", ArcadeChrome.Ui(Math.Max(16, field * 0.12)), PetalPopPalette.Ink,
            c.X, c.Y - field * 0.46, ppd, field * 1.5);
        ArcadeChrome.DrawCentered(dc, $"{ControllerButtons.Text(PadButton.Triangle)}  {Loc.T(UiText.Arcade.HowToPlay)}",
            ArcadeChrome.Ui(Math.Max(9, field * 0.052)), PetalPopPalette.Accent, c.X, c.Y - field * 0.08, ppd, field * 1.6);
        ArcadeChrome.DrawCentered(dc, $"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Begin)}",
            ArcadeChrome.Ui(Math.Max(9, field * 0.048)), PetalPopPalette.InkDim, c.X, c.Y + field * 0.16, ppd, field * 1.5);
    }

    private static void DrawGameOver(DrawingContext dc, Point c, double field, PetalPop g, double ppd)
    {
        dc.DrawEllipse(PetalPopPalette.Scrim, null, c, field, field);
        ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.GameOver), ArcadeChrome.Ui(Math.Max(14, field * 0.095)), PetalPopPalette.Danger,
            c.X, c.Y - field * 0.50, ppd, field * 1.5);
        DrawEndStack(dc, c, field, g, ppd);
    }

    /// <summary>The run's end, the good way: 8-8 cleared.</summary>
    private static void DrawWin(DrawingContext dc, Point c, double field, PetalPop g, double ppd)
    {
        dc.DrawEllipse(PetalPopPalette.Scrim, null, c, field, field);
        // The core, whole and enormous, behind the word — the thing every level was peeled toward.
        double pulse = 0.5 + 0.5 * Math.Sin(g.PhaseTime * 2.2);
        dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Core, 0.5 + 0.3 * pulse), null, c, field * 0.9, field * 0.9);
        var core = Disc(c, field * g.Flower.CoreRadius * (3.2 + 0.15 * pulse));
        dc.DrawGeometry(PetalPopPalette.Solid(PetalPopPalette.Core, 0.55), null, core);
        dc.DrawGeometry(null, PetalPopPalette.Stroke(PetalPopPalette.CelInk, PetalPopPalette.CelWidth, 0.55), core);
        ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.YouWin), ArcadeChrome.Ui(Math.Max(16, field * 0.12)), PetalPopPalette.Ink,
            c.X, c.Y - field * 0.54, ppd, field * 1.5);
        DrawEndStack(dc, c, field, g, ppd);
    }

    private static void DrawEndStack(DrawingContext dc, Point c, double field, PetalPop g, double ppd)
    {
        ArcadeChrome.DrawCentered(dc, Loc.F(UiText.Arcade.StageLevel, g.StageOrdinal, g.Level), ArcadeChrome.Ui(Math.Max(8, field * 0.045)), PetalPopPalette.InkDim,
            c.X, c.Y - field * 0.22, ppd, field * 1.5);
        ArcadeChrome.DrawCentered(dc, g.Score.ToString("N0"), ArcadeChrome.Ui(Math.Max(15, field * 0.115)), PetalPopPalette.Ink,
            c.X, c.Y - field * 0.10, ppd, field * 1.6);
        ArcadeChrome.DrawCentered(dc, Loc.F(UiText.Arcade.Best, g.HighScore.ToString("N0")), ArcadeChrome.Ui(Math.Max(9, field * 0.050)), PetalPopPalette.Accent,
            c.X, c.Y + field * 0.20, ppd, field * 1.5);
        // Both ways out of the end screen, so ✕ is not the only door the card admits to.
        ArcadeChrome.DrawCenteredRow(dc,
            [$"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Again)}",
             $"{ControllerButtons.Text(PadButton.Circle)}  {Loc.T(UiText.Arcade.ExitGame)}"],
            ArcadeChrome.Ui(Math.Max(9, field * 0.047)), PetalPopPalette.InkDim, c.X, c.Y + field * 0.40, ppd);
    }

    // ── How-to illustrations ──────────────────────────────────────────────────
    // Drawn with the board's own routines and palette so the card teaches the shapes the player will see —
    // on the square the first stage is played on.

    public void DrawHowToArt(DrawingContext dc, Rect box, string art, double ppd)
    {
        var c = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
        double r = Math.Min(box.Width, box.Height) / 2;
        var L = PetalPopLayout.For(Math.Clamp(PetalPopTuning.SidesStart, PetalPopLayout.MinSides, PetalPopLayout.MaxSides));
        int serve = L.ServeSide;
        switch (art)
        {
            // ⚠ These three share one framing — the whole arena at ArenaS, centred. They are the controls
            // half of the card and read as a sequence; the two flower panels below are close-ups because
            // their subject is a single petal. Changing the scale between line 1 and line 2 is what made
            // this set incoherent before, and a wide shot with a lone arrow in the middle of it read as
            // something standing in the playfield rather than as a direction.
            case "paddles":
            {
                double S = ArenaS(r);
                dc.DrawGeometry(PetalPopPalette.Ground, PetalPopPalette.Stroke(PetalPopPalette.CelInk, 1.2), ArcPolygon(c, S, L));
                // The rest position in ghost under each paddle, and the paddle itself slid from it — so
                // "All of them, together, the way you pushed" is shown by the displacement rather than said
                // by an arrow pointing at nothing.
                var push = new Vec2(0.8, -0.6);
                for (int i = 0; i < L.Sides; i++)
                    MiniPaddle(dc, c, S, L, i, 0, PetalPopPalette.Solid(PetalPopPalette.CelInk, 0.16));
                for (int i = 0; i < L.Sides; i++)
                {
                    var s = L.Side[i];
                    double pos = Math.Clamp(push.Dot(s.Chord), -1, 1) * L.PosMax;
                    MiniPaddle(dc, c, S, L, i, pos, PetalPopPalette.PaddleFill);
                }
                break;
            }

            // Keeping it in play, and what it is for: the ball comes off a paddle and heads back in at the
            // gold core, which is the thing the line names and the only gold on the card.
            case "gutter":
            {
                double S = ArenaS(r);
                dc.DrawGeometry(PetalPopPalette.Ground, PetalPopPalette.Stroke(PetalPopPalette.CelInk, 1.2), ArcPolygon(c, S, L));
                for (int i = 0; i < L.Sides; i++)
                    MiniPaddle(dc, c, S, L, i, 0, PetalPopPalette.PaddleFill);
                var s = L.Side[serve];
                var off = W(c, S, s.PointAt(0) + s.NormalAt(0) * 0.10);
                double coreR = r * 0.20;
                dc.DrawEllipse(PetalPopPalette.CoreFill, PetalPopPalette.FineCel, c, coreR, coreR);
                // The trail runs from the paddle to the ball, so the bounce reads as having just happened.
                for (int t = 1; t <= 4; t++)
                {
                    double k = t / 5.0;
                    var tp = new Point(off.X + (c.X - off.X) * k * 0.5, off.Y + (c.Y - off.Y) * k * 0.5);
                    dc.DrawEllipse(PetalPopPalette.Solid(PetalPopPalette.Trail, 0.45 - 0.08 * t), null, tp,
                                   r * 0.09 * (1 - 0.12 * t), r * 0.09 * (1 - 0.12 * t));
                }
                var ballAt = new Point(off.X + (c.X - off.X) * 0.34, off.Y + (c.Y - off.Y) * 0.34);
                dc.DrawEllipse(PetalPopPalette.BallFill, PetalPopPalette.FineCel, ballAt, r * 0.12, r * 0.12);
                break;
            }
            case "petals":
            {
                // A level-3 flower: two-hit petals against the core, one-hit ones outside, one already chipped.
                var F = PetalPopFlower.For(L.Sides, 3);
                double S = r * 2.6;
                var at = new Point(c.X, c.Y + r * 0.10);
                var core = Disc(at, S * F.CoreRadius);
                for (int ring = 0; ring < Math.Min(2, F.Rings); ring++)
                    for (int side = 0; side < L.Sides; side++)
                    {
                        if (side != serve && side != (serve + 1) % L.Sides && side != (serve - 1 + L.Sides) % L.Sides) continue;
                        for (int m = 0; m < F.CountPerSide[ring]; m++)
                        {
                            int idx = F.BrickIndex(ring, side, m);
                            int hp = side == serve && ring == 0 && m == 0 ? Math.Max(1, F.MaxHp[ring] - 1) : F.MaxHp[ring];
                            DrawPetal(dc, at, S, F, idx, ring, hp, 1);
                        }
                    }
                dc.DrawGeometry(PetalPopPalette.CoreFill, null, core);
                DrawGlass(dc, core);
                dc.DrawGeometry(null, PetalPopPalette.FineCel, core);
                break;
            }
            case "multiball":
            {
                // The marked petal — the mint alone is the cue — and the three balls that come out of popping it.
                var markAt = new Point(c.X - r * 0.5, c.Y + r * 0.3);
                double mw = r * 0.62;
                var petal = new RectangleGeometry(new Rect(markAt.X - mw / 2, markAt.Y - mw * 0.32, mw, mw * 0.64), mw * 0.2, mw * 0.2);
                petal.Freeze();
                dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Gem, 0.85), null, markAt, r * 0.75, r * 0.75);
                dc.DrawGeometry(PetalPopPalette.GemFill, null, petal);
                DrawGlass(dc, petal);
                dc.DrawGeometry(null, PetalPopPalette.FineCel, petal);
                for (int k = -1; k <= 1; k++)
                {
                    double a = -0.55 + k * 0.45;
                    var dir = new Point(Math.Cos(a), Math.Sin(a));
                    for (int t = 3; t >= 1; t--)
                    {
                        var tp = new Point(markAt.X + dir.X * r * (0.5 + 0.22 * t), markAt.Y + dir.Y * r * (0.5 + 0.22 * t));
                        dc.DrawEllipse(PetalPopPalette.Solid(PetalPopPalette.Trail, 0.5 - 0.12 * t), null, tp, r * 0.07, r * 0.07);
                    }
                    var bp = new Point(markAt.X + dir.X * r * 1.2, markAt.Y + dir.Y * r * 1.2);
                    dc.DrawEllipse(PetalPopPalette.BallFill, PetalPopPalette.FineCel, bp, r * 0.12, r * 0.12);
                }
                break;
            }
            // The charged paddle and what it buys. Same arena as the two panels above — the one armed paddle
            // against three at rest is what says which paddle is charged, so the pale fill has something to
            // be pale against.
            case "smash":
            {
                double S = ArenaS(r);
                dc.DrawGeometry(PetalPopPalette.Ground, PetalPopPalette.Stroke(PetalPopPalette.CelInk, 1.2), ArcPolygon(c, S, L));
                for (int i = 0; i < L.Sides; i++)
                    if (i != serve) MiniPaddle(dc, c, S, L, i, 0, PetalPopPalette.PaddleFill);
                var s = L.Side[serve];
                var seat = W(c, S, s.PointAt(0));
                dc.DrawEllipse(PetalPopPalette.Halo(PetalPopPalette.Trail, 0.9), null, seat, r * 0.34, r * 0.34);
                MiniPaddle(dc, c, S, L, serve, 0, PetalPopPalette.PaddleArmedFill);
                // Straight up the rail's normal, into the arena — the hot ball leaving the charge behind it.
                var up = s.NormalAt(0);
                var ballAt = new Point(seat.X + up.X * r * 0.62, seat.Y + up.Y * r * 0.62);
                for (int t = 1; t <= 4; t++)
                {
                    var tp = new Point(ballAt.X - up.X * r * 0.13 * t, ballAt.Y - up.Y * r * 0.13 * t);
                    dc.DrawEllipse(PetalPopPalette.Solid(PetalPopPalette.TrailHot, 0.7 - 0.15 * t), null, tp,
                                   r * 0.11 * (1 - 0.15 * t), r * 0.11 * (1 - 0.15 * t));
                }
                dc.DrawEllipse(PetalPopPalette.BallHotFill, PetalPopPalette.FineCel, ballAt, r * 0.13, r * 0.13);
                Ring(dc, ballAt, r * 0.28, r * 0.045, PetalPopPalette.ShoutSmash, 0.8);
                break;
            }
        }
    }

    /// <summary>The arena's half-size on a how-to panel. ⚠ One number for every controls panel: the three
    /// that share this framing are read as a sequence, and a scale that changes between them reads as a
    /// change of subject.</summary>
    private static double ArenaS(double r) => r * 0.95;

    private static void MiniPaddle(DrawingContext dc, Point c, double S, PetalPopLayout L, int side, double pos, Brush fill)
    {
        var capsule = PaddleShape(c, S, L, side, pos, 0, Math.Max(1, PetalPopTuning.PaddleHalfThickness * S));
        dc.DrawGeometry(fill, null, capsule);
        DrawGlass(dc, capsule);
        dc.DrawGeometry(null, PetalPopPalette.FineCel, capsule);
    }

    /// <summary>The paddle slab: the spine thickened by <see cref="PetalPopTuning.PaddleHalfThickness"/> either
    /// side of the face. The two corners toward the field are rounded (a full quarter circle, so the ball rolls
    /// off the end rather than catching), the two toward the rail are square. <paramref name="bow"/> 0..1 bends
    /// the whole slab like a drawn bow — its ends stay put and its middle is pulled back from the field, the
    /// thickness constant along the curve — which is the slingshot's tension. Shared by the live board and the
    /// how-to card so the paddle reads the same in both.</summary>
    private static Geometry PaddleShape(Point c, double S, PetalPopLayout L, int side, double pos, double lunge,
                                        double minHalfPx = 0, double bow = 0)
    {
        var a = L.PaddleSpine(side, pos, -1, lunge, bow);
        var b = L.PaddleSpine(side, pos, 1, lunge, bow);
        var t = (b - a).Normalized();
        var f = L.PaddleFaceNormal(side, pos);
        double h = Math.Max(PetalPopTuning.PaddleHalfThickness, minHalfPx / S);
        // The spine is the sim's own bent spine, so the slab is drawn exactly where the collider is; the local
        // frame follows it so the slab keeps one thickness all the way round the bend.
        Vec2 P(double u) => L.PaddleSpine(side, pos, u, lunge, bow);
        Vec2 T(double u) => (P(Math.Min(1, u + 1e-3)) - P(Math.Max(-1, u - 1e-3))).Normalized();
        Vec2 N(double u) { var d = T(u); return f * d.Dot(t) - t * d.Dot(f); }
        const int arc = 6, span = 14;
        // ⚠ The slab reaches half a thickness past each spine end, exactly as far as the collider does (which
        // measures distance to the spine, so its ends are rounded caps of radius h). Drawing it flush with
        // the spine ends made the paddle look narrower than the thing the ball actually bounces off.
        var ta = T(-1); var na = N(-1); var pa = P(-1);
        var tb = T(1); var nb = N(1); var pb = P(1);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Rail edge, square corner to square corner.
            ctx.BeginFigure(W(c, S, pa - ta * h - na * h), true, true);
            for (int k = 0; k <= span; k++)
            {
                double u = -1 + 2.0 * k / span;
                ctx.LineTo(W(c, S, P(u) - N(u) * h), true, true);
            }
            ctx.LineTo(W(c, S, pb + tb * h - nb * h), true, true);
            // End B: up the square end to spine level, then a quarter circle about the spine end onto the field edge.
            ctx.LineTo(W(c, S, pb + tb * h), true, true);
            for (int k = 1; k <= arc; k++)
            {
                double th = Math.PI / 2 * k / arc;
                ctx.LineTo(W(c, S, pb + tb * (h * Math.Cos(th)) + nb * (h * Math.Sin(th))), true, true);
            }
            // Field edge, following the bend.
            for (int k = 1; k < span; k++)
            {
                double u = 1 - 2.0 * k / span;
                ctx.LineTo(W(c, S, P(u) + N(u) * h), true, true);
            }
            // End A: quarter circle about the spine end down to spine level; the close runs the square end home.
            for (int k = 0; k <= arc; k++)
            {
                double th = Math.PI / 2 * k / arc;
                ctx.LineTo(W(c, S, pa + na * (h * Math.Cos(th)) - ta * (h * Math.Sin(th))), true, true);
            }
        }
        geo.Freeze();
        return geo;
    }


    // ── Shared drawing ────────────────────────────────────────────────────────

    /// <summary>The glass pass — gloss, an inner rim that shades on the lit side and brightens on the far one,
    /// and a hard specular — inside a clip of the shape. Call before the cel outline.
    /// <paramref name="strength"/> thins the whole pass at once — gloss, rim and specular together, so a
    /// surface can be made flatter without any one of the three going missing and leaving the shape lit
    /// from nowhere.</summary>
    private static void DrawGlass(DrawingContext dc, Geometry shape, double strength = 1)
    {
        Rect bounds = shape.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 1 || bounds.Height <= 1) return;
        if (strength <= 0.01) return;
        // The strength is folded into the brushes (PetalPopPalette.Faded) rather than a PushOpacity around
        // the pass — a transparency layer per petal per frame. The gloss fills the clip shape itself, so it
        // draws before the clip; only the wide inner rim and the specular need cropping.
        dc.DrawGeometry(PetalPopPalette.Faded(PetalPopPalette.Gloss, strength), null, shape);
        dc.PushClip(shape);
        double rim = Math.Max(1.5, Math.Min(bounds.Width, bounds.Height) * 0.16);
        dc.DrawGeometry(null, PetalPopPalette.RimPen(rim * 2, strength), shape);
        var spot = new Point(bounds.X + bounds.Width * 0.32, bounds.Y + bounds.Height * 0.24);
        dc.DrawEllipse(PetalPopPalette.Faded(PetalPopPalette.Specular, strength), null, spot, bounds.Width * 0.21, bounds.Height * 0.15);
        dc.Pop();
    }

    /// <summary>How much of the glass pass a petal wears. Held well down: the petals are the largest flat
    /// area on the board and the sprite art around them is cel-shaded, so a full glass reading on them is
    /// the one surface that breaks the style.</summary>
    private const double PetalGloss = 0.25;

    /// <summary>The core, as the circle it is. Frozen so the clip and the strokes over it share one object.</summary>
    private static Geometry Disc(Point centre, double radius)
    {
        var geo = new EllipseGeometry(centre, Math.Max(0.01, radius), Math.Max(0.01, radius));
        geo.Freeze();
        return geo;
    }

    private static Point W(Point c, double S, Vec2 v) => new(c.X + v.X * S, c.Y + v.Y * S);
    private static Point Polar(Point c, double radius, double angle) => ArcadePalette.Polar(c, radius, angle);
    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static double EaseOut(double t) { t = Math.Clamp(t, 0, 1); return 1 - (1 - t) * (1 - t); }
    private static double EaseOutBack(double t)
    {
        t = Math.Clamp(t, 0, 1);
        const double c1 = 1.70158, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }
}
