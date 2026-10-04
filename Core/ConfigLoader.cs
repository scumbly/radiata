using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// Loads config.json from AppData, writes a default on first run, and fires
/// <see cref="Reloaded"/> whenever the file changes (debounced).
/// </summary>
public sealed class ConfigLoader : IDisposable
{
    // %APPDATA%\Radiata\config.json
    public static readonly string ConfigPath = Path.Combine(AppPaths.AppDataDir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = true,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Func<string, string> _protectBackup;
    private readonly Func<string, string> _readBackup;
    private readonly SynchronizationContext _sync;   // UI thread to marshal Reloaded onto
    private          FileSystemWatcher?       _watcher;
    private          Timer?                   _debounce;
    private readonly object _watchGate = new();
    private volatile bool _disposed;

    public AppConfig Current { get; private set; }
    public string? LastWriteError { get; private set; }

    /// <summary>Fires on the UI thread after a debounced file change with the new config.</summary>
    public event Action<AppConfig>? Reloaded;

    /// <param name="sync">The UI synchronization context (Reloaded is posted here).</param>
    public ConfigLoader(SynchronizationContext sync, Func<string, string>? protectBackup = null, Func<string, string>? readBackup = null)
    {
        _protectBackup = protectBackup ?? (text => text);
        _readBackup = readBackup ?? (text => text);
        _sync   = sync;
        Current = LoadOrCreateDefault();
    }

    public void StartWatching()
    {
        // 300 ms idle after the last change event → reload (portable timer; reload marshalled to UI)
        lock (_watchGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_watcher is not null) return;
            _debounce = new Timer(_ =>
            {
                if (_disposed) return;
                try { _sync.Post(_ => { if (!_disposed) Reload(); }, null); }
                catch (Exception ex) { Trace.WriteLine($"[Config] Reload dispatch failed: {ex.Message}"); }
            }, null, Timeout.Infinite, Timeout.Infinite);

        var dir  = Path.GetDirectoryName(ConfigPath)!;
        var file = Path.GetFileName(ConfigPath);

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        // Some editors do atomic saves (write + rename), so listen to Created too
        _watcher.Changed += ScheduleReload;
        _watcher.Created += ScheduleReload;
        _watcher.Renamed += ScheduleReload;
        }
    }

    private void ScheduleReload(object sender, FileSystemEventArgs e)
    {
        lock (_watchGate)
            if (!_disposed) _debounce?.Change(300, Timeout.Infinite);
    }

    private void Reload()
    {
        if (_disposed) return;
        // Drop the watcher's echo of our own write (WriteConfig already applied it synchronously).
        if (IsEchoOfOurWrite()) { Trace.WriteLine("[Config] watcher echo of our own write — skipped."); return; }
        var config = TryLoad();
        if (config is null) return;
        Current = config;
        Trace.WriteLine("[Config] Reloaded from disk.");
        Reloaded?.Invoke(config);
    }

    /// <summary>True when config.json on disk is byte-identical to our last write, i.e. the watcher is
    /// echoing us rather than reporting an external edit. Clears the marker on a match so a later external
    /// edit still reloads even if the user reverts the file to exactly what we wrote. Any read failure
    /// answers false — reloading redundantly is harmless; skipping a real external edit is not.</summary>
    private static bool IsEchoOfOurWrite()
    {
        var expected = Volatile.Read(ref _lastWrittenJson);
        if (expected is null) return false;
        try
        {
            if (!string.Equals(File.ReadAllText(ConfigPath), expected, StringComparison.Ordinal)) return false;
            Volatile.Write(ref _lastWrittenJson, null);
            return true;
        }
        catch (Exception ex)
        {
            // Throttled: a reload storm re-enters this per watcher event and would flood the log.
            var now = Environment.TickCount64;
            if (now - _lastEchoReadFailLog >= 30_000)
            {
                _lastEchoReadFailLog = now;
                Trace.WriteLine($"[Config] echo check could not read config.json: {ex.Message}");
            }
            return false;
        }
    }

    private static long _lastEchoReadFailLog = -30_000;

    // ── File I/O ──────────────────────────────────────────────────────────────

    private AppConfig LoadOrCreateDefault()
    {
        if (!File.Exists(ConfigPath))
        {
            // Silent write, not WriteConfig: this runs inside the constructor, and WriteConfig posts
            // a Reloaded that would fire once with the default config after handlers subscribe.
            TryWriteAtomic(AppConfig.Default);
            Trace.WriteLine($"[Config] Created default config at {ConfigPath}");
        }
        var loaded = TryLoad();
        if (loaded is null && File.Exists(ConfigPath))
        {
            _unreadablePreserved = BackUpUnreadableConfig();
            // Before falling back to shipped defaults — which means the user opens Radiata to empty wheels
            // and none of their settings — try the .bak written after the last successful load, so recovery
            // is automatic instead of "find the timestamped copy and rename it by hand".
            if (LoadKnownGood() is { } good)
            {
                Trace.WriteLine("[Config] Recovered from the last-known-good copy — the unreadable file was "
                                + "kept aside. Nothing is overwritten until the next save.");
                return good;
            }
        }
        else if (loaded is not null && loaded.SavedByVersion != CurrentAppVersion)
        {
            VersionChangedAtLoad = true;   // the re-stamp below erases the evidence — record it first
            BackUpBeforeVersionChange(loaded.SavedByVersion);
            // Persist the new stamp now rather than waiting for the user's next settings change:
            // the stamp is what makes the backup one-per-version, and a session that never saves
            // anything (the common case — launch, use a wheel, quit) would otherwise re-snapshot an
            // identical file on every single launch. Silent write: no Current/Reloaded side effects,
            // since we're inside the constructor and the caller assigns Current from our return.
            TryWriteAtomic(loaded);
        }
        // Refresh the last-known-good copy from whatever just parsed — it is by definition good.
        if (loaded is not null) SaveKnownGood();
        return loaded ?? AppConfig.Default;
    }

    /// <summary>The last-known-good copy: a protected snapshot of the original config.json text after it parsed
    /// cleanly at startup. Deliberately not a re-serialization of the in-memory object — copying the file
    /// preserves anything the current build doesn't model (a key from a newer version, a hand-added comment),
    /// so recovering can't quietly strip it.</summary>
    private static string KnownGoodPath => ConfigPath + ".bak";

    private void SaveKnownGood()
    {
        try
        {
            // Skip the copy when it would be identical — this runs on every launch and there's no reason to
            // touch the disk (or churn a backup tool's view of the folder) for an unchanged file.
            if (File.Exists(KnownGoodPath)
                && File.GetLastWriteTimeUtc(KnownGoodPath) >= File.GetLastWriteTimeUtc(ConfigPath)) return;
            WriteProtectedBackup(KnownGoodPath);
        }
        catch (Exception ex) { Trace.WriteLine($"[Config] known-good copy failed ({ex.Message}) — continuing."); }
    }

    /// <summary>Parse the last-known-good copy, or null if there isn't one / it doesn't parse either.</summary>
    private AppConfig? LoadKnownGood()
    {
        try
        {
            if (!File.Exists(KnownGoodPath)) return null;
            return Sanitize(JsonSerializer.Deserialize<AppConfig>(_readBackup(File.ReadAllText(KnownGoodPath)), JsonOpts));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Config] last-known-good copy is also unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>The config on disk was last saved by a different build than the one now running —
    /// captured at load, because the loader immediately re-stamps the file (so comparing
    /// <see cref="AppConfig.SavedByVersion"/> later always reads "unchanged"). The app reads this as one
    /// of its post-update-launch signals.</summary>
    public static bool VersionChangedAtLoad { get; private set; }

    /// <summary>Running app version (InformationalVersion of the entry exe, +suffix stripped) —
    /// compared against <see cref="AppConfig.SavedByVersion"/> at startup.</summary>
    public static readonly string CurrentAppVersion =
        (System.Reflection.Assembly.GetEntryAssembly()
            ?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "0")
        .Split('+')[0];

    private const int MaxVersionBackups = 10;

    /// <summary>The config was written by a different app version (upgrade, downgrade, or a pre-stamp
    /// file): snapshot it to backups\ before this session's first save rewrites it under the new
    /// version's rules. Best-effort; startup proceeds either way. Keeps the newest
    /// <see cref="MaxVersionBackups"/> snapshots.</summary>
    private void BackUpBeforeVersionChange(string? oldVersion)
    {
        try
        {
            var dir = Path.Combine(AppPaths.AppDataDir, "backups");
            Directory.CreateDirectory(dir);
            var tag = string.IsNullOrWhiteSpace(oldVersion) ? "unstamped"
                : string.Concat(oldVersion.Split(Path.GetInvalidFileNameChars()));   // version comes from the file = untrusted
            var backup = Path.Combine(dir, $"config-{tag}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            WriteProtectedBackup(backup);
            Trace.WriteLine($"[Config] App version changed ({oldVersion ?? "unstamped"} → {CurrentAppVersion}) — snapshotted config to {backup}");

            foreach (var stale in Directory.GetFiles(dir, "config-*.json")
                                           .OrderByDescending(File.GetLastWriteTimeUtc)
                                           .Skip(MaxVersionBackups))
                File.Delete(stale);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Config] Version-change backup failed ({ex.Message}) — continuing without it.");
        }
    }

    /// <summary>Copy an unreadable config.json to a timestamped sibling before falling back to defaults:
    /// the next save overwrites config.json, so without this the user's only copy is destroyed.
    /// Defaults may still be displayed on failure, but writes remain blocked until preservation succeeds.</summary>
    private bool BackUpUnreadableConfig()
    {
        var backup = Path.Combine(AppPaths.AppDataDir,
                                  $"config-corrupt-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        try
        {
            WriteProtectedBackup(backup);
            Trace.WriteLine($"[Config] UNREADABLE config.json — copied to {backup}; loading defaults. " +
                            "Use Settings Restore to import the backup; it may be protected for this Windows account.");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Config] UNREADABLE config.json and the backup copy failed ({ex.Message}) — " +
                            "loading defaults; saving is blocked until the original can be preserved.");
            return false;
        }
    }

    private void WriteProtectedBackup(string destination)
    {
        string content = _protectBackup(File.ReadAllText(ConfigPath));
        AtomicFile.WriteAllText(destination, content);
    }

    private AppConfig? TryLoad()
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return Sanitize(JsonSerializer.Deserialize<AppConfig>(json, JsonOpts));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Config] Failed to load: {ex.Message}");
            return null;
        }
    }

    /// <summary>Parse config JSON (e.g. a user-chosen backup file) with the same options as the live
    /// config, so Restore can validate before overwriting. Null if the text isn't valid config JSON.</summary>
    public static AppConfig? TryParse(string json)
    {
        try { return Sanitize(JsonSerializer.Deserialize<AppConfig>(json, JsonOpts)); }
        catch { return null; }
    }

    // ── Sanitise on read ─────────────────────────────────────────────────────────
    // config.json is a plain text file the user can edit, and a restored "backup" is a file a stranger
    // produced. Deserialization is the only place every route into the app meets, so the invariants the
    // renderer relies on get established here rather than at each of the dozens of use sites.
    //
    // These ranges are authoritative — there is no editor-side mirror to keep in sync. FadeMs/DriftPx/
    // StickyMs and the D-pad repeat timings have no Settings UI at all, so this is their only gate. ObsPort
    // (the OBS setup window's port box range-checks 1-65535) and MixBalance (the D-pad mixer in
    // App.xaml.cs steps within 0-100) each have an in-app writer that must stay consistent with the range
    // here; TestHarness group `pairs` (T_Consistency) fails when either drifts. Anything corrected is
    // traced, because silently rewriting someone's file is worse than saying so.

    /// <summary>Hard ceiling on slices per wheel. The UI tops out at 12 (SystemConfig.MaxSlicesPerWheel), so this
    /// only trims a hand-edited file — but OnRender loops the array with no cap, so a 10,000-slice wheel
    /// meant 10,000 wedges of geometry and text per frame.</summary>
    private const int MaxSlicesPerWheelHardCap = 32;

    private static AppConfig? Sanitize(AppConfig? cfg)
    {
        if (cfg is null) return null;
        var sys = cfg.System ?? new SystemConfig();

        int Clamp(int v, int lo, int hi, string name)
        {
            int c = Math.Clamp(v, lo, hi);
            if (c != v) Trace.WriteLine($"[Config] {name} {v} out of range — clamped to {c}");
            return c;
        }

        // NaN/Infinity would survive Math.Clamp and poison the layout maths. Standard JSON can't express
        // either (a "NaN" string fails to deserialize at all, so TryParse already returns null), but the
        // guard is one comparison and stays correct if the serializer options ever change.
        double clampedDrift = double.IsFinite(sys.DriftPx) ? Math.Clamp(sys.DriftPx, 0, 400) : 150;
        if (clampedDrift != sys.DriftPx)
            Trace.WriteLine($"[Config] DriftPx {sys.DriftPx} out of range — using {clampedDrift}");

        var wheelA = SanitizeWheel(cfg.WheelA, nameof(cfg.WheelA));
        var wheelB = SanitizeWheel(cfg.WheelB, nameof(cfg.WheelB));
        return new AppConfig
        {
            WheelA = wheelA,
            WheelB = wheelB,
            ActionColors = cfg.ActionColors ?? new(),
            ActionIcons  = cfg.ActionIcons  ?? new(),
            CustomColors = cfg.CustomColors?.Where(c => c is not null).ToArray() ?? [],
            SavedByVersion = cfg.SavedByVersion,
            // SliceThicknessRule on the read path covers every wheel-write this process didn't make: the
            // upgrade from the maxSlices era (where thick + 12 slices was storable), a hand-edited file, and
            // a restored backup (Restore writes the parsed file raw and restarts — this is where its rule
            // pass actually happens). In-app writes apply the rule at write time, so this is normally a
            // no-op; it's also idempotent, so double application is harmless.
            System = SliceThicknessRule.Apply(sys, wheelA.Length, wheelB.Length) with
            {
                FadeMs   = Clamp(sys.FadeMs,   0, 2000, nameof(sys.FadeMs)),
                DriftPx  = clampedDrift,
                StickyMs = Clamp(sys.StickyMs, 0, 1000, nameof(sys.StickyMs)),
                // A 0 ms repeat interval sets DispatcherTimer.Interval = 0, which re-queues on every
                // dispatcher pass — it saturates the UI thread and hammers the volume API while held.
                DpadVolumeRepeatDelayMs    = Clamp(sys.DpadVolumeRepeatDelayMs,    100, 2000, nameof(sys.DpadVolumeRepeatDelayMs)),
                DpadVolumeRepeatIntervalMs = Clamp(sys.DpadVolumeRepeatIntervalMs,  20, 1000, nameof(sys.DpadVolumeRepeatIntervalMs)),
                MixBalance = Clamp(sys.MixBalance, 0, 100, nameof(sys.MixBalance)),
                ObsPort    = Clamp(sys.ObsPort,    1, 65535, nameof(sys.ObsPort)),
                SafeModeApps = sys.SafeModeApps?.Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Path))
                                               .ToList() ?? [],
                // An unknown language (hand-edited file, or a backup from a build that shipped more
                // languages) would leave the picker with nothing selected — normalise to English.
                Language = sys.Language is null ? null : HelpLocalization.Normalize(sys.Language),
                // Materials are canonicalized here, the single entry point: a dozen consumers compare the
                // config string raw (status toast, slice-editor icon well, onboarding Material tiles), so
                // normalizing on read makes all of them right by construction — no per-call-site normalize
                // calls are needed. Materials.Normalize is total (every legacy token has an arm, unknown →
                // Pearl), and because the canonical value is what the next config save writes back, this is
                // the legacy-token migration. It's independent of SliceThicknessRule.Apply above (that only
                // touches SliceThickness/ThickAutoDemoted), so composing them here can't conflict.
                SliceMaterial    = Materials.Normalize(sys.SliceMaterial),
                GameGridMaterial = Materials.Normalize(sys.GameGridMaterial),
                // A custom token that can't resolve renders as Pearl, but the stored choice is kept so a
                // build or run that can resolve it finds it again (SystemConfig.HeldSliceMaterial).
                HeldSliceMaterial    = Unresolved(sys.SliceMaterial),
                HeldGameGridMaterial = Unresolved(sys.GameGridMaterial),
            },
        };
    }

    /// <summary>The trimmed stored token when it names a custom material that isn't registered, else null.</summary>
    private static string? Unresolved(string? stored)
    {
        var t = stored?.Trim();
        return t is not null && t.StartsWith(MaterialPackage.TokenPrefix, StringComparison.OrdinalIgnoreCase)
                            && t.Length > MaterialPackage.TokenPrefix.Length && !Materials.IsValid(t.ToLowerInvariant())
            ? t : null;
    }

    private static AppConfig WithSystem(AppConfig config, SystemConfig sys) => new()
    {
        WheelA = config.WheelA, WheelB = config.WheelB,
        ActionColors = config.ActionColors, ActionIcons = config.ActionIcons, CustomColors = config.CustomColors,
        SavedByVersion = config.SavedByVersion,
        System = sys,
    };

    /// <summary>The config as it goes to disk: a material still showing the Pearl that stood in for an
    /// unresolved custom token is written as that token again.</summary>
    public static AppConfig ForDisk(AppConfig config)
    {
        var sys = config.System;
        bool slice = sys.HeldSliceMaterial is not null && sys.SliceMaterial == Materials.GlossLight;
        bool grid  = sys.HeldGameGridMaterial is not null && sys.GameGridMaterial == Materials.GlossLight;
        if (!slice && !grid) return config;
        return WithSystem(config, sys with
        {
            SliceMaterial    = slice ? sys.HeldSliceMaterial! : sys.SliceMaterial,
            GameGridMaterial = grid ? sys.HeldGameGridMaterial! : sys.GameGridMaterial,
        });
    }

    /// <summary>A material that has moved off Pearl was chosen, so the token it stood in for is released.</summary>
    public static AppConfig ReleaseChosen(AppConfig config)
    {
        var sys = config.System;
        bool slice = sys.HeldSliceMaterial is not null && sys.SliceMaterial != Materials.GlossLight;
        bool grid  = sys.HeldGameGridMaterial is not null && sys.GameGridMaterial != Materials.GlossLight;
        if (!slice && !grid) return config;
        return WithSystem(config, sys with
        {
            HeldSliceMaterial    = slice ? null : sys.HeldSliceMaterial,
            HeldGameGridMaterial = grid ? null : sys.HeldGameGridMaterial,
        });
    }

    /// <summary>Drop null array elements and cap the count. A <c>"wheelA": [null]</c> deserializes to an
    /// array holding a null, and the first thing the icon pass does is assign a property on it — an
    /// NRE during wheel open, i.e. fatal and repeated forever because it's persisted.</summary>
    private static WheelSlice[] SanitizeWheel(WheelSlice[]? slices, string name)
    {
        if (slices is null || slices.Length == 0) return [];
        var clean = slices.Where(s => s is not null).ToArray();
        if (clean.Length != slices.Length)
            Trace.WriteLine($"[Config] {name}: dropped {slices.Length - clean.Length} null slice(s)");
        if (clean.Length > MaxSlicesPerWheelHardCap)
        {
            Trace.WriteLine($"[Config] {name}: {clean.Length} slices exceeds the {MaxSlicesPerWheelHardCap} cap — keeping the first {MaxSlicesPerWheelHardCap}");
            clean = clean[..MaxSlicesPerWheelHardCap];
        }
        return clean;
    }

    /// <summary>Stamp + serialize + atomically replace config.json. Throws on failure; no side effects
    /// (Current / Reloaded untouched) so the load path can persist a version stamp during construction.
    ///
    /// Atomic write: serialize to a unique sibling temp, then replace config.json in one move. A bare
    /// File.WriteAllText truncates-then-writes in place, so a crash / power loss / full disk mid-write
    /// would leave config.json truncated — all wheels and settings lost. Move-with-overwrite on the same
    /// volume is atomic (NTFS), so config.json is only ever the whole old file or the whole new one.
    /// The unique temp name avoids colliding with a concurrent write; the watcher filters to
    /// "config.json" so the temp never triggers a spurious reload. Not <see cref="AtomicFile"/>: this
    /// flushes the temp file to disk before the move, which that shared helper doesn't do.</summary>
    private static void WriteAtomic(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        // Stamp every save (see AppConfig.SavedByVersion). This mutates the caller's object — deliberate,
        // so the in-memory config matches disk; note it also stamps the shared AppConfig.Default instance
        // on the first-run write, which is harmless (nothing compares Default's stamp).
        config.SavedByVersion = CurrentAppVersion;
        var json = JsonSerializer.Serialize(ForDisk(config), JsonOpts);
        var tmp = ConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        finally { try { File.Delete(tmp); } catch { /* Only this write's unique temporary file. */ } }
        // Remember the exact text we just wrote so the watcher's echo of our own write can be dropped
        // (see _lastWrittenJson). Set only after the move succeeds — a failed write must not suppress
        // the reload of whatever is actually on disk.
        Volatile.Write(ref _lastWrittenJson, json);
    }

    /// <summary>The exact JSON text of our most recent successful write. <see cref="WriteConfig"/> already
    /// updates <see cref="Current"/> and raises <see cref="Reloaded"/> synchronously, and the
    /// FileSystemWatcher then fires ~300 ms later for that same write — without this echo check every save
    /// would reload and re-raise twice, re-running every Reloaded handler for no change. Text, not a
    /// timestamp: it's exact, and an external edit that happens to produce byte-identical content is a
    /// no-op anyway.</summary>
    private static string? _lastWrittenJson;

    /// <summary>WriteAtomic, but a failure is traced and swallowed — used for the version-stamp write,
    /// where losing the stamp costs one redundant backup next launch, not user data.</summary>
    private static void TryWriteAtomic(AppConfig config)
    {
        try { WriteAtomic(config); }
        catch (Exception ex) { Trace.WriteLine($"[Config] Version-stamp write failed: {ex.Message}"); }
    }

    private bool _unreadablePreserved = true;

    public bool WriteConfig(AppConfig config)
    {
        if (_disposed) { LastWriteError = "Configuration loader is closed."; return false; }
        if (!_unreadablePreserved && !(_unreadablePreserved = BackUpUnreadableConfig()))
        {
            LastWriteError = "The unreadable settings file could not be backed up. Restore access to the settings folder and retry.";
            return false;
        }
        config = ReleaseChosen(config);
        try { WriteAtomic(config); }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Config] WRITE FAILED — config NOT saved to {ConfigPath}: {ex.Message}");
            LastWriteError = ex.Message;
            return false;
        }

        // A notification failure cannot undo a durable write or report that committed assets need rollback.
        LastWriteError = null;
        Current = config;
        try { _sync.Post(_ => { if (!_disposed) Reloaded?.Invoke(config); }, null); }
        catch (Exception ex) { Trace.WriteLine($"[Config] Saved, but reload notification failed: {ex.Message}"); }
        return true;
    }

    public void Dispose()
    {
        lock (_watchGate)
        {
            _disposed = true;
            _watcher?.Dispose();
            _debounce?.Dispose();
            _watcher = null;
            _debounce = null;
        }
    }
}
