using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.Json;

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
