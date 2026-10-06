using System.Diagnostics;
using Microsoft.Win32;

namespace ControllerWheel;

/// <summary>Read-only, no-admin detection of the Nefarius driver stack for onboarding: is HidHide present
/// (and which version), is the conflicting legacy HidGuardian present, is ViGEmBus present. Uses registry
/// reads (contention-free) rather than the HidHide control device, which is EXCLUSIVE.
/// (ViGEmBus presence is probed separately via
/// <see cref="GamepadEmulator.ProbeDriverInstalled"/>, which owns the ViGEm client.)</summary>
internal static class DriverStatus
{
    // The reliable HidHide key on real installs, read in both registry views. ⚠ Don't switch to the HKCR
    // Installer\Dependencies\NSS.Drivers.HidHide.x64 Version key the Nefarius docs mention — it is ABSENT on
    // live installs where THIS key carries Version + Path.
    private const string HidHideKey = @"SOFTWARE\Nefarius Software Solutions e.U.\HidHide";

    private static string? HidHideValue(string name)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var k = b.OpenSubKey(HidHideKey);
                if (k?.GetValue(name) is string s && !string.IsNullOrWhiteSpace(s)) return s;
            }
            catch (Exception ex) { Trace.WriteLine($"[Drivers] HidHide '{name}' read failed ({view}): {ex.Message}"); }
        }
        return null;
    }

    /// <summary>Installed HidHide version string (e.g. "1.2.128"), or null if HidHide isn't installed.</summary>
    public static string? HidHideVersion() => HidHideValue("Version");

    public static bool HidHideInstalled() => HidHideVersion() is not null;

    /// <summary>True if the ViGEmBus kernel driver is registered (installed), regardless of client-probe
    /// state. Cheap registry check for UI gating (e.g. hiding the Passthru Mode tab when the emulation stack
    /// isn't present) — for whether the client can actually talk to it, see
    /// <see cref="GamepadEmulator.ProbeDriverInstalled"/>.</summary>
    public static bool ViGEmBusInstalled()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
            return k is not null;
        }
        catch { return false; }
    }

    /// <summary>File version of the ViGEmBus driver the service key points at, or null when the key, the
    /// ImagePath or the file can't be read. Diagnostics only (the startup <c>[Drivers]</c> line) — never a
    /// gate: the same service name is registered by every fork, so a version says nothing about origin.</summary>
    public static string? ViGEmBusVersion()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
            if (k?.GetValue("ImagePath") is not string image || image.Length == 0) return null;
            // \SystemRoot\System32\DriverStore\… or System32\drivers\… — both relative to the Windows dir.
            string path = image;
            if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
                path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path[12..]);
            else if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
                path = path[4..];
            else if (!System.IO.Path.IsPathRooted(path))
                path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path);
            return System.IO.File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : null;
        }
        catch { return null; }
    }

    /// <summary>Where the installed ViGEmBus came from: <c>Nefarius</c> when the bundled installer's Apps
    /// entry is registered, otherwise <c>foreign</c> plus the bus devnode(s) that load the service — HP OMEN
    /// Gaming Hub (<c>SWD\DRIVERENUM\…#HPIncVigemBus…</c>), Oculus, Virtual Desktop and DS4Windows-era installs
    /// all register the same service name. A foreign bus is the first suspect when the virtual pad's devnode
    /// is judged physical (docs/INPUT-CAPTURE.md ▸ Xbox / XInput pads) and is never Radiata's to remove
    /// (<see cref="DriverSetup.UninstallViGEmBus"/>).</summary>
    public static string ViGEmBusOrigin()
    {
        var foreign = ForeignViGEmBusName();
        if (foreign is null) return "Nefarius";
        var nodes = ViGEmBusDevnodes();
        string why = VersionExceedsFinalNefarius(ViGEmBusVersion()) ? "version above the final Nefarius 1.22" : "no Nefarius Apps entry";
        return nodes.Count == 0 ? $"foreign ({foreign}; {why}), no bus devnode present"
             : $"foreign ({foreign}; {why}): " + string.Join(" | ", nodes);
    }

    /// <summary>The program whose ViGEmBus fork this PC runs, or null when the bus is Nefarius's own (or
    /// absent). Named from the bus devnode's instance id: HP OMEN Gaming Hub ships a 2018 fork (1.4.3
    /// reporting itself as 10.x) under <c>SWD\DRIVERENUM\…#HPIncVigemBusOmenFusionSoftware</c>
    /// (nefarius/ViGEmBus#99); the ViGEm client binds to whichever bus answers its interface first, so with
    /// that fork present Radiata is running on it. Nefarius supports no coexistence — the driver engine
    /// must not install a second bus beside it, and the uninstall must not remove it.</summary>
    public static string? ForeignViGEmBusName()
    {
        if (!ViGEmBusInstalled()) return null;
        // ViGEmBus tops out at 1.22.0: a higher file version is a fork by definition, whatever its Apps
        // entry says — HP's reports 10.x. A version at or below the ceiling clears nothing on its own
        // (a fork built from an older tree reports honestly), hence the two checks below.
        bool aboveCeiling = VersionExceedsFinalNefarius(ViGEmBusVersion());
        if (!aboveCeiling && DriverSetup.NefariusUninstallerRegistered(DriverSetup.ViGEmBusDisplayNames)) return null;
        foreach (var id in ViGEmBusDevnodes())
        {
            if (id.Contains("HPIncVigemBus", StringComparison.OrdinalIgnoreCase)) return "HP OMEN Gaming Hub";
            if (id.Contains("Oculus", StringComparison.OrdinalIgnoreCase)) return "Oculus";
        }
        return "another program";
    }

    /// <summary>The last ViGEmBus Nefarius released. Nothing above it is theirs.</summary>
    public static readonly Version FinalNefariusViGEmBus = new(1, 22, 0, 0);

    /// <summary>True when <paramref name="fileVersion"/> parses and is newer than
    /// <see cref="FinalNefariusViGEmBus"/> on major.minor (an unparseable or absent version is not a verdict).</summary>
    public static bool VersionExceedsFinalNefarius(string? fileVersion)
    {
        if (fileVersion is null) return false;
        var digits = System.Text.RegularExpressions.Regex.Match(fileVersion, @"\d+(\.\d+)+").Value;
        if (!Version.TryParse(digits, out var v)) return false;
        return v.Major > FinalNefariusViGEmBus.Major
            || (v.Major == FinalNefariusViGEmBus.Major && v.Minor > FinalNefariusViGEmBus.Minor);
    }

    /// <summary>Instance ids of the present devnodes whose service is <c>ViGEmBus</c>: read from the Enum
    /// registry tree (no SetupAPI, no elevation), where each devnode's <c>Service</c> value names its driver.</summary>
    private static IReadOnlyList<string> ViGEmBusDevnodes()
    {
        var found = new List<string>();
        try
        {
            // Software buses enumerate under ROOT\ or SWD\; a fork under a vendor ACPI node is still SWD\.
            using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum");
            if (enumKey is null) return found;
            foreach (var enumerator in new[] { "ROOT", "SWD" })
            {
                using var e = enumKey.OpenSubKey(enumerator);
                if (e is null) continue;
                foreach (var deviceName in e.GetSubKeyNames())
                {
                    using var d = e.OpenSubKey(deviceName);
                    if (d is null) continue;
                    foreach (var inst in d.GetSubKeyNames())
                    {
                        using var i = d.OpenSubKey(inst);
                        if (i?.GetValue("Service") is string svc
                            && svc.Equals("ViGEmBus", StringComparison.OrdinalIgnoreCase))
                            found.Add($@"{enumerator}\{deviceName}\{inst}");
                    }
                }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Drivers] ViGEmBus devnode scan failed: {ex.Message}"); }
        return found;
    }

    /// <summary>Install directory of HidHide (from its registry Path), or null.</summary>
    public static string? HidHidePath() => HidHideValue("Path");

    /// <summary>True if the LEGACY HidGuardian driver is present — it MUST NOT coexist with HidHide (its
    /// successor), so onboarding removes it (via Legacinator) before installing HidHide. Detected by its
    /// driver-service key; the exact service name is unconfirmed against real hardware.</summary>
    public static bool HidGuardianInstalled()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\HidGuardian");
            return k is not null;
        }
        catch { return false; }
    }

    // Peer controller-remapping apps that share our two drivers (ViGEmBus + HidHide). Running one alongside
    // Radiata is the sharpest real-world conflict: HidHide's control device is single-owner (Radiata may be
    // unable to cloak, or go blind if the peer cloaks without whitelisting us), and both present a virtual
    // pad (the game sees double). Process name → friendly name.
    private static readonly (string Proc, string Name)[] KnownControllerTools =
    [
        ("DS4Windows",   "DS4Windows"),
        ("reWASD",       "reWASD"),
        ("reWASDEngine", "reWASD"),
        ("InputMapper",  "InputMapper"),
    ];

    // HidHide's OWN tools take the single-owner control device too, so while either runs every cloak call
    // fails with "Access is denied (5)" and capture silently degrades to raw passthrough. The config client
    // is what a user reaches for to diagnose a controller problem, which makes this the failure mode that
    // punishes investigation. Kept separate from KnownControllerTools: these aren't peer remappers, they
    // present no virtual pad, and the cure is "close it", not "don't run it alongside Radiata".
    private static readonly (string Proc, string Name)[] HidHideOwnTools =
    [
        ("HidHideClient",   "the HidHide Configuration Client"),
        ("HidHideCLI",      "HidHideCLI"),
        // Nefarius's own service. It can wedge holding the control device (an in-place driver reinstall
        // over a pending-delete does it) and runs in session 0, so it can only be seen by a machine-wide
        // scan and can only be cured by a restart, never by "close it".
        ("HidHideWatchdog", WatchdogHolderName),
    ];

    /// <summary>The one holder whose cure is a restart, not closing a window. Consumers switch on it.</summary>
    public const string WatchdogHolderName = "the HidHide watchdog service";

    /// <summary>Friendly names of peer controller tools currently RUNNING (deduped; empty = none). Warn the
    /// user to close them — they contend for HidHide/ViGEmBus and cause double or blocked input.</summary>
    public static IReadOnlyList<string> RunningControllerTools() => RunningAny(KnownControllerTools);

    /// <summary>Friendly names of HidHide's own tools currently RUNNING (deduped; empty = none). These hold
    /// the exclusive control device, so this is the first thing to check when a cloak call comes back
    /// "Access is denied" — see <c>HidHideManager.CloakFailure.Contended</c>.</summary>
    /// ⚠ Deliberately MACHINE-WIDE, unlike every other running-process check here: the control device is
    /// a single machine-wide exclusive handle, so a holder in another session (or session 0) is still the
    /// true holder. Don't widen the sentry/OOBE checks the same way - their session scoping is correct.
    public static IReadOnlyList<string> RunningHidHideTools()
    {
        var found = RunningAny(HidHideOwnTools, machineWide: true);
        // The watchdog service is ALWAYS running on HidHide 1.5.x; running is not holding. A cloak denied while a
        // closable tool is open is explained by that tool, and naming the service too would turn "close it" into
        // "restart the PC". Name the service only when nothing closable is running (the wedged-service case).
        if (found.Count > 1 && found.Contains(WatchdogHolderName))
            return found.Where(n => n != WatchdogHolderName).ToList();
        return found;
    }

    private static IReadOnlyList<string> RunningAny((string Proc, string Name)[] table, bool machineWide = false)
    {
        var found = new List<string>();
        foreach (var (proc, name) in table)
        {
            try
            {
                // Session-scoped: naming another account's Discord/DS4Windows as this session's
                // contention holder is exactly the blames-the-wrong-thing class.
                bool running;
                if (machineWide)
                {
                    var all = System.Diagnostics.Process.GetProcessesByName(proc);
                    running = all.Length > 0;
                    foreach (var p in all) p.Dispose();   // AnySessionProcess disposes its handles; match it
                }
                else running = WindowsPlatformActions.AnySessionProcess(proc);
                if (running && !found.Contains(name)) found.Add(name);
            }
            catch { /* access denied / transient — skip */ }
        }
        return found;
    }
}
