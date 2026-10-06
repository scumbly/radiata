using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>Remembers that the user closed Radiata during the current logon session, so an autostart
/// launch that arrives later in the same session (the Run-key entry, which Explorer reaches minutes after
/// the logon task has already started the app) stays closed instead of bringing it back. Keyed on the
/// logon session's authentication id, which changes at every sign-in, so the marker expires by itself and
/// nothing has to clear it. A crash writes no marker: the late Run-key launch then restarts the app, which
/// is the recovery that path exists for. Best-effort throughout; any failure reads as "no marker".</summary>
public static class QuitMarker
{
    private static string Path => System.IO.Path.Combine(AppPaths.AppDataDir, "quit-marker.txt");

    /// <summary>Record a deliberate close in this logon session.</summary>
    public static void Write()
    {
        try
        {
            if (LogonSessionId() is not { } id) return;
            System.IO.Directory.CreateDirectory(AppPaths.AppDataDir);
            System.IO.File.WriteAllText(Path, id.ToString("X16"));
        }
        catch { /* the worst case is one unwanted relaunch */ }
    }

    /// <summary>True when the user closed Radiata earlier in THIS logon session.</summary>
    public static bool MatchesCurrentSession()
    {
        try
        {
            if (!System.IO.File.Exists(Path) || LogonSessionId() is not { } id) return false;
            return ulong.TryParse(System.IO.File.ReadAllText(Path).Trim(),
                                  System.Globalization.NumberStyles.HexNumber, null, out var saved)
                   && saved == id;
        }
        catch { return false; }
    }

    /// <summary>The current token's authentication id (the logon session LUID), or null.</summary>
    public static ulong? LogonSessionId()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var buf = new byte[56];   // TOKEN_STATISTICS
            if (!GetTokenInformation(identity.Token, TokenStatistics, buf, buf.Length, out _)) return null;
            return BitConverter.ToUInt64(buf, 8);   // AuthenticationId follows the 8-byte TokenId
        }
        catch { return null; }
    }

    private const int TokenStatistics = 10;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, byte[] info, int length, out int returned);
}
