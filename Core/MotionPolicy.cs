namespace ControllerWheel;

/// <summary>The product-wide Reduce Motion policy.
/// One shared answer to "may decorative motion run right now?", resolved from the saved Radiata setting or
/// the Windows "show animations" preference — either source reduces. Every surface that creates animation
/// asks this instead of carrying its own check; new motion must consult it and be listed in
/// docs/MOTION-INVENTORY.md.
/// <para>Lives in Core (WPF-free) so <see cref="WheelStateMachine"/> and unit tests can read it. App owns
/// the two inputs: <see cref="UserSetting"/> from <c>SystemConfig.ReduceMotion</c>,
/// <see cref="SystemPrefersReduced"/> from the SPI_GETCLIENTAREAANIMATION probe. Consumers that cache the
/// value (or hold running animations) must also subscribe to <see cref="Changed"/> so a live flip stops
/// loops that are already running.</para>
/// <para>Essential state stays (statically), helpful transitions become instant or opacity-only,
/// decorative motion stops entirely. <see cref="TransitionMs"/> is for movement/scale/travel only —
/// opacity-only fades keep their duration and must not be run through it.</para></summary>
public static class MotionPolicy
{
    private static bool _user;
    private static bool _system;

    /// <summary>The saved Radiata setting (<c>SystemConfig.ReduceMotion</c>). Setting either input
    /// raises <see cref="Changed"/> when the effective answer flips.</summary>
    public static bool UserSetting
    {
        get => _user;
        set => Update(ref _user, value);
    }

    /// <summary>The OS-level signal: Windows' "Show animations in Windows" is off. Never overrides
    /// <see cref="UserSetting"/> in either direction — either source alone reduces.</summary>
    public static bool SystemPrefersReduced
    {
        get => _system;
        set => Update(ref _system, value);
    }

    /// <summary>The effective policy: decorative motion must stop and transitions collapse when true.</summary>
    public static bool Reduce => _user || _system;

    /// <summary>Raised (on whatever thread set the input) when <see cref="Reduce"/> flips. Surfaces with
    /// running loops (storyboards, effect timers) stop them here rather than waiting for the next open.</summary>
    public static event Action? Changed;

    /// <summary>Duration for a movement/scale transition: unchanged normally, 0 (instant) under Reduce
    /// Motion. Opacity-only fades are allowed under the policy and should keep their own duration.</summary>
    public static int TransitionMs(int ms) => Reduce ? 0 : ms;

    private static void Update(ref bool field, bool value)
    {
        if (field == value) return;
        bool before = Reduce;
        field = value;
        if (Reduce != before) Changed?.Invoke();
    }
}
