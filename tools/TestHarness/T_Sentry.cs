using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>What may end an isolation claim, and what may earn one. SteamSentry runs on an injected
/// process probe and an injected bounce, so no process is queried, started or stopped; the Bluetooth
/// gate and the interface-path parser are pure.</summary>
internal static class T_Sentry
{
    public static void Run()
    {
        H.Group("Sentry clearance and the Bluetooth verdict gate — injected probes, no process or driver touched");
        H.Try("episode survives what leaves handles alive", EpisodeSurvivesDips);
        H.Try("Passthru suspends an episode without curing it", PassthruSuspends);
        H.Try("Steam restart cures Steam's handle only", SteamRestartCuresSteamOnly);
        H.Try("pad departure severs every handle", DepartureSeversAll);
        H.Try("a failed process probe is never memoised", ProbeFailureNotMemoised);
        H.Try("Bluetooth verdict gate", BtGate);
        H.Try("interface path to instance id", InterfacePaths);
    }

    // A probe answering from a mutable table; null = "the probe failed".
    private sealed class Probe
    {
        public readonly Dictionary<string, bool?> Answers = new(StringComparer.OrdinalIgnoreCase);
        public int Calls;
        public bool? Ask(string name) { Calls++; return Answers.TryGetValue(name, out var a) ? a : false; }
    }

    private static SteamSentry SteamFirst(Probe probe, Func<Task<bool>> bounce = null)
    {
        probe.Answers["steam"] = true;
        var s = new SteamSentry(probe.Ask, bounce, predatesMemoMs: 0);
        s.Evaluate(padPresent: true, cloakConfirmedWithPad: true, safeMode: false);
        return s;
    }

    private static void EpisodeSurvivesDips()
    {
        var probe = new Probe();
        var s = SteamFirst(probe);
        H.Check("Steam-first with the pad present at launch is Detected", s.Current == SteamSentry.State.Detected);

        s.Evaluate(true, cloakConfirmedWithPad: false, safeMode: false);
        H.Check("a cloak/virtual-pad dip (Bluetooth hold, disconnect grace) keeps the episode",
            s.Current == SteamSentry.State.Detected);

        probe.Answers["steam"] = null;
        s.Evaluate(true, true, false);
        H.Check("a failed Steam probe is not 'Steam gone'", s.Current == SteamSentry.State.Detected);

        probe.Answers["steam"] = true;
        s.Evaluate(true, true, false);
        H.Check("the episode is still live after the dips (no latch was taken)", s.Current == SteamSentry.State.Detected);
    }

    private static void PassthruSuspends()
    {
        var probe = new Probe();
        var s = SteamFirst(probe);
        s.Evaluate(true, false, safeMode: true);
        H.Check("Passthru stands the episode down (the card closes on any non-Detected state)",
            s.Current == SteamSentry.State.Idle);
        H.Check("Passthru is not reported as unverified isolation", !s.IsolationUnverified);
        s.Evaluate(true, true, safeMode: false);
        H.Check("leaving Passthru with Steam still predating detects again", s.Current == SteamSentry.State.Detected);
    }

    private static void SteamRestartCuresSteamOnly()
    {
        var probe = new Probe();
        probe.Answers["Discord"] = true;
        var s = SteamFirst(probe, bounce: () => Task.FromResult(true));
        s.Evaluate(true, true, false);   // the downgrade is computed before the pass's transition
        H.Check("during the Steam episode the tray's Steam state outranks 'unverified'", !s.IsolationUnverified);

        bool ok = s.RelaunchSteamAsync().GetAwaiter().GetResult();
        H.Check("a successful bounce ends Steam's episode", ok && s.Current == SteamSentry.State.Cleared);
        probe.Answers["steam"] = false;   // the relaunched Steam starts after the session
        s.Evaluate(true, true, false);
        H.Check("after the bounce, Discord (older than the session) keeps isolation unverified",
            s.IsolationUnverified && s.UnverifiedHolder == "Discord");

        probe.Answers["steam"] = true;
        s.Evaluate(true, true, false);
        H.Check("the cured episode does not re-arise this session", s.Current == SteamSentry.State.Cleared);

        var failed = SteamFirst(new Probe(), bounce: () => Task.FromResult(false));
        failed.RelaunchSteamAsync().GetAwaiter().GetResult();
        H.Check("a failed bounce returns to Detected", failed.Current == SteamSentry.State.Detected);

        var user = new SteamSentry(new Probe().Ask, () => Task.FromResult(false), predatesMemoMs: 0);
        user.RestartSteamForUserAsync().GetAwaiter().GetResult();
        H.Check("a failed user-opted restart fabricates no episode", user.Current == SteamSentry.State.Idle);

        var p2 = new Probe();
        p2.Answers["Discord"] = true;
        var ext = SteamFirst(p2);
        p2.Answers["steam"] = false;   // the user restarted Steam themselves
        ext.Evaluate(true, true, false);
        H.Check("an external Steam restart ends Steam's episode", ext.Current == SteamSentry.State.Cleared);
        ext.Evaluate(true, true, false);
        H.Check("...and leaves the generic downgrade to Discord", ext.IsolationUnverified && ext.UnverifiedHolder == "Discord");
    }

    private static void DepartureSeversAll()
    {
        var probe = new Probe();
        probe.Answers["Discord"] = true;
        var s = SteamFirst(probe);
        s.OnPadDeparted();
        H.Check("departure clears the Steam episode", s.Current == SteamSentry.State.Cleared);
        s.Evaluate(true, true, false);
        H.Check("departure severs Discord's handle too — isolation is no longer unverified", !s.IsolationUnverified);
        H.Check("the episode stays cleared on later passes", s.Current == SteamSentry.State.Cleared);

        var generic = new SteamSentry(new Probe().Ask, null, predatesMemoMs: 0);
        generic.Evaluate(true, true, false);
        H.Check("no Steam, no known holder, pad present at launch = generic 'unverified'",
            generic.IsolationUnverified && generic.UnverifiedHolder is null && generic.Current == SteamSentry.State.Idle);
        generic.OnPadDeparted();
        generic.Evaluate(true, true, false);
        H.Check("departure lifts the generic downgrade", !generic.IsolationUnverified);
    }

    private static void ProbeFailureNotMemoised()
    {
        var probe = new Probe();
        probe.Answers["steam"] = null;
        var s = new SteamSentry(probe.Ask, null, predatesMemoMs: 60_000);
        s.Evaluate(true, true, false);
        int afterFirst = probe.Calls;
        s.Evaluate(true, true, false);
        H.Check("a null answer is asked again on the next pass", probe.Calls > afterFirst);
        H.Check("an unknown answer detects nothing", s.Current == SteamSentry.State.Idle);

        probe.Answers["steam"] = true;
        s.Evaluate(true, true, false);
        int afterKnown = probe.Calls;
        s.Evaluate(true, true, false);
        H.Check("a known answer is memoised for Steam", probe.Calls - afterKnown < 2);
        H.Check("the known answer detects", s.Current == SteamSentry.State.Detected);
    }

    private static void BtGate()
    {
        static BtObservationGate.Outcome R(bool superseded = false, bool pending = true, bool changed = false,
                                           bool present = true, bool cloaked = true, int? count = 0) =>
            BtObservationGate.Resolve(superseded, pending, changed, present, cloaked, count);

        H.Check("present, cloaked, observed zero = Verified", R() == BtObservationGate.Outcome.Verified);
        H.Check("present, cloaked, pads still seen = Refused", R(count: 1) == BtObservationGate.Outcome.Refused);
        H.Check("probe unavailable = Refused (fail closed)", R(count: null) == BtObservationGate.Outcome.Refused);
        H.Check("pad absent during the probe: zero proves nothing = Reset",
            R(present: false) == BtObservationGate.Outcome.Reset);
        H.Check("pad absent with probe unavailable = Reset, not Refused",
            R(present: false, count: null) == BtObservationGate.Outcome.Reset);
        H.Check("ids no longer cloaked = Reset", R(cloaked: false) == BtObservationGate.Outcome.Reset);
        H.Check("id set changed mid-probe = Reset", R(changed: true) == BtObservationGate.Outcome.Reset);
        H.Check("late result after the deadline's Refused (no longer pending) = Discard",
            R(pending: false) == BtObservationGate.Outcome.Discard);
        H.Check("superseded by a newer arm or the deadline = Discard",
            R(superseded: true) == BtObservationGate.Outcome.Discard);
        H.Check("superseded outranks every other input",
            R(superseded: true, present: false, changed: true, count: null) == BtObservationGate.Outcome.Discard);
    }

    private static void InterfacePaths()
    {
        H.Check("USB HID link", HidInstanceId.FromInterfacePath(
                @"\\?\HID#VID_054C&PID_0DF2&MI_03#8&198b44e5&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")
            == @"HID\VID_054C&PID_0DF2&MI_03\8&198b44e5&0&0000");
        string bt = HidInstanceId.FromInterfacePath(
            @"\\?\HID#{00001812-0000-1000-8000-00805f9b34fb}&Dev&VID_045e&PID_0b13&REV_0509&ac8ebd1d1956&IG_00#a&10d52c90&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}");
        H.Check("Bluetooth Xbox IG_ link matches the devnode id case-insensitively", string.Equals(bt,
            @"HID\{00001812-0000-1000-8000-00805F9B34FB}&DEV&VID_045E&PID_0B13&REV_0509&AC8EBD1D1956&IG_00\A&10D52C90&0&0000",
            StringComparison.OrdinalIgnoreCase));
        H.Check("XUSB composite link", HidInstanceId.FromInterfacePath(
                @"\\?\USB#VID_045E&PID_0B12#3039373130353935333736343337#{ec87f1e3-c13b-4100-b5f7-8b84d54260cb}")
            == @"USB\VID_045E&PID_0B12\3039373130353935333736343337");
        H.Check("null / empty / shapeless input is null",
            HidInstanceId.FromInterfacePath(null) is null && HidInstanceId.FromInterfacePath("") is null
            && HidInstanceId.FromInterfacePath(@"\\?\nohashes") is null);
    }
}
