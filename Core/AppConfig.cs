namespace ControllerWheel;

public sealed class AppConfig
{
    public WheelSlice[] WheelA { get; init; } = [];
    public WheelSlice[] WheelB { get; init; } = [];
    public SystemConfig System { get; init; } = new();

    /// <summary>Per-action-type icon tint overrides (type → "#RRGGBB"). Read-only compatibility: no UI
    /// writes new entries — <see cref="ActionTint"/>.Defaults is the sole source of truth — but the field
    /// and the <c>ActionTint.SetOverrides</c> load-time apply stay so an existing config's overrides keep
    /// working, and SettingsWindow.Save keeps round-tripping them.</summary>
    public Dictionary<string, string> ActionColors { get; init; } = new();

    /// <summary>Per-action-type default glyph overrides (type → Material icon name). Applied as a live
    /// fallback for slices of that type with no explicit per-slice icon. Same read-only compatibility
    /// contract as <see cref="ActionColors"/> — see there.</summary>
    public Dictionary<string, string> ActionIcons { get; init; } = new();

    /// <summary>Saved custom-colour palette ("#RRGGBB") shared by the colour pickers.</summary>
    public string[] CustomColors { get; init; } = [];

    /// <summary>App version that last wrote this file (stamped by ConfigLoader.WriteConfig). When the
    /// version at load time differs, ConfigLoader snapshots the file to backups\ before anything can
    /// rewrite it — the readable-but-about-to-be-migrated safety net (the unreadable case already had
    /// one). Settable (not init) so WriteConfig can stamp without cloning the whole config.</summary>
    public string? SavedByVersion { get; set; }

    /// <summary>Used on first launch before a config file exists.</summary>
    public static readonly AppConfig Default = new()
    {
        WheelA =
        [
            new() { Label = UiText.DefaultLabels.Discord,     Action = new() { Type = "launch",  Path    = "" } },
            new() { Label = UiText.DefaultLabels.Volume,      Action = new() { Type = "system",  Command = "volume" } },
            new() { Label = "Steam",      Action = new() { Type = "launch",  Path    = "" } },
            new() { Label = UiText.DefaultLabels.XboxPad,     Action = new() { Type = "xbox-emulation" } },
            new() { Label = UiText.DefaultLabels.Sleep,       Action = new() { Type = "system",  Command = "sleep" } },
            new() { Label = UiText.DefaultLabels.Settings,    Action = new() { Type = "settings" } },
        ],
        WheelB =
        [
            new() { Label = "Playnite",     Action = new() { Type = "launch", Path = "" } },
            new() { Label = UiText.DefaultLabels.MicMute,       Action = new() { Type = "system", Command = "mic-mute" } },
            new() { Label = UiText.DefaultLabels.GameGrid,      Action = new() { Type = "game-browser" } },
            new() { Label = UiText.DefaultLabels.ShowDesktop,   Action = new() { Type = "system", Command = "show-desktop" } },
            new() { Label = "Moonlight",    Action = new() { Type = "launch", Path = "" } },
            new() { Label = UiText.DefaultLabels.Settings,      Action = new() { Type = "settings" } },
        ],
    };
}
