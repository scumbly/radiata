namespace ControllerWheel;

/// <summary>Capture uses the last connected family through a transient loss. A reader's
/// disconnected default is not evidence that a different controller has been selected.
/// This tracks controller family only; it does not establish XInput slot or PnP identity.</summary>
public sealed class ControllerCaptureContinuity
{
    private ControllerKind? _lastConnected;

    public void ObserveConnected(ControllerKind kind) => _lastConnected = kind;

    public ControllerKind ResolveKind(ControllerKind fallback) => _lastConnected ?? fallback;

    /// <summary>Preserve only an existing, neutral Xbox target during the bounded absence grace.
    /// Never create an absent pad, extend the grace, or bypass an unverified owned cloak.</summary>
    public bool RetainNeutralXboxTarget(bool sourceConnected, bool graceActive,
        bool xboxTargetActive, bool ownedCloakVerified) =>
        _lastConnected == ControllerKind.Xbox && !sourceConnected && graceActive
        && xboxTargetActive && ownedCloakVerified;

    /// <summary>Upper bound on <see cref="HoldNeutralForIdentity"/>. XInput can read a pad that has just
    /// switched transport before PnP lists its devnode: 1.7 s apart on a Bluetooth fallback (measured on
    /// Windows 10, Debug build, Xbox Series pad).</summary>
    public const long IdentityHoldMs = 3_000;

    /// <summary>Keep an existing Xbox target up, NEUTRAL (the caller suppresses it), while a connected
    /// source's devnode has not enumerated yet, instead of removing the game's pad and re-adding it a
    /// moment later. The source is unidentified for the whole hold, so nothing it sends is forwarded.
    /// Never creates a target, never outlasts <see cref="IdentityHoldMs"/>, and needs the owned cloak
    /// still verified.</summary>
    public bool HoldNeutralForIdentity(bool sourceConnected, bool xboxTargetActive,
        bool ownedCloakVerified, long heldMs) =>
        _lastConnected == ControllerKind.Xbox && sourceConnected && xboxTargetActive
        && ownedCloakVerified && heldMs >= 0 && heldMs < IdentityHoldMs;

    /// <summary>How long two Xbox-class pads must stay listed before the multi-pad guard un-cloaks. A USB↔Bluetooth
    /// switch lists both transports of ONE pad for up to ~3 s (measured on Windows 10, Xbox Series pad, Bluetooth→USB);
    /// PnP gives no identity that links them (ContainerId differs by transport).</summary>
    public const long MultiPadConfirmMs = 5_000;

    public enum MultiPadHold { None, Forward, Neutral }

    /// <summary>While a second Xbox-class pad has been listed for less than <see cref="MultiPadConfirmMs"/>, keep
    /// an established capture as it is instead of un-cloaking: the listing is usually one pad mid-switch.
    /// Forward only while the reader is still on the source it had when the second pad appeared: a source that
    /// reconnected since may be the new, un-cloaked transport, so its input is held neutral. Nothing to hold
    /// without an active Xbox target over a still-verified owned cloak.</summary>
    public static MultiPadHold HoldForMultiPad(bool xboxTargetActive, bool ownedCloakVerified, long heldMs,
        bool sourceConnected, bool sourceUnchanged) =>
        !xboxTargetActive || !ownedCloakVerified || heldMs < 0 || heldMs >= MultiPadConfirmMs ? MultiPadHold.None
        : sourceConnected && sourceUnchanged ? MultiPadHold.Forward
        : MultiPadHold.Neutral;
}
