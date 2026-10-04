using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

// Reactor material: the fused hub+wedge keyhole silhouette an armed slice draws instead of a separate
// wedge (see DrawSlice's reactorFused branch in RadialMenuControl.cs, which calls into this).
public sealed partial class RadialMenuControl
{
    /// <summary>The smallest outer-rim fillet <see cref="BuildFusedKeyhole"/> will accept — if even this
    /// can't fit the caller falls back to the widen-based rounding route.</summary>
    private const double RimFilletMinPx = 10.0;
    /// <summary>Fallback radius for the concave hub↔side junction fillets — used only when the wheel-level
    /// rest-disc radius (the usual junction radius, so the join's curve matches the unarmed glyph discs)
    /// isn't available.</summary>
    private const double FusionJunctionR = 10.0;

    /// <summary>The whole reactor fused keyhole as ONE analytic closed path — hub circle, concave
    /// hub↔side junction fillets, straight sides, and two big convex rim fillets grown as far as the
    /// shape allows (tangent point halfway down the visible side, or the pair meeting mid-rim, whichever
    /// binds first). Everything is closed-form circle/line tangencies plus one bisection — deliberately
    /// NO CombinedGeometry union and NO RoundCorners: the widen pass those run notches large-radius arcs
    /// at some orientations (the mid-rim "divot", divots on the fillet arcs at certain slice angles).
    /// Cheap enough that edit-mode reflow builds it per frame uncached. Returns null when the fillet
    /// can't fit (degenerate span) — the caller falls back to the union+RoundCorners route.</summary>
    private static Geometry? BuildFusedKeyhole(
        Point center, double hubR, double outer, double startBnd, double endBnd, double gapPx,
        double junctionR)
    {
        double span = endBnd - startBnd;
        if (span >= 359.9 || span <= 0) return null;

        // Work in each side's own frame: x along the boundary's radial line, y across the gap toward the
        // slice. The side runs along y = half (BuildWedge's perpendicular half-gap offset); a rim fillet
        // of radius r has its centre at (t, half + r) with t² = (outer - r)² - (half + r)². t shrinks and
        // the fillet's angular reach grows monotonically with r → bisect for the largest fit.
        double half = gapPx / 2.0;
        double midR    = (hubR + outer) / 2.0;        // halfway down the visible side
        double rimGapDeg = Math.Max(0.25, 2.0 / outer * (180 / Math.PI));   // tiny true-rim arc survives
        double maxTurn = span / 2.0 - rimGapDeg / 2.0;

        static bool Solve(double r, double outer, double half, out double t, out double turnDeg)
        {
            double a = outer - r, b = half + r;
            t = 0; turnDeg = 180;
            if (a <= b) return false;
            t = Math.Sqrt(a * a - b * b);
            turnDeg = Math.Atan2(b, t) * (180 / Math.PI);
            return true;
        }
        bool Fits(double r) =>
            Solve(r, outer, half, out double t, out double turn)
            && Math.Sqrt(t * t + half * half) >= midR && turn <= maxTurn;

        double lo = RimFilletMinPx;
        if (!Fits(lo)) return null;
        double hi = (outer - half) / 2.0;             // r beyond this leaves no room for the fillet at all
        for (int k = 0; k < 24; k++) { double m = (lo + hi) / 2; if (Fits(m)) lo = m; else hi = m; }
        Solve(lo, outer, half, out double tan, out double turnAt);
        double sideR  = Math.Sqrt(tan * tan + half * half);   // where the fillet leaves the straight side
        double siSide = Math.Asin(Math.Min(1.0, half / sideR)) * (180 / Math.PI);

        // Concave junction fillet: circle of radius ρ tangent to the side line from the gap side (centre
        // at y = half − ρ) and externally tangent to the hub circle (centre distance hubR + ρ). ρ matches
        // the unarmed glyph discs' radius (passed in), so the join's curve is the same arc as those rims.
        double rho = junctionR;
        double cy = half - rho;
        double cx2 = (hubR + rho) * (hubR + rho) - cy * cy;
        if (cx2 <= 0) return null;                            // fillet circle can't touch the side line
        double cx = Math.Sqrt(cx2);
        double jLineR   = Math.Sqrt(cx * cx + half * half);   // fillet's tangent point ON the side line
        double jLineOff = Math.Atan2(half, cx) * (180 / Math.PI);
        // The fillet must leave a real straight run on the side (its tangent point below the rim fillet's)
        // and its two tangent points must stay on THIS slice's side of the gap axis, not wrap past the
        // neighbour's — either failure means ρ is too big for this layout.
        if (jLineR >= sideR - 1.0 || jLineOff >= span / 2.0) return null;
        double hubScale = hubR / (hubR + rho);
        // Tangent point on the hub: the fillet centre projected onto the hub circle. Its angular offset is
        // NEGATIVE — it sits on the gap side of the boundary, where the hub is exposed.
        double jHubOff = Math.Atan2(cy * hubScale, cx * hubScale) * (180 / Math.PI);
        if (360 - span + 2 * jHubOff <= 1) return null;       // no exposed hub left for the return arc

        var tls = Polar(center, jLineR, startBnd + jLineOff); // junction tangent on the start side line
        var ths = Polar(center, hubR,   startBnd + jHubOff);  // junction tangent on the hub, start side
        var ss  = Polar(center, sideR,  startBnd + siSide);   // rim fillet leaves the start side here
        var As  = Polar(center, outer,  startBnd + turnAt);   // …and rejoins the rim here
        var Ae  = Polar(center, outer,  endBnd   - turnAt);
        var se  = Polar(center, sideR,  endBnd   - siSide);
        var tle = Polar(center, jLineR, endBnd   - jLineOff);
        var the = Polar(center, hubR,   endBnd   - jHubOff);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(tls, isFilled: true, isClosed: true);
            ctx.LineTo(ss, isStroked: true, isSmoothJoin: true);
            ctx.ArcTo(As, new Size(lo, lo), 0, false,
                SweepDirection.Clockwise,        isStroked: true, isSmoothJoin: true);   // rim fillet
            ctx.ArcTo(Ae, new Size(outer, outer), 0, (endBnd - turnAt) - (startBnd + turnAt) > 180,
                SweepDirection.Clockwise,        isStroked: true, isSmoothJoin: true);   // outer rim
            ctx.ArcTo(se, new Size(lo, lo), 0, false,
                SweepDirection.Clockwise,        isStroked: true, isSmoothJoin: true);   // rim fillet
            ctx.LineTo(tle, isStroked: true, isSmoothJoin: true);
            ctx.ArcTo(the, new Size(rho, rho), 0, false,
                SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);   // concave junction
            ctx.ArcTo(ths, new Size(hubR, hubR), 0, true,
                SweepDirection.Clockwise,        isStroked: true, isSmoothJoin: true);   // hub, the long way
            ctx.ArcTo(tls, new Size(rho, rho), 0, false,
                SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);   // concave junction
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>Reactor material only: the union of the hub disc and an armed slice's wedge — the hub's
    /// near-black fill "flows out" to engulf the wedge, reading as one continuous keyhole/blob silhouette.
    /// The wedge is built with a reduced inner radius (pulled in past the hub's edge) so the two shapes
    /// genuinely overlap — no floating seam between hub rim and wedge inner edge. <paramref name="i"/> is
    /// unused today (plain BuildWedge, not the seeded organic wobble) but kept for call-site symmetry with
    /// the other per-slice geometry builders.</summary>
    // Cached per (slice, start angle) with a layout key, like the terra/kawaii wedge caches — the
    // fallback path's rounding runs four widen/combine passes, far too heavy per frame.
    private (int n, double inner, double cx, double cy, double j) _fusionKey;
    private readonly Dictionary<(int i, double start), Geometry> _fusionGeo = new();

    private Geometry BuildReactorFusion(Point center, int i, double startBnd, double endBnd,
                                        double junctionR)
    {
        // Keyed per (slice, start angle) like TerraWedge — a single last-value slot re-ran the 8
        // boolean/widen rounding passes on EVERY arm change, so sweeping the stick around the wheel
        // rebuilt fusions all the way round. The dictionary holds one entry per slice (≤12).
        var lkey = (n: Slices.Count, _sliceInner, center.X, center.Y, junctionR);
        if (lkey != _fusionKey) { _fusionGeo.Clear(); _fusionKey = lkey; }
        if (_fusionGeo.TryGetValue((i, startBnd), out var cachedFusion)) return cachedFusion;

        double hubR = HubRadius;   // same hub disc radius DrawCenter uses (scales with thickness)
        // The armed wedge SWELLS: its outer edge pushes past the resting ring so the fused slice reads
        // as surging outward. Keep in sync with DrawSlice's content shift (half of this) that re-centres
        // the icon/logo in the swollen wedge.
        var keyhole = BuildFusedKeyhole(center, hubR, OuterRadius + ReactorSwellPx,
                                        startBnd, endBnd, GapPx, junctionR);
        if (keyhole is not null)
        {
            // Analytic path — cheap enough that edit-mode reflow (angles animate per frame) builds it
            // uncached; steady state still caches per slice to skip the bisection.
            if (!_sm.EditMode) _fusionGeo[(i, startBnd)] = keyhole;
            return keyhole;
        }

        // Degenerate span (full-circle slice, or no room for the min fillet) — the union+RoundCorners
        // route still covers it. Fine here: the widen-pass divots only bite the analytic-eligible shapes.
        var hub = new EllipseGeometry(center, hubR, hubR);
        double wedgeInner = Math.Min(_sliceInner, hubR - 4);   // overlap into the hub — no gap
        var wedge = BuildWedge(center, wedgeInner, OuterRadius + ReactorSwellPx, startBnd, endBnd, GapPx);
        var fusion = new CombinedGeometry(GeometryCombineMode.Union, hub, wedge);
        if (_sm.EditMode) { fusion.Freeze(); return fusion; }
        var rounded = RoundCorners(fusion, 10.0);
        rounded.Freeze();
        _fusionGeo[(i, startBnd)] = rounded;
        return rounded;
    }

    /// <summary>The hollow-rest reactor slice's whole body: a black disc with a white rim. The radius is
    /// the WHEEL-level one (see reactorDiscR) so every slice's disc is identical regardless of what sits
    /// in it; <paramref name="undoFit"/> divides out the content-fit scale the caller has already pushed,
    /// so a slice with a taller label doesn't end up with a smaller circle.</summary>
    /// <param name="rimOverride">Replaces the disc's own white rim — the in-wheel editor's carry/delete
    /// outline lands here, since on reactor the disc IS the slice's apparent shape.</param>
    private static void DrawReactorRestDisc(DrawingContext dc, Point contentCentre, double radius, double undoFit,
                                            Pen? rimOverride = null)
    {
        double r = radius / Math.Max(0.01, undoFit);
        dc.DrawEllipse(HubOpaqueDark, null, contentCentre, r, r);   // opaque body — see the plate block
        dc.DrawEllipse(ReactorRestDiscFill, rimOverride ?? ReactorRestDiscPen, contentCentre, r, r);
    }

    /// <summary>Draw the heavily-blurred, very-opaque drop shadow for a reactor icon/logo occupying
    /// <paramref name="rect"/>: two cached blurred silhouettes (the second blurred 2×) stacked at the
    /// EXACT same destination — dense core + soft halo — mapped UNIFORMLY (rect preserves the logo's
    /// aspect) so the baked blur stays isotropic on screen. <paramref name="clip"/> (the slice's own
    /// wedge geometry) masks the shadow so its halo can't bleed past the slice edge.</summary>
    private static void DrawReactorDropShadow(DrawingContext dc, ImageSource img, Rect rect, Geometry? clip)
    {
        var bake = SilhouetteBlurBitmaps(img);
        double s = rect.Width / bake.FitW;   // uniform (rect.Height / bake.FitH is the same ratio)
        var dst = new Rect(rect.X - (SilhouetteBlurCanvas - bake.FitW) / 2.0 * s,
                           rect.Y - (SilhouetteBlurCanvas - bake.FitH) / 2.0 * s + 1.0,   // 1px south
                           SilhouetteBlurCanvas * s, SilhouetteBlurCanvas * s);
        if (clip is not null) dc.PushClip(clip);
        dc.DrawImage(bake.Maps[BlurTight], dst);   // dense core
        dc.DrawImage(bake.Maps[BlurWide], dst);   // soft halo, stacked exactly atop
        if (clip is not null) dc.Pop();
    }

    /// <summary>Reactor's tile art: a real generated board, clipped to its generation rect (the off-board
    /// terminators would otherwise blow up the drawing's bounds and shrink everything when it's fitted to
    /// the tile) and drawn with a THICKER trace pen, since it renders at roughly a third scale — the
    /// wheel's 0.9px pen would land near a quarter-pixel and wash out.</summary>
    // internal: the Game Grid's reactor card paints the same generated board behind its content.
    internal static Brush ReactorTileBrush()
    {
        if (_reactorTileBrush is not null) return _reactorTileBrush;
        var gen = new Rect(0, 0, 340, 150);
        var pen = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 150, 150, 158)), 2.6);
        var chip = FrozenPen(new SolidColorBrush(Color.FromArgb(255, 150, 150, 158)), 3.6);
        var circuit = BuildReactorCircuit(gen, gen, seed: 20260725, tracePen: pen, chipPen: chip);
        var clipped = new DrawingGroup { ClipGeometry = new RectangleGeometry(gen) };
        clipped.Children.Add(circuit.Board);
        clipped.Freeze();
        var b = new DrawingBrush(clipped) { Stretch = Stretch.UniformToFill, Opacity = 0.5 };
        b.Freeze();
        return _reactorTileBrush = b;
    }

    /// <summary>Reactor fusion ripples — the same fused outline redrawn a few times, scaled inward about its
    /// own bounds centre with decreasing opacity, reading as concentric contours echoing the keyhole
    /// silhouette — plus the circuit-board parallax layer (flip <see cref="ReactorGridEnabled"/> to
    /// disable): a seeded PCB trace network clipped to the fused silhouette, zooming toward a stick-driven
    /// anchor, with a few bright sparks running the traces.</summary>
    private void DrawReactorFusionEffects(DrawingContext dc, Geometry geo, Point center, int n)
    {
        var fb = geo.Bounds;
        double fcx = fb.Left + fb.Width / 2.0, fcy = fb.Top + fb.Height / 2.0;
        for (int r = 0; r < ReactorRipplePens.Length; r++)
        {
            dc.PushTransform(new ScaleTransform(ReactorRippleScales[r], ReactorRippleScales[r], fcx, fcy));
            dc.DrawGeometry(null, ReactorRipplePens[r], geo);
            dc.Pop();
        }

        if (ReactorGridEnabled)
        {
            // ONE wheel-space board set (B3): regenerate only when the wheel's geometry changes. Every
            // fused slice clips a window onto the SAME boards, so focusing a different slice shows a
            // different part of one continuous circuit instead of a brand-new layout.
            var gkey = (n, _sliceInner, center.X, center.Y);
            if (_circuitGeoKey != gkey || _wheelBoards is null)
            {
                _circuitGeoKey = gkey;
                double wr = OuterRadius + ReactorSwellPx + 60;   // covers the swelled fusion everywhere
                var wheel = new Rect(center.X - wr, center.Y - wr, wr * 2, wr * 2);
                var front = BuildReactorCircuit(wheel, wheel, seed: 20260730);
                // The scaled-down layers need proportionally LARGER boards to still cover the wheel —
                // generated over inflated rects rather than scaled from the front one, which would
                // just show the same layout twice.
                var backGen = Rect.Inflate(wheel, wheel.Width * 0.35, wheel.Height * 0.35);
                var back    = BuildReactorCircuit(backGen, backGen, seed: 20260730 + 104729);
                var farGen  = Rect.Inflate(wheel, wheel.Width * 0.6, wheel.Height * 0.6);
                var far     = BuildReactorCircuit(farGen, farGen, seed: 20260730 + 224737);
                _wheelBoards = (front, back, far);
            }
            var boards = _wheelBoards.Value;
            // Re-roll the sparks whenever the BOARD ITSELF changes — sparks hold trace indices, and
            // indices into a REGENERATED board can point off its end (PointAt then threw inside
            // OnRender, which is unrecoverable: it killed the app — the 0.9.198 bug class).
            if (!ReferenceEquals(_circuit, boards.Front))
            {
                _circuit = boards.Front; _circuitBack = boards.Back; _circuitFar = boards.Far;
                _circuitSparkT = NowUtc;
                Array.Clear(_circuitSparks);
                Array.Clear(_circuitSparksBack);
                Array.Clear(_circuitSparksFar);
            }

            // The zoom emanates from an ANCHOR toward the pressed direction, so it reads as scaling
            // in the stick's direction rather than about a fixed corner.
            // Reduce Motion: pin the anchor/zoom at zero so the planes hold a fixed offset — the boards
            // + sparks still draw, they just stop shearing with the stick.
            double mag = _reduceMotion ? 0.0 : Math.Min(1.0, Math.Sqrt(_sm.StickX * _sm.StickX + _sm.StickY * _sm.StickY));
            dc.PushClip(geo);
            // Darkened plate behind the traces (full opacity — inside the 0.28 group it would barely
            // register), so the board reads as a surface rather than lines floating on the slice fill.
            dc.DrawGeometry(ReactorBoardPlate, null, geo);
            // Deepest plane first (least stick response — B5), then back, then front — the three
            // shear apart as the stick moves, which is what sells the depth.
            DrawCircuitLayer(_circuitFar,  _circuitSparksFar,  CircuitFarScale,  CircuitFarZoomAmt,  CircuitFarDim);
            DrawCircuitLayer(_circuitBack, _circuitSparksBack, CircuitBackScale, CircuitBackZoomAmt, CircuitBackDim);
            DrawCircuitLayer(_circuit,     _circuitSparks,     1.0,              1.0,                1.0);
            dc.Pop();   // clip

            void DrawCircuitLayer(ReactorCircuit? circuit, CircuitSpark[] sparks,
                                  double scale, double parallax, double dim)
            {
                if (circuit is null) return;
                double zm = 1.0 + CircuitZoomAmt * mag * parallax;
                // Reduce Motion: anchor stays at the wheel centre (mag is already pinned to 0 above,
                // but the anchor's stick term is independent of it, so zero it explicitly too).
                double ax = center.X + (_reduceMotion ? 0.0 : _sm.StickX * InnerRadius * CircuitAnchorReach * parallax);
                double ay = center.Y + (_reduceMotion ? 0.0 : _sm.StickY * InnerRadius * CircuitAnchorReach * parallax);
                if (scale < 0.999) dc.PushTransform(new ScaleTransform(scale, scale, center.X, center.Y));
                dc.PushTransform(new ScaleTransform(zm, zm, ax, ay));
                dc.PushOpacity(CircuitBoardOpacity * dim);
                dc.DrawDrawing(circuit.Board);
                dc.Pop();   // opacity — sparks draw bright, not at the board's 0.28

                // Sparks: head + a tail that re-walks the trace behind it (turns corners with the wire).
                bool dimmed = dim < 0.999;
                if (dimmed) dc.PushOpacity(dim);
                foreach (var s in sparks)
                {
                    if (s is null || s.Trace >= circuit.Traces.Count) continue;   // see Advance's backstop
                    var head = circuit.PointAt(s.Trace, s.Dist);
                    var prev = head;
                    var tail = SparkTailPens[s.Color];
                    for (int q = 0; q < SparkTailSteps; q++)
                    {
                        var pt = circuit.PointAt(s.Trace, s.Dist - s.Dir * SparkTailStepPx * (q + 1));
                        dc.DrawLine(tail[q], prev, pt);
                        if (pt == prev) break;   // clamped at the trace end — tail is fully drawn
                        prev = pt;
                    }
                    // Head matches the tail's width (1.6px pen → 0.8px radius) with a tight glow, so the
                    // spark reads as the wire lighting up rather than a bead travelling along it.
                    dc.DrawEllipse(SparkGlowBrushes[s.Color], null, head, 2.0, 2.0);   // fake glow (no BlurEffect)
                    dc.DrawEllipse(SparkHeadBrush, null, head, 0.8, 0.8);
                }
                if (dimmed) dc.Pop();
                dc.Pop();                          // zoom transform
                if (scale < 0.999) dc.Pop();       // layer scale
            }
        }
    }

    /// <summary>Reactor material: fine concentric etched rings inside the hub disc (every ~5px, from
    /// r-6 down to ~45% of r) — a subtle machined/etched-metal texture on the dark hub node.</summary>
    private (double r, double cx, double cy) _etchKey;
    private DrawingGroup? _etchDrawing;
    private void DrawReactorHubEtching(DrawingContext dc, Point center, double hubR)
    {
        // ~19 rings at a fixed radius/centre — retained as one frozen drawing instead of re-issuing
        // every ellipse per frame. Rebuilds only when the hub geometry moves (thickness change, collapse).
        if (_etchDrawing is null || _etchKey != (hubR, center.X, center.Y))
        {
            var g = new DrawingGroup();
            using (var gdc = g.Open())
                for (double r = hubR - 6; r >= hubR * 0.45; r -= 5)
                    gdc.DrawEllipse(null, ReactorHubEtchPen, center, r, r);
            g.Freeze();
            _etchDrawing = g;
            _etchKey = (hubR, center.X, center.Y);
        }
        dc.DrawDrawing(_etchDrawing);
    }
}
