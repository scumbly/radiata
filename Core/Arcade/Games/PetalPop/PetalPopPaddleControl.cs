namespace ControllerWheel;

/// <summary>How the one stick drives the five paddles. Two strategies share this seam so the pause menu can
/// swap them with one assignment; both operate on the same paddle array and the same clamp.
///
/// <para>Neither is <c>ArcadeAim</c>: the paddles are not a rim-riding avatar. The stick is a screen
/// direction and every paddle slides toward it along its own rail, so pushing right moves every paddle right
/// and the six-o'clock mirroring question never arises. The paddle array's length is the side count; the
/// layout handed in must be the one for that count.</para></summary>
public interface IPetalPopPaddleControl
{
    /// <summary><paramref name="speedCap"/> is the most rail speed a paddle may carry this step, playfield
    /// units per second, or <see cref="double.PositiveInfinity"/> for the strategy's own top speed. A swing
    /// caps it to a crawl; both strategies have to honour it, since the pause menu can swap them mid-game and
    /// the cost of a swing cannot depend on which one is loaded.</summary>
    void Step(PetalPopLayout layout, PetalPopPaddle[] paddles, double stickX, double stickY, double dt,
              double speedCap);
}

public static class PetalPopControls
{
    public static IPetalPopPaddleControl For(int mode) => mode == 1 ? new PetalPopRailControl() : new PetalPopSpringControl();

    /// <summary>How far a paddle is asked along its rail for a stick vector <paramref name="d"/>: the projection
    /// onto the rail's chord, scaled so a push aimed at a corner saturates both adjacent paddles into it. A
    /// corner direction meets each adjacent chord at 90° − 180°/N, so the bare projection there is only
    /// sin(π/N) (0.71 on a square, 0.38 on an octagon) and two paddles never met; the gain 1/sin(π/N) closes
    /// that gap while a push at a side's midpoint still leaves that side's paddle centred.</summary>
    public static double RailTarget(PetalPopLayout L, Vec2 d, int side)
    {
        double gain = Math.Max(0.1, PetalPopTuning.CornerGain) / Math.Sin(Math.PI / L.Sides);
        return Math.Clamp(d.Dot(L.Side[side].Chord) * gain, -1, 1);
    }

    /// <summary>Hold a paddle to <paramref name="speedCap"/> playfield units per second, given where it stood
    /// before this step. Both strategies land here so a swing costs the same under either one.</summary>
    public static void Cap(PetalPopPaddle p, double from, double dt, double speedCap)
    {
        if (double.IsPositiveInfinity(speedCap) || dt <= 0) return;
        double cap = Math.Max(0, speedCap);
        double moved = p.Pos - from;
        double most = cap * dt;
        if (Math.Abs(moved) > most) p.Pos = from + Math.Sign(moved) * most;
        p.Vel = Math.Clamp(p.Vel, -cap, cap);
    }

    /// <summary>Deadzone-relieved stick vector, magnitude 0..1, then curved by <see cref="PetalPopTuning.StickExponent"/>
    /// so a partial tilt asks for less than its share of the travel while a full tilt still saturates.</summary>
    public static Vec2 Relieve(double sx, double sy)
    {
        double dz = Math.Clamp(PetalPopTuning.Deadzone, 0, 0.9);
        double mag = Math.Sqrt(sx * sx + sy * sy);
        if (mag <= dz || mag < 1e-9) return new(0, 0);
        double m = Math.Min(1, (mag - dz) / (1 - dz));
        m = Math.Pow(m, Math.Clamp(PetalPopTuning.StickExponent, 0.5, 4));
        double k = m / mag;
        return new(sx * k, sy * k);
    }
}

/// <summary>Absolute: the stick direction is where every paddle sits. Each paddle's target is the stick's
/// projection onto its rail, followed through a critically damped spring so intent is instant, arrival is
/// visible, and release brings all five home.</summary>
public sealed class PetalPopSpringControl : IPetalPopPaddleControl
{
    public void Step(PetalPopLayout L, PetalPopPaddle[] paddles, double sx, double sy, double dt,
                     double speedCap)
    {
        var d = PetalPopControls.Relieve(sx, sy);
        double omega = 2 * Math.PI * Math.Max(0.1, PetalPopTuning.SpringHz);
        double k = Math.Exp(-omega * dt);
        for (int i = 0; i < paddles.Length; i++)
        {
            var p = paddles[i];
            double target = PetalPopControls.RailTarget(L, d, i) * L.PosMax;
            double from = p.Pos;
            // Closed-form critically damped spring: exact at any fixed dt, never overshoots into instability.
            double delta = p.Pos - target;
            double b = p.Vel + omega * delta;
            p.Pos = target + (delta + b * dt) * k;
            p.Vel = (p.Vel - b * omega * dt) * k;
            // ⚠ The cap has to bite on the travel, not just on Vel: this spring is position-driven, so a
            // distant target moves the paddle a long way in one step no matter what Vel says afterwards.
            // Vel is clamped alongside it because it is also the rail english the next hit inherits.
            PetalPopControls.Cap(p, from, dt, speedCap);
            if (p.Pos > L.PosMax) { p.Pos = L.PosMax; p.Vel = 0; }
            else if (p.Pos < -L.PosMax) { p.Pos = -L.PosMax; p.Vel = 0; }
            if (Math.Abs(p.Pos - target) < 1e-6 && Math.Abs(p.Vel) < 1e-6) { p.Pos = target; p.Vel = 0; }
        }
    }
}

/// <summary>Relative: the stick drives each paddle's rail speed toward the direction pushed; releasing leaves
/// the paddles where they are.</summary>
public sealed class PetalPopRailControl : IPetalPopPaddleControl
{
    public void Step(PetalPopLayout L, PetalPopPaddle[] paddles, double sx, double sy, double dt,
                     double speedCap)
    {
        var d = PetalPopControls.Relieve(sx, sy);
        double blend = 1 - Math.Exp(-Math.Max(0.1, PetalPopTuning.RailAccelPerSec) * dt);
        double top = Math.Min(PetalPopTuning.RailSpeed, speedCap);
        for (int i = 0; i < paddles.Length; i++)
        {
            var p = paddles[i];
            double vTarget = PetalPopControls.RailTarget(L, d, i) * top;
            p.Vel += (vTarget - p.Vel) * blend;
            if (Math.Abs(p.Vel) < 1e-6) p.Vel = 0;
            double from = p.Pos;
            p.Pos += p.Vel * dt;
            // ⚠ Capping the target only asks the paddle to slow down; a paddle already at full speed when the
            // windup starts has to be held to the crawl outright, or the first frames of a swing are free
            // travel at the old speed.
            PetalPopControls.Cap(p, from, dt, speedCap);
            if (p.Pos > L.PosMax) { p.Pos = L.PosMax; p.Vel = 0; }
            else if (p.Pos < -L.PosMax) { p.Pos = -L.PosMax; p.Vel = 0; }
        }
    }
}
