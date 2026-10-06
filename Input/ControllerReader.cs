using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using HidSharp;

namespace ControllerWheel;

/// <summary>
/// Reads a supported controller and fires per-button / stick events. Two backends behind one read loop:
/// (1) raw HID for the Sony family — the device + report layout come from a <see cref="ControllerProfile"/>
/// chosen at open time (DualSense Edge, standard DualSense, DualShock 4), with USB and the CRC-verified
/// Bluetooth variant handled transparently; only the Edge has Fn-button triggers, the rest summon wheels
/// via chord/swipe. (2) XInput for Xbox-compatible pads (see <see cref="RunXInputLoop"/>) — used as a
/// fallback when no Sony HID pad is present, raising the same events so the rest of the app is
/// backend-agnostic. Sony HID takes priority when both are connected.
/// </summary>
public sealed class ControllerReader : IController
{
    /// <summary>Device + trigger summary of the detected controller — Fn buttons for the Edge, chord /
    /// touchpad swipe for the rest. Falls back to the Edge profile until a device is opened.</summary>
    public string TriggerDescription =>
        _kindOverride == ControllerKind.Xbox ? "Xbox-compatible pad — chord" : _profile.TriggerDescription;

    /// <summary>The kind of controller currently open — drives which trigger option set applies (Settings
    /// dropdown, onboarding). The XInput backend overrides it to <see cref="ControllerKind.Xbox"/> while an
    /// Xbox pad is the source; otherwise it's the HID profile's kind. Defaults to the Edge until detection
    /// runs on the read thread.</summary>
    public ControllerKind Kind => _kindOverride ?? _profile.Kind;

    /// <summary>Whether the pad currently open has proprietary adaptive triggers (the DualSense family) —
    /// distinguishes it from the DualShock 4, which shares <see cref="ControllerKind.PlayStationOther"/> but
    /// has none. False while an Xbox pad is the source (no HID profile is open).</summary>
    public bool HasAdaptiveTriggers => _kindOverride != ControllerKind.Xbox && _profile.HasAdaptiveTriggers;

    /// <summary>Display name of the raw-HID profile currently open ("DualSense Edge", "DualSense",
    /// "DualShock 4", "8BitDo U2C") — <see cref="ControllerProfile.ShortName"/>; the trace uses the full
    /// <see cref="ControllerProfile.Name"/> instead.
    /// ⚠ Names the LAYOUT being parsed, never a verified identity: third-party pads in DS4-compatible mode
    /// present Sony's own VID/PID (docs/CONTROLLERS.md ▸ Pad identity), so user-facing use must hedge it.
    /// Meaningless while an Xbox pad is the source (the XInput backend has no HID profile).</summary>
    public string ProfileName => _profile.ShortName;

    /// <summary>Whether <see cref="ProfileName"/> must be hedged "(or compatible)" — true when the pad's
    /// VID/PID is one third-party pads also present (the Sony family), so the name states the parsed
    /// layout rather than a known model. See <see cref="ControllerProfile.HedgeName"/>.</summary>
    public bool ProfileNameHedged => _profile.HedgeName;

    /// <summary>Transport of the pad currently open. The raw-HID backend knows it from the report length
    /// settled at open time; XInput exposes no such thing, so an Xbox pad reports
    /// <see cref="ControllerTransport.Unknown"/>.</summary>
    public ControllerTransport Transport =>
        !_connected || _kindOverride == ControllerKind.Xbox ? ControllerTransport.Unknown
        : _isBt ? ControllerTransport.Bluetooth : ControllerTransport.Usb;

    /// <summary>Slot the XInput backend is reading, or -1 when the raw-HID backend owns the pad. Lets a
    /// diagnostic surface name the active path — the two backends expose very different detail.</summary>
    public int XInputSlot => _xinputSlot;

    /// <summary>The LATCHED digital L2/R2 state — what the chord matcher actually sees, hysteresis and all.
    /// <see cref="ControllerState"/> carries only the analog byte, from which this cannot be recomputed
    /// (the latch is what decides which threshold applies). Written on the read thread, read for display.</summary>
    public bool L2Down => _l2;
    public bool R2Down => _r2;

    // ── Detected device profile (VID/PIDs, per-device byte offsets, BT params) ─
    // Seeded to the Edge; ControllerProfile.Detect() picks the real profile for the connected pad on open.
    private ControllerProfile _profile = ControllerProfile.DualSenseEdge;

    // Set by the XInput backend to ControllerKind.Xbox while an Xbox pad is the source (null = the HID
    // profile is authoritative). Written/cleared on the read thread; read as an atomic int elsewhere.
    private ControllerKind? _kindOverride;

    // The XInput slot RunXInputLoop is currently reading, or -1 when the XInput backend isn't active —
    // lets SetRumble (called from another thread, the emulator's ViGEm callback via App) forward rumble
    // to the right physical pad without threading a slot parameter through the whole capture stack.
    private volatile int _xinputSlot = -1;
    private bool _rumbleForwardTraced;   // trace "forwarding active" once, not per event

    // ── Selected-device identity ──────────────────────────────────────────────
    // The ONE physical pad the read loop actually opened: its HID serial (the BT MAC on the Sony family,
    // stable across USB⇄BT) and, as a serial-less fallback, its PnP instance id. GetHidInstanceIds scopes
    // the HidHide cloak to THIS pad only — a second identical controller (couch co-op) must never be
    // hidden while we forward just one. Written on the read thread, read on the UI thread (atomic
    // reference writes). The serial is also persisted as the PREFERRED pad, so reconnects re-pick the
    // same device instead of letting profile priority silently switch to a newly attached pad.
    private string? _selectedSerial;
    private string? _selectedInstanceId;
    private string? _preferredSerial = LoadPreferredSerial();
    private static readonly string PreferredSerialPath = Path.Combine(AppPaths.AppDataDir, "controller-serial.txt");

    private static string? LoadPreferredSerial()
    {
        try
        {
            return File.Exists(PreferredSerialPath)
                ? File.ReadLines(PreferredSerialPath).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim()
                : null;
        }
        catch { return null; }
    }

    private static void SavePreferredSerial(string serial)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDir);
            File.WriteAllText(PreferredSerialPath, serial);
        }
        catch (Exception ex) { Trace.WriteLine($"[HID] preferred-serial persist failed: {ex.Message}"); }
    }

    /// <summary>Host-supplied query for our OWN virtual pad's XInput slot (XBox Mode presents a virtual
    /// Xbox 360, which is itself an XInput device) — so XInput detection skips it and never reads our own
    /// tail. Returns null when the virtual pad isn't an XInput device (native DS4 mode) or isn't up.</summary>
    public Func<(bool Active, int? Slot)>? VirtualOutput { get; set; }
    private string? _lastSelectionIssue;
    public string? DetectionIssue => _lastSelectionIssue;
    public event Action<bool>? XInputSelectionBlocked;
    private bool _selectionBlocked;

    // Stick axes sit at 1–4 and USB uses report id 0x01 across the whole Sony family.
    private const byte UsbReportId = 0x01;
    private const int  OffLX = 1, OffLY = 2, OffRX = 3, OffRY = 4;
    private const int  BtCrcLength = 4;

    // Working Fn/dpad offsets: seeded from the profile on each open, then a saved HID-wizard override wins
    // on the Edge (the wizard is Edge-only calibration). Inline defaults = the verified Edge layout so
    // there's never a zero window before the first open.
    private int _fnLeftByte = 10, _fnLeftMask = 0x10, _fnRightByte = 10, _fnRightMask = 0x20, _dpadByte = 8;
    private HidOffsets? _offsetOverride;   // manual HID-wizard calibration (hid_offsets.json), Edge-only

    /// <summary>Apply offsets discovered by the HID setup wizard (Edge calibration). Stored and re-applied
    /// over the profile on the next open.</summary>
    public void ApplyOffsets(HidOffsets o)
    {
        _offsetOverride = o.Sanitized();
        ApplyProfileOffsets();
    }

    /// <summary>Seed the working Fn/dpad offsets from the current profile, then let a saved HID-wizard
    /// override win on the Edge.</summary>
    private void ApplyProfileOffsets()
    {
        _fnLeftByte  = _profile.FnLeftByte;  _fnLeftMask  = _profile.FnLeftMask;
        _fnRightByte = _profile.FnRightByte; _fnRightMask = _profile.FnRightMask;
        _dpadByte    = _profile.DpadFaceByte;
        if (_offsetOverride is { } o && _profile.Kind == ControllerKind.DualSenseEdge)
        {
            _fnLeftByte  = o.FnLeftByte;  _fnLeftMask  = o.FnLeftMask;
            _fnRightByte = o.FnRightByte; _fnRightMask = o.FnRightMask;
            _dpadByte    = o.DPadByte;
        }
    }

    // ── Public events ─────────────────────────────────────────────────────────
    /// <summary>Fires true when the controller is found, false when lost.</summary>
    public event Action<bool>? Connected;

    /// <summary>Left wheel trigger — the left front Fn button (pressed = true).</summary>
    public event Action<bool>? TriggerLeftChanged;

    /// <summary>Right wheel trigger — the right front Fn button (pressed = true).</summary>
    public event Action<bool>? TriggerRightChanged;

    /// <summary>Fires on every input report with normalised stick axes in [-1, +1].</summary>
    public event Action<float, float, float, float>? StickUpdate; // lx, ly, rx, ry

    /// <summary>
    /// Fires when the D-pad direction changes.
    /// Value is the raw nibble: 0=Up, 1=UpRight, 2=Right, 3=DownRight,
    /// 4=Down, 5=DownLeft, 6=Left, 7=UpLeft, 8=Neutral.
    /// </summary>
    public event Action<int>? DPadChanged;

    /// <summary>Fires when the ✕ (Cross) face button changes — launch/select in the game browser.</summary>
    public event Action<bool>? CrossChanged;

    /// <summary>Fires when the ○ (Circle) face button changes — back/close in the game browser.</summary>
    public event Action<bool>? CircleChanged;

    /// <summary>Fires when the △ (Triangle) face button changes — assign-to-wheel in the game browser.</summary>
    public event Action<bool>? TriangleChanged;

    /// <summary>Fires when the □ (Square) face button changes — delete the selected slice in edit mode.</summary>
    public event Action<bool>? SquareChanged;

    /// <summary>Fires when the L1 / R1 shoulder buttons change — cycle the game-browser launcher filter.</summary>
    public event Action<bool>? L1Changed;
    public event Action<bool>? R1Changed;

    /// <summary>Fires when the L3 / R3 stick-clicks change — held with a wheel's Fn to enter edit mode.</summary>
    public event Action<bool>? L3Changed;
    public event Action<bool>? R3Changed;

    /// <summary>Fires when L2 / R2 cross a digital press/release threshold (analog, with hysteresis).</summary>
    public event Action<bool>? L2Changed;
    public event Action<bool>? R2Changed;

    /// <summary>Fires when the PS/Guide ("Home"), Create/Share, or Options/Menu buttons change — chord
    /// modifiers for the alternate trigger modes.</summary>
    public event Action<bool>? PsChanged;
    public event Action<bool>? CreateChanged;
    public event Action<bool>? OptionsChanged;

    /// <summary>Fires when the touchpad finger position/contact changes — x,y in [0,1], contact flag.</summary>
    public event Action<float, float, bool>? TouchpadChanged;

    /// <summary>Fires when the SECOND touchpad finger changes — for two-finger gestures.</summary>
    public event Action<float, float, bool>? Touchpad2Changed;

    /// <summary>Fires when the controller battery level (0–100%) or charging state changes.</summary>
    public event Action<int, bool>? BatteryChanged;

    /// <summary>Fires every report with a full input snapshot — drives Xbox-360 pad emulation
    /// (<see cref="GamepadEmulator"/>). The snapshot is only built when something is subscribed, so
    /// it costs nothing while emulation is off.</summary>
    public event Action<ControllerState>? StateChanged;
    private readonly object _stateGate = new();
    private ControllerState _latestState;
    private bool _hasLatestState;

    /// <summary>Replay in order with live submissions, including when an event-driven pad is held still.</summary>
    public void ReplayLatestState(Action<ControllerState> consumer)
    {
        lock (_stateGate) { if (_hasLatestState) consumer(_latestState); }
    }

    private void PublishState(ControllerState state)
    {
        lock (_stateGate)
        {
            _latestState = state;
            _hasLatestState = true;
            StateChanged?.Invoke(state);
        }
    }

    /// <summary>
    /// Fires with every raw report byte array, including invalid reports, for diagnostics.
    /// Calibration uses CalibrationReport so USB and Bluetooth share the same offsets.
    /// </summary>
    public event Action<byte[]>? DiagnosticReport;
    /// <summary>Validated Edge reports in a fixed 64-byte USB-aligned layout for calibration.</summary>
    public event Action<byte[]>? CalibrationReport;

    // ── State ─────────────────────────────────────────────────────────────────
    private Thread?    _thread;
    private HidStream? _stream;
    private volatile bool _stop;
    private volatile bool _forceReopen;   // tray "Reset Input": drop + reopen the stream on the next tick
    private volatile bool _isBt;          // current transport (set on open); BT uses 0x31 output + 0xA2 CRC
    private volatile bool _connected;     // mirrors the last Connected event, for the Transport readout
    private bool?         _lastConnected; // last state raised via Connected (null = none yet); read-loop thread only

    // A connected DualSense streams reports continuously, so this long without any data means the link
    // wedged with the handle still open (no IOException to catch) — treat it as a disconnect + reopen.
    private const long StallReadMs = 2000;

    // How often to re-read a BT pad's system-maintained battery level (see BluetoothBattery). The
    // Bluetooth stack refreshes it on its own schedule, so polling faster buys nothing.
    private const long SysBatteryPollMs = 60_000;
    private const long XiAggregateMs    = 60_000;   // XInput frame-rate sample cadence; see the read loop

    // BT feature-pipe wedge escalation: a stream reopen does NOT clear a wedged BT feature pipe — only a
    // full device-lost teardown does, and an instant reconnect keeps the capture layer's 10s pad-grace
    // timer from expiring, so the virtual pad + cloak never cycle. After this many consecutive opens where
    // BT full-report mode never engaged, hold the device CLOSED for longer than the grace window so the
    // capture layer does its full release/uncloak, then reconnect fresh.
    // ⚠ A capture reset can't fix a zombied WINDOWS BT STACK (GetFeature fails with win32=0, zero input
    // reports, for every process) — only cycling the Windows Bluetooth radio clears that. When a second
    // escalation is reached without a single live report, BtLinkDead tells the app to surface the
    // "toggle Bluetooth" notice. See docs/CONTROLLERS.md.
    private const int  BtDeadOpenLimit   = 3;
    private const int  BtEscalateHoldMs  = 12_000;   // > App's 10s pad-grace timer, by a margin
    private int        _btDeadOpens;                 // consecutive opens with no full report; read-loop thread only
    private int        _btEscalations;               // capture resets run without a live report since; read-loop thread only
    private bool       _btDeadSignaled;              // BtLinkDead(true) raised, not yet cleared; read-loop thread only

    /// <summary>Raised true when the BT link is dead beyond in-app recovery — a full capture-reset
    /// escalation already ran and the link STILL never delivered a report, the signature of a zombied
    /// Windows Bluetooth stack that only a radio off/on cycle clears. Raised false as soon as reports
    /// flow again. Fired on the reader thread — marshal to the UI thread before touching UI.</summary>
    public event Action<bool>? BtLinkDead;
    // Lightbar: desired RGB + a dirty flag the read loop drains, so ALL stream I/O stays on one thread.
    private volatile bool _lightbarOn;    // are we actively driving the lightbar?
    private volatile bool _lightbarDirty; // a new colour needs writing
    private byte _lbR, _lbG, _lbB;
    private bool _fnLeft, _fnRight;
    private int  _dpad = 8; // Neutral
    private bool _cross, _circle, _triangle, _square;
    private bool _l1, _r1, _l3, _r3;
    private bool _l2, _r2, _ps, _create, _options;   // chord inputs (L2/R2 thresholded)
    private bool _touchContact; private int _touchX = -1, _touchY = -1;     // touchpad finger 0
    private bool _touch2Contact; private int _touch2X = -1, _touch2Y = -1;  // touchpad finger 1
    private int  _batteryPercent = -1;   // last reported battery % (-1 = none yet)
    private bool _batteryCharging;

    // Battery + touchpad BYTE offsets live per-device on the profile (ControllerProfile.BatteryByte /
    // TouchByte), cross-verified against SDL hidapi, DS5Dongle firmware, and Linux hid-playstation.

    // L2/R2 analog→digital thresholds — hysteresis (press high, release low) avoids chatter at the edge.
    private const byte TriggerPress = 140, TriggerRelease = 100;

    // Touchpad finger-0 sub-layout (at the profile's TouchByte): [bit7=no-contact | id][X low 8b]
    // [X high nibble | Y low nibble][Y high 8b]; the pad resolution used to normalise to [0,1].
    private const int TouchMaxX = 1920, TouchMaxY = 1080;

    // DualSense buttons byte (the same byte as the D-pad): face buttons are the high nibble.
    private const byte SquareMask   = 0x10; // □
    private const byte CrossMask    = 0x20; // ✕
    private const byte CircleMask   = 0x40; // ○
    private const byte TriangleMask = 0x80; // △

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    public void Start()
    {
        if (_thread?.IsAlive == true) return;
        _stop   = false;
        _lastConnected = null;   // a fresh loop always reports its first state
        _thread = new Thread(ReadLoop) { IsBackground = true, Name = "HID-Reader" };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _stream?.Close();
        _thread?.Join(2000);
    }

    // (No exclusive-read mode: exclusive HID open can't block Steam / Windows.Gaming.Input — they read
    // via Raw Input, which ignores file-handle locks. Exclusivity is provided by HidHide cloaking + the
    // emulated pad; see App.UpdateInputCapture.)

    public void Dispose() => Stop();

    /// <summary>Forward a virtual-pad rumble event to the physical pad — only meaningful while the XInput
    /// backend is the active source (Sony HID pads don't rumble through this path). No-ops silently
    /// otherwise, so the host can wire this unconditionally without checking backend/kind itself.</summary>
    public void SetRumble(ushort left, ushort right)
    {
        int slot = _xinputSlot;
        if (slot < 0 || _kindOverride != ControllerKind.Xbox) return;
        if (XInputInterop.SetVibration(slot, left, right) && !_rumbleForwardTraced)
        {
            _rumbleForwardTraced = true;
            Trace.WriteLine($"[XI] rumble forwarding active (slot {slot})");
        }
    }

    /// <summary>Set the lightbar colour (0..255 each); all-zero turns it off. Thread-safe — the write
    /// is performed on the HID thread and re-asserted across reconnects until changed again.</summary>
    public void SetLightbar(byte r, byte g, byte b)
    {
        _lbR = r; _lbG = g; _lbB = b;
        _lightbarOn    = (r | g | b) != 0;
        _lightbarDirty = true;
    }

    /// <summary>Why <see cref="TryGetHidInstanceIds"/> returned the id list it did. The distinction between
    /// the last two is load-bearing for capture safety: an empty list because nothing is plugged in means
    /// "nothing to cloak, the virtual pad is safe to present", while an empty list because we could not
    /// resolve a pad we DO have open means "we don't know what to cloak" — and presenting the virtual pad
    /// there is precisely the double-input failure the cloak exists to prevent.</summary>
    public enum PadIdentity
    {
        /// <summary>No pad is selected/open — there is genuinely no physical controller to hide.</summary>
        NoPhysicalPad,
        /// <summary>The selected pad's cloakable devnodes were resolved (list is non-empty).</summary>
        Resolved,
        /// <summary>A pad IS selected, but no devnode could be resolved for it — enumeration failed, threw,
        /// or every candidate path failed to parse. Treat as "cloak unknown", never as "no pad".</summary>
        Unresolved,
    }

    /// <summary>Device instance IDs (e.g. "HID\VID_054C&amp;PID_0DF2\...") of every HID interface
    /// the controller currently exposes — for HidHide to cloak from other apps. Derived from each
    /// HidSharp device path. Empty if the controller isn't connected.
    /// <para>Callers deciding whether it is safe to bring up the virtual pad must use
    /// <see cref="TryGetHidInstanceIds"/> instead — an empty list here is ambiguous.</para></summary>
    public IReadOnlyList<string> GetHidInstanceIds()
    {
        TryGetHidInstanceIds(out var ids);
        return ids;
    }

    /// <summary>As <see cref="GetHidInstanceIds"/>, but reports WHY the list is empty. See
    /// <see cref="PadIdentity"/> — the caller must not treat "couldn't resolve" as "nothing connected".</summary>
    public PadIdentity TryGetHidInstanceIds(out IReadOnlyList<string> result)
    {
        result = Array.Empty<string>();
        // XInput (Xbox) backend: return nothing — HID-derived ids are the WRONG nodes for an XInput pad
        // (the game-visible devnode is the XUSB device; hiding only HID children hides nothing). The
        // experimental Xbox capture path supplies the correct devnode tree via XboxDeviceTree in
        // UpdateInputCaptureCore instead; with the flag off, Xbox stays pure-read.
        // NOT a resolution failure: the Xbox path is cloaked from a different devnode source entirely, so
        // "no HID ids" is the correct and expected answer, and the Sony cloak gate must not trip on it.
        if (_kindOverride == ControllerKind.Xbox) return PadIdentity.NoPhysicalPad;

        // Scope to the SELECTED pad only: match by serial when the device exposes one (covers every
        // interface/devnode of that pad on BOTH transports, so a BT⇄USB change still gets cloaked), else
        // by the opened device's exact instance id. Never an all-of-this-model sweep — with two identical
        // pads that would hide a second player's controller while forwarding only one.
        var serial = _selectedSerial;
        var selId  = _selectedInstanceId;
        if (serial is null && selId is null) return PadIdentity.NoPhysicalPad;   // nothing open → nothing to hide

        // Briefly cached per selected pad: the sweep below enumerates every HID device for each profile
        // product id and sits on the wheel-open path, so one capture update firing several of these in a
        // row would otherwise re-enumerate repeatedly.
        //
        // The TTL is deliberately short: correctness depends on every pass RE-SWEEPING, since matching by
        // serial (not a cached instance id) is what catches a new devnode appearing for a pad already open
        // on another transport — missing it means bleed-through. 250 ms collapses the burst while keeping
        // that guarantee within a quarter second.
        var cacheKey = (serial ?? "") + "|" + (selId ?? "");
        long now = Environment.TickCount64;
        if (Volatile.Read(ref _hidIdCache) is { } cached
            && cached.Key == cacheKey && now - cached.StampMs < HidIdCacheMs)
        {
            result = cached.Ids;
            return cached.Ids.Count > 0 ? PadIdentity.Resolved : PadIdentity.Unresolved;
        }

        var ids = new List<string>();
        try
        {
            foreach (var pid in _profile.ProductIds)
                foreach (var d in DeviceList.Local.GetHidDevices(_profile.VendorId, pid))
                {
                    try
                    {
                        // Never cloak our OWN ViGEm virtual pad (it shares Sony's VID + the DS4 v1 PID) —
                        // cloaking it would hide it from the very games it stands in for.
                        if (ControllerProfile.ShouldSkipHidDevice(d)) continue;
                        if (HidInstanceId.FromInterfacePath(d.DevicePath) is not { } id || ids.Contains(id)) continue;
                        bool ours = serial is not null
                            ? string.Equals(ControllerProfile.SerialOf(d), serial, StringComparison.OrdinalIgnoreCase)
                            : string.Equals(id, selId, StringComparison.OrdinalIgnoreCase);
                        if (ours) ids.Add(id);
                    }
                    catch { /* skip a device we can't read a path for */ }
                }
        }
        catch (Exception ex)
        {
            // A throwing enumeration must NOT surface as an empty list — that's indistinguishable from
            // "no pad" and would let the caller bring the virtual pad up UNCLOAKED. Report unresolved,
            // and don't cache: the next pass should retry rather than sit on a failure for the TTL.
            Trace.WriteLine($"[Capture] pad devnode enumeration failed: {ex.Message} — cloak identity unresolved");
            return PadIdentity.Unresolved;
        }
        Volatile.Write(ref _hidIdCache, new HidIdCache(cacheKey, now, ids));
        result = ids;
        // A pad is open but produced no cloakable devnode → we do NOT know what to hide.
        return ids.Count > 0 ? PadIdentity.Resolved : PadIdentity.Unresolved;
    }

    /// <summary>Memoized <see cref="GetHidInstanceIds"/> result for one selected pad (see the call site for
    /// why, and for why the window is this short). Written on whichever thread asks first, read on the UI
    /// thread — a single reference swap of an immutable record, so no lock is needed; a race can only cost
    /// one redundant enumeration.</summary>
    private sealed record HidIdCache(string Key, long StampMs, IReadOnlyList<string> Ids);
    private HidIdCache? _hidIdCache;
    private const int HidIdCacheMs = 250;

    // ── Read loop (runs on background thread) ─────────────────────────────────

    /// <summary>Raise <see cref="Connected"/> only on a state CHANGE. The outer loop's device-null
    /// branch re-runs every ~2 s for as long as no pad is enumerated (this app's normal idle state —
    /// controller powered off), and the host's handler does a full teardown + trace line per fire, so a
    /// level-triggered raise spams the trace log and churns the capture path all night. Read-loop
    /// thread only.</summary>
    private void RaiseConnected(bool connected)
    {
        if (_lastConnected == connected) return;
        _lastConnected = connected;
        _connected = connected;
        Connected?.Invoke(connected);
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _failedHidPaths = new(StringComparer.OrdinalIgnoreCase);
    private Task<bool>? _hidAvailabilityProbe; // One probe per reader, including across reconnect loops.
    private bool MayRetryHid(string path) => !_failedHidPaths.TryGetValue(path, out long until) || Environment.TickCount64 >= until;

    private void ReadLoop()
    {
        while (!_stop)
        {
            // Pick whichever supported controller is connected (Edge → DualSense → DS4 priority), with its
            // gamepad HID interface (longest input report — over BT the pad exposes several).
            (ControllerProfile, HidSharp.HidDevice)? found;
            try { found = ControllerProfile.Detect(_preferredSerial, MayRetryHid); }
            catch (Exception ex)
            {
                // Enumeration can throw under device churn. Outside the read block's catch this killed the
                // reader thread and, through the unhandled-exception hook, the process.
                Trace.WriteLine($"[Controller] detect failed, retrying: {ex.Message}");
                Thread.Sleep(2000);
                continue;
            }
            if (found is null)
            {
                // No Sony HID pad — try an Xbox-compatible pad on XInput (USB / dongle), skipping our own
                // virtual pad's slot. RunXInputLoop blocks until that pad drops or a Sony pad appears.
                var own = VirtualOutput?.Invoke() ?? (Active: false, Slot: (int?)null);
                int mask = XInputInterop.ConnectedMask();
                int slot = XInputSourceSelection.Select(mask, own.Slot, own.Active);
                string? issue = XInputSourceSelection.IsOwnOutputAmbiguous(mask, own.Slot, own.Active)
                    ? $"[XInput] acquisition paused: Radiata's output cannot be excluded reliably (mask={mask:X}, ownSlot={own.Slot})"
                    : null;
                if (issue != _lastSelectionIssue) { _lastSelectionIssue = issue; if (issue is not null) Trace.WriteLine(issue); }
                bool blocked = issue is not null;
                if (blocked != _selectionBlocked) { _selectionBlocked = blocked; XInputSelectionBlocked?.Invoke(blocked); }
                if (slot >= 0) { RunXInputLoop(slot); continue; }

                ResetInputState();
                _kindOverride = null;
                _selectedSerial = null;
                _selectedInstanceId = null;
                Volatile.Write(ref _hidIdCache, null);   // selection gone — never serve stale cloak ids
                RaiseConnected(false);
                Thread.Sleep(2000);
                continue;
            }
            _lastSelectionIssue = null;
            if (_selectionBlocked) { _selectionBlocked = false; XInputSelectionBlocked?.Invoke(false); }
            var (profile, device) = found.Value;
            _profile = profile;
            _kindOverride = null;    // a Sony HID pad is authoritative again
            // Record the opened pad's identity: it scopes the cloak (GetHidInstanceIds) and, persisted,
            // makes THIS the preferred pad on the next detect/reconnect (sticky selection).
            _selectedSerial     = ControllerProfile.SerialOf(device);
            _selectedInstanceId = HidInstanceId.FromInterfacePath(device.DevicePath);
            // A newly opened device may be the SAME pad on a different transport/devnode, which keeps the
            // serial and so would hit the cache with ids from the old instance — drop it unconditionally.
            Volatile.Write(ref _hidIdCache, null);
            if (_selectedSerial is not null
                && !string.Equals(_selectedSerial, _preferredSerial, StringComparison.OrdinalIgnoreCase))
            {
                _preferredSerial = _selectedSerial;
                SavePreferredSerial(_selectedSerial);
            }
            ApplyProfileOffsets();   // re-seed Fn/dpad offsets for this device (Edge wizard override wins)

            bool wentLive = false;   // did THIS open ever deliver a parseable report? (full BT report, or any USB report)
            try
            {
                // Shared open — HidHide (App.UpdateInputCapture) handles keeping input out of the game.
                HidStream stream = device.Open();
                using (stream)
                {
                    _stream = stream;
                    _forceReopen = false;   // a reset latched while disconnected is moot — this open IS the fresh stream
                    stream.ReadTimeout = 500;

                    int maxLen = device.GetMaxInputReportLength();
                    // Settled BEFORE the Connected event: handlers read Transport off it. The ≥78 cue is
                    // Sony-specific (the extended BT report length); a BluetoothOnly profile just IS BT.
                    _isBt = _profile.BluetoothOnly || maxLen >= 78;
                    // Battery is edge-reported (ParseUsb, and the BT sys-battery poll, fire only on a CHANGE) and
                    // the host clears its cached level on the disconnect half of a reopen. Without dropping
                    // this cache too, a pad whose charge hasn't moved reopens silent and the readouts stay
                    // blank until the level next steps.
                    _batteryPercent = -1; _batteryCharging = false;
                    RaiseConnected(true);

                    string mode = _isBt ? "BT" : "USB";
                    Trace.WriteLine($"[HID] Opened {_profile.Name}: {device.DevicePath}  max-input={maxLen}  mode={mode}");

                    // Over BT the pad emits a compact 0x01 report (wrong layout, no Fn buttons) until the
                    // host reads a feature report (0x05 DualSense / 0x02 DS4), which switches it to the
                    // extended report. Without this, a clean BT reconnect leaves buttons invisible.
                    if (_isBt && _profile.BtEnableFeatureId != 0)
                        EnableBtFullReports(device, stream, _profile.BtEnableFeatureId);

                    // Re-assert the lightbar on every (re)open so its colour survives a reconnect.
                    _lightbarDirty = _lightbarOn;

                    var buf = new byte[Math.Max(maxLen, 80)];
                    long lastData = Environment.TickCount64;
                    long lastCrcLog = 0;
                    int btCompactStreak = 0, btEnableAttempts = 0;   // 0x05-didn't-take recovery
                    long nextSysBattery = 0;   // 0 = read on the first tick of this open

                    while (!_stop)
                    {
                        if (_profile.StreamsContinuously && Environment.TickCount64 - lastData > StallReadMs)
                        {
                            Trace.WriteLine("[HID] no valid input reports — forcing reopen");
                            ResetInputState();
                            RaiseConnected(false);
                            break;
                        }
                        // A BT pad whose report carries no battery level: take the one Windows already
                        // maintains off the devnode. Polled (the property updates on the stack's own GATT
                        // schedule, not ours) and cheap, so a slow cadence is plenty.
                        if (_isBt && !_profile.ReportsBattery && BatteryChanged is not null
                            && Environment.TickCount64 >= nextSysBattery)
                        {
                            nextSysBattery = Environment.TickCount64 + SysBatteryPollMs;
                            if (BluetoothBattery.TryRead(_selectedInstanceId) is { } pct
                                && (pct != _batteryPercent || _batteryCharging))
                            {
                                // Charging state isn't exposed by this property; a pad on Bluetooth is not
                                // on the data cable (USB forces these pads into XInput mode), so report false.
                                _batteryPercent = pct; _batteryCharging = false;
                                BatteryChanged.Invoke(pct, false);
                            }
                        }

                        // Tray "Reset Input" → drop this stream and let the outer loop reopen it. Catches a
                        // wedge that still delivers (frozen) reports, which the stall watchdog can't see.
                        if (_forceReopen)
                        {
                            _forceReopen = false;
                            Trace.WriteLine("[HID] manual reset — forcing reopen");
                            ResetInputState();
                            RaiseConnected(false);
                            break;
                        }
                        if (_lightbarDirty) WriteLightbar();
                        int len = 0;
                        try { len = stream.Read(buf, 0, buf.Length); }
                        catch (TimeoutException)
                        {
                            // Stall watchdog: no data for too long → the link wedged with the handle still
                            // open. Drop + reopen (the outer loop re-acquires + re-runs the BT 0x05 read).
                            // Only for pads that stream continuously — an event-driven BLE pad is silent
                            // whenever nothing is touched, and that silence must not force a reopen.
                            if (!_profile.StreamsContinuously) continue;
                            if (Environment.TickCount64 - lastData <= StallReadMs) continue;
                            Trace.WriteLine("[HID] stream stalled — forcing reopen");
                            ResetInputState();
                            RaiseConnected(false);
                            break;
                        }

                        if (len < 1) continue;
                        bool validReport = false;

                        var report = buf.AsSpan(0, len);
                        DiagnosticReport?.Invoke(report.ToArray());

                        if (_profile.Style == ControllerReportStyle.HatButtons16)
                        {
                            // One report shape on this style — no compact/extended split, no CRC.
                            if (report[0] == _profile.UsbReportId && report.Length >= _profile.MinimumInputReportLength)
                            {
                                validReport = true;
                                ParseHatButtons(report);
                            }
                        }
                        else if (report[0] == UsbReportId)
                        {
                            if (!_isBt && report.Length >= _profile.MinimumInputReportLength)
                            {
                                validReport = true;
                                ParseUsb(report);
                            }
                            else if (_isBt)
                            {
                                // Over BT, 0x01 is the COMPACT report — a different layout from USB's 0x01
                                // (parsing it with USB offsets reads garbage, and it has NO Fn buttons).
                                // Seeing it persistently means the Feature-0x05 switch DIDN'T TAKE (the
                                // classic clean-reconnect-after-reboot failure that leaves Fn dead).
                                // Self-heal: re-request full reports a few times, then force a reopen
                                // (the outer loop re-acquires + re-runs the 0x05 read).
                                if (++btCompactStreak >= 25)   // ~a beat of sustained compact traffic
                                {
                                    btCompactStreak = 0;
                                    if (++btEnableAttempts <= 3)
                                    {
                                        Trace.WriteLine($"[HID] BT still on compact 0x01 — re-requesting full reports (attempt {btEnableAttempts})");
                                        EnableBtFullReports(device, stream, _profile.BtEnableFeatureId);
                                    }
                                    else
                                    {
                                        Trace.WriteLine("[HID] BT stuck on compact report — forcing reopen");
                                        ResetInputState();
                                        RaiseConnected(false);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (report[0] == _profile.BtReportId)
                        {
                            if (ValidateBtCrc(report, _profile.BtReportLength)
                                && report.Length - _profile.BtDataOffset >= _profile.MinimumInputReportLength)
                            {
                                btCompactStreak = btEnableAttempts = 0;
                                ParseUsb(report.Slice(_profile.BtDataOffset));
                                validReport = true;
                            }
                            else if (Environment.TickCount64 - lastCrcLog >= 1000)
                            {
                                lastCrcLog = Environment.TickCount64;
                                Trace.WriteLine($"[HID] BT CRC mismatch  len={len}");
                            }
                        }
                        if (validReport)
                        {
                            wentLive = true;
                            lastData = Environment.TickCount64;
                        }
                    }
                }
                _stream = null;
            }
            catch (Exception ex)
            {
                // Treat ANY open/read failure as a transient disconnect — the loop re-detects and reopens.
                // Must stay catch-all: device.Open()/GetMaxInputReport can surface Win32Exception /
                // UnauthorizedAccessException / TimeoutException (notably right after a HidHide devnode
                // restart, when the interface is briefly inaccessible). An uncaught throw here kills the
                // reader thread → silently dead controller (no reconnect) AND trips the crash-uncloak path.
                Trace.WriteLine($"[HID] Device lost: {ex.Message}");
                ResetInputState();
                RaiseConnected(false);
                _stream = null;
                Thread.Sleep(1000);
            }

            if (!wentLive) _failedHidPaths[device.DevicePath] = Environment.TickCount64 + 10_000;
            else _failedHidPaths.TryRemove(device.DevicePath, out _);

            // BT wedge escalation (see BtDeadOpenLimit above): hold the device closed past the pad-grace
            // window so the capture layer fully releases before the fresh reconnect re-runs setup.
            if (wentLive || _stop)
            {
                _btDeadOpens = _btEscalations = 0;
                if (_btDeadSignaled && wentLive)
                {
                    _btDeadSignaled = false;
                    Trace.WriteLine("[HID] BT link recovered — clearing the toggle-Bluetooth notice");
                    BtLinkDead?.Invoke(false);
                }
            }
            else if (_isBt && ++_btDeadOpens >= BtDeadOpenLimit)
            {
                Trace.WriteLine($"[HID] BT full-report mode never engaged after {_btDeadOpens} opens — " +
                                $"holding device closed {BtEscalateHoldMs / 1000}s to force a full capture reset");
                // A second escalation means the capture reset didn't fix it — that's the zombie-BT-stack
                // signature, which only a Windows Bluetooth radio cycle clears. Tell the user.
                if (++_btEscalations >= 2 && !_btDeadSignaled)
                {
                    _btDeadSignaled = true;
                    Trace.WriteLine("[HID] BT link dead beyond in-app recovery — raising the toggle-Bluetooth notice");
                    BtLinkDead?.Invoke(true);
                }
                for (int slept = 0; slept < BtEscalateHoldMs && !_stop; slept += 250)
                    Thread.Sleep(250);
                _btDeadOpens = 0;
            }
        }
    }

    /// <summary>Tray "Reset Input": drop the current HID stream and reopen it on the next read-loop tick
    /// (within the 500 ms read timeout), recovering a wedged handle that still delivers frozen reports —
    /// a state the passive disconnect path and the stall watchdog can't detect. A reset requested while
    /// nothing is open is discarded when a stream next opens (that open IS a fresh stream), so it can't
    /// latch and force a spurious disconnect/reconnect bounce on the next connection.</summary>
    public void ForceReconnect() => _forceReopen = true;

    /// <summary>Drop all cached button/d-pad state back to neutral so a reconnect re-baselines cleanly and
    /// re-fires edge events from a known-good zero — a held press can't survive a disconnect. (Sticks and
    /// the full-state snapshot are pushed every report, so they self-heal once reports resume.)</summary>
    private void ResetInputState()
    {
        lock (_stateGate)
        {
            _hasLatestState = false;
            _latestState = default;
            StateChanged?.Invoke(default);
        }
        _fnLeft = _fnRight = false;
        _dpad = 8;   // neutral hat
        _cross = _circle = _triangle = _square = false;
        _l1 = _r1 = _l3 = _r3 = false;
        _l2 = _r2 = _ps = _create = _options = false;
        _touchContact = false; _touchX = _touchY = -1;
        _touch2Contact = false; _touch2X = _touch2Y = -1;
    }

    // ── XInput backend (Xbox-compatible pads) ───────────────────────────────────

    /// <summary>Read an Xbox-compatible pad on XInput slot <paramref name="slot"/> until it disconnects
    /// (or a reset/stop is requested, or a higher-priority Sony pad appears), raising the same button /
    /// stick / snapshot events as the HID path so the rest of the app is backend-agnostic. Sets
    /// Kind = Xbox for the trigger set. Runs on the read-loop thread (called from <see cref="ReadLoop"/>).</summary>
    private void RunXInputLoop(int slot)
    {
        _kindOverride = ControllerKind.Xbox;
        _xinputSlot = slot;
        _rumbleForwardTraced = false;
        ResetInputState();
        _batteryPercent = -1;
        RaiseConnected(true);
        Trace.WriteLine($"[XInput] Opened Xbox-compatible pad on slot {slot}");

        uint lastPacket = uint.MaxValue;
        long lastBattery = 0, lastSonyCheck = Environment.TickCount64;
        long lastAggLog = Environment.TickCount64;
        int  aggFrames = 0;
        try
        {
            while (!_stop)
            {
                // One aggregate line per minute while frames arrive. ⚠ Not per second: a connected pad
                // returns a frame on every poll even when idle, so a 1 s cadence is ~86k lines a day of
                // log. lastPkt is what the zombie-link triage needs (a frozen packet counter through a
                // wiggle) and a per-minute sample still shows it.
                long aggNow = Environment.TickCount64;
                if (aggNow - lastAggLog >= XiAggregateMs)
                {
                    if (aggFrames > 0)
                        Trace.WriteLine($"[XI] slot={slot} frames={aggFrames * 1000L / XiAggregateMs}/sec lastPkt={lastPacket}");
                    aggFrames = 0;
                    lastAggLog = aggNow;
                }

                if (_forceReopen)
                {
                    _forceReopen = false;
                    Trace.WriteLine("[XInput] manual reset — dropping pad");
                    break;
                }

                long now = Environment.TickCount64;

                // A Sony HID pad is higher priority — yield so the outer loop switches to it. Enumerating
                // HID is relatively costly, so only check ~1×/s, not every poll.
                if (now - lastSonyCheck > 1000)
                {
                    lastSonyCheck = now;
                    if (_hidAvailabilityProbe is { IsCompletedSuccessfully: true } && _hidAvailabilityProbe.Result)
                    {
                        _hidAvailabilityProbe = null; // Consume the result; a later loop must not reuse stale availability.
                        Trace.WriteLine("[XInput] readable HID pad appeared — yielding to HID");
                        break;
                    }
                    if (_hidAvailabilityProbe is null || _hidAvailabilityProbe.IsCompleted)
                        _hidAvailabilityProbe = Task.Run(() =>
                        {
                            try
                            {
                                var candidate = ControllerProfile.Detect(_preferredSerial, MayRetryHid);
                                if (candidate is null) return false;
                                try { using var probe = candidate.Value.device.Open(); return true; }
                                catch
                                {
                                    _failedHidPaths[candidate.Value.device.DevicePath] = Environment.TickCount64 + 10_000;
                                    return false;
                                }
                            }
                            catch (Exception ex) { Trace.WriteLine($"[XInput] HID availability probe failed: {ex.Message}"); return false; }
                        });
                    // Keep the already selected source while its reads remain valid. Later device
                    // arrivals must not silently replace it; a failed read below starts fresh acquisition.
                }

                if (!XInputInterop.TryGetState(slot, out var gp, out uint packet))
                {
                    // A failed XInput read invalidates this slot's binding. Re-enumerate before reading
                    // it again; a replacement virtual controller may reuse the same index immediately.
                    Trace.WriteLine("[XInput] source unavailable — neutralizing and rechecking eligible slots");
                    break;
                }
                aggFrames++;

                if (packet != lastPacket) { lastPacket = packet; ParseXInput(gp); }

                if (BatteryChanged is not null && now - lastBattery > 2000)
                {
                    lastBattery = now;
                    if (XInputInterop.TryGetBattery(slot, out int pct, out bool chg) &&
                        (pct != _batteryPercent || chg != _batteryCharging))
                    {
                        _batteryPercent = pct; _batteryCharging = chg;
                        BatteryChanged?.Invoke(pct, chg);
                    }
                }

                Thread.Sleep(5);   // Scheduler-dependent delay; this is not a guaranteed 200 Hz polling rate.
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[XInput] loop error: {ex.Message}"); }
        finally
        {
            XInputInterop.SetVibration(slot, 0, 0);   // best-effort: don't leave the motors stuck on
            _xinputSlot = -1;
            ResetInputState();
            _kindOverride = null;
            _batteryPercent = -1;
            RaiseConnected(false);
        }
    }

    /// <summary>Map one XInput frame to the same events + <see cref="ControllerState"/> snapshot as the HID
    /// path. Semantic slots match the Sony mapping used everywhere else: A→Cross, B→Circle, X→Square,
    /// Y→Triangle, LB/RB→L1/R1, thumb-clicks→L3/R3, View(Back)→Create, Menu(Start)→Options, Guide→Ps.</summary>
    private void ParseXInput(in XInputInterop.XInputGamepad gp)
    {
        ushort b = gp.wButtons;

        // XInput sticks are int16 [-32768,+32767], up-positive; the app (like the Sony pads) uses
        // down-positive Y, so negate Y. Normalise to [-1,+1].
        float lx = Clamp1(gp.sThumbLX / 32767f),  ly = Clamp1(-gp.sThumbLY / 32767f);
        float rx = Clamp1(gp.sThumbRX / 32767f),  ry = Clamp1(-gp.sThumbRY / 32767f);
        StickUpdate?.Invoke(lx, ly, rx, ry);

        // D-pad → hat nibble (0=Up..8=Neutral) to match DPadChanged consumers.
        bool du = (b & XInputInterop.DPadUp)    != 0, dd = (b & XInputInterop.DPadDown)  != 0;
        bool dl = (b & XInputInterop.DPadLeft)  != 0, dr = (b & XInputInterop.DPadRight) != 0;
        int dpad = HatFrom(du, dd, dl, dr);
        if (dpad != _dpad) { _dpad = dpad; DPadChanged?.Invoke(dpad); }

        SetBtn(ref _cross,    (b & XInputInterop.A) != 0, CrossChanged);
        SetBtn(ref _circle,   (b & XInputInterop.B) != 0, CircleChanged);
        SetBtn(ref _square,   (b & XInputInterop.X) != 0, SquareChanged);
        SetBtn(ref _triangle, (b & XInputInterop.Y) != 0, TriangleChanged);
        SetBtn(ref _l1, (b & XInputInterop.LeftShoulder)  != 0, L1Changed);
        SetBtn(ref _r1, (b & XInputInterop.RightShoulder) != 0, R1Changed);
        SetBtn(ref _l3, (b & XInputInterop.LeftThumb)  != 0, L3Changed);
        SetBtn(ref _r3, (b & XInputInterop.RightThumb) != 0, R3Changed);
        SetBtn(ref _ps,      (b & XInputInterop.Guide) != 0, PsChanged);

        // Analog triggers → digital with the same hysteresis (press high, release low) as the HID path.
        bool l2 = _l2 ? gp.bLeftTrigger  > TriggerRelease : gp.bLeftTrigger  >= TriggerPress;
        bool r2 = _r2 ? gp.bRightTrigger > TriggerRelease : gp.bRightTrigger >= TriggerPress;
        if (l2 != _l2) { _l2 = l2; L2Changed?.Invoke(l2); }
        if (r2 != _r2) { _r2 = r2; R2Changed?.Invoke(r2); }

        // Select/Start fire after every chord primary above — TriggerInterpreter.SelectStartChordEngaged
        // must already see this report's primary-button state.
        SetBtn(ref _create,  (b & XInputInterop.Back)  != 0, CreateChanged);
        SetBtn(ref _options, (b & XInputInterop.Start) != 0, OptionsChanged);

        // Full-state snapshot for gamepad emulation (down/right-positive, like ControllerState from HID).
        {
            PublishState(new ControllerState
            {
                LeftStickX = lx,  LeftStickY = ly,  RightStickX = rx,  RightStickY = ry,
                LeftTrigger = gp.bLeftTrigger, RightTrigger = gp.bRightTrigger,
                Cross = _cross, Circle = _circle, Square = _square, Triangle = _triangle,
                L1 = _l1, R1 = _r1, L3 = _l3, R3 = _r3,
                Create = _create, Options = _options, Ps = _ps,
                DpadUp = du, DpadDown = dd, DpadLeft = dl, DpadRight = dr,
            });
        }
    }

    /// <summary>Raise an edge-triggered button event only on change (shared XInput helper).</summary>
    private static void SetBtn(ref bool cur, bool val, Action<bool>? evt)
    {
        if (val != cur) { cur = val; evt?.Invoke(val); }
    }

    private static float Clamp1(float v) => Math.Clamp(v, -1f, 1f);

    /// <summary>D-pad direction bits → the raw hat nibble the app uses (0=Up,1=UR,2=R,3=DR,4=D,5=DL,6=L,7=UL,8=Neutral).</summary>
    private static int HatFrom(bool up, bool down, bool left, bool right)
    {
        if (up   && right) return 1;
        if (up   && left)  return 7;
        if (down && right) return 3;
        if (down && left)  return 5;
        if (up)    return 0;
        if (right) return 2;
        if (down)  return 4;
        if (left)  return 6;
        return 8;
    }

    /// <summary>
    /// Reading a feature report switches a Sony pad from its compact BT input report (0x01) to the
    /// extended report that carries the full button set: feature 0x05 (DualSense) or 0x02 (DualShock 4).
    /// Best-effort.
    /// </summary>
    private static void EnableBtFullReports(HidDevice device, HidStream stream, byte featureId)
    {
        try
        {
            int len = device.GetMaxFeatureReportLength();
            if (len <= 0) len = 64;
            var feat = new byte[len];
            feat[0] = featureId;
            stream.GetFeature(feat);
            Trace.WriteLine($"[HID] BT feature 0x{featureId:X2} read — full reporting enabled");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[HID] BT feature read failed: {ex.Message}");
        }
    }

    // ── Report parsing (profile-driven: byte offsets come from _profile) ────────
    private void ParseUsb(ReadOnlySpan<byte> r)
    {
        if (r.Length < _profile.MinimumInputReportLength) return;
        var p = _profile;
        if (p.Kind == ControllerKind.DualSenseEdge && r.Length >= 64)
            CalibrationReport?.Invoke(r[..64].ToArray());
        bool createNow = _create, optionsNow = _options;   // raised late — see the shoulder-byte comment

        StickUpdate?.Invoke(Axis(r[OffLX]), Axis(r[OffLY]), Axis(r[OffRX]), Axis(r[OffRY]));

        // Rear Fn buttons — the direct wheel triggers, Edge only. Other pads have no Fn (HasFnButtons
        // false) and summon the wheels via chord/touchpad, so we don't read (or fire) triggers for them.
        if (p.HasFnButtons)
        {
            if (r.Length > _fnLeftByte)
            {
                bool fnL = (r[_fnLeftByte] & _fnLeftMask) != 0;
                if (fnL != _fnLeft) { _fnLeft = fnL; TriggerLeftChanged?.Invoke(fnL); }
            }
            if (r.Length > _fnRightByte)
            {
                bool fnR = (r[_fnRightByte] & _fnRightMask) != 0;
                if (fnR != _fnRight) { _fnRight = fnR; TriggerRightChanged?.Invoke(fnR); }
            }
        }

        if (r.Length > _dpadByte)
        {
            byte b = r[_dpadByte];
            int dpad = b & 0x0F;             // D-pad hat in the low nibble
            if (dpad != _dpad) { _dpad = dpad; DPadChanged?.Invoke(dpad); }

            bool square   = (b & SquareMask)   != 0;   // □
            bool cross    = (b & CrossMask)    != 0;   // ✕ — high nibble of the same byte
            bool circle   = (b & CircleMask)   != 0;   // ○
            bool triangle = (b & TriangleMask) != 0;   // △
            if (square   != _square)   { _square   = square;   SquareChanged?.Invoke(square); }
            if (cross    != _cross)    { _cross    = cross;    CrossChanged?.Invoke(cross); }
            if (circle   != _circle)   { _circle   = circle;   CircleChanged?.Invoke(circle); }
            if (triangle != _triangle) { _triangle = triangle; TriangleChanged?.Invoke(triangle); }
        }

        // Shoulder byte: L1=0x01, R1=0x02, Create/Share=0x10, Options=0x20, L3=0x40, R3=0x80 (shared masks
        // across the Sony family; DS4 also packs L2/R2 digital at 0x04/0x08, which we ignore — analog wins).
        if (r.Length > p.ShoulderByte)
        {
            byte b = r[p.ShoulderByte];
            bool l1 = (b & 0x01) != 0;
            bool r1 = (b & 0x02) != 0;
            bool create  = (b & 0x10) != 0;
            bool options = (b & 0x20) != 0;
            bool l3 = (b & 0x40) != 0;
            bool r3 = (b & 0x80) != 0;
            if (l1 != _l1) { _l1 = l1; L1Changed?.Invoke(l1); }
            if (r1 != _r1) { _r1 = r1; R1Changed?.Invoke(r1); }
            if (l3 != _l3) { _l3 = l3; L3Changed?.Invoke(l3); }
            if (r3 != _r3) { _r3 = r3; R3Changed?.Invoke(r3); }
            // Select/Start are deferred until after L2/R2 below — TriggerInterpreter.SelectStartChordEngaged
            // must already see every chord primary from this report (bumpers/sticks here, triggers below).
            createNow = create; optionsNow = options;
        }

        // PS/Guide ("Home") — mask 0x01.
        if (r.Length > p.PsByte)
        {
            bool ps = (r[p.PsByte] & 0x01) != 0;
            if (ps != _ps) { _ps = ps; PsChanged?.Invoke(ps); }
        }

        // L2 / R2 analog → digital with hysteresis.
        if (r.Length > p.L2AnalogByte && r.Length > p.R2AnalogByte)
        {
            byte lv = r[p.L2AnalogByte], rv = r[p.R2AnalogByte];
            bool l2 = _l2 ? lv > TriggerRelease : lv >= TriggerPress;
            bool r2 = _r2 ? rv > TriggerRelease : rv >= TriggerPress;
            if (l2 != _l2) { _l2 = l2; L2Changed?.Invoke(l2); }
            if (r2 != _r2) { _r2 = r2; R2Changed?.Invoke(r2); }
        }

        // Fire the Select/Start deferred above.
        if (createNow  != _create)  { _create  = createNow;  CreateChanged?.Invoke(createNow); }
        if (optionsNow != _options) { _options = optionsNow; OptionsChanged?.Invoke(optionsNow); }

        // Touchpad finger 0 (only when something's listening — fires on contact/position change).
        int tb = p.TouchByte;
        if (TouchpadChanged is { } onTouch && r.Length > tb + 3)
        {
            bool contact = (r[tb] & 0x80) == 0;
            int rawX = r[tb + 1] | ((r[tb + 2] & 0x0F) << 8);
            int rawY = (r[tb + 2] >> 4) | (r[tb + 3] << 4);
            if (contact != _touchContact || rawX != _touchX || rawY != _touchY)
            {
                _touchContact = contact; _touchX = rawX; _touchY = rawY;
                onTouch(Math.Clamp(rawX / (float)TouchMaxX, 0f, 1f),
                        Math.Clamp(rawY / (float)TouchMaxY, 0f, 1f), contact);
            }
        }

        // Touchpad finger 1 (next 4 bytes) — for two-finger gestures.
        if (Touchpad2Changed is { } onTouch2 && r.Length > tb + 7)
        {
            bool contact = (r[tb + 4] & 0x80) == 0;
            int rawX = r[tb + 5] | ((r[tb + 6] & 0x0F) << 8);
            int rawY = (r[tb + 6] >> 4) | (r[tb + 7] << 4);
            if (contact != _touch2Contact || rawX != _touch2X || rawY != _touch2Y)
            {
                _touch2Contact = contact; _touch2X = rawX; _touch2Y = rawY;
                onTouch2(Math.Clamp(rawX / (float)TouchMaxX, 0f, 1f),
                         Math.Clamp(rawY / (float)TouchMaxY, 0f, 1f), contact);
            }
        }

        if (BatteryChanged is not null && r.Length > p.BatteryByte)
        {
            byte bs = r[p.BatteryByte];
            int percent; bool charging;
            if (p.BatteryDualSenseStyle)
            {
                // DualSense: low nibble = level 0..10, high nibble = status (0 discharging, 1 charging,
                // 2 full). Matches DS5Dongle firmware and Linux hid-playstation.
                int status = (bs >> 4) & 0x0F;
                charging = status == 0x1;
                percent = status == 0x2 ? 100 : Math.Min(100, (bs & 0x0F) * 10);
            }
            else
            {
                // DS4: low nibble = level 0..11 (11 = full while powered), bit 0x10 = cable powered.
                // Matches SDL hidapi_ps4 + Linux hid-sony (SDL computes level*10+5; our flat ×10 capped
                // at 100 differs by ≤5% — immaterial for the hub readout).
                charging = (bs & 0x10) != 0;
                percent = Math.Min(100, (bs & 0x0F) * 10);
            }
            if (percent != _batteryPercent || charging != _batteryCharging)
            {
                _batteryPercent = percent; _batteryCharging = charging;
                BatteryChanged?.Invoke(percent, charging);
            }
        }

        // Cache a full snapshot even before emulation starts, so an unchanged held input can be replayed.
        // r is USB-aligned here (the BT path already sliced to match), so the profile's byte offsets hold
        // for USB and BT alike.
        {
            var st = new ControllerState
            {
                LeftStickX  = Axis(r[OffLX]),  LeftStickY  = Axis(r[OffLY]),
                RightStickX = Axis(r[OffRX]),  RightStickY = Axis(r[OffRY]),
                LeftTrigger  = r.Length > p.L2AnalogByte ? r[p.L2AnalogByte] : (byte)0,
                RightTrigger = r.Length > p.R2AnalogByte ? r[p.R2AnalogByte] : (byte)0,
            };
            if (r.Length > _dpadByte)
            {
                byte b = r[_dpadByte];
                int hat = b & 0x0F;                          // 0=Up,1=UR,2=R,3=DR,4=D,5=DL,6=L,7=UL,8=Neutral
                st.DpadUp = hat is 0 or 1 or 7; st.DpadRight = hat is 1 or 2 or 3;
                st.DpadDown = hat is 3 or 4 or 5; st.DpadLeft = hat is 5 or 6 or 7;
                st.Square = (b & 0x10) != 0; st.Cross = (b & 0x20) != 0;
                st.Circle = (b & 0x40) != 0; st.Triangle = (b & 0x80) != 0;
            }
            if (r.Length > p.ShoulderByte)
            {
                byte b = r[p.ShoulderByte];
                st.L1 = (b & 0x01) != 0; st.R1 = (b & 0x02) != 0;
                st.Create = (b & 0x10) != 0; st.Options = (b & 0x20) != 0;
                st.L3 = (b & 0x40) != 0; st.R3 = (b & 0x80) != 0;
            }
            if (r.Length > p.PsByte) st.Ps = (r[p.PsByte] & 0x01) != 0;
            PublishState(st);
        }
    }

    // ── HatButtons16 style (generic DInput-style gamepad report; reference: 8BitDo Ultimate 2C BT) ─────
    // 16-bit little-endian button field at the profile's ButtonsByte. Face buttons map to the same
    // semantic slots as ParseXInput (A→Cross, B→Circle, X→Square, Y→Triangle); L2/R2 carry digital bits
    // too, but the analog axes win (same hysteresis as everywhere else). The extra buttons (L4/R4) come
    // from the profile's Fn byte/mask fields and drive the wheel triggers directly, like the Edge's Fn.
    private const ushort HbA      = 0x0001, HbB     = 0x0002, HbX      = 0x0008, HbY     = 0x0010;
    private const ushort HbL1     = 0x0040, HbR1    = 0x0080, HbSelect = 0x0400, HbStart = 0x0800;
    private const ushort HbHome   = 0x1000, HbL3    = 0x2000, HbR3     = 0x4000;

    private void ParseHatButtons(ReadOnlySpan<byte> r)
    {
        var p = _profile;
        if (r.Length <= Math.Max(p.ButtonsByte + 1, Math.Max(p.L2AnalogByte, p.R2AnalogByte))) return;

        StickUpdate?.Invoke(Axis(r[2]), Axis(r[3]), Axis(r[4]), Axis(r[5]));

        // Hat nibble: 0=Up..7=UpLeft like the Sony family, but neutral is 0xF (or anything >7) — normalise
        // to the app's 8.
        int hat = r[p.DpadFaceByte] & 0x0F;
        int dpad = hat <= 7 ? hat : 8;
        if (dpad != _dpad) { _dpad = dpad; DPadChanged?.Invoke(dpad); }

        ushort b = (ushort)(r[p.ButtonsByte] | (r[p.ButtonsByte + 1] << 8));

        // Extra buttons (the direct wheel triggers) fire FIRST, like ParseUsb's Fn block, so a chord
        // matcher never sees a modifier lead its primary within one report.
        bool fnL = (r[_fnLeftByte]  & _fnLeftMask)  != 0;
        bool fnR = (r[_fnRightByte] & _fnRightMask) != 0;
        if (fnL != _fnLeft)  { _fnLeft  = fnL; TriggerLeftChanged?.Invoke(fnL); }
        if (fnR != _fnRight) { _fnRight = fnR; TriggerRightChanged?.Invoke(fnR); }

        SetBtn(ref _cross,    (b & HbA) != 0, CrossChanged);
        SetBtn(ref _circle,   (b & HbB) != 0, CircleChanged);
        SetBtn(ref _square,   (b & HbX) != 0, SquareChanged);
        SetBtn(ref _triangle, (b & HbY) != 0, TriangleChanged);
        SetBtn(ref _l1, (b & HbL1) != 0, L1Changed);
        SetBtn(ref _r1, (b & HbR1) != 0, R1Changed);
        SetBtn(ref _l3, (b & HbL3) != 0, L3Changed);
        SetBtn(ref _r3, (b & HbR3) != 0, R3Changed);
        SetBtn(ref _ps, (b & HbHome) != 0, PsChanged);

        byte lv = r[p.L2AnalogByte], rv = r[p.R2AnalogByte];
        bool l2 = _l2 ? lv > TriggerRelease : lv >= TriggerPress;
        bool r2 = _r2 ? rv > TriggerRelease : rv >= TriggerPress;
        if (l2 != _l2) { _l2 = l2; L2Changed?.Invoke(l2); }
        if (r2 != _r2) { _r2 = r2; R2Changed?.Invoke(r2); }

        // Select/Start deferred until every chord primary above fires — the same ordering requirement
        // TriggerInterpreter.SelectStartChordEngaged depends on in the other two backends.
        SetBtn(ref _create,  (b & HbSelect) != 0, CreateChanged);
        SetBtn(ref _options, (b & HbStart)  != 0, OptionsChanged);

        // Battery: a direct 0–100 percent with no charging bit on this style. Skipped entirely when the
        // pad declares the field but never fills it (ReportsBattery=false) — a constant 0 would render as
        // an empty-battery alert on a healthy pad, and "unknown" hides the readouts instead.
        if (BatteryChanged is not null && p.ReportsBattery && r.Length > p.BatteryByte)
        {
            int percent = Math.Min(100, (int)r[p.BatteryByte]);
            if (percent != _batteryPercent || _batteryCharging)
            {
                _batteryPercent = percent; _batteryCharging = false;
                BatteryChanged?.Invoke(percent, false);
            }
        }

        {
            PublishState(new ControllerState
            {
                LeftStickX  = Axis(r[2]), LeftStickY  = Axis(r[3]),
                RightStickX = Axis(r[4]), RightStickY = Axis(r[5]),
                LeftTrigger = lv, RightTrigger = rv,
                Cross = _cross, Circle = _circle, Square = _square, Triangle = _triangle,
                L1 = _l1, R1 = _r1, L3 = _l3, R3 = _r3,
                Create = _create, Options = _options, Ps = _ps,
                DpadUp = dpad is 0 or 1 or 7, DpadRight = dpad is 1 or 2 or 3,
                DpadDown = dpad is 3 or 4 or 5, DpadLeft = dpad is 5 or 6 or 7,
            });
        }
    }

    // ── Bluetooth CRC ─────────────────────────────────────────────────────────
    // Standard CRC32 (reflected, poly 0xEDB88320) over [0xA1] + report[0..logical-5]. The CRC lives at
    // logicalLen-4, NOT at the end of the (possibly zero-padded) buffer — Windows' BT-HID stack pads a
    // DS4 v2 report to 547 bytes while the real 78-byte payload + CRC stay at the front.
    private static bool ValidateBtCrc(ReadOnlySpan<byte> report, int logicalLen)
    {
        if (report.Length < logicalLen) return false;
        int logical = logicalLen;
        if (logical < BtCrcLength + 1) return false;
        int dataLen = logical - BtCrcLength;
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(report.Slice(dataLen, BtCrcLength));
        return Crc32WithSeed(report[..dataLen]) == expected;
    }

    // seed: 0xA1 for input reports (validation), 0xA2 for output reports (lightbar etc).
    private static uint Crc32WithSeed(ReadOnlySpan<byte> data, byte seed = 0xA1)
    {
        uint crc = 0xFFFFFFFF;
        crc = CrcStep(crc, seed);
        foreach (byte b in data)
            crc = CrcStep(crc, b);
        return ~crc;
    }

    // ── Lightbar output report ──────────────────────────────────────────────────
    // DualSense output report (Linux hid-playstation layout): a "common" block carries
    // valid_flag1 (bit 2 = lightbar-control-enable) and R/G/B. USB sends it as report 0x02; BT wraps
    // it as report 0x31 (seq tag + tag 0x10 prefix) with a CRC32 (seed 0xA2) appended as the last 4
    // bytes. Called only on the HID thread (from the read loop), so it never races the reader.
    // DualSense-family only: the DS4 lightbar uses a different output report, so we skip it there (the
    // green "emulating" tint is a nicety, not worth writing a malformed report to a DS4).
    private void WriteLightbar()
    {
        var s = _stream;
        if (s is null) return;
        if (_profile.BtReportId != 0x31) { _lightbarDirty = false; return; }   // not DualSense-family
        try
        {
            byte[] buf;
            if (_isBt)
            {
                buf = new byte[78];
                buf[0] = 0x31;           // DualSense BT output report
                buf[1] = 0x00;           // sequence tag (0 is accepted)
                buf[2] = 0x10;           // output tag
                const int c = 3;         // common block offset within the BT report
                buf[c + 1]  = 0x04;      // valid_flag1: LIGHTBAR_CONTROL_ENABLE
                buf[c + 44] = _lbR;
                buf[c + 45] = _lbG;
                buf[c + 46] = _lbB;
                uint crc = Crc32WithSeed(buf.AsSpan(0, 74), seed: 0xA2);
                BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(74), crc);
            }
            else
            {
                buf = new byte[48];
                buf[0] = 0x02;           // USB output report
                buf[2] = 0x04;           // valid_flag1: LIGHTBAR_CONTROL_ENABLE (common offset 1)
                buf[45] = _lbR;          // common offset 44
                buf[46] = _lbG;
                buf[47] = _lbB;
            }
            s.Write(buf);
            _lightbarDirty = false;
            Trace.WriteLine($"[HID] lightbar -> #{_lbR:X2}{_lbG:X2}{_lbB:X2} ({(_isBt ? "BT" : "USB")})");
        }
        catch (Exception ex) { Trace.WriteLine($"[HID] lightbar write failed: {ex.Message}"); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CrcStep(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Axis(byte raw) => (raw - 128f) / 127f; // 0-255 → [-1, +1]
}
