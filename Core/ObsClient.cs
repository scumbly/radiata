using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ControllerWheel;

/// <summary>
/// Minimal obs-websocket v5 client (BCL only — <see cref="ClientWebSocket"/> + System.Text.Json +
/// System.Security.Cryptography), fire-and-forget from the caller's point of view: <see cref="Send"/>
/// queues a request onto a background worker and returns immediately, never throwing. Lives in
/// <c>Core</c> (no WPF references) so it stays portable; the WPF host owns the <see cref="Configure"/>
/// call (port/password from <see cref="SystemConfig"/>) and binds <see cref="Send"/> into
/// <see cref="ActionExecutor"/>'s <c>obsSend</c> delegate.
/// </summary>
public sealed class ObsClient
{
    private const int ConnectTimeoutMs = 3000;

    private readonly object _configLock = new();
    private int _port = 4455;
    private string? _password;

    // Must stay bounded: a dead/slow OBS blocks the worker ~3–6 s per item, so an unbounded queue grows
    // for as long as the user keeps firing wheel actions. Overflow policy: drop-newest, with a trace — 8
    // pending toggles against an unreachable server are already meaningless, and toggles are not
    // idempotent, so coalescing/deduping would change what the user's presses mean.
    private readonly BlockingCollection<ActionConfig> _queue = new(boundedCapacity: 8);
    private readonly object _connLock = new();
    private ClientWebSocket? _socket;
    private Thread? _worker;
    private volatile bool _started;

    /// <summary>Store the server port/password. If either changed, the current connection (if any) is
    /// dropped so the next <see cref="Send"/> reconnects with the new settings. Thread-safe.</summary>
    public void Configure(int port, string? password)
    {
        lock (_configLock)
        {
            bool changed = port != _port || !string.Equals(password, _password, StringComparison.Ordinal);
            _port = port;
            _password = password;
            if (changed) DropSocket();
        }
    }

    /// <summary>Queue <paramref name="action"/> for delivery to OBS. Fire-and-forget: never throws;
    /// failures are traced as <c>[Obs] …</c>. No-op if <paramref name="action"/> has no recognized
    /// <see cref="ActionConfig.Command"/>.</summary>
    public void Send(ActionConfig action)
    {
        if (action is null) return;
        try
        {
            EnsureWorker();
            if (!_queue.TryAdd(action))
                Trace.WriteLine($"[Obs] queue full ({_queue.BoundedCapacity} pending) — dropped '{action.Command}'");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Obs] queue failed: {ex.Message}");
        }
    }

    private void EnsureWorker()
    {
        if (_started) return;
        lock (_connLock)
        {
            if (_started) return;
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "ObsClientWorker" };
            _worker.Start();
            _started = true;
        }
    }

    private void WorkerLoop()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try
            {
                DeliverAsync(action).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Obs] delivery failed: {ex.Message}");
            }
        }
    }

    private async Task DeliverAsync(ActionConfig action)
    {
        if (!TryMapRequest(action, out var requestType, out var requestData))
        {
            Trace.WriteLine($"[Obs] unrecognized command: {action.Command}");
            return;
        }

        var socket = await GetConnectedSocketAsync();
        if (socket is null)
        {
            Trace.WriteLine("[Obs] no connection; request dropped");
            return;
        }

        if (!await TrySendRequestAsync(socket, requestType, requestData))
        {
            // One reconnect attempt, then give up for this request.
            DropSocket();
            var retrySocket = await GetConnectedSocketAsync();
            if (retrySocket is null || !await TrySendRequestAsync(retrySocket, requestType, requestData))
                Trace.WriteLine($"[Obs] send failed for {requestType} (after reconnect attempt)");
        }
    }

    private static bool TryMapRequest(ActionConfig action, out string requestType, out Dictionary<string, object?>? requestData)
    {
        requestData = null;
        switch (action.Command?.Trim().ToLowerInvariant())
        {
            case "obs-toggle-stream":
                requestType = "ToggleStream";
                return true;
            case "obs-toggle-record":
                requestType = "ToggleRecord";
                return true;
            case "obs-save-replay":
                requestType = "SaveReplayBuffer";
                return true;
            case "obs-set-scene":
                requestType = "SetCurrentProgramScene";
                requestData = new Dictionary<string, object?> { ["sceneName"] = action.ObsTarget };
                return true;
            case "obs-toggle-mic":
                requestType = "ToggleInputMute";
                requestData = new Dictionary<string, object?>
                {
                    ["inputName"] = string.IsNullOrWhiteSpace(action.ObsTarget) ? "Mic/Aux" : action.ObsTarget,
                };
                return true;
            default:
                requestType = "";
                return false;
        }
    }

    /// <summary>Live On/Off state of a stream/record toggle, for the armed hub preview: a
    /// one-shot connect + identify + GetStreamStatus/GetRecordStatus, then disconnect — the singleton
    /// delivery socket stays fire-and-forget and untouched. Null when OBS is unreachable, the command
    /// isn't a queryable toggle, or the response didn't arrive in time (the preview just stays plain).</summary>
    public async Task<bool?> QueryToggleStateAsync(ActionConfig action)
    {
        string? requestType = action?.Command?.ToLowerInvariant() switch
        {
            "obs-toggle-stream" => "GetStreamStatus",
            "obs-toggle-record" => "GetRecordStatus",
            _                   => null,
        };
        if (requestType is null) return null;

        int port; string? password;
        lock (_configLock) { port = _port; password = _password; }
        try
        {
            using var socket = new ClientWebSocket();
            using var cts = new CancellationTokenSource(ConnectTimeoutMs);
            await socket.ConnectAsync(new Uri($"ws://localhost:{port}"), cts.Token).ConfigureAwait(false);
            if (!await IdentifyAsync(socket, password, cts.Token).ConfigureAwait(false)) return null;

            string id = Guid.NewGuid().ToString("N");
            await SendJsonAsync(socket, new Dictionary<string, object?>
            {
                ["op"] = 6,
                ["d"]  = new Dictionary<string, object?> { ["requestType"] = requestType, ["requestId"] = id },
            }, cts.Token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++)   // skip Events (op 5) until our response or a small cap
            {
                using var msg = await ReceiveJsonAsync(socket, cts.Token).ConfigureAwait(false);
                if (msg is null) return null;
                if (GetOp(msg) != 7) continue;
                var d = msg.RootElement.GetProperty("d");
                if (d.GetProperty("requestId").GetString() != id) continue;
                return d.TryGetProperty("responseData", out var rd)
                       && rd.TryGetProperty("outputActive", out var oa)
                    ? oa.GetBoolean()
                    : null;
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Obs] status query failed: {ex.Message}"); }
        return null;
    }

    /// <summary>One-shot connection test for the slice editor's Test button: connect + full v5
    /// identify handshake, then disconnect. Returns null on success, else a short human-readable
    /// reason. Independent of the singleton's socket — a test never disturbs live delivery.</summary>
    public static async Task<string?> TestAsync(int port, string? password)
    {
        try
        {
            using var socket = new ClientWebSocket();
            using var cts = new CancellationTokenSource(ConnectTimeoutMs);
            await socket.ConnectAsync(new Uri($"ws://localhost:{port}"), cts.Token).ConfigureAwait(false);
            return await IdentifyAsync(socket, password, cts.Token).ConfigureAwait(false)
                ? null
                : Loc.T(UiText.Wizards.ObsRefusedPassword);
        }
        catch (OperationCanceledException) { return Loc.T(UiText.Wizards.ObsNoResponse); }
        catch (Exception ex) { return Loc.F(UiText.Wizards.ObsConnectFailed, ex.Message); }
    }

    // ── Connection lifecycle ─────────────────────────────────────────────────

    private async Task<ClientWebSocket?> GetConnectedSocketAsync()
    {
        ClientWebSocket? existing;
        lock (_connLock) existing = _socket;
        if (existing is { State: WebSocketState.Open }) return existing;

        int port;
        string? password;
        lock (_configLock) { port = _port; password = _password; }

        try
        {
            var socket = new ClientWebSocket();
            using var cts = new CancellationTokenSource(ConnectTimeoutMs);
            await socket.ConnectAsync(new Uri($"ws://localhost:{port}"), cts.Token);

            if (!await IdentifyAsync(socket, password, cts.Token))
            {
                SafeAbort(socket);
                return null;
            }

            lock (_connLock) _socket = socket;
            return socket;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Obs] connect failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Hello (op 0) → Identify (op 1) → Identified (op 2) handshake, per obs-websocket 5.x.</summary>
    private static async Task<bool> IdentifyAsync(ClientWebSocket socket, string? password, CancellationToken ct)
    {
        var hello = await ReceiveJsonAsync(socket, ct);
        if (hello is null || GetOp(hello) != 0) return false;

        string? auth = null;
        if (hello.RootElement.TryGetProperty("d", out var helloD) &&
            helloD.TryGetProperty("authentication", out var authEl) &&
            authEl.ValueKind == JsonValueKind.Object)
        {
            string challenge = authEl.GetProperty("challenge").GetString() ?? "";
            string salt      = authEl.GetProperty("salt").GetString() ?? "";
            if (string.IsNullOrEmpty(password))
            {
                Trace.WriteLine("[Obs] server requires a password but none is configured");
                return false;
            }
            auth = ComputeAuthString(password, salt, challenge);
        }

        var identifyD = new Dictionary<string, object?> { ["rpcVersion"] = 1 };
        if (auth is not null) identifyD["authentication"] = auth;

        await SendJsonAsync(socket, new Dictionary<string, object?> { ["op"] = 1, ["d"] = identifyD }, ct);

        var identified = await ReceiveJsonAsync(socket, ct);
        return identified is not null && GetOp(identified) == 2;
    }

    /// <summary>authString = Base64( SHA256( Base64(SHA256(password + salt)) + challenge ) ), both SHA256
    /// hashes taken over UTF-8 bytes and the intermediate result concatenated as a string.</summary>
    private static string ComputeAuthString(string password, string salt, string challenge)
    {
        string secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    private async Task<bool> TrySendRequestAsync(ClientWebSocket socket, string requestType, Dictionary<string, object?>? requestData)
    {
        if (socket.State != WebSocketState.Open) return false;
        try
        {
            var d = new Dictionary<string, object?>
            {
                ["requestType"] = requestType,
                ["requestId"]   = Guid.NewGuid().ToString("N"),
            };
            if (requestData is not null) d["requestData"] = requestData;

            using var cts = new CancellationTokenSource(ConnectTimeoutMs);
            await SendJsonAsync(socket, new Dictionary<string, object?> { ["op"] = 6, ["d"] = d }, cts.Token);

            // Fire-and-forget overall, but do a best-effort read of the next message so a same-connection
            // RequestResponse failure gets traced; Events (op 5) are ignored and just logged past.
            await DrainResponseAsync(socket, cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Obs] send exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>Reads at most one incoming frame looking for the matching RequestResponse (op 7); ignores
    /// Events (op 5). Best-effort only — a missed/late response is not treated as failure since this
    /// client is fire-and-forget by design.</summary>
    private static async Task DrainResponseAsync(ClientWebSocket socket, CancellationToken ct)
    {
        try
        {
            var msg = await ReceiveJsonAsync(socket, ct);
            if (msg is null) return;
            if (GetOp(msg) == 7 &&
                msg.RootElement.TryGetProperty("d", out var d) &&
                d.TryGetProperty("requestStatus", out var status) &&
                status.TryGetProperty("result", out var result) &&
                result.ValueKind == JsonValueKind.False)
            {
                string comment = status.TryGetProperty("comment", out var c) ? c.GetString() ?? "" : "";
                Trace.WriteLine($"[Obs] request failed: {comment}");
            }
        }
        catch
        {
            // Best-effort read only; ignore timeouts/closures here — TrySendRequestAsync already sent.
        }
    }

    private static int GetOp(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("op", out var op) ? op.GetInt32() : -1;

    private static async Task SendJsonAsync(ClientWebSocket socket, Dictionary<string, object?> payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    /// <summary>Ceiling on one assembled websocket message. obs-websocket replies are small, and whatever
    /// is listening on the configured port isn't necessarily OBS — without this the only cap is the
    /// connect timeout, which over loopback is hundreds of MB into a MemoryStream.</summary>
    private const int MaxMessageBytes = 4 * 1024 * 1024;

    private static async Task<JsonDocument?> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (ms.Length + result.Count > MaxMessageBytes)
                throw new InvalidDataException($"OBS message exceeded {MaxMessageBytes / 1024 / 1024} MB");
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        ms.Position = 0;
        return ms.Length == 0 ? null : JsonDocument.Parse(ms);
    }

    private void DropSocket()
    {
        ClientWebSocket? old;
        lock (_connLock)
        {
            old = _socket;
            _socket = null;
        }
        if (old is not null) SafeAbort(old);
    }

    private static void SafeAbort(ClientWebSocket socket)
    {
        try { socket.Abort(); } catch { /* best-effort */ }
        try { socket.Dispose(); } catch { /* best-effort */ }
    }
}
