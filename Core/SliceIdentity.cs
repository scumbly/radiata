namespace ControllerWheel;

/// <summary>What makes two slices the SAME action, for the paths that must not seed a duplicate (the
/// first-run starter merge, the OOBE game dropdown). Field-for-field equality is the wrong test: a slice
/// carries residue from earlier edits — retyping a Sleep slice to Arcade leaves <c>Command="sleep"</c>
/// behind, and that slice still opens the Arcade Launcher, so a raw type|command|url key reads it as a
/// different action and seeds a second launcher beside it.
/// <para>So the key is the payload the type actually reads, normalized the way
/// <see cref="ActionExecutor"/> resolves it. Types with no distinguishing payload (Settings, Game Grid,
/// Exit App…) are singletons keyed on type alone. An unknown type falls back to every payload field, which
/// can only ever over-distinguish — a new type is never silently collapsed into one slice.</para></summary>
public static class SliceIdentity
{
    /// <summary>Types whose payload fields carry no identity — one per wheel, whatever else is stored on
    /// them. Any field they do hold (RequireConfirm, a tint) is presentation, not a different action.</summary>
    private static readonly HashSet<string> Singletons = new(StringComparer.OrdinalIgnoreCase)
    {
        "exit-app", "settings", "game-browser", "disable-wheels", "safe-mode",
        "discord-launch", "discord-mute", "discord-deafen", "steam-mute",
        "xbox-emulation", "dualshock-emulation",
    };

    /// <summary>A case-insensitive equality key for <paramref name="slice"/>'s action. Two slices with the
    /// same key run the same thing; different keys may still be the same thing only for types this doesn't
    /// know (see the class note).</summary>
    public static string Key(WheelSlice? slice) => Key(slice?.Action);

    public static string Key(ActionConfig? a)
    {
        string type = (a?.Type ?? "").Trim().ToLowerInvariant();
        if (a is null || type.Length == 0) return "|";
        if (Singletons.Contains(type)) return type;
        return type switch
        {
            // Command = a game id, blank = the launcher — and an unknown id resolves to the launcher too,
            // so resolve through the catalog rather than comparing the stored string.
            "arcade"         => $"arcade|{ArcadeCatalog.Resolve(a.Command)?.Id ?? ""}",
            "launch"         => $"launch|{N(a.Path)}|{N(a.Process)}",
            "toggle"         => $"toggle|{N(a.Path)}|{N(a.Process)}",
            "script"         => $"script|{N(a.Path)}",
            "url"            => $"url|{N(a.Url)}",
            "installed-game" => $"installed-game|{N(a.Url)}",
            "discord-join"   => $"discord-join|{N(a.Url)}",
            "launcher"       => $"launcher|{N(a.Command)}",
            "keypress"       => $"keypress|{N(a.Keys)}",
            "switch-audio"   => $"switch-audio|{N(a.Device)}|{N(a.MicDevice)}",
            "obs"            => $"obs|{N(a.Command)}|{N(a.ObsTarget)}",
            "text-chat"      => $"text-chat|{N(a.Command)}|{N(a.Keys)}|{N(a.ChatText)}",
            // Command is the leaf (sleep, hdr-toggle, power-plan…); the few leaves with their own payload
            // are distinct per payload — two Set Volume slices at different levels are two actions.
            "system"         => $"system|{N(a.Command)}|{a.Level}|{N(a.PowerPlan)}|{N(a.PowerPlanB)}",
            // A sequence's identity is its whole step list. Nothing seeds one, so give every instance its
            // own key instead of walking the steps: a user's sequence can never block a seed.
            "sequence"       => $"sequence|{Guid.NewGuid():N}",
            _                => $"{type}|{N(a.Path)}|{N(a.Command)}|{N(a.Url)}|{N(a.Process)}|{N(a.Keys)}|" +
                                $"{N(a.Device)}|{N(a.MicDevice)}|{a.Level}|{N(a.PowerPlan)}|{N(a.PowerPlanB)}|" +
                                $"{N(a.ObsTarget)}|{N(a.ChatText)}",
        };

        static string N(string? s) => (s ?? "").Trim().ToLowerInvariant();
    }
}
