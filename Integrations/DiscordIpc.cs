using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// Toggles a Discord voice-channel connection via the Discord desktop client's local IPC pipe:
/// joins the target channel, or leaves it if already connected there.
///
/// Requires user-supplied Discord app credentials in %APPDATA%\Radiata\discord-oauth.json (see
/// <see cref="OAuthFile"/>); absent, the feature is a no-op (logged). No shared secret ships in the build.
///
/// Flow:
///   First run  — HANDSHAKE → READY → AUTHORIZE (Discord shows a one-time approval dialog)
///              → exchange code for OAuth token → cache token → AUTHENTICATE → SELECT_VOICE_CHANNEL
///   Subsequent — HANDSHAKE → READY → AUTHENTICATE (cached token, no dialog) → SELECT_VOICE_CHANNEL
///   Token expired — refresh silently, fall back to full AUTHORIZE if refresh fails
/// </summary>
internal static class DiscordIpc
{
    private const string RedirectUriDefault = "http://localhost"; // must be registered in the app's OAuth2 → Redirects

    // The RPC scopes every token is minted with. `rpc` alone carries the voice-channel commands, but the
    // voice settings commands (GET/SET_VOICE_SETTINGS, behind Deafen and Mute Me) are documented under
    // rpc.voice.read / rpc.voice.write. These are restricted scopes: being the app's developer does
    // not guarantee availability or consent. Propagate authorization and command errors to the user.
    // ⚠ A cached token minted before this list grew still AUTHENTICATEs — it is a valid token — and only
    // fails at the command that needs the scope it lacks. That is why the voice-settings toggle treats a
    // command error as a possible scope shortfall and re-AUTHORIZEs once (see ToggleVoiceSettingAsync);
    // without it, a user who had used Join Voice Channel before this build would get silence forever.
    private const string RpcScopes = "\"rpc\",\"rpc.voice.read\",\"rpc.voice.write\"";

    // Discord app credentials are user-supplied and never committed: %APPDATA%\Radiata\discord-oauth.json
    //   { "clientId": "…", "clientSecret": "…", "redirectUri": "http://localhost" }
    // Never ship a shared secret here: a distributed desktop app can't keep one secret, and Discord's
    // token endpoint requires a secret for this RPC authorization-code flow (there is no PKCE/public-client
    // path), so each user brings their own Discord application.
    private static readonly string OAuthFile = Path.Combine(AppPaths.AppDataDir, "discord-oauth.json");

    private sealed record OAuthApp(string ClientId, string ClientSecret, string RedirectUri);

    private static OAuthApp? _app;
    // Cache a successful load only — never memoize a null. A null (file missing/invalid) must be retried
    // on the next use so credentials saved at runtime (Settings ▸ Advanced ▸ Discord / onboarding) apply
    // without an app restart. DiscordOAuth.Save also calls InvalidateApp() so a credential *change* is
    // picked up immediately, not just a first-time config.
    private static OAuthApp? App => _app ??= LoadApp();

    /// <summary>Forget the cached credentials so the next voice action re-reads discord-oauth.json. Called
    /// when the user saves credentials at runtime so they apply without restarting Radiata.</summary>
    internal static void InvalidateApp() => _app = null;

    private static OAuthApp? LoadApp()
    {
        try
        {
            if (LocalSecret.Read(OAuthFile) is not { } read) return null;
            var (json, wasLegacy) = read;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? id     = root.TryGetProperty("clientId",     out var i) ? i.GetString() : null;
            string? secret = root.TryGetProperty("clientSecret", out var s) ? s.GetString() : null;
            string  redir  = (root.TryGetProperty("redirectUri", out var r) ? r.GetString() : null)
                             is { Length: > 0 } u ? u : RedirectUriDefault;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret)) return null;
            if (wasLegacy) try { LocalSecret.Write(OAuthFile, json); } catch { /* migrate best-effort */ }
            return new OAuthApp(id!, secret!, redir);
        }
        catch { return null; }
    }

    private static readonly string TokenFile = Path.Combine(AppPaths.AppDataDir, "discord-token.json");

    private static readonly string LogFile = Path.Combine(AppPaths.AppDataDir, "discord-debug.txt");

    // A single join/leave takes ~180 ms and the slice can be re-fired before it finishes. Overlapping
    // invocations each open their own IPC pipe and thrash Discord's voice state into a multi-second stall,
    // so only one may run at a time; presses arriving while one is in flight are dropped (a duplicate
    // toggle inside that window is unwanted anyway). Interlocked, so it's safe off any thread.
    private static int _busy;   // 0 = idle, 1 = a join/leave is running

    public static void JoinVoiceChannel(string discordUrl)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            WriteLog("voice toggle ignored — a previous join/leave is still in flight");
            return;
        }
        _ = RunGuardedAsync(discordUrl);
    }

    private static readonly AsyncLocal<CancellationToken> OperationToken = new();

    private static Task RunGuardedAsync(string discordUrl) =>
        RunGuardedOperationAsync(() => JoinAsync(discordUrl), TimeSpan.FromSeconds(120));

    private static async Task RunGuardedOperationAsync(Func<Task> operation, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        var previous = OperationToken.Value;
        OperationToken.Value = deadline.Token;
        try
        {
            // Await the operation through cancellation and cleanup. A timeout never releases ownership
            // while an older operation can still consume replies from the shared connection.
            await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { WriteLog("Discord operation timed out or was cancelled"); }
        catch (Exception ex) { WriteLog($"Discord operation failed: {ex.GetType().Name}: {ex.Message}"); }
        finally
        {
            if (deadline.IsCancellationRequested) ResetConnection();
            OperationToken.Value = previous;
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static bool IsDiscordRunning() =>
        WindowsPlatformActions.AnySessionProcess("Discord");

    // Start a cold Discord. The Update stub is the reliable route (it resolves the versioned app folder);
    // the discord:// deep link is only a fallback, because the protocol handler is absent on an install
    // that has never run and Process.Start then throws instead of launching anything.
    private static void LaunchDiscordClient(string discordUrl)
    {
        var update = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Discord\Update.exe");
        if (File.Exists(update))
        {
            WriteLog("Discord not running — launching via Update.exe");
            try
            {
                Process.Start(new ProcessStartInfo(update, "--processStart Discord.exe") { UseShellExecute = false });
                return;
            }
            catch (Exception ex) { WriteLog($"Update.exe launch failed ({ex.GetType().Name}) — falling back to the deep link"); }
        }

        WriteLog("Discord not running — launching via deep link");
        try { Process.Start(new ProcessStartInfo(discordUrl) { UseShellExecute = true }); }
        catch (Exception ex) { WriteLog($"deep-link launch failed: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static async Task JoinAsync(string discordUrl)
    {
        var channelId = ParseChannelId(discordUrl);
        if (channelId == null) { WriteLog($"Cannot parse channel ID from '{discordUrl}'"); return; }

        if (App is null)
        {
            WriteLog("Discord voice-join is not configured — set up the Discord integration in Settings ▸ Advanced ▸ Discord.");
            return;
        }

        // Only use the discord:// deep link to launch Discord when it isn't already running. With Discord
        // up, the deep-link navigation races SELECT_VOICE_CHANNEL and leaves you viewing the channel
        // without voice connected; the IPC call alone focuses and connects voice in one shot.
        if (!IsDiscordRunning())
        {
            LaunchDiscordClient(discordUrl);
            if (!await WaitForIpcAsync(TimeSpan.FromSeconds(45)).ConfigureAwait(false))
            {
                WriteLog("Discord never published its IPC pipe — join abandoned");
                return;
            }
        }

        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        if (token == null) { WriteLog("Could not obtain access token"); return; }

        bool joined = await ToggleViaIpcAsync(token, channelId).ConfigureAwait(false);

        // Deep-link focus only after a join, and only sequenced after the IPC call so it can't race the
        // voice connect. When leaving, don't navigate anywhere.
        if (joined)
            Process.Start(new ProcessStartInfo(discordUrl) { UseShellExecute = true });
    }

    // ── Token acquisition ─────────────────────────────────────────────────────

    private static async Task<string?> GetAccessTokenAsync()
    {
        var cached = LoadCachedToken();

        if (cached != null && cached.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            WriteLog("Using cached token");
            return cached.AccessToken;
        }

        if (cached?.RefreshToken is { } rt)   // silent refresh before any user-facing prompt
        {
            WriteLog("Refreshing token...");
            var refreshed = await RefreshTokenAsync(rt).ConfigureAwait(false);
            if (refreshed != null) return refreshed;
        }

        // Full authorization — Discord shows a one-time approval popup
        WriteLog("Starting first-time OAuth authorization...");
        return await AuthorizeAsync().ConfigureAwait(false);
    }

    private static async Task<string?> AuthorizeAsync()
    {
        using var pipe = await ConnectAsync().ConfigureAwait(false);
        if (pipe == null) { WriteLog("Discord IPC not available for AUTHORIZE"); return null; }

        try
        {
            await HandshakeAsync(pipe).ConfigureAwait(false);

            string nonce = NewNonce();
            await WriteFrameAsync(pipe, OpcodeFrame,
                $"{{\"cmd\":\"AUTHORIZE\",\"args\":{{\"client_id\":\"{App!.ClientId}\",\"scopes\":[{RpcScopes}]}},\"nonce\":\"{nonce}\"}}")
                .ConfigureAwait(false);

            WriteLog("Waiting for user to click Authorize in Discord (up to 60 s)...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var authJson = await ReadUntilCmdAsync(pipe, "AUTHORIZE", nonce, cts.Token).ConfigureAwait(false);
            WriteLog("AUTHORIZE → response received");   // don't log the raw payload — it carries the auth code

            using var doc = JsonDocument.Parse(authJson);
            if (IsError(doc)) { WriteLog("AUTHORIZE returned error"); return null; }

            var code = doc.RootElement.GetProperty("data").GetProperty("code").GetString();
            if (code == null) { WriteLog("No code in AUTHORIZE response"); return null; }

            var tokenCache = await ExchangeCodeAsync(code).ConfigureAwait(false);
            return tokenCache?.AccessToken;
        }
        catch (OperationCanceledException)
        {
            WriteLog("AUTHORIZE timed out — user did not approve within 60 s");
            return null;
        }
        catch (Exception ex)
        {
            WriteLog($"AuthorizeAsync: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // ── Persistent RPC connection ─────────────────────────────────────────────
    // Discord's RPC is meant to run over one long-lived pipe. Never re-open + re-handshake +
    // re-AUTHENTICATE per toggle: that connect/disconnect churn makes Discord stall the handshake READY by
    // tens of seconds after a few rapid cycles. Connect + handshake + authenticate once and reuse the pipe
    // for every GET/SELECT (~10 ms per repeat toggle). The _busy guard (see JoinVoiceChannel) serializes
    // access, so only one operation ever touches _pipe at a time.
    private static NamedPipeClientStream? _pipe;
    private static bool _authenticated;

    private static void ResetConnection()
    {
        _authenticated = false;
        try { _pipe?.Dispose(); } catch { }
        _pipe = null;
    }

    /// <summary>Return a live, authenticated RPC pipe — reusing the persistent one when it's still good,
    /// otherwise establishing it (connect → handshake → AUTHENTICATE). If AUTHENTICATE reports a
    /// bad/expired token, clears the cache, runs a full AUTHORIZE, and tries once more. Null if Discord IPC
    /// is unavailable or auth ultimately fails.</summary>
    private static async Task<NamedPipeClientStream?> EnsureSessionAsync(string accessToken)
    {
        if (_pipe is { IsConnected: true } && _authenticated) return _pipe;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            ResetConnection();
            var pipe = await ConnectAsync().ConfigureAwait(false);
            if (pipe == null) { WriteLog("Discord IPC not available"); return null; }
            _pipe = pipe;

            await HandshakeAsync(pipe).ConfigureAwait(false);
            string nonce = NewNonce();
            await WriteFrameAsync(pipe, OpcodeFrame,
                $"{{\"cmd\":\"AUTHENTICATE\",\"args\":{{\"access_token\":\"{accessToken}\"}},\"nonce\":\"{nonce}\"}}")
                .ConfigureAwait(false);
            var authJson = await ReadUntilCmdAsync(pipe, "AUTHENTICATE", nonce).ConfigureAwait(false);
            WriteLog("AUTHENTICATE → response received");
            using (var authDoc = JsonDocument.Parse(authJson))
            {
                if (!IsError(authDoc)) { _authenticated = true; return pipe; }
            }

            // Bad/expired token — clear it, and on the first pass re-authorize for a fresh one.
            WriteLog("AUTHENTICATE failed — clearing token cache" + (attempt == 0 ? " and re-authorizing" : ""));
            ResetConnection();
            ClearCachedToken();
            if (attempt == 0)
            {
                var newToken = await AuthorizeAsync().ConfigureAwait(false);
                if (newToken == null) return null;
                accessToken = newToken;
            }
        }
        return null;
    }

    /// <summary>Toggle voice connection: leave if already connected to <paramref name="channelId"/>,
    /// otherwise join it. Returns true if we joined, false if we left (or on failure).</summary>
    private static async Task<bool> ToggleViaIpcAsync(string accessToken, string channelId)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var pipe = await EnsureSessionAsync(accessToken).ConfigureAwait(false);
                if (pipe == null) return false;

                // Toggle: if already connected to this channel, leave (channel_id: null); else join.
                var currentId = await GetSelectedVoiceChannelAsync(pipe).ConfigureAwait(false);
                bool leave = currentId == channelId;
                string channelArg = leave ? "null" : $"\"{channelId}\"";

                string nonce = NewNonce();
                await WriteFrameAsync(pipe, OpcodeFrame,
                    $"{{\"cmd\":\"SELECT_VOICE_CHANNEL\",\"args\":{{\"channel_id\":{channelArg},\"force\":true}},\"nonce\":\"{nonce}\"}}")
                    .ConfigureAwait(false);

                var json = await ReadUntilCmdAsync(pipe, "SELECT_VOICE_CHANNEL", nonce).ConfigureAwait(false);
                WriteLog($"SELECT_VOICE_CHANNEL ({(leave ? "leave" : "join")}) → {Clip(json)}");
                using var response = JsonDocument.Parse(json);
                return !IsError(response) && !leave;
            }
            catch (Exception ex) when (attempt == 0 && ex is IOException or ObjectDisposedException)
            {
                // The reused pipe went stale (Discord restarted / connection dropped) — drop it and retry
                // once with a fresh session.
                WriteLog($"toggle pipe stale ({ex.GetType().Name}) — reconnecting and retrying");
                ResetConnection();
            }
            catch (Exception ex)
            {
                WriteLog($"ToggleViaIpcAsync: {ex.GetType().Name}: {ex.Message}");
                ResetConnection();
                return false;
            }
        }
        return false;
    }

    /// <summary>The voice channel id the user is currently connected to, or null if not in voice.</summary>
    private static async Task<string?> GetSelectedVoiceChannelAsync(NamedPipeClientStream pipe)
    {
        string nonce = NewNonce();
        await WriteFrameAsync(pipe, OpcodeFrame,
            $"{{\"cmd\":\"GET_SELECTED_VOICE_CHANNEL\",\"args\":{{}},\"nonce\":\"{nonce}\"}}")
            .ConfigureAwait(false);
        var json = await ReadUntilCmdAsync(pipe, "GET_SELECTED_VOICE_CHANNEL", nonce).ConfigureAwait(false);
        WriteLog("GET_SELECTED_VOICE_CHANNEL → response received");
        using (var response = JsonDocument.Parse(json))
            if (IsError(response)) throw new InvalidOperationException("Discord refused the current voice-channel query.");
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("id", out var id))
                return id.GetString();
        }
        catch { /* data is null when not connected to any voice channel */ }
        return null;
    }

    // ── Voice settings: Deafen and Mute Me ────────────────────────────────────
    // Both are toggles over the same RPC session the voice-channel action uses, which is why they share its
    // _busy guard and its persistent pipe. They send no keystroke: Discord's Ctrl+Shift+M / Ctrl+Shift+D
    // ship as defaults that only fire while Discord is focused, so the keystroke route never worked
    // mid-game and leaked the combo into the game on the way past; both switches are RPC-only, with no keystroke fallback.

    /// <summary>Which voice switch a slice flips.</summary>
    internal enum VoiceSwitch { Mute, Deafen }

    public static void ToggleVoiceSetting(bool deafen)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            WriteLog("voice-setting toggle ignored — a previous Discord call is still in flight");
            return;
        }
        _ = RunVoiceSettingGuardedAsync(deafen ? VoiceSwitch.Deafen : VoiceSwitch.Mute);
    }

    private static Task RunVoiceSettingGuardedAsync(VoiceSwitch which) =>
        RunGuardedOperationAsync(() => ToggleVoiceSettingAsync(which), TimeSpan.FromSeconds(90));

    private static async Task ToggleVoiceSettingAsync(VoiceSwitch which)
    {
        if (App is null)
        {
            WriteLog($"Discord {which} is not configured — set up the Discord integration in Settings ▸ Advanced ▸ Discord.");
            return;
        }

        // Unlike a join, there is nothing to launch into: deafening a Discord that isn't running is not a
        // thing the user can have meant, and starting it would be a surprise from a mute button.
        if (!IsDiscordRunning()) { WriteLog($"Discord {which}: Discord isn't running"); return; }

        var token = await GetAccessTokenAsync().ConfigureAwait(false);
        if (token == null) { WriteLog("Could not obtain access token"); return; }

        // Two passes at most. The second exists for one cause: a cached token minted before the voice
        // scopes joined the list (see RpcScopes). It authenticates fine and then refuses the command, and
        // the only cure is a fresh AUTHORIZE — which costs the user one approval click, once, forever.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var pipe = await EnsureSessionAsync(token).ConfigureAwait(false);
                if (pipe == null) return;

                // A toggle needs the current state, so read before writing. Reading also proves the scope:
                // if GET is refused the SET would be too, and we can re-authorize without having flipped
                // anything (a blind SET that half-worked would leave the user unsure what they'd pressed).
                bool? current = await GetVoiceSettingAsync(pipe, which).ConfigureAwait(false);
                if (current is null)
                {
                    if (attempt == 0 && await ReauthorizeAsync() is { } fresh) { token = fresh; continue; }
                    WriteLog($"Discord {which}: could not read the current state");
                    return;
                }

                string field = which == VoiceSwitch.Deafen ? "deaf" : "mute";
                string nonce = NewNonce();
                await WriteFrameAsync(pipe, OpcodeFrame,
                    $"{{\"cmd\":\"SET_VOICE_SETTINGS\",\"args\":{{\"{field}\":{(current.Value ? "false" : "true")}}},\"nonce\":\"{nonce}\"}}")
                    .ConfigureAwait(false);
                var json = await ReadUntilCmdAsync(pipe, "SET_VOICE_SETTINGS", nonce).ConfigureAwait(false);
                WriteLog($"SET_VOICE_SETTINGS {field}={!current.Value} → {Clip(json)}");

                using var doc = JsonDocument.Parse(json);
                if (!IsError(doc)) return;
                if (attempt == 0 && await ReauthorizeAsync() is { } retryToken) { token = retryToken; continue; }
                WriteLog($"Discord {which}: SET_VOICE_SETTINGS refused");
                return;
            }
            catch (Exception ex) when (attempt == 0 && ex is IOException or ObjectDisposedException)
            {
                // The reused pipe went stale (Discord restarted) — same recovery the channel toggle takes.
                WriteLog($"voice-setting pipe stale ({ex.GetType().Name}) — reconnecting and retrying");
                ResetConnection();
            }
            catch (Exception ex)
            {
                WriteLog($"ToggleVoiceSettingAsync: {ex.GetType().Name}: {ex.Message}");
                ResetConnection();
                return;
            }
        }
    }

    /// <summary>The current state of one voice switch, or null if it could not be read — which is how a
    /// missing scope arrives, since Discord answers a command it won't run with an error frame.</summary>
    private static async Task<bool?> GetVoiceSettingAsync(NamedPipeClientStream pipe, VoiceSwitch which)
    {
        string nonce = NewNonce();
        await WriteFrameAsync(pipe, OpcodeFrame,
            $"{{\"cmd\":\"GET_VOICE_SETTINGS\",\"args\":{{}},\"nonce\":\"{nonce}\"}}")
            .ConfigureAwait(false);
        var json = await ReadUntilCmdAsync(pipe, "GET_VOICE_SETTINGS", nonce).ConfigureAwait(false);
        WriteLog($"GET_VOICE_SETTINGS → {Clip(json)}");
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (IsError(doc)) return null;
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty(which == VoiceSwitch.Deafen ? "deaf" : "mute", out var v) &&
                v.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return v.GetBoolean();
        }
        catch (JsonException) { /* fall through — unreadable is the same as refused here */ }
        return null;
    }

    /// <summary>Drop the cached token and mint a fresh one on the current scope list. Returns the new
    /// access token, or null if the user declined or Discord refused.</summary>
    private static async Task<string?> ReauthorizeAsync()
    {
        WriteLog("voice settings refused on the cached token — re-authorizing on the current scopes");
        ResetConnection();
        ClearCachedToken();
        return await AuthorizeAsync().ConfigureAwait(false);
    }


    // ── HTTP token exchange ───────────────────────────────────────────────────

    private static readonly HttpClient Http = new();

    private static async Task<TokenCache?> ExchangeCodeAsync(string code)
    {
        return await PostTokenAsync(new Dictionary<string, string>
        {
            ["client_id"]     = App!.ClientId,
            ["client_secret"] = App!.ClientSecret,
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = App!.RedirectUri,
        }).ConfigureAwait(false);
    }

    private static async Task<string?> RefreshTokenAsync(string refreshToken)
    {
        var cache = await PostTokenAsync(new Dictionary<string, string>
        {
            ["client_id"]     = App!.ClientId,
            ["client_secret"] = App!.ClientSecret,
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken,
        }).ConfigureAwait(false);
        return cache?.AccessToken;
    }

    private static async Task<TokenCache?> PostTokenAsync(Dictionary<string, string> fields)
    {
        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var resp = await Http.PostAsync("https://discord.com/api/oauth2/token",
                content, OperationToken.Value).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(OperationToken.Value).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            // Never log the raw token response — it carries the access + refresh tokens. Status only, plus
            // the non-secret OAuth error code on failure.
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                WriteLog($"Token endpoint error: {err.GetString()} (HTTP {(int)resp.StatusCode})");
                return null;
            }

            var access  = doc.RootElement.GetProperty("access_token").GetString()!;
            var refresh = doc.RootElement.GetProperty("refresh_token").GetString()!;
            var expiry  = DateTime.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32());
            WriteLog($"Token endpoint → HTTP {(int)resp.StatusCode}, token acquired");

            var cache = new TokenCache(access, refresh, expiry);
            SaveCachedToken(cache);
            return cache;
        }
        catch (Exception ex)
        {
            WriteLog($"PostTokenAsync: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // ── Token cache ───────────────────────────────────────────────────────────

    private record TokenCache(string AccessToken, string RefreshToken, DateTime ExpiresAt);

    // The token cache holds live delegated access to the user's Discord, so it stays DPAPI-encrypted at
    // rest (LocalSecret), like discord-oauth.json. Legacy plaintext is read once and re-saved encrypted.
    private static TokenCache? LoadCachedToken()
    {
        try
        {
            if (LocalSecret.Read(TokenFile) is not { } read) return null;
            var (json, wasLegacy) = read;
            using var doc = JsonDocument.Parse(json);
            var cache = new TokenCache(
                doc.RootElement.GetProperty("access_token").GetString()!,
                doc.RootElement.GetProperty("refresh_token").GetString()!,
                doc.RootElement.GetProperty("expires_at").GetDateTime());
            if (wasLegacy) SaveCachedToken(cache);   // migrate plaintext → encrypted
            return cache;
        }
        catch { return null; }
    }

    private static void SaveCachedToken(TokenCache cache)
    {
        try
        {
            LocalSecret.Write(TokenFile, JsonSerializer.Serialize(new
            {
                access_token  = cache.AccessToken,
                refresh_token = cache.RefreshToken,
                expires_at    = cache.ExpiresAt,
            }));
        }
        catch (Exception ex) { Trace.WriteLine($"[Discord] token cache write failed: {ex.Message}"); }
    }

    private static void ClearCachedToken()
    {
        try { File.Delete(TokenFile); } catch { }
    }

    // ── IPC plumbing ──────────────────────────────────────────────────────────

    private const int OpcodeHandshake = 0;
    private const int OpcodeFrame     = 1;

    /// <summary>Poll until Discord's RPC pipe answers, up to <paramref name="budget"/>.
    ///
    /// <para>A cold-booted Discord takes far longer than any fixed sleep to publish the pipe — update check,
    /// splash, login — so a single delay meant the first press only ever launched the client and the join
    /// needed a second press. Don't put a fixed delay back here.</para></summary>
    private static async Task<bool> WaitForIpcAsync(TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            var pipe = await ConnectAsync().ConfigureAwait(false);
            if (pipe != null) { pipe.Dispose(); return true; }
            await Task.Delay(500, OperationToken.Value).ConfigureAwait(false);
        }
        return false;
    }

    private static async Task<NamedPipeClientStream?> ConnectAsync()
    {
        var t0 = DateTime.UtcNow;
        for (int i = 0; i <= 9; i++)
        {
            OperationToken.Value.ThrowIfCancellationRequested();
            NamedPipeClientStream? pipe = null;
            try
            {
                // CurrentUserOnly is required: it makes Windows verify the pipe server is owned by this
                // same user, so a process on a different local account can't publish "discord-ipc-N" first
                // and collect the user's Discord access token, which AUTHENTICATE sends in the clear. It
                // does not defend against same-user malware (which could read Discord's own token store).
                pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                    PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.ConnectAsync(500, OperationToken.Value).ConfigureAwait(false);
                WriteLog($"connected slot {i} in {(DateTime.UtcNow - t0).TotalMilliseconds:F0}ms");
                var connected = pipe;
                pipe = null;
                return connected;
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x80070002))
            {
                // This slot doesn't exist. Keep scanning — a second Discord install (or a stale slot 0)
                // can leave the live client sitting on a higher slot.
            }
            catch (TimeoutException) { WriteLog($"slot {i} busy (timeout)"); /* try next */ }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { WriteLog($"ConnectAsync slot {i}: {ex.Message}"); }
            finally { pipe?.Dispose(); }
        }
        return null;
    }

    private static async Task HandshakeAsync(NamedPipeClientStream pipe)
    {
        await WriteFrameAsync(pipe, OpcodeHandshake,
            $"{{\"v\":1,\"client_id\":\"{App!.ClientId}\"}}").ConfigureAwait(false);
        WriteLog("handshake sent, awaiting READY");
        var ready = await ReadUntilEvtAsync(pipe, "READY").ConfigureAwait(false);
        WriteLog($"READY → {Clip(ready)}");
    }

    private static async Task WriteFrameAsync(NamedPipeClientStream pipe, int opcode, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var buf     = new byte[8 + payload.Length];
        BitConverter.TryWriteBytes(buf.AsSpan(0, 4), opcode);
        BitConverter.TryWriteBytes(buf.AsSpan(4, 4), payload.Length);
        payload.CopyTo(buf, 8);
        using var deadline = ReadCts(default);
        await pipe.WriteAsync(buf, deadline.Token).ConfigureAwait(false);
    }

    /// <summary>Largest frame accepted. The length field is whatever the pipe peer claims — never allocate
    /// on it unchecked (0x7FFFFFF0 asks for a ~2 GB array, a negative value throws). Real Discord RPC
    /// frames are a few hundred bytes, so 1 MiB is orders of magnitude above anything legitimate.</summary>
    private const int MaxFrameBytes = 1024 * 1024;

    /// <summary>Ceiling on non-matching frames before we give up waiting for the one we asked for. Without
    /// it a peer that streams valid-but-irrelevant frames keeps the read loop and its pipe alive forever.</summary>
    private const int MaxFramesPerWait = 64;

    /// <summary>Default deadline for a request/response exchange. Linked to the overall operation
    /// deadline; AUTHORIZE may use its longer human-consent deadline within that overall bound.</summary>
    private static readonly TimeSpan ReadDeadline = TimeSpan.FromSeconds(15);

    private static async Task<(int opcode, string json)> ReadFrameAsync(
        NamedPipeClientStream pipe, CancellationToken ct = default)
    {
        var header = new byte[8];
        await pipe.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        int len = BitConverter.ToInt32(header, 4);
        if (len is < 0 or > MaxFrameBytes)
            throw new InvalidDataException($"Discord IPC frame length {len} is out of range");
        var body = new byte[len];
        await pipe.ReadExactlyAsync(body, ct).ConfigureAwait(false);
        return (BitConverter.ToInt32(header, 0), Encoding.UTF8.GetString(body));
    }

    /// <summary>A token source for one exchange. Callers that supply their own token keep their deadline —
    /// AUTHORIZE waits up to 60 s because a human has to click Approve in Discord, and capping that at
    /// ReadDeadline would break first-run setup. Only token-less calls get the default deadline.</summary>
    private static CancellationTokenSource ReadCts(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, OperationToken.Value);
        if (!ct.CanBeCanceled) cts.CancelAfter(ReadDeadline);
        return cts;
    }

    private static async Task<string> ReadUntilCmdAsync(
        NamedPipeClientStream pipe, string cmd, string nonce, CancellationToken ct = default)
    {
        using var cts = ReadCts(ct);
        for (int seen = 0; seen < MaxFramesPerWait; seen++)
        {
            var (_, json) = await ReadFrameAsync(pipe, cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("cmd", out var v) && v.GetString() == cmd
                && doc.RootElement.TryGetProperty("nonce", out var n) && n.GetString() == nonce)
                return json;
        }
        throw new InvalidDataException($"no '{cmd}' reply within {MaxFramesPerWait} frames");
    }

    private static async Task<string> ReadUntilEvtAsync(
        NamedPipeClientStream pipe, string evt, CancellationToken ct = default)
    {
        using var cts = ReadCts(ct);
        for (int seen = 0; seen < MaxFramesPerWait; seen++)
        {
            var (_, json) = await ReadFrameAsync(pipe, cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("evt", out var v) && v.GetString() == evt)
                return json;
        }
        throw new InvalidDataException($"no '{evt}' event within {MaxFramesPerWait} frames");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsError(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("evt", out var e) && e.GetString() == "ERROR";

    private static string? ParseChannelId(string discordUrl)
    {
        // discord://discord.com/channels/{guildId}/{channelId}. A malformed/scheme-less URL makes new Uri
        // throw UriFormatException — return null instead, which the caller treats as "can't parse".
        try
        {
            var segments = new Uri(discordUrl).AbsolutePath.Trim('/').Split('/');
            return segments.Length >= 3 ? segments[2] : null;
        }
        catch { return null; }
    }

    private static string NewNonce() => Guid.NewGuid().ToString("N")[..8];

    private static string Clip(string s, int max = 200) =>
        s.Length <= max ? s : s[..max] + "…";

    private static void WriteLog(string msg)
    {
        try { File.AppendAllText(LogFile, $"{DateTime.Now:HH:mm:ss.fff}: {msg}\n"); } catch { }
        Trace.WriteLine($"[Discord IPC] {msg}");
    }
}
