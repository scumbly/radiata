namespace ControllerWheel;

/// <summary>The picker's steering: level inputs in, discrete carousel steps out. One step per flick of the
/// stick or tap of the d-pad, then auto-repeat while the direction is held. Pure and clock-free (the caller
/// supplies <c>dt</c>) so the harness can drive it.
///
/// <para>Only the stick's horizontal component is read. A carousel has one axis, and a diagonal push is
/// just its sideways part — the vertical component must never do anything here.</para></summary>
public sealed class ArcadeCarouselNav
{
    private int    _stickDir;     // armed stick direction with hysteresis, -1 / 0 / +1
    private int    _heldDir;      // the direction currently producing steps
    private double _repeatLeft;

    /// <summary>Returns -1, 0 or +1 carousel steps for this frame. +1 = the cabinet to the right of the
    /// front one comes forward.</summary>
    public int Step(float stickX, bool dpadLeft, bool dpadRight, double dt)
    {
        double ax = Math.Abs(stickX);
        if (ax >= ArcadePickerTuning.StepArmThreshold)          _stickDir = Math.Sign(stickX);
        else if (ax <= ArcadePickerTuning.StepReleaseThreshold) _stickDir = 0;

        // Both d-pad directions down cancel out; the stick, when armed, outranks the d-pad.
        int dpadDir = dpadRight == dpadLeft ? 0 : dpadRight ? 1 : -1;
        int dir = _stickDir != 0 ? _stickDir : dpadDir;

        if (dir == 0) { _heldDir = 0; return 0; }
        if (dir != _heldDir)
        {
            _heldDir    = dir;
            _repeatLeft = ArcadePickerTuning.RepeatDelaySeconds;
            return dir;
        }

        _repeatLeft -= Math.Max(0, dt);
        if (_repeatLeft > 0) return 0;
        // Re-arm from the repeat period, never from an accumulated deficit — a frame hitch must not burst.
        _repeatLeft = ArcadePickerTuning.RepeatSeconds;
        return dir;
    }

    public void Reset()
    {
        _stickDir   = 0;
        _heldDir    = 0;
        _repeatLeft = 0;
    }
}
