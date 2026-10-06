namespace ControllerWheel;

/// <summary>UI sound effects: slice armed (tick), slice fired, wheels enabled/disabled. The .wav files
/// are compiled in as WPF resources (Assets\sfx\, see the csproj) and lazily decoded once by
/// <see cref="SfxEngine"/>, which mixes them: sounds OVERLAP rather than cutting each other off, with a
/// master limiter keeping a stacked burst from getting loud. Gated by <see cref="Enabled"/>
/// (SystemConfig.SoundEffects — the "Silent" tile of the Sound-effects picker), set at startup + on
/// config reload. The sounds come in SETS (<see cref="Theme"/>): "digital", "physical" (tap.wav /
/// selected.wav), and one per styled material — "kawaii" (whose ARMED sound is not a WAV at all but the
/// melodic xylophone sequencer, <see cref="KawaiiXylophone"/>), "mesa", "salvage", "reactor", plus
/// "obsidian" (Digital's samples pitched down). Config
/// stores SystemConfig.SoundTheme = "material" / "digital" / "physical"; <see cref="ResolveSet"/> turns
/// that plus the material into the active set. A set with no wheels on/off pair of its own (digital,
/// physical, mesa) falls back to the shared enable/disable-wheels pair. Every path is fail-quiet: a
/// missing resource or a machine with no audio device must never break input.</summary>
internal static class Sfx
{
    /// <summary>Master switch (SystemConfig.SoundEffects; default on). Off = every Play is a no-op.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>The ACTIVE sound set: "digital", "physical", "kawaii", "mesa", "salvage", "reactor" or "obsidian" —
    /// always a resolved set name ("obsidian" included), never the stored "material" token (App assigns
    /// <see cref="ResolveSet"/>'s result). Every set stays lazily cached, so flipping it takes effect on
    /// the next play, no reload.</summary>
    public static string Theme { get; set; } = "digital";

    /// <summary>Stored SoundTheme token + material → the set that actually plays. "material" (and any
    /// unknown token) follows the material via <see cref="Materials.SoundThemeFor"/>; explicit
    /// digital/physical stand alone. Legacy stored tokens map to their sets: "tap"/"classic" (day one),
    /// "kawaii"/"sparkle" (from when Kawaii was a pickable theme).</summary>
    public static string ResolveSet(string? theme, string? material) => theme switch
    {
        "digital" or "classic" => "digital",
        "physical" or "tap"    => "physical",
        "kawaii" or "sparkle"  => "kawaii",
        _                      => Materials.SoundThemeFor(material),   // "material" + anything unknown
    };

    private static bool Kawaii => Theme is "kawaii" or "sparkle";   // "sparkle" = pre-rename token

    /// <summary>One sound set: the sample name per event plus its by-ear gain trim. A null
    /// <see cref="Enable"/>/<see cref="Disable"/> means the set has no wheels on/off pair of its own and
    /// takes the shared one. Gains default to 1.0, so an entry names only what it deviates on.</summary>
    private sealed record SoundSet(string Armed, string Fired)
    {
        public string? Enable      { get; init; }
        public string? Disable     { get; init; }
        public float   ArmedGain   { get; init; } = 1f;
        public float   FiredGain   { get; init; } = 1f;
        public float   EnableGain  { get; init; } = 1f;
        public float   DisableGain { get; init; } = 1f;
        /// <summary>Uniform pitch jitter on the ARMED sample only (0 = fixed pitch); see
        /// <see cref="ArmedPitchJitter"/>.</summary>
        public double  Jitter      { get; init; }

        /// <summary>Playback speed for every sample this set plays: 1.0 = as recorded, below 1.0 is
        /// deeper AND proportionally longer (the engine resamples — this is not a time-preserving
        /// pitch shift). Lets a set be a pitched variant of another set rather than needing its own
        /// recordings; see <see cref="SetObsidian"/>.</summary>
        public double  Pitch       { get; init; } = 1.0;
    }

    // The sets, built once. Trims are tuned by ear on hardware — several source recordings ship hotter
    // than the rest of the bus. Kawaii's Armed entry is nominal: SliceArmed routes its arming to the
    // xylophone sequencer instead.
    //
    // NAMING CONTRACT: a sample is named <set>-<event>.wav for the set that plays it, so a set's row
    // below reads back as its own filenames. The only exceptions are the genuinely shared samples,
    // which carry no set prefix — tap/selected (Physical's pair, and Mesa borrows the tap with
    // physical's gain and jitter) and enable-wheels/disable-wheels (the fallback pair; Kawaii names
    // the shared disable explicitly, everyone else falls through to it). Keep it that way: when a set
    // is re-pointed at another set's recording, RENAME THE FILE to follow it rather than leaving a
    // name that lies about who plays it.
    private static readonly SoundSet SetDigital  = new("slice-armed", "slice-fired") { ArmedGain = 0.54f };
    // Obsidian is Digital DEEPER, not its own recordings: same samples at 0.84 speed (~3 semitones down,
    // and ~19% longer with them). A variant, so it deliberately shares Digital's files — see the naming
    // contract above. Retune by moving Pitch alone; 2^(n/12) converts semitones to a ratio.
    private static readonly SoundSet SetObsidian = SetDigital with { Pitch = 0.84 };
    private static readonly SoundSet SetPhysical = new("tap", "selected")
        { ArmedGain = PhysicalGain, FiredGain = PhysicalGain, Jitter = ArmedPitchJitter };
    private static readonly SoundSet SetKawaii   = new("kawaii-slice-armed", "kawaii-slice-fired")
        { FiredGain = 0.85f, Enable = "kawaii-enable-wheels", EnableGain = 0.27f, Disable = "disable-wheels" };
    private static readonly SoundSet SetMesa     = new("tap", "mesa-slice-fired")
        { ArmedGain = PhysicalGain, Jitter = ArmedPitchJitter };
    private static readonly SoundSet SetSalvage  = new("salvage-slice-armed", "salvage-slice-fired")
        { Enable = "salvage-enable-wheels", Disable = "salvage-disable-wheels",
          ArmedGain = 0.75f, EnableGain = 0.69f, DisableGain = 0.69f };
    private static readonly SoundSet SetReactor  = new("reactor-slice-armed", "reactor-slice-fired")
        { Enable = "reactor-enable-wheels", Disable = "reactor-disable-wheels",
          ArmedGain = 0.38f, FiredGain = 0.67f };

    private static SoundSet SetFor(string theme) => theme switch
    {
        "physical" or "tap"   => SetPhysical,
        "kawaii" or "sparkle" => SetKawaii,
        "mesa"                => SetMesa,
        "salvage"             => SetSalvage,
        "reactor"             => SetReactor,
        "obsidian"            => SetObsidian,
        _                     => SetDigital,
    };

    private static SoundSet Set => SetFor(Theme);

    /// <summary>A drop-in theme's per-event sound overrides (docs/PACKAGES.md; null = none active).
    /// Set by App whenever the wheel material applies; each present event trumps the theme's sound,
    /// absent events fall through to it. Buffers are pre-decoded to the mix format by
    /// <see cref="SfxEngine.LoadFile"/>, so a bad file is an EMPTY buffer — treated as absent here
    /// rather than playing as silence, so a package with one broken wav keeps the theme's cue.</summary>
    public static CustomSounds? Custom { get; set; }

    public sealed record CustomSounds(float[]? Armed, float[]? Fired, float[]? EnableWheels, float[]? DisableWheels);

    private static float[]? Buf(float[]? b) => b is { Length: > 0 } ? b : null;

    /// <summary>Per-theme trim for the PHYSICAL pair (tap/selected), which recorded quieter than the
    /// other sets — +20% by ear. Applied wherever those two samples play, previews included.</summary>
    private const float PhysicalGain = 1.2f;

    /// <summary>±3% uniform pitch jitter applied to the PHYSICAL armed tick only, so a fast scrub down the
    /// ring doesn't sound mechanically identical on every slice. Kept subtle on purpose — a texture cue,
    /// not a pitch cue; 0 = fixed pitch. Deliberately NOT applied to Digital's arm (the tick is a synthetic
    /// blip and the wobble reads as a fault), nor to Kawaii's: that "sound" is the next note of the
    /// xylophone melody, whose pitch IS the tune, so jitter would put it out of key.</summary>
    private const double ArmedPitchJitter = 0.03;

    /// <summary>A slice armed. Kawaii does NOT play a fixed chime — it plays the next note of the
    /// current xylophone melody (<see cref="KawaiiXylophone"/>), so scrubbing the wheel plays a tune;
    /// kawaii-slice-armed.wav is consequently unused for arming (it still backs
    /// <see cref="SliceArmedSoft"/>'s parked cue, which stays at fixed pitch). Physical gets a small
    /// random pitch jitter (see <see cref="ArmedPitchJitter"/>); every other set plays fixed pitch.</summary>
    public static void SliceArmed()
    {
        if (Buf(Custom?.Armed) is { } customArmed) { if (Enabled) SfxEngine.Play(customArmed); return; }
        if (Kawaii) { if (Enabled) KawaiiXylophone.ArmedNote(); return; }
        var set = Set;
        Play(set.Armed, set.ArmedGain,
             set.Pitch * (set.Jitter > 0 ? 1.0 + (Random.Shared.NextDouble() * 2 - 1) * set.Jitter : 1.0));
    }

    // Half-volume arm cue for a guarded slice at dwell focus. Currently unused: it muddied the real
    // tick at dwell completion, which is the only sound a guarded slice makes. App's focus branch is
    // deliberately left in place, empty, as the hook for a future cue; this stays with it so wiring one
    // back up is a one-line change.
    public static void SliceArmedSoft() => Play(Set.Armed, 0.5f * Set.ArmedGain, Set.Pitch);

    public static void SliceFired()
    {
        if (Buf(Custom?.Fired) is { } customFired) { if (Enabled) SfxEngine.Play(customFired); return; }
        var set = Set;
        Play(set.Fired, set.FiredGain, set.Pitch);
    }

    /// <summary>A wheel bloomed in — rotate the Kawaii melody to the next song and rewind it. Called on
    /// every real wheel open whatever the theme, so a mid-session switch to Kawaii starts a song from
    /// its first note. Entering the in-wheel editor or the Game Grid is NOT an open (deliberate): those
    /// carry on through the song they're already in. Also primes the audio device, so the first tick of
    /// the scrub never waits on it opening.</summary>
    public static void WheelOpened()
    {
        if (Enabled) SfxEngine.Prime();
        KawaiiXylophone.WheelOpened();
    }

    /// <summary>Preview a specific SET's fire sound on demand (the Customize sound picker + onboarding's
    /// sound toggle) — regardless of the current <see cref="Enabled"/>/<see cref="Theme"/>, so clicking a
    /// tile always demonstrates it. Callers pass a resolved set name (run "material" through
    /// <see cref="ResolveSet"/> first); "none"/unknown = silent. Fail-quiet like every other path.</summary>
    public static void PreviewFire(string theme)
    {
        if (theme is not ("digital" or "physical" or "tap" or "kawaii" or "sparkle"
                          or "mesa" or "salvage" or "reactor" or "obsidian")) return;   // "none"/unknown = silent
        var set = SetFor(theme);
        // Same trim AND pitch as the real fire, so the preview is honest (Obsidian previews deeper).
        SfxEngine.Play(Sample(set.Fired), set.FiredGain, set.Pitch);
    }

    /// <summary>Wheels toggled. Both sounds are nudged late to land on their toast animation (delays
    /// tuned by ear on hardware): ON waits out the toast circle's 150 ms scale-in so it hits the flower
    /// bloom; OFF gets a smaller nudge into the flower's collapse. Kawaii, Salvage and Reactor ship their
    /// own pairs; digital, physical and Mesa share the originals.</summary>
    public static void Wheels(bool enabled)
    {
        // Each call supersedes any toggle sound still waiting to play. The two delays differ (175 vs 100),
        // so a fast chord bounce — disable then re-enable inside ~175 ms — would let the LATER, shorter-
        // delayed sound land FIRST: enabled, but the last thing heard is the disable cue. The generation
        // stamp makes the newest toggle the only one that speaks.
        int gen = ++_wheelsSfxGen;
        // Prime at chord time, not at play time: the 100/175 ms animation delay below becomes wake-ahead
        // lead for a cold audio chain (see SfxEngine.Prime) instead of the cue eating the wake window.
        if (Enabled) SfxEngine.Prime();
        var custom = enabled ? Buf(Custom?.EnableWheels) : Buf(Custom?.DisableWheels);
        if (custom is not null) { PlayDelayedBuffer(custom, enabled ? 175 : 100, gen); return; }
        var set = Set;
        // A set with no pair of its own falls back to the SHARED sample at unity GAIN — a gain trims the
        // recording a set ships, and must not follow a sample it doesn't own. PITCH is different: it is the
        // set's character (Obsidian = Digital, deeper), so it applies to whatever the set plays, borrowed
        // or not — otherwise Obsidian's wheels chord would be the one cue that isn't pitched with it.
        if (enabled) PlayDelayed(set.Enable  ?? "enable-wheels",  175, gen, set.Enable  is null ? 1f : set.EnableGain,  set.Pitch);
        else         PlayDelayed(set.Disable ?? "disable-wheels", 100, gen, set.Disable is null ? 1f : set.DisableGain, set.Pitch);
    }

    /// <summary>The custom-sound twin of <see cref="PlayDelayed"/> — same supersession stamp.</summary>
    private static async void PlayDelayedBuffer(float[] samples, int delayMs, int gen)
    {
        try { await Task.Delay(delayMs); } catch { return; }   // async void — contain
        if (gen != Volatile.Read(ref _wheelsSfxGen)) return;
        if (Enabled) SfxEngine.Play(samples);
    }

    private static int _wheelsSfxGen;

    private static void Play(string name, float gain = 1f, double pitchRatio = 1.0)
    {
        if (!Enabled) return;
        SfxEngine.Play(Sample(name), gain, pitchRatio);
    }

    /// <param name="gen">Supersession stamp: the sound is dropped if another toggle happened while this one
    /// was waiting (0 = not superseded, for callers with no newer-call concept).</param>
    private static async void PlayDelayed(string name, int delayMs, int gen = 0, float gain = 1f, double pitch = 1.0)
    {
        try { await Task.Delay(delayMs); } catch { return; }   // async void — contain
        if (gen != 0 && gen != Volatile.Read(ref _wheelsSfxGen)) return;
        Play(name, gain, pitch);   // Enabled is re-checked at fire time
    }

    // The Arcade's sound lives in ArcadeSfx: bespoke banks per game plus a shared chrome vocabulary, all through
    // the same bus and limiter (nothing in the app is allowed its own audio path), and deliberately NOT following
    // the wheel's material or sound theme. Drop-in script games speak the GENERIC vocabulary directly via
    // ArcadeSfx.Generic.Cue(string). See docs/SOUND.md ▸ Arcade.

    private static float[] Sample(string name) =>
        SfxEngine.LoadResource($"pack://application:,,,/Assets/sfx/{name}.wav");
}
