using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>A Toggle slice kills the right process, gracefully.
///
/// The hand check is to point a Toggle slice at an app with unsaved state and see that it "gets a close request first
/// (so a 'save changes?' prompt appears) and is force-killed ~3 s later if it ignores it". A save prompt is
/// only indirect evidence, and a name-based toggle on notepad.exe would also hit the user's own unsaved
/// documents — <c>ToggleProcess</c> matches by process NAME. So this drives <c>tools\CloseProbe</c>, which logs
/// each close request it receives and then refuses to exit: the request, the refusal and the force-kill are all
/// directly observable, with timings.</summary>
internal static class T_Toggle
{
    private static readonly WindowsPlatformActions P = new();

    public static void Run()
    {
        H.Group("Toggle slice — graceful close preserves unsaved work");

        var probe = FindProbe();
        if (probe is null)
        {
            H.Skip("toggle graceful-close", "CloseProbe.exe not built — run: dotnet build tools\\CloseProbe -c Debug");
            return;
        }

        var log = Path.Combine(Path.GetTempPath(), "radiata-closeprobe-" + Guid.NewGuid().ToString("N")[..6] + ".log");
        Process p = null;
        try
        {
            p = Process.Start(new ProcessStartInfo(probe, $"\"{log}\"") { UseShellExecute = true });
            for (int i = 0; i < 60 && (p.MainWindowHandle == IntPtr.Zero); i++) { Thread.Sleep(100); p.Refresh(); }
            H.Check("the probe is running with a window", p is { HasExited: false } && p.MainWindowHandle != IntPtr.Zero,
                    $"pid {p.Id}");
            H.Check("…and it logged STARTED", WaitForLine(log, "STARTED", 5000));

            // Sanity: the toggle must see it as running before we ask it to close anything.
            H.Check("Toggle reads the probe as running", P.IsProcessRunning("CloseProbe", probe));

            var sw = Stopwatch.StartNew();
            using var t = new H.TraceGrab();
            var result = P.ToggleProcess("CloseProbe", probe);
            H.Check("ToggleProcess reports a request rather than a confirmed exit", result == ProcessToggleResult.CloseRequested);

            // 1) The graceful request must arrive, and arrive FIRST.
            bool requested = WaitForLine(log, "CLOSE_REQUEST", 4000);
            long tRequest = sw.ElapsedMilliseconds;
            H.Check("a CLOSE REQUEST arrives before any kill", requested, requested ? $"after {tRequest} ms" : "never arrived");
            H.Check("…and the probe was still alive when it arrived (so it was asked, not killed)",
                    requested && !SafeHasExited(p));

            // A real unsaved-work prompt can stay open indefinitely; the old three-second kill must be gone.
            Thread.Sleep(4500);
            H.Check("a refused close survives beyond the old force-kill deadline", !SafeHasExited(p));
            var repeated = P.ToggleProcess("CloseProbe", probe);
            H.Check("repeated Toggle still requests a close without killing", repeated == ProcessToggleResult.CloseRequested && !SafeHasExited(p));

            var lines = File.Exists(log) ? File.ReadAllLines(log) : Array.Empty<string>();
            Console.WriteLine("        (record) probe log:");
            foreach (var l in lines) Console.WriteLine("        " + l);
            Console.WriteLine("        (record) trace: " + string.Join(" | ",
                t.Lines.Where(l => l.Contains("[Action]")).Select(l => l.Trim())));

            H.Check("the close was traced", t.Saw("toggle: closing") || t.Saw("closing"));
            H.Check("the current-state readout still sees the running app", P.IsProcessRunning("CloseProbe", probe));
        }
        finally
        {
            try { if (p is { HasExited: false }) p.Kill(true); } catch { }
            p?.Dispose(); // Only the child created by this test is cleaned up.
            try { File.Delete(log); } catch { }
        }
    }

    private static bool SafeHasExited(Process p)
    {
        try { p.Refresh(); return p.HasExited; } catch { return true; }
    }

    private static bool WaitForLine(string log, string needle, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (File.Exists(log) && File.ReadAllText(log).Contains(needle, StringComparison.Ordinal)) return true;
            }
            catch { /* the probe holds it open for append — retry */ }
            Thread.Sleep(50);
        }
        return false;
    }

    private static string FindProbe()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "ControllerWheel.csproj"))) continue;
            var exe = Path.Combine(dir.FullName, "tools", "CloseProbe", "bin", "Debug", "net8.0-windows", "CloseProbe.exe");
            return File.Exists(exe) ? exe : null;
        }
        return null;
    }
}
