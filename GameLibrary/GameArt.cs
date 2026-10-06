using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>
/// Resolves game key art and caches it to disk (%APPDATA%\Radiata\artcache). Default source order:
/// SteamGridDB (needs a user-configured key; covers Epic/GOG/Xbox/itch by appid or name), then a
/// Playnite-supplied local cover, then the storefront's own cover URL, then Steam's public CDN (by
/// appid) as last resort; PreferPlaynite swaps Playnite ahead of SteamGridDB (see CoverPathAsync).
/// A ".miss" marker per source avoids re-hitting the network for art that isn't there.
/// </summary>
internal static class GameArt
{
    // MaxResponseContentBufferSize caps a buffered read even when the response declares no Content-Length
    // (which is where FetchBytesAsync's own length check can't help): the read throws instead of growing
    // without limit. 33 MB = MaxArtBytes plus headroom, so the explicit check is what normally rejects.
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = 33L * 1024 * 1024,
    };

    // Not `readonly` on purpose, and nothing in the app ever assigns it: the quota trim below DELETES
    // files, so the only safe way to test it is to redirect this at a temp folder of synthetic art — and
    // an initonly static cannot be redirected even by reflection. See tools\TestHarness ▸ T_ArtQuota,
    // which restores this to the real path in a finally.
    private static string CacheDir = Path.Combine(AppPaths.AppDataDir, "artcache");

    /// <summary>The on-disk cover/logo cache directory. Backup/restore round-trips slice logo files
    /// through here so installed-game "Custom Logo" picks survive a wipe or a move to another machine.</summary>
    public static string CacheDirectory => CacheDir;

    /// <summary>Optional SteamGridDB API key (from SystemConfig). Set at startup + on config reload;
    /// null/blank disables the SteamGridDB source.</summary>
    public static string? SteamGridDbKey { get; set; }

    /// <summary>Preferred SteamGridDB grid style token (e.g. "no_logo"), or null/blank for any. Only
    /// affects the SteamGridDB source. Set at startup + on config reload.</summary>
    public static string? SteamGridDbStyle { get; set; }

    /// <summary>When both Playnite and SteamGridDB are configured, prefer the Playnite cover over
    /// SteamGridDB. Set from SystemConfig at startup + on config reload.</summary>
    public static bool PreferPlaynite { get; set; }

    /// <summary>The URL used to extract a storefront-native id for art lookup: prefer the direct
    /// storefront URL (e.g. steam://rungameid/ID). Playnite-sourced games carry that on DirectLaunchUrl
    /// while their LaunchUrl is a playnite:// link with no storefront id — so without this the grid
    /// can't find Steam covers/logos for Playnite games and falls back to text.</summary>
    private static string ArtUrl(InstalledGame g) =>
        !string.IsNullOrEmpty(g.DirectLaunchUrl) ? g.DirectLaunchUrl! : (g.LaunchUrl ?? "");

    /// <summary>The STEAM appid from the launch URL, or null for any other storefront. The scheme MUST be
    /// matched: a bare "rungameid/(\d+)" also matches GOG's <c>goggalaxy://rungameid/&lt;productId&gt;</c>,
    /// feeding GOG ids into Steam-keyed lookups (404 + a cached .miss, no cover even when a name match
    /// exists). Scheme-anchored, GOG falls through to the name search.</summary>
    private static string? SteamAppId(InstalledGame g)
    {
        var m = Regex.Match(ArtUrl(g), @"steam://rungameid/(\d+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static async Task<ImageSource?> GetAsync(InstalledGame game)
    {
        var path = await GetCachedPathAsync(game).ConfigureAwait(false);
        return path is null ? null : LoadFrozen(path);
    }

    /// <summary>Resolve a stable local file PATH to the game's cover art (downloading + caching as
    /// needed), or null if no source has art. Used both to render browser tiles and to persist a
    /// cover reference in a wheel slice's config.</summary>
    public static Task<string?> GetCachedPathAsync(InstalledGame game) => GetCachedPathAsync(game, false);

    /// <summary>The cover the auto-resolution chain would pick, IGNORING any saved override — i.e. the
    /// "default" stop (candidate 0) in the Select cover cycle.</summary>
    public static Task<string?> GetAutoCoverPathAsync(InstalledGame game) => GetCachedPathAsync(game, true);

    private static async Task<string?> GetCachedPathAsync(InstalledGame game, bool ignoreOverride)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            if (!ignoreOverride && GameMetadata.CoverOverride(game)?.Path is { Length: > 0 } chosen && File.Exists(chosen)) return chosen;   // user's Select pick wins
            // A curated pick (CuratedArt — hand-picked art for popular games, pinned by explicit URL)
            // beats every auto source: it IS the shipped default for that game. Needs no SGDB key (the
            // URL is a plain CDN fetch); a failed download falls through to the normal chain.
            if (CuratedArt.Get(game.Name) is { } cur)
            {
                if (cur.FlatCoverHex is { } hex && FlatColorPath("#" + hex) is { } flat) return flat;
                if (cur.CoverUrl is { } cu
                    && await UrlFileAsync($"curated_{GameLibrary.NormalizeName(game.Name)}.jpg", cu).ConfigureAwait(false) is { } cp)
                    return cp;
            }
            // Source priority. Each source returns null when it's not configured/applicable, so the
            // ?? chain naturally skips the missing ones. Steam CDN is the last-resort fallback in both
            // orders; PreferPlaynite swaps Playnite ahead of SteamGridDB.
            return PreferPlaynite
                ? PlaynitePath(game)
                  ?? await SteamGridDbPathAsync(game).ConfigureAwait(false)
                  ?? await SourceCoverPathAsync(game).ConfigureAwait(false)
                  ?? await SteamCdnPathAsync(game).ConfigureAwait(false)
                : await SteamGridDbPathAsync(game).ConfigureAwait(false)
                  ?? PlaynitePath(game)
                  ?? await SourceCoverPathAsync(game).ConfigureAwait(false)
                  ?? await SteamCdnPathAsync(game).ConfigureAwait(false);
        }
        catch { return null; }
    }

    /// <summary>Download-and-cache an image by explicit URL — a curated pick (see <see cref="CuratedArt"/>)
    /// or a storefront's own cover URL. Same miss-marker discipline as the auto sources: a definitive
    /// failure is remembered for <see cref="MissMarkerTtl"/> so a dead URL doesn't re-fetch on every tile,
    /// a transient one isn't.</summary>
    private static Task<string?> UrlFileAsync(string fileName, string url) =>
        CacheFetchAsync(Path.Combine(CacheDir, fileName), () => FetchBytesAsync(url));

    /// <summary>The cover the game's OWN storefront published, from
    /// <see cref="InstalledGame.SourceCoverUrl"/> (itch reads it out of butler.db) — downloaded and cached
    /// like a curated pick. Null when the storefront gave us no URL.
    ///
    /// <para>Deliberately placed AFTER SteamGridDB in the chain: SGDB art is portrait 600x900, the tile's
    /// own shape, while an itch cover is landscape 315x250 and centre-crops in the tile's UniformToFill
    /// brush. So this can only ever turn "no art" into "art" — it never displaces a better-shaped cover.
    /// For itch specifically it's the ONLY source there is: the Steam CDN is Steam-gated, and Playnite's
    /// local covers need Playnite installed.</para></summary>
    private static Task<string?> SourceCoverPathAsync(InstalledGame game) =>
        string.IsNullOrWhiteSpace(game.SourceCoverUrl)
            ? Task.FromResult<string?>(null)
            // Keyed like every other non-Steam cache entry (storefront + name), so it lands beside the
            // game's other art and the quota trim treats it identically.
            : UrlFileAsync($"srccover_{LogoCacheId(game)}.jpg", game.SourceCoverUrl!);

    /// <summary>Playnite-supplied local cover (no network), or null if the game has none.</summary>
    private static string? PlaynitePath(InstalledGame game) =>
        !string.IsNullOrWhiteSpace(game.CoverPath) && File.Exists(game.CoverPath) ? game.CoverPath : null;

    // ── Steam public CDN (by appid) ─────────────────────────────────────────────

    /// <summary>A ".miss" marker only suppresses re-fetching for 14 days, then expires: a genuine
    /// "no art" today can gain art later (SteamGridDB uploads land constantly), so misses self-heal at
    /// a negligible request cost instead of being permanent until the user finds the manual "Retry
    /// missed covers". A stale marker that re-misses is rewritten, which refreshes its clock.</summary>
    private static readonly TimeSpan MissMarkerTtl = TimeSpan.FromDays(14);

    private static bool MissIsFresh(string miss)
    {
        try { return File.Exists(miss) && DateTime.UtcNow - File.GetLastWriteTimeUtc(miss) < MissMarkerTtl; }
        catch { return false; }   // unreadable marker → behave as absent (re-fetch)
    }

    /// <summary>The cache-file/miss-marker dance every art source repeats: a cache hit returns the file,
    /// a fresh miss marker returns null, otherwise <paramref name="download"/> runs and its result is
    /// written to cache (genuine miss) or the marker (definitive absence) — a transient failure caches
    /// neither, so the next resolve retries. Callers whose fallback continues past a miss (rather than
    /// returning null) can't use this — see <see cref="GetSliceLogoPathAsync"/>.</summary>
    private static async Task<string?> CacheFetchAsync(string file, Func<Task<(byte[]? bytes, bool transient)>> download)
    {
        string miss = file + ".miss";
        if (File.Exists(file)) return file;
        if (MissIsFresh(miss)) return null;
        var (bytes, transient) = await download().ConfigureAwait(false);
        if (bytes is null) { if (!transient) File.WriteAllText(miss, ""); return null; }
        return await WriteCacheFileAsync(file, bytes).ConfigureAwait(false) ? file : null;
    }

    private static Task<string?> SteamCdnPathAsync(InstalledGame game)
    {
        if (!string.Equals(game.Storefront, "Steam", StringComparison.OrdinalIgnoreCase)) return Task.FromResult<string?>(null);
        if (SteamAppId(game) is not { } appId) return Task.FromResult<string?>(null);
        return CacheFetchAsync(Path.Combine(CacheDir, $"steam_{appId}.jpg"), () => DownloadAsync(appId));
    }

    /// <summary>Try each Steam CDN cover flavour via <see cref="FetchBytesAsync"/>, so the caller gets
    /// its transient contract: (null, true) when any failure was network/timeout/429/5xx — a ".miss"
    /// must only be cached for a definitive not-found on every flavour.</summary>
    private static async Task<(byte[]? bytes, bool transient)> DownloadAsync(string appId)
    {
        // library_600x900 is the portrait box art (best for the portrait browser cards + square
        // slice tiles); library_hero (landscape) and header (capsule) are fallbacks.
        string[] urls =
        {
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_hero.jpg",
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
        };
        bool anyTransient = false;
        foreach (var url in urls)
        {
            var (bytes, transient) = await FetchBytesAsync(url).ConfigureAwait(false);
            if (bytes is not null) return (bytes, false);
            anyTransient |= transient;
        }
        return (null, anyTransient);
    }

    // ── SteamGridDB (optional, user key) — covers any storefront ────────────────

    private static Task<string?> SteamGridDbPathAsync(InstalledGame game)
    {
        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return Task.FromResult<string?>(null);
        return CacheFetchAsync(Path.Combine(CacheDir, $"sgdb_{SgdbCacheId(game)}.jpg"),
            () => SgdbDownloadAsync(game, key!));
    }

    /// <summary>Stable, filesystem-safe cache key for a game's SteamGridDB art: storefront + appid
    /// (Steam) or sanitized name.</summary>
    private static string SgdbCacheId(InstalledGame game)
    {
        // Mix in the style so a different style fetches a fresh file; "Any" (blank) keeps the original
        // ids so existing cached covers aren't invalidated.
        string style = string.IsNullOrWhiteSpace(SteamGridDbStyle) ? "" : "_" + SteamGridDbStyle.Trim();
        string basis = (SteamAppId(game) is { } sid ? "steam_" + sid : game.Storefront + "_" + game.Name) + style;
        var safe = new string(basis.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return safe.Length > 64 ? safe[..64] : safe;
    }

    /// <summary>The game's SteamGridDB grids endpoint base (by Steam appid, else by name search), or null.</summary>
    private static async Task<string?> SgdbBaseAsync(InstalledGame game, string key)
    {
        if (SteamAppId(game) is { } sid) return $"https://www.steamgriddb.com/api/v2/grids/steam/{sid}";
        return await GameArtNameMatch.SgdbSearchIdAsync(game.Name, key).ConfigureAwait(false) is { } gid
            ? $"https://www.steamgriddb.com/api/v2/grids/game/{gid}" : null;
    }

    /// <summary>The preferred DEFAULT cover URL: the configured style if one is set, else "no_logo" (we
    /// draw the game's own logo on top, so a logo-free cover reads best), falling back to the top-scored
    /// cover of any style. This is the same image the cover cycle's "default" stop shows.</summary>
    private static async Task<string?> SgdbDefaultUrlAsync(string @base, string key)
    {
        const string q = "dimensions=600x900&types=static";
        string style = string.IsNullOrWhiteSpace(SteamGridDbStyle) ? "no_logo" : SteamGridDbStyle.Trim();
        return await SgdbFirstUrlAsync($"{@base}?{q}&styles={Uri.EscapeDataString(style)}", key).ConfigureAwait(false)
            ?? await SgdbFirstUrlAsync($"{@base}?{q}", key).ConfigureAwait(false);
    }

    private static async Task<(byte[]? bytes, bool transient)> SgdbDownloadAsync(InstalledGame game, string key)
    {
        try
        {
            var @base = await SgdbBaseAsync(game, key).ConfigureAwait(false);
            if (@base is null) return (null, false);         // search returned empty → genuinely no such game
            var imageUrl = await SgdbDefaultUrlAsync(@base, key).ConfigureAwait(false);
            if (imageUrl is null) return (null, false);       // game exists but has no cover of any style
            return await FetchBytesAsync(imageUrl).ConfigureAwait(false);
        }
        catch (SgdbTransientException) { return (null, true); }
        catch { return (null, false); }
    }

    // Query-variant generation and the exact-match compare live in GameArtNameMatch.

    private static async Task<string?> SgdbFirstUrlAsync(string url, string key)
    {
        using var doc = await SgdbJsonAsync(url, key).ConfigureAwait(false);
        if (doc is null) return null;
        return doc.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
            && data[0].TryGetProperty("url", out var u)
            ? u.GetString() : null;
    }

    /// <summary>Validate a SteamGridDB API key with a lightweight authorized call (onboarding's cover-art
    /// step). True = the key authenticates; false = rejected (401) or unreachable.</summary>
    public static async Task<bool> TestKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://www.steamgriddb.com/api/v2/search/autocomplete/test");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>Thrown for a SteamGridDB failure that is TRANSIENT (bad key / rate-limit / server / network)
    /// rather than a genuine "no such art", so callers can avoid caching a permanent ".miss" for it.</summary>
    private sealed class SgdbTransientException : Exception { }

    /// <summary>Image types we ask SteamGridDB for. WPF decodes PNG and JPEG through WIC on every supported
    /// Windows; **WebP is NOT decodable on Windows 10** without the Store's "Webp Image Extensions", and AVIF
    /// isn't either — a WebP asset would download fine, get written into the cache under its cover/logo name,
    /// and then fail to decode FOREVER, because a cached file that exists is a hit and never re-fetched.
    /// Asking the server to omit them removes the whole class for the price of a query parameter.
    /// <para><c>mimes</c> is a REAL, VALIDATED parameter (verified against the live API): a bad value
    /// returns 400, an unknown parameter name is ignored.</para>
    /// <para>⚠ The allowed enum is PER ENDPOINT (openapi.yml): grids/heroes take png|jpeg|webp, but
    /// <b>/logos take ONLY png|webp and /icons ONLY png|ico</b>. Sending <c>image/jpeg</c> to /logos is a
    /// 400 on EVERY request, which reads as a definitive "no art" and .miss-poisons every game's logo for
    /// 14 days while covers keep working. Hence the per-path table below; never send one shared list.</para></summary>
    private const string SgdbMimesGrid = "mimes=image%2Fpng%2Cimage%2Fjpeg";
    private const string SgdbMimesLogo = "mimes=image%2Fpng";   // webp excluded: Win10 can't decode it
    private const string SgdbMimesIcon = "mimes=image%2Fpng";

    /// <summary>SteamGridDB paths that return IMAGE assets and the <c>mimes</c> filter EACH accepts.
    /// The search/autocomplete endpoint returns games, not images, and takes no such filter.</summary>
    private static readonly (string Path, string Mimes)[] SgdbAssetPaths =
    [
        ("/grids/",  SgdbMimesGrid),
        ("/heroes/", SgdbMimesGrid),
        ("/logos/",  SgdbMimesLogo),
        ("/icons/",  SgdbMimesIcon),
    ];

    /// <summary>Add the decodable-formats filter to an asset request. Done HERE, at the single point every
    /// SteamGridDB call passes through, rather than in the dozen query strings that build these URLs — one of
    /// which would inevitably be missed or added later without it.</summary>
    private static string WithSgdbMimes(string url)
    {
        if (url.Contains("mimes=", StringComparison.OrdinalIgnoreCase)) return url;   // caller was explicit
        foreach (var (path, mimes) in SgdbAssetPaths)
            if (url.Contains(path, StringComparison.OrdinalIgnoreCase))
                return url + (url.Contains('?') ? "&" : "?") + mimes;
        return url;
    }

    internal static async Task<JsonDocument?> SgdbJsonAsync(string url, string key)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, WithSgdbMimes(url));
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                // 401 (bad key), 429 (rate-limit), 5xx (server) are transient — don't let the caller cache
                // a permanent miss for the whole library. 400 is transient too: it means OUR request is
                // malformed (see the /logos mimes trap above), so it says nothing about whether the game
                // has art. 404 / other 4xx = a real "not found" → return null.
                int code = (int)resp.StatusCode;
                if (code is 400 or 401 or 429 || code >= 500) throw new SgdbTransientException();
                return null;
            }
            var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        }
        catch (SgdbTransientException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            throw new SgdbTransientException();   // network failure / timeout — transient, don't cache a miss
        }
        // Parse/other unexpected failures are TRANSIENT, not "no result": a mangled response (flaky
        // proxy, captive portal, truncated body) must not let a caller cache a permanent .miss.
        catch { throw new SgdbTransientException(); }
    }

    /// <summary>Hard ceiling on a single downloaded image. Real SteamGridDB art tops out a couple of MB
    /// (the largest in one real 498-file cache is 4.8 MB), so this is far above anything legitimate — it
    /// exists so a broken or hostile response can't stream unbounded bytes into memory. The HttpClient's
    /// own <c>MaxResponseContentBufferSize</c> is the backstop for a response that declares no length.</summary>
    private const long MaxArtBytes = 32L * 1024 * 1024;

    /// <summary>Fetch an image's bytes without caching it — used by <see cref="CuratedPicksExport"/> to prove a
    /// resolved URL really is the artwork sitting in a cached pick file.</summary>
    internal static async Task<byte[]?> FetchImageBytesAsync(string url) =>
        (await FetchBytesAsync(url).ConfigureAwait(false)).bytes;

    /// <summary>Fetch raw image bytes: (bytes,false) on success; (null,false) on a definitive absence
    /// (404/other 4xx, or an over-size body); (null,true) on a TRANSIENT failure (429/5xx/network/timeout)
    /// that must NOT be cached.</summary>
    private static async Task<(byte[]? bytes, bool transient)> FetchBytesAsync(string url)
    {
        try
        {
            using var resp = await Http.GetAsync(url).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                // Declared length first: refuse before reading a byte. Over-size counts as a DEFINITIVE
                // miss, not a transient one — retrying can't make the file smaller, and marking it
                // transient would re-download it on every prefetch sweep forever.
                if (resp.Content.Headers.ContentLength is > MaxArtBytes) return (null, false);
                return (await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false), false);
            }
            int code = (int)resp.StatusCode;
            return (null, code is 429 || code >= 500);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return (null, true);
        }
        catch { return (null, false); }
    }

    // ── Oversized downloaded art: normalise on the way into the cache ───────────────────────────────
    // SteamGridDB serves some absurd assets — one real logo is 32766x22265 (729 MP, ~2.9 GB decoded) in a
    // 4.8 MB PNG. Display survives it only because every read decodes downsampled (LoadFrozen's
    // DecodePixelWidth), which is a guarantee that quietly fails for an INTERLACED PNG, since Adam7 has to
    // be buffered whole. So anything over the threshold is re-encoded ONCE here, at a size still far above
    // what anything on screen uses. Under the threshold — the overwhelming majority — bytes are stored
    // byte-for-byte as served: no re-encode, no quality change.

    private const long NormalizeAbovePixels = 16_000_000;
    private const int  NormalizedMaxEdge    = 2048;   // covers/logos are decoded at 600 for display

    /// <summary>Downscale + re-encode <paramref name="bytes"/> if the image is huge; otherwise return it
    /// unchanged. The encoder follows <paramref name="targetFile"/>'s extension, so a .png logo stays PNG
    /// (alpha intact) and a .jpg cover stays JPEG. ANY failure returns the original bytes — a normalisation
    /// problem must never cost the user their art.</summary>
    private static byte[] NormalizeArtBytes(byte[] bytes, string targetFile)
    {
        try
        {
            int w, h;
            using (var probe = new MemoryStream(bytes, writable: false))
            {
                var dec = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (dec.Frames.Count == 0) return bytes;
                w = dec.Frames[0].PixelWidth;
                h = dec.Frames[0].PixelHeight;
            }
            if (w <= 0 || h <= 0 || (long)w * h <= NormalizeAbovePixels) return bytes;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption   = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.StreamSource  = new MemoryStream(bytes, writable: false);
            if (w >= h) bmp.DecodePixelWidth = NormalizedMaxEdge;
            else        bmp.DecodePixelHeight = NormalizedMaxEdge;
            bmp.EndInit();
            bmp.Freeze();   // created off the UI thread — freezing makes it safe to hand to the encoder

            BitmapEncoder enc =
                Path.GetExtension(targetFile).Equals(".png", StringComparison.OrdinalIgnoreCase)
                    ? new PngBitmapEncoder()
                    : new JpegBitmapEncoder { QualityLevel = 92 };
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var outMs = new MemoryStream();
            enc.Save(outMs);
            var outBytes = outMs.ToArray();
            if (outBytes.Length == 0) return bytes;
            // Re-encoding can GROW a file that was only just over the threshold (our encoder is less
            // efficient than the original's). Those are also the LEAST risky files — the unbounded-decode
            // danger lives in the hundreds of megapixels, not at 16 — so keep the original and move on.
            //
            // ⚠ But ONLY while the original is still READABLE. This mercy is keyed on BYTE SIZE, and a
            // hugely compressible image (flat colour / mostly-transparent) can be enormous in pixels yet
            // tiny on disk — above LoadFrozen's UserLogoMaxPixels read budget it is refused on EVERY read,
            // and because the file exists it's a cache hit that never re-fetches: the logo is gone
            // permanently. Above the read budget a BIGGER file that decodes beats a smaller one that never
            // will, so take the re-encode regardless of size.
            if (outBytes.Length >= bytes.Length && (long)w * h <= UserLogoMaxPixels)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[Art] normalise declined for {Path.GetFileName(targetFile)}: re-encode was larger "
                    + $"({outBytes.Length / 1024} KB vs {bytes.Length / 1024} KB)");
                return bytes;
            }
            if (outBytes.Length >= bytes.Length)
                System.Diagnostics.Trace.WriteLine(
                    $"[Art] normalise kept a LARGER re-encode for {Path.GetFileName(targetFile)} "
                    + $"({outBytes.Length / 1024} KB vs {bytes.Length / 1024} KB) — the original's "
                    + $"{(long)w * h / 1_000_000} MP is over the {UserLogoMaxPixels / 1_000_000} MP read budget "
                    + "and would never decode");

            System.Diagnostics.Trace.WriteLine(
                $"[Art] normalised {Path.GetFileName(targetFile)}: {w}x{h} ({(long)w * h / 1_000_000} MP, "
                + $"{bytes.Length / 1024} KB) → {bmp.PixelWidth}x{bmp.PixelHeight} ({outBytes.Length / 1024} KB)");
            return outBytes;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Art] normalise skipped for {Path.GetFileName(targetFile)}: {ex.Message}");
            return bytes;
        }
    }

    /// <summary>One-time pass over art ALREADY cached, applying <see cref="NormalizeArtBytes"/> to the
    /// oversized ones — a cache filled by an older build still holds them, and they're exactly the files
    /// whose decode cost is unbounded. Best-effort and idempotent (a normalised file is under the
    /// threshold, so it's skipped forever after). Returns how many were rewritten.</summary>
    public static int NormalizeOversizedCache()
    {
        int n = 0;
        try
        {
            if (!Directory.Exists(CacheDir)) return 0;
            foreach (var file in Directory.GetFiles(CacheDir))
            {
                string ext = Path.GetExtension(file);
                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    // Header first: the sweep must be nearly free for the ~99% of files that are fine,
                    // rather than reading 150 MB of cache to discover that.
                    int w, h;
                    using (var fs = File.OpenRead(file))
                    {
                        var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        if (dec.Frames.Count == 0) continue;
                        w = dec.Frames[0].PixelWidth;
                        h = dec.Frames[0].PixelHeight;
                    }
                    if (w <= 0 || h <= 0 || (long)w * h <= NormalizeAbovePixels) continue;

                    var bytes = File.ReadAllBytes(file);
                    var slim  = NormalizeArtBytes(bytes, file);
                    if (ReferenceEquals(slim, bytes)) continue;      // normalisation failed → leave it alone
                    AtomicFile.WriteAllBytes(file, slim);
                    n++;
                }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Art] sweep skipped {Path.GetFileName(file)}: {ex.Message}"); }
            }
            if (n > 0)
            {
                System.Diagnostics.Trace.WriteLine($"[Art] normalised {n} oversized cached image(s)");
                InvalidateCacheSizeEstimate();   // rewrote files in place → the measured total is stale
            }
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Art] normalise sweep failed: {ex.Message}"); }
        return n;
    }

    // A marker beside the art, not a config flag: it belongs to the cache's own state, and a cache wipe
    // SHOULD reset it (the sweep over an empty cache costs nothing anyway).
    //
    // BUMP THE VERSION whenever NormalizeArtBytes starts rewriting files it leaves alone today — the
    // marker is what stops the sweep re-running, so a fix to the normalise RULE is invisible on any cache
    // that already swept.
    private static string NormalizeMarkerPath => Path.Combine(CacheDir, ".normalized-v2");

    /// <summary>Run <see cref="NormalizeOversizedCache"/> at most once per cache. Cheap to call: after the
    /// first pass this is a single File.Exists.</summary>
    public static void NormalizeOversizedCacheOnce()
    {
        try
        {
            if (!Directory.Exists(CacheDir) || File.Exists(NormalizeMarkerPath)) return;
            NormalizeOversizedCache();
            File.WriteAllText(NormalizeMarkerPath, "");
        }
        catch { /* best effort — retried next session */ }
    }

    // ── One-time purge of logo data poisoned by the /logos mimes bug ────────────────────────────────
    // WithSgdbMimes once sent image/jpeg to /logos — invalid there — so every SGDB logo request 400'd,
    // leaving per game: a logosq_*.miss and a logo_sgdb_*.miss (14-day TTL), and an EMPTY
    // urls_logos_*.json candidate list, which non-refresh reads accept regardless of age. Bump the marker
    // version if a future bug poisons logo data again.
    private static string LogoPurgeMarkerPath => Path.Combine(CacheDir, ".logofix-v1");

    /// <summary>Delete the poisoned logo miss-markers and empty logo-candidate lists, at most once per
    /// cache. Cheap after the first pass (one File.Exists). Successful logo files are never touched.</summary>
    public static void PurgePoisonedLogoDataOnce()
    {
        try
        {
            if (!Directory.Exists(CacheDir) || File.Exists(LogoPurgeMarkerPath)) return;
            int n = 0;
            foreach (var pattern in new[] { "logosq_*.miss", "logo_*.miss", "urls_logos_*.json" })
                foreach (var f in Directory.EnumerateFiles(CacheDir, pattern))
                    try { File.Delete(f); n++; } catch { /* locked — the TTL still expires it */ }
            File.WriteAllText(LogoPurgeMarkerPath, "");
            if (n > 0)
            {
                System.Diagnostics.Trace.WriteLine($"[Art] purged {n} logo miss/list file(s) poisoned by the /logos mimes bug");
                ArtPrefetcher.Reset();   // let the next sweep re-fetch what the markers were suppressing
            }
        }
        catch { /* best effort — retried next session */ }
    }

    /// <summary>Write an image into the cache ATOMICALLY (temp file + move) so a disk-full / kill
    /// mid-write can never leave a truncated file that File.Exists then treats as a valid hit forever.
    /// Also tolerates the benign resolver race (grid tile load vs the ArtPrefetcher sweep writing the
    /// same path): the loser's sharing-violation IOException is swallowed and the winner's completed
    /// file accepted. True = <paramref name="file"/> exists on return (ours or the other racer's).</summary>
    // ── Cache size budget ───────────────────────────────────────────────────────
    //
    // The prefetcher sweeps a WHOLE library and caches several images per game (cover candidates + logo
    // flavours for the Select/Start cycles), so an unbounded cache grows with library size until the profile disk
    // complains — and low disk on the system volume degrades far more than Radiata. Two limits, both
    // enforced at the write choke point below so no source can bypass them:
    //
    //   • a total cache quota, trimmed oldest-first when a write would cross it;
    //   • a free-space floor, below which we simply stop caching art (the app still works — a cache miss
    //     costs a spinner and a re-fetch, which is the correct thing to lose here).
    //
    // NormalizeArtBytes already caps each individual file's pixel count, so the quota is about COUNT, not
    // rogue single images.
    private const long CacheQuotaBytes   = 512L * 1024 * 1024;   // ~512 MB of covers/logos
    private const long MinFreeDiskBytes  = 500L * 1024 * 1024;   // never take the volume below this
    private static long _cacheBytes = -1;                        // -1 = not measured yet this session
    private static readonly object _quotaGate = new();

    /// <summary>Reserve room for a <paramref name="incoming"/>-byte cache write that will REPLACE
    /// <paramref name="replacing"/> bytes already on disk, trimming the oldest art if the quota would be
    /// crossed. False = don't write (disk too full, or the trim couldn't free enough).
    /// Trimming uses last-WRITE time, not last-access: NTFS last-access updates are disabled by default on
    /// Windows, so an access-based LRU would silently be a random eviction.
    /// <para>The estimate books the NET change, not the full incoming size: booking the full size on every
    /// refresh-in-place or lost resolver/prefetch race ratchets the estimate upward until "cache at quota
    /// and nothing evictable" while the disk is nowhere near quota. The free-DISK check stays on the full
    /// incoming size: the temp file and the file it replaces coexist until the move.</para></summary>
    private static bool ReserveCacheSpace(long incoming, long replacing)
    {
        lock (_quotaGate)
        {
            try
            {
                var root = Path.GetPathRoot(CacheDir);
                if (!string.IsNullOrEmpty(root))
                {
                    var free = new DriveInfo(root).AvailableFreeSpace;
                    if (free - incoming < MinFreeDiskBytes)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[Art] skipping cache write — only {free / (1024 * 1024)} MB free on {root}");
                        return false;
                    }
                }

                long net = incoming - replacing;   // may be negative — a smaller refresh SHRINKS the cache
                if (_cacheBytes < 0) _cacheBytes = MeasureCacheBytes();
                if (_cacheBytes + net <= CacheQuotaBytes) { _cacheBytes = Math.Max(0, _cacheBytes + net); return true; }

                // Trim to 80% of quota so a full cache doesn't evict on every single subsequent write —
                // then RE-MEASURE from disk rather than arithmetic on the old estimate. This is the one
                // moment the estimate's residual drift (racing writers each netting against the same
                // pre-existing file) can turn into wrongful evictions or refusals, and one directory
                // enumeration here is cheap next to the trim's deletes.
                long target = (long)(CacheQuotaBytes * 0.8) - Math.Max(0, net);
                TrimOldestArt(target);
                _cacheBytes = MeasureCacheBytes();
                if (_cacheBytes + net > CacheQuotaBytes)
                {
                    System.Diagnostics.Trace.WriteLine("[Art] cache at quota and nothing evictable — skipping write");
                    return false;
                }
                _cacheBytes = Math.Max(0, _cacheBytes + net);
                return true;
            }
            catch (Exception ex)
            {
                // Budgeting must never be the reason art breaks: on any failure to measure, allow the write
                // and re-measure next time.
                System.Diagnostics.Trace.WriteLine($"[Art] cache budget check failed: {ex.Message} — allowing write");
                _cacheBytes = -1;
                return true;
            }
        }
    }

    private static long MeasureCacheBytes()
    {
        long total = 0;
        if (!Directory.Exists(CacheDir)) return 0;
        foreach (var f in Directory.EnumerateFiles(CacheDir))
            try { total += new FileInfo(f).Length; } catch { /* vanished mid-scan */ }
        return total;
    }

    /// <summary>Delete cached art oldest-first until the cache is at or under <paramref name="target"/> bytes.
    /// Returns bytes actually freed. Skips <c>.miss</c> markers (deleting those would trigger a re-fetch
    /// storm — the opposite of relieving pressure), <c>.tmp</c> files owned by a concurrent writer, and the
    /// dotfile state markers.</summary>
    private static long TrimOldestArt(long target)
    {
        long freed = 0;
        try
        {
            var files = new List<FileInfo>();
            foreach (var f in Directory.EnumerateFiles(CacheDir))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith('.')) continue;
                if (name.EndsWith(".miss", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.EndsWith(".tmp",  StringComparison.OrdinalIgnoreCase)) continue;
                try { files.Add(new FileInfo(f)); } catch { /* vanished */ }
            }
            long total = files.Sum(f => f.Length);
            foreach (var f in files.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (total - freed <= target) break;
                try { long len = f.Length; f.Delete(); freed += len; }
                catch { /* in use by a decode in flight, or gone — leave it, try the next */ }
            }
            if (freed > 0)
                System.Diagnostics.Trace.WriteLine($"[Art] trimmed {freed / (1024 * 1024)} MB of oldest cached art");
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Art] cache trim failed: {ex.Message}"); }
        return freed;
    }

    /// <summary>Drop the measured cache size so the next write re-measures. Call after any bulk change to the
    /// cache directory made outside <see cref="WriteCacheFileAsync"/> (a wipe, a normalise sweep).</summary>
    private static void InvalidateCacheSizeEstimate()
    {
        lock (_quotaGate) _cacheBytes = -1;
    }

    private static async Task<bool> WriteCacheFileAsync(string file, byte[] bytes)
    {
        bytes = NormalizeArtBytes(bytes, file);   // the ONE choke point every cached download passes through
        // Same choke point, so the size budget can't be bypassed either. A replace-in-place books only the
        // NET change — booking the full size every time is a one-way ratchet on the estimate (see
        // ReserveCacheSpace). The length read is best-effort; a racer deleting the file between here and
        // the move just means we booked the pessimistic (full) size, which the post-trim re-measure
        // reconciles.
        long replacing = 0;
        try { if (File.Exists(file)) replacing = new FileInfo(file).Length; } catch { /* pessimistic is fine */ }
        if (!ReserveCacheSpace(bytes.Length, replacing)) return File.Exists(file);
        // Not AtomicFile: this write is async and a losing race with a concurrent writer is recovered
        // (the other file is just as good) rather than rethrown.
        string tmp = file + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tmp, bytes).ConfigureAwait(false);
            File.Move(tmp, file, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            try { File.Delete(tmp); } catch { /* other racer's / already gone */ }
            return File.Exists(file);   // the concurrent writer's file is just as good
        }
    }

    // ── Game logos (transparent wordmarks) — SteamGridDB (white pref) → Steam CDN logo.png ─────────
    // Separate from the cover; used for Game Grid tiles and tinted assigned-game wheel slices.
    // (Playnite ExtraMetadata logos are keyed by the Playnite GUID, which InstalledGame doesn't carry,
    // so that source is deferred.)

    /// <summary>Resolve a local file path to the game's transparent LOGO (downloading + caching), or null.</summary>
    public static async Task<string?> GetLogoPathAsync(InstalledGame game)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            // Curated pick first (see the cover chain in GetCachedPathAsync for rationale). A curated
            // "hidden" means the game reads best with NO logo over its cover — that's the shipped
            // default, not a failure, so it returns null rather than falling through.
            if (CuratedArt.Get(game.Name) is { } cur)
            {
                if (cur.HideLogo) return null;
                if (cur.LogoUrl is { } lu
                    && await UrlFileAsync($"curatedlogo_{GameLibrary.NormalizeName(game.Name)}.png", lu).ConfigureAwait(false) is { } lp)
                    return lp;
            }
            return await SgdbLogoPathAsync(game).ConfigureAwait(false)
                ?? await SteamLogoPathAsync(game).ConfigureAwait(false);
        }
        catch { return null; }
    }

    // Logo cache id mirrors the cover basis but never includes the cover-style suffix (logos use white).
    private static string LogoCacheId(InstalledGame game)
    {
        string basis = SteamAppId(game) is { } sid ? "steam_" + sid : game.Storefront + "_" + game.Name;
        var safe = new string(basis.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return safe.Length > 64 ? safe[..64] : safe;
    }

    private static Task<string?> SgdbLogoPathAsync(InstalledGame game)
    {
        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return Task.FromResult<string?>(null);
        return CacheFetchAsync(Path.Combine(CacheDir, $"logo_sgdb_{LogoCacheId(game)}.png"),
            () => SgdbLogoDownloadAsync(game, key!));
    }

    /// <summary>The SteamGridDB /logos endpoint for a game (by Steam appid, else by searched game id),
    /// or null when the game can't be identified. Throws <see cref="SgdbTransientException"/> on a
    /// transient search failure, so callers can avoid caching a permanent miss.</summary>
    private static async Task<string?> SgdbLogoBaseAsync(InstalledGame game, string key) =>
        SteamAppId(game) is { } sid
            ? $"https://www.steamgriddb.com/api/v2/logos/steam/{sid}"
            : (await GameArtNameMatch.SgdbSearchIdAsync(game.Name, key).ConfigureAwait(false) is { } gid
                ? $"https://www.steamgriddb.com/api/v2/logos/game/{gid}" : null);

    private static async Task<(byte[]? bytes, bool transient)> SgdbLogoDownloadAsync(InstalledGame game, string key)
    {
        try
        {
            string? @base = await SgdbLogoBaseAsync(game, key).ConfigureAwait(false);
            if (@base is null) return (null, false);

            // Prefer a white logo (tints cleanly + reads over art); fall back to any style.
            string? imageUrl =
                await SgdbFirstUrlAsync($"{@base}?styles=white&types=static&limit=1", key).ConfigureAwait(false)
                ?? await SgdbFirstUrlAsync($"{@base}?types=static&limit=1", key).ConfigureAwait(false);
            if (imageUrl is null) return (null, false);
            return await FetchBytesAsync(imageUrl).ConfigureAwait(false);
        }
        catch (SgdbTransientException) { return (null, true); }
        catch { return (null, false); }
    }

    private static Task<string?> SteamLogoPathAsync(InstalledGame game)
    {
        if (!string.Equals(game.Storefront, "Steam", StringComparison.OrdinalIgnoreCase)) return Task.FromResult<string?>(null);
        if (SteamAppId(game) is not { } appId) return Task.FromResult<string?>(null);

        // "logo_steam2_", not "logo_steam_": caches may hold poisoned 14-day .miss markers under the
        // "logo_steam_" name (its URL "library_logo.png" does not exist on the CDN); the "2" name skips
        // them instead of waiting out their TTL. Keep the "2".
        // logo.png is the transparent logo drawn over Steam's library hero.
        return CacheFetchAsync(Path.Combine(CacheDir, $"logo_steam2_{appId}.png"),
            () => FetchBytesAsync($"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/logo.png"));
    }

    // ── Slice logos: BEST-RANKED candidate within an acceptable aspect band (slices ONLY) ──────────
    // A wheel slice draws its logo inside a roughly square content box, so a 6:1 wordmark shrinks to an
    // unreadable sliver. But "squarest wins" over-corrects: it walks past a perfectly good 3:1 wordmark to
    // reach a 1:1 emblem that ranks far lower. So the slice takes SteamGridDB's own best-ranked English
    // candidate as long as it lands inside the band below, and only when it doesn't does it fall to the
    // best-ranked candidate that does — heavily weighted toward what the community
    // ranks best, while refusing the extreme outliers. The grid keeps GetLogoPathAsync (wide reads fine
    // over a 600x900 tile) and its own Start pick, so the two never fight over one cached file — hence the
    // separate "logosq_" name. Falls back to the shared default when SteamGridDB has no dimensions.

    // The acceptable aspect band, as width/height: 4:1 wide through 1:2 tall.
    private const double SliceLogoMaxRatio = 4.0;
    private const double SliceLogoMinRatio = 0.5;

    private static string SliceLogoFile(InstalledGame game) =>
        Path.Combine(CacheDir, $"logosq_{LogoCacheId(game)}.png");

    /// <summary>True when <paramref name="path"/> is a logo THIS class cached (it lives in the art cache),
    /// as opposed to an image the user supplied themselves. Only cache-owned logos can be cycled in the
    /// slice editor — a user's own picture has no sibling candidates and must never be cycled away from.</summary>
    public static bool IsCachedLogo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (!string.Equals(Path.GetDirectoryName(path), CacheDir.TrimEnd(Path.DirectorySeparatorChar),
                               StringComparison.OrdinalIgnoreCase)) return false;
            string name = Path.GetFileName(path);
            // ⚠ Must cover EVERY name GetDownloadedLogos can hand back, curated ones included — cycling onto
            // a file this predicate rejects makes the editor treat it as the user's own picture and drop the
            // ◀ ▶ arrows mid-cycle. "curated_*.jpg" is the curated COVER and is deliberately not here.
            return name.StartsWith("logo", StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith("curatedslice_", StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith("curatedlogo_", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ── Cycling among the logos already ON DISK (the slice editor's ◀ ▶ arrows) ─────────────────────
    // Purely local: the set is whatever has been downloaded for this game (the slice pick, the grid's
    // default, its Start alternates, the Steam CDN logo), so it grows as the grid/prefetch pulls more and
    // never costs a request. Cached per game, re-read only when the files on disk actually change.

    private static readonly Dictionary<string, (string Sig, string[] Paths)> LogoSetCache = [];

    /// <summary>Every logo already downloaded for a game, in a stable order (slice pick → grid default →
    /// Start alternates → Steam CDN), DE-DUPLICATED BY CONTENT: the same image is routinely cached under
    /// several names, and arrows that step through visually identical entries read as broken. Empty when
    /// nothing is cached. Stable order matters — ◀ ▶ must be predictable, so it does NOT depend on which
    /// logo is currently selected.</summary>
    public static IReadOnlyList<string> GetDownloadedLogos(InstalledGame game)
    {
        try
        {
            string id = LogoCacheId(game);
            string norm = GameLibrary.NormalizeName(game.Name);
            // Curated files lead: when a curated pick exists it IS the default the slice shows, so the
            // arrows' "start" position should be it (dedupe-by-content collapses any same-image aliases).
            var names = new List<string> { $"curatedslice_{norm}.png", $"curatedlogo_{norm}.png",
                                           $"logosq_{id}.png", $"logo_sgdb_{id}.png" };
            names.AddRange(AlternateLogoNames(id));
            if (SteamAppId(game) is { } sid) names.Add($"logo_steam2_{sid}.png");   // see SteamLogoPathAsync

            // Only files that exist, with a signature that changes whenever any of them is rewritten.
            var live = new List<FileInfo>();
            var sig = new System.Text.StringBuilder();
            foreach (var n in names)
            {
                var fi = new FileInfo(Path.Combine(CacheDir, n));
                if (!fi.Exists) continue;
                live.Add(fi);
                sig.Append(n).Append('|').Append(fi.Length).Append('|').Append(fi.LastWriteTimeUtc.Ticks).Append(';');
            }
            string signature = sig.ToString();

            lock (LogoSetCache)
            {
                if (LogoSetCache.TryGetValue(id, out var hit) && hit.Sig == signature) return hit.Paths;
                var paths = DedupeByContent(live);
                LogoSetCache[id] = (signature, paths);
                return paths;
            }
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>"logoalt_&lt;id&gt;_&lt;n&gt;.png" for this id, in numeric order. The trailing part must parse as
    /// an integer: a plain wildcard would also match ANOTHER game whose cache id merely starts with this
    /// one ("Epic_Infinifactory" vs "Epic_Infinifactory_Demo"), pulling a different game's logos in.</summary>
    private static IEnumerable<string> AlternateLogoNames(string id)
    {
        string prefix = $"logoalt_{id}_";
        var found = new List<(int n, string name)>();
        foreach (var f in Directory.EnumerateFiles(CacheDir, prefix + "*.png"))
        {
            string name = Path.GetFileName(f);
            string tail = Path.GetFileNameWithoutExtension(name)[prefix.Length..];
            if (int.TryParse(tail, out int n)) found.Add((n, name));
        }
        found.Sort((a, b) => a.n.CompareTo(b.n));
        return found.Select(t => t.name);
    }

    private static string[] DedupeByContent(List<FileInfo> files)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<string>(files.Count);
        foreach (var fi in files)
        {
            string h;
            try { h = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fi.FullName))); }
            catch { continue; }   // unreadable / mid-write — just leave it out of the cycle
            if (seen.Add(h)) kept.Add(fi.FullName);
        }
        return [.. kept];
    }

    /// <summary>Resolve a local file path to the logo a WHEEL SLICE should draw: SteamGridDB's best-ranked
    /// candidate that fits a slice's near-square box, else the shared default resolution (grid logo →
    /// Steam CDN). Null = no logo anywhere.</summary>
    public static async Task<string?> GetSliceLogoPathAsync(InstalledGame game)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            // Curated slice pick first (see the cover chain in GetCachedPathAsync for rationale) — a
            // separate pick from the grid logo, because a slice's box is near-square. Falls through to
            // the normal band-ranked resolution when absent or the download fails.
            if (CuratedArt.Get(game.Name)?.SliceLogoUrl is { } su
                && await UrlFileAsync($"curatedslice_{GameLibrary.NormalizeName(game.Name)}.png", su).ConfigureAwait(false) is { } sp)
                return sp;
            string file = SliceLogoFile(game);
            string miss = file + ".miss";
            if (File.Exists(file)) return file;
            // Key checked BEFORE the miss logic, not inside SgdbSliceLogoAsync alone: with no key that
            // call returns a definitive (null, transient:false), which would write a 14-day .miss on a
            // keyless install — blocking a key added later (onboarding, Settings ▸ Advanced) from
            // fetching a slice logo until the markers expired. No key → no fetch, no marker.
            if (!string.IsNullOrWhiteSpace(SteamGridDbKey) && !MissIsFresh(miss))   // expired markers re-fetch (14-day TTL)
            {
                var (bytes, transient) = await SgdbSliceLogoAsync(game).ConfigureAwait(false);
                if (bytes is not null && await WriteCacheFileAsync(file, bytes).ConfigureAwait(false))
                    return file;
                if (bytes is null && !transient) File.WriteAllText(miss, "");   // cache only genuine misses
            }
            // No key / no match / no dimensioned candidate — the grid's resolution is still better than
            // a bare glyph, and it carries the Steam CDN logo.png fallback.
            return await GetLogoPathAsync(game).ConfigureAwait(false);
        }
        catch { return null; }
    }

    private static async Task<(byte[]? bytes, bool transient)> SgdbSliceLogoAsync(InstalledGame game)
    {
        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return (null, false);
        try
        {
            string? @base = await SgdbLogoBaseAsync(game, key!).ConfigureAwait(false);
            if (@base is null) return (null, false);
            // Every style, not just white: the slice masks the logo's alpha, and a wide white wordmark is
            // no better there than a wide official one. No limit — the whole list is ranked.
            string? url = await SgdbSliceLogoUrlAsync($"{@base}?types=static", key!).ConfigureAwait(false);
            if (url is null) return (null, false);
            return await FetchBytesAsync(url).ConfigureAwait(false);
        }
        catch (SgdbTransientException) { return (null, true); }
        catch { return (null, false); }
    }

    /// <summary>The logo URL a wheel slice should use, out of one /logos response.
    /// <para>Preference order, applied to the ENGLISH (or untagged) candidates and only falling back to
    /// the rest when that set is empty: (1) the best-ranked candidate whose aspect ratio is inside
    /// [<see cref="SliceLogoMinRatio"/>, <see cref="SliceLogoMaxRatio"/>]; (2) if the whole list is
    /// outside the band, the candidate closest to square. Candidates with no usable width/height are
    /// skipped — null if that leaves none.</para>
    /// <para>"Best-ranked" = SteamGridDB's own score, and where that ties — which is the common case, since
    /// most logos carry score 0 — the order the API returned them in, i.e. SteamGridDB's default sort.
    /// That's what makes step 1 land on the same image a human browsing the site would call the default.
    /// English-first exists because "squarest wins" landed PEAK on an Arabic wordmark.</para></summary>
    private static async Task<string?> SgdbSliceLogoUrlAsync(string url, string key)
    {
        using var doc = await SgdbJsonAsync(url, key).ConfigureAwait(false);
        if (doc is null || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array) return null;

        // Two independent races, run in one pass: preferred = English/untagged, any = the whole list.
        var pref = new Ranked();
        var any  = new Ranked();
        int order = 0;
        foreach (var e in data.EnumerateArray())
        {
            if (!e.TryGetProperty("url", out var u) || u.GetString() is not { Length: > 0 } s) continue;
            if (!TryPositiveNumber(e, "width", out double w) || !TryPositiveNumber(e, "height", out double h)) continue;

            var c = new Candidate(s, w / h, SgdbScore(e), order++);
            any.Offer(c);
            if (IsEnglishLogo(e)) pref.Offer(c);
        }
        return pref.Best ?? any.Best;
    }

    private readonly record struct Candidate(string Url, double Ratio, double Score, int Order)
    {
        public bool InBand => Ratio is >= SliceLogoMinRatio and <= SliceLogoMaxRatio;
        /// <summary>Distance from square, as |ln(ratio)| — scale-free, and symmetric so 2:1 and 1:2 tie.</summary>
        public double SquareDev => Math.Abs(Math.Log(Ratio));
        /// <summary>Outranks <paramref name="o"/> on SteamGridDB's own ordering (score, then API order).</summary>
        public bool OutranksInBand(Candidate o) => Score > o.Score || (Score == o.Score && Order < o.Order);
    }

    /// <summary>Running winner for one candidate set: the best-ranked IN-BAND candidate if any was offered,
    /// else the closest to square. Keeping both races live means a single pass decides it.</summary>
    private sealed class Ranked
    {
        private Candidate? _inBand, _squarest;
        public string? Best => (_inBand ?? _squarest)?.Url;

        public void Offer(Candidate c)
        {
            if (c.InBand && (_inBand is null || c.OutranksInBand(_inBand.Value))) _inBand = c;
            if (_squarest is null || c.SquareDev < _squarest.Value.SquareDev - 1e-9
                || (c.SquareDev <= _squarest.Value.SquareDev + 1e-9 && c.OutranksInBand(_squarest.Value)))
                _squarest = c;
        }
    }

    /// <summary>Whether a SteamGridDB asset is English or unlabelled. Matches the primary subtag, so
    /// "en-US" counts; a missing/empty/null tag is treated as English rather than discarded (plenty of
    /// perfectly good logos carry no language at all).</summary>
    private static bool IsEnglishLogo(JsonElement e)
    {
        if (!e.TryGetProperty("language", out var l) || l.ValueKind != JsonValueKind.String) return true;
        string lang = l.GetString() ?? "";
        if (lang.Length == 0) return true;
        int cut = lang.IndexOfAny(['-', '_']);
        return string.Equals(cut < 0 ? lang : lang[..cut], "en", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Read a positive numeric JSON property, tolerating a string-encoded number.</summary>
    private static bool TryPositiveNumber(JsonElement e, string name, out double value)
    {
        value = 0;
        if (!e.TryGetProperty(name, out var p)) return false;
        if (p.ValueKind == JsonValueKind.Number) { if (!p.TryGetDouble(out value)) return false; }
        else if (p.ValueKind != JsonValueKind.String
                 || !double.TryParse(p.GetString(), System.Globalization.NumberStyles.Float,
                                     System.Globalization.CultureInfo.InvariantCulture, out value))
            return false;
        return value > 0;
    }

    // ── A user's OWN image as a slice logo (drag & drop onto the icon well) ─────────────────────────
    // Unlike every other file in this cache, a user image CANNOT BE RE-FETCHED, so it gets its own name
    // prefix and is the one thing "Clear art cache" keeps (see ClearCache). It is also COPIED in rather
    // than referenced in place: a slice pointing at Downloads\logo.png would silently lose its art the
    // moment the user tidied that folder, and only installed-game slices have a heal to fall back on.

    public const string UserLogoPrefix = "usericon_";

    /// <summary>Longest edge kept when importing. The wheel decodes logos at 600px and the editor well
    /// draws at 67, so anything beyond this is invisible detail that would bloat every backup — one real
    /// cached logo here is 21200x5344, which is 450 MB decoded.</summary>
    private const int UserLogoMaxEdge = 1024;
    private const long UserLogoMaxBytes = 20L * 1024 * 1024;

    /// <summary>Ceiling on SOURCE pixel count, checked from the header before any decode. File size is no
    /// proxy for this: a real SteamGridDB logo already in the cache here is 4.8 MB on disk and
    /// 32766x22265 = 729 MEGAPIXELS, which is ~2.9 GB decoded to RGBA. Downsampled decoding normally keeps
    /// that in check, but it can't for an INTERLACED PNG — Adam7 has to be buffered whole — so the only
    /// reliable guard is to refuse the dimensions up front. 64 MP still allows anything sane (an 8K
    /// wallpaper is 33 MP).</summary>
    private const long UserLogoMaxPixels = 64_000_000;

    /// <summary>Outcome of importing a user image: the cached path, or an <see cref="Error"/> to show.</summary>
    public readonly record struct UserLogoImport(string? Path, string? Error);

    /// <summary>Copy a user-supplied image into the art cache as a slice logo, normalised to PNG (so the
    /// stored extension is honest whatever was dropped, transparency is preserved, and backups — which
    /// bundle png/jpg only — always include it). Named by content hash, so re-dropping the same picture
    /// reuses one file. Decoding is DOWNSAMPLED, never full-size: a multi-megapixel drop must not be able
    /// to exhaust memory.
    /// <para>An image with NO transparency is REJECTED: a slice masks its logo by ALPHA, so such an image
    /// can only ever paint as a solid block of the slice colour — never something the user wanted.
    /// Rejecting beats importing-with-a-warning because nothing is written and there's nothing to
    /// undo.</para></summary>
    public static UserLogoImport ImportUserLogo(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return new(null, Loc.T(UiText.Editor.LogoMissing));
            var info = new FileInfo(sourcePath);
            if (info.Length == 0) return new(null, Loc.T(UiText.Editor.LogoEmpty));
            if (info.Length > UserLogoMaxBytes)
                return new(null, Loc.F(UiText.Editor.LogoTooBig, info.Length / (1024 * 1024), UserLogoMaxBytes / (1024 * 1024)));

            // Header-only read first: we need the true dimensions to pick a decode scale WITHOUT decoding
            // the whole thing.
            int srcW, srcH;
            using (var probe = File.OpenRead(sourcePath))
            {
                var dec = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (dec.Frames.Count == 0) return new(null, Loc.T(UiText.Editor.LogoNotImage));
                srcW = dec.Frames[0].PixelWidth;
                srcH = dec.Frames[0].PixelHeight;
            }
            if (srcW <= 0 || srcH <= 0) return new(null, Loc.T(UiText.Editor.LogoNoSize));
            if ((long)srcW * srcH > UserLogoMaxPixels)
                return new(null, Loc.F(UiText.Editor.LogoTooManyPixels, srcW, srcH, (long)srcW * srcH / 1_000_000, UserLogoMaxPixels / 1_000_000));

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption   = BitmapCacheOption.OnLoad;          // read it now, release the file handle
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource     = new Uri(sourcePath);
            if (srcW >= srcH && srcW > UserLogoMaxEdge) bmp.DecodePixelWidth  = UserLogoMaxEdge;
            else if (srcH > srcW && srcH > UserLogoMaxEdge) bmp.DecodePixelHeight = UserLogoMaxEdge;
            bmp.EndInit();
            bmp.Freeze();

            // Checked BEFORE encoding or writing, so a rejected drop leaves nothing behind.
            if (IsFullyOpaque(bmp))
                return new(null, Loc.T(UiText.Editor.LogoNoTransparency));

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var mem = new MemoryStream();
            enc.Save(mem);
            var bytes = mem.ToArray();

            Directory.CreateDirectory(CacheDir);
            string name = UserLogoPrefix
                          + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..16]
                          + ".png";
            string dest = Path.Combine(CacheDir, name);
            if (!File.Exists(dest))
                AtomicFile.WriteAllBytes(dest, bytes);
            return new(dest, null);
        }
        catch (Exception ex)
        {
            // Anything the imaging stack rejects (unsupported/corrupt) lands here rather than crashing a drop.
            System.Diagnostics.Trace.WriteLine($"[Art] user logo import failed: {ex.Message}");
            return new(null, "Radiata couldn't read that image.");
        }
    }

    /// <summary>True when NO pixel is even slightly transparent — such an image can't work as a masked logo.</summary>
    private static bool IsFullyOpaque(BitmapSource src)
    {
        try
        {
            var bgra = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
            var buf = new byte[stride * h];
            bgra.CopyPixels(buf, stride, 0);
            for (int i = 3; i < buf.Length; i += 4)
                if (buf[i] < 250) return false;
            return true;
        }
        catch { return false; }   // can't tell → treat as fine, don't block a drop on an unclear check
    }

    /// <summary>True for an image the USER supplied (imported by <see cref="ImportUserLogo"/>).</summary>
    public static bool IsUserLogo(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.GetFileName(path).StartsWith(UserLogoPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Where the saved logo pick sits in <paramref name="urls"/>, or -1 when it isn't one of them
    /// (default stop, or a pre-fingerprint pick). The logo counterpart of
    /// <see cref="CandidateIndexOf"/>: it lets the Start cycle resume from the artwork on screen rather
    /// than from a position that may since have moved.</summary>
    public static int LogoCandidateIndexOf(InstalledGame game, IReadOnlyList<string> urls)
    {
        if (GameMetadata.SavedLogoKey(game) is not { Length: > 0 } key) return -1;
        for (int i = 0; i < urls.Count; i++)
            if (string.Equals(UrlKey(urls[i]), key, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // ── Candidate URL lists: disk-cached so the FIRST Select/Start press needs no API round-trip ────────────
    // Written by the fetch paths below; the background prefetch (ArtPrefetcher) refreshes entries older
    // than StaleAfter. Cycling always accepts a cached list regardless of age (stale > spinner).

    private static readonly TimeSpan UrlCacheStaleAfter = TimeSpan.FromDays(7);

    private static string CoverUrlCachePath(InstalledGame g) => Path.Combine(CacheDir, $"urls_covers_{SgdbCacheId(g)}.json");
    private static string LogoUrlCachePath(InstalledGame g)  => Path.Combine(CacheDir, $"urls_logos_{LogoCacheId(g)}.json");

    // ── RADIATA PICKER (dev-only) — delete these four with RadiataPicker.cs ────────────────────────
    internal static string PickerCoverUrlCacheFile(InstalledGame g) => CoverUrlCachePath(g);
    internal static string PickerLogoUrlCacheFile(InstalledGame g)  => LogoUrlCachePath(g);

    /// <summary>RADIATA PICKER: resolve the DEFAULT cover's source URL right now — the same image the
    /// cycle's stop 0 shows — so the export can pin today's default explicitly (SteamGridDB rankings
    /// shift over time; "default" is not a durable pick).</summary>
    internal static async Task<string?> PickerDefaultCoverUrlAsync(InstalledGame g)
    {
        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            var @base = await SgdbBaseAsync(g, key!).ConfigureAwait(false);
            return @base is null ? null : await SgdbDefaultUrlAsync(@base, key!).ConfigureAwait(false);
        }
        catch { return null; }
    }

    /// <summary>RADIATA PICKER: resolve the DEFAULT slice logo's source URL right now (best-ranked
    /// English candidate inside the slice aspect band — mirrors SgdbSliceLogoAsync), for the same
    /// durability reason.</summary>
    internal static async Task<string?> PickerDefaultSliceLogoUrlAsync(InstalledGame g)
    {
        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            string? @base = await SgdbLogoBaseAsync(g, key!).ConfigureAwait(false);
            return @base is null ? null : await SgdbSliceLogoUrlAsync($"{@base}?types=static", key!).ConfigureAwait(false);
        }
        catch { return null; }
    }

    /// <summary>RADIATA PICKER: resolve the DEFAULT logo's source URL right now (top white-styled logo,
    /// else the top of any style — mirrors SgdbLogoDownloadAsync), for the same durability reason.</summary>
    internal static async Task<string?> PickerDefaultLogoUrlAsync(InstalledGame g)
    {
        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            string? @base = await SgdbLogoBaseAsync(g, key!).ConfigureAwait(false);
            if (@base is null) return null;
            return await SgdbFirstUrlAsync($"{@base}?styles=white&types=static&limit=1", key!).ConfigureAwait(false)
                ?? await SgdbFirstUrlAsync($"{@base}?types=static&limit=1", key!).ConfigureAwait(false);
        }
        catch { return null; }
    }

    /// <summary>Both candidate lists are already on disk (fresh enough) — the prefetch sweep can skip
    /// its rate-limit delay for this game.</summary>
    public static bool HasFreshUrlCaches(InstalledGame game) =>
        IsFresh(CoverUrlCachePath(game)) && IsFresh(LogoUrlCachePath(game));

    private static bool IsFresh(string file)
    {
        try { return File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < UrlCacheStaleAfter; }
        catch { return false; }
    }

    private static IReadOnlyList<string>? ReadUrlCache(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file));
        }
        catch { return null; }
    }

    private static void WriteUrlCache(string file, IReadOnlyList<string> urls)
    {
        try { File.WriteAllText(file, JsonSerializer.Serialize(urls)); }
        catch { /* best effort — worst case the next call re-fetches */ }
    }

    /// <summary>SteamGridDB LOGO alternate URLs for Start cycling, ordered by score (best first), all
    /// styles — empty if no key / match / results. The DEFAULT logo's own image is EXCLUDED (the same
    /// white-preferred pick <see cref="GetLogoPathAsync"/> downloads — stop 0 of the cycle — so it never
    /// appears twice), and the list is capped at 2 (3 logos total in the cycle, plus the off stop).
    /// Disk-cached; <paramref name="refresh"/> forces a re-fetch of a stale cache (prefetch sweep).</summary>
    public static async Task<IReadOnlyList<string>> GetLogoCandidateUrlsAsync(InstalledGame game, bool refresh = false)
    {
        string cacheFile = LogoUrlCachePath(game);
        if ((!refresh || IsFresh(cacheFile)) && ReadUrlCache(cacheFile) is { } cached) return cached;

        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return Array.Empty<string>();
        try
        {
            string? @base = SteamAppId(game) is { } sid
                ? $"https://www.steamgriddb.com/api/v2/logos/steam/{sid}"
                : (await GameArtNameMatch.SgdbSearchIdAsync(game.Name, key!).ConfigureAwait(false) is { } gid
                    ? $"https://www.steamgriddb.com/api/v2/logos/game/{gid}" : null);
            var list = new List<string>();
            if (@base is not null)
            {
                var all = await SgdbUrlsAsync($"{@base}?types=static", key!).ConfigureAwait(false);
                // The default resolution = the top WHITE-styled logo, else the top of any style — mirror
                // SgdbLogoDownloadAsync so we exclude exactly what stop 0 shows.
                var def = await SgdbFirstUrlAsync($"{@base}?styles=white&types=static&limit=1", key!).ConfigureAwait(false)
                          ?? all.FirstOrDefault();
                var cur = CuratedArt.Get(game.Name)?.LogoUrl;   // curated default = the cycle's stop 0 too
                list = all.Where(u => u != def && u != cur).Take(2).ToList();
            }
            WriteUrlCache(cacheFile, list);   // cache genuine results, including genuine "none"
            return list;
        }
        catch { return ReadUrlCache(cacheFile) ?? Array.Empty<string>(); }   // transient → stale beats empty
    }

    /// <summary>Where a logo candidate caches. Keyed by the URL's fingerprint, NOT its position in the
    /// candidate list — that list refetches every <see cref="UrlCacheStaleAfter"/> and SteamGridDB re-ranks,
    /// so a position-keyed pick silently becomes a different logo. Mirrors
    /// <see cref="CandidateCachePath"/>; the filename IS the URL's identity.</summary>
    public static string LogoCandidateCachePath(InstalledGame game, string url) =>
        Path.Combine(CacheDir, $"logoalt_{LogoCacheId(game)}_{UrlKey(url)}.png");

    /// <summary>The pre-fingerprint name for a candidate at a given POSITION. Read-only legacy: a cache
    /// filled by an older build still holds these, and they're accepted only after a byte match (see
    /// <see cref="CuratedPicksExport"/>) or as the last resort in <see cref="GetGridLogoPathAsync"/>.</summary>
    internal static string LegacyLogoCandidatePath(InstalledGame game, int index) =>
        Path.Combine(CacheDir, $"logoalt_{LogoCacheId(game)}_{index}.png");

    /// <summary>Download a Start logo candidate into the cache and return its local path.</summary>
    public static async Task<string?> DownloadLogoCandidateAsync(InstalledGame game, string url, int index)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            string file = LogoCandidateCachePath(game, url);
            // An older build cached this same artwork under its position; adopt that file rather than
            // re-downloading identical bytes, then let the old name age out with the quota.
            if (!File.Exists(file) && File.Exists(LegacyLogoCandidatePath(game, index)))
                try { File.Copy(LegacyLogoCandidatePath(game, index), file); } catch { }
            if (!File.Exists(file))
            {
                var (bytes, _) = await FetchBytesAsync(url).ConfigureAwait(false);
                if (bytes is null) return null;
                if (!await WriteCacheFileAsync(file, bytes).ConfigureAwait(false)) return null;
            }
            return file;
        }
        catch { return null; }
    }

    /// <summary>The logo path the GRID should show for a game, honouring a saved Start alternate pick
    /// (falls back to the default resolution when the alternate isn't cached). Null = no logo.</summary>
    public static async Task<string?> GetGridLogoPathAsync(InstalledGame game)
    {
        // Fingerprint first: it survives a re-rank, which is exactly what the index cannot do.
        if (GameMetadata.SavedLogoKey(game) is { Length: > 0 } key)
        {
            string pinned = Path.Combine(CacheDir, $"logoalt_{LogoCacheId(game)}_{key}.png");
            if (File.Exists(pinned)) return pinned;
        }
        int idx = GameMetadata.LogoIndexFor(game);
        if (idx > 0)
        {
            string alt = LegacyLogoCandidatePath(game, idx - 1);   // pre-fingerprint pick
            if (File.Exists(alt)) return alt;
        }
        return await GetLogoPathAsync(game).ConfigureAwait(false);
    }

    /// <summary>Up to 10 SteamGridDB cover candidate URLs for cycling, ordered by SteamGridDB score (best
    /// first) and spanning ALL styles (not just the preferred one) — empty if no key / match / results.
    /// The auto-resolved DEFAULT cover is excluded (it's the cycle's separate candidate-0 stop, so it
    /// won't appear twice). Disk-cached; <paramref name="refresh"/> forces a re-fetch of a stale cache
    /// (prefetch sweep); the chosen image is fetched on demand.</summary>
    public static async Task<IReadOnlyList<string>> GetCoverCandidateUrlsAsync(InstalledGame game, bool refresh = false)
    {
        string cacheFile = CoverUrlCachePath(game);
        if ((!refresh || IsFresh(cacheFile)) && ReadUrlCache(cacheFile) is { } cached) return cached;

        var key = SteamGridDbKey;
        if (string.IsNullOrWhiteSpace(key)) return Array.Empty<string>();
        try
        {
            var @base = await SgdbBaseAsync(game, key!).ConfigureAwait(false);
            var list = new List<string>();
            if (@base is not null)
            {
                // All styles, ordered by score; portrait box art only; static (WPF can't animate APNG).
                var all    = await SgdbUrlsAsync($"{@base}?dimensions=600x900&types=static", key!).ConfigureAwait(false);
                var defUrl = await SgdbDefaultUrlAsync(@base, key!).ConfigureAwait(false);
                var curUrl = CuratedArt.Get(game.Name)?.CoverUrl;   // curated default = the cycle's stop 0 too
                list = all.Where(u => u != defUrl && u != curUrl).Take(10).ToList();
            }
            WriteUrlCache(cacheFile, list);   // cache genuine results, including genuine "none"
            return list;
        }
        catch { return ReadUrlCache(cacheFile) ?? Array.Empty<string>(); }   // transient → stale beats empty
    }

    /// <summary>Warm everything the Select/Start cycles touch first for one game: the default cover + logo, both
    /// candidate URL lists (disk-cached above), the first 3 cover alternates and every logo alternate
    /// (≤2). After this, the first several cycle presses are all cache hits. Called by the background
    /// prefetch sweep (ArtPrefetcher) — never from an interactive path.</summary>
    public static async Task PrefetchGameArtAsync(InstalledGame game, bool refreshStale = false)
    {
        Directory.CreateDirectory(CacheDir);
        await GetCachedPathAsync(game).ConfigureAwait(false);          // default cover (tile + cycle stop 0)
        var covers = await GetCoverCandidateUrlsAsync(game, refreshStale).ConfigureAwait(false);
        for (int i = 0; i < Math.Min(3, covers.Count); i++)
            await DownloadCandidateAsync(game, covers[i]).ConfigureAwait(false);
        await GetLogoPathAsync(game).ConfigureAwait(false);            // default logo (overlay + Start stop 0)
        await GetSliceLogoPathAsync(game).ConfigureAwait(false);       // squarest logo (wheel slices)
        var logos = await GetLogoCandidateUrlsAsync(game, refreshStale).ConfigureAwait(false);
        for (int i = 0; i < logos.Count; i++)
            await DownloadLogoCandidateAsync(game, logos[i], i).ConfigureAwait(false);
    }

    /// <summary>Grid image URLs from a SteamGridDB grids query, ordered by score (best first).</summary>
    private static async Task<IReadOnlyList<string>> SgdbUrlsAsync(string url, string key)
    {
        using var doc = await SgdbJsonAsync(url, key).ConfigureAwait(false);
        var scored = new List<(double score, string url)>();
        if (doc?.RootElement.TryGetProperty("data", out var data) == true && data.ValueKind == JsonValueKind.Array)
            foreach (var e in data.EnumerateArray())
                if (e.TryGetProperty("url", out var u) && u.GetString() is { } s)
                    scored.Add((SgdbScore(e), s));
        return scored.OrderByDescending(t => t.score).Select(t => t.url).ToList();
    }

    /// <summary>A grid's rank signal: SteamGridDB's "score" if present, else upvotes − downvotes.</summary>
    private static double SgdbScore(JsonElement e)
    {
        if (e.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number && sc.TryGetDouble(out var v))
            return v;
        int up   = e.TryGetProperty("upvotes",   out var u) && u.TryGetInt32(out var uv) ? uv : 0;
        int down = e.TryGetProperty("downvotes", out var d) && d.TryGetInt32(out var dv) ? dv : 0;
        return up - down;
    }

    /// <summary>Download a chosen candidate URL into the cache and return its local path (cached per index).</summary>
    public static async Task<string?> DownloadCandidateAsync(InstalledGame game, string url)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            string file = CandidateCachePath(game, url);
            if (!File.Exists(file))
            {
                // Via FetchBytesAsync, not a direct Http.GetAsync call, so this shares the MaxArtBytes
                // Content-Length refusal instead of buffering a 33 MB body before finding out it was junk.
                var (bytes, _) = await FetchBytesAsync(url).ConfigureAwait(false);
                if (bytes is null) return null;
                if (!await WriteCacheFileAsync(file, bytes).ConfigureAwait(false)) return null;
            }
            return file;
        }
        catch { return null; }
    }

    /// <summary>Cache path for one cover alternate, derived from the image URL.
    ///
    /// Keyed by SgdbCacheId (like the URL list, CoverUrlCachePath) — NOT LogoCacheId — so changing the
    /// cover style re-fetches the list AND misses these files instead of serving a stale alternate.
    /// The second component is a hash of the URL, NOT the candidate's position in the list: SteamGridDB
    /// re-ranks, and the list is refetched every 7 days, so a position-keyed file would let the user's
    /// SAVED cover silently become a different image whenever the ordering moved under it. A URL-keyed
    /// name pins the pick to the actual artwork, and lets the grid find the saved pick's position in a
    /// freshly fetched list (see CandidateIndexOf).</summary>
    public static string CandidateCachePath(InstalledGame game, string url) =>
        Path.Combine(CacheDir, $"sgdbalt_{SgdbCacheId(game)}_{UrlKey(url)}.jpg");

    /// <summary>The fingerprint a pick is pinned by — see <see cref="CoverPick.LogoKey"/>.</summary>
    public static string UrlFingerprint(string url) => UrlKey(url);

    private static string UrlKey(string url) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(url)))[..16];

    /// <summary>Where the game's CURRENTLY SAVED cover sits in <paramref name="urls"/>, or -1 when the
    /// saved pick isn't one of them (the auto default, a flat colour, or a legacy position-keyed file).
    /// Lets the Select cycle resume from what's on screen instead of always restarting at stop 0, so the
    /// first press after reopening a game neither repeats nor rewinds.</summary>
    public static int CandidateIndexOf(InstalledGame game, IReadOnlyList<string> urls)
    {
        var saved = GameMetadata.CoverOverride(game)?.Path;
        if (string.IsNullOrEmpty(saved)) return -1;
        for (int i = 0; i < urls.Count; i++)
            if (string.Equals(CandidateCachePath(game, urls[i]), saved, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>The game's saved cover file ("" / null = the auto-resolved default).</summary>
    public static string? SavedCoverPath(InstalledGame game) => GameMetadata.CoverOverride(game)?.Path;

    /// <summary>A tiny solid-colour PNG (cached, shared across games) used as a flat cover option.
    /// ArtBrushConverter's UniformToFill stretches it to fill the tile.</summary>
    public static string? FlatColorPath(string hex)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            string file = Path.Combine(CacheDir, $"flat_{hex.TrimStart('#')}.png");
            if (!File.Exists(file))
            {
                var c = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                const int n = 8;
                var px = new byte[n * n * 4];
                for (int i = 0; i < n * n; i++) { px[i * 4] = c.B; px[i * 4 + 1] = c.G; px[i * 4 + 2] = c.R; px[i * 4 + 3] = 255; }
                var bmp = BitmapSource.Create(n, n, 96, 96, PixelFormats.Bgra32, null, px, n * 4);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using var fs = File.Create(file);
                enc.Save(fs);
            }
            return file;
        }
        catch { return null; }
    }

    /// <summary>Delete all cached cover-art files (steam_*.jpg / sgdb_*.jpg + ".miss" markers) so the
    /// next browse re-fetches and previously-missed art is retried. Playnite's own covers (referenced by
    /// their own path, not stored here) are untouched. Returns the number of files removed.</summary>
    /// <param name="keepPaths">Slice logo paths still in use. Any USER-SUPPLIED image among them survives
    /// the clear: everything else here re-downloads on demand, but a user's own artwork is gone for good,
    /// and "clear the art cache" must not mean "destroy my art". User images NOT in this list are orphans
    /// (their slice changed or went away) and are cleared like anything else. Pass null to keep every user
    /// image, which is the safe default when the caller can't enumerate the slices.</param>
    public static int ClearCache(IReadOnlyCollection<string>? keepPaths = null)
    {
        try
        {
            // A saved COVER pick points into the cache we're about to clear, so drop each pick's cover path
            // — but KEEP its LOGO choice (Start), which is only an index (the logo art re-fetches), not a
            // cache path.
            GameMetadata.ClearAllCoverPaths();
            if (!Directory.Exists(CacheDir)) return 0;
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (keepPaths is not null)
                foreach (var p in keepPaths)
                    if (IsUserLogo(p)) keep.Add(Path.GetFileName(p));
            int n = 0;
            foreach (var f in Directory.EnumerateFiles(CacheDir))
            {
                string name = Path.GetFileName(f);
                if (IsUserLogo(name) && (keepPaths is null || keep.Contains(name))) continue;
                try { File.Delete(f); n++; } catch { /* skip locked/removed */ }
            }
            ArtPrefetcher.Reset();   // the sweep's per-session "done" record now points at deleted files
            InvalidateCacheSizeEstimate();   // the running total we were budgeting against is now wrong
            return n;
        }
        catch { return 0; }
    }

    /// <summary>Delete only the ".miss" markers (failed-lookup records) so previously-missed art is
    /// retried on the next browse — WITHOUT touching already-cached covers/logos or the user's saved cover
    /// picks. Use after a fix that lets a failed search succeed. Returns markers removed.</summary>
    public static int RetryMissedArt()
    {
        try
        {
            if (!Directory.Exists(CacheDir)) return 0;
            int n = 0;
            foreach (var f in Directory.EnumerateFiles(CacheDir, "*.miss"))
                try { File.Delete(f); n++; } catch { /* skip locked/removed */ }
            ArtPrefetcher.Reset();   // let the next sweep re-attempt the games behind those markers
            return n;
        }
        catch { return 0; }
    }

    /// <summary>Load an image file as a frozen ImageSource (e.g. a slice's cover-art IconPath).</summary>
    public static ImageSource? LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        // Keyed on the file's stamp so a wheel with a cover or logo slice hands back the SAME frozen image on
        // every open: the wheel's resting-slice bakes and every per-image bake cache key on the ImageSource's
        // identity, so a fresh decode per open was a disk read, a decode and a full rebake of every slice on
        // every summon. One stat per open instead; a rewritten file (new stamp) decodes again.
        FileInfo info;
        try { info = new FileInfo(path); if (!info.Exists) return null; }
        catch { return null; }
        var stamp = (info.LastWriteTimeUtc.Ticks, info.Length);
        lock (FileImages)
        {
            if (FileImages.TryGetValue(path, out var hit) && hit.Stamp == stamp) return hit.Image;
        }
        var img = LoadFrozen(path);
        lock (FileImages)
        {
            if (FileImages.Count >= 256) FileImages.Clear();
            FileImages[path] = (stamp, img);
        }
        return img;
    }

    private static readonly Dictionary<string, ((long, long) Stamp, ImageSource? Image)> FileImages =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Header-only check that an image's pixel count is safe to decode. False (and a trace line)
    /// for anything over <see cref="UserLogoMaxPixels"/>, or for a file whose header won't read at all —
    /// in which case the decode below would fail anyway.</summary>
    private static bool WithinPixelBudget(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            using var fs = File.OpenRead(path);
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (dec.Frames.Count == 0) return false;
            width = dec.Frames[0].PixelWidth;
            height = dec.Frames[0].PixelHeight;
            long px = (long)width * height;
            if (px <= UserLogoMaxPixels) return true;
            System.Diagnostics.Trace.WriteLine(
                $"[Art] refusing to decode {Path.GetFileName(path)} — {px / 1_000_000} MP exceeds the "
                + $"{UserLogoMaxPixels / 1_000_000} MP budget");
            return false;
        }
        catch { return false; }
    }

    private static ImageSource? LoadFrozen(string path)
    {
        try
        {
            // Dimension gate BEFORE decoding. DecodePixelWidth below normally bounds the cost, but it
            // cannot for an INTERLACED PNG (Adam7 must be buffered whole), so a config pointing LogoPath or
            // IconPath at a 700-megapixel image would try for gigabytes — synchronously, on the UI thread,
            // during wheel open. Our own cached files are re-encoded under 16 MP on write, so this only
            // ever rejects something the user (or a restored backup) pointed us at. Same ceiling as
            // ImportUserLogo, deliberately: one number to reason about.
            if (!WithinPixelBudget(path, out int width, out int height)) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption    = BitmapCacheOption.OnLoad;     // load now, don't lock the file
            bmp.CreateOptions  = BitmapCreateOptions.IgnoreColorProfile;
            // Bound the longest edge without enlarging narrow images or exceeding the source pixel budget.
            if (width >= height && width > 600) bmp.DecodePixelWidth = 600;
            else if (height > 600) bmp.DecodePixelHeight = 600;
            bmp.UriSource      = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();                                       // safe to use off the UI thread
            return bmp;
        }
        catch
        {
            // An undecodable file (truncated by an old non-atomic write, disk corruption) would
            // otherwise be a permanent bad hit — File.Exists keeps accepting it and nothing ever
            // re-downloads. Drop OUR cache copies so the next resolve re-fetches; never touch
            // Playnite/user-supplied paths (LoadFromFile routes those through here too).
            try
            {
                if (Path.GetDirectoryName(path) is { } dir &&
                    string.Equals(dir, CacheDir, StringComparison.OrdinalIgnoreCase))
                    File.Delete(path);
            }
            catch { /* locked / already gone */ }
            return null;
        }
    }
}
