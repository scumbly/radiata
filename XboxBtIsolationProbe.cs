using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerWheel;

/// <summary>Observes what a NON-allow-listed process can see over XInput — the gate for the Bluetooth
/// Xbox cloak. Radiata.exe is HidHide-allow-listed, so its own XInput view proves nothing about a
/// game's; the observer must be a different image. It reuses the Arcade script helper
/// (<c>Radiata.ArcadeHost.exe --xinput-count</c>, launched directly — no jail, no pipe), which already
/// ships beside Radiata.exe in both the dev and installer layouts and is never allow-listed.
///
/// ⚠ Fail-closed by contract: null (probe missing, crashed, timed out) must be treated by the caller
/// exactly like "pads still visible" — the virtual pad may only come up on an observed zero. Gating on
/// <c>Hide()</c> success instead would ship double input to every Bluetooth user the day a Windows or
/// HidHide change altered the behaviour this feature rests on.</summary>
internal static class XboxBtIsolationProbe
{
    /// <summary>Maximum simultaneous XInput pad count another process sees (0–4), or null when it
    /// could not be observed. Takes ~0.6s (the probe samples across XInput's PnP settle lag).</summary>
    public static async Task<int?> CountPadsAnotherProcessSeesAsync()
    {
        string? exe = FindHelper();
        if (exe is null)
        {
            Trace.WriteLine($"[Capture] isolation probe unavailable — {ScriptSessionCoordinator.HelperExeName} not found beside Radiata.exe");
            return null;
        }
        try
        {
            var psi = new ProcessStartInfo(exe, "--xinput-count")
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await p.WaitForExitAsync(cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { p.Kill(); } catch { }
                Trace.WriteLine("[Capture] isolation probe timed out");
                return null;
            }
            // 10 + count (10–14) — offset so a helper crash (1) or bad-args (2) can never read as a count.
            return p.ExitCode is >= 10 and <= 14 ? p.ExitCode - 10 : null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Capture] isolation probe failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Same layout rules as the script launcher: the installer ships the helper in the
    /// ArcadeHost subfolder; dev builds keep it flat beside the exe. Launched from wherever it sits —
    /// the probe needs no AppContainer, so no stage-copy.</summary>
    private static string? FindHelper()
    {
        string appDir = AppContext.BaseDirectory;
        string sub = Path.Combine(appDir, ScriptSessionCoordinator.HelperSubdirName, ScriptSessionCoordinator.HelperExeName);
        if (File.Exists(sub)) return sub;
        string flat = Path.Combine(appDir, ScriptSessionCoordinator.HelperExeName);
        return File.Exists(flat) ? flat : null;
    }
}
