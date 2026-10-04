using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>Every "feel" number in the arcade, in one place. No magic numbers in a sim.
///
/// <para>Mutable static fields rather than consts, so <see cref="LoadOverrides"/> can replace them at open.
/// Units belong in the names; keep it that way.</para></summary>
public static class ArcadeTuning
{
    // ── Frame pump ────────────────────────────────────────────────────────────
    /// <summary>Fixed sim step. 120 Hz: comfortably finer than any display's frame time, so the render is
    /// always interpolating between two recent states rather than extrapolating past one.</summary>
    public static double StepSeconds = 1.0 / 120.0;

    /// <summary>Most sim steps allowed to catch up in one rendered frame. A longer backlog is dropped rather
    /// than simulated, or a starved overlay fast-forwards the player into an enemy they never saw.</summary>
    public static int MaxStepsPerFrame = 12;

    /// <summary>Frozen game resumes behind this many seconds of rendered-but-not-simulated "ready" beat, so
    /// you don't die to an enemy that was one pixel away when you dismissed.</summary>
    public static double ResumeReadySeconds = 0.375;

    /// <summary>Share of the ready beat spent fading the scrim and its word out, measured from the end. The
    /// remainder is held at full. Most of the beat is the fade: the point is to hand the board back
    /// gradually — a scrim that pops off is the drop into play this beat exists to prevent.</summary>
    public static double ResumeReadyFadeFrac = 0.75;

    // ── The disc ──────────────────────────────────────────────────────────────
    /// <summary>A game's disc is this many times the launcher's. The launcher keeps the wheel's own footprint
    /// (the wheel dissolves and the cabinets bloom in its place); a game takes the extra room. The window is
    /// sized for a game and the launcher draws inside it at 1 / this, so the disc can grow and shrink between
    /// the two without the window moving.</summary>
    public static double GameDiscScale = 1.30;
    /// <summary>Seconds the disc takes to shrink back to the launcher's size when ○ leaves a game for the
    /// cabinets — and, on the same clock, for the board to merge into its cabinet's screen. Growing rides
    /// the launch zoom's own clock (<c>ArcadePickerTuning.LaunchZoomSeconds</c>).</summary>
    public static double DiscShrinkSeconds = 0.45;

    // ── The overlaid UI ───────────────────────────────────────────────────────
    /// <summary>Multipliers on every overlaid readout — scores, level readouts, shouts, prompts, cards, the
    /// launcher's text — and on the pictograms that ride with them (Connate's bomb meter, Kabloom's nectar
    /// gem, Petalpop's life pearls, the bezel lives). Nothing in play is touched: petals, tiles, balls,
    /// paddles, runners, bombs in flight and the like keep their own sizes. Applied through
    /// <c>ArcadeChrome.Ui</c> / <c>ArcadeChrome.UiArt</c>.</summary>
    public static double HudTextScale = 1.75;
    public static double HudArtScale  = 2.00;
    /// <summary>The how-to card's own text scale. Its five illustrated bullets must fit one disc with no
    /// scrolling (TestHarness arcade ▸ HowToFits), which the HUD scale above does not leave room for.</summary>
    public static double HowToTextScale = 1.15;

    // ── Dev-only tuning override ──────────────────────────────────────────────

    /// <summary>Every class the override file may address, in lookup order for a bare key. This class first,
    /// so a key that exists in two of them keeps landing where it always did; a <c>Type.Field</c> key names the
    /// other one explicitly.</summary>
    private static readonly Type[] TuningTypes =
        [typeof(ArcadeTuning), typeof(ConnateTuning), typeof(KabloomTuning), typeof(PetalPopTuning), typeof(InternodeTuning),
         typeof(ArcadePickerTuning), typeof(ArcadeSfxTuning)];

    /// <summary>The compiled-in values, snapshotted before any override is applied. Restoring from this on
    /// each apply is what makes deleting a key from the file take effect; without it an override would stick
    /// for the life of the process. Keyed by the field itself, not its name — names repeat across the
    /// tuning classes.</summary>
    private static Dictionary<FieldInfo, object>? _compiledDefaults;

    private static IEnumerable<FieldInfo> Knobs() =>
        TuningTypes.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => !f.IsLiteral && !f.IsInitOnly
                        && (f.FieldType == typeof(double) || f.FieldType == typeof(int)));

    /// <summary>Resolve an override key: <c>Field</c> searches <see cref="TuningTypes"/> in order,
    /// <c>Type.Field</c> names one class outright.</summary>
    private static FieldInfo? Knob(string key)
    {
        int dot = key.IndexOf('.');
        if (dot > 0)
        {
            string typeName = key[..dot], field = key[(dot + 1)..];
            var t = TuningTypes.FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.OrdinalIgnoreCase));
            return t?.GetField(field, BindingFlags.Public | BindingFlags.Static);
        }
        foreach (var t in TuningTypes)
            if (t.GetField(key, BindingFlags.Public | BindingFlags.Static) is { } f) return f;
        return null;
    }

    /// <summary>Dev only, absent by default. If <c>%APPDATA%\Radiata\arcade-tuning.json</c> exists, its
    /// <c>{"PlayerDegPerSec": 500, "PetalPopTuning.SpringHz": 8, …}</c> pairs overwrite the fields of every
    /// class in <see cref="TuningTypes"/>, re-read on every arcade open.
    ///
    /// <para>Each call resets every knob to its compiled default first, so the file is the whole truth and a
    /// deleted key reverts. A partial file is the normal case.</para>
    ///
    /// <para>No UI, no Settings surface, no writing: read-only, and every failure is swallowed — a malformed
    /// override must never stop a game opening.</para></summary>
    public static void LoadOverrides()
    {
        try
        {
            if (_compiledDefaults is null)
            {
                _compiledDefaults = new Dictionary<FieldInfo, object>();
                foreach (var f in Knobs()) _compiledDefaults[f] = f.GetValue(null)!;
            }
            else
            {
                foreach (var f in Knobs())
                    if (_compiledDefaults.TryGetValue(f, out var def)) f.SetValue(null, def);
            }

            var path = Path.Combine(AppPaths.AppDataDir, "arcade-tuning.json");
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            int applied = 0;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var f = Knob(prop.Name);
                if (f is null || f.IsLiteral || f.IsInitOnly) continue;
                try
                {
                    if (f.FieldType == typeof(double) && prop.Value.TryGetDouble(out var d)) { f.SetValue(null, d); applied++; }
                    else if (f.FieldType == typeof(int) && prop.Value.TryGetInt32(out var i)) { f.SetValue(null, i); applied++; }
                }
                catch { /* one bad value must not abandon the rest */ }
            }
            Trace.WriteLine($"[Arcade] tuning override applied to {applied} field(s) from {path}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] tuning override skipped: {ex.Message}");
        }
    }
}
