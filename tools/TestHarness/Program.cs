using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Radiata.TestHarness;

/// <summary>Radiata.dll is referenced straight out of its output folder, so the harness's own deps.json
/// knows nothing about its TRANSITIVE dependencies (System.Text.Json 10, NAudio, SQLitePCLRaw, the Nefarius
/// clients…). Rather than mirror every version by hand — which would silently rot the first time the app
/// updates a package — resolve any miss from the app's output folder, which is by definition the exact set
/// the app runs against.</summary>
internal static class DepResolver
{
    [ModuleInitializer]
    internal static void Install()
    {
        var appDir = FindAppOutput();
        if (appDir is null) return;
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            var dll = Path.Combine(appDir, name.Name + ".dll");
            return File.Exists(dll) ? ctx.LoadFromAssemblyPath(dll) : null;
        };
    }

    private static string FindAppOutput()
    {
        // Anchor on the repo root (the app csproj), NOT on "a folder containing Radiata.dll" — the harness's
        // own output holds a copy of Radiata.dll but none of its transitive package deps, so matching on that
        // finds the wrong folder and the resolver silently does nothing.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "ControllerWheel.csproj"))) continue;
            var candidate = Path.Combine(dir.FullName, "bin", "Debug", "net8.0-windows");
            return File.Exists(Path.Combine(candidate, "Radiata.dll")) ? candidate : null;
        }
        return null;
    }
}

/// <summary>Headless checks for the behaviours that don't need a controller in a hand.
/// Groups are selected on the command line so a destructive-ish group (audio, power) is never run by
/// accident: <c>TestHarness config help</c>. <c>all</c> runs the non-destructive set.</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Initialize before any persistence type so mutation tests cannot reach the user's settings.
        var testData = Path.Combine(Path.GetTempPath(), "radiata-regression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testData);
        AppContext.SetData("Radiata.TestDataDirectory", testData);
        Console.WriteLine($"Isolated application data: {testData}");
        var groups = args.Length == 0 ? new[] { "all" } : args;
        // render:snapshot <dir> / render:compare <baselineDir> [outDir] take paths, not further groups — see T_Render.
        if (T_Render.Handles(groups)) { T_Render.Run(groups); groups = []; }
        // render:screens <dir> — the arcade games' text screens as PNGs to paint over; see T_RenderScreens.
        if (T_RenderScreens.Handles(groups)) { T_RenderScreens.Run(groups); groups = []; }
        if (groups.Contains("all"))
            groups = new[] { "config", "restore", "gamelib", "help", "art", "package", "textchat", "arcade", "sfx",
                             "narration", "glyphs", "update", "hidid", "triggers", "loc", "locregress", "passthru",
                             "hygiene", "remediation", "steamconsent", "sentry", "workshop", "wheelsm", "churn", "stuckroot", "pairs" };

        foreach (var g in groups)
        {
            switch (g.ToLowerInvariant())
            {
                case "config":  T_Config.Run();  break;
                case "remediation": T_Remediation.Run(); break;
                case "restore": T_Restore.Run(); break;
                case "gamelib": T_GameLib.Run(); break;
                case "help":    T_Help.Run();    break;
                case "art":     T_Art.Run();     break;
                case "actions": T_Actions.Run(); break;
                case "package": T_Package.Run(); break;
                case "payload": T_Payload.Run(); break;
                case "textchat": T_TextChat.Run(); break;
                // Pure in-memory gate check (no machine state, no UI, no config write) — safe in the
                // default set. It DOES flip Arcade.ConcealedForHarness and restores it.
                case "arcade": T_Arcade.Run(); break;
                // Every ArcadeSfx bank take is an embedded WAV (skips in a build that ships none — the public mirror).
                case "sfx": T_ArcadeSfx.Run(); break;
                // Announcer coordination against a FAKE sink — no SAPI voice is constructed, so this runs
                // on a machine with no audio endpoints at all. Safe in the default set.
                case "narration": T_Narration.Run(); T_Narration.RunPeer(); break;
                // Source scan: no drawn string spells a face glyph out instead of asking ControllerButtons.
                // Read-only and fast, so it belongs in the default set — and it has to BE in it, because
                // the bug it guards is only visible after switching the Button icons setting.
                case "glyphs": T_Glyphs.Run(); break;
                // Update-feed parse/version-compare + crash-report text rules — pure Core, no network.
                case "update": T_Update.Run(); break;
                case "steamconsent": T_SteamConsent.Run(); break;
                case "sentry": T_Sentry.Run(); break;
                // HID instance-id vid/pid parse (USB + BT shapes) — pure Core, guards orphan adoption.
                case "hidid": T_HidInstanceId.Run(); break;
                // Summon-gesture catalog consistency — every offered chord has copy AND an interpreter
                // case. Pure Core, no pad; guards a class of failure that only shows up in a user's hands.
                case "triggers": T_Triggers.Run(); break;
                // Drawn surfaces stay left-to-right and the language table is consistent — pure construction,
                // no config, no pad, no window shown. Safe in the default set.
                case "loc": T_Loc.Run(); break;
                // Translation-style regressions (register, tokens, Arabic/Japanese conventions, culture sort, site
                // structure). Pure catalog scans plus a reflective flip of Loc.Lang that is always restored.
                case "locregress": T_LocRegress.Run(); break;
                case "workshop": T_Workshop.Run(); break;
                // Stick-arming, dwells and in-wheel edit ops — pure Core, no pad, no window.
                case "wheelsm": T_WheelStateMachine.Run(); break;
                // The pad-count churn breaker's rule on an injected clock — pure Core, no pad, no timer.
                case "churn": T_Churn.Run(); break;
                // A childless Xbox composite root: settling, then stuck, on an injected clock — pure Core.
                case "stuckroot": T_StuckRoot.Run(); break;
                // Facts kept in two places (Run key, update-feed prefix, payload manifest, range clamps):
                // each check parses the far side from source and compares it with the built assemblies.
                // Read-only; the checks that read development-only files skip in the public snapshot.
                case "pairs": T_Consistency.Run(); break;
                case "launcher-shot": T_SiteShots.LauncherShot(); break;
                case "launcher-shot-long":
                    T_SiteShots.LauncherShot("Extraordinary Fireflies", Path.Combine(Path.GetTempPath(), "radiata-launcher-long1.png"));
                    T_SiteShots.LauncherShot("Supercalifragilisticexpi", Path.Combine(Path.GetTempPath(), "radiata-launcher-long2.png"));
                    break;
                // Settings checkbox wiring, the live-file contract (development repository only), CONTROLS.md
                // sync — mechanical guards with no machine state, no UI, no config write.
                case "hygiene": T_Hygiene.Run(); break;
                case "passthru": T_Passthru.Run(); break;
                case "cloak": T_Cloak.Run(); break;
                // Exercise adoption against an isolated driver; never mutate the user's cloak for a regression.
                case "adopt": T_Remediation.CloakRecovery(); break;
                case "orphans": T_Orphans.Run(apply: false); break;
                case "orphans:apply": T_Orphans.Run(apply: true); break;
                case "artsweep": T_ArtSweep.Run(force: false); break;
                case "artsweep:force": T_ArtSweep.Run(force: true); break;
                case "toggle": T_Toggle.Run(); break;
                // Drives the real art-cache trim against a REDIRECTED cache dir (temp folder, synthetic
                // files). Skips rather than touching the real cache if the redirect fails.
                case "artquota": T_ArtQuota.Run(); break;
                // Read-only: does the uninstaller's ARP lookup actually match the drivers installed here?
                // (A DisplayName needle mismatch once made ViGEmBus removal a silent no-op that reported
                // success.) Repeatable, no UAC, unlike driving the real uninstall.
                case "drivermatch": T_DriverMatch.Run(); break;
                // Recovery, not a test: cycle the default output until it lands on a device whose name
                // contains <needle>. Exists because the audio group's baseline bug once left the machine's
                // default one step from where the user had it, and Windows offers no scriptable way back.
                case string s when s.StartsWith("audio:"):
                    T_Actions.SetDefaultOutput(s["audio:".Length..]);
                    break;
                // Diagnostic: run one config file through the real load path and report what survived.
                // "the app ignored my config" is otherwise very hard to tell from "the app rejected it".
                case string s when s.StartsWith("parse:"):
                {
                    var path = s["parse:".Length..];
                    var json = System.IO.File.ReadAllText(path);
                    var cfg = ControllerWheel.ConfigLoader.TryParse(json);
                    Console.WriteLine($"TryParse({path}) → {(cfg is null ? "NULL (rejected)" : "ok")}");
                    if (cfg is not null)
                    {
                        Console.WriteLine($"  wheelA={cfg.WheelA.Length} wheelB={cfg.WheelB.Length} "
                                          + $"safeModeApps={cfg.System.SafeModeApps.Count} "
                                          + $"captureSafeMode={cfg.System.CaptureSafeMode}");
                        // Slice action types matter for the Arcade kill-switch round-trip: a hidden-but-
                        // supported type must survive a load + re-serialize untouched.
                        foreach (var slice in cfg.WheelA.Concat(cfg.WheelB))
                            Console.WriteLine($"  slice \"{slice.Label}\" action.type=\"{slice.Action?.Type}\" "
                                              + $"command=\"{slice.Action?.Command}\"");
                        var round = System.Text.Json.JsonSerializer.Serialize(cfg, new System.Text.Json.JsonSerializerOptions
                        { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = false });
                        Console.WriteLine($"  re-serialized keeps \"type\":\"arcade\": "
                                          + round.Contains("\"type\":\"arcade\""));
                    }
                    break;
                }
                default:
                    Console.WriteLine($"unknown group '{g}' — try: config restore gamelib help art actions");
                    return 99;
            }
        }

        Console.WriteLine();
        var skipNote = H.SkippedDevOnly > 0 ? $" ({H.SkippedDevOnly} development-repository)" : "";
        Console.WriteLine($"══ {H.Passed} passed, {H.Failed} failed, {H.Skipped} skipped{skipNote} ".PadRight(78, '═'));
        return H.Failed;
    }
}
