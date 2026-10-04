using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ControllerWheel;

/// <summary>Splits the Radiata flower mark (24×24 box) into its animatable regions — the two lower leaves
/// (their own paths in the source art) and the blossom sliced into petal wedges — for the springing logo
/// entrance shared by the About screen and the wheels-enabled toast. Pure geometry; the animation lives
/// with each consumer.</summary>
internal static class FlowerMark
{
    public const double BoxSize   = 24.0;   // the glyph's square design box
    public const double CentreX   = 12.0;   // glyph is a 24×24 box
    public const double LeafBaseY = 23.6;   // plant base (bottom-centre) — leaves sprout from here

    // Far enough to cover the blossom from its centre (its farthest petal edge is ~12.5 out); used as the
    // wedge radius and the valley-march start.
    private const double Reach = 16.0;

    /// <summary>The flower's pieces in 24×24 space: the two lower leaves, the blossom sliced into petals
    /// (ordered from the top, clockwise, so a consumer can sweep them in), and the blossom centre (the
    /// petal scale origin). Null if the glyph isn't the expected shape.</summary>
    public sealed record Parts(Geometry Left, Geometry Right, IReadOnlyList<Geometry> Petals, Point HeadCentre);

    public static Parts? BuildParts()
    {
        var headGeo = FlowerIcon.Blossom;
        var hb = headGeo.Bounds;
        if (hb.IsEmpty) return null;   // unexpected shape → caller draws nothing
        var headCentre = new Point(hb.X + hb.Width / 2, hb.Y + hb.Height / 2);

        // Slice the blossom into pie-wedges cut at the petal valleys so each wedge holds one lobe (plus
        // its share of the centre disc); the union of all wedges is exactly the original blossom.
        var cuts = PetalValleys(headGeo, headCentre);
        if (cuts.Count < 4)   // detection unclear → even wedges (the mark has six petals)
        {
            cuts = new List<double>();
            const int n = 6;
            for (int i = 0; i < n; i++) cuts.Add(-90 + i * 360.0 / n);
        }

        var petals = new List<(Geometry geo, double sweepKey)>();
        for (int i = 0; i < cuts.Count; i++)
        {
            double b0 = cuts[i], b1 = cuts[(i + 1) % cuts.Count];
            if (b1 <= b0) b1 += 360;
            var petal = Geometry.Combine(headGeo, Wedge(headCentre, Reach, b0, b1), GeometryCombineMode.Intersect, null);
            if (petal.IsEmpty()) continue;
            petal.Freeze();
            double mid = (b0 + b1) / 2;
            double sweepKey = (((mid + 90) % 360) + 360) % 360;   // 0° at the top, increasing clockwise
            petals.Add((petal, sweepKey));
        }
        petals.Sort((x, y) => x.sweepKey.CompareTo(y.sweepKey));
        return new Parts(FlowerIcon.LeftLeaf, FlowerIcon.RightLeaf, [.. petals.Select(p => p.geo)], headCentre);
    }

    /// <summary>Angles (degrees) of the blossom's petal valleys — local minima of its boundary radius
    /// about <paramref name="c"/> — so wedge cuts fall between lobes, not across them. De-duplicated,
    /// sorted; empty/short if the shape is ambiguous.</summary>
    private static List<double> PetalValleys(Geometry g, Point c)
    {
        const int N = 180;                       // 2° resolution
        var rad = new double[N];
        for (int i = 0; i < N; i++)
        {
            double a = i * 2 * System.Math.PI / N;
            var dir = new Vector(System.Math.Cos(a), System.Math.Sin(a));
            double r = 0;
            for (double rr = Reach; rr >= 0; rr -= 0.2)   // march inward until inside → boundary radius
                if (g.FillContains(c + rr * dir)) { r = rr; break; }
            rad[i] = r;
        }

        const int W = 8;                         // a valley is the smallest radius within ±16°
        var raw = new List<double>();
        for (int i = 0; i < N; i++)
        {
            bool isMin = true;
            for (int d = -W; d <= W; d++)
                if (rad[(i + d + N) % N] < rad[i] - 1e-6) { isMin = false; break; }
            if (isMin) raw.Add(i * 360.0 / N);
        }

        // Merge adjacent hits (flat valleys produce clusters), including across the 0°/360° wrap.
        raw.Sort();
        var merged = new List<double>();
        foreach (var v in raw)
        {
            if (merged.Count > 0 && v - merged[^1] < 18) merged[^1] = (merged[^1] + v) / 2;
            else merged.Add(v);
        }
        if (merged.Count > 1 && merged[0] + 360 - merged[^1] < 18)
        {
            merged[0] = (((merged[0] + merged[^1] + 360) / 2) % 360);
            merged.RemoveAt(merged.Count - 1);
        }
        return merged;
    }

    /// <summary>A filled pie-wedge from <paramref name="c"/> spanning [a0,a1] degrees (screen space,
    /// 0° = +x, clockwise), out to <paramref name="r"/>. Slices the blossom into petal sectors.</summary>
    private static PathGeometry Wedge(Point c, double r, double a0Deg, double a1Deg)
    {
        double a0 = a0Deg * System.Math.PI / 180, a1 = a1Deg * System.Math.PI / 180;
        var p0 = new Point(c.X + r * System.Math.Cos(a0), c.Y + r * System.Math.Sin(a0));
        var p1 = new Point(c.X + r * System.Math.Cos(a1), c.Y + r * System.Math.Sin(a1));
        var fig = new PathFigure { StartPoint = c, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new LineSegment(p0, false));
        fig.Segments.Add(new ArcSegment(p1, new Size(r, r), 0, false, SweepDirection.Clockwise, false));
        var g = new PathGeometry();
        g.Figures.Add(fig);
        g.Freeze();
        return g;
    }

    /// <summary>An animation that holds <paramref name="from"/> for <paramref name="delayMs"/> (so the
    /// piece stays hidden / at its start pose until its turn — reliably, even when the entrance replays
    /// over a previously-finished run), then eases to <paramref name="to"/> over <paramref name="moveMs"/>.</summary>
    internal static DoubleAnimationUsingKeyFrames Delayed(double from, double to, double delayMs, double moveMs, IEasingFunction ease)
    {
        var k = new DoubleAnimationUsingKeyFrames();
        k.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (delayMs > 0)
            k.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs))));
        k.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delayMs + moveMs)), ease));
        return k;
    }
}
