using System;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace Radiata.TestHarness;

/// <summary>Asserts that the uninstaller's ARP lookup actually FINDS the drivers installed on this machine.
///
/// This exists because of a bug that shipped for a long time and reported success the whole way: the ViGEmBus
/// lookup matched <c>DisplayName</c> against the needles "ViGEmBus" and "Virtual Gamepad Emulation", while the
/// real installed entry is "ViGEm Bus Driver" — matching neither, because of the space. FindUninstallCommand
/// returned null, and RunRegisteredUninstall treated null as "already removed" and returned true without
/// running or checking anything.
///
/// Driving the real uninstall can only be done once per machine and needs UAC. This is read-only and
/// repeatable: it calls the private FindUninstallCommand directly and asserts it resolves a command for each
/// driver that IS installed. It SKIPS a driver it can't see in ARP rather than passing vacuously — "no ARP
/// entry" is exactly what the bug looked like, so a silent pass there would defeat the point.</summary>
internal static class T_DriverMatch
{
    public static void Run()
    {
        var t = Type.GetType("ControllerWheel.DriverSetup, Radiata");
        if (t is null) { H.Fail("load ControllerWheel.DriverSetup"); return; }
        var find = t.GetMethod("FindUninstallCommand", BindingFlags.NonPublic | BindingFlags.Static);
        if (find is null) { H.Fail("find DriverSetup.FindUninstallCommand", "renamed or removed?"); return; }

        // The needle sets the production callers pass. Duplicated literally on purpose: if someone edits the
        // needles in DriverSetup, this keeps checking the OLD ones and fails — which is the alarm we want,
        // because the needles are precisely what broke.
        Check(find, "HidHide",  ["HidHide"]);
        Check(find, "ViGEmBus", ["ViGEm", "ViGEmBus", "Virtual Gamepad Emulation"]);
    }

    private static void Check(MethodInfo find, string label, string[] needles)
    {
        var installed = ArpDisplayName(needles);
        if (installed is null)
        {
            H.Skip($"{label}: ARP match", "not installed on this machine — nothing to match against");
            return;
        }
        var cmd = find.Invoke(null, [needles]) as string;
        H.Check($"{label}: the ARP entry \"{installed}\" is matched by the shipped needles",
                !string.IsNullOrWhiteSpace(cmd),
                string.IsNullOrWhiteSpace(cmd)
                    ? "FindUninstallCommand returned NULL — the uninstaller would call this \"already removed\" "
                      + $"and report success having done nothing. Needles: {string.Join(", ", needles)}"
                    : $"resolved: {cmd}");
    }

    /// <summary>The real DisplayName of an installed Nefarius product matching any needle, or null. Found
    /// independently of the code under test — otherwise a broken lookup would "prove" itself absent.</summary>
    private static string? ArpDisplayName(string[] needles)
    {
        string[] roots =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];
        foreach (var root in roots)
        {
            using var key = Registry.LocalMachine.OpenSubKey(root);
            if (key is null) continue;
            foreach (var subName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subName);
                if (sub?.GetValue("DisplayName") is not string name) continue;
                if ((sub.GetValue("Publisher") as string ?? "").Contains("Nefarius", StringComparison.OrdinalIgnoreCase)
                    && needles.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                    return name;
            }
        }
        return null;
    }
}
