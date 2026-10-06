using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ControllerWheel;

/// <summary>The Radiata app mark: the bespoke flower from Assets\flower-mark-*-unique.svg, embedded here
/// as path data and rendered on demand. Used for the tray icon (monochrome, light or dark to suit the
/// taskbar theme) and, via <see cref="Geometry"/> / the per-part geometries, as the SHAPE of the animated
/// About-screen logo, the wheels on/off toast (FlowerMark), and the Quit-button / Add-picker glyph
/// (PackIconHelper "RadiataFlower"). The exe/taskbar .ico is not generated here — Assets\radiata.ico is
/// built by tools\make-radiata-ico.ps1 from Assets\flower-mark-color-unique.png.</summary>
internal static class FlowerIcon
{
    // The mark's authored colours, verbatim from Assets\flower-mark-color-unique.svg. The About-screen
    // animation and the OOBE tray preview fill each part with these directly.
    public static readonly Color LeafLeftColor  = Color.FromRgb(0x3C, 0x40, 0x5B);   // ink blue
    public static readonly Color LeafRightColor = Color.FromRgb(0x81, 0xB2, 0x9A);   // sage green
    public static readonly Color BlossomColor   = Color.FromRgb(0xE0, 0x7A, 0x5F);   // terracotta

    // Path data verbatim from Assets\flower-mark-color-unique.svg (1024×1024 box; normalised to the app's
    // 24×24 icon convention by the transform below). The blossom is a centre disc plus six fused petal
    // circles in ONE path — it needs the nonzero fill rule (F1) or the disc punches a hole in the ring.
    private const string LeftLeafData =
        "F1 M 54.49902 745.43457 C 173.89679 991.723999 186.51152 1030.789551 491.349915 1009.630127 " +
        "C 327.588348 718.443726 304.874908 737.512573 54.887352 746.757996";
    private const string RightLeafData =
        "F1 M 971.584351 745.780579 C 851.332214 991.653931 838.581909 1030.675415 533.818848 1008.457825 " +
        "C 698.590393 717.841675 721.237488 736.98938 971.191467 747.102661";
    private const string BlossomData =
        "F1 M 511.577881 282.323303 C 596.360779 282.323303 665.09082 351.053345 665.09082 435.836243 " +
        "C 665.09082 520.619019 596.360779 589.34906 511.577881 589.34906 C 426.795135 589.34906 " +
        "358.064941 520.619019 358.064941 435.836243 C 358.064941 351.053345 426.795135 282.323303 " +
        "511.577881 282.323303 M 118.584816 573.997742 C 118.584816 658.78064 187.314911 727.510742 " +
        "272.097687 727.510742 C 304.642517 727.510742 334.730988 717.071838 358.064941 700.492432 " +
        "C 358.064941 704.176758 358.064941 707.861084 358.064941 712.159424 C 358.064941 796.942139 " +
        "426.795135 865.672241 511.577881 865.672241 C 596.360779 865.672241 665.09082 796.942139 " +
        "665.09082 712.159424 C 665.09082 707.861084 665.09082 704.176758 665.09082 700.492432 " +
        "C 688.424805 717.071838 718.513245 727.510742 751.057922 727.510742 C 835.797241 727.510742 " +
        "904.571045 658.736938 904.571045 573.997742 C 904.571045 512.592651 868.341736 460.398193 " +
        "816.761353 435.836243 C 868.341736 411.27417 904.571045 358.465576 904.571045 297.6745 " +
        "C 904.571045 212.935364 835.797241 144.161743 751.057922 144.161743 C 718.513245 144.161743 " +
        "688.424805 153.98645 665.09082 171.179871 C 665.09082 167.495728 665.09082 163.197266 " +
        "665.09082 159.512939 C 665.09082 74.730042 596.360779 6 511.577881 6 C 426.795135 6 " +
        "358.064941 74.730042 358.064941 159.512939 C 358.064941 163.197266 358.064941 167.495728 " +
        "358.064941 171.179871 C 334.730988 153.98645 304.642517 144.161743 272.097687 144.161743 " +
        "C 187.314911 144.161743 118.584816 212.891602 118.584816 297.6745 C 118.584816 358.465576 " +
        "154.813873 411.27417 206.394226 435.836243 C 154.813873 460.398193 118.584816 512.592651 " +
        "118.584816 573.997742";

    private static readonly Transform NormaliseTo24 = CreateNormalise();

    private static Transform CreateNormalise()
    {
        var t = new ScaleTransform(24.0 / 1024.0, 24.0 / 1024.0);
        t.Freeze();
        return t;
    }

    private static Geometry Part(string data)
    {
        // Geometry.Parse returns a frozen StreamGeometry — clone before attaching the transform.
        var g = System.Windows.Media.Geometry.Parse(data).Clone();
        g.Transform = NormaliseTo24;
        g.Freeze();
        return g;
    }

    /// <summary>The mark's parts on the 24×24 viewbox, for the About screen / toast entrance animation
    /// (FlowerMark) and per-part colouring.</summary>
    internal static readonly Geometry LeftLeaf  = Part(LeftLeafData);
    internal static readonly Geometry RightLeaf = Part(RightLeafData);
    internal static readonly Geometry Blossom   = Part(BlossomData);

    /// <summary>The whole mark as one geometry on a 24×24 viewbox — for silhouette renderings (tray icon,
    /// Quit button, OOBE resting glyph).</summary>
    internal static Geometry Geometry()
    {
        var g = new GeometryGroup { FillRule = FillRule.Nonzero };
        g.Children.Add(LeftLeaf);
        g.Children.Add(RightLeaf);
        g.Children.Add(Blossom);
        g.Freeze();
        return g;
    }

    /// <summary>Render the flower to a square premultiplied-BGRA bitmap of <paramref name="size"/> px.</summary>
    private static BitmapSource Render(int size, Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze();
        var geo = Geometry();
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double margin = size * 0.08;                  // small breathing room inside the icon
            double scale  = (size - 2 * margin) / 24.0;
            dc.PushTransform(new TranslateTransform(margin, margin));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawGeometry(brush, null, geo);
            dc.Pop(); dc.Pop();
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static byte[] Png(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Assemble a PNG-compressed .ico (Vista+) holding the flower at several sizes.</summary>
    public static byte[] BuildIco(Color color, params int[] sizes)
    {
        var frames = sizes.Select(s => (size: s, data: Png(Render(s, color)))).ToArray();
        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);
        w.Write((short)0);              // reserved
        w.Write((short)1);              // type: icon
        w.Write((short)frames.Length);  // image count
        int offset = 6 + 16 * frames.Length;
        foreach (var (size, data) in frames)
        {
            w.Write((byte)(size >= 256 ? 0 : size));   // width  (0 ⇒ 256)
            w.Write((byte)(size >= 256 ? 0 : size));   // height (0 ⇒ 256)
            w.Write((byte)0);     // palette count
            w.Write((byte)0);     // reserved
            w.Write((short)1);    // colour planes
            w.Write((short)32);   // bits per pixel
            w.Write(data.Length); // image byte size
            w.Write(offset);      // image offset
            offset += data.Length;
        }
        foreach (var (_, data) in frames) w.Write(data);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>A tray icon: a monochrome flower, near-white on a dark taskbar / near-black on a light
    /// one, so it stays legible either way.</summary>
    public static System.Drawing.Icon TrayIcon()
    {
        var color = TaskbarIsLight()
            ? Color.FromRgb(0x2A, 0x2A, 0x30)    // dark flower on a light taskbar
            : Color.FromRgb(0xF2, 0xF2, 0xF2);   // light flower on a dark taskbar
        using var ms = new MemoryStream(BuildIco(color, 16, 20, 24, 32));
        return new System.Drawing.Icon(ms);
    }

    /// <summary>Windows taskbar/tray theme: true when the system uses the light theme for the taskbar.</summary>
    private static bool TaskbarIsLight()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
        catch { return false; }   // default to dark-taskbar assumption (light flower)
    }
}
