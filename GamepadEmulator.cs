using System.Diagnostics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using XboxFeedbackEventArgs = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360FeedbackReceivedEventArgs;

namespace ControllerWheel;

/// <summary>Which kind of virtual pad we present to the system.</summary>
public enum EmulatedPad
{
    /// <summary>Xbox-360 pad — universal XInput compatibility (explicit "XBox Mode").</summary>
    Xbox360,
    /// <summary>DualShock 4 pad — keeps PlayStation button prompts/identity. Used as the stable
    /// stand-in for the cloaked DualSense in native mode, so games never see a device swap when
    /// wheels open and close.</summary>
    DualShock4,
}

/// <summary>Emulates a virtual gamepad (Xbox 360 or DualShock 4) from DualSense input via ViGEmBus.
/// Windows-only. Start() throws VigemBusNotFoundException if the driver isn't installed — catch + surface it.
///
/// Thread-safety: <see cref="Submit"/> runs on the controller reader thread, while Start/Stop/
/// Suppressed are driven from the UI thread. All pad access is serialized by <see cref="_gate"/> so the
/// two threads never touch the ViGEm pad concurrently.</summary>
public sealed class GamepadEmulator : IDisposable
{
    private const  byte            TriggerDigitalThreshold = 30;   // XInput trigger deadzone → digital L2/R2 bit
    private readonly object        _gate = new();
    private ViGEmClient?           _client;
    private IVirtualGamepad?       _pad;    // active pad — shared ops (connect/submit/reset); null = inactive
    private IXbox360Controller?    _x360;   // non-null only when _type == Xbox360
    private IDualShock4Controller? _ds4;    // non-null only when _type == DualShock4
    private EmulatedPad?           _type;   // null when inactive
    private bool                   _suppressed;
    private int                    _submitFailures;   // consecutive — a dead ViGEm target throws on every submit
    private long                   _lastRevive;       // TickCount64 of the last revive attempt (rate limit)
    private long                   _lastSubmitAggLog; // TRACE-ONLY: once-per-second Submit aggregate
    private int                    _submitCount;       // TRACE-ONLY: submits since _lastSubmitAggLog
    private int?                   _resolvedVirtualSlot; // slot that APPEARED when we connected — authoritative
    private int                    _slotMaskBeforeConnect; // connected slots sampled just before Connect()
    // Lifecycle trace state. Every connect/disconnect is a HID device arrival/removal delivered to whatever
    // game is running, and a game that binds a pad on arrival can be left bound to one we've since removed —
    // input goes dead in a way that toggling the wheels only makes worse (each toggle churns the pad again).
    // The generation number and uptime are what make that legible after the fact: a game that stopped
    // responding at generation N tells you the churn caused it, rather than leaving it to be re-argued.
    private int                    _padGeneration;    // identifies each target for asynchronous resolver results
    private long                   _padUpSince;       // TRACE-ONLY: TickCount64 at the current pad's connect

    /// <summary>Fires when the game rumbles the virtual Xbox 360 pad — bytes (0-255) scaled to ushort
    /// (0-65535, XInput's native range) for the host to forward to the physical pad. Xbox 360 target
    /// only (DS4 rumble isn't wired through ViGEm the same way, and native mode has no XInput physical
    /// pad to forward to). Raised on ViGEm's own callback thread — keep handlers fast.</summary>
    public event Action<ushort, ushort>? RumbleReceived;

    public bool Active { get { lock (_gate) return _pad is not null; } }

    /// <summary>The kind of pad currently up, or null if none.</summary>
    public EmulatedPad? PadType { get { lock (_gate) return _type; } }

    /// <summary>The source already being read before a new target is connected. Its slot cannot
    /// identify that newly created output, even if a connect-time state poll happens to fail.</summary>
    public Func<int>? SelectedSourceSlot { get; set; }
    public (bool Active, int? Slot) XInputIdentity
    {
        get { lock (_gate) return (_type == EmulatedPad.Xbox360 && _x360 is not null, _resolvedVirtualSlot); }
    }
    /// <summary>Connect-window inference, not a hardware identity. No ViGEm UserIndex fallback.</summary>
    public int? VirtualXInputSlot
    {
        get { lock (_gate) return _type == EmulatedPad.Xbox360 && _x360 is not null ? _resolvedVirtualSlot : null; }
    }

    /// <summary>Read-only check: is the ViGEmBus driver installed + reachable? Creating a
    /// <see cref="ViGEmClient"/> throws <c>VigemBusNotFoundException</c> when the bus driver is missing, so
    /// we probe with a throwaway client (no pad is created). For the onboarding "ViGEmBus: installed /
    /// missing" status + gating the Install action. Safe to call anytime.</summary>
    public bool ProbeDriverInstalled()
    {
        lock (_gate)
        {
            if (_client is not null) return true;   // a client is already up → the bus is present
            try { using var probe = new ViGEmClient(); return true; }
            catch { return false; }
        }
    }

    /// <summary>While true, the emulated pad reports a neutral state regardless of physical input — so
    /// the game underneath gets nothing while a Radiata overlay is up (Radiata still reads the physical
    /// pad for its own UI). Setting it true pushes a neutral report immediately; setting it false replays
    /// the last known physical state.
    ///
    /// <para>⚠ That replay is not cosmetic. Un-suppressing only restores the pad's real state if
    /// something SUBMITS afterwards, and on the XInput backend the reader only raises StateChanged when
    /// <c>dwPacketNumber</c> changes — a stick held against its gate or a trigger held down produces a
    /// stable packet, so nothing would be submitted and the game would keep seeing the neutral report we
    /// pushed on the way in, until the user moved the stick again. (The Sony HID path streams reports
    /// continuously, so it self-heals in milliseconds.)</para></summary>
    public bool Suppressed
    {
        get { lock (_gate) return _suppressed; }
        set
        {
            lock (_gate)
            {
                bool changed = _suppressed != value;
                if (changed) Trace.WriteLine($"[Emu] suppressed -> {value}");
                _suppressed = value;
                if (value) SubmitNeutralLocked();
                else if (changed && _hasLastState) SubmitLastStateLocked();
            }
        }
    }

    /// <summary>Newest state handed to <see cref="Submit"/>, kept so un-suppressing can restore it without
    /// waiting for the next physical change. Recorded even while suppressed, so what gets replayed is where
    /// the sticks are NOW, not where they were when the wheel opened.</summary>
    private ControllerState _lastState;
    private bool            _hasLastState;

    /// <summary>Re-submit <see cref="_lastState"/>. Caller must hold <see cref="_gate"/>.</summary>
    private void SubmitLastStateLocked()
    {
        if (_pad is null) return;
        try
        {
            if (_type == EmulatedPad.Xbox360) SubmitX360Locked(_lastState);
            else                              SubmitDs4Locked(_lastState);
            _pad.SubmitReport();
            _submitFailures = 0;
        }
        catch (Exception ex) { NoteSubmitFailureLocked(ex); }
    }

    /// <summary>Bring up (or switch to) the given virtual pad. Requesting a different type than the one
    /// already up disconnects the old one first — a one-off device change the game sees, which only
    /// happens on an explicit XBox-Mode toggle, never per wheel.</summary>
    public void Start(EmulatedPad type)
    {
        lock (_gate)
        {
            if (_type == type) return;
            StopLocked();                            // drop a different-type pad if one is up
            try { ConnectLocked(type); }
            catch { DiscardClientLocked(); throw; }  // a dead bus handle would poison every retry
            _suppressed = false;
        }
    }

    /// <summary>Connect a fresh pad of <paramref name="type"/>. Caller must hold <see cref="_gate"/>.
    /// Throws if ViGEmBus is missing or the connect fails (state stays "inactive" — StopLocked ran).</summary>
    private void ConnectLocked(EmulatedPad type)
    {
        _client ??= new ViGEmClient();               // throws if ViGEmBus missing; reused across start/stop,
                                                     // discarded on a connect failure (DiscardClientLocked)
        if (type == EmulatedPad.Xbox360)
        {
            var pad = _client.CreateXbox360Controller();
            pad.AutoSubmitReport = false;
            // Sample the occupied slots BEFORE the pad exists: whatever appears next is ours. Must be taken
            // here, not after Connect(), or the difference is meaningless.
            _slotMaskBeforeConnect = XInputInterop.ConnectedMask();
            int selectedSource = SelectedSourceSlot?.Invoke() ?? -1;
            if (selectedSource is >= 0 and <= 3) _slotMaskBeforeConnect |= 1 << selectedSource;
            _resolvedVirtualSlot = null;
            pad.Connect();
            // ⚠ Phantom-stick trap: a freshly Connect()-ed target's OS-visible XInputGetState report is a
            // fixed non-neutral stick deflection baked in at connect time (measured LX≈-3356 LY≈-1869
            // RX≈-3255 RY≈-848, dwPacketNumber=1, every session) — a game launched before the first stick
            // touch sees ~10% phantom deflection. Submitting an all-neutral report right after Connect()
            // does NOT clear it: the client SDK only pushes to the bus when the report differs from its
            // own shadow, which already reads "neutral", so zero-value submits are silently dropped until
            // the user's first real movement. Hence the SENTINEL-then-neutral pair below: the non-zero
            // sentinel forces a real diff, the immediate neutral forces a second — measured to land
            // packet=neutral synchronously, before Connect() returns. A reader polling between the two
            // submits could observe one report of full deflection (sub-millisecond, at startup).
            // Do NOT shrink the sentinel (e.g. to 1) without re-verifying on hardware: "any
            // diff forces a push" is an assumption the original bug already broke once.
            pad.SetAxisValue(Xbox360Axis.LeftThumbX, short.MaxValue);
            pad.SubmitReport();
            pad.ResetReport();
            pad.SubmitReport();
            pad.FeedbackReceived += OnXboxFeedback;
            _x360 = pad; _pad = pad;
            Trace.WriteLine($"[Emu] x360 connected — pad generation {_padGeneration + 1}");
        }
        else
        {
            var pad = _client.CreateDualShock4Controller();
            pad.AutoSubmitReport = false;
            pad.Connect();
            // Same connect-gap bug as Xbox 360 (see the comment above) — a bare ResetReport()+SubmitReport()
            // measurably does NOT clear it, so force the same sentinel-then-neutral pair.
            pad.SetAxisValue(DualShock4Axis.LeftThumbX, byte.MaxValue);
            pad.SubmitReport();
            pad.ResetReport();
            pad.SubmitReport();
            _ds4 = pad; _pad = pad;
            // Traced like the Xbox 360 arrival above: native-mode pad churn (the default) is otherwise
            // invisible outside the caller's [Capture] trace.
            Trace.WriteLine($"[Emu] ds4 connected — pad generation {_padGeneration + 1}");
        }
        _type = type;                                // assign only after a successful connect
        _submitFailures = 0;
        _padGeneration++;
        _padUpSince = Environment.TickCount64;
        if (type == EmulatedPad.Xbox360) StartVirtualSlotResolver();
    }

    /// <summary>Observe the full three-second connect window and retain a single-arrival inference
    /// without treating PnP counts as identity. Ambiguous arrivals remain unresolved; stale targets cannot assign a slot.</summary>
    private void StartVirtualSlotResolver()
    {
        int before = _slotMaskBeforeConnect;
        int generation = _padGeneration;
        var target = _x360;
        var thread = new Thread(() =>
        {
            var observation = new XInputSlotObservation(before);
            long start = Environment.TickCount64;
            while (Environment.TickCount64 - start < 3000)
            {
                lock (_gate)
                    if (_type != EmulatedPad.Xbox360 || _padGeneration != generation || !ReferenceEquals(_x360, target)) return;
                observation.Observe(XInputInterop.ConnectedMask());
                Thread.Sleep(50);
            }
            observation.Observe(XInputInterop.ConnectedMask());
            int? slot = observation.Resolve();
            lock (_gate)
            {
                if (_type != EmulatedPad.Xbox360 || _padGeneration != generation || !ReferenceEquals(_x360, target)) return;
                _resolvedVirtualSlot = slot;
            }
            Trace.WriteLine(slot is int resolved
                ? $"[Emu] virtual Xbox slot {resolved} inferred from the single arrival in the connect window"
                : "[Emu] virtual Xbox slot unresolved; replacement acquisition will pause while this target exists; valid current input is retained");
        }) { IsBackground = true, Name = "Emu-VirtualSlot-Resolve" };
        thread.Start();
    }

    /// <summary>Force the virtual pad to a neutral report right now — used when the physical controller
    /// drops out so the pad can't be left frozen on its last (pre-disconnect) input. No-op if no pad is up
    /// or while suppressed (already neutral).</summary>
    public void Neutralize() { lock (_gate) SubmitNeutralLocked(); }

    /// <summary>Disconnect the virtual pad but keep the ViGEm client alive, so a later Start() only
    /// re-connects a pad — cheap.</summary>
    public void Stop() { lock (_gate) StopLocked(); }

    private void StopLocked()
    {
        // Trace BEFORE the teardown clears _type, and only for a pad that was really up — StopLocked also
        // runs as a no-op precursor to every Start (see Start's "drop a different-type pad" call).
        if (_pad is not null)
            Trace.WriteLine($"[Emu] {(_type == EmulatedPad.Xbox360 ? "x360" : "ds4")} disconnected — "
                            + $"pad generation {_padGeneration} was up {(Environment.TickCount64 - _padUpSince) / 1000.0:0.0}s "
                            + "(games bound to it see a device removal)");
        if (_x360 is not null) { try { _x360.FeedbackReceived -= OnXboxFeedback; } catch { } }
        try { _pad?.Disconnect(); }
        catch (Exception ex) { Trace.WriteLine($"[Emu] virtual pad disconnect failed (possible ghost pad): {ex.Message}"); }
        _pad = null; _x360 = null; _ds4 = null; _type = null; _suppressed = false;
        _resolvedVirtualSlot = null;  // ditto the resolved slot: the next pad may land somewhere else
        _hasLastState = false;        // ditto the replay state: a fresh pad must not inherit the old one's
    }

    /// <summary>ViGEm callback: a game rumbled the virtual pad. Scale byte (0-255) to XInput's ushort
    /// range (×257 maps 0-255 onto 0-65535 exactly) and hand it to the host. Never throws — this runs on
    /// ViGEm's own thread, not ours.</summary>
    private void OnXboxFeedback(object sender, XboxFeedbackEventArgs e)
    {
        try { RumbleReceived?.Invoke((ushort)(e.LargeMotor * 257), (ushort)(e.SmallMotor * 257)); }
        catch (Exception ex) { Trace.WriteLine($"[Emu] rumble handler failed: {ex.Message}"); }
    }

    public void Submit(ControllerState s)
    {
        lock (_gate)
        {
            _lastState = s;
            _hasLastState = true;
            if (_pad is null) return;
            _submitCount++;
            TraceSubmitAggLocked();
            // Remember the newest physical state even while suppressed — it's what the Suppressed setter
            // replays on the way back out. See _lastState.
            if (_suppressed) { SubmitNeutralLocked(); return; }
            try
            {
                if (_type == EmulatedPad.Xbox360) SubmitX360Locked(s);
                else                              SubmitDs4Locked(s);
                _pad.SubmitReport();
                _submitFailures = 0;
            }
            catch (Exception ex) { NoteSubmitFailureLocked(ex); }
        }
    }

    /// <summary>TRACE-ONLY: once-per-second aggregate of Submit() calls. Caller must hold <see cref="_gate"/>.</summary>
    private void TraceSubmitAggLocked()
    {
        long now = Environment.TickCount64;
        if (now - _lastSubmitAggLog < 1000) return;
        if (_submitCount > 0)
            Trace.WriteLine($"[Emu] submits={_submitCount}/sec suppressed={_suppressed}");
        _submitCount = 0;
        _lastSubmitAggLog = now;
    }

    /// <summary>Watchdog for a DEAD ViGEm target: a bus reset, device restart (e.g. the driver-repair
    /// pnputil pass) or driver contention makes every SubmitReport throw while <see cref="Active"/> stays
    /// true — swallowing that forever would leave the game on whatever report last landed (typically the
    /// wheel-open neutral = a permanently dead pad). After a few consecutive failures, reconnect the same
    /// pad type in place (rate-limited); if the revive fails, the pad goes inactive so the host's capture
    /// logic can retry or fall back to the raw controller. Caller must hold <see cref="_gate"/>.</summary>
    private void NoteSubmitFailureLocked(Exception ex)
    {
        Trace.WriteLine($"[Emu] submit failed ({_submitFailures + 1} consecutive): {ex.Message}");
        if (++_submitFailures < 5) return;
        long now = Environment.TickCount64;
        if (now - _lastRevive < 3000) return;
        _lastRevive = now;
        var type = _type;
        bool sup  = _suppressed;
        var latest = _lastState;
        bool hadLatest = _hasLastState;
        StopLocked();
        try
        {
            if (type is { } t)
            {
                ConnectLocked(t);
                _suppressed = sup;
                _lastState = latest;
                _hasLastState = hadLatest;
                if (sup) { _pad!.ResetReport(); _pad.SubmitReport(); }
                else if (hadLatest)
                {
                    if (t == EmulatedPad.Xbox360) SubmitX360Locked(latest);
                    else SubmitDs4Locked(latest);
                    _pad!.SubmitReport();
                }
                Trace.WriteLine($"[Emu] virtual pad revived ({t}) after submit failures");
            }
        }
        catch (Exception rex)
        {
            // The client itself may be what's dead (a bus reset invalidates its open control-device
            // handle) — discard it so the next Start()/revive builds a fresh one, or every retry
            // fails identically against the same stale handle.
            DiscardClientLocked();
            Trace.WriteLine($"[Emu] revive failed — pad inactive: {rex.Message}");
        }
    }

    /// <summary>Dispose + null the ViGEm client after a failed connect. A bus-level reset (e.g. the
    /// driver-repair pnputil pass, or a HidHide/ViGEmBus install) invalidates the client's open
    /// control-device handle, so a retained client makes every later connect throw — only a fresh
    /// client can reach the restarted bus. Caller must hold <see cref="_gate"/>.</summary>
    private void DiscardClientLocked()
    {
        try { _client?.Dispose(); } catch { }
        _client = null;
    }

    private void SubmitX360Locked(ControllerState s)
    {
        var pad = _x360!;
        pad.SetButtonState(Xbox360Button.A, s.Cross);
        pad.SetButtonState(Xbox360Button.B, s.Circle);
        pad.SetButtonState(Xbox360Button.X, s.Square);
        pad.SetButtonState(Xbox360Button.Y, s.Triangle);
        pad.SetButtonState(Xbox360Button.LeftShoulder,  s.L1);
        pad.SetButtonState(Xbox360Button.RightShoulder, s.R1);
        pad.SetButtonState(Xbox360Button.LeftThumb,  s.L3);
        pad.SetButtonState(Xbox360Button.RightThumb, s.R3);
        pad.SetButtonState(Xbox360Button.Back,  s.Create);
        pad.SetButtonState(Xbox360Button.Start, s.Options);
        pad.SetButtonState(Xbox360Button.Guide, s.Ps);
        pad.SetButtonState(Xbox360Button.Up,    s.DpadUp);
        pad.SetButtonState(Xbox360Button.Down,  s.DpadDown);
        pad.SetButtonState(Xbox360Button.Left,  s.DpadLeft);
        pad.SetButtonState(Xbox360Button.Right, s.DpadRight);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger,  s.LeftTrigger);
        pad.SetSliderValue(Xbox360Slider.RightTrigger, s.RightTrigger);
        // DS sticks down-positive; Xbox thumb-Y up-positive → negate Y.
        pad.SetAxisValue(Xbox360Axis.LeftThumbX,  ToAxis(s.LeftStickX));
        pad.SetAxisValue(Xbox360Axis.LeftThumbY,  ToAxis(-s.LeftStickY));
        pad.SetAxisValue(Xbox360Axis.RightThumbX, ToAxis(s.RightStickX));
        pad.SetAxisValue(Xbox360Axis.RightThumbY, ToAxis(-s.RightStickY));
    }

    private void SubmitDs4Locked(ControllerState s)
    {
        var pad = _ds4!;
        pad.SetButtonState(DualShock4Button.Cross,    s.Cross);
        pad.SetButtonState(DualShock4Button.Circle,   s.Circle);
        pad.SetButtonState(DualShock4Button.Square,   s.Square);
        pad.SetButtonState(DualShock4Button.Triangle, s.Triangle);
        pad.SetButtonState(DualShock4Button.ShoulderLeft,  s.L1);
        pad.SetButtonState(DualShock4Button.ShoulderRight, s.R1);
        pad.SetButtonState(DualShock4Button.ThumbLeft,  s.L3);
        pad.SetButtonState(DualShock4Button.ThumbRight, s.R3);
        pad.SetButtonState(DualShock4Button.Share,   s.Create);
        pad.SetButtonState(DualShock4Button.Options, s.Options);
        // Digital L2/R2 bit engages past a small deadzone (XInput's trigger threshold, 30/255) — a bare
        // ">0/>8" would latch the bit permanently on a worn DualSense whose triggers rest a few counts off 0.
        pad.SetButtonState(DualShock4Button.TriggerLeft,  s.LeftTrigger  >= TriggerDigitalThreshold);
        pad.SetButtonState(DualShock4Button.TriggerRight, s.RightTrigger >= TriggerDigitalThreshold);
        pad.SetSpecialButtonsFull(s.Ps ? (byte)0x01 : (byte)0x00);   // bit0 = PS button (bit1 = touchpad)
        pad.SetDPadDirection(DpadOf(s));
        pad.SetSliderValue(DualShock4Slider.LeftTrigger,  s.LeftTrigger);
        pad.SetSliderValue(DualShock4Slider.RightTrigger, s.RightTrigger);
        // DS4 axes are byte (128 = centre); DualSense & DS4 share down/right-positive, so no flip.
        pad.SetAxisValue(DualShock4Axis.LeftThumbX,  ToByte(s.LeftStickX));
        pad.SetAxisValue(DualShock4Axis.LeftThumbY,  ToByte(s.LeftStickY));
        pad.SetAxisValue(DualShock4Axis.RightThumbX, ToByte(s.RightStickX));
        pad.SetAxisValue(DualShock4Axis.RightThumbY, ToByte(s.RightStickY));
    }

    private static DualShock4DPadDirection DpadOf(ControllerState s)
    {
        if (s.DpadUp)    return s.DpadRight ? DualShock4DPadDirection.Northeast
                              : s.DpadLeft  ? DualShock4DPadDirection.Northwest
                                            : DualShock4DPadDirection.North;
        if (s.DpadDown)  return s.DpadRight ? DualShock4DPadDirection.Southeast
                              : s.DpadLeft  ? DualShock4DPadDirection.Southwest
                                            : DualShock4DPadDirection.South;
        if (s.DpadRight) return DualShock4DPadDirection.East;
        if (s.DpadLeft)  return DualShock4DPadDirection.West;
        return DualShock4DPadDirection.None;
    }

    /// <summary>Push a neutral report on whichever pad is up (verified truly centred: sticks 0x80, d-pad
    /// released). Caller must hold <see cref="_gate"/>.</summary>
    private void SubmitNeutralLocked()
    {
        if (_pad is null) return;
        try { _pad.ResetReport(); _pad.SubmitReport(); _submitFailures = 0; }   // buttons released, sticks/triggers centred
        catch (Exception ex) { NoteSubmitFailureLocked(ex); }
    }

    private static short ToAxis(float v) =>
        (short)Math.Clamp((int)Math.Round(v * 32767f), short.MinValue, short.MaxValue);

    private static byte ToByte(float v) =>
        (byte)Math.Clamp((int)Math.Round(v * 127f) + 128, 0, 255);

    public void Dispose()
    {
        lock (_gate)
        {
            StopLocked();
            _client?.Dispose();
            _client = null;
        }
    }
}
