using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ControllerWheel;

/// <summary>The animated Radiata flower mark used on the About screen (and the first-run wizard's finish
/// step): the two leaves spring up from the base, then the blossom's petals sweep in one at a time around
/// the circle. Each piece wears the mark's authored colour (FlowerIcon.LeafLeftColor / LeafRightColor /
/// BlossomColor, from Assets\flower-mark-color-unique.svg).
/// Plays on first realisation and every time it becomes visible again (e.g. returning to the About tab).
/// (The wheels on/off toast uses the separate monochrome <see cref="FlowerMarkControl"/>.)</summary>
public sealed class AboutFlowerControl : Viewbox
{
    private readonly System.Windows.Shapes.Path _leafL = new();
    private readonly System.Windows.Shapes.Path _leafR = new();
    private ScaleTransform? _leafLScale, _leafRScale;
    private readonly List<ScaleTransform> _petalScales = new();

    public AboutFlowerControl()
    {
        FlowDirection = System.Windows.FlowDirection.LeftToRight;   // drawn geometry: never mirrors under a right-to-left window
        // Transparent background so the whole 24×24 box is hit-testable, not just the petal geometry —
        // clicking anywhere on the mark plays it backwards / forwards again.
        var canvas = new Canvas { Width = 24, Height = 24, Background = Brushes.Transparent };
        canvas.Children.Add(_leafL);
        canvas.Children.Add(_leafR);
        Child = canvas;

        var parts = FlowerMark.BuildParts();
        if (parts is null) return;   // unexpected glyph → leave it empty rather than guess

        _leafL.Data = parts.Left;
        _leafR.Data = parts.Right;
        _leafL.Fill = Frozen(FlowerIcon.LeafLeftColor);
        _leafR.Fill = Frozen(FlowerIcon.LeafRightColor);

        // Leaves sprout from the plant's base (bottom-centre); transforms start collapsed (scale 0).
        _leafLScale = new ScaleTransform(0, 0, FlowerMark.CentreX, FlowerMark.LeafBaseY);
        _leafRScale = new ScaleTransform(0, 0, FlowerMark.CentreX, FlowerMark.LeafBaseY);
        _leafL.RenderTransform = _leafLScale;
        _leafR.RenderTransform = _leafRScale;

        // Each petal is its own Path scaling up from the blossom centre (sweep order: top, clockwise).
        var petalFill = Frozen(FlowerIcon.BlossomColor);
        foreach (var geo in parts.Petals)
        {
            var scale = new ScaleTransform(0, 0, parts.HeadCentre.X, parts.HeadCentre.Y);
            canvas.Children.Add(new System.Windows.Shapes.Path { Fill = petalFill, Data = geo, RenderTransform = scale });
            _petalScales.Add(scale);
        }

        Cursor = System.Windows.Input.Cursors.Hand;
        // Under Reduce Motion the click does nothing — falling through to the reduced path would make the
        // mark vanish and reappear, and a jump-cut is not a calmer animation, it's a worse one; this toy
        // has no purpose beyond the animation itself. The Loaded / visible calls below still run reduced,
        // because those have to leave the mark in its formed state.
        MouseLeftButtonDown += (_, _) => { if (!MotionPolicy.Reduce) Play(reverse: !_wilted); };

        Loaded          += (_, _) => Play(delayMs: AutoPlayDelayMs);
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) Play(delayMs: AutoPlayDelayMs); };
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>Hold the mark collapsed for this long before the self-started bloom begins (Loaded /
    /// becoming visible). The first-run wizard's finish step uses it to let the tray-highlight flight land
    /// first. A click always plays immediately, and Reduce Motion ignores it — there's no travel to defer.</summary>
    public double AutoPlayDelayMs { get; set; }

    /// <summary>How long a full bloom takes, from the leaves starting to the last petal settling (0 under
    /// Reduce Motion, where the mark simply appears). For a caller sequencing something after the bloom.</summary>
    public double BloomDurationMs => MotionPolicy.Reduce ? 0 : BloomTotalMs;

    /// <summary>Bloom now, whatever <see cref="AutoPlayDelayMs"/> says — for a caller that set a delay and
    /// then found the thing it was waiting on isn't happening.</summary>
    public void PlayNow() => Play();

    /// <summary>True once a reverse (wilt) run has been played, so the next click blooms it again.</summary>
    private bool _wilted;

    /// <summary>Bloom the mark in — or, with <paramref name="reverse"/>, wilt it back out in the mirror
    /// order (petals first, last-in-first-out, then the right leaf, then the left).</summary>
    private void Play(bool reverse = false, double delayMs = 0)
    {
        if (_leafLScale is null) return;
        _wilted = reverse;
        // Reduce Motion: the mark appears (or clears) fully formed — no bloom/wilt travel.
        if (MotionPolicy.Reduce)
        {
            double s = reverse ? 0 : 1;
            SetScale(_leafLScale, s);
            SetScale(_leafRScale!, s);
            foreach (var p in _petalScales) SetScale(p, s);
            return;
        }
        // Blooming springs out past its target; wilting eases back in the way it came.
        var ease = new BackEase { EasingMode = reverse ? EasingMode.EaseIn : EasingMode.EaseOut, Amplitude = 0.45 };
        double total = BloomTotalMs;

        // Reversing mirrors each piece's start time about the run's total length, so the animation runs
        // backwards as a whole rather than each piece merely playing its own scale in reverse.
        // The lead-in shifts the whole bloom later without changing its internal timing; a wilt run has no
        // caller that asks for one, so it never carries the offset.
        double lead = reverse ? 0 : Math.Max(0, delayMs);
        double At(double startMs, double moveMs) => lead + (reverse ? total - startMs - moveMs : startMs);

        SpringScale(_leafLScale,    At(0, LeafMove),       LeafMove, ease, reverse);
        SpringScale(_leafRScale!,   At(LeafGap, LeafMove), LeafMove, ease, reverse);
        for (int i = 0; i < _petalScales.Count; i++)
            SpringScale(_petalScales[i], At(SweepStart + i * PerPetal, PetalMove), PetalMove, ease, reverse);
    }

    // Bloom timing: the two leaves spring first, then the petals sweep one at a time.
    private const double LeafMove = 600, LeafGap = 440;
    private const double SweepStart = 880, PerPetal = 127, PetalMove = 480;

    private double BloomTotalMs => SweepStart + Math.Max(0, _petalScales.Count - 1) * PerPetal + PetalMove;

    private static void SetScale(ScaleTransform tr, double s)
    {
        tr.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        tr.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        tr.ScaleX = s; tr.ScaleY = s;
    }

    private static void SpringScale(ScaleTransform tr, double delayMs, double moveMs, IEasingFunction ease, bool reverse = false)
    {
        double from = reverse ? 1 : 0, to = reverse ? 0 : 1;
        tr.BeginAnimation(ScaleTransform.ScaleXProperty, FlowerMark.Delayed(from, to, Math.Max(0, delayMs), moveMs, ease));
        tr.BeginAnimation(ScaleTransform.ScaleYProperty, FlowerMark.Delayed(from, to, Math.Max(0, delayMs), moveMs, ease));
    }
}
