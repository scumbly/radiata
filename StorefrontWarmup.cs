using System.Diagnostics;

namespace ControllerWheel;

/// <summary>Anticipates the one visibly slow launch case: a game launched THROUGH a storefront that isn't
/// running yet, costing seconds of cold start with nothing on screen — the fire path holds a lingering hub
/// over it. Keep the prediction narrow so no hub can linger past a fast launch: an already-running
/// storefront and direct-exe / <c>shell:AppsFolder</c> URLs all predict nothing.</summary>
internal static class StorefrontWarmup
{
    /// <summary>URL scheme → launcher key (<see cref="LauncherCatalog"/>) → process names that mean
    /// "this storefront is already up". Battle.net's ever-present <c>Agent.exe</c> is deliberately NOT
    /// listed: it runs as a background updater even when the client is closed.</summary>
    private static readonly (string Scheme, string Key, string[] Processes)[] Map =
    [
        ("steam://",                    "steam",     ["steam"]),
        ("com.epicgames.launcher://",   "epic",      ["EpicGamesLauncher"]),
        ("goggalaxy://",                "gog",       ["GalaxyClient"]),
        ("uplay://",                    "ubisoft",   ["upc", "UbisoftConnect"]),
        ("battlenet://",                "battlenet", ["Battle.net"]),
        ("itch://",                     "itch",      ["itch"]),
        // EADesktop is the long-lived client; EALauncher is its short-lived bootstrapper, listed so a
        // launch that's already bootstrapping doesn't get a second "Launching" hub.
        ("origin2://",                  "ea",        ["EADesktop", "EALauncher"]),
        ("playnite://",                 "playnite",  ["Playnite.FullscreenApp", "Playnite.DesktopApp"]),
    ];

    /// <summary>The storefront this launch URL routes through and which is NOT running yet. Null when the
    /// URL launches directly, the storefront is unknown, or it's already up.</summary>
    public static LauncherInfo? PredictColdStart(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        foreach (var (scheme, key, procs) in Map)
        {
            if (!url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) continue;
            if (IsAnyRunning(procs)) return null;                    // already warm — launch is prompt
            return LauncherCatalog.Find(key);
        }
        return null;
    }

    private static bool IsAnyRunning(string[] names)
    {
        foreach (var n in names)
        {
            try { if (WindowsPlatformActions.AnySessionProcess(n)) return true; }
            catch (Exception ex) { Trace.WriteLine($"[Warmup] process probe '{n}' failed: {ex.Message}"); }
        }
        return false;
    }
}
