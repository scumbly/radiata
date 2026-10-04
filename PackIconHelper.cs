using System.Windows;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace ControllerWheel;

/// <summary>Creates frozen WPF DrawingImages from Material icon names (MahApps.Metro.IconPacks).</summary>
public static class PackIconHelper
{
    private static readonly SolidColorBrush DefaultInk;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    // Filled variants keyed on (name, brush identity) — callers pass shared frozen brushes, and some
    // (the delete-armed slice glyph) resolve per frame, so an uncached parse+build here is a hot path.
    private static readonly Dictionary<(string name, Brush fill), ImageSource?> FilledCache = new();

    public static readonly string[] AllNames =
        Enum.GetNames<PackIconMaterialKind>()
            .Where(n => n != "None")
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    static PackIconHelper()
    {
        DefaultInk = new SolidColorBrush(Color.FromArgb(215, 20, 20, 30));
        DefaultInk.Freeze();
    }

    /// <summary>Returns a frozen DrawingImage for a MaterialDesign icon name, or null if not found.
    /// Results are cached so the picker can repopulate without re-parsing geometry.</summary>
    public static ImageSource? GetCached(string name)
    {
        if (!Cache.TryGetValue(name, out var img))
            Cache[name] = img = FromName(name);
        return img;
    }

    /// <summary>Returns a frozen DrawingImage for a MaterialDesign icon name, or null if not found.
    /// Filled variants are cached per (name, brush) — pass a shared frozen brush, not a fresh one per
    /// call, or the cache degenerates into a leak.</summary>
    public static ImageSource? FromName(string? name, Brush? fill = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (fill is not null && fill.IsFrozen)
        {
            var key = (name, fill);
            if (!FilledCache.TryGetValue(key, out var cachedImg))
                FilledCache[key] = cachedImg = Build(name, fill);
            return cachedImg;
        }
        return Build(name, fill);
    }

    /// <summary>The app's own brand mark, addressable like an icon name so the Quit button and the Add
    /// picker's Radiata category ride the same cached pipeline. Deliberately not in <see cref="AllNames"/>
    /// — the user-facing icon catalog stays MDI-only, and the MDI "Flower" name still means the MDI glyph.</summary>
    public const string RadiataMarkName = "RadiataFlower";

    /// <summary>The Arcade Launcher's default glyph: the author's artwork (joystick, buttons and an "ARCADE" wordmark),
    /// its alpha filled with the slice's brush so it tints like an MDI glyph. Same contract as
    /// <see cref="RadiataMarkName"/>: addressable by name, not listed in <see cref="AllNames"/>. The wordmark
    /// is why <c>SliceLabelRule</c> treats it as artwork.</summary>
    public const string JoystickName = ArcadeCatalog.LauncherGlyphName;
    private const string LauncherGlyphAsset = "Assets/arcade/arcade-glyph.png";

    private static ImageSource? Build(string name, Brush? fill)
    {
        if (name.Trim().Equals(RadiataMarkName, StringComparison.OrdinalIgnoreCase))
            return BuildFromGeometry(FlowerIcon.Geometry(), fill);
        if (name.Trim().Equals(JoystickName, StringComparison.OrdinalIgnoreCase))
            return ArcadeArt.LoadPacked(LauncherGlyphAsset, "Arcade Launcher glyph") is { } glyph
                ? BuildFromMask(glyph, fill) : Build("GamepadVariant", fill);
        if (name.StartsWith(ArcadeCatalog.PackageGlyphPrefix, StringComparison.Ordinal))
            return BuildPackageGlyph(name[ArcadeCatalog.PackageGlyphPrefix.Length..], fill);
        if (!Enum.TryParse<PackIconMaterialKind>(name.Trim(), ignoreCase: true, out var kind) ||
            kind == PackIconMaterialKind.None) return null;
        try
        {
            var icon = new PackIconMaterial { Kind = kind };
            var data = icon.Data;
            if (string.IsNullOrEmpty(data)) return null;

            var geo = Geometry.Parse(data);
            geo.Freeze();
            return BuildFromGeometry(geo, fill);
        }
        catch { return null; }
    }

    private static readonly Dictionary<string, System.Windows.Media.Imaging.BitmapSource?> PackageMasks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A drop-in Arcade game's own glyph PNG as a tintable icon: the PNG's alpha is the shape, filled
    /// with the slice's brush exactly as an MDI glyph's geometry is, in the same 24×24 box (the picture scaled
    /// uniformly and centred). Decoded once per game, capped, from its consented package; a glyph that is gone
    /// or unreadable falls back to the shared drop-in glyph.</summary>
    private static ImageSource? BuildPackageGlyph(string gameId, Brush? fill)
    {
        if (!PackageMasks.TryGetValue(gameId, out var mask))
        {
            mask = ArcadeCatalog.Find(gameId)?.GlyphPath is { } path
                ? ArcadeArt.LoadFile(path, 256, $"package glyph ({gameId})") : null;
            PackageMasks[gameId] = mask;
        }
        return mask is null ? Build("ScriptText", fill) : BuildFromMask(mask, fill) ?? Build("ScriptText", fill);
    }

    /// <summary>A PNG's alpha as a tintable 24×24 icon, the picture scaled uniformly and centred.</summary>
    private static ImageSource? BuildFromMask(System.Windows.Media.Imaging.BitmapSource mask, Brush? fill)
    {
        try
        {
            var box = new RectangleGeometry(new Rect(0, 0, 24, 24));
            box.Freeze();
            var paint = new GeometryDrawing(fill ?? DefaultInk, null, box);
            var shape = new ImageBrush(mask) { Stretch = Stretch.Uniform };
            shape.Freeze();
            var group = new DrawingGroup { OpacityMask = shape };
            group.Children.Add(paint);
            group.Freeze();
            var img = new DrawingImage(group);
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    /// <summary>A frozen DrawingImage of a 24×24-box geometry over a transparent 24×24 backplate (the
    /// plate keeps every icon the same logical size regardless of the glyph's own bounds).</summary>
    private static ImageSource? BuildFromGeometry(Geometry geo, Brush? fill)
    {
        try
        {
            var brush = fill ?? DefaultInk;
            var drawing = new GeometryDrawing(brush, null, geo);
            drawing.Freeze();

            var bg = new RectangleGeometry(new Rect(0, 0, 24, 24));
            bg.Freeze();
            var bgDraw = new GeometryDrawing(Brushes.Transparent, null, bg);
            bgDraw.Freeze();

            var group = new DrawingGroup();
            group.Children.Add(bgDraw);
            group.Children.Add(drawing);
            group.Freeze();

            var img = new DrawingImage(group);
            img.Freeze();
            return img;
        }
        catch { return null; }
    }
}
