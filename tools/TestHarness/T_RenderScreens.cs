using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The arcade games' text screens, one PNG each (<c>render:screens &lt;dir&gt;</c>): titles, end cards,
/// shouts and notices, drawn through the same <c>ArcadeControl.RenderCore</c> path and at the same size as the
/// render matrix (<see cref="T_RenderArcade"/>). A separate command, never part of the matrix: the matrix is a
/// pixel-identity baseline and these entries must not enter it.
///
/// <para>Each screen is a real sim driven to its state with scripted input; where the state is otherwise a
/// long play away (a won campaign, a level-26 card) the game's own fields are set by reflection. Host state
/// is set on the control's fields directly — its own entry points play sounds.</para>
///
/// <para>⚠ The arcade's Share Tech face is a <c>pack://</c> font URI, which the harness process resolves to the
/// fallback family, so <see cref="UseRealFont"/> replaces the cached typeface with one loaded from the repo's
/// <c>Assets\fonts</c> folder before anything draws.</para></summary>
internal static class T_RenderScreens
{
    private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool Handles(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "render:screens", StringComparison.OrdinalIgnoreCase);

    public static void Run(string[] args)
    {
        if (args.Length < 2) { H.Fail("render:screens", "usage: render:screens <dir>"); return; }
        H.Group("render:screens — the arcade text screens");
        string dir = Path.GetFullPath(args[1]);
        if (!T_Render.Prepare()) return;
        if (!UseRealFont()) return;
        // What ArcadeControl.Open does before anything draws: compiled tuning, fresh sprite resolution.
        ArcadeTuning.LoadOverrides();
        H.InvokeStatic(H.AppType("ArcadeSprites"), "Reload");

        int written = 0;
        foreach (var (name, render) in Screens())
        {
            try
            {
                T_Render.PinClocks();
                var frame = render();
                T_Render.Drain();
                string png = T_Render.PngPath(dir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(png)!);
                File.WriteAllBytes(png, T_Render.EncodePng(frame));
                written++;
                H.Pass(name);
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
                H.Fail(name, $"render threw {inner.GetType().Name}: {inner.Message}");
                try { T_Render.Drain(); } catch { }
            }
        }
        H.StaticField(typeof(RadialMenuControl), "PinnedClockUtc")!.SetValue(null, null);
        H.StaticField(H.AppType("ArcadeSprites"), "PinnedTime")!.SetValue(null, null);
        H.Check($"render:screens wrote {written} screens", written > 0, dir);
    }

    /// <summary>Point <c>ArcadeChrome</c>'s cached typeface at the repo's Share Tech file, keeping its fallback
    /// list.</summary>
    private static bool UseRealFont()
    {
        string fonts = Path.Combine(T_Render.RepoRoot(), "Assets", "fonts") + Path.DirectorySeparatorChar;
        var face = new Typeface(new FontFamily(new Uri(fonts, UriKind.Absolute), "./#Share Tech, Segoe UI, Tahoma, Arial"),
                                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!face.TryGetGlyphTypeface(out var glyphs) || !glyphs.FamilyNames.Values.Any(n => n.Contains("Share Tech")))
        {
            H.Fail("render:screens: Share Tech loads from Assets\\fonts", "the typeface resolved to a fallback family");
            return false;
        }
        H.StaticField(H.AppType("ArcadeChrome"), "_face")!.SetValue(null, face);
        return true;
    }

    private static IEnumerable<(string Name, Func<RenderedFrame> Render)> Screens()
    {
        // ── Kabloom ──
        yield return ("kabloom/title", () => T_RenderArcade.Game(Kabloom.GameId, played: false));
        yield return ("kabloom/stung", () => Show(StungBoard(), hint: true));
        yield return ("kabloom/growth-notice", () => Show(GrowthBoard(), hint: true));
        yield return ("kabloom/capacity-card", () => Show(CapacityBoard(), hint: true));
        yield return ("kabloom/campaign-complete", () => Show(CompleteCampaign(), hint: true));

        // ── Connate ──
        yield return ("connate/title", () => T_RenderArcade.Game(Connate.GameId, played: false));
        yield return ("connate/game-over", () => T_RenderArcade.Game(Connate.GameId, gameOver: true));
        yield return ("connate/combo", () => Show(Played(Connate.GameId, g =>
        {
            SetProp(g, "ComboCount", 3);
            SetProp(g, "ComboDisplayLeft", ConnateTuning.ComboDisplaySeconds * 0.7);
        }), hint: true));
        yield return ("connate/board-clear", () => Show(Played(Connate.GameId, g =>
        {
            SetProp(g, "BoardClearLeft", ConnateTuning.BoardClearDisplaySeconds * 0.7);
            SetProp(g, "BoardClearBonus", 1200L);
        }), hint: true));

        // ── PetalPop ──
        yield return ("petalpop/title", () => T_RenderArcade.Game(PetalPop.GameId, played: false));
        yield return ("petalpop/game-over", () => T_RenderArcade.Game(PetalPop.GameId, gameOver: true));
        yield return ("petalpop/win", () => Show(PetalPopWin(), hint: true));
        yield return ("petalpop/shout-combo", () => Show(PetalPopShout(PetalPop.ShoutKind.Combo, 5, 0), hint: true));
        yield return ("petalpop/shout-extra-life", () => Show(PetalPopShout(PetalPop.ShoutKind.ExtraLife, 5, 1), hint: true));
        yield return ("petalpop/shout-level", () => Show(PetalPopShout(PetalPop.ShoutKind.Level, 4, 2), hint: true));

        // ── Internode ──
        yield return ("internode/title", () => T_RenderArcade.Game(Internode.GameId, played: false));
        yield return ("internode/shout-pass", () => Show(InternodeShouted(InternodeShout.Pass, 0), hint: true));
        yield return ("internode/shout-stage", () => Show(InternodeShouted(InternodeShout.Stage, 3), hint: true));
        yield return ("internode/shout-short-by", () => Show(InternodeShouted(InternodeShout.ShortBy, 2), hint: true));
        yield return ("internode/shout-ouch", () => Show(InternodeShouted(InternodeShout.Ouch, 0), hint: true));
        yield return ("internode/gate-levelup", () => Show(Played(Internode.GameId, g =>
        {
            SetProp(g, "GateNotice", InternodeGateNotice.LevelUp);
            SetProp(g, "GateNoticeAge", InternodeTuning.GateNoticeSeconds * 0.5);
        }), hint: true));
        yield return ("internode/gate-checkpoint", () => Show(Played(Internode.GameId, g =>
        {
            SetProp(g, "GateNotice", InternodeGateNotice.Checkpoint);
            SetProp(g, "GateNoticeAge", InternodeTuning.GateNoticeSeconds * 0.5);
        }), hint: true));
        yield return ("internode/stage-demoted", () => Show(Played(Internode.GameId, g =>
        {
            SetProp(g, "StageNotice", InternodeStageNotice.Demoted);
            SetProp(g, "StageNoticeAge", InternodeTuning.StageNoticeSeconds * 0.5);
        }), hint: true));
        yield return ("internode/stage-promoted", () => Show(Played(Internode.GameId, g =>
        {
            SetProp(g, "StageNotice", InternodeStageNotice.Promoted);
            SetProp(g, "StageNoticeAge", InternodeTuning.StageNoticeSeconds * 0.5);
        }), hint: true));
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────────────

    private static void SetProp(object target, string name, object value) =>
        (target.GetType().GetProperty(name, Inst) ?? throw new MissingMemberException(target.GetType().Name, name))
            .SetValue(target, value);

    /// <summary>The game in the round window, through the same host wiring as the matrix's <c>Game</c>.
    /// <paramref name="hint"/> false leaves the △/START how-to hint up, as a fresh session shows it.</summary>
    private static RenderedFrame Show(IArcadeGame game, bool hint)
    {
        var c = T_RenderArcade.NewControl();
        T_Render.Set(c, "_game", game);
        T_Render.Set(c, "_renderer", T_RenderArcade.Renderer(game.Id));
        T_Render.Set(c, "_howToSeen", hint);
        T_Render.Set(c, "_musicOn", true);
        return T_RenderArcade.Frame(c, capture: true);
    }

    /// <summary>The game after its matrix "play" script, with <paramref name="state"/> applied on top.</summary>
    private static IArcadeGame Played(string id, Action<IArcadeGame> state)
    {
        var g = T_RenderArcade.Create(id);
        T_RenderArcade.Play(g, T_RenderArcade.Scripts[id]);
        state(g);
        return g;
    }

    private static readonly T_RenderArcade.Seg[] PressCross = [new(0.05, Cross: true)];

    // ── Kabloom ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Plant a board with a first reveal and let its cascade finish.</summary>
    private static Kabloom Planted(Kabloom k)
    {
        T_RenderArcade.Play(k, PressCross);
        double spent = 0;
        while (k.Phase != Kabloom.Stage.Playing && spent < 120)
        {
            T_RenderArcade.Play(k, [new(0.25)]);
            spent += 0.25;
        }
        if (k.Phase != Kabloom.Stage.Playing) throw new InvalidOperationException("Kabloom did not plant a board");
        T_RenderArcade.Play(k, [new(1.5)]);
        return k;
    }

    private static Kabloom StungBoard()
    {
        var k = Planted(new Kabloom());
        int mine = Enumerable.Range(0, k.Grid.Cells.Count).First(i => k.Board.IsMine(i) && !k.Board.IsRevealed(i));
        SetProp(k, "FocusCell", mine);
        T_Render.Call(k, "SyncPointerToFocus");
        T_RenderArcade.Play(k, PressCross);
        T_RenderArcade.Play(k, [new(KabloomTuning.StungBeeFlightSeconds + 2.5)]);
        if (k.Phase != Kabloom.Stage.Failed || !k.OutcomePromptsReady)
            throw new InvalidOperationException($"Kabloom is {k.Phase}, prompts ready {k.OutcomePromptsReady}");
        return k;
    }

    /// <summary>The first level whose profile widens the crop and crowds it, with no new bee capacity to put a
    /// card over the banner; it is loaded as a level change and stepped to the banner's held half.</summary>
    private static Kabloom GrowthBoard()
    {
        int level = Enumerable.Range(2, Kabloom.CampaignLevels - 1).FirstOrDefault(l =>
        {
            var a = KabloomLevelProfile.ForLevel(l - 1);
            var b = KabloomLevelProfile.ForLevel(l);
            return b.TargetCells > a.TargetCells && b.MineOccupancy > a.MineOccupancy + 1e-9 && b.BeeCapacity == a.BeeCapacity;
        });
        if (level == 0) throw new InvalidOperationException("no level grows both axes without a capacity change");
        var k = new Kabloom();
        T_Render.Call(k, "LoadLevel", level - 1, false, false);
        T_Render.Call(k, "LoadLevel", level, true, true);
        T_RenderArcade.Play(k, [new(KabloomTuning.GrowthNoticeSeconds * 0.4)]);
        if (!k.GrowthShowsCells || !k.GrowthShowsMines)
            throw new InvalidOperationException("the growth banner is not up");
        return k;
    }

    /// <summary>The "bees share petals" card, reached the way the pause menu's starting size reaches it, then
    /// read long enough to unlock its continue prompt.</summary>
    private static Kabloom CapacityBoard()
    {
        var k = new Kabloom();
        k.ApplyPauseOption("start", 1);
        T_RenderArcade.Play(k, [new(KabloomTuning.CapacityNoticeMinSeconds + 0.5)]);
        if (k.CapacityNotice == 0 || !k.CapacityNoticeReady)
            throw new InvalidOperationException("the capacity card is not up and ready");
        return k;
    }

    /// <summary>The last level of the campaign with every safe petal opened.</summary>
    private static Kabloom CompleteCampaign()
    {
        var k = new Kabloom();
        T_Render.Call(k, "LoadLevel", Kabloom.CampaignLevels, false, false);
        // Planted directly: the runtime generator's search on the last, largest crop runs far past a render's
        // budget, and the finished board's bee layout is not what the card shows.
        var near = k.Grid.Cells[k.FocusCell].Neighbors.Append(k.FocusCell).ToHashSet();
        k.Board.Plant(Enumerable.Range(0, k.Grid.Cells.Count).Where(i => i % 7 == 3 && !near.Contains(i)), k.FocusCell);
        T_Render.Call(k, "SetPhase", Kabloom.Stage.Playing);
        k.Board.Reveal(k.FocusCell);
        object last = null;
        int lastCell = -1;
        for (int i = 0; i < k.Grid.Cells.Count && k.Board.Phase == KabloomBoardPhase.Playing; i++)
        {
            if (k.Board.IsMine(i) || k.Board.IsRevealed(i)) continue;
            last = k.Board.Reveal(i);
            lastCell = i;
        }
        if (k.Board.Phase != KabloomBoardPhase.Cleared || last is null)
            throw new InvalidOperationException("the board did not clear");
        T_Render.Call(k, "Act", last, (int?)lastCell);
        T_RenderArcade.Play(k, [new(8)]);
        if (k.Phase != Kabloom.Stage.CampaignComplete || !k.OutcomePromptsReady)
            throw new InvalidOperationException($"Kabloom is {k.Phase}, prompts ready {k.OutcomePromptsReady}");
        return k;
    }

    // ── PetalPop ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The 8-8 finish: the last stage's geometry and level, the run's end phase.</summary>
    private static PetalPop PetalPopWin()
    {
        var p = (PetalPop)T_RenderArcade.Create(PetalPop.GameId);
        T_RenderArcade.Play(p, T_RenderArcade.Scripts[PetalPop.GameId]);
        SetProp(p, "Sides", 8);
        T_Render.Call(p, "ConfigureSides");
        SetProp(p, "Level", 8);
        SetProp(p, "Score", 48250L);
        T_Render.Call(p, "SetPhase", PetalPop.Stage.Won);
        T_RenderArcade.Play(p, [new(1.5)]);
        if (p.Phase != PetalPop.Stage.Won) throw new InvalidOperationException($"PetalPop is {p.Phase}");
        return p;
    }

    /// <summary>A shout held past its pop-in and before its fade.</summary>
    private static PetalPop PetalPopShout(PetalPop.ShoutKind kind, int value, int value2)
    {
        var p = (PetalPop)T_RenderArcade.Create(PetalPop.GameId);
        T_RenderArcade.Play(p, T_RenderArcade.Scripts[PetalPop.GameId]);
        SetProp(p, "Shout", kind);
        SetProp(p, "ShoutValue", value);
        SetProp(p, "ShoutValue2", value2);
        SetProp(p, "ShoutLeft", PetalPopTuning.ShoutSeconds * 0.65);
        return p;
    }

    // ── Internode ────────────────────────────────────────────────────────────────────────────────────

    private static Internode InternodeShouted(InternodeShout kind, int value) =>
        (Internode)Played(Internode.GameId, g =>
        {
            SetProp(g, "Shout", kind);
            SetProp(g, "ShoutValue", value);
            SetProp(g, "ShoutLeft", InternodeTuning.ShoutSeconds * 0.65);
        });
}
