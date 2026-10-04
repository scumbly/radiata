using System.Numerics;

namespace ControllerWheel;

/// <summary>Collect the complete connect window; an early single arrival is not an identity proof.</summary>
public sealed class XInputSlotObservation
{
    private readonly int _before;
    private int _appeared;
    private int _last;
    private bool _changedUnexpectedly;

    public XInputSlotObservation(int before) { _before = before; _last = before; }

    public void Observe(int mask)
    {
        if ((mask & ~15) != 0 || (mask & _before) != _before || (_appeared & mask) != _appeared)
            _changedUnexpectedly = true;
        _appeared |= mask & ~_before;
        _last = mask;
    }

    public int? Resolve()
    {
        if (_changedUnexpectedly || BitOperations.PopCount((uint)_appeared) != 1
            || (_last & _appeared) != _appeared)
            return null;
        return BitOperations.TrailingZeroCount((uint)_appeared);
    }
}
