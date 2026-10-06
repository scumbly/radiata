namespace ControllerWheel;

/// <summary>
/// The Arcade feature gate, and the copy for the "we can't play here" guard card.
///
/// <para><b>⚠ Never declare a <c>ControllerWheel.Arcade</c> namespace.</b> This class occupies that name
/// inside <c>ControllerWheel</c>, so a file under <c>Core\Arcade\</c> or <c>Arcade\</c> that relies on
/// folder-derived namespaces would collide. Every arcade file declares <c>namespace ControllerWheel;</c>
/// explicitly, matching the rest of the repo.</para>
/// </summary>
public static class Arcade
{
    /// <summary>
    /// Arcade ships in every release. No Arcade-off build exists — don't plan work around one, and
    /// don't describe this as a kill switch.
    ///
    /// <para>The const stays as the compile-time seam the availability gates read, which is what keeps the
    /// no-offer/no-rewrite contract (docs/ACTIONS.md) testable through
    /// <see cref="ConcealedForHarness"/>: an arcade slice must load, keep its type through an edit +
    /// auto-save, and no-op when fired even where the feature isn't offered. It is deliberately not a
    /// config key — a key would appear in every shipped <c>config.json</c> and backup.</para>
    /// </summary>
    public const bool Enabled = true;

    /// <summary>Harness-only concealment. Nothing in the app ever sets this — <c>TestHarness.exe arcade</c>
    /// flips it to exercise the unavailable posture (which a const can't simulate at runtime) and to prove
    /// the availability-keyed caches (<c>WheelEditorControl.Categories</c>, <c>HelpContent.Topics</c>)
    /// rebuild rather than latch. Those caches carry the no-offer/no-rewrite contract that keeps an arcade
    /// slice loadable wherever the feature isn't offered (docs/ARCADE.md).</summary>
    public static bool ConcealedForHarness { get; set; }

    /// <summary>Is Arcade visible and usable right now? Everything gates on this, never on
    /// <see cref="Enabled"/> alone — the harness conceals through it.</summary>
    public static bool Available => Enabled && !ConcealedForHarness;

    // ── The bleed-through guard ───────────────────────────────────────────────

    /// <summary>Why the arcade won't run right now. Derived from App's existing isolation bookkeeping
    /// (<c>_isolationNote</c>, set by <c>UpdateInputCaptureCore</c>) rather than re-deriving capture state.</summary>
    public enum Block
    {
        /// <summary>Isolated (or nothing to bleed into) — play.</summary>
        None,
        SafeMode,
        NoVirtualPad,
        CloakFailed,
        MultiplePads,
        /// <summary>The pad count kept flapping and the churn breaker paused capture.</summary>
        PadCountUnstable,
        Unknown,
    }

    /// <summary>Map App's tray-tooltip isolation note to a guard reason. The note strings are authored in
    /// <c>App.UpdateInputCaptureCore</c>'s <c>NoteIsolationState</c> call as <c>UiText.IsolationNote</c> consts.</summary>
    public static Block ReasonFromNote(string? note) => note switch
    {
        null or ""                        => Block.Unknown,
        UiText.IsolationNote.PassthruNoCloak            => Block.SafeMode,
        UiText.IsolationNote.NoVirtualPad   => Block.NoVirtualPad,
        UiText.IsolationNote.CloakFailed     => Block.CloakFailed,
        UiText.IsolationNote.MultiplePads    => Block.MultiplePads,
        UiText.IsolationNote.PadCountUnstable => Block.PadCountUnstable,
        // "Xbox fallback" is the multi-cause remainder (BT best-effort, capture off, probe unavailable)
        // — it must not map to MultiplePads: that card told a single-pad user to unplug a second pad.
        UiText.IsolationNote.XboxFallback    => Block.Unknown,
        _                                 => Block.Unknown,
    };

    /// <summary>What the guard card says: a heading, why, what the user can do — and whether the △-hold
    /// override is offered at all. Passthru Mode says no: it is a deliberate user choice and the card's own copy
    /// calls staying put "the safe choice", so it must not offer a bypass in the same breath. The override
    /// remains for the involuntary reasons (no drivers, cloak failed, too many pads), where "I know it's
    /// fine here" is a legitimate answer.</summary>
    public sealed record GuardCopy(string Title, string Body, string Fix, bool Overridable = true);

    /// <summary>Copy for the guard card. <paramref name="gameName"/> is the foreground game, when known —
    /// naming it is what makes the card read as an explanation rather than an error.</summary>
    public static GuardCopy Guard(Block reason, string? gameName)
    {
        string game = string.IsNullOrWhiteSpace(gameName) ? Loc.T(UiText.Arcade.TheGameYoureIn) : gameName!;
        return reason switch
        {
            // Passthru Mode is a feature working correctly — it exists so an anticheat never sees a virtual pad.
            // Deliberately no nudge to turn it off (trading that away for a minigame is the wrong advice), and no △ override (see GuardCopy).
            Block.SafeMode => new GuardCopy(
                Loc.T(UiText.Arcade.GuardPassthruTitle),
                Loc.F(UiText.Arcade.GuardPassthruBody, game),
                Loc.F(UiText.Arcade.GuardPassthruFix, game),
                Overridable: false),

            Block.NoVirtualPad => new GuardCopy(
                Loc.T(UiText.Arcade.GuardNotIsolatedTitle),
                Loc.F(UiText.Arcade.GuardNoPadBody, game),
                Loc.T(UiText.Arcade.GuardNoPadFix)),

            Block.CloakFailed => new GuardCopy(
                Loc.T(UiText.Arcade.GuardNotIsolatedTitle),
                Loc.F(UiText.Arcade.GuardCloakBody, game),
                Loc.T(UiText.Arcade.GuardCloakFix)),

            Block.MultiplePads => new GuardCopy(
                Loc.T(UiText.Arcade.GuardManyTitle),
                Loc.F(UiText.Arcade.GuardManyBody, game),
                Loc.T(UiText.Arcade.GuardManyFix)),

            Block.PadCountUnstable => new GuardCopy(
                Loc.T(UiText.Arcade.GuardNotIsolatedTitle),
                Loc.F(UiText.Arcade.GuardUnstableBody, game),
                Loc.T(UiText.Arcade.GuardUnstableFix)),

            _ => new GuardCopy(
                Loc.T(UiText.Arcade.GuardNotIsolatedTitle),
                Loc.F(UiText.Arcade.GuardUnknownBody, game),
                Loc.T(UiText.Arcade.GuardUnknownFix)),
        };
    }

    /// <summary>Hold time on △ to override the guard for the rest of the session. Deliberately a hold, not
    /// a tap: the override is a real decision, and the card is the one place the arcade asks for one.</summary>
    public const int OverrideHoldMs = 1000;
}
