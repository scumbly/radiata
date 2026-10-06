namespace ControllerWheel;

/// <summary>User-chosen default glyph per action type, overlaying the built-in
/// <see cref="MonoIcons.DefaultMaterialGlyph"/> defaults. Resolved for slices that have no explicit
/// per-slice icon — the icon analogue of <see cref="ActionTint"/>'s per-type colour overrides.</summary>
public static class ActionIcons
{
    private static Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Apply user overrides (type → Material icon name); blank entries ignored.</summary>
    public static void SetOverrides(IReadOnlyDictionary<string, string>? icons)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (icons is not null)
            foreach (var kv in icons)
                if (!string.IsNullOrWhiteSpace(kv.Value)) map[kv.Key] = kv.Value;
        _overrides = map;
    }

    /// <summary>The configured default glyph for a type, or null if unset.</summary>
    public static string? Override(string? type) =>
        type is not null && _overrides.TryGetValue(type, out var n) ? n : null;

    /// <summary>The effective default glyph for a slice's action: the user's override, else the
    /// built-in default. Launchers are keyed per-storefront ("launcher:&lt;key&gt;").</summary>
    public static string? Resolve(ActionConfig? action)
    {
        if (action?.Type is "launcher" && LauncherCatalog.Find(action.Command) is { } li)
            return Override("launcher:" + li.Key) ?? li.Glyph;
        // System actions are keyed per-command ("system:<command>") so each command's default glyph is
        // editable in the Color Defaults window; fall back to the built-in per-command glyph.
        if (action?.Type is "system" && !string.IsNullOrWhiteSpace(action.Command))
            return Override("system:" + action.Command!.ToLowerInvariant()) ?? MonoIcons.DefaultMaterialGlyph(action);
        return Override(action?.Type) ?? MonoIcons.DefaultMaterialGlyph(action);
    }
}
