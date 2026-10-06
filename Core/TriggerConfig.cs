namespace ControllerWheel;

/// <summary>The kind of physical controller currently driving the app — a FUNCTIONAL grouping (trigger
/// set + glyphs), never a verified identity. All are read on hardware: <see cref="DualSenseEdge"/> (raw
/// HID, Fn triggers), <see cref="PlayStationOther"/> (raw HID — DualSense, DualShock 4, or a third-party
/// pad in a DS4-compatible mode), <see cref="Xbox"/> (XInput), and <see cref="ExtraButtonPad"/> (raw HID,
/// Xbox-layout pads with dedicated extra buttons that drive the wheels directly, like the Edge's Fn pair —
/// reference device: 8BitDo Ultimate 2C over Bluetooth, L4/R4).</summary>
public enum ControllerKind { DualSenseEdge, PlayStationOther, Xbox, ExtraButtonPad }

/// <summary>The "button" half of a wheel-trigger chord — the FIRST dropdown in the Settings ▸ Triggers
/// builder.</summary>
public enum TriggerPrimary { Fn, Bumper, Trigger, Touchpad }

/// <summary>The "combined with" half of a wheel-trigger chord — the SECOND dropdown. <see cref="None"/> is
/// the lone (locked) choice for Fn; <see cref="Swipe"/> the lone choice for Touchpad.</summary>
public enum TriggerModifier { None, Swipe, Trigger, Home, Stick, BackStart, Dpad, Bumper }

/// <summary>The catalog of wheel-trigger gestures. Single source of truth shared by the Settings builder
/// (which composes a <c>primary + modifier</c> pair into a token) and the TriggerInterpreter (which reads
/// the token). Tokens are stored in <see cref="SystemConfig.TriggerModes"/>.
///
/// All three kinds are read on hardware (Edge/DualSense/DS4 via raw HID, Xbox via XInput).</summary>
public static class TriggerModes
{
    // Tokens (stored in config; also the composed value of a builder (primary, modifier) pair).
    public const string FnButtons        = "fn";                  // DS Edge front Fn1/Fn2
    public const string TouchpadSwipe    = "touchpad-swipe";      // finger entering from a touchpad edge, inward
    public const string BumpersHome      = "bumpers-home";        // L1/R1 + Home(PS/Guide)
    public const string TriggersHome     = "triggers-home";       // L2/R2 + Home(PS/Guide)
    public const string BumpersTriggers  = "bumpers-triggers";    // L1+L2 / R1+R2
    public const string ViewMenuStick    = "viewmenu-stick";      // LEGACY (View/Menu + L3/R3): honoured at runtime, not offered in the builder
    public const string ViewMenuBumpers  = "viewmenu-bumpers";    // Bumper + Back/Start (either Back/Start; the bumper hand picks the side, like Fn)
    public const string ViewMenuTriggers = "viewmenu-triggers";   // Trigger + Back/Start (either Back/Start; the trigger hand picks the side)
    public const string BumpersStick     = "bumpers-stick";       // Bumper + L3/R3 (clicked stick = side, other-hand bumper)
    public const string TriggersStick    = "triggers-stick";      // Trigger + L3/R3
    public const string BumpersDpad      = "bumpers-dpad";        // Bumper + D-Pad L/R (direction = side, either bumper)
    public const string TriggersDpad     = "triggers-dpad";       // Trigger + D-Pad L/R
    // Extra-button pads (L4/R4) as a CHORD half rather than a lone summon — for players who bind L4/R4
    // in games and need the wheel gesture to take a second button. Same shapes as the Bumper family.
    // ⚠ The shoulder pairings are OPPOSITE-HAND (L4+R2, R4+L1 …), unlike the Bumper family's same-hand
    // squeeze: one hand holds the paddle, the other taps — and the paddle hand still names the side, so
    // L4 + a right shoulder opens the RIGHT wheel via the usual cross (see App.ResolveTrigger).
    public const string ExtraTriggers    = "extra-triggers";      // L4 + R2 / R4 + L2
    public const string ExtraBumpers     = "extra-bumpers";       // L4 + R1 / R4 + L1
    public const string ExtraHome        = "extra-home";          // L4/R4 + Home
    public const string ExtraStick       = "extra-stick";         // L4/R4 + L3/R3 (clicked stick = side)
    public const string ExtraSelectStart = "extra-select-start";  // L4/R4 + Select/Start (either)
    public const string ExtraDpad        = "extra-dpad";          // L4/R4 + D-Pad L/R (direction = side)

    // ── The (primary, modifier) ⇄ token catalog that drives the Settings ▸ Triggers builder ──────────
    // Each row a user builds picks a primary from the first dropdown and a modifier from the second; the
    // pair composes to one of these tokens. Order here is the second-dropdown order for Bumper/Trigger.
    private static readonly (TriggerPrimary P, TriggerModifier M, string Token)[] Catalog =
    [
        (TriggerPrimary.Fn,       TriggerModifier.None,      FnButtons),
        (TriggerPrimary.Touchpad, TriggerModifier.Swipe,     TouchpadSwipe),
        (TriggerPrimary.Bumper,   TriggerModifier.Trigger,   BumpersTriggers),
        (TriggerPrimary.Bumper,   TriggerModifier.Home,      BumpersHome),
        (TriggerPrimary.Bumper,   TriggerModifier.Stick,     BumpersStick),
        (TriggerPrimary.Bumper,   TriggerModifier.BackStart, ViewMenuBumpers),
        (TriggerPrimary.Bumper,   TriggerModifier.Dpad,      BumpersDpad),
        (TriggerPrimary.Trigger,  TriggerModifier.Home,      TriggersHome),
        (TriggerPrimary.Trigger,  TriggerModifier.Stick,     TriggersStick),
        (TriggerPrimary.Trigger,  TriggerModifier.BackStart, ViewMenuTriggers),
        (TriggerPrimary.Trigger,  TriggerModifier.Dpad,      TriggersDpad),
        // Fn's non-None rows are offered ONLY to extra-button pads (PrimariesFor/ModifiersFor); the Edge's
        // Fn pair stays a lone summon. Decompose is shared, so a hand-edited token still round-trips.
        (TriggerPrimary.Fn,       TriggerModifier.Bumper,    ExtraBumpers),
        (TriggerPrimary.Fn,       TriggerModifier.Trigger,   ExtraTriggers),
        (TriggerPrimary.Fn,       TriggerModifier.Home,      ExtraHome),
        (TriggerPrimary.Fn,       TriggerModifier.Stick,     ExtraStick),
        (TriggerPrimary.Fn,       TriggerModifier.BackStart, ExtraSelectStart),
        (TriggerPrimary.Fn,       TriggerModifier.Dpad,      ExtraDpad),
    ];

    /// <summary>The token for a (primary, modifier) pair, or null if the pair isn't a real chord.</summary>
    public static string? Compose(TriggerPrimary p, TriggerModifier m)
    {
        foreach (var c in Catalog) if (c.P == p && c.M == m) return c.Token;
        return null;
    }

    /// <summary>Split a stored token back into its (primary, modifier) pair for the builder. False for a
    /// token with no builder representation (the legacy <see cref="ViewMenuStick"/>) — the row is skipped.</summary>
    public static bool TryDecompose(string token, out TriggerPrimary p, out TriggerModifier m)
    {
        foreach (var c in Catalog) if (c.Token == token) { p = c.P; m = c.M; return true; }
        p = TriggerPrimary.Bumper; m = TriggerModifier.Trigger; return false;
    }

    /// <summary>Primary options offered per kind (first dropdown). Fn = dedicated extra buttons — the
    /// Edge's Fn pair, or an extra-button pad's L4/R4 (same token, same event path; only the label
    /// differs — see <see cref="PrimaryLabel"/>); Touchpad = pads that have one (Sony pads);
    /// Bumper/Trigger everywhere.</summary>
    public static IReadOnlyList<TriggerPrimary> PrimariesFor(ControllerKind kind) => kind switch
    {
        ControllerKind.DualSenseEdge    => [TriggerPrimary.Fn, TriggerPrimary.Bumper, TriggerPrimary.Trigger, TriggerPrimary.Touchpad],
        ControllerKind.PlayStationOther => [TriggerPrimary.Bumper, TriggerPrimary.Trigger, TriggerPrimary.Touchpad],
        ControllerKind.ExtraButtonPad   => [TriggerPrimary.Fn, TriggerPrimary.Bumper, TriggerPrimary.Trigger],
        _                               => [TriggerPrimary.Bumper, TriggerPrimary.Trigger],
    };

    /// <summary>Modifier options for a chosen primary (second dropdown). Touchpad is single-option (the
    /// dropdown locks), and so is the Edge's Fn pair. An extra-button pad's L4/R4 additionally offers the
    /// standard second buttons, for players who bind L4/R4 in games and need the summon to take a
    /// deliberate two-button press — "—" (a lone L4/R4 press) stays FIRST, so it remains the default.
    /// Bumper adds "Trigger" (a same-hand squeeze); both bumper and trigger offer Home / L3/R3 /
    /// Back/Start / D-Pad.</summary>
    public static IReadOnlyList<TriggerModifier> ModifiersFor(TriggerPrimary primary, ControllerKind kind) => primary switch
    {
        TriggerPrimary.Fn when kind == ControllerKind.ExtraButtonPad =>
            [TriggerModifier.None, TriggerModifier.BackStart, TriggerModifier.Bumper,
             TriggerModifier.Trigger, TriggerModifier.Home, TriggerModifier.Stick, TriggerModifier.Dpad],
        TriggerPrimary.Fn       => [TriggerModifier.None],
        TriggerPrimary.Touchpad => [TriggerModifier.Swipe],
        // Back/Start is first → it's the default modifier for a fresh Bumper/Trigger row.
        TriggerPrimary.Bumper   => [TriggerModifier.BackStart, TriggerModifier.Trigger, TriggerModifier.Home, TriggerModifier.Stick, TriggerModifier.Dpad],
        TriggerPrimary.Trigger  => [TriggerModifier.BackStart, TriggerModifier.Home, TriggerModifier.Stick, TriggerModifier.Dpad],
        _                       => [TriggerModifier.None],
    };

    /// <summary>Display text for a primary.</summary>
    public static string PrimaryLabel(TriggerPrimary p, ControllerKind kind) => p switch
    {
        TriggerPrimary.Fn       => kind == ControllerKind.ExtraButtonPad ? "L4/R4" : "Fn1/Fn2",
        TriggerPrimary.Bumper   => Loc.T(UiText.Chords.Bumper),
        TriggerPrimary.Trigger  => Loc.T(UiText.Chords.Trigger),
        TriggerPrimary.Touchpad => Loc.T(UiText.Chords.Touchpad),
        _                       => p.ToString(),
    };

    /// <summary>Display text for a modifier. <paramref name="primary"/> is optional and changes only the
    /// empty pairing: a dedicated-button chord spells "(none)" out rather than showing a dash. The
    /// shoulder rows stay bare "Bumper"/"Trigger" even though the L4/R4 pairings are
    /// opposite-hand — <see cref="Describe"/> carries that detail in prose instead.</summary>
    public static string ModifierLabel(TriggerModifier m, TriggerPrimary? primary = null) => m switch
    {
        TriggerModifier.None      => primary == TriggerPrimary.Fn ? Loc.T(UiText.Chords.None) : "—",
        TriggerModifier.Swipe     => Loc.T(UiText.Chords.EdgeSwipe),
        TriggerModifier.Bumper    => Loc.T(UiText.Chords.Bumper),
        TriggerModifier.Trigger   => Loc.T(UiText.Chords.Trigger),
        TriggerModifier.Home      => Loc.T(UiText.Chords.Home),
        TriggerModifier.Stick     => "L3/R3",
        TriggerModifier.BackStart => Loc.T(UiText.Chords.SelectStart),
        TriggerModifier.Dpad      => Loc.T(UiText.Chords.DpadLeftRight),
        _                         => m.ToString(),
    };

    /// <summary>The builder's seed combo for a kind — used for a fresh row and each "+ Add": the default
    /// primary (Fn on the Edge, Bumper otherwise) paired with that primary's first (default) modifier
    /// (None for Fn, Select/Start for Bumper/Trigger).</summary>
    public static (TriggerPrimary Primary, TriggerModifier Modifier) DefaultCombo(ControllerKind kind)
    {
        var primary = PrimariesFor(kind)[0];
        return (primary, ModifiersFor(primary, kind)[0]);
    }

    /// <summary>The OUT-OF-THE-BOX default invocation for a controller kind — used before onboarding and
    /// whenever the config has no trigger entry for the kind: Fn on the Edge, Bumper + Back/Start on every
    /// other pad. Never a Home/Guide chord: that button is prone to Steam Big Picture / Xbox Game Bar
    /// interception, too fraught for a default. The bumper hand picks the side, like Fn — see
    /// TriggerInterpreter.SidesFor.
    /// (The Settings builder and onboarding practice both offer the FULL primary×modifier set via
    /// PrimariesFor/ModifiersFor, so there's no curated per-kind list to keep this in sync with.)</summary>
    /// <remarks>Extra-button pads deliberately do NOT default to their L4/R4 buttons: unlike
    /// the Edge's Fn pair, L4/R4 are ordinary remappable buttons players commonly bind in games, so
    /// claiming them out of the box would collide with an existing habit. They are one dropdown away —
    /// offered as a lone summon and as a chord half (see <see cref="ModifiersFor"/>).</remarks>
    public static string DefaultFor(ControllerKind kind) => kind switch
    {
        ControllerKind.DualSenseEdge => FnButtons,
        _                            => ViewMenuBumpers,
    };

    /// <summary>True for the tokens pairing an extra-button pad's L4/R4 with a second button — the ones
    /// whose whole point is that a LONE L4/R4 press must not act (it would be the <see cref="FnButtons"/>
    /// gesture instead, pre-empting the chord).</summary>
    public static bool IsExtraChord(string? token) =>
        token is ExtraTriggers or ExtraBumpers or ExtraHome or ExtraStick or ExtraSelectStart or ExtraDpad;

    /// <summary>A short human phrase for a stored trigger token — for toasts / notifications (the Settings
    /// builder composes its own labels from <see cref="PrimaryLabel"/> / <see cref="ModifierLabel"/>).</summary>
    public static string Describe(string token, ControllerKind? kind = null) => token switch
    {
        FnButtons        => kind == ControllerKind.ExtraButtonPad ? "L4/R4" : "Fn1/Fn2",
        TouchpadSwipe    => Loc.T(UiText.Chords.TouchpadSwipe),
        BumpersHome      => Loc.T(UiText.Chords.BumperHome),
        TriggersHome     => Loc.T(UiText.Chords.TriggerHome),
        BumpersTriggers  => Loc.T(UiText.Chords.BumperTrigger),
        ViewMenuStick    => Loc.T(UiText.Chords.SelectStartStick),
        ViewMenuBumpers  => Loc.T(UiText.Chords.BumperSelectStart),
        ViewMenuTriggers => Loc.T(UiText.Chords.TriggerSelectStart),
        BumpersStick     => Loc.T(UiText.Chords.BumperStick),
        TriggersStick    => Loc.T(UiText.Chords.TriggerStick),
        BumpersDpad      => Loc.T(UiText.Chords.BumperDpad),
        TriggersDpad     => Loc.T(UiText.Chords.TriggerDpad),
        ExtraTriggers    => Loc.T(UiText.Chords.ExtraTrigger),
        ExtraBumpers     => Loc.T(UiText.Chords.ExtraBumper),
        ExtraHome        => Loc.T(UiText.Chords.ExtraHome),
        ExtraStick       => Loc.T(UiText.Chords.ExtraStick),
        ExtraSelectStart => Loc.T(UiText.Chords.ExtraSelectStart),
        ExtraDpad        => Loc.T(UiText.Chords.ExtraDpad),
        _                => token,
    };

    /// <summary>The "both sides at once" enable/disable chord phrase for an invocation token — matches
    /// TriggerInterpreter.EnableDisableFires. D-pad chords are directional at runtime (Up = enable,
    /// Down = disable); this generic phrasing shows both. (OnboardingWindow keeps its own stateful variant
    /// that renders the exact direction for the live wheel state.)</summary>
    public static string EnableDisableLabel(string token, ControllerKind? kind = null) => token switch
    {
        FnButtons        => kind == ControllerKind.ExtraButtonPad ? "L4 + R4" : "Fn1 + Fn2",
        TouchpadSwipe    => Loc.T(UiText.Chords.BothTouchpad),
        BumpersTriggers  => Loc.T(UiText.Chords.BothBumpersTriggers),
        BumpersHome      => Loc.T(UiText.Chords.BothBumpersHome),
        TriggersHome     => Loc.T(UiText.Chords.BothTriggersHome),
        BumpersStick     => Loc.T(UiText.Chords.BothSticksBumper),
        TriggersStick    => Loc.T(UiText.Chords.BothSticksTrigger),
        ViewMenuBumpers  => Loc.T(UiText.Chords.BothBumpersSelectStart),
        ViewMenuTriggers => Loc.T(UiText.Chords.BothTriggersSelectStart),
        ViewMenuStick    => Loc.T(UiText.Chords.BothSticksSelectStart),
        BumpersDpad      => Loc.T(UiText.Chords.BothBumpersDpad),
        TriggersDpad     => Loc.T(UiText.Chords.BothTriggersDpad),
        ExtraTriggers    => Loc.T(UiText.Chords.ExtraBothTriggers),
        ExtraBumpers     => Loc.T(UiText.Chords.ExtraBothBumpers),
        ExtraHome        => Loc.T(UiText.Chords.ExtraBothHome),
        ExtraStick       => Loc.T(UiText.Chords.BothSticksExtra),
        ExtraSelectStart => Loc.T(UiText.Chords.ExtraBothSelectStart),
        ExtraDpad        => Loc.T(UiText.Chords.ExtraBothDpad),
        _                => Loc.T(UiText.Chords.TheChord),
    };

    /// <summary>A short phrase for a SET of enabled tokens: the first gesture, plus "+N" when more than one
    /// is enabled (kept short for a toast). Empty set → an em dash.</summary>
    public static string DescribeSet(IReadOnlyList<string> tokens, ControllerKind? kind = null) =>
        tokens.Count == 0 ? "—"
        : tokens.Count == 1 ? Describe(tokens[0], kind)
        : Loc.F(UiText.Chords.PlusMore, Describe(tokens[0], kind), tokens.Count - 1);

    /// <summary>How PROSE should refer to the user's invocation gesture. Naming one specific chord is only
    /// honest when exactly one is enabled — with several configured, singling out the first (which is just
    /// the builder's insertion order, not a primary) makes the sentence read as exclusive when any of them
    /// works. So multiples collapse to a plural phrase instead. Used for the Help topics' {invoke} token;
    /// callers must keep the surrounding sentence grammatical for BOTH shapes (prefer "open a wheel with
    /// X" over "X opens a wheel").</summary>
    public static string DescribeChoice(IReadOnlyList<string> tokens) =>
        tokens.Count switch { 0 => Loc.T(UiText.Chords.YourInvocationChord), 1 => Describe(tokens[0]), _ => Loc.T(UiText.Chords.YourChosenChords) };

    /// <summary>The <see cref="EnableDisableLabel"/> counterpart to <see cref="DescribeChoice"/> — with
    /// several chords configured every one of them has its own "both sides" form, so name the rule rather
    /// than one arbitrary instance. Used for the Help topics' {disable} token.</summary>
    public static string EnableDisableChoice(IReadOnlyList<string> tokens) =>
        tokens.Count switch
        {
            0 => Loc.T(UiText.Chords.TheChord),
            1 => EnableDisableLabel(tokens[0]),
            _ => Loc.T(UiText.Chords.BothSidesOfAny),
        };
}
