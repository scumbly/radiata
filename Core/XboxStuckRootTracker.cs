using System;
using System.Collections.Generic;

namespace ControllerWheel;

/// <summary>Separates a USB Xbox-class composite root that has not yet grown its input children (normal for a
/// moment after arrival) from one that never will. A root that stays childless past <see cref="SettleMs"/> is
/// STUCK: Windows lists the controller but its XInput/HID children were never created, which only a power-cycle
/// of the controller cures. A stuck or still-settling root is not a pad — nothing is cloaked and it is not
/// counted.
/// <para>The settle clock starts the first time a sweep sees the root childless and resets when it gains a
/// child or leaves the tree. Pure: the caller supplies the clock; thread-safe.</para></summary>
public sealed class XboxStuckRootTracker
{
    public const int SettleMs = 10_000;

    public enum RootState
    {
        /// <summary>Has at least one child: an ordinary pad.</summary>
        Healthy,
        /// <summary>Childless for less than <see cref="SettleMs"/>: not yet a pad, not yet a fault.</summary>
        Settling,
        /// <summary>Childless for <see cref="SettleMs"/> or longer.</summary>
        Stuck,
    }

    public readonly record struct Entry(string Id, RootState State, long ChildlessMs);

    private readonly object _gate = new();
    private readonly Dictionary<string, long> _childlessSince = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Folds one complete sweep in. Roots absent from <paramref name="roots"/> are forgotten.</summary>
    public IReadOnlyList<Entry> Observe(long nowMs, IReadOnlyList<(string Id, bool HasChildren)> roots)
    {
        var result = new List<Entry>(roots.Count);
        lock (_gate)
        {
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, hasChildren) in roots)
            {
                present.Add(id);
                if (hasChildren)
                {
                    _childlessSince.Remove(id);
                    result.Add(new Entry(id, RootState.Healthy, 0));
                    continue;
                }
                if (!_childlessSince.TryGetValue(id, out long since)) _childlessSince[id] = since = nowMs;
                long age = Math.Max(0, nowMs - since);
                result.Add(new Entry(id, age >= SettleMs ? RootState.Stuck : RootState.Settling, age));
            }
            foreach (var id in new List<string>(_childlessSince.Keys))
                if (!present.Contains(id)) _childlessSince.Remove(id);
        }
        return result;
    }
}
