using System.Numerics;

namespace ControllerWheel;

/// <summary>Preserve the existing first-readable-slot policy while excluding Radiata's own output.
/// A selected slot may belong to physical hardware or another tool; this does not prove PnP identity.</summary>
public static class XInputSourceSelection
{
    public static bool IsOwnOutputAmbiguous(int connectedMask, int? ownSlot, bool ownVirtualActive) =>
        ownVirtualActive && connectedMask != 0 &&
        (ownSlot is not (>= 0 and <= 3) || (connectedMask & (1 << ownSlot.Value)) == 0);

    public static int Select(int connectedMask, int? ownSlot, bool ownVirtualActive)
    {
        if ((connectedMask & ~15) != 0) return -1;
        if (ownVirtualActive)
        {
            if (ownSlot is not (>= 0 and <= 3) || (connectedMask & (1 << ownSlot.Value)) == 0) return -1;
            connectedMask &= ~(1 << ownSlot.Value);
        }
        return connectedMask != 0
            ? BitOperations.TrailingZeroCount((uint)connectedMask) : -1;
    }
}
