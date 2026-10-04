namespace ControllerWheel;

/// <summary>Four-way navigation whose repeats depend on elapsed time, not controller packet frequency.</summary>
public sealed class StickNavigationRepeat
{
    private int _x, _y;
    private long _next;
    public bool Engaged => _x != 0 || _y != 0;
    public void Reset() { _x = _y = 0; _next = 0; }

    public (int X, int Y)? SetStick(float x, float y, long now)
    {
        int dx = 0, dy = 0;
        if (float.IsFinite(x) && float.IsFinite(y) && x * x + y * y >= 0.25f)
        {
            if (MathF.Abs(x) >= MathF.Abs(y)) dx = x > 0 ? 1 : -1;
            else dy = y > 0 ? 1 : -1;
        }
        if (dx == 0 && dy == 0) { Reset(); return null; }
        if (dx == _x && dy == _y) return null;
        _x = dx; _y = dy; _next = now + 350;
        return (dx, dy);
    }

    public (int X, int Y)? Tick(long now)
    {
        if (!Engaged || now < _next) return null;
        _next = now + 120; // No burst of catch-up moves after a stalled UI thread.
        return (_x, _y);
    }
}
