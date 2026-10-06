namespace ControllerWheel;

/// <summary>Development switches for the arcade. Compiled out of a public release through
/// <see cref="ReleaseGates.PublicRelease"/> (the one place that reads <c>PUBLIC_RELEASE</c>); a dev build or any
/// build made without the switch keeps them. Nothing in this class may be referenced by a save format, a probe's pass condition or a
/// help string.</summary>
public static class ArcadeDebug
{
    /// <summary>Select (View / Create) steps the live game forward one level, clamped at the last. Read by Internode,
    /// Petalpop and Kabloom in their <c>Step</c>; the host forwards the button only while a game is open.</summary>
    public const bool LevelSkip = !ReleaseGates.PublicRelease;

    /// <summary>Internode's phrase tester pause-menu row: the run becomes the authored bank in difficulty order,
    /// each phrase three times to a free gate, for playability checks. Never persisted, never in a public build.</summary>
    public const bool PhraseTester = !ReleaseGates.PublicRelease;
}
