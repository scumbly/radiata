using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// The <see cref="IArcadeGame"/> adapter for a consented drop-in script game. The sim lives in the
/// jailed helper process; this object is the host-side stand-in that slots the game into the
/// existing catalog / pump / freeze machinery:
///
/// <list type="bullet">
/// <item><see cref="Step"/> never simulates — it aggregates the frame's input (edges OR'd, levels
/// latest) and counts fixed steps, which the shell-side session drains once per rendered frame
/// into one pipe request.</item>
/// <item><see cref="Serialize"/>/<see cref="Restore"/> round-trip the game's KV store — the only
/// state that survives a dismiss. Scripts are stateless across dismiss otherwise, by design: the
/// helper dies with the arcade and a fresh session replays load + kv restore.</item>
/// <item>Cues arrive from the helper (validated) via <see cref="EnqueueCues"/> and drain through
/// <see cref="TakeCues"/> once per rendered frame, like every other game.</item>
/// </list>
///
/// Construction is cheap and headless — no process, no pipe. The session is owned by the WPF
/// shell (`Arcade\ScriptHost\`) and launched lazily on the first rendered frame, so the harness
/// can instantiate catalog entries without spawning helpers.
/// </summary>
public sealed class ScriptArcadeGame : IArcadeGame
{
    public ScriptGameManifest Manifest { get; }

    public ScriptArcadeGame(ScriptGameManifest manifest) => Manifest = manifest;

    public string Id => Manifest.Token;
    public string Title => Manifest.Title;

    /// <summary>M1: script games never end themselves — ○ freezes the kv store and leaves.</summary>
    public bool IsGameOver => false;
    public void Restart() { }

    // ── Input aggregation (fixed steps → one frame request) ─────────────────

    private int _pendingSteps;
    private int _pendingEdges;
    private float _stickX, _stickY;
    private bool _crossDown, _squareDown;

    public void Step(in ArcadeInput input, double dt)
    {
        _pendingSteps++;
        _stickX = input.StickX; _stickY = input.StickY;
        _crossDown = input.CrossDown; _squareDown = input.SquareDown;
        if (input.CrossPressed) _pendingEdges |= ScriptInput.CrossPressed;
        if (input.SquarePressed) _pendingEdges |= ScriptInput.SquarePressed;
        _pendingEdges |= (input.DPadPressed & 0xF) << 4;   // DPad bits 1..8 → wire bits 16..128
    }

    /// <summary>Drained by the session once per rendered frame. Returns the elapsed sim time the
    /// helper should cover and the packed input, then clears the aggregate. Zero steps (guard up,
    /// ready beat, pause) returns 0 elapsed — the session then skips the request entirely, so a
    /// paused script genuinely stops.</summary>
    public (double ElapsedMs, ScriptInput Input) TakePendingFrame()
    {
        double elapsed = _pendingSteps * ScriptProtocol.StepSeconds * 1000.0;
        int buttons = _pendingEdges
            | (_crossDown ? ScriptInput.CrossDown : 0)
            | (_squareDown ? ScriptInput.SquareDown : 0);
        _pendingSteps = 0; _pendingEdges = 0;
        return (elapsed, new ScriptInput { stickX = _stickX, stickY = _stickY, buttons = buttons });
    }

    // ── KV store (the persistence contract) ──────────────────────────────────

    private readonly Dictionary<string, string> _kv = new(StringComparer.Ordinal);

    /// <summary>Host-side mirror of the helper's store, updated from each frame's validated
    /// kvWrites. The mirror is what persists — the helper's copy dies with it.</summary>
    public IReadOnlyDictionary<string, string> Kv => _kv;

    /// <summary>The helper is untrusted, so the 4 KB store cap is enforced here as well as inside it — the
    /// mirror is what persists, and a helper that lied about its own cap would otherwise grow host memory and
    /// the state file without bound. A write that would cross the cap is dropped; the store is never cleared.</summary>
    public void ApplyKvWrites(IReadOnlyDictionary<string, string>? writes)
    {
        if (writes is null) return;
        foreach (var (k, v) in writes)
        {
            if (k is null || v is null || k.Length > 64 || v.Length > 1024) continue;
            int delta = Bytes(k) + Bytes(v) - (_kv.TryGetValue(k, out var old) ? Bytes(k) + Bytes(old) : 0);
            if (_kvBytes + delta > ScriptProtocol.KvTotalCapBytes) continue;
            _kv[k] = v;
            _kvBytes += delta;
        }
    }

    private int _kvBytes;
    private static int Bytes(string s) => System.Text.Encoding.UTF8.GetByteCount(s);

    public string Serialize() => JsonSerializer.Serialize(_kv);

    public void Restore(string json)
    {
        // Hostile input, like every arcade read path: anything unparseable → fresh store.
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed is null) return;
            _kv.Clear();
            _kvBytes = 0;
            foreach (var (k, v) in parsed)
            {
                if (k is null || v is null || k.Length > 64 || v.Length > 1024) continue;
                int add = Bytes(k) + Bytes(v);
                if (_kvBytes + add > ScriptProtocol.KvTotalCapBytes) break;
                _kv[k] = v;
                _kvBytes += add;
            }
        }
        catch { /* fresh store */ }
    }

    /// <summary>Scripts report a best by writing the reserved kv key <c>hiscore</c> (an integer as
    /// text); it rides the same store and lands in the picker like any built-in's best.</summary>
    public int HighScore
        => _kv.TryGetValue("hiscore", out var s) && int.TryParse(s, out int v) ? Math.Max(0, v) : 0;

    public void SeedHighScore(int high)
    {
        if (high > HighScore) _kv["hiscore"] = high.ToString();
    }

    // ── Cues (helper → Sfx, drained once per rendered frame) ────────────────

    private readonly List<string> _cues = [];

    public void EnqueueCues(IReadOnlyList<string>? cues)
    {
        if (cues is null) return;
        foreach (var c in cues)
            if (_cues.Count < ScriptProtocol.MaxCuesPerFrame) _cues.Add(c);
    }

    public IReadOnlyList<string> TakeCues()
    {
        if (_cues.Count == 0) return [];
        var taken = _cues.ToArray();
        _cues.Clear();
        return taken;
    }

    // ── How-to card ──────────────────────────────────────────────────────────

    /// <summary>Manifest lines become the △ card; no lines → null, and per the arcade's no-offer
    /// contract neither the △ hint nor the pause-menu row appears. Art keys stay empty — M1 script
    /// games get text-only bullets.</summary>
    public ArcadeHowTo? HowTo
        => Manifest.HowTo.Count == 0
            ? null
            : new ArcadeHowTo(Manifest.Title.ToUpperInvariant(),
                [.. Manifest.HowTo.Select(l => new ArcadeHowToLine(l))]);
}
