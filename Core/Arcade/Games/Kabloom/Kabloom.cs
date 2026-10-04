using System.Diagnostics;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>Controller-first Minesweeper deduction on a circular pentagonal-floret crop. Stays out of
/// <see cref="ArcadeCatalog"/> until an integration change gives players a path to open it.</summary>
public sealed class Kabloom : IArcadeGame
{
    public const string GameId = "kabloom";
    public string Id => GameId;
    public string Title => "Kabloom";

    public enum Stage { AwaitingFirstReveal, Generating, Playing, Failed, Cleared, CampaignComplete }

    [Flags]
    public enum Cue
    {
        None = 0, Focus = 1, Reveal = 2, Bloom = 4, Flag = 8, Unflag = 16,
        Guarded = 32, Mine = 64, Cleared = 128, Continue = 256, Diamond = 512,
        // Progression beats the host sounds distinctly; LevelUp is the ordinary advance, Continue the fall-back
        // after a bee. BeeDeparts fires once, when the hit bee starts its flight (a beat after the reveal).
        LevelUp = 1024, CampaignComplete = 2048, Growth = 4096, BeeDeparts = 8192,
        /// <summary>The run's gem count passed the best it started with. Once per run.</summary>
        NewBest = 16384,
    }

    public KabloomGrid Grid { get; private set; } = null!;
    public KabloomBoard Board { get; private set; } = null!;
    public KabloomLevelProfile Profile { get; private set; } = null!;
    public KabloomGenerationSession? Generation { get; private set; }
    public KabloomSolveReport? AcceptedSolve { get; private set; }
    public Stage Phase { get; private set; }
    public double PhaseTime { get; private set; }
    public double ActiveTime { get; private set; }
    public int CurrentLevel { get; private set; } = 1;
    public int HighestClearedLevel { get; private set; }
    public int FocusCell { get; private set; }
    /// <summary>The analog reticle, in board coordinates. The renderer draws it; the cell nearest it is
    /// <see cref="FocusCell"/>. Not serialized — derivable from the focused cell, where a resume parks
    /// it.</summary>
    public double PointerX { get; private set; }
    public double PointerY { get; private set; }
    public double ActionPulse { get; private set; }
    public KabloomActionKind LastAction { get; private set; }
    /// <summary>Cells the last reveal opened. Read by the host beside <see cref="Cue.Bloom"/> so a cascade can
    /// sound as big as it was; not serialized (a cue-frame fact).</summary>
    public int LastCascadeSize { get; private set; }
    // ⚠ Don't bind a mechanic to △: the host owns it for the HOW TO PLAY card, so a game never sees it.
    public double PresentationTime { get; private set; }
    public int DiamondScore { get; private set; }
    public int LevelDiamondsCollected => _diamondCounted.Count(collected => collected);
    public int LevelDiamondCount => Grid.CenterCaps.Count;
    public int HighScore { get; private set; }
    public bool IsGameOver => false; // This is a persistent campaign, so the host must always freeze it.
    public ulong CampaignSeed { get; private set; } = 0x4B41424C4F4F4D21UL; // "KABLOOM!"

    private Cue _cues;
    /// <summary>The run has already announced a new best; the counter keeps climbing silently after that.
    /// Not serialized: rebuilt on restore from whether the run's score already stands at the best.</summary>
    private bool _bestBeaten;
    private bool[] _knownRevealed = [];
    private readonly Dictionary<int, double> _revealStarts = [];
    private readonly Dictionary<int, double> _flagStarts = [];
    /// <summary>The □ press in flight: when it landed, on which cell, and whether the hold already spent it as
    /// a question mark. Negative infinity = no press in flight. Presentation-clock, not serialized: a press
    /// cannot outlive a freeze.</summary>
    private double _squareHeldSince = double.NegativeInfinity;
    private int _squareCell;
    private bool _squareSpent;

    /// <summary>The interstitial that announces a new bees-per-petal capacity. <see cref="CapacityNotice"/> is
    /// the capacity being announced, 0 when no card is up; it goes up the first time a run reaches a level whose
    /// capacity exceeds the highest it has already been told about (<c>_noticeSeenCapacity</c>, per run, saved),
    /// and it holds the board until ✕ is pressed at least <see cref="KabloomTuning.CapacityNoticeMinSeconds"/>
    /// after it appeared. The timer restarts on resume so a frozen card is never dismissable on the first
    /// frame.</summary>
    public int CapacityNotice => _noticeCapacity;
    public double CapacityNoticeSeconds => _noticeCapacity == 0 ? 0 : Math.Max(0, PresentationTime - _noticeStart);
    public bool CapacityNoticeReady => _noticeCapacity != 0
        && (!_noticeLocked || CapacityNoticeSeconds >= KabloomTuning.CapacityNoticeMinSeconds);
    /// <summary>True while the card is a first meeting with this capacity and still inside its reading period.</summary>
    public bool CapacityNoticeLocked => _noticeCapacity != 0 && _noticeLocked && !CapacityNoticeReady;
    private int _noticeCapacity;
    private double _noticeStart;
    private bool _noticeLocked;
    private int _noticeSeenCapacity = 1;
    private double[] _mineClearStarts = [];
    private double[] _diamondStarts = [];
    private bool[] _diamondCounted = [];
    /// <summary>When the level-change animation started, on the presentation clock. Negative infinity = no
    /// transition, which makes <see cref="TransitionElapsed"/> infinite and every renderer branch fall through
    /// to "settled" without a null check.</summary>
    private double _transitionStart = double.NegativeInfinity;

    /// <summary>Presentation-clock time the difficulty-growth banner was raised, and what it says.</summary>
    private double _growthStart = double.NegativeInfinity;
    private bool _growthCells, _growthMines;
    /// <summary>0→1 across the banner's life; ≥1 = nothing to show.</summary>
    public double GrowthProgress => Math.Clamp(
        (PresentationTime - _growthStart) / Math.Max(0.1, KabloomTuning.GrowthNoticeSeconds), 0, 1);
    /// <summary>The board got bigger this level.</summary>
    public bool GrowthShowsCells => _growthCells && GrowthProgress < 1;
    /// <summary>Bees got denser this level. Can be true alongside <see cref="GrowthShowsCells"/> — they're
    /// independent axes and a level that moves both should say both.</summary>
    public bool GrowthShowsMines => _growthMines && GrowthProgress < 1;
    private int _scoreAtLevelStart;
    // Indexed by level, so it needs one more slot than there are levels.
    private int[] _levelEntryScores = new int[CampaignLevels + 1];

    public bool RevealCascadeActive => _revealStarts.Values.Any(start =>
        PresentationTime < start + KabloomTuning.RevealBurstSeconds);
    public bool PresentationActive => RevealCascadeActive
        || _mineClearStarts.Any(start => double.IsFinite(start)
            && PresentationTime < start + KabloomTuning.MineFinaleSeconds)
        || Enumerable.Range(0, _diamondStarts.Length).Any(id => !_diamondCounted[id]
            && double.IsFinite(_diamondStarts[id]));

    /// <summary>Seconds since the level-change animation began; infinite when there isn't one. The renderer
    /// derives the zoom, the wave delay, and each petal's flip from this one number, so the parts of the
    /// effect can't drift against each other.</summary>
    public double TransitionElapsed => PresentationTime - _transitionStart;
    /// <summary>True while the level-change animation is running.</summary>
    public bool Transitioning => TransitionElapsed < KabloomTuning.LevelTransitionSeconds;

    /// <summary>-1 while a logically revealed tile is still waiting for its visual wave, then 0..1.</summary>
    public double RevealProgress(int cell)
    {
        if (!Board.IsRevealed(cell)) return -1;
        if (!_revealStarts.TryGetValue(cell, out double start)) return 1;
        return Math.Clamp((PresentationTime - start) / Math.Max(0.05, KabloomTuning.RevealBurstSeconds), -1, 1);
    }

    /// <summary>0..1 age of the most recent lock placement, or 1 for a settled lock.</summary>
    public double FlagProgress(int cell)
    {
        if (!Board.IsFlagged(cell) && !Board.IsMarked(cell)) return 0;
        if (!_flagStarts.TryGetValue(cell, out double start)) return 1;
        return Math.Clamp((PresentationTime - start) / Math.Max(0.05, KabloomTuning.FlagPopSeconds), 0, 1);
    }

    // The bee you set off: which cell, and when its departure starts, so the renderer needs no state of its own.
    private int _hitBeeCell = -1;
    private double _hitBeeStart = double.NegativeInfinity;
    private bool _beeDepartCued;
    /// <summary>When a cleared board's presentation fell quiet, so the level can hold for a beat before the
    /// transition takes it away. Negative infinity = not quiet yet.</summary>
    private double _clearedIdleSince = double.NegativeInfinity;
    /// <summary>When a failed or finished board's presentation fell quiet — the moment its card may show.
    /// Negative infinity = not quiet yet. Sim-owned so the card's fade holds still under pause and freeze.</summary>
    private double _outcomeQuietSince = double.NegativeInfinity;
    /// <summary>Seconds the outcome card has been showable for; 0 while the presentation still runs.</summary>
    public double OutcomeQuietSeconds =>
        double.IsNegativeInfinity(_outcomeQuietSince) ? 0 : Math.Max(0, PresentationTime - _outcomeQuietSince);
    /// <summary>The cell whose bee was set off, or −1. Its lightning runs for as long as the failure screen
    /// is up; its flight is <see cref="HitBeeProgress"/>.</summary>
    public int HitBeeCell => _hitBeeCell;
    /// <summary>−1 before the hit bee starts leaving, then 0..1 across its departure.</summary>
    public double HitBeeProgress
    {
        get
        {
            if (_hitBeeCell < 0 || !double.IsFinite(_hitBeeStart)) return -1;
            double t = (PresentationTime - _hitBeeStart) / Math.Max(0.05, KabloomTuning.StungBeeFlightSeconds);
            return t < 0 ? -1 : Math.Min(1, t);
        }
    }

    /// <summary>True once the outcome card may offer its button prompts — and, in the same breath, once it
    /// may act on them. ⚠ One property for both on purpose: a prompt the board is already obeying, or a
    /// button that does nothing while its prompt is on screen, are the two ways this goes wrong, and they
    /// cannot happen if the renderer and the sim read the same answer.
    ///
    /// <para>On a stung board that means waiting for the bee to finish flying and settle into its parked
    /// bob — the bee is the explanation for what just happened, and a prompt arriving over it invites the
    /// player to dismiss the board before they have read why they lost. A board with no bee to show
    /// (<see cref="HitBeeCell"/> below zero) and a finished campaign have nothing to wait for.</para>
    ///
    /// <para>○ leaves the arcade regardless — that door is the host's and is never barred.</para></summary>
    public bool OutcomePromptsReady =>
        Phase is Stage.Failed or Stage.CampaignComplete && !PresentationActive
        && (Phase != Stage.Failed || _hitBeeCell < 0 || HitBeeProgress >= 1);

    /// <summary>-1 before a completion mine begins its harmless clear, then 0..1.</summary>
    public double MineClearProgress(int cell)
    {
        if (cell < 0 || cell >= _mineClearStarts.Length || !Board.IsMine(cell)) return -1;
        double start = _mineClearStarts[cell];
        if (!double.IsFinite(start)) return -1;
        return Math.Clamp((PresentationTime - start) / Math.Max(0.05, KabloomTuning.MineFinaleSeconds), -1, 1);
    }

    /// <summary>-1 while resting in its opening, 0..1 while flying to the counter, 1 once counted.
    /// ⚠ The flight starts <see cref="KabloomTuning.DiamondSwellSeconds"/> after the gem is freed (see
    /// <see cref="DiamondSwell"/>); this stays −1 for that whole beat.</summary>
    public double DiamondProgress(int diamond)
    {
        if (diamond < 0 || diamond >= _diamondStarts.Length) return -1;
        if (_diamondCounted[diamond]) return 1;
        double start = _diamondStarts[diamond];
        if (!double.IsFinite(start)) return -1;
        double flight = PresentationTime - start - KabloomTuning.DiamondSwellSeconds;
        if (flight < 0) return -1;
        return Math.Clamp(flight / Math.Max(0.1, KabloomTuning.DiamondFlightSeconds), 0, 1);
    }

    /// <summary>A freed gem spins up and swells to <see cref="KabloomTuning.DiamondSwellScale"/> before it
    /// flies. −1 while resting or already in flight, else 0..1 across the swell.</summary>
    public double DiamondSwell(int diamond)
    {
        if (diamond < 0 || diamond >= _diamondStarts.Length || _diamondCounted[diamond]) return -1;
        double start = _diamondStarts[diamond];
        if (!double.IsFinite(start)) return -1;
        double swell = PresentationTime - start;
        double span = Math.Max(0.05, KabloomTuning.DiamondSwellSeconds);
        return swell >= span ? -1 : Math.Clamp(swell / span, 0, 1);
    }

    public Kabloom() => LoadLevel(1);

    public Cue TakeCues() { Cue cues = _cues; _cues = Cue.None; return cues; }

    /// <summary>Start the campaign over at whatever <see cref="StartingSize"/> says. ⚠ Must not be a no-op:
    /// the pause menu's Reset row calls it as well as the host.
    ///
    /// <para>Resets the whole run, not the current board — level back to the starting size's own first level,
    /// score to zero, seed rerolled. Replaying one board is what falling back after a bee does.</para></summary>
    public void Restart()
    {
        DiamondScore = 0;
        _bestBeaten = false;
        Array.Clear(_levelEntryScores);
        RerollCampaignSeed();
        _noticeSeenCapacity = 1;
        LoadLevel(StartLevelFor(StartingSize), animate: true);
    }

    /// <summary>Which chunk of the campaign to start from: level 1, or the first level of each bees-per-petal
    /// step (two a petal, then three), letting a returning player past the early teaching ramp.</summary>
    public int StartingSize { get; private set; }

    /// <summary>First level for a starting-size choice: the first level whose <see cref="KabloomLevelProfile.BeeCapacity"/>
    /// reaches <c>size + 1</c>. Read off the profile rather than written down, so it tracks the curve.</summary>
    public static int StartLevelFor(int size)
    {
        int capacity = Math.Clamp(size, 0, 2) + 1;
        for (int level = 1; level <= CampaignLevels; level++)
            if (KabloomLevelProfile.ForLevel(level).BeeCapacity >= capacity) return level;
        return 1;
    }

    /// <summary>Campaign length, including the introductory level 1. Derived from the profile source so the
    /// two can't disagree.</summary>
    public const int CampaignLevels = KabloomLevelProfile.TotalLevels;

    public IReadOnlyList<ArcadePauseOption> PauseOptions =>
        [new("start", Loc.T(UiText.Arcade.StartingSize), [Loc.T(UiText.Arcade.Easy), Loc.T(UiText.Arcade.Medium), Loc.T(UiText.Arcade.Hard)], StartingSize)];

    /// <summary>The △ card. It teaches the rule, not the button — a button is one token and the rest of the
    /// line is what the game actually wants understood. That is what earns the card its place over a prompt
    /// printed on the board.
    ///
    /// <para>The last line is the one worth the space: every board is provably solvable, and a player who
    /// doesn't know that plays it like a slot machine and blames the game when they lose.</para>
    ///
    /// <para>Copy is author-editable — see docs/ARCADE.md. ⚠ Keep the bullets short: they wrap, and a wrap
    /// pushes every row below it down. <c>TestHarness.exe arcade</c> fails if the card stops fitting.</para></summary>
    public ArcadeHowTo? HowTo => new(Loc.T(UiText.Arcade.HowToPlay),
    [
        new(Loc.T(UiText.Arcade.KabloomHow1), "petals"),
        new(Loc.T(UiText.Arcade.KabloomHow3), "cursor"),
        new(Loc.T(UiText.Arcade.KabloomHow2), "clue"),
        new(Loc.T(UiText.Arcade.KabloomHow4), "flag"),
        new(Loc.T(UiText.Arcade.KabloomHow5), "safe"),
    ]);

    private sealed record SettingsSnap(int Start);

    public string? SerializeSettings() => JsonSerializer.Serialize(new SettingsSnap(StartingSize));

    /// <summary>⚠ Records the choice without jumping levels. Restoring runs while the game is being made
    /// live, and a resumed campaign that had progressed past its starting size would be thrown back to it.
    /// Only the pause menu jumps.</summary>
    public void RestoreSettings(string json)
    {
        try { StartingSize = Math.Clamp(JsonSerializer.Deserialize<SettingsSnap>(json)?.Start ?? 0, 0, 2); }
        catch (JsonException ex) { Trace.WriteLine($"[Arcade] Kabloom: settings snapshot unreadable: {ex.Message}"); }
        catch (NotSupportedException ex) { Trace.WriteLine($"[Arcade] Kabloom: settings snapshot unreadable: {ex.Message}"); }
    }

    /// <summary>Changing the starting size starts a new campaign, so it asks first when there is a campaign to
    /// lose. "A run in progress" is generous: any planted board, any level past the one this choice would drop
    /// you on, or any score banked.
    ///
    /// <para>⚠ Re-selecting the size you already have is not a change and must never ask — the host cycles an
    /// option row with ✕, so wrapping past the end lands back on the current value.</para></summary>
    public string? ConfirmPauseOption(string key, int choice)
    {
        if (!string.Equals(key, "start", StringComparison.OrdinalIgnoreCase)) return null;
        if (Math.Clamp(choice, 0, 2) == StartingSize) return null;
        bool inProgress = Board.Phase != KabloomBoardPhase.Unplanted
                          || CurrentLevel > StartLevelFor(StartingSize)
                          || DiamondScore > 0;
        return inProgress ? Loc.T(UiText.Arcade.StartNewRun) : null;
    }

    public void ApplyPauseOption(string key, int choice)
    {
        if (!string.Equals(key, "start", StringComparison.OrdinalIgnoreCase)) return;
        StartingSize = Math.Clamp(choice, 0, 2);
        // Jump immediately rather than waiting for the next level, via Restart so the score reset and reroll
        // come with it. ⚠ Reset must land in the same place this does, or the two rows disagree about where a
        // run begins.
        Restart();
    }

    public void SeedHighScore(int high)
    {
        HighScore = Math.Max(HighScore, Math.Max(0, high));
    }

    public void Step(in ArcadeInput input, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.05);
        PhaseTime += dt;
        PresentationTime += dt;
        ActionPulse = Math.Max(0, ActionPulse - dt * 4.5);
        UpdateCollectibles();
        if (!_beeDepartCued && _hitBeeCell >= 0 && PresentationTime >= _hitBeeStart) { _beeDepartCued = true; _cues |= Cue.BeeDeparts; }
        if (Phase is Stage.Failed or Stage.CampaignComplete && !PresentationActive)
        {
            if (double.IsNegativeInfinity(_outcomeQuietSince)) _outcomeQuietSince = PresentationTime;
        }
        else _outcomeQuietSince = double.NegativeInfinity;

        if (Phase == Stage.Generating)
        {
            Generation?.Advance(KabloomTuning.GenerationAttemptsPerStep);
            if (Generation?.Accepted is { } accepted) AcceptGeneratedBoard(accepted);
            return;
        }

        // Dev only (ArcadeDebug.LevelSkip): the bumpers step the campaign a level either way, clamped by LoadLevel.
        // ⚠ Not while the bees-per-petal card is up: a skip would load the next level and clear the card in the
        // same frame, so the threshold would never be seen. Dismiss it with ✕ first.
        if (ArcadeDebug.LevelSkip && input.SkipPressed && _noticeCapacity == 0)
        {
            LoadLevel(CurrentLevel + 1, preserveRunScore: true, animate: true);
            return;
        }
        // Any deliberate press cuts the level-change flip short rather than being queued behind it.
        if (Transitioning && (input.CrossPressed || input.SquarePressed || input.DPadPressed != 0))
            _transitionStart = double.NegativeInfinity;

        if (Phase is Stage.AwaitingFirstReveal or Stage.Playing) UpdateNavigation(input, dt);

        switch (Phase)
        {
            case Stage.AwaitingFirstReveal:
                if (_noticeCapacity != 0)
                {
                    // The card swallows every press; ✕ dismisses it only once it has been up long enough to read,
                    // and that press does not also open the board.
                    if (input.CrossPressed && CapacityNoticeReady) { _noticeSeenCapacity = _noticeCapacity; _noticeCapacity = 0; }
                    break;
                }
                if (input.SquarePressed) Act(new KabloomActionResult(KabloomActionKind.Guarded));
                if (input.CrossPressed) BeginGeneration();
                break;

            case Stage.Playing:
                ActiveTime += dt;
                if (RevealCascadeActive) { _squareHeldSince = double.NegativeInfinity; break; }
                // □ press steps the flag count on release; a hold drops the question mark instead, once, and the
                // release that follows does nothing. Both act on the cell the press landed on.
                if (input.SquarePressed) { _squareHeldSince = PresentationTime; _squareCell = FocusCell; _squareSpent = false; }
                if (input.SquareDown && !_squareSpent && double.IsFinite(_squareHeldSince)
                    && PresentationTime - _squareHeldSince >= KabloomTuning.QuestionHoldSeconds)
                {
                    _squareSpent = true;
                    Act(Board.ToggleQuestion(_squareCell), _squareCell);
                }
                if (!input.SquareDown && double.IsFinite(_squareHeldSince))
                {
                    if (!_squareSpent) Act(Board.CycleFlag(_squareCell), _squareCell);
                    _squareHeldSince = double.NegativeInfinity;
                }
                else if (input.CrossPressed)
                    Act(Board.IsRevealed(FocusCell) ? Board.Chord(FocusCell) : Board.Reveal(FocusCell));
                break;

            case Stage.Failed:
                // Gated on the same flag the card draws its prompts from, so the board never obeys a button
                // it is not yet offering. See OutcomePromptsReady.
                if (OutcomePromptsReady && (input.CrossPressed || input.SquarePressed)) FallBackAfterMine();
                break;

            case Stage.Cleared:
                // No confirmation card and no replay offer: the level ends when the board finishes clearing
                // itself. The wait is for the presentation, not for the player.
                if (PresentationActive || PhaseTime < KabloomTuning.CompletionInputDelaySeconds)
                {
                    _clearedIdleSince = double.NegativeInfinity;
                    break;
                }
                // Measured from when the presentation went quiet, not from when the board cleared: the
                // completion's length varies with how many bees and gems the level had.
                if (double.IsNegativeInfinity(_clearedIdleSince)) _clearedIdleSince = PresentationTime;
                if (PresentationTime - _clearedIdleSince >= KabloomTuning.CompletionHoldSeconds) AdvanceLevel();
                break;

            case Stage.CampaignComplete:
                if (OutcomePromptsReady && input.SquarePressed) ReplayCurrentLevel();
                break;
        }
    }

    private void BeginGeneration()
    {
        if (Board.Phase != KabloomBoardPhase.Unplanted) return;
        // Baked levels plant at once: the board is one the bake certified as no-guess from this cell. A level
        // with no bake, or a bake that no longer fits the crop, searches at play time as the early levels do.
        if (KabloomBakedBoards.TryPick(CurrentLevel, Grid.Cells.Count, FocusCell, CampaignSeed) is { } pick
            && BakedFitsCrop(pick.Bees))
        {
            Board.Plant(pick.Bees, FocusCell, pick.Capacity);
            // The bake already certified this board; re-solving it here is a full tier-5 pass on the UI thread
            // for a report nothing in the app reads. Only the probe asks for it.
            AcceptedSolve = VerifyBakedBoards ? KabloomSolver.Solve(Grid, pick.Bees, FocusCell, 5, pick.Capacity) : null;
            Generation = null;
            SetPhase(Stage.Playing);
            Act(Board.Reveal(FocusCell));
            return;
        }
        Generation = new KabloomGenerationSession(Grid, Profile, FocusCell, CampaignSeed);
        SetPhase(Stage.Generating);
    }

    /// <summary>Probe-only: re-certify every baked board on the live crop and publish the report on
    /// <see cref="AcceptedSolve"/>. Off in the app — the bake is the certificate.</summary>
    public static bool VerifyBakedBoards;

    /// <summary>A bake is keyed to the crop by cell count alone, and a crop change that re-labels cells without
    /// changing their count maps the mines onto different cells; <c>Plant</c> would then throw out of
    /// <c>Step</c> and close the arcade. Check the one thing it asserts — first-cell protection — and fall
    /// through to the runtime generator instead, so a stale bake degrades to slow, never to wrong.</summary>
    private bool BakedFitsCrop(byte[] baked)
    {
        if (baked.Length != Grid.Cells.Count || FocusCell < 0 || FocusCell >= baked.Length) return false;
        if (baked[FocusCell] > 0) return false;
        foreach (int n in Grid.Cells[FocusCell].Neighbors)
            if (n >= 0 && n < baked.Length && baked[n] > 0) return false;
        return true;
    }

    private void AcceptGeneratedBoard(KabloomGenerationCandidate candidate)
    {
        Board.Plant(Enumerable.Range(0, candidate.Mines.Length).Where(i => candidate.Mines[i]), FocusCell);
        AcceptedSolve = candidate.Report;
        Generation = null;
        SetPhase(Stage.Playing);
        Act(Board.Reveal(FocusCell));
    }

    private void FallBackAfterMine(bool animate = true)
    {
        int fallbackLevel = Math.Max(1, CurrentLevel - 1);
        DiamondScore = _levelEntryScores[fallbackLevel];
        RerollCampaignSeed();
        if (animate) _cues |= Cue.Continue;
        LoadLevel(fallbackLevel, preserveRunScore: true, animate);
    }

    /// <summary>Dismissed on an outcome card: take the step the card was waiting for, with no transition and
    /// no cue, so the frozen board is the settled one the player lands on. A stung board falls back, a cleared
    /// one advances, the finished campaign reopens its last field — each what the card's own button does.
    /// ⚠ Cues are cleared outright: anything the outcome raised would otherwise sound on resume.</summary>
    public void AcknowledgeOutcome()
    {
        switch (Phase)
        {
            case Stage.Failed:           FallBackAfterMine(animate: false); break;
            case Stage.Cleared:          LoadLevel(CurrentLevel + 1, preserveRunScore: true); break;
            case Stage.CampaignComplete: DiamondScore = _scoreAtLevelStart; LoadLevel(CurrentLevel, preserveRunScore: true); break;
            default: return;
        }
        _cues = Cue.None;
    }

    private void Act(KabloomActionResult result, int? actedCell = null)
    {
        int cell = actedCell ?? FocusCell;
        if (result.ChangedCells > 0) QueueRevealCascade(cell);
        LastCascadeSize = result.ChangedCells;
        if (result.Kind is KabloomActionKind.Flagged or KabloomActionKind.Marked) _flagStarts[cell] = PresentationTime;
        else if (result.Kind is KabloomActionKind.Unflagged or KabloomActionKind.Unmarked) _flagStarts.Remove(cell);
        LastAction = result.Kind;
        ActionPulse = 1;
        _cues |= result.Kind switch
        {
            KabloomActionKind.Revealed => result.ChangedCells > 1 ? Cue.Bloom : Cue.Reveal,
            KabloomActionKind.Chorded => result.ChangedCells > 1 ? Cue.Bloom : Cue.Reveal,
            KabloomActionKind.Flagged or KabloomActionKind.Marked => Cue.Flag,
            KabloomActionKind.Unflagged or KabloomActionKind.Unmarked => Cue.Unflag,
            KabloomActionKind.Failed => Cue.Mine,
            KabloomActionKind.Cleared => Cue.Cleared,
            _ => Cue.Guarded,
        };

        if (Board.Phase == KabloomBoardPhase.Failed)
        {
            _hitBeeCell = FocusCell;
            // A beat after the reveal, not with it, so the bee is visible in the opened cell before it exits.
            _hitBeeStart = PresentationTime + 0.45;
            _beeDepartCued = false;
            SetPhase(Stage.Failed);
        }
        else if (Board.Phase == KabloomBoardPhase.Cleared)
        {
            ScheduleMineFinale(LatestRevealEnd() + 0.08);
            HighestClearedLevel = Math.Max(HighestClearedLevel, CurrentLevel);
            SetPhase(CurrentLevel >= CampaignLevels ? Stage.CampaignComplete : Stage.Cleared);
            if (CurrentLevel >= CampaignLevels) _cues |= Cue.CampaignComplete;
        }
    }

    private void AdvanceLevel()
    {
        if (CurrentLevel >= CampaignLevels) { _cues |= Cue.CampaignComplete; SetPhase(Stage.CampaignComplete); return; }
        _cues |= Cue.LevelUp;
        LoadLevel(CurrentLevel + 1, preserveRunScore: true, animate: true);
    }

    private void ReplayCurrentLevel()
    {
        DiamondScore = _scoreAtLevelStart;
        LoadLevel(CurrentLevel, preserveRunScore: true, animate: true);
    }

    /// <summary><paramref name="animate"/> drives the level-change transition. Off for the constructor and
    /// for <see cref="Restore"/>: opening the arcade is not a level change.</summary>
    private void LoadLevel(int level, bool preserveRunScore = false, bool animate = false)
    {
        if (!preserveRunScore) DiamondScore = 0;
        _scoreAtLevelStart = DiamondScore;
        // Compared against the outgoing profile, so the notice reports an actual change. The two axes move on
        // their own schedules and either can move alone, so they're tested separately.
        KabloomLevelProfile? previous = Profile;
        CurrentLevel = Math.Clamp(level, 1, CampaignLevels);
        _levelEntryScores[CurrentLevel] = DiamondScore;
        Profile = KabloomLevelProfile.ForLevel(CurrentLevel);
        Grid = KabloomGrid.Create(Profile.TargetCells);
        Board = new KabloomBoard(Grid);
        FocusCell = Grid.Cells.MinBy(c => c.Center.Length)!.Id;
        SyncPointerToFocus();
        Generation = null;
        AcceptedSolve = null;
        ActiveTime = 0;
        LastAction = KabloomActionKind.None;
        ActionPulse = 0;
        ResetPresentationState();
        // ⚠ Must come after ResetPresentationState, and be zero rather than PresentationTime: that call
        // rewinds PresentationTime to 0, so arming from the outgoing clock makes TransitionElapsed negative
        // and the renderer's `>= 0` guard never fires.
        _transitionStart = animate ? 0 : double.NegativeInfinity;
        _growthCells = animate && previous is not null && Profile.TargetCells > previous.TargetCells;
        _growthMines = animate && previous is not null && Profile.MineOccupancy > previous.MineOccupancy + 1e-9;
        _growthStart = _growthCells || _growthMines ? 0 : double.NegativeInfinity;
        if (_growthCells || _growthMines) _cues |= Cue.Growth;
        SetPhase(Stage.AwaitingFirstReveal);
        // Announce the capacity whenever a level raises it over the level before; the card is locked for a
        // reading period only the first time this run meets that capacity, so a fall-back and climb shows the
        // reminder without the wait.
        int previousCapacity = previous?.BeeCapacity ?? 1;
        if (Profile.BeeCapacity > previousCapacity)
        {
            _noticeCapacity = Profile.BeeCapacity;
            _noticeStart = 0;
            _noticeLocked = Profile.BeeCapacity > _noticeSeenCapacity;
        }
        else _noticeCapacity = 0;
    }

    private void RerollCampaignSeed()
    {
        ulong value = CampaignSeed;
        value ^= value >> 12; value ^= value << 25; value ^= value >> 27;
        CampaignSeed = value * 0x2545F4914F6CDD1DUL;
        if (CampaignSeed == 0) CampaignSeed = 0x4B41424C4F4F4D21UL;
    }

    private void UpdateNavigation(in ArcadeInput input, double dt)
    {
        // The d-pad moves the cursor by neighbour. It arrives as edges, so each press is exactly one step —
        // no repeat timer, no deadzone, no arming.
        //
        // ⚠ Handled before the stick and returning early, so a thumb resting on the stick can't fight a
        // deliberate d-pad tap. Diagonals raise both directions and MoveFocus takes the combined vector.
        if (input.DPadPressed != 0)
        {
            double dx = (input.DPadRight ? 1 : 0) - (input.DPadLeft ? 1 : 0);
            double dy = (input.DPadDown  ? 1 : 0) - (input.DPadUp   ? 1 : 0);
            if (dx != 0 || dy != 0)
            {
                double length = new Vec2(dx, dy).Length;
                MoveFocus(dx / length, dy / length);
                // Bring the reticle along, or the next stick touch yanks focus back and undoes the d-pad step.
                SyncPointerToFocus();
                return;
            }
        }

        // The stick is a pointer, not a second d-pad: it drives a free reticle over the board and whatever
        // cell sits under it is focused, so the far side of a 240-cell crop is one motion away. Don't turn it
        // back into stepping — the d-pad above already answers "that way, exactly once".
        double magnitude = input.Magnitude;
        if (magnitude < KabloomTuning.PointerDeadzone) return;

        // Past the deadzone, tilt is proportional speed, so a nudge creeps and a full push sprints.
        double drive = Math.Min(1, (magnitude - KabloomTuning.PointerDeadzone)
                                   / Math.Max(0.01, 1 - KabloomTuning.PointerDeadzone));
        // Slower by level: level 1 runs at full speed, the last at PointerFinalLevelSpeedFraction of it.
        double levelT = CampaignLevels <= 1 ? 0 : (CurrentLevel - 1) / (double)(CampaignLevels - 1);
        double pace = 1 + (KabloomTuning.PointerFinalLevelSpeedFraction - 1) * Math.Clamp(levelT, 0, 1);
        double speed = KabloomTuning.PointerRadiiPerSecond * pace * Grid.Radius * drive * dt / magnitude;
        PointerX += input.StickX * speed;
        PointerY += input.StickY * speed;

        // Clamp to the board radius so the reticle never sits where no cell is and focus can't go stale.
        double reach = Math.Sqrt(PointerX * PointerX + PointerY * PointerY);
        double bound = Grid.Radius * KabloomTuning.PointerReachFraction;
        if (reach > bound && reach > 1e-9)
        {
            PointerX = PointerX / reach * bound;
            PointerY = PointerY / reach * bound;
        }
        FocusNearestToPointer();
    }

    /// <summary>Focus whatever cell the reticle is over. Nearest centre rather than a polygon hit test, which
    /// can never return "no cell" when the pointer lands on a seam.</summary>
    private void FocusNearestToPointer()
    {
        int best = -1;
        double bestDistance = double.PositiveInfinity;
        for (int i = 0; i < Grid.Cells.Count; i++)
        {
            Vec2 centre = Grid.Cells[i].Center;
            double dx = centre.X - PointerX, dy = centre.Y - PointerY;
            double distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        if (best < 0 || best == FocusCell) return;
        FocusCell = best;
        ActionPulse = 0.45;
        _cues |= Cue.Focus;
    }

    /// <summary>Park the reticle on the focused cell. Must be called whenever focus moves by any means other
    /// than the pointer — d-pad, level load, restore — so picking the stick back up continues from where the
    /// cursor visibly is.</summary>
    private void SyncPointerToFocus()
    {
        if (Grid is null || FocusCell < 0 || FocusCell >= Grid.Cells.Count) return;
        PointerX = Grid.Cells[FocusCell].Center.X;
        PointerY = Grid.Cells[FocusCell].Center.Y;
    }

    private void MoveFocus(double dx, double dy)
    {
        KabloomCell current = Grid.Cells[FocusCell];
        int best = FindDirectional(current.Neighbors, current.Center, dx, dy, requireStrongAlignment: false);
        if (best < 0)
            best = FindDirectional(Grid.Cells.Select(c => c.Id).Where(id => id != FocusCell),
                current.Center, dx, dy, requireStrongAlignment: true);
        if (best < 0 || best == FocusCell) return;
        FocusCell = best;
        ActionPulse = 0.45;
        _cues |= Cue.Focus;
    }

    private int FindDirectional(IEnumerable<int> candidates, Vec2 from, double dx, double dy,
                                bool requireStrongAlignment)
    {
        int best = -1;
        double bestScore = double.NegativeInfinity;
        foreach (int id in candidates)
        {
            Vec2 delta = Grid.Cells[id].Center - from;
            double distance = delta.Length;
            if (distance <= 1e-8) continue;
            double alignment = (delta.X * dx + delta.Y * dy) / distance;
            if (alignment <= (requireStrongAlignment ? 0.42 : 0.05)) continue;
            double score = alignment * 8.0 - distance / Math.Max(1, Grid.Radius);
            if (score > bestScore) { bestScore = score; best = id; }
        }
        return best;
    }

    private void SetPhase(Stage phase) { Phase = phase; PhaseTime = 0; }

    private void ResetPresentationState(bool settleExisting = false)
    {
        PresentationTime = 0;
        // A restored failure screen shows the bee sitting still rather than replaying its exit.
        _hitBeeCell = -1;
        _hitBeeStart = double.NegativeInfinity;
        _clearedIdleSince = double.NegativeInfinity;
        _outcomeQuietSince = double.NegativeInfinity;
        _revealStarts.Clear();
        _flagStarts.Clear();
        _knownRevealed = new bool[Grid.Cells.Count];
        _mineClearStarts = Enumerable.Repeat(double.PositiveInfinity, Grid.Cells.Count).ToArray();
        _diamondStarts = Enumerable.Repeat(double.PositiveInfinity, Grid.CenterCaps.Count).ToArray();
        _diamondCounted = new bool[Grid.CenterCaps.Count];
        if (!settleExisting) return;
        for (int i = 0; i < _knownRevealed.Length; i++)
        {
            _knownRevealed[i] = Board.IsRevealed(i);
            if (Board.IsFlagged(i) || Board.IsMarked(i)) _flagStarts[i] = -KabloomTuning.FlagPopSeconds;
            if (Board.Phase == KabloomBoardPhase.Cleared && Board.IsMine(i))
                _mineClearStarts[i] = -KabloomTuning.MineFinaleSeconds;
        }
    }

    private void QueueRevealCascade(int origin)
    {
        if (_knownRevealed.Length != Grid.Cells.Count) _knownRevealed = new bool[Grid.Cells.Count];
        int[] fresh = Enumerable.Range(0, Grid.Cells.Count)
            .Where(id => Board.IsRevealed(id) && !_knownRevealed[id]).ToArray();
        if (fresh.Length == 0) return;

        var freshSet = fresh.ToHashSet();
        int[] distance = Enumerable.Repeat(-1, Grid.Cells.Count).ToArray();
        var queue = new Queue<int>();
        origin = Math.Clamp(origin, 0, Grid.Cells.Count - 1);
        distance[origin] = 0;
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            foreach (int neighbor in Grid.Cells[cell].Neighbors)
            {
                if (distance[neighbor] >= 0 || !freshSet.Contains(neighbor)) continue;
                distance[neighbor] = distance[cell] + 1;
                queue.Enqueue(neighbor);
            }
        }

        foreach (int cell in fresh)
        {
            int hops = Math.Max(0, distance[cell]);
            double jitter = HashUnit(cell, origin) * KabloomTuning.RevealHopDelaySeconds * 0.28;
            double delay = Math.Min(KabloomTuning.RevealMaximumDelaySeconds,
                hops * KabloomTuning.RevealHopDelaySeconds + jitter);
            _revealStarts[cell] = PresentationTime + delay;
            _knownRevealed[cell] = true;
        }
    }

    private double LatestRevealEnd() => _revealStarts.Count == 0 ? PresentationTime
        : _revealStarts.Values.Max() + KabloomTuning.RevealBurstSeconds;

    private void ScheduleMineFinale(double start)
    {
        int index = 0;
        foreach (int mine in Enumerable.Range(0, Grid.Cells.Count).Where(Board.IsMine)
                     .OrderBy(id => Grid.Cells[id].Center.Length).ThenBy(id => id))
        {
            double delay = Math.Min(0.55, index * KabloomTuning.MineFinaleStaggerSeconds);
            _mineClearStarts[mine] = start + delay;
            index++;
        }
    }

    private void UpdateCollectibles()
    {
        ScheduleAvailableDiamonds();
        for (int diamond = 0; diamond < _diamondStarts.Length; diamond++)
        {
            if (_diamondCounted[diamond] || !double.IsFinite(_diamondStarts[diamond])) continue;
            // Swell then flight — the same two spans DiamondSwell/DiamondProgress divide the wait into, so
            // the counter ticks exactly as the stone lands rather than a second early.
            if (PresentationTime < _diamondStarts[diamond]
                + KabloomTuning.DiamondSwellSeconds + KabloomTuning.DiamondFlightSeconds) continue;
            _diamondCounted[diamond] = true;
            DiamondScore++;
            _cues |= Cue.Diamond;
            if (DiamondScore > HighScore)
            {
                HighScore = DiamondScore;
                // Announced on the gem that first passes the old best, then never again this run: the best
                // climbs with every gem after it, and a fanfare per gem would drown the counter it celebrates.
                if (!_bestBeaten) { _bestBeaten = true; _cues |= Cue.NewBest; }
            }
        }
    }

    private void ScheduleAvailableDiamonds()
    {
        for (int diamond = 0; diamond < Grid.CenterCaps.Count; diamond++)
        {
            if (_diamondCounted[diamond] || double.IsFinite(_diamondStarts[diamond])) continue;
            double ready = PresentationTime;
            bool allClear = true;
            foreach (int cell in Grid.CenterCaps[diamond].TouchingCells)
            {
                double completion;
                if (Board.IsMine(cell))
                {
                    double mineStart = _mineClearStarts[cell];
                    if (!double.IsFinite(mineStart)) { allClear = false; break; }
                    completion = mineStart + KabloomTuning.MineFinaleSeconds;
                }
                else
                {
                    if (!Board.IsRevealed(cell)) { allClear = false; break; }
                    completion = _revealStarts.TryGetValue(cell, out double revealStart)
                        ? revealStart + KabloomTuning.RevealBurstSeconds : PresentationTime;
                }
                ready = Math.Max(ready, completion);
            }
            if (!allClear) continue;
            double ripple = (diamond % 4) * 0.022;
            _diamondStarts[diamond] = ready + KabloomTuning.DiamondReleaseDelaySeconds + ripple;
        }
    }

    private static double HashUnit(int a, int b)
    {
        uint value = (uint)(a * 0x45D9F3B) ^ (uint)(b * 0x119DE1F3) ^ 0x9E3779B9u;
        value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15;
        return (value & 0xFFFF) / 65535.0;
    }

    /// <summary>V4 writes bee and flag counts (<c>Bees</c>, <c>Flags</c>, <c>Capacity</c>). V3 saves carry the
    /// bool arrays <c>Mines</c>/<c>Flagged</c>; they are folded into counts on read (capacity 1) and the next
    /// save writes V4. Both shapes stay on the record so either deserialises.</summary>
    private sealed record Snap(
        int V, int Level, int HighestCleared, int High, int Score, int ScoreAtLevelStart,
        ulong CampaignSeed, int Stage, double PhaseTime, double ActiveTime, int Focus, int BoardPhase,
        int FirstCell, bool[]? Mines, bool[] Revealed, bool[]? Flagged, bool[] Diamonds, int[] LevelEntryScores,
        bool[]? Marked = null, int Capacity = 1, byte[]? Bees = null, byte[]? Flags = null,
        int NoticeSeenCapacity = 1, int NoticeCapacity = 0, bool NoticeLocked = false);

    private const int SnapVersion = 4;
    private const int LegacySnapVersion = 3;

    private static readonly JsonSerializerOptions SnapJson = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public string Serialize() => JsonSerializer.Serialize(new Snap(
        SnapVersion, CurrentLevel, HighestClearedLevel, HighScore, DiamondScore, _scoreAtLevelStart,
        CampaignSeed, (int)Phase, PhaseTime,
        ActiveTime, FocusCell, (int)Board.Phase, Board.FirstCell,
        null, [.. Board.Revealed], null, [.. _diamondCounted],
        [.. _levelEntryScores], [.. Board.Marked], Board.Capacity, [.. Board.Bees], [.. Board.Flags],
        _noticeSeenCapacity, _noticeCapacity, _noticeLocked), SnapJson);

    public void Restore(string json)
    {
        // ⚠ A structural rejection below is a bare `return`, and it must still leave a fresh game — see the
        // same committed/finally guard in Connate.Restore for why the caller can't be trusted to supply one.
        bool committed = false;
        try
        {
            Snap? snap = JsonSerializer.Deserialize<Snap>(json);
            if (snap is null || (snap.V != SnapVersion && snap.V != LegacySnapVersion)) return;
            // A legacy save holds one bee per mined cell and one flag per flagged cell.
            byte[] bees = snap.Bees ?? (snap.Mines ?? []).Select(m => m ? (byte)1 : (byte)0).ToArray();
            byte[] flags = snap.Flags ?? (snap.Flagged ?? []).Select(f => f ? (byte)1 : (byte)0).ToArray();
            int capacity = snap.V == LegacySnapVersion ? 1 : snap.Capacity;
            int level = Math.Clamp(snap.Level, 1, CampaignLevels);
            KabloomLevelProfile profile = KabloomLevelProfile.ForLevel(level);
            KabloomGrid grid = KabloomGrid.Create(profile.TargetCells);
            var board = new KabloomBoard(grid);
            if (snap.Diamonds is null || snap.Diamonds.Length != grid.CenterCaps.Count) return;
            // ⚠ Length must track the array, never a literal: Restore discards a bad save without complaint,
            // so a stale literal here rejects every snapshot with no symptom but a campaign that won't resume.
            if (snap.LevelEntryScores is null || snap.LevelEntryScores.Length != _levelEntryScores.Length
                || snap.LevelEntryScores.Any(score => score < 0)) return;
            if (snap.Score < 0 || snap.ScoreAtLevelStart < 0 || snap.ScoreAtLevelStart > snap.Score
                || snap.Score - snap.ScoreAtLevelStart != snap.Diamonds.Count(value => value)) return;
            if (snap.BoardPhase < 0 || snap.BoardPhase > (int)KabloomBoardPhase.Failed) return;
            var boardPhase = (KabloomBoardPhase)snap.BoardPhase;
            if (!board.RestoreState(bees, snap.Revealed ?? [], flags, snap.Marked, snap.FirstCell, boardPhase, capacity))
                return;

            CurrentLevel = level;
            Profile = profile;
            Grid = grid;
            Board = board;
            HighestClearedLevel = Math.Clamp(snap.HighestCleared, 0, CampaignLevels);
            DiamondScore = snap.Score;
            _scoreAtLevelStart = snap.ScoreAtLevelStart;
            _levelEntryScores = [.. snap.LevelEntryScores];
            HighScore = Math.Max(Math.Max(0, snap.High), DiamondScore);
            // A run restored already standing at the best has had its moment; one below it still has it coming.
            _bestBeaten = DiamondScore > 0 && DiamondScore >= Math.Max(0, snap.High);
            CampaignSeed = snap.CampaignSeed == 0 ? CampaignSeed : snap.CampaignSeed;
            FocusCell = Math.Clamp(snap.Focus, 0, Grid.Cells.Count - 1);
            SyncPointerToFocus();
            PhaseTime = Finite(snap.PhaseTime, 0, 3600);
            _noticeSeenCapacity = Math.Clamp(snap.NoticeSeenCapacity, 1, 3);
            _noticeCapacity = snap.NoticeCapacity is >= 2 and <= 3 && Phase == Stage.AwaitingFirstReveal ? snap.NoticeCapacity : 0;
            _noticeLocked = _noticeCapacity != 0 && snap.NoticeLocked;
            // A frozen card's reading period starts over on the rewound presentation clock: never dismissable on the first frame back.
            _noticeStart = 0;
            ActiveTime = Finite(snap.ActiveTime, 0, 365 * 24 * 3600);
            AcceptedSolve = null;
            Generation = null;
            ResetPresentationState(settleExisting: true);
            snap.Diamonds.CopyTo(_diamondCounted, 0);

            Stage requested = snap.Stage is >= 0 and <= (int)Stage.CampaignComplete
                ? (Stage)snap.Stage : Stage.AwaitingFirstReveal;
            Phase = boardPhase switch
            {
                KabloomBoardPhase.Unplanted => Stage.AwaitingFirstReveal,
                KabloomBoardPhase.Playing => Stage.Playing,
                KabloomBoardPhase.Failed => Stage.Failed,
                KabloomBoardPhase.Cleared when level >= CampaignLevels => Stage.CampaignComplete,
                KabloomBoardPhase.Cleared => Stage.Cleared,
                _ => requested,
            };
            committed = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] Kabloom snapshot discarded ({ex.Message})");
        }
        finally
        {
            if (!committed) LoadLevel(1);
        }
    }

    private static double Finite(double value, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
}
