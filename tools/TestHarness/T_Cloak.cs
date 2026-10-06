using System;
using System.Collections.Generic;
using System.Linq;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>A READ-ONLY view of the cloak's actual state, for the hard-kill / relaunch / --uncloak sequence
/// driven by tools\watchdog-state.ps1.
///
/// Deliberately reads and never writes. The HidHide allow-list is the subject of a known, expensive trap — a
/// bad write leaves the pad cloaked and Radiata not whitelisted, i.e. a blind controller needing an
/// elevated fix — so this asserts against the driver's own
/// blocklist and the app's persisted record, and mutates neither.
///
/// The control device is EXCLUSIVE: while Radiata holds it, the direct read fails and only the persisted
/// record is available. That's reported rather than papered over, because "couldn't read the driver" must not
/// look like "nothing is cloaked".</summary>
internal static class T_Cloak
{
    public static void Run()
    {
        H.Group("Cloak state (read-only)");

        var persisted = HidHideManager.PersistedCloakedIds();
        Console.WriteLine($"        (record) cloaked-ids.txt records {persisted.Count} id(s)");
        foreach (var id in persisted) Console.WriteLine($"        (record)   {id}");

        var (blocked, error) = DriverBlockedIds();
        if (error is not null)
            Console.WriteLine($"        (record) driver blocklist unreadable: {error} "
                              + "(expected while Radiata is running — the control device is exclusive)");
        else
        {
            Console.WriteLine($"        (record) driver blocklist holds {blocked.Count} id(s)");
            foreach (var id in blocked) Console.WriteLine($"        (record)   {id}");
        }

        // The one invariant worth asserting from outside: the app's record must never claim ids the driver
        // isn't actually blocking. The reverse is fine — another tool (or a foreign entry) may block more.
        if (error is null)
        {
            var orphaned = persisted.Where(p => !blocked.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
            H.Check("every id in cloaked-ids.txt is actually blocked in the driver",
                    orphaned.Count == 0,
                    orphaned.Count == 0 ? $"{persisted.Count} id(s) agree"
                                        : "recorded but NOT blocked: " + string.Join(", ", orphaned));
        }

        Console.WriteLine($"        (record) SUMMARY persisted={persisted.Count} "
                          + $"driverBlocked={(error is null ? blocked.Count.ToString() : "unreadable")}");
    }

    /// <summary>The driver's blocked-instance list, read through the app's own HidHide client. Returns the
    /// failure reason rather than throwing — an exclusive-open failure is information, not an error.</summary>
    private static (List<string> ids, string error) DriverBlockedIds()
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
