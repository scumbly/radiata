using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>Battery level for a Bluetooth pad whose own input report carries none
/// (<see cref="ControllerProfile.ReportsBattery"/> = false).
///
/// <para>The Bluetooth stack reads a BLE peripheral's GATT Battery Service (0x180F) itself and caches the
/// level as a PnP property on the pad's <c>BTHLE\DEV_…</c> devnode, reachable through CfgMgr32 — the same
/// API family <see cref="ControllerProfile.ShouldSkipHidDevice"/> already walks. Taking it from there avoids a
/// WinRT projection, which would mean a Windows-versioned target framework: a minimum-OS floor on Win10
/// (a first-class target) and a large projection assembly in the single-file publish, for one number.
/// A WinRT GATT read would also have to contend with the BLE HID driver already owning the service.</para>
///
/// <para>⚠ The property is not a documented API contract, so every miss MUST fail soft: null means
/// "unknown", which the readouts hide — indistinguishable from a pad that never reports a level.</para></summary>
internal static class BluetoothBattery
{
    // DEVPKEY_Bluetooth_Battery — a single byte, 0..100.
    private static readonly Guid BluetoothDeviceFmtId = new("104EA319-6EE2-4701-BD47-8DDBF425BBE5");
    private const uint BatteryPid = 2;
    private const uint DevPropTypeByte = 0x00000003;
    private const int  MaxParentWalk = 6;   // HID function → BTHLEDEVICE service → BTHLE\DEV_ → radio…

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public Guid Fmtid;
        public uint Pid;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Locate_DevNodeW(out uint dn, string id, uint flags);
    [DllImport("cfgmgr32.dll")]                            private static extern int CM_Get_Parent(out uint parent, uint dn, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_PropertyW(uint dn, ref DevPropKey key, out uint type,
                                                       [Out] byte[]? buffer, ref uint size, uint flags);

    /// <summary>Battery percent (0–100) for the pad whose HID interface has this PnP instance id, or null
    /// when it can't be determined — not a Bluetooth pad, property absent, or any lookup failure. The
    /// property lives on an ANCESTOR of the HID node (the per-device BTHLE node), so the chain is walked
    /// upward and the first node carrying the property wins.</summary>
    public static int? TryRead(string? hidInstanceId)
    {
        if (string.IsNullOrEmpty(hidInstanceId)) return null;
        try
        {
            if (CM_Locate_DevNodeW(out uint dn, hidInstanceId, 0) != 0) return null;
            for (int level = 0; level <= MaxParentWalk; level++)
            {
                if (ReadByteProperty(dn) is { } pct) return Math.Clamp(pct, 0, 100);
                if (CM_Get_Parent(out uint parent, dn, 0) != 0) return null;
                dn = parent;
            }
            return null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Battery] Bluetooth property lookup failed: {ex.Message}");
            return null;
        }
    }

    private static int? ReadByteProperty(uint dn)
    {
        var key = new DevPropKey { Fmtid = BluetoothDeviceFmtId, Pid = BatteryPid };
        var buf = new byte[1];
        uint size = 1;
        if (CM_Get_DevNode_PropertyW(dn, ref key, out uint type, buf, ref size, 0) != 0) return null;
        return type == DevPropTypeByte && size == 1 ? buf[0] : null;
    }
}
