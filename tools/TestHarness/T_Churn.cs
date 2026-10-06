using System.Collections.Generic;
using System.Linq;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Core/PadCountChurnRule.cs — the pad-count churn breaker's rule: guard-ON edges inside a window latch a
/// hold, a second latch holds for the session, and only every pad gone releases that. Pure Core, no pad, no
/// timer: each scenario drives the rule with its own clock, so nothing here waits on the wall.
/// <para>The WPF method that wraps the rule (<c>App.PadCountChurnBreaker</c>) adds only trace lines, the transient
/// pad ids and the expiry timer; the live staging of this breaker needs a second pad that is present for about
/// six seconds and absent for about three, three times (docs/INPUT-CAPTURE.md), which hand plugging cannot hold.
/// This group is the check that does not depend on that.</para></summary>
internal static class T_Churn
{
    private const int PadsSteady = 1, PadsFlapped = 2;
    private const PadCountChurnRule.Verdict Held = PadCountChurnRule.Verdict.Held;
    private const PadCountChurnRule.Verdict None = PadCountChurnRule.Verdict.None;
    private const PadCountChurnRule.Verdict LatchedTimed = Held | PadCountChurnRule.Verdict.Latched;
    private const PadCountChurnRule.Verdict LatchedSession = LatchedTimed | PadCountChurnRule.Verdict.ForSession;

    /// <summary>One capture pass with the guard off (a single pad listed).</summary>
    private static PadCountChurnRule.Verdict Off(PadCountChurnRule r, long t) => r.Observe(t, false, PadsSteady);

    /// <summary>One capture pass with the guard on (a second pad listed).</summary>
    private static PadCountChurnRule.Verdict On(PadCountChurnRule r, long t) => r.Observe(t, true, PadsFlapped);

    /// <summary>A full flap: a guard-off pass, then the guard-ON pass that is the edge. Returns the edge's verdict.</summary>
    private static PadCountChurnRule.Verdict Edge(PadCountChurnRule r, long t)
    {
        Off(r, t);
        return On(r, t);
    }

    /// <summary>A rule that has just latched its first hold at t = 11 000 (edges at 1 000, 6 000, 11 000).</summary>
    private static PadCountChurnRule FirstLatch()
    {
        var r = new PadCountChurnRule();
        Edge(r, 1_000);
        Edge(r, 6_000);
        Edge(r, 11_000);
        return r;
    }

    /// <summary>A rule that has latched twice: a first hold, its expiry on a guard-off pass, then three more edges.</summary>
    private static PadCountChurnRule SessionLatch(out long latchedAtMs)
    {
        var r = FirstLatch();
        long expiry = r.HoldUntilMs;
        Off(r, expiry);
        Edge(r, expiry + 1_000);
        Edge(r, expiry + 6_000);
        latchedAtMs = expiry + 11_000;
        Edge(r, latchedAtMs);
        return r;
    }

    public static void Run()
    {
        H.Group("Pad-count churn rule (Core)");

        // ── the constants the docs and the trace line cite ───────────────────────────────────────────
        H.Check("The rule is three guard trips inside 20 s, holding 60 s",
                PadCountChurnRule.ChurnTrips == 3 && PadCountChurnRule.ChurnWindowMs == 20_000
                && PadCountChurnRule.ChurnHoldMs == 60_000);

        // ── what counts as a trip ────────────────────────────────────────────────────────────────────
        {
            var r = new PadCountChurnRule();
            var all = new List<PadCountChurnRule.Verdict>();
            for (int i = 0; i < 100; i++) all.Add(Off(r, i * 500L));
            H.Check("A guard that never turns on records nothing and never latches",
                    all.All(v => v == None) && r.PendingTrips == 0 && r.Latches == 0 && r.HoldUntilMs == 0);
        }
        {
            var r = new PadCountChurnRule();
            Off(r, 0);
            var scans = new[] { On(r, 1_000), On(r, 4_000), On(r, 7_000), On(r, 10_000) };
            H.Check("A guard that stays on is one trip, not one per scan",
                    scans.All(v => v == None) && r.PendingTrips == 1 && r.Latches == 0);
        }
        {
            var r = new PadCountChurnRule();
            var first = Edge(r, 1_000);
            var second = Edge(r, 6_000);
            H.Check("Two guard-ON edges inside the window do not latch",
                    first == None && second == None && r.PendingTrips == 2 && r.HoldUntilMs == 0 && r.Latches == 0);
        }

        // ── the first latch ──────────────────────────────────────────────────────────────────────────
        {
            var r = new PadCountChurnRule();
            Edge(r, 1_000);
            Edge(r, 6_000);
            var third = Edge(r, 11_000);
            H.Check("The third guard-ON edge inside 20 s latches a hold for 60 s",
                    third == LatchedTimed && r.HoldUntilMs == 11_000 + PadCountChurnRule.ChurnHoldMs && r.Latches == 1,
                    $"verdict {third}, hold until {r.HoldUntilMs}");
            H.Check("A latch clears the trips recorded toward it", r.PendingTrips == 0);
        }

        // ── the window (stamp aging) ─────────────────────────────────────────────────────────────────
        {
            var r = new PadCountChurnRule();
            Edge(r, 0);
            Edge(r, 10_000);
            var third = Edge(r, 20_000);
            H.Check("A trip exactly 20 s old still counts: the window is inclusive", third == LatchedTimed,
                    $"verdict {third}");
        }
        {
            var r = new PadCountChurnRule();
            Edge(r, 0);
            Edge(r, 10_000);
            var third = Edge(r, 20_001);
            H.Check("A trip 1 ms past the window ages out, so the third edge does not latch",
                    third == None && r.PendingTrips == 2 && r.Latches == 0,
                    $"verdict {third}, pending {r.PendingTrips}");
        }
        {
            var r = new PadCountChurnRule();
            Edge(r, 0);
            Edge(r, 5_000);
            var late = Edge(r, 30_000);
            H.Check("Two trips and then a gap over 20 s leave only the new edge pending",
                    late == None && r.PendingTrips == 1 && r.Latches == 0,
                    $"verdict {late}, pending {r.PendingTrips}");
            Edge(r, 31_000);
            var latch = Edge(r, 32_000);
            H.Check("The aged-out trips are gone for good: a fresh run of three inside the window latches",
                    latch == LatchedTimed, $"verdict {latch}");
        }

        // ── inside a hold ────────────────────────────────────────────────────────────────────────────
        {
            var r = FirstLatch();
            long until = r.HoldUntilMs;
            var fourth = Edge(r, 12_000);
            H.Check("A fourth edge inside the same window is ignored: the hold stays, no second latch",
                    fourth == Held && r.Latches == 1 && r.HoldUntilMs == until && r.PendingTrips == 0,
                    $"verdict {fourth}, latches {r.Latches}");
        }
        {
            var r = FirstLatch();
            long until = r.HoldUntilMs;
            var verdicts = new List<PadCountChurnRule.Verdict>();
            for (long t = 12_000; t < until; t += 3_000)
            {
                verdicts.Add(Off(r, t));
                verdicts.Add(On(r, t + 500));
            }
            H.Check("While held, every pass reports the hold and nothing else: no new latch, no trips recorded",
                    verdicts.Count > 20 && verdicts.All(v => v == Held) && r.Latches == 1
                    && r.PendingTrips == 0 && r.HoldUntilMs == until,
                    $"{verdicts.Count} passes");
        }
        {
            var r = FirstLatch();
            long until = r.HoldUntilMs;
            var empty = r.Observe(until - 5_000, false, 0);
            H.Check("A timed hold ignores an empty pad list: only the session hold releases on every pad gone",
                    empty == Held && r.HoldUntilMs == until && r.Latches == 1, $"verdict {empty}");
        }

        // ── expiry ───────────────────────────────────────────────────────────────────────────────────
        {
            var r = FirstLatch();
            long until = r.HoldUntilMs;
            var justBefore = Off(r, until - 1);
            var atExpiry = Off(r, until);
            var next = Off(r, until + 1_000);
            H.Check("The hold lasts until its deadline: 1 ms before it still holds", justBefore == Held);
            H.Check("At the deadline the hold reports expired exactly once",
                    atExpiry == PadCountChurnRule.Verdict.Expired && next == None,
                    $"at deadline {atExpiry}, next pass {next}");
            H.Check("Expiry clears the hold and the recorded trips; the latch count stands",
                    r.HoldUntilMs == 0 && r.PendingTrips == 0 && r.Latches == 1);
        }
        {
            var r = FirstLatch();
            long until = r.HoldUntilMs;
            Off(r, until);
            var edge = On(r, until + 1_000);
            H.Check("The first guard-ON edge after expiry counts as a new trip",
                    edge == None && r.PendingTrips == 1, $"verdict {edge}, pending {r.PendingTrips}");
        }
        {
            var r = FirstLatch();
            long until = r.HoldUntilMs;
            var atExpiry = On(r, until);
            H.Check("An expiry pass that still sees the guard on is not itself a trip: the latch's guard-on carries over",
                    atExpiry == PadCountChurnRule.Verdict.Expired && r.PendingTrips == 0,
                    $"verdict {atExpiry}, pending {r.PendingTrips}");
            Off(r, until + 1_000);
            On(r, until + 2_000);
            H.Check("...and the next off-then-on after it is a trip", r.PendingTrips == 1, $"pending {r.PendingTrips}");
        }

        // ── the second latch: the session hold ───────────────────────────────────────────────────────
        {
            var r = FirstLatch();
            long first = r.HoldUntilMs;
            Off(r, first);
            Edge(r, first + 1_000);
            Edge(r, first + 6_000);
            var third = Edge(r, first + 11_000);
            H.Check("A second latch in the session holds with no deadline (long.MaxValue)",
                    third == LatchedSession && r.HoldUntilMs == long.MaxValue && r.Latches == 2,
                    $"verdict {third}, hold until {r.HoldUntilMs}");
        }
        {
            var r = SessionLatch(out long latchedAt);
            var distant = r.Observe(latchedAt + 3L * 365 * 24 * 3_600_000, true, PadsFlapped);
            var farthest = r.Observe(long.MaxValue - 1, false, PadsSteady);
            H.Check("A session hold outlasts any clock reading while a pad is present",
                    distant == Held && farthest == Held && r.HoldUntilMs == long.MaxValue,
                    $"3 years on {distant}, at the clock's end {farthest}");
        }
        {
            var r = SessionLatch(out long latchedAt);
            var one = r.Observe(latchedAt + 1_000, false, 1);
            var two = r.Observe(latchedAt + 2_000, true, 2);
            var released = r.Observe(latchedAt + 3_000, false, 0);
            H.Check("Only every pad gone releases a session hold: one or two pads keep it",
                    one == Held && two == Held && released == PadCountChurnRule.Verdict.Released,
                    $"one pad {one}, two pads {two}, none {released}");
            H.Check("A release is not a hold, and it forgets the history: hold, latch count and trips all clear",
                    (released & Held) == None && r.HoldUntilMs == 0 && r.Latches == 0 && r.PendingTrips == 0);
            var after = Off(r, latchedAt + 4_000);
            H.Check("The release is reported once", after == None, $"next pass {after}");
        }
        {
            var r = SessionLatch(out long latchedAt);
            r.Observe(latchedAt + 1_000, false, 0);
            long t = latchedAt + 10_000;
            Edge(r, t);
            Edge(r, t + 1_000);
            var latch = Edge(r, t + 2_000);
            H.Check("After a release the next latch is a first latch again: 60 s, not the session",
                    latch == LatchedTimed && r.HoldUntilMs == t + 2_000 + PadCountChurnRule.ChurnHoldMs && r.Latches == 1,
                    $"verdict {latch}, hold until {r.HoldUntilMs}");
        }

        // ── Reset ────────────────────────────────────────────────────────────────────────────────────
        {
            var r = FirstLatch();
            r.Reset();
            H.Check("Reset clears a timed hold, the latch count and the recorded trips",
                    r.HoldUntilMs == 0 && r.Latches == 0 && r.PendingTrips == 0);
            var next = Off(r, 12_000);
            H.Check("After Reset the next pass is not held", next == None, $"verdict {next}");
        }
        {
            var r = SessionLatch(out long latchedAt);
            r.Reset();
            var next = r.Observe(latchedAt + 1_000, false, PadsSteady);
            H.Check("Reset ends a session hold without waiting for every pad to go",
                    r.HoldUntilMs == 0 && r.Latches == 0 && next == None, $"verdict {next}");
        }
        {
            var r = new PadCountChurnRule();
            Off(r, 0);
            On(r, 1_000);
            r.Reset();
            var edge = On(r, 2_000);
            H.Check("Reset forgets that the guard was on: the next guard-on pass is a fresh trip",
                    edge == None && r.PendingTrips == 1, $"verdict {edge}, pending {r.PendingTrips}");
        }
        {
            var r = new PadCountChurnRule();
            Edge(r, 0);
            Edge(r, 1_000);
            r.Reset();
            Edge(r, 2_000);
            var latch = Edge(r, 3_000);
            H.Check("Trips recorded before a Reset do not count toward a latch after it",
                    latch == None && r.PendingTrips == 2 && r.Latches == 0, $"verdict {latch}");
        }
    }
}
