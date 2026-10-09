using System.Diagnostics;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>Endless radial number-cluster game and owner of all deterministic run state.
/// <para>No WPF, wall-clock, audio, or filesystem calls here: the host supplies fixed-step input and elapsed
/// time, and <c>ConnateRenderer</c> reads the public views without mutating them. Connate stays out of
/// ArcadeCatalog until separately authorized.</para></summary>
public sealed class Connate : IArcadeGame
{
    public const string GameId = "connate";
    public string Id => GameId;
    public string Title => "Connate";

    public enum Stage { Intro, Playing, LimitBreach, GameOver }

    [Flags]
    public enum Cue
    {
        None = 0, Move = 1, Fire = 2, Merge = 4, Chain = 8, Guarded = 16,
        Warning = 32, Recover = 64, GameOver = 128, NewBest = 256,
        /// <summary>A nugget of value landed in the counter. Rate-limited in the sim rather than the mixer —
        /// see ConnateTuning.ScoreCollectCueMinimumSeconds.</summary>
        Collect = 512,
        /// <summary>The field emptied of every tile and blob (the boss block is coming), a full charge or a board
        /// clear gifted a bomb, a bomb went off (the block takes bomb hits as explosions), a pressure blob arrived or
        /// was swept, and one more merge counted toward the combo. Split from NewBest and Chain so each reads as
        /// its own moment.</summary>
        BoardClear = 1024, BombEarned = 2048, BombExplode = 4096, GarbageArrive = 8192, GarbageCleared = 16384, ComboStep = 32768,
    }

    // Authoritative physics state. Pending merges reserve body IDs; flash/explosion/clear lists are transient
    // renderer events and therefore do not alter score, collision, or future random draws.
    private readonly List<ConnateBody> _bodies = [];
    private readonly List<ConnatePendingMerge> _pendingMerges = [];
    private readonly List<ConnateMergeFlash> _mergeFlashes = [];
    private readonly List<ConnateBombProjectile> _bombProjectiles = [];
    private readonly List<ConnateBombExplosion> _bombExplosions = [];
    private readonly List<ConnateGarbageClear> _garbageClears = [];
    // Feed and cadence state. Every RNG-consuming decision is stored or derivable so closing/reopening cannot
    // reroll the held number, next bomb, garbage arrival, or accepted physics continuation.
    private readonly List<int> _baseBag = [];
    private int _baseBagIndex;
    private double _fireCooldown;
    /// <summary>Seconds since the craft last fired, counting up to <see cref="RecoilCap"/> and stopping.
    /// Presentation only — the recoil clock the renderer plays its launch animation off. It lives in the sim
    /// because a frozen game must repaint the same frame forever, which a renderer-side timer cannot promise.</summary>
    private double _sinceFire = RecoilCap;

    /// <summary>Where the recoil clock stops. Long past any launch animation, and short enough that a run
    /// left idle for an hour still freezes to a small number.</summary>
    private const double RecoilCap = 1;

    /// <summary>Seconds since the last shot left the craft, capped. 0 is the instant of firing.</summary>
    public double SinceFire => _sinceFire;

    /// <summary>Is the fire button down right now — the launcher wound, holding its piece, not yet let go?
    ///
    /// <para>⚠ Deliberately not snapshotted. Every other presentation clock is, because it describes the
    /// board; this describes the player's thumb, and nobody is holding a button on a run being resumed. It
    /// restores false, which is the truth.</para></summary>
    public bool FireHeld => _fireHeld;

    /// <summary>Seconds since the trigger went down, capped — the wind-up clock, meaningless unless
    /// <see cref="FireHeld"/>. Its own counter rather than a share of <see cref="SinceFire"/>: a player can
    /// hold for as long as they like, so the wind has a start but no fixed end.
    ///
    /// <para>⚠ Not snapshotted, for the same reason <see cref="FireHeld"/> is not.</para></summary>
    public double SinceHold => _sinceHold;

    /// <summary>How far the bow was drawn when the last shot left, 0..1 on the same scale as the launch charge
    /// — 0 for a shot nobody drew. The renderer springs the craft forward from this, so it has to be the
    /// tension the craft was actually at, not the shot's power: a deadline shot while ✕ is up was never drawn,
    /// and springing it from a draw would jump the whole assembly outward on the frame the clock spent the
    /// piece.
    ///
    /// <para>⚠ Not snapshotted, for the same reason <see cref="FireHeld"/> is not. It restores 0, which
    /// simply means a resumed run does not finish a spring it cannot know it was in.</para></summary>
    public double ShotDrawTension => _shotDrawTension;

    /// <summary>Drops a hold in progress without firing: the host cleared its input because the board stopped
    /// being played (pause, a how-to card, the isolation guard, the resume beat, a restart). A button still
    /// physically down across the gap is consumed, so it cannot arm a charge nobody started on the live board,
    /// and a release made during the gap cannot fire a stale charge on the first step back.</summary>
    public void CancelInput()
    {
        _fireHeld = false;
        _pressConsumed = true;
        _sinceHold = RecoilCap;
    }

    private bool _fireHeld;
    /// <summary>✕ is down but its press was spent without a release — the shot clock fired the piece mid-hold,
    /// the intro was skipped with it, or the host cancelled the hold. Cleared by releasing or by a fresh press
    /// edge; while set, the hold neither draws the bow nor fires on release.</summary>
    private bool _pressConsumed;
    private double _sinceHold = RecoilCap;
    private double _shotDrawTension;
    // Chain provenance changes audiovisual intensity only; it never awards score or changes merge rules.
    private double _chainTimeLeft;
    private int _chainDepth;
    private long _lastMergeShotId = -1;
    private long _nextBodyId = 1;
    private long _nextShotId = 1;
    // Xorshift state is explicit rather than System.Random's opaque state so snapshots round-trip determinism.
    private ulong _rng = 0x434F4E4E41544521UL; // "CONNATE!"
    private bool _warningLatched;
    private double _shotClockElapsed;
    /// <summary>Rim angular velocity (rad/s) for the steering model. Transient — not part of the snapshot;
    /// see StepPlayer.</summary>
    private double _rimVelocity;
    private double _garbageTimeLeft;
    private Cue _cues;
    /// <summary>Floor on bomb immunity, so the respite a bomb gives outlasts the next tile fired.</summary>
    private double _immunityFloorLeft;
    /// <summary>The tile that was NEXT when a bomb loaded, parked behind the bomb(s) and handed to the rail
    /// after the last one fires. −1 = nothing parked; only ever set while a bomb is loaded.
    /// Serialized: losing it would silently eat the player's tile.</summary>
    private int _queuedRank = -1;
    private int _queuedHue = ConnateRules.HueAzure;
    /// <summary>Motes flying from a destroyed tile toward the score counter, and the lagging displayed total
    /// they feed. Transient presentation, like the merge flashes beside them.</summary>
    private readonly List<ConnateScoreMote> _scoreMotes = [];
    /// <summary>Combo bursts, transient presentation like the merge flashes beside them.</summary>
    private readonly List<ConnateComboBurst> _comboBursts = [];

    public Stage Phase { get; private set; }
    public double PhaseTime { get; private set; }
    public double PlayerAngle { get; private set; }
    public int HeldRank { get; private set; }
    /// <summary>Family of the held tile. Meaningless while <see cref="HeldIsBomb"/> — a bomb belongs to
    /// nobody and acts on every tile equally.</summary>
    public int HeldHue { get; private set; } = ConnateRules.HueAzure;
    public bool HeldIsBomb { get; private set; }
    public bool SizeLimitImmune { get; private set; }
    // ⚠ Don't bind a mechanic to △: the host owns it for the HOW TO PLAY card, so a game never sees it.
    public double SizeFuse { get; private set; }
    public long FinalFieldSum { get; private set; }
    /// <summary>The BANKED value: every numbered body a bomb has turned into motes plus every boss-block hit,
    /// credited the instant it is earned rather than when the motes land. It is the difficulty input
    /// (<see cref="DifficultyIndex"/>) and the total <see cref="CollectedScore"/> is always catching up to;
    /// keeping it separate from the board lets physical deletion reclaim space without erasing earned value.
    /// </summary>
    public long ExplodedValue { get; private set; }

    // ── Difficulty stages ────────────────────────────────────────────────────
    /// <summary>0..19, derived from <see cref="ExplodedValue"/> on every read — never stored, so one payout
    /// can skip stages and a restored run is simply at the stage its banked total says. Twenty is the cap.
    /// ⚠ Frozen at the stage the board was cleared at for as long as the boss-block encounter lasts, whatever
    /// the hits bank; the owed rise is announced when the block is gone.</summary>
    public int DifficultyIndex => _boss.Active ? _boss.CapturedStage : ConnateTuning.StageIndexFor(ExplodedValue);
    /// <summary>The player-facing stage number, 1..20.</summary>
    public int DifficultyStage => DifficultyIndex + 1;
    /// <summary>The live shot deadline in seconds. Tracked rather than recomputed per read so a change can
    /// rescale the elapsed clock exactly once — see <see cref="SyncShotClockDeadline"/>.</summary>
    public double ShotClockDeadline => _shotClockDeadline;
    private double _shotClockDeadline = ConnateTuning.ShotClockSeconds;

    /// <summary>Moves the deadline to what the stage now calls for, carrying the FRACTION of the clock already
    /// spent across: a clock half-used at 3.00 s is half-used at 2.85 s. Without the rescale a shrinking
    /// deadline could drop beneath the elapsed time and fire the piece on the spot.</summary>
    private void SyncShotClockDeadline()
    {
        double deadline = ConnateTuning.ShotClockDeadline(DifficultyIndex);
        if (Math.Abs(deadline - _shotClockDeadline) < 1e-9) return;
        _shotClockElapsed = Math.Clamp(_shotClockElapsed * deadline / Math.Max(1e-6, _shotClockDeadline), 0, deadline);
        _shotClockDeadline = deadline;
    }

    /// <summary>The stage the last shout announced (a stage INDEX). Compared against <see cref="DifficultyIndex"/>
    /// at the end of every playing step, so each rise is announced exactly once; a restore and a restart
    /// re-anchor it without announcing.</summary>
    private int _announcedStageIndex;
    /// <summary>Seconds the "STAGE n" shout has left, so the renderer can fade it. Presentation only — a
    /// resumed run never replays it.</summary>
    public double StageShoutLeft { get; private set; }
    /// <summary>The player-facing stage the live shout names. Meaningful only while
    /// <see cref="StageShoutLeft"/> is above zero.</summary>
    public int StageShoutStage { get; private set; }

    /// <summary>Whether this run has already shouted "NEW BEST". Serialized, so a run resumed after passing the
    /// best does not shout it again.</summary>
    private bool _newBestShouted;
    /// <summary>Seconds the "NEW BEST" shout has left. Presentation only.</summary>
    public double NewBestShoutLeft { get; private set; }
    public int HighScore { get; private set; }
    public bool IsGameOver => Phase == Stage.GameOver;

    /// <summary>Dismissed during the slow-motion death settle: the player has read the death, so the shot and
    /// the snapshot must show the next playable board, not a run 0.65 s from game over. The score was
    /// committed when the settle began, so restarting here loses nothing.</summary>
    public void AcknowledgeOutcome()
    {
        if (Phase == Stage.LimitBreach) RestartRound();
    }

    public IReadOnlyList<ConnateBody> Bodies => _bodies;
    public IReadOnlyList<ConnatePendingMerge> PendingMerges => _pendingMerges;
    public IReadOnlyList<ConnateMergeFlash> MergeFlashes => _mergeFlashes;
    public IReadOnlyList<ConnateBombProjectile> BombProjectiles => _bombProjectiles;
    public IReadOnlyList<ConnateBombExplosion> BombExplosions => _bombExplosions;
    public IReadOnlyList<ConnateGarbageClear> GarbageClears => _garbageClears;
    public IReadOnlyList<ConnateScoreMote> ScoreMotes => _scoreMotes;
    public IReadOnlyList<ConnateComboBurst> ComboBursts => _comboBursts;

    // ── Combo economy + live score ───────────────────────────────────────────

    /// <summary>Merges caused by the resolving shot once it has caused more than one; zero when no combo is
    /// on screen.</summary>
    public int ComboCount { get; private set; }
    /// <summary>How fast the current cascade is running (merges inside the continuation window).</summary>
    public int ChainDepth => _chainDepth;
    /// <summary>Seconds the combo readout has left, so the renderer can fade it rather than blink it away.</summary>
    public double ComboDisplayLeft { get; private set; }
    /// <summary>0..<see cref="BombChargeCost"/> − 1 between combos. Each combo adds charge on a doubling curve.</summary>
    public int BombCharge { get; private set; }
    /// <summary>Charge a full bomb costs right now: the base cost plus one step per stage index, so bombs get
    /// dearer as the run gets harder. Derived, like the stage; earned charge carries across a stage change.</summary>
    public int BombChargeCost => Math.Max(1, ConnateTuning.BombChargePerBomb
        + ConnateTuning.BombChargePerStage * Math.Min(DifficultyIndex, ConnateTuning.BombCostMaximumSteps));
    /// <summary>The dearest a bomb can be: the snapshot's upper bound on stored charge, which is checked before
    /// the restored bank (and so the stage) is known.</summary>
    private static int MaximumBombChargeCost =>
        Math.Max(1, ConnateTuning.BombChargePerBomb
            + ConnateTuning.BombChargePerStage * ConnateTuning.BombCostMaximumSteps);
    /// <summary>Bombs earned but not yet on the rail. An earned bomb never displaces the numbered tile in the
    /// craft: it waits here until that tile fires, then loads; each bomb fired loads the next from this count.</summary>
    public int PendingBombs { get; private set; }
    /// <summary>Merges since the last successful fire — the combo counter; reset by firing and nothing
    /// else.</summary>
    private int _mergesSinceShot;

    // ── Board clear ──────────────────────────────────────────────────────────
    /// <summary>Whether the board has had anything on it since the last clear. Without this latch an
    /// already-empty board would start the boss-block encounter every frame.</summary>
    private bool _boardOccupied = true;
    /// <summary>Seconds the board-clear banner has left, so the renderer can fade it.</summary>
    public double BoardClearLeft { get; private set; }

    /// <summary>The boss-block encounter a board clear starts; <see cref="ConnateBossBlock.Active"/> for as long
    /// as it lasts. The renderer reads it; only this class changes it.</summary>
    public ConnateBossBlock BossBlock => _boss;
    private readonly ConnateBossBlock _boss = new();
    /// <summary>Reused by the encounter step so a bomb hit allocates nothing.</summary>
    private readonly List<ConnateBossHit> _bossHits = [];
    /// <summary>Stage numbers earned during an encounter and not yet shouted, ascending. Presentation only.</summary>
    private readonly Queue<int> _owedStageShouts = new();

    // ── Unlocked starts ──────────────────────────────────────────────────────
    /// <summary>The unlocked start stages. Settings, not run state: it survives a restart and a finished run.</summary>
    public ConnateStarts Starts => _starts;
    private readonly ConnateStarts _starts = new();
    /// <summary>The stage the current run began at: 1 for every start but a pause-menu START AT STAGE. It is the
    /// pause row's current value and is not persisted, so Reset, AGAIN and a relaunch all read 1.</summary>
    public int StartStage { get; private set; } = 1;
    private bool _settingsDirty;

    /// <summary>Whether the persisted settings changed since last asked — a start unlocked. The host writes them
    /// straight away when it is true, so a hard kill cannot take an unlock back. Read once: it clears.</summary>
    public bool TakeSettingsDirty() { bool dirty = _settingsDirty; _settingsDirty = false; return dirty; }

    /// <summary>True while the rail cannot fire: a block is landing, or the last bomb has gone and the block is
    /// breaking. The renderer drops the aim guide and the shot clock for it.</summary>
    public bool FireLocked => _boss.Active && !(_boss.Phase == ConnateBossPhase.Fight && HeldIsBomb);

    /// <summary>True when the encounter has taken the piece off the rail and nothing has replaced it: the loaded
    /// tile has shattered and the last bomb is away. The preserved NEXT returns when the block breaks.</summary>
    public bool RailEmpty => _boss.Active && _boss.Shattered && !HeldIsBomb;

    /// <summary>Phase of the over-limit warning pulse, in radians, for the renderer to take a sine of.
    ///
    /// <para>⚠ The rate must be integrated here, never computed in the renderer as <c>PhaseTime × rate</c>:
    /// the rate varies with the fuse, so that form picks up a <c>2·t·fuse′</c> term and the pulse accelerates
    /// with board age. Integrating the rate is the only form where the constant means cycles per second.</para>
    ///
    /// <para>Presentation only, so it isn't serialized.</para></summary>
    public double OverLimitPhase { get; private set; }

    /// <summary>Seconds left in the bomb's flight from the charge meter to the payload. Firing is refused
    /// while it runs, so the bomb can't be launched from a rail it hasn't reached.</summary>
    public double BombDeliveryLeft { get; private set; }
    /// <summary>0→1 across the delivery, for the renderer.</summary>
    public double BombDeliveryProgress => BombDeliveryLeft <= 0 ? 1
        : 1 - Math.Clamp(BombDeliveryLeft / Math.Max(0.05, ConnateTuning.BombDeliverySeconds), 0, 1);
    public bool BombDelivering => BombDeliveryLeft > 0;
    /// <summary>The tile that was NEXT when a bomb loaded is parked behind it and comes back to the rail once
    /// the last bomb has fired.</summary>
    public bool HasQueuedTile => _queuedRank >= 0;

    /// <summary>Value that has actually landed in the counter, i.e. the score. Only collected motes
    /// count — tiles sitting on the board are worth nothing until a bomb turns them into motes and those
    /// motes arrive.</summary>
    public long CollectedScore { get; private set; }

    /// <summary>The score. One named source of truth, so the renderer, the end-of-round commit and the host
    /// high score don't each decide what "the score" is.</summary>
    public long LiveScore => CollectedScore;
    /// <summary>The number actually drawn: chases <see cref="LiveScore"/> so it counts up as motes land.</summary>
    public double DisplayedScore { get; private set; }

    /// <summary>0..1, kicked by every arriving nugget and decaying between them, so the counter visibly takes
    /// each hit. Bigger nuggets kick harder. Presentation only — the score itself is
    /// <see cref="CollectedScore"/>, which arrival has already added to by the time this moves.</summary>
    public double ScorePulse { get; private set; }
    /// <summary>Throttle on <see cref="Cue.Collect"/>; a burst arriving over a few frames would otherwise ask
    /// the mixer for a blip per frame.</summary>
    private double _collectCueCooldown;

    /// <summary>The refusal blip's floor. Once the shot clock fills, <see cref="Fire"/> retries every step until
    /// the lane clears, and every refusal raised the cue — at 60 Hz while garbage sat across the launch ring.</summary>
    private double _guardedCueCooldown;

    private void RaiseGuarded()
    {
        if (_guardedCueCooldown > 0) return;
        _cues |= Cue.Guarded;
        _guardedCueCooldown = ConnateTuning.GuardedCueMinimumSeconds;
    }
    /// <summary>Normalized cradle timer used only for its attached arc; one means automatic fire is due.</summary>
    public double ShotClockProgress => Math.Clamp(
        _shotClockElapsed / Math.Max(0.1, _shotClockDeadline), 0, 1);

    /// <summary>0 until the clock reaches its final stretch, then 0→1 across just that stretch, so the readout
    /// appears only once the deadline is near. A second view of the same clock, not a second clock —
    /// <see cref="ShotClockProgress"/> is still the whole span and auto-fire still fires at 1.</summary>
    public double ShotClockUrgency
    {
        get
        {
            double shown = Math.Clamp(ConnateTuning.ShotClockVisibleFraction, 0.05, 0.95);
            return Math.Clamp((ShotClockProgress - (1 - shown)) / shown, 0, 1);
        }
    }

    public long HeldValue => HeldIsBomb ? 0 : ConnateRules.ValueForRank(HeldRank);
    // Garbage carries a placeholder Rank=0 for compact storage but must never unlock feed tiers.
    public int HighestRank => _bodies.Where(body => !body.IsGarbage)
        .Select(body => body.Rank).DefaultIfEmpty(-1).Max();
    public long HighestValue => HighestRank < 0 ? 0 : ConnateRules.ValueForRank(HighestRank);
    /// <summary>Outer edge of every armed body, including garbage; this is the sole spatial loss metric.</summary>
    public double ClumpExtent => _bodies.Where(body => body.SizeArmed)
        .Select(body => body.RadiusFromCenter + body.Radius).DefaultIfEmpty(0).Max();

    /// <summary>Live logical sum, deliberately not rendered until the round ends.</summary>
    public long FieldSum
    {
        get
        {
            long sum = 0;
            foreach (ConnateBody body in _bodies) sum = ConnateRules.SaturatingAdd(sum, body.Value);
            return sum;
        }
    }

    /// <summary>Predicted radial coordinate of the first collision along the current inward firing ray.</summary>
    public double AimContactRadius
    {
        get
        {
            double ox = Math.Sin(PlayerAngle) * ConnateTuning.LaunchRadius;
            double oy = -Math.Cos(PlayerAngle) * ConnateTuning.LaunchRadius;
            double ix = -Math.Sin(PlayerAngle), iy = Math.Cos(PlayerAngle);
            double heldRadius = HeldIsBomb ? ConnateTuning.BombRadius : ConnateTuning.RadiusForRank(HeldRank);
            double bestTravel = ConnateTuning.LaunchRadius;
            foreach (ConnateBody body in _bodies)
            {
                if (HeldIsBomb && body.IsGarbage) continue;
                double contactRadius = heldRadius + body.Radius;
                double rx = body.X - ox, ry = body.Y - oy;
                double along = rx * ix + ry * iy;
                if (along < 0 || along > bestTravel + contactRadius) continue;
                double perpendicularSquared = rx * rx + ry * ry - along * along;
                if (perpendicularSquared >= contactRadius * contactRadius) continue;
                double contact = along - Math.Sqrt(Math.Max(0, contactRadius * contactRadius - perpendicularSquared));
                if (contact >= 0) bestTravel = Math.Min(bestTravel, contact);
            }
            return Math.Max(0, ConnateTuning.LaunchRadius - bestTravel);
        }
    }

    public Connate() => RestartRound();

    public Cue TakeCues() { Cue result = _cues; _cues = Cue.None; return result; }

    /// <summary>Radial (absolute-angle) aiming, per game and persisted. The pause menu is the only thing
    /// that sets it; it defaults off.</summary>
    public bool RadialAiming { get; private set; }

    /// <summary>The pause-menu key of the START AT STAGE row.</summary>
    public const string StartKey = "start";

    /// <summary>RADIAL CONTROLS always; START AT STAGE only once a start beyond stage 1 is unlocked, offering
    /// exactly the unlocked starts, as an action row (<see cref="ArcadePauseOption.StartsOnConfirm"/>). Its value is
    /// the stage the current run began at.</summary>
    public IReadOnlyList<ArcadePauseOption> PauseOptions
    {
        get
        {
            var radial = new ArcadePauseOption("radial", Loc.T(UiText.Arcade.RadialControls),
                [Loc.T(UiText.Arcade.Off), Loc.T(UiText.Arcade.On)], RadialAiming ? 1 : 0);
            if (_starts.Highest <= 1) return [radial];
            int[] offered = _starts.Offered();
            return [radial, new ArcadePauseOption(StartKey, Loc.T(UiText.Arcade.StartAtStage),
                [.. offered.Select(stage => stage.ToString(System.Globalization.CultureInfo.InvariantCulture))],
                Math.Max(0, Array.IndexOf(offered, StartStage)), StartsOnConfirm: true)];
        }
    }

    /// <summary>✕ on the row starts a new run, so over a live run it asks the question Reset asks — at the stage the
    /// run already began at too, which is still a new run. A board with nothing banked has nothing to lose.</summary>
    public string? ConfirmPauseOption(string key, int choice)
    {
        if (!string.Equals(key, StartKey, StringComparison.OrdinalIgnoreCase)) return null;
        int[] offered = _starts.Offered();
        if (choice < 0 || choice >= offered.Length) return null;
        bool inProgress = (Phase == Stage.Playing || Phase == Stage.LimitBreach) && CollectedScore > 0;
        return inProgress ? Loc.T(UiText.Arcade.EndsRun) : null;
    }

    /// <summary>The △ card. It teaches the rules nothing on the board states: the tap and the slam, which
    /// pieces merge, what earns and loads a bomb, garbage, the shot clock and the stages, the boss block, and
    /// the boundary.
    ///
    /// <para>Copy is author-editable — see docs/ARCADE.md. ⚠ Keep the bullets short: they wrap, and a wrap
    /// pushes every row below it down. <c>TestHarness.exe arcade</c> fails if the card stops fitting.</para></summary>
    public ArcadeHowTo? HowTo => new(Loc.T(UiText.Arcade.HowToPlay),
    [
        new(Loc.T(UiText.Arcade.ConnateHow1), "craft"),
        new(Loc.T(UiText.Arcade.ConnateHow2), "merge"),
        new(Loc.T(UiText.Arcade.ConnateHow3), "families"),
        new(Loc.T(UiText.Arcade.ConnateHow4), "bomb"),
        new(Loc.T(UiText.Arcade.ConnateHow5), "garbage"),
        new(Loc.T(UiText.Arcade.ConnateHow6), "clock"),
        new(Loc.T(UiText.Arcade.ConnateHow7), "limit"),
    ]);

    public void ApplyPauseOption(string key, int choice)
    {
        if (string.Equals(key, StartKey, StringComparison.OrdinalIgnoreCase))
        {
            int[] offered = _starts.Offered();
            if (choice < 0 || choice >= offered.Length) return;
            // Stage 1 is Reset, intro card and all; a later stage goes straight in, seeded.
            if (offered[choice] <= 1) RestartRound(); else RestartRound(offered[choice]);
            return;
        }
        if (!string.Equals(key, "radial", StringComparison.OrdinalIgnoreCase)) return;
        RadialAiming = choice == 1;
        _rimVelocity = 0;   // the two models don't share state; carrying speed across would lurch the craft
    }

    /// <summary>The per-game store: the aiming model, then the highest unlocked start. The appended field
    /// defaults, so a blob from before unlocks reads as "none recorded" and the unlock is derived once from the
    /// best score. Fields an older build wrote beside it are ignored.</summary>
    private sealed record SettingsSnap(bool Radial, int Unlocked = 1);

    public string? SerializeSettings() =>
        JsonSerializer.Serialize(new SettingsSnap(RadialAiming, _starts.Highest));

    /// <summary>Read field by field rather than through a record, so one hostile value costs only itself: a
    /// stored unlock that is not a start stage reads as 1 without taking the aiming model with it.</summary>
    public void RestoreSettings(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            RadialAiming = root.TryGetProperty("Radial", out JsonElement radial) && radial.ValueKind == JsonValueKind.True;
            // Absent or null = never recorded (derive from the best); present but not an int = hostile (1).
            int? unlocked = null;
            if (root.TryGetProperty("Unlocked", out JsonElement stored) && stored.ValueKind != JsonValueKind.Null)
                unlocked = stored.ValueKind == JsonValueKind.Number && stored.TryGetInt32(out int value) ? value : -1;
            _starts.Load(unlocked);
        }
        catch (JsonException ex) { Trace.WriteLine($"[Arcade] Connate: settings snapshot unreadable: {ex.Message}"); }
        catch (InvalidOperationException ex) { Trace.WriteLine($"[Arcade] Connate: settings snapshot unreadable: {ex.Message}"); }
    }

    public void SeedHighScore(int high)
    {
        if (high > HighScore) HighScore = high;
        // A player whose settings predate unlocks already earned them: derive the unlock from the best, once.
        _starts.SeedFromBest(Math.Max(0, high));
    }

    /// <summary>Advances logical time. The order is load-bearing: age presentation, handle terminal states,
    /// move the craft, accept/auto-fire, schedule garbage, simulate physics/merges, then evaluate the
    /// post-physics size envelope.</summary>
    public void Step(in ArcadeInput input, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.05);
        double phaseBefore = PhaseTime;
        PhaseTime += dt;
        StageShoutLeft = Math.Max(0, StageShoutLeft - dt);
        NewBestShoutLeft = Math.Max(0, NewBestShoutLeft - dt);
        _fireCooldown = Math.Max(0, _fireCooldown - dt);
        _sinceFire = Math.Min(RecoilCap, _sinceFire + dt);
        _chainTimeLeft = Math.Max(0, _chainTimeLeft - dt);
        for (int i = _mergeFlashes.Count - 1; i >= 0; i--)
        {
            ConnateMergeFlash flash = _mergeFlashes[i] with { Age = _mergeFlashes[i].Age + dt };
            if (flash.Age >= ConnateTuning.MergeFlashSeconds) _mergeFlashes.RemoveAt(i);
            else _mergeFlashes[i] = flash;
        }
        for (int i = _bombExplosions.Count - 1; i >= 0; i--)
        {
            ConnateBombExplosion explosion = _bombExplosions[i] with { Age = _bombExplosions[i].Age + dt };
            if (explosion.Age >= ConnateTuning.BombExplosionSeconds) _bombExplosions.RemoveAt(i);
            else _bombExplosions[i] = explosion;
        }
        for (int i = _garbageClears.Count - 1; i >= 0; i--)
        {
            ConnateGarbageClear clear = _garbageClears[i] with { Age = _garbageClears[i].Age + dt };
            if (clear.Age >= ConnateTuning.GarbageClearSeconds) _garbageClears.RemoveAt(i);
            else _garbageClears[i] = clear;
        }
        _collectCueCooldown = Math.Max(0, _collectCueCooldown - dt);
        _guardedCueCooldown = Math.Max(0, _guardedCueCooldown - dt);
        ScorePulse = Math.Max(0, ScorePulse - dt * ConnateTuning.ScorePulseDecayPerSec);
        for (int i = _scoreMotes.Count - 1; i >= 0; i--)
        {
            ConnateScoreMote mote = _scoreMotes[i] with { Age = _scoreMotes[i].Age + dt };
            // ⚠ Delay first, then flight: a mote scatters before it is pulled, so arrival is the sum. Comparing
            // against the flight time alone would land the whole burst on one frame — the lump this exists to
            // break up.
            if (mote.Age >= mote.Delay + ConnateTuning.ScoreMoteFlightSeconds)
            {
                // Arrival is the scoring event. Nothing else adds to the total.
                CollectedScore = ConnateRules.SaturatingAdd(CollectedScore, mote.Value);
                // The counter takes the hit, harder for a bigger nugget.
                ScorePulse = Math.Min(1, ScorePulse + 0.34 + 0.30 * Math.Clamp(mote.Size, 0, 2));
                if (_collectCueCooldown <= 0)
                {
                    _cues |= Cue.Collect;
                    _collectCueCooldown = ConnateTuning.ScoreCollectCueMinimumSeconds;
                }
                _scoreMotes.RemoveAt(i);
            }
            else _scoreMotes[i] = mote;
        }
        for (int i = _comboBursts.Count - 1; i >= 0; i--)
        {
            ConnateComboBurst burst = _comboBursts[i] with { Age = _comboBursts[i].Age + dt };
            if (burst.Age >= ConnateTuning.ComboBurstSeconds) _comboBursts.RemoveAt(i);
            else _comboBursts[i] = burst;
        }
        StepScoreReadout(dt);
        _immunityFloorLeft = Math.Max(0, _immunityFloorLeft - dt);
        BombDeliveryLeft = Math.Max(0, BombDeliveryLeft - dt);
        OverLimitPhase += dt * Math.PI * 2
            * ConnateTuning.OverLimitPulseHz * (1 + SizeFuse * ConnateTuning.OverLimitFuseBoost);
        // Wrapped, so a long session can't drift into the range where a double's step is coarser than a frame.
        if (OverLimitPhase > Math.PI * 2) OverLimitPhase %= Math.PI * 2;
        // ⚠ ComboDisplayLeft fades the banner; it must not bound the count, or the counter gains a rolling
        // time window by the back door. The count is reset by firing and by nothing else.
        ComboDisplayLeft = Math.Max(0, ComboDisplayLeft - dt);
        BoardClearLeft = Math.Max(0, BoardClearLeft - dt);

        // Terminal states never run shot clocks or spawn garbage. LimitBreach gets only a brief slow-motion settle.
        if (Phase == Stage.GameOver)
        {
            if (input.CrossPressed) RestartRound();
            return;
        }

        if (Phase == Stage.LimitBreach)
        {
            StepPhysics(dt * 0.25);
            // The final countdown holds while a merge is pending or its chain window is open, so a chain that is
            // still resolving can finish before the run ends. Physics and the chain window keep running, which
            // is what lets the hold lift.
            if (ContainmentProtected) PhaseTime = phaseBefore;
            else if (PhaseTime >= ConnateTuning.LimitBreachSeconds) SetPhase(Stage.GameOver);
            return;
        }

        StepPlayer(input, dt);
        if (Phase == Stage.Intro)
        {
            if (PhaseTime >= ConnateTuning.IntroSeconds
                || (input.CrossPressed && PhaseTime >= ConnateTuning.IntroSkipGuardSeconds))
            {
                SetPhase(Stage.Playing);
                _shotClockElapsed = 0;
                // The ✕ that skipped the intro may still be down; its hold began before the board was live.
                _pressConsumed = input.CrossDown;
            }
            return;
        }

        // ⚠ Manual fire happens on release, not on press. Holding winds the launcher: the clamps stay shut on
        // the piece and the craft sits on its wound frame, and letting go is what lets the shot go.
        //
        // The press edge is taken as a release too, for the frame where a pad reports a whole tap at once —
        // otherwise a fast enough tap is a shot that never leaves. Manual fire still gets first refusal on the
        // deadline frame, and a blocked attempt leaves the clock full so later steps can retry safely without
        // consuming the held piece or advancing cadence counters.
        if (_boss.Active)
        {
            StepEncounter(input, dt);
            return;
        }
        bool fired = StepFireInput(input, dt);
        SyncShotClockDeadline();
        // A loaded bomb is not on the clock: a bomb's value is entirely in where it lands, so no timer spends
        // it for the player. Pinned at zero rather than paused, so the arc reads as off, not frozen mid-sweep.
        if (HeldIsBomb) _shotClockElapsed = 0;
        else if (!fired)
        {
            _shotClockElapsed = Math.Min(_shotClockDeadline, _shotClockElapsed + dt);
            if (_shotClockElapsed >= _shotClockDeadline)
            {
                // The deadline shot is charged only if ✕ is down with the charge already complete; otherwise it
                // is a normal shot. Either way a press that was down when the clock spent the piece is consumed,
                // or its later release would fire the next piece the player never aimed.
                bool holding = _fireHeld;
                if (Fire(holding && HoldIsFullCharge(_sinceHold), holding ? DrawTension(_sinceHold) : 0) && holding)
                {
                    _fireHeld = false;
                    _pressConsumed = true;
                }
            }
        }
        StepGarbageSpawner(dt);
        StepPhysics(dt);
        CheckBoardClear();
        // Banked value may have moved in the two calls above; re-synced here so the readout and the next
        // step's clock agree within this frame rather than one step late.
        SyncShotClockDeadline();
        // The encounter a clear just began has no containment to run: the board is empty and the fuse is spent.
        if (!_boss.Active) StepSizeLimit(dt);
        if (Phase == Stage.Playing) AnnounceProgress();
    }

    /// <summary>Reads the fire button: winds the bow while it is held and releases the shot when it goes up.
    /// Returns whether a shot left.</summary>
    private bool StepFireInput(in ArcadeInput input, double dt)
    {
        bool wasHeld = _fireHeld;
        if (!input.CrossDown || input.CrossPressed) _pressConsumed = false;
        _fireHeld = input.CrossDown && !_pressConsumed;
        // Zeroed on the down edge only, so the wind-up plays once and then simply stays wound however long
        // the trigger is held.
        if (_fireHeld && !wasHeld) _sinceHold = 0;
        else if (_fireHeld) _sinceHold = Math.Min(RecoilCap, _sinceHold + dt);
        // ⚠ The charge is read from the hold clock, and a whole tap reported in one frame never had a down
        // edge for that clock to start from — _sinceHold is still whatever the last real hold left. Such a tap
        // is a normal shot, by definition, so it must not inherit that number.
        bool released = !input.CrossDown && (wasHeld || input.CrossPressed);
        return released && Fire(wasHeld && HoldIsFullCharge(_sinceHold), wasHeld ? DrawTension(_sinceHold) : 0);
    }

    /// <summary>One step of the boss-block encounter. Only the fight takes the fire button, and only for the
    /// bombs; the shot clock, the garbage spawner, the heap physics, containment and the stage are all stopped,
    /// and the block moves the bombs itself.
    ///
    /// <para>⚠ A press made while the block is falling or landing is spent: it neither winds the bow nor fires
    /// on release, and the release is refused with the guarded cue. A hold already in progress when the board
    /// cleared is treated the same way.</para></summary>
    private void StepEncounter(in ArcadeInput input, double dt)
    {
        if (FireLocked)
        {
            bool wasHeld = _fireHeld;
            if (!input.CrossDown && (wasHeld || _pressConsumed || input.CrossPressed)) RaiseGuarded();
            _fireHeld = false;
            _pressConsumed = input.CrossDown;
            _sinceHold = RecoilCap;
        }
        else StepFireInput(input, dt);

        _shotClockElapsed = 0;
        var context = new ConnateBossContext(
            HeldIsBomb ? ConnateTuning.BombRadius : ConnateTuning.RadiusForRank(HeldRank),
            HeldIsBomb, BombDelivering, TotalBombs);
        _bossHits.Clear();
        ConnateBossEvent events = _boss.Step(dt, context, _bombProjectiles, _bossHits);

        foreach (ConnateBossHit hit in _bossHits) PayBossHit(hit);
        if ((events & ConnateBossEvent.ShatterHeld) != 0) ShatterHeldPiece();
        if ((events & ConnateBossEvent.Ended) != 0) EndEncounter();
    }

    /// <summary>Raises the stage-up shout once per rise in <see cref="DifficultyIndex"/> and the mid-run
    /// "NEW BEST" shout the first time the collected score passes the best recorded before this run.
    ///
    /// <para>⚠ The best compared against is <see cref="HighScore"/>, which only moves when a run ends or the
    /// host seeds it, so it IS the run-start best for as long as play is live. A best of zero never counts —
    /// the first run has no record to beat, and shouting on its first mote would be noise.</para></summary>
    private void AnnounceProgress()
    {
        int stage = DifficultyIndex;
        if (stage > _announcedStageIndex)
        {
            StageShoutStage = stage + 1;
            StageShoutLeft = ConnateTuning.StageShoutSeconds;
            RecordRise(_announcedStageIndex + 1, stage + 1);
        }
        _announcedStageIndex = stage;

        if (!_newBestShouted && HighScore > 0 && ConnateRules.HostScore(CollectedScore) > HighScore)
        {
            _newBestShouted = true;
            NewBestShoutLeft = ConnateTuning.NewBestShoutSeconds;
            _cues |= Cue.NewBest;
        }

        // The stage-ups an encounter owed are shouted one after another, lowest first.
        PopOwedStageShout();
    }

    /// <summary>The run has risen from stage <paramref name="from"/> to stage <paramref name="to"/> (player-facing
    /// numbers). Every start stage it passes is unlocked, silently. A rise is the only thing that earns one: a run
    /// that starts at an unlocked stage rises past nothing.</summary>
    private void RecordRise(int from, int to)
    {
        foreach (int start in ConnateStarts.Stages)
        {
            if (start <= from || start > to) continue;
            if (_starts.Reach(start)) _settingsDirty = true;
        }
    }

    /// <summary>True while a merge is pending (its anticipation included) or the chain-continuation window is
    /// open. Containment pressure pauses for exactly this long: the size fuse neither rises nor starts a
    /// breach, the body-cap shortcut waits, and a breach already under way holds its final countdown. The fuse
    /// is retained rather than reset, and recovery still drains it while the cluster is inside the limit.
    /// A chain depth left over from a chain that has expired protects nothing.</summary>
    public bool ContainmentProtected =>
        _pendingMerges.Count > 0 || (_chainDepth > 0 && _chainTimeLeft > 0);

    /// <summary>Emptying the field starts the boss-block encounter, once per emptying.
    ///
    /// <para>⚠ The reward's base is the BANKED total (<see cref="ExplodedValue"/>), which already holds motes
    /// still in flight: a clear is triggered by the very explosion that launched them, and a base of what has
    /// landed would be momentarily near zero.</para></summary>
    private void CheckBoardClear()
    {
        if (_bodies.Count > 0) { _boardOccupied = true; return; }
        if (!_boardOccupied) return;
        _boardOccupied = false;
        BeginEncounter();
    }

    /// <summary>The board has just emptied. Firing locks, everything in flight is dropped, the clocks stop, a
    /// bomb is added (to the three-bomb total) and the encounter's reward and stage are fixed.</summary>
    private void BeginEncounter()
    {
        int stage = DifficultyIndex;
        long reward = ConnateBossBlock.RewardFor(ExplodedValue, stage);
        if (TotalBombs < ConnateTuning.MaximumBombs)
        {
            PendingBombs++;
            _cues |= Cue.BombEarned;
        }
        // Bombs already in flight and merges still reserving bodies are not part of the encounter.
        _bombProjectiles.Clear();
        _pendingMerges.Clear();
        SizeFuse = 0;
        _warningLatched = false;
        _shotClockElapsed = 0;
        ComboDisplayLeft = 0;
        _owedStageShouts.Clear();
        _boss.Begin(reward, TotalBombs, stage);
        BoardClearLeft = ConnateTuning.BoardClearDisplaySeconds;
        _cues |= Cue.BoardClear;
    }

    /// <summary>The landing ring has reached the loaded piece. A numbered tile shatters and the first waiting
    /// bomb comes in through the ordinary delivery, with NEXT parked behind it; a bomb already loaded stays.</summary>
    private void ShatterHeldPiece()
    {
        double sine = Math.Sin(PlayerAngle), cosine = Math.Cos(PlayerAngle);
        if (!HeldIsBomb)
        {
            _bombExplosions.Add(new ConnateBombExplosion(
                sine * ConnateTuning.LaunchRadius, -cosine * ConnateTuning.LaunchRadius, HeldRank, 0));
            if (PendingBombs > 0) LoadPendingBomb();
        }
    }

    /// <summary>A bomb has reached the block. The hit banks at once and pays through the ordinary gold motes
    /// (deterministic, no draw), blasts at the contact point, and the block's own state — cracks, flash, kick —
    /// has already moved.</summary>
    private void PayBossHit(ConnateBossHit hit)
    {
        long reward = _boss.Reward;
        ExplodedValue = ConnateRules.SaturatingAdd(ExplodedValue, reward);
        SpawnScoreMotes(hit.BombId, ConnateTuning.BossMoteRank, reward, _boss.X, _boss.Y);
        _bombExplosions.Add(new ConnateBombExplosion(hit.X, hit.Y, ConnateTuning.BossHitBlastRank, 0));
        _cues |= Cue.BombExplode;
    }

    /// <summary>The fragments have gone. Play resumes as a fresh run's board would: the preserved NEXT is on the
    /// rail, the shot clock is new, and an opening blob sits at the centre with a new garbage interval. The stage
    /// the hits earned is released last, and every stage-up it owes is shouted in ascending order.</summary>
    private void EndEncounter()
    {
        int captured = _boss.CapturedStage;
        _boss.Clear();
        HeldIsBomb = false;
        if (_queuedRank >= 0)
        {
            HeldRank = _queuedRank;
            HeldHue = _queuedHue;
            _queuedRank = -1;
            _queuedHue = ConnateRules.HueAzure;
        }
        else DrawNextHeld();
        BombDeliveryLeft = 0;
        _shotClockElapsed = 0;
        _garbageTimeLeft = DrawGarbageInterval();
        SeedOpeningGarbage();
        _boardOccupied = true;

        int reached = DifficultyIndex;
        for (int index = captured + 1; index <= reached; index++) _owedStageShouts.Enqueue(index + 1);
        // The encounter's own hits can pass a start stage the clear itself did not.
        RecordRise(captured + 1, reached + 1);
        _announcedStageIndex = Math.Max(_announcedStageIndex, reached);
        PopOwedStageShout();
        SyncShotClockDeadline();
    }

    private void PopOwedStageShout()
    {
        if (StageShoutLeft > 0 || !_owedStageShouts.TryDequeue(out int stage)) return;
        StageShoutStage = stage;
        StageShoutLeft = ConnateTuning.StageShoutSeconds;
    }

    /// <summary>Starting at stage N banks exactly that stage's threshold, so the run opens at N with N's pace and
    /// price and the score already stands there. It counts toward the best from the first step, which also keeps
    /// the mid-run NEW BEST from firing on a score the player is handed. Nothing is announced: the run did not
    /// rise to this stage.</summary>
    private void SeedStart(int stage)
    {
        long threshold = ConnateTuning.StageThresholdFor(stage);
        if (threshold <= 0) return;
        ExplodedValue = threshold;
        CollectedScore = threshold;
        DisplayedScore = threshold;
        _announcedStageIndex = DifficultyIndex;
        SyncShotClockDeadline();
        HighScore = Math.Max(HighScore, ConnateRules.HostScore(threshold));
    }

    /// <summary>The drawn score chases the real one. A proportional chase with a floor rather than a fixed
    /// rate, so a two-point merge still ticks and a 384-point explosion still lands quickly.</summary>
    private void StepScoreReadout(double dt)
    {
        double target = LiveScore;
        double gap = target - DisplayedScore;
        if (Math.Abs(gap) < 0.5) { DisplayedScore = target; return; }
        double step = Math.Max(Math.Abs(gap) * ConnateTuning.ScoreCountUpPerSec * dt, 6.0 * dt);
        DisplayedScore += Math.Sign(gap) * Math.Min(Math.Abs(gap), step);
    }

    private void StepPlayer(in ArcadeInput input, double dt)
    {
        // Default model: stick left/right drives the rim, spinner-style. The absolute-angle model below runs
        // only under RadialAiming — see ArcadeAim.
        if (!RadialAiming)
        {
            double before = PlayerAngle;
            // Shared rim model: instant tilt-proportional speed, fractional brake on release. _rimVelocity is
            // a sub-second transient and is not serialized — a resumed run starts at rest.
            _rimVelocity = ArcadeAim.StepVelocity(_rimVelocity, input, ConnateTuning.PlayerDegPerSec, dt);
            PlayerAngle = Norm(PlayerAngle + _rimVelocity * dt);
            if (Math.Abs(Wrap(PlayerAngle - before)) > 0.002) _cues |= Cue.Move;
            return;
        }

        // ScreenAngle is an absolute requested rim position; shortest-arc interpolation keeps stick recentering
        // from adding momentum.
        if (input.Magnitude < ConnateTuning.PlayerDeadzone) return;
        double target = Norm(input.ScreenAngle);
        double difference = Wrap(target - PlayerAngle);
        double maxStep = ConnateTuning.PlayerDegPerSec * Math.PI / 180.0 * dt;
        double old = PlayerAngle;
        PlayerAngle = Math.Abs(difference) <= Math.Max(maxStep, ConnateTuning.PlayerSnapDeg * Math.PI / 180.0)
            ? target : Norm(PlayerAngle + Math.Sign(difference) * maxStep);
        if (Math.Abs(Wrap(PlayerAngle - old)) > 0.002) _cues |= Cue.Move;
    }

    /// <summary>Has a hold of <paramref name="holdSeconds"/> reached a full charge? The only thing a hold's
    /// length decides: below it the release is a normal shot, at or past it a slam, and nothing longer adds
    /// anything.</summary>
    private static bool HoldIsFullCharge(double holdSeconds) =>
        holdSeconds >= ConnateTuning.FullChargeSeconds - ConnateTuning.FullChargeEpsilon;

    /// <summary>How far the bow is drawn after a hold of <paramref name="holdSeconds"/>, 0..1 — the same ramp
    /// the renderer draws while ✕ is down.</summary>
    private static double DrawTension(double holdSeconds) =>
        Math.Clamp(holdSeconds / Math.Max(1e-3, ConnateTuning.FullChargeSeconds), 0, 1);

    /// <summary>Attempts one committed shot. Returns false only for cooldown/body-cap/muzzle obstruction.
    ///
    /// <para><paramref name="charged"/> launches a numbered tile at
    /// <see cref="ConnateTuning.ChargedLaunchMultiplier"/> times <see cref="ConnateTuning.LaunchSpeed"/> and
    /// flags the body (see <see cref="ConnateBody.Charged"/>). <paramref name="drawTension"/> is how far the
    /// bow was actually drawn, for the release spring. ⚠ Bombs ignore charge: a bomb's whole job is to reach
    /// the spot it was aimed at, at its fixed speed.</para></summary>
    private bool Fire(bool charged, double drawTension)
    {
        // Refused here rather than at the call sites, so the auto-fire path also can't spend a bomb still in
        // flight from the meter.
        if (BombDelivering || FireLocked) { RaiseGuarded(); return false; }
        if (_fireCooldown > 0 || (!HeldIsBomb && _bodies.Count >= ConnateTuning.MaximumBodies))
        {
            RaiseGuarded();
            return false;
        }

        double outwardX = Math.Sin(PlayerAngle), outwardY = -Math.Cos(PlayerAngle);
        double x = outwardX * ConnateTuning.LaunchRadius;
        double y = outwardY * ConnateTuning.LaunchRadius;
        double heldRadius = HeldIsBomb ? ConnateTuning.BombRadius : ConnateTuning.RadiusForRank(HeldRank);
        // Never rely on collision correction to repair a bad spawn: large high-rank or garbage bodies can block
        // the muzzle, in which case both manual and automatic fire wait until the lane physically clears.
        if (_bodies.Any(body =>
        {
            double clearance = (heldRadius + body.Radius) * ConnateTuning.MuzzleClearance;
            return DistanceSquared(body.X, body.Y, x, y) < clearance * clearance;
        }))
        {
            RaiseGuarded();
            return false;
        }

        long shotId = _nextShotId++;
        // Bombs remain swept projectiles outside the heap list. Numbered shots become ordinary bodies immediately
        // and are initially excluded from the size envelope while travelling inward.
        if (HeldIsBomb)
        {
            _bombProjectiles.Add(new ConnateBombProjectile
            {
                Id = shotId, X = x, Y = y,
                VelocityX = -outwardX * ConnateTuning.LaunchSpeed,
                VelocityY = -outwardY * ConnateTuning.LaunchSpeed,
            });
            SizeLimitImmune = true;
            SizeFuse = 0;
            _warningLatched = false;
            // Immunity lasts until the next tile is fired OR this long, whichever is longer.
            _immunityFloorLeft = ConnateTuning.BombImmunityMinimumSeconds;
        }
        else
        {
            // Firing a numbered tile ends immunity only once the floor has also run out; StepSizeLimit reads both.
            SizeLimitImmune = false;
            double launch = ConnateTuning.LaunchSpeed * (charged ? ConnateTuning.ChargedLaunchMultiplier : 1);
            _bodies.Add(new ConnateBody
            {
                Id = _nextBodyId++, Rank = HeldRank, X = x, Y = y, Hue = HeldHue,
                VelocityX = -outwardX * launch,
                VelocityY = -outwardY * launch,
                SizeArmed = false, ShotId = shotId, Charged = charged,
                // Only ranks 0-1 spin; direction alternates with the shot id so shots don't look copy-pasted.
                SpinRate = HeldRank <= 1
                    ? (shotId % 2 == 0 ? 1 : -1) * ConnateTuning.PieceSpinRevsPerSec * Math.PI * 2
                    : 0,
            });
        }
        _fireCooldown = ConnateTuning.FireCooldownSeconds;
        _sinceFire = 0;
        // What the bow was actually at, not the shot's power: a deadline shot with ✕ up was never drawn, and a
        // deadline shot mid-hold springs from the draw the player had built.
        _shotDrawTension = Math.Clamp(drawTension, 0, 1);
        _shotClockElapsed = 0;
        // This reset defines "between shots" and is the only thing that clears the combo count.
        _mergesSinceShot = 0;
        ComboCount = 0;
        // Bombs are earned, not interleaved into the feed by shot count (see AwardCombo), and an earned bomb
        // waits for the numbered tile in the craft to fire rather than displacing it.
        if (HeldIsBomb)
        {
            // The next earned bomb loads straight away, behind the same parked tile; the last one hands the
            // rail back to that tile, or to a fresh draw when nothing was parked.
            if (PendingBombs > 0) LoadPendingBomb();
            else
            {
                HeldIsBomb = false;
                // During the boss-block encounter the rail stays empty until the block breaks, so the
                // preserved NEXT is not handed over mid-fight (EndEncounter returns it).
                if (!_boss.Active)
                {
                    if (_queuedRank >= 0) { HeldRank = _queuedRank; HeldHue = _queuedHue; _queuedRank = -1; }
                    else { DrawNextHeld(); }
                }
            }
        }
        else if (PendingBombs > 0) LoadPendingBomb();
        else { DrawNextHeld(); }
        _cues |= Cue.Fire;
        return true;
    }

    /// <summary>Puts the next earned bomb on the rail, after the numbered tile (or the bomb) that was in the
    /// craft has fired. The tile that was NEXT is parked behind it — NEXT is preserved, never spent on a
    /// bomb — and the preview is refilled from the feed, which is the same single draw the unbombed reload
    /// makes. The bomb flies in from the meter and cannot be fired until it lands.</summary>
    private void LoadPendingBomb()
    {
        if (_queuedRank < 0)
        {
            if (_nextRank < 0) DrawNextTile();
            _queuedRank = _nextRank;
            _queuedHue = _nextHue;
            DrawNextTile();
        }
        PendingBombs--;
        HeldIsBomb = true;
        BombDeliveryLeft = ConnateTuning.BombDeliverySeconds;
    }

    private void StepGarbageSpawner(double dt)
    {
        // Garbage timing uses active Playing time only. If the hard body safety cap is full, postpone rather than
        // silently losing the scheduled pressure piece or forcing it into an invalid spawn. The stage scales how
        // fast the stored countdown is consumed; the 10–30 s draw itself never changes.
        _garbageTimeLeft -= dt / ConnateTuning.GarbageCountdownScale(DifficultyIndex);
        if (_garbageTimeLeft > 0) return;
        // Nothing arrives while a bomb is loaded: a bomb has no shot clock either, so the player is being
        // given time to read the board.
        //
        // ⚠ Deferred, not paused — the arrival is retried a second later. Pausing the countdown outright
        // opens a full stall: with a bomb held there is no auto-fire and no garbage, so a player who never
        // fires is never threatened again.
        if (HeldIsBomb || BombDelivering)
        {
            _garbageTimeLeft = 1.0;
            return;
        }
        if (_bodies.Count >= ConnateTuning.MaximumBodies)
        {
            _garbageTimeLeft = 1.0;
            return;
        }
        // Spawn beyond the hard physics wall but within the visual outer disc. EnteringPlayfield is the narrow,
        // one-way exception that lets this intentional arrival cross inward before normal containment takes over.
        double angle = NextDouble() * Math.PI * 2;
        double garbageRadius = DrawGarbageRadius();
        double outwardX = Math.Sin(angle), outwardY = -Math.Cos(angle);
        _bodies.Add(new ConnateBody
        {
            Id = _nextBodyId++, Rank = 0,
            X = outwardX * ConnateTuning.GarbageSpawnRadius,
            Y = outwardY * ConnateTuning.GarbageSpawnRadius,
            VelocityX = -outwardX * ConnateTuning.GarbageEntrySpeed,
            VelocityY = -outwardY * ConnateTuning.GarbageEntrySpeed,
            IsGarbage = true, EnteringPlayfield = true, SizeArmed = false,
            GarbageRadius = garbageRadius,
        });
        _garbageTimeLeft += DrawGarbageInterval();
        _cues |= Cue.GarbageArrive;
    }

    private double DrawGarbageRadius()
    {
        // Only complete numbered circles define the size ceiling; complementary 1/2 pieces and garbage are not
        // circles. Include the held 3+ orb because it is visibly onscreen when the arrival is chosen.
        var circleRadii = _bodies.Where(body => !body.IsGarbage && body.Rank >= 2)
            .Select(body => body.Radius).ToList();
        if (!HeldIsBomb && HeldRank >= 2) circleRadii.Add(ConnateTuning.RadiusForRank(HeldRank));
        circleRadii.Sort((a, b) => b.CompareTo(a));
        double ceiling = circleRadii.Count >= 2 ? circleRadii[1] * ConnateTuning.GarbageScale : ConnateTuning.GarbageRadius;
        ceiling = Math.Max(ConnateTuning.GarbageRadius, ceiling);
        return ConnateTuning.GarbageRadius + NextDouble() * (ceiling - ConnateTuning.GarbageRadius);
    }

    private void StepPhysics(double dt)
    {
        // Bomb motion must resolve before heap physics so its deletion/blast participates in this frame's
        // collisions. It shares the garbage-event list with merge resolution, so bomb-swept and merge-scrubbed
        // blobs reach the renderer and the combo count by the same route.
        var garbageEvents = new List<ConnateGarbageClear>();
        StepBombs(dt, garbageEvents);
        List<ConnateMergeEvent> events = ConnatePhysics.Step(
            _bodies, _pendingMerges, dt, ref _nextBodyId, garbageEvents);
        _garbageClears.AddRange(garbageEvents);
        if (garbageEvents.Count > 0) _cues |= Cue.GarbageCleared;
        foreach (ConnateMergeEvent merge in events)
        {
            if (_chainTimeLeft > 0 && merge.ShotId == _lastMergeShotId) _chainDepth++;
            else _chainDepth = 1;
            _lastMergeShotId = merge.ShotId;
            _chainTimeLeft = ConnateTuning.ChainContinuationSeconds;
            _mergeFlashes.Add(new ConnateMergeFlash(merge.X, merge.Y, merge.ResultRank, _chainDepth, 0));
            _cues |= _chainDepth > 1 ? Cue.Chain : Cue.Merge;

            // Combos are counted between shots: every merge since the last successful fire, the first
            // recorded but not scoring, so three merges off one shot read "COMBO ×2" and pay 2.
            // ⚠ Scoring must not use _chainDepth — that pairs merges inside a rolling
            // ChainContinuationSeconds window, so a slow cascade silently restarts and stops paying.
            // _chainDepth drives the merge flash intensity only, which wants the "how fast" reading.
            CountTowardCombo(merge.X, merge.Y);
        }

        // A cleared garbage block counts like a merge, through the same counter.
        // ⚠ Includes garbage a bomb swept (see StepBombs): several clears can cash into one combo and, with
        // the exponential charge, pay for the next bomb outright. Self-limiting only because garbage arrives
        // on a slow timer — look here first if bombs start feeling free.
        foreach (ConnateGarbageClear clear in garbageEvents) CountTowardCombo(clear.X, clear.Y);
    }

    /// <summary>One more thing-that-counts since the last shot: the combo counter, the shot clock, and the
    /// award past the first. A merge resets the shot clock — the clock nudges a player who has stopped
    /// deciding, and a board still resolving a cascade is not idle.</summary>
    private void CountTowardCombo(double x, double y)
    {
        _mergesSinceShot++;
        _shotClockElapsed = 0;
        if (_mergesSinceShot > 1) AwardCombo(x, y);
    }

    /// <summary>One combo — bump the on-screen counter, add charge, and earn a bomb when the charge fills.
    /// The bomb is automatic, and waits as pending until the tile in the craft has fired.</summary>
    private void AwardCombo(double x = 0, double y = 0)
    {
        // The raw merge count, so the second merge reads "COMBO ×2"; the first never awards.
        ComboCount = _mergesSinceShot;
        ComboDisplayLeft = ConnateTuning.ComboDisplaySeconds;

        // Charge grows exponentially with depth: ×2 pays 1, ×3 pays 2, ×4 pays 4, ×5 pays 8, so deep cascades
        // beat the same number of merges spread across separate shots.
        int award = ChargeForCombo(ComboCount);
        int cost = BombChargeCost;
        int pip = Math.Min(BombCharge, cost - 1);
        BombCharge += award;

        // The burst names the pip it just filled. Captured before the charge is spent below, or a completing
        // combo would point at pip 0 of an empty meter.
        bool completes = BombCharge >= cost;
        _comboBursts.Add(new ConnateComboBurst(x, y, ComboCount, pip, completes, 0));
        _cues |= Cue.ComboStep;
        if (!completes) return;

        // An earned bomb waits: the numbered tile in the craft is the player's shot, and a bomb yanking it away
        // mid-aim is the thing this avoids. It loads when that tile fires (see Fire).
        // A loop, because an exponential award can cross the threshold more than once: at ×5 a single merge
        // pays eight, which is more than one bomb's worth.
        while (BombCharge >= cost && TotalBombs < ConnateTuning.MaximumBombs)
        {
            BombCharge -= cost;
            PendingBombs++;
        }
        // At the cap the leftover charge is held just under a full meter rather than discarded.
        if (TotalBombs >= ConnateTuning.MaximumBombs)
            BombCharge = Math.Min(BombCharge, cost - 1);

        _cues |= Cue.BombEarned;
    }

    /// <summary>Charge paid by a combo of <paramref name="comboCount"/> merges: 1, 2, 4, 8, … doubling per
    /// extra merge. Clamped so a pathological cascade can't overflow the shift.</summary>
    public static int ChargeForCombo(int comboCount) =>
        comboCount < 2 ? 0 : 1 << Math.Min(comboCount - 2, 20);

    /// <summary>Bombs the player has earned and not yet fired, loaded plus pending; the cap is a total, not a
    /// per-slot one.</summary>
    public int TotalBombs => (HeldIsBomb ? 1 : 0) + PendingBombs;

    /// <summary><paramref name="garbageEvents"/> collects every garbage blob a detonation sweeps, so it
    /// reaches the renderer and the combo counter by the same route a merge-scrubbed one does.</summary>
    private void StepBombs(double dt, List<ConnateGarbageClear> garbageEvents)
    {
        for (int i = _bombProjectiles.Count - 1; i >= 0; i--)
        {
            ConnateBombProjectile bomb = _bombProjectiles[i];
            double oldX = bomb.X, oldY = bomb.Y;
            bomb.Age += dt;
            bomb.X += bomb.VelocityX * dt;
            bomb.Y += bomb.VelocityY * dt;

            // Segment-circle intersection avoids tunnelling at the high launch velocity. ⚠ Garbage is skipped
            // as a target — a littered board would otherwise swallow every bomb at its edge; garbage is
            // cleared on detonation instead (below).
            ConnateBody? hit = null;
            double bestT = double.PositiveInfinity;
            foreach (ConnateBody body in _bodies)
            {
                if (body.IsGarbage) continue;
                double contact = bomb.Radius + body.Radius;
                double t = SegmentContact(oldX, oldY, bomb.X, bomb.Y, body.X, body.Y, contact);
                if (t < bestT) { bestT = t; hit = body; }
            }
            if (hit is not null)
            {
                double blastX = hit.X, blastY = hit.Y;
                int destroyedRank = hit.Rank;
                // Bank value before physical removal. The ledger is hidden until game over and survives snapshots.
                ExplodedValue = ConnateRules.SaturatingAdd(ExplodedValue, hit.Value);
                SpawnScoreMotes(hit.Id, hit.Rank, hit.Value, blastX, blastY);
                _bodies.Remove(hit);
                _pendingMerges.RemoveAll(bond => bond.AId == hit.Id || bond.BId == hit.Id);
                ConnatePhysics.ApplyBlast(_bodies, blastX, blastY);
                // Pending bonds survive the blast; only unbonded neighbours absorb the full impulse.
                ConnatePhysics.Contain(_bodies);
                // A detonation clears every garbage block on the board — the bomb is the only answer to a
                // silted-up board. Recorded as ordinary clear events, so they animate exactly like a
                // merge-scrubbed blob and count toward the combo the same way (see StepPhysics).
                for (int g = _bodies.Count - 1; g >= 0; g--)
                {
                    ConnateBody garbage = _bodies[g];
                    if (!garbage.IsGarbage) continue;
                    garbageEvents.Add(new ConnateGarbageClear(garbage.X, garbage.Y, garbage.Radius,
                                                              garbage.Id, 0, garbage.Rotation));
                    _bodies.RemoveAt(g);
                }

                _bombExplosions.Add(new ConnateBombExplosion(blastX, blastY, destroyedRank, 0));
                _bombProjectiles.RemoveAt(i);
                _cues |= Cue.BombExplode;
            }
            // No tile to hit: the bomb still detonates when it reaches the centre, paying a flat sum, so a bomb
            // spent on an empty field is a reward rather than a dud. Garbage is not a target (above) but is
            // still swept, so a garbage-only board counts as empty here.
            else if (!_bodies.Any(body => !body.IsGarbage)
                     && SegmentContact(oldX, oldY, bomb.X, bomb.Y, 0, 0, bomb.Radius) < double.PositiveInfinity)
            {
                long value = ConnateTuning.BombEmptyFieldValue;
                int rank = NearestRankForValue(value);
                ExplodedValue = ConnateRules.SaturatingAdd(ExplodedValue, value);
                SpawnScoreMotes(bomb.Id, rank, value, 0, 0);
                for (int g = _bodies.Count - 1; g >= 0; g--)
                {
                    ConnateBody garbage = _bodies[g];
                    garbageEvents.Add(new ConnateGarbageClear(garbage.X, garbage.Y, garbage.Radius,
                                                              garbage.Id, 0, garbage.Rotation));
                    _bodies.RemoveAt(g);
                }
                _bombExplosions.Add(new ConnateBombExplosion(0, 0, rank, 0));
                _bombProjectiles.RemoveAt(i);
                _cues |= Cue.BombExplode;
            }
            else if (bomb.Age >= ConnateTuning.BombLifetimeSeconds
                     || Math.Sqrt(bomb.X * bomb.X + bomb.Y * bomb.Y) > ConnateTuning.OuterHardLimitRadius + 0.1)
                _bombProjectiles.RemoveAt(i);
        }
    }

    /// <summary>Pays a destroyed tile out as flying nuggets. Collecting is the only way Connate scores, so
    /// this is the run's payoff and is deliberately scaled by what was destroyed: a higher rank pays more
    /// nuggets, bigger ones, over a longer stream.
    ///
    /// <para>Everything here is deterministic off the body id — no RNG draw — because a draw would perturb the
    /// feed and make an explosion change which tile you are handed next.</para>
    ///
    /// <para>⚠ The split must re-add to <c>hit.Value</c> exactly. Under the collected-motes-only rule a
    /// rounding loss is a scoring bug, not a cosmetic one, so shares are cut from a running rounded cumulative
    /// total rather than by rounding each share on its own.</para></summary>
    private void SpawnScoreMotes(long id, int hitRank, long total, double blastX, double blastY)
    {
        int rank = Math.Max(0, hitRank);
        int motes = Math.Clamp(
            (int)Math.Round(ConnateTuning.ScoreMotesMinimum + rank * ConnateTuning.ScoreMotesPerRank),
            Math.Max(1, ConnateTuning.ScoreMotesMinimum), Math.Max(1, ConnateTuning.ScoreMotesMaximum));
        double baseSize = Math.Min(ConnateTuning.ScoreMoteSizeMaximum,
                                   1 + rank * ConnateTuning.ScoreMoteSizePerRank);

        // Size and delay both climb across the burst, so the stream ends on its largest nugget and its largest
        // tick. A crescendo is what makes a big tile feel like a big tile; a uniform spray does not.
        var sizes = new double[motes];
        double weightTotal = 0;
        for (int m = 0; m < motes; m++)
        {
            double along = motes == 1 ? 1 : m / (double)(motes - 1);
            ulong hash = (ulong)(id * 0x9E3779B1L + m * 0x45D9F3BL);
            double jitter = 0.92 + (hash & 255) / 255.0 * 0.16;
            sizes[m] = baseSize * jitter * (ConnateTuning.ScoreMoteSizeSpreadLow
                + along * (ConnateTuning.ScoreMoteSizeSpreadHigh - ConnateTuning.ScoreMoteSizeSpreadLow));
            weightTotal += sizes[m] * sizes[m];   // by area: a nugget twice as wide should read as four times the haul
        }

        long paid = 0;
        double weightSoFar = 0;
        for (int m = 0; m < motes; m++)
        {
            double along = motes == 1 ? 1 : m / (double)(motes - 1);
            ulong hash = (ulong)(id * 0x2545F491L + m * 0x27220A95L);
            weightSoFar += sizes[m] * sizes[m];
            long upTo = weightTotal <= 0 ? total
                : (long)Math.Round(total * (weightSoFar / weightTotal));
            long value = m == motes - 1 ? total - paid : Math.Max(0, upTo - paid);
            paid += value;

            // Fanned rather than random, offset per body so consecutive explosions don't throw an identical
            // pinwheel, and with the scatter reach varied so the burst has depth.
            double angle = (m + 0.5) / motes * Math.PI * 2 + id * 0.7;
            double reach = 0.45 + 0.75 * ((hash >> 8 & 255) / 255.0);
            double delay = ConnateTuning.ScoreMoteScatterSeconds
                + along * ConnateTuning.ScoreMoteStaggerSeconds
                + (hash & 255) / 255.0 * ConnateTuning.ScoreMoteStaggerSeconds * 0.12;

            _scoreMotes.Add(new ConnateScoreMote(blastX, blastY,
                Math.Sin(angle) * reach, -Math.Cos(angle) * reach, 0, value,
                sizes[m], delay, id * 31 + m));
        }
    }

    /// <summary>The ladder rank whose value is closest to <paramref name="value"/>, for sizing blast art off a
    /// payout that is not itself a tile.</summary>
    private static int NearestRankForValue(long value)
    {
        int best = 0;
        for (int rank = 0; rank <= ConnateRules.MaximumRank; rank++)
        {
            if (Math.Abs(ConnateRules.ValueForRank(rank) - value) < Math.Abs(ConnateRules.ValueForRank(best) - value))
                best = rank;
            if (ConnateRules.ValueForRank(rank) > value) break;
        }
        return best;
    }

    private void StepSizeLimit(double dt)
    {
        // Bomb immunity suppresses both fuse accumulation and the emergency body-cap loss. It runs until the
        // next numbered shot fires (see Fire) OR the floor expires, whichever is longer — hence either, not both;
        // never merely until the bomb animation finishes.
        if (SizeLimitImmune || _immunityFloorLeft > 0)
        {
            SizeFuse = 0;
            _warningLatched = false;
            return;
        }
        // A merge in progress or a live chain suspends every way of losing: the fuse holds where it is, the
        // body-cap shortcut waits, and no breach starts. Recovery below is NOT suspended, so a cluster that
        // has come back inside drains its fuse as usual.
        bool protectedNow = ContainmentProtected;
        if (!protectedNow && _bodies.Count >= ConnateTuning.MaximumBodies)
        {
            SizeFuse = 1;
            BeginLimitBreach();
            return;
        }

        // A recoverable fuse absorbs transient collision spikes. Deeper penetration charges faster, while a fully
        // contained heap drains continuously and can unlatch the warning cue.
        double excess = Math.Max(0, ClumpExtent - ConnateTuning.ClumpLimitRadius);
        if (excess > 0)
        {
            if (!protectedNow)
            {
                double grace = Math.Max(0.1, ConnateTuning.BarelyOversizeGraceSeconds);
                double multiplier = 1 + excess / Math.Max(0.01, ConnateTuning.SoftExcessBand);
                SizeFuse = Math.Min(1, SizeFuse + dt / grace * multiplier);
            }
        }
        else SizeFuse = Math.Max(0, SizeFuse - ConnateTuning.FuseRecoveryPerSec * dt);

        if (!_warningLatched && SizeFuse >= 0.55) { _warningLatched = true; _cues |= Cue.Warning; }
        if (_warningLatched && SizeFuse <= 0.05) { _warningLatched = false; _cues |= Cue.Recover; }
        if (!protectedNow && SizeFuse >= 1) BeginLimitBreach();
    }

    private void BeginLimitBreach()
    {
        // Motes still in the air are credited, not dropped — they are value the player already earned.
        foreach (ConnateScoreMote mote in _scoreMotes)
            CollectedScore = ConnateRules.SaturatingAdd(CollectedScore, mote.Value);
        _scoreMotes.Clear();

        // The run's final number is what was collected — the board itself is worth nothing. FieldSum and
        // ExplodedValue survive as diagnostics and as the bomb ledger.
        FinalFieldSum = CollectedScore;
        int hostScore = ConnateRules.HostScore(FinalFieldSum);
        bool newBest = hostScore > HighScore;
        if (newBest) HighScore = hostScore;
        _cues |= Cue.GameOver;
        // A run that already shouted NEW BEST when it passed the record does not play the voice a second time.
        if (newBest && !_newBestShouted) _cues |= Cue.NewBest;
        StageShoutLeft = 0;
        NewBestShoutLeft = 0;
        SetPhase(Stage.LimitBreach);
    }

    /// <summary>Family for the next held tile: a flat coin flip off the run's own RNG, never balanced against
    /// what's on the board. Don't smooth it — being handed the "wrong" colour for a ripe pair is the tension
    /// the mechanic exists to create.</summary>
    private int DrawHue() => (NextUInt64() & 1) == 0 ? ConnateRules.HueAzure : ConnateRules.HueEmber;

    /// <summary>Draw the next held tile, rank then family, in that RNG order — a resumed run replays the same
    /// stream only if the order is stable. Auto-blend is applied here too: the advanced pool can hand out a
    /// tile at or above the threshold once the board has one, and it must arrive blended like any other.</summary>
    private void DrawNextHeld(bool baseOnly = false)
    {
        // One tile is drawn ahead so the HUD can show what comes after the loaded one. The pre-drawn tile is
        // what lands here; the draw below refills the preview. A reset (baseOnly) drops any stale preview so
        // the opening pair comes from the base bag.
        if (baseOnly) _nextRank = -1;
        if (_nextRank >= 0) { HeldRank = _nextRank; HeldHue = _nextHue; }
        else
        {
            HeldRank = baseOnly ? DrawBaseRank() : DrawFeedRank();
            HeldHue  = ConnateRules.WithAutoBlend(HeldRank, DrawHue());
        }
        DrawNextTile(baseOnly);
    }

    /// <summary>Refills the preview slot: rank then family, in that RNG order.</summary>
    private void DrawNextTile(bool baseOnly = false)
    {
        _nextRank = baseOnly ? DrawBaseRank() : DrawFeedRank();
        _nextHue  = ConnateRules.WithAutoBlend(_nextRank, DrawHue());
    }

    private int _nextRank = -1, _nextHue = ConnateRules.HueAzure;

    /// <summary>What the payload will hold after the current piece is fired, for the HUD's next-tile slot: an
    /// earned bomb first (it loads the moment the held piece leaves, whether that piece is a tile or a bomb),
    /// then the tile parked behind a loaded bomb, then the pre-drawn feed tile. Read-only — the sim's own
    /// reload logic in Fire is the authority, this mirrors its order.</summary>
    public (bool Bomb, int Rank, int Hue) NextPreview()
    {
        if (PendingBombs > 0) return (true, -1, 0);
        if ((HeldIsBomb || RailEmpty) && _queuedRank >= 0) return (false, _queuedRank, _queuedHue);
        return (false, _nextRank, _nextHue);
    }

    private int DrawFeedRank()
    {
        // First choose base bag versus advanced injection. Garbage is excluded from HighestRank, and base 1/2/3
        // remain legal exceptions even when the heap has no numbered bodies.
        int highest = Math.Max(2, HighestRank);
        // ── The board's top tile is never handed back ──
        // Above value 6 the advanced pool stops one rank short of the highest numbered body, so the biggest
        // thing on the board can only grow by being built up to — never by being gifted its own partner. That
        // gift was the cheapest scoring line in the game and it arrived by luck rather than by play.
        //
        // 1, 2, 3 and 6 are exempt (ConnateTuning.FeedTopMatchExemptRank): they are the base bag's own range
        // plus the first double, and gating them would leave an early board with nothing legal to feed.
        int maximumAdvanced = Math.Max(2,
            highest > ConnateTuning.FeedTopMatchExemptRank ? highest - 1 : highest);
        double chance = Math.Clamp((highest - 2) * ConnateTuning.AdvancedChancePerRank,
            0, ConnateTuning.AdvancedChanceMaximum);
        if (maximumAdvanced >= 3 && NextDouble() < chance)
        {
            int first = 3;
            int unlockedCount = maximumAdvanced + 1;
            // Exponential decay favors low advanced ranks. Applying a second multiplier to the exact top 30%
            // makes newly unlocked values rare without ever drawing above the highest numbered field rank.
            int topStart = Math.Max(first, (int)Math.Ceiling(unlockedCount * (1 - ConnateTuning.TopTierFraction)));
            double total = 0;
            for (int rank = first; rank <= maximumAdvanced; rank++)
            {
                double weight = Math.Exp(-ConnateTuning.AdvancedRankWeightDecay * (rank - first));
                if (rank >= topStart) weight *= ConnateTuning.TopTierWeightMultiplier;
                total += weight;
            }
            double draw = NextDouble() * total;
            for (int rank = first; rank <= maximumAdvanced; rank++)
            {
                double weight = Math.Exp(-ConnateTuning.AdvancedRankWeightDecay * (rank - first));
                if (rank >= topStart) weight *= ConnateTuning.TopTierWeightMultiplier;
                if ((draw -= weight) <= 0) return rank;
            }
            return first;
        }
        return DrawBaseRank();
    }

    private double DrawGarbageInterval() => ConnateTuning.GarbageIntervalMinimumSeconds
        + NextDouble() * (ConnateTuning.GarbageIntervalMaximumSeconds - ConnateTuning.GarbageIntervalMinimumSeconds);

    private int DrawBaseRank()
    {
        // The 7-piece bag guarantees balanced 1/2 supply across each cycle while making 3 slightly more common.
        if (_baseBagIndex >= _baseBag.Count)
        {
            _baseBag.Clear();
            _baseBag.AddRange([0, 0, 1, 1, 2, 2, 2]);
            for (int i = _baseBag.Count - 1; i > 0; i--)
            {
                int j = (int)(NextUInt64() % (uint)(i + 1));
                (_baseBag[i], _baseBag[j]) = (_baseBag[j], _baseBag[i]);
            }
            _baseBagIndex = 0;
        }
        return _baseBag[_baseBagIndex++];
    }

    public void Restart() => RestartRound();

    /// <summary>Begins a run. <paramref name="startStage"/> above 1 (the pause menu's START AT STAGE) goes
    /// straight into play with the score banked at that stage's threshold; every other restart begins at stage 1.</summary>
    private void RestartRound(int startStage = 1)
    {
        // A new run started from the game-over screen goes straight into play: the player has just read a
        // card and pressed to go, and the intro card would only flash for its auto-dismiss window. The
        // intro is for a game opened fresh.
        bool straightIn = Phase == Stage.GameOver || startStage > 1;
        StartStage = startStage;
        // HighScore and RNG survive restart; every other per-run collection and timer must be reset before the
        // first held value and future cadences are drawn.
        _bodies.Clear();
        _pendingMerges.Clear();
        _mergeFlashes.Clear();
        _bombProjectiles.Clear();
        _bombExplosions.Clear();
        _garbageClears.Clear();
        _scoreMotes.Clear();
        _comboBursts.Clear();
        _baseBag.Clear();
        _baseBagIndex = 0;
        _immunityFloorLeft = 0;
        _queuedRank = -1;
        _queuedHue = ConnateRules.HueAzure;
        ComboCount = 0;
        ComboDisplayLeft = 0;
        BoardClearLeft = 0;
        _boss.Clear();
        _bossHits.Clear();
        _owedStageShouts.Clear();
        _announcedStageIndex = 0;
        StageShoutLeft = 0;
        StageShoutStage = 0;
        NewBestShoutLeft = 0;
        _newBestShouted = false;
        // An invariant, so set explicitly rather than relying on SeedOpeningGarbage below.
        _boardOccupied = true;
        BombCharge = 0;
        PendingBombs = 0;
        BombDeliveryLeft = 0;
        _mergesSinceShot = 0;
        DisplayedScore = 0;
        ScorePulse = 0;
        _collectCueCooldown = 0;
        _fireCooldown = _chainTimeLeft = 0;
        _sinceFire = RecoilCap;
        _fireHeld = false;
        _pressConsumed = false;
        _sinceHold = RecoilCap;
        _shotDrawTension = 0;
        _chainDepth = 0;
        _lastMergeShotId = -1;
        _nextBodyId = 1;
        _nextShotId = 1;
        _warningLatched = false;
        _shotClockElapsed = 0;
        _shotClockDeadline = ConnateTuning.ShotClockSeconds;
        _garbageTimeLeft = DrawGarbageInterval();
        HeldIsBomb = false;
        SizeLimitImmune = false;
        SizeFuse = 0;
        FinalFieldSum = 0;
        ExplodedValue = 0;
        CollectedScore = 0;
        // PlayerAngle is a screen angle (0 = 12 o'clock, increasing clockwise), so π is the bottom of the ring
        // — the least obstructed launch line and where a hand naturally rests the stick.
        PlayerAngle = Math.PI;
        DrawNextHeld(baseOnly: true);
        SeedOpeningGarbage();
        if (straightIn) { SetPhase(Stage.Playing); _shotClockElapsed = 0; SeedStart(startStage); }
        else SetPhase(Stage.Intro);
    }

    /// <summary>The board opens with one garbage blob already in it, so the very first shot has to be aimed
    /// around something. It clears the usual way — beside a resolving numbered merge.</summary>
    private void SeedOpeningGarbage()
    {
        // Sized off RadiusForRank, the same curve the renderer draws with, so the blob reads as exactly a
        // value-24 tile at garbage scale rather than approximately.
        double radius = ConnateTuning.RadiusForRank(ConnateRules.RankForValue(24)) * ConnateTuning.GarbageScale;
        _bodies.Add(new ConnateBody
        {
            Id = _nextBodyId++,
            IsGarbage = true,
            GarbageRadius = radius,
            // Dead centre and at rest: gravity pulls toward the middle, so anywhere else just slides there.
            X = 0, Y = 0, VelocityX = 0, VelocityY = 0,
            SizeArmed = true,          // it counts toward the clump envelope immediately, like any settled body
            EnteringPlayfield = false, // already inside; it never crosses the outer wall
            Hue = ConnateRules.HueNone,
        });
    }

    private void SetPhase(Stage phase) { Phase = phase; PhaseTime = 0; }

    private ulong NextUInt64() => ArcadeRng.Next(ref _rng);

    private double NextDouble() => ArcadeRng.NextDouble(ref _rng);
    private static double DistanceSquared(double ax, double ay, double bx, double by) =>
        (ax - bx) * (ax - bx) + (ay - by) * (ay - by);

    private static double SegmentContact(double ax, double ay, double bx, double by,
                                         double px, double py, double radius)
    {
        // Earliest parametric intersection of movement segment AB with the target circle. Infinity means no hit;
        // returning zero for an initially overlapping segment lets an obstructed bomb resolve deterministically.
        double dx = bx - ax, dy = by - ay;
        double fx = ax - px, fy = ay - py;
        double a = dx * dx + dy * dy;
        if (a < 1e-14) return fx * fx + fy * fy <= radius * radius ? 0 : double.PositiveInfinity;
        double b = 2 * (fx * dx + fy * dy);
        double c = fx * fx + fy * fy - radius * radius;
        double discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return double.PositiveInfinity;
        double root = Math.Sqrt(discriminant);
        double t = (-b - root) / (2 * a);
        if (t is >= 0 and <= 1) return t;
        return c <= 0 ? 0 : double.PositiveInfinity;
    }

    private static double Norm(double angle) => ArcadeMath.NormTau(angle);

    private static double Wrap(double angle) => ArcadeMath.WrapPi(angle);

    // Snapshot DTOs stay primitive/closed: never serialize runtime object references, brushes, or derived
    // presentation lists, and bump SnapVersion plus the hostile-input validation on any schema change.
    // Appended fields carry defaults so an older snapshot still loads; all-sage is the safe hue default,
    // since every tile can still merge with every arithmetically compatible neighbour.
    private sealed record BodySnap(long Id, int Rank, double X, double Y, double VX, double VY,
                                   double Lock, bool Armed, long Shot, double Age, double Jelly,
                                   bool Garbage, bool Entering, double GarbageRadius,
                                   int Hue = ConnateRules.HueAzure,
                                   double Rot = 0, double Spin = 0,
                                   // A body in flight on a charged launch; false is every body a v7/v8 file holds.
                                   bool Charged = false);
    private sealed record PendingSnap(long A, long B, int ResultRank, double Age, double Duration);
    private sealed record BombSnap(long Id, double X, double Y, double VX, double VY, double Age);
    private sealed record Snap(int V, int Phase, double PhaseTime, double PlayerAngle, int HeldRank,
        double Fuse, long FinalSum, long ExplodedValue, int High, double Cooldown, double ChainTime, int ChainDepth,
        long LastMergeShot, long NextBody, long NextShot, ulong Rng, int[] Bag, int BagIndex,
        bool HeldBomb, bool Immune, BodySnap[] Bodies, PendingSnap[] Pending,
        BombSnap[] Bombs, double ShotClock, double GarbageTime,
        int HeldHue = ConnateRules.HueAzure,
        // Defaults are the "nothing in flight" state, which is always a legal board.
        double ImmunityFloor = 0, int QueuedRank = -1, int QueuedHue = ConnateRules.HueAzure,
        int BombCharge = 0,
        // A dropped in-flight combo count is the right trade: it can only ever cost a partial cascade, where
        // restoring a stale count could pay for merges that already happened.
        int PendingBombs = 0, int MergesSinceShot = 0,
        // The cap is "no shot in flight", which is the right resting state for a board that never fired.
        double SinceFire = RecoilCap,
        // A document without a preview draws one on the next reload; the stream diverges from the frozen
        // run's by one tile, which no rule depends on.
        int NextRank = -1, int NextHue = ConnateRules.HueAzure,
        // A run resumed without the flag has not shouted: Restore also derives it from the score, so a v8 run
        // already past the best does not shout on its first step.
        bool NewBestShown = false,
        // Null is every board that is not mid-encounter, which is every v7/v8 file.
        BossSnap? Boss = null);
    /// <summary>The boss-block encounter, whole. The crack count is not stored: it is the hits taken. A mid-flight
    /// bomb delivery rides here too, since the encounter's first fight bomb waits on it.</summary>
    private sealed record BossSnap(int Phase, double Time, long Reward, int Bombs, int Hits, int Stage,
        bool Shattered, double X, double Y, double VX, double VY, double Rot, double Impact, double Delivery);

    /// <summary>⚠ Bump this whenever merge legality or the bomb economy changes in a way an older board cannot
    /// represent, so boards frozen under the old rules are discarded rather than migrated — a save that loads
    /// and then behaves differently is worse than one that doesn't load. High scores survive regardless; they
    /// live outside the snapshot.</summary>
    private const int SnapVersion = 9;

    /// <summary>Versions this build accepts, newest first. v8 and v7 stay readable because every board they
    /// can hold is a legal v9 board: 7 → 8 only dropped the bomb schedule's dead Turns/NextBomb fields, and
    /// 8 → 9 appended three defaulted fields (<c>NewBestShown</c>, a body's <c>Charged</c> flag, false on every
    /// body an older file holds, and the boss-block <c>Boss</c> record, null in every older file) while widening what the bomb rail may hold
    /// (a pending bomb under a numbered tile) — a v8 board never has one, and still reads as itself. The stage
    /// count, thresholds and deadlines are derived from the banked total, so nothing in them is stored.
    /// A v9 file that still carries the retired unlock-card members (<c>OwedCards</c>, <c>Card</c>, <c>CardLeft</c>)
    /// loads too: the snapshot reader ignores members it does not know, so they must never be made required.
    ///
    /// <para>A version not on this list — including a future one — still yields a fresh game rather than a
    /// crash or a prompt, the same posture <c>ArcadeStore</c> takes with the file itself.</para></summary>
    private static readonly int[] ReadableVersions = [SnapVersion, 8, 7];

    public string Serialize() => JsonSerializer.Serialize(new Snap(
        SnapVersion, (int)Phase, PhaseTime, PlayerAngle, HeldRank, SizeFuse, FinalFieldSum, ExplodedValue, HighScore,
        _fireCooldown, _chainTimeLeft, _chainDepth, _lastMergeShotId, _nextBodyId, _nextShotId, _rng,
        [.. _baseBag], _baseBagIndex, HeldIsBomb, SizeLimitImmune,
        [.. _bodies.Select(body => new BodySnap(body.Id, body.Rank, body.X, body.Y,
            body.VelocityX, body.VelocityY, body.MergeLock, body.SizeArmed, body.ShotId, body.Age, body.JellyTime,
            body.IsGarbage, body.EnteringPlayfield, body.GarbageRadius, body.Hue,
            body.Rotation, body.SpinRate, body.Charged))],
        [.. _pendingMerges.Select(bond => new PendingSnap(
            bond.AId, bond.BId, bond.ResultRank, bond.Age, bond.Duration))],
        [.. _bombProjectiles.Select(bomb => new BombSnap(
            bomb.Id, bomb.X, bomb.Y, bomb.VelocityX, bomb.VelocityY, bomb.Age))],
        _shotClockElapsed, _garbageTimeLeft, HeldHue,
        _immunityFloorLeft, _queuedRank, _queuedHue, BombCharge, PendingBombs, _mergesSinceShot, _sinceFire,
        _nextRank, _nextHue, _newBestShouted,
        _boss.Active
            ? new BossSnap((int)_boss.Phase, _boss.Time, _boss.Reward, _boss.Bombs, _boss.Hits, _boss.CapturedStage,
                _boss.Shattered, _boss.X, _boss.Y, _boss.VelocityX, _boss.VelocityY, _boss.Rotation,
                _boss.ImpactLeft, BombDeliveryLeft)
            : null));

    public void Restore(string json)
    {
        // ⚠ The structural rejections below are bare `return`s, and a rejection must still leave a fresh
        // game — the contract this method signs. Committed/finally rather than a restart at each return:
        // there are twenty of them, and the next one added would silently skip it. Don't rely on the caller
        // for this. `ArcadeControl.EnsureGame` happens to restore onto a newly-constructed object today, so
        // the contract has been held by accident; anything restoring onto a live game (a named save, a
        // harness) would otherwise leave the previous run standing while the caller believed it loaded.
        bool committed = false;
        try
        {
            // Treat saves as hostile: reject structural/range/identity contradictions before mutating live state.
            // Restore is transactional until all temporary body, bond, and projectile collections validate.
            Snap? snap = JsonSerializer.Deserialize<Snap>(json);
            if (snap is null || !ReadableVersions.Contains(snap.V)) return;
            if (snap.Phase < 0 || snap.Phase > (int)Stage.GameOver) return;
            if (snap.HeldRank < 0 || snap.HeldRank > ConnateRules.MaximumRank) return;
            if (snap.Bodies is null || snap.Bodies.Length > ConnateTuning.MaximumBodies) return;
            if (snap.Pending is null || snap.Pending.Length > snap.Bodies.Length / 2) return;
            if (snap.Bombs is null || snap.Bombs.Length > 4) return;
            if (snap.Bag is null || snap.Bag.Length is not (0 or 7) || snap.Bag.Any(rank => rank is < 0 or > 2)) return;
            if (snap.BagIndex < 0 || snap.BagIndex > snap.Bag.Length) return;
            if (snap.Rng == 0) return;
            if (snap.ExplodedValue < 0) return;
            if (!Finite(snap.ShotClock) || snap.ShotClock < 0 || snap.ShotClock > ConnateTuning.ShotClockSeconds) return;
            if (!Finite(snap.GarbageTime) || snap.GarbageTime < 0
                || snap.GarbageTime > ConnateTuning.GarbageIntervalMaximumSeconds) return;
            if (snap.QueuedRank < -1 || snap.QueuedRank > ConnateRules.MaximumRank) return;
            // A tile is only ever parked behind a LOADED bomb. Without this, a file claiming a parked tile with
            // the rail holding a numbered one leaves a tile that HasQueuedTile reports to the renderer and that
            // nothing consumes until some future bomb happens to load and fire.
            // The pending count is checked as it will be committed, not as written: the commit clamps it against
            // the live-tunable cap. A pending bomb under a numbered tile is legal — that is where an earned
            // bomb waits.
            int pendingBombs = Math.Clamp(snap.PendingBombs, 0, Math.Max(0, ConnateTuning.MaximumBombs - (snap.HeldBomb ? 1 : 0)));
            // The one exception is an encounter whose last bomb has gone: the rail is empty and NEXT waits for
            // the block to break.
            if (snap.QueuedRank >= 0 && !snap.HeldBomb && snap.Boss is not { Shattered: true }) return;
            if (snap.BombCharge < 0 || snap.BombCharge > MaximumBombChargeCost) return;
            if (!Finite(snap.ImmunityFloor) || snap.ImmunityFloor < 0
                || snap.ImmunityFloor > ConnateTuning.BombImmunityMinimumSeconds) return;

            var restored = new List<ConnateBody>(snap.Bodies.Length);
            var ids = new HashSet<long>();
            foreach (BodySnap body in snap.Bodies)
            {
                if (body.Id <= 0 || !ids.Add(body.Id) || body.Rank < 0 || body.Rank > ConnateRules.MaximumRank) return;
                if (!Finite(body.X) || !Finite(body.Y) || Math.Abs(body.X) > 2 || Math.Abs(body.Y) > 2) return;
                if (!Finite(body.VX) || !Finite(body.VY)
                    || Math.Abs(body.VX) > ConnateTuning.MaxSpeedPerSec * 2
                    || Math.Abs(body.VY) > ConnateTuning.MaxSpeedPerSec * 2) return;
                if (!Finite(body.GarbageRadius) || body.GarbageRadius < 0
                    || body.GarbageRadius > ConnateTuning.RadiusForRank(ConnateRules.MaximumRank)) return;
                if (body.Garbage && body.GarbageRadius < ConnateTuning.GarbageRadius) return;
                restored.Add(new ConnateBody
                {
                    Id = body.Id, Rank = body.Rank, X = body.X, Y = body.Y,
                    VelocityX = body.VX, VelocityY = body.VY,
                    MergeLock = ClampFinite(body.Lock, 0, 2), SizeArmed = body.Armed,
                    ShotId = Math.Max(0, body.Shot), Age = ClampFinite(body.Age, 0, 86400),
                    JellyTime = ClampFinite(body.Jelly, 0, ConnateTuning.JellySeconds),
                    IsGarbage = body.Garbage, EnteringPlayfield = body.Garbage && body.Entering,
                    GarbageRadius = body.Garbage ? body.GarbageRadius : 0,
                    // Garbage is family-less whatever the file claims; a numbered tile with an out-of-range
                    // hue falls back to sage rather than becoming un-mergeable with everything.
                    Hue = body.Garbage ? ConnateRules.HueNone : RestoreHue(body.Hue),
                    Rotation = ClampFinite(body.Rot, -1e6, 1e6),
                    SpinRate = ClampFinite(body.Spin, -40, 40),
                    Charged = body.Charged && !body.Garbage,
                });
            }
            // A bond that is not a legal merge — missing or repeated bodies, garbage, an impossible
            // result or family, or a body already reserved by an earlier bond — is DISCARDED, not grounds for
            // rejecting the whole board: the bodies are fine and simply never merge on this bond. The same
            // predicate runs every physics substep (ConnatePhysics.IsMergeValid).
            var restoredPending = new List<ConnatePendingMerge>(snap.Pending.Length);
            var reserved = new HashSet<long>();
            var restoredById = restored.ToDictionary(body => body.Id);
            foreach (PendingSnap bond in snap.Pending)
            {
                if (bond.A <= 0 || bond.B <= 0 || bond.A == bond.B
                    || reserved.Contains(bond.A) || reserved.Contains(bond.B)) continue;
                if (!restoredById.TryGetValue(bond.A, out ConnateBody? a)
                    || !restoredById.TryGetValue(bond.B, out ConnateBody? b)) continue;
                double duration = ClampFinite(bond.Duration, 0.04, 1.0);
                var candidate = new ConnatePendingMerge
                {
                    AId = bond.A, BId = bond.B, ResultRank = bond.ResultRank,
                    Age = ClampFinite(bond.Age, 0, duration), Duration = duration,
                };
                if (!ConnatePhysics.IsMergeValid(candidate, a, b)) continue;
                reserved.Add(bond.A); reserved.Add(bond.B);
                restoredPending.Add(candidate);
            }
            long maxId = restored.Select(body => body.Id).DefaultIfEmpty(0).Max();
            if (snap.NextBody <= maxId || snap.NextShot <= 0) return;
            var restoredBombs = new List<ConnateBombProjectile>(snap.Bombs.Length);
            foreach (BombSnap bomb in snap.Bombs)
            {
                if (bomb.Id <= 0 || !Finite(bomb.X) || !Finite(bomb.Y) || !Finite(bomb.VX) || !Finite(bomb.VY)) return;
                restoredBombs.Add(new ConnateBombProjectile
                {
                    Id = bomb.Id, X = bomb.X, Y = bomb.Y, VelocityX = bomb.VX, VelocityY = bomb.VY,
                    Age = ClampFinite(bomb.Age, 0, ConnateTuning.BombLifetimeSeconds),
                });
            }
            if (snap.Boss is { } saved && !BossSnapIsLegal(snap, saved, pendingBombs, restoredBombs.Count)) return;

            // Commit only after every cross-reference has passed. Transient flashes deliberately restart empty;
            // the logical body/bond/projectile state resumes exactly and will generate future effects normally.
            _bodies.Clear(); _bodies.AddRange(restored);
            _pendingMerges.Clear(); _pendingMerges.AddRange(restoredPending);
            _mergeFlashes.Clear();
            _bombProjectiles.Clear(); _bombProjectiles.AddRange(restoredBombs);
            _bombExplosions.Clear();
            _garbageClears.Clear();
            _baseBag.Clear(); _baseBag.AddRange(snap.Bag);
            _baseBagIndex = snap.BagIndex;
            Phase = (Stage)snap.Phase;
            PhaseTime = ClampFinite(snap.PhaseTime, 0, 86400);
            PlayerAngle = Norm(ClampFinite(snap.PlayerAngle, -Math.PI * 2, Math.PI * 2));
            HeldRank = snap.HeldRank;
            HeldHue = RestoreHue(snap.HeldHue);
            HeldIsBomb = snap.HeldBomb;
            _bossHits.Clear();
            StartStage = 1;
            _owedStageShouts.Clear();
            if (snap.Boss is { } boss)
            {
                _boss.Load((ConnateBossPhase)boss.Phase, ClampFinite(boss.Time, 0, 86400), boss.Reward, boss.Bombs,
                    boss.Hits, boss.Stage, boss.Shattered, boss.X, boss.Y, boss.VX, boss.VY, boss.Rot,
                    ClampFinite(boss.Impact, 0, 1));
                BombDeliveryLeft = ClampFinite(boss.Delivery, 0, ConnateTuning.BombDeliverySeconds);
            }
            else
            {
                _boss.Clear();
                BombDeliveryLeft = 0;
            }
            SizeLimitImmune = snap.Immune;
            _immunityFloorLeft = ClampFinite(snap.ImmunityFloor, 0, ConnateTuning.BombImmunityMinimumSeconds);
            _queuedRank = snap.QueuedRank;
            _queuedHue = RestoreHue(snap.QueuedHue);
            _nextRank = snap.NextRank >= 0 && snap.NextRank <= ConnateRules.MaximumRank ? snap.NextRank : -1;
            _nextHue  = RestoreHue(snap.NextHue);
            // The cap is a total, so a snapshot claiming a full queue and a loaded bomb is clamped against
            // both together rather than letting the file hand back one more than the rules allow.
            PendingBombs = pendingBombs;
            _mergesSinceShot = Math.Clamp(snap.MergesSinceShot, 0, 999);
            ComboCount = 0;
            ComboDisplayLeft = 0;
            BoardClearLeft = 0;
            // Derived, not stored: an already-empty board must not start the encounter on its first step.
            _boardOccupied = _bodies.Count > 0;
            _scoreMotes.Clear();
            _comboBursts.Clear();
            _garbageTimeLeft = ClampFinite(snap.GarbageTime, 0, ConnateTuning.GarbageIntervalMaximumSeconds);
            SizeFuse = ClampFinite(snap.Fuse, 0, 1);
            ExplodedValue = snap.ExplodedValue;
            // ⚠ Must follow the bank: the cost is a function of the stage, and a charge at or past it would sit
            // on a full meter that nothing spends until the next combo.
            BombCharge = Math.Clamp(snap.BombCharge, 0, BombChargeCost - 1);
            // The deadline is derived from the banked total, committed above, so the restored elapsed time is
            // clamped to the deadline this run actually has rather than the ceiling.
            _shotClockDeadline = ConnateTuning.ShotClockDeadline(DifficultyIndex);
            _shotClockElapsed = ClampFinite(snap.ShotClock, 0, _shotClockDeadline);
            // ⚠ Must follow the line above. Motes are transient, so a run frozen mid-flight resumes with them
            // already landed; deriving the score from the bomb ledger keeps the two from ever disagreeing.
            CollectedScore = ExplodedValue;
            HighScore = Math.Max(HighScore, Math.Max(0, snap.High));
            FinalFieldSum = Phase is Stage.LimitBreach or Stage.GameOver ? CollectedScore : 0;
            _fireCooldown = ClampFinite(snap.Cooldown, 0, 2);
            _sinceFire = ClampFinite(snap.SinceFire, 0, RecoilCap);
            _chainTimeLeft = ClampFinite(snap.ChainTime, 0, 5);
            _chainDepth = Math.Clamp(snap.ChainDepth, 0, 999);
            _lastMergeShotId = snap.LastMergeShot;
            _nextBodyId = snap.NextBody;
            _nextShotId = snap.NextShot;
            _rng = snap.Rng;
            _warningLatched = SizeFuse >= 0.55;
            // The readout resumes at the restored total rather than counting up from zero, and with no
            // arrival to punch it — the motes it would have belonged to were already banked above.
            DisplayedScore = LiveScore;
            ScorePulse = 0;
            _collectCueCooldown = 0;
            // A restore announces nothing: the stage is re-anchored wherever the bank puts it, and a run
            // already past the best has shouted — derived as well as stored, so a board saved without the flag
            // does not shout on its first step.
            _announcedStageIndex = DifficultyIndex;
            StageShoutLeft = 0;
            StageShoutStage = 0;
            NewBestShoutLeft = 0;
            _newBestShouted = snap.NewBestShown
                || (HighScore > 0 && ConnateRules.HostScore(CollectedScore) > HighScore);
            committed = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] Connate snapshot discarded ({ex.Message})");
        }
        finally
        {
            if (!committed) RestartRound();
        }
    }

    /// <summary>A family from a hostile file. Anything unrecognised falls back to sage, never
    /// <see cref="ConnateRules.HueNone"/>, which would make the tile un-mergeable and brick the board.</summary>
    private static int RestoreHue(int hue) => hue switch
    {
        ConnateRules.HueEmber => ConnateRules.HueEmber,
        ConnateRules.HueBlend => ConnateRules.HueBlend,
        _ => ConnateRules.HueAzure,
    };

    /// <summary>Is a saved encounter one the game could have been in? Anything else is hostile and yields a fresh
    /// game: the encounter runs only on a live, empty board; its numbers are inside their caps and finite; each
    /// phase agrees with the hits and the shatter flag; and every bomb it still owes is accounted for — in flight,
    /// on the rail, or waiting — so no saved fight can wait on a bomb that does not exist.</summary>
    private static bool BossSnapIsLegal(Snap snap, BossSnap boss, int pendingBombs, int bombsInFlight)
    {
        if (boss.Phase is < (int)ConnateBossPhase.Drop or > (int)ConnateBossPhase.Break) return false;
        if (snap.Phase != (int)Stage.Playing || snap.Bodies.Length != 0) return false;
        if (boss.Bombs < 1 || boss.Bombs > ConnateTuning.MaximumBombs) return false;
        if (boss.Hits < 0 || boss.Hits > boss.Bombs) return false;
        if (boss.Reward < 0 || boss.Reward > ConnateTuning.BossRewardCeiling) return false;
        if (boss.Stage < 0 || boss.Stage > ConnateTuning.MaximumStageIndex) return false;
        if (!Finite(boss.Time) || boss.Time < 0 || boss.Time > 86400) return false;
        if (!Finite(boss.X) || !Finite(boss.Y) || Math.Abs(boss.X) > 1 || Math.Abs(boss.Y) > 1) return false;
        if (!Finite(boss.VX) || !Finite(boss.VY) || Math.Abs(boss.VX) > 50 || Math.Abs(boss.VY) > 50) return false;
        if (!Finite(boss.Rot) || Math.Abs(boss.Rot) > 1e6) return false;
        if (!Finite(boss.Impact) || boss.Impact < 0 || boss.Impact > 1) return false;
        if (!Finite(boss.Delivery) || boss.Delivery < 0 || boss.Delivery > ConnateTuning.BombDeliverySeconds) return false;

        int onRail = (snap.HeldBomb ? 1 : 0) + pendingBombs;
        switch ((ConnateBossPhase)boss.Phase)
        {
            case ConnateBossPhase.Drop:
                if (boss.Shattered || boss.Hits != 0 || boss.Time > ConnateTuning.BossDropSeconds + 0.1) return false;
                break;
            case ConnateBossPhase.Land:
                if (boss.Hits != 0) return false;
                break;
            case ConnateBossPhase.Fight:
                if (!boss.Shattered || boss.Hits >= boss.Bombs) return false;
                break;
            case ConnateBossPhase.Break:
                if (!boss.Shattered || bombsInFlight != 0 || onRail != 0
                    || boss.Time > ConnateTuning.BossBreakSeconds + 0.1) return false;
                return true;
        }
        // Before the break, every bomb not yet landed is in flight, on the rail, or waiting for it.
        return boss.Bombs - boss.Hits == bombsInFlight + onRail;
    }

    private static bool Finite(double value) => ArcadeMath.Finite(value);
    private static double ClampFinite(double value, double minimum, double maximum) =>
        Finite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
}
