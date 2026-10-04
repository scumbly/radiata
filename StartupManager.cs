using Microsoft.Win32;

namespace ControllerWheel;

/// <summary>Manages the "run at Windows login" entry under the per-user Run key. The entry is the single
/// source of truth for "start with Windows": the logon task (<see cref="RecoveryTask"/>) reads it at
/// sign-in and only becomes the app when it is present, so toggling autostart never needs elevation.
/// Points at the currently-running executable, so it stays correct across build/publish moves.
/// Value format: <c>"&lt;exe&gt;" --autostart</c> — the switch marks the Run-key launch so the app can tell
/// it from a manual launch (see <c>App.OnStartup</c>). The installer's <c>radiata.iss</c>
/// <c>[Registry]</c> section independently writes the same format on fresh installs.</summary>
public static class StartupManager
{
    private const string RunKey          = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName       = "Radiata";
    /// <summary>Windows' own Startup-apps toggle (Settings ▸ Apps ▸ Startup, Task Manager ▸ Startup)
    /// records its verdict here, not in the Run key: the entry stays put and this value's first byte
    /// goes odd (0x03) for disabled, even (0x02) for enabled.</summary>
    private const string StartupApproved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    /// <summary>Marks a launch that came from the Run-key entry rather than the user or the logon task.</summary>
    public  const string AutostartSwitch = "--autostart";
    // Pre-rename Run-key value names, newest first.
    private static readonly string[] LegacyValueNames = ["Capstan", "ControllerWheel"];

    private static string ExePath =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>The exact value data written for <paramref name="exe"/>.</summary>
    public static string Format(string exe) => $"\"{exe}\" {AutostartSwitch}";

    /// <summary>The executable a Run-key value names: the quoted span when the value starts with a quote,
    /// else everything up to the first space. Accepts both the current format and the older bare
    /// quoted path.</summary>
    public static string ParseTarget(string value)
    {
        value = value.Trim();
        if (value.StartsWith('"'))
        {
            int close = value.IndexOf('"', 1);
            return close > 0 ? value[1..close] : value.Trim('"');
        }
        int sp = value.IndexOf(' ');
        return sp > 0 ? value[..sp] : value;
    }

    /// <summary>True when the Run entry exists and points at this exact executable.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string v &&
                   ParseTarget(v).Equals(ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>True when the user switched Radiata off in Windows' Startup-apps UI. Explorer already
    /// honours that for the Run key; the logon task cannot see it, so it asks here.</summary>
    public static bool WindowsStartupDisabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupApproved);
                return key?.GetValue(ValueName) is byte[] { Length: > 0 } bytes && (bytes[0] & 1) == 1;
            }
            catch { return false; }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key!.SetValue(ValueName, Format(ExePath));
        else if (key!.GetValue(ValueName) is string value
                 && ParseTarget(value).Equals(ExePath, StringComparison.OrdinalIgnoreCase))
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Rewrites an entry that already points at this exe but predates the
    /// <see cref="AutostartSwitch"/> (a bare quoted path). Presence is preserved; an absent entry stays
    /// absent. Returns true when a rewrite happened.</summary>
    public static bool EnsureCurrentFormat()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not string value) return false;
            if (!ParseTarget(value).Equals(ExePath, StringComparison.OrdinalIgnoreCase)) return false;
            if (value.Trim() == Format(ExePath)) return false;
            key.SetValue(ValueName, Format(ExePath));
            return true;
        }
        catch { return false; }   // best-effort
    }

    /// <summary>Installed-copy migration: a Run entry pointing at a DIFFERENT Radiata.exe (a portable
    /// copy) is rewritten to the current exe. The entry existing is what "enabled" means, so an absent
    /// entry is left absent and the enabled state is preserved by the rewrite itself. Returns the old
    /// target when a rewrite happened, else null. Never touches the old copy's files.</summary>
    public static string? RedirectToCurrentExe()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not string value) return null;
            var oldTarget = ParseTarget(value);
            if (oldTarget.Equals(ExePath, StringComparison.OrdinalIgnoreCase)) return null;
            key.SetValue(ValueName, Format(ExePath));
            return oldTarget;
        }
        catch { return null; }   // best-effort
    }

    /// <summary>One-time rename migration: a legacy Run entry means the user had start-with-Windows
    /// enabled — move it to the "Radiata" value pointing at the current exe and drop the stale entry,
    /// which points at a renamed/absent exe.</summary>
    public static void MigrateLegacyName()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            foreach (var legacy in LegacyValueNames)
                if (key.GetValue(legacy) is string)
                {
                    key.DeleteValue(legacy, throwOnMissingValue: false);
                    key.SetValue(ValueName, Format(ExePath));
                    return;
                }
        }
        catch { /* best-effort */ }
    }
}
