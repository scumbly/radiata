using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Mutable flat model used by the settings editor for one slice.</summary>
public sealed class SliceEditModel
{
    public string  Label       { get; set; } = "";

    /// <summary>Label as shown in the settings slice list — truncated past 17 chars so a long name never
    /// pushes the wheel-position preview off the right edge. Recomputed on each list refresh; no change
    /// notification needed.</summary>
    public string  ListLabel => Loc.DefaultLabel(Label) is var shown && shown.Length > 17 ? shown[..17] + "…" : shown;
    public string? IconName    { get; set; }
    public string? IconColor   { get; set; }      // per-slice icon tint override "#RRGGBB" — AUTHORED, material-neutral
    /// <summary>Whether <see cref="IconColor"/> is an EXACT typed hex (true) or a palette swatch pick (false);
    /// null when there's no per-slice colour at all. Must be written EXPLICITLY whenever a colour is set: a
    /// null flag on a slice with no legacy pairing reads as "exact" — see <c>ActionTint.IsExactColor</c>.
    /// The editor stores no second material-dependent half.</summary>
    public bool?   IconColorExact { get; set; }

    /// <summary>True for a slice added during THIS Settings session (the + button / an exe drop) — it keeps
    /// the full category-tab type picker. Slices loaded from config are type-locked (change the category by
    /// deleting + re-adding). Session-only; never persisted.</summary>
    public bool    IsNew       { get; set; }
    public bool    ShowLabel   { get; set; } = true;   // show this slice's text label on the wheel (default off for custom-logo games)
    // Preserved (never edited here): without them ToSlice() nulls them in config, so a Settings round-trip
    // would strip an assigned game's SteamGridDB logo / cover.
    public string? LogoPath    { get; set; }       // transparent game logo (assigned games)
    public string? IconPath    { get; set; }       // image file (e.g. cover art)
    public string  ActionType  { get; set; } = "launch";
    public string  Path        { get; set; } = "";
    public bool    QuitIfRunning { get; set; }            // launch: quit instead of focus if already running
    public string  ProcessName { get; set; } = "";
    public string  Command     { get; set; } = "sleep";
    public string  Url         { get; set; } = "";
    public string  Keys        { get; set; } = "";        // keypress
    public string  Device      { get; set; } = "";        // switch-audio  output device
    public string  MicDevice   { get; set; } = "";        // switch-audio  mic device
    public string  Level       { get; set; } = "50";      // volume-set (0-100 as string for the TextBox)
    public string  PowerPlan   { get; set; } = "";        // system:power-plan  plan A GUID (Toggle Power Plan)
    public string  PowerPlanB  { get; set; } = "";        // system:power-plan  plan B GUID (Toggle Power Plan)
    public bool    LogInAfterReboot { get; set; } = true; // system:reboot  ARSO restart (default) vs plain /r
    public string  ObsTarget   { get; set; } = "";        // obs  scene / audio-source name
    public string  ChatText    { get; set; } = "";        // text-chat  message to send
    public string  SequenceText { get; set; } = "";       // sequence: steps as editable text
    public bool    RequireConfirm { get; set; }           // hold-to-fire guard

    // Drives the wheel-shape indicator in the settings slice list. Set by the editor.
    public int     Index { get; set; }
    public int     Total { get; set; }

    /// <summary>A detached working copy (for the editor's draft). Every member is currently a value type or
    /// an immutable string, so this shallow MemberwiseClone is a full copy. ⚠ If a MUTABLE reference member
    /// is ever added (e.g. a List for sequence steps), the draft and committed models would SHARE it and
    /// edits would leak across the Save/Revert boundary — deep-copy it here AND add it to CopyValuesInto.</summary>
    public SliceEditModel Clone() => (SliceEditModel)MemberwiseClone();

    /// <summary>What a screen reader announces for a row in the settings slice list. The row's TextBlocks
    /// sit inside a DataTemplate the automation tree summarises via ToString(), so without this every row
    /// reads as "ControllerWheel.SliceEditModel". Keep it mirroring what those two lines show.</summary>
    public override string ToString() => $"{ListLabel} — {ActionType}";

    /// <summary>Copy this model's editable values onto <paramref name="t"/> (used to commit a draft
    /// back into the committed slice). Leaves the target's Index/Total — those are structural.</summary>
    public void CopyValuesInto(SliceEditModel t)
    {
        t.Label = Label; t.IconName = IconName; t.IconColor = IconColor; t.IconColorExact = IconColorExact; t.ShowLabel = ShowLabel; t.ActionType = ActionType;
        t.Path = Path; t.QuitIfRunning = QuitIfRunning; t.ProcessName = ProcessName; t.Command = Command;
        t.Url = Url; t.Keys = Keys; t.Device = Device; t.MicDevice = MicDevice;
        t.Level = Level; t.PowerPlan = PowerPlan; t.PowerPlanB = PowerPlanB;
        t.LogInAfterReboot = LogInAfterReboot; t.SequenceText = SequenceText;
        t.ObsTarget = ObsTarget; t.ChatText = ChatText;
        t.RequireConfirm = RequireConfirm; t.LogoPath = LogoPath; t.IconPath = IconPath;
    }

    /// <summary>Icon shown in the settings slice list. Mirrors the overlay's resolution order
    /// (App.PopulateIcons): explicit icon name → exe-extracted icon → built-in shape.</summary>
    public ImageSource? Preview
    {
        get
        {
            var slice = ToSlice();
            // A fetched game logo (custom logo) → a generic "ImageFrame" placeholder, not the slice's
            // fallback glyph; the wheel renders the real logo, this list doesn't reproduce it.
            if (!string.IsNullOrWhiteSpace(LogoPath))
                return PackIconHelper.FromName("ImageFrame", ActionTint.BrushFor(slice)) ?? MonoIcons.ForSlice(slice);
            if (!string.IsNullOrWhiteSpace(IconName))
                return PackIconHelper.FromName(IconName!, ActionTint.BrushFor(slice)) ?? MonoIcons.ForSlice(slice);
            if (WearsAppIcon(LogoPath, IconName, ActionType, Path))
                return IconCache.Get(Path) ?? MonoIcons.ForSlice(slice);
            var glyph = ActionIcons.Resolve(slice.Action);
            return (glyph is not null ? PackIconHelper.FromName(glyph, ActionTint.BrushFor(slice)) : null)
                   ?? MonoIcons.ForSlice(slice);
        }
    }

    /// <summary>Does this slice wear the launched app's OWN icon (extracted from its exe) rather than a picked
    /// icon, a logo or the action type's glyph? The ONE place the editor decides it — the preview list and the
    /// icon well both ask here, so they cannot disagree about which slices show the exe icon. Mirrors the
    /// order <c>App.PopulateIcons</c> applies to the live wheel: a logo or an explicit icon name wins first.</summary>
    public static bool WearsAppIcon(string? logoPath, string? iconName, string? actionType, string? path) =>
        string.IsNullOrWhiteSpace(logoPath) && string.IsNullOrWhiteSpace(iconName)
        && actionType is "launch" && !string.IsNullOrWhiteSpace(path);

    public static SliceEditModel FromSlice(WheelSlice s) => new()
    {
        Label       = s.Label,
        IconName    = s.IconName,
        IconColor   = s.IconColor,
        // Resolved through the shared legacy-aware helper so a legacy slice (which marked "swatch pick" by
        // carrying an IconColorLight) comes in with the exactness the renderer reads off it. Null only when
        // there's no per-slice colour to describe.
        IconColorExact = s.IconColor is null ? null : ActionTint.IsExactColor(s),
        ShowLabel   = s.ShowLabel ?? SliceLabelRule.DefaultShowLabel(s),
        LogoPath    = s.LogoPath,
        IconPath    = s.IconPath,
        ActionType  = s.Action?.Type    ?? "launch",
        Path        = s.Action?.Path    ?? "",
        QuitIfRunning = s.Action?.QuitIfRunning ?? false,
        ProcessName = s.Action?.Process ?? "",
        // Only a system slice reads a missing command as Sleep. Any other type keeps it blank: ToSlice writes
        // Command for arcade / launcher / obs / text-chat, so a "sleep" filler would be persisted into a
        // slice that never had one (an Arcade Launcher slice would round-trip as an unknown game id).
        Command     = s.Action?.Command ?? (string.Equals(s.Action?.Type, "system", StringComparison.OrdinalIgnoreCase) ? "sleep" : ""),
        Url         = s.Action?.Url     ?? "",
        Keys        = s.Action?.Keys      ?? "",
        Device      = s.Action?.Device    ?? "",
        MicDevice   = s.Action?.MicDevice ?? "",
        Level       = s.Action?.Level is float l ? ((int)Math.Round(l * 100)).ToString() : "50",
        PowerPlan   = s.Action?.PowerPlan ?? "",
        PowerPlanB  = s.Action?.PowerPlanB ?? "",
        LogInAfterReboot = s.Action?.LogInAfterReboot ?? true,   // absent = the default (sign back in)
        ObsTarget   = s.Action?.ObsTarget ?? "",
        ChatText    = s.Action?.ChatText  ?? "",
        SequenceText = s.Action?.Type == "sequence" ? SequenceFormat.ToText(s.Action.Steps) : "",
        RequireConfirm = s.Action?.RequireConfirm ?? false,
    };

    public WheelSlice ToSlice() => new()
    {
        Label     = Label.Trim(),
        IconName  = Nz(IconName ?? ""),
        IconColor = Nz(IconColor ?? ""),
        IconColorExact = Nz(IconColor ?? "") is null ? null : IconColorExact,
        // ⚠ Write the retired pairing field NULL unconditionally — nothing may reintroduce a
        // material-dependent stored colour (see WheelSlice.IconColorLight). Saving a legacy slice sheds it
        // without changing appearance, because FromSlice folded its meaning into IconColorExact on the way in.
        IconColorLight = null,
        ShowLabel = ShowLabel,
        LogoPath  = Nz(LogoPath ?? ""),
        IconPath  = Nz(IconPath ?? ""),
        Action    = new ActionConfig
        {
            Type      = ActionType,
            Path      = Nz(Path),
            QuitIfRunning = ActionType == "launch" && QuitIfRunning,
            Process   = Nz(ProcessName),
            // Command belongs to system/launcher/obs/text-chat/arcade — every other type gets null, or the
            // model's "sleep" default would be persisted into every slice. For text-chat, Command doubles as
            // the "default" | "custom" mode (see Core/ActionConfig.cs); for arcade it carries the game id
            // ReadEditorInto writes, so "arcade" must stay in this list or editing a slice drops the game.
            Command   = ActionType switch
            {
                "system" or "launcher" or "obs" or "text-chat" or "arcade" => Nz(Command),
                _                               => null,
            },
            Url       = Nz(Url),
            Keys      = Nz(Keys),
            Device    = Nz(Device),
            MicDevice = Nz(MicDevice),
            Level     = ActionType == "system" && Command == "volume-set"
                            && int.TryParse(Level, out int lvl)
                            ? Math.Clamp(lvl, 0, 100) / 100f
                            : null,
            PowerPlan  = ActionType == "system" && Command == "power-plan" ? Nz(PowerPlan)  : null,
            PowerPlanB = ActionType == "system" && Command == "power-plan" ? Nz(PowerPlanB) : null,
            // Persisted only when UNCHECKED — absent means the default (true), so ordinary reboot slices
            // stay one line shorter and every pre-existing config is already "checked".
            LogInAfterReboot = ActionType == "system" && Command == "reboot" && !LogInAfterReboot
                               ? false : null,
            ObsTarget = ActionType == "obs" ? Nz(ObsTarget) : null,
            ChatText  = ActionType == "text-chat" ? Nz(ChatText) : null,
            Steps     = ActionType == "sequence" ? SequenceFormat.Parse(SequenceText) : null,
            RequireConfirm = RequireConfirm,
        },
    };

    private static string? Nz(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
