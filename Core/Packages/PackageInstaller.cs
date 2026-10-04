using System.Diagnostics;
using System.IO.Compression;

namespace ControllerWheel;

/// <summary>
/// Drag-and-drop install for drop-in packages: copy a dropped folder or ZIP into a staging folder, validate
/// it exactly as <see cref="PackageStore.Scan"/> would, and move it into its kind's folder once the shell
/// has the user's approval. See docs/PACKAGES.md ▸ Installing and removing.
///
/// Packages are flat by format (the consent hash and every file reference cover top-level files only), so
/// staging copies top-level files and nothing else. Every cap is enforced on the bytes actually copied,
/// never on a size the ZIP declares. ZIP entry names are untrusted: the leaf is taken from FullName split
/// on both separators, and must be a plain file name — traversal is refused, not normalized.
///
/// Nothing here throws, executes, or decodes package content beyond <see cref="PackageStore.Inspect"/>.
/// </summary>
public static class PackageInstaller
{
    /// <summary>The same folder-shape caps <c>PackageStore.HashFolder</c> enforces at scan.</summary>
    public const int MaxFiles = 32;
    public const long MaxFileBytes = 4L * 1024 * 1024;
    public const long MaxTotalBytes = 16L * 1024 * 1024;
    private const int MaxFolderNameLength = 64;

    /// <summary>Under <see cref="PackageStore.PackagesDir"/> (so a commit is a same-volume move) but outside
    /// both kind folders, so a scan never sees a half-staged package.</summary>
    public static string StagingDir => Path.Combine(PackageStore.PackagesDir, ".staging");

    /// <summary>A validated package waiting in staging. <see cref="Package"/> was inspected in a folder
    /// already named <see cref="FolderName"/>, so its consent identity matches the committed copy.</summary>
    public sealed record Staged(string Kind, string FolderName, string StageRoot, PackageStore.Discovered Package)
    {
        public string Token => TokenOf(Package) ?? "";
        public string DisplayName => Package.Material?.Name ?? Package.ArcadeGame?.Title ?? FolderName;
    }

    /// <summary>Whether a dropped path is shaped like a package: a folder, or a <c>.zip</c> file.</summary>
    public static bool IsCandidate(string path)
    {
        try
        {
            return Directory.Exists(path)
                || (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    public static string KindDir(string kind) => kind == "material" ? PackageStore.MaterialsDir : PackageStore.ArcadeDir;

    public static string? TokenOf(PackageStore.Discovered d) => d.Material?.Token ?? d.ArcadeGame?.Token;

    /// <summary>Copy <paramref name="source"/> (a folder or a .zip) into staging and validate it. Returns null
    /// with a user-readable <paramref name="error"/> on any failure, leaving nothing staged.
    /// <paramref name="kindOffered"/> answers whether this build offers a kind (the release gates).</summary>
    public static Staged? Stage(string source, Func<string, bool> kindOffered, out string? error)
    {
        string root = Path.Combine(StagingDir, Guid.NewGuid().ToString("N"));
        try
        {
            string full = Path.GetFullPath(source);
            if (IsUnder(full, PackageStore.PackagesDir))
            {
                error = "it's already inside Radiata's Packages folder";
                return null;
            }

            string? name;
            string provisional = Path.Combine(root, ".provisional");   // a sanitized name never starts with a dot
            Directory.CreateDirectory(provisional);
            if (Directory.Exists(full))
            {
                name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                error = CopyFolder(full, provisional);
            }
            else
            {
                name = null;
                error = CopyZip(full, provisional, out name);
                name ??= Path.GetFileNameWithoutExtension(full);
            }
            if (error is not null) { Discard(root); return null; }

            var files = Directory.GetFiles(provisional).Select(Path.GetFileName).ToList();
            bool isMaterial = files.Contains(MaterialPackage.ManifestFileName, StringComparer.OrdinalIgnoreCase);
            bool isGame = files.Contains(ScriptGameManifest.ManifestFileName, StringComparer.OrdinalIgnoreCase);
            if (isMaterial == isGame)
            {
                error = isMaterial
                    ? $"it holds both {MaterialPackage.ManifestFileName} and {ScriptGameManifest.ManifestFileName}"
                    : $"it isn't a Radiata package (no {MaterialPackage.ManifestFileName} or {ScriptGameManifest.ManifestFileName})";
                Discard(root); return null;
            }
            string kind = isMaterial ? "material" : "arcade";
            if (!kindOffered(kind))
            {
                error = "this build doesn't offer that kind of package";
                Discard(root); return null;
            }

            // Inspect under the final folder name: the consent identity is folder + hash.
            string folder = SanitizeFolderName(name);
            if (folder.Length == 0)
            {
                var probe = PackageStore.Inspect(provisional, kind);
                folder = SanitizeFolderName(TokenOf(probe) is { } t ? t[(t.IndexOf('-') + 1)..] : null);
                if (folder.Length == 0) folder = "package";
            }
            string named = Path.Combine(root, folder);
            Directory.Move(provisional, named);
            var found = PackageStore.Inspect(named, kind);
            if (found.Error is not null || TokenOf(found) is null)
            {
                error = found.Error ?? "it didn't validate";
                Discard(root); return null;
            }
            error = null;
            return new Staged(kind, folder, root, found);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Discard(root);
            return null;
        }
    }

    /// <summary>Every installed folder this package would collide with: a folder of the same name in its
    /// kind's folder, plus any folder in <paramref name="onDisk"/> carrying the same token. Pass what is on
    /// disk now (<see cref="PackageStore.Scan"/>), never the run's startup inventory: a replace that waits
    /// for a restart leaves that inventory stale, and a stale one hides the installed copy. Empty when the
    /// package is new.</summary>
    public static IReadOnlyList<string> FindConflicts(Staged s, IEnumerable<PackageStore.Discovered> onDisk)
    {
        var found = new List<string>();
        var target = Path.Combine(KindDir(s.Kind), s.FolderName);
        if (Directory.Exists(target)) found.Add(target);
        foreach (var k in onDisk)
            if (k.Kind == s.Kind && string.Equals(TokenOf(k), s.Token, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(k.FolderPath)
                && !found.Contains(k.FolderPath, StringComparer.OrdinalIgnoreCase))
                found.Add(k.FolderPath);
        return found;
    }

    /// <summary>Move a staged package into its kind's folder and inspect it there. The caller has already
    /// removed any conflicting folder. Null with <paramref name="error"/> on failure; staging is cleaned
    /// either way.</summary>
    public static PackageStore.Discovered? Commit(Staged s, out string? error)
    {
        try
        {
            var dir = KindDir(s.Kind);
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, s.FolderName);
            if (Directory.Exists(target) || File.Exists(target))
            {
                error = $"a folder named \"{s.FolderName}\" is still in the way";
                return null;
            }
            Directory.Move(Path.Combine(s.StageRoot, s.FolderName), target);
            var found = PackageStore.Inspect(target, s.Kind);
            if (found.Error is not null)
            {
                error = found.Error;
                return null;
            }
            error = null;
            return found;
        }
        catch (Exception ex) { error = ex.Message; return null; }
        finally { Discard(s.StageRoot); }
    }

    public static void Discard(Staged s) => Discard(s.StageRoot);

    /// <summary>Remove anything a previous run left in staging. Never throws.</summary>
    public static void CleanStaging()
    {
        try { if (Directory.Exists(StagingDir)) Directory.Delete(StagingDir, recursive: true); }
        catch (Exception ex) { Trace.WriteLine($"[Packages] staging cleanup failed: {ex.Message}"); }
    }

    /// <summary>A folder name safe on Windows: invalid characters dropped, trimmed of spaces and dots, no
    /// leading dot (staging and hidden folders), DOS device names suffixed, at most 64 characters. Empty
    /// when nothing usable is left.</summary>
    public static string SanitizeFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var s = new string(name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray());
        s = s.Trim().Trim('.').Trim();
        if (s.Length > MaxFolderNameLength) s = s[..MaxFolderNameLength].TrimEnd(' ', '.');
        var stem = s.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
            s += "-package";
        return s;
    }

    // ── copying ──────────────────────────────────────────────────────────────────────────────────────

    private static string? CopyFolder(string src, string dest)
    {
        if ((File.GetAttributes(src) & FileAttributes.ReparsePoint) != 0)
            return "it's a link to another folder, not a folder";
        int count = 0; long total = 0;
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0) continue;
            if (++count > MaxFiles) return $"it has over {MaxFiles} files";
            using var input = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (CopyCapped(input, Path.Combine(dest, Path.GetFileName(f)), Path.GetFileName(f), ref total) is { } err)
                return err;
        }
        return null;
    }

    private static string? CopyZip(string zipPath, string dest, out string? folderName)
    {
        folderName = null;
        using var zip = ZipFile.OpenRead(zipPath);
        var entries = new List<(ZipArchiveEntry Entry, string[] Parts)>();
        foreach (var e in zip.Entries)
        {
            var parts = e.FullName.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);
            bool isDir = e.FullName.EndsWith('/') || e.FullName.EndsWith('\\');
            if (parts.Length == 0 || isDir) continue;
            if (parts[0] == "__MACOSX") continue;   // macOS Finder's resource-fork shadow tree
            entries.Add((e, parts));
        }
        if (entries.Count == 0) return "the ZIP is empty";

        // Files at the root win; otherwise everything must sit under one top folder.
        List<(ZipArchiveEntry Entry, string[] Parts)> picked;
        if (entries.Any(x => x.Parts.Length == 1))
            picked = entries.Where(x => x.Parts.Length == 1).ToList();
        else
        {
            var tops = entries.Select(x => x.Parts[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (tops.Count != 1) return "the ZIP holds more than one folder — zip one package at a time";
            folderName = tops[0];
            picked = entries.Where(x => x.Parts.Length == 2).ToList();
        }

        var destFull = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0; long total = 0;
        foreach (var (e, parts) in picked)
        {
            var leaf = parts[^1];
            if (!IsPlainFileName(leaf)) return $"the ZIP has an entry with an unsafe name ({e.FullName})";
            if (!seen.Add(leaf)) return $"the ZIP holds two files named {leaf}";
            if (++count > MaxFiles) return $"it has over {MaxFiles} files";
            var outPath = Path.GetFullPath(Path.Combine(dest, leaf));
            if (!outPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                return $"the ZIP has an entry with an unsafe name ({e.FullName})";
            using var input = e.Open();
            if (CopyCapped(input, outPath, leaf, ref total) is { } err) return err;
        }
        return null;
    }

    private static bool IsPlainFileName(string leaf) =>
        leaf is not ("." or "..")
        && leaf.Length <= 255
        && leaf.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && leaf.Trim().Length > 0
        && !Path.IsPathRooted(leaf);

    /// <summary>Stream-copy under the per-file and running-total caps, counting the bytes that actually
    /// arrive — a ZIP's declared sizes are not trusted.</summary>
    private static string? CopyCapped(Stream input, string outPath, string label, ref long total)
    {
        var buffer = new byte[81920];
        long written = 0;
        using var output = new FileStream(outPath, FileMode.CreateNew, FileAccess.Write);
        int n;
        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            written += n; total += n;
            if (written > MaxFileBytes) return $"{label} is over {MaxFileBytes / 1024 / 1024} MB";
            if (total > MaxTotalBytes) return $"the package is over {MaxTotalBytes / 1024 / 1024} MB";
            output.Write(buffer, 0, n);
        }
        return null;
    }

    private static bool IsUnder(string path, string dir)
    {
        var d = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return (path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .StartsWith(d, StringComparison.OrdinalIgnoreCase);
    }

    private static void Discard(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        catch (Exception ex) { Trace.WriteLine($"[Packages] staging discard failed: {ex.Message}"); }
    }
}
