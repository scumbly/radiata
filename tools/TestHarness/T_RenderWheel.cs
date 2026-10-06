using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The wheel half of the render matrix (<see cref="T_Render"/>): every material — the eight built-ins
/// plus both sample drop-in themes — through every hub and slice state, the count × thickness grid, the
/// Reduce Motion frames that differ, the clock-driven effects at pinned instants, and the static preview
/// bakes the Settings tiles and icon well use.
///
/// <para>Each entry builds a fresh <see cref="RadialMenuControl"/> laid out at the overlay's 430 DIP box, applies
/// everything the resting-slice bake keys on, pre-warms the bakes the way a parked wheel does, sets the state
/// through the control's own API (reflection only where no API exists: the state machine's dwell tick, the
/// collapse tick, the circuit-spark tick), and draws through <c>OnRender</c> onto a canvas padded to hold
/// everything that reaches past the box.</para></summary>
internal static class T_RenderWheel
{
    public const double Box = 430, Pad = 110, Canvas = Box + 2 * Pad;
    public static double DpiScale { get; private set; } = 1;

    private static readonly string[] Counts = ["n4", "n8", "n12"];
    private static readonly string[] Thicknesses = ["thick", "medium", "thin"];

    private static string[] _materials = [];
    private static string _root = "";

    /// <summary>The materials whose guarded dwell, armed treatment or hub differ under Reduce Motion.</summary>
    private static readonly string[] OwnDwell = ["pearl", "obsidian", "kawaii", "salvage", "mesa", "reactor"];

    public static bool Init()
    {
        _root = T_Render.RepoRoot();
        var packages = new List<MaterialPackage>();
        foreach (var name in new[] { "starter", "ember" })
        {
            var d = PackageStore.Inspect(Path.Combine(_root, "packaging", "sample-material-package", name), "material");
            if (d.Material is null) { H.Fail($"render: sample material '{name}' loads", d.Error); return false; }
            packages.Add(d.Material);
        }
        Materials.RegisterCustom(packages);
        _materials = [.. Materials.All, .. packages.Select(p => p.Token)];

        var probe = NewControl();
        DpiScale = VisualTreeHelper.GetDpi(probe).DpiScaleX;
        return true;
    }

    private static string Asset(string rel) => Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));

    // ── The matrix ────────────────────────────────────────────────────────────────────────────────────

    public static IEnumerable<RenderEntry> Entries()
    {
        foreach (var mat in _materials)
        {
            string m = "wheel/" + mat;

            // Every count × thickness, at rest.
            foreach (var n in Counts)
                foreach (var t in Thicknesses)
                    yield return E($"{m}/rest/{n}-{t}", () => Wheel(mat, Count(n), t, w => w.Steady()));

            // The same armed slice at every thickness; then the other kinds of content armed.
            foreach (var t in Thicknesses)
                yield return E($"{m}/armed-icon/n8-{t}", () => Wheel(mat, 8, t, w => { w.Arm(0); return w.Steady(); }));
            yield return E($"{m}/armed-logo", () => Wheel(mat, 8, "medium", w => { w.Arm(1); return w.Steady(); }));
            yield return E($"{m}/armed-logo-labelled", () => Wheel(mat, 12, "medium", w => { w.Arm(10); return w.Steady(); }, labels: "all"));
            yield return E($"{m}/armed-long-label", () => Wheel(mat, 4, "thin", w => { w.Arm(2); return w.Steady(); }));
            yield return E($"{m}/armed-cover", () => Wheel(mat, 12, "thick", w => { w.Arm(5); return w.Steady(); }));

            // Label modes.
            yield return E($"{m}/rest-labels-all", () => Wheel(mat, 12, "medium", w => w.Steady(), labels: "all"));
            yield return E($"{m}/rest-labels-none", () => Wheel(mat, 8, "medium", w => w.Steady(), labels: "none"));

            // Hold to confirm: mid-dwell (13 ticks of 30/800 = 48.75%) and complete.
            yield return E($"{m}/guard-dwell", () => Wheel(mat, 8, "medium", w => { w.Arm(3); w.Tick(13); return w.Steady(); }));
            yield return E($"{m}/guard-done", () => Wheel(mat, 8, "medium", w => { w.Arm(3); w.Tick(27); return w.Steady(); }));

            // Hub readouts.
            foreach (var t in Thicknesses)
                yield return E($"{m}/status/n8-{t}", () => Wheel(mat, 8, t, w => { w.C.ShowStatus("Mute", "On"); return w.Steady(); }));
            yield return E($"{m}/status-preview", () => Wheel(mat, 8, "medium", w =>
            {
                w.Arm(0);
                w.C.ShowStatus("Mute", "Off", "On");
                return w.Steady();
            }));
            yield return E($"{m}/status-caption", () => Wheel(mat, 8, "medium", w =>
                { w.C.ShowStatus("Volume", "Volume", caption: "was selected"); return w.Steady(); }));
            yield return E($"{m}/status-long", () => Wheel(mat, 8, "thick", w =>
                { w.C.ShowStatus("Output", "Speakers (Realtek High Definition Audio)"); return w.Steady(); }));
            yield return E($"{m}/status-coldstart", () => Wheel(mat, 8, "medium", w =>
                { w.C.ShowStatus("Steam", "Launching", icon: Logo()); return w.Steady(); }));
            yield return E($"{m}/scrubber", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.ScrubberActive = true;
                w.C.ScrubberLevel = 0.62f;
                w.C.ScrubberGlyph = "VolumeHigh";
                return w.Steady();
            }));
            yield return E($"{m}/hub-glyph", () => Wheel(mat, 8, "thin", w => { w.C.HubGlyph = "SkipForward"; return w.Steady(); }));
            foreach (var t in Thicknesses)
                yield return E($"{m}/notice/n8-{t}", () => Wheel(mat, 8, t, w =>
                    { w.C.ShowNotice("Configure this slice in Settings"); return w.Steady(); }));
            yield return E($"{m}/practice", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.ShowNotice("Practice", RadialMenuControl.NoticeKind.Practice);
                w.C.SetOnboardingPracticeArt(true);
                return w.Steady();
            }));
            yield return E($"{m}/practice-subtitle", () => Wheel(mat, 8, "thin", w =>
            {
                w.C.ShowNotice("Practice|While Settings is Open", RadialMenuControl.NoticeKind.Practice);
                w.Arm(2);
                return w.Steady();
            }));
            yield return E($"{m}/edit-hint", () => Wheel(mat, 8, "medium", w =>
                { w.C.ShowNotice("Click R3 to edit this wheel", RadialMenuControl.NoticeKind.EditHint); return w.Steady(); }));
            yield return E($"{m}/hub-battery", () => Wheel(mat, 8, "medium", w =>
                { w.C.SetBattery(55, false); return w.Steady(); }, alwaysHub: true));
            yield return E($"{m}/mix-balance", () => Wheel(mat, 8, "medium", w =>
                { w.C.ShowMixIndicator(0.3, "Speakers", "Headphones (Wireless)"); return w.Steady(); }));

            // Arcade slices armed: a game's shot in the hub, and the launcher's cabinet.
            yield return E($"{m}/arcade-game-armed", () => Wheel(mat, 8, "medium", w => { w.Arm(4); return w.Steady(); }));
            yield return E($"{m}/arcade-launcher-armed", () => Wheel(mat, 8, "thin", w => { w.Arm(6); return w.Steady(); }));

            // A fire: the chosen slice lingering in its own layer while the ring and hub fade.
            yield return E($"{m}/firing", () => Wheel(mat, 8, "medium", w =>
            {
                w.Arm(0);
                w.C.MarkFiringSlice(celebrate: false);
                w.C.RingOpacity = 0.35;
                w.C.SelectedSliceOpacity = 0.8;
                w.C.CenterOpacity = 0.6;
                return w.Steady();
            }));

            // The arcade collapse, held: slices gone, hub grown to the playfield (the Reduce Motion route).
            yield return E($"{m}/collapse-held", () => Wheel(mat, 8, "medium", w =>
            {
                w.Arm(4);
                w.C.BeginArcadeCollapse(4, 300, () => { });
                return w.Steady();
            }, reduceMotion: true));

            // In-wheel edit mode.
            foreach (var t in Thicknesses)
                yield return E($"{m}/edit-select/n8-{t}", () => Wheel(mat, 8, t, w =>
                {
                    w.C.BeginEdit(w.C.Slices);
                    w.Arm(2);
                    return w.Steady();
                }, prewarm: false));
            yield return E($"{m}/edit-full-undo", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.EditMaxSlices = 8;
                w.C.BeginEdit(w.C.Slices);
                w.Arm(2);
                WithReducedMotion(() => w.C.EditNudge(1));
                return w.Steady();
            }, prewarm: false));
            yield return E($"{m}/edit-carry", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.BeginEdit(w.C.Slices);
                w.Arm(2);
                w.C.EditPickUp();
                return w.Steady();
            }, prewarm: false));
            yield return E($"{m}/edit-carry-moved", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.BeginEdit(w.C.Slices);
                w.Arm(2);
                w.C.EditPickUp();
                WithReducedMotion(() => w.Aim(5, 8));
                return w.Steady();
            }, prewarm: false));
            yield return E($"{m}/edit-add", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.BeginEdit(w.C.Slices);
                var added = Populate([new WheelSlice { Label = "New Slice", IconName = "Star", Action = new ActionConfig { Type = "url", Url = "https://example.invalid" } }], mat);
                w.C.EditAddAndCarry(added[0]);
                return w.Steady();
            }, prewarm: false));
            yield return E($"{m}/edit-delete-hold", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.BeginEdit(w.C.Slices);
                w.Arm(2);
                w.C.EditSetDeleteHeld(true);
                w.Tick(8);   // 8 × 30/500 = 48%
                return w.Steady();
            }, prewarm: false));
            yield return E($"{m}/edit-ghost", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.BeginEdit(w.C.Slices);
                w.Arm(2);
                w.C.EditSetDeleteHeld(true);
                // The state machine's clock stays pinned from here, so the reflow the deletion starts reads
                // progress 0 — the frame the deletion lands on, ghost at full strength.
                T_Render.Set(w.Sm, "PinnedClockMs", (long?)0);
                w.Tick(17);   // the dwell completes on tick 17 and the slice is deleted
                return w.Steady();
            }, prewarm: false));
            yield return E($"{m}/edit-picker", () => Wheel(mat, 8, "medium", w =>
            {
                w.C.BeginEdit(w.C.Slices);
                w.C.SetPicker(Populate(PickerMenu(), mat));
                w.Arm(1, 5);
                return w.Steady();
            }, prewarm: false));

            // Reduce Motion, where it changes a still frame: every material with its own dwell falls back to
            // the standard arc, and the armed decorations hold still.
            if (OwnDwell.Contains(mat))
                yield return E($"{m}/guard-dwell-reduced", () => Wheel(mat, 8, "medium", w => { w.Arm(3); w.Tick(13); return w.Steady(); }, reduceMotion: true));
            if (mat is "kawaii" or "salvage" or "reactor")
                yield return E($"{m}/armed-icon-reduced", () => Wheel(mat, 8, "medium", w => { w.Arm(0); return w.Steady(); }, reduceMotion: true));
        }

        // ── Clock-driven effects at pinned instants ──
        foreach (var mat in new[] { "pearl", "salvage", "kawaii" })
            yield return E($"wheel/{mat}/hub-appear-mid", () => Wheel(mat, 8, "medium", w =>
            {
                w.Frame(false);                                   // first frame: no hub
                w.C.ShowNotice("Configure this slice in Settings");
                w.Frame(false);                                   // the appear starts on the pinned clock
                T_Render.AdvanceClock(85);                        // half of the 170 ms appear
                return w.Frame(true);
            }));
        yield return E("wheel/obsidian/hub-disappear-mid", () => Wheel("obsidian", 8, "medium", w =>
        {
            w.C.ShowNotice("Configure this slice in Settings");
            w.Frame(false);
            w.C.ClearStatus();
            w.Frame(false);                                       // the shrink starts, replaying the snapshot
            T_Render.AdvanceClock(40);
            return w.Frame(true);
        }));
        foreach (var mat in new[] { "pearl", "mesa", "reactor" })
            foreach (var ms in new[] { 90, 220 })
                yield return E($"wheel/{mat}/collapse-{ms}ms", () => Wheel(mat, 8, "medium", w =>
                {
                    w.Arm(4);
                    w.C.BeginArcadeCollapse(4, 300, () => { });
                    for (int k = 0; k < ms / 10; k++)
                    {
                        T_Render.AdvanceClock(10);
                        T_Render.Call(w.C, "CollapseTick", null, EventArgs.Empty);
                    }
                    return w.Steady();
                }));
        yield return E("wheel/kawaii/armed-glitter-150ms", () => Wheel("kawaii", 8, "medium", w =>
        {
            w.Arm(0);
            w.Frame(false);                                       // the sweep starts on this frame
            T_Render.AdvanceClock(150);
            return w.Frame(true);
        }));
        yield return E("wheel/kawaii/guard-dwell-slosh-t+700ms", () => Wheel("kawaii", 8, "medium", w =>
        {
            w.Arm(3); w.Tick(20);
            T_Render.AdvanceClock(700);
            return w.Steady();
        }));
        yield return E("wheel/salvage/armed-icon-t+400ms", () => Wheel("salvage", 8, "medium", w =>
        {
            w.Arm(0);
            T_Render.AdvanceClock(400);
            return w.Steady();
        }));
        yield return E("wheel/reactor/guard-dwell-late", () => Wheel("reactor", 8, "medium", w => { w.Arm(3); w.Tick(24); return w.Steady(); }));
        yield return E("wheel/kawaii/twinkle", () => Wheel("kawaii", 8, "medium", w =>
        {
            w.C.SetWheelLive(true);
            T_Render.AdvanceClock(700);
            return w.Steady();
        }, alwaysHub: true));
        yield return E("wheel/kawaii/twinkle-reduced", () => Wheel("kawaii", 8, "medium", w =>
        {
            w.C.SetWheelLive(true);
            T_Render.AdvanceClock(700);
            return w.Steady();
        }, alwaysHub: true, reduceMotion: true));
        yield return E("wheel/reactor/circuit-sparks", () => Wheel("reactor", 8, "medium", w =>
        {
            w.Arm(0);
            w.Frame(false);                                       // builds the boards and clears the sparks
            for (int k = 0; k < 20; k++)
            {
                T_Render.AdvanceClock(33);
                T_Render.Call(w.C, "TickCircuitSparks");
            }
            return w.Frame(true);
        }));
        foreach (var (mat, ms) in new[] { ("kawaii", 250), ("salvage", 180) })
            yield return E($"wheel/{mat}/fire-fx-{ms}ms", () => Wheel(mat, 8, "medium", w =>
            {
                w.Arm(0);
                SpawnWhileVisible(w.C);
                w.C.MarkFiringSlice(celebrate: false);
                w.C.RingOpacity = 0.5;
                w.Frame(false);                                   // the burst's clock starts on its first painted frame
                T_Render.AdvanceClock(ms);
                return w.Frame(true);
            }));

        // ── One-offs: the second wheel's tables, the other battery glyph, a mic scrub, no-device mix ──
        yield return E("wheel/salvage/wheel-b-armed", () => Wheel("salvage", 12, "medium", w => { w.Arm(9); return w.Steady(); }, wheelB: true));
        yield return E("wheel/pearl/wheel-b-practice", () => Wheel("pearl", 8, "medium", w =>
        {
            w.C.ShowNotice("Practice", RadialMenuControl.NoticeKind.Practice);
            w.C.SetOnboardingPracticeArt(true);
            return w.Steady();
        }, wheelB: true));
        yield return E("wheel/flat-dark/hub-battery-charging", () => Wheel("flat-dark", 8, "thin", w =>
            { w.C.SetBattery(8, true); return w.Steady(); }, alwaysHub: true));
        yield return E("wheel/flat-light/scrubber-mic", () => Wheel("flat-light", 8, "thick", w =>
        {
            w.C.ScrubberActive = true;
            w.C.ScrubberLevel = 0.0f;
            w.C.ScrubberGlyph = "Microphone";
            return w.Steady();
        }));
        yield return E("wheel/obsidian/mix-no-device", () => Wheel("obsidian", 8, "medium", w =>
            { w.C.ShowMixIndicator(0.5, null, null, noDevice: true); return w.Steady(); }));
        yield return E("wheel/pearl/rest-labels-selected", () => Wheel("pearl", 12, "medium", w => w.Steady(), labels: "selected"));

        // Degenerate rings — one slice is a full annulus, two are half rings with a vanishing chord — and an
        // edit session on an empty wheel, the one state that draws with no slices at all.
        foreach (var mat in new[] { "pearl", "salvage", "mesa", "reactor" })
        {
            yield return E($"wheel/{mat}/rest/n1-medium", () => Wheel(mat, 1, "medium", w => w.Steady()));
            yield return E($"wheel/{mat}/armed/n2-medium", () => Wheel(mat, 2, "medium", w => { w.Arm(0); return w.Steady(); }));
        }
        foreach (var mat in new[] { "pearl", "kawaii", "reactor" })
            yield return E($"wheel/{mat}/edit-empty", () => Wheel(mat, 4, "medium", w =>
            {
                w.C.BeginEdit([]);
                return w.Steady();
            }, prewarm: false));

        foreach (var e in PreviewEntries()) yield return e;
    }

    private static int Count(string n) => int.Parse(n[1..]);

    private static RenderEntry E(string name, Func<RenderedFrame> render) => new(name, render);

    // ── Static preview bakes (Settings icon well, material tiles) ──────────────────────────────────────

    private static IEnumerable<RenderEntry> PreviewEntries()
    {
        var rmc = typeof(RadialMenuControl);
        foreach (var mat in _materials)
        {
            string p = "preview/" + mat;
            yield return E($"{p}/icon", () =>
            {
                var slice = Populate([new WheelSlice { Label = "Cog", IconName = "Cog", IconColor = "#7A3FB0", Action = new ActionConfig { Type = "settings" } }], mat)[0];
                var rim = ActionTint.DisplayColor(slice, ActionTint.TintSetFor(mat));
                return Bake(H.InvokeStatic(rmc, "RenderPreviewIcon", slice.Icon, 67.0, mat, (Color?)rim));
            });
            yield return E($"{p}/logo", () =>
            {
                var slice = Populate([new WheelSlice { Label = "Firefly", IconName = "Gamepad", LogoPath = Asset("packaging/sample-arcade-package/firefly/glyph.png"), Action = new ActionConfig { Type = "installed-game", Url = "steam://rungameid/1" } }], mat)[0];
                var set = ActionTint.TintSetFor(mat);
                return Bake(H.InvokeStatic(rmc, "RenderPreviewLogo", Logo(), 67.0, ActionTint.BrushFor(slice, set), mat,
                                           (Color?)ActionTint.DisplayColor(slice, set)));
            });
            yield return E($"{p}/tile", () => Tile(mat));
        }
        var tint = (Brush)new SolidColorBrush(Color.FromRgb(0x3C, 0x6E, 0x74)).GetAsFrozen();
        var edge = (Brush)new SolidColorBrush(Color.FromArgb(0x4D, 0, 0, 0)).GetAsFrozen();
        var rimBrush = (Brush)new SolidColorBrush(Color.FromRgb(0x8A, 0x4B, 0x2A)).GetAsFrozen();
        yield return E("preview/tinted-logo/plain", () => Bake(H.InvokeStatic(rmc, "RenderTintedLogo", WideLogo(), 67.0, tint, null, 0.0, false)));
        yield return E("preview/tinted-logo/inner-edge", () => Bake(H.InvokeStatic(rmc, "RenderTintedLogo", Logo(), 67.0, tint, edge, 0.5, false)));
        yield return E("preview/tinted-logo/outer-edge", () => Bake(H.InvokeStatic(rmc, "RenderTintedLogo", Logo(), 67.0, tint, rimBrush, 5.5, true)));
        yield return E("preview/edged-icon", () => Bake(H.InvokeStatic(rmc, "RenderEdgedIcon", Glyph("Cog", tint), 67.0, edge, 0.5)));
        yield return E("preview/outer-edged-icon", () => Bake(H.InvokeStatic(rmc, "RenderOuterEdgedIcon", Glyph("Cog", tint), 67.0, rimBrush, 5.5)));
    }

    /// <summary>A preview bake as pixels: a bitmap bake is hashed as it is; a material that returns the glyph
    /// untreated hands back a vector image, which is rasterised at the bakes' own 2× scale.</summary>
    private static RenderedFrame Bake(object? result)
    {
        var img = (ImageSource)result!;
        if (img is System.Windows.Media.Imaging.BitmapSource bmp) return T_Render.FromBitmap(bmp);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) dc.DrawImage(img, new Rect(0, 0, img.Width, img.Height));
        return T_Render.Capture(dv, img.Width, img.Height, 2.0);
    }

    /// <summary>The pieces a material tile is built from — the resting fill, a per-slice fill, the styled
    /// decoration and the label face and shadow — on one tile-sized card.</summary>
    private static RenderedFrame Tile(string mat)
    {
        var grid = new Grid { Width = 180, Height = 100, Background = RadialMenuControl.PreviewFill(mat) };
        if (RadialMenuControl.PreviewDecoration(mat, 10) is { } deco) grid.Children.Add(deco);
        var swatch = new Border
        {
            Width = 28, Height = 28, CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 8, 0), Background = RadialMenuControl.PreviewFillForSlice(mat, 3, 8),
        };
        grid.Children.Add(swatch);
        var label = new TextBlock
        {
            Text = mat, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = RadialMenuControl.PreviewIsDark(mat) ? Brushes.White : Brushes.Black,
            Effect = RadialMenuControl.PreviewLabelShadow(mat),
        };
        RadialMenuControl.ApplyPreviewLabelFace(label, mat);
        grid.Children.Add(label);
        var host = T_Render.Unscaled(new Border { Width = 200, Height = 120, Padding = new Thickness(10), Child = grid });
        host.Measure(new Size(200, 120));
        host.Arrange(new Rect(0, 0, 200, 120));
        host.UpdateLayout();
        return T_Render.Capture(host, 200, 120, DpiScale);
    }

    // ── One wheel ─────────────────────────────────────────────────────────────────────────────────────

    private static RadialMenuControl NewControl()
    {
        var c = T_Render.Unscaled(new RadialMenuControl { Width = Box, Height = Box });
        c.Measure(new Size(Box, Box));
        c.Arrange(new Rect(0, 0, Box, Box));
        return c;
    }

    /// <summary>Build, configure and pre-warm one wheel, run <paramref name="state"/> on it, and tear it down.
    /// Everything the resting-slice bake keys on is applied BEFORE the pre-warm, so a state never leaves the
    /// bakes half-rebuilt.</summary>
    private static RenderedFrame Wheel(string mat, int n, string thickness, Func<W, RenderedFrame> state,
                                       string? labels = null, bool reduceMotion = false, bool wheelB = false,
                                       bool alwaysHub = false, bool prewarm = true)
    {
        var c = NewControl();
        c.SetSliceMaterial(mat);
        c.SetSliceThickness(thickness);
        c.SetShowSliceLabels(labels);
        c.IsWheelB = wheelB;
        c.AlwaysShowHub = alwaysHub;
        c.ReduceMotion = reduceMotion;
        c.Slices = BuildSlices(n, mat);
        var w = new W(c, n);
        if (prewarm) T_Render.Call(c, "PrewarmParked");
        try { return state(w); }
        finally { Neutralize(c); }
    }

    /// <summary>Leave nothing live on a finished control: the dispatcher work it queued runs next (see
    /// <see cref="T_Render.Drain"/>), and it must find an empty wheel rather than repaint this one.</summary>
    private static void Neutralize(RadialMenuControl c)
    {
        if (c.EditMode) c.EndEdit();
        c.EndArcadeCollapse();
        c.SetWheelLive(false);
        c.Slices = [];
    }

    private static void WithReducedMotion(Action a)
    {
        MotionPolicy.UserSetting = true;
        try { a(); }
        finally { MotionPolicy.UserSetting = false; }
    }

    /// <summary>The fire celebrations spawn only while the control is visible. Rooting it in a
    /// never-shown <see cref="HwndSource"/> makes it so for the spawn, then it is detached again: the burst
    /// itself is drawn offscreen like every other frame.
    /// <para>⚠ Rooting lays the control out and PAINTS it, and several of its bake caches are keyed without the
    /// DPI they were baked at. The window is therefore created DPI-unaware, so that paint runs at 96 DPI like
    /// every other one here instead of seeding those caches at the monitor's scale.</para></summary>
    private static void SpawnWhileVisible(RadialMenuControl c)
    {
        var p = new HwndSourceParameters("radiata-render-spawn") { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000) };
        IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-1));   // DPI_AWARENESS_CONTEXT_UNAWARE
        try
        {
            using var src = new HwndSource(p);
            src.RootVisual = c;
            if (!c.IsVisible) throw new InvalidOperationException("the control did not become visible under a hidden HwndSource");
            double dpi = VisualTreeHelper.GetDpi(c).DpiScaleX;
            if (Math.Abs(dpi - 1) > 1e-9) throw new InvalidOperationException($"the hidden HwndSource runs at DPI scale {dpi}, not 1");
            c.CelebrateFire();
            src.RootVisual = null;
        }
        finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        T_Render.Unscaled(c);
        c.Measure(new Size(Box, Box));
        c.Arrange(new Rect(0, 0, Box, Box));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    /// <summary>One wheel under construction, with the drivers a state needs.</summary>
    private sealed class W(RadialMenuControl c, int n)
    {
        public readonly RadialMenuControl C = c;
        public readonly int N = n;
        public WheelStateMachine Sm => (WheelStateMachine)T_Render.Get(C, "_sm")!;

        private static readonly MethodInfo OnRender = T_Render.Method(typeof(RadialMenuControl), "OnRender");

        /// <summary>One paint of the control. Only a captured frame is rasterised.</summary>
        public RenderedFrame Frame(bool capture)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(Pad, Pad));
                OnRender.Invoke(C, [dc]);
                dc.Pop();
            }
            return capture ? T_Render.Capture(dv, Canvas, Canvas, DpiScale) : null!;
        }

        /// <summary>A frame after one warm-up paint: what a wheel looks like once it has been on screen.</summary>
        public RenderedFrame Steady()
        {
            Frame(false);
            return Frame(true);
        }

        /// <summary>Tilt the stick at slice <paramref name="i"/> of <paramref name="of"/> until it arms
        /// (the state machine smooths the stick, so it takes a few reports).</summary>
        public void Arm(int i, int of = -1)
        {
            Aim(i, of < 0 ? N : of);
            if (C.ArmedIndex != i) throw new InvalidOperationException($"arming slice {i} armed {C.ArmedIndex}");
        }

        public void Aim(int i, int of)
        {
            double rad = (360.0 / of * i - 90) * Math.PI / 180.0;
            float x = (float)(Math.Cos(rad) * 0.95), y = (float)(Math.Sin(rad) * 0.95);
            for (int k = 0; k < 40; k++) C.UpdateStick(x, y);
        }

        /// <summary>Advance the dwell timers by <paramref name="ticks"/> render ticks, as the control's own
        /// 30 ms timer would while it is on screen.</summary>
        public void Tick(int ticks)
        {
            for (int k = 0; k < ticks; k++) Sm.Tick(true);
        }
    }

    // ── Slices ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The slice pool, first <paramref name="n"/> taken: a glyph, a game logo, a long label, a guarded
    /// action with an exact tint, an arcade game, cover art, the Arcade Launcher, a label-off slice, a swatch
    /// tint, a system glyph, a wide wordmark logo with its label on, and a lock. Fresh instances every time,
    /// with their icons resolved the way the app resolves them for this material.</summary>
    private static WheelSlice[] BuildSlices(int n, string mat)
    {
        WheelSlice[] pool =
        [
            new() { Label = "Volume", IconName = "VolumeHigh", Action = new ActionConfig { Type = "system", Command = "volume" } },
            new() { Label = "Firefly", IconName = "Gamepad", LogoPath = Asset("packaging/sample-arcade-package/firefly/glyph.png"), Action = new ActionConfig { Type = "installed-game", Url = "steam://rungameid/1" } },
            new() { Label = "Open the Extraordinarily Long Settings Page", IconName = "Cog", IconColor = "#7A3FB0", Action = new ActionConfig { Type = "settings" } },
            new() { Label = "Shut Down", IconName = "Power", IconColor = "#E0302A", IconColorExact = true, Action = new ActionConfig { Type = "system", Command = "shutdown", RequireConfirm = true } },
            new() { Label = "Kabloom", IconName = "BeeFlower", Action = new ActionConfig { Type = "arcade", Command = "kabloom" } },
            new() { Label = "Cover Art", IconPath = Asset("Assets/Kabloom-Question.png"), Action = new ActionConfig { Type = "installed-game", Url = "steam://rungameid/2" } },
            new() { Label = "Arcade", IconName = "RadiataJoystick", Action = new ActionConfig { Type = "arcade", Command = "" } },
            new() { Label = "Game Grid", IconName = "ViewGrid", ShowLabel = false, Action = new ActionConfig { Type = "game-browser" } },
            new() { Label = "Discord Mute", IconName = "Headset", IconColor = "#2F8F5A", Action = new ActionConfig { Type = "discord-mute" } },
            new() { Label = "Screenshot", IconName = "Camera", Action = new ActionConfig { Type = "system", Command = "gamebar-screenshot" } },
            new() { Label = "Wide Logo", IconName = "Gamepad", LogoPath = Asset("Assets/tile-logo-null.png"), ShowLabel = true, Action = new ActionConfig { Type = "installed-game", Url = "steam://rungameid/3" } },
            new() { Label = "Lock", IconName = "Lock", Action = new ActionConfig { Type = "system", Command = "lock" } },
        ];
        return Populate(pool.Take(n).ToArray(), mat);
    }

    private static WheelSlice[] PickerMenu() =>
    [
        new() { Label = "Launch", IconName = "RocketLaunch", Action = new ActionConfig { Type = "launch" } },
        new() { Label = "System", IconName = "Cog", Action = new ActionConfig { Type = "system" } },
        new() { Label = "Radiata", IconName = PackIconHelper.RadiataMarkName, Action = new ActionConfig { Type = "settings" } },
        new() { Label = "Arcade", IconName = PackIconHelper.JoystickName, Action = new ActionConfig { Type = "arcade" } },
        new() { Label = "Media", IconName = "Music", Action = new ActionConfig { Type = "keypress" } },
    ];

    /// <summary>Resolve icons and logos through the app's own <c>App.PopulateIcons</c>.</summary>
    private static WheelSlice[] Populate(WheelSlice[] slices, string mat)
    {
        H.InvokeStatic(H.AppType("App"), "PopulateIcons", slices, (ActionTint.TintSet?)ActionTint.TintSetFor(mat));
        return slices;
    }

    private static ImageSource Logo() =>
        (ImageSource)H.InvokeStatic(H.AppType("GameArt"), "LoadFromFile", Asset("packaging/sample-arcade-package/firefly/glyph.png"))!;

    private static ImageSource WideLogo() =>
        (ImageSource)H.InvokeStatic(H.AppType("GameArt"), "LoadFromFile", Asset("Assets/tile-logo-null.png"))!;

    private static ImageSource Glyph(string name, Brush fill) => PackIconHelper.FromName(name, fill)!;
}
