using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Core/WheelStateMachine.cs — stick-to-armed-slice arming, the confirm/delete dwells and the
/// in-wheel edit-mode operations. Pure Core, no pad, no window; every scenario drives the class directly.
/// <para>Two clocks work differently here and the scenarios below drive them accordingly. The confirm and
/// delete-hold dwells advance by a fixed <see cref="WheelStateMachine.ConfirmTickMs"/> per <c>Tick</c> call
/// regardless of real elapsed time, so they're driven by looping <c>Tick</c>. The sticky-grace window, the
/// edit-mode reflow and the landing settle read the machine's millisecond clock, which these scenarios pin
/// (<c>PinnedClockMs</c>) and step by hand — no scenario waits on the wall clock.</para>
/// <para>Either-stick aiming ("the more-deflected stick wins") is NOT tested here: <c>UpdateStick</c> takes
/// one already-merged (x,y) pair, and the merge itself lives in App.xaml.cs's <c>ApplyStick</c> — a WPF-shell
/// concern outside this class and this harness's easy reach.</para></summary>
internal static class T_WheelStateMachine
{
    // ── slice builders ───────────────────────────────────────────────────────────────────────────────

    private static WheelSlice Plain(string label) => new() { Label = label };

    private static WheelSlice Guarded(string label) =>
        new() { Label = label, Action = new ActionConfig { Type = "system", RequireConfirm = true } };

    private static WheelSlice[] PlainSlices(int n) =>
        Enumerable.Range(0, n).Select(i => Plain($"S{i}")).ToArray();

    // ── angle helper ─────────────────────────────────────────────────────────────────────────────────
    // AngleToIndex's own formula (WheelStateMachine.cs): deg = atan2(y,x)*180/pi + 90, normalized to
    // [0,360); index = floor((deg + halfSlice) / sliceWidth) mod n. Vec() is that formula's inverse, so
    // Vec(deg) fed back through IndexAtAngle/UpdateStick reproduces the same deg (mod 360) — it lets a
    // scenario name a bucket boundary in DEGREES instead of guessing raw (x,y) pairs by hand.
    private static (float x, float y) Vec(double deg, double mag = 1.0)
    {
        double rad = (deg - 90.0) * Math.PI / 180.0;
        return ((float)(mag * Math.Cos(rad)), (float)(mag * Math.Sin(rad)));
    }

    /// <summary>Angle (in <see cref="Vec"/>'s convention) of bucket i's own centre, for an n-slice wheel.</summary>
    private static double BucketCenterDeg(int i, int n) => i * (360.0 / n);

    /// <summary>Angle of the boundary between bucket i and bucket (i+1)%n — i = n-1 is the 360°/0° wrap.</summary>
    private static double BoundaryDeg(int i, int n) => (i + 0.5) * (360.0 / n);

    private static void Settle(WheelStateMachine sm, float x, float y, int iterations = 60)
    {
        for (int i = 0; i < iterations; i++) sm.UpdateStick(x, y);
    }

    /// <summary>Pin the machine's millisecond clock (the internal <c>PinnedClockMs</c> seam) to
    /// <paramref name="ms"/>; every sticky, reflow and landing read then sees exactly that instant.</summary>
    private static void PinClock(WheelStateMachine sm, long ms)
    {
        var f = typeof(WheelStateMachine).GetField("PinnedClockMs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(nameof(WheelStateMachine), "PinnedClockMs");
        f.SetValue(sm, (long?)ms);
    }

    /// <summary>Run <paramref name="a"/> with Reduce Motion forced off, restoring both inputs after: the
    /// reflow and landing clocks never start under it.</summary>
    private static void WithFullMotion(Action a)
    {
        bool user = MotionPolicy.UserSetting, system = MotionPolicy.SystemPrefersReduced;
        MotionPolicy.UserSetting = false;
        MotionPolicy.SystemPrefersReduced = false;
        try { a(); }
        finally { MotionPolicy.UserSetting = user; MotionPolicy.SystemPrefersReduced = system; }
    }

    public static void Run()
    {
        NearestSlice();
        DeadzoneAndFireCancel();
        StickyGraceWindow();
        ReflowTiming();
        LandingTiming();
        GuardedConfirmDwell();
        DeleteHoldDwell();
        EditEntryKeepsArmed();
        EditPickUpCarryDrop();
        EditReorderBounds();
        EditUndoRedo();
        AddAndCancelCarry();
        TickWhileHidden();
    }

    // ── nearest-slice selection: pure IndexAtAngle, no smoothing/deadzone involved ──────────────────────
    private static void NearestSlice()
    {
        H.Group("WheelStateMachine — nearest-slice selection (IndexAtAngle, pure)");

        // Independent sanity check that doesn't rely on the Vec()/BucketCenterDeg derivation: four
        // literal cardinal vectors, 90° apart, must land in four DIFFERENT buckets for a 4-slice wheel.
        H.Try("four cardinal directions hit four distinct slices (n=4)", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(4) };
            var hit = new HashSet<int>
            {
                sm.IndexAtAngle(1, 0), sm.IndexAtAngle(0, 1),
                sm.IndexAtAngle(-1, 0), sm.IndexAtAngle(0, -1),
            };
            H.Check("all four are in range and distinct", hit.Count == 4 && hit.All(i => i is >= 0 and < 4),
                    string.Join(",", hit));
        });

        foreach (int n in new[] { 1, 2, 3, 8, 12 })
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(n) };

            for (int i = 0; i < n; i++)
            {
                var (x, y) = Vec(BucketCenterDeg(i, n));
                H.Check($"n={n}: bucket {i}'s own centre selects {i}", sm.IndexAtAngle(x, y) == i);
            }

            if (n == 1)
            {
                // One slice covers the whole circle — every angle must land on it, including the 0°/360°
                // seam itself.
                foreach (double deg in new[] { 0, 45, 90, 180, 270, 359.999 })
                    H.Check($"n=1: {deg}° still selects 0", sm.IndexAtAngle(Vec(deg).x, Vec(deg).y) == 0);
                continue;
            }

            const double eps = 0.05; // degrees; far above float round-trip error (~1e-6) through Vec/atan2
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                double b = BoundaryDeg(i, n);
                var (xi, yi) = Vec(b - eps);
                var (xo, yo) = Vec(b + eps);
                string wrap = next == 0 ? " (0°/360° wrap)" : "";
                H.Check($"n={n}: just before the {i}→{next} boundary{wrap} is still {i}",
                        sm.IndexAtAngle(xi, yi) == i);
                H.Check($"n={n}: just after the {i}→{next} boundary{wrap} is {next}",
                        sm.IndexAtAngle(xo, yo) == next);
            }
        }
    }

    // ── deadzone boundary + release-to-fire vs release-while-centred cancel ─────────────────────────────
    private static void DeadzoneAndFireCancel()
    {
        H.Group("WheelStateMachine — deadzone boundary, arm/fire/cancel");

        H.Try("just inside the deadzone does not arm", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(4) };
            var (x, y) = Vec(0, WheelStateMachine.Deadzone - 0.005);
            Settle(sm, x, y);
            H.Check("ArmedIndex == -1", sm.ArmedIndex == -1, $"got {sm.ArmedIndex}");
        });

        H.Try("just outside the deadzone arms the pointed-at slice", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(4) };
            int expected = sm.IndexAtAngle(0, -1);
            var (x, y) = Vec(0, WheelStateMachine.Deadzone + 0.005);
            Settle(sm, x, y);
            H.Check("ArmedIndex == the pointed-at slice", sm.ArmedIndex == expected, $"got {sm.ArmedIndex}");
        });

        H.Try("release while centred (never armed) reads as cancel", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(4) };
            Settle(sm, 0, 0);
            H.Check("ArmedIndex == -1", sm.ArmedIndex == -1);
            H.Check("StickyArmedIndex == -1 (nothing to fire)", sm.StickyArmedIndex == -1);
        });

        H.Try("still armed at release reads as fire", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(4) };
            var (x, y) = Vec(0, 1.0);
            Settle(sm, x, y);
            int armed = sm.ArmedIndex;
            H.Check("armed", armed >= 0);
            H.Check("StickyArmedIndex mirrors the live armed slice", sm.StickyArmedIndex == armed);
        });
    }

    // ── the sticky grace window ──────────────────────────────────────────────────────────────────────
    private static void StickyGraceWindow()
    {
        H.Group("WheelStateMachine — sticky grace window (pinned clock)");

        H.Try("recentring inside the grace window still fires; one ms past it cancels", () =>
        {
            const long t0 = 10_000;
            var sm = new WheelStateMachine { Slices = PlainSlices(4), StickyMs = 200 };
            PinClock(sm, t0);
            var (x, y) = Vec(0, 1.0);
            Settle(sm, x, y);
            int armed = sm.ArmedIndex;
            H.Check("armed before recentring", armed >= 0);

            Settle(sm, 0, 0); // the last report still past the deadzone stamps the window at t0
            H.Check("ArmedIndex drops once centred", sm.ArmedIndex == -1);
            H.Check("StickyArmedIndex fires at the recentring instant", sm.StickyArmedIndex == armed,
                    $"got {sm.StickyArmedIndex}");

            PinClock(sm, t0 + sm.StickyMs);
            H.Check("…and still fires at exactly StickyMs past it (the window is inclusive)",
                    sm.StickyArmedIndex == armed, $"got {sm.StickyArmedIndex}");

            PinClock(sm, t0 + sm.StickyMs + 1);
            H.Check("StickyArmedIndex cancels one ms past the window", sm.StickyArmedIndex == -1,
                    $"got {sm.StickyArmedIndex}");
        });

        H.Try("the window runs from the LAST armed report, so a re-aim restarts it", () =>
        {
            const long t0 = 20_000;
            var sm = new WheelStateMachine { Slices = PlainSlices(4), StickyMs = 200 };
            PinClock(sm, t0);
            Settle(sm, Vec(0, 1.0).x, Vec(0, 1.0).y);
            Settle(sm, 0, 0);

            PinClock(sm, t0 + 150);
            var (x, y) = Vec(180, 1.0);
            Settle(sm, x, y);                       // aim at the opposite slice, stamped at t0 + 150
            int second = sm.ArmedIndex;
            Settle(sm, 0, 0);
            H.Check("re-armed a different slice", second >= 0);

            PinClock(sm, t0 + 150 + sm.StickyMs);
            H.Check("the re-aim's own window is still open", sm.StickyArmedIndex == second,
                    $"got {sm.StickyArmedIndex}");
            PinClock(sm, t0 + 150 + sm.StickyMs + 1);
            H.Check("…and closes StickyMs after the re-aim", sm.StickyArmedIndex == -1,
                    $"got {sm.StickyArmedIndex}");
        });
    }

    // ── edit-mode reflow clock ───────────────────────────────────────────────────────────────────────
    private static void ReflowTiming()
    {
        H.Group("WheelStateMachine — edit-mode reflow clock (pinned)");

        H.Try("a nudge reflows over ReflowMs, and the first tick at ReflowMs settles it", () => WithFullMotion(() =>
        {
            const long t0 = 50_000;
            long reflow = (long)WheelStateMachine.ReflowMs;
            var sm = new WheelStateMachine();
            PinClock(sm, t0);
            sm.BeginEdit(PlainSlices(4));
            Settle(sm, 0, -1);
            H.Check("armed a slice", sm.ArmedIndex >= 0);
            H.Check("no reflow before the edit", !sm.Reflowing && sm.ReflowProgress == 1.0);

            sm.NudgeSelected(1);
            H.Check("the nudge starts a reflow", sm.Reflowing);
            H.Check("progress 0 at the instant it starts", sm.ReflowProgress == 0.0, $"got {sm.ReflowProgress}");
            H.Check("the pre-change centres are captured", sm.PrevCenter is { Count: 4 });
            H.Check("Settling while the reflow runs", sm.Settling);

            PinClock(sm, t0 + reflow / 2);
            H.Check("half way at ReflowMs / 2", Math.Abs(sm.ReflowProgress - 0.5) < 1e-9, $"got {sm.ReflowProgress}");
            var mid = sm.Tick(true);
            H.Check("a mid-reflow tick keeps it running and repaints", sm.Reflowing && mid.Invalidate);

            PinClock(sm, t0 + reflow - 1);
            sm.Tick(true);
            H.Check("still running one ms short of ReflowMs", sm.Reflowing);

            PinClock(sm, t0 + reflow);
            H.Check("progress reads 1.0 at ReflowMs", sm.ReflowProgress == 1.0, $"got {sm.ReflowProgress}");
            H.Check("…but the reflow runs until a tick settles it", sm.Reflowing);
            sm.Tick(true);
            H.Check("the tick at ReflowMs settles it", !sm.Reflowing);
            H.Check("settled progress reads 1.0", sm.ReflowProgress == 1.0);
            H.Check("the captured centres are dropped", sm.PrevCenter is null);
            H.Check("nothing left to hold the wheel open for", !sm.Settling);
        }));

        H.Try("a delete's ghost fades out over the same clock", () => WithFullMotion(() =>
        {
            const long t0 = 70_000;
            long reflow = (long)WheelStateMachine.ReflowMs;
            var sm = new WheelStateMachine();
            PinClock(sm, t0);
            sm.BeginEdit(PlainSlices(4));
            Settle(sm, 0, -1);
            int n = (int)Math.Ceiling(sm.DeleteConfirmMs / WheelStateMachine.ConfirmTickMs);
            sm.SetDeleteHeld(true);
            for (int i = 0; i < n; i++) sm.Tick(true);
            H.Check("the dwell deleted the slice", sm.CurrentSlices.Length == 3);
            H.Check("the ghost is at full strength on the deleting instant",
                    sm.Ghost is not null && sm.GhostFade == 1.0, $"fade={sm.GhostFade}");
            H.Check("the ghost is sized from the pre-delete count", sm.GhostCount == 4, $"got {sm.GhostCount}");

            PinClock(sm, t0 + reflow / 4);
            H.Check("a quarter of the way through, the ghost is at 0.75", Math.Abs(sm.GhostFade - 0.75) < 1e-9,
                    $"got {sm.GhostFade}");

            PinClock(sm, t0 + reflow);
            sm.Tick(true);
            H.Check("once the reflow settles, the ghost is gone", sm.Ghost is null && sm.GhostFade == 0.0);
        }));

        H.Try("under Reduce Motion an edit is instant: no reflow clock, nothing settling", () =>
        {
            bool user = MotionPolicy.UserSetting;
            MotionPolicy.UserSetting = true;
            try
            {
                var sm = new WheelStateMachine();
                PinClock(sm, 90_000);
                sm.BeginEdit(PlainSlices(4));
                Settle(sm, 0, -1);
                bool moved = sm.NudgeSelected(1);
                H.Check("the nudge still happened", moved && sm.StructureVersion > 0);
                H.Check("no reflow runs", !sm.Reflowing);
                H.Check("progress reads 1.0 immediately", sm.ReflowProgress == 1.0);
                H.Check("nothing to hold the wheel open for", !sm.Settling);
            }
            finally { MotionPolicy.UserSetting = user; }
        });
    }

    // ── edit-mode landing settle ─────────────────────────────────────────────────────────────────────
    private static void LandingTiming()
    {
        H.Group("WheelStateMachine — edit-mode landing settle (pinned clock)");

        H.Try("a drop lands for LandingMs inclusive, progress running 0 → 1", () => WithFullMotion(() =>
        {
            const long t0 = 120_000;
            long landing = (long)WheelStateMachine.LandingMs, reflow = (long)WheelStateMachine.ReflowMs;
            var sm = new WheelStateMachine();
            PinClock(sm, t0);
            sm.BeginEdit(PlainSlices(4));
            Settle(sm, 0, -1);
            sm.PickUpArmed();
            Settle(sm, 0, 1);                       // the carry preview reflows, clocked at t0
            sm.DropCarried();
            int landed = sm.ArmedIndex;
            H.Check("the dropped slice is the one landing", landed >= 0 && sm.LandingIndex == landed,
                    $"landing={sm.LandingIndex} armed={landed}");
            H.Check("landing at the drop instant", sm.Landing);
            H.Check("progress 0 at the drop", sm.LandingProgress == 0.0, $"got {sm.LandingProgress}");
            H.Check("Settling while it lands", sm.Settling);

            PinClock(sm, t0 + landing / 2);
            H.Check("half way at LandingMs / 2", Math.Abs(sm.LandingProgress - 0.5) < 1e-9,
                    $"got {sm.LandingProgress}");
            H.Check("a tick mid-landing repaints", sm.Tick(true).Invalidate);

            PinClock(sm, t0 + reflow);
            sm.Tick(true);
            H.Check("the reflow settling mid-landing leaves the landing running", !sm.Reflowing && sm.Landing);
            H.Check("…so the wheel is still Settling", sm.Settling);

            PinClock(sm, t0 + landing);
            H.Check("still landing at exactly LandingMs (inclusive)", sm.Landing);
            H.Check("progress 1.0 at LandingMs", sm.LandingProgress == 1.0, $"got {sm.LandingProgress}");

            PinClock(sm, t0 + landing + 1);
            H.Check("done one ms later", !sm.Landing);
            H.Check("progress stays clamped at 1.0", sm.LandingProgress == 1.0);
            H.Check("nothing left Settling", !sm.Settling);
            H.Check("a tick after the landing no longer repaints", !sm.Tick(true).Invalidate);
        }));
    }

    // ── hold-to-confirm dwell ────────────────────────────────────────────────────────────────────────
    private static void GuardedConfirmDwell()
    {
        H.Group("WheelStateMachine — hold-to-confirm dwell (Tick-driven, no wall clock)");

        H.Try("fires at the confirm threshold, not before; stays done after", () =>
        {
            var sm = new WheelStateMachine();
            var slices = new[] { Guarded("Guarded"), Plain("Other") };
            sm.Slices = slices;
            var (x, y) = Vec(0, 1.0);
            Settle(sm, x, y);
            int armed = sm.ArmedIndex;
            H.Check("armed the guarded slice", armed == 0, $"armed={armed}");
            H.Check("RequiresConfirm(0)", sm.RequiresConfirm(0));

            double holdMs = (double)H.GetStatic(typeof(WheelStateMachine), "ConfirmHoldMs");
            int n = (int)Math.Ceiling(holdMs / WheelStateMachine.ConfirmTickMs);

            for (int i = 0; i < n - 1; i++) sm.Tick(true);
            H.Check($"not done at {n - 1} ticks", !sm.ConfirmDone, $"progress={sm.ConfirmProgress}");
            H.Check("not fire-ready yet", !sm.ArmedConfirmReady);

            var result = sm.Tick(true); // the n-th tick
            H.Check($"done at {n} ticks", sm.ConfirmDone);
            H.Check("fire-ready once done", sm.ArmedConfirmReady);
            H.Check("progress reads 1.0", sm.ConfirmProgress == 1.0, $"got {sm.ConfirmProgress}");
            H.Check("the completing tick invalidates", result.Invalidate);

            var after = sm.Tick(true); // one more tick past done
            H.Check("still done, doesn't over-advance", sm.ConfirmDone && sm.ConfirmProgress == 1.0);
            H.Check("a no-op tick after done does not invalidate", !after.Invalidate);
        });

        H.Try("re-aiming away and back resets progress (does not accumulate)", () =>
        {
            var sm = new WheelStateMachine();
            sm.Slices = new[] { Guarded("Guarded"), Plain("Other") };
            var (gx, gy) = Vec(0, 1.0);
            var (ox, oy) = Vec(180, 1.0);

            Settle(sm, gx, gy);
            H.Check("armed the guarded slice", sm.ArmedIndex == 0);
            for (int i = 0; i < 10; i++) sm.Tick(true);
            double partial = sm.ConfirmProgress;
            H.Check("partial progress accrued", partial > 0 && !sm.ConfirmDone, $"progress={partial}");

            Settle(sm, ox, oy);
            H.Check("armed the other (unguarded) slice", sm.ArmedIndex == 1);
            sm.Tick(true);
            H.Check("progress cleared while aiming off the guarded slice", sm.ConfirmProgress == 0);

            Settle(sm, gx, gy);
            H.Check("re-armed the guarded slice", sm.ArmedIndex == 0);
            for (int i = 0; i < 10; i++) sm.Tick(true);
            H.Check("progress after re-aiming matches a fresh 10 ticks, not 10+10",
                    Math.Abs(sm.ConfirmProgress - partial) < 1e-9, $"got {sm.ConfirmProgress} vs first {partial}");
        });

        H.Try("a completed dwell does not carry onto a different action swapped into the same slot", () =>
        {
            static WheelSlice Sys(string cmd) =>
                new() { Label = cmd, Action = new ActionConfig { Type = "system", Command = cmd, RequireConfirm = true } };

            var sm = new WheelStateMachine();
            var original = new[] { Sys("sleep"), Plain("Other") };
            sm.Slices = original;
            var (x, y) = Vec(0, 1.0);
            Settle(sm, x, y);
            double holdMs = (double)H.GetStatic(typeof(WheelStateMachine), "ConfirmHoldMs");
            int n = (int)Math.Ceiling(holdMs / WheelStateMachine.ConfirmTickMs);
            for (int i = 0; i < n; i++) sm.Tick(true);
            H.Check("dwell completed on slot 0", sm.ArmedConfirmReady);

            sm.Slices = original;
            H.Check("re-setting the same list keeps the completed dwell", sm.ArmedConfirmReady);

            sm.Slices = new[] { Sys("sleep"), Plain("Other") };
            H.Check("a reloaded copy of the same action keeps the completed dwell", sm.ArmedConfirmReady);

            sm.Slices = new[] { Sys("shutdown"), Plain("Other") };
            H.Check("a different guarded action in slot 0 is not confirm-ready", !sm.ArmedConfirmReady);
            H.Check("progress cleared", sm.ConfirmProgress == 0 && !sm.ConfirmDone,
                    $"progress={sm.ConfirmProgress}");

            for (int i = 0; i < n - 1; i++) sm.Tick(true);
            H.Check("a fresh dwell is needed: not ready one tick short", !sm.ArmedConfirmReady);
            sm.Tick(true);
            H.Check("ready after a full fresh dwell", sm.ArmedConfirmReady);

            sm.Slices = new[] { Plain("Gone") };
            sm.Slices = new[] { Sys("shutdown") };
            H.Check("a shrunken list then a restored one starts over", !sm.ArmedConfirmReady);
        });
        H.Try("RequiresConfirm is false out of range", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(2) };
            H.Check("negative index", !sm.RequiresConfirm(-1));
            H.Check("index == count", !sm.RequiresConfirm(2));
        });
    }

    // ── edit-mode hold-□ delete dwell ────────────────────────────────────────────────────────────────
    private static void DeleteHoldDwell()
    {
        H.Group("WheelStateMachine — edit-mode delete-hold dwell (Tick-driven)");

        H.Try("deletes at the threshold, not before", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(4));
            var (x, y) = Vec(0, 1.0);
            Settle(sm, x, y);
            int armed = sm.ArmedIndex;
            H.Check("armed a slice in edit mode", armed >= 0);

            int n = (int)Math.Ceiling(sm.DeleteConfirmMs / WheelStateMachine.ConfirmTickMs);
            sm.SetDeleteHeld(true);
            for (int i = 0; i < n - 1; i++) sm.Tick(true);
            H.Check($"not deleted at {n - 1} ticks", sm.CurrentSlices.Length == 4, $"progress={sm.DeleteProgress}");
            H.Check("progress > 0", sm.DeleteProgress > 0);

            sm.Tick(true); // the n-th tick
            H.Check($"deleted at {n} ticks", sm.CurrentSlices.Length == 3);
            H.Check("delete progress resets after completion", sm.DeleteProgress == 0);
            H.Check("StructureVersion bumped", sm.StructureVersion > 0);
        });

        H.Try("releasing the delete hold early cancels the dwell without deleting", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(3));
            Settle(sm, Vec(0, 1.0).x, Vec(0, 1.0).y);
            sm.SetDeleteHeld(true);
            for (int i = 0; i < 5; i++) sm.Tick(true);
            H.Check("partial progress", sm.DeleteProgress > 0);

            sm.SetDeleteHeld(false);
            H.Check("progress cancels immediately on release", sm.DeleteProgress == 0);
            sm.Tick(true);
            H.Check("nothing deleted", sm.CurrentSlices.Length == 3);
        });
    }

    // ── edit mode: entry ─────────────────────────────────────────────────────────────────────────────
    private static void EditEntryKeepsArmed()
    {
        H.Group("WheelStateMachine — edit mode: entry");

        H.Try("entering edit mode keeps the slice that was armed at the click", () =>
        {
            var sm = new WheelStateMachine { Slices = PlainSlices(4) };
            var (x, y) = Vec(0, 1.0);
            Settle(sm, x, y);
            int armed = sm.ArmedIndex;
            H.Check("a slice is armed on the live wheel before the click", armed >= 0, $"got {armed}");

            sm.BeginEdit(PlainSlices(4));
            H.Check("entering edit mode keeps the slice that was armed at the click", sm.ArmedIndex == armed,
                    $"armed {armed}, got {sm.ArmedIndex}");

            Settle(sm, x, y);
            H.Check("…while the stick holds the same tilt", sm.ArmedIndex == armed, $"got {sm.ArmedIndex}");
            Settle(sm, 0, 0);
            H.Check("…and when the stick recentres", sm.ArmedIndex == armed, $"got {sm.ArmedIndex}");

            var (x2, y2) = Vec(180, 1.0);
            Settle(sm, x2, y2);
            int other = sm.IndexAtAngle(x2, y2);
            H.Check("…until the stick aims at another slice", other != armed && sm.ArmedIndex == other,
                    $"armed {armed}, aimed {other}, got {sm.ArmedIndex}");
        });
    }

    // ── edit mode: pick up / carry / drop ────────────────────────────────────────────────────────────
    private static void EditPickUpCarryDrop()
    {
        H.Group("WheelStateMachine — edit mode: pick up / carry / drop");

        H.Try("carrying to a new slot commits the exact previewed order", () =>
        {
            var original = PlainSlices(4);
            var sm = new WheelStateMachine();
            sm.BeginEdit(original);
            int pickIdx = sm.IndexAtAngle(0, -1);
            int targetIdx = sm.IndexAtAngle(0, 1);
            H.Check("pick and target angles resolve to different slots", pickIdx != targetIdx,
                    $"pick={pickIdx} target={targetIdx}");

            Settle(sm, 0, -1);
            H.Check("armed the pick slot", sm.ArmedIndex == pickIdx);
            var pickedSlice = sm.CurrentSlices[pickIdx];

            sm.PickUpArmed();
            H.Check("phase is Carrying", sm.Phase == WheelStateMachine.EditPhase.Carrying);
            H.Check("carried slice is the one picked up", ReferenceEquals(sm.CarriedSlice, pickedSlice));
            H.Check("carry starts back at its own slot", sm.CarrySlot == pickIdx);
            H.Check("this is a MOVE, not an add", !sm.CarryIsAdd);

            Settle(sm, 0, 1);
            H.Check("CarryTarget follows the aim", sm.CarryTarget == targetIdx);
            H.Check("CarrySlot previews the drop position", sm.CarrySlot == targetIdx);

            bool changed = sm.DropCarried();
            H.Check("drop reports a change", changed);
            H.Check("back to Selecting", sm.Phase == WheelStateMachine.EditPhase.Selecting);
            H.Check("ArmedIndex follows the dropped slice", sm.ArmedIndex == targetIdx);

            var expected = new List<WheelSlice>(original);
            expected.RemoveAt(pickIdx);
            expected.Insert(Math.Clamp(targetIdx, 0, expected.Count), pickedSlice);
            bool sameOrder = sm.CurrentSlices.Length == expected.Count
                              && sm.CurrentSlices.Select((s, i) => ReferenceEquals(s, expected[i])).All(ok => ok);
            H.Check("committed order matches base-minus-picked with the slice inserted at the target",
                    sameOrder, string.Join(",", sm.CurrentSlices.Select(s => s.Label)));
            H.Check("StructureVersion bumped on a real move", sm.StructureVersion > 0);
        });

        H.Try("dropping back in the same slot reports no change", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(4));
            Settle(sm, 0, -1);
            int idx = sm.ArmedIndex;
            sm.PickUpArmed();
            // Never move the aim: the provisional order equals the committed order.
            bool changed = sm.DropCarried();
            H.Check("no-op drop reports unchanged", !changed);
            H.Check("StructureVersion did not bump", sm.StructureVersion == 0);
            H.Check("still armed on the same slot", sm.ArmedIndex == idx);
        });
    }

    // ── edit mode: reorder (nudge) bounds ────────────────────────────────────────────────────────────
    private static void EditReorderBounds()
    {
        H.Group("WheelStateMachine — edit mode: reorder (nudge) bounds");

        H.Try("nudging the last slice forward wraps to the first (0°/360° wrap in list terms)", () =>
        {
            var original = PlainSlices(4);
            var sm = new WheelStateMachine();
            sm.BeginEdit(original);
            Settle(sm, 0, -1);
            // Walk the armed index to the last slot with repeated nudges is circular; instead re-derive it:
            int last = sm.CurrentSlices.Length - 1;
            while (sm.ArmedIndex != last) sm.NudgeSelected(1);
            var atLast = sm.CurrentSlices[last];

            bool moved = sm.NudgeSelected(1);
            H.Check("nudge past the end reports moved", moved);
            H.Check("wrapped to index 0", sm.ArmedIndex == 0);
            H.Check("the slice that was last is now first", ReferenceEquals(sm.CurrentSlices[0], atLast));
        });

        H.Try("nudging the first slice backward wraps to the last", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(4));
            Settle(sm, 0, -1);
            while (sm.ArmedIndex != 0) sm.NudgeSelected(1);
            var atFirst = sm.CurrentSlices[0];

            bool moved = sm.NudgeSelected(-1);
            H.Check("nudge before the start reports moved", moved);
            H.Check("wrapped to the last index", sm.ArmedIndex == sm.CurrentSlices.Length - 1);
            H.Check("the slice that was first is now last", ReferenceEquals(sm.CurrentSlices[^1], atFirst));
        });

        H.Try("nudging a single-slice wheel is a no-op", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(1));
            Settle(sm, 0, -1);
            H.Check("armed the only slice", sm.ArmedIndex == 0);
            bool moved = sm.NudgeSelected(1);
            H.Check("reports no move", !moved);
        });

        H.Try("nudging with nothing armed is a no-op", () =>
        {
            var sm = new WheelStateMachine();
            sm.Reset(); // guarantees ArmedIndex == -1 regardless of any prior scenario's leftover state
            sm.BeginEdit(PlainSlices(4));
            H.Check("nothing armed yet", sm.ArmedIndex == -1);
            bool moved = sm.NudgeSelected(1);
            H.Check("reports no move", !moved);
        });
    }

    // ── edit mode: undo / redo, including the empty-stack bounds ────────────────────────────────────
    private static void EditUndoRedo()
    {
        H.Group("WheelStateMachine — edit mode: undo / redo bounds");

        H.Try("undo/redo on a fresh edit session are no-ops (empty-stack bound)", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(3));
            H.Check("CanUndo false", !sm.CanUndo);
            H.Check("CanRedo false", !sm.CanRedo);
            H.Check("Undo() returns false", !sm.Undo());
            H.Check("Redo() returns false", !sm.Redo());
            H.Check("slice count unaffected", sm.CurrentSlices.Length == 3);
        });

        H.Try("one structural change round-trips through undo and redo", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(4));
            var before = sm.CurrentSlices;
            Settle(sm, 0, -1);
            while (sm.ArmedIndex != 0) sm.NudgeSelected(1);
            sm.NudgeSelected(1); // one real structural change
            var afterMove = sm.CurrentSlices;
            H.Check("order actually changed", !before.SequenceEqual(afterMove));
            H.Check("CanUndo true, CanRedo false", sm.CanUndo && !sm.CanRedo);

            H.Check("Undo restores the pre-move order", sm.Undo() && sm.CurrentSlices.SequenceEqual(before));
            H.Check("stack flips: CanUndo false, CanRedo true", !sm.CanUndo && sm.CanRedo);

            H.Check("Redo re-applies the move", sm.Redo() && sm.CurrentSlices.SequenceEqual(afterMove));
            H.Check("stack flips back: CanUndo true, CanRedo false", sm.CanUndo && !sm.CanRedo);

            // Empty-stack bound the OTHER side: undo back to empty, then one more undo must not throw
            // or corrupt state.
            sm.Undo();
            H.Check("CanUndo false again", !sm.CanUndo);
            H.Check("a second Undo() past empty returns false", !sm.Undo());
            H.Check("order still the pre-move order", sm.CurrentSlices.SequenceEqual(before));
        });
    }

    // ── Add picker / AddSliceAndCarry, and CancelCarry's move-vs-add split ──────────────────────────
    private static void AddAndCancelCarry()
    {
        H.Group("WheelStateMachine — AddSliceAndCarry and CancelCarry (move vs add)");

        H.Try("AddSliceAndCarry appends and carries the new slice", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(3));
            var added = Plain("New");
            int at = sm.AddSliceAndCarry(added);
            H.Check("appended at the end", at == 3);
            H.Check("phase is Carrying", sm.Phase == WheelStateMachine.EditPhase.Carrying);
            H.Check("marked as an Add", sm.CarryIsAdd);
            H.Check("carried slice is the new one", ReferenceEquals(sm.CarriedSlice, added));
            H.Check("StructureVersion bumped on append", sm.StructureVersion > 0);
            H.Check("editList already holds 4 (append persists even before drop)", sm.CurrentSlices.Length == 4);
        });

        H.Try("cancelling a MOVE restores the original order and count", () =>
        {
            var original = PlainSlices(4);
            var sm = new WheelStateMachine();
            sm.BeginEdit(original);
            Settle(sm, 0, -1);
            int idx = sm.ArmedIndex;
            sm.PickUpArmed();
            Settle(sm, 0, 1); // move the aim elsewhere before cancelling
            sm.CancelCarry();

            H.Check("back to Selecting", sm.Phase == WheelStateMachine.EditPhase.Selecting);
            H.Check("count unchanged", sm.CurrentSlices.Length == 4);
            H.Check("order unchanged", sm.CurrentSlices.SequenceEqual(original));
            H.Check("armed index returns to the carried slice's home", sm.ArmedIndex == idx);
        });

        H.Try("cancelling an ADD removes the un-placed slice entirely", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(3));
            var added = Plain("New");
            sm.AddSliceAndCarry(added);
            int versionAfterAdd = sm.StructureVersion;
            Settle(sm, 0, 1);
            sm.CancelCarry();

            H.Check("back to Selecting", sm.Phase == WheelStateMachine.EditPhase.Selecting);
            H.Check("the added slice is gone", sm.CurrentSlices.Length == 3);
            H.Check("the added slice is not present by reference", !sm.CurrentSlices.Any(s => ReferenceEquals(s, added)));
            H.Check("CarryIsAdd cleared", !sm.CarryIsAdd);
            H.Check("StructureVersion bumped again for the removal", sm.StructureVersion > versionAfterAdd);
        });
    }

    // ── Tick while the wheel is hidden ───────────────────────────────────────────────────────────────
    private static void TickWhileHidden()
    {
        H.Group("WheelStateMachine — Tick while not visible");

        H.Try("a hidden tick drops confirm progress and never invalidates", () =>
        {
            var sm = new WheelStateMachine();
            sm.Slices = new[] { Guarded("Guarded") };
            Settle(sm, 0, -1);
            H.Check("armed", sm.ArmedIndex == 0);
            for (int i = 0; i < 5; i++) sm.Tick(true);
            H.Check("partial confirm progress before hiding", sm.ConfirmProgress > 0);

            var result = sm.Tick(false);
            H.Check("confirm progress cleared", sm.ConfirmProgress == 0);
            H.Check("confirm not done", !sm.ConfirmDone);
            H.Check("hidden tick never invalidates", !result.Invalidate);
        });

        H.Try("a hidden tick drops a partial delete-hold without deleting", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(3));
            Settle(sm, 0, -1);
            sm.SetDeleteHeld(true);
            for (int i = 0; i < 5; i++) sm.Tick(true);
            H.Check("partial delete progress before hiding", sm.DeleteProgress > 0);

            sm.Tick(false);
            H.Check("delete progress cleared", sm.DeleteProgress == 0);
            H.Check("nothing deleted", sm.CurrentSlices.Length == 3);
        });

        H.Try("a hidden tick preserves an in-progress carry", () =>
        {
            var sm = new WheelStateMachine();
            sm.BeginEdit(PlainSlices(3));
            Settle(sm, 0, -1);
            sm.PickUpArmed();
            var carried = sm.CarriedSlice;
            H.Check("carrying before hiding", sm.Phase == WheelStateMachine.EditPhase.Carrying);

            sm.Tick(false);
            H.Check("still carrying after a hidden tick", sm.Phase == WheelStateMachine.EditPhase.Carrying);
            H.Check("same slice still in hand", ReferenceEquals(sm.CarriedSlice, carried));
        });
    }
}
