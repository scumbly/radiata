using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControllerWheel;

namespace Radiata.TestHarness;

internal static class T_Remediation
{
    public static void Run()
    {
        H.Group("Remediation — real filesystem failures, rename notifications, decoded image bounds");
        H.Try("configuration persistence", ConfigPersistence);
        H.Try("corrupt configuration preservation failure", CorruptPreservationFailure);
        H.Try("image decoded bounds", ImageBounds);
        H.Try("unsupported Arcade data preservation", ArcadePreservation);
        H.Try("transactional restore rollback", RestoreRollback);
        H.Try("restored cover paths", RestoreCovers);
        H.Try("controller reports and replay", ControllerReports);
        H.Try("settings notification failure", NotificationFailure);
        H.Try("HidHide ownership and recovery failures", CloakRecovery);
        H.Try("literal portable uninstall", LiteralUninstall);
        H.Try("protected secrets and automatic backups", SecretBackups);
        H.Try("keyboard grammar and modifier ownership", KeyboardPlans);
        H.Try("Discord timeout retains operation ownership", DiscordDeadline);
        H.Try("Discord real pipe fragmentation and cancellation", T_RpcFaults.Run);
        H.Try("audio policy error outcomes", AudioPolicy);
        H.Try("Arcade music failure backoff", MusicBackoff);
        H.Try("event-driven controller navigation repeat", NavigationRepeat);
        H.Try("unreadable controller ancestry", UnknownAncestry);
        H.Try("recovery task account and install ownership", RecoveryOwnership);
        H.Try("staged helper cleanup preserves unrelated data", StagedCleanup);
        H.Try("driver architecture boundary", DriverArchitecture);
        H.Try("XInput identity ambiguity", XInputIdentity);
        H.Try("configuration watcher shutdown", WatcherShutdown);
        H.Try("analog backlog preserves release ordering", AnalogBacklog);
        H.Try("UI-thread gesture reset and configure ordering", GestureOrdering);
        H.Try("onboarding save failure and retry", OnboardingSaveFailure);
        H.Try("update respects unsaved-edit veto", UpdateVeto);
        H.Try("controller family continuity through transport loss", CaptureContinuity);
    }

    private static void CaptureContinuity()
    {
        var capture = new ControllerCaptureContinuity();
        H.Check("cold start has no remembered Xbox grace", !capture.RetainNeutralXboxTarget(false, true, true, true));
        capture.ObserveConnected(ControllerKind.Xbox);
        H.Check("USB loss does not convert remembered Xbox to the reader's disconnected Sony default",
            capture.ResolveKind(ControllerKind.DualSenseEdge) == ControllerKind.Xbox);
        H.Check("owned Xbox target can remain neutral during a transient transport gap",
            capture.RetainNeutralXboxTarget(false, true, true, true));
        H.Check("grace cannot preserve a target after its deadline", !capture.RetainNeutralXboxTarget(false, false, true, true));
        H.Check("grace cannot bypass cloak checks for a connected replacement", !capture.RetainNeutralXboxTarget(true, true, true, true));
        H.Check("grace cannot create a new target with no source", !capture.RetainNeutralXboxTarget(false, true, false, true));
        H.Check("external cloak removal prevents grace from claiming isolation", !capture.RetainNeutralXboxTarget(false, true, true, false));
        capture.ObserveConnected(ControllerKind.Xbox); // Same family reconnects through Bluetooth.
        H.Check("Bluetooth reconnect retains Xbox family", capture.ResolveKind(ControllerKind.PlayStationOther) == ControllerKind.Xbox);
        capture.ObserveConnected(ControllerKind.PlayStationOther);
        H.Check("a real Sony replacement supersedes remembered Xbox", capture.ResolveKind(ControllerKind.Xbox) == ControllerKind.PlayStationOther);
        H.Check("a Sony replacement cannot inherit Xbox grace", !capture.RetainNeutralXboxTarget(false, true, true, true));
        capture.ObserveConnected(ControllerKind.ExtraButtonPad);
        H.Check("extra-button family also survives a disconnected default", capture.ResolveKind(ControllerKind.DualSenseEdge) == ControllerKind.ExtraButtonPad);

        var hold = new ControllerCaptureContinuity();
        H.Check("identity hold needs a remembered Xbox family", !hold.HoldNeutralForIdentity(true, true, true, 0));
        hold.ObserveConnected(ControllerKind.Xbox);
        H.Check("connected source before its devnode enumerates keeps the target neutral", hold.HoldNeutralForIdentity(true, true, true, 0));
        H.Check("identity hold is bounded", !hold.HoldNeutralForIdentity(true, true, true, ControllerCaptureContinuity.IdentityHoldMs));
        H.Check("identity hold never creates a target", !hold.HoldNeutralForIdentity(true, false, true, 0));
        H.Check("identity hold needs the owned cloak verified", !hold.HoldNeutralForIdentity(true, true, false, 0));
        H.Check("a disconnected source is the grace rule's, not the hold's", !hold.HoldNeutralForIdentity(false, true, true, 0));
        hold.ObserveConnected(ControllerKind.PlayStationOther);
        H.Check("a Sony replacement gets no identity hold", !hold.HoldNeutralForIdentity(true, true, true, 0));

        const ControllerCaptureContinuity.MultiPadHold None = ControllerCaptureContinuity.MultiPadHold.None,
            Forward = ControllerCaptureContinuity.MultiPadHold.Forward, Neutral = ControllerCaptureContinuity.MultiPadHold.Neutral;
        long confirm = ControllerCaptureContinuity.MultiPadConfirmMs;
        H.Check("a second listing over an established capture holds it, forwarding the unchanged source",
            ControllerCaptureContinuity.HoldForMultiPad(true, true, 0, true, true) == Forward);
        H.Check("a source that reconnected during the listing is held neutral (it may be the new, uncloaked transport)",
            ControllerCaptureContinuity.HoldForMultiPad(true, true, 1000, true, false) == Neutral);
        H.Check("a disconnected source is held neutral",
            ControllerCaptureContinuity.HoldForMultiPad(true, true, 1000, false, true) == Neutral);
        H.Check("the multi-pad hold ends at its bound so a real second pad still trips the guard",
            ControllerCaptureContinuity.HoldForMultiPad(true, true, confirm, true, true) == None);
        H.Check("no established capture, no hold (two pads at launch trip the guard at once)",
            ControllerCaptureContinuity.HoldForMultiPad(false, true, 0, true, true) == None);
        H.Check("an owned cloak that no longer verifies gets no hold",
            ControllerCaptureContinuity.HoldForMultiPad(true, false, 0, true, true) == None);    }

    private static void UpdateVeto()
    {
        var type = H.AppType("UpdateService");
        var gate = type.GetField("CanExitForUpdate", BindingFlags.Public | BindingFlags.Static)!;
        var exit = type.GetField("ExitForUpdate", BindingFlags.Public | BindingFlags.Static)!;
        var oldGate = gate.GetValue(null);
        var oldExit = exit.GetValue(null);
        bool consulted = false, exited = false;
        try
        {
            gate.SetValue(null, new Func<bool>(() => { consulted = true; return false; }));
            exit.SetValue(null, new Action(() => exited = true));
            var result = (bool)type.GetMethod("LaunchInstallerAndExit")!.Invoke(null, new object[] { "" })!;
            H.Check("unsaved-edit gate is consulted before installer launch", consulted);
            H.Check("veto reports no installation and does not exit", !result && !exited);
        }
        finally { gate.SetValue(null, oldGate); exit.SetValue(null, oldExit); }
    }

    private static void OnboardingSaveFailure()
    {
        using var loader = new ConfigLoader(new QueuedContext());
        H.Check("onboarding save test creates a durable baseline", loader.WriteConfig(loader.Current));
        var baseline = File.ReadAllText(ConfigLoader.ConfigPath);
        var windowType = H.AppType("OnboardingWindow");
        var hooksType = windowType.GetNestedType("Hooks");
        var constructor = hooksType.GetConstructors().Single();
        var args = constructor.GetParameters().Select(p => p.Name switch
        {
            "GetSystem" => (object)new Func<SystemConfig>(() => loader.Current.System),
            "WriteSystem" => new Func<SystemConfig, bool>(system => loader.WriteConfig(new AppConfig
            {
                WheelA = loader.Current.WheelA, WheelB = loader.Current.WheelB, System = system,
                ActionColors = loader.Current.ActionColors, ActionIcons = loader.Current.ActionIcons,
                CustomColors = loader.Current.CustomColors,
            })),
            _ => null,
        }).ToArray();
        // Exercise the actual choice-commit method without starting the wizard, scans, or driver probes.
        var window = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(windowType);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        windowType.GetField("_hooks", flags).SetValue(window, constructor.Invoke(args));
        windowType.GetField("_pKind", flags).SetValue(window, ControllerKind.Xbox);
        var save = windowType.GetMethod("SavePracticeChord", flags);
        using (var held = new FileStream(ConfigLoader.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            H.Check("onboarding reports failure while configuration cannot be replaced",
                !(bool)save.Invoke(window, new object[] { TriggerModes.BumpersHome }));
            H.Check("failed onboarding save preserves the durable configuration",
                File.ReadAllText(ConfigLoader.ConfigPath) == baseline);
        }
        H.Check("onboarding choice can be retried after releasing the file lock",
            (bool)save.Invoke(window, new object[] { TriggerModes.BumpersHome }));
        H.Check("successful onboarding retry stores the selected chord",
            loader.Current.System.TriggerModes[ControllerKind.Xbox.ToString()].SequenceEqual(new[] { TriggerModes.BumpersHome }));
    }

    private static void CorruptPreservationFailure()
    {
        const string original = "{ unreadable but irreplaceable owner configuration";
        File.WriteAllText(ConfigLoader.ConfigPath, original);
        bool deny = true;
        using var loader = new ConfigLoader(new QueuedContext(), text =>
            deny ? throw new IOException("injected backup failure") : text);
        H.Check("failed corrupt-file backup prevents an overwriting save", !loader.WriteConfig(AppConfig.Default));
        H.Check("unreadable original survives backup and save failures", File.ReadAllText(ConfigLoader.ConfigPath) == original);
        deny = false;
        H.Check("save can retry once the original can be preserved", loader.WriteConfig(AppConfig.Default));
        H.Check("successful retry retained the unreadable original in a backup", Directory.GetFiles(AppPaths.AppDataDir, "config-corrupt-*.json")
            .Any(path => File.ReadAllText(path) == original));
    }

    private static void GestureOrdering()
    {
        var type = H.AppType("TriggerInterpreter");
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var callbackType = type.GetNestedType("Callbacks", BindingFlags.NonPublic);
        var callbacks = Activator.CreateInstance(callbackType, new object[]
        {
            new Func<bool>(() => true), new Func<bool>(() => false), new Func<bool>(() => false),
            new Func<bool>(() => false), new Action<bool>(_ => { }), new Action(() => { }),
            new Action(() => { }), new Action(() => { }),
        });
        using var reader = new ControllerReader(); // No Start: this test never opens hardware.
        var interpreter = Activator.CreateInstance(type, flags, null,
            new object[] { reader, System.Windows.Threading.Dispatcher.CurrentDispatcher, callbacks }, null);
        type.GetMethod("Configure", flags).Invoke(interpreter, new object[] { new[] { TriggerModes.BumpersHome }, true, false });
        H.Check("UI-thread configuration is visible before another event can run",
            ((IEnumerable<string>)type.GetField("_modes", flags).GetValue(interpreter)).Contains(TriggerModes.BumpersHome));
        type.GetField("_l1", flags).SetValue(interpreter, true);
        type.GetField("_gestureActive", flags).SetValue(interpreter, true);
        type.GetMethod("ResetGesture", flags).Invoke(interpreter, null);
        H.Check("UI-thread reset immediately clears held buttons and active gesture",
            !(bool)type.GetField("_l1", flags).GetValue(interpreter)
            && !(bool)type.GetProperty("GestureHeld", flags).GetValue(interpreter));
    }

    private static void AnalogBacklog()
    {
        var queue = new Queue<Action>();
        var consumed = new List<int>();
        int selected = -1, fired = -1;
        var input = new CoalescedInput<int>(queue.Enqueue, sample => { selected = sample; consumed.Add(sample); });
        for (int i = 0; i < 10000; i++) input.Submit(i);
        H.Check("ten thousand analog packets queue one UI operation", queue.Count == 1);
        input.Seal();
        queue.Enqueue(() => fired = selected); // A release after the first analog segment.
        for (int i = 10000; i < 20000; i++) input.Submit(i);
        H.Check("button boundary bounds each analog segment independently", queue.Count == 3);
        while (queue.TryDequeue(out var action)) action();
        H.Check("release uses the last sample before release, not a later one", fired == 9999);
        H.Check("latest state after release is still delivered", selected == 19999 && consumed.SequenceEqual(new[] { 9999, 19999 }));
        input.Submit(1);
        queue.Dequeue()();
        input.Submit(2);
        queue.Dequeue()();
        H.Check("normal-rate input is delivered without extra delay", consumed.TakeLast(2).SequenceEqual(new[] { 1, 2 }));
    }

    private sealed class QueuedContext : SynchronizationContext
    {
        public readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object State)> Work = new();
        public override void Post(SendOrPostCallback callback, object state) => Work.Enqueue((callback, state));
        public void Drain() { while (Work.TryDequeue(out var item)) item.Callback(item.State); }
    }

    private static void WatcherShutdown()
    {
        var context = new QueuedContext();
        var loader = new ConfigLoader(context);
        loader.StartWatching();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var watcher = typeof(ConfigLoader).GetField("_watcher", flags).GetValue(loader);
        loader.StartWatching();
        H.Check("starting the watcher twice does not replace or leak its instance", ReferenceEquals(watcher, typeof(ConfigLoader).GetField("_watcher", flags).GetValue(loader)));
        int notifications = 0;
        loader.Reloaded += _ => notifications++;
        H.Check("write commits before queued notification", loader.WriteConfig(loader.Current));
        string committed = File.ReadAllText(ConfigLoader.ConfigPath);
        loader.Dispose();
        context.Drain();
        H.Check("queued notifications do not run after disposal", notifications == 0);
        H.Check("closed loader rejects later writes without changing disk", !loader.WriteConfig(AppConfig.Default)
            && File.ReadAllText(ConfigLoader.ConfigPath) == committed);
        typeof(ConfigLoader).GetMethod("ScheduleReload", flags).Invoke(loader,
            new object[] { new object(), new FileSystemEventArgs(WatcherChangeTypes.Changed, AppPaths.AppDataDir, "config.json") });
        H.Check("late watcher event after disposal is harmless", typeof(ConfigLoader).GetField("_debounce", flags).GetValue(loader) is null);
    }

    private static void XInputIdentity()
    {
        // This compares acquisition behavior with the former FindPhysical loop. It verifies selection
        // compatibility at the slot-policy layer, not the identity or behavior of real hardware/drivers.
        for (int mask = 0; mask < 16; mask++)
        {
            int Legacy(int? exclude)
            {
                for (int slot = 0; slot < 4; slot++)
                    if (slot != exclude && (mask & (1 << slot)) != 0) return slot;
                return -1;
            }
            H.Check($"legacy acquisition order retained for mask {mask:X}",
                XInputSourceSelection.Select(mask, null, false) == Legacy(null));
            for (int own = 0; own < 4; own++)
            {
                if ((mask & (1 << own)) == 0) continue;
                H.Check($"known-own exclusion preserves other slots: mask {mask:X}, own {own}",
                    XInputSourceSelection.Select(mask, own, true) == Legacy(own));
            }
            H.Check($"unresolved own output cannot be selected: mask {mask:X}",
                XInputSourceSelection.Select(mask, null, true) == -1);
        }
        H.Check("several readable controllers keep the original first-slot behavior",
            XInputSourceSelection.Select(15, null, false) == 0);
        H.Check("a remapping tool's output is not rejected based on source type",
            XInputSourceSelection.Select(4, null, false) == 2);
        H.Check("a missing previously resolved own slot requires revalidation",
            XInputSourceSelection.Select(1, 1, true) == -1);
        H.Check("only our known output remaining means no source, not ambiguity",
            XInputSourceSelection.Select(2, 1, true) == -1 &&
            !XInputSourceSelection.IsOwnOutputAmbiguous(2, 1, true));
        H.Check("an unknown own output among readable slots reports ambiguity",
            XInputSourceSelection.IsOwnOutputAmbiguous(3, null, true));
        H.Check("a missing own slot among readable replacements reports ambiguity",
            XInputSourceSelection.IsOwnOutputAmbiguous(4, 1, true));
        H.Check("no readable slots do not report selection ambiguity",
            !XInputSourceSelection.IsOwnOutputAmbiguous(0, null, true));
        H.Check("multiple slots without our output do not report ambiguity",
            !XInputSourceSelection.IsOwnOutputAmbiguous(15, null, false));
        var stable = new XInputSlotObservation(1);
        stable.Observe(1); stable.Observe(3); stable.Observe(3);
        H.Check("single stable arrival is retained as an inference", stable.Resolve() == 1);
        var multiSource = new XInputSlotObservation(7);
        multiSource.Observe(15);
        H.Check("pre-existing multiple sources do not prevent observing our new output", multiSource.Resolve() == 3);
        var late = new XInputSlotObservation(1);
        late.Observe(3); late.Observe(7);
        H.Check("late second arrival invalidates early candidate", late.Resolve() is null);
        var reused = new XInputSlotObservation(1);
        reused.Observe(2); reused.Observe(3);
        H.Check("source slot loss/reuse invalidates the window", reused.Resolve() is null);
        var vanished = new XInputSlotObservation(1);
        vanished.Observe(3); vanished.Observe(1); vanished.Observe(3);
        H.Check("transient arrival does not resolve ownership", vanished.Resolve() is null);
    }

    private static void DriverArchitecture()
    {
        var type = H.AppType("DriverSetup");
        foreach (var architecture in Enum.GetValues<System.Runtime.InteropServices.Architecture>())
        {
            bool supported = (bool)H.InvokeStatic(type, "SupportsArchitecture", architecture);
            H.Check($"bundled driver architecture {architecture}", supported == (architecture == System.Runtime.InteropServices.Architecture.X64));
        }
        H.Check("unknown bundled filename cannot bypass integrity validation",
            !(bool)H.InvokeStatic(type, "VerifyBundledHash", "unknown.exe", Path.Combine(AppPaths.AppDataDir, "unknown.exe")));
    }

    private static void StagedCleanup()
    {
        string root = Path.Combine(Path.GetTempPath(), "radiata-stage-cleanup-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source"), stage = Path.Combine(root, "stage");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(stage);
        try
        {
            foreach (string file in new[] { "helper.dll", "modified.dll" })
            {
                File.WriteAllText(Path.Combine(source, file), "shipped payload");
                File.WriteAllText(Path.Combine(stage, file), "shipped payload");
            }
            File.WriteAllText(Path.Combine(stage, "modified.dll"), "owner's changed file");
            File.WriteAllText(Path.Combine(stage, "notes.txt"), "owner's notes");
            OwnedPayloadCleanup.RemoveMatchingFiles(stage, source, new[] { "helper.dll", "modified.dll", "absent.dll" });
            H.Check("matching staged helper is removed", !File.Exists(Path.Combine(stage, "helper.dll")));
            H.Check("modified stage file is preserved", File.ReadAllText(Path.Combine(stage, "modified.dll")) == "owner's changed file");
            H.Check("unknown stage file is preserved", File.ReadAllText(Path.Combine(stage, "notes.txt")) == "owner's notes");
            H.Check("original helper is preserved", File.Exists(Path.Combine(source, "helper.dll")));
            File.WriteAllText(Path.Combine(stage, "helper.dll"), "shipped payload");
            bool refused = false;
            try { OwnedPayloadCleanup.RemoveMatchingFiles(stage, source, new[] { "helper.dll", "..\\outside.txt" }); }
            catch (IOException) { refused = true; }
            H.Check("escaping plan fails before deleting any files", refused && File.Exists(Path.Combine(stage, "helper.dll")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RecoveryOwnership()
    {
        var type = H.AppType("RecoveryTask");
        const string sid = "S-1-5-21-1-2-3-1001";
        const string otherSid = "S-1-5-21-1-2-3-1002";
        const string exe = @"C:\Users\Owner\Radiata & Games\Radiata.exe";
        string Xml(string path, string owner) => (string)H.InvokeStatic(type, "TaskXml", path, owner, owner);
        string Name(string path, string owner) => (string)H.InvokeStatic(type, "ScopedName", path, owner);
        bool Owns(string xml, string path = exe, string owner = sid) => (bool)H.InvokeStatic(type, "OwnsXml", xml, path, owner);
        var xml = Xml(exe, sid);
        H.Check("task identity survives path casing", Name(exe, sid) == Name(exe.ToLowerInvariant(), sid));
        H.Check("different account has independent task", Name(exe, sid) != Name(exe, otherSid));
        H.Check("different installation has independent task", Name(exe, sid) != Name(@"D:\Radiata\Radiata.exe", sid));
        H.Check("generated task has verified ownership", Owns(xml));
        H.Check("other user's task is preserved", !Owns(Xml(exe, otherSid)));
        H.Check("other installation task is preserved", !Owns(Xml(@"D:\Radiata\Radiata.exe", sid)));
        H.Check("unexpected arguments prevent task takeover", !Owns(xml.Replace("--logon", "--uninstall")));
        H.Check("extra action prevents task takeover", !Owns(xml.Replace("</Actions>", "<Exec><Command>cmd.exe</Command></Exec></Actions>")));
        H.Check("extra trigger prevents task takeover", !Owns(xml.Replace("</Triggers>", "<BootTrigger /></Triggers>")));
        H.Check("malformed task cannot prove ownership", !Owns("<Task>"));
        H.Check("task trigger uses the originating account SID", xml.Split("<UserId>" + sid + "</UserId>").Length == 3);
    }

    private static void UnknownAncestry()
    {
        var verdict = H.InvokeStatic(H.AppType("PnpAncestry"), "Classify", @"HID\RADIATA-NONEXISTENT-REGRESSION\" + Guid.NewGuid().ToString("N"));
        H.Check("missing devnode is unknown rather than proven physical", !(bool)verdict.GetType().GetProperty("Known").GetValue(verdict));
        H.Check("unknown ancestry exposes a diagnostic cause", ((string)verdict.GetType().GetProperty("Detail").GetValue(verdict)).Contains("unreadable"));
    }

    private static void NavigationRepeat()
    {
        var repeat = new StickNavigationRepeat();
        H.Check("new direction steps immediately", repeat.SetStick(1, 0, 1000) == (1, 0));
        H.Check("no extra packet is required before initial repeat", repeat.Tick(1349) is null && repeat.Tick(1350) == (1, 0));
        H.Check("held stick repeats with no changed controller packets", repeat.Tick(1470) == (1, 0));
        H.Check("identical high-rate reports do not accelerate repeat", repeat.SetStick(1, 0, 1471) is null && repeat.Tick(1471) is null);
        H.Check("direction change steps immediately", repeat.SetStick(0, -1, 1500) == (0, -1));
        H.Check("centering cancels repeat", repeat.SetStick(0, 0, 1510) is null && repeat.Tick(10000) is null);
        repeat.SetStick(1, 0, 2000);
        repeat.Reset();
        H.Check("closing the grid cancels held direction", !repeat.Engaged && repeat.Tick(10000) is null);
        H.Check("nonfinite input cannot engage navigation", repeat.SetStick(float.NaN, 1, 10000) is null && !repeat.Engaged);
    }

    private static void MusicBackoff()
    {
        var type = H.AppType("SfxEngine");
        const string uri = "synthetic-test-track";
        bool Begin(long now) => (bool)H.InvokeStatic(type, "TryBeginMusicAttempt", uri, now);
        try
        {
            H.Check("initial music start is allowed", Begin(1000));
            H.InvokeStatic(type, "CompleteMusicAttempt", uri, false, 1500L);
            H.Check("600 rapid frame retries cause no new music starts", Enumerable.Range(0, 600).All(i => !Begin(1500 + i * 8)));
            H.Check("music becomes retryable after five seconds", Begin(6500));
            H.InvokeStatic(type, "CompleteMusicAttempt", uri, true, 6600L);
            H.Check("successful playback permits an immediate deliberate restart", Begin(6601));
        }
        finally { H.InvokeStatic(type, "CompleteMusicAttempt", uri, true, 7000L); }
    }

    private static void DiscordDeadline()
    {
        var type = H.AppType("DiscordIpc");
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var busy = type.GetField("_busy", flags);
        var tokenScope = (AsyncLocal<CancellationToken>)type.GetField("OperationToken", flags).GetValue(null);
        var cancelled = new System.Threading.Tasks.TaskCompletionSource(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new System.Threading.Tasks.TaskCompletionSource(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        Func<System.Threading.Tasks.Task> operation = async () =>
        {
            try { await System.Threading.Tasks.Task.Delay(Timeout.Infinite, tokenScope.Value).ConfigureAwait(false); }
            catch (OperationCanceledException) { cancelled.SetResult(); }
            await cleanup.Task.ConfigureAwait(false);
        };
        busy.SetValue(null, 1);
        var running = (System.Threading.Tasks.Task)H.InvokeStatic(type, "RunGuardedOperationAsync", operation, TimeSpan.FromMilliseconds(100));
        try
        {
            H.Check("operation receives its timeout cancellation", cancelled.Task.Wait(3000));
            H.Check("timeout does not release the guard before cleanup", !running.IsCompleted && (int)busy.GetValue(null) == 1);
        }
        finally { cleanup.TrySetResult(); }
        H.Check("finished cleanup releases the guard", running.Wait(3000) && (int)busy.GetValue(null) == 0);
    }

    private static void AudioPolicy()
    {
        var type = H.AppType("AudioDeviceSwitcher");
        var calls = new List<NAudio.CoreAudioApi.Role>();
        var apply = H.StaticMethod(type, "ApplyDefaultRoles");
        var defaults = Enum.GetValues<NAudio.CoreAudioApi.Role>().ToDictionary(role => role, role => "old-" + role);
        Func<NAudio.CoreAudioApi.Role, string> get = role => defaults[role];
        Func<string, NAudio.CoreAudioApi.Role, int> success = (id, role) => { calls.Add(role); defaults[role] = id; return 0; };
        apply.Invoke(null, new object[] { "synthetic-endpoint", success, get });
        H.Check("audio switch checks all three roles", calls.SequenceEqual(new[] { NAudio.CoreAudioApi.Role.Console, NAudio.CoreAudioApi.Role.Multimedia, NAudio.CoreAudioApi.Role.Communications }));
        bool rejected = false;
        foreach (var role in defaults.Keys.ToArray()) defaults[role] = "old-" + role;
        Func<string, NAudio.CoreAudioApi.Role, int> denied = (id, role) =>
        {
            if (role == NAudio.CoreAudioApi.Role.Multimedia) return unchecked((int)0x80070005);
            defaults[role] = id;
            return 0;
        };
        try { apply.Invoke(null, new object[] { "synthetic-endpoint", denied, get }); }
        catch (TargetInvocationException ex) when (ex.InnerException is UnauthorizedAccessException) { rejected = true; }
        H.Check("a failing audio HRESULT cannot be reported as success", rejected);
        H.Check("partial switch restores every changed role", defaults.All(pair => pair.Value == "old-" + pair.Key));
        Func<string, NAudio.CoreAudioApi.Role, int> competing = (id, role) =>
        {
            if (role == NAudio.CoreAudioApi.Role.Multimedia)
            {
                defaults[NAudio.CoreAudioApi.Role.Console] = "external-choice";
                return unchecked((int)0x80070005);
            }
            defaults[role] = id;
            return 0;
        };
        try { apply.Invoke(null, new object[] { "synthetic-endpoint", competing, get }); }
        catch (TargetInvocationException ex) when (ex.InnerException is UnauthorizedAccessException) { }
        H.Check("rollback preserves an external tool's later audio choice", defaults[NAudio.CoreAudioApi.Role.Console] == "external-choice");
    }

    private static void KeyboardPlans()
    {
        var type = H.AppType("KeypressSender");
        foreach (var invalid in new[] { "Crtl+S", "Ctrl++S", "+S", "Ctrl+", "Alt+Mystery+F4" })
            H.Check("invalid shortcut refuses " + invalid, !(bool)H.InvokeStatic(type, "CanParse", invalid));
        var layout = (IntPtr)H.InvokeStatic(type, "TargetLayout");
        (bool Ok, Array Inputs, Array Releases) Build(string keys, params ushort[] held)
        {
            var args = new object[] { keys, layout, new Func<ushort, bool>(held.Contains), null, null };
            bool ok = (bool)H.StaticMethod(type, "TryBuildCombo").Invoke(null, args);
            return (ok, (Array)args[3], (Array)args[4]);
        }
        var ordinary = Build("Ctrl+S");
        H.Check("secure attention sequence is not advertised as injectable", !Build("Ctrl+Alt+Del").Ok);
        foreach (var expected in new[] { (0u, 0), (1u, 1), (2u, 2), (3u, 1), (4u, 0) })
        {
            var release = (Array)H.InvokeStatic(type, "ReleasesForPrefix", ordinary.Inputs, expected.Item1);
            H.Check($"partial shortcut prefix {expected.Item1} releases only outstanding introduced keys", release.Length == expected.Item2);
        }
        H.Check("ordinary shortcut has one modifier down/up and one key down/up", ordinary.Ok && ordinary.Inputs.Length == 4 && ordinary.Releases.Length == 2);
        var duplicate = Build("Ctrl+Control+S");
        H.Check("modifier aliases never produce duplicate presses", duplicate.Ok && duplicate.Inputs.Length == 4);
        var borrowed = Build("Ctrl+S", 0xA3);
        H.Check("held right Ctrl is borrowed without pressing or releasing either Ctrl", borrowed.Ok && borrowed.Inputs.Length == 2 && borrowed.Releases.Length == 1);
        var conflicting = Build("Ctrl+S", 0xA0);
        H.Check("held Shift cannot turn Ctrl+S into Ctrl+Shift+S", !conflicting.Ok && conflicting.Inputs.Length == 0);
        H.Check("an already held main key is not released by the shortcut", !Build("Ctrl+S", 0x53).Ok);
        H.Check("slash resolves on the target keyboard layout", Build("/").Ok);
        H.Check("backtick resolves on the target keyboard layout", Build("`").Ok);
        var nav = Build("Left");
        var input = nav.Inputs.GetValue(0);
        var data = input.GetType().GetField("Data").GetValue(input);
        var keyboard = data.GetType().GetField("Keyboard").GetValue(data);
        uint flags = (uint)keyboard.GetType().GetField("dwFlags").GetValue(keyboard);
        H.Check("navigation scan input is extended and wVk is explicitly unused", (flags & 9) == 9
            && (ushort)keyboard.GetType().GetField("wVk").GetValue(keyboard) == 0);
    }

    private static void SecretBackups()
    {
        var secret = H.AppType("LocalSecret");
        var field = H.AppType("SecretField");
        var protect = H.StaticMethod(secret, "Protect").CreateDelegate<Func<string, string>>();
        var unprotect = H.StaticMethod(secret, "Unprotect").CreateDelegate<Func<string, string>>();
        var protectedValue = H.StaticMethod(secret, "IsProtected").CreateDelegate<Func<string, bool>>();
        var sealBackup = H.StaticMethod(field, "ProtectBackup").CreateDelegate<Func<string, string>>();
        var openBackup = H.StaticMethod(field, "ReadBackup").CreateDelegate<Func<string, string>>();
        const string key = "synthetic-secret-for-regression-only";
        var blob = protect(key);
        H.Check("new secrets have an explicit DPAPI marker", blob.StartsWith("dpapi:v1:") && protectedValue(blob));
        H.Check("same-user secret decryption succeeds", unprotect(blob) == key);
        var legacyBlob = blob.Substring("dpapi:v1:".Length);
        H.Check("old unmarked DPAPI secrets remain readable", protectedValue(legacyBlob) && unprotect(legacyBlob) == key);
        var broken = "dpapi:v1:not-a-valid-blob";
        H.Check("unreadable protected key is never used as an API key", H.InvokeStatic(field, "ToPlaintext", broken) is null);
        H.Check("an unrelated settings save preserves an unreadable stored key", (string)H.InvokeStatic(field, "ToStoredStable", null, broken) == broken);
        H.Check("entering a replacement replaces the unreadable key", unprotect((string)H.InvokeStatic(field, "ToStoredStable", key, broken)) == key);
        H.Check("legacy plaintext remains available for one-time migration", (string)H.InvokeStatic(field, "ToPlaintext", key) == key);
        var secretFile = Path.Combine(AppPaths.AppDataDir, "bad-secret.dat");
        File.WriteAllText(secretFile, broken);
        bool rejected = false;
        try { H.InvokeStatic(secret, "Read", secretFile); }
        catch (TargetInvocationException ex) when (ex.InnerException is System.Security.Cryptography.CryptographicException) { rejected = true; }
        H.Check("unreadable encrypted file never becomes legacy plaintext", rejected && File.ReadAllText(secretFile) == broken);

        var backupDir = Path.Combine(AppPaths.AppDataDir, "backups");
        var previous = Directory.Exists(backupDir) ? Directory.GetFiles(backupDir).ToHashSet() : new HashSet<string>();
        File.Delete(ConfigLoader.ConfigPath + ".bak");
        File.WriteAllText(ConfigLoader.ConfigPath, "{\"savedByVersion\":\"secret-test\",\"wheelA\":[{\"label\":\"protected-backup\"}],\"system\":{\"steamGridDbKey\":\"" + key + "\"},\"unknownFutureField\":17}");
        using (var loader = new ConfigLoader(new SynchronizationContext(), sealBackup, openBackup))
        {
            var backups = Directory.GetFiles(backupDir).Where(p => !previous.Contains(p)).ToArray();
            H.Check("version change creates a protected backup before key migration", backups.Length == 1 && !File.ReadAllText(backups[0]).Contains(key));
            H.Check("protected backup preserves unknown JSON fields and legacy key", backups.Length == 1 && openBackup(File.ReadAllText(backups[0])).Contains("unknownFutureField")
                && openBackup(File.ReadAllText(backups[0])).Contains(key));
            H.Check("last-known-good copy contains no plaintext key", !File.ReadAllText(ConfigLoader.ConfigPath + ".bak").Contains(key));
        }
        File.WriteAllText(ConfigLoader.ConfigPath, "broken json containing " + key);
        using (var recovered = new ConfigLoader(new SynchronizationContext(), sealBackup, openBackup))
            H.Check("encrypted last-known-good copy actually recovers settings", recovered.Current.WheelA[0].Label == "protected-backup");
        var corruptCopies = Directory.GetFiles(AppPaths.AppDataDir, "config-corrupt-*.json");
        H.Check("even malformed-config backups are protected without losing original text", corruptCopies.Length > 0 && corruptCopies.All(p => !File.ReadAllText(p).Contains(key))
            && corruptCopies.Any(p => openBackup(File.ReadAllText(p)) == "broken json containing " + key));
        File.Delete(secretFile);
    }

    private static void LiteralUninstall()
    {
        var root = Path.Combine(AppPaths.AppDataDir, "uninstall %PATH% ' ` $() [owner]");
        Directory.CreateDirectory(Path.Combine(root, "drivers", "user-data"));
        foreach (var relative in PortableUninstall.Files) File.WriteAllText(Path.Combine(root, relative), "test payload");
        var keep = Path.Combine(root, "drivers", "user-data", "keep.txt");
        File.WriteAllText(keep, "owner data");
        var neighboring = root + "-neighbor";
        Directory.CreateDirectory(neighboring);
        File.WriteAllText(Path.Combine(neighboring, "Radiata.exe"), "neighbor data");
        var helper = Path.Combine(root, "ArcadeHost");
        Directory.CreateDirectory(helper);
        File.WriteAllText(Path.Combine(helper, "runtime.dll"), "runtime payload");
        File.WriteAllText(Path.Combine(helper, "modified.dll"), "owner replacement");
        File.WriteAllText(Path.Combine(helper, "notes.txt"), "owner notes");
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("runtime payload")));
        File.WriteAllText(Path.Combine(root, "Radiata.helper-files.txt"), hash + "|ArcadeHost\\runtime.dll\n" + hash + "|ArcadeHost\\modified.dll\n");
        var script = PortableUninstall.Script(root, int.MaxValue, 0);
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        using var worker = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + encoded,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
        });
        bool exited = worker.WaitForExit(15000);
        H.Check("literal-path uninstall worker exits successfully", exited && worker.ExitCode == 0,
            exited ? worker.StandardError.ReadToEnd() : "worker did not finish");
        H.Check("all named test payload files are deleted", PortableUninstall.Files.All(f => !File.Exists(Path.Combine(root, f))));
        H.Check("unrelated nested drivers data survives", File.ReadAllText(keep) == "owner data");
        H.Check("neighbor directory is untouched", File.ReadAllText(Path.Combine(neighboring, "Radiata.exe")) == "neighbor data");
        H.Check("hashed helper runtime is removed", !File.Exists(Path.Combine(helper, "runtime.dll")));
        H.Check("modified helper and user notes survive", File.ReadAllText(Path.Combine(helper, "modified.dll")) == "owner replacement"
            && File.ReadAllText(Path.Combine(helper, "notes.txt")) == "owner notes");
        File.WriteAllText(Path.Combine(root, "Radiata.helper-files.txt"), hash + "|ArcadeHost\\..\\..\\outside.txt\n");
        bool manifestRefused = false;
        try { PortableUninstall.Script(root, int.MaxValue, 0); }
        catch (IOException) { manifestRefused = true; }
        H.Check("escaping helper manifest refuses the deletion plan", manifestRefused);
        File.WriteAllText(Path.Combine(root, "Radiata.helper-files.txt"), string.Join("\n",
            Enumerable.Range(0, 2000).Select(i => hash + "|ArcadeHost\\file-" + i + ".dll")));
        string largeManifestScript = PortableUninstall.Script(root, int.MaxValue, 0);
        H.Check("large runtime manifest does not inflate the worker command beyond Windows limits",
            Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(largeManifestScript)).Length < 30000);
        bool refused = false;
        try { PortableUninstall.Script(Path.GetPathRoot(root), 0, 0); }
        catch (ArgumentException) { refused = true; }
        H.Check("worker generator refuses a drive root", refused);
    }

    private sealed class FakeHide : IHidHideService
    {
        public bool IsInstalled { get; set; } = true;
        public bool IsActive { get; set; }
        public bool IsAppListInverted { get; set; }
        public readonly List<string> Apps = new();
        public readonly List<string> Blocks = new();
        public IReadOnlyList<string> ApplicationPaths => Apps.ToArray();
        public IReadOnlyList<string> BlockedInstanceIds => Blocks.ToArray();
        public string FailAdd, FailRemove;
        public int Adds, Removes;
        public void AddApplicationPath(string path) => Apps.Add(path);
        public void RemoveApplicationPath(string path) => Apps.Remove(path);
        public void AddBlockedInstanceId(string id)
        {
            Adds++;
            if (id == FailAdd) throw new IOException("injected add failure");
            Blocks.Add(id);
        }
        public void RemoveBlockedInstanceId(string id)
        {
            Removes++;
            if (id == FailRemove) throw new IOException("injected remove failure");
            Blocks.Remove(id);
        }
    }

    internal static void CloakRecovery()
    {
        var record = Path.Combine(AppPaths.AppDataDir, "cloaked-ids.txt");
        const string foreign = @"HID\VID_045E&PID_0B12\OTHER-MODEL";
        const string orphan = @"HID\{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0df2\ABSENT-BT";
        const string ours = @"HID\VID_054C&PID_0DF2\OURS";
        const string second = @"HID\VID_054C&PID_0DF2\SECOND";
        File.Delete(record);
        var driver = new FakeHide();
        driver.Blocks.AddRange(new[] { foreign, orphan, ours });
        using (var manager = new HidHideManager(driver))
        {
            H.Check("capture adopts requested and other-transport same-model orphan blocks", manager.Hide(new[] { ours }));
            H.Check("same-model USB and absent Bluetooth blocks enter durable recovery", File.ReadAllLines(record).Contains(ours) && File.ReadAllLines(record).Contains(orphan));
            H.Check("adoption does not add a driver block", driver.Adds == 0);
            H.Check("unrelated model does not enter recovery ownership", !File.ReadAllLines(record).Contains(foreign));
            H.Check("live configuration is initially verified", manager.VerifyCloak(new[] { ours }));
            driver.IsActive = false;
            H.Check("external filter disable invalidates cached capture", !manager.VerifyCloak(new[] { ours }));
            driver.IsActive = true;
            driver.IsAppListInverted = true;
            H.Check("inverse mode invalidates capture", !manager.VerifyCloak(new[] { ours }));
            H.Check("Hide refuses to change another tool's inverse mode", !manager.Hide(new[] { second }) && driver.IsAppListInverted && !driver.Blocks.Contains(second));
            driver.IsAppListInverted = false;
            H.Check("unhide refuses ownership of explicitly supplied foreign ids", manager.Unhide(new[] { foreign }));
            H.Check("foreign block and global enable remain intact", driver.Blocks.SequenceEqual(new[] { foreign }) && driver.IsActive);
        }
        driver = new FakeHide { FailAdd = second };
        using (var manager = new HidHideManager(driver))
        {
            H.Check("partial driver write reports failure", !manager.Hide(new[] { ours, second }));
            H.Check("partial driver write retains all recovery intent", File.ReadAllLines(record).Contains(ours) && File.ReadAllLines(record).Contains(second));
            driver.Blocks.Add(second);
            driver.FailRemove = second;
            H.Check("partial unhide reports incomplete recovery", !manager.Unhide());
            H.Check("successful unhide is reconciled without losing failed id", !driver.Blocks.Contains(ours) && driver.Blocks.Contains(second)
                && File.ReadAllLines(record).SequenceEqual(new[] { second }));
            driver.FailRemove = null;
            H.Check("retry recovers remaining id", manager.Unhide() && driver.Blocks.Count == 0 && !File.Exists(record));
        }
        File.WriteAllText(record, foreign + Environment.NewLine);
        driver = new FakeHide();
        using (var manager = new HidHideManager(driver))
        using (var held = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            H.Check("failed recovery-record replacement prevents new blocks", !manager.Hide(new[] { ours }) && driver.Adds == 0 && driver.Blocks.Count == 0);
            H.Check("failed intent write preserves existing recovery bytes", File.ReadAllText(record).Trim() == foreign);
        }
        // Exercise prior-session-only records, where the new manager has no in-memory ownership.
        File.WriteAllLines(record, new[] { ours, second });
        driver = new FakeHide { FailRemove = second };
        driver.Blocks.AddRange(new[] { ours, second });
        using (var manager = new HidHideManager(driver))
        {
            H.Check("startup partial recovery reports failure", !manager.Unhide(new[] { ours, second }));
            H.Check("startup recovery retains failed prior-session id", File.ReadAllLines(record).SequenceEqual(new[] { second }));
            driver.FailRemove = null;
            H.Check("startup recovery can retry persisted-only ownership", manager.Unhide(new[] { second }));
        }
        File.Delete(record);
        driver = new FakeHide { FailRemove = orphan };
        driver.Blocks.AddRange(new[] { foreign, orphan, @"HID\UNPARSEABLE-ORPHAN" });
        using (var manager = new HidHideManager(driver))
        {
            H.Check("uninstall reports failure when an unrecorded absent-device block cannot be removed", !manager.UnhideForUninstall());
            H.Check("uninstall persists the remaining orphan for retry without a connected controller",
                File.ReadAllLines(record).SequenceEqual(new[] { orphan }) && driver.Blocks.SequenceEqual(new[] { orphan }));
            driver.FailRemove = null;
            H.Check("uninstall retry clears every model and unparseable block", manager.UnhideForUninstall()
                && driver.Blocks.Count == 0 && !File.Exists(record));
            H.Check("empty uninstall cleanup is idempotent", manager.UnhideForUninstall());
        }
        File.WriteAllText(record, ours + Environment.NewLine);
        driver = new FakeHide();
        driver.Blocks.Add(orphan);
        using (var manager = new HidHideManager(driver))
        using (var held = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            H.Check("uninstall cannot erase orphan recovery intent when the record is locked",
                !manager.UnhideForUninstall() && driver.Removes == 0 && driver.Blocks.Contains(orphan));
        }
        File.Delete(record);
    }

    private sealed class RejectedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state) => throw new InvalidOperationException("dispatcher unavailable");
    }

    private static void NotificationFailure()
    {
        using var loader = new ConfigLoader(new RejectedContext());
        var changed = new AppConfig { WheelA = new[] { new WheelSlice { Label = "committed-before-notification" } } };
        H.Check("notification failure does not report a failed durable write", loader.WriteConfig(changed));
        H.Check("notification failure retains committed Current", ReferenceEquals(changed, loader.Current));
        H.Check("notification failure leaves actual committed bytes", File.ReadAllText(ConfigLoader.ConfigPath).Contains("committed-before-notification"));
        H.Check("notification failure is not a write error", loader.LastWriteError is null);
    }

    private delegate void ParseReport(ReadOnlySpan<byte> bytes);
    private delegate bool CheckCrc(ReadOnlySpan<byte> bytes, int logicalLength);
    private delegate uint MakeCrc(ReadOnlySpan<byte> bytes, byte seed);

    private static void ControllerReports()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var reader = new ControllerReader();
        var parse = typeof(ControllerReader).GetMethod("ParseUsb", flags).CreateDelegate<ParseReport>(reader);
        var crc = H.StaticMethod(typeof(ControllerReader), "Crc32WithSeed").CreateDelegate<MakeCrc>();
        var validate = H.StaticMethod(typeof(ControllerReader), "ValidateBtCrc").CreateDelegate<CheckCrc>();
        var usb = new byte[64];
        usb[0] = 1;
        usb[1] = usb[2] = usb[3] = usb[4] = 128;
        usb[8] = 0x28; // Cross held, neutral d-pad.
        usb[10] = 0x10; // Left Fn.
        usb[33] = usb[37] = 0x80; // No touch contacts.
        byte[] calibration = null;
        reader.CalibrationReport += bytes => calibration = bytes;
        parse(usb);
        H.Check("USB calibration is exactly 64 bytes", calibration is { Length: 64 } && calibration[10] == 0x10);
        var bt = new byte[78];
        usb.CopyTo(bt, 1);
        bt[0] = 0x31;
        BinaryPrimitives.WriteUInt32LittleEndian(bt.AsSpan(74), crc(bt.AsSpan(0, 74), 0xA1));
        H.Check("complete Bluetooth report CRC validates", validate(bt, 78));
        H.Check("truncated Bluetooth report is rejected", !validate(bt.AsSpan(0, 77), 78));
        var padded = new byte[547];
        bt.CopyTo(padded, 0);
        H.Check("Windows padding does not move the logical CRC", validate(padded, 78));
        bt[20] ^= 1;
        H.Check("corrupted Bluetooth payload is rejected", !validate(bt, 78));
        parse(padded.AsSpan(1));
        H.Check("Bluetooth calibration uses USB-aligned Fn offsets", calibration is { Length: 64 } && calibration[10] == usb[10]);
        int replayed = 0;
        reader.ReplayLatestState(s => { replayed++; H.Check("state cached before any emulator subscribes", s.Cross && !s.DpadUp); });
        H.Check("cached input is replayed once", replayed == 1);
        int published = 0;
        reader.StateChanged += s => { published++; H.Check("disconnect immediately publishes neutral", !s.Cross && s.LeftTrigger == 0 && s.LeftStickX == 0); };
        typeof(ControllerReader).GetMethod("ResetInputState", flags).Invoke(reader, null);
        H.Check("disconnect publishes one neutral frame", published == 1);
        reader.ReplayLatestState(_ => replayed++);
        H.Check("disconnect invalidates cached held buttons", replayed == 1);
        parse(usb.AsSpan(0, 12));
        reader.ReplayLatestState(_ => replayed++);
        H.Check("truncated USB cannot resurrect stale input", replayed == 1 && published == 1);

        var offsets = new HidOffsets { FnLeftByte = -1, FnRightMask = 3, DPadByte = 100 }.Sanitized();
        H.Check("invalid calibration falls back to safe Edge defaults", offsets.FnLeftByte == 10 && offsets.FnRightMask == 0x20 && offsets.DPadByte == 8);
        var valid = new HidOffsets { FnLeftByte = 11, FnLeftMask = 0x40 }.Sanitized();
        H.Check("valid custom calibration is retained", valid.FnLeftByte == 11 && valid.FnLeftMask == 0x40);

        var diagnostic = new DiagnosticWindow(reader);
        try
        {
            var apply = typeof(DiagnosticWindow).GetMethod("ApplyReport", flags);
            foreach (int length in new[] { 64, 78, 547, 11, 0 }) apply.Invoke(diagnostic, new object[] { new byte[length] });
            H.Check("diagnostics accepts transport changes and empty packets", ((byte[])typeof(DiagnosticWindow).GetField("_baseline", flags).GetValue(diagnostic)).Length == 11);
        }
        finally { diagnostic.Close(); }
        using var emulator = new GamepadEmulator(); // No Start: never creates a virtual device.
        emulator.Submit(new ControllerState { Cross = true });
        H.Check("emulator remembers input while no target exists", (bool)typeof(GamepadEmulator).GetField("_hasLastState", flags).GetValue(emulator)
            && ((ControllerState)typeof(GamepadEmulator).GetField("_lastState", flags).GetValue(emulator)).Cross);
    }

    private static void ConfigPersistence()
    {
        H.Check("test data is isolated", AppPaths.AppDataDir == (string)AppContext.GetData("Radiata.TestDataDirectory"));
        using var loader = new ConfigLoader(new SynchronizationContext());
        var before = loader.Current;
        var original = File.ReadAllText(ConfigLoader.ConfigPath);
        var changed = new AppConfig { WheelA = new[] { new WheelSlice { Label = "write-regression" } } };
        using (var held = new FileStream(ConfigLoader.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            H.Check("locked destination reports write failure", !loader.WriteConfig(changed));
            H.Check("failed write retains in-memory config", ReferenceEquals(before, loader.Current));
            H.Check("failed write exposes an error", !string.IsNullOrWhiteSpace(loader.LastWriteError));
            H.Check("failed write preserves original bytes", File.ReadAllText(ConfigLoader.ConfigPath) == original);
        }
        H.Check("retry succeeds after lock is released", loader.WriteConfig(changed));
        H.Check("successful retry clears error", loader.LastWriteError is null);
        using var observed = new ManualResetEventSlim();
        loader.Reloaded += cfg => { if (cfg.WheelA.Length == 1 && cfg.WheelA[0].Label == "rename-regression") observed.Set(); };
        loader.StartWatching();
        var replacement = ConfigLoader.ConfigPath + ".external-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(replacement, "{\"wheelA\":[{\"label\":\"rename-regression\"}],\"wheelB\":[],\"system\":{}}");
        File.Move(replacement, ConfigLoader.ConfigPath, true);
        H.Check("atomic replacement reloads the actual new config", observed.Wait(5000));
    }

    private static void ImageBounds()
    {
        foreach (var size in new[] { (100, 1000), (1000, 100), (10, 10), (1, 1000) })
        {
            var path = Path.Combine(AppPaths.AppDataDir, $"image-{size.Item1}-{size.Item2}.png");
            var pixels = new byte[size.Item1 * size.Item2 * 4];
            var source = BitmapSource.Create(size.Item1, size.Item2, 96, 96, PixelFormats.Bgra32, null, pixels, size.Item1 * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = File.Create(path)) encoder.Save(stream);
            var decoded = H.InvokeStatic(H.AppType("GameArt"), "LoadFrozen", path) as BitmapSource;
            H.Check($"{size.Item1}x{size.Item2} decodes within source and 600px bounds", decoded is not null
                && decoded.PixelWidth <= Math.Min(600, size.Item1)
                && decoded.PixelHeight <= Math.Min(600, size.Item2),
                decoded is null ? "null" : $"{decoded.PixelWidth}x{decoded.PixelHeight}");
        }
    }

    private static void RestoreRollback()
    {
        var root = Path.Combine(AppPaths.AppDataDir, "restore-test");
        Directory.CreateDirectory(root);
        var first = Path.Combine(root, "first.txt");
        var second = Path.Combine(root, "second.txt");
        var created = Path.Combine(root, "created.txt");
        File.WriteAllText(first, "original-first");
        File.WriteAllText(second, "original-second");
        bool refused = false;
        using (var locked = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try
            {
                using var restore = new FileRestoreTransaction(root);
                restore.WriteText(first, "replacement-first");
                restore.WriteText(created, "new-file");
                H.Check("transaction applied the first replacement before failure", File.ReadAllText(first) == "replacement-first");
                H.Check("transaction created the new asset before failure", File.Exists(created));
                restore.WriteText(second, "replacement-second");
                restore.Commit();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { refused = true; }
        }
        H.Check("locked later asset fails the restore", refused);
        H.Check("earlier asset is rolled back", File.ReadAllText(first) == "original-first");
        H.Check("failed asset retains its bytes", File.ReadAllText(second) == "original-second");
        H.Check("newly created asset is removed on rollback", !File.Exists(created));
        using (var restore = new FileRestoreTransaction(root))
        {
            restore.WriteText(first, "committed");
            restore.Commit();
        }
        H.Check("successful commit retains replacement", File.ReadAllText(first) == "committed");
        using (var restore = new FileRestoreTransaction(root))
        {
            bool escaped = false;
            try { restore.WriteText(Path.Combine(root, "..", "outside.txt"), "bad"); }
            catch (IOException) { escaped = true; }
            H.Check("restore refuses a path outside its root", escaped && !File.Exists(Path.Combine(AppPaths.AppDataDir, "outside.txt")));
        }
    }

    private static void RestoreCovers()
    {
        var art = H.AppType("GameArt");
        var cache = (string)art.GetProperty("CacheDirectory", System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
        Directory.CreateDirectory(cache);
        var expected = Path.Combine(cache, "restored-cover.png");
        File.WriteAllBytes(expected, new byte[] { 1 });
        const string oldPath = @"C:\another-profile\artcache\restored-cover.png";
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            modern = new { Path = oldPath, Favorite = true, FutureField = "preserve-me" },
            legacy = oldPath,
        });
        var result = (string)H.InvokeStatic(H.AppType("SettingsWindow"), "RepointRestoredCovers", json, null);
        using var parsed = System.Text.Json.JsonDocument.Parse(result);
        H.Check("modern cover selection points into restored cache", parsed.RootElement.GetProperty("modern").GetProperty("Path").GetString() == expected);
        H.Check("legacy cover selection points into restored cache", parsed.RootElement.GetProperty("legacy").GetString() == expected);
        H.Check("cover rebase preserves other and unknown fields", parsed.RootElement.GetProperty("modern").GetProperty("Favorite").GetBoolean()
            && parsed.RootElement.GetProperty("modern").GetProperty("FutureField").GetString() == "preserve-me");
    }

    private static void ArcadePreservation()
    {
        var store = typeof(AppConfig).Assembly.GetType("ControllerWheel.ArcadeStore");
        H.Check("ArcadeStore type exists", store is not null);
        if (store is null) return;
        var path = Path.Combine(AppPaths.AppDataDir, "arcade-state.json");
        foreach (var content in new[] { "{\"V\":999,\"Games\":{\"future\":{\"State\":\"keep-me\"}}}", "{corrupt-but-recoverable" })
        {
            File.WriteAllText(path, content);
            H.InvokeStatic(store, "DropCacheForHarness");
            H.InvokeStatic(store, "SaveWindowPosition", 1);
            H.InvokeStatic(store, "SaveMusicOn", false);
            H.Check("unsupported/unreadable source survives attempted saves", File.ReadAllText(path) == content);
            H.Check("persistence-disabled status is observable", !(bool)store.GetProperty("CanSave").GetValue(null));
        }
        File.Delete(path);
        H.InvokeStatic(store, "DropCacheForHarness");
        H.InvokeStatic(store, "SaveWindowPosition", 2);
        H.InvokeStatic(store, "DropCacheForHarness");
        H.Check("fresh supported state still saves and reloads", (int)H.InvokeStatic(store, "LoadWindowPosition") == 2);
    }
}
