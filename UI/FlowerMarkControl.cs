using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ControllerWheel;

/// <summary>The Radiata flower mark, scaled to a target size and animated to spring in (Forward = true)
/// or collapse (Forward = false, the exact time-reverse). Used by the wheels-enabled toast — forward for
/// "On", reverse for "Off". Set <see cref="MarkBrush"/> for the fill (white on dark, black on light).
/// The same petal-sweep entrance as the About screen, on a compact schedule that fits the toast.</summary>
internal sealed class FlowerMarkControl : Canvas
{
    private readonly List<ScaleTransform> _scales = new();
    private readonly List<Path>           _paths  = new();
    private readonly List<double>         _start  = new();   // per-piece forward start (ms, pre-speedup)
    private readonly List<double>         _move   = new();   // per-piece move duration (ms, pre-speedup)

    private const double Speed = 1.25;                       // play 25% faster than the authored schedule

    /// <summary>Total play time (ms) at the sped-up rate — the caller uses it to sequence the toast.</summary>
    public double PlayDurationMs { get; private set; }

    private Brush _brush = Brushes.Black;
    public Brush MarkBrush
    {
        get => _brush;
        set { _brush = value; foreach (var p in _paths) p.Fill = value; }
    }

    public FlowerMarkControl(double size)
    {
        // Pinned left-to-right: drawn geometry never mirrors under a right-to-left window. Under an RTL
        // parent WPF composes that pin as a flip about the arranged slot, so the glyph-box scale must be a
        // LayoutTransform (part of the arranged size) — a RenderTransform from (0,0) lands the mark a full
        // width outside its slot, over the toast text.
        FlowDirection = System.Windows.FlowDirection.LeftToRight;
        Width = Height = FlowerMark.BoxSize;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment   = VerticalAlignment.Center;

        LayoutTransform = new ScaleTransform(size / FlowerMark.BoxSize, size / FlowerMark.BoxSize);

        var parts = FlowerMark.BuildParts();
        if (parts is null) return;

        // Compact schedule (total ≈ 0.8s, well inside the toast's lifetime): leaves sprout, then the
        // petals sweep in one at a time around the circle.
        AddPiece(parts.Left,  new Point(FlowerMark.CentreX, FlowerMark.LeafBaseY), 0,   280);
        AddPiece(parts.Right, new Point(FlowerMark.CentreX, FlowerMark.LeafBaseY), 100, 280);
        for (int i = 0; i < parts.Petals.Count; i++)
            AddPiece(parts.Petals[i], parts.HeadCentre, 270 + i * 48, 220);

        double raw = 0;
        for (int i = 0; i < _start.Count; i++) raw = Math.Max(raw, _start[i] + _move[i]);
        PlayDurationMs = raw / Speed;
    }

    private void AddPiece(Geometry geo, Point centre, double startMs, double moveMs)
    {
        var scale = new ScaleTransform(0, 0, centre.X, centre.Y);
        var path  = new Path { Data = geo, Fill = _brush, RenderTransform = scale };
        Children.Add(path);
        _paths.Add(path); _scales.Add(scale); _start.Add(startMs); _move.Add(moveMs);
    }

    /// <summary>Spring every piece 0→1 (forward) or 1→0 as the exact time-reverse (collapse), starting
    /// <paramref name="startOffsetMs"/> after now (lets the toast circle scale in first).</summary>
    public void Play(bool forward, double startOffsetMs = 0)
    {
        if (_scales.Count == 0) return;
        // Reduce Motion: the mark shows/clears fully formed — the toast's own fade still frames it.
        if (MotionPolicy.Reduce)
        {
            foreach (var sc in _scales)
            {
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                sc.ScaleX = sc.ScaleY = forward ? 1 : 0;
            }
            return;
        }
        var ease = new BackEase { EasingMode = forward ? EasingMode.EaseOut : EasingMode.EaseIn, Amplitude = 0.35 };
        for (int i = 0; i < _scales.Count; i++)
        {
            double s = _start[i] / Speed, m = _move[i] / Speed;
            double from  = forward ? 0 : 1, to = forward ? 1 : 0;
            double delay = startOffsetMs + (forward ? s : PlayDurationMs - (s + m));   // mirror in time
            _scales[i].BeginAnimation(ScaleTransform.ScaleXProperty, FlowerMark.Delayed(from, to, delay, m, ease));
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, FlowerMark.Delayed(from, to, delay, m, ease));
        }
    }
}
