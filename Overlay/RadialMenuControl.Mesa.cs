using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

// Mesa material (identifiers use its original name, Terra — see the class-level naming note in
// RadialMenuControl.cs): the hand-cut organic wedge shape.
public sealed partial class RadialMenuControl
{
    // Terra wedge cache: the rounded organic wedge costs 4 widen passes, so it's computed once per
    // (slice, layout) — the whole cache clears when the layout key (count/thickness/centre) changes.
    private (int n, double inner, double cx, double cy) _terraKey;
    private readonly Dictionary<(int i, double start), Geometry> _terraGeo = new();

    /// <summary>Terra slice geometry: the organic hand-cut wedge with (a) the inter-slice gap widened to
    /// MATCH the hub↔slice breathing gap and (b) every corner rounded (~7px fillets — no sharp points).
    /// Cached per slice; during edit-mode reflow (angles animate per frame) the un-rounded wedge is
    /// returned instead of re-running the rounding every frame.</summary>
    private Geometry TerraWedge(Point center, int i, int n, double startBnd, double endBnd)
    {
        var key = (n, _sliceInner, center.X, center.Y);
        if (key != _terraKey) { _terraGeo.Clear(); _terraKey = key; }
        if (_terraGeo.TryGetValue((i, startBnd), out var cached)) return cached;

        double gap = _sliceInner - HubRadius;   // same breathing room between slices as hub↔slice
        var raw = BuildOrganicWedge(center, _sliceInner, OuterRadius, startBnd, endBnd, gap,
                                    amp: 2.2, segPx: 8.0, seed: i);
        if (_sm.EditMode) return raw;           // animating layout — skip the expensive rounding
        var rounded = RoundCorners(raw, 7.0);   // fillets the corners; the gentle 70px waves survive
        rounded.Freeze();
        _terraGeo[(i, startBnd)] = rounded;
        return rounded;
    }
}
