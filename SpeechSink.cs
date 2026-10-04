using System.Speech.Synthesis;

namespace ControllerWheel;

/// <summary>The narration voice: Windows' built-in SAPI synthesizer on the default output device. It is
/// the primary channel for the overlay — Narrator can't service a never-focused tool window.
/// <para>Volume is the synthesizer's own 0–100 and must stay independent of <c>SfxEngine</c>'s bus (quiet
/// chimes + loud speech is a supported combination). Construction must stay lazy at the call site (App):
/// SAPI init costs ~100 ms and loads a voice, which nobody who leaves narration off should pay.</para>
/// <para>Thread-tolerant per <see cref="ISpeechSink"/>: SpeakAsync/SpeakAsyncCancelAll are safe from any
/// thread, and the Announcer's debounce timer calls in from a pool thread.</para></summary>
public sealed class SpeechSink : ISpeechSink, IDisposable
{
    private readonly SpeechSynthesizer _synth;

    /// <summary>Whether an installed voice speaks the run's language. False = narration is suppressed rather than
    /// read by a voice for another language, which produces spelled-out noise; the caller tells the user once
    /// where the Windows language pack lives.</summary>
    public bool HasVoice { get; }

    public SpeechSink(string languageCode)
    {
        _synth = new SpeechSynthesizer();
        _synth.SetOutputToDefaultAudioDevice();
        _synth.Rate = Rate;
        HasVoice = SelectVoice(languageCode);
    }

    /// <summary>Speaking rate on SAPI's own scale: an integer −10…+10 where 0 is the voice's default, not a
    /// multiplier. Each step is roughly 1.15–1.2×, so +1 is the nearest thing to the "about 15% faster"
    /// this was tuned to — the API cannot express a literal 1.15×.
    /// <para>⚠ Judge any change by ear against the longest lines (the edit-mode button legend, the
    /// hide-confirm prompt): a faster rate helps most there and costs intelligibility there first.</para></summary>
    private const int Rate = 1;

    /// <summary>0–100 (SAPI's own scale). Set from <c>SystemConfig.NarrationVolume</c>.</summary>
    public int Volume
    {
        get => _synth.Volume;
        set => _synth.Volume = Math.Clamp(value, 0, 100);
    }

    /// <summary>SAPI's own async queue is the queue: skipping the cancel makes this line follow the one
    /// already in progress instead of cutting it.</summary>
    public bool IsSpeaking => _synth.State == SynthesizerState.Speaking;

    /// <summary>Pick an installed voice for the language (any gender/age); English falls back to whatever SAPI
    /// defaults to, which is the behaviour every English install has today. Returns whether a match exists.</summary>
    private bool SelectVoice(string languageCode)
    {
        try
        {
            var installed = _synth.GetInstalledVoices()
                .Where(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (installed.Count == 0)
                return languageCode.Equals(HelpLocalization.DefaultCode, StringComparison.OrdinalIgnoreCase);
            _synth.SelectVoice(installed[0].VoiceInfo.Name);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Narration] voice select failed ({languageCode}): {ex.Message}");
            return languageCode.Equals(HelpLocalization.DefaultCode, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Speak(string text, bool interrupt)
    {
        try
        {
            if (!HasVoice) return;   // no voice for this language: silence beats gibberish
            if (interrupt) _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            // Narration must never take the app down — a missing voice/device just logs.
            System.Diagnostics.Trace.WriteLine($"[Narration] speak failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        try { _synth.SpeakAsyncCancelAll(); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Narration] stop failed: {ex.Message}"); }
    }

    public void Dispose() => _synth.Dispose();
}
