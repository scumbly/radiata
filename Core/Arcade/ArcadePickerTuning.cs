namespace ControllerWheel;

/// <summary>Every feel and layout number in the Arcade picker's cabinet carousel. Mutable statics so the
/// dev-only <c>arcade-tuning.json</c> reaches them (this class is in <see cref="ArcadeTuning"/>'s
/// <c>TuningTypes</c>); units and reference frames are in the names.
///
/// <para>Fractions marked "field" are multiples of the playfield radius. Fractions marked "of the art" are
/// normalised coordinates on the cabinet PNG (0..1 across its width / height), measured against
/// <c>Assets/arcade-carousel-machine_720.png</c> at 386×720.</para></summary>
public static class ArcadePickerTuning
{
    // ── Carousel geometry ─────────────────────────────────────────────────────
    /// <summary>Height of the front cabinet, in field radii. Above 1.0: the cabinet is the picker's whole
    /// subject and the disc is its frame.</summary>
    public static double CabinetHeightFrac = 1.28;
    /// <summary>Radius of the ring the cabinets stand on, in field radii, before perspective.</summary>
    public static double RingRadiusFrac = 0.62;
    /// <summary>Viewer's distance from the ring's centre, in field radii. Smaller = stronger perspective
    /// (the back shrinks more and the sides spread wider).</summary>
    public static double CameraDistanceFrac = 2.30;
    /// <summary>Where a cabinet standing at the ring's side (z = 0) puts its feet, in field radii below the
    /// centre; nearer cabinets stand lower by <see cref="FloorDepthFrac"/>.</summary>
    public static double FloorBaseFrac = 0.315;
    public static double FloorDepthFrac = 0.345;
    /// <summary>Extra shrink applied to a cabinet by depth on top of the perspective: the very back draws at
    /// (1 − this) of its perspective size, the front untouched, sides half way.</summary>
    public static double RearShrink = 0.20;
    /// <summary>How far the front of the ring stands below where the floor alone would put it, as a fraction
    /// of the cabinet's own height. Weighted by depth squared, so it fades in over the whole approach rather
    /// than arriving near the front — see the weighting note in <c>ArcadePickerRenderer.DrawCabinets</c>.</summary>
    public static double FrontDropFrac = 0.15;
    /// <summary>The far end settles lower and draws smaller than the floor and the perspective alone would
    /// put it: <see cref="RearSettleFrac"/> in field radii downward, <see cref="RearTuck"/> as a fraction of
    /// its size. Weighted the same way, mirrored to the back.
    ///
    /// <para>⚠ Two limits. Keep the settle short of burying the rearmost cabinet behind the front one — its
    /// marquee peeking over the front cabinet is the carousel's depth cue, and there is no other sign the
    /// ring continues. And keep the drop under <see cref="FloorDepthFrac"/>'s own front-to-side rise, or the
    /// front stops being the lowest point and the cabinets visibly bob on their way round.</para></summary>
    public static double RearSettleFrac = 0.04;
    public static double RearTuck       = 0.12;
    /// <summary>The perspective scale never drops below this share of the front cabinet's size, so the far
    /// side stays legible as cabinets rather than as clutter.</summary>
    public static double RearScaleFloor = 0.45;
    /// <summary>Opacity of a cabinet's art at the very back; the front draws at 1.0. The only thing depth
    /// does to the art — nothing recolours it, since each game brings its own.</summary>
    public static double DimBack = 0.28;
    /// <summary>Depth (cos of the ring angle, 1 = front) above which a cabinet counts as lit: its screen
    /// turns on and its lines take the game's accent, fading in over the remaining range.</summary>
    public static double ScreenLitZ = 0.70;
    /// <summary>Cabinets deeper than this (cos of ring angle) are not drawn. The rear of the ring is not
    /// hidden by the front cabinet — a cabinet back there stands higher on the floor and draws shorter, so its
    /// top clears the front one and reads as the far side of the carousel. With an even game count the
    /// rearmost sits dead centre behind the front, which is exactly where it belongs.
    /// <see cref="MaxVisibleCabinets"/> is what keeps a large set from becoming a wall of lines.</summary>
    public static double CullBehindZ = -1.01;
    /// <summary>Most cabinets drawn in one frame, nearest to the front first. A dozen drop-in games must
    /// not become a wall of lines.</summary>
    public static int MaxVisibleCabinets = 7;

    // ── Ground shadow ─────────────────────────────────────────────────────────
    public static double ShadowWidthFrac  = 0.55;   // of the cabinet rect width
    public static double ShadowHeightFrac = 0.07;   // of the cabinet rect height
    public static double ShadowAlpha      = 0.55;   // at the front; scaled by depth

    // ── Swing ─────────────────────────────────────────────────────────────────
    /// <summary>Natural frequency of the critically damped spring that carries the ring to its target. 3.2
    /// settles to within 1 % in about a third of a second and re-aims cleanly under auto-repeat.</summary>
    public static double SwingSpringHz = 3.2;

    // ── The screen, in art coordinates ────────────────────────────────────────
    public static double ScreenCxFrac = 0.440;   // of the art width
    public static double ScreenCyFrac = 0.306;   // of the art height
    public static double ScreenRxFrac = 0.332;   // of the art width
    public static double ScreenRyFrac = 0.194;   // of the art height
    /// <summary>How much darker (0..1) and how far desaturated (0..1) a screen draws when its cabinet is not
    /// the front one; both ease off as the cabinet swings forward.</summary>
    public static double RearScreenDarken     = 0.35;
    public static double RearScreenDesaturate = 0.90;
    /// <summary>Every shot is drawn turned this many degrees counter-clockwise inside its screen.</summary>
    public static double ScreenShotTiltDeg = 5;
    /// <summary>The control panel's back edge crosses the bottom of the screen: the shot is masked above the
    /// line from (0, <see cref="ScreenCutLeftFrac"/>) to (1, <see cref="ScreenCutRightFrac"/>) in art
    /// coordinates (x of width, y of height) — the safe area in the author's marked-up cabinet.</summary>
    public static double ScreenCutLeftFrac  = 0.516;
    public static double ScreenCutRightFrac = 0.398;
    /// <summary>The bundled stills are field-only captures of the same 512² disc a live shot is, so they take
    /// the same hair of zoom; a still that includes a bezel ring needs ~1.12 to keep the ring off-screen.</summary>
    public static double BundledZoom = 1.02;
    /// <summary>Live captures are field-only; a hair of zoom hides the anti-aliased rim.</summary>
    public static double LiveZoom = 1.02;
    /// <summary>Square pixel size of a captured screenshot.</summary>
    public static int ShotPx = 512;

    // ── The title on the blank cabinet's nameplate ──────────────────────────────
    /// <summary>The nameplate band painted across the blank cabinet's lower body, as fractions of the art
    /// (measured on <c>cabinet-blank.png</c>: the flat 216-grey fill spans x 11-496, y 605-699 of 506×944).
    /// A game without cabinet art of its own has its title printed inside it.</summary>
    public static double NameplateLeftFrac   = 0.0217;
    public static double NameplateTopFrac    = 0.6409;
    public static double NameplateRightFrac  = 0.9822;
    public static double NameplateBottomFrac = 0.7415;
    /// <summary>Title em size as a fraction of the nameplate's height, and the inset kept clear at each end.
    /// Sized for the condensed nameplate face, whose tall caps fill the band the way the built-ins' lettering does.</summary>
    public static double NameplateTextFrac  = 0.80;
    public static double NameplateInsetFrac = 0.05;
    /// <summary>A drop-in's badge illustration, laid out like the built-ins' (Kabloom's bee): floated at the
    /// nameplate's left end and overflowing it above and below. Height as a multiple of the band's height, its
    /// centre raised by a fraction of that height, and its left edge offset by a fraction of the band's width;
    /// the title then centres in what is left of the band, overlapping the badge's right edge by the last
    /// fraction (of the band's width).</summary>
    public static double NameplateBadgeHeightFrac  = 2.5;
    public static double NameplateBadgeRiseFrac    = 0.12;
    public static double NameplateBadgeLeftFrac    = -0.02;
    public static double NameplateBadgeOverlapFrac = 0.04;

    // ── Launching a game ──────────────────────────────────────────────────────
    /// <summary>✕ on a cabinet: its screen picture lifts off the monitor and grows to fill the disc (the disc
    /// itself growing to a game's size on the same clock), then the game appears behind its ready beat. The
    /// mirror of <c>ArcadeTuning.DiscShrinkSeconds</c>; keep them equal unless the two are meant to differ.
    /// Skipped under Reduce Motion.</summary>
    public static double LaunchSeconds = 0.45;

    // ── Steering ──────────────────────────────────────────────────────────────
    /// <summary>Horizontal stick deflection that arms a step; releasing below
    /// <see cref="StepReleaseThreshold"/> disarms. The gap between them is what stops a stick resting near
    /// the edge from stepping twice.</summary>
    public static double StepArmThreshold     = 0.55;
    public static double StepReleaseThreshold = 0.30;
    /// <summary>Hold a direction this long before it starts repeating, then one step per
    /// <see cref="RepeatSeconds"/>.</summary>
    public static double RepeatDelaySeconds = 0.42;
    public static double RepeatSeconds      = 0.18;

    // ── The wireframe backdrop ────────────────────────────────────────────────
    /// <summary>Horizon height in field radii from the centre (negative = above centre).</summary>
    public static double HorizonYFrac = -0.25;
    public static int    GridRows    = 9;
    public static int    GridColumns = 12;
    /// <summary>Floor rows drift toward the viewer at this many rows per second. Decorative: stopped under
    /// Reduce Motion.</summary>
    public static double GridDriftPerSec = 0.35;
    /// <summary>How far the vanishing point slides sideways during a swing, in field radii per slot of
    /// remaining travel. Zero once the ring has settled.</summary>
    public static double GridParallaxFrac = 0.06;
    /// <summary>One sky tile's edge, in field radii — the sprite is a seamless starfield, so this is purely
    /// how large its structures read against the disc.</summary>
    public static double SkyTileFrac = 3.1;
    /// <summary>How fast the sky slides and on what heading (0° = right, measured clockwise since screen y
    /// grows downward). Slow enough to notice only if you watch for it. Decorative: still under Reduce
    /// Motion.</summary>
    public static double SkyDriftPerSec   = 0.012;
    public static double SkyDriftAngleDeg = 195;
    /// <summary>How much the sky is darkened (0 = the sprite as shipped, 1 = black) — a flat black wash over
    /// the tile, so the same sprite reads dimmer here than it does in Internode.</summary>
    public static double SkyDarken = 0.40;
    /// <summary>Sparks riding the floor grid's lines (the Reactor material's charge, on the ground).
    /// Decorative: not drawn under Reduce Motion.</summary>
    public static int    SparkCount = 6;
    /// <summary>Spark speed range, in trace lengths per second; each spark rolls its own.</summary>
    public static double SparkSpeedMin = 0.30;
    public static double SparkSpeedMax = 0.65;
}
