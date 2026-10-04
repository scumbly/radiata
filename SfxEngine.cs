using System;
using System.Collections.Concurrent;
using System.Windows;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ControllerWheel;

/// <summary>The shared audio output behind every Radiata sound effect: one always-mixed bus, so sounds
/// OVERLAP instead of cutting each other off.
///
/// <para><b>Runaway protection.</b> Overlap means a fast slice-walk can stack a dozen voices, so the bus
/// ends in <see cref="MasterLimiter"/> — a hard ceiling at <see cref="Ceiling"/> that ducks instantly
/// and releases over ~250 ms, then a clipping 16-bit conversion as a backstop. A burst therefore gets
/// quieter, never louder or distorted, and <see cref="MaxVoices"/> caps how many can pile up at all.</para>
///
/// <para><b>Device lifetime.</b> The output device is opened on the first sound and released again after
/// <see cref="IdleCloseMs"/> of silence, so a tray app that's been quiet for a minute isn't sitting on
/// the audio endpoint — which on a couch rig would keep a TV/receiver awake and park Radiata in the
/// volume mixer forever. <see cref="Prime"/> re-opens it ahead of an expected burst (called on every
/// wheel open) so re-opening never delays the first tick.</para>
///
/// <para>Every path is fail-quiet: no audio device, a missing resource, or a device change mid-play
/// must never break input. Repeated failures disable the engine permanently rather than throw on
/// every arm.</para></summary>
internal static class SfxEngine
{
    /// <summary>Mix bus format. 44.1 kHz stereo float — the format the shipped sfx are already in, so
    /// only the 24 kHz xylophone samples are resampled (once, at load).</summary>
    private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    /// <summary>Master gain applied to every voice, on top of whatever per-sound gain the caller passes —
    /// the single app-wide volume trim. Sits at the bus so no sound can forget it. <see cref="Ceiling"/>
    /// is kept in proportion to it, so how dense a stack has to be before the limiter engages doesn't
    /// drift as this is tuned.</summary>
    private const float MasterGain = 0.75f;

    /// <summary>Peak the master limiter holds the bus to. Well below 1.0, so the 16-bit conversion never
    /// has to clip and a dense stack of voices is ducked long before it gets loud. Move it in proportion
    /// to <see cref="MasterGain"/>, which holds the voice count that triggers limiting constant.</summary>
    private const float Ceiling = 0.82f;

    /// <summary>How long the limiter takes to give the gain back after a peak (ms). Long enough not to
    /// pump audibly between notes, short enough that one loud stack doesn't mute the next second.</summary>
    private const double ReleaseMs = 250;

    /// <summary>Hard cap on simultaneous voices — a frantic scrub drops the oldest rather than growing
    /// the mixer without bound.</summary>
    private const int MaxVoices = 16;

    /// <summary>Silence after which the output device is released. Re-opened on the next sound (or by
    /// <see cref="Prime"/>).</summary>
    private const int IdleCloseMs = 20_000;

    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, float[]> Cache = new();

    private static IWavePlayer? _out;
    private static MixingSampleProvider? _mixer;
    private static MasterLimiter? _limiter;
    private static System.Threading.Timer? _idleTimer;

    /// <summary>A device failure parks SFX until this tick (0 = healthy), then the next Play/Prime
    /// retries a full re-init through the normal cold-start path — one waveOutOpen attempt per
    /// cooldown, only when a sound is actually requested, never a spin. Without the cooldown-then-retry,
    /// one transient failure (autostart racing a sleeping TV/AVR endpoint, an endpoint-less RDP session)
    /// would silence the whole session with no way back but a restart.</summary>
    private static long _retryAtMs;
    private const int RetryCooldownMs = 30_000;

    /// <summary>Voices currently in the mixer. Counted here rather than read off
    /// <c>MixingSampleProvider.MixerInputs</c>, which exposes the live source list — enumerating it
    /// while the audio thread drops a finished voice would throw.</summary>
    private static int _voices;
    /// <summary>Voices refused at the cap since start (see AddVoice); a listening pass reads it to prove a cue storm
    /// never reaches silence.</summary>
    internal static int DroppedVoices;
    private static long _lastDropTraceMs;

    /// <summary>Decode a packed WAV resource to the mix format, cached forever after the first call.
    /// Mono is duplicated to stereo and any other sample rate is resampled here, so playback never has
    /// to. <paramref name="normalizePeak"/> &gt; 0 scales the whole clip so its loudest peak is that —
    /// used by the xylophone set, whose eight samples differ in level. Returns an empty array on any
    /// failure (a silent sound beats a crash).</summary>
    public static float[] LoadResource(string packUri, float normalizePeak = 0f)
    {
        string key = $"{packUri}|{normalizePeak}";
        return Cache.GetOrAdd(key, _ =>
        {
            try
            {
                var res = Application.GetResourceStream(new Uri(packUri));
                if (res is null) return Array.Empty<float>();
                using var stream = res.Stream;
                using var reader = new WaveFileReader(stream);

                ISampleProvider provider = reader.ToSampleProvider();
                if (provider.WaveFormat.Channels == 1) provider = new MonoToStereoSampleProvider(provider);
                if (provider.WaveFormat.SampleRate != MixFormat.SampleRate)
                    provider = new WdlResamplingSampleProvider(provider, MixFormat.SampleRate);

                var buffer = new List<float>((int)reader.Length / 2);
                var chunk = new float[8192];
                int read;
                while ((read = provider.Read(chunk, 0, chunk.Length)) > 0)
                    buffer.AddRange(chunk.Take(read));
                var samples = buffer.ToArray();

                if (normalizePeak > 0f)
                {
                    float peak = 0f;
                    foreach (var s in samples) peak = Math.Max(peak, Math.Abs(s));
                    if (peak > 0.0001f)
                    {
                        float scale = normalizePeak / peak;
                        for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
                    }
                }
                return samples;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Sfx] resource {packUri} unusable: {ex.Message}");
                return Array.Empty<float>();
            }
        });
    }

    /// <summary>Decode a package WAV FILE to the mix format — the drop-in themes' sound path
    /// (docs/PACKAGES.md). Same chain as <see cref="LoadResource"/> (WaveFileReader → mono→stereo →
    /// resample), so the buffer enters <see cref="Play"/> complete and format-true — the mixer's
    /// short-read hazard structurally can't occur. WAV only, hard-capped at <paramref name="maxSeconds"/>
    /// of mixed samples (the file size was already capped at scan). Cached per path for the run (a
    /// registered package's content never changes within a run — PackageInstallFlow). Empty array on any failure — silence beats a crash.</summary>
    public static float[] LoadFile(string path, double maxSeconds = 3.0)
    {
        return Cache.GetOrAdd("file|" + path, _ =>
        {
            try
            {
                using var stream = System.IO.File.OpenRead(path);
                using var reader = new WaveFileReader(stream);

                ISampleProvider provider = reader.ToSampleProvider();
                if (provider.WaveFormat.Channels == 1) provider = new MonoToStereoSampleProvider(provider);
                if (provider.WaveFormat.SampleRate != MixFormat.SampleRate)
                    provider = new WdlResamplingSampleProvider(provider, MixFormat.SampleRate);

                int cap = (int)(MixFormat.SampleRate * MixFormat.Channels * maxSeconds);
                var buffer = new List<float>(Math.Min(cap, 1 << 18));
                var chunk = new float[8192];
                int read;
                while (buffer.Count < cap && (read = provider.Read(chunk, 0, chunk.Length)) > 0)
                    buffer.AddRange(chunk.Take(Math.Min(read, cap - buffer.Count)));
                return buffer.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Sfx] package sound {path} unusable: {ex.Message}");
                return Array.Empty<float>();
            }
        });
    }

    /// <summary>Mix one voice in. <paramref name="pitchRatio"/> 1.0 plays at the recorded pitch; anything
    /// else resamples (2^(semitones/12)), which also scales the length — the xylophone's whole trick.</summary>
    public static void Play(float[] samples, float gain = 1f, double pitchRatio = 1.0)
    {
        if (samples.Length == 0 || SfxParked()) return;
        try
        {
            lock (Gate)
            {
                if (_mixer is null && !Start()) return;
                AddVoice(samples, gain, pitchRatio);
                BumpIdle();
            }
        }
        catch (Exception ex)
        {
            Fail("playback", ex);
        }
    }

    /// <summary>Mix a voice into the running device. Caller holds <see cref="Gate"/> and has started
    /// the mixer.</summary>
    private static void AddVoice(float[] samples, float gain, double pitchRatio)
    {
        if (System.Threading.Volatile.Read(ref _voices) >= MaxVoices)   // frantic scrub: drop, don't pile up
        {
            // Counted and traced (at most once a second) so a listening pass can prove a cue storm never
            // reaches the cap — a dropped voice is silent, and silence is the one failure nobody reports.
            DroppedVoices++;
            long now = Environment.TickCount64;
            if (now - _lastDropTraceMs >= 1000)
            {
                _lastDropTraceMs = now;
                System.Diagnostics.Trace.WriteLine($"[Sfx] voice cap hit ({DroppedVoices} dropped so far)");
            }
            return;
        }
        System.Threading.Interlocked.Increment(ref _voices);
        _mixer!.AddMixerInput((ISampleProvider)new Voice(samples, MixFormat, pitchRatio, gain * MasterGain, ReleaseVoice));
    }

    /// <summary>Open the output device now, ahead of an expected burst (a wheel opening), so the first
    /// sound isn't the one that pays for the device open. No-op if it's already up.
    ///
    /// <para><b>Wake-ahead.</b> When the open is COLD (the device had been torn down), the physical
    /// chain downstream — TV/AVR HDMI lock, soundbar/DAC auto-standby, codec amp power-down — takes
    /// ~100 ms–2 s to unmute after samples resume, and swallows whatever plays inside that window.
    /// A cold open therefore also queues a short sub-audible wake voice: the mixer's ReadFully zeros
    /// already give stream-triggered sinks their wake signal at open, but LEVEL-triggered standby
    /// detectors need a non-silent signal, which zeros never provide. Real sounds arriving 300 ms+
    /// after the prime (a wheel bloom + aiming time) then land on an awake chain. A slow AVR can still
    /// clip an instant first tick — the only complete cure is a keep-alive stream, rejected because a
    /// tray app must not hold the endpoint (see docs/SOUND.md ▸ Device lifetime).</para></summary>
    public static void Prime()
    {
        if (SfxParked()) return;
        try
        {
            lock (Gate)
            {
                bool cold = _mixer is null;
                if (cold && !Start()) return;
                if (cold) AddVoice(WakeSignal(), 1f, 1.0);
                BumpIdle();
            }
        }
        catch (Exception ex) { Fail("prime", ex); }
    }

    /// <summary>The cold-open wake voice: 300 ms of 40 Hz sine at −54 dBFS in the samples, ≈ −56.5 dBFS
    /// once AddVoice applies MasterGain. Low enough to be inaudible
    /// on consumer gear (and below what small drivers reproduce at all), non-zero enough to register on
    /// a level-triggered standby detector. Built once.</summary>
    private static float[]? _wake;
    private static float[] WakeSignal()
    {
        if (_wake is null)
        {
            int frames = (int)(MixFormat.SampleRate * 0.3);
            var buf = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                float s = 0.002f * (float)Math.Sin(2 * Math.PI * 40 * i / MixFormat.SampleRate);
                buf[i * 2] = s; buf[i * 2 + 1] = s;
            }
            _wake = buf;
        }
        return _wake;
    }

    /// <summary>Release the output device (app exit). Safe to call when nothing is open.</summary>
    public static void Shutdown()
    {
        System.Threading.Interlocked.Increment(ref _musicGen);   // a music start in flight must not re-open the device
        lock (Gate) Stop();
    }

    // ── device lifetime ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Open the device. Caller holds <see cref="Gate"/>.</summary>
    private static bool Start()
    {
        try
        {
            _mixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
            _limiter = new MasterLimiter(_mixer, Ceiling, ReleaseMs);
            // 16-bit out, not float: SampleToWaveProvider16 CLIPS, so anything that somehow slips past
            // the limiter distorts rather than wrapping around into a bang. Every device accepts PCM.
            var player = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 };
            player.Init(new SampleToWaveProvider16(_limiter));
            // NAudio never throws for a device that dies mid-session (HDMI renegotiation, an AVR cycle, the
            // app's own audio-switch action): its playback thread swallows the fault and raises this instead.
            // Without the hook the mixer stays "open" with nothing reading it, no voice ever short-reads, the
            // count pins at the cap and every later sound is dropped for the session — the permanent latch
            // _retryAtMs exists to prevent, by another door. Handled off the playback thread: Stop() disposes
            // the player that is raising the event.
            player.PlaybackStopped += (sender, e) => System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                lock (Gate)
                {
                    if (!ReferenceEquals(sender, _out)) return;   // a deliberate Stop() already replaced it
                    Stop();
                    if (e.Exception is { } ex) Fail("device lost", ex);
                    else System.Diagnostics.Trace.WriteLine("[Sfx] device stopped on its own — re-opening on the next sound");
                }
            });
            player.Play();
            _out = player;
            if (System.Threading.Volatile.Read(ref _retryAtMs) != 0)
            {
                System.Threading.Volatile.Write(ref _retryAtMs, 0);
                System.Diagnostics.Trace.WriteLine("[Sfx] device recovered — sound effects re-enabled");
            }
            return true;
        }
        catch (Exception ex)
        {
            Stop();
            Fail("device open", ex);
            return false;
        }
    }

    /// <summary>Tear the device down. Caller holds <see cref="Gate"/>.</summary>
    private static void Stop()
    {
        // ⚠ Traced, and only when a device was actually open. This is the one event that says Radiata has
        // LET GO of the audio endpoint, and without a line for it the question "is the tray app still
        // holding the device?" has no answer but a guess — which is exactly the complaint that arrives
        // hours later, about a receiver that will not idle or a stuck entry in the volume mixer.
        if (_out is not null)
            System.Diagnostics.Trace.WriteLine($"[Sfx] audio device released (music={_musicActive})");
        try { _out?.Stop(); } catch { /* already gone */ }
        try { _out?.Dispose(); } catch { /* already gone */ }
        try { _idleTimer?.Dispose(); } catch { /* already gone */ }
        // The bed dies with the mixer that was reading it, or it decodes into a graph that is gone.
        try { _music?.Dispose(); } catch { /* already gone */ }
        _out = null; _mixer = null; _limiter = null; _idleTimer = null;
        _music = null; _musicActive = false; _musicPending = false;
        System.Threading.Volatile.Write(ref _voices, 0);   // the mixer went with the device
    }

    /// <summary>Restart the idle countdown. Caller holds <see cref="Gate"/>.</summary>
    private static void BumpIdle()
    {
        // Fires only when nothing has been played or primed for the whole window. Every sound we ship is
        // under 3 s, so by then the bus is silent no matter what the voice count says — tearing down
        // unconditionally (which re-zeroes the count) means a drifted count self-heals after one quiet
        // spell instead of latching the voice cap forever.
        _idleTimer ??= new System.Threading.Timer(_ => { lock (Gate) Stop(); });
        // A music bed holds the device open: it runs for minutes, so the idle window would tear the device
        // down underneath it. Stopping the bed re-arms the countdown.
        int due = _musicActive ? System.Threading.Timeout.Infinite : IdleCloseMs;
        try { _idleTimer.Change(due, System.Threading.Timeout.Infinite); }
        catch (ObjectDisposedException) { /* raced with Stop; the device is down anyway */ }
    }

    /// <summary>A voice left the mixer. Floored at zero: <see cref="Stop"/> zeroes the count with the
    /// mixer, so a voice from the old device finishing afterwards must not push it negative — an
    /// undercount would quietly raise the voice cap.</summary>
    private static void ReleaseVoice()
    {
        if (System.Threading.Interlocked.Decrement(ref _voices) < 0)
            System.Threading.Interlocked.Exchange(ref _voices, 0);
    }

    /// <summary>True while a failure cooldown is running — SFX calls are dropped without touching the
    /// device. Reads outside <see cref="Gate"/> by design (the old flag did too): worst case a racing
    /// caller sees a stale value one call long.</summary>
    private static bool SfxParked() =>
        System.Threading.Volatile.Read(ref _retryAtMs) > Environment.TickCount64;

    private static void Fail(string what, Exception ex)
    {
        // Re-init after the cooldown goes through Start()'s normal cold path on a fresh mixer —
        // never a reuse of the failed graph (a partially-reused MixingSampleProvider is the
        // silently-kills-all-audio trap; see docs/SOUND.md).
        System.Threading.Volatile.Write(ref _retryAtMs, Environment.TickCount64 + RetryCooldownMs);
        lock (Gate) Stop();
        System.Diagnostics.Trace.WriteLine(
            $"[Sfx] {what} failed, sound effects paused: {ex.Message} (retried on the next sound after {RetryCooldownMs / 1000}s)");
    }

    // ── music ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The track currently mixed in, or null. Music is one long STREAM rather than a cached
    /// buffer: a four-minute track decodes to ~90 MB of float, so it is decoded MP3 frame by frame as it
    /// plays and only the compressed bytes stay resident.</summary>
    private static MusicStream? _music;

    /// <summary>Read by <see cref="BumpIdle"/> off the timer thread, so it is volatile rather than
    /// Gate-guarded: music holds the device open, and a stale read one call long only delays a teardown.</summary>
    private static volatile bool _musicActive;

    /// <summary>The current track reached its end and is not looping — a playlist host polls this each
    /// frame and starts the next track. Stays true (playing silence) until it does.
    ///
    /// <para>⚠ FALSE while a start is still decoding. The host advances on every frame this reads true, and
    /// a start takes a few frames to land, during which <see cref="_music"/> is still the ENDED track: every
    /// one of those frames advanced the playlist again and issued a start that cancelled the last, so which
    /// track finally played depended only on how many frames the decode took — and with three tracks that
    /// could be the same one every time, a playlist that sounded like a single loop.</para></summary>
    internal static bool MusicEnded => !_musicPending && (_music?.Ended ?? false);

    /// <summary>A <see cref="PlayMusic"/> has been issued and its stream has not yet replaced <see cref="_music"/>.</summary>
    private static volatile bool _musicPending;

    internal static bool MusicActive => _musicActive;

    /// <summary>Start <paramref name="packUri"/> as the music bed, fading the outgoing track out over its
    /// own tail. Music never counts against <see cref="MaxVoices"/> — a game's cues must not be dropped
    /// because a bed is up — and never returns a short read, so the mixer cannot drop it mid-track (see
    /// the trap on <see cref="Voice"/>). A missing resource is silence, like every other sound.</summary>
    /// <param name="startSeconds">Where in the track to begin — a bed picking up where a dismissed game left
    /// it. Clamped to the track; past its end, or on a track that cannot seek, it starts from the top.</param>
    public static void PlayMusic(string packUri, float gain, bool loop, int fadeInMs = 400, double startSeconds = 0)
    {
        if (SfxParked() || !TryBeginMusicAttempt(packUri, Environment.TickCount64)) return;
        // The start is built OFF the UI thread: reading a 2.5–4 MB track out of the resource stream and
        // letting Mp3FileReader index every frame of it took tens of milliseconds inside the arcade's frame
        // pump at every playlist hand-over and every resume. The bed fades in anyway, so a start that lands a
        // frame or two late is inaudible. _musicActive is claimed now so the per-frame Sync does not
        // re-request the same track while it decodes; a Stop (or a newer Play) in the meantime bumps the
        // generation and the late start is discarded.
        int gen = System.Threading.Interlocked.Increment(ref _musicGen);
        _musicActive = true;
        _musicPending = true;
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            // Whatever happens below, the start this generation issued is no longer pending once it is
            // over — landed, discarded or failed — unless a newer start has taken the flag over.
            bool succeeded = false;
            try
            {
                var bytes = MusicBytes(packUri);
                if (bytes is null) { if (gen == System.Threading.Volatile.Read(ref _musicGen)) _musicActive = false; return; }
                var next = new MusicStream(bytes, MixFormat, gain, loop, fadeInMs, startSeconds);
                lock (Gate)
                {
                    if (gen != System.Threading.Volatile.Read(ref _musicGen)) { next.Dispose(); return; }
                    if (_mixer is null && !Start()) { next.Dispose(); _musicActive = false; return; }
                    _music?.FadeOut(MusicCrossfadeMs);   // the outgoing track leaves the mixer on its own tail
                    _music = next;
                    _musicActive = true;
                    if (_musicGainScale < 0.999f) next.SetGain(_musicGainScale);   // a duck asked for meanwhile
                    _mixer!.AddMixerInput((ISampleProvider)next);
                    BumpIdle();
                    succeeded = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Sfx] music {packUri} unusable: {ex.Message}");
                if (gen == System.Threading.Volatile.Read(ref _musicGen)) _musicActive = false;
            }
            finally
            {
                if (gen == System.Threading.Volatile.Read(ref _musicGen))
                {
                    CompleteMusicAttempt(packUri, succeeded, Environment.TickCount64);
                    _musicPending = false;
                }
            }
        });
    }

    private static int _musicGen;
    private static readonly Dictionary<string, long> MusicRetryAt = new(StringComparer.Ordinal);

    private static bool TryBeginMusicAttempt(string uri, long now)
    {
        lock (Gate)
        {
            if (MusicRetryAt.TryGetValue(uri, out long next) && now < next) return false;
            MusicRetryAt[uri] = now + 5000;
            return true;
        }
    }

    private static void CompleteMusicAttempt(string uri, bool succeeded, long now)
    {
        lock (Gate)
        {
            if (succeeded) MusicRetryAt.Remove(uri);
            else MusicRetryAt[uri] = now + 5000;
        }
    }

    /// <summary>A track's compressed bytes, read from the resource stream once per process. They are
    /// immutable and the decoder streams from them, so the second start of a bed (a resume, a playlist
    /// wrap) skips the copy entirely. A few MB per track.</summary>
    private static readonly Dictionary<string, byte[]?> MusicBytesCache = new(StringComparer.OrdinalIgnoreCase);

    private static byte[]? MusicBytes(string packUri)
    {
        lock (MusicBytesCache)
        {
            if (MusicBytesCache.TryGetValue(packUri, out var hit)) return hit;
        }
        var bytes = ReadResource(packUri);
        lock (MusicBytesCache) { MusicBytesCache[packUri] = bytes; }
        return bytes;
    }

    /// <summary>Fade the bed out and let it go. Idempotent. Returns how far into the track it was, in
    /// seconds, so the host can bring it back from there; a negative value when nothing was playing.</summary>
    public static double StopMusic(int fadeMs = MusicCrossfadeMs)
    {
        System.Threading.Interlocked.Increment(ref _musicGen);   // a start still decoding is discarded
        _musicPending = false;
        lock (Gate)
        {
            if (_music is null) { _musicActive = false; return -1; }
            double at = _music.PositionSeconds;
            _music.FadeOut(fadeMs);
            _music = null;
            _musicActive = false;
            BumpIdle();          // the device may now go idle on schedule again
            return at;
        }
    }

    /// <summary>Ride the bed's level — the host ducks it under menus and cards. Ramped inside the stream,
    /// so this is safe to call every frame.</summary>
    public static void SetMusicGain(float gain)
    {
        _musicGainScale = gain;   // remembered so a start still decoding installs at the ducked level
        _music?.SetGain(gain);
    }

    private static volatile float _musicGainScale = 1f;

    private const int MusicCrossfadeMs = 500;

    /// <summary>The compressed bytes of a packed resource, or null when this build doesn't carry it.
    /// Copied out because <see cref="Mp3FileReader"/> needs a seekable stream to loop.</summary>
    private static byte[]? ReadResource(string packUri)
    {
        try
        {
            var res = Application.GetResourceStream(new Uri(packUri));
            if (res is null) return null;
            using var stream = res.Stream;
            using var buffer = new System.IO.MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Sfx] resource {packUri} unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>One music track, decoded as it plays and looped by seeking back to the start.
    ///
    /// <para><b>It never returns a short read.</b> A short read is how a finished <see cref="Voice"/> tells
    /// the mixer to drop it, so a bed that ran dry for one buffer would vanish for good; this fills any
    /// shortfall with silence instead and returns 0 — the one deliberate drop — only once its fade-out has
    /// finished. Every decode failure is treated as the end of the track rather than thrown, because this
    /// runs on the audio thread.</para>
    ///
    /// <para>Gain is a ramp toward <see cref="_target"/> (about 40 ms end to end), which serves the fade in,
    /// the host's ducking and the fade out with one mechanism and no clicks.</para></summary>
    private sealed class MusicStream : ISampleProvider, IDisposable
    {
        private readonly System.IO.MemoryStream _bytes;
        // Media Foundation, not Mp3FileReader: the beds ship as mono AAC (.m4a) — see tools\MusicIngest. The
        // reader decodes frame by frame from the seekable memory stream, so a track never sits decoded in RAM.
        private readonly WaveStream _reader;
        private readonly ISampleProvider _source;
        private readonly bool _loop;
        private readonly float _base;

        private volatile float _target;
        private float _gain;
        private readonly float _step;         // gain change per frame
        private volatile bool _stopping;
        private volatile bool _ended;
        private bool _dead;

        public MusicStream(byte[] bytes, WaveFormat format, float baseGain, bool loop, int fadeInMs,
                           double startSeconds = 0)
        {
            _bytes = new System.IO.MemoryStream(bytes, writable: false);
            _reader = new StreamMediaFoundationReader(_bytes);
            _loop = loop;
            _base = baseGain;
            WaveFormat = format;
            // Resume point. A seek that fails, or one past the end, leaves the reader at the top — the track
            // still plays, just not from where it was left.
            if (startSeconds > 0)
            {
                try
                {
                    if (startSeconds < _reader.TotalTime.TotalSeconds - 0.5)
                        _reader.CurrentTime = TimeSpan.FromSeconds(startSeconds);
                }
                catch { try { _reader.Position = 0; } catch { } }
            }

            ISampleProvider p = _reader.ToSampleProvider();
            if (p.WaveFormat.Channels == 1) p = new MonoToStereoSampleProvider(p);
            if (p.WaveFormat.SampleRate != format.SampleRate)
                p = new WdlResamplingSampleProvider(p, format.SampleRate);
            _source = p;

            _gain = 0f;
            _target = baseGain;
            _step = 1f / Math.Max(1, format.SampleRate * Math.Max(1, fadeInMs) / 1000f);
        }

        public WaveFormat WaveFormat { get; }
        public bool Ended => _ended;

        /// <summary>How far into the track playback is, in seconds. Read off the UI thread while the audio
        /// thread decodes, so it is a snapshot good to a buffer or so — plenty for a resume point.</summary>
        public double PositionSeconds
        {
            get { try { return _reader.CurrentTime.TotalSeconds; } catch { return 0; } }
        }

        /// <summary>Ride the level. <paramref name="scale"/> is a fraction of the track's own gain.</summary>
        public void SetGain(float scale)
        {
            if (!_stopping) _target = _base * Math.Clamp(scale, 0f, 1f);
        }

        public void FadeOut(int fadeMs)
        {
            _stopping = true;
            _target = 0f;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_dead) return 0;

            int got = 0;
            try { got = _source.Read(buffer, offset, count); }
            catch { got = 0; }                                  // a broken frame ends the track, quietly

            if (got < count)
            {
                if (_loop && !_stopping && TryRewind())
                {
                    try { got += _source.Read(buffer, offset + got, count - got); } catch { }
                }
                else _ended = true;
            }
            for (int i = got; i < count; i++) buffer[offset + i] = 0f;   // never a short read

            // Ramp toward the target, per frame so both channels share one gain.
            for (int i = 0; i < count; i += 2)
            {
                if (_gain < _target) _gain = Math.Min(_target, _gain + _step);
                else if (_gain > _target) _gain = Math.Max(_target, _gain - _step);
                buffer[offset + i]     *= _gain;
                buffer[offset + i + 1] *= _gain;
            }

            // Faded out and silent: leave the mixer (this IS the deliberate short read) and release the decoder.
            if (_stopping && _gain <= 0f)
            {
                _dead = true;
                Dispose();
                return 0;
            }
            return count;
        }

        private bool TryRewind()
        {
            try { _reader.Position = 0; return true; }
            catch { return false; }
        }

        public void Dispose()
        {
            try { _reader.Dispose(); } catch { }
            try { _bytes.Dispose(); } catch { }
        }
    }

    // ── graph nodes ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>One playing sound, read out of a cached buffer at <paramref name="ratio"/> speed — which
    /// is the pitch shift and, with a plain resample, also the length change. Linear interpolation
    /// between frames. Removes itself from the mixer by returning 0 once the buffer runs out, and tells
    /// the engine so on the way out (once — the mixer may read a finished voice again before dropping it).</summary>
    private sealed class Voice : ISampleProvider
    {
        private readonly float[] _src;      // interleaved stereo
        private readonly int _frames;
        private readonly double _step;
        private readonly float _gain;
        private readonly Action _onFinished;
        private double _pos;                // fractional frame index
        private bool _finished;

        public Voice(float[] src, WaveFormat format, double ratio, float gain, Action onFinished)
        {
            _src = src; WaveFormat = format; _step = ratio; _gain = gain; _onFinished = onFinished;
            _frames = src.Length / 2;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int written = 0;
            while (written + 1 < count)
            {
                int i = (int)_pos;
                if (i + 1 >= _frames) break;
                float t = (float)(_pos - i);
                int a = i * 2, b = a + 2;
                buffer[offset + written++] = _gain * (_src[a]     + (_src[b]     - _src[a])     * t);
                buffer[offset + written++] = _gain * (_src[a + 1] + (_src[b + 1] - _src[a + 1]) * t);
                _pos += _step;
            }
            // ⚠ MixingSampleProvider drops a source the moment it returns FEWER samples than asked for —
            // not only on a zero read. So the voice count has to be released on any short read, which is
            // normally the voice's final partial buffer. Getting this wrong leaks the count until it pins
            // at MaxVoices and every later sound is silently dropped.
            if (written < count && !_finished) { _finished = true; _onFinished(); }
            return written;
        }
    }

    /// <summary>Master ceiling for the whole bus. Gain reduction is computed from the sample it is
    /// applied to, so a peak can never get through — this is a hard limiter, not a soft one, and it
    /// needs no lookahead. Attack is instantaneous (a stack of notes ducks on its first sample) and
    /// release is a one-pole rise back to unity, so the level walks back up rather than pumping.
    /// Both channels share one gain, so the stereo image never shifts under limiting.</summary>
    private sealed class MasterLimiter : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly float _ceiling;
        private readonly float _releaseCoef;
        private float _gain = 1f;

        public MasterLimiter(ISampleProvider source, float ceiling, double releaseMs)
        {
            _source = source; _ceiling = ceiling;
            // One-pole coefficient: the gain covers ~63% of the remaining distance to unity per release
            // time constant.
            _releaseCoef = (float)Math.Exp(-1.0 / (source.WaveFormat.SampleRate * releaseMs / 1000.0));
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            for (int i = 0; i + 1 < read; i += 2)
            {
                int l = offset + i, r = l + 1;
                float peak = Math.Max(Math.Abs(buffer[l]), Math.Abs(buffer[r]));
                float needed = peak > _ceiling ? _ceiling / peak : 1f;
                // Attack: clamp immediately. Release: ease back toward unity.
                _gain = needed < _gain ? needed : needed - (needed - _gain) * _releaseCoef;
                buffer[l] *= _gain;
                buffer[r] *= _gain;
            }
            return read;
        }
    }
}
