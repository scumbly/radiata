using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>Frozen game state on disk: <c>%APPDATA%\Radiata\arcade-state.json</c>.
///
/// <para>Its own file, never <c>config.json</c>. Config is user settings — hand-edited, backed up,
/// versioned. Game state churns on every dismiss and is worth nothing if lost, so mixing them would put
/// throwaway data in the backup rotation and a wave counter one bad write from the user's wheels.</para>
///
/// <para>Written on dismiss (and at app teardown) rather than on a timer: dismissing is the checkpoint, and
/// it's rate-limited by a human pressing ○. A hard kill mid-game loses that session's progress.</para>
///
/// <para>Every read path treats the file as hostile and every failure is swallowed: a missing, truncated,
/// hand-mangled or newer-version file must produce a fresh game, never a crash and never a prompt. Same
/// posture as <c>ConfigLoader</c>, and the same atomic write (unique temp + single <c>File.Move</c>) so a
/// crash or full disk can't leave a half-written file behind.</para></summary>
public static class ArcadeStore
{
    private const int FileVersion = 1;

    /// <summary><paramref name="Settings"/> is a game's own pause-menu choices, as a blob only that game
    /// understands — the same posture as <paramref name="State"/>.
    ///
    /// <para>⚠ Must stay separate from State: a finished run clears its snapshot (see <see cref="Save"/>), so
    /// a setting folded in there would be forgotten the moment the player lost.</para>
    ///
    /// <para><paramref name="HowToSeen"/> (the one-time △ hint's kill switch, per game) lives here for the
    /// same reason the high score does: it's a fact about the player, not the run, so it must outlive both a
    /// finished run and a Reset. A new game inherits the hint, and its disappearance, for free.</para>
    ///
    /// <para>⚠ Keep it a defaulted parameter so an <c>arcade-state.json</c> from an older build still
    /// deserializes — the missing field reads false and the hint shows once more. That is why the file
    /// version does not bump for additive fields; bumping it would make existing saved campaigns
    /// unreadable.</para>
    ///
    /// <para><paramref name="MusicOff"/> is the same kind of fact: a per-game preference that must outlive a
    /// finished run and a Reset. Stored as the off choice so the defaulted false reads as music on — the
    /// shipped default — for a fresh install and an older file alike. (A file that carried the retired
    /// <c>MusicOn</c> key reads as on too; the key is simply unknown to this shape.)</para></summary>
    private sealed record Slot(string? State, int HighScore, string? Settings = null, bool HowToSeen = false,
                               bool MusicOff = false);
    /// <summary><paramref name="Last"/> is the surface the arcade was dismissed on — a game id, or null for
    /// the cabinet picker. The Arcade Launcher resumes it, so closing and re-opening lands where you left.
    ///
    /// <para>⚠ Defaulted, like every field added since v1: a file from an older build deserializes with it
    /// null (the picker), which is exactly the old behaviour. That is why the version does not bump.</para>
    ///
    /// <para><paramref name="Cabinet"/> is the cabinet that was frontmost when the picker was dismissed, so
    /// the launcher's carousel re-opens on it rather than the first catalogue entry. Defaulted the same way.</para>
    ///
    /// <para><paramref name="Position"/> is where the round game window sits — 0 over the left wheel, 1 the
    /// screen centre, 2 over the right wheel — one choice for every game; −1 until the player has moved it,
    /// when the wheel that launched the game decides. Defaulted like the fields before it, so no version bump.</para>
    ///
    /// <para><paramref name="MusicOff"/> is the music choice, and it is one choice for the whole arcade —
    /// top level beside <paramref name="Last"/>, not a field on each game. Turning the bed off in any game
    /// turns it off in all of them.</para>
    ///
    /// <para>⚠ <see cref="Slot.MusicOff"/> is still read, for exactly one purpose: carrying a pre-global
    /// file's choice up to here the first time such a file is loaded. That load also scrubs every slot and
    /// rewrites the file, because <see cref="Write"/> serialises every cached slot as-is: a slot left holding
    /// off would be folded up again on the next load, over whatever the player chose since.</para></summary>
    private sealed record File1(int V, Dictionary<string, Slot> Games, string? Last = null, string? Cabinet = null,
                                int Position = -1, bool MusicOff = false);

    private static string Path_ => Path.Combine(AppPaths.AppDataDir, "arcade-state.json");

    /// <summary>Where the picker's screenshots live, one PNG per game. Beside the state file, never in
    /// config, for the same reason: churn on every dismiss and worth nothing if lost.</summary>
    public static string ShotsDir => Path.Combine(AppPaths.AppDataDir, "arcade-shots");

    /// <summary>The screenshot path for a game id. Ids are shipped tokens, but a drop-in package's id is
    /// user-authored, so it is sanitised into a file name rather than trusted.</summary>
    public static string ShotPath(string gameId)
    {
        var bad = Path.GetInvalidFileNameChars();
        var name = new string(gameId.Select(ch => Array.IndexOf(bad, ch) >= 0 ? '_' : ch).ToArray());
        if (name.Length == 0) name = "_";
        return Path.Combine(ShotsDir, name + ".png");
    }

    private static Dictionary<string, Slot>? _cache;
    private static string? _last;
    private static string? _cabinet;
    private static int _position = -1;
    private static bool _musicOff;
    private static bool _writeBlocked;

    public static bool CanSave { get { Load(); return !_writeBlocked; } }

    /// <summary>Harness-only: forget the loaded file so the next call re-reads it from disk. Nothing in the app
    /// calls this — the app loads once per process by design. <c>TestHarness.exe arcade</c> uses it to prove
    /// the legacy per-game music fold survives a write and a reload.</summary>
    public static void DropCacheForHarness()
    {
        _cache = null; _last = null; _cabinet = null; _position = -1; _musicOff = false; _writeBlocked = false;
    }

    private static Dictionary<string, Slot> Load()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(Path_))
            {
                var f = JsonSerializer.Deserialize<File1>(File.ReadAllText(Path_));
                // A future version's file is ignored, not migrated and not deleted — a user who downgrades
                // for one session shouldn't lose their progress to the older build.
                if (f is { V: FileVersion, Games: not null })
                {
                    _last = string.IsNullOrWhiteSpace(f.Last) ? null : f.Last;
                    _cabinet = string.IsNullOrWhiteSpace(f.Cabinet) ? null : f.Cabinet;
                    // Validated on read like the rest: anything but the three positions reads as "not yet moved".
                    _position = f.Position is >= 0 and <= 2 ? f.Position : -1;
                    // The dictionary's values are hostile too: `"kabloom": null` parses, and every reader
                    // dereferences the slot — one of them from the picker's per-frame draw.
                    var games = new Dictionary<string, Slot>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (id, slot) in f.Games)
                        if (slot is not null && !string.IsNullOrWhiteSpace(id)) games[id] = slot;
                    // The music choice went global. A file written before that carries it per game and
                    // nothing at the top level, so any game holding the off choice adopts it for all of
                    // them — the alternative is silently handing that player music back on. Folded once:
                    // the slots are scrubbed and the file rewritten now, because Write() serialises every
                    // cached slot and a stale off would otherwise be folded up again on every later load.
                    bool legacyOff = games.Values.Any(s => s.MusicOff);
                    _musicOff = f.MusicOff || legacyOff;
                    _cache = games;
                    if (legacyOff)
                    {
                        foreach (var id in games.Keys.ToList()) games[id] = games[id] with { MusicOff = false };
                        Write(games);
                    }
                    return games;
                }
                _writeBlocked = true;
                Trace.WriteLine($"[Arcade] unsupported state preserved; saving disabled (this build reads v{FileVersion})");
            }
        }
        catch (Exception ex)
        {
            _writeBlocked = true;
            Trace.WriteLine($"[Arcade] state file unreadable; preserved with saving disabled: {ex.Message}");
        }
        return _cache = new Dictionary<string, Slot>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The frozen snapshot for a game, or null for "start fresh".</summary>
    public static string? LoadState(string gameId) =>
        Load().TryGetValue(gameId, out var s) ? s.State : null;

    public static int LoadHighScore(string gameId) =>
        Load().TryGetValue(gameId, out var s) ? Math.Max(0, s.HighScore) : 0;

    /// <summary>A game's saved pause-menu settings, or null if it has never saved any.</summary>
    public static string? LoadSettings(string gameId) =>
        Load().TryGetValue(gameId, out var s) ? s.Settings : null;

    /// <summary>Has this game's how-to card been opened before? False shows the one-time △ hint.</summary>
    public static bool LoadHowToSeen(string gameId) =>
        Load().TryGetValue(gameId, out var s) && s.HowToSeen;

    /// <summary>Is the music bed switched on? On unless the player has turned it off — one answer for every
    /// game, so a game with no stored slot of its own still honours the choice.</summary>
    public static bool LoadMusicOn() { Load(); return !_musicOff; }

    /// <summary>Remember the music choice, written straight away like the how-to flag: a preference is worth
    /// more than a run, and a hard kill must not lose it. Applies to the whole arcade.</summary>
    public static void SaveMusicOn(bool on)
    {
        Load();
        if (_musicOff == !on) return;
        _musicOff = !on;
        Write(Load());
    }

    /// <summary>Remember that this game's how-to has been read, retiring its △ hint for good.
    ///
    /// <para>Written straight away rather than at the next dismiss, unlike a game's snapshot. The whole
    /// promise of the hint is that it goes away and stays away; a hard kill between reading the card and
    /// dismissing the game would otherwise bring it back, and "I already dealt with this" reappearing is
    /// exactly the thing a one-time hint must never do. Idempotent, so re-opening the card doesn't churn
    /// the file.</para></summary>
    public static void MarkHowToSeen(string gameId)
    {
        var games = Load();
        if (games.TryGetValue(gameId, out var s))
        {
            if (s.HowToSeen) return;
            games[gameId] = s with { HowToSeen = true };
        }
        else
        {
            // No slot yet — the card can be read before anything worth freezing has happened.
            games[gameId] = new Slot(null, 0, null, true);
        }
        Write(games);
    }

    /// <summary>The game the arcade was last dismissed on, or null when it was dismissed on the picker.
    /// What the Arcade Launcher resumes.</summary>
    public static string? LoadLastSurface() { Load(); return _last; }

    /// <summary>The cabinet that was frontmost when the arcade was last dismissed, or null when none has
    /// been recorded. Where the launcher's carousel lands when it resumes on the cabinets.</summary>
    public static string? LoadLastCabinet() { Load(); return _cabinet; }

    /// <summary>The window position the player last chose (0 left wheel, 1 centre, 2 right wheel), or −1 when
    /// they never have — the launching wheel then decides.</summary>
    public static int LoadWindowPosition() { Load(); return _position; }

    public static void SaveWindowPosition(int position)
    {
        var games = Load();
        int next = Math.Clamp(position, 0, 2);
        if (next == _position) return;
        _position = next;
        Write(games);
    }

    /// <summary>Remember what the arcade was showing as it closed. <paramref name="gameId"/> blank means the
    /// picker; <paramref name="cabinetId"/> is the cabinet in front at that moment (blank keeps the stored
    /// one). Idempotent, so closing on the same surface twice doesn't churn the file.</summary>
    public static void SaveLastSurface(string? gameId, string? cabinetId)
    {
        var games = Load();
        string? next = string.IsNullOrWhiteSpace(gameId) ? null : gameId;
        string? cab  = string.IsNullOrWhiteSpace(cabinetId) ? _cabinet : cabinetId;
        if (string.Equals(next, _last, StringComparison.OrdinalIgnoreCase)
            && string.Equals(cab, _cabinet, StringComparison.OrdinalIgnoreCase)) return;
        _last = next;
        _cabinet = cab;
        Write(games);
    }

    /// <summary>Freeze a game. <paramref name="state"/> null clears the slot's snapshot while keeping the
    /// high score and the settings — which is what a finished run wants.</summary>
    public static void Save(string gameId, string? state, int highScore, string? settings)
    {
        var games = Load();
        // Settings fall back to whatever was already stored, so a caller that has none to offer can never
        // wipe them — the clear-on-finish path goes through here too. HowToSeen is carried the same way and
        // has no parameter at all: nothing but MarkHowToSeen may ever set it, so a routine save can't
        // resurrect a hint the player has already dismissed.
        bool seen = false;
        string? keep = settings;
        if (games.TryGetValue(gameId, out var old))
        {
            keep ??= old.Settings;
            seen = old.HowToSeen;
        }
        // ⚠ The slot's MusicOff is left at its default and never carried: the choice is global now (see
        // File1.MusicOff), and writing a stale per-game copy back is how the two would start disagreeing.
        games[gameId] = new Slot(state, Math.Max(0, highScore), keep, seen);
        Write(games);
    }

    private static void Write(Dictionary<string, Slot> games)
    {
        if (_writeBlocked) return;
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDir);
            var json = JsonSerializer.Serialize(new File1(FileVersion, games, _last, _cabinet, _position, _musicOff),
                new JsonSerializerOptions { WriteIndented = false });
            AtomicFile.WriteAllText(Path_, json);
        }
        catch (Exception ex)
        {
            // Losing a saved wave is a shrug; taking the app down over it is not.
            Trace.WriteLine($"[Arcade] state save failed: {ex.Message}");
        }
    }
}
