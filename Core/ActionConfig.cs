namespace ControllerWheel;

public sealed class ActionConfig
{
    public string  Type      { get; init; } = "";
    public string? Path      { get; init; }  // launch, script, toggle
    public bool    QuitIfRunning { get; init; }  // launch: Toggle behavior — request that the running instance close instead of focusing it
    // arcade: Command = the game id (see ArcadeCatalog). Blank/absent = open the Arcade picker, and an
    // unknown id resolves the same way rather than dead-ending — so a config from a newer build still works.
    public string? Command   { get; init; }  // system, launcher, arcade
    public string? Url       { get; init; }  // url, installed-game, discord-join
    public string? Process   { get; init; }  // launch (optional, if process name differs from the exe), toggle
    public string? Keys      { get; init; }  // keypress  e.g. "Win+D", "Ctrl+Shift+Esc"
    public string? Device    { get; init; }  // switch-audio  partial output device name; null = cycle output
    public string? MicDevice { get; init; }  // switch-audio  partial mic device name; null = leave mic unchanged
    public float?  Level     { get; init; }  // volume-set  0.0–1.0
    public string? PowerPlan { get; init; }  // system:power-plan  Windows power-scheme GUID (plan A of the toggle)
    // system:power-plan — the second plan of the Toggle Power Plan pair. Firing toggles: active == PowerPlan
    // → switch to PowerPlanB, else → PowerPlan. Absent (a legacy single-plan config) = plain "set PowerPlan".
    public string? PowerPlanB { get; init; }
    // system:reboot — "Log In after Reboot". Null/true (the default) = restart with Automatic Restart
    // Sign-On (shutdown /g); false = a traditional restart (shutdown /r) that lands wherever a Start-menu
    // reboot without ARSO would (usually the sign-in screen).
    public bool?   LogInAfterReboot { get; init; }
    public string? ObsTarget { get; init; }  // obs  scene name (obs-set-scene) / audio source name (obs-toggle-mic)
    // text-chat: Command doubles as the mode ("default" = detect the running game's chat key; "custom" =
    // Keys below). ChatText is the message typed once the chat box is open — see Core/ActionExecutor.cs
    // "text-chat" case and Core/GameChatButtons.cs for the default-mode lookup table.
    public string? ChatText  { get; init; }  // text-chat  message to type into the opened chat box
    public bool    RequireConfirm { get; init; }  // hold the armed slice ~0.8s to fire (guards destructive actions)

    public ActionConfig[]? Steps { get; init; }  // sequence: sub-actions run in order
    public int?    DelayMs   { get; init; }  // sequence step: wait this many ms before running it
}
