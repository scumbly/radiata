using System;
using System.Linq;
using ControllerWheel;
using Nefarius.Drivers.HidHide;

namespace Radiata.TestHarness;

/// <summary>End-to-end proof of same-model orphan adoption against the REAL HidHide driver, using
/// SYNTHETIC instance ids (the block list accepts arbitrary strings; nothing real matches them, so
/// no device goes invisible). Recreates the orphaned-block shape: an id blocked with no record
/// naming it — written through the raw Nefarius service, exactly as a foreign tool or a lost
/// record leaves it — then a Hide of a same-model id on the "other transport".
///
/// ⚠ OPT-IN ONLY (`TestHarness adopt`), never in the default set: it WRITES the driver's block
/// list and the cloak record (unelevated — HidHide 1.5 list writes), and must not run while
/// Radiata is up (the tray's cloak bookkeeping would interleave with ours). Cleans up everything
/// it adds, pass or fail.</summary>
internal static class T_Adopt
{
    // Synthetic same-model pair (vid 054C / pid 0DF2) in the two real id shapes — the "deadbee…"
    // runs can't collide with a real devnode's enumeration path.
    private const string OrphanUsb = @"hid\vid_054c&pid_0df2&mi_03\9&deadbee1&0&0000";
    private const string LiveBt    = @"hid\{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0df2\9&deadbee2&0&0000";
    // Foreign model (different pid): must NOT be adopted.
    private const string Foreign   = @"hid\vid_054c&pid_0ce6\9&deadbee3&0&0000";

    private static readonly string[] AllSynthetic = { OrphanUsb, LiveBt, Foreign };

    public static void Run()
    {
        H.Group("Orphan adoption vs the real driver (synthetic ids)");

        HidHideControlService raw;
        try
        {
            raw = new HidHideControlService();
            if (!raw.IsInstalled) { H.Check("driver installed", false, "HidHide absent — group skipped"); return; }
            _ = raw.BlockedInstanceIds;   // control-device probe: contended/unreachable throws here
        }
        catch (Exception ex) { H.Check("driver reachable", false, ex.Message + " — group skipped"); return; }

        var hh = new HidHideManager();
        try
        {
            RemoveSynthetic(raw);   // hermetic start even after an earlier crashed run

            // Recordless blocks, as a foreign tool / a lost record would leave them: raw driver
            // writes that no cloak bookkeeping ever sees.
            raw.AddBlockedInstanceId(OrphanUsb);
            raw.AddBlockedInstanceId(Foreign);

            // The moment under test: cloak the BT id — adoption must claim the same-model USB
            // orphan and leave the foreign-model id alone.
            H.Check("Hide(BT id) succeeds", hh.Hide(new[] { LiveBt }));
            H.Check("BT id blocked in the driver",
                    raw.BlockedInstanceIds.Contains(LiveBt, StringComparer.OrdinalIgnoreCase));

            var record = HidHideManager.PersistedCloakedIds();
            H.Check("orphan ADOPTED into the record",
                    record.Contains(OrphanUsb, StringComparer.OrdinalIgnoreCase),
                    $"record: [{string.Join(", ", record)}]");
            H.Check("foreign-model id NOT adopted",
                    !record.Contains(Foreign, StringComparer.OrdinalIgnoreCase));

            // Teardown must now lift the orphan along with the live id — the whole point.
            hh.Unhide();
            var post = raw.BlockedInstanceIds;
            H.Check("orphan lifted by Unhide",
                    !post.Contains(OrphanUsb, StringComparer.OrdinalIgnoreCase));
            H.Check("live id lifted by Unhide",
                    !post.Contains(LiveBt, StringComparer.OrdinalIgnoreCase));
            H.Check("foreign-model id still blocked (ownership respected)",
                    post.Contains(Foreign, StringComparer.OrdinalIgnoreCase));

            // Pass-1 adoption: the id being cloaked is ITSELF already blocked with no record — the
            // lost-record shape (power loss / torn write). Must be claimed, recorded, and lifted.
            raw.AddBlockedInstanceId(LiveBt);
            H.Check("Hide(own already-blocked id) succeeds", hh.Hide(new[] { LiveBt }));
            var record2 = HidHideManager.PersistedCloakedIds();
            H.Check("own already-blocked id ADOPTED into the record",
                    record2.Contains(LiveBt, StringComparer.OrdinalIgnoreCase),
                    $"record: [{string.Join(", ", record2)}]");
            hh.Unhide();
            H.Check("own id lifted after Pass-1 adoption",
                    !raw.BlockedInstanceIds.Contains(LiveBt, StringComparer.OrdinalIgnoreCase));
            H.Check("foreign-model id still blocked after the second cycle",
                    raw.BlockedInstanceIds.Contains(Foreign, StringComparer.OrdinalIgnoreCase));
        }
        finally
        {
            // Leave the driver and the record exactly as found, pass or fail.
            try { hh.UnhideIds(AllSynthetic); } catch { }
            try { RemoveSynthetic(raw); } catch { }
        }
    }

    private static void RemoveSynthetic(HidHideControlService raw)
    {
        foreach (var id in AllSynthetic)
            if (raw.BlockedInstanceIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                raw.RemoveBlockedInstanceId(id);
    }
}
