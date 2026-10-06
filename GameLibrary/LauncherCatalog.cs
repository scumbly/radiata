using System.Windows.Media;

namespace ControllerWheel;

/// <summary>One storefront/launcher the "launcher" action type can open: its key (stored in
/// ActionConfig.Command), editor display name, default Material glyph, special-cased brand tint, and
/// the InstalledGame.Storefront value its games carry (null = no directly-filterable games, e.g.
/// Playnite, which aggregates games tagged by their real store). Launch behaviour for each key lives
/// in WindowsPlatformActions.LaunchStorefront.</summary>
public readonly record struct LauncherInfo(string Key, string Display, string Glyph, Color Color, string? Storefront)
{
    /// <summary>Short name (before the em-dash), e.g. "Steam".</summary>
    public string ShortName => Display.Split('—')[0].Trim();

    /// <summary>The effective glyph (Defaults-tab override → brand default), tinted with the
    /// effective colour brightened so dark colours read on a dark surface.</summary>
    public ImageSource? BrightGlyph()
    {
        var color = ActionTint.EffectiveColor("launcher", Key);        // override → brand default
        var glyph = ActionIcons.Override("launcher:" + Key) ?? Glyph;  // override → brand default
        var c = Color.FromRgb((byte)((color.R + 255) / 2), (byte)((color.G + 255) / 2), (byte)((color.B + 255) / 2));
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return PackIconHelper.FromName(glyph, brush);
    }
}

/// <summary>The installed-storefront catalogue: key, display name, brand glyph and brand accent colour.
/// <para><b>The colours are BASE TONES and belong in HSL lightness ≈0.25–0.45</b>, like every other baked
/// base — see the note on <see cref="ActionTint.Defaults"/> for why. A launcher slice resolves its colour
/// through <see cref="ActionTint.EffectiveColor(string?,string?)"/> to the material-appropriate variant,
/// so an out-of-band base tone shows up as a mis-lightened glyph. Adjust lightness only; the hue is the
/// brand.</para></summary>
public static class LauncherCatalog
{
    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    // Only Steam (Big Picture URI) and Playnite (fullscreen exe) have a true big-picture mode on
    // Win10; Xbox gets window-maximize; the rest just open (no such mode exists). Display text says so.
    public static readonly LauncherInfo[] All =
    [
        new("steam",    "Steam — Big Picture",       "Steam",             Rgb( 26,  44,  90), "Steam"),    // dark blue
        new("playnite", "Playnite — Fullscreen",     "Controller",        Rgb(175,  70,  30), null),       // reddish orange (aggregator)
        new("xbox",     "Xbox — maximize (Win10)",   "MicrosoftXbox",     Rgb( 22, 121,  35), "Xbox"),     // xbox green (L .28 — see the base-lightness note below)
        new("gog",      "GOG Galaxy — open",         "Gog",               Rgb( 75,  35, 110), "GOG"),      // dark purple
        new("epic",     "Epic Games — open",         "Shield",            Rgb( 58,  60,  66), "Epic"),     // dark grey
        new("ubisoft",  "Ubisoft Connect — open",    "Ubisoft",           Rgb( 64,  66,  74), "Ubisoft"),  // slate grey (L .27)
        new("itch",     "itch.io — open",            "ControllerClassic", Rgb(162,  52,  42), "itch.io"),  // deep salmon-red (L .40)
        new("battlenet","Battle.net — open",         "RotateOrbit",       Rgb( 12,  80, 150), "Battle.net"),// Blizzard blue (orbit ~ bnet logo; no "Battlenet" glyph in this icon set)
        new("ea",       "EA — open",                 "AlphaECircle",      Rgb(170,  24,  34), "EA"),       // EA crimson (L .38); no brand glyph in this icon set, so the "EA" mark is spelled out
        new("amazon",   "Amazon Games — open",       "AlphaACircle",      Rgb(150,  85,  10), "Amazon"),   // dark amber (L .31 — Amazon's #FF9900 is far too light for a base tone); letter mark like EA
    ];

    public static LauncherInfo? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        foreach (var l in All)
            if (l.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) return l;
        return null;
    }

    /// <summary>True when the storefront's LAUNCHER APP is actually present on this machine — presence of
    /// its GAMES does not imply it (e.g. GOG games installed without GOG Galaxy). Gates everywhere a
    /// launcher entry is OFFERED: the slice editor's Storefront dropdown, the in-wheel Add picker's
    /// storefront sub-wheel, the Game Grid's "Open …" tile, and onboarding's starter-launcher seeding.
    /// Xbox is always true (the shell: AUMID launch has no probe-able exe). Probes are cached ~30 s.</summary>
    public static bool IsPresent(string? key)
    {
        if (Find(key) is not { } li) return false;
        return li.Key.ToLowerInvariant() switch
        {
            "xbox"     => true,
            "playnite" => PlayniteLibrary.IsAvailable,
            _          => li.Storefront is { } s && PresentStorefronts().Contains(s),
        };
    }

    private static HashSet<string>? _present;   // InstalledLaunchers() snapshot (registry/file probes)
    private static long _presentAt;
    private static HashSet<string> PresentStorefronts()
    {
        if (_present is null || Environment.TickCount64 - _presentAt > 30_000)
        {
            _present   = GameLibrary.InstalledLaunchers();
            _presentAt = Environment.TickCount64;
        }
        return _present;
    }

    /// <summary>Look up by the <see cref="LauncherInfo.Storefront"/> value that a scanned game carries
    /// (e.g. "Steam", "itch.io", "Battle.net") — for showing a storefront's brand glyph by name.</summary>
    public static LauncherInfo? FindByStorefront(string? store)
    {
        if (string.IsNullOrWhiteSpace(store)) return null;
        foreach (var l in All)
            if (l.Storefront is { } s && s.Equals(store, StringComparison.OrdinalIgnoreCase)) return l;
        return null;
    }
}
