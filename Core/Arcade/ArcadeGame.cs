namespace ControllerWheel;

/// <summary>One frame's worth of controller state, as an arcade game sees it.
///
/// <para><b>The input budget:</b> one stick, ✕ / □, and the <b>d-pad</b>. ○ is reserved globally for
/// dismiss and never reaches a game — that's what makes "you can always get out with one button" true of
/// every game in the set, present and future. Bumpers and triggers stay out: the summon gestures live there.</para>
///
/// <para>⚠ <b>△ is a host button</b> (it opens the game's <see cref="IArcadeGame.HowTo"/> card), so
/// <see cref="TriangleDown"/> / <see cref="TrianglePressed"/> are <b>always false</b> in the app — the host
/// consumes the press before <see cref="IArcadeGame.Step"/> is reached. They stay on the record because
/// every probe and harness constructs this positionally: treat them
/// as reserved, and do not bind a new game's mechanic to △ — it would silently never fire. The guard card's
/// hold-to-play-anyway △ is host-side too, and also never reaches a game.</para>
///
/// <para><b>The d-pad</b> reaches a game only as edges (<see cref="DPadPressed"/>) — a grid cursor wants
/// discrete steps, and level state would make every game invent its own repeat timer. While an arcade game
/// is open, the wheel's own
/// d-pad modes (volume scrub, Alt-Tab, track skip) are suppressed; that suppression is what makes this safe,
/// since they would otherwise fire under a game using the same thumb.</para>
///
/// <para>The <c>*Down</c> flags are level (held); the <c>*Pressed</c> flags are edges, true on exactly one
/// sim step per physical press. Stick coordinates are the raw pad values in screen orientation — y is
/// positive downward, matching <c>WheelStateMachine.UpdateStick</c>. Games apply their own deadzone; the
/// wheel's 0.38 is tuned to make accidental arming hard, which is the opposite of what a game wants.</para></summary>
/// <param name="SkipPressed">The Select button, as an edge — the dev level-skip (<see cref="ArcadeDebug.LevelSkip"/>)
/// is its only consumer and the host forwards it only while a game is open.</param>
public readonly record struct ArcadeInput(
    float StickX,
    float StickY,
    bool CrossDown,
    bool TriangleDown,
    bool SquareDown,
    bool CrossPressed,
    bool TrianglePressed,
    bool SquarePressed,
    int DPadPressed = 0,
    bool SkipPressed = false)
{
    /// <summary>D-pad directions, as bits in <see cref="DPadPressed"/>. Optional trailing parameter so every
    /// existing construction site — games, probes, the standalone harness — still compiles unchanged.</summary>
    [Flags]
    public enum DPad { None = 0, Up = 1, Right = 2, Down = 4, Left = 8 }

    /// <summary>D-pad edges this step, in readable form. True for exactly one sim step per press.</summary>
    public DPad DPadEdges => (DPad)DPadPressed;

    public bool DPadUp    => (DPadPressed & (int)DPad.Up)    != 0;
    public bool DPadRight => (DPadPressed & (int)DPad.Right) != 0;
    public bool DPadDown  => (DPadPressed & (int)DPad.Down)  != 0;
    public bool DPadLeft  => (DPadPressed & (int)DPad.Left)  != 0;

    /// <summary>Stick deflection, 0..~1.</summary>
    public float Magnitude => MathF.Sqrt(StickX * StickX + StickY * StickY);

    /// <summary>Stick direction as a screen angle in radians: 0 = straight up (12 o'clock), increasing
    /// clockwise. Every arcade game measures angles this way, so "up is up" is never re-derived.
    /// Meaningless when <see cref="Magnitude"/> is inside the game's deadzone.</summary>
    public double ScreenAngle => Math.Atan2(StickX, -StickY);

    /// <summary>Everything released / centred. The state a game is stepped with while a guard card or the
    /// resume countdown is up, so nothing accumulates behind them.</summary>
    public static ArcadeInput Neutral => default;
}

/// <summary>One game-supplied row in the START pause menu: a label and a set of choices, one of which is
/// current. Deliberately a tiny value type rather than a callback — the menu has to be able to draw the row
/// (and remember where the cursor is) without knowing what any of it means.</summary>
/// <param name="Key">Stable identifier the game matches in <see cref="IArcadeGame.ApplyPauseOption"/>.</param>
/// <param name="StartsOnConfirm">True for an action row: d-pad left/right only browse the shown value, and ✕ is
/// what calls <see cref="IArcadeGame.ApplyPauseOption"/> with it. The browsed value is the host's and is dropped
/// when the menu closes; <paramref name="Selected"/> stays the game's own current value. False (every other
/// row): a step applies at once and ✕ cycles.</param>
public sealed record ArcadePauseOption(string Key, string Label, string[] Choices, int Selected,
                                       bool StartsOnConfirm = false);

/// <summary>One bullet on a game's how-to-play card: a line of text and, optionally, a small drawing beside
/// it.
///
/// <para><paramref name="Art"/> is a key the game's renderer understands
/// (<c>IArcadeRenderer.DrawHowToArt</c>) — the sim names the picture, the shell draws it, so this stays
/// WPF-free like the rest of <c>Core</c>. An empty key means "no picture", and a key the renderer doesn't
/// recognise draws nothing rather than throwing: a how-to card must never be the thing that takes down a
/// game.</para>
///
/// <para><paramref name="Text"/> may carry the button tokens <c>{cross}</c>, <c>{circle}</c>,
/// <c>{square}</c>, <c>{triangle}</c>, resolved by the host against the user's chosen glyph set — so a
/// bullet reads "Y" on an Xbox pad and "△" on a DualSense without the game knowing either exists.</para></summary>
public sealed record ArcadeHowToLine(string Text, string Art = "");

/// <summary>A game's how-to-play card — the △ overlay, and the pause menu's HOW TO PLAY row.
///
/// <para>Deliberately data rather than a rendered card each game draws for itself: the host owns the layout,
/// so every game's how-to has the same shape, the same type scale and the same way out, and a new game gets
/// all of that by writing five strings. It is the same argument <see cref="ArcadePauseOption"/> makes for the
/// pause menu.</para>
///
/// <para>⚠ Keep it to about five short lines. The card is drawn in a round window with no scrolling — there
/// is no second page and no room to add one, which is the constraint that keeps a how-to a how-to rather
/// than a manual.</para></summary>
public sealed record ArcadeHowTo(string Title, ArcadeHowToLine[] Lines);

/// <summary>
/// A miniature game playable in the round arcade window.
///
/// <para>Sim only — no rendering, no WPF, no clock of its own. The host owns the frame pump and calls
/// <see cref="Step"/> at a fixed timestep (<see cref="ArcadeTuning.StepSeconds"/>), so behaviour is
/// deterministic: a tuning number means the same thing on every machine, and a frame hitch — which an
/// overlay drawn over a loading game will get — can't teleport anything through anything. The matching
/// renderer lives in the WPF shell and reads this object's state directly.</para>
///
/// <para><see cref="Serialize"/> / <see cref="Restore"/> are what make the whole feature's promise work:
/// dismissing with ○ freezes the game exactly, and it resumes mid-wave later — including across an app
/// restart, via <see cref="ArcadeStore"/>.</para>
/// </summary>
public interface IArcadeGame
{
    /// <summary>Stable config token — this is what a slice stores in <c>ActionConfig.Command</c>, so it can
    /// never change once shipped.</summary>
    string Id { get; }

    /// <summary>Display name, for the slice label, the picker and the HUD.</summary>
    string Title { get; }

    /// <summary>Advance one fixed step.</summary>
    void Step(in ArcadeInput input, double dt);

    /// <summary>The run is over. The host uses this to decide there's nothing worth freezing: a finished run's
    /// snapshot is cleared so the next open starts clean instead of re-showing a game-over screen.</summary>
    bool IsGameOver { get; }

    /// <summary>Extra rows this game contributes to the START pause menu, after Resume and Reset. Empty for
    /// a game with no settings of its own.
    ///
    /// <para>A default implementation, so adding a game never forces you to think about the pause menu — and
    /// so the two games that do have options declare them next to the state they change, rather than the menu
    /// growing a switch over game ids.</para></summary>
    IReadOnlyList<ArcadePauseOption> PauseOptions => [];

    /// <summary>How to play this game, shown by △ and by the pause menu's HOW TO PLAY row. Null = this game
    /// has no card, and the host then offers neither route to one.
    ///
    /// <para>A card is why a game need not print button hints over its own playfield: a hint spends board
    /// space on every frame of every session to teach one thing once, where this costs one button and can
    /// afford to explain the rules rather than name the keys.</para>
    ///
    /// <para>A default returning null, so a game is never forced to write one — and the △ hint and the menu
    /// row appear only when it doesn't, per the no-offer contract this codebase applies everywhere: nothing
    /// advertises a route that dead-ends.</para></summary>
    ArcadeHowTo? HowTo => null;

    /// <summary>Apply a pause-menu choice. <paramref name="key"/> is the option's own key and
    /// <paramref name="choice"/> its new index. Called on the sim thread while paused.</summary>
    void ApplyPauseOption(string key, int choice) { }

    /// <summary>A yes/no question the host must ask before applying this option, or null to apply it straight
    /// away.
    ///
    /// <para>Some options are destructive of the thing you are in the middle of — Kabloom's starting size
    /// throws the current campaign away — and a pause menu is exactly where a thumb wanders. The game is the
    /// only thing that knows whether there is anything to lose, so it decides: the same choice asks nothing on
    /// a fresh board and asks before a run in progress.</para>
    ///
    /// <para>⚠ Asking must have no side effect. Nothing changes until <see cref="ApplyPauseOption"/> is
    /// actually called, so backing out at the prompt leaves the run exactly as it was. Default: never ask,
    /// so an option with nothing at stake needs no thought from a future game.</para></summary>
    string? ConfirmPauseOption(string key, int choice) => null;

    /// <summary>This game's pause-menu settings, as its own blob. Null or empty = nothing to remember.
    ///
    /// <para>⚠ Separate from <see cref="Serialize"/> on purpose. A run's snapshot is cleared the moment the
    /// run ends, so a setting kept in there would survive dismissing and not dying — worse than not
    /// persisting at all. Settings live beside the high score, which outlives a finished run.</para></summary>
    string? SerializeSettings() => null;

    /// <summary>Restore settings written by <see cref="SerializeSettings"/>. Hostile input, like every other
    /// read path here: anything unparseable is discarded in favour of the defaults, never a crash.
    ///
    /// <para>⚠ Restoring must not have gameplay side effects. This runs while a game is being made live,
    /// which is not the moment to act on a choice the player made in a previous session.</para></summary>
    void RestoreSettings(string json) { }

    /// <summary>Rotational kick the host applies to the whole disc this frame, in radians. The disc twists on
    /// impact and never slides: a linear shake in a round window reads as the window breaking. A sim-owned
    /// value that must decay to exactly 0, or the host keeps a transform pushed forever. Default: still.</summary>
    double DiscTwist => 0;

    /// <summary>Scale kick the host applies to the whole disc this frame (1 = none). Same rules as
    /// <see cref="DiscTwist"/>: sim-owned, decays to exactly 1.</summary>
    double DiscPunch => 1;

    /// <summary>Begin a fresh run, discarding whatever the current one was.
    ///
    /// <para>Resuming into a game-over screen is never what you wanted. The host calls this whenever
    /// it makes a finished game live again, so re-opening the wheel always drops you into something playable.
    /// The disk path doesn't need it (<c>ArcadeStore</c> clears a finished run's snapshot), but a game
    /// dismissed mid-game-over keeps its live object for the rest of the session — this is what revives
    /// it.</para>
    ///
    /// <para>Games with no lose condition (Kabloom's campaign) never have this called — the host only calls it
    /// when <see cref="IsGameOver"/> is true, which for those is never — so whatever they implement here is
    /// unreachable from the host.</para></summary>
    void Restart();

    /// <summary>The game is being frozen while an outcome screen is up — a stung board, a fall, a cleared level
    /// still presenting, a finished campaign. Dismissing is the player's acknowledgement of it: move straight
    /// to the next playable state, settled — no transition or cue in flight — so the cabinet shot and the
    /// snapshot both show the board they will come back to, never the screen they already read.
    ///
    /// <para>For a game whose <see cref="IsGameOver"/> is true the host calls <see cref="Restart"/> instead
    /// and never reaches this. It is for the games whose "game over" is a step in a continuing run. No-op
    /// when nothing is up; a default, so a game with no such screen need not think about it.</para></summary>
    void AcknowledgeOutcome() { }

    /// <summary>The host cleared its input because the board stopped being played — pause, a how-to card, the
    /// isolation guard, the resume beat, a restart. A game that tracks a held button drops it here, so a
    /// release made during the gap cannot act on the first step back. Default: nothing to drop.</summary>
    void CancelInput() { }

    /// <summary>Score worth persisting as this game's best.</summary>
    int HighScore { get; }

    /// <summary>Hand the game its stored best from a previous session. Separate from <see cref="Restore"/>
    /// because a fresh game still has a best to beat — the snapshot may be absent (first run, or a finished
    /// run that cleared it) while the high score is not.</summary>
    void SeedHighScore(int high);

    /// <summary>JSON snapshot of everything needed to resume. Must round-trip through
    /// <see cref="Restore"/>.</summary>
    string Serialize();

    /// <summary>Restore a snapshot. Must treat the input as hostile: anything unparseable, from an older
    /// build, or internally inconsistent is discarded silently in favour of a fresh game. A save file is
    /// never worth a crash or a prompt.</summary>
    void Restore(string json);
}
