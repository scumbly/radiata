using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Internode's frozen brushes and pens, built from explicit <c>(r, g, b, a)</c> bytes so the arcade's
/// <c>#AARRGGBB</c> trap (docs/ARCADE.md) cannot happen. Names are roles: a brush that fills a shape is never
/// also handed out as an ink.
///
/// <para>The look: a checkered half-pipe darkening into near-black at its own horizon under a starfield, its tone changing with every gate passed, cyan orbs, dark
/// spiked mines with ember tips, a pearl runner. Gold is reserved for the gate: the gate ring, the quota readout
/// the instant it is met, and the pass shout. Nothing else on the board wears it.</para></summary>
internal static class InternodePalette
{
    private static SolidColorBrush B(byte r, byte g, byte b, byte a = 255) => ArcadePalette.Fill(r, g, b, a);
    private static Pen P(Brush brush, double width, PenLineCap cap = PenLineCap.Round) => ArcadePalette.RoundPen(brush, width, cap);
    private static T Frozen<T>(T f) where T : Freezable => ArcadePalette.Frozen(f);

    /// <summary>A gradient brush at a fraction of its alpha, every stop scaled; see <see cref="ArcadePalette.Faded"/>.
    /// Lets a sprite glow carry its fade in the brush instead of a <c>PushOpacity</c> layer per sprite per frame.</summary>
    public static Brush Faded(Brush brush, double alpha) => ArcadePalette.Faded(brush, alpha);

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    public static Color Lerp(Color a, Color b, double t) => ArcadePalette.Lerp(a, b, t);
    /// <summary>A frozen fill in <paramref name="c"/> at <paramref name="alpha"/>, cached on a 32-step alpha.
    /// <see cref="Dimmed"/> builds on it, and so does every fading ring.</summary>
    public static SolidColorBrush Solid(Color c, double alpha = 1) => ArcadePalette.Solid(c, alpha);

    /// <summary>A round-capped stroke, cached on the same terms as <see cref="Solid"/> with the width
    /// quantised to a quarter pixel.
    ///
    /// <para>⚠ Per object per frame where it matters: a checkpoint sends every banked token flying with two
    /// fading rings each, so both the alpha and the width change from one to the next.</para></summary>
    public static Pen Stroke(Color c, double width, double alpha = 1) => ArcadePalette.Stroke(c, width, alpha);

    // ── The pipe ──────────────────────────────────────────────────────────────
    /// <summary>Checker tone pairs at the mouth, one per theme. ⚠ The two tones of a pair sit close together: the
    /// bands rush past several times a second, and a hard light/dark alternation strobes at that rate. They carry
    /// the pattern, not the contrast — the ramp below carries each into near-black, so
    /// these are the lightest the ground ever gets and distance still ends in the dark: the pipe takes the next one every time a gate is
    /// passed, so progress is visible in the ground itself. The ramps below carry each pair into the fog by depth.</summary>
    private static readonly (Color A, Color B)[] ThemeColours =
    [
        Pair(0x66, 0x5F, 0xA6),   // indigo
        Pair(0x38, 0x86, 0x92),   // teal
        Pair(0x87, 0x50, 0x92),   // plum
        Pair(0x4F, 0x85, 0x5C),   // moss
        Pair(0xA0, 0x5B, 0x46),   // rust
        Pair(0x5C, 0x67, 0x87),   // slate
    ];

    /// <summary>How much darker the B square is than the A square: the checkerboard's contrast, and the one
    /// lever for it. Derived from a single factor rather than authored as twelve separate literals, so the
    /// pattern's strength stays one number.
    ///
    /// <para>⚠ Raise this with care. The bands rush past several times a second, and a hard light/dark
    /// alternation strobes at that rate, which is why the pair must stay close. At 0.93 the two tones differ
    /// by 7–12 of 255 per channel, enough to read the grid as tiles and well short of a flicker. It
    /// multiplies <see cref="FloorTone"/>, so brightening the track does not also widen the pattern.</para></summary>
    private const double CheckPairDrop = 0.93;

    /// <summary>One theme's checker pair from its identity colour: the literal is the A square, and B is it
    /// darkened by <see cref="CheckPairDrop"/>.</summary>
    private static (Color A, Color B) Pair(int r, int g, int b) =>
        (Floor(r, g, b), Floor((int)Math.Round(r * CheckPairDrop), (int)Math.Round(g * CheckPairDrop),
                               (int)Math.Round(b * CheckPairDrop)));

    /// <summary>How bright the floor is against the tones above. Applied as a straight multiply on all three
    /// channels, so every theme keeps its hue and its distance from its own pair — the pipe gets darker, not
    /// greyer, and the pairs stay close enough not to strobe.
    ///
    /// <para>⚠ This is the one lever for the track's level. The distance ramp already holds the middle
    /// distance near its mouth tone, so the far read is limited by the mouth tone itself, and raising it
    /// here keeps the far ring landing exactly on <see cref="FogColour"/>, which is what stops the pipe
    /// ending on a lit disc. Brightening by shortening the ramp instead would break that.</para></summary>
    private const double FloorTone = 0.78;

    /// <summary>A theme tone at <see cref="FloorTone"/>. The literals stay the identity of each theme; this
    /// is the one place their level is set.</summary>
    private static Color Floor(int r, int g, int b) =>
        Rgb((byte)Math.Round(r * FloorTone), (byte)Math.Round(g * FloorTone), (byte)Math.Round(b * FloorTone));
    public static int Themes => ThemeColours.Length;
    /// <summary>The far end of the pipe: near-black, so the last rings sink into the sky instead of ending on a
    /// lit disc.</summary>
    public static readonly Color FogColour = Rgb(0x00, 0x00, 0x00);
    public const int RampSteps = 32;
    /// <summary>[theme][step]: the tone at brightness 1 − step / (<see cref="RampSteps"/> − 1), a straight
    /// multiply toward <see cref="FogColour"/>, so step 0 is the mouth tone and the last step is the fog exactly.
    /// Evenly spaced in brightness; the shape of the fall with distance is the renderer's
    /// (<c>InternodeRenderer.DistanceBrightness</c>). Pre-baked: the ring pass allocates no brushes.</summary>
    public static readonly Brush[][] CheckA = [.. ThemeColours.Select(t => Ramp(t.A))];
    public static readonly Brush[][] CheckB = [.. ThemeColours.Select(t => Ramp(t.B))];
    private static Brush[] Ramp(Color near)
    {
        var ramp = new Brush[RampSteps];
        for (int i = 0; i < RampSteps; i++)
            ramp[i] = Solid(Lerp(near, FogColour, (double)i / (RampSteps - 1)));
        return ramp;
    }
    /// <summary>The outline every checker square on the track surface carries: white, faint, so the grid
    /// reads as tiled panels rather than as two flat tones meeting.
    ///
    /// <para>⚠ It does not take the fog ramp, deliberately: the highlight is the surface's own edge, and a
    /// receding one would leave the far rings as bare colour again. The sectors' own darkening does the
    /// depth work.</para>
    ///
    /// <para>⚠ Adjacent squares share their side edges, so those take two strokes and land near double the
    /// single-edge alpha. Left alone: an even grid is what the eye wants here, and the alternative is
    /// tracking which edges a neighbour already drew, per sector, per ring, every frame.</para></summary>
    public static readonly Pen CheckEdge = P(B(0xFF, 0xFF, 0xFF, 0x13), 1.0, PenLineCap.Flat);

    /// <summary><see cref="CheckEdge"/> at a ramp step of the checker fill (<see cref="CheckA"/>): its alpha
    /// scaled by the same brightness, so the hairline darkens with the floor it outlines. One weight still.</summary>
    public static Pen CheckEdgeAt(int step) => _checkEdges[Math.Clamp(step, 0, RampSteps - 1)];
    private static readonly Pen[] _checkEdges = [.. Enumerable.Range(0, RampSteps).Select(i =>
        P(B(0xFF, 0xFF, 0xFF, (byte)Math.Round(0x13 * (1 - (double)i / (RampSteps - 1)))), 1.0, PenLineCap.Flat))];

    /// <summary>The seam where the surface ends and the open top begins. ⚠ The checker grid is deliberately
    /// not depth-weighted — one weight everywhere, so the far tiling keeps reading as a grid.</summary>
    public static readonly Pen SplitEdge = P(B(0xB9, 0xB0, 0xE0, 0xFF), 4.2);
    public static readonly Pen SplitEdgeFine = P(B(0xB9, 0xB0, 0xE0, 0xFF), 3.0);

    /// <summary>The seam and the gap lips at a depth-dependent weight: full thickness at the mouth, thinning
    /// with distance, so the rim you are about to launch off is the one that reads heaviest.
    ///
    /// <para>⚠ <paramref name="share"/> is 1 at the mouth and 0 at the far plane. It is floored by the caller
    /// rather than allowed to reach zero — a gap lip is a hazard boundary a player judges a jump against from
    /// a long way off, and a lip that thins to nothing is the one line here that must never disappear.</para>
    ///
    /// <para>Cached on a sixteenth-pixel ladder, like every stroke on a per-ring path: this is called once per
    /// ring per side per frame and must never build a pen.</para></summary>
    public static Pen SplitEdgeAt(double baseThickness, double share, double fade = 1)
    {
        int step = (int)Math.Clamp(Math.Round(baseThickness * share * 16), 1, 80);
        int tone = (int)Math.Clamp(Math.Round(fade * FadeSteps), 0, FadeSteps);
        int key = step * (FadeSteps + 1) + tone;
        if (_splitEdges.TryGetValue(key, out var hit)) return hit;
        var pen = P(Solid(Towards(SplitEdgeColour, tone / (double)FadeSteps), 1), step / 16.0);
        _splitEdges[key] = pen;
        return pen;
    }
    private static readonly Dictionary<int, Pen> _splitEdges = [];
    private const int FadeSteps = 16;
    private static readonly Color SplitEdgeColour = Color.FromRgb(0xB9, 0xB0, 0xE0);

    /// <summary>A colour on the way up from black: <paramref name="share"/> 0 is black, 1 is <paramref name="c"/>
    /// itself, alpha untouched. Quantised to <see cref="FadeSteps"/> so callers caching by colour stay bounded.
    /// Strokes only: the checker fills are never run through this.</summary>
    public static Color Towards(Color c, double share)
    {
        share = Math.Round(Math.Clamp(share, 0, 1) * FadeSteps) / FadeSteps;
        return Color.FromArgb(c.A, (byte)(c.R * share), (byte)(c.G * share), (byte)(c.B * share));
    }

    // ── Sky ───────────────────────────────────────────────────────────────────
    /// <summary>The sky's two tones: the renderer builds the actual brush per frame, black at the far station and
    /// lightening outward to these.</summary>
    public static readonly Color SkyDeep = Rgb(0x0E, 0x0F, 0x24);
    public static readonly Color SkyEdge = Rgb(0x2B, 0x23, 0x50);
    /// <summary>Flat sky for the how-to card, which has no far station.</summary>
    public static readonly Brush SkyFlat = Solid(SkyDeep);
    public static readonly Brush Star = B(0xF6, 0xF1, 0xE0, 0x8C);
    public static readonly Brush StarBright = B(0xFF, 0xFB, 0xEE, 0xE6);
    /// <summary>The far end of the pipe. Near-black so it reads as depth continuous with the sky rather than as a
    /// lit disc hanging at the horizon, fading out across the farthest rings.</summary>
    public static readonly Brush FogGlow = Frozen(new RadialGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
        GradientStops =
        {
            new GradientStop(FogColour, 0.00),
            new GradientStop(FogColour, 0.36),
            new GradientStop(Color.FromArgb(0x70, FogColour.R, FogColour.G, FogColour.B), 0.62),
            new GradientStop(Color.FromArgb(0x00, FogColour.R, FogColour.G, FogColour.B), 1.00),
        },
    });
    /// <summary>Soft dark falloff at the rim, drawn outside the world rotation so the bowl never tilts.</summary>
    public static readonly Brush RimVignette = Frozen(new RadialGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0.70),
            new GradientStop(Color.FromArgb(0x3C, 0x00, 0x00, 0x00), 0.90),
            new GradientStop(Color.FromArgb(0x8C, 0x00, 0x00, 0x00), 1.00),
        },
    });

    // ── Orbs ──────────────────────────────────────────────────────────────────
    public static readonly Color OrbColour = Rgb(0x5F, 0xD8, 0xFF);
    /// <summary>An orb off the ground shifts hue with its height — cyan on the surface toward violet at the tallest
    /// jump — so how high it sits is readable without depending on the shadow alone. ⚠ Never toward gold: gold is
    /// the bonus orb's meaning.</summary>
    private static readonly Color OrbHighColour = Rgb(0xC9, 0x7B, 0xFF);
    public const int OrbHeightSteps = 8;
    public static Color OrbHeightColour(int step) => Lerp(OrbColour, OrbHighColour, (double)Math.Clamp(step, 0, OrbHeightSteps - 1) / (OrbHeightSteps - 1));
    public static readonly Brush[] OrbByHeight = [.. Enumerable.Range(0, OrbHeightSteps).Select(i => Solid(OrbHeightColour(i)))];
    public static readonly Color OrbHotColour = Rgb(0xE8, 0xFB, 0xFF);
    public static readonly Color BonusColour = Rgb(0xF2, 0xB8, 0x4B);
    /// <summary>The ring on a pattern's last orb before it pays: orange, not the prize gold, at 70%, so the
    /// promise is a shade apart from the payment and sits under it.</summary>
    public static readonly Color GoldCandidateRing = Color.FromArgb(0xB3, 0xF0, 0x8A, 0x2E);
    public static readonly Color MissedColour = Rgb(0x7A, 0x80, 0x8A);
    public static readonly Color MineColour = Rgb(0xF6, 0xF3, 0xEE);
    public static readonly Color MineTipColour = Rgb(0xD8, 0x2A, 0x22);

    // ── Sprite glows ──────────────────────────────────────────────────────────
    // What a token or a mine is, readable from the far end of the pipe. Distance darkens every sprite toward
    // black (see the renderer's brightness curve), which is right for reading shape up close and useless for
    // spotting a hazard in time — so the glow deliberately does not take that darkening. It is the long-range
    // channel; the sprite itself is the short-range one.
    //
    // ⚠ Each is one frozen brush for every size, mapped RelativeToBoundingBox: the geometry it is drawn
    // through supplies the extent. A per-sprite absolute gradient would churn a brush per sprite per frame,
    // and up to MaxSprites of them can be in view.

    /// <summary>Falloff shared by all three: solid at the sprite, gone by the edge of the halo. The middle
    /// stop is what stops it reading as a flat disc with a soft rim.</summary>
    private static RadialGradientBrush GlowBrush(Color c, byte core) => Frozen(new RadialGradientBrush
    {
        MappingMode = BrushMappingMode.RelativeToBoundingBox,
        GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(core, c.R, c.G, c.B), 0.00),
            new GradientStop(Color.FromArgb((byte)(core * 0.62), c.R, c.G, c.B), 0.34),
            new GradientStop(Color.FromArgb((byte)(core * 0.20), c.R, c.G, c.B), 0.64),
            new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1.00),
        },
    });

    /// <summary>An ordinary token's glow: white.
    ///
    /// <para>⚠ A halo has to beat its background, and this pipe's floor is a mid-luminance colour across
    /// every theme, so the only reliable direction is lighter, not deeper — a hued glow close to the floor's
    /// own luminance renders every frame and cannot be seen.</para>
    ///
    /// <para>White also keeps the one distinction that matters: a plain token is white light, a gold one is
    /// gold (<see cref="BonusGlow"/>), and nothing else on the track glows either colour. ⚠ It carries at a
    /// much lower alpha than a hue would — see <c>InternodeRenderer.TokenGlowNear</c>, which is a third of
    /// what the navy needed — so raising the alpha here to make it "clearer" will bloom instead.</para></summary>
    public static readonly Color TokenGlowColour = Rgb(0xFF, 0xFF, 0xFF);
    public static readonly Brush TokenGlow = GlowBrush(TokenGlowColour, 0xC4);

    /// <summary>A gold token's glow. Gold is what "this one pays" means on this board, so the halo has to
    /// carry it too — a blue glow round a gold token would undo the one distinction that matters.</summary>
    public static readonly Brush BonusGlow = GlowBrush(BonusColour, 0xCE);

    /// <summary>A mine's glow: the red of its tips, never the pearl of its body. Red is the hazard, and this
    /// is the half of the mine visible while it is still far enough away to steer around.
    /// ⚠ Hotter than the token glows on purpose — the one sprite whose warning has to arrive first.</summary>
    public static readonly Brush MineGlow = GlowBrush(MineTipColour, 0xE6);
    /// <summary>The exhaust ring's two strokes: an orange body with a hotter yellow core drawn inside it, so
    /// one expanding circle reads as flame rather than as a coloured hoop.
    ///
    /// <para>⚠ Colours, not brushes. The ring's width changes every frame as it thins away, and
    /// <see cref="Stroke"/> caches a pen per (colour, width) — so these are handed to that rather than frozen
    /// into a pen here, which could only ever serve one width.</para></summary>
    public static readonly Color FlameRing = Rgb(0xFF, 0x7A, 0x22);
    public static readonly Color FlameRingHot = Rgb(0xFF, 0xE2, 0x3C);
    /// <summary>Sprites stay opaque at every depth: distance darkens them toward the fog instead of thinning them.
    /// <paramref name="dim"/> in [0, 1] is quantised to <see cref="DimSteps"/> and the brushes are cached, so the
    /// per-frame cost is a dictionary lookup.</summary>
    public const int DimSteps = 24;
    private static readonly Dictionary<(Color, int), Brush> _dimmed = [];
    public static Brush Dimmed(Color c, double dim)
    {
        int step = (int)Math.Clamp(dim * (DimSteps - 0.001), 0, DimSteps - 1);
        if (step == 0) return Solid(c);
        var key = (c, step);
        if (!_dimmed.TryGetValue(key, out var b))
        {
            // Linear to near-black: the renderer decides where the darkening starts (screen space), this only how far it goes.
            b = Solid(Lerp(c, Rgb(0x02, 0x02, 0x06), (double)step / (DimSteps - 1)));
            _dimmed[key] = b;
        }
        return b;
    }
    public static readonly Brush Orb = B(0x5F, 0xD8, 0xFF);
    public static readonly Brush OrbHot = B(0xE8, 0xFB, 0xFF);
    /// <summary>The bonus orb: gold, and the only orb that is. Worth ten.</summary>
    public static readonly Brush Bonus = B(0xF2, 0xB8, 0x4B);
    /// <summary>An orb that got past: drained to grey.</summary>
    public static readonly Brush Missed = B(0x7A, 0x80, 0x8A);
    /// <summary>The facet line across an orb; an ink, never a fill.</summary>
    public static readonly Pen OrbFacet = P(B(0x0B, 0x0D, 0x14, 0x8C), 1.25);
    /// <summary>An elevated orb's tether down to the surface.</summary>
    public static readonly Pen Tether = Frozen(new Pen(B(0xE8, 0xFB, 0xFF, 0x66), 1.0)
    {
        DashStyle = new DashStyle([2.0, 3.0], 0), DashCap = PenLineCap.Flat,
    });
    public static readonly Brush Accent = B(0x5F, 0xD8, 0xFF);
    public static readonly Color AccentInk = Rgb(0x5F, 0xD8, 0xFF);

    // ── Mines ─────────────────────────────────────────────────────────────────
    /// <summary>White body, red spikes and core, black ink: the board's ground runs from light to near-black with
    /// depth, so the one shape the player must never touch is the one that carries all three and reads against any
    /// of it. ⚠ This is why the mine takes a black outline where Connate's dark bomb needed a pale one.</summary>
    public static readonly Brush Mine = B(0xF6, 0xF3, 0xEE);
    public static readonly Brush MineTip = B(0xD8, 0x2A, 0x22);
    /// <summary>A mine's edge is pale, never a dark cel: a dark body with a dark outline is a blot at depth.
    /// Width in the sprite's unit space, so it scales with distance.</summary>
    public static readonly Pen MineEdgeUnit = P(B(0x08, 0x08, 0x0E), 0.14);

    // ── Runner ────────────────────────────────────────────────────────────────
    public static readonly Color RunnerColour = Rgb(0xF4, 0xF1, 0xEA);
    public static readonly Brush Runner = B(0xF4, 0xF1, 0xEA);
    public static readonly Brush RunnerTrim = B(0x5F, 0xD8, 0xFF);
    /// <summary>Contact shadows: a crisp dark blob under the runner, every orb and every mine, cast straight onto the
    /// nearest wall. Dense enough to read on the light near checkers; the runner's carries jump height.</summary>
    public static readonly Brush Shadow = B(0x00, 0x00, 0x00, 0x99);

    // ── Gate ──────────────────────────────────────────────────────────────────
    // Gold is reserved for the gate and what the gate is about.
    /// <summary>The wash over the token count when a gate is reached short — pulsed several times, so the
    /// number the player has to beat is pointed at. ⚠ Cached on a sixteenth-of-alpha ladder: this is asked
    /// for every frame of the pulse and a radial gradient built per frame is a per-frame allocation on the
    /// render pump. Gold, not a warning ink — coming up short is not a mistake, and the run simply holds.</summary>
    public static Brush QuotaGlow(double alpha)
    {
        int step = (int)Math.Clamp(Math.Round(alpha * 16), 0, 16);
        if (_quotaGlows.TryGetValue(step, out var hit)) return hit;
        var brush = GlowBrush(GateGoldColour, (byte)Math.Clamp(step * 16, 0, 255));
        _quotaGlows[step] = brush;
        return brush;
    }
    private static readonly Dictionary<int, Brush> _quotaGlows = [];

    public static readonly Color GateGoldColour = Rgb(0xF2, 0xB8, 0x4B);
    public static readonly Brush GateGold = B(0xF2, 0xB8, 0x4B);
    /// <summary>The bonus toast's highlighted multiplier: yellow ink over an orange drop shadow (a hard offset copy,
    /// no blur).</summary>
    public static readonly Brush ToastHighlight = B(0xFF, 0xE4, 0x5C);
    public static readonly Brush ToastShadow    = B(0xE0, 0x7A, 0x1A);

    // ── Text ──────────────────────────────────────────────────────────────────
    public static readonly Brush Ink = B(0xF2, 0xEE, 0xE3);
    public static readonly Color InkColour = Rgb(0xF2, 0xEE, 0xE3);
    public static readonly Brush InkDim = B(0xB8, 0xB3, 0xA6);
    public static readonly Brush Danger = B(0xE0, 0x5A, 0x4F);
    /// <summary>Short by: ember, deliberately neither the gold nor the mine fill.</summary>
    public static readonly Color ShoutFail = Rgb(0xE0, 0x7A, 0x5F);
    public static readonly Color ShoutStage = Rgb(0x8F, 0xF0, 0xE0);
    public static readonly Color CelInk = Rgb(0x0B, 0x0D, 0x14);
    public const double LabelStrokeWidth = 1.25;
    public static readonly Pen FineCel = P(B(0x0B, 0x0D, 0x14), LabelStrokeWidth);
    /// <summary>Cel outline in sprite unit space (a unit-radius sprite), so the line thins with distance.</summary>
    public static readonly Pen CelUnit = P(B(0x0B, 0x0D, 0x14), 0.10);
    /// <summary>Behind the intro and game-over cards: the sky's bottom tone at 87%.</summary>
    public static readonly Brush Scrim = B(0x14, 0x12, 0x2C, 0xDD);
    /// <summary>Backing plate for the HUD and the checkpoint banner.</summary>
    public static readonly Brush Plaque = B(0x10, 0x0F, 0x22, 0xE0);
    public static readonly Pen PlaquePen = P(B(0x6E, 0x66, 0x9C), 1.0);

    // ── Glass ─────────────────────────────────────────────────────────────────
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

    /// <summary>Inner rim shading in sprite unit space: dark on the lit side, bright on the far one.</summary>
    public static readonly Pen EdgeBounceUnit = Frozen(new Pen(Frozen(new LinearGradientBrush
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
    }), 0.30) { LineJoin = PenLineJoin.Round });

    /// <summary>A stroke in <see cref="Dimmed"/>'s distance-darkened colour, at a width quantised to a
    /// quarter pixel and cached.
    ///
    /// <para>⚠ Per token per frame: the gold marker rings every prize orb in view, and a track holds many at
    /// once at every depth, so both the dim step and the width vary from one to the next. Quantising both is
    /// what makes the table finite; <see cref="DimSteps"/> already bounds the colour.</para></summary>
    public static Pen DimPen(Color c, double dim, double thickness)
    {
        int step = (int)Math.Clamp(dim * (DimSteps - 0.001), 0, DimSteps - 1);
        int width = (int)Math.Clamp(Math.Round(thickness * 4), 1, 4000);
        var key = (c, step << 16 | width);
        if (_dimPens.TryGetValue(key, out var hit)) return hit;
        var pen = new Pen(Dimmed(c, dim), width / 4.0);
        pen.Freeze();
        // 24 dim steps against the orb radii a track actually produces; the guard is for a pathological scale.
        if (_dimPens.Count < 4096) _dimPens[key] = pen;
        return pen;
    }

    private static readonly Dictionary<(Color, int), Pen> _dimPens = [];
}
