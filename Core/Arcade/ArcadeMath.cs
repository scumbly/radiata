namespace ControllerWheel;

/// <summary>A 2-D point or vector in playfield units, shared by every arcade sim that needs one. Deliberately
/// its own tiny type: <c>Core</c> has no WPF <c>Point</c>, and a tuple has no arithmetic.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public double Dot(Vec2 o) => X * o.X + Y * o.Y;
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec2 Normalized() { double l = Length; return l > 1e-12 ? new(X / l, Y / l) : new(0, -1); }
    /// <summary>Rotated by <paramref name="radians"/>, clockwise on screen (y down).</summary>
    public Vec2 Rotated(double radians)
    {
        double c = Math.Cos(radians), s = Math.Sin(radians);
        return new(X * c - Y * s, X * s + Y * c);
    }
    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    /// <summary>Unit vector at a screen angle: 0 = 12 o'clock, increasing clockwise, y down.</summary>
    public static Vec2 FromScreenAngle(double a) => new(Math.Sin(a), -Math.Cos(a));
}

/// <summary>The scalar helpers every arcade sim (and, through its own thin wrappers, every renderer) reaches for.
/// One copy: four hand-typed copies of one formula is how a probe-proven sim drifts from its
/// neighbour.</summary>
public static class ArcadeMath
{
    /// <summary>Hermite ease, clamped to [0, 1].</summary>
    public static double Smoothstep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Move <paramref name="value"/> toward zero by <paramref name="amount"/>, landing exactly on it.</summary>
    public static double TowardZero(double value, double amount)
    {
        if (value == 0) return 0;
        return Math.Abs(value) <= amount ? 0 : value - Math.Sign(value) * amount;
    }

    /// <summary>An angle wrapped into [0, 2π).</summary>
    public static double NormTau(double angle)
    {
        angle %= Math.PI * 2;
        return angle < 0 ? angle + Math.PI * 2 : angle;
    }

    /// <summary>An angle wrapped into [-π, π).</summary>
    public static double WrapPi(double angle)
    {
        angle = (angle + Math.PI) % (Math.PI * 2);
        if (angle < 0) angle += Math.PI * 2;
        return angle - Math.PI;
    }

    public static bool Finite(double v) => double.IsFinite(v);
}

/// <summary>xorshift64* — the one generator every arcade sim seeds and carries in its snapshot, so "a seed means
/// the same thing" holds by construction rather than by four copies of three shift constants agreeing.</summary>
public static class ArcadeRng
{
    public static ulong Next(ref ulong state)
    {
        state ^= state >> 12; state ^= state << 25; state ^= state >> 27;
        return state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, 1) with 53 bits of mantissa.</summary>
    public static double NextDouble(ref ulong state) => (Next(ref state) >> 11) / (double)(1UL << 53);
}
