using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerWheel;

/// <summary>
/// The opt-in crash reporter's I/O half. Crash time: <see cref="TryWritePending"/> appends one
/// scrubbed report block to %APPDATA%\Radiata\pending-crash.txt — minimal, one-shot, and swallowing
/// its own failures so it can never mask the original crash (it runs inside the same handlers as
/// the crash-safe un-cloak; see App.OnStartup). Next startup: App offers the pending file through
/// <see cref="CrashReportWindow"/> at a quiet moment; nothing is ever sent without the user pressing
/// Send, and the window shows the FULL body first (PRIVACY.md). Text shaping (compose / scrub /
/// 64 KB budget) is Core's <see cref="CrashReport"/> so the harness covers it.
/// </summary>
internal static class CrashReporter
{
    public const string PostUrl = "https://getradiata.app/update/crash.php";
    private static readonly TimeSpan PostTimeout = TimeSpan.FromSeconds(15);

    public static string PendingPath => Path.Combine(AppPaths.AppDataDir, "pending-crash.txt");

    /// <summary>Set once at startup. Must be CHEAP — it reads already-held state at crash time,
    /// never probes drivers or devices.</summary>
    public static Func<string>? DriverStateProvider;

    private static volatile bool _shuttingDown;

    /// <summary>Set once the app has begun a deliberate exit (TearDown, OnExit). Exceptions raised after
    /// that point come from unload/finalizer code (e.g. a native module's CRT uninitializer at domain
    /// unload), not from a crash of the running app, and are not persisted.</summary>
    public static void MarkShuttingDown() => _shuttingDown = true;

    private static int _wroteThisRun;   // Dispatcher + AppDomain both fire for one crash — write once

    /// <summary>Crash-time append. Exception-safe by construction: everything is inside one catch-all,
    /// and the caller's other duties (the un-cloak) run before this. <paramref name="exceptionObject"/>
    /// is AppDomain's object-typed payload; non-Exception values are stringified.</summary>
    public static void TryWritePending(object? exceptionObject)
    {
        try
        {
            if (_shuttingDown || Environment.HasShutdownStarted) return;
            if (Interlocked.Exchange(ref _wroteThisRun, 1) != 0) return;

            string driver = "";
            try { driver = DriverStateProvider?.Invoke() ?? ""; } catch { /* report without it */ }

            string exText = exceptionObject switch
            {
                Exception ex => ex.ToString(),
                null         => "(no exception object)",
                var other    => other.ToString() ?? "(unprintable exception object)",
            };

            string block = CrashReport.Compose(
                UpdateService.CurrentVersion,
                Environment.OSVersion.VersionString,
                driver, exText, DateTime.Now);
            block = CrashReport.ScrubUserPaths(
                block, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            string existing = "";
            try { if (File.Exists(PendingPath)) existing = File.ReadAllText(PendingPath); } catch { }
            File.WriteAllText(PendingPath, CrashReport.TruncateToBudget(existing + block));
        }
        catch { /* never mask the crash */ }
    }

    /// <summary>The pending report's text, re-scrubbed and re-budgeted on READ (the file could have
    /// been replaced by anything since the crash — validate-on-read like every other local file).
    /// Null when absent/unreadable/empty.</summary>
    public static string? ReadPending()
    {
        try
        {
            if (!File.Exists(PendingPath)) return null;
            var text = File.ReadAllText(PendingPath);
            if (string.IsNullOrWhiteSpace(text)) { DeletePending(); return null; }
            text = CrashReport.ScrubUserPaths(
                text, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return CrashReport.TruncateToBudget(text);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Crash] pending report unreadable: {ex.Message}");
            return null;
        }
    }

    public static void DeletePending()
    {
        try { if (File.Exists(PendingPath)) File.Delete(PendingPath); }
        catch (Exception ex) { Trace.WriteLine($"[Crash] couldn't delete pending report: {ex.Message}"); }
    }

    private static HttpClient? _http;

    /// <summary>POST the report per the contract: text/plain UTF-8, ≤64 KB (the caller's text is
    /// already budgeted; re-clamped here anyway), 15 s timeout. True = accepted (2xx).</summary>
    public static async Task<bool> SendAsync(string body)
    {
        try
        {
            _http ??= new HttpClient();
            body = CrashReport.TruncateToBudget(body);
            using var cts = new CancellationTokenSource(PostTimeout);
            using var content = new StringContent(body, System.Text.Encoding.UTF8, "text/plain");
            using var resp = await _http.PostAsync(PostUrl, content, cts.Token);
            Trace.WriteLine($"[Crash] report POST → {(int)resp.StatusCode}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Crash] report POST failed: {ex.Message}");
            return false;
        }
    }
}
