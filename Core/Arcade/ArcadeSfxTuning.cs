namespace ControllerWheel;

/// <summary>Family-level knobs for the Arcade's sound layer (the shell's <c>ArcadeSfx</c>), kept in Core so the
/// dev-only <c>arcade-tuning.json</c> override reaches them like every other feel number. Per-sample gains live
/// with the sample banks in the shell; these are the trims a by-ear pass actually moves.
///
/// <para>Field names are deliberately distinct from the other tuning classes: a bare override key resolves
/// first-match across <c>ArcadeTuning.TuningTypes</c>, so <c>MasterGain</c> here would shadow nothing today but
/// would be shadowed by any later class that grew one.</para></summary>
public static class ArcadeSfxTuning
{
    /// <summary>Arcade-wide trim, stacked on the bus gain. Held under unity: the games sit over someone else's
    /// audio, and a rapid-fire cue at full level is a nuisance before it is a sound.</summary>
    public static double SfxMasterGain = 0.8;

    /// <summary>Per-family trims, so one game can be pulled down without touching its neighbours.</summary>
    public static double ChromeGain = 1.0;
    public static double KabloomGain = 1.0;
    public static double ConnateGain = 1.0;
    public static double PetalPopGain = 1.0;
    public static double InternodeGain = 1.0;

    /// <summary>Pitch ladders: semitones per step and the step cap. A ladder plays the same take higher as a
    /// count climbs (combo, cascade size, merge rank, catch streak). Resampling shortens the clip with the
    /// pitch, so the cap keeps the top of a ladder recognisable as the same sound.</summary>
    public static double LadderSemitones = 1.5;
    public static int LadderMaxSteps = 8;

    /// <summary>Host-side throttles (wall-clock ms) for cues a sim can raise faster than a sound reads. The
    /// Kabloom gem burst lands stone after stone; a paddle can be struck by nine
    /// balls in a frame; a long Petalpop chain shouts on every pop.</summary>
    public static int DiamondMinIntervalMs = 70;
    public static int PaddleMinIntervalMs = 40;
    public static int ComboShoutMinIntervalMs = 250;

    /// <summary>Petalpop's smash stacks the ordinary paddle take under its own: this is the paddle layer's
    /// pitch ratio (below 1 = lower, ~2 semitones) and its level, on top of the paddle bank's own gain.</summary>
    public static double SmashPaddlePitch = 0.89;
    public static double SmashPaddleGain = 1.2;

    /// <summary>The music beds' level, and the fraction they duck to while a menu, a card or the ready beat is
    /// up (docs/SOUND.md ▸ Music). Music sits well under the cues: it is a bed, and the games play over
    /// whatever the player already had running.</summary>
    public static double MusicGain = 0.20;
    public static double MusicDuck = 0.35;
    /// <summary>Fade-in on a bed starting from a track's top, and the longer one when it picks up where a
    /// dismissed game left it — a resume lands mid-phrase, and needs the softer entry.</summary>
    public static int MusicStartFadeMs  = 400;
    public static int MusicResumeFadeMs = 700;

    /// <summary>A Kabloom cascade at least this many cells wide layers the bloom with a second, brighter take.</summary>
    public static int BloomLayerAt = 8;
}
