using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>Draws Kabloom as a luminous enamel garden.</summary>
internal sealed class KabloomRenderer : IArcadeRenderer
{
    public static readonly KabloomRenderer Instance = new();
    public Brush Accent => KabloomPalette.FocusCyan;
    public bool CoversField => true;   // the opaque Disc below paints the whole field

    public void Draw(DrawingContext dc, Point c, double fieldRadius, IArcadeGame game, double ppd)
    {
        if (game is not Kabloom k) return;

        // Replace the shared grey-blue playfield with Kabloom's own colored depth, while retaining the common
        // bezel and circular composition supplied by ArcadeChrome.
        dc.DrawEllipse(KabloomPalette.Disc, null, c, fieldRadius, fieldRadius);
        dc.DrawEllipse(KabloomPalette.DiscGlow, null, c, fieldRadius * 0.72, fieldRadius * 0.72);

        Point boardOrigin = new(c.X - fieldRadius * 0.055, c.Y + fieldRadius * 0.008);
        double scale = fieldRadius * 0.785 / Math.Max(1e-6, k.Grid.Radius);

        // ── The level transition ──
        // One clock from the sim drives all three parts. The board pulls away on a single sine, so it returns
        // to exactly 1.0 rather than drifting, and each petal flips on a delay proportional to its distance
        // from the middle — that delay is the centre-out wave, and it also makes the new garden's outer ring
        // arrive last.
        double transition = k.TransitionElapsed;
        bool transitioning = transition >= 0 && transition < KabloomTuning.LevelTransitionSeconds;
        if (transitioning)
            scale *= 1 - KabloomTuning.LevelTransitionZoom
                * Math.Sin(transition / KabloomTuning.LevelTransitionSeconds * Math.PI);
        PetalPaths(k, boardOrigin, scale, out Geometry[] paths, out Vec2[][] outlines, out int[] topDown,
                   out int[] gemsTopDown);
        double[] lifts = PetalLifts(k, scale, transition, transitioning);
        Brush tint = PetalTint(boardOrigin, k.Grid.Radius * scale);

        // ⚠ △ is consumed by the host (it opens the HOW TO PLAY card), so the game never sees it — never bind
        // an in-game aid to it.
        // Two passes: the flat petals, then the raised ones from the top of the screen down. ⚠ Raised after
        // flat, or a cleared petal above a covered one paints over the covered face standing up into it; and
        // top-down, so a lower petal's face lands in front of the side of the one above it, as the tiles read.
        // Resting gems join the raised pass in the same order, keyed on their hub, so the petals below a hub cover
        // its walls and the gem covers the petals above.
        int nextGem = 0;
        for (int pass = 0; pass < 2; pass++)
        foreach (int i in topDown)
        {
            if ((lifts[i] > 0) != (pass == 1)) continue;
            if (pass == 1)
                while (nextGem < gemsTopDown.Length
                       && k.Grid.CenterCaps[gemsTopDown[nextGem]].Center.Y < k.Grid.Cells[i].Center.Y)
                    DrawRestingGem(dc, boardOrigin, scale, k, outlines, gemsTopDown[nextGem++]);
            KabloomCell cell = k.Grid.Cells[i];
            double reveal = k.RevealProgress(i);
            double mineClear = k.MineClearProgress(i);
            bool visuallyRevealed = (k.Board.IsRevealed(i) && reveal >= 1) || mineClear >= 1;
            Brush fill = TileFill(k, cell, visuallyRevealed);
            // The cursor cell wears its own face where the artist supplied one; the focus outline and its
            // sparks are still drawn over the top, so the selection reads with or without art.
            bool focused = i == k.FocusCell;

            if (transitioning)
            {
                // Before its own midpoint a petal still wears the cleared face — the board you just finished
                // is gone from the simulation by now (LoadLevel replaced it), so the memory of it has to be
                // drawn rather than read. After the midpoint it's the fresh covered petal.
                double flip = PetalFlip(k, cell, transition, out bool showCleared);
                if (showCleared) fill = KabloomPalette.Revealed;
                if (flip < 1)
                {
                    Point centre = Screen(cell.Center, boardOrigin, scale);
                    // Edge-on at the midpoint: squash to nothing across the wave's own axis, which here is
                    // vertical. A real 3D flip would need a projection the rest of this renderer doesn't have.
                    dc.PushTransform(new ScaleTransform(1, Math.Max(0.02, flip), centre.X, centre.Y));
                    DrawRaisedPetal(dc, paths[i], outlines[i], boardOrigin, scale, cell, fill,
                                    PetalSlot(k, cell, visuallyRevealed, showCleared, focused), shade: false,
                                    lifts[i], showCleared ? null : tint);
                    dc.Pop();
                    continue;
                }
            }
            // Warm shading toward the hub side of each covered petal, so the tile reads as a material rather
            // than a flat enamel chip. Revealed petals are excluded: they are already the dark "cleared" fill,
            // and this would only muddy the numeral.
            DrawRaisedPetal(dc, paths[i], outlines[i], boardOrigin, scale, cell, fill,
                            PetalSlot(k, cell, visuallyRevealed, false, focused), shade: !visuallyRevealed,
                            lifts[i], visuallyRevealed ? null : tint);
        }
        while (nextGem < gemsTopDown.Length)
            DrawRestingGem(dc, boardOrigin, scale, k, outlines, gemsTopDown[nextGem++]);

        // A logical flood fill is already complete, but presentation travels one graph hop at a time.
        // Active petals punch through the light enamel, overshoot, and shed a few inexpensive vector sparks.
        for (int i = 0; i < k.Grid.Cells.Count; i++)
        {
            double reveal = k.RevealProgress(i);
            double mineClear = k.MineClearProgress(i);
            double presentation = Math.Max(reveal, mineClear);
            if (presentation is < 0 or >= 1) continue;
            Point center = Screen(k.Grid.Cells[i].Center, boardOrigin, scale);
            DrawRevealPetal(dc, paths[i], center, presentation, i, scale,
                k.Phase == Kabloom.Stage.Failed && k.Board.IsMine(i)
                    ? TileFill(k, k.Grid.Cells[i], visuallyRevealed: true) : KabloomPalette.Revealed);
        }

        DrawCellContents(dc, boardOrigin, scale, k, ppd, lifts);

        // ⚠ Bounds-checked, alone among the sim indices this renderer reads: an out-of-range focus cell would
        // throw inside OnRender, which is a hard app crash with a game up — the cloak lifts and the game
        // underneath takes double input. A missing outline for one frame is the cheaper failure by far.
        if (k.FocusCell >= 0 && k.FocusCell < paths.Length)
        {
            double lift = lifts[k.FocusCell];
            if (lift > 0) dc.PushTransform(LiftTransform(lift));
            DrawFocusOutline(dc, paths[k.FocusCell], k.PresentationTime);
            if (lift > 0) dc.Pop();
        }
        DrawPointer(dc, new Point(k.PointerX, k.PointerY), boardOrigin, scale, k.PresentationTime);

        DrawGrowthNotice(dc, c, fieldRadius, k, ppd);
        DrawHud(dc, c, fieldRadius, k, ppd);
        // ⚠ After the HUD, not before it. A freed gem is the reward and is far too big to pass behind
        // anything without reading as a mistake — it flies to the plate's own gem, so ending the flight
        // behind the plate is the one place it must not go. Drawing here also means it reads the gem target
        // DrawHud wrote this frame rather than last frame's.
        DrawDiamonds(dc, boardOrigin, scale, c, fieldRadius, k, ppd);
        DrawStage(dc, c, fieldRadius, k, ppd);
        // Over the scrim: the bee you set off is the thing a lost board is for. The card's words go over the
        // bee, so they are never hidden behind the insect parked beside them.
        DrawStruckBee(dc, c, fieldRadius, boardOrigin, scale, k);
        DrawOutcomeCard(dc, c, fieldRadius, k, ppd);
        DrawGeneratingBee(dc, c, fieldRadius, k);
    }

    /// <summary>The generation wait, carried by a bee: a fixed 100 DIP tall insect wanders a lazy Lissajous
    /// path over the board with a small side-to-side wiggle, shadow and all, drawn over everything including
    /// the GENERATING plaque. Clocked off <see cref="Kabloom.PhaseTime"/>, so pause and freeze hold it still.</summary>
    private static void DrawGeneratingBee(DrawingContext dc, Point c, double field, Kabloom game)
    {
        if (game.Phase != Kabloom.Stage.Generating || game.PhaseTime < GeneratingGraceSeconds) return;
        double t = game.PhaseTime;
        // Two incommensurate periods, so the loop never visibly repeats over a long wait.
        double x = c.X + field * GeneratingBeeReach * Math.Sin(t * 0.61);
        double y = c.Y + field * GeneratingBeeReach * 0.75 * Math.Sin(t * 0.37 + 1.3);
        // The wiggle runs across the direction of travel.
        double vx = Math.Cos(t * 0.61) * 0.61, vy = 0.75 * Math.Cos(t * 0.37 + 1.3) * 0.37;
        double vl = Math.Max(1e-6, Math.Sqrt(vx * vx + vy * vy));
        double wiggle = Math.Sin(t * 5.2) * field * 0.03;
        var at = new Point(x - vy / vl * wiggle, y + vx / vl * wiggle);
        double size = GeneratingBeeHeightDip / (2 * BeeArtHalf);
        DrawStruckBeeShadow(dc, at, size, 1);
        DrawMine(dc, at, size);
    }

    /// <summary>How long a generation must run before the wait indicator (bee and plaque) shows at all. The
    /// early levels generate at play time in a few milliseconds, and a bee that exists for one frame reads as a
    /// bug on the first reveal rather than as a wait.</summary>
    private const double GeneratingGraceSeconds = 0.30;

    /// <summary>The wandering bee's height in DIPs. Fixed rather than field-relative: it is a wait indicator,
    /// not part of the board, and should read the same at every window size.</summary>
    private const double GeneratingBeeHeightDip = 100;

    /// <summary>How far from centre the wandering bee ranges, as a fraction of the field radius.</summary>
    private const double GeneratingBeeReach = 0.55;

    /// <summary>Which way the bee art looks. ⚠ Must match the drawing: the bee is parked on the side of the
    /// card it can face, so it looks at the words rather than away from them. Flip this if the art faces the
    /// other way — nothing else needs changing.</summary>
    private const bool BeeFacesLeft = true;

    /// <summary>Which side of the card the parked bee sits on: +1 right, −1 left. ⚠ Read by the card as well,
    /// which steps the opposite way to clear it — two places deriving this from the facing separately is how
    /// they end up disagreeing.</summary>
    private static int StruckBeeSide => BeeFacesLeft ? 1 : -1;

    /// <summary>How far the end card is offset from the disc's centre, away from the parked bee, in field
    /// radii. A third of the radius puts its near side a third of the diameter in from the rim it sits
    /// nearest, which is the measure this is set by.</summary>
    private const double StungCardShift = 1.0 / 3;

    /// <summary>How far past the disc's centre, toward the parked bee, the STUNG shout may reach, in field
    /// radii. The front bee's drawn head sits about 0.18 in; each extra bee from a stacked petal parks
    /// <see cref="StruckBeeStackDx"/> nearer the card, so the allowance shrinks by that per bee, keeping about
    /// 0.06 clear.</summary>
    private static double StungShoutReach(Kabloom game)
    {
        int id = game.HitBeeCell;
        int bees = id >= 0 && id < game.Grid.Cells.Count ? Math.Clamp(game.Board.BeeCount(id), 1, 3) : 1;
        return 0.12 - StruckBeeStackDx * (bees - 1);
    }

    /// <summary>How long the failure scrim takes to reach full strength once the card may show: the struck bee's
    /// own flight time, so the wash completes as the bee parks at full size.</summary>
    private static double StungScrimFadeSeconds => KabloomTuning.StungBeeFlightSeconds;

    /// <summary>The parked bee's size, in field radii, and where it settles beside the card. The size is set
    /// so the drawn insect stands 40% of the playfield's height — the glyph and the art both span a little
    /// over twice this.</summary>
    private const double StruckBeeSize = 0.39;
    private const double StruckBeeX = 0.45;
    private const double StruckBeeY = -0.10;

    /// <summary>How much bigger than the mine in its own cell the struck bee starts. At 1 it grew out of the
    /// cell at exactly the size it had been sitting there, which is honest but leaves the beat that says
    /// "this one just stung you" to the flight alone — at this distance it barely registered. Above 1 the bee
    /// pops as it breaks loose, so the object that ends the run announces itself immediately.</summary>
    private const double StruckBeeStartScale = 3.0;

    /// <summary>How far the parked bee bobs, in field radii, and how fast in cycles per second.</summary>
    private const double StruckBeeBob = 0.035;
    private const double StruckBeeBobHz = 0.9;

    /// <summary>The bee that stung you, drawn over the scrim that greys the crop and under the card's words —
    /// everything else on a failed board is context for this one cell.
    ///
    /// <para>⚠ It does not leave. It flies its wandering arc off the cell, swells, and parks beside the
    /// results card, bobbing there until the board is exited or restarted — so the thing that ended the run
    /// is still on screen while the run is being read. The bees on a cleared board are the other case and
    /// still go off the crop; that is <see cref="DrawDepartingBee"/>, which this deliberately does not
    /// share.</para>
    ///
    /// <para>Always drawn over the crop, never masked by the petal it came out of: a mask keyed to the flight
    /// broke whenever the bee parked over its own tile, and the emergence it bought was not worth the edge cases.</para></summary>
    private static void DrawStruckBee(DrawingContext dc, Point c, double field, Point origin, double scale,
                                      Kabloom game)
    {
        if (game.Phase != Kabloom.Stage.Failed) return;
        int id = game.HitBeeCell;
        if (id < 0 || id >= game.Grid.Cells.Count || !game.Board.IsMine(id)) return;
        double reveal = game.RevealProgress(id);
        if (reveal < 0.22) return;

        var from = Screen(game.Grid.Cells[id].Center, origin, scale);
        // Parked on the side it can look toward the words from.
        var rest = new Point(c.X + StruckBeeSide * field * StruckBeeX, c.Y + field * StruckBeeY);

        double raw = Math.Clamp(Math.Max(0, game.HitBeeProgress), 0, 1);
        double dx = rest.X - from.X, dy = rest.Y - from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        double small = Math.Max(2.8, scale * 0.30) * StruckBeeStartScale;

        // A stacked petal lets out every bee it held. They leave one after another, sway on their own phases,
        // and park in a stagger behind the front one — each a step smaller and offset — drawn back to front so
        // the front bee, the one that carries the bolts, is on top.
        int bees = Math.Clamp(game.Board.BeeCount(id), 1, 3);
        dc.PushOpacity(Smooth(Math.Clamp((reveal - 0.22) / 0.35, 0, 1)));
        for (int k = bees - 1; k >= 0; k--)
        {
            // Later bees start later but all land together at the end of the flight.
            double lag = k * StruckBeeStagger;
            double t = Smooth(Math.Clamp((raw - lag) / Math.Max(0.05, 1 - lag), 0, 1));
            var park = new Point(rest.X - StruckBeeSide * field * StruckBeeStackDx * k, rest.Y - field * StruckBeeStackDy * k);
            var at = new Point(from.X + (park.X - from.X) * t, from.Y + (park.Y - from.Y) * t);

            // The wander runs across the flight and is pinned to zero at both ends, so the arc curves and
            // zigzags on the way but lands exactly on the parking spot rather than jittering around it.
            if (len > 1e-6)
            {
                double sway = Math.Sin(t * Math.PI * 2 * KabloomTuning.BeeWiggleWaves + id * 1.7 + k * 2.1)
                              * Math.Sin(t * Math.PI) * field * KabloomTuning.BeeWiggleAmplitude * (k % 2 == 0 ? 1 : -1);
                at = new Point(at.X - dy / len * sway, at.Y + dx / len * sway);
            }

            // Bobbing eases in with the arrival, so it is not already rocking while still crossing the board.
            at = new Point(at.X, at.Y + Math.Sin(game.PresentationTime * Math.Tau * StruckBeeBobHz + k * 0.9)
                                        * field * StruckBeeBob * t);

            double size = (small + (field * StruckBeeSize - small) * t) * Math.Pow(StruckBeeStackScale, k);
            DrawStruckBeeShadow(dc, at, size, t);
            // ⚠ No bolts until the front bee is parked at full size. Scaled down and dragged along the flight
            // they read as a bee that arrived already sparking, and their zigzag fought the flight's own wiggle
            // for the same silhouette. The per-bolt blink staggers their arrival, so this needs no fade of its own.
            if (k == 0 && t >= 1) DrawLightning(dc, at, size, game.PresentationTime, id);
            // Arrives a shade darker than its own tone and rises to it by the halfway point of the flight: the
            // bee reads as coming out of shadow, never as a silhouette. Untinted well before it parks, so the
            // hovering loop is never seen through a wash.
            DrawMine(dc, at, size, StruckBeeInkStart * Math.Clamp(1 - t / StruckBeeInkFade, 0, 1));
        }
        dc.Pop();
    }

    /// <summary>Share of the flight each further bee from a stacked petal waits before leaving.</summary>
    private const double StruckBeeStagger = 0.18;
    /// <summary>Where each further bee parks relative to the one in front: away from the card and upward, as
    /// fractions of the field radius, and how much smaller it is.</summary>
    private const double StruckBeeStackDx = 0.075;
    private const double StruckBeeStackDy = 0.065;
    private const double StruckBeeStackScale = 0.85;

    /// <summary>How far the struck bee's shadow trails it, as a fraction of the bee's own size, at full size.
    /// Pushed well out, so at full size the shadow clears the bee entirely
    /// rather than sitting half under it — which is what sells the bee as having risen off the board rather
    /// than grown flat against it.</summary>
    private const double StruckBeeShadowDrift = 1.365;

    /// <summary>How dark the struck bee starts, as a black wash from 0 to 1. Deliberately shallow — a bee
    /// that starts filled black reads as a hole in the card rather than as an insect arriving.</summary>
    private const double StruckBeeInkStart = 0.30;

    /// <summary>Share of the struck bee's flight over which it lightens to its own colours.
    /// ⚠ Well under 1 on purpose — the bee has to be fully itself before it parks,
    /// or the hovering loop plays under a wash and reads as a rendering fault rather than an entrance.</summary>
    private const double StruckBeeInkFade = 0.5;

    /// <summary>Which way the shadow falls, as a clock hour: 6 is straight down, 9 straight left. At
    /// <b>7</b> — down and a shade left — this scene's light sits up and to the right.
    ///
    /// <para>⚠ Stated as an hour and turned into a vector at use, rather than as a bare
    /// <c>+drift, +drift</c> diagonal: a direction stated as a sign pair is one nobody can change without
    /// first working out which sign is which.</para>
    ///
    /// <para>⚠ It disagrees with the house lighting on purpose. The wheel's materials and Connate's glass are
    /// all lit 16° from the upper left; this scene is lit from the right. Don't "correct" it to match — and
    /// don't take the mismatch as licence to relight anything else.</para></summary>
    private const double StruckBeeShadowHour = 7.0;

    /// <summary>How wide the fallback blob spreads past the bee, and how dark the shadow sits at full size.
    /// The shadow is the depth cue that lifts the bee off the failure card, so it has to stay faint enough to
    /// read as cast light rather than as a second bee in black.</summary>
    private const double StruckBeeShadowSpread = 1.22;
    private const byte StruckBeeShadowInk = 0x1B;

    /// <summary>Stacked silhouettes that stand in for a blur, and how far the outermost one grows past the
    /// bee. ⚠ Each is drawn at <see cref="StruckBeeShadowInk"/> divided by the layer count, so the stack
    /// lands near that ink rather than several times it. WPF's <c>BlurEffect</c> is not an option here: an
    /// effect applies to a Visual, and this is a renderer painting straight into a DrawingContext.</summary>
    private const int StruckBeeShadowLayers = 4;
    private const double StruckBeeShadowFuzz = 0.20;

    /// <summary>A soft cast shadow under the struck bee, offset further the larger the bee has grown — which
    /// is what makes it read as rising off the board rather than swelling flat against it. Growth and offset
    /// are the same term, so a bee at its start size sits almost on its own shadow.
    ///
    /// <para><paramref name="grown"/> is the flight progress, 0 breaking loose and 1 parked — the same term
    /// the bee's size ramps on, so offset and growth cannot drift apart.</para>
    ///
    /// <para>It is the bee's own silhouette, not a blob: <see cref="ArcadeSprites.Tint"/> masks a black fill
    /// through the art's alpha, and the art asked for is the same frame <see cref="DrawMine"/> is about to
    /// draw — so the shadow beats its wings with the bee instead of sitting under it as a disc. Softened by
    /// stacking a few copies at growing size rather than by a blur; see
    /// <see cref="StruckBeeShadowLayers"/>.</para>
    ///
    /// <para>Falls back to a fuzzed radial blob when there is no art, because the vector bee has no alpha to
    /// take a silhouette from.</para></summary>
    private static void DrawStruckBeeShadow(DrawingContext dc, Point at, double size, double grown)
    {
        grown = Math.Clamp(grown, 0, 1);
        double drift = size * StruckBeeShadowDrift * grown;
        // The hour as a direction: 0 rad is straight down the screen, and each hour past 6 swings 30° toward
        // the left. Screen Y grows downward, so "down" is +cos and "left" is −sin.
        double swing = (StruckBeeShadowHour - 6) * 30 * Math.PI / 180;
        var centre = new Point(at.X - drift * Math.Sin(swing), at.Y + drift * Math.Cos(swing));
        double ink = StruckBeeShadowInk / 255.0 * grown;
        if (ink <= 0.004 || size < 1) return;

        // ⚠ One image where there is one. The stack below fakes a blur with four tinted copies of the bee,
        // and each copy is an opacity mask — four intermediate render surfaces over a region as wide as the
        // whole bee, measured at 54.7 ms against 17.9 ms for a single pre-blurred draw. It is also the coarser
        // picture: four hard steps against a real falloff.
        //
        // ⚠ The shadow's canvas is larger than the bee's, because the bee's wings touch the edge of its own
        // and a blur inside that canvas would be sliced off in a straight line down both sides. The pad is
        // read from the ratio rather than agreed as a constant: whatever `tools/SpriteShadowBake` used, the
        // box scales by it and the silhouette lands back exactly on the body. That is deliberate — a pad held
        // in two places is the kind of coupling that goes stale silently, and a shadow half a body off is
        // hard to read as a bug rather than as art.
        // ⚠ Equal cycle lengths is a condition, not a hope. Both frames are asked for at the same phase
        // and the same fps, so the frame index is shared and the shadow is its bee's frame — but only while
        // the cycles are the same length. Add a fifth bee frame without a fifth shadow and the two run at
        // different periods and slide against each other, which reads as the shadow lagging the wings
        // rather than as a missing file. Mismatched, this falls through to the stack below and stays right.
        // Paired too: no shadow image without the body art it was derived from.
        if (ArcadeSprites.Cycle(ArcadeSprites.Slot.KabloomBee).Length
                == ArcadeSprites.Cycle(ArcadeSprites.Slot.KabloomBeeShadow).Length
            && ArcadeSprites.Frame(ArcadeSprites.Slot.KabloomBee, 0, BeeFps) is { } body
            && ArcadeSprites.Frame(ArcadeSprites.Slot.KabloomBeeShadow, 0, BeeFps) is { } cast
            && body.PixelWidth > 0)
        {
            double pad = cast.PixelWidth / (double)body.PixelWidth;
            dc.PushOpacity(Math.Min(1, ink));
            ArcadeSprites.Draw(dc, cast, ArcadeSprites.Box(centre, size * BeeArtHalf * pad));
            dc.Pop();
            return;
        }

        if (ArcadeSprites.Frame(ArcadeSprites.Slot.KabloomBee, 0, BeeFps) is { } art)
        {
            // Outermost (largest, faintest) first so the crisper inner copies land on top. One layer would be
            // a hard cut-out; the growing stack is what gives the edge its falloff.
            // ⚠ Fallback only: prefer KabloomBeeShadow. Kept for a board with no shadow art supplied.

            double per = ink / StruckBeeShadowLayers;
            for (int i = StruckBeeShadowLayers - 1; i >= 0; i--)
            {
                double t = StruckBeeShadowLayers == 1 ? 0 : i / (double)(StruckBeeShadowLayers - 1);
                double grow = 1 + StruckBeeShadowFuzz * t;
                var box = ArcadeSprites.Box(centre, size * BeeArtHalf * grow);
                ArcadeSprites.Tint(dc, art, box, Colors.Black, per);
            }
            return;
        }

        double r = size * StruckBeeShadowSpread;
        // ⚠ Built per call, not cached: a RadialGradientBrush is mapped RelativeToBoundingBox by default, so
        // one frozen brush would serve every size — but the stops are what carry the fuzz, and they are what
        // the grown term has to soften. A shadow with fixed stops reads as a hard disc at the start size.
        var fuzz = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(255 * ink), 0, 0, 0), 0.00),
                new GradientStop(Color.FromArgb((byte)(255 * ink * 0.72), 0, 0, 0), 0.42),
                new GradientStop(Color.FromArgb((byte)(255 * ink * 0.22), 0, 0, 0), 0.74),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.00),
            },
        };
        fuzz.Freeze();
        dc.DrawEllipse(fuzz, null, centre, r, r);
    }

    /// <summary>The analog reticle: a small black ring. Keep it dark and distinct from the ivory focus
    /// outline it sits inside, or at speed the two read as one smeared selection. It stays small because the
    /// cell it picks is already outlined, and a heavy cursor competes for that job.</summary>
    private static void DrawPointer(DrawingContext dc, Point board, Point origin, double scale, double time)
    {
        var p = new Point(origin.X + board.X * scale, origin.Y + board.Y * scale);
        double breathe = 1 + Math.Sin(time * 4.2) * 0.06;
        // Hollow: a filled dot at this size would cover the very cell it is selecting — the ring lets the
        // tile's own number and colour read straight through it.
        double r = Math.Max(4, scale * 0.3575) * breathe;
        var ring = new Pen(KabloomPalette.PointerInk, 1.5); ring.Freeze();
        // A sage ring just outside the black, so the reticle survives a dark revealed petal and reads as one
        // of the game's own marks rather than as a stray highlight.
        var halo = new Pen(KabloomPalette.PointerRim, 3.0); halo.Freeze();
        dc.DrawEllipse(null, halo, p, r, r);
        dc.DrawEllipse(null, ring, p, r, r);
    }

    /// <summary>One petal's flip, 1 → 0 → 1 (its vertical scale), and whether it is still showing the old
    /// cleared face. The delay from the centre is what makes the wave; everything else is a cosine.</summary>
    private static double PetalFlip(Kabloom game, KabloomCell cell, double elapsed, out bool showCleared)
    {
        double distance = cell.Center.Length / Math.Max(1e-6, game.Grid.Radius);
        double delay = Math.Clamp(distance, 0, 1) * KabloomTuning.LevelTransitionWaveSeconds;
        double local = (elapsed - delay) / Math.Max(0.05, KabloomTuning.LevelTransitionFlipSeconds);
        if (local <= 0) { showCleared = true; return 1; }      // the wave hasn't reached it yet
        if (local >= 1) { showCleared = false; return 1; }     // settled as a fresh covered petal
        showCleared = local < 0.5;
        return Math.Abs(Math.Cos(local * Math.PI));
    }

    /// <summary>A subtle brownish-red shading toward the "hub" side of a covered petal — the vertex nearest
    /// the crop's centre, which is also where a diamond sits when this cell contributes to one.
    ///
    /// <para><paramref name="hubVertex"/> is <see cref="KabloomCell.Vertices"/>'s <b>index 0</b> in screen
    /// space — the same vertex <see cref="PetalVertices"/> cuts toward and <c>KabloomCenterCap.Center</c> is
    /// built from, so "the hub spot" here is literally the same point the diamond animation aims at.</para>
    ///
    /// <para>The falloff is anchored so it is gone by the three-quarter point of the tile's own extent, then
    /// held transparent — the remaining quarter stays a solid colour rather than trailing off across the whole
    /// shape.</para></summary>
    private static void DrawHubShading(DrawingContext dc, Geometry path, Point hubVertex)
    {
        Rect bounds = path.Bounds;
        if (bounds.IsEmpty) return;
        // The hub vertex sits at one edge of the petal's bounding box, so the box's own larger dimension is a
        // fair stand-in for "the tile's extent measured from that vertex" without needing the far vertices.
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        // The geometry itself is already the petal's true shape, so filling it directly needs no clip.
        dc.DrawGeometry(HubShade(bounds, hubVertex), null, path);
    }

    /// <summary>The hub-shading brush for one petal, cached.
    ///
    /// <para>⚠ This was the single largest allocation source in the arcade: a <c>RadialGradientBrush</c> with
    /// its stop collection, per covered petal, per frame — around 1,200 objects a frame on a 240-cell crop.
    /// It had to be rebuilt because <c>Absolute</c> mapping bakes the hub's screen position and the reach
    /// into the brush.</para>
    ///
    /// <para>The fix is <c>RelativeToBoundingBox</c>, the same trick <c>ConnatePalette.Gloss</c> uses: the
    /// geometry being filled supplies the extent, so a brush can be shared. ⚠ It is an exact swap, not an
    /// approximation — relative <c>RadiusX = reach / width</c> is multiplied back by the width when mapped,
    /// so the falloff is the same circle of radius <c>reach</c> it always was.</para>
    ///
    /// <para>Only the hub's position within the box and the box's aspect vary, and petals come in a handful
    /// of orientations: measured across every campaign level's crop, 489 petals need <b>6</b> brushes.
    /// Keying on the measured geometry rather than on a petal index is deliberate — it cannot go stale if the
    /// grid changes shape.</para>
    ///
    /// <para>⚠ The centre is signed and the radii are not, and that asymmetry is load-bearing. The petal's
    /// outline is cut back from the hub by the centre cap, so the hub sits outside its own bounding box and
    /// the relative centre goes negative. Quantising it unsigned clamped those to the box edge and moved the
    /// shading's origin by up to 12 px. Signed over [−2, +2] at 1/64 brings the worst case to 0.69 px, with
    /// the radii under [0, 4] at 1/64 worst 0.48 px — sub-pixel on a 46 px petal with a soft falloff.</para></summary>
    private static Brush HubShade(Rect bounds, Point hubVertex)
    {
        double reach = Math.Max(bounds.Width, bounds.Height);
        int cx = QSigned((hubVertex.X - bounds.X) / bounds.Width);
        int cy = QSigned((hubVertex.Y - bounds.Y) / bounds.Height);
        // ⚠ Saturates past 3.98, which would need a petal four times taller than wide. The gradient would
        // read a shade small rather than wrong, and no crop in the campaign comes near it.
        int rx = QPlain(reach / bounds.Width);
        int ry = QPlain(reach / bounds.Height);
        int key = cx | cy << 8 | rx << 16 | ry << 24;
        if (_hubShade.TryGetValue(key, out var hit)) return hit;

        // ⚠ Built from the quantised values, so the cached brush is exactly what its key describes.
        var centre = new Point(cx / 64.0 - 2, cy / 64.0 - 2);
        var shade = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = centre, GradientOrigin = centre,
            RadiusX = rx / 64.0, RadiusY = ry / 64.0,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x36, 0x3A, 0x1E, 0x14), 0.00),
                new GradientStop(Color.FromArgb(0x00, 0x3A, 0x1E, 0x14), 0.75),
                new GradientStop(Color.FromArgb(0x00, 0x3A, 0x1E, 0x14), 1.00),
            },
        };
        shade.Freeze();
        // Bounded by petal shape, not by frames; the guard is for a pathological crop.
        if (_hubShade.Count < 512) _hubShade[key] = shade;
        return shade;

        static int QSigned(double v) => (int)Math.Clamp(Math.Round((v + 2) * 64), 0, 255);
        static int QPlain(double v) => (int)Math.Clamp(Math.Round(v * 64), 0, 255);
    }

    private static readonly Dictionary<int, Brush> _hubShade = [];

    /// <summary>One petal: its fill and grid edge, then replacement art clipped to that petal and turned so
    /// the art's up runs hub-outward, then the edge again over the top.
    ///
    /// <para>With no art this is exactly the fill-plus-shading it replaced, down to the draw order — the
    /// second stroke and the shading are mutually exclusive, so a board with no sprites pays one branch and
    /// nothing else.</para></summary>
    private static void DrawPetalFace(DrawingContext dc, Geometry path, Vec2[] outline, Point origin,
                                      double scale, KabloomCell cell, Brush fill, string? slot, bool shade, Brush? tint)
    {
        dc.DrawGeometry(fill, KabloomPalette.TilePen, path);

        // A stable per-petal offset, so a supplied cycle does not pulse the whole crop as one sheet.
        var art = slot is null ? null : ArcadeSprites.Frame(slot, cell.Id * 0.6180339887 % 1);
        if (art is null)
        {
            if (shade) DrawHubShading(dc, path, Screen(cell.Vertices[0], origin, scale));
            if (tint is not null) dc.DrawGeometry(tint, null, path);
            return;
        }

        var hub = Screen(cell.Vertices[0], origin, scale);
        var mid = Screen(cell.Center, origin, scale);
        double dx = mid.X - hub.X, dy = mid.Y - hub.Y;
        if (dx * dx + dy * dy < 1e-9) return;
        // The turn that sends the art's up (screen −Y) along hub → centroid, which is the petal's own axis.
        double ang = Math.Atan2(dx, -dy) * 180 / Math.PI;
        var box = TurnedBounds(outline, origin, scale, ang, mid);
        if (box.Width <= 0.5 || box.Height <= 0.5) return;

        dc.PushClip(path);
        dc.PushTransform(new RotateTransform(ang, mid.X, mid.Y));
        // Every face at full strength, the mine-hit one included: a face washed out over its fill reads as a
        // blacked-out tile rather than as a petal that changed.
        ArcadeSprites.Draw(dc, art, box, ArcadeSprites.Fit.Cover);
        dc.Pop();
        dc.Pop();
        if (tint is not null) dc.DrawGeometry(tint, null, path);
        // ⚠ Re-stroked over the art. The edge is how a crop reads as separate cells at all, and the fill's
        // own stroke is half buried by anything drawn inside the clip.
        dc.DrawGeometry(null, KabloomPalette.TilePen, path);
    }

    /// <summary>Which petal art a cell's state calls for, or null for the vector face. Mirrors
    /// <see cref="TileFill"/>'s branches exactly — the two must never disagree about what a petal is.
    ///
    /// <para>Falls back the way a partial set wants: the checker's second shade to its first, and the struck
    /// bee to the vector face. ⚠ A cleared petal has no art: it returns null outright, so a cleared cell is
    /// always the plain dark fill.</para></summary>
    private static string? PetalSlot(Kabloom game, KabloomCell cell, bool visuallyRevealed, bool showCleared,
                                     bool focused)
    {
        // ⚠ A cleared petal wears no art, focused or not. Its slots are gone, and null here is what puts the
        // vector face back: the dark fill a clue number is read off, plus its stroke. That face is the
        // ground the numeral has to stay legible against, and it does that job better plain than under a
        // drawing.
        if (showCleared) return null;
        if (visuallyRevealed)
            // The struck bee's own face is the exception and survives: on the failure board that cell is the
            // whole lesson. With no hit art supplied it now falls through to the vector face, not to a
            // cleared sprite.
            return game.Phase == Kabloom.Stage.Failed && game.Board.IsMine(cell.Id)
                   && ArcadeSprites.Has(ArcadeSprites.Slot.KabloomPetalHit)
                ? ArcadeSprites.Slot.KabloomPetalHit
                : null;

        if (focused && ArcadeSprites.Has(ArcadeSprites.Slot.KabloomPetalFocus))
            return ArcadeSprites.Slot.KabloomPetalFocus;
        if (((cell.FlowerQ - cell.FlowerR + cell.Petal) & 1) == 0) return ArcadeSprites.Slot.KabloomPetal;
        return ArcadeSprites.Has(ArcadeSprites.Slot.KabloomPetalAlt)
            ? ArcadeSprites.Slot.KabloomPetalAlt : ArcadeSprites.Slot.KabloomPetal;
    }

    /// <summary>The petal's extent once turned upright, as the box its art fills. Measured off the six
    /// outline points rather than by rotating and re-bounding the geometry: this runs per cell on a crop
    /// that can carry a couple of hundred of them, so it allocates nothing.</summary>
    private static Rect TurnedBounds(Vec2[] outline, Point origin, double scale, double angDeg,
                                     Point about)
    {
        double a = -angDeg * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var v in outline)
        {
            var p = Screen(v, origin, scale);
            double dx = p.X - about.X, dy = p.Y - about.Y;
            double rx = about.X + dx * cos - dy * sin, ry = about.Y + dx * sin + dy * cos;
            if (rx < x0) x0 = rx;
            if (ry < y0) y0 = ry;
            if (rx > x1) x1 = rx;
            if (ry > y1) y1 = ry;
        }
        return x1 <= x0 || y1 <= y0 ? Rect.Empty : new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    private static Brush TileFill(Kabloom game, KabloomCell cell, bool visuallyRevealed)
    {
        int id = cell.Id;
        Brush covered = ((cell.FlowerQ - cell.FlowerR + cell.Petal) & 1) == 0
            ? KabloomPalette.CoveredA : KabloomPalette.CoveredB;
        // ⚠ The struck petal is not blacked out. It swaps to its own hit face and keeps the covered tint
        // under it, so any transparency in that art reads as petal rather than as a hole in the crop.
        if (visuallyRevealed && game.Phase == Kabloom.Stage.Failed && game.Board.IsMine(id)) return covered;
        if (visuallyRevealed) return KabloomPalette.Revealed;
        return covered;
    }

    private static void DrawCellContents(DrawingContext dc, Point origin, double scale, Kabloom game, double ppd,
                                         double[] lifts)
    {
        // One board unit in pixels — the bee flight's reach and wiggle are both expressed in board radii, so
        // a departure looks the same on a 40-cell crop and a 240-cell one.
        double unit = game.Grid.Radius * scale;
        for (int id = 0; id < game.Grid.Cells.Count; id++)
        {
            Point p = Screen(game.Grid.Cells[id].Center, origin, scale);
            double reveal = game.RevealProgress(id);
            double mineClear = game.MineClearProgress(id);
            bool showMine = game.Board.IsMine(id) && game.Phase == Kabloom.Stage.Failed;

            // ── A bee that is done here leaves ──
            // On a cleared board every bee swells and flies off on its own wandering path; it does not flare
            // and fade in place, which would read as "removed" rather than as a creature going home.
            if (mineClear >= 0)
            {
                DrawDepartingBee(dc, p, origin, unit, scale, mineClear, id, game.PresentationTime, false);
                continue;
            }
            if (showMine && reveal >= 0.22)
            {
                // Only the bee you actually set off is angry, and only it leaves. The rest are simply
                // revealed — making all of them buzz turns the failure screen into noise and buries the one
                // cell the player needs to learn from.
                // ⚠ The struck one is skipped here and drawn last of everything — see DrawStruckBee. It is
                // the single cell the player has to read off a failed board, and this pass sits under the
                // scrim and the card.
                if (id == game.HitBeeCell) continue;
                dc.PushOpacity(Smooth(Math.Clamp((reveal - 0.22) / 0.35, 0, 1)));
                DrawDepartingBee(dc, p, origin, unit, scale, 0, id, game.PresentationTime, false);
                dc.Pop();
                continue;
            }
            if (game.Board.IsFlagged(id) && !game.Board.IsRevealed(id) && mineClear < 0)
            {
                DrawBeeFlag(dc, Raised(p, lifts[id]), Math.Max(3.5, scale * 0.425), game.FlagProgress(id), game.Board.FlagCount(id), ppd);
                continue;
            }
            if (game.Board.IsMarked(id) && !game.Board.IsRevealed(id) && mineClear < 0)
            {
                DrawQuestionFlag(dc, Raised(p, lifts[id]), Math.Max(3.5, scale * 0.425), game.FlagProgress(id), ppd);
                continue;
            }
            if (!game.Board.IsRevealed(id) || reveal < 0.28) continue;

            int clue = game.Board.Clue(id);
            if (clue <= 0) continue;
            double size = Math.Max(6.5, scale * 0.64);
            var (text, ink) = ClueGlyph(clue, size, ppd);
            double settle = Smooth(Math.Clamp((reveal - 0.28) / 0.42, 0, 1));
            // ⚠ Skip PushOpacity once settled: every cell on a full board would otherwise push an opacity
            // layer of exactly 1, ~70 of them a frame.
            bool fading = settle < 0.999;
            if (fading) dc.PushOpacity(settle);
            ArcadeChrome.DrawInkCentered(dc, text, p, ink);
            if (fading) dc.Pop();
        }
    }

    private static void DrawRevealPetal(DrawingContext dc, Geometry path, Point center, double progress,
                                        int cellId, double tileScale, Brush fill)
    {
        double appear = Smooth(Math.Clamp(progress / 0.42, 0, 1));
        double scale = progress < 0.42
            ? 0.72 + BackOut(progress / 0.42) * 0.40
            : 1.12 - Smooth((progress - 0.42) / 0.58) * 0.12;
        double turn = ((cellId & 1) == 0 ? -1 : 1) * (1 - appear) * 3.5;

        dc.PushOpacity(appear);
        dc.PushTransform(new RotateTransform(turn, center.X, center.Y));
        dc.PushTransform(new ScaleTransform(scale, scale, center.X, center.Y));
        dc.DrawGeometry(fill, KabloomPalette.TilePen, path);
        dc.Pop();
        dc.Pop();
        dc.Pop();

        double burst = Math.Sin(Math.Clamp(progress / 0.62, 0, 1) * Math.PI);
        if (burst <= 0.02) return;
        double haloRadius = tileScale * (0.22 + progress * 0.68);
        // Cached, quantised — a cascade can have dozens of petals mid-reveal on the same frame.
        byte alpha = (byte)(150 * burst);
        var haloPen = KabloomPalette.WhitePen(alpha, Math.Max(0.8, tileScale * 0.045));
        dc.DrawEllipse(null, haloPen, center, haloRadius, haloRadius);

        for (int spark = 0; spark < 5; spark++)
        {
            double angle = cellId * 1.61803398875 + spark * Math.PI * 2 / 5.0;
            double inner = tileScale * (0.20 + progress * 0.34);
            double outer = inner + tileScale * (0.10 + 0.14 * burst);
            dc.DrawLine(haloPen,
                new Point(center.X + Math.Cos(angle) * inner, center.Y + Math.Sin(angle) * inner),
                new Point(center.X + Math.Cos(angle) * outer, center.Y + Math.Sin(angle) * outer));
        }
    }

    private static Vec2[] PetalVertices(KabloomCell cell)
    {
        Vec2 centerVertex = cell.Vertices[0];
        Vec2 cutTowardFirst = AtDistance(centerVertex, cell.Vertices[1], KabloomTuning.CenterCapRadiusInGridUnits);
        Vec2 cutTowardLast = AtDistance(centerVertex, cell.Vertices[4], KabloomTuning.CenterCapRadiusInGridUnits);
        return [cutTowardFirst, cell.Vertices[1], cell.Vertices[2], cell.Vertices[3], cell.Vertices[4], cutTowardLast];
    }

    // ── The petal outlines and paths, built once per board rather than per frame ──────────────────────────
    // A 240-cell crop is 240 vertex arrays, 240 PathFigures, 1,440 LineSegments and 240 PathGeometries —
    // the largest allocation left in the arcade, and not one of them changes while the board, the origin
    // and the scale hold. ⚠ The geometries are frozen, so handing the same instances to every frame is
    // safe. A level transition animates `scale` and so rebuilds every frame, which is correct: the petals
    // really are moving then. The outlines are kept alongside because replacement art needs each petal's
    // extent in its own turned frame, cheaper to measure off six points than to read back off the path.
    private static void PetalPaths(Kabloom k, Point origin, double scale,
                                   out Geometry[] paths, out Vec2[][] outlines, out int[] topDown,
                                   out int[] gemsTopDown)
    {
        int count = k.Grid.Cells.Count;
        if (!ReferenceEquals(_pathGrid, k.Grid) || _pathOrigin != origin || _pathScale != scale
            || _pathShape.Length != count)
        {
            _pathGrid = k.Grid;
            _pathOrigin = origin;
            _pathScale = scale;
            _pathShape = new Geometry[count];
            _pathOutline = new Vec2[count][];
            for (int i = 0; i < count; i++)
            {
                _pathOutline[i] = PetalVertices(k.Grid.Cells[i]);
                _pathShape[i] = Polygon(_pathOutline[i], origin, scale);
            }
            _pathSide = new Geometry?[count];
            var cells = k.Grid.Cells;
            _pathTopDown = Enumerable.Range(0, count).OrderBy(i => cells[i].Center.Y).ToArray();
            var caps = k.Grid.CenterCaps;
            _gemsTopDown = Enumerable.Range(0, caps.Count).OrderBy(i => caps[i].Center.Y).ToArray();
        }
        paths = _pathShape;
        outlines = _pathOutline;
        topDown = _pathTopDown;
        gemsTopDown = _gemsTopDown;
    }

    private static object? _pathGrid;
    private static Point _pathOrigin;
    private static double _pathScale = double.NaN;
    private static Geometry[] _pathShape = [];
    private static Vec2[][] _pathOutline = [];
    private static int[] _pathTopDown = [];
    private static int[] _gemsTopDown = [];
    /// <summary>Each petal's side at full thickness, built on first use; see <see cref="DrawRaisedPetal"/>.</summary>
    private static Geometry?[] _pathSide = [];

    /// <summary>How far each petal's face stands up the screen this frame, in pixels: full thickness while
    /// covered, zero once cleared, and sinking with the reveal between so the punch-through lands on a petal
    /// that has already come down to meet it. A flip showing the old cleared face is flat.
    ///
    /// <para>⚠ One buffer reused across frames — the caller must not keep it past the frame.</para></summary>
    private static double[] PetalLifts(Kabloom k, double scale, double transition, bool transitioning)
    {
        int count = k.Grid.Cells.Count;
        if (_lifts.Length != count) _lifts = new double[count];
        double full = KabloomTuning.TileThicknessInGridUnits * scale;
        for (int i = 0; i < count; i++)
        {
            double reveal = k.RevealProgress(i);
            double mineClear = k.MineClearProgress(i);
            double lift;
            if ((k.Board.IsRevealed(i) && reveal >= 1) || mineClear >= 1) lift = 0;
            else
            {
                double presentation = Math.Max(reveal, mineClear);
                lift = presentation is >= 0 and < 1 ? full * (1 - Smooth(presentation)) : full;
            }
            if (transitioning)
            {
                PetalFlip(k, k.Grid.Cells[i], transition, out bool showCleared);
                if (showCleared) lift = 0;
            }
            _lifts[i] = lift;
        }
        return _lifts;
    }

    private static double[] _lifts = [];

    private static Point Raised(Point p, double lift) => new(p.X, p.Y - lift);

    /// <summary>A frozen upward shift. The full thickness repeats for every covered petal and resting gem on
    /// a frame, so the last one is kept; a petal part-way through sinking gets its own.</summary>
    private static Transform LiftTransform(double lift)
    {
        if (_liftTransform is not null && _liftTransform.Y == -lift) return _liftTransform;
        var t = new TranslateTransform(0, -lift);
        t.Freeze();
        return _liftTransform = t;
    }

    private static TranslateTransform? _liftTransform;

    /// <summary>A petal at its <paramref name="lift"/>: the side first — the petal swept straight down the
    /// screen from its raised face to its footprint — then the face over it. Flat, it is just the face.
    ///
    /// <para>The side's outline is the convex hull of the face and its footprint, which is the whole sweep
    /// because a petal is convex. Cached per petal at full thickness (the board's resting state); a petal
    /// part-way through sinking builds its own for the frame.</para></summary>
    private static void DrawRaisedPetal(DrawingContext dc, Geometry path, Vec2[] outline, Point origin,
                                        double scale, KabloomCell cell, Brush fill, string? slot, bool shade,
                                        double lift, Brush? tint)
    {
        if (lift <= 0.05)
        {
            DrawPetalFace(dc, path, outline, origin, scale, cell, fill, slot, shade, tint);
            return;
        }
        double full = KabloomTuning.TileThicknessInGridUnits * scale;
        Geometry side;
        if (Math.Abs(lift - full) < 1e-6 && cell.Id < _pathSide.Length)
            side = _pathSide[cell.Id] ??= SweepDown(outline, origin, scale, lift);
        else
            side = SweepDown(outline, origin, scale, lift);
        dc.DrawGeometry(KabloomPalette.PetalSide, KabloomPalette.TilePen, side);
        dc.PushTransform(Math.Abs(lift - full) < 1e-6 ? LiftTransform(lift) : new TranslateTransform(0, -lift));
        DrawPetalFace(dc, path, outline, origin, scale, cell, fill, slot, shade, tint);
        dc.Pop();
    }

    /// <summary>The screen-space region a convex outline covers as it slides from <paramref name="lift"/>
    /// pixels up down to where it lies: the hull of both copies.</summary>
    private static Geometry SweepDown(Vec2[] outline, Point origin, double scale, double lift)
    {
        var pts = new List<Point>(outline.Length * 2);
        foreach (var v in outline)
        {
            var p = Screen(v, origin, scale);
            pts.Add(p);
            pts.Add(new Point(p.X, p.Y - lift));
        }
        return Polygon(ConvexHull(pts));
    }

    private static Geometry SweepDown(List<Point> face, double lift)
    {
        var pts = new List<Point>(face.Count * 2);
        foreach (var p in face) { pts.Add(p); pts.Add(new Point(p.X, p.Y - lift)); }
        return Polygon(ConvexHull(pts));
    }

    /// <summary>Andrew's monotone chain, counter-clockwise, collinear points dropped.</summary>
    private static List<Point> ConvexHull(List<Point> pts)
    {
        pts.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var hull = new List<Point>(pts.Count + 1);
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            for (int n = 0; n < pts.Count; n++)
            {
                Point p = pts[pass == 0 ? n : pts.Count - 1 - n];
                while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;

        static double Cross(Point o, Point a, Point b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
    }

    /// <summary>The covered-petal colour sweep as one board-wide brush: honey at the crop's centre fading to
    /// clear by mid-radius, then violet building toward the rim. Absolute-mapped, so every petal samples its
    /// own spot on the one gradient. Cached; rebuilt only when the board moves or scales, which is every frame
    /// of a level flip and never otherwise.</summary>
    private static Brush PetalTint(Point centre, double radius)
    {
        if (_tint is not null && _tintCentre == centre && _tintRadius == radius) return _tint;
        Color honey = KabloomPalette.PetalTintCentre, violet = KabloomPalette.PetalTintRim;
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = centre, GradientOrigin = centre,
            RadiusX = Math.Max(1, radius), RadiusY = Math.Max(1, radius),
            // ⚠ Two stops at the midpoint, each fully transparent in its own hue: WPF interpolates straight
            // (not premultiplied) colour, so one shared transparent stop would drag a grey band through it.
            GradientStops =
            {
                new GradientStop(honey, 0.00),
                new GradientStop(Color.FromArgb(0, honey.R, honey.G, honey.B), 0.50),
                new GradientStop(Color.FromArgb(0, violet.R, violet.G, violet.B), 0.50),
                new GradientStop(violet, 1.00),
            },
        };
        brush.Freeze();
        _tint = brush;
        _tintCentre = centre;
        _tintRadius = radius;
        return brush;
    }

    private static Brush? _tint;
    private static Point _tintCentre;
    private static double _tintRadius = double.NaN;


    private static Vec2 AtDistance(Vec2 from, Vec2 toward, double distance)
    {
        Vec2 delta = toward - from;
        double scale = Math.Min(0.45, distance / Math.Max(1e-9, delta.Length));
        return from + delta * scale;
    }

    /// <summary>
    /// The selection outline: a steady black stroke with two light-grey sparks riding it, 180° apart, each
    /// trailing a tail.
    ///
    /// <para>⚠ Don't pulse the whole outline's opacity — that makes the selection periodically <i>harder</i>
    /// to see and competes with the reveal animations on a dense floret grid. A constant stroke plus moving
    /// highlights keeps the cell unambiguous at every instant.</para>
    ///
    /// <para>The sparks walk the outline's real geometry via <see cref="PathGeometry.GetPointAtFractionLength"/>,
    /// so they follow a pentagon's corners exactly rather than orbiting an approximating circle.</para>
    /// </summary>
    private static void DrawFocusOutline(DrawingContext dc, Geometry outline, double time)
    {
        dc.DrawGeometry(null, KabloomPalette.FocusStrokePen, outline);

        // Flatten once: GetPointAtFractionLength needs a PathGeometry, and the cell paths are already simple
        // polygons, so this is cheap and exact rather than an approximation.
        PathGeometry path = outline as PathGeometry ?? PathGeometry.CreateFromGeometry(outline);
        if (path is null || path.Figures.Count == 0) return;

        const int tailSegments = 7;          // enough to read as a tail, few enough to stay a hairline
        const double tailLength = 0.085;     // fraction of the outline's perimeter
        double head = (time * KabloomTuning.FocusSparkRevolutionsPerSecond) % 1.0;

        for (int spark = 0; spark < 2; spark++)
        {
            // The two sparks sit exactly opposite each other, so the cell always has a highlight on both
            // sides however the outline is oriented.
            double baseFraction = head + spark * 0.5;
            for (int segment = 0; segment < tailSegments; segment++)
            {
                double t = segment / (double)(tailSegments - 1);
                double fraction = baseFraction - t * tailLength;
                fraction -= Math.Floor(fraction);   // wrap into [0,1) for negative values too
                path.GetPointAtFractionLength(fraction, out Point p, out _);

                // Head is brightest and fattest; the tail fades and thins to nothing.
                double falloff = 1 - t;
                double radius = KabloomTuning.FocusSparkRadius * (0.35 + 0.65 * falloff);
                // The whole spark ring runs dim: it is a sign of life on the selected cell, and at full
                // strength it competes with the analog cursor sitting on the same tile.
                dc.PushOpacity(Math.Clamp(falloff * falloff, 0, 1) * KabloomTuning.FocusSparkOpacity);
                dc.DrawEllipse(KabloomPalette.FocusSpark, null, p, radius, radius);
                dc.Pop();
            }
        }
    }

    /// <summary>The nectar gems a player has just freed — swelling and flying. Drawn over the selector outline
    /// and the cursor: they are the reward beat, and a stone the size of several cells passing behind a petal
    /// outline read as a bug. Resting gems are drawn in the petal pass instead (<see cref="DrawRestingGem"/>).</summary>
    private static void DrawDiamonds(DrawingContext dc, Point boardOrigin, double scale, Point fieldCenter,
                                     double field, Kabloom game, double ppd)
    {
        Point target = DiamondCounterPoint(fieldCenter, field);
        for (int diamond = 0; diamond < game.Grid.CenterCaps.Count; diamond++)
        {
            double progress = game.DiamondProgress(diamond);
            if (progress >= 1) continue;
            // Resting is progress < 0 and swell < 0 — the petal pass draws those.
            if (progress < 0 && game.DiamondSwell(diamond) < 0) continue;
            // A freed stone leaves from where it stood, raised with the board, so it doesn't drop as it frees.
            double gemLift = KabloomTuning.TileThicknessInGridUnits * scale;
            Point source = Raised(Screen(game.Grid.CenterCaps[diamond].Center, boardOrigin, scale), gemLift);
            double size = Math.Max(3.1, scale * 0.235);
            if (progress < 0)
            {
                double swell = game.DiamondSwell(diamond);

                // ── Freed, it spins up and swells before it flies ──
                // Eased so the growth is unhurried at first and arrives at full size right as the flight
                // takes it — a linear swell peaks in the middle and then just sits there being big.
                double eased = Smooth(swell);
                double grown = 1 + (KabloomTuning.DiamondSwellScale - 1) * eased;
                // ⚠ No spin while it swells. The stone grows in place, still, and only starts turning once it
                // is on its way — the growth is the beat, and a stone winding up as well was two things
                // happening where one reads. Held at zero so the flight below can pick it up without a jump.
                const double spin = 0;
                // A halo opening around it — the stone still outgrows its cell, and without something
                // acknowledging that it reads as a drawing error rather than a reward.
                double halo = size * grown * (1.15 + eased * 0.35);
                dc.PushOpacity(Math.Clamp(1 - eased * 0.65, 0, 1));
                dc.DrawEllipse(null, KabloomPalette.CorePen(Math.Max(1, size * 0.10 * (1 - eased * 0.5))),
                    source, halo, halo);
                dc.Pop();
                DrawGem(dc, source, size * grown, spin, game.PresentationTime + diamond * 0.6);
                continue;
            }

            double t = Smooth(progress);
            Point control = new(fieldCenter.X + field * 0.18, fieldCenter.Y - field * 0.82);
            double oneMinus = 1 - t;
            Point position = new(
                oneMinus * oneMinus * source.X + 2 * oneMinus * t * control.X + t * t * target.X,
                oneMinus * oneMinus * source.Y + 2 * oneMinus * t * control.Y + t * t * target.Y);
            // Hands off from the swell at exactly the size it ended on and shrinks back to normal as it
            // travels, so the gem visibly recedes toward the counter.
            double shrink = KabloomTuning.DiamondSwellScale
                            + (1 - KabloomTuning.DiamondSwellScale) * Smooth(progress);
            // In flight it spins hard — the collection is the reward beat, and a stone tumbling as it flies is
            // the cheapest way to say so. ⚠ From zero, matching the still swell it hands off from: a per-gem
            // offset or a carried-over angle here would snap the stone round at the halfway point.
            DrawGem(dc, position, size * shrink, progress * 540,
                game.PresentationTime + diamond * 0.6);

            double trailT = Math.Max(0, t - 0.075);
            double trailOneMinus = 1 - trailT;
            Point trail = new(
                trailOneMinus * trailOneMinus * source.X + 2 * trailOneMinus * trailT * control.X + trailT * trailT * target.X,
                trailOneMinus * trailOneMinus * source.Y + 2 * trailOneMinus * trailT * control.Y + trailT * trailT * target.Y);
            dc.DrawLine(KabloomPalette.CorePen(Math.Max(1, size * 0.16), round: true), trail, position);
        }
    }

    /// <summary>Where the HUD plate's gem sits — the target a freed gem flies to. Written by <see cref="DrawHud"/>
    /// each frame (the plate is laid out from its text) and read by the active gem pass, which runs after it.
    /// The fallback below covers the first frame only, before the plate has ever been laid out.</summary>
    private static Point _hudGemPoint;

    private static Point DiamondCounterPoint(Point c, double field) =>
        _hudGemPoint == default ? new Point(c.X - field * 0.18, c.Y - field * 0.93) : _hudGemPoint;

    /// <summary>How far a resting nectar node is pushed past its opening. Over 1, so the node overlaps the
    /// petals around it and reads as sitting on top of the board rather than recessed into a gap between the
    /// petal edges.
    ///
    /// <para>⚠ Applied about the cap's own centre, which is why the shape's corner angles — and so the
    /// <c>turn</c> the art is lined up by — come out unchanged. Scaling about the polygon's bounds centre
    /// instead would drift the node off the hub on any cap the crop has cut asymmetrically.</para></summary>
    private const double NectarOverlap = 1.15;

    /// <summary>The opening a nectar gem sits in, as a polygon.
    ///
    /// <para>Built from the same cut points the petals are drawn with — each petal round the hub is trimmed
    /// back by the centre cap, and the two trim points it contributes are its neighbours' too, so the corners
    /// are shared and the shape is the true hub outline rather than a disc approximating it. One side per
    /// petal touching the hub. Null where too few petals survive the crop to bound anything.</para>
    ///
    /// <para>⚠ The returned polygon is pushed out by <see cref="NectarOverlap"/>, so it does not fit the hole
    /// edge to edge — that overlap is deliberate. Everything keyed off this shape grows with it: the fill, the
    /// edge stroke, the art clip and the vector fallback.</para></summary>
    // ── The nectar openings, built once per board rather than per frame ──────
    // An opening is a pure function of the grid and the board transform, but it was being rebuilt for every
    // resting node on every frame: a list, a sorting closure, a PathGeometry and about 35 Atan2 calls each,
    // times the ~37 caps of a full crop. That is roughly 150 allocations and 1,300 trig calls a frame for a
    // shape that only changes when the board is rebuilt or resized.
    //
    // ⚠ Keyed on the grid by reference as well as on the transform. A fresh level can land on the same origin
    // and scale with a different flower under it, and identity is the only exact test for that — Kabloom
    // assigns Grid once per layout, so it is a reliable one.
    private static object? _nectarGrid;
    private static Point _nectarOrigin;
    private static double _nectarScale = double.NaN;
    private static Geometry?[] _nectarShape = [];
    private static double[] _nectarTurn = [];
    private static Geometry?[] _nectarSide = [];
    private static NectarWall[]?[] _nectarWalls = [];
    private static bool[] _nectarBuilt = [];

    private readonly record struct NectarWall(Geometry Shape, Brush Fill);

    /// <summary>A raised gem's visible side walls: one quad per opening edge that faces down the screen, from
    /// the raised edge to its footprint, each shaded by which way it faces so the stone reads as faceted rather
    /// than as one flat band. Edges facing up the screen are hidden behind the face and skipped.</summary>
    private static NectarWall[] NectarWalls(List<Point> pts, Point centre, double lift)
    {
        var walls = new List<NectarWall>(pts.Count);
        for (int n = 0; n < pts.Count; n++)
        {
            Point a = pts[n], b = pts[(n + 1) % pts.Count];
            double ex = b.X - a.X, ey = b.Y - a.Y, len = Math.Sqrt(ex * ex + ey * ey);
            if (len < 1e-6) continue;
            double nx = ey / len, ny = -ex / len;
            if (nx * ((a.X + b.X) / 2 - centre.X) + ny * ((a.Y + b.Y) / 2 - centre.Y) < 0) { nx = -nx; ny = -ny; }
            if (ny <= 0.05) continue;
            var quad = Polygon([a, b, new Point(b.X, b.Y - lift), new Point(a.X, a.Y - lift)]);
            walls.Add(new NectarWall(quad, GemWallBrush((nx + 1) / 2)));
        }
        return [.. walls];
    }

    /// <summary>A wall tone between <see cref="KabloomPalette.GemSideLeft"/> (0) and
    /// <see cref="KabloomPalette.GemSideRight"/> (1), quantised to 16 steps and cached.</summary>
    private static Brush GemWallBrush(double t)
    {
        int step = (int)Math.Clamp(Math.Round(t * 15), 0, 15);
        if (_gemWallBrushes[step] is { } hit) return hit;
        Color l = KabloomPalette.GemSideLeft, r = KabloomPalette.GemSideRight;
        double u = step / 15.0;
        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Round(l.R + (r.R - l.R) * u), (byte)Math.Round(l.G + (r.G - l.G) * u),
            (byte)Math.Round(l.B + (r.B - l.B) * u)));
        brush.Freeze();
        return _gemWallBrushes[step] = brush;
    }

    private static readonly Brush?[] _gemWallBrushes = new Brush?[16];

    /// <summary>One gem resting in its opening, raised like the covered petals: its walls, then its face.
    /// Drawn from inside the raised-petal pass so it takes its place in the top-down order — the petals
    /// below its hub cover its walls, and it covers the petals above.</summary>
    private static void DrawRestingGem(DrawingContext dc, Point boardOrigin, double scale, Kabloom game,
                                       Vec2[][] outlines, int diamond)
    {
        if (game.DiamondProgress(diamond) >= 0 || game.DiamondSwell(diamond) >= 0) return;
        double gemLift = KabloomTuning.TileThicknessInGridUnits * scale;
        double time = game.PresentationTime + diamond * 0.6;
        // Not during the level flip: the zoom animates `scale`, which is the nectar cache's key, so every
        // opening would be rebuilt (~35 Atan2 and a sort each) on every frame of the game's most expensive
        // animation, for gems mostly hidden under the flipping petals. They return the moment the board settles.
        if (!Transitioning(game)
            && NectarShape(game, outlines, diamond, boardOrigin, scale, out double turn) is { } hole)
        {
            if (_nectarWalls[diamond] is { } walls)
                foreach (var wall in walls)
                    dc.DrawGeometry(wall.Fill, KabloomPalette.GemSideSeam, wall.Shape);
            if (_nectarSide[diamond] is { } side)
                dc.DrawGeometry(null, KabloomPalette.NectarEdge, side);
            dc.PushTransform(LiftTransform(gemLift));
            DrawNectar(dc, hole, turn, time);
            dc.Pop();
        }
        else
            // ⚠ Takes the same inset. This stands in only where the crop left too few petals to bound a hub,
            // and a lone node at full size beside inset ones reads as a glitch.
            DrawGem(dc, Raised(Screen(game.Grid.CenterCaps[diamond].Center, boardOrigin, scale), gemLift),
                    Math.Max(3.1, scale * 0.235) * NectarOverlap, 0, time);
    }

    private static bool Transitioning(Kabloom k)
        =>k.TransitionElapsed >= 0 && k.TransitionElapsed < KabloomTuning.LevelTransitionSeconds;

    private static Geometry? NectarShape(Kabloom game, Vec2[][] outlines, int cap, Point origin,
                                         double scale, out double turn)
    {
        int caps = game.Grid.CenterCaps.Count;
        if (!ReferenceEquals(_nectarGrid, game.Grid) || _nectarOrigin != origin
            || _nectarScale != scale || _nectarShape.Length != caps)
        {
            _nectarGrid = game.Grid;
            _nectarOrigin = origin;
            _nectarScale = scale;
            _nectarShape = new Geometry?[caps];
            _nectarTurn = new double[caps];
            _nectarSide = new Geometry?[caps];
            _nectarWalls = new NectarWall[]?[caps];
            _nectarBuilt = new bool[caps];
        }
        if (_nectarBuilt[cap]) { turn = _nectarTurn[cap]; return _nectarShape[cap]; }

        turn = 0;
        var touching = game.Grid.CenterCaps[cap].TouchingCells;
        // ⚠ A null is cached too. Too few surviving petals to bound an opening is a property of the board,
        // not of the frame, so re-deriving it every frame would be the one case the cache still paid for.
        if (touching.Length < 3) { _nectarBuilt[cap] = true; return null; }
        var centre = Screen(game.Grid.CenterCaps[cap].Center, origin, scale);
        var pts = new List<Point>(touching.Length * 2);
        foreach (int id in touching)
        {
            if (id < 0 || id >= outlines.Length) continue;
            var o = outlines[id];
            if (o.Length < 2) continue;
            Add(Screen(o[0], origin, scale));
            Add(Screen(o[^1], origin, scale));
        }
        if (pts.Count < 3) { _nectarBuilt[cap] = true; return null; }
        pts.Sort((a, b) => Math.Atan2(a.Y - centre.Y, a.X - centre.X)
                    .CompareTo(Math.Atan2(b.Y - centre.Y, b.X - centre.X)));

        // How far the opening is turned, taken from the corner nearest straight up: art is drawn with a
        // corner at the top, so lining that corner up with this one lines the whole shape up. Any corner
        // would do on a regular hexagon; the nearest is the one that needs the least turn.
        double best = double.MaxValue;
        foreach (var p in pts)
        {
            double a = Math.Atan2(p.Y - centre.Y, p.X - centre.X) * 180 / Math.PI + 90;
            while (a > 180) a -= 360;
            while (a < -180) a += 360;
            if (Math.Abs(a) < Math.Abs(best)) best = a;
        }
        turn = best == double.MaxValue ? 0 : best;
        // ⚠ After the turn is taken, though a radial scale about the centre leaves every corner's angle
        // alone, so the two are order-independent by construction rather than by luck.
        for (int i = 0; i < pts.Count; i++)
            pts[i] = new Point(centre.X + (pts[i].X - centre.X) * NectarOverlap,
                               centre.Y + (pts[i].Y - centre.Y) * NectarOverlap);
        // Polygon freezes what it returns, which is what makes it safe to hand the same object back every
        // frame from here on.
        var built = Polygon(pts);
        _nectarShape[cap] = built;
        _nectarSide[cap] = SweepDown(pts, KabloomTuning.TileThicknessInGridUnits * scale);
        _nectarWalls[cap] = NectarWalls(pts, centre, KabloomTuning.TileThicknessInGridUnits * scale);
        _nectarTurn[cap] = turn;
        _nectarBuilt[cap] = true;
        return built;

        // Neighbouring petals cut at the same place, so every corner arrives twice.
        void Add(Point p)
        {
            foreach (var q in pts)
                if (Math.Abs(q.X - p.X) < 0.75 && Math.Abs(q.Y - p.Y) < 0.75) return;
            pts.Add(p);
        }
    }

    /// <summary>A gem at rest in its opening: the opening's own shape, inset and filled.
    ///
    /// <para>⚠ Neither turning nor breathing. A shape cut from its hole cannot rotate without ceasing to
    /// match it, and a bed of them pulsing was motion on the one part of the board that never changes. Motion
    /// is kept for the moment a gem is freed, where it means something.</para></summary>
    private static void DrawNectar(DrawingContext dc, Geometry shape, double turn, double time)
    {
        dc.DrawGeometry(KabloomPalette.Diamond, KabloomPalette.NectarEdge, shape);
        if (ArcadeSprites.Frame(ArcadeSprites.Slot.KabloomGem) is { } art)
        {
            // Turned to the opening, not left upright: neighbouring hubs sit at different angles, so art with
            // any facet or grain of its own has to follow the hole or it reads as a stone dropped in crooked.
            // The clip is the hole itself and stays put; only the art turns inside it.
            var b = shape.Bounds;
            var pivot = new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
            // ⚠ Clip first and unrotated. The hole is already at its own angle; turning the clip as well
            // would turn the opening twice and cut the stone off its corners.
            dc.PushClip(shape);
            dc.PushTransform(new RotateTransform(turn, pivot.X, pivot.Y));
            // A shade over the bounds, because a turned image has to cover corners the upright box does not.
            ArcadeSprites.Draw(dc, art, ArcadeSprites.Box(pivot, b.Width * 0.58, b.Height * 0.58),
                               ArcadeSprites.Fit.Cover);
            dc.Pop();
            dc.Pop();
        }
        else
        {
            var b = shape.Bounds;
            DrawGem(dc, new Point(b.X + b.Width / 2, b.Y + b.Height / 2),
                    Math.Min(b.Width, b.Height) / 2, 0, time);
        }
        dc.DrawGeometry(null, KabloomPalette.NectarEdge, shape);
    }

    /// <summary>A honey-orange gemstone, drawn as a brilliant cut seen from directly above: a round girdle, a
    /// ring of eight kite facets, and an eight-pointed star table in the middle.
    ///
    /// <para><paramref name="rotation"/> turns the stone; the glitter is deliberately not a function of it.
    /// Each facet's brightness comes from the angle between its own outward normal and a fixed light — so as
    /// the stone turns, facets pass through the light and flash one after another, which is what a real
    /// stone does. Tying brightness to the facet index instead would rotate the highlights with the stone and
    /// look painted on.</para></summary>
    private static void DrawGem(DrawingContext dc, Point p, double size, double rotation, double time)
    {
        // Replacement art turns on the same angle the cut facets do, so a stone still reads as one object
        // spinning rather than as a picture sitting still on a spinning board.
        if (ArcadeSprites.Frame(ArcadeSprites.Slot.KabloomGem) is { } stone)
        {
            dc.PushTransform(new RotateTransform(rotation, p.X, p.Y));
            ArcadeSprites.Draw(dc, stone, ArcadeSprites.Box(p, size));
            dc.Pop();
            return;
        }
        const int facets = 8;
        double turn = rotation * Math.PI / 180.0;
        // A fixed light, drifting very slowly so a stone at rest still lives.
        double lightAngle = -Math.PI / 3 + Math.Sin(time * 0.6) * 0.25;

        dc.DrawEllipse(KabloomPalette.Diamond, KabloomPalette.GemEdge, p, size, size);

        double girdle = size * 0.97;
        double tableR = size * 0.44;
        for (int i = 0; i < facets; i++)
        {
            double a0 = turn + i * Math.PI * 2 / facets;
            double a1 = a0 + Math.PI * 2 / facets;
            double mid = a0 + Math.PI / facets;

            // Kite: girdle corner → outer point → girdle corner → table corner.
            var kite = Polygon([
                new Point(p.X + Math.Cos(a0) * girdle,   p.Y + Math.Sin(a0) * girdle),
                new Point(p.X + Math.Cos(mid) * girdle,  p.Y + Math.Sin(mid) * girdle),
                new Point(p.X + Math.Cos(a1) * girdle,   p.Y + Math.Sin(a1) * girdle),
                new Point(p.X + Math.Cos(mid) * tableR,  p.Y + Math.Sin(mid) * tableR),
            ]);

            // cos of the angle between this facet's outward normal and the light: 1 = facing it.
            double lit = Math.Cos(mid - lightAngle);
            Brush fill = lit > 0.72 ? KabloomPalette.GemGlint
                       : lit > 0.25 ? KabloomPalette.GemLight
                       : lit > -0.35 ? KabloomPalette.GemMid
                       : KabloomPalette.GemDeep;
            dc.DrawGeometry(fill, KabloomPalette.GemFacet, kite);
        }

        // The table: an eight-pointed star, the flat top of the stone.
        var table = new PathFigure { IsClosed = true, IsFilled = true };
        for (int i = 0; i < facets * 2; i++)
        {
            double r = i % 2 == 0 ? tableR : tableR * 0.62;
            double a = turn + i * Math.PI / facets;
            var pt = new Point(p.X + Math.Cos(a) * r, p.Y + Math.Sin(a) * r);
            if (i == 0) table.StartPoint = pt; else table.Segments.Add(new LineSegment(pt, true));
        }
        var tableGeo = new PathGeometry([table]); tableGeo.Freeze();
        dc.DrawGeometry(KabloomPalette.DiamondCore, KabloomPalette.GemFacet, tableGeo);

        // The single sharp glint, riding the girdle where the light hits it.
        double glintA = lightAngle;
        var glint = new Point(p.X + Math.Cos(glintA) * size * 0.66, p.Y + Math.Sin(glintA) * size * 0.66);
        double sparkle = 0.5 + 0.5 * Math.Sin(time * 3.1 + rotation * 0.05);
        dc.DrawEllipse(KabloomPalette.GemGlint, null, glint, size * 0.11 * sparkle, size * 0.11 * sparkle);

        dc.DrawEllipse(null, KabloomPalette.GemEdge, p, size, size);
    }

    private static void DrawHud(DrawingContext dc, Point c, double field, Kabloom game, double ppd)
    {
        // One plate hanging in from the top of the disc, like Internode's HUD: the gem, then the count.
        // ⚠ The gem score alone. The chapter and level ride the footer plate instead — the top of the disc is
        // the narrowest place a plate can sit (the chord at the plate's own top edge is barely a third of the
        // field), so a long chapter name ran its glyphs straight out through the curve. Sized to what it
        // carries, with side padding wider than the corner radius so the rounding never clips a glyph, and the
        // hang above the rim taken by the field clip.
        double size = ArcadeChrome.Ui(Math.Max(9, field * 0.050));
        double gem  = ArcadeChrome.UiArt(field * 0.024);
        FormattedText score = ArcadeChrome.Text(game.DiamondScore.ToString(), size, KabloomPalette.FocusIvory, ppd, TextAlignment.Left);
        Rect sInk = score.BuildGeometry(new Point()).Bounds;
        double scoreW = Math.Max(sInk.Width, size * 0.6);
        double gap = size * 0.40, padX = size * 0.80, padY = size * 0.34;
        double contentW = gem * 2 + gap + scoreW;
        double h = Math.Max(gem * 2, sInk.Height) + padY * 2;
        var body = new Rect(c.X - contentW / 2 - padX, c.Y - field * 0.985, contentW + padX * 2, h);
        ArcadeChrome.DrawHangingPlate(dc, body, field * ArcadeChrome.PlateHang, KabloomPalette.Plaque, KabloomPalette.PlaquePen);
        double midY = body.Y + h / 2;
        double gemX = body.X + padX + gem;
        _hudGemPoint = new Point(gemX, midY);
        DrawGem(dc, _hudGemPoint, gem, game.PresentationTime * KabloomTuning.GemDegreesPerSecond, game.PresentationTime);
        ArcadeChrome.DrawInkCentered(dc, score, new Point(gemX + gem + gap + scoreW / 2, midY), sInk);

        DrawFooterPlate(dc, c, field, game, ppd);
    }

    /// <summary>The plate under the board: the chapter and level always, the bee count beside it while the
    /// hint runs. One plate rather than two, so the readouts share a baseline instead of stacking a second bar
    /// across the crop, and it is sized to what it actually carries — when the count drops out the plate
    /// narrows to the level readout alone rather than leaving a hole where the count was.
    ///
    /// <para>⚠ One FormattedText, with the count's ink applied to its tail by range — not two texts laid out
    /// side by side. The count carries its own colour, but it has to be separated from the level by the same
    /// " · " the chapter and level are separated by, and a hand-built gap between two layouts cannot match a
    /// bullet the font placed. Laying the whole line as one run puts every separator under the same metrics,
    /// and costs one text layout instead of two.</para>
    ///
    /// <para>⚠ The count has no over-flagged state, and must not grow one speculatively: flagging is capped
    /// at the mine count in <c>KabloomBoard.CycleMark</c>, and <c>RestoreState</c> rejects a snapshot that
    /// claims more — so the remainder is never negative and a warning ink here would be unreachable.</para></summary>
    private static void DrawFooterPlate(DrawingContext dc, Point c, double field, Kabloom game, double ppd)
    {
        double size = ArcadeChrome.Ui(Math.Max(8.5, field * 0.046));
        // The chapter name is a UiText const carried through the difficulty table — look it up, don't draw
        // the key. (Kabloom's stage names are the only game text that reaches a renderer as data.)
        string line = $"{Loc.T(game.Profile.ChapterName)} · {game.CurrentLevel:00}";

        // Nothing to count before the board is planted. Past the hint levels the player keeps their own tally;
        // the ALL CLEAR line is an outcome, not a hint, and stays.
        string? count = null;
        int remaining = game.Board.BeeTotal - game.Board.FlagSum;
        // ⚠ Shown at every level. The count is information the certification relies on: the mine-counting endgame
        // (all remaining bees accounted for on the frontier, so the clue-less petals are safe) is impossible
        // without it, and a board certified with the total but played without it is a forced guess.
        if (game.Board.Phase != KabloomBoardPhase.Unplanted)
        {
            count = game.Board.Phase == KabloomBoardPhase.Cleared
                ? Loc.T(UiText.Arcade.AllClear)
                : Loc.F(UiText.Arcade.BeesDelta, remaining.ToString(System.Globalization.CultureInfo.InvariantCulture));
            line = $"{line} · {count}";
        }

        FormattedText text = ArcadeChrome.Text(line, size, KabloomPalette.FocusIvory, ppd, TextAlignment.Left);
        // The bullet stays ivory with the level readout; only the count itself takes its own ink.
        if (count is not null)
            text.SetForegroundBrush(KabloomPalette.SuccessGold, line.Length - count.Length, count.Length);
        Rect glyphs = text.BuildGeometry(new Point()).Bounds;
        if (glyphs.IsEmpty) return;

        double padX = size * 0.62, padY = size * 0.30;
        double h = glyphs.Height + padY * 2;
        // High enough that the plate's bottom edge clears the rim at this size.
        double top = c.Y + field * 0.78;
        var plate = new Rect(c.X - glyphs.Width / 2 - padX, top, glyphs.Width + padX * 2, h);
        dc.DrawRoundedRectangle(KabloomPalette.Plaque, KabloomPalette.PlaquePen, plate, h * 0.34, h * 0.34);
        ArcadeChrome.DrawInkCentered(dc, text, new Point(c.X, top + h / 2), glyphs);
    }

    /// <summary>A shout in poster word art (<see cref="KabloomPalette"/> ▸ the STUNG shout), centred on
    /// <paramref name="cx"/> with its face's top at <paramref name="top"/>, then pulled back if its near edge
    /// would pass <paramref name="nearLimit"/> on the <paramref name="beeSide"/> (+1 right, −1 left). The speed
    /// lines trail off the far side only. Returns the height it takes, extrusion included.
    ///
    /// <para>Built once per string, size, side and DPI into a frozen drawing: the extrusion outline is a union
    /// of a dozen offset copies of the glyphs, far too much geometry work to repeat each frame.</para></summary>
    private static double DrawShout(DrawingContext dc, double cx, double top, string message, double size,
                                    double ppd, int beeSide, double nearLimit)
    {
        var key = (message, Math.Round(size, 2), ppd, beeSide);
        if (_shoutKey != key || _shout is null)
        {
            _shout = BuildShout(message, size, ppd, -beeSide, out _shoutFace, out _shoutBody);
            _shoutKey = key;
        }
        if (_shoutFace.IsEmpty) return 0;
        double x = cx - _shoutFace.X - _shoutFace.Width / 2, y = top - _shoutFace.Y;
        if (beeSide > 0) x = Math.Min(x, nearLimit - _shoutBody.Right);
        else if (beeSide < 0) x = Math.Max(x, nearLimit - _shoutBody.Left);
        dc.PushTransform(new TranslateTransform(x, y));
        dc.DrawDrawing(_shout);
        dc.Pop();
        return _shoutBody.Bottom - _shoutFace.Y;
    }

    private static (string, double, double, int) _shoutKey;
    private static Drawing? _shout;
    private static Rect _shoutFace, _shoutBody;

    /// <param name="trailSide">Which end the speed lines trail off: +1 right, −1 left.</param>
    /// <param name="body">The face and extrusion's bounds, speed lines excluded — what must clear the bee.</param>
    private static Drawing BuildShout(string message, double size, double ppd, int trailSide,
                                      out Rect face, out Rect body)
    {
        var text = new FormattedText(message, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                                     ShoutFace, size, Brushes.Black, ppd);
        Geometry glyphs = text.BuildGeometry(new Point());
        face = body = glyphs.Bounds;
        var group = new DrawingGroup();
        if (face.IsEmpty) { group.Freeze(); return group; }

        double depth = size * 0.16;         // extrusion, down-left
        Vector dir = new(-0.55, 1.0);

        // The extrusion's footprint: the glyphs swept along `dir` by `depth`, as a union of copies about a
        // pixel apart. Only its hatching is drawn — the lines alone carry the depth.
        int steps = Math.Max(2, (int)Math.Ceiling(depth));
        Geometry block = glyphs;
        for (int s = 1; s <= steps; s++)
        {
            var copy = glyphs.Clone();
            copy.Transform = new TranslateTransform(dir.X * depth * s / steps, dir.Y * depth * s / steps);
            block = Geometry.Combine(block, copy, GeometryCombineMode.Union, null);
        }
        block.Freeze();
        body = block.Bounds;

        using (var g = group.Open())
        {
            Pen Flat(Brush b, double w)
            {
                var p = new Pen(b, w) { LineJoin = PenLineJoin.Miter };
                p.Freeze();
                return p;
            }

            // Speed lines trailing off the far end, behind everything: parallel rules of staggered length at
            // the word's height.
            var rule = Flat(KabloomPalette.ShoutShade, Math.Max(1, size * 0.028));
            double pitch = size * 0.085, reach = size * 0.9, inset = size * 0.25;
            int n = 0;
            for (double y = face.Top + pitch * 0.5; y < face.Bottom + depth; y += pitch, n++)
            {
                double jag = (n * 0.618 % 1) * reach * 0.45;
                if (trailSide < 0)
                    g.DrawLine(rule, new Point(body.Left - reach + jag, y), new Point(face.Left + inset, y));
                else
                    g.DrawLine(rule, new Point(face.Right - inset, y), new Point(body.Right + reach - jag, y));
            }

            // The extrusion: diagonal hatching clipped to the footprint, and nothing else.
            g.PushClip(block);
            Rect b = block.Bounds;
            double hatchPitch = Math.Max(2, size * 0.045);
            var hatch = Flat(KabloomPalette.ShoutShade, Math.Max(0.8, size * 0.018));
            for (double x = b.Left - b.Height; x < b.Right; x += hatchPitch)
                g.DrawLine(hatch, new Point(x, b.Bottom), new Point(x + b.Height, b.Top));
            g.Pop();

            // The face: a thin cream rule parting it from the hatching, then the solid colour.
            g.DrawGeometry(null, Flat(KabloomPalette.ShoutInline, size * 0.06), glyphs);
            g.DrawGeometry(KabloomPalette.ShoutFace, null, glyphs);
        }
        group.Freeze();
        return group;
    }

    /// <summary>The shout's face: a geometric sans in bold, falling back through Windows' own heavy faces.
    /// A comma list, so a machine missing one lands on the next rather than on a default.</summary>
    private static Typeface? _shoutTypeface;
    private static Typeface ShoutFace => _shoutTypeface ??= new Typeface(
        new FontFamily("Century Gothic, Futura, Segoe UI"),
        FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    /// <summary>Text that overlays the playfield sits on a contrasting plate.
    ///
    /// <para>⚠ Every string drawn over the crop goes through here, bar the STUNG shout (<see cref="DrawShout"/>),
    /// which lands on the failure scrim and carries its own contrast. The crop's tiles run from near-white
    /// covered petals to a near-black revealed face, so a bare ink can be crisp on one board and almost gone
    /// on the next, and the growth notices land during the level flip when the ground under them is actively
    /// changing. The plate makes legibility a property of the readout rather than of where it fell.</para>
    ///
    /// <para>Sized to the text rather than to a fixed box, so a short label doesn't drag a wide bar across the
    /// board — the plate is a backing for these words, not a HUD strip.</para></summary>
    private static double DrawPlaque(DrawingContext dc, double cx, double top, string message, double size,
                                     Brush ink, double field, double ppd, double opacity = 1, double maxWidth = 0)
    {
        _ = field;
        // A button prompt takes the shared badge treatment inside the same plate.
        if (ArcadeChrome.TryPrompt(message, out string button, out string verb))
        {
            Size box = ArcadeChrome.PromptSize(button, verb, size, ppd);
            double px = size * 0.45, py = size * 0.18;
            var plaque = new Rect(cx - box.Width / 2 - px, top - py, box.Width + px * 2, box.Height + py * 2);
            if (opacity < 1) dc.PushOpacity(Math.Clamp(opacity, 0, 1));
            dc.DrawRoundedRectangle(KabloomPalette.Plaque, KabloomPalette.PlaquePen, plaque,
                                    plaque.Height * 0.34, plaque.Height * 0.34);
            ArcadeChrome.DrawPrompt(dc, button, verb, size, ink, cx - box.Width / 2, top, ppd);
            if (opacity < 1) dc.Pop();
            return plaque.Height;
        }
        // ⚠ Left alignment plus measured ink bounds, never TextAlignment.Center. A centre-aligned
        // FormattedText centres its line inside MaxTextWidth, and with MaxTextWidth unset that box is not the
        // same width as the `Width` property the plate is sized from — plate and glyphs then derive from two
        // different numbers and drift apart. Measuring the geometry gives one box for both, the same way
        // ArcadeChrome.DrawInkCentered does.
        FormattedText text = ArcadeChrome.Text(message, size, ink, ppd, TextAlignment.Left, maxWidth);
        Rect glyphs = text.BuildGeometry(new Point()).Bounds;
        if (glyphs.IsEmpty) return 0;

        double padX = size * 0.62, padY = size * 0.30;
        var plate = new Rect(cx - glyphs.Width / 2 - padX, top - padY,
                             glyphs.Width + padX * 2, glyphs.Height + padY * 2);
        if (opacity < 1) dc.PushOpacity(Math.Clamp(opacity, 0, 1));
        dc.DrawRoundedRectangle(KabloomPalette.Plaque, KabloomPalette.PlaquePen, plate,
                                plate.Height * 0.34, plate.Height * 0.34);
        // Offset so the measured ink lands where the plate expects it, rather than the layout box — digits and
        // caps carry very different side bearings and ascents at these sizes.
        dc.DrawText(text, new Point(cx - glyphs.X - glyphs.Width / 2, top - glyphs.Y));
        if (opacity < 1) dc.Pop();
        return plate.Height;
    }

    /// <summary>The face buttons as text, following Settings ▸ Advanced ▸ Button icons. Read per draw rather
    /// than cached — the setting can change while a game is frozen and open, and the renderer redraws every
    /// frame anyway.</summary>
    private static string Cross => ControllerButtons.Text(PadButton.Cross);
    private static string Square => ControllerButtons.Text(PadButton.Square);
    private static string Circle => ControllerButtons.Text(PadButton.Circle);

    /// <summary>Says what just got harder. The two difficulty axes move independently, so a level that widens
    /// the crop and crowds it announces both.
    ///
    /// <para>Placed above the board and below the level readout, rising and fading, so it never covers the
    /// petals during the flip it arrives with.</para></summary>
    private static void DrawGrowthNotice(DrawingContext dc, Point c, double field, Kabloom game, double ppd)
    {
        if (!game.GrowthShowsCells && !game.GrowthShowsMines) return;
        double t = game.GrowthProgress;
        // Hold at full for the first half, then fade — the flip is still settling early on, and a banner
        // that starts fading immediately is gone before the eye reaches it.
        double alpha = t < 0.5 ? 1 : 1 - (t - 0.5) / 0.5;
        double rise = field * 0.05 * t;

        // On a plate: this notice arrives during the level flip, so the ground beneath it is mid-change and
        // no fixed ink can be trusted.
        double size = ArcadeChrome.Ui(Math.Max(11, field * 0.068));
        double y = c.Y - field * 0.66 - rise;
        if (game.GrowthShowsCells)
            y += DrawPlaque(dc, c.X, y, Loc.T(UiText.Arcade.PatchGrows), size, KabloomPalette.Success, field, ppd, alpha)
                 + field * 0.020;
        if (game.GrowthShowsMines)
            // Warm, because this one is a threat rather than a reward.
            DrawPlaque(dc, c.X, y, Loc.T(UiText.Arcade.BeesIncrease), size, KabloomPalette.Warning, field, ppd, alpha);
    }

    private static void DrawStage(DrawingContext dc, Point c, double field, Kabloom game, double ppd)
    {
        if (game.Phase == Kabloom.Stage.AwaitingFirstReveal && game.CapacityNotice > 0)
        {
            // The bees-per-petal interstitial: a scrim over the unplanted board and four plates. The continue
            // line is dimmed while the card is locked, so the eye reads "not yet" rather than "press now".
            dc.DrawEllipse(KabloomPalette.Scrim, null, c, field, field);
            // One plate, everything centred. Copy is wrapped to a fixed column inside the disc so no line can run
            // off the window; the plate takes the column's width and the stacked heights.
            double wrap = field * 1.30;
            double gap = field * 0.035;
            FormattedText title = ArcadeChrome.Text(Loc.T(UiText.Arcade.CapacityTitle), ArcadeChrome.Ui(Math.Max(14, field * 0.095)),
                KabloomPalette.Warning, ppd, TextAlignment.Center, wrap);
            FormattedText body = ArcadeChrome.Text(Loc.F(UiText.Arcade.CapacityBody, game.CapacityNotice),
                ArcadeChrome.Ui(Math.Max(8, field * 0.046)), KabloomPalette.FocusIvory, ppd, TextAlignment.Center, wrap);
            FormattedText flags = ArcadeChrome.Text(Loc.T(UiText.Arcade.CapacityFlags).Replace("{square}", Square),
                ArcadeChrome.Ui(Math.Max(8, field * 0.046)), KabloomPalette.FocusIvory, ppd, TextAlignment.Center, wrap);
            double promptSize = ArcadeChrome.Ui(Math.Max(8, field * 0.044));
            string button = Cross, verb = Loc.T(UiText.Arcade.Continue);
            Size prompt = ArcadeChrome.PromptSize(button, verb, promptSize, ppd);
            double inner = title.Height + gap * 1.5 + body.Height + gap + flags.Height + gap * 2 + prompt.Height;
            double padX = field * 0.05, padY = field * 0.05;
            var plate = new Rect(c.X - wrap / 2 - padX, c.Y - inner / 2 - padY, wrap + padX * 2, inner + padY * 2);
            dc.DrawRoundedRectangle(KabloomPalette.Plaque, KabloomPalette.PlaquePen, plate, field * 0.05, field * 0.05);
            double y = plate.Y + padY;
            dc.DrawText(title, new Point(c.X - wrap / 2, y)); y += title.Height + gap * 1.5;
            dc.DrawText(body, new Point(c.X - wrap / 2, y)); y += body.Height + gap;
            dc.DrawText(flags, new Point(c.X - wrap / 2, y)); y += flags.Height + gap * 2;
            // The continue prompt alone dims while the card is locked, so the eye reads "not yet".
            dc.PushOpacity(game.CapacityNoticeLocked ? 0.35 : 1.0);
            ArcadeChrome.DrawPrompt(dc, button, verb, promptSize, KabloomPalette.FocusIvory, c.X - prompt.Width / 2, y, ppd);
            dc.Pop();
        }
        else if (game.Phase == Kabloom.Stage.AwaitingFirstReveal)
        {
            // No button token on this line: naming the key is the how-to card's job. What stays is the state
            // readout — this board has nothing planted in it yet, which is not otherwise visible on a crop of
            // blank petals.
            DrawPlaque(dc, c.X, c.Y + field * 0.64, Loc.T(UiText.Arcade.FieldReady),
                ArcadeChrome.Ui(Math.Max(9, field * 0.050)), KabloomPalette.FocusIvory, field, ppd);
        }
        else if (game.Phase == Kabloom.Stage.Generating && game.PhaseTime >= GeneratingGraceSeconds)
        {
            // No pulse behind the word: the wait is carried by the bee wandering over the board, drawn last of
            // everything in DrawGeneratingBee.
            DrawPlaque(dc, c.X, c.Y - field * 0.06, "GENERATING...", ArcadeChrome.Ui(Math.Max(11, field * 0.062)),
                KabloomPalette.SuccessGold, field, ppd);
        }
        // ⚠ Stage.Cleared is deliberately absent — don't add a card there. A finished board must not stop for
        // a keypress between the player and the reward they just earned: the bees leave, the gems come in, and
        // the level transition takes over on its own. CampaignComplete keeps its card; that one is an ending.
        else if (!game.PresentationActive
                 && game.Phase is Kabloom.Stage.Failed or Kabloom.Stage.CampaignComplete)
        {
            // The scrim fades in over the board rather than popping: it arrives the moment the presentation
            // goes quiet, and a full-field wash landing in one frame reads as a glitch over a board that was
            // just moving. Clocked off the sim, so pause and freeze hold it.
            dc.PushOpacity(Smooth(Math.Clamp(game.OutcomeQuietSeconds / StungScrimFadeSeconds, 0, 1)));
            dc.DrawEllipse(KabloomPalette.Scrim, null, c, field, field);
            dc.Pop();
        }
    }

    /// <summary>The outcome card's words, drawn over the struck bee: the scrim under it is part of
    /// <see cref="DrawStage"/>, so the bee sits between the wash and the text and the text is never hidden
    /// behind the insect parked beside it. Positions are shared with the scrim branch above.</summary>
    private static void DrawOutcomeCard(DrawingContext dc, Point c, double field, Kabloom game, double ppd)
    {
        if (game.PresentationActive || game.Phase is not (Kabloom.Stage.Failed or Kabloom.Stage.CampaignComplete)) return;
        {
            bool failed = game.Phase == Kabloom.Stage.Failed;
            // Both lines go through the plaque routine, not ArcadeChrome.DrawCentered: the field-wide Scrim
            // still shows the board through it, and every piece of text drawn over the crop sits on its own
            // opaque plate.
            // Stepped away from the parked bee on a failed board, so the two are not fighting for the middle.
            double cx = c.X - (failed ? StruckBeeSide * field * StungCardShift : 0);
            double y = c.Y - field * 0.30;
            y += (failed
                    ? DrawShout(dc, cx, y, Loc.T(UiText.Arcade.Stung), ArcadeChrome.Ui(Math.Max(16, field * 0.12)), ppd,
                                StruckBeeSide, c.X + StruckBeeSide * field * StungShoutReach(game))
                    : DrawPlaque(dc, cx, y, Loc.T(UiText.Arcade.YouWin),
                        ArcadeChrome.Ui(Math.Max(14, field * 0.105)), KabloomPalette.Success, field, ppd))
                 + field * 0.05;
            // ⚠ The outcome word lands immediately; the button prompts wait for the bee to finish flying and
            // settle. The bee is the explanation for what just happened, and a prompt over it invites the
            // player to dismiss the board before they have read why they lost. Gated on the game's own
            // OutcomePromptsReady, which also gates the input — so the card never shows a button the board
            // will not obey, nor obeys one it has not offered.
            if (!game.OutcomePromptsReady) return;
            y += DrawPlaque(dc, cx, y, failed ? $"{Cross}  {Loc.T(UiText.Arcade.FallBack)}" : $"{Square}  {Loc.T(UiText.Arcade.ReplayFinalField)}",
                ArcadeChrome.Ui(Math.Max(8, field * 0.044)), KabloomPalette.FocusIvory, field, ppd) + field * 0.03;
            // The way out, on every outcome card: the same ○ that leaves the arcade from anywhere else. It
            // keeps working while this prompt is hidden — that door is the host's and is never barred.
            DrawPlaque(dc, cx, y, $"{Circle}  {Loc.T(UiText.Arcade.ExitGame)}",
                ArcadeChrome.Ui(Math.Max(8, field * 0.044)), KabloomPalette.FocusIvory, field, ppd);
        }
    }

    /// <summary>How far oversized the marker starts before it stamps down onto the petal.</summary>
    private const double FlagStampOvershoot = 0.45;

    /// <summary>The "I think there is a bee here" marker: a <b>bee</b>, the same thing the player thinks is in
    /// the cell.
    ///
    /// <para>⚠ It takes the dark flag ink on its pale covered petal, where a revealed bee is white on a dark
    /// one. Same shape, opposite treatment: a guess and a fact must not look alike, and the tile underneath
    /// already tells you which is which.</para></summary>
    /// <param name="count">Bees the flag claims: that many bee glyphs share the ring.</param>
    private static void DrawBeeFlag(DrawingContext dc, Point p, double size, double progress, int count = 1, double ppd = 1)
    {
        // A stamp, not a bounce: it comes down from oversized and settles at 1 without passing under it.
        double t = Math.Clamp(progress, 0, 1);
        double settle = 1 - Math.Pow(1 - t, 3);
        double stamp = 1 + FlagStampOvershoot * (1 - settle);
        dc.PushTransform(new ScaleTransform(stamp, stamp, p.X, p.Y));
        // Held clear of the glyph: the ring is a frame around the bee, not a rim touching it.
        double r = size * 1.22;
        // The ground first, at the ring's own radius, so the stroke lands on its edge.
        dc.DrawEllipse(KabloomPalette.FlagGround, null, p, r, r);
        dc.DrawEllipse(null, KabloomPalette.FlagPen(Math.Max(1, size * 0.11)), p, r, r);
        // ⚠ No halo — the ground disc above already lifts the silhouette off the petal, and DrawBee's own
        // halo would be a second, smaller white disc inside this one. Only the no-art fallback in DrawMine
        // still passes a halo, and that one has no ring to sit in.
        // The count is the number of bees drawn, not a numeral: two share the ring side by side, three sit in a
        // triangle, each scaled so the group still fits inside it. Readable at petal size where a digit is not.
        switch (Math.Clamp(count, 1, 3))
        {
            case 1:
                DrawBee(dc, p, size * 0.92, KabloomPalette.Flag, halo: null);
                break;
            case 2:
                DrawBee(dc, new Point(p.X - r * 0.40, p.Y), size * 0.60, KabloomPalette.Flag, halo: null);
                DrawBee(dc, new Point(p.X + r * 0.40, p.Y), size * 0.60, KabloomPalette.Flag, halo: null);
                break;
            default:
                DrawBee(dc, new Point(p.X, p.Y - r * 0.40), size * 0.54, KabloomPalette.Flag, halo: null);
                DrawBee(dc, new Point(p.X - r * 0.42, p.Y + r * 0.30), size * 0.54, KabloomPalette.Flag, halo: null);
                DrawBee(dc, new Point(p.X + r * 0.42, p.Y + r * 0.30), size * 0.54, KabloomPalette.Flag, halo: null);
                break;
        }
        dc.Pop();
    }

    /// <summary>The question mark's skeleton: a heavy system family, deliberately not the arcade's Share Tech,
    /// which has no bold and whose synthesized one smears. A comma list, so a machine missing one falls to
    /// the next rather than to a default the mark was never drawn for — and nothing is packaged for it.
    /// ⚠ Because the answer varies by machine, the glyph is scaled to a measured height at the draw; do not
    /// replace that with a point size.</summary>
    private static Typeface? _markFace;
    private static Typeface MarkFace => _markFace ??= new Typeface(
        new FontFamily("Arial Black, Segoe UI Black, Franklin Gothic Heavy, Arial"),
        FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    /// <summary>The question mark's height inside the ring, in ring radii. The art is fitted by height and
    /// keeps its aspect, so this is the only size knob.</summary>
    private const double MarkHeight = 1.30;

    /// <summary>The drawn question mark: the author's art, cropped to its ink and recoloured to the flag ink
    /// once, then cached — so the per-frame cost is one DrawImage with no opacity mask (the trap
    /// <see cref="ArcadeSprites.Tint"/> documents for anything drawn per object per frame).
    ///
    /// <para>Cropping is done here rather than asked of the file: the canvas carries wide margins, and art
    /// fitted to a box shrinks by whatever padding is baked into it. Recolouring keeps only the alpha, so the
    /// mark belongs to the same family as the bee marker whatever ink the file itself was drawn in.</para>
    ///
    /// <para>Null if the resource is missing or unreadable, and the vector glyph below stands in.</para></summary>
    private static BitmapSource? _questionArt;
    private static bool _questionArtProbed;
    private static BitmapSource? QuestionArt
    {
        get
        {
            if (_questionArtProbed) return _questionArt;
            _questionArtProbed = true;
            try
            {
                var raw = new FormatConvertedBitmap(
                    new BitmapImage(new Uri("pack://application:,,,/Assets/Kabloom-Question.png")),
                    PixelFormats.Bgra32, null, 0);
                int w = raw.PixelWidth, h = raw.PixelHeight;
                var px = new byte[w * h * 4];
                raw.CopyPixels(px, w * 4, 0);
                int x0 = w, x1 = -1, y0 = h, y1 = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[(y * w + x) * 4 + 3] <= 16) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
                if (x1 < x0 || y1 < y0) return null;
                int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
                var ink = new byte[cw * ch * 4];
                Color tint = ((SolidColorBrush)KabloomPalette.Flag).Color;
                for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int s = ((y + y0) * w + (x + x0)) * 4, d = (y * cw + x) * 4;
                    ink[d] = tint.B; ink[d + 1] = tint.G; ink[d + 2] = tint.R; ink[d + 3] = px[s + 3];
                }
                var shaped = BitmapSource.Create(cw, ch, 96, 96, PixelFormats.Bgra32, null, ink, cw * 4);
                shaped.Freeze();
                _questionArt = shaped;
            }
            catch (Exception ex) { Trace.WriteLine($"[Arcade] Kabloom question art failed: {ex.Message}"); }
            return _questionArt;
        }
    }

    /// <summary>The "not sure" marker: the bee flag's ring with a question mark in the flag ink where the bee
    /// would be, stamping down the same way so the two marks read as one family.</summary>
    private static void DrawQuestionFlag(DrawingContext dc, Point p, double size, double progress, double ppd)
    {
        double t = Math.Clamp(progress, 0, 1);
        double settle = 1 - Math.Pow(1 - t, 3);
        double stamp = 1 + FlagStampOvershoot * (1 - settle);
        dc.PushTransform(new ScaleTransform(stamp, stamp, p.X, p.Y));
        double r = size * 1.22;
        dc.DrawEllipse(KabloomPalette.FlagGround, null, p, r, r);
        dc.DrawEllipse(null, KabloomPalette.FlagPen(Math.Max(1, size * 0.11)), p, r, r);
        if (QuestionArt is { } art)
        {
            double mh = r * MarkHeight;
            double mw = mh * art.PixelWidth / art.PixelHeight;
            dc.DrawImage(art, new Rect(p.X - mw / 2, p.Y - mh / 2, mw, mh));
            dc.Pop();
            return;
        }
        // No art: the glyph from a system face, solid in the flag ink. ⚠ Scaled to a measured height, never
        // trusted to the point size — MarkFace is a fallback chain, so which face answers differs by machine,
        // and two faces at one point size are not two marks of one size. Centred on the glyph's ink bounds as
        // well, because the text box sits high and left of the mark inside it.
        var mark = new FormattedText("?", System.Globalization.CultureInfo.InvariantCulture,
                                     System.Windows.FlowDirection.LeftToRight, MarkFace,
                                     Math.Max(6, size * 1.9), KabloomPalette.Flag, ppd);
        Geometry glyph = mark.BuildGeometry(new Point(0, 0));
        Rect b = glyph.Bounds;
        if (b.IsEmpty || b.Height <= 0) { dc.Pop(); return; }
        double fit = r * MarkHeight / b.Height;
        var place = new TransformGroup();
        place.Children.Add(new ScaleTransform(fit, fit));
        place.Children.Add(new TranslateTransform(p.X - (b.X + b.Width / 2) * fit,
                                                  p.Y - (b.Y + b.Height / 2) * fit));
        glyph.Transform = place;
        dc.DrawGeometry(KabloomPalette.Flag, null, glyph);
        dc.Pop();
    }

    /// <summary>The hazard glyph, taken from the MaterialDesign pack the rest of the app draws its glyphs
    /// from so it matches the wheel's icon vocabulary. Loaded once and frozen; if a pack build drops the name,
    /// the spiked-mine silhouette below is the fallback rather than an empty cell.</summary>
    private static readonly Geometry? BeeGlyph = LoadBeeGlyph();

    private static Geometry? LoadBeeGlyph()
    {
        try
        {
            // Enum.TryParse rather than the named member, so a pack build without "Bee" fails soft to the
            // fallback silhouette instead of failing the compile — the same defensive shape AddMenuIcons uses.
            if (!Enum.TryParse(typeof(MahApps.Metro.IconPacks.PackIconMaterialKind), "Bee", out object? kind)
                || kind is null) return null;
            var icon = new MahApps.Metro.IconPacks.PackIconMaterial
            {
                Kind = (MahApps.Metro.IconPacks.PackIconMaterialKind)kind,
            };
            if (string.IsNullOrWhiteSpace(icon.Data)) return null;
            Geometry geometry = Geometry.Parse(icon.Data);
            geometry.Freeze();
            return geometry;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Arcade] Kabloom bee glyph unavailable: {ex.Message}");
            return null;
        }
    }

    /// <summary>A revealed bee: white on the dark petal it always lands on. A bee that is there — the hazard you uncovered and the swarm leaving a cleared board, which
    /// are the same insect seen twice.
    ///
    /// <para>The one drawing replacement art may take. <see cref="DrawBeeFlag"/> deliberately does not —
    /// see <see cref="ArcadeSprites.Slot.KabloomBee"/>.</para></summary>
    /// <param name="darken">Black washed over the art, 0 to 1. The struck bee arrives lightly washed and
    /// reaches 0 halfway through its flight; every other caller leaves it alone. ⚠ Goes through DrawWashed rather than
    /// Tint: the wash is baked into a cached copy, so a bee darkening across a flight costs one DrawImage a
    /// frame instead of an opacity mask, the arcade's most expensive per-frame operation.</param>
    private static void DrawMine(DrawingContext dc, Point p, double size, double darken = 0)
    {
        if (ArcadeSprites.Frame(ArcadeSprites.Slot.KabloomBee, 0, BeeFps) is { } art)
        {
            // ⚠ No halo behind drawn art. The vector glyph is a solid silhouette that needed a disc under it
            // to keep from reading as a hole in the petal; a drawn bee carries its own shading, and the disc
            // read as a dark blob following it around.
            ArcadeSprites.DrawWashed(dc, art, ArcadeSprites.Box(p, size * BeeArtHalf), Colors.Black, 0, darken);
            return;
        }
        DrawBee(dc, p, size, KabloomPalette.Mine, KabloomPalette.MineRim);
    }

    /// <summary>Half-extent of bee art against the caller's <c>size</c>, matching the span the vector glyph
    /// is scaled to — so swapping one for the other does not resize the insect.</summary>
    private const double BeeArtHalf = 1.025;

    /// <summary>Frame rate for the bee's wing loop. Well above the shared cycle rate: a wingbeat is the one
    /// thing on this board that has to read as a blur rather than as four pictures.</summary>
    private const double BeeFps = 30;

    /// <summary>The bee glyph, in whatever ink the caller needs. Shared by the flag marker and by the
    /// revealed hazard's fallback, so with no art in play the two can never end up being different insects.
    ///
    /// <para>⚠ The marker is drawn from here and never from a sprite: a guess and a fact must not look
    /// alike, and the hazard is the half that takes replacement art.</para></summary>
    private static void DrawBee(DrawingContext dc, Point p, double size, Brush ink, Brush? halo)
    {
        if (BeeGlyph is null)
        {
            var fallback = new Pen(halo ?? ink, Math.Max(1.2, size * 0.18)); fallback.Freeze();
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                dc.DrawLine(fallback,
                    new Point(p.X + Math.Cos(angle) * size * 0.45, p.Y + Math.Sin(angle) * size * 0.45),
                    new Point(p.X + Math.Cos(angle) * size, p.Y + Math.Sin(angle) * size));
            }
            dc.DrawEllipse(ink, fallback, p, size * 0.58, size * 0.58);
            return;
        }

        Rect bounds = BeeGlyph.Bounds;
        double span = Math.Max(bounds.Width, bounds.Height);
        if (span <= 0) return;
        double factor = size * 2.05 / span;
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(
            -(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2)));
        transform.Children.Add(new ScaleTransform(factor, factor));
        transform.Children.Add(new TranslateTransform(p.X, p.Y));
        Geometry shaped = BeeGlyph.Clone();
        shaped.Transform = transform;
        shaped.Freeze();
        // A contrasting halo behind it: the bee is a solid silhouette and can land on a petal close to its own
        // value at either end of the board's range. Without this it reads as a hole rather than an insect.
        // Null from a caller that already lays its own ground, which would otherwise show through as a second
        // smaller disc inside the first.
        if (halo is not null) dc.DrawEllipse(halo, null, p, size * 0.86, size * 0.86);
        dc.DrawGeometry(ink, null, shaped);
    }

    /// <summary>A bee whose work here is done grows and flies off the crop.
    ///
    /// <para>Two callers, one animation: every bee on a cleared board, and the single bee you set off. Keep
    /// it one implementation — the failure and the completion must stay identical, and a second copy would
    /// drift the moment either was tuned.</para>
    ///
    /// <para><paramref name="lightning"/> adds the white bolts that orbit the one you disturbed, and is the
    /// only difference between the two.</para></summary>
    private static void DrawDepartingBee(DrawingContext dc, Point from, Point boardCentre, double unit,
                                         double scale, double t, int id, double time, bool lightning)
    {
        (double swell, Vector offset) = BeeDeparture(t, from, boardCentre, unit, id);
        var at = new Point(from.X + offset.X, from.Y + offset.Y);
        double size = Math.Max(2.8, scale * 0.30) * swell;

        // A short flare as it breaks loose — the beat that says the cell released it, rather than the bee
        // having quietly been elsewhere all along.
        double flare = Math.Clamp(1 - t / 0.22, 0, 1);
        if (flare > 0)
        {
            // Cached: a board clear releases every mine at once, each with this flare.
            double r = size * (0.9 + (1 - flare) * 1.1);
            dc.DrawEllipse(null, KabloomPalette.WhitePen(255, Math.Max(1, size * 0.14 * flare)), at, r, r);
        }

        // Fades only at the very end, as insurance for a bee that started near the middle and hasn't quite
        // cleared the rim. Everything else is done by simply leaving.
        double fade = t > 0.85 ? Math.Clamp((1 - t) / 0.15, 0, 1) : 1;
        if (fade <= 0.01) return;
        dc.PushOpacity(fade);
        if (lightning) DrawLightning(dc, at, size, time, id);
        DrawMine(dc, at, size);
        dc.Pop();
    }

    /// <summary>How big a departing bee is and where it has got to. Outward from the middle of the crop with
    /// a per-bee bias, on a sine that wanders across its own heading — a straight line reads as a sprite
    /// being tweened off screen, and the wander is most of what makes this read as flight.
    ///
    /// <para>Accelerating (t²) rather than linear: it is heavy at the moment it lifts and gone quickly, which
    /// keeps the swell readable at the start where it happens.</para></summary>
    private static (double Swell, Vector Offset) BeeDeparture(double t, Point from, Point boardCentre,
                                                              double unit, int id)
    {
        t = Math.Clamp(t, 0, 1);
        // Grows the whole way out rather than reaching full size early: the bee coming at the screen is the
        // beat, so it is still swelling as it leaves and biggest at the moment it goes.
        double swell = 1 + KabloomTuning.BeeDepartureSwell * Smooth(t);
        if (t <= 0) return (swell, default);

        double dx = from.X - boardCentre.X, dy = from.Y - boardCentre.Y;
        // A bee sitting dead centre has no outward direction of its own; give it one off its id rather than
        // letting the whole middle of the board leave in the same direction.
        double heading = dx * dx + dy * dy < 1e-6
            ? HashUnit(id, 7) * Math.PI * 2
            : Math.Atan2(dy, dx) + (HashUnit(id, 3) - 0.5) * 1.1;

        // Straight out, so it leaves by the nearest rim rather than crossing the crop on a diagonal — the
        // wander below is what keeps that from reading as a tween.
        double travel = unit * KabloomTuning.BeeDepartureReach * t * t;
        // The zigzag is the sine across the heading, and it widens as the bee gets further out: near the
        // start a wide sway would slide it off its own cell, and by the end there is room to weave.
        double wiggle = Math.Sin(t * Math.PI * 2 * KabloomTuning.BeeWiggleWaves + id * 1.7)
                        * unit * KabloomTuning.BeeWiggleAmplitude * (0.35 + 0.65 * t);
        return (swell, new Vector(
            Math.Cos(heading) * travel - Math.Sin(heading) * wiggle,
            Math.Sin(heading) * travel + Math.Cos(heading) * wiggle));
    }

    /// <summary>How solid the bolts are at their blink's peak. ⚠ They are white and the end-card wash is now
    /// light, so this is the one number standing between a jolt and an invisible one.</summary>
    private const double LightningAlpha = 0.5;

    /// <summary>White lightning bolts orbiting the bee you set off, each blinking on and off on its own phase.
    /// White because these bees sit on a dark revealed petal; the failure colour is black, which would make
    /// the angriest thing on screen the least visible.
    ///
    /// <para>Derived from the presentation clock rather than being a particle system: this fires on the
    /// failure screen, where the sim has stopped and there is nothing left to advance particles.</para></summary>
    private static void DrawLightning(DrawingContext dc, Point p, double size, double time, int id)
    {
        const int bolts = 5;
        for (int b = 0; b < bolts; b++)
        {
            // Each bolt blinks on its own beat. A shared one would strobe the whole ring in unison, which
            // reads as the screen flashing rather than as sparks.
            double blink = Math.Sin(time * (7.0 + b * 1.9) + id + b * 2.1);
            if (blink < 0.15) continue;
            double alpha = Math.Clamp((blink - 0.15) / 0.85, 0, 1);

            double orbit = time * 1.9 + b * Math.PI * 2 / bolts;
            double inner = size * 1.15;
            double outer = size * (1.55 + (b % 2) * 0.22);
            var along = new Vector(Math.Cos(orbit), Math.Sin(orbit));
            var across = new Vector(-along.Y, along.X);

            // A three-kink bolt striking outward: the zigzag is across the radius, so it reads as a jolt
            // leaving the bee rather than as a ring drawn around it.
            var figure = new PathFigure
            {
                StartPoint = new Point(p.X + along.X * inner, p.Y + along.Y * inner),
            };
            const int kinks = 3;
            for (int k = 1; k <= kinks; k++)
            {
                double along01 = k / (double)kinks;
                double lateral = (k % 2 == 0 ? -1 : 1) * size * 0.26 * (1 - along01);
                double reach = inner + (outer - inner) * along01;
                figure.Segments.Add(new LineSegment(new Point(
                    p.X + along.X * reach + across.X * lateral,
                    p.Y + along.Y * reach + across.Y * lateral), true));
            }
            var geometry = new PathGeometry([figure]); geometry.Freeze();

            // Cached: five bolts a frame for as long as the failure board is up.
            var pen = KabloomPalette.WhitePen((byte)(255 * alpha * LightningAlpha), Math.Max(1.1, size * 0.13),
                round: true);
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    // Projected straight into the figure: the Select/ToArray this replaced allocated a closure, an
    // enumerator and a Point[] per petal per frame purely to reshape six points.
    private static Geometry Polygon(IReadOnlyList<Vec2> vertices, Point origin, double scale)
    {
        var figure = new PathFigure
        { StartPoint = Screen(vertices[0], origin, scale), IsClosed = true, IsFilled = true };
        for (int i = 1; i < vertices.Count; i++)
            figure.Segments.Add(new LineSegment(Screen(vertices[i], origin, scale), true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>The clue numerals are a five-value alphabet at one size per board: laying out ~70
    /// <c>FormattedText</c>s and building their glyph outlines every frame was Kabloom's largest text cost.
    /// Cached with the ink bounds; the size is quantised to an eighth of a pixel and the table is capped, so
    /// the level transition's animating scale cannot grow it without bound. The ink index is clamped: the
    /// palette has one entry per possible degree, and a grid with a sixth neighbour must not index out of
    /// range on the render pump.</summary>
    private static readonly Dictionary<(int Clue, int SizeQ, int PpdQ), (FormattedText Text, Rect Ink)> ClueGlyphs = new();

    private static (FormattedText Text, Rect Ink) ClueGlyph(int clue, double size, double ppd)
    {
        var key = (clue, (int)Math.Round(size * 8), (int)Math.Round(ppd * 100));
        if (ClueGlyphs.TryGetValue(key, out var hit)) return hit;
        Brush ink = KabloomPalette.Clues[Math.Clamp(clue, 1, KabloomPalette.Clues.Length - 1)];
        var text = ArcadeChrome.Text(clue.ToString(), size, ink, ppd);
        hit = (text, text.BuildGeometry(new Point()).Bounds);
        if (ClueGlyphs.Count >= 512) ClueGlyphs.Clear();
        ClueGlyphs[key] = hit;
        return hit;
    }

    /// <summary>A stable 0..1 from two ints. The renderer needs its own — the sim's is private, and a
    /// presentation detail has no business reaching into the simulation for a random number anyway.</summary>
    private static double HashUnit(int a, int b)
    {
        uint value = (uint)(a * 0x45D9F3B) ^ (uint)(b * 0x119DE1F3) ^ 0x9E3779B9u;
        value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15;
        return (value & 0xFFFF) / 65535.0;
    }

    private static double Smooth(double t) => ArcadeMath.Smoothstep(t);

    private static double BackOut(double t)
    {
        t = Math.Clamp(t, 0, 1) - 1;
        const double overshoot = 1.70158;
        return 1 + t * t * ((overshoot + 1) * t + overshoot);
    }

    private static Geometry Polygon(IReadOnlyList<Point> vertices)
    {
        var figure = new PathFigure { StartPoint = vertices[0], IsClosed = true, IsFilled = true };
        for (int i = 1; i < vertices.Count; i++) figure.Segments.Add(new LineSegment(vertices[i], true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    private static Point Screen(Vec2 p, Point origin, double scale) =>
        new(origin.X + p.X * scale, origin.Y + p.Y * scale);

    // ── How-to-play illustrations ─────────────────────────────────────────────
    // One per bullet on the △ card. Drawn from Kabloom's own palette and its own shapes — a petal here is
    // the same five-sided tile the board draws, so the card teaches the thing the player is about to see
    // rather than a diagram of it. Deliberately built from primitives rather than by calling the board's
    // draw path: those routines want a live grid, a scale and a presentation clock, and a picture on a
    // static card has none of them.

    /// <summary>A petal roughly the shape the crop draws, centred in <paramref name="box"/>. Not the real
    /// <c>PetalVertices</c> geometry, which is cut from a live cell's neighbours — this is the same
    /// silhouette expressed directly, which is all a thumbnail needs.</summary>
    private static Geometry HowToPetal(Rect box, double scale = 1.0)
    {
        var c = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
        double r = Math.Min(box.Width, box.Height) / 2 * scale;
        var pts = new Point[5];
        for (int i = 0; i < 5; i++)
        {
            // Point-down, so the narrow end reads as the hub side exactly as it does on the board.
            double a = Math.PI / 2 + i * Math.PI * 2 / 5;
            pts[i] = new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
        }
        return Polygon(pts);
    }

    /// <summary>A petal on the how-to card, wearing whatever the board wears. The card's petal is already
    /// point-down, which is the orientation petal art is drawn in, so it needs no turn — but it does need the
    /// same re-stroked edge, for the same reason.</summary>
    /// <remarks><paramref name="slot"/> is null where the face the card teaches wears no art — a cleared
    /// petal — so the card shows the same plain fill the board does.
    /// ⚠ The null must be caught before <c>ArcadeSprites.Frame</c>: that takes a non-nullable slot and looks
    /// it up in a dictionary, so a null would throw out of the how-to card's own draw.</remarks>
    private static void HowToPetalFace(DrawingContext dc, Rect box, Brush fill, string? slot)
    {
        var shape = HowToPetal(box);
        dc.DrawGeometry(fill, KabloomPalette.TilePen, shape);
        if (slot is null || ArcadeSprites.Frame(slot) is not { } art) return;
        ArcadeSprites.DrawClipped(dc, art, shape);
        dc.DrawGeometry(null, KabloomPalette.TilePen, shape);
    }

    public void DrawHowToArt(DrawingContext dc, Rect box, string art, double ppd)
    {
        var c = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
        double r = Math.Min(box.Width, box.Height) / 2;

        switch (art)
        {
            // Two petals, one covered and one revealed — "under every petal is a flower or a bee" is a
            // statement about the two states, so the picture has to show both at once.
            case "petals":
            {
                var left  = new Rect(box.X, box.Y + box.Height * 0.10, box.Width * 0.62, box.Height * 0.80);
                var right = new Rect(box.X + box.Width * 0.38, box.Y + box.Height * 0.10,
                                     box.Width * 0.62, box.Height * 0.80);
                HowToPetalFace(dc, right, KabloomPalette.Revealed, null);
                HowToPetalFace(dc, left, KabloomPalette.CoveredA, ArcadeSprites.Slot.KabloomPetal);
                break;
            }

            // A revealed petal wearing a 3, in the same ink the board's clue-3 uses.
            case "clue":
            {
                HowToPetalFace(dc, box, KabloomPalette.Revealed, null);
                FormattedText t = ArcadeChrome.Text("3", r * 1.05, KabloomPalette.Clues[3], ppd);
                ArcadeChrome.DrawInkCentered(dc, t, c);
                break;
            }

            // The reticle over a covered petal: the cursor is the thing ✕ acts on, and it is the one piece
            // of Kabloom's vocabulary a player must locate before anything else makes sense.
            case "cursor":
            {
                HowToPetalFace(dc, box, KabloomPalette.CoveredA, ArcadeSprites.Slot.KabloomPetal);
                var halo = new Pen(KabloomPalette.PointerRim, Math.Max(2, r * 0.20)); halo.Freeze();
                var ring = new Pen(KabloomPalette.PointerInk, Math.Max(1, r * 0.10)); ring.Freeze();
                dc.DrawEllipse(null, halo, c, r * 0.46, r * 0.46);
                dc.DrawEllipse(null, ring, c, r * 0.46, r * 0.46);
                break;
            }

            // The marker, on the pale petal it can only ever sit on — dark ink, the opposite treatment to
            // the white revealed bee below it on the card. A guess and a fact must not look alike, and the
            // card is the one place both appear together to be compared.
            // ⚠ Through DrawBeeFlag, never a bare DrawBee: the card teaches the mark, so ring, ground disc
            // and glyph have to be the ones the board stamps. Settled (progress 1) — the panel is a picture
            // of the mark at rest, not of it landing.
            case "flag":
                HowToPetalFace(dc, box, KabloomPalette.CoveredA, ArcadeSprites.Slot.KabloomPetal);
                DrawBeeFlag(dc, c, r * 0.55, 1);
                break;

            // "The first petal is always safe": a revealed, empty petal with the cursor still on it — the opening
            // press and what it always finds. (The bee itself is taught by the flag line above it.)
            case "safe":
            {
                HowToPetalFace(dc, box, KabloomPalette.Revealed, null);
                var halo = new Pen(KabloomPalette.PointerRim, Math.Max(2, r * 0.20)); halo.Freeze();
                var ring = new Pen(KabloomPalette.PointerInk, Math.Max(1, r * 0.10)); ring.Freeze();
                dc.DrawEllipse(null, halo, c, r * 0.46, r * 0.46);
                dc.DrawEllipse(null, ring, c, r * 0.46, r * 0.46);
                break;
            }
        }
    }
}
