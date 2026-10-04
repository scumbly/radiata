using System.Diagnostics;
using System.IO;

namespace ControllerWheel;

/// <summary>Single source of truth for the per-user app-data folder. All persisted state
/// (config, caches, tokens, HID offsets) lives under <see cref="AppDataDir"/>.</summary>
public static class AppPaths
{
    /// <summary>%APPDATA%\Radiata</summary>
    public static readonly string AppDataDir = AppContext.GetData("Radiata.TestDataDirectory") is string testDirectory
        ? Path.GetFullPath(testDirectory)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Radiata");

    // Pre-rename folder names, newest first; migrated to AppDataDir once on startup.
    private static readonly string[] LegacyDirs =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Capstan"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ControllerWheel"),
    ];

    /// <summary>One-time rename: if an old data folder ("Capstan", or the original "ControllerWheel")
    /// exists and the new "Radiata" one doesn't, move it so existing settings/caches carry over.
    /// Best-effort — on any failure we just fall through to a fresh Radiata folder. Call once at startup
    /// before any path under <see cref="AppDataDir"/> is read or written.</summary>
    public static void MigrateLegacyFolder()
    {
        // A harness supplies this in-process before app types initialize; it must never migrate real data.
        if (AppContext.GetData("Radiata.TestDataDirectory") is string) return;
        try
        {
            if (Directory.Exists(AppDataDir)) return;
            foreach (var legacy in LegacyDirs)
                if (Directory.Exists(legacy))
                {
                    Directory.Move(legacy, AppDataDir);
                    Trace.WriteLine($"[Paths] Migrated {legacy} → {AppDataDir}");
                    return;
                }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Paths] Legacy folder migration skipped: {ex.Message}");
        }
    }
}
