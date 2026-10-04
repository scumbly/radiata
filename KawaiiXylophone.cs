namespace ControllerWheel;

/// <summary>Kawaii's slice-ARMED sound: instead of one fixed chime, arming plays the next note of a
/// short melody, so scrubbing the wheel "plays" a tune. Each wheel open
/// (<see cref="WheelOpened"/>) rotates to the next of the five <see cref="Songs"/> in fixed order —
/// never randomly — and restarts it at note 1; every subsequent arm advances one note and wraps at the end.
/// Slice ORDER is irrelevant — the note sequence is purely "how many times have you armed something".
/// Only the Kawaii theme uses this; fired / wheels-on-off stay on <see cref="Sfx"/>'s plain samples.
///
/// <para><b>Pitch.</b> The eight shipped samples (Assets\sfx\xylophone) are a C-major octave, C6…C7 —
/// no accidentals — while the songs need sharps, flats and lower registers, so every note is played by
/// resampling the nearest sample. Playback, mixing and the master ceiling all belong to
/// <see cref="SfxEngine"/>; this class only decides which sample and at what ratio.</para>
///
/// <para><b>Register.</b> The songs must stay RECOGNIZABLE as transcribed while all sitting in one
/// common register — so each carries its own <c>Octaves</c> transposition,
/// chosen as the whole-octave shift that lands its mean pitch nearest 90 (F#6, the middle of the
/// C6–C7 sample band). Whole octaves only, so no interval is ever altered and the melody survives
/// intact; the shifted means all land within 87–92. Recompute the shift if a transcription changes
/// enough to move its mean by half an octave.</para>
///
/// <para>Fail-quiet everywhere, like the rest of the sound stack: no audio device, a missing resource or
/// a device change mid-play must never break input — arming simply goes silent.</para></summary>
internal static class KawaiiXylophone
{
    /// <summary>Peak level each note is mixed at, 0–1. The samples are peak-normalized to
    /// <see cref="NormalizePeak"/> first, so every note lands at the same loudness regardless of which
    /// source WAV it came from; this constant is then trimmed by ear — overlapping notes stack, so
    /// peak-matching a single strike runs hot. Single knob — raise/lower here if the melody sits wrong
    /// against the fired sound. Stacks with <c>SfxEngine.MasterGain</c>, the app-wide trim.</summary>
    private const float MasterGain = 0.125f;

    /// <summary>Every sample is scaled so its loudest peak is this, evening out the source set (the raw
    /// WAVs range ~0.38–0.69 FS, which made C7 noticeably quieter than D6 mid-melody).</summary>
    private const float NormalizePeak = 0.55f;

    /// <summary>The shipped samples, as (file, MIDI note). C4 = 60, so C6 = 84.</summary>
    private static readonly (string File, int Midi)[] Samples =
    {
        ("C6", 84), ("D6", 86), ("E6", 88), ("F6", 89), ("G6", 91), ("A6", 93), ("B6", 95), ("C7", 96),
    };

    /// <summary>The five melodies, in written pitch, each with the whole-octave transposition applied
    /// at load (<c>Octaves</c>: 0 = play as written). Flats are written "b". Rotation follows this
    /// order exactly.</summary>
    private static readonly (string Name, int Octaves, string Notes)[] Songs =
    {
        ("Nerv",     0, "C6 D#6 F6 D#6 F6 F6 A#6 G#6 G6 F6 G6 G6 A#6 C7 F7 D#7 A#6 G6 A#6 A#6 C7"),
        // Moon's 4th note is written C♭5 — the parser resolves it enharmonically to B4, a step above the
        // B♭4 before it, which is the neighbour tone intended.
        ("Moon",    +2, "Eb5 D5 Bb4 Cb5 Ab4 G4 G4 G4 G4 F4 F4 Eb4 D4 F4 Bb3 D4 F4 Ab4 Ab4 Bb4 Ab4 G4 F4 Eb4 F4 Eb4 D4 F4 Eb4"),
        ("Run",     +2, "C#4 F#4 G#4 A4 G#4 F#4 C#4 F#4 G#4 A4 B4 A4 B4 C#5 C#4 F#4 G#4 A4 G#4 F#4 D5 C#5 B4 A4 G#4 A4 G#4 F#4"),
        ("Twinkle", +2, "C4 C4 G4 G4 A4 A4 G4 F4 F4 E4 E4 D4 D4 C4"),
        ("Sunshine",+1, "G4 C5 D5 E5 E5 E5 D5 E5 C5 C5 C5 D5 E5 F5 A5 A5 G5 F5 E5 C5 D5 E5 F5 A5 A5 G5 F5 E5 C5 C5 D5 E5 C5 D5 B4 G4 C5"),
    };

    // Sequencer position. Both are touched only from the UI dispatcher (wheel open / arm), so no lock.
    // _song starts at -1 so the FIRST wheel open lands on song 0 (Nerv).
    private static int _song = -1;
    private static int _note;
    private static long _lastNoteAt;   // TickCount64 of the last note struck — the debounce reference

    /// <summary>Two arms closer together than this (ms) count as ONE musical event: aiming exactly at a
    /// slice boundary can flutter across it, arming both slices within a frame or two, and two melody
    /// notes landing near-simultaneously reads as a mistake rather than a fast scrub. The debounced arm
    /// neither plays NOR advances the song — as if it hadn't happened — so the melody doesn't silently
    /// skip a note. Deliberately far below any intentional scrub interval (even a frantic stick sweep
    /// arms slices ~80 ms+ apart).</summary>
    private const int DebounceMs = 50;

    /// <summary>A wheel bloomed in: rotate to the next song (in order, wrapping) and rewind it to note 1.
    /// Called on every real wheel open regardless of theme, so switching to Kawaii mid-session starts a
    /// song cleanly rather than mid-phrase.</summary>
    public static void WheelOpened()
    {
        if (_parsed.Length == 0) return;
        _song = (_song + 1) % _parsed.Length;
        _note = 0;
    }

    /// <summary>A slice armed: play this song's current note and advance (wrapping at the end).</summary>
    public static void ArmedNote()
    {
        if (_parsed.Length == 0) return;
        if (_song < 0) _song = 0;   // armed before any WheelOpened (grid-only session): start at song 1
        var song = _parsed[_song];
        if (song.Length == 0) return;
        long now = Environment.TickCount64;
        if (now - _lastNoteAt < DebounceMs) return;   // boundary flutter — one event, one note
        _lastNoteAt = now;
        int midi = song[_note % song.Length];
        _note = (_note + 1) % song.Length;
        PlayNote(midi);
    }

    // ── Songs → MIDI ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Each song as transposed MIDI notes. Empty if a song failed to parse (fail-quiet).</summary>
    private static readonly int[][] _parsed = ParseSongs();

    private static int[][] ParseSongs()
    {
        var result = new int[Songs.Length][];
        for (int i = 0; i < Songs.Length; i++)
        {
            try
            {
                int shift = Songs[i].Octaves * 12;
                result[i] = Songs[i].Notes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(n => ParseNote(n) + shift).ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Xylophone] song '{Songs[i].Name}' unparseable: {ex.Message}");
                result[i] = Array.Empty<int>();
            }
        }
        return result;
    }

    /// <summary>Scientific pitch name → MIDI number ("C4" = 60). Accepts # and b; the octave may be
    /// negative ("C-1").</summary>
    private static int ParseNote(string name)
    {
        int step = char.ToUpperInvariant(name[0]) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new FormatException($"bad letter in '{name}'"),
        };
        int i = 1;
        for (; i < name.Length && (name[i] == '#' || name[i] == 'b' || name[i] == '♯' || name[i] == '♭'); i++)
            step += name[i] is '#' or '♯' ? 1 : -1;
        if (!int.TryParse(name.AsSpan(i), out int octave)) throw new FormatException($"bad octave in '{name}'");
        return (octave + 1) * 12 + step;   // MIDI 0 = C-1
    }

    // ── Audio ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Strike the nearest sample, pitched to <paramref name="midi"/>. Mixing, the master
    /// ceiling and the device's lifetime all belong to <see cref="SfxEngine"/> — this only picks the
    /// sample and the ratio.</summary>
    private static void PlayNote(int midi)
    {
        if (!Sfx.Enabled) return;

        // Nearest sample, then resample away the difference. Inside the C6–C7 band that is at most a
        // semitone; the transposed songs stray a few semitones outside it at their extremes (Nerv tops
        // at F7, Sunshine bottoms at G5), which a mallet strike takes cleanly.
        int best = 0;
        for (int i = 1; i < Samples.Length; i++)
            if (Math.Abs(Samples[i].Midi - midi) < Math.Abs(Samples[best].Midi - midi)) best = i;

        double ratio = Math.Pow(2.0, (midi - Samples[best].Midi) / 12.0);
        SfxEngine.Play(Sample(best), MasterGain, ratio);
    }

    /// <summary>One xylophone strike, decoded and peak-normalized on first use (the engine caches it).
    /// Normalizing evens out the source set, whose eight WAVs peak between 0.38 and 0.69 FS — without it
    /// C7 lands noticeably quieter than D6 mid-melody.</summary>
    private static float[] Sample(int index) =>
        SfxEngine.LoadResource($"pack://application:,,,/Assets/sfx/xylophone/{Samples[index].File}.wav", NormalizePeak);
}