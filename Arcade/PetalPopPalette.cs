using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Petalpop's frozen brushes and pens. Built with explicit <c>(r, g, b, a)</c> bytes rather than
/// hex literals — the arcade's <c>#AARRGGBB</c> trap (docs/ARCADE.md) can't happen when alpha is a named
/// argument. Names are roles, never colours: a brush used as both a fill and an ink is eventually wrong in one.
///
/// <para>The look: a deep ink-blue ground inside the pentagon, near-black gutters with a teal breath along
/// the rails, brass paddles, a pearl ball, and a flower of petals graded blush → plum toward a gold core.
/// Gold is reserved for the core — nothing else on the board wears it.</para></summary>
internal static class PetalPopPalette
{
    private static SolidColorBrush B(byte r, byte g, byte b, byte a = 255) => ArcadePalette.Fill(r, g, b, a);
    private static Pen P(Brush brush, double width, PenLineCap cap = PenLineCap.Round) => ArcadePalette.RoundPen(brush, width, cap);
    private static T Frozen<T>(T f) where T : Freezable => ArcadePalette.Frozen(f);

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    public static Color Lerp(Color a, Color b, double t) => ArcadePalette.Lerp(a, b, t);
    /// <summary>A frozen fill in <paramref name="c"/> at <paramref name="alpha"/>, cached.
    ///
    /// <para>⚠ Called per petal, per ball, per gem and per shard, every frame — the petal fill alone is one
    /// per cell. Alpha is quantised to 32 steps, and the colours arrive already byte-quantised by the lerps
    /// that make them, so the table is bounded by the palette rather than by frames.</para></summary>
    public static SolidColorBrush Solid(Color c, double alpha = 1) => ArcadePalette.Solid(c, alpha);

    /// <summary>A round-capped stroke in <paramref name="c"/>, cached on the same terms as <see cref="Solid"/>
    /// with the width quantised to a quarter pixel.
    ///
    /// <para>⚠ Every petal's cel outline comes through here, as do its veins and cracks and the rings around
    /// the balls and gems — all per object per frame.</para></summary>
    public static Pen Stroke(Color c, double width, double alpha = 1) => ArcadePalette.Stroke(c, width, alpha);

    private const int AlphaSteps = ArcadePalette.AlphaSteps;

    // ── Ground ────────────────────────────────────────────────────────────────
    /// <summary>Inside the pentagon.</summary>
    public static readonly Brush Ground = B(0x1B, 0x21, 0x40);
    /// <summary>The gutter lenses between the rails and the rim.</summary>
    public static readonly Brush Gutter = B(0x0B, 0x0D, 0x16);
    /// <summary>The breath of teal along a rail's gutter side.</summary>
    public static readonly Color RailGlow = Rgb(0x3F, 0xB8, 0xA8);
    /// <summary>The flash a gutter takes when a ball falls through it.</summary>
    public static readonly Color GutterLost = Rgb(0xE0, 0x5A, 0x4F);
    public static readonly Color CelInk = Rgb(0x0B, 0x0D, 0x14);
    public const double CelWidth = 2.0;
    public const double LabelStrokeWidth = 1.25;
    public static readonly Pen CelPen = P(B(0x0B, 0x0D, 0x14), CelWidth);
    public static readonly Pen RailPen = P(B(0x0B, 0x0D, 0x14), 2.4);
    public static readonly Pen FineCel = P(B(0x0B, 0x0D, 0x14), LabelStrokeWidth);

    // ── The flower ────────────────────────────────────────────────────────────
    /// <summary>Petal fill per ring, index 0 = the ring touching the core. Graded plum → blush outward, so the
    /// flower reads as one bloom and depth reads as saturation.</summary>
    public static readonly Color[] Petal =
    [
        Rgb(0x8D, 0x3B, 0x79),   // plum
        Rgb(0xD9, 0x4F, 0x8A),   // magenta
        Rgb(0xEE, 0x7A, 0x8E),   // coral pink
        Rgb(0xF4, 0xA7, 0xB9),   // blush
        Rgb(0xF8, 0xC6, 0xD2),   // beyond four rings: paler still
        Rgb(0xFA, 0xDC, 0xE4), Rgb(0xFC, 0xE8, 0xEE), Rgb(0xFD, 0xF0, 0xF4),
    ];
    public static Color PetalColour(int ring) => Petal[Math.Clamp(ring, 0, Petal.Length - 1)];
    /// <summary>A tough petal is the same hue, deeper for every extra hit it will take — it reads as thicker,
    /// not as a different flower.</summary>
    public static Color ToughColour(int ring, int hp) => Lerp(PetalColour(ring), Rgb(0x2A, 0x10, 0x28), Math.Min(0.55, 0.26 * (hp - 1)));
    /// <summary>The vein down a tough petal and the cracks across a chipped one.</summary>
    public static readonly Color PetalVein = Rgb(0xFF, 0xF4, 0xF8);
    public static readonly Color Crack = Rgb(0x1A, 0x0C, 0x18);

    // ── Core ──────────────────────────────────────────────────────────────────
    // Gold is reserved for the core: the payoff, the one thing the whole flower is peeled toward.
    public static readonly Color Core = Rgb(0xF2, 0xB8, 0x4B);
    public static readonly Color CoreHot = Rgb(0xFF, 0xE2, 0x9A);
    public static readonly Brush CoreFill = B(0xF2, 0xB8, 0x4B);

    // ── Paddles ───────────────────────────────────────────────────────────────
    // Wood: a warm mid brown with a darker grain drawn along the slab, so the paddles read as planks and
    // sit apart from the gold core and the brass-to-orange charge tiers.
    public static readonly Color Paddle = Rgb(0x8E, 0x5B, 0x33);
    public static readonly Color PaddleArmed = Rgb(0xD8, 0xB0, 0x86);
    public static readonly Brush PaddleFill = B(0x8E, 0x5B, 0x33);
    public static readonly Brush PaddleArmedFill = B(0xD8, 0xB0, 0x86);
    /// <summary>The grain lines laid along a paddle, and the lighter streak between them.</summary>
    public static readonly Color PaddleGrain = Rgb(0x4E, 0x2E, 0x18);
    public static readonly Color PaddleGrainLight = Rgb(0xB0, 0x7A, 0x4C);
    /// <summary>The windup's charge tiers, worn by the paddle body: +1 punch is a warm yellow-orange, +2 a
    /// red-orange that pulses. Distinct from the brass rest colour at couch distance, and from each other.</summary>
    public static readonly Color PaddleCharge1 = Rgb(0xF4, 0xB0, 0x3C);
    public static readonly Color PaddleCharge2 = Rgb(0xF0, 0x58, 0x2A);
    public static readonly Color PaddleCharge2Hot = Rgb(0xFF, 0x8C, 0x4A);

    // ── Ball and gem ──────────────────────────────────────────────────────────
    public static readonly Color Ball = Rgb(0xF7, 0xF3, 0xEA);
    public static readonly Color BallHot = Rgb(0xFF, 0xFF, 0xFF);
    /// <summary>A charged ball's body tends toward this orange as its +punch stock grows: a little at one,
    /// fully at the cap. The stock is also the ball's own shade, not only its halo and sparks.</summary>
    public static readonly Color BallCharge = Rgb(0xFF, 0xA8, 0x3C);
    /// <summary>The parked ball's flash through the shot clock's last second.</summary>
    public static readonly Color BallWarn = Rgb(0xFF, 0x7A, 0x1F);
    public static readonly Color Trail = Rgb(0xFF, 0xB3, 0x5C);
    public static readonly Color TrailHot = Rgb(0xFF, 0xF6, 0xE0);
    public static readonly Brush BallFill = B(0xF7, 0xF3, 0xEA);
    public static readonly Brush BallHotFill = B(0xFF, 0xFF, 0xFF);
    /// <summary>The split gem: teal-mint, the board's accent. Never a disc — a faceted cut, so it can't be
    /// mistaken for a ball.</summary>
    public static readonly Color Gem = Rgb(0x5F, 0xE0, 0xD0);
    public static readonly Brush GemFill = B(0x5F, 0xE0, 0xD0);
    public static readonly Brush Accent = B(0x4F, 0xC9, 0xB8);
    public static readonly Color AccentInk = Rgb(0x4F, 0xC9, 0xB8);

    // ── Text ──────────────────────────────────────────────────────────────────
    public static readonly Brush Ink = B(0xF2, 0xEE, 0xE3);
    public static readonly Color InkColour = Rgb(0xF2, 0xEE, 0xE3);
    public static readonly Brush InkDim = B(0xB8, 0xB3, 0xA6);
    public static readonly Brush Danger = B(0xE0, 0x5A, 0x4F);
    public static readonly Color ShoutCombo = Rgb(0xFF, 0xD2, 0x6B);
    public static readonly Color ShoutSmash = Rgb(0xFF, 0xFF, 0xFF);
    public static readonly Color ShoutLevel = Rgb(0x8F, 0xF0, 0xE0);
    /// <summary>Behind the intro and game-over cards: the ground colour at 87%.</summary>
    public static readonly Brush Scrim = B(0x14, 0x1A, 0x33, 0xDD);

    // ── Glass ─────────────────────────────────────────────────────────────────
    // The Obsidian/Pearl vocabulary the wheel and Connate share: a bowed sheen, a hard specular, and an inner
    // edge that shades where the light lands and brightens on the far rim. RelativeToBoundingBox so one
    // frozen brush serves every petal, paddle, ball and gem at every size.
    public static readonly Brush Gloss = Frozen(new LinearGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        StartPoint = new Point(0.35, 0.0), EndPoint = new Point(0.65, 1.0),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x5C, 0xFF, 0xFF, 0xFF), 0.00),
            new GradientStop(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), 0.16),
            new GradientStop(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), 0.46),
            new GradientStop(Color.FromArgb(0x1E, 0x00, 0x00, 0x00), 0.52),
            new GradientStop(Color.FromArgb(0x46, 0x00, 0x00, 0x00), 0.88),
            new GradientStop(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 1.00),
        },
    });

    public static readonly Brush Specular = Frozen(new RadialGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0xC4, 0xFF, 0xFF, 0xFF), 0.00),
            new GradientStop(Color.FromArgb(0x5A, 0xFF, 0xFF, 0xFF), 0.55),
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.00),
        },
    });

    public static readonly Brush EdgeBounce = Frozen(new LinearGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        StartPoint = new Point(0.28, 0.0), EndPoint = new Point(0.72, 1.0),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x3C, 0x00, 0x00, 0x00), 0.00),
            new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0.42),
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.58),
            new GradientStop(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF), 1.00),
        },
    });

    /// <summary>The warm bloom behind a ball and the mint bloom behind a gem.
    ///
    /// <para>⚠ Cached on a 32-step alpha: a gradient brush plus its stop collection is half a dozen objects,
    /// and the multiball petals, the balls and the core all breathe one every frame.</para></summary>
    public static Brush Halo(Color c, double alpha)
    {
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(alpha, 0, 1) * AlphaSteps), 0, AlphaSteps);
        var key = (c, step);
        if (_halos.TryGetValue(key, out var hit)) return hit;
        double a = (double)step / AlphaSteps;
        var brush = Frozen(new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(0xA0 * a), c.R, c.G, c.B), 0.00),
                new GradientStop(Color.FromArgb((byte)(0x40 * a), c.R, c.G, c.B), 0.45),
                new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1.00),
            },
        });
        if (_halos.Count < 512) _halos[key] = brush;
        return brush;
    }

    private static readonly Dictionary<(Color, int), Brush> _halos = [];

    /// <summary>A gradient brush at a fraction of its alpha — every stop scaled — cached on the same 32-step
    /// ladder as <see cref="Halo"/>. This is how a petal wears a quarter of the glass: folding the strength
    /// into the brush instead of a <c>PushOpacity</c> around three draws, which was a transparency layer per
    /// petal per frame, ninety-six of them on a late board.</summary>
    public static Brush Faded(Brush brush, double strength) => ArcadePalette.Faded(brush, strength);

    /// <summary>The rim stroke at a fraction of its alpha; see <see cref="Faded"/>.</summary>
    public static Pen RimPen(double thickness, double strength)
    {
        if (strength >= 0.999) return RimPen(thickness);
        int t = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(strength, 0, 1) * AlphaSteps), 0, AlphaSteps);
        var key = (t, step);
        if (_fadedRimPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(Faded(EdgeBounce, (double)step / AlphaSteps), t / 4.0) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        if (_fadedRimPens.Count < 512) _fadedRimPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<(int, int), Pen> _fadedRimPens = [];

    /// <summary>The glass inner-rim stroke, at the width the caller wants, quantised to a quarter pixel and
    /// cached.
    ///
    /// <para>⚠ <c>DrawGlass</c> runs for every petal, ball, paddle and core on every frame, so a fresh
    /// <c>Pen</c> here was a per-object-per-frame allocation. A quarter pixel is far below what a soft inner
    /// rim can show, and the sizes come from a short ladder of element kinds, so the table stays small.</para></summary>
    public static Pen RimPen(double thickness)
    {
        int key = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        if (_rimPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(EdgeBounce, key / 4.0) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        // Bounded by the element ladder, not by frames; the guard is for a pathological scale.
        if (_rimPens.Count < 512) _rimPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<int, Pen> _rimPens = [];
}
