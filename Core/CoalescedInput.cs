namespace ControllerWheel;

/// <summary>Keep the latest analog sample between input boundaries. Sealing preserves the last
/// sample preceding a button/touch/connection event, so later input cannot change that event's aim.</summary>
public sealed class CoalescedInput<T>(Action<Action> post, Action<T> consume)
{
    private sealed class Batch(T value) { public T Value = value; }
    private readonly object _gate = new();
    private Batch? _pending;

    public void Submit(T value)
    {
        lock (_gate)
        {
            if (_pending is { } pending) { pending.Value = value; return; }
            var batch = new Batch(value);
            _pending = batch;
            try
            {
                post(() =>
                {
                    T latest;
                    lock (_gate)
                    {
                        latest = batch.Value;
                        if (ReferenceEquals(_pending, batch)) _pending = null;
                    }
                    consume(latest);
                });
            }
            catch { if (ReferenceEquals(_pending, batch)) _pending = null; throw; }
        }
    }

    public void Seal() { lock (_gate) _pending = null; }
}
