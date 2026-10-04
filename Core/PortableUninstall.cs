using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace ControllerWheel;

/// <summary>Build a literal-path, nonrecursive deletion worker for the portable distribution.</summary>
public static class PortableUninstall
{
    public static readonly IReadOnlyList<string> Files = Array.AsReadOnly(new[]
    {
        "Radiata.exe", "Radiata.pdb", "Uninstall Radiata.cmd", "README - Install Radiata.txt",
        "Radiata.ArcadeHost.exe", "Radiata.ArcadeHost.dll", "Radiata.ArcadeHost.deps.json",
        "Radiata.ArcadeHost.runtimeconfig.json", "Jint.dll", "Acornima.dll",
        @"drivers\HidHide_1.5.230_x64.exe", @"drivers\Legacinator.exe",
        @"drivers\ViGEmBus_1.22.0_x64_x86_arm64.exe",
    });

    public static string Script(string directory, int ownerPid, long ownerStartTicks)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(root, Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An installation cannot be a drive root.", nameof(directory));
        _ = ReadHelperManifest(root); // Validate now; the worker independently validates the same hashed data.
        string manifest = Path.Combine(root, "Radiata.helper-files.txt");
        string? manifestHash = File.Exists(manifest)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest))) : null;
        var json = JsonSerializer.Serialize(new { Root = root, Files, ManifestHash = manifestHash, OwnerPid = ownerPid, OwnerStartTicks = ownerStartTicks });
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        // Only base64 data is interpolated into code. %, $, apostrophes and backticks in paths are data.
        return "$plan = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + data + "')) | ConvertFrom-Json\n" + """
            $ErrorActionPreference = 'Stop'
            function File-Hash([string]$path) {
                $algorithm = [Security.Cryptography.SHA256]::Create()
                $stream = [IO.File]::OpenRead($path)
                try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
                finally { $stream.Dispose(); $algorithm.Dispose() }
            }
            function Assert-NoLink([string]$path) {
                $cursor = $path
                while ($cursor) {
                    if ([IO.File]::Exists($cursor) -or [IO.Directory]::Exists($cursor)) {
                        if (([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing a reparse-point path.' }
                    }
                    $cursor = [IO.Path]::GetDirectoryName($cursor)
                }
            }
            # Wait for this exact worker, with a finite deadline. PID reuse is not a reason to wait forever.
            $deadline = [DateTime]::UtcNow.AddSeconds(60)
            do {
                $owner = Get-Process -Id $plan.OwnerPid -ErrorAction SilentlyContinue
                $same = $owner -and $owner.StartTime.ToUniversalTime().Ticks -eq $plan.OwnerStartTicks
                if (-not $same) { break }
                if ([DateTime]::UtcNow -ge $deadline) { exit 2 }
                Start-Sleep -Milliseconds 250
            } while ($true)
            $root = [IO.Path]::GetFullPath([string]$plan.Root).TrimEnd('\')
            Assert-NoLink $root
            $helper = @()
            $manifest = [IO.Path]::Combine($root, 'Radiata.helper-files.txt')
            if ($plan.ManifestHash) {
                Assert-NoLink $manifest
                $bytes = [IO.File]::ReadAllBytes($manifest)
                if ($bytes.Length -gt 1048576) { throw 'Helper manifest is too large.' }
                $algorithm = [Security.Cryptography.SHA256]::Create()
                try { $hash = [BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '') }
                finally { $algorithm.Dispose() }
                if ($hash -ne $plan.ManifestHash) { throw 'Helper manifest changed after cleanup was prepared.' }
                foreach ($line in ([Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) -split '\r?\n')) {
                    if ([string]::IsNullOrWhiteSpace($line)) { continue }
                    $parts = $line.Split('|')
                    if ($parts.Length -ne 2 -or $parts[0] -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Invalid helper manifest entry.' }
                    $relative = $parts[1]
                    $file = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $relative))
                    if (-not $relative.StartsWith('ArcadeHost\', [StringComparison]::OrdinalIgnoreCase) -or $relative.Contains(':') -or
                        -not $file.StartsWith($root + '\ArcadeHost\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Helper path escaped its directory.' }
                    Assert-NoLink $file
                    $helper += [pscustomobject]@{ Path = $relative; Hash = $parts[0] }
                }
            }
            foreach ($relative in $plan.Files) {
                $file = [IO.Path]::GetFullPath([IO.Path]::Combine($root, [string]$relative))
                if (-not $file.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'File escaped installation root.' }
                Assert-NoLink $file
                if ([IO.File]::Exists($file)) { [IO.File]::Delete($file) }
            }
            foreach ($entry in $helper) {
                $file = [IO.Path]::GetFullPath([IO.Path]::Combine($root, [string]$entry.Path))
                if (-not $file.StartsWith($root + '\ArcadeHost\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Helper file escaped its directory.' }
                Assert-NoLink $file
                if ([IO.File]::Exists($file) -and (File-Hash $file) -eq $entry.Hash) {
                    [IO.File]::Delete($file)
                }
            }
            $helperDirs = @($helper | ForEach-Object { [IO.Path]::GetDirectoryName([IO.Path]::Combine($root, [string]$_.Path)) })
            $helperDirs += [IO.Path]::Combine($root, 'ArcadeHost')
            foreach ($directory in ($helperDirs | Sort-Object -Unique | Sort-Object -Property Length -Descending)) {
                Assert-NoLink $directory
                if ([IO.Directory]::Exists($directory) -and [IO.Directory]::GetFileSystemEntries($directory).Length -eq 0) {
                    [IO.Directory]::Delete($directory, $false)
                }
            }
            $manifest = [IO.Path]::Combine($root, 'Radiata.helper-files.txt')
            Assert-NoLink $manifest
            if ([IO.File]::Exists($manifest) -and -not [IO.Directory]::Exists([IO.Path]::Combine($root, 'ArcadeHost'))) {
                [IO.File]::Delete($manifest)
            }
            foreach ($directory in @([IO.Path]::Combine($root, 'drivers'), $root)) {
                Assert-NoLink $directory
                # Nonrecursive: user files and unknown future payload files keep their directory intact.
                if ([IO.Directory]::Exists($directory) -and [IO.Directory]::GetFileSystemEntries($directory).Length -eq 0) {
                    [IO.Directory]::Delete($directory, $false)
                }
            }
            """;
    }

    private sealed record HelperFile(string Path, string Hash);

    private static HelperFile[] ReadHelperManifest(string root)
    {
        string manifest = Path.Combine(root, "Radiata.helper-files.txt");
        if (!File.Exists(manifest)) return [];
        if ((File.GetAttributes(manifest) & FileAttributes.ReparsePoint) != 0 || new FileInfo(manifest).Length > 1024 * 1024)
            throw new IOException("Invalid helper manifest.");
        var files = new List<HelperFile>();
        foreach (string line in File.ReadLines(manifest))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int separator = line.IndexOf('|');
            if (separator != 64 || !line[..separator].All(Uri.IsHexDigit)) throw new IOException("Invalid helper hash.");
            string relative = line[(separator + 1)..];
            string full = Path.GetFullPath(Path.Combine(root, relative));
            if (!relative.StartsWith("ArcadeHost\\", StringComparison.OrdinalIgnoreCase)
                || !full.StartsWith(root + "\\ArcadeHost\\", StringComparison.OrdinalIgnoreCase)
                || relative.Contains(':')) throw new IOException("Invalid helper path.");
            files.Add(new HelperFile(relative, line[..separator]));
        }
        return files.ToArray();
    }
}
