using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>The drawing-side helpers every arcade palette and renderer shares: freezing, brush and pen
/// construction, the quantised brush and pen caches, colour interpolation and the polar-to-screen convention
/// (0 = 12 o'clock, clockwise). Each game's palette keeps thin wrappers under its own names; they delegate
/// here so there is one body per formula.</summary>
internal static class ArcadePalette
{
    public static T Frozen<T>(T freezable) where T : Freezable { freezable.Freeze(); return freezable; }

    /// <summary>A frozen solid brush from explicit bytes. Alpha is its own trailing argument rather than the
    /// head of an eight-digit hex literal, where <c>#AARRGGBB</c> read as <c>#RRGGBBAA</c> parses cleanly to
    /// the wrong opacity.</summary>
    public static SolidColorBrush Fill(byte r, byte g, byte b, byte a = 255) =>
        Frozen(new SolidColorBrush(Color.FromArgb(a, r, g, b)));

    /// <summary>A frozen pen with round joins and <paramref name="cap"/> at both ends.</summary>
    public static Pen RoundPen(Brush brush, double width, PenLineCap cap = PenLineCap.Round) =>
        Frozen(new Pen(brush, width) { LineJoin = PenLineJoin.Round, StartLineCap = cap, EndLineCap = cap });

    /// <summary>The alpha ladder <see cref="Solid"/>, <see cref="Stroke"/> and <see cref="Faded"/> quantise to.</summary>
    public const int AlphaSteps = 32;

    /// <summary>A frozen fill in <paramref name="c"/>'s RGB at <paramref name="alpha"/>, cached on the
    /// <see cref="AlphaSteps"/> ladder.
    ///
    /// <para>⚠ Called per object per frame (every petal, orb, fading ring). The colours arrive already
    /// byte-quantised by the lerps that make them, so the table is bounded by the palettes rather than by
    /// frames.</para></summary>
    public static SolidColorBrush Solid(Color c, double alpha = 1)
    {
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(alpha, 0, 1) * AlphaSteps), 0, AlphaSteps);
        var key = (c, step);
        if (_solids.TryGetValue(key, out var hit)) return hit;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * step / AlphaSteps), c.R, c.G, c.B));
        brush.Freeze();
        if (_solids.Count < 4096) _solids[key] = brush;
        return brush;
    }

    /// <summary>A round-capped, round-joined stroke, cached on the same terms as <see cref="Solid"/> with the
    /// width quantised to a quarter pixel.</summary>
    public static Pen Stroke(Color c, double width, double alpha = 1)
    {
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(alpha, 0, 1) * AlphaSteps), 0, AlphaSteps);
        int quarter = (int)Math.Clamp(Math.Round(width * 4), 1, 4000);
        var key = (c, step << 16 | quarter);
        if (_strokes.TryGetValue(key, out var hit)) return hit;
        var pen = RoundPen(Solid(c, alpha), quarter / 4.0);
        if (_strokes.Count < 4096) _strokes[key] = pen;
        return pen;
    }

    private static readonly Dictionary<(Color, int), SolidColorBrush> _solids = [];
    private static readonly Dictionary<(Color, int), Pen> _strokes = [];

    /// <summary>A frozen fill in <paramref name="c"/>'s RGB at exactly <paramref name="alpha"/>, cached on the
    /// bytes themselves. For the paints whose alpha must not step onto <see cref="Solid"/>'s ladder.</summary>
    public static SolidColorBrush ExactSolid(Color c, byte alpha)
    {
        var key = Color.FromArgb(alpha, c.R, c.G, c.B);
        if (_exactSolids.TryGetValue(key, out var hit)) return hit;
        var brush = Frozen(new SolidColorBrush(key));
        if (_exactSolids.Count >= 4096) _exactSolids.Clear();
        _exactSolids[key] = brush;
        return brush;
    }

    /// <summary>A butt-capped, mitre-joined pen in <see cref="ExactSolid"/>'s fill at exactly
    /// <paramref name="width"/>, cached on the exact width.
    ///
    /// <para>The widths it serves animate on the sim's fixed step, so they repeat from one play of an effect
    /// to the next and the table hits; it is cleared when full rather than grown.</para></summary>
    public static Pen ExactPen(Color c, byte alpha, double width)
    {
        var key = (Color.FromArgb(alpha, c.R, c.G, c.B), width);
        if (_exactPens.TryGetValue(key, out var hit)) return hit;
        var pen = Frozen(new Pen(ExactSolid(c, alpha), width));
        if (_exactPens.Count >= 4096) _exactPens.Clear();
        _exactPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<Color, SolidColorBrush> _exactSolids = [];
    private static readonly Dictionary<(Color, double), Pen> _exactPens = [];

    /// <summary>A brush at a fraction of its alpha, every gradient stop scaled, cached on the
    /// <see cref="AlphaSteps"/> ladder. Carries a fade in the brush instead of a <c>PushOpacity</c> layer per
    /// object per frame. A brush that is neither solid nor a gradient comes back unchanged.</summary>
    public static Brush Faded(Brush brush, double alpha)
    {
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(alpha, 0, 1) * AlphaSteps), 0, AlphaSteps);
        if (step >= AlphaSteps) return brush;
        var key = (brush, step);
        if (_faded.TryGetValue(key, out var hit)) return hit;
        double a = (double)step / AlphaSteps;
        Brush result = brush switch
        {
            GradientBrush g => ScaleStops(g, a),
            SolidColorBrush s => Frozen(new SolidColorBrush(Color.FromArgb((byte)(s.Color.A * a), s.Color.R, s.Color.G, s.Color.B))),
            _ => brush,
        };
        if (_faded.Count < 512) _faded[key] = result;
        return result;
    }

    private static Brush ScaleStops(GradientBrush g, double a)
    {
        var clone = (GradientBrush)g.Clone();
        var stops = new GradientStopCollection();
        foreach (var s in g.GradientStops)
            stops.Add(new GradientStop(Color.FromArgb((byte)(s.Color.A * a), s.Color.R, s.Color.G, s.Color.B), s.Offset));
        clone.GradientStops = stops;
        return Frozen(clone);
    }

    private static readonly Dictionary<(Brush, int), Brush> _faded = [];

    /// <summary>Pure white at <paramref name="alpha"/>, quantised to 16 levels and cached.</summary>
    public static Brush WhiteBrush(byte alpha)
    {
        int step = alpha >> 4;
        if (_whiteBrushes[step] is { } hit) return hit;
        var b = new SolidColorBrush(Color.FromArgb((byte)(step * 17), 0xFF, 0xFF, 0xFF));
        b.Freeze();
        return _whiteBrushes[step] = b;
    }

    /// <summary>A white stroke at <paramref name="alpha"/> (16 levels), width quantised to a quarter pixel,
    /// cached.
    ///
    /// <para>⚠ Butt caps and mitred joins unless <paramref name="round"/>: the same pen strokes sparks only a
    /// couple of widths long, and a round cap adds half the width at each end, enough to lengthen them
    /// visibly.</para></summary>
    public static Pen WhitePen(byte alpha, double thickness, bool round = false)
    {
        int step = alpha >> 4;
        int width = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        int key = step << 20 | (round ? 1 << 16 : 0) | width;
        if (_whitePens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(WhiteBrush(alpha), width / 4.0);
        if (round)
        {
            pen.LineJoin = PenLineJoin.Round;
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
        }
        pen.Freeze();
        if (_whitePens.Count < 512) _whitePens[key] = pen;
        return pen;
    }

    private static readonly Brush?[] _whiteBrushes = new Brush?[16];
    private static readonly Dictionary<int, Pen> _whitePens = [];

    /// <summary>Interpolate all four channels, <paramref name="t"/> clamped to [0, 1].</summary>
    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t),
                              (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Interpolate RGB only to an opaque colour, <paramref name="t"/> as given — the form the
    /// Connate palette and the picker use for their ramps.</summary>
    public static Color LerpRgb(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    /// <summary>A point <paramref name="radius"/> from <paramref name="c"/> at <paramref name="angle"/> radians,
    /// 0 straight up and increasing clockwise — the screen convention every game draws in.</summary>
    public static Point Polar(Point c, double radius, double angle) =>
        new(c.X + Math.Sin(angle) * radius, c.Y - Math.Cos(angle) * radius);
}
