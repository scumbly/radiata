using System.Diagnostics;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>Petalpop: a polygonal brick-clearer. One paddle rides the inward-bowing arc of each side, one
/// stick moves them all toward the direction pushed, and the flower of petals in the middle is popped down
/// to its gold core. A run climbs through stages of increasing side count — a square with four levels, a
/// pentagon with five, up to an octagon with eight — and beating 8-8 wins. Pure sim: no WPF, clock, audio
/// or filesystem; <c>PetalPopRenderer</c> reads the public state and draws.
///
/// <para>Every animation clock lives here (twist, punch, squash, shards, trails), advanced by
/// <see cref="Step"/>, so pause, the ready beat and freeze/resume hold the picture still for free and the
/// renderer stays a pure function of the state it is handed.</para></summary>
public sealed class PetalPop : IArcadeGame
{
    // ⚠ Saved slices and the high-score store key on this id. It must not follow a display rename.
    public const string GameId = "petalpop";
    public string Id => GameId;
    public string Title => "Petalpop";

    public enum Stage { Intro, Serve, Playing, BallLost, LevelClear, GameOver, Won }

    [Flags]
    public enum Cue
    {
        None = 0, Serve = 1, PaddleHit = 2, Brick = 4, BrickTough = 8, Core = 16, Powerup = 32, Split = 64,
        Smash = 128, SmashArm = 256, Lost = 512, LifeLost = 1024, LevelClear = 2048, GameOver = 4096,
        NewBest = 8192, ComboShout = 16384, Win = 32768, ExtraLife = 65536,
        /// <summary>A corner bumper (the square stage only) threw the ball back — furniture, not a paddle.</summary>
        Bumper = 131072,
        /// <summary>A cold ball rang off the core instead of breaking it.</summary>
        CorePing = 262144,
    }

    /// <summary>⚠ There is no Smash. A slam is felt, not captioned — see the burst site
    /// in <c>Collide</c>. The member is gone rather than merely unused so it cannot quietly come back.</summary>
    public enum ShoutKind { None, Combo, Level, ExtraLife }

    // ── Game state (serialized) ───────────────────────────────────────────────
    public Stage Phase { get; private set; }
    public double PhaseTime { get; private set; }
    /// <summary>The stage: how many sides (and paddles) the board has. <see cref="PetalPopTuning.SidesStart"/> .. <see cref="PetalPopTuning.SidesEnd"/>.</summary>
    public int Sides { get; private set; } = 4;
    /// <summary>Level within the stage, 1 .. <see cref="Sides"/>.</summary>
    public int Level { get; private set; } = 1;
    public int Lives { get; private set; } = PetalPopTuning.PlayerLives;
    public long Score { get; private set; }
    public int HighScore { get; private set; }
    /// <summary>Petals popped since the last paddle touch.</summary>
    public int Combo { get; private set; }
    /// <summary>Seconds of hit-stop left. Serialized so a snapshot mid-impact resumes mid-impact.</summary>
    public double HitStopLeft { get; private set; }
    public double SmashCooldownLeft { get; private set; }
    /// <summary>The angle a served ball will leave the paddle's normal at, drawn when the ball is served so
    /// the aim can be shown before launch. See <see cref="ServeAim"/>.</summary>
    public double ServeAngle { get; private set; }
    /// <summary>0 = spring, 1 = rail. A setting (per game, persisted beside the high score), not run state.</summary>
    public int ControlMode { get; private set; }

    private int[] _hp = [];
    private PetalPopPaddle[] _paddles = [];
    private readonly List<PetalPopBall> _balls = [];
    private ulong _rng = Seed;
    private const ulong Seed = 0x506574616C506F70UL;   // "PetalPop"
    private bool _bestBeaten;
    private IPetalPopPaddleControl _control;

    // ── Presentation state (transient, never serialized) ─────────────────────
    public double PresentationTime { get; private set; }
    /// <summary>Rotational kick of the whole disc, radians. Decays to exactly 0.</summary>
    public double DiscTwist { get; private set; }
    /// <summary>Scale kick of the whole disc. Decays to exactly 1.</summary>
    public double DiscPunch { get; private set; } = 1;
    public double ScorePulse { get; private set; }
    /// <summary>1 the instant a cold ball rang the core, decaying to 0: the core's bell wobble. Presentation
    /// only — the collider is the same circle whether it is ringing or still.</summary>
    public double CoreRing { get; private set; }
    public double SmashArmedLeft { get; private set; }
    /// <summary>How far the paddles currently stand off their rails (the smash surge). Feeds collision and
    /// drawing, so the slam connects with what it looks like it connects with.</summary>
    public double Lunge { get; private set; }
    /// <summary>How far the slingshot is drawn, 0..1, while ✕ is held.</summary>
    public double Draw { get; private set; }
    /// <summary>+Punch the windup under way right now would add, or 0 when there is no smash in it yet — what
    /// the paddles' arming glow steps through, so the player can see the charge click up.</summary>
    public int SmashCharge => Draw >= PetalPopTuning.MinDraw ? PunchForDraw(Draw) : 0;

    /// <summary>+Punch a smash released at <paramref name="draw"/> adds: even steps up to
    /// <see cref="PetalPopTuning.SmashPunch"/>, so a half draw adds one and a full draw adds two.</summary>
    public static int PunchForDraw(double draw)
    {
        int max = Math.Clamp(PetalPopTuning.SmashPunch, 1, 4);
        // Tiers land ON the fractions: worth k at draw (k-1)/(max-1), so k = max needs a saturated draw.
        return Math.Clamp(1 + (int)(Math.Clamp(draw, 0, 1) * (max - 1) + 1e-9), 1, max);
    }

    private bool _drawing;
    private bool _springing;
    private double _slamAmplitude;
    /// <summary>The draw the last release let go at, so the hit it arms knows what it is worth.</summary>
    private double _slamCharge;
    public ShoutKind Shout { get; private set; }
    public double ShoutLeft { get; private set; }
    public int ShoutValue { get; private set; }
    public int ShoutValue2 { get; private set; }
    /// <summary>The side whose gutter the last lost ball fell into. −1 until one does.</summary>
    public int LastLostSide { get; private set; } = -1;
    public double LivesFlash { get; private set; }
    /// <summary>A life was just gained: the pips swell rather than flicker.</summary>
    public double LivesGain { get; private set; }
    private readonly List<PetalPopShard> _shards = [];
    private readonly List<PetalPopCoreShard> _coreShards = [];
    private readonly List<PetalPopBurst> _bursts = [];
    private readonly List<PetalPopBall> _pending = [];
    private Cue _cues;

    // ── Public views ──────────────────────────────────────────────────────────
    public PetalPopLayout Layout => PetalPopLayout.For(Sides, PaddleHalfLength);
    /// <summary>This level's paddle half-length: wide on the first square, narrow on the last octagon.</summary>
    public double PaddleHalfLength => PaddleHalfLengthFor(LevelOrdinal);
    public static double PaddleHalfLengthFor(int ordinal)
    {
        int last = Ordinal(Math.Max(PetalPopTuning.SidesStart, PetalPopTuning.SidesEnd), Math.Max(PetalPopTuning.SidesStart, PetalPopTuning.SidesEnd));
        double t = last <= 1 ? 1 : Math.Clamp((ordinal - 1.0) / (last - 1.0), 0, 1);
        return PetalPopTuning.PaddleHalfLengthFirst + (PetalPopTuning.PaddleHalfLengthLast - PetalPopTuning.PaddleHalfLengthFirst) * t;
    }
    /// <summary>This level's flower: rings, petal shapes and hit counts. Replaced by <see cref="FillBricks"/>.</summary>
    public PetalPopFlower Flower { get; private set; }
    /// <summary>The paddle that serves: the one on the bottom side.</summary>
    public int ServeSide => Layout.ServeSide;
    public IReadOnlyList<PetalPopPaddle> Paddles => _paddles;
    public IReadOnlyList<PetalPopBall> Balls => _balls;
    public IReadOnlyList<PetalPopShard> Shards => _shards;
    public IReadOnlyList<PetalPopCoreShard> CoreShards => _coreShards;
    public IReadOnlyList<PetalPopBurst> Bursts => _bursts;
    public IReadOnlyList<int> Hp => _hp;
    public bool IsGameOver => Phase is Stage.GameOver or Stage.Won;
    public bool SmashArmed => SmashArmedLeft > 0;
    /// <summary>The core is reachable once any petal touching it is gone.</summary>
    public bool CoreExposed
    {
        get
        {
            int ring0 = Sides * Flower.CountPerSide[0];
            for (int m = 0; m < ring0 && m < _hp.Length; m++) if (_hp[m] <= 0) return true;
            return false;
        }
    }
    /// <summary>True while the level-clear interlude is a change of shape rather than another level on the
    /// same polygon. It picks the longer interlude and drives the whole-playfield spin.</summary>
    public bool StageChanging { get; private set; }
    /// <summary>How long the current level-clear interlude runs.</summary>
    public double ClearSeconds => Math.Max(1e-6,
        StageChanging ? PetalPopTuning.StageClearSeconds : PetalPopTuning.LevelClearSeconds);
    /// <summary>0..1 through the level-clear transition; 0 outside it.</summary>
    public double LevelClearProgress => Phase == Stage.LevelClear
        ? Math.Clamp(PhaseTime / ClearSeconds, 0, 1) : 0;
    /// <summary>The playfield's rotation this frame, in degrees: one full 360° turn across a shape change,
    /// and exactly 0 at both ends and on every other frame of the game. Presentation only — the sim's
    /// geometry never turns, so a ball's bounce is unchanged.
    ///
    /// <para>Smoothstep applied twice. One pass peaks at 1.5× the average rate and reads as a near-constant
    /// turn; nesting it peaks at 2.25×, so the board visibly winds up, whips through the middle and settles.
    /// It stays symmetric, so half the turn is still done at half time.</para></summary>
    public double StageSpinDegrees
    {
        get
        {
            if (!StageChanging || Phase != Stage.LevelClear) return 0;
            return 360 * ArcadeMath.Smoothstep(ArcadeMath.Smoothstep(LevelClearProgress));
        }
    }
    /// <summary>Sim time scale this step: slow-mo while the last ball flies the gutter, a crawl during a
    /// hit-stop, else 1. Derived, never stored, so it is exactly 1 the instant the timer ends.</summary>
    public double TimeScale => Phase == Stage.BallLost ? PetalPopTuning.BallLostTimeScale
        : HitStopLeft > 0 ? PetalPopTuning.HitStopScale : 1;

    public Cue TakeCues() { Cue c = _cues; _cues = Cue.None; return c; }
    public void SeedHighScore(int high) { if (high > HighScore) HighScore = high; }

    /// <summary>1-based position of (sides, level) in the whole run: the square's four levels, then the
    /// pentagon's five, and so on. Ball speed ramps on this.</summary>
    public static int Ordinal(int sides, int level)
    {
        int start = Math.Max(3, PetalPopTuning.SidesStart);
        int ordinal = 0;
        for (int n = start; n < sides; n++) ordinal += n;
        return ordinal + Math.Max(1, level);
    }
    public int LevelOrdinal => Ordinal(Sides, Level);
    /// <summary>The stage as the player counts it: 1 for the first shape, however many sides it has.</summary>
    public int StageOrdinal => Sides - PetalPopTuning.SidesStart + 1;

    /// <summary>This level's corner bumpers, one convex triangle per corner (none at all on most levels).
    /// Rebuilt with the flower, so a restored game grows them back from its stage and level.</summary>
    public IReadOnlyList<Vec2[]> Bumpers => _bumpers;
    private Vec2[][] _bumpers = [];

    /// <summary>Leg length of a corner bumper as a fraction of the side, for this stage and level; zero means
    /// no bumpers. Only the <see cref="PetalPopTuning.BumperSides"/>-sided stage has them, and they shrink a
    /// step per level.</summary>
    public static double BumperFraction(int sides, int level) =>
        sides != PetalPopTuning.BumperSides ? 0
        : Math.Clamp(PetalPopTuning.BumperFractionFirst - PetalPopTuning.BumperFractionStep * Math.Max(0, level - 1), 0, 0.45);
    public double SpeedCap(int ordinal) => Math.Min(PetalPopTuning.BallSpeedCapMax,
        PetalPopTuning.BallSpeedCapBase + (ordinal - 1) * PetalPopTuning.BallSpeedCapPerLevel);
    public double SpeedBase(int ordinal) => Math.Min(SpeedCap(ordinal),
        PetalPopTuning.BallSpeedBase + (ordinal - 1) * PetalPopTuning.BallSpeedPerLevel);

    public PetalPop()
    {
        ControlMode = PetalPopTuning.ControlMode == 1 ? 1 : 0;
        _control = PetalPopControls.For(ControlMode);
        Flower = PetalPopFlower.For(Sides, Level);
        BeginRun();
    }

    // ── Pause menu ────────────────────────────────────────────────────────────

    public IReadOnlyList<ArcadePauseOption> PauseOptions =>
        [new("control", Loc.T(UiText.Arcade.Control), [Loc.T(UiText.Arcade.Spring), Loc.T(UiText.Arcade.Rail)], ControlMode)];

    public void ApplyPauseOption(string key, int choice)
    {
        if (!string.Equals(key, "control", StringComparison.OrdinalIgnoreCase)) return;
        SetControlMode(choice);
    }

    private void SetControlMode(int mode)
    {
        ControlMode = mode == 1 ? 1 : 0;
        _control = PetalPopControls.For(ControlMode);
        foreach (var p in _paddles) p.Vel = 0;   // the two models don't share speed; carrying it over lurches
    }

    private sealed record SettingsSnap(int Control);
    public string? SerializeSettings() => JsonSerializer.Serialize(new SettingsSnap(ControlMode));
    public void RestoreSettings(string json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<SettingsSnap>(json);
            if (s is not null) SetControlMode(s.Control);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { /* defaults stand */ }
    }

    public ArcadeHowTo? HowTo => new(Loc.T(UiText.Arcade.HowToPlay),
    [
        new(Loc.T(UiText.Arcade.PetalPopHow1), "paddles"),
        new(Loc.T(UiText.Arcade.PetalPopHow2), "gutter"),
        new(Loc.T(UiText.Arcade.PetalPopHow5), "smash"),
        new(Loc.T(UiText.Arcade.PetalPopHow3), "petals"),
        new(Loc.T(UiText.Arcade.PetalPopHow4), "multiball"),
    ]);

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public void Restart() => BeginRun();

    private void BeginRun()
    {
        // A new run from the game-over or won screen goes straight to the serve: the player has just read
        // a card and pressed to go, and the intro would make them press again. The intro is for a game
        // opened fresh.
        bool straightIn = Phase is Stage.GameOver or Stage.Won;
        Sides = Math.Clamp(PetalPopTuning.SidesStart, PetalPopLayout.MinSides, PetalPopLayout.MaxSides);
        Level = 1;
        Lives = Math.Max(1, PetalPopTuning.PlayerLives);
        Score = 0;
        Combo = 0;
        HitStopLeft = 0;
        SmashCooldownLeft = 0;
        _rng = Seed;
        _bestBeaten = false;
        ConfigureSides();
        FillBricks();
        _balls.Clear();
        ResetPresentation();
        if (straightIn) { SetPhase(Stage.Serve); SpawnServeBall(); }
        else SetPhase(Stage.Intro);
    }

    /// <summary>Size the paddle set to the stage's side count, everything centred and still.</summary>
    private void ConfigureSides()
    {
        if (_paddles.Length != Sides)
        {
            _paddles = new PetalPopPaddle[Sides];
            for (int i = 0; i < Sides; i++) _paddles[i] = new PetalPopPaddle();
        }
        else foreach (var p in _paddles) { p.Pos = 0; p.Vel = 0; }
    }

    private void ResetPresentation()
    {
        PresentationTime = 0;
        StageChanging = false;
        DiscTwist = 0;
        DiscPunch = 1;
        ScorePulse = 0;
        CoreRing = 0;
        SmashArmedLeft = 0;
        Lunge = 0;
        Draw = 0;
        _drawing = false;
        _springing = false;
        _slamAmplitude = 0;
        _slamCharge = 0;
        Shout = ShoutKind.None;
        ShoutLeft = 0;
        ShoutValue = 0;
        ShoutValue2 = 0;
        LastLostSide = -1;
        LivesFlash = 0;
        LivesGain = 0;
        _shards.Clear();
        _coreShards.Clear();
        _bursts.Clear();
        _pending.Clear();
        _cues = Cue.None;
        foreach (var b in _balls) { b.Squash = 0; b.ClearTrail(); }
    }

    /// <summary>Build this level's flower and give every petal its full hit count. A slot the ring's pattern
    /// leaves empty starts at zero, which is the same state as a popped petal — nothing draws it and nothing
    /// collides with it, so a gap needs no other machinery.</summary>
    private void FillBricks()
    {
        Flower = PetalPopFlower.For(Sides, Level);
        if (_hp.Length != Flower.BrickCount) _hp = new int[Flower.BrickCount];
        for (int i = 0; i < _hp.Length; i++)
            _hp[i] = Flower.Present(i) ? Flower.MaxHp[Flower.Decode(i).Ring] : 0;
        BuildBumpers();
    }

    /// <summary>A bracket per corner: two bars, one lying along each rail that meets there, each
    /// <c>fraction × side</c> long and a paddle's full depth thick, meeting at the corner. Each bar is its own
    /// convex quad — the L they make together is not convex, and the collider wants convex pieces — so the
    /// list holds two entries per corner (the renderer draws each pair as one L). A bar straddles its rail
    /// exactly as a paddle does, half in the field and half in the gutter, so its face is flush with a
    /// paddle's face and a ball meets it the same way.</summary>
    private void BuildBumpers()
    {
        var L = Layout;
        double f = BumperFraction(Sides, Level);
        if (f <= 0) { _bumpers = []; return; }
        double half = PetalPopTuning.PaddleHalfThickness;
        var list = new Vec2[L.Sides * 2][];
        for (int k = 0; k < L.Sides; k++)
        {
            var s = L.Side[k];
            var prevSide = L.Side[(k + L.Sides - 1) % L.Sides];
            // Along this side's rail, from corner A toward B; along the previous side's rail, from its corner B
            // (the same point) back toward its A. Both follow the rail's own arc.
            list[2 * k] = Bar(s, -s.HalfAngle, -s.HalfAngle + f * 2 * s.HalfAngle, half);
            list[2 * k + 1] = Bar(prevSide, prevSide.HalfAngle, prevSide.HalfAngle - f * 2 * prevSide.HalfAngle, half);
        }
        _bumpers = list;

        // A short strip of the rail between two arc parameters, straddling it by half a paddle's depth each way.
        // Vertex order: gutter edge first (corner, end), then the field edge back (end, corner). A bar is a
        // small fraction of a side, so its chord sits on the arc to well under a hundredth of a ball radius.
        static Vec2[] Bar(PetalPopSide s, double a0, double a1, double half)
        {
            var p0 = s.PointAt(a0); var n0 = s.NormalAt(a0);
            var p1 = s.PointAt(a1); var n1 = s.NormalAt(a1);
            return [p0 - n0 * half, p1 - n1 * half, p1 + n1 * half, p0 + n0 * half];
        }
    }

    /// <summary>Drop a charge that is in flight. ⚠ <see cref="StepSlingshot"/> is only reached in the
    /// <see cref="Stage.Playing"/> branch of <see cref="Step"/>, so a phase change is the one thing that can
    /// strand the slingshot: nothing is left running that would ever clear <see cref="Draw"/>.
    ///
    /// <para>That stranding was visible. <c>DecayPresentation</c>'s entire lunge return sits behind
    /// <c>Draw &lt;= 0</c>, so a level cleared (or a ball lost) mid-draw left every paddle parked behind its
    /// rail until the next press re-entered the slingshot and released it — and
    /// <see cref="PaddlesCrawling"/> reads <c>Draw</c> too, so they were pinned to the crawl for the whole
    /// interlude as well.</para>
    ///
    /// <para>⚠ <see cref="Lunge"/> is deliberately not zeroed. With <c>Draw</c> clear, the existing decay
    /// walks it home at <c>LungeReturnPerSec</c>, so the paddles ease back rather than snapping.</para></summary>
    private void CancelSlingshot()
    {
        _drawing = false;
        Draw = 0;
        _springing = false;
        _slamAmplitude = 0;
        _slamCharge = 0;
        SmashArmedLeft = 0;
    }

    /// <summary>Dismissed on a screen the player has already read: the level-clear interlude (the next flower
    /// is already built — land on its serve now, debris and shout dropped), or the ball-lost slow-mo while a
    /// life remains (lose it now and serve). The last life is left to play out: a run must not end on a screen
    /// nobody saw. The cabinet shot and the resume then show the next playable board.</summary>
    public void AcknowledgeOutcome()
    {
        switch (Phase)
        {
            case Stage.LevelClear:
                _shards.Clear();
                _coreShards.Clear();
                _bursts.Clear();
                ShoutLeft = 0;
                SetPhase(Stage.Serve);
                SpawnServeBall();
                break;
            case Stage.BallLost when Lives > 1:
                _balls.Clear();
                LoseLife();
                break;
        }
    }

    private void SetPhase(Stage s)
    {
        // Every phase but Playing stops the slingshot being stepped, so a draw still up has to be dropped
        // here or it never resolves. Playing is the one case that must not cancel — Launch enters it.
        if (s != Stage.Playing) CancelSlingshot();
        // The spin belongs to one interlude only; leaving it puts the playfield back square.
        if (s != Stage.LevelClear) StageChanging = false;
        Phase = s;
        PhaseTime = 0;
    }

    // ── Step ──────────────────────────────────────────────────────────────────

    /// <summary>The order is load-bearing: real-time presentation decay, then the phase clock, then the
    /// phase gates, then paddles (real time — controls stay crisp in slow-mo), then physics at the scaled
    /// step, then the high-score latch.</summary>
    public void Step(in ArcadeInput input, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.05);

        PresentationTime += dt;
        DecayPresentation(dt);
        PhaseTime += dt;
        if (ArcadeDebug.LevelSkip && input.SkipPressed && Phase != Stage.GameOver)
        {
            DebugSkipLevel(1);
            return;
        }
        double sdt = dt * TimeScale;

        switch (Phase)
        {
            case Stage.GameOver:
            case Stage.Won:
                if (input.CrossPressed) BeginRun();
                return;
            case Stage.Intro:
                StepPaddles(input, dt);
                if (input.CrossPressed) { SetPhase(Stage.Serve); SpawnServeBall(); }
                return;
            case Stage.LevelClear:
                StepPaddles(input, dt);
                if (PhaseTime >= ClearSeconds) { SetPhase(Stage.Serve); SpawnServeBall(); }
                return;
        }

        StepPaddles(input, dt);

        if (Phase == Stage.Serve)
        {
            if (_balls.Count == 0) SpawnServeBall();
            ParkServeBall();
            SteerServe(dt);
            if (input.CrossPressed || PhaseTime >= PetalPopTuning.ServeAutoSeconds) Launch();
            return;
        }

        StepSlingshot(input, dt);

        int substeps = Math.Clamp(PetalPopTuning.PhysicsSubsteps, 1, 8);
        double h = sdt / substeps;
        for (int s = 0; s < substeps; s++)
        {
            if (!StepPhysics(h)) break;
        }

        if (Score > HighScore)
        {
            HighScore = (int)Math.Min(Score, int.MaxValue);
            if (!_bestBeaten) { _bestBeaten = true; _cues |= Cue.NewBest; }
        }
    }

    private void DecayPresentation(double dt)
    {
        DiscTwist = TowardZero(DiscTwist, PetalPopTuning.TwistDecayPerSec * dt);
        DiscPunch = 1 + TowardZero(DiscPunch - 1, PetalPopTuning.PunchDecayPerSec * dt);
        ScorePulse = Math.Max(0, ScorePulse - PetalPopTuning.ScorePulseDecayPerSec * dt);
        ShoutLeft = Math.Max(0, ShoutLeft - dt);
        if (ShoutLeft <= 0) Shout = ShoutKind.None;
        SmashArmedLeft = Math.Max(0, SmashArmedLeft - dt);
        // The slam's forward surge: the released slingshot sweeps out at SpringPerSec, then eases back across
        // the armed window and settles home. While the slingshot is drawn the paddles sit behind the rail
        // instead (StepSlingshot).
        // ⚠ The sweep must not teleport. A paddle that crossed the whole draw in one step could land on the
        // far side of a ball and throw it out of bounds.
        if (Draw <= 0)
        {
            if (_springing)
            {
                Lunge = Math.Min(_slamAmplitude, Lunge + PetalPopTuning.SpringPerSec * dt);
                if (Lunge >= _slamAmplitude - 1e-9) { Lunge = _slamAmplitude; _springing = false; }
            }
            else
            {
                double armedLunge = SmashArmedLeft > 0
                    ? _slamAmplitude * Math.Sqrt(SmashArmedLeft / Math.Max(1e-6, PetalPopTuning.SmashWindowSeconds)) : 0;
                Lunge = Math.Max(armedLunge, TowardZero(Lunge, PetalPopTuning.LungeReturnPerSec * dt));
            }
        }
        SmashCooldownLeft = Math.Max(0, SmashCooldownLeft - dt);
        HitStopLeft = Math.Max(0, HitStopLeft - dt);
        LivesFlash = Math.Max(0, LivesFlash - dt);
        CoreRing = Math.Max(0, CoreRing - dt / Math.Max(1e-6, PetalPopTuning.CoreRingSeconds));
        LivesGain = Math.Max(0, LivesGain - dt * 0.45);
        double squash = PetalPopTuning.SquashDecayPerSec * dt;
        foreach (var b in _balls) b.Squash = Math.Max(0, b.Squash - squash);

        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            var s = _shards[i];
            double age = s.Age + dt;
            if (age >= PetalPopTuning.ShardSeconds) { _shards.RemoveAt(i); continue; }
            // Shards drift outward and slow, tumbling as they go.
            double drag = Math.Exp(-2.4 * dt);
            _shards[i] = s with
            {
                X = s.X + s.VX * dt, Y = s.Y + s.VY * dt, VX = s.VX * drag, VY = s.VY * drag,
                Rot = s.Rot + s.Spin * dt, Age = age,
            };
        }
        for (int i = _coreShards.Count - 1; i >= 0; i--)
        {
            var s = _coreShards[i];
            double age = s.Age + dt;
            if (age >= PetalPopTuning.CoreShardSeconds) { _coreShards.RemoveAt(i); continue; }
            // Heavier than a petal shard: less drag, so the pieces carry further before they fade.
            double drag = Math.Exp(-1.6 * dt);
            _coreShards[i] = s with
            {
                X = s.X + s.VX * dt, Y = s.Y + s.VY * dt, VX = s.VX * drag, VY = s.VY * drag,
                Rot = s.Rot + s.Spin * dt, Age = age,
            };
        }
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            double age = _bursts[i].Age + dt;
            if (age >= PetalPopTuning.BurstSeconds) _bursts.RemoveAt(i);
            else _bursts[i] = _bursts[i] with { Age = age };
        }
    }

    /// <summary>Decay toward zero from either side and land exactly on it — a residual 1e-17 would keep the
    /// host's transform pushed forever.</summary>
    private static double TowardZero(double value, double amount) => ArcadeMath.TowardZero(value, amount);

    /// <summary>Paddles crawl along their rails for the whole swing — from the windup through the release —
    /// held to <see cref="PetalPopTuning.DrawCrawlSpeed"/>. A slam still lands near where it was aimed, since
    /// the crawl cannot cross the rail inside a windup, but the aim stays live: a late correction is worth
    /// attempting and a paddle that stopped dead read as the game having dropped the stick. They slide at full
    /// speed again the moment ✕ is not held and no swing is in flight, which in practice is the instant the
    /// smash connects: landing it zeroes the armed window.</summary>
    public bool PaddlesCrawling => Draw > 0 || SmashArmedLeft > 0;

    private void StepPaddles(in ArcadeInput input, double dt)
        => _control.Step(Layout, _paddles, input.StickX, input.StickY, dt,
            PaddlesCrawling ? Math.Max(0, PetalPopTuning.DrawCrawlSpeed) : double.PositiveInfinity);

    /// <summary>The slingshot. Holding ✕ draws every paddle back behind its rail (<see cref="Draw"/> climbs
    /// over <see cref="PetalPopTuning.DrawSeconds"/>); releasing springs them forward by a surge proportional
    /// to the draw and opens the armed window, so the hit that follows is the smash. A release before
    /// <see cref="PetalPopTuning.MinDraw"/> is just a twitch: a small surge, no smash. The drawn-back paddle
    /// is where the collider is, so a ball can reach it there — see the loss test in StepPhysics.</summary>
    private void StepSlingshot(in ArcadeInput input, double dt)
    {
        bool held = Phase == Stage.Playing && input.CrossDown && SmashCooldownLeft <= 0 && SmashArmedLeft <= 0;
        if (held)
        {
            if (Draw <= 0 && !_drawing) { _drawing = true; _cues |= Cue.SmashArm; }
            Draw = Math.Min(1, Draw + dt / Math.Max(1e-3, PetalPopTuning.DrawSeconds));
            // Pulled back behind the rail, eased so the first frames of a hold read as tension not a jump.
            double target = -PetalPopTuning.DrawDepth * Draw;
            Lunge += (target - Lunge) * (1 - Math.Exp(-18 * dt));
            return;
        }
        if (!_drawing) return;
        // Release: the spring. It sweeps forward from wherever the draw left it rather than jumping, so the
        // paddle face passes through the ball's lane instead of over it.
        _drawing = false;
        double draw = Draw;
        Draw = 0;
        _slamAmplitude = PetalPopTuning.SmashLungeDepth * Math.Max(0.35, draw);
        _slamCharge = draw;
        _springing = true;
        Lunge = Math.Min(_slamAmplitude, Lunge + PetalPopTuning.SpringPerSec * dt);
        SmashCooldownLeft = PetalPopTuning.SmashCooldownSeconds;
        if (draw >= PetalPopTuning.MinDraw) SmashArmedLeft = PetalPopTuning.SmashWindowSeconds;
    }

    // ── Serve ─────────────────────────────────────────────────────────────────

    /// <summary>Put a fresh ball on the serving paddle and, unless <paramref name="aim"/> says otherwise, draw
    /// the angle it will launch at. ⚠ <c>Restore</c> passes false: the aim it should use is the one in the
    /// snapshot, and drawing a new one would advance the RNG past what was frozen.</summary>
    private void SpawnServeBall(bool aim = true)
    {
        _balls.Clear();
        _pending.Clear();
        _balls.Add(new PetalPopBall());
        // A fresh serve starts somewhere inside the near-straight deadzone (drawn from the run's RNG, so it
        // is in the snapshot); the player steers it by sliding the paddle while the ball waits (SteerServe).
        // A restore keeps the angle the snapshot froze.
        if (aim) ServeAngle = (NextDouble() * 2 - 1) * ServeDeadzone;
        ParkServeBall();
        _balls[0].ClearTrail();
    }

    private Vec2 ServePoint(out Vec2 normal, out Vec2 tangent)
    {
        var L = Layout;
        int serve = L.ServeSide;
        var side = L.Side[serve];
        double ac = _paddles[serve].Pos / L.ArcRadius;
        normal = side.NormalAt(ac);
        tangent = side.TangentAt(ac);
        return side.PointAt(ac) + normal * (PetalPopTuning.PaddleHalfThickness + PetalPopTuning.BallRadius);
    }

    private void ParkServeBall()
    {
        var p = ServePoint(out _, out _);
        var b = _balls[0];
        b.X = p.X; b.Y = p.Y; b.VX = 0; b.VY = 0; b.State = PetalPopBallState.Live; b.GutterSide = -1;
    }

    /// <summary>Exactly where a served ball will go. The renderer draws the aim dots along this and
    /// <see cref="Launch"/> fires along it, so the picture cannot promise a direction the launch does not
    /// take. The angle is the steered <see cref="ServeAngle"/> alone — nothing instantaneous rides on top,
    /// so the aim the player sees when they stop moving is the aim the launch takes.</summary>
    public Vec2 ServeAim
    {
        get
        {
            ServePoint(out var n, out var t);
            return (n * Math.Cos(ServeAngle) + t * Math.Sin(ServeAngle)).Normalized();
        }
    }

    /// <summary>Sliding the serving paddle steers the waiting serve; the aim then eases back toward straight
    /// slowly, so a slide-then-release keeps most of its angle through the launch. Integrated here rather than
    /// read off the paddle's velocity at launch, which fell to nothing the instant the stick was released.</summary>
    private void SteerServe(double dt)
    {
        double maxA = Math.Clamp(PetalPopTuning.ServeAngleMaxDeg, 0, 89) * Math.PI / 180;
        double dz = ServeDeadzone;
        ServeAngle += _paddles[Layout.ServeSide].Vel * PetalPopTuning.ServeSteerPerUnit * dt;
        // The return pulls only the excess beyond the deadzone back to its edge; inside it the aim stays
        // where the player (or the opening draw) left it, so it never settles on the exact normal.
        double excess = Math.Abs(ServeAngle) - dz;
        if (excess > 0)
            ServeAngle = Math.Sign(ServeAngle) * (dz + excess * Math.Exp(-Math.Max(0, PetalPopTuning.ServeReturnPerSec) * dt));
        ServeAngle = Math.Clamp(ServeAngle, -maxA, maxA);
    }

    private static double ServeDeadzone =>
        Math.Clamp(PetalPopTuning.ServeDeadzoneDeg, 0, Math.Max(0, PetalPopTuning.ServeAngleMaxDeg)) * Math.PI / 180;

    private void Launch()
    {
        var p = ServePoint(out _, out _);
        var dir = ServeAim;
        var b = _balls[0];
        b.X = p.X; b.Y = p.Y;
        double speed = SpeedBase(LevelOrdinal);
        b.VX = dir.X * speed; b.VY = dir.Y * speed;
        b.PrevX = b.X; b.PrevY = b.Y;
        SetPhase(Stage.Playing);
        _cues |= Cue.Serve;
    }

    // ── Physics ───────────────────────────────────────────────────────────────

    /// <summary>One substep. Returns false when the board changed state under the balls (core struck, life
    /// lost) and the rest of the frame's substeps must not run.</summary>
    private bool StepPhysics(double h)
    {
        var L = Layout;
        double R = PetalPopTuning.BallRadius;
        double rimPop = PetalPopTuning.RimRadius - R;

        bool coreStruck = false;
        for (int i = 0; i < _balls.Count && !coreStruck; i++)
        {
            var b = _balls[i];
            if (b.State == PetalPopBallState.Gutter)
            {
                b.X += b.VX * h; b.Y += b.VY * h;
                SampleTrail(b, h);
                if (Math.Sqrt(b.X * b.X + b.Y * b.Y) >= rimPop)
                {
                    _bursts.Add(new(b.X, b.Y, PetalPopBurstKind.Pop, 0));
                    _balls.RemoveAt(i); i--;
                }
                continue;
            }

            b.PrevX = b.X; b.PrevY = b.Y;
            BleedSpeed(b, h);
            b.X += b.VX * h; b.Y += b.VY * h;
            SampleTrail(b, h);

            var pos = b.Position;
            var prev = new Vec2(b.PrevX, b.PrevY);
            var vel = b.Velocity;

            // Paddles: deepest approaching contact wins. An engulfed contact — the paddle standing on the ball
            // after a swing swept onto it — is taken whatever way the ball was travelling, since the paddle
            // occupies that space and the response carries it into the field.
            int hitSide = -1; PetalPopContact hit = default; double hitOffset = 0;
            for (int side = 0; side < Sides; side++)
            {
                if (!PetalPopCollision.Paddle(L, side, _paddles[side].Pos, pos, R, out var c, out var off, Lunge, Draw)) continue;
                if (!c.Engulfed && vel.Dot(c.Normal) >= 0) continue;
                if (hitSide < 0 || c.Depth > hit.Depth) { hitSide = side; hit = c; hitOffset = off; }
            }
            if (hitSide >= 0) { PaddleResponse(b, hitSide, hit, hitOffset); pos = b.Position; prev = pos; vel = b.Velocity; }

            // Corner bumpers: a plain reflection, no english, no combo reset, and +punch is untouched — the
            // bumper is furniture, not a strike.
            foreach (var bumper in _bumpers)
            {
                if (!PetalPopCollision.Polygon(bumper, prev, pos, R, out var bc) || vel.Dot(bc.Normal) >= 0) continue;
                var bn = bc.Normal;
                b.X += bn.X * bc.Depth; b.Y += bn.Y * bc.Depth;
                double vn = b.VX * bn.X + b.VY * bn.Y;
                b.VX -= 2 * vn * bn.X; b.VY -= 2 * vn * bn.Y;
                b.PrevX = b.X; b.PrevY = b.Y;
                b.Squash = 1; b.HitNX = bn.X; b.HitNY = bn.Y;
                pos = b.Position; prev = pos; vel = b.Velocity;
                _cues |= Cue.Bumper;
                break;
            }

            if (!BrickPass(b, prev))
            {
                if (CoreExposed && PetalPopCollision.Circle(default, Flower.CoreRadius, b.Position, R, out var cc)
                    && b.Velocity.Dot(cc.Normal) < 0)
                {
                    // ⚠ The core only breaks to a charged ball. A cold one rings it and comes off: the level
                    // is won with a stock carried in from a slam, never by drifting the last ball inward.
                    if (b.Punch <= 0)
                    {
                        var cn = cc.Normal;
                        b.X += cn.X * cc.Depth; b.Y += cn.Y * cc.Depth;
                        double cvn = b.VX * cn.X + b.VY * cn.Y;
                        b.VX -= 2 * cvn * cn.X; b.VY -= 2 * cvn * cn.Y;
                        b.PrevX = b.X; b.PrevY = b.Y;
                        b.Squash = 1; b.HitNX = cn.X; b.HitNY = cn.Y;
                        CoreRing = 1;
                        _cues |= Cue.CorePing;
                        pos = b.Position; prev = pos; vel = b.Velocity;
                    }
                    else
                    {
                        BeginLevelClear(b.Position, b.Velocity);
                        coreStruck = true;
                        break;
                    }
                }
            }

            var (depth, side2) = L.MinDepth(b.Position);
            // Lost once the whole ball is past the rail — or past wherever this side's drawn-back paddle sits,
            // so a ball can still be caught by a paddle that is pulled behind its rail. The draw is bounded
            // per paddle by its room in the gutter lens, so the allowance is too.
            double drawn = Lunge < 0 ? -Math.Min(-Lunge, L.MaxDrawDepth(side2, _paddles[side2].Pos)) : 0;
            if (depth < -R + drawn || b.Position.Length > PetalPopTuning.RimRadius) ToGutter(b, side2);
        }

        if (coreStruck) return false;

        if (_pending.Count > 0) { _balls.AddRange(_pending); _pending.Clear(); }

        if (_balls.Count == 0 && Phase is Stage.Playing or Stage.BallLost)
        {
            LoseLife();
            return false;
        }
        return true;
    }

    private void BleedSpeed(PetalPopBall b, double h)
    {
        double cap = SpeedCap(LevelOrdinal);
        double speed = b.Speed;
        if (speed <= cap + 1e-9 || speed < 1e-9) return;
        double target = cap + (speed - cap) * Math.Exp(-h / Math.Max(1e-3, PetalPopTuning.SmashDecaySeconds));
        double k = target / speed;
        b.VX *= k; b.VY *= k;
    }

    private static void SampleTrail(PetalPopBall b, double h)
    {
        b.TrailTimer += h;
        double every = Math.Max(1e-3, PetalPopTuning.TrailSampleSeconds);
        if (b.TrailTimer < every) return;
        b.TrailTimer -= every;
        b.SampleTrail();
    }

    private void PaddleResponse(PetalPopBall b, int side, PetalPopContact contact, double offset)
    {
        var L = Layout;
        var n = contact.Normal;
        var pos = b.Position + n * contact.Depth;
        var v = b.Velocity;
        double speed = v.Length;

        var paddle = _paddles[side];

        // ── The exit direction, in the paddle's own frame ────────────────────────
        // One rule for every contact. The normal component always leaves inward — a reflection for a ball
        // that arrived, a shove for one the paddle overtook — and the component along the paddle is carried
        // through. ⚠ Deliberately blind to whether the paddle met the ball or swept onto it: the player
        // cannot see that difference, so it must not change where the ball goes. Engulfing decides only
        // whether a contact counts and how far the ball is pushed clear, never its direction.
        //
        // The paddle is a straight bar with rounded end caps (see PetalPopCollision.Paddle — the reach test
        // against the segment's closest point is a capsule, and the corner radius matches the drawn quarter
        // circle). A hit on the straight majority mirrors about the flat face's fixed normal; a hit on a cap
        // — the collider's closest point clamped to the spine end, which only happens right at |offset| == 1
        // — mirrors about that cap's own radial normal (contact.Normal) instead, so the reflection is a true
        // rounded-corner bounce rather than the flat face's.
        bool corner = !contact.Engulfed && Math.Abs(offset) >= 1 - 1e-9;
        var along = L.PaddleAlong(side, paddle.Pos);
        Vec2 face, t;
        if (corner)
        {
            face = n;
            t = new Vec2(-face.Y, face.X);
            if (t.Dot(along) < 0) t = -t;
        }
        else
        {
            face = L.PaddleFaceNormal(side, paddle.Pos);
            t = along;
        }
        double a = Math.Atan2(v.Dot(t), Math.Abs(v.Dot(face)));

        // A smash throws the ball where the paddle faces rather than back along the angle it arrived at: the
        // swing decides, not the bounce. Scaling the arrival term (rather than discarding it) keeps a trace
        // of the approach, and leaves the english below to do the aiming.
        // ⚠ Read the armed window here — the smash block further down consumes it.
        if (SmashArmedLeft > 0) a *= 1 - Math.Clamp(PetalPopTuning.SmashNormalPull, 0, 1);

        double english = offset * PetalPopTuning.MaxEnglishDeg + paddle.Vel * PetalPopTuning.RailEnglishDegPerUnit;
        english = Math.Clamp(english, -PetalPopTuning.MaxTotalEnglishDeg, PetalPopTuning.MaxTotalEnglishDeg);
        a += english * Math.PI / 180;
        // ⚠ This clamp is what makes "a paddle never sends a ball away from the middle" structural: the frame
        // is the paddle's own, so the exit's inward component is exactly cos(a). Nothing downstream needs to
        // re-check it, and nothing may express the direction in any other frame.
        double maxA = Math.Acos(Math.Clamp(PetalPopTuning.MinInwardNormal, 0, 0.99));
        a = Math.Clamp(a, -maxA, maxA);
        var dir = face * Math.Cos(a) + t * Math.Sin(a);

        double cap = SpeedCap(LevelOrdinal);
        speed = Math.Min(speed + PetalPopTuning.SpeedPerPaddleHit, Math.Max(cap, speed));
        speed = Math.Min(speed, cap * PetalPopTuning.SmashCapMultiplier);
        double twistSign = offset >= 0 ? 1 : -1;

        // A paddle contact costs the ball one +punch, exactly like a petal contact does, so a stock has to be
        // carried into the flower rather than banked around the board. ⚠ Every contact pays it, a smash
        // included — the smash's grant lands on top of this, which is what makes a half draw break even on a
        // hot ball and a full draw net one.
        b.Punch = Math.Max(0, b.Punch - 1);

        if (SmashArmedLeft > 0)
        {
            SmashArmedLeft = 0;
            speed = Math.Min(Math.Max(speed * PetalPopTuning.SmashSpeedMultiplier, speed + PetalPopTuning.SmashMinBoost),
                             cap * PetalPopTuning.SmashCapMultiplier);
            b.Punch = Math.Min(Math.Max(1, PetalPopTuning.PunchMax), b.Punch + PunchForDraw(_slamCharge));
            AddScore(PetalPopTuning.SmashScore);
            DiscTwist = PetalPopTuning.TwistSmashDeg * Math.PI / 180 * twistSign;
            DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchBig);
            HitStopLeft = Math.Max(HitStopLeft, PetalPopTuning.SmashHitStopSeconds);
            _bursts.Add(new(pos.X, pos.Y, PetalPopBurstKind.Smash, 0));
            // ⚠ No shout. A slam already announces itself in the hand and on the board — the disc twists,
            // punches, hit-stops, and throws its own burst — so a word over the middle of the playfield would
            // only tell the player something they already did. Don't reinstate it; the cue and the burst are
            // the feedback.
            _cues |= Cue.Smash;
        }
        else
        {
            DiscTwist = PetalPopTuning.TwistPaddleDeg * Math.PI / 180 * twistSign;
        }

        b.X = pos.X; b.Y = pos.Y;
        b.VX = dir.X * speed; b.VY = dir.Y * speed;
        b.PrevX = b.X; b.PrevY = b.Y;
        b.Squash = 1; b.HitNX = n.X; b.HitNY = n.Y;
        Combo = 0;
        _cues |= Cue.PaddleHit;
    }

    /// <summary>Petal contacts for one substep. A bounce ends the pass — a ball centred on a seam must not pop
    /// two petals and reflect twice, which would send it straight back in. A slammed ball that destroys a petal
    /// with damage to spare does not bounce, so the scan runs again and it can plow into the next one.</summary>
    private bool BrickPass(PetalPopBall b, Vec2 prev)
    {
        bool any = false;
        // Bounded by construction: every hit either bounces (ending the pass) or clears a petal, and a cleared
        // petal is skipped by the scan. The guard is belt-and-braces against a future damage rule.
        for (int guard = 0; guard < 4; guard++)
        {
            if (!FindBrick(b, prev, out int idx, out int ring, out int side, out var c)) break;
            any = true;
            if (HitBrick(b, idx, ring, side, c)) break;
        }
        return any;
    }

    /// <summary>The nearest petal this ball is overlapping and approaching, searched by ring band and then by
    /// the two nearest sides (the corner seams need the runner-up).</summary>
    private bool FindBrick(PetalPopBall b, Vec2 prev, out int index, out int ring, out int side,
                           out PetalPopContact contact)
    {
        index = ring = side = -1;
        contact = default;
        var L = Layout;
        var F = Flower;
        double R = PetalPopTuning.BallRadius;
        var pos = b.Position;
        double apo = L.Apothem(pos);
        if (apo > F.FlowerApothem + F.BulgeMargin + R) return false;
        var (best, second) = L.SidesOf(pos);

        for (int k = 0; k < F.Rings; k++)
        {
            if (apo < F.InnerApothem(k) - R || apo > F.OuterApothem(k) + F.BulgeMargin + R) continue;
            for (int pass = 0; pass < 2; pass++)
            {
                int s = pass == 0 ? best : second;
                for (int m = 0; m < F.CountPerSide[k]; m++)
                {
                    int idx = F.BrickIndex(k, s, m);
                    if (_hp[idx] <= 0) continue;
                    foreach (var part in F.Collider(idx))
                    {
                        if (!PetalPopCollision.Polygon(part, prev, pos, R, out var c)) continue;
                        if (b.Velocity.Dot(c.Normal) >= 0) continue;
                        index = idx; ring = k; side = s; contact = c;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>Damage one petal. Returns whether the ball bounced off it.
    ///
    /// <para>A contact always deals at least one damage and spends one <see cref="PetalPopBall.Punch"/>; a
    /// petal tough enough to absorb more takes the rest of the stock as extra damage in the same contact, so a
    /// two-punch ball kills a three-hit petal outright. The ball rebounds unless it arrived with +punch and the
    /// petal died, so a charged ball plows through one more petal than its stock and a cold one always
    /// bounces.</para></summary>
    private bool HitBrick(PetalPopBall b, int idx, int ring, int side, PetalPopContact c)
    {
        var F = Flower;
        int had = _hp[idx];
        int punch = Math.Max(0, b.Punch);
        int dealt = Math.Min(had, 1 + punch);
        _hp[idx] = had - dealt;
        b.Punch = Math.Max(0, punch - Math.Max(1, dealt - 1));
        // Rebound unless the ball had +punch coming in — the stock it arrived with is what carries it through,
        // so a two-punch ball plows two petals and turns on the third. A petal that survives always stops it:
        // passing through a standing petal would leave the ball inside it.
        bool bounce = punch == 0 || _hp[idx] > 0;

        if (bounce)
        {
            var n = c.Normal;
            b.X += n.X * c.Depth; b.Y += n.Y * c.Depth;
            double vn = b.VX * n.X + b.VY * n.Y;
            b.VX -= 2 * vn * n.X; b.VY -= 2 * vn * n.Y;
            b.PrevX = b.X; b.PrevY = b.Y;
            b.Squash = 1; b.HitNX = n.X; b.HitNY = n.Y;
        }

        var poly = F.Polygon(idx);
        var centre = PetalPopLayout.Centroid(poly);
        if (_hp[idx] > 0)
        {
            AddScore(PetalPopTuning.BrickChipScore);
            HitStopLeft = Math.Max(HitStopLeft, PetalPopTuning.ToughHitStopSeconds);
            DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchTough);
            _bursts.Add(new(centre.X, centre.Y, PetalPopBurstKind.Chip, 0));
            _cues |= Cue.BrickTough;
            return bounce;
        }

        Combo++;
        int depth = F.Rings - ring;
        AddScore((long)PetalPopTuning.BrickBaseScore * depth * Math.Max(1, Combo));
        SpawnShards(poly, ring, b.Velocity);
        DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchBrick);
        _cues |= Cue.Brick;
        if (Combo >= PetalPopTuning.ComboShoutAt) { StartShout(ShoutKind.Combo, Combo); _cues |= Cue.ComboShout; }
        // A multiball petal splits the ball that popped it. On the pop rather than on a chip, so a tough
        // marked petal cannot pay out twice.
        if (F.Multiball(idx)) Split(b);
        return bounce;
    }

    private void SpawnShards(Vec2[] poly, int ring, Vec2 impact)
    {
        var centre = PetalPopLayout.Centroid(poly);
        double size = (poly[1] - poly[0]).Length * 0.35;
        // One fragment per corner, thrown from the centre with a little of the ball's momentum and a
        // deterministic spin — no RNG here, so a pop can never change the next gem roll.
        for (int k = 0; k < poly.Length; k++)
        {
            var corner = Vec2.Lerp(centre, poly[k], 0.55);
            var away = (corner - centre).Normalized();
            var v = away * 0.55 + impact * 0.25;
            double spin = (k % 2 == 0 ? 1 : -1) * (6 + k * 1.7);
            _shards.Add(new(corner.X, corner.Y, v.X, v.Y, 0, spin, size, ring, 0));
        }
    }

    /// <summary>The core breaks into its seven pieces (<see cref="PetalPopCoreShatter"/>). Every piece is
    /// kicked away from the point the ball struck and carries a share of the ball's own velocity, so the
    /// wreckage flies on with the shot rather than blooming evenly from the middle; pieces nearest the
    /// strike take the hardest kick. Deterministic spin, no RNG, like every other shard.</summary>
    private void SpawnCoreShards(Vec2 at, Vec2 ballVelocity)
    {
        double r = Flower.CoreRadius;
        // The contact point on the core's rim, not the ball's centre, so the kick radiates from the surface.
        var contact = at.Normalized() * r;
        for (int k = 0; k < PetalPopCoreShatter.Pieces.Length; k++)
        {
            var centroid = PetalPopCoreShatter.Centroids[k] * r;
            var fromContact = centroid - contact;
            double dist = fromContact.Length;
            var kickDir = dist > 1e-9 ? fromContact * (1 / dist) : centroid.Normalized();
            // Near the strike the kick is sharpest; it falls off across the disc's diameter.
            double near = Math.Clamp(1 - dist / (2 * r), 0, 1);
            var v = kickDir * (0.25 + 0.45 * near) + ballVelocity * 0.55;
            double spin = (k % 2 == 0 ? 1 : -1) * (3.5 + k * 1.1);
            _coreShards.Add(new(k, centroid.X, centroid.Y, v.X, v.Y, 0, spin, 0));
        }
    }

    /// <summary>The extra-life shower: gold confetti thrown from the centre in every direction, spinning. Ring −1
    /// is the shard colour sentinel for gold. Deterministic and RNG-free, like petal shards.</summary>
    private void SpawnConfetti()
    {
        const int count = 28;
        for (int k = 0; k < count; k++)
        {
            double a = Math.Tau * k / count + (k % 2) * 0.11;
            double speed = 0.55 + 0.25 * ((k * 7) % 5) / 4.0;
            var v = new Vec2(Math.Cos(a), Math.Sin(a)) * speed;
            double spin = (k % 2 == 0 ? 1 : -1) * (5 + (k % 3) * 2.5);
            double size = 0.030 + 0.012 * ((k * 3) % 4) / 3.0;
            _shards.Add(new(0, 0, v.X, v.Y, a, spin, size, -1, 0));
        }
    }

    private void Split(PetalPopBall b)
    {
        int room = PetalPopTuning.MaxBalls - _balls.Count - _pending.Count;
        int spawn = Math.Clamp(room, 0, 2);
        double spread = PetalPopTuning.SplitSpreadDeg * Math.PI / 180;
        for (int s = 0; s < spawn; s++)
        {
            var v = b.Velocity.Rotated(s == 0 ? spread : -spread);
            // The children inherit the parent's +punch: they came off the same swing.
            var nb = new PetalPopBall
            {
                X = b.X, Y = b.Y, VX = v.X, VY = v.Y, Punch = b.Punch, PrevX = b.X, PrevY = b.Y,
            };
            nb.Squash = 1; nb.HitNX = -v.Normalized().X; nb.HitNY = -v.Normalized().Y;
            _pending.Add(nb);
        }
        _bursts.Add(new(b.X, b.Y, PetalPopBurstKind.Split, 0));
        HitStopLeft = Math.Max(HitStopLeft, PetalPopTuning.ToughHitStopSeconds);
        DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchTough);
        _cues |= Cue.Powerup | Cue.Split;
    }

    private void ToGutter(PetalPopBall b, int side)
    {
        var s = Layout.Side[side];
        var outward = -s.NormalAt(s.ParameterOf(b.Position));
        double speed = Math.Max(b.Speed, PetalPopTuning.GutterMinSpeed);
        b.VX = outward.X * speed; b.VY = outward.Y * speed;
        b.State = PetalPopBallState.Gutter;
        b.GutterSide = side;
        LastLostSide = side;
        _cues |= Cue.Lost;
        if (Phase == Stage.Playing && !_balls.Any(x => x.State == PetalPopBallState.Live) && _pending.Count == 0)
            SetPhase(Stage.BallLost);
    }

    private void LoseLife()
    {
        Lives--;
        Combo = 0;
        LivesFlash = 1;
        _pending.Clear();
        DiscTwist = PetalPopTuning.TwistImpulseDeg * Math.PI / 180 * (NextDouble() < 0.5 ? -1 : 1);
        DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchBig);
        HitStopLeft = Math.Max(HitStopLeft, PetalPopTuning.HitStopSeconds);
        _cues |= Cue.LifeLost;
        if (Lives <= 0)
        {
            Lives = 0;
            SetPhase(Stage.GameOver);
            _cues |= Cue.GameOver;
            return;
        }
        SetPhase(Stage.Serve);
        SpawnServeBall();
    }

    /// <summary>Dev only (<see cref="ArcadeDebug.LevelSkip"/>): step a level either way through the stages, clamped at
    /// the first level of the first stage, and serve fresh. Score and lives stand.</summary>
    private void DebugSkipLevel(int delta)
    {
        int first = Math.Max(3, PetalPopTuning.SidesStart);
        int lastStage = Math.Clamp(PetalPopTuning.SidesEnd, first, PetalPopLayout.MaxSides);
        bool sidesChanged = false;
        if (delta > 0)
        {
            if (Level < Sides) Level++;
            else if (Sides < lastStage) { Sides++; Level = 1; sidesChanged = true; }
            else return;
        }
        else
        {
            if (Level > 1) Level--;
            else if (Sides > first) { Sides--; Level = Sides; sidesChanged = true; }
            else return;
        }
        if (sidesChanged) ConfigureSides();
        FillBricks();
        _balls.Clear();
        _pending.Clear();
        Combo = 0;
        StartShout(ShoutKind.Level, StageOrdinal, Level);
        SetPhase(Stage.Serve);
        SpawnServeBall();
    }

    private void BeginLevelClear(Vec2 at, Vec2 ballVelocity = default)
    {
        var F = Flower;
        AddScore((long)PetalPopTuning.CoreScore * LevelOrdinal + PetalPopTuning.LevelClearBonus);
        _bursts.Add(new(0, 0, PetalPopBurstKind.Core, 0));
        SpawnCoreShards(at, ballVelocity);
        // Every petal still standing blows away with the core.
        for (int i = 0; i < _hp.Length; i++)
            if (_hp[i] > 0) SpawnShards(F.Polygon(i), F.Decode(i).Ring, F.Centroid(i).Normalized() * 0.4);
        _balls.Clear();
        _pending.Clear();
        DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchBig);
        DiscTwist = PetalPopTuning.TwistSmashDeg * Math.PI / 180 * (at.X >= 0 ? 1 : -1);
        HitStopLeft = Math.Max(HitStopLeft, PetalPopTuning.HitStopSeconds);
        Combo = 0;
        _cues |= Cue.Core | Cue.LevelClear;

        int lastStage = Math.Clamp(PetalPopTuning.SidesEnd, Sides, PetalPopLayout.MaxSides);
        if (Level < Sides)
        {
            Level++;
            FillBricks();
            SetPhase(Stage.LevelClear);
            StartShout(ShoutKind.Level, StageOrdinal, Level);
        }
        else if (Sides < lastStage)
        {
            // Graduation: a new geometry, a new paddle set, and a life for getting here.
            Sides++;
            Level = 1;
            StageChanging = true;
            ConfigureSides();
            FillBricks();
            Lives = Math.Min(Lives + Math.Max(0, PetalPopTuning.LivesPerStage), Math.Max(1, PetalPopTuning.MaxLives));
            LivesGain = 1;
            SetPhase(Stage.LevelClear);
            StartShout(ShoutKind.ExtraLife, StageOrdinal, Level);
            // Fanfare: a gold flash and ring from the centre, a shower of gold confetti, and a kick to the disc.
            _bursts.Add(new(0, 0, PetalPopBurstKind.Core, 0));
            SpawnConfetti();
            DiscPunch = Math.Max(DiscPunch, PetalPopTuning.PunchBig);
            _cues |= Cue.ExtraLife;
        }
        else
        {
            SetPhase(Stage.Won);
            _cues |= Cue.Win;
        }
    }

    private void AddScore(long amount)
    {
        if (amount <= 0) return;
        Score += amount;
        ScorePulse = 1;
    }

    private void StartShout(ShoutKind kind, int value, int value2 = 0)
    {
        Shout = kind;
        ShoutValue = value;
        ShoutValue2 = value2;
        ShoutLeft = PetalPopTuning.ShoutSeconds;
    }

    // ── RNG ───────────────────────────────────────────────────────────────────
    // Xorshift64*, state in the snapshot so a frozen game's next gem roll is the one it would have had.
    private ulong NextUInt64() => ArcadeRng.Next(ref _rng);
    private double NextDouble() => ArcadeRng.NextDouble(ref _rng);

    // ── Snapshot ──────────────────────────────────────────────────────────────

    private sealed record PaddleSnap(double Pos, double Vel);
    private sealed record BallSnap(double X, double Y, double VX, double VY, int State = 0, int Side = -1,
                                   int Punch = 0);
    private sealed record Snap(int V, int Phase, double PhaseTime, int Sides, int Level, int Lives, long Score, int High,
                               int Combo, double HitStop, ulong Rng, bool BestBeaten, double SmashCooldown,
                               int[] Hp, PaddleSnap[] Paddles, BallSnap[] Balls, double ServeAngle = 0,
                               bool StageChange = false);

    /// <summary>⚠ Bump whenever the ring layout, stage structure, scoring or the phase enum change — a board
    /// frozen under old rules is discarded, never migrated. The high score lives outside the snapshot and
    /// survives. v3: stages by side count.
    ///
    /// <para>A field appended to one of these records with a default is not a version change: an older
    /// document deserializes with that default, which is why the ball's mid-strike <c>Punch</c> carry could
    /// join without discarding a saved board.</para></summary>
    private const int SnapVersion = 3;
    private static readonly int[] ReadableVersions = [SnapVersion];

    public string Serialize() => JsonSerializer.Serialize(new Snap(
        SnapVersion, (int)Phase, PhaseTime, Sides, Level, Lives, Score, HighScore, Combo, HitStopLeft, _rng, _bestBeaten,
        SmashCooldownLeft, [.. _hp],
        [.. _paddles.Select(p => new PaddleSnap(p.Pos, p.Vel))],
        [.. _balls.Select(b => new BallSnap(b.X, b.Y, b.VX, b.VY, (int)b.State, b.GutterSide, b.Punch))],
        ServeAngle, StageChanging));

    public void Restore(string json)
    {
        // ⚠ A structural rejection below is a bare `return`, and it must still leave a fresh game — see the
        // same committed/finally guard in Connate.Restore for why the caller can't be trusted to supply one.
        bool committed = false;
        try
        {
            var snap = JsonSerializer.Deserialize<Snap>(json);
            if (snap is null || !ReadableVersions.Contains(snap.V)) return;
            if (snap.Phase < 0 || snap.Phase > (int)Stage.Won) return;
            int sidesStart = Math.Clamp(PetalPopTuning.SidesStart, PetalPopLayout.MinSides, PetalPopLayout.MaxSides);
            int sidesEnd = Math.Clamp(PetalPopTuning.SidesEnd, sidesStart, PetalPopLayout.MaxSides);
            if (snap.Sides < sidesStart || snap.Sides > sidesEnd) return;
            if (snap.Level < 1 || snap.Level > snap.Sides) return;
            if (snap.Lives < 0 || snap.Lives > Math.Max(1, PetalPopTuning.MaxLives)) return;
            if (snap.Score < 0 || snap.Combo is < 0 or > 999) return;
            if (!Finite(snap.PhaseTime) || snap.PhaseTime < 0 || snap.PhaseTime > 600) return;
            if (!Finite(snap.HitStop) || !Finite(snap.SmashCooldown) || !Finite(snap.ServeAngle)) return;
            if (Math.Abs(snap.ServeAngle) > Math.PI / 2) return;
            if (snap.Rng == 0) return;
            var L = PetalPopLayout.For(snap.Sides, PaddleHalfLengthFor(Ordinal(snap.Sides, snap.Level)));
            var flower = PetalPopFlower.For(snap.Sides, snap.Level);
            if (snap.Hp is null || snap.Hp.Length != flower.BrickCount) return;
            for (int i = 0; i < snap.Hp.Length; i++)
            {
                if (snap.Hp[i] < 0 || snap.Hp[i] > flower.MaxHp[flower.Decode(i).Ring]) return;
                // ⚠ The range is not enough — the slot has to exist. FillBricks zeroes every slot the
                // pattern leaves empty and is then overwritten wholesale by the snapshot, so hit points on
                // an absent slot survive as a phantom petal: FindBrick only tests `_hp[idx] <= 0`, so it
                // collides, scores and draws.
                if (snap.Hp[i] > 0 && !flower.Present(i)) return;
            }
            if (snap.Paddles is null || snap.Paddles.Length != snap.Sides) return;
            foreach (var p in snap.Paddles)
                if (!Finite(p.Pos) || !Finite(p.Vel) || Math.Abs(p.Pos) > L.PosMax + 1e-6) return;
            if (snap.Balls is null || snap.Balls.Length > PetalPopTuning.MaxBalls) return;
            var phase = (Stage)snap.Phase;
            bool ballsInPlay = phase is Stage.Playing or Stage.BallLost;
            double maxSpeed = SpeedCap(Ordinal(snap.Sides, snap.Level)) * PetalPopTuning.SmashCapMultiplier * 1.5;
            foreach (var b in snap.Balls)
            {
                if (!Finite(b.X) || !Finite(b.Y) || !Finite(b.VX) || !Finite(b.VY)) return;
                if (Math.Abs(b.X) > 1.05 || Math.Abs(b.Y) > 1.05) return;
                if (b.State is not (0 or 1)) return;
                if (b.State == 1 && (b.Side < 0 || b.Side >= snap.Sides)) return;
                if (b.Punch < 0 || b.Punch > Math.Max(1, PetalPopTuning.PunchMax)) return;
                double speed = Math.Sqrt(b.VX * b.VX + b.VY * b.VY);
                // A stuck live ball is a stuck game — but only where the balls are actually kept; a parked
                // serve ball is legitimately still and is respawned rather than restored.
                if (ballsInPlay && b.State == 0 && (speed <= 1e-6 || speed > maxSpeed)) return;
            }
            bool liveBalls = snap.Balls.Any(b => b.State == 0);
            if (phase == Stage.Playing && snap.Balls.Length == 0) return;
            if (phase == Stage.Playing && !liveBalls) phase = Stage.BallLost;
            if (phase == Stage.BallLost && snap.Balls.Length == 0) return;

            // Commit.
            Sides = snap.Sides;
            Level = snap.Level;
            Lives = snap.Lives;
            Score = snap.Score;
            Combo = snap.Combo;
            ServeAngle = snap.ServeAngle;
            HitStopLeft = Math.Clamp(snap.HitStop, 0, Math.Max(PetalPopTuning.HitStopSeconds, PetalPopTuning.SmashHitStopSeconds));
            SmashCooldownLeft = Math.Clamp(snap.SmashCooldown, 0, PetalPopTuning.SmashCooldownSeconds);
            _rng = snap.Rng;
            _bestBeaten = snap.BestBeaten;
            ConfigureSides();
            FillBricks();   // builds this level's flower and sizes _hp
            Array.Copy(snap.Hp, _hp, _hp.Length);
            for (int i = 0; i < _paddles.Length; i++)
            {
                _paddles[i].Pos = Math.Clamp(snap.Paddles[i].Pos, -L.PosMax, L.PosMax);
                _paddles[i].Vel = Math.Clamp(snap.Paddles[i].Vel, -10, 10);
            }
            _balls.Clear();
            if (phase is Stage.Playing or Stage.BallLost)
            {
                foreach (var b in snap.Balls)
                    _balls.Add(new PetalPopBall
                    {
                        X = b.X, Y = b.Y, VX = b.VX, VY = b.VY, State = (PetalPopBallState)b.State,
                        GutterSide = b.State == 1 ? b.Side : -1,
                        Punch = Math.Clamp(b.Punch, 0, Math.Max(1, PetalPopTuning.PunchMax)),
                        PrevX = b.X, PrevY = b.Y,
                    });
            }
            if (snap.High > HighScore) HighScore = snap.High;
            ResetPresentation();
            Phase = phase;
            StageChanging = snap.StageChange && phase == Stage.LevelClear;
            PhaseTime = Math.Min(snap.PhaseTime, phase == Stage.LevelClear ? ClearSeconds : snap.PhaseTime);
            // ⚠ Keep the restored aim: drawing a fresh one here would advance the RNG past the snapshot and
            // the restored game would diverge from the one that was frozen.
            if (phase == Stage.Serve) SpawnServeBall(aim: false);
            committed = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] Petalpop snapshot discarded ({ex.Message})");
        }
        finally
        {
            if (!committed) BeginRun();
        }
    }

    private static bool Finite(double v) => ArcadeMath.Finite(v);
}
