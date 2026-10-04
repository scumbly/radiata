using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Frozen cream, indigo, sage, white, and black palette for Kabloom.</summary>
internal static class KabloomPalette
{
    private static SolidColorBrush B(byte r, byte g, byte b, byte a = 255) => ArcadePalette.Fill(r, g, b, a);

    private static Pen P(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    public static readonly Brush Disc = B(0x3C, 0x40, 0x5B);
    public static readonly Brush DiscGlow = B(0x50, 0x55, 0x72);
    public static readonly Brush CoveredA = B(0xF2, 0xEE, 0xE3);
    public static readonly Brush CoveredB = B(0xE2, 0xDE, 0xD3);
    public static readonly Brush Revealed = B(0x3C, 0x40, 0x5B);
    /// <summary>The exposed side of a raised covered petal, below its face.</summary>
    public static readonly Brush PetalSide = B(0x29, 0x1D, 0x1F);
    /// <summary>The covered-petal colour sweep, centre to rim: honey over the middle of the crop, violet toward
    /// its edge, clear between. Kept faint — it tints the petal art, it doesn't repaint it.</summary>
    public static readonly Color PetalTintCentre = Color.FromArgb(0x29, 0xFF, 0xE0, 0x50);
    public static readonly Color PetalTintRim = Color.FromArgb(0x30, 0x8C, 0x5C, 0xE0);
    public static readonly Brush Edge = B(0x00, 0x00, 0x00);
    /// <summary>The analog cursor: a small black dot with a pale ring so it survives a dark petal.</summary>
    public static readonly Brush PointerInk = B(0x0A, 0x0C, 0x14);
    /// <summary>The reticle's outer ring: the sage green the rest of the game uses for its own marks
    /// (<see cref="Success"/>, <see cref="CenterCap"/>). Keep it opaque — any alpha here makes the ring take
    /// on the petal underneath, which over the cream covered face reads as a dirty yellow.</summary>
    public static readonly Brush PointerRim = B(0x81, 0xB2, 0x9A);
    public static readonly Brush FocusIvory = B(0xF2, 0xEE, 0xE3);
    public static readonly Brush FocusCyan = B(0xFF, 0xFF, 0xFF);
    public static readonly Brush Flag = B(0x3C, 0x40, 0x5B);
    public static readonly Brush FlagShine = B(0xFF, 0xFF, 0xFF);
    /// <summary>The disc a marker sits on, behind its ring — white at 30%, so the petal art still reads
    /// through it and the mark sits on the tile rather than punching a hole in it.</summary>
    public static readonly Brush FlagGround = B(0xFF, 0xFF, 0xFF, 0x4D);
    // ── The gem ──
    // Honey-yellow through orange, read as a brilliant cut seen from directly above: a girdle circle, a ring
    // of kite facets, and a star table in the middle. Each facet gets its own tone so the stone has internal
    // structure to catch light on as it turns — a single fill would just be an orange disc.
    public static readonly Brush Diamond = B(0xF6, 0xC4, 0x53);        // girdle / body
    public static readonly Brush DiamondCore = B(0xFF, 0xE9, 0xA8);    // table, the brightest plane
    public static readonly Brush GemDeep = B(0xC8, 0x78, 0x1E);        // facets angled away
    public static readonly Brush GemMid = B(0xE9, 0xA1, 0x3B);
    public static readonly Brush GemLight = B(0xFF, 0xD8, 0x7A);
    public static readonly Brush GemGlint = B(0xFF, 0xFF, 0xFF);
    /// <summary>The girdle outline. Dark rather than white so the stone holds its edge against the pale
    /// covered petals it sits on.</summary>
    public static readonly Pen GemEdge = P(B(0x7A, 0x44, 0x08), 1.2);
    /// <summary>The outline of a node still resting in its opening, where the freed stone and the counter
    /// keep <see cref="GemEdge"/>: an unclaimed node reads as part of the board, a freed one as the reward.</summary>
    public static readonly Pen NectarEdge = P(B(0x0A, 0x23, 0x32), 1.2);
    public static readonly Pen GemFacet = P(B(0x9C, 0x5A, 0x11), 0.7);
    /// <summary>A resting gem's side walls: each wall is shaded by which way it faces, from this tone for a
    /// wall facing left to <see cref="GemSideRight"/> for one facing right.</summary>
    public static readonly Color GemSideLeft = Color.FromRgb(0x9B, 0x4F, 0x09);
    public static readonly Color GemSideRight = Color.FromRgb(0xC5, 0x5F, 0x08);
    /// <summary>The seam between two of a gem's side walls.</summary>
    public static readonly Pen GemSideSeam = P(B(0x22, 0x18, 0x14), 0.9);
    /// <summary>The bee. White, because the failure screen reveals mines onto <see cref="Revealed"/> — the
    /// dark petal — never onto the pale covered face.</summary>
    public static readonly Brush Mine = B(0xFF, 0xFF, 0xFF);
    /// <summary>The disc behind the bee. Dark, to counter the white bee: it keeps the silhouette legible when
    /// a mine lands on a light tile during the completion finale.</summary>
    public static readonly Brush MineRim = B(0x16, 0x1A, 0x28);
    public static readonly Brush Success = B(0x81, 0xB2, 0x9A);
    /// <summary>The warm ink on a growth notice that reports a threat rather than a reward.</summary>
    public static readonly Brush Warning = B(0xF6, 0xC4, 0x53);
    public static readonly Brush SuccessGold = B(0xF2, 0xEE, 0xE3);
    /// <summary>⚠ Unused, and must stay that way for the struck petal: that tile wears its hit face over the
    /// covered tint, never a black-out. Kept because <see cref="FailureInk"/>'s warning below is about this
    /// brush, and a future failure surface may want a true black fill.</summary>
    public static readonly Brush Failure = B(0x00, 0x00, 0x00);
    /// <summary>The ink for text that reports a failure — the face of the STUNG shout (<see cref="ShoutFace"/>).
    /// ⚠ Not the seed count: marking is capped at the mine count, so there is no over-flagged state for it to
    /// report.
    ///
    /// <para>⚠ Never use <see cref="Failure"/> as ink: it is <b>black</b>, and every string over the crop is
    /// drawn on the near-black <see cref="Plaque"/>. The two are deliberately separate brushes — one is a tile
    /// fill on a light board, one is ink on a dark plate.</para>
    ///
    /// <para>Ember coral rather than the gold <see cref="Warning"/>: gold means "this got harder", and a run
    /// that has ended should not wear the same colour as a difficulty notice. Shared with Connate's ember
    /// family so the two games agree on what warm means.</para></summary>
    public static readonly Brush FailureInk = B(0xE0, 0x7A, 0x5F);

    // ── The STUNG shout ──
    // Poster word art in one hue, the ember coral of FailureInk: a solid coral face parted by a thin cream
    // rule from an extrusion drawn only as dark-coral hatch lines, with speed lines in the same dark coral
    // trailing off the far end. The hatching carries the depth; no fill behind it.
    public static readonly Brush ShoutFace = FailureInk;
    public static readonly Brush ShoutInline = B(0xF2, 0xEE, 0xE3);
    public static readonly Brush ShoutShade = B(0xA8, 0x4E, 0x3C);
    public static readonly Brush CenterCap = B(0x81, 0xB2, 0x9A);
    /// <summary>The full-field wash behind an end card: the covered petal's own tan at 87%. Light, not a
    /// dim — an ended board lifts rather than darkens. ⚠ The text plaques carry their own opaque dark plate,
    /// which is what keeps their ink readable over this; anything drawn straight onto the wash has to be
    /// dark enough to win against a near-white ground.</summary>
    public static readonly Brush Scrim = B(0xF2, 0xEE, 0xE3, 0xDD);
    /// <summary>The plate behind text drawn over the crop. Fully opaque, and darker than the disc itself, because
    /// these labels sit over petals ranging from near-white to near-black and only a backing that wins against
    /// both ends makes the ink's contrast a fixed quantity. The thin pale rule around it keeps the plate from
    /// disappearing into a revealed petal, which is nearly its own colour.</summary>
    public static readonly Brush Plaque = B(0x15, 0x18, 0x27);

    /// <summary>Indexed by clue value 1–15 (a five-neighbour petal beside cells holding up to three bees);
    /// slot 0 is never drawn and is an ink, not the revealed tile fill, so a future zero-clue draw cannot vanish
    /// into its own petal. Values above 5 warm toward the flag ink so a stacked neighbourhood reads hotter.</summary>
    public static readonly Brush[] Clues =
    [
        B(0xFF, 0xFF, 0xFF), B(0xFF, 0xFF, 0xFF), B(0xA8, 0xC9, 0xB9), B(0xF2, 0xEE, 0xE3), B(0x81, 0xB2, 0x9A), B(0xFF, 0xFF, 0xFF),
        B(0xF6, 0xC4, 0x53), B(0xF6, 0xC4, 0x53), B(0xF2, 0xA6, 0x5A), B(0xF2, 0xA6, 0x5A), B(0xE0, 0x7A, 0x5F),
        B(0xE0, 0x7A, 0x5F), B(0xE0, 0x7A, 0x5F), B(0xE0, 0x7A, 0x5F), B(0xE0, 0x7A, 0x5F), B(0xE0, 0x7A, 0x5F),
    ];

    public static readonly Pen TilePen = P(Edge, 1.15);
    /// <summary>The selection outline: a steady black stroke, which holds its edge against every tile state
    /// (an ivory one does not, over a revealed petal). Life comes from the sparks riding it.</summary>
    public static readonly Pen FocusStrokePen = P(B(0x0C, 0x0F, 0x16), 2.6);
    /// <summary>The two light-grey sparks that ride the focus outline 180° apart.</summary>
    public static readonly Brush FocusSpark = B(0xD8, 0xDC, 0xE4);
    public static readonly Pen FocusInnerPen = P(FocusCyan, 1.35);
    /// <summary>⚠ Unused. Kept because it is the only stroke declared in the flag ink, and a future marker
    /// will want it.</summary>
    public static readonly Pen LockPen = P(Flag, 1.6);
    public static readonly Pen PlaquePen = P(B(0x6E, 0x75, 0x90), 1.0);

    /// <summary>A white stroke at <paramref name="alpha"/> and <paramref name="thickness"/>, cached.
    ///
    /// <para>⚠ For the effects that fade per object per frame: a reveal burst is drawn for every petal
    /// mid-reveal, and a cascade can have dozens going at once, while the struck bee's bolts redraw five a
    /// frame for as long as the failure board is up. Alpha is quantised to 16 levels and the width to a
    /// quarter pixel, both far below what a fading hairline can show.</para>
    ///
    /// <para>⚠ Butt caps unless asked. The reveal burst's pen strokes both its halo ring and five sparks a
    /// couple of widths long, and a round cap adds half the width at each end — enough to lengthen those
    /// sparks visibly. The bolts want round, and say so.</para></summary>
    public static Pen WhitePen(byte alpha, double thickness, bool round = false) =>
        ArcadePalette.WhitePen(alpha, thickness, round);

    /// <summary>A <see cref="DiamondCore"/> stroke at a width quantised to a quarter pixel, cached.
    ///
    /// <para>⚠ Per gem per frame, twice: the halo opening around a swelling stone and the streak behind a
    /// flying one. A board clear sends every gem it uncovered at once, so both peak together.</para>
    ///
    /// <para>Butt caps unless asked, matching the halo ring; the flight streak is a line and wants
    /// round.</para></summary>
    public static Pen CorePen(double thickness, bool round = false)
    {
        int width = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        int key = (round ? 1 << 16 : 0) | width;
        if (_corePens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(DiamondCore, width / 4.0);
        if (round) { pen.StartLineCap = PenLineCap.Round; pen.EndLineCap = PenLineCap.Round; }
        pen.Freeze();
        if (_corePens.Count < 512) _corePens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<int, Pen> _corePens = [];

    /// <summary>A <see cref="Flag"/> stroke at a width quantised to a quarter pixel, cached. Drawn once per
    /// flagged tile per frame, so it must not allocate.</summary>
    public static Pen FlagPen(double thickness)
    {
        int key = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        if (_flagPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(Flag, key / 4.0);
        pen.Freeze();
        if (_flagPens.Count < 512) _flagPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<int, Pen> _flagPens = [];
}
