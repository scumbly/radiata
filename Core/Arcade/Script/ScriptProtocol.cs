using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControllerWheel;

/// <summary>
/// Wire protocol between Radiata and the jailed <c>Radiata.ArcadeHost.exe</c>: 4-byte little-endian
/// length prefix + UTF-8 JSON, one message per exchange, over a named pipe whose ACL names exactly
/// the derived AppContainer SID + the current user.
///
/// <para>⚠ Compiled as SOURCE into Radiata.ArcadeHost.exe, which has no Core.dll: nothing in this file may
/// reference another Core type (Loc, HelpLocalization, UiStrings, …). The app's own build would still pass —
/// ReferenceOutputAssembly is false — and script games would simply stop working at runtime.</para>
///
/// <para>The length prefix is the first validation point — an out-of-range value is a protocol
/// violation, never an allocation. The helper is untrusted end to end: everything it returns goes
/// through <see cref="ScriptDrawValidator"/> before a pixel is drawn.</para>
///
/// <para>⚠ The script text travels over the pipe in the load message, never as a path — the
/// AppContainer'd helper couldn't read a user-profile path anyway, and handing it one would invite
/// someone to widen the container instead.</para>
/// </summary>
public static class ScriptProtocol
{
    /// <summary>Mirrors <see cref="ArcadeTuning.StepSeconds"/> — the helper steps at the same fixed
    /// 120 Hz as the built-in games.</summary>
    public const double StepSeconds = 1.0 / 120.0;
    /// <summary>Mirrors <see cref="ArcadeTuning.MaxStepsPerFrame"/> — a longer backlog is dropped,
    /// not simulated.</summary>
    public const int MaxStepsPerFrame = 12;

    // Bound by what a manifest-legal script can become on the wire: ScriptGameManifest.MaxEntryFileBytes of
    // source, whose worst-case JSON escaping is ~6x, plus the envelope. Below that a game the manifest accepts
    // can fail to LOAD, and the helper reports the truncated read rather than the size.
    public const int MaxRequestBytes = 2 * 1024 * 1024;   // load carries the whole script text
    public const int MaxResponseBytes = 1024 * 1024;  // hard response cap (host-enforced); fits MaxCommands of the heaviest poly
    public const int MaxCommands = 1024;              // hard draw-command cap (host-enforced)
    public const int MaxTextLength = 64;              // any string in a draw command
    public const int MaxPolygonPoints = 16;
    public const int KvTotalCapBytes = 4096;          // per game, keys+values, UTF-8
    public const int MaxCuesPerFrame = 8;
    public const int MaxKvWritesPerFrame = 16;

    public static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static async Task WriteMessageAsync(Stream s, object msg, CancellationToken ct)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(msg, msg.GetType(), Json);
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await s.WriteAsync(prefix, ct).ConfigureAwait(false);
        await s.WriteAsync(payload, ct).ConfigureAwait(false);
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Reads one framed message. Throws <see cref="ScriptProtocolViolationException"/> on a
    /// bad prefix or a truncated stream; the caller treats that as a fault of the peer.</summary>
    public static async Task<byte[]> ReadMessageAsync(Stream s, int maxBytes, CancellationToken ct)
    {
        byte[] prefix = await ReadExactAsync(s, 4, ct).ConfigureAwait(false);
        int len = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (len <= 0 || len > maxBytes)
            throw new ScriptProtocolViolationException($"length prefix {len} outside (0, {maxBytes}]");
        return await ReadExactAsync(s, len, ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(Stream s, int count, CancellationToken ct)
    {
        byte[] buf = new byte[count];
        int off = 0;
        while (off < count)
        {
            int n = await s.ReadAsync(buf.AsMemory(off, count - off), ct).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException("pipe closed mid-message");
            off += n;
        }
        return buf;
    }
}

public sealed class ScriptProtocolViolationException(string message) : Exception(message);

// ── Messages ─────────────────────────────────────────────────────────────────

/// <summary>Controller state one rendered frame carries to the script — packed as a bit field so
/// the wire stays one integer. The script reads it through named helpers the helper binds
/// (btnCross(), dpadUp(), …), so the packing is an implementation detail on both ends.</summary>
public sealed class ScriptInput
{
    public double stickX { get; set; }
    public double stickY { get; set; }
    /// <summary>Bits: 1 ✕ held, 2 □ held, 4 ✕ pressed-edge, 8 □ pressed-edge, then d-pad EDGES
    /// (never levels — same contract as <see cref="ArcadeInput.DPadPressed"/>): 16 up, 32 right,
    /// 64 down, 128 left.</summary>
    public int buttons { get; set; }

    public const int CrossDown = 1, SquareDown = 2, CrossPressed = 4, SquarePressed = 8,
                     DUp = 16, DRight = 32, DDown = 64, DLeft = 128;
}

/// <summary>Host → helper. <c>type</c> "load" carries the script + seed + kv snapshot once per
/// session; "frame" is the per-rendered-frame request.</summary>
public sealed class ScriptRequest
{
    public string type { get; set; } = "frame";     // "load" | "frame"
    public string? script { get; set; }             // load only — script TEXT, never a path
    public ulong seed { get; set; }                 // load only — deterministic RNG seed
    public Dictionary<string, string>? kv { get; set; }   // load only — persisted kv snapshot
    public long frameId { get; set; }
    public double elapsedMs { get; set; }
    public ScriptInput? input { get; set; }
}

/// <summary>One polar draw command. Field roles per <c>t</c> (angles in degrees, screen convention:
/// 0 = 12 o'clock, increasing clockwise; radii 0..1 of the playfield):
/// <list type="bullet">
/// <item><c>arc</c> — annulus segment: r=inner radius, r2=outer radius, a=start angle, a2=end angle.</item>
/// <item><c>dot</c> — filled circle: r, a = position; w = radius (0..0.5).</item>
/// <item><c>line</c> — r,a → r2,a2; w = width (0..0.1).</item>
/// <item><c>poly</c> — filled polygon: p = flat [r,θ, r,θ, …], 3..16 points.</item>
/// <item><c>text</c> — s at r,a; w = em size (0..0.3 of the field).</item>
/// </list>
/// <c>c</c> is the fill, 0xAARRGGBB (alpha first — the app-wide convention). <c>sc</c>/<c>sw</c>
/// are an optional stroke, honoured by <c>arc</c>, <c>dot</c> and <c>poly</c>; <c>line</c> ignores
/// them (a line is already a stroke — its colour and width are <c>c</c> and <c>w</c>). The host does all trig, the circular clip and the safe area — a script
/// can neither escape the disc nor know where it is on screen.</summary>
public sealed class ScriptDrawCommand
{
    public string? t { get; set; }
    public double r { get; set; }
    public double r2 { get; set; }
    public double a { get; set; }
    public double a2 { get; set; }
    public double w { get; set; }
    public double[]? p { get; set; }
    public string? s { get; set; }
    public uint c { get; set; }
    public uint? sc { get; set; }
    public double sw { get; set; }
}

/// <summary>Helper → host. "ready" answers a load; "draw" answers a frame and carries the command
/// buffer plus any kv writes and cue names the steps produced; "fault" reports a script error
/// (the session survives a fault — the host keeps the last good buffer up).</summary>
public sealed class ScriptResponse
{
    public string type { get; set; } = "draw";      // "ready" | "draw" | "fault"
    public long frameId { get; set; }
    public List<ScriptDrawCommand>? commands { get; set; }
    public Dictionary<string, string>? kvWrites { get; set; }
    public List<string>? cues { get; set; }
    public string? message { get; set; }            // fault only
}

/// <summary>Host-side validation of everything the helper returns — the helper is untrusted, and a
/// value that fails here is a protocol violation (kill + restart), not a clamp. Clamping would let
/// a compromised helper feed garbage forever at zero cost.</summary>
public static class ScriptDrawValidator
{
    /// <summary>Returns null when valid, else the reason (which the session treats as a violation).</summary>
    public static string? Validate(ScriptResponse resp)
    {
        var commands = resp.commands;
        if (commands is null) return "draw response with no commands";
        if (commands.Count > ScriptProtocol.MaxCommands)
            return $"command flood: {commands.Count} > {ScriptProtocol.MaxCommands}";
        foreach (var c in commands)
        {
            string? bad = ValidateCommand(c);
            if (bad is not null) return bad;
        }
        if (resp.cues is { } cues)
        {
            if (cues.Count > ScriptProtocol.MaxCuesPerFrame)
                return $"cue flood: {cues.Count} > {ScriptProtocol.MaxCuesPerFrame}";
            foreach (var cue in cues)
                if (cue is null || cue.Length > 32) return "cue name null or over 32 chars";
        }
        if (resp.kvWrites is { } kv)
        {
            if (kv.Count > ScriptProtocol.MaxKvWritesPerFrame)
                return $"kv write flood: {kv.Count} > {ScriptProtocol.MaxKvWritesPerFrame}";
            foreach (var (k, v) in kv)
                if (string.IsNullOrEmpty(k) || k.Length > 64 || v is null || v.Length > 1024)
                    return "kv write key/value out of bounds";
        }
        return null;
    }

    private static string? ValidateCommand(ScriptDrawCommand c)
    {
        switch (c.t)
        {
            case "arc":
                if (!Finite(c.r, c.r2, c.a, c.a2)) return "arc: non-finite value";
                if (c.r is < 0 or > 1 || c.r2 is < 0 or > 1) return "arc: radius out of [0,1]";
                if (c.r2 < c.r) return "arc: outer radius under inner";
                if (Angle(c.a) || Angle(c.a2)) return "arc: angle out of range";
                break;
            case "dot":
                if (!Finite(c.r, c.a, c.w)) return "dot: non-finite value";
                if (c.r is < 0 or > 1) return "dot: radius out of [0,1]";
                if (Angle(c.a)) return "dot: angle out of range";
                if (c.w is <= 0 or > 0.5) return "dot: size out of (0,0.5]";
                break;
            case "line":
                if (!Finite(c.r, c.a, c.r2, c.a2, c.w)) return "line: non-finite value";
                if (c.r is < 0 or > 1 || c.r2 is < 0 or > 1) return "line: radius out of [0,1]";
                if (Angle(c.a) || Angle(c.a2)) return "line: angle out of range";
                if (c.w is <= 0 or > 0.1) return "line: width out of (0,0.1]";
                break;
            case "poly":
                if (c.p is null || c.p.Length < 6 || c.p.Length > ScriptProtocol.MaxPolygonPoints * 2
                    || c.p.Length % 2 != 0)
                    return $"poly: needs 3..{ScriptProtocol.MaxPolygonPoints} r,θ pairs";
                for (int i = 0; i < c.p.Length; i += 2)
                {
                    if (!double.IsFinite(c.p[i]) || !double.IsFinite(c.p[i + 1])) return "poly: non-finite point";
                    if (c.p[i] is < 0 or > 1) return "poly: radius out of [0,1]";
                    if (Angle(c.p[i + 1])) return "poly: angle out of range";
                }
                break;
            case "text":
                if (!Finite(c.r, c.a, c.w)) return "text: non-finite value";
                if (c.r is < 0 or > 1) return "text: radius out of [0,1]";
                if (Angle(c.a)) return "text: angle out of range";
                if (c.w is <= 0 or > 0.3) return "text: size out of (0,0.3]";
                if (c.s is null || c.s.Length > ScriptProtocol.MaxTextLength)
                    return $"text: length {(c.s?.Length ?? -1)} > {ScriptProtocol.MaxTextLength}";
                // Drawn per frame with no other sanitizer in the path: no control characters, and no Format
                // characters (the bidi controls), which could reorder the text once RTL is honoured.
                foreach (var ch in c.s)
                    if (char.IsControl(ch) || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.Format)
                        return "text: control or format character";
                break;
            default:
                return $"unknown command type '{c.t}'";
        }
        if (c.sc is not null && (!double.IsFinite(c.sw) || c.sw is < 0 or > 0.1))
            return "stroke width out of [0,0.1]";
        return null;
    }

    private static bool Angle(double deg) => !double.IsFinite(deg) || Math.Abs(deg) > 3600;
    // Fixed-arity overloads: `params double[]` allocated an array plus a LINQ enumerator per draw command,
    // ~61k of each per second at the command cap, on the UI thread.
    private static bool Finite(double a) => double.IsFinite(a);
    private static bool Finite(double a, double b, double c) => double.IsFinite(a) && double.IsFinite(b) && double.IsFinite(c);
    private static bool Finite(double a, double b, double c, double d)
        => double.IsFinite(a) && double.IsFinite(b) && double.IsFinite(c) && double.IsFinite(d);
    private static bool Finite(double a, double b, double c, double d, double e)
        => double.IsFinite(a) && double.IsFinite(b) && double.IsFinite(c) && double.IsFinite(d) && double.IsFinite(e);
}
