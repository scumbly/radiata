using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Default glyph + accent colour for the in-wheel Add picker's first-level categories and its
/// grouped sub-wheels (Audio/Display/…). Override-aware via the Color Defaults window using the keys
/// "category:&lt;header&gt;" and "group:&lt;name&gt;" (glyph through <see cref="ActionIcons"/>, colour through
/// <see cref="ActionTint"/>). Shared by the picker (App.BuildAddMenu) and the Defaults editor so the
/// built-in defaults live in exactly one place.</summary>
internal static class AddMenuIcons
{
    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    // First-level category accent glyph + colour.
    private static readonly Dictionary<string, (string Glyph, Color Color)> Cats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Games & Apps"] = ("RocketLaunch", C(0x14, 0x6A, 0x5E)),   // teal-green
        // ⚠ The category key is the dictionary key (WheelEditorControl.Categories' Key drives
        // CategoryGlyph/Color lookups directly) — renaming a category key means moving it here
        // too, or that tab silently falls back to CatFallback. The header may change or translate freely;
        // the key never does, because "category:<key>" is also the persisted override key.
        ["Chat & Streaming"] = ("Headset", C(0x32, 0x65, 0xA9)),   // blue
        ["System"]    = ("Cog",          C(0x26, 0x4C, 0x80)),   // steel blue
        ["Custom"]    = ("Tune",         C(0x5A, 0x38, 0x78)),   // violet
        ["Radiata"]   = (PackIconHelper.RadiataMarkName, C(0x54, 0x40, 0x8C)),   // the app's own flower mark, violet — its actions, not a power red
        // "Arcade" is a child group of Radiata (group maps below), not a top-level category.
    };
    private static readonly (string Glyph, Color Color) CatFallback = ("DotsHorizontal", C(0x3C, 0x3E, 0x4A));

    // Grouped sub-wheel glyphs (their colour inherits the parent category unless a per-group default or a
    // user override is set).
    private static readonly Dictionary<string, string> GroupGlyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Audio"]      = "VolumeHigh",
        ["Display"]    = "Monitor",
        ["Power"]      = "Power",
        ["Controller"] = "Controller",
        ["Game Bar"]   = "MicrosoftXbox",
        // "Recording" is TypeEntry.Hidden (see WheelEditorControl.Categories); BuildAddMenu's
        // `if (e.Hidden) continue;` skips it before this glyph lookup is ever consulted — the entry is
        // kept only in case Recording is un-hidden later.
        ["Recording"]  = "RecordRec",
        ["OBS Studio"] = "Broadcast",
        ["Discord"]    = "MicrosoftXboxController",   // a group inside Chat & Streaming
        ["Steam Chat"]       = "Steam",
        ["Xbox Party Chat"]  = "HeadsetDock",
        // "Windows": Show Desktop / Do Not Disturb / Empty Recycle Bin — "OS chrome" actions grouped
        // under System. MicrosoftWindows is a standard Material Design Icons glyph name; if this
        // icon pack build ever lacks it, PackIconHelper.FromName's Enum.TryParse fails soft (returns null),
        // and the caller (SliceEditModel.Preview / MonoIcons.ForSlice) already has its own generic fallback
        // — no crash either way.
        ["Windows"]    = "MicrosoftWindows",
        // Arcade — a group under Radiata. Only ever looked up while Arcade.Available offers the group.
        ["Arcade"]     = PackIconHelper.JoystickName,   // the same glyph the Arcade Launcher slice defaults to (MonoIcons)
    };

    // Per-group default accent colour (baked). Groups not listed inherit the parent category's colour.
    private static readonly Dictionary<string, Color> GroupColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Display"]   = C(0x16, 0x62, 0x74),   // cyan
        ["Power"]     = C(0x88, 0x30, 0x2C),   // warm red — matches the power/exit commands inside it
        ["Recording"] = C(0x88, 0x30, 0x2C),   // record red
        ["Game Bar"]  = C(0x2E, 0x68, 0x2E),   // xbox green
        ["Steam Chat"]      = C(0x2A, 0x47, 0x5E),   // steam slate-blue — matches the steam-mute type tint
        ["Xbox Party Chat"] = C(0x2D, 0x6C, 0x3E),   // xbox green — matches the party commands' tint
        ["Windows"]         = C(0x46, 0x52, 0x60),   // blue-grey — matches show-desktop's own tint
        ["Arcade"]          = C(0x2E, 0x4E, 0x7E),   // deep blue — a cabinet, not a system action
    };

    /// <summary>A group's default accent colour: its per-group baked colour, else the parent category's.</summary>
    public static Color GroupDefaultColor(string group, string parentHeader) =>
        GroupColors.TryGetValue(group, out var c) ? c : CategoryDefaultColor(parentHeader);

    // The Add picker's "Storefront" sub-wheel node (Edit → Add → Storefront; the launcher action type's
    // UI label is "Storefront"). Keyed "launcher-menu" — the config key keeps the old name for compat.
    public const  string LauncherMenuDefaultGlyph = "Storefront";
    public static Color  LauncherMenuDefaultColor => CategoryDefaultColor("Games & Apps");
    public static string LauncherMenuGlyph()    => ActionIcons.Override("launcher-menu") ?? LauncherMenuDefaultGlyph;
    public static string LauncherMenuColorHex() =>
        ActionTint.ToHex(ActionTint.OverrideColor("launcher-menu") ?? LauncherMenuDefaultColor);

    // ── Built-in defaults (for the Defaults-tab rows) ──
    public static string CategoryDefaultGlyph(string header) => (Cats.TryGetValue(header, out var v) ? v : CatFallback).Glyph;
    public static Color  CategoryDefaultColor(string header) => (Cats.TryGetValue(header, out var v) ? v : CatFallback).Color;
    public static string GroupDefaultGlyph(string group)     => GroupGlyphs.TryGetValue(group, out var g) ? g : "FolderOutline";

    // ── Effective values for the picker (user override → built-in default) ──
    public static string CategoryGlyph(string header) =>
        ActionIcons.Override("category:" + header) ?? CategoryDefaultGlyph(header);
    public static string CategoryColorHex(string header) =>
        ActionTint.ToHex(ActionTint.OverrideColor("category:" + header) ?? CategoryDefaultColor(header));
    public static string GroupGlyph(string group) =>
        ActionIcons.Override("group:" + group) ?? GroupDefaultGlyph(group);
    public static string GroupColorHex(string group, string parentHeader) =>
        ActionTint.ToHex(ActionTint.OverrideColor("group:" + group) ?? GroupDefaultColor(group, parentHeader));
}
