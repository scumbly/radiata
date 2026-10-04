using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ControllerWheel;

/// <summary>At-rest protection for small local secrets — the user's Discord client secret and OAuth
/// access/refresh tokens — via Windows DPAPI (<see cref="DataProtectionScope.CurrentUser"/>): only the
/// signed-in Windows user on THIS machine can decrypt. Scope of the guarantee: it stops another USER's
/// process reading the file and a profile/backup sync carrying it off the box; it is NOT server-side
/// secrecy, and NOT a boundary against code running as the same Windows user (any same-user process can
/// call DPAPI Unprotect on the blob). Encrypts arbitrary UTF-8 text to a base64 blob and back; a DPAPI
/// blob (base64, no braces) is trivially distinguishable from legacy plaintext JSON, which drives the
/// transparent one-time migration in <see cref="Read"/>.</summary>
internal static class LocalSecret
{
    private const string Prefix = "dpapi:v1:";

    public static bool IsProtected(string value)
    {
        if (value.StartsWith("dpapi:", StringComparison.Ordinal)) return true;
        try
        {
            var bytes = Convert.FromBase64String(value);
            // DPAPI's provider header also identifies unmarked blobs written by older releases.
            return bytes.AsSpan().StartsWith(new byte[] { 1, 0, 0, 0, 0xD0, 0x8C, 0x9D, 0xDF,
                1, 0x15, 0xD1, 0x11, 0x8C, 0x7A, 0, 0xC0, 0x4F, 0xC2, 0x97, 0xEB });
        }
        catch (FormatException) { return false; }
    }

    /// <summary>DPAPI-encrypt <paramref name="plaintext"/> to a base64 string (CurrentUser scope).</summary>
    public static string Protect(string plaintext) =>
        Prefix + Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), optionalEntropy: null, DataProtectionScope.CurrentUser));

    /// <summary>Decrypt a blob from <see cref="Protect"/>. Null when it isn't ours — a different Windows
    /// user/machine, corruption, or a legacy plaintext file (which isn't valid base64) — so the caller can
    /// fall back to a plaintext read and re-encrypt.</summary>
    public static string? Unprotect(string protectedBase64)
    {
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                Convert.FromBase64String(protectedBase64.StartsWith(Prefix, StringComparison.Ordinal) ? protectedBase64[Prefix.Length..] : protectedBase64), optionalEntropy: null, DataProtectionScope.CurrentUser));
        }
        catch { return null; }
    }

    /// <summary>Read a file that's either DPAPI-encrypted (current) or legacy plaintext. Returns the JSON
    /// text plus whether it was legacy — so the caller re-saves it encrypted after a successful parse
    /// (transparent migration). Null when the file is absent.</summary>
    public static (string Json, bool WasLegacy)? Read(string path)
    {
        if (!File.Exists(path)) return null;
        var raw = File.ReadAllText(path);
        var decrypted = Unprotect(raw);
        if (decrypted is not null) return (decrypted, false);
        if (IsProtected(raw)) throw new CryptographicException("This protected secret cannot be read by this Windows account. Reconnect the integration to replace it.");
        return (raw, true);
    }

    /// <summary>Write <paramref name="json"/> to <paramref name="path"/>, DPAPI-encrypted. Atomic (temp +
    /// move) so an interrupted write can't leave a truncated blob that <see cref="Unprotect"/> then rejects.</summary>
    public static void Write(string path, string json) => AtomicFile.WriteAllText(path, Protect(json));
}

/// <summary>Secrets stored INSIDE config.json (currently the SteamGridDB API key — the OBS password
/// already goes through <see cref="LocalSecret"/> directly): DPAPI blob in the config field, plaintext at
/// the point of use. Accepted trade-off: a DPAPI blob doesn't survive a backup restored on another
/// machine/user — the key must be re-entered there — in exchange for the key never sitting in plaintext
/// in config.json or the portable backup ZIPs.</summary>
internal static class SecretField
{
    private const string BackupPrefix = "radiata-protected-config:v1:";
    public static string ProtectBackup(string json) => BackupPrefix + LocalSecret.Protect(json);
    public static string ReadBackup(string text) => text.StartsWith(BackupPrefix, StringComparison.Ordinal)
        ? LocalSecret.Unprotect(text[BackupPrefix.Length..])
            ?? throw new CryptographicException("This backup belongs to a different Windows account or is damaged.")
        : text;

    /// <summary>Plaintext from a stored config value: decrypts a DPAPI blob, passes legacy plaintext
    /// through unchanged (it isn't a valid blob), null for empty/absent.</summary>
    public static string? ToPlaintext(string? stored) =>
        string.IsNullOrEmpty(stored) ? null : LocalSecret.IsProtected(stored) ? LocalSecret.Unprotect(stored) : stored;

    /// <summary>Stored (DPAPI-blob) form of a plaintext secret; null-through for empty.</summary>
    public static string? ToStored(string? plaintext) =>
        string.IsNullOrEmpty(plaintext) ? null : LocalSecret.Protect(plaintext);

    /// <summary>Stored form for a SAVE: when the edited plaintext equals what the existing stored value
    /// decrypts to, the existing blob is reused — DPAPI output differs on every call, and a fresh blob
    /// per save would make "did anything change" comparisons and config diffs permanently noisy.</summary>
    public static string? ToStoredStable(string? plaintext, string? existingStored) =>
        string.IsNullOrEmpty(plaintext) && existingStored is not null && LocalSecret.IsProtected(existingStored)
            && LocalSecret.Unprotect(existingStored) is null ? existingStored :
        string.Equals(plaintext ?? "", ToPlaintext(existingStored) ?? "", StringComparison.Ordinal)
            ? (string.IsNullOrEmpty(plaintext) ? null : existingStored)
            : ToStored(plaintext);
}
