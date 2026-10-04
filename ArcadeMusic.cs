using System;
using System.Collections.Generic;
using System.Linq;

namespace ControllerWheel;

/// <summary>The Arcade's music beds: one track at a time, streamed through <see cref="SfxEngine"/>'s music
/// channel so it shares the app's single bus and master limiter. On by default and switchable per game — the
/// switch is a row the HOST adds to every pause menu (<c>ArcadeControl.PauseOptions</c>), stored in
/// <c>ArcadeStore</c> beside the high score, so no game's sim knows music exists.
///
/// <para><b>Two policies</b>, because the games want different things:</para>
/// <list type="bullet">
/// <item><b>Loop</b> — one track, repeating: Petalpop and Connate.</item>
/// <item><b>Playlist</b> — each track plays out in full, then the next, wrapping: Kabloom and Internode. The
/// tracks are NOT tied to levels; a board or a stage change never interrupts one.</item>
/// </list>
///
/// <para><b>Fail-quiet.</b> The tracks are CC BY-SA 4.0 (THIRD-PARTY-LICENSES.md §4d) and a build may carry
/// none, so a missing resource plays as silence exactly like a missing cue. A game
/// with no bed offers no music row at all rather than a switch that does nothing.</para></summary>
internal static class ArcadeMusic
{
    /// <summary>⚠ There is no KEYED policy. A bed must not track the stage or level — Internode's simply
    /// cycles as the run goes. Don't reintroduce a `key` argument threaded through the host to feed one: an
    /// unused policy and an unused parameter are exactly the kind of thing that gets wired back up by
    /// accident.</summary>
    private enum Policy { Loop, Playlist }

    private sealed record Bed(Policy Policy, string[] Tracks);

    /// <summary>File names under <c>Assets\music\</c>, without extension — the creator's own titles, kept
    /// as given. ⚠ The order of a Playlist bed IS the cycle; a new entry changes what plays when.</summary>
    private static readonly Dictionary<string, Bed> Beds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["petalpop"]  = new(Policy.Loop,     ["I Miss Toonami"]),
        ["connate"]   = new(Policy.Loop,     ["001 - ARTIFICIAL INCOHERENCE"]),
        ["kabloom"]   = new(Policy.Playlist, ["A NEON RAIN THAT NEVER ENDS", "Eventual Consistency"]),
        ["internode"] = new(Policy.Playlist, ["Eudaimonia", "P01s0n.p1ll", "decoupl.3d"]),
    };

    /// <summary>Does this game have a bed? Gates the pause row — a switch with nothing behind it is worse
    /// than no switch.</summary>
    public static bool HasBed(string? gameId)
        => gameId is not null && Beds.TryGetValue(gameId, out var bed) && TrackAvailable(bed.Tracks[0]);

    /// <summary>Is the track actually compiled into this build? The public mirror ships no music by design,
    /// and a bed whose resource is missing must neither offer its pause row nor be re-requested — a missing
    /// resource costs an <c>IOException</c> per attempt, and <see cref="Sync"/> runs once per rendered frame.
    /// Answered once per track for the life of the process.</summary>
    private static readonly Dictionary<string, bool> Available = new(StringComparer.OrdinalIgnoreCase);

    private static bool TrackAvailable(string track)
    {
        if (Available.TryGetValue(track, out bool known)) return known;
        bool ok;
        try
        {
            using var s = System.Windows.Application.GetResourceStream(new System.Uri(Uri(track)))?.Stream;
            ok = s is not null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Music] track '{track}' is not in this build: {ex.Message}");
            ok = false;
        }
        Available[track] = ok;
        return ok;
    }

    /// <summary>Every track any bed names, for the harness's resource check.</summary>
    internal static readonly string[] AllTracks = Beds.Values.SelectMany(b => b.Tracks).Distinct().ToArray();

    // Live state. UI thread only, like the rest of the arcade host.
    private static string? _gameId;
    private static int _index = -1;
    private static bool _ducked;

    /// <summary>Where each game's bed was when it last stopped — the track and how far into it — so a game
    /// resumed later picks its music up from the same point, behind a short fade
    /// (<see cref="ArcadeSfxTuning.MusicResumeFadeMs"/>). Session-scoped, like the live game objects: a
    /// fresh process starts every bed from the top.</summary>
    private sealed record Spot(int Index, double Seconds);
    private static readonly Dictionary<string, Spot> Spots = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>The remembered position to start the NEXT track from; consumed by the start.</summary>
    private static double _resumeAt;

    /// <summary>Bring the bed in line with the game on screen, once per frame. Cheap when nothing changes:
    /// the work is one dictionary lookup and an index compare.
    ///
    /// <para><paramref name="ducked"/> drops the level while a menu or card is up.</para></summary>
    public static void Sync(string? gameId, bool on, bool ducked)
    {
        if (!on || gameId is null || !Beds.TryGetValue(gameId, out var bed))
        {
            Stop();
            return;
        }

        // A different game means a different bed: pick up where this game's bed was left, or its first track
        // from the top if it has never played.
        if (!string.Equals(gameId, _gameId, StringComparison.OrdinalIgnoreCase))
        {
            _gameId = gameId;
            if (Spots.TryGetValue(gameId, out var spot) && spot.Index >= 0 && spot.Index < bed.Tracks.Length)
            {
                _index = spot.Index;
                _resumeAt = spot.Seconds;
            }
            else
            {
                _index = -1;
                _resumeAt = 0;
            }
        }

        int wanted = bed.Policy switch
        {
            // A finished track hands over to the next; anything else keeps the one that is playing.
            Policy.Playlist when _index >= 0 && SfxEngine.MusicEnded => (_index + 1) % bed.Tracks.Length,
            _ => _index < 0 ? 0 : _index,
        };

        if (wanted != _index || !SfxEngine.MusicActive)
        {
            // A track handing over to the next always starts from its top; only the remembered track resumes.
            double from = wanted == _index ? _resumeAt : 0;
            _index = wanted;
            _resumeAt = 0;
            // A track this build does not carry is skipped silently, once — not retried every frame.
            if (!TrackAvailable(bed.Tracks[_index])) return;
            SfxEngine.PlayMusic(Uri(bed.Tracks[_index]), (float)ArcadeSfxTuning.MusicGain,
                                loop: bed.Policy != Policy.Playlist,
                                fadeInMs: from > 0 ? ArcadeSfxTuning.MusicResumeFadeMs : ArcadeSfxTuning.MusicStartFadeMs,
                                startSeconds: from);
            _ducked = false;                 // the new track fades in at full; ducking re-applies below
        }

        if (ducked != _ducked)
        {
            _ducked = ducked;
            SfxEngine.SetMusicGain(ducked ? (float)ArcadeSfxTuning.MusicDuck : 1f);
        }
    }

    /// <summary>Fade the bed out and remember where it was, so this game's next open picks it up there. A
    /// track that had already run out hands the spot to the next one, from its top.</summary>
    public static void Stop()
    {
        if (_gameId is null && _index < 0) return;
        if (_gameId is not null && _index >= 0 && Beds.TryGetValue(_gameId, out var bed))
        {
            bool ended = SfxEngine.MusicEnded;
            double at = SfxEngine.StopMusic();
            Spots[_gameId] = ended || at < 0
                ? new Spot((_index + 1) % bed.Tracks.Length, 0)
                : new Spot(_index, at);
        }
        else SfxEngine.StopMusic();
        _gameId = null;
        _index = -1;
        _resumeAt = 0;
        _ducked = false;
    }

    /// <summary>Titles carry spaces and punctuation, so the pack path is escaped per segment rather than
    /// assembled by hand.</summary>
    private static string Uri(string track) =>
        $"pack://application:,,,/Assets/music/{System.Uri.EscapeDataString(track)}.m4a";
}
