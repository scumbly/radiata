using System.Runtime.InteropServices;
using System.Text;

namespace ControllerWheel;

/// <summary>
/// The one virtual-pad fingerprint both capture paths use (XInput composite roots in <see cref="XboxDeviceTree"/>,
/// raw-HID devices in <see cref="ControllerProfile.ShouldSkipHidDevice"/>). Decided by PnP ancestry, never by VID/PID:
/// the ViGEm X360 target is byte-identical to a wired Xbox 360 pad at 045E:028E and the virtual DS4 to a real
/// DS4 v1, so only the bus a devnode hangs from can tell them apart.
///
/// <para>A software bus does not have one shape. Nefarius's installer creates <c>ROOT\SYSTEM\000N</c>; forks ship
/// under other roots (Oculus: <c>ROOT\SYSTEM</c> with service <c>Oculus_ViGEmBus</c>) or as Software Device
/// Framework nodes parented to a vendor's own ACPI driver (HP OMEN Gaming Hub:
/// <c>SWD\DRIVERENUM\{…}#HPIncVigemBusOmenFusionSoftware…</c>, service <c>ViGEmBus</c>, whose chain then tops out at
/// <c>ROOT\ACPI_HAL</c> exactly like a physical pad's). A <c>ROOT\SYSTEM</c>-only test called that pad physical,
/// tripped the multi-pad guard on Radiata's own stand-in and toggled the virtual pad every three seconds. Any ONE
/// of these marks a devnode virtual:</para>
/// <list type="bullet">
/// <item>an ancestor whose <c>Service</c> contains <c>VIGEM</c> (catches every ViGEmBus build and fork by the
/// driver it actually loads);</item>
/// <item>an ancestor whose instance id contains <c>VIGEM</c>;</item>
/// <item>the DIRECT parent under <c>ROOT\</c> or <c>SWD\</c> (root-enumerated or software-enumerated bus of
/// any make — ScpVBus, vJoy, Virtual Desktop's vdvge). Only the direct parent: every physical chain ends at
/// <c>ROOT\ACPI_HAL\0000</c>, and a forked bus can sit under <c>ACPI\…</c> nodes;</item>
/// <item>any ancestor under <c>ROOT\SYSTEM</c> (the original Nefarius shape).</item>
/// </list>
/// <para>Unreadable ancestry returns Known=false. The compatibility fallback admits unknown devices;
/// it does not establish physical identity. Detail records the failed step.</para>
/// </summary>
internal static class PnpAncestry
{
    public readonly record struct Verdict(bool Virtual, string Detail, string? Parent, bool Known = true);

    private const int MaxDepth = 24;
    private const int MaxDeviceIdLen = 400;
    private const uint CR_SUCCESS = 0;

    /// <summary>Classify a devnode by its ancestry. <paramref name="instanceId"/> is a PnP device instance id
    /// (<c>USB\VID_…\…</c>), as SetupDi and the HidSharp path both yield.</summary>
    public static Verdict Classify(string instanceId)
    {
        try
        {
            uint cr = NativeMethods.CM_Locate_DevNode(out uint node, instanceId, 0);
            if (cr != CR_SUCCESS) return new(false, $"ancestry unreadable: CM_Locate_DevNode CR_{cr}", null, false);

            string? directParent = null;
            for (int depth = 0; depth < MaxDepth; depth++)
            {
                if (string.Equals(DevNodeId(node), @"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                    return new(false, "physical", directParent);
                cr = NativeMethods.CM_Get_Parent(out uint parent, node, 0);
                if (cr != CR_SUCCESS)
                {
                    // The top of the tree (HTREE\ROOT\0) has no parent — an ordinary end of walk, not a fault.
                    return new(false, $"ancestry unreadable: CM_Get_Parent CR_{cr}", directParent, false);
                }
                var id = DevNodeId(parent);
                if (id is null) return new(false, "ancestry unreadable: CM_Get_Device_ID failed", directParent, false);
                if (depth == 0) directParent = id;

                var service = DevNodeService(parent);
                if (service is not null && service.Contains("VIGEM", StringComparison.OrdinalIgnoreCase))
                    return new(true, $"ancestor service {service}", directParent);
                if (id.Contains("VIGEM", StringComparison.OrdinalIgnoreCase))
                    return new(true, "VIGEM in ancestor id", directParent);
                if (depth == 0 && (id.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase)
                                   || id.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase)))
                    return new(true, "software-bus parent", directParent);
                if (id.StartsWith(@"ROOT\SYSTEM", StringComparison.OrdinalIgnoreCase))
                    return new(true, @"ROOT\SYSTEM ancestor", directParent);
                node = parent;
            }
            return new(false, "ancestry exceeded depth limit", directParent, false);
        }
        catch (Exception ex)
        {
            return new(false, $"ancestry unreadable: {ex.GetType().Name}", null, false);
        }
    }

    private static string? DevNodeId(uint node)
    {
        var sb = new StringBuilder(MaxDeviceIdLen);
        return NativeMethods.CM_Get_Device_ID(node, sb, sb.Capacity, 0) == CR_SUCCESS ? sb.ToString() : null;
    }

    // DEVPKEY_Device_Service = {a45c254e-df1c-4efd-8020-67d146a850e0}, 6 — the driver service a devnode loads.
    private static readonly DEVPROPKEY DevpkeyService = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 6,
    };

    private static string? DevNodeService(uint node)
    {
        var key = DevpkeyService;
        var buf = new byte[512];
        uint size = (uint)buf.Length;
        uint cr = CM_Get_DevNode_Property(node, ref key, out _, buf, ref size, 0);
        if (cr != CR_SUCCESS || size < 2) return null;   // no Service property (a raw PDO) or an oversize value
        return Encoding.Unicode.GetString(buf, 0, (int)size).TrimEnd('\0');
    }

    // ── interop ──────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY { public Guid fmtid; public uint pid; }

    // CM_Locate_DevNode / CM_Get_Parent / CM_Get_Device_ID are NativeMethods.CM_Locate_DevNode etc.
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_DevNode_PropertyW")]
    private static extern uint CM_Get_DevNode_Property(uint devInst, ref DEVPROPKEY propertyKey, out uint propertyType,
                                                       byte[] buffer, ref uint bufferLen, uint flags);
}
