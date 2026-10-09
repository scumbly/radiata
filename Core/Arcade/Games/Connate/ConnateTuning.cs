namespace ControllerWheel;

/// <summary>Connate's feel/physics knobs, all in one place.</summary>
public static class ConnateTuning
{
    // All distances use normalized playfield radii: center = 0 and the visible physics field is roughly 1.
    // Speeds are normalized radii/second; angular speeds are degrees/second unless the name says otherwise.

    // Rim-avatar response. The stick selects an absolute angle rather than steering angular velocity.
    public static double PlayerDeadzone = 0.20;
    /// <summary>Top rim speed. Tuned for relative steering, which holds top speed for as long as the stick is
    /// held (absolute aiming only reaches it in short darts). Deliberately a separate constant from any other
    /// game's, not shared.</summary>
    public static double PlayerDegPerSec = 235;
    public static double PlayerSnapDeg = 1.2;

    // Shot geometry and cadence. LaunchRadius is inside the craft orbit so the held orb reads as its payload.
    /// <summary>Base numbered-tile radius, and the one knob that scales the whole cast: every rank derives
    /// from it via <see cref="RadiusForRank"/>, and the player craft is drawn at 1.65× it.
    ///
    /// <para>⚠ <see cref="ClumpLimitRadius"/> is deliberately not scaled with it — a bigger cast in the same
    /// safe zone fills the board faster and shortens the round. That difficulty shift is the point, not a
    /// side effect.</para></summary>
    public static double TileRadius = 0.09108;
    public static double RankRadiusGrowth = 1.15;
    public static double CraftOrbitRadius = 0.90;
    public static double LaunchRadius = 0.815;
    public static double LaunchSpeed = 3.15;
    /// <summary>How long ✕ must be held for the release to be a CHARGED shot. Anything shorter is a normal
    /// shot at <see cref="LaunchSpeed"/>; holding longer adds nothing. The charge is read off the hold clock on
    /// the release edge, and off the same clock when the shot clock spends the piece while ✕ is still down.
    /// The bow draw reads 0 to full across exactly this stretch, so a full draw and a slam arrive together.</summary>
    public static double FullChargeSeconds = 0.5;
    /// <summary>Slack under <see cref="FullChargeSeconds"/> that still counts as a full charge, so a hold
    /// measured in 120 Hz steps is not refused by accumulated rounding.</summary>
    public static double FullChargeEpsilon = 1e-5;
    /// <summary>A charged shot leaves at this multiple of <see cref="LaunchSpeed"/>.</summary>
    public static double ChargedLaunchMultiplier = 2.60;
    /// <summary>The speed clamp while a body carries the charged flag; every other body keeps
    /// <see cref="MaxSpeedPerSec"/>. ⚠ Must stay above <c>LaunchSpeed * ChargedLaunchMultiplier</c> or the slam is
    /// clamped on its first substep.</summary>
    public static double ChargedMaxSpeedPerSec = 8.40;
    /// <summary>Restitution of the first closing contact a charged body makes; the contact also clears the
    /// flag, so the bounce is spent once and ordinary collisions (<see cref="Restitution"/>) follow.</summary>
    public static double ChargedImpactRestitution = 0.55;
    public static double FireCooldownSeconds = 0.18;
    public static double MuzzleClearance = 1.05;
    /// <summary>The stage-1 shot deadline, and the ceiling every later deadline is clamped to. The live
    /// deadline is <see cref="ShotClockDeadline"/>; nothing in the sim reads this constant directly except
    /// through it and the snapshot's upper bound.</summary>
    public static double ShotClockSeconds = 3.0;
    /// <summary>The tail of the shot clock the readout is actually visible for, as a fraction. Deliberately
    /// not full-span — a permanent readout is a fixture the eye stops reading.</summary>
    public static double ShotClockVisibleFraction = 1.0 / 3.0;

    // ── Difficulty stages ──
    // Banked value (Connate.ExplodedValue: bombed tiles plus boss-block payouts, never the live board) drives
    // a twenty-stage climb. A stage is DERIVED from the banked total on every read, so a single payout can
    // skip stages and nothing is stored or migrated; the top stage is a cap, not an ending. Three things get
    // harder: the shot deadline shortens, the garbage countdown is consumed faster, and a bomb costs more
    // charge. They run at the stage rate continuously — nothing in the run eases off on a clock.
    // ⚠ Arrays are not reachable from arcade-tuning.json (it reflects doubles and ints only); the scalar knobs
    // beside them are.
    /// <summary>Highest stage index (the player-facing stage is one more).</summary>
    public const int MaximumStageIndex = 19;

    /// <summary>Banked value that ENTERS player-facing stage <paramref name="stage"/> (2..20). Inclusive:
    /// exactly 100 is stage 2. Quadratic through stage 10 (25·(n−1)·(n+2)), then the gaps grow by 40 rather
    /// than 50, so stage 20 lands on exactly 10,000.</summary>
    public static long StageThresholdFor(int stage)
    {
        if (stage <= 1) return 0;
        stage = Math.Min(stage, MaximumStageIndex + 1);
        if (stage <= 10) return 25L * (stage - 1) * (stage + 2);
        int n = stage - 10;
        return 2700 + 550L * n + 20L * n * (n - 1);
    }

    /// <summary>Banked value that ENTERS stage 2, 3, … 20: nineteen entries, ascending.</summary>
    public static readonly long[] StageThresholds =
        [.. Enumerable.Range(2, MaximumStageIndex).Select(StageThresholdFor)];

    /// <summary>Seconds taken off the shot deadline per stage index through <see cref="ShotClockTaperIndex"/>:
    /// 3.00 s at stage 1 down to 1.65 s at stage 10.</summary>
    public static double ShotClockStepSeconds = 0.15;
    /// <summary>Stage index at which the deadline's slope changes.</summary>
    public const int ShotClockTaperIndex = 9;
    /// <summary>Seconds taken off per stage index past <see cref="ShotClockTaperIndex"/>: 1.65 s at stage 10
    /// down to 0.50 s at stage 20.</summary>
    public static double LateShotClockStepSeconds = 0.115;
    /// <summary>The shortest deadline the clock can reach, which is also the stage-20 value.</summary>
    public const double ShotClockFloorSeconds = 0.5;

    /// <summary>Stage index 0..19 for a banked total. Thresholds are inclusive.</summary>
    public static int StageIndexFor(long banked)
    {
        int index = 0;
        foreach (long threshold in StageThresholds) if (banked >= threshold) index++; else break;
        return index;
    }

    /// <summary>Player-facing stage count: the thresholds plus the stage before the first.</summary>
    public static int StageCount => StageThresholds.Length + 1;

    /// <summary>The shot deadline at a stage index. Floored well above zero so a tuning override can't produce
    /// a clock that fires on the frame a piece is handed over.</summary>
    public static double ShotClockDeadline(int stageIndex)
    {
        int index = Math.Clamp(stageIndex, 0, MaximumStageIndex);
        double deadline = index <= ShotClockTaperIndex
            ? ShotClockSeconds - ShotClockStepSeconds * index
            : ShotClockSeconds - ShotClockStepSeconds * ShotClockTaperIndex
                - LateShotClockStepSeconds * (index - ShotClockTaperIndex);
        return Math.Clamp(deadline, ShotClockFloorSeconds, ShotClockSeconds);
    }

    /// <summary>How much of a second the garbage countdown loses per second of play at a stage index. The
    /// 10–30 s draw itself never changes; this scales how fast the stored countdown is consumed, so stage 20
    /// sees arrivals 2.5–7.5 s apart. 1.00 → 0.60 by 0.10 a stage through index 4, 0.55 → 0.35 by 0.05
    /// through index 9, then 0.34 → 0.25 by 0.01.</summary>
    public static double GarbageCountdownScale(int stageIndex)
    {
        int index = Math.Clamp(stageIndex, 0, MaximumStageIndex);
        double scale = index <= 4 ? 1.0 - 0.1 * index
            : index <= 9 ? 0.6 - 0.05 * (index - 4)
            : 0.35 - 0.01 * (index - 9);
        return Math.Max(0.05, scale);
    }

    // Heap dynamics. Gravity is a stable center spring; swirl applies the clockwise perpendicular component.
    // MaxSpeed must remain above LaunchSpeed or fresh shots will be silently clamped on their first substep.
    /// <summary>⚠ This does not control how tightly the heap packs. Measured over six shot patterns, a
    /// sevenfold gravity increase moves the settled mean radius by about 7%, non-monotonically — i.e. noise.
    /// The heap is contact-limited: once tiles touch, their positions are set by packing, and the solver
    /// separates overlaps positionally (<see cref="PositionCorrection"/>), so pressing harder just gets
    /// pushed back out by the same amount. Anyone reaching for this to shrink the cluster wants
    /// <see cref="TileRadius"/> or <see cref="ClumpLimitRadius"/> instead.
    ///
    /// <para>What it does own is loose bodies — the heap re-collapsing after a bomb blast, and garbage
    /// drifting in. It is high so a blast is answered quickly rather than settling at its own pace.</para>
    ///
    /// <para>⚠ Must stay well under <see cref="MaxSpeedPerSec"/> or an inbound shot gets clamped on its
    /// first substep.</para></summary>
    public static double GravityPerSec2 = 8.00;
    /// <summary>The heap's emergent spin — a perpendicular acceleration proportional to radius, not a
    /// rotation anyone applies. ⚠ Being an acceleration, the outer bodies gain the most; that's inherent to
    /// the model, not a side effect. The legible turn comes from <see cref="PlatterDegreesPerSecond"/>
    /// instead.</summary>
    public static double ClockwiseSwirlPerSec2 = 2.32;
    public static double LinearDragPerSec = 2.15;
    public static double MaxSpeedPerSec = 5.40;
    public static double Restitution = 0.16;
    public static double TangentialDamping = 0.08;
    public static double PositionCorrection = 0.84;
    public static double CollisionSlop = 0.0015;
    public static int PhysicsSubsteps = 2;
    public static int CollisionIterations = 2;
    // A collision first reserves a deterministic pair, then animates the bond before resolving the merge.
    // MergeLock prevents a newborn result from disappearing into another merge in the same visual beat.
    public static double MergeLockSeconds = 0.085;
    public static double MergeAnticipationSeconds = 0.110;
    public static double MergeBondStrength = 2.8;
    public static double MergeBondDampingPerSec = 8.0;
    public static double JellySeconds = 0.70;

    // Loss envelope. SizeArmed bodies beyond ClumpLimitRadius charge a recoverable normalized fuse.
    // SoftExcessBand makes deep excursions more urgent than a one-pixel threshold crossing.
    public static double ClumpLimitRadius = 0.55125;
    /// <summary>Seconds of continuous oversize before the round ends — the death timer. Long on purpose:
    /// crossing the limit ring should be a warning you can answer, not a verdict, and answering it means
    /// landing a bomb or a merge that pulls the clump back in — which takes shots, and shots take time.
    ///
    /// <para>⚠ This is the base. <see cref="SoftExcessBand"/> multiplies the burn rate the further out the
    /// clump strays, so a heap way over the line still dies fast; doubling this widens the gentle end of that
    /// curve, not the whole of it. It is also not <see cref="LimitBreachSeconds"/>, which is the brief
    /// slow-motion settle after the fuse fills.</para></summary>
    public static double BarelyOversizeGraceSeconds = 14.40;
    public static double SoftExcessBand = 0.075;
    public static double FuseRecoveryPerSec = 1.25;
    /// <summary>How fast an out-of-bounds tile's outline pulses, in FULL cycles (dark → orange → dark) per
    /// second at rest. The renderer multiplies this by <c>1 + SizeFuse × 2</c>, so the board gets visibly
    /// more agitated the closer the run is to ending.
    ///
    /// <para>At rest that's one breath roughly every three seconds; at a full fuse it's still only about one
    /// a second.</para></summary>
    public static double OverLimitPulseHz = 0.35;
    /// <summary>How much a full fuse multiplies the pulse rate: <c>1 + SizeFuse × this</c>. At 2.0 a board on
    /// the brink flickers three times as fast as one that has just strayed.</summary>
    public static double OverLimitFuseBoost = 2.0;
    public static int MaximumBodies = 120;

    // Presentation/state-transition durations. These do not affect arithmetic or collision outcomes.
    public static double IntroSeconds = 1.0;
    public static double IntroSkipGuardSeconds = 0.25;
    public static double LimitBreachSeconds = 0.65;
    public static double MergeFlashSeconds = 0.64;
    public static double ChainContinuationSeconds = 1.10;

    // Feed economy. AdvancedChance chooses base-bag versus advanced pool; the remaining fields shape that pool.
    // TopTierWeightMultiplier deliberately makes the highest 30% of unlocked ranks exceptional rather than routine.
    // The bias toward low tiles is deliberate — low tiles are what a heap is built out of. ⚠ The four knobs
    // are a matched set: the first two decide how often you draw from the advanced pool at all, the last two
    // decide what that pool hands you, and leaning on either pair alone changes the character rather than
    // the mix — retune them together.
    /// <summary>Chance of drawing from the advanced pool, per rank unlocked.</summary>
    public static double AdvancedChancePerRank = 0.10;
    /// <summary>Ceiling on that chance however deep the board goes.</summary>
    public static double AdvancedChanceMaximum = 0.50;
    /// <summary>Exponential decay across the advanced pool. Lower = flatter, spreading the pool further up
    /// the ladder instead of piling on its first two or three ranks.</summary>
    public static double AdvancedRankWeightDecay = 0.46;
    public static double TopTierFraction = 0.30;
    /// <summary>The extra multiplier on the top 30% of unlocked ranks, which keeps a newly reached tile
    /// exceptional rather than routine — rare by an order of magnitude, just not almost never.
    /// ⚠ ConnateProbe asserts the top tier stays under a few percent of advanced draws — raise this much
    /// further and that assertion is the thing that will tell you.</summary>
    public static double TopTierWeightMultiplier = 0.14;

    // ── Bombs ──
    // Bombs are earned: a shot that causes more than one merge is a combo, combos pay charge on an
    // exponential curve, and a full charge gifts a bomb immediately.
    /// <summary>Scales with the tiles. A bomb is a projectile rather than a tile, but it rides the rim as the
    /// held payload beside them — out of scale it reads as shrunk.</summary>
    public static double BombRadius = 0.0726;

    /// <summary>Charge needed to earn a bomb.
    ///
    /// <para>⚠ This is not "combos needed" — the award is exponential: a ×2 pays 1, a ×3 pays 2, a ×5 pays 8.
    /// At 8 a chain of shallow doubles is a real grind and one deep cascade pays outright, which is the shape
    /// the exponential curve is for.</para>
    ///
    /// <para>⚠ A cascade pays per merge, not per shot. Each merge beyond the first from a single shot
    /// counts once, so a three-merge chain pays for two combos — it falls out of <c>_chainDepth &gt; 1</c> in
    /// StepPhysics, and ConnateProbe pins it, because it is the whole reason a target this high is reachable
    /// rather than a grind.</para>
    ///
    /// <para>⚠ The meter draws one wedge per unit. Past about a dozen they stop being countable at a glance
    /// and the readout would want a bar instead.</para></summary>
    public static int BombChargePerBomb = 8;
    /// <summary>Extra charge a bomb costs per difficulty stage index, on top of <see cref="BombChargePerBomb"/>,
    /// for the first <see cref="BombCostMaximumSteps"/> indices: 8 at stage 1, 15 from stage 8 on. The live
    /// cost is <c>Connate.BombChargeCost</c>; a stage change leaves the charge already earned where it is and
    /// only moves the line it has to reach.</summary>
    public static int BombChargePerStage = 1;
    /// <summary>Stage indices that still raise the bomb cost; past this it stays flat.</summary>
    public const int BombCostMaximumSteps = 7;
    // ── Board clear ──
    // Emptying the field — every numbered tile and every garbage blob gone at once — is the rarest thing that
    // can happen here; it starts the boss-block encounter (ConnateBossBlock).
    /// <summary>How long the board-clear banner stays up. Longer than a combo's — it happens once in a run, if
    /// ever, and it lands on a board with nothing left to look at.</summary>
    public static double BoardClearDisplaySeconds = 2.60;

    // ── Boss block ──
    // Distances are normalized field radii, like everything above. Rim, launch and field radii are shared with
    // the Playdate edition; only these encounter numbers are ported from it.
    /// <summary>The share of the banked total the encounter's payout starts from, before the level-gap cap.
    /// Half the bank, rounded to the nearest integer (half to even).</summary>
    public static double BossBonusShare = 0.5;
    /// <summary>Seconds the block takes to fall from <see cref="BossDropStartRadius"/> to its resting size.</summary>
    public static double BossDropSeconds = 1.30;
    /// <summary>The block's radius when it begins falling in: 80% of the playfield's diameter.</summary>
    public static double BossDropStartRadius = 0.8;
    /// <summary>Resting radius: <c>BossRadiusBase + BossRadiusPerBomb × bombs</c>, so 0.3 / 0.4 / 0.5.</summary>
    public static double BossRadiusBase = 0.2;
    public static double BossRadiusPerBomb = 0.1;
    /// <summary>How fast the landing ring grows past the block's own radius, in radii per second.</summary>
    public static double BossWaveSpeed = 3.0;
    /// <summary>Seconds after landing before the fight can open, so the delivery and the ring have both read.</summary>
    public static double BossFightDelaySeconds = 1.1;
    /// <summary>Seconds the broken block's fragments take to leave the field.</summary>
    public static double BossBreakSeconds = 1.25;
    /// <summary>The block is a damped spring tethered to the centre: stiffness, damping, and the displacement
    /// limit as a fraction of its radius. ⚠ The limit is what makes every inward shot hit — it must stay
    /// inside <see cref="BossHitReach"/> or a bomb can pass beside the block.</summary>
    public static double BossTetherStiffness = 45;
    public static double BossTetherDamping = 8;
    public static double BossTetherLimit = 0.25;
    /// <summary>A bomb hits when its swept path passes within this fraction of the block's radius, plus the
    /// bomb's own radius, of the block's centre. The visible junk core, not the crystals.</summary>
    public static double BossHitReach = 0.65;
    /// <summary>How much of a bomb's velocity the block takes as a kick.</summary>
    public static double BossHitKick = 0.35;
    /// <summary>Seconds the impact flash and the block's shake last after a hit; a landing shakes for
    /// <see cref="BossLandShakeSeconds"/>.</summary>
    public static double BossHitFlashSeconds = 0.3;
    public static double BossLandShakeSeconds = 0.35;
    /// <summary>The block's turn in radians per second once landed, and the extra turn each hit adds.</summary>
    public static double BossSpinPerSec = 0.45;
    public static double BossHitSpin = 0.12;
    /// <summary>Ladder rank whose size the payout's motes (a gold stream, 6 + 1.75 per rank) and the hit's
    /// blast are cut to. Presentation only; the value paid is the reward.</summary>
    public static int BossMoteRank = 8;
    public static int BossHitBlastRank = 2;
    /// <summary>The most one hit can pay; the snapshot's upper bound on a stored reward. The real ceiling is
    /// half the widest level gap (455).</summary>
    public const long BossRewardCeiling = 2000;

    /// <summary>How long the on-screen combo counter stays up after the last merge of a chain. Slightly longer
    /// than <see cref="ChainContinuationSeconds"/> so the number you earned is readable after the chain that
    /// earned it has formally expired.</summary>
    public static double ComboDisplaySeconds = 1.60;
    /// <summary>How long the stage-up shout ("STAGE n" over "INTENSITY INCREASES") stays up.</summary>
    public static double StageShoutSeconds = 2.8;
    /// <summary>How long the mid-run "NEW BEST" shout stays up.</summary>
    public static double NewBestShoutSeconds = 2.0;
    /// <summary>How long one combo's burst lives — the number popping at the merge, the spark flying to
    /// its bomb-charge pip, and the pip's flash on arrival. The spark lands at
    /// <see cref="ComboSparkArrivalFraction"/> of this, leaving the rest for the flash.</summary>
    public static double ComboBurstSeconds = 0.90;

    /// <summary>How long an earned bomb takes to fly from the charge meter into the payload. Firing is refused
    /// for the duration — long enough to be a reward you watch land, short enough that it never feels like the
    /// game took the controller away.</summary>
    public static double BombDeliverySeconds = 0.55;

    /// <summary>Bombs the player may hold in total — the loaded one plus any queued. Three is enough that a
    /// monster cascade is never wasted, and few enough that the board can't be banked into a trivial
    /// win.</summary>
    public static int MaximumBombs = 3;

    // ── Merge anticipation ──
    // A reserved pair reaches for each other like a magnetic blob. Purely visual: the sim's colliders are
    // untouched, so nothing here can change what merges or when.
    /// <summary>Peak stretch along the axis between a bonded pair, as a fraction of tile radius.</summary>
    public static double AnticipationStretch = 0.34;
    /// <summary>Peak lean toward the partner, as a fraction of the gap between them.</summary>
    public static double AnticipationLean = 0.16;

    // ── Blended-tile highlight sweep ──
    // A blended tile matches any tile of its number regardless of family, which makes it the most valuable
    // thing on the board — and the yin-yang alone reads as "decorated" rather than "important". A shine
    // crossing it every few seconds is the vocabulary a card game uses for a foil.
    /// <summary>Seconds between one tile's sweeps. Long enough that it's a punctuation rather than a shimmer.</summary>
    public static double BlendSweepPeriodSeconds = 3.2;
    /// <summary>How long one sweep takes to cross the tile.</summary>
    public static double BlendSweepSeconds = 0.55;
    /// <summary>Per-tile offset applied to the period, so a board of blended tiles ripples instead of
    /// flashing in unison — which would read as the screen blinking rather than the tiles being special.</summary>
    public static double BlendSweepStagger = 0.37;
    public static double ComboSparkArrivalFraction = 0.62;

    /// <summary>A bomb's size-limit immunity lasts until the next tile is fired or this long, whichever is
    /// longer — long enough that the respite a bomb is for does not end the instant the follow-up shot leaves
    /// the rim.</summary>
    public static double BombImmunityMinimumSeconds = 2.5;

    /// <summary>Highest rank the feed may still hand you a match for. Above this, the advanced pool stops one
    /// rank short of the board's highest numbered tile, so the top tile has to be built rather than gifted its
    /// own partner. Rank 3 is value 6 — the base bag's 1/2/3 plus the first double, all of which stay legal
    /// because an early board gated any harder would have nothing to feed.
    /// <para>⚠ The base bag is unaffected either way: 1, 2 and 3 are always legal.</para></summary>
    public static int FeedTopMatchExemptRank = 3;

    // ── Score motes ──
    // Collecting is the only way Connate scores, so this burst is the game's payoff moment and is tuned to be
    // the loudest good thing on the board. A bigger tile pays in more, bigger, later-arriving nuggets: the
    // stream lengthens, the counter ticks longer, and the last tick is the largest.
    /// <summary>How long one nugget takes to fly to the score counter once its scatter is spent. It lands,
    /// then the counter ticks up by that nugget's share — the count-up IS the arrival.</summary>
    public static double ScoreMoteFlightSeconds = 0.62;
    /// <summary>Nuggets a rank-0 tile pays. Everything above that is <see cref="ScoreMotesPerRank"/>.</summary>
    public static int ScoreMotesMinimum = 6;
    /// <summary>Extra nuggets per rank of the destroyed tile, so bombing a 384 visibly pays more than bombing
    /// a 3 does. Capped — past a couple of dozen the burst stops reading as countable and starts reading as
    /// fog.</summary>
    public static double ScoreMotesPerRank = 1.75;
    public static int ScoreMotesMaximum = 26;
    /// <summary>How long the first nugget scatters outward before the counter starts pulling. A beat of free
    /// flight is what makes the pull afterwards read as the score reaching out and taking them.</summary>
    public static double ScoreMoteScatterSeconds = 0.16;
    /// <summary>Spread of launch delays across the burst. ⚠ This is what turns one lump of value into a
    /// counting-up stream; dropping it to zero collapses the whole effect into a single jump.</summary>
    public static double ScoreMoteStaggerSeconds = 0.40;
    /// <summary>Drawn radius of a size-1 nugget, as a fraction of the field radius.</summary>
    public static double ScoreMoteRadiusFraction = 0.0165;
    /// <summary>Nugget growth per rank of the destroyed tile, before the per-nugget spread.</summary>
    public static double ScoreMoteSizePerRank = 0.085;
    /// <summary>Ceiling on that growth, so a very high rank stays a handful of treasure rather than boulders
    /// crossing the board.</summary>
    public static double ScoreMoteSizeMaximum = 2.1;
    /// <summary>Smallest and largest nugget within one burst, as multipliers on its base size. The spread runs
    /// small→large across the stream, so the last and biggest one lands on the biggest tick.</summary>
    public static double ScoreMoteSizeSpreadLow = 0.68;
    public static double ScoreMoteSizeSpreadHigh = 1.34;
    /// <summary>Floor on the gap between collect blips. Arrivals coalesce into one cue per frame anyway, but
    /// at 120 Hz that is still a buzz rather than a till — this makes them countable by ear.</summary>
    public static double ScoreCollectCueMinimumSeconds = 0.075;
    /// <summary>Floor between two "guarded" refusal blips — a blocked launch lane retries every step.</summary>
    public static double GuardedCueMinimumSeconds = 0.35;
    /// <summary>How fast the counter's arrival punch decays. Fast enough that consecutive arrivals each read
    /// as their own hit rather than smearing into one long swell.</summary>
    public static double ScorePulseDecayPerSec = 4.6;
    /// <summary>How fast the displayed score chases the real one, in fraction-closed per second. A visible
    /// climb, not an instant snap.</summary>
    public static double ScoreCountUpPerSec = 3.4;

    // ── Fired-tile spin ──
    /// <summary>Spin, in revolutions per second, given to a fired 1 or 2. Only those two: they're the
    /// complementary wedge and mouth, so rotation actually reads on them — on a complete circle it would be
    /// invisible. Damped away once the tile arms into the heap.</summary>
    public static double PieceSpinRevsPerSec = 1.35;
    public static double PieceSpinDampingPerSec = 2.6;
    public static double BombLifetimeSeconds = 1.10;
    public static double BombBlastRadius = 0.62;
    public static double BombBlastImpulse = 5.60;
    public static double BombBlastFalloffExponent = 1.35;
    public static double BombExplosionSeconds = 0.72;
    /// <summary>What a bomb fired at an empty field pays when it reaches the centre. Nothing to hit would
    /// otherwise turn an earned bomb into a dud; instead it detonates as if it had struck a tile of this value.
    /// Not on the tile ladder — the motes carry the exact sum, and the blast art is sized off the nearest rank.</summary>
    public static long BombEmptyFieldValue = 100;

    // Garbage uses elapsed active-play time. SpawnRadius is intentionally outside OuterHardLimitRadius;
    // EnteringPlayfield temporarily bypasses containment until the incoming body crosses that boundary.
    /// <summary>Minimum garbage radius; the per-blob draw picks between this and the second-largest circle on
    /// the board, both scaled by <see cref="GarbageScale"/>. <c>0.068 × 1.15 × 1.10 × 1.20 × GarbageScale</c>.</summary>
    public static double GarbageRadius = 0.1032240 * GarbageScale;
    /// <summary>Garbage is drawn and collided this much larger than the numbered tiles it is sized against:
    /// the floor above, the second-largest-circle ceiling, and the opening blob all carry it, so every blob on
    /// the board grows together.</summary>
    public const double GarbageScale = 1.40;
    public static double GarbageIntervalMinimumSeconds = 10.0;
    public static double GarbageIntervalMaximumSeconds = 30.0;
    public static double GarbageSpawnRadius = 1.035;
    public static double GarbageEntrySpeed = 0.46;
    /// <summary>How long the garbage shatter runs. The sprite pieces have to cross the whole board in this
    /// time, so it sets their speed. Purely presentational — the garbage body is already gone from the
    /// simulation when this starts.</summary>
    public static double GarbageClearSeconds = 2.60;

    /// <summary>How far into the clear the shards break a second time — "a split second later". Early enough
    /// that the fragments still have travel left to spend.</summary>
    public static double GarbageSecondBreakFraction = 0.26;

    /// <summary>Spread of the per-fragment shrink stagger, as a fraction of the clear. Every piece scales to
    /// nothing rather than fading — a fading shard reads as smoke, and a fading black cel outline is the most
    /// obviously wrong thing that can happen on this board — and the stagger keeps them from all popping out
    /// on the same frame.</summary>
    public static double GarbageShardStagger = 0.35;


    // Two-layer containment: a visible soft rebound followed by an absolute center-position clamp.
    // This protects against bomb impulses without making the outer edge feel like a rigid billiards wall.
    public static double OuterCushionStartRadius = 0.79;
    public static double OuterHardLimitRadius = 0.955;
    public static double OuterCushionStrength = 42.0;
    public static double OuterRadialDamping = 9.0;

    /// <summary>The 1 and the 2 share one footprint, <see cref="StarPairFraction"/> of the 3's radius. Every
    /// rank from the 3 up compounds by <see cref="RankRadiusGrowth"/> off the base: at the default 15%,
    /// 3 = 1.15x base, 6 = 1.3225x, 12 = 1.520875x, and so on.</summary>
    public static double RadiusForRank(int rank) => rank switch
    {
        0 or 1 => TileRadius * Math.Max(1.0, RankRadiusGrowth) * StarPairFraction,
        _ => TileRadius * Math.Pow(Math.Max(1.0, RankRadiusGrowth), Math.Max(0, rank - 1)),
    };

    /// <summary>The shared collider radius of the 1 and the 2, as a fraction of the 3's — one size for both
    /// ranks, since the current art draws each as a full disc with its shape as a face inside a rim rather
    /// than as a silhouette.
    ///
    /// <para>⚠ <see cref="ConnateBody.AreaFraction"/> divides this back out for both ranks so
    /// <c>mass(1) + mass(2) == mass(3)</c> still holds exactly. That identity is what keeps a merge from
    /// nudging the heap, and it is asserted; change this and the masses follow automatically, but never make
    /// one of the pair a literal again.</para></summary>
    public static double StarPairFraction = 0.76;

    /// <summary>The star face's radius as a fraction of the tile it sits on — the drawn shape only; it does
    /// not affect collision. Reads 1/√3 ≈ 0.577 rounded up: the share the original 120° wedge had, with a
    /// little back for the star's points overhanging its circle. Used by the vector fallback and by
    /// <c>tools/SpriteTemplates</c>, which is where the artist's guides come from.</summary>
    public static double StarRadiusFraction = 0.60;

    /// <summary>Inner radius of the five-pointed star, as a fraction of its outer radius. 0.382 is the
    /// regular pentagram's own ratio (1/φ²); anything larger stops reading as a star.</summary>
    public static double StarInnerFraction = 0.382;

    /// <summary>How far in the 1's pentagon collider sits from its star's points, as a fraction of the drawn
    /// outer radius. Just inside 1.0: the corners align with the points, but the last few percent of each
    /// spike is outside the collider so a tip can overlap a neighbour without catching.</summary>
    public static double StarColliderInset = 0.90;

    /// <summary>Radius of the star cut out of a 2, as a fraction of the 2's radius. Matches the 1's drawn
    /// outer radius exactly, so the two shapes are visibly the same size and the fit is obvious.</summary>
    public static double StarCutoutFraction = StarRadiusFraction;

    /// <summary>The whole heap turns clockwise like a record platter. This is a rigid rotation applied to
    /// every body's position and velocity together, not a force — the emergent swirl
    /// (<see cref="ClockwiseSwirlPerSec2"/>) never produced legible rotation because gravity and collision
    /// damping ate it. A rigid turn cannot disturb relative contacts, so it costs the physics nothing.</summary>
    public static double PlatterDegreesPerSecond = 7.0;
}
