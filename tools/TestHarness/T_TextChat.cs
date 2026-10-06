using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The Text Chat action's four guards.
///
/// Driven through a STUB <see cref="IPlatformActions"/> rather than the real one. That's the point: the guards
/// are decisions about the foreground window, and a stub is the only way to hold the foreground still, change
/// it at an exact moment, and record every keystroke the executor *would* have sent — while nothing is typed
/// into any real window. The keystroke ORDER (chat key → text → Enter) and each refusal are therefore checked
/// exactly, which no manual test can do.
///
/// The remaining on-device half is unchanged: whether a given game's chat box actually opens for the key
/// <c>GameChatButtons</c> names. That needs a real game.</summary>
internal static class T_TextChat
{
    /// <summary>Records what the executor asked the platform to do, and lets a test move the foreground at a
    /// chosen moment. Every member either records or returns a benign default — nothing touches the machine.</summary>
    private sealed class StubPlatform : IPlatformActions
    {
        public readonly List<string> Sent = new();       // "keys:X" / "text:Y" in order
        public string GameName;                          // what DetectRunningGameName reports
        public long Token = 1;                           // the foreground token
        public Func<long> TokenHook;                     // lets a test change the token mid-sequence
        public bool SendKeysResult = true;
        public bool SendTextResult = true;
        public bool EnterResult = true;

        public bool SendKeys(string keys) { Sent.Add("keys:" + keys); return SendKeysResult && (keys != "Enter" || EnterResult); }
        public bool SendText(string text) { Sent.Add("text:" + text); return SendTextResult; }
        public string DetectRunningGameName() => GameName;
        public long ForegroundToken() => TokenHook is null ? Token : TokenHook();

        // Everything below is unreachable from the text-chat path; present only to satisfy the interface.
        public void LaunchOrFocus(string path, string processName = null) { }
        public void RunFile(string path) { }
        public ProcessToggleResult ToggleProcess(string process, string launchPath) => ProcessToggleResult.CloseRequested;
        public bool IsProcessRunning(string process, string exePath = null) => false;
        public void OpenUrl(string url) { }
        public bool LaunchStorefront(string key) => false;
        public string SwitchAudioDevice(string nameFilter, bool capture) => null;
        public string CloseFrontmostApp() => null;
        public string PeekFrontmostApp() => null;
        public bool? ToggleMute(bool capture) => null;
        public bool? GetMute(bool capture) => null;
        public void SetVolume(float level) { }
        public float? AdjustMicVolume(float delta) => null;
        public void MediaKey(string key) { }
        public void Sleep() { }
        public void Reboot(bool signBackIn = true) { }
        public void Lock() { }
        public void Shutdown() { }
        public void Logout() { }
        public void Hibernate() { }
        public void ToggleShowDesktop() { }
        public void SetDisplayMode(string mode) { }
        public string ToggleDisplayTopology() => null;
        public void EmptyRecycleBin() { }
        public string SetPowerPlan(string planGuid) => null;
        public string ActivePowerPlanGuid() => null;
        public bool? HdrIsEnabled() => null;
        public bool? HdrToggle() => null;
        public void JoinDiscordVoice(string url) { }
        public void ToggleDiscordVoiceSetting(bool deafen) { }
        public void LaunchDiscord() { }
    }

    private const string Secret = "ZZ-secret-message-body-QQ";

    public static void Run()
    {
        H.Group("Text Chat — the foreground guards, and the trace never carries the message");

        // ── Try Game Default with NO installed game in front → do nothing at all. ────────────────────
        {
            var p = new StubPlatform { GameName = null };
            var (sent, trace, status) = Fire(p, new ActionConfig { Type = "text-chat", Command = "default", ChatText = Secret });
            H.Check("default mode, no game in front: nothing is typed", sent.Count == 0,
                    sent.Count == 0 ? null : string.Join(", ", sent));
            H.Check("…and it says why", trace.Any(l => l.Contains("no installed game in the foreground")));
            // No game to NAME here, so this refusal stays narration-only — no hub readout.
            H.Check("…and no hub readout (nothing to name)", status is null, status?.Line2);
        }

        // ── A game that IS in the chat-key table → its key, then the text, then Enter. ────────────────
        {
            var known = FirstKnownGame();
            if (known is null) H.Skip("mapped-game keystroke order", "GameChatButtons table unreadable");
            else
            {
                var (name, key) = known.Value;
                var p = new StubPlatform { GameName = name };
                var (sent, trace, status) = Fire(p, new ActionConfig { Type = "text-chat", Command = "default", ChatText = Secret });
                H.Check($"mapped game \"{name}\" uses its table key \"{key}\" first",
                        sent.Count > 0 && sent[0] == "keys:" + key, string.Join(" → ", sent));
                H.Check("…then the message, then Enter",
                        sent.Count == 3 && sent[1] == "text:" + Secret && sent[2] == "keys:Enter",
                        string.Join(" → ", sent));
                H.Check("…and the trace names the game and key", trace.Any(l => l.Contains($"game='{name}'")));
                // A working send is silent in the hub — the readout is reserved for the refusal.
                H.Check("…and a successful send shows no hub readout", status is null, status?.Line2);
            }
        }

        // ── Anti-spam throttle: a second fire inside the cooldown sends NOTHING. ─────────────────────
        // A slice is one button press, so without this the action is a held-button chat flood. The check
        // must come before any input goes out, not merely before Enter.
        {
            var known = FirstKnownGame();
            if (known is null) H.Skip("chat throttle", "GameChatButtons table unreadable");
            else
            {
                var p = new StubPlatform { GameName = known.Value.Name };
                using var t = new H.TraceGrab();
                // ONE executor for both fires — the cooldown is per-instance state, so a fresh executor
                // per fire (what Fire() does) would defeat the very thing under test.
                var x = new ActionExecutor(p, () => { }, () => { });
                var slice = new WheelSlice
                {
                    Label = "chat",
                    Action = new ActionConfig { Type = "text-chat", Command = "default", ChatText = Secret },
                };
                x.Execute(slice);
                for (int i = 0; i < 40 && p.Sent.Count < 3; i++) Thread.Sleep(50);
                int afterFirst = p.Sent.Count;

                var second = x.Execute(slice);
                Thread.Sleep(200);
                AllTrace.AddRange(t.Lines);

                H.Check("the first fire sends", afterFirst == 3, string.Join(" → ", p.Sent));
                H.Check("a second fire inside the cooldown types NOTHING MORE",
                        p.Sent.Count == afterFirst, string.Join(" → ", p.Sent));
                H.Check("…and the trace says it was throttled",
                        t.Lines.Any(l => l.Contains("throttled")),
                        string.Join(" | ", t.Lines.Where(l => l.Contains("text-chat")).Take(4)));
                // Visible as well as spoken: silence would be indistinguishable from a dead input.
                H.Check("…and the hub says why", second is not null && second.Line2.Contains("Cooldown"),
                        second?.Line2 ?? "(no status)");
            }
        }

        // ── An installed game NOT in the table → refuse, don't guess Enter, and SAY SO in the hub. ───
        {
            const string Obscure = "Some Deliberately Obscure Indie Game 9471";
            var p = new StubPlatform { GameName = Obscure };
            var (sent, trace, status) = Fire(p, new ActionConfig { Type = "text-chat", Command = "default", ChatText = Secret });
            H.Check("unmapped game: nothing is typed, the key is unconfirmed", sent.Count == 0,
                    sent.Count == 0 ? null : string.Join(", ", sent));
            H.Check("…and it says why", trace.Any(l => l.Contains("unconfirmed")));
            H.Check("…and the hub readout NAMES the game", status?.Line1 == Obscure, status?.Line1);
            H.Check("…and explains the fix without opening Settings",
                    status?.Line2 == "No chat key default found. Configure in Settings", status?.Line2);
            H.Check("…and holds long enough to read (longer than a state word's linger)",
                    status?.LingerMs is int ms && ms >= 2000, status?.LingerMs?.ToString() ?? "(null)");
        }

        // ── A game KNOWN to have no text chat → same refusal + same readout (only the trace differs). ─
        {
            var known = FirstNoTextChatGame();
            if (known is null) H.Skip("known-chatless refusal", "GameChatButtons NoTextChat set unreadable");
            else
            {
                var p = new StubPlatform { GameName = known };
                var (sent, trace, status) = Fire(p, new ActionConfig { Type = "text-chat", Command = "default", ChatText = Secret });
                H.Check($"known-chatless game \"{known}\": nothing is typed", sent.Count == 0,
                        sent.Count == 0 ? null : string.Join(", ", sent));
                H.Check("…and the trace distinguishes it from merely-unconfirmed",
                        trace.Any(l => l.Contains("known to have no text chat")));
                H.Check("…and the user sees the SAME readout as the unconfirmed case",
                        status?.Line1 == known
                        && status?.Line2 == "No chat key default found. Configure in Settings",
                        $"{status?.Line1} / {status?.Line2}");
            }
        }

        // ── Custom mode doesn't care what's running, but still needs a readable foreground. ──────────
        {
            var p = new StubPlatform { GameName = null };
            var (sent, _, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "T", ChatText = Secret });
            H.Check("custom mode uses the configured key, no game needed",
                    sent.Count == 3 && sent[0] == "keys:T", string.Join(" → ", sent));
        }
        {
            var p = new StubPlatform { GameName = "Anything", Token = 0 };   // 0 = no readable foreground
            var (sent, trace, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "T", ChatText = Secret });
            H.Check("no readable foreground window: nothing is typed", sent.Count == 0, string.Join(", ", sent));
            H.Check("…and it says why", trace.Any(l => l.Contains("no readable foreground window")));
        }

        // ── Alt-tab DURING the chat-box delay → abort before typing. ─────────────────────────────────
        {
            int calls = 0;
            var p = new StubPlatform { GameName = "Anything" };
            p.TokenHook = () => ++calls == 1 ? 1 : 99;   // first read = the target, every later read = elsewhere
            var (sent, trace, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "T", ChatText = Secret });
            H.Check("foreground changed while the chat box opened: the message is NOT typed",
                    !sent.Any(s => s.StartsWith("text:")), string.Join(" → ", sent));
            H.Check("…and it aborts rather than guessing",
                    trace.Any(l => l.Contains("foreground changed while the chat box was opening")));
        }

        // ── Alt-tab AFTER the text but before Enter → don't post it. ─────────────────────────────────
        {
            int calls = 0;
            var p = new StubPlatform { GameName = "Anything" };
            p.TokenHook = () => ++calls <= 2 ? 1 : 99;   // target for the pre-open and post-delay reads, then moves
            var (sent, trace, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "T", ChatText = Secret });
            H.Check("foreground changed mid-message: Enter is NOT sent (nothing is posted)",
                    sent.Count(s => s == "keys:Enter") == 0, string.Join(" → ", sent));
            H.Check("…and it says why", trace.Any(l => l.Contains("foreground changed mid-message")));
        }

        // An opening failure must stop before typing or Enter.
        {
            var p = new StubPlatform { GameName = "Anything", SendKeysResult = false };
            var (sent, trace, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "NotAKey", ChatText = Secret });
            H.Check("opening-key failure stops before text and Enter", sent.SequenceEqual(new[] { "keys:NotAKey" }) && trace.Any(l => l.Contains("opening key failed")));
        }
        {
            var p = new StubPlatform { GameName = "Anything", SendTextResult = false };
            var (sent, trace, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "T", ChatText = Secret });
            H.Check("partial text delivery never posts Enter", !sent.Contains("keys:Enter") && trace.Any(l => l.Contains("text injection incomplete")));
        }
        {
            var p = new StubPlatform { GameName = "Anything", EnterResult = false };
            var (_, trace, _) = Fire(p, new ActionConfig { Type = "text-chat", Command = "custom", Keys = "T", ChatText = Secret });
            H.Check("failed Enter is reported instead of confirmed", trace.Any(l => l.Contains("Enter injection failed")));
        }

        // ── The privacy claim: the body never reaches the trace — only its length. ───────────────────
        H.Check("the message body NEVER appears in any traced line", !AllTrace.Any(l => l.Contains(Secret)),
                $"{AllTrace.Count} traced lines checked across every case above");
        H.Check("…and its LENGTH is what gets logged instead",
                AllTrace.Any(l => l.Contains($"sending {Secret.Length} chars")));
    }

    private static readonly List<string> AllTrace = new();

    /// <summary>Fire one text-chat action and wait for the async-void executor to finish. The chat-box delay is
    /// ~150 ms, so a short poll is enough; the wait is on the recorded output, not a fixed sleep.
    /// <para><c>status</c> is what Execute returned — non-null only for the "no chat key for this game"
    /// refusal, which is the one text-chat outcome that gets a hub readout.</para></summary>
    private static (List<string> sent, List<string> trace, ActionStatus status) Fire(
        StubPlatform p, ActionConfig action)
    {
        using var t = new H.TraceGrab();
        var x = new ActionExecutor(p, () => { }, () => { });
        var status = x.Execute(new WheelSlice { Label = "chat", Action = action });
        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(50);
            if (t.Lines.Any(l => l.Contains("not injecting") || l.Contains("aborting")
                                 || l.Contains("mid-message")) || p.Sent.Count >= 3) break;
        }
        Thread.Sleep(150);   // let a late line land before the listener is removed
        var trace = t.Lines.ToList();
        AllTrace.AddRange(trace);
        return (p.Sent, trace, status);
    }

    /// <summary>A (game, key) pair straight out of the shipped lookup table, so the test can't disagree with
    /// it.</summary>
    private static (string Name, string Key)? FirstKnownGame()
    {
        var t = H.Core.GetType("ControllerWheel.GameChatButtons");
        var field = t?.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
                                 | System.Reflection.BindingFlags.Public)
                     .FirstOrDefault(f => typeof(System.Collections.IDictionary).IsAssignableFrom(f.FieldType));
        if (field?.GetValue(null) is not System.Collections.IDictionary map) return null;
        foreach (System.Collections.DictionaryEntry e in map)
        {
            var name = e.Key?.ToString();
            var key  = e.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key)) return (name, key);
        }
        return null;
    }

    /// <summary>A name from the shipped known-no-text-chat set, so the test can't disagree with it. Picks one
    /// the REAL resolution order actually lands on: Lookup runs first, so a name that its relaxed containment
    /// match happens to catch would resolve to a key instead and never reach the chatless branch.</summary>
    private static string FirstNoTextChatGame()
    {
        var t = H.Core.GetType("ControllerWheel.GameChatButtons");
        var field = t?.GetField("NoTextChat", System.Reflection.BindingFlags.Static
                                              | System.Reflection.BindingFlags.NonPublic);
        if (field?.GetValue(null) is not System.Collections.IEnumerable set) return null;
        foreach (var item in set)
        {
            if (item?.ToString() is not { Length: > 0 } name) continue;
            if (GameChatButtons.Lookup(name) is null && GameChatButtons.HasNoTextChat(name)) return name;
        }
        return null;
    }
}
