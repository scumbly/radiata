namespace ControllerWheel;

/// <summary>Internode's feel and rule knobs. Mutable statics (never consts) so <see cref="ArcadeTuning.LoadOverrides"/>
/// can rewrite them from <c>arcade-tuning.json</c> on every open.
///
/// <para>Units: angles say <c>Deg</c> where they are degrees (the sim works in radians); heights are pipe
/// radii; z and every duration are seconds (forward speed is 1 s/s by definition, there is no speed knob).
/// Anything without a unit in its name is a plain factor or a count.</para></summary>
public static class InternodeTuning
{
    // ── Geometry ──────────────────────────────────────────────────────────────
    /// <summary>The surface ends here, degrees either side of the bottom; beyond it is the open top. A true half-pipe
    /// plus a small curl-in lip above the centreline: the lip is what tilts a launch off the rim inward, so a fast
    /// runner arcs across the open top instead of going straight up and dropping back. This is the base rim: the course's closures raise it toward <see cref="RimMaxDeg"/> along a section (see
    /// the Course group), and the gate is always approached at the base.</summary>
    public static double RimDeg = 100;
    /// <summary>Widest opening the rim profile may reach, degrees; equal to the base, so the course only ever closes
    /// the pipe from the half-pipe at rest. Kept as the profile's lower clamp.</summary>
    public static double RimMinDeg = 100;
    /// <summary>Most closed the rim profile may reach, degrees; 180 is a full tube with no rim at all.</summary>
    public static double RimMaxDeg = 180;
    /// <summary>Angular half-width of a token's catch window, degrees.</summary>
    public static double TokenHalfWidthDeg = 16;
    /// <summary>Angular half-width of a mine, degrees.
    ///
    /// <para>⚠ This is the whole hitbox, everywhere — not a row-spacing knob. Sized so a ring's mines can
    /// space out far enough that the mine art stops overlapping while the ring still blocks: the wall works
    /// only while the step stays under twice this (see <c>InternodePhrases.RingMines</c>). The cost, accepted
    /// deliberately, is that every mine in the game reads wider to a runner.</para></summary>
    public static double MineHalfWidthDeg = 15.2;
    /// <summary>A ground token is caught only at or below this height, pipe radii.</summary>
    public static double GroundGrabHeight = 0.03;
    /// <summary>Authoring floor for an elevated token's drawn height: it must sit more than this above
    /// <see cref="GroundGrabHeight"/> so the tether reads as a lift (InternodeProbe enforces it). ⚠ Not a catch
    /// band: a lifted orb is caught at any height above the ground-grab height on the right line.</summary>
    public static double ElevatedBandHalf = 0.10;
    /// <summary>A mine is cleared at or above this height, pipe radii.</summary>
    public static double MineClearHeight = 0.05;
    /// <summary>Half the height band a mine hung above the floor strikes in, pipe radii. Such a mine is
    /// cleared by passing under it on the ground or over it at the top of a jump; stacked on a ground mine it
    /// closes the "just hop the row" answer, so the pair has to be threaded or timed.</summary>
    public static double MineBandHalf = 0.14;
    /// <summary>How far ahead the course is visible, seconds. Must equal the renderer's far plane
    /// (<c>InternodeRenderer.ZFar</c>); the sim uses it to keep the gate approach straight and the lead of every
    /// section empty.</summary>
    public static double LookaheadSeconds = 6.0;
    /// <summary>Gravity across the open top, pipe radii per second squared, pulling toward the bottom of the pipe.
    /// Up there the runner is a free particle, not a bead on the ring, so a runner who barely clears the rim falls
    /// straight through the middle to the floor, while one with speed presses against the ring and slides over it
    /// to the far wall. The crossing threshold is emergent — about <c>sqrt(FlightGravity)</c> radii/s at the top.
    /// Matched to <see cref="JumpGravity"/> so leaving a wall mid-jump feels continuous.</summary>
    public static double FlightGravity = 7.5;
    /// <summary>Fraction of the steer torque available as a sideways push across the open top. ⚠ Keep it under
    /// <see cref="FlightGravity"/> or the stick can hold the runner in the air.</summary>
    public static double GapSteerFraction = 0.25;

    // ── Steering ──────────────────────────────────────────────────────────────
    /// <summary>Stick deflection below this doesn't steer.</summary>
    public static double Deadzone = 0.12;
    /// <summary>Angular acceleration a full stick applies, degrees per second squared.</summary>
    public static double SteerTorqueDegPerSec2 = 800;
    /// <summary>Pendulum pull toward the bottom at 90°, degrees per second squared. Must stay below the steer
    /// torque or a full stick cannot climb.</summary>
    public static double PendulumGravityDegPerSec2 = 257;
    /// <summary>Angular damping that always acts, per second. Keep it low: it is what lets a crossover carry.</summary>
    public static double AngularDampingPerSec = 0.8;
    /// <summary>Extra damping scaled by how far the stick is released, per second:
    /// <c>D = AngularDampingPerSec + ReleaseDampingPerSec · (1 − |u|)</c>. A held stick feels none of it; a
    /// released one stops coasting. The probe holds the released coast from 200°/s at or under 30% of the
    /// undamped coast; the pendulum pull shortens both, so the value sits well above the 4× time-constant
    /// ratio alone would suggest.</summary>
    public static double ReleaseDampingPerSec = 20.0;
    /// <summary>Extra grounded damping while the stick opposes the current swing, per second at full deflection: a
    /// reversal bites sooner without touching same-direction speed or a released coast, so the crossings' full-stick
    /// traces are unchanged. Zero at ω = 0 or with the stick along the swing.</summary>
    public static double CounterSteerDampingPerSec = 6.0;
    /// <summary>Angular speed cap, degrees per second.</summary>
    public static double MaxAngularSpeedDeg = 400;
    /// <summary>Fraction of the steer torque available during a jump. Across the open top the stick does nothing at
    /// all; see <see cref="InternodePhysics"/>.</summary>
    public static double AirSteerFraction = 0.6;
    /// <summary>How much of the airborne homing and its damping a full stick buys off at launch. The pull exists to
    /// stop passive drift, not to veto a committed input: at |u| = 0 nothing is relieved, so the hands-off jump
    /// settles exactly as tuned. ⚠ Relieving the homing alone is not enough — the damping alone caps a held swing
    /// at about 40°.
    ///
    /// <para>The relief fades as the runner descends (see <see cref="InternodePhysics"/>): by the end of the jump the
    /// remaining pull exactly offsets a full stick, so a commitment buys travel early in the arc and nothing at
    /// the end of it. That balance point is derived from the steer torque and the pull at the runner's own angle,
    /// not tuned, so it holds wherever on the pipe the jump happens.</para></summary>
    public static double AirControlRelief = 0.85;
    /// <summary>Extra pull toward the bottom while jumping, degrees per second squared at the top of the pipe,
    /// scaled by |θ|/π. ⚠ It grows with how far round the pipe the runner is, countering how the pendulum term
    /// weakens above 90° — without it a jump taken high on a wall would simply drift back to the same spot. It
    /// touches the angular channel only, so jump height is identical everywhere on the pipe. Not applied across
    /// the open top, where a crossing is pure momentum.</summary>
    public static double AirHomingDegPerSec2 = 3300;
    /// <summary>Angular damping while jumping, per second. ⚠ Sized against <see cref="AirHomingDegPerSec2"/>: the
    /// homing pull is a spring, and an undamped spring trades height for speed and flings the runner past the
    /// bottom to the far wall. Critical damping is <c>2·sqrt(k)</c> where k is the homing stiffness per radian
    /// (<c>AirHomingDegPerSec2·π/180/π</c>) plus the pendulum's own <c>PendulumGravityDegPerSec2·π/180</c>. Keep it
    /// at or a little above that, or the fling returns; the probe asserts a still jump never crosses the bottom.</summary>
    public static double AirDampingPerSec = 9.5;
    /// <summary>Fraction of the pendulum pull that acts in the air. Full: a jump is sticky shoes leaving the wall,
    /// not a suspension of gravity, so a leap from either wall lands near the bottom.</summary>
    public static double AirPendulumFraction = 1.0;
    /// <summary>Angular speed kept on landing on the far wall after a crossing.</summary>
    public static double OverTopLandRetain = 0.75;

    // ── Wall grip ─────────────────────────────────────────────────────────────
    /// <summary>A stick deflected at least this far (after the deadzone remap) holds the grip: the pendulum pull
    /// acts at full strength and <see cref="GripSeconds"/> is banked. Below it the stick counts as released.</summary>
    public static double GripHoldThreshold = 0.2;
    /// <summary>Seconds a released runner keeps the wall before gravity returns. Grounded and airborne alike; a
    /// jump does not reset it.</summary>
    public static double GripSeconds = 1.3;
    /// <summary>The last stretch of <see cref="GripSeconds"/> over which the pendulum pull eases back in
    /// (smoothstep), so a released runner slides rather than drops.</summary>
    public static double GripFadeSeconds = 0.7;

    // ── Jump ──────────────────────────────────────────────────────────────────
    /// <summary>Vertical launch speed, pipe radii per second. One jump — don't reintroduce the second press.
    /// ⚠ Impulse and gravity set <see cref="JumpApex"/> and <see cref="JumpSeconds"/> together, and the phrase bank is
    /// authored against both. Airtime is not free reach — the pendulum keeps acting on θ in flight, so a longer float
    /// drags the runner round the pipe and a held jump from -30° can clear the rim. Retune at a fixed apex A with
    /// I = 4A/T and G = 8A/T², and re-run the reachability sweep before believing any change.
    /// A tap is clipped to a hop by <see cref="JumpTapFraction"/>; a press in the air only buffers a landing jump.</summary>
    public static double JumpImpulse = 3.305;
    /// <summary>Vertical gravity, pipe radii per second squared.</summary>
    public static double JumpGravity = 7.5;
    /// <summary>A press this long before landing fires on landing.</summary>
    public static double JumpBufferSeconds = 0.10;
    /// <summary>Releasing the jump button while still rising clips the climb to this share of
    /// <see cref="JumpImpulse"/>, so a tap is a small hop and a hold is the full arc. The shortest tap still clears
    /// <see cref="MineClearHeight"/> — a hop that cannot clear a mine would be a trap.</summary>
    public static double JumpTapFraction = 0.32;
    /// <summary>The held jump's height, derived from the impulse and gravity so an override file cannot desync them.
    /// The phrase bank's arcs are authored up to it (<see cref="JumpMaxApex"/> is the same number).</summary>
    public static double JumpApex => JumpGravity > 0 ? JumpImpulse * JumpImpulse / (2 * JumpGravity) : 0;
    public static double JumpMaxApex => JumpApex;
    /// <summary>The held jump's airtime: what the longest gap and the longest plank void must fit under.</summary>
    public static double JumpSeconds => JumpGravity > 0 ? 2 * JumpImpulse / JumpGravity : 0;
    /// <summary>Coyote time: for this long after a grounded runner steps off a lip the jump still fires — deliberately
    /// short for this game, about eight frames. Past it a sink into a break is a fall, and a jump that comes
    /// down inside a break is a fall at once.</summary>
    public static double CoyoteSeconds = 0.07;

    // ── Run structure ─────────────────────────────────────────────────────────
    public static int    SectionsPerStage = 3;
    /// <summary>Target duration of a section's phrase run, before the return to centre and the gate gap.</summary>
    public static double SectionSeconds = 32;
    /// <summary><see cref="SectionSeconds"/> for level 1-1 alone: the opening stretch is short so a first-time
    /// player reaches a gate quickly.</summary>
    public static double FirstSectionSeconds = 18;
    /// <summary>Level 1-1's gate asks this fraction of the quota it would otherwise ask.</summary>
    public static double FirstSectionQuotaScale = 0.5;
    /// <summary>Empty pipe at the start of every section. Must be at least <see cref="LookaheadSeconds"/> so
    /// events scroll in from the horizon after the checkpoint banner.</summary>
    public static double SectionLeadSeconds = 6.0;
    /// <summary>Seconds held back from <see cref="SectionSeconds"/> for the return-to-centre phrases.</summary>
    public static double ReturnReserveSeconds = 3.0;
    /// <summary>Straight pipe between the last phrase and the gate.</summary>
    public static double CheckpointGapSeconds = 1.5;
    /// <summary>The checkpoint banner beat after a gate is passed.</summary>
    public static double CheckpointBeatSeconds = 1.2;
    public static int    MaxTier = 4;
    /// <summary>Stages per step of the tier cap.</summary>
    public static int    TierStagesPerStep = 1;
    /// <summary>How many tiers below the cap remain eligible.</summary>
    public static int    TierWindow = 2;
    /// <summary>The diabolical phrases (<c>InternodePhrase.Diabolical</c>) are absent below their own
    /// <c>MinStage</c> and then ramp: their pick weight climbs from nothing at <see cref="DiabolicalRampFromStage"/>
    /// to <see cref="DiabolicalBoost"/> times an ordinary tier-4 phrase's at <see cref="DiabolicalRampToStage"/>,
    /// so they trickle in and then take over the late run rather than arriving all at once.</summary>
    public static int    DiabolicalRampFromStage = 50;
    public static int    DiabolicalRampToStage = 100;
    public static double DiabolicalBoost = 2.5;
    /// <summary>Phrase time compression: the stage-1 scale, the drop per stage after it, and the floor. Authored
    /// spacing is never played at 1.0. The oracle validates every phrase at the start scale and at the floor;
    /// the sequencer clamps every stage between them.</summary>
    public static double PhraseTimeScaleStart = 0.5;
    public static double PhraseTimeScalePerStage = 0.03;
    public static double PhraseTimeScaleMin = 0.45;
    public static int    MaxEventsPerSection = 576;
    /// <summary>Smallest gap between two token planes after compression; the sim-side cue throttle.</summary>
    public static double MinTokenSpacingSeconds = 0.15;
    /// <summary>How long after a mine a token may appear in that mine's shadow, after compression.
    ///
    /// <para>A mine occludes roughly its own width of arc, so a token at a similar angle a short way behind it
    /// is invisible until the mine sweeps past — and by then it is too late to take. Draw distance is not the
    /// problem (<see cref="ViewHorizonSeconds"/> shows the whole approach); the mine in front of it is.</para>
    ///
    /// <para>⚠ Judged by projection, not by an angular band: the probe draws both sprites through the
    /// renderer's own perspective at the fastest ride and asks whether the orb's centre falls inside the
    /// mine's disc at any moment in the token's last <see cref="MinMineShadowLeadSeconds"/> of approach. A
    /// token to the side of a mine is never hidden however close in depth; one behind it is hidden until the
    /// mine passes, so it needs this much travel between them. Enforced over the authored bank and over built
    /// sections (a phrase seam, a gap mine) by <c>InternodeProbe</c>; there is no runtime check, because a
    /// phrase is authored once and played forever.</para></summary>
    public static double MinMineShadowLeadSeconds = 0.5;

    // ── Formation balance ──────────────────────────────────────────────────────
    /// <summary>Weight multiplier on a phrase that carries mines, against one that does not.
    ///
    /// <para>⚠ Well under a half even though the intent is about half as many mine encounters: 40 of the bank's
    /// 60 phrases carry mines, so an unweighted draw meets one 65-74% of the time. Halving the encounter rate
    /// therefore means weighting well below 0.5 — measure with the balance harness rather than reasoning from
    /// this number, because the answer depends on the pool each entry class offers, not on the multiplier
    /// alone.</para>
    ///
    /// <para>Mines are still the primary obstacle and must not vanish: the flavour rotation below gives them
    /// their own stages, where they come back concentrated. Fewer overall, more pointed when they arrive.</para></summary>
    public static double MinePhraseWeight = 0.34;

    /// <summary>Weight multiplier on the family a stage is flavoured toward.
    ///
    /// <para>⚠ This exists because the tier window saturates: from stage 4 the cap sits at
    /// <see cref="MaxTier"/> and the floor at cap − <see cref="TierWindow"/>, so every stage from 4 upward draws
    /// from an identical 48-phrase pool — no new phrases, ever. The bank cannot be narrowed instead: restricted
    /// to tiers 3+ the high-wall classes lose their route back to Centre, and to tier 4 the low classes have no
    /// entering phrase at all, so a stage there would be nothing but breathers.</para>
    ///
    /// <para>So distinctness comes from what a stage is made of rather than which phrases it may use. Each
    /// stage leans on one family — flow, crossings, choices, track, mines — and consecutive stages lean
    /// differently, which is what makes one level feel unlike the last from the same vocabulary.</para>
    ///
    /// <para>At 3.2, a mine-flavoured stage lands at 41–49% mine phrases against 13–21% on the rest: a mine
    /// level is a theme a level can have, not the game's texture.</para></summary>
    public static double StageFlavourBoost = 3.2;

    /// <summary>How many families the flavour rotates through; the stage picks one by <c>(stage − 1) % this</c>.
    /// Five: flow, crossing, choice, track and mines — so a level can be about the floor itself.
    /// ⚠ Coprime-ish with <see cref="SectionsPerStage"/> is not required — the flavour is per stage, so every
    /// section of one stage shares it, which is the point: a level has a character.</summary>
    public static int    StageFlavours = 5;

    /// <summary>Extra pull toward the top of the tier window per stage, on top of the base
    /// <c>1 + (tier − floor)</c>. With the window saturated this is the one lever that still sharpens the mix
    /// with the stage, and it does so without excluding the easier phrases a class may need to route.</summary>
    public static double TierBiasPerStage = 0.16;
    public static double TierBiasMax = 3.0;

    // ── Score ─────────────────────────────────────────────────────────────────
    /// <summary>The gate's demand as a share of a perfect run of the section: every collectable orb plus the
    /// full payout of every prize on offer (<see cref="BonusTokenValue"/> − 1 more per gold, the orb itself
    /// being counted already). Rises linearly from <see cref="QuotaShareStart"/> at stage 1 to
    /// <see cref="QuotaShareEnd"/> at <see cref="QuotaShareEndStage"/> and holds there. Orbs nobody can collect —
    /// the branch of a choice not taken — are never in the count. ⚠ Near the end one missed prize fails the
    /// gate; that is the design. Keep <see cref="QuotaShareEnd"/> under 1 or a single missed plain orb does.</summary>
    public static double QuotaShareStart = 0.60;
    public static double QuotaShareEnd = 0.98;
    public static int    QuotaShareEndStage = 100;
    public static int    CheckpointBonusTokens = 5;
    public static int    CheckpointBonusPerStage = 1;
    /// <summary>A mine costs this share of the held orbs, never fewer than <see cref="MineLossMinTokens"/> and never
    /// more than <see cref="MineLossCapTokens"/>, clamped at zero. A mine taken with nothing held, and a fall through a
    /// gap whatever is held, demote: back one stage with the hand empty.</summary>
    public static double MineLossShare = 0.5;
    public static int    MineLossMinTokens = 10;
    public static int    MineLossCapTokens = 40;
    /// <summary>The last orb of a phrase with at least <see cref="BonusMinOrbs"/> orbs turns gold when every orb
    /// before it was caught, and pays <see cref="BonusTokenValue"/>.</summary>
    public static int    BonusMinOrbs = 4;
    public static int    BonusTokenValue = 10;
    /// <summary>The flight of banked orbs into the score readout after a gate: per-orb travel time, the stagger
    /// between orbs, and the most orbs drawn. Fits inside the empty section lead.</summary>
    public static double BankSeconds = 0.55;
    public static double BankStaggerSeconds = 0.03;
    public static int    MaxBankOrbs = 24;
    /// <summary>Angular speed added away from a mine on impact, degrees per second.</summary>
    public static double MineKnockbackDegPerSec = 140;

    // ── Kinetic layer ─────────────────────────────────────────────────────────
    /// <summary>Disc twist on a mine hit (signed by the knockback) and on game over. The disc twists, never slides.</summary>
    public static double TwistMineDeg = 6;
    public static double TwistGameOverDeg = 8;
    public static double TwistDecayPerSec = 6;
    public static double PunchMine = 1.05;
    public static double PunchCheckpoint = 1.04;
    public static double PunchLand = 1.015;
    public static double PunchDecayPerSec = 0.6;
    public static double MineHitStopSeconds = 0.06;
    /// <summary>After a mine hit, mines pass through the runner for this long: one slip is one loss, never a chain
    /// through a dense pattern. The runner flickers for the duration.</summary>
    public static double InvulnerableSeconds = 2.0;
    /// <summary>Sim time scale during a hit-stop. Not zero: a crawl reads as impact, a freeze as a hitch.</summary>
    public static double HitStopScale = 0.15;
    public static double CollectPulseDecayPerSec = 4;
    /// <summary>The short-hand swell: reaching a gate under quota pulses the token count this many times, at
    /// this many seconds each.
    ///
    /// <para>⚠ A repeat, not a longer decay. Coming up short says nothing in the middle of the screen, so the
    /// number the player has to beat is the whole of the message — and one swell reads as an acknowledgement
    /// where a repeat reads as "look at this". Deliberately not a disc kick: the run simply holds at this
    /// section, and jolting the screen for it reads as punishment for something nobody did wrong.</para></summary>
    public static int    QuotaPulseCount = 4;
    public static double QuotaPulseSeconds = 0.30;
    /// <summary>Life of the ring-and-number pop a catch leaves at the runner.</summary>
    public static double CollectPopSeconds = 0.35;
    /// <summary>Life of the flare a pattern's last orb throws when it turns gold. Short and small on purpose: the orb
    /// is often mid-board, and the reward has to read without covering what is coming.</summary>
    public static double GoldFlashSeconds = 0.42;
    /// <summary>Life of the puff the second press of a double jump leaves in the air.</summary>
    public static double ShoutSeconds = 1.1;
    /// <summary>How long the checkpoint / level up row stays in the HUD plaque, slide-in and slide-out included.</summary>
    public static double GateNoticeSeconds = 2.2;
    /// <summary>How long the plaque's bottom row holds demoted or promoted, slide in and out included. The
    /// text pulses on the quota pulse's own beat for as long as it is up.</summary>
    public static double StageNoticeSeconds = 2.2;
    /// <summary>How long the bank's bonus toasts (first try, perfect) stay up.</summary>
    public static double BonusToastSeconds = 2.6;
    /// <summary>A gate passed on the first try multiplies the bank by Base, and each further consecutive first try
    /// adds Step: 2×, 2.5×, 3×… Missing a gate resets the streak. Fights the incentive to miss on purpose and farm
    /// the re-roll for a near-double hand.</summary>
    public static double FirstTryMultiplierBase = 2.0;
    public static double FirstTryMultiplierStep = 0.5;
    /// <summary>A section with every orb caught adds Base to the hand before the multiplier, and each further
    /// consecutive perfect section adds Step more: 50, 75, 100…</summary>
    public static int PerfectBonusBase = 50;
    public static int PerfectBonusStep = 25;
    /// <summary>Life of a lost orb: sprayed out, then left behind on the track and carried off screen as the runner
    /// passes. Long enough that the renderer's off-screen test, not this, ends the visible ones.</summary>
    public static double BurstSeconds = 1.6;
    /// <summary>Most scattered orbs drawn from one loss; at the loss cap every lost orb is shown.</summary>
    public static int    MaxBurstOrbs = 40;

    // ── View ─────────────────────────────────────────────────────────────────
    /// <summary>How compressed the depth axis is drawn. z is seconds of travel and the sim decides when an object
    /// arrives, so this is the whole of perceived speed: it scales the perspective constant, so the floor rushes
    /// and objects grow this much faster. Raising it alone does not change what arrives when — that is
    /// <see cref="PhraseTimeScaleStart"/>.</summary>
    public static double ViewSpeed = 9.0;
    /// <summary>Each stage adds this to <see cref="ViewSpeed"/>, up to <see cref="ViewSpeedMax"/>: the ride inches
    /// faster as the run goes on. Perceived only — arrival times are the sim's.</summary>
    public static double ViewSpeedPerStage = 0.6;
    public static double ViewSpeedMax = 18.0;
    /// <summary>Draw distance, seconds of travel ahead of the runner. Independent of <see cref="ViewSpeed"/>, so a
    /// faster board can still see a long way down the pipe; the far rings cost nothing (under 1.5 px they are
    /// skipped). Keep <see cref="LookaheadSeconds"/> in step: it is the sim's matching promise that the gate
    /// approach is straight and the section lead empty.</summary>
    public static double ViewHorizonSeconds = 6.0;
    /// <summary>Seconds of travel per checker band. Shorter bands read as faster, and strobe if they get much
    /// under a tenth of a second.</summary>
    public static double ViewBandSeconds = 0.08;
    /// <summary>How hard a turn swings the vanishing point, in units of the displaced ring's own radius.</summary>
    public static double ViewTurnGain = 3.5;

    // ── Course ────────────────────────────────────────────────────────────────
    /// <summary>Turns per section: axis-aligned bends (one of up, down, left, right), never overlapping, full
    /// amplitude from stage 1. There is no stage ramp: a turn has no physics effect, and the point of one is
    /// that the pipe ahead is hidden.</summary>
    public static int    TurnsPerSectionMin = 1;
    public static int    TurnsPerSectionMax = 2;
    public static double TurnMinSeconds = 4;
    public static double TurnMaxSeconds = 8;
    /// <summary>Full swing of a turn on its axis, in the renderer's [−1, 1] offset space. A drawn turn's
    /// magnitude is uniform in [<see cref="TurnMinAmplitudeShare"/>, 1] × this.</summary>
    public static double TurnMaxAmplitude = 1.0;
    public static double TurnMinAmplitudeShare = 0.5;
    /// <summary>The renderer draws vertical turns at this fraction of the lateral swing; a rising pipe hides
    /// more course than a turning one.</summary>
    public static double BendVerticalScale = 0.6;
    /// <summary>First stage with corkscrew twists.</summary>
    public static int    TwistFirstStage = 2;
    /// <summary>Chance per section (from <see cref="TwistFirstStage"/>) of drawing a twist group.</summary>
    public static double TwistChance = 0.3;
    /// <summary>Twists per section. They come as a group that nets to zero so the gate is level; below 2 disables them.</summary>
    public static int    TwistsPerSectionMax = 2;
    public static double TwistMaxDeg = 90;
    public static double TwistMinSeconds = 2.5;
    public static double TwistMaxSeconds = 5;
    /// <summary>Closures of the pipe: the true half-pipe is the common case, and the rest of the time the course runs
    /// through one of three more-closed tunnel designs (high walls, a canyon, a full tube), each a plateau at its
    /// target held between two ramps. Every section from <see cref="RimClosureFirstStage"/> is split into
    /// <see cref="RimClosuresPerSectionMax"/> slots; each slot draws a closure with probability
    /// <see cref="RimClosureChance"/>, sized between <see cref="RimMinSeconds"/> and <see cref="RimMaxSeconds"/> and
    /// capped by the slot. A closure sits only over phrases marked rim-safe (plus <see cref="RimSafeMarginSeconds"/>
    /// either side), never over a crossing that needs the open top; one that finds no home is dropped, not forced.
    /// The probe measures the share of time closed over the eligible stages and holds it inside
    /// [<see cref="RimClosedShareMin"/>, <see cref="RimClosedShareMax"/>] — the half-pipe is 50–70% of the ride.</summary>
    public static int    RimClosureFirstStage = 2;
    public static int    RimClosuresPerSectionMax = 2;
    public static double RimClosureChance = 0.8;
    public static double RimMinSeconds = 4;
    public static double RimMaxSeconds = 14;
    /// <summary>Smoothstep ramp at each end of a closure, seconds; the plateau is what remains.</summary>
    public static double RimRampSeconds = 1.2;
    public static double RimDesignWallsDeg = 125;
    public static double RimDesignCanyonDeg = 150;
    public static double RimDesignTubeDeg = 180;
    public static double RimClosedShareMin = 0.30;
    public static double RimClosedShareMax = 0.50;
    public static double RimSafeMarginSeconds = 0.5;

    // ── Gaps in the floor ─────────────────────────────────────────────────────
    /// <summary>Breaks in the floor: at most this many per section, reserved in the phrase chain at returns to Centre.</summary>
    public static int    GapsPerSectionMax = 3;
    /// <summary>The gap budget grows with the stage: one more gap every <see cref="GapsPerSectionStageStep"/> stages,
    /// up to <see cref="GapsPerSectionCap"/>.</summary>
    public static int    GapsPerSectionStageStep = 2;
    public static int    GapsPerSectionCap = 6;
    /// <summary>Chance of a gap at each return to Centre in the chain, at stage 1, plus this much per stage after, capped.</summary>
    public static double GapChance = 0.55;
    public static double GapChancePerStage = 0.045;
    public static double GapChanceMax = 0.82;
    /// <summary>From <see cref="GapMineFirstStage"/>, a gap may come armed: a full gap gets a ring of mines
    /// <see cref="GapMineLeadSeconds"/> before its near lip, so the jump has to start before the mine and carry the
    /// hole too; a partial gap gets mines on the floor beside the hole, so running round it means jumping a mine
    /// with a drop at your elbow. The chance rises per stage to its cap. ⚠ Lead + <see cref="GapMaxSeconds"/> must stay
    /// under the double jump's longest airtime, or an armed gap becomes a wall; the probe holds it.</summary>
    public static int    GapMineFirstStage = 4;
    public static double GapMineChance = 0.35;
    public static double GapMineChancePerStage = 0.05;
    public static double GapMineChanceMax = 0.8;
    public static double GapMineLeadSeconds = 0.17;   // lead + GapMaxSeconds + 0.1 must fit under JumpSeconds (0.88 s)
    /// <summary>Seconds of travel. A short one is tappable; the longest still fits under a held jump.</summary>
    public static double GapMinSeconds = 0.25;
    public static double GapMaxSeconds = 0.6;
    /// <summary>Clear space demanded either side of a gap, seconds of travel, so the jump over it can never cost a
    /// collection: no orb may sit within this of the span. ⚠ It must cover the airtime of the hop the player will
    /// use, or a gap forces a miss and the fairness promise breaks.</summary>
    public static double GapTokenClearSeconds = 0.7;
    /// <summary>The drop through a gap before the demotion, and how long the forward motion takes to coast to a stop
    /// inside it (linear, so it reads as a skid rather than a wall).</summary>
    public static double FallSeconds = 1.8;
    public static double FallCoastSeconds = 1.6;
    /// <summary>How far below the floor the falling bike ends up, in pipe radii at its depth. The drop grows with
    /// the square of time, so gravity visibly wins over the forward carry as the fall goes on.</summary>
    public static double FallDepthRadii = 3.2;

    // ── Planks: a strip of floor with a gap down either side ─────────────────
    // A plank is two gaps sharing one z-span, leaving a narrow ribbon of floor between them. The physics
    // needed nothing for it — InternodePhysics.Integrate already drops the runner through any gap covering
    // its angle (Internode.InGap supplies floorHere) — and the renderer paints every hole on a ring rather
    // than the first, which is what made it drawable.
    //
    // ⚠ The reachability oracle proves phrases, not gaps, so a plank's fairness is structural like an
    // ordinary gap's: the ribbon is wide enough to hold, the void at the end of one is short enough for one
    // jump press, and the sideways shift to the next is inside what air steering can cover. Loosen any of
    // those three and the guarantee is gone with nothing to catch it.

    /// <summary>Half-width of the ribbon, degrees. ⚠ Wide enough to ride, not just to land on: the runner is
    /// steering at up to <see cref="MaxAngularSpeedDeg"/> and the pendulum is pulling it toward the bottom
    /// the whole way, so a ribbon near the tolerance of a good stick is a coin flip, not a skill test.</summary>
    public static double PlankHalfWidthDeg = 26.0;

    /// <summary>Where a ribbon may be centred, degrees either side of the bottom. Kept modest: the airborne
    /// homing grows with |θ|, so a ribbon high on a wall is far harder to arrive at than to leave.</summary>
    public static double PlankCentreDegMax = 34.0;

    /// <summary>Seconds of travel one ribbon runs for.</summary>
    public static double PlankMinSeconds = 0.9;
    public static double PlankMaxSeconds = 1.6;

    /// <summary>Stage a plank may first appear at, and the stage the staggered form unlocks — where a ribbon
    /// ends outright and the next one starts on a different axis, so the hand-over is a jump rather than a
    /// steer.</summary>
    public static int    PlankFirstStage = 2;
    public static int    PlankStaggerFirstStage = 5;

    /// <summary>The void between two staggered ribbons, seconds of travel. ⚠ Bounded by
    /// <see cref="GapMaxSeconds"/>'s own promise — one press clears it — because that is the only reason the
    /// hand-over is guaranteed jumpable at all.</summary>
    public static double PlankVoidMinSeconds = 0.25;
    public static double PlankVoidMaxSeconds = 0.5;

    /// <summary>How many breaks in the floor the renderer will paint on one ring, and it must cover a whole
    /// section's worth.
    ///
    /// <para>⚠ Not "a few". Far rings merge — a band is only painted once it is 1.5 px deep — so one far band
    /// can span several seconds of course and take in every hole in it.</para>
    ///
    /// <para>⚠ It lives here, in Core, rather than beside the renderer that uses it, because it is a property
    /// of the course generator and not of the drawing. InternodeProbe measures the worst section it can build
    /// and asserts it against this, so the number is checked rather than reasoned about.</para>
    ///
    /// <para>Overflow is handled conservatively rather than by dropping a hole: the renderer skips the whole
    /// band, showing sky. A missing annulus at the far plane is a glitch; floor painted over a hole the
    /// physics still drops the runner through is a lie, and the kind nobody sees until they fall.</para></summary>
    public static int    MaxHolesPerRing = 160;   // a fork (43 gaps) and a serpent (36) in one section reached 103

    /// <summary>How far the axis may shift across a hand-over, degrees. ⚠ Air steering runs at
    /// <see cref="AirSteerFraction"/> of <see cref="MaxAngularSpeedDeg"/> — 240°/s — so even the shortest
    /// void affords far more than this on paper; the margin is for the homing pulling the other way and for
    /// a player who jumps late. It also has a floor, or a "different axis" is not a different axis.</summary>
    public static double PlankShiftDegMin = 26.0;
    public static double PlankShiftDegMax = 55.0;

    /// <summary>How many ribbons a staggered run chains, and how many planks a section may hold.</summary>
    public static int    PlankRibbonsMax = 3;
    public static int    PlanksPerSectionMax = 2;
    public static double PlankChance = 0.5;
    public static double PlankChancePerStage = 0.05;
    public static double PlankChanceMax = 0.8;
}
