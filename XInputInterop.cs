using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>Minimal XInput P/Invoke for readable Xbox-compatible slots, physical or virtual — the backend
/// for <see cref="ControllerKind.Xbox"/>. Selection does not establish a slot's hardware or transport
/// identity. Devices use this path whenever their driver exposes XInput, including supported Bluetooth
/// configurations; raw Sony HID is handled separately by <see cref="ControllerReader"/>.
///
/// Uses xinput1_4.dll (Windows 8+). Guide/Home is outside the documented XInputGetState button contract,
/// so we try undocumented ordinal #100 (XInputGetStateEx), falling back to XInputGetState
/// (Guide unavailable) if the entry point cannot be called. The DEFAULT
/// trigger (Bumper + Back/Start) doesn't need Guide, so the fallback only degrades USER-CHOSEN Home chords —
/// it still traces loudly so a "my Home chord won't fire" report is diagnosable from the log.</summary>
internal static class XInputInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort wButtons;
        public byte   bLeftTrigger;
        public byte   bRightTrigger;
        public short  sThumbLX;
        public short  sThumbLY;
        public short  sThumbRX;
        public short  sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState { public uint dwPacketNumber; public XInputGamepad Gamepad; }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation { public byte BatteryType; public byte BatteryLevel; }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration { public ushort wLeftMotorSpeed; public ushort wRightMotorSpeed; }

    // Button bits (xinput.h wButtons mask).
    public const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
    public const ushort Start  = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080;
    public const ushort LeftShoulder = 0x0100, RightShoulder = 0x0200, Guide = 0x0400;
    public const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    private const uint ERROR_SUCCESS = 0;
    private const byte BATTERY_DEVTYPE_GAMEPAD   = 0x00;
    private const byte BATTERY_TYPE_DISCONNECTED = 0x00;
    private const byte BATTERY_TYPE_WIRED        = 0x01;

    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out XInputState state);
    // Ordinal #100 = XInputGetStateEx — identical to XInputGetState but the Guide bit isn't masked off.
    [DllImport("xinput1_4.dll", EntryPoint = "#100")] private static extern uint XInputGetStateEx(uint index, out XInputState state);
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetBatteryInformation(uint index, byte devType, out XInputBatteryInformation info);
    [DllImport("xinput1_4.dll")] private static extern uint XInputSetState(uint index, ref XInputVibration vibration);

    private static bool _exMissing;   // XInputGetStateEx unavailable → stop trying it

    /// <summary>Read one slot. Returns false on disconnection or another API failure. <paramref name="packet"/>
    /// changes only when the input state changes, so callers can skip redundant work.</summary>
    public static bool TryGetState(int index, out XInputGamepad pad, out uint packet)
    {
        pad = default; packet = 0;
        XInputState st;
        uint r;
        if (!_exMissing)
        {
            try { r = XInputGetStateEx((uint)index, out st); pad = st.Gamepad; packet = st.dwPacketNumber; return r == ERROR_SUCCESS; }
            catch
            {
                // Ordinal not exported here — fall through to the public entry. Loud on purpose: without
                // #100 the Guide bit is always false, so any user-chosen Home chord silently can't fire.
                _exMissing = true;
                System.Diagnostics.Trace.WriteLine(
                    "[XInput] XInputGetStateEx (#100) unavailable — Guide/Home unreadable; Home chords (e.g. Bumper+Home) will NOT work on this pad. Pick a non-Home trigger in Settings.");
            }
        }
        try { r = XInputGetState((uint)index, out st); }
        catch { return false; }
        pad = st.Gamepad; packet = st.dwPacketNumber;
        return r == ERROR_SUCCESS;
    }

    /// <summary>Bitmask of slots readable during this scan (bit N = slot N). The four reads are sequential,
    /// not an atomic topology snapshot. Connect-window differences support a virtual-slot inference;
    /// they do not prove ownership when devices or remapping tools change concurrently.</summary>
    public static int ConnectedMask()
    {
        int mask = 0;
        for (int i = 0; i < 4; i++)
            if (TryGetState(i, out _, out _)) mask |= 1 << i;
        return mask;
    }

    // Change-only trace latch for FindPhysical: the idle read loop probes every ~2s for as long as no
    // pad is attached (this app's normal overnight state), and per-poll lines were ~99% of a long idle
    // trace — rotating real evidence out of the log. Read-loop thread only.
    private static string? _lastProbeTrace;

    /// <summary>The first connected XInput slot other than <paramref name="exclude"/> (our own virtual
    /// pad's slot in XBox Mode), or -1 if none. Despite the legacy name, the slot can be another tool's
    /// virtual output. The caller is responsible for a reliable exclusion.
    /// Traces one summary line per state CHANGE, never per poll.</summary>
    public static int FindPhysical(int? exclude)
    {
        int slot = -1;
        var parts = new string[4];
        for (int i = 0; i < 4; i++)
        {
            if (exclude is int ex && ex == i) { parts[i] = $"{i}=excluded"; continue; }
            bool connected = TryGetState(i, out _, out _);
            parts[i] = $"{i}={(connected ? "connected" : "empty")}";
            if (connected && slot < 0) slot = i;
        }
        string sig = $"[XI] probe: {string.Join(" ", parts)} -> slot {slot}";
        if (sig != _lastProbeTrace)
        {
            _lastProbeTrace = sig;
            System.Diagnostics.Trace.WriteLine(sig);
        }
        return slot;
    }

    /// <summary>Rumble a physical XInput pad — the forwarding path for a ViGEm virtual pad's
    /// FeedbackReceived (games rumble the virtual pad; nothing reads that on its own). Best-effort;
    /// false (no throw) if the slot is empty or the call fails.</summary>
    public static bool SetVibration(int slot, ushort left, ushort right)
    {
        var vib = new XInputVibration { wLeftMotorSpeed = left, wRightMotorSpeed = right };
        try { return XInputSetState((uint)slot, ref vib) == ERROR_SUCCESS; }
        catch { return false; }
    }

    /// <summary>Battery for a slot as (percent, charging). Wired/disconnected → treated as full; a
    /// wireless pack maps EMPTY/LOW/MEDIUM/FULL → 5/33/66/100. Best-effort; false if the query fails.</summary>
    public static bool TryGetBattery(int index, out int percent, out bool charging)
    {
        percent = 100; charging = false;
        XInputBatteryInformation info;
        try { if (XInputGetBatteryInformation((uint)index, BATTERY_DEVTYPE_GAMEPAD, out info) != ERROR_SUCCESS) return false; }
        catch { return false; }
        if (info.BatteryType is BATTERY_TYPE_DISCONNECTED or BATTERY_TYPE_WIRED)
        {
            percent = 100; charging = info.BatteryType == BATTERY_TYPE_WIRED;   // wired = mains-powered, treat as full
            return true;
        }
        percent  = info.BatteryLevel switch { 0 => 5, 1 => 33, 2 => 66, _ => 100 };
        charging = false;   // XInput doesn't report a charging state for battery packs
        return true;
    }
}
