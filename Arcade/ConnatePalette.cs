using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// Frozen cream, indigo, blue, red, white, black, and blue-grey garbage palette for Connate.
/// Keeping construction here prevents per-frame brush churn and makes contrast changes reviewable in one place.
/// </summary>
internal static class ConnatePalette
{
    private static SolidColorBrush B(byte r, byte g, byte b, byte a = 255) => ArcadePalette.Fill(r, g, b, a);
    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Pen P(Brush brush, double width)
    {
        var pen = new Pen(brush, width);
        pen.Freeze();
        return pen;
    }

    // Environment and rule communication.
    /// <summary>Inside the limit ring: the safe zone, this blue-grey. Hue on this board belongs to the
    /// tiles; the ground doesn't compete for it.</summary>
    public static readonly Brush Well = B(0x32, 0x36, 0x4D);
    /// <summary>Everything outside the limit ring, much darker than the safe zone inside it: dark enough
    /// that a tile drifting out is obviously somewhere it shouldn't be.</summary>
    public static readonly Brush Outfield = B(0x12, 0x14, 0x1F);
    /// <summary>The dark red that floods the outfield over the fuse's last third. Deep enough to read as the
    /// ground going bad rather than as a glow, and distinct from <see cref="OverLimitInk"/> — a strayed tile's
    /// warning orange must not be mistaken for the verdict this colour delivers.</summary>
    public static readonly Brush Doom = B(0x6E, 0x14, 0x14);
    public static readonly Brush GravityGlow = B(0x4B, 0x52, 0x68);
    /// <summary>Sage at 40%: the gravity rings wear the game's own accent.</summary>
    public static readonly Brush GravityLine = B(0x81, 0xB2, 0x9A, 0x66);
    public static readonly Brush Ink = B(0xF2, 0xEE, 0xE3);
    public static readonly Brush InkDim = B(0xFF, 0xFF, 0xFF);
    public static readonly Brush Aim = B(0x81, 0xB2, 0x9A);
    /// <summary>Cream at 60%.</summary>
    public static readonly Brush Limit = B(0xF2, 0xEE, 0xE3, 0x99);
    public static readonly Brush Danger = B(0xFF, 0xFF, 0xFF);

    // ── The fuse ramp ──
    // The danger ring sweeps white → orange → red as it fills, so how much trouble you're in is readable
    // from colour alone at the edge of vision — the arc's length says the same thing, but you have to look
    // at it to read it, and by then you were already looking.
    public static readonly Color FuseCalm = Rgb(0xFF, 0xFF, 0xFF);
    public static readonly Color FuseWarn = Rgb(0xF0, 0x8A, 0x2E);
    public static readonly Color FuseDire = Rgb(0xD8, 0x34, 0x2A);

    /// <summary>Fuse colour at <paramref name="t"/> (0..1). Two straight lerps rather than one three-stop
    /// gradient: the midpoint has to land on the orange, since that's the beat where the ring stops being
    /// information and starts being a warning.</summary>
    public static Color FuseColour(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t < 0.5 ? Lerp(FuseCalm, FuseWarn, t * 2) : Lerp(FuseWarn, FuseDire, (t - 0.5) * 2);
    }

    private static Color Lerp(Color a, Color b, double t) => ArcadePalette.LerpRgb(a, b, t);
    public static readonly Brush Merge = B(0xFF, 0xFF, 0xFF);
    /// <summary>The field colour at 87%.</summary>
    public static readonly Brush Scrim = B(0x3C, 0x40, 0x5B, 0xDD);
    /// <summary>The HUD plate hanging in from the top of the disc (next tile, score, bomb meter) — the same
    /// near-black plaque Internode and Kabloom hang, so the three HUDs read as one family.</summary>
    public static readonly Brush Plaque = B(0x12, 0x16, 0x26, 0xE0);
    public static readonly Pen PlaquePen = Frozen(new Pen(B(0x6E, 0x75, 0x90), 1.0));
    // Player craft. Names retain the original art roles even though the restricted palette uses cream/sage.
    public static readonly Brush CraftBronze = B(0xF2, 0xEE, 0xE3);
    public static readonly Brush CraftDark = B(0x00, 0x00, 0x00);
    public static readonly Brush CraftTeal = B(0x81, 0xB2, 0x9A);
    public static readonly Brush CraftLight = B(0xFF, 0xFF, 0xFF);
    // No drop-shadow brush, deliberately: this board is a cel drawing, and its depth comes from the flat
    // black outline and the glass rim, not from lighting.
    // Garbage must read as inert obstruction rather than another rank; its darker values sit outside rank fills.
    public static readonly Brush Garbage = B(0x25, 0x2B, 0x3D);
    public static readonly Brush GarbageInset = B(0x3C, 0x40, 0x5B);
    public static readonly Brush GarbageGlint = B(0x59, 0x60, 0x78);
    /// <summary>Cel outline for garbage, the same ink as every other element's stroke, so garbage sits
    /// inside the drawn look rather than beside it.</summary>
    public static readonly Pen GarbagePen = P(B(0x0B, 0x0D, 0x14), CelWidth);

    // ── Tile families ────────────────────────────────────────────────────────
    // Two clearly separated hues, because colour is a rule, not decoration: only same-family tiles merge, so
    // a merge gate has to be readable at a glance.
    //
    // Within a family, rank is carried by lightness: a shared hue keeps "can these two combine" answerable at
    // a glance across the board, while the value ladder stays legible up close. The numeral is still the
    // authority; this only has to make the pairing obvious.
    //
    // ⚠ The families are blue and red. The deep end of the blue ladder is kept saturated because both the
    // safe-zone disc and the field beyond the ring are blue-greys — a desaturated rank-8 blue sinks into
    // them, which is the pairing to check first if these values ever move.
    private static readonly Brush[] AzureFamily =
    [
        B(0xBF, 0xD3, 0xF2), B(0xA2, 0xBE, 0xEC), B(0x85, 0xA8, 0xE4), B(0x6E, 0x90, 0xD2),
        B(0x5A, 0x79, 0xBB), B(0x4A, 0x65, 0xA4), B(0x3C, 0x54, 0x8C), B(0x31, 0x46, 0x77),
    ];
    private static readonly Brush[] EmberFamily =
    [
        B(0xF0, 0xB4, 0x9F), B(0xEA, 0x97, 0x81), B(0xE0, 0x7A, 0x5F), B(0xC8, 0x66, 0x48),
        B(0xAC, 0x54, 0x39), B(0x8E, 0x44, 0x2E), B(0x74, 0x38, 0x26), B(0x5E, 0x2E, 0x1F),
    ];

    /// <summary>Fill for a numbered tile. <paramref name="hue"/> is <see cref="ConnateRules.HueAzure"/> or
    /// <see cref="ConnateRules.HueEmber"/>; anything else falls back to azure rather than throwing, since a
    /// renderer must never be the thing that takes the app down.</summary>
    public static Brush Tile(int rank, int hue)
    {
        Brush[] family = hue == ConnateRules.HueEmber ? EmberFamily : AzureFamily;
        return family[Math.Abs(rank) % family.Length];
    }

    /// <summary>The two family fills for a blended tile (C5/C6), light lobe first. A blended tile is drawn as
    /// both at once — see <c>ConnateRenderer.DrawBlendSwirl</c> — because it genuinely is both: it matches any
    /// tile of its number regardless of family, and a third invented colour would read as a third family.</summary>
    public static (Brush A, Brush B) BlendPair(int rank) =>
        (Tile(rank, ConnateRules.HueAzure), Tile(rank, ConnateRules.HueEmber));

    /// <summary>The cel-shading stroke. Every game element is outlined in flat black — tiles, garbage,
    /// bombs, the craft, the meter pips — which is what turns a set of soft filled shapes into one drawn
    /// scene. Width is per-element rather than fixed: a hairline on a big tile and a slab on a small one
    /// would read as two different art styles.</summary>
    /// <remarks>Quantised to a quarter pixel and cached: the rank pips on every tile ask for one every
    /// frame, and the widths come from a short ladder of ranks, so the table stays tiny.</remarks>
    public static Pen Cel(double thickness)
    {
        int key = (int)Math.Clamp(Math.Round(Math.Max(1.0, thickness) * 4), 4, 4000);
        if (_celPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(CelInk, key / 4.0)
        { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        if (_celPens.Count < 512) _celPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<int, Pen> _celPens = [];

    /// <summary>The cel stroke: one width for everything, rather than scaling with the element. A stroke
    /// that thickens with its shape is how an illustration reads; a stroke of constant weight is how a cel
    /// reads, because the ink is a property of the drawing, not of the thing drawn.</summary>
    public const double CelWidth = 2.0;

    /// <summary>The colour a tile's outline pulses to while it sits outside the limit ring. Dark orange
    /// rather than red: red is what the fuse ring reaches at its very end, and a tile that has merely
    /// strayed is a warning rather than a verdict — the two should not look the same. Dark enough to stay
    /// distinct from the ember family's own fills, which are the tiles most likely to be wearing it.</summary>
    public static readonly Color OverLimitInk = Rgb(0xB8, 0x48, 0x0F);

    /// <summary>Outline for a tile outside the ring, pulsing black → dark orange → black.
    /// <paramref name="pulse"/> is 0..1.</summary>
    /// <remarks>⚠ Cached on a 32-step pulse. This is a per-tile-per-frame stroke and it arrives at the
    /// worst moment: a heap crowding the limit ring can have every tile wearing it at once. 32 steps put
    /// the colour lerp and the width ladder both well below anything visible.</remarks>
    public static Pen OverLimitPen(double pulse)
    {
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(pulse, 0, 1) * (OverLimitSteps - 1)), 0, OverLimitSteps - 1);
        if (_overLimitPens[step] is { } cached) return cached;
        pulse = step / (OverLimitSteps - 1.0);
        Color ink = Lerp(((SolidColorBrush)CelInk).Color, OverLimitInk, pulse);
        var brush = new SolidColorBrush(ink); brush.Freeze();
        // Slightly heavier than the standard cel stroke at the peak, so the warning reads as the outline
        // thickening as well as changing colour — colour alone is the first thing lost at couch distance.
        var pen = new Pen(brush, CelWidth * (1 + pulse * 0.55))
        { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return _overLimitPens[step] = pen;
    }

    private const int OverLimitSteps = 32;
    private static readonly Pen?[] _overLimitPens = new Pen?[OverLimitSteps];

    /// <summary>The stroke on tile numerals. Thinner than the shape stroke on purpose: glyphs already carry
    /// their own weight, and a 2px outline on a digit closes up the counters in 8, 6 and 0 at tile size — the
    /// outline is there to separate the number from the fill behind it, not to re-draw it.</summary>
    public const double LabelStrokeWidth = 1.25;

    // ⚠ CelInk must be declared before the pens. C# initialises static fields in source order, so a pen
    // built from a brush declared below it gets null and the compiler is right to warn.
    public static readonly Brush CelInk = B(0x0B, 0x0D, 0x14);
    public static readonly Pen CelPen = P(CelInk, CelWidth);
    public static readonly Pen LabelPen = P(CelInk, LabelStrokeWidth);

    /// <summary>Ink for a tile's value label — white, so one colour reads against every family tone. The
    /// black cel stroke around the glyphs is what makes that true on the pale end of a ramp.</summary>
    public static readonly Brush Pip = B(0xFF, 0xFF, 0xFF);


    /// <summary>Bombs (C1): black, no family, white glyph. They belong to nobody and act on everything
    /// equally, so they deliberately sit outside both ramps.</summary>
    public static readonly Brush BombBody = B(0x0E, 0x10, 0x18);
    public static readonly Brush BombGlyph = B(0xFF, 0xFF, 0xFF);
    /// <summary>The bomb's outline: cream, not the cel black. The body is already near-black, so a black
    /// stroke on it would be invisible and the bomb would lose its silhouette against a dark board — the one
    /// element where the cel rule has to bend to keep its own job.</summary>
    public static readonly Pen BombEdge = P(B(0xF2, 0xEE, 0xE3), CelWidth);

    /// <summary>The loaded bomb's fuse sparks, hot core cooling to amber. Warm yellow is the one colour the
    /// bomb is allowed to carry: the sparks sit on the art's own yellow flame, so they read as that flame
    /// spitting rather than as a third accent competing with the two tile families.</summary>
    public static readonly Color FuseSparkHot = Rgb(0xFF, 0xF7, 0xC8);
    public static readonly Color FuseSparkCool = Rgb(0xF2, 0xA2, 0x27);

    /// <summary><paramref name="t"/> is one spark's age — 0 leaving the flame, 1 at the end of its life.</summary>
    public static Color FuseSparkColour(double t) => Lerp(FuseSparkHot, FuseSparkCool, Math.Clamp(t, 0, 1));

    /// <summary>A spark's brush at age <paramref name="t"/> — colour and alpha together, because both are
    /// functions of the age alone.
    ///
    /// <para>⚠ Quantised and cached, not built per draw. Nine sparks plus the flame core are drawn every
    /// frame a bomb is loaded; a fresh <c>SolidColorBrush</c> each would be ~600 throwaway brushes a
    /// second, on a board that has to stay lean while a game outside it eats the machine.</para></summary>
    public static Brush FuseSpark(double t)
    {
        int step = (int)Math.Clamp(Math.Clamp(t, 0, 1) * (FuseSteps - 0.001), 0, FuseSteps - 1);
        if (_fuseSpark[step] is { } hit) return hit;
        double age = (double)step / (FuseSteps - 1);
        Color c = FuseSparkColour(age);
        var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(255 * (1 - age * age), 0, 255), c.R, c.G, c.B));
        b.Freeze();
        return _fuseSpark[step] = b;
    }

    /// <summary>The flame core's brush at <paramref name="strength"/> 0..1 — the flicker, quantised for the
    /// same reason as <see cref="FuseSpark"/>.</summary>
    public static Brush FuseGlow(double strength)
    {
        int step = (int)Math.Clamp(Math.Clamp(strength, 0, 1) * (FuseSteps - 0.001), 0, FuseSteps - 1);
        if (_fuseGlow[step] is { } hit) return hit;
        var b = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Clamp(FuseGlowPeakAlpha * step / (FuseSteps - 1.0), 0, 255),
            FuseSparkHot.R, FuseSparkHot.G, FuseSparkHot.B));
        b.Freeze();
        return _fuseGlow[step] = b;
    }

    /// <summary>The glass inner-rim pen, at the stroke width the caller wants.
    ///
    /// <para>⚠ Quantised to a quarter pixel and cached. <c>DrawGlass</c> runs for every tile on the board on
    /// every frame — a heap on a busy board is dozens of tiles — and a fresh <c>Pen</c> each would be the
    /// largest single allocation source in this renderer. A quarter pixel is far below what a soft inner rim
    /// can show, and tile sizes come from a short ladder of ranks anyway, so the table stays tiny.</para></summary>
    public static Pen RimPen(double thickness)
    {
        int key = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        if (_rimPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(EdgeBounce, key / 4.0) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        // Bounded by the rank ladder that feeds it, not by frames; the guard is for a pathological scale.
        if (_rimPens.Count < 512) _rimPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<int, Pen> _rimPens = [];

    /// <summary>The cream ink at <paramref name="alpha"/>, as a brush.
    ///
    /// <para>⚠ Alpha quantised to 16 levels and cached. Used by the things that fade — aim beads, trails —
    /// where the value changes every frame but only ever within one ramp.</para></summary>
    public static Brush InkBrush(byte alpha)
    {
        int step = alpha >> 4;
        if (_inkBrushes[step] is { } hit) return hit;
        var b = new SolidColorBrush(Color.FromArgb((byte)(step * 17), 0xF2, 0xEE, 0xE3));
        b.Freeze();
        return _inkBrushes[step] = b;
    }

    /// <summary>A stroke of that same ink, at a width quantised to a quarter pixel.
    ///
    /// <para>⚠ For the per-body effects. A motion trail is drawn for every young moving body every frame,
    /// and a bomb blast can set the whole heap moving at once — exactly the moment the frame budget is
    /// tightest.</para>
    ///
    /// <para>⚠ <paramref name="round"/> is not cosmetic where the same pen also strokes lines: a round cap
    /// adds half the width at each end, and a blast's sparks are only a couple of widths long, so turning it
    /// on by default lengthens them visibly. Ask for it where the stroke wants it.</para></summary>
    public static Pen InkPen(byte alpha, double thickness, bool round = false)
    {
        int step = alpha >> 4;
        int width = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        int key = step << 20 | (round ? 1 << 16 : 0) | width;
        if (_inkPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(InkBrush(alpha), width / 4.0);
        if (round) { pen.StartLineCap = PenLineCap.Round; pen.EndLineCap = PenLineCap.Round; }
        pen.Freeze();
        // 16 alphas across a short ladder of body radii; the guard is for a pathological scale.
        if (_inkPens.Count < 512) _inkPens[key] = pen;
        return pen;
    }

    private static readonly Brush?[] _inkBrushes = new Brush?[16];
    private static readonly Dictionary<int, Pen> _inkPens = [];

    /// <summary>A sage stroke at <paramref name="alpha"/> and a width quantised to a quarter pixel: the
    /// <see cref="Aim"/> family's answer to <see cref="InkPen"/>.
    ///
    /// <para>⚠ Two of its callers run per object per frame — a merge bond is drawn for every reserved pair,
    /// and a cascade reserves many at once, while each combo spark carries a ring. The rest are once-a-frame
    /// rings that pay for a <c>Pen</c> anyway. Round caps and joins, which every caller wants: closed rings
    /// and beads have no caps to show, and both line callers — the bomb trail and the merge bond — need
    /// them.</para></summary>
    public static Pen AimStroke(byte alpha, double thickness)
    {
        int step = alpha >> 4;
        int width = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        int key = step << 20 | width;
        if (_aimPens.TryGetValue(key, out var hit)) return hit;
        if (_aimBrushes[step] is not { } brush)
        {
            var made = new SolidColorBrush(Color.FromArgb((byte)(step * 17), 0x81, 0xB2, 0x9A));
            made.Freeze();
            brush = _aimBrushes[step] = made;
        }
        var pen = new Pen(brush, width / 4.0)
        { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        // 16 alphas across a short ladder of radii; the guard is for a pathological scale.
        if (_aimPens.Count < 512) _aimPens[key] = pen;
        return pen;
    }

    private static readonly Brush?[] _aimBrushes = new Brush?[16];
    private static readonly Dictionary<int, Pen> _aimPens = [];

    /// <summary>Pure white at <paramref name="alpha"/>. <see cref="Merge"/> and <see cref="BombGlyph"/> are
    /// this colour opaque; this is the same family when it has to fade.</summary>
    public static Brush WhiteBrush(byte alpha) => ArcadePalette.WhiteBrush(alpha);

    /// <summary>A white stroke at <paramref name="alpha"/>, width quantised to a quarter pixel.
    ///
    /// <para>⚠ Three callers run per object per frame and all three peak together: a merge flash ring per
    /// flash, a shockwave per combo burst, and a crack ring per garbage clear — which is precisely what a
    /// long cascade produces at once.</para>
    ///
    /// <para>⚠ Butt caps unless asked: the merge-flash pen also strokes that flash's sparks, which are a
    /// couple of widths long, so round caps would lengthen them. Same trap as <see cref="InkPen"/>.</para></summary>
    public static Pen WhitePen(byte alpha, double thickness, bool round = false) =>
        ArcadePalette.WhitePen(alpha, thickness, round);

    /// <summary>The white bar that sweeps across a blended tile, at its peak alpha.
    ///
    /// <para>⚠ Per blended tile per frame — a gradient brush plus its stop collection is five objects, and
    /// the sweep runs on every C5/C6 on the board at once. Relative mapping, so one brush per alpha serves
    /// every tile size; keyed on the byte, so the table can't exceed 256.</para></summary>
    public static Brush BlendShine(byte peak)
    {
        if (_blendShine[peak] is { } hit) return hit;
        var shine = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.0),
                new GradientStop(Color.FromArgb(peak, 0xFF, 0xFF, 0xFF), 0.5),
                new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0),
            },
        };
        shine.Freeze();
        return _blendShine[peak] = shine;
    }

    private static readonly Brush?[] _blendShine = new Brush?[256];

    /// <summary>The darkening that falls off toward the outfield rim, whose inner stop is where the safe disc
    /// ends. Relative mapping, so it is the stop alone that varies — quantised to 1/256 and cached, since the
    /// whole field paints one every frame.</summary>
    public static Brush RimFall(double innerStop)
    {
        int step = (int)Math.Clamp(Math.Round(Math.Clamp(innerStop, 0, 1) * 255), 0, 255);
        if (_rimFall[step] is { } hit) return hit;
        var fall = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, 0x0A, 0x0B, 0x12), step / 255.0),
                new GradientStop(Color.FromArgb(0xCC, 0x0A, 0x0B, 0x12), 1.0),
            },
        };
        fall.Freeze();
        return _rimFall[step] = fall;
    }

    private static readonly Brush?[] _rimFall = new Brush?[256];

    private const int FuseSteps = 16;
    private const double FuseGlowPeakAlpha = 150;
    private static readonly Brush?[] _fuseSpark = new Brush?[FuseSteps];
    private static readonly Brush?[] _fuseGlow = new Brush?[FuseSteps];

    // ── Glass ─────────────────────────────────────────────────────────────────
    // Flat colour inside a cel outline reads as printed on the board rather than sitting on it. These three
    // brushes borrow the wheel's Obsidian/Pearl vocabulary — a bowed horizon lit from 16° upper-left, a
    // specular sheen, and a bounce off the ground — and give it physicality without transparency: every
    // layer is a light or a shade painted on an opaque fill, so a tile still hides whatever is behind it.
    // (Transparency here would be actively wrong: overlapping tiles are the normal state of this board, and
    // a stack of see-through discs is unreadable.)
    //
    // ⚠ RelativeToBoundingBox, so one frozen brush serves every tile at every size — the geometry it's drawn
    // through supplies the extent. A per-tile absolute gradient would churn a brush per body per frame.

    /// <summary>The body gloss: sheen at the top, a soft horizon just past the middle, shade below it, and a
    /// faint bounce at the very bottom. The centre is off to the left, which is what puts the light 16° off
    /// vertical — the same angle Obsidian and Pearl are lit from, so the arcade and the wheel agree about
    /// where the light in this app comes from.</summary>
    public static readonly Brush Gloss = Frozen(new RadialGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        Center = new Point(0.357, 0.0), GradientOrigin = new Point(0.357, 0.0),
        RadiusX = 1.35, RadiusY = 1.0,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF), 0.00),
            new GradientStop(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), 0.16),
            new GradientStop(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), 0.46),
            new GradientStop(Color.FromArgb(0x1E, 0x00, 0x00, 0x00), 0.52),   // the horizon
            new GradientStop(Color.FromArgb(0x46, 0x00, 0x00, 0x00), 0.88),
            new GradientStop(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 1.00),   // bounce off the ground
        },
    });

    /// <summary>The specular: a small hard highlight where the light actually hits, distinct from the broad
    /// sheen above. A glossy surface has both — the gradient says "curved", the specular says "wet".</summary>
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

    /// <summary>The inner edge: dark along the lit top-left, bright along the bottom-right. That inversion is
    /// the whole trick — a thick object's rim catches light from the far side and shades where the light
    /// glances off it, and it's what separates glass from a sticker with a gradient on it. Stroked inside a
    /// clip of the tile's own shape, so it traces any silhouette (star, socket, garbage lump) for free.</summary>
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

    private static T Frozen<T>(T freezable) where T : Freezable => ArcadePalette.Frozen(freezable);

    public static readonly Pen GravityPen = P(GravityLine, 1);
    public static readonly Pen LimitPen = P(Limit, 1.4);
    public static readonly Pen AimPen = P(Aim, 1.6);
    /// <summary>The full-charge ring: sage, never gold (gold is value on its way to the counter).</summary>
    public static readonly Pen ChargeRing = P(Aim, 3.0);
    /// <summary>The craft's outline — cel black like the tiles it fires.</summary>
    public static readonly Pen CraftPen = P(CelInk, CelWidth);

    // ── Collected value ──────────────────────────────────────────────────────
    // Gold is reserved. Nothing on this board is gold except value on its way to the counter — not a tile
    // family, not the bomb, not a rule ring. That exclusivity is the whole effect: a burst of this colour can
    // only ever mean "you are being paid", so it needs no label and survives being glimpsed at couch distance.
    // ⚠ Don't spend it on anything else, however well it would look.

    /// <summary>The nugget body.</summary>
    public static readonly Brush Mote = B(0xEF, 0xB1, 0x3C);
    /// <summary>Its glint, and the tint the counter takes as value lands in it.</summary>
    public static readonly Brush MoteHot = B(0xFF, 0xF0, 0xBE);
    public static readonly Color MoteHotInk = Rgb(0xFF, 0xDE, 0x8A);
    /// <summary>Thinner than the shape cel stroke: at nugget size the standard 2px would be most of the
    /// object. This is the numeral weight, which is the right one for something this small.</summary>
    public static readonly Pen MoteEdge = P(CelInk, LabelStrokeWidth);

    /// <summary>The warm bloom a nugget carries. RelativeToBoundingBox, so one frozen brush serves every
    /// nugget at every size rather than churning a gradient per mote per frame.</summary>
    public static readonly Brush MoteGlow = Frozen(new RadialGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x9E, 0xFF, 0xD1, 0x6A), 0.00),
            new GradientStop(Color.FromArgb(0x44, 0xF0, 0xAE, 0x33), 0.42),
            new GradientStop(Color.FromArgb(0x00, 0xE0, 0x92, 0x1E), 1.00),
        },
    });

    /// <summary>Counter ink, cream at rest and gold at the instant value lands in it. Allocated only while a
    /// nugget is actually arriving; the rest of the time the shared <see cref="Ink"/> brush is returned.</summary>
    public static Brush ScoreInk(double pulse)
    {
        pulse = Math.Clamp(pulse, 0, 1);
        if (pulse <= 0.02) return Ink;
        int step = (int)Math.Clamp(Math.Round(pulse * 15), 0, 15);
        if (_scoreInks[step] is { } hit) return hit;
        var brush = new SolidColorBrush(Lerp(((SolidColorBrush)Ink).Color, MoteHotInk, step / 15.0));
        brush.Freeze();
        return _scoreInks[step] = brush;
    }

    private static readonly Brush?[] _scoreInks = new Brush?[16];
}
