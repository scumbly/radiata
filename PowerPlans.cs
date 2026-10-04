using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControllerWheel;

/// <summary>Enumerates Windows power schemes via <c>powercfg /list</c> for the slice editor's Power Plan
/// picker. Read-only; activating a plan goes through <see cref="IPlatformActions.SetPowerPlan"/>
/// (<c>powercfg /setactive</c>).</summary>
public static class PowerPlans
{
    public sealed record Plan(string Guid, string Name, bool Active);

    /// <summary>Windows' built-in Balanced scheme — the same GUID on every install. The slice editor's
    /// default for Toggle Power Plan's plan B when a legacy single-plan config doesn't carry one.</summary>
    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    // "Power Scheme GUID: 381b4222-... (Balanced) *"  — the trailing * marks the active scheme.
    private static readonly Regex LineRx =
        new(@"Power Scheme GUID:\s*([0-9a-fA-F-]{36})\s*\(([^)]*)\)(\s*\*)?", RegexOptions.Compiled);

    /// <summary>All power schemes (empty list on any failure). The active scheme is flagged.</summary>
    public static IReadOnlyList<Plan> List()
    {
        var plans = new List<Plan>();
        try
        {
            var psi = new ProcessStartInfo("powercfg.exe", "/list")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return plans;
            // Read asynchronously so the 3 s deadline is real: a synchronous ReadToEnd() blocks until the
            // child closes stdout, which a hung powercfg never does — the WaitForExit timeout can't help then.
            var readTask = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(3000))
            {
                Trace.WriteLine("[PowerPlans] powercfg /list timed out — killing");
                try { p.Kill(entireProcessTree: true); } catch { }
            }
            string output = readTask.Wait(2000) ? readTask.Result : string.Empty;
            foreach (Match m in LineRx.Matches(output))
                plans.Add(new Plan(m.Groups[1].Value, m.Groups[2].Value.Trim(), m.Groups[3].Success));
        }
        catch (Exception ex) { Trace.WriteLine($"[PowerPlans] list failed: {ex.Message}"); }
        return plans;
    }
}
