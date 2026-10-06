namespace ControllerWheel;

/// <summary>What one finished Bluetooth isolation observation may write. A count of zero proves
/// isolation only for a pad that was present and still cloaked across the probe: an absent pad reads
/// zero too, and a verdict earned against nothing would be honoured when the same ids reconnect.
/// Only the still-pending observation for the current id set may write — the deadline's Refused, a
/// re-arm or a changed id set all outrank a late result.</summary>
public static class BtObservationGate
{
    public enum Outcome
    {
        /// <summary>Another observation or the deadline owns the verdict: write nothing.</summary>
        Discard,
        /// <summary>The result describes no present, cloaked pad: back to None so the next pass re-observes.</summary>
        Reset,
        Verified,
        /// <summary>Pads still visible, or the probe could not observe (null) — fail closed.</summary>
        Refused,
    }

    public static Outcome Resolve(bool superseded, bool stillPending, bool idSetChanged,
                                  bool padPresentThroughout, bool idsStillCloaked, int? count)
    {
        if (superseded || !stillPending) return Outcome.Discard;
        if (idSetChanged || !padPresentThroughout || !idsStillCloaked) return Outcome.Reset;
        return count == 0 ? Outcome.Verified : Outcome.Refused;
    }
}
