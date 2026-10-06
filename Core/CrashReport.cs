using System.Text;

namespace ControllerWheel;

/// <summary>
/// Pure text-shaping for the opt-in crash reporter: compose one report block, scrub user paths,
/// and hold the whole pending file to the 64 KB POST budget. No I/O and no exception types here —
/// the shell's crash-time writer stays minimal and calls these, and the harness can exercise the
/// scrub/truncate rules headlessly. Privacy contract (PRIVACY.md): the report is shown to the user
/// IN FULL before anything is sent, so what these functions produce is exactly what the user reads.
/// </summary>
public static class CrashReport
{
    /// <summary>Maximum pending-report size in UTF-8 bytes — the crash.php POST body cap.</summary>
    public const int MaxBytes = 64 * 1024;

    /// <summary>Marker prepended when older content was dropped to fit <see cref="MaxBytes"/>.</summary>
    public const string TruncationMarker = "[older report content truncated]\n";

    /// <summary>One report block: version, OS, driver one-liner, exception text. Fields arrive
    /// already resolved (no probing here); null/blank driver state is simply omitted.</summary>
    public static string Compose(string appVersion, string osVersion, string? driverState,
                                 string exceptionText, DateTime whenLocal)
    {
        var sb = new StringBuilder(exceptionText.Length + 256);
        sb.Append("── Radiata crash report ──\n");
        sb.Append("time: ").Append(whenLocal.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
        sb.Append("app: ").Append(appVersion).Append('\n');
        sb.Append("os: ").Append(osVersion).Append('\n');
        if (!string.IsNullOrWhiteSpace(driverState))
            sb.Append("drivers: ").Append(driverState).Append('\n');
        sb.Append("exception:\n").Append(exceptionText.TrimEnd()).Append('\n');
        return sb.ToString();
    }

    /// <summary>Replace every occurrence of the user-profile directory with "~", case-insensitively
    /// and under both separator spellings (stack traces mix them). Null/blank profile = no-op.</summary>
    public static string ScrubUserPaths(string text, string? userProfileDir)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(userProfileDir)) return text;
        var profile = userProfileDir.TrimEnd('\\', '/');
        if (profile.Length < 3) return text;   // never scrub a bare drive root
        text = text.Replace(profile, "~", StringComparison.OrdinalIgnoreCase);
        var alt = profile.Contains('\\') ? profile.Replace('\\', '/') : profile.Replace('/', '\\');
        return text.Replace(alt, "~", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Hold <paramref name="text"/> to <see cref="MaxBytes"/> of UTF-8 by dropping the
    /// OLDEST content first — the pending file is append-written, so the top is the oldest crash.
    /// The kept tail starts on a line boundary where one exists, behind <see cref="TruncationMarker"/>.</summary>
    public static string TruncateToBudget(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= MaxBytes) return text;

        int budget = MaxBytes - Encoding.UTF8.GetByteCount(TruncationMarker);
        // Walk back from the end until the tail fits the budget. Probe on char counts (cheap,
        // monotonic) rather than re-encoding per character.
        int keepChars = Math.Min(text.Length, budget);   // upper bound: ≥1 byte per char
        while (keepChars > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(text.Length - keepChars)) > budget)
            keepChars -= Math.Max(1, keepChars / 16);

        int start = text.Length - keepChars;
        int nl = text.IndexOf('\n', start);
        if (nl >= 0 && nl + 1 < text.Length) start = nl + 1;   // resume on a whole line when possible
        return TruncationMarker + text[start..];
    }
}
