using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// A validated <c>game.json</c> from an Arcade Games drop-in package. Same posture as
/// <see cref="MaterialPackage.TryParse"/>: every string bounded, null+reason instead of a throw —
/// a bad package is listed and skipped, never a crash. The manifest carries no code; the entry
/// file's text is read separately at session start and shipped over the pipe.
/// </summary>
public sealed record ScriptGameManifest
{
    /// <summary>Catalog/config token: "pkg-" + manifest id. Prefixed so a package id can never
    /// collide with a built-in game id (kabloom/connate) in a slice's Command payload.</summary>
    public required string Token { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Self-reported, for the consent card only — never trusted for anything.</summary>
    public required string Author { get; init; }
    /// <summary>Bare .js file name inside the package folder (grammar-validated — traversal is
    /// unrepresentable). Existence and the 256 KB cap are checked by the scanner.</summary>
    public required string EntryFile { get; init; }
    /// <summary>Optional how-to card lines, ≤5, each ≤80 chars. Empty = no card, no △ route.</summary>
    public required IReadOnlyList<string> HowTo { get; init; }
    /// <summary>Optional cabinet colour in the Arcade Launcher, normalised to <c>#RRGGBB</c>; null = the plain
    /// grey cabinet.</summary>
    public string? TintHex { get; init; }
    /// <summary>Optional bare .png/.jpg file name inside the package folder: the cabinet screen's picture until
    /// the player's own capture exists (grammar-validated here, existence checked by the scanner).</summary>
    public string? PreviewFile { get; init; }
    /// <summary>Optional bare .png file name inside the package folder: the illustration floated left of the
    /// title on the cabinet's nameplate, as the built-in cabinets carry theirs. Drawn in its own colours.</summary>
    public string? BadgeFile { get; init; }
    /// <summary>Optional bare .png file name inside the package folder: the game's slice glyph. Its alpha is the
    /// shape; the wheel tints it like any other glyph.</summary>
    public string? GlyphFile { get; init; }
    /// <summary>Package folder name (diagnostics + consent dialog), not trusted for identity.</summary>
    public required string FolderName { get; init; }
    /// <summary>Whole-folder SHA-256 — the consent identity, set by the scanner.</summary>
    public required string ContentHash { get; init; }
    /// <summary>Package folder absolute path (set by the scanner).</summary>
    public string DirPath { get; init; } = "";

    public const int FormatVersion = 1;
    public const string ManifestFileName = "game.json";
    public const string TokenPrefix = "pkg-";
    public const int MaxEntryFileBytes = 256 * 1024;
    public const int MaxHowToLines = 5;
    public const int MaxHowToLineLength = 80;

    public static ScriptGameManifest? TryParse(string manifestJson, string folderName,
        string contentHash, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(manifestJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 8,
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "manifest is not a JSON object"; return null; }

            if (!root.TryGetProperty("format", out var f) || f.ValueKind != JsonValueKind.Number
                || !f.TryGetInt32(out int format) || format != FormatVersion)
            { error = $"unsupported or missing format (this Radiata reads format {FormatVersion})"; return null; }

            string? id = GetString(root, "id");
            if (id is null || !IdIsValid(id))
            { error = "id must be 1-32 chars of a-z, 0-9, '-'"; return null; }

            string? title = Clean(GetString(root, "title"), 24);
            if (string.IsNullOrWhiteSpace(title)) { error = "title is required (≤24 chars)"; return null; }

            string? entry = GetString(root, "entry");
            if (entry is null || !MaterialPackage.FileNameIsValid(entry, ".js"))
            { error = "entry must be a bare .js file name inside the package folder"; return null; }

            var howTo = new List<string>();
            if (root.TryGetProperty("howTo", out var ht))
            {
                if (ht.ValueKind != JsonValueKind.Array) { error = "howTo must be an array of strings"; return null; }
                foreach (var line in ht.EnumerateArray())
                {
                    if (line.ValueKind != JsonValueKind.String)
                    { error = "howTo must be an array of strings"; return null; }
                    var text = Clean(line.GetString(), MaxHowToLineLength);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    howTo.Add(text!);
                    if (howTo.Count >= MaxHowToLines) break;
                }
            }

            string? tint = null;
            if (root.TryGetProperty("tint", out var tn))
            {
                if (tn.ValueKind != JsonValueKind.String || !MaterialPackage.TryParseColor(tn.GetString(), out uint argb))
                { error = "tint must be a colour written #RGB or #RRGGBB"; return null; }
                tint = $"#{argb & 0xFFFFFF:X6}";   // the cabinet is opaque: any alpha pair is dropped
            }

            if (!OptionalFile(root, "preview", out var preview, out error, ".png", ".jpg", ".jpeg")) return null;
            // Badge and glyph are drawn over other art, so they need transparency: PNG only.
            if (!OptionalFile(root, "badge", out var badge, out error, ".png")) return null;
            if (!OptionalFile(root, "glyph", out var glyph, out error, ".png")) return null;

            return new ScriptGameManifest
            {
                Token = TokenPrefix + id,
                Id = id,
                Title = title!,
                Author = Clean(GetString(root, "author"), 40) is { Length: > 0 } a ? a : "(not stated)",
                EntryFile = entry,
                HowTo = howTo,
                TintHex = tint,
                PreviewFile = preview,
                BadgeFile = badge,
                GlyphFile = glyph,
                FolderName = folderName,
                ContentHash = contentHash,
            };
        }
        catch (JsonException ex) { error = $"game.json is not valid JSON: {ex.Message}"; return null; }
        catch (Exception ex) { error = ex.Message; return null; }
    }

    /// <summary>An optional file key: absent is fine, present must be a bare file name with one of the
    /// extensions (existence is the scanner's check).</summary>
    private static bool OptionalFile(JsonElement root, string key, out string? file, out string? error, params string[] extensions)
    {
        file = null; error = null;
        if (!root.TryGetProperty(key, out var v)) return true;
        if (v.ValueKind != JsonValueKind.String || !MaterialPackage.FileNameIsValid(v.GetString(), extensions))
        {
            error = $"{key} must be a bare {string.Join("/", extensions)} file name inside the package folder";
            return false;
        }
        file = v.GetString();
        return true;
    }

    private static bool IdIsValid(string id)
    {
        if (id.Length is < 1 or > 32) return false;
        foreach (var c in id)
            if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')) return false;
        return true;
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Length-bound + strip control characters; the same hostile-input hygiene the
    /// material manifest applies.</summary>
    private static string? Clean(string? s, int maxLen)
    {
        if (s is null) return null;
        var sb = new System.Text.StringBuilder(Math.Min(s.Length, maxLen));
        foreach (var c in s)
        {
            // Control (Cc) and Format (Cf): the bidi controls — RLO/LRO, the isolates, the marks — are Format
            // characters, and a name carrying one would visually reorder itself in the consent dialog once a
            // right-to-left language is honoured. A package name never needs them.
            if (char.IsControl(c) || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
            sb.Append(c);
            if (sb.Length >= maxLen) break;
        }
        return sb.ToString().Trim();
    }
}
