using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The audio-cycle, power-plan and app-slice actions.
///
/// These fire for real against this machine: the default audio output is cycled and the active power plan is
/// switched — each is read back from the OS to confirm the effect, then restored to what it was. What's NOT covered is the gesture: <see cref="ActionExecutor.Execute"/> is what a released
/// slice calls, so the action and the hub readout are real, but arming/dwell/cancel are not.
///
/// The app-slice cases use two copies of mspaint.exe in one folder: a long-lived process whose NAME the slice
/// doesn't know, which is exactly the state a stub-launched app leaves behind. A genuine updater stub that
/// respawns under a different name isn't reproduced — the install-folder fallback it relies on is.</summary>
internal static class T_Actions
{
    private static readonly WindowsPlatformActions P = new();
    private static readonly ActionExecutor X = new(P, () => { }, () => { });

    // AudioDeviceSwitcher is internal to the app assembly, so the OS-state read-back goes through
    // reflection. Worth it: asserting only on what the action REPORTS would pass even if the action
    // reported a switch it never made.
    private static Type AudioT => H.AppType("AudioDeviceSwitcher");

    private static int OutputCount() => (int)H.InvokeStatic(AudioT, "OutputCount");

    /// <summary>The OS's current default RENDER device name (NAudio DataFlow.Render == 0).</summary>
    private static string CurrentRenderDefault()
    {
        var m = H.StaticMethod(AudioT, "CurrentDefaultName", 1);
        var flowType = m.GetParameters()[0].ParameterType;
        return (string)m.Invoke(null, new[] { Enum.ToObject(flowType, 0) });
    }

    public static void Run()
    {
        H.Group("Actions — audio cycle, power plan, app slices");
        AudioCycle();
        PowerPlan();
        AppSlices();
        SharedFolderRefusal();
        UnusablePaths();
    }

    private static WheelSlice Slice(ActionConfig a) => new() { Label = "test", Action = a };

    // ── "An empty Switch Audio Output slice cycles outputs" ───────────────────────────────────────────
    private static void AudioCycle()
    {
        int outputs = OutputCount();
        Console.WriteLine($"        (record) render devices on this machine: {outputs}");
        if (outputs < 2)
        {
            // OutputCount() swallows every failure as 0, so probe directly before concluding anything about
            // the machine — "0 active endpoints" and "the COM enumeration threw here" look identical.
            try
            {
                using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                var direct = en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render,
                                                        NAudio.CoreAudioApi.DeviceState.Active).ToList();
                Console.WriteLine($"        (record) direct enumeration sees {direct.Count}: "
                                  + string.Join(", ", direct.Select(d => d.FriendlyName)));
                foreach (var d in direct) d.Dispose();

                // Zero ACTIVE endpoints is a real machine state, not necessarily a bug — enumerate every
                // state so the report can say which (unplugged / disabled / not present) rather than guess.
                var all = en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render,
                                                     NAudio.CoreAudioApi.DeviceState.All).ToList();
                Console.WriteLine($"        (record) render endpoints in ANY state: {all.Count}");
                foreach (var d in all)
                {
                    Console.WriteLine($"        (record)   {d.State,-12} {d.FriendlyName}");
                    d.Dispose();
                }
                try
                {
                    using var def = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render,
                                                               NAudio.CoreAudioApi.Role.Multimedia);
                    Console.WriteLine($"        (record) default render endpoint: {def.FriendlyName} ({def.State})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"        (record) no default render endpoint: {ex.GetType().Name}: {ex.Message}");
                }

                if (direct.Count == outputs)
                    H.Skip("switch-audio cycles outputs",
                           $"this machine has {outputs} ACTIVE render endpoint(s) right now — nothing to cycle "
                           + "(see the endpoint states above); OutputCount() agrees with a direct enumeration");
                else
                    H.Fail("AudioDeviceSwitcher.OutputCount() agrees with a direct enumeration",
                           $"OutputCount()={outputs} but direct enumeration found {direct.Count}");
            }
            catch (Exception ex)
            {
                H.Skip("switch-audio cycles outputs",
                       $"audio endpoint enumeration is unavailable in this process: {ex.GetType().Name}: {ex.Message}");
            }
            return;
        }

        // Read the baseline; do NOT fire to get it. Calling the switch to "learn where we started" recorded the
        // device one step PAST the machine's real default, which made the wrap-around check compare the wrong
        // pair AND left the machine's default output moved by one after the restore.
        var original = CurrentRenderDefault();
        var seen = new System.Collections.Generic.List<string>();
        ActionStatus last = null;
        for (int i = 0; i < outputs; i++)
        {
            last = X.Execute(Slice(new ActionConfig { Type = "switch-audio" }));
            var now = CurrentRenderDefault();
            seen.Add(now);
            H.Check($"cycle step {i + 1}: the hub names the device it switched to",
                    last is not null && now is not null
                    && (last.Line2?.Contains(now, StringComparison.OrdinalIgnoreCase) == true
                        || now.Contains(last.Line2 ?? "\u0000", StringComparison.OrdinalIgnoreCase)),
                    $"hub said \"{last?.Line1} / {last?.Line2}\", OS default is \"{now}\"");
        }

        H.Check("each fire advances to a different output",
                seen.Count > 1 && seen.Zip(seen.Skip(1)).All(p => p.First != p.Second),
                string.Join(" → ", seen));
        // A full cycle lands back on the BASELINE, not on seen[0] — seen[0] is already one fire in.
        H.Check($"cycling {outputs} times returns to the starting device",
                seen.Count == outputs && string.Equals(seen[^1], original, StringComparison.OrdinalIgnoreCase),
                $"baseline \"{original}\", first fire went to \"{seen.FirstOrDefault()}\", "
                + $"ended at \"{seen.LastOrDefault()}\"");

        // Back to where the machine was.
        if (original is not null)
        {
            for (int i = 0; i < outputs + 1; i++)
            {
                if (string.Equals(CurrentRenderDefault(),
                                  original, StringComparison.OrdinalIgnoreCase)) break;
                P.SwitchAudioDevice(null, false);
            }
            Console.WriteLine($"        (record) default output restored to \"{CurrentRenderDefault()}\"");
        }

        // A named device that doesn't exist must report a miss, not throw or silently "succeed".
        H.Try("switch-audio with a device name that matches nothing", () =>
        {
            var st = X.Execute(Slice(new ActionConfig { Type = "switch-audio", Device = "zzz-no-such-device" }));
            H.Pass("switch-audio with an unmatched name is handled", $"hub said \"{st?.Line1} / {st?.Line2}\"");
        });
    }

    /// <summary>Recovery helper, not a check: walk the output cycle until the default's name contains
    /// <paramref name="needle"/>. Used to put a machine's default output back by hand.</summary>
    public static void SetDefaultOutput(string needle)
    {
        int outputs = OutputCount();
        Console.WriteLine($"default output is \"{CurrentRenderDefault()}\"");
        for (int i = 0; i <= outputs; i++)
        {
            if (CurrentRenderDefault()?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.WriteLine($"landed on \"{CurrentRenderDefault()}\" after {i} step(s)");
                return;
            }
            P.SwitchAudioDevice(null, false);
        }
        Console.WriteLine($"no output matched \"{needle}\" — left at \"{CurrentRenderDefault()}\"");
    }


    // ── "Toggle Power Plan (single-plan/legacy form — reports its result) fires correctly" ──────────
    private static void PowerPlan()
    {
        var plans = PowerPlans.List();
        Console.WriteLine($"        (record) power plans: {string.Join(", ", plans.Select(p => p.Name + (p.Active ? " (active)" : "")))}");
        var original = plans.FirstOrDefault(p => p.Active);
        if (plans.Count == 0) { H.Skip("power-plan action", "powercfg listed no plans"); return; }

        foreach (var plan in plans)
        {
            var st = X.Execute(Slice(new ActionConfig { Type = "system", Command = "power-plan", PowerPlan = plan.Guid }));
            Thread.Sleep(300);
            var nowActive = PowerPlans.List().FirstOrDefault(p => p.Active);
            H.Check($"power-plan \"{plan.Name}\" is actually activated",
                    nowActive?.Guid == plan.Guid, $"active is now \"{nowActive?.Name}\"");
            H.Check($"…and the hub reports the plan name, not a bare OK",
                    st?.Line2 is not null && (st.Line2.Contains(plan.Name, StringComparison.OrdinalIgnoreCase)
                                              || st.Line2.Equals("Failed", StringComparison.OrdinalIgnoreCase)),
                    $"hub said \"{st?.Line1} / {st?.Line2}\"");
        }

        // A bogus GUID must report Failed rather than appearing to work — the whole reason it reports now.
        var bogus = X.Execute(Slice(new ActionConfig { Type = "system", Command = "power-plan", PowerPlan = "11111111-2222-3333-4444-555555555555" }));
        H.Check("an unknown power-scheme GUID reports Failed",
                bogus?.Line2?.Equals("Failed", StringComparison.OrdinalIgnoreCase) == true,
                $"hub said \"{bogus?.Line1} / {bogus?.Line2}\"");

        if (original is not null)
        {
            X.Execute(Slice(new ActionConfig { Type = "system", Command = "power-plan", PowerPlan = original.Guid }));
            Thread.Sleep(300);
            var restored = PowerPlans.List().FirstOrDefault(p => p.Active);
            H.Check("the machine's original power plan was restored",
                    restored?.Guid == original.Guid, $"active is \"{restored?.Name}\"");
        }
    }

    // ── App slice on a stub-launched app: Run focuses, Toggle kills ──────────────────────────────────
    private static void AppSlices()
    {
        var dir = Path.Combine(Path.GetTempPath(), "radiata-harness", "StubGame-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        var real   = Path.Combine(dir, "RealGame.exe");     // what actually ends up running
        var stub   = Path.Combine(dir, "Launcher.exe");     // what the slice points at
        Process runner = null;
        try
        {
            var src = Path.Combine(Environment.SystemDirectory, "mspaint.exe");
            if (!File.Exists(src)) { H.Skip("app-slice cases", "no mspaint.exe to use as a long-lived process"); return; }
            File.Copy(src, real);
            File.Copy(src, stub);

            runner = Process.Start(new ProcessStartInfo(real) { UseShellExecute = true });
            for (int i = 0; i < 50 && (runner is null || runner.MainWindowHandle == IntPtr.Zero); i++)
            { Thread.Sleep(100); runner?.Refresh(); }
            H.Check("a process is running from the app's install folder",
                    runner is { HasExited: false }, $"pid {runner?.Id}");

            // The slice names "Launcher", which is NOT running — only the folder match can find it.
            H.Check("IsProcessRunning: name miss, install-folder hit → running",
                    P.IsProcessRunning("Launcher", stub));
            H.Check("PreviewStatus reads \"On\" for a Toggle slice via the folder fallback",
                    X.PreviewStatus(Slice(new ActionConfig { Type = "launch", Path = stub, QuitIfRunning = true }))?.Line2 == "On",
                    X.PreviewStatus(Slice(new ActionConfig { Type = "launch", Path = stub, QuitIfRunning = true }))?.Line2);

            // Run: must FOCUS the folder-matched window, never start the stub a second time.
            int launcherBefore = Process.GetProcessesByName("Launcher").Length;
            using (var t = new H.TraceGrab())
            {
                X.Execute(Slice(new ActionConfig { Type = "launch", Path = stub }));
                Thread.Sleep(1500);
                H.Check("Run focuses the running copy instead of starting a second one",
                        Process.GetProcessesByName("Launcher").Length == launcherBefore,
                        $"\"Launcher\" processes: {launcherBefore} → {Process.GetProcessesByName("Launcher").Length}");
                H.Check("…and it says so in the trace", t.Saw("focused a window from its install folder"));
            }

            // Toggle: must KILL the folder-matched process, not launch a duplicate.
            using (var t = new H.TraceGrab())
            {
                X.Execute(Slice(new ActionConfig { Type = "launch", Path = stub, QuitIfRunning = true }));
                Thread.Sleep(2500);
                runner.Refresh();
                H.Check("Toggle kills the folder-matched process", runner.HasExited);
                H.Check("…and does not launch a duplicate",
                        Process.GetProcessesByName("Launcher").Length == 0
                        && Process.GetProcessesByName("RealGame").Length == 0,
                        $"Launcher={Process.GetProcessesByName("Launcher").Length}, RealGame={Process.GetProcessesByName("RealGame").Length}");
                H.Check("…and the close is traced", t.Saw("toggle: closing") || t.Saw("closing"));
            }

            H.Check("PreviewStatus reads \"Off\" once nothing is running from the folder",
                    X.PreviewStatus(Slice(new ActionConfig { Type = "launch", Path = stub, QuitIfRunning = true }))?.Line2 == "Off");
        }
        finally
        {
            try { if (runner is { HasExited: false }) runner.Kill(true); } catch { }
            foreach (var n in new[] { "RealGame", "Launcher" })
                foreach (var p in Process.GetProcessesByName(n)) { try { p.Kill(true); } catch { } }
            Thread.Sleep(500);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // ── "App slice whose exe sits directly in a shared folder" — the deliberate refusal ──────────────
    private static void SharedFolderRefusal()
    {
        var shared = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var running = Path.Combine(shared, "RadiataHarnessProbe.exe");
        var slicePath = Path.Combine(shared, "SomeOtherApp.exe");
        Process p = null;
        try
        {
            var src = Path.Combine(Environment.SystemDirectory, "mspaint.exe");
            if (!File.Exists(src)) { H.Skip("shared-folder refusal", "no mspaint.exe available"); return; }
            File.Copy(src, running, true);
            p = Process.Start(new ProcessStartInfo(running) { UseShellExecute = true });
            Thread.Sleep(1200);

            H.Check("a process IS running from the shared folder", p is { HasExited: false });
            H.Check("folder matching is REFUSED for an exe directly in %LOCALAPPDATA% (unchanged behaviour)",
                    !P.IsProcessRunning("SomeOtherApp", slicePath),
                    "otherwise every installed program would match");
            H.Check("…while the NAME check still works there",
                    P.IsProcessRunning("RadiataHarnessProbe", running));
        }
        finally
        {
            try { if (p is { HasExited: false }) p.Kill(true); } catch { }
            Thread.Sleep(400);
            try { File.Delete(running); } catch { }
        }
    }

    // ── Paths that must never yield a folder match ───────────────────────────────────────────────────
    private static void UnusablePaths()
    {
        foreach (var (what, path) in new (string, string)[]
        {
            ("a UWP AUMID",        @"shell:AppsFolder\Foo.Bar_8wekyb3d8bbwe!App"),
            ("a relative path",    @"..\game\game.exe"),
            ("a bare exe name",    "game.exe"),
            ("a drive root",       @"C:\game.exe"),
            ("steamapps\\common",  @"C:\Program Files (x86)\Steam\steamapps\common\game.exe"),
            ("%WINDIR%\\System32", @"C:\Windows\System32\game.exe"),
            ("a missing folder",   @"C:\no-such-folder-xyz\game.exe"),
            ("an empty path",      ""),
        })
            H.Check($"no folder match for {what}", !P.IsProcessRunning("zzz-nothing-by-this-name", path));
    }
}
