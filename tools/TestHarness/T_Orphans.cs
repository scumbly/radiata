using System;
using System.Collections.Generic;
using System.Linq;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Clear ORPHANED HidHide blocklist entries — ids the driver is blocking that Radiata does not own
/// (they aren't in <c>cloaked-ids.txt</c>) and that the HidHide configuration client cannot show, because it
/// renders a checkbox per enumerable device and an entry whose devnode isn't enumerable has no row to tick.
///
/// <para>An orphan hides its device from games with no visible cause and no UI to fix it: a DualSense Edge
/// over Bluetooth blocked this way is simply invisible.</para>
///
/// <para><b>Direction matters.</b> This only ever REMOVES ids from the blocklist — it un-hides devices. The
/// expensive HidHide trap is the opposite direction (a device hidden while Radiata isn't on the allow-list =
/// a blind pad needing an elevated repair), and the allow-list is never touched here. Radiata re-cloaks the
/// ids it owns on its next launch, so this is reversible by restarting the app.</para>
///
/// <para>Run with Radiata stopped AND the HidHide configuration client closed — both take the control device
/// exclusively.</para></summary>
internal static class T_Orphans
{
    public static void Run(bool apply)
    {
        H.Group(apply ? "HidHide orphans — CLEARING" : "HidHide orphans — dry run");

        var owned = HidHideManager.PersistedCloakedIds();
        var (blocked, error) = Read();
        if (error is not null)
        {
            H.Fail("read the driver blocklist", error
                   + " — stop Radiata and close the HidHide configuration client first");
            return;
        }

        Console.WriteLine($"        (record) driver blocks {blocked.Count} id(s); Radiata owns {owned.Count}");
        var orphans = blocked.Where(b => !owned.Contains(b, StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (var id in blocked)
            Console.WriteLine($"        (record)   {(owned.Contains(id, StringComparer.OrdinalIgnoreCase) ? "OWNED  " : "ORPHAN ")}{id}");

        if (orphans.Count == 0) { H.Pass("no orphaned blocklist entries", $"{blocked.Count} id(s), all accounted for"); return; }
        if (!apply)
        {
            H.Skip("clearing orphans", $"{orphans.Count} would be removed — re-run with 'orphans:apply'");
            return;
        }

        using var t = new H.TraceGrab();
        var mgr = new HidHideManager();
        mgr.UnhideIds(orphans);
        mgr.Release();
        foreach (var l in t.Lines.Where(l => l.Contains("[HidHide]"))) Console.WriteLine("        " + l);

        var (after, err2) = Read();
        if (err2 is not null) { H.Fail("re-read the blocklist after clearing", err2); return; }

        H.Check("every orphan is gone from the driver blocklist",
                orphans.All(o => !after.Contains(o, StringComparer.OrdinalIgnoreCase)),
                $"{after.Count} id(s) remain: {(after.Count == 0 ? "none" : string.Join(", ", after))}");
        H.Check("nothing Radiata owns was removed",
                owned.All(o => after.Contains(o, StringComparer.OrdinalIgnoreCase)),
                $"{owned.Count} owned id(s)");
    }

    private static (List<string> ids, string error) Read()
    {
        try
        {
            var svc = new Nefarius.Drivers.HidHide.HidHideControlService();
            if (!svc.IsInstalled) return (new List<string>(), "HidHide is not installed");
            return (svc.BlockedInstanceIds.ToList(), null);
        }
        catch (Exception ex) { return (new List<string>(), $"{ex.GetType().Name}: {ex.Message}"); }
    }
}
