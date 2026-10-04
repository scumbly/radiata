using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The arcade half of the render matrix (<see cref="T_Render"/>): every game in the catalog, each
/// driven by a scripted input sequence at the sim's fixed step and drawn through
/// <c>ArcadeControl.RenderCore</c> — bezel, playfield and the host's layers (ready beat, how-to card, pause menu,
/// confirm prompt, guard card, the △/START hint and the window-move cues) — then the Arcade Launcher at rest,
/// mid-swing with its floor sparks, and mid-launch / mid-return, and last the cabinet shots
/// <c>ArcadeShots.Capture</c> takes of each game.
///
/// <para>Every entry builds a fresh game and replays its script: the sims are deterministic by contract, and a
/// fresh game object is what puts Internode's wall-clock presentation smoothing on its first-frame path. Host
/// state is set on the control's fields directly — its own entry points play sounds. Drop-in script games are
/// out: they run in the jailed out-of-process helper, whose frame timing no pin reaches.</para></summary>
internal static class T_RenderArcade
{
    private const double Pad = 8;
    private static double Diameter => RadialMenuControl.OuterRadius * 2 * Math.Max(1, ArcadeTuning.GameDiscScale);
    private static double Canvas => Diameter + 2 * Pad;

    private static Type Control => H.AppType("ArcadeControl");
    private static Type Shots => H.AppType("ArcadeShots");

    /// <summary>Scripted input: a hold of the stick and buttons for a while; a d-pad press lands on the
    /// segment's first step, and a button press on the step it goes down.</summary>
    internal readonly record struct Seg(double Seconds, float X = 0, float Y = 0, bool Cross = false,
                                       bool Square = false, ArcadeInput.DPad DPad = ArcadeInput.DPad.None);

    internal static readonly Dictionary<string, Seg[]> Scripts = new(StringComparer.OrdinalIgnoreCase)
    {
        [Kabloom.GameId] =
        [
            new(0.5), new(0.05, Cross: true), new(1.6),
            new(0.05, DPad: ArcadeInput.DPad.Right), new(0.2), new(0.05, DPad: ArcadeInput.DPad.Right), new(0.2),
            new(0.05, DPad: ArcadeInput.DPad.Up), new(0.2), new(0.05, Square: true), new(0.6),
        ],
        [Connate.GameId] =
        [
            new(0.5), new(0.35, -0.5f, -0.8f, Cross: true), new(1.2), new(0.2, 0.7f, -0.2f, Cross: true),
            new(1.5), new(0.55, 0.2f, 0.9f, Cross: true), new(2.0, -0.3f, 0.4f),
        ],
        [PetalPop.GameId] =
        [
            new(0.4), new(0.05, Cross: true), new(0.9, 0.6f, 0f), new(0.9, -0.6f, 0.3f), new(0.3, 0f, 0f, Cross: true),
            new(1.2, 0.3f, -0.6f),
        ],
        [Internode.GameId] =
        [
            new(0.5), new(0.05, Cross: true), new(1.5, 0.6f, 0f), new(0.05, Cross: true), new(1.2, -0.5f, 0f), new(2.0),
            new(1.5, 0.8f, 0f), new(0.05, Cross: true), new(0.4), new(0.05, Cross: true), new(1.8, -0.7f, 0f), new(1.0),
        ],
    };

    /// <summary>Games whose run can end: an input pattern repeated until it does, and the most sim time to
    /// give it.</summary>
    internal static readonly Dictionary<string, (Seg[] Pattern, double CapSeconds)> GameOverPlans = new(StringComparer.OrdinalIgnoreCase)
    {
        // Start the run, then let every serve go by.
        [PetalPop.GameId] = ([new(0.05, Cross: true), new(2.0)], 900),
        // Lob as fast as the launcher allows, so the heap outgrows its limit.
        [Connate.GameId] = ([new(0.05, Cross: true), new(0.25)], 900),
    };

    public static IEnumerable<RenderEntry> Entries()
    {
        // What ArcadeControl.Open does before anything draws: compiled tuning, fresh sprite resolution.
        ArcadeTuning.LoadOverrides();
        H.InvokeStatic(H.AppType("ArcadeSprites"), "Reload");

        var ids = ArcadeCatalog.Games.Select(g => g.Id).ToArray();
        foreach (var id in ids)
        {
            string a = "arcade/" + id;
            bool howTo = Create(id).HowTo is not null;
            yield return E($"{a}/start", () => Game(id, played: false));
            yield return E($"{a}/play", () => Game(id));
            yield return E($"{a}/play-hint-seen", () => Game(id, c =>
            {
                T_Render.Set(c, "_howToSeen", true);
                Control.GetProperty("WindowPosition")!.SetValue(c, 0);
            }));
            yield return E($"{a}/ready", () => Game(id, c => T_Render.Set(c, "_readyLeft", ArcadeTuning.ResumeReadySeconds * 0.5)));
            if (howTo) yield return E($"{a}/howto", () => Game(id, c => T_Render.Set(c, "_howTo", true)));
            yield return E($"{a}/pause", () => Game(id, c => T_Render.Set(c, "_paused", true)));
            yield return E($"{a}/pause-last-row", () => Game(id, c =>
            {
                T_Render.Set(c, "_paused", true);
                T_Render.Set(c, "_musicOn", false);
                int rows = ((Array)Control.GetProperty("FixedRows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).Length
                         + ((System.Collections.IEnumerable)Control.GetProperty("PauseOptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!).Cast<object>().Count();
                T_Render.Set(c, "_pauseIndex", rows - 1);
            }));
            yield return E($"{a}/confirm", () => Game(id, c => Confirm(c, yes: false)));
            if (GameOverPlans.ContainsKey(id)) yield return E($"{a}/game-over", () => Game(id, gameOver: true));
        }

        yield return E("arcade/connate/confirm-yes", () => Game(Connate.GameId, c => Confirm(c, yes: true)));
        yield return E("arcade/connate/slide-cues-triggers-right", () => Game(Connate.GameId, c =>
        {
            T_Render.Set(c, "_howToSeen", true);
            Control.GetProperty("WindowPosition")!.SetValue(c, 2);
            Control.GetProperty("SlideOnTriggers")!.SetValue(c, true);
        }));
        yield return E("arcade/guard/cloak-failed-holding", () => Game(Connate.GameId, c =>
        {
            var copy = Arcade.Guard(Arcade.Block.CloakFailed, "Some Game");
            T_Render.Set(c, "_guard", copy);
            T_Render.Set(c, "_guardOverridable", copy.Overridable);
            T_Render.Set(c, "_guardHeld", 0.4);
        }));
        yield return E("arcade/guard/passthru", () => Game(Kabloom.GameId, c =>
        {
            var copy = Arcade.Guard(Arcade.Block.SafeMode, null);
            T_Render.Set(c, "_guard", copy);
            T_Render.Set(c, "_guardOverridable", copy.Overridable);
        }));

        // ── The Arcade Launcher ──
        int count = ArcadeCatalog.Games.Length;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            yield return E($"arcade/launcher/front-{ArcadeCatalog.Games[i].Id}", () => Launcher(index));
        }
        yield return E("arcade/launcher/swing-and-sparks", () => Launcher(0, (c, carousel) =>
        {
            T_Render.Set(carousel, "_rng", new Random(T_Render.FxSeed));
            Frame(c, capture: false);                                  // the carousel learns its field size
            for (int k = 0; k < 90; k++) T_Render.Call(carousel, "Step", 1.0 / 60, false);
            T_Render.Set(c, "_pickerIndex", 1);
            T_Render.Call(carousel, "Rotate", 1);
            for (int k = 0; k < 6; k++) T_Render.Call(carousel, "Step", 1.0 / 60, false);
        }));
        yield return E("arcade/launcher/departure-mid", () => Launcher(1, (c, _) =>
        {
            T_Render.Set(c, "_launchEntry", ArcadeCatalog.Games[1]);
            T_Render.Set(c, "_launchLeft", ArcadePickerTuning.LaunchSeconds * 0.5);
            double from = 1 / Math.Max(1, ArcadeTuning.GameDiscScale);
            T_Render.Call(c, "SetDisc", from + (1 - from) * 0.5);
        }));
        yield return E("arcade/launcher/arrival-mid", () => Launcher(2, (c, _) =>
        {
            T_Render.Set(c, "_arriveShot", H.InvokeStatic(Shots, "Get", ArcadeCatalog.Games[2].Id));
            T_Render.Set(c, "_arriveLeft", ArcadeTuning.DiscShrinkSeconds * 0.5);
        }));
        // The sample package lives only in the development repository; the public snapshot omits the entry.
        if (H.IsDevRepo(T_Render.RepoRoot()))
            yield return E("arcade/launcher/drop-in-cabinet", LauncherWithFirefly);

        // ── Cabinet shots: ArcadeShots.Capture of a played board. LAST: capturing replaces the bundled still in
        // the shot cache, which the launcher and the wheel's arcade hub would otherwise read. ──
        foreach (var id in ids)
            yield return E($"arcade/{id}/cabinet-shot", () =>
            {
                var game = Create(id);
                Play(game, Scripts[id]);
                H.InvokeStatic(Shots, "Capture", id, Renderer(id), game);
                var shot = H.InvokeStatic(Shots, "Get", id)!;
                var img = (System.Windows.Media.Imaging.BitmapSource)shot.GetType().GetProperty("Image")!.GetValue(shot)!;
                H.InvokeStatic(Shots, "WaitPending", 5000);
                return T_Render.FromBitmap(img);
            });
    }

    private static RenderEntry E(string name, Func<RenderedFrame> render) => new(name, render);

    internal static IArcadeGame Create(string id) =>
        ArcadeCatalog.Create(id) ?? throw new InvalidOperationException($"no game '{id}'");

    internal static object Renderer(string id) =>
        H.InvokeStatic(H.AppType("ArcadeRenderers"), "For", id) ?? throw new InvalidOperationException($"no renderer for '{id}'");

    /// <summary>Step the sim through <paramref name="segs"/> at its fixed step; <paramref name="stopAtGameOver"/>
    /// stops on the step the run ends, before any further press could start a new one.</summary>
    internal static void Play(IArcadeGame g, Seg[] segs, bool stopAtGameOver = false)
    {
        double dt = ArcadeTuning.StepSeconds;
        bool cross = false, square = false;
        foreach (var s in segs)
        {
            int steps = Math.Max(1, (int)Math.Round(s.Seconds / dt));
            for (int k = 0; k < steps; k++)
            {
                if (stopAtGameOver && g.IsGameOver) return;
                g.Step(new ArcadeInput(s.X, s.Y, s.Cross, false, s.Square,
                                       s.Cross && !cross, false, s.Square && !square,
                                       k == 0 ? (int)s.DPad : 0), dt);
                cross = s.Cross; square = s.Square;
            }
        }
    }

    /// <summary>Repeat the game's plan until its run ends, then give the game-over screen a second to settle.</summary>
    private static void PlayToGameOver(IArcadeGame g)
    {
        var (pattern, capSeconds) = GameOverPlans[g.Id];
        double spent = 0, per = pattern.Sum(s => s.Seconds);
        while (!g.IsGameOver && spent < capSeconds) { Play(g, pattern, stopAtGameOver: true); spent += per; }
        if (!g.IsGameOver) throw new InvalidOperationException($"{g.Id} did not reach game over within {capSeconds} s");
        Play(g, [new Seg(1.0)]);
    }

    internal static object NewControl()
    {
        var c = T_Render.Unscaled((FrameworkElement)Activator.CreateInstance(Control)!);
        c.Width = c.Height = Diameter;
        c.Measure(new Size(Diameter, Diameter));
        c.Arrange(new Rect(0, 0, Diameter, Diameter));
        return c;
    }

    /// <summary>One game in the round window. <paramref name="played"/> false draws the fresh board.</summary>
    internal static RenderedFrame Game(string id, Action<object>? state = null, bool played = true, bool gameOver = false)
    {
        var game = Create(id);
        if (gameOver) PlayToGameOver(game);
        else if (played) Play(game, Scripts[id]);
        var c = NewControl();
        T_Render.Set(c, "_game", game);
        T_Render.Set(c, "_renderer", Renderer(id));
        T_Render.Set(c, "_howToSeen", false);
        T_Render.Set(c, "_musicOn", true);
        state?.Invoke(c);
        return Frame(c, capture: true);
    }

    /// <summary>The Reset row's prompt, as the pause menu raises it (NO highlighted unless <paramref name="yes"/>).</summary>
    private static void Confirm(object c, bool yes)
    {
        T_Render.Set(c, "_paused", true);
        T_Render.Set(c, "_confirmPrompt", Loc.T(UiText.Arcade.EndsRun));
        T_Render.Set(c, "_confirmKey", "host:reset");
        T_Render.Set(c, "_confirmYes", yes);
    }

    private static RenderedFrame Launcher(int index, Action<object, object>? state = null)
    {
        var games = ArcadeCatalog.Games;
        var c = NewControl();
        H.InvokeStatic(Shots, "Preload", games.Select(g => g.Id));
        T_Render.Set(c, "_picker", true);
        T_Render.Set(c, "_pickerIndex", index);
        var carousel = T_Render.Get(c, "_carousel")!;
        T_Render.Call(carousel, "Reset", index, games.Length);
        T_Render.Call(c, "SetDisc", 1 / Math.Max(1, ArcadeTuning.GameDiscScale));
        state?.Invoke(c, carousel);
        return Frame(c, capture: true);
    }

    /// <summary>The launcher with the Workshop's sample game registered and in front — a drop-in cabinet's own
    /// tint, preview and badge. Unregistered again before anything else renders.</summary>
    private static RenderedFrame LauncherWithFirefly()
    {
        var root = T_Render.RepoRoot();
        var ff = PackageStore.Inspect(Path.Combine(root, "packaging", "sample-arcade-package", "firefly"), "arcade");
        if (ff.ArcadeGame is null) throw new InvalidOperationException($"firefly sample: {ff.Error}");
        ArcadeCatalog.RegisterScripts([ff.ArcadeGame]);
        try
        {
            int index = Array.FindIndex(ArcadeCatalog.Games, g => g.Id == ff.ArcadeGame.Token);
            return Launcher(index);
        }
        finally { ArcadeCatalog.RegisterScripts([]); }
    }

    internal static RenderedFrame Frame(object c, bool capture)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(Pad, Pad));
            T_Render.Method(Control, "RenderCore").Invoke(c, [dc]);
            dc.Pop();
        }
        return capture ? T_Render.Capture(dv, Canvas, Canvas, T_RenderWheel.DpiScale) : null!;
    }
}
