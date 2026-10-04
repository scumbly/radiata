using Microsoft.Win32;
using System.IO;

namespace ControllerWheel;

/// <summary>One entry of a Windows "Programs and Features" (uninstall) registry key — the only place some
/// storefronts and games record a real title or a custom install location.</summary>
/// <param name="KeyName">The registry sub-key name (e.g. "Uplay Install 1234", "1207658693_is1").</param>
public readonly record struct UninstallEntry(
    string KeyName, string? DisplayName, string? InstallLocation, string? Publisher, string? UninstallString);

/// <summary>Enumerates the Windows uninstall hives. Ported in shape from Playnite's
/// <c>Playnite/Common/Programs.cs</c> (<c>GetUninstallProgsFromView</c>, MIT — see THIRD-PARTY-LICENSES.md)
/// for its COVERAGE.
///
/// <para>Two axes, four passes — all four are load-bearing:</para>
/// <list type="bullet">
/// <item><b>HKLM and HKCU.</b> A PER-USER install (no admin rights, increasingly the default for
/// launchers and some storefront games) records itself only under HKCU, so an HKLM-only sweep can't see
/// it at all.</item>
/// <item><b>Registry64 and Registry32 views</b> rather than a hardcoded <c>WOW6432Node</c> path — the
/// view API is what actually maps 32-bit writers, and it costs nothing to read both.</item>
/// </list>
///
/// <para>Entries are de-duplicated by (key name + install location), since the two views of a hive can
/// surface the same key. Every read is individually guarded: one ACL'd or malformed key must cost only
/// itself.</para></summary>
public static class UninstallRegistry
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>Every readable uninstall entry across both hives and both registry views. Memoised for a
    /// short window: one library scan asks for this four to six times over (Ubisoft titles, Ubisoft
    /// entries, EA locations, three launcher lookups), and the scan re-runs every 60 s while a game holds the
    /// foreground. Several hundred keys × four passes, once per window instead of per caller.</summary>
    public static IEnumerable<UninstallEntry> Entries()
    {
        long now = Environment.TickCount64;
        lock (CacheGate)
        {
            if (_cached is not null && now - _cachedAtMs < CacheMs) return _cached;
        }
        var list = Enumerate().ToList();
        lock (CacheGate) { _cached = list; _cachedAtMs = now; }
        return list;
    }

    private static readonly object CacheGate = new();
    private static List<UninstallEntry>? _cached;
    private static long _cachedAtMs;
    private const int CacheMs = 30_000;

    private static IEnumerable<UninstallEntry> Enumerate()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            RegistryKey? root = null, list = null;
            try
            {
                root = RegistryKey.OpenBaseKey(hive, view);
                list = root.OpenSubKey(UninstallPath);
            }
            catch { root?.Dispose(); continue; }
            if (list is null) { root?.Dispose(); continue; }

            using (root)
            using (list)
            {
                string[] names;
                try { names = list.GetSubKeyNames(); } catch { continue; }
                foreach (var name in names)
                {
                    UninstallEntry? entry = null;
                    try
                    {
                        using var sub = list.OpenSubKey(name);
                        if (sub is null) continue;
                        var e = new UninstallEntry(
                            name,
                            sub.GetValue("DisplayName")     as string,
                            (sub.GetValue("InstallLocation") as string)?.Trim(),
                            sub.GetValue("Publisher")       as string,
                            sub.GetValue("UninstallString") as string);
                        if (seen.Add($"{name}\0{e.InstallLocation}")) entry = e;
                    }
                    catch { /* unreadable key — skip just this one */ }
                    if (entry is { } yielded) yield return yielded;
                }
            }
        }
    }

    /// <summary>The <see cref="UninstallEntry.InstallLocation"/> of the first entry whose DisplayName matches
    /// <paramref name="displayName"/> exactly (case-insensitively) and whose location exists on disk. This is
    /// how a launcher installed OUTSIDE Program Files is found — the fixed-path probes can't see it.</summary>
    public static string? InstallLocationOf(string displayName)
    {
        try
        {
            foreach (var e in Entries())
            {
                if (!string.Equals(e.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)) continue;
                var loc = e.InstallLocation?.Trim('"');
                if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc)) return loc;
            }
        }
        catch { }
        return null;
    }
}
