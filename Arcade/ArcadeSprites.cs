using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>Drop-in bitmap art for the arcade games. A renderer asks for a slot by name; if art exists the
/// renderer draws it instead of (or over) its vector body, and if it does not the vector body stands alone.
/// <b>Every slot is optional</b> — a game with no art at all looks exactly as it did before this file.
///
/// <para><b>Two homes, override first.</b> <c>%APPDATA%\Radiata\arcade-sprites\&lt;slot&gt;.png</c> wins over
/// the packed <c>Assets/arcade/sprites/sprites-finished/&lt;slot&gt;.png</c>, so art can be swapped without a
/// rebuild. Finished art is the only thing in that folder; guides live beside it in
/// <c>templates-unfinished/</c> and never ship. The
/// override folder is re-read when the arcade window opens (<see cref="Reload"/>) — drop a file in, dismiss,
/// reopen. Nothing here re-probes from the render pump: a resolved slot, a miss included, is latched.</para>
///
/// <para><b>Slot names are the contract with the artist</b> — see <see cref="Slot"/> and
/// <c>Assets/arcade/sprites/README.md</c>. Transparent PNG, square unless the slot says otherwise; the
/// drawing helpers preserve aspect, so art that is not the nominal shape is letterboxed rather than
/// stretched.</para></summary>
internal static class ArcadeSprites
{
    /// <summary>Every replaceable slot, as the file name (without <c>.png</c>) the artist supplies.
    ///
    /// <para>A slot may also ship as a numbered cycle — <c>&lt;slot&gt;-1.png</c>, <c>-2</c>, … — read by
    /// <see cref="Frame"/>. A cycle wins over the single file of the same name.</para></summary>
    public static class Slot
    {
        /// <summary>The runner's nine lean states, hardest left to hardest right, chosen by lateral
        /// (angular) speed. Art is fitted by height with the wheels on the floor line, so the states may
        /// differ in width. A missing state falls back to <c>internode-runner-c</c>, and that to the vector
        /// runner — so a three-state set can be supplied as l2/c/r2 and still read.
        ///
        /// <para>Each state is a cycle — <c>internode-runner-c-1.png</c>, <c>-2.png</c> — alternating at
        /// <see cref="RunnerFps"/> to spin the tyre. Two frames is the intended set; more is allowed and a
        /// single unnumbered file is a still bike.</para></summary>
        public static readonly string[] InternodeRunner =
            ["internode-runner-l3", "internode-runner-l2", "internode-runner-l1", "internode-runner-l0",
             "internode-runner-c",
             "internode-runner-r0", "internode-runner-r1", "internode-runner-r2", "internode-runner-r3"];

        /// <summary>Index of the upright state in <see cref="InternodeRunner"/>.</summary>
        public const int RunnerCentre = 4;

        public const string InternodeRunnerJump = "internode-runner-jump";
        public const string InternodeRunnerFall = "internode-runner-fall";

        /// <summary>The collectible: a four-frame loop, <c>internode-token-1.png</c> … <c>-4.png</c>, played
        /// at <see cref="CycleFps"/> and offset per token so a row does not flash in lockstep. Any frame
        /// count works; a single unnumbered file is a still token.</summary>
        public const string InternodeToken       = "internode-token";
        /// <summary>The gold token, its own loop. Falls back to <see cref="InternodeToken"/> tinted gold.</summary>
        public const string InternodeTokenBonus  = "internode-token-bonus";
        /// <summary>The one that got past you. Falls back to <see cref="InternodeToken"/> drained grey.</summary>
        public const string InternodeTokenMissed = "internode-token-missed";
        /// <summary>The sky behind the pipe. Tiled, and panned and rolled by how far the course has turned the
        /// camera, so a bend or a corkscrew moves the world outside the pipe rather than only the pipe.
        ///
        /// <para>⚠ It must tile on both axes — a bend goes either way and a run is unbounded, so every edge
        /// meets its opposite eventually. Nothing anchors a horizon; there is no up.</para>
        ///
        /// <para>The far end of the pipe is still faded to black over the top of it. That is not decoration:
        /// it is what keeps the vanishing point reading as depth rather than as a disc hung on a backdrop.</para></summary>
        public const string InternodeSky = "internode-sky";

        /// <summary>The hazard: two frames that cross-fade, A into B and back, at <see cref="PulseFps"/> —
        /// a throb, not a flipbook. Drawn through <see cref="Blend"/> rather than <see cref="Frame"/>.</summary>
        public const string InternodeMine        = "internode-mine";

        /// <summary>The double jump's exhaust burst: one radial frame, drawn centred on each exhaust port,
        /// grown out and faded over the flare's life and spun a little. Radially symmetric art with no up.
        /// Without it the procedural ring-and-darts burst draws.</summary>
        public const string InternodeJetBurst    = "internode-jetburst";

        // ⚠ Petalpop takes no art: the flat cel drawing is the intended look, not a fallback. The core, ball
        // and paddle slots and the three paddle bend stages do not exist, and PetalPopRenderer asks
        // ArcadeSprites for nothing. Do not reintroduce a slot: a stray PNG would silently override the
        // drawing again.

        /// <summary>A petal face, clipped to that petal's own polygon and turned so the art's up runs from
        /// the floret's hub outward — one drawing serves all five petals of every floret at every angle.
        ///
        /// <para>The grid edge is re-stroked over the art: the outline is how a crop reads as cells at all,
        /// and art may not swallow it. The numeral, flag and bee are a later pass and sit on top
        /// regardless.</para></summary>
        public const string KabloomPetal        = "kabloom-petal";
        /// <summary>The other half of the covered checker. Falls back to <see cref="KabloomPetal"/>, which
        /// simply retires the checker.</summary>
        public const string KabloomPetalAlt     = "kabloom-petal-alt";
        /// <summary>The one bee you set off, on the failure board. With none supplied that cell takes the
        /// plain cleared fill like any other.
        /// ⚠ There is deliberately no slot for a cleared petal, focused or not: a cleared cell is the dark
        /// ground a clue number is read off, and it does that job better plain than under a drawing. Don't
        /// reintroduce one — the renderer returns null for that state on purpose.</summary>
        public const string KabloomPetalHit     = "kabloom-petal-hit";

        /// <summary>The petal the cursor is on, covered. Replaces both checker shades, so the selected cell
        /// is one face rather than two. Falls back to the ordinary covered face — the black outline and its
        /// orbiting sparks are drawn over the selection either way, so a board with no focus art is still
        /// unambiguous about where the cursor is.</summary>
        public const string KabloomPetalFocus        = "kabloom-petal-focus";

        public const string KabloomGem  = "kabloom-gem";

        /// <summary>The bee as a fact — the hazard you uncovered, and the swarm leaving a cleared board.
        /// A four-frame loop at <see cref="CycleFps"/>.
        ///
        /// <para>⚠ Not the flag. The player's "I think there is a bee here" marker keeps the vector glyph on
        /// purpose: a guess and a fact must not look alike, and drawn art would make them identical.</para></summary>
        public const string KabloomBee  = "kabloom-bee";

        /// <summary>The stung bee's cast shadow, one frame per <see cref="KabloomBee"/> frame. Generated, not
        /// drawn: <c>dotnet run --project tools/SpriteShadowBake</c> derives it from the bee's own alpha.
        ///
        /// <para>⚠ It is a derived asset, so redrawing the bee frames leaves it stale — and a stale shadow is
        /// the invisible kind of wrong, because it still draws, just under a body that has moved. Re-run the
        /// baker after any change to <see cref="KabloomBee"/>.</para>
        ///
        /// <para>The canvas is deliberately larger than the bee's (1.5× at present): the bee's wings touch
        /// x = 0 and x = 511 of their own canvas, so a blur confined to that frame would be sliced off in a
        /// straight line down both sides. The renderer is not told the factor — it reads the ratio of this
        /// bitmap's pixel width to the bee's and scales its draw box by it, so the silhouette lands back
        /// exactly on the body whatever padding the baker used. Keep the aspect equal to the bee's.</para>
        ///
        /// <para>⚠ The cycle must be the same length as the bee's. Both are asked for at one phase and one
        /// fps, so the frame index is shared and the shadow is its bee's frame; different lengths would run
        /// at different periods and slide. The renderer checks it and falls back rather than drift.</para>
        ///
        /// <para>Black with its own alpha falloff, drawn through a single opacity, so the gradient in the file
        /// is the shadow's shape.</para></summary>
        public const string KabloomBeeShadow = "kabloom-bee-shadow";

        /// <summary>A merge tile, replaced outright — art carries the family, so supply one per hue.
        ///
        /// <para>⚠ The two families and the blend are the merge rule itself: what a tile can join with is
        /// read off its colour and nothing else. Three sprites that do not separate at a glance, at tile size,
        /// break the game rather than restyle it.</para>
        ///
        /// <para>Resolution runs rank, then hue, then <see cref="ConnateOrb"/>. The numeral is drawn after
        /// and must stay readable over whatever the art is; the over-limit rim is stroked over it too.</para></summary>
        public const string ConnateOrbAzure = "connate-orb-azure";
        public const string ConnateOrbEmber = "connate-orb-ember";
        public const string ConnateOrbBlend = "connate-orb-blend";

        /// <summary>The fallback face for any hue with no art of its own. On its own it makes every family
        /// look alike, so it is only useful as a base while the set is being drawn.</summary>
        public const string ConnateOrb     = "connate-orb";

        /// <summary>The two lowest ranks, which are shapes rather than numbered discs: a star, and a disc
        /// with a star-shaped socket cut out of it at exactly the star's size. The star dropping into the
        /// socket is how the board says the two of them make the next rank.
        ///
        /// <para>⚠ Clipped to the generated outline, unlike every other Connate slot. The fit between the
        /// two has to stay exact whatever is drawn, so art is cut to the real star and the real socket —
        /// anything outside them, a glow or a soft edge, is lost. Draw to fill the shape.</para>
        ///
        /// <para>Per hue, resolved before the shared pair below.</para></summary>
        public const string ConnateStarAzure        = "connate-star-azure";
        public const string ConnateStarEmber        = "connate-star-ember";
        public const string ConnateStarOpeningAzure = "connate-star-opening-azure";
        public const string ConnateStarOpeningEmber = "connate-star-opening-ember";

        /// <summary>Shared fallbacks for the two shape ranks, for a family with no art of its own.</summary>
        public const string ConnateStar        = "connate-star";
        public const string ConnateStarOpening = "connate-star-opening";
        public const string ConnateBomb    = "connate-bomb";
        /// <summary>Clipped to the lump's own silhouette, which is also its collider.</summary>
        public const string ConnateGarbage = "connate-garbage";

        /// <summary>The whole playfield ground — outfield, rim band and well — as one picture, clipped to the
        /// field disc and drawn under everything. Replaces the vector ground fills only; every rule ring and
        /// dynamic overlay (limit, cushion, fuse, flood) is still drawn on top of it.</summary>
        public const string ConnateBackground = "connate-background";
        /// <summary>The clamps that hold the loaded piece, drawn over it and straddling its edge, at 2:30,
        /// 10:30 and 6:00 on the craft's own clock — 12:00 being the craft's nose, which points inward at
        /// the board. The clock turns with the craft, because the clamps are part of it.
        ///
        /// <para>Draw one as the <b>12 o'clock</b> clamp: sitting above the piece and gripping down at it.
        /// The renderer turns it to each position. A position may take its own art
        /// (<c>connate-clamp-0230</c>) and falls back to this.</para>
        ///
        /// <para>⚠ They belong to the craft, not to the piece — the loaded tile spins in place, and a clamp
        /// turning with it would not be holding anything.</para></summary>
        public const string ConnateClamp = "connate-clamp";

        /// <summary>Clock positions the clamps sit at, and the per-position slot for each. Degrees clockwise
        /// from the craft's outward direction.</summary>
        public static readonly (double Deg, string Slot)[] ConnateClamps =
            [(75, "connate-clamp-0230"), (285, "connate-clamp-1030"), (180, "connate-clamp-0600")];

        /// <summary>The launcher, rotated to its orbit angle. Art points up, i.e. nose toward the board — the way
        /// it shoots.
        ///
        /// <para>⚠ Frame <b>2</b> is the craft at rest, and what a player sees nearly all the time; holding
        /// the trigger takes it to frame <b>1</b> and keeps it there.</para>
        ///
        /// <para>⚠ Frame <b>3</b> is drawn but deliberately unused for the time being. The file stays (and
        /// stays embedded) so it can come back without an art pass; nothing reads index 2. If a third pose is
        /// wanted again, it belongs in one of the two orders below, not in a new one.</para></summary>
        public const string ConnateCraft   = "connate-craft";

        /// <summary>The craft in parts: a body and one wing, the wing drawn once as authored for the left and
        /// mirrored for the right, the body over both. When both are supplied they replace the pose frames
        /// of <see cref="ConnateCraft"/>; with either missing the frames are drawn instead.</summary>
        public const string ConnateCraftBody = "connate-craft-body";
        public const string ConnateCraftWing = "connate-craft-wing";

        /// <summary>The wind-up, as frame indices: 2 → 1, then wound on 1 for as long as the trigger is down.
        /// The order ends on the frame it holds, which is how <see cref="Once"/> knows what to settle on.</summary>
        public static readonly int[] ConnateCraftWind = [1, 0];

        /// <summary>The release: the rest pose, and nothing before it. The craft snaps back the instant the
        /// shot leaves rather than unwinding through the wind-up in reverse — played back up, an intermediate
        /// frame read as the craft re-cocking itself after a shot it had already taken.
        ///
        /// <para>⚠ One entry, so this length is no longer the grips' let-go window. That is
        /// <see cref="ConnateGripReleaseSeconds"/>; don't derive one from the other again.</para></summary>
        public static readonly int[] ConnateCraftShot = [1];

        /// <summary>How long the craft's grips stay open after a shot leaves, seconds. The craft's own pose
        /// snaps back instantly now, but the grips still need a beat visibly ajar or the shot reads as passing
        /// through a closed clamp.</summary>
        public const double ConnateGripReleaseSeconds = 0.125;

        /// <summary>A rank's own tile face, tried before <see cref="ConnateOrb"/>.</summary>
        public static string ConnateOrbRank(int rank) => "connate-orb-" + rank.ToString();
    }

    /// <summary>Frames per second for a numbered cycle.</summary>
    public const double CycleFps = 12;

    /// <summary>Steps per second for a one-shot played out of an event (<see cref="Once"/>). Fast: a launch
    /// is a snap, and three frames out and back land in about a sixth of a second.</summary>
    public const double ShotFps = 24;

    /// <summary>Steps per second for a cross-faded cycle (<see cref="Blend"/>). Far slower than
    /// <see cref="CycleFps"/>: these dissolve rather than flip, so two frames at this rate are a breath of
    /// about a second out and back, not a flicker.</summary>
    public const double PulseFps = 2;

    /// <summary>Frame rate for the runner's lean states. Faster than <see cref="CycleFps"/>: the two frames
    /// are a spinning tyre, and a tyre that reads as two pictures instead of one blur has failed.</summary>
    public const double RunnerFps = 20;

    /// <summary>How much of a token's colour cue survives onto sprite art: the height / hot / gold colour is
    /// washed over the sprite through its own alpha. 0 leaves the art untouched and drops the cue.</summary>
    public const double TokenTint = 0.34;

    /// <summary>Longest edge a sprite decodes to. Art beyond this is downscaled on load — the arcade disc is
    /// a few hundred pixels across, so nothing here needs more.</summary>
    private const int MaxEdgePx = 512;

    /// <summary>Highest frame a cycle may reach. A cycle stops at the first gap regardless.</summary>
    private const int MaxFrames = 64;

    /// <summary>⚠ Must match the csproj's sprite <c>Resource</c> glob exactly: this string is spliced into a
    /// <c>pack://</c> URI, so a mismatch is not a build error — every slot simply misses and every game
    /// quietly falls back to its vector body. The sibling <c>templates-unfinished/</c> is guide art and is
    /// deliberately not here.</summary>
    private const string PackedDir = "Assets/arcade/sprites/sprites-finished/";

    /// <summary>Where a replacement PNG goes to win over the packed one.</summary>
    public static string OverrideDir => Path.Combine(AppPaths.AppDataDir, "arcade-sprites");

    private static readonly Dictionary<string, BitmapSource?> Singles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, BitmapSource[]> Cycles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, Brush> Tints = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// <summary>Wall time driving every cycle. Deliberately not a game's presentation clock: the games do not
    /// share one, and a token's spin is decoration that may keep turning while the pause card is up.</summary>
    public static double Time => PinnedTime ?? Clock.Elapsed.TotalSeconds;

    /// <summary>Pins <see cref="Time"/> for the render-snapshot harness, so a sprite cycle's frame is a function
    /// of state alone. Null reads the real clock.</summary>
    internal static double? PinnedTime = null;

    /// <summary>Forget every resolved slot, so the next ask re-reads the override folder. Called when the
    /// arcade opens; that is the whole hot-swap story, and it is why nothing probes per frame.</summary>
    public static void Reload()
    {
        Singles.Clear();
        Cycles.Clear();
        // The bakes are keyed on the bitmap source; override art gets a fresh instance per open, so without
        // this every reopen added a generation of bakes (and pinned the old bitmaps) against a budget that
        // only ever went up, until the washes silently stopped.
        _washed.Clear();
        _washedBytes = 0;
        _washRetryAtMs = 0;
    }

    /// <summary>The art for a slot, or null if the artist has supplied none. Latched, miss included.</summary>
    public static BitmapSource? Get(string slot)
    {
        if (Singles.TryGetValue(slot, out var hit)) return hit;
        var bmp = FromOverride(slot) ?? ArcadeArt.LoadPacked(PackedDir + slot + ".png", $"sprite {slot}");
        Singles[slot] = bmp;
        return bmp;
    }

    /// <summary>Has the artist supplied anything for this slot — a cycle or a single file?</summary>
    public static bool Has(string slot) => Cycle(slot).Length > 0;

    /// <summary>The current frame of the first slot in <paramref name="slots"/> that has art, or null. For
    /// fallback chains — a missing runner lean state resolving to the upright one, say.</summary>
    public static BitmapSource? First(double phase, double fps, params string[] slots)
    {
        foreach (var s in slots) { var b = Frame(s, phase, fps); if (b is not null) return b; }
        return null;
    }

    /// <summary>A slot's numbered cycle (<c>&lt;slot&gt;-1.png</c> upward, stopping at the first gap), falling
    /// back to the single <c>&lt;slot&gt;.png</c> as a one-frame cycle. Empty when the artist supplied
    /// neither.</summary>
    public static BitmapSource[] Cycle(string slot)
    {
        if (Cycles.TryGetValue(slot, out var hit)) return hit;
        var frames = new List<BitmapSource>();
        for (int i = 1; i <= MaxFrames; i++)
        {
            var f = Get($"{slot}-{i}");
            if (f is null) break;
            frames.Add(f);
        }
        if (frames.Count == 0 && Get(slot) is { } single) frames.Add(single);
        var arr = frames.ToArray();
        Cycles[slot] = arr;
        return arr;
    }

    /// <summary>One frame of a slot's cycle, or null when it has none. <paramref name="phase"/> offsets the
    /// cycle (pass something stable per object — a token's index — so a row of them does not beat as one).</summary>
    public static BitmapSource? Frame(string slot, double phase = 0, double fps = CycleFps)
    {
        var frames = Cycle(slot);
        if (frames.Length == 0) return null;
        if (frames.Length == 1) return frames[0];
        double t = Time * Math.Max(0.01, fps) + phase * frames.Length;
        int i = (int)(t - Math.Floor(t / frames.Length) * frames.Length);
        return frames[Math.Clamp(i, 0, frames.Length - 1)];
    }

    /// <summary>A cycle played once from a clock that counts up out of an event, one step of
    /// <paramref name="order"/> per frame time, then held on the frame that order ends on. For a recoil or a
    /// stomp — something that happens rather than something that idles.
    ///
    /// <para><paramref name="order"/> is frame indices, 0-based, so a set whose resting pose is not its first
    /// file says so rather than being renamed. <paramref name="since"/> is seconds since the event, and must
    /// come from a clock the sim owns: a frozen game has to repaint the same frame forever. Null when the
    /// slot has no art.</para></summary>
    public static BitmapSource? Once(string slot, double since, int[] order, double fps = ShotFps)
    {
        var frames = Cycle(slot);
        if (frames.Length == 0) return null;
        // The resting pose is wherever the order ends, so a played-out animation and an untouched one are the
        // same picture — a one-shot that settled on a different frame from its own last step would jump.
        int rest = Math.Clamp(order.Length > 0 ? order[^1] : 0, 0, frames.Length - 1);
        // ⚠ Range-checked before the cast: callers pass infinity for "never fired", and (int) on that is a
        // negative number, not a large one — it would land mid-animation instead of at rest.
        double step = since * Math.Max(0.01, fps);
        if (frames.Length == 1 || order.Length == 0 || !(step >= 0) || step >= order.Length) return frames[rest];
        return frames[Math.Clamp(order[(int)step], 0, frames.Length - 1)];
    }

    /// <summary>A cycle as a cross-fade rather than a flipbook: the frame to draw, the one after it, and how
    /// far between them the slot has got, eased so it breathes instead of ramping. Two frames read as a
    /// pulse — out to B and back to A — and more frames as a dissolve round the loop.
    ///
    /// <para>False when the artist has supplied nothing. <paramref name="to"/> is null for a single file,
    /// which is then simply a still.</para></summary>
    public static bool Blend(string slot, out BitmapSource from, out BitmapSource? to, out double t,
                             double phase = 0, double fps = PulseFps)
    {
        from = null!; to = null; t = 0;
        var frames = Cycle(slot);
        if (frames.Length == 0) return false;
        from = frames[0];
        if (frames.Length == 1) return true;
        double u = Time * Math.Max(0.01, fps) + phase * frames.Length;
        u -= Math.Floor(u / frames.Length) * frames.Length;
        int i = Math.Clamp((int)u, 0, frames.Length - 1);
        from = frames[i];
        to = frames[(i + 1) % frames.Length];
        double f = Math.Clamp(u - i, 0, 1);
        t = f * f * (3 - 2 * f);
        return true;
    }

    /// <summary>How art is fitted to the box it is given.</summary>
    public enum Fit
    {
        /// <summary>Whole image inside the box, letterboxed. For a free-standing sprite.</summary>
        Contain,
        /// <summary>Box entirely covered, art cropped. For an overlay a clip will trim anyway.</summary>
        Cover,
    }

    /// <summary>Draw art into <paramref name="box"/>, aspect preserved and centred. Returns the rectangle it
    /// actually occupied — hand that to <see cref="Tint"/>. Empty when nothing was drawn.</summary>
    public static Rect Draw(DrawingContext dc, BitmapSource? bmp, Rect box, Fit fit = Fit.Contain,
                            double opacity = 1)
    {
        if (bmp is null || opacity <= 0.004) return Rect.Empty;
        if (box.Width <= 0.5 || box.Height <= 0.5) return Rect.Empty;
        double bw = bmp.Width, bh = bmp.Height;
        if (bw <= 0 || bh <= 0) return Rect.Empty;
        double k = fit == Fit.Cover ? Math.Max(box.Width / bw, box.Height / bh)
                                    : Math.Min(box.Width / bw, box.Height / bh);
        var r = new Rect(box.X + (box.Width - bw * k) / 2, box.Y + (box.Height - bh * k) / 2, bw * k, bh * k);
        bool fade = opacity < 0.999;
        if (fade) dc.PushOpacity(opacity);
        dc.DrawImage(bmp, r);
        if (fade) dc.Pop();
        return r;
    }

    /// <summary>Draw art standing on <paramref name="bottomCentre"/> at a fixed <paramref name="height"/>,
    /// its width following the art's own aspect. For a figure with a floor under it: the states of a
    /// character need not share a width, but their feet must all land on the same line.</summary>
    /// <param name="darken">Black washed over the art through its own alpha, 0 to 1 — for a hit flash that
    /// goes dark rather than see-through. Baked and cached by <see cref="DrawWashed"/>, so a flicker costs one
    /// <c>DrawImage</c> a frame rather than an opacity mask.</param>
    /// <param name="liveWash">Apply <paramref name="darken"/> with a live opacity mask instead of the bake.
    /// ⚠ Required under a scaling transform: the bake sizes itself from the rect it is handed, and a caller
    /// drawing in its own unit frame hands it a rect a few units tall, so the bake comes out at
    /// <see cref="WashMinEdge"/> pixels and the transform blows it up into a blur. One masked draw per frame
    /// for a single large figure is cheap; the bake is for the many small sprites.</param>
    public static Rect DrawStanding(DrawingContext dc, BitmapSource? bmp, Point bottomCentre, double height,
                                    double opacity = 1, double darken = 0, bool liveWash = false)
    {
        if (bmp is null || height <= 0 || bmp.Height <= 0) return Rect.Empty;
        double width = height * (bmp.Width / bmp.Height);
        var box = new Rect(bottomCentre.X - width / 2, bottomCentre.Y - height, width, height);
        if (darken <= 0.004) return Draw(dc, bmp, box, Fit.Contain, opacity);
        if (!liveWash) return DrawWashed(dc, bmp, box, Colors.Black, 0, darken, opacity);
        Rect drawn = Draw(dc, bmp, box, Fit.Contain, opacity);
        Tint(dc, bmp, drawn, Colors.Black, darken);
        return drawn;
    }


    /// <summary>Draw art centred on and clipped to <paramref name="shape"/> — the pattern Petalpop's core
    /// uses: the vector body keeps the silhouette and the state colour, the art fills it. Sized to cover the
    /// shape's bounds times <paramref name="cover"/>, so art larger than 1 crops harder.</summary>
    public static void DrawClipped(DrawingContext dc, BitmapSource? bmp, Geometry shape, double cover = 1,
                                   double opacity = 1)
    {
        if (bmp is null) return;
        var b = shape.Bounds;
        if (b.IsEmpty || b.Width <= 1 || b.Height <= 1) return;
        var centre = new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
        dc.PushClip(shape);
        Draw(dc, bmp, Box(centre, b.Width / 2 * cover, b.Height / 2 * cover), Fit.Cover, opacity);
        dc.Pop();
    }

    /// <summary>A box of <paramref name="halfW"/> × <paramref name="halfH"/> about a point. Half-extents,
    /// because every call site here already thinks in radii.</summary>
    public static Rect Box(Point p, double halfW, double halfH) =>
        new(p.X - halfW, p.Y - halfH, halfW * 2, halfH * 2);

    public static Rect Box(Point p, double half) => Box(p, half, half);

    /// <summary>Wash a colour over art through the art's own alpha, so a state the vector drawing carried in
    /// its fill (a token's height hue, a miss, a gold prize) still reads on a replacement sprite.
    /// <paramref name="drawn"/> is what <see cref="Draw"/> returned.</summary>
    public static void Tint(DrawingContext dc, BitmapSource? bmp, Rect drawn, Color colour, double alpha)
    {
        if (bmp is null || alpha <= 0.004 || drawn.IsEmpty || drawn.Width <= 0.5) return;
        dc.PushOpacityMask(ArcadeArt.BrushOf(bmp));
        dc.PushOpacity(Math.Min(1, alpha));
        dc.DrawRectangle(TintBrush(colour), null, drawn);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>Draw art with a colour wash and a distance darkening already burnt in, from a cache — one
    /// <c>DrawImage</c> and no opacity mask.
    ///
    /// <para>⚠ Prefer this to <see cref="Draw"/> + <see cref="Tint"/> on any path that runs per object per
    /// frame. <see cref="Tint"/> works by <c>PushOpacityMask</c>, and every push makes WPF allocate an
    /// intermediate render surface for the masked region — a cost paid per call, not per pixel, so it lands
    /// hardest on the many small sprites rather than the few big ones, and a track carrying hundreds of them
    /// at once needs the bake to stay inside its frame budget.</para>
    ///
    /// <para>The washes are a pure function of (art, colour, wash strength, darkening), so the result caches.
    /// Both strengths quantise to <see cref="WashSteps"/> and the bake is sized to the draw, which is what
    /// keeps the table at a few dozen entries rather than the full cross product — the callers' strengths
    /// come from short ramps (a height ladder, a depth ladder), not from continuous per-frame values.</para>
    ///
    /// <para>⚠ The bake costs a second resample — source to bake size, then bake size to the draw — where
    /// the live path had one. Against the live wash it is visually the same picture; where they differ it is
    /// confined to a fraction of a level on the antialiased silhouette, not the sprite moving. Don't chase
    /// it; re-measure if <see cref="WashMinEdge"/> or the 2x rule below ever drops, because that is what
    /// bounds it.</para>
    ///
    /// <para>⚠ Returns the rect it drew into, exactly as <see cref="Draw"/> does, computed from the original
    /// art's aspect. Don't compute it from the baked bitmap: the bake rounds its own edges to whole pixels,
    /// and letting that feed the fit would shift every sprite by a fraction of its size.</para></summary>
    public static Rect DrawWashed(DrawingContext dc, BitmapSource? bmp, Rect box, Color colour, double wash,
                                  double darken, double opacity = 1)
    {
        if (bmp is null || opacity <= 0.004) return Rect.Empty;
        if (box.Width <= 0.5 || box.Height <= 0.5) return Rect.Empty;
        double bw = bmp.Width, bh = bmp.Height;
        if (bw <= 0 || bh <= 0) return Rect.Empty;
        double k = Math.Min(box.Width / bw, box.Height / bh);
        var drawn = new Rect(box.X + (box.Width - bw * k) / 2, box.Y + (box.Height - bh * k) / 2, bw * k, bh * k);

        BitmapSource? art = Washed(bmp, colour, wash, darken, Math.Max(drawn.Width, drawn.Height));
        bool fade = opacity < 0.999;
        if (fade) dc.PushOpacity(opacity);
        if (art is not null) dc.DrawImage(art, drawn);
        else
        {
            // No bake (budget, or a bake failure's cooldown): the live washes, so the state the wash carries
            // — a miss's grey, a token's height hue, the darkening with distance — is never dropped.
            dc.DrawImage(bmp, drawn);
            if (wash   > 0.004) Tint(dc, bmp, drawn, colour,       Math.Min(1, wash));
            if (darken > 0.004) Tint(dc, bmp, drawn, Colors.Black, Math.Min(1, darken));
        }
        if (fade) dc.Pop();
        return drawn;
    }

    /// <summary>The washed copy behind <see cref="DrawWashed"/>, or the art itself when there is nothing to
    /// wash. Null only for null art.</summary>
    private static BitmapSource? Washed(BitmapSource? bmp, Color colour, double wash, double darken,
                                        double drawnPx)
    {
        if (bmp is null) return null;
        int w = (int)Math.Round(Math.Clamp(wash, 0, 1) * (WashSteps - 1));
        int d = (int)Math.Round(Math.Clamp(darken, 0, 1) * (WashSteps - 1));
        if (w == 0 && d == 0) return bmp;

        // At least twice the drawn size, so the bake is never the thing that softens a sprite, and capped —
        // a 256 px bake is already 2.5x supersampled for the largest sprite the pipe produces.
        int edge = WashMinEdge;
        while (edge < WashMaxEdge && edge < drawnPx * 2) edge <<= 1;

        var key = (bmp, colour, edge, w, d);
        if (_washed.TryGetValue(key, out var hit)) return hit;
        // Past the budget, or inside a failure's cooldown, the caller takes the live masked path. Never
        // wrong, only slower.
        if (_washedBytes >= WashBudgetBytes || Environment.TickCount64 < _washRetryAtMs) return null;

        // The art fills the whole baked bitmap rather than being letterboxed into a square: DrawWashed
        // stretches it back to a rect built from the original aspect, so any rounding here is sub-pixel.
        double scale = edge / Math.Max(bmp.Width, bmp.Height);
        int pw = Math.Max(1, (int)Math.Round(bmp.Width * scale)), ph = Math.Max(1, (int)Math.Round(bmp.Height * scale));
        var full = new Rect(0, 0, pw, ph);

        BitmapSource baked;
        try
        {
            var dv = new DrawingVisual();
            using (var g = dv.RenderOpen())
            {
                g.DrawImage(bmp, full);
                // The quantised strengths, not the caller's: what is cached has to be what the key says.
                WashOver(g, bmp, full, colour, w / (WashSteps - 1.0));
                WashOver(g, bmp, full, Colors.Black, d / (WashSteps - 1.0));
            }
            var rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            baked = rtb;
        }
        catch (Exception ex)
        {
            // A render target can fail on a starved machine, which is exactly when this path is busiest.
            // Returning null puts the caller back on the live washes rather than dropping the sprite; the
            // bakes resume after a short cooldown rather than never (a starved machine recovers).
            Trace.WriteLine($"[Arcade] wash bake failed: {ex.Message}");
            _washRetryAtMs = Environment.TickCount64 + WashRetryCooldownMs;
            return null;
        }

        _washed[key] = baked;
        _washedBytes += (long)pw * ph * 4;
        return baked;

        static void WashOver(DrawingContext g, BitmapSource src, Rect at, Color c, double alpha)
        {
            if (alpha <= 0.004) return;
            g.PushOpacityMask(ArcadeArt.BrushOf(src));
            g.PushOpacity(Math.Min(1, alpha));
            g.DrawRectangle(TintBrush(c), null, at);
            g.Pop();
            g.Pop();
        }
    }

    /// <summary>Quantisation of both wash strengths. Matches <c>InternodePalette.DimSteps</c>, which is where
    /// the darkening it caches is already quantised — a finer table here would only hold duplicates.</summary>
    private const int WashSteps = 24;
    private const int WashMinEdge = 16;
    private const int WashMaxEdge = 256;
    /// <summary>Ceiling on the baked bitmaps, bytes. Measured need is well under a megabyte (a few dozen
    /// entries, most of them small); this is loose enough never to bind in play and tight enough that a
    /// pathological case cannot eat the process.</summary>
    private const long WashBudgetBytes = 24L * 1024 * 1024;
    private static readonly Dictionary<(BitmapSource, Color, int, int, int), BitmapSource> _washed = [];
    private static long _washedBytes;
    private static long _washRetryAtMs;
    private const int WashRetryCooldownMs = 5_000;

    private static Brush TintBrush(Color c)
    {
        uint key = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        if (Tints.TryGetValue(key, out var hit)) return hit;
        var b = new SolidColorBrush(c);
        b.Freeze();
        // Bounded by the palettes that feed it — a handful of ramps, not per-frame colour.
        if (Tints.Count < 512) Tints[key] = b;
        return b;
    }

    /// <summary>An override PNG, or null. The folder is optional and its contents are a user's files, so
    /// every failure here is a shrug that leaves the packed art (or the vector body) standing.</summary>
    private static BitmapSource? FromOverride(string slot)
    {
        try
        {
            // The slot names are ours, but join through GetFullPath and check containment anyway: this is the
            // one path in the arcade that turns a string into a file read.
            string dir = Path.GetFullPath(OverrideDir);
            string path = Path.GetFullPath(Path.Combine(dir, slot + ".png"));
            if (!path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return null;
            if (!File.Exists(path)) return null;
            return ArcadeArt.LoadFile(path, MaxEdgePx, $"sprite override {slot}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] sprite override {slot} skipped: {ex.Message}");
            return null;
        }
    }
}
