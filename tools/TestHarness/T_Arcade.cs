using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>
/// The Arcade CONCEALMENT contract, which is the half a human can't reliably eyeball.
///
/// The claim under test: with Arcade unavailable (a build compiled without it, simulated here through
/// Arcade.ConcealedForHarness — a const can't be flipped at runtime), nothing in the app offers or mentions
/// Arcade — and yet an existing arcade slice still RESOLVES, so auto-save can't rewrite its type. Those two
/// pull in opposite directions, which is exactly why it's worth a test: the obvious implementation of the
/// first (delete the entries) silently breaks the second.
///
/// What this can't settle: whether the tab strip actually LOOKS right. This asserts the taxonomy and Help
/// topic sets that drive it, and flips the gate both ways so the caches are proven to rebuild rather than
/// latch.
/// </summary>
internal static class T_Arcade
{
    public static void Run()
    {
        // ArcadeStore is plain Core, never gated by Arcade.Enabled — runs whether or not the rest of this
        // group's build carries the feature.
        ArcadeStoreLegacyMusicFold();
        ArcadeStoreRetiredGameIdSurvives();

        CatalogIsWired();

        PauseActionRow();

        InternodeOverTop();

        H.Group("Arcade — an unavailable build conceals everything, yet slices still resolve");

        if (!Arcade.Enabled)
        {
            H.Skip("concealment round trip", "this build is compiled without Arcade (Arcade.Enabled = false) — "
                                             + "concealed is its only state, nothing to flip");
            return;
        }

        try
        {
            Arcade.ConcealedForHarness = true;
            H.Check("Available is false while concealed", !Arcade.Available);
            Assert(offered: false);

            Arcade.ConcealedForHarness = false;
            H.Check("Available is true when not concealed", Arcade.Available);
            Assert(offered: true);

            // Conceal again: the caches key off Arcade.Available, so a one-way latch would pass the two
            // checks above and still leave a permanently-visible Arcade after the first flip.
            Arcade.ConcealedForHarness = true;
            Assert(offered: false);
            H.Pass("gate is reversible (caches rebuild, don't latch)");
        }
        finally { Arcade.ConcealedForHarness = false; }

        Tuning();
        HubCoverage();
        HowToFits();
        CarouselSteering();
    }

    /// <summary>ArcadeStore's legacy-music-fold migration survives a write and a reload. A pre-global file
    /// carries a slot's choice (<c>Slot.MusicOff</c>); <c>Load()</c> must fold that into the top-level flag
    /// ONCE and scrub every slot, because it re-serialises every cached slot on every later write — a slot
    /// left holding OFF would fold the choice back up over whatever the player chose since, and music would
    /// silently revert to off after an unrelated save.
    ///
    /// <para>Also proves the window-position read: <c>Load()</c> must restore <c>Position</c> from disk, or the
    /// arcade window forgets where it was placed on every restart.</para>
    ///
    /// <para>Touches the REAL <c>%APPDATA%\Radiata\arcade-state.json</c> — <see cref="ArcadeStore"/> has no
    /// redirect hook for the harness — so any file already there is renamed aside and restored in
    /// <c>finally</c>, and <see cref="ArcadeStore.DropCacheForHarness"/> forces every assertion to re-read
    /// disk rather than the in-process cache.</para></summary>
    private static void ArcadeStoreLegacyMusicFold()
    {
        H.Group("ArcadeStore — the legacy per-game music fold, and window position, survive a write and a reload");

        var path = Path.Combine(AppPaths.AppDataDir, "arcade-state.json");
        var backup = path + ".harness-backup";
        bool backedUp = false;
        try
        {
            if (File.Exists(path))
            {
                try { File.Delete(backup); } catch { }   // a stale backup from an earlier interrupted run
                File.Move(path, backup);
                backedUp = true;
            }
            Directory.CreateDirectory(AppPaths.AppDataDir);

            // A pre-global file: the music choice lives on the "kabloom" slot, and the window remembers
            // position 2 (the right wheel).
            File.WriteAllText(path,
                "{\"V\":1,\"Position\":2,\"Games\":{\"kabloom\":{\"State\":null,\"HighScore\":0,\"MusicOff\":true}}}");

            ArcadeStore.DropCacheForHarness();
            H.Check("a pre-global file's per-slot MusicOff folds into LoadMusicOn()", !ArcadeStore.LoadMusicOn());
            H.Check("LoadWindowPosition() reads the legacy file's top-level Position", ArcadeStore.LoadWindowPosition() == 2);

            using (var afterFold = JsonDocument.Parse(File.ReadAllText(path)))
            {
                bool topLevelOff = afterFold.RootElement.TryGetProperty("MusicOff", out var top) && top.ValueKind == JsonValueKind.True;
                bool anySlotOff = afterFold.RootElement.GetProperty("Games").EnumerateObject()
                    .Any(g => g.Value.TryGetProperty("MusicOff", out var slot) && slot.ValueKind == JsonValueKind.True);
                H.Check("the fold rewrites the file: the top level now carries MusicOff:true", topLevelOff);
                H.Check("the fold rewrites the file: no slot still carries MusicOff:true", !anySlotOff);
            }

            ArcadeStore.SaveMusicOn(true);
            ArcadeStore.DropCacheForHarness();
            H.Check("LoadMusicOn() reads true after the scrub-and-rewrite (the old code re-folded OFF here)",
                    ArcadeStore.LoadMusicOn());
            H.Check("LoadWindowPosition() still reads 2 after the migration write", ArcadeStore.LoadWindowPosition() == 2);
            H.Check("the scrubbed slot's high score still resolves", ArcadeStore.LoadHighScore("kabloom") == 0);

            // Validated on read, like every other field: an out-of-range Position never written by this
            // build must not be trusted verbatim off a hand-mangled or downgraded file.
            File.WriteAllText(path, "{\"V\":1,\"Position\":7,\"Games\":{}}");
            ArcadeStore.DropCacheForHarness();
            H.Check("an out-of-range Position (7) reads back as -1, not trusted verbatim", ArcadeStore.LoadWindowPosition() == -1);
        }
        finally
        {
            ArcadeStore.DropCacheForHarness();
            try { File.Delete(path); } catch { }
            if (backedUp) { try { File.Move(backup, path); } catch { } }
            ArcadeStore.DropCacheForHarness();
        }
    }

    /// <summary>A slot keyed by a since-deleted game's id (<c>well</c>, removed from
    /// <see cref="ArcadeCatalog"/>) must not stop the file loading, and must still be there — untouched — after
    /// a write and a reload: <see cref="ArcadeStore"/> keys its dictionary by whatever string a slot names, with
    /// no dependency on the catalog, so a deleted game's saved run rides along inertly rather than failing the
    /// load or getting silently dropped on the next save.</summary>
    private static void ArcadeStoreRetiredGameIdSurvives()
    {
        H.Group("ArcadeStore — a retired game's saved slot survives a load, a write and a reload");

        var path = Path.Combine(AppPaths.AppDataDir, "arcade-state.json");
        var backup = path + ".harness-backup";
        bool backedUp = false;
        try
        {
            if (File.Exists(path))
            {
                try { File.Delete(backup); } catch { }
                File.Move(path, backup);
                backedUp = true;
            }
            Directory.CreateDirectory(AppPaths.AppDataDir);

            File.WriteAllText(path,
                "{\"V\":1,\"Games\":{\"well\":{\"State\":\"{\\\"V\\\":1}\",\"HighScore\":1234},"
                + "\"kabloom\":{\"State\":null,\"HighScore\":0}}}");

            ArcadeStore.DropCacheForHarness();
            H.Try("loading a file with a retired game's slot does not throw", () => ArcadeStore.LoadHighScore("kabloom"));
            H.Check("the retired slot's high score still reads back", ArcadeStore.LoadHighScore("well") == 1234);

            // Any write serialises the whole cached dictionary, so a save for a live game must not drop the
            // unrelated deleted-game slot riding along in it.
            ArcadeStore.Save("kabloom", null, 5, null);
            ArcadeStore.DropCacheForHarness();
            H.Check("the retired slot survives a write triggered by a different game", ArcadeStore.LoadHighScore("well") == 1234);
            H.Check("the live game's own save still took", ArcadeStore.LoadHighScore("kabloom") == 5);
        }
        finally
        {
            ArcadeStore.DropCacheForHarness();
            try { File.Delete(path); } catch { }
            if (backedUp) { try { File.Move(backup, path); } catch { } }
            ArcadeStore.DropCacheForHarness();
        }
    }

    /// <summary>The host's pause-row handling of an action row (<see cref="ArcadePauseOption.StartsOnConfirm"/>):
    /// Connate's START AT STAGE browses on the d-pad and starts only on Cross, while every other row — Kabloom's
    /// starting size, Connate's RADIAL CONTROLS — still applies on a step and cycles on Cross. Driven through the
    /// control's own <c>StepPause</c>, with the arcade store backed up because a start persists.</summary>
    private static void PauseActionRow()
    {
        H.Group("Arcade — the pause menu's action row browses on the d-pad and starts on Cross");
        var path = Path.Combine(AppPaths.AppDataDir, "arcade-state.json");
        var backup = path + ".harness-backup";
        bool backedUp = false;
        try
        {
            if (File.Exists(path))
            {
                try { File.Delete(backup); } catch { }
                File.Move(path, backup);
                backedUp = true;
            }
            Directory.CreateDirectory(AppPaths.AppDataDir);
            ArcadeStore.DropCacheForHarness();

            const int Right = (int)ArcadeInput.DPad.Right, Left = (int)ArcadeInput.DPad.Left;
            const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            System.Collections.Generic.IReadOnlyList<ArcadePauseOption> Options(object c) =>
                (System.Collections.Generic.IReadOnlyList<ArcadePauseOption>)c.GetType().GetProperty("PauseOptions", Priv)!.GetValue(c)!;
            object Control(IArcadeGame game, string rowKey)
            {
                var c = T_RenderArcade.NewControl();
                T_Render.Set(c, "_game", game);
                T_Render.Set(c, "_musicOn", true);
                T_Render.Set(c, "_paused", true);
                var fixedRows = (Array)c.GetType().GetProperty("FixedRows", Priv)!.GetValue(c)!;
                T_Render.Set(c, "_pauseIndex", fixedRows.Length + Options(c).ToList().FindIndex(o => o.Key == rowKey));
                return c;
            }
            ArcadePauseOption Row(object c, string key) => Options(c).First(o => o.Key == key);
            void Press(object c, int dpad = 0, bool cross = false)
            {
                T_Render.Set(c, "_dpadEdges", (ArcadeInput.DPad)dpad);
                T_Render.Set(c, "_crossEdge", cross);
                T_Render.Call(c, "StepPause");
            }
            bool Asking(object c) => T_Render.Get(c, "_confirmPrompt") is not null;
            void Answer(object c, bool yes)
            {
                T_Render.Set(c, "_confirmYes", yes);
                Press(c, cross: true);
            }
            Connate Live(int unlocked, int start)
            {
                var g = (Connate)T_RenderArcade.Create(Connate.GameId);
                g.SeedHighScore(1234);
                g.RestoreSettings($"{{\"Radial\":false,\"Unlocked\":{unlocked}}}");
                g.ApplyPauseOption(Connate.StartKey, Array.IndexOf(g.Starts.Offered(), start));
                T_RenderArcade.Play(g, [new(0.4)]);
                return g;
            }

            // Stepping browses: no prompt, no restart, the run untouched, the shown value moves and clamps.
            var live = Live(20, 5);
            string snapshot = live.Serialize();
            var c1 = Control(live, Connate.StartKey);
            for (int i = 0; i < 5; i++) Press(c1, Right);
            H.Check("stepping over a live run never prompts", !Asking(c1));
            H.Check("stepping never restarts or touches the run", live.StartStage == 5 && live.Serialize() == snapshot);
            H.Check("the shown value follows the d-pad and clamps at the last start",
                    Row(c1, Connate.StartKey).Selected == 4 && Row(c1, Connate.StartKey).Choices[4] == "20");
            Press(c1, Left); Press(c1, Left);
            H.Check("left steps back (no wrap)", Row(c1, Connate.StartKey).Selected == 2 && !Asking(c1) && live.StartStage == 5);

            // Closing the menu discards the browsed value.
            T_Render.Call(c1, "TogglePause", true);
            T_Render.Call(c1, "TogglePause", true);
            T_Render.Set(c1, "_pauseIndex", 4);
            H.Check("closing and reopening the menu shows the stage the run began at",
                    Row(c1, Connate.StartKey).Selected == Array.IndexOf(live.Starts.Offered(), 5) && live.StartStage == 5);

            // Cross starts at the shown value, through the Reset confirm over a live run.
            Press(c1, Right); Press(c1, Right);   // 5 -> 10 -> 15
            Press(c1, cross: true);
            H.Check("Cross over a live run asks THIS ENDS THE RUN IN PROGRESS", Asking(c1)
                    && (string)T_Render.Get(c1, "_confirmPrompt")! == Loc.T(UiText.Arcade.EndsRun));
            H.Check("nothing has restarted while the question is up", live.StartStage == 5 && live.Serialize() == snapshot);
            Answer(c1, yes: false);
            H.Check("NO leaves the run as it was", !Asking(c1) && live.StartStage == 5 && live.Serialize() == snapshot);
            Press(c1, cross: true);
            Answer(c1, yes: true);
            H.Check("YES starts a new run at the shown value, seeded to its threshold",
                    live.StartStage == 15 && live.ExplodedValue == ConnateTuning.StageThresholdFor(15));
            H.Check("the row then shows the new run's stage", Row(c1, Connate.StartKey).Selected == 3);

            // Cross at the stage the run already began at is still a new run, so it asks.
            Press(c1, cross: true);
            H.Check("Cross at the current start over a live run asks", Asking(c1));
            Answer(c1, yes: false);

            // Value 1 is Reset.
            for (int i = 0; i < 4; i++) Press(c1, Left);
            Press(c1, cross: true);
            H.Check("Cross on 1 over a live run asks", Asking(c1));
            Answer(c1, yes: true);
            H.Check("Cross on 1 is Reset: the opening card at stage 1",
                    live.Phase == Connate.Stage.Intro && live.StartStage == 1 && live.CollectedScore == 0);

            // Nothing live: Cross starts without a question.
            var idle = (Connate)T_RenderArcade.Create(Connate.GameId);
            idle.RestoreSettings("{\"Radial\":false,\"Unlocked\":20}");
            var c2 = Control(idle, Connate.StartKey);
            Press(c2, Right); Press(c2, Right);
            Press(c2, cross: true);
            H.Check("Cross with nothing to lose starts at the shown value without a question",
                    !Asking(c2) && idle.StartStage == 10 && idle.ExplodedValue == ConnateTuning.StageThresholdFor(10));

            // Every other row behaves as before.
            var radialGame = Live(20, 5);
            var c3 = Control(radialGame, "radial");
            Press(c3, Right);
            H.Check("RADIAL CONTROLS still applies on a d-pad step", radialGame.RadialAiming);
            Press(c3, cross: true);
            H.Check("RADIAL CONTROLS still cycles on Cross (wraps back)", !radialGame.RadialAiming);
            H.Check("an ordinary row is not an action row", !Row(c3, "radial").StartsOnConfirm);

            // Kabloom's starting size is an action row too: it browses, and Cross starts at the shown size.
            var kab = (Kabloom)T_RenderArcade.Create(Kabloom.GameId);
            var c4 = Control(kab, "start");
            H.Check("Kabloom's row is an action row", Row(c4, "start").StartsOnConfirm);
            Press(c4, Right); Press(c4, Right); Press(c4, Right);
            H.Check("Kabloom's d-pad only browses (clamped), the game's size untouched",
                    kab.StartingSize == 0 && Row(c4, "start").Selected == 2 && !Asking(c4));
            string kabSnap = kab.Serialize();
            string? kabSettings = kab.SerializeSettings();
            T_Render.Call(c4, "TogglePause", true);
            T_Render.Call(c4, "TogglePause", true);
            H.Check("closing the menu drops the browsed size", Row(c4, "start").Selected == 0 && kab.Serialize() == kabSnap);

            // Nothing live: Cross starts at the shown size without a question, and persists the size.
            T_Render.Set(c4, "_pauseIndex", ((Array)c4.GetType().GetProperty("FixedRows", Priv)!.GetValue(c4)!).Length
                         + Options(c4).ToList().FindIndex(o => o.Key == "start"));
            Press(c4, Right);
            Press(c4, cross: true);
            H.Check("Kabloom Cross with nothing to lose starts at the shown size without asking",
                    !Asking(c4) && kab.StartingSize == 1 && kab.CurrentLevel == Kabloom.StartLevelFor(1));
            H.Check("only the confirmed start persisted the size", kab.SerializeSettings() != kabSettings
                    && ArcadeStore.LoadSettings(Kabloom.GameId) == kab.SerializeSettings());

            // Over a live run: the Reset-style question, at a different size and at the current one.
            var live4 = new Kabloom();
            T_RenderArcade.Play(live4, [new(0.4)]);
            T_Render.Call(live4, "LoadLevel", Kabloom.StartLevelFor(0) + 1, false, false);
            string liveSnap = live4.Serialize();
            var c5 = Control(live4, "start");
            Press(c5, Right);
            Press(c5, cross: true);
            H.Check("Kabloom Cross over a live run asks START A NEW RUN?",
                    Asking(c5) && (string)T_Render.Get(c5, "_confirmPrompt")! == Loc.T(UiText.Arcade.StartNewRun));
            H.Check("nothing has restarted or persisted while the question is up",
                    live4.StartingSize == 0 && live4.Serialize() == liveSnap);
            Answer(c5, yes: false);
            H.Check("NO leaves the run as it was", !Asking(c5) && live4.StartingSize == 0 && live4.Serialize() == liveSnap);
            Press(c5, cross: true);
            Answer(c5, yes: true);
            H.Check("YES starts a new run at the shown size",
                    live4.StartingSize == 1 && live4.CurrentLevel == Kabloom.StartLevelFor(1));
            T_Render.Call(live4, "LoadLevel", Kabloom.StartLevelFor(1) + 1, false, false);
            Press(c5, cross: true);
            H.Check("Kabloom Cross at the current size over a live run asks", Asking(c5));
            Answer(c5, yes: true);
            H.Check("and restarts at that size", live4.StartingSize == 1 && live4.CurrentLevel == Kabloom.StartLevelFor(1));
        }
        finally
        {
            ArcadeStore.DropCacheForHarness();
            try { File.Delete(path); } catch { }
            if (backedUp) { try { File.Move(backup, path); } catch { } }
            ArcadeStore.DropCacheForHarness();
        }
    }

    /// <summary>The picker's steering state machine (<see cref="ArcadeCarouselNav"/>): one step per flick,
    /// auto-repeat while held, hysteresis on release, d-pad level identical, vertical input ignored — the
    /// contract docs/ARCADE.md ▸ The picker states. Pure Core, so it runs here without a controller.</summary>
    private static void CarouselSteering()
    {
        H.Group("Arcade — picker steering");
        const double dt = 1.0 / 60;
        double arm = ArcadePickerTuning.StepArmThreshold, rel = ArcadePickerTuning.StepReleaseThreshold;

        var nav = new ArcadeCarouselNav();
        int Hold(float x, bool left, bool right, double seconds)
        {
            int steps = 0;
            for (double t = 0; t < seconds; t += dt) steps += nav.Step(x, left, right, dt);
            return steps;
        }

        H.Check("a flick past the arm threshold steps once", nav.Step((float)(arm + 0.05), false, false, dt) == 1);
        H.Check("holding below the repeat delay adds nothing",
                Hold((float)(arm + 0.05), false, false, ArcadePickerTuning.RepeatDelaySeconds * 0.8) == 0);
        H.Check("easing into the hysteresis band keeps the hold armed (no re-step, no release)",
                Hold((float)((arm + rel) / 2), false, false, 0.05) == 0);
        nav.Reset();

        // The flick itself, the first repeat at the delay, then one per period up to the second mark.
        int expected = 2 + (int)Math.Floor((1.0 - ArcadePickerTuning.RepeatDelaySeconds) / ArcadePickerTuning.RepeatSeconds + 1e-9);
        int held = Hold(1f, false, false, 1.0);
        H.Check("a one-second hold repeats at the tuned cadence", Math.Abs(held - expected) <= 1, $"{held} steps, expected about {expected}");

        H.Check("releasing below the release threshold disarms", nav.Step((float)(rel - 0.05), false, false, dt) == 0
                && nav.Step((float)(arm + 0.05), false, false, dt) == 1);
        nav.Reset();

        H.Check("left is the mirror of right", nav.Step(-1f, false, false, dt) == -1);
        nav.Reset();
        H.Check("d-pad right steps once and then repeats like the stick", nav.Step(0, false, true, dt) == 1
                && Math.Abs(Hold(0, false, true, 1.0) - (expected - 1)) <= 1);
        nav.Reset();
        H.Check("both d-pad directions held cancel out", Hold(0, true, true, 0.5) == 0);
        H.Check("a stick at rest with no d-pad does nothing", Hold(0, false, false, 0.5) == 0);
    }

    /// <summary>Every game's how-to card fits inside the round window.
    ///
    /// <para>The bullets are editable copy and the card has no scrolling and no second page. A longer
    /// line wraps, which pushes every row below it down — so an edit that reads fine in a text file can walk
    /// the last bullet off the bottom of the disc, or out through its curved side, with nothing to say so
    /// until someone opens that game on hardware.</para>
    ///
    /// <para>Measured through <c>ArcadeControl.MeasureHowTo</c>, i.e. the layout the app actually draws,
    /// rather than a re-derivation of its constants. Checked at the SMALLEST plausible playfield: the type
    /// sizes have floors (<c>Math.Max(8, …)</c>), so a small disc is proportionally the tightest case and a

    /// <summary>Across the OPEN TOP the runner is a free particle under gravity with the ring as a one-sided
    /// wall — no homing term at all. The question this settles: can a crossing STALL up there, leaving the
    /// runner parked at the apex where the pendulum has no pull?
    ///
    /// <para>Gravity acts on every step of that case, so the answer should be no for every entry, and this
    /// drives real crossings to confirm it: a grounded runner is
    /// walked up the wall at a sweep of angular speeds until the rim throws them into flight, then integrated
    /// hands-off (the worst case: a stick input can only add energy) and under a full stick each way. Every
    /// entry must leave the open top — landing on the ring, or dropping through where the floor is missing —
    /// inside the bound. A stall shows up as a runner still airborne at the end with the apex behind them.</para></summary>
    private static void InternodeOverTop()
    {
        H.Group("Internode — no crossing stalls at the open top");

        const double Dt = 1.0 / 120;
        const double Bound = 6.0;          // seconds; a real crossing resolves in well under one
        double rim = InternodePhysics.RimRad;
        double wMax = InternodeTuning.MaxAngularSpeedDeg * InternodePhysics.Deg;

        int crossings = 0, stalled = 0, slowest = 0;
        double worstApex = 0;
        string worst = null;

        // Entry speeds from a crawl at the lip up to the runner's own ceiling. The slow end matters
        // most: a barely-clearing entry is the shape the knob's claim described.
        foreach (double frac in new[] { 0.05, 0.10, 0.15, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 1.00 })
        foreach (double steer in new[] { 0.0, 1.0, -1.0 })
        foreach (bool floorHere in new[] { true, false })
        {
            var r = InternodeRunnerState.AtRest;
            r.Theta = rim * 0.98;   // a runner already at the lip: the entry speed alone decides the crossing
            r.Omega = wMax * frac;
            bool entered = false;
            int steps = 0;
            double apex = 0;

            for (double t = 0; t < Bound; t += Dt, steps++)
            {
                // Hands off until the rim: the entry speed under test is the one set above, not one the stick
                // topped up on the way there.
                double stick = entered ? steer : 0;
                r = InternodePhysics.Integrate(r, stick, false, false, Dt, rim, entered || floorHere, out var flags);
                if ((flags & InternodeStepFlags.RimExit) != 0) { entered = true; crossings++; }
                if (!entered) continue;
                apex = Math.Max(apex, Math.Abs(r.Theta));
                if (r.Air != InternodeAir.OverTop) break;
            }

            if (!entered) continue;   // too slow to reach the rim at all — not a crossing, nothing to stall
            if (r.Air == InternodeAir.OverTop)
            {
                stalled++;
                worst = $"ω={frac:0.00}·max steer={steer:+0;-0;0} floor={floorHere} parked at θ={r.Theta * 180 / Math.PI:0.#}°, h={r.H:0.###}";
            }
            else if (steps > slowest) { slowest = steps; worstApex = apex * 180 / Math.PI; }
        }

        H.Check($"every crossing leaves the open top ({crossings} entries swept)", stalled == 0,
                stalled == 0 ? $"slowest resolved in {slowest * Dt:0.00} s, reaching {worstApex:0.#}° from the floor"
                             : $"{stalled} stalled — {worst}");
        H.Check("the sweep actually crossed the rim", crossings > 0, $"{crossings} rim exits");

        // The mechanism behind the answer, asserted directly: the case's gravity is what removes the stall, so
        // a future retune that zeroes it would bring the floater back without failing the sweep above (which
        // would simply stop entering the top). Named here so the reason is checked, not just the symptom.
        H.Check("the open top is still under gravity", InternodeTuning.FlightGravity > 0,
                $"FlightGravity = {InternodeTuning.FlightGravity}");
    }

    /// card that fits there fits everywhere.</para></summary>
    private static void HowToFits()
    {
        var control = H.AppType("ArcadeControl");
        var measure = H.StaticMethod(control, "MeasureHowTo", 3);
        if (measure is null) { H.Fail("ArcadeControl.MeasureHowTo is missing — how-to fit unchecked"); return; }

        // Every shipped language: a translation runs longer than English, and the card shrinks rather than
        // scrolls, so each language is measured against the same disc.
        var langProp = typeof(Loc).GetProperty("Lang") ?? throw new MissingMemberException("Loc.Lang");
        object? before = langProp.GetValue(null);
        try
        {
            foreach (var lang in HelpLocalization.Languages.Where(l => !l.Code.StartsWith("qps", StringComparison.Ordinal)))
            {
                langProp.SetValue(null, lang.Code);
                HowToFitsIn(measure, lang.Code);
            }
        }
        finally { langProp.SetValue(null, before); }
    }

    private static void HowToFitsIn(System.Reflection.MethodInfo measure, string lang)
    {
        string tag = lang == HelpLocalization.DefaultCode ? "" : $" [{lang}]";
        foreach (var entry in ArcadeCatalog.Games)
        {
            IArcadeGame game = entry.Create();
            ArcadeHowTo card = game.HowTo;
            if (card is null) { H.Skip($"{entry.Id}: how-to fits{tag}", "this game has no card"); continue; }

            // 338 px playfield ≈ a game's disc (ArcadeTuning.GameDiscScale × the wheel's own footprint) on a
            // 1080p display, which is the small end of what ships; 1.0 pixels-per-dip keeps this independent
            // of the test machine's scaling.
            const double Field = 338;
            object layout = measure.Invoke(null, new object[] { card, Field, 1.0 });
            double top = (double)layout.GetType().GetProperty("Top").GetValue(layout);
            double bottom = (double)layout.GetType().GetProperty("Bottom").GetValue(layout);
            double half = (double)layout.GetType().GetProperty("ContentWidth").GetValue(layout) / 2;
            double rowSize = (double)layout.GetType().GetProperty("RowSize").GetValue(layout);

            // The disc narrows as you leave its middle, so the block's own corners are what fail first.
            double worst = Math.Max(Math.Abs(top), Math.Abs(bottom));
            double allowed = worst >= Field ? 0 : Field * Math.Sqrt(1 - worst * worst / (Field * Field));
            bool inside = worst < Field && half <= allowed;

            // The footer ("○ BACK") is drawn at 0.72 of the radius; the block must stop above it.
            bool clearsFooter = bottom <= Field * 0.72;

            H.Check($"{entry.Id}: the how-to card fits the disc{tag}", inside,
                    $"{card.Lines.Length} bullets at {rowSize:0.#} px text, block {top:0.#}..{bottom:0.#} of {Field:0} px "
                    + $"— half-width {half:0.#} vs {allowed:0.#} available at the widest row");
            H.Check($"{entry.Id}: the how-to card clears its footer{tag}", clearsFooter,
                    $"block ends at {bottom:0.#}, footer starts at {Field * 0.72:0.#}");
        }
    }

    /// <summary>The hub grows to sit behind the round playfield, divided by
    /// <c>RadialMenuControl.HubCoverageFraction</c> so the SMALLEST part of a scalloped silhouette still
    /// clears the game rather than only its peaks.
    ///
    /// <para>Worth a harness check because the failure is silent and looks like a design choice: a material
    /// left at 1.0 doesn't error, it just quietly draws its shaped rim underneath the playfield and reads as a
    /// plain circle. A wobble that sits "far inside the arcade's 5% margin" is still visible, because
    /// clearing the edge is not the same as being invisible.</para>
    ///
    /// <para>The numbers are PRINTED, not pinned, because they are measurements of shapes that are allowed to
    /// be retuned. What is asserted is the property that matters: a shaped hub must report less than 1, a
    /// round one must report exactly 1, and nothing may report a value so small it would demand an enormous
    /// hub.</para></summary>
    private static void HubCoverage()
    {
        H.Group("Arcade — the hub clears the playfield on every material");

        var control = new RadialMenuControl();
        // Shaped rims (measured) versus plain discs (exactly 1). Reactor and the flat materials draw a clean
        // ellipse, so they are the control group: if one of THEM ever reports under 1 the measurement has
        // started firing on shapes it shouldn't.
        string[] shaped = ["kawaii", "mesa", "salvage"];
        string[] round = ["obsidian", "pearl", "flat", "flat-dark", "reactor"];

        foreach (string material in shaped)
        {
            control.SetSliceMaterial(material);
            double coverage = control.HubCoverageFraction;
            H.Check($"{material}: shaped rim is measured, not assumed",
                    coverage is > 0.5 and < 0.9995,
                    $"coverage {coverage:0.0000} → the hub grows {(1 / coverage - 1) * 100:0.00}% extra");
        }

        foreach (string material in round)
        {
            control.SetSliceMaterial(material);
            double coverage = control.HubCoverageFraction;
            H.Check($"{material}: round hub needs no correction", Math.Abs(coverage - 1.0) < 1e-9,
                    $"coverage {coverage:0.0000}");
        }
    }

    /// <summary>The dev tuning override: <c>%APPDATA%\Radiata\arcade-tuning.json</c>, re-read on every arcade
    /// open. Both directions matter — applying a value, and DELETING it going back to the compiled default
    /// rather than lingering for the life of the process (which is what a naive one-shot loader does, and what
    /// this file's whole "edit and re-open" premise depends on).</summary>
    private static void Tuning()
    {
        H.Group("Arcade — dev tuning override round trip");

        var path = System.IO.Path.Combine(AppPaths.AppDataDir, "arcade-tuning.json");
        if (System.IO.File.Exists(path))
        {
            // Never clobber a real one — the whole point is that a human edits it by hand.
            H.Skip("tuning override round trip", $"{path} already exists; not overwriting a live tuning file");
            return;
        }

        double baseline = ConnateTuning.PlayerDegPerSec;   // a bare key resolves to Connate's knob
        double probe    = baseline + 137;   // an unmistakable value no default could coincidentally be
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            System.IO.File.WriteAllText(path, $"{{ \"PlayerDegPerSec\": {probe} }}");
            ArcadeTuning.LoadOverrides();
            H.Check("override applies", Math.Abs(ConnateTuning.PlayerDegPerSec - probe) < 0.001,
                    $"PlayerDegPerSec = {ConnateTuning.PlayerDegPerSec} (wanted {probe})");

            // A key that isn't in the file must return to its compiled default, not keep the last override.
            System.IO.File.WriteAllText(path, "{ }");
            ArcadeTuning.LoadOverrides();
            H.Check("deleting a key restores the compiled default",
                    Math.Abs(ConnateTuning.PlayerDegPerSec - baseline) < 0.001,
                    $"PlayerDegPerSec = {ConnateTuning.PlayerDegPerSec} (wanted {baseline})");

            // Garbage must be survivable — this file is hand-edited by definition.
            System.IO.File.WriteAllText(path, "{ \"PlayerDegPerSec\": \"fast\", \"NoSuchKnob\": 1, ");
            H.Try("a malformed file is swallowed", ArcadeTuning.LoadOverrides);
            H.Check("…and leaves the defaults intact",
                    Math.Abs(ConnateTuning.PlayerDegPerSec - baseline) < 0.001,
                    $"PlayerDegPerSec = {ConnateTuning.PlayerDegPerSec}");
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { }
            ArcadeTuning.LoadOverrides();   // back to compiled defaults for anything running after this
        }
    }

    /// <summary>Assert the whole visible surface agrees with <paramref name="offered"/>.</summary>
    private static void Assert(bool offered)
    {
        string when = offered ? "available" : "concealed";

        // ── The slice taxonomy (drives the Settings type dropdown AND the in-wheel Add picker) ──
        var editor = H.AppType("WheelEditorControl");
        if (editor is null) { H.Fail($"WheelEditorControl found ({when})", "type missing"); return; }
        var catsProp = editor.GetProperty("Categories", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (catsProp is null)
        {
            H.Fail($"WheelEditorControl.Categories found ({when})",
                   "renamed or reverted to a field — the availability gate needs it to be a property");
            return;
        }

        var cats = ((IEnumerable)catsProp.GetValue(null)).Cast<object>().ToList();
        // Categories is (Key, Header, Entries): Item1 is the stable key (what AddMenuIcons and the overrides
        // are keyed by), Item2 the displayed header, Item3 the entries.
        var headers = cats.Select(c => (string)c.GetType().GetField("Item1").GetValue(c)).ToList();
        // Arcade is a child GROUP of the Radiata category, never a
        // top-level category — in EITHER gate state. The group's Hidden flag is what tracks availability.
        H.Check($"Arcade is not a top-level category ({when})",
                !headers.Contains("Arcade"), string.Join(" / ", headers));

        // ── The no-rewrite contract: arcade leaves must EXIST either way, hidden when not offered ──
        // A deleted entry is what makes SelectTypeOption fall to tab 0 and the next auto-save rewrite an
        // arcade slice's type outright (docs/ACTIONS.md). Hidden is the whole point; absent is a data-loss bug.
        int total = 0, hidden = 0;
        foreach (var cat in cats)
            foreach (var entry in ((IEnumerable)cat.GetType().GetField("Item3").GetValue(cat)).Cast<object>())
            {
                var t = entry.GetType();
                bool entryHidden = (bool)t.GetProperty("Hidden").GetValue(entry);
                foreach (var opt in ((IEnumerable)t.GetProperty("Options").GetValue(entry)).Cast<object>())
                {
                    if ((string)opt.GetType().GetProperty("Type").GetValue(opt) != "arcade") continue;
                    total++;
                    if (entryHidden) hidden++;
                }
            }

        // One picker leaf + one per catalogued game, both when offered and when hidden.
        int expected = 1 + ArcadeCatalog.Games.Length;
        H.Check($"arcade leaves still resolve ({when})", total == expected,
                $"{total} of {expected} — a MISSING leaf means an existing arcade slice's type gets rewritten on the next auto-save");
        H.Check($"arcade leaves {(offered ? "offered" : "hidden")} ({when})",
                hidden == (offered ? 0 : total), $"{hidden}/{total} hidden");

        // ── Help (drives the Help tab AND its search index) ──
        // EVERY arcade topic: the feature's own, one per game, and the package-authoring one, which names the
        // folder and the script API and so leaks the feature just as loudly. ⚠ A new game topic has to be
        // added HERE as well as to HelpContent.ArcadeTopicIds — that list is what the gate filters on, and a
        // topic missing from it survives into a build with the arcade compiled out.
        foreach (var id in (string[])["arcade", "arcade-kabloom", "arcade-connate", "arcade-petalpop",
                                      "arcade-internode", "custom-arcade-games"])
        {
            // The Internode and package-authoring topics are compiled off in a public-flavour build, so they are
            // absent there even when Arcade is offered.
            bool gatedOff = ReleaseGates.PublicRelease && (id == "arcade-internode" || id == "custom-arcade-games");
            bool topic = HelpContent.Topics.Any(t => t.Id == id);
            if (gatedOff && offered)
                H.Skip($"Help topic '{id}' present ({when})", "public-flavour build: the dev flavour's features are compiled off");
            else
                H.Check($"Help topic '{id}' {(offered ? "present" : "absent")} ({when})", topic == offered);

            // Localized topic sets are built from the same source, so a leak would show there too — and
            // that's the set the Help pane actually renders.
            bool esTopic = HelpLocalization.Topics("es").Any(t => t.Id == id);
            if (gatedOff && offered)
                H.Skip($"localized Help topic '{id}' present ({when})", "public-flavour build: the dev flavour's features are compiled off");
            else
                H.Check($"localized Help topic '{id}' {(offered ? "present" : "absent")} ({when})", esTopic == offered);
        }

        // ── The generated doc follows the BUILD const directly (CONTROLS.md is a generic artifact) ──
        if (!offered)
        {
            string md = HelpContent.ExportControlsMarkdown();
            H.Check("CONTROLS.md export still documents Arcade while concealed",
                    md.IndexOf("Arcade", StringComparison.OrdinalIgnoreCase) >= 0,
                    "the doc follows Arcade.Enabled, not the harness concealment of Arcade.Available");
        }
    }

    // ── The arcade catalog is actually WIRED to the renderers ───────────────────────────────────────

    /// <summary>Every <c>UiText.Arcade</c> const is named by code outside the catalog files.
    ///
    /// <para>The failure this exists for is invisible to every other gate: a renderer that interpolates the
    /// ENGLISH TEXT (<c>$"COMBO ×{n}"</c>) instead of looking the const up compiles, runs, and draws the
    /// right thing in English — while the const it shadows sits in the catalog collecting translations
    /// nobody ever sees. Connate's combo shout did exactly that, and only a hunt for unreferenced consts
    /// found it. An orphan left by a REMOVED feature trips this too, which is the same answer: delete it or
    /// wire it.</para>
    ///
    /// <para>Source text, not reflection: a const is inlined at every use site, so a compiled assembly has
    /// no record of which consts were named. The check reads the repo's own .cs files.</para></summary>
    private static void CatalogIsWired()
    {
        H.Group("Arcade — every catalog string is wired to something that draws it");

        var root = H.RepoRoot();
        if (root is null) { H.Skip("arcade catalog wiring", "repo root not found from the harness working directory"); return; }

        var arcade = typeof(UiText).GetNestedType("Arcade");
        if (arcade is null) { H.Fail("UiText.Arcade not found", "the arcade string group was renamed or removed"); return; }

        var names = arcade.GetFields(BindingFlags.Public | BindingFlags.Static)
                          .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                          .Select(f => f.Name).ToList();

        // Every .cs in the tree except the catalog itself and its translation maps — a const naming itself
        // in UiText.cs, or a translation keyed by its value, is not a use.
        var sources = T_Glyphs.SourceFiles(root)
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        && !System.IO.Path.GetFileName(p).StartsWith("UiText", StringComparison.Ordinal))
            .Select(System.IO.File.ReadAllText)
            .ToList();

        var orphans = names.Where(n => !sources.Any(s => s.Contains("Arcade." + n, StringComparison.Ordinal))).ToList();
        H.Check($"every UiText.Arcade const is referenced outside the catalog ({names.Count} strings)",
                orphans.Count == 0,
                orphans.Count == 0 ? null
                                   : $"{orphans.Count} unreferenced — either a renderer is drawing the English "
                                     + "literal instead of looking it up, or the feature is gone and the string "
                                     + "should be deleted with its translations: " + string.Join(", ", orphans.Take(8)));
    }
}
