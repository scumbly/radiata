using System.Diagnostics;
using System.Windows.Threading;

namespace ControllerWheel;

/// <summary>
/// Translates the configured wheel-trigger gesture (button chords; touchpad edge-swipe) into wheel
/// open/dismiss calls, reusing App's overlay lifecycle via callbacks. The legacy <c>fn</c> mode stays
/// handled directly in App; this owns the alternate modes. Subscribes once to the controller; all work
/// is marshalled to the UI thread (controller events arrive on the HID read thread).
///
/// It reports only WHICH GROUP invoked — <c>OpenWheel(leftHanded)</c> — and App maps that to a wheel
/// side (so the side convention lives in one place, App.ResolveTrigger): squeeze chords cross by
/// Hold/Toggle, direct-side chords (stick / Back-Start / D-Pad) name the wheel, Swap reverses both.
/// Chord lifetime (hold + tap):
/// opens when a hand's two-button group is fully pressed; the group's HOLD button (the chord builder's
/// FIRST dropdown — the bumper/trigger; see <see cref="HoldFor"/>) keeps it up, and the other button is
/// just a completing TAP that may be released freely. Releasing the HOLD button ends the gesture — firing
/// the armed slice when <c>holdFires</c> (Hold), else leaving the wheel up for ✕/○ (Toggle).
/// Re-triggering while a wheel is up dismisses it (toggle-style). The both-sides enable/disable chord is
/// only honoured pressed TOGETHER (a short grace window) — a held invocation can't be "elevated" into it
/// after the wheel is already up. That grace does NOT apply to a wheel the WHEEL-FLIP grace opened:
/// enable/disable outranks flipping (see <see cref="_gestureFromFlip"/>).
/// </summary>
internal sealed class TriggerInterpreter
{
    internal readonly record struct Callbacks(
        Func<bool> WheelsEnabled,
        Func<bool> Busy,
        Func<bool> OverlayVisible,
        Func<bool> OverlayToggleStyle,   // the OPEN wheel's style (not this interpreter's config) — see StartOrDismiss
        Action<bool> OpenWheel,   // leftHanded
        Action CloseAndFire,
        Action Cancel,
        Action ToggleEnabled);     // the mode's "both sides / both edges" enable-disable chord

    private readonly Dispatcher _ui;
    private readonly Callbacks _cb;

    // The active CHORD modes (fn + touchpad-swipe are handled outside EvaluateMode: fn by the host's Fn
    // handlers, touchpad by EvalTouch). More than one may be enabled at once (multi-select in Settings).
    private string[] _modes = [];
    private bool _touchpadEnabled;
    private bool _holdFires, _toggleStyle;

    // Onboarding practice: EVERY gesture of the detected kind is live at once (any-order try-out).
    private bool _practiceAll;
    private string[] _practiceModes = [];

    /// <summary>The trigger token whose gesture performed the most recent open — "fn" is reported by the
    /// host's own Fn handlers; this covers the chord/touchpad modes (incl. practice mode, where several
    /// are live at once).</summary>
    public string? LastInvokeToken { get; private set; }

    // Live button state (only touched on the UI thread).
    private bool _l1, _r1, _l2, _r2, _l3, _r3, _ps, _create, _options;
    // The pad's dedicated extra buttons (an extra-button pad's L4/R4), as a CHORD half. A lone press is
    // the "fn" token, which the host's own handlers own — these are only read by the extra-* modes, so
    // the two paths never both act on one press (App.FnInvoke/FnChord return unless "fn" is enabled).
    private bool _fnL, _fnR;
    // Cardinal D-pad (hat 6/2/0/4): Left/Right pick the invocation wheel; Up/Down are the D-pad chord's
    // enable(Up)/disable(Down) directions (see EnableDisableFires).
    private bool _dpadLeft, _dpadRight, _dpadUp, _dpadDown;

    // Gesture latch: a side-group currently owns (or just owned) an open wheel.
    private bool _gestureActive, _gestureLeftHand, _dismissed;
    private string _gestureMode = "";   // which mode's group holds the active gesture (matters in practice)

    /// <summary>True while an invocation chord is physically engaged (set on open, cleared on full
    /// release/dismiss). Lets the host gate "while the trigger is held" behaviours for non-Fn modes —
    /// e.g. resurrecting an EMPTY (silently-invoked) wheel via a stick click.</summary>
    public bool GestureHeld => _gestureActive;
    private bool _disableLatched;   // the both-sides enable/disable chord (fires once per press)

    /// <summary>True when a Select/Start press AT THIS MOMENT belongs to an enabled summon chord: a
    /// viewmenu gesture currently owns the open wheel, or an enabled viewmenu mode's primary
    /// (bumper / trigger / stick click) is physically held, so the press forms or extends that chord.
    /// The host gates the on-wheel Select/Start actions (material cycle / Settings) on this rather than
    /// on the static gesture config — a plain press keeps those actions under EVERY configuration.
    /// UI thread only, like all state here. ⚠ Relies on the reader raising Select/Start AFTER the chord
    /// primaries within one report (see ControllerReader) so this reads settled state.</summary>
    public bool SelectStartChordEngaged()
    {
        if (_gestureActive && _gestureMode is TriggerModes.ViewMenuBumpers
            or TriggerModes.ViewMenuTriggers or TriggerModes.ViewMenuStick
            or TriggerModes.ExtraSelectStart) return true;
        foreach (var m in _practiceAll ? _practiceModes : _modes)
            switch (m)
            {
                case TriggerModes.ViewMenuBumpers:  if (_l1 || _r1) return true; break;
                case TriggerModes.ViewMenuTriggers: if (_l2 || _r2) return true; break;
                case TriggerModes.ViewMenuStick:    if (_l3 || _r3) return true; break;
                case TriggerModes.ExtraSelectStart: if (_fnL || _fnR) return true; break;
            }
        return false;
    }

    // When the current gesture began (Environment.TickCount64). The enable/disable chord may only fire
    // while a wheel is up within this window of the invocation — pressing the other side's chord LATER
    // must not "elevate" a held invocation into a toggle.
    private long _gestureStartTick;
    // internal: the Fn trigger mode lives outside this class (App.FnChord) but shares this window.
    internal const long EnableDisableGraceMs = 250;

    // …with ONE exception: a wheel that the user never deliberately opened. The wheel-flip grace lets a
    // lone shoulder re-open the other wheel, and the first shoulder of the enable/disable chord looks
    // exactly like that — pressing L1 to start "both bumpers + Select" flips a wheel up, and the remaining
    // buttons would then land OUTSIDE the grace above and be swallowed. Enable/disable OUTRANKS the flip:
    // a flip-opened wheel doesn't start the grace clock at all, so the chord still completes at human
    // speed and the stray wheel is dismissed on the way through.
    private bool _gestureFromFlip;

    // Touchpad state — two fingers (0 = primary swipe; both = enable/disable).
    private bool _t0Active, _t1Active, _touchTriggered, _touchDisableLatched;
    private float _t0StartX, _t0X, _t1StartX, _t1X;

    public TriggerInterpreter(IController c, Dispatcher ui, Callbacks cb)
    {
        _ui = ui; _cb = cb;
        c.L1Changed      += p => On(() => _l1 = p);
        c.R1Changed      += p => On(() => _r1 = p);
        c.L2Changed      += p => On(() => _l2 = p);
        c.R2Changed      += p => On(() => _r2 = p);
        c.L3Changed      += p => On(() => _l3 = p);
        c.R3Changed      += p => On(() => _r3 = p);
        c.PsChanged      += p => On(() => _ps = p);
        c.CreateChanged  += p => On(() => _create = p);
        c.OptionsChanged += p => On(() => _options = p);
        c.TriggerLeftChanged  += p => On(() => _fnL = p);
        c.TriggerRightChanged += p => On(() => _fnR = p);
        // D-pad as a chord modifier: cardinals Left(6)/Right(2) arm an invocation side, Up(0)/Down(4) drive
        // the D-pad chord's enable/disable. Only fires while no wheel is open (invocation) or as the
        // enable/disable chord; the volume scrub / edit-nudge own the d-pad WHILE a wheel is up (no overlap).
        c.DPadChanged    += d => On(() => { _dpadLeft = d == 6; _dpadRight = d == 2; _dpadUp = d == 0; _dpadDown = d == 4; });
        c.TouchpadChanged  += (x, _, contact) => _ui.InvokeAsync(() => { if (contact) { if (!_t0Active) { _t0Active = true; _t0StartX = x; } _t0X = x; } else _t0Active = false; EvalTouch(); });
        c.Touchpad2Changed += (x, _, contact) => _ui.InvokeAsync(() => { if (contact) { if (!_t1Active) { _t1Active = true; _t1StartX = x; } _t1X = x; } else _t1Active = false; EvalTouch(); });
    }

    private void On(Action update) => _ui.InvokeAsync(() => { update(); Evaluate(); });

    // ── Rejection diagnostics ────────────────────────────────────────────────────
    // Makes "I did the gesture and nothing happened" diagnosable. Every rejection below is a silent return
    // on a hot path (one per button event), so the traces are throttled per distinct reason rather than
    // emitted per press. UI thread only, like the rest of the class.
    private readonly Dictionary<string, long> _rejectLogged = [];
    private readonly HashSet<string> _unknownTokenLogged = [];
    private const long RejectLogThrottleMs = 30_000;

    private void TraceReject(string key, string message)
    {
        long now = Environment.TickCount64;
        if (_rejectLogged.TryGetValue(key, out long last) && now - last <= RejectLogThrottleMs) return;
        _rejectLogged[key] = now;
        Trace.WriteLine(message);
    }

    /// <summary>The shared "can this gesture open a wheel right now" gate, with the reason traced. Busy
    /// means another surface (Game Grid / arcade / edit) owns input; wheels-off is the enable/disable chord.</summary>
    private bool OpenBlocked(string where)
    {
        if (!_cb.WheelsEnabled())
        {
            TraceReject($"disabled:{where}", $"[Trigger] {where}: gesture ignored — wheels are DISABLED " +
                                             "(the enable/disable chord toggles them back on)");
            return true;
        }
        if (_cb.Busy())
        {
            TraceReject($"busy:{where}", $"[Trigger] {where}: gesture ignored — another surface owns input " +
                                         "(Game Grid / arcade / edit)");
            return true;
        }
        return false;
    }

    /// <summary>Set the active invocation gesture SET + activation flags. <paramref name="modes"/> is the
    /// full enabled set for the kind (may include "fn"/"touchpad-swipe", which are filtered here: fn is the
    /// host's own handlers, touchpad is EvalTouch); the rest are chord modes evaluated together. Cancels a
    /// gesture-owned wheel if one is open — EXCEPT in practice-all mode, where every gesture is live anyway
    /// and the write is just the wizard recording the tried option as the keeper: cancelling there would
    /// make the practice wheel vanish the moment a not-currently-selected gesture opened it.</summary>
    public void Configure(IReadOnlyList<string> modes, bool holdFires, bool toggleStyle)
    {
        var snapshot = modes.ToArray();
        if (_ui.CheckAccess()) ConfigureCore(snapshot, holdFires, toggleStyle);
        else _ui.InvokeAsync(() => ConfigureCore(snapshot, holdFires, toggleStyle));
    }

    private void ConfigureCore(IReadOnlyList<string> modes, bool holdFires, bool toggleStyle)
    {
        if (!_practiceAll)
        {
            if (_gestureActive && _cb.OverlayVisible()) _cb.Cancel();
            _gestureActive = _dismissed = _disableLatched = _gestureFromFlip = false;
            _t0Active = _t1Active = _touchTriggered = _touchDisableLatched = false;
            _flipEligibleTick = 0;
        }
        _modes = [.. modes.Where(m => m != TriggerModes.FnButtons && m != TriggerModes.TouchpadSwipe)];
        _touchpadEnabled = modes.Contains(TriggerModes.TouchpadSwipe);
        _holdFires = holdFires; _toggleStyle = toggleStyle;
        // The live gesture set is the first thing to check against a "my gesture does nothing" report — and
        // re-evaluating a fixed config re-earns any unknown-token warning.
        _unknownTokenLogged.Clear();
        Trace.WriteLine($"[Trigger] gestures live: {(modes.Count == 0 ? "(none)" : string.Join(", ", modes))} " +
                        $"— holdFires={holdFires} toggleStyle={toggleStyle}");
    }

    /// <summary>Hard-reset every button and gesture latch. Call on controller DISCONNECT: the reader's
    /// cached state is zeroed without release events, so a mid-chord drop-out otherwise leaves stale
    /// side-button state latched here — the gesture never "releases", and the first real press+release
    /// after reconnect fires a phantom close (a disconnect-invariant violation).</summary>
    public void ResetGesture()
    {
        if (_ui.CheckAccess()) ResetGestureCore();
        else _ui.InvokeAsync(ResetGestureCore);
    }

    private void ResetGestureCore()
    {
        _l1 = _r1 = _l2 = _r2 = _l3 = _r3 = _ps = _create = _options = false;
        _fnL = _fnR = false;
        _dpadLeft = _dpadRight = _dpadUp = _dpadDown = false;
        _gestureActive = _dismissed = _disableLatched = _gestureFromFlip = false;
        _t0Active = _t1Active = _touchTriggered = _touchDisableLatched = false;
        _flipEligibleTick = 0;
    }

    /// <summary>Onboarding practice: evaluate EVERY chord mode in <paramref name="modes"/> (the detected
    /// kind's option set) plus the touchpad swipe simultaneously, so the user can try each gesture in any
    /// order without re-configuring. The host's Fn handlers cover "fn" themselves.</summary>
    public void SetPracticeAllModes(bool on, IReadOnlyList<string>? modes = null) => _ui.InvokeAsync(() =>
    {
        _practiceAll   = on;
        _practiceModes = on ? (modes ?? []).Where(m => m != TriggerModes.FnButtons
                                                       && m != TriggerModes.TouchpadSwipe).ToArray() : [];
        // Touchpad liveness follows the practice LIST too (the wizard's Recommended state passes only the
        // recommended chord — a touchpad swipe must not open a wheel there; see EvalTouch).
        _practiceTouchpad = on && (modes ?? []).Contains(TriggerModes.TouchpadSwipe);
        _gestureActive = _dismissed = _disableLatched = _gestureFromFlip = false;
        _t0Active = _t1Active = _touchTriggered = _touchDisableLatched = false;
        _flipEligibleTick = 0;
    });
    private bool _practiceTouchpad;

    // Practice: during practice-all, only THIS token's enable/disable chord toggles the wheels (every
    // INVOCATION stays live, but practicing a different enable/disable pair than the wizard's card shows
    // would teach the wrong chord). Null = unrestricted. Ignored entirely outside practice-all.
    private string? _practiceChordMode;
    public void SetPracticeChordMode(string? mode) => _ui.InvokeAsync(() => _practiceChordMode = mode);

    /// <summary>Decompose a mode into each hand's SIDE button (the left/right disambiguator)
    /// and its MODIFIER (the rest of the group — often shared, e.g. Home/View). False = not a chord mode
    /// (fn / touchpad-swipe handled elsewhere).</summary>
    private bool SidesFor(string mode, out bool sL, out bool mL, out bool sR, out bool mR)
    {
        switch (mode)
        {
            case TriggerModes.BumpersHome:     sL = _l1; mL = _ps; sR = _r1; mR = _ps; return true;
            case TriggerModes.TriggersHome:    sL = _l2; mL = _ps; sR = _r2; mR = _ps; return true;
            case TriggerModes.BumpersTriggers: sL = _l1; mL = _l2; sR = _r1; mR = _r2; return true;
            // The SIDE (which wheel) is the STICK-click; the bumper/trigger is a SHARED modifier — EITHER
            // hand's works. App.ResolveTrigger opens the wheel on the clicked stick's side directly (no
            // Hold/Toggle crossing) — see IsDirectSideChord. Enable/disable falls to the generic case:
            // both sticks + the shared modifier.
            case TriggerModes.BumpersStick:    { bool m = _l1 || _r1; sL = _l3; mL = m; sR = _r3; mR = m; return true; }
            case TriggerModes.TriggersStick:   { bool m = _l2 || _r2; sL = _l3; mL = m; sR = _r3; mR = m; return true; }
            // Back/Start is a SHARED modifier (either one works); the BUMPER/TRIGGER hand picks the side,
            // exactly like Fn and Bumper+Home. Not direct-side: Hold opens the OPPOSITE wheel (free hand
            // aims), Toggle is uncrossed — see App.ResolveTrigger. Legacy ViewMenuStick keeps its
            // stick-named side (still direct-side).
            case TriggerModes.ViewMenuStick:    { bool m = _create || _options; sL = _l3; mL = m; sR = _r3; mR = m; return true; }
            case TriggerModes.ViewMenuBumpers:  { bool m = _create || _options; sL = _l1; mL = m; sR = _r1; mR = m; return true; }
            case TriggerModes.ViewMenuTriggers: { bool m = _create || _options; sL = _l2; mL = m; sR = _r2; mR = m; return true; }
            // D-pad direction names the SIDE (Left → left, Right → right); the bumper/trigger is SHARED.
            case TriggerModes.BumpersDpad:      { bool b = _l1 || _r1; sL = _dpadLeft; mL = b; sR = _dpadRight; mR = b; return true; }
            case TriggerModes.TriggersDpad:     { bool t = _l2 || _r2; sL = _dpadLeft; mL = t; sR = _dpadRight; mR = t; return true; }
            // Extra-button pads: L4/R4 take the role the bumper/trigger plays above — the per-hand side
            // button for the squeeze-style chords, and the SHARED hold for the stick / D-pad ones (where
            // the click or the direction names the wheel).
            case TriggerModes.ExtraHome:        sL = _fnL; mL = _ps; sR = _fnR; mR = _ps; return true;
            // ⚠ Shoulder pairings are OPPOSITE-HAND, unlike the Bumper family's same-hand squeeze: L4
            // pairs with R1/R2, R4 with L1/L2. One hand holds the paddle while the other taps, and the
            // paddle still names the side — so L4 + a right shoulder is the LEFT group and opens the
            // RIGHT wheel through the usual cross (App.ResolveTrigger), which is what the hands expect.
            case TriggerModes.ExtraTriggers:    sL = _fnL; mL = _r2; sR = _fnR; mR = _l2; return true;
            case TriggerModes.ExtraBumpers:     sL = _fnL; mL = _r1; sR = _fnR; mR = _l1; return true;
            case TriggerModes.ExtraSelectStart: { bool m = _create || _options; sL = _fnL; mL = m; sR = _fnR; mR = m; return true; }
            case TriggerModes.ExtraStick:       { bool e = _fnL || _fnR; sL = _l3; mL = e; sR = _r3; mR = e; return true; }
            case TriggerModes.ExtraDpad:        { bool e = _fnL || _fnR; sL = _dpadLeft; mL = e; sR = _dpadRight; mR = e; return true; }
            default: sL = mL = sR = mR = false; return false;
        }
    }

    /// <summary>Whether the enable/disable chord for <paramref name="mode"/> is currently pressed (fires
    /// once per press), and whether ANY of its buttons are held (<paramref name="held"/>, for the latch
    /// reset). Per-mode so each stays ergonomic:
    ///   • Bumper/Trigger + Select/Start → BOTH bumpers/triggers + (Select OR Start) [toggle]
    ///   • Bumper/Trigger + D-Pad → BOTH bumpers/triggers + D-Pad Up = ENABLE, + D-Pad Down = DISABLE
    ///     (directional; resolved against the current WheelsEnabled state so Up only fires when disabled
    ///     and Down only when enabled)
    ///   • everything else → the generic both-sides + both-modifiers of the invocation chord.</summary>
    private bool EnableDisableFires(string mode, bool sL, bool mL, bool sR, bool mR, out bool held)
    {
        switch (mode)
        {
            case TriggerModes.ViewMenuBumpers:
                held = _l1 || _r1 || _create || _options;
                return _l1 && _r1 && (_create || _options);
            case TriggerModes.ViewMenuTriggers:
                held = _l2 || _r2 || _create || _options;
                return _l2 && _r2 && (_create || _options);
            case TriggerModes.BumpersDpad:
                held = _l1 || _r1 || _dpadUp || _dpadDown;
                return (_l1 && _r1 && _dpadUp && !_cb.WheelsEnabled()) || (_l1 && _r1 && _dpadDown && _cb.WheelsEnabled());
            case TriggerModes.TriggersDpad:
                held = _l2 || _r2 || _dpadUp || _dpadDown;
                return (_l2 && _r2 && _dpadUp && !_cb.WheelsEnabled()) || (_l2 && _r2 && _dpadDown && _cb.WheelsEnabled());
            case TriggerModes.ExtraSelectStart:
                held = _fnL || _fnR || _create || _options;
                return _fnL && _fnR && (_create || _options);
            case TriggerModes.ExtraDpad:
                held = _fnL || _fnR || _dpadUp || _dpadDown;
                return (_fnL && _fnR && _dpadUp && !_cb.WheelsEnabled()) || (_fnL && _fnR && _dpadDown && _cb.WheelsEnabled());
            default:
                held = sL || sR || mL || mR;
                return sL && sR && mL && mR;
        }
    }

    private void Evaluate()
    {
        // The chord modes under evaluation: the configured set (possibly several — multi-select), or in
        // onboarding practice every chord mode of the detected kind. Once a gesture owns the wheel, only
        // its owning group is tracked (for the release); otherwise the first triggering mode wins.
        var modes = _practiceAll ? _practiceModes : _modes;
        if (_gestureActive) { EvaluateMode(_gestureMode); return; }
        foreach (var m in modes)
        {
            EvaluateMode(m);
            if (_gestureActive || _disableLatched) return;
        }
    }

    private void EvaluateMode(string mode)
    {
        if (!SidesFor(mode, out bool sL, out bool mL, out bool sR, out bool mR))
        {
            // A token SidesFor doesn't know is dead weight: that gesture can never fire, for the whole
            // session, with no other symptom. Config-driven and therefore stable, so log it once per token
            // (not throttled) — cleared on Configure so a fixed config reports clean.
            if (_unknownTokenLogged.Add(mode))
                Trace.WriteLine($"[Trigger] trigger token \"{mode}\" is not a chord mode — that gesture " +
                                "will never open a wheel (stale or hand-edited config?)");
            return;
        }

        // Enable/disable chord (fires once per press; ungated by WheelsEnabled so it can re-enable). The
        // combo is PER-MODE (EnableDisableFires), kept ergonomic rather than literally "both sides" of the
        // invocation: both Bumpers/Triggers + (Select or Start); both Bumpers/Triggers + D-Pad Up=enable /
        // Down=disable; else the generic both-sides + both-modifiers.
        bool edFire = EnableDisableFires(mode, sL, mL, sR, mR, out bool edHeld);
        // Practice: only the wizard's chosen chord may toggle (see SetPracticeChordMode).
        if (_practiceAll && _practiceChordMode is not null && mode != _practiceChordMode)
        {
            if (edFire)
                TraceReject($"practice-chord:{mode}",
                            $"[Trigger] enable/disable chord for \"{mode}\" ignored — practice is teaching " +
                            $"\"{_practiceChordMode}\"");
            edFire = false;
        }
        // Pressed-together only: with a wheel already up, the chord counts just inside the grace window of
        // the invocation (a simultaneous both-sides press still lands even though one side opened first) —
        // completing the other side LATER never elevates a held invocation into enable/disable.
        if (edFire && _cb.OverlayVisible() && !_gestureFromFlip
            && Environment.TickCount64 - _gestureStartTick > EnableDisableGraceMs)
        {
            // Worth a trace line: from the outside this swallow is indistinguishable from the chord not
            // being recognised at all.
            TraceReject($"ed-grace:{mode}",
                        $"[Trigger] enable/disable chord for \"{mode}\" swallowed — completed " +
                        $"{Environment.TickCount64 - _gestureStartTick}ms after the invoke, past the " +
                        $"{EnableDisableGraceMs}ms together-press grace (press both sides at once)");
            edFire = false;
        }
        if (edFire)
        {
            if (!_disableLatched)
            {
                _disableLatched = true;
                _gestureActive = _dismissed = _gestureFromFlip = false;
                _flipEligibleTick = 0;   // don't leave a pending flip armed behind an enable/disable press
                Trace.WriteLine($"[Trigger] enable/disable chord latched (mode={mode}) — handing off to the host");
                _cb.ToggleEnabled();
            }
            return;
        }
        // The latch holds until the whole enable/disable group is idle: button releases arrive as separate
        // events even within one HID report, so a staggered release passes through states that read as a
        // one-sided gesture below — clearing early lets the chord open a wheel + fire a slice on release.
        if (!edHeld) _disableLatched = false;

        // A side opens when its side-button + modifier are down and the OTHER side-button is NOT — so a
        // SHARED modifier (Home / View-Menu) doesn't make both sides look active. Once open, only the
        // group's HOLD button keeps it up (hold + tap — the tap half may be released freely).
        bool openLeft  = sL && mL && !sR;
        bool openRight = sR && mR && !sL;

        if (!_gestureActive)
        {
            if (_disableLatched) return;   // mid-release enable/disable chord — not a one-sided open
            // Wheel-flip, sequential case: within the grace of the last release, the OPPOSITE shoulder
            // alone opens the other wheel — no tap half required (that's the whole point: a quick flip
            // without re-forming the chord). A tap held would make this a normal chord press instead,
            // which the ordinary open below already handles.
            if (_flipEligibleTick != 0 && mode == _flipMode && !mL && !mR)
            {
                if (Environment.TickCount64 - _flipEligibleTick > FlipGraceMs) _flipEligibleTick = 0;
                else if ((_flipFromLeftHand ? sR : sL) && !(_flipFromLeftHand ? sL : sR))
                {
                    FlipOpen(mode, !_flipFromLeftHand);
                    return;
                }
            }
            if (openLeft)       StartOrDismiss(mode, leftHand: true);
            else if (openRight) StartOrDismiss(mode, leftHand: false);
        }
        else if (mode == _gestureMode)
        {
            var (holdL, holdR) = HoldFor(mode, sL, mL, sR, mR);
            if (_gestureLeftHand ? holdL : holdR) return;   // HOLD button still down — the gesture lives
            _gestureActive = false;
            // Wheel-flip, overlap case: the OPPOSITE shoulder is already down as this hold releases —
            // open the other wheel instead of firing/cancelling. Only for hold-style chords whose hold
            // is a per-hand shoulder (FlipCapable), and never while a tap/modifier half is held: shoulder
            // + shoulder + tap is the enable/disable chord forming, not a flip.
            if (_holdFires && !_dismissed && FlipCapable(mode) && !mL && !mR
                && (_gestureLeftHand ? sR : sL))
            {
                _dismissed = false;
                FlipOpen(mode, !_gestureLeftHand);
                return;
            }
            if (!_dismissed)
            {
                if (_holdFires) _cb.CloseAndFire();   // Hold fires the armed slice on release
                // Wheel-flip, sequential case: for a short grace after this release, pressing the
                // OPPOSITE shoulder alone (no tap needed) re-opens on the other side — see the
                // grace check at the top of this method.
                if (_holdFires && FlipCapable(mode))
                {
                    _flipEligibleTick = Environment.TickCount64;
                    _flipMode = mode;
                    _flipFromLeftHand = _gestureLeftHand;
                }
            }
            _dismissed = false;
        }
    }

    // ── Wheel-flip grace ─────────────────────────────────────────────────────────
    // "Flipping" from one wheel to the other without re-forming the full chord: while a hold chord is
    // engaged, pressing the opposite shoulder and releasing the first opens the other wheel (overlap);
    // and for a short grace after an ordinary release, the opposite shoulder ALONE re-opens on the other
    // side (sequential). Restricted to modes whose HOLD is a per-hand shoulder button — the stick/D-pad
    // chords hold on a SHARED bumper/trigger, so "the opposite shoulder" doesn't identify a hand there.
    // The sequential flip makes a lone shoulder press ambiguous with the first button of the
    // enable/disable chord, so the window is kept as short as still feels like a flick (the ambiguity
    // itself is resolved by _gestureFromFlip; this just narrows the blast radius).
    private const long FlipGraceMs = 250;
    private long   _flipEligibleTick;
    private string _flipMode = "";
    private bool   _flipFromLeftHand;

    private static bool FlipCapable(string mode) => mode is TriggerModes.BumpersHome
        or TriggerModes.TriggersHome or TriggerModes.BumpersTriggers
        or TriggerModes.ViewMenuBumpers or TriggerModes.ViewMenuTriggers
        // L4/R4 are per-hand buttons, so "the opposite one" identifies a hand exactly like a bumper.
        or TriggerModes.ExtraHome or TriggerModes.ExtraTriggers or TriggerModes.ExtraBumpers
        or TriggerModes.ExtraSelectStart;

    /// <summary>Open the opposite side's wheel as a continuation of the just-released gesture. Bypasses
    /// StartOrDismiss's "wheel up → dismiss" branch: a flip is a re-open, not a re-trigger.</summary>
    private void FlipOpen(string mode, bool leftHand)
    {
        _flipEligibleTick = 0;
        if (OpenBlocked($"flip {mode}")) return;
        _gestureActive = true;
        _gestureLeftHand = leftHand;
        _gestureMode = mode;
        _gestureStartTick = Environment.TickCount64;
        _gestureFromFlip = true;   // exempts the enable/disable chord from the grace — see _gestureFromFlip
        LastInvokeToken = mode;
        _dismissed = false;
        _cb.OpenWheel(leftHand);
    }

    /// <summary>The HOLD half of a two-button chord, per hand — the button the chord builder's FIRST
    /// dropdown names (the bumper/trigger). For the stick/D-pad chords the side element is a momentary
    /// click/tap, so the shared bumper/trigger modifier is the hold; for the legacy Select/Start+stick
    /// mode the Select/Start is. Everything else holds on its side button.</summary>
    private static (bool holdL, bool holdR) HoldFor(string mode, bool sL, bool mL, bool sR, bool mR) => mode switch
    {
        TriggerModes.BumpersStick or TriggerModes.TriggersStick or TriggerModes.ViewMenuStick or
        TriggerModes.BumpersDpad  or TriggerModes.TriggersDpad or
        TriggerModes.ExtraStick   or TriggerModes.ExtraDpad => (mL, mR),
        _ => (sL, sR),
    };

    private void StartOrDismiss(string mode, bool leftHand)
    {
        _flipEligibleTick = 0;   // a full chord press supersedes any pending flip grace
        _gestureActive = true;
        _gestureLeftHand = leftHand;
        _gestureMode = mode;
        _gestureStartTick = Environment.TickCount64;   // anchors the enable/disable grace window
        _gestureFromFlip = false;                      // a deliberate chord press — the grace applies
        LastInvokeToken = mode;
        if (_cb.OverlayVisible())          // a wheel is already up → re-trigger dismisses (toggle-style)
        {
            _dismissed = true;
            // Dismiss based on the OPEN WHEEL's style, not this interpreter's configured one. The two
            // diverge whenever gestures of both styles are live at once (onboarding practice-all, or a
            // touchpad swipe alongside hold chords): a swipe-opened TOGGLE wheel stays up until an
            // explicit dismiss, and a chord press against it must count as that dismiss — otherwise
            // chords no-op against the standing wheel and read as dead input.
            if (_cb.OverlayToggleStyle()) _cb.Cancel();
            return;
        }
        // Game Grid open (Busy) → invoking a wheel does nothing; the grid stays up.
        if (OpenBlocked($"chord {mode}")) { _gestureActive = false; return; }
        _dismissed = false;
        _cb.OpenWheel(leftHand);
    }

    // Touchpad edge-swipe (always toggle-style): one finger from an edge, inward, opens the matching
    // wheel (re-swipe dismisses). BOTH fingers from opposite edges inward = the enable/disable chord.
    private const float EdgeBand = 0.15f, InwardTravel = 0.25f;
    private bool InFromLeft (float startX, float x) => startX <= EdgeBand      && x - startX >= InwardTravel;
    private bool InFromRight(float startX, float x) => startX >= 1f - EdgeBand && startX - x >= InwardTravel;

    private void EvalTouch()
    {
        // During practice-all the wizard's explicit list is authoritative (its Recommended state passes
        // ONLY the recommended chord — a swipe must not open a wheel there); otherwise the saved config.
        if (!(_practiceAll ? _practiceTouchpad : _touchpadEnabled))
        {
            // Only meaningful once the finger has actually travelled — otherwise every stray touchpad
            // contact (they're constant on a DualSense) would claim a rejected swipe.
            if (_t0Active && (InFromLeft(_t0StartX, _t0X) || InFromRight(_t0StartX, _t0X)))
                TraceReject("touchpad-off", "[Trigger] edge swipe ignored — the touchpad gesture is not enabled");
            return;
        }
        if (!_t0Active && !_t1Active) { _touchTriggered = false; _touchDisableLatched = false; return; }

        // Two fingers swiping in from opposite edges → enable/disable chord (fires once).
        bool bothEdges = _t0Active && _t1Active &&
            ((InFromLeft(_t0StartX, _t0X) && InFromRight(_t1StartX, _t1X)) ||
             (InFromRight(_t0StartX, _t0X) && InFromLeft(_t1StartX, _t1X)));
        if (bothEdges)
        {
            if (!_touchDisableLatched)
            {
                _touchDisableLatched = true; _touchTriggered = true;
                // Practice: the two-finger toggle only counts when touchpad-swipe IS the chosen chord.
                if (!_practiceAll || _practiceChordMode is null || _practiceChordMode == TriggerModes.TouchpadSwipe)
                    _cb.ToggleEnabled();
            }
            return;
        }

        // Single-finger open (suppressed if a second finger is present — that's a two-finger gesture).
        if (_touchTriggered || _t1Active || !_t0Active) return;
        bool fromLeft  = InFromLeft(_t0StartX, _t0X);
        bool fromRight = InFromRight(_t0StartX, _t0X);
        if (!fromLeft && !fromRight) return;

        _touchTriggered = true;
        LastInvokeToken = TriggerModes.TouchpadSwipe;
        if (_cb.OverlayVisible()) { _cb.Cancel(); return; }   // re-swipe dismisses
        if (OpenBlocked("touchpad swipe")) return;            // grid open (Busy) → swipe does nothing
        _cb.OpenWheel(fromLeft);                              // fromLeft ⇒ left-handed
    }
}
