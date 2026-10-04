using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Human colour descriptions for narration — "dark blue color", "muted brown color". A hex
/// string read aloud is noise, so UIA names for text-less swatches route through here (the hex moves
/// to the visible tooltip). Nearest-bucket HSL naming, deliberately coarse: the goal is telling chips
/// apart by ear, not colorimetry.
/// <para>Every description is a whole phrase from a closed set (<see cref="AllKeys"/>), never
/// modifier + hue + " color" glued at the call site: a translation of "dark blue" is not "dark" + "blue"
/// in Spanish (postposed, gendered), German (compounded) or Japanese (linked), so the finished English
/// phrase is the unit that gets looked up.</para></summary>
public static class ColorNamer
{
    public static string Describe(Color c)
    {
        var (h, s, l) = ToHsl(c);

        if (s < 0.10)
            return l switch
            {
                < 0.08 => "black color",
                < 0.30 => "dark gray color",
                < 0.62 => "gray color",
                < 0.90 => "light gray color",
                _      => "white color",
            };

        // Brown is dark orange — name it before the generic hue ladder or it reads "dark orange".
        string hue = h is >= 15 and < 50 && l < 0.42 ? "brown" : HueName(h);
        return Phrase(hue, Modifier(hue, s, l));
    }

    private enum Mod { None, Dark, Light, Muted }

    private static Mod Modifier(string hue, double s, double l) =>
        hue != "brown" && l < 0.28 ? Mod.Dark
        : l > 0.72                 ? Mod.Light
        : s < 0.35                 ? Mod.Muted
        :                            Mod.None;

    private static string Phrase(string hue, Mod m) => m switch
    {
        Mod.Dark  => $"dark {hue} color",
        Mod.Light => $"light {hue} color",
        Mod.Muted => $"muted {hue} color",
        _         => $"{hue} color",
    };

    private static readonly string[] Hues =
        ["red", "orange", "yellow", "yellow-green", "green", "teal", "cyan", "blue", "purple", "magenta", "pink"];

    /// <summary>Every phrase <see cref="Describe"/> can produce — the translatable set. Brown is reachable
    /// only below l = 0.42 and never takes "dark", so it has two forms; the eleven hues have four each; five
    /// achromatic phrases round it out.</summary>
    public static IEnumerable<string> AllKeys()
    {
        yield return "black color"; yield return "dark gray color"; yield return "gray color";
        yield return "light gray color"; yield return "white color";
        foreach (var hue in Hues)
            foreach (var m in new[] { Mod.None, Mod.Dark, Mod.Light, Mod.Muted })
                yield return Phrase(hue, m);
        yield return Phrase("brown", Mod.None);
        yield return Phrase("brown", Mod.Muted);
    }

    private static string HueName(double h) => h switch
    {
        < 15  => "red",
        < 40  => "orange",
        < 65  => "yellow",
        < 90  => "yellow-green",
        < 150 => "green",
        < 180 => "teal",
        < 200 => "cyan",
        < 250 => "blue",
        < 290 => "purple",
        < 330 => "magenta",
        < 345 => "pink",
        _     => "red",
    };

    private static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0, d = max - min;
        if (d < 1e-9) return (0, 0, l);
        double s = d / (1 - Math.Abs(2 * l - 1));
        double h = max == r ? ((g - b) / d % 6 + 6) % 6
                 : max == g ? (b - r) / d + 2
                 :            (r - g) / d + 4;
        return (h * 60, s, l);
    }
}
