namespace ControllerWheel;

/// <summary>The runner's motion in the pipe's own frame, as pure functions. <see cref="Integrate"/> is called
/// by <see cref="Internode.Step"/> and by <see cref="InternodeReachability"/>: one integrator, so what the oracle
/// proves is what the game does. ⚠ Never give the oracle its own copy of any constant here.
///
/// <para>Torque model, not rate model: the stick applies angular acceleration against a pendulum pull, so
/// <c>ArcadeAim</c> is deliberately not used. The pull is gated by wall grip (<see cref="GripPull"/>): a
/// released runner keeps the wall for <see cref="InternodeTuning.GripSeconds"/> before sliding. Bends and twists
/// have no physics effect; a future tilt term belongs in the grounded α below, never in the renderer.</para>
///
/// <para>The rim is a parameter of <see cref="Integrate"/>, not a constant: the course's rim profile widens and
/// closes the opening along a section and the game passes <c>Internode.RimAt(0)</c>. The oracle passes the base
/// <see cref="RimRad"/>, which is valid because the sequencer only places a rim bump over events that stay
/// over phrases marked rim-safe (each proven with flight forbidden, so it never needs the rim). At a rim of
/// π there is no rim: the runner is grounded all the way round and θ simply wraps.</para></summary>
public static class InternodePhysics
{
    public const double Deg = Math.PI / 180;

    /// <summary>The base rim, radians; the value the profile rests at and the gate is approached at.</summary>
    public static double RimRad => InternodeTuning.RimDeg * Deg;

    /// <summary>A rim this close to π is a closed tube.</summary>
    private const double ClosedEpsilon = 1e-9;

    /// <summary>Normalise to (−π, π].</summary>
    public static double Wrap(double a)
    {
        a = Math.IEEERemainder(a, 2 * Math.PI);
        if (a <= -Math.PI) a += 2 * Math.PI;
        else if (a > Math.PI) a -= 2 * Math.PI;
        return a;
    }

    /// <summary>Signed shortest angle from <paramref name="b"/> to <paramref name="a"/>.</summary>
    public static double AngleDiff(double a, double b) => Wrap(a - b);

    /// <summary>Stick x → steer in [−1, 1], deadzone removed and the remainder rescaled to full travel.</summary>
    public static double Steer(double stickX)
    {
        if (!double.IsFinite(stickX)) return 0;
        double dz = Math.Clamp(InternodeTuning.Deadzone, 0, 0.99);
        double u = (Math.Abs(stickX) - dz) / (1 - dz);
        return Math.Sign(stickX) * Math.Clamp(u, 0, 1);
    }

    /// <summary>One semi-implicit Euler step. <paramref name="steer"/> ∈ [−1, 1]; <paramref name="jumpPressed"/>
    /// is the Cross edge this step and <paramref name="jumpHeld"/> the button's level (releasing while still rising
    /// clips the climb to <see cref="InternodeTuning.JumpTapFraction"/>, which is what makes a tap a small hop). A
    /// press from the ground is the first jump; one press in the air is the second (see the height shares in
    /// <see cref="InternodeTuning"/>), and only after that is an air press buffered for the landing;
    /// <paramref name="rimRad"/> is the rim at the runner's z (see the class remark).
    /// Damping is <c>D + ReleaseDamping·(1 − |steer|)</c>: a released stick stops coasting, a held one keeps
    /// the crossover. Wall grip (grounded only — a jump hands the pull straight back): a held stick
    /// (|steer| ≥ <see cref="InternodeTuning.GripHoldThreshold"/>) banks
    /// <see cref="InternodeTuning.GripSeconds"/> and feels the full pendulum pull; released, the grip runs down and
    /// the pull is scaled by <see cref="GripPull"/>, zero until the last <see cref="InternodeTuning.GripFadeSeconds"/>.
    /// Grounded and airborne alike; a jump never resets it.</summary>
    public static InternodeRunnerState Integrate(in InternodeRunnerState s, double steer, bool jumpPressed, bool jumpHeld, double dt, double rimRad, out InternodeStepFlags flags)
        => Integrate(s, steer, jumpPressed, jumpHeld, dt, rimRad, true, out flags);

    /// <summary><paramref name="floorHere"/> is false over a break in the floor: the runner cannot stand there and
    /// cannot land there, so a grounded one steps off into the hole and an airborne one keeps sinking with a negative
    /// height. Sinking is ordinary flight — steerable, and still owed the second press if it is unspent — so a runner
    /// who drops in can launch back out. <see cref="Internode"/> decides when a sink becomes a fall.</summary>
    public static InternodeRunnerState Integrate(in InternodeRunnerState s, double steer, bool jumpPressed, bool jumpHeld, double dt, double rimRad, bool floorHere, out InternodeStepFlags flags)
    {
        flags = InternodeStepFlags.None;
        var r = s;
        double G = InternodeTuning.PendulumGravityDegPerSec2 * Deg;
        double T = InternodeTuning.SteerTorqueDegPerSec2 * Deg;
        double wMax = InternodeTuning.MaxAngularSpeedDeg * Deg;
        steer = Math.Clamp(steer, -1, 1);
        // Release damping is the drag of the shoes on the surface, so it acts only on a grounded runner: it kills
        // coasting after a let-go without braking a jump, which falls under the pull alone.
        double D = InternodeTuning.AngularDampingPerSec;
        if (s.Air == InternodeAir.Grounded)
        {
            D += Math.Max(0, InternodeTuning.ReleaseDampingPerSec) * (1 - Math.Abs(steer));
            // A reversal bites sooner: extra drag only while the stick opposes the swing.
            if (steer != 0 && s.Omega != 0 && Math.Sign(steer) != Math.Sign(s.Omega))
                D += Math.Max(0, InternodeTuning.CounterSteerDampingPerSec) * Math.Abs(steer);
        }
        double rim = Math.Clamp(rimRad, 0, Math.PI);
        bool closed = rim >= Math.PI - ClosedEpsilon;

        // Grip is sticky shoes on the wall, not a suspension of gravity: it only holds a grounded runner who has
        // let go. Leaving the surface hands the pull back at once, so a leap from either wall falls to the bottom.
        if (Math.Abs(steer) >= InternodeTuning.GripHoldThreshold) r.GripLeft = Math.Max(0, InternodeTuning.GripSeconds);
        else
        {
            r.GripLeft = Math.Max(0, r.GripLeft - dt);
            if (r.Air == InternodeAir.Grounded) G *= GripPull(r.GripLeft);
        }

        r.JumpBuffer = Math.Max(0, r.JumpBuffer - dt);
        if (r.Air == InternodeAir.Jumping) r.CoyoteLeft = Math.Max(0, r.CoyoteLeft - dt);
        if (jumpPressed)
        {
            // One jump per flight. A runner who stepped off a lip without jumping still has it, for CoyoteSeconds
            // (the launch then fires from wherever they have sunk to); otherwise a press in the air is the landing buffer.
            if (r.Air == InternodeAir.Grounded || (r.Air == InternodeAir.Jumping && !r.Launched && r.CoyoteLeft > 0)) Launch(ref r, ref flags);
            else if (r.Air == InternodeAir.Jumping) r.JumpBuffer = Math.Max(0, InternodeTuning.JumpBufferSeconds);
        }

        switch (r.Air)
        {
            case InternodeAir.Grounded:
            {
                double alpha = -G * Math.Sin(r.Theta) + T * steer - D * r.Omega;
                r.Omega = Math.Clamp(r.Omega + alpha * dt, -wMax, wMax);
                r.Theta = Wrap(r.Theta + r.Omega * dt);
                // Nothing under the shoes: off the lip of a break at once, at rest vertically, with the jump still in
                // hand for CoyoteSeconds. It is a sink, not yet a fall. ⚠ Tested before the rim: a runner over a
                // break cannot push off it to leave over the rim. With the rim tested first, a fast runner landing
                // on the wall over a hole left the surface again on the next step and could grind the lip of any
                // chasm for as long as it lasted, floor or no floor — the oracle found that route through a 3 s hole.
                if (!floorHere) { r.Air = InternodeAir.Jumping; r.Vh = 0; r.Launched = false; r.CoyoteLeft = InternodeTuning.CoyoteSeconds; }
                else if (!closed) RimCheck(ref r, rim, ref flags);
                break;
            }
            case InternodeAir.Jumping:
            {
                // The homing term is linear in |θ|, not in sin θ: the pendulum's own pull fades above 90°, which is
                // what let a jump high on a wall drift back to the same place. Angular only — height is untouched.
                // A hand on the stick buys off most of the homing and its damping: the pull is there to stop passive
                // drift, not to veto a commitment. At |u| = 0 nothing is relieved, so the hands-off settle is
                // exactly as tuned.
                //
                // The relief fades as the runner descends, and its end point is derived rather than tuned: at the
                // bottom of the arc the remaining pull exactly cancels a full stick, so a commitment buys travel
                // early and nothing late. A tap descends from a lower apex, so it reaches proportionally less of
                // the fade — a short hop is a small commitment.
                double u = Math.Abs(steer);
                double homingFull = InternodeTuning.AirHomingDegPerSec2 * Deg * (r.Theta / Math.PI);
                double fall = Math.Clamp(-r.Vh / Math.Max(1e-9, InternodeTuning.JumpImpulse), 0, 1);
                double pull = G * Math.Sin(r.Theta) * InternodeTuning.AirPendulumFraction;
                double balance = Math.Abs(homingFull) > 1e-9
                    ? Math.Clamp((T * InternodeTuning.AirSteerFraction - Math.Abs(pull)) / Math.Abs(homingFull), 0, 1)
                    : 1;
                double reliefLaunch = 1 - Math.Clamp(InternodeTuning.AirControlRelief, 0, 1) * u;
                double reliefLand = 1 - (1 - balance) * u;
                double relief = reliefLaunch + (reliefLand - reliefLaunch) * fall;
                double homing = homingFull * relief;
                // The homing pull is a spring, so the air carries its own damping: undamped it converts the wall's
                // height into speed and throws the runner past the bottom to the far side. See AirDampingPerSec.
                double airD = Math.Max(0, InternodeTuning.AirDampingPerSec) * relief;
                double alpha = -G * Math.Sin(r.Theta) * InternodeTuning.AirPendulumFraction - homing
                               + T * steer * InternodeTuning.AirSteerFraction - airD * r.Omega;
                r.Omega = Math.Clamp(r.Omega + alpha * dt, -wMax, wMax);
                r.Theta = Wrap(r.Theta + r.Omega * dt);
                if (!closed) RimCheck(ref r, rim, ref flags);
                if (r.Air != InternodeAir.Jumping) break;
                // A tap is a short hop: letting go while still rising clips the remaining climb.
                double cut = InternodeTuning.JumpImpulse * Math.Clamp(InternodeTuning.JumpTapFraction, 0, 1);
                if (!jumpHeld && r.Vh > cut) r.Vh = cut;
                // Trapezoid velocity: exact under constant gravity, so the apex is the derived JumpApex rather
                // than a step-size-dependent shortfall.
                double vh0 = r.Vh;
                r.Vh -= InternodeTuning.JumpGravity * dt;
                r.H += 0.5 * (vh0 + r.Vh) * dt;
                // Landing needs floor to land ON, and a runner rising back out of a break passes h = 0 going up
                // rather than arriving: only a descent onto floor is a landing.
                if (r.H <= 0 && floorHere && r.Vh <= 0)
                {
                    r.H = 0; r.Vh = 0; r.Air = InternodeAir.Grounded; r.Launched = false; r.CoyoteLeft = 0;
                    flags |= InternodeStepFlags.Land;
                    if (r.JumpBuffer > 0) Launch(ref r, ref flags);
                }
                break;
            }
            case InternodeAir.OverTop:
            {
                // Across the open top the runner is a free particle in the pipe's cross-section under gravity
                // toward the floor, with the ring as a one-sided wall: it cannot be passed outward (the top is
                // rimless, not doorless), inward is free. So a runner who barely clears the rim at the top falls
                // straight through the middle to the floor, while one with speed presses against the ring and
                // slides over it to the far wall — the crossing threshold is emergent, not a constant. Where the
                // ring is surface, touching it is landing. The stick gives a small sideways push and nothing more.
                double g = Math.Max(0, InternodeTuning.FlightGravity);
                double push = T * Math.Clamp(InternodeTuning.GapSteerFraction, 0, 1) * steer;
                double radius = Math.Clamp(1 - r.H, 0, 1);
                double px = radius * Math.Sin(r.Theta), py = -radius * Math.Cos(r.Theta);
                r.FlyVx += push * dt;
                r.FlyVy -= g * dt;
                px += r.FlyVx * dt;
                py += r.FlyVy * dt;
                double len = Math.Sqrt(px * px + py * py);
                double theta = len > 1e-9 ? Math.Atan2(px, -py) : r.Theta;
                if (len >= 1)
                {
                    double nx = px / len, ny = py / len;
                    double tx = Math.Cos(theta), ty = Math.Sin(theta);
                    if (Math.Abs(theta) <= rim && !floorHere)
                    {
                        // The ring where the floor is missing: the flyer goes through it. ⚠ Without this a flight
                        // landed on the wall over a hole, sank with the jump in hand, pressed, steered back out
                        // over the rim and flew again — a loop that crossed a 3 s chasm in the oracle and would
                        // in play. A fall from flight has no coyote window: nothing was stepped off.
                        r.Theta = Wrap(theta);
                        r.H = 0; r.Vh = 0; r.FlyVx = 0; r.FlyVy = 0; r.Launched = true; r.CoyoteLeft = 0;
                        r.Air = InternodeAir.Jumping;
                        break;
                    }
                    if (Math.Abs(theta) <= rim)
                    {
                        // Landing: the tangential part of the flight becomes ω, the rest is absorbed.
                        double omega = (r.FlyVx * tx + r.FlyVy * ty) * InternodeTuning.OverTopLandRetain;
                        r.Theta = theta;
                        r.Omega = Math.Clamp(omega, -wMax, wMax);
                        r.H = 0; r.Vh = 0; r.FlyVx = 0; r.FlyVy = 0; r.Launched = false; r.CoyoteLeft = 0;
                        r.Air = InternodeAir.Grounded;
                        flags |= InternodeStepFlags.OverTopLand | InternodeStepFlags.Land;
                        break;
                    }
                    // Against the ring where there is no surface: slide along it, shedding the outward speed.
                    double outward = r.FlyVx * nx + r.FlyVy * ny;
                    if (outward > 0) { r.FlyVx -= outward * nx; r.FlyVy -= outward * ny; }
                    len = 1;
                }
                r.Theta = Wrap(theta);
                r.H = 1 - len;
                // ω stays the angular rate of the position so the lean, the trail and the oracle's bucket read it.
                r.Omega = len > 1e-6
                    ? Math.Clamp((r.FlyVx * Math.Cos(theta) + r.FlyVy * Math.Sin(theta)) / len, -wMax, wMax)
                    : 0;
                break;
            }
        }
        return r;
    }

    /// <summary>Share of the pendulum pull that acts on a released stick with <paramref name="gripLeft"/> seconds
    /// of grip banked: 0 while more than <see cref="InternodeTuning.GripFadeSeconds"/> remain, smoothstep up to 1 as
    /// it runs out. A held stick never consults this.</summary>
    public static double GripPull(double gripLeft)
    {
        double fade = InternodeTuning.GripFadeSeconds;
        if (gripLeft <= 0) return 1;
        if (fade <= 0) return 0;
        double u = Math.Clamp(gripLeft / fade, 0, 1);
        return 1 - u * u * (3 - 2 * u);
    }

    private static void Launch(ref InternodeRunnerState r, ref InternodeStepFlags flags)
    {
        r.Air = InternodeAir.Jumping;
        r.Vh = InternodeTuning.JumpImpulse;
        r.Launched = true;
        r.CoyoteLeft = 0;
        r.JumpBuffer = 0;
        r.GripLeft = 0;
        flags |= InternodeStepFlags.Jump;
    }

    /// <summary>Leaving the surface is unconditional: run or jump off the lip at any speed and you are off it, with
    /// no threshold and no bounce. The bead's angular motion (and a jump's radial one) becomes a free velocity in
    /// the cross-section; from there <see cref="InternodeTuning.FlightGravity"/> decides whether the runner reaches the
    /// far wall or falls to the floor. A rim closing under a runner puts them in the air the same way.</summary>
    private static void RimCheck(ref InternodeRunnerState r, double rim, ref InternodeStepFlags flags)
    {
        if (Math.Abs(r.Theta) <= rim) return;
        double radius = Math.Clamp(1 - r.H, 0, 1);
        double tx = Math.Cos(r.Theta), ty = Math.Sin(r.Theta);
        double nx = Math.Sin(r.Theta), ny = -Math.Cos(r.Theta);
        // Tangential speed from ω at the runner's radius; a jump's rise is a speed toward the centre.
        r.FlyVx = r.Omega * radius * tx - r.Vh * nx;
        r.FlyVy = r.Omega * radius * ty - r.Vh * ny;
        r.Vh = 0;
        r.JumpBuffer = 0;
        r.Air = InternodeAir.OverTop;
        flags |= InternodeStepFlags.RimExit;
    }

    /// <summary>The event-plane test the game and the oracle share. <paramref name="marginRad"/> shrinks a
    /// token's window and grows a mine's (the oracle's safety margin); the game passes 0.</summary>
    public static bool Collides(InternodeEventKind kind, double eventTheta, double eventHeight, in InternodeRunnerState s, double marginRad = 0)
    {
        double d = Math.Abs(AngleDiff(s.Theta, eventTheta));
        // Across the open top there is no surface, so height means nothing: passing an orb's angle as you fly is
        // what collects it. This is what lets a formation lay orbs in the gap.
        if (s.Air == InternodeAir.OverTop)
            return kind != InternodeEventKind.Mine && d <= InternodeTuning.TokenHalfWidthDeg * Deg - marginRad;
        switch (kind)
        {
            case InternodeEventKind.Token:
                return d <= InternodeTuning.TokenHalfWidthDeg * Deg - marginRad && s.H <= InternodeTuning.GroundGrabHeight;
            case InternodeEventKind.ElevatedToken:
                // Off the ground at all and on the line is a catch: the authored height is where the
                // orb is drawn and where its tether reaches, not a band the runner has to hit. Above the
                // ground-grab height so a runner on the floor cannot take it, which is the whole of what makes
                // it a lifted orb.
                return d <= InternodeTuning.TokenHalfWidthDeg * Deg - marginRad && s.H > InternodeTuning.GroundGrabHeight;
            case InternodeEventKind.Mine:
                if (d > InternodeTuning.MineHalfWidthDeg * Deg + marginRad) return false;
                // A mine on the floor is cleared by height alone. One hung above the floor is a band: the runner
                // passes under it on the ground or over it near the apex, and a hop that puts them level with it
                // is the hit, which is what a row of them stacked over a ground row is for.
                return eventHeight <= 0
                    ? s.H <= InternodeTuning.MineClearHeight
                    : Math.Abs(s.H - eventHeight) <= InternodeTuning.MineBandHalf;
        }
        return false;
    }
}
