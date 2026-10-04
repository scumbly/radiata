using System.Diagnostics;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// The per-package consent ledger: %APPDATA%\Radiata\packages-consent.json.
/// A package is identified by kind + folder name + content hash — any content change invalidates
/// prior consent and re-triggers the stern first-run gate. Declines are recorded too, so a
/// declined package doesn't re-prompt every launch (it re-prompts only when its content changes).
/// Plain JSON (no secrets), atomic writes, fail-quiet reads: an unreadable ledger means nothing
/// is consented, which is the safe direction.
/// </summary>
public static class PackageConsent
{
    public static string LedgerPath => Path.Combine(AppPaths.AppDataDir, "packages-consent.json");

    public enum Verdict { Unknown, Accepted, Declined }

    private sealed record Entry(string Kind, string Folder, string Hash, bool Accepted, string DecidedUtc);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static List<Entry> Load()
    {
        try
        {
            if (!File.Exists(LedgerPath)) return [];
            return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(LedgerPath), JsonOpts) ?? [];
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Packages] consent ledger unreadable ({ex.Message}) — treating all packages as unconsented");
            return [];
        }
    }

    private static void Save(List<Entry> entries)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDir);
            var json = JsonSerializer.Serialize(entries, JsonOpts);
            AtomicFile.WriteAllText(LedgerPath, json);
        }
        catch (Exception ex) { Trace.WriteLine($"[Packages] consent ledger write failed: {ex.Message}"); }
    }

    public static Verdict Check(string kind, string folder, string contentHash)
    {
        if (string.IsNullOrEmpty(contentHash)) return Verdict.Unknown;   // no identity → never consented
        var e = Load().FirstOrDefault(x =>
            x.Kind == kind &&
            string.Equals(x.Folder, folder, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Hash, contentHash, StringComparison.OrdinalIgnoreCase));
        return e is null ? Verdict.Unknown : e.Accepted ? Verdict.Accepted : Verdict.Declined;
    }

    /// <summary>Record a decision, replacing any prior entry for the same kind+folder (any hash) —
    /// one live decision per package slot keeps the ledger from growing with every edit.</summary>
    public static void Record(string kind, string folder, string contentHash, bool accepted)
    {
        var entries = Load();
        entries.RemoveAll(x => x.Kind == kind &&
                               string.Equals(x.Folder, folder, StringComparison.OrdinalIgnoreCase));
        entries.Add(new Entry(kind, folder, contentHash, accepted,
                              DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")));
        Save(entries);
    }
}
