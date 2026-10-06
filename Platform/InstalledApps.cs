using System.Diagnostics;

namespace ControllerWheel;

/// <summary>One entry of the shell's Applications folder: display name + AppUserModelID. Covers everything
/// with a Start-menu presence, including UWP/Store apps that have no .exe path to browse to. Launch with
/// <see cref="LaunchPath"/> via ShellExecute.</summary>
public sealed record InstalledApp(string Name, string Aumid)
{
    public const string ShellPrefix = @"shell:AppsFolder\";
    public string LaunchPath => ShellPrefix + Aumid;
}

/// <summary>Enumerates installed applications from the shell's virtual AppsFolder (Kando-inspired, MIT —
/// see THIRD-PARTY-LICENSES.md section 5): Shell.Application → NameSpace("shell:AppsFolder"), where each item's Path property IS its
/// AppUserModelID. COM shell objects want an STA thread — call <see cref="Scan"/> from the UI thread or a
/// dedicated STA thread.</summary>
public static class InstalledApps
{
    private static InstalledApp[]? _cache;
    private static readonly object Lock = new();

    /// <summary>All installed apps, sorted by display name. Scans once and caches for the process
    /// lifetime; <paramref name="refresh"/> forces a re-scan. Returns an empty array on any COM
    /// failure — never throws.</summary>
    public static InstalledApp[] Scan(bool refresh = false)
    {
        lock (Lock)
        {
            if (_cache is not null && !refresh) return _cache;
            var list = new List<InstalledApp>();
            object? shell = null;
            try
            {
                var type = Type.GetTypeFromProgID("Shell.Application");
                if (type is null) return _cache = [];
                shell = Activator.CreateInstance(type);
                dynamic? folder = ((dynamic?)shell)?.NameSpace("shell:AppsFolder");
                if (folder is not null)
                {
                    foreach (dynamic item in folder.Items())
                    {
                        string? name  = item.Name as string;
                        string? aumid = item.Path as string;
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(aumid))
                            list.Add(new InstalledApp(name!, aumid!));
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Apps] AppsFolder scan failed: {ex.Message}");
            }
            finally
            {
                if (shell is not null) try { System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); } catch { }
            }
            _cache = list.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            Trace.WriteLine($"[Apps] AppsFolder scan: {_cache.Length} apps");
            return _cache;
        }
    }

    /// <summary>Display name for a <c>shell:AppsFolder\…</c> launch path. Null for any other path shape.
    /// Scans on first need — call from the UI thread (STA).</summary>
    public static string? NameFor(string? launchPath)
    {
        if (launchPath is null ||
            !launchPath.StartsWith(InstalledApp.ShellPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var aumid = launchPath[InstalledApp.ShellPrefix.Length..];
        return Scan().FirstOrDefault(a => string.Equals(a.Aumid, aumid, StringComparison.OrdinalIgnoreCase))?.Name;
    }
}
