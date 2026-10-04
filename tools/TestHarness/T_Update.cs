using System;
using System.Text;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The update feed's Core half (UpdateFeed) and the crash-report text rules (CrashReport):
/// pure parse/compare/scrub logic, so it runs headlessly with no network. The feed is hostile input
/// by policy — most checks here are rejections.</summary>
internal static class T_Update
{
    private const string GoodFeed =
        """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/Radiata-0.13.87-setup.exe","sha256":"AAbb00112233445566778899aabbccddeeff00112233445566778899aabbccdd","notesUrl":"https://github.com/scumbly/radiata/releases/tag/v0.13.87"}""";

    public static void Run()
    {
        H.Group("Update feed parse + version compare (Core)");

        // ── the well-formed feed ──
        var feed = UpdateFeed.TryParse(GoodFeed, out var err);
        H.Check("well-formed feed parses", feed is not null, err);
        if (feed is not null)
        {
            H.Check("feed version read", feed.Version == "0.13.87");
            H.Check("feed url read", feed.Url.EndsWith("setup.exe", StringComparison.Ordinal));
            H.Check("sha256 normalized to lowercase",
                    feed.Sha256 == "aabb00112233445566778899aabbccddeeff00112233445566778899aabbccdd");
            H.Check("notesUrl read", feed.NotesUrl == "https://github.com/scumbly/radiata/releases/tag/v0.13.87");
        }

        Rejects("empty body", "");
        Rejects("not JSON", "latest: nope");
        Rejects("not an object", "[1,2,3]");
        Rejects("missing version", """{"url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%"}""");
        Rejects("non-numeric version", """{"version":"0.13.beta","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%"}""");
        Rejects("two-part version", """{"version":"0.13","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%"}""");
        Rejects("missing url", """{"version":"0.13.87","sha256":"%SHA%"}""");
        Rejects("https url on a foreign host", """{"version":"0.13.87","url":"https://x.example/Radiata-setup.exe","sha256":"%SHA%"}""");
        Rejects("https url on a github.com path outside the release assets", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/raw/main/evil.exe","sha256":"%SHA%"}""");
        Rejects("https url on a github.com lookalike host", """{"version":"0.13.87","url":"https://github.com.evil.example/scumbly/radiata/releases/download/v1/a.exe","sha256":"%SHA%"}""");
        Rejects("userinfo trick in the download url", """{"version":"0.13.87","url":"https://github.com@evil.example/scumbly/radiata/releases/download/v1/a.exe","sha256":"%SHA%"}""");
        Rejects("release prefix with nothing after it", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/","sha256":"%SHA%"}""");
        Rejects("traversal out of the release prefix", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/../../other/releases/download/v1/a.exe","sha256":"%SHA%"}""");
        H.Check("upper-cased host still accepted (normalized)",
                UpdateFeed.IsTrustedUrl("https://GitHub.com/scumbly/radiata/releases/download/v0.13.87/a.exe"));
        Rejects("http (non-https) url", """{"version":"0.13.87","url":"http://x.example/a.exe","sha256":"%SHA%"}""");
        Rejects("file: url", """{"version":"0.13.87","url":"file:///C:/evil.exe","sha256":"%SHA%"}""");
        Rejects("missing sha256", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe"}""");
        Rejects("short sha256", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"abc123"}""");
        Rejects("non-hex sha256", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"zzbb00112233445566778899aabbccddeeff00112233445566778899aabbccdd"}""");
        Rejects("http notesUrl", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%","notesUrl":"http://x.example/notes"}""");
        Rejects("oversized body", """{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%","pad":""" +
                                  "\"" + new string('a', UpdateFeed.MaxFeedBytes) + "\"}");

        var noNotes = UpdateFeed.TryParse(Fill("""{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%"}"""), out err);
        H.Check("notesUrl optional", noNotes is { NotesUrl: null }, err);
        var extras = UpdateFeed.TryParse(Fill("""{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/a.exe","sha256":"%SHA%","future":{"x":1}}"""), out err);
        H.Check("unknown properties ignored (forward compat)", extras is not null, err);

        // ── version compare: numeric on the 0.R.B triplet, 4th part ignored ──
        H.Check("0.13.87 newer than 0.12.700.260819 (4th part ignored)", UpdateFeed.IsNewer("0.13.87", "0.12.700.260819"));
        H.Check("equal triplet is not newer", !UpdateFeed.IsNewer("0.13.87", "0.13.87.260820"));
        H.Check("older feed is not newer", !UpdateFeed.IsNewer("0.12.9", "0.13.1.260820"));
        H.Check("numeric, not lexicographic (0.13.100 > 0.13.9)", UpdateFeed.IsNewer("0.13.100", "0.13.9"));
        H.Check("unparsable feed version never wins", !UpdateFeed.IsNewer("garbage", "0.13.87"));
        H.Check("unparsable current version never offers", !UpdateFeed.IsNewer("0.14.0", "garbage"));

        // ── download file name: bare-name grammar or the fallback ──
        H.Check("plain setup name kept",
                UpdateFeed.SetupFileNameFrom("https://x.example/dl/Radiata-0.13.87-setup.exe") == "Radiata-0.13.87-setup.exe");
        H.Check("query string never reaches the name",
                UpdateFeed.SetupFileNameFrom("https://x.example/dl/setup.exe?token=..%2F..%2Fboom") == "setup.exe");
        H.Check("traversal in the segment falls back",
                UpdateFeed.SetupFileNameFrom("https://x.example/dl/..%2F..%2Fevil.exe") == UpdateFeed.FallbackSetupFileName);
        H.Check("non-exe segment falls back",
                UpdateFeed.SetupFileNameFrom("https://x.example/dl/setup.msi") == UpdateFeed.FallbackSetupFileName);
        H.Check("no segment falls back",
                UpdateFeed.SetupFileNameFrom("https://x.example/dl/") == UpdateFeed.FallbackSetupFileName);

        H.Group("Crash report text rules (Core)");

        string report = CrashReport.Compose("0.12.700.260819", "Microsoft Windows NT 10.0.19045.0",
                                            "isolated=True passthru=False",
                                            "System.InvalidOperationException: boom\n   at X.Y()",
                                            new DateTime(2026, 8, 20, 12, 0, 0));
        H.Check("compose carries app version", report.Contains("app: 0.12.700.260819"));
        H.Check("compose carries os + drivers + exception",
                report.Contains("os: ") && report.Contains("drivers: isolated=True") && report.Contains("boom"));
        H.Check("blank driver state omits the line",
                !CrashReport.Compose("1", "2", "  ", "x", DateTime.Now).Contains("drivers:"));

        string scrubbed = CrashReport.ScrubUserPaths(
            @"at C:\Users\Jesse\source\repos\thing.cs:line 5 and c:\users\jesse/AppData/x plus C:/Users/Jesse/y",
            @"C:\Users\Jesse");
        H.Check("profile prefix scrubbed (case-insensitive)", !scrubbed.Contains("Jesse", StringComparison.OrdinalIgnoreCase), scrubbed);
        H.Check("scrub replaces with ~", scrubbed.Contains(@"~\source\repos") && scrubbed.Contains("~/AppData"));
        H.Check("null profile is a no-op", CrashReport.ScrubUserPaths("text", null) == "text");

        string small = "line1\nline2\n";
        H.Check("under-budget text untouched", ReferenceEquals(CrashReport.TruncateToBudget(small), small));
        string old = string.Concat(System.Linq.Enumerable.Repeat("OLD line that should be dropped first\n", 3000));
        string fresh = "── newest crash ──\nkeep me\n";
        string truncated = CrashReport.TruncateToBudget(old + fresh);
        H.Check("truncation keeps the NEWEST content", truncated.EndsWith(fresh, StringComparison.Ordinal));
        H.Check("truncation marks itself", truncated.StartsWith(CrashReport.TruncationMarker, StringComparison.Ordinal));
        H.Check("truncated size within the 64 KB budget",
                Encoding.UTF8.GetByteCount(truncated) <= CrashReport.MaxBytes,
                $"{Encoding.UTF8.GetByteCount(truncated)} bytes");
    }

    private const string Sha = "aabb00112233445566778899aabbccddeeff00112233445566778899aabbccdd";
    private static string Fill(string template) => template.Replace("%SHA%", Sha);

    private static void Rejects(string what, string json)
    {
        var feed = UpdateFeed.TryParse(Fill(json), out var err);
        H.Check($"rejects {what}", feed is null, feed is null ? err : "PARSED — should have been rejected");
    }
}
