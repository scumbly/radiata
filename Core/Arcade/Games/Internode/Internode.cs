using System.Diagnostics;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>Internode: a forward-scrolling half-pipe runner. Stick-x swings the runner around the pipe's circular
/// cross-section against a pendulum pull; enough speed carries it over the rim and across the open top; Cross
/// jumps. Orbs are collected, mines scatter them, and every section ends at a gate with a token quota. Pure sim:
/// no WPF, clock, audio or filesystem; <c>InternodeRenderer</c> reads the public state and draws.
///
/// <para>Conventions: θ radians, 0 at the bottom, positive = runner's right; z in seconds of travel (forward
/// speed is 1 s/s by definition, there is no speed knob). Only one section is held; it is rebuilt from
/// (<see cref="Stage"/>, <see cref="Section"/>, section seed) on restore. Bends and twists are sim data with no
/// physics effect. Every presentation clock lives here so pause and freeze/resume hold the picture still.</para></summary>
public sealed class Internode : IArcadeGame
{
    public const string GameId = "internode";
    public string Id => GameId;
    public string Title => "Internode";

    [Flags]
    public enum Cue
    {
        None = 0, Begin = 1, Token = 2, TokenElevated = 4, Mine = 8, Jump = 16, Land = 32, RimExit = 64,
        Fell = 128, Checkpoint = 256, CheckpointFail = 512, StageUp = 1024, Bonus = 2048, NewBest = 4096, Miss = 8192,
        OverTopLand = 32768, Respawn = 65536, GoldTurn = 131072,
        /// <summary>The banked orbs have left the runner for the readout: raised once per gate, with the flight.</summary>
        BankFlight = 262144,
        // ⚠ Every value is its own bit. Two cues on one bit play both sounds on either event, and nothing
        // else notices: the harness checks each game's Cue enum for this, so add a cue by doubling the last.
    }

    // ── Game state (serialized) ───────────────────────────────────────────────
    public InternodePhase Phase { get; private set; }
    public double PhaseTime { get; private set; }
    /// <summary>1-based, endless.</summary>
    public int Stage { get; private set; } = 1;
    /// <summary>1 .. <see cref="InternodeTuning.SectionsPerStage"/>.</summary>
    public int Section { get; private set; } = 1;
    /// <summary>Seconds travelled into the current section.</summary>
    public double Z { get; private set; }
    /// <summary>Orbs held since the last gate: what the gate compares against <see cref="Quota"/>, what a mine or a
    /// fall takes from, and what a passed gate banks into <see cref="Score"/> before resetting to zero.</summary>
    public int Tokens { get; private set; }
    /// <summary>Banked orbs: the score. Only a passed gate adds to it, and nothing takes from it.</summary>
    public int Score { get; private set; }
    /// <summary>What this section's gate demands of the held orbs.</summary>
    public int Quota { get; private set; }
    /// <summary>The demand derived from the current section's content. Equal to <see cref="Quota"/> except on a
    /// retry, where the quota stands while a re-rolled section carries its own figure.</summary>
    public int SectionQuotaIncrement { get; private set; }
    public int HighScore { get; private set; }
    /// <summary>Orbs flying into the score readout after a gate, and the count they carry: the readout climbs
    /// as they land. Presentation only.</summary>
    public IReadOnlyList<InternodeBankOrb> BankFlight => _bank;
    /// <summary>The fraction of the orbs caught since the last gate that were gold, 0..1, by count: a gold orb is
    /// one orb here whatever it pays (value matters only when the gate banks). Moves only when an orb is caught
    /// and resets with the hand; losing orbs to a mine leaves it exactly where it was. Every spray the hand
    /// produces — a mine hit, a fall, a gate's bank — paints this share of its orbs gold.</summary>
    public double GoldShare { get; private set; }
    /// <summary>Orbs caught since the last gate: the weight the next catch re-averages <see cref="GoldShare"/>
    /// against. Presentation bookkeeping only; a mine's loss is taken off it so a long hand does not pin the
    /// share forever.</summary>
    private int _orbsHeld;

    /// <summary>Is orb <paramref name="i"/> of <paramref name="n"/> gold when <paramref name="share"/> of them are?
    /// The gold ones fall evenly through the run rather than bunching at either end.</summary>
    private static bool GoldAt(int i, int n, double share)
    {
        int gold = (int)Math.Round(share * n);
        return gold > 0 && (i + 1) * gold / n > i * gold / n;
    }
    public int BankAmount { get; private set; }
    public double BankAge { get; private set; } = 1e9;
    /// <summary>Seconds since the last jump press, first or second; 1e9 when none. The renderer lights the
    /// runner's exhaust ports off it. Presentation only, and transient like <see cref="BankAge"/> — a resume
    /// mid-air simply starts with the flare spent, which is cosmetic.</summary>
    public double JumpFlareAge { get; private set; } = 1e9;
    private readonly List<InternodeBankOrb> _bank = [];
    /// <summary>Per phrase of the current section: orbs it holds, orbs caught so far, and whether one got away.</summary>
    private int[] _phraseOrbs = [];
    private int[] _phraseTaken = [];
    private bool[] _phraseFailed = [];
    /// <summary>Routes through each phrase (1, or one per branch on a choice) and where its routes start in
    /// the arrays above. The gold rule counts per route — see <see cref="IndexPhrases"/>.</summary>
    private int[] _phraseRoutes = [];
    private int[] _routeBase = [];
    /// <summary>Seconds of hit-stop left. Serialized so a snapshot mid-impact resumes mid-impact.</summary>
    public double HitStopLeft { get; private set; }
    /// <summary>Seconds of mine immunity left after a hit. Serialized, like the hit-stop.</summary>
    public double InvulnerableLeft { get; private set; }
    /// <summary>A setting (per game, persisted beside the high score), not run state. Until the player picks it
    /// in the pause menu it follows Reduce Motion: off when motion is reduced, on otherwise. The renderer
    /// rate-limits the roll it draws so a fall through the open top cannot flick the frame.</summary>
    public bool CameraRoll => _rollChoice ?? !MotionPolicy.Reduce;
    private bool? _rollChoice;

    // ── Phrase tester (dev only, ArcadeDebug.PhraseTester) ────────────────────
    /// <summary>The run is the authored bank in difficulty order, one phrase three times to a gate that asks
    /// nothing, then the next; Select jumps a phrase; a fall replays the same one; the last wraps to the first.
    /// Off by default, never persisted with the settings, and never on in a public build.</summary>
    public bool TesterMode { get; private set; }
    /// <summary>Index into <see cref="TesterOrder"/> of the phrase being tested.</summary>
    public int TesterIndex { get; private set; }
    /// <summary>The phrase under test, or null outside tester mode; the renderer names it on its own plate.</summary>
    public string? TesterPhraseId => TesterMode ? TesterOrder[TesterIndex].Id : null;
    public static int TesterCount => TesterOrder.Length;
    /// <summary>Easiest to hardest by the bank's own gates: tier, then the stage a phrase is first offered, then
    /// the diabolical flag, then bank order (so a phrase and its mirror stay together).</summary>
    public static readonly InternodePhrase[] TesterOrder =
        [.. InternodePhrases.All.OrderBy(p => p.Tier).ThenBy(p => p.MinStage).ThenBy(p => p.Diabolical ? 1 : 0)];

    // ── Gate scoring: first-try streak and perfect sections ──
    /// <summary>How many times the current section has been re-rolled after a short gate; 0 on a first try.</summary>
    public int SectionAttempts { get; private set; }
    /// <summary>Consecutive gates passed on the first try; 0 after any retry. Drives the bank multiplier.</summary>
    public int FirstTryStreak { get; private set; }
    /// <summary>Consecutive gates passed with every collectable orb of the section caught; 0 after a miss that
    /// cost the player something. Drives the perfect bonus.</summary>
    public int PerfectCombo { get; private set; }

    /// <summary>Tokens were taken off the player inside this section, so it can no longer pay the no-miss
    /// bonus however cleanly the rest of it is run. Separate from the per-route miss flags: a mine spoils the
    /// section outright, not one branch of it. The first-try multiplier is deliberately untouched — a hit is
    /// not a retry, and a section still passed on the first attempt still earns it.</summary>
    private bool _sectionSpoilt;

    /// <summary>Was every orb the player could have taken taken? Per phrase, not per section: a phrase is clean
    /// when any one of its routes is unspoilt, so the orbs on the branch of a choice that was not run are
    /// exempt — they were never collectable, and the player who ran the other branch perfectly should be paid
    /// for it. A single-route phrase reduces to "that route is clean".
    ///
    /// <para>⚠ Reads <see cref="_phraseFailed"/>, which has no minimum-orb gate, so a one-orb breather counts
    /// the same as a nine-orb figure. Don't reach for the gold rings instead: those need
    /// <see cref="InternodeTuning.BonusMinOrbs"/> orbs on a route before one is offered at all, so eleven
    /// phrases (the three breathers and eight three-orb figures) plus their mirrors would be invisible to
    /// the test, and a route spoilt early never turns its gold, which would read as nothing missed.</para>
    ///
    /// <para>Every authored orb carries a phrase index (<c>InternodeSequencer.Place</c>); only a course-added
    /// mine has none, and a mine is never missed, so nothing collectable falls outside a route.</para></summary>
    private bool SectionPerfect()
    {
        if (_sectionSpoilt) return false;
        for (int p = 0; p < _phraseRoutes.Length; p++)
        {
            bool any = false;
            for (int r = 0; r < _phraseRoutes[p] && !any; r++) any = !_phraseFailed[_routeBase[p] + r];
            if (!any) return false;
        }
        return true;
    }
    /// <summary>What the last bank was made of, for the HUD's toasts and the probe: the multiplier applied
    /// (1 when the gate was not a first try), the perfect bonus added before it (0 when not perfect), and the total.</summary>
    public double LastMultiplier { get; private set; } = 1;
    public int LastPerfectBonus { get; private set; }
    public int LastBanked { get; private set; }
    /// <summary>The row that slides into the top of the HUD plaque at a gate: checkpoint when the hand was short,
    /// level up when it banked. Presentation only; ages out.</summary>
    public InternodeGateNotice GateNotice { get; private set; }
    public double GateNoticeAge { get; private set; } = 1e9;
    /// <summary>The plaque's bottom row while the stage number has just moved; ages out. Presentation only.</summary>
    public InternodeStageNotice StageNotice { get; private set; }
    public double StageNoticeAge { get; private set; } = 1e9;
    /// <summary>0..1 swell on the stage row: the quota pulse's humps, repeating for as long as the row is up.</summary>
    public double StagePulse
    {
        get
        {
            if (StageNotice == InternodeStageNotice.None || StageNoticeAge >= InternodeTuning.StageNoticeSeconds) return 0;
            double period = Math.Max(0.05, InternodeTuning.QuotaPulseSeconds);
            return Math.Abs(Math.Sin(Math.PI * StageNoticeAge / period));
        }
    }
    /// <summary>Age of the bonus toasts raised by the last bank (one clock, they appear together).</summary>
    public double BonusToastAge { get; private set; } = 1e9;

    private InternodeRunnerState _runner;
    private ulong _sectionSeed;
    private InternodeSection _section;
    private ulong _rng = Seed;
    private const ulong Seed = 0x466C756D6552756EUL;   // "InternodeRun"
    private bool _bestBeaten;

    /// <summary>Last step's <see cref="TwistAt"/>(0), for accumulating <see cref="CameraTwist"/> by difference.</summary>
    private double _twistMark;

    /// <summary>Biggest per-step twist change treated as real. A section seam resets TwistAt to zero, which
    /// arrives as a jump far beyond anything the course authors.</summary>
    private const double MaxTwistStepRad = 0.35;

    // ── Presentation state (transient, never serialized) ─────────────────────
    /// <summary>Rotational kick of the whole disc, radians. Decays to exactly 0.</summary>
    public double DiscTwist { get; private set; }
    /// <summary>Scale kick of the whole disc. Decays to exactly 1.</summary>
    public double DiscPunch { get; private set; } = 1;
    public double CollectPulse { get; private set; }
    private double _quotaPulseLeft;
    /// <summary>0..1 swell on the token count after a gate reached under quota — a run of
    /// <see cref="InternodeTuning.QuotaPulseCount"/> humps rather than one decay, so the number reads as
    /// being pointed at rather than merely acknowledged. Zero at every other moment.</summary>
    public double QuotaPulse
    {
        get
        {
            if (_quotaPulseLeft <= 0) return 0;
            double period = Math.Max(0.05, InternodeTuning.QuotaPulseSeconds);
            double elapsed = period * Math.Max(1, InternodeTuning.QuotaPulseCount) - _quotaPulseLeft;
            return Math.Abs(Math.Sin(Math.PI * elapsed / period));
        }
    }
    public InternodeShout Shout { get; private set; }
    public double ShoutLeft { get; private set; }
    public int ShoutValue { get; private set; }
    private readonly List<InternodeBurst> _bursts = [];
    private readonly List<InternodeCollect> _collects = [];
    private readonly List<InternodeGoldFlash> _goldFlashes = [];
    private Cue _cues;

    // ── Public views ──────────────────────────────────────────────────────────
    public double Theta => _runner.Theta;
    public double Omega => _runner.Omega;
    public double H => _runner.H;
    public InternodeAir Air => _runner.Air;
    public InternodeRunnerState Runner => _runner;
    public IReadOnlyList<InternodePlacedEvent> Events => _section.Events;
    public IReadOnlyList<InternodeBurst> Bursts => _bursts;
    public IReadOnlyList<InternodeCollect> Collects => _collects;
    public IReadOnlyList<InternodeGoldFlash> GoldFlashes => _goldFlashes;
    /// <summary>Perceived speed for this stage: <see cref="InternodeTuning.ViewSpeed"/> plus the per-stage step, capped.</summary>
    public double ViewSpeedNow => Math.Min(InternodeTuning.ViewSpeedMax,
        InternodeTuning.ViewSpeed + Math.Max(0, InternodeTuning.ViewSpeedPerStage) * (Math.Max(1, Stage) - 1));
    /// <summary>Where the gate stands in section seconds.</summary>
    public double CheckpointZ => _section.Length;
    /// <summary>Always false: a missed gate holds the run at the same section instead of ending it, so there is no
    /// failure state and the host always freezes the game rather than clearing its snapshot.</summary>
    public bool IsGameOver => false;

    /// <summary>Gates passed, which is what the renderer keys the pipe's tone to: the ground changes colour every
    /// time a quota is met, and a missed gate leaves it alone.</summary>
    public int PipeTheme => (Math.Max(1, Stage) - 1) * Math.Max(1, InternodeTuning.SectionsPerStage) + Math.Max(1, Section) - 1;
    /// <summary>Tokens a mine costs right now: a share of what is in hand, so the sting scales with the run.
    /// Falling through a gap costs the same, on top of the rewind.</summary>
    public int MineLoss => Math.Clamp(Math.Max(Math.Max(0, InternodeTuning.MineLossMinTokens), (int)Math.Round(Tokens * Math.Clamp(InternodeTuning.MineLossShare, 0, 1))),
                                      0, Math.Max(0, InternodeTuning.MineLossCapTokens));
    /// <summary>How far the camera has turned since the run began: lateral and vertical as the integral of the
    /// pipe's bend over distance travelled, and <see cref="CameraTwist"/> as its accumulated corkscrew.
    /// Unbounded and monotone through a turn; the bend units are bend × seconds, so only the shape matters and
    /// a consumer scales it to taste. Twist is radians.
    ///
    /// <para>⚠ Nothing else can stand in for these. The renderer's own bend integration starts at the near
    /// edge of the visible window and jumps as that window slides, and <see cref="TwistAt"/> is measured from
    /// the section start and so resets at every seam — both are fine for drawing the pipe relative to the
    /// runner, and neither can carry something that has to move continuously for a whole run.</para>
    ///
    /// <para>Snapshotted, unlike the input-derived presentation flags: this is where the course has carried
    /// the camera, so a frozen run resumes looking the way it was left.</para></summary>
    public double CameraTurnX { get; private set; }
    public double CameraTurnY { get; private set; }
    public double CameraTwist { get; private set; }

    /// <summary>How far the bike has dropped through a gap, in pipe radii below the surface: presentation
    /// only, zero except during <see cref="InternodePhase.Falling"/>.</summary>
    public double FallDepth { get; private set; }
    /// <summary>How far ahead of the camera the falling bike has carried, in seconds of travel: the bike keeps
    /// its full speed through the drop while the camera skids to a stop. Presentation only, zero except
    /// during <see cref="InternodePhase.Falling"/>.</summary>
    public double FallAhead { get; private set; }
    /// <summary>The section's breaks in the floor, for the renderer to leave unpainted.</summary>
    public IReadOnlyList<InternodeGap> Gaps => _section.Gaps;

    /// <summary>Sim time scale this step: a crawl during a hit-stop, else 1. Derived, never stored, so it is
    /// exactly 1 the instant the timer ends.</summary>
    public double TimeScale => HitStopLeft > 0 ? InternodeTuning.HitStopScale : 1;

    /// <summary>Lateral (x) and vertical (y) swing of the pipe <paramref name="zAhead"/> seconds ahead of the
    /// runner, each in [−1, 1]; the sequencer's turns put a value on one axis only. (0, 0) within
    /// <see cref="InternodeTuning.LookaheadSeconds"/> of the gate and past it.</summary>
    public (double X, double Y) BendAt(double zAhead)
    {
        double z = Z + zAhead;
        if (!double.IsFinite(z) || z >= _section.Length - InternodeTuning.LookaheadSeconds) return (0, 0);
        double x = 0, y = 0;
        foreach (var b in _section.Bends)
        {
            if (z <= b.Z0 || z >= b.Z0 + b.Seconds || b.Seconds <= 0) continue;
            double s = Math.Sin(Math.PI * (z - b.Z0) / b.Seconds);
            x += b.AmpX * s * s;
            y += b.AmpY * s * s;
        }
        return (Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1));
    }

    /// <summary>Cumulative corkscrew of the pipe about its axis <paramref name="zAhead"/> seconds ahead, radians.</summary>
    public double TwistAt(double zAhead)
    {
        double z = Z + zAhead;
        if (!double.IsFinite(z)) return 0;
        double total = 0;
        foreach (var t in _section.Twists)
        {
            if (t.Seconds <= 0) { if (z >= t.Z0) total += t.TotalRad; continue; }
            double u = Math.Clamp((z - t.Z0) / t.Seconds, 0, 1);
            total += t.TotalRad * (u * u * (3 - 2 * u));
        }
        return total;
    }

    /// <summary>The rim <paramref name="zAhead"/> seconds ahead of the runner, radians: the base rim raised by the
    /// section's closures (each a plateau at its target between two smoothstep ramps), clamped to [<see cref="InternodeTuning.RimMinDeg"/>, <see cref="InternodeTuning.RimMaxDeg"/>].
    /// Always the base within <see cref="InternodeTuning.LookaheadSeconds"/> of the gate and past it. This one does
    /// reach the physics: <see cref="Step"/> integrates against <c>RimAt(0)</c>.</summary>
    public double RimAt(double zAhead) => RimOf(_section, Z + zAhead);

    internal static double RimOf(InternodeSection section, double z)
    {
        double baseDeg = InternodeTuning.RimDeg;
        double lo = Math.Clamp(InternodeTuning.RimMinDeg, 30, 180), hi = Math.Clamp(InternodeTuning.RimMaxDeg, lo, 180);
        if (!double.IsFinite(z) || z >= section.Length - InternodeTuning.LookaheadSeconds) return Math.Clamp(baseDeg, lo, hi) * InternodePhysics.Deg;
        double deg = baseDeg;
        foreach (var r in section.Rims)
        {
            if (z <= r.Z0 || z >= r.Z0 + r.Seconds || r.Seconds <= 0) continue;
            // The ramp is capped at half the span, so a closure shorter than two ramps is still a smooth bump.
            double ramp = Math.Clamp(InternodeTuning.RimRampSeconds, 0.01, r.Seconds / 2);
            double u = Math.Clamp(Math.Min(z - r.Z0, r.Z0 + r.Seconds - z) / ramp, 0, 1);
            deg += (r.TargetDeg - baseDeg) * u * u * (3 - 2 * u);
        }
        return Math.Clamp(deg, lo, hi) * InternodePhysics.Deg;
    }

    public Cue TakeCues() { Cue c = _cues; _cues = Cue.None; return c; }
    public void SeedHighScore(int high) { if (high > HighScore) HighScore = high; }

    public Internode()
    {
        _section = InternodeSequencer.Build(1, 1, 1);
        BeginRun();
    }

    // ── Pause menu ────────────────────────────────────────────────────────────

    public IReadOnlyList<ArcadePauseOption> PauseOptions
    {
        get
        {
            var rows = new List<ArcadePauseOption>
            {
                new("roll", Loc.T(UiText.Arcade.CameraRoll), [Loc.T(UiText.Arcade.Off), Loc.T(UiText.Arcade.On)], CameraRoll ? 1 : 0),
            };
            if (ArcadeDebug.PhraseTester)
                rows.Add(new("tester", Loc.T(UiText.Arcade.PhraseTester), [Loc.T(UiText.Arcade.Off), Loc.T(UiText.Arcade.On)], TesterMode ? 1 : 0));
            return rows;
        }
    }

    public void ApplyPauseOption(string key, int choice)
    {
        if (string.Equals(key, "roll", StringComparison.OrdinalIgnoreCase)) { _rollChoice = choice != 0; return; }
        if (!ArcadeDebug.PhraseTester || !string.Equals(key, "tester", StringComparison.OrdinalIgnoreCase)) return;
        bool on = choice != 0;
        if (on == TesterMode) return;
        // Either way the run in hand is thrown away: the two modes build sections nobody can cross between.
        TesterMode = on;
        TesterIndex = 0;
        BeginRun();
    }

    /// <summary>Roll: 1 on, 0 off, -1 never chosen (follow Reduce Motion).</summary>
    private sealed record SettingsSnap(int Roll);
    public string? SerializeSettings() => JsonSerializer.Serialize(new SettingsSnap(_rollChoice is { } r ? (r ? 1 : 0) : -1));
    public void RestoreSettings(string json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<SettingsSnap>(json);
            if (s is not null) _rollChoice = s.Roll < 0 ? null : s.Roll != 0;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { /* defaults stand */ }
    }

    public ArcadeHowTo? HowTo => new(Loc.T(UiText.Arcade.HowToPlay),
    [
        new(Loc.T(UiText.Arcade.InternodeHow1), "swing"),
        new(Loc.T(UiText.Arcade.InternodeHow2), "mine"),
        new(Loc.T(UiText.Arcade.InternodeHow3), "gold"),
        new(Loc.T(UiText.Arcade.InternodeHow4), "gate"),
        new(Loc.T(UiText.Arcade.InternodeHow5), "bonus"),
    ]);

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public void Restart() => BeginRun();

    /// <summary>Dismissed mid-fall: land the demotion now rather than freezing the runner halfway down the
    /// hole, so the shot and the snapshot show the section they resume into. The respawn cue is dropped —
    /// it would otherwise sound on resume for a fall already read.</summary>
    public void AcknowledgeOutcome()
    {
        if (Phase != InternodePhase.Falling) return;
        Demote();
        _cues &= ~Cue.Respawn;
    }

    /// <summary>The RNG is not reseeded here: consecutive runs in one session get different courses, while a
    /// fresh object is deterministic.</summary>
    private void BeginRun()
    {
        Stage = 1;
        Section = 1;
        Tokens = 0;
        GoldShare = 0; _orbsHeld = 0;
        Score = 0;
        SectionAttempts = 0; FirstTryStreak = 0; PerfectCombo = 0;
        LastMultiplier = 1; LastPerfectBonus = 0; LastBanked = 0;
        Quota = 0;
        SectionQuotaIncrement = 0;
        HitStopLeft = 0;
        InvulnerableLeft = 0;
        _bestBeaten = false;
        CameraTurnX = CameraTurnY = CameraTwist = 0;
        _twistMark = 0;
        _runner = InternodeRunnerState.AtRest;
        Z = 0;
        TesterIndex = 0;
        _sectionSeed = NextUInt64();
        BuildSection();
        ResetPresentation();
        SetPhase(InternodePhase.Intro);
    }

    /// <summary>Dev only (<see cref="ArcadeDebug.LevelSkip"/>): jump a whole stage, clamped at the first. The held orbs
    /// are dropped, the score stands, and the new stage starts at its first section from rest.</summary>
    private void DebugSkipStage(int delta)
    {
        // In the tester the skip is the next phrase trio, wrapping; BuildSection sets the stage readout from it.
        if (TesterMode) TesterIndex = ((TesterIndex + delta) % TesterCount + TesterCount) % TesterCount;
        Stage = Math.Clamp(Stage + delta, 1, 9999);
        Section = 1;
        Tokens = 0;
        GoldShare = 0; _orbsHeld = 0;
        _runner = InternodeRunnerState.AtRest;
        _sectionSeed = NextUInt64();
        BuildSection();
        Z = 0;
        FallDepth = 0; FallAhead = 0;
        HitStopLeft = 0;
        InvulnerableLeft = 0;
        // A respawn puts the camera back where the section starts, so the sky does not carry the turn of a
        // run that has been rewound.
        CameraTurnX = CameraTurnY = CameraTwist = 0;
        _twistMark = 0;
        StartShout(InternodeShout.Stage, Stage);
        SetPhase(InternodePhase.Running);
    }

    /// <summary>Build the current section from its seed and set its gate's demand. <paramref name="raiseQuota"/> is
    /// false for a retry after a missed gate: the re-rolled section carries its own figure, but the standing quota
    /// does not move.</summary>
    private void BuildSection(bool raiseQuota = true)
    {
        if (TesterMode)
        {
            // The stage readout counts phrases; the gate asks nothing, so every crossing moves on.
            Stage = TesterIndex + 1;
            Section = 1;
            _section = InternodeSequencer.BuildTester(TesterOrder[TesterIndex], _sectionSeed);
            SectionQuotaIncrement = 0;
            Quota = 0;
            if (raiseQuota) SectionAttempts = 0; else SectionAttempts++;
            IndexPhrases();
            return;
        }
        _section = InternodeSequencer.Build(Stage, Section, _sectionSeed);
        SectionQuotaIncrement = QuotaIncrementFor(_section, Stage, Section);
        if (raiseQuota) { Quota = SectionQuotaIncrement; SectionAttempts = 0; } else SectionAttempts++;
        // A perfect section is every collectable orb of this layout; a re-roll starts the count over, which
        // IndexPhrases does by allocating the route arrays fresh.
        IndexPhrases();
    }

    /// <summary>Count each route's orbs so the gold rule can tell when a pattern is one orb from perfect.
    ///
    /// <para>⚠ The unit of the gold rule is the route, not the phrase, and for every ordinary phrase those are
    /// the same thing — one route, all its orbs. A choice phrase has one route per branch, and the rule reads
    /// naturally on each: catch every orb on the route you took but its last, and that last one turns gold.
    /// Counting per phrase instead would make the condition unsatisfiable for a choice, which is why the
    /// bank's standing rule — every pattern crossing the open top ends on a gold candidate — needs this.</para>
    ///
    /// <para>An orb on branch 0 of a choice phrase sits on every route, so it counts toward each of that
    /// phrase's routes and taking it advances all of them. That is what lets a split's shared rejoin orb be
    /// the prize whichever side was run.</para></summary>
    private void IndexPhrases()
    {
        int n = _section.PhraseIds.Length;
        // Routes per phrase: 1 unless the phrase branches, in which case one per branch. Branches are
        // authored 1..K with no gaps (InternodeProbe enforces it), so a branch indexes its own slot.
        _phraseRoutes = new int[n];
        for (int i = 0; i < n; i++) _phraseRoutes[i] = 1;
        foreach (var e in _section.Events)
            if (e.Branch > 0 && e.Phrase >= 0 && e.Phrase < n)
                _phraseRoutes[e.Phrase] = Math.Max(_phraseRoutes[e.Phrase], e.Branch);
        _routeBase = new int[n];
        int groups = 0;
        for (int i = 0; i < n; i++) { _routeBase[i] = groups; groups += _phraseRoutes[i]; }
        _phraseOrbs = new int[groups];
        _phraseTaken = new int[groups];
        _phraseFailed = new bool[groups];
        _sectionSpoilt = false;
        // Each event's routes, and each route's events in section order — resolved here, once, so a miss
        // (the commonest event in the game) spoils a route by walking that route's orbs rather than the
        // whole section with an iterator per event.
        _routeEvents = new List<InternodePlacedEvent>[groups];
        for (int g = 0; g < groups; g++) _routeEvents[g] = new List<InternodePlacedEvent>();
        foreach (var e in _section.Events)
        {
            e.Routes = ResolveRoutes(e);
            foreach (int g in e.Routes) _routeEvents[g].Add(e);
        }

        foreach (var e in _section.Events)
        {
            if (e.Kind == InternodeEventKind.Mine) continue;
            foreach (int g in RoutesOf(e)) _phraseOrbs[g]++;
        }
        // The prize is shown ahead of time: the last orb of every route long enough to pay gold wears the
        // ring. On a choice phrase that is one ring per branch — both routes really do offer the prize, and
        // only the one taken can be collected on.
        int min = Math.Max(2, InternodeTuning.BonusMinOrbs);
        var last = new InternodePlacedEvent?[groups];
        foreach (var e in _section.Events)
        {
            e.GoldCandidate = false;
            if (e.Kind == InternodeEventKind.Mine) continue;
            foreach (int g in RoutesOf(e)) if (_phraseOrbs[g] >= min) last[g] = e;
        }
        foreach (var e in last) if (e is not null) e.GoldCandidate = true;
    }

    /// <summary>The gold-rule groups an orb belongs to: one, except a branch-0 orb of a choice phrase, which
    /// belongs to every route through it. Empty for anything with no phrase (a course-added mine).</summary>
    private int[] RoutesOf(InternodePlacedEvent e) => e.Routes;

    private List<InternodePlacedEvent>[] _routeEvents = [];

    private int[] ResolveRoutes(InternodePlacedEvent e)
    {
        if (e.Phrase < 0 || e.Phrase >= _routeBase.Length) return [];
        int baseIndex = _routeBase[e.Phrase], routes = _phraseRoutes[e.Phrase];
        if (e.Branch > 0) return e.Branch <= routes ? [baseIndex + e.Branch - 1] : [];
        var all = new int[routes];
        for (int r = 0; r < routes; r++) all[r] = baseIndex + r;
        return all;
    }

    /// <summary>The share of a perfect run the gate asks for at <paramref name="stage"/>: a linear ramp from
    /// <see cref="InternodeTuning.QuotaShareStart"/> at stage 1 to <see cref="InternodeTuning.QuotaShareEnd"/> at
    /// <see cref="InternodeTuning.QuotaShareEndStage"/>, held there after.</summary>
    public static double QuotaShare(int stage)
    {
        stage = Math.Max(1, stage);
        double a = Math.Clamp(InternodeTuning.QuotaShareStart, 0, 1), b = Math.Clamp(InternodeTuning.QuotaShareEnd, a, 1);
        int end = Math.Max(2, InternodeTuning.QuotaShareEndStage);
        return a + (b - a) * Math.Clamp((stage - 1) / (double)(end - 1), 0, 1);
    }

    /// <summary>What a run that takes every collectable orb and turns every prize is worth: the offer plus the
    /// nine extra each gold pays over the orb already counted in it.</summary>
    public static int PerfectValue(InternodeSection section) =>
        section.TokensOffered + section.PrizesOffered * (Math.Max(1, InternodeTuning.BonusTokenValue) - 1);

    private static int QuotaIncrementFor(InternodeSection section, int stage, int sectionIndex) =>
        Math.Max(0, (int)Math.Round(QuotaShare(stage) * PerfectValue(section)
                                    * (stage == 1 && sectionIndex == 1 ? InternodeTuning.FirstSectionQuotaScale : 1)));

    private void ResetPresentation()
    {
        DiscTwist = 0;
        DiscPunch = 1;
        // Pause ▸ Reset is reachable mid-fall; a depth left over draws the new run's bike sunk and shrunk.
        FallDepth = 0; FallAhead = 0;
        CollectPulse = 0;
        _quotaPulseLeft = 0;
        Shout = InternodeShout.None;
        ShoutLeft = 0;
        GateNotice = InternodeGateNotice.None;
        GateNoticeAge = 1e9;
        StageNotice = InternodeStageNotice.None;
        StageNoticeAge = 1e9;
        BonusToastAge = 1e9;
        ShoutValue = 0;
        _bursts.Clear();
        _collects.Clear();
        _goldFlashes.Clear();
        _bank.Clear();
        BankAmount = 0;
        BankAge = 1e9;
        JumpFlareAge = 1e9;
        _cues = Cue.None;
    }

    private void SetPhase(InternodePhase p) { Phase = p; PhaseTime = 0; }

    // ── Step ──────────────────────────────────────────────────────────────────

    /// <summary>The order is load-bearing: real-time presentation decay, the phase clock, the phase gates, then
    /// the runner and the pipe at the scaled step (hit-stop slows both), then event planes, the gate, and the
    /// high-score latch.</summary>
    public void Step(in ArcadeInput input, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.05);

        DecayPresentation(dt);
        PhaseTime += dt;
        if (ArcadeDebug.LevelSkip && input.SkipPressed && Phase != InternodePhase.Intro)
        {
            DebugSkipStage(1);
            return;
        }
        double steer = InternodePhysics.Steer(input.StickX);

        switch (Phase)
        {
            case InternodePhase.Intro:
                _runner = InternodePhysics.Integrate(_runner, steer, false, false, dt, RimAt(0), out _);
                if (input.CrossPressed) { SetPhase(InternodePhase.Running); _cues |= Cue.Begin; }
                return;
            case InternodePhase.Checkpoint:
                if (PhaseTime >= InternodeTuning.CheckpointBeatSeconds) SetPhase(InternodePhase.Running);
                break;
            case InternodePhase.Falling:
            {
                // The drop is shown before anything changes: the camera skids to a stop while the bike keeps its
                // speed and drops through the hole, and only then does the demotion land. The held tokens already
                // went at the lip, so a snapshot taken mid-drop is consistent with what the player has.
                // FallAhead is the closed form of full speed minus the skid, so a restore needs no state for it.
                double fall = Math.Max(0.05, InternodeTuning.FallSeconds);
                double coast = Math.Max(0.01, InternodeTuning.FallCoastSeconds);
                Z += dt * Math.Clamp(1 - PhaseTime / coast, 0, 1);
                double u = Math.Min(1, PhaseTime / fall);
                FallDepth = Math.Max(0, InternodeTuning.FallDepthRadii) * u * u;
                FallAhead = PhaseTime < coast ? PhaseTime * PhaseTime / (2 * coast) : PhaseTime - coast / 2;
                if (PhaseTime >= fall) Demote();
                return;
            }
        }

        double sdt = dt * TimeScale;
        _runner = InternodePhysics.Integrate(_runner, steer, input.CrossPressed, input.CrossDown, sdt, RimAt(0), !InGap(Z), out var flags);
        if ((flags & InternodeStepFlags.Jump) != 0) _cues |= Cue.Jump;
        // ⚠ The exhaust burst is the second press only. The first jump is the ordinary thing a runner does
        // and needs no announcement; the double is the spend, so it gets the flame — firing on both would
        // make the second one say nothing the first had not already said.
        if ((flags & InternodeStepFlags.Jump) != 0) JumpFlareAge = 0;   // every jump gets the jet burst
        if ((flags & InternodeStepFlags.Land) != 0) _cues |= Cue.Land;
        if ((flags & InternodeStepFlags.RimExit) != 0) _cues |= Cue.RimExit;
        if ((flags & InternodeStepFlags.OverTopLand) != 0) { DiscPunch = Math.Max(DiscPunch, InternodeTuning.PunchLand); _cues |= Cue.OverTopLand; }

        double zPrev = Z;
        // The camera's own accumulated turn, integrated as the runner travels. The renderer's per-frame bend
        // integration starts at the near edge of the visible window and so jumps as that window slides; this
        // is continuous for the whole run, which is what anything tracking the heading (the sky) needs.
        var (bendX, bendY) = BendAt(0);
        CameraTurnX += bendX * sdt;
        CameraTurnY += bendY * sdt;
        // Twist is a position, not a rate, so it accumulates by difference. ⚠ The difference is discarded when
        // it is implausibly large: TwistAt is measured from the section start and drops to zero at every seam,
        // and a restore starts from whatever the file said. Real twist rates are nowhere near this, so the
        // guard only ever catches a reset.
        double twistNow = TwistAt(0);
        double dTwist = twistNow - _twistMark;
        if (Math.Abs(dTwist) < MaxTwistStepRad) CameraTwist += dTwist;
        _twistMark = twistNow;
        Z += sdt;
        // The floor first, and nothing else this step matters if the run ends here. A runner who stepped off a lip
        // sinks for CoyoteSeconds with the jump still in hand and can launch back out; past that, or for a runner
        // who comes down inside a break with the jump spent, the fall commits at once.
        if (_runner.H < 0 && _runner.Vh <= 0 && (_runner.Launched || _runner.CoyoteLeft <= 0)) { FallThroughGap(); return; }
        CrossEventPlanes(zPrev, Z);
        if (zPrev < _section.Length && _section.Length <= Z) CrossGate();

        if (Score > HighScore)
        {
            HighScore = Score;
            if (!_bestBeaten) { _bestBeaten = true; _cues |= Cue.NewBest; }
        }
    }

    private void DecayPresentation(double dt)
    {
        DiscTwist = TowardZero(DiscTwist, InternodeTuning.TwistDecayPerSec * dt);
        DiscPunch = 1 + TowardZero(DiscPunch - 1, InternodeTuning.PunchDecayPerSec * dt);
        CollectPulse = Math.Max(0, CollectPulse - InternodeTuning.CollectPulseDecayPerSec * dt);
        _quotaPulseLeft = Math.Max(0, _quotaPulseLeft - dt);
        ShoutLeft = Math.Max(0, ShoutLeft - dt);
        if (ShoutLeft <= 0) Shout = InternodeShout.None;
        HitStopLeft = Math.Max(0, HitStopLeft - dt);
        InvulnerableLeft = Math.Max(0, InvulnerableLeft - dt);
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            double age = _bursts[i].Age + dt;
            if (age >= InternodeTuning.BurstSeconds) _bursts.RemoveAt(i);
            else _bursts[i] = _bursts[i] with { Age = age };
        }
        for (int i = _collects.Count - 1; i >= 0; i--)
        {
            double age = _collects[i].Age + dt;
            if (age >= InternodeTuning.CollectPopSeconds) _collects.RemoveAt(i);
            else _collects[i] = _collects[i] with { Age = age };
        }
        for (int i = _goldFlashes.Count - 1; i >= 0; i--)
        {
            double age = _goldFlashes[i].Age + dt;
            if (age >= InternodeTuning.GoldFlashSeconds) _goldFlashes.RemoveAt(i);
            else _goldFlashes[i] = _goldFlashes[i] with { Age = age };
        }
        BankAge += dt;
        JumpFlareAge += dt;
        GateNoticeAge += dt;
        if (GateNoticeAge >= InternodeTuning.GateNoticeSeconds) GateNotice = InternodeGateNotice.None;
        StageNoticeAge += dt;
        if (StageNoticeAge >= InternodeTuning.StageNoticeSeconds) StageNotice = InternodeStageNotice.None;
        BonusToastAge += dt;
        double bankLife = Math.Max(0, InternodeTuning.BankSeconds) + Math.Max(0, InternodeTuning.BankStaggerSeconds) * Math.Max(0, InternodeTuning.MaxBankOrbs);
        if (_bank.Count > 0 && BankAge >= bankLife) _bank.Clear();
        for (int i = 0; i < _bank.Count; i++) _bank[i] = _bank[i] with { Age = _bank[i].Age + dt };
    }

    /// <summary>Decay toward zero from either side and land exactly on it: a residual 1e-17 would keep the
    /// host's transform pushed forever.</summary>
    private static double TowardZero(double value, double amount) => ArcadeMath.TowardZero(value, amount);

    // ── Event planes ──────────────────────────────────────────────────────────

    /// <summary>Every plane in (zPrev, zNow] fires once, against the runner's state after this step's integration.</summary>
    private void CrossEventPlanes(double zPrev, double zNow)
    {
        foreach (var e in _section.Events)
        {
            if (e.Z <= zPrev) continue;
            if (e.Z > zNow) break;
            if (e.Taken) continue;
            // Immune: the mine is neither taken nor missed, it simply passes.
            if (e.Kind == InternodeEventKind.Mine && InvulnerableLeft > 0) continue;
            if (!InternodePhysics.Collides(e.Kind, e.Theta, e.Height, _runner))
            {
                // An orb that gets past is a miss the player should feel: the renderer greys it as it flies by.
                if (e.Kind != InternodeEventKind.Mine)
                {
                    e.Missed = true;
                    _cues |= Cue.Miss;
                    // Only the routes that orb was on are spoilt. A missed branch-1 orb says nothing about
                    // branch 2, which the player may be running; a missed shared orb spoils both.
                    foreach (int g in RoutesOf(e))
                    {
                        _phraseFailed[g] = true;
                        // The ring comes off every orb of a spoilt route: it can no longer pay.
                        foreach (var o in _routeEvents[g])
                            if (o.Kind != InternodeEventKind.Mine) o.GoldCandidate = false;
                    }
                }
                continue;
            }
            e.Taken = true;
            switch (e.Kind)
            {
                case InternodeEventKind.Token:
                case InternodeEventKind.ElevatedToken:
                {
                    int value = e.Bonus ? Math.Max(1, InternodeTuning.BonusTokenValue) : 1;
                    // The share is re-averaged by the one orb just caught and by nothing else.
                    GoldShare = (GoldShare * _orbsHeld + (e.Bonus ? 1 : 0)) / (_orbsHeld + 1);
                    _orbsHeld++;
                    Tokens += value;
                    // The readout swells for a gold catch and a passed gate only; a plain catch moves
                    // the number and nothing else.
                    if (e.Bonus) CollectPulse = 1;
                    _collects.Add(new InternodeCollect(_runner.Theta, _runner.H, 0, value));
                    _cues |= e.Bonus ? Cue.Bonus : e.Kind == InternodeEventKind.ElevatedToken ? Cue.TokenElevated : Cue.Token;
                    foreach (int g in RoutesOf(e)) TurnLastGold(g);
                    break;
                }
                case InternodeEventKind.Mine:
                    if (HitMine(e)) return;   // the section was just replaced; the array we are walking is gone
                    break;
            }
        }
    }

    /// <summary>One more orb caught on route <paramref name="route"/>; when that leaves the route one orb from
    /// clean, its last remaining orb turns gold. Turned, not placed: the gold is the reward.
    ///
    /// <para>⚠ Takes a route group, not a phrase index — see <see cref="IndexPhrases"/>. On an ordinary phrase
    /// the two coincide; on a choice, each branch keeps its own count and only the branch the player is
    /// actually running can complete.</para></summary>
    private void TurnLastGold(int route)
    {
        if (route < 0 || route >= _phraseOrbs.Length) return;
        _phraseTaken[route]++;
        int orbs = _phraseOrbs[route];
        if (_phraseFailed[route] || orbs < Math.Max(2, InternodeTuning.BonusMinOrbs) || _phraseTaken[route] != orbs - 1) return;
        InternodePlacedEvent? last = null;
        foreach (var e in _routeEvents[route])
            if (e.Kind != InternodeEventKind.Mine && !e.Taken) last = e;
        if (last is null) return;
        last.Bonus = true;
        _goldFlashes.Add(new InternodeGoldFlash(last.Z, last.Theta, last.Height, 0));
        _cues |= Cue.GoldTurn;
    }

    /// <summary>No stun: steering is never interrupted. The impact is a knockback, a hit-stop, a twist and a punch.
    /// Returns true when the mine cost the stage: the section the caller is walking has been replaced and
    /// the walk must stop — a ring's seven mines share one plane with overlapping bands, so continuing would
    /// demote a second time and bank the plane's orbs into the emptied hand.</summary>
    private bool HitMine(InternodePlacedEvent mine)
    {
        double d = InternodePhysics.AngleDiff(_runner.Theta, mine.Theta);
        int sign = d != 0 ? Math.Sign(d) : (_runner.Omega != 0 ? -Math.Sign(_runner.Omega) : 1);
        if (Tokens <= 0)
        {
            // Nothing left to take: the mine costs the stage instead.
            HitStopLeft = Math.Max(HitStopLeft, InternodeTuning.MineHitStopSeconds);
            DiscTwist = InternodeTuning.TwistGameOverDeg * InternodePhysics.Deg * sign;
            DiscPunch = Math.Max(DiscPunch, InternodeTuning.PunchMine);
            _cues |= Cue.Mine;
            Demote();
            return true;
        }
        int lost = Math.Min(Tokens, MineLoss);
        double share = GoldShare;
        Tokens -= lost;
        // Losing tokens ends the no-miss run here and now: the section can't come back to clean, and the
        // streak reads zero from this frame rather than only at the next gate.
        _sectionSpoilt = true;
        PerfectCombo = 0;
        _orbsHeld = Math.Max(0, _orbsHeld - lost);
        _runner.Omega += sign * InternodeTuning.MineKnockbackDegPerSec * InternodePhysics.Deg;
        HitStopLeft = Math.Max(HitStopLeft, InternodeTuning.MineHitStopSeconds);
        InvulnerableLeft = Math.Max(InvulnerableLeft, Math.Max(0, InternodeTuning.InvulnerableSeconds));
        DiscTwist = InternodeTuning.TwistMineDeg * InternodePhysics.Deg * sign;
        DiscPunch = Math.Max(DiscPunch, InternodeTuning.PunchMine);
        int orbs = Math.Min(lost, Math.Max(0, InternodeTuning.MaxBurstOrbs));
        for (int i = 0; i < orbs; i++)
            _bursts.Add(new InternodeBurst(_runner.Theta, _runner.H, 0, i * 7919 + lost, GoldAt(i, orbs, share)));
        _cues |= Cue.Mine;
        return false;
    }

    /// <summary>Is <paramref name="z"/> inside a break in the floor?</summary>
    private bool InGap(double z)
    {
        foreach (var gap in _section.Gaps)
            if (z > gap.Z0 && z < gap.Z0 + gap.Seconds && gap.Covers(_runner.Theta, z)) return true;
        return false;
    }

    /// <summary>Everything held goes, then the drop is played out; <see cref="Demote"/> finishes it.</summary>
    private void FallThroughGap()
    {
        // Everything held goes at the lip; the stage itself goes when the drop has played out.
        int lost = Tokens;
        double share = GoldShare;
        Tokens = 0;
        GoldShare = 0; _orbsHeld = 0;
        int orbs = Math.Min(lost, Math.Max(0, InternodeTuning.MaxBurstOrbs));
        for (int i = 0; i < orbs; i++)
            _bursts.Add(new InternodeBurst(_runner.Theta, _runner.H, 0, i * 6151 + lost, GoldAt(i, orbs, share)));
        HitStopLeft = Math.Max(HitStopLeft, InternodeTuning.MineHitStopSeconds);
        DiscTwist = InternodeTuning.TwistGameOverDeg * InternodePhysics.Deg * (_runner.Theta >= 0 ? 1 : -1);
        _cues |= Cue.Fell;
        FallDepth = 0; FallAhead = 0;
        SetPhase(InternodePhase.Falling);
    }

    /// <summary>Back one section (clamped at 1-1; from a stage's first section, to the previous stage's last) with
    /// the hand empty, on a fresh section from rest. The score stands: only a passed gate ever touched it.</summary>
    private void Demote()
    {
        // The tester replays the phrase that dropped the runner (BuildSection restores the readout).
        if (Section > 1) Section--;
        else if (Stage > 1) { Stage--; Section = Math.Max(1, InternodeTuning.SectionsPerStage); }
        StageNotice = InternodeStageNotice.Demoted; StageNoticeAge = 0;
        Tokens = 0;
        GoldShare = 0; _orbsHeld = 0;
        _sectionSeed = NextUInt64();
        BuildSection();
        // A fall-back is a failure: the streaks end, and the section it lands on is a replay, not a first try —
        // otherwise two passes, a fall and a re-pass read streak 3× on a gate the player had already lost once.
        FirstTryStreak = 0; PerfectCombo = 0; SectionAttempts = 1;
        Z = 0;
        _runner = InternodeRunnerState.AtRest;
        FallDepth = 0; FallAhead = 0;
        InvulnerableLeft = 0;
        _cues |= Cue.Respawn;
        SetPhase(InternodePhase.Running);
    }

    // ── The gate ──────────────────────────────────────────────────────────────

    private void CrossGate()
    {
        double carry = Z - _section.Length;
        if (Tokens < Quota)
        {
            // A missed gate holds the run here: the same section is re-rolled and the quota stands, so the player
            // keeps collecting until they can pass. ⚠ The quota must not rise on a retry or the target runs away.
            // A short hand says nothing in the middle of the screen: checkpoint slides into the top of the
            // readout, which swells once in place, and the numbers it shows are the whole of the message.
            // The count swells once (CollectPulse) and its glow pulses several times: the size says the
            // readout was touched, the repeat says to look at it. Two channels, because a size that repeated
            // would have the plaque breathing under the notice sliding into it.
            CollectPulse = 1;
            _quotaPulseLeft = Math.Max(0.05, InternodeTuning.QuotaPulseSeconds)
                              * Math.Max(1, InternodeTuning.QuotaPulseCount);
            GateNotice = InternodeGateNotice.Checkpoint; GateNoticeAge = 0;
            // ⚠ No disc kick here. Coming up short is not a mistake and not an impact —
            // the run simply holds at this section — and jolting the screen for it read as punishment for
            // something the player did nothing wrong to earn. The shout carries the news on its own.
            _cues |= Cue.CheckpointFail;
            _sectionSeed = NextUInt64();
            BuildSection(raiseQuota: false);
            Z = Math.Max(0, carry);
            SetPhase(InternodePhase.Checkpoint);
            return;
        }

        // The held orbs bank: they join the score, the readout climbs as they fly in, and the hand starts empty.
        // Two bonuses fight the incentive to miss a gate on purpose and farm the re-roll: a perfect section (every
        // orb caught) adds a flat bonus that grows with consecutive perfects, and a first-try pass multiplies the
        // lot, more for every consecutive first try. The perfect bonus goes in before the multiplier.
        int baseBank = Tokens + Math.Max(0, InternodeTuning.CheckpointBonusTokens + InternodeTuning.CheckpointBonusPerStage * (Stage - 1));
        bool sectionHadOrbs = _section.Events.Any(e => e.Kind != InternodeEventKind.Mine);
        PerfectCombo = sectionHadOrbs && SectionPerfect() ? PerfectCombo + 1 : 0;
        LastPerfectBonus = PerfectCombo > 0 ? InternodeTuning.PerfectBonusBase + InternodeTuning.PerfectBonusStep * (PerfectCombo - 1) : 0;
        FirstTryStreak = SectionAttempts == 0 ? FirstTryStreak + 1 : 0;
        LastMultiplier = FirstTryStreak > 0 ? InternodeTuning.FirstTryMultiplierBase + InternodeTuning.FirstTryMultiplierStep * (FirstTryStreak - 1) : 1;
        int banked = (int)Math.Ceiling((baseBank + LastPerfectBonus) * LastMultiplier - 1e-9);
        LastBanked = banked;
        BonusToastAge = FirstTryStreak > 0 || PerfectCombo > 0 ? 0 : 1e9;
        GateNotice = InternodeGateNotice.LevelUp; GateNoticeAge = 0;
        double share = GoldShare;
        Score += banked;
        Tokens = 0;
        GoldShare = 0; _orbsHeld = 0;
        BankAmount = banked;
        BankAge = 0;
        _bank.Clear();
        // The flight draws the hand's gold share, spread evenly through the plain orbs so the payout reads as
        // the hand that was cashed rather than bunching the gold at either end.
        int flight = Math.Min(banked, Math.Max(0, InternodeTuning.MaxBankOrbs));
        for (int i = 0; i < flight; i++)
            _bank.Add(new InternodeBankOrb(0, i * 7919 + banked, GoldAt(i, flight, share)));
        DiscPunch = Math.Max(DiscPunch, InternodeTuning.PunchCheckpoint);
        _cues |= Cue.Checkpoint;
        if (_bank.Count > 0) _cues |= Cue.BankFlight;
        CollectPulse = 1;

        if (TesterMode)
        {
            // The next phrase trio, the last wrapping to the first; no promotion, the plate names the phrase.
            TesterIndex = (TesterIndex + 1) % TesterCount;
        }
        else if (Section >= Math.Max(1, InternodeTuning.SectionsPerStage))
        {
            Stage++;
            Section = 1;
            _cues |= Cue.StageUp;   // no centre shout: the plaque's level up row and the pipe's colour carry it
            StageNotice = InternodeStageNotice.Promoted; StageNoticeAge = 0;
        }
        else
        {
            Section++;
        }
        _sectionSeed = NextUInt64();
        BuildSection();
        Z = Math.Max(0, carry);
        SetPhase(InternodePhase.Checkpoint);
    }

    private void StartShout(InternodeShout kind, int value)
    {
        Shout = kind;
        ShoutValue = value;
        ShoutLeft = InternodeTuning.ShoutSeconds;
    }

    // ── RNG ───────────────────────────────────────────────────────────────────
    // Xorshift64*, state in the snapshot so a frozen game's next section is the one it would have had. The
    // multiplier is odd, so the output is never 0 from a non-zero state.
    private ulong NextUInt64() => ArcadeRng.Next(ref _rng);

    // ── Snapshot ──────────────────────────────────────────────────────────────

    private sealed record Snap(int V, int Phase, double PhaseTime, int Stage, int Section, ulong SectionSeed, ulong Rng,
                               double Z, double Theta, double Omega, double H, double Vh, int Air, double JumpBuffer,
                               double GripLeft, double FlyVx, double FlyVy, bool Launched, double HitStop, double Invulnerable, int Tokens, int Score, int Quota,
                               int SectionQuotaIncrement, int High, bool BestBeaten,
                               // Defaulted so a snapshot from before the sky tracked the course still loads: a
                               // camera that has turned nowhere is a legal camera.
                               double TurnX = 0, double TurnY = 0, double Roll = 0,
                               // Defaulted likewise: a hand frozen before gold was counted banks as all plain.
                               double GoldShare = 0,
                               // Gate scoring state. Not defaulted away: the version bump below discards older runs.
                               int SectionAttempts = 0, int FirstTryStreak = 0, int PerfectCombo = 0,
                               // Which routes of this section have had an orb get past. The section is rebuilt
                               // from (stage, section, seed), so IndexPhrases lays the groups out identically
                               // and the array lines up; a length that does not match is treated as clean.
                               bool[]? RouteFailed = null,
                               // Whether a mine has already taken tokens off this section, which no amount of
                               // clean running afterwards undoes.
                               bool SectionSpoilt = false,
                               // The tester's phrase index, or -1 for an ordinary run. A dev-only mode may be
                               // frozen and resumed; a public build never writes anything but -1.
                               int Tester = -1);

    /// <summary>⚠ Bump whenever the phrase bank, sequencer, scoring, runner state or the phase enum change: a run
    /// frozen under old rules is discarded, never migrated. The high score lives outside the snapshot and survives.</summary>
    private const int SnapVersion = 19;
    private static readonly int[] ReadableVersions = [SnapVersion];

    public string Serialize() => JsonSerializer.Serialize(new Snap(
        SnapVersion, (int)Phase, PhaseTime, Stage, Section, _sectionSeed, _rng, Z,
        _runner.Theta, _runner.Omega, _runner.H, _runner.Vh, (int)_runner.Air, _runner.JumpBuffer, _runner.GripLeft,
        _runner.FlyVx, _runner.FlyVy, _runner.Launched,
        HitStopLeft, InvulnerableLeft, Tokens, Score, Quota, SectionQuotaIncrement, HighScore, _bestBeaten,
        CameraTurnX, CameraTurnY, CameraTwist, GoldShare,
        SectionAttempts, FirstTryStreak, PerfectCombo, _phraseFailed, _sectionSpoilt, TesterMode ? TesterIndex : -1));

    public void Restore(string json)
    {
        // ⚠ A structural rejection below is a bare `return`, and it must still leave a fresh game — see the
        // same committed/finally guard in Connate.Restore for why the caller can't be trusted to supply one.
        bool committed = false;
        try
        {
            var snap = JsonSerializer.Deserialize<Snap>(json);
            if (snap is null || !ReadableVersions.Contains(snap.V)) return;
            if (snap.Phase < 0 || snap.Phase > (int)InternodePhase.Falling) return;
            if (snap.Stage < 1 || snap.Stage > 9999) return;
            if (snap.Section < 1 || snap.Section > Math.Max(1, InternodeTuning.SectionsPerStage)) return;
            if (snap.SectionSeed == 0 || snap.Rng == 0) return;
            if (!Finite(snap.Z) || snap.Z < 0) return;
            if (!Finite(snap.Theta) || Math.Abs(snap.Theta) > Math.PI + 1e-9) return;
            if (!Finite(snap.Omega) || Math.Abs(snap.Omega) > 1.5 * InternodeTuning.MaxAngularSpeedDeg * InternodePhysics.Deg + 1e-9) return;
            // Across the open top H is the depth inside the pipe, up to 1 at its centre; otherwise it is jump height.
            double hMax = snap.Air == (int)InternodeAir.OverTop ? 1.0 : 1.2 * InternodeTuning.JumpMaxApex;
            // A runner sinking into a break is airborne a hair below the floor for the coyote window; anywhere else a
            // negative height is impossible.
            double hMin = snap.Air == (int)InternodeAir.Jumping ? -0.5 * InternodeTuning.JumpGravity * InternodeTuning.CoyoteSeconds * InternodeTuning.CoyoteSeconds - 1e-6 : 0;
            if (!Finite(snap.H) || snap.H < hMin || snap.H > hMax + 1e-9) return;
            if (!Finite(snap.FlyVx) || !Finite(snap.FlyVy) || Math.Abs(snap.FlyVx) > 40 || Math.Abs(snap.FlyVy) > 40) return;
            if (snap.Air != (int)InternodeAir.OverTop && (snap.FlyVx != 0 || snap.FlyVy != 0)) return;
            if (!Finite(snap.Vh) || Math.Abs(snap.Vh) > 1.2 * InternodeTuning.JumpImpulse) return;
            if (snap.Air < 0 || snap.Air > (int)InternodeAir.OverTop) return;
            var air = (InternodeAir)snap.Air;
            if (air == InternodeAir.Grounded && snap.H != 0) return;
            // The spent second press is an airborne fact; on the ground it is always clear.
            if (snap.Launched && air == InternodeAir.Grounded) return;
            if (!Finite(snap.JumpBuffer) || snap.JumpBuffer < 0 || snap.JumpBuffer > InternodeTuning.JumpBufferSeconds + 1e-9) return;
            if (!Finite(snap.GripLeft) || snap.GripLeft < 0 || snap.GripLeft > InternodeTuning.GripSeconds + 1e-9) return;
            if (!Finite(snap.HitStop) || snap.HitStop < 0 || snap.HitStop > InternodeTuning.MineHitStopSeconds + 1e-9) return;
            if (!Finite(snap.Invulnerable) || snap.Invulnerable < 0 || snap.Invulnerable > InternodeTuning.InvulnerableSeconds + 1e-9) return;
            if (snap.Tokens < 0 || snap.Tokens > 1_000_000 || snap.Quota < 0 || snap.Quota > 1_000_000) return;
            if (snap.Score < 0 || snap.Score > 100_000_000) return;
            // Not bounded by Quota: a retry after a missed gate rebuilds the section without raising the quota, so
            // the increment can legitimately exceed it. The rebuilt-section comparison below is the real check.
            if (snap.SectionQuotaIncrement < 0) return;
            if (!Finite(snap.PhaseTime) || snap.PhaseTime < 0 || snap.PhaseTime > 600) return;

            bool tester = snap.Tester >= 0;
            if (tester && (!ArcadeDebug.PhraseTester || snap.Tester >= TesterCount || snap.Stage != snap.Tester + 1 || snap.Quota != 0)) return;
            var section = tester ? InternodeSequencer.BuildTester(TesterOrder[snap.Tester], snap.SectionSeed)
                                 : InternodeSequencer.Build(snap.Stage, snap.Section, snap.SectionSeed);
            if (snap.Z > section.Length + 0.5) return;
            if (snap.SectionQuotaIncrement != (tester ? 0 : QuotaIncrementFor(section, snap.Stage, snap.Section))) return;
            // The rim at the frozen z, not the base: a closed tube grounds the runner anywhere, an open one
            // cannot hold anyone over the top.
            double rim = RimOf(section, snap.Z);
            if (air == InternodeAir.OverTop && Math.Abs(snap.Theta) <= rim) return;
            if (air != InternodeAir.OverTop && Math.Abs(snap.Theta) > rim + 1e-9) return;

            // Commit.
            TesterMode = tester;
            TesterIndex = tester ? snap.Tester : 0;
            Stage = snap.Stage;
            Section = snap.Section;
            _sectionSeed = snap.SectionSeed;
            _rng = snap.Rng;
            _section = section;
            IndexPhrases();
            // Every passed plane counts as caught, so a route one orb from its end has its gold restored.
            foreach (var e in _section.Events)
                if (e.Z <= snap.Z)
                {
                    e.Taken = true;
                    if (e.Kind != InternodeEventKind.Mine) foreach (int g in RoutesOf(e)) _phraseTaken[g]++;
                }
            for (int g = 0; g < _phraseOrbs.Length; g++)
                if (_phraseOrbs[g] >= Math.Max(2, InternodeTuning.BonusMinOrbs) && _phraseTaken[g] == _phraseOrbs[g] - 1)
                    foreach (var e in _routeEvents[g])
                        if (e.Kind != InternodeEventKind.Mine && !e.Taken) e.Bonus = true;
            Z = snap.Z;
            // Bounded on the way in like every other restored double: the file is treated as hostile, and an
            // absurd camera turn would send the sky somewhere it can never pan back from.
            CameraTurnX = Finite(snap.TurnX) ? Math.Clamp(snap.TurnX, -1e4, 1e4) : 0;
            CameraTurnY = Finite(snap.TurnY) ? Math.Clamp(snap.TurnY, -1e4, 1e4) : 0;
            CameraTwist = Finite(snap.Roll) ? Math.Clamp(snap.Roll, -1e4, 1e4) : 0;
            _twistMark = TwistAt(0);
            _runner = new InternodeRunnerState
            {
                Theta = InternodePhysics.Wrap(snap.Theta), Omega = snap.Omega, H = snap.H, Vh = snap.Vh,
                JumpBuffer = snap.JumpBuffer, GripLeft = snap.GripLeft, FlyVx = snap.FlyVx, FlyVy = snap.FlyVy,
                Launched = snap.Launched, Air = air,
            };
            HitStopLeft = snap.HitStop;
            InvulnerableLeft = snap.Invulnerable;
            Tokens = snap.Tokens;
            GoldShare = Tokens > 0 && Finite(snap.GoldShare) ? Math.Clamp(snap.GoldShare, 0, 1) : 0;
            // The weight is not frozen; the held value is the best stand-in for how many orbs are behind the share.
            _orbsHeld = Tokens;
            Score = snap.Score;
            Quota = snap.Quota;
            SectionQuotaIncrement = snap.SectionQuotaIncrement;
            _bestBeaten = snap.BestBeaten;
            SectionAttempts = Math.Clamp(snap.SectionAttempts, 0, 9999);
            FirstTryStreak  = Math.Clamp(snap.FirstTryStreak, 0, 9999);
            PerfectCombo    = Math.Clamp(snap.PerfectCombo, 0, 9999);
            _sectionSpoilt  = snap.SectionSpoilt;   // ⚠ after IndexPhrases above, which clears it
            // ⚠ After IndexPhrases above, which allocates these. The restore marks every passed plane as
            // caught, so without this a resumed section would read as perfect however it was played.
            if (snap.RouteFailed is { } failed && failed.Length == _phraseFailed.Length)
                for (int g = 0; g < failed.Length; g++)
                {
                    if (!failed[g]) continue;
                    _phraseFailed[g] = true;
                    foreach (var e in _routeEvents[g])
                        if (e.Kind != InternodeEventKind.Mine) { e.GoldCandidate = false; e.Bonus = false; }
                }
            if (snap.High > HighScore) HighScore = snap.High;
            ResetPresentation();
            Phase = (InternodePhase)snap.Phase;
            PhaseTime = Phase == InternodePhase.Checkpoint ? Math.Min(snap.PhaseTime, InternodeTuning.CheckpointBeatSeconds) : snap.PhaseTime;
            committed = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] Internode snapshot discarded ({ex.Message})");
        }
        finally
        {
            if (!committed) BeginRun();
        }
    }

    private static bool Finite(double v) => ArcadeMath.Finite(v);
}
