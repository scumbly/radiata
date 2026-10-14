using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Radiata.TestHarness;

/// <summary>
/// The Arcade sound banks resolve.
///
/// Every <c>ArcadeSfx.Bank</c> names its takes by resource stem, and the engine turns a missing resource into
/// SILENCE rather than an error — the right posture for the public mirror, which ships without the licensed
/// WAVs, and the wrong one for a typo, which would ship a mute cue nobody reports. So: when this build carries
/// any arcade WAV at all, every stem every bank names must be one of them, no take may be empty, and no take
/// may run long enough to crowd the mixer's voice cap. When the build carries none, the group SKIPS: that is the
/// mirror, and silence there is by design.
/// </summary>
internal static class T_ArcadeSfx
{
    private const string Prefix = "assets/sfx/arcade/";
    /// <summary>The longest a take may run. Sized to the author's own longest takes (Internode's bank flight at
    /// 3.4 s, Petalpop's win at 3.5 s), each a once-per-event sound; the cap exists to stop a RAPID cue from
    /// piling long voices onto the 16-voice mixer, so a frequent cue's take should stay well under it.</summary>
    private const double MaxSeconds = 3.5;

    public static void Run()
    {
        H.Group("Arcade sound — every bank take is a shipped resource");

        var keys = ResourceKeys();
        if (!keys.Any(k => k.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            H.Skip("arcade banks resolve", "this build carries no Assets\\sfx\\arcade\\*.wav (the public mirror) — "
                                          + "the arcade is silent by design there");
            return;
        }

        var sfx = H.AppType("ArcadeSfx");
        var banks = ((IEnumerable)H.GetStatic(sfx, "AllBanks")).Cast<object>().ToList();
        H.Check("AllBanks enumerates something", banks.Count > 0, $"{banks.Count} banks");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int missing = 0, empty = 0, longOnes = 0;
        foreach (var b in banks)
        {
            var t = b.GetType();
            string name = (string)t.GetProperty("Name").GetValue(b);
            var variants = (string[])t.GetProperty("Variants").GetValue(b);
            if (variants is null || variants.Length == 0) { empty++; H.Fail($"bank {name} has takes", "none"); continue; }
            foreach (var stem in variants)
            {
                // A "shared:" stem names the wheel's own set, not the arcade folder (ArcadeSfx.SharedPrefix).
                bool shared = stem.StartsWith("shared:", StringComparison.Ordinal);
                string key = shared ? "assets/sfx/" + stem["shared:".Length..].ToLowerInvariant() + ".wav"
                                    : Prefix + stem.ToLowerInvariant() + ".wav";
                if (!shared) seen.Add(key);
                if (!keys.Contains(key)) { missing++; H.Fail($"{stem}.wav is a resource", "not in Radiata.g.resources"); continue; }
                double s = Seconds(key);
                if (s > MaxSeconds) { longOnes++; H.Fail($"{stem}.wav under {MaxSeconds:0.0} s", $"{s:0.00} s"); }
            }
        }
        if (missing == 0) H.Pass("every take resolves to a shipped resource", $"{seen.Count} distinct takes");
        if (empty == 0)   H.Pass("no bank is empty");
        if (longOnes == 0) H.Pass($"every take is under {MaxSeconds:0.0} s");

        // The other direction: a WAV shipped that no bank plays is dead weight in the exe.
        var orphans = keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal) && !seen.Contains(k)).ToList();
        H.Check("no shipped arcade WAV is unreferenced", orphans.Count == 0, string.Join(", ", orphans.Take(8)));

        CheckCueBits();
        CheckConnateCues(banks);
        CheckConnateBus(banks, keys);
        CheckMusic(keys);
        CheckManifestCoverage(keys);
    }

    /// <summary>The converse of the shipped-WAV-is-referenced check above: every SfxIngest manifest entry
    /// NOT marked "alt" (an audition-only copy, never shipped) must resolve to a shipped resource. A
    /// cue whose WAV and bank wiring were removed but whose manifest entry was left live re-decodes on the
    /// next ingest run and ships again. Development repository only: the public snapshot carries neither the
    /// ingest tool nor its manifest.</summary>
    private static void CheckManifestCoverage(HashSet<string> keys)
    {
        var root = H.RepoRoot();
        if (root is null) { H.Skip("SfxIngest manifest coverage", "repo root not found from the harness working directory"); return; }
        if (!H.DevRepoOnly("SfxIngest manifest coverage (tools/SfxIngest/manifest.json)", root)) return;
        var manifestPath = Path.Combine(root, "tools", "SfxIngest", "manifest.json");
        if (!File.Exists(manifestPath)) { H.Fail("tools/SfxIngest/manifest.json exists", "missing"); return; }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }); }
        catch (Exception ex) { H.Fail("manifest.json parses", ex.Message); return; }

        using (doc)
        {
            int missing = 0, checkedCount = 0;
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
            {
                if (e.TryGetProperty("alt", out var altProp) && altProp.ValueKind == JsonValueKind.True) continue;
                string name = e.GetProperty("name").GetString();
                checkedCount++;
                string key = Prefix + name.ToLowerInvariant() + ".wav";
                if (!keys.Contains(key))
                {
                    missing++;
                    H.Fail($"manifest entry \"{name}\" has a shipped WAV", $"{key} not in Radiata.g.resources — mark it \"alt\": true if it was retired");
                }
            }
            if (missing == 0) H.Pass("every non-alt SfxIngest manifest entry has a shipped WAV", $"{checkedCount} entries");
        }
    }

    /// <summary>Every game's <c>Cue</c> flag enum must give each cue its own bit. Two cues on one value make
    /// BOTH sounds play on either event, and nothing else in the pipeline can tell: the sim sets a bit, the
    /// dispatcher tests bits, and the sample that results is simply the wrong one. Internode shipped with its
    /// gold-turn and double-jump cues on 16384, so every double jump played the bonus take.</summary>
    private static void CheckCueBits()
    {
        foreach (var cue in new[] { typeof(ControllerWheel.Internode.Cue), typeof(ControllerWheel.Kabloom.Cue),
                                    typeof(ControllerWheel.Connate.Cue), typeof(ControllerWheel.PetalPop.Cue) })
        {
            var members = Enum.GetNames(cue).Select(n => (Name: n, Value: Convert.ToInt64(Enum.Parse(cue, n)))).ToList();
            var shared = members.Where(m => m.Value != 0).GroupBy(m => m.Value).Where(g => g.Count() > 1)
                                .Select(g => $"{string.Join("=", g.Select(m => m.Name))}={g.Key}").ToList();
            var multiBit = members.Where(m => m.Value != 0 && (m.Value & (m.Value - 1)) != 0).Select(m => $"{m.Name}={m.Value}").ToList();
            string game = cue.DeclaringType?.Name ?? cue.Name;
            H.Check($"{game}.Cue: every cue has its own bit", shared.Count == 0 && multiBit.Count == 0,
                    string.Join(", ", shared.Concat(multiBit)));
        }
    }

    /// <summary>Every Connate cue except the silent-by-design <c>Move</c> reaches a vocabulary method in
    /// <c>ArcadeControl.PlayConnate</c>, and that method exists on <c>ArcadeSfx.Connate</c> — a cue the
    /// dispatcher never tests is a sound that never plays, and the sim cannot tell. Source-scanned because
    /// the mapping is plain statements in a private method. Also pins that the earned-bomb and combo-layer
    /// banks each name their own Connate stem (the Playdate edition's samples).</summary>
    private static void CheckConnateCues(List<object> banks)
    {
        var root = H.RepoRoot();
        string path = root is null ? null : Path.Combine(root, "Arcade", "ArcadeControl.cs");
        if (path is null || !File.Exists(path)) { H.Skip("Connate cues map to banks", "ArcadeControl.cs not found from the harness working directory"); return; }

        var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path),
            @"private static void PlayConnate\(Connate c, Connate\.Cue cc\)\s*\{(?<body>.*?)\r?\n    \}",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!m.Success) { H.Fail("ArcadeControl.PlayConnate is scannable", "method not found"); return; }
        string body = m.Groups["body"].Value;

        var vocab = H.AppType("ArcadeSfx").GetNestedType("Connate", BindingFlags.Public | BindingFlags.NonPublic);
        var methods = vocab?.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(x => x.Name).ToHashSet() ?? [];
        var unmapped = new List<string>();
        foreach (var name in Enum.GetNames(typeof(ControllerWheel.Connate.Cue)))
        {
            if (name is "None" or "Move") continue;
            var call = System.Text.RegularExpressions.Regex.Match(body,
                $@"Connate\.Cue\.{name}\)\s*!=\s*0\)\s*ArcadeSfx\.Connate\.(?<m>\w+)\(");
            if (!call.Success || !methods.Contains(call.Groups["m"].Value)) unmapped.Add(name);
        }
        H.Check("every Connate cue (except Move) plays an ArcadeSfx.Connate method", unmapped.Count == 0, string.Join(", ", unmapped));

        string[] TakesOf(string bankName) => banks
            .Where(b => b.GetType().GetProperty("Name").GetValue(b) is string n && n == bankName
                        && b.GetType().GetProperty("Family").GetValue(b).ToString() == "Connate")
            .Select(b => (string[])b.GetType().GetProperty("Variants").GetValue(b)).FirstOrDefault() ?? [];
        H.Check("Connate bomb-earned plays its own take",
                TakesOf("bomb-earned").SequenceEqual(new[] { "connate-bomb-earned" }), string.Join(", ", TakesOf("bomb-earned")));
        H.Check("Connate combo layer plays its own take",
                TakesOf("combo-layer").SequenceEqual(new[] { "connate-chain-layer" }), string.Join(", ", TakesOf("combo-layer")));
        // The combo layer is throttled at 75 ms, taken on its own bank so the plain merge blip (which fires on
        // every merge) is not throttled by it, and starts 50 ms after the blip.
        string sfxPath = Path.Combine(root, "Arcade", "ArcadeSfx.cs");
        string sfxText = File.Exists(sfxPath) ? File.ReadAllText(sfxPath) : "";
        bool layerThrottled = System.Text.RegularExpressions.Regex.IsMatch(sfxText,
            @"public static void ComboLayer\(int combo\)\s*\{\s*if \(Throttled\(CnComboLayer, ArcadeSfxTuning\.ComboLayerMinIntervalMs\)\) return;\s*PlayAfter\(CnComboLayer, 50, combo - 1\);");
        H.Check("Connate combo layer is throttled at 75 ms and delayed 50 ms",
                ControllerWheel.ArcadeSfxTuning.ComboLayerMinIntervalMs == 75 && layerThrottled,
                $"{ControllerWheel.ArcadeSfxTuning.ComboLayerMinIntervalMs} ms, call shape: {layerThrottled}");

        // The layer is triggered and pitched by the combo count alone: the dispatcher hands it ComboCount, the
        // cue is raised from the second merge since the shot, and neither the dispatcher nor the sound bank
        // reads the rolling chain window.
        H.Check("Connate combo layer is pitched by ComboCount",
                body.Contains("Connate.Cue.ComboLayer) != 0) ArcadeSfx.Connate.ComboLayer(c.ComboCount);"), "dispatcher call");
        H.Check("no Connate sound path reads ChainDepth",
                !body.Contains("ChainDepth") && !sfxText.Contains("ChainDepth")
                && !Enum.GetNames(typeof(ControllerWheel.Connate.Cue)).Contains("Chain"),
                "ChainDepth in PlayConnate/ArcadeSfx, or a Chain cue");

        // Rung ladder against the combo, Playdate's: rung = combo - 1, 1.5 semitones a rung, capped at 8
        // rungs (a 12-semitone octave).
        var ladder = H.AppType("ArcadeSfx").GetMethod("Ladder", BindingFlags.NonPublic | BindingFlags.Static);
        bool ladderOk = ladder is not null && ControllerWheel.ArcadeSfxTuning.LadderSemitones == 1.5 && ControllerWheel.ArcadeSfxTuning.LadderMaxSteps == 8;
        string ladderWhy = "";
        for (int combo = 2; ladderOk && combo <= 14; combo++)
        {
            double expect = Math.Pow(2.0, Math.Min(combo - 1, 8) * 1.5 / 12.0);
            double got = (double)ladder.Invoke(null, new object[] { combo - 1 });
            if (Math.Abs(got - expect) > 1e-9) { ladderOk = false; ladderWhy = $"combo {combo}: {got} vs {expect}"; }
        }
        H.Check("Connate combo layer rung = combo - 1 at 1.5 semitones, capped at 8", ladderOk, ladderWhy);
    }

    /// <summary>The Connate bank is the Playdate edition's mono samples. The bus mixes one WaveFormat (44.1 kHz
    /// stereo float) and a source that reads short or in another format silently drops out of the mixer, so:
    /// every Connate stem must be a Connate-owned stem shipped in the bus's own format (the ingest widens mono
    /// and resamples), decode to a whole number of stereo frames within full scale, and play to completion
    /// through a real <c>Voice</c> in a <c>MixingSampleProvider</c> at the ratios the cues use (1.0, the merge's
    /// 1.30, the ladder's top step). The manifest side — every Connate entry is ingested from the repo's
    /// Playdate folder — is development-repository only.</summary>
    private static void CheckConnateBus(List<object> banks, HashSet<string> keys)
    {
        var stems = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var b in banks)
        {
            var t = b.GetType();
            if (t.GetProperty("Family").GetValue(b).ToString() != "Connate") continue;
            foreach (var v in (string[])t.GetProperty("Variants").GetValue(b)) stems.Add(v);
        }
        H.Check("Connate banks name only connate-* stems", stems.Count > 0 && stems.All(s => s.StartsWith("connate-", StringComparison.Ordinal)),
                string.Join(", ", stems.Where(s => !s.StartsWith("connate-", StringComparison.Ordinal))));

        var engine = H.AppType("SfxEngine");
        var voiceType = engine.GetNestedType("Voice", BindingFlags.NonPublic | BindingFlags.Public);
        var mixFormat = (WaveFormat)engine.GetField("MixFormat", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        double ladderTop = Math.Pow(2.0, ControllerWheel.ArcadeSfxTuning.LadderMaxSteps * ControllerWheel.ArcadeSfxTuning.LadderSemitones / 12.0);
        int bad = 0;
        foreach (var stem in stems)
        {
            string key = Prefix + stem + ".wav";
            if (!keys.Contains(key)) continue;   // already reported as an unresolved take above
            byte[] wav = ResourceBytes(key);
            using var reader = new WaveFileReader(new MemoryStream(wav));
            if (reader.WaveFormat.SampleRate != mixFormat.SampleRate || reader.WaveFormat.Channels != mixFormat.Channels)
            { bad++; H.Fail($"{stem} ships in the bus format", $"{reader.WaveFormat.SampleRate} Hz x {reader.WaveFormat.Channels}"); continue; }

            // The same decode chain SfxEngine.LoadResource runs.
            ISampleProvider p = reader.ToSampleProvider();
            if (p.WaveFormat.Channels == 1) p = new MonoToStereoSampleProvider(p);
            if (p.WaveFormat.SampleRate != mixFormat.SampleRate) p = new WdlResamplingSampleProvider(p, mixFormat.SampleRate);
            var all = new List<float>(); var chunk = new float[8192]; int n;
            while ((n = p.Read(chunk, 0, chunk.Length)) > 0) all.AddRange(chunk.Take(n));
            var buf = all.ToArray();
            if (buf.Length == 0 || buf.Length % 2 != 0 || buf.Any(s => !float.IsFinite(s) || Math.Abs(s) > 1f))
            { bad++; H.Fail($"{stem} decodes to whole stereo frames within full scale", $"{buf.Length} samples"); continue; }

            foreach (double ratio in new[] { 1.0, 1.30, ladderTop })
            {
                int finished = 0;
                var voice = (ISampleProvider)Activator.CreateInstance(voiceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new object[] { buf, mixFormat, ratio, 1f, (Action)(() => finished++) }, null);
                var mixer = new MixingSampleProvider(mixFormat) { ReadFully = false };
                mixer.AddMixerInput(voice);
                var outBuf = new float[mixFormat.SampleRate / 10 * 2];
                long total = 0; int guard = 0; bool nonFinite = false;
                try
                {
                    int got;
                    while ((got = mixer.Read(outBuf, 0, outBuf.Length)) > 0 && guard++ < 200)
                    {
                        total += got;
                        for (int i = 0; i < got; i++) if (!float.IsFinite(outBuf[i])) nonFinite = true;
                    }
                }
                catch (Exception ex) { bad++; H.Fail($"{stem} plays at x{ratio:0.00} without an exception", ex.Message); continue; }
                double expectFrames = buf.Length / 2 / ratio;
                if (finished != 1 || nonFinite || Math.Abs(total / 2 - expectFrames) > 4)
                { bad++; H.Fail($"{stem} plays to completion at x{ratio:0.00}", $"finished {finished}x, {total / 2} frames of {expectFrames:0}"); }
            }
        }
        if (bad == 0) H.Pass("every Connate take is bus-format, decodes cleanly and plays to completion in the mixer", $"{stems.Count} stems x 3 ratios");

        var root = H.RepoRoot();
        if (root is null || !H.DevRepoOnly("Connate takes come from the Playdate folder", root)) return;
        string manifestPath = Path.Combine(root, "tools", "SfxIngest", "manifest.json");
        if (!File.Exists(manifestPath)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var wrong = new List<string>();
        foreach (var stem in stems)
        {
            JsonElement? entry = null;
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
                if (e.GetProperty("name").GetString() == stem) entry = e;
            string src = entry?.GetProperty("source").GetString() ?? "";
            const string folder = "repo:Assets/sfx/Contenders/Playdate/";
            if (!src.StartsWith(folder, StringComparison.Ordinal) || !File.Exists(Path.Combine(root, src["repo:".Length..].Replace('/', Path.DirectorySeparatorChar))))
                wrong.Add(stem);
        }
        H.Check("every Connate stem is ingested from an existing file in Assets/sfx/Contenders/Playdate", wrong.Count == 0, string.Join(", ", wrong));
    }

    private static byte[] ResourceBytes(string key)
    {
        using var stream = H.App.GetManifestResourceStream("Radiata.g.resources");
        using var reader = new ResourceReader(stream);
        reader.GetResourceData(key, out _, out byte[] data);
        int off = Array.IndexOf(data, (byte)'R');
        while (off >= 0 && off + 4 <= data.Length && !(data[off + 1] == 'I' && data[off + 2] == 'F' && data[off + 3] == 'F'))
            off = Array.IndexOf(data, (byte)'R', off + 1);
        return off < 0 ? [] : data[off..];
    }
    /// <summary>Every track a game's music bed names must be a shipped resource. A build carrying none SKIPS
    /// (the folder is optional to compile), while a build carrying some must carry ALL of them — a typo would
    /// ship a game whose music switch does nothing.</summary>
    private static void CheckMusic(HashSet<string> keys)
    {
        const string mp = "assets/music/";
        if (!keys.Any(k => k.StartsWith(mp, StringComparison.Ordinal)))
        {
            H.Skip("arcade music resolves", @"this build carries no Assets\music\*.m4a");
            return;
        }

        var tracks = ((IEnumerable)H.GetStatic(H.AppType("ArcadeMusic"), "AllTracks")).Cast<string>().ToList();
        H.Check("every bed names tracks", tracks.Count > 0, $"{tracks.Count} tracks");
        int gone = 0;
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tracks)
        {
            string key = mp + Uri.EscapeDataString(t).ToLowerInvariant() + ".m4a";
            named.Add(key);
            if (!keys.Contains(key)) { gone++; H.Fail($"{t}.m4a is a resource", key); }
        }
        if (gone == 0) H.Pass("every music track resolves to a shipped resource", $"{tracks.Count} tracks");

        // A track nothing plays is megabytes of licensed audio in every download. Spares belong in
        // Assets\music\spare\, which the non-recursive resource glob does not embed.
        var spare = keys.Where(k => k.StartsWith(mp, StringComparison.Ordinal) && !named.Contains(k)).ToList();
        H.Check("no shipped music track is unreferenced", spare.Count == 0, string.Join(", ", spare.Take(6)));
    }

    private static HashSet<string> ResourceKeys()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var stream = H.App.GetManifestResourceStream("Radiata.g.resources");
        if (stream is null) return set;
        using var reader = new ResourceReader(stream);
        foreach (DictionaryEntry e in reader) set.Add((string)e.Key);
        return set;
    }

    /// <summary>Duration straight off the WAV header of the embedded resource — no audio device involved.</summary>
    private static double Seconds(string key)
    {
        using var stream = H.App.GetManifestResourceStream("Radiata.g.resources");
        using var reader = new ResourceReader(stream);
        foreach (DictionaryEntry e in reader)
        {
            if (!string.Equals((string)e.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            reader.GetResourceData((string)e.Key, out _, out byte[] data);
            // WPF wraps the payload with a 4-byte length prefix before the RIFF header.
            int off = Array.IndexOf(data, (byte)'R');
            while (off >= 0 && off + 4 <= data.Length && !(data[off + 1] == 'I' && data[off + 2] == 'F' && data[off + 3] == 'F'))
                off = Array.IndexOf(data, (byte)'R', off + 1);
            if (off < 0) return 0;
            int rate = BitConverter.ToInt32(data, off + 24);
            short ch = BitConverter.ToInt16(data, off + 22);
            short bits = BitConverter.ToInt16(data, off + 34);
            int dataSize = data.Length - (off + 44);
            return rate > 0 && ch > 0 && bits > 0 ? dataSize / (double)(rate * ch * bits / 8) : 0;
        }
        return 0;
    }
}
