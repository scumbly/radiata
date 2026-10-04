using System;
using System.Collections.Generic;

namespace ControllerWheel;

/// <summary>The pad-count churn breaker's rule. Capture's own action (plugging the stand-in) changes the
/// physical pad count it decides on whenever a software bus goes unrecognised, so the multi-pad guard can
/// trip and release on its own output. The guard turning ON <see cref="ChurnTrips"/> times inside
/// <see cref="ChurnWindowMs"/> latches a hold for <see cref="ChurnHoldMs"/>; a second latch holds for the
/// session, until every pad is gone or <see cref="Reset"/>.
/// <para>Pure rule: the caller supplies the clock on every call and owns the tracing and the timer that
/// re-runs capture when a timed hold expires. Not thread-safe; the caller serialises its calls.</para></summary>
public sealed class PadCountChurnRule
{
    public const int ChurnTrips = 3, ChurnWindowMs = 20_000, ChurnHoldMs = 60_000;

    /// <summary>What one <see cref="Observe"/> call concluded. Bits combine: a latch is always
    /// <see cref="Held"/> too, and a call that ends a hold can go on to evaluate the same scan as a new edge.</summary>
    [Flags]
    public enum Verdict
    {
        /// <summary>Not held, nothing to report.</summary>
        None = 0,
        /// <summary>A hold is in force: the caller treats the pad count as unstable and does not capture.</summary>
        Held = 1,
        /// <summary>A session hold ended because every pad is gone; all history is cleared.</summary>
        Released = 2,
        /// <summary>A timed hold ran out on this call; the caller retries capture once.</summary>
        Expired = 4,
        /// <summary>This call latched a hold (always with <see cref="Held"/>).</summary>
        Latched = 8,
        /// <summary>With <see cref="Latched"/>: the session's second latch, which has no expiry.</summary>
        ForSession = 16,
    }

    private readonly Queue<long> _tripStamps = new();
    private bool _guardWasOn;
    private int _latches;
    private long _holdUntilMs;

    /// <summary>When the current hold ends: 0 = not held, <see cref="long.MaxValue"/> = held for the session.</summary>
    public long HoldUntilMs => _holdUntilMs;

    /// <summary>Latches since the last release or <see cref="Reset"/>.</summary>
    public int Latches => _latches;

    /// <summary>Guard-ON edges recorded toward the next latch (aged out only when the next edge arrives).</summary>
    public int PendingTrips => _tripStamps.Count;

    /// <summary>Feed one capture pass. <paramref name="guardOn"/> is whether the multi-pad guard is on this pass
    /// (only a complete device sweep may call this: an empty list from a failed sweep reads as every pad gone);
    /// <paramref name="padCount"/> is the physical Xbox-class pads listed across both transports.
    /// A guard-ON edge is a pass with the guard on after a pass with it off; passes that run inside a hold do
    /// not track the guard.</summary>
    public Verdict Observe(long nowMs, bool guardOn, int padCount)
    {
        var verdict = Verdict.None;

        if (_holdUntilMs == long.MaxValue && padCount <= 0)
        {
            Reset();
            verdict |= Verdict.Released;
        }

        if (_holdUntilMs != 0)
        {
            if (nowMs < _holdUntilMs) return verdict | Verdict.Held;
            _holdUntilMs = 0;
            _tripStamps.Clear();
            verdict |= Verdict.Expired;
        }

        if (guardOn && !_guardWasOn)
        {
            _tripStamps.Enqueue(nowMs);
            while (_tripStamps.Count > 0 && nowMs - _tripStamps.Peek() > ChurnWindowMs)
                _tripStamps.Dequeue();
            if (_tripStamps.Count >= ChurnTrips)
            {
                _latches++;
                bool session = _latches >= 2;
                _holdUntilMs = session ? long.MaxValue : nowMs + ChurnHoldMs;
                _tripStamps.Clear();
                _guardWasOn = guardOn;
                return verdict | Verdict.Held | Verdict.Latched | (session ? Verdict.ForSession : Verdict.None);
            }
        }

        _guardWasOn = guardOn;
        return verdict;
    }

    /// <summary>Forget all history: a release, or a capture profile change (the next episode starts clean).</summary>
    public void Reset()
    {
        _holdUntilMs = 0;
        _latches = 0;
        _tripStamps.Clear();
        _guardWasOn = false;
    }
}
