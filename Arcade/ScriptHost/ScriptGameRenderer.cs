using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// The one renderer every drop-in script game shares. It never simulates and never trusts: it
/// pumps the jailed session once per rendered frame (the host clip and safe area are already in
/// place around <see cref="Draw"/>) and replays the last validated polar command buffer —
/// polar→cartesian happens here, so a script never learns where the disc is on screen.
/// A session that gave up (restart budget spent) draws a guard-style card instead; ○ still
/// dismisses through the normal host path, and the card offers nothing else.
/// </summary>
internal sealed class ScriptGameRenderer : IArcadeRenderer
{
    public static readonly ScriptGameRenderer Instance = new();

    public Brush Accent => ArcadeChrome.Ink;

    private static readonly Dictionary<uint, SolidColorBrush> _brushes = [];

    private static SolidColorBrush BrushFor(uint argb)
    {
        if (_brushes.TryGetValue(argb, out var b)) return b;
        if (_brushes.Count > 128) _brushes.Clear();   // scripts churn palettes; keep the cache bounded
        b = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        b.Freeze();
        _brushes[argb] = b;
        return b;
    }

    public void Draw(DrawingContext dc, Point c, double fieldRadius, IArcadeGame game, double ppd)
    {
        if (game is not ScriptArcadeGame script) return;

        var buffer = ScriptSessionCoordinator.Pump(script);
        foreach (var cmd in buffer)
            DrawCommand(dc, c, fieldRadius, cmd, ppd);

        if (ScriptSessionCoordinator.FaultFor(script) is not null)
            DrawFaultCard(dc, c, fieldRadius, ppd);
    }

    /// <summary>Replay the last validated buffer without pumping the session — for a capture taken outside
    /// the frame pump. Draws nothing when the helper has produced no frame yet.</summary>
    public void DrawLast(DrawingContext dc, Point c, double fieldRadius, ScriptArcadeGame game, double ppd)
    {
        foreach (var cmd in ScriptSessionCoordinator.LastBuffer(game))
            DrawCommand(dc, c, fieldRadius, cmd, ppd);
    }

    private static Point At(Point c, double field, double r, double deg)
    {
        double rad = deg * Math.PI / 180.0;
        return new Point(c.X + field * r * Math.Sin(rad), c.Y - field * r * Math.Cos(rad));
    }

    private static void DrawCommand(DrawingContext dc, Point c, double field, ScriptDrawCommand cmd, double ppd)
    {
        var fill = BrushFor(cmd.c);
        Pen? stroke = cmd.sc is { } sc && sc != 0
            ? new Pen(BrushFor(sc), Math.Max(1, cmd.sw * field)) : null;
        stroke?.Freeze();

        switch (cmd.t)
        {
            case "dot":
            {
                var p = At(c, field, cmd.r, cmd.a);
                dc.DrawEllipse(fill, stroke, p, cmd.w * field, cmd.w * field);
                break;
            }
            case "line":
            {
                var pen = new Pen(fill, Math.Max(1, cmd.w * field)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                dc.DrawLine(pen, At(c, field, cmd.r, cmd.a), At(c, field, cmd.r2, cmd.a2));
                break;
            }
            case "arc":
            {
                dc.DrawGeometry(fill, stroke, AnnulusSegment(c, field, cmd.r, cmd.r2, cmd.a, cmd.a2));
                break;
            }
            case "poly":
            {
                var pts = cmd.p!;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(At(c, field, pts[0], pts[1]), isFilled: true, isClosed: true);
                    for (int i = 2; i < pts.Length; i += 2)
                        ctx.LineTo(At(c, field, pts[i], pts[i + 1]), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(fill, stroke, g);
                break;
            }
            case "text":
            {
                var p = At(c, field, cmd.r, cmd.a);
                var ft = ArcadeChrome.Text(cmd.s!, Math.Max(6, cmd.w * field), fill, ppd);
                dc.DrawText(ft, new Point(p.X, p.Y - ft.Height / 2));
                break;
            }
        }
    }

    /// <summary>Annulus segment between radii r0..r1 and angles a0..a1 (screen degrees). A sweep
    /// of 360° or more is drawn as a full ring.</summary>
    private static Geometry AnnulusSegment(Point c, double field, double r0, double r1, double a0, double a1)
    {
        double sweep = a1 - a0;
        double inner = r0 * field, outer = Math.Max(r1 * field, 0.5);
        if (Math.Abs(sweep) >= 360)
        {
            var outerE = new EllipseGeometry(c, outer, outer);
            if (inner <= 0) { outerE.Freeze(); return outerE; }
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude,
                outerE, new EllipseGeometry(c, inner, inner));
            ring.Freeze();
            return ring;
        }

        if (sweep < 0) { (a0, a1) = (a1, a0); sweep = -sweep; }
        bool large = sweep > 180;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            var o0 = At(c, field, r1, a0);
            var o1 = At(c, field, r1, a1);
            ctx.BeginFigure(o0, isFilled: true, isClosed: true);
            ctx.ArcTo(o1, new Size(outer, outer), 0, large, SweepDirection.Clockwise, true, false);
            if (inner > 0)
            {
                var i1 = At(c, field, r0, a1);
                var i0 = At(c, field, r0, a0);
                ctx.LineTo(i1, true, false);
                ctx.ArcTo(i0, new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, false);
            }
            else
            {
                ctx.LineTo(c, true, false);
            }
        }
        g.Freeze();
        return g;
    }

    // ── The gave-up card ─────────────────────────────────────────────────────
    // Guard-card vocabulary: plain wording, names the way out, offers nothing else — a sandboxed
    // game that keeps dying is not something the player can fix from the couch, so no nudge.

    private static readonly Brush CardScrim = Frozen(Color.FromArgb(0xE2, 0x06, 0x08, 0x0D));

    private static Brush Frozen(Color color) { var b = new SolidColorBrush(color); b.Freeze(); return b; }

    private static void DrawFaultCard(DrawingContext dc, Point c, double field, double ppd)
    {
        dc.DrawEllipse(CardScrim, null, c, field, field);
        double y = c.Y - field * 0.28;
        y += ArcadeChrome.DrawCentered(dc, "THIS GAME HIT A PROBLEM", Math.Max(12, field * 0.085),
            ArcadeChrome.Ink, c.X, y, ppd, field * 1.5) + field * 0.07;
        y += ArcadeChrome.DrawCentered(dc,
            "Its script stopped responding and was shut down. The game's saved progress is kept.",
            Math.Max(9, field * 0.055), ArcadeChrome.InkDim, c.X, y, ppd, field * 1.35) + field * 0.12;
        ArcadeChrome.DrawCentered(dc,
            $"{ControllerButtons.Text(PadButton.Circle)}  CLOSE",
            Math.Max(9, field * 0.06), ArcadeChrome.Ink, c.X, y, ppd, field * 1.2);
    }
}
