using System.Diagnostics;
using System.IO;

namespace ControllerWheel;

/// <summary>Starts Windows' system-wide Narrator, for the Settings tip that points at it.
/// <para>One-way on purpose. Radiata offers to start Narrator and never stops it or writes its
/// auto-start state: Narrator is an OS-owned accessibility service that other software and the user's
/// own habits may depend on, so a Radiata checkbox must not own its lifecycle. Do not add a stop path
/// (killing <c>Narrator.exe</c> / synthesising Ctrl+Win+Enter) or a registry write to
/// <c>…\Accessibility\Configuration</c> — that key is the same class of undocumented state that made the
/// Focus Assist write fail silently (it read back as applied while nothing changed).</para>
/// <para>Runs at the caller's integrity level with no elevation; a second launch while Narrator is
/// already up is a no-op Windows absorbs, so no running-state probe is needed.</para></summary>
public static class NarratorLauncher
{
    /// <summary>True when Narrator is up right now — the gate on offering to turn it on. A process probe
    /// rather than the auto-start registry value on purpose: what matters is whether the user currently
    /// has system narration, not whether Windows was told to start it at sign-in (they disagree whenever
    /// Narrator is started or quit by hand, which is the common case).
    /// <para>Unknown counts as not running: a redundant tip is a far milder failure than withholding the
    /// one pointer a user who can't hear Settings depends on.</para></summary>
    public static bool IsRunning
    {
        get
        {
            try
            {
                return WindowsPlatformActions.AnySessionProcess("Narrator");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Narration] Narrator probe failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>Launch Narrator. False = it couldn't be started (missing on N/stripped images, or blocked
    /// by policy) and the caller should say so rather than assume speech is now coming.</summary>
    public static bool Launch()
    {
        try
        {
            var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                                   "Narrator.exe");
            if (!File.Exists(exe))
            {
                Trace.WriteLine($"[Narration] Narrator.exe not present at {exe}");
                return false;
            }
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Trace.WriteLine("[Narration] Windows Narrator launched");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Narration] Narrator launch failed: {ex.Message}");
            return false;
        }
    }
}
