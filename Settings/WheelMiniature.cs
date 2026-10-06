using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Tiny radial diagram of a wheel with N wedges, one highlighted — shown in the settings
/// slice list so each row indicates which slice position it occupies. Matches the live wheel's
/// orientation: slice 0 at 12 o'clock, going clockwise.</summary>
public sealed class WheelMiniature : FrameworkElement
{
    /// <summary>Pinned left-to-right: a miniature of the wheel is geometry keyed to the physical stick, so it
    /// must not mirror inside a right-to-left Settings window (see RadialMenuControl).</summary>
    public WheelMiniature() { FlowDirection = System.Windows.FlowDirection.LeftToRight; }

    private static readonly Brush SliceFill     = Frozen(Color.FromArgb(255, 222, 222, 226)); // light grey
    private static readonly Brush HighlightFill = Frozen(Color.FromArgb(255, 150, 150, 160)); // darker grey
    private static readonly Pen   GapPen        = FrozenPen(Color.FromArgb(255, 252, 252, 253), 1.1);

    public static readonly DependencyProperty SliceCountProperty =
        DependencyProperty.Register(nameof(SliceCount), typeof(int), typeof(WheelMiniature),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public int SliceCount { get => (int)GetValue(SliceCountProperty); set => SetValue(SliceCountProperty, value); }

    public static readonly DependencyProperty HighlightIndexProperty =
        DependencyProperty.Register(nameof(HighlightIndex), typeof(int), typeof(WheelMiniature),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));
    public int HighlightIndex { get => (int)GetValue(HighlightIndexProperty); set => SetValue(HighlightIndexProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        int n = SliceCount;
        if (n <= 0) return;

        double r = Math.Min(ActualWidth, ActualHeight) / 2 - 1;
        if (r <= 0) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);

        if (n == 1)
        {
            dc.DrawEllipse(HighlightIndex == 0 ? HighlightFill : SliceFill, GapPen, c, r, r);
            return;
        }

        double sliceDeg = 360.0 / n;
        for (int i = 0; i < n; i++)
        {
            double start = sliceDeg * i - 90 - sliceDeg / 2.0;   // slice 0 centred at 12 o'clock
            dc.DrawGeometry(i == HighlightIndex ? HighlightFill : SliceFill, GapPen,
                            Wedge(c, r, start, sliceDeg));
        }

        // Centre cut-out in the gap colour so the wedges read as a ring (the live wheel's hub), not a pie.
        double hole = r * HoleRatio;
        dc.DrawEllipse(GapPen.Brush, GapPen, c, hole, hole);
    }

    private const double HoleRatio = 0.34;

    private static Geometry Wedge(Point c, double r, double startDeg, double sweepDeg)
    {
        static double Rad(double d) => d * Math.PI / 180.0;
        var p0 = new Point(c.X + r * Math.Cos(Rad(startDeg)),            c.Y + r * Math.Sin(Rad(startDeg)));
        var p1 = new Point(c.X + r * Math.Cos(Rad(startDeg + sweepDeg)), c.Y + r * Math.Sin(Rad(startDeg + sweepDeg)));

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(c, isFilled: true, isClosed: true);
            ctx.LineTo(p0, isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(p1, new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        sg.Freeze();
        return sg;
    }

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    private static Pen FrozenPen(Color c, double w) { var p = new Pen(new SolidColorBrush(c), w); p.Freeze(); return p; }
}
