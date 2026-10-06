namespace ControllerWheel;

/// <summary>
/// Shared RELATIVE steering for a rim-riding avatar: stick LEFT/RIGHT drives it around the rim at a fixed
/// rate, the way a Tempest cabinet's spinner does. A game wanting the ABSOLUTE model instead — the stick's
/// angle is a rim position the avatar slides to — reads <see cref="ArcadeInput.ScreenAngle"/> directly and
/// owns its own switch between the two (e.g. Connate's <c>RadialAiming</c> pause option).
///
/// <para>Screen-angle convention throughout: 0 = 12 o'clock, increasing CLOCKWISE, matching
/// <see cref="ArcadeInput.ScreenAngle"/> and the wheel's own <c>AngleToIndex</c>.</para>
/// </summary>
public static class ArcadeAim
{
    /// <summary>Stick deadzone for relative steering. Lower than any absolute-mode deadzone on purpose: in
    /// relative mode a small deflection is a legitimate slow nudge, whereas in absolute mode a small
    /// deflection is an ambiguous angle and must be ignored.</summary>
    public static double RelativeDeadzone = 0.18;

    /// <summary>Signed steering rate in −1..+1 from the stick's horizontal axis alone. Positive = counter-
    /// clockwise in screen angle (stick right).
    ///
    /// <para>Vertical deflection is ignored entirely rather than folded in, so pushing diagonally — which
    /// happens constantly on a thumbstick — never slows the turn or fights it.</para>
    ///
    /// <para>⚠ The sign is anchored to six o'clock, not twelve. Screen angle increases clockwise, so at the
    /// bottom of the circle a decreasing angle is what moves the avatar rightward on screen — hence the
    /// negation. Push right at 6 o'clock and the craft goes right; the same push at 12 o'clock necessarily
    /// looks backwards, and that's the accepted half of the trade. Six o'clock is the correct anchor because
    /// it's where a rim-riding avatar spends most of its time in front of the player.</para>
    ///
    /// <para>Direction is a fixed screen convention, not "the way the avatar is facing". Don't "fix" the
    /// 12-o'clock case by flipping the sign above the horizon — that makes the control reverse mid-lap, which
    /// is far worse than a consistently mirrored top half.</para></summary>
    public static double Rate(in ArcadeInput input)
    {
        double x = input.StickX;
        if (Math.Abs(x) < RelativeDeadzone) return 0;
        // Rescale past the deadzone so the usable range starts at zero rather than stepping to 0.18.
        double t = (Math.Abs(x) - RelativeDeadzone) / (1.0 - RelativeDeadzone);
        return -Math.Sign(x) * Math.Clamp(t, 0, 1);
    }

    /// <summary>How long a released stick takes to bleed most of its speed away (time constant, seconds).
    /// Small on purpose: this is a fractional slide that keeps the craft from stopping like a brick wall, not
    /// momentum you have to plan around.</summary>
    public static double BrakeSeconds = 0.015;

    /// <summary>
    /// The shipping steering model. Returns the new angular velocity in radians/second.
    ///
    /// <para>Speeding up is instant and proportional to tilt — a half-tilted stick is half speed, and it
    /// arrives on the frame you ask for it. No acceleration ramp: in a dodging game the input has to land
    /// now.</para>
    ///
    /// <para>Slowing down is the only thing with a clock — an exponential bleed toward the requested
    /// speed over <see cref="BrakeSeconds"/>. Stopping dead reads as a bug; this gives just enough follow-through
    /// to feel physical while still being over inside a tenth of a second.</para>
    ///
    /// <para>Applies when the request is smaller than the current speed, or reverses it. Reversal deliberately
    /// brakes rather than snapping: whipping the stick the other way should cost the slide, not teleport.</para>
    /// </summary>
    public static double StepVelocity(double velocity, in ArcadeInput input, double degPerSec, double dt)
    {
        double target = Rate(input) * degPerSec * Math.PI / 180.0;

        bool speedingUp = Math.Abs(target) >= Math.Abs(velocity)
                          && (velocity == 0 || Math.Sign(target) == Math.Sign(velocity) || target == 0);
        if (speedingUp) return target;

        // Exponential approach: frame-rate independent, and it never overshoots the target from either side.
        double k = BrakeSeconds <= 0 ? 1.0 : 1.0 - Math.Exp(-dt / BrakeSeconds);
        double next = velocity + (target - velocity) * k;
        // Land exactly on zero rather than trailing an asymptote forever — a craft with 1e-12 rad/s of
        // residual velocity still fails an "is it at rest" check.
        return Math.Abs(next - target) < 1e-6 ? target : next;
    }
}
