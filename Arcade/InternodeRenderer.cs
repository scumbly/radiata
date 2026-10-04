using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>Stateless, code-only renderer for Internode. Everything it draws is read off the sim's own state and
/// clocks, so a frozen game paints the same frame forever and drawing can never change an outcome.
///
/// <para>Projection: the pipe's cross-section is the disc. A view depth <c>zv</c> in seconds maps to a ring of
/// unit radius <c>R(zv) = Rvp + (Rmouth − Rvp) / (1 + k·zv)</c>; an object at sim <c>Z_e</c> sits at
/// <c>zv = (Z_e − Z) + ZLead</c>, the runner at <c>zv = ZLead</c>. Screen angle (0 = 12 o'clock, clockwise) of
/// pipe angle θ is <c>π − θ</c>, so stick-right is screen-right at 6 o'clock; the course twist adds
/// <c>TwistAt(zv − ZLead) − TwistAt(0)</c> per ring, and each ring's surface arc spans <c>±RimAt(zv − ZLead)</c>
/// so the sky wedge opens and closes down the pipe. One rotation about the disc centre carries the camera roll
/// (θ/6, shaped to 0 across the last stretch before the top) plus <c>TwistAt(0)</c>, both zeroed by the CAMERA
/// ROLL setting. WPF's RotateTransform is clockwise-positive, which is what a right-wall climb must look like.</para>
///
/// <para>Draw order is load-bearing: sky, fog, checkered rings far to near, the split-edge seams, the gate,
/// events beyond the runner, the runner's shadow and the runner, events already past it, bursts; then outside
/// the rotation the rim vignette, the HUD plaque, the shout and the cards.</para></summary>
internal sealed class InternodeRenderer : IArcadeRenderer
{
    public static readonly InternodeRenderer Instance = new();
    public Brush Accent => InternodePalette.Accent;

    // ── Projection ────────────────────────────────────────────────────────────
    private const double Rmouth = 1.06;
    private const double Rvp = 0.05;
    /// <summary>Perspective compression: <see cref="InternodeTuning.ViewSpeed"/> is the whole of perceived speed, since
    /// z is seconds of travel and the sim owns arrival times.</summary>
    private static double K => _k;
    private static double _k = 1.6 * Math.Clamp(InternodeTuning.ViewSpeed, 0.5, 40);
    /// <summary>The runner's depth, held at a fixed K·z so their ring keeps the same screen radius at every speed.
    /// Without this the runner shrinks toward the vanishing point as the view compresses.</summary>
    private static double ZLead => LeadKz / K;

    /// <summary>Where the camera sits behind the runner, as K·z. It is the whole of the near composition:
    /// it fixes the runner's ring radius — and so how far down the disc the bike rides — and every event's
    /// screen position is measured from it, so bringing it in enlarges and spreads the whole near field, not
    /// just the player.
    ///
    /// <para>⚠ Moves together with <see cref="RunnerArtScale"/>: the bike's size is its art height times this
    /// ring, so changing one without the other resizes the player as a side effect of a camera move.</para>
    ///
    /// <para>⚠ <b>Smaller means the camera is closer</b>, which rides the bike further out toward the rim and
    /// scales it up in step — <c>R(ZLead) = Rvp + (Rmouth − Rvp) / (1 + LeadKz)</c>, in which K cancels, so
    /// this alone fixes the seat at every view speed. At 0.35 the runner sits at 0.798 of the field.
    /// The scale-up needs no second knob: the bike is <see cref="RunnerHalf"/> × that same ring.</para>
    ///
    /// <para>⚠ It is not a runner-only knob, and that is inherent, not an oversight: every event's screen
    /// position is measured from this depth, so the whole near field spreads and enlarges with it, and
    /// <see cref="ZFar"/> shortens by the same shift. The sprite brightness curve normalises against
    /// <c>R(ZLead)</c>, so it re-fits on its own.</para></summary>
    private const double LeadKz = 0.35;
    /// <summary>Draw distance, seconds of travel; its own knob, not a function of speed.</summary>
    private static double ZFar => Math.Clamp(InternodeTuning.ViewHorizonSeconds, 0.2, 8) + ZLead;
    /// <summary>Seconds of travel per checker band. The far rings a short band adds cost nothing: rings under
    /// 1.5 px are skipped and sectors under 2 px collapse to an annulus in <see cref="DrawRing"/>.</summary>
    private static double BandSeconds => Math.Clamp(InternodeTuning.ViewBandSeconds, 0.02, 1.0);
    private const int SectorsPerRing = 8;
    /// <summary>Caps ZFar / BandSeconds + 2 for any tuning the override file can name; the station loop clamps to
    /// it, so a silly pair costs draw distance rather than an overrun.</summary>
    private const int MaxStations = 96;
    /// <summary>Sprites drawn at once. A mine ring is 17 and two rings plus orbs are routinely in view; far sprites
    /// under a pixel are skipped by their own draw routines, so the cap only has to be generous, not tight: it matches InternodeTuning.MaxEventsPerSection, so it can never truncate.</summary>
    private const int MaxSprites = 576;
    /// <summary>Share of the draw distance over which sprites and the gate fade in at the horizon.</summary>
    private static double FadeSeconds => 0.25 * ZFar;
    /// <summary>Turns the bend into a vanishing-point swing, in units of the displaced ring's own radius. The bend is
    /// a heading rate: it integrates once to heading and again to displacement, measured in the runner's own
    /// frame, so the pipe under the runner never moves and the swing grows with the square of the distance. A
    /// full turn still carries the far end several ring radii out, past the near wall, where the occlusion clips in
    /// <see cref="Draw"/> hide it. The soft limit only rounds off the extreme.</summary>
    private static double BendGain => Math.Clamp(InternodeTuning.ViewTurnGain, 0, 20);
    private const double OffsetLimit = 6.0;

    // Sprite sizes in pipe radii (one unit of the ring they sit on). Cel pens are in sprite unit space
    // (InternodePalette.CelUnit, MineEdgeUnit), so they scale with these. The runner's shadow shrink below is tied
    // to JumpMaxApex so it spans the tallest double jump.
    /// <summary>0.156 and 0.2088, sized for legibility down the pipe.
    ///
    /// <para>⚠ Drawn size, but not free of the rules. The catch and hit windows are angular and live in the
    /// tuning (<c>TokenHalfWidthDeg</c> 16°, <c>MineHalfWidthDeg</c> 15.2°). The token's growth costs nothing;
    /// the mine's does not, because a mine row has to keep blocking while its art stops overlapping, and two
    /// mines only clear each other at a step of <c>2·asin(MineRadius)</c> — 24.1° here. That is what pushed
    /// the mine width to 15.2° and a ring to seven mines; see <c>InternodePhrases.RingMines</c>. Growing this
    /// again reopens that whole question.</para></summary>
    private const double OrbRadius = 0.156;
    private const double MineRadius = 0.2088;

    /// <summary>How far up the screen a sprite has to be before it is at full brightness — the share of the
    /// way from the vanishing point to the runner. Distance still darkens, but it lets go early, so a token
    /// or a mine is readable for most of the time it is on screen rather than only at the end.
    ///
    /// <para>⚠ Screen position is crushed by perspective: a sprite four seconds out sits at 0.008 of the way
    /// up, one second out at 0.07. At 0.08, full brightness arrives around three quarters of a second
    /// out.</para></summary>
    private const double BrightFullAtScreen = 0.08;

    /// <summary>Brightness a sprite never falls below, however far away it is: without this floor the curve
    /// would run to black at the vanishing point, leaving a mine four seconds out at 5% and effectively
    /// invisible until too late to route around.
    /// ⚠ It is a floor on the sprite, not on the track: the pipe still darkens into its own horizon, which is
    /// what a sprite standing at half brightness now reads against.</para></summary>
    private const double SpriteBrightFloor = 0.45;
    private const double RunnerHalf = 0.22;
    /// <summary>Share of a ground sprite's radius it is lifted off the surface, so it stands on the floor rather than
    /// being bisected by it.</summary>
    private const double GroundLift = 1.0;

    private static double R(double zv) => Rvp + (Rmouth - Rvp) / (1 + K * zv);
    private static double Depth(double zv) => 1 / (1 + K * zv);

    /// <summary>How near the mouth a view depth is, 1 at the runner's plane falling to 0 at the far plane —
    /// the weight the rim seam and the gap lips ride.
    ///
    /// <para>⚠ Linear in distance, deliberately not in <see cref="Depth"/>. That is the perspective term,
    /// <c>1/(1 + K·zv)</c> with K around 16 at ordinary speed, so it is already down to ~0.06 one second
    /// down the pipe: used as a gradient it lands on the floor within a nose-length of the runner and every
    /// line past that draws at one weight, which reads as no scaling at all.</para></summary>
    private static double DepthShare(double zv) => Math.Clamp(1 - zv / ZFar, 0, 1);

    /// <summary>How thin a depth-weighted seam or lip may get. ⚠ Not zero: a gap lip is judged from a long
    /// way off, and one that fades out entirely hides the edge a player is timing a jump against.</summary>
    private const double EdgeWeightFloor = 0.35;

    /// <summary>The seam's base stroke at the mouth, by disc size — the weights the flat pens carried before
    /// depth entered into it. ⚠ Keep in step with <c>InternodePalette.SplitEdge</c>/<c>SplitEdgeFine</c>,
    /// which are still the brushes these are struck from.</summary>
    private const double SplitEdgeWidth = 4.2;
    private const double SplitEdgeFineWidth = 3.0;

    /// <summary>Shapes <see cref="DepthShare"/> into the stroke multiplier. Above 1 at the mouth so the near
    /// rim gains weight rather than only losing it with distance, and a straight ramp from there — a power
    /// curve on top of the floor spends most of the pipe clamped, which is indistinguishable from flat.</summary>
    private static double EdgeWeight(double zv) =>
        Math.Max(EdgeWeightFloor, 1.2 * DepthShare(zv));

    /// <summary>How much of its own colour a depth-weighted line (rim seam, gap lip, gravity seam) shows at view
    /// depth <paramref name="zv"/>: the floor's own <see cref="DistanceBrightness"/>, so the lines darken with the
    /// track they edge, but never under <see cref="EdgeFadeFloor"/> — a lip is judged from a long way off.</summary>
    private static double EdgeFade(double zv) => Math.Max(EdgeFadeFloor, DistanceBrightness(R(zv)));
    private const double EdgeFadeFloor = 0.10;

    /// <summary>How bright the track and sky are left by distance at screen radius <paramref name="u"/> (in
    /// field radii from the vanishing point: a ring's own radius, <see cref="R"/>): 1 is untouched, 0 is black.
    ///
    /// <para>⚠ Measured on screen, not in seconds down the pipe. The perspective term puts nearly the whole
    /// visible pipe inside the first second of a six-second horizon, so a fade over time lands in the last few
    /// pixels around the vanishing point; by screen radius it reads the same at every stage speed. The knots are
    /// a measured reference look; retune by editing
    /// them.</para></summary>
    private static double DistanceBrightness(double u)
    {
        if (u <= DarkU[0]) return DarkB[0];
        for (int k = 1; k < DarkU.Length; k++)
            if (u <= DarkU[k]) return DarkB[k - 1] + (DarkB[k] - DarkB[k - 1]) * (u - DarkU[k - 1]) / (DarkU[k] - DarkU[k - 1]);
        return 1;
    }
    private static readonly double[] DarkU = [0.00, 0.05, 0.10, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.85];
    private static readonly double[] DarkB = [0.00, 0.03, 0.09, 0.29, 0.47, 0.68, 0.80, 0.88, 0.945, 0.995, 1.00];
    /// <summary>The base rim, for the how-to card only; the board reads the profile per station.</summary>
    private static double RimRad => InternodePhysics.RimRad;

    /// <summary>How deep a ring has to be on screen, in pixels, before its checker squares are given their
    /// white edge. Below it the band is a couple of pixels of near-black and the 6% hairline renders nothing
    /// visible — while still costing a stroke pass on the most-called geometry in the game.</summary>
    private const double CheckEdgeMinDepthPx = 4.0;

    /// <summary>How many breaks in the floor one ring will paint, and it must cover a whole section's worth.
    ///
    /// <para>⚠ Not "a few, because a plank is two". Far rings merge — a band is only painted once it is 1.5 px
    /// deep, so one far band can span several seconds of course and take in every gap in it. Anything past
    /// this cap is silently not painted, which means floor drawn over a hole the physics still drops the
    /// runner through: harmless at the far plane where nothing is legible, but it is a lie that resolves into
    /// holes as it approaches, and exactly the class of thing that is invisible until someone falls.</para>
    ///
    /// <para>⚠ The bound is the sequencer's, and hand-deriving it here went stale the first time that
    /// budget moved. It lives in <see cref="InternodeTuning.MaxHolesPerRing"/> now, where InternodeProbe
    /// measures the worst section it can actually build and asserts against it. A local const here only
    /// because the scratch arrays below need a compile-time size.</para></summary>
    private const int MaxHolesPerRing = 160;

    // ── Scratch (singleton, UI thread only) ──────────────────────────────────
    private static int _stations;
    private static int _nFirst;
    /// <summary>Section depth of view depth 0 this frame: station i sits at view depth (_nFirst + i)·BandSeconds − _zBase, clamped.</summary>
    private static double _zBase;
    private static readonly InternodeGap[] _holes = new InternodeGap[MaxHolesPerRing];
    /// <summary>Each of <see cref="_holes"/>' drawn span (<see cref="BuildDrawnSpans"/>), section seconds.</summary>
    private static readonly double[] _holeD0 = new double[MaxHolesPerRing];
    private static readonly double[] _holeD1 = new double[MaxHolesPerRing];
    /// <summary>The depths one ring is split at (<see cref="DrawRing"/>). Past the cap the last part keeps
    /// the rest unsplit.</summary>
    private static readonly double[] _ringSplit = new double[128];
    private static readonly double[] _holeT0 = new double[MaxHolesPerRing];
    private static readonly double[] _holeT1 = new double[MaxHolesPerRing];
    // The same holes on the ring's far arc: a swept hole cuts the two arcs at different angles.
    private static readonly double[] _holeS0 = new double[MaxHolesPerRing];
    private static readonly double[] _holeS1 = new double[MaxHolesPerRing];
    private static readonly double[] _zv = new double[MaxStations];
    private static readonly double[] _rad = new double[MaxStations];
    /// <summary>This frame's occlusion clips, kept so a falling bike can lift them and push the same ones back.</summary>
    private static readonly Geometry?[] _clip = new Geometry?[MaxStations];
    /// <summary>Which of <see cref="_clip"/> were pushed this frame.</summary>
    private static readonly bool[] _clipOn = new bool[MaxStations];
    /// <summary>How far a farther disc may poke past a nearer one before the nearer one's clip is needed: under
    /// half a pixel of overdraw at the very edge of a turn is not visible.</summary>
    private const double ClipSlackPx = 0.5;
    private static readonly double[] _offX = new double[MaxStations];
    private static readonly double[] _offY = new double[MaxStations];
    private static readonly double[] _headX = new double[MaxStations];
    private static readonly double[] _headY = new double[MaxStations];
    private static readonly double[] _tw = new double[MaxStations];
    private static readonly double[] _rim = new double[MaxStations];
    private static readonly int[] _visible = new int[MaxSprites];
    private static int _visibleCount;
    /// <summary>Which checker pair the pipe is wearing: the gate count, so the ground changes on every pass.</summary>
    private static int _theme;
    /// <summary>The theme the pipe is fading from and how far along the fade is (1 = settled).</summary>
    private static int _themeFrom;
    private static double _themeBlend = 1;
    private const double ThemeFadeSeconds = 1.0;
    /// <summary>The roll currently drawn, chasing the runner's at most this many degrees per second.
    ///
    /// <para>⚠ It bounds the course's twist as well as the runner's own swing — both feed one target — so
    /// lowering it trades a gentler centre-drop against the view lagging a hard corkscrew. The shaped part of
    /// the target spans about ±17°, so a sign flip through the centre is a ~33° swing: at this rate it takes
    /// roughly a second and a third to settle.</para></summary>
    private static double _rollShown;
    private const double RollMaxDegPerSec = 25;
    private static Internode? _presentedGame;
    private static long _wallStamp;

    /// <summary>Wall-clock seconds since the last frame, clamped — presentation-only smoothing (roll, theme fade)
    /// runs on the renderer's own clock so a paused or frozen sim still settles what it was mid-way through.</summary>
    private static double WallStep(Internode g)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double dt = _wallStamp == 0 || !ReferenceEquals(g, _presentedGame) ? 0 : (now - _wallStamp) / (double)System.Diagnostics.Stopwatch.Frequency;
        _wallStamp = now;
        return Math.Clamp(dt, 0, 0.1);
    }

    public void Draw(DrawingContext dc, Point c, double field, IArcadeGame game, double ppd)
    {
        if (game is not Internode g) return;
        double S = field;

        // Perceived speed rides the stage: the depth compression is rebuilt per frame from the game's own number.
        _k = 1.6 * Math.Clamp(g.ViewSpeedNow, 0.5, 40);
        BuildStations(g);
        BuildDrawnSpans(g);
        double dtWall = WallStep(g);
        int theme = ((g.PipeTheme % InternodePalette.Themes) + InternodePalette.Themes) % InternodePalette.Themes;
        // The pipe takes a new colour over ThemeFadeSeconds rather than on one frame: the previous theme is kept
        // and the rings draw both, the new one over the old at the blend. A fresh game or a restore snaps.
        if (theme != _theme)
        {
            _themeFrom = _theme;
            _theme = theme;
            _themeBlend = ReferenceEquals(g, _presentedGame) ? 0 : 1;
        }
        else if (_themeBlend < 1) _themeBlend = Math.Min(1, _themeBlend + dtWall / ThemeFadeSeconds);
        double twist0 = g.TwistAt(0);
        double rimHere = g.RimAt(0);
        double rollTarget = g.CameraRoll ? ShapedRollDeg(g.Theta, rimHere / InternodePhysics.Deg) / 6 + twist0 * 180 / Math.PI : 0;
        // The roll drawn follows the roll the runner asks for at a bounded rate. A top cross that falls short
        // drops the runner straight through the centre, and the shaped roll flips sign in a frame or two; drawn
        // raw that is a flick of the whole frame, which is the one thing a rolling camera must never do.
        if (!ReferenceEquals(g, _presentedGame)) _rollShown = rollTarget;
        else _rollShown += Math.Clamp(rollTarget - _rollShown, -RollMaxDegPerSec * dtWall, RollMaxDegPerSec * dtWall);
        _presentedGame = g;
        double roll = _rollShown;
        bool rolled = Math.Abs(roll) > 1e-3;
        var rollTransform = rolled ? new RotateTransform(roll, c.X, c.Y) : null;
        if (rollTransform is not null) dc.PushTransform(rollTransform);

        PrepareSky(g, S);
        DrawSky(dc, c, S);
        // Occlusion: everything beyond boundary i is drawn inside the disc of boundary i, so the pipe bends out
        // of view around a turn and nothing behind the corner shows until the pipe straightens. Boundary 0 is
        // the mouth, wider than the disc, and needs no clip.
        //
        // ⚠ On the GPU a boundary's clip is pushed only where it can cut something. It stays up for everything
        // farther than it, all of which lies inside the farther boundaries' own discs, so a disc that holds every
        // farther disc clips nothing; on a straight stretch that is every disc, and the nested stack cost the
        // hardware path up to 45 % of a curving frame (measured on Windows 10, RTX 3060 Ti). In SOFTWARE rendering (render tier 0, which is every Remote
        // Desktop session) the full stack is kept: there the nested discs confine the rasteriser's work, and
        // dropping them made curving frames several times slower. Measured with tools/InternodeFrameProbe.
        int clips = Math.Max(0, _stations - 2);
        bool everyClip = (RenderCapability.Tier >> 16) == 0;
        for (int i = 1; i <= clips; i++)
        {
            double cr = _rad[i] * S;
            var ci = StationCentre(c, S, i);
            bool needed = everyClip;
            for (int k = i + 1; k < _stations && !needed; k++)
            {
                var ck = StationCentre(c, S, k);
                double d = Math.Sqrt((ck.X - ci.X) * (ck.X - ci.X) + (ck.Y - ci.Y) * (ck.Y - ci.Y));
                needed = d + _rad[k] * S > cr + ClipSlackPx;
            }
            _clipOn[i] = needed;
            if (needed) dc.PushClip(_clip[i] = new EllipseGeometry(ci, cr, cr));
        }
        DrawFog(dc, c, S);
        CollectVisible(g);
        // Thin far rings merge into the next drawable band instead of being skipped one by one: skipping left a
        // bare annulus between the last drawn ring and the far end, with sprites floating in it.
        int far = _stations - 1;
        // Sprites are drawn one ring late: a sprite standing on band i reaches outward into the annulus of the nearer
        // band i−1, which is painted after it, so drawing band i's sprites right after ring i left far orbs and mines
        // "clipped" by the floor in front of them. Each ring paints, then the sprites of the band behind it.
        double drawnFrom = double.PositiveInfinity;
        bool falling = g.Phase == InternodePhase.Falling, runnerDrawn = false;
        for (int i = _stations - 2; i >= 0; i--)
        {
            double zLo = _zv[i], zHi = i + 1 == _stations - 1 ? double.PositiveInfinity : _zv[i + 1];
            int farOld = far;
            bool painted = (_rad[i] - _rad[far]) * S >= 1.5 || i == 0;
            // Through the floor the bike drops outward, behind the track, while carrying on down the pipe: drawn
            // before the floor of the band it has reached paints, so that floor and every nearer one cover it and
            // it is seen only through a hole. ⚠ With this boundary's clips lifted: they are the pipe's own
            // outline, and the bike has left the pipe — clipped, it would be cut off inside the very hole it is
            // seen through.
            if (falling && !runnerDrawn && painted && _zv[i] <= ZLead + g.FallAhead)
            {
                int open = Math.Min(i, clips);
                for (int k = 1; k <= open; k++) if (_clipOn[k]) dc.Pop();
                DrawRunner(dc, c, S, g, rimHere);
                for (int k = 1; k <= open; k++) if (_clipOn[k]) dc.PushClip(_clip[k]!);
                runnerDrawn = true;
            }
            if (painted)
            {
                // A break in the floor is simply not painted over its arc: the sky shows through the hole.
                // ⚠ Both readers share the _holes scratch this fills, so nothing may run between them.
                int holes = GapsBetween(g, _zv[far], _zv[i]);
                if (holes >= 0)
                {
                    DrawRing(dc, c, S, g, i, far, holes);
                    DrawSeamSegment(dc, c, S, g, i, far, holes);
                    DrawGravitySeam(dc, c, S, g, i, far, holes);
                }
                far = i;
            }
            // The ring is painted inside this boundary's clip, but what stands on it is not: a sprite sitting on
            // the floor straddles the boundary circle, and clipping it there cut every near orb and mine in half.
            if (i >= 1 && i <= clips && _clipOn[i]) dc.Pop();
            DrawGapEdges(dc, c, S, g, twist0, zLo, zHi);
            DrawGate(dc, c, S, g, twist0, zLo, zHi);
            if (painted)
            {
                DrawEvents(dc, c, S, g, twist0, Math.Max(_zv[farOld], ZLead), drawnFrom);
                drawnFrom = _zv[farOld];
            }
        }
        DrawEvents(dc, c, S, g, twist0, ZLead, drawnFrom);

        // ⚠ The readout goes down here, under the runner and everything nearer than it: the runner
        // must read over the plaque, and it must keep its place under the sprites passing nearer than it, so
        // it is the plaque that moves, not the runner. The plaque is upright and the runner rolls, so the roll
        // comes off for the plaque and goes back on. The rim vignette comes with it: laid over the plaque it
        // would darken the readout's top edge, and the near field it no longer covers is the runner and what
        // passes it, which want to read clearly at the rim.
        if (rolled) dc.Pop();
        dc.DrawEllipse(InternodePalette.RimVignette, null, c, S, S);
        DrawHud(dc, c, S, g, ppd);
        if (rollTransform is not null) dc.PushTransform(rollTransform);

        if (!runnerDrawn) DrawRunner(dc, c, S, g, rimHere);
        DrawEvents(dc, c, S, g, twist0, double.NegativeInfinity, ZLead);
        DrawBursts(dc, c, S, g);
        DrawGoldFlashes(dc, c, S, g, twist0);
        DrawCollects(dc, c, S, g);

        if (rolled) dc.Pop();

        DrawShoutIfAny(dc, c, S, g, ppd);
        DrawBonusToasts(dc, c, S, g, ppd);

        // No card at a gate: the plaque's CHECKPOINT / LEVEL UP row and the bonus toasts carry it.
        if (g.Phase == InternodePhase.Intro) DrawIntro(dc, c, S, ppd);
    }

    /// <summary>The roll follows θ until the rim at the runner, then runs linearly back to 0 at the top so the
    /// wrap has no snap. The knee never passes the base rim: a closed tube has no rim, and without a tail the
    /// roll would snap from +30° to −30° as θ wraps. Degrees.</summary>
    private static double ShapedRollDeg(double theta, double rimDeg)
    {
        double deg = theta * 180 / Math.PI;
        double knee = Math.Min(rimDeg, InternodeTuning.RimDeg);
        double mag = Math.Abs(deg);
        if (mag <= knee) return deg;
        double tail = Math.Max(1e-6, 180 - knee);
        return Math.Sign(deg) * knee * Math.Clamp((180 - mag) / tail, 0, 1);
    }

    // ── Stations ──────────────────────────────────────────────────────────────

    /// <summary>Ring boundaries sit at world distance n·BandSeconds, read from the sim's Z, so a frozen game
    /// paints the same frame. Station i holds the boundary's view depth, ring radius, vanishing-point offset
    /// (screen units of its own radius, y down), relative twist and rim.</summary>
    private static void BuildStations(Internode g)
    {
        double zBase = g.Z - ZLead;
        _zBase = zBase;
        _nFirst = (int)Math.Floor(zBase / BandSeconds);
        int nLast = (int)Math.Ceiling((zBase + ZFar) / BandSeconds);
        int count = Math.Clamp(nLast - _nFirst + 1, 2, MaxStations);
        _stations = count;
        double twist0 = g.TwistAt(0);
        double hx = 0, hy = 0, dx = 0, dy = 0, prevBx = 0, prevBy = 0, prevZ = 0;
        for (int i = 0; i < count; i++)
        {
            double zv = Math.Clamp((_nFirst + i) * BandSeconds - zBase, 0, ZFar);
            _zv[i] = zv;
            _rad[i] = R(zv);
            _tw[i] = g.TwistAt(zv - ZLead) - twist0;
            _rim[i] = g.RimAt(zv - ZLead);
            var (bx, by) = g.BendAt(zv - ZLead);
            if (i > 0)
            {
                double dz = zv - prevZ;
                double hx0 = hx, hy0 = hy;
                hx += 0.5 * (prevBx + bx) * dz;
                hy += 0.5 * (prevBy + by) * dz;
                dx += 0.5 * (hx0 + hx) * dz;
                dy += 0.5 * (hy0 + hy) * dz;
            }
            _headX[i] = hx;
            _headY[i] = hy;
            _offX[i] = dx;
            _offY[i] = dy;
            prevBx = bx; prevBy = by; prevZ = zv;
        }
        // The runner's own frame is the reference: displacement relative to where the runner's current heading
        // would carry the pipe, so the ring underfoot stays centred and only the course ahead swings.
        double refX = Interp(_offX, ZLead), refY = Interp(_offY, ZLead);
        double headRefX = Interp(_headX, ZLead), headRefY = Interp(_headY, ZLead);
        double vertical = Math.Clamp(InternodeTuning.BendVerticalScale, 0, 1);
        for (int i = 0; i < count; i++)
        {
            double ahead = _zv[i] - ZLead;
            _offX[i] = SoftLimit(BendGain * (_offX[i] - refX - headRefX * ahead));
            _offY[i] = -SoftLimit(BendGain * vertical * (_offY[i] - refY - headRefY * ahead));
        }
    }

    private static double SoftLimit(double v) => OffsetLimit * Math.Tanh(v / OffsetLimit);

    private static double Interp(double[] values, double zv)
    {
        if (_stations <= 0) return 0;
        if (zv <= _zv[0]) return values[0];
        if (zv > _zv[_stations - 1]) return values[_stations - 1];
        // Stations are a uniform grid bar the clamped ends, so the bracket is computed and then nudged: the
        // nudge covers the clamps and rounding, and leaves _zv[i − 1] < zv ≤ _zv[i]. Called for every lip point
        // and ring part, where a scan over ~75 stations each time added up.
        int i = Math.Clamp((int)Math.Floor((zv + _zBase) / BandSeconds) - _nFirst + 1, 1, _stations - 1);
        while (i > 1 && zv <= _zv[i - 1]) i--;
        while (i < _stations - 1 && zv > _zv[i]) i++;
        double span = _zv[i] - _zv[i - 1];
        double t = span <= 1e-9 ? 1 : (zv - _zv[i - 1]) / span;
        return values[i - 1] + (values[i] - values[i - 1]) * t;
    }

    private static Point Centre(Point c, double S, double zv)
    {
        double r = R(zv) * S;
        return new Point(c.X + Interp(_offX, zv) * r, c.Y + Interp(_offY, zv) * r);
    }

    private static Point StationCentre(Point c, double S, int i)
    {
        double r = _rad[i] * S;
        return new Point(c.X + _offX[i] * r, c.Y + _offY[i] * r);
    }

    /// <summary>Screen point of pipe angle <paramref name="theta"/> at height <paramref name="h"/> on the ring
    /// at view depth <paramref name="zv"/>.</summary>
    private static Point At(Point c, double S, double zv, double theta, double h, double relTwist) =>
        Polar(Centre(c, S, zv), R(zv) * (1 - h) * S, Math.PI - theta + relTwist);

    // ── Sky, rings, seams ─────────────────────────────────────────────────────

    /// <summary>One tile's span on screen, in field radii. Smaller means more tiles across the disc and a
    /// faster-reading pan.
    ///
    /// <para>At <b>4.0</b> the disc shows about half the texture. ⚠ The arithmetic is worth keeping: the
    /// playfield is a disc of radius <c>S</c>, so 2·S across, and the visible fraction of a tile is
    /// <c>2·S / (S · this)</c> — i.e. <c>2 / this</c>. A value near 1 shows nearly the whole tile and reads
    /// as a repeating pattern rather than a place.</para>
    ///
    /// <para>⚠ It changes how the pan reads without <see cref="SkyPanGain"/> moving. The gain is in pixels,
    /// so the same camera turn now slides the sky across a smaller share of the texture — a distant sky that
    /// drifts rather than scrolls, which is right, but don't be surprised the two are coupled.</para></summary>
    private const double SkyTileSpan = 4.0;

    /// <summary>How far the sky slides per unit of camera turn, in field radii. The turn is bend × seconds,
    /// so this is the only place the pan's strength is set.</summary>
    private const double SkyPanGain = 1.6;

    private static ImageBrush? _skyBrush;
    private static BitmapSource? _skyBitmap;
    private static bool _skyPreDarkened;

    /// <summary>The tiling brush for this frame, or null when no sky art is supplied.</summary>
    private static ImageBrush? _skyArt;
    private static double _skyTwist;

    /// <summary>Point the sky brush at where the course has carried the camera.
    ///
    /// <para>The brush is kept and its viewport moved rather than rebuilt: it is one object per bitmap, not
    /// one per frame. ⚠ It is therefore not frozen — UI thread only, which is where every renderer here
    /// runs.</para></summary>
    private static void PrepareSky(Internode g, double S)
    {
        _skyArt = null;
        var bmp = ArcadeSprites.Get(ArcadeSprites.Slot.InternodeSky);
        if (bmp is null) return;
        if (!ReferenceEquals(bmp, _skyBitmap))
        {
            _skyBitmap = bmp;
            // The darkening is baked into the tile once here (the SpriteShadowBake shape) instead of a
            // full-disc black wash painted over the tiled art on every frame. The supplied art is untouched;
            // only the copy the brush tiles is darker. A failed bake falls back to the live wash.
            _skyPreDarkened = false;
            BitmapSource tileSource = bmp;
            try
            {
                var dv = new DrawingVisual();
                var full = new Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight);
                using (var g2 = dv.RenderOpen())
                {
                    g2.DrawImage(bmp, full);
                    g2.DrawRectangle(SkyArtDarken, null, full);
                }
                var rtb = new RenderTargetBitmap(bmp.PixelWidth, bmp.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                tileSource = rtb;
                _skyPreDarkened = true;
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Arcade] sky darken bake failed: {ex.Message}"); }
            _skyBrush = new ImageBrush(tileSource)
            {
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill,
            };
        }
        if (_skyBrush is null) return;
        double span = Math.Max(8, S * SkyTileSpan);
        // ⚠ The two axes take opposite signs, and it is not a typo. X is negated because a course bending
        // right carries the world left past the camera; Y is not, because a course heading down should carry
        // the sky down out of view with it — negating both would leave the vertical reading backwards, the
        // sky rising as the track fell.
        _skyBrush.Viewport = new Rect(-g.CameraTurnX * S * SkyPanGain, g.CameraTurnY * S * SkyPanGain, span, span);
        _skyArt = _skyBrush;
        _skyTwist = g.CameraTwist;
    }

    /// <summary>The same colour at <paramref name="alpha"/>. Local to the sky because it is the one place a
    /// palette colour has to become a wash at draw time — the depth of the wash depends on whether there is
    /// art under it, which the palette cannot know.</summary>
    private static Color Fade(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(255 * alpha, 0, 255), c.R, c.G, c.B);

    /// <summary>The sky gradient's radius, in field radii.</summary>
    private const double SkyGradientRadius = 1.05;
    private static readonly GradientStopCollection SkyStopsPlain = SkyStops(over: false);
    private static readonly GradientStopCollection SkyStopsOverArt = SkyStops(over: true);

    /// <summary>The sky's own gradient — black core, <c>SkyDeep</c>, <c>SkyEdge</c> — with the track's
    /// <see cref="DistanceBrightness"/> folded into it, so the sky darkens toward the vanishing point with the
    /// track in the same one fill rather than under a second full-disc wash. Stops at every knot of the
    /// darkening. Over art the result is solved for, not multiplied: a stop of colour C at alpha A over the art
    /// must leave b·(A·C + (1 − A)·art), which is alpha 1 − b(1 − A) and colour b·A·C over that alpha.</summary>
    private static GradientStopCollection SkyStops(bool over)
    {
        (double P, Color C)[] basis =
        [
            (0.00, Colors.Black),
            (0.10, Colors.Black),
            (0.45, over ? Fade(InternodePalette.SkyDeep, 0.55) : InternodePalette.SkyDeep),
            (1.00, over ? Fade(InternodePalette.SkyEdge, 0.00) : InternodePalette.SkyEdge),
        ];
        var at = new SortedSet<double>(basis.Select(s => s.P));
        foreach (double u in DarkU) if (u / SkyGradientRadius < 1) at.Add(u / SkyGradientRadius);
        var stops = new GradientStopCollection();
        foreach (double p in at)
        {
            int k = 1;
            while (k < basis.Length - 1 && basis[k].P < p) k++;
            var (p0, c0) = basis[k - 1];
            var (p1, c1) = basis[k];
            double t = p1 - p0 <= 1e-9 ? 1 : Math.Clamp((p - p0) / (p1 - p0), 0, 1);
            double A = (c0.A + (c1.A - c0.A) * t) / 255.0;
            double r = c0.R + (c1.R - c0.R) * t, g = c0.G + (c1.G - c0.G) * t, bl = c0.B + (c1.B - c0.B) * t;
            double b = DistanceBrightness(p * SkyGradientRadius);
            double a2 = 1 - b * (1 - A);
            double k2 = a2 <= 1e-6 ? 0 : b * A / a2;
            stops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(255 * a2), (byte)Math.Round(r * k2),
                                                      (byte)Math.Round(g * k2), (byte)Math.Round(bl * k2)), p));
        }
        stops.Freeze();
        return stops;
    }

    /// <summary>How much the supplied sky art is darkened before the gradient goes over it: black at this alpha.</summary>
    private const double SkyArtDarkenAlpha = 0.40;
    private static readonly Brush SkyArtDarken = Frozen(new SolidColorBrush(Color.FromArgb((byte)(255 * SkyArtDarkenAlpha), 0, 0, 0)));

    private static void DrawSky(DrawingContext dc, Point c, double S)
    {
        // Black where the pipe vanishes and the sky's own colour out at the disc's edge, centred on the far station,
        // so the near-black far end of the pipe reads as depth continuous with the sky, never as a disc hung on it.
        //
        // ⚠ With art the outer stops must fade to transparent. This gradient is painted over the tiled sky,
        // and while its colours were opaque it covered every pixel of it — the art was loaded, tiled and
        // panned, and then buried, so the sky simply never appeared. Only the black core is load-bearing
        // (it is what sinks the vanishing point); everything outward of it is a wash, and a wash has to let
        // what it washes through. Without art the same gradient is the sky and stays fully opaque.
        var far = StationCentre(c, S, _stations - 1);
        bool over = _skyArt is not null;
        var sky = new RadialGradientBrush(over ? SkyStopsOverArt : SkyStopsPlain)
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = far, GradientOrigin = far, RadiusX = S * SkyGradientRadius, RadiusY = S * SkyGradientRadius,
        };
        sky.Freeze();

        // Replacement art, tiled and carried by the course's own turn. Drawn under the gradient rather than
        // instead of it: the black centre is what keeps the vanishing point reading as depth, so the art is
        // laid down first and the gradient still sinks the far end of the pipe into it.
        if (_skyArt is { } tile)
        {
            dc.PushClip(new EllipseGeometry(c, S * 1.02, S * 1.02));
            dc.PushTransform(new RotateTransform(_skyTwist * 180 / Math.PI, c.X, c.Y));
            // Oversized well past the disc: the clip is a circle, the paint is rotating inside it, and a box
            // sized to the disc would swing its own corners into view.
            double reach = S * 1.9;
            var box = new Rect(c.X - reach, c.Y - reach, reach * 2, reach * 2);
            dc.DrawRectangle(tile, null, box);
            // The art is laid down darker than supplied, under the gradient, so the track and its
            // sprites stand off the sky. Normally baked into the tile in PrepareSky; the live wash is the fallback.
            if (!_skyPreDarkened) dc.DrawRectangle(SkyArtDarken, null, box);
            dc.Pop();
            dc.Pop();
        }

        dc.DrawEllipse(sky, null, c, S * 1.02, S * 1.02);
        // ⚠ Stars only when there is no sky art. They are a stand-in for a sky, not a layer over one.
        for (int k = 0; _skyArt is null && k < 56; k++)
        {
            double r = Math.Sqrt(Hash01(k * 3 + 1)) * S * 0.98;
            double a = Hash01(k * 3 + 2) * Math.PI * 2;
            double size = Math.Max(0.6, (0.5 + Hash01(k * 3 + 3)) * S * 0.0045);
            var p = Polar(c, r, a);
            // Stars are sky, so they fade with it toward the vanishing point.
            double fade = DistanceBrightness(Math.Sqrt((p.X - far.X) * (p.X - far.X) + (p.Y - far.Y) * (p.Y - far.Y)) / S);
            if (fade <= 0.02) continue;
            dc.DrawEllipse(InternodePalette.Faded(Hash01(k * 3 + 3) > 0.62 ? InternodePalette.StarBright : InternodePalette.Star, fade),
                           null, p, size, size);
        }
    }

    /// <summary>Each gap's drawn span, section seconds, parallel to <see cref="Internode.Gaps"/>: the span the fill
    /// cuts and the lips run over.
    ///
    /// <para>⚠ A gap's own end is kept exactly where another gap starts or ends at the same depth — a swept plank's
    /// chords, a strip crossing 12 o'clock, a hole butted to a ribbon — because there the break in the floor
    /// carries on and only its shape changes. Only a true end, where the floor comes back, is carried out to the
    /// station boundary it falls between, so a hole still starts and stops on a checker edge. Over that carried
    /// stretch the edges hold where the hole left them (<see cref="InternodeGap.MinAt"/> clamps); continued along
    /// their slope instead, a chord's edge ran into its neighbour's stretch, and the fill and the lips each drew it.</para>
    ///
    /// <para>Stations sit at section depth n·<see cref="BandSeconds"/>, so the table depends on the section and the
    /// band length only, and is rebuilt when either changes.</para></summary>
    private static void BuildDrawnSpans(Internode g)
    {
        var gaps = g.Gaps;
        double band = BandSeconds;
        if (ReferenceEquals(gaps, _drawnFor) && band == _drawnBand) return;
        _drawnFor = gaps;
        _drawnBand = band;
        if (_drawn0.Length < gaps.Count)
        {
            _drawn0 = new double[gaps.Count];
            _drawn1 = new double[gaps.Count];
        }
        const double touch = 1e-7;
        for (int k = 0; k < gaps.Count; k++)
        {
            double s = gaps[k].Z0, e = gaps[k].Z0 + gaps[k].Seconds;
            bool before = false, after = false;
            for (int m = 0; m < gaps.Count && !(before && after); m++)
            {
                if (m == k) continue;
                double ms = gaps[m].Z0, me = gaps[m].Z0 + gaps[m].Seconds;
                before |= Math.Abs(me - s) < touch || Math.Abs(ms - s) < touch && me > s;
                after |= Math.Abs(ms - e) < touch || Math.Abs(me - e) < touch && ms < e;
            }
            _drawn0[k] = before ? s : Math.Floor(s / band + 1e-9) * band;
            _drawn1[k] = after ? e : Math.Ceiling(e / band - 1e-9) * band;
        }
        if (_reach1.Length < gaps.Count) _reach1 = new double[gaps.Count];
        _gapsSorted = true;
        for (int k = 0; k < gaps.Count; k++)
        {
            _reach1[k] = k == 0 ? _drawn1[k] : Math.Max(_reach1[k - 1], _drawn1[k]);
            if (k > 0 && gaps[k].Z0 < gaps[k - 1].Z0) _gapsSorted = false;
        }
    }

    private static IReadOnlyList<InternodeGap>? _drawnFor;
    private static double _drawnBand;
    private static double[] _drawn0 = [], _drawn1 = [];
    /// <summary>The farthest drawn end among gaps 0..k: non-decreasing, so it can be searched.</summary>
    private static double[] _reach1 = [];
    private static bool _gapsSorted;

    /// <summary>The index range [Lo, Hi) of <see cref="Internode.Gaps"/> that can have a drawn span touching section
    /// depths [<paramref name="z0"/>, <paramref name="z1"/>]. Conservative — callers still test each gap — and
    /// the whole list if the section's gaps are not sorted by start, which the sequencer guarantees.
    ///
    /// <para>⚠ Every per-frame reader of the gap list goes through this. They run per station, per lip piece and
    /// per probe, and a full scan in each was the renderer's worst scaling with a long section.</para></summary>
    private static (int Lo, int Hi) GapWindow(IReadOnlyList<InternodeGap> gaps, double z0, double z1)
    {
        int n = gaps.Count;
        if (!_gapsSorted || n == 0) return (0, n);
        // A drawn start is never more than one band before the gap's own start.
        double slack = _drawnBand + 1e-6;
        int a = 0, b = n;
        while (a < b) { int m = (a + b) >> 1; if (_reach1[m] < z0) a = m + 1; else b = m; }
        int lo = a;
        a = lo; b = n;
        while (a < b) { int m = (a + b) >> 1; if (gaps[m].Z0 - slack <= z1) a = m + 1; else b = m; }
        return (lo, a);
    }

    /// <summary>Every break in the floor whose drawn span overlaps the view-depth span
    /// [<paramref name="zvFar"/>, <paramref name="zvNear"/>], written into <see cref="_holes"/> (drawn span in
    /// <see cref="_holeD0"/>/<see cref="_holeD1"/>) and returned as a count.
    ///
    /// <para>⚠ All of them, not the first. Two gaps sharing a z-span is how a plank is built — floor left
    /// between them — and the physics has always dropped the runner through any gap covering its angle
    /// (<c>Internode.InGap</c> loops the list). A renderer that painted only the first hole would leave
    /// the second one invisible and lethal, which is why this returns a set.</para></summary>
    private static int GapsBetween(Internode g, double zvFar, double zvNear)
    {
        double lo = g.Z + (zvNear - ZLead), hi = g.Z + (zvFar - ZLead);
        var gaps = g.Gaps;
        int n = 0;
        var (w0, w1) = GapWindow(gaps, lo, hi);
        for (int k = w0; k < w1; k++)
        {
            if (!(lo < _drawn1[k] && _drawn0[k] < hi)) continue;
            if (n >= MaxHolesPerRing)
            {
                // ⚠ Overflow is not survivable by dropping a hole: painting the band with one missing draws
                // floor over something the runner still falls through, which is the one failure here that
                // nobody sees until they fall. So the band is not painted at all — a missing annulus at the
                // far plane is a visual glitch, and a lie about where the floor is is not. Loud in Debug
                // too: reaching this means the bound and the course generator have drifted, and
                // InternodeProbe asserts against the worst section it can build precisely so they cannot.
                System.Diagnostics.Debug.Fail(
                    $"Internode: more than {MaxHolesPerRing} holes on one ring; the band will be skipped.");
                return -1;
            }
            _holes[n] = gaps[k];
            _holeD0[n] = _drawn0[k];
            _holeD1[n] = _drawn1[k];
            n++;
        }
        return n;
    }

    /// <summary>One ring: the surface between boundary stations <paramref name="i"/> (near) and
    /// <paramref name="far"/>, cut into checker sectors. The two arcs may have different centres (a turn), twists
    /// (a corkscrew) and rims (the opening), so a sector's side edges skew rather than staying radial. A hole
    /// leaves its arc unpainted, cut exactly at its edges so the checker stays aligned; a swept hole is cut at its
    /// own angle on each arc, so its edge runs skew across the band with its lip.
    ///
    /// <para>⚠ A band that a hole's drawn span starts, ends or bends inside is painted as parts split at those
    /// depths, every part with its own exact edges. One straight cut from the near arc to the far one can only be
    /// right when every hole in the band keeps one slope across it; a plank's chords hand over mid-band, and
    /// cutting both chords across the whole band painted their envelope — floor taken where there is floor, and
    /// wedges left where there is none. The parts share the band's colour and only its outer arcs are stroked,
    /// so the checker reads unchanged.</para></summary>
    private static void DrawRing(DrawingContext dc, Point c, double S, Internode g, int i, int far, int holes)
    {
        double zA = _zv[i] - ZLead + g.Z, zB = _zv[far] - ZLead + g.Z;
        double ra = _rad[i] * S, rb = _rad[far] * S;
        // One tone per ring, at the brightness halfway across it on screen.
        double bright = DistanceBrightness((_rad[i] + _rad[far]) / 2);
        int ramp = Math.Clamp((int)Math.Round((1 - bright) * (InternodePalette.RampSteps - 1)), 0, InternodePalette.RampSteps - 1);
        int n = _nFirst + i;
        // ⚠ The edge is dropped once a ring is too shallow to show it. This is the hottest path in the game —
        // a few hundred sectors a frame — and a 6% white hairline on a band a couple of pixels deep costs a
        // full stroke pass to render nothing a player can see. Depth is what decides it, so the near field
        // (where the grid actually reads) keeps every edge.
        //
        // ⚠ One weight at every depth. Do not thin the far edges by depth: that costs the tiling that tells
        // the eye how fast the floor is moving, which is most of what the checker is for. The rim and the gap
        // lips take the weighting instead: they are single lines, and losing a little of one in the distance
        // costs nothing the grid's texture was carrying. Its brightness does follow the distance darkening, at
        // the ring's own step, or the grid stays grey where the floor under it has gone to black.
        var pen = (ra - rb) >= CheckEdgeMinDepthPx ? InternodePalette.CheckEdgeAt(ramp) : null;

        // Split depths: every drawn-span and real end inside the band, where some hole's clamped edge appears,
        // disappears or bends. A band too shallow to show a part is painted whole.
        int splits = 0;
        if (ra - rb >= RingSplitMinDepthPx)
        {
            void Add(double z) { if (z > zA + 1e-9 && z < zB - 1e-9 && splits < _ringSplit.Length) _ringSplit[splits++] = z; }
            for (int h = 0; h < holes; h++)
            {
                Add(_holeD0[h]);
                Add(_holeD1[h]);
                Add(_holes[h].Z0);
                Add(_holes[h].Z0 + _holes[h].Seconds);
            }
            Array.Sort(_ringSplit, 0, splits);
            // ⚠ And wherever a hole's edge crosses a checker sector line. A sector is painted as one quad from
            // arc to arc, so an edge that enters or leaves it mid-part has its cut run straight along the sector
            // line to the far arc instead of following the edge: a thin wedge of floor painted over the hole,
            // hugging the checker line, worst in the deep near band. Inside each part every edge is linear, so
            // each crossing is found from the part's two ends.
            int bounded = splits;
            for (int p = 0; p <= bounded; p++)
            {
                double z0 = p == 0 ? zA : _ringSplit[p - 1], z1 = p < bounded ? _ringSplit[p] : zB;
                if (z1 - z0 < 1e-9) continue;
                double rim0 = g.RimAt(z0 - g.Z), rim1 = g.RimAt(z1 - g.Z);
                if (rim0 < 1e-6 || rim1 < 1e-6) continue;
                for (int h = 0; h < holes; h++)
                {
                    if (!(_holeD0[h] < z1 - 1e-9 && _holeD1[h] > z0 + 1e-9)) continue;
                    var (lo0, hi0) = _holes[h].RadAt(z0);
                    var (lo1, hi1) = _holes[h].RadAt(z1);
                    for (int edge = 0; edge < 2; edge++)
                    {
                        double u0 = SectorsPerRing * (rim0 - (edge == 0 ? lo0 : hi0)) / (2 * rim0);
                        double u1 = SectorsPerRing * (rim1 - (edge == 0 ? lo1 : hi1)) / (2 * rim1);
                        if (Math.Abs(u1 - u0) < 1e-9) continue;
                        for (double j = Math.Floor(Math.Min(u0, u1)) + 1; j < Math.Max(u0, u1) && j < SectorsPerRing; j++)
                            if (j > 0) Add(z0 + (z1 - z0) * (j - u0) / (u1 - u0));
                    }
                }
            }
            if (splits > bounded) Array.Sort(_ringSplit, 0, splits);
        }
        double twist0 = g.TwistAt(0);
        double zNear = zA;
        for (int p = 0; p <= splits; p++)
        {
            double zFar = p < splits ? _ringSplit[p] : zB;
            if (zFar - zNear < 1e-9 && p < splits) continue;
            DrawRingPart(dc, c, S, g, twist0, zNear, zFar, zNear == zA ? i : -1, p == splits ? far : -1,
                         holes, n, ramp, pen);
            zNear = zFar;
        }
    }

    /// <summary>A band too shallow to split into parts, pixels: under it the parts' differences are sub-pixel.</summary>
    private const double RingSplitMinDepthPx = 3.0;
    private const double RingSplitOverlapPx = 1.0;

    /// <summary>One part of a ring, section depths <paramref name="zA"/> (near) to <paramref name="zB"/> (far). A
    /// station index (≥ 0) marks an end that is the band's own boundary: its geometry is the station's and its arc
    /// is stroked; −1 is a split inside the band.</summary>
    private static void DrawRingPart(DrawingContext dc, Point c, double S, Internode g, double twist0, double zA, double zB,
                                     int stationA, int stationB, int holes, int n, int ramp, Pen? pen)
    {
        double zvA = zA - g.Z + ZLead, zvB = zB - g.Z + ZLead;
        Point ca = stationA >= 0 ? StationCentre(c, S, stationA) : Centre(c, S, zvA);
        Point cb = stationB >= 0 ? StationCentre(c, S, stationB) : Centre(c, S, zvB);
        double ra = (stationA >= 0 ? _rad[stationA] : R(zvA)) * S, rb = (stationB >= 0 ? _rad[stationB] : R(zvB)) * S;
        // ⚠ A part ending at a split reaches a pixel under the next one, which is painted after it in the same
        // colour. Two anti-aliased fills meeting edge to edge each cover half of the pixels on the seam, and the
        // sky shows through as a dark ring inside the floor.
        if (stationB < 0) rb = Math.Max(0, rb - RingSplitOverlapPx);
        double rimA = stationA >= 0 ? _rim[stationA] : g.RimAt(zvA - ZLead);
        double rimB = stationB >= 0 ? _rim[stationB] : g.RimAt(zvB - ZLead);
        double twA = stationA >= 0 ? _tw[stationA] : g.TwistAt(zvA - ZLead) - twist0;
        double twB = stationB >= 0 ? _tw[stationB] : g.TwistAt(zvB - ZLead) - twist0;
        double sa = Math.PI - rimA + twA, spanA = 2 * rimA;
        double sb = Math.PI - rimB + twB, spanB = 2 * rimB;
        // Each hole in the ring's own parameter t ∈ [0, 1] along the surface (t = 0 at pipe angle +rim, 1 at
        // −rim), each arc against its own rim, so it lands on the hole's angle on both arcs whatever their twist
        // and opening. Sorted and merged, so the painter below can walk them once.
        int cuts = 0;
        if (rimA > 1e-6 && rimB > 1e-6)
        {
            for (int h = 0; h < holes; h++)
            {
                if (!(_holeD0[h] < zB - 1e-9 && _holeD1[h] > zA + 1e-9)) continue;
                var (loA, hiA) = _holes[h].RadAt(zA);
                var (loB, hiB) = _holes[h].RadAt(zB);
                double t0 = Math.Clamp((rimA - hiA) / (2 * rimA), 0, 1), t1 = Math.Clamp((rimA - loA) / (2 * rimA), 0, 1);
                double s0 = Math.Clamp((rimB - hiB) / (2 * rimB), 0, 1), s1 = Math.Clamp((rimB - loB) / (2 * rimB), 0, 1);
                if (t1 - t0 < 1e-6 && s1 - s0 < 1e-6) continue;
                _holeT0[cuts] = t0; _holeT1[cuts] = t1;
                _holeS0[cuts] = s0; _holeS1[cuts] = s1;
                cuts++;
            }
            // Insertion sort by near-arc start, then merge: a handful of entries, and it keeps the walk below a
            // single pass rather than an interval test per sector per hole. Two holes that overlap on either arc
            // merge on both: the union may then take a sliver of floor at the other arc, which is the safe
            // direction (floor painted over a hole is the lie this code exists to avoid). ⚠ Holes that only
            // touch on one arc do not merge: that is a wedge of floor closing to a point — two strips meeting,
            // a fork parting — and merging would delete it.
            for (int a = 1; a < cuts; a++)
                for (int b = a; b > 0 && _holeT0[b] < _holeT0[b - 1]; b--)
                {
                    (_holeT0[b - 1], _holeT0[b]) = (_holeT0[b], _holeT0[b - 1]);
                    (_holeT1[b - 1], _holeT1[b]) = (_holeT1[b], _holeT1[b - 1]);
                    (_holeS0[b - 1], _holeS0[b]) = (_holeS0[b], _holeS0[b - 1]);
                    (_holeS1[b - 1], _holeS1[b]) = (_holeS1[b], _holeS1[b - 1]);
                }
            int keep = 0;
            for (int a = 0; a < cuts; a++)
            {
                bool merge = keep > 0 && (_holeT0[a] < _holeT1[keep - 1] - 1e-9 || _holeS0[a] < _holeS1[keep - 1] - 1e-9
                                          || (_holeT0[a] <= _holeT1[keep - 1] + 1e-9 && _holeS0[a] <= _holeS1[keep - 1] + 1e-9));
                if (merge)
                {
                    _holeT1[keep - 1] = Math.Max(_holeT1[keep - 1], _holeT1[a]);
                    _holeS0[keep - 1] = Math.Min(_holeS0[keep - 1], _holeS0[a]);
                    _holeS1[keep - 1] = Math.Max(_holeS1[keep - 1], _holeS1[a]);
                }
                else
                {
                    _holeT0[keep] = _holeT0[a]; _holeT1[keep] = _holeT1[a];
                    _holeS0[keep] = _holeS0[a]; _holeS1[keep] = _holeS1[a];
                    keep++;
                }
            }
            cuts = keep;
        }
        bool strokeNear = stationA >= 0, strokeFar = stationB >= 0;
        // During a theme fade the previous theme's fill goes under and the new one over it at the blend, so
        // every ring changes colour together over the second rather than the pipe snapping.
        bool fading = _themeBlend < 1;
        // ⚠ One geometry per checker colour for the whole part, each piece a figure in it, filled and stroked
        // in one call. This is the hottest path in the game; a geometry and a draw per sector per piece was up
        // to sixteen of each per ring. Nonzero, so pieces that touch (every one is wound the same way) union
        // rather than cancelling as EvenOdd would.
        StreamGeometry? geoA = null, geoB = null;
        StreamGeometryContext? ctxA = null, ctxB = null;
        // [t0, t1] on the near arc and [s0, s1] on the far one: equal for a checker sector, skew beside a swept hole.
        void Piece(bool b, double t0, double t1, double s0, double s1)
        {
            if (t1 - t0 < 1e-6 && s1 - s0 < 1e-6) return;
            t1 = Math.Max(t0, t1); s1 = Math.Max(s0, s1);
            var ctx = b ? ctxB ??= (geoB = new StreamGeometry { FillRule = FillRule.Nonzero }).Open()
                        : ctxA ??= (geoA = new StreamGeometry { FillRule = FillRule.Nonzero }).Open();
            AddSector(ctx, ca, ra, sa + spanA * t0, sa + spanA * t1, cb, rb, sb + spanB * s0, sb + spanB * s1, strokeNear, strokeFar);
        }
        // [t0, t1] minus every hole: paint the surviving runs between them. With no holes this is one figure.
        void Span(bool b, double t0, double t1)
        {
            double at = t0, atS = t0;
            for (int h = 0; h < cuts && (at < t1 || atS < t1); h++)
            {
                if ((_holeT1[h] <= at && _holeS1[h] <= atS) || (_holeT0[h] >= t1 && _holeS0[h] >= t1)) continue;
                Piece(b, at, Math.Min(t1, _holeT0[h]), atS, Math.Min(t1, _holeS0[h]));
                at = Math.Max(at, _holeT1[h]);
                atS = Math.Max(atS, _holeS1[h]);
            }
            Piece(b, at, t1, atS, t1);
        }
        if (rb * spanB / SectorsPerRing < 2) Span(false, 0, 1);
        else
            for (int j = 0; j < SectorsPerRing; j++)
                Span(((n + j) & 1) != 0, (double)j / SectorsPerRing, (double)(j + 1) / SectorsPerRing);
        // During a theme fade the previous theme's fill goes under and the new one over it at the blend, so
        // every ring changes colour together over the second rather than the pipe snapping.
        // ⚠ A ring whose own depth spans more than one ramp step is filled with the darkening as a gradient
        // along it, not one tone. The near rings are a third of the screen deep, so a single tone per ring held
        // the whole near field at full brightness where the distance darkening is still rising.
        Brush Tone(Brush[][] ramps, int theme)
        {
            double uA = ra / S, uB = rb / S;
            double bA = DistanceBrightness(uA), bB = DistanceBrightness(uB);
            if ((bA - bB) * (InternodePalette.RampSteps - 1) < 1 || ra < 1) return ramps[theme][ramp];
            var mouth = ((SolidColorBrush)ramps[theme][0]).Color;
            var stops = new GradientStopCollection();
            void Stop(double u)
            {
                double b = DistanceBrightness(u);
                stops.Add(new GradientStop(Color.FromRgb((byte)Math.Round(mouth.R * b), (byte)Math.Round(mouth.G * b),
                                                         (byte)Math.Round(mouth.B * b)), u * S / ra));
            }
            Stop(uB);
            foreach (double u in DarkU) if (u > uB && u < uA) Stop(u);
            Stop(uA);
            var brush = new RadialGradientBrush(stops)
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = ca, GradientOrigin = ca, RadiusX = ra, RadiusY = ra,
            };
            brush.Freeze();
            return brush;
        }
        void Paint(StreamGeometry? geo, StreamGeometryContext? ctx, Brush[][] ramps)
        {
            if (geo is null) return;
            ctx!.Close();
            geo.Freeze();
            if (!fading) { dc.DrawGeometry(Tone(ramps, _theme), pen, geo); return; }
            dc.DrawGeometry(Tone(ramps, _themeFrom), pen, geo);
            dc.PushOpacity(_themeBlend);
            dc.DrawGeometry(Tone(ramps, _theme), null, geo);
            dc.Pop();
        }
        Paint(geoA, ctxA, InternodePalette.CheckA);
        Paint(geoB, ctxB, InternodePalette.CheckB);
    }

    /// <summary>The lips of every break in the floor, drawn the way the split edges are: the near and far edges over
    /// the hole's arc, and for a partial gap the side edges running the hole's length where floor remains beside it.
    /// Without them a hole reads as a darker band rather than an edge to clear.
    ///
    /// <para>⚠ Every lip runs over its gap's drawn span (<see cref="BuildDrawnSpans"/>) and nowhere else, reading
    /// the same clamped edges the fill is cut with, so the line and the floor it edges cannot disagree. A lip
    /// continued past its own gap along its slope, into a neighbour piece's stretch, is the doubled line and the
    /// stray wedge of floor this layout exists to prevent.</para>
    ///
    /// <para>⚠ A lip is drawn only where there is floor on the other side of it. Gaps are unioned, so one
    /// gap's boundary is not the floor's boundary wherever another gap continues past it: the full-width
    /// void between two planks has its own near and far arcs, but the floor there is only the plank's
    /// width, and an arc drawn across the whole rim reads as a solid edge over sky and gets the runner
    /// killed on a mis-read. The same holds for a side edge with another hole beside it. Each lip is
    /// therefore probed a hair past its own gap and cut wherever the union says there is no floor.</para></summary>
    private static void DrawGapEdges(DrawingContext dc, Point c, double S, Internode g, double twist0, double zLo, double zHi)
    {
        // ⚠ Weighted at each lip's own depth, not once for the gap: a break long enough to matter spans a lot
        // of depth, and one thickness along it is the flat look this replaced.
        double baseWidth = S >= 200 ? SplitEdgeWidth : SplitEdgeFineWidth;
        Pen LipPen(double zv) => InternodePalette.SplitEdgeAt(baseWidth, EdgeWeight(zv), EdgeFade(zv));
        var gaps = g.Gaps;
        var (w0, w1) = GapWindow(gaps, zLo - ZLead + g.Z, zHi - ZLead + g.Z);
        for (int k = w0; k < w1; k++)
        {
            var gap = gaps[k];
            double zvStart = _drawn0[k] - g.Z + ZLead, zvEnd = _drawn1[k] - g.Z + ZLead;
            if (zvEnd < zLo || zvStart >= zHi) continue;
            // Near and far edges: an arc over the hole's angles at each end of its drawn span, minus every angle
            // where another gap is still open just beyond that end.
            for (int end = 0; end < 2; end++)
            {
                double zv = end == 0 ? zvStart : zvEnd;
                if (zv <= 0 || zv > ZFar || zv < zLo || zv >= zHi) continue;
                double rim = g.RimAt(zv - ZLead);
                double zEdge = end == 0 ? _drawn0[k] : _drawn1[k];
                var (gapLo, gapHi) = gap.RadAt(zEdge);
                double lo = Math.Max(-rim, gapLo), hi = Math.Min(rim, gapHi);
                if (hi <= lo) continue;
                double rel = g.TwistAt(zv - ZLead) - twist0;
                var centre = Centre(c, S, zv);
                double r = R(zv) * S;
                if (r < 2) continue;
                int cuts = GapCutsAt(g, k, zEdge, end == 0 ? -1 : 1, lo, hi);
                double at = lo;
                for (int h = 0; h <= cuts; h++)
                {
                    double to = h < cuts ? _lipT0[h] : hi;
                    // A sliver under a pixel is two ends of the same edge failing to meet in the last digit.
                    if ((to - at) * r > 0.5) DrawLipArc(dc, LipPen(zv), centre, r, Math.PI - to + rel, Math.PI - at + rel);
                    if (h < cuts) at = Math.Max(at, _lipT1[h]);
                }
            }
            // Side edges: where the hole stops short of a rim, a line down its length at that angle. Drawn only
            // for the stretch inside this interval, so it takes the same clips as the floor beside it, and only
            // along the stretches where the floor just outside that angle is really there.
            double a0 = Math.Max(zvStart, Math.Max(zLo, 1e-6)), a1 = Math.Min(zvEnd, Math.Min(zHi, ZFar));
            if (a1 - a0 < 1e-6) continue;
            for (int side = 0; side < 2; side++)
            {
                // A swept hole's edge is a function of depth; both ends are read to decide whether it is on the pipe.
                double edgeStart = (side == 0 ? gap.ThetaMinDeg : gap.ThetaMaxDeg) * InternodePhysics.Deg;
                double edgeEnd = (side == 0 ? gap.MinEndDeg : gap.MaxEndDeg) * InternodePhysics.Deg;
                double rimHere = Math.Min(g.RimAt(a0 - ZLead), g.RimAt(a1 - ZLead));
                // ⚠ Only an open rim suppresses this lip. On a closed tube the surface wraps through 12
                // o'clock, so a hole reaching ±π has real floor on the far side of that line and needs its
                // edge drawn like any other — it was being dropped, leaving one side of the hole unlipped.
                // The pipe's own seam is the separate case and stays suppressed when closed: see
                // DrawSeamSegment, which is why both read the same epsilon.
                if (!ClosedRim(rimHere) && Math.Abs(edgeStart) >= rimHere - ClosedRimEpsilon
                                        && Math.Abs(edgeEnd) >= rimHere - ClosedRimEpsilon) continue;
                DrawSideEdge(dc, LipPen, c, S, g, twist0, k, side, a0, a1);
            }
        }
    }

    /// <summary>One side edge of hole <paramref name="self"/>, from view depth <paramref name="from"/> to
    /// <paramref name="to"/>, as a polyline cut into pieces over which the floor beside it is either wholly
    /// there or wholly gone, each piece drawn only if it is there.
    ///
    /// <para>⚠ A polyline through the station boundaries, never one straight chord. A constant-θ line down the
    /// pipe does not project to a straight screen line: the radius runs on the perspective curve, the centre
    /// drifts with the course's turn, and the angle itself rotates with its twist. A single segment cuts the
    /// corner off all three, worst where depth compresses fastest.</para>
    ///
    /// <para>The pieces break at every station, at every drawn-span and real end of any gap overlapping the run
    /// (where some hole's clamped edge bends or appears), and wherever another hole's edge crosses the probe line
    /// just outside this one. Between consecutive breaks everything is linear in depth, so one probe at the
    /// middle decides the piece.</para></summary>
    private static void DrawSideEdge(DrawingContext dc, Func<double, Pen> pen, Point c, double S, Internode g,
                                     double twist0, int self, int side, double from, double to)
    {
        var gaps = g.Gaps;
        var gap = gaps[self];
        Point At(double zv) =>
            Polar(Centre(c, S, zv), R(zv) * S, Math.PI - EdgeAt(gap, side, zv - ZLead + g.Z) + g.TwistAt(zv - ZLead) - twist0);

        double zFrom = from - ZLead + g.Z, zTo = to - ZLead + g.Z;
        int n = 0;
        void Add(double zv) { if (zv > from + 1e-9 && zv < to - 1e-9 && n < _sideZ.Length - 2) _sideZ[n++] = zv; }
        for (int i = 0; i < _stations; i++) Add(_zv[i]);
        int others = 0;
        var (w0, w1) = GapWindow(gaps, zFrom, zTo);
        for (int m = w0; m < w1; m++)
        {
            if (_drawn1[m] <= zFrom || _drawn0[m] >= zTo) continue;
            var o = gaps[m];
            Add(_drawn0[m] - g.Z + ZLead);
            Add(_drawn1[m] - g.Z + ZLead);
            Add(o.Z0 - g.Z + ZLead);
            Add(o.Z0 + o.Seconds - g.Z + ZLead);
            if (m != self && others < _sideOthers.Length) _sideOthers[others++] = m;
        }
        _sideZ[n++] = from;
        _sideZ[n++] = to;
        Array.Sort(_sideZ, 0, n);
        int pieces = n;
        // Crossings: inside each piece every edge is linear, so a sign change of (other edge − probe line) between
        // the piece's ends is exactly one crossing.
        for (int p = 0; p + 1 < pieces; p++)
        {
            double za = _sideZ[p] - ZLead + g.Z, zb = _sideZ[p + 1] - ZLead + g.Z;
            for (int q = 0; q < others; q++)
            {
                int m = _sideOthers[q];
                if (_drawn1[m] <= za || _drawn0[m] >= zb) continue;
                for (int edge = 0; edge < 2; edge++)
                {
                    double fa = EdgeAt(gaps[m], edge, za) - BesideAt(gap, side, za);
                    double fb = EdgeAt(gaps[m], edge, zb) - BesideAt(gap, side, zb);
                    if (fa * fb < 0 && n < _sideZ.Length) _sideZ[n++] = _sideZ[p] + (_sideZ[p + 1] - _sideZ[p]) * fa / (fa - fb);
                }
            }
        }
        if (n > pieces) Array.Sort(_sideZ, 0, n);

        for (int p = 0; p + 1 < n; p++)
        {
            double segStart = _sideZ[p], segEnd = _sideZ[p + 1];
            if (segEnd - segStart < 1e-9) continue;
            // The pen is taken per piece for the same reason the polyline is: one stroke down a long break
            // would be the flat look, and the near end is where the weight has to land.
            double midZ = (segStart + segEnd) / 2;
            double zMid = midZ - ZLead + g.Z;
            if (FloorDrawn(g, zMid, BesideAt(gap, side, zMid)))
                dc.DrawLine(pen(midZ), At(segStart), At(segEnd));
        }
    }

    /// <summary>How far past a gap's own boundary its lip probes for floor: far enough that a butt-joined
    /// neighbour (shared boundary to the last digit) counts as open, small enough that nothing a player
    /// could stand on is skipped over.</summary>
    private const double LipProbeSeconds = 1e-4;
    private const double LipProbeDeg = 1e-3;

    /// <summary>Is floor painted at pipe depth <paramref name="z"/> (section time) and angle
    /// <paramref name="thetaRad"/>? The union of every gap over its drawn span with its clamped edges — the
    /// fill's own reading, which is the physics' union (<c>Internode.InGap</c>) with the true ends of a hole
    /// carried out to the band they fall in.</summary>
    private static bool FloorDrawn(Internode g, double z, double thetaRad)
    {
        var gaps = g.Gaps;
        var (w0, w1) = GapWindow(gaps, z, z);
        for (int k = w0; k < w1; k++)
            if (_drawn0[k] <= z && z < _drawn1[k] && gaps[k].Covers(thetaRad, z)) return false;
        return true;
    }

    /// <summary>One side edge of a hole, radians, at section depth <paramref name="z"/>: side 0 is the low edge.
    /// Clamped to the hole's own span, so over the stretch a true end is carried to its band boundary the edge
    /// holds where the hole left it.</summary>
    private static double EdgeAt(in InternodeGap gap, int side, double z)
        => (side == 0 ? gap.MinAt(z) : gap.MaxAt(z)) * InternodePhysics.Deg;

    /// <summary>The angle a hair outside a hole's side edge at depth <paramref name="z"/>, wrapped into
    /// (−180°, 180°] so a hole reaching ±180° on a closed tube probes the floor on the far side of 12 o'clock
    /// rather than an angle no gap covers.</summary>
    private static double BesideAt(in InternodeGap gap, int side, double z)
    {
        double besideDeg = (side == 0 ? gap.MinAt(z) - LipProbeDeg : gap.MaxAt(z) + LipProbeDeg);
        if (besideDeg > 180) besideDeg -= 360;
        else if (besideDeg <= -180) besideDeg += 360;
        return besideDeg * InternodePhysics.Deg;
    }

    /// <summary>The angle runs, within [<paramref name="lo"/>, <paramref name="hi"/>] radians, that other gaps
    /// cover just beyond depth <paramref name="z"/> (on the <paramref name="dir"/> side: −1 nearer, +1 farther),
    /// read at <paramref name="z"/> itself so a butt-joined neighbour's matching end cancels this arc exactly —
    /// into <see cref="_lipT0"/>/<see cref="_lipT1"/>, sorted and merged, count returned. Past
    /// <see cref="MaxHolesPerRing"/> the rest are dropped, which can only leave an extra lip drawn, never floor
    /// painted where there is none.</summary>
    private static int GapCutsAt(Internode g, int self, double z, int dir, double lo, double hi)
    {
        var gaps = g.Gaps;
        double zProbe = z + dir * LipProbeSeconds;
        int n = 0;
        var (w0, w1) = GapWindow(gaps, zProbe, zProbe);
        for (int m = w0; m < w1; m++)
        {
            if (n >= MaxHolesPerRing) break;
            if (m == self || !(_drawn0[m] <= zProbe && zProbe < _drawn1[m])) continue;
            var (otherLo, otherHi) = gaps[m].RadAt(z);
            double t0 = Math.Max(lo, otherLo), t1 = Math.Min(hi, otherHi);
            if (t1 - t0 < 1e-9) continue;
            _lipT0[n] = t0;
            _lipT1[n] = t1;
            n++;
        }
        return SortMerge(n);
    }

    /// <summary>Sort the first <paramref name="n"/> runs in <see cref="_lipT0"/>/<see cref="_lipT1"/> by start and
    /// merge the overlaps; the surviving count is returned.</summary>
    private static int SortMerge(int n)
    {
        for (int a = 1; a < n; a++)
            for (int b = a; b > 0 && _lipT0[b] < _lipT0[b - 1]; b--)
            {
                (_lipT0[b - 1], _lipT0[b]) = (_lipT0[b], _lipT0[b - 1]);
                (_lipT1[b - 1], _lipT1[b]) = (_lipT1[b], _lipT1[b - 1]);
            }
        int keep = 0;
        for (int a = 0; a < n; a++)
            if (keep > 0 && _lipT0[a] <= _lipT1[keep - 1] + 1e-9)
                _lipT1[keep - 1] = Math.Max(_lipT1[keep - 1], _lipT1[a]);
            else
            {
                _lipT0[keep] = _lipT0[a];
                _lipT1[keep] = _lipT1[a];
                keep++;
            }
        return keep;
    }

    /// <summary>Scratch for the lip cuts.</summary>
    private static readonly double[] _lipT0 = new double[MaxHolesPerRing];
    private static readonly double[] _lipT1 = new double[MaxHolesPerRing];
    /// <summary>Scratch for one side edge's breaks (view depths) and the other gaps overlapping it. A run past
    /// either cap keeps its last pieces unsplit, which can only misplace where a lip starts or stops.</summary>
    private static readonly double[] _sideZ = new double[4 * MaxStations + 8 * MaxHolesPerRing];
    private static readonly int[] _sideOthers = new int[MaxHolesPerRing];

    private static void DrawLipArc(DrawingContext dc, Pen pen, Point centre, double r, double from, double to)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(Polar(centre, r, from), false, false);
            Arc(ctx, centre, r, from, to);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    /// <summary>How close to π a rim has to be to count as a closed tube — no opening, so no seam and no
    /// wall to end a hole against. ⚠ Shared by the two passes that draw a line near 12 o'clock
    /// (<see cref="DrawSeamSegment"/> and <see cref="DrawGapEdges"/>), because they have to agree about
    /// whether there is an opening there at all.</summary>
    private const double ClosedRimEpsilon = 1e-6;

    private static bool ClosedRim(double rim) => rim >= Math.PI - ClosedRimEpsilon;

    /// <summary>The two split edges across one ring, following the rim at each boundary. A side whose rim the
    /// <paramref name="hole"/> reaches has no floor there to edge, so its line is left out: the gap's own lips take over.</summary>
    private static void DrawSeamSegment(DrawingContext dc, Point c, double S, Internode g, int i, int far, int holes)
    {
        double zA = _zv[i] - ZLead + g.Z, zB = _zv[far] - ZLead + g.Z;
        // Weighted at this ring's own depth, so the seam thickens as it comes toward the runner rather than
        // drawing one thickness from the mouth to the far plane.
        double baseWidth = S >= 200 ? SplitEdgeWidth : SplitEdgeFineWidth;
        double zvMid = (_zv[i] + _zv[far]) / 2;
        var pen = InternodePalette.SplitEdgeAt(baseWidth, EdgeWeight(zvMid), EdgeFade(zvMid));
        Point ca = StationCentre(c, S, i), cb = StationCentre(c, S, far);
        double ra = _rad[i] * S, rb = _rad[far] * S;
        for (int side = -1; side <= 1; side += 2)
        {
            // No rim, no edge: a closed tube has no lip to highlight, and both seams would collapse into one line at 12 o'clock.
            // ⚠ This suppression is the pipe's own seam only. A floor gap that reaches 12 o'clock on a closed
            // tube still gets its lip — see DrawGapEdges.
            if (ClosedRim(_rim[i]) || ClosedRim(_rim[far])) continue;
            // ⚠ Any hole reaching this rim takes the seam over, not just the first: with a plank the near
            // wall's hole and the far wall's hole are different gaps, and testing one would leave the other
            // seam drawn straight across open sky.
            bool taken = false;
            for (int h = 0; h < holes && !taken; h++)
                taken = _holes[h].Covers(side * _rim[i], zA) && _holes[h].Covers(side * _rim[far], zB);
            if (taken) continue;
            dc.DrawLine(pen, Polar(cb, rb, Math.PI - side * _rim[far] + _tw[far]), Polar(ca, ra, Math.PI - side * _rim[i] + _tw[i]));
        }
    }

    /// <summary>The checker boundary at exactly 6 o'clock, θ = 0, drawn with the rim's pen rather than the
    /// checker hairline: the one line on the floor that always marks where gravity pulls, so under a
    /// twist, a roll or a full tube the player can still read "down" off the track itself. Ring by ring like
    /// the split edges, so it thins into the distance exactly as they do, and left out over a hole that covers
    /// the bottom, the way a split edge is left out where a hole reaches its rim.</summary>
    private static void DrawGravitySeam(DrawingContext dc, Point c, double S, Internode g, int i, int far, int holes)
    {
        double zA = _zv[i] - ZLead + g.Z, zB = _zv[far] - ZLead + g.Z;
        for (int h = 0; h < holes; h++) if (_holes[h].Covers(0, zA) || _holes[h].Covers(0, zB)) return;
        // Full rim width only at the mouth, where the ring meets the screen's edge; each segment's stroke is
        // scaled by its own ring radius against the mouth's, so the line tapers into the distance with the
        // pipe rather than staying one width all the way down. At half the rim line's opacity.
        double full = S >= 200 ? GravitySeamWidthPx : GravitySeamWidthFinePx;
        double width = Math.Max(0.5, full * ((_rad[i] + _rad[far]) / 2) / Rmouth);
        // Light-dark-light: the light stroke at full width, then a dark core over its middle third, leaving a
        // light line bounding the core on either side.
        double fade = EdgeFade((_zv[i] + _zv[far]) / 2);
        var pen = InternodePalette.Stroke(InternodePalette.Towards(GravitySeamColour, fade), width, GravitySeamAlpha);
        var core = InternodePalette.Stroke(InternodePalette.Towards(GravitySeamCoreColour, fade), width * GravitySeamCoreShare, GravitySeamCoreAlpha);
        Point ca = StationCentre(c, S, i), cb = StationCentre(c, S, far);
        Point pa = Polar(cb, _rad[far] * S, Math.PI + _tw[far]), pb = Polar(ca, _rad[i] * S, Math.PI + _tw[i]);
        dc.DrawLine(pen, pa, pb);
        dc.DrawLine(core, pa, pb);
    }

    /// <summary>Twice the rim line's widths (InternodePalette.SplitEdge / SplitEdgeFine), tapered by depth in
    /// DrawGravitySeam, in the checker hairline's own white a little above its opacity
    /// (InternodePalette.CheckEdge is 0x13 of 0xFF): the line is read by its width and stripe, not its brightness.</summary>
    private static readonly Color GravitySeamColour = Colors.White;
    private const double GravitySeamWidthPx = 8.4, GravitySeamWidthFinePx = 6.0, GravitySeamAlpha = 0.15;
    /// <summary>The seam's dark core: its share of the seam's width, colour and opacity.</summary>
    private static readonly Color GravitySeamCoreColour = Colors.Black;
    private const double GravitySeamCoreShare = 0.36, GravitySeamCoreAlpha = 0.55;

    /// <summary>Under the rings, inside every clip: a ring too thin to draw leaves fog, never sky.</summary>
    private static void DrawFog(DrawingContext dc, Point c, double S)
    {
        var far = StationCentre(c, S, _stations - 1);
        double fogR = _rad[_stations - 1] * S * 2.8;
        dc.DrawEllipse(InternodePalette.FogGlow, null, far, fogR, fogR);
    }

    /// <summary>A band between two arcs: the outer arc clockwise, across to the inner circle, the inner arc
    /// back. Sweeps are split under 90° per ArcTo so no segment is ambiguous. An arc that is not a band
    /// boundary (<paramref name="strokeNear"/>/<paramref name="strokeFar"/> false: the seam between two parts of
    /// one band) is filled but not stroked, so the checker hairline does not grow a ring there.</summary>
    private static StreamGeometry Sector(Point ca, double ra, double a0, double a1, Point cb, double rb, double b0, double b1,
                                         bool strokeNear = true, bool strokeFar = true)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
            AddSector(ctx, ca, ra, a0, a1, cb, rb, b0, b1, strokeNear, strokeFar);
        geo.Freeze();
        return geo;
    }

    /// <summary><see cref="Sector"/>'s figure, appended to a geometry being built, so a ring's pieces of one
    /// colour share one geometry and one draw.</summary>
    private static void AddSector(StreamGeometryContext ctx, Point ca, double ra, double a0, double a1, Point cb, double rb,
                                  double b0, double b1, bool strokeNear, bool strokeFar)
    {
        var start = Polar(ca, ra, a0);
        ctx.BeginFigure(start, true, true);
        Arc(ctx, ca, ra, a0, a1, strokeNear);
        ctx.LineTo(Polar(cb, rb, b1), true, true);
        Arc(ctx, cb, rb, b1, b0, strokeFar);
        ctx.LineTo(start, true, true);
    }

    private static void Arc(StreamGeometryContext ctx, Point c, double r, double from, double to, bool stroked = true)
    {
        double sweep = to - from;
        int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
        var dir = sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        for (int k = 1; k <= pieces; k++)
            ctx.ArcTo(Polar(c, r, from + sweep * k / pieces), new Size(r, r), 0, false, dir, stroked, true);
    }

    /// <summary>The split edges follow ±rim per station; where the tube closes the two meet at the top.</summary>

    // ── The gate ──────────────────────────────────────────────────────────────

    private static void DrawGate(DrawingContext dc, Point c, double S, Internode g, double twist0, double zLo, double zHi)
    {
        double zv = (g.CheckpointZ - g.Z) + ZLead;
        if (zv <= 0 || zv > ZFar || zv < zLo || zv >= zHi) return;
        double alpha = Math.Clamp((ZFar - zv) / FadeSeconds, 0, 1);
        if (alpha <= 0.01) return;
        double rel = g.TwistAt(zv - ZLead) - twist0;
        var centre = Centre(c, S, zv);
        double r = R(zv) * S;
        DrawGateRing(dc, centre, r, rel, g.RimAt(zv - ZLead), Math.Max(4.5, r * 0.15), alpha);
    }

    /// <summary>A stroke along the surface (±rim) with a post at each split edge.</summary>
    private static void DrawGateRing(DrawingContext dc, Point centre, double r, double relTwist, double rim, double width, double alpha)
    {
        double start = Math.PI - rim + relTwist, span = 2 * rim;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(Polar(centre, r * 0.86, start), false, false);
            ctx.LineTo(Polar(centre, r, start), true, true);
            Arc(ctx, centre, r, start, start + span);
            ctx.LineTo(Polar(centre, r * 0.86, start + span), true, true);
        }
        geo.Freeze();
        if (alpha < 0.995) dc.PushOpacity(alpha);
        dc.DrawGeometry(null, InternodePalette.Stroke(InternodePalette.GateGoldColour, width), geo);
        if (alpha < 0.995) dc.Pop();
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>The nearest untaken events ahead of the mouth, capped, in ascending depth.</summary>
    private static void CollectVisible(Internode g)
    {
        _visibleCount = 0;
        var events = g.Events;
        double zMouth = g.Z - ZLead;
        for (int i = 0; i < events.Count && _visibleCount < MaxSprites; i++)
        {
            var e = events[i];
            if (e.Z <= zMouth) continue;
            if (e.Z - zMouth > ZFar) break;
            if (e.Taken) continue;
            _visible[_visibleCount++] = i;
        }
    }

    /// <summary>Sprites whose view depth lies in [<paramref name="zLo"/>, <paramref name="zHi"/>), far to near.</summary>
    private static void DrawEvents(DrawingContext dc, Point c, double S, Internode g, double twist0, double zLo, double zHi)
    {
        var events = g.Events;
        for (int k = _visibleCount - 1; k >= 0; k--)
        {
            var e = events[_visible[k]];
            // A stable per-token offset into any animation cycle, from its index down the track: neighbours
            // land on different frames, so a row of tokens breathes instead of blinking as one.
            double phase = _visible[k] * 0.6180339887 % 1;
            double zv = (e.Z - g.Z) + ZLead;
            if (zv < zLo || zv >= zHi) continue;
            // Opaque at every depth; distance darkens a sprite, it never thins it. Measured on screen as the
            // share of the way from the vanishing point to the runner, brightness rising as 1 − e^(−4s) from
            // SpriteBrightFloor rather than from black — see that constant for why the floor exists.
            double screen = Math.Clamp((R(zv) - R(ZFar)) / Math.Max(1e-6, R(ZLead) - R(ZFar)), 0, 1);
            double curve = Math.Min(1, (1 - Math.Exp(-4 * screen)) / (1 - Math.Exp(-4 * BrightFullAtScreen)));
            double bright = SpriteBrightFloor + (1 - SpriteBrightFloor) * curve;
            double dim = 1 - bright;
            // The gate's own horizon curve, borrowed so a glow eases in at the far plane instead of popping.
            double horizon = Math.Clamp((ZFar - zv) / FadeSeconds, 0, 1);
            // Seconds of travel still to come. The glow ramps on this, not on screen position.
            double away = Math.Max(0, zv - ZLead);
            double rel = g.TwistAt(zv - ZLead) - twist0;
            var centre = Centre(c, S, zv);
            double ring = R(zv) * S;
            double a = Math.PI - e.Theta + rel;
            switch (e.Kind)
            {
                case InternodeEventKind.Token:
                {
                    double r = ring * OrbRadius * (e.Bonus ? BonusScale : 1);
                    // Sprites sit on the floor, not half through it: the centre is lifted by a share of the radius
                    // so the lower edge touches the surface, which also keeps them off the boundary circles.
                    var p = Polar(centre, ring - r * GroundLift, a);
                    // ⚠ A missed token gets none: it is spent, drained to grey, and a halo saying "collect
                    // this" round something that can no longer be collected is worse than no halo.
                    if (!e.Missed) Glow(dc, p, r, e.Bonus ? InternodePalette.BonusGlow : InternodePalette.TokenGlow,
                        TokenGlowSpread, away, horizon, TokenGlowFar, TokenGlowNear, GlowRampExponent);
                    Contact(dc, centre, ring, a, e.Theta, zv, r * 0.9);
                    if (e.Missed) DrawMissed(dc, p, r, dim, phase);
                    else { DrawOrb(dc, p, r, false, 0, e.Bonus, dim, phase); if (e.GoldCandidate && !e.Bonus) GoldRing(dc, p, r, dim); }
                    break;
                }
                case InternodeEventKind.ElevatedToken:
                {
                    var foot = Polar(centre, ring, a);
                    var p = Polar(centre, ring * (1 - e.Height), a);
                    if (!e.Missed) Glow(dc, p, ring * OrbRadius,
                        e.Bonus ? InternodePalette.BonusGlow : InternodePalette.TokenGlow,
                        TokenGlowSpread, away, horizon, TokenGlowFar, TokenGlowNear, GlowRampExponent);
                    Contact(dc, centre, ring, a, e.Theta, zv, ring * OrbRadius * 0.9 * (1 - e.Height));
                    if (ring * OrbRadius >= 3) dc.DrawLine(InternodePalette.Tether, foot, p);
                    if (e.Missed) DrawMissed(dc, p, ring * OrbRadius, dim, phase);
                    else { DrawOrb(dc, p, ring * OrbRadius, false, e.Height, e.Bonus, dim, phase); if (e.GoldCandidate && !e.Bonus) GoldRing(dc, p, ring * OrbRadius, dim); }
                    break;
                }
                case InternodeEventKind.Mine:
                {
                    // A hung mine sits at its own height on a tether, like a lifted orb; a floor mine rests
                    // its own radius off the surface.
                    bool hung = e.Height > 0;
                    var p = Polar(centre, ring * (1 - (hung ? e.Height : MineRadius * GroundLift)), a);
                    Glow(dc, p, ring * MineRadius, InternodePalette.MineGlow,
                        MineGlowSpread, away, horizon, MineGlowFar, MineGlowNear, MineGlowRampExponent);
                    Contact(dc, centre, ring, a, e.Theta, zv, ring * MineRadius * 0.9 * (hung ? 1 - e.Height : 1));
                    if (hung && ring * MineRadius >= 3) dc.DrawLine(InternodePalette.Tether, Polar(centre, ring, a), p);
                    DrawMine(dc, p, ring * MineRadius, a * 180 / Math.PI + 180, dim, phase);
                    break;
                }
            }
        }
    }

    /// <summary>The mark a body casts on the wall it is nearest: an ellipse squashed onto the surface at the body's
    /// own angle. Nothing in the gap across the top casts one, because up there is no wall to cast onto.</summary>
    private static void Contact(DrawingContext dc, Point centre, double ring, double screenAngle, double theta, double zv, double radius)
    {
        if (radius < 1.2) return;
        double rim = Interp(_rim, zv);
        if (Math.Abs(theta) > rim) return;
        var onWall = Polar(centre, ring, screenAngle);
        // Centred a touch outward of the surface line, so the blob shows under and around a sprite standing on it
        // rather than hiding entirely beneath the disc.
        dc.PushTransform(Placement(radius, screenAngle * 180 / Math.PI + 180, onWall));
        dc.DrawEllipse(InternodePalette.Shadow, null, new Point(0, 0.18), 1.15, 0.45);
        dc.Pop();
    }

    /// <summary>How far a glow reaches past its sprite, in sprite radii — per kind, because they are not
    /// asked to do the same job. A token's hugs it — a wide halo reads as a light source sitting on the
    /// track rather than as the orb being lit; a mine's stays the wider of the two, because a hazard wants
    /// its warning to arrive before its outline does.
    /// ⚠ The ordering is the part that matters, so keep the mine's above the token's if either moves
    /// again.</summary>
    private const double TokenGlowSpread = 1.7;
    private const double MineGlowSpread = 2.15;

    /// <summary>⚠ The whole reason a glow carries down the track: a screen floor, in pixels, under the halo's
    /// radius. A sprite most of the pipe away is well under a pixel and its own draw routine skips it; scaled
    /// off that radius the glow would vanish with it. Below this the halo stops shrinking and just sits there,
    /// so what the player sees at the far end is a coloured smudge that resolves into a token or a mine as it
    /// comes on.
    ///
    /// <para>⚠ It binds earlier for tokens now that their spread hugs (1.7): below about a 3 px sprite the
    /// floor takes over, so a distant token's halo is proportionally wider than a near one's. That is the
    /// floor doing its job, not the hug failing — and the opacity ramp has those distances at 0.1–0.25, so
    /// what it buys is a faint smudge rather than a wide bright ring.</para></summary>
    private const double GlowMinPixels = 5.0;

    /// <summary>Token halo opacity at the far end of the ramp and at the runner's feet. It brightens on
    /// approach: the far value is a hint that something collectable is coming, the near value says so plainly.
    ///
    /// <para>⚠ This is the reverse of the first cut, which lifted opacity with the sprite's own darkening and
    /// so sat at 0.98 four seconds out before *falling* to 0.55 on arrival — full blast at exactly the
    /// distance where a quiet hint is wanted.</para>
    ///
    /// <para>Held well under the mine's: a token halo is white, and white over a near-black floor carries at
    /// a fraction of the alpha a colour needs. See <see cref="InternodePalette.TokenGlowColour"/>.</para></summary>
    private const double TokenGlowFar = 0.06;
    private const double TokenGlowNear = 0.38;

    /// <summary>Mine halo opacity, and it runs the other way: brightest at the far end, tapering to almost
    /// nothing as the mine arrives.
    ///
    /// <para>⚠ Don't "fix" this to match the token's direction. The two halos answer different questions. A
    /// token's says <i>there is something here worth having</i>, which only matters once you can still choose
    /// to go and get it — late. A mine's says <i>plan around this</i>, which is only actionable early, while
    /// there is still track to change lanes in; by the time it is on top of the runner the decision is made
    /// and the sprite is large and unmistakable on its own, so a red bloom over it just hides the thing the
    /// player is trying to judge the edge of.</para></summary>
    private const double MineGlowFar = 0.78;
    private const double MineGlowNear = 0.07;

    /// <summary>The ramp runs on seconds remaining to the runner, not on screen position.
    ///
    /// <para>⚠ Screen position is unusable for this and it is worth knowing why: perspective crushes it, so
    /// the share of the way from vanishing point to runner is under 0.05 from four seconds out to one second
    /// out and then races to 1. A ramp on that reads as a glow doing nothing and then popping. Seconds are
    /// what the player actually experiences an approach in, and they are linear.</para></summary>
    private const double GlowRampSeconds = 5.0;

    /// <summary>Curve on the token ramp. Above 1 holds the glow down through the early approach and lets it
    /// build late: measured at view speed 9, roughly 0.11 at four seconds out, 0.16 at three, 0.26 at two,
    /// 0.45 at one, and 0.71 as it arrives.</summary>
    private const double GlowRampExponent = 2.6;

    /// <summary>Curve on the mine ramp. Near 1 on purpose — an even taper across the whole approach rather
    /// than the token's late surge. ⚠ At the token's 2.6 the mine would hold nearly full brightness until the
    /// last half second and then drop off a cliff, which reads as the glow being switched off rather than as
    /// a warning that has been heeded.</summary>
    private const double MineGlowRampExponent = 1.15;

    /// <summary>The halo behind a token or a mine. Says what is coming from far enough away to act on it,
    /// where the sprite itself is only a dark speck.
    ///
    /// <para>⚠ Drawn before <see cref="Contact"/>, so the contact shadow still reads on top of the glow. The
    /// shadow is how a player tells where a body actually sits on the wall, and a halo washing over it trades
    /// a gameplay read for a decorative one.</para>
    ///
    /// <para><paramref name="away"/> is seconds of travel still between the sprite and the runner — the ramp
    /// axis, see <see cref="GlowRampSeconds"/>. <paramref name="horizon"/> fades the halo in at the far plane
    /// on the gate's own curve — ⚠ without it the pixel floor makes a glow appear at full size the instant
    /// its event enters the draw distance, which the sprites themselves never did: they were sub-pixel there
    /// and skipped.</para></summary>
    private static void Glow(DrawingContext dc, Point p, double r, Brush glow, double spread, double away,
                             double horizon, double far, double near, double exponent)
    {
        if (horizon <= 0.01) return;
        double closing = 1 - Math.Clamp(away / Math.Max(1e-3, GlowRampSeconds), 0, 1);
        double lit = far + (near - far) * Math.Pow(closing, exponent);
        double radius = Math.Max(GlowMinPixels, r * spread);
        // The alpha rides in a cached faded brush, not an opacity layer: one glow per sprite per frame was
        // ~60 transparency layers on a busy stretch.
        dc.DrawEllipse(InternodePalette.Faded(glow, horizon * lit), null, p, radius, radius);
    }

    /// <summary>A bonus orb's size against a plain one; gold is its whole meaning, the size just makes sure it is seen.</summary>
    private const double BonusScale = 1.35;

    /// <summary>Disc, facet line and specular. Glass only above 6 px, cel only above 3 px.
    /// <paramref name="height"/> is the orb's height in pipe radii: its hue lifts with it, so how far off
    /// the ground an orb sits is readable on the orb itself rather than only from its shadow.</summary>
    private static void DrawOrb(DrawingContext dc, Point p, double r, bool hot, double height = 0, bool bonus = false,
                                double dim = 0, double phase = 0)
    {
        if (r < 0.6) return;
        int hueStep = (int)Math.Clamp(height / Math.Max(1e-6, InternodeTuning.JumpMaxApex) * (InternodePalette.OrbHeightSteps - 0.001),
                                     0, InternodePalette.OrbHeightSteps - 1);
        var colour = bonus ? InternodePalette.BonusColour : hot ? InternodePalette.OrbHotColour : InternodePalette.OrbHeightColour(hueStep);
        // Replacement art, animated if the artist supplied a cycle. A gold token with no art of its own
        // borrows the plain one and is washed gold — the colour is what "this one pays" means, so it may
        // never be lost to a missing file.
        bool ownArt = !bonus || ArcadeSprites.Has(ArcadeSprites.Slot.InternodeTokenBonus);
        // ⚠ Art on the floor is left exactly as drawn. The hue wash is the height cue and nothing else, so a
        // token sitting on the surface — which is every ordinary one — has no height to say and gets none of
        // it. Only a token up on a tether is washed. A gold one with no art of its own is the exception: it
        // is borrowing the plain token, and the colour is the whole of what "this one pays" means.
        double wash = bonus && !ownArt ? 1 : height > 0 ? ArcadeSprites.TokenTint : 0;
        if (Token(dc, p, r, bonus && ownArt ? ArcadeSprites.Slot.InternodeTokenBonus : ArcadeSprites.Slot.InternodeToken,
                  phase, colour, wash, dim))
            return;
        var fill = InternodePalette.Dimmed(colour, dim);
        dc.DrawEllipse(fill, null, p, r, r);
        // Detail fades in with size rather than switching on at a threshold: switching read as a pop as each orb
        // approached. Gloss, facet and specular from 5 to 10 px; the cel from 2.5 to 5 px.
        double detail = Math.Clamp((r - 5) / 5, 0, 1), cel = Math.Clamp((r - 2.5) / 2.5, 0, 1);
        if (detail > 0.02)
        {
            dc.PushOpacity(detail);
            dc.DrawEllipse(InternodePalette.Gloss, null, p, r, r);
            dc.DrawLine(InternodePalette.OrbFacet, new Point(p.X - r * 0.7, p.Y + r * 0.45), new Point(p.X + r * 0.7, p.Y - r * 0.45));
            dc.DrawEllipse(InternodePalette.Specular, null, new Point(p.X - r * 0.32, p.Y - r * 0.34), r * 0.42, r * 0.32);
            dc.Pop();
        }
        if (cel > 0.02)
        {
            dc.PushOpacity(cel);
            dc.DrawEllipse(null, InternodePalette.FineCel, p, r, r);
            dc.Pop();
        }
    }

    /// <summary>The ring's radius against the orb's. Hugs the cel: 15% under the 1.28 it opened at.</summary>
    private const double GoldRingScale = 1.09;

    /// <summary>The prize marker on a pattern's last orb: an orange ring outside the cel, so the player can see which
    /// orb pays before they have earned it, a shade apart from the gold it turns into.</summary>
    private static void GoldRing(DrawingContext dc, Point p, double r, double dim)
    {
        if (r < 3) return;
        dc.DrawEllipse(null, InternodePalette.DimPen(InternodePalette.GoldCandidateRing, dim, Math.Max(1.2, r * 0.2)),
            p, r * GoldRingScale, r * GoldRingScale);
    }

    /// <summary>An orb the runner let past: drained grey, so the miss is read on the orb itself as it flies by, not deduced
    /// later from the count.</summary>
    private static void DrawMissed(DrawingContext dc, Point p, double r, double dim = 0, double phase = 0)
    {
        if (r < 0.6) return;
        // With no missed-token art of its own the plain token stands in, washed to the drained grey at full
        // strength — a miss that still looked collectable would be a lie about the run.
        bool ownArt = ArcadeSprites.Has(ArcadeSprites.Slot.InternodeTokenMissed);
        if (Token(dc, p, r, ownArt ? ArcadeSprites.Slot.InternodeTokenMissed : ArcadeSprites.Slot.InternodeToken,
                  phase, InternodePalette.MissedColour, ownArt ? 0 : 0.85, dim))
            return;
        dc.DrawEllipse(InternodePalette.Dimmed(InternodePalette.MissedColour, dim), null, p, r, r);
        if (r >= 3) dc.DrawEllipse(null, InternodePalette.FineCel, p, r, r);
    }

    /// <summary>Draw a token from replacement art, or report false so the caller draws its vector body.
    ///
    /// <para>Two washes over the art, both through its own alpha: <paramref name="tint"/> carries the same
    /// state the vector fill carries (height hue, gold, drained grey), and <paramref name="dim"/> carries
    /// distance as black — darkening with depth, never thinning, which is the rule the whole pipe is drawn
    /// to.</para></summary>
    private static bool Token(DrawingContext dc, Point p, double r, string slot, double phase, Color tint,
                              double tintAlpha, double dim)
    {
        var bmp = ArcadeSprites.Frame(slot, phase);
        if (bmp is null) return false;
        ArcadeSprites.DrawWashed(dc, bmp, ArcadeSprites.Box(p, r), tint, tintAlpha, dim);
        return true;
    }

    private static void DrawMine(DrawingContext dc, Point p, double r, double rotationDeg, double dim = 0, double phase = 0)
    {
        if (r < 0.8) return;
        var body = InternodePalette.Dimmed(InternodePalette.MineColour, dim);
        var tip = InternodePalette.Dimmed(InternodePalette.MineTipColour, dim);
        // Replacement art turns with the wall the same way the star does, so a mine on the far side of the
        // pipe is oriented to its own floor rather than to the screen. Its two frames cross-fade into a
        // throb, each mine on its own phase so a run of them does not strobe together.
        if (ArcadeSprites.Blend(ArcadeSprites.Slot.InternodeMine, out var art, out var next, out double pulse, phase))
        {
            var box = ArcadeSprites.Box(p, r);
            dc.PushTransform(new RotateTransform(rotationDeg, p.X, p.Y));
            ArcadeSprites.DrawWashed(dc, art, box, Colors.Black, 0, dim);
            // The fading frame carries the same darkening, drawn at `pulse` — with the wash baked in the
            // opacity scales it for free, where the live path had to pre-scale by hand to avoid doubling.
            if (next is not null) ArcadeSprites.DrawWashed(dc, next, box, Colors.Black, 0, dim, opacity: pulse);
            dc.Pop();
            return;
        }
        dc.PushTransform(Placement(r, rotationDeg, p));
        double detail = Math.Clamp((r - 2.5) / 2.5, 0, 1);
        dc.DrawGeometry(body, null, MineUnit);
        if (detail > 0.02)
        {
            dc.PushOpacity(detail);
            dc.DrawGeometry(null, InternodePalette.MineEdgeUnit, MineUnit);
            dc.DrawGeometry(tip, null, MineTipsUnit);
            dc.Pop();
        }
        dc.DrawGeometry(tip, null, MineCoreUnit);
        dc.Pop();
    }

    // ── Runner ────────────────────────────────────────────────────────────────

    private static void DrawRunner(DrawingContext dc, Point c, double S, Internode g, double rimHere)
    {
        bool falling = g.Phase == InternodePhase.Falling;
        // A falling bike is drawn at the depth it has carried to, so perspective shrinks it as it goes.
        double zBike = falling ? ZLead + g.FallAhead : ZLead;
        var centre = Centre(c, S, zBike);
        double ring = R(zBike) * S;
        double a = Math.PI - g.Theta;
        double scale = RunnerHalf * ring;
        // Up is toward the pipe axis; a lean into the direction of travel reads as effort.
        double upright = a * 180 / Math.PI + 180;
        double lean = Math.Clamp(g.Omega / InternodePhysics.Deg * RunnerLeanPerDegPerSec,
                                 -RunnerLeanDegMax, RunnerLeanDegMax);

        if (falling)
        {
            // Through the floor: the runner sinks past the surface at full strength — the caller draws them in depth
            // order, so the nearer track occludes them. No shadow: there is no floor under them to cast one on.
        }
        else if (g.Air == InternodeAir.OverTop)
        {
            // ⚠ Nothing, and it must stay a branch rather than falling through: crossing the open top there is
            // no floor underneath to cast a shadow onto, exactly as when falling.
            // ⚠ Don't add discs to mark the flown path here: at the scale the runner is drawn they read as
            // white cloud smeared across the gap, not as a trail, and the path is already legible from the
            // bike itself.
        }
        else
        {
            // The shadow stays on the ring and shrinks to 35% at the apex: the one cue that carries jump height.
            // Below the floor there is nothing to cast it on — that is what a break in the floor is.
            if (g.H >= 0)
            {
                double shrink = Math.Clamp(1 - 0.65 * g.H / Math.Max(1e-6, InternodeTuning.JumpMaxApex), 0.35, 1);
                dc.PushTransform(Placement(scale, upright, Polar(centre, ring, a)));
                // Local Y runs toward and away from the pipe axis, so the long axis here is the one that
                // reads as depth under the bike. Softer than a ground mark because it is under a tall
                // object rather than beside a small one.
                dc.PushOpacity(RunnerShadowAlpha);
                dc.DrawEllipse(InternodePalette.Shadow, null, new Point(0, 0.12), 0.7 * shrink, RunnerShadowLong * shrink);
                dc.Pop();
                dc.Pop();
            }
        }

        // Invulnerable: a hard flicker at 10 Hz, the arcade convention for "you cannot be hurt right now".
        // ⚠ It flashes dark, not see-through: an opacity flicker would let the track show through the bike,
        // which reads as the runner half-existing rather than as a struck one, and barely reads at all over
        // a pale checker square. A black wash keeps the silhouette solid and still gives the 10 Hz beat, at
        // any depth and over any theme.
        bool flicker = g.InvulnerableLeft > 0 && ((int)(g.InvulnerableLeft * 20) & 1) == 1;
        double hitInk = flicker ? RunnerHitInk : 0;
        // The drop is straight down the screen from the bike's spot on the pipe at its depth, whatever its angle:
        // gravity reads as gravity, not as a slide outward along the wall's normal. It is in pipe radii at that
        // depth, so it shrinks with the bike.
        var stood = Polar(centre, ring, a);
        var p = falling ? new Point(stood.X, stood.Y + g.FallDepth * ring) : Polar(centre, ring * (1 - g.H), a);
        double grow = scale;
        // ⚠ The sprite path takes the lean rotation as well as its lean-state art. Don't drop it and lean on
        // the art alone: the lean states are discrete steps, and the rotation is what carries the figure
        // smoothly between them and tips it at all below the first threshold.
        // The two therefore compound: hard weave = the r3/l3 drawing plus up to RunnerLeanDegMax of turn.
        // RunnerArtLean is the dial if that reads as too much.
        if (RunnerArt(g, falling) is { } pose)
        {
            var place = Placement(grow, upright + lean * RunnerArtLean, p);
            dc.PushTransform(place);
            // ⚠ liveWash: this draw is inside Placement's unit frame, and the baked wash would size itself
            // from a rect ~2.7 units tall, bake at 16 px and be magnified into a blurred ghost of the bike.
            Rect drawn = ArcadeSprites.DrawStanding(dc, pose.Art, new Point(0, RunnerArtFootY), RunnerArtHeight,
                                                    darken: hitInk, liveWash: true);
            dc.Pop();
            // ⚠ Outside the transform, in screen pixels. Everything inside Placement is in the runner's own
            // unit frame — `drawn` is about 2.7 units tall, not 213 px — and the burst was written in pixel
            // terms, so its minimum-width guard rejected every frame and it never drew at all. Nothing was
            // visible in play but the double-jump puff, which is a different effect entirely.
            //
            // Screen space is the right frame for it regardless: the ring is a circle, so the bike's lean
            // cannot change how it looks, while the pen cache and the sub-pixel guard are both meaningful
            // only in pixels. `place` maps a port from the sprite's own frame onto the board; `grow` carries
            // units to pixels.
            //
            // The flames are not darkened with the bike: fire does not go black because its rider was hit.
            DrawExhaust(dc, pose.Slot, drawn, place, grow, c, g.JumpFlareAge);
            return;
        }
        dc.PushTransform(Placement(grow, upright + lean, p));
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerTorso);
        dc.PushClip(RunnerTorso);
        dc.DrawGeometry(InternodePalette.Gloss, null, RunnerTorso);
        dc.DrawGeometry(null, InternodePalette.EdgeBounceUnit, RunnerTorso);
        dc.DrawGeometry(InternodePalette.RunnerTrim, null, RunnerTrim);
        dc.DrawEllipse(InternodePalette.Specular, null, new Point(-0.16, -1.08), 0.26, 0.20);
        dc.Pop();
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerArms);
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerFeet);
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerHead);
        dc.DrawGeometry(null, InternodePalette.CelUnit, RunnerArms);
        dc.DrawGeometry(null, InternodePalette.CelUnit, RunnerFeet);
        dc.DrawGeometry(null, InternodePalette.CelUnit, RunnerTorso);
        dc.DrawGeometry(null, InternodePalette.CelUnit, RunnerHead);
        // The vector fallback darkens the same way: its silhouette pieces re-filled in black over the
        // drawing, inside the placement transform so they land on the body exactly.
        if (hitInk > 0)
        {
            var ink = InternodePalette.Solid(Colors.Black, hitInk);
            dc.DrawGeometry(ink, null, RunnerTorso);
            dc.DrawGeometry(ink, null, RunnerArms);
            dc.DrawGeometry(ink, null, RunnerFeet);
            dc.DrawGeometry(ink, null, RunnerHead);
        }
        dc.Pop();
    }

    /// <summary>The unit-space box replacement runner art stands in: the vector runner's own extent, head
    /// top to sole, so a sprite is the same size on the floor as the figure it replaces. Width follows the
    /// art's aspect — the lean states are free to be wider than the upright one.</summary>
    private const double RunnerArtHeight = 2.11 * RunnerArtScale;

    /// <summary>Where the art's bottom edge sits in unit space. Pushed outward (down the screen, away from
    /// the pipe axis) by <see cref="RunnerArtDrop"/> of its own height, so the bike rides low against a
    /// shadow that stays on the floor line. Nothing the sim owns moves with it — the collider is the runner's
    /// angle, not its picture.</summary>
    private const double RunnerArtFootY = 0.15 + RunnerArtDrop * RunnerArtHeight;

    // ── The exhaust ports ─────────────────────────────────────────────────────
    /// <summary>Where each runner pose's exhaust ports sit, as fractions of that sprite's own bitmap, keyed
    /// by the slot the art was resolved from.
    ///
    /// <para>The ports are fixed in the art, so this is a table rather than a scan. Most pairs are the old
    /// pixel detector's own readings of the shipped set rather than hand measurements; the two frames of a
    /// cycle agreed to within 0.002, a fraction of a pixel once drawn, so one pair covers both. The far port
    /// on l2 and r2 is the author's measurement, x71 y234 and x315 y234 on the 388×512 sprite. l0 and r0 are
    /// interpolated halfway between c and l1/r1 — those neighbours differ by under four pixels, so the error
    /// is sub-pixel at the size the bike is drawn.</para>
    ///
    /// <para>⚠ Every pose has two ports. The detector discarded l2's and r2's far one — occluded, so its
    /// visible remnant failed a size test against the near port — which is one reason it is gone.</para>
    ///
    /// <para>⚠ Fractions of the bitmap, so they only describe the art that ships. A replacement runner
    /// supplied through <c>%APPDATA%\Radiata\arcade-sprites</c> gets flames at these coordinates whether or
    /// not its own ports are there.</para></summary>
    private static readonly Dictionary<string, Point[]> RunnerPorts = new()
    {
        ["internode-runner-l3"]   = [new(0.0676, 0.4588), new(0.4497, 0.5450)],
        ["internode-runner-l2"]   = [new(0.1830, 0.4570), new(0.5539, 0.5425)],
        ["internode-runner-l1"]   = [new(0.1785, 0.4952), new(0.8034, 0.5163)],
        ["internode-runner-l0"]   = [new(0.1837, 0.4960), new(0.8076, 0.5063)],
        ["internode-runner-c"]    = [new(0.1888, 0.4967), new(0.8117, 0.4963)],
        ["internode-runner-r0"]   = [new(0.1914, 0.5065), new(0.8153, 0.4958)],
        ["internode-runner-r1"]   = [new(0.1940, 0.5163), new(0.8189, 0.4952)],
        ["internode-runner-r2"]   = [new(0.4409, 0.5426), new(0.8119, 0.4570)],
        ["internode-runner-r3"]   = [new(0.5476, 0.5448), new(0.9298, 0.4590)],
        ["internode-runner-jump"] = [new(0.2017, 0.6016), new(0.7949, 0.6013)],
        ["internode-runner-fall"] = [new(0.1980, 0.4041), new(0.8028, 0.4040)],
    };

    /// <summary>A pose's ports, or empty for a slot with none recorded — which costs that pose its flames
    /// and nothing else.</summary>
    private static Point[] ExhaustPorts(string slot) =>
        RunnerPorts.TryGetValue(slot, out var hit) ? hit : [];

    /// <summary>The exhaust burst: a flame-coloured ring that leaps out of each port and thins away to
    /// nothing.
    ///
    /// <para>⚠ A ring that leaves, not a glow that sits. The first cut was a filled radial gradient on the
    /// port, and however briefly it lasted it read as a lamp that had been switched on rather than as a kick
    /// — because a bright blob centred on a fixed point is what a light looks like. An expanding circle is
    /// unmistakably an event: it is somewhere else by the time you have registered it, and the width going to
    /// zero is what ends it, so there is no fade to mistake for something dimming.</para>
    ///
    /// <para>Two strokes on the one circle: an orange body with a narrower hot core inside it, which is what
    /// makes a coloured hoop read as flame.</para>
    ///
    /// <para>⚠ Runs off the sim's own <c>JumpFlareAge</c> rather than a clock of its own, like every other
    /// effect here: a frozen or paused board has to repaint identically, and a renderer-side timer would keep
    /// burning while the game is held still.</para></summary>
    private static void DrawExhaust(DrawingContext dc, string slot, Rect drawn, Transform place,
                                    double scale, Point field, double age)
    {
        // The art has its own life, longer than the procedural ring's, with an attack the ring never needed.
        var art = ArcadeSprites.Get(ArcadeSprites.Slot.InternodeJetBurst);
        double life = art is not null ? JetBurstArtSeconds : FlareSeconds;
        if (drawn.IsEmpty || age < 0 || age >= life) return;
        var ports = ExhaustPorts(slot);
        if (ports.Length == 0) return;

        // ⚠ Pixels from here down. `drawn` is in the runner's unit frame, so every size crosses into screen
        // space through `scale` — the sub-pixel cut-off below and InternodePalette.Stroke's quarter-pixel pen
        // cache are both nonsense in any other frame, and getting that wrong is what stopped this drawing.
        double height = drawn.Height * scale;
        // ⚠ The guard that would have caught this silently-dead effect the first time. The runner sits at a
        // fixed depth, so its drawn height is hundreds of pixels; anything of order 1 means unit values
        // reached here instead of screen ones, and every size below would then fall under the cut-off and
        // draw nothing at all — which is exactly what shipped.
        Debug.Assert(height > 8, $"Internode: exhaust height {height:0.00} looks like unit space, not pixels.");
        double t = Math.Clamp(age / life, 0, 1);
        // Out fast and settling: over half the travel is gone in the first quarter of the life, so the ring
        // is already leaving the port by the time the eye finds it.
        double radius = height * FlareReach * (1 - Math.Pow(1 - t, 3.2));
        // ...and the stroke thins to nothing. This is the whole expiry — no alpha ramp, so what the player
        // sees is a rim of fire being stretched thinner until it is gone.
        double width = height * FlareWidth * Math.Pow(1 - t, 1.15);
        if (art is null && width < 0.3) return;
        // Left behind. The burst slides outward from the middle of the playfield over its life, which is the
        // direction everything on the pipe travels as the runner goes forward — so the ring reads as a jet of
        // flame dropped in the track's frame rather than as a decal stuck to the bike. Accelerating, because
        // that is how the perspective moves a fixed point.
        double drift = height * FlareDrift * t * t;

        // The author's burst art, when supplied, replaces the whole procedural figure: grown out on the same
        // curve the ring travels, spun a little so two bursts a frame apart are not the same picture, and on
        // its own envelope: in over the attack, held full, then out over the release. Its glow
        // reaches past the ring's radius, hence the margin.
        double artHalf = radius * JetBurstArtSpread;
        // The release is two halves: the burst goes to black at full opacity first, then the black
        // fades out. A fire that dims before it thins reads as burnt out; one that only thins reads as blown away.
        double artAlpha = 1, artInk = 0;
        if (art is not null) drift *= JetBurstDriftBoost;
        if (JetBurstAttackSeconds > 0 && age < JetBurstAttackSeconds) artAlpha = age / JetBurstAttackSeconds;
        else if (age >= JetBurstAttackSeconds + JetBurstHoldSeconds)
        {
            double r = Math.Clamp((age - JetBurstAttackSeconds - JetBurstHoldSeconds) / JetBurstReleaseSeconds, 0, 1);
            if (r < 0.5) artInk = r / 0.5;
            else { artInk = 1; artAlpha = 1 - (r - 0.5) / 0.5; }
        }

        var ringPen = InternodePalette.Stroke(InternodePalette.FlameRing, width);
        var hotPen = InternodePalette.Stroke(InternodePalette.FlameRingHot, width * FlareHotShare);
        var dartFill = InternodePalette.Solid(InternodePalette.FlameRing);
        var dartHot = InternodePalette.Solid(InternodePalette.FlameRingHot);

        for (int portIndex = 0; portIndex < ports.Length; portIndex++)
        {
            var port = ports[portIndex];
            var at = place.Transform(new Point(drawn.X + port.X * drawn.Width,
                                               drawn.Y + port.Y * drawn.Height));
            double ox = at.X - field.X, oy = at.Y - field.Y;
            double away = Math.Sqrt(ox * ox + oy * oy);
            if (away > 1e-6) at = new Point(at.X + ox / away * drift, at.Y + oy / away * drift);

            if (art is not null)
            {
                if (artHalf < 1) continue;
                // ⚠ The two ports counter-rotate: spun the same way they read as one rigid pair.
                double spinDir = (portIndex & 1) == 0 ? 1 : -1;
                dc.PushTransform(new RotateTransform(spinDir * t * JetBurstSpinDeg, at.X, at.Y));
                // The ink and the fade share one opacity push, so the black-out is never see-through.
                dc.PushOpacity(artAlpha);
                Rect burst = ArcadeSprites.Draw(dc, art, ArcadeSprites.Box(at, artHalf), ArcadeSprites.Fit.Contain);
                ArcadeSprites.Tint(dc, art, burst, Colors.Black, artInk);
                dc.Pop();
                dc.Pop();
                continue;
            }

            dc.DrawEllipse(null, ringPen, at, radius, radius);
            dc.DrawEllipse(null, hotPen, at, radius, radius);

            // ⚠ The darts counter-rotate, alternating by index. Spun the same way they read as one rigid
            // wheel turning, which is a mechanism; crossing each other they read as fire being thrown off,
            // which is the point. With three of them the pattern never repeats inside a single burst.
            for (int i = 0; i < FlareDarts; i++)
            {
                double spin = ((i & 1) == 0 ? 1 : -1) * t * FlareDartSpin;
                double a = i * Math.Tau / FlareDarts + spin;
                double cx = Math.Cos(a), cy = Math.Sin(a);
                // Long axis radial: a point toward the port and a point away from it, exactly as drawn.
                var mid = new Point(at.X + cx * radius, at.Y + cy * radius);
                double lng = radius * FlareDartLong, wide = width * FlareDartWide;
                Dart(dc, dartFill, mid, cx, cy, lng, wide);
                Dart(dc, dartHot, mid, cx, cy, lng * FlareHotShare, wide * FlareHotShare);
            }
        }
    }

    /// <summary>One radially-stretched diamond: points at <paramref name="lng"/> along the radius either way
    /// from <paramref name="mid"/>, and at <paramref name="wide"/> across it.</summary>
    private static void Dart(DrawingContext dc, Brush fill, Point mid, double cx, double cy,
                             double lng, double wide)
    {
        if (lng < 0.4 || wide < 0.2) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(mid.X + cx * lng, mid.Y + cy * lng), true, true);
            g.LineTo(new Point(mid.X - cy * wide, mid.Y + cx * wide), true, true);
            g.LineTo(new Point(mid.X - cx * lng, mid.Y - cy * lng), true, true);
            g.LineTo(new Point(mid.X + cy * wide, mid.Y - cx * wide), true, true);
        }
        geo.Freeze();
        dc.DrawGeometry(fill, null, geo);
    }

    /// <summary>How black the runner goes on the invulnerable flicker: half. The flash is the art at
    /// 50% brightness and nothing else — no blur, no shadow, no opacity — so the silhouette stays exactly
    /// the sprite's own.</summary>
    private const double RunnerHitInk = 0.50;

    /// <summary>How long the whole burst lasts, seconds. The second press of a double jump
    /// restarts it, so two kicks read as two.</summary>
    private const double FlareSeconds = 0.30;
    /// <summary>How far the ring travels, as a share of the sprite's drawn height, and how thick it starts.
    /// Both scale off the sprite so the burst holds its proportions at every distance down the pipe.
    /// <para>⚠ Reach was 0.30 first and it was far too much: the two rings met over the middle of the bike
    /// and grew past its silhouette, which reads as an explosion around the vehicle rather than a kick out
    /// of its ports. At 0.16 each ring ends about two and a half times the port's own diameter, so it
    /// plainly leaves the port and plainly belongs to it.</para></summary>
    private const double FlareReach = 0.16;
    /// <summary>The burst art's half-size against the ring radius the procedural burst would have at the same
    /// instant: its glow feathers well past its core, so it needs room beyond the ring to read as full size.</summary>
    private const double JetBurstArtSpread = 1.7;
    /// <summary>How far the burst art turns over its life, degrees. Enough that two bursts in quick
    /// succession differ, not enough to read as a wheel.</summary>
    private const double JetBurstSpinDeg = 40;
    /// <summary>The burst art's opacity envelope: in over the attack, full through the hold, out over
    /// the release. Their sum is the art's life, which the growth and the drift also span.</summary>
    private const double JetBurstAttackSeconds = 0;     // full from the first frame
    // The burst lives twice as long undarkened (the hold) as darkened (the release, which inks it to
    // black over its first half and fades it over its second) — 0.30 s total.
    private const double JetBurstHoldSeconds = 0.20;
    private const double JetBurstReleaseSeconds = 0.10;
    /// <summary>The art leaves the bike this many times faster than the procedural ring did.</summary>
    private const double JetBurstDriftBoost = 2.0;
    private const double JetBurstArtSeconds = JetBurstAttackSeconds + JetBurstHoldSeconds + JetBurstReleaseSeconds;
    private const double FlareWidth = 0.05;
    /// <summary>The hot core's width against the orange body's. Well under half, so the orange reads as a
    /// rim around it rather than as a second ring.</summary>
    private const double FlareHotShare = 0.42;

    /// <summary>How far the whole burst slides outward from the playfield centre over its life, as a share of
    /// the sprite's drawn height. Outward is the direction everything on the pipe travels, so this is what
    /// makes the ring read as flame left behind rather than as a decal stuck to the bike.</summary>
    private const double FlareDrift = 0.42;
    /// <summary>The radially-stretched diamonds riding the ring, and how far each turns across the burst.
    /// Alternate ones turn the other way — see the loop.</summary>
    private const int FlareDarts = 3;
    private const double FlareDartSpin = 0.62;
    /// <summary>A dart's half-length along the radius, against the ring's own radius, and its half-width
    /// across it, against the ring's stroke. Tying the girth to the stroke is what makes the darts thin away
    /// with the ring instead of surviving it as three floating shards.</summary>
    private const double FlareDartLong = 0.62;
    private const double FlareDartWide = 1.15;

    /// <summary>How far the bike is dropped clear of its shadow, as a share of its own height.</summary>
    private const double RunnerArtDrop = 0.15;

    /// <summary>Degrees of tilt per degree-per-second of travel around the pipe, and the cap. The sign is the
    /// board's own: positive angular speed tips the figure toward its own right, which is why the <c>r</c>
    /// art states are the positive half. At <c>MaxAngularSpeedDeg</c> 400 the raw term is 20°, so the cap is
    /// what the fastest weave actually reaches.</summary>
    private const double RunnerLeanPerDegPerSec = 0.05;
    private const double RunnerLeanDegMax = 20;

    /// <summary>Share of that lean the art path takes. 1 keeps the sprite and the vector runner tilting
    /// identically, which is why it is the default — one lean quantity, not two.
    /// ⚠ It adds to the tilt already drawn into the lean states, so this is the knob to pull down if a hard
    /// weave over-rotates. Zero restores the old upright-sprite behaviour.</summary>
    private const double RunnerArtLean = 1.0;

    /// <summary>The runner shadow's half-extent along the toward/away-from-centre axis, in pipe radii. Longer
    /// than the across axis, so it lies under the bike rather than reading as a disc beside it.</summary>
    private const double RunnerShadowLong = 0.34;

    private const double RunnerShadowAlpha = 0.7;

    /// <summary>Replacement art against the vector runner's own height. The wheels stay on the floor line
    /// whatever this is — only the top grows. ⚠ The shadow is not scaled by it: it is drawn from
    /// <see cref="RunnerHalf"/> like every other ground mark, so a large change here wants a look at whether
    /// the bike still sits on its own shadow. ⚠ Screen size is this times the runner's ring, so it moves
    /// with <see cref="LeadKz"/>.</summary>
    private const double RunnerArtScale = 1.275;

    /// <summary>Where each lean state takes over, as a share of <see cref="InternodeTuning.MaxAngularSpeedDeg"/>
    /// — 2, 12, 88 and 280 °/s.
    ///
    /// <para>The first entry is the l0/r0 tip: anything moving sideways at all is off the upright state, so
    /// the sprite answers the stick immediately, and the band it covers is everything under the l1/r1
    /// threshold.</para>
    ///
    /// <para>⚠ Deliberately uneven, and evenly spaced thresholds are the thing this replaces. The first lean
    /// starts almost at once, so any steering at all shows in the bike; the hardest is withheld until the
    /// runner is well up toward the speed cap, so it means something; and the middle state gets the wide
    /// band between them, which is where a player actually spends their time. Measured over fourteen seconds
    /// of play: the middle lean went from 8% of an ordinary weave to 25%, and the hardest stopped appearing
    /// in one at all.</para></summary>
    private static readonly double[] RunnerLeanAt = [0.005, 0.03, 0.22, 0.70];

    /// <summary>Which runner sprite the state calls for, or null to draw the vector runner.
    ///
    /// <para>Lateral speed picks one of nine leans, hardest left to hardest right, off the same signed
    /// quantity the vector lean uses: positive angular speed tips the figure toward its own right, so the
    /// <c>r</c> states are the positive half. Air beats lean — a jump or a fall is a whole-body pose, not a
    /// tilt — and every missing file falls back inward to the upright state, so a partial set still
    /// reads.</para></summary>
    private static (BitmapSource Art, string Slot)? RunnerArt(Internode g, bool falling)
    {
        // ⚠ The tyre spins with distance, never with wall time. ArcadeSprites.Time is deliberately
        // wall-clocked — a token's spin is decoration and may keep turning behind a card — but the runner is
        // a game object, and the pause menu repaints on every cursor move, so a wall-clocked runner flicks
        // between frames on a board the player can see is frozen. Off g.Z it holds still exactly when the sim
        // does, and the wheel turns with the ground rather than beside it.
        // z advances at 1 per second of travel, so multiplying by RunnerFps keeps the cadence it always had.
        double spin = g.Z * ArcadeSprites.RunnerFps;
        string centre = ArcadeSprites.Slot.InternodeRunner[ArcadeSprites.Slot.RunnerCentre];
        // ⚠ The slot travels with the bitmap, because the fallbacks below can hand back a pose other than
        // the one asked for and the exhaust ports are tabulated per pose.
        (BitmapSource, string)? Pick(params string[] slots)
        {
            foreach (string s in slots)
            {
                var frames = ArcadeSprites.Cycle(s);
                if (frames.Length == 0) continue;
                int i = frames.Length == 1 ? 0 : (int)(spin - Math.Floor(spin / frames.Length) * frames.Length);
                return (frames[Math.Clamp(i, 0, frames.Length - 1)], s);
            }
            return null;
        }
        if (falling)
            return Pick(ArcadeSprites.Slot.InternodeRunnerFall,
                        ArcadeSprites.Slot.InternodeRunnerJump, centre);
        double u = Math.Abs(g.Omega) / Math.Max(1e-6, InternodeTuning.MaxAngularSpeedDeg * InternodePhysics.Deg);
        int step = 0;
        for (int s = 0; s < RunnerLeanAt.Length; s++) if (u >= RunnerLeanAt[s]) step = s + 1;
        // ⚠ Negated. Positive angular speed carries the runner toward screen right round the pipe, but the
        // states are named for the way the bike leans into it, which is the other way — so positive omega
        // takes the l-states. Don't "fix" the sign without looking at the art on the board.
        int i = Math.Clamp(ArcadeSprites.Slot.RunnerCentre - Math.Sign(g.Omega) * step,
                           0, ArcadeSprites.Slot.InternodeRunner.Length - 1);

        // ⚠ Lean outranks air. A bike carrying speed sideways is leaning whether or not it is touching the
        // floor, and that includes the slide across the top gap — the jump pose is for going straight up.
        if (g.Air != InternodeAir.Grounded && i == ArcadeSprites.Slot.RunnerCentre)
            return Pick(ArcadeSprites.Slot.InternodeRunnerJump, centre);

        // Walk back toward upright rather than giving up: a set drawn as l2 / c / r2 resolves l3 → l2 → c.
        while (i != ArcadeSprites.Slot.RunnerCentre)
        {
            if (Pick(ArcadeSprites.Slot.InternodeRunner[i]) is { } hit) return hit;
            i += i < ArcadeSprites.Slot.RunnerCentre ? 1 : -1;
        }
        return Pick(centre);
    }

    /// <summary>How fast a spray orb closes the gap to the rim: the exponential rate, so 1/e of the gap is
    /// left after 1/this seconds. 7 puts an orb most of the way out in a seventh of a second.</summary>
    private const double BurstFlingPerSec = 7.0;
    /// <summary>Where the fling stops, as a share of the disc radius; each orb takes 86..100% of it.</summary>
    private const double BurstRimStop = 0.97;
    /// <summary>The opening scatter: how far an orb is thrown in its own direction, as a share of the disc
    /// radius (each orb 0.5..1.5× this), and how fast that throw is spent.</summary>
    private const double BurstScatterReach = 0.12;
    private const double BurstScatterPerSec = 10.0;
    /// <summary>How long the scatter has the orb to itself before the rim starts pulling.</summary>
    private const double BurstPullDelaySeconds = 0.08;

    /// <summary>A spray orb against a live one. Half.</summary>
    private const double BurstOrbScale = 0.5;

    private static void DrawBursts(DrawingContext dc, Point c, double S, Internode g)
    {
        if (g.Bursts.Count == 0) return;
        var centre = Centre(c, S, ZLead);
        double ring = R(ZLead) * S;
        double total = Math.Max(1e-3, InternodeTuning.BurstSeconds);
        foreach (var b in g.Bursts)
        {
            if (b.Age >= total) continue;
            double h1 = Hash01(b.Seed), h2 = Hash01(b.Seed * 31 + 7);
            // A fan flung away from the screen centre: each orb takes its own angle and its own outward
            // speed, and nothing brings it back — a negative height here is a radius past the ring. On top of
            // that it is left behind: the runner runs on, so it swells like any passed sprite until it leaves
            // the disc. Half the size of a live orb, so the spray reads as debris rather than as pickups.
            double spray = Math.Min(b.Age, 0.35);
            double theta = b.Theta + (h1 - 0.5) * 5.0 * spray;
            double grow = 1 + 1.7 * b.Age;
            // ⚠ Thrown outward toward the rim, not off it. The runner already stands near the disc's edge,
            // so a straight outward speed clears the visible disc in a few frames and the spray is never
            // seen. Instead each orb closes most of the gap to the rim fast and eases to a stop just inside
            // it (its own share of the gap, so the fan has depth), and the left-behind swell then carries it
            // off over the rest of its life.
            // Two motions: a scatter in every direction first — each orb has its own heading and
            // reach, spent quickly — and then the pull to the rim, which starts a beat later and takes over.
            double h3 = Hash01(b.Seed * 17 + 3), h4 = Hash01(b.Seed * 53 + 11);
            double basePx = ring * grow * (1 - b.Radial);
            var from = Polar(centre, basePx, Math.PI - theta);
            double scatter = S * BurstScatterReach * (0.5 + h4) * (1 - Math.Exp(-BurstScatterPerSec * b.Age));
            double heading = h3 * Math.Tau;
            var p0 = new Point(from.X + Math.Cos(heading) * scatter, from.Y + Math.Sin(heading) * scatter);
            double r0x = p0.X - centre.X, r0y = p0.Y - centre.Y;
            double r0 = Math.Sqrt(r0x * r0x + r0y * r0y);
            double rimPx = S * BurstRimStop * (0.86 + 0.14 * h2);
            double fling = 1 - Math.Exp(-BurstFlingPerSec * Math.Max(0, b.Age - BurstPullDelaySeconds));
            double radiusPx = r0 + Math.Max(0, rimPx - r0) * fling;
            if (radiusPx > S * 1.7 || r0 < 1e-6) continue;
            var p = new Point(centre.X + r0x / r0 * radiusPx, centre.Y + r0y / r0 * radiusPx);
            DrawOrb(dc, p, ring * grow * OrbRadius * BurstOrbScale, false, bonus: b.Gold);
        }
    }

    /// <summary>The snap of light a pattern's last orb throws the instant it turns gold: the orb flares, two thin rings
    /// run out of it and a ring of short spokes flies off. Under half a second and inside three orb radii, so it reads
    /// as a reward without hiding the pipe behind it.</summary>
    private static void DrawGoldFlashes(DrawingContext dc, Point c, double S, Internode g, double twist0)
    {
        if (g.GoldFlashes.Count == 0) return;
        double total = Math.Max(1e-3, InternodeTuning.GoldFlashSeconds);
        var gold = InternodePalette.GateGoldColour;
        foreach (var f in g.GoldFlashes)
        {
            double t = Math.Clamp(f.Age / total, 0, 1);
            double zv = (f.Z - g.Z) + ZLead;
            if (zv <= 0 || zv > ZFar) continue;
            var centre = Centre(c, S, zv);
            double ring = R(zv) * S;
            double a = Math.PI - f.Theta + (g.TwistAt(zv - ZLead) - twist0);
            double r = ring * OrbRadius * (f.Height > 0 ? 1 : BonusScale);
            if (r < 1.5) continue;
            var p = f.Height > 0 ? Polar(centre, ring * (1 - f.Height), a) : Polar(centre, ring - r * GroundLift, a);
            // Fast out, slow settle: the light is gone before the orb has travelled far.
            double ease = Math.Sqrt(t), fade = (1 - t) * (1 - t);

            // The flare on the orb itself, brightest at the instant of the turn.
            double flare = Math.Clamp(1 - t / 0.35, 0, 1);
            if (flare > 0.01)
            {
                dc.PushOpacity(flare);
                dc.DrawEllipse(InternodePalette.Bonus, null, p, r * (1 + 0.5 * flare), r * (1 + 0.5 * flare));
                dc.DrawEllipse(InternodePalette.OrbHot, null, p, r * 0.55 * flare, r * 0.55 * flare);
                dc.Pop();
            }

            // Two rings running out, the second a beat behind.
            Ring(dc, p, r * (1 + 1.9 * ease), Math.Max(1.0, r * 0.22) * fade, gold, 0.85 * fade);
            double t2 = Math.Clamp((t - 0.18) / 0.82, 0, 1);
            if (t2 > 0) Ring(dc, p, r * (1 + 1.5 * Math.Sqrt(t2)), Math.Max(0.8, r * 0.14) * (1 - t2), gold, 0.5 * (1 - t2) * (1 - t2));

            // Spokes: short darts thrown outward, thinning as they go.
            if (r >= 3 && fade > 0.02)
            {
                var pen = InternodePalette.Stroke(gold, Math.Max(1.0, r * 0.18) * fade, 0.9 * fade);
                for (int i = 0; i < 6; i++)
                {
                    double sa = a + i * Math.PI / 3 + 0.26;
                    double inner = r * (1.15 + 1.5 * ease), outer = inner + r * 0.75 * (1 - t);
                    dc.DrawLine(pen, Polar(p, inner, sa), Polar(p, outer, sa));
                }
            }
        }
    }


    /// <summary>A catch leaves a pop at the runner: a ring that swells and fades, gold for a bonus. This is the "did I
    /// get it" signal; a miss is carried by the greyed orb flying past instead.</summary>
    private static void DrawCollects(DrawingContext dc, Point c, double S, Internode g)
    {
        if (g.Collects.Count == 0) return;
        var centre = Centre(c, S, ZLead);
        double ring = R(ZLead) * S;
        double total = Math.Max(1e-3, InternodeTuning.CollectPopSeconds);
        foreach (var col in g.Collects)
        {
            // A plain catch pops nothing at the runner: the sound and the counter carry it. Only a gold
            // catch still throws its ring.
            if (col.Value <= 1) continue;
            double t = Math.Clamp(col.Age / total, 0, 1);
            var p = Polar(centre, ring * (1 - col.H), Math.PI - col.Theta);
            Ring(dc, p, ring * OrbRadius * (0.7 + 1.3 * t), Math.Max(1.2, ring * 0.02) * (1 - t), InternodePalette.GateGoldColour, 0.55 * (1 - t));
        }
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    /// <summary>A plaque at 12 o'clock in the fixed frame: under roll and twist no patch of sky is guaranteed
    /// anywhere, so the readout brings its own ground.</summary>
    private static void DrawHud(DrawingContext dc, Point c, double S, Internode g, double ppd)
    {
        double pulse = Math.Clamp(g.CollectPulse, 0, 1);
        double baseSize = Math.Max(10, S * 0.068);
        double size = ArcadeChrome.Ui(baseSize) * (1 + 0.12 * pulse);
        bool met = g.Tokens >= g.Quota;
        var ink = met ? InternodePalette.GateGold : InternodePalette.Ink;
        var count = ArcadeChrome.Text(Loc.F(UiText.Arcade.TokensOfQuota, g.Tokens, g.Quota), size, ink, ppd, TextAlignment.Left);
        // The score climbs as the banked orbs land on it, so the number and the flight agree.
        double bankLife = Math.Max(1e-3, InternodeTuning.BankSeconds + InternodeTuning.BankStaggerSeconds * Math.Max(0, InternodeTuning.MaxBankOrbs));
        int shown = g.Score - (int)Math.Round(g.BankAmount * (1 - Math.Clamp(g.BankAge / bankLife, 0, 1)));
        var score = ArcadeChrome.Text(Loc.F(UiText.Arcade.Score, shown), ArcadeChrome.Ui(Math.Max(9, S * 0.046)), InternodePalette.GateGold, ppd, TextAlignment.Left);
        var stage = ArcadeChrome.Text(Loc.F(UiText.Arcade.StageLevel, g.Stage, g.Section), ArcadeChrome.Ui(Math.Max(8, S * 0.040)),
                                      InternodePalette.InkDim, ppd, TextAlignment.Left);
        // The pictogram is on the art scale, the words on the text scale; the pulse swells both together.
        double orb = ArcadeChrome.UiArt(baseSize * 0.34) * (1 + 0.12 * pulse);
        // At a gate a row slides in from the top of the plaque — CHECKPOINT for a short hand, LEVEL UP for a
        // bank — pushing the readout and the plate's bottom down with it, then slides back out.
        FormattedText? notice = null;
        double noticeH = 0;
        if (g.GateNotice != InternodeGateNotice.None && g.GateNoticeAge < InternodeTuning.GateNoticeSeconds)
        {
            bool up = g.GateNotice == InternodeGateNotice.LevelUp;
            notice = ArcadeChrome.Text(Loc.T(up ? UiText.Arcade.LevelUp : UiText.Arcade.Checkpoint), ArcadeChrome.Ui(Math.Max(9, S * 0.050)),
                                       up ? InternodePalette.GateGold : InternodePalette.Ink, ppd, TextAlignment.Left);
            double life = InternodeTuning.GateNoticeSeconds, slide = Math.Min(0.25, life / 3);
            double p = g.GateNoticeAge < slide ? g.GateNoticeAge / slide
                     : g.GateNoticeAge > life - slide ? (life - g.GateNoticeAge) / slide : 1;
            p = Math.Clamp(p, 0, 1); p = p * p * (3 - 2 * p);
            noticeH = (notice.Height + size * 0.20) * p;
        }
        // The stage row opens at the bottom when the stage number moves — DEMOTED after a fall or a mine on
        // an empty hand, PROMOTED when a gate ends a stage — pushing the plate's bottom down and sliding back
        // out; it pulses on the quota pulse's beat the whole time it is up, since it is the plaque's one
        // piece of news that is about the run rather than the section.
        FormattedText? stageRow = null;
        double stageRowH = 0, stagePulse = 0;
        if (g.StageNotice != InternodeStageNotice.None && g.StageNoticeAge < InternodeTuning.StageNoticeSeconds)
        {
            bool down = g.StageNotice == InternodeStageNotice.Demoted;
            stageRow = ArcadeChrome.Text(Loc.T(down ? UiText.Arcade.Demoted : UiText.Arcade.Promoted), ArcadeChrome.Ui(Math.Max(8, S * 0.040)),
                                         down ? InternodePalette.Solid(InternodePalette.ShoutFail) : InternodePalette.GateGold, ppd, TextAlignment.Left);
            double life = InternodeTuning.StageNoticeSeconds, slide = Math.Min(0.25, life / 3);
            double p = g.StageNoticeAge < slide ? g.StageNoticeAge / slide
                     : g.StageNoticeAge > life - slide ? (life - g.StageNoticeAge) / slide : 1;
            p = Math.Clamp(p, 0, 1); p = p * p * (3 - 2 * p);
            // The band is sized for a row a quarter taller than the text, so the plate keeps the height it had
            // at the larger type and the difference becomes padding under the word.
            stageRowH = (stageRow.Height * 1.25 + size * 0.62) * p;
            stagePulse = g.StagePulse * p;
        }
        var plate = DrawPlaque(dc, c.X, c.Y - S * 0.95, [count, score, stage], orb * 2 + size * 0.35, size * 0.20, size * 0.5, size * 0.26,
                               out Point lead, out Point stageSlot, S * PlaqueHang, notice, noticeH, stageRow, stageRowH, 1 + 0.12 * stagePulse);
        // Dev only (ArcadeDebug.PhraseTester): the phrase under test on its own small plate under the readout,
        // by its bank id and its place in the order, so a playability note names the phrase exactly.
        if (g.TesterPhraseId is { } testerId)
        {
            var name = ArcadeChrome.Text(testerId.ToUpperInvariant(), ArcadeChrome.Ui(Math.Max(8, S * 0.040)), InternodePalette.GateGold, ppd, TextAlignment.Left);
            var place = ArcadeChrome.Text($"{g.TesterIndex + 1} / {Internode.TesterCount}", ArcadeChrome.Ui(Math.Max(7, S * 0.032)), InternodePalette.InkDim, ppd, TextAlignment.Left);
            double padX = size * 0.4, padY = size * 0.18, gap = size * 0.1;
            double w = Math.Max(name.Width, place.Width) + padX * 2, h = name.Height + place.Height + gap + padY * 2;
            var body = new Rect(c.X - w / 2, plate.Bottom + size * 0.25, w, h);
            ArcadeChrome.DrawHangingPlate(dc, body, 0, InternodePalette.Plaque, InternodePalette.PlaquePen);
            dc.DrawText(name, new Point(c.X - name.Width / 2, body.Y + padY));
            dc.DrawText(place, new Point(c.X - place.Width / 2, body.Y + padY + name.Height + gap));
        }
        if (stageRow is not null && stagePulse > 0.01)
        {
            double gw = stageRow.Width + size * 0.9, gh = stageRow.Height * 1.5;
            dc.DrawEllipse(InternodePalette.QuotaGlow(0.55 * stagePulse), null, stageSlot, gw / 2, gh / 2);
        }
        // A short hand at a gate pulses a glow behind the count several times — the size swell above says the
        // readout was touched, the repeat says to look at it. ⚠ Drawn over the plaque and under nothing else:
        // the count is already painted, so this is a wash on top of it rather than a backing, which is what
        // keeps the digits readable at the peak instead of being lifted off their own plate.
        double glow = g.QuotaPulse;
        if (glow > 0.01)
        {
            double gw = count.Width + orb * 2 + size * 0.7, gh = size * 1.5;
            var at = new Point(lead.X + gw / 2 - orb * 0.5, lead.Y);
            dc.DrawEllipse(InternodePalette.QuotaGlow(0.55 * glow), null, at, gw / 2, gh / 2);
        }
        DrawOrb(dc, new Point(lead.X + orb, lead.Y), orb, pulse > 0.05);
        DrawBankFlight(dc, c, S, g, new Point(lead.X + orb, lead.Y), orb);
    }

    /// <summary>How large a banked orb leaves the runner, against the readout's own pictogram it lands on.
    /// The payout is the loudest good thing in a run, and pictogram size alone reads as a few specks
    /// crossing the disc rather than as a hand of orbs being cashed in.</summary>
    private const double BankOrbStartScale = 1.8;

    /// <summary>The flight's end size against the readout's pictogram. Half the start scale, so it reads as
    /// a stream of orbs rather than a hand of them.</summary>
    private const double BankOrbEndScale = 0.5;

    /// <summary>How far the flight's arc bows, as a fraction of the runner-to-readout chord. ⚠ The bow is away
    /// from the disc's centre line — a runner on the right wall throws the arc out to the right — so the
    /// stream leaves the way the runner is leaning instead of cutting across the playfield.</summary>
    private const double BankArcBow = 0.9;

    /// <summary>Curve on the shrink. Above 1 keeps an orb near full size for most of the flight and collapses
    /// it into the counter at the end — so it reads as being swallowed by the readout, where a linear shrink
    /// reads as the orb receding on its own and arriving as an afterthought.</summary>
    private const double BankOrbShrinkExponent = 2.6;

    /// <summary>The banked orbs fly from the runner to the readout's orb, staggered, each on a slight outward arc, so
    /// the gate visibly pays the score. Runs inside the empty section lead.</summary>
    private static void DrawBankFlight(DrawingContext dc, Point c, double S, Internode g, Point target, double orb)
    {
        if (g.BankFlight.Count == 0) return;
        var centre = Centre(c, S, ZLead);
        double ring = R(ZLead) * S;
        var from = Polar(centre, ring * (1 - g.H), Math.PI - g.Theta);
        double travel = Math.Max(1e-3, InternodeTuning.BankSeconds), stagger = Math.Max(0, InternodeTuning.BankStaggerSeconds);
        // The chord's perpendicular, signed so it points away from the disc's centre line; a runner dead
        // centre bows to the side the flight's own spread would have taken it.
        double nx = target.Y - from.Y, ny = -(target.X - from.X);
        double side = Math.Sign(from.X - c.X);
        int i = 0;
        foreach (var b in g.BankFlight)
        {
            double t = (b.Age - stagger * i++) / travel;
            if (t <= 0 || t >= 1) continue;
            double e = t * t * (3 - 2 * t);
            double h = Hash01(b.Seed) - 0.5;
            double outward = side != 0 ? side : (h < 0 ? -1 : 1);
            double dir = (nx >= 0 ? 1 : -1) * outward;
            double bow = (BankArcBow + 0.25 * h) * dir;
            var mid = new Point((from.X + target.X) / 2 + nx * bow, (from.Y + target.Y) / 2 + ny * bow);
            double u = 1 - e;
            var p = new Point(u * u * from.X + 2 * u * e * mid.X + e * e * target.X, u * u * from.Y + 2 * u * e * mid.Y + e * e * target.Y);
            // Big out of the runner, shrinking into the counter — the shrink lags the flight, so the collapse
            // happens at the readout rather than being spent on the way over.
            double shrink = Math.Pow(e, BankOrbShrinkExponent);
            DrawOrb(dc, p, orb * (BankOrbStartScale + (BankOrbEndScale - BankOrbStartScale) * shrink), false, bonus: b.Gold);
        }
    }

    /// <summary>How far the readout's plate reaches above its text, in field radii, so its top edge lies past
    /// the disc's rim and the plaque reads as hanging down from the top of the playfield rather than
    /// floating on it. The text itself does not move.</summary>
    private const double PlaqueHang = ArcadeChrome.PlateHang;

    /// <summary>Stacked centred lines on a plate sized from the measured ink, the first line indented by
    /// <paramref name="lead"/> for a pictogram whose slot is returned. Left-aligned text with geometry bounds,
    /// never TextAlignment.Center: a centre-aligned FormattedText centres inside a box that is not its Width,
    /// and plate and glyphs would then derive from two different numbers.</summary>
    private static Rect DrawPlaque(DrawingContext dc, double cx, double top, FormattedText[] lines, double lead,
                                   double rowGap, double padX, double padY, out Point leadSlot, double above = 0,
                                   FormattedText? insert = null, double insertHeight = 0,
                                   FormattedText? footer = null, double footerHeight = 0, double footerScale = 1)
        => DrawPlaque(dc, cx, top, lines, lead, rowGap, padX, padY, out leadSlot, out _, above, insert, insertHeight, footer, footerHeight, footerScale);

    /// <param name="footer">A row that opens at the bottom of the plate, the mirror of <paramref name="insert"/>:
    /// the plate's bottom slides down by <paramref name="footerHeight"/> and the row is clipped to that band. The
    /// rows above do not move. <paramref name="footerScale"/> swells the row about its own centre; its centre
    /// once open is returned in <paramref name="footerSlot"/> for a glow to sit under.</param>
    private static Rect DrawPlaque(DrawingContext dc, double cx, double top, FormattedText[] lines, double lead,
                                   double rowGap, double padX, double padY, out Point leadSlot, out Point footerSlot, double above,
                                   FormattedText? insert, double insertHeight, FormattedText? footer, double footerHeight, double footerScale)
    {
        var bounds = new Rect[lines.Length];
        double width = 0, height = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            bounds[i] = lines[i].BuildGeometry(new Point()).Bounds;
            if (bounds[i].IsEmpty) bounds[i] = new Rect(0, 0, 0, lines[i].Height);
            width = Math.Max(width, bounds[i].Width + (i == 0 ? lead : 0));
            height += bounds[i].Height + (i > 0 ? rowGap : 0);
        }
        // An inserted row grows the plate from the top by insertHeight and pushes every row (and the plate's
        // bottom) down by it; the insert itself is clipped to that band, so it slides in as the band opens.
        Rect insertInk = default, footerInk = default;
        if (insert is not null) { insertInk = insert.BuildGeometry(new Point()).Bounds; width = Math.Max(width, insertInk.Width); }
        if (footer is not null) { footerInk = footer.BuildGeometry(new Point()).Bounds; width = Math.Max(width, footerInk.Width * footerScale); }
        // The corner radius is pinned to the resting rows: an open insert or footer band must not round the
        // corners further as it grows the plate.
        var plate = ArcadeChrome.DrawHangingPlate(dc, new Rect(cx - width / 2 - padX, top, width + padX * 2, height + padY * 2 + insertHeight + footerHeight),
                                                  above, InternodePalette.Plaque, InternodePalette.PlaquePen, height + padY * 2);
        if (insert is not null && insertHeight > 0.5)
        {
            dc.PushClip(new RectangleGeometry(new Rect(plate.X, top, plate.Width, insertHeight)));
            double band = insertInk.Height + padY;   // the row's full height once open
            dc.DrawText(insert, new Point(cx - insertInk.Width / 2 - insertInk.X, top + insertHeight - band + padY * 0.9 - insertInk.Y));
            dc.Pop();
        }

        double y = top + padY + insertHeight;
        leadSlot = new Point(0, 0);
        for (int i = 0; i < lines.Length; i++)
        {
            double rowWidth = bounds[i].Width + (i == 0 ? lead : 0);
            double x0 = cx - rowWidth / 2;
            if (i == 0) leadSlot = new Point(x0, y + bounds[i].Height / 2);
            dc.DrawText(lines[i], new Point(x0 + (i == 0 ? lead : 0) - bounds[i].X, y - bounds[i].Y));
            y += bounds[i].Height + rowGap;
        }

        // The footer band starts where the rows end (y is past the last rowGap; back it off) and is clipped so
        // the row slides down into view as the plate's bottom drops, then back up out of it.
        double footerTop = top + padY + insertHeight + height + padY - padY * 0.35;
        footerSlot = new Point(cx, footerTop + footerHeight / 2);
        if (footer is not null && footerHeight > 0.5)
        {
            // The row hangs well above the plate's bottom once open: its descenders clear the edge
            // and the plate reads as having grown a row, not as clipping one.
            var at = new Point(cx, footerTop + footerHeight - padY * 1.6 - footerInk.Height / 2);
            dc.PushClip(new RectangleGeometry(new Rect(plate.X, footerTop, plate.Width, footerHeight)));
            if (Math.Abs(footerScale - 1) > 1e-3) dc.PushTransform(new ScaleTransform(footerScale, footerScale, at.X, at.Y));
            ArcadeChrome.DrawInkCentered(dc, footer, at, footerInk);
            if (Math.Abs(footerScale - 1) > 1e-3) dc.Pop();
            dc.Pop();
            footerSlot = at;
        }
        return plate;
    }

    // ── Bonus toasts ──────────────────────────────────────────────────────────

    /// <summary>The bank's bonus toasts — first try/streak and perfect — on plates in the left half of the disc
    /// about halfway down, stacked no-miss over nailed it when both apply, so neither sits over the centre
    /// where the runner's next event is. Drawn in the fixed frame, after the roll is popped.</summary>
    private static void DrawBonusToasts(DrawingContext dc, Point c, double S, Internode g, double ppd)
    {
        double life = InternodeTuning.BonusToastSeconds;
        if (g.BonusToastAge >= life) return;
        bool nailed = g.FirstTryStreak > 0, noMiss = g.PerfectCombo > 0;
        if (!nailed && !noMiss) return;
        double size = ArcadeChrome.Ui(Math.Max(9, S * 0.048));
        double padX = size * 0.55, padY = size * 0.30, gutter = size * 0.35, x = c.X - S * 0.48;
        // In over the first 0.2 s (rising a little), hold, out over the last 0.3 s.
        double a = Math.Min(1, Math.Min(g.BonusToastAge / 0.2, (life - g.BonusToastAge) / 0.3));
        double rise = S * 0.03 * (1 - Math.Min(1, g.BonusToastAge / 0.2));

        // "Nailed it: 2x" — the label in the plaque ink, the multiplier highlighted yellow over an orange drop shadow.
        FormattedText? label = null, mult = null;
        if (nailed)
        {
            label = ArcadeChrome.Text(Loc.T(g.FirstTryStreak == 1 ? UiText.Arcade.NailedIt : UiText.Arcade.NailedItStreak), size, InternodePalette.Ink, ppd, TextAlignment.Left);
            string m = g.LastMultiplier.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
            mult = ArcadeChrome.Text(Loc.F(UiText.Arcade.Multiplier, m), size * 1.15, InternodePalette.ToastHighlight, ppd, TextAlignment.Left);
        }
        // "No-Miss Bonus" with a cluster of gold tokens on the right.
        FormattedText? miss = noMiss
            ? ArcadeChrome.Text(Loc.T(g.PerfectCombo == 1 ? UiText.Arcade.NoMissBonus : UiText.Arcade.NoMissStreakBonus), size, InternodePalette.Ink, ppd, TextAlignment.Left)
            : null;
        double tokenR = size * 0.34, cluster = tokenR * 4.2;

        double h1 = label is null ? 0 : Math.Max(label.Height, mult!.Height) + padY * 2;
        double h2 = miss is null ? 0 : Math.Max(miss.Height, tokenR * 2.6) + padY * 2;
        double stackH = h1 + h2 + (h1 > 0 && h2 > 0 ? gutter : 0);
        double top = c.Y - stackH / 2 + rise;
        dc.PushOpacity(Math.Clamp(a, 0, 1));
        // ⚠ No-miss on top, nailed it under it, when both fire. The stack height above already counts both,
        // so the order here is the only thing that decides it — keep the two blocks in this sequence and the
        // `top` advance with the upper one.
        if (miss is not null)
        {
            double w = miss.Width + size * 0.35 + cluster;
            var body = new Rect(x - w / 2 - padX, top, w + padX * 2, h2);
            ArcadeChrome.DrawHangingPlate(dc, body, 0, InternodePalette.Plaque, InternodePalette.PlaquePen);
            dc.DrawText(miss, new Point(body.X + padX, top + (h2 - miss.Height) / 2));
            // Three gold tokens, the board's own art, in a little heap on the right.
            double cx = body.X + padX + miss.Width + size * 0.35 + cluster / 2, cy = top + h2 / 2;
            DrawOrb(dc, new Point(cx - tokenR * 1.1, cy + tokenR * 0.45), tokenR, hot: false, bonus: true);
            DrawOrb(dc, new Point(cx + tokenR * 1.1, cy + tokenR * 0.45), tokenR, hot: false, bonus: true);
            DrawOrb(dc, new Point(cx, cy - tokenR * 0.55), tokenR, hot: false, bonus: true);
            top += h2 + gutter;
        }
        if (label is not null)
        {
            double w = label.Width + size * 0.35 + mult!.Width;
            var body = new Rect(x - w / 2 - padX, top, w + padX * 2, h1);
            ArcadeChrome.DrawHangingPlate(dc, body, 0, InternodePalette.Plaque, InternodePalette.PlaquePen);
            double lx = body.X + padX, ly = top + (h1 - label.Height) / 2;
            dc.DrawText(label, new Point(lx, ly));
            var mp = new Point(lx + label.Width + size * 0.35, top + (h1 - mult.Height) / 2);
            double drop = Math.Max(1, size * 0.09);
            mult.SetForegroundBrush(InternodePalette.ToastShadow);
            dc.DrawText(mult, new Point(mp.X + drop, mp.Y + drop));
            mult.SetForegroundBrush(InternodePalette.ToastHighlight);
            dc.DrawText(mult, mp);
        }
        dc.Pop();
    }

    // ── Shouts ────────────────────────────────────────────────────────────────

    private static void DrawShoutIfAny(DrawingContext dc, Point c, double S, Internode g, double ppd)
    {
        if (g.Shout == InternodeShout.None || g.ShoutLeft <= 0) return;
        double total = Math.Max(1e-3, InternodeTuning.ShoutSeconds);
        double age = total - g.ShoutLeft;
        double pop = age < 0.16 ? 1.35 - 0.35 * (age / 0.16) : 1.0 + Math.Sin(age * Math.PI) * 0.05;
        double alpha = Math.Clamp(g.ShoutLeft / 0.34, 0, 1);
        double rise = S * 0.10 * (age / total);
        (string text, Color ink, double size) = g.Shout switch
        {
            InternodeShout.Pass => (Loc.T(UiText.Arcade.Pass), InternodePalette.GateGoldColour, 0.13),
            InternodeShout.Stage => (Loc.F(UiText.Arcade.StageN, g.ShoutValue), InternodePalette.ShoutStage, 0.12),
            InternodeShout.ShortBy => (Loc.F(UiText.Arcade.ShortBy, g.ShoutValue), InternodePalette.ShoutFail, 0.11),
            _ => (Loc.T(UiText.Arcade.Ouch), InternodePalette.InkColour, 0.12),
        };
        // ⚠ At the disc centre, never up under the plaque: the top of the board belongs to the readout and the
        // phrase-name plate below it, and a shout placed there covers both. The run keeps moving underneath, so
        // a shout keeps its own size rather than the HUD scale, and drifts up a tenth of the disc as it fades.
        ArcadeChrome.DrawShout(dc, new Point(c.X, c.Y - rise), S, text, ink, age, pop, alpha, size, ppd,
                               ArcadeChrome.ShoutPaint.Quantised);
    }

    private static void Ring(DrawingContext dc, Point p, double radius, double width, Color colour, double alpha) =>
        ArcadeChrome.DrawRing(dc, p, radius, width, colour, alpha);

    // ── Cards ─────────────────────────────────────────────────────────────────

    private static void DrawIntro(DrawingContext dc, Point c, double field, double ppd)
    {
        dc.DrawEllipse(InternodePalette.Scrim, null, c, field, field);
        ArcadeChrome.DrawCentered(dc, "INTERNODE", ArcadeChrome.Ui(Math.Max(16, field * 0.12)), InternodePalette.Ink,
            c.X, c.Y - field * 0.46, ppd, field * 1.5);
        ArcadeChrome.DrawCentered(dc, $"{ControllerButtons.Text(PadButton.Triangle)}  {Loc.T(UiText.Arcade.HowToPlay)}",
            ArcadeChrome.Ui(Math.Max(9, field * 0.052)), InternodePalette.Accent, c.X, c.Y - field * 0.08, ppd, field * 1.6);
        ArcadeChrome.DrawCentered(dc, $"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Begin)}",
            ArcadeChrome.Ui(Math.Max(9, field * 0.048)), InternodePalette.InkDim, c.X, c.Y + field * 0.16, ppd, field * 1.5);
    }

    // ── How-to illustrations ──────────────────────────────────────────────────
    // Drawn with the board's own sprites and checker so the card teaches the shapes the player will see. The
    // box is about 38 px at the smallest playfield, so only bold shapes survive.

    public void DrawHowToArt(DrawingContext dc, Rect box, string art, double ppd)
    {
        var c = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
        double r = Math.Min(box.Width, box.Height) / 2;
        var clip = new EllipseGeometry(c, r, r);
        clip.Freeze();
        dc.PushClip(clip);
        switch (art)
        {
            case "swing":
            {
                dc.PushTransform(new RotateTransform(10, c.X, c.Y));
                MiniPipe(dc, c, r, out var centre, out double ring);
                MiniRunner(dc, centre, ring, 60 * InternodePhysics.Deg, 0, r * 0.20);
                dc.Pop();
                var from = new Point(c.X - r * 0.40, c.Y - r * 0.05);
                var to = new Point(c.X + r * 0.20, c.Y - r * 0.05);
                dc.DrawLine(InternodePalette.Stroke(InternodePalette.AccentInk, Math.Max(1.5, r * 0.09)), from, to);
                dc.DrawEllipse(InternodePalette.Accent, null, to, r * 0.10, r * 0.10);
                break;
            }
            case "mine":
            {
                MiniPipe(dc, c, r, out var centre, out double ring);
                var mineAt = Polar(centre, ring, Math.PI - 32 * InternodePhysics.Deg);
                DrawMine(dc, mineAt, r * 0.16, 0);
                for (int k = 0; k < 3; k++)
                {
                    var p = new Point(mineAt.X + (k - 1) * r * 0.16, mineAt.Y - r * (0.28 + 0.08 * (k == 1 ? 1 : 0)));
                    dc.DrawEllipse(InternodePalette.OrbHot, null, p, r * 0.05, r * 0.05);
                }
                MiniRunner(dc, centre, ring, -22 * InternodePhysics.Deg, 0, r * 0.20);
                break;
            }
            case "gate":
            {
                MiniPipe(dc, c, r, out var centre, out double ring);
                DrawGateRing(dc, centre, ring * 0.72, 0, RimRad, Math.Max(1.5, r * 0.07), 1);
                MiniRunner(dc, centre, ring, 0, 0, r * 0.20);
                break;
            }
            case "gold":
            {
                // A run of orbs with the last one gold-ringed: the perfect-pattern payoff the line describes.
                MiniPipe(dc, c, r, out var centre, out double ring);
                // The board's own token art, the last one wearing its gold ring, through DrawOrb.
                for (int k = 0; k < 4; k++)
                {
                    var p = new Point(centre.X - r * 0.36 + k * r * 0.24, centre.Y - ring * 0.55 + Math.Abs(k - 1.5) * r * 0.05);
                    DrawOrb(dc, p, r * 0.11, hot: false, bonus: k == 3);
                }
                MiniRunner(dc, centre, ring, 0, 0, r * 0.20);
                break;
            }
            case "bonus":
            {
                // The gate with its multiplier toast: what a first-try, no-miss pass pays.
                MiniPipe(dc, c, r, out var centre, out double ring);
                DrawGateRing(dc, centre, ring * 0.72, 0, RimRad, Math.Max(1.5, r * 0.07), 1);
                var tag = ArcadeChrome.Text("2×", ArcadeChrome.Ui(Math.Max(8, r * 0.34)), InternodePalette.GateGold, ppd, TextAlignment.Left);
                var ink = tag.BuildGeometry(new Point()).Bounds;
                var plate = new Rect(centre.X - ink.Width / 2 - r * 0.10, centre.Y - ring * 0.20 - ink.Height / 2 - r * 0.06, ink.Width + r * 0.20, ink.Height + r * 0.12);
                dc.DrawRoundedRectangle(InternodePalette.Plaque, InternodePalette.PlaquePen, plate, plate.Height * 0.3, plate.Height * 0.3);
                ArcadeChrome.DrawInkCentered(dc, tag, new Point(centre.X, centre.Y - ring * 0.20), ink);
                MiniRunner(dc, centre, ring, 0, 0, r * 0.20);
                break;
            }
        }
        dc.Pop();
    }

    /// <summary>A mini cross-section framed on the bottom of the pipe: sky, fog, three concentric checkered
    /// rings and the seams. Returns the runner ring's centre and pixel radius.</summary>
    private static void MiniPipe(DrawingContext dc, Point c, double r, out Point centre, out double ring)
    {
        double S = r * 1.25;
        centre = new Point(c.X, c.Y - r * 0.25);
        ring = S * 0.72;
        dc.DrawEllipse(InternodePalette.SkyFlat, null, centre, S * 1.1, S * 1.1);
        double fog = S * 0.38 * 2.2;
        dc.DrawEllipse(InternodePalette.FogGlow, null, centre, fog, fog);
        double start = Math.PI - RimRad, span = 2 * RimRad;
        ReadOnlySpan<double> radii = [1.06, 0.72, 0.52, 0.38];
        ReadOnlySpan<int> ramps = [0, 3, 7, 11];
        for (int i = 2; i >= 0; i--)
        {
            double ra = radii[i] * S, rb = radii[i + 1] * S;
            for (int j = 0; j < SectorsPerRing; j++)
            {
                double a0 = start + span * j / SectorsPerRing, a1 = start + span * (j + 1) / SectorsPerRing;
                // The card always shows the first theme: it teaches the shape, not whatever tone the run is on.
                var fill = (((i + j) & 1) == 0 ? InternodePalette.CheckA : InternodePalette.CheckB)[0][ramps[i]];
                dc.DrawGeometry(fill, null, Sector(centre, ra, a0, a1, centre, rb, a0, a1));
            }
        }
        for (int side = -1; side <= 1; side += 2)
        {
            double a = Math.PI - side * RimRad;
            dc.DrawLine(InternodePalette.SplitEdgeFine, Polar(centre, radii[3] * S, a), Polar(centre, radii[0] * S, a));
        }
    }

    private static void MiniRunner(DrawingContext dc, Point centre, double ring, double theta, double h, double scale)
    {
        double a = Math.PI - theta;
        dc.PushTransform(Placement(scale, a * 180 / Math.PI + 180, Polar(centre, ring * (1 - h), a)));
        // The card shows the bike the board shows: the centre lean of the runner art, placed exactly as DrawRunner
        // places it (liveWash, because this is inside the unit frame). The vector figure is the no-art fallback.
        if (ArcadeSprites.Frame(ArcadeSprites.Slot.InternodeRunner[ArcadeSprites.Slot.RunnerCentre], 0, ArcadeSprites.RunnerFps) is { } bike)
        {
            ArcadeSprites.DrawStanding(dc, bike, new Point(0, RunnerArtFootY), RunnerArtHeight, liveWash: true);
            dc.Pop();
            return;
        }
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerTorso);
        dc.DrawGeometry(InternodePalette.RunnerTrim, null, RunnerTrim);
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerArms);
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerFeet);
        dc.DrawGeometry(InternodePalette.Runner, null, RunnerHead);
        dc.DrawGeometry(null, InternodePalette.CelUnit, RunnerTorso);
        dc.DrawGeometry(null, InternodePalette.CelUnit, RunnerHead);
        dc.Pop();
    }

    // ── Sprites: frozen unit geometry, one matrix per instance ───────────────

    /// <summary>Scale, rotate (degrees, clockwise), then translate to <paramref name="p"/>.</summary>
    private static MatrixTransform Placement(double scale, double rotationDeg, Point p)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(rotationDeg);
        m.Translate(p.X, p.Y);
        var t = new MatrixTransform(m);
        t.Freeze();
        return t;
    }

    private static T Frozen<T>(T f) where T : Freezable => ArcadePalette.Frozen(f);

    /// <summary>Six spikes; unit radius at the tips.</summary>
    private static readonly Geometry MineUnit = BuildStar(6, 1.0, 0.56);
    private static readonly Geometry MineTipsUnit = BuildDots(6, 0.90, 0.17);
    private static readonly Geometry MineCoreUnit = Frozen(new EllipseGeometry(new Point(0, 0), 0.30, 0.30));

    // The runner from behind, feet at the origin, head at y = -2 (up is -y before placement).
    private static readonly Geometry RunnerTorso = Frozen(new RectangleGeometry(new Rect(-0.48, -1.35, 0.96, 1.17), 0.42, 0.42));
    private static readonly Geometry RunnerTrim = Frozen(new RectangleGeometry(new Rect(-0.60, -0.82, 1.20, 0.20)));
    private static readonly Geometry RunnerHead = Frozen(new EllipseGeometry(new Point(0, -1.62), 0.34, 0.34));
    private static readonly Geometry RunnerArms = Frozen(new GeometryGroup
    {
        Children =
        {
            new EllipseGeometry(new Point(-0.64, -0.92), 0.17, 0.42),
            new EllipseGeometry(new Point(0.64, -0.92), 0.17, 0.42),
        },
    });
    private static readonly Geometry RunnerFeet = Frozen(new GeometryGroup
    {
        Children =
        {
            new EllipseGeometry(new Point(-0.30, -0.10), 0.30, 0.17),
            new EllipseGeometry(new Point(0.30, -0.10), 0.30, 0.17),
        },
    });

    private static Geometry BuildStar(int spikes, double outer, double inner)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (int k = 0; k < spikes * 2; k++)
            {
                double a = k * Math.PI / spikes;
                double rr = (k & 1) == 0 ? outer : inner;
                var p = new Point(Math.Sin(a) * rr, -Math.Cos(a) * rr);
                if (k == 0) ctx.BeginFigure(p, true, true); else ctx.LineTo(p, true, true);
            }
        }
        geo.Freeze();
        return geo;
    }

    private static Geometry BuildDots(int count, double radius, double dot)
    {
        var group = new GeometryGroup();
        for (int k = 0; k < count; k++)
        {
            double a = k * Math.PI * 2 / count;
            group.Children.Add(new EllipseGeometry(new Point(Math.Sin(a) * radius, -Math.Cos(a) * radius), dot, dot));
        }
        group.Freeze();
        return group;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static Point Polar(Point c, double radius, double angle) => ArcadePalette.Polar(c, radius, angle);

    private static double Hash01(int n)
    {
        uint x = (uint)n * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
        return (x & 0xFFFFFF) / 16777216.0;
    }
}
