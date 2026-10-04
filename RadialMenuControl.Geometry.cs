using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

// Wedge/ring/arc geometry builders shared across materials, plus the corner-rounding and hand-cut-edge
// primitives Terra/Salvage/Kawaii's own wedge builders (RadialMenuControl.Mesa.cs/.Salvage.cs/.Kawaii.cs)
// compose.
public sealed partial class RadialMenuControl
{
    // ── Geometry helpers ──────────────────────────────────────────────────────
    private static Geometry BuildWedge(
        Point center, double inner, double outer, double startBnd, double endBnd, double gapPx = GapPx)
    {
        // A single slice spans the full circle → a seamless donut (annulus), no gap/notch.
        if (endBnd - startBnd >= 359.9)
        {
            var donut = new GeometryGroup { FillRule = FillRule.EvenOdd };
            donut.Children.Add(new EllipseGeometry(center, outer, outer));
            donut.Children.Add(new EllipseGeometry(center, inner, inner));
            donut.Freeze();
            return donut;
        }

        // Offset each radius by a different angle so the physical gap is gapPx pixels wide
        // at every point along the radial edge — not just at one radius. (Min(1,…): see BuildOrganicWedge.)
        double half = gapPx / 2.0;
        double siInner = Math.Asin(Math.Min(1.0, half / inner)) * (180 / Math.PI);
        double siOuter = Math.Asin(Math.Min(1.0, half / outer)) * (180 / Math.PI);

        var p0 = Polar(center, inner, startBnd + siInner);
        var p1 = Polar(center, outer, startBnd + siOuter);
        var p2 = Polar(center, outer, endBnd   - siOuter);
        var p3 = Polar(center, inner, endBnd   - siInner);
        bool large = (endBnd - startBnd - siOuter * 2) > 180;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p0, isFilled: true, isClosed: true);
            ctx.LineTo(p1, isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(p2, new Size(outer, outer), 0, large,
                SweepDirection.Clockwise,        isStroked: true, isSmoothJoin: false);
            ctx.LineTo(p3, isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(p0, new Size(inner, inner), 0, large,
                SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>Round all corners of a filled shape via morphological close-then-open: dilate/erode by
    /// <paramref name="r"/> (a round-pen widened outline unioned into / excluded from the shape). The
    /// close pass fillets CONCAVE corners, the open pass fillets CONVEX ones; smooth edges are unchanged.
    /// O(widen×4) — cache the result, never call per frame.</summary>
    private static Geometry RoundCorners(Geometry g, double r)
    {
        static Geometry Widen(Geometry x, double rad)
        {
            var p = new Pen(Brushes.Black, rad * 2)
            { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            return x.GetWidenedPathGeometry(p);
        }
        static Geometry Dilate(Geometry x, double rad) => Geometry.Combine(x, Widen(x, rad), GeometryCombineMode.Union,   null);
        static Geometry Erode (Geometry x, double rad) => Geometry.Combine(x, Widen(x, rad), GeometryCombineMode.Exclude, null);
        var closed = Erode(Dilate(g, r), r);      // fillet concave corners (hub↔wedge junctions)
        return Dilate(Erode(closed, r), r);       // fillet convex corners (outer-rim corners)
    }

    // Target wavelengths in PX ALONG THE EDGE, so the wobble reads at a consistent density on a short
    // inner arc, a long outer arc, and the hub's full circumference alike — a fixed cycle count per edge
    // would read dense on the inner diameter and sparse on the hub.
    private const double WobbleLen1 = 70.0;   // primary wave px wavelength
    private const double WobbleLen2 = 30.0;   // secondary detail wave px wavelength

    /// <summary>Deterministic smooth "hand-cut edge" wobble for a point at fractional position
    /// <paramref name="t"/> (0..1) along an arc/loop — two summed sine waves whose phase is derived from
    /// <paramref name="seed"/>, so repeated calls with the same seed reproduce the identical shape (no
    /// frame-to-frame shimmer) while different seeds (slice index) give each slice its own irregularity.
    /// <paramref name="c1"/>/<paramref name="c2"/> are the two waves' CYCLE COUNTS over the edge — pass
    /// edgeLength / WobbleLen so density is constant in px (closed loops must round them to integers or
    /// the shape jumps at the seam).</summary>
    private static double EdgeWobble(double t, double amp, int seed, double c1, double c2)
    {
        double ph1 = (seed * 12.9898) % (2 * Math.PI);
        double ph2 = (seed * 78.233)  % (2 * Math.PI);
        return amp * (0.6 * Math.Sin(t * Math.PI * 2 * c1 + ph1) + 0.4 * Math.Sin(t * Math.PI * 2 * c2 + ph2));
    }

    /// <summary>Salvage's edge profile: instead of undulating, the radius holds a DISCRETE level across a
    /// cell and then jumps, so the rim reads as torn/stamped plate rather than a wavy cut. Two points per
    /// cell at the same radius means the jump between cells is purely radial — a real step, not a diagonal
    /// ramp. Cell WIDTHS vary too (a uniform pitch read as a machined comb), hashed per cell in
    /// [BlockyCellMinPx, BlockyCellMaxPx] and then normalised to tile the arc exactly — so a closed ring
    /// still meets itself. Normalising nudges the realised widths off the nominal range by the ratio
    /// between the arc length and the sampled total, which is near 1 since the count targets the mean.</summary>
    private const double BlockyCellMinPx = 10.0;
    private const double BlockyCellMaxPx = 45.0;
    private static Point[] BlockyArcPoints(Point center, double radius, double a0, double a1,
                                           double amp, int seed)
    {
        static uint Hash(int c, int seed, uint salt)
        {
            unchecked
            {
                uint h = (uint)(c * 374761393 + seed * 668265263) ^ salt;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return h;
            }
        }

        double len = radius * Math.Abs(a1 - a0) * (Math.PI / 180.0);
        int cells = Math.Max(4, (int)Math.Round(len / ((BlockyCellMinPx + BlockyCellMaxPx) / 2.0)));

        var width = new double[cells];
        double total = 0;
        for (int c = 0; c < cells; c++)
        {
            width[c] = BlockyCellMinPx
                     + (Hash(c, seed, 0x5BD1u) % 1000) / 999.0 * (BlockyCellMaxPx - BlockyCellMinPx);
            total += width[c];
        }

        var pts = new Point[cells * 2];
        double acc = 0;
        for (int c = 0; c < cells; c++)
        {
            double t0 = acc / total;
            acc += width[c];
            double t1 = acc / total;
            double r = radius + ((int)(Hash(c, seed, 0xA7C3u) % 5) - 2) * amp;   // five levels, -2..+2 × amp
            pts[c * 2]     = Polar(center, r, a0 + (a1 - a0) * t0);
            pts[c * 2 + 1] = Polar(center, r, a0 + (a1 - a0) * t1);
        }
        return pts;
    }

    /// <summary>Closed blocky ring — the single-slice (donut) case of a blocky wedge.</summary>
    private static Geometry BuildBlockyRing(Point center, double radius, double amp, int seed)
        => CloseRing(BlockyArcPoints(center, radius, 0, 360, amp, seed));

    /// <summary>Same wedge shape as <see cref="BuildWedge"/>, but the outer and inner ARCS are drawn as
    /// wobbled polylines (radius perturbed by <see cref="EdgeWobble"/>) instead of true circular arcs —
    /// a coarse polyline (large <paramref name="segPx"/>, larger <paramref name="amp"/>) reads as a
    /// hand-cut paper edge; a fine one (small segPx, small amp) reads as a sprayed/eroded salvage edge.
    /// Wave density is constant in px along each arc (see WobbleLen1/2). The two straight radial edges
    /// are left as plain lines between the (already-wobbled) arc endpoints. Deterministic per
    /// <paramref name="seed"/>; frozen like the plain wedge.</summary>
    /// <param name="blocky">Salvage's stepped/stamped edge (see <see cref="BlockyArcPoints"/>) instead of
    /// the smooth sine wobble terra uses. <paramref name="segPx"/> is ignored in this mode — cell width
    /// governs the step density.</param>
    private static Geometry BuildOrganicWedge(
        Point center, double inner, double outer, double startBnd, double endBnd, double gapPx,
        double amp, double segPx, int seed, bool blocky = false)
    {
        // A single slice spans the full circle → a seamless wobbled donut (mirrors BuildWedge's donut
        // special case — without it a one-slice terra/salvage wheel showed a phantom gap notch).
        if (endBnd - startBnd >= 359.9)
        {
            var donut = new GeometryGroup { FillRule = FillRule.EvenOdd };
            donut.Children.Add(blocky ? BuildBlockyRing(center, outer, amp, seed * 2 + 1)
                                      : BuildWobbleRingDense(center, outer, amp, segPx, seed * 2 + 1));
            donut.Children.Add(blocky ? BuildBlockyRing(center, inner, amp, seed * 2 + 2)
                                      : BuildWobbleRingDense(center, inner, amp, segPx, seed * 2 + 2));
            donut.Freeze();
            return donut;
        }

        double half = gapPx / 2.0;
        // Min(1, …) guards a future retune where the gap exceeds the inner radius — Asin would silently
        // return NaN and degenerate the wedge (same clamp FitLogoHeight already uses).
        double siInner = Math.Asin(Math.Min(1.0, half / inner)) * (180 / Math.PI);
        double siOuter = Math.Asin(Math.Min(1.0, half / outer)) * (180 / Math.PI);

        double sIn = startBnd + siInner, eIn = endBnd - siInner;     // inner boundary angles
        double sOut = startBnd + siOuter, eOut = endBnd - siOuter;   // outer boundary angles

        Point[] Arc(double radius, double a0, double a1, int localSeed)
        {
            if (blocky) return BlockyArcPoints(center, radius, a0, a1, amp, localSeed);
            // Constant px density: cycle counts + segment count both derive from THIS arc's length, so
            // the inner arc never inherits the outer arc's cycle count on its shorter edge.
            double len = radius * Math.Abs(a1 - a0) * (Math.PI / 180.0);
            double c1  = Math.Max(1.0, len / WobbleLen1);
            double c2  = Math.Max(2.0, len / WobbleLen2);
            int segs = Math.Max(8, (int)Math.Ceiling(len / segPx));
            var pts = new Point[segs + 1];
            for (int k = 0; k <= segs; k++)
            {
                double t = (double)k / segs;
                double ang = a0 + (a1 - a0) * t;
                double r = radius + EdgeWobble(t, amp, localSeed, c1, c2);
                pts[k] = Polar(center, r, ang);
            }
            return pts;
        }

        // Outer arc runs start→end; inner arc runs end→start so the figure closes without crossing.
        var outerPts = Arc(outer, sOut, eOut, seed * 2 + 1);
        var innerPts = Arc(inner, eIn,  sIn,  seed * 2 + 2);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(innerPts[^1], isFilled: true, isClosed: true);        // inner point at sIn
            ctx.LineTo(outerPts[0], isStroked: true, isSmoothJoin: false);        // radial edge → outer at sOut
            ctx.PolyLineTo(outerPts[1..], isStroked: true, isSmoothJoin: true);   // wobbled outer arc → eOut
            ctx.LineTo(innerPts[0], isStroked: true, isSmoothJoin: false);        // radial edge → inner at eIn
            ctx.PolyLineTo(innerPts[1..], isStroked: true, isSmoothJoin: true);   // wobbled inner arc → sIn (closes)
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>The blocky inner rim as its raw CELL PAIRS (points 2k / 2k+1 are one cell's ends, both at
    /// that cell's radius) — the same array <see cref="BuildOrganicInnerEdge"/> turns into a polyline, but
    /// exposed so the lip can stroke each cell with its own thickness. Null for a full-circle wheel, which
    /// has no gap inset to compute.</summary>
    private static Point[] BlockyInnerEdgeCells(
        Point center, double inner, double startBnd, double endBnd, double gapPx, double amp, int seed)
    {
        int localSeed = seed * 2 + 2;
        if (endBnd - startBnd >= 359.9)
            return BlockyArcPoints(center, inner, 0, 360, amp, localSeed);
        double siInner = Math.Asin(Math.Min(1.0, (gapPx / 2.0) / inner)) * (180 / Math.PI);
        // BuildOrganicWedge walks this arc eIn→sIn; the SAME direction is required, not merely allowed —
        // the blocky level is hashed from the cell index, so walking the other way would step a different
        // sequence and the stroke would no longer sit on the wedge's own edge.
        return BlockyArcPoints(center, inner, endBnd - siInner, startBnd + siInner, amp, localSeed);
    }

    /// <summary>The wedge's TWO RADIAL SIDES plus its OUTER arc, as an OPEN polyline — the complement of
    /// <see cref="BuildOrganicInnerEdge"/>, which covers the inner rim. Salvage strokes this so the plate
    /// is edged all the way round while the inner rim keeps its own heavier lip treatment.
    /// <para>Mirrors <see cref="BuildOrganicWedge"/>'s walk exactly — same gap insets, same seeds
    /// (<c>seed*2+1</c> outer, <c>seed*2+2</c> inner), same arc DIRECTIONS — so the stroke lands on the
    /// slice's own painted edge rather than near it. BLOCKY only, which is the shape salvage builds; the
    /// single-slice donut has no radial sides, so its outer ring is returned on its own.</para></summary>
    private static Geometry BuildBlockyOuterEdge(
        Point center, double inner, double outer, double startBnd, double endBnd, double gapPx,
        double amp, int seed)
    {
        if (endBnd - startBnd >= 359.9) return BuildBlockyRing(center, outer, amp, seed * 2 + 1);

        double half = gapPx / 2.0;
        double siInner = Math.Asin(Math.Min(1.0, half / inner)) * (180 / Math.PI);
        double siOuter = Math.Asin(Math.Min(1.0, half / outer)) * (180 / Math.PI);
        double sIn = startBnd + siInner, eIn = endBnd - siInner;
        double sOut = startBnd + siOuter, eOut = endBnd - siOuter;

        // The outer arc, walked start→end exactly as the wedge walks it (same cell points).
        Point[] outerPts = BlockyArcPoints(center, outer, sOut, eOut, amp, seed * 2 + 1);
        // The inner arc's two ENDPOINTS are where the radial sides land; take them from the same walk the
        // wedge uses (eIn→sIn) so a step at the corner matches instead of missing by a cell.
        Point[] innerPts = BlockyArcPoints(center, inner, eIn, sIn, amp, seed * 2 + 2);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(innerPts[^1], isFilled: false, isClosed: false);       // inner corner at sIn
            ctx.LineTo(outerPts[0], isStroked: true, isSmoothJoin: false);         // radial side out
            ctx.PolyLineTo(outerPts[1..], isStroked: true, isSmoothJoin: true);    // outer arc
            ctx.LineTo(innerPts[0], isStroked: true, isSmoothJoin: false);         // radial side back in
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>Just the INNER wobbled arc of <see cref="BuildOrganicWedge"/> — same seed/amp/segPx, so it
    /// traces exactly the slice's own inner edge — as an OPEN, unfilled polyline for stroking alone
    /// (salvage draws its edge stroke on the inner rim only, not the full wedge outline). The single-slice
    /// donut case reuses <see cref="BuildWobbleRingDense"/> with the same seed the wedge's inner ring uses.</summary>
    private static Geometry BuildOrganicInnerEdge(
        Point center, double inner, double outer, double startBnd, double endBnd, double gapPx,
        double amp, double segPx, int seed, bool blocky = false)
    {
        if (blocky)
            return CellsToPolyline(BlockyInnerEdgeCells(center, inner, startBnd, endBnd, gapPx, amp, seed));
        if (endBnd - startBnd >= 359.9)
            return BuildWobbleRingDense(center, inner, amp, segPx, seed * 2 + 2);

        double half = gapPx / 2.0;
        double siInner = Math.Asin(Math.Min(1.0, half / inner)) * (180 / Math.PI);
        double sIn = startBnd + siInner, eIn = endBnd - siInner;
        int localSeed = seed * 2 + 2;

        double len = inner * Math.Abs(sIn - eIn) * (Math.PI / 180.0);
        double c1  = Math.Max(1.0, len / WobbleLen1);
        double c2  = Math.Max(2.0, len / WobbleLen2);
        int segs = Math.Max(8, (int)Math.Ceiling(len / segPx));
        var pts = new Point[segs + 1];
        for (int k = 0; k <= segs; k++)
        {
            double t = (double)k / segs;
            pts[k] = Polar(center, inner + EdgeWobble(t, amp, localSeed, c1, c2), eIn + (sIn - eIn) * t);
        }
        return CellsToPolyline(pts);
    }

    /// <summary>Open, unfilled polyline through the given points — for stroking alone.</summary>
    private static Geometry CellsToPolyline(Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: false, isClosed: false);
            ctx.PolyLineTo(pts[1..], isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>A closed, wobbled circle (see <see cref="EdgeWobble"/>) used as the Terra material's
    /// irregular terracotta backdrop blob and its offset cutout shadow — same point set for both, only the
    /// draw call's transform differs, so the shadow tracks the blob's shape exactly.</summary>
    private static Geometry BuildWobbleRing(Point center, double radius, double amp, int segs, int seed)
    {
        // Legacy fixed 3/7-cycle waves — kept ONLY for the terra backdrop blob, whose deliberately
        // low-frequency lumpiness is a different effect from the constant-density cut edges.
        segs = Math.Max(3, segs);
        var pts = new Point[segs];
        for (int k = 0; k < segs; k++)
        {
            double t = (double)k / segs;
            double r = radius + EdgeWobble(t, amp, seed, 3.0, 7.0);
            pts[k] = Polar(center, r, t * 360.0);
        }
        return CloseRing(pts);
    }

    /// <summary>Constant-density wobbled circle (the hub disc + material toasts): cycle counts derive
    /// from the circumference (rounded to integers so the loop closes seamlessly), so the hub's waves
    /// match the slices' px density instead of stretching a fixed cycle count around a big circle.</summary>
    private static Geometry BuildWobbleRingDense(Point center, double radius, double amp, double segPx, int seed)
    {
        double len = 2 * Math.PI * radius;
        double c1  = Math.Max(2.0, Math.Round(len / WobbleLen1));
        double c2  = Math.Max(3.0, Math.Round(len / WobbleLen2));
        int segs = Math.Max(24, (int)Math.Ceiling(len / segPx));
        var pts = new Point[segs];
        for (int k = 0; k < segs; k++)
        {
            double t = (double)k / segs;
            double r = radius + EdgeWobble(t, amp, seed, c1, c2);
            pts[k] = Polar(center, r, t * 360.0);
        }
        return CloseRing(pts);
    }

    private static Geometry CloseRing(Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
            ctx.PolyLineTo(pts[1..], isStroked: true, isSmoothJoin: true);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>The hub's own outline for a toast host. Salvage gets its STEPPED plate ring, terra the
    /// wobbled cut — same seed and amp as the real hub in both cases, since a toast is meant to read as
    /// the hub's material, not an approximation of it.
    /// <para>Hosts that mirror the hub's wavy edge outside this control (the App's enable/disable
    /// toasts) borrow the same constant-density wobbled ring, with the hub's fixed seed so the shapes
    /// match.</para></summary>
    internal static Geometry BuildHubToastRing(Point center, double radius, double amp, double segPx, bool blocky = false)
        => blocky ? BuildBlockyRing(center, radius, amp, seed: 101)
                  : BuildWobbleRingDense(center, radius, amp, segPx, seed: 101);

    private static Geometry BuildArc(Point center, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg >= 359.9)
        {
            var full = new EllipseGeometry(center, r, r);
            full.Freeze();
            return full;
        }
        double endDeg = startDeg + sweepDeg;
        bool   large  = sweepDeg > 180;
        var p0 = Polar(center, r, startDeg);
        var p1 = Polar(center, r, endDeg);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p0, isFilled: false, isClosed: false);
            ctx.ArcTo(p1, new Size(r, r), 0, large, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        // Not frozen — contains per-frame data
        return geo;
    }

    private static Point Polar(Point center, double r, double deg)
    {
        double rad = deg * Math.PI / 180;
        return new Point(center.X + r * Math.Cos(rad), center.Y + r * Math.Sin(rad));
    }

    // ── Freeze helpers ────────────────────────────────────────────────────────
    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen   FrozenPen(SolidColorBrush color, double thickness)
    {
        var p = new Pen(color, thickness);
        color.Freeze();
        p.Freeze();
        return p;
    }
}
