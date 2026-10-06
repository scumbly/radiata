using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>Reads and toggles display HDR via the Windows CCD API, across API generations and monitors:
///
/// • **Win11 24H2+**: probes <c>GET_ADVANCED_COLOR_INFO_2</c> (type 15) — its <c>activeColorMode</c>
///   discriminates SDR / WCG / HDR exactly, and <c>SET_HDR_STATE</c> (type 16) targets HDR unambiguously.
/// • **Win10 / pre-24H2**: falls back to the legacy pair (types 9/10). Legacy "advancedColorEnabled" means
///   "a TYPE of advanced color is on" — on 22H2/23H2 an SDR display with Automatic Color Management reports
///   it TRUE with <c>wideColorEnforced</c> set, so HDR-on is read as <c>enabled &amp;&amp; !wideColorEnforced</c>
///   (the bug class that broke every legacy-only HDR utility under ACM).
/// • **Multi-monitor**: IsEnabled = ANY capable display has HDR active; Toggle drives EVERY capable display
///   to the same new state (the flip of that any-state) — one button, coherent result, like Win+Alt+B.
///
/// All methods return null when nothing HDR-capable is present or every call failed.</summary>
internal static class HdrState
{
    private static bool _generationTraced;   // one-shot: log which API generation this machine uses

    public static bool? IsEnabled()
    {
        try
        {
            if (!QueryPaths(out var paths, out uint pathCount)) return null;
            bool anySupported = false, anyActive = false;
            for (int i = 0; i < pathCount; i++)
            {
                if (!TryGetPath(paths[i].targetInfo.adapterId, paths[i].targetInfo.id,
                                out bool supported, out bool active)) continue;
                anySupported |= supported;
                anyActive    |= supported && active;
            }
            return anySupported ? anyActive : null;
        }
        catch { return null; }
    }

    /// <summary>Flip HDR on every HDR-capable active display (all driven to the same new state).
    /// Returns the NEW state, or null if there's no capable display or every set call failed.</summary>
    public static bool? Toggle()
    {
        try
        {
            if (!QueryPaths(out var paths, out uint pathCount)) return null;

            // Pass 1: what exists + the current any-state (the toggle pivot).
            var capable = new List<(LUID adapter, uint id)>();
            bool anyActive = false;
            for (int i = 0; i < pathCount; i++)
            {
                if (!TryGetPath(paths[i].targetInfo.adapterId, paths[i].targetInfo.id,
                                out bool supported, out bool active) || !supported) continue;
                capable.Add((paths[i].targetInfo.adapterId, paths[i].targetInfo.id));
                anyActive |= active;
            }
            if (capable.Count == 0) return null;

            // Pass 2: drive every capable display to the same new state.
            bool newState = !anyActive, anySet = false;
            foreach (var (adapter, id) in capable)
                anySet |= TrySetPath(adapter, id, newState);
            return anySet ? newState : (bool?)null;
        }
        catch { return null; }
    }

    /// <summary>Drive every HDR-capable display to <paramref name="on"/> (not a flip). Used by the
    /// --hdr-probe harness to restore a snapshotted state; the wheel action stays on Toggle().</summary>
    internal static bool? SetAll(bool on)
    {
        try
        {
            if (!QueryPaths(out var paths, out uint pathCount)) return null;
            bool anySet = false;
            for (int i = 0; i < pathCount; i++)
            {
                if (!TryGetPath(paths[i].targetInfo.adapterId, paths[i].targetInfo.id,
                                out bool supported, out _) || !supported) continue;
                anySet |= TrySetPath(paths[i].targetInfo.adapterId, paths[i].targetInfo.id, on);
            }
            return anySet ? on : (bool?)null;
        }
        catch { return null; }
    }

    /// <summary>One line per active display target for the --hdr-probe report: capability + active
    /// state + which API generation answered.</summary>
    internal static string InventoryReport()
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            if (!QueryPaths(out var paths, out uint pathCount)) return "QueryDisplayConfig failed";
            for (int i = 0; i < pathCount; i++)
            {
                var a = paths[i].targetInfo.adapterId; uint id = paths[i].targetInfo.id;
                bool got = TryGetPath(a, id, out bool supported, out bool active);
                sb.AppendLine($"  target {i}: adapter={a.HighPart:X}:{a.LowPart:X} id={id} " +
                              (got ? $"hdrCapable={supported} hdrActive={active}" : "query FAILED"));
            }
        }
        catch (Exception ex) { sb.AppendLine("  inventory failed: " + ex.Message); }
        return sb.ToString().TrimEnd();
    }

    // ── Per-path get/set across API generations ────────────────────────────────

    /// <summary>HDR capability + active state for one target: INFO_2 (24H2+) first, legacy fallback.</summary>
    private static bool TryGetPath(LUID adapter, uint id, out bool supported, out bool active)
    {
        supported = active = false;

        var info2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2,
                            Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(), adapter, id),
        };
        if (GetDeviceInfo2(ref info2) == 0)
        {
            TraceGeneration(info2Available: true);
            supported = (info2.value & 0x10) != 0;                    // highDynamicRangeSupported (bit 4)
            active    = info2.activeColorMode == ADVANCED_COLOR_MODE_HDR;
            return true;
        }

        var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                            Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(), adapter, id),
        };
        if (DisplayConfigGetDeviceInfo(ref info) != 0) return false;
        TraceGeneration(info2Available: false);
        bool enabled           = (info.value & 0x2) != 0;             // advancedColorEnabled
        bool wideColorEnforced = (info.value & 0x4) != 0;             // ACM-SDR marker (22H2/23H2)
        supported = (info.value & 0x1) != 0;                          // advancedColorSupported
        active    = enabled && !wideColorEnforced;
        return true;
    }

    /// <summary>Set one target's HDR state: SET_HDR_STATE (24H2+) first, legacy fallback.</summary>
    private static bool TrySetPath(LUID adapter, uint id, bool on)
    {
        var hdr = new DISPLAYCONFIG_SET_HDR_STATE
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE,
                            Marshal.SizeOf<DISPLAYCONFIG_SET_HDR_STATE>(), adapter, id),
            value  = on ? 1u : 0u,                                    // bit0 = enableHdr
        };
        if (SetHdrState(ref hdr) == 0) return true;

        var set = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = Header(DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                            Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(), adapter, id),
            value  = on ? 1u : 0u,
        };
        return DisplayConfigSetDeviceInfo(ref set) == 0;
    }

    private static bool QueryPaths(out DISPLAYCONFIG_PATH_INFO[] paths, out uint pathCount)
    {
        paths = []; pathCount = 0;
        if (NativeMethods.GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out uint modeCount) != 0)
            return false;
        paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        return QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) == 0;
    }

    private static DISPLAYCONFIG_DEVICE_INFO_HEADER Header(uint type, int size, LUID adapter, uint id) =>
        new() { type = type, size = (uint)size, adapterId = adapter, id = id };

    private static void TraceGeneration(bool info2Available)
    {
        if (_generationTraced) return;
        _generationTraced = true;
        Trace.WriteLine(info2Available
            ? "[HDR] using GET_ADVANCED_COLOR_INFO_2 / SET_HDR_STATE (Win11 24H2+ API)"
            : "[HDR] using legacy ADVANCED_COLOR_INFO API (pre-24H2); ACM-SDR displays excluded via wideColorEnforced");
    }

    // ── CCD interop ─────────────────────────────────────────────────────────────
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO   = 9;    // legacy get
    private const uint DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE  = 10;   // legacy set
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 = 15;   // Win11 24H2+
    private const uint DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE             = 16;   // Win11 24H2+
    private const int  ADVANCED_COLOR_MODE_HDR = 2;   // DISPLAYCONFIG_ADVANCED_COLOR_MODE: SDR=0 WCG=1 HDR=2

    [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_2DREGION { public uint cx; public uint cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId; public uint id; public uint modeInfoIdx;
        public uint outputTechnology; public uint rotation; public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate; public uint scanLineOrdering;
        public int targetAvailable; public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }

    // Sized to the union's largest member (DISPLAYCONFIG_TARGET_MODE = 48 bytes) so the array
    // marshals at the right stride; we never read mode contents.
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate; public DISPLAYCONFIG_RATIONAL hSyncFreq; public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize; public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard; public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO { public uint infoType; public uint id; public LUID adapterId; public DISPLAYCONFIG_VIDEO_SIGNAL_INFO mode; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }

    // Legacy (type 9). value bits: 0 advancedColorSupported, 1 advancedColorEnabled, 2 wideColorEnforced,
    // 3 advancedColorForceDisabled.
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public int  colorEncoding;
        public uint bitsPerColorChannel;
    }

    // 24H2+ (type 15). value bits: 0 advancedColorSupported, 1 advancedColorActive, 3 limitedByPolicy,
    // 4 highDynamicRangeSupported, 5 highDynamicRangeUserEnabled, 6 wideColorSupported, 7 wideColorUserEnabled.
    // activeColorMode: SDR=0, WCG=1, HDR=2 — the unambiguous discriminator.
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public int  colorEncoding;
        public uint bitsPerColorChannel;
        public int  activeColorMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;              // bit0 = enableAdvancedColor
    }

    // 24H2+ (type 16): targets HDR specifically (never flips ACM/WCG).
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SET_HDR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;              // bit0 = enableHdr
    }

    // GetDisplayConfigBufferSizes is NativeMethods.GetDisplayConfigBufferSizes (identical signature).
    // QueryDisplayConfig stays local: NativeMethods' own declaration serves QDC_DATABASE_CURRENT (which
    // requires a non-null topology pointer, `out uint`) against its own opaque path/mode struct pair; this
    // one serves QDC_ONLY_ACTIVE_PATHS (which requires a NULL topology pointer, plain `IntPtr`) against the
    // richly-typed structs this file reads fields from. Moving it would mean renaming one of the two
    // same-named struct pairs — left as a documented near-duplicate rather than risking that here.
    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int GetDeviceInfo2(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")]
    private static extern int SetHdrState(ref DISPLAYCONFIG_SET_HDR_STATE setPacket);
}
