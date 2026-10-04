using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfFlow = System.Windows.FlowDirection;

namespace ControllerWheel;

/// <summary>
/// Draws a radial menu. Slices are rendered from the control's centre outward. The selection /
/// dwell / delete logic lives in a platform-agnostic <see cref="WheelStateMachine"/>; this control
/// owns one, feeds it stick input on a render timer, and reads its state back to draw.
/// <para>⚠ NAMING: identifiers and comments here use the ORIGINAL material vocabulary, which is not what
/// the UI shows — <c>Terra*</c> is the material shipped as <b>Mesa</b>, <c>GlossLight</c>/<c>GlossDark</c>
/// are <b>Pearl</b>/<b>Obsidian</b>. Kept deliberately so the renderer stays greppable against one
/// vocabulary; the stored config tokens are the canonical ones (see <see cref="Materials"/>).</para>
/// </summary>
public sealed partial class RadialMenuControl : FrameworkElement
{
    // ── Geometry ──────────────────────────────────────────────────────────────
    public const double InnerRadius = 81;
    public const double OuterRadius = 267;
    private const double GapPx = 6.0; // constant pixel gap between adjacent slice edges

    /// <summary>The inter-slice gap for the CURRENT material. Salvage takes double the standard gap: its
    /// plates carry a heavy inner lip and a dark stroke right up to their edges, so at the shared 6px they
    /// read as one riveted sheet rather than separate panels.
    /// Every salvage path that consumes a gap — wedge geometry, the inner-rim cells, the inner/outer edge
    /// strokes, rivet placement, logo fitting — must use THIS, or the strokes land off the painted edge.
    /// Kawaii takes +3.5px: the rounded candy corners want more air than the crisp-edged materials.</summary>
    private double SliceGapPx => _custom?.Spec.GapPx is { } customGap ? customGap : _material switch
    {
        "salvage" => GapPx * 2.0,
        "kawaii"  => GapPx + 3.5,
        _         => GapPx,
    };
    private const double IconSize = 66;  // 3× the original 22-px icon

    // Inner radius of the SLICE RING (the coloured wedges + their icons/labels). The outer edge is
    // fixed; thinner just moves this inward edge outward. The centre hub stays at InnerRadius.
    private double _sliceInner = InnerRadius;

    /// <summary>Radius of the centre hub disc — scales with the ring's inner edge so the hub fills the
    /// centre opening at ANY thickness. The 0.92 factor opens an ~8% breathing gap between the hub's edge
    /// and the slices' inner edge. Only the disc scales; hub TEXT sizing stays tied to InnerRadius.</summary>
    /// <remarks>Reactor is the exception: the 0.92 factor scales WITH the ring, so thinner rings would open
    /// an ever-wider moat. It uses the flat inter-slice gap instead, so the whole ring is spaced consistently.</remarks>
    private double BaseHubRadius => _material == "reactor"
        ? _sliceInner - GapPx
        : (_sliceInner - 4) * 0.92;

    /// <summary>The hub disc radius everything draws against. Normally <see cref="BaseHubRadius"/>; during
    /// the arcade collapse it grows toward the game's own radius and STAYS there while the game is up (see
    /// <see cref="BeginArcadeCollapse"/>), which is what makes the hub read as the thing the game is
    /// mounted in rather than something that got out of the way.</summary>
    private double HubRadius => BaseHubRadius * _hubGrowFactor;

    /// <summary>Draw the hub disc for the current material: a wobbled/eroded ring for the organic-edge
    /// materials (terra/salvage, matching their wedge edges), a plain ellipse otherwise. Reactor keeps a
    /// clean ellipse (its etched rings + armed fusion carry the look).</summary>
    // The hub draws every frame on Terra — cache its wobble ring per (material, radius, centre).
    private (string mat, double r, double cx, double cy) _hubGeoKey;
    private Geometry? _hubGeo;

    // ── The wheel body is opaque: the game or desktop never reads through it ──────────────
    // Most fills carry a little alpha (gloss ramps 242-252, flat fills 235), so every drawn body — wedges,
    // hub, reactor's rest discs, mesa's backdrop blob — sits on a solid backing plate of its own shape. The
    // inter-slice gaps and the empty centre stay transparent.
    // A plate rather than a forced-opaque fill: the fills are gradients as often as solids, and an opaque
    // copy of each would be a second copy of every material's ramp. The plate colour matches the material's
    // end of the scale, so the shift a translucent fill picks up from it is imperceptible. It applies to
    // every material: a no-op where the fill is already opaque.
    private static readonly Brush HubOpaqueLight = Frozen(new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA)));
    private static readonly Brush HubOpaqueDark = Frozen(new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x16)));
    private void DrawHubDisc(DrawingContext dc, Point center, double hubR, Brush fill, Pen? pen)
    {
        Brush plateBrush = DarkMaterial ? HubOpaqueDark : HubOpaqueLight;
        // ⚠ The plate takes the SAME geometry as the fill it backs, so it can never peek at the rim — which
        // is why this is a local function over the shape rather than a disc drawn before the branches.
        void Plate(Geometry shape) => dc.DrawGeometry(plateBrush, null, shape);

        if (_material == "kawaii")
        {
            // Cloud-scalloped hub: soft quadratic bumps around the rim (bump size ~constant in px).
            var cloud = HubSilhouette(center, hubR)!;
            Plate(cloud);
            dc.DrawGeometry(fill, pen, cloud);
            return;
        }
        if (_material is "mesa" or "salvage")
        {
            // Terra: same amp as the slices, at constant px wave density (BuildWobbleRingDense) — the hub's
            // edge matches the wedges' cut instead of stretching a fixed cycle count around a big circle
            // (which read as a nearly-plain circle on medium/thin). Salvage: the STEPPED plate edge its
            // wedges use, so the hub is cut from the same stock.
            bool salvage = _material == "salvage";
            var  ring    = HubSilhouette(center, hubR)!;
            Plate(ring);
            if (salvage)
            {
                // The wedges' panel treatment, minus the per-cell lip variation — the hub reads as one
                // plate, so its lip stays even: fill → rust grain → a uniform recessed lip inside the rim,
                // all clipped to the ring so the centred lip pen's outer half is discarded.
                dc.DrawGeometry(fill, pen, ring);
                dc.PushClip(ring);
                dc.DrawGeometry(SalvageTexture(center), null, ring);
                dc.DrawGeometry(null, SalvageHubLipPen, ring);
                dc.Pop();
            }
            else
            {
                // Same cut-paper treatment as the slices: hard offset cutout shadow under the shape,
                // and NO stroke — the cut edge + shadow carry the hub, exactly like a Terra wedge.
                dc.PushTransform(new TranslateTransform(2, 3));
                dc.DrawGeometry(TerraShadow, null, ring);
                dc.Pop();
                dc.DrawGeometry(fill, null, ring);
            }
        }
        else
        {
            dc.DrawEllipse(plateBrush, null, center, hubR, hubR);
            dc.DrawEllipse(fill, pen, center, hubR, hubR);
            // Drop-in theme hub texture: painted over the disc fill, under the pen's inner half being
            // repainted is avoided by drawing unstroked at the same radius (the rim stays the pen's).
            if (_custom?.HubTexture is { } hubTex)
                dc.DrawEllipse(hubTex, null, center, hubR, hubR);
        }
    }

    /// <summary>The hub's OUTLINE at <paramref name="hubR"/> on the materials whose rim isn't a plain
    /// circle, or null on the ones where it is. The single source for that shape: <see cref="DrawHubDisc"/>
    /// fills it and the arcade preview clips itself to it, so a picture can never be clipped to an edge
    /// nothing draws.
    /// <para>The radius is quantized to 0.5px (the terra-blob trick): the arcade collapse animates
    /// <c>_hubGrowFactor</c> every 16 ms tick, and an exact-radius key rebuilt the cloud/wobble/blocky ring
    /// every frame for the whole sweep. A ≤0.25px radius step is invisible at any size.</para></summary>
    private Geometry? HubSilhouette(Point center, double hubR)
    {
        if (_material is not ("kawaii" or "mesa" or "salvage")) return null;
        double hubRq = Math.Round(hubR * 2.0) / 2.0;
        var key = (_material, hubRq, center.X, center.Y);
        if (_hubGeo is null || _hubGeoKey != key)
        {
            _hubGeo = _material switch
            {
                "kawaii"  => BuildCloudRing(center, hubRq),
                "salvage" => BuildBlockyRing(center, hubRq, amp: 1.1, seed: 101),
                _         => BuildWobbleRingDense(center, hubRq, amp: 2.2, segPx: 8.0, seed: 101),
            };
            _hubGeoKey = key;
        }
        return _hubGeo;
    }

    /// <summary>The fraction of <see cref="HubRadius"/> the hub's silhouette covers in EVERY direction —
    /// 1.0 for a plain disc, less for a material whose rim is scalloped.
    ///
    /// <para>The arcade grows the hub to sit behind the round playfield; dividing the target radius by this
    /// makes the SMALLEST part of the silhouette clear the playfield. Every scalloped/wobbled material needs
    /// it — a rim whose peaks clear by 2px and whose troughs clear by nothing reads as a plain circle, hiding
    /// exactly the edge the material exists to show. Measuring costs one bisection sweep, cached.</para></summary>
    public double HubCoverageFraction => _material switch
    {
        "kawaii" => RingCoverage("kawaii", static (c, r) => BuildCloudRing(c, r)),
        // ⚠ These two must build the ring with the SAME arguments HubSilhouette does, or the measurement
        // describes a shape nothing draws. The amp/segPx pairs are duplicated deliberately: change one and
        // the other stops matching, which is the alarm we want.
        "mesa" => RingCoverage("mesa",
            static (c, r) => BuildWobbleRingDense(c, r, amp: 2.2, segPx: 8.0, seed: 101)),
        "salvage" => RingCoverage("salvage", static (c, r) => BuildBlockyRing(c, r, amp: 1.1, seed: 101)),
        _ => 1.0,
    };

    private static readonly Dictionary<string, double> CoverageCache = [];

    /// <summary>The fraction of its nominal radius a hub silhouette reaches in its WORST direction. MEASURED
    /// off the real geometry rather than re-deriving each material's edge maths, so it can't drift from what
    /// <see cref="DrawHubDisc"/> actually draws. Cached per material: it's a fixed property of the shape.</summary>
    private static double RingCoverage(string material, Func<Point, double, Geometry> build)
    {
        if (CoverageCache.TryGetValue(material, out double cached)) return cached;
        // Sampled at a large radius on purpose: every one of these shapes derives its feature count from the
        // circumference and settles by this size, so one measurement covers every radius the arcade asks for.
        const double r = 260;
        var origin = new Point(0, 0);
        Geometry ring = build(origin, r);
        // 1° apart and 16 bisections: these are broad, smooth features, so this lands within a fraction of a
        // percent of a much finer sweep. Kept modest because it runs on the UI thread immediately before the
        // collapse animation starts, and a hitch there would be the one place it shows.
        double worst = 1.0;
        for (int i = 0; i < 360; i++)
        {
            double a = i * Math.PI / 180.0;
            double lo = 0, hi = r * 1.25;
            for (int step = 0; step < 16; step++)     // bisect to the outline along this ray
            {
                double mid = (lo + hi) / 2;
                if (ring.FillContains(new Point(Math.Cos(a) * mid, Math.Sin(a) * mid))) lo = mid;
                else hi = mid;
            }
            worst = Math.Min(worst, lo / r);
        }
        // Floor it well above zero: a pathological measurement must never make the caller demand an enormous
        // hub, and 0.5 is far below anything these shapes can produce.
        return CoverageCache[material] = Math.Clamp(worst, 0.5, 1.0);
    }

    /// <summary>Kawaii's cloud hub: a UNION of a few big overlapping lobe circles around a core disc —
    /// how a cartoon cloud is actually constructed, and the shape the material was pitched with.
    /// <para>Lobe COUNT scales with circumference at a fixed ~85px pitch, clamped to 7…12, so a lobe keeps
    /// roughly the same on-screen size whether it's a thick hub (r≈71), the default medium hub (r≈122, which
    /// lands on 9 — the tuned look), a thin hub (r≈174) or the toast (r≈79). Neither a constant lobe size
    /// (large hubs become a doily) nor a fixed count (lobe size swings with the hub) survives all radii.</para>
    /// <para>Lobe RADIUS is then derived from the count so the geometry stays self-consistent: adjacent
    /// lobes must overlap by the same proportion at any count, i.e. rb ≈ 0.672 × the centre-to-centre pitch.
    /// Solving that against the extent budget (rc + 1.16·rb = r) gives the closed form below, which
    /// reproduces the tuned 0.30r exactly at 9 lobes. Radii also vary deterministically ±14%, because
    /// perfectly equal lobes read as a flower even at the right count.</para>
    /// Extent is held just inside <paramref name="r"/> so the cloud stays within the hub's breathing gap.</summary>
    private static Geometry BuildCloudRing(Point c, double r)
    {
        int    lobes = Math.Clamp((int)Math.Round(2 * Math.PI * r / 85.0), 7, 12);
        double s     = Math.Sin(Math.PI / lobes);
        double rb    = r * 1.344 * s / (1 + 1.559 * s);   // lobe radius (0.30r at 9 lobes)
        double rc    = r - rb * 1.16;                     // lobe-centre ring — biggest lobe just reaches r
        // Core disc fills the middle (the lobes ring the rim and don't reach the centre on their own).
        Geometry acc = new EllipseGeometry(c, rc + rb * 0.10, rc + rb * 0.10);
        for (int k = 0; k < lobes; k++)
        {
            double ang = k * (360.0 / lobes) - 90;
            double v   = 0.86 + 0.28 * ((k * 7 % 5) / 4.0);   // 0.86…1.14, deterministic per lobe
            acc = Geometry.Combine(acc, new EllipseGeometry(Polar(c, rc, ang), rb * v, rb * v),
                                   GeometryCombineMode.Union, null);
        }
        acc.Freeze();
        return acc;
    }

    // Extra shrink applied to assigned-game LOGOS only (built-in glyphs unaffected): game logos read
    // large, so on the thin ring they get an additional 30% reduction. 1.0 = no extra shrink.
    private double _logoExtraScale = 1.0;
    private bool   _thinRing;   // thin thickness → tighten the icon↔label gap a touch
    private bool   _thickRing;  // thick thickness → the hub's CONTENT shrinks 10% (see HubContentScale)
    private double _batteryScale = 1.0;   // battery glyph grows +10% on medium, +20% on thin
    // Thick ring has band to spare, so ICON glyphs (not custom logos / cover art) run 30% larger there.
    // Medium/thin don't — they'd only get clamped back by contentFit.
    private double _iconThickScale = 1.0;
    // …and MEDIUM gets the same 30% once the wheel is CROWDED. IconScaleFor shrinks glyphs 10%/slice
    // past 8, which thick's own boost offsets — the boost keeps a crowded medium wheel matching thick
    // instead of reading cramped. Under 9 slices medium is unchanged (no crowding shrink to offset);
    // thin keeps the full shrink, since its band genuinely has no room. Medium stays unclamped by contentFit.
    private bool   _mediumRing;
    private const double MediumCrowdBoost = 1.30;
    // Thick ring: push slice content (icon/logo/label) 7.5% further out along its radius.
    private double _contentPushOut = 1.0;

    /// <summary>Glyph size multiplier: the crowding shrink, the ring-thickness boost, and medium's
    /// crowded-wheel boost. Cover art opts out of the boosts (it's fitted art, not a glyph).</summary>
    private double GlyphScale(int n, bool cover) =>
        IconScaleFor(n) * (cover ? 1.0 : _iconThickScale * (_mediumRing && n > 8 ? MediumCrowdBoost : 1.0));

    /// <summary>Set the slice-ring thickness: "thick" (full), "medium" (70%, default), "thin" (40%).
    /// Only the slices' inner edge moves; OuterRadius and the hub are unchanged.</summary>
    public void SetSliceThickness(string? thickness)
    {
        SchedulePrewarm();
        double full = OuterRadius - InnerRadius;
        string t = thickness?.Trim().ToLowerInvariant() ?? "medium";
        _sliceInner = t switch
        {
            "thin"   => OuterRadius - full * 0.40,
            "medium" => OuterRadius - full * 0.70,
            _        => InnerRadius,   // "thick" / default / unknown (full thickness)
        };
        _logoExtraScale = t == "thin" ? 0.90 : 1.0;
        _thinRing  = t == "thin";
        _thickRing = t is not ("thin" or "medium");   // thick / default / unknown — matches _sliceInner above
        // The hub grows on thinner rings — grow the battery glyph with it so it doesn't read tiny.
        _batteryScale = t switch { "thin" => 1.20, "medium" => 1.10, _ => 1.0 };
        _iconThickScale = t is "thin" or "medium" ? 1.0 : 1.30;
        _mediumRing     = t == "medium";
        _contentPushOut = t is "thin" or "medium" ? 1.0 : 1.075;
        _backglow = _backglowNoHub = null;   // shadow shape depends on the ring's inner edge — rebuild both
        InvalidateVisual();
    }

    // Global slice-label mode (Settings ▸ Customize ▸ "Label slices"). Interpreted only through
    // SliceLabelRule.ShouldShow — see the one label decision point in the slice draw.
    private string _labelMode = SliceLabelRule.Default;

    /// <summary>Set the global slice-label mode: "all-except-logos" (default), "all", "none", or
    /// "selected" (honour each slice's own Show-label flag). Unknown/blank normalizes to the default.
    /// The in-wheel editor still forces labels on for non-logo slices whatever this says.</summary>
    public void SetShowSliceLabels(string? mode)
    {
        SchedulePrewarm();
        _labelMode = SliceLabelRule.Normalize(mode);
        InvalidateVisual();
    }

    // Resting-slice "material": the original 2×2 of finish × theme — "flat-light" (solid white), "pearl"
    // (glossy white glass, default), "flat-dark" (solid dark), "obsidian" (glossy dark) — plus the four
    // styled looks. Only the RESTING fill changes — armed/confirm/assign keep their state colours. Dark
    // materials flip the label/hub ink to light.
    private string _material = Materials.GlossLight;

    /// <summary>Map any stored material token — legacy aliases included ("flat"/"flat-white"/"glass"/
    /// "frosted"/"gloss-black-*"/"frost-*"/"sparkle"/"stencil"/"gloss-light"/"gloss-dark"/"terra"/"paper")
    /// — onto its canonical name in <see cref="Materials.All"/>.
    /// The static preview bakes (<see cref="RenderPreviewIcon"/>/<see cref="RenderPreviewLogo"/>) must
    /// resolve aliases through this same path as the wheel, or a legacy-token config gets a bare, untreated
    /// glyph in the Settings icon well while the wheel draws the full treatment.
    /// <para>The alias table lives in <see cref="Materials.Normalize"/> (Core) so <c>ConfigLoader</c> can
    /// canonicalize on load; this is the shell-side name call sites use.</para></summary>
    internal static string NormalizeMaterial(string? material) => Materials.Normalize(material);

    public void SetSliceMaterial(string? material)
    {
        SchedulePrewarm();
        _material = NormalizeMaterial(material);
        _custom = CustomTheme.For(_material);   // null unless the token is a registered drop-in theme
        ResolveBatteryIcon();   // the glyph is pre-tinted per material — re-bake it for the new one
        InvalidateVisual();
    }

    // ── Drop-in custom materials (Core\Packages; data-only — see docs/PACKAGES.md) ───────────────
    // A custom theme renders through the shared default paths with baked substitutions: fills
    // (solid/bowed/linear or a Kawaii-style per-slice hue walk), pens, armed lift/scale, glyph
    // treatments (all via the existing generic effect helpers), and label styling. It selects and
    // parameterizes host effects; it can never reach a styled material's bespoke branches. Baked +
    // frozen once per SetSliceMaterial; a token's content never changes within a run.
    private CustomTheme? _custom;

    private sealed class CustomTheme
    {
        public required MaterialPackage Spec;
        public required Brush Resting, Armed, Confirm;
        public required Brush? Label;
        public required Pen? RestingPen, ArmedPen;
        public required Typeface? Face;
        public required Brush EdgeBrush;
        public required Color GlowColor;
        // Format 3 textures — frozen ImageBrushes (null = role absent or its file failed to decode;
        // fail-quiet per file, the theme still loads). Backdrop keeps its opacity separately because
        // it draws through a wheel-sized clip rather than as a shape fill.
        public Brush? SliceTexture, HubTexture;
        public ImageSource? BackdropImage;
        public double BackdropOpacity;
        /// <summary>The Customize tile's button face (resting fill, texture-composited if the
        /// package's tile.texture is set). Always non-null once the theme is built.</summary>
        public Brush TileFill = Brushes.Transparent;
        private readonly Dictionary<int, Brush> _restHue = [], _armedHue = [];

        public bool HasHue => Spec.HueWalk is not null;

        /// <summary>Per-slice hue-walk fills (host-shaped bowed ramps; the package supplies only
        /// sat/light numbers). Cached per whole hue degree, like Kawaii's.</summary>
        public Brush HueFill(double midDeg, bool armedState)
        {
            var w = Spec.HueWalk!;
            double hue = (((midDeg + 90.0 + w.HueOffsetDeg) % 360) + 360) % 360;
            int key = (int)Math.Round(hue);
            var cache = armedState ? _armedHue : _restHue;
            if (cache.TryGetValue(key, out var b)) return b;
            double s = armedState ? w.ArmedSat : w.Sat;
            double l = armedState ? w.ArmedLight : w.Light;
            (double o, Color c)[] ramp =
            {
                (0.00, HslColor(255, key, Math.Min(1.0, s * 0.85), Math.Min(0.98, l + 0.045))),
                (0.50, HslColor(255, key, s, l)),
                (1.00, HslColor(255, key, Math.Min(1.0, s * 1.10), Math.Max(0.05, l - 0.035))),
            };
            return cache[key] = BowedGloss(ramp);
        }

        // A token's content never changes within a run (PackageInstallFlow), so a theme's baked
        // brushes/textures never go stale — cache them keyed by token. Without this, every call site
        // (SetSliceMaterial, PreviewFill/PreviewDecoration on each tile build) would re-decode any
        // texture from disk on every call.
        private static readonly Dictionary<string, CustomTheme> _cache = [];

        public static CustomTheme? For(string token)
        {
            if (_cache.TryGetValue(token, out var cached)) return cached;
            if (Materials.CustomFor(token) is not { } spec) return null;
            var glyph = spec.Glyph;
            var backdrop = spec.Backdrop is { } bd ? LoadTextureImage(spec.DirPath, bd.File) : null;
            var resting = FillBrush(spec.RestingFill, spec.RestingArgb);
            var theme = new CustomTheme
            {
                SliceTexture = TextureBrush(spec.DirPath, spec.SliceTexture),
                HubTexture = TextureBrush(spec.DirPath, spec.HubTexture),
                BackdropImage = backdrop,
                BackdropOpacity = spec.Backdrop?.Opacity ?? 0,
                Spec = spec,
                Resting = resting,
                Armed = FillBrush(spec.ArmedFill, spec.ArmedArgb),
                Confirm = FillBrush(spec.ConfirmFill, spec.ConfirmArgb),
                Label = spec.LabelArgb is { } l ? Solid(l) : null,
                RestingPen = PenFrom(spec.Outline)
                          ?? (spec.OutlineArgb is { } o ? (Pen)new Pen(Solid(o), 1.5).GetAsFrozen() : null),
                ArmedPen = PenFrom(spec.ArmedOutline),
                Face = FaceFor(spec.LabelFont),
                EdgeBrush = glyph is not null ? Solid(glyph.EdgeArgb) : Brushes.White,
                GlowColor = glyph?.GlowArgb is { } g ? ColorOf(g) : Colors.White,
            };
            theme.TileFill = ComposeTileFill(resting, TextureBrush(spec.DirPath, spec.Tile?.Texture));
            _cache[token] = theme;
            return theme;
        }

        /// <summary>The Customize tile's button face: the theme's resting fill (solid/bowed/linear —
        /// whatever the theme already uses), with the tile's own texture layered over it if present.
        /// Composited once via a tiny DrawingBrush so the tile swatch matches what the wheel itself
        /// shows, rather than falling back to a flat color the moment a theme has a real texture.</summary>
        private static Brush ComposeTileFill(Brush resting, Brush? tileTexture)
        {
            if (tileTexture is null) return resting;
            var unit = new Rect(0, 0, 1, 1);
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(resting, null, new RectangleGeometry(unit)));
            dg.Children.Add(new GeometryDrawing(tileTexture, null, new RectangleGeometry(unit)));
            var brush = new DrawingBrush(dg)
            {
                Stretch = Stretch.Fill,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = unit,
            };
            return (Brush)brush.GetAsFrozen();
        }

        private static Color ColorOf(uint argb) => Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        private static Brush Solid(uint argb) => Frozen(new SolidColorBrush(ColorOf(argb)));

        private static Brush FillBrush(MaterialPackage.FillSpec? fill, uint fallbackArgb)
        {
            if (fill is null) return Solid(fallbackArgb);
            switch (fill.Type)
            {
                case "bowed":
                {
                    var ramp = new (double o, Color c)[fill.Stops.Count];
                    for (int i = 0; i < fill.Stops.Count; i++)
                        ramp[i] = (fill.Stops[i].At, ColorOf(fill.Stops[i].Argb));
                    return BowedGloss(ramp);
                }
                case "linear":
                {
                    double rad = fill.AngleDeg * Math.PI / 180.0;
                    var b = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5 - Math.Cos(rad) / 2, 0.5 - Math.Sin(rad) / 2),
                        EndPoint   = new Point(0.5 + Math.Cos(rad) / 2, 0.5 + Math.Sin(rad) / 2),
                    };
                    foreach (var (at, argb) in fill.Stops) b.GradientStops.Add(new GradientStop(ColorOf(argb), at));
                    return (Brush)b.GetAsFrozen();
                }
                default: return Solid(fill.Color);
            }
        }

        private static Pen? PenFrom(MaterialPackage.OutlineSpec? spec)
        {
            if (spec is null) return null;
            var pen = new Pen(Solid(spec.Argb), spec.Width) { LineJoin = PenLineJoin.Round };
            if (spec.Dash is { Count: > 0 } dash)
                pen.DashStyle = new DashStyle(dash, 0);
            return (Pen)pen.GetAsFrozen();
        }

        /// <summary>Decode a package texture with the decompression-bomb guard: probe the header first
        /// (no pixel decode), then decode with a pixel cap only when the source is oversized — a small
        /// tile is never upscaled. OnLoad + freeze, so the file handle closes and the render thread can
        /// touch the result. Any failure → null (the theme loads without that texture, traced).</summary>
        private static ImageSource? LoadTextureImage(string dir, string file)
        {
            try
            {
                var path = System.IO.Path.Combine(dir, file);
                int width;
                using (var probe = System.IO.File.OpenRead(path))
                    width = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation,
                                                 BitmapCacheOption.None).Frames[0].PixelWidth;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                if (width > 2048) bi.DecodePixelWidth = 2048;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Packages] texture {file} unusable: {ex.Message}");
                return null;
            }
        }

        private static Brush? TextureBrush(string dir, MaterialPackage.TextureSpec? spec)
        {
            if (spec is null || LoadTextureImage(dir, spec.File) is not { } img) return null;
            var brush = new ImageBrush(img) { Opacity = spec.Opacity, Stretch = Stretch.UniformToFill };
            if (spec.Tile)
            {
                brush.TileMode = TileMode.Tile;
                brush.Stretch = Stretch.Fill;
                brush.ViewportUnits = BrushMappingMode.Absolute;
                brush.Viewport = new Rect(0, 0, img.Width, img.Height);   // natural size, DIP
            }
            brush.Freeze();
            return brush;
        }

        /// <summary>System-installed families only; an unknown name falls back silently (null =
        /// default face) — a package must never surface a font error.</summary>
        internal static Typeface? FaceFor(string? family)
        {
            if (string.IsNullOrWhiteSpace(family)) return null;
            try
            {
                foreach (var f in Fonts.SystemFontFamilies)
                    if (string.Equals(f.Source, family, StringComparison.OrdinalIgnoreCase))
                        return new Typeface(f, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            }
            catch { }
            return null;
        }
    }

    private bool DarkMaterial  => Materials.IsDark(_material);   // single source: Core\Materials
    // A subtle wheel-wide glass dome (overhead sheen + top-rim catch) layered over the glass materials.
    private bool WheelDome     => _material is "obsidian" or "pearl";

    // Text/glyph ink: dark on the white themes (as before), light on the dark themes.
    // Kawaii keeps a DEEP PLUM instead of the near-black default — on-palette, still ≥7:1 on the milk.
    private Brush InkLabel => _custom?.Label is { } customInk ? customInk
                            : _material == "kawaii" ? KawaiiInk
                            : DarkMaterial ? LightLabelBrush : LabelBrush;        // slice labels
    private Brush InkState => DarkMaterial ? LightStateBrush : StatusStateBrush;  // toggle state (target)
    private Brush InkNext  => DarkMaterial ? LightNextBrush  : StatusNextBrush;   // toggle current state
    private Brush InkHint  => DarkMaterial ? LightHintBrush  : EditHubBrush;      // edit-legend hint lines

    private static readonly Brush DarkFlatFill =
        Frozen(new SolidColorBrush(Color.FromArgb(235, 33, 36, 45)));   // solid dark slate (a bit darker)

    // Gloss Dark — a "glass button" with a light sheen at the top, a HARD horizon at 0.50 cutting to
    // near-black, then a faint bottom bounce. Per-slice (each slice is its own button), lit slightly OFF
    // the vertical axis (upper-left). COLOURS ONLY are shared with the Game Grid via GlossDarkPalette (its
    // muted teal), so the two read as the same hue; the wheel keeps its own chrome (white dome rim, no
    // edge glow) — only the fill colours were brought over.
    private static readonly (double o, Color c)[] GlossDarkRamp =
    {
        (0.00, GlossDarkPalette.A(GlossDarkPalette.Sheen,        252)),   // top-lit cool sheen
        (0.08, GlossDarkPalette.A(GlossDarkPalette.UpperMid,     248)),
        (0.49, GlossDarkPalette.A(GlossDarkPalette.AboveHorizon, 244)),   // darkens just above the horizon
        (0.50, GlossDarkPalette.A(GlossDarkPalette.DarkCut,      250)),   // HARD cut → near-black
        (0.90, GlossDarkPalette.A(GlossDarkPalette.Body,         250)),
        (1.00, GlossDarkPalette.A(GlossDarkPalette.Bounce,       242)),   // faint bottom bounce
    };
    private Brush? _glossDarkFill;
    private Brush GlossDarkFill() => _glossDarkFill ??= BowedGloss(GlossDarkRamp);

    // Gloss Light — the Gloss Dark "glass button" lessons on a LIGHT theme: a bright specular sheen at the
    // top, a gentle (not black) horizon step near the middle, then a faint bottom bounce. Drawn with the
    // same bowed-horizon radial brush + 16° overhead-left light as Gloss Dark, plus the wheel-wide dome.
    private static readonly (double o, Color c)[] GlossLightRamp =
    {
        (0.00, Color.FromArgb(252, 255, 255, 255)),   // bright specular sheen at the top
        (0.10, Color.FromArgb(250, 246, 250, 253)),
        (0.47, Color.FromArgb(244, 224, 232, 238)),   // just above the horizon
        (0.50, Color.FromArgb(246, 212, 222, 230)),   // gentle horizon step — low contrast across the line
        (0.65, Color.FromArgb(242, 220, 230, 237)),   // recovers just below the horizon
        (1.00, Color.FromArgb(230, 228, 236, 242)),   // faint bottom bounce
    };
    private Brush? _glossLightFill;
    private Brush GlossLightFill() => _glossLightFill ??= BowedGloss(GlossLightRamp);

    // Armed (selected) gloss slices light up: the SAME bowed glass, but a brighter, accent-tinted ramp so
    // the glass reads as ENERGISED — not the flat opaque swatch the flat materials use. Gloss Dark glows
    // cyan (horizon lifted off black); Gloss Light picks up a cool-blue accent.
    private static readonly (double o, Color c)[] GlossDarkArmedRamp =
    {
        (0.00, Color.FromArgb(252, 150, 214, 232)),   // bright cyan sheen — lit
        (0.08, Color.FromArgb(248,  96, 168, 190)),
        (0.49, Color.FromArgb(246,  40, 104, 124)),   // above the horizon
        (0.50, Color.FromArgb(250,  18,  64,  82)),   // horizon LIFTED (glows instead of cutting to black)
        (0.90, Color.FromArgb(250,  28,  84, 104)),
        (1.00, Color.FromArgb(244,  44, 110, 130)),   // bottom bounce
    };
    // MUTED slate blue with a hint of teal, anchored on #8CA4BA — deliberately desaturated, not a
    // high-saturation aqua; a quiet blue keeps "selected = cool" legibility against warm glyph tints.
    private static readonly (double o, Color c)[] GlossLightArmedRamp =
    {
        (0.00, Color.FromArgb(252, 246, 250, 253)),   // cool near-white sheen
        (0.10, Color.FromArgb(250, 225, 235, 244)),
        (0.47, Color.FromArgb(246, 172, 194, 211)),   // misty slate-blue glass just above the horizon
        (0.50, Color.FromArgb(248, 140, 164, 186)),   // horizon step — the #8CA4BA anchor
        (0.65, Color.FromArgb(244, 182, 202, 217)),
        (1.00, Color.FromArgb(238, 167, 190, 207)),   // bottom bounce
    };
    private Brush? _glossDarkArmedFill, _glossLightArmedFill;
    private Brush GlossDarkArmedFill()  => _glossDarkArmedFill  ??= BowedGloss(GlossDarkArmedRamp);
    private Brush GlossLightArmedFill() => _glossLightArmedFill ??= BowedGloss(GlossLightArmedRamp);

    // Hold-to-confirm on the GLOSS materials: the EXACT armed-glass treatment (same alphas, same ramp
    // structure, same bowed geometry) with the hue swapped to the confirm RED — so a confirming slice
    // reads as "the armed glass, but red", not a flat swatch breaking the material.
    private static readonly (double o, Color c)[] GlossLightConfirmRamp =
    {
        (0.00, Color.FromArgb(252, 253, 247, 246)),   // warm near-white sheen
        (0.10, Color.FromArgb(250, 244, 227, 225)),
        (0.47, Color.FromArgb(246, 215, 172, 167)),   // misty red glass just above the horizon
        (0.50, Color.FromArgb(248, 197, 126, 120)),   // horizon step — red analogue of the #8CA4BA anchor
        (0.65, Color.FromArgb(244, 220, 184, 179)),
        (1.00, Color.FromArgb(238, 210, 169, 163)),   // bottom bounce
    };
    private static readonly (double o, Color c)[] GlossDarkConfirmRamp =
    {
        (0.00, Color.FromArgb(252, 240, 162, 152)),   // bright red sheen — lit
        (0.08, Color.FromArgb(248, 196, 104,  94)),
        (0.49, Color.FromArgb(246, 132,  50,  44)),   // above the horizon
        (0.50, Color.FromArgb(250,  90,  24,  20)),   // horizon LIFTED (glows instead of cutting to black)
        (0.90, Color.FromArgb(250, 110,  34,  28)),
        (1.00, Color.FromArgb(244, 130,  48,  42)),   // bottom bounce
    };
    private Brush? _glossDarkConfirmFill, _glossLightConfirmFill;

    /// <summary>Armed-slice fill for the current material: gloss materials light their glass up (a brighter
    /// bowed gradient); flat materials use the shared flat <see cref="ArmedFill"/> swatch.</summary>
    private Brush ArmedFillFor() => _custom is { } custom ? custom.Armed : _material switch
    {
        "obsidian"  => GlossDarkArmedFill(),
        "pearl" => GlossLightArmedFill(),
        "flat-dark"   => ArmedFillDark,
        "kawaii"     => KawaiiArmedFallback,   // slices use the per-slice hue fill (DrawSlice); this covers non-slice callers (edit ring etc.)
        "salvage"     => SalvageFill,         // fill unchanged — the white inner crescent is the armed cue
        "mesa"       => TerraArmedFill,      // saturated warm yellow + chunky black stroke
        "reactor"     => ReactorArmedFill,    // near-black hub-fusion fill (union drawn in DrawSlice)
        _             => ArmedFill,   // flat-light
    };

    /// <summary>Hold-to-confirm fill for the current material: the gloss materials use the armed-glass
    /// treatment recoloured red; the flat materials keep the flat light-red swatch.</summary>
    private Brush ConfirmFillFor() => _custom is { } custom ? custom.Confirm : _material switch
    {
        "obsidian"  => _glossDarkConfirmFill  ??= BowedGloss(GlossDarkConfirmRamp),
        "pearl" => _glossLightConfirmFill ??= BowedGloss(GlossLightConfirmRamp),
        _             => ConfirmFill,   // flat-light / flat-dark
    };

    // ── Pearl / Obsidian guarded dwell: the glass RECOLOURS instead of fading ──────────────────────
    // The slice holds FULL opacity for the whole dwell and its ramp is lerped resting → confirm-red as the
    // hold progresses, so the progress cue is colour, not the shared arming-transparency ramp.
    // ⚠ Lerped stop-for-stop: each gloss RESTING ramp and its CONFIRM ramp must keep the same stop count
    // and the same offsets. Adding a stop to one means adding it to the other.
    private const int GlossDwellSteps = 24;   // progress is quantized so a frame reuses a frozen brush
    private readonly Dictionary<int, Brush> _glossDwellCache = new();

    private static Color LerpColor(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>The gloss materials' guarded-dwell fill at confirm progress <paramref name="t"/>
    /// (0 = the resting glass, 1 = the full confirm red).</summary>
    private Brush GlossDwellFill(double t)
    {
        bool dark = _material == "obsidian";
        int step  = (int)Math.Round(Math.Clamp(t, 0.0, 1.0) * GlossDwellSteps);
        int key   = dark ? step + 1000 : step;
        if (_glossDwellCache.TryGetValue(key, out var cached)) return cached;

        var from = dark ? GlossDarkRamp : GlossLightRamp;
        var to   = dark ? GlossDarkConfirmRamp : GlossLightConfirmRamp;
        double k = (double)step / GlossDwellSteps;
        var ramp = new (double o, Color c)[from.Length];
        for (int s = 0; s < from.Length; s++) ramp[s] = (from[s].o, LerpColor(from[s].c, to[s].c, k));
        return _glossDwellCache[key] = BowedGloss(ramp);
    }

    // Low alpha PER TAP; the taps overlap to build up a soft, feathered shadow (fake blur).
    // LIGHT materials use a 1/3-opacity variant — a full-strength shadow reads too heavy on their
    // bright slice fills.
    private static readonly Brush ContentShadowBrush      = Frozen(new SolidColorBrush(Color.FromArgb(30, 0, 0, 4)));
    private static readonly Brush ContentShadowBrushLight = Frozen(new SolidColorBrush(Color.FromArgb(10, 0, 0, 4)));
    // Terra halves the light shadow again — even the 1/3 light shadow reads too heavy on its bright surface.
    private static readonly Brush ContentShadowBrushHalf  = Frozen(new SolidColorBrush(Color.FromArgb( 5, 0, 0, 4)));
    // Salvage RESTING slices get a heavier shadow than the shared dark-material one: the plate is a busy
    // mid-tone rust photo, so a 30-alpha shadow doesn't separate the glyph from what's behind it. Armed
    // salvage is unaffected — it swaps the silhouette shadow for the tinted back-light glow + outward
    // cast shadow instead.
    private static readonly Brush SalvageRestShadowBrush  = Frozen(new SolidColorBrush(Color.FromArgb(64, 0, 0, 4)));
    // Per-slice icon/logo edge stroke + the armed-icon lighten overlay (terra / gloss-light).
    // Terra's icon treatment is the OUTER rim (TerraIconEdgeBrush), not an inner stroke.
    private static readonly Brush ReactorEdgeBrush  = Frozen(new SolidColorBrush(Color.FromArgb( 77, 0, 0, 0)));   // 30% black (reactor, 1px)
    /// <summary>Reactor's inner icon-stroke width. Named so the wheel's edgeInset switch and the Settings
    /// icon-well preview (RenderPreviewIcon) read the same number.</summary>
    internal const double ReactorIconEdgeInset = 0.5;
    // Thin thickness halves every edge-stroke opacity — the crowded thin ring reads too heavy at full strength.
    private static readonly Brush ReactorEdgeBrushThin = Frozen(new SolidColorBrush(Color.FromArgb( 39, 0, 0, 0)));
    private static readonly Brush IconLightenBrush  = Frozen(new SolidColorBrush(Color.FromArgb( 26, 255, 255, 255)));   // ~10% white overlay
    // A small spread of offsets around the base offset → soft edge instead of one crisp copy.
    private static readonly (double dx, double dy)[] ShadowTaps =
    {
        (0, 0),
        (-2, 0), (2, 0), (0, -2), (0, 2),
        (-1.4, -1.4), (1.4, -1.4), (-1.4, 1.4), (1.4, 1.4),
    };

    /// <summary>A shape-accurate, softly-feathered drop shadow for an armed slice's glyph/logo: the image
    /// used as an opacity mask over translucent-black rects, drawn at a small spread of offsets (a cheap
    /// blur) and nudged down-right so it rests just behind the real one — keeps a light glyph/logo legible
    /// over the bright lit-cyan armed glass.</summary>
    // Gaussian-blurred bake of an icon/logo silhouette, cached per image. TWO consumers: reactor's unarmed
    // drop shadow draws the bitmaps directly (near-black, offset), and salvage's armed glow uses their
    // ALPHA as an opacity mask over a tinted fill — so the same bake serves any colour. Blur is expensive,
    // so it happens once per image at load-shape time, never per frame.
    private const double SilhouetteBlurCanvas = 200.0;   // canonical bake size (logical px)
    private const double SilhouetteBlurInner  = 120.0;   // silhouette box inside it (40px blur margin each side)
    // Blur levels baked per image, indexed by the constants below. All share the SAME canvas geometry so
    // they stack pixel-exactly. Adding a level costs one more RenderTargetBitmap per image, once.
    private static readonly double[] SilhouetteBlurRadii = [18.0, 36.0, 33.0];
    private const int BlurTight = 0;   // dense core — reactor's shadow, salvage's glow core
    private const int BlurWide  = 1;   // soft outer halo
    private const int BlurCast  = 2;   // salvage's cast shadow — softer than the core, tighter than the halo
    // The silhouette is baked ASPECT-CORRECT (fit inside the inner box, not stretched square) and mapped
    // back out UNIFORMLY, so the Gaussian stays isotropic on screen — a square-stretch bake compresses a
    // wide logo's vertical blur into a "horizontal-stripey" mess.
    private sealed record SilhouetteBlurBake(BitmapSource[] Maps, double FitW, double FitH);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, SilhouetteBlurBake>
        SilhouetteBlurCache = new();
    private static SilhouetteBlurBake SilhouetteBlurBitmaps(ImageSource img)
    {
        if (SilhouetteBlurCache.TryGetValue(img, out var cached)) return cached;
        double iw = Math.Max(1.0, img.Width), ih = Math.Max(1.0, img.Height);
        double k  = Math.Min(SilhouetteBlurInner / iw, SilhouetteBlurInner / ih);
        double fw = iw * k, fh = ih * k;   // aspect-preserving fit inside the inner box
        var fit = new Rect((SilhouetteBlurCanvas - fw) / 2.0, (SilhouetteBlurCanvas - fh) / 2.0, fw, fh);
        int px = (int)SilhouetteBlurCanvas;
        BitmapSource Bake(double blur)
        {
            // The blurred silhouette must be a CHILD visual — RenderTargetBitmap ignores an Effect on the
            // root visual it renders, but applies it on children.
            var child = new DrawingVisual { Effect = new BlurEffect { Radius = blur, KernelType = KernelType.Gaussian } };
            using (var cdc = child.RenderOpen())
            {
                cdc.PushOpacityMask(new ImageBrush(img));
                cdc.DrawRectangle(Brushes.Black, null, fit);
                cdc.Pop();
            }
            var root = new DrawingVisual();
            root.Children.Add(child);
            var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            rtb.Freeze();
            return rtb;
        }
        var bake = new SilhouetteBlurBake([.. SilhouetteBlurRadii.Select(Bake)], fw, fh);
        SilhouetteBlurCache.Add(img, bake);
        return bake;
    }

    // Salvage armed glow: the icon/logo's own silhouette, blurred and tinted to the slice colour, laid
    // UNDER the glyph so it reads as back-lit. Two stacked layers (dense core + wide halo), with the core
    // OVERDRAWN: pushing alpha alone flattens the falloff toward solid colour, whereas repeating the draw
    // compounds the same gradient and keeps the bloom's shape while gaining strength.
    private const double SalvageGlowCoreAlpha = 0.55;
    private const double SalvageGlowHaloAlpha = 0.42;
    private const int    SalvageGlowCorePasses = 2;
    private static readonly Dictionary<Color, Brush> SalvageIconGlowCache = new();

    // One frozen default-stretch ImageBrush per ImageSource — several per-frame paths (opacity masks,
    // tint fills) need "this image as a brush" and were allocating a fresh ImageBrush every frame.
    // Callers that set Stretch/Transform/etc. can't use this; those need their own keyed cache.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, ImageBrush>
        ImageBrushCache = new();
    private static ImageBrush BrushOf(ImageSource img) =>
        ImageBrushCache.GetValue(img, static i => { var b = new ImageBrush(i); b.Freeze(); return b; });

    // Same idea for the crop-to-fill cover-art fill (Stretch differs, so it can't share BrushOf's cache).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, ImageBrush>
        CoverBrushCache = new();
    private static ImageBrush CoverBrushOf(ImageSource img) =>
        CoverBrushCache.GetValue(img, static i =>
        { var b = new ImageBrush(i) { Stretch = Stretch.UniformToFill }; b.Freeze(); return b; });

    /// <summary>Tinted, blurred halo behind an ARMED salvage icon/logo. Reuses the cached silhouette blur
    /// (see <see cref="SilhouetteBlurBitmaps"/>) as an OPACITY MASK over a flat tint, so one bake per image
    /// serves every slice colour and no blur runs per frame. Centred on the glyph — unlike the reactor
    /// shadow, a glow must not be offset — and deliberately UNCLIPPED: the halo spilling onto neighbouring
    /// slices is what sells it as light rather than paint.</summary>
    /// <param name="coreAlpha">Overrides the core/halo strength — Reactor shares this helper at a much
    /// lower alpha (its glow only has to tint a white glyph, not light a charcoal plate).</param>
    private static void DrawSalvageIconGlow(DrawingContext dc, ImageSource img, Rect rect, Color tint,
                                            double coreAlpha = SalvageGlowCoreAlpha,
                                            double haloAlpha = SalvageGlowHaloAlpha,
                                            int corePasses = SalvageGlowCorePasses)
    {
        if (!SalvageIconGlowCache.TryGetValue(tint, out var brush))
        {
            // Whiten the slice colour a touch: a saturated hue alone muddies against the charcoal, and the
            // crescent's glow does the same (see SalvageGlowPens) — the two cues should match.
            static byte Mix(byte v) => (byte)(v + (255 - v) * 0.30);
            brush = Frozen(new SolidColorBrush(Color.FromRgb(Mix(tint.R), Mix(tint.G), Mix(tint.B))));
            SalvageIconGlowCache[tint] = brush;
        }
        var bake = SilhouetteBlurBitmaps(img);
        double s = rect.Width / bake.FitW;   // uniform (rect.Height / bake.FitH is the same ratio)
        var dst = new Rect(rect.X - (SilhouetteBlurCanvas - bake.FitW) / 2.0 * s,
                           rect.Y - (SilhouetteBlurCanvas - bake.FitH) / 2.0 * s,
                           SilhouetteBlurCanvas * s, SilhouetteBlurCanvas * s);
        for (int k = 0; k < 2; k++)
        {
            int passes = k == 0 ? corePasses : 1;
            for (int p = 0; p < passes; p++)
            {
                dc.PushOpacity(k == 0 ? coreAlpha : haloAlpha);
                dc.PushOpacityMask(BrushOf(bake.Maps[k == 0 ? BlurTight : BlurWide]));   // alpha of the blurred silhouette
                dc.DrawRectangle(brush, null, dst);
                dc.Pop();
                dc.Pop();
            }
        }
    }

    // Salvage armed cast shadow: the crescent glowing on the INNER rim is the light source, so the glyph
    // throws a soft shadow radially OUTWARD (away from the wheel centre) rather than south. Clipped to the
    // wedge — a cast shadow belongs to the surface it falls on, so it stops at the slice edge even though
    // the glow itself is allowed to spill.
    private const double SalvageCastShadowPx    = 10.0;
    private const double SalvageCastShadowAlpha = 0.55;

    /// <summary>Salvage tilts every glyph a little — nothing on a scavenged panel is straight. Hand-authored
    /// rather than hashed: a hash deals near-identical angles to neighbouring slices, which reads as
    /// alignment, not randomness. 24 distinct values in ±10°, twelve per wheel, laid out so EVERY adjacent
    /// pair differs by ≥6° — including the 11→0 wraparound, and including the wraparound of any shorter
    /// wheel (a 6-slice wheel uses entries 0-5 and wraps 5→0).</summary>
    private static readonly double[] SalvageTiltsA =
        [-8.5, 4.0, -2.5, 9.0, -6.0, 1.5, -9.5, 6.5, -4.5, 8.0, -1.0, 5.0];
    private static readonly double[] SalvageTiltsB =
        [7.5, -5.5, 2.0, -9.0, 5.5, -3.0, 10.0, -7.0, 3.5, -10.0, 0.5, -6.5];

    /// <summary>Which wheel this control is drawing — the two wheels use different tilt tables so a slice
    /// in the same position on each doesn't lean the same way. Set by App on every open.</summary>
    public bool IsWheelB { get; set; }

    private double SalvageTilt(int i)
    {
        var table = IsWheelB ? SalvageTiltsB : SalvageTiltsA;
        return table[((i % table.Length) + table.Length) % table.Length];
    }

    private static void DrawSalvageCastShadow(DrawingContext dc, ImageSource img, Rect rect,
                                              double midDeg, Geometry? clip)
    {
        var bake = SilhouetteBlurBitmaps(img);
        double s = rect.Width / bake.FitW;   // uniform (rect.Height / bake.FitH is the same ratio)
        double rad = midDeg * Math.PI / 180.0;
        var dst = new Rect(rect.X - (SilhouetteBlurCanvas - bake.FitW) / 2.0 * s + Math.Cos(rad) * SalvageCastShadowPx,
                           rect.Y - (SilhouetteBlurCanvas - bake.FitH) / 2.0 * s + Math.Sin(rad) * SalvageCastShadowPx,
                           SilhouetteBlurCanvas * s, SilhouetteBlurCanvas * s);
        if (clip is not null) dc.PushClip(clip);
        dc.PushOpacity(SalvageCastShadowAlpha);
        dc.DrawImage(bake.Maps[BlurCast], dst);   // 33px blur — soft enough to read as ambient occlusion
        dc.Pop();
        if (clip is not null) dc.Pop();
    }

    // The 9-tap spread is POSITION-INDEPENDENT (the taps are pure translations of one masked rect), so
    // the whole composite bakes to a bitmap: one DrawImage per glyph per frame replaces nine stacked
    // opacity-mask layers (each an intermediate render surface). Keyed per (image, brush, box size) —
    // boxes are stable per wheel layout, so this is a handful of bakes per session, like RimMask.
    private const double ShadowTapSpreadPx = 2.0;   // max |dx|,|dy| in ShadowTaps
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource,
        Dictionary<(Brush brush, int w, int h), BitmapSource>> ShapeShadowCache = new();

    private BitmapSource ShapeShadowBake(ImageSource img, Brush shadow, double rectW, double rectH)
    {
        var perImage = ShapeShadowCache.GetOrCreateValue(img);
        var key = (shadow, (int)Math.Round(rectW), (int)Math.Round(rectH));
        if (perImage.TryGetValue(key, out var cached)) return cached;
        // Supersample at the display scale (min 1.5×) so the blit is indistinguishable from the live
        // masked draws it replaces — a shadow this soft hides any residual resampling.
        double ss = Math.Max(1.5, VisualTreeHelper.GetDpi(this).DpiScaleX);
        double cw = rectW + ShadowTapSpreadPx * 2, ch = rectH + ShadowTapSpreadPx * 2;
        var mask = BrushOf(img);
        var dv = new DrawingVisual();
        using (var bdc = dv.RenderOpen())
        {
            foreach (var (dx, dy) in ShadowTaps)
            {
                bdc.PushOpacityMask(mask);
                bdc.DrawRectangle(shadow, null,
                    new Rect(ShadowTapSpreadPx + dx, ShadowTapSpreadPx + dy, rectW, rectH));
                bdc.Pop();
            }
        }
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(cw * ss), (int)Math.Ceiling(ch * ss),
                                         96.0 * ss, 96.0 * ss, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        perImage[key] = rtb;
        return rtb;
    }

    private void DrawShapeShadow(DrawingContext dc, ImageSource img, Rect rect, double offsetScale = 1.0,
                                 double opacity = 1.0, Brush? brushOverride = null, double? throwRad = null)
    {
        // Base offset behind the icon: normally mostly straight down, as if one light hung over the whole
        // wheel. No reactor branch: reactor armed draws no silhouette shadow at all now, and its unarmed
        // slices use the blurred blob instead.
        // throwRad overrides the DIRECTION (salvage rest throws toward the wheel centre — see the call
        // site). Same length as the default (0.5, 1.5) vector, so a directional shadow reads exactly as
        // deep as a straight-down one at the same offsetScale.
        const double DefaultThrowLen = 1.5811;   // |(0.5, 1.5)|
        double bx = throwRad is { } r ? Math.Cos(r) * DefaultThrowLen * offsetScale : 0.5 * offsetScale;
        double by = throwRad is { } r2 ? Math.Sin(r2) * DefaultThrowLen * offsetScale : 1.5 * offsetScale;
        Brush shadow = brushOverride                                                    // salvage rest (heavier)
                     ?? (_material == "mesa" ? ContentShadowBrushHalf                  // half again on Terra
                     : DarkMaterial ? ContentShadowBrush                                // full on dark
                     : ContentShadowBrushLight);                                        // 1/3 on the other light materials
        var bake = ShapeShadowBake(img, shadow, rect.Width, rect.Height);
        if (opacity < 0.999) dc.PushOpacity(opacity);
        dc.DrawImage(bake, new Rect(rect.X + bx - ShadowTapSpreadPx, rect.Y + by - ShadowTapSpreadPx,
                                    rect.Width + ShadowTapSpreadPx * 2, rect.Height + ShadowTapSpreadPx * 2));
        if (opacity < 0.999) dc.Pop();
    }

    // Unit directions for the erosion mask stack (4 orthogonal + 4 diagonal at unit length).
    private static readonly (double dx, double dy)[] ErodeDirs =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (0.7071, 0.7071), (-0.7071, 0.7071), (0.7071, -0.7071), (-0.7071, -0.7071),
    };

    /// <summary>A TRUE inner edge stroke, uniform on every edge (a scale-inset stroke thins to nothing on
    /// edges near the icon's centre — scaling shrinks about the centre rather than eroding every boundary
    /// equally). Morphological identity: the stroke rim =
    /// silhouette − eroded silhouette, and EROSION = the intersection of the alpha mask translated in all
    /// directions by the stroke width — which WPF expresses natively because nested PushOpacityMask layers
    /// MULTIPLY. So: (1) fill the whole silhouette with the stroke colour; (2) push 8 opacity masks of the
    /// same image, each shifted <paramref name="w"/>px in one compass direction; (3) let the caller draw
    /// the real full-size content — it survives only where ALL shifted copies overlap (the eroded core),
    /// leaving a consistent ~w px stroke rim ON the edge, entirely inside the original silhouette.</summary>
    // Per-mask, per-stroke-width brush set for DrawInnerEdged (1 straight + 8 shifted) — building ~9
    // ImageBrushes per icon per FRAME was a hot-path allocation churn; the offsets depend only on w.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BitmapSource, Dictionary<double, ImageBrush[]>>
        EdgeBrushCache = new();

    private static void DrawInnerEdged(DrawingContext dc, ImageSource img, Rect rect, Brush stroke, double w,
                                       Action<Rect> drawContent)
    {
        // Masks come from a BINARIZED opaque silhouette, not the image itself: tinted glyphs render with
        // a translucent tint brush (~78% alpha), and multiplying that alpha through 8 nested masks
        // (0.78^8 ≈ 0.14) annihilated the content draw — every glyph read as a solid stroke-colour blob.
        var mask = OpaqueMask(img);
        var perW = EdgeBrushCache.GetOrCreateValue(mask);
        if (!perW.TryGetValue(w, out var brushes))
        {
            brushes = new ImageBrush[ErodeDirs.Length + 1];
            brushes[0] = new ImageBrush(mask);
            brushes[0].Freeze();
            for (int k = 0; k < ErodeDirs.Length; k++)
            {
                var (dx, dy) = ErodeDirs[k];
                var mb = new ImageBrush(mask) { Transform = new TranslateTransform(dx * w, dy * w) };
                mb.Freeze();
                brushes[k + 1] = mb;
            }
            perW[w] = brushes;
        }
        dc.PushOpacityMask(brushes[0]);
        dc.DrawRectangle(stroke, null, rect);            // whole silhouette in the stroke colour
        dc.Pop();
        for (int k = 0; k < ErodeDirs.Length; k++)       // erosion = product of shifted (0/1) alpha masks
            dc.PushOpacityMask(brushes[k + 1]);
        drawContent(rect);                               // full-size content, clipped to the eroded core
        for (int k = 0; k < ErodeDirs.Length; k++) dc.Pop();
    }

    // ── Outer-rim distance-field bake ─────────────────────────────────────────
    // EUCLIDEAN DISTANCE TRANSFORM bake: once per (glyph, rim width, box size), compute each pixel's true
    // distance to the hole-filled silhouette on a 256px-long-edge grid, then map "distance ≤ w" through a
    // ~1.5px soft ramp. Geometrically round at sharp points, antialiased everywhere, one masked draw per
    // frame. (Discrete dilation — a union of offset copies — scallops at sharp points and staircases at
    // the edges; don't go back to it.)
    private static readonly System.Runtime.CompilerServices
        .ConditionalWeakTable<ImageSource, Dictionary<(double w, int rw, int rh), ImageBrush>> RimMaskCache = new();

    private const double RimAaPx        = 1.5;   // the alpha ramp's width, in device px
    private const int    RimBakeLongEdge = 256;  // bake resolution (long edge of the padded box)

    /// <summary>A TRUE OUTER edge stroke: the glyph's hole-filled silhouette dilated by <paramref name="w"/>px
    /// and filled with <paramref name="stroke"/>, drawn BEHIND the content so a uniform rim shows all the way
    /// around the OUTSIDE (kawaii's puffy-sticker look; terra's terracotta rim) and the stroke colour backs
    /// any enclosed empty space inside the glyph. One masked rectangle draw per call; the mask itself is a
    /// cached distance-field bake (see <see cref="RimMask"/>).</summary>
    private static void DrawOuterEdged(DrawingContext dc, ImageSource img, Rect rect, Brush stroke, double w)
    {
        if (rect.Width < 1 || rect.Height < 1) return;
        var brush = RimMask(img, w, rect.Width, rect.Height);
        double pad = w + RimAaPx;   // the bake canvas grew by this much on every side — mirror it here
        var inflated = new Rect(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2);
        dc.PushOpacityMask(brush);          // brush maps onto the layer's bounds = the inflated rect
        dc.DrawRectangle(stroke, null, inflated);
        dc.Pop();
    }

    /// <summary>The rim's alpha mask for one (glyph, rim width, content-box size): silhouette + enclosed
    /// holes at full alpha, ramping to 0 over <see cref="RimAaPx"/> once the distance to the silhouette
    /// passes <paramref name="w"/>. The canvas keeps the padded box's ASPECT (uniform scale), so mask-space
    /// distances convert to device px by one factor and the rim stays equally thick on both axes of a
    /// non-square logo box. Box dims are cache-keyed at whole-px granularity — icon boxes are stable per
    /// wheel config, so this stays a handful of bakes per session.</summary>
    private static ImageBrush RimMask(ImageSource img, double w, double rectW, double rectH)
    {
        var perImage = RimMaskCache.GetOrCreateValue(img);
        var key = (w, (int)Math.Round(rectW), (int)Math.Round(rectH));
        if (perImage.TryGetValue(key, out var cached)) return cached;

        double pad  = w + RimAaPx;
        double devW = rectW + pad * 2, devH = rectH + pad * 2;
        double ss   = RimBakeLongEdge / Math.Max(devW, devH);   // ONE scale for both axes → isotropic distances
        int cw = Math.Max(8, (int)Math.Round(devW * ss));
        int ch = Math.Max(8, (int)Math.Round(devH * ss));

        // 1) Silhouette: the glyph rendered into the padded canvas, binarized (OpaqueMask's threshold —
        //    a translucent tinted logo must still yield a solid rim).
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawImage(img, new Rect(pad * ss, pad * ss, rectW * ss, rectH * ss));
        var rtb = new RenderTargetBitmap(cw, ch, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var px = new byte[cw * ch * 4];
        rtb.CopyPixels(px, cw * 4, 0);
        var solid = new bool[cw * ch];
        for (int i = 0; i < solid.Length; i++) solid[i] = px[i * 4 + 3] >= 64;

        // 2) Fill enclosed holes (letter counters, gaps in a logo mark): flood the OUTSIDE from the
        //    border; whatever the flood can't reach is interior and joins the silhouette, so the rim
        //    colour backs the glyph's empty space.
        var outside = new bool[cw * ch];
        var work = new Stack<int>();
        void Seed(int x, int y)
        {
            int i = y * cw + x;
            if (!outside[i] && !solid[i]) { outside[i] = true; work.Push(i); }
        }
        for (int x = 0; x < cw; x++) { Seed(x, 0); Seed(x, ch - 1); }
        for (int y = 0; y < ch; y++) { Seed(0, y); Seed(cw - 1, y); }
        while (work.Count > 0)
        {
            int i = work.Pop(); int x = i % cw, y = i / cw;
            if (x > 0)      Seed(x - 1, y);
            if (x < cw - 1) Seed(x + 1, y);
            if (y > 0)      Seed(x, y - 1);
            if (y < ch - 1) Seed(x, y + 1);
        }
        for (int i = 0; i < solid.Length; i++) solid[i] |= !outside[i];

        // 3) Exact squared Euclidean distance to the silhouette, then the soft-thresholded rim alpha.
        var dist2 = SquaredEdt(solid, cw, ch);
        var outPx = new byte[cw * ch * 4];
        for (int i = 0; i < solid.Length; i++)
        {
            double dDev = Math.Sqrt(dist2[i]) / ss;   // device px from the silhouette (0 inside)
            double a = Math.Clamp((w + RimAaPx - dDev) / RimAaPx, 0.0, 1.0);
            byte v = (byte)Math.Round(a * 255);
            outPx[i * 4] = outPx[i * 4 + 1] = outPx[i * 4 + 2] = outPx[i * 4 + 3] = v;   // premultiplied white
        }
        var bmp = BitmapSource.Create(cw, ch, 96, 96, PixelFormats.Pbgra32, null, outPx, cw * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp);
        brush.Freeze();
        return perImage[key] = brush;
    }

    /// <summary>Exact squared Euclidean distance transform (Felzenszwalb &amp; Huttenlocher): for every
    /// pixel, the squared distance to the nearest <paramref name="solid"/> pixel. Two separable 1D passes
    /// (columns, then rows) of the lower-envelope-of-parabolas algorithm — O(n) per line.</summary>
    private static double[] SquaredEdt(bool[] solid, int cw, int ch)
    {
        const double Inf = 1e12;
        var f = new double[cw * ch];
        for (int i = 0; i < f.Length; i++) f[i] = solid[i] ? 0.0 : Inf;

        int n = Math.Max(cw, ch);
        var line = new double[n]; var d = new double[n];
        var v = new int[n]; var z = new double[n + 1];

        void Edt1D(int count)
        {
            int k = 0; v[0] = 0; z[0] = double.NegativeInfinity; z[1] = double.PositiveInfinity;
            for (int q = 1; q < count; q++)
            {
                double s;
                while (true)
                {
                    s = (line[q] + (double)q * q - (line[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                    if (s <= z[k]) k--; else break;
                }
                k++;
                v[k] = q; z[k] = s; z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < count; q++)
            {
                while (z[k + 1] < q) k++;
                d[q] = (q - v[k]) * (double)(q - v[k]) + line[v[k]];
            }
        }

        for (int x = 0; x < cw; x++)   // columns
        {
            for (int y = 0; y < ch; y++) line[y] = f[y * cw + x];
            Edt1D(ch);
            for (int y = 0; y < ch; y++) f[y * cw + x] = d[y];
        }
        for (int y = 0; y < ch; y++)   // rows
        {
            for (int x = 0; x < cw; x++) line[x] = f[y * cw + x];
            Edt1D(cw);
            for (int x = 0; x < cw; x++) f[y * cw + x] = d[x];
        }
        return f;
    }

    /// <summary>Kawaii's content drop shadow: the icon/logo INCLUDING its white outer rim, offset slightly
    /// down-right. Same distance-field silhouette as the rim itself; the PushOpacity group keeps the brush
    /// opaque so the shadow reads as one flat shape whatever the mask's edge alpha does.</summary>
    private static void DrawKawaiiContentShadow(DrawingContext dc, ImageSource img, Rect rect, double w)
    {
        dc.PushOpacity(KawaiiShadowAlpha);
        dc.PushTransform(new TranslateTransform(KawaiiShadowDx, KawaiiShadowDy));
        DrawOuterEdged(dc, img, rect, KawaiiShadowBrush, w);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>Mesa's content drop shadow — the same shape as Kawaii's (icon/logo INCLUDING its rim, one flat
    /// offset copy of the dilated silhouette) in Mesa's own tone and thrown straight down. See
    /// <see cref="TerraShadowAlpha"/> for why it exists and why it isn't <see cref="DrawShapeShadow"/>.</summary>
    private static void DrawTerraContentShadow(DrawingContext dc, ImageSource img, Rect rect, double w)
    {
        dc.PushOpacity(TerraShadowAlpha);
        dc.PushTransform(new TranslateTransform(TerraShadowDx, TerraShadowDy));
        DrawOuterEdged(dc, img, rect, TerraContentShadowBrush, w);
        dc.Pop();
        dc.Pop();
    }

    // Binarized opaque silhouette per source image, cached by instance (icons are created once per
    // populate and reused every frame, so the bake is a one-off per icon).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, BitmapSource>
        MaskCache = new();
    private static BitmapSource OpaqueMask(ImageSource img)
    {
        if (MaskCache.TryGetValue(img, out var cached)) return cached;
        const int px = 128;   // mask resolution — ample for the ~66px wheel draws
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) dc.DrawImage(img, new Rect(0, 0, px, px));
        var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var buf = new byte[px * px * 4];
        rtb.CopyPixels(buf, px * 4, 0);
        for (int p = 0; p < buf.Length; p += 4)
        {
            // Alpha over the threshold → fully-opaque white (premultiplied), else fully transparent.
            byte v = buf[p + 3] >= 64 ? (byte)255 : (byte)0;
            buf[p] = buf[p + 1] = buf[p + 2] = buf[p + 3] = v;
        }
        var bmp = BitmapSource.Create(px, px, 96, 96, PixelFormats.Pbgra32, null, buf, px * 4);
        bmp.Freeze();
        MaskCache.Add(img, bmp);
        return bmp;
    }

    // (Hole-filling for a glyph's enclosed empty space lives in the outer-rim distance-field bake — see
    // RimMask. The INNER-edge erosion (reactor) deliberately keeps the un-filled OpaqueMask: erosion inside
    // a filled counter would paint stroke over glyph area that reads as background.)

    /// <summary>Bake the inner-edged icon into a bitmap (2× supersampled) for STATIC hosts — the Settings
    /// icon-well preview — so it uses the exact same erosion technique as the wheel.</summary>
    internal static ImageSource RenderEdgedIcon(ImageSource img, double sizePx, Brush stroke, double w)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            DrawInnerEdged(dc, img, new Rect(0, 0, sizePx, sizePx), stroke, w, r => dc.DrawImage(img, r));
        return Bake(dv, sizePx);
    }

    /// <summary>The OUTER-rim counterpart of <see cref="RenderEdgedIcon"/> (terra's terracotta rim), so the
    /// Settings icon well shows the same treatment the wheel draws instead of a bare glyph.
    /// <para>The rim dilates OUTWARD, so the bake canvas has to grow by <paramref name="w"/> on every side —
    /// drawing into a rect the size of the host Image would clip the rim on all four edges. The host Image
    /// scales the wider bitmap back down, which is what keeps the rim-to-glyph PROPORTION equal to the
    /// wheel's: the wheel's nominal icon box (<see cref="IconSize"/>, 66) is already ~the well's 67px, so
    /// <paramref name="w"/> transfers with no rescaling.</para></summary>
    internal static ImageSource RenderOuterEdgedIcon(ImageSource img, double sizePx, Brush stroke, double w)
    {
        var rect = new Rect(w, w, sizePx, sizePx);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            DrawOuterEdged(dc, img, rect, stroke, w);
            dc.DrawImage(img, rect);
        }
        return Bake(dv, sizePx + w * 2);
    }

    /// <summary>Bake a GLYPH exactly as the given material's slices draw it, for a static host (the Settings
    /// icon well). This — not the caller — owns the material→treatment mapping, so the well can't fall behind
    /// the wheel: adding or changing a material's icon treatment here updates both surfaces at once.
    /// <para>Not reproduced (they depend on live per-slice state the well has no notion of): the armed
    /// lighten washes, salvage's tilt + back-light glow, and reactor's rest disc / blurred drop shadow.
    /// Thin-ring edge variants are skipped too — slice thickness isn't pushed to the well.</para>
    /// <para><paramref name="rimBase"/> is the slice's AUTHORED colour, used to derive Mesa's per-slice
    /// hue-matched rim; null (or an achromatic colour) falls back to the shared umber. Callers that know the
    /// colour should always pass it — otherwise the well shows a rim the wheel won't draw.</para></summary>
    internal static ImageSource RenderPreviewIcon(ImageSource img, double sizePx, string? material,
                                                  Color? rimBase = null) => NormalizeMaterial(material) switch
    {
        "mesa"   => RenderTerraPreview(img, sizePx, TerraRimBrushFor(rimBase)),
        "kawaii"  => RenderKawaiiPreview(img, sizePx, tint: null),
        "reactor" => RenderEdgedIcon(img, sizePx, ReactorEdgeBrush, ReactorIconEdgeInset),
        _         => img,
    };

    /// <summary>The logo counterpart of <see cref="RenderPreviewIcon"/> — aspect-fits and tints the way the
    /// wheel does, then applies the same per-material treatment.</summary>
    internal static ImageSource RenderPreviewLogo(ImageSource logo, double sizePx, Brush tint, string? material,
                                                  Color? rimBase = null) =>
        NormalizeMaterial(material) switch
        {
            "mesa"   => RenderTerraPreview(logo, sizePx, TerraRimBrushFor(rimBase), tint),
            "kawaii"  => RenderKawaiiPreview(logo, sizePx, tint),
            "reactor" => RenderTintedLogo(logo, sizePx, tint, ReactorEdgeBrush, ReactorIconEdgeInset),
            _         => RenderTintedLogo(logo, sizePx, tint),
        };

    /// <summary>Mesa's treatment for a preview: the drop shadow of the content plus its rim, then the rim,
    /// then the content — the same order and helpers a slice uses. <paramref name="tint"/> non-null aspect-fits
    /// and tints a logo; null draws an already-tinted glyph bitmap as-is. (Mirrors
    /// <see cref="RenderKawaiiPreview"/>; the two materials are the outer-rim-plus-shadow pair.)</summary>
    private static ImageSource RenderTerraPreview(ImageSource img, double sizePx, Brush rim, Brush? tint = null) =>
        RenderRimmedPreview(img, sizePx, TerraIconEdgePx, TerraShadowDx, TerraShadowDy,
                            DrawTerraContentShadow, rim, tint);

    /// <summary>Kawaii's puffy-sticker treatment for a preview: the rim's own drop shadow, then the white
    /// outer rim, then the content — the same order and helpers a slice uses. <paramref name="tint"/> non-null
    /// aspect-fits and tints a logo; null draws an already-tinted glyph bitmap as-is.</summary>
    private static ImageSource RenderKawaiiPreview(ImageSource img, double sizePx, Brush? tint) =>
        RenderRimmedPreview(img, sizePx, KawaiiIconEdgePx, KawaiiShadowDx, KawaiiShadowDy,
                            DrawKawaiiContentShadow, KawaiiIconEdgeBrush, tint);

    /// <summary>Shared outer-rim-plus-shadow preview scaffold (Terra and Kawaii). Padded by the rim width
    /// plus the shadow's own offset (+1 for the shadow's antialiased edge row), so neither is clipped at the
    /// canvas edge. <paramref name="tint"/> non-null aspect-fits and tints a logo; null draws an
    /// already-tinted glyph bitmap as-is.</summary>
    private static ImageSource RenderRimmedPreview(ImageSource img, double sizePx, double edgePx,
        double shadowDx, double shadowDy, Action<DrawingContext, ImageSource, Rect, double> drawShadow,
        Brush rim, Brush? tint)
    {
        double pad = edgePx + Math.Max(shadowDx, shadowDy) + 1.0;
        Rect rect;
        if (tint is not null)
        {
            double aspect = img.Width > 0 && img.Height > 0 ? img.Width / img.Height : 1.0;
            double w = aspect >= 1 ? sizePx : sizePx * aspect;
            double h = aspect >= 1 ? sizePx / aspect : sizePx;
            rect = new Rect(pad + (sizePx - w) / 2, pad + (sizePx - h) / 2, w, h);
        }
        else rect = new Rect(pad, pad, sizePx, sizePx);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            drawShadow(dc, img, rect, edgePx);
            DrawOuterEdged(dc, img, rect, rim, edgePx);
            if (tint is not null)
            {
                dc.PushOpacityMask(new ImageBrush(img));
                dc.DrawRectangle(tint, null, rect);
                dc.Pop();
            }
            else dc.DrawImage(img, rect);
        }
        return Bake(dv, sizePx + pad * 2);
    }

    /// <summary>Rasterize a preview visual at 2× for crisp scaling in a fixed-size host.</summary>
    private static ImageSource Bake(DrawingVisual dv, double logicalSize)
    {
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(logicalSize * 2), (int)Math.Ceiling(logicalSize * 2),
                                         192, 192, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>Bake a game LOGO the way the wheel paints it — the image's alpha masking a solid tint fill —
    /// aspect-fitted inside a square box, for the Settings icon-well preview. The aspect fit is the point:
    /// the slice editor's logo arrows are only usable if the well shows each candidate's real SHAPE, and a
    /// wide wordmark vs. a square emblem is the difference the user is choosing between. Pass
    /// <paramref name="edge"/> to also trace the material's edge stroke — <paramref name="outerEdge"/>
    /// selects the OUTER rim (terra's terracotta, matching <see cref="RenderOuterEdgedIcon"/>) over the
    /// erosion-based inner stroke (<see cref="RenderEdgedIcon"/>). 2× supersampled.</summary>
    internal static ImageSource RenderTintedLogo(ImageSource logo, double sizePx, Brush tint,
                                                Brush? edge = null, double edgeW = 0, bool outerEdge = false)
    {
        // An outer rim needs room outside the logo box, so pad the canvas — same reason as
        // RenderOuterEdgedIcon (the host Image scales the result back down).
        double pad = edge is not null && edgeW > 0 && outerEdge ? edgeW : 0;
        double aspect = logo.Width > 0 && logo.Height > 0 ? logo.Width / logo.Height : 1.0;
        double w = aspect >= 1 ? sizePx : sizePx * aspect;
        double h = aspect >= 1 ? sizePx / aspect : sizePx;
        var rect = new Rect(pad + (sizePx - w) / 2, pad + (sizePx - h) / 2, w, h);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            void DrawTinted(Rect r)
            {
                dc.PushOpacityMask(new ImageBrush(logo));
                dc.DrawRectangle(tint, null, r);
                dc.Pop();
            }
            if (edge is not null && edgeW > 0)
            {
                if (outerEdge) { DrawOuterEdged(dc, logo, rect, edge, edgeW); DrawTinted(rect); }
                else           DrawInnerEdged(dc, logo, rect, edge, edgeW, DrawTinted);
            }
            else DrawTinted(rect);
        }
        return Bake(dv, sizePx + pad * 2);
    }

    /// <summary>Overlay a translucent wash, masked to the icon/logo silhouette — lightens the drawn image.
    /// Call immediately AFTER drawing the image. Defaults to the flat ~10% white wash (terra / gloss-light
    /// armed slices); pass a hue-preserving brush (see <see cref="ActionTint.Lighten"/>) for a material
    /// that should brighten without washing toward grey (salvage armed).</summary>
    private static void DrawIconLighten(DrawingContext dc, ImageSource img, Rect rect, Brush? wash = null)
    {
        dc.PushOpacityMask(BrushOf(img));   // per-frame path — the frozen shared brush, not a fresh one
        dc.DrawRectangle(wash ?? IconLightenBrush, null, rect);
        dc.Pop();
    }

    /// <summary>The shared bowed-horizon radial brush (centre on the slice's top edge, light 16° left).</summary>
    /// <para>RADIAL (not linear) so the horizon BOWS instead of running straight: the gradient centre sits on
    /// the slice's top edge, so iso-value arcs dip lower at the slice's middle than at its sides — a shallow
    /// bowl. RadiusY = 1 maps the ramp top→bottom; RadiusX = 2 (wide) keeps the bow gentle. Centre is offset
    /// LEFT so the sheen reads as an overhead light 16° off the vertical axis: from the slice centre
    /// (0.5,0.5) up to the top edge is 0.5, so cx = 0.5 − 0.5·tan(16°) ≈ 0.357.</para>
    private static Brush BowedGloss((double o, Color c)[] ramp)
    {
        var b = new RadialGradientBrush
        {
            MappingMode    = BrushMappingMode.RelativeToBoundingBox,
            Center         = new Point(0.357, 0.0),   // top edge; light 16° left of vertical
            GradientOrigin = new Point(0.357, 0.0),
            RadiusX        = 2.0,                      // wide → shallow horizontal curvature
            RadiusY        = 1.0,                      // ramp spans top (0) → bottom (1)
        };
        foreach (var (o, c) in ramp) b.GradientStops.Add(new GradientStop(c, o));
        b.Freeze();
        return b;
    }

    /// <summary>A resting fill brush for a material token, for the onboarding "wheel look" preview tiles —
    /// built from the SAME ramps/solids the wheel renders with, so a swatch matches the real slice. Callers
    /// pass a CANONICAL token (the tiles' own Tag, or a config value — <c>ConfigLoader.Sanitize</c>
    /// normalizes those on load); anything unrecognised falls through to Pearl, as
    /// <see cref="SetSliceMaterial"/> does.</summary>
    public static Brush PreviewFill(string? material) => material switch
    {
        // The tile's button face: the theme's resting fill (which may itself be a gradient), with the
        // package's tile texture composited over it if present — see CustomTheme.TileFill.
        _ when Materials.CustomFor(material) is { } custom =>
            CustomTheme.For(material!)?.TileFill ?? Frozen(new SolidColorBrush(Color.FromArgb(
                (byte)(custom.RestingArgb >> 24), (byte)(custom.RestingArgb >> 16),
                (byte)(custom.RestingArgb >> 8), (byte)custom.RestingArgb))),
        "obsidian" => BowedGloss(GlossDarkRamp),
        "flat-dark"  => DarkFlatFill,
        "flat" or "flat-white" or "flat-light" => SliceFill,
        // 2026 styles: opaque swatch approximations (the real fills are translucent over the game —
        // a tile needs a representative solid so it reads on the Settings background).
        "kawaii"     => Frozen(new SolidColorBrush(Color.FromRgb(0xF7, 0xDD, 0xEC))),   // representative pastel pink (the live fill walks the ring)
        "salvage"     => Frozen(new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C))),
        "mesa"       => Frozen(new SolidColorBrush(Color.FromRgb(0xF2, 0xED, 0xE3))),
        "reactor"     => Frozen(new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1A))),
        _            => BowedGloss(GlossLightRamp),
    };

    /// <summary>True when a material token uses light ink on a dark slice (so a preview tile flips its
    /// label colour to match).</summary>
    public static bool PreviewIsDark(string? material) => Materials.IsDark(material);

    /// <summary>Resting fill for the slice at <paramref name="index"/> of <paramref name="count"/> — for
    /// previews that represent ONE slice rather than the material in general (the slice editor's icon
    /// well). Only Kawaii differs per slice: its hue walks the ring, so a single swatch can't stand for
    /// it. Every other material returns the same brush <see cref="PreviewFill"/> does. Falls back to the
    /// generic fill when the position is unknown (index &lt; 0) or the wheel is empty.</summary>
    public static Brush PreviewFillForSlice(string? material, int index, int count) =>
        material == "kawaii" && index >= 0 && count > 0
            ? KawaiiRestFill(360.0 / count * index - 90.0)   // same midDeg the wheel lays slices out on
            : PreviewFill(material);

    // ── Material preview-tile decorations ─────────────────────────────────────
    // A flat fill can't tell Salvage from Flat Dark, or Mesa from Flat Light. These overlays give the
    // four styled materials their signature on the tile itself, built from the SAME sources the wheel
    // renders with. Shared by BOTH tile surfaces (Settings ▸ Customize and the onboarding Material step)
    // so the two can never drift; only the corner radius differs, hence the parameter.
    // internal: the Game Grid's Mesa selection ring + accents reuse the same terracotta.
    internal static readonly Brush TerraTileStroke = Frozen(new SolidColorBrush(Color.FromRgb(201, 106, 59)));
    private static Brush? _reactorTileBrush;
    private static Brush? _kawaiiTileStarFill, _kawaiiTileHeartFill;
    /// <summary>Kawaii's lavender accent — the mini-wheel glyph colour on a selected Slice-Thickness tile,
    /// and its material tile's bottom edge. Public so Customize reads it from here rather than keeping a
    /// second copy of the hex.</summary>
    public static readonly Color KawaiiAccent = Color.FromRgb(0xB9, 0xA5, 0xF5);
    private static readonly Brush KawaiiTileLipFill = Frozen(new SolidColorBrush(KawaiiAccent));
    private static readonly Brush ReactorTileLipFill = Frozen(new SolidColorBrush(Color.FromRgb(0, 0, 0)));
    // Salvage's tile sits on a lifted plate: a heavy black bottom edge that follows the rounded corners.
    private static readonly Brush SalvageTileLipFill = Frozen(new SolidColorBrush(Color.FromArgb(215, 0, 0, 0)));
    // Reactor's hairline: a pale inset rule, like a board's silkscreen keepout line. Alpha kept low —
    // brighter reads as a hard white edge against the dark board.
    private static readonly Brush ReactorTileHairline = Frozen(new SolidColorBrush(Color.FromArgb(158, 244, 246, 250)));
    // A decoration is the tile Border's CHILD, so it lays out INSIDE that border and stops 1px short of
    // the tile's edge. Both tile surfaces rest at 1px, so a matching negative margin puts full-bleed
    // decorations back on the true edge. (Border doesn't clip its child, so the overhang paints over the
    // tile's own rest border — which is the intent: the material's edge IS the tile's edge. A selected
    // tile thickens to 3px and the decoration then sits 2px inside its selection ring, which reads fine.)
    private const double PreviewTileBorderPx = 1.0;

    /// <summary>Subtle drop shadow under a material tile's label — the styled materials' textures and art
    /// cost the text contrast a flat swatch gave it for free. Cached per shadow colour: kawaii casts in
    /// its own deep plum (black reads as grime against those pastels), everything else in black.</summary>
    /// <remarks><paramref name="opacityScale"/> multiplies the per-colour alpha — the Material grid's FIRST
    /// ROW passes 0.75 (see CustomizeEditorControl.MaterialTopRowShadowScale).
    /// The cache is keyed on colour, scale AND the dark flag, so every strength coexists.
    /// <para>DARK materials (Flat Dark, Obsidian, Salvage, Reactor) carry a HEAVIER black shadow: their
    /// labels are near-white on a near-black tile, where a 0.32 black shadow does almost nothing. The light
    /// tiles keep the subtler value tuned for dark-on-light.</para></remarks>
    private static readonly Dictionary<(Color, double, bool), System.Windows.Media.Effects.Effect> PreviewLabelShadowCache = new();
    public static System.Windows.Media.Effects.Effect PreviewLabelShadow(string? material, double opacityScale = 1.0)
    {
        var c = material == "kawaii" ? Color.FromRgb(0x9B, 0x6F, 0xC7) : Colors.Black;
        bool dark = Materials.IsDark(material);
        var key = (c, Math.Round(opacityScale, 3), dark);
        if (PreviewLabelShadowCache.TryGetValue(key, out var cached)) return cached;
        var fx = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = c, BlurRadius = 3.5, ShadowDepth = 3.0, Direction = 270,
            // lighter/more-purple orchid now carries less alpha too
            Opacity = (c != Colors.Black ? 0.45 : dark ? 0.62 : 0.32) * key.Item2,
        };
        fx.Freeze();
        return PreviewLabelShadowCache[key] = fx;
    }

    /// <summary>Applies a material tile's own display typeface to its name label:
    /// the four materials that swap the wheel's slice-label face show their NAME in that same face, so the
    /// tile is a sample of the look and not just a swatch. Mesa → Jua, Salvage → condensed Bahnschrift,
    /// Kawaii → Sour Gummy, Reactor → Share Tech (its labels and hub share one face). Every other material
    /// is left exactly as the caller styled it — the inherited Segoe UI SemiBold.
    /// <para>Weight comes from the typeface, not the caller's SemiBold: all three bundled faces ship Regular
    /// only, and WPF's synthesized bold wrecks them (see <see cref="ReactorHubFace"/> / <see
    /// cref="TerraLabelFace"/>). Shared by the Customize grid and the onboarding Look step so the two can't
    /// drift.</para>
    /// <para>A tile that takes a display face also takes the two corrections below
    /// (<see cref="PreviewFaceSizeMul"/> / <see cref="PreviewFaceDropPx"/>) — the same
    /// em-size-is-not-apparent-size problem <c>LabelSizeMul</c> solves in the wheel, plus a baseline nudge,
    /// since these faces sit higher in their em box than Segoe UI and the names read misaligned against the
    /// four plain tiles otherwise. Salvage's name is additionally UPPER-CASED, matching how the material draws
    /// its slice labels.</para></summary>
    public static void ApplyPreviewLabelFace(System.Windows.Controls.TextBlock label, string? material)
    {
        string mat = Materials.Normalize(material);
        Typeface? face = mat switch
        {
            "mesa"    => TerraLabelFace,
            "salvage" => BahnschriftAvailable ? SalvageLabelFace : null,   // no Bahnschrift → keep Segoe UI
            "kawaii"  => KawaiiFace,
            "reactor" => ReactorHubFace,
            _ when Materials.CustomFor(mat) is { } customSpec => CustomTheme.FaceFor(customSpec.LabelFont),
            _         => null,
        };
        if (Materials.CustomFor(mat)?.LabelUpper == true) label.Text = label.Text.ToUpperInvariant();
        // Salvage's caps are about the material's look, not its typeface, so they apply even when Bahnschrift
        // is missing and the name stays in the fallback Segoe UI.
        if (mat == "salvage") label.Text = label.Text.ToUpperInvariant();
        if (Loc.UsesSystemFace) { label.FontFamily = LocWpf.LanguageFamily; return; }   // the material face has no glyphs for this script
        if (face is null) return;
        label.FontFamily  = face.FontFamily;
        label.FontStyle   = face.Style;
        label.FontWeight  = face.Weight;
        label.FontStretch = face.Stretch;
        label.FontSize   *= PreviewFaceSizeMulFor(mat);
        // Seeds the baseline drop for the moment between build and the first SelectTile pass. After that
        // ApplyPreviewLabelLift owns this property outright and folds the same drop in — see the warning
        // there. Both must agree, which is why both go through PreviewFaceDropFor.
        label.RenderTransform = new TranslateTransform(0, PreviewFaceDropFor(mat));
    }

    /// <summary>Size/baseline corrections for a tile name set in a material's own display face:
    /// 10% larger, and pushed DOWN so its baseline lines up with the plain tiles' Segoe UI.
    /// The 3px drop exactly cancels the premium lift, i.e. these four names sit where a plain tile's does.
    /// Salvage takes one px more, since its name is upper-cased and so has no descenders or
    /// lowercase to balance the optical centre.</summary>
    private const double PreviewFaceSizeMul = 1.10, PreviewFaceDropPx = 3.0, SalvageFaceDropPx = 4.0;

    /// <summary>Extra bump on top of <see cref="PreviewFaceSizeMul"/> for Mesa's tile name specifically —
    /// 10% bigger than the other three styled materials' names, which stay at the shared 1.10×.</summary>
    private const double MesaPreviewFaceSizeMul = 1.10;

    private static double PreviewFaceSizeMulFor(string material) =>
        material == "mesa" ? PreviewFaceSizeMul * MesaPreviewFaceSizeMul : PreviewFaceSizeMul;

    /// <summary>How far DOWN a tile name is pushed for <paramref name="material"/> — 0 for the materials that
    /// keep Segoe UI, which get no correction because they define the baseline the others are matching.
    /// Takes an ALREADY-NORMALIZED token (both callers have one in hand).</summary>
    private static double PreviewFaceDropFor(string material) => material switch
    {
        "salvage"                     => SalvageFaceDropPx,
        "mesa" or "kawaii" or "reactor" => PreviewFaceDropPx,
        _                             => 0.0,
    };

    /// <summary>Lift a selected material tile's label 2px — a RenderTransform, so
    /// it shifts visually without re-running layout or disturbing the decoration underneath. Shared by both
    /// tile surfaces; finds the label whether the tile holds it directly or inside a decoration Grid.</summary>
    public static void ApplyPreviewLabelLift(System.Windows.Controls.Border tile, bool selected)
    {
        var label = tile.Child as System.Windows.Controls.TextBlock
                 ?? (tile.Child as System.Windows.Controls.Grid)?.Children
                        .OfType<System.Windows.Controls.TextBlock>().FirstOrDefault();
        if (label is null) return;
        // PREMIUM materials sit their label 3px north ALWAYS, clearing their coloured bottom edge; the
        // selected lift then stacks 1px more on top of that, so selection still reads as a change on
        // every tile. Read from the tile's own Tag, so both surfaces get it without extra plumbing.
        double lift = (Materials.IsPremium(tile.Tag as string)
                       || Materials.CustomFor(tile.Tag as string)?.Tile?.Lifted == true ? 3.0 : 0.0)
                      + (selected ? 1.0 : 0.0);
        // ⚠ This method is the SOLE owner of the label's RenderTransform — it re-runs on every selection
        // change, so anything else that sets the property is silently reverted the first time a tile is
        // picked. The display-face baseline drop is therefore folded in here, net of the lift.
        double y = PreviewFaceDropFor(Materials.Normalize(tile.Tag as string)) - lift;
        label.RenderTransform = y != 0 ? new TranslateTransform(0, y) : null;
    }

    /// <summary>The PREMIUM materials' shared tile signature: a 5px bottom edge in the material's own
    /// accent, following the tile's corner radius. One factory so all four stay identical in weight and
    /// technique — they're meant to read as a set.</summary>
    private const double PremiumTileEdgePx = 5.0;
    private static System.Windows.Controls.Border PremiumTileEdge(double cornerRadius, Brush ink) =>
        new()
        {
            CornerRadius = new CornerRadius(cornerRadius),
            BorderBrush = ink, BorderThickness = new Thickness(0, 0, 0, PremiumTileEdgePx),
            IsHitTestVisible = false,
        };

    /// <summary>Decoration overlaid on a material preview tile, or null for the materials whose flat fill
    /// already reads correctly. Sized to the tile by the caller's layout, so it works at either tile size.</summary>
    public static UIElement? PreviewDecoration(string? material, double cornerRadius)
    {
        switch (material)
        {
            case "mesa":
                // Just the shared premium edge, in terracotta — anything heavier outweighs every other
                // tile in the grid.
                return PremiumTileEdge(cornerRadius, TerraTileStroke);
            case "kawaii":
            {
                // The material's OWN confetti shapes (BuildFxStar / BuildFxHeart — the hub twinkle and the
                // fire burst use them): a yellow star top-right and a blue heart bottom-left, both
                // white-rimmed so they read on the pale pink swatch.
                const double r = 11.0;
                _kawaiiTileStarFill  ??= Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD9, 0x4A)));
                _kawaiiTileHeartFill ??= Frozen(new SolidColorBrush(Color.FromRgb(0x5B, 0xB0, 0xF2)));
                var sg = new System.Windows.Controls.Grid { IsHitTestVisible = false };
                sg.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = BuildFxStar(new Point(r, r), r, 0),
                    Fill = _kawaiiTileStarFill,
                    Stroke = Brushes.White, StrokeThickness = 2.0,
                    StrokeLineJoin = PenLineJoin.Round,   // a star's points spike under a miter
                    Width = r * 2, Height = r * 2,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 6, 8, 0),
                });
                sg.Children.Add(new System.Windows.Shapes.Path
                {
                    // BuildFxHeart is built around the origin with negative coordinates, so this one is
                    // fitted by Stretch rather than positioned like the star's centred geometry.
                    Data = BuildFxHeart(10.0), Stretch = Stretch.Uniform,
                    Fill = _kawaiiTileHeartFill,
                    Stroke = Brushes.White, StrokeThickness = 2.0, StrokeLineJoin = PenLineJoin.Round,
                    Width = 18, Height = 17,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(9, 0, 0, 7),
                });
                sg.Children.Add(PremiumTileEdge(cornerRadius, KawaiiTileLipFill));
                return sg;
            }
            // NOTE: these two deliberately do NOT take the negative margin. Their brushes are TRANSLUCENT
            // and the tile's charcoal background stops at the border, so bleeding them over the tile's
            // 1px rest border paints grain onto GREY instead of onto the material — verified in rendered
            // pixels. Staying inside also keeps the grey unselected ring these tiles share with the other
            // five. Terra is the exception because its decoration is an opaque stroke standing in for
            // that ring.
            case "salvage":
            {
                var vg = new System.Windows.Controls.Grid { IsHitTestVisible = false };
                // The rusted-metal grain, in its tile variant (see SalvageTileTexture).
                vg.Children.Add(new System.Windows.Controls.Border
                {
                    CornerRadius = new CornerRadius(cornerRadius), Background = SalvageTileTexture(),
                });
                vg.Children.Add(PremiumTileEdge(cornerRadius, SalvageTileLipFill));   // the shared premium edge
                return vg;
            }
            case "reactor":
            {
                var rg = new System.Windows.Controls.Grid { IsHitTestVisible = false };
                rg.Children.Add(new System.Windows.Controls.Border
                {
                    CornerRadius = new CornerRadius(cornerRadius), Background = ReactorTileBrush(),
                });
                // Hairline inset 3px from the TILE's edge — the decoration already sits 1px in, so the
                // margin makes up the remaining 2. Radius shrinks with the inset to stay concentric.
                // Its BOTTOM sits 3px higher still, clearing the premium bottom edge that would otherwise
                // paint straight over that run of it.
                rg.Children.Add(new System.Windows.Controls.Border
                {
                    Margin = new Thickness(3 - PreviewTileBorderPx, 3 - PreviewTileBorderPx,
                                           3 - PreviewTileBorderPx, 3 - PreviewTileBorderPx + 3),
                    CornerRadius = new CornerRadius(Math.Max(0, cornerRadius - 3)),
                    BorderBrush = ReactorTileHairline, BorderThickness = new Thickness(1),
                });
                rg.Children.Add(PremiumTileEdge(cornerRadius, ReactorTileLipFill));
                return rg;
            }
            default:
                // Custom drop-in themes: tile treatment from the package's tile block — the premium
                // bottom edge in the theme's accent, and/or a soft top sheen (a data-shaped nod to the
                // gloss tiles). Absent block (or neither flag) → plain tile, like the flats.
                if (Materials.CustomFor(material) is { Tile: { } tileSpec })
                {
                    var cg = new System.Windows.Controls.Grid { IsHitTestVisible = false };
                    if (tileSpec.Sheen)
                        cg.Children.Add(new System.Windows.Controls.Border
                        {
                            CornerRadius = new CornerRadius(cornerRadius),
                            Background = (Brush)new LinearGradientBrush(
                                Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                                new Point(0.5, 0), new Point(0.5, 0.75)).GetAsFrozen(),
                        });
                    if (tileSpec.EdgeArgb is { } edge)
                        cg.Children.Add(PremiumTileEdge(cornerRadius, Frozen(new SolidColorBrush(
                            Color.FromArgb((byte)(edge >> 24), (byte)(edge >> 16), (byte)(edge >> 8), (byte)edge)))));
                    return cg.Children.Count > 0 ? cg : null;
                }
                return null;
        }
    }

    private static Pen FrozenRoundPen(Brush b, double w)
    {
        var p = new Pen(b, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        p.Freeze();
        return p;
    }

    // Subtle wheel-wide glass dome, drawn OVER the slices: a soft overhead sheen on the top of the disc
    // fading to a faint darken at the bottom (the curve), plus a thin top-rim specular catch.
    private static readonly Brush WheelDomeFill = BuildWheelDome();
    // Shared rim-catch pen for BOTH the outer (top) and inner (bottom) catches: a gradient ALONG the arc
    // (bbox is horizontal: ends = the arc's ends, centre = its apex) — fades to transparent at both ends
    // and dips a little at the apex, so each catch tapers off at its tips and eases off toward its extreme.
    // Round caps soften the (already-faded) tips. Width 4.8 (doubled).
    private static readonly Pen   WheelRimPen = MakeRimPen();
    private static Pen MakeRimPen()
    {
        var c = GlossDarkPalette.Edge;   // the Game Grid dark-gloss teal/cool highlight tone
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(c, 0),  0.00));   // one end → fades out
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(c, 92), 0.22));
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(c, 72), 0.50));   // apex → eased off
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(c, 92), 0.78));
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(c, 0),  1.00));   // other end → fades out
        g.Freeze();
        var p = new Pen(g, 3.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        p.Freeze();
        return p;
    }

    private static Brush BuildWheelDome()
    {
        var b = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            StartPoint  = new Point(0.5, 0.0),   // top of the disc
            EndPoint    = new Point(0.5, 1.0),   // bottom
        };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(34, 255, 255, 255), 0.00));   // soft sheen up top
        b.GradientStops.Add(new GradientStop(Color.FromArgb(14, 255, 255, 255), 0.16));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0,  255, 255, 255), 0.42));   // clear through the middle
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0,    0,   0,   0), 0.58));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(16,   0,   0,   0), 0.85));   // faint bottom shade
        b.GradientStops.Add(new GradientStop(Color.FromArgb(40,   0,   0,   0), 1.00));
        b.Freeze();
        return b;
    }

    private void DrawWheelDome(DrawingContext dc, Point center)
    {
        dc.DrawEllipse(WheelDomeFill, null, center, OuterRadius, OuterRadius);              // overhead sheen + shade

        // Rim catches: outer along the top, inner along the bottom (the inner edge of the lower slices
        // faces up toward the overhead light). Clip to the slice wedges so the catch breaks at the gaps
        // rather than drawing across them; the gradient pen still spans each full arc so the taper holds.
        int n = Slices.Count;
        if (n <= 0) return;
        double sliceDeg = 360.0 / n;
        // Clip the catch to slightly-narrower wedges (wider gap) so it stops a little short of each slice's
        // visible edge instead of running right to it.
        const double catchEdgeMargin = 3.0;                 // px short of the visible slice edge
        double clipGap = GapPx + 2 * catchEdgeMargin;
        // The N-wedge clip group and both rim-catch arcs depend only on the layout — cached; rebuilding
        // them was N+2 StreamGeometry allocations per frame on pearl/obsidian.
        var domeKey = (n, _sliceInner, center.X, center.Y);
        if (_domeClip is null || _domeKey != domeKey)
        {
            var clip = new GeometryGroup();
            for (int i = 0; i < n; i++)
            {
                double mid = sliceDeg * i - 90;
                clip.Children.Add(BuildWedge(center, _sliceInner, OuterRadius, mid - sliceDeg / 2, mid + sliceDeg / 2, clipGap));
            }
            clip.Freeze();
            _domeClip = clip;
            _domeOuterArc = BuildArc(center, OuterRadius - 4, -162, 144);
            _domeInnerArc = BuildArc(center, _sliceInner + 4, 30, 120);
            _domeOuterArc.Freeze();
            _domeInnerArc.Freeze();
            _domeKey = domeKey;
        }

        dc.PushClip(_domeClip);
        dc.DrawGeometry(null, WheelRimPen, _domeOuterArc);  // outer top-rim specular
        dc.DrawGeometry(null, WheelRimPen, _domeInnerArc);  // inner bottom-rim catch
        dc.Pop();
    }
    private (int n, double inner, double cx, double cy) _domeKey;
    private Geometry? _domeClip, _domeOuterArc, _domeInnerArc;

    /// <summary>Terra material only: an irregular terracotta backdrop blob behind the whole wheel (a
    /// perturbed circle, larger than the wheel) with a hard offset cutout shadow underneath it — sells the
    /// "wheel is a paper cutout sitting on a backing card" look. Called once per frame, before any slices.</summary>
    private (double cx, double cy) _blobKey;   // the blob draws every frame — cache per centre
    private Geometry? _blobGeo;
    private bool _blobDonut;      // whether the cached blob has the centre hole punched out
    private double _blobHoleKey;  // …and at which (quantized) hole radius, since it animates with the hub
    private double _blobRKey;     // …and at which (quantized) outer radius, since the arcade grows it
    /// <summary>How far terra's backdrop hole closes once the hub is fully in: 0.5 = half its resting
    /// radius — closes the uneven gap between hub rim and donut rim.</summary>
    private const double TerraDonutHubClose = 0.5;
    /// <summary>How far proud of the GROWN hub the blob sits during an arcade session. The hub is sized to
    /// the playfield and the blob's resting radius lands within a pixel or two of it, so without this the
    /// ground disappears behind the game's own field instead of framing it.</summary>
    private const double TerraArcadeBlobOversize = 1.06;
    /// <summary>Drop-in theme wheel-wide underlay: the package's backdrop image, aspect-preserving
    /// cover-fit, clipped to a soft circle just past the ring (Terra's blob reach) at the manifest's
    /// opacity. Static per frame — no animation, so Reduce Motion has nothing to gate.</summary>
    private void DrawCustomBackdrop(DrawingContext dc, Point center)
    {
        var img = _custom!.BackdropImage!;
        double r = OuterRadius + 26;
        dc.PushClip(new EllipseGeometry(center, r, r));
        dc.PushOpacity(_custom.BackdropOpacity);
        double side = r * 2;
        double scale = Math.Max(side / img.Width, side / img.Height);
        double w = img.Width * scale, h = img.Height * scale;
        dc.DrawImage(img, new Rect(center.X - w / 2, center.Y - h / 2, w, h));
        dc.Pop();
        dc.Pop();
    }

    private void DrawTerraBackdrop(DrawingContext dc, Point center)
    {
        // Arcade: the hub grows past the wheel to sit behind the playfield, and the ground grows with it so
        // a band of terracotta still reads on every side of the game.
        double R = Math.Max(OuterRadius + 16.0, HubRadius * TerraArcadeBlobOversize);
        // Hub gone → centre gone: with the resting hub hidden, the terracotta/
        // texture backdrop is a DONUT like the other materials' backglow, not a full disc showing through
        // the wheel's open middle. The hole's edge is the same wobbled cut as the outer rim (its own seed),
        // and its resting radius mirrors the blob's 16px overhang inward of the slice ring.
        //
        // The hole then CLOSES TO HALF as the hub arrives, driven by the hub's own eased presence scale —
        // so it tightens in lockstep with the hub growing (and re-opens as it leaves, at the faster exit
        // rate, for free) instead of leaving a wide uneven gap between the hub's rim and the donut's.
        // A DiscFreeNotice (the practice pill, the edit hint) is a badge floating in an empty centre with
        // no hub disc under it, so there is no rim for the hole to close up to — it stays at rest.
        bool donut = HideCenter;
        double hubClose = DiscFreeNotice ? 0.0 : Math.Clamp(_hubFrameScale, 0.0, 1.0);
        double restHoleR = Math.Max(24.0, _sliceInner - 16.0);
        double holeR = restHoleR * (1.0 - TerraDonutHubClose * hubClose);
        // Quantized so the mid-transition rebuilds are a handful of geometries, not one per frame per
        // sub-pixel; outside a transition this settles and the cache holds as before.
        double holeKey = donut ? Math.Round(holeR * 2.0) / 2.0 : 0.0;
        double rKey    = Math.Round(R * 2.0) / 2.0;   // quantized for the same reason as the hole
        if (_blobGeo is null || _blobKey != (center.X, center.Y) || _blobDonut != donut
            || _blobHoleKey != holeKey || _blobRKey != rKey)
        {
            Geometry g = BuildWobbleRing(center, R, amp: 7, segs: 40, seed: 4242);
            if (donut)
            {
                var cg = new CombinedGeometry(GeometryCombineMode.Exclude, g,
                    BuildWobbleRing(center, holeKey, amp: 7, segs: 26, seed: 4243));
                cg.Freeze();
                g = cg;
            }
            _blobGeo = g;
            _blobKey = (center.X, center.Y);
            _blobDonut = donut;
            _blobHoleKey = holeKey;
            _blobRKey = rKey;
        }
        dc.PushTransform(new TranslateTransform(2, 3));
        dc.DrawGeometry(TerraShadow, null, _blobGeo);   // cutout shadow, offset
        dc.Pop();
        dc.DrawGeometry(HubOpaqueLight, null, _blobGeo); // opaque body — see the plate block
        dc.DrawGeometry(TerraBlobFill, null, _blobGeo);  // the blob itself
        // …then the seamless ground tile over it, drawn on the SAME geometry so the blob's wobbly edge clips
        // the texture for free. Skipped (silently) if the asset failed to decode.
        if (TerraGroundBrush(center, R) is { } ground) dc.DrawGeometry(ground, null, _blobGeo);
    }

    // Build the resting fill for one shape. Glass materials use a per-slice radial "glass button" gradient
    // (bowed horizon); flat materials are a solid fill. (center is unused now but kept for call-site symmetry.)
    private Brush ShapeFill(Rect bounds, Point center)
    {
        if (_material == "obsidian")  return GlossDarkFill();   // dark glass button (bowed horizon)
        if (_material == "pearl") return GlossLightFill();  // light glass button (bowed horizon)
        if (_material == "kawaii")     return KawaiiHubFill;    // milky pink-white (slices use the per-slice hue fill in DrawSlice)
        if (_material == "salvage")     return SalvageFill;       // solid charcoal
        if (_material == "mesa")       return TerraFill;         // cream paper
        // Reactor resting wedges paint NOTHING now (see ReactorHollowRest) — the glyph's own disc is the
        // whole slice. Only the SLICE path reaches here; the hub keeps its own fills (see DrawCenter).
        if (_material == "reactor")     return ReactorHollowRest ? Brushes.Transparent
                                                                 : ReactorRestGradient(center);
        if (_custom is { } custom)       return custom.Resting;   // drop-in theme (palette-only)
        return DarkMaterial ? DarkFlatFill : SliceFill;           // flat-dark / flat-light solid fills
    }

    /// <summary>Resting outline for the current material (SlicePen unless the style says otherwise;
    /// terra is deliberately UNSTROKED — its cut edge + shadow carry the shape).</summary>
    private Pen? RestingPenFor() => _custom is { } custom ? (custom.RestingPen ?? SlicePen) : _material switch
    {
        "kawaii"                     => KawaiiRestPen,
        "salvage"                     => SalvagePen,
        "mesa"                       => null,
        "reactor"                     => null,   // unarmed reactor wedges are unstroked
        _                             => SlicePen,
    };

    /// <summary>Armed outline for the current material (chunky black on Terra;
    /// salvage keeps its resting edge — the white inner crescent is its armed cue).</summary>
    private Pen? ArmedPenFor() => _custom is { } custom ? (custom.ArmedPen ?? ArmedPen) : _material switch
    {
        "kawaii"                     => KawaiiArmedPen,
        "salvage"                     => SalvagePen,
        "mesa"                       => TerraArmedPen,
        "reactor"                     => ReactorFusedPen,   // heavy continuous stroke around the fused keyhole
        _                             => ArmedPen,
    };

    // ── 2026 style materials (kawaii / salvage / terra / reactor) — specs measured from reference captures ──
    // ── KAWAII ("Dream Sky"): milky wedges each carrying a whisper of a
    //    pastel hue that WALKS the ring (pink → peach → butter → mint → sky → lavender → pink); the armed
    //    slice saturates its own hue and gains a pastel-rainbow rim arc; the hub is a cloud scallop; hub
    //    pills are ribbon banners; guarded slices fill bottom-up with "strawberry milk"; firing bursts
    //    heart/star confetti. Glyph tints stay TintSet.Light — saturated glyphs read on milky wedges. ──
    // White sticker-ish outline — round joins so the rounded wedge corners don't spike.
    private static readonly Pen   KawaiiRestPen  = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), 4.0);
    private static readonly Pen   KawaiiArmedPen = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 5.0);
    // Armed slice lifts + grows like Terra's 3D card, minus the card edge. Unlike terra it moves PURELY
    // radially — no screen-up nudge; kawaii's slice scales straight out from the wheel centre.
    private const double KawaiiLiftPx    = 17.0;   // pushes further out than terra's 10
    private const double KawaiiNorthPx   = 0.0;
    private const double KawaiiArmedScale = 1.08;
    // White OUTER edge stroke around every icon/logo — a puffy-sticker rim
    // OUTSIDE the silhouette, not the erosion-based INNER stroke terra/reactor use. Nominal width matches
    // KawaiiRestPen (4.0); note a slice's pen is centred on its path, so only ~2px of it shows outside —
    // halve this if the icon rim reads heavier than the slice rim.
    private const double KawaiiIconEdgePx = 4.0;
    private static readonly Brush KawaiiIconEdgeBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)));
    // Terra's icon/logo rim: an OUTER stroke around every slice's content — the same dilation Kawaii's
    // white rim uses, not an erosion-based inner stroke.
    // internal, not private: the Settings icon-well preview renders through RenderOuterEdgedIcon and must
    // use THESE values, not its own copies — duplicates go stale silently when the wheel's treatment changes.
    internal const double TerraIconEdgePx = 1.60;
    // The rim COLOUR is per-slice (see TerraRimBrushFor and ActionTint.MesaRimColor): each slice's rim takes
    // its own action type's hue at a dark umber L/S, so the outline belongs to the glyph it wraps. This fixed
    // burnt umber — the SAME tone as the armed-slice outline (TerraArmedPen's 110,56,30), deliberately NOT
    // the lighter terracotta of the backdrop blob — is the FALLBACK for an achromatic authored colour, which
    // has no hue to match, and the static preview default.
    internal static readonly Brush TerraIconEdgeBrush = Frozen(new SolidColorBrush(Color.FromRgb(110, 56, 30)));
    private static readonly Dictionary<Color, Brush> TerraRimBrushCache = new();

    /// <summary>Mesa's rim brush for one slice: its authored colour's hue at the rim's fixed dark umber
    /// (<see cref="ActionTint.MesaRimColor"/>), falling back to the shared <see cref="TerraIconEdgeBrush"/>
    /// when the authored colour is achromatic. Cached per colour — this is called per slice per frame.</summary>
    private static Brush TerraRimBrushFor(WheelSlice slice) =>
        TerraRimBrushFor(ActionTint.AuthoredColor(slice));

    /// <summary>As <see cref="TerraRimBrushFor(WheelSlice)"/>, from a bare authored colour — for the static
    /// preview bakes, which have a colour but no slice. Null <paramref name="authored"/> means the caller
    /// doesn't know it, so the shared umber stands in.</summary>
    private static Brush TerraRimBrushFor(Color? authored)
    {
        if (authored is not { } a || ActionTint.MesaRimColor(a) is not { } c) return TerraIconEdgeBrush;
        if (TerraRimBrushCache.TryGetValue(c, out var b)) return b;
        var s = new SolidColorBrush(c);
        s.Freeze();
        return TerraRimBrushCache[c] = s;
    }

    // Mesa's content drop shadow — the rim is thin enough that the content needs lifting off the cream.
    // Shape follows DrawKawaiiContentShadow — the shadow of the glyph PLUS its rim, one flat offset copy of
    // the same dilated silhouette — not DrawShapeShadow's 9-tap feathered spread, which would sit under an
    // opaque rim and mostly vanish. Thrown STRAIGHT DOWN, matching the one-light-over-the-wheel direction
    // the other materials assume (DrawShapeShadow's default vector).
    // Brush is OPAQUE with PushOpacity supplying the alpha, for the same reason Kawaii's is.
    private const double TerraShadowAlpha = 0.24;
    private const double TerraShadowDx = 0.0, TerraShadowDy = 3.5;
    private static readonly Brush TerraContentShadowBrush = Frozen(new SolidColorBrush(Color.FromRgb(30, 20, 10)));   // TerraShadow's tone, opaque
    // Content drop shadow: the shadow of the icon PLUS its white rim (the whole sticker), on EVERY slice —
    // armed or not. Drawn as the same dilated silhouette, offset. The brush is
    // OPAQUE and the whole thing rides inside a PushOpacity group: DrawOuterEdged lays down 12 overlapping
    // copies, so a translucent brush would compound alpha (dark core, pale rim) instead of reading as one
    // flat shadow.
    private const double KawaiiShadowAlpha = 0.16;
    private const double KawaiiShadowDx = 0.5, KawaiiShadowDy = 2.0;
    private static readonly Brush KawaiiShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 40, 24, 48)));   // opaque plum-black; PushOpacity supplies the alpha
    private static readonly Brush KawaiiHubFill  = Frozen(new SolidColorBrush(Color.FromArgb(246, 253, 246, 250)));   // milky pink-white cloud
    private static readonly Pen   KawaiiHubPen   = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 232, 213, 238)), 2.0);   // soft orchid rim
    private static readonly Brush KawaiiArmedFallback = Frozen(new SolidColorBrush(Color.FromArgb(246, 255, 183, 217)));   // pastel pink (non-slice ArmedFillFor callers)
    private static readonly Brush KawaiiArrowBrush    = Frozen(new SolidColorBrush(Color.FromRgb(0xF4, 0x91, 0xC1)));      // hub state-change arrow
    private static readonly Brush KawaiiInk      = Frozen(new SolidColorBrush(Color.FromArgb(255, 63, 43, 79)));      // deep plum label ink (≥7:1 on the milky wedges)
    private static readonly Dictionary<Color, Brush> KawaiiLabelBrushCache = new();

    /// <summary>Kawaii's per-slice LABEL ink: the slice's own authored colour — the
    /// swatch it was picked from, or the hex typed into the editor — used raw, with none of the material's
    /// pastel transform applied. Contrast is therefore whatever the authored colour gives against the milky
    /// wedge, deliberately: this is a chosen look, not the ≥7:1 <see cref="KawaiiInk"/> plum
    /// (which stays as the fallback for any caller with no slice, e.g. the hub readout).
    /// <para>Cached per colour — called per slice per frame, same as
    /// <see cref="TerraRimBrushFor(WheelSlice)"/>, and for the same reason.</para></summary>
    private static Brush KawaiiLabelBrushFor(WheelSlice slice)
    {
        var c = ActionTint.AuthoredColor(slice);
        if (KawaiiLabelBrushCache.TryGetValue(c, out var b)) return b;
        var s = new SolidColorBrush(c);
        s.Freeze();
        return KawaiiLabelBrushCache[c] = s;
    }
    // Guarded (hold-to-confirm) dwell: the wedge fills inner→outer with "strawberry milk" and fires at the
    // brim (replaces the red dwell arc on this material); a thin white line marks the milk's surface.
    private static readonly Brush KawaiiMilkFill    = Frozen(new SolidColorBrush(Color.FromArgb(242, 255, 178, 208)));
    private static readonly Pen   KawaiiMilkEdgePen = FrozenPen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1.5);
    // Armed rim arc: a pastel rainbow along the armed wedge's outer edge (gradient runs along the arc's bbox).
    private static readonly Pen KawaiiRimPen = MakeKawaiiRimPen();
    private static Pen MakeKawaiiRimPen()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        g.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 158, 201), 0.00));   // pink
        g.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 224, 122), 0.34));   // butter
        g.GradientStops.Add(new GradientStop(Color.FromArgb(255, 143, 232, 208), 0.66));   // mint
        g.GradientStops.Add(new GradientStop(Color.FromArgb(255, 185, 165, 245), 1.00));   // lilac
        g.Freeze();
        var p = new Pen(g, 5.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        p.Freeze();
        return p;
    }
    // ── Kawaii sloshing milk surface (D1) + arm glitter sweep (D4) ─────────────────

    /// <summary>The milk pour's fill + surface as ONE wavy boundary: two octaves of animated sine on
    /// the fill radius. Overshoots the wedge span; the caller's wedge clip trims it. Sampled at ~3°.</summary>
    private static (Geometry fill, Geometry surface) BuildKawaiiSlosh(
        Point c, double r, double midDeg, double sliceDeg, double ampEase)
    {
        double t = NowMs / 1000.0;
        double half = sliceDeg / 2.0 + 8;                    // past the wedge; clip trims
        double a1 = 3.4 * ampEase, a2 = 1.5 * ampEase;       // broad swell + counter-ripple (px)
        var pts = new List<Point>();
        for (double d = midDeg - half; d <= midDeg + half + 0.001; d += 3.0)
        {
            double rad = d * Math.PI / 180.0;
            double rr = r + a1 * Math.Sin(rad * 5.0 + t * 2.1) + a2 * Math.Sin(rad * 11.0 - t * 3.7);
            pts.Add(new Point(c.X + Math.Cos(rad) * rr, c.Y + Math.Sin(rad) * rr));
        }
        var fill = new StreamGeometry();
        using (var ctx = fill.Open())
        {
            ctx.BeginFigure(c, isFilled: true, isClosed: true);   // centre fan under the surface
            foreach (var p in pts) ctx.LineTo(p, isStroked: false, isSmoothJoin: true);
        }
        fill.Freeze();
        var surface = new StreamGeometry();
        using (var ctx = surface.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: false, isClosed: false);
            for (int k = 1; k < pts.Count; k++) ctx.LineTo(pts[k], isStroked: true, isSmoothJoin: true);
        }
        surface.Freeze();
        return (fill, surface);
    }

    // D4 glitter: a one-shot sweep of tiny white sparkles across the freshly-armed wedge. Cheap by
    // construction — ≤12 hashed positions, each two short crossed lines, alive ~225 ms.
    private int _kawaiiGlitterIndex = -1;
    private DateTime _kawaiiGlitterUtc;
    private const int KawaiiGlitterMs = 300;
    private static readonly Pen KawaiiGlitterPen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), 1.4);

    private void DrawKawaiiGlitter(DrawingContext dc, Point center, double midDeg, double sliceDeg, int i)
    {
        double ms = (NowUtc - _kawaiiGlitterUtc).TotalMilliseconds;
        if (ms < 0 || ms >= KawaiiGlitterMs) return;
        // Centre-out sweep: "front" is a DISTANCE FROM THE WEDGE'S ANGULAR CENTRE (u=0.5), 0..0.5, so the
        // sweep starts at the middle and expands toward both edges simultaneously.
        double front = 0.5 * (ms / KawaiiGlitterMs);
        for (int k = 0; k < 12; k++)
        {
            unchecked
            {
                uint h = (uint)(k * 374761393 + i * 668265263) ^ 0x51EDu;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                double u = (h & 0xFFFF) / 65535.0;                 // angular position within the wedge
                double v = ((h >> 16) & 0xFFFF) / 65535.0;         // radial position within the band
                double uDistFromMid = Math.Abs(u - 0.5);           // 0 at wedge centre, 0.5 at either edge
                // Bell-curve opacity around the passing sweep front; each sparkle glints once.
                double dist = Math.Abs(uDistFromMid - front);
                if (dist > 0.18) continue;
                double glint = 1.0 - dist / 0.18;
                double deg = midDeg - sliceDeg / 2.0 + u * sliceDeg;
                double rad = deg * Math.PI / 180.0;
                double rr  = _sliceInner + (0.15 + 0.7 * v) * (OuterRadius - _sliceInner);
                var p = new Point(center.X + Math.Cos(rad) * rr, center.Y + Math.Sin(rad) * rr);
                double s = 2.0 + 2.5 * glint;
                dc.PushOpacity(glint);
                dc.DrawLine(KawaiiGlitterPen, new Point(p.X - s, p.Y), new Point(p.X + s, p.Y));
                dc.DrawLine(KawaiiGlitterPen, new Point(p.X, p.Y - s), new Point(p.X, p.Y + s));
                dc.Pop();
            }
        }
    }

    // Per-slice rest/armed fills: the same bowed-gloss geometry as Gloss Light, with the ramp re-anchored
    // on the slice's own walking hue. Cached per rounded hue degree × state (a wheel has ≤12 hues).
    private static readonly Dictionary<int, Brush> _kawaiiRestCache = new(), _kawaiiArmedCache = new();
    /// <summary>The pastel hue (deg) for a slice centred at <paramref name="midDeg"/>: pink at 12 o'clock,
    /// walking the full wheel once around the ring.</summary>
    private static double KawaiiHueFor(double midDeg) => (((330.0 + midDeg + 90.0) % 360.0) + 360.0) % 360.0;
    private static Brush KawaiiRestFill(double midDeg)
    {
        int key = (int)Math.Round(KawaiiHueFor(midDeg));
        if (_kawaiiRestCache.TryGetValue(key, out var b)) return b;
        double h = key;
        (double o, Color c)[] ramp =
        {
            (0.00, HslColor(250, h, 0.30, 0.975)),   // milky sheen at the top
            (0.10, HslColor(248, h, 0.38, 0.945)),
            (0.50, HslColor(246, h, 0.48, 0.895)),   // gentle horizon — the whisper of the hue
            (1.00, HslColor(238, h, 0.52, 0.865)),
        };
        return _kawaiiRestCache[key] = BowedGloss(ramp);
    }
    private static Brush KawaiiArmedFill(double midDeg)
    {
        int key = (int)Math.Round(KawaiiHueFor(midDeg));
        if (_kawaiiArmedCache.TryGetValue(key, out var b)) return b;
        double h = key;
        (double o, Color c)[] ramp =   // the slice's own hue saturated (~3× the rest chroma)
        {
            (0.00, HslColor(252, h, 0.55, 0.93)),
            (0.10, HslColor(250, h, 0.68, 0.86)),
            (0.47, HslColor(246, h, 0.80, 0.78)),
            (0.50, HslColor(248, h, 0.85, 0.72)),   // anchor (pink slice ≈ #FFB7D9-class)
            (0.65, HslColor(244, h, 0.78, 0.77)),
            (1.00, HslColor(238, h, 0.80, 0.74)),
        };
        return _kawaiiArmedCache[key] = BowedGloss(ramp);
    }
    /// <summary>HSL → WPF Color (h in degrees, s/l 0..1) — kawaii's hue-walk fills.</summary>
    private static Color HslColor(byte a, double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        double m = l - c / 2;
        (double r, double g, double bl) = (h / 60.0) switch
        {
            < 1 => (c, x, 0.0), < 2 => (x, c, 0.0), < 3 => (0.0, c, x),
            < 4 => (0.0, x, c), < 5 => (x, 0.0, c), _   => (c, 0.0, x),
        };
        return Color.FromArgb(a, (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((bl + m) * 255));
    }
    // internal: the enable/disable toast fills its plate with the same charcoal, so toast and hub match.
    internal static readonly Brush SalvageFill    = Frozen(new SolidColorBrush(Color.FromArgb(243,  24,  24,  28)));  // charcoal
    private static readonly Pen   SalvagePen      = FrozenPen(new SolidColorBrush(Color.FromArgb( 24,   0,   0,   0)), 1.0);
    // A darker band just INSIDE each slice's inner rim — the wedge reads as a lipped panel — and the lip
    // VARIES per edge cell so the plate looks unevenly worn rather than machined. Pens run 10px → 20px;
    // each is centred on the rim and clipped to the wedge, which discards the inward half and
    // leaves 5px → 10px showing inside.
    // Sits directly on the fill, UNDER every glyph, crescent, glow and shadow.
    // Cached up front — a pen per cell per frame would allocate on the hot path. Butt caps, deliberately:
    // a round cap bulges past the cell boundary and softens the steps the blocky edge exists to create.
    private const int    SalvageLipLevels = 8;
    private const double SalvageLipMinPx  = 10.0;
    private const double SalvageLipMaxPx  = 20.0;
    // The HUB gets the same lip at one EVEN width (mid-ladder) — it's a single plate, not a ring of them,
    // so per-cell variation there would read as damage rather than construction.
    // internal: the enable/disable toast reuses it (with its own clip) so the toast plate matches the hub.
    internal static readonly Pen SalvageHubLipPen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(150, 12, 12, 14)), (SalvageLipMinPx + SalvageLipMaxPx) / 2.0);
    // The sides + outer rim get a thin edge in the LIP's colour, so the plate
    // is edged all the way round — the inner rim keeps the heavy varying lip, the rest of the perimeter
    // gets this hairline. Same brush as the lip so the two can't drift apart.
    private static readonly Pen SalvageOuterEdgePen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(150, 12, 12, 14)), 1.5);
    private static readonly Pen[] SalvageLipPens = BuildSalvageLipPens();
    private static Pen[] BuildSalvageLipPens()
    {
        var brush = Frozen(new SolidColorBrush(Color.FromArgb(150, 12, 12, 14)));
        var pens = new Pen[SalvageLipLevels];
        for (int k = 0; k < SalvageLipLevels; k++)
        {
            double w = SalvageLipMinPx
                     + (SalvageLipMaxPx - SalvageLipMinPx) * k / (SalvageLipLevels - 1.0);
            var p = new Pen(brush, w) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            p.Freeze();
            pens[k] = p;
        }
        return pens;
    }
    // ── Salvage surface texture ───────────────────────────────────────────────
    // A PHOTOGRAPHIC metal surface (Assets\salvage-background.jpg). A photo brings what
    // procedural noise compositing structurally can't: hard "eaten" rust edges, real scratch/pit detail,
    // and dimensional light on the flaking.
    //
    // ONE photo feeds BOTH surfaces — the wheel and the Customize/onboarding preview tile — so the swatch
    // can never advertise a different material than the wheel paints. Only the VIEWPORT differs:
    // see BuildSalvageTexture.
    //
    // SCALED, NOT TILED, and OPAQUE: exactly ONE copy of the photo is mapped
    // across the wheel's bounding square, so the ring is a single continuous sheet of metal — no repeat,
    // no mirror seams — and each wedge/hub shows its own crop of it, the way a plate cut from one sheet
    // would. The texture IS the surface (see SalvageRustOpacity for how much of the plate it covers) —
    // which costs nothing, since that plate fill is a UNIFORM charcoal on every slice (SalvageFill, armed
    // included; unlike Kawaii there is no per-slice hue here). Everything that carries per-slice colour or
    // state — the tinted glyph, the armed crescent, the back-light glow halo, the cast shadow — draws
    // AFTER this.
    //
    // The viewport is absolute and follows the wheel's centre, so the brush is rebuilt only when the
    // wheel moves (not per slice, not per frame); per frame it stays one DrawGeometry with a frozen brush.
    private static Brush? _salvageTexBrush, _salvageTexArmedBrush, _salvageTileTexBrush;
    private static Point _salvageTexCenter = new(double.NaN, double.NaN);
    private static BitmapSource? _salvageRustBmp;
    private static bool _salvageRustTried;

    /// <summary>The wheel's metal surface: one copy of the photo scaled across the whole wheel
    /// (centre ± <see cref="OuterRadius"/>), shared by every wedge and the hub. <paramref name="armed"/>
    /// takes the exposure-lifted variant — see <see cref="SalvageArmedRustOpacity"/>.</summary>
    private static Brush SalvageTexture(Point center, bool armed = false)
    {
        if (center != _salvageTexCenter)   // wheel moved — both variants' absolute viewports are stale
        {
            _salvageTexCenter = center;
            _salvageTexBrush = _salvageTexArmedBrush = null;
        }
        var box = new Rect(center.X - OuterRadius, center.Y - OuterRadius, OuterRadius * 2, OuterRadius * 2);
        return armed ? _salvageTexArmedBrush ??= BuildSalvageTexture(box, SalvageArmedBitmap())
                     : _salvageTexBrush      ??= BuildSalvageTexture(box);
    }

    /// <summary>The preview TILE's surface: the SAME photo, scaled to fill the swatch (cropped to its aspect
    /// by UniformToFill), so the tile shows the material the wheel actually paints.</summary>
    private static Brush SalvageTileTexture() => _salvageTileTexBrush ??= BuildSalvageTexture(null);

    /// <summary>The WHOLE surface photo at its full decoded resolution, scaled to fill whatever it's
    /// painted on — the Game Grid's salvage card (the grid wears the full sheet,
    /// not the preview tile's centred crop). Same photo and same <see cref="SalvageRustOpacity"/> as the
    /// wheel, so card and wheel are one material; only the framing differs (one whole sheet across the
    /// card, the way the wheel maps one sheet across the ring).</summary>
    private static Brush? _salvageSheetBrush;
    internal static Brush SalvageSheetTexture()
    {
        if (_salvageSheetBrush is not null) return _salvageSheetBrush;
        if (SalvageRustBitmap() is not { } bmp) return Brushes.Transparent;
        var b = new ImageBrush(bmp)
        {
            Stretch  = Stretch.UniformToFill,   // fill the card, crop the overflow, never squash
            TileMode = TileMode.None,           // one copy — no repeat, no mirror seams
            Opacity  = SalvageRustOpacity,
        };
        b.Freeze();
        return _salvageSheetBrush = b;
    }

    /// <summary>The salvage surface photo, decoded and frozen once.</summary>
    private static BitmapSource? SalvageRustBitmap() =>
        LoadFrozenBitmap("Assets/salvage-background.jpg", "salvage surface", ref _salvageRustBmp, ref _salvageRustTried);

    private static BitmapSource? _salvageArmedBmp;
    private static bool _salvageArmedTried;

    /// <summary>The surface photo with <see cref="SalvageArmedGain"/> applied per channel — the armed slice's
    /// exposed plate. Baked once, lazily, so a user who never arms a salvage slice never pays for it (one
    /// extra copy of the decoded image). Bgra32 deliberately, NOT Pbgra32: premultiplied alpha would need the
    /// gain applied to the alpha-weighted values, and this photo is fully opaque anyway.</summary>
    private static BitmapSource? SalvageArmedBitmap()
    {
        if (_salvageArmedTried) return _salvageArmedBmp ?? SalvageRustBitmap();
        _salvageArmedTried = true;
        try
        {
            if (SalvageRustBitmap() is not { } src) return null;
            var bgra = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            bgra.CopyPixels(px, stride, 0);
            // Per-channel gain through a 256-entry table: one multiply+clamp per VALUE instead of per pixel
            // (1254² = 1.57M pixels × 3 channels otherwise).
            var map = new byte[256];
            for (int v = 0; v < 256; v++) map[v] = (byte)Math.Min(255.0, Math.Round(v * SalvageArmedGain));
            for (int p = 0; p < px.Length; p += 4)
            {
                px[p]     = map[px[p]];       // B
                px[p + 1] = map[px[p + 1]];   // G
                px[p + 2] = map[px[p + 2]];   // R
            }                                  // A (p+3) untouched
            var lifted = BitmapSource.Create(w, h, bgra.DpiX, bgra.DpiY, PixelFormats.Bgra32, null, px, stride);
            lifted.Freeze();
            _salvageArmedBmp = lifted;
        }
        catch (Exception ex)
        {
            // Fall back to the unlifted photo: an armed slice then just looks like a resting one, which is
            // far better than losing the surface entirely.
            System.Diagnostics.Trace.WriteLine($"[Render] salvage armed exposure bake failed: {ex.Message}");
        }
        return _salvageArmedBmp ?? SalvageRustBitmap();
    }

    /// <summary>Decode a packed image resource once and freeze it, latching the attempt so a failure isn't
    /// retried every frame. Null if the resource is missing/undecodable — callers then paint no texture
    /// rather than failing the whole render.</summary>
    private static BitmapSource? LoadFrozenBitmap(string assetPath, string label,
                                                  ref BitmapSource? cache, ref bool tried, string kind = "texture")
    {
        if (tried) return cache;
        tried = true;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri($"pack://application:,,,/{assetPath}");
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // decode now, release the stream
            bmp.EndInit();
            bmp.Freeze();
            cache = bmp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Render] {label} {kind} unavailable: {ex.Message}");
        }
        return cache;
    }

    // ── Terra ground texture ──────────────────────────────────────────────────
    // A seamless cartoon dirt/cracks tile laid translucently over the
    // terracotta backdrop blob, so the "backing card" reads as ground rather than flat colour.
    /// <summary>Alpha of the ground texture over the terracotta blob. Under 1 — the terracotta stays
    /// the dominant colour and the tile only adds grain and cracks.</summary>
    private const double TerraGroundOpacity = 0.68;
    /// <summary>Tile edge in wheel px. Drawing the art smaller than its native size packs the cartoon detail
    /// tighter, which reads better at wheel scale than one giant crack pattern. 515px is a hair under the
    /// ~566px blob, i.e. roughly one tile per axis.</summary>
    private const double TerraGroundTilePx = 515.0;
    private static BitmapSource? _terraGroundBmp;
    private static bool _terraGroundTried;
    private Brush? _terraGroundBrush;
    private (double cx, double cy) _terraGroundKey;

    // The ground tile belongs to the backdrop blob ONLY — do not lay it over the slices or hub.

    /// <summary>The tiled ground brush, anchored in ABSOLUTE wheel coordinates on the blob's own top-left so
    /// the pattern stays locked to the wheel as it drifts in, instead of swimming across it. Cached per
    /// centre (the wheel's drift animation moves the centre every frame of the bloom).</summary>
    private Brush? TerraGroundBrush(Point center, double r)
    {
        if (_terraGroundBrush is not null && _terraGroundKey == (center.X, center.Y)) return _terraGroundBrush;
        if (TerraGroundBitmap() is not { } bmp) return null;
        var brush = new ImageBrush(bmp)
        {
            Opacity       = TerraGroundOpacity,
            TileMode      = TileMode.Tile,          // seamless art — repeat rather than stretch to fit
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport      = new Rect(center.X - r, center.Y - r, TerraGroundTilePx, TerraGroundTilePx),
            Stretch       = Stretch.Fill,           // the viewport IS the tile size; don't letterbox it
        };
        brush.Freeze();
        _terraGroundKey = (center.X, center.Y);
        return _terraGroundBrush = brush;
    }

    private static Brush? _terraGroundTileBrush;

    /// <summary>The same ground tile as a card-space brush for the Game Grid's Terra panel —
    /// identical tile size and opacity as the wheel's backdrop, so grid and wheel read as the
    /// same ground. Anchored absolutely at the layer's own origin (a card doesn't drift like the wheel does),
    /// and transparent if the art is missing so the cream plate simply shows through.</summary>
    internal static Brush TerraGroundTileTexture()
    {
        if (_terraGroundTileBrush is not null) return _terraGroundTileBrush;
        if (TerraGroundBitmap() is not { } bmp) return Brushes.Transparent;
        var b = new ImageBrush(bmp)
        {
            Opacity       = TerraGroundOpacity,
            TileMode      = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport      = new Rect(0, 0, TerraGroundTilePx, TerraGroundTilePx),
            Stretch       = Stretch.Fill,   // the viewport IS the tile size; don't letterbox it
        };
        b.Freeze();
        return _terraGroundTileBrush = b;
    }

    /// <summary>The ground tile, decoded and frozen once. Null if the resource is missing/undecodable —
    /// the backdrop then paints plain terracotta rather than failing the whole render.</summary>
    private static BitmapSource? TerraGroundBitmap() =>
        LoadFrozenBitmap("Assets/terra-ground.png", "terra ground", ref _terraGroundBmp, ref _terraGroundTried);

    // ── Salvage rivets ────────────────────────────────────────────────────────
    // Four rivets per slice, one inset from each CORNER of the wedge —
    // a wedge's corners being inner/outer × leading/trailing edge. Positions are derived from the slice's
    // real PAINTED edges, not its nominal boundary angles: the inter-slice gap eats an asin(gapHalf / r)
    // wedge of angle at every radius (see BuildWedge), so the inset has to be measured from that or the
    // inner rivets would sit visibly closer to the edge than the outer ones. On top of that each rivet
    // steps in by ~0.85 of its own diameter, so it reads as fastened THROUGH the plate with clearance.
    // Skipped when a wedge is too small to hold four without them colliding (thin ring × 12 slices).
    private static BitmapSource? _salvageRivetBmp;
    private static bool _salvageRivetTried;

    /// <summary>Rivet diameter as a fraction of the ring's thickness, and its clamp in px — so it stays
    /// proportionate on a thick ring without becoming a boulder, or vanishing on a thin one.</summary>
    /// <para>The MAX clamp is the real limit at 0.16: it binds on medium and thick rings (which want
    /// 21px and 30px), and 14px is the largest value where a thick ring at 12 slices still seats its
    /// inner pair without them colliding — past that the collision guard below skips those rivets
    /// entirely, which looks worse than a slightly smaller rivet.</para>
    private const double SalvageRivetOfThickness = 0.16;
    private const double SalvageRivetMinPx = 7.0, SalvageRivetMaxPx = 14.0;
    /// <summary>Rivets sit well back from full strength so they read as part of the plate rather than
    /// stickers on top of it. The HOLES don't take this — a hole is a void, so it paints fully opaque
    /// (hence the per-mark push below rather than one push around all four).</summary>
    private const double SalvageRivetOpacity = 0.50;
    /// <summary>Corner inset, in multiples of the rivet's own diameter — uniform for all four (don't
    /// reintroduce per-rivet scatter). Applies to both the radial and the angular inset.
    /// <para>Raising this costs angular room between the leading and trailing rivets of a pair, which is
    /// scarcest on the INNER arc of a thick ring with many slices; the collision guard below then skips
    /// that pair. At 1.15 with salvage's doubled gap, the only casualty is thick × 12 slices, which is an
    /// unsupported combination.</para></summary>
    private const double SalvageRivetPad = 1.15;
    /// <summary>Missing rivets run on a CADENCE, not a probability: every 7th spot walking the ring is a
    /// hole, each step jittered ±2 so the rhythm isn't metronomic (gaps of 5–9). A spot is
    /// <c>slice × 4 + corner</c>, and 7 is coprime with the 4 corners, so successive holes also rotate
    /// through inner/outer and leading/trailing instead of favouring one corner.
    /// <para>A cadence, not a 1-in-N hash test: with only 24–48 spots on a wheel a probability gives 0–2
    /// holes, often adjacent, and isn't monotonic in its divisor. A cadence gives an exact, even spread.</para></summary>
    private const int SalvageRivetHoleEvery  = 7;
    private const int SalvageRivetHoleJitter = 2;

    // Hole spots per slice-count, computed once. Render-thread only (OnRender), so no lock needed.
    private static readonly Dictionary<int, HashSet<int>> _salvageHoleSpots = new();

    private static HashSet<int> SalvageHoleSpots(int slices)
    {
        if (_salvageHoleSpots.TryGetValue(slices, out var cached)) return cached;
        var set = new HashSet<int>();
        int total = slices * 4;
        for (int g = 0, ord = 0; ; ord++)
        {
            int step;
            unchecked
            {
                // Stir the ordinal with a large odd constant BEFORE mixing, then a strong finalizer
                // (lowbias32). A plain multiply-xorshift avalanches badly for tiny inputs: it returned a
                // step of 8 for most ordinals, and 8 = exactly two slices, so every hole landed on the
                // SAME corner — the one thing the jitter exists to prevent (measured, not guessed).
                uint h = (uint)ord * 0x9E3779B1u + 0x85EBCA6Bu;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                step = SalvageRivetHoleEvery
                     + (int)(h % (uint)(SalvageRivetHoleJitter * 2 + 1)) - SalvageRivetHoleJitter;
            }
            g += Math.Max(1, step);       // Max guards a future jitter ≥ the cadence
            if (g >= total) break;
            set.Add(g);
        }
        return _salvageHoleSpots[slices] = set;
    }
    private const double SalvageRivetHoleR    = 0.252;  // × diameter — a hole is smaller than its rivet
    // 0.60 alpha — the rust reads through it, so a hole looks worn/shadowed rather than punched out
    // with a hole saw.
    private static readonly Brush SalvageRivetHoleBrush = Frozen(new SolidColorBrush(Color.FromArgb(153, 6, 5, 5)));

    private static BitmapSource? SalvageRivetBitmap() =>
        LoadFrozenBitmap("Assets/salvage-rivet.png", "salvage rivet", ref _salvageRivetBmp, ref _salvageRivetTried,
                         kind: "art");

    /// <summary>Draw one rivet at each of the wedge's four corners. Caller supplies the slice's boundary
    /// angles + radii and has already clipped to the wedge (so a rivet can never spill onto a neighbour).</summary>
    private static void DrawSalvageRivets(DrawingContext dc, Point center, double inner, double outer,
                                          double startBnd, double endBnd, double gapPx, int i, int n)
    {
        if (SalvageRivetBitmap() is not { } bmp) return;
        // Full circle (single slice) has no radial edges to inset from — no corners, no rivets.
        if (endBnd - startBnd >= 359.9) return;

        double d    = Math.Clamp((outer - inner) * SalvageRivetOfThickness, SalvageRivetMinPx, SalvageRivetMaxPx);
        double pad  = d * SalvageRivetPad;
        double half = gapPx / 2.0;
        double rIn  = inner + pad, rOut = outer - pad;
        if (rOut <= rIn) return;                     // ring too thin to seat a rivet at all

        for (int side = 0; side < 2; side++)          // 0 = inner edge, 1 = outer edge
        {
            double r = side == 0 ? rIn : rOut;
            // The painted edge's own angle at this radius (the gap widens in angle as r shrinks — see
            // BuildWedge), plus the rivet's pad, both converted from px to degrees.
            double a  = (Math.Asin(Math.Min(1.0, half / r)) + pad / r) * (180 / Math.PI);
            double aL = startBnd + a, aT = endBnd - a;
            // Leading and trailing must not touch: their separation, walked along the arc, has to clear a
            // whole diameter. A thick ring at 12 slices is where this bites (inner pair, short arc).
            if ((aT - aL) * Math.PI / 180.0 * r < d * 1.1) continue;

            Blit(r, aL, side * 2);                    // corner ids 0..3 = inner/outer × leading/trailing
            Blit(r, aT, side * 2 + 1);
        }

        void Blit(double r, double aDeg, int corner)
        {
            double rad = aDeg * Math.PI / 180.0;
            var c = new Point(center.X + Math.Cos(rad) * r, center.Y + Math.Sin(rad) * r);
            if (SalvageHoleSpots(n).Contains(i * 4 + corner))   // this one fell out — leave the hole
            {
                dc.DrawEllipse(SalvageRivetHoleBrush, null, c, d * SalvageRivetHoleR, d * SalvageRivetHoleR);
                return;
            }
            dc.PushOpacity(SalvageRivetOpacity);
            dc.DrawImage(bmp, new Rect(c.X - d / 2, c.Y - d / 2, d, d));
            dc.Pop();
        }
    }

    /// <summary>How strongly the rust photo covers the charcoal plate. 1.0 = the photo alone; lower lets
    /// the plate darken/mute it (the whole plate is one flat charcoal, so this is a straight blend toward
    /// that tone, not a per-slice effect).</summary>
    private const double SalvageRustOpacity = 0.85;

    /// <summary>Armed-slice EXPOSURE on the salvage plate: a per-channel gain baked into a copy of the
    /// photo, <c>out = min(255, in × gain)</c>. A true multiply, so BLACKS STAY BLACK by construction
    /// (0 × gain = 0) — unlike a white overlay, which lifts the blacks worst of all.
    /// <para><b>Why 1.35, not the 1.05 that "5% brighter" implies.</b> The photo is very DARK (mean channel
    /// ~25/255), and at that end of the range a 5% gain is ~1 RGB level — invisible. Percentages (and
    /// opacity moves) are misleading near black — pick by on-screen effect:</para>
    /// <para><c>gain 1.05 → +4%  ·  1.10 → +9%  ·  1.22 → +19%  ·  1.35 → +30%  ·  1.50 → +43%  ·  2.00 → +85%</c>
    /// (on-screen mean; clipping stays 0.00% right through 1.75, so the whole range is usable). 1.35 lands
    /// ~7 levels up — visible as a lift without reading as a different material. Turn it down if it shouts.</para>
    /// <para>Only highlights compress, above 255/gain. Measured at this gain: 8 channels out of 4.7M, i.e.
    /// 0.00% — the grain lives in the mid-lows and scales linearly.</para></summary>
    private const double SalvageArmedGain = 1.35;

    /// <summary>How much of the source photo the PREVIEW TILE shows — a centred square crop, as a fraction
    /// of the image. The tile is ~64px and the photo is ~1254px, so fitting the WHOLE image in would be a
    /// ~20× downscale: the grain, pits and eaten edges all disappear and the swatch reads as flat grey-brown.
    /// Cropping shows less of the sheet at a far more legible scale.
    /// <para>Reference for tuning: the WHEEL maps the photo across <c>OuterRadius × 2</c> = 534px, a ~2.35×
    /// downscale, so a crop of about <b>0.12</b> would put the tile at exactly the wheel's apparent texture
    /// scale. This sits a little above that — close enough to preview honestly, wide enough that the crop
    /// catches some variation instead of one flat patch.</para></summary>
    private const double SalvageTileCrop = 0.16;

    /// <summary>The salvage surface for an arbitrary square box OUTSIDE the wheel — the enable/disable toast.
    /// Crops the photo by the box's own size relative to the wheel's span, so the grain reads at roughly the
    /// scale the wheel shows it at instead of a shrunken blur (the same problem, and the same fix, as
    /// <see cref="SalvageTileCrop"/>; 0.16 is about what this formula yields for a ~64px preview tile, so the
    /// two are consistent in spirit). Widened slightly past exact parity so a small box still catches some
    /// variation rather than one flat patch.</summary>
    internal static Brush SalvageBoxTexture(double sizePx)
    {
        if (SalvageRustBitmap() is not { } bmp) return Brushes.Transparent;
        const double widen = 1.35;
        double crop = Math.Clamp(sizePx / (OuterRadius * 2) * widen, 0.06, 1.0);
        var brush = new ImageBrush(bmp)
        {
            Opacity      = SalvageRustOpacity,
            TileMode     = TileMode.None,
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox      = new Rect((1 - crop) / 2, (1 - crop) / 2, crop, crop),
            Stretch      = Stretch.UniformToFill,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>The salvage surface brush. <paramref name="wheelRect"/> = the absolute square to map the
    /// single photo across (the wheel); null = the preview tile, which instead scales a
    /// <see cref="SalvageTileCrop"/> crop to its own bounding box. Untiled either way — UniformToFill crops
    /// rather than distorts. <paramref name="source"/> overrides which photo is used (the armed slice passes
    /// the exposure-lifted bake); null = the plain surface.</summary>
    private static Brush BuildSalvageTexture(Rect? wheelRect, BitmapSource? source = null)
    {
        if ((source ?? SalvageRustBitmap()) is not { } bmp) return Brushes.Transparent;
        var brush = new ImageBrush(bmp)
        {
            // Viewbox selects the SOURCE region (default = the whole image, which is what the wheel wants).
            // The tile takes a centred crop so its texture reads at something near wheel scale.
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox      = wheelRect is not null
                ? new Rect(0, 0, 1, 1)
                : new Rect((1 - SalvageTileCrop) / 2, (1 - SalvageTileCrop) / 2, SalvageTileCrop, SalvageTileCrop),
            Opacity       = SalvageRustOpacity,
            TileMode      = TileMode.None,      // one copy, scaled — no repeat, no seams
            ViewportUnits = wheelRect is null ? BrushMappingMode.RelativeToBoundingBox : BrushMappingMode.Absolute,
            Viewport      = wheelRect ?? new Rect(0, 0, 1, 1),
            Stretch       = Stretch.UniformToFill,   // fill the target, crop the overflow, never squash
        };
        brush.Freeze();
        return brush;
    }

    internal static readonly Brush SalvageCrescent = Frozen(new SolidColorBrush(Color.FromArgb(240, 255, 255, 255))); // armed inner-arc marker
    private const double SalvageCrescentPx = 10.0;   // tube thickness — the noise dots scatter inside it
    private static readonly Pen   SalvageCrescentPen = FrozenRoundPen(SalvageCrescent, SalvageCrescentPx);   // thick round-capped armed crescent

    // ── E1: cheap-fluorescent instability on the armed crescent ─────────────────
    // Glow flicker: mostly steady at 1.0, with occasional slight dips — two slow sines gated so the
    // dip only bites when their product runs deep. The MAIN stroke never rides this (never goes dark).
    private double SalvageGlowFlicker()
    {
        if (_reduceMotion) return 1.0;   // continuous decorative motion: held steady under Reduce Motion
        double t = NowMs / 1000.0;
        double s = Math.Sin(t * 7.3) * Math.Sin(t * 1.9 + 1.3);   // -1..1, deep troughs are rare
        return s < -0.55 ? 1.0 - (-s - 0.55) * 0.55 : 1.0;        // ≤ ~25% dip, brief
    }

    // Tube noise: short dark dashes across the white core, opacity ∝ (distance from the arc's
    // centre)², so the ends carry the grit and the middle stays clean. Positions are hashed per
    // (slice, k) and drift slowly with time, so the grain crawls the way a dying tube's does.
    // Each mark is an explicit 2px round dot centred on the tube's centreline — an arc dash stroked at
    // the tube's thickness reads as a blob, and a thinned dash pen reads as streaks.
    private static readonly Brush SalvageTubeNoiseBrush =
        Frozen(new SolidColorBrush(Color.FromArgb(70, 120, 120, 124)));
    private const double SalvageTubeNoiseDotR = 1.0;   // 2px diameter

    private void DrawSalvageTubeNoise(DrawingContext dc, Point center, double r,
                                      double startDeg, double spanDeg, int i)
    {
        // Under Reduce Motion the grain still shows, hashed in place — it just stops crawling.
        double t = _reduceMotion ? 0 : NowMs / 1000.0;
        for (int k = 0; k < 9; k++)
        {
            unchecked
            {
                uint h = (uint)(k * 374761393 + i * 668265263) ^ 0xF1C3u;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                double u = ((h & 0xFFFF) / 65535.0 + t * 0.07 * (1 + (k % 3))) % 1.0;   // slow crawl
                double edge = Math.Abs(u - 0.5) * 2.0;             // 0 centre → 1 ends
                double a = edge * edge * 0.9;                      // invisible mid, visible ends, smooth
                if (a < 0.05) continue;
                // Scatter ACROSS the tube's thickness instead of riding its centreline: a hashed base
                // radius per (slice, k) plus a slow per-dot wobble, so the specks sit at different depths
                // in the glass and wander between them as they crawl. Clamped so the whole dot stays
                // inside the tube however the two combine.
                double room = SalvageCrescentPx / 2.0 - SalvageTubeNoiseDotR;      // ±4px of travel
                double band = ((h >> 16) & 0xFF) / 255.0 * 2.0 - 1.0;              // hashed depth, -1..+1
                double wob  = Math.Sin(t * (0.5 + 0.35 * (k % 4)) + band * Math.PI * 2);
                double rr   = r + Math.Clamp(band * 0.75 + wob * 0.35, -1.0, 1.0) * room;

                double rad = (startDeg + u * spanDeg) * Math.PI / 180.0;
                var dot = new Point(center.X + Math.Cos(rad) * rr, center.Y + Math.Sin(rad) * rr);
                dc.PushOpacity(a);
                dc.DrawEllipse(SalvageTubeNoiseBrush, null, dot, SalvageTubeNoiseDotR, SalvageTubeNoiseDotR);
                dc.Pop();
            }
        }
    }
    // Per-slice-colour NEON glow: a soft, gradual falloff built from many concentric round-capped strokes
    // — wide + faint on the outside, narrowing and brightening (hue → near-white) toward the core — so it
    // reads like a glowing tube rather than two hard bands. Cached by colour (a wheel has few tints).
    // Layers: (widthPx, alpha, whiten 0..1 toward white).
    private static readonly (double w, byte a, double whiten)[] SalvageGlowLayers =
    {
        (54, 14, 0.00), (44, 22, 0.00), (36, 32, 0.05), (28, 48, 0.12),
        (22, 74, 0.22), (16, 120, 0.35), (11, 200, 0.50),   // whiten ramp kept low — the halo keeps its hue longer
    };
    private static readonly Dictionary<Color, Pen[]> SalvageGlowCache = new();
    private static readonly Dictionary<Color, Brush> SalvageWashCache = new();   // armed-lighten washes, by base colour
    private static Pen[] SalvageGlowPens(Color c)
    {
        if (SalvageGlowCache.TryGetValue(c, out var cached)) return cached;
        static byte Mix(byte v, double t) => (byte)(v + (255 - v) * t);
        var pens = new Pen[SalvageGlowLayers.Length];
        for (int k = 0; k < pens.Length; k++)
        {
            var (w, a, whiten) = SalvageGlowLayers[k];
            var col = Color.FromArgb(a, Mix(c.R, whiten), Mix(c.G, whiten), Mix(c.B, whiten));
            pens[k] = FrozenRoundPen(Frozen(new SolidColorBrush(col)), w);
        }
        SalvageGlowCache[c] = pens;
        return pens;
    }
    private static readonly Brush TerraFill       = Frozen(new SolidColorBrush(Color.FromArgb(246, 242, 237, 227)));  // cream
    private static readonly Brush TerraArmedFill  = Frozen(new SolidColorBrush(Color.FromArgb(250, 240, 206, 110)));  // armed = saturated warm YELLOW
    private static readonly Pen   TerraArmedPen   = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 110,  56,  30)), 3.6);  // chunky BURNT-UMBER focus stroke
    internal static readonly Brush TerraBlobFill  = Frozen(new SolidColorBrush(Color.FromArgb(238, 201, 106,  59)));  // terracotta backdrop blob
    internal static readonly Brush TerraShadow    = Frozen(new SolidColorBrush(Color.FromArgb( 44,  30,  20,  10)));  // hard offset cutout shadow
    // Armed terra "3D card": the slice lifts outward/bigger and shows a thick darker-cream SOUTH edge
    // (the same shape drawn offset straight down underneath) — reads as card thickness.
    // internal: the Game Grid's terra card edge (a selected tile's lifted-card thickness) fills with it.
    internal static readonly Brush TerraCardFill   = Frozen(new SolidColorBrush(Color.FromArgb(250, 206, 192, 166)));
    private const double TerraCardPx    = 10.0;   // south-edge card thickness (px)
    // The armed terra card lifts STRAIGHT UP only — no outward slide from the hub, no scale-up.
    // The screen-up nudge is the whole armed motion.
    private const double TerraLiftPx    = 0.0;    // outward slide from the wheel centre — keep at 0
    private const double TerraNorthPx   = -10.0;  // screen-up nudge (raised toward the light)
    private const double TerraArmedScale = 1.0;   // no growth — the lift + shadow carry the armed read
    private static readonly Brush ReactorRestFill = Frozen(new SolidColorBrush(Color.FromArgb( 34,  10,  10,  12)));  // faint smoke (hub disc)
    // Unarmed wedges: significantly darker at the INNER edge, fading to near-transparent at the outer —
    // one wheel-wide absolute radial gradient, cached per centre/thickness.
    private Brush? _reactorRestGrad;
    private (double inner, double cx, double cy) _reactorGradKey;
    private Brush ReactorRestGradient(Point center)
    {
        var key = (_sliceInner, center.X, center.Y);
        if (_reactorRestGrad is not null && _reactorGradKey == key) return _reactorRestGrad;
        var b = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = center, GradientOrigin = center,
            RadiusX = OuterRadius, RadiusY = OuterRadius,
        };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(205, 8, 8, 10), 0.0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(205, 8, 8, 10), _sliceInner / OuterRadius));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(  0, 8, 8, 10), 1.0));   // FULLY clear at the rim
        b.Freeze();
        _reactorGradKey = key;
        return _reactorRestGrad = b;
    }
    // Armed circuit-board parallax (a seeded PCB-style trace network inside the fused dark area, shifting
    // with the aiming stick — the hub's analog readout while armed), plus a few "electricity"
    // sparks running the traces. Flip FALSE to fully revert the effect.
    private const bool ReactorGridEnabled = true;
    private static readonly Pen ReactorGridPen = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 150, 150, 158)), 0.9);
    private static readonly Pen ReactorChipPen = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 150, 150, 158)), 1.4);

    // ── Reactor circuit board ─────────────────────────────────────────────────
    // STATIC layer: generated once per (slice, geometry) into a frozen DrawingGroup — per-frame cost is one
    // DrawDrawing under a transform, cheaper than the per-frame line loop it replaced. DYNAMIC layer: 4
    // sparks, each just (trace, distance, speed) resolved against per-trace cumulative arc-length tables;
    // tails are the trace geometry itself re-walked backward, so they turn corners with the wire.
    private sealed class ReactorCircuit
    {
        public required Drawing         Board;
        public required List<Point[]>   Traces;   // polyline points per trace
        public required List<double[]>  Cum;      // cumulative arc length per trace (Cum[t][k] = length up to point k)
        public required List<Point>     Pads;     // solder-pad terminators
        public required List<Rect>      Chips;    // chip packages (crossings are legal inside these)
        public double TotalLen(int t) => Cum[t][^1];

        /// <summary>Point at arc-length <paramref name="dist"/> along trace <paramref name="t"/> (clamped).</summary>
        public Point PointAt(int t, double dist)
        {
            var pts = Traces[t]; var cum = Cum[t];
            dist = Math.Clamp(dist, 0, cum[^1]);
            int k = 1;
            while (k < cum.Length - 1 && cum[k] < dist) k++;   // traces are 4-8 points — linear walk beats a search
            double segLen = cum[k] - cum[k - 1];
            double u = segLen > 0.0001 ? (dist - cum[k - 1]) / segLen : 0;
            return new Point(pts[k - 1].X + (pts[k].X - pts[k - 1].X) * u,
                             pts[k - 1].Y + (pts[k].Y - pts[k - 1].Y) * u);
        }
    }
    private ReactorCircuit? _circuit;
    // A SECOND board sits behind the first at 80% scale and half the stick response — two depth planes,
    // so pushing the stick shears them apart and the fused slice reads as having actual depth. Its own
    // seed, so it isn't a visibly duplicated layout.
    private ReactorCircuit? _circuitBack;
    // A THIRD board sits furthest back with the least stick response — the extra plane deepens the
    // parallax stack.
    private ReactorCircuit? _circuitFar;
    // ONE board set for the whole wheel, generated in WHEEL SPACE and clipped per fused slice — the
    // slices are windows onto the same continuous circuitry, so rotating the stick pans across it
    // instead of swapping backgrounds per focus change. Regenerated only when the wheel's geometry
    // changes; generation runs once on the UI thread (3 boards ONCE beat 2×N amortized per-slice ones).
    private (ReactorCircuit Front, ReactorCircuit Back, ReactorCircuit Far)? _wheelBoards;
    private (int n, double inner, double cx, double cy) _circuitGeoKey = (0, 0, 0, 0);
    // internal: the Game Grid's reactor card runs the SAME three planes behind its tiles (selection drives
    // the shear there instead of the stick), so the two surfaces can't drift out of proportion.
    internal const double CircuitBackScale   = 0.80;
    internal const double CircuitBackDim     = 0.55;   // × the front layer's opacity — distance haze
    internal const double CircuitBackZoomAmt = 0.5;    // × the front layer's zoom + anchor travel
    internal const double CircuitFarScale    = 0.65;
    internal const double CircuitFarDim      = 0.32;   // deepest haze
    internal const double CircuitFarZoomAmt  = 0.28;   // moves the least of the three
    /// <summary>Opacity the board layer draws at (× each plane's dim). internal — the Game Grid matches it.</summary>
    internal const double CircuitBoardOpacity = 0.28;

    private sealed class CircuitSpark
    {
        public int Trace; public double Dist; public double Speed; public int Dir = 1; public int Color;
    }
    private readonly CircuitSpark[] _circuitSparks     = new CircuitSpark[4];
    private readonly CircuitSpark[] _circuitSparksBack = new CircuitSpark[2];   // fewer + dimmer, further away
    private readonly CircuitSpark[] _circuitSparksFar  = new CircuitSpark[1];   // the far plane gets a lone drifter
    private DateTime _circuitSparkT;   // last advance, for dt

    // Spark head is white-hot whatever the charge; the TAIL carries the colour. Pens are indexed
    // [colour][tail step] (step 0 = at the head) — alpha and width both taper. All frozen up front;
    // tails draw as plain line segments between sampled points, so the dynamic layer allocates nothing.
    private const int    SparkTailSteps  = 8;
    private const double SparkTailStepPx = 8.0;    // 8 steps × 8px ≈ a 64px tail
    // Electric blue, cyan-green and amber — distinct hues that all read as "charge" on near-black.
    private static readonly Color[] SparkColors =
    {
        Color.FromRgb(170, 225, 255), Color.FromRgb(150, 255, 215), Color.FromRgb(255, 205, 130),
    };
    private static readonly Pen[][]  SparkTailPens = BuildSparkTailPens();
    private static readonly Brush[]  SparkGlowBrushes = SparkColors
        .Select(c => Frozen(new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B)))).ToArray();
    private static Pen[][] BuildSparkTailPens()
    {
        var byColor = new Pen[SparkColors.Length][];
        for (int c = 0; c < SparkColors.Length; c++)
        {
            var col  = SparkColors[c];
            var pens = new Pen[SparkTailSteps];
            for (int q = 0; q < SparkTailSteps; q++)
            {
                byte a = (byte)(210 - q * (200.0 / SparkTailSteps));
                pens[q] = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(a, col.R, col.G, col.B)), 1.6 - q * 0.12);
            }
            byColor[c] = pens;
        }
        return byColor;
    }
    private static readonly Brush SparkHeadBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)));

    /// <summary>The circuit spark's two tokens, for hosts running their own spark outside this control (the
    /// Game Grid's reactor card traces one around the selected tile): the head is white-hot whatever the
    /// charge, and the TAIL carries the colour — the same split the wheel's <c>DrawCircuitLayer</c> draws,
    /// so a grid spark and a wheel spark are the same spark. (The wheel's fake glow halo is deliberately NOT
    /// shared: on a tile outline it just fattens the head.)</summary>
    internal static Brush ReactorSparkHead => SparkHeadBrush;
    /// <summary>One of the wheel's charge colours by index (electric blue, cyan-green, amber), wrapped — the
    /// Game Grid runs two sparks at once and picks a different one for each.</summary>
    internal static Color ReactorSparkTailColor(int index) => SparkColors[((index % SparkColors.Length) + SparkColors.Length) % SparkColors.Length];
    // Darkened plate behind the traces so the board reads as a board, not lines floating on the fill.
    // Fully OPAQUE: the armed slice's interior is a solid plate, so the game
    // behind never shows through the circuit board. Dark grey rather than pure black — the traces and
    // sparks need something to sit on.
    private static readonly Brush ReactorBoardPlate = Frozen(new SolidColorBrush(Color.FromRgb(16, 16, 18)));
    // The dwell glyph washes in the FRONT board's own trace grey (same colour as ReactorGridPen), so it
    // reads as part of the circuitry until the quarters snap and it flips to white.
    private static readonly Brush ReactorDwellGlyphWash = Frozen(new SolidColorBrush(Color.FromRgb(150, 150, 158)));

    /// <summary>Generate the seeded circuit board for one slice: Manhattan+45° traces, a few tightly
    /// spaced parallel "buses", and 2-3 chip packages placed first so traces can originate at their pins.
    /// Deterministic per (slice, count): re-arming the same slice shows the same board.</summary>
    private static ReactorCircuit BuildReactorCircuit(Rect r, Rect visible, int seed, Pen? tracePen = null,
                                                      Pen? chipPen = null, double density = 1.0)
    {
        var rng    = new Random(seed);
        var traces = new List<Point[]>();
        var pads   = new List<Point>();   // small square terminators (see Terminate below)

        // Chips first — traces below want to start at their edges so the convergence reads as intentional.
        // Counts scale with `density` so a board generated over a LARGER rect than the wheel's (the Game
        // Grid's card) keeps the same traces-per-pixel — otherwise the same ~60 traces spread over three
        // times the area and the board reads as sparse wiring rather than the wheel's dense circuitry.
        int chipCount = Math.Max(2, (int)Math.Round((2 + rng.Next(2)) * density));
        var chips = new List<Rect>();
        for (int c = 0; c < chipCount; c++)
        {
            double w = 26 + rng.NextDouble() * 16, h = 16 + rng.NextDouble() * 12;
            chips.Add(new Rect(r.Left + 20 + rng.NextDouble() * Math.Max(1, r.Width  - 40 - w),
                               r.Top  + 20 + rng.NextDouble() * Math.Max(1, r.Height - 40 - h), w, h));
        }

        // No trace may just STOP in view — a bare dead-end reads as an unfinished drawing. Each free end is
        // either run off-board (extended along its own heading until it clears the generation rect, so it
        // reads as continuing past the viewport) or capped with a small solder pad. Ends that already leave
        // the visible area, and ends anchored on a chip, need nothing.
        // Pads go to `pending`, not straight to `pads`: a candidate route can still be REJECTED by the
        // crossing test below, and a rejected route's pads would be left floating with no trace attached.
        // Place() commits them.
        void Terminate(List<Point> pts, List<Point> pending, bool atStart)
        {
            int    iEnd = atStart ? 0 : pts.Count - 1;
            int    iAdj = atStart ? 1 : pts.Count - 2;
            var    end  = pts[iEnd];
            if (!visible.Contains(end)) return;                       // already off-screen: nothing to hide
            if (rng.Next(100) < 45) { pending.Add(end); return; }     // ~45%: cap with a solder pad
            double dx = end.X - pts[iAdj].X, dy = end.Y - pts[iAdj].Y;
            double m  = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy));
            dx /= m; dy /= m;
            // Extend until clear of the GENERATION rect (which is inflated past the visible area).
            double t = 0;
            while (t < 600 && r.Contains(new Point(end.X + dx * t, end.Y + dy * t))) t += 12;
            var far = new Point(end.X + dx * (t + 24), end.Y + dy * (t + 24));
            if (atStart) pts.Insert(0, far); else pts.Add(far);
        }

        // ── Non-crossing routing ──────────────────────────────────────────────
        // A real board's traces never cross except at a component: two nets touching anywhere else is a
        // short. So candidate traces are REJECTED if any segment crosses an already-placed one, unless the
        // crossing falls inside a chip package (where convergence is the point). Rejection sampling, not a
        // router: cheap, runs once per slice geometry, and a candidate that can't be placed is just dropped.
        var placed = new List<(Point a, Point b)>();
        var chipZones = chips.Select(c => Rect.Inflate(c, 5, 5)).ToList();   // +5 covers the stub pins

        bool SegsCross(Point p1, Point p2, Point p3, Point p4, out Point hit)
        {
            hit = default;
            double d = (p2.X - p1.X) * (p4.Y - p3.Y) - (p2.Y - p1.Y) * (p4.X - p3.X);
            if (Math.Abs(d) < 1e-9) return false;                       // parallel — the bus case, allowed
            double t = ((p3.X - p1.X) * (p4.Y - p3.Y) - (p3.Y - p1.Y) * (p4.X - p3.X)) / d;
            double u = ((p3.X - p1.X) * (p2.Y - p1.Y) - (p3.Y - p1.Y) * (p2.X - p1.X)) / d;
            if (t < 0 || t > 1 || u < 0 || u > 1) return false;
            hit = new Point(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
            return true;
        }

        bool Clear(IReadOnlyList<Point> pts)
        {
            for (int k = 1; k < pts.Count; k++)
                foreach (var (a, b) in placed)
                    if (SegsCross(pts[k - 1], pts[k], a, b, out var hit)
                        && !chipZones.Any(z => z.Contains(hit)))
                        return false;
            return true;
        }

        void Place(Point[] pts, List<Point>? pending = null)
        {
            traces.Add(pts);
            for (int k = 1; k < pts.Length; k++) placed.Add((pts[k - 1], pts[k]));
            if (pending is not null) pads.AddRange(pending);
        }

        (Point[] Pts, List<Point> Pads) Walk(Point start, double angRad, bool startOnChip)
        {
            var pending = new List<Point>();
            var pts = new List<Point> { start };
            var p = start;
            int segs = 3 + rng.Next(4);
            // Turns are -45° / 0 / +45° per joint, so a 90° corner is only ever reachable as TWO chamfered
            // 45s — proper PCB practice (a hard right angle is an etching/impedance no-no). `chamfer` makes
            // that deliberate: after a turn, the very next joint repeats it over a SHORT run, so the pair
            // reads as one mitred corner instead of a staircase, and then the trace settles straight again.
            int chamfer = 0;
            for (int s = 0; s < segs; s++)
            {
                double len = chamfer != 0 ? 7 + rng.NextDouble() * 9 : 22 + rng.NextDouble() * 58;
                p = new Point(p.X + Math.Cos(angRad) * len, p.Y + Math.Sin(angRad) * len);
                pts.Add(p);
                int turn;
                if (chamfer != 0) { turn = chamfer; chamfer = 0; }        // close the mitre
                else { turn = rng.Next(3) - 1; chamfer = turn; }          // 0 = stay straight, ±1 opens one
                angRad += turn * Math.PI / 4.0;
            }
            Terminate(pts, pending, atStart: false);
            if (!startOnChip) Terminate(pts, pending, atStart: true);   // a chip edge IS a termination
            return (pts.ToArray(), pending);
        }

        Point ChipEdgePoint(Rect ch) => rng.Next(2) == 0
            ? new Point(rng.Next(2) == 0 ? ch.Left : ch.Right, ch.Top + rng.NextDouble() * ch.Height)
            : new Point(ch.Left + rng.NextDouble() * ch.Width, rng.Next(2) == 0 ? ch.Top : ch.Bottom);

        // Target trace count. Non-crossing routing means the board self-limits — once the space is full,
        // candidates stop finding room and the extra attempts are wasted rather than harmful. Raising the
        // target past that point buys nothing, so this sits just above where placement starts to saturate.
        int nTraces = (int)Math.Round((52 + rng.Next(14)) * density);
        for (int k = 0; k < nTraces; k++)
        {
            bool onChip = k < chipCount * 2;   // first few leave a chip edge — the "convergence points"
            for (int attempt = 0; attempt < 16; attempt++)   // retry a rejected route; give up quietly
            {
                var start = onChip
                    ? ChipEdgePoint(chips[k % chipCount])
                    : new Point(r.Left + rng.NextDouble() * r.Width, r.Top + rng.NextDouble() * r.Height);
                var (cand, candPads) = Walk(start, rng.Next(8) * Math.PI / 4.0, onChip);
                if (!Clear(cand)) continue;
                Place(cand, candPads);
                break;
            }
        }

        // Buses: clone traces at tight parallel offsets (perpendicular to the first segment). A uniform
        // offset of a polyline can still cross a NEIGHBOURING trace, so clones go through the same test —
        // and because parallel segments never register as crossing, a clone never conflicts with its source.
        for (int b = 0; b < (int)Math.Round(12 * density) && traces.Count > 0; b++)
        {
            var src = traces[rng.Next(traces.Count)];
            double dx = src[1].X - src[0].X, dy = src[1].Y - src[0].Y;
            double m  = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy));
            double px = -dy / m, py = dx / m;
            int sign = rng.Next(2) == 0 ? 1 : -1;
            for (int q = 1; q <= 2; q++)
            {
                var clone = src.Select(p => new Point(p.X + px * 3.5 * q * sign, p.Y + py * 3.5 * q * sign)).ToArray();
                if (!Clear(clone)) break;   // blocked on this side — stop widening the bus
                Place(clone);
                // The clone's ends sit off the source's, so a source end capped by a pad leaves the clone
                // bare beside it. Pad any clone end still inside the visible area.
                if (visible.Contains(clone[0]))  pads.Add(clone[0]);
                if (visible.Contains(clone[^1])) pads.Add(clone[^1]);
            }
        }

        // Arc-length tables (the spark position lookup).
        var cum = new List<double[]>(traces.Count);
        foreach (var pts in traces)
        {
            var c = new double[pts.Length];
            for (int k = 1; k < pts.Length; k++)
            {
                double dx = pts[k].X - pts[k - 1].X, dy = pts[k].Y - pts[k - 1].Y;
                c[k] = c[k - 1] + Math.Sqrt(dx * dx + dy * dy);
            }
            cum.Add(c);
        }

        // Bake the static layer: all traces as ONE StreamGeometry + the chip packages with stub pins.
        var boardGeo = new StreamGeometry();
        using (var g = boardGeo.Open())
            foreach (var pts in traces)
            {
                g.BeginFigure(pts[0], false, false);
                for (int k = 1; k < pts.Length; k++) g.LineTo(pts[k], true, false);
            }
        boardGeo.Freeze();

        var dg = new DrawingGroup();
        using (var c = dg.Open())
        {
            c.DrawGeometry(null, (tracePen ?? ReactorGridPen), boardGeo);
            foreach (var p in pads)   // solder-pad terminators (see Terminate)
                c.DrawRectangle(null, (chipPen ?? ReactorChipPen), new Rect(p.X - 2.6, p.Y - 2.6, 5.2, 5.2));
            foreach (var ch in chips)
            {
                c.DrawRoundedRectangle(null, (chipPen ?? ReactorChipPen), ch, 3, 3);
                for (double x = ch.Left + 5; x <= ch.Right - 5; x += 7)   // stub pins, top + bottom edges
                {
                    c.DrawLine((tracePen ?? ReactorGridPen), new Point(x, ch.Top),    new Point(x, ch.Top - 4));
                    c.DrawLine((tracePen ?? ReactorGridPen), new Point(x, ch.Bottom), new Point(x, ch.Bottom + 4));
                }
            }
        }
        dg.Freeze();

        return new ReactorCircuit { Board = dg, Traces = traces, Cum = cum, Pads = pads, Chips = chipZones };
    }

    /// <summary>A circuit board generated over an ARBITRARY rect at the wheel's own screen-space scale —
    /// the wheel's trace/chip pens (so line weights are identical) and a trace count scaled to keep the
    /// wheel's traces-per-pixel. For the Game Grid's reactor card, which needs boards far larger than a
    /// wheel: stretching the small preview-tile board to card size is what made the lines read as fat and
    /// widely spaced. Returns just the frozen static layer — no sparks, no arc-length tables.
    ///
    /// SAFE TO CALL OFF THE UI THREAD: everything it touches is a frozen static, and the returned Drawing
    /// is frozen before it leaves. (Density is capped because the non-crossing routing is O(placed) per
    /// candidate, so cost climbs faster than area.)</summary>
    internal static Drawing BuildReactorBoardFor(Rect gen, int seed)
    {
        double wr = OuterRadius + ReactorSwellPx + 60;                    // the wheel's own board rect …
        double wheelArea = wr * wr * 4;                                   // … (side = wr*2)
        double density = Math.Clamp(gen.Width * gen.Height / wheelArea, 1.0, 2.2);
        var board = BuildReactorCircuit(gen, gen, seed, density: density).Board;
        board.Freeze();
        return board;
    }

    /// <summary>Advance the sparks (called from the render-timer tick while a reactor slice is armed).
    /// Erratic behavior comes from the re-rolls, not physics: at a trace end the spark jumps to a random
    /// trace at a fresh speed, and each tick has a small chance to reverse direction mid-run.</summary>
    private void TickCircuitSparks()
    {
        if (_circuit is null) return;
        var now = NowUtc;
        double dt = Math.Clamp((now - _circuitSparkT).TotalSeconds, 0, 0.1);
        _circuitSparkT = now;
        Advance(_circuitSparks,     _circuit);
        Advance(_circuitSparksBack, _circuitBack);

        void Advance(CircuitSpark[] set, ReactorCircuit? circuit)
        {
            if (circuit is null) return;
            for (int k = 0; k < set.Length; k++)
            {
                // Re-roll a spark whose trace index doesn't fit THIS board as well as a missing one: the
                // draw path runs inside OnRender, where an out-of-range index is fatal rather than a
                // glitch. The owning code keeps them in sync (see the board-identity check in DrawSlice);
                // this is the backstop, because the cost of being wrong here is the whole app.
                var s = set[k];
                if (s is null || s.Trace >= circuit.Traces.Count) s = set[k] = NewSpark(circuit);
                s.Dist += s.Speed * dt * s.Dir;
                if (FxRng.NextDouble() < 0.008) s.Dir = -s.Dir;   // the occasional mid-wire double-back
                if (s.Dist < 0 || s.Dist > circuit.TotalLen(s.Trace))
                    set[k] = NewSpark(circuit);
            }
        }

        static CircuitSpark NewSpark(ReactorCircuit circuit)
        {
            int t = FxRng.Next(circuit.Traces.Count);
            int dir = FxRng.Next(2) == 0 ? 1 : -1;
            return new CircuitSpark
            {
                Trace = t,
                Dir   = dir,
                Dist  = dir > 0 ? 0 : circuit.TotalLen(t),
                Speed = 260 + FxRng.NextDouble() * 320,
                Color = FxRng.Next(SparkColors.Length),   // each spark re-rolls its charge colour
            };
        }
    }
    // ── Reactor resting slices: hollow ────────────────────────────────────────
    // The resting wedge paints nothing; the glyph sits on a hard black disc with a white rim, so a resting
    // reactor wheel reads as discs over the game. False restores the gradient wedge (ReactorRestGradient,
    // which the hub still uses) with the glyph on a blurred blob (DrawReactorDropShadow).
    private const bool ReactorHollowRest = true;
    // Disc radius as a multiple of the glyph box.
    private const double ReactorRestDiscScale = 0.78;
    private static readonly Brush ReactorRestDiscFill = Frozen(new SolidColorBrush(Color.FromArgb(232, 8, 8, 10)));
    // Half the armed fusion outline's weight: the resting discs recede; the armed keyhole's stroke is the
    // focal line.
    private static readonly Pen   ReactorRestDiscPen  =
        FrozenPen(new SolidColorBrush(Color.FromArgb(235, 240, 240, 240)), 3.0);
    // Armed glyphs are washed white, so their glow only has to tint that white — far subtler than salvage's,
    // which lights a charcoal plate. It is the only colour on an otherwise monochrome armed slice.
    private const double ReactorGlowCoreAlpha = 0.40;
    private const double ReactorGlowHaloAlpha = 0.30;

    // The circuit board's parallax, as named constants rather than literals at the draw site: the armed
    // glyph's drift samples the SAME displacement, and two copies of "0.55" and "1.44" would eventually
    // drift apart and stop matching.
    // internal: the Game Grid's board planes scale their own depth-zoom + shear from the same two numbers.
    internal const double CircuitZoomAmt     = 0.55;   // zoom added at full stick deflection
    internal const double CircuitAnchorReach = 1.44;   // how far the zoom anchor rides, in InnerRadius units

    /// <summary>How much of the circuit board's own parallax displacement the ARMED glyph borrows. The board
    /// zooms about an anchor pushed in the stick's direction; sampling that same displacement AT THE GLYPH and
    /// scaling it down means the glyph drifts in lockstep with the traces under it — same direction, same
    /// falloff — just gently, so it reads as the whole assembly shifting rather than the glyph sliding
    /// around. Set to 0 to disable (the drift maths then yields no offset).</summary>
    private const double ReactorGlyphDriftAmt = 0.22;

    /// <summary>How much closer to the hub the ARMED glyph sits than the swell midpoint (B2).</summary>
    private const double ReactorArmedGlyphInsetPx = 8.0;

    /// <summary>The armed glyph's parallax offset: the front circuit layer's displacement evaluated at the
    /// glyph's own position, scaled by <see cref="ReactorGlyphDriftAmt"/>. Scaling a point P about anchor A
    /// by zm moves it by (zm-1)·(P-A), so this reproduces the board's motion at exactly that spot — meaning
    /// the glyph and the traces beneath it never shear against each other, whichever side of the anchor the
    /// slice happens to sit on.</summary>
    private (double dx, double dy) ReactorGlyphDrift(Point center, Point content)
    {
        // No early-out for a zero amount or a centred stick: `grow` falls to 0 in both cases and the
        // offsets come out (0,0) on their own. (An explicit guard on the const is dead code — warning.)
        // Reduce Motion (Task 11): pin mag at 0 so the armed glyph never drifts off its resting spot.
        double mag = _reduceMotion ? 0.0 : Math.Min(1.0, Math.Sqrt(_sm.StickX * _sm.StickX + _sm.StickY * _sm.StickY));
        double grow = CircuitZoomAmt * mag * ReactorGlyphDriftAmt;
        double ax = center.X + _sm.StickX * InnerRadius * CircuitAnchorReach;
        double ay = center.Y + _sm.StickY * InnerRadius * CircuitAnchorReach;
        return ((content.X - ax) * grow, (content.Y - ay) * grow);
    }

    // Fully opaque: besides the fused slice (where the board plate covers it anyway), this brush fills the
    // scrubber hub and the unarmed hub carrying text, which have no opaque backing of their own.
    private static readonly Brush ReactorArmedFill= Frozen(new SolidColorBrush(Color.FromArgb(255,  12,  12,  14)));  // hub-fusion near-black
    private static readonly Pen   ReactorPen      = FrozenPen(new SolidColorBrush(Color.FromArgb(235, 240, 240, 240)), 2.0);
    // The unarmed HUB uses a dimmed rim — 40% of ReactorPen's opacity — so the armed fusion's
    // full-bright stroke pops (resting WEDGES are unstroked). The scrubber hub keeps ReactorPen.
    private static readonly Pen   ReactorRestPen  = FrozenPen(new SolidColorBrush(Color.FromArgb( 94, 240, 240, 240)), 2.0);
    // The FUSED (armed) hub+wedge outline: one continuous heavy white stroke around the whole keyhole
    // silhouette (3× the resting weight), with a thin black keyline sitting just OUTSIDE it (a wider black
    // pen drawn underneath; the fill hides its inner half).
    // Round line-joins: the rounded fusion geometry is a flattened polyline — miter joins would spike.
    private static readonly Pen   ReactorFusedPen      = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(235, 240, 240, 240)), 6.0);
    private static readonly Pen   ReactorFusedBlackPen = FrozenJoinPen(new SolidColorBrush(Color.FromArgb( 46, 0, 0, 0)), 9.0);   // 20% opacity
    private static Pen FrozenJoinPen(SolidColorBrush color, double w)
    {
        var p = new Pen(color, w) { LineJoin = PenLineJoin.Round };
        color.Freeze();
        p.Freeze();
        return p;
    }
    // Concentric contour "ripples" echoing the fused hub+wedge outline inward, decreasing opacity —
    // drawn as the same fusion outline scaled inward about its own bounds centre.
    private static readonly Pen[] ReactorRipplePens =
    {
        FrozenPen(new SolidColorBrush(Color.FromArgb(115, 255, 255, 255)), 1.2),   // 45%
        FrozenPen(new SolidColorBrush(Color.FromArgb( 89, 255, 255, 255)), 1.2),   // 35%
        FrozenPen(new SolidColorBrush(Color.FromArgb( 56, 255, 255, 255)), 1.2),   // 22%
        FrozenPen(new SolidColorBrush(Color.FromArgb( 31, 255, 255, 255)), 1.2),   // 12%
    };
    private static readonly double[] ReactorRippleScales = { 0.94, 0.88, 0.82, 0.76 };
    // How far the armed fusion's outer edge swells past the resting ring (px).
    private const double ReactorSwellPx = 30.0;
    // Fine concentric etched rings inside the reactor hub disc — reads as a machined/etched dark node.
    private static readonly Pen   ReactorHubEtchPen = FrozenPen(new SolidColorBrush(Color.FromArgb( 20, 255, 255, 255)), 0.75);  // ~8%


    // ── Brushes and pens (frozen for render-thread safety) ───────────────────
    private static readonly Brush SliceFill =
        Frozen(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)));   // 90% opaque white
    private static readonly Brush ArmedFill =
        Frozen(new SolidColorBrush(Color.FromArgb(230, 177, 195, 207)));   // flat-light armed: flat muted slate blue ~#B1C3CF
    private static readonly Brush ArmedFillDark =
        Frozen(new SolidColorBrush(Color.FromRgb(0x68, 0x78, 0x94)));      // flat-dark armed: #687894
    private static readonly Pen SlicePen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(64, 0, 0, 0)), 1.5);
    // Armed outline + hub-chip rim are material-toned: a teal-leaning slate on the LIGHT materials
    // (matches the muted blue-teal armed fills), the original blue-grey on the DARK ones.
    private static readonly Pen ArmedPenDark =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 80, 106, 130)), 2.5);  // ~#506a82
    private static readonly Pen ArmedPenLight =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 80, 105, 122)), 2.5);  // slate ~#50697A
    private Pen ArmedPen => DarkMaterial ? ArmedPenDark : ArmedPenLight;
    private static readonly Pen HubChipRimDark =                                 // thin 1px armed rim for the hub title chip
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 80, 106, 130)), 1.0);
    private static readonly Pen HubChipRimLight =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 80, 105, 122)), 1.0);
    // Terra: pills (e.g. the "Practice" badge) get a thicker rim in the armed-slice burnt umber.
    private static readonly Pen HubChipRimTerra =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 110, 56, 30)), 2.5);
    private Pen HubChipRim => _material == "mesa" ? HubChipRimTerra
                           : DarkMaterial ? HubChipRimDark : HubChipRimLight;
    private static readonly Pen CenterRingPen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1.0);
    private Brush? _backglow;        // soft drop-shadow under the wheel (WITH the central hub shadow)
    private Brush? _backglowNoHub;   // …and the hub-less variant (ring band only) for a hidden centre
    private static readonly Brush LabelBrush =
        Frozen(new SolidColorBrush(Color.FromArgb(255, 15, 15, 25)));
    // Non-Latin languages draw every label and hub readout in the language's own chain: none of the display
    // faces below (Jua, Sour Gummy, Bahnschrift, Share Tech) carries Japanese or Arabic, and a missing glyph
    // falls to whatever WPF finds, which differs per material. LAZY for the same pack:// reason as ReactorHubFace.
    private static Typeface? _languageLabelFace, _languageHubFace;
    private static Typeface LanguageLabelFace => _languageLabelFace ??=
        new Typeface(LocWpf.LanguageFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static Typeface LanguageHubFace => _languageHubFace ??=
        new Typeface(LocWpf.LanguageFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Typeface LabelFace =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    // ── Per-material label typography ─────────────────────────────────────────
    // Whether the condensed Windows system font "Bahnschrift SemiCondensed" (or its family "Bahnschrift")
    // is actually installed — checked once at startup so a missing font falls back cleanly to Segoe UI
    // SemiBold instead of WPF silently substituting something unpredictable.
    private static readonly bool BahnschriftAvailable = Fonts.SystemFontFamilies.Any(f =>
        f.Source.Equals("Bahnschrift SemiCondensed", StringComparison.OrdinalIgnoreCase) ||
        f.Source.Equals("Bahnschrift",               StringComparison.OrdinalIgnoreCase));
    // Mesa: Jua (OFL 1.1, embedded — see THIRD-PARTY-LICENSES.md §7), a rounded soft-slab display face.
    // Regular weight only — the family ships no Bold, and letting WPF synthesize one thickens the rounded
    // terminals into blobs. Same reason ReactorHubFace pins Regular.
    // Lazy, not a static field initializer, for the reason spelled out on ReactorHubFace: the pack:// scheme
    // is only registered once WPF's Application exists, so building this Uri during static init would throw
    // a TypeInitializationException and take the whole class down with it.
    private static Typeface? _terraLabelFace;
    private static Typeface TerraLabelFace => _terraLabelFace ??=
        new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Assets/fonts/#Jua"),
                     FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    // Salvage: condensed sprayed-through-a-template look — Bahnschrift SemiCondensed if installed, else
    // fall back to the default SemiBold (labels are additionally upper-cased for this material).
    // Kawaii: Sour Gummy (OFL 1.1, embedded — see THIRD-PARTY-LICENSES.md §7), for slice labels, the hub
    // readout and the preview tile. Pinned Regular for the same reason as the other two — the file is the
    // upstream variable font, so the family also exposes Thin…Black instances and an Oblique set; naming the
    // weight/style explicitly keeps WPF from drifting onto one of them. Lazy for the pack:// reason spelled
    // out on ReactorHubFace.
    private static Typeface? _kawaiiFace;
    private static Typeface KawaiiFace => _kawaiiFace ??=
        new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Assets/fonts/#Sour Gummy"),
                     FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface SalvageLabelFace = BahnschriftAvailable
        ? new Typeface(new FontFamily("Bahnschrift SemiCondensed"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Condensed)
        : LabelFace;
    /// <summary>Per-material slice-label size multiplier, applied on top of the shared count-driven formula,
    /// because em size is not apparent size: a display face's cap height relative to its em differs from
    /// Segoe UI's. Kept separate from that formula, which encodes how labels yield to a crowded wheel and is
    /// material-independent.</summary>
    private double LabelSizeMul => _custom is { } custom ? custom.Spec.LabelSizeMul
                                 : _material == "mesa" ? MesaLabelSizeMul : 1.0;
    private const double MesaLabelSizeMul = 1.08;

    /// <summary>Per-material slice-label typeface: mesa → Jua; salvage → condensed Bahnschrift (or the
    /// SemiBold fallback); kawaii → Sour Gummy; reactor → Share Tech, the same face as its hub text;
    /// everything else → the default <see cref="LabelFace"/>. No material varies by <paramref name="armed"/>.</summary>
    private Typeface LabelFaceFor(bool armed) => Loc.UsesSystemFace ? LanguageLabelFace : _custom?.Face is { } customFace ? customFace : _material switch
    {
        "mesa"                        => TerraLabelFace,
        "salvage"                     => SalvageLabelFace,
        "kawaii"                      => KawaiiFace,
        "reactor"                     => ReactorHubFace,
        _                             => LabelFace,
    };

    // ── Scrubber brushes/pens ─────────────────────────────────────────────────
    // Volume meter, per theme. Dark = a toned cyan pulled toward the wheel's cool rim-light tone; light =
    // #D76E64 (coral). The centre hub keeps its material fill during a volume change (no darkening).
    private static readonly Pen   ScrubFgDark    = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 150, 224, 240)), 5.0);
    private static readonly Pen   ScrubBgDark    = FrozenPen(new SolidColorBrush(Color.FromArgb( 55, 150, 224, 240)), 5.0);
    private static readonly Brush ScrubTextDark  = Frozen(new SolidColorBrush(Color.FromArgb(255, 176, 232, 246)));
    private static readonly Pen   ScrubFgLight   = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 215, 110, 100)), 5.0);
    private static readonly Pen   ScrubBgLight   = FrozenPen(new SolidColorBrush(Color.FromArgb( 55, 215, 110, 100)), 5.0);
    private static readonly Brush ScrubTextLight = Frozen(new SolidColorBrush(Color.FromArgb(255, 215, 110, 100)));
    private static readonly Typeface ScrubberFace =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    // Reactor hub text is set in Share Tech (OFL 1.1, embedded — see THIRD-PARTY-LICENSES.md §7): a
    // squared techno face that matches the fused-keyhole/circuit look. Regular weight only — the family
    // ships no Bold, and letting WPF synthesize one smears the glyphs' hard corners.
    // LAZY, not a static field initializer: the pack:// scheme is only registered once WPF's Application
    // exists, so building this Uri during static init would throw a TypeInitializationException and take
    // the whole class down with it if anything ever touched a static here first.
    private static Typeface? _reactorHubFace;
    private static Typeface ReactorHubFace => _reactorHubFace ??=
        new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Assets/fonts/#Share Tech"),
                     FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    /// <summary>The hub-text typeface for the current material (reactor swaps in Share Tech, kawaii in Sour
    /// Gummy; everything else keeps the bold Segoe UI the hub has always used).</summary>
    private Typeface HubFace => Loc.UsesSystemFace ? LanguageHubFace : _custom?.Face is { } customHubFace ? customHubFace : _material switch
    {
        "reactor" => ReactorHubFace,
        "kawaii"  => KawaiiFace,
        _         => ScrubberFace,
    };

    // ── Volume-mixer balance bar brushes/pens (same palette as the scrubber above, per theme) ────────
    private static readonly Brush MixTrackDark   = Frozen(new SolidColorBrush(Color.FromArgb( 55, 150, 224, 240)));
    private static readonly Brush MixTrackLight  = Frozen(new SolidColorBrush(Color.FromArgb( 55, 215, 110, 100)));
    private static readonly Brush MixThumbDark   = Frozen(new SolidColorBrush(Color.FromArgb(255, 150, 224, 240)));
    private static readonly Brush MixThumbLight  = Frozen(new SolidColorBrush(Color.FromArgb(255, 215, 110, 100)));
    private static readonly Pen   MixThumbOutline = FrozenPen(new SolidColorBrush(Color.FromArgb(160, 20, 20, 30)), 1.2);
    private static readonly Pen   MixNotchPen     = FrozenPen(new SolidColorBrush(Color.FromArgb(130, 128, 132, 140)), 1.5);
    private static readonly Brush MixLabelInk     = Frozen(new SolidColorBrush(Color.FromArgb(190, 190, 196, 206)));

    // ── Confirm (hold-to-fire) brushes/pens ───────────────────────────────────
    private static readonly Brush ConfirmFill =
        Frozen(new SolidColorBrush(Color.FromArgb(235, 250, 214, 214)));   // light red
    private static readonly Pen ConfirmPen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 200, 60, 60)), 2.5);
    private static readonly Pen ConfirmArcBg =
        FrozenPen(new SolidColorBrush(Color.FromArgb(70, 200, 60, 60)), 6.0);
    private static readonly Pen ConfirmArcFg =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 224, 40, 40)), 6.0);


    // ── Delete styling (edit-mode hold-□) ──────────────────────────────────────
    // The slice gets a thick red outline (and red icon + label) while the delete dwell fills.
    private static readonly Pen DeleteOutlinePen =
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 200, 36, 32)), 5.0);
    private static readonly Brush DeleteContentBrush =
        Frozen(new SolidColorBrush(Color.FromArgb(255, 200, 36, 32)));

    // ── Edit mode (V2 in-wheel editing) ────────────────────────────────────────
    private static readonly Pen CarryLiftPen =      // outline on the slice currently being carried
        FrozenPen(new SolidColorBrush(Color.FromArgb(255, 120, 200, 255)), 3.0);
    private static readonly Brush EditHubBrush =
        Frozen(new SolidColorBrush(Color.FromArgb(255, 20, 20, 30)));

    // Battery readout — light grey so it isn't distracting.
    private static readonly Brush BatteryInk =
        Frozen(new SolidColorBrush(Color.FromArgb(150, 150, 150, 158)));
    /// <summary>The light-hub mirror of <see cref="BatteryInk"/>. ⚠ One grey cannot serve both: the light
    /// grey above carries almost no contrast on a near-white hub, and only ever read there because the
    /// hub's sub-opaque fill composited over the game and darkened it. The opaque wheel body
    /// removed that accident, so the light materials need their own ink —
    /// tuned to the same ~3:1 weight the light grey has on a dark hub, so the readout stays subdued.</summary>
    private static readonly Brush BatteryInkOnLight =
        Frozen(new SolidColorBrush(Color.FromArgb(150, 70, 72, 84)));

    /// <summary>Extra lift for Kawaii's hub battery glyph, on top of the shared 10px bottom gap — the cloud
    /// hub's scalloped lower edge doesn't follow the circle the gap is measured from.</summary>
    private const double KawaiiBatteryLiftPx = 10.0;

    /// <summary>Battery-glyph ink for the current material. Kawaii and Terra swap the neutral grey for
    /// their OWN accent — the same colour each already uses for the hub's state-change arrow — because a
    /// translucent grey reads as dirt on a milky-pink or cream hub. Kept saturated rather than literally
    /// pale: both hubs are near-white, so a pastel-on-pastel glyph would disappear.
    /// <para>The remaining materials pick the ink by hub tone (<see cref="BatteryInkOnLight"/>), and a
    /// drop-in theme supplies its own — the glyph must never inherit an ink chosen for the opposite
    /// background, which is exactly how it went invisible on the light materials.</para></summary>
    private Brush BatteryInkFor() => _custom?.Label is SolidColorBrush { Color: var themeInk }
        ? BatteryInkFrom(themeInk)   // drop-in theme: its own ink, dimmed (see BatteryInkFrom)
        : _material switch
        {
            "kawaii" => KawaiiArrowBrush,   // #F491C1 pink
            "mesa"  => TerraBlobFill,      // terracotta
            _        => DarkMaterial ? BatteryInk : BatteryInkOnLight,   // follows the hub, like every other ink
        };

    /// <summary>A drop-in theme's battery ink: the theme's OWN label colour — the one its author chose to
    /// be legible on that hub — carried at <see cref="BatteryInk"/>'s alpha so the readout stays quiet
    /// rather than reading as bright as a label. The neutral grey can't serve a themed hub: on a saturated
    /// or dark package palette it reads as dirt, the same reason Kawaii and Mesa swap it for their accents.</summary>
    private static Brush BatteryInkFrom(Color c)
    {
        if (!BatteryInkCache.TryGetValue(c, out var b))
            BatteryInkCache[c] = b = Frozen(new SolidColorBrush(Color.FromArgb(BatteryInkAlpha, c.R, c.G, c.B)));
        return b;
    }
    private const byte BatteryInkAlpha = 150;   // matches BatteryInk's alpha
    private static readonly Dictionary<Color, Brush> BatteryInkCache = new();

    // ── Status readout (toggle confirmation in the centre) ────────────────────
    // Dark text to sit on the light (slice-coloured) centre.
    private static readonly Brush StatusStateBrush =
        Frozen(new SolidColorBrush(Color.FromArgb(255, 15, 15, 25)));      // dark, matches labels (current state)
    private static readonly Brush StatusNextBrush =
        Frozen(new SolidColorBrush(Color.FromArgb(255, 122, 128, 140)));   // medium-dark grey (changed state)

    // Light ink for the DARK themes (flat-dark / gloss-dark), where text sits on a dark slice/hub.
    private static readonly Brush LightLabelBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 238, 240, 246)));
    private static readonly Brush LightStateBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 246, 248, 252)));
    private static readonly Brush LightNextBrush  = Frozen(new SolidColorBrush(Color.FromArgb(255, 168, 176, 192)));
    private static readonly Brush LightHintBrush  = Frozen(new SolidColorBrush(Color.FromArgb(255, 222, 227, 238)));

    // ── Hub TITLE chip — reuses the L1/R1 "physical button" look (glossy-dark gradient + cool rim + cool
    //    label ink) so the header matches the button prompts drawn beside it. Opaque (no fade). ──────────
    private const double HubChipPadX = 12, HubChipPadY = 3;

    /// <summary>Space between the two lines of a two-line hub chip. Shared, because a caller that stacks
    /// something above one has to predict its height exactly, and a private copy of this number in the
    /// drawing code would drift from the caller's copy without anything failing loudly.</summary>
    private const double HubChipLineGap = 2;

    // Chip ink: cool-white on the glossy-dark button (gloss-dark); otherwise the material's own slice-label
    // ink — light on the dark flat-dark swatch, dark on the light gloss-light glass / flat-light swatch.
    private Brush HubChipInk => _material == "obsidian" ? ControllerButtons.LabelInkBrush : InkLabel;

    /// <summary>A hub-title FormattedText in the chip's ink (left-aligned so the chip can wrap tightly
    /// around it).</summary>
    private FormattedText HubTitleText(string text, double fontSize, double ppd) =>
        new(text, CultureInfo.InvariantCulture, WpfFlow.LeftToRight, HubFace, fontSize, HubChipInk, ppd)
        { TextAlignment = TextAlignment.Left, MaxTextWidth = (HubHoleRadius - 6) * 2.0 };

    /// <summary>The hub's ACTUAL inner radius — the centre hole, which GROWS as the slice ring thins
    /// (81 thick → 136.8 medium → 192.6 thin). Hub type and glyphs were historically laid out against the
    /// <see cref="InnerRadius"/> CONSTANT instead, which is the thick-ring value, so on the thinner rings
    /// everything was sized and width-clamped for a hole a third the size of the one it was floating in.</summary>
    private double HubHoleRadius => _sliceInner;

    /// <summary>Draw a hub title on a rounded button chip (1px rim), centred at <paramref name="centerX"/>
    /// with its top at <paramref name="topY"/>, mirroring the current material's armed-slice look:
    /// gloss-dark → the L1/R1 glossy-dark button; gloss-light → the cool-blue armed glass; flat materials →
    /// the flat armed swatch. Returns the chip height so callers can lay out following content.</summary>
    private double DrawHubChip(DrawingContext dc, FormattedText ft, double centerX, double topY,
                               double padScale = 1.0, bool allowSalvageTilt = true)
    {
        double extra = _material == "kawaii" ? KawaiiPillPadExtra : 0.0;   // cushion inside the ribbon's layered edge
        double padX = HubChipPadX * padScale + extra, padY = HubChipPadY * padScale + extra;
        double chipW = ft.Width + padX * 2, chipH = ft.Height + padY * 2;
        var rect = new Rect(centerX - chipW / 2, topY, chipW, chipH);
        if (_material == "salvage")
        {
            // Oversized cloth banner with ENLARGED text centred on the visible cloth — its own geometry,
            // not the pill rect. Still returns the pill height, so the hub's layout below is unchanged and
            // only the banner grows (northward).
            double ts    = SalvageBannerTextScale;
            var    cloth = SalvageBannerRect(rect, ft.Width * ts, ft.Height * ts);
            bool tilted = allowSalvageTilt && PushSalvageBannerTilt(dc, cloth);   // cloth + text rotate together
            DrawSalvageBanner(dc, cloth, chipH * 0.32);
            DrawSalvageBannerText(dc, ft, cloth, ts, ft.Height * ts);
            if (tilted) dc.Pop();
            return chipH;
        }
        if (_material == "obsidian")
            ControllerButtons.DrawPill(dc, rect);   // glossy-dark physical button (its rim is already 1px)
        else if (_material == "kawaii")
            DrawRibbonBanner(dc, rect, chipH * 0.45);   // ribbon banner, notched ends
        else
            dc.DrawRoundedRectangle(ArmedFillFor(), HubChipRim, rect, chipH * 0.32, chipH * 0.32);   // material's armed fill
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, topY + padY));
        return chipH;
    }

    // ── Salvage: hub pills draw as a ragged red cloth banner instead of the flat armed swatch ──
    private static BitmapSource? _salvageBannerBmp;
    private static bool _salvageBannerTried;
    /// <summary>The banner art's opaque CORE as a fraction of the bitmap — the cloth is inset inside frayed
    /// ends and transparent margins, so the destination rect is inflated by 1/core to seat the pill's text
    /// box on the cloth rather than on the fringe. Measured off the source (both axes land at ~0.755).</summary>
    private const double SalvageBannerCore = 0.755;
    /// <summary>Visible cloth width as a multiple of the live hub radius (<see cref="HubRadius"/>) — over 2.0
    /// on thick, so the banner overlaps the hub's sides, and tracks the hub as the thickness changes. Tuned
    /// per thickness because no single multiple sits right on both. Thick's figure is before the 10%
    /// <see cref="HubContentScale"/> shrink (net 2.10). A long header still wins — the width is the max of
    /// this and the text's own box.</summary>
    private double SalvageBannerOfHub => _thickRing ? 2.34 : 1.315;
    /// <summary>The cloth's own width : height ratio (the art's opaque core, 1252 × 338 at 0.755 both ways).
    /// The banner is never squashed below it — a wide banner gets proportionally taller.</summary>
    private const double SalvageBannerAspect = 3.70;
    /// <summary>Header text is drawn this much larger on the cloth than on the other materials' pills — the
    /// banner is far bigger than the pill it replaces, so pill-sized text floated in the middle of it.</summary>
    private const double SalvageBannerTextScale = 1.35;
    /// <summary>Text shadow on the cloth: 1px south, faked-3px blur, black. WPF can't blur inside a
    /// DrawingContext without an effect pass, so the shadow is the text geometry stamped at a ring of small
    /// offsets (the same trick as <see cref="ShadowTaps"/>), each at this alpha — the taps stack, so the
    /// shadow reads far darker than any one of them.</summary>
    private static readonly Brush SalvageBannerTextShadow =
        Frozen(new SolidColorBrush(Color.FromArgb(56, 0, 0, 0)));
    private const double SalvageBannerShadowDy = 1.0, SalvageBannerShadowBlur = 3.0;

    /// <summary>Uniform scale on the whole banner, and a westward nudge as a fraction of its own width.
    /// Applied after the size floors, so a long header's text box is scaled up too rather than being cropped
    /// by the growth.</summary>
    private const double SalvageBannerScale = 1.10, SalvageBannerShiftX = -0.05;
    /// <summary>Two-line headlines (the "Practice" badge) ride this much higher than dead-centre on the
    /// cloth — the art's cloth sags toward the bottom, so a geometrically centred block reads low.
    /// Single-line headers are unaffected.</summary>
    private const double SalvageBannerTwoLineLift = 5.0;

    /// <summary>How far ABOVE its pill rect a hub chip's art actually reaches. Zero on every material except
    /// Salvage, whose cloth banner is far larger than the pill it replaces and is anchored by its BOTTOM edge
    /// — so it grows northward out of the box <see cref="DrawHubChip"/> was handed.
    ///
    /// <para>Callers that stack something above a hub chip (the storefront cold-start logo) must reserve
    /// this, or the banner paints straight over it. Measured, not drawn — the same geometry
    /// <see cref="SalvageBannerRect"/> will produce, plus a small allowance for the ±3° tack tilt, which
    /// lifts the cloth's corners past its axis-aligned box.</para></summary>
    private double HubChipRiseAbove(FormattedText ft)
        => HubChipRiseAbove(ft.Width, ft.Height, ft.Height + HubChipPadY * 2, SalvageBannerTextScale);

    /// <summary>The same measurement for a chip whose contents aren't one line of text at the default banner
    /// text scale — a two-line badge draws its lines at 1.0 and is taller, so it produces a different cloth
    /// and a different rise.</summary>
    private double HubChipRiseAbove(double contentW, double contentH, double chipH, double textScale)
    {
        if (_material != "salvage") return 0.0;
        var cloth = SalvageBannerRect(new Rect(0, 0, contentW + HubChipPadX * 2, chipH),
                                      contentW * textScale, contentH * textScale);
        double rise = Math.Max(0.0, cloth.Height - chipH);
        // Tilt allowance: rotating a w×h box by θ about its centre raises its top by (w·sinθ + h·(cosθ−1))/2.
        double rad = SalvageBannerTilts.Max(Math.Abs) * Math.PI / 180.0;
        return rise + (cloth.Width * Math.Sin(rad) + cloth.Height * (Math.Cos(rad) - 1)) / 2.0;
    }

    /// <summary>The visible cloth rect for a hub pill: wide enough to overlap the hub's sides (or to hold the
    /// text, whichever is larger), never squashed below the art's aspect, and BOTTOM-ANCHORED to the pill —
    /// so scaling the banner up grows it northward and it can't creep down over the hub's content.</summary>
    private Rect SalvageBannerRect(Rect pill, double contentW, double contentH)
    {
        double w = Math.Max(HubRadius * SalvageBannerOfHub, contentW + HubChipPadX * 2) * SalvageBannerScale;
        double h = Math.Max(contentH + HubChipPadY * 2, w / SalvageBannerAspect) * SalvageBannerScale;
        return new Rect(pill.Left + pill.Width / 2.0 - w / 2.0 + w * SalvageBannerShiftX, pill.Bottom - h, w, h);
    }

    /// <summary>Draw hub text scaled by <paramref name="scale"/> and centred in the visible cloth, with the
    /// 1px-south soft-black drop shadow. <paramref name="blockH"/> is the already-scaled height of the whole
    /// text block, so a two-line badge centres as one unit rather than by its first line.</summary>
    private void DrawSalvageBannerText(DrawingContext dc, FormattedText ft, Rect cloth,
                                       double scale, double blockH, double lineDy = 0.0)
    {
        var at = new Point(cloth.X + (cloth.Width - ft.Width * scale) / 2.0,
                           cloth.Y + (cloth.Height - blockH) / 2.0 + lineDy);
        if (scale is < 0.999 or > 1.001) dc.PushTransform(new ScaleTransform(scale, scale, at.X, at.Y));
        var geo = ft.BuildGeometry(at);
        double spread = SalvageBannerShadowBlur / 2.0 / scale;   // taps span ±2 → a ~3px blur, in screen px
        foreach (var (dx, dy) in ShadowTaps)
        {
            dc.PushTransform(new TranslateTransform(dx * spread,
                                                    SalvageBannerShadowDy / scale + dy * spread));
            dc.DrawGeometry(SalvageBannerTextShadow, null, geo);
            dc.Pop();
        }
        dc.DrawText(ft, at);
        if (scale is < 0.999 or > 1.001) dc.Pop();
    }

    /// <summary>The cloth is tacked up slightly crooked, by a fixed angle per slice, so moving between
    /// hub-using slices visibly shifts it without anything animating. Indexed by the armed slice rather than
    /// randomised at draw time: a live random would jitter every frame. The text rides with the cloth.</summary>
    private static readonly double[] SalvageBannerTilts =
        { -2.6, 1.8, 3.0, -1.1, 2.2, -3.0, 0.9, -2.0, 2.7, -1.5, 1.3, -2.4 };   // one per slice, ±3°

    /// <summary>Push the armed slice's tilt about the cloth's centre; returns true if a push happened (pop
    /// it). Wraps BOTH the cloth and its text, so they rotate as one object.</summary>
    private bool PushSalvageBannerTilt(DrawingContext dc, Rect cloth)
    {
        int i = _sm.ArmedIndex;
        if (i < 0) return false;                       // unarmed (e.g. the practice badge) hangs level
        double tilt = SalvageBannerTilts[i % SalvageBannerTilts.Length];
        if (tilt == 0.0) return false;
        dc.PushTransform(new RotateTransform(tilt, cloth.Left + cloth.Width  / 2.0,
                                                   cloth.Top  + cloth.Height / 2.0));
        return true;
    }

    /// <summary>Draw the salvage cloth banner behind a hub pill of <paramref name="rect"/>. The image is
    /// stretched to fit (the cloth is a single strip — there's no repeatable middle to nine-slice), inflated
    /// so its opaque core covers the text box. Falls back to the material's armed swatch if the art is
    /// missing, so a hub header can never render as bare text.</summary>
    private void DrawSalvageBanner(DrawingContext dc, Rect rect, double corner)
    {
        if (SalvageBannerBitmap() is not { } bmp)
        {
            dc.DrawRoundedRectangle(ArmedFillFor(), HubChipRim, rect, corner, corner);   // art missing → swatch
            return;
        }
        double gw = rect.Width  * (1.0 / SalvageBannerCore - 1.0) / 2.0;
        double gh = rect.Height * (1.0 / SalvageBannerCore - 1.0) / 2.0;
        dc.DrawImage(bmp, new Rect(rect.Left - gw, rect.Top - gh,
                                   rect.Width + gw * 2, rect.Height + gh * 2));
    }

    private static BitmapSource? SalvageBannerBitmap() =>
        LoadFrozenBitmap("Assets/salvage-banner.png", "salvage banner",
                         ref _salvageBannerBmp, ref _salvageBannerTried);

    // ── Kawaii: hub pills draw as ribbon banners — the text rect plus a tail extending `notch` past each
    //    end, with a dovetail notch cut back to the rect edge. Three layers, drawn outside-in:
    //      · a 4px white outer outline outside the main one, via a 12px pen underneath — centred strokes
    //        span ±6px, and the fill + 4px main stroke cover everything inward of +2, leaving the +2…+6 band;
    //      · the 4px main orchid outline;
    //      · a dashed gold back-stitch inset inside the path (see StitchPath).
    //    Round joins throughout — the dovetail's sharp V would spike badly under a 12px miter. ──
    private static readonly Brush KawaiiPillFill  = Frozen(new SolidColorBrush(Color.FromArgb(250, 247, 238, 250)));
    private static readonly Pen   KawaiiPillPen   = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(255, 214, 186, 226)), 4.0);
    private static readonly Pen   KawaiiPillOuter = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 12.0);
    // Stitch centreline inset from the banner path. Budget: the 4px main stroke is centred, so its inner
    // edge sits 2px in; + a 2px clear gap; + half the 1px stitch pen = 4.5px.
    private const double KawaiiStitchInset = 4.5;
    private const double KawaiiStitchUnderDy = 1.0;   // the white highlight copy sits this far below (emboss)
    // Extra breathing room inside a kawaii pill so the three outlines + stitch don't crowd the text.
    private const double KawaiiPillPadExtra = 4.0;
    // Per-layer alpha — gold 55%, white highlight opaque — baked into the pens, since the two layers don't
    // share an opacity group.
    private static readonly Pen KawaiiStitchPen       = MakeStitchPen(Color.FromArgb(140, 0xD9, 0xB4, 0x5B));   // gold filigree @55%
    private static readonly Pen KawaiiStitchHighlight = MakeStitchPen(Colors.White);                            // emboss highlight, 1px lower, opaque
    private static Pen MakeStitchPen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        // Identical geometry/dash phase to its twin, so the highlight's dashes line up with the gold ones.
        var p = new Pen(b, 1.0)
        {
            LineJoin  = PenLineJoin.Round,
            DashCap   = PenLineCap.Flat,
            // Dash units are multiples of THICKNESS, so at 1.0 they are literal px: 4px stitch, 2px gap.
            DashStyle = new DashStyle(new double[] { 4.0, 2.0 }, 0),
        };
        p.Freeze();
        return p;
    }

    /// <summary>Draw a kawaii hub pill: white outer outline, main outline, then the gold back-stitch.</summary>
    private void DrawRibbonBanner(DrawingContext dc, Rect rect, double notch)
    {
        var geo = BuildRibbonBanner(rect, notch);
        dc.DrawGeometry(null, KawaiiPillOuter, geo);    // white halo (its inner half gets covered below)
        dc.DrawGeometry(KawaiiPillFill, KawaiiPillPen, geo);
        if (rect.Width <= KawaiiStitchInset * 2 + 4 || rect.Height <= KawaiiStitchInset * 2 + 4) return;
        // The stitch path is cached in LOCAL space (origin at 0,0) so the key is just the pill's size —
        // its screen position changes constantly, its dimensions almost never do.
        var stitch = StitchPath(rect.Width, rect.Height, notch);
        // Two passes: an opaque WHITE copy 1px lower (the emboss highlight), then the 55% gold stitch over
        // it. Alphas live in the pens, so the translucent gold lets the white read through where they
        // overlap — that wash IS the effect here, not the muddying a shared opacity group used to prevent.
        dc.PushTransform(new TranslateTransform(rect.X, rect.Y + KawaiiStitchUnderDy));
        dc.DrawGeometry(null, KawaiiStitchHighlight, stitch);
        dc.Pop();
        dc.PushTransform(new TranslateTransform(rect.X, rect.Y));
        dc.DrawGeometry(null, KawaiiStitchPen, stitch);
        dc.Pop();
    }

    // Cache of eroded stitch paths by (pill w, h, notch) — the erosion is a widen + a boolean combine, far
    // too costly to redo every frame (the hub redraws continuously while a wheel is up).
    private readonly Dictionary<(double w, double h, double n), Geometry> _stitchCache = new();

    /// <summary>The gold back-stitch path: the banner outline offset INWARD at a constant perpendicular
    /// distance, via morphological erosion (shape minus a band of ±inset widened around its own outline).
    /// <para>Deflating the banner's rect by the inset is NOT a parallel offset: at a corner of interior
    /// angle θ the offset vertex must travel <c>inset / sin(θ/2)</c> along the bisector, so on the
    /// dovetail's sharp V a deflated path stays ~5px away on the straight runs but converges onto the main
    /// outline at every point. Erosion offsets every edge by the true perpendicular distance and blunts the
    /// acute corners, which is exactly how real stitching sits.</para></summary>
    private Geometry StitchPath(double w, double h, double notch)
    {
        var key = (Math.Round(w, 1), Math.Round(h, 1), Math.Round(notch, 1));
        if (_stitchCache.TryGetValue(key, out var cached)) return cached;
        if (_stitchCache.Count > 64) _stitchCache.Clear();   // defensive: chip sizes are few and stable

        var banner = BuildRibbonBanner(new Rect(0, 0, w, h), notch);
        var widenPen = new Pen(Brushes.Black, KawaiiStitchInset * 2)
        {
            LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round,
        };
        var eroded = Geometry.Combine(banner, banner.GetWidenedPathGeometry(widenPen),
                                      GeometryCombineMode.Exclude, null);
        eroded.Freeze();
        _stitchCache[key] = eroded;
        return eroded;
    }
    private static Geometry BuildRibbonBanner(Rect rect, double notch)
    {
        double midY = rect.Top + rect.Height / 2.0;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(rect.Left - notch, rect.Top), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(rect.Right + notch, rect.Top), true, false);
            ctx.LineTo(new Point(rect.Right, midY), true, false);            // dovetail cut, right end
            ctx.LineTo(new Point(rect.Right + notch, rect.Bottom), true, false);
            ctx.LineTo(new Point(rect.Left - notch, rect.Bottom), true, false);
            ctx.LineTo(new Point(rect.Left, midY), true, false);             // dovetail cut, left end
        }
        g.Freeze();
        return g;
    }

    // Soft drop-shadow under the wheel (drawn as an ellipse of radius OuterRadius+18; offset 1.0 = its
    // edge). On THICK the wheel is a near-full disc so the shadow is a soft dark blob through the centre
    // fading out. On MEDIUM/THIN the slices' inner edge moves out, leaving a donut gap between the hub and
    // the ring — so we punch a TRANSPARENT band through that gap (shadow only under the hub + the ring).
    /// <summary>How far the backglow spills past the shape it sits under.</summary>
    private const double BackglowHaloPx = 18.0;

    /// <summary>The wheel drop-shadow. <paramref name="includeHub"/> false drops the central hub shadow —
    /// a donut only under the slice ring, clear through the middle — for when the hub circle is hidden
    /// ("Hide wheel centers"), so no stray circular shadow sits under an empty centre.</summary>
    private Brush BuildBackglow(bool includeHub)
    {
        const double R = OuterRadius + BackglowHaloPx;
        double Off(double px) => px / R;      // px radius → relative gradient offset (0 = centre, 1 = ellipse edge)
        const byte D = 65;   // darkest shadow opacity (85 → 72 → 65; lightened ~15% then ~10% more) — all materials
        Color dark = Color.FromArgb(D, 0, 0, 12), clear = Color.FromArgb(0, 0, 0, 0);

        double bandIn = _sliceInner;          // slice-ring inner edge (px)

        var g = new RadialGradientBrush();    // default: relative, centre 0.5,0.5, radius 0.5
        var s = g.GradientStops;

        if (!includeHub)
        {
            // Hidden centre: NO central shadow — a donut under the slice ring only, clear through the middle
            // (covers all thicknesses; thick's tiny hub gap just leaves a small clear core).
            const double feather = BackglowDonutFeather, inset = BackglowDonutInset;
            double flatIn  = bandIn + inset;
            double flatOut = OuterRadius - inset;
            s.Add(new GradientStop(clear, 0.00));
            s.Add(new GradientStop(clear, Off(Math.Max(0, flatIn - feather))));
            s.Add(new GradientStop(dark,  Off(flatIn)));
            s.Add(new GradientStop(dark,  Off(flatOut)));
            s.Add(new GradientStop(clear, Off(flatOut + feather)));
            s.Add(new GradientStop(clear, 1.00));
            g.Freeze();
            return g;
        }

        // With the hub disc now scaling to FILL the centre opening at every thickness (HubRadius tracks
        // _sliceInner), there is no big empty centre left to punch a clear band through — the old
        // medium/thin "donut" shadow cut a hole exactly where the enlarged hub now sits. A soft disc
        // (solid through 50%, falloff to the edge) is the correct shadow at ALL thicknesses.
        s.Add(new GradientStop(dark, 0.00));
        s.Add(new GradientStop(dark,  0.50));
        s.Add(new GradientStop(clear, 1.00));
        g.Freeze();
        return g;
    }

    // Shared with BackglowRing, which must cut its annulus exactly where the donut gradient goes clear.
    private const double BackglowDonutFeather = 20.0, BackglowDonutInset = 9.0;

    /// <summary>Annulus geometry for the DONUT backglow variant: everything inside
    /// (flatIn − feather) is fully transparent by construction, so drawing the ring instead of the full
    /// ellipse is pixel-identical while skipping the dead centre's fill. The excluded inner ellipse keeps
    /// the combined geometry's bounds equal to the outer ellipse's, so the RELATIVE gradient maps the
    /// same way it did on the plain ellipse draw.</summary>
    private Geometry BackglowRing(Point center, double r)
    {
        double rIn = Math.Max(0.0, _sliceInner + BackglowDonutInset - BackglowDonutFeather);
        var key = (center.X, center.Y, r, rIn);
        if (_backglowRing is null || _backglowRingKey != key)
        {
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(center, r, r), new EllipseGeometry(center, rIn, rIn));
            ring.Freeze();
            _backglowRing = ring;
            _backglowRingKey = key;
        }
        return _backglowRing;
    }
    private Geometry? _backglowRing;
    private (double cx, double cy, double r, double rIn) _backglowRingKey;

    // The platform-agnostic selection / dwell / delete state machine.
    private readonly WheelStateMachine _sm = new();

    private readonly DispatcherTimer _confirmTimer;

    // Toggle confirmation drawn in the centre deadzone (label + new state).
    private string? _statusLine1;
    private string? _statusLine2;
    private string? _statusLine2Next;   // predicted next state for the armed "current › next" preview
    /// <summary>Optional small caption under the state line of a post-fire readout (onboarding practice:
    /// "&lt;slice&gt;" over "was selected", so the readout doesn't imply the action ran). Ignored by the armed
    /// "current › next" preview, which has no room for it.</summary>
    private string? _statusCaption;
    private string? _centerNotice;      // hub hint for an armed-but-unconfigured slice (mutually exclusive with status)
    private ImageSource? _statusIcon;   // optional logo above the readout's label (storefront cold-start hub)

    /// <summary>What a hub notice IS, carried beside its text so the layout never has to recognise English
    /// wording: a <see cref="Practice"/> pill and an <see cref="EditHint"/> float in an empty centre with no
    /// hub disc under them; a <see cref="Plain"/> notice gets the disc.</summary>
    public enum NoticeKind { Plain, Practice, EditHint }
    private NoticeKind _noticeKind;

    /// <summary>True while the centre notice is the oversized practice pill — either the bare "Practice"
    /// token (onboarding; default "Actions are Disabled" subtitle) or "Practice|&lt;subtitle&gt;" (the
    /// Settings-open practice mode overrides the subtitle, e.g. "While Settings is Open").</summary>
    private bool PracticeNotice => _noticeKind == NoticeKind.Practice;

    /// <summary>The post-onboarding "Click L3/R3 to edit" discovery hint (composed in App.xaml.cs).</summary>
    private bool EditHintNotice => _noticeKind == NoticeKind.EditHint;

    /// <summary>Notices that render as a bare badge floating in an empty centre — no hub disc behind them.
    /// Both are standing hints rather than readouts; firing a slice swaps the notice for the status readout,
    /// which brings the disc back. Only true while the notice is the thing actually being drawn: a status
    /// readout outranks it in <see cref="DrawHubContent"/>, and that keeps its disc.</summary>
    private bool DiscFreeNotice => _statusLine2 is null && (PracticeNotice || EditHintNotice);

    // ── Onboarding practice-step stick art (OOBE step 2 only) ────────────────
    // Which-thumbstick-aims-this-wheel art (analog-left/right), drawn inside the hub above the Practice
    // pill. OverlayWindow.SetOnboardingPracticeToast owns the gate and forwards here. True only for the
    // practice step's lifetime, so the Settings-open practice pill never grows the art.
    private bool _oobePracticeArt;
    private static BitmapSource? _practiceArtLeft, _practiceArtRight;
    private static bool _practiceArtLeftTried, _practiceArtRightTried;

    /// <summary>Gate the practice-step stick art on/off (forwarded from OverlayWindow's onboarding hook).</summary>
    public void SetOnboardingPracticeArt(bool on)
    {
        if (_oobePracticeArt == on) return;
        _oobePracticeArt = on;
        InvalidateVisual();
    }

    private static BitmapSource? PracticeStickArt(bool right) => right
        ? LoadFrozenBitmap("Assets/analog-right.png", "practice stick art (right)", ref _practiceArtRight, ref _practiceArtRightTried)
        : LoadFrozenBitmap("Assets/analog-left.png",  "practice stick art (left)",  ref _practiceArtLeft,  ref _practiceArtLeftTried);

    private int          _batteryPercent = -1;   // -1 = unknown (hide the readout)
    private bool         _batteryCharging;
    private ImageSource? _batteryIcon;            // resolved light-grey Xbox battery glyph, or null

    // Scrubber percentage FormattedText, rebuilt on value/ink/DPI change only (see DrawHubContent).
    private FormattedText? _pctFt;
    private int    _pctVal = -1;
    private Brush? _pctInk;
    private double _pctPpd;
    // Salvage upper-cases every slice label per frame — memoized string transform.
    private static readonly Dictionary<string, string> UpperLabelCache = new();

    public RadialMenuControl()
    {
        // Pinned: the stick→slice aim math lives in Core (WheelStateMachine) and knows nothing about
        // mirroring, so an RTL ancestor would flip the drawing but not the aim — push right, arm the slice
        // that appears left. Right-to-left languages shape their TEXT runs; the geometry never mirrors.
        FlowDirection = WpfFlow.LeftToRight;
        _confirmTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(WheelStateMachine.ConfirmTickMs),
        };
        _confirmTimer.Tick += ConfirmTick;
        // ⚠ IsVisible alone cannot gate this timer: the overlay window is shown once and never hidden
        // (parking is Menu.Opacity = 0), so IsVisible is true for the whole session. The timer starts
        // here on visibility, but ConfirmTick STOPS it once the wheel is parked and nothing is in
        // flight; SetWheelLive(true) restarts it. Everything that needs ticks while parked (confetti /
        // spark fx draining, a hub shrink-out) is in ConfirmTick's keep-alive condition.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) _confirmTimer.Start();
            else                  _confirmTimer.Stop();
        };
    }

    // ── Public surface (delegates to the state machine) ────────────────────────
    public IReadOnlyList<WheelSlice> Slices
    {
        get => _sm.Slices;
        set { _sm.Slices = value ?? []; InvalidatePeerChildren(); InvalidateVisual(); SchedulePrewarm(); }
    }

    public bool  ScrubberActive { get; set; }
    public float ScrubberLevel  { get; set; }
    /// <summary>MaterialDesign glyph name drawn under the scrubber percentage, naming which level is being
    /// scrubbed: "VolumeHigh" for the speaker (D-pad ▲▼), "Microphone" for the mic (D-pad ◀▶ in mic mode).
    /// The two share this one hub readout, so without it a mic nudge would look like a speaker nudge.</summary>
    public string? ScrubberGlyph { get; set; }
    private ImageSource? _scrubGlyph;      // cached render of ScrubberGlyph in the current hub ink
    private string?      _scrubGlyphName;
    private Brush?       _scrubGlyphInk;

    /// <summary>A single LARGE MaterialDesign glyph filling the hub, with no percentage or ring — the
    /// track-skip cue for the D-pad ◀▶ "song" mode ("SkipBackward"/"SkipForward"). A skip has no level to
    /// show, so it gets a momentary icon instead; App clears it on the same 800 ms timer as the scrubber.
    /// Mutually exclusive with <see cref="ScrubberActive"/> (see OverlayWindow.SetScrubber/ShowHubGlyph).</summary>
    public string? HubGlyph { get; set; }
    private ImageSource? _hubGlyph;
    private string?      _hubGlyphName;
    private Brush?       _hubGlyphInk;

    // ── Volume-mixer balance indicator (D-pad Left/Right while a wheel is open) ────────────────────
    // Mirrors the scrubber above but draws as a horizontal bar BELOW the wheel (outside OuterRadius) so it
    // never collides with the volume readout, which lives in the hub. Auto-fades the same way: App flips
    // MixBalanceActive off on a timer and calls InvalidateVisual — no animated fade of its own.
    public bool   MixBalanceActive    { get; private set; }
    public double MixBalanceValue     { get; private set; } = 0.5;   // 0 = full-left device, 1 = full-right device
    public string? MixLeftName        { get; private set; }
    public string? MixRightName       { get; private set; }
    public bool   MixBalanceNoDevice  { get; private set; }

    /// <summary>Show/update the balance bar. <paramref name="balance01"/> is 0..1 (0 = full-left device,
    /// 1 = full-right device). <paramref name="noDevice"/> swaps the bar for a "No Device" warning label
    /// when neither configured output device could be found.</summary>
    public void ShowMixIndicator(double balance01, string? leftName, string? rightName, bool noDevice = false)
    {
        MixBalanceActive   = true;
        MixBalanceValue    = balance01;
        MixLeftName        = leftName;
        MixRightName       = rightName;
        MixBalanceNoDevice = noDevice;
        InvalidateVisual();
    }

    public void HideMixIndicator()
    {
        if (!MixBalanceActive) return;
        MixBalanceActive = false;
        InvalidateVisual();
    }

    /// <summary>Keep the last armed slice this long after the stick recentres (sticky grace window).</summary>
    public long StickyMs { get => _sm.StickyMs; set => _sm.StickyMs = value; }

    /// <summary>When true, UpdateStick records the stick position but does not recompute the armed index.</summary>
    public bool FreezeArmed { get => _sm.FreezeArmed; set => _sm.FreezeArmed = value; }


    public int ArmedIndex       => _sm.ArmedIndex;
    public int StickyArmedIndex => _sm.StickyArmedIndex;
    public bool ArmedConfirmReady => _sm.ArmedConfirmReady;

    /// <summary>True when the currently-armed slice is GUARDED — arming it starts a hold dwell instead of
    /// arming it outright, so the host can hold its arm cue back until the dwell completes. Excludes edit
    /// mode (which returns before the state machine's confirm block, so no dwell runs) and assign mode
    /// (whose dwell auto-fires and never latches <see cref="ArmedConfirmReady"/>, so a suppressed cue there
    /// would simply be lost).</summary>
    public bool ArmedRequiresConfirm =>
        !_sm.EditMode && _sm.ArmedIndex >= 0 && _sm.RequiresConfirm(_sm.ArmedIndex);

    // "Always show hub" (Settings ▸ Advanced ▸ Developer Settings): default OFF. Hub visibility is now
    // per-material — only TERRA always draws the hub (it's part of its look); every other material
    // (reactor included — its hub only exists as part of the armed fused slice) rest-hides it, drawn only
    // when it carries information: status readout, scrubber, edit mode, notice, or the reactor armed label.
    // Checking the box forces the hub visible on EVERY material.
    private bool _alwaysShowHub;
    public bool AlwaysShowHub
    {
        get => _alwaysShowHub;
        set { if (_alwaysShowHub != value) { _alwaysShowHub = value; InvalidateVisual(); } }
    }

    /// <summary>Reduce Motion — the wheel's slice of the product-wide policy (MotionPolicy). Under it the
    /// wheel suppresses: Reactor's circuit-board parallax + armed-glyph
    /// drift (held at zero offset — boards still draw), Reactor's circuit sparks, Kawaii's ambient hub
    /// twinkles, the fire celebrations (kawaii confetti / salvage spark bloom — never spawned), the hub's
    /// appear/disappear scale (snaps instead), and every material-specific guarded dwell (pour / sweep /
    /// magnet-lock + snap shake) — those fall back to the standard stationary confirm arc. Set by App
    /// (SetReduceMotion) with MotionPolicy's EFFECTIVE value; enabling it live also clears any particles
    /// already in flight so no suppressed effect keeps the 30 ms render tick churning.</summary>
    private bool _reduceMotion;
    public bool ReduceMotion
    {
        get => _reduceMotion;
        set
        {
            if (_reduceMotion == value) return;
            _reduceMotion = value;
            if (value)
            {
                // Stop, don't just hide: in-flight decorative particles would otherwise keep
                // TickKawaiiFx invalidating (and OverlayWindow's confetti-hold polling alive)
                // until their clocks drained.
                _fireFx.Clear();  _fireFxPending  = false;
                _sparkFx.Clear(); _sparkFxPending = false;
            }
            InvalidateVisual();
        }
    }
    /// <summary>Effective "hide the resting hub" (see AlwaysShowHub). No material overrides this —
    /// the setting is the whole answer, and the per-frame reasons a hub appears anyway live in
    /// <see cref="HubDemanded"/>.
    ///
    /// <para>⚠ Must stay a SETTING-level fact that cannot change while a wheel is up — RenderCore keys the
    /// drop-shadow SHAPE (soft disc vs donut) on it, and a value that flips mid-wheel would visibly change
    /// the shadow under the whole wheel. "Show the hub when an arcade slice is armed" therefore lives in
    /// <see cref="HubDemanded"/>, which is allowed to vary per frame, and not here.</para></summary>
    private bool HideCenter => !_alwaysShowHub;

    /// <summary>The armed slice's action when it is an arcade one, else null. Edit and assign modes are
    /// excluded because the hub is already saying something else in both.</summary>
    private ActionConfig? ArmedArcadeAction
    {
        get
        {
            if (_sm.EditMode) return null;
            int i = _sm.ArmedIndex;
            var slices = Slices;
            if (i < 0 || i >= slices.Count) return null;
            var action = slices[i].Action;
            return string.Equals(action?.Type, "arcade", StringComparison.OrdinalIgnoreCase) ? action : null;
        }
    }

    /// <summary>The armed slice opens the arcade — a game OR the Arcade Launcher. The hub is held open and
    /// sized whenever this is true (see <see cref="HubDemanded"/>), which is what lets
    /// <see cref="DrawArcadePreview"/> be a plain draw call rather than a visibility rule.
    ///
    /// <para>⚠ Must cover the LAUNCHER too, not just a resolvable game: the arcade collapse sweeps the slices
    /// in behind the hub, so a launcher slice without one would animate them behind nothing.</para></summary>
    internal bool ArmedIsArcade => ArmedArcadeAction is not null;

    /// <summary>The catalogued game id of the armed arcade slice, or null when it is the Arcade Launcher
    /// (blank command) or names a game this build doesn't have. Both open the picker, so there is no single
    /// game to preview and the hub carries the cabinet instead.</summary>
    private string? ArmedArcadeGameId => ArcadeCatalog.Resolve(ArmedArcadeAction?.Command)?.Id;

    // ── Arcade hub preview ──────────────────────────────────────────────────────
    // Arming an arcade slice shows that game's shot in the hub — the SAME picture the Arcade picker puts on
    // its cabinet screen (ArcadeShots: the player's last board, else the bundled still). Both are a round
    // playfield on a square, so an aspect-preserving fit into a square box centred on the hub lands the
    // game's disc concentric with the hub's — nothing to re-tune per material. A SCALLOPED hub is the one
    // shape that needs more: it fills its own outline and clips to it (see ArcadePreviewOutline).

    /// <summary>Fraction of the hub's DIAMETER the preview disc occupies on a PLAIN-rimmed hub. Under 1.0 so
    /// the hub's own rim and material stay visible as a ring around it — at 1.0 the preview reads as the hub
    /// rather than as something sitting in it. A scalloped rim has no such ring to leave and fills instead:
    /// see <see cref="ArcadePreviewOutline"/>.</summary>
    private const double ArcadePreviewOfHub = 0.92;

    /// <summary>Opacity the preview is drawn at, so the hub's material reads THROUGH the still rather than
    /// the still replacing the hub — at full opacity it stops looking like something the hub is showing.</summary>
    private const double ArcadePreviewOpacity = 0.80;

    /// <summary>Fraction of the launcher plate's DIAMETER the cabinet stands at. A cabinet inscribed corner
    /// to corner in the plate would reach 0.88 at this art's proportions; under that leaves it room.</summary>
    private const double ArcadeLauncherCabinetOfPlate = 0.80;

    /// <summary>The Arcade Launcher's hub: the picker's own wireframe cabinet on the arcade's dark field, so
    /// the slice that opens the carousel previews it the way a game slice previews its board — and so the
    /// collapse has the same disc to sweep the slices in behind.</summary>
    private void DrawArcadeLauncherPreview(DrawingContext dc, Point center)
    {
        bool   fill    = ArcadePreviewOutline(center, out Geometry? outline);
        double r       = HubRadius * (fill ? 1.0 : ArcadePreviewOfHub);
        if (outline is not null) dc.PushClip(outline);
        dc.PushOpacity(ArcadePreviewOpacity);
        dc.DrawEllipse(ArcadeChrome.Field, null, center, r, r);
        // The art is pale line work, so it needs no tint to read on that field. Sized off the plate's own
        // radius, so it keeps its proportion to the field whether that field is inset or fills the outline.
        if (ArcadeArt.Cabinet is { } art)
        {
            double h = 2 * r * ArcadeLauncherCabinetOfPlate;
            double w = h * ArcadeArt.CabinetAspect;
            dc.DrawImage(art, new Rect(center.X - w / 2, center.Y - h / 2, w, h));
        }
        dc.Pop();
        if (outline is not null) dc.Pop();
    }

    /// <summary>Whether the arcade picture fills the hub's whole OUTLINE — clipped to it — instead of
    /// sitting as an inset disc inside a plain rim, and the outline to clip to when it does.
    ///
    /// <para>A rim whose troughs come in FURTHER than the inset leaves no ring to read: the disc spills out
    /// between the scallops while the peaks stand outside it, which is the picture failing to fill the hub
    /// and overflowing it at the same time. Those hubs take the picture edge to edge and clip it to their
    /// own shape; a plain (or barely-textured) rim keeps its ring. Comparing the measured coverage against
    /// the inset is the test — no material list to keep in step with the eight.</para></summary>
    private bool ArcadePreviewOutline(Point center, out Geometry? outline)
    {
        outline = HubCoverageFraction < ArcadePreviewOfHub ? HubSilhouette(center, HubRadius) : null;
        return outline is not null;
    }

    /// <summary>Draw the armed arcade slice's picture, centred in the hub: the game's shot, or for the Arcade
    /// Launcher (<paramref name="gameId"/> null) the carousel as it was last left — falling back to the
    /// cabinet plate until the launcher has been opened once. Silent no-op when a game has neither a
    /// capture nor bundled art — the hub then simply carries whatever it otherwise would.</summary>
    private void DrawArcadePreview(DrawingContext dc, Point center, string? gameId)
    {
        if (ArcadeShots.Get(gameId ?? ArcadeShots.LauncherId).Image is not { } bmp)
        {
            if (gameId is null) DrawArcadeLauncherPreview(dc, center);
            return;
        }
        bool   fill    = ArcadePreviewOutline(center, out Geometry? outline);
        double box     = HubRadius * 2 * (fill ? 1.0 : ArcadePreviewOfHub);
        // Fit the still inside that square without distorting it — the art is near-square, but a future
        // game's could not be, and a stretched screenshot is worse than a smaller one.
        double aspect = bmp.PixelHeight > 0 ? (double)bmp.PixelWidth / bmp.PixelHeight : 1.0;
        double w = aspect >= 1.0 ? box : box * aspect;
        double h = aspect >= 1.0 ? box / aspect : box;
        if (outline is not null) dc.PushClip(outline);
        dc.PushOpacity(ArcadePreviewOpacity);
        dc.DrawImage(bmp, new Rect(center.X - w / 2, center.Y - h / 2, w, h));
        dc.Pop();
        if (outline is not null) dc.Pop();
    }

    // ── Arcade collapse ─────────────────────────────────────────────────────────
    // Choosing an arcade slice keeps the wheel: the slices sweep in behind the hub — outward in both
    // directions from the chosen one — and the hub then grows to sit behind the round playfield for the
    // whole session. It lives here, not in OverlayWindow's dismissal animations, because it needs per-slice
    // timing and the hub radius, neither expressible as a DependencyProperty animation on the control.

    private int _collapseIndex = -1;          // the chosen slice; -1 = not collapsing
    private double _collapseElapsed;
    private long _collapseLastTick;           // NowMs at the last CollapseTick
    private double _hubGrowFactor = 1.0;
    private double _hubGrowTarget = 1.0;
    private bool _collapseHeld;               // slices are gone; hold the grown hub behind the game
    private Action? _collapseDone;
    private DispatcherTimer? _collapseTimer;

    /// <summary>Per-slice travel, and the gap between one slice leaving and its neighbour following.</summary>
    private const double CollapseSliceSeconds = 0.15;
    private const double CollapseStaggerSeconds = 0.0225;
    private const double CollapseHubSeconds = 0.13;
    /// <summary>The wheel-wide backglow is drawn at a FIXED radius under the whole disc, so it can follow
    /// neither the slices sweeping in nor the hub growing — left alone it sits there unmoved while
    /// everything above it travels, and the wheel looks like it's sliding out from under its own shadow.
    /// It ducks out over these seconds as the collapse starts and comes back once the game is up.</summary>
    private const double CollapseGlowFadeSeconds = 0.10;
    private const double CollapseGlowRiseSeconds = 0.125;
    /// <summary>Fraction of the slice sweep that has to be done before the hub starts growing. Under 1 so the
    /// hub grows as the slices finish: waiting for the last one leaves a dead beat where nothing moves.</summary>
    private const double CollapseHubStartFraction = 0.62;

    public bool ArcadeCollapsing => _collapseIndex >= 0 || _collapseHeld;

    /// <summary>Sweep the slices behind the hub and grow the hub to <paramref name="targetHubRadius"/>, then
    /// call <paramref name="onDone"/> — which is where the host opens the game.
    ///
    /// <para>The hub is left grown afterwards and the slices stay gone, so the wheel becomes the game's
    /// backdrop. <see cref="EndArcadeCollapse"/> puts it back.</para></summary>
    public void BeginArcadeCollapse(int index, double targetHubRadius, Action onDone)
    {
        _collapseIndex = Math.Clamp(index, 0, Math.Max(0, Slices.Count - 1));
        _collapseElapsed = 0;
        _collapseLastTick = NowMs;
        _collapseHeld = false;
        _collapseDone = onDone;
        double baseR = BaseHubRadius;
        // Guard the divide: a zero base radius would make the factor infinite and the hub swallow the screen.
        _hubGrowTarget = baseR > 1 ? Math.Max(1.0, targetHubRadius / baseR) : 1.0;
        _hubGrowFactor = 1.0;

        // ── Reduce Motion (accessibility goal A2) ──
        // The sweep is exactly the class of thing the policy exists to suppress: a dozen objects accelerating
        // across the screen with squash-and-stretch. The compliant transition keeps the OUTCOME — slices gone,
        // hub grown to the playfield, game opens — and drops the travel: the hub is simply already the right
        // size and the slices are simply already away. The host still gets its callback, one dispatcher turn
        // later so the caller's own state settles first exactly as it would after a real animation.
        if (_reduceMotion)
        {
            _collapseIndex = -1;
            _collapseHeld = true;
            _hubGrowFactor = _hubGrowTarget;
            InvalidateVisual();
            Dispatcher.BeginInvoke(DispatcherPriority.Render, onDone);
            return;
        }

        _collapseTimer ??= new DispatcherTimer(DispatcherPriority.Render)
        { Interval = TimeSpan.FromMilliseconds(16) };
        _collapseTimer.Tick -= CollapseTick;
        _collapseTimer.Tick += CollapseTick;
        _collapseTimer.Start();
        InvalidateVisual();
    }

    /// <summary>Re-size the held hub to sit behind a disc that wants a hub of <paramref name="hubRadius"/>.
    /// Driven by the arcade's own disc animation (launcher ↔ game), one call per frame, so the two never
    /// drift; refused unless the collapse is holding — nothing else may resize the hub.</summary>
    public void SetArcadeHubRadius(double hubRadius)
    {
        if (!_collapseHeld) return;
        double baseR = BaseHubRadius;
        _hubGrowTarget = _hubGrowFactor = baseR > 1 ? Math.Max(1.0, hubRadius / baseR) : 1.0;
        InvalidateVisual();
    }

    /// <summary>Drop the collapse and restore the normal hub. Called when the game closes.</summary>
    public void EndArcadeCollapse()
    {
        _collapseTimer?.Stop();
        _collapseIndex = -1;
        _collapseHeld = false;
        _collapseElapsed = 0;
        _collapseDone = null;
        _hubGrowFactor = 1.0;
        _hubGrowTarget = 1.0;
        _collapseGlow = 1.0;
        InvalidateVisual();
    }

    /// <summary>Backglow opacity multiplier — see <see cref="CollapseGlowFadeSeconds"/>. 1 whenever no
    /// arcade collapse is in play, so an ordinary wheel is untouched.</summary>
    private double _collapseGlow = 1.0;

    private void CollapseTick(object? sender, EventArgs e)
    {
        // Real elapsed time, not the timer's nominal 16ms: DispatcherTimer ticks late under load, and a
        // fixed increment stretched the sweep whenever a game was hammering the machine. Capped so a
        // multi-second stall skips ahead smoothly instead of teleporting the animation state.
        long now = NowMs;
        double dt = Math.Clamp((now - _collapseLastTick) / 1000.0, 0.001, 0.1);
        _collapseLastTick = now;
        _collapseElapsed += dt;

        // The glow's rise runs AFTER the collapse has landed, on the same timer — the wheel is otherwise
        // repaint-free for the whole session behind the game, so nothing else would drive it.
        if (_collapseHeld)
        {
            _collapseGlow = Math.Clamp(_collapseGlow + dt / CollapseGlowRiseSeconds, 0, 1);
            if (_collapseGlow >= 1) _collapseTimer?.Stop();
            InvalidateVisual();
            return;
        }
        _collapseGlow = 1.0 - Math.Clamp(_collapseElapsed / CollapseGlowFadeSeconds, 0, 1);

        double sweep = CollapseSweepSeconds();
        double hubStart = sweep * CollapseHubStartFraction;
        _hubGrowFactor = _collapseElapsed <= hubStart ? 1.0
            : 1.0 + (_hubGrowTarget - 1.0)
                    * EaseOut(Math.Clamp((_collapseElapsed - hubStart) / CollapseHubSeconds, 0, 1));

        if (_collapseElapsed >= Math.Max(sweep, hubStart + CollapseHubSeconds))
        {
            _collapseIndex = -1;
            _collapseHeld = true;              // hold the grown hub; the game draws on top of it
            _hubGrowFactor = _hubGrowTarget;
            Action? done = _collapseDone;
            _collapseDone = null;
            InvalidateVisual();
            done?.Invoke();                    // the game is up — the next ticks bring the glow back
            return;
        }
        InvalidateVisual();
    }

    /// <summary>Total time for the slice sweep: one slice's travel plus the stagger out to the farthest
    /// slice. Both directions run at once, so the farthest slice is half a ring away, not a whole one.</summary>
    private double CollapseSweepSeconds() =>
        CollapseSliceSeconds + CollapseStaggerSeconds * Math.Max(1, Slices.Count / 2);

    /// <summary>0 (untouched) → 1 (fully behind the hub) for one slice. The delay is the ring distance from
    /// the chosen slice, measured the SHORT way, which is what makes the sweep radiate outward in both
    /// directions from what the player picked rather than sweeping one way past it.</summary>
    private double CollapseProgressFor(int i)
    {
        if (_collapseHeld) return 1;
        if (_collapseIndex < 0) return 0;
        int n = Math.Max(1, Slices.Count);
        int raw = Math.Abs(i - _collapseIndex);
        int hops = Math.Min(raw, n - raw);
        double delay = hops * CollapseStaggerSeconds;
        return Math.Clamp((_collapseElapsed - delay) / CollapseSliceSeconds, 0, 1);
    }

    // ── Edit mode (V2) — App drives these; ops return true when the slice list changed (persist then). ──
    public bool EditMode  => _sm.EditMode;
    public WheelStateMachine.EditPhase EditPhase => _sm.Phase;
    public bool EditCanUndo => _sm.CanUndo;
    public bool EditCanRedo => _sm.CanRedo;
    public int  EditMaxSlices { get; set; } = int.MaxValue;   // host sets the per-wheel cap; legend drops "add" at it
    /// <summary>Hold-□ delete dwell, ms. The host lengthens it while narration is on so the spoken warning
    /// finishes before the delete lands.</summary>
    public double EditDeleteHoldMs { get => _sm.DeleteConfirmMs; set => _sm.DeleteConfirmMs = value; }
    /// <summary>Add picker keeps its armed entry at stick centre. The host turns it on while narration is on.</summary>
    public bool EditPickerLatchesAtCentre { get => _sm.PickerLatchesAtCentre; set => _sm.PickerLatchesAtCentre = value; }
    public WheelSlice[] EditCurrentSlices => _sm.CurrentSlices;
    /// <summary>Where a carried slice would land right now — narration's source for "position N", since
    /// <see cref="ArmedIndex"/> stays pinned to the origin during a carry. -1 when not carrying.</summary>
    public int EditCarrySlot => _sm.CarrySlot;

    /// <summary>Fired whenever the slice list structurally changes (move/delete/add/undo) — including
    /// the timer-driven hold-□ delete. The host persists EditCurrentSlices in response (live save).</summary>
    public event Action? EditStructureChanged;
    private int _lastStructVer;
    private void CheckStructure()
    {
        if (_sm.StructureVersion == _lastStructVer) return;
        _lastStructVer = _sm.StructureVersion;
        InvalidatePeerChildren();   // a slice was added/removed/reordered — the UIA tree is now stale
        EditStructureChanged?.Invoke();
    }

    public void BeginEdit(IReadOnlyList<WheelSlice> slices)
    { _sm.BeginEdit(slices); _lastStructVer = _sm.StructureVersion; InvalidatePeerChildren(); InvalidateVisual(); }
    public WheelSlice[] EndEdit() { var r = _sm.EndEdit(); InvalidatePeerChildren(); InvalidateVisual(); return r; }
    public void EditPickUp()        { _sm.PickUpArmed();    InvalidateVisual(); }
    public bool EditDrop()          { bool c = _sm.DropCarried();  CheckStructure(); InvalidateVisual(); return c; }
    public void EditCancelCarry()   { _sm.CancelCarry();   InvalidateVisual(); }
    public bool EditNudge(int dir)  { bool c = _sm.NudgeSelected(dir); CheckStructure(); InvalidateVisual(); return c; }
    public void EditSetDeleteHeld(bool held) => _sm.SetDeleteHeld(held);
    public int  EditAddAndCarry(WheelSlice s) { int i = _sm.AddSliceAndCarry(s); CheckStructure(); InvalidateVisual(); return i; }
    public bool EditUndo()          { bool c = _sm.Undo(); CheckStructure(); InvalidateVisual(); return c; }
    public bool EditRedo()          { bool c = _sm.Redo(); CheckStructure(); InvalidateVisual(); return c; }
    public void SetPicker(IReadOnlyList<WheelSlice> menu) { _sm.SetPicker(menu); InvalidatePeerChildren(); InvalidateVisual(); }
    public void EndPicker()         { _sm.EndPicker(); InvalidatePeerChildren(); InvalidateVisual(); }
    /// <summary>An edit animation (landing settle or reflow) is in flight — hold the wheel a beat
    /// before despawning so the placement is visible.</summary>
    public bool EditSettling => _sm.Settling;

    // ── UI Automation (accessibility A1) ──────────────────────────────────────
    // The wheel is drawing operations, so without a peer it has NO automation tree — one anonymous element.
    // This is for inspection tooling and tests, NOT for Narrator: the overlay window is never focused and
    // Windows won't voice it (see RadialMenuPeer's remarks).
    private RadialMenuPeer? _peer;

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        _peer ??= new RadialMenuPeer(this);

    /// <summary>Tell any listening UIA client the armed slice moved. Called from the confirm tick's
    /// armed-change edge rather than per frame — the tick runs at 30 Hz and the raise is only cheap when
    /// nothing is listening.</summary>
    private void NotifyPeerSelection() => _peer?.NotifySelectionChanged();

    /// <summary>The set of slices being DRAWN changed — the automation tree must be rebuilt or it reports
    /// the previous list (WPF caches children; see RadialMenuPeer.InvalidateChildren).</summary>
    private void InvalidatePeerChildren() => _peer?.InvalidateChildren();

    /// <summary>Fired when the user clicks directly on a slice (mouse testing mode).</summary>
    public event Action<int>? SliceClicked;

    /// <summary>Fired the moment a guarded slice's hold dwell COMPLETES — the transition into "fully armed
    /// / release to fire", and the point at which such a slice earns its arm cue. Raised from the dwell
    /// timer, which is the only place that transition happens (nothing on the input path observes it).</summary>
    public event Action? ConfirmReady;
    private bool _lastConfirmReady;

    // ── Reactor guarded-dwell "magnet lock" ───────────────────────────────────
    // The glyph is cut into radial sector pieces, pulled ReactorLockSepPx apart at dwell start, and drawn
    // back together with cubic ease-in — barely moving at first, accelerating like a magnetic snap. On
    // completion the whole glyph shakes briefly.
    private const double ReactorLockSepPx   = 10.0;
    private const double ReactorSnapShakeMs = 240.0;
    // How far around the centre the pieces start, before spiralling back to their own sectors.
    private const double ReactorSwirlDeg    = 55.0;
    private DateTime _reactorSnapUtc = DateTime.MinValue;
    // Five pieces: a unit separation direction per piece, a point-up pentagon starting at 12 o'clock.
    private const int ReactorLockPieces = 5;
    private static readonly (double dx, double dy)[] PieceDirs = BuildPieceDirs(ReactorLockPieces);
    private static (double dx, double dy)[] BuildPieceDirs(int count) =>
        [.. Enumerable.Range(0, count).Select(k =>
        {
            double a = (-90.0 + k * 360.0 / count) * Math.PI / 180.0;
            return (Math.Cos(a), Math.Sin(a));
        })];

    /// <summary>Sector wedge covering piece <paramref name="k"/> of <see cref="ReactorLockPieces"/>,
    /// cut from the rect's centre (the pieces carry their cut edges as they separate). Not cached:
    /// it's built once per piece per frame only while a reactor slice is mid-dwell.</summary>
    private static Geometry PieceClip(Rect r, int k)
    {
        var c = new Point(r.X + r.Width / 2.0, r.Y + r.Height / 2.0);
        double radius = Math.Sqrt(r.Width * r.Width + r.Height * r.Height);   // covers every corner
        double a0 = (-90.0 + (k - 0.5) * 360.0 / ReactorLockPieces) * Math.PI / 180.0;
        double a1 = (-90.0 + (k + 0.5) * 360.0 / ReactorLockPieces) * Math.PI / 180.0;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(c, isFilled: true, isClosed: true);
            const int segs = 6;   // chorded arc — generous radius keeps full coverage
            for (int s = 0; s <= segs; s++)
            {
                double a = a0 + (a1 - a0) * s / segs;
                ctx.LineTo(new Point(c.X + Math.Cos(a) * radius, c.Y + Math.Sin(a) * radius),
                           isStroked: false, isSmoothJoin: false);
            }
        }
        g.Freeze();
        return g;
    }

    /// <summary>Index of the slice that was just fired (drawn in its own fade layer), or -1.</summary>
    private int _firingIndex = -1;

    /// <summary>Mark the currently-armed slice as the one being fired, so the fade-out can hold
    /// it slightly longer than the rest of the wheel. Call before resetting the armed state.
    /// <paramref name="celebrate"/> false suppresses kawaii's confetti burst (e.g. the
    /// unconfigured-slice fire that jumps to Settings — not a success to celebrate).</summary>
    public void MarkFiringSlice(bool celebrate = true)
    {
        _firingIndex = _sm.StickyArmedIndex;
        if (celebrate) SpawnFireFx(_firingIndex);
    }
    public void ClearFiringSlice() { _firingIndex = -1; _fireFxSpawnedFor = -1; _sparkFxSpawnedFor = -1; }

    /// <summary>Route a fire celebration to whatever the current material celebrates with: kawaii's confetti
    /// burst, salvage's spark bloom, nothing elsewhere.</summary>
    private void SpawnFireFx(int index)
    {
        // Reduce Motion: fire celebrations are decorative particle travel — they don't spawn at all
        // (the arm/fire sounds and the selected-slice linger still confirm the fire). Not spawning also
        // keeps FireFxRemainingMs at 0, so OverlayWindow parks immediately instead of polling.
        if (_reduceMotion) return;
        if (_material == "kawaii") SpawnFireConfetti(index);
        else if (_material == "salvage") SpawnFireSparks(index);
    }

    /// <summary>Spawn the fire celebration for the armed slice WITHOUT touching the fade state — called
    /// before the action executes, so a slow action (app launch, HDR toggle, an HTTP call to the control
    /// service) can't sit between the fire sound and its reward. Idempotent: the later MarkFiringSlice on
    /// the same fire won't restart the burst.</summary>
    public void CelebrateFire() => SpawnFireFx(_sm.StickyArmedIndex);

    // ── Kawaii fire confetti: a one-shot burst of pastel hearts + stars flying off the fired slice,
    //    "power-up collected" style. Advanced by the existing 30ms confirm timer (no new render driver);
    //    drawn ON TOP of every layer at its own opacity, so it stays bright while the wheel fades. ──
    private readonly record struct FireFxParticle(
        double AngRad, double Dist, double RotDeg, double Scale, bool Heart, int ColorIx,
        Vector Offset, double FlutterPhase, double FlutterHz, double FlutterAmp);
    private readonly List<FireFxParticle> _fireFx = new();
    private Point    _fireFxOrigin;
    private DateTime _fireFxStartUtc;
    private int      _fireFxSpawnedFor = -1;   // slice index the current burst belongs to (idempotence guard)
    // Spawned but not yet PAINTED. The clock starts on the first frame that actually renders, not at spawn:
    // the fire path can block the UI thread (ActionExecutor runs synchronously), and starting the clock at
    // spawn time would burn that dead time — the burst would appear already half-flown once frames resume.
    private bool     _fireFxPending;
    // A backstop, not the intended ending: pieces end by falling off-screen, and with FireFxGravityPx the
    // last one clears a 1440px-tall screen inside ~1.2s, so the list normally empties itself well before
    // this and nothing is seen to vanish in mid-air.
    private const int FireFxDurMs = 2000;
    /// <summary>Reduce Motion: the confetti expires at 0.5s instead of flying its full arc. The motion is
    /// unchanged while it lives (t still runs over <see cref="FireFxDurMs"/> — compressing it would make the
    /// pieces faster); the list is simply cleared early.</summary>
    private const int FireFxReducedLifeMs = 500;
    private int FireFxLifeMs => _reduceMotion ? FireFxReducedLifeMs : FireFxDurMs;
    /// <summary>The roll behind every decorative particle (confetti, spark bloom, twinkle, circuit sparks).
    /// Reassigned only by the render-snapshot harness, to seed them.</summary>
    internal static Random FxRng = new();
    private static readonly Brush[] FireFxBrushes =
    {
        Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0xC3))),   // pink
        Frozen(new SolidColorBrush(Color.FromRgb(0x8F, 0xE8, 0xF0))),   // cyan
        Frozen(new SolidColorBrush(Color.FromRgb(0xC7, 0xA9, 0xF5))),   // lilac
        Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x7A))),   // butter
    };
    private static readonly Pen FireFxOutline = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 1.6);
    private static readonly Geometry FireFxHeart = BuildFxHeart(31.0);
    private static readonly Geometry FireFxStar  = BuildFxStar(new Point(0, 0), 31.0, 0);
    // Downward acceleration applied over a particle's life (px at t=1) — the confetti arcs and falls
    // instead of flying straight out, so the burst reads as a physical pop rather than a radial wipe.
    // Sized so every piece is PAST the bottom edge well inside FireFxDurMs (see that constant).
    private const double FireFxGravityPx = 3400.0;
    // Late-life gravity on top of the t² ramp: a multiplier growing from 1× (the early arc is unchanged) to
    // FireFxGravityLateMul× as a piece's age in real seconds (not the t fraction of the backstop)
    // approaches FireFxGravityRampSec, so the fall reads as gravity catching up rather than a uniform curve.
    private const double FireFxGravityRampSec = 1.2;
    private const double FireFxGravityLateMul = 2.75;
    /// <summary>Radius of the disc the pieces are thrown from — the same scatter salvage's sparks use, so the
    /// burst doesn't radiate from one pinprick.</summary>
    private const double FireFxOriginJitterPx = 18.0;
    /// <summary>Where the flutter fades in and is fully established (as burst time): once the outward throw
    /// has spent itself and the fall is taking over, each piece starts swaying and flipping like real paper
    /// instead of dropping on rails.</summary>
    private const double FireFxFlutterFromT = 0.28;
    private const double FireFxFlutterFullT = 0.52;

    /// <summary>Milliseconds of fire celebration still in flight (0 = none) — kawaii's confetti or salvage's
    /// spark bloom, whichever is running. OverlayWindow delays parking the window on a fire until this
    /// drains, so the burst isn't cut off by the ~200ms wheel fade.</summary>
    public int FireFxRemainingMs => Math.Max(
        _fireFx.Count == 0 ? 0
            : _fireFxPending ? FireFxLifeMs
            : Math.Max(0, FireFxLifeMs - (int)(NowUtc - _fireFxStartUtc).TotalMilliseconds),
        _sparkFx.Count == 0 ? 0
            : _sparkFxPending ? (int)SparkFxTotalMs
            : Math.Max(0, (int)SparkFxTotalMs - (int)(NowUtc - _sparkFxStartUtc).TotalMilliseconds));

    private void SpawnFireConfetti(int index)
    {
        if (_material != "kawaii" || index < 0 || index >= Slices.Count || !IsVisible) return;
        if (_fireFxSpawnedFor == index && _fireFx.Count > 0) return;   // this fire is already celebrating
        int n = Math.Max(1, Slices.Count);
        double midDeg = 360.0 / n * index - 90;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        _fireFxOrigin     = Polar(center, (_sliceInner + OuterRadius) / 2.0, midDeg);
        _fireFxPending    = true;    // clock starts on the first painted frame (see the field's remarks)
        _fireFxSpawnedFor = index;
        _fireFx.Clear();
        // An even spread around the full circle with a little jitter, not a cone biased outward.
        for (int k = 0; k < 11; k++)
            _fireFx.Add(new FireFxParticle(
                AngRad:  (k / 11.0) * Math.PI * 2 + (FxRng.NextDouble() - 0.5) * 0.5,
                // Thrown hard enough to clear the wheel before gravity takes the pieces.
                Dist:    190 + FxRng.NextDouble() * 240,
                RotDeg:  (FxRng.NextDouble() - 0.5) * 260,
                Scale:   0.6 + FxRng.NextDouble() * 0.7,
                Heart:   FxRng.NextDouble() < 0.5,
                ColorIx: k % FireFxBrushes.Length,
                // Scattered launch points (as salvage's sparks do), and each piece's own flutter — phase so
                // they don't sway in unison, rate and width so some drift lazily and others skitter.
                Offset:       JitterInDisc(FireFxOriginJitterPx),
                FlutterPhase: FxRng.NextDouble() * Math.PI * 2,
                FlutterHz:    1.6 + FxRng.NextDouble() * 2.4,
                FlutterAmp:   14 + FxRng.NextDouble() * 26));
        InvalidateVisual();
    }

    private void DrawFireConfetti(DrawingContext dc)
    {
        if (_fireFxPending) { _fireFxStartUtc = NowUtc; _fireFxPending = false; }   // first painted frame = t0
        double elapsedMs = (NowUtc - _fireFxStartUtc).TotalMilliseconds;
        double t = elapsedMs / FireFxDurMs;
        // Lifetime backstop — usually every piece left earlier. Under Reduce Motion the life is cut to
        // 0.5s (FireFxLifeMs) while the motion clock (t over FireFxDurMs) is untouched.
        if (t >= 1.0 || elapsedMs >= FireFxLifeMs) { _fireFx.Clear(); return; }
        double ease    = 1 - (1 - t) * (1 - t);                                  // position ease-out
        // Pop in to full size and stay there: the pieces end by falling off the bottom, and off-screen
        // pieces are dropped from the list so nothing accumulates.
        double animSc  = t < 0.35 ? 0.4 + (1.05 - 0.4) * (t / 0.35) : 1.05;
        double bottom  = OffScreenBottom();   // fully past the WINDOW's edge before removal
        for (int idx = _fireFx.Count - 1; idx >= 0; idx--)
        {
            var p = _fireFx[idx];
            // Flutter ramps in as the throw spends itself, so the pop stays a clean radial burst and only
            // the FALL is papery.
            double flut = Math.Clamp((t - FireFxFlutterFromT) / (FireFxFlutterFullT - FireFxFlutterFromT), 0, 1);
            double wave = Math.Sin(p.FlutterPhase + t * p.FlutterHz * Math.PI * 2);

            double x = _fireFxOrigin.X + p.Offset.X + Math.Cos(p.AngRad) * p.Dist * ease * 1.25
                     + wave * p.FlutterAmp * flut;                 // side-to-side drift
            // …plus gravity: t² so the pull ramps up over the particle's life, times the late-life
            // multiplier (1× while ageSec is small, so the launch is untouched).
            double ageSec = t * (FireFxDurMs / 1000.0);
            double gravityRamp = Math.Min(1.0, ageSec / FireFxGravityRampSec);
            double gravityMul = 1.0 + (FireFxGravityLateMul - 1.0) * gravityRamp;
            double y = _fireFxOrigin.Y + p.Offset.Y + Math.Sin(p.AngRad) * p.Dist * ease * 1.25
                     + FireFxGravityPx * t * t * gravityMul;
            if (y > bottom) { _fireFx.RemoveAt(idx); continue; }   // fell off — remove, don't accumulate
            dc.PushTransform(new TranslateTransform(x, y));
            // Rock about the sway's own phase (a quarter-turn behind it, the way a falling leaf tips into
            // its drift) on top of the spin the pop imparted.
            dc.PushTransform(new RotateTransform(
                p.RotDeg * ease * 1.3 + Math.Cos(p.FlutterPhase + t * p.FlutterHz * Math.PI * 2) * 26 * flut));
            double k = p.Scale * animSc;
            // Squash the width on the same wave: the piece reads as turning edge-on and back, which is what
            // sells it as paper rather than a sprite sliding sideways. Never fully flat (0.22 floor) so it
            // can't disappear for a frame.
            double flipX = 1 - flut * (1 - Math.Max(0.22, Math.Abs(wave)));
            dc.PushTransform(new ScaleTransform(k * flipX, k));
            dc.DrawGeometry(FireFxBrushes[p.ColorIx], FireFxOutline, p.Heart ? FireFxHeart : FireFxStar);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    // ── Salvage fire sparks: a grinder-strike bloom off the fired slice's glyph. Same one-shot lifecycle
    //    as the kawaii confetti — spawned on fire, clocked from the first painted frame, drawn above every
    //    layer at its own opacity — but far shorter: a handful of streaks that arc and wink out.
    private readonly record struct SparkFxParticle(
        double AngRad, double Dist, double LenFrac, double Width, double Life,
        Vector Offset, double StartMs, bool Tinted);
    private readonly List<SparkFxParticle> _sparkFx = new();
    private Point    _sparkFxOrigin;
    private Color    _sparkFxTint;      // the fired slice's own glyph colour (half the pack burns in it)
    private Color    _sparkFxTintHot;   // …lightened, for those sparks' leading tip
    private DateTime _sparkFxStartUtc;
    private int      _sparkFxSpawnedFor = -1;
    private bool     _sparkFxPending;
    /// <summary>Whole bloom, first strike to last ember. Short by design — this is a strike, not a shower —
    /// but long enough that the eye catches it over the ~200ms wheel fade.
    /// <para>Backstop for the WHOLE bloom — the longest a single spark can live. Individual sparks take a
    /// random slice of this (see SparkFxLifeSpread), so the bloom thins out over time instead of ending all
    /// at once; the short ones are gone in a few hundred ms.</para></summary>
    private const int SparkFxDurMs = 620;
    /// <summary>Longest-to-shortest lifetime ratio across the pack; the short end of the range holds at
    /// ~170-310ms.</summary>
    private const double SparkFxLifeSpread = 2.0;
    /// <summary>Chance a downward-launched spark is flipped upward instead — the bloom should throw up and
    /// rain back, not spray at the floor.</summary>
    private const double SparkFxUpBias = 0.85;
    private const int SparkFxCount = 5;   // a handful of legible arcs, not a starburst
    /// <summary>Radius of the disc the launch points are scattered over, roughly the glyph's own footprint.
    /// Sparks struck from ONE exact point stay on a perfect expanding circle however they're timed — the
    /// scatter is what stops the bloom reading as a starburst radiating from a pinprick.</summary>
    private const double SparkFxOriginJitterPx = 17.0;
    /// <summary>Longest a spark's launch can be held back. Deliberately small: enough to break the pack off
    /// a shared radius, not so much that the strike stops reading as one impact.</summary>
    private const double SparkFxStaggerMs = 55.0;
    /// <summary>Whole-bloom backstop: the longest life plus the longest launch delay.</summary>
    private const double SparkFxTotalMs = SparkFxDurMs + SparkFxStaggerMs;
    /// <summary>Gravity in px/s². Heavy enough that a streak visibly ARCHES over rather than flying straight
    /// out. Flight is timed in SECONDS, not in fractions of the bloom: lifetimes now vary 4× across the pack,
    /// and tying the throw to either clock would have long-lived sparks crawl and short ones teleport.</summary>
    private const double SparkFxGravityPxPerSec2 = 1450.0;
    /// <summary>Seconds the outward throw takes to spend itself; after this a spark is falling ballistically.</summary>
    private const double SparkFxThrowSec = 0.62;
    /// <summary>Fraction of a spark's life spent fading IN, after an invisible beat of whichever is LONGER:
    /// a quarter of that spark's life, or SparkFxFadeDelayMs. Nothing is drawn until the pack has spread —
    /// streaks appearing while still bunched at the glyph read as one starburst from a single point, which
    /// is the opposite of the intent.</summary>
    private const double SparkFxFadeInFrac = 0.14;
    private const double SparkFxFadeDelayMs = 100.0;
    private const double SparkFxFadeDelayFrac = 0.25;
    /// <summary>How far behind the head the tail samples the spark's own path, as a fraction of its life.
    /// Sampling the PATH (not the launch direction) is what bends each streak along its arc.</summary>
    private const double SparkFxTailLagFrac = 0.13;
    private static readonly Color SparkFxHot = Color.FromRgb(0xFF, 0xFF, 0xFF);   // white throughout

    private void SpawnFireSparks(int index)
    {
        if (_material != "salvage" || index < 0 || index >= Slices.Count || !IsVisible) return;
        if (_sparkFxSpawnedFor == index && _sparkFx.Count > 0) return;   // this fire is already sparking
        int n = Math.Max(1, Slices.Count);
        double midDeg = 360.0 / n * index - 90;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        _sparkFxOrigin      = Polar(center, (_sliceInner + OuterRadius) / 2.0, midDeg);
        _sparkFxPending     = true;   // clock starts on the first painted frame (see _fireFxPending's remarks)
        _sparkFxSpawnedFor  = index;
        // Half the pack burns in the slice's own glyph colour on this material, lightened for the leading tip.
        _sparkFxTint    = ActionTint.DisplayColor(Slices[index], ActionTint.TintSetFor(_material));
        _sparkFxTintHot = ActionTint.Lighten(_sparkFxTint, 0.55);
        _sparkFx.Clear();
        // An even fan around the full circle with heavy per-spark jitter in reach and lifetime, so the bloom
        // frays instead of expanding as one clean ring.
        for (int k = 0; k < SparkFxCount; k++)
        {
            // Even fan + jitter, then bias UPWARD: a downward-pointing spark is flipped over the horizontal
            // most of the time, so the bloom throws up and rains back rather than spraying at the floor.
            // (Screen space — negative Y is up.)
            double ang = (k / (double)SparkFxCount) * Math.PI * 2 + (FxRng.NextDouble() - 0.5) * 0.9;
            if (Math.Sin(ang) > 0 && FxRng.NextDouble() < SparkFxUpBias) ang = -ang;
            _sparkFx.Add(new SparkFxParticle(
                AngRad:  ang,
                // Squaring the roll weights the pack toward short, lazy sparks with the occasional one flung
                // right across the wheel.
                Dist:    26 + Math.Pow(FxRng.NextDouble(), 2) * 330,
                LenFrac: 0.16 + FxRng.NextDouble() * 0.20,   // scales how far back the tail samples the path
                Width:   1.1 + FxRng.NextDouble() * 1.7,
                // Lifetime = the base span, stretched by a random 1×…SparkFxLifeSpread — expressed against
                // the backstop, so a 1× spark burns out in a few hundred ms while the longest rides it out.
                // The stretch roll is cubed, which pins most of the pack near 1× and leaves only a spark or
                // two burning near the top of the range.
                Life:    (0.55 + FxRng.NextDouble() * 0.45)
                         * (1 + Math.Pow(FxRng.NextDouble(), 3) * (SparkFxLifeSpread - 1))
                         / SparkFxLifeSpread,
                // Each spark is struck from its own point on a small disc over the glyph, and a few ms apart,
                // so the pack never shares one radius — the two things that made it read as a starburst.
                Offset:  JitterInDisc(SparkFxOriginJitterPx),
                StartMs: FxRng.NextDouble() * SparkFxStaggerMs,
                Tinted:  k % 2 == 0));
        }
        InvalidateVisual();
    }

    private void DrawFireSparks(DrawingContext dc)
    {
        if (_sparkFxPending) { _sparkFxStartUtc = NowUtc; _sparkFxPending = false; }   // first painted frame = t0
        double bloomMs = (NowUtc - _sparkFxStartUtc).TotalMilliseconds;
        if (bloomMs >= SparkFxTotalMs) { _sparkFx.Clear(); return; }

        for (int idx = _sparkFx.Count - 1; idx >= 0; idx--)
        {
            var p = _sparkFx[idx];
            double ms = bloomMs - p.StartMs;                           // age since THIS spark was struck
            if (ms < 0) continue;                                      // not launched yet
            double lifeMs = p.Life * SparkFxDurMs;
            double lt = ms / lifeMs;                                   // this spark's OWN normalized age
            if (lt >= 1.0) { _sparkFx.RemoveAt(idx); continue; }       // burnt out early

            // Head, and a tail sampled from the spark's OWN PATH a moment earlier — so the streak lies along
            // the direction it is actually travelling and bends over as gravity turns the flight downward.
            double sec = ms / 1000.0;
            var head = SparkAt(p, sec);
            var tail = SparkAt(p, Math.Max(0, sec - SparkFxThrowSec * SparkFxTailLagFrac * (0.6 + p.LenFrac * 2)));
            if ((head - tail).LengthSquared < 0.5) continue;           // not moving yet — nothing to draw

            var body = p.Tinted ? _sparkFxTint    : SparkFxHot;
            var tip  = p.Tinted ? _sparkFxTintHot : SparkFxHot;
            var g = new LinearGradientBrush { StartPoint = tail, EndPoint = head, MappingMode = BrushMappingMode.Absolute };
            g.GradientStops.Add(new GradientStop(Color.FromArgb(0, body.R, body.G, body.B), 0.0));
            g.GradientStops.Add(new GradientStop(Color.FromArgb(190, body.R, body.G, body.B), 0.55));
            g.GradientStops.Add(new GradientStop(tip, 1.0));
            g.Freeze();

            // Fade IN over the opening fraction (so the pack doesn't flash as one blob while still bunched at
            // the glyph), then out on the spark's own clock — squared, so it holds bright then drops away.
            double delayMs = Math.Max(SparkFxFadeDelayMs, lifeMs * SparkFxFadeDelayFrac);
            double fadeIn = Math.Clamp((ms - delayMs) / (SparkFxFadeInFrac * lifeMs), 0, 1);
            if (fadeIn <= 0) continue;   // still in its invisible opening beat
            dc.PushOpacity(fadeIn * Math.Clamp(1 - lt * lt, 0, 1));
            dc.DrawLine(new Pen(g, p.Width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                        tail, head);
            dc.Pop();
        }
    }

    /// <summary>A spark's position <paramref name="sec"/> seconds after the strike: an outward throw that
    /// spends itself over SparkFxThrowSec, plus an ever-growing fall. Evaluating the same curve at two times
    /// is what gives the streak its arc.</summary>
    private Point SparkAt(SparkFxParticle p, double sec)
    {
        double th   = Math.Min(1.0, sec / SparkFxThrowSec);
        double ease = 1 - (1 - th) * (1 - th);   // fast out of the gate, decaying — a strike, not a launch
        return new Point(
            _sparkFxOrigin.X + p.Offset.X + Math.Cos(p.AngRad) * p.Dist * ease,
            _sparkFxOrigin.Y + p.Offset.Y + Math.Sin(p.AngRad) * p.Dist * ease
                + SparkFxGravityPxPerSec2 * sec * sec);
    }

    /// <summary>A uniformly-distributed random point inside a disc of radius <paramref name="r"/> (√ on the
    /// radius roll — a plain roll would bunch the picks toward the centre).</summary>
    private static Vector JitterInDisc(double r)
    {
        double a = FxRng.NextDouble() * Math.PI * 2;
        double d = Math.Sqrt(FxRng.NextDouble()) * r;
        return new Vector(Math.Cos(a) * d, Math.Sin(a) * d);
    }

    /// <summary>Y (in this control's coordinates) a falling piece must pass to be off the bottom of the
    /// screen. The control is a ~430px box on the overlay's canvas, not the full-screen surface, so its own
    /// ActualHeight would put the despawn line at the bottom of the wheel. Falls back to the control box only
    /// if the window isn't reachable.</summary>
    private double OffScreenBottom()
    {
        try
        {
            if (Window.GetWindow(this) is { } w && w.ActualHeight > 0)
                return w.ActualHeight - TransformToAncestor(w).Transform(new Point(0, 0)).Y + 60;
        }
        catch (InvalidOperationException) { }   // not in the window's visual tree (yet)
        return ActualHeight + 40;
    }

    /// <summary>Unit confetti heart (centred on the origin, ~2s tall) — two mirrored cubic lobes.</summary>
    private static Geometry BuildFxHeart(double s)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, s * 0.7), isFilled: true, isClosed: true);
            ctx.BezierTo(new Point(-s * 1.4, -s * 0.4), new Point(-s * 0.5, -s * 1.3), new Point(0, -s * 0.45), true, false);
            ctx.BezierTo(new Point( s * 0.5, -s * 1.3), new Point( s * 1.4, -s * 0.4), new Point(0,  s * 0.7), true, false);
        }
        g.Freeze();
        return g;
    }

    /// <summary>Four-point cartoon star at <paramref name="c"/> (outer radius <paramref name="r"/>) —
    /// the confetti star and the hub's ambient twinkle.</summary>
    private static Geometry BuildFxStar(Point c, double r, double rotDeg)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (int k = 0; k < 8; k++)
            {
                double rr = k % 2 == 0 ? r : r * 0.36;
                double a  = (rotDeg - 90 + k * 45) * Math.PI / 180.0;
                var pt = new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr);
                if (k == 0) ctx.BeginFigure(pt, isFilled: true, isClosed: true);
                else        ctx.LineTo(pt, true, false);
            }
        }
        g.Freeze();
        return g;
    }

    // ── Kawaii ambient twinkle: a pool of small stars around the cloud hub's rim, each on its own
    //    staggered cycle so something is always sparkling while a wheel is up. The cycle clock resets when
    //    the wheel goes live (SetWheelLive), not on IsVisible, which stays true while the wheel is parked
    //    at zero opacity. ──
    private sealed class AmbientTwinkle
    {
        public double Ang, Size, Phase;
        public int    Cycle = -1;   // re-rolls position/size on each new cycle
    }
    private readonly AmbientTwinkle[] _twinkles = new AmbientTwinkle[5];
    private DateTime _twinkleT0;
    private bool     _wheelLive;
    private const double TwinklePeriodSec = 1.5;   // per-star cycle; 5 stars staggered → ~2 lit at any moment
    private static readonly Brush TwinkleBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 255, 244, 205)));
    private static readonly Brush TwinkleCoreBrush = Frozen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)));
    // Stars are a pale warm white and vanished against a light hub/toast — outline them in the SAME rim
    // colour the hub and toast edges use. Round joins: a star's points would spike under a miter.
    private static readonly Pen TwinkleEdgePen = FrozenJoinPen(new SolidColorBrush(Color.FromArgb(255, 232, 213, 238)), 2.0);

    /// <summary>Told by OverlayWindow when the wheel is actually on screen (vs parked at zero opacity —
    /// <see cref="UIElement.IsVisible"/> can't tell the difference). Gates the ambient-twinkle frames and
    /// restarts its cycle clock, so a kawaii wheel always opens into a live twinkle.</summary>
    public void SetWheelLive(bool live)
    {
        if (_wheelLive == live) return;
        _wheelLive = live;
        if (!live) return;
        // ConfirmTick parks the timer while the wheel is off screen — a live wheel needs it back.
        if (IsVisible && !_confirmTimer.IsEnabled) _confirmTimer.Start();
        // Hub presence starts fresh each open: the first rendered frame SEEDS visibility without animating
        // (a hub already on screen at open rides the wheel's own bloom-in, not its own pop), and no stale
        // snapshot from the last session can flash on frame one.
        _hubFirstFrame = true;
        _hubAnimUtc    = DateTime.MinValue;
        _hubScale      = 1.0;
        _hubSnapshot   = null;
        _twinkleT0 = NowUtc;
        for (int k = 0; k < _twinkles.Length; k++)
            _twinkles[k] = new AmbientTwinkle { Phase = k / (double)_twinkles.Length };
    }

    // ── Hub appear / disappear animation ──────────────────────────────────────
    // The hub scales between 0 and full size at the wheel centre whenever its reason to exist changes — a
    // toggle slice arming a state readout, edit mode, the volume scrubber, reactor's armed caption. Plain
    // cubic ease-out both ways, no overshoot. HubPresence is the one place the curve and duration live.
    private DateTime _hubAnimUtc = DateTime.MinValue;   // MinValue = settled, no transition running
    private bool   _hubAnimIn;        // direction of the running transition
    private double _hubAnimFrom;      // scale it started from (mid-flight reversals don't jump)
    private double _hubScale = 1.0;   // last computed scale
    // This frame's presence, computed once at the top of RenderCore and consumed by DrawCenter (and by
    // terra's backdrop, which sizes its centre hole from the same scale).
    private double _hubFrameScale = 1.0;
    private bool   _hubFrameDraw;
    private bool   _hubShownLastFrame;
    private bool   _hubFirstFrame = true;
    private DrawingGroup? _hubSnapshot;   // last captured hub, replayed while it shrinks away
    private const double HubAppearMs    = 170.0;
    private const double HubDisappearMs = 85.0;   // exit at 2× the intro's speed

    /// <summary>Advance the hub's presence animation and report this frame's scale plus whether to draw at
    /// all. Call exactly once per render (RenderCore does) — it advances the transition state.
    /// <paramref name="demanded"/> is whether the hub has a reason to exist right now; `draw` stays true
    /// through a shrink-out after that reason is gone.</summary>
    private (double scale, bool draw) HubPresence(bool demanded)
    {
        // Reduce Motion: the hub appears/disappears as an instant state change — no scale pop either way.
        // The scale still reports 0 with no hub: it is not only the hub's own size but also what terra's
        // backdrop sizes its centre hole from, and a hidden hub must leave that hole fully open.
        if (_reduceMotion)
        {
            _hubFirstFrame = false;
            _hubShownLastFrame = demanded;
            _hubAnimUtc = DateTime.MinValue;
            _hubScale = demanded ? 1.0 : 0.0;
            return (_hubScale, demanded);
        }

        // First frame of a freshly-opened wheel SEEDS the state: a hub that's already there rides the
        // wheel's own bloom-in rather than playing its own pop.
        // The seed MUST follow `demanded`, exactly as the Reduce Motion branch does. The scale is not only
        // the hub's own size — Mesa's backdrop sizes its centre hole from it — so seeding 1.0 with no hub
        // closed that hole to half for frame one. Nothing re-renders during the intro (it animates Opacity
        // and RenderTransform, not content), so that wrong hole stayed on screen until the next
        // InvalidateVisual, then snapped open.
        if (_hubFirstFrame)
        {
            _hubFirstFrame = false;
            _hubShownLastFrame = demanded;
            _hubAnimUtc = DateTime.MinValue;
            _hubScale = demanded ? 1.0 : 0.0;
            return (_hubScale, demanded);
        }

        if (demanded != _hubShownLastFrame)
        {
            _hubShownLastFrame = demanded;
            _hubAnimIn  = demanded;
            _hubAnimFrom = _hubScale;   // reversing mid-flight resumes from where it got to
            _hubAnimUtc  = NowUtc;
        }

        if (_hubAnimUtc == DateTime.MinValue)          // settled
        {
            _hubScale = demanded ? 1.0 : 0.0;
            return (_hubScale, demanded);
        }

        double target = _hubAnimIn ? 1.0 : 0.0;
        double t = (NowUtc - _hubAnimUtc).TotalMilliseconds / (_hubAnimIn ? HubAppearMs : HubDisappearMs);
        if (t >= 1.0)
        {
            _hubAnimUtc = DateTime.MinValue;
            _hubScale = target;
            return (_hubScale, demanded);
        }
        _hubScale = _hubAnimFrom + (target - _hubAnimFrom) * EaseOut(Math.Max(0.0, t));
        return (_hubScale, true);   // mid-transition: keep drawing, shrink-out included
    }

    private void TickKawaiiFx()
    {
        bool invalidate = false;
        if (_fireFx.Count > 0)
        {
            invalidate = true;   // confetti in flight — keep frames coming (list self-clears on expiry)
            if (!_fireFxPending && (NowUtc - _fireFxStartUtc).TotalMilliseconds >= FireFxDurMs)
                _fireFx.Clear();
        }
        if (_sparkFx.Count > 0)
        {
            invalidate = true;   // salvage spark bloom in flight — same deal
            if (!_sparkFxPending && (NowUtc - _sparkFxStartUtc).TotalMilliseconds >= SparkFxTotalMs)
                _sparkFx.Clear();
        }
        // Ambient twinkle needs a frame every tick — but ONLY while the wheel is genuinely on screen
        // (see SetWheelLive; gating on IsVisible would invalidate ~33×/s forever, parked or not).
        // Reduce Motion: ambient loops don't run OR invalidate — the tick goes quiet, not invisible.
        if (_material == "kawaii" && _wheelLive && !_reduceMotion) invalidate = true;
        // Reactor circuit sparks animate while a slice is armed (same live-gate: no frames while parked).
        if (_material == "reactor" && _wheelLive && !_reduceMotion && _sm.ArmedIndex >= 0 && _circuit is not null)
        {
            TickCircuitSparks();
            invalidate = true;
        }
        // The hub's appear/disappear scale needs frames on EVERY material for its ~170ms (HubPresence).
        if (_wheelLive && _hubAnimUtc != DateTime.MinValue
            && (NowUtc - _hubAnimUtc).TotalMilliseconds < HubAppearMs + 40)
            invalidate = true;
        if (invalidate) InvalidateVisual();
    }

    private void DrawHubTwinkle(DrawingContext dc, Point center, double hubR)
    {
        if (!_wheelLive || _reduceMotion) return;   // Reduce Motion: the ambient loop stops entirely
        double elapsed = (NowUtc - _twinkleT0).TotalSeconds;
        foreach (var s in _twinkles)
        {
            if (s is null) continue;
            double x   = elapsed / TwinklePeriodSec + s.Phase;
            int    cyc = (int)Math.Floor(x);
            double u   = x - cyc;
            if (cyc != s.Cycle)   // new cycle → re-roll where this star appears (done even while dark)
            {
                s.Cycle = cyc;
                s.Ang   = FxRng.NextDouble() * 360.0;
                s.Size  = 4.5 + FxRng.NextDouble() * 4.0;
            }
            const double lit = 0.55;                  // fraction of the cycle the star is visible
            if (u > lit) continue;
            // Asymmetric: the rise takes a QUARTER of the lit window (it was half — a symmetric sine), so
            // the star snaps alight and then eases away. sin/cos quarter-waves meet smoothly at the peak.
            const double rise = lit * 0.25;
            double a = u < rise ? Math.Sin(u / rise * Math.PI / 2)
                                : Math.Cos((u - rise) / (lit - rise) * Math.PI / 2);
            // Ride the cloud's rim, alternating just inside / just outside so they read as ON the puff.
            double rad = hubR * (s.Cycle % 2 == 0 ? 0.90 : 1.03);
            var p = Polar(center, rad, s.Ang);
            dc.PushOpacity(a * 0.95);
            double sz = s.Size * (0.55 + 0.45 * a);              // pops as it brightens
            dc.DrawGeometry(TwinkleBrush, TwinkleEdgePen, BuildFxStar(p, sz, u * 50));   // slow rotate as it glints
            dc.DrawGeometry(TwinkleCoreBrush, null, BuildFxStar(p, sz * 0.45, u * 50));   // white hot core
            dc.Pop();
        }
    }

    /// <summary>Opacity of the outer ring (backglow + slices). Animated to fade the wheel out
    /// on selection, independently of the centre.</summary>
    public static readonly DependencyProperty RingOpacityProperty =
        DependencyProperty.Register(nameof(RingOpacity), typeof(double), typeof(RadialMenuControl),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double RingOpacity
    {
        get => (double)GetValue(RingOpacityProperty);
        set => SetValue(RingOpacityProperty, value);
    }

    /// <summary>Opacity of the centre hub (disc + status readout). Can linger after
    /// the ring has faded when a toggle readout is showing.</summary>
    public static readonly DependencyProperty CenterOpacityProperty =
        DependencyProperty.Register(nameof(CenterOpacity), typeof(double), typeof(RadialMenuControl),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double CenterOpacity
    {
        get => (double)GetValue(CenterOpacityProperty);
        set => SetValue(CenterOpacityProperty, value);
    }

    /// <summary>Opacity of the just-selected slice during a fade-out, so it can linger slightly
    /// longer than the rest of the ring. Only used while <see cref="_firingIndex"/> is set.</summary>
    public static readonly DependencyProperty SelectedSliceOpacityProperty =
        DependencyProperty.Register(nameof(SelectedSliceOpacity), typeof(double), typeof(RadialMenuControl),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double SelectedSliceOpacity
    {
        get => (double)GetValue(SelectedSliceOpacityProperty);
        set => SetValue(SelectedSliceOpacityProperty, value);
    }

    /// <summary>True while a toggle-state readout is shown in the centre.</summary>
    public bool StatusActive => _statusLine2 is not null;

    /// <summary>Show a two-line toggle-state readout (label + new state) in the centre deadzone.
    /// <paramref name="icon"/> (optional, post-fire readouts only) rides above the label — used by the
    /// storefront cold-start hub to show whose logo is being waited on.</summary>
    public void ShowStatus(string line1, string line2, string? line2Next = null, ImageSource? icon = null,
                           string? caption = null)
    {
        _centerNotice = null;            // a real state readout replaces any "configure" notice
        _noticeKind = NoticeKind.Plain;
        _statusLine1 = line1;
        _statusLine2 = line2;
        _statusLine2Next = line2Next;
        _statusIcon = icon;
        _statusCaption = caption;
        // A post-fire readout (no `next` target) is a hold, not a live transition: the hub has to be legible
        // for the whole linger. Snap it fully present instead of playing the appear — that animation is
        // advanced by render frames, and on a hub-hiding material it would start from scale 0, so frames the
        // fire path swallows would leave it stuck near-invisible. Armed previews keep the animation.
        if (line2Next is null) SnapHubPresent();
        InvalidateVisual();
    }

    /// <summary>Force the hub to fully-present, cancelling any in-flight appear/disappear. Used by post-fire
    /// readouts, whose legibility must not depend on animation frames surviving the fire path.</summary>
    private void SnapHubPresent()
    {
        _hubFirstFrame     = false;
        _hubShownLastFrame = true;
        _hubAnimUtc        = DateTime.MinValue;   // settled
        _hubScale          = 1.0;
        _hubFrameScale     = 1.0;
        _hubFrameDraw      = true;
    }

    /// <summary>Show a centred hint in the hub (e.g. "Configure this slice in Settings") for an armed
    /// slice that's missing required setup. Cleared like a status readout.</summary>
    public void ShowNotice(string text, NoticeKind kind = NoticeKind.Plain)
    {
        if (_centerNotice == text && _noticeKind == kind && _statusLine2 is null) return;
        _noticeKind = kind;
        _centerNotice = text;
        _statusLine1 = _statusLine2 = _statusLine2Next = _statusCaption = null;
        _statusIcon = null;
        InvalidateVisual();
    }

    public void ClearStatus()
    {
        if (_statusLine1 is null && _statusLine2 is null && _centerNotice is null) return;
        _statusLine1 = _statusLine2 = _statusLine2Next = _statusCaption = _centerNotice = null;
        _noticeKind = NoticeKind.Plain;
        _statusIcon = null;
        InvalidateVisual();
    }

    /// <summary>Update the centre battery readout (light-grey Xbox battery glyph). percent &lt; 0 hides it.
    /// The glyph is resolved once here (not per frame) since battery changes rarely.</summary>
    public void SetBattery(int percent, bool charging)
    {
        _batteryPercent  = percent;
        _batteryCharging = charging;
        ResolveBatteryIcon();
        InvalidateVisual();
    }

    /// <summary>Re-bake the battery glyph in the current material's ink. Called on a battery change AND
    /// on a material change — the glyph is a pre-tinted bitmap, so switching material without this would
    /// leave the previous material's colour on screen until the battery percentage next moved.</summary>
    private void ResolveBatteryIcon()
    {
        string glyph = BatteryGlyph(_batteryPercent, _batteryCharging);
        _batteryIcon = _batteryPercent < 0 ? null : PackIconHelper.FromName(glyph, BatteryInkFor());
        // Fires only on a battery/material change, so it's once-per-state-change, not per frame. "no
        // battery glyph in the hub" is otherwise undiagnosable: percent<0 (the pad never reported),
        // icon=NULL (the glyph name didn't resolve) and a drawn-but-invisible glyph look identical.
        TraceBattery(glyph);
    }

    private string? _lastBatteryTrace;
    private void TraceBattery(string glyph)
    {
        // Change-gated: the resolve runs on every battery AND material change, and the preview controls
        // resolve too, so an ungated line lands several times per material cycle. One line per genuine
        // state change per control, per docs/OVERLAY.md ▸ Diagnostics.
        string line = $"[Battery] pct={_batteryPercent} charging={_batteryCharging} mat={_material} " +
                      $"glyph={glyph} icon={(_batteryIcon is null ? "NULL" : "ok")}";
        if (line == _lastBatteryTrace) return;
        _lastBatteryTrace = line;
        System.Diagnostics.Trace.WriteLine(line);
    }

    private static string BatteryGlyph(int pct, bool charging) =>
        charging    ? "MicrosoftXboxControllerBatteryCharging"
        : pct <= 10 ? "MicrosoftXboxControllerBatteryAlert"
        : pct <= 35 ? "MicrosoftXboxControllerBatteryLow"
        : pct <= 70 ? "MicrosoftXboxControllerBatteryMedium"
        :             "MicrosoftXboxControllerBatteryFull";

    public void Reset()
    {
        _sm.Reset();
        ScrubberActive = false;
        HubGlyph = null;
        InvalidateVisual();
    }

    // ── Render timer → drives the dwell/delete state machine ────────────────────
    private void ConfirmTick(object? sender, EventArgs e)
    {
        var r = _sm.Tick(IsVisible);
        if (r.Invalidate)  InvalidateVisual();
        if (_sm.EditMode)  CheckStructure();   // catch the timer-driven hold-□ delete
        // Edge-detect the dwell completing. Falls back to false whenever the slice disarms, so re-arming
        // the same slice cues again.
        bool ready = _sm.ArmedConfirmReady;
        if (ready != _lastConfirmReady)
        {
            _lastConfirmReady = ready;
            if (ready)
            {
                ConfirmReady?.Invoke();
                _reactorSnapUtc = NowUtc;   // reactor: the quarters just snapped — start the shake
            }
        }
        TickKawaiiFx();                       // kawaii confetti / hub twinkle (no-ops on other materials)
        // UIA: raise a selection-changed event on the armed-index EDGE (not per frame). Nothing usually
        // listens, and the raise is a no-op then — but an inspection tool attached mid-session sees the
        // selection track the stick, which is what makes the peer verifiable at all.
        if (_sm.ArmedIndex != _lastPeerArmed)
        {
            _lastPeerArmed = _sm.ArmedIndex;
            NotifyPeerSelection();
        }
        // Park the timer when nothing needs frames: wheel not on screen, no fx draining, no hub
        // transition, and the state machine didn't ask for a repaint. SetWheelLive(true) restarts it.
        // Fx spawned while live keep it alive through the wheel's fade-out (the conditions above ran
        // this tick), so confetti never freezes mid-flight.
        if (!_wheelLive && !_sm.EditMode && !r.Invalidate
            && _fireFx.Count == 0 && _sparkFx.Count == 0
            && _hubAnimUtc == DateTime.MinValue)
            _confirmTimer.Stop();
    }
    private int _lastPeerArmed = -1;

    // Last stick value that actually produced a repaint (NaN = force the first one through).
    private float _drawnStickX = float.NaN, _drawnStickY = float.NaN;
    private int   _drawnStickArmed = -1;

    public void UpdateStick(float x, float y)
    {
        _sm.UpdateStick(x, y);
        // Reports arrive at the pad's rate (~125-250Hz); at rest consecutive values differ by sensor
        // noise only, and material animation frames come from the 30ms tick, not from here. Skip the
        // repaint when the stick is effectively where it was last DRAWN (not last reported — slow drift
        // still accumulates into a repaint) and the armed slice didn't change. Edit mode is exempt:
        // its focus/placement logic reads the stick in ways this gate can't see.
        const float eps = 0.004f;
        if (!_sm.EditMode
            && _sm.ArmedIndex == _drawnStickArmed
            && Math.Abs(x - _drawnStickX) < eps && Math.Abs(y - _drawnStickY) < eps)
            return;
        _drawnStickX = x; _drawnStickY = y; _drawnStickArmed = _sm.ArmedIndex;
        InvalidateVisual();
    }

    // ── Mouse click (testing) ─────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Slices.Count == 0) return;

        var    pt   = e.GetPosition(this);
        double cx   = ActualWidth / 2, cy = ActualHeight / 2;
        double dx   = pt.X - cx,       dy = pt.Y - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist < _sliceInner || dist > OuterRadius) return;

        int idx = _sm.IndexAtAngle(dx, dy);
        if (idx < 0) return;

        SliceClicked?.Invoke(idx);
        e.Handled = true;
    }

    // ── Animation clock ───────────────────────────────────────────────────────
    /// <summary>The clock every time-driven effect here reads: the hub pop, arm glitter, pour slosh, crescent
    /// flicker and tube noise, circuit sparks, fire fx, the twinkle and the arcade collapse. Null reads the
    /// real clocks; the render-snapshot harness pins it so a frame is a function of state alone. The
    /// slow-frame trace in <see cref="OnRender"/> measures real time and deliberately bypasses it.</summary>
    internal static DateTime? PinnedClockUtc = null;
    private static DateTime NowUtc => PinnedClockUtc ?? DateTime.UtcNow;
    /// <summary>Milliseconds on the same clock, for the effects that count in <c>Environment.TickCount64</c>.</summary>
    private static long NowMs => PinnedClockUtc is { } pinned
        ? pinned.Ticks / TimeSpan.TicksPerMillisecond
        : Environment.TickCount64;

    // ── Rendering ─────────────────────────────────────────────────────────────
    // C3/D3 diagnosis: slow-frame tracing (throttled to one line/second) so the "Terra and Kawaii draw
    // slower" report can be MEASURED from real use before anything is optimized — the trace names the
    // material, slice count and cost of any OnRender pass over the threshold.
    private static long _slowFrameTraceTick;
    private const double SlowFrameMs = 18.0;

    protected override void OnRender(DrawingContext dc)
    {
        var renderSw = System.Diagnostics.Stopwatch.StartNew();
        try { RenderCore(dc); }
        finally
        {
            renderSw.Stop();
            if (renderSw.Elapsed.TotalMilliseconds > SlowFrameMs
                && Environment.TickCount64 - _slowFrameTraceTick > 1000)
            {
                _slowFrameTraceTick = Environment.TickCount64;
                System.Diagnostics.Trace.WriteLine(
                    $"[Render] slow frame {renderSw.Elapsed.TotalMilliseconds:F1}ms material={_material} " +
                    $"slices={Slices.Count} edit={_sm.EditMode} armed={_sm.ArmedIndex}");
            }
        }
    }

    private void RenderCore(DrawingContext dc)
    {
        // An EMPTY wheel is a valid state that acts as a DISABLED side: a plain invoke draws NOTHING
        // (App's silent-invoke path never even shows the overlay — the trigger stays free for the game).
        // Only EDIT mode renders on an empty wheel (the hub + legend must survive backing out of the Add
        // picker at 0 slices, or the user is stranded on a dimmed screen with no visible UI).
        if (Slices.Count == 0 && !_sm.EditMode) return;

        double ppd    = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var    center = new Point(ActualWidth / 2, ActualHeight / 2);
        int    n      = Slices.Count;

        // Advance the hub's appear/disappear animation ONCE, here at the top — DrawCenter consumes the
        // result rather than computing it, because terra's backdrop (drawn well before the hub) sizes its
        // centre hole from the same scale and must not lag a frame behind it.
        (_hubFrameScale, _hubFrameDraw) = HubPresence(HubDemanded());

        // Ring layer (backglow + non-selected slices) — fades out together on selection.
        dc.PushOpacity(RingOpacity);
        if (n > 0 && _material == "mesa") DrawTerraBackdrop(dc, center);   // irregular blob behind everything
        else if (n > 0 && _custom is { BackdropImage: not null }) DrawCustomBackdrop(dc, center);   // drop-in theme underlay
        // Reactor draws NO wheel-wide drop shadow — its dark gradient wedges + per-icon drop shadows
        // carry it. Kawaii keeps the backglow like the other light materials.
        if (n > 0 && !_sm.EditMode && _material is not "reactor" && _collapseGlow > 0.001)   // edit mode dims the whole screen instead (OverlayWindow scrim)
        {
            // Shadow shape is keyed ONLY on HideCenter — a material/setting-level fact that cannot change
            // while a wheel is up. It must NOT follow the hub's momentary content: the two variants differ
            // across the WHOLE disc (soft disc vs donut), so keying on content would visibly change (or
            // appear to drop) the entire drop shadow mid-wheel whenever a status readout / notice / volume
            // scrub pops the hub in. Cost when the hub does appear on a hub-hiding material: no shadow blob
            // under it. The hub disc is opaque, so that's invisible — and, crucially, CONSTANT.
            // An arcade collapse leaves the grown hub as the game's backdrop, and that disc — not the wheel —
            // is what the shadow now belongs to. Both gradients are RELATIVE to the drawn ellipse, so the
            // same brush re-radiused follows it; and it's always the DISC variant here, because the donut
            // would cut its hole straight through the middle of a solid backdrop.
            bool behindHub = _collapseHeld || _collapseIndex >= 0;
            bool donut = !behindHub && HideCenter;
            var glow = donut
                ? (_backglowNoHub ??= BuildBackglow(false))
                : (_backglow ??= BuildBackglow(true));
            double r = behindHub ? HubRadius + BackglowHaloPx : OuterRadius + BackglowHaloPx;
            bool ducked = _collapseGlow < 1;
            if (ducked) dc.PushOpacity(_collapseGlow);
            // The donut variant paints as an annulus — identical pixels (its centre is fully transparent
            // by construction, see BackglowRing) without filling the dead middle every frame.
            if (donut) dc.DrawGeometry(glow, null, BackglowRing(center, r));
            else       dc.DrawEllipse(glow, null, center, r, r);
            if (ducked) dc.Pop();
        }
        // While carrying, the armed index stays pinned to the carried slice's origin slot — which now
        // holds a different slice in the provisional order. Suppress the armed highlight so that stale
        // slot doesn't light up redundantly; the carried slice shows intent via its lift.
        bool carrying = _sm.EditMode && _sm.Phase == WheelStateMachine.EditPhase.Carrying;
        // The ARMED slice draws LAST: its (grown/lifted) icon or logo may overhang the wedge, and in index
        // order a later neighbour's background painted over the overhang.
        int armedNow = !carrying ? _sm.ArmedIndex : -1;
        // Kawaii's arm glitter starts when the armed index differs from the one it last swept; clearing that
        // memory on every frame with nothing armed makes re-arming the same slice sweep again.
        if (armedNow < 0) _kawaiiGlitterIndex = -1;
        // Resting slices blit from per-slice bitmap bakes (see EnsureSliceBakes) whenever the wheel is in
        // a steady state: only the armed slice, the dome, the hub, and fx render live. Edit mode, assign
        // mode, and the arcade collapse animate per-slice geometry, so they draw everything directly.
        bool baked = !_sm.EditMode && _collapseIndex < 0 && !_collapseHeld;
        if (baked) EnsureSliceBakes(center, n, ppd);
        for (int i = 0; i < n; i++)
            if (i != _firingIndex && i != armedNow)
            {
                if (baked && i < _sliceBakes.Length && _sliceBakes[i] is { } sb)
                    dc.DrawImage(sb.Bmp, sb.Dst);
                else
                    DrawCollapsingSlice(dc, center, i, n, armed: false, ppd);
            }
        if (armedNow >= 0 && armedNow < n && armedNow != _firingIndex)
            DrawCollapsingSlice(dc, center, armedNow, n, armed: true, ppd);
        if (_sm.Ghost is not null && _sm.GhostFade > 0.001) DrawGhost(dc, center);   // a just-deleted slice fading out (incl. the LAST one)
        if (n > 0 && WheelDome) DrawWheelDome(dc, center);   // subtle wheel-wide glass dome over the slices
        dc.Pop();

        // The just-selected slice fades on its own (lagging) opacity so it lingers slightly
        // longer than the rest of the wheel.
        if (_firingIndex >= 0 && _firingIndex < n)
        {
            dc.PushOpacity(SelectedSliceOpacity);
            DrawCollapsingSlice(dc, center, _firingIndex, n, armed: true, ppd, firing: true);
            dc.Pop();
        }

        // Centre layer (hub + status readout) — can linger then fade separately.
        // No tilt dot in the hub on any material, and don't reintroduce one: the armed slice IS the analog
        // readout. WheelStateMachine.StickX/Y stay live for arming, Reactor parallax, and the Game Grid.
        dc.PushOpacity(CenterOpacity);
        DrawCenter(dc, center, ppd);
        dc.Pop();

        if (MixBalanceActive) DrawMixBalance(dc, center, ppd);

        // Kawaii fire confetti — the very top layer, at its OWN opacity (independent of the ring/centre
        // fades), so the burst stays bright while the fired wheel dissolves underneath it.
        if (_fireFx.Count > 0) DrawFireConfetti(dc);
        // Salvage's spark bloom rides the same top layer, for the same reason.
        if (_sparkFx.Count > 0) DrawFireSparks(dc);
    }

    /// <summary>Horizontal balance bar for the volume-mixer D-pad scrub: a rounded track with a centre
    /// notch, a thumb at the current balance, and the two device names (truncated) at the ends. Drawn
    /// below the wheel's outer ring so it never overlaps the volume scrubber's readout in the hub.</summary>
    private void DrawMixBalance(DrawingContext dc, Point center, double ppd)
    {
        double y = center.Y + OuterRadius + 40;

        if (MixBalanceNoDevice)
        {
            var textBrush = DarkMaterial ? ScrubTextDark : ScrubTextLight;
            var ft = new FormattedText(Loc.T(UiText.Status.NoDevice), CultureInfo.InvariantCulture, WpfFlow.LeftToRight,
                HubFace, 15, textBrush, ppd);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2, y - ft.Height / 2));
            return;
        }

        const double barW = 210, barH = 12;
        var trackBrush = DarkMaterial ? MixTrackDark : MixTrackLight;
        var thumbBrush = DarkMaterial ? MixThumbDark : MixThumbLight;

        var trackRect = new Rect(center.X - barW / 2, y, barW, barH);
        dc.DrawRoundedRectangle(trackBrush, null, trackRect, barH / 2, barH / 2);
        dc.DrawLine(MixNotchPen, new Point(center.X, y - 3), new Point(center.X, y + barH + 3));   // centre notch

        double balance = Math.Clamp(MixBalanceValue, 0.0, 1.0);
        double thumbX  = trackRect.X + balance * barW;
        dc.DrawEllipse(thumbBrush, MixThumbOutline, new Point(thumbX, y + barH / 2), 7, 7);

        string leftLabel  = TruncateMixName(MixLeftName);
        string rightLabel = TruncateMixName(MixRightName);
        var leftFt  = new FormattedText(leftLabel,  CultureInfo.InvariantCulture, WpfFlow.LeftToRight, ScrubberFace, 11, MixLabelInk, ppd);
        var rightFt = new FormattedText(rightLabel, CultureInfo.InvariantCulture, WpfFlow.LeftToRight, ScrubberFace, 11, MixLabelInk, ppd);
        dc.DrawText(leftFt,  new Point(trackRect.Left  - leftFt.Width - 8, y + barH / 2 - leftFt.Height / 2));
        dc.DrawText(rightFt, new Point(trackRect.Right + 8,                y + barH / 2 - rightFt.Height / 2));
    }

    private static string TruncateMixName(string? s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= 12 ? s : s[..12] + "…");

    /// <summary><see cref="DrawSlice"/>, wrapped in the arcade collapse's per-slice transform.
    ///
    /// <para>Deliberately a WRAPPER rather than another branch inside DrawSlice: that method is already
    /// threading six materials' worth of dwell, arm and edit state, and the collapse needs none of it — it
    /// only has to move and deform whatever the material drew. Squash-and-stretch is applied along the
    /// slice's own RADIAL axis (rotate to it, scale, rotate back), so a slice stretches in the direction it
    /// is travelling and pinches across it, which is what sells the motion as weight rather than as a
    /// shrink.</para></summary>
    // ── Resting-slice bitmap cache ────────────────────────────────────────────
    // Each RESTING slice bakes once into a frozen bitmap; steady-state frames blit ≤12 bitmaps instead of
    // re-running the full vector pipeline (textures, blocky walks, masked shadows, label layout) per slice.
    // The bakes survive arm changes — arming just skips that slot's bitmap and draws the armed slice live —
    // so a stick sweep costs no rebakes. The key carries everything a resting slice's look derives from;
    // the style hash catches IN-PLACE restyles (App.PopulateIcons mutates slice.Icon on the live list).
    private sealed record SliceBake(BitmapSource Bmp, Rect Dst);
    private SliceBake?[] _sliceBakes = [];
    private (object list, double inner, double cx, double cy, string mat, bool wheelB, double ppd, string labels, int style)
        _sliceBakeKey;
    private const double SliceBakePadPx = 40.0;   // spill margin past the sector box (shadows, rims, rivets)

    /// <summary>Bake the resting wheel while it is PARKED, so the first frame of the first open blits
    /// bitmaps instead of building them. Measured before this: the first open of every process spent
    /// 55 ms (any material) to 200 ms (Salvage, with its photo decodes) inside <c>OnRender</c>, because the
    /// bakes were only ever built from the render pass and <see cref="BakesPerFrame"/> covers a small
    /// wheel in one frame. Runs at Background priority on the UI thread: nothing is on screen, the pad feed
    /// never touches the dispatcher, and the key already covers every input, so a pre-warm can't go stale.</summary>
    private void SchedulePrewarm()
    {
        if (_prewarmQueued) return;
        _prewarmQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, PrewarmParked);
    }

    private bool _prewarmQueued;

    private void PrewarmParked()
    {
        _prewarmQueued = false;
        if (_wheelLive || _sm.EditMode || ActualWidth <= 0 || ActualHeight <= 0 || Slices.Count == 0) return;
        try
        {
            // The material assets first — a Salvage wheel's 1.57 M-pixel surface photo, its rivet and banner,
            // and the exposure-lifted copy the first ARMED frame otherwise pays for.
            if (_material == "salvage") { SalvageRustBitmap(); SalvageRivetBitmap(); SalvageBannerBitmap(); SalvageArmedBitmap(); }
            else if (_material == "mesa") TerraGroundBitmap();
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            EnsureSliceBakes(new Point(ActualWidth / 2, ActualHeight / 2), Slices.Count, ppd, all: true);
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Render] pre-warm skipped: {ex.Message}"); }
    }

    private void EnsureSliceBakes(Point center, int n, double ppd, bool all = false)
    {
        int style = 17;
        for (int i = 0; i < n; i++)
        {
            var s = Slices[i];
            style = HashCode.Combine(style,
                s.Icon      is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.Icon),
                s.LogoImage is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.LogoImage),
                s.Label, s.IconColor, s.ShowLabel, s.IconIsCover);
        }
        // Material identity includes a drop-in theme's content hash: a token's content is fixed per
        // run today, but the bake must not depend on that policy never changing (docs/PACKAGES.md).
        var matId = _custom is { } customBake ? _material + ":" + customBake.Spec.ContentHash : _material;
        var key = ((object)Slices, _sliceInner, center.X, center.Y, matId, IsWheelB, ppd, _labelMode, style);
        if (_sliceBakes.Length != n || _sliceBakeKey != key)
        {
            _sliceBakeKey = key;
            _sliceBakes = new SliceBake?[n];
            _bakeCursor = 0;
        }
        if (_bakeCursor >= n) return;

        // Bake a FEW slices per frame rather than the whole wheel at once. Baking every slice on the
        // first frame after an open cost 40-80ms exactly as the wheel bloomed in — a visible hitch, and
        // precisely the stutter this cache exists to remove. Slots past the cursor fall through to the
        // live vector draw (the pre-cache path), so a partly-filled cache is never wrong, only less fast.
        double ss = Math.Max(1.0, VisualTreeHelper.GetDpi(this).DpiScaleX);
        double sliceDeg = 360.0 / n;
        int stop = all ? n : Math.Min(n, _bakeCursor + BakesPerFrame);
        for (int i = _bakeCursor; i < stop; i++)
        {
            try
            {
                double midDeg = sliceDeg * i - 90;
                var rect = SectorBounds(center, OuterRadius, midDeg - sliceDeg / 2.0, midDeg + sliceDeg / 2.0);
                rect.Inflate(SliceBakePadPx, SliceBakePadPx);
                // Snap the box to DEVICE pixels so the blit is a 1:1 copy, never a half-pixel resample.
                double x0 = Math.Floor(rect.X * ss) / ss,      y0 = Math.Floor(rect.Y * ss) / ss;
                double x1 = Math.Ceiling(rect.Right * ss) / ss, y1 = Math.Ceiling(rect.Bottom * ss) / ss;
                rect = new Rect(x0, y0, x1 - x0, y1 - y0);
                var dv = new DrawingVisual();
                using (var bdc = dv.RenderOpen())
                {
                    bdc.PushTransform(new TranslateTransform(-rect.X, -rect.Y));
                    DrawCollapsingSlice(bdc, center, i, n, armed: false, ppd);
                    bdc.Pop();
                }
                var rtb = new RenderTargetBitmap(
                    (int)Math.Round(rect.Width * ss), (int)Math.Round(rect.Height * ss),
                    96.0 * ss, 96.0 * ss, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                _sliceBakes[i] = new SliceBake(rtb, rect);
            }
            catch { _sliceBakes[i] = null; }   // that slot falls back to the live vector draw
        }
        // The cursor only ever advances, so a slot whose bake threw is settled as "draw live" rather
        // than retried every frame. Keep frames coming until the wheel is fully baked — on a material
        // with no ambient animation nothing else would ask for them.
        _bakeCursor = stop;
        if (_bakeCursor < n) Dispatcher.BeginInvoke(InvalidateVisual, DispatcherPriority.Background);
    }

    private int _bakeCursor;              // slices [0, _bakeCursor) are settled (baked, or failed → live)
    private const int BakesPerFrame = 2;  // ≤12 slices ⇒ fully cached within ~6 frames of an open

    /// <summary>Bounding box of the full circular sector (centre → <paramref name="rOut"/> between the two
    /// boundary angles): both boundary points, plus every axis-aligned extreme (0/90/180/270°) the arc
    /// crosses, plus the centre itself — conservative for a ring wedge, exact for a pie slice.</summary>
    private static Rect SectorBounds(Point c, double rOut, double a0, double a1)
    {
        double minX = c.X, maxX = c.X, minY = c.Y, maxY = c.Y;
        void P(double deg)
        {
            double rad = deg * Math.PI / 180.0;
            double x = c.X + Math.Cos(rad) * rOut, y = c.Y + Math.Sin(rad) * rOut;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        P(a0); P(a1);
        for (double a = Math.Ceiling(a0 / 90.0) * 90.0; a <= a1; a += 90.0) P(a);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private void DrawCollapsingSlice(DrawingContext dc, Point center, int i, int n, bool armed, double ppd,
                                     bool firing = false)
    {
        double p = CollapseProgressFor(i);
        if (p >= 1) return;                                  // already behind the hub
        if (p <= 0) { DrawSlice(dc, center, i, n, armed, ppd, firing); return; }

        double midDeg = 360.0 / Math.Max(1, n) * i - 90;
        double midRad = midDeg * Math.PI / 180.0;
        double dirX = Math.Cos(midRad), dirY = Math.Sin(midRad);
        double pivotR = (_sliceInner + OuterRadius) / 2.0;
        var pivot = new Point(center.X + dirX * pivotR, center.Y + dirY * pivotR);

        // Anticipation: a slice leans OUT slightly before it goes in. Cheap, and it's most of the reason
        // this reads as animation rather than as interpolation.
        double eased = p < 0.18
            ? -0.12 * Math.Sin(p / 0.18 * Math.PI)
            : EaseIn((p - 0.18) / 0.82);
        // Travel the whole way to the centre and never fade: the hub is opaque and drawn after this layer, so
        // it occludes the slice for real, and a slice parked at dead centre is covered whatever the hub's
        // current size.
        double travel = pivotR * eased;

        // Stretch along travel, pinch across it, conserving rough area.
        double stretch = 1 + 0.42 * Math.Sin(Math.Clamp(p, 0, 1) * Math.PI);
        double squash = 1 / stretch;

        dc.PushTransform(new TranslateTransform(-dirX * travel, -dirY * travel));
        dc.PushTransform(new RotateTransform(midDeg, pivot.X, pivot.Y));
        dc.PushTransform(new ScaleTransform(stretch, squash, pivot.X, pivot.Y));
        dc.PushTransform(new RotateTransform(-midDeg, pivot.X, pivot.Y));
        DrawSlice(dc, center, i, n, armed, ppd, firing);
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static double EaseIn(double t) => Math.Clamp(t, 0, 1) * Math.Clamp(t, 0, 1);

    private void DrawSlice(DrawingContext dc, Point center, int i, int n, bool armed, double ppd,
                           bool firing = false)
    {
        double sliceDeg = 360.0 / n;
        double targetMid = sliceDeg * i - 90;                    // slice 0 centered at 12 o'clock
        double midDeg    = targetMid;

        // Edit-mode per-slice animation: slide in from the pre-change layout (reflow), lift the
        // carried slice, pop a freshly-added one in.
        double animScale = 1.0, animOpacity = 1.0;
        // The carried slice is identified by REFERENCE (it lives at its provisional slot in the render
        // list, not a fixed index), so the lift tracks it as the preview flows the wheel around it.
        bool carried = _sm.EditMode && _sm.CarriedSlice is { } cs && ReferenceEquals(Slices[i], cs);
        if (_sm.EditMode)
        {
            if (_sm.Reflowing && _sm.PrevCenter is { } pc && pc.TryGetValue(Slices[i], out double prev))
                midDeg = LerpAngle(prev, targetMid, EaseOut(_sm.ReflowProgress));
            if (firing) { /* fading out — no edit transform */ }
            else if (carried) animScale = 1.12;                  // lifted in hand, at its provisional slot
            else if (i == _sm.PopInIndex && _sm.Reflowing)
            { double e = EaseOut(_sm.ReflowProgress); animScale = 0.45 + 0.55 * e; animOpacity = e; }

            // Landing settle on the just-placed slice: a brief overshoot (1 → ~1.13 → 1) so the drop
            // reads as snapping into place. Half-sine gives a smooth up-and-back with no discontinuity.
            if (_sm.Landing && i == _sm.LandingIndex && !firing)
                animScale *= 1.0 + Math.Sin(_sm.LandingProgress * Math.PI) * 0.13;
        }

        double startBnd = midDeg - sliceDeg / 2.0;
        double endBnd   = midDeg + sliceDeg / 2.0;

        // In the firing layer the slice is just a highlighted wedge fading out — no confirm
        // dwell visuals (the dwell is already satisfied by the time it fires).
        // In EDIT mode a hold-to-confirm slice is being moved/added, not fired, so it must arm like any
        // normal slice — suppress the red confirm fill + dwell arc (assign mode keeps its green dwell,
        // since it runs with EditMode false).
        bool confirming = !firing && armed && !_sm.EditMode && _sm.RequiresConfirm(i);
        bool arming     = confirming && !_sm.ConfirmDone;

        // "Pouring" guarded dwell (kawaii + terra): the slice stays OPAQUE and its armed colour rises
        // inner→outer, popping to the full armed treatment at 100% — instead of the red dwell arc +
        // arming-transparency ramp (assign mode keeps its shared green dwell visuals on every material).
        // Kawaii pours strawberry milk with a white surface line; terra pours its armed yellow.
        // Reduce Motion: every material-specific dwell below (pour / sweep / magnet-lock) is replaced by
        // the shared static treatment — red confirm fill + stationary dwell arc — which carries the same
        // progress information without liquid, growth, travel, or shake.
        bool ownDwellAllowed = !_reduceMotion;
        bool kawaiiMilk = _material == "kawaii" && ownDwellAllowed;
        bool terraPour   = _material == "mesa"   && ownDwellAllowed;
        bool pourDwell   = kawaiiMilk || terraPour;
        // Salvage's own guarded dwell: its armed cue — the glowing crescent on the inner rim — GROWS from
        // a dot at the slice's centre out to both inner corners, arriving exactly as the normal armed
        // crescent. Like the pours, this replaces the red fill + dwell arc, and the icon doesn't lighten
        // until the dwell completes (armedLighten already requires !arming).
        bool salvageSweep = _material == "salvage" && ownDwellAllowed;
        // Reactor's own guarded dwell: the slice renders as a completely NORMAL armed slice (fused
        // keyhole, white glyph, circuit board), but the glyph is cut into four quarters along its
        // diagonals, pulled 10px apart, and drawn back together with magnet-like acceleration; at 100%
        // they snap and the glyph briefly shakes (see reactorQuarters / _reactorSnapUtc below).
        bool reactorLock  = _material == "reactor" && ownDwellAllowed;
        bool ownDwell     = pourDwell || salvageSweep || reactorLock;
        // Pearl/Obsidian's own guarded dwell: the glass keeps FULL opacity and RECOLOURS instead — its
        // ramp lerps resting → confirm red across the hold (GlossDwellFill). Unlike the four styled
        // materials' dwells this does NOT replace the red progress arc, which still rings the slice; only
        // the arming-transparency ramp is displaced, so glossDwell stays out of `ownDwell`.
        bool glossDwell = (_material == "pearl" || _material == "obsidian")
                          && ownDwellAllowed;

        // While a slice arms, draw its body extra-transparent and ramp it back to full as the dwell
        // fills — at normal opacity exactly when it finishes arming.
        double bodyOpacity = arming && !ownDwell && !glossDwell
            ? WheelStateMachine.ConfirmArmFloor + (1.0 - WheelStateMachine.ConfirmArmFloor) * Math.Clamp(_sm.ConfirmProgress, 0.0, 1.0)
            : 1.0;
        bodyOpacity *= animOpacity;

        // …× LabelSizeMul: the per-material font-metrics correction (Mesa's Jua reads small at a given em).
        double fontSize = Math.Max(13.0, 16.0 - Math.Max(0, n - 4) * 0.5) * LabelScaleFor(n) * 0.9 * LabelSizeMul;   // cap 10% smaller
        // Salvage's armed crescent (a 10px arc at _sliceInner+7) claims the innermost ~16px of the wedge,
        // so its content safe-area starts past it — icons/logos/labels never sit under the white arc.
        double contentInner = _material == "salvage" ? _sliceInner + 16 : _sliceInner;
        // …but that 16px reservation over-constrains GLYPHS on a thin or crowded ring: it's a quarter of a
        // ~62px band, so the icon lost roughly a third of its size to a crescent that only exists while
        // the slice is armed — and which draws UNDERNEATH the content anyway (see the crescent block), so
        // a few px of overlap is invisible. Glyphs therefore reserve far less. LOGOS keep the full 16 —
        // they fit the wedge outright via FitLogoHeight.
        double glyphInner = _material == "salvage" ? _sliceInner + 4 : contentInner;
        // Thick ring only: nudge the content 7.5% further out so it sits nearer the outer arc rather than
        // dead-centre in a very deep band. Medium/thin bands are too shallow to spare it.
        double midR     = (contentInner + OuterRadius) / 2.0 * _contentPushOut;
        // Reactor's hollow-rest disc is sized from the WHEEL, never from the slice's own content: every
        // input here (ring band, slice angle, content radius) is identical for every slice, so all the
        // discs come out the same size. Sizing it off each glyph would make the discs vary wildly, since
        // a fitted logo's box differs per slice and per aspect ratio. Content is then
        // fitted INSIDE this radius (see the logo/icon branches), rather than the disc chasing content.
        double reactorDiscR = 0;
        if (_material == "reactor" && ReactorHollowRest)
        {
            // Grow the disc until it reaches the wedge's own geometric limits — no rim closer to a
            // neighbour than the wedge boundary is, and no rim outside the ring band the wedge fills.
            // The pen is centred on the circle, so its outer half counts against both budgets.
            double halfPen = ReactorRestDiscPen.Thickness / 2.0;
            // RADIAL: measured from the ACTUAL disc centre, which is midR — on a thick ring that's pushed
            // outward (_contentPushOut), leaving less room outward than inward. Measuring the band's full
            // width instead ignores that and pushes thick-ring discs past the outer arc.
            double radial  = Math.Min(OuterRadius - midR, midR - _sliceInner) - halfPen;
            // ANGULAR: rim-to-rim across the chord between neighbouring centres must keep the wedges' gap.
            double angular = ChordWidth(midR, Math.Min(sliceDeg, 120.0)) / 2.0 - halfPen - GapPx / 2.0;
            reactorDiscR = Math.Max(18.0, Math.Min(radial, angular));
        }
        // Largest box of a given aspect that fits INSIDE that disc (corners on the circle), with a little
        // breathing room: half-width² + half-height² = r², so h = 2r / √(1 + aspect²).
        double DiscFitHeight(double aspect) => 2.0 * reactorDiscR / Math.Sqrt(1.0 + aspect * aspect) * 0.92;
        // Reactor's armed fusion swells the wedge outward. Like terra, the CONTENT is sized/placed at
        // resting geometry and pushed through a uniform translate+scale (see the content push below) —
        // consistent logo/glyph scaling regardless of slice shape. (Flag reused below for the fused
        // geometry + black keyline.)
        // Fused during the guarded DWELL too (reactorLock): the dwell renders as a normal armed slice —
        // only the quartered glyph marks it. (Only the dwell arc is shared.)
        bool reactorFused = _material == "reactor" && (armed || confirming) && (!arming || reactorLock);
        // Terra armed "3D card" lift: content is sized/placed at RESTING geometry and then run through the
        // SAME lift+scale transform as the card body (see the content push below) — so the logo/icon/label
        // scale exactly with the slice (1.08), stay aligned, and can never spill the wedge. Shifting the
        // content centre and re-running the wedge fit against it instead would change logo size by a
        // slice-angle-dependent amount.
        bool terraLifted = _material == "mesa" && (armed || confirming) && !arming;
        // Kawaii borrows terra's armed lift+grow — same transform, no card edge.
        bool kawaiiArmed = _material == "kawaii" && (armed || confirming) && !arming;
        // Custom theme armed motion: the same lift+scale transform, with the package's clamped numbers.
        // Same Reduce Motion class as Mesa/Kawaii's lift (MOTION-INVENTORY: essential, kept) — a static
        // state-driven transform, not continuous motion, so it is NOT gated on _reduceMotion.
        bool customLifted = _custom?.Spec.Motion is not null && (armed || confirming) && !arming;
        // Reactor: while armed the slice fuses into the hub and the hub captions it — suppress the wedge's
        // own label so the name isn't shown twice.
        bool suppressWedgeLabel = reactorFused;
        var    sc       = Polar(center, midR, midDeg);   // screen-space slice centre
        // A single 360° slice (or a 2-slice 180°) has a degenerate chord — sin(180°)≈0 collapses
        // maxW to ~0, wrapping the label into stacked "…". Cap the angle used for label width.
        double chordW   = ChordWidth(midR, Math.Min(sliceDeg, 120.0)) * 0.82;
        // Labels render HORIZONTALLY, so on a side slice the text runs radially toward the rim and the
        // chord is far too generous — a long label fits "within" the chord yet pokes past the outer arc.
        // Clamp the half-width so the label's horizontal endpoints stay inside the outer radius (top/bottom
        // slices, whose text runs across the chord, stay governed by chordW).
        double dx = sc.X - center.X, dy = sc.Y - center.Y;
        double span = (OuterRadius - 6) * (OuterRadius - 6) - dy * dy;
        double outerW = span > 0 ? 2.0 * (Math.Sqrt(span) - Math.Abs(dx)) : 0;
        double maxW   = Math.Max(40.0, Math.Min(chordW, outerW));

        // Shrink the icon+label block to fit a thinner ring so it doesn't spill past the band edges.
        // At full (thick) thickness this is 1.0 (no change); only thin needs it. Floor keeps it legible.
        double naturalBlockH = (IconSize + 4 + fontSize * 1.3) * IconScaleFor(n);
        double contentFit    = Math.Clamp(((OuterRadius - contentInner) - 12.0) / naturalBlockH, 0.40, 1.0);

        bool scaled = Math.Abs(animScale - 1.0) > 0.001;
        if (scaled) dc.PushTransform(new ScaleTransform(animScale, animScale, sc.X, sc.Y));

        bool pushed = bodyOpacity < 0.999;
        if (pushed) dc.PushOpacity(bodyOpacity);

        Brush confirmFill = ConfirmFillFor();
        Pen   confirmPen  = ConfirmPen;

        // Delete: armed (old hold-to-delete) OR the edit-mode hold-□ dwell in progress → red.
        bool editDeleting = _sm.EditMode && armed && _sm.DeleteProgress > 0.001;
        bool deleting = editDeleting;
        // `carried` (by-ref) computed above; the slice now occupies the drop slot in the preview, so there
        // is no separate target outline — the lifted slice itself marks where it will land.

        // Terra/salvage get an irregular hand-cut/sprayed edge (deterministic per slice via seed = i);
        // every other material keeps the crisp true-arc wedge.
        var salv = _material == "salvage" ? SalvageGeo(center, i, n, startBnd, endBnd) : null;
        var geo = _material switch
        {
            "mesa"   => TerraWedge(center, i, n, startBnd, endBnd),
            "kawaii" => KawaiiWedge(center, n, startBnd, endBnd),
            "salvage" => salv!.Wedge,
            _         => BuildWedge(center, _sliceInner, OuterRadius, startBnd, endBnd),
        };
        // Reactor: an armed (or confirm-dwelling-past-arm) slice FUSES with the hub into one continuous
        // keyhole/blob silhouette — the hub's near-black fill "flows out" to engulf the wedge — instead of
        // drawing as a separate wedge. This also guarantees the keyhole reads even when the hub disc itself
        // is hidden (see DrawCenter's `HideCenter` gating): the hub is baked into this geometry.
        // (reactorFused itself is computed above, where the content centre shifts with the swell.)
        if (reactorFused) geo = BuildReactorFusion(center, i, startBnd, endBnd,
                                                   reactorDiscR > 0 ? reactorDiscR : FusionJunctionR);
        // Once the confirm dwell completes (ConfirmDone), a guarded slice reads as a normal armed
        // slice (material-driven fill/outline) — the red confirm visuals are for the dwell only.
        // Kawaii: fills are PER-SLICE (the hue walks the ring); its guarded dwell keeps the rest fill
        // under the rising milk, and the milk itself is the progress cue.
        Brush fill = _material == "kawaii"
                   ? ((armed || confirming) && !arming ? KawaiiArmedFill(midDeg)
                      : arming && !kawaiiMilk ? confirmFill                     // assign mode's green dwell
                      : KawaiiRestFill(midDeg))
                   // Custom theme hue walk: per-slice fills like Kawaii's, but through the SHARED dwell
                   // visuals (custom themes have no own-dwell replacement — the red arc is the cue).
                   : _custom is { HasHue: true } customHue
                   ? (arming ? confirmFill
                      : (armed || confirming) ? customHue.HueFill(midDeg, armedState: true)
                      : customHue.HueFill(midDeg, armedState: false))
                   : arming && terraPour ? TerraFill   // terra pours over its RESTING cream (overlay below)
                   : arming && salvageSweep ? SalvageFill   // salvage dwells on its RESTING charcoal
                   : arming && reactorLock ? ArmedFillFor()   // reactor dwells looking fully ARMED
                   : arming && glossDwell ? GlossDwellFill(_sm.ConfirmProgress)   // glass recolours over the hold
                   : arming ? confirmFill : (armed || confirming) ? ArmedFillFor() : ShapeFill(geo.Bounds, center);
        Pen?  pen  = deleting    ? DeleteOutlinePen   // thick red outline; keep the normal fill
                   : carried     ? CarryLiftPen       // the slice in hand, at its provisional slot
                   : arming      ? (kawaiiMilk ? KawaiiArmedPen : terraPour ? TerraArmedPen
                                    : salvageSweep ? SalvagePen
                                    : reactorLock ? ArmedPenFor() : confirmPen)
                   : (armed || confirming) ? ArmedPenFor()
                   :               RestingPenFor();   // material-styled resting outline (null = unstroked)
        // Reactor hollow-rest: the wedge itself is INVISIBLE (transparent fill, no stroke) — the slice's
        // apparent body is the rimmed rest disc — so the carry/delete outline tracing the wedge silhouette
        // hovered around nothing the user could see. Move the outline ONTO the disc: the wedge stays at its
        // resting (null) pen and the edit pen replaces the disc's own rim in the DrawReactorRestDisc calls
        // below, so the placement line fits the slice's APPARENT shape.
        Pen? reactorDiscEditPen = null;
        if ((deleting || carried)
            && _material == "reactor" && ReactorHollowRest && !armed && !confirming && !arming)
        {
            reactorDiscEditPen = deleting ? DeleteOutlinePen : CarryLiftPen;
            pen = RestingPenFor();
        }

        // Terra armed "3D card": the whole body (shadow + card edge + face) lifts outward along the
        // slice's own direction and grows about its centre; a thick darker-cream copy offset straight
        // DOWN gives the south/6-o'clock edge its card thickness.
        if (terraLifted || kawaiiArmed || customLifted)
        {
            double liftRad = midDeg * Math.PI / 180.0;
            // Outward lift along the slice direction, plus a fixed 8px NORTH nudge (screen-up) so the
            // lifted card reads as raised toward the light.
            double liftPx = terraLifted ? TerraLiftPx  : kawaiiArmed ? KawaiiLiftPx  : _custom!.Spec.Motion!.LiftPx;
            double northPx = terraLifted ? TerraNorthPx : kawaiiArmed ? KawaiiNorthPx : _custom!.Spec.Motion!.NorthPx;
            double liftScale = terraLifted ? TerraArmedScale : kawaiiArmed ? KawaiiArmedScale : _custom!.Spec.Motion!.Scale;
            dc.PushTransform(new TranslateTransform(Math.Cos(liftRad) * liftPx,
                                                    Math.Sin(liftRad) * liftPx + northPx));
            dc.PushTransform(new ScaleTransform(liftScale, liftScale, sc.X, sc.Y));
        }
        // Terra cutout shadow: a hard offset copy of the SAME wobbled shape underneath the real slice
        // (skipped while fading out in the firing layer — the offset shadow reads oddly mid-fade).
        if (_material == "mesa" && !firing)
        {
            dc.PushTransform(new TranslateTransform(2, 3));
            dc.DrawGeometry(TerraShadow, null, geo);
            dc.Pop();
        }
        if (terraLifted)   // thick south card edge, under the face — umber-outlined like the face itself
        {
            dc.PushTransform(new TranslateTransform(0, TerraCardPx));
            dc.DrawGeometry(TerraCardFill, TerraArmedPen, geo);
            dc.Pop();
        }
        // Reactor armed fusion: a thin black keyline OUTSIDE the white stroke — a wider black pen drawn
        // first; the fill + white stroke drawn over it hide everything but the outer sliver.
        if (reactorFused && ReferenceEquals(pen, ReactorFusedPen))
            dc.DrawGeometry(null, ReactorFusedBlackPen, geo);
        // While pouring, the body is drawn UNSTROKED and the outline goes on last (below), so the rising
        // fill slides UNDER it — an outline pen is centred on the path, so its inner half sits inside the
        // wedge and a pour clipped to that wedge would otherwise paint over it.
        bool pouring = arming && pourDwell;
        // Reactor: while ANY slice is armed, the others' background gradient drops to a third — the fused
        // slice owns the eye and the rest recede. Only the wedge FILL dims (the icon/logo is drawn later,
        // outside this push, and keeps full strength); unarmed reactor wedges are unstroked, so wrapping
        // the draw can't dim an outline that isn't there.
        bool reactorRecede = _material == "reactor" && _sm.ArmedIndex >= 0 && !armed && !confirming;
        // Salvage's edge stroke belongs on the INNER rim only, not the full wedge outline — suppressed
        // here (only when `pen` is genuinely the shared SalvagePen; the delete/carry/add-slice outlines
        // are a different pen and still draw normally) and drawn separately below, against a matching
        // wobbled polyline so it traces the slice's own sprayed inner edge.
        bool salvageInnerOnly = ReferenceEquals(pen, SalvagePen);
        Pen? bodyPen = pouring || salvageInnerOnly ? null : pen;
        // Reactor's hollow rest resolves to a fully transparent, unstroked wedge — drawing it still
        // tessellates and rasterizes the whole geometry, so skip the no-op (the rimmed rest disc is the
        // slice's visible body). The recede opacity only ever wrapped this draw, so it skips with it.
        if (!ReferenceEquals(fill, Brushes.Transparent) || bodyPen is not null)
        {
            // Opaque body: back the wedge with a solid plate of the SAME shape (see the plate block up
            // top). Skipped when the fill is Transparent (reactor hollow rest — its disc has its own).
            if (!ReferenceEquals(fill, Brushes.Transparent))
                dc.DrawGeometry(DarkMaterial ? HubOpaqueDark : HubOpaqueLight, null, geo);
            if (reactorRecede) dc.PushOpacity(1.0 / 3.0);
            dc.DrawGeometry(fill, bodyPen, geo);
            if (reactorRecede) dc.Pop();
            // Drop-in theme surface texture: painted over the fill, clipped to the wedge by drawing the
            // same geometry (the Salvage-grain pattern, data-driven). Everything later — outline, dwell,
            // glyph, label — sits on top.
            if (_custom?.SliceTexture is { } sliceTex && !ReferenceEquals(fill, Brushes.Transparent))
                dc.DrawGeometry(sliceTex, null, geo);
        }

        // Salvage's rusted-metal grain, then BOTH edge treatments — and both live on the INNER RIM ONLY,
        // so a slice reads as a plate lipped along its bottom edge rather than one outlined all the way
        // round. Everything later (crescent, glyph, shadow) sits on top.
        if (_material == "salvage" && !deleting && !carried)
            DrawSalvageEdgeDetail(dc, geo, center, i, n, startBnd, endBnd, armed, confirming, arming,
                                  salv!, salvageInnerOnly);

        // Pouring guarded dwell: the armed colour pours in from the inner edge and fires at the brim —
        // a rising radial fill clipped to the wedge. Kawaii's strawberry milk carries a thin white
        // "surface" line; terra's yellow keeps a hard edge (a cut-paper shape, not a liquid).
        if (pouring) DrawPourDwell(dc, geo, pen, center, midDeg, sliceDeg, kawaiiMilk);

        // Kawaii armed cue #2: a pastel-rainbow arc hugging the armed wedge's outer edge. Drawn INSIDE the
        // lift transform so it rides out with the slice.
        if (kawaiiArmed) DrawKawaiiArmedRim(dc, center, midDeg, sliceDeg, i);
        if (terraLifted || kawaiiArmed || customLifted) { dc.Pop(); dc.Pop(); }

        // Reactor fusion ripples + circuit board parallax (the fused keyhole's own detail layer).
        if (reactorFused) DrawReactorFusionEffects(dc, geo, center, n);

        // Salvage armed cue: a thick white inner-arc crescent spanning the middle of the wedge, once armed
        // (or confirm-done). On a GUARDED slice it doubles as the dwell: the crescent starts as a dot at
        // the slice's centre and grows toward both inner corners, reaching full span exactly as the slice
        // arms — so the finished state is identical to a normal arm, no separate "completed" visual.
        // (The standard dwell arc is excluded via salvageSweep.)
        bool salvageGrowing = _material == "salvage" && arming && salvageSweep;
        if (_material == "salvage" && ((armed || confirming) && !arming || salvageGrowing))
            DrawSalvageCrescent(dc, center, i, midDeg, sliceDeg, salvageGrowing);


        // Per-material label typeface: salvage is condensed + UPPERCASE (sprayed-salvage look); terra is
        // chunky Black; other materials keep the default face.
        var labelFace = LabelFaceFor(armed || confirming);
        string label = Loc.DefaultLabel(Slices[i].Label);   // stored English, shown in the run's language
        if ((_material == "salvage" || _custom?.Spec.LabelUpper == true) && label.Length > 0)
        {
            // Memoized — this runs per slice per frame, and the label set is small and stable.
            if (!UpperLabelCache.TryGetValue(label, out var up))
            {
                if (UpperLabelCache.Count > 512) UpperLabelCache.Clear();   // same cap as LabelMeasureCache
                UpperLabelCache[label] = up = label.ToUpperInvariant();
            }
            label = up;
        }

        // While delete is armed, the slice's glyph becomes a red ⊖ (MinusCircle) and the label
        // turns red — that's the "release to remove" cue (no separate badge).
        Brush? labelBrush = deleting  ? DeleteContentBrush
                          : _material == "kawaii" ? KawaiiLabelBrushFor(Slices[i])
                          : InkLabel;
        bool   cover = Slices[i].IconIsCover && !deleting;
        var    icon  = deleting   ? (PackIconHelper.FromName("MinusCircle", DeleteContentBrush) ?? Slices[i].Icon as ImageSource)
                     : Slices[i].Icon as ImageSource;   // Core stores it as object; cast for WPF draw

        // An assigned-game logo (transparent) replaces the glyph + label, tinted to the slice colour.
        var logo = !deleting ? Slices[i].LogoImage as ImageSource : null;

        // Label visibility: the GLOBAL "Label slices" mode decides — default "all-except-logos" matches
        // the per-slice default, and "selected" is where the per-slice Show-label flag still rules. The
        // Add picker always labels its entries (they're a menu of choices and must be readable to be
        // usable), and EDIT MODE forces labels on too — you can't tell same-glyph slices apart while
        // rearranging them otherwise. That second clause is DELIBERATELY outside the rule, so "none" does
        // NOT apply inside edit mode / the picker.
        // Game-LOGO slices are exempt from that EDIT-MODE clause alone — the logo identifies them while
        // rearranging. The MODE still governs them: "All Slices" labels a logo slice, and so does a ticked
        // one under "Slices I Choose" (see the logo draw below, which reserves the label's band).
        bool showLabels = SliceLabelRule.ShouldShow(Slices[i], _labelMode)
                          || ((_sm.EditMode || _sm.Phase == WheelStateMachine.EditPhase.Picking)
                              && string.IsNullOrWhiteSpace(Slices[i].LogoPath));

        // Geometry-aware label fit for the icon+label block: measure the WRAPPED label up front so a
        // two-line label grows the block and contentFit shrinks the whole block back into the ring band
        // (an estimate that assumed one line would let a wrapped second line spill below the band on
        // medium/thin). And clamp the label's width by the outer arc at the label's TRUE height — the
        // label hangs BELOW the slice centre, so the centre-height clamp above is too generous for bottom
        // slices (labels poked past the arc). Two passes, because the wrap depends on the width, which
        // depends on the wrapped height.
        FormattedText? labelFt = null;
        if (logo is null && icon is not null && showLabels && !suppressWedgeLabel)
        {
            double iconSz = IconSize * GlyphScale(n, cover);
            double lgap   = _thinRing ? 0.0 : 4.0;
            // Terra's opaque icon rim dilates TerraIconEdgePx past the glyph silhouette — on a thin ring the
            // gap is ZERO, so without this the umber rim paints straight under the label's ink. Reserve the
            // rim. (Its drop shadow falls further still, TerraShadowDy below the rim, but a shadow at
            // TerraShadowAlpha under label ink is invisible — not worth pushing every label down for.)
            if (_material == "mesa") lgap = Math.Max(lgap, TerraIconEdgePx + 1.0);
            double band   = (OuterRadius - glyphInner) - 12.0;   // glyph reservation, not the logo one
            double lh     = fontSize * 1.3;                                   // pass-1 guess: one line
            for (int pass = 0; pass < 2; pass++)
            {
                double blockH = iconSz + lgap + lh;
                contentFit  = Math.Clamp(band / blockH, 0.40, 1.0);
                double topL = -blockH / 2 + iconSz + lgap;                    // label top, local about sc
                double yT   = sc.Y + contentFit * topL        - center.Y;     // label edges in SCREEN space
                double yB   = sc.Y + contentFit * (topL + lh) - center.Y;     //   (the fit scale is about sc)
                double dyE  = Math.Max(Math.Abs(yT), Math.Abs(yB));
                double sp   = (OuterRadius - 6) * (OuterRadius - 6) - dyE * dyE;
                double ow   = sp > 0 ? 2.0 * (Math.Sqrt(sp) - Math.Abs(dx)) : 0;
                double w    = Math.Max(40.0, Math.Min(chordW, ow / contentFit));
                labelFt = MeasureLabel(label, ppd, fontSize, w, fontSize * 2.8, labelBrush ?? LabelBrush, labelFace);
                lh = labelFt.Height;
            }
        }
        else if (logo is null && icon is not null && glyphInner < contentInner)
        {
            // A LABEL-LESS glyph skips the loop above, so it would otherwise keep the logo-sized
            // reservation. Same eased band, and the block is just the icon (no label to allow for).
            contentFit = Math.Clamp(((OuterRadius - glyphInner) - 12.0)
                                    / (IconSize * GlyphScale(n, cover)),
                                    0.40, 1.0);
        }

        // Terra armed card / kawaii armed lift / reactor armed fusion: run the content through a UNIFORM
        // lift+scale (sized at resting geometry above), so logo/icon/label track the armed slice 1:1 with
        // no per-shape re-fit.
        bool liftContent = terraLifted || kawaiiArmed || customLifted || reactorFused;
        if (liftContent)
        {
            double contentRad = midDeg * Math.PI / 180.0;
            // Reactor's armed glyph sits ReactorArmedGlyphInsetPx CLOSER to the hub than the swell's
            // midpoint — pulled inward, toward the fused keyhole's throat.
            double lift  = terraLifted ? TerraLiftPx : kawaiiArmed ? KawaiiLiftPx
                         : customLifted ? _custom!.Spec.Motion!.LiftPx
                         : ReactorSwellPx / 2.0 - ReactorArmedGlyphInsetPx;
            double north = terraLifted ? TerraNorthPx : kawaiiArmed ? KawaiiNorthPx
                         : customLifted ? _custom!.Spec.Motion!.NorthPx : 0.0;
            double lscale = terraLifted ? TerraArmedScale : kawaiiArmed ? KawaiiArmedScale
                          : customLifted ? _custom!.Spec.Motion!.Scale
                          : 1.35;   // reactor keeps its bigger armed pop
            // Reactor armed: add the circuit board's own parallax drift, sampled at the content centre, so
            // the glyph moves WITH the traces under it. Folded into this existing translate rather than
            // pushed separately — the pops for this block are unwound far below, and one more paired push
            // there is a bug waiting to happen. The wheel label is suppressed while fused, so this moves
            // the glyph/logo only.
            var (driftX, driftY) = reactorFused ? ReactorGlyphDrift(center, sc) : (0.0, 0.0);
            dc.PushTransform(new TranslateTransform(Math.Cos(contentRad) * lift + driftX,
                                                    Math.Sin(contentRad) * lift + north + driftY));
            dc.PushTransform(new ScaleTransform(lscale, lscale, sc.X, sc.Y));
        }

        // Scale the content (icon/label/logo) to fit the slice ring's thickness — centred on sc.
        bool fitScaled = contentFit < 0.999;
        if (fitScaled) dc.PushTransform(new ScaleTransform(contentFit, contentFit, sc.X, sc.Y));

        // EVERY material drops the shape-accurate dark shadow (an offset black copy of the same
        // silhouette) behind an ARMED slice's glyph/logo. Salvage keeps the shape shadow on ALL slices.
        // Reactor ARMED keeps it; reactor UNARMED uses a soft dark BLOB behind the content instead
        // (drawn below).
        bool armedShadow = armed || _material == "salvage";
        // Unarmed reactor without the hollow-rest treatment: a blurred blob backs the glyph on the
        // gradient wedge. With hollow rest the glyph gets a hard rimmed disc instead, and stacking a soft
        // blob under it just muddies the rim — so the blob only survives on the rollback path.
        bool reactorBlob = _material == "reactor" && !armed && !ReactorHollowRest;
        bool reactorDisc = _material == "reactor" && !armed && ReactorHollowRest;   // hollow rest → rimmed disc
        // Armed reactor: a subtle glow behind the white glyph in the slice's OWN colour — the only colour
        // on an otherwise monochrome armed slice.
        // No glow during the guarded dwell (B1) — the glyph earns its colour when the lock completes,
        // same gating as salvage's back-light. Non-guarded slices arm instantly (arming=false) and
        // glow immediately as before.
        bool reactorGlow = reactorFused && !deleting && !arming;
        // Terra opts OUT: the opaque icon rim paints AFTER the shadow and its dilation covers the
        // shadow's whole reach (rest offset 0 → ≤2px of tap spread; armed offset 3 → ≤1px sliver at half
        // alpha), so a silhouette shadow here would be masked draws producing zero visible pixels. If the
        // rim ever goes translucent or thin, reconsider.
        // Kawaii opts OUT of the generic silhouette shadow — it drops a shadow of the icon PLUS its white
        // rim instead (see KawaiiShadowBrush), on every slice.
        // Reactor's ARMED glyph drops nothing at all: it's an opaque white/grey silhouette on a
        // now-opaque dark plate, so a shadow adds nothing and the dwell's four quarters would each need
        // their own — this is the simpler read. Reactor UNARMED keeps the blurred blob.
        bool contentShadow = armedShadow && _material != "kawaii" && _material != "reactor"
                             && _material != "mesa";
        double shadowOffset  = 1.0;   // terra's 0/×3-lift offsets are absorbed by its own shadow instead
        double shadowOpacity = 1.0;
        // Salvage at REST: darker brush, longer throw, and the shadow always falls TOWARD THE WHEEL
        // CENTRE — 12 o'clock throws straight down, 6 o'clock straight up, and so on. Reads as one light
        // source out beyond the rim shining inward across the plate,
        // which is also the opposite of the armed cast shadow's outward throw, so arming flips the
        // lighting rather than just brightening. Armed salvage never reaches DrawShapeShadow (it uses the
        // back-light glow + cast shadow), so this is resting slices only.
        Brush?  shadowBrush = null;
        double? shadowRad   = null;
        if (_material == "salvage" && !armed)
        {
            shadowBrush  = SalvageRestShadowBrush;
            shadowOffset = 2.0;
            shadowRad    = (midDeg + 180.0) * Math.PI / 180.0;   // midDeg points outward; +180 = inward
        }

        // Reactor magnet-lock dwell: quarter separation eases IN (1-t³ — barely moving early, rushing at
        // the end), and for ~ReactorSnapShakeMs after the snap the whole glyph jitters at decaying
        // amplitude. Frames come from the render timer, which already invalidates while a reactor slice
        // is armed (the circuit sparks).
        double qSep = 0, shakeX = 0, shakeY = 0;
        if (reactorLock && arming)
        {
            double t = Math.Clamp(_sm.ConfirmProgress, 0.0, 1.0);
            qSep = ReactorLockSepPx * (1.0 - t * t * t);
        }
        else if (reactorLock && confirming)
        {
            double ms = (NowUtc - _reactorSnapUtc).TotalMilliseconds;
            if (ms >= 0 && ms < ReactorSnapShakeMs)
            {
                double u   = ms / ReactorSnapShakeMs;
                double amp = 2.4 * (1.0 - u);
                shakeX = Math.Sin(u * 43.0) * amp;         // ~7 x-wobbles across the window; y runs at a
                shakeY = Math.Cos(u * 59.0) * amp * 0.6;   // different rate so it reads as a rattle, not a spin
            }
        }
        // Draw one content pass through the dwell treatment: quartered while separating, jittered while
        // snapping, plain otherwise. Clips are the rect's own diagonal triangles pushed AFTER the
        // translate, so each clip rides with its piece — the glyph is cut along its own diagonals and the
        // pieces carry their cut edges as they move.
        void DrawGlyphPieces(Rect r, Action drawOnce)
        {
            if (qSep > 0.05)
            {
                // SWIRL: the offset is the N/E/S/W vector ROTATED, by an angle tied to the gap itself —
                // so the pieces spiral in rather than sliding straight, and the rotation necessarily
                // reaches 0 exactly when the gap does. The end state is therefore bit-identical to the
                // straight version (offset 0, no residual angle) with no separate unwind to keep in sync.
                double sw = ReactorSwirlDeg * (Math.PI / 180.0) * (qSep / ReactorLockSepPx);
                double cs = Math.Cos(sw), sn = Math.Sin(sw);
                for (int q = 0; q < ReactorLockPieces; q++)
                {
                    double vx = PieceDirs[q].dx * qSep, vy = PieceDirs[q].dy * qSep;
                    // Orbit only — the pieces do NOT spin about themselves, so they stay axis-aligned and
                    // meet perfectly; a self-rotation would need its own unwind to avoid a seam at 100%.
                    dc.PushTransform(new TranslateTransform(vx * cs - vy * sn, vx * sn + vy * cs));
                    dc.PushClip(PieceClip(r, q));
                    drawOnce();
                    dc.Pop(); dc.Pop();
                }
            }
            else if (shakeX != 0 || shakeY != 0)
            {
                dc.PushTransform(new TranslateTransform(shakeX, shakeY));
                drawOnce();
                dc.Pop();
            }
            else drawOnce();
        }
        // INNER icon/logo edge stroke — reactor only.
        bool iconEdge = _material is "reactor";
        // Kawaii gets a white OUTER rim (outside the silhouette — see DrawOuterEdged) plus a shadow of
        // that whole rimmed shape on EVERY slice, so it replaces the generic silhouette shadow.
        bool kawaiiIconEdge = _material == "kawaii";
        // Terra gets an OUTER rim too — the armed-outline burnt umber, and without Kawaii's rim-shadow
        // (the rim is opaque and wider than a silhouette shadow's reach would be, so terra drops its
        // silhouette shadow outright — see contentShadow above).
        bool terraIconEdge = _material == "mesa";
        // Thin thickness → half-opacity edge brush variant. Inner-stroke width = the inset the real icon
        // shrinks by.
        Brush edgeBrush = _thinRing ? ReactorEdgeBrushThin : ReactorEdgeBrush;
        double edgeInset = ReactorIconEdgeInset;
        // Terra + Gloss Light + Kawaii armed: lighten the icon/logo ~10% (a translucent white wash) so an
        // armed slice's content brightens as it lifts. Salvage armed: a HUE-PRESERVING lighten instead —
        // the glyph brightens without washing toward white/grey.
        // Reactor armed goes all the way: an OPAQUE wash, so the fused slice's glyph/logo reads as a solid
        // silhouette against the dark keyhole. WHITE once armed — but during the
        // magnet-lock dwell the quarters wash in the circuit traces' own grey, then flip to white on the
        // snap, so the glyph reads as part of the board until it locks in.
        // (Reactor keys off reactorFused, not `armed`, so the wash tracks the fused geometry exactly —
        // including the confirm dwell, where the slice is already fused but not yet armed.)
        bool armedLighten = (armed && !arming && _material is "mesa" or "pearl" or "kawaii" or "salvage")
                            || reactorFused
                            || (armed && !arming && _custom?.Spec.Glyph?.ArmedWash == true);
        // Custom theme glyph treatments — data-selected uses of the SAME generic helpers the styled
        // materials run (outer/inner rim via DrawOuterEdged/DrawInnerEdged, back-glow + radial cast
        // shadow via the Salvage helpers). Glow color "slice" follows the slice tint like Salvage;
        // strength scales the halo alphas. All mutually exclusive with the built-in flags above —
        // a custom token can never be "kawaii"/"mesa"/"salvage"/"reactor".
        var  customGlyph     = _custom?.Spec.Glyph;
        bool customOuterEdge = customGlyph?.Edge == "outer";
        bool customInnerEdge = customGlyph?.Edge == "inner";
        bool customGlow      = customGlyph is { GlowStrength: > 0 } && (armed || confirming) && !arming && !deleting;
        Brush? armedWash = reactorFused ? (reactorLock && arming ? ReactorDwellGlyphWash : Brushes.White) : null;
        // Salvage armed: back-light the glyph with a blurred, slice-tinted halo. Gated the same way as the
        // inner crescent — fully armed only, so the guarded dwell stays owned by the growing crescent.
        bool salvageIconGlow = _material == "salvage" && (armed || confirming) && !arming && !deleting;
        if (armedLighten && _material == "salvage")
        {
            // Overlay the SAME hue, lightened, as a partial-alpha wash — brightens without desaturating
            // toward white the way the flat IconLightenBrush would. Cached per colour (per-frame draw).
            var baseColor = ActionTint.DisplayColor(Slices[i], ActionTint.TintSetFor(_material));
            if (!SalvageWashCache.TryGetValue(baseColor, out armedWash))
            {
                var lit = ActionTint.Lighten(baseColor, 0.45);
                var b = new SolidColorBrush(Color.FromArgb(150, lit.R, lit.G, lit.B));
                b.Freeze();
                SalvageWashCache[baseColor] = armedWash = b;
            }
        }

        if (logo is not null && logo.Width > 0 && logo.Height > 0)
        {
            // Fit the logo INSIDE the actual wedge (annular sector) so it never bleeds past the slice's
            // angled sides or arcs: the largest aspect-preserving box whose bounds stay within the slice.
            // This depends on the slice's thickness, count AND angular orientation (an axis-aligned logo
            // rect fits a rotated wedge differently). Thin ring still shrinks game logos an extra 30%.
            const double MaxLogoWidth = 200.0;                                 // hard width cap (px), independent of the wedge
            // A logo slice is labelled when the mode asks for it ("All Slices", or a ticked slice under
            // "Slices I Choose") — the same SliceLabelRule every other slice answers to. It then shares the
            // wedge with its name the way a glyph does: the label's band comes OUT of the logo's height
            // budget, and the pair centres on the slice centre, so the block still fits what contentFit
            // (measured on the same natural block height) scales it into.
            FormattedText? logoLabelFt = showLabels && !suppressWedgeLabel && label.Length > 0
                ? MeasureLabel(label, ppd, fontSize, maxW, fontSize * 2.8, labelBrush ?? LabelBrush, labelFace)
                : null;
            double logoGap    = logoLabelFt is null ? 0.0 : (_thinRing ? 0.0 : 4.0);
            double logoLabelH = logoLabelFt?.Height ?? 0.0;
            double aspect = logo.Width / logo.Height;
            double maxH = (IconSize + 4 + fontSize * 1.3) * IconScaleFor(n);   // intended block height (as before)
            double tilt = _material == "salvage" ? SalvageTilt(i) : 0.0;   // see the tilt push below
            double fitH = FitLogoHeight(center, sc, midDeg, sliceDeg, aspect);   // largest that fits the RESTING wedge
            double dh = Math.Min(maxH, fitH) * _logoExtraScale;                // keep prior size; only shrink to fit
            dh = Math.Min(dh, MaxLogoWidth / aspect);                          // …and never wider than MaxLogoWidth
            // Hollow reactor: the disc is fixed, so the LOGO yields to it — otherwise a wide logo would
            // spill out of its circle while a narrow one floated in the middle of an identical one.
            if (reactorDisc) dh = Math.Min(dh, DiscFitHeight(aspect));
            // Floored at 45% so a two-line label on a crowded wheel shrinks the artwork rather than
            // erasing it — an unrecognisable logo is worse than a label that crowds one.
            if (logoLabelFt is not null) dh = Math.Max(dh * 0.45, dh - (logoGap + logoLabelH));
            double dw = dh * aspect;
            double logoBlockH = dh + logoGap + logoLabelH;
            double logoTop    = sc.Y - logoBlockH / 2;
            var rect  = new Rect(sc.X - dw / 2, logoTop, dw, dh);               // drawn at the SHIFTED centre
            // Salvage's crooked-panel tilt, about the LOGO's own centre — with a label under it the block
            // hangs below the slice centre, so turning about sc would swing the artwork sideways instead of
            // rotating it in place. Pushed around EVERYTHING that belongs to the logo — glow, cast shadow,
            // edge stroke, the logo itself — so they stay locked together; the label is outside it.
            bool tilted = Math.Abs(tilt) > 0.001;
            if (tilted) dc.PushTransform(new RotateTransform(tilt, sc.X, logoTop + dh / 2));
            if (reactorBlob) DrawReactorDropShadow(dc, logo, rect, geo);       // unarmed reactor: blurred drop shadow, clipped to the slice
            if (reactorDisc) DrawReactorRestDisc(dc, sc, reactorDiscR, contentFit, reactorDiscEditPen);   // hollow rest: rimmed black disc
            if (reactorGlow)                                                   // armed reactor: slice-coloured glow
                DrawSalvageIconGlow(dc, logo, rect, ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light),
                                    ReactorGlowCoreAlpha, ReactorGlowHaloAlpha, corePasses: 1);
            if (salvageIconGlow)                                               // armed salvage: tinted back-light
            {
                DrawSalvageIconGlow(dc, logo, rect, ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light));
                // …then the shadow it casts AWAY from the crescent, over the glow's outer edge.
                DrawSalvageCastShadow(dc, logo, rect, midDeg, geo);
            }
            if (customGlow)                                                    // custom theme: armed back-light
            {
                var glowTint = customGlyph!.GlowSliceTint
                    ? ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light) : _custom!.GlowColor;
                DrawSalvageIconGlow(dc, logo, rect, glowTint,
                                    SalvageGlowCoreAlpha * customGlyph.GlowStrength,
                                    SalvageGlowHaloAlpha * customGlyph.GlowStrength);
                if (customGlyph.CastShadow) DrawSalvageCastShadow(dc, logo, rect, midDeg, geo);
            }
            // The logo's alpha masks a solid tint fill → recolours any logo to the slice colour. Terra
            // uses its own in-between tint set (bright like Dark's, saturated like Light's). With an edge
            // material, the tinted logo draws inside DrawInnerEdged's erosion stack so a uniform rim of
            // stroke shows on every edge.
            var tintBrush = ActionTint.BrushFor(Slices[i], ActionTint.TintSetFor(_material));
            void DrawTintedLogo(Rect r)
            {
                dc.PushOpacityMask(BrushOf(logo));   // per-frame path — the frozen shared brush
                dc.DrawRectangle(tintBrush, null, r);
                dc.Pop();
            }
            if (kawaiiIconEdge)
            {
                DrawKawaiiContentShadow(dc, logo, rect, KawaiiIconEdgePx);          // shadow of logo + rim
                DrawOuterEdged(dc, logo, rect, KawaiiIconEdgeBrush, KawaiiIconEdgePx);
            }
            if (customOuterEdge)                                                    // custom theme: outer rim
            {
                if (customGlyph!.EdgeShadow) DrawKawaiiContentShadow(dc, logo, rect, customGlyph.EdgeWidth);
                DrawOuterEdged(dc, logo, rect, _custom!.EdgeBrush, customGlyph.EdgeWidth);
            }
            // Generic south-offset shadow, OUTSIDE the pieces pass — reactor's quartered dwell casts
            // none. (Salvage armed skips it too: it would fight the radial cast shadow; same for a
            // custom glow with its own cast.)
            if (contentShadow && !salvageIconGlow && !customGlow) DrawShapeShadow(dc, logo, rect, shadowOffset, shadowOpacity, shadowBrush, shadowRad);
            if (terraIconEdge)   // shadow of logo + rim, then the per-slice hue-matched rim over it
            {
                DrawTerraContentShadow(dc, logo, rect, TerraIconEdgePx);
                DrawOuterEdged(dc, logo, rect, TerraRimBrushFor(Slices[i]), TerraIconEdgePx);
            }
            DrawGlyphPieces(rect, () =>   // reactor dwell quarters/snap-shake; a plain single pass elsewhere
            {
                if (iconEdge)             DrawInnerEdged(dc, logo, rect, edgeBrush, edgeInset, DrawTintedLogo);
                else if (customInnerEdge) DrawInnerEdged(dc, logo, rect, _custom!.EdgeBrush, customGlyph!.EdgeWidth, DrawTintedLogo);
                else                      DrawTintedLogo(rect);
                if (armedLighten) DrawIconLighten(dc, logo, rect, armedWash);   // terra/gloss-light wash, salvage hue-lighten, or reactor solid white
            });
            if (tilted) dc.Pop();
            if (logoLabelFt is not null)   // outside the tilt: the name stays level under a crooked panel
                dc.DrawText(logoLabelFt, new Point(sc.X - logoLabelFt.MaxTextWidth / 2, logoTop + dh + logoGap));
        }
        else if (icon is not null)
        {
            // Centre the icon+label block at sc.Y so the label is always screen-down
            // from the icon regardless of which slice position we're in. Icons shrink on a crowded wheel.
            // With labels off the block is just the icon, so it centres on sc. The label was measured
            // up front (labelFt) with its geometry-aware width/height, so the block uses its REAL size.
            bool   showLabel = labelFt is not null;
            double iconSz  = IconSize * GlyphScale(n, cover);
            // Hollow reactor: keep the glyph inside its fixed disc (square, so aspect 1). contentFit
            // scales the whole block afterwards, so undo it here — otherwise a long two-line label would
            // shrink the glyph relative to a disc that never moves.
            if (reactorDisc) iconSz = Math.Min(iconSz, DiscFitHeight(1.0) / Math.Max(0.01, contentFit));
            double labelH  = labelFt?.Height ?? 0.0;
            double gap     = showLabel ? (_thinRing ? 0.0 : 4.0) : 0.0;   // thin: pull the glyph + label closer
            // Must match the measurement pass's terra clearance above (lgap), or contentFit is computed
            // for one block height and drawn at another.
            if (terraIconEdge && showLabel) gap = Math.Max(gap, TerraIconEdgePx + 1.0);
            double blockH  = iconSz + gap + labelH;
            double iconTop = sc.Y - blockH / 2;
            double labelCY = iconTop + iconSz + gap + labelH / 2;

            var iconRect = new Rect(sc.X - iconSz / 2, iconTop, iconSz, iconSz);
            // Salvage's crooked-panel tilt, about the ICON's own centre (not sc — the block hangs a label
            // below, so rotating about sc would swing the glyph sideways instead of turning it in place).
            // The label sits outside the push and stays level.
            double iconTilt = _material == "salvage" ? SalvageTilt(i) : 0.0;
            bool   iconTilted = Math.Abs(iconTilt) > 0.001;
            if (iconTilted)
                dc.PushTransform(new RotateTransform(iconTilt, iconRect.X + iconSz / 2, iconRect.Y + iconSz / 2));
            if (reactorBlob && !cover) DrawReactorDropShadow(dc, icon, iconRect, geo);   // unarmed reactor: blurred drop shadow, clipped to the slice
            if (reactorDisc) DrawReactorRestDisc(dc, sc, reactorDiscR, contentFit, reactorDiscEditPen);   // hollow rest: rimmed black disc
            if (reactorGlow && !cover)                                                   // armed reactor: slice-coloured glow
                DrawSalvageIconGlow(dc, icon, iconRect, ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light),
                                    ReactorGlowCoreAlpha, ReactorGlowHaloAlpha, corePasses: 1);
            if (salvageIconGlow && !cover)                                               // armed salvage: tinted back-light
            {
                DrawSalvageIconGlow(dc, icon, iconRect, ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light));
                DrawSalvageCastShadow(dc, icon, iconRect, midDeg, geo);   // …and its outward cast shadow
            }
            if (customGlow && !cover)                                                    // custom theme: armed back-light
            {
                var glowTint = customGlyph!.GlowSliceTint
                    ? ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light) : _custom!.GlowColor;
                DrawSalvageIconGlow(dc, icon, iconRect, glowTint,
                                    SalvageGlowCoreAlpha * customGlyph.GlowStrength,
                                    SalvageGlowHaloAlpha * customGlyph.GlowStrength);
                if (customGlyph.CastShadow) DrawSalvageCastShadow(dc, icon, iconRect, midDeg, geo);
            }
            if (kawaiiIconEdge && !cover)
            {
                DrawKawaiiContentShadow(dc, icon, iconRect, KawaiiIconEdgePx);       // shadow of glyph + rim
                DrawOuterEdged(dc, icon, iconRect, KawaiiIconEdgeBrush, KawaiiIconEdgePx);
            }
            if (customOuterEdge && !cover)                                           // custom theme: outer rim
            {
                if (customGlyph!.EdgeShadow) DrawKawaiiContentShadow(dc, icon, iconRect, customGlyph.EdgeWidth);
                DrawOuterEdged(dc, icon, iconRect, _custom!.EdgeBrush, customGlyph.EdgeWidth);
            }
            if (contentShadow && !cover && !salvageIconGlow && !customGlow) DrawShapeShadow(dc, icon, iconRect, shadowOffset, shadowOpacity, shadowBrush, shadowRad);
            // !cover: cover art is an opaque crop-to-fill rectangle, so a dilated silhouette would just
            // trace an umber box around the tile rather than outline any shape — and its drop shadow would
            // be a plain offset rectangle, so that's skipped with it.
            if (terraIconEdge && !cover)
            {
                DrawTerraContentShadow(dc, icon, iconRect, TerraIconEdgePx);
                DrawOuterEdged(dc, icon, iconRect, TerraRimBrushFor(Slices[i]), TerraIconEdgePx);
            }
            DrawGlyphPieces(iconRect, () =>   // reactor dwell quarters/snap-shake; a plain single pass elsewhere
            {
                if (cover)
                    // Cover art is portrait — crop-to-fill the icon square instead of stretching. (No edge.)
                    dc.DrawRectangle(CoverBrushOf(icon), null, iconRect);
                else if (iconEdge)
                    // Uniform inner edge stroke ON the glyph's edges (erosion-masked full-size draw).
                    DrawInnerEdged(dc, icon, iconRect, edgeBrush, edgeInset, r => dc.DrawImage(icon, r));
                else if (customInnerEdge)
                    DrawInnerEdged(dc, icon, iconRect, _custom!.EdgeBrush, customGlyph!.EdgeWidth, r => dc.DrawImage(icon, r));
                else
                    dc.DrawImage(icon, iconRect);
                if (armedLighten && !cover) DrawIconLighten(dc, icon, iconRect, armedWash);   // terra/gloss-light wash, salvage hue-lighten, or reactor solid white
            });
            if (iconTilted) dc.Pop();
            if (showLabel)
                dc.DrawText(labelFt!, new Point(sc.X - labelFt!.MaxTextWidth / 2, labelCY - labelH / 2));
        }
        else if (showLabels && !suppressWedgeLabel)   // no icon → label is the only content (kept even text-free would be blank)
        {
            DrawLabel(dc, label, sc, ppd, fontSize, maxW, labelBrush, labelFace);
        }

        if (fitScaled) dc.Pop();
        if (liftContent) { dc.Pop(); dc.Pop(); }   // the armed-slice-tracking content transform
        if (pushed) dc.Pop();
        if (scaled) dc.Pop();

        // Dwell progress ring (just outside the slice) fills 0→100% as the slice arms, then DISAPPEARS the
        // moment it completes — `arming` (confirming && !ConfirmDone) goes false at 100% and while firing.
        // Full opacity (drawn after the body Pop) so it stays readable while the slice body is faded.
        // (A material with its OWN dwell — the pours' rising fill, salvage's growing crescent — IS its own
        // progress cue, so it gets no arc; assign mode keeps the arc on every material.)
        if (arming && !ownDwell)
        {
            double ringR = OuterRadius + 12;
            Pen arcBg = ConfirmArcBg;
            Pen arcFg = ConfirmArcFg;
            dc.DrawGeometry(null, arcBg, BuildArc(center, ringR, startBnd, sliceDeg));
            if (_sm.ConfirmProgress > 0.001)
                dc.DrawGeometry(null, arcFg, BuildArc(center, ringR, startBnd, sliceDeg * _sm.ConfirmProgress));
        }

        // Edit-mode hold-□ delete dwell: a red progress arc winding around the slice as you hold □.
        if (editDeleting)
        {
            double ringR = OuterRadius + 12;
            dc.DrawGeometry(null, ConfirmArcBg, BuildArc(center, ringR, startBnd, sliceDeg));
            dc.DrawGeometry(null, ConfirmArcFg, BuildArc(center, ringR, startBnd, sliceDeg * Math.Clamp(_sm.DeleteProgress, 0, 1)));
        }

        // (Hold-to-delete spin-up is drawn in the centre layer, around the cursor dot — see OnRender.)
    }

    /// <summary>Salvage's rusted-metal grain plus both edge treatments — the recessed cell-by-cell lip and
    /// the corner rivets, all clipped to the INNER RIM ONLY so a slice reads as a plate lipped along its
    /// bottom edge rather than one outlined all the way round. <paramref name="salvageInnerOnly"/> also
    /// traces the rest of the perimeter in the lip's colour when the shared wedge pen was suppressed for
    /// it (see <c>DrawSlice</c>'s <c>salvageInnerOnly</c>).</summary>
    private void DrawSalvageEdgeDetail(DrawingContext dc, Geometry geo, Point center, int i, int n,
        double startBnd, double endBnd, bool armed, bool confirming, bool arming,
        SalvageSliceGeo salv, bool salvageInnerOnly)
    {
        dc.PushClip(geo);
        // Armed slices take the exposure-lifted plate (SalvageArmedRustOpacity). Gated the same way as
        // salvageIconGlow — the guarded DWELL is excluded, since it deliberately sweeps over the resting
        // charcoal and brightening mid-dwell would pre-empt the arrival the crescent is building to.
        dc.DrawGeometry(SalvageTexture(center, (armed || confirming) && !arming), null, geo);
        // The recessed lip, stroked CELL BY CELL so its thickness can vary along the rim. Each cell is
        // a straight chord at one radius — the same chord the wedge boundary uses — so a plain
        // DrawLine per cell traces the slice's own edge exactly. Still clipped to the wedge, so each
        // pen's inward half is discarded and only the half inside the slice shows.
        var cells = salv.Cells;
        for (int c = 0; c + 1 < cells.Length; c += 2)
        {
            unchecked
            {
                uint h = (uint)((c / 2) * 374761393 + i * 668265263) ^ 0x3F19u;   // own salt: thickness
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;                     // uncorrelated with
                dc.DrawLine(SalvageLipPens[h % SalvageLipLevels], cells[c], cells[c + 1]);   // width/depth
            }
        }
        // Corner rivets last, so they sit ON the rust and over the inner lip — fastened THROUGH the
        // plate rather than buried under its surface. Still inside the wedge clip.
        DrawSalvageRivets(dc, center, _sliceInner, OuterRadius, startBnd, endBnd, SliceGapPx, i, n);
        dc.Pop();
        if (salvageInnerOnly)
        {
            dc.DrawGeometry(null, SalvagePen, salv.InnerEdge);
            // …and the rest of the perimeter (both radial sides + the outer arc) in the lip's colour,
            // so the plate is edged all the way round. Same params as the wedge, so it traces the
            // slice's own stepped edge; unclipped like the inner stroke, so the pen sits centred on it.
            dc.DrawGeometry(null, SalvageOuterEdgePen, salv.OuterEdge);
        }
    }

    /// <summary>The pours' guarded dwell (kawaii + terra): the armed colour rises from the inner edge and
    /// fires at the brim, clipped to the wedge. Kawaii's strawberry milk carries a sloshing surface line;
    /// terra's yellow keeps a hard cut-paper edge.</summary>
    private void DrawPourDwell(DrawingContext dc, Geometry geo, Pen? pen, Point center, double midDeg,
        double sliceDeg, bool kawaiiMilk)
    {
        if (_sm.ConfirmProgress > 0.001)
        {
            // Every material's fill hits the brim exactly when the dwell completes, so the milk arriving
            // and the slice arming read as one event.
            double fillProgress = Math.Clamp(_sm.ConfirmProgress, 0.0, 1.0);
            double pourR = _sliceInner + fillProgress * (OuterRadius - _sliceInner);
            dc.PushClip(geo);
            if (kawaiiMilk)
            {
                // D1: the milk's surface SLOSHES — two octaves of animated sine ride the fill radius
                // (a slow broad swell + a faster counter-running ripple), cartoony rather than
                // physical. The fill is a centre fan under that wavy surface; the white line strokes
                // the surface polyline itself. Amplitude eases in so the first drop isn't wavy.
                var (fillGeo, surfGeo) = BuildKawaiiSlosh(center, pourR, midDeg, sliceDeg,
                                                          Math.Clamp(_sm.ConfirmProgress * 4.0, 0.0, 1.0));
                dc.DrawGeometry(KawaiiMilkFill, null, fillGeo);
                dc.DrawGeometry(null, KawaiiMilkEdgePen, surfGeo);
            }
            else
            {
                dc.DrawEllipse(TerraArmedFill, null, center, pourR, pourR);   // terra: hard cut-paper edge
            }
            dc.Pop();
        }
        dc.DrawGeometry(null, pen, geo);   // outline last — the pour reads as contained by it
    }

    /// <summary>Kawaii armed cue #2: a pastel-rainbow arc hugging the armed wedge's outer edge, plus the
    /// one-shot glitter sweep the moment the slice arms. Drawn INSIDE the caller's lift transform so it
    /// rides out with the slice.</summary>
    private void DrawKawaiiArmedRim(DrawingContext dc, Point center, double midDeg, double sliceDeg, int i)
    {
        // D4: tiny white sparkles washing leading-edge → trailing-edge. The caller already excludes the
        // dwell, so a guarded slice glitters only when its fill completes.
        if (_kawaiiGlitterIndex != i) { _kawaiiGlitterIndex = i; _kawaiiGlitterUtc = NowUtc; }
        if (!_reduceMotion) DrawKawaiiGlitter(dc, center, midDeg, sliceDeg, i);   // decorative sweep — docs/MOTION-INVENTORY.md
        double rimSpan = sliceDeg * 0.86;
        dc.DrawGeometry(null, KawaiiRimPen, BuildArc(center, OuterRadius - 5, midDeg - rimSpan / 2.0, rimSpan));
    }

    /// <summary>Salvage's armed cue: a thick white inner-arc crescent spanning the middle of the wedge, once
    /// armed (or confirm-done). On a GUARDED slice it doubles as the dwell: the crescent starts as a dot at
    /// the slice's centre and grows toward both inner corners, reaching full span exactly as the slice
    /// arms — so the finished state is identical to a normal arm, no separate "completed" visual.</summary>
    private void DrawSalvageCrescent(DrawingContext dc, Point center, int i, double midDeg, double sliceDeg,
        bool salvageGrowing)
    {
        double crescentR    = _sliceInner + 12;   // inset a little further from the inner edge (was +7)
        double crescentSpan = sliceDeg * 0.80;   // 80% of the inner edge
        // Growing: ease the span open from a short "dot" so there's something visible at t=0. The arc
        // stays centred on midDeg, so it opens symmetrically toward both corners.
        // …and brighten 80% → 100% across the dwell, so it also gains intensity as it fills.
        bool growFade = false;
        if (salvageGrowing)
        {
            double t = Math.Clamp(_sm.ConfirmProgress, 0.0, 1.0);
            double dotDeg = Math.Min(crescentSpan, 4.0);        // the seed dot's angular width
            crescentSpan = dotDeg + (crescentSpan - dotDeg) * t;
            growFade = true;
            dc.PushOpacity(0.80 + 0.20 * t);
        }
        double crescentStart = midDeg - crescentSpan / 2.0;
        var crescentArc = BuildArc(center, crescentR, crescentStart, crescentSpan);
        // Neon glow under the white line, tinted to THIS slice's colour: many concentric round-capped
        // strokes, wide+faint outside → narrow+near-white inside, for a soft gaussian-ish falloff.
        // The glow occasionally flickers slightly — a cheap-fluorescent "uneven power" dip, slow
        // pseudo-random amplitude that mostly sits at 1.0. The core stroke is outside this opacity push,
        // so the tube itself never goes dark.
        var glowTint = ActionTint.DisplayColor(Slices[i], ActionTint.TintSet.Light);
        dc.PushOpacity(SalvageGlowFlicker());
        foreach (var p in SalvageGlowPens(glowTint))
            dc.DrawGeometry(null, p, crescentArc);
        dc.Pop();
        dc.DrawGeometry(null, SalvageCrescentPen, crescentArc);   // crisp white tube core on top
        // Faint gray tube noise near the ENDS of the crescent, fading to nothing at the centre — the
        // instability lives where a worn tube's electrodes are. Short dark dashes at hashed positions
        // whose opacity scales with distance-from-centre squared (smooth falloff).
        DrawSalvageTubeNoise(dc, center, crescentR, crescentStart, crescentSpan, i);
        if (growFade) dc.Pop();
    }

    // Shrink slice icons as the wheel gets crowded: 10% per slice starting at 9, floored at 60% of start.
    private static double IconScaleFor(int n) => n <= 8 ? 1.0 : Math.Max(0.60, 1.0 - 0.10 * (n - 8));

    // Labels start shrinking one slice earlier (at 8): 8% per slice, floored at 70% of start.
    private static double LabelScaleFor(int n) => n <= 7 ? 1.0 : Math.Max(0.70, 1.0 - 0.08 * (n - 7));

    // Width of the arc chord at radius r spanning one slice — label must fit inside this.
    private static double ChordWidth(double r, double sliceDeg) =>
        2.0 * r * Math.Sin(sliceDeg / 2.0 * Math.PI / 180.0);

    private static void DrawLabel(DrawingContext dc, string text, Point center,
        double ppd, double fontSize, double maxWidth, Brush? brush = null, Typeface? face = null)
    {
        var ft = MeasureLabel(text, ppd, fontSize, maxWidth, fontSize * 2.8, brush ?? LabelBrush, face ?? LabelFace);
        dc.DrawText(ft, new Point(center.X - maxWidth / 2, center.Y - ft.Height / 2));
    }

    /// <summary>Measure a slice label: wrap to <paramref name="maxWidth"/>, THEN scale the font down until
    /// the wrapped text fits both the width and the <paramref name="maxHeight"/> budget — a long unbreakable
    /// token (e.g. "Join/Leave") would otherwise overflow the slice edge, and a 3-line label would spill
    /// below. Floored at 8px so it stays legible; below the floor the text hard-clips with an ellipsis
    /// rather than spilling. Short labels fit at full size on the first try. <paramref name="face"/> is the
    /// per-material typeface (see <see cref="LabelFaceFor"/>) — defaults to <see cref="LabelFace"/>.</summary>
    // Fitted-label cache: the shrink-to-fit loop below builds a FormattedText per 0.75px step, and the
    // caller runs the whole measurement TWICE per labelled slice per frame — while its inputs only change
    // on layout/label edits. FormattedText is safe to re-draw across frames. Capped, cleared wholesale.
    private static readonly Dictionary<(string text, double ppd, double fs, double w, double h, Brush b, Typeface? f),
        FormattedText> LabelMeasureCache = new();

    private static FormattedText MeasureLabel(string text, double ppd, double fontSize,
        double maxWidth, double maxHeight, Brush brsh, Typeface? face = null)
    {
        var cacheKey = (text, ppd, fontSize, maxWidth, maxHeight, brsh, face);
        if (LabelMeasureCache.TryGetValue(cacheKey, out var cachedFt)) return cachedFt;
        var tf = face ?? LabelFace;
        FormattedText Make(double size) =>
            new(text, CultureInfo.InvariantCulture, WpfFlow.LeftToRight, tf, size, brsh, ppd)
            { MaxTextWidth = maxWidth, TextAlignment = TextAlignment.Center,
              // Tighten the inter-line gap on wrapped labels (~1.33× em natural → 1.05×): lines sit close
              // without touching (cap-to-descender clearance stays positive for Segoe UI).
              LineHeight = size * 1.05 };

        double fs = fontSize;
        var ft = Make(fs);
        while ((ft.Width > maxWidth + 0.5 || ft.Height > maxHeight + 0.5) && fs > 8.0)
            ft = Make(fs -= 0.75);
        if (ft.Height > maxHeight + 0.5)   // even at the floor it doesn't fit → clip, never spill
        {
            ft.MaxTextHeight = Math.Max(maxHeight, fs * 1.3);
            ft.Trimming      = TextTrimming.CharacterEllipsis;
        }
        if (LabelMeasureCache.Count > 512) LabelMeasureCache.Clear();
        LabelMeasureCache[cacheKey] = ft;
        return ft;
    }

    /// <summary>Whether the hub has a reason to be on screen this frame — the volume scrubber, a toggle-state
    /// readout, a notice, edit mode, reactor's armed caption, or simply a material that always shows it.
    /// Split out of <see cref="DrawHubContent"/> so the appear/disappear animation can gate on it.</summary>
    private bool HubDemanded()
    {
        if (ScrubberActive || HubGlyph is not null) return true;
        if (!HideCenter) return true;
        if (_statusLine2 is not null || _centerNotice is not null || _sm.EditMode) return true;
        // An armed arcade slice always shows the hub, on every material and whatever the setting says — see
        // ArmedIsArcade for what it's held open for.
        if (ArmedIsArcade) return true;
        int armedIdx = _sm.ArmedIndex;
        return _material == "reactor" && !_sm.EditMode
            && armedIdx >= 0 && armedIdx < Slices.Count
            && !string.IsNullOrWhiteSpace(Slices[armedIdx].Label);
    }

    private void DrawCenter(DrawingContext dc, Point center, double ppd)
    {
        // Presence was advanced once at the top of RenderCore (terra's backdrop needs the same value).
        bool demanded = HubDemanded();
        double scale = _hubFrameScale;
        if (!_hubFrameDraw) { _hubSnapshot = null; return; }

        // While the hub is on screen it's captured into a replayable Drawing. The moment its REASON goes
        // away we keep replaying that last capture at a shrinking scale — so the hub collapses complete
        // with whatever it was captioning, instead of the text vanishing and a bare disc shrinking after
        // it. (Re-deriving the content during the shrink isn't possible: the state that produced it —
        // _statusLine2, EditMode, ScrubberActive — is already gone by then.)
        if (demanded)
        {
            var dg = new DrawingGroup();
            using (var hc = dg.Open()) DrawHubContent(hc, center, ppd);
            _hubSnapshot = dg;   // deliberately NOT frozen: ShapeFill can hand back bounds-derived brushes
        }
        if (_hubSnapshot is null) return;

        // Salvage fades its hub in and out instead of scaling it — a rigid stepped-edge slab visibly growing
        // reads as a glitch. Same durations: `scale` is applied as alpha, the geometry stays full size. Under
        // Reduce Motion every material fades, unless "Always show hub" is on, in which case the material's
        // own treatment stands.
        bool hubFade = _material == "salvage" || (_reduceMotion && !_alwaysShowHub);
        bool scaled = !hubFade && scale < 0.999;
        bool faded  = hubFade && scale < 0.999;
        if (scaled) dc.PushTransform(new ScaleTransform(scale, scale, center.X, center.Y));
        if (faded)  dc.PushOpacity(scale);
        dc.DrawDrawing(_hubSnapshot);
        if (scaled) dc.Pop();
        if (faded)  dc.Pop();
    }

    /// <summary>Uniform 10% shrink applied to everything drawn inside the hub (text, chips, spacing, the
    /// volume ring, the battery glyph) on the thick ring, which has the smallest centre opening while hub
    /// content is sized off the constant <see cref="InnerRadius"/>. Reactor opts out: its hub is a composed
    /// node (etching + armed fusion + caption) tuned as one piece. The disc itself is never scaled.</summary>
    private double HubContentScale => _thickRing && _material != "reactor" ? 0.90 : 1.0;

    /// <summary>Heading-pill font multiplier for the thinner rings, whose centre holes are far larger (hub
    /// r ≈ 174 thin, ≈ 122 medium, ≈ 71 thick) while hub type is sized off the constant
    /// <see cref="InnerRadius"/>. Thin's 1.6 brings a 15px header near the Practice badge's 30px; medium's 1.3
    /// is that carried back in proportion to its hole. Applies to the hub toast headings only — the status
    /// readout's title and the notice/banner header — not the Practice badge (already 2×), its subtitle, the
    /// state row, or the edit hub's header (its hint rows are laid out in absolute px against that header's
    /// height, so scaling it alone would crowd them).</summary>
    private double HubHeadingScale => _thinRing ? 1.6 : _mediumRing ? 1.3 : 1.0;

    /// <summary>Push <see cref="HubContentScale"/> about the hub centre; returns true if a push happened
    /// (pop it). No-op at 1.0 so the non-thick path adds no transform.</summary>
    private bool PushHubContentScale(DrawingContext dc, Point center)
    {
        double s = HubContentScale;
        if (s > 0.999) return false;
        dc.PushTransform(new ScaleTransform(s, s, center.X, center.Y));
        return true;
    }

    private void DrawHubContent(DrawingContext dc, Point center, double ppd)
    {
        if (ScrubberActive || HubGlyph is not null)
        {
            // Keep the normal material hub — don't darken it during a volume change.
            double hubR = HubRadius;
            var hubBounds = new Rect(center.X - hubR, center.Y - hubR, hubR * 2, hubR * 2);
            // Reactor: the hub always reads as the dark node (near-black fill + white rim), with fine
            // etched contour rings, instead of the material's normal (near-transparent) ShapeFill.
            Brush scrubHubFill = _material == "reactor" ? ReactorArmedFill : ShapeFill(hubBounds, center);
            Pen?  scrubHubPen  = _material == "reactor" ? ReactorPen
                               : _material == "kawaii" ? KawaiiHubPen   // soft orchid rim on the cloud hub
                               : CenterRingPen;
            DrawHubDisc(dc, center, hubR, scrubHubFill, scrubHubPen);
            if (_material == "reactor") DrawReactorHubEtching(dc, center, hubR);
            bool scrubScaled = PushHubContentScale(dc, center);   // thick ring: ring + readout 10% smaller

            var fg   = DarkMaterial ? ScrubFgDark   : ScrubFgLight;
            var bg   = DarkMaterial ? ScrubBgDark   : ScrubBgLight;
            var text = DarkMaterial ? ScrubTextDark : ScrubTextLight;

            if (HubGlyph is { Length: > 0 } big)
            {
                // Track skip: one large glyph, no ring and no percentage — there's no level to report.
                // Reactor inks it white: the shared blue scrub ink reads as part of its circuit board.
                Brush glyphInk = _material == "reactor" ? Brushes.White : text;
                if (_hubGlyphName != big || !ReferenceEquals(_hubGlyphInk, glyphInk))
                {
                    _hubGlyph     = PackIconHelper.FromName(big, glyphInk);
                    _hubGlyphName = big;
                    _hubGlyphInk  = glyphInk;
                }
                if (_hubGlyph is not null)
                {
                    // 78% of the hub radius, clear of the rim.
                    double bsz = hubR * 1.2 * 0.65;
                    dc.DrawImage(_hubGlyph, new Rect(center.X - bsz / 2, center.Y - bsz / 2, bsz, bsz));
                }
                if (scrubScaled) dc.Pop();
                return;
            }

            // Volume ring: background (full circle) + foreground (partial arc)
            double ringR = InnerRadius - 10;   // 44 — sits inside the center circle
            dc.DrawEllipse(null, bg, center, ringR, ringR);
            if (ScrubberLevel > 0.005f)
            {
                var arc = BuildArc(center, ringR, -90, ScrubberLevel * 360);
                dc.DrawGeometry(null, fg, arc);
            }

            // Percentage text — rebuilt only when the value/ink/DPI actually change, not per frame
            // (the scrubber invalidates continuously while it's up).
            int pctVal = (int)Math.Round(ScrubberLevel * 100);
            if (_pctFt is null || _pctVal != pctVal || !ReferenceEquals(_pctInk, text) || _pctPpd != ppd)
            {
                _pctFt = new FormattedText($"{pctVal}%",
                    CultureInfo.InvariantCulture,
                    WpfFlow.LeftToRight,
                    HubFace, 19, text, ppd);
                _pctVal = pctVal; _pctInk = text; _pctPpd = ppd;
            }
            var ft = _pctFt;
            // With a glyph under it, lift the percentage so the pair straddles the hub centre.
            const double glyphSz = 36;   // readable from the couch
            double pctTop = center.Y - ft.Height / 2 - (ScrubberGlyph is null ? 0 : (glyphSz + 2) / 2.0);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2, pctTop));
            if (ScrubberGlyph is { Length: > 0 } gname)
            {
                if (_scrubGlyphName != gname || !ReferenceEquals(_scrubGlyphInk, text))
                {
                    _scrubGlyph     = PackIconHelper.FromName(gname, text);
                    _scrubGlyphName = gname;
                    _scrubGlyphInk  = text;
                }
                if (_scrubGlyph is not null)
                    dc.DrawImage(_scrubGlyph, new Rect(center.X - glyphSz / 2, pctTop + ft.Height + 2,
                                                       glyphSz, glyphSz));
            }
            if (scrubScaled) dc.Pop();
        }
        else
        {
            // "Hide wheel centers": draw the hub ONLY when it carries information — a toggle-state readout
            // (_statusLine2), a notice box (_centerNotice), or edit mode. A battery-only or idle
            // centre draws nothing (the volume scrubber is the branch above, so it's unaffected).
            bool hasInfo = _statusLine2 is not null || _centerNotice is not null || _sm.EditMode;
            // Reactor: when the hub would otherwise be empty, show the armed slice's label in it — the armed
            // slice fuses into the hub, so its centre is free to caption it.
            int  armedIdx = _sm.ArmedIndex;
            bool reactorArmedLabel = _material == "reactor" && !_sm.EditMode && !hasInfo
                && armedIdx >= 0 && armedIdx < Slices.Count
                && !string.IsNullOrWhiteSpace(Slices[armedIdx].Label);
            // (Visibility itself is decided by HubDemanded before this runs — see DrawCenter.)

            // The practice pill and the post-onboarding edit hint are standing badges, not readouts: they
            // float in an empty centre with no hub disc behind them. Every other hub content earns the disc.
            bool drawHubDisc = !DiscFreeNotice;

            // The hub always carries the slice MATERIAL (so it doesn't go flat in edit mode); a blue
            // outline ring (ArmedPen) is the at-a-glance edit-mode cue.
            bool edit = _sm.EditMode && _statusLine2 is null;
            double hubR = HubRadius;
            var hubBounds = new Rect(center.X - hubR, center.Y - hubR, hubR * 2, hubR * 2);
            if (drawHubDisc)
            {
                // Reactor: while a slice is armed the hub is baked into the fused keyhole geometry
                // (BuildReactorFusion) — drawing the disc again would stroke a rim straight across the
                // fusion's open throat. Unarmed, the hub matches the unarmed slices (faint smoke + thin
                // white rim), except under hub text (`hasInfo`), which takes the armed hub's near-black
                // fill so the text doesn't sit on a near-transparent disc.
                bool reactorFusedNow = _material == "reactor" && !_sm.EditMode && _sm.ArmedIndex >= 0;
                if (!reactorFusedNow)
                {
                    bool reactorBoardedUnarmed = _material == "reactor" && hasInfo;
                    Brush hubFill = reactorBoardedUnarmed ? ReactorArmedFill
                                  : _material == "reactor" ? ReactorRestFill : ShapeFill(hubBounds, center);
                    Pen?  hubPen  = _material == "reactor" ? ReactorRestPen   // matches the resting wedges' dimmed rim
                                  : edit ? ArmedPen
                                  : _material == "kawaii" ? KawaiiHubPen   // soft orchid rim on the cloud hub
                                  : SlicePen;
                    DrawHubDisc(dc, center, hubR, hubFill, hubPen);
                    if (_material == "reactor") DrawReactorHubEtching(dc, center, hubR);
                    if (_material == "kawaii") DrawHubTwinkle(dc, center, hubR);   // rare ambient glint on the cloud rim
                }
            }
            // Everything from here down is hub CONTENT — thick shrinks it 10% about the hub centre (the disc
            // above is deliberately outside the transform). The edit-mode SLICE toast is excluded too: it
            // rides on the focused slice, and a hub-centred scale would drag it off its wedge.
            bool contentScaled = PushHubContentScale(dc, center);
            bool sliceToast = false;
            if (_statusLine2 is not null)
                DrawStatus(dc, center, ppd);
            else if (_centerNotice is not null)
                DrawNotice(dc, center, ppd);
            else if (_sm.EditMode)
            {
                DrawEditHub(dc, center, ppd);
                // H5: the slice-DEPENDENT prompts (Move/Remove) ride on the focused slice itself.
                sliceToast = _sm.Phase == WheelStateMachine.EditPhase.Selecting;
            }
            else if (ArmedIsArcade)
            {
                // Ahead of the reactor label: on that material an armed arcade slice fuses into the hub, and
                // a still of the game (or the cabinet, for the launcher) says more than its own name does.
                DrawArcadePreview(dc, center, ArmedArcadeGameId);
            }
            else if (reactorArmedLabel)
            {
                // Centred armed-slice label on the fused hub node (light ink; wraps to the hub width).
                // Independent of "Label slices": this is the hub's readout of what's armed, and the fused
                // wedge has no text of its own. Gating it on the mode would empty the fused hub under
                // "none" (and, via HubDemanded, make the hub blink out per slice).
                var lf = new FormattedText(Loc.DefaultLabel(Slices[armedIdx].Label), CultureInfo.InvariantCulture,
                    WpfFlow.LeftToRight, HubFace, 23, InkLabel, ppd)   // matches the status readout's short-state size
                    { MaxTextWidth = hubR * 1.7, MaxTextHeight = hubR * 1.6,
                      TextAlignment = TextAlignment.Center, Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(lf, new Point(center.X - lf.MaxTextWidth / 2, center.Y - lf.Height / 2));
            }

            // ⚠ The battery is NOT one of the content branches above — it owns its own reserved spot just
            // above the hub's bottom edge, clear of the centred readout, so it rides ALONGSIDE whatever
            // the hub is saying. It must not be an `else if` on that chain: the resting hub is hidden
            // unless something gives it a reason to appear, so "hub is up" and "hub has nothing to say"
            // are very nearly exclusive, and chaining it meant the glyph was unreachable in exactly the
            // cases the hub actually shows — a toggle's state change, a notice naming what it acts on.
            //
            // Suppressed only where it would collide or have nothing behind it:
            //   • edit mode — the legend's rows fill the hub down to that spot
            //   • arcade preview — the game still fills the hub
            //   • DiscFreeNotice — a standing badge floats with no hub disc under it
            if (_batteryIcon is not null && drawHubDisc && !_sm.EditMode && ArmedArcadeGameId is null)
            {
                // Anchored a fixed distance above the hub's BOTTOM edge so it reads "near the bottom" at
                // every thickness (the hub grows large on medium/thin). Kawaii lifts it further: its hub
                // is a CLOUD, so the bottom edge is a scalloped silhouette rather than the circle `hubR`
                // describes, and the glyph sat in one of the scallops' dips.
                double sz = 26 * _batteryScale;
                double bottomGap = 10 + (_material == "kawaii" ? KawaiiBatteryLiftPx : 0);
                dc.DrawImage(_batteryIcon, new Rect(center.X - sz / 2, center.Y + hubR - sz - bottomGap, sz, sz));
            }
            if (contentScaled) dc.Pop();
            if (sliceToast) DrawEditSliceToast(dc, center, ppd);   // unscaled — it belongs to the slice
        }
    }

    /// <summary>Largest height for an aspect-locked, axis-aligned logo box centred at <paramref name="sc"/>
    /// that stays fully inside this slice's wedge — bounded by the inner/outer arcs, the two angled radial
    /// edges, and the inter-slice gap. Binary search on height; the box's corners + edge midpoints must all
    /// fall within [innerR+pad, outerR−pad] radius and within the (gap-adjusted) angular half-width.</summary>
    private double FitLogoHeight(Point center, Point sc, double midDeg, double sliceDeg, double aspect,
                                 double outerR = OuterRadius)
    {
        double edgePad = _thinRing ? 2.0 : 5.0;                       // px clearance off the arcs (tighter on thin)
        // Salvage: keep logos clear of the armed crescent, matching DrawSlice's contentInner shift.
        double ri = (_material == "salvage" ? _sliceInner + 16 : _sliceInner) + edgePad;
        double ro = outerR - edgePad;
        // Leave the inter-slice gap (a fixed pixel gap → its widest angle is at the inner radius).
        double gapHalfDeg = Math.Asin(Math.Min(1.0, (SliceGapPx / 2.0) / Math.Max(1.0, _sliceInner))) * (180.0 / Math.PI);
        double halfAng = Math.Max(2.0, sliceDeg / 2.0 - gapHalfDeg - 1.0);
        if (_logoFitCache.TryGetValue((sc.X, sc.Y, midDeg, sliceDeg, aspect, outerR, ri), out var cachedFit))
            return cachedFit;

        bool Inside(double px, double py)
        {
            double dx = px - center.X, dy = py - center.Y;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r < ri || r > ro) return false;
            double ang = Math.Atan2(dy, dx) * (180.0 / Math.PI);
            double d = ((ang - midDeg) % 360 + 540) % 360 - 180;      // signed angular distance from the slice centre
            return Math.Abs(d) <= halfAng;
        }
        // NOTE: the fit is computed on the UPRIGHT box. Salvage's tilt deliberately does NOT feed back
        // into it — refitting shrinks tilted logos noticeably, and a few px of overhang on a
        // scavenged-panel material is the cheaper trade.
        bool Fits(double h)
        {
            double hw = h * aspect / 2.0, hh = h / 2.0;
            return Inside(sc.X - hw, sc.Y - hh) && Inside(sc.X + hw, sc.Y - hh)
                && Inside(sc.X - hw, sc.Y + hh) && Inside(sc.X + hw, sc.Y + hh)
                && Inside(sc.X, sc.Y - hh) && Inside(sc.X, sc.Y + hh)          // edge midpoints catch the arcs
                && Inside(sc.X - hw, sc.Y) && Inside(sc.X + hw, sc.Y);
        }

        double lo = 0, hi = ro - ri;          // can't be taller than the band depth
        for (int k = 0; k < 16; k++) { double mid = (lo + hi) / 2.0; if (Fits(mid)) lo = mid; else hi = mid; }
        if (_logoFitCache.Count > 256) _logoFitCache.Clear();
        _logoFitCache[(sc.X, sc.Y, midDeg, sliceDeg, aspect, outerR, ri)] = lo;
        return lo;
    }

    // FitLogoHeight memo: the bisection runs 16 × 8 point tests (trig + sqrt each) per logo slice per
    // FRAME, on inputs that only change with the layout. `ri` folds in _sliceInner/_thinRing/material.
    private readonly Dictionary<(double scx, double scy, double mid, double deg, double aspect,
        double outer, double ri), double> _logoFitCache = new();

    /// <summary>One piece of an edit-hub hint line: literal text, or a controller-button prompt.
    /// <paramref name="Dim"/> draws the segment at reduced opacity — a control that EXISTS but is
    /// unavailable right now (the "△ Full" prompt on a wheel at capacity), which reads as unavailable
    /// rather than leaving the user wondering where the Add prompt went.</summary>
    // ⚠ Layout never rides inside the words: a segment is a bare word or a bare glyph, DrawHintRow puts the
    // glyph↔word space in, and Gap is the wide gutter between prompt pairs. A translator sees "Add", not " Add   ".
    /// <summary>The edit-mode legend words, resolved once per run — the legend is drawn every frame while
    /// editing, so the lookup must not sit in the draw path (see docs/LOCALIZATION.md §3).</summary>
    private static class Leg
    {
        public static readonly string Placing = Loc.T(UiText.Overlay.Placing), Moving = Loc.T(UiText.Overlay.Moving),
            Aim = Loc.T(UiText.Overlay.Aim), Place = Loc.T(UiText.Overlay.Place), Cancel = Loc.T(UiText.Overlay.Cancel),
            Add = Loc.T(UiText.Overlay.Add), Move = Loc.T(UiText.Overlay.Move), Remove = Loc.T(UiText.Overlay.Remove),
            Back = Loc.T(UiText.Overlay.Back), Choose = Loc.T(UiText.Overlay.Choose), Done = Loc.T(UiText.Overlay.Done),
            Edit = Loc.T(UiText.Overlay.Edit), Undo = Loc.T(UiText.Overlay.Undo), Redo = Loc.T(UiText.Overlay.Redo);
    }

    private readonly record struct HintSeg(string? Text, PadButton? Btn, bool Dim = false, bool Gutter = false)
    {
        public static HintSeg T(string t) => new(t, null);
        public static HintSeg B(PadButton b) => new(null, b);
        public static HintSeg TDim(string t) => new(t, null, Dim: true);
        public static HintSeg BDim(PadButton b) => new(null, b, Dim: true);
        public static HintSeg Gap => new(null, null, Gutter: true);
    }

    /// <summary>Opacity for a <see cref="HintSeg.Dim"/> segment.</summary>
    private const double HintDimOpacity = 0.40;

    /// <summary>The Selecting-phase legend (the resting edit state). Its row count varies: L1 Undo and R1
    /// Redo are independent prompts, each drawn only when that direction has history, and with both empty
    /// the row is omitted. DrawEditHub measures its block from the row count, and DrawHintRow centres each
    /// row on its own width, so a dropped row or a lone Redo still lays out cleanly.</summary>
    private HintSeg[][] EditLegendLines()
    {
        var rows = new List<HintSeg[]>(3);
        rows.Add([HintSeg.T(Leg.Edit)]);

        // At capacity "△ Add" becomes a dimmed "△ Full", so the limit reads as a limit.
        rows.Add(_sm.SliceCount >= EditMaxSlices
            ? [HintSeg.BDim(PadButton.Triangle), HintSeg.TDim("Full"), HintSeg.Gap,
               HintSeg.B(PadButton.Circle), HintSeg.T(Leg.Done)]
            : [HintSeg.B(PadButton.Triangle), HintSeg.T(Leg.Add), HintSeg.Gap,
               HintSeg.B(PadButton.Circle), HintSeg.T(Leg.Done)]);

        var hist = new List<HintSeg>(5);
        if (_sm.CanUndo) { hist.Add(HintSeg.B(PadButton.L1)); hist.Add(HintSeg.T(Leg.Undo)); }
        if (_sm.CanRedo)
        {
            if (hist.Count > 0) hist.Add(HintSeg.Gap);
            hist.Add(HintSeg.B(PadButton.R1)); hist.Add(HintSeg.T(Leg.Redo));
        }
        if (hist.Count > 0) rows.Add(hist.ToArray());

        return rows.ToArray();
    }

    /// <summary>Contextual control legend shown in the hub while editing (changes with the sub-state).
    /// Button prompts (✕ ○ □ △ L1 R1) render as geometric glossy-dark buttons inline with the text.</summary>
    private void DrawEditHub(DrawingContext dc, Point center, double ppd)
    {
        HintSeg[][] lines = _sm.Phase switch
        {
            // Title reads "Add" when the carry is a new slice (game assign / Add picker), "Moving" when
            // it's a re-position of an existing one.
            WheelStateMachine.EditPhase.Carrying =>
            [
                // "Placing" (H1): the slice is chosen; what's happening now is choosing WHERE.
                [HintSeg.T(_sm.CarryIsAdd ? Leg.Placing : Leg.Moving)],
                [HintSeg.T(Leg.Aim), HintSeg.B(PadButton.Cross), HintSeg.T(Leg.Place)],
                [HintSeg.B(PadButton.Circle), HintSeg.T(Leg.Cancel)],
            ],
            WheelStateMachine.EditPhase.Picking =>
            [
                [HintSeg.T(Leg.Add)],
                [HintSeg.B(PadButton.Cross), HintSeg.T(Leg.Choose)],
                [HintSeg.B(PadButton.Circle), HintSeg.T(Leg.Back)],
            ],
            // The hub carries only the slice-INDEPENDENT controls (H5) — Move/Remove ride on the
            // focused slice itself as a toast (DrawEditSliceToast). Built in EditLegendLines because its
            // row count varies with undo/redo availability.
            _ => EditLegendLines(),
        };

        const double titleSz = 18.2, hintSz = 12.5;   // ALL-CAPS header chip (26 → −30%); hints a touch smaller so
        double hintH = hintSz * 1.4;                   // the control rows don't overflow the hub circle
        var titleFt = HubTitleText(lines[0][0].Text!.ToUpperInvariant(), titleSz, ppd);
        double titleH = titleFt.Height + HubChipPadY * 2;   // the chip's height
        double total = titleH + 2;
        for (int k = 1; k < lines.Length; k++) total += hintH + 2;

        // Nudge the whole block up ~10% of its height so the big title chip doesn't leave dead headroom at
        // the top of the hub circle.
        double y = center.Y - total / 2.0 - total * 0.1;
        // Edit's header hangs level on salvage: the banner stays put while the focus moves from slice to
        // slice, so a per-slice tilt would swing it on every step.
        DrawHubChip(dc, titleFt, center.X, y, allowSalvageTilt: false);
        // Kawaii's ribbon and Salvage's banner carry extra visual weight, so the legend sits further below
        // them than the stock 2px.
        double headerGap = _material == "kawaii" ? 15 : _material == "salvage" ? 10 : 2;
        y += titleH + headerGap;
        for (int k = 1; k < lines.Length; k++)
        {
            DrawHintRow(dc, lines[k], center.X, y, hintH, hintSz, InkHint, ppd);
            y += hintH + 2;
        }
    }

    /// <summary>The width one hint line will draw at, measured with exactly the rules
    /// <see cref="DrawHintRow"/> lays it out by — text runs, inline buttons, prompt-pair gutters and the
    /// glyph↔word gap. ⚠ Every caller that needs a hint row's width goes through here: a second copy of
    /// these rules once forgot the gutter segment and dereferenced a gap's missing button, taking the app
    /// down from the render pass. <paramref name="fts"/>, when supplied, receives the shaped text runs so the
    /// draw pass needn't shape them twice.</summary>
    private double MeasureHintRow(HintSeg[] segs, double fontSize, Brush ink, double ppd, FormattedText?[]? fts = null)
    {
        double bh = fontSize * 1.2;   // inline button height (slightly taller than the text)
        double glyphGap = fontSize * 0.30, gutter = fontSize * 1.1;   // glyph↔word, and between prompt pairs
        double total = 0;
        for (int i = 0; i < segs.Length; i++)
        {
            if (segs[i].Text is { } t)
            {
                var ft = new FormattedText(t, CultureInfo.InvariantCulture, WpfFlow.LeftToRight, HubFace, fontSize, ink, ppd);
                if (fts is not null) fts[i] = ft;
                total += ft.WidthIncludingTrailingWhitespace;
            }
            else if (segs[i].Gutter) total += gutter;
            else if (segs[i].Btn is { } b) total += ControllerButtons.Width(b, bh);
            if (i > 0 && !segs[i].Gutter && !segs[i - 1].Gutter) total += glyphGap;   // between a glyph and its word
        }
        return total;
    }

    /// <summary>Lay out one hint line — text runs + inline controller-button prompts — centred at
    /// <paramref name="centerX"/>, vertically centred in a <paramref name="rowH"/>-tall row.</summary>
    private void DrawHintRow(DrawingContext dc, HintSeg[] segs, double centerX, double y, double rowH,
                             double fontSize, Brush ink, double ppd)
    {
        double bh = fontSize * 1.2;
        double glyphGap = fontSize * 0.30, gutter = fontSize * 1.1;
        var fts = new FormattedText?[segs.Length];
        double total = MeasureHintRow(segs, fontSize, ink, ppd, fts);
        double x = centerX - total / 2.0;
        for (int i = 0; i < segs.Length; i++)
        {
            if (segs[i].Gutter) { x += gutter; continue; }
            if (i > 0 && !segs[i - 1].Gutter) x += glyphGap;
            bool dim = segs[i].Dim;
            if (dim) dc.PushOpacity(HintDimOpacity);
            if (fts[i] is { } ft)
            {
                dc.DrawText(ft, new Point(x, y + (rowH - ft.Height) / 2));
                x += ft.WidthIncludingTrailingWhitespace;
            }
            else if (segs[i].Btn is { } b)
            {
                double bw = ControllerButtons.Width(b, bh);
                ControllerButtons.Draw(dc, new Rect(x, y + (rowH - bh) / 2, bw, bh), b, ppd);
                x += bw;
            }
            if (dim) dc.Pop();
        }
    }

    // H5: the Move/Remove toast that rides ON the focused slice (the hub keeps only the
    // slice-independent controls). Fixed dark chip + light ink so it reads on every material.
    private static readonly Brush EditToastBg  = Frozen(new SolidColorBrush(Color.FromArgb(0xE2, 0x1C, 0x1E, 0x28)));
    private static readonly Pen   EditToastPen = FrozenPen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)), 1.0);
    private static readonly Brush EditToastInk = Frozen(new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF4)));

    /// <summary>Small "✕ Move  □ Remove" chip centred on the currently-focused slice in edit mode.</summary>
    private void DrawEditSliceToast(DrawingContext dc, Point center, double ppd)
    {
        int i = _sm.ArmedIndex, n = Math.Max(1, _sm.SliceCount);
        if (i < 0 || i >= n) return;
        double rad = (360.0 / n * i - 90) * Math.PI / 180.0;
        double r = (InnerRadius + OuterRadius) / 2.0;
        var p = new Point(center.X + Math.Cos(rad) * r, center.Y + Math.Sin(rad) * r);

        HintSeg[] segs =
            [HintSeg.B(PadButton.Cross), HintSeg.T(Leg.Move), HintSeg.Gap, HintSeg.B(PadButton.Square), HintSeg.T(Leg.Remove)];
        const double fontSize = 12.0;
        double rowH = fontSize * 1.5;
        double total = MeasureHintRow(segs, fontSize, EditToastInk, ppd);

        var chip = new Rect(p.X - total / 2 - 9, p.Y - rowH / 2 - 4, total + 18, rowH + 8);
        dc.DrawRoundedRectangle(EditToastBg, EditToastPen, chip, 7, 7);
        DrawHintRow(dc, segs, p.X, chip.Y + 4, rowH, fontSize, EditToastInk, ppd);
    }

    /// <summary>Draw a just-deleted slice fading + shrinking out at the slot it occupied.</summary>
    private void DrawGhost(DrawingContext dc, Point center)
    {
        int gn = Math.Max(1, _sm.GhostCount);
        double sliceDeg = 360.0 / gn;
        double mid = _sm.GhostCenterDeg;
        double fade = Math.Clamp(_sm.GhostFade, 0, 1);
        double scale = 0.7 + 0.3 * fade;                 // shrink toward 0.7 as it fades
        double midR = (_sliceInner + OuterRadius) / 2.0;
        var sc = Polar(center, midR, mid);
        dc.PushTransform(new ScaleTransform(scale, scale, sc.X, sc.Y));
        dc.PushOpacity(fade);
        var geo = BuildWedge(center, _sliceInner, OuterRadius, mid - sliceDeg / 2.0, mid + sliceDeg / 2.0);
        dc.DrawGeometry(SliceFill, DeleteOutlinePen, geo);
        dc.Pop();
        dc.Pop();
    }

    private static double EaseOut(double t) { t = Math.Clamp(t, 0, 1); double u = 1 - t; return 1 - u * u * u; }

    /// <summary>Interpolate between two angles (degrees) along the shortest direction.</summary>
    private static double LerpAngle(double a, double b, double t)
    {
        double d = ((b - a + 540) % 360) - 180;
        return a + d * t;
    }

    /// <summary>Draw the toggle-state readout (label over state) centred in the deadzone.</summary>
    // The "current → target" arrow glyph, rebuilt only when the ink changes (the readout redraws per frame).
    private ImageSource? _statusArrow;
    private Brush?       _statusArrowInk;

    /// <summary>Side of the optional logo above a post-fire readout (see <see cref="ShowStatus"/>).</summary>
    private const double StatusIconSize = 30;

    /// <summary>Extra size for the storefront cold-start toast at every thickness, scoped to that one
    /// readout — its icon, title and caption — so the notice/banner headings, which share
    /// <see cref="HubHeadingScale"/>, don't move with it. It is a cue over an otherwise empty screen, so it
    /// carries further than the readouts on a populated wheel.</summary>
    private const double ColdStartToastBoost = 1.20;

    /// <summary>The store logo's multiplier on top of that. The logo does the identifying and, unlike type,
    /// has no legibility floor, so it takes a much bigger multiplier than the text under it.</summary>
    private const double ColdStartIconBoost = 2.625;

    /// <summary>Caption size on top of the toast multiplier. The call site first halves the caption relative
    /// to a state word (<c>/= 2.0</c>); read the two together.</summary>
    private const double ColdStartCaptionBoost = 1.125;

    /// <summary>How much of the logo the storefront name's chip rides over, bottom-up — a badge rather than a
    /// stack. Measured against the logo's visible bottom edge, and the chip's art is what overlaps, so on
    /// Salvage (whose cloth banner rises above its pill rect) the rise is added back.</summary>
    private const double ColdStartChipOverlap = 0.25;

    // Salvage's cold-start badge only: title and caption share one cloth, so they are balanced against each
    // other here — the store's name takes the emphasis, the caption gives some back, and the lines close up
    // into one plate of text. The Practice badge, which shares the drawing code, is untouched.
    private const double SalvageColdStartTitleBoost  = 1.15;
    private const double SalvageColdStartCaptionTrim = 0.90;
    private const double SalvageColdStartLineGap     = 0.6;   // px, against HubChipLineGap's 2
    /// <summary>Give back part of <see cref="SalvageBannerTwoLineLift"/>'s 5px for this badge only: with the
    /// logo overlapping the cloth's top, the pair sits higher on the visible cloth than the Practice badge
    /// does, so it needs less of the anti-sag lift. Net lift here is 3px.</summary>
    private const double SalvageColdStartTextDrop    = 2.0;

    private void DrawStatus(DrawingContext dc, Point center, double ppd)
    {
        // Fit inside the real centre hole, which grows as the ring thins — not the constant InnerRadius.
        double maxW = (HubHoleRadius - 8) * 2.0;
        string state   = _statusLine2 ?? "";
        bool   hasNext = _statusLine2Next is not null;

        // The title (label) sits in a contrasting L1/R1-style chip above the state value. Kawaii needs a
        // wider gap: its ribbon's white outer rim extends 6px BEYOND the chip rect, so the default 3px left
        // the rim overlapping the state row.
        double gap = _material == "kawaii" ? 12.0 : 3.0;
        // …and its pill is taller than the shared padding implies (KawaiiPillPadExtra), which this height
        // estimate has to match or the whole block centres off.
        double pillExtra = _material == "kawaii" ? KawaiiPillPadExtra : 0.0;
        // One multiplier for the whole storefront cold-start toast — icon, title and caption together, so the
        // block keeps its proportions at every thickness. An icon is what identifies that readout: it's the
        // only one that carries one. Stays 1.0 everywhere else, which matters because it also scales the
        // state row, and HubHeadingScale deliberately does NOT apply there.
        double toast      = _statusIcon is null ? 1.0 : HubHeadingScale * ColdStartToastBoost;
        // Titles are the one thing HubHeadingScale has always applied to, icon or not — so a plain readout
        // keeps exactly what it had, and only the cold-start toast takes the extra boost on top.
        double titleScale = _statusIcon is null ? HubHeadingScale : toast;
        var    titleFt = string.IsNullOrEmpty(_statusLine1) ? null : HubTitleText(_statusLine1!, 15 * titleScale, ppd);
        // The chip's padding grows with its text, or a bumped title sits in a pill sized for the old one. The
        // estimate here and the padScale passed to DrawHubChip below have to stay in step.
        double titleH  = titleFt is null ? 0 : titleFt.Height + (HubChipPadY * titleScale + pillExtra) * 2 + gap;

        // Post-fire readout: no target, so just the resulting state at the larger size. Short state words
        // render big; a long one (e.g. an audio device's friendly name) shrinks and may wrap, ellipsized
        // rather than clipped mid-glyph.
        if (!hasNext)
        {
            bool   longText = state.Length > 14;
            double sz       = longText ? 15 : 23;
            // Storefront cold-start hub ("Steam" + logo over "Launching") — the only status readout that
            // carries an icon. Its second line is a caption under the store's name, not a state word, so it
            // starts at half size; everything in this toast then rides the per-thickness multiplier, with the
            // caption and the logo each carrying their own boost on top.
            if (_statusIcon is not null) sz /= 2.0;
            sz *= toast * (_statusIcon is null ? 1.0 : ColdStartCaptionBoost);
            double iconSize = StatusIconSize * toast * ColdStartIconBoost;
            var only = new FormattedText(state, CultureInfo.InvariantCulture, WpfFlow.LeftToRight,
                HubFace, sz, InkState, ppd)
                { MaxTextWidth = maxW, MaxTextHeight = (longText ? 44 : 32) * toast,
                  TextAlignment = TextAlignment.Center, Trimming = TextTrimming.CharacterEllipsis };
            // An optional logo (storefront cold-start) sits above the label chip and joins the same vertical
            // centring. The chip rides over the logo's bottom quarter (ColdStartChipOverlap), so the advance
            // from the logo's top to the chip's top is three quarters of the logo — plus, on Salvage, the
            // cloth's rise above its pill box (see HubChipRiseAbove), or the banner would swallow more.
            // Salvage only: the caption goes inside the cloth rather than under it — the banner is far wider
            // than the one word it carries. This reuses the Practice badge's two-line path, so there stays
            // exactly one cloth-banner text path.
            if (_material == "salvage" && titleFt is not null && _statusIcon is not null
                && !string.IsNullOrEmpty(state))
            {
                // Both lines are re-measured for this badge: the title bigger, the caption smaller. `only` is
                // no use for the caption anyway — it's centred and stretched to maxW, so its Width is the whole
                // hub and would size the banner to match. Tight, and in the chip's ink, exactly like the title.
                var    bigFt  = HubTitleText(_statusLine1!, 15 * titleScale * SalvageColdStartTitleBoost, ppd);
                var    capFt  = HubTitleText(state, sz * SalvageColdStartCaptionTrim, ppd);
                double blockH = bigFt.Height + SalvageColdStartLineGap + capFt.Height;
                double chipH2 = blockH + HubChipPadY * titleScale * 2;
                // The two-line badge draws its lines at 1.0, not SalvageBannerTextScale — measure the rise
                // the cloth will actually have.
                double rise2  = HubChipRiseAbove(Math.Max(bigFt.Width, capFt.Width), blockH, chipH2, 1.0);
                double iconH2 = iconSize * (1.0 - ColdStartChipOverlap) + rise2;
                double top2   = center.Y - (iconH2 + chipH2) / 2.0;
                dc.DrawImage(_statusIcon, new Rect(center.X - iconSize / 2.0, top2, iconSize, iconSize));
                DrawTwoLineHubChip(dc, bigFt, capFt, center.X, top2 + iconH2 + chipH2 / 2.0, titleScale,
                                   lineGap: SalvageColdStartLineGap,
                                   bannerTextDy: SalvageColdStartTextDrop);
                return;
            }

            // Optional caption under the state line (onboarding practice: "<slice>" over "was selected").
            // Deliberately well under the state's size — the slice's own name stays what you read first —
            // and it joins the same vertical centring so the block doesn't ride high in the hub.
            const double captionGap = 2.0;
            var capUnder = string.IsNullOrEmpty(_statusCaption) ? null
                : new FormattedText(_statusCaption!, CultureInfo.InvariantCulture, WpfFlow.LeftToRight,
                    HubFace, Math.Max(12.0, sz * 0.6), InkState, ppd)
                  { MaxTextWidth = maxW, TextAlignment = TextAlignment.Center,
                    Trimming = TextTrimming.CharacterEllipsis };
            double capUnderH = capUnder is null ? 0 : capUnder.Height + captionGap;

            double chipRise = titleFt is null ? 0 : HubChipRiseAbove(titleFt);
            double iconH = _statusIcon is null ? 0 : iconSize * (1.0 - ColdStartChipOverlap) + chipRise;
            double yOnly = center.Y - (iconH + titleH + only.Height + capUnderH) / 2.0;
            var iconAt = new Rect(center.X - iconSize / 2.0, yOnly, iconSize, iconSize);
            // Logo first, so the chip lands on top of it: drawn last, the overlapping logo would cover the
            // store's name.
            if (_statusIcon is not null) dc.DrawImage(_statusIcon, iconAt);
            if (_statusIcon is not null) yOnly += iconH;
            if (titleFt is not null) yOnly += DrawHubChip(dc, titleFt, center.X, yOnly, padScale: titleScale) + gap;
            dc.DrawText(only, new Point(center.X - maxW / 2.0, yOnly));
            if (capUnder is not null)
                dc.DrawText(capUnder, new Point(center.X - maxW / 2.0, yOnly + only.Height + captionGap));
            return;
        }

        // Armed preview — "current → target", laid out as three pieces so each can be styled: the current
        // state in the target's ink at reduced opacity (so it picks up the hub colour instead of reading as
        // flat grey), a bold arrow glyph, and the target on a rounded chip in the material's armed colour.
        const double sz2 = 19, padX = 8, padY = 3, sp = 7;
        var curFt = new FormattedText(state, CultureInfo.InvariantCulture, WpfFlow.LeftToRight,
            HubFace, sz2, InkState, ppd) { Trimming = TextTrimming.CharacterEllipsis };
        var tgtFt = new FormattedText(_statusLine2Next!, CultureInfo.InvariantCulture, WpfFlow.LeftToRight,
            HubFace, sz2, InkState, ppd) { Trimming = TextTrimming.CharacterEllipsis };
        // The arrow carries the material's own accent where one is defined; everywhere else it matches the
        // CURRENT state's treatment — same ink, same 40% fade (applied below) — so the row reads as
        // "faded current + arrow" pointing at the solid target, rather than a third styling.
        Brush arrowInk = _material == "kawaii" ? KawaiiArrowBrush
                       : _material == "mesa"   ? TerraBlobFill      // the terracotta backdrop tone
                       : InkState;
        bool  fadeArrow = _material is not ("kawaii" or "mesa");   // accented materials stay full-strength
        if (!ReferenceEquals(_statusArrowInk, arrowInk))
        {
            _statusArrow    = PackIconHelper.FromName("ArrowRightBoldCircle", arrowInk);
            _statusArrowInk = arrowInk;
        }

        double iconSz = sz2 * 1.15;
        double chipW  = tgtFt.Width + padX * 2, chipH = tgtFt.Height + padY * 2;
        double totalW = curFt.Width + sp + iconSz + sp + chipW;
        double lineH  = Math.Max(chipH, Math.Max(curFt.Height, iconSz));

        double y = center.Y - (titleH + lineH) / 2.0;
        if (titleFt is not null) y += DrawHubChip(dc, titleFt, center.X, y, padScale: titleScale) + gap;

        // Scale the whole row down rather than wrapping it — a broken "current → target" reads as garbage.
        double s  = Math.Min(1.0, maxW / Math.Max(1.0, totalW));
        double cy = y + lineH / 2.0;
        if (s < 0.999) dc.PushTransform(new ScaleTransform(s, s, center.X, cy));

        double x = center.X - totalW / 2.0;
        dc.PushOpacity(0.4);                                        // current state = the target's ink, faded
        dc.DrawText(curFt, new Point(x, cy - curFt.Height / 2.0));
        dc.Pop();
        x += curFt.Width + sp;
        if (fadeArrow) dc.PushOpacity(0.4);   // …matching the current state exactly (see arrowInk above)
        if (_statusArrow is not null)
            dc.DrawImage(_statusArrow, new Rect(x, cy - iconSz / 2.0, iconSz, iconSz));
        else
            dc.DrawText(new FormattedText("›", CultureInfo.InvariantCulture, WpfFlow.LeftToRight,   // glyph missing
                HubFace, sz2, InkState, ppd), new Point(x, cy - curFt.Height / 2.0));
        if (fadeArrow) dc.Pop();
        x += iconSz + sp;
        var chipRect = new Rect(x, cy - chipH / 2.0, chipW, chipH);
        dc.DrawRoundedRectangle(ArmedFillFor(), null, chipRect, chipH * 0.35, chipH * 0.35);   // fill only — no rim
        dc.DrawText(tgtFt, new Point(x + padX, cy - tgtFt.Height / 2.0));

        if (s < 0.999) dc.Pop();
    }

    /// <summary>Draw the "Configure in Settings" hub notice on the same L1/R1-style button chip as the
    /// armed-slice status title, centred in the hub. The "Practice" badge is oversized (2× text and chip
    /// padding) so a practice wheel can't be mistaken for a live one — it may overlap the hub's edge — and
    /// carries a smaller "Actions are Disabled" line inside the same pill (see
    /// <see cref="DrawTwoLineHubChip"/>). It floats with no hub disc under it (<see cref="DiscFreeNotice"/>).</summary>
    private void DrawNotice(DrawingContext dc, Point center, double ppd)
    {
        if (PracticeNotice)
        {
            // "Practice|<subtitle>" overrides the default subtitle (the Settings-open practice mode).
            int bar = _centerNotice!.IndexOf('|');
            string sub = bar >= 0 ? _centerNotice[(bar + 1)..] : Loc.T(UiText.Overlay.ActionsDisabled);
            const double scale = 2.0;   // 30px title, chip padding scaled to match
            var titleFt = HubTitleText(Loc.T(UiText.Overlay.Practice), 15 * scale, ppd);
            titleFt.MaxTextWidth = 600;   // don't wrap to the hub width — overlapping the edge is fine
            var subFt = HubTitleText(sub, 14, ppd);
            subFt.MaxTextWidth = 600;
            // Salvage: the Practice banner hangs level. It stays up through the whole practice step, so a
            // per-slice tilt would swing it as slices arm under it — the same pin the edit header takes.
            DrawTwoLineHubChip(dc, titleFt, subFt, center.X, center.Y, scale, allowSalvageTilt: false);
            // Onboarding practice step: the which-stick art (analog-left for the left wheel, analog-right for
            // the right) sits above the pill; like the pill it may overlap the hub's edge.
            if (_oobePracticeArt && PracticeStickArt(IsWheelB) is { } art)
            {
                double extra = _material == "kawaii" ? KawaiiPillPadExtra : 0.0;
                double chipH = titleFt.Height + HubChipLineGap + subFt.Height + (HubChipPadY * scale + extra) * 2;
                const double artSize = 64.8;
                dc.DrawImage(art, new Rect(center.X - artSize / 2, center.Y - chipH / 2 - 6 - artSize,
                                           artSize, artSize));
            }
            return;
        }
        var ft = HubTitleText(_centerNotice ?? "", 15 * HubHeadingScale, ppd);
        // Height estimate must include kawaii's extra pill padding, or the notice sits off-centre.
        double noticePad = HubChipPadY + (_material == "kawaii" ? KawaiiPillPadExtra : 0.0);
        DrawHubChip(dc, ft, center.X, center.Y - (ft.Height + noticePad * 2) / 2.0);
    }

    /// <summary>Two-line variant of <see cref="DrawHubChip"/>: a title line over a smaller sub-line,
    /// both inside one pill sized to fit both (used by the onboarding practice badge — "Practice" over
    /// "Actions are Disabled"). Mirrors DrawHubChip's fill/pill styling exactly, just taller.
    /// <para><paramref name="allowSalvageTilt"/> false pins the salvage banner LEVEL, as DrawHubChip's same
    /// parameter does for the edit header — a persistent badge must not swing as slices are armed under it.</para></summary>
    private void DrawTwoLineHubChip(DrawingContext dc, FormattedText titleFt, FormattedText subFt,
                                     double centerX, double centerY, double padScale,
                                     bool allowSalvageTilt = true, double lineGap = HubChipLineGap,
                                     double bannerTextDy = 0.0)
    {
        double extra = _material == "kawaii" ? KawaiiPillPadExtra : 0.0;   // cushion inside the ribbon's layered edge
        double padX = HubChipPadX * padScale + extra, padY = HubChipPadY * padScale + extra;
        double chipW = Math.Max(titleFt.Width, subFt.Width) + padX * 2;
        double chipH = titleFt.Height + lineGap + subFt.Height + padY * 2;
        var rect = new Rect(centerX - chipW / 2, centerY - chipH / 2, chipW, chipH);
        if (_material == "salvage")
        {
            // As in DrawHubChip: an oversized bottom-anchored cloth banner, both lines centred on the visible
            // cloth as one block. No extra text scaling here — this badge is already drawn oversized.
            double blockH = titleFt.Height + lineGap + subFt.Height;
            var cloth = SalvageBannerRect(rect, Math.Max(titleFt.Width, subFt.Width), blockH);
            bool tilted = allowSalvageTilt && PushSalvageBannerTilt(dc, cloth);   // cloth + both lines rotate together
            DrawSalvageBanner(dc, cloth, chipH * 0.32);
            // bannerTextDy shifts BOTH lines together, so the pair keeps its own spacing and only its seat on
            // the cloth moves — callers that overlap the banner's top need less of the anti-sag lift.
            double lift = SalvageBannerTwoLineLift - bannerTextDy;
            DrawSalvageBannerText(dc, titleFt, cloth, 1.0, blockH, -lift);
            DrawSalvageBannerText(dc, subFt,   cloth, 1.0, blockH, titleFt.Height + lineGap - lift);
            if (tilted) dc.Pop();
            return;
        }
        if (_material == "obsidian")
            ControllerButtons.DrawPill(dc, rect);   // glossy-dark physical button (its rim is already 1px)
        else if (_material == "kawaii")
            DrawRibbonBanner(dc, rect, chipH * 0.32);   // ribbon banner (shallower notch — this pill is tall)
        else
            dc.DrawRoundedRectangle(ArmedFillFor(), HubChipRim, rect, chipH * 0.32, chipH * 0.32);   // material's armed fill
        double y = rect.Top + padY;
        dc.DrawText(titleFt, new Point(centerX - titleFt.Width / 2, y));
        y += titleFt.Height + lineGap;
        dc.DrawText(subFt, new Point(centerX - subFt.Width / 2, y));
    }

    // (No stick-dot drawing — see the centre layer of OnRender.)

}
