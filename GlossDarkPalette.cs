using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Muted-teal tones for the Gloss Dark material. The body colours (Sheen…Bounce) are shared by
/// both the wheel slices (<see cref="RadialMenuControl"/>) and the Game Grid card
/// (<see cref="GameBrowserControl"/>) so the two read as the same hue. The Edge/Glow chrome is the grid's
/// liquid-glass edge only — the wheel deliberately keeps its own chrome (white dome rim, no edge glow).</summary>
internal static class GlossDarkPalette
{
    public static readonly Color Sheen        = Rgb(66, 92, 102);  // top-lit cool sheen
    public static readonly Color UpperMid     = Rgb(34, 54,  62);
    public static readonly Color AboveHorizon = Rgb(14, 30,  38);  // just above the horizon
    public static readonly Color DarkCut      = Rgb( 2,  9,  13);  // darkest, just below the horizon
    public static readonly Color Body         = Rgb( 8, 19,  24);
    public static readonly Color Bounce       = Rgb(18, 34,  42);  // faint bottom bounce

    // Chrome shared by both surfaces' liquid-glass edge treatment.
    public static readonly Color Edge = Rgb(207, 242, 251);        // bright cool-white "lit lip" highlight
    public static readonly Color Glow = Rgb( 88, 128, 140);        // muted-teal outer halo

    /// <summary>The colour with a specific alpha (each surface tunes opacity per stop).</summary>
    public static Color A(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);
}
