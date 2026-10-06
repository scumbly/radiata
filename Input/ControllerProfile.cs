using HidSharp;

namespace ControllerWheel;

/// <summary>Which report SHAPE the reader parses for a profile. <see cref="Sony"/> = the shared Sony-family
/// layout (ParseUsb + the CRC'd BT extended report). <see cref="HatButtons16"/> = the generic DInput-style
/// gamepad report: hat nibble, 8-bit axes, analog triggers, and a 16-bit little-endian button field —
/// reference device: 8BitDo Ultimate 2C over Bluetooth (ParseHatButtons).</summary>
public enum ControllerReportStyle { Sony, HatButtons16 }

/// <summary>Per-device HID description that makes <see cref="ControllerReader"/> device-agnostic: which
/// VID/PIDs identify the pad, and where each input sits in its report. The Sony family (DualSense Edge,
/// DualShock 4, standard DualSense) shares a report SHAPE — sticks at 1–4, a dpad+face byte, a shoulder
/// byte, a PS byte, analog L2/R2, a touch block, a battery byte, and a CRC'd BT variant — so only the
/// BYTE positions (and a couple of BT params) differ between them; the bit MASKS are identical, kept as
/// constants in the reader. Xbox pads are NOT profiled here — they go through XInput
/// (<see cref="XInputInterop"/>), not raw HID.
///
/// Trigger note: only the Edge has rear Fn buttons (<see cref="HasFnButtons"/>); every other pad summons
/// the wheels via a button chord / touchpad swipe (TriggerInterpreter), so the reader just needs to expose
/// the standard buttons and those modes fall out. See TriggerModes and docs/CONTROLLERS.md.</summary>
public sealed record ControllerProfile
{
    public required ControllerKind Kind { get; init; }

    public ControllerReportStyle Style { get; init; } = ControllerReportStyle.Sony;

    // False when this VID/PID identifies the model outright, so the readouts can name it plainly. True
    // (the default) for the Sony family, whose VID/PIDs third-party pads present as their own — there the
    // name states the LAYOUT being parsed and must be hedged "(or compatible)" to stay true of a clone.
    public bool HedgeName { get; init; } = true;

    // Compact form of Name for the user-facing readouts, which may append a hedge and a transport and get
    // unwieldy with a long product name. Defaults to Name; the trace always logs the full Name.
    private readonly string? _shortName;
    public string ShortName
    {
        get => _shortName ?? Name;
        init => _shortName = value;
    }
    public required string Name { get; init; }
    public required string TriggerDescription { get; init; }
    public required int VendorId { get; init; }
    public required int[] ProductIds { get; init; }

    // USB report id (the extended report; the BT variant is handled via the Bt* fields).
    public byte UsbReportId { get; init; } = 0x01;

    // Byte positions within a USB-aligned report (the BT slice is realigned to match — see BtDataOffset).
    // Sticks are at 1/2/3/4 for the whole Sony family, so they're not parameterised.
    public required int DpadFaceByte { get; init; }   // low nibble = dpad hat; high nibble = □✕○△
    public required int ShoulderByte { get; init; }   // L1/R1/Create(Share)/Options/L3/R3 (shared masks)
    public required int PsByte { get; init; }         // PS/Guide "Home" (mask 0x01)
    public required int L2AnalogByte { get; init; }
    public required int R2AnalogByte { get; init; }
    public required int TouchByte { get; init; }      // finger-0 block start: counter@+0 (bit7 = no contact), packed 12-bit x/y @+1..+3; finger-1 = +4 (cross-verified vs SDL hidapi, DS4 + DualSense)
    public required int BatteryByte { get; init; }
    public bool BatteryDualSenseStyle { get; init; } = true;   // false = DS4 decode (level in the low nibble)

    // False when the pad never populates its battery field: reporting a hardcoded 0% reads as an EMPTY
    // battery (alert glyph on a healthy pad), so the level must stay unknown and the readouts stay hidden.
    public bool ReportsBattery { get; init; } = true;

    // True for the DualSense family (Edge + standard) — the pads with proprietary adaptive triggers that
    // input isolation cannot pass through. False for the DualShock 4 (no adaptive triggers) and the
    // HatButtons16 pads. Onboarding step 1's caveat card reads this to distinguish the two Sony pads that
    // otherwise share ControllerKind.PlayStationOther.
    public bool HasAdaptiveTriggers { get; init; }

    // Edge-only rear Fn buttons (the direct wheel triggers). HasFnButtons=false ⇒ chord/touchpad only.
    public bool HasFnButtons { get; init; }
    public int FnLeftByte { get; init; }
    public int FnLeftMask { get; init; }
    public int FnRightByte { get; init; }
    public int FnRightMask { get; init; }

    // HatButtons16 style only: byte index of the 16-bit little-endian button field (buttons at
    // ButtonsByte/ButtonsByte+1); the extra buttons reuse the Fn byte/mask fields above. BatteryByte is a
    // direct 0–100 percent with no charging bit.
    public int ButtonsByte { get; init; }

    // Complete input reports: reject truncation even when the first few buttons are present.
    public int MinimumInputReportLength => Style == ControllerReportStyle.HatButtons16 ? 11 : 64;

    // False for event-driven links (BLE): the pad only reports on CHANGE, so read-loop silence is normal
    // and the stall watchdog must not force a reopen.
    public bool StreamsContinuously { get; init; } = true;

    // True when this profile's raw-HID identity only ever appears over Bluetooth (the transport readout
    // can't be inferred from report length there).
    public bool BluetoothOnly { get; init; }

    // Bluetooth: the extended report id, how far to slice it so it realigns to the USB layout, and the
    // feature-report id whose read flips the pad from its compact BT report to the extended one.
    // All zero for styles with no compact/extended split (HatButtons16 — the 0x01 report IS the report).
    public required byte BtReportId { get; init; }
    public required int BtDataOffset { get; init; }
    public required byte BtEnableFeatureId { get; init; }

    // Logical BT report length (the real payload + trailing CRC32). Windows' native BT-HID stack can
    // ZERO-PAD the report far beyond this (a DS4 v2 arrives as 547 bytes) — the CRC still sits at
    // BtReportLength-4, NOT at the end of the padded buffer, so CRC validation must use this length.
    public int BtReportLength { get; init; } = 78;   // 78 across the Sony family

    /// <summary>The DualSense Edge — the reference device. Front Fn buttons are the wheel triggers.
    /// Layout matches SDL hidapi_ps5: buttons[2] @ byte 10 carries the Fn bits 0x10/0x20; battery 53;
    /// touch 33.</summary>
    public static readonly ControllerProfile DualSenseEdge = new()
    {
        Kind = ControllerKind.DualSenseEdge, Name = "DualSense Edge",
        TriggerDescription = "DualSense Edge — Fn buttons",
        VendorId = 0x054C, ProductIds = [0x0DF2],
        DpadFaceByte = 8, ShoulderByte = 9, PsByte = 10, L2AnalogByte = 5, R2AnalogByte = 6,
        TouchByte = 33, BatteryByte = 53, BatteryDualSenseStyle = true,
        HasAdaptiveTriggers = true,
        HasFnButtons = true, FnLeftByte = 10, FnLeftMask = 0x10, FnRightByte = 10, FnRightMask = 0x20,
        BtReportId = 0x31, BtDataOffset = 1, BtEnableFeatureId = 0x05,
    };

    /// <summary>Standard DualSense (no Fn buttons) — identical report layout to the Edge; wheels via
    /// chord / touchpad swipe. Hardware-verified over BOTH transports: Bluetooth (78-byte 0x31 report,
    /// feature-0x05 enable) and USB (clean 64-byte 0x01). PID 0x0CE6 doesn't collide with the ViGEm virtual
    /// pad (0x05C4), so no exclusion edge-case here.</summary>
    public static readonly ControllerProfile DualSense = new()
    {
        Kind = ControllerKind.PlayStationOther, Name = "DualSense",
        TriggerDescription = "DualSense — chord / touchpad swipe",
        VendorId = 0x054C, ProductIds = [0x0CE6],
        DpadFaceByte = 8, ShoulderByte = 9, PsByte = 10, L2AnalogByte = 5, R2AnalogByte = 6,
        TouchByte = 33, BatteryByte = 53, BatteryDualSenseStyle = true,
        HasAdaptiveTriggers = true,
        HasFnButtons = false,
        BtReportId = 0x31, BtDataOffset = 1, BtEnableFeatureId = 0x05,
    };

    /// <summary>DualShock 4 (v1 0x05C4 / v2 0x09CC). Different byte layout from the DualSense (buttons a
    /// few bytes earlier, analog L2/R2 at 8/9); wheels via chord / touchpad swipe. Hardware-verified (v2
    /// 09CC) over BOTH transports: Bluetooth (the standard 78-byte 0x11 zero-padded to 547 by Windows, CRC
    /// at 74) and USB (a clean 64-byte 0x01). Touch byte 35, no-contact sentinel 0x80 at rest.
    /// ⚠ Battery is byte 30, NOT 12 (12 sits in struct padding and reads garbage): SDL hidapi_ps4 (struct
    /// offset 29 → +1 for the report id) and Linux hid-sony both read 30 — level 0..11 in the low nibble,
    /// bit 0x10 = cable powered (11 = full-while-powered).
    /// v1's PID 0x05C4 is shared with Radiata's own ViGEm virtual pad, but detection excludes virtual
    /// devices (see <see cref="ShouldSkipHidDevice"/>), so a real DS4 v1 is picked and the virtual pad isn't.</summary>
    public static readonly ControllerProfile DualShock4 = new()
    {
        Kind = ControllerKind.PlayStationOther, Name = "DualShock 4",
        TriggerDescription = "DualShock 4 — chord / touchpad swipe",
        VendorId = 0x054C, ProductIds = [0x05C4, 0x09CC],   // v1, v2
        DpadFaceByte = 5, ShoulderByte = 6, PsByte = 7, L2AnalogByte = 8, R2AnalogByte = 9,
        TouchByte = 35, BatteryByte = 30, BatteryDualSenseStyle = false,
        HasFnButtons = false,
        BtReportId = 0x11, BtDataOffset = 2, BtEnableFeatureId = 0x02,
    };

    /// <summary>8BitDo Ultimate 2C (Wireless SKU) over Bluetooth — the extra-button reference device.
    /// L4/R4 are dedicated extra buttons that drive the wheels directly, like the Edge's Fn pair (the
    /// square/star buttons are on-pad function keys and never reach the host). BLE-only identity: over USB
    /// (and the 2.4G dongle) the pad is a plain XUSB device at PID 0x310A with NO extra-button bits — that
    /// mode rides the XInput backend as Kind=Xbox. Report characterized on hardware (docs/CONTROLLERS.md):
    /// id 0x01, 11 bytes — hat nibble @1, sticks @2–5, R2 analog @6, L2 analog @7,
    /// 16 buttons LE @8–9 (A 0x0001, B 0x0002, L4 0x0004, X 0x0008, Y 0x0010, R4 0x0020, L1 0x0040,
    /// R1 0x0080, L2 0x0100, R2 0x0200, Select 0x0400, Start 0x0800, Home 0x1000, L3 0x2000, R3 0x4000),
    /// battery-strength @10. ⚠ An ONBOARD remap (hold L4/R4 + a button + the square key) REPLACES the
    /// native bit — a remapped paddle stops driving the wheel.
    /// <para>⚠ The battery byte is DECLARED by the report descriptor but never populated — measured 0x00
    /// across 1014 reports while Windows showed 79%, because the real level lives on the BLE GATT Battery
    /// Service (0x180F), off the HID interface entirely. Hence <see cref="ReportsBattery"/> = false:
    /// parsing it would show an empty-battery alert on a charged pad.</para></summary>
    public static readonly ControllerProfile Ultimate2C = new()
    {
        Kind = ControllerKind.ExtraButtonPad, Name = "8BitDo Ultimate 2C", ShortName = "8BitDo U2C",
        TriggerDescription = "8BitDo Ultimate 2C — L4/R4 buttons",
        HedgeName = false,   // 8BitDo's own VID/PID; no other vendor presents it
        Style = ControllerReportStyle.HatButtons16,
        VendorId = 0x2DC8, ProductIds = [0x301B],
        // Sony-layout fields unused by the HatButtons16 parser; the L2/R2 analog bytes ARE used.
        DpadFaceByte = 1, ShoulderByte = 0, PsByte = 0, L2AnalogByte = 7, R2AnalogByte = 6,
        TouchByte = 0, BatteryByte = 10, ReportsBattery = false,
        HasFnButtons = true, FnLeftByte = 8, FnLeftMask = 0x04, FnRightByte = 8, FnRightMask = 0x20,
        ButtonsByte = 8,
        StreamsContinuously = false, BluetoothOnly = true,
        BtReportId = 0, BtDataOffset = 0, BtEnableFeatureId = 0,
    };

    /// <summary>Profiles ACTIVELY auto-detected, in priority order: the Edge first (so a dev with an Edge
    /// plus another Sony pad keeps the Fn-button experience), then the standard DualSense and DualShock 4
    /// (both chord/swipe). All hardware-verified; the virtual-pad exclusion (<see cref="ShouldSkipHidDevice"/>)
    /// makes DS4 v1's shared PID 0x05C4 safe. Xbox is a separate XInput backend, not a raw-HID profile.</summary>
    public static readonly IReadOnlyList<ControllerProfile> All = [DualSenseEdge, DualSense, DualShock4, Ultimate2C];

    /// <summary>Defined-but-not-yet-detected profiles. Empty now — enabling one is a one-line move to All.</summary>
    public static readonly IReadOnlyList<ControllerProfile> Staged = [];

    /// <summary>Find a connected controller: the first profile (in priority order) whose VID/PID is
    /// currently enumerated by Windows, paired with its gamepad HID device (the interface with the longest
    /// input report — over BT the pad exposes several). Radiata's OWN ViGEm virtual pad shares Sony's VID
    /// and the DS4 v1 PID, so <see cref="ShouldSkipHidDevice"/> filters it out (else we'd read our own tail).
    /// Null if no supported physical controller is present.</summary>
    public static (ControllerProfile profile, HidDevice device)? Detect(string? preferredSerial = null, Func<string, bool>? mayOpen = null)
    {
        // Sticky identity: a pad already in use wins over the profile priority order, matched by serial (the
        // BT MAC on the Sony family, stable across USB⇄BT), so attaching a "higher" pad mid-session doesn't
        // steal the wheel from the one in hand. Not present → fall through to the priority pick.
        if (!string.IsNullOrEmpty(preferredSerial))
            foreach (var p in All)
            {
                HidDevice? best = null;
                int bestLen = -1;
                foreach (var pid in p.ProductIds)
                    foreach (var d in DeviceList.Local.GetHidDevices(p.VendorId, pid))
                    {
                        if (mayOpen?.Invoke(d.DevicePath) == false || ShouldSkipHidDevice(d)) continue;
                        if (!string.Equals(SerialOf(d), preferredSerial, StringComparison.OrdinalIgnoreCase)) continue;
                        int len;
                        try { len = d.GetMaxInputReportLength(); } catch { len = 0; }
                        if (len >= p.MinimumInputReportLength && len > bestLen) { bestLen = len; best = d; }
                    }
                if (best is not null) return (p, best);
            }

        foreach (var p in All)
        {
            HidDevice? best = null;
            int bestLen = -1;
            foreach (var pid in p.ProductIds)
                foreach (var d in DeviceList.Local.GetHidDevices(p.VendorId, pid))
                {
                    if (mayOpen?.Invoke(d.DevicePath) == false || ShouldSkipHidDevice(d)) continue;   // skip our own ViGEm virtual pad
                    int len;
                    try { len = d.GetMaxInputReportLength(); } catch { len = 0; }
                    if (len >= p.MinimumInputReportLength && len > bestLen) { bestLen = len; best = d; }
                }
            if (best is not null) return (p, best);
        }
        return null;
    }

    /// <summary>The device's HID serial (the BT MAC on the Sony family — stable across USB and Bluetooth),
    /// or null when unreadable/blank. The per-device identity used for sticky selection and for scoping the
    /// HidHide cloak to exactly the pad being read.</summary>
    internal static string? SerialOf(HidDevice device)
    {
        try
        {
            var s = device.GetSerialNumber();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
        catch { return null; }
    }

    // ── ViGEm virtual-pad exclusion (PnP parentage) ────────────────────────────

    /// <summary>True if a HID device is Radiata's OWN ViGEm virtual pad (or any ViGEm device) rather than a
    /// physical controller — detection AND cloaking must skip it, since it shares Sony's VID and the DS4 v1
    /// PID (0x05C4). Decided by PnP ancestry through <see cref="PnpAncestry"/>, the same test the XInput
    /// path runs. Unknown ancestry keeps the compatibility fallback; it does not prove physical identity.</summary>
    public static bool ShouldSkipHidDevice(HidDevice device)
    {
        var id = HidInstanceId.FromInterfacePath(device.DevicePath);
        return id is not null && PnpAncestry.Classify(id).Virtual;
    }
}
