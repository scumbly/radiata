using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

// Kawaii material: the rounded wedge shape, plus the geometry/token wrappers hosts outside this control
// use to match the hub's cloud outline and ambient twinkle (see docs/MATERIALS.md).
public sealed partial class RadialMenuControl
{
    // Kawaii wedge cache: the corner rounding costs 4 widen passes, so it's computed once per
    // (layout, angle) — the whole cache clears when the layout key (count/thickness/centre) changes.
    // Every slice is the SAME true-arc wedge shape (no per-slice seed like terra/salvage), so the start
    // angle alone keys it.
    private (int n, double inner, double cx, double cy) _kawaiiKey;
    private readonly Dictionary<double, Geometry> _kawaiiGeo = new();

    /// <summary>Kawaii slice geometry: the plain true-arc wedge with every corner rounded (~9px fillets —
    /// candy-soft, no sharp points). Cached per (layout, start angle); during
    /// edit-mode reflow (angles animate every frame) the un-rounded wedge is returned instead.</summary>
    private Geometry KawaiiWedge(Point center, int n, double startBnd, double endBnd)
    {
        var key = (n, _sliceInner, center.X, center.Y);
        if (key != _kawaiiKey) { _kawaiiGeo.Clear(); _kawaiiKey = key; }
        if (_kawaiiGeo.TryGetValue(startBnd, out var cached)) return cached;

        var raw = BuildWedge(center, _sliceInner, OuterRadius, startBnd, endBnd, SliceGapPx);
        if (_sm.EditMode) return raw;            // animating layout — skip the expensive rounding
        var rounded = RoundCorners(raw, 9.0);
        rounded.Freeze();
        _kawaiiGeo[startBnd] = rounded;
        return rounded;
    }

    /// <summary>Kawaii's cloud-scalloped hub outline, for the same toast hosts (see
    /// <see cref="BuildHubToastRing"/>) — identical lobe construction, so toast and hub match.</summary>
    internal static Geometry BuildCloudToastRing(Point center, double radius) => BuildCloudRing(center, radius);

    /// <summary>A four-point kawaii star, for hosts drawing the ambient twinkle outside this control.</summary>
    internal static Geometry KawaiiStarGeometry(Point center, double radius, double rotDeg)
        => BuildFxStar(center, radius, rotDeg);

    /// <summary>The ambient twinkle's warm-white ink, shared with the toast so the two can't drift.</summary>
    internal static Brush KawaiiTwinkleInk => TwinkleBrush;

    /// <summary>The twinkle's orchid rim + white-hot core, for hosts drawing the ambient stars outside this
    /// control (the Game Grid's kawaii card scatters them over its pink material). Same three tokens the
    /// hub's <c>DrawHubTwinkle</c> uses, so a star on the card and a star on the cloud are the same star.</summary>
    internal static Pen   KawaiiTwinkleEdge => TwinkleEdgePen;
    internal static Brush KawaiiTwinkleCore => TwinkleCoreBrush;

    /// <summary>The hub twinkle's per-star cycle length, so an ambient host can pace its own stars to the
    /// same rhythm (scaled if it wants a calmer background version).</summary>
    internal const double KawaiiTwinklePeriodSec = TwinklePeriodSec;
}
