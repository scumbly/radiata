using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ControllerWheel;

/// <summary>
/// Enumerates the PnP devnodes of PHYSICAL XInput (Xbox-class) controllers so HidHide can cloak them.
///
/// Why this exists: an XInput pad's game-visible device is the XUSB function device under the
/// <b>XnaComposite</b> (Xbox 360) or <b>XboxComposite</b> (Xbox One/Series) setup class, and it NEVER
/// appears in a HID enumeration — the HID-derived instance ids of the Sony path
/// (<c>ControllerReader.GetHidInstanceIds</c>) hide nothing here. HidHide does install upper filters on both
/// Xbox classes, and per nefarius/HidHide#39 its block list needs the composite parent AND its child nodes,
/// so this returns the whole subtree per pad.
///
/// Wired/dongle pads come from the two Xbox composite classes; a genuine Xbox pad over BLUETOOTH
/// enumerates under HIDClass via xinputhid.sys instead, so it has its own sweep
/// (<see cref="GetBluetoothXboxInstanceIds"/>) — blocking that HID entry DOES starve XInput for
/// non-allow-listed processes (measured with XInputProbe: 0 pads blocked / 1 unblocked /
/// 1 blocked-but-allow-listed), the finding that overturned the old "BT can't be cloaked" prior.
///
/// <para><b>Multi-pad:</b> XInput hands out a SLOT, not a device identity, so nothing here can tell which of
/// several connected pads the reader is forwarding — cloaking is all-or-nothing across every physical
/// Xbox-class pad, while only one gets a virtual stand-in, which on a couch setup would make a second
/// player's controller disappear. The caller therefore refuses to cloak at all when <c>padCount &gt; 1</c>
/// and stays in best-effort mode; the real fix is device→slot identity, which a controller picker needs.</para>
/// </summary>
internal static class XboxDeviceTree
{
    // Setup class GUIDs HidHide filters (its recovery doc lists exactly these two Xbox classes).
    private static readonly Guid XnaComposite  = new("d61ca365-5af4-4486-998b-9db4734c6ca3");
    private static readonly Guid XboxComposite = new("05f5cfe2-4733-4950-a6bb-07aad01a3a84");

    /// <summary>Instance ids (parent + all descendants) of every physical Xbox-class pad present.
    /// Empty when none are connected, on any enumeration failure, or when the only Xbox-class devices
    /// are virtual (our own ViGEm X360 target enumerates as XnaComposite and MUST NOT be cloaked —
    /// it is the pad the game is supposed to see; <see cref="PnpAncestry"/> is the shared test that
    /// separates it). Never throws.</summary>
    public static IReadOnlyList<string> GetPhysicalXboxInstanceIds() =>
        GetPhysicalXboxInstanceIds(out _);

    /// <summary>As <see cref="GetPhysicalXboxInstanceIds()"/>, and reports how many DISTINCT PADS those ids
    /// belong to via <paramref name="padCount"/> (one per composite root; the rest of the list is their child
    /// PDOs). The caller needs the pad count, not the id count, because cloaking is all-or-nothing here while
    /// only ONE pad gets forwarded to the virtual device — see the multi-pad note in the type summary.</summary>
    public static IReadOnlyList<string> GetPhysicalXboxInstanceIds(out int padCount) =>
        GetPhysicalXboxInstanceIds(out padCount, out _);

    /// <summary>As above, and separates the two causes of an empty result via
    /// <paramref name="enumFailed"/>: true means the SWEEP failed (SetupDi handle/exception), so "empty"
    /// says nothing about what's connected — a caller holding a cloak must treat that as an outage to
    /// retry, never as "no pad = best-effort by design". False + empty = genuinely nothing enumerable.</summary>
    public static IReadOnlyList<string> GetPhysicalXboxInstanceIds(out int padCount, out bool enumFailed)
    {
        // Same 250 ms memo the Sony path carries (ControllerReader._hidIdCache): a capture pass runs on the
        // wheel-open and slice-fire path, and a burst of passes in one gesture collapses to one SetupAPI
        // sweep. Failures are never cached — the next pass must retry.
        long now = Environment.TickCount64;
        if (Volatile.Read(ref _wiredCache) is { } hit && now - hit.StampMs < CacheMs)
        {
            padCount = hit.Count; enumFailed = false;
            return hit.Ids;
        }
        var ids = new List<string>(8);
        var found = new List<(string Id, bool HasChildren)>(2);
        int roots = 0;
        enumFailed = false;
        var notes = BeginNotes();
        try
        {
            if (!CollectClass(XnaComposite,  ids, ref roots, found)) enumFailed = true;
            if (!CollectClass(XboxComposite, ids, ref roots, found)) enumFailed = true;
        }
        catch (Exception ex)
        {
            enumFailed = true;
            Trace.WriteLine($"[XboxTree] enumeration failed: {ex.Message}");
        }
        FlushNotes(notes, ref _lastWiredTrace);
        if (enumFailed) { ids.Clear(); roots = 0; }
        else
        {
            // Childless roots were left out of ids/roots by CollectClass; the tracker decides which are
            // merely settling and which are stuck, and the stuck ones are published for the caller.
            var entries = _stuckTracker.Observe(Environment.TickCount64, found);
            Volatile.Write(ref _stuckRoots, entries.Where(e => e.State == XboxStuckRootTracker.RootState.Stuck).ToList());
        }
        padCount = roots;
        if (!enumFailed) Volatile.Write(ref _wiredCache, new Sweep(now, ids, roots));
        return ids;
    }

    private static readonly XboxStuckRootTracker _stuckTracker = new();
    private static IReadOnlyList<XboxStuckRootTracker.Entry> _stuckRoots = Array.Empty<XboxStuckRootTracker.Entry>();

    /// <summary>Roots from the latest successful sweep that are present but never grew an input child
    /// for <see cref="XboxStuckRootTracker.SettleMs"/> (see <see cref="XboxStuckRootTracker"/>). They are
    /// excluded from the ids and the pad count of <see cref="GetPhysicalXboxInstanceIds(out int, out bool)"/>,
    /// as are roots still inside the settle window, so this is the only place a caller learns of them.
    /// A failed sweep leaves the previous value standing.</summary>
    public static IReadOnlyList<XboxStuckRootTracker.Entry> StuckRoots => Volatile.Read(ref _stuckRoots);

    // ── memo + change-only tracing ───────────────────────────────────────────

    private sealed record Sweep(long StampMs, IReadOnlyList<string> Ids, int Count);
    private static Sweep? _wiredCache, _btCache;
    private const int CacheMs = 250;

    /// <summary>The per-devnode lines of one sweep are written only when they differ from the previous
    /// sweep's. A steady state — the common one, ~20,000 passes a day on a box with a pad attached — logs
    /// once; a genuine device change still logs in full. The same latch <c>XInputInterop.FindPhysical</c>
    /// uses, for the same reason: these lines were 88 % of a long idle trace and cut its retention to hours.</summary>
    [ThreadStatic] private static List<string>? _notes;
    private static string? _lastWiredTrace, _lastBtTrace;

    private static List<string> BeginNotes() { _notes = new List<string>(8); return _notes; }
    private static void Note(string line) { if (_notes is { } n) n.Add(line); else Trace.WriteLine(line); }
    private static void FlushNotes(List<string> notes, ref string? last)
    {
        _notes = null;
        string sig = string.Join('\n', notes);
        if (sig == last) return;
        last = sig;
        foreach (var line in notes) Trace.WriteLine(line);
    }

    private static bool CollectClass(Guid classGuid, List<string> ids, ref int roots, List<(string Id, bool HasChildren)> found)
    {
        var set = SetupDiGetClassDevs(in classGuid, null, IntPtr.Zero, DIGCF_PRESENT);
        if (set == INVALID_HANDLE_VALUE) return false;
        try
        {
            var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            for (uint i = 0; ; i++)
            {
                if (!SetupDiEnumDeviceInfo(set, i, ref data))
                {
                    if (Marshal.GetLastWin32Error() != 259) throw new InvalidOperationException("Device enumeration stopped before completion.");
                    break; // ERROR_NO_MORE_ITEMS
                }
                var sb = new StringBuilder(MaxDeviceIdLen);
                if (!SetupDiGetDeviceInstanceId(set, ref data, sb, sb.Capacity, out _)) throw new InvalidOperationException("Unreadable device instance identity.");
                string rootId = sb.ToString();

                var verdict = PnpAncestry.Classify(rootId);

                if (verdict.Virtual)
                {
                    Note($"[XboxTree] skipping virtual pad: {rootId}  (matched: {verdict.Detail})");
                    continue;
                }

                // The parent and the verdict detail are what a misclassification report needs: a pad
                // judged physical for want of a readable chain says so here instead of vanishing into
                // the multi-pad guard.
                Note($"[XboxTree] Xbox-class candidate: {rootId}  (parent: {verdict.Parent ?? "?"}"
                     + (verdict.Detail == "physical" ? ")" : $"; {verdict.Detail})"));
                if (found.Exists(f => string.Equals(f.Id, rootId, StringComparison.OrdinalIgnoreCase))) continue;
                // A root with no children is held out of the pad list until the tracker has judged it:
                // it is neither cloaked nor counted as a pad.
                var children = new List<string>(4);
                CollectDescendants(rootId, children);
                found.Add((rootId, children.Count > 0));
                if (children.Count == 0) continue;
                ids.Add(rootId); roots++;
                foreach (var c in children) if (!ids.Contains(c, StringComparer.OrdinalIgnoreCase)) ids.Add(c);
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return true;
    }

    // HIDClass setup class — where a Bluetooth Xbox pad's game-visible devnode lives (via xinputhid.sys).
    private static readonly Guid HidClass = new("745a17a0-74d3-11d0-b6fe-00a0c90f57da");

    /// <summary>Instance ids of every BLUETOOTH Xbox-class pad's XInput-compatible HID devnode — the
    /// entry HidHide must block to starve XInput for other processes. Selection is structural, never
    /// VID/PID (the virtual pad is byte-identical to a real 360 pad at 045E:028E):
    /// a present HIDClass device whose instance id carries the <c>IG_</c> XInput-compatibility marker,
    /// whose ancestry is Bluetooth (<c>BTH…</c>), and which is not a software-bus (virtual) devnode.
    /// Wired pads' IG_ HID children hang under the XUSB composite (USB ancestry) and are excluded here —
    /// the composite tree above already covers them. One id per pad, so the count doubles as the pad
    /// count for the multi-pad guard. Empty on none / any enumeration failure; <paramref
    /// name="enumFailed"/> separates the two — see <see cref="GetPhysicalXboxInstanceIds(out int, out bool)"/>.
    /// Never throws.</summary>
    public static IReadOnlyList<string> GetBluetoothXboxInstanceIds(out int btPadCount) =>
        GetBluetoothXboxInstanceIds(out btPadCount, out _);

    public static IReadOnlyList<string> GetBluetoothXboxInstanceIds(out int btPadCount, out bool enumFailed)
    {
        long now = Environment.TickCount64;
        if (Volatile.Read(ref _btCache) is { } hit && now - hit.StampMs < CacheMs)
        {
            btPadCount = hit.Count; enumFailed = false;
            return hit.Ids;
        }
        var ids = new List<string>(2);
        enumFailed = false;
        var notes = BeginNotes();
        try
        {
            var set = SetupDiGetClassDevs(in HidClass, null, IntPtr.Zero, DIGCF_PRESENT);
            if (set == INVALID_HANDLE_VALUE) enumFailed = true;
            else
            {
                try
                {
                    var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                    for (uint i = 0; ; i++)
                    {
                        if (!SetupDiEnumDeviceInfo(set, i, ref data))
                        {
                            if (Marshal.GetLastWin32Error() != 259) throw new InvalidOperationException("Device enumeration stopped before completion.");
                            break; // ERROR_NO_MORE_ITEMS
                        }
                        var sb = new StringBuilder(MaxDeviceIdLen);
                        if (!SetupDiGetDeviceInstanceId(set, ref data, sb, sb.Capacity, out _)) throw new InvalidOperationException("Unreadable device instance identity.");
                        string id = sb.ToString();
                        if (id.IndexOf("IG_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var verdict = PnpAncestry.Classify(id);

                        if (verdict.Virtual)
                        {
                            Note($"[XboxTree] skipping virtual XInput HID devnode: {id}  (matched: {verdict.Detail})");
                            continue;
                        }
                        if (!HasBluetoothAncestor(id)) continue;   // wired child — the composite tree owns it
                        if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                        {
                            ids.Add(id);
                            Note($"[XboxTree] Bluetooth Xbox-class candidate (HID): {id} ({verdict.Detail})");
                        }
                    }
                }
                finally { SetupDiDestroyDeviceInfoList(set); }
            }
        }
        catch (Exception ex)
        {
            enumFailed = true;
            Trace.WriteLine($"[XboxTree] BT enumeration failed: {ex.Message}");
        }
        FlushNotes(notes, ref _lastBtTrace);
        if (enumFailed) ids.Clear();
        btPadCount = ids.Count;
        if (!enumFailed) Volatile.Write(ref _btCache, new Sweep(now, ids, ids.Count));
        return ids;
    }

    /// <summary>True when every id names a devnode configured in the device tree right now (a
    /// <c>CM_LOCATE_DEVNODE_NORMAL</c> lookup, which fails for a non-present devnode). Deliberately
    /// unmemoised: it must see a departure that the 250 ms sweep memo would still hide. Empty = false.</summary>
    public static bool AllPresent(IReadOnlyCollection<string> instanceIds) =>
        instanceIds.Count > 0 && instanceIds.All(id => NativeMethods.CM_Locate_DevNode(out _, id, 0) == CR_SUCCESS);

    /// <summary>Whether any ancestor devnode is Bluetooth-enumerated (<c>BTHENUM\</c> classic,
    /// <c>BTHLE…\</c> LE/GATT) — the transport test that separates a BT pad's HID entry from a wired
    /// pad's HID child without touching VID/PID.</summary>
    private static bool HasBluetoothAncestor(string instanceId)
    {
        if (NativeMethods.CM_Locate_DevNode(out uint node, instanceId, 0) != CR_SUCCESS) return false;
        for (int depth = 0; depth < 24 && NativeMethods.CM_Get_Parent(out uint parent, node, 0) == CR_SUCCESS; depth++)
        {
            var id = DevNodeId(parent);
            if (id is null) break;
            if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return true;
            node = parent;
        }
        return false;
    }

    /// <summary>Depth-first walk of a devnode's children (the XUSB composite exposes HID/audio child
    /// PDOs; HidHide needs those blocked too — nefarius/HidHide#39's "hide HID and USB" rule).</summary>
    private static void CollectDescendants(string rootId, List<string> ids)
    {
        if (NativeMethods.CM_Locate_DevNode(out uint root, rootId, 0) != CR_SUCCESS) return;
        var stack = new Stack<uint>();
        if (CM_Get_Child(out uint first, root, 0) == CR_SUCCESS) stack.Push(first);
        int guard = 0;
        while (stack.Count > 0 && ++guard < 64)
        {
            uint node = stack.Pop();
            if (DevNodeId(node) is { } id)
            {
                if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase)) ids.Add(id);
                Note($"[XboxTree]   child: {id}");
            }
            if (CM_Get_Child(out uint child, node, 0) == CR_SUCCESS) stack.Push(child);
            if (CM_Get_Sibling(out uint sib, node, 0) == CR_SUCCESS) stack.Push(sib);
        }
    }

    private static string? DevNodeId(uint node)
    {
        var sb = new StringBuilder(MaxDeviceIdLen);
        return NativeMethods.CM_Get_Device_ID(node, sb, sb.Capacity, 0) == CR_SUCCESS ? sb.ToString() : null;
    }

    // ── interop ──────────────────────────────────────────────────────────────

    private const int MaxDeviceIdLen = 400;   // MAX_DEVICE_ID_LEN is 200; double it for safety
    private const uint DIGCF_PRESENT = 0x02;
    private const uint CR_SUCCESS = 0;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA { public uint cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(in Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    // CM_Locate_DevNode / CM_Get_Parent / CM_Get_Device_ID are NativeMethods.CM_Locate_DevNode etc.
    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Child(out uint child, uint devInst, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Sibling(out uint sibling, uint devInst, uint flags);
}
