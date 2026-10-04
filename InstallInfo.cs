using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ControllerWheel;

/// <summary>Whether THIS copy of Radiata is the installer-managed one (Inno Setup, per-user under
/// %LOCALAPPDATA%\Programs\Radiata) or a portable/dev copy. Decided by reading Inno's HKCU uninstall
/// key and comparing its InstallLocation to <see cref="AppContext.BaseDirectory"/> — so a portable
/// copy running side-by-side with an installed one still reads as portable. Cached per process.
/// <para>The split matters at uninstall time: an installed copy's FILES belong to the Inno
/// uninstaller (Settings launches <see cref="UninstallerPath"/>; the uninstaller calls back into
/// <c>--uninstall-cleanup</c> for Windows-state cleanup), while a portable copy keeps the
/// manifest-based self-delete flow (<c>--uninstall</c>).</para></summary>
public static class InstallInfo
{
    // Inno Setup registers its uninstall key as <AppId>_is1 with the braces KEPT in the key name.
    private const string InnoUninstallKeyName = "{FC9B183F-8F24-4343-82FC-7D8CE9914300}_is1";

    // HKCU only (PrivilegesRequired=lowest installs per-user). The WOW6432Node variant is checked
    // defensively — a 32-bit Inno build's writes land there under a 64-bit view.
    private static readonly string[] UninstallKeyPaths =
    [
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + InnoUninstallKeyName,
        @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" + InnoUninstallKeyName,
    ];

    private static readonly Lazy<(bool Installed, string? Uninstaller, string? Detail)> Cached = new(Detect);

    /// <summary>True when this exe runs from the directory the Inno uninstall key registers.</summary>
    public static bool IsInstalledCopy => Cached.Value.Installed;

    /// <summary>The Inno uninstaller exe (parsed from QuietUninstallString/UninstallString, existence
    /// verified), or null when this isn't an installed copy or the uninstaller is missing.</summary>
    public static string? UninstallerPath => Cached.Value.Uninstaller;

    /// <summary>One startup trace line stating the determination (call from the primary instance).</summary>
    public static void TraceDetermination() =>
        Trace.WriteLine($"[Install] {(IsInstalledCopy ? "installer-managed copy" : "portable/dev copy")} "
                        + $"@ {AppContext.BaseDirectory} ({Cached.Value.Detail})");

    private static (bool, string?, string?) Detect()
    {
        try
        {
            foreach (var path in UninstallKeyPaths)
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                if (key is null) continue;
                var loc = key.GetValue("InstallLocation") as string;
                if (!PathsEqual(loc, AppContext.BaseDirectory))
                    return (false, null, $"uninstall key present but InstallLocation is '{loc}'");
                var uninstaller = ExeFrom(key.GetValue("QuietUninstallString") as string)
                                  ?? ExeFrom(key.GetValue("UninstallString") as string);
                return (true, uninstaller,
                        uninstaller is null ? "uninstall key matched; no uninstaller exe found"
                                            : $"uninstaller: {uninstaller}");
            }
            return (false, null, "no Inno uninstall key");
        }
        catch (Exception ex)
        {
            // Fail toward portable: the portable flow never deletes anything it doesn't own.
            return (false, null, $"detection failed: {ex.Message}");
        }
    }

    /// <summary>Case-insensitive, trailing-separator-tolerant path equality; false on any parse failure.</summary>
    private static bool PathsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd('\\', '/'),
                Path.GetFullPath(b).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>The executable path out of an uninstall command line (Inno quotes it; tolerate an
    /// unquoted bare path too). Null unless the file actually exists.</summary>
    private static string? ExeFrom(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var cmd = command.Trim();
        string exe;
        if (cmd[0] == '"')
        {
            int end = cmd.IndexOf('"', 1);
            if (end <= 1) return null;
            exe = cmd[1..end];
        }
        else
        {
            int sp = cmd.IndexOf(' ');
            exe = sp < 0 ? cmd : cmd[..sp];
        }
        try { return File.Exists(exe) ? exe : null; }
        catch { return null; }
    }
}
