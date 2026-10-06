using System.Collections.Generic;
using System.Linq;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Core/XboxStuckRootTracker.cs — a childless Xbox composite root is settling for 10 s, stuck after,
/// and healthy the moment it has a child. Pure Core on an injected clock; no pad, no devnode.</summary>
internal static class T_StuckRoot
{
    private const string Root = @"USB\VID_045E&PID_0B12\3039";
    private const XboxStuckRootTracker.RootState Healthy = XboxStuckRootTracker.RootState.Healthy;
    private const XboxStuckRootTracker.RootState Settling = XboxStuckRootTracker.RootState.Settling;
    private const XboxStuckRootTracker.RootState Stuck = XboxStuckRootTracker.RootState.Stuck;

    private static XboxStuckRootTracker.RootState Pass(XboxStuckRootTracker t, long now, bool hasChildren)
        => t.Observe(now, new[] { (Root, hasChildren) }).Single().State;

    public static void Run()
    {
        H.Group("Stuck Xbox root (Core)");

        var t = new XboxStuckRootTracker();
        H.Check("a childless root is settling on first sight (not a pad, no card)", Pass(t, 1_000, false) == Settling);
        H.Check("still settling one ms inside the window", Pass(t, 1_000 + XboxStuckRootTracker.SettleMs - 1, false) == Settling);
        var e = t.Observe(1_000 + XboxStuckRootTracker.SettleMs, new[] { (Root, false) }).Single();
        H.Check("stuck once the window has elapsed, with its childless age", e.State == Stuck && e.ChildlessMs == XboxStuckRootTracker.SettleMs);
        H.Check("a stuck root stays stuck on later passes", Pass(t, 60_000, false) == Stuck);
        H.Check("children appearing ends the episode", Pass(t, 61_000, true) == Healthy);
        H.Check("a root that lost its children starts a fresh window", Pass(t, 62_000, false) == Settling);

        var g = new XboxStuckRootTracker();
        Pass(g, 0, false);
        g.Observe(5_000, new List<(string Id, bool HasChildren)>());
        H.Check("a root that left the tree is forgotten; its return restarts the window", Pass(g, 20_000, false) == Settling);

        var h = new XboxStuckRootTracker();
        H.Check("a root with children is healthy on first sight", Pass(h, 0, true) == Healthy);
    }
}
