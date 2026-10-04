using System.Diagnostics;

namespace ControllerWheel;

/// <summary>Background sweep that warms the Game Grid's Select/Start cycle caches for a whole library (see
/// GameArt.PrefetchGameArtAsync). Kicked from onboarding (heavily rate-limited) and from every Game Grid
/// load. One sweep runs at a time; games already swept this session or holding fresh disk caches are
/// skipped without delay, so re-kicks over a warmed library are free.</summary>
internal static class ArtPrefetcher
{
    private static readonly object _lock = new();
    private static readonly HashSet<string> _done = new(StringComparer.OrdinalIgnoreCase);   // MatchKeys swept this session
    private static bool _running;

    /// <summary>Queue a sweep over <paramref name="games"/>. No-op for games already swept/warm; a call
    /// made while a sweep runs is dropped (the next grid open re-kicks, so nothing is lost).
    /// <paramref name="perGameDelay"/> throttles between games that needed NETWORK work.</summary>
    public static void Kick(IReadOnlyList<InstalledGame> games, TimeSpan perGameDelay)
    {
        List<InstalledGame> todo;
        lock (_lock)
        {
            if (_running) return;
            todo = games.Where(g => !_done.Contains(GameLibrary.MatchKey(g))).ToList();
            if (todo.Count == 0) return;
            _running = true;
        }
        _ = Task.Run(() => RunAsync(todo, perGameDelay));
    }

    /// <summary>Forget which games were swept this session so the next <see cref="Kick"/> re-warms the
    /// whole library. MUST be called whenever the on-disk caches the "done" record vouches for are
    /// dropped (GameArt.ClearCache / RetryMissedArt) or cleared games stay filtered out until
    /// restart.</summary>
    public static void Reset()
    {
        lock (_lock) _done.Clear();
    }

    private static async Task RunAsync(List<InstalledGame> todo, TimeSpan perGameDelay)
    {
        int warmed = 0;
        try
        {
            // One-time riders: both need a non-UI thread and no network, and after the first run cost
            // one File.Exists.
            GameArt.NormalizeOversizedCacheOnce();
            GameArt.PurgePoisonedLogoDataOnce();

            foreach (var game in todo)
            {
                bool wasWarm = GameArt.HasFreshUrlCaches(game);
                try { await GameArt.PrefetchGameArtAsync(game, refreshStale: true).ConfigureAwait(false); }
                catch (Exception ex) { Trace.WriteLine($"[Prefetch] {game.Name}: {ex.Message}"); }
                // Only mark swept when an SGDB key is configured: with no key nothing SGDB-side warmed,
                // and a key added mid-session must re-warm on the next kick, not stay filtered out.
                if (!string.IsNullOrWhiteSpace(GameArt.SteamGridDbKey))
                    lock (_lock) _done.Add(GameLibrary.MatchKey(game));
                if (!wasWarm)
                {
                    warmed++;
                    await Task.Delay(perGameDelay).ConfigureAwait(false);   // rate-limit only real work
                }
            }
        }
        finally
        {
            lock (_lock) _running = false;
            if (warmed > 0) Trace.WriteLine($"[Prefetch] sweep done — warmed {warmed} of {todo.Count} game(s)");
        }
    }
}
