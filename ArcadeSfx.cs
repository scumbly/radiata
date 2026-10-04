using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControllerWheel;

/// <summary>The Arcade's own sound layer: a bespoke sample bank per game plus one shared chrome vocabulary for
/// the picker, pause menu, how-to card, prompts, READY beat and guard card. Sits beside <see cref="Sfx"/> the way
/// <see cref="KawaiiXylophone"/> does — it decides WHICH take at WHAT pitch and gain, and hands the buffer to
/// <see cref="SfxEngine"/>, which owns mixing, the limiter and the voice cap. Nothing here opens its own audio path.
///
/// <para><b>Banks.</b> A role is a <see cref="Bank"/>: one or more takes of the same sound. Playing one picks a take
/// other than the last if it has several (nothing ships that way — one take per role; a small pitch
/// jitter is the only variation), and optionally climbs a
/// pitch LADDER on a count the sim exposes (combo, cascade size, merge rank, catch streak). Big moments LAYER two
/// banks a few tens of milliseconds apart.</para>
///
/// <para><b>Throttles</b> are wall-clock and host-side, for cues a sim can raise faster than a sound reads. The
/// sim's own throttles (Connate's collect cadence, Internode's token spacing) stay where they are.</para>
///
/// <para><b>Fail-quiet.</b> A missing resource decodes to an empty buffer and plays as silence — which is exactly
/// what the public mirror does, since the licensed WAVs under <c>Assets\sfx\arcade\</c> are not redistributed
/// (THIRD-PARTY-LICENSES.md §4c). Gated on <see cref="Sfx.Enabled"/> like every other sound.</para>
///
/// <para>Family gains, ladder shape and throttle intervals are <see cref="ArcadeSfxTuning"/> fields, so the dev
/// <c>arcade-tuning.json</c> tunes them by ear without a rebuild; per-take gains live on the banks below.</para></summary>
internal static class ArcadeSfx
{
    internal enum Family { Chrome, Generic, Kabloom, Connate, PetalPop, Internode }

    /// <summary>One role's sound: its takes and how they play. Runtime rotation state lives on the instance
    /// (UI thread only, like the xylophone sequencer).</summary>
    internal sealed class Bank
    {
        public Bank(Family family, string name, float gain, double jitter, params string[] variants)
        { Family = family; Name = name; Gain = gain; Jitter = jitter; Variants = variants; }

        public Family   Family   { get; }
        public string   Name     { get; }
        /// <summary>Resource stems under <c>Assets\sfx\arcade\</c>, without extension.</summary>
        public string[] Variants { get; }
        public float    Gain     { get; }
        /// <summary>± uniform pitch ratio per play; 0 = fixed.</summary>
        public double   Jitter   { get; }

        internal int  LastVariant = -1;
        internal long LastPlayedMs;
    }

    // ── Engine glue ───────────────────────────────────────────────────────────

    /// <summary>A take by resource stem. A stem prefixed <c>shared:</c> names the WHEEL's own set instead of the
    /// arcade folder — Connate's merge is deliberately the wheel's fired blip pitched up, the sound that game
    /// shipped with, and playing the existing resource reproduces it exactly rather than re-cutting it.</summary>
    private static float[] Load(string stem) =>
        SfxEngine.LoadResource(stem.StartsWith(SharedPrefix, System.StringComparison.Ordinal)
            ? $"pack://application:,,,/Assets/sfx/{stem[SharedPrefix.Length..]}.wav"
            : $"pack://application:,,,/Assets/sfx/arcade/{stem}.wav");

    internal const string SharedPrefix = "shared:";

    /// <summary>Random among the OTHER takes, so a rapid cue never repeats a take back to back.</summary>
    private static int NextVariant(Bank b)
    {
        int n = b.Variants.Length;
        if (n <= 1) return 0;
        int i = System.Random.Shared.Next(n - 1);
        if (i >= b.LastVariant) i++;
        return b.LastVariant = i;
    }

    private static double Jitter(double j) =>
        j <= 0 ? 1.0 : 1.0 + (System.Random.Shared.NextDouble() * 2 - 1) * j;

    /// <summary>Pitch ratio for <paramref name="step"/> rungs up the ladder (0 = as recorded).</summary>
    private static double Ladder(int step) =>
        step <= 0 ? 1.0
        : System.Math.Pow(2.0, System.Math.Min(step, ArcadeSfxTuning.LadderMaxSteps) * ArcadeSfxTuning.LadderSemitones / 12.0);

    private static bool Throttled(Bank b, int minIntervalMs)
    {
        if (minIntervalMs <= 0) return false;
        long now = System.Environment.TickCount64;
        if (now - b.LastPlayedMs < minIntervalMs) return true;
        b.LastPlayedMs = now;
        return false;
    }

    private static float FamilyGain(Family f) => (float)(ArcadeSfxTuning.SfxMasterGain * (f switch
    {
        Family.Chrome    => ArcadeSfxTuning.ChromeGain,
        Family.Kabloom   => ArcadeSfxTuning.KabloomGain,
        Family.Connate   => ArcadeSfxTuning.ConnateGain,
        Family.PetalPop  => ArcadeSfxTuning.PetalPopGain,
        Family.Internode => ArcadeSfxTuning.InternodeGain,
        _                => 1.0,
    }));

    /// <summary>Play one take of <paramref name="b"/>. <paramref name="ladder"/> climbs the pitch ladder;
    /// <paramref name="pitch"/> is a fixed ratio on top (a role that is "the same sound, lower"); an empty buffer
    /// (no such resource in this build) is silence.</summary>
    private static void Play(Bank b, int ladder = 0, double pitch = 1.0, float gain = 1f, int minIntervalMs = 0)
    {
        if (!Sfx.Enabled || Throttled(b, minIntervalMs)) return;
        var buf = Load(b.Variants[NextVariant(b)]);
        if (buf.Length == 0) return;
        SfxEngine.Play(buf, b.Gain * gain * FamilyGain(b.Family), pitch * Jitter(b.Jitter) * Ladder(ladder));
    }

    /// <summary>Two banks, the second nudged late so the pair reads as one event with a tail (impact then
    /// shards, fanfare then payout). Same async-void containment as <c>Sfx.PlayDelayed</c>; <see cref="Sfx.Enabled"/>
    /// is re-checked when the second half fires. A game closed inside the gap plays a harmless tail.</summary>
    private static async void Layer(Bank first, Bank second, int delayMs,
                                    int ladderFirst = 0, int ladderSecond = 0,
                                    double pitchFirst = 1.0, double pitchSecond = 1.0)
    {
        Play(first, ladderFirst, pitchFirst);
        try { await Task.Delay(delayMs); } catch { return; }
        Play(second, ladderSecond, pitchSecond);
    }

    /// <summary>The arcade is opening: wake the device now so the first tick isn't the one that pays for it
    /// (the wheel does the same in <see cref="Sfx.WheelOpened"/>), and decode the chrome off the UI thread.</summary>
    public static void Opened()
    {
        if (!Sfx.Enabled) return;
        SfxEngine.Prime();
        _ = Task.Run(() => { foreach (var b in ChromeBanks) foreach (var v in b.Variants) Load(v); });
    }

    /// <summary>Decode one game's banks off-thread when that game comes up. <see cref="Opened"/> warms only
    /// the chrome; the per-game takes were cold until their first play, which happens inside the frame pump.</summary>
    public static void Warm(string gameId)
    {
        if (!Sfx.Enabled) return;
        Family family = gameId switch
        {
            "kabloom"   => Family.Kabloom,
            "connate"   => Family.Connate,
            "petalpop"  => Family.PetalPop,
            "internode" => Family.Internode,
            _           => Family.Generic,
        };
        _ = Task.Run(() =>
        {
            foreach (var b in AllBanks)
                if (b.Family == family) foreach (var v in b.Variants) Load(v);
        });
    }

    // ── Banks ─────────────────────────────────────────────────────────────────
    // Gains are per-take trims by ear; the family trims in ArcadeSfxTuning stack on top. Ingest already
    // peak-normalises each take, so a trim here says "this role sits under/over its neighbours", nothing else.

    private static Bank B(Family f, string name, float gain, double jitter, params string[] v) => new(f, name, gain, jitter, v);

    // Chrome: low, short and soft — furniture rather than beeps. The cabinet-to-cabinet tick sets the
    // register and is the quietest thing here. One take per role: several takes for the same event read as
    // different events, so rotation stays available in Bank but nothing ships with more than one take.
    // The cabinets appear silently: no picker-open bank. The tick and the select carry the launcher.
    private static readonly Bank ChPickerNav     = B(Family.Chrome, "picker-nav",     0.18f, 0.02, "chrome-picker-nav");
    private static readonly Bank ChPickerSelect  = B(Family.Chrome, "picker-select",  0.55f, 0,    "chrome-picker-select");
    private static readonly Bank ChClose         = B(Family.Chrome, "close",          0.50f, 0,    "chrome-close");
    // ⚠ The four banks below borrow the Mesa wheel's own pair, and they carry the WHEEL'S gains — 1.2 for the
    // tap, 1.0 for the fire (see Sfx.SetMesa / PhysicalGain). Those are not arbitrary: tap.wav is a quiet
    // recording that the wheel lifts ABOVE its fire to be heard at all, so scaling the pair down "because
    // chrome is soft" silences the tap while leaving the fire audible — which is exactly what 0.40/0.45 did.
    // Move them together or not at all; the ratio is the sound.
    private static readonly Bank ChPauseOpen     = B(Family.Chrome, "pause-open",     1.00f, 0,    "shared:mesa-slice-fired");   // the menu arriving
    private static readonly Bank ChPauseClose    = B(Family.Chrome, "pause-close",    0.50f, 0,    "chrome-pause-close");
    private static readonly Bank ChPauseNav      = B(Family.Chrome, "pause-nav",      1.20f, 0.03, "shared:tap");                // the Mesa wheel's ARM: cursor moves
    private static readonly Bank ChOption        = B(Family.Chrome, "option",         1.00f, 0,    "shared:mesa-slice-fired");   // the Mesa wheel's FIRE: a choice taken
    private static readonly Bank ChSelect        = B(Family.Chrome, "select",         1.00f, 0,    "shared:mesa-slice-fired");   // ✕ on Resume / How to play
    private static readonly Bank ChConfirmOpen   = B(Family.Chrome, "confirm-open",   0.35f, 0,    "chrome-confirm-open");
    // ⚠ A press is a press. YES, NO and the reset it guards answer with the SAME Mesa fire the menu rows
    // answer with — same stem AND same gain, or "the same sound" is only true until someone reads the gains.
    // The prompt's ARRIVAL keeps its own cue above: that is not a press.
    private static readonly Bank ChConfirmYes    = B(Family.Chrome, "confirm-yes",    1.00f, 0,    "shared:mesa-slice-fired");
    private static readonly Bank ChConfirmNo     = B(Family.Chrome, "confirm-no",     1.00f, 0,    "shared:mesa-slice-fired");
    private static readonly Bank ChReset         = B(Family.Chrome, "reset",          1.00f, 0,    "shared:mesa-slice-fired");
    private static readonly Bank ChHowToOpen     = B(Family.Chrome, "howto-open",     0.55f, 0,    "chrome-howto-open");
    private static readonly Bank ChHowToClose    = B(Family.Chrome, "howto-close",    0.50f, 0,    "chrome-howto-close");
    private static readonly Bank ChReady         = B(Family.Chrome, "ready",          0.30f, 0,    "chrome-ready");
    private static readonly Bank ChGuardShow     = B(Family.Chrome, "guard-show",     0.55f, 0,    "chrome-guard-show");
    private static readonly Bank ChGuardClear    = B(Family.Chrome, "guard-clear",    0.55f, 0,    "chrome-guard-clear");
    private static readonly Bank ChGuardOverride = B(Family.Chrome, "guard-override", 0.60f, 0,    "chrome-guard-override");

    // Generic: the nine-name vocabulary drop-in script games speak.
    private static readonly Bank GnFire     = B(Family.Generic, "fire",     0.35f, 0.03, "generic-fire");
    private static readonly Bank GnTick     = B(Family.Generic, "tick",     0.30f, 0.03, "generic-tick");
    private static readonly Bank GnGood     = B(Family.Generic, "good",     0.50f, 0,    "generic-good");
    private static readonly Bank GnDenied   = B(Family.Generic, "denied",   0.45f, 0,    "generic-denied");
    private static readonly Bank GnKill     = B(Family.Generic, "kill",     0.50f, 0.04, "generic-kill");
    private static readonly Bank GnZap      = B(Family.Generic, "zap",      0.60f, 0.03, "generic-zap");
    private static readonly Bank GnHurt     = B(Family.Generic, "hurt",     0.70f, 0,    "generic-hurt");
    private static readonly Bank GnClear    = B(Family.Generic, "clear",    0.70f, 0,    "generic-clear");
    private static readonly Bank GnGameOver = B(Family.Generic, "gameover", 0.75f, 0,    "generic-gameover");

    // Kabloom: soft, organic, papery.
    private static readonly Bank KbReveal       = B(Family.Kabloom, "reveal",        0.45f, 0.02, "kabloom-reveal");
    // ⚠ THE SWEEP is these two, not "reveal" — a press opening more than one cell raises Cue.Bloom instead.
    // They also climb the pitch ladder with the cascade's size and layer past BloomLayerAt, so the widest
    // sweep is the loudest AND brightest thing on the board: it needs the most headroom, not the least.
    private static readonly Bank KbBloom        = B(Family.Kabloom, "bloom",         0.26f, 0.02, "kabloom-bloom");
    private static readonly Bank KbBloomLayer   = B(Family.Kabloom, "bloom-layer",   0.18f, 0,    "kabloom-bloom-layer");
    private static readonly Bank KbFlag         = B(Family.Kabloom, "flag",          0.50f, 0.02, "kabloom-flag");
    private static readonly Bank KbGuarded      = B(Family.Kabloom, "guarded",       0.40f, 0,    "kabloom-guarded");
    private static readonly Bank KbDiamond      = B(Family.Kabloom, "diamond",       0.55f, 0.03, "kabloom-diamond");
    // The failure strike, layered 90 ms apart by Kabloom.Mine(). ⚠ One event in two takes — move the gains
    // together, or the pair stops reading as a single strike and starts reading as buzz-then-thud.
    private static readonly Bank KbMineBuzz     = B(Family.Kabloom, "mine-buzz",     0.55f, 0,    "kabloom-mine-buzz");
    private static readonly Bank KbMineThud     = B(Family.Kabloom, "mine-thud",     0.60f, 0,    "kabloom-mine-thud");
    private static readonly Bank KbBeeDeparts   = B(Family.Kabloom, "bee-departs",   0.35f, 0.03, "kabloom-bee-departs");
    private static readonly Bank KbCleared      = B(Family.Kabloom, "cleared",       0.70f, 0,    "kabloom-cleared");
    private static readonly Bank KbLevelUp      = B(Family.Kabloom, "levelup",       0.60f, 0,    "kabloom-levelup");
    private static readonly Bank KbGrowth       = B(Family.Kabloom, "growth",        0.40f, 0,    "kabloom-growth");
    private static readonly Bank KbContinue     = B(Family.Kabloom, "continue",      0.55f, 0,    "kabloom-continue");
    private static readonly Bank KbCampaign     = B(Family.Kabloom, "campaign",      0.80f, 0,    "kabloom-campaign");
    private static readonly Bank KbNewBest      = B(Family.Kabloom, "newbest",       0.70f, 0,    "internode-newbest");

    // Connate: glassy, metallic, a coin till.
    private static readonly Bank CnFire           = B(Family.Connate, "fire",            0.40f, 0.03, "connate-fire");
    private static readonly Bank CnMerge          = B(Family.Connate, "merge",           0.55f, 0,    "connate-merge");
    private static readonly Bank CnChainLayer     = B(Family.Connate, "chain-layer",     0.45f, 0,    "connate-chain-layer");
    private static readonly Bank CnCollect        = B(Family.Connate, "collect",         0.40f, 0.04, "connate-collect");
    private static readonly Bank CnCombo          = B(Family.Connate, "combo",           0.45f, 0.02, "connate-combo");
    private static readonly Bank CnBombEarned     = B(Family.Connate, "bomb-earned",     0.65f, 0,    "connate-bomb-earned");
    private static readonly Bank CnBombExplode    = B(Family.Connate, "bomb-explode",    0.75f, 0.03, "connate-bomb-explode");
    private static readonly Bank CnBombGlass      = B(Family.Connate, "bomb-glass",      0.55f, 0.03, "connate-bomb-glass");
    private static readonly Bank CnBoardClear     = B(Family.Connate, "boardclear",      0.80f, 0,    "connate-boardclear");
    private static readonly Bank CnBoardClearPour = B(Family.Connate, "boardclear-pour", 0.45f, 0,    "connate-boardclear-pour");
    // ⚠ One new-best voice across the arcade: Connate and Petalpop play Internode's take. Each keeps
    // its own family trim; the sample is what is shared.
    private static readonly Bank CnNewBest        = B(Family.Connate, "newbest",         0.70f, 0,    "internode-newbest");
    private static readonly Bank CnWarning        = B(Family.Connate, "warning",         0.45f, 0,    "connate-warning");
    private static readonly Bank CnRecover        = B(Family.Connate, "recover",         0.55f, 0,    "connate-recover");
    private static readonly Bank CnGuarded        = B(Family.Connate, "guarded",         0.40f, 0,    "connate-guarded");
    private static readonly Bank CnGarbageArrive  = B(Family.Connate, "garbage-arrive",  0.45f, 0.03, "connate-garbage-arrive");
    private static readonly Bank CnGarbageCleared = B(Family.Connate, "garbage-cleared", 0.50f, 0.04, "connate-garbage-cleared");
    private static readonly Bank CnGameOver       = B(Family.Connate, "gameover",        0.80f, 0,    "connate-gameover");
    private static readonly Bank CnGameOverTail   = B(Family.Connate, "gameover-tail",   0.60f, 0,    "connate-gameover-layer");

    // Petalpop: poppy, bubbly, punchy.
    private static readonly Bank PpPaddle       = B(Family.PetalPop, "paddle",        0.40f, 0.05, "petalpop-paddle");
    private static readonly Bank PpPop          = B(Family.PetalPop, "pop",           0.55f, 0.03, "petalpop-pop");
    private static readonly Bank PpChip         = B(Family.PetalPop, "chip",          0.45f, 0.03, "petalpop-chip");
    private static readonly Bank PpSplit        = B(Family.PetalPop, "split",         0.65f, 0,    "petalpop-split");
    private static readonly Bank PpSplitSparkle = B(Family.PetalPop, "split-sparkle", 0.50f, 0,    "petalpop-split-sparkle");
    private static readonly Bank PpSmash        = B(Family.PetalPop, "smash",         0.60f, 0.03, "petalpop-smash");
    private static readonly Bank PpSmashArm     = B(Family.PetalPop, "smasharm",      0.45f, 0,    "petalpop-smasharm");
    private static readonly Bank PpServe        = B(Family.PetalPop, "serve",         0.45f, 0.03, "petalpop-serve");
    // A ball lost that is NOT the last one plays Internode's jump take, quietly: a small "gone" rather than a
    // failure, since the round is still alive. The last ball keeps its own thud (PpLifeLost).
    private static readonly Bank PpLost         = B(Family.PetalPop, "lost",          0.22f, 0.03, "internode-jump");
    private static readonly Bank PpLifeLost     = B(Family.PetalPop, "lifelost",      0.70f, 0,    "petalpop-lifelost");
    private static readonly Bank PpGameOver     = B(Family.PetalPop, "gameover",      0.80f, 0,    "petalpop-gameover");
    private static readonly Bank PpGameOverTail = B(Family.PetalPop, "gameover-tail", 0.60f, 0,    "petalpop-gameover-layer");
    private static readonly Bank PpLevelClear   = B(Family.PetalPop, "levelclear",    0.75f, 0,    "petalpop-levelclear");
    private static readonly Bank PpCore         = B(Family.PetalPop, "core",          0.55f, 0,    "petalpop-core");
    private static readonly Bank PpCorePing     = B(Family.PetalPop, "coreping",      0.50f, 0.02, "petalpop-coreping");
    private static readonly Bank PpExtraLife    = B(Family.PetalPop, "extralife",     0.70f, 0,    "petalpop-extralife");
    private static readonly Bank PpExtraLifeTail= B(Family.PetalPop, "extralife-tail", 0.50f, 0,    "petalpop-extralife-layer");
    private static readonly Bank PpWin          = B(Family.PetalPop, "win",           0.85f, 0,    "petalpop-win");
    private static readonly Bank PpCombo        = B(Family.PetalPop, "combo",         0.45f, 0.02, "petalpop-combo");
    private static readonly Bank PpNewBest      = B(Family.PetalPop, "newbest",       0.70f, 0,    "internode-newbest");

    // Internode: one take per moment, played as recorded unless a cue below says otherwise.
    // ⚠ Gains are mix positions against peak-normalised takes, so a re-cut under the same stem does not move
    // them; a take normalised to a DIFFERENT peak target does, and the manifest is where to check that before
    // trusting a gain across a swap. ⚠ Two cues have NO take of their own on purpose: the elevated catch is
    // the token higher and the turn to gold is the bonus lighter and higher, so each pair is one voice. Three
    // more have no take at all for now: mine thud, fail tail, run start — their cues stay in the
    // vocabulary and play nothing, so wiring them back is a bank, not a plumbing change.
    // ⚠ The catch has no jitter: every orb is the identical sound, and the elevated one differs from it
    // by its fixed pitch step alone.
    private static readonly Bank InToken          = B(Family.Internode, "token",           0.20f, 0,    "internode-token");
    private static readonly Bank InTokenElevated  = B(Family.Internode, "token-elevated",  0.22f, 0,    "internode-token");
    private static readonly Bank InBonus          = B(Family.Internode, "bonus",           0.60f, 0,    "internode-bonus");
    private static readonly Bank InGoldTurn       = B(Family.Internode, "goldturn",        0.55f, 0,    "internode-goldturn");
    private static readonly Bank InBankFlight     = B(Family.Internode, "bankflight",      0.50f, 0,    "internode-bankflight");
    private static readonly Bank InJump           = B(Family.Internode, "jump",            0.35f, 0.04, "internode-jump");
    private static readonly Bank InRimExit        = B(Family.Internode, "rimexit",         0.15f, 0.03, "internode-rimexit");
    private static readonly Bank InMine           = B(Family.Internode, "mine",            0.70f, 0.03, "internode-mine");
    private static readonly Bank InMineStruck     = B(Family.Internode, "minestruck",      0.60f, 0.03, "internode-minestruck");
    private static readonly Bank InFell           = B(Family.Internode, "fell",            0.65f, 0,    "internode-fell");
    private static readonly Bank InRespawn        = B(Family.Internode, "respawn",         0.50f, 0,    "internode-respawn");
    private static readonly Bank InCheckpoint     = B(Family.Internode, "checkpoint",      0.65f, 0,    "internode-checkpoint");
    private static readonly Bank InStageUp        = B(Family.Internode, "stageup",         0.70f, 0,    "internode-stageup");
    private static readonly Bank InNewBest        = B(Family.Internode, "newbest",         0.70f, 0,    "internode-newbest");

    private static readonly Bank[] ChromeBanks =
    [
        ChPickerNav, ChPickerSelect, ChClose, ChPauseOpen, ChPauseClose, ChPauseNav, ChOption,
        ChConfirmOpen, ChConfirmYes, ChConfirmNo, ChReset, ChHowToOpen, ChHowToClose, ChReady, ChGuardShow,
        ChGuardClear, ChGuardOverride,
    ];

    /// <summary>Every bank, for the harness's resource check (a typo in a stem must not ship as silence).</summary>
    internal static readonly Bank[] AllBanks =
    [
        .. ChromeBanks,
        GnFire, GnTick, GnGood, GnDenied, GnKill, GnZap, GnHurt, GnClear, GnGameOver,
        KbReveal, KbBloom, KbBloomLayer, KbFlag, KbGuarded, KbDiamond, KbMineBuzz, KbMineThud, KbBeeDeparts,
        KbCleared, KbLevelUp, KbGrowth, KbContinue, KbCampaign, KbNewBest,
        CnFire, CnMerge, CnChainLayer, CnCollect, CnCombo, CnBombEarned, CnBombExplode, CnBombGlass, CnBoardClear,
        CnBoardClearPour, CnNewBest, CnWarning, CnRecover, CnGuarded, CnGarbageArrive, CnGarbageCleared, CnGameOver,
        CnGameOverTail,
        PpPaddle, PpPop, PpChip, PpSplit, PpSplitSparkle, PpSmash, PpSmashArm, PpServe, PpLost,
        PpLifeLost, PpGameOver, PpGameOverTail, PpLevelClear, PpCore, PpCorePing, PpExtraLife, PpExtraLifeTail, PpWin, PpCombo, PpNewBest,
        InToken, InTokenElevated, InBonus, InGoldTurn, InBankFlight, InJump, InRimExit, InMine, InMineStruck, InFell,
        InRespawn, InCheckpoint, InStageUp, InNewBest,
    ];

    // ── Vocabularies ─────────────────────────────────────────────────────────

    /// <summary>Host chrome: the picker, the pause menu, the how-to card, prompts, READY, the guard card.</summary>
    public static class Chrome
    {
        public static void PickerNav()     => Play(ChPickerNav);
        public static void PickerSelect()  => Play(ChPickerSelect);
        public static void Close()         => Play(ChClose);
        public static void PauseOpen()     => Play(ChPauseOpen);
        public static void PauseClose()    => Play(ChPauseClose);
        public static void PauseNav()      => Play(ChPauseNav);
        public static void OptionChange()  => Play(ChOption);
        public static void Select()        => Play(ChSelect);
        public static void ConfirmOpen()   => Play(ChConfirmOpen);
        public static void ConfirmYes()    => Play(ChConfirmYes);
        public static void ConfirmNo()     => Play(ChConfirmNo);
        public static void Reset()         => Play(ChReset);
        public static void HowToOpen()     => Play(ChHowToOpen);
        public static void HowToClose()    => Play(ChHowToClose);
        public static void Ready()         => Play(ChReady);
        public static void GuardShow()     => Play(ChGuardShow);
        public static void GuardClear()    => Play(ChGuardClear);
        public static void GuardOverride() => Play(ChGuardOverride);
    }

    /// <summary>The fixed nine-name vocabulary drop-in games speak (docs/ARCADE.md); unknown names are silent.</summary>
    public static class Generic
    {
        public static void Fire()     => Play(GnFire);
        public static void Tick()     => Play(GnTick);
        public static void Good()     => Play(GnGood);
        public static void Denied()   => Play(GnDenied);
        public static void Kill()     => Play(GnKill);
        public static void Zap()      => Play(GnZap);
        public static void Hurt()     => Play(GnHurt);
        public static void Clear()    => Play(GnClear);
        public static void GameOver() => Play(GnGameOver);

        public static void Cue(string cue)
        {
            switch (cue)
            {
                case "fire":     Fire(); break;
                case "tick":     Tick(); break;
                case "good":     Good(); break;
                case "denied":   Denied(); break;
                case "kill":     Kill(); break;
                case "zap":      Zap(); break;
                case "hurt":     Hurt(); break;
                case "clear":    Clear(); break;
                case "gameover": GameOver(); break;
            }
        }
    }

    public static class Kabloom
    {
        /// <summary>ONE petal opened. Highlighting one is silent: the click belongs to the action.
        /// ⚠ A press that opens more than one cell raises <c>Cue.Bloom</c> INSTEAD of this
        /// (<c>Kabloom.Act</c>), so this is never the sweep — tune <see cref="Bloom"/> for that.</summary>
        public static void Reveal()  => Play(KbReveal);
        /// <summary>The cascade: pitched by how many cells opened, and layered with a brighter take past
        /// <see cref="ArcadeSfxTuning.BloomLayerAt"/>.</summary>
        public static void Bloom(int cells)
        {
            int step = cells switch { < 4 => 0, < 8 => 1, < 14 => 2, < 24 => 3, _ => 4 };
            if (cells >= ArcadeSfxTuning.BloomLayerAt) Layer(KbBloom, KbBloomLayer, 60, step, step);
            else Play(KbBloom, step);
        }
        public static void Flag()    => Play(KbFlag);
        public static void Unflag()  => Play(KbFlag, pitch: 0.85);
        public static void Guarded() => Play(KbGuarded);
        /// <summary>A gem landing in the counter; the run of them climbs (<paramref name="index"/> = how many
        /// this level has banked so far).</summary>
        public static void Diamond(int index) => Play(KbDiamond, index, minIntervalMs: ArcadeSfxTuning.DiamondMinIntervalMs);
        public static void Mine()       => Layer(KbMineBuzz, KbMineThud, 90);
        public static void BeeDeparts() => Play(KbBeeDeparts);
        public static void Cleared()    => Play(KbCleared);
        public static void LevelUp()    => Play(KbLevelUp);
        public static void Growth()     => Play(KbGrowth);
        public static void Continue()   => Play(KbContinue);
        public static void CampaignComplete() => Play(KbCampaign);
        public static void NewBest()    => Play(KbNewBest);
    }

    public static class Connate
    {
        /// <summary>Playback speed of the borrowed wheel blip: the ratio the arcade played it at before the
        /// bespoke banks existed, so the restored cue is the same sound and not an approximation.</summary>
        private const double MergePitch = 1.30;

        public static void Fire() => Play(CnFire);

        /// <summary>A merge: flat, not pitched by rank — the restored original is one fixed blip.</summary>
        public static void Merge() => Play(CnMerge, pitch: MergePitch);
        /// <summary>A cascade: the merge blip plus a metal tail that climbs with the chain depth.</summary>
        public static void Chain(int depth) => Layer(CnMerge, CnChainLayer, 50, ladderSecond: depth - 1, pitchFirst: MergePitch);
        public static void Collect()          => Play(CnCollect);
        public static void ComboStep(int n)   => Play(CnCombo, n);
        public static void BombEarned()       => Play(CnBombEarned);
        public static void BombExplode()      => Layer(CnBombExplode, CnBombGlass, 40);
        public static void BoardClear()       => Layer(CnBoardClear, CnBoardClearPour, 200);
        public static void NewBest()          => Play(CnNewBest);
        public static void Warning()          => Play(CnWarning);
        public static void Recover()          => Play(CnRecover);
        public static void Guarded()          => Play(CnGuarded);
        public static void GarbageArrive()    => Play(CnGarbageArrive);
        public static void GarbageCleared()   => Play(CnGarbageCleared);
        public static void GameOver()         => Layer(CnGameOver, CnGameOverTail, 110);
    }

    public static class PetalPop
    {
        /// <summary>The ball meets a paddle — or a corner bumper, which is a paddle that never moves and speaks
        /// with the same voice (a second impact sound for the same physical event reads as a second event).</summary>
        public static void PaddleHit()          => Play(PpPaddle, minIntervalMs: ArcadeSfxTuning.PaddleMinIntervalMs);
        /// <summary>A petal popped: the chain climbs with the combo.</summary>
        public static void Pop(int combo)       => Play(PpPop, combo - 1);
        public static void Chip()               => Play(PpChip);
        public static void Split()              => Layer(PpSplit, PpSplitSparkle, 70);
        /// <summary>The smash take carries the impact, and the ordinary paddle hit is stacked under it a few
        /// semitones down so the connect still reads as the paddle it came off. Both start on the same frame —
        /// a delay between them would read as two events. ⚠ The paddle layer deliberately bypasses
        /// <see cref="PaddleHit"/>'s throttle: it belongs to THIS event, not to the paddle cadence.</summary>
        public static void Smash()
        {
            Play(PpSmash);
            Play(PpPaddle, pitch: ArcadeSfxTuning.SmashPaddlePitch, gain: (float)ArcadeSfxTuning.SmashPaddleGain);
        }
        public static void SmashArm()           => Play(PpSmashArm);
        public static void Serve()              => Play(PpServe);
        public static void Lost()               => Play(PpLost);
        public static void LifeLost()           => Play(PpLifeLost);
        public static void GameOver()           => Layer(PpGameOver, PpGameOverTail, 120);
        public static void LevelClear()         => Layer(PpLevelClear, PpCore, 80);
        /// <summary>A cold ball rang off the core: the bell, not the break.</summary>
        public static void CorePing()           => Play(PpCorePing);
        public static void ExtraLife()          => Layer(PpExtraLife, PpExtraLifeTail, 140);
        public static void Win()                => Play(PpWin);
        public static void ComboShout(int combo) => Play(PpCombo, combo - 2, minIntervalMs: ArcadeSfxTuning.ComboShoutMinIntervalMs);
        public static void NewBest()            => Play(PpNewBest);
    }

    public static class Internode
    {
        /// <summary>The catch sits under its recorded pitch; the elevated catch is a fixed step above
        /// this, so moving one moves both.</summary>
        private const double TokenPitch = 0.85;
        /// <summary>An orb caught; a streak of them climbs. Flat: a run of catches is the same sound each
        /// time, never a ladder.</summary>
        public static void Token()            => Play(InToken, pitch: TokenPitch);
        /// <summary>The token's own take a little higher: a tethered orb is the same pickup, so it must
        /// never read as a different one.</summary>
        public static void TokenElevated()    => Play(InTokenElevated, pitch: TokenPitch * 1.15);
        public static void Bonus()            => Play(InBonus);
        /// <summary>A pattern's last orb turning gold. Its own take, played as recorded; it rides the catch
        /// that armed it in place of that catch's tick, so it is still one sound per orb.</summary>
        public static void GoldTurn()         => Play(InGoldTurn);
        /// <summary>The banked orbs leaving the runner for the readout, once per gate, under the gate's own sound.</summary>
        public static void BankFlight()       => Play(InBankFlight);
        /// <summary>The whole air family sits well under its recorded pitch. One ratio for the three so
        /// they stay one register; the takes themselves carry the difference between them.</summary>
        private const double AirPitch = 0.60;
        public static void Jump()             => Play(InJump, pitch: AirPitch);
        /// <summary>The second take is recorded brighter than the first; a further step down brings the pair
        /// closer in register while the takes still tell them apart.</summary>
        public static void RimExit()          => Play(InRimExit, pitch: AirPitch);
        /// <summary>The hit, then its body 50 ms under it: the two takes read as one strike.</summary>
        public static void Mine()             => Layer(InMine, InMineStruck, 50);
        public static void Fell()             => Play(InFell);
        public static void Respawn()          => Play(InRespawn);
        public static void Checkpoint()       => Play(InCheckpoint);
        public static void StageUp()          => Play(InStageUp);
        /// <summary>Silent: a short hand is told by the readout alone. The cue is still raised and
        /// dispatched so a take is one bank away.</summary>
        public static void CheckpointFail()   { }
        /// <summary>No take for now. The cue is still raised and dispatched so a take is one bank away.</summary>
        public static void Begin()            { }
        public static void NewBest()          => Play(InNewBest);
    }
}
