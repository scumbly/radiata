using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Facts the repository states in two places that no compiler joins. Each check parses the far side
/// from its source text and compares it with the authoritative side read from the built assemblies, so a drift
/// fails here and not on a user's machine:
/// <list type="bullet">
/// <item><c>StartupManager.Format</c> and the <c>packaging\radiata.iss</c> <c>[Registry]</c> Run entry.</item>
/// <item><c>UpdateFeed.TrustedDownloadPrefix</c>, the URL <c>release.yml</c> writes into the feed, and
/// <c>docs\INSTALLER.md</c>.</item>
/// <item><c>PortableUninstall.Files</c> and the newest installer staging folder.</item>
/// <item>The action types <c>ActionExecutor</c> accepts and <c>SequenceFormat.PrimaryFields</c>, with a payload
/// round trip per type.</item>
/// <item>The <c>ObsPort</c> / <c>MixBalance</c> ranges <c>ConfigLoader</c> clamps to, and the in-app writers of
/// those two values.</item>
/// <item>The action types and system commands <c>ActionExecutor</c> runs, and the editor's <c>Categories</c>
/// entries that keep a stored slice from being rewritten.</item>
/// </list>
/// <para>Checks that read files the public snapshot does not carry (packaging, workflows, docs, the gitignored
/// <c>publish</c> folder) run behind <see cref="H.DevRepoOnly"/>; the range check reads shipped sources and runs
/// everywhere.</para></summary>
internal static class T_Consistency
{
    private const string HelperManifestName = "Radiata.helper-files.txt";

    public static void Run()
    {
        H.Group("Consistency — facts kept in two places agree (Run key, update feed, payload manifest, range clamps)");

        var root = H.RepoRoot();
        if (root is null) { H.Fail("repo root not found"); return; }

        InstallerRunKey(root);
        FeedDownloadPrefix(root);
        PayloadManifest(root);
        RangeBounds(root);
        SequenceRoundTrip(root);
        ExecutorTypesHaveCategoryEntries(root);
    }

    // ── ActionExecutor's accepted types ↔ SequenceFormat's payload table ──────────────────────────────

    private static void SequenceRoundTrip(string root)
    {
        var src = Path.Combine(root, "Core", "ActionExecutor.cs");
        if (!File.Exists(src)) { H.Fail(@"Core\ActionExecutor.cs exists", "missing"); return; }
        var text = File.ReadAllText(src);
        int start = text.IndexOf("public ActionStatus? Execute(WheelSlice", StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf("default:", start, StringComparison.Ordinal);
        if (start < 0 || end < 0) { H.Fail("ActionExecutor.Execute's switch is locatable in source"); return; }

        var accepted = Regex.Matches(text[start..end], "case \"([a-z-]+)\":").Select(m => m.Groups[1].Value)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var known = SequenceFormat.PrimaryFields.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        H.Check("SequenceFormat.PrimaryFields knows every type the executor accepts", !accepted.Except(known).Any(),
                "missing: " + string.Join(", ", accepted.Except(known)));
        H.Check("SequenceFormat.PrimaryFields names no type the executor lacks", !known.Except(accepted).Any(),
                "extra: " + string.Join(", ", known.Except(accepted)));

        const string payload = "payload-value 42";
        foreach (var (type, field) in SequenceFormat.PrimaryFields.Where(kv => kv.Value is not null))
        {
            var prop = typeof(ActionConfig).GetProperty(field!);
            var line = $"{type}: {payload}";
            var once = SequenceFormat.Parse(line);
            var twice = SequenceFormat.Parse(SequenceFormat.ToText(once));
            H.Check($"sequence step \"{type}\" keeps its {field} through the editor round trip",
                    once.Length == 1 && twice.Length == 1
                    && (string)prop!.GetValue(once[0]) == payload
                    && (string)prop.GetValue(twice[0]) == payload
                    && SequenceFormat.ToText(twice) == line,
                    $"text: {SequenceFormat.ToText(twice)}");
        }
    }

    /// <summary>Every action type the executor runs, and every system command it runs, has a Categories entry,
    /// offered or Hidden. SelectTypeOption resolves a stored slice only through Categories; a type or command with
    /// none drops to a fallback, and the next auto-save rewrites the slice's type or command.</summary>
    private static void ExecutorTypesHaveCategoryEntries(string root)
    {
        var path = Path.Combine(root, "Core", "ActionExecutor.cs");
        if (!File.Exists(path)) { H.Fail(@"Core\ActionExecutor.cs exists", "missing"); return; }

        H.Try("every executor type and system command has a Categories entry", () =>
        {
            var lines = File.ReadAllLines(path);
            int sysStart = Array.FindIndex(lines, l => l.Contains("string? ExecuteSystem(ActionConfig"));
            int typeStart = Array.FindIndex(lines, l => l.Contains("case \"launch\":"));
            bool located = sysStart > 0 && typeStart > 0 && typeStart < sysStart;
            H.Check("the executor's type switch and ExecuteSystem were located", located, $"typeStart={typeStart} sysStart={sysStart}");
            if (!located) return;

            var caseRx = new Regex("^\\s*case \"([a-z0-9-]+)\":");
            static int Indent(string l) => l.Length - l.TrimStart().Length;

            int typeIndent = Indent(lines[typeStart]);
            var types = new SortedSet<string>();
            for (int i = typeStart; i < sysStart; i++)
            {
                var m = caseRx.Match(lines[i]);
                if (m.Success && Indent(lines[i]) == typeIndent) types.Add(m.Groups[1].Value);
            }

            // ExecuteSystem's own switch: labels at the switch body's indent, until the next member (indent 4).
            int sysIndent = Indent(lines[sysStart]) + 8;
            var commands = new SortedSet<string>();
            for (int i = sysStart + 1; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length > 1 && Indent(lines[i]) == 4) break;
                var m = caseRx.Match(lines[i]);
                if (m.Success && Indent(lines[i]) == sysIndent) commands.Add(m.Groups[1].Value);
            }
            H.Check("the executor's labels were parsed",
                    types.Contains("toggle") && types.Contains("safe-mode") && commands.Contains("media-mute") && commands.Contains("sleep"),
                    $"{types.Count} types, {commands.Count} system commands");

            var editor = H.AppType("WheelEditorControl") ?? throw new MissingMemberException("WheelEditorControl");
            var catsProp = editor.GetProperty("Categories", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? throw new MissingMemberException("WheelEditorControl.Categories");
            var opts = new List<(string Type, string Command, bool Alias)>();
            foreach (var cat in ((IEnumerable)catsProp.GetValue(null)).Cast<object>())
                foreach (var entry in ((IEnumerable)cat.GetType().GetField("Item3").GetValue(cat)).Cast<object>())
                    foreach (var opt in ((IEnumerable)entry.GetType().GetProperty("Options").GetValue(entry)).Cast<object>())
                    {
                        var ot = opt.GetType();
                        opts.Add(((string)ot.GetProperty("Type").GetValue(opt), (string)ot.GetProperty("Command").GetValue(opt),
                                  (bool)ot.GetProperty("Alias").GetValue(opt)));
                    }

            var noType = types.Where(t => !opts.Any(o => !o.Alias && o.Type == t)).ToList();
            H.Check($"all {types.Count} executor types have a Categories entry (offered or Hidden)", noType.Count == 0,
                    $"missing: {string.Join(", ", noType)}. A stored slice of that type is rewritten by the next auto-save (docs/ACTIONS.md)");
            var noCmd = commands.Where(c => !opts.Any(o => !o.Alias && o.Type == "system" && o.Command == c)).ToList();
            H.Check($"all {commands.Count} executor system commands have a Categories option (offered or Hidden)", noCmd.Count == 0,
                    $"missing: {string.Join(", ", noCmd)}. A stored slice's Command is rewritten by the next auto-save (docs/ACTIONS.md)");
        });
    }

    // ── StartupManager.Format ↔ radiata.iss [Registry] ────────────────────────────────────────────────

    private static void InstallerRunKey(string root)
    {
        if (!H.DevRepoOnly("the installer seeds the Run-key entry StartupManager writes (packaging/radiata.iss)", root)) return;

        var issPath = Path.Combine(root, "packaging", "radiata.iss");
        if (!File.Exists(issPath)) { H.Fail(@"packaging\radiata.iss exists", "missing"); return; }

        H.Try("radiata.iss [Registry] parses against StartupManager's constants", () =>
        {
            var sm = H.AppType("StartupManager") ?? throw new MissingMemberException("StartupManager");
            var valueName = (string)H.GetStatic(sm, "ValueName");
            var runKey = (string)H.GetStatic(sm, "RunKey");

            var entries = SectionLines(File.ReadAllLines(issPath), "Registry").Select(InnoParams)
                .Where(p => p.TryGetValue("Subkey", out var sub) && sub.Equals(runKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
            H.Check("the installer's [Registry] section writes exactly one entry under StartupManager's Run key",
                    entries.Count == 1, $"{entries.Count} entries under {runKey}");
            if (entries.Count != 1) return;
            var entry = entries[0];

            H.Check("the installer writes the Run entry under HKCU, as StartupManager does",
                    entry.GetValueOrDefault("Root", "").Equals("HKCU", StringComparison.OrdinalIgnoreCase),
                    $"Root: {entry.GetValueOrDefault("Root")}");
            H.Check("the installer's Run entry has StartupManager's value name",
                    entry.GetValueOrDefault("ValueName", "").Equals(valueName, StringComparison.OrdinalIgnoreCase),
                    $"iss \"{entry.GetValueOrDefault("ValueName")}\" vs StartupManager \"{valueName}\"");

            // {app} is the install directory; a space in it exercises the quoting.
            const string installDir = @"C:\Users\Test User\AppData\Local\Programs\Radiata";
            var data = entry.GetValueOrDefault("ValueData", "").Replace("{app}", installDir, StringComparison.OrdinalIgnoreCase);
            var leftover = Regex.Match(data, @"\{\w+\}");
            H.Check("the installer's Run value data resolves from {app} alone", !leftover.Success,
                    leftover.Success ? $"unresolved Inno constant {leftover.Value}" : null);
            var expected = StartupManager.Format(installDir + @"\Radiata.exe");
            H.Check("the installer's Run value data equals StartupManager.Format for the installed exe",
                    data == expected, $"iss {data}  vs  Format {expected}");
        });
    }

    /// <summary>The entry lines of one Inno Setup section: comments and blanks dropped, a trailing <c>\</c>
    /// continuation joined onto the next line.</summary>
    private static List<string> SectionLines(string[] lines, string section)
    {
        var result = new List<string>();
        var pending = new StringBuilder();
        bool inSection = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (pending.Length == 0)
            {
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    inSection = line.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inSection || line.Length == 0 || line.StartsWith(';')) continue;
            }
            if (line.EndsWith('\\')) { pending.Append(line[..^1].TrimEnd()).Append(' '); continue; }
            pending.Append(line);
            result.Add(pending.ToString());
            pending.Clear();
        }
        return result;
    }

    /// <summary>One Inno Setup entry line as <c>Name: value; Name: value</c> pairs. A value is either bare up
    /// to the next <c>;</c> or double-quoted, where <c>""</c> inside the quotes is one literal quote.</summary>
    private static Dictionary<string, string> InnoParams(string line)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < line.Length)
        {
            int colon = line.IndexOf(':', i);
            if (colon < 0) break;
            var name = line[i..colon].Trim();
            i = colon + 1;
            while (i < line.Length && line[i] == ' ') i++;

            var value = new StringBuilder();
            if (i < line.Length && line[i] == '"')
            {
                i++;
                while (i < line.Length)
                {
                    if (line[i] != '"') { value.Append(line[i++]); continue; }
                    if (i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i += 2; continue; }
                    i++;
                    break;
                }
                int next = line.IndexOf(';', i);
                i = next < 0 ? line.Length : next + 1;
            }
            else
            {
                int next = line.IndexOf(';', i);
                value.Append((next < 0 ? line[i..] : line[i..next]).Trim());
                i = next < 0 ? line.Length : next + 1;
            }
            map[name] = value.ToString();
        }
        return map;
    }

    // ── UpdateFeed.TrustedDownloadPrefix ↔ release.yml ↔ INSTALLER.md ─────────────────────────────────

    private static void FeedDownloadPrefix(string root)
    {
        if (!H.DevRepoOnly("the release workflow and the INSTALLER docs name UpdateFeed.TrustedDownloadPrefix", root)) return;

        var prefix = UpdateFeed.TrustedDownloadPrefix;

        // Each far side is found by the feed's own `url` key or by the sentence naming the constant, never by
        // the prefix's shape, so a corrupted prefix is a mismatch and not an absent match.
        var ymlPath = Path.Combine(root, ".github", "workflows", "release.yml");
        if (!File.Exists(ymlPath)) { H.Fail(@".github\workflows\release.yml exists", "missing"); return; }
        var feedUrls = File.ReadAllLines(ymlPath)
            .Where(l => l.Contains("notesUrl", StringComparison.Ordinal) && l.Contains("sha256", StringComparison.Ordinal))
            .Select(l => Regex.Match(l, @"url`?""?\s*:\s*`?""(https://[^`""\s]+)"))
            .Where(m => m.Success).Select(m => m.Groups[1].Value)
            .ToList();
        H.Check("release.yml writes the feed's url under UpdateFeed.TrustedDownloadPrefix",
                feedUrls.Count > 0 && feedUrls.All(u => u.StartsWith(prefix, StringComparison.Ordinal)),
                feedUrls.Count == 0 ? "no line of release.yml writes a feed with url, sha256 and notesUrl keys"
                                    : $"workflow {string.Join(" | ", feedUrls)}  vs  constant {prefix}");

        // Both copies carry the feed example: the development doc and the public page (public-docs/INSTALLER.md,
        // which publishes as docs/INSTALLER.md).
        foreach (var rel in new[] { "docs/INSTALLER.md", "public-docs/INSTALLER.md" })
        {
            var docPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(docPath)) { H.Fail($"{rel} exists", "missing"); continue; }
            var doc = File.ReadAllText(docPath);
            var sampleUrls = Regex.Matches(doc, @"""url"":""(https://[^""]+)""").Select(m => m.Groups[1].Value).ToList();
            H.Check($"the feed example in {rel} has a url under UpdateFeed.TrustedDownloadPrefix",
                    sampleUrls.Count > 0 && sampleUrls.All(u => u.StartsWith(prefix, StringComparison.Ordinal)),
                    sampleUrls.Count == 0 ? "no \"url\" in the doc's feed example"
                                          : $"doc {string.Join(" | ", sampleUrls)}  vs  constant {prefix}");
            var stated = Regex.Matches(doc, @"(https://[^\s`*""]+)[`*\s]*\(\s*`UpdateFeed\.TrustedDownloadPrefix`")
                .Select(m => m.Groups[1].Value).ToList();
            H.Check($"{rel} states UpdateFeed.TrustedDownloadPrefix as the same prefix",
                    stated.Count > 0 && stated.All(u => u == prefix),
                    stated.Count == 0 ? "no sentence in the doc gives a URL followed by (UpdateFeed.TrustedDownloadPrefix)"
                                      : $"doc {string.Join(" | ", stated)}  vs  constant {prefix}");
        }
    }

    // ── PortableUninstall.Files ↔ the shipped payload ─────────────────────────────────────────────────

    /// <summary>Names the portable manifest carries that the installer payload never does: the debug symbols
    /// the installer build strips, the portable uninstall command it removes, and the portable ZIP's README.</summary>
    private static readonly string[] PortableOnly = ["Radiata.pdb", "Uninstall Radiata.cmd", "README - Install Radiata.txt"];

    private static void PayloadManifest(string root)
    {
        if (!H.DevRepoOnly(@"PortableUninstall.Files matches the newest installer staging folder (publish\installer-staging*)", root)) return;

        var publish = Path.Combine(root, "publish");
        var staging = !Directory.Exists(publish) ? null
            : new DirectoryInfo(publish).GetDirectories("installer-staging*")
                .Where(d => File.Exists(Path.Combine(d.FullName, "Radiata.exe")))
                .OrderByDescending(d => d.CreationTimeUtc)
                .FirstOrDefault();
        if (staging is null)
        {
            H.Skip("PortableUninstall.Files matches the installer payload",
                   @"no publish\installer-staging* folder holding a Radiata.exe (gitignored); make one with tools\build-installer.ps1 -StageOnly");
            return;
        }
        Console.WriteLine($"        (record) staging folder: {staging.Name}, created {staging.CreationTime:yyyy-MM-dd HH:mm}");

        var ps1Path = Path.Combine(root, "tools", "build-installer.ps1");
        if (!File.Exists(ps1Path)) { H.Fail(@"tools\build-installer.ps1 exists", "missing"); return; }
        var record = Regex.Match(File.ReadAllText(ps1Path), @"\$stagingRecordName\s*=\s*'([^']+)'");
        if (!record.Success) { H.Fail(@"build-installer.ps1 names its staging record file ($stagingRecordName)", "marker not found — renamed?"); return; }

        H.Try("PortableUninstall.Files and the staging folder agree", () =>
        {
            var files = new HashSet<string>(PortableUninstall.Files.Select(f => f.Replace('/', '\\')), StringComparer.OrdinalIgnoreCase);
            var ownedDirs = ((string[])H.GetStatic(H.AppType("App") ?? throw new MissingMemberException("App"), "OwnedDirs"))
                .Select(d => d.Replace('/', '\\').TrimEnd('\\') + @"\").ToList();
            var coordinator = H.AppType("ScriptSessionCoordinator") ?? throw new MissingMemberException("ScriptSessionCoordinator");
            var helperDir = (string)H.GetStatic(coordinator, "HelperSubdirName") + @"\";
            var devLayoutHelper = (string[])H.GetStatic(coordinator, "HelperFiles");

            var manifestPath = Path.Combine(staging.FullName, HelperManifestName);
            var helperListed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(manifestPath))
                foreach (var line in File.ReadAllLines(manifestPath))
                {
                    int bar = line.IndexOf('|');
                    if (bar > 0) helperListed.Add(line[(bar + 1)..].Trim());
                }
            H.Check("the staging folder carries the helper hash manifest", File.Exists(manifestPath), HelperManifestName);

            // Everything in the folder is removed by name from Files, by hash from the helper manifest, or is the
            // manifest itself / the staging record (the installer build moves that out before packing).
            var uncovered = new List<string>();
            foreach (var file in Directory.EnumerateFiles(staging.FullName, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(staging.FullName, file);
                if (rel.Equals(HelperManifestName, StringComparison.OrdinalIgnoreCase)
                    || rel.Equals(record.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) continue;
                bool covered = rel.StartsWith(helperDir, StringComparison.OrdinalIgnoreCase)
                    ? helperListed.Contains(rel)
                    : files.Contains(rel) || ownedDirs.Any(d => rel.StartsWith(d, StringComparison.OrdinalIgnoreCase));
                if (!covered) uncovered.Add(rel);
            }
            H.Check("every file in the newest installer staging is removed by PortableUninstall.Files or the helper manifest",
                    uncovered.Count == 0,
                    uncovered.Count == 0 ? $"{staging.Name}"
                        : $"{uncovered.Count} uncovered in {staging.Name}: {string.Join(", ", uncovered.Take(5))}"
                          + (uncovered.Count > 5 ? $", +{uncovered.Count - 5} more" : "")
                          + " — a staging folder older than the last payload change reads this way too");

            // Every name the manifest holds for the installer payload ships. A portable-only name or a
            // development-layout helper name is legitimately absent from a published payload.
            var shippedNames = files.Where(f => !PortableOnly.Contains(f, StringComparer.OrdinalIgnoreCase)
                                                && !devLayoutHelper.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
            var missing = shippedNames.Where(f => !File.Exists(Path.Combine(staging.FullName, f))).ToList();
            H.Check("every PortableUninstall.Files entry the installer ships exists in the newest installer staging",
                    missing.Count == 0,
                    missing.Count == 0 ? $"{shippedNames.Count} entries, {staging.Name}"
                                       : $"absent from {staging.Name}: {string.Join(", ", missing)}");

            var unnamed = devLayoutHelper.Where(f => !files.Contains(f)).ToList();
            H.Check("PortableUninstall.Files names every loose development-layout helper file ScriptSessionCoordinator stages",
                    unnamed.Count == 0, unnamed.Count == 0 ? null : $"not in the manifest: {string.Join(", ", unnamed)}");
        });
    }

    // ── ConfigLoader's ObsPort / MixBalance clamps ↔ the in-app writers ───────────────────────────────

    private static void RangeBounds(string root)
    {
        // Core's side is read by feeding the real sanitiser values past either end, so the check follows
        // the clamp itself and not the literal it is written with.
        (int Lo, int Hi) CoreRange(Func<SystemConfig, int> read, string key)
        {
            int Probe(int v)
            {
                var cfg = ConfigLoader.TryParse("{\"system\":{\"" + key + "\":" + v + "}}")
                          ?? throw new InvalidOperationException($"ConfigLoader.TryParse rejected a config carrying only {key}");
                return read(cfg.System);
            }
            return (Probe(int.MinValue), Probe(int.MaxValue));
        }

        // The numeric bounds the first pattern matches in a source file, as (lo, hi).
        (int Lo, int Hi)? ShellRange(string file, string pattern)
        {
            var path = Path.Combine(root, file);
            if (!File.Exists(path)) { H.Fail($"{file} exists", "missing"); return null; }
            var m = Regex.Match(File.ReadAllText(path), pattern);
            return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
        }

        H.Try("ConfigLoader's ObsPort and MixBalance clamps match their in-app writers", () =>
        {
            var obs = CoreRange(s => s.ObsPort, "obsPort");
            var mix = CoreRange(s => s.MixBalance, "mixBalance");

            var obsWindow = ShellRange("Integrations/ObsSetupWindow.xaml.cs","""PortBox\.Text[^;]*?\bis\s*>=\s*(\d+)\s+and\s+<=\s*(\d+)""");
            H.Check("the OBS setup window accepts exactly the port range ConfigLoader clamps ObsPort to",
                    obsWindow == obs,
                    obsWindow is null ? "no PortBox range check found in Integrations/ObsSetupWindow.xaml.cs — moved or reworded?"
                                      : $"window {obsWindow.Value.Lo}-{obsWindow.Value.Hi}  vs  ConfigLoader {obs.Lo}-{obs.Hi}");

            var mixStep = ShellRange("App.xaml.cs", """Math\.Clamp\(\s*_mixBalance\s*\+\s*delta\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)""");
            H.Check("the D-pad mixer steps within the range ConfigLoader clamps MixBalance to",
                    mixStep == mix,
                    mixStep is null ? "no _mixBalance + delta clamp found in App.xaml.cs — moved or reworded?"
                                    : $"App {mixStep.Value.Lo}-{mixStep.Value.Hi}  vs  ConfigLoader {mix.Lo}-{mix.Hi}");

            var mixSeed = ShellRange("App.xaml.cs", """Math\.Clamp\(\s*_config\.Current\.System\.MixBalance\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)""");
            H.Check("the D-pad mixer seeds its balance within the range ConfigLoader clamps MixBalance to",
                    mixSeed == mix,
                    mixSeed is null ? "no System.MixBalance seed clamp found in App.xaml.cs — moved or reworded?"
                                    : $"App {mixSeed.Value.Lo}-{mixSeed.Value.Hi}  vs  ConfigLoader {mix.Lo}-{mix.Hi}");
        });
    }
}
