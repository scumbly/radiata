using System.Text;

namespace ControllerWheel;

/// <summary>
/// Converts a sequence's steps to/from a compact one-line-per-step text form for the Settings
/// editor. Each line is "type: value" (the value maps to that action type's main field), and a
/// "wait: N" line inserts an N-ms delay before the next step. Example:
/// <code>
///   toggle: SignalRgb
///   wait: 500
///   launch: C:\Games\game.exe
///   keypress: Win+D
/// </code>
///
/// A step whose action carries settings beyond that main field appends them after a "|" as
/// <c>key=value</c> pairs, separated by ";":
/// <code>
///   launch: C:\Games\game.exe | process=game.exe; quit=true
///   switch-audio: Speakers | mic=Blue Yeti
///   system: volume-set | level=0.4
/// </code>
/// Round-tripping is the contract: every field <see cref="ActionExecutor"/> reads for a step type must
/// survive ToText → Parse unchanged. Anything the suffix can't carry — "quit if already running", which
/// mic to switch, the target volume, a toggle's exe path, a power-plan GUID, an OBS scene, the text-chat
/// message — is silently dropped when a sequence is opened in the editor and saved, and the step quietly
/// stops doing that part. Unknown keys are ignored rather than rejected, so a config written by a newer
/// build degrades instead of failing to parse.
/// </summary>
public static class SequenceFormat
{
    public static string ToText(ActionConfig[]? steps)
    {
        if (steps is null || steps.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var s in steps)
        {
            if (s is null) continue;
            if (s.DelayMs is int d && d > 0) sb.AppendLine($"wait: {d}");
            var value = PrimaryValue(s);
            var extras = ExtraPairs(s);
            var head = value.Length == 0 ? (s.Type ?? "") : $"{s.Type}: {value}";
            sb.AppendLine(extras.Length == 0 ? head : $"{head} | {extras}");
        }
        return sb.ToString().TrimEnd();
    }

    public static ActionConfig[] Parse(string? text)
    {
        var steps = new List<ActionConfig>();
        if (string.IsNullOrWhiteSpace(text)) return [];

        int pendingDelay = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            // Split the extras suffix off first so a "|" can't be mistaken for part of the value.
            string extras = "";
            int bar = line.IndexOf('|');
            if (bar >= 0) { extras = line[(bar + 1)..].Trim(); line = line[..bar].Trim(); }

            string type, value;
            int colon = line.IndexOf(':');
            if (colon >= 0) { type = line[..colon].Trim().ToLowerInvariant(); value = line[(colon + 1)..].Trim(); }
            else            { type = line.ToLowerInvariant();                 value = ""; }

            if (type == "wait")
            {
                if (int.TryParse(value, out int d)) pendingDelay += Math.Max(0, d);
                continue;
            }

            steps.Add(Make(type, value, pendingDelay, ParseExtras(extras)));
            pendingDelay = 0;
        }
        return [.. steps];
    }

    /// <summary>Every action type <see cref="ActionExecutor"/> accepts, mapped to the <see cref="ActionConfig"/>
    /// property its text-form value edits (null = the type carries no payload). The one table both
    /// <see cref="PrimaryValue"/> and <see cref="Make"/> read; a type missing here loses its payload in the
    /// editor, and the harness's pairs group fails when this set drifts from the executor's switch.</summary>
    public static readonly IReadOnlyDictionary<string, string?> PrimaryFields =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["launch"] = nameof(ActionConfig.Path),
            ["script"] = nameof(ActionConfig.Path),
            ["toggle"] = nameof(ActionConfig.Process),
            ["system"] = nameof(ActionConfig.Command),
            ["url"] = nameof(ActionConfig.Url),
            ["installed-game"] = nameof(ActionConfig.Url),
            ["launcher"] = nameof(ActionConfig.Command),
            ["keypress"] = nameof(ActionConfig.Keys),
            ["switch-audio"] = nameof(ActionConfig.Device),
            ["discord-join"] = nameof(ActionConfig.Url),
            ["steam-mute"] = nameof(ActionConfig.Keys),
            ["obs"] = nameof(ActionConfig.Command),
            ["text-chat"] = nameof(ActionConfig.Command),
            ["arcade"] = nameof(ActionConfig.Command),
            ["exit-app"] = null,
            ["discord-launch"] = null,
            ["discord-mute"] = null,
            ["discord-deafen"] = null,
            ["sequence"] = null,
            ["settings"] = null,
            ["game-browser"] = null,
            ["disable-wheels"] = null,
            ["xbox-emulation"] = null,
            ["dualshock-emulation"] = null,
            ["safe-mode"] = null,
        };

    private static string? PrimaryField(string? type) =>
        type is not null && PrimaryFields.TryGetValue(type, out var f) ? f : null;

    // The single "main" field for each action type (what the value column edits).
    private static string PrimaryValue(ActionConfig a) => PrimaryField(a.Type) switch
    {
        nameof(ActionConfig.Path)    => a.Path    ?? "",
        nameof(ActionConfig.Process) => a.Process ?? "",
        nameof(ActionConfig.Command) => a.Command ?? "",
        nameof(ActionConfig.Keys)    => a.Keys    ?? "",
        nameof(ActionConfig.Url)     => a.Url     ?? "",
        nameof(ActionConfig.Device)  => a.Device  ?? "",
        _                            => "",
    };

    /// <summary>Everything the primary value can't carry, as "key=value; key=value" (empty when the step
    /// has no extras). Only non-empty fields are emitted, so simple steps keep their clean one-line form.</summary>
    private static string ExtraPairs(ActionConfig a)
    {
        var parts = new List<string>();
        void Add(string key, string? v) { if (!string.IsNullOrWhiteSpace(v)) parts.Add($"{key}={v.Trim()}"); }

        switch (a.Type?.ToLowerInvariant())
        {
            case "launch":
                Add("process", a.Process);
                if (a.QuitIfRunning) parts.Add("quit=true");
                break;
            case "toggle":
                Add("path", a.Path);
                break;
            case "switch-audio":
                Add("mic", a.MicDevice);
                break;
            case "system":
                if (a.Level is float lv) parts.Add($"level={lv.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                Add("plan", a.PowerPlan);
                Add("planb", a.PowerPlanB);
                if (a.LogInAfterReboot == false) parts.Add("login=false");   // absent = the default (true)
                break;
            case "obs":
                Add("target", a.ObsTarget);
                break;
            case "text-chat":
                Add("keys", a.Keys);
                Add("text", a.ChatText);
                break;
        }
        return string.Join("; ", parts);
    }

    private static Dictionary<string, string> ParseExtras(string extras)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (extras.Length == 0) return map;
        // "text=" (the text-chat message) is free-form user prose and is always emitted last, so it takes
        // the rest of the line verbatim — otherwise a perfectly ordinary message containing ";" would be
        // truncated at the semicolon. (A "|" in the message is still not representable; the editor's own
        // Text Chat field is the place to write one.)
        int t = extras.IndexOf("text=", StringComparison.OrdinalIgnoreCase);
        if (t >= 0 && (t == 0 || extras[t - 1] is ';' or ' '))
        {
            map["text"] = extras[(t + 5)..].Trim();
            extras = extras[..t].TrimEnd(' ', ';');
            if (extras.Length == 0) return map;
        }
        foreach (var pair in extras.Split(';'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;                       // no key, or "=value" — ignore rather than throw
            var k = pair[..eq].Trim();
            var v = pair[(eq + 1)..].Trim();
            if (k.Length > 0) map[k] = v;                // last one wins on a duplicate key
        }
        return map;
    }

    private static ActionConfig Make(string type, string value, int delayMs, Dictionary<string, string> x)
    {
        string? v = string.IsNullOrWhiteSpace(value) ? null : value;
        string? X(string key) => x.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;

        float? level = null;
        if (X("level") is { } lvs
            && float.TryParse(lvs, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out var lvf))
            level = Math.Clamp(lvf, 0f, 1f);

        var field = PrimaryField(type);
        string? P(string name) => field == name ? v : null;

        return new ActionConfig
        {
            Type    = type,
            // "toggle" reads its exe path from the extras (its primary value is the process name).
            Path    = P(nameof(ActionConfig.Path)) ?? (type == "toggle" ? X("path") : null),
            Process = P(nameof(ActionConfig.Process)) ?? (type == "launch" ? X("process") : null),
            QuitIfRunning = type == "launch"
                            && string.Equals(X("quit"), "true", StringComparison.OrdinalIgnoreCase),
            Command = P(nameof(ActionConfig.Command)),
            Keys    = P(nameof(ActionConfig.Keys)) ?? (type == "text-chat" ? X("keys") : null),
            Url     = P(nameof(ActionConfig.Url)),
            Device  = P(nameof(ActionConfig.Device)),
            MicDevice = type == "switch-audio" ? X("mic") : null,
            Level     = type == "system" ? level : null,
            PowerPlan = type == "system" ? X("plan") : null,
            PowerPlanB = type == "system" ? X("planb") : null,
            LogInAfterReboot = type == "system"
                               && string.Equals(X("login"), "false", StringComparison.OrdinalIgnoreCase)
                               ? false : null,
            ObsTarget = type == "obs" ? X("target") : null,
            ChatText  = type == "text-chat" ? X("text") : null,
            DelayMs = delayMs > 0 ? delayMs : null,
        };
    }
}
