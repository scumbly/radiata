using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>Resolves a Windows .lnk shortcut to the path it points at, via the WScript.Shell COM
/// object (no extra dependencies). Used when an .lnk is dropped into the slice editor so the launch
/// slice targets the real executable — keeping focus/toggle detection (which keys off the path's
/// filename) accurate. Returns null if the shortcut can't be read.</summary>
internal static class ShortcutResolver
{
    public static string? ResolveTarget(string lnkPath)
    {
        object? shell = null;
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t is null) return null;
            shell = Activator.CreateInstance(t);
            if (shell is null) return null;

            dynamic sc = ((dynamic)shell).CreateShortcut(lnkPath);
            string target = sc.TargetPath as string ?? "";
            return string.IsNullOrWhiteSpace(target) ? null : target.Trim();
        }
        catch
        {
            return null;   // missing/corrupt shortcut, COM unavailable, etc. — caller falls back to the .lnk path
        }
        finally
        {
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
