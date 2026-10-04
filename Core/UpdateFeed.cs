using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// A validated update-feed entry (latest.json from getradiata.app/update). Parse + version compare
/// only — no HTTP here; the shell fetches and hands the body in as a string. The feed is treated as
/// hostile input (validate-on-READ): every field is bounded and shape-checked, the download URL must
/// be https, and a malformed feed yields null + reason rather than ever throwing. The one-button
/// update flow this feeds is prompt-only (nothing installs until the user presses Update Now) and the
/// check sends only the app version (PRIVACY.md).
/// </summary>
public sealed record UpdateFeed
{
    /// <summary>Feed version string, e.g. "0.13.87" — numeric on the 0.R.B triplet (a 4th part is
    /// tolerated and ignored by the comparison, matching the app's own 0.R.B.YYMMDD format).</summary>
    public required string Version { get; init; }

    /// <summary>The setup-exe download URL. Guaranteed https, absolute, and under
    /// <see cref="TrustedDownloadPrefix"/> — a feed offering anything else was rejected at parse.</summary>
    public required string Url { get; init; }

    /// <summary>SHA-256 of the setup exe, normalized to lowercase hex at parse. The downloader
    /// compares case-insensitively and hard-rejects (delete, never execute) on mismatch.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Release-notes page (https), or null when the feed omitted it — the prompt window
    /// simply drops its notes link then.</summary>
    public string? NotesUrl { get; init; }

    /// <summary>Cap on the feed body. Anything larger is rejected before parsing — latest.json is a
    /// one-line static file, so an oversized response is a misconfigured host or an attack, not data.</summary>
    public const int MaxFeedBytes = 64 * 1024;

    private const int MaxUrlLength = 2048;

    /// <summary>Fallback download file name when the feed URL's last segment isn't a usable bare
    /// file name.</summary>
    public const string FallbackSetupFileName = "Radiata-Setup.exe";

    /// <summary>Parse + validate a feed body. Returns null with a reason instead of throwing —
    /// a bad feed must never surface UI or crash; the caller traces the reason and moves on.
    /// Unknown JSON properties are ignored (forward compatibility).</summary>
    public static UpdateFeed? TryParse(string json, out string? error)
    {
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) { error = "empty feed"; return null; }
            if (json.Length > MaxFeedBytes) { error = "feed exceeds the 64 KB cap"; return null; }

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 4,
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "feed is not a JSON object"; return null; }

            var version = GetString(root, "version");
            if (version is null || version.Length > 32 || !TryParseTriplet(version, out _, out _, out _))
            { error = "version missing or not a numeric x.y.z"; return null; }

            var url = GetString(root, "url");
            if (!IsTrustedUrl(url)) { error = "url missing, not https, or not a github.com/scumbly/radiata release asset"; return null; }

            var sha = GetString(root, "sha256");
            if (sha is null || sha.Length != 64 || !AllHex(sha))
            { error = "sha256 missing or not 64 hex chars"; return null; }

            var notes = GetString(root, "notesUrl");
            if (notes is not null && !IsHttps(notes)) { error = "notesUrl present but not https"; return null; }

            return new UpdateFeed
            {
                Version = version,
                Url = url!,
                Sha256 = sha.ToLowerInvariant(),
                NotesUrl = notes,
            };
        }
        catch (JsonException ex) { error = "invalid JSON: " + ex.Message; return null; }
        catch (Exception ex) { error = ex.Message; return null; }
    }

    /// <summary>Whether <paramref name="feedVersion"/> is strictly newer than
    /// <paramref name="currentVersion"/> on the numeric 0.R.B triplet (any 4th part — the app's
    /// YYMMDD date — is ignored). False whenever either side doesn't parse: never offer an update
    /// on a version we couldn't read.</summary>
    public static bool IsNewer(string? feedVersion, string? currentVersion) =>
        Compare(feedVersion, currentVersion) > 0;

    /// <summary>Numeric triplet comparison: &gt;0 when <paramref name="a"/> is newer, 0 on equal or
    /// when either side is unparsable (unparsable never wins in either direction).</summary>
    public static int Compare(string? a, string? b)
    {
        if (!TryParseTriplet(a, out int a0, out int a1, out int a2) ||
            !TryParseTriplet(b, out int b0, out int b1, out int b2)) return 0;
        if (a0 != b0) return a0.CompareTo(b0);
        if (a1 != b1) return a1.CompareTo(b1);
        return a2.CompareTo(b2);
    }

    /// <summary>The first three dot-separated parts as non-negative ints. At least three parts are
    /// required; extra parts (the build-date 4th) are allowed and ignored.</summary>
    public static bool TryParseTriplet(string? version, out int p0, out int p1, out int p2)
    {
        p0 = p1 = p2 = 0;
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64) return false;
        var parts = version.Trim().Split('.');
        if (parts.Length < 3) return false;
        return TryPart(parts[0], out p0) && TryPart(parts[1], out p1) && TryPart(parts[2], out p2);

        static bool TryPart(string s, out int value)
        {
            value = 0;
            if (s.Length is 0 or > 7) return false;
            foreach (var c in s) if (!char.IsAsciiDigit(c)) return false;
            return int.TryParse(s, out value) && value >= 0;
        }
    }

    /// <summary>The bare file name to save the download under: the URL path's last segment,
    /// unescaped, accepted only if it passes the same strict bare-file-name grammar the package
    /// system uses (no separators, no "..", sane charset) AND ends in .exe. Anything else — traversal
    /// attempts, query tricks, an empty path — falls back to <see cref="FallbackSetupFileName"/>.</summary>
    public static string SetupFileNameFrom(string url)
    {
        try
        {
            var u = new Uri(url, UriKind.Absolute);
            int slash = u.AbsolutePath.LastIndexOf('/');
            var name = Uri.UnescapeDataString(slash >= 0 ? u.AbsolutePath[(slash + 1)..] : u.AbsolutePath);
            return MaterialPackage.FileNameIsValid(name, ".exe") ? name : FallbackSetupFileName;
        }
        catch { return FallbackSetupFileName; }
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool IsHttps(string? url) =>
        url is { Length: > 0 and <= MaxUrlLength }
        && Uri.TryCreate(url, UriKind.Absolute, out var u)
        && string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    /// <summary>The only place a setup exe may be fetched from: the public mirror's GitHub release
    /// assets, over https. The feed and the exe then sit behind two unrelated accounts (the web host
    /// serving latest.json and the GitHub account owning the release), so a compromised web host
    /// alone cannot point installs at a foreign binary. The same prefix is independently encoded in
    /// release.yml's download URL and documented in docs/INSTALLER.md.</summary>
    public const string TrustedDownloadPrefix = "https://github.com/scumbly/radiata/releases/download/";

    /// <summary>Where a setup exe may be fetched from: <see cref="TrustedDownloadPrefix"/>, full stop.
    /// DEBUG builds additionally accept plain http to the IPv4 loopback so the prompt → download →
    /// verify → install flow can be driven end to end against a local feed; a
    /// Release build never does, and nothing else may widen this.</summary>
    public static bool IsTrustedUrl(string? url)
    {
        if (url is not { Length: > 0 and <= MaxUrlLength } || !Uri.TryCreate(url, UriKind.Absolute, out var u))
            return false;
        if (string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            // Compare on the normalized absolute URI (lower-cased scheme/host, no userinfo tricks,
            // no default-port games) and require a path segment beyond the prefix.
            var abs = u.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
            return string.IsNullOrEmpty(u.UserInfo)
                   && abs.StartsWith(TrustedDownloadPrefix, StringComparison.Ordinal)
                   && abs.Length > TrustedDownloadPrefix.Length
                   && !abs.Contains("/../", StringComparison.Ordinal);
        }
#if DEBUG
        return string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               && u.Host == "127.0.0.1";
#else
        return false;
#endif
    }

    private static bool AllHex(string s)
    {
        foreach (var c in s) if (!char.IsAsciiHexDigit(c)) return false;
        return true;
    }
}
