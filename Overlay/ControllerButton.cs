using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>A single on-screen controller-button prompt for use in XAML (e.g. the Game Grid hint strip).
/// Renders via the shared <see cref="ControllerButtons"/> renderer, so it's a glossy-dark button with a
/// geometric PlayStation symbol (or Xbox letter) regardless of the app's chosen material. Set
/// <c>Button</c> and a <c>Height</c>; the width is derived from the button's aspect (round vs pill).</summary>
public sealed class ControllerButton : FrameworkElement
{
    public static readonly DependencyProperty ButtonProperty = DependencyProperty.Register(
        nameof(Button), typeof(PadButton), typeof(ControllerButton),
        new FrameworkPropertyMetadata(PadButton.Cross,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public PadButton Button
    {
        get => (PadButton)GetValue(ButtonProperty);
        set => SetValue(ButtonProperty, value);
    }

    // Redraw when the glyph set flips (controller swap / Advanced override). Subscribe only while loaded so
    // the static event never pins a detached element.
    public ControllerButton()
    {
        // Pinned: the Xbox face letters are drawn text and would render mirrored under an RTL ancestor —
        // a button glyph is not prose.
        FlowDirection = System.Windows.FlowDirection.LeftToRight;
        Loaded   += (_, _) => ControllerButtons.Changed += OnGlyphSetChanged;
        Unloaded += (_, _) => ControllerButtons.Changed -= OnGlyphSetChanged;
    }

    private void OnGlyphSetChanged() { InvalidateMeasure(); InvalidateVisual(); }

    protected override Size MeasureOverride(Size availableSize)
    {
        double h = !double.IsNaN(Height) ? Height
                 : double.IsInfinity(availableSize.Height) ? 22.0 : availableSize.Height;
        return new Size(ControllerButtons.Width(Button, h), h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        ControllerButtons.Draw(dc, new Rect(0, 0, ActualWidth, ActualHeight), Button, ppd);
    }
}
