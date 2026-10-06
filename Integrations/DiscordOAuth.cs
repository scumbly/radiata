using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>The user's own Discord application credentials (Client ID + Secret), stored at
/// <c>%APPDATA%\Radiata\discord-oauth.json</c>. No secrets ship with Radiata — each user brings their own
/// free app credentials (Developer Portal ▸ New Application ▸ OAuth2). Written by the onboarding Discord
/// step and the Settings ▸ Advanced ▸ Discord fields (both operate on this one file), read by the
/// Discord voice-join action. Shared so post-onboarding edits and first-run setup stay in sync.</summary>
public static class DiscordOAuth
{
    public static string CredentialsPath => Path.Combine(AppPaths.AppDataDir, "discord-oauth.json");

    public static bool IsConfigured => File.Exists(CredentialsPath);

    /// <summary>The saved Client ID + Secret, or null if none is stored / the file is unreadable. The file
    /// is DPAPI-encrypted at rest (<see cref="LocalSecret"/>); a legacy plaintext file is read once and
    /// re-saved encrypted.</summary>
    public static (string ClientId, string ClientSecret)? Load()
    {
        try
        {
            if (LocalSecret.Read(CredentialsPath) is not { } read) return null;
            var (json, wasLegacy) = read;
            using var doc = JsonDocument.Parse(json);
            var id     = doc.RootElement.TryGetProperty("clientId", out var i) ? i.GetString() : null;
            var secret = doc.RootElement.TryGetProperty("clientSecret", out var s) ? s.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret)) return null;
            if (wasLegacy) Save(id!, secret!);   // migrate plaintext → encrypted
            return (id!, secret!);
        }
        catch (Exception ex) { Trace.WriteLine($"[Discord] load creds failed: {ex.Message}"); return null; }
    }

    /// <summary>Write the credentials (clientId/clientSecret + the fixed http://localhost redirect),
    /// DPAPI-encrypted at rest.</summary>
    public static void Save(string clientId, string clientSecret)
    {
        Directory.CreateDirectory(AppPaths.AppDataDir);
        LocalSecret.Write(CredentialsPath, JsonSerializer.Serialize(
            new { clientId, clientSecret, redirectUri = "http://localhost" },
            new JsonSerializerOptions { WriteIndented = true }));
        DiscordIpc.InvalidateApp();   // apply new creds without an app restart (see DiscordIpc.App)
    }

    /// <summary>Validate a Client ID + Secret via the OAuth2 client-credentials grant: a 200 means the
    /// pair is a genuine app credential (401 = wrong id/secret). No user interaction, doesn't touch the
    /// saved token flow — purely a credential check.</summary>
    public static async Task<bool> TestAsync(string clientId, string clientSecret)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/v10/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"]      = "identify",
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        using var resp = await http.SendAsync(req).ConfigureAwait(true);
        return resp.IsSuccessStatusCode;
    }
}
