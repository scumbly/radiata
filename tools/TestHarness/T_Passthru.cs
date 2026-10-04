using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The "Safe Mode" → "Passthru Mode" rename — the text and contract halves that
/// need no screen. What a pass settles: every English UI string, action label and Help title carries the
/// new name; Help search still finds the topic under the OLD name; the Arcade guard maps the isolation note
/// to the Passthru reason and offers no override; the FROZEN config keys (<c>captureSafeMode</c>,
/// <c>safeModeApps</c>) still round-trip. What it can't settle: how the tray item, toasts and card RENDER —
/// that stays a visual check.</summary>
internal static class T_Passthru
{
    public static void Run()
    {
        H.Group("Passthru Mode rename — strings, Help search, Arcade guard contract, frozen config keys");

        // ── 1. No English UI string still says "Safe Mode" ─────────────────────────────────────────
        {
            var offenders = new List<string>();
            foreach (var (path, value) in AllStringConsts(typeof(UiText)))
                if (value.Contains("safe mode", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{path} = \"{value}\"");
            H.Check("UiText: no English string contains \"Safe Mode\"", offenders.Count == 0,
                    offenders.Count == 0 ? $"{AllStringConsts(typeof(UiText)).Count()} strings scanned"
                                         : string.Join("; ", offenders.Take(5)));

            H.Check("UiText.DefaultLabels.PassthruMode is the new name",
                    UiText.DefaultLabels.PassthruMode == "Passthru Mode", UiText.DefaultLabels.PassthruMode);

            var tint = ActionTint.Defaults.FirstOrDefault(d => d.Type == "safe-mode");
            H.Check("ActionTint.Defaults keeps the \"safe-mode\" type (key frozen)", tint.Type == "safe-mode");
            H.Check("…with label \"Toggle Passthru Mode\"", tint.Label == "Toggle Passthru Mode", tint.Label);

            var en = HelpLocalization.Topics("en");
            var titles = en.Where(t => t.Title.Contains("safe mode", StringComparison.OrdinalIgnoreCase)).Select(t => t.Id).ToList();
            H.Check("Help: no English topic TITLE says \"Safe Mode\"", titles.Count == 0, string.Join(", ", titles));
            var topic = en.FirstOrDefault(t => t.Id == "passthru-mode");
            H.Check("Help: topic id \"passthru-mode\" exists", topic is not null);
            H.Check("Help: its title names Passthru Mode",
                    topic?.Title.Contains("Passthru Mode", StringComparison.Ordinal) == true, topic?.Title);
        }

        // ── 2. Help search finds the topic under old AND new names (§A0 item 4) ────────────────────
        foreach (var q in new[] { "safe mode", "passthru", "passthrough" })
        {
            var hits = HelpLocalization.Search("en", q).Select(t => t.Id).ToList();
            H.Check($"Help search \"{q}\" → passthru-mode", hits.Contains("passthru-mode"),
                    $"{hits.Count} hit(s): {string.Join(", ", hits.Take(6))}");
        }

        // ── 3. Every cross-link resolves, markers pair (§A0 item 3, the resolvable half) ────────────
        {
            var m = H.StaticMethod(typeof(HelpLocalization), "MarkupProblems", 1);
            if (m is null) H.Fail("HelpLocalization.MarkupProblems", "MISSING (renamed?)");
            else
            {
                var problems = ((IEnumerable<string>)m.Invoke(null, new object[] { HelpLocalization.Topics("en") })!).ToList();
                H.Check("Help (en): no dead cross-links / unpaired markers", problems.Count == 0,
                        problems.Count == 0 ? null : string.Join(" | ", problems.Take(4)));
            }
        }

        // ── 4. Arcade guard contract (§A0 item 6 / §A6): same const both sides, Passthru = no override ──
        {
            var reason = Arcade.ReasonFromNote(UiText.IsolationNote.PassthruNoCloak);
            H.Check("ReasonFromNote(IsolationNote.PassthruNoCloak) → Block.SafeMode",
                    reason == Arcade.Block.SafeMode, reason.ToString());

            var copy = Arcade.Guard(reason, "Some Game");
            H.Check("Guard card title = \"Not while Passthru Mode is on\"",
                    copy.Title == UiText.Arcade.GuardPassthruTitle, copy.Title);
            H.Check("Guard card names the game", copy.Body.Contains("Some Game"), copy.Body);
            H.Check("Passthru guard offers NO hold-to-override", !copy.Overridable);
            H.Check("Guard copy never says \"Safe Mode\"",
                    !(copy.Title + copy.Body + copy.Fix).Contains("safe mode", StringComparison.OrdinalIgnoreCase));

            foreach (var other in new[] { Arcade.Block.NoVirtualPad, Arcade.Block.CloakFailed, Arcade.Block.MultiplePads, Arcade.Block.Unknown })
                H.Check($"{other}: override still offered", Arcade.Guard(other, "G").Overridable);

            H.Check("XboxFallback note must NOT map to MultiplePads",
                    Arcade.ReasonFromNote(UiText.IsolationNote.XboxFallback) != Arcade.Block.MultiplePads);
            H.Check("Passthru ENDING note is not the Passthru block (game still isolated once it ends)",
                    Arcade.ReasonFromNote(UiText.IsolationNote.PassthruEnding) != Arcade.Block.SafeMode,
                    Arcade.ReasonFromNote(UiText.IsolationNote.PassthruEnding).ToString());
        }

        // ── 5. Frozen config keys round-trip (§A0 item 5) ─────────────────────────────────────────
        {
            var opts = H.StaticField(typeof(ConfigLoader), "JsonOpts")?.GetValue(null) as JsonSerializerOptions;
            if (opts is null) H.Fail("ConfigLoader.JsonOpts", "MISSING (renamed?)");
            else
            {
                var cfg = new AppConfig
                {
                    System = new SystemConfig
                    {
                        CaptureSafeMode = true,
                        SafeModeApps = new List<SafeModeApp> { new() { Path = @"C:\Games\Anticheat\game.exe" } },
                    },
                };
                var json = JsonSerializer.Serialize(cfg, opts);
                H.Check("serialised key is still \"captureSafeMode\"", json.Contains("\"captureSafeMode\": true") || json.Contains("\"captureSafeMode\":true"));
                H.Check("serialised key is still \"safeModeApps\"", json.Contains("\"safeModeApps\""));
                H.Check("no \"passthru\" key leaked into the config schema",
                        !json.Contains("passthru", StringComparison.OrdinalIgnoreCase));

                var back = ConfigLoader.TryParse(json);
                H.Check("round-trip: captureSafeMode survives", back?.System.CaptureSafeMode == true);
                H.Check("round-trip: safeModeApps entry survives",
                        back?.System.SafeModeApps.Count == 1 && back.System.SafeModeApps[0].Path.EndsWith("game.exe"),
                        $"count={back?.System.SafeModeApps.Count}");

                // A hand-authored slice of the frozen action type still parses (§A0 item 7, the text half).
                var slice = ConfigLoader.TryParse("{\"wheelA\":[{\"label\":\"\",\"action\":{\"type\":\"safe-mode\"}}]}");
                H.Check("hand-authored \"safe-mode\" slice parses", slice?.WheelA?.Length == 1 && slice.WheelA[0].Action?.Type == "safe-mode",
                        $"type={slice?.WheelA?.FirstOrDefault()?.Action?.Type}");
            }
        }
    }

    /// <summary>Every public string const under a static class and its nested static classes, as
    /// "Outer.Inner.Name" → value.</summary>
    private static IEnumerable<(string Path, string Value)> AllStringConsts(Type root, string prefix = null)
    {
        prefix ??= root.Name;
        foreach (var f in root.GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.IsLiteral && f.FieldType == typeof(string) && f.GetRawConstantValue() is string s)
                yield return ($"{prefix}.{f.Name}", s);
        foreach (var nested in root.GetNestedTypes(BindingFlags.Public))
            foreach (var item in AllStringConsts(nested, $"{prefix}.{nested.Name}"))
                yield return item;
    }
}
