using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Robustness against hostile input — the READ path.
/// Everything here goes through <see cref="ConfigLoader.TryParse"/>, which is the single place a
/// hand-edited config and a restored backup both land, so it's the honest unit under test. The
/// "does the app survive it" half is driven separately by launching Radiata.exe against each file.</summary>
internal static class T_Config
{
    public static void Run()
    {
        H.Group("config.json — hostile / malformed input (ConfigLoader.TryParse)");

        // ── 50 slices in one wheel → trimmed to the 32 hard cap, and it says so ──────────────────────
        {
            var slices = string.Join(",", Enumerable.Range(0, 50)
                .Select(i => $"{{\"label\":\"s{i}\",\"action\":{{\"type\":\"system\",\"command\":\"lock\"}}}}"));
            using var t = new H.TraceGrab();
            var cfg = ConfigLoader.TryParse($"{{\"wheelA\":[{slices}]}}");
            H.Check("50 slices in one wheel → kept 32", cfg?.WheelA?.Length == 32,
                    $"WheelA.Length = {cfg?.WheelA?.Length}");
            H.Check("…and the trim is traced", t.Saw("exceeds the 32"),
                    t.Saw("exceeds the 32") ? null : "no [Config] trim line");
        }

        // ── "label": null → the crash-loop shape from 2ed581b ────────────────────────────────────────
        {
            var cfg = ConfigLoader.TryParse(
                "{\"wheelA\":[{\"label\":null,\"action\":{\"type\":\"system\",\"command\":\"lock\"}}]}");
            H.Check("\"label\": null parses", cfg is not null);
            H.Check("\"label\": null → empty string, never null", cfg?.WheelA?[0]?.Label == "",
                    $"Label = {(cfg?.WheelA?[0]?.Label is null ? "NULL" : "\"" + cfg.WheelA[0].Label + "\"")}");
        }

        // ── a null ELEMENT in the array (the other NRE shape) ────────────────────────────────────────
        {
            using var t = new H.TraceGrab();
            var cfg = ConfigLoader.TryParse("{\"wheelA\":[null,{\"label\":\"ok\"},null]}");
            H.Check("null slice elements dropped", cfg?.WheelA?.Length == 1 && cfg.WheelA[0].Label == "ok",
                    $"WheelA.Length = {cfg?.WheelA?.Length}");
            H.Check("…and the drop is traced", t.Saw("dropped 2 null slice"));
        }

        // ── garbage material tokens → normalised, never left raw ────────────────────────────────────
        {
            var cfg = ConfigLoader.TryParse(
                "{\"system\":{\"sliceMaterial\":\"not-a-material-\uD83D\uDE00\",\"gameGridMaterial\":\"\"}}");
            var known = Materials.All.ToArray();
            H.Check("garbage sliceMaterial normalised to a known token",
                    cfg is not null && known.Contains(cfg.System.SliceMaterial),
                    $"SliceMaterial = \"{cfg?.System?.SliceMaterial}\"");
            H.Check("empty gameGridMaterial normalised to a known token",
                    cfg is not null && known.Contains(cfg.System.GameGridMaterial),
                    $"GameGridMaterial = \"{cfg?.System?.GameGridMaterial}\"");
        }

        // ── legacy material aliases still migrate ───────────────────────────────────────────────────
        {
            var cfg = ConfigLoader.TryParse("{\"system\":{\"sliceMaterial\":\"gloss-light\"}}");
            H.Check("legacy \"gloss-light\" → \"pearl\"", cfg?.System?.SliceMaterial == "pearl",
                    $"got \"{cfg?.System?.SliceMaterial}\"");
            cfg = ConfigLoader.TryParse("{\"system\":{\"sliceMaterial\":\"terra\"}}");
            H.Check("legacy \"terra\" → \"mesa\"", cfg?.System?.SliceMaterial == "mesa",
                    $"got \"{cfg?.System?.SliceMaterial}\"");
        }

        // ── an explicit null on a collection a consumer dereferences → empty, never null ─────────────
        {
            var cfg = ConfigLoader.TryParse(
                "{\"system\":{\"triggerModes\":null,\"disabledStorefronts\":null,\"knownControllerKinds\":null,\"safeModeApps\":null}}");
            H.Check("null collections parse", cfg is not null);
            var s = cfg?.System;
            H.Check("\"triggerModes\": null → empty map", s?.TriggerModes is { Count: 0 });
            H.Check("TriggerModesFor falls back to the kind's default",
                    s is not null && s.TriggerModesFor(ControllerKind.Xbox)
                        .SequenceEqual(new[] { TriggerModes.DefaultFor(ControllerKind.Xbox) }));
            H.Check("\"disabledStorefronts\": null → empty list", s?.DisabledStorefronts is { Count: 0 });
            H.Check("\"knownControllerKinds\": null → empty list", s?.KnownControllerKinds is { Count: 0 });
            H.Check("\"safeModeApps\": null → empty list", s?.SafeModeApps is { Count: 0 });

            var el = ConfigLoader.TryParse(
                "{\"system\":{\"disabledStorefronts\":[null,\"gog\"],\"knownControllerKinds\":[\"Xbox\",null]}}");
            H.Check("null list elements dropped",
                    el?.System.DisabledStorefronts.SequenceEqual(new[] { "gog" }) == true
                    && el.System.KnownControllerKinds.SequenceEqual(new[] { "Xbox" }));
        }

        // ── an unresolvable custom token renders as Pearl but is kept for the next save ───────────────
        {
            var cfg = ConfigLoader.TryParse("{\"system\":{\"sliceMaterial\":\"custom-ember\",\"gameGridMaterial\":\"custom-ember\"}}");
            H.Check("unregistered custom token renders as Pearl", cfg?.System is { SliceMaterial: "pearl", GameGridMaterial: "pearl" });
            H.Check("…and is remembered as held", cfg?.System is { HeldSliceMaterial: "custom-ember", HeldGameGridMaterial: "custom-ember" });
            var disk = ConfigLoader.ForDisk(cfg!);
            H.Check("…and written back as the custom token", disk.System is { SliceMaterial: "custom-ember", GameGridMaterial: "custom-ember" });
            H.Check("…without touching the in-memory Pearl", cfg!.System is { SliceMaterial: "pearl" });
            var picked = ConfigLoader.ReleaseChosen(new AppConfig { System = cfg.System with { SliceMaterial = "mesa" } });
            H.Check("choosing another material releases the held token",
                picked.System is { HeldSliceMaterial: null, HeldGameGridMaterial: "custom-ember" }
                && ConfigLoader.ForDisk(picked).System.SliceMaterial == "mesa");
            var cleared = new AppConfig { System = cfg.System with { HeldSliceMaterial = null, HeldGameGridMaterial = null } };
            H.Check("an explicit Pearl (Uninstall) with nothing held writes pearl", ConfigLoader.ForDisk(cleared).System.SliceMaterial == "pearl");
            H.Check("a built-in or legacy token holds nothing",
                ConfigLoader.TryParse("{\"system\":{\"sliceMaterial\":\"terra\"}}")?.System is { HeldSliceMaterial: null });
        }

        // ── out-of-range numbers → clamped to the documented ranges, each traced ─────────────────────
        {
            using var t = new H.TraceGrab();
            var cfg = ConfigLoader.TryParse("""
                {"system":{"fadeMs":999999,"stickyMs":-500,"driftPx":100000,
                           "dpadVolumeRepeatDelayMs":0,"dpadVolumeRepeatIntervalMs":0,
                           "mixBalance":9001,"obsPort":70000}}
                """);
            var s = cfg?.System;
            H.Check("fadeMs 999999 → 2000",                    s?.FadeMs == 2000,   $"{s?.FadeMs}");
            H.Check("stickyMs -500 → 0",                       s?.StickyMs == 0,    $"{s?.StickyMs}");
            H.Check("driftPx 100000 → 400",                    s?.DriftPx == 400,   $"{s?.DriftPx}");
            H.Check("dpadVolumeRepeatDelayMs 0 → 100",         s?.DpadVolumeRepeatDelayMs == 100, $"{s?.DpadVolumeRepeatDelayMs}");
            H.Check("dpadVolumeRepeatIntervalMs 0 → 20",       s?.DpadVolumeRepeatIntervalMs == 20, $"{s?.DpadVolumeRepeatIntervalMs}");
            H.Check("mixBalance 9001 → 100",                   s?.MixBalance == 100, $"{s?.MixBalance}");
            H.Check("obsPort 70000 → 65535",                   s?.ObsPort == 65535, $"{s?.ObsPort}");
            H.Check("every clamp is traced", t.Lines.Count(l => l.Contains("out of range")) >= 7,
                    $"{t.Lines.Count(l => l.Contains("out of range"))} clamp lines");
        }

        // ── unknown Help language → English, not "nothing selected" ─────────────────────────────────
        {
            var cfg = ConfigLoader.TryParse("{\"system\":{\"language\":\"kl\"}}");
            H.Check("unknown language normalised",
                    cfg is not null && HelpLocalization.Normalize(cfg.System.Language) == cfg.System.Language,
                    $"Language = \"{cfg?.System?.Language}\"");
            H.Check("\"ar\" is a known help language that normalises to itself",
                    HelpLocalization.IsKnown("ar") && HelpLocalization.Normalize("AR") == "ar");
        }

        // ── a passthru-mode entry with a blank path is dropped (it would match everything) ───────────────
        {
            var cfg = ConfigLoader.TryParse(
                "{\"system\":{\"safeModeApps\":[{\"path\":\"\"},null,{\"path\":\"C:\\\\g\\\\g.exe\"}]}}");
            H.Check("blank/null safeModeApps entries dropped", cfg?.System?.SafeModeApps?.Count == 1,
                    $"{cfg?.System?.SafeModeApps?.Count} entries kept");
        }

        // ── enormous label → clamped. NOTE: the checklist says ConfigLoader has no label clamp; as of
        //    WheelSlice.MaxLabelLength (200) it does, in the property accessor. This proves which. ─────
        {
            var huge = new string('A', 200_000);
            var cfg = ConfigLoader.TryParse(
                $"{{\"wheelA\":[{{\"label\":\"{huge}\"}}]}}");
            int len = cfg?.WheelA?[0]?.Label?.Length ?? -1;
            H.Check("200,000-char label clamped to 200", len == WheelSlice.MaxLabelLength,
                    $"kept {len} chars (MaxLabelLength = {WheelSlice.MaxLabelLength})");
        }

        // ── enormous PAYLOAD (path / keys / chat text). No clamp is claimed for these — this records
        //    what actually happens so the checklist can say something true. ────────────────────────────
        {
            var huge = new string('B', 200_000);
            var cfg = ConfigLoader.TryParse(
                "{\"wheelA\":[{\"label\":\"x\",\"action\":{\"type\":\"launch\",\"path\":\"" + huge
                + "\",\"keys\":\"" + huge + "\",\"chatText\":\"" + huge + "\"}}]}");
            var a = cfg?.WheelA?[0]?.Action;
            H.Check("200,000-char payload parses without throwing", cfg is not null);
            Console.WriteLine($"        (record) path={a?.Path?.Length}, keys={a?.Keys?.Length}, chatText={a?.ChatText?.Length} chars kept — no clamp claimed for payloads");
        }

        // ── deep nesting: a sequence of sequences. System.Text.Json's default depth limit is 64. ─────
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 200; i++) sb.Append("{\"type\":\"sequence\",\"steps\":[");
            sb.Append("{\"type\":\"system\",\"command\":\"lock\"}");
            for (int i = 0; i < 200; i++) sb.Append("]}");
            H.Try("200-deep nested sequence doesn't crash the parser", () =>
            {
                var cfg = ConfigLoader.TryParse($"{{\"wheelA\":[{{\"label\":\"x\",\"action\":{sb}}}]}}");
                Console.WriteLine($"        (record) TryParse returned {(cfg is null ? "null → falls back to .bak/defaults" : "a config")}");
                H.Pass("200-deep nested sequence handled without throwing");
            });
        }

        // ── truncated / non-JSON → null, so the caller can fall back ─────────────────────────────────
        {
            H.Check("truncated JSON → null", ConfigLoader.TryParse("{\"wheelA\":[{\"label\":\"x\"") is null);
            H.Check("not JSON at all → null", ConfigLoader.TryParse("<html>nope</html>") is null);
            H.Check("empty file → null", ConfigLoader.TryParse("") is null);
            H.Check("JSON array (wrong root) → null", ConfigLoader.TryParse("[1,2,3]") is null);
            H.Check("NaN literal → null", ConfigLoader.TryParse("{\"system\":{\"driftPx\":NaN}}") is null);
        }

        // ── wrong TYPES in every field the renderer trusts ───────────────────────────────────────────
        {
            H.Try("string where a number belongs → null, not a throw", () =>
            {
                var cfg = ConfigLoader.TryParse("{\"system\":{\"fadeMs\":\"lots\"}}");
                H.Check("fadeMs:\"lots\" → null", cfg is null);
            });
            H.Try("object where an array belongs → null, not a throw", () =>
            {
                var cfg = ConfigLoader.TryParse("{\"wheelA\":{\"nope\":1}}");
                H.Check("wheelA as an object → null", cfg is null);
            });
        }

        // ── an OLD config carrying the removed arcadeScale/arcadeRadialAiming keys: they're accepted on
        //    load (unknown properties are ignored), every ordinary value beside them survives, and a save
        //    drops both from the file for good — no migration, since nothing ever derived from them. ─────
        {
            const string oldStyleJson = """
                {
                  "wheelA": [ { "label": "old-style-slice", "action": { "type": "system", "command": "lock" } } ],
                  "system": {
                    "fadeMs": 400,
                    "sliceMaterial": "mesa",
                    "showTrayIcon": false,
                    "obsPort": 12345,
                    "dpadHorizontalMode": "song",
                    "mixBalance": 30,
                    "arcadeScale": 160,
                    "arcadeRadialAiming": true
                  }
                }
                """;
            File.WriteAllText(ConfigLoader.ConfigPath, oldStyleJson);
            using var loader = new ConfigLoader(new SynchronizationContext());
            var sys = loader.Current.System;
            H.Check("ordinary values survive a file carrying the removed keys",
                    loader.Current.WheelA.Length == 1 && loader.Current.WheelA[0].Label == "old-style-slice"
                    && sys.FadeMs == 400 && sys.SliceMaterial == "mesa" && sys.ShowTrayIcon == false
                    && sys.ObsPort == 12345 && sys.DpadHorizontalMode == "song" && sys.MixBalance == 30,
                    $"fadeMs={sys.FadeMs} sliceMaterial={sys.SliceMaterial} showTrayIcon={sys.ShowTrayIcon} "
                    + $"obsPort={sys.ObsPort} dpadHorizontalMode={sys.DpadHorizontalMode} mixBalance={sys.MixBalance}");

            H.Check("a save drops the removed keys from the file",
                    loader.WriteConfig(loader.Current)
                    && !File.ReadAllText(ConfigLoader.ConfigPath).Contains("arcadeScale", StringComparison.OrdinalIgnoreCase)
                    && !File.ReadAllText(ConfigLoader.ConfigPath).Contains("arcadeRadialAiming", StringComparison.OrdinalIgnoreCase));

            using var reloaded = new ConfigLoader(new SynchronizationContext());
            var rs = reloaded.Current.System;
            H.Check("a reload after the save still has every ordinary value",
                    reloaded.Current.WheelA.Length == 1 && reloaded.Current.WheelA[0].Label == "old-style-slice"
                    && rs.FadeMs == sys.FadeMs && rs.SliceMaterial == sys.SliceMaterial
                    && rs.ShowTrayIcon == sys.ShowTrayIcon && rs.ObsPort == sys.ObsPort
                    && rs.DpadHorizontalMode == sys.DpadHorizontalMode && rs.MixBalance == sys.MixBalance);
        }

        MirroredFieldsCases();
    }

    /// <summary>The write-back guard for Settings controls that mirror fields an outside writer also owns.
    /// A tiny fake "control" stands in for the WPF one: a bool, a (thickness, demoted) pair and a trigger list.</summary>
    private static void MirroredFieldsCases()
    {
        H.Group("MirroredFields — a Settings control writes a shared field only when the user moved it");

        bool safe = false; string thick = "medium"; bool demoted = false;
        var tokens = new List<string> { "fn" };
        var mirror = new MirroredFields()
            .Add("safe", c => c.CaptureSafeMode, () => safe, (c, v) => c with { CaptureSafeMode = v })
            .Add("thickness", c => (Thickness: c.SliceThickness, Demoted: c.ThickAutoDemoted),
                 () => (Thickness: thick, Demoted: demoted),
                 (c, v) => c with { SliceThickness = v.Thickness, ThickAutoDemoted = v.Demoted })
            .Add<List<string>>("triggers", c => new List<string>(c.TriggerModesFor(ControllerKind.DualSenseEdge)),
                 () => tokens,
                 (c, v) => c with { TriggerModes = new Dictionary<string, List<string>>(c.TriggerModes)
                                                   { [ControllerKind.DualSenseEdge.ToString()] = new List<string>(v) } },
                 MirroredFields.StringSequence, MirroredFields.CopyStrings);

        var loaded = new SystemConfig
        {
            TriggerModes = new() { [ControllerKind.DualSenseEdge.ToString()] = new List<string> { "fn" } },
        };

        H.Check("a control not yet captured writes nothing",
                mirror.ApplyTo(loaded with { CaptureSafeMode = true }).CaptureSafeMode);
        mirror.Capture(loaded);

        var outside = loaded with
        {
            CaptureSafeMode = true, SliceThickness = "thin", ThickAutoDemoted = true,
            TriggerModes = new() { [ControllerKind.DualSenseEdge.ToString()] = new List<string> { "l3r3" },
                                   ["Xbox"] = new List<string> { "view" } },
        };
        var saved = mirror.ApplyTo(outside);
        H.Check("untouched controls keep every value an outside writer set",
                saved.CaptureSafeMode && saved.SliceThickness == "thin" && saved.ThickAutoDemoted
                && saved.TriggerModesFor(ControllerKind.DualSenseEdge).SequenceEqual(new[] { "l3r3" })
                && saved.TriggerModes.ContainsKey("Xbox"));
        H.Check("…and the outside change is reported for the host's refresh", mirror.ChangedOutside(outside));
        H.Check("a config that still matches what the control was loaded from reports nothing",
                !mirror.ChangedOutside(loaded));

        mirror.NoteSaved(saved);
        H.Check("a save that wrote none of them leaves the outside change reported until the control re-Loads",
                mirror.ChangedOutside(saved));
        safe = true; thick = "thin"; demoted = true; tokens = new List<string> { "l3r3" };
        mirror.Capture(saved);
        H.Check("a re-Load re-bases every field", !mirror.ChangedOutside(saved));

        // The user moves two controls while the config also moved both: the user's values win, the rest stay.
        tokens = new List<string> { "fn", "touchpad" };
        thick = "thick"; demoted = false;
        var both = saved with
        {
            CaptureSafeMode = false, SliceThickness = "medium",
            TriggerModes = new() { [ControllerKind.DualSenseEdge.ToString()] = new List<string> { "other" },
                                   ["Xbox"] = new List<string> { "view" } },
        };
        var written = mirror.ApplyTo(both);
        H.Check("a control the user moved wins over an outside change to the same field",
                written.SliceThickness == "thick" && !written.ThickAutoDemoted
                && written.TriggerModesFor(ControllerKind.DualSenseEdge).SequenceEqual(new[] { "fn", "touchpad" }));
        H.Check("…while a control the user left alone keeps the outside value", !written.CaptureSafeMode);
        H.Check("…and the other controller kinds' choices come from the live config", written.TriggerModes.ContainsKey("Xbox"));

        mirror.NoteSaved(written);
        H.Check("after a save the written controls are in step again: a later outside value is kept",
                mirror.ApplyTo(written with { SliceThickness = "thin" }).SliceThickness == "thin");
        H.Check("a field the save did not write is still reported as changed outside", mirror.ChangedOutside(written));

        // A list is compared by value: a fresh equal list is not a move; an edit in place is one.
        mirror.Capture(written);
        var probe = written with
        {
            TriggerModes = new() { [ControllerKind.DualSenseEdge.ToString()] = new List<string> { "zzz" } },
        };
        tokens = new List<string>(tokens);
        H.Check("an equal copy of the list is not a change",
                mirror.ApplyTo(probe).TriggerModesFor(ControllerKind.DualSenseEdge).SequenceEqual(new[] { "zzz" }));
        tokens.Add("select");
        H.Check("an edit in place to the list is seen as a change",
                mirror.ApplyTo(probe).TriggerModesFor(ControllerKind.DualSenseEdge).SequenceEqual(tokens));

        // One field of the set can be re-based alone.
        thick = "thin";
        H.Check("without a re-base the moved thickness control writes",
                mirror.ApplyTo(probe).SliceThickness == "thin");
        mirror.Rebase("thickness", probe with { SliceThickness = "thin" });
        H.Check("Rebase re-snapshots only the named field",
                mirror.ApplyTo(probe).SliceThickness == probe.SliceThickness
                && mirror.ApplyTo(probe).TriggerModesFor(ControllerKind.DualSenseEdge).SequenceEqual(tokens));
    }
}
