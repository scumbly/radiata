using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>Bitmap plumbing shared by the arcade picker and its screenshot store: decode once, freeze, and
/// latch a failure so a missing or hostile file is never re-probed from the render pump.
///
/// <para>Nothing here runs in a static initializer — <c>pack://</c> is not registered until WPF's
/// <c>Application</c> exists, and a Uri built during static init throws a <c>TypeInitializationException</c>
/// that takes the class down with it (the trap <see cref="ArcadeChrome.Face"/> documents).</para></summary>
internal static class ArcadeArt
{
    /// <summary>Where the cabinet art ships. Shared with the replaceable in-game sprites, which are packed by
    /// the same csproj wildcard — the cabinets need no entry of their own. ⚠ They are not
    /// <c>ArcadeSprites</c> slots: that class looks its art up by slot name and never enumerates the folder,
    /// so these sit alongside without being part of that contract.</summary>
    private const string CabinetDir = "Assets/arcade/sprites/sprites-finished";

    public const string CabinetAsset = CabinetDir + "/cabinet-blank.png";

    /// <summary>Native proportions of <see cref="CabinetAsset"/> (506×944). The screen-ellipse and nameplate
    /// fractions in <see cref="ArcadePickerTuning"/> and the silhouette outline in
    /// <see cref="ArcadePickerRenderer"/> were measured against this art; a replacement PNG must keep the
    /// same proportions or those move with it.</summary>
    public const double CabinetAspect = 386.0 / 720.0;

    private static readonly Dictionary<string, BitmapSource?> Packed = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConditionalWeakTable<BitmapSource, ImageBrush> Brushes = new();
    private static readonly ConditionalWeakTable<BitmapSource, BitmapSource> Grays = new();

    /// <summary>A frozen greyscale copy of a bitmap, converted once. Drawing it under the colour original at
    /// reduced opacity is how a drawing context desaturates (there is no saturation filter on it).
    /// Transparent pixels come out black, so callers clip to the shape they fill.</summary>
    public static BitmapSource GrayOf(BitmapSource bmp) =>
        Grays.GetValue(bmp, static b =>
        {
            try
            {
                var g = new FormatConvertedBitmap(b, PixelFormats.Gray8, null, 0);
                g.Freeze();
                return g;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Arcade] grey conversion failed: {ex.Message}");
                return b;   // colour stands in; the picture still shows
            }
        });

    /// <summary>The blank cabinet — what a game without art of its own stands in, and what the Arcade
    /// Launcher's wheel hub shows.</summary>
    public static BitmapSource? Cabinet => LoadPacked(CabinetAsset, "blank cabinet");

    /// <summary>A game's own cabinet art, or the blank one when it has none — tinted when the game names a
    /// cabinet colour (a drop-in package's <c>tint</c>). Every probe is latched by <see cref="LoadPacked"/>, so a
    /// game without art costs one lookup for the life of the process, not one per frame.</summary>
    public static BitmapSource? CabinetFor(string gameId, string? tintHex = null) =>
        OwnCabinet(gameId) ?? (tintHex is null ? Cabinet : TintedCabinet(tintHex));

    /// <summary>The colour the blank cabinet's nameplate comes out in under a tint, for choosing the title's
    /// ink. The nameplate is the art's flat light grey (216), multiplied like the rest of the body.</summary>
    public static Color NameplateColor(string? tintHex)
    {
        const double plate = 216 / 255.0;
        var t = TintColor(tintHex) ?? Color.FromRgb(255, 255, 255);
        return Color.FromRgb((byte)(t.R * plate), (byte)(t.G * plate), (byte)(t.B * plate));
    }

    private static Color? TintColor(string? hex)
    {
        if (hex is null) return null;
        try { return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return null; }
    }

    private static readonly Dictionary<string, BitmapSource?> Tinted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The blank cabinet with every pixel's grey multiplied by the tint: its own shading (the lit top,
    /// the front, the shadowed side) survives as light and dark of the chosen colour, and the transparent
    /// screen stays transparent. Built once per colour and frozen; an unparseable colour falls back to the
    /// plain cabinet.</summary>
    private static BitmapSource? TintedCabinet(string tintHex)
    {
        if (Tinted.TryGetValue(tintHex, out var hit)) return hit;
        BitmapSource? result = Cabinet;
        try
        {
            if (Cabinet is { } blank && TintColor(tintHex) is { } t)
            {
                var conv = new FormatConvertedBitmap(blank, PixelFormats.Bgra32, null, 0);
                int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
                var px = new byte[stride * h];
                conv.CopyPixels(px, stride, 0);
                for (int i = 0; i < px.Length; i += 4)
                {
                    px[i]     = (byte)(px[i]     * t.B / 255);
                    px[i + 1] = (byte)(px[i + 1] * t.G / 255);
                    px[i + 2] = (byte)(px[i + 2] * t.R / 255);
                }
                var bmp = BitmapSource.Create(w, h, blank.DpiX, blank.DpiY, PixelFormats.Bgra32, null, px, stride);
                bmp.Freeze();
                result = bmp;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] cabinet tint {tintHex} failed: {ex.Message}");
        }
        Tinted[tintHex] = result;
        return result;
    }

    private static readonly Dictionary<string, BitmapSource?> PackageImages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A drop-in package's own picture (its cabinet badge), decoded once, capped at 512 px on the long
    /// side, and latched — a missing or unreadable file costs the picture, is traced once, and is never
    /// re-probed from the render pump. The file is consented package content, inside the folder caps.</summary>
    public static BitmapSource? PackageImage(string path)
    {
        if (PackageImages.TryGetValue(path, out var hit)) return hit;
        var img = LoadFile(path, 512, "package image");
        PackageImages[path] = img;
        return img;
    }

    /// <summary>Whether this game brought a cabinet of its own. The picker prints the title on a machine
    /// that did not — art of its own names the game on its marquee, the blank cabinet cannot.</summary>
    public static bool HasOwnCabinet(string gameId) => OwnCabinet(gameId) is not null;

    private static BitmapSource? OwnCabinet(string gameId) =>
        string.IsNullOrWhiteSpace(gameId) ? null
            : LoadPacked($"{CabinetDir}/cabinet-{gameId}.png", $"cabinet art ({gameId})");

    /// <summary>The colour a game's own cabinet art is mostly painted in — what the picker's horizon glow,
    /// bezel and screen glow take, so they agree with the machine on screen. A saturation-weighted mean of
    /// the opaque pixels (grey line work and shadow carry no weight), lifted to a lit value. Null when the
    /// game has no art of its own, or the art is all grey. Computed once per game, at open.</summary>
    public static Color? DominantColor(string gameId)
    {
        if (Dominant.TryGetValue(gameId, out var known)) return known;
        Color? result = null;
        try
        {
            if (OwnCabinet(gameId) is { } bmp) result = Dominant_(bmp);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] cabinet colour ({gameId}) unreadable: {ex.Message}");
        }
        Dominant[gameId] = result;
        return result;
    }

    private static readonly Dictionary<string, Color?> Dominant = new(StringComparer.OrdinalIgnoreCase);

    private static Color? Dominant_(BitmapSource bmp)
    {
        var conv = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
        conv.Freeze();
        int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        conv.CopyPixels(px, stride, 0);

        double r = 0, g = 0, b = 0, weight = 0;
        for (int y = 0; y < h; y += 2)
        for (int x = 0; x < w; x += 2)
        {
            int i = y * stride + x * 4;
            byte bb = px[i], gg = px[i + 1], rr = px[i + 2], a = px[i + 3];
            if (a < 200) continue;
            int max = Math.Max(rr, Math.Max(gg, bb)), min = Math.Min(rr, Math.Min(gg, bb));
            if (max < 40) continue;                       // shadow and outline
            double sat = (max - min) / (double)max;
            if (sat < 0.20) continue;                     // grey line work
            double k = sat * sat;
            r += rr * k; g += gg * k; b += bb * k; weight += k;
        }
        if (weight <= 0) return null;
        r /= weight; g /= weight; b /= weight;
        // Lifted so the brightest channel sits at a glow value, ratios kept: the hue is the art's, the
        // brightness is the light's.
        double lift = 0xE0 / Math.Max(1, Math.Max(r, Math.Max(g, b)));
        return Color.FromRgb((byte)Math.Min(255, r * lift), (byte)Math.Min(255, g * lift), (byte)Math.Min(255, b * lift));
    }

    /// <summary>A packed application resource, decoded once and frozen. Null (and cached as null) when the
    /// resource is missing or undecodable, so callers paint nothing rather than failing the frame.</summary>
    public static BitmapSource? LoadPacked(string assetPath, string label)
    {
        if (Packed.TryGetValue(assetPath, out var hit)) return hit;
        BitmapSource? bmp = null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource   = new Uri($"pack://application:,,,/{assetPath}");
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            bmp = img;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] {label} unavailable: {ex.Message}");
        }
        Packed[assetPath] = bmp;
        return bmp;
    }

    /// <summary>Decode a PNG from disk without keeping the file open, bounded to <paramref name="maxEdgePx"/>
    /// on its longer side so a hostile oversized file cannot balloon memory. The header is read first and
    /// the down-scale requested only when needed, so a normal-sized file decodes at native size. Null on any
    /// failure; not cached here — the caller decides when a file is stale.</summary>
    public static BitmapSource? LoadFile(string path, int maxEdgePx, string label)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            var header = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation,
                                              BitmapCacheOption.None);
            int w = header.Frames[0].PixelWidth, h = header.Frames[0].PixelHeight;
            if (w <= 0 || h <= 0) return null;
            fs.Position = 0;

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption   = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            img.StreamSource  = fs;
            if (w >= h && w > maxEdgePx) img.DecodePixelWidth  = maxEdgePx;
            else if (h > w && h > maxEdgePx) img.DecodePixelHeight = maxEdgePx;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] {label} unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>One frozen default-stretch <see cref="ImageBrush"/> per bitmap, for opacity masks and tint
    /// fills. Never allocate an ImageBrush on a per-frame path; ask here.</summary>
    public static ImageBrush BrushOf(BitmapSource bmp) =>
        Brushes.GetValue(bmp, static b => { var br = new ImageBrush(b); br.Freeze(); return br; });
}
