using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ControllerWheel;

// ── Two-tier notice priority (the toast slot is single-occupancy) ─────────
// SAFETY notices (isolation warnings, driver faults, recovery steps) always show — they evict
// whatever holds the slot and then hold it for their duration. INFO notices (confirmations,
// controller-changed, Passthru Mode state) show only when no safety hold is live; otherwise they are
// DROPPED, never queued — a stale "Controller changed" arriving after the hold would out-date
// itself, and an info notice must never evict a live safety warning. Round wheel-anchored status
// toasts yield to the same hold.
internal enum NoticeTier { Info, Safety }

/// <summary>The single-occupancy toast slot App owns: the round material-themed toast, the rectangular
/// standalone notice card, the close timer and the safety hold. Each show evicts whatever holds the slot.</summary>
internal sealed class ToastPresenter
{
    private readonly Func<string?> _sliceMaterial;
    private readonly Func<bool> _onboardingOpen;

    private Window[]?        _statusToasts;
    private DispatcherTimer? _toastTimer;
    private long _safetyToastUntilMs;

    /// <param name="sliceMaterial">The wheel material the round toast mirrors, read at each show.</param>
    /// <param name="onboardingOpen">True while the first-run wizard is up; rectangular notices are suppressed then.</param>
    internal ToastPresenter(Func<string?> sliceMaterial, Func<bool> onboardingOpen)
    {
        _sliceMaterial  = sliceMaterial;
        _onboardingOpen = onboardingOpen;
    }

    /// <summary>A safety notice holds the slot; info notices and round status toasts yield to it.</summary>
    internal bool SafetyHoldActive => Environment.TickCount64 < _safetyToastUntilMs;

    /// <summary>Hold the slot for a safety notice for <paramref name="holdMs"/> from now.</summary>
    internal void HoldForSafety(int holdMs) => _safetyToastUntilMs = Environment.TickCount64 + holdMs;

    /// <summary>End the safety hold early — for the outcome of the very operation a safety progress notice
    /// announced, which supersedes it and would otherwise be dropped as an info notice.</summary>
    internal void ReleaseSafetyHold() => _safetyToastUntilMs = 0;

    internal void StopCloseTimer() => _toastTimer?.Stop();

    /// <summary>Build + show a rectangular standalone-notice card in the SteamSentry card's chrome
    /// (dark roundrect, 1.5px border) and put it in the single-occupancy toast slot. Null centre =
    /// pinned top-right (App.ShowCornerToast's corner contract); a centre point places it there. Appears fully
    /// formed — no entrance animation, by the same contract as the round corner toast it replaced.
    /// Returns false when the notice was suppressed (OOBE, or an info notice yielding to a safety
    /// hold) so callers don't start hold timers for a card that never showed. An <paramref
    /// name="onClick"/> makes the whole card a one-shot click target (the balloon-replacement
    /// affordance: the card is the only notice channel, so a click action lives on it).</summary>
    internal bool ShowRectToast(string line1, string line2, double? centerX, double? centerY,
                               NoticeTier tier = NoticeTier.Safety, Action? onClick = null)
    {
        // Onboarding suppresses the whole rectangular-notice class: a first-run user's opening minutes
        // must not be a stack of controller alarms competing with the wizard — which itself surfaces
        // driver/capture state. The conditions are NOT suppressed (tray
        // text, traces, silent cures all continue), and the wizard's Closed handler re-runs capture so
        // anything still true fires then.
        if (_onboardingOpen())
        {
            Trace.WriteLine($"[Notice] suppressed during onboarding — \"{line1}\"");
            return false;
        }
        if (tier == NoticeTier.Info && Environment.TickCount64 < _safetyToastUntilMs)
        {
            Trace.WriteLine($"[Notice] info notice dropped — a safety notice holds the slot (\"{line1}\")");
            return false;
        }
        // Every card the user can see leaves a line: "did the card appear" is otherwise unanswerable
        // from a log, and the negative (a card that must NOT appear) is what several tests assert.
        Trace.WriteLine($"[Notice] card shown ({tier}) — \"{line1}\" / \"{line2}\"");
        _toastTimer?.Stop();
        CloseToasts();

        var tb = new System.Windows.Controls.TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth     = 400,
            Foreground   = System.Windows.Media.Brushes.White,
        };
        tb.Inlines.Add(new System.Windows.Documents.Run(line1)
            { FontWeight = FontWeights.Bold, FontSize = 14 });
        tb.Inlines.Add(new System.Windows.Documents.LineBreak());
        tb.Inlines.Add(new System.Windows.Documents.Run(line2) { FontSize = 13 });

        // The Radiata mark on the left names the sender — a bare topmost card gives the user no clue which
        // app is talking, and these cards speak about the user's CONTROLLER, where several other apps also
        // pop notices. Code-drawn (FlowerMarkControl). Round wheel-anchored status toasts are a different
        // surface and stay unmarked.
        // ⚠ Play() is REQUIRED, not decoration: every piece is built with a 0×0 ScaleTransform, so a mark
        // that is never played renders nothing at all. Play() is also what honours Reduce Motion — it
        // shows the mark fully formed there — so this does not violate the card's no-entrance-animation
        // contract. Don't "simplify" it away.
        var mark = new FlowerMarkControl(44) { MarkBrush = System.Windows.Media.Brushes.White };
        var markHost = new System.Windows.Controls.Border
        {
            Width  = 44, Height = 44,
            Margin = new Thickness(0, 2, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child  = mark,
        };

        var row = new System.Windows.Controls.StackPanel
            { Orientation = System.Windows.Controls.Orientation.Horizontal };
        row.Children.Add(markHost);
        row.Children.Add(tb);

        var card = new System.Windows.Controls.Border
        {
            Background      = new System.Windows.Media.SolidColorBrush(
                                  System.Windows.Media.Color.FromArgb(0xF0, 0x20, 0x20, 0x20)),
            BorderBrush     = new System.Windows.Media.SolidColorBrush(
                                  System.Windows.Media.Color.FromArgb(0xFF, 0x6A, 0x6A, 0x70)),
            BorderThickness = new Thickness(1.5),
            CornerRadius    = new CornerRadius(12),
            Padding         = new Thickness(18, 14, 18, 14),
            Child           = row,
        };
        if (onClick is not null)
        {
            // One-shot: the action also closes the card, so a double-click can't fire it twice. The
            // card auto-closes after its hold either way — the click is a convenience, and every such
            // action has a persistent home elsewhere (Settings / the tray) for anyone who missed it.
            card.Cursor = System.Windows.Input.Cursors.Hand;
            card.MouseLeftButtonUp += (_, _) =>
            {
                var a = onClick;
                onClick = null;
                CloseToasts();
                // The slot is empty from here — a dismissed safety card must not keep dropping
                // info notices for the remainder of its hold.
                _safetyToastUntilMs = 0;
                a?.Invoke();
            };
        }

        var wa = SystemParameters.WorkArea;
        var win = new Window
        {
            FlowDirection = LocWpf.Flow,
            WindowStyle        = WindowStyle.None,
            AllowsTransparency = true,
            Background         = Brushes.Transparent,
            Topmost            = true,
            ShowActivated      = false,
            ShowInTaskbar      = false,
            SizeToContent      = SizeToContent.WidthAndHeight,
            Content            = card,
        };
        // SizeToContent means the real size exists only after layout (the sentry-card lesson) —
        // position from the MEASURED size, and re-pin if wrapping changes it.
        win.SizeChanged += (_, _) =>
        {
            if (centerX is double cx && centerY is double cy)
            {
                win.Left = cx - win.ActualWidth / 2;
                win.Top  = cy - win.ActualHeight / 2;
            }
            else
            {
                win.Left = wa.Right - win.ActualWidth - 24;
                win.Top  = wa.Top + 24;
            }
        };
        win.Show();
        mark.Play(forward: true);   // after Show(): the pieces are 0-scaled until this runs
        _statusToasts = [win];
        return true;
    }

    /// <summary>Build + show a material-themed round toast centred at (x,y); returns its window and the
    /// content's centre-origin ScaleTransform (start at <paramref name="initialScale"/>, so a caller can
    /// animate the circle in/out without a first-frame flash). Close via <see cref="StartToastClose"/>.</summary>
    internal (Window toast, System.Windows.Media.ScaleTransform scale) BuildAndShowToast(
        Func<System.Windows.Media.Brush, System.Windows.UIElement> inner, double centerX, double centerY,
        double initialScale = 1)
    {
        _toastTimer?.Stop();
        CloseToasts();

        const double Size = RadialMenuControl.InnerRadius * 2;
        string mat = _sliceMaterial() ?? "pearl";
        var (fill, ink, edge, edgeW, drawRim) = ToastBrushes(mat);   // follow the wheel's material

        System.Windows.FrameworkElement content;
        if (mat is "mesa" or "salvage")
        {
            // Irregular-edged toast (terra/salvage): the SAME ring the wheel hub uses, drawn as a Path —
            // a Border can only render a true circle. Terra's edge is a wobbled cut and additionally gets
            // the hub's hard offset cutout shadow and no stroke (the cut edge + shadow carry the shape);
            // salvage's is the stamped stepped plate, matching its hub since its edge stopped being wavy.
            double r   = Size / 2;
            double amp = mat == "mesa" ? 2.2 : 1.1;   // same amp + px wave density as the wheel hub
            var ring = RadialMenuControl.BuildHubToastRing(new Point(r, r), r - amp, amp,
                                                           mat == "mesa" ? 8.0 : 3.5, blocky: mat == "salvage");
            var grid = new System.Windows.Controls.Grid { Width = Size, Height = Size };
            if (mat == "mesa")
                grid.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = ring, Fill = RadialMenuControl.TerraShadow,
                    RenderTransform = new System.Windows.Media.TranslateTransform(2, 3), IsHitTestVisible = false,
                });
            grid.Children.Add(new System.Windows.Shapes.Path
            {
                // Salvage fills with the WHEEL's own plate charcoal, not the lighter translucent grey the
                // other toasts use: this plate then carries the same rust + lip passes as the hub, and over
                // a pale toast fill the rust washed out to nothing.
                Data = ring, Fill = mat == "salvage" ? RadialMenuControl.SalvageFill : fill,
                Stroke = mat == "salvage" ? edge : null, StrokeThickness = mat == "salvage" ? edgeW : 0,
            });
            if (mat == "salvage")
            {
                // The hub's two remaining passes (see the salvage branch of the hub draw): the rust surface,
                // then the uniform recessed inner lip. Without these the toast is a flat translucent disc of
                // the right SHAPE but visibly not the same material.
                // A Path's fill is already bounded by its geometry, so the texture needs no clip — but the
                // LIP is a centred stroke, so its outer half has to be clipped away exactly as the hub's
                // PushClip(ring) does, or the plate grows by half the lip width.
                grid.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = ring, Fill = RadialMenuControl.SalvageBoxTexture(Size), IsHitTestVisible = false,
                });
                var lip = RadialMenuControl.SalvageHubLipPen;
                grid.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = ring, Stroke = lip.Brush, StrokeThickness = lip.Thickness,
                    Clip = ring, IsHitTestVisible = false,
                });
            }
            grid.Children.Add(inner(ink));   // explicit-size / self-centring content sits over the shape
            content = grid;
        }
        else if (mat == "kawaii")
        {
            // Kawaii: the SAME cloud-scalloped shape as the wheel hub, plus its ambient twinkle — here the
            // stars are WPF opacity animations rather than the hub's render-loop cycle, since a toast is a
            // plain Window with no per-frame draw of its own.
            double r = Size / 2;
            var grid = new System.Windows.Controls.Grid { Width = Size, Height = Size };
            grid.Children.Add(new System.Windows.Shapes.Path
            {
                Data = RadialMenuControl.BuildCloudToastRing(new Point(r, r), r - edgeW),
                Fill = fill, Stroke = edge, StrokeThickness = edgeW,
            });
            AddKawaiiStars(grid, r, edge);
            grid.Children.Add(inner(ink));
            content = grid;
        }
        else
        {
            var circle = new System.Windows.Controls.Border
            {
                Width           = Size,
                Height          = Size,
                Background      = fill,
                CornerRadius    = new CornerRadius(Size / 2),
                BorderBrush     = edge,
                BorderThickness = new Thickness(edgeW),
                Child           = inner(ink),
            };

            // Gloss: overlay the slices' Edge rim-light arcs over the circle.
            content = circle;
            if (drawRim)
            {
                var grid = new System.Windows.Controls.Grid { Width = Size, Height = Size };
                grid.Children.Add(circle);
                grid.Children.Add(BuildToastRim(Size));
                content = grid;
            }
        }
        var scale = new System.Windows.Media.ScaleTransform(initialScale, initialScale);
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment   = VerticalAlignment.Center;
        content.RenderTransformOrigin = new Point(0.5, 0.5);   // scale animations grow/shrink from centre
        content.RenderTransform = scale;

        // Transparent padding around the circle gives the scale bounce room to overshoot past 1 (or wind
        // up before the shrink) without clipping against the window edge.
        const double Pad = Size * 0.24;
        var host = new System.Windows.Controls.Grid { Width = Size + 2 * Pad, Height = Size + 2 * Pad };
        host.Children.Add(content);

        var toast = new Window
        {
            FlowDirection = LocWpf.Flow,
            WindowStyle        = WindowStyle.None,
            AllowsTransparency = true,
            Background         = Brushes.Transparent,
            ShowInTaskbar      = false,
            Topmost            = true,
            ShowActivated      = false,
            SizeToContent      = SizeToContent.WidthAndHeight,
            Content            = host,
            Left               = centerX - (Size + 2 * Pad) / 2,
            Top                = centerY - (Size + 2 * Pad) / 2,
        };

        toast.Show();
        _statusToasts = [toast];
        return (toast, scale);
    }

    /// <summary>Kawaii toast twinkle: nine stars around the cloud of radius <paramref name="r"/>, outlined in
    /// <paramref name="edge"/>, each on its own WPF opacity clock.</summary>
    private static void AddKawaiiStars(System.Windows.Controls.Grid grid, double r, System.Windows.Media.Brush edge)
    {
        // Nine stars rather than a token few, each deliberately DISTINCT — size, rim distance, stagger
        // and cycle length all vary (coprime steps keep the pattern from repeating), so they never
        // pulse in lockstep. Each is a warm star with a smaller white-hot core, matching the hub's.
        const int stars = 9;
        for (int k = 0; k < stars; k++)
        {
            double ang = k * (360.0 / stars) + (k % 3) * 9 + 12;                 // spread + jitter
            double rad = r * (k % 3 == 0 ? 0.82 : k % 3 == 1 ? 0.99 : 1.11);     // inside / on / outside the puffs
            double sz  = 4.5 + (k * 7 % 5) * 1.7;                                // 4.5 … 11.3
            double a   = ang * Math.PI / 180.0;
            var at = new Point(r + Math.Cos(a) * rad, r + Math.Sin(a) * rad);

            // Both paths ride one container so a single opacity animation drives the pair.
            var starHost = new System.Windows.Controls.Grid { IsHitTestVisible = false, Opacity = 0 };
            starHost.Children.Add(new System.Windows.Shapes.Path
            {
                Data = RadialMenuControl.KawaiiStarGeometry(at, sz, ang),
                Fill = RadialMenuControl.KawaiiTwinkleInk,
                // Outline in the toast's own rim colour — the pale warm star vanished on a light
                // background. Round joins so the star's points don't spike.
                Stroke = edge, StrokeThickness = 2, StrokeLineJoin = System.Windows.Media.PenLineJoin.Round,
            });
            starHost.Children.Add(new System.Windows.Shapes.Path
            {
                Data = RadialMenuControl.KawaiiStarGeometry(at, sz * 0.45, ang),
                Fill = System.Windows.Media.Brushes.White,
            });
            // Keyframes, not AutoReverse: reversing ties the fade-out to the fade-in, and the rise needs
            // to be twice as quick as the fall — the star should snap alight, then ease away.
            double outMs = 320 + (k * 5 % 4) * 120;   // 320…680ms
            double inMs  = outMs / 2.0;
            var tw = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
            {
                BeginTime      = TimeSpan.FromMilliseconds(90 * (k * 4 % stars)),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                Duration       = new Duration(TimeSpan.FromMilliseconds(inMs + outMs)),
            };
            tw.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
            tw.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0.95, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(inMs))));
            tw.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(inMs + outMs))));
            starHost.BeginAnimation(UIElement.OpacityProperty, tw);
            grid.Children.Add(starHost);
        }
    }

    internal void StartToastClose(double ms)
    {
        // Stop any pending close first, and capture the LOCAL timer in the tick closure, not the
        // _toastTimer FIELD: a closure that read the field would, when a second toast replaces the
        // first inside the close window, have the first timer stop the SECOND timer (closing the new
        // toast early) while itself never stopping — an immortal repeating tick.
        _toastTimer?.Stop();
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); if (ReferenceEquals(_toastTimer, t)) CloseToasts(); };
        _toastTimer = t;
        t.Start();
    }

    /// <summary>Status-toast styling matched to the wheel's SliceMaterial (already canonicalized by
    /// <c>ConfigLoader.Sanitize</c>, so the raw token comparisons below are safe). Glossy = the horizon-cut
    /// "liquid glass" gradient (horizon at 0.65), with the SAME edge chrome the wheel slices use: Obsidian
    /// gets the cool-white Edge rim-light arcs (top + bottom), Pearl gets the faint slice
    /// outline stroke (it also gets the rim arcs). Flat = solid fill + a plain faint edge.
    /// <paramref name="drawRim"/> tells the caller to overlay the Edge rim arcs.</summary>
    private static (System.Windows.Media.Brush fill, System.Windows.Media.Brush ink,
             System.Windows.Media.Brush edge, double edgeW, bool drawRim) ToastBrushes(string mat)
    {
        static System.Windows.Media.Color Argb(byte a, byte r, byte g, byte b) => System.Windows.Media.Color.FromArgb(a, r, g, b);
        static System.Windows.Media.Brush Solid(System.Windows.Media.Color c) { var b = new System.Windows.Media.SolidColorBrush(c); b.Freeze(); return b; }

        bool dark  = Materials.IsDark(mat);
        var ink = Solid(dark ? Argb(0xEE, 238, 240, 246) : Argb(0xDE, 40, 40, 46));

        // The styled materials mirror the wheel HUB's treatment exactly — same fill + rim as the hub disc;
        // the caller draws Mesa's wavy cut-paper shape (and its shadow) / salvage's stamped-plate shape.
        switch (mat)
        {
            case "kawaii":       // hub's milky pink-white cloud + the soft orchid rim, plum ink
                return (Solid(Argb(246, 253, 246, 250)), Solid(Argb(0xDE, 63, 43, 79)), Solid(Argb(255, 232, 213, 238)), 2.0, false);
            case "salvage":       // matte charcoal + faint dark edge (stepped plate shape drawn by the caller)
                return (Solid(Argb(222, 56, 56, 60)), ink, Solid(Argb(60, 0, 0, 0)), 1.0, false);
            case "mesa":         // cream cut paper (Mesa) — no stroke; the caller adds the offset cutout shadow
                return (Solid(Argb(246, 242, 237, 227)), ink, Solid(System.Windows.Media.Colors.Transparent), 0.0, false);
            case "reactor":       // OPAQUE dark plate (the hub's alpha-34 smoke reads as almost nothing
                                  // over a game) + the full-bright rim for contrast
                return (Solid(Argb(242, 14, 14, 18)), ink, Solid(Argb(235, 240, 240, 240)), 2.0, false);
        }

        // Glassy/lit toasts: Pearl / Obsidian (specular dome). Flat toasts: flat-*.
        // ⚠ Match the material constants, never a token prefix: pearl/obsidian share none, and naming the
        // constants makes a token rename break the build rather than silently flatten the default toast.
        bool gloss = mat is Materials.GlossLight or Materials.GlossDark;

        if (!gloss)
        {
            var solid = Solid(dark ? Argb(255, 34, 38, 46) : Argb(255, 250, 250, 252));
            var edge  = Solid(dark ? Argb(70, 255, 255, 255) : Argb(60, 0, 0, 0));
            return (solid, ink, edge, 1.5, false);
        }

        // Horizon-cut gloss: sheen up top, a hard horizon at 0.65, body, faint bounce (same stops as
        // GameBrowserControl's GlossDark/LightCard, sheen centred for the round toast).
        var grad = new System.Windows.Media.RadialGradientBrush
        {
            MappingMode    = System.Windows.Media.BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(0.5, 0.0), GradientOrigin = new Point(0.5, 0.0),
            RadiusX = 2.4, RadiusY = 1.0,
        };
        void Stop(double o, System.Windows.Media.Color c) => grad.GradientStops.Add(new System.Windows.Media.GradientStop(c, o));
        if (dark)
        {
            Stop(0.00, GlossDarkPalette.A(GlossDarkPalette.Sheen,        250));
            Stop(0.45, GlossDarkPalette.A(GlossDarkPalette.UpperMid,     250));
            Stop(0.61, GlossDarkPalette.A(GlossDarkPalette.AboveHorizon, 248));
            Stop(0.65, GlossDarkPalette.A(GlossDarkPalette.DarkCut,      252));   // hard horizon cut (0.65)
            Stop(0.83, GlossDarkPalette.A(GlossDarkPalette.Body,         252));
            Stop(1.00, GlossDarkPalette.A(GlossDarkPalette.Bounce,       246));
        }
        else
        {
            // Low-contrast horizon that recovers below it — matches the slices' GlossLightRamp
            // (RadialMenuControl), rescaled so the gentle step lands at 0.65.
            Stop(0.00, Argb(252, 255, 255, 255));   // bright specular sheen
            Stop(0.13, Argb(250, 246, 250, 253));
            Stop(0.61, Argb(244, 224, 232, 238));   // just above the horizon
            Stop(0.65, Argb(246, 212, 222, 230));   // gentle horizon step — low contrast across the line
            Stop(0.76, Argb(242, 220, 230, 237));   // recovers just below the horizon
            Stop(1.00, Argb(230, 228, 236, 242));   // faint bottom bounce
        }
        grad.Freeze();

        // Both gloss themes draw the slices' Edge rim-light arcs (subtle/cool-white — barely-there on
        // light). Gloss Dark has no other border; Gloss Light adds the slice outline stroke (SlicePen).
        return dark
            ? (grad, ink, Solid(System.Windows.Media.Colors.Transparent), 0.0, true)
            : (grad, ink, Solid(Argb(64, 0, 0, 0)), 1.5, true);
    }

    /// <summary>The cool-white Edge rim-light arcs the Gloss Dark wheel slices use, sized for the toast
    /// circle — a top catch + a bottom catch, each fading to transparent at its ends (mirrors
    /// RadialMenuControl.WheelRimPen).</summary>
    private static System.Windows.UIElement BuildToastRim(double size)
    {
        var canvas = new System.Windows.Controls.Canvas { Width = size, Height = size, IsHitTestVisible = false };
        canvas.Children.Add(RimArc(size, -162, 144));   // top
        canvas.Children.Add(RimArc(size,   30, 120));   // bottom
        return canvas;
    }

    private static System.Windows.Shapes.Path RimArc(double size, double startDeg, double sweepDeg)
    {
        double r = size / 2 - 3;
        var c = new Point(size / 2, size / 2);
        Point P(double deg) { double a = deg * Math.PI / 180; return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)); }

        var fig = new System.Windows.Media.PathFigure { StartPoint = P(startDeg) };
        fig.Segments.Add(new System.Windows.Media.ArcSegment(
            P(startDeg + sweepDeg), new Size(r, r), 0, sweepDeg > 180,
            System.Windows.Media.SweepDirection.Clockwise, true));
        var geo = new System.Windows.Media.PathGeometry();
        geo.Figures.Add(fig); geo.Freeze();

        // Gradient ALONG the arc (bbox is horizontal): fade at both ends, ease off at the apex.
        var e = GlossDarkPalette.Edge;
        var g = new System.Windows.Media.LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        g.GradientStops.Add(new System.Windows.Media.GradientStop(GlossDarkPalette.A(e, 0),  0.00));
        g.GradientStops.Add(new System.Windows.Media.GradientStop(GlossDarkPalette.A(e, 92), 0.22));
        g.GradientStops.Add(new System.Windows.Media.GradientStop(GlossDarkPalette.A(e, 72), 0.50));
        g.GradientStops.Add(new System.Windows.Media.GradientStop(GlossDarkPalette.A(e, 92), 0.78));
        g.GradientStops.Add(new System.Windows.Media.GradientStop(GlossDarkPalette.A(e, 0),  1.00));
        g.Freeze();

        return new System.Windows.Shapes.Path
        {
            Data = geo, Stroke = g, StrokeThickness = 3,
            StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
            StrokeEndLineCap   = System.Windows.Media.PenLineCap.Round,
        };
    }

    internal void CloseToasts()
    {
        if (_statusToasts is null) return;
        foreach (var t in _statusToasts) t.Close();
        _statusToasts = null;
    }
}
