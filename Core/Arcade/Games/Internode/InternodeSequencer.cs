namespace ControllerWheel;

/// <summary>Builds one section from (stage, section, seed): a pure function, so the snapshot carries only the
/// seed and the section (events, bends, twists) is rebuilt identically on restore.
///
/// <para>Every section starts and ends at Centre on a straight, level pipe at the base rim: the lead is empty,
/// the phrase run returns to Centre, and turns, twists and closures stop <see cref="InternodeTuning.LookaheadSeconds"/>
/// before the gate.</para></summary>
public static class InternodeSequencer
{
    /// <summary>Tier cap for a stage.</summary>
    public static int TierCap(int stage) =>
        Math.Clamp(1 + (Math.Max(1, stage) - 1) / Math.Max(1, InternodeTuning.TierStagesPerStep), 1, Math.Max(1, InternodeTuning.MaxTier));

    public static int TierFloor(int stage) => Math.Max(1, TierCap(stage) - Math.Max(0, InternodeTuning.TierWindow));

    /// <summary>Phrase time compression for a stage: <see cref="InternodeTuning.PhraseTimeScaleStart"/> at stage 1,
    /// down by <see cref="InternodeTuning.PhraseTimeScalePerStage"/> per stage, never under the floor. Both ends
    /// are what the oracle validates, so every stage plays inside the proven range.</summary>
    public static double TimeScale(int stage)
    {
        double floor = Math.Clamp(InternodeTuning.PhraseTimeScaleMin, 0.1, 1);
        double start = Math.Clamp(InternodeTuning.PhraseTimeScaleStart, floor, 1);
        return Math.Max(floor, start - InternodeTuning.PhraseTimeScalePerStage * (Math.Max(1, stage) - 1));
    }

    /// <summary>Holes kept back from <see cref="InternodeTuning.MaxHolesPerRing"/> for what the sequencer
    /// reserves between phrases: at most <see cref="InternodeTuning.GapsPerSectionCap"/> gaps plus the planks'
    /// flanks (two per ribbon, <see cref="InternodeTuning.PlankRibbonsMax"/> ribbons per plank, one void each).</summary>
    private const int HoleReserve = 28;

    public static InternodeSection Build(int stage, int section, ulong seed)
    {
        stage = Math.Max(1, stage);
        ulong rng = seed ^ ((ulong)stage * 0x9E3779B97F4A7C15UL) ^ ((ulong)Math.Max(1, section) * 0xC2B2AE3D27D4EB4FUL);
        if (rng == 0) rng = 0x466C756D65UL;

        int cap = TierCap(stage), floor = TierFloor(stage);
        Family flavour = FlavourOf(stage);
        // ⚠ The opening section carries no holes at all, phrase-authored ones included. The same rule the
        // reserved gaps and planks obey below, and for the same reason: the first stretch is where a player
        // learns what the stick does, and a hole there is a fall they had no way to read. Their first hole
        // arrives in section 2, and the tier-1 track figures are built to be the ones that teach it.
        bool holesAllowed = !(stage == 1 && Math.Max(1, section) == 1);
        double scale = TimeScale(stage);
        double lead = Math.Max(InternodeTuning.LookaheadSeconds, InternodeTuning.SectionLeadSeconds);
        double sectionSeconds = holesAllowed ? InternodeTuning.SectionSeconds : InternodeTuning.FirstSectionSeconds;
        double runEnd = lead + Math.Max(0, sectionSeconds - InternodeTuning.ReturnReserveSeconds - lead);
        int maxEvents = Math.Max(1, InternodeTuning.MaxEventsPerSection);

        var events = new List<InternodePlacedEvent>();
        var ids = new List<string>();
        var starts = new List<double>();
        var pos = InternodePos.Centre;
        string? last = null;
        double z = lead;

        // The phrase run: weighted toward the cap, never the same phrase twice running, the class breather
        // when nothing else fits.
        var candidates = new List<InternodePhrase>();
        var gaps = new List<InternodeGap>();
        int planks = 0;
        double closedUntil = double.NegativeInfinity;   // end of the last forced tube, ramp included
        double crossingUntil = double.NegativeInfinity; // end of the last phrase that needs the open top
        double rimMargin = Math.Max(0, InternodeTuning.RimSafeMarginSeconds);
        while (z < runEnd && events.Count < maxEvents)
        {
            candidates.Clear();
            // A phrase that needs the full tube is offered only where its forced closure fits: from the first
            // stage closures exist at all, and ending (ramp included) before the gate approach, which the rim
            // profile holds at the base.
            double closedEnd = runEnd + InternodeTuning.CheckpointGapSeconds - InternodeTuning.LookaheadSeconds
                               - Math.Max(0, InternodeTuning.RimRampSeconds);
            // …and never so soon after the last closed phrase that the two tubes' ramps would touch: merged,
            // their closure could run past RimMaxSeconds.
            double ramp = Math.Max(0.01, InternodeTuning.RimRampSeconds);
            // A closed phrase's tube must also keep the rim-safe margin from any crossing either side of it, so a
            // crossing is refused in the tube's wake and a tube is refused in a crossing's.
            bool closedAllowed = stage >= Math.Max(1, InternodeTuning.RimClosureFirstStage) && InternodeTuning.RimClosuresPerSectionMax > 0
                                 && z - ramp >= closedUntil && z - ramp - rimMargin >= crossingUntil;
            // ⚠ The renderer paints at most MaxHolesPerRing holes on a ring, and a far ring can take in every hole
            // in the section; a phrase whose own holes would push the section past that bound, less a reserve
            // for the gaps and planks reserved between phrases, is simply not offered. The bending planks carry
            // dozens of holes each, and three in one late section is otherwise routine.
            int holeRoom = Math.Max(0, InternodeTuning.MaxHolesPerRing) - HoleReserve - gaps.Count;
            foreach (var p in InternodePhrases.All)
                if (p.Entry == pos && p.Tier >= floor && p.Tier <= cap && p.Id != last
                    && stage >= p.MinStage
                    && (holesAllowed || p.Holes.Length == 0)
                    && p.Holes.Length <= holeRoom
                    && (p.RimSafe || z >= closedUntil + rimMargin)
                    && (!p.Closed || (closedAllowed && z + p.Length * scale <= closedEnd))) candidates.Add(p);
            var pick = candidates.Count == 0 ? InternodePhrases.Breather(pos) : Weighted(candidates, floor, stage, flavour, ref rng);
            if (pick.Closed) closedUntil = z + pick.Length * scale + ramp;
            if (!pick.RimSafe) crossingUntil = z + pick.Length * (pick.TimeLocked ? 1 : scale);
            Place(pick, ref z, scale, events, ids, starts, maxEvents, gaps);
            last = pick.Id;
            pos = pick.Exit;
            // A break in the floor is reserved here, not searched for afterwards: a dense section has no natural
            // stretch long enough. Only from Centre — the empty pipe either side lets the runner settle at the
            // bottom, and the clearance is what guarantees the jump over it never costs an orb.
            // Never before the first gate: the opening stretch teaches the pipe, and a hole in it is a fall the
            // player could not have read yet.
            // ⚠ One reservation per phrase boundary. A gap and a plank both firing here would leave a stretch
            // that neither alone accounts for, and the run-structure invariant is exactly that every skipped
            // stretch is one reservation's clearance + span + clearance.
            bool reserved = false;
            bool gapsAllowed = !(stage == 1 && Math.Max(1, section) == 1);
            int gapBudget = Math.Min(Math.Max(0, InternodeTuning.GapsPerSectionCap),
                Math.Max(0, InternodeTuning.GapsPerSectionMax) + (stage - 1) / Math.Max(1, InternodeTuning.GapsPerSectionStageStep));
            double gapChance = Math.Min(Math.Clamp(InternodeTuning.GapChanceMax, 0, 1),
                Math.Clamp(InternodeTuning.GapChance, 0, 1) + Math.Max(0, InternodeTuning.GapChancePerStage) * (stage - 1));
            if (gapsAllowed && pos == InternodePos.Centre && gaps.Count < gapBudget && NextDouble(ref rng) < gapChance)
            {
                double minS = Math.Max(0.1, InternodeTuning.GapMinSeconds), maxS = Math.Max(minS, InternodeTuning.GapMaxSeconds);
                double clear = Math.Max(0, InternodeTuning.GapTokenClearSeconds);
                double seconds = minS + NextDouble(ref rng) * (maxS - minS);
                // The gate approach (Lookahead before the gate) must stay clear too. Length is not known yet, but it
                // is at least runEnd + CheckpointGapSeconds, so this bound is conservative.
                if (z + clear + seconds + clear <= runEnd + InternodeTuning.CheckpointGapSeconds - InternodeTuning.LookaheadSeconds)
                {
                    // Which part of the floor goes: the whole width, one half, a slot in the middle, or one wall.
                    // A partial gap can be run round as well as jumped, so it reads as a routing choice.
                    var (lo, hi) = (int)(NextDouble(ref rng) * 6) switch
                    {
                        0 => (-60.0, 60.0),      // a slot down the middle: hug a wall or hop
                        1 => (-180.0, 0.0),      // the left half
                        2 => (0.0, 180.0),       // the right half
                        3 => (-180.0, -40.0),    // the left wall
                        4 => (40.0, 180.0),      // the right wall
                        _ => (-180.0, 180.0),    // the whole floor: jump
                    };
                    gaps.Add(new InternodeGap(z + clear, seconds, lo, hi));
                    ArmGap(stage, z + clear, seconds, lo, hi, events, maxEvents, ref rng);
                    z += clear + seconds + clear;
                    reserved = true;
                }
            }
            // A plank is drawn on its own budget, after the ordinary gap so the RNG stream is unchanged for
            // every stage that has no planks yet. Same two conditions as a gap — from Centre, never in the
            // opening section — because a ribbon asks more of the player than a hole does, not less.
            bool planksAllowed = gapsAllowed && stage >= Math.Max(1, InternodeTuning.PlankFirstStage);
            double plankChance = Math.Min(Math.Clamp(InternodeTuning.PlankChanceMax, 0, 1),
                Math.Clamp(InternodeTuning.PlankChance, 0, 1)
                    + Math.Max(0, InternodeTuning.PlankChancePerStage) * (stage - 1));
            if (!reserved && planksAllowed && pos == InternodePos.Centre
                && planks < Math.Max(0, InternodeTuning.PlanksPerSectionMax)
                && NextDouble(ref rng) < plankChance)
            {
                double cap2 = runEnd + InternodeTuning.CheckpointGapSeconds - InternodeTuning.LookaheadSeconds;
                double used = PlacePlank(stage, z, cap2, gaps, events, maxEvents, ref rng);
                if (used > 0) { z += used; planks++; }
            }
        }

        // Return to Centre: only phrases that lower the class rank, within the cap (the floor is ignored so a
        // tier-1 return is always available).
        int guard = 0;
        while (pos != InternodePos.Centre && guard++ < 4 && events.Count < maxEvents)
        {
            candidates.Clear();
            int best = int.MaxValue;
            foreach (var p in InternodePhrases.All)
            {
                if (p.Entry != pos || p.Tier > cap || InternodePhrases.Rank(p.Exit) >= InternodePhrases.Rank(pos)) continue;
                if (!holesAllowed && p.Holes.Length > 0) continue;
                if (!p.RimSafe && z < closedUntil + rimMargin) continue;   // no crossing in a forced tube's wake
                if (p.Closed) continue;                                     // the return home never needs the tube
                if (p.Holes.Length > Math.Max(0, InternodeTuning.MaxHolesPerRing) - HoleReserve - gaps.Count) continue;
                int rank = InternodePhrases.Rank(p.Exit);
                if (rank < best) { best = rank; candidates.Clear(); }
                if (rank == best) candidates.Add(p);
            }
            if (candidates.Count == 0) break;
            var pick = candidates[(int)(NextDouble(ref rng) * candidates.Count) % candidates.Count];
            Place(pick, ref z, scale, events, ids, starts, maxEvents, gaps);
            pos = pick.Exit;
        }

        double length = SectionLength(z, ids, starts, scale);
        events.Sort((a, b) => a.Z.CompareTo(b.Z));
        // ⚠ Sorted, because a phrase's own holes are appended as it is placed while the sequencer's
        // reservations go in at the boundary between phrases, so the two interleave out of order. The renderer
        // walks this list per ring and CheckPlanks groups it by span; both want it in course order.
        gaps.Sort((a, b) => a.Z0.CompareTo(b.Z0));

        var bends = DrawTurns(length, ref rng);
        var twists = DrawTwists(stage, lead, length, ref rng);
        var rims = DrawRims(stage, length, ids, starts, scale, ref rng);
        return new InternodeSection(length, scale, [.. events], bends, twists, rims, [.. gaps], [.. ids], [.. starts]);
    }

    /// <summary>Dev only (<see cref="ArcadeDebug.PhraseTester"/>): a section that is one authored phrase
    /// <paramref name="repeats"/> times over and nothing else — no reserved gaps, planks or mines between — at
    /// the time scale of the stage the phrase is first offered, so it plays as the oracle proved it. Where the
    /// phrase's exit does not meet its own entry, the shortest chain of plain connector phrases (tier 1 or 2,
    /// no holes, open rim, offered from stage 1) walks the position round; a closed phrase gets a breather
    /// between repeats so its forced tubes do not merge. The course still turns, twists and closes as a section
    /// of that stage would, and it still returns to Centre for the gate.</summary>
    public static InternodeSection BuildTester(InternodePhrase p, ulong seed, int repeats = 3)
    {
        int stage = Math.Max(1, p.MinStage);
        ulong rng = seed ^ 0xA5A5F00D5EEDUL;
        if (rng == 0) rng = 0x466C756D65UL;
        double scale = TimeScale(stage);
        double lead = Math.Max(InternodeTuning.LookaheadSeconds, InternodeTuning.SectionLeadSeconds);
        int maxEvents = Math.Max(1, InternodeTuning.MaxEventsPerSection);
        var events = new List<InternodePlacedEvent>();
        var ids = new List<string>();
        var starts = new List<double>();
        var gaps = new List<InternodeGap>();
        var pos = InternodePos.Centre;
        double z = lead;
        for (int r = 0; r < Math.Max(1, repeats); r++)
        {
            if (r > 0 && p.Closed) { var b = InternodePhrases.Breather(InternodePos.Centre); Place(b, ref z, scale, events, ids, starts, maxEvents, gaps); pos = b.Exit; }
            foreach (var c in Connect(pos, p.Entry)) { Place(c, ref z, scale, events, ids, starts, maxEvents, gaps); pos = c.Exit; }
            Place(p, ref z, scale, events, ids, starts, maxEvents, gaps);
            pos = p.Exit;
        }
        foreach (var c in Connect(pos, InternodePos.Centre)) { Place(c, ref z, scale, events, ids, starts, maxEvents, gaps); pos = c.Exit; }

        double length = SectionLength(z, ids, starts, scale);
        events.Sort((a, b) => a.Z.CompareTo(b.Z));
        gaps.Sort((a, b) => a.Z0.CompareTo(b.Z0));
        var bends = DrawTurns(length, ref rng);
        var twists = DrawTwists(stage, lead, length, ref rng);
        var rims = DrawRims(stage, length, ids, starts, scale, ref rng);
        return new InternodeSection(length, scale, [.. events], bends, twists, rims, [.. gaps], [.. ids], [.. starts]);
    }

    /// <summary>Keep every authored full tube, including its opening ramp, before the gate approach where
    /// RimOf forces the pipe open. Shared by normal courses and the phrase tester: placement restrictions
    /// alone cannot protect a final closed phrase in every section builder.</summary>
    private static double SectionLength(double end, List<string> ids, List<double> starts, double scale)
    {
        double length = end + Math.Max(0, InternodeTuning.CheckpointGapSeconds);
        double ramp = Math.Max(0.01, InternodeTuning.RimRampSeconds);
        for (int i = 0; i < ids.Count; i++)
        {
            var p = InternodePhrases.Find(ids[i]);
            if (p?.Closed != true) continue;
            double phraseEnd = starts[i] + p.Length * (p.TimeLocked ? 1 : scale);
            length = Math.Max(length, phraseEnd + ramp + InternodeTuning.LookaheadSeconds);
        }
        return length;
    }

    /// <summary>The shortest chain of connector phrases from one position to another (empty when they are the
    /// same), by breadth-first search over the plain phrases; among chains of one length the lowest tiers win.
    /// The position graph is five nodes, and every position has a tier-1 way back to Centre, so a chain always
    /// exists.</summary>
    private static List<InternodePhrase> Connect(InternodePos from, InternodePos to)
    {
        var chain = new List<InternodePhrase>();
        if (from == to) return chain;
        var links = InternodePhrases.All.Where(q => q.Tier <= 2 && q.Holes.Length == 0 && !q.Closed && q.RimSafe && q.MinStage <= 1 && q.Entry != q.Exit)
                                        .OrderBy(q => q.Tier).ToArray();
        var via = new Dictionary<InternodePos, InternodePhrase>();
        var queue = new Queue<InternodePos>();
        queue.Enqueue(from);
        while (queue.Count > 0 && !via.ContainsKey(to))
        {
            var at = queue.Dequeue();
            foreach (var q in links)
                if (q.Entry == at && q.Exit != from && !via.ContainsKey(q.Exit)) { via[q.Exit] = q; queue.Enqueue(q.Exit); }
        }
        if (!via.ContainsKey(to)) return chain;
        for (var at = to; at != from; at = via[at].Entry) chain.Insert(0, via[at]);
        return chain;
    }

    /// <summary>A plank: a ribbon of floor with a gap down either side, which the player has to ride rather
    /// than jump. From <see cref="InternodeTuning.PlankStaggerFirstStage"/> a run of them staggers — a ribbon
    /// ends outright, a short void follows, and the next starts on a different axis, so the hand-over is a
    /// jump with a sideways move in it.
    ///
    /// <para>Each ribbon is two gaps sharing a z-span: everything from the far rim up to the ribbon's near
    /// edge, and from its far edge back to the other rim. Nothing else was needed to build one — the physics
    /// drops the runner through any gap covering its angle, and the renderer paints every hole on a ring.</para>
    ///
    /// <para>⚠ Fairness here is structural, not proven: the oracle checks phrases, not floor. The three
    /// things holding it up are the ribbon's width, the void being inside one jump press, and the axis shift
    /// being inside what air steering covers — see the plank block in <c>InternodeTuning</c>.</para>
    ///
    /// <para>Returns the seconds of travel consumed, or 0 if there was no room and nothing was placed.</para></summary>
    private static double PlacePlank(int stage, double z, double sectionCap,
                                     List<InternodeGap> gaps, List<InternodePlacedEvent> events, int maxEvents,
                                     ref ulong rng)
    {
        double clear = Math.Max(0, InternodeTuning.GapTokenClearSeconds);
        double half = Math.Clamp(InternodeTuning.PlankHalfWidthDeg, 6, 80);
        double minS = Math.Max(0.2, InternodeTuning.PlankMinSeconds);
        double maxS = Math.Max(minS, InternodeTuning.PlankMaxSeconds);
        bool stagger = stage >= Math.Max(1, InternodeTuning.PlankStaggerFirstStage);
        int ribbons = stagger ? 1 + (int)(NextDouble(ref rng) * Math.Max(1, InternodeTuning.PlankRibbonsMax)) : 1;
        ribbons = Math.Clamp(ribbons, 1, Math.Max(1, InternodeTuning.PlankRibbonsMax));

        // ⚠ Measure the whole run before placing any of it: a plank that ran out of section half way would
        // leave a ribbon with no floor after it, which is a fall the player could not have read.
        double voidMin = Math.Max(0.1, InternodeTuning.PlankVoidMinSeconds);
        double voidMax = Math.Clamp(Math.Max(voidMin, InternodeTuning.PlankVoidMaxSeconds), voidMin,
                                    Math.Max(voidMin, InternodeTuning.GapMaxSeconds));
        double needed = clear + ribbons * maxS + (ribbons - 1) * voidMax + clear;
        if (z + needed > sectionCap) return 0;

        double centreMax = Math.Clamp(InternodeTuning.PlankCentreDegMax, 0, 60);
        double shiftMin = Math.Clamp(InternodeTuning.PlankShiftDegMin, 1, 120);
        double shiftMax = Math.Clamp(Math.Max(shiftMin, InternodeTuning.PlankShiftDegMax), shiftMin, 120);
        double rim = InternodeTuning.RimDeg;
        double at = z + clear;
        double centre = (NextDouble(ref rng) * 2 - 1) * centreMax;

        for (int r = 0; r < ribbons; r++)
        {
            if (r > 0)
            {
                // The void: the ribbon simply ends. A full-width hole, so there is no running round it.
                double gapS = voidMin + NextDouble(ref rng) * (voidMax - voidMin);
                gaps.Add(new InternodeGap(at, gapS, -rim, rim, InternodeGapOrigin.Plank));
                at += gapS;
                // …and the next ribbon lands on a different axis.
                // ⚠ The side is chosen from the room available, and the shift is then drawn to fit it: drawing
                // the shift first and clamping it to the band can produce a hand-over narrower than the ribbon
                // itself — a steer wearing a jump's clothes — whenever both directions overflow.
                double roomLeft = (centre - shiftMin) + centreMax;
                double roomRight = centreMax - (centre + shiftMin);
                bool right = roomLeft > 0 && roomRight > 0 ? NextDouble(ref rng) < 0.5
                           : roomRight >= roomLeft;
                double room = Math.Max(0, right ? roomRight : roomLeft);
                double shift = shiftMin + NextDouble(ref rng) * Math.Min(shiftMax - shiftMin, room);
                centre = Math.Clamp(centre + (right ? shift : -shift), -centreMax, centreMax);
            }

            double runS = minS + NextDouble(ref rng) * (maxS - minS);
            double lo = centre - half, hi = centre + half;
            // The two flanks. Each reaches its rim, so on an open pipe they simply run out at the opening and
            // on a closed one they meet over the top — either way the only floor left is the ribbon.
            gaps.Add(new InternodeGap(at, runS, -rim, lo, InternodeGapOrigin.Plank));
            gaps.Add(new InternodeGap(at, runS, hi, rim, InternodeGapOrigin.Plank));

            // ⚠ No orbs on the ribbon, deliberately. Every orb in a section belongs to a phrase, because that
            // is what the reachability oracle proves catchable one phrase at a time; orbs laid straight onto
            // the floor here would be plainly reachable to a human and unproven to the suite, which trades a
            // guarantee for a flourish. The plank's own span is left empty like a gap's, and the phrases
            // either side of it keep their orbs.
            at += runS;
        }
        return at + clear - z;
    }

    /// <summary>Mines laid against a gap, from <see cref="InternodeTuning.GapMineFirstStage"/>: a ring across the whole
    /// surface just before a full gap (the jump has to start before the mine and carry the hole), or mines on the
    /// floor that remains beside a partial gap, at its middle (running round it means jumping a mine next to the
    /// drop). Mines never sit over the hole itself. The chance is drawn whenever a gap is placed, so the RNG stream
    /// does not depend on the stage.</summary>
    private static void ArmGap(int stage, double z0, double seconds, double lo, double hi, List<InternodePlacedEvent> events, int maxEvents, ref ulong rng)
    {
        double chance = Math.Min(Math.Clamp(InternodeTuning.GapMineChanceMax, 0, 1),
            Math.Clamp(InternodeTuning.GapMineChance, 0, 1) + Math.Max(0, InternodeTuning.GapMineChancePerStage) * (stage - 1));
        bool arm = NextDouble(ref rng) < chance;
        if (!arm || stage < Math.Max(1, InternodeTuning.GapMineFirstStage)) return;
        double max = InternodeTuning.RimDeg - InternodeTuning.MineHalfWidthDeg;
        // ⚠ Same count as a phrase's own ring (InternodePhrases.RingMines), for the same reason: at the mine
        // width this pairs with, seven across the surface still block shoulder to shoulder while the mine art
        // clears its neighbours. Kept a separate literal rather than shared because the phrase bank bakes its
        // rings at type-load, before any tuning override is read, and this runs per section.
        const int count = 7;
        double step = 2 * max / (count - 1);
        bool full = lo <= -InternodeTuning.RimDeg && hi >= InternodeTuning.RimDeg;
        double z = full ? z0 - Math.Max(0, InternodeTuning.GapMineLeadSeconds) : z0 + seconds / 2;
        for (int i = 0; i < count && events.Count < maxEvents; i++)
        {
            double a = -max + i * step;
            // Beside a partial gap only where there is floor: a mine over the hole would be unavoidable to a jumper.
            if (!full && a >= lo - InternodeTuning.MineHalfWidthDeg && a <= hi + InternodeTuning.MineHalfWidthDeg) continue;
            events.Add(new InternodePlacedEvent(z, InternodePhysics.Wrap(a * InternodePhysics.Deg), InternodeEventKind.Mine, 0));
        }
    }

    private static void Place(InternodePhrase p, ref double z, double scale, List<InternodePlacedEvent> events,
                              List<string> ids, List<double> starts, int maxEvents, List<InternodeGap>? gaps = null)
    {
        ids.Add(p.Id);
        starts.Add(z);
        // A time-locked phrase is spaced in real seconds: its timing is a flight the physics fixes, not a tempo.
        double f = p.TimeLocked ? 1 : scale;
        foreach (var e in p.Events)
        {
            if (events.Count >= maxEvents) break;
            events.Add(new InternodePlacedEvent(z + e.Dz * f, InternodePhysics.Wrap(e.ThetaDeg * InternodePhysics.Deg), e.Kind, e.Height)
                { Phrase = ids.Count - 1, Branch = e.Branch });
        }
        // The phrase's own holes, on the same clock as its events so a hole and the orbs around it keep the
        // spacing the oracle proved. ⚠ Never Plank: that flag marks the sequencer's ribbon reservations, and
        // CheckPlanks reads it to apply the hand-over rules, which say nothing about an authored figure.
        if (gaps is not null)
            foreach (var h in p.Holes)
                gaps.Add(new InternodeGap(z + h.Dz * f, h.Seconds * f, h.ThetaMinDeg, h.ThetaMaxDeg,
                                          InternodeGapOrigin.Authored, h.ThetaMinEndDeg, h.ThetaMaxEndDeg));
        z += p.Length * f;
    }

    /// <summary>Which family a phrase belongs to, for the stage flavour. One family each, by priority:
    /// a choice phrase is a choice first, then a crossing, then a mine figure, else plain flow.
    /// <para>⚠ Track outranks Mines on purpose. A ribbon with one mine parked on it is a geometry problem with
    /// seasoning, not a mine figure, and classing it by its single mine would scatter the gap vocabulary
    /// across two families — where the flavour rotation could never feature it as the thing a level is about.</para></summary>
    public enum Family { Flow = 0, Crossing = 1, Choice = 2, Track = 3, Mines = 4 }

    public static Family FamilyOf(InternodePhrase p) =>
        p.Events.Any(e => e.Branch > 0) ? Family.Choice
        : !p.RimSafe ? Family.Crossing
        : p.Holes.Length > 0 ? Family.Track
        : p.Events.Any(e => e.Kind == InternodeEventKind.Mine) ? Family.Mines
        : Family.Flow;

    /// <summary>The family a stage leans on. ⚠ Per stage, not per section: a level is meant to have one
    /// character, so every section of it shares the lean.</summary>
    public static Family FlavourOf(int stage) =>
        (Family)(((Math.Max(1, stage) - 1) % Math.Max(1, InternodeTuning.StageFlavours) + Math.Max(1, InternodeTuning.StageFlavours))
                 % Math.Max(1, InternodeTuning.StageFlavours));

    /// <summary>Pick weight for one candidate. Base is <c>1 + (tier − floor)</c>, the pull toward the cap,
    /// steepened with the stage; then the mine damper, then the stage's flavour boost.</summary>
    private static double Weight(InternodePhrase p, int floor, int stage, Family flavour)
    {
        double bias = 1 + Math.Min(Math.Max(0, InternodeTuning.TierBiasMax),
                                   Math.Max(0, InternodeTuning.TierBiasPerStage) * (Math.Max(1, stage) - 1));
        double w = 1 + (p.Tier - floor) * bias;
        if (p.Events.Any(e => e.Kind == InternodeEventKind.Mine)) w *= Math.Max(0, InternodeTuning.MinePhraseWeight);
        if (FamilyOf(p) == flavour) w *= Math.Max(0, InternodeTuning.StageFlavourBoost);
        // A diabolical phrase trickles in along its ramp and dominates past the end of it; below its MinStage
        // the candidate filter never offers it at all.
        if (p.Diabolical)
        {
            int from = Math.Max(1, InternodeTuning.DiabolicalRampFromStage), to = Math.Max(from + 1, InternodeTuning.DiabolicalRampToStage);
            w *= Math.Max(0, InternodeTuning.DiabolicalBoost) * Math.Clamp((stage - from) / (double)(to - from), 0, 1);
        }
        // Never zero: a class whose only candidates are damped still has to be able to route out of itself.
        return Math.Max(0.02, w);
    }

    /// <summary>⚠ Draws exactly one <see cref="NextDouble"/> whatever the weights, so the RNG stream keeps its
    /// shape and a section's later draws (turns, twists, rims, gaps) are unaffected by how this picked.</summary>
    private static InternodePhrase Weighted(List<InternodePhrase> candidates, int floor, int stage, Family flavour, ref ulong rng)
    {
        double total = 0;
        foreach (var p in candidates) total += Weight(p, floor, stage, flavour);
        double r = NextDouble(ref rng) * total;
        foreach (var p in candidates)
        {
            r -= Weight(p, floor, stage, flavour);
            if (r < 0) return p;
        }
        return candidates[^1];
    }

    /// <summary>Turns: each on one axis (up, down, left or right), so the renderer's occlusion reads as a corner
    /// and never a diagonal drift. Non-overlapping (one per slot of [0, Length − Lookahead]) so two can never
    /// sum past the offset space, and the gate approach stays straight.</summary>
    private static InternodeBend[] DrawTurns(double length, ref ulong rng)
    {
        int min = Math.Max(0, InternodeTuning.TurnsPerSectionMin), max = Math.Max(min, InternodeTuning.TurnsPerSectionMax);
        int count = Math.Min(max, min + (int)(NextDouble(ref rng) * (max - min + 1)));
        double end = length - InternodeTuning.LookaheadSeconds;
        double minS = Math.Max(0.5, InternodeTuning.TurnMinSeconds), maxS = Math.Max(minS, InternodeTuning.TurnMaxSeconds);
        double ampMax = Math.Clamp(InternodeTuning.TurnMaxAmplitude, 0, 1);
        double ampMin = ampMax * Math.Clamp(InternodeTuning.TurnMinAmplitudeShare, 0, 1);
        if (count <= 0 || end < minS) return [];
        double slot = end / count;
        var bends = new List<InternodeBend>(count);
        for (int i = 0; i < count; i++)
        {
            double seconds = Math.Min(slot, minS + NextDouble(ref rng) * (maxS - minS));
            double z0 = i * slot + NextDouble(ref rng) * (slot - seconds);
            double amp = (ampMin + NextDouble(ref rng) * (ampMax - ampMin)) * (NextDouble(ref rng) < 0.5 ? -1 : 1);
            bool vertical = NextDouble(ref rng) < 0.5;
            bends.Add(vertical ? new InternodeBend(z0, seconds, 0, amp) : new InternodeBend(z0, seconds, amp, 0));
        }
        return [.. bends];
    }

    /// <summary>Twists come as a group that nets to exactly zero: the gate is level and the next section starts
    /// level, so the renderer's world roll never snaps at a section seam. Non-overlapping, confined to
    /// [lead, Length − Lookahead]. A section draws a group with probability <see cref="InternodeTuning.TwistChance"/>
    /// (the draw is made whenever twists are eligible, so the rest of the section's RNG stream is unaffected
    /// by the outcome).</summary>
    private static InternodeTwist[] DrawTwists(int stage, double lead, double length, ref ulong rng)
    {
        int count = InternodeTuning.TwistsPerSectionMax;
        if (stage < Math.Max(1, InternodeTuning.TwistFirstStage) || count < 2) return [];
        if (NextDouble(ref rng) >= Math.Clamp(InternodeTuning.TwistChance, 0, 1)) return [];
        double spanStart = lead, spanEnd = length - InternodeTuning.LookaheadSeconds;
        double minS = Math.Max(0.5, InternodeTuning.TwistMinSeconds), maxS = Math.Max(minS, InternodeTuning.TwistMaxSeconds);
        if (spanEnd - spanStart < count * minS) return [];

        // Partition the span into `count` slots and place one twist inside each, so they cannot overlap.
        double slot = (spanEnd - spanStart) / count;
        var twists = new InternodeTwist[count];
        double sum = 0;
        for (int i = 0; i < count; i++)
        {
            double seconds = Math.Min(slot, minS + NextDouble(ref rng) * (maxS - minS));
            double z0 = spanStart + i * slot + NextDouble(ref rng) * (slot - seconds);
            double total;
            if (i == count - 1) total = -sum;
            else
            {
                double mag = (0.5 + 0.5 * NextDouble(ref rng)) * InternodeTuning.TwistMaxDeg * InternodePhysics.Deg;
                total = NextDouble(ref rng) < 0.5 ? -mag : mag;
            }
            sum += total;
            twists[i] = new InternodeTwist(z0, seconds, total);
        }
        return twists;
    }

    /// <summary>Closures of the pipe: the span [0, Length − Lookahead] is split into <see cref="InternodeTuning.RimClosuresPerSectionMax"/>
    /// slots, and each slot draws one closure with probability <see cref="InternodeTuning.RimClosureChance"/> — a
    /// tunnel design (high walls, canyon or full tube) held over a span sized between
    /// <see cref="InternodeTuning.RimMinSeconds"/> and <see cref="InternodeTuning.RimMaxSeconds"/> and capped by the
    /// slot, so two can never overlap and the gate is approached at the base rim. A closure may sit only where no
    /// phrase that needs the open top (one not marked rim-safe) lies within <see cref="InternodeTuning.RimSafeMarginSeconds"/>
    /// of it; that rule is what lets the oracle prove the bank at the base rim. It takes the longest crossing-free
    /// stretch of its slot, and a slot with no stretch long enough gets none, never a forced one. Every RNG draw is
    /// made whether or not the closure lands, so the rest of the stream does not depend on the outcome.</summary>
    private static InternodeRim[] DrawRims(int stage, double length, List<string> ids, List<double> starts, double scale, ref ulong rng)
    {
        if (stage < Math.Max(1, InternodeTuning.RimClosureFirstStage)) return [];
        int slots = Math.Max(0, InternodeTuning.RimClosuresPerSectionMax);
        double end = length - InternodeTuning.LookaheadSeconds;
        double minS = Math.Max(1, InternodeTuning.RimMinSeconds), maxS = Math.Max(minS, InternodeTuning.RimMaxSeconds);
        double lo = Math.Clamp(InternodeTuning.RimMinDeg, 30, 180), hi = Math.Clamp(InternodeTuning.RimMaxDeg, lo, 180);
        double margin = Math.Max(0, InternodeTuning.RimSafeMarginSeconds);
        if (slots <= 0 || end < minS) return [];
        double slot = end / slots;
        if (slot < minS) return [];

        // Two kinds of span the random closures must keep off: a crossing, which needs the open top, and a
        // closed phrase, which gets a forced full-tube closure of its own below (ramps included) that a random
        // one may not stack on — the profile sums overlapping closures, and a sum past the tube is clamped,
        // which would leave a bump that never reaches its own target.
        var needsTop = new List<(double A, double B)>();
        var forced = new List<(double A, double B)>();
        double ramp = Math.Max(0.01, InternodeTuning.RimRampSeconds);
        for (int i = 0; i < ids.Count; i++)
        {
            var p = InternodePhrases.Find(ids[i]);
            if (p is null) continue;
            double a = starts[i], b = starts[i] + p.Length * (p.TimeLocked ? 1 : scale);
            if (p.Closed) forced.Add((a - ramp, b + ramp));
            else if (!p.RimSafe) needsTop.Add((a, b));
        }
        // Adjacent closed phrases share one closure: their ramps would otherwise overlap and sum past the tube.
        forced.Sort((x, y) => x.A.CompareTo(y.A));
        for (int i = forced.Count - 1; i > 0; i--)
            if (forced[i].A <= forced[i - 1].B) { forced[i - 1] = (forced[i - 1].A, Math.Max(forced[i - 1].B, forced[i].B)); forced.RemoveAt(i); }
        needsTop.AddRange(forced);

        needsTop.Sort((x, y) => x.A.CompareTo(y.A));
        var rims = new List<InternodeRim>(slots);
        foreach (var (a, b) in forced) rims.Add(new InternodeRim(a, b - a, InternodeTuning.RimDesignTubeDeg));
        for (int i = 0; i < slots; i++)
        {
            bool draw = NextDouble(ref rng) < Math.Clamp(InternodeTuning.RimClosureChance, 0, 1);
            double target = Math.Clamp((int)(NextDouble(ref rng) * 3) switch
            {
                0 => InternodeTuning.RimDesignWallsDeg,
                1 => InternodeTuning.RimDesignCanyonDeg,
                _ => InternodeTuning.RimDesignTubeDeg,
            }, lo, hi);
            double u1 = NextDouble(ref rng), u2 = NextDouble(ref rng);
            if (!draw) continue;
            // The longest stretch of the slot that no crossing (plus its margin) touches is where the closure goes:
            // searching at random found homes too rarely for the half-pipe to be the minority it should be.
            double slotA = i * slot, slotB = (i + 1) * slot;
            double bestA = 0, bestB = 0, cursor = slotA;
            foreach (var (a, b) in needsTop)
            {
                // A hair beyond the margin, so a closure starting exactly at the edge cannot round into the block.
                double blockA = a - margin - 1e-6, blockB = b + margin + 1e-6;
                if (blockB <= cursor) continue;
                if (blockA >= slotB) break;
                if (blockA - cursor > bestB - bestA) { bestA = cursor; bestB = blockA; }
                cursor = Math.Max(cursor, blockB);
            }
            if (slotB - cursor > bestB - bestA) { bestA = cursor; bestB = slotB; }
            double room = bestB - bestA;
            if (room < minS) continue;
            double seconds = Math.Min(room, minS + u1 * (maxS - minS));
            rims.Add(new InternodeRim(bestA + u2 * (room - seconds), seconds, target));
        }
        rims.Sort((a, b) => a.Z0.CompareTo(b.Z0));
        return [.. rims];
    }

    // Xorshift64*, local to the build: the run RNG only supplies the seed.
    private static ulong NextUInt64(ref ulong s) => ArcadeRng.Next(ref s);
    private static double NextDouble(ref ulong s) => ArcadeRng.NextDouble(ref s);
}
