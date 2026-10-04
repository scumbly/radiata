using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Radiata.TestHarness;

/// <summary>Packaging &amp; uninstall — the parts that are decidable without cutting a ZIP or
/// running the uninstaller.
///
/// The short tester README (5 sections only, no version title or footer) names the version in exactly ONE
/// spot — the setup-exe filename mention in step 1 — and packaging is a MANUAL ritual
/// (docs/BUILD-RELEASE.md), so the useful guard is: the template carries exactly one <c>{VERSION}</c> token,
/// there, and a substitution leaves none behind.
///
/// The uninstaller itself is NOT run here — it removes machine-global state (Run key, HidHide allow-list
/// entry, the recovery task) and can uninstall shared drivers. What IS exercised is
/// <c>IsDeletableAppFolder</c>, the guard that stops the self-delete from wiping a drive root or a profile
/// folder when someone extracts the ZIP's contents instead of the folder — the single most damaging line in
/// the flow, and a pure function.</summary>
internal static class T_Package
{
    public static void Run()
    {
        H.Group("Packaging & uninstall — README version, delete-manifest, folder guard");

        var root = H.RepoRoot();
        if (root is null) { H.Fail("repo root not found"); return; }

        ReadmeTemplate(root);
        UninstallerEntryPoint(root);
        DeleteManifest();
        FolderGuard();
        Autostart(root);
        PublicMirrorAllowList(root);
    }

    /// <summary>Every unconditional &lt;Resource Include&gt; in the csproj must be covered by
    /// tools/export-public.ps1's allow-list, or the next public-mirror export fails to compile — the
    /// script's own build guard catches it, but only by trying to build the mirror.
    ///
    /// <para>A literal entry is matched directly. A glob entry is EXPANDED against the files on disk and each
    /// expanded file must match an allow-list pattern: a glob nothing in the allow-list covers compiles in
    /// development and quietly ships an empty set in the mirror.
    /// A path the script's own comments name as deliberately absent is a documented exclusion, not a gap.</para>
    ///
    /// <para>Development repository only: the export script is not in the public snapshot.</para></summary>
    private static void PublicMirrorAllowList(string root)
    {
        if (!H.DevRepoOnly("public-mirror allow-list covers the csproj resources (tools/export-public.ps1)", root)) return;

        var csprojPath = Path.Combine(root, "ControllerWheel.csproj");
        var ps1Path = Path.Combine(root, "tools", "export-public.ps1");
        if (!File.Exists(csprojPath)) { H.Fail("ControllerWheel.csproj exists", "missing"); return; }
        if (!File.Exists(ps1Path)) { H.Fail(@"tools\export-public.ps1 exists", "missing"); return; }

        XDocument csproj;
        try { csproj = XDocument.Load(csprojPath); }
        catch (Exception ex) { H.Fail("ControllerWheel.csproj parses as XML", ex.Message); return; }

        // A Condition="" entry is optional-at-build (the file may not exist yet) or deliberately held back,
        // so it is not a silent gap; only unconditional entries are checked.
        var entries = csproj.Descendants().Where(e => e.Name.LocalName == "Resource")
            .Where(e => e.Attribute("Condition") is null && e.Ancestors().All(a => a.Attribute("Condition") is null))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v.Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var literals = entries.Where(v => !v.Contains('*')).ToList();
        var csprojGlobs = entries.Where(v => v.Contains('*')).ToList();

        var ps1Text = File.ReadAllText(ps1Path);
        var arrayStart = ps1Text.IndexOf("$include = @(", StringComparison.Ordinal);
        if (arrayStart < 0) { H.Fail(@"tools\export-public.ps1 has an $include allow-list", "marker not found — renamed?"); return; }
        var arrayEnd = ps1Text.IndexOf("\n)", arrayStart, StringComparison.Ordinal);
        var arrayBlock = arrayEnd > arrayStart ? ps1Text[arrayStart..arrayEnd] : ps1Text[arrayStart..];

        var globs = arrayBlock.Split('\n').SelectMany(QuotedStrings).ToList();
        H.Check(@"tools\export-public.ps1's $include allow-list parses to a set of patterns", globs.Count > 5, $"{globs.Count} patterns");

        var patterns = globs.Select(g => new Regex(GlobToRegex(g), RegexOptions.IgnoreCase)).ToList();

        // A path or glob the script's own comment names as deliberately absent ("<path> is NOT listed" or
        // "<path> is deliberately NOT listed") is a documented exclusion, not a gap. A documented glob such as
        // Assets/sfx/arcade/*.wav excludes every file it matches; a documented literal excludes that file.
        var documented = Regex.Matches(ps1Text, @"([\w./\\*-]+\.[\w*]+)\s+is\s+(?:deliberately\s+)?NOT\s+listed")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(g => new Regex(GlobToRegex(g), RegexOptions.IgnoreCase))
            .ToList();

        foreach (var path in literals)
        {
            if (documented.Any(d => d.IsMatch(path))) continue;
            bool covered = patterns.Any(p => p.IsMatch(path));
            H.Check($"export-public.ps1 allow-list covers <Resource Include=\"{path}\">", covered,
                    covered ? null : "no $include pattern matches — the next public-mirror export will fail to build");
        }

        foreach (var glob in csprojGlobs)
        {
            var files = ExpandGlob(root, glob);
            var exempt = files.Where(f => documented.Any(d => d.IsMatch(f))).ToList();
            var uncovered = files.Except(exempt, StringComparer.OrdinalIgnoreCase)
                                 .Where(f => !patterns.Any(p => p.IsMatch(f)))
                                 .ToList();
            string detail = uncovered.Count > 0
                ? $"{uncovered.Count} of {files.Count} files match no $include pattern (the mirror would compile without them): "
                  + string.Join(", ", uncovered.Take(5)) + (uncovered.Count > 5 ? $", +{uncovered.Count - 5} more" : "")
                : exempt.Count > 0 ? $"{files.Count} files on disk, {exempt.Count} documented as deliberately not exported"
                                   : $"{files.Count} files on disk";
            H.Check($"export-public.ps1 allow-list covers every file <Resource Include=\"{glob}\"> expands to", uncovered.Count == 0, detail);
        }
    }

    /// <summary>The single-quoted strings on one script line, ignoring a whole-line or trailing '#' comment.
    /// A comment can carry an apostrophe; pairing quotes across the raw text would let one pair it with the
    /// next entry's opening quote and shift every later entry out of step.</summary>
    private static IEnumerable<string> QuotedStrings(string line)
    {
        var sb = new StringBuilder();
        bool inQuote = false;
        foreach (char c in line)
        {
            if (!inQuote && c == '#') yield break;
            if (c == '\'')
            {
                if (inQuote) { yield return sb.ToString(); sb.Clear(); }
                inQuote = !inQuote;
                continue;
            }
            if (inQuote) sb.Append(c);
        }
    }

    /// <summary>The files a csproj glob matches on disk, as repo-relative forward-slash paths. The directory
    /// before the first wildcard segment is walked (recursively when the glob uses '**', otherwise just that
    /// directory) and each candidate is filtered with the same glob semantics the allow-list uses, so the
    /// expansion and the allow-list can never disagree about what '*' means. A directory that does not exist
    /// expands to nothing, matching MSBuild's empty glob.</summary>
    private static List<string> ExpandGlob(string root, string glob)
    {
        var segments = glob.Split('/');
        int firstWild = Array.FindIndex(segments, s => s.Contains('*'));
        string baseRel = string.Join("/", segments.Take(Math.Max(firstWild, 0)));
        string baseDir = baseRel.Length == 0 ? root : Path.Combine(root, baseRel.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(baseDir)) return new List<string>();

        bool recursive = glob.Contains("**", StringComparison.Ordinal);
        var rx = new Regex(GlobToRegex(glob), RegexOptions.IgnoreCase);
        return Directory.EnumerateFiles(baseDir, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => rx.IsMatch(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The same glob semantics as export-public.ps1's own ConvertTo-PathRegex: '*' matches within
    /// one path segment, '**' matches across segments, everything else is literal. '**' is substituted
    /// before '*' so the wildcard pass cannot eat the placeholder.</summary>
    private static string GlobToRegex(string glob)
    {
        const string placeholder = "\u0001";
        var re = Regex.Escape(glob).Replace(@"\*\*", placeholder).Replace(@"\*", "[^/]*").Replace(placeholder, ".*");
        return "^" + re + "$";
    }

    /// <summary>The two autostart routes agree on their contracts: the Run-key value format the app writes
    /// is the one the installer seeds, the parser reads both it and the older bare path, and the logon
    /// task's XML carries the settings that keep a resident app alive (no battery stop, no 72 h kill).</summary>
    private static void Autostart(string root)
    {
        var sm = H.AppType("StartupManager");
        var parse  = H.StaticMethod(sm, "ParseTarget", 1);
        var format = H.StaticMethod(sm, "Format", 1);
        if (parse is null || format is null) { H.Fail("StartupManager.ParseTarget / Format not found", "renamed?"); return; }
        string Parse(string v) => (string)parse.Invoke(null, new object[] { v });
        string Format(string exe) => (string)format.Invoke(null, new object[] { exe });

        const string exe = @"C:\Users\x\AppData\Local\Programs\Radiata\Radiata.exe";
        H.Check("Run-key format is the quoted exe + --autostart", Format(exe) == $"\"{exe}\" --autostart", Format(exe));
        H.Check("ParseTarget reads the current format back", Parse(Format(exe)) == exe);
        H.Check("ParseTarget reads the older bare quoted path", Parse($"\"{exe}\"") == exe);
        H.Check("ParseTarget reads an unquoted path with no switch", Parse(@"C:\R\Radiata.exe") == @"C:\R\Radiata.exe");

        var iss = File.ReadAllText(Path.Combine(root, "packaging", "radiata.iss"));
        H.Check("radiata.iss seeds the SAME Run-key format (\"{app}\\Radiata.exe\" --autostart)",
                iss.Contains("ValueData: \"\"\"{app}\\Radiata.exe\"\" --autostart\""));

        H.Check("radiata.iss PrepareToInstall signals the quit event and waits for Radiata.exe + ArcadeHost",
                iss.Contains("function PrepareToInstall(") && iss.Contains("'Local\\Radiata.QuitRequest'")
                && iss.Contains("{app}\\ArcadeHost\\Radiata.ArcadeHost.exe") && iss.Contains("CloseApplications=yes"));
        var appSrc = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        H.Check("App listens on the same quit event the installer signals",
                appSrc.Contains("QuitEventName     = @\"Local\\Radiata.QuitRequest\""));

        var rt = H.AppType("RecoveryTask");
        var xmlM = H.StaticMethod(rt, "TaskXml", 3);
        if (xmlM is null) { H.Fail("RecoveryTask.TaskXml not found", "renamed?"); return; }
        var xml = (string)xmlM.Invoke(null, new object[] { exe, "S-1-5-21-1-2-3-1001", "BOX\\user" });
        H.Check("logon task runs --logon", xml.Contains("<Arguments>--logon</Arguments>") && xml.Contains($"<Command>{exe}</Command>"));
        H.Check("logon task: interactive token, least privilege",
                xml.Contains("<LogonType>InteractiveToken</LogonType>") && xml.Contains("<RunLevel>LeastPrivilege</RunLevel>"));
        H.Check("logon task: no battery stop/refusal",
                xml.Contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>")
                && xml.Contains("<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>"));
        H.Check("logon task: no execution time limit, no hard terminate",
                xml.Contains("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>") && xml.Contains("<AllowHardTerminate>false</AllowHardTerminate>"));
        H.Check("logon task: normal priority (schtasks defaults to below normal)", xml.Contains("<Priority>5</Priority>"));
        H.Check("logon task: this user's logon only", xml.Split("<UserId>S-1-5-21-1-2-3-1001</UserId>").Length == 3);

        var qm = H.AppType("QuitMarker");
        var sid = H.StaticMethod(qm, "LogonSessionId", 0);
        if (sid is null) { H.Fail("QuitMarker.LogonSessionId not found", "renamed?"); return; }
        var id = sid.Invoke(null, null);
        H.Check("QuitMarker resolves this logon session's id", id is ulong v && v != 0, id?.ToString());
    }

    private static void ReadmeTemplate(string root)
    {
        var path = Path.Combine(root, "packaging", "README-Install-Radiata.txt");
        if (!File.Exists(path)) { H.Fail("packaging\\README-Install-Radiata.txt is missing", path); return; }

        var bytes = File.ReadAllBytes(path);
        H.Check("the README template is BOM-less UTF-8 (or em-dashes mojibake)",
                !(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                bytes.Length >= 3 ? $"first bytes {bytes[0]:X2} {bytes[1]:X2} {bytes[2]:X2}" : null);

        var text  = File.ReadAllText(path, new UTF8Encoding(false));
        var lines = text.Split('\n');
        int tokens = text.Split("{VERSION}").Length - 1;
        H.Check("the README template carries exactly 1 {VERSION} token", tokens == 1, $"{tokens} found");

        var hits = Enumerable.Range(0, lines.Length).Where(i => lines[i].Contains("{VERSION}")).ToList();
        Console.WriteLine("        (record) {VERSION} lines: "
                          + string.Join(" | ", hits.Select(i => $"L{i + 1}: {lines[i].Trim()}")));

        H.Check("…it's the setup-exe filename mention",
                hits.Any(i => lines[i].Contains("-setup.exe", StringComparison.OrdinalIgnoreCase)));
        // Deliberately no title-line or footer check: the short README carries neither a version title nor a
        // version footer.

        // Substituting the version this build reports must leave nothing behind, and must produce the
        // documented installer name — Radiata-0.<minor>.<build>-setup.exe (the .YYMMDD field is dropped).
        var info = Assembly.Load("Radiata").GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? "0.0.0.000000";
        var ver  = string.Join(".", info.Split('+')[0].Split('.').Take(3));
        var subbed = text.Replace("{VERSION}", ver);
        Console.WriteLine($"        (record) this build reports {info} → <ver> = {ver}");
        H.Check("substitution leaves no {VERSION} behind", !subbed.Contains("{VERSION}"));
        H.Check("the setup-exe filename line reads the documented name",
                subbed.Contains($"Radiata-{ver}-setup.exe"),
                subbed.Split('\n').FirstOrDefault(l => l.Contains("-setup.exe"))?.Trim());
        H.Check("every substituted line carries the SAME version (no half-updated README)",
                subbed.Split('\n').Where(l => l.Contains("Radiata-") || l.Contains("RADIATA "))
                      .All(l => !l.Contains("{VERSION}")));

        // A stale hard-coded version anywhere in the template would survive substitution unnoticed.
        var stale = subbed.Split('\n')
            .Select((l, i) => (l, i))
            .Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.l, @"\b0\.\d+\.\d+")
                        && !x.l.Contains(ver))
            .Take(4).ToList();
        H.Check("no hard-coded version other than the substituted one",
                stale.Count == 0,
                stale.Count == 0 ? null : string.Join(" | ", stale.Select(x => $"L{x.i + 1}: {x.l.Trim()}")));
    }

    private static void UninstallerEntryPoint(string root)
    {
        var cmd = Path.Combine(root, "Uninstall Radiata.cmd");
        if (!File.Exists(cmd)) { H.Fail("Uninstall Radiata.cmd is missing"); return; }
        var text = File.ReadAllText(cmd);
        H.Check("the uninstaller .cmd invokes Radiata.exe --uninstall",
                text.Contains("--uninstall") && text.Contains("Radiata.exe"));
        H.Check("…using its own folder (%~dp0), not a fixed path", text.Contains("%~dp0"));
        H.Check("…and does not pass --drivers itself (opt-in belongs to the dialog)",
                !text.Contains("--drivers"));
    }

    private static void DeleteManifest()
    {
        var app = H.AppType("App");
        var files = app?.GetField("OwnedFiles", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(null) as string[];
        var dirs  = app?.GetField("OwnedDirs", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(null) as string[];
        if (files is null || dirs is null) { H.Fail("App.OwnedFiles / OwnedDirs not found", "renamed?"); return; }

        Console.WriteLine($"        (record) delete manifest: files [{string.Join(", ", files)}] dirs [{string.Join(", ", dirs)}]");
        // Uninstall works from a MANIFEST rather than a recursive wipe — so the manifest must name every
        // shipped entry, and nothing else.
        foreach (var expected in new[] { "Radiata.exe", "Uninstall Radiata.cmd", "README - Install Radiata.txt" })
            H.Check($"the manifest names \"{expected}\"", files.Contains(expected));
        H.Check("no directory is recursively owned", dirs.Length == 0);
        H.Check("the real worker uses this exact file manifest", files.SequenceEqual(ControllerWheel.PortableUninstall.Files));
        H.Check("the manifest contains only confined literal relative paths",
                files.All(f => !Path.IsPathRooted(f) && !f.Contains('*') && !f.Contains('?') && !f.Split('\\', '/').Contains("..")));
    }

    private static void FolderGuard()
    {
        var app = H.AppType("App");
        var guard = H.StaticMethod(app, "IsDeletableAppFolder", 1);
        if (guard is null) { H.Fail("App.IsDeletableAppFolder(dir) not found", "renamed? the self-delete guard is untested"); return; }
        bool Deletable(string dir) => (bool)guard.Invoke(null, new object[] { dir });

        // Every refusal below is a location a tester could plausibly extract the ZIP's CONTENTS into. Each is
        // given a Radiata.exe first, so the ONLY thing that can save it is the location check itself.
        var tmp = Path.Combine(Path.GetTempPath(), "radiata-harness", "guard-" + Guid.NewGuid().ToString("N")[..6]);
        var real = Path.Combine(tmp, "Radiata-0.12.505");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "Radiata.exe"), new byte[16]);
        try
        {
            H.Check("a normal install folder containing Radiata.exe IS deletable", Deletable(real), real);

            var noExe = Path.Combine(tmp, "NotOurs");
            Directory.CreateDirectory(noExe);
            H.Check("a folder WITHOUT Radiata.exe is refused", !Deletable(noExe));

            foreach (var (what, dir) in new (string, string)[]
            {
                ("a drive root",      Path.GetPathRoot(Environment.SystemDirectory)),
                ("%WINDIR%",          Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
                ("System32",          Environment.GetFolderPath(Environment.SpecialFolder.System)),
                ("Program Files",     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
                ("Program Files x86", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
                ("the user profile",  Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
                ("Desktop",           Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
                ("Documents",         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                ("%APPDATA%",         Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                ("%LOCALAPPDATA%",    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                ("%PROGRAMDATA%",     Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
                ("an empty path",     ""),
                ("whitespace",        "   "),
            })
                H.Check($"{what} is refused even with a Radiata.exe in it", !Deletable(dir), dir);

            // Trailing-separator and case variants of a refused root must ALSO be refused.
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            H.Check("a refused root with a trailing backslash is still refused", !Deletable(profile + "\\"));
            H.Check("a refused root in a different case is still refused", !Deletable(profile.ToUpperInvariant()));
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }
}
