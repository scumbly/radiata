using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>
/// The `--hdr-probe` diagnostic: a safe, self-reverting way to test HDR toggling on a panel where the
/// wheel's HDR-ON blacks the display out until a power cycle while the Windows Settings toggle does not.
/// The suspected gap is link renegotiation — the CCD set call succeeds (state reads back True) but the
/// display never re-syncs, whereas Settings follows its set with a mode re-apply. The probe isolates that
/// one variable:
///
///   Radiata.exe --hdr-probe                read-only display inventory
///   Radiata.exe --hdr-probe on             snapshot → HDR ON (the app's exact path) → hold → AUTO-REVERT
///   Radiata.exe --hdr-probe on modeset     same, plus a ChangeDisplaySettingsEx mode re-apply after the set
///   Radiata.exe --hdr-probe off            force HDR off everywhere (recovery)
///
/// The hold defaults to 12 s and ALWAYS reverts — the dead-man switch is the point: if the TV goes
/// black, doing nothing restores SDR. Output goes to the launching console (AttachConsole) and to a
/// report file beside the config, so results survive a blacked-out screen either way.
/// </summary>
internal static class HdrProbe
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    private const int AttachParentProcess = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsExW(string? deviceName, IntPtr devMode, IntPtr hwnd,
                                                       uint flags, IntPtr param);

    private static readonly List<string> Lines = [];

    private static void Say(string s)
    {
        Lines.Add(s);
        try { Console.WriteLine(s); } catch { /* no console attached */ }
        Trace.WriteLine("[HdrProbe] " + s);
    }

    /// <summary>Run the probe named by the args after `--hdr-probe`. Returns the process exit code.</summary>
    public static int Run(string[] args, int probeIdx)
    {
        AttachConsole(AttachParentProcess);
        string mode = probeIdx + 1 < args.Length ? args[probeIdx + 1].ToLowerInvariant() : "";
        bool modeset = Array.IndexOf(args, "modeset") > probeIdx;
        int hold = 12;
        foreach (var a in args[(probeIdx + 1)..])
            if (int.TryParse(a, out int n) && n is > 0 and <= 60) hold = n;

        Say($"hdr-probe {DateTime.Now:HH:mm:ss}  mode='{mode}' modeset={modeset} hold={hold}s");
        Say("inventory (before):");
        Say(HdrState.InventoryReport());
        bool? before = HdrState.IsEnabled();
        Say($"IsEnabled (any-active): {before?.ToString() ?? "no capable display"}");

        int code = 0;
        switch (mode)
        {
            case "on":
                if (before is null) { Say("no HDR-capable display — nothing to test."); code = 1; break; }
                Say($"setting HDR ON ({(modeset ? "with" : "no")} modeset poke), auto-revert in {hold}s…");
                Say($"SetAll(true) → {HdrState.SetAll(true)?.ToString() ?? "FAILED"}");
                if (modeset) ModesetPoke();
                System.Threading.Thread.Sleep(1500);
                Say("readback: " + HdrState.InventoryReport());
                Say($"holding {hold}s — if the screen is BLACK, just wait: it reverts by itself.");
                System.Threading.Thread.Sleep(hold * 1000);
                Say($"reverting to previous state ({before})…");
                Say($"SetAll({before}) → {HdrState.SetAll(before.Value)?.ToString() ?? "FAILED"}");
                if (modeset) ModesetPoke();
                System.Threading.Thread.Sleep(1500);
                Say("readback (after revert): " + HdrState.InventoryReport());
                break;

            case "off":
                Say($"forcing HDR OFF everywhere → {HdrState.SetAll(false)?.ToString() ?? "FAILED"}");
                if (modeset) ModesetPoke();
                break;

            case "":
                break;   // inventory-only, already printed

            default:
                Say($"unknown probe mode '{mode}' — use: (nothing) | on [modeset] [seconds] | off");
                code = 2;
                break;
        }

        var report = System.IO.Path.Combine(AppPaths.AppDataDir, "hdr-probe.txt");
        try { System.IO.File.WriteAllLines(report, Lines); Say("report: " + report); }
        catch { /* console + trace already have it */ }
        return code;
    }

    /// <summary>Re-apply the current registry display mode — a no-change ChangeDisplaySettingsEx that
    /// still forces the GPU→display link to renegotiate; the candidate fix for the blackout.</summary>
    private static void ModesetPoke()
    {
        int r = ChangeDisplaySettingsExW(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        Say($"modeset poke (ChangeDisplaySettingsEx re-apply) → {r} ({(r == 0 ? "ok" : "error")})");
    }
}
