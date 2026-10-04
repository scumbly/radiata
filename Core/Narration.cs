namespace ControllerWheel;

// ── Narration: the overlay speaks for itself ──────────────────────────────────────────────────────────────────
// The overlay's voice is Radiata's own speech engine, never Narrator — don't route it back through UIA:
// Windows will not service UIA notifications or live-region events from the overlay's never-focused tool
// window. The Announcer here is the WPF-free coordination layer:
// prioritise, coalesce rapid selection changes, and keep stale speech from landing after the context is
// gone. The actual voice is behind ISpeechSink (SpeechSink in the app project), so this whole layer is
// unit-testable in Core.

/// <summary>What kind of announcement — decides how it competes with other speech.</summary>
public enum AnnouncementKind
{
    /// <summary>The situation changed: a wheel opened, a mode began, a notice appeared. Interrupts
    /// everything, including queued results.</summary>
    Context,
    /// <summary>An action's outcome: "HDR On", a failure, a suppression. Interrupts selection chatter
    /// but is never itself dropped by later selection changes.</summary>
    Result,
    /// <summary>The armed slice / focused item changed. Debounced: rapid stick noise collapses to the
    /// final stable selection, and only the newest pending selection ever speaks.</summary>
    Selection,
    /// <summary>A continuously-scrubbed value ("Volume 55 percent"). Selection with a longer window: a
    /// held D-pad repeats about every 80 ms, so a selection-length debounce still lands mid-sweep and
    /// reads as a machine-gun of numbers. Speaks the value the scrub settles on.</summary>
    Scrub,
}

/// <summary>The voice. Implementations must tolerate calls from any thread.</summary>
public interface ISpeechSink
{
    /// <summary><paramref name="interrupt"/> false must queue behind what is already speaking, not talk
    /// over it — the Announcer relies on that to let a result finish before the next line starts.</summary>
    void Speak(string text, bool interrupt);
    /// <summary>Stop speaking and drop anything queued in the sink.</summary>
    void Stop();
    /// <summary>True while anything handed to the sink is still being spoken or is queued behind it.</summary>
    bool IsSpeaking { get; }
}

/// <summary>Prioritising, coalescing front door for all narration. One instance app-wide; App owns the
/// enabled flag and the sink. Thread-tolerant: announcements may arrive from the UI thread while the
/// debounce timer fires on a pool thread.</summary>
public sealed class Announcer : IDisposable
{
    private readonly object _lock = new();
    private readonly System.Threading.Timer _debounce;
    // A debounce tick already in flight when this is cleared re-reads it under the lock and finds null,
    // so a stale selection can never speak after a Reset.
    private string? _pendingSelection;
    private int _pendingWindowMs;   // which window the pending item was queued under, for the re-check below
    // True when what the sink is currently working through is a Result/Context rather than selection
    // chatter. Combined with Sink.IsSpeaking this is what keeps consecutive results from cutting each
    // other off: an instruction ("Picked up Steam, aim to a new position") is always followed within
    // milliseconds by the focus change it caused, and without this the follow-up wins every time.
    private bool _importantInFlight;

    /// <summary>Master switch (the "Narrate wheel and Game Grid" setting). Off = every call is a no-op.</summary>
    public bool Enabled { get; set; }

    /// <summary>The voice to speak through. Null = coordinate silently (useful under test).</summary>
    public ISpeechSink? Sink { get; set; }

    /// <summary>How long a selection must hold still before it's spoken. Long enough to swallow stick
    /// noise sweeping across slices, short enough that narration doesn't feel laggy.</summary>
    public int SelectionDebounceMs { get; set; } = 180;

    /// <summary>The same idea for <see cref="AnnouncementKind.Scrub"/>, but sized against a held D-pad's
    /// ~80 ms auto-repeat rather than stick noise: it has to outlast the gaps within a sweep, or the value
    /// is read out over and over while the user is still moving it.</summary>
    public int ScrubDebounceMs { get; set; } = 500;

    public Announcer() => _debounce = new System.Threading.Timer(OnDebounce);

    public void Announce(string text, AnnouncementKind kind)
    {
        if (!Enabled || Sink is null || string.IsNullOrWhiteSpace(text)) return;
        lock (_lock)
        {
            switch (kind)
            {
                case AnnouncementKind.Selection:
                case AnnouncementKind.Scrub:
                    // Replace, never queue: only the newest selection matters. The timer restart is the
                    // coalescing — a sweep across five slices speaks once, at the one it settles on.
                    _pendingSelection = text;
                    _pendingWindowMs = kind == AnnouncementKind.Scrub ? ScrubDebounceMs : SelectionDebounceMs;
                    _debounce.Change(_pendingWindowMs, System.Threading.Timeout.Infinite);
                    return;
                case AnnouncementKind.Result:
                    // A result supersedes selection chatter (the fired slice was just announced as a
                    // selection; repeating it as preamble buries the outcome) but queues behind another
                    // result — two outcomes in a row are both worth hearing, in order.
                    ClearPendingLocked();
                    Sink.Speak(text, interrupt: !(_importantInFlight && Sink.IsSpeaking));
                    _importantInFlight = true;
                    return;
                case AnnouncementKind.Context:
                    // The situation itself changed, so anything still being said is about a context that
                    // no longer applies: this one alone cuts results off.
                    ClearPendingLocked();
                    Sink.Speak(text, interrupt: true);
                    _importantInFlight = true;
                    return;
            }
        }
    }

    /// <summary>The context is gone (wheel closed, mode ended): drop any un-spoken selection and cut off
    /// selection speech mid-word. Results that were already handed to the sink are not recalled — an
    /// action's outcome is still true after the wheel closes.</summary>
    public void Reset(bool stopSpeech = false)
    {
        lock (_lock)
        {
            ClearPendingLocked();
            if (stopSpeech) { Sink?.Stop(); _importantInFlight = false; }
        }
    }

    private void ClearPendingLocked()
    {
        _pendingSelection = null;
        _debounce.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
    }

    private void OnDebounce(object? state)
    {
        string? text;
        lock (_lock)
        {
            text = _pendingSelection;
            if (text is null || !Enabled || Sink is null) { _pendingSelection = null; return; }
            // A result is still being spoken: hold the selection back and re-check rather than either
            // talking over the outcome or dropping the selection on the floor. Sweeping slots while an
            // instruction plays must still read out the slot you land on, just after the voice frees up.
            if (_importantInFlight && Sink.IsSpeaking)
            {
                _debounce.Change(_pendingWindowMs, System.Threading.Timeout.Infinite);
                return;
            }
            _pendingSelection = null;
            _importantInFlight = false;
        }
        Sink?.Speak(text, interrupt: true);
    }

    public void Dispose() => _debounce.Dispose();
}
