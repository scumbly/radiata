using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

// Salvage material: the blocky/stamped wedge shape and its edge-cell geometry. Salvage's draw-time
// helpers (edge detail, crescent, banner, rust/rivet textures) live in RadialMenuControl.cs alongside
// DrawSlice, which calls them.
public sealed partial class RadialMenuControl
{
    // Salvage slice-geometry cache: the wedge, the lip cell array, and both edge polylines each run a
    // full BlockyArcPoints walk (per-cell hashing, two array allocations) — four walks per slice per
    // FRAME before this cache. All four are deterministic per (layout, slice, start angle), so one
    // record carries the set; the whole cache clears when the layout key changes. Edit-mode reflow
    // animates the angles per frame, so it builds uncached like terra/kawaii do.
    private sealed record SalvageSliceGeo(Geometry Wedge, Point[] Cells, Geometry InnerEdge, Geometry OuterEdge);
    private (int n, double inner, double cx, double cy) _salvageGeoKey;
    private readonly Dictionary<(int i, double start), SalvageSliceGeo> _salvageGeo = new();

    private SalvageSliceGeo SalvageGeo(Point center, int i, int n, double startBnd, double endBnd)
    {
        var key = (n, _sliceInner, center.X, center.Y);
        if (key != _salvageGeoKey) { _salvageGeo.Clear(); _salvageGeoKey = key; }
        if (!_sm.EditMode && _salvageGeo.TryGetValue((i, startBnd), out var cached)) return cached;
        var built = new SalvageSliceGeo(
            BuildOrganicWedge(center, _sliceInner, OuterRadius, startBnd, endBnd, SliceGapPx,
                              amp: 1.1, segPx: 3.5, seed: i, blocky: true),
            BlockyInnerEdgeCells(center, _sliceInner, startBnd, endBnd, SliceGapPx, amp: 1.1, seed: i),
            BuildOrganicInnerEdge(center, _sliceInner, OuterRadius, startBnd, endBnd,
                                  SliceGapPx, amp: 1.1, segPx: 3.5, seed: i, blocky: true),
            BuildBlockyOuterEdge(center, _sliceInner, OuterRadius, startBnd, endBnd,
                                 SliceGapPx, amp: 1.1, seed: i));
        if (!_sm.EditMode) _salvageGeo[(i, startBnd)] = built;
        return built;
    }
}
