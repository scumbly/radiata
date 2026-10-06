using System;
using System.Collections.Generic;
using System.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Narration coordination (accessibility A1). The acceptance criteria require this covered
/// "independently of audio playback", so every check runs against a FAKE sink that records calls instead of
/// speaking — no SAPI voice is ever constructed here, which also means these run on a machine with no audio
/// endpoints at all (the condition that blocked the audio group on a disconnected session).
///
/// What's actually under test is <see cref="Announcer"/>'s contract, the part that can silently rot:
/// priority between kinds, selection coalescing, and — the one with real user impact — that speech queued
/// for a context which has since gone away never lands.</summary>
internal static class T_Narration
{
    /// <summary>Records what would have been spoken. Thread-safe: the debounce fires on a pool thread.</summary>
    private sealed class FakeSink : ISpeechSink
    {
        public readonly List<string> Spoken = new();
        public readonly List<bool> Interrupts = new();
        public int Stops;
        /// <summary>Stands in for a voice still talking. Set it to hold the sink "busy" for a test; a real
        /// sink flips it false on its own when the queue drains.</summary>
        public bool IsSpeaking { get; set; }
        public void Speak(string text, bool interrupt)
        {
            lock (Spoken) { Spoken.Add(text); Interrupts.Add(interrupt); }
        }
        public void Stop() { lock (Spoken) { Stops++; IsSpeaking = false; } }
        public bool[] AllInterrupts { get { lock (Spoken) return Interrupts.ToArray(); } }
        public string[] All { get { lock (Spoken) return Spoken.ToArray(); } }
        public int Count { get { lock (Spoken) return Spoken.Count; } }
        public string Last { get { lock (Spoken) return Spoken.Count == 0 ? null : Spoken[^1]; } }
        public void Clear() { lock (Spoken) { Spoken.Clear(); Interrupts.Clear(); Stops = 0; } }
    }

    // Debounce used for the tests: short enough to keep the run quick, long enough that a same-tick burst
    // is unambiguously inside one window on a loaded machine.
    private const int Debounce = 60;
    private static readonly int Settle = Debounce * 4;   // wait past the debounce before asserting

    private static (Announcer a, FakeSink s) New()
    {
        var sink = new FakeSink();
        var ann = new Announcer { Enabled = true, Sink = sink, SelectionDebounceMs = Debounce };
        return (ann, sink);
    }

    public static void Run()
    {
        H.Group("Narration — Announcer coordination (no audio, fake sink)");

        // ── the master switch ─────────────────────────────────────────────────────────────────────
        H.Try("disabled Announcer speaks nothing", () =>
        {
            var (ann, sink) = New();
            ann.Enabled = false;
            ann.Announce("context", AnnouncementKind.Context);
            ann.Announce("result", AnnouncementKind.Result);
            ann.Announce("selection", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            H.Check("narration off = silence", sink.Count == 0, $"{sink.Count} spoken");
            ann.Dispose();
        });

        H.Try("no sink wired is not a crash", () =>
        {
            var ann = new Announcer { Enabled = true, Sink = null };
            ann.Announce("x", AnnouncementKind.Selection);
            ann.Announce("y", AnnouncementKind.Result);
            Thread.Sleep(Settle);
            H.Pass("null sink tolerated");
            ann.Dispose();
        });

        // ── immediacy vs debounce ────────────────────────────────────────────────────────────────
        H.Try("results and context speak immediately", () =>
        {
            var (ann, sink) = New();
            ann.Announce("HDR, On", AnnouncementKind.Result);
            // No sleep: a Result must not wait on the selection debounce — a fired action's outcome
            // arriving 180 ms late reads as a hang.
            H.Check("Result is synchronous", sink.Count == 1, string.Join(" | ", sink.All));
            ann.Announce("Left wheel, 6 actions", AnnouncementKind.Context);
            H.Check("Context is synchronous", sink.Count == 2, string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("a single selection speaks after the debounce", () =>
        {
            var (ann, sink) = New();
            ann.Announce("Steam, 1 of 6", AnnouncementKind.Selection);
            H.Check("not spoken before the window elapses", sink.Count == 0, $"{sink.Count} spoken");
            Thread.Sleep(Settle);
            H.Check("spoken after the window", sink.Count == 1 && sink.Last == "Steam, 1 of 6",
                    string.Join(" | ", sink.All));
            ann.Dispose();
        });

        // ── coalescing: the check that keeps a stick sweep usable ────────────────────────────────
        H.Try("a rapid sweep collapses to the slice it lands on", () =>
        {
            var (ann, sink) = New();
            for (int i = 1; i <= 8; i++)
                ann.Announce($"Slice {i}, {i} of 8", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            H.Check("8 selections → 1 announcement", sink.Count == 1, $"{sink.Count}: {string.Join(" | ", sink.All)}");
            H.Check("it's the LAST one", sink.Last == "Slice 8, 8 of 8", sink.Last);
            ann.Dispose();
        });

        H.Try("selections separated by more than the window each speak", () =>
        {
            var (ann, sink) = New();
            ann.Announce("A", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            ann.Announce("B", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            H.Check("two settled selections → two announcements",
                    sink.Count == 2 && sink.All[0] == "A" && sink.All[1] == "B",
                    string.Join(" | ", sink.All));
            ann.Dispose();
        });

        // ── results must not cut each other off ───────────────────────────────────────────────────
        // The wave 2/3 hardware failures were all one defect: every result interrupted, so an instruction
        // ("Picked up Steam, aim to a new position") was killed mid-word by the focus change it caused.
        H.Try("a Result queues behind a Result that is still speaking", () =>
        {
            var (ann, sink) = New();
            sink.IsSpeaking = true;                       // the voice is mid-sentence
            ann.Announce("Picked up Steam. Aim to a new position", AnnouncementKind.Result);
            ann.Announce("Dropped at position 4", AnnouncementKind.Result);
            H.Check("both results reach the sink", sink.Count == 2, string.Join(" | ", sink.All));
            var flags = sink.AllInterrupts;
            H.Check("the second one does NOT interrupt", flags.Length == 2 && !flags[1],
                    $"interrupt flags: {string.Join(",", flags)}");
            ann.Dispose();
        });

        H.Try("a Context still cuts a Result off", () =>
        {
            var (ann, sink) = New();
            sink.IsSpeaking = true;
            ann.Announce("Steam deleted, 5 slices left", AnnouncementKind.Result);
            ann.Announce("Left wheel closed", AnnouncementKind.Context);
            var flags = sink.AllInterrupts;
            // The situation itself changed, so whatever is still being said describes a context that is
            // already gone — this is the one kind allowed to talk over a result.
            H.Check("Context interrupts", flags.Length == 2 && flags[1],
                    $"interrupt flags: {string.Join(",", flags)}");
            ann.Dispose();
        });

        H.Try("a selection waits for a speaking Result instead of cutting or vanishing", () =>
        {
            var (ann, sink) = New();
            sink.IsSpeaking = true;
            ann.Announce("Hold to delete Steam", AnnouncementKind.Result);
            ann.Announce("Position 3 of 6", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            H.Check("the selection has NOT talked over the result", sink.Count == 1,
                    string.Join(" | ", sink.All));
            sink.IsSpeaking = false;                      // the voice frees up
            Thread.Sleep(Settle);
            // Dropping it instead would be the "nothing announced while carrying a slice" failure: the one
            // moment a listener most needs the slot read out.
            H.Check("it speaks once the voice frees up",
                    sink.Count == 2 && sink.Last == "Position 3 of 6", string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("a held-back selection is still dropped by Reset", () =>
        {
            var (ann, sink) = New();
            sink.IsSpeaking = true;
            ann.Announce("Steam deleted, 5 slices left", AnnouncementKind.Result);
            ann.Announce("Position 3 of 6", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            ann.Reset();                                  // the wheel closed while the result played
            sink.IsSpeaking = false;
            Thread.Sleep(Settle);
            H.Check("the stale selection never lands", sink.Count == 1, string.Join(" | ", sink.All));
            ann.Dispose();
        });

        // ── priority ──────────────────────────────────────────────────────────────────────────────
        H.Try("a Result supersedes a pending selection", () =>
        {
            var (ann, sink) = New();
            ann.Announce("Shut down, 4 of 6", AnnouncementKind.Selection);
            ann.Announce("Ready, release to fire", AnnouncementKind.Result);
            Thread.Sleep(Settle);
            // The guarded-hold case: the slice was just named, and repeating it as preamble to the ready
            // cue would bury the cue.
            H.Check("only the Result is spoken",
                    sink.Count == 1 && sink.Last == "Ready, release to fire",
                    string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("a Context supersedes a pending selection", () =>
        {
            var (ann, sink) = New();
            ann.Announce("Steam, 2 of 6", AnnouncementKind.Selection);
            ann.Announce("Cancelled", AnnouncementKind.Context);
            Thread.Sleep(Settle);
            H.Check("only the Context is spoken",
                    sink.Count == 1 && sink.Last == "Cancelled", string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("a Result never blocks a LATER selection", () =>
        {
            var (ann, sink) = New();
            ann.Announce("HDR, On", AnnouncementKind.Result);
            ann.Announce("Steam, 2 of 6", AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            H.Check("both spoken, in order",
                    sink.Count == 2 && sink.All[0] == "HDR, On" && sink.All[1] == "Steam, 2 of 6",
                    string.Join(" | ", sink.All));
            ann.Dispose();
        });

        // ── stale speech: the acceptance criterion about reopening ────────────────────────────────
        H.Try("Reset drops a pending selection (no stale speech after close)", () =>
        {
            var (ann, sink) = New();
            ann.Announce("Steam, 2 of 6", AnnouncementKind.Selection);
            ann.Reset();                     // wheel closed mid-sweep
            Thread.Sleep(Settle);
            H.Check("nothing lands after the context is gone", sink.Count == 0,
                    string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("Reset(stopSpeech) also stops the sink", () =>
        {
            var (ann, sink) = New();
            ann.Announce("a long announcement", AnnouncementKind.Result);
            ann.Reset(stopSpeech: true);
            H.Check("sink.Stop called", sink.Stops == 1, $"stops={sink.Stops}");
            ann.Dispose();
        });

        H.Try("a selection queued BEFORE Reset can't speak into the next context", () =>
        {
            var (ann, sink) = New();
            ann.Announce("old wheel slice", AnnouncementKind.Selection);
            ann.Reset();
            ann.Announce("Right wheel, 3 actions", AnnouncementKind.Context);   // reopened
            Thread.Sleep(Settle);
            // The reopen bug this guards: the previous wheel's name arriving after the new wheel opened.
            H.Check("only the new context is spoken",
                    sink.Count == 1 && sink.Last == "Right wheel, 3 actions", string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("disabling mid-flight drops the pending selection", () =>
        {
            var (ann, sink) = New();
            ann.Announce("pending", AnnouncementKind.Selection);
            ann.Enabled = false;             // user unticked the setting during the window
            Thread.Sleep(Settle);
            H.Check("nothing spoken after the switch went off", sink.Count == 0,
                    string.Join(" | ", sink.All));
            ann.Dispose();
        });

        // ── input hygiene ─────────────────────────────────────────────────────────────────────────
        H.Try("blank announcements are ignored", () =>
        {
            var (ann, sink) = New();
            ann.Announce("", AnnouncementKind.Result);
            ann.Announce("   ", AnnouncementKind.Context);
            ann.Announce(null, AnnouncementKind.Selection);
            Thread.Sleep(Settle);
            H.Check("empty/whitespace/null spoken never", sink.Count == 0, string.Join(" | ", sink.All));
            ann.Dispose();
        });

        H.Try("Dispose is safe and repeatable", () =>
        {
            var (ann, _) = New();
            ann.Announce("x", AnnouncementKind.Selection);
            ann.Dispose();
            ann.Dispose();
            H.Pass("double Dispose tolerated");
        });

        // ── the semantic strings the app builds (formatting, not plumbing) ────────────────────────
        // These assert the SHAPE the checklist items quote, so a reworded announcement shows up as a
        // failing test rather than as a surprise in someone's ear.
        H.Try("slice announcement shape", () =>
        {
            H.Check("position is 1-based", Position(0, 6) == "1 of 6", Position(0, 6));
            H.Check("last slice reads correctly", Position(5, 6) == "6 of 6", Position(5, 6));
        });

        H.Try("scrub percentages round to the 5% step", () =>
        {
            // The D-pad steps 5% at a time, so a spoken number that isn't a multiple of 5 can never be
            // landed on exactly — see App.AnnounceScrub.
            H.Check("0.55 → 55", Pct(0.55f) == 55, Pct(0.55f).ToString());
            H.Check("0.53 → 55", Pct(0.53f) == 55, Pct(0.53f).ToString());
            H.Check("0.52 → 50", Pct(0.52f) == 50, Pct(0.52f).ToString());
            H.Check("clamps low", Pct(-1f) == 0, Pct(-1f).ToString());
            H.Check("clamps high", Pct(2f) == 100, Pct(2f).ToString());
        });

        H.Try("a toggle state is spoken as CURRENT, not the target", () =>
        {
            // A bare "Off" is ambiguous between what IS and what firing will do.
            H.Check("Off → Currently off", Clause("Off") == "Currently off", Clause("Off"));
            H.Check("On → Currently on", Clause("On") == "Currently on", Clause("On"));
            H.Check("Muted → Currently muted", Clause("Muted") == "Currently muted", Clause("Muted"));
            H.Check("Unmuted → Currently unmuted", Clause("Unmuted") == "Currently unmuted", Clause("Unmuted"));
            H.Check("Focus Assist profile keeps its words",
                    Clause("Priority Only") == "Currently priority Only", Clause("Priority Only"));
        });

        H.Try("a NO-FUNCTION condition never gets \"Currently\"", () =>
        {
            // These describe what's MISSING, not a state the slice is in, so
            // "Currently No Device" / "Currently OBS unavailable" is incoherent.
            foreach (var s in new[] { "No Device", "Unavailable", "Not Installed", "Failed", "OBS unavailable" })
                H.Check($"\"{s}\" speaks bare", !Clause(s).StartsWith("Currently"), Clause(s));
            H.Check("No Device is phrased usefully", Clause("No Device") == "no device available",
                    Clause("No Device"));
            // Naive lowering produced "oBS unavailable" — this is why LowerFirst checks the 2nd char.
            H.Check("an ACRONYM survives", Clause("OBS unavailable") == "OBS unavailable",
                    Clause("OBS unavailable"));
        });

        H.Try("exit-app's state is an OBJECT, so it gets a verb", () =>
        {
            // PreviewStatus returns the frontmost app's NAME here — "Currently Notepad" says nothing.
            H.Check("an app name becomes an action", Clause("Notepad", "exit-app") == "will close Notepad",
                    Clause("Notepad", "exit-app"));
            H.Check("the empty case reads naturally",
                    Clause("Nothing to exit", "exit-app") == "nothing to close",
                    Clause("Nothing to exit", "exit-app"));
        });
    }

    /// <summary>The wheel's UI Automation tree (accessibility A1). The peer is NOT how a blind user hears
    /// the wheel — Narrator can't see the overlay's never-focused window — it exists so the
    /// wheel's semantics are inspectable and assertable. This group is that assertion: it builds a real
    /// RadialMenuControl, gives it slices, and reads the tree back the way an inspection tool would.</summary>
    public static void RunPeer()
    {
        H.Group("Narration — RadialMenuControl UIA tree");

        H.Try("the wheel exposes a peer with one child per slice", () =>
        {
            var wheel = new ControllerWheel.RadialMenuControl
            {
                Slices = new[]
                {
                    new WheelSlice { Label = "Steam" },
                    // ShowLabel false + a logo is the artwork-only case: nothing is DRAWN, but the peer
                    // must still name it (the acceptance criterion about artwork-only slices).
                    new WheelSlice { Label = "Helldivers 2", ShowLabel = false, LogoPath = "x.png" },
                    new WheelSlice { Label = "Shut down",
                                     Action = new ActionConfig { Type = "system", Command = "shutdown",
                                                                 RequireConfirm = true } },
                },
            };

            // Invoke the control's own OnCreateAutomationPeer override rather than
            // UIElementAutomationPeer.CreatePeerForElement: the latter returns null unless a real UIA
            // client is attached to the process, so it can't tell "no override" from "nobody listening".
            // Reflection here proves the WIRING (a rename shows up as MISSING, not as a pass).
            var create = wheel.GetType().GetMethod("OnCreateAutomationPeer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (create is null) { H.Fail("RadialMenuControl overrides OnCreateAutomationPeer", "method missing"); return; }
            var peer = create.Invoke(wheel, null) as System.Windows.Automation.Peers.AutomationPeer;
            H.Check("the control returns its own peer", peer is ControllerWheel.RadialMenuPeer,
                    peer?.GetType().Name);
            if (peer is null) return;

            var kids = peer.GetChildren();
            H.Check("3 slices → 3 children", kids is { Count: 3 }, $"{kids?.Count}");
            if (kids is null || kids.Count != 3) return;

            H.Check("name comes from the resolved Label", kids[0].GetName() == "Steam", kids[0].GetName());
            H.Check("an ARTWORK-ONLY slice is still named",
                    kids[1].GetName() == "Helldivers 2", kids[1].GetName());
            H.Check("position is exposed", kids[0].GetItemStatus() == "1 of 3", kids[0].GetItemStatus());
            H.Check("a GUARDED slice says so",
                    kids[2].GetItemStatus() == "3 of 3, hold to confirm", kids[2].GetItemStatus());
            H.Check("children are ListItems",
                    kids[0].GetAutomationControlType() == System.Windows.Automation.Peers.AutomationControlType.ListItem,
                    kids[0].GetAutomationControlType().ToString());
            H.Check("the wheel names which side it is",
                    peer.GetName() == "Left wheel", peer.GetName());

            wheel.IsWheelB = true;
            H.Check("…and follows IsWheelB", peer.GetName() == "Right wheel", peer.GetName());

            // Selection is read-only: a UIA client must not be able to fire a slice, because the product
            // has no such operation (a slice fires on trigger RELEASE while armed).
            var sel = kids[0].GetPattern(System.Windows.Automation.Peers.PatternInterface.SelectionItem)
                      as System.Windows.Automation.Provider.ISelectionItemProvider;
            H.Check("slices expose SelectionItem", sel is not null);
            if (sel is not null)
            {
                H.Check("nothing is armed at rest", !sel.IsSelected);
                bool refused = false;
                try { sel.Select(); } catch (InvalidOperationException) { refused = true; }
                H.Check("Select() is refused, not faked", refused);
            }

            // The tree must follow what's DRAWN: a peer that reported the configured slices while a picker
            // was on screen would actively mislead an inspection tool.
            wheel.BeginEdit(new[] { new WheelSlice { Label = "only one" } });
            H.Check("edit mode retargets the children to the working set",
                    peer.GetChildren() is { Count: 1 }, $"{peer.GetChildren()?.Count}");
            H.Check("edit mode is named", peer.GetName() == "Right wheel, edit mode", peer.GetName());
        });
    }

    // Mirrors of the app's formatting, kept tiny and deliberate: the app builds these inline inside
    // handlers that need an overlay + a controller, so the SHAPE is what's testable headlessly.
    private static string Position(int index, int count) => $"{index + 1} of {count}";
    private static int Pct(float level01) =>
        (int)Math.Round(Math.Clamp(level01, 0f, 1f) * 100 / 5.0) * 5;
    /// <summary>Drives the REAL classifier — App.StateClause — by reflection, so a reworded clause or a
    /// state word that slips back into the "Currently" branch fails here rather than in someone's ear.
    /// Private static on App, hence the reflection (see Harness's note on why that's deliberate).</summary>
    private static string Clause(string state, string actionType = "system")
    {
        var slice = new WheelSlice { Label = "x", Action = new ActionConfig { Type = actionType } };
        var m = H.StaticMethod(H.AppType("App"), "StateClause", 2);
        if (m is null) throw new MissingMethodException("App.StateClause(WheelSlice, string)");
        return (string)m.Invoke(null, new object[] { slice, state });
    }
}
