using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ControllerWheel;

/// <summary>Outcome of one feed check, for the Settings tab's inline readout.</summary>
public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

/// <summary>
/// The in-app update checker: fetches the static feed (~60 s after startup, then every 24 h, plus
/// the manual Settings check), and runs the one-button download → SHA-256 verify → silent-install
/// flow (docs/INSTALLER.md ▸ The silent-update contract). Parsing/validation and the version
/// comparison live in Core (<see cref="UpdateFeed"/>); this class owns HTTP, scheduling, and the
/// prompt orchestration.
///
/// Posture (binding — PRIVACY.md): the check sends
/// ONLY the app version; failures are trace-only (never UI); updates are prompt-only; the download
/// is https-only, size-capped, and NEVER executed on a hash mismatch. Prompts never interrupt the
/// overlay or a game — App's quiet-moment scheduler (<see cref="RunWhenQuiet"/>) defers them.
/// </summary>
internal static class UpdateService
{
    public const string PublicFeedUrl = "https://getradiata.app/update/latest.json";

    /// <summary>The feed actually fetched. DEBUG builds honour the <c>RADIATA_DEV_FEED</c> environment
    /// variable (a loopback http URL is enough — see <see cref="UpdateFeed.IsTrustedUrl"/>) so the whole
    /// update flow can run against a local server; a Release build ignores the variable entirely.</summary>
    public static string FeedUrl
    {
        get
        {
#if DEBUG
            var dev = Environment.GetEnvironmentVariable("RADIATA_DEV_FEED");
            if (!string.IsNullOrWhiteSpace(dev) && UpdateFeed.IsTrustedUrl(dev)) return dev;
#endif
            return PublicFeedUrl;
        }
    }

    /// <summary>Download cap. The setup exe is tens of MB; anything approaching this is wrong.</summary>
    public const long MaxDownloadBytes = 500L * 1024 * 1024;

    private static readonly TimeSpan FeedTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Dead-air window for the setup download, re-armed on every chunk that arrives, so a
    /// slow link runs to completion and only a stalled connection fails. This is the ONLY deadline
    /// on the transfer: HttpClient's own timeout does not bound a ResponseHeadersRead body read, so
    /// without it a server that sends headers and then goes silent hangs the download forever behind
    /// a frozen progress bar. Deliberately no overall cap — a fixed one would exclude slow links.
    /// <see cref="MaxDownloadBytes"/> stays the absolute bound on size.</summary>
    private static readonly TimeSpan DownloadIdleTimeout = TimeSpan.FromSeconds(60);

    // ── Hooks (set once by App before Start) ─────────────────────────────────────────────────────
    public static Func<SystemConfig>? GetSystem;
    public static Action<SystemConfig>? WriteSystem;
    /// <summary>Defer an action until no overlay/grid/arcade/wizard is up and no game owns the
    /// foreground (App.RunWhenQuiet).</summary>
    public static Action<Action>? RunWhenQuiet;
    /// <summary>Non-interrupting notification (tray balloon, corner toast fallback): title, text,
    /// optional click action.</summary>
    public static Action<string, string, Action?>? Notify;
    /// <summary>The app's NORMAL clean-exit path (App.ExitApp: TearDown — cloak lift, virtual-pad
    /// release — then Shutdown). Called after the installer is launched; never a bare Shutdown.</summary>
    public static Action? ExitForUpdate;
    public static Func<bool>? CanExitForUpdate;

    /// <summary>The running app's InformationalVersion (0.R.B.YYMMDD), resolved once.</summary>
    public static readonly string CurrentVersion =
        System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                System.Reflection.Assembly.GetExecutingAssembly())?.InformationalVersion ?? "0.0.0.0";

    /// <summary>The newest feed entry known to be newer than this build, or null. Feeds the Settings
    /// tab's persistent "version available" line and the prompt window.</summary>
    public static UpdateFeed? Available { get; private set; }

    private static HttpClient? _http;
    // Every call site passes its own CancellationToken, and for the download that token is the only
    // deadline (DownloadAsync's idle timer). The client-level timeout is off deliberately: it does
    // not bound a ResponseHeadersRead body read, but it WOULD apply in full the moment anything here
    // buffered a response, silently capping a >100 MB setup download at 100 s.
    private static HttpClient Http => _http ??= new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    private static DispatcherTimer? _startupTimer, _dailyTimer;
    private static int _checking;              // one check in flight at a time
    private static string? _promptedVersion;   // balloon shown for this version already (per session)
    private static UpdateWindow? _window;

    /// <summary>Arm the schedule: one check ~60 s after startup (off the launch critical path), then
    /// every 24 h while resident. Timers only — no network happens here.</summary>
    public static void Start()
    {
        _ = Task.Run(CleanupDownloadDir);   // before any download can exist this session
        _startupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _startupTimer.Tick += (_, _) => { _startupTimer!.Stop(); _ = AutoCheckAsync(); };
        _startupTimer.Start();

        _dailyTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
        _dailyTimer.Tick += (_, _) => _ = AutoCheckAsync();
        _dailyTimer.Start();
    }

    private static async Task AutoCheckAsync()
    {
        try
        {
            if (GetSystem?.Invoke().UpdateCheckEnabled != true) return;
            await CheckAsync(manual: false);
        }
        catch (Exception ex) { Trace.WriteLine($"[Update] auto check failed: {ex.Message}"); }
    }

    /// <summary>One feed check. Failures are silent by contract (trace only) — the manual caller
    /// shows its own inline "check failed" from the returned status. Automatic checks honour the
    /// skip list; the manual check bypasses it (it still reports the version as available).</summary>
    public static async Task<UpdateCheckStatus> CheckAsync(bool manual)
    {
        if (Interlocked.Exchange(ref _checking, 1) != 0) return UpdateCheckStatus.Failed;
        try
        {
            string body;
            try
            {
                using var cts = new CancellationTokenSource(FeedTimeout);
                using var resp = await Http.GetAsync(
                    FeedUrl + "?v=" + Uri.EscapeDataString(CurrentVersion),
                    HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!resp.IsSuccessStatusCode)
                {
                    Trace.WriteLine($"[Update] feed returned {(int)resp.StatusCode}");
                    return UpdateCheckStatus.Failed;
                }
                body = await ReadCappedAsync(resp, UpdateFeed.MaxFeedBytes, cts.Token);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Update] feed unreachable: {ex.Message}");
                return UpdateCheckStatus.Failed;
            }

            var feed = UpdateFeed.TryParse(body, out var error);
            if (feed is null)
            {
                Trace.WriteLine($"[Update] feed rejected: {error}");
                return UpdateCheckStatus.Failed;
            }

            if (!UpdateFeed.IsNewer(feed.Version, CurrentVersion))
            {
                Trace.WriteLine($"[Update] up to date ({CurrentVersion}; feed {feed.Version})");
                Available = null;
                return UpdateCheckStatus.UpToDate;
            }

            Available = feed;
            Trace.WriteLine($"[Update] {feed.Version} available (running {CurrentVersion})");

            if (!manual) MaybePromptFor(feed);
            return UpdateCheckStatus.UpdateAvailable;
        }
        finally { Interlocked.Exchange(ref _checking, 0); }
    }

    /// <summary>Automatic-path prompt gate: honour the skip list (clearing it when the feed moved
    /// PAST the skipped version), prompt at most once per version per session, and only at a quiet
    /// moment. The prompt itself is a click-to-open notification, never a focus-stealing window.</summary>
    private static void MaybePromptFor(UpdateFeed feed)
    {
        var sys = GetSystem?.Invoke();
        if (sys is null) return;

        if (!string.IsNullOrEmpty(sys.UpdateSkippedVersion))
        {
            if (UpdateFeed.Compare(feed.Version, sys.UpdateSkippedVersion) > 0)
            {
                // A still-newer version supersedes the skip — clear it and prompt normally.
                Trace.WriteLine($"[Update] {feed.Version} supersedes skipped {sys.UpdateSkippedVersion} — skip cleared");
                WriteSystem?.Invoke(sys with { UpdateSkippedVersion = "" });
            }
            else if (UpdateFeed.Compare(feed.Version, sys.UpdateSkippedVersion) == 0)
            {
                Trace.WriteLine($"[Update] {feed.Version} is the skipped version — not prompting");
                return;
            }
        }

        if (_promptedVersion == feed.Version) return;
        _promptedVersion = feed.Version;

        RunWhenQuiet?.Invoke(() => Notify?.Invoke(
            Loc.F(UiText.Dialogs.UpdateHeading, UpdateFeed.ShortVersion(feed.Version)),
            Loc.T(UiText.Toasts.UpdateClickBody),
            OpenPromptWindow));
    }

    /// <summary>Show (or front) the update window for <see cref="Available"/>.</summary>
    public static void OpenPromptWindow()
    {
        if (Available is not { } feed) return;
        if (_window is { IsLoaded: true }) { _window.Activate(); return; }
        _window = new UpdateWindow(feed);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    /// <summary>"Skip This Version": remember the feed version so automatic checks stop prompting
    /// for it (a still-newer version clears it — see <see cref="MaybePromptFor"/>).</summary>
    public static void SkipVersion(string version)
    {
        var sys = GetSystem?.Invoke();
        if (sys is null) return;
        Trace.WriteLine($"[Update] user skipped {version}");
        WriteSystem?.Invoke(sys with { UpdateSkippedVersion = version });
    }

    /// <summary>"Later": remember the version so the NEXT prompt for it offers Skip This Version.</summary>
    public static void DeferVersion(string version)
    {
        var sys = GetSystem?.Invoke();
        if (sys is null || sys.UpdateDeferredVersion == version) return;
        Trace.WriteLine($"[Update] user deferred {version}");
        WriteSystem?.Invoke(sys with { UpdateDeferredVersion = version });
    }

    /// <summary>Whether the user has already pressed Later on this exact version.</summary>
    public static bool WasDeferred(string version) =>
        GetSystem?.Invoke().UpdateDeferredVersion == version;

    /// <summary>Download the setup exe to %TEMP%\Radiata-Update and verify its SHA-256. Returns the
    /// verified path, or null + reason. A hash mismatch DELETES the file — a file that failed
    /// verification must not exist to be run by anything later.</summary>
    public static async Task<(string? path, string? error)> DownloadAsync(
        UpdateFeed feed, IProgress<double> progress, CancellationToken ct)
    {
        // Verified at parse; re-check here so no future caller can bypass it.
        if (!UpdateFeed.IsTrustedUrl(feed.Url))
            return (null, "The update URL is not https.");

        string dir = DownloadDir;
        string path = Path.Combine(dir, UpdateFeed.SetupFileNameFrom(feed.Url));

        // An idle timer, not a total budget: armed here and re-armed per chunk below, so only dead air
        // fails the download. Every await in this method must take linked.Token, never ct — ct alone
        // leaves the body read with no deadline whatsoever, hanging on a silent server until the user
        // presses Cancel.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(DownloadIdleTimeout);
        try
        {
            Directory.CreateDirectory(dir);
            using var resp = await Http.GetAsync(feed.Url, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (!resp.IsSuccessStatusCode)
                return (null, $"Download failed ({(int)resp.StatusCode}).");
            long? total = resp.Content.Headers.ContentLength;
            if (total > MaxDownloadBytes) return (null, "The download is larger than the 500 MB cap.");

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long read = 0;
            {
                await using var net = await resp.Content.ReadAsStreamAsync(linked.Token);
                await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                var buf = new byte[81920];
                int n;
                while ((n = await net.ReadAsync(buf, linked.Token)) > 0)
                {
                    read += n;
                    linked.CancelAfter(DownloadIdleTimeout);
                    if (read > MaxDownloadBytes)
                        throw new InvalidOperationException("download exceeded the 500 MB cap");
                    await file.WriteAsync(buf.AsMemory(0, n), linked.Token);
                    sha.AppendData(buf, 0, n);
                    if (total > 0) progress.Report((double)read / total.Value);
                }
            }

            var hex = Convert.ToHexString(sha.GetHashAndReset());
            if (!string.Equals(hex, feed.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(path);
                Trace.WriteLine($"[Update] SHA-256 MISMATCH — file deleted (expected {feed.Sha256}, got {hex.ToLowerInvariant()})");
                return (null, "The download didn't match its checksum, so it was discarded. Try again later.");
            }

            Trace.WriteLine($"[Update] downloaded + verified {path} ({read} bytes)");
            return (path, null);
        }
        catch (OperationCanceledException)
        {
            TryDelete(path);
            // Only the caller's token means the user pressed Cancel; the idle timer firing is a stall,
            // and reporting that as a cancel reads as the app abandoning the download on its own.
            return ct.IsCancellationRequested
                ? (null, "Download cancelled.")
                : (null, "The download stalled and was stopped. Try again later.");
        }
        catch (Exception ex)
        {
            TryDelete(path);
            Trace.WriteLine($"[Update] download failed: {ex.Message}");
            return (null, "The download failed. Try again later.");
        }
    }

    /// <summary>Launch the VERIFIED setup exe with the silent-update arguments
    /// (docs/INSTALLER.md — /RADIATARELAUNCH=1 is what restarts the tray afterwards), then exit
    /// through the normal clean-shutdown path so the cloak lifts before the installer replaces us.</summary>
    public static bool LaunchInstallerAndExit(string setupPath)
    {
        if (CanExitForUpdate?.Invoke() == false) return false;
        try
        {
            Process.Start(new ProcessStartInfo(setupPath,
                "/SILENT /CLOSEAPPLICATIONS /NORESTART /RADIATARELAUNCH=1")
            { UseShellExecute = true });
            Trace.WriteLine("[Update] installer launched — exiting for update");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Update] installer launch failed: {ex.Message}");
            return false;
        }
        ExitForUpdate?.Invoke();
        return true;
    }

    /// <summary>Read a response body up to <paramref name="cap"/> bytes; anything longer throws
    /// (the caller reports "feed rejected"). Headers can lie or be absent, so the STREAM is capped.</summary>
    private static async Task<string> ReadCappedAsync(HttpResponseMessage resp, int cap, CancellationToken ct)
    {
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buf = new byte[8192];
        int n;
        while ((n = await s.ReadAsync(buf, ct)) > 0)
        {
            if (ms.Length + n > cap) throw new InvalidOperationException("feed body exceeds the 64 KB cap");
            ms.Write(buf, 0, n);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { Trace.WriteLine($"[Update] couldn't delete {path}: {ex.Message}"); }
    }

    private static string DownloadDir => Path.Combine(Path.GetTempPath(), "Radiata-Update");

    /// <summary>Startup sweep of <see cref="DownloadDir"/>. The installer is launched FROM that
    /// folder and can't delete itself, and file names carry the version — so every update taken
    /// would otherwise leave one setup exe behind, accumulating until Windows temp cleanup runs.
    /// Best-effort: a file still held (the installer only just exited) waits for the next launch.</summary>
    private static void CleanupDownloadDir()
    {
        try
        {
            if (!Directory.Exists(DownloadDir)) return;
            Directory.Delete(DownloadDir, recursive: true);
            Trace.WriteLine("[Update] cleared the leftover download folder");
        }
        catch (Exception ex) { Trace.WriteLine($"[Update] download-folder sweep failed: {ex.Message}"); }
    }
}
