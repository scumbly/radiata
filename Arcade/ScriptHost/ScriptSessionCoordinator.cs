using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ControllerWheel;

/// <summary>
/// Owns the lifecycle of script-game helper sessions on the shell side: lazy launch on the first
/// rendered frame, restart after a kill (bounded), teardown on dismiss and app shutdown, and the
/// AppContainer launch-path handling.
///
/// <para><b>Launch path:</b> the helper runs in place only from Program Files — the one tree Windows
/// stamps <c>ALL APPLICATION PACKAGES:(RX)</c> down every level. Everywhere else some ancestor denies
/// the AppContainer token — a user-profile path fails with <c>CurHostFindFailure</c> 0x80008085; a
/// data drive or loose folder simply carries no package ACE, and an in-place launch there dies before
/// ever connecting — so the helper and its deps are stage-copied to a shallow
/// <c>C:\Radiata-ArcadeHost-&lt;hash&gt;</c> dir with an explicit DACL granting the derived SID RX
/// (<see cref="PrepareStageDir"/>). Fail toward staging: the copy always works. The staging dir is keyed on the source dir and refreshed only when files change. The flat
/// dev stage is removed at shutdown (best-effort; a survivor is reused next run); the installed
/// copy's self-contained stage persists across runs and is removed by the uninstall cleanup
/// (<see cref="DeleteOwnedStagedCopy"/>) — the install dir itself (%LOCALAPPDATA%\Programs\Radiata)
/// is under the user profile, so the installed copy always stages.</para>
/// </summary>
internal static class ScriptSessionCoordinator
{
    private const int MaxRestarts = 3;
    public const string HelperExeName = "Radiata.ArcadeHost.exe";

    /// <summary>The installer payload ships the helper self-contained in this subfolder beside
    /// Radiata.exe; when it exists the whole folder is the stage-copy source. Dev builds keep the
    /// flat framework-dependent layout (<see cref="HelperFiles"/> loose beside the exe).</summary>
    public const string HelperSubdirName = "ArcadeHost";

    /// <summary>The flat dev layout's dependency closure — what the staging fallback copies when
    /// there is no <see cref="HelperSubdirName"/> folder. Kept explicit so a repo-dir dev build
    /// never stages the whole WPF output beside C:\.</summary>
    private static readonly string[] HelperFiles =
    [
        "Radiata.ArcadeHost.exe", "Radiata.ArcadeHost.dll",
        "Radiata.ArcadeHost.deps.json", "Radiata.ArcadeHost.runtimeconfig.json",
        "Jint.dll", "Acornima.dll",
    ];

    // Stage-copy naming, shared by creation and the uninstall sweep so they can never drift.
    private const string StageDirPrefix = "Radiata-ArcadeHost-";
    private const int StageHashLength = 12;
    private static string StageRoot => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    private static string StageDirFor(string appDir) => Path.Combine(StageRoot,
        StageDirPrefix + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(RecoveryTask.CurrentSid + "\n" + Path.GetFullPath(appDir).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant())))[..StageHashLength]);

    private sealed class Live
    {
        public ScriptGameSession? Session;                                   // null while a start is in flight
        public Task<(ScriptGameSession? Session, string? Error)>? Starting;  // in-flight start/restart (threadpool)
        public int Restarts;
        public bool GaveUp;
        public string? FaultNote;
    }

    private static readonly Dictionary<ScriptArcadeGame, Live> _live = [];
    private static ScriptAppContainer? _container;
    private static string? _stagedDir;
    private static bool _stagedThisRun;
    // Serialises StartSession bodies — container creation and the staging statics are shared, and a
    // fast dismiss+reopen can overlap two starts. Only ever taken on threadpool threads.
    private static readonly object _startGate = new();
    private static volatile bool _shutdown;

    /// <summary>Why the current session can't run, or null while healthy. The renderer draws the
    /// guard-style card from this.</summary>
    public static string? FaultFor(ScriptArcadeGame game)
        => _live.TryGetValue(game, out var l) && l.GaveUp ? (l.FaultNote ?? "the game stopped responding") : null;

    /// <summary>The last validated command buffer, or empty — without pumping, starting or restarting
    /// anything. For drawing a script game outside the frame pump (the picker's screenshot capture).</summary>
    public static IReadOnlyList<ScriptDrawCommand> LastBuffer(ScriptArcadeGame game)
        => _live.TryGetValue(game, out var l) && l.Session is { } s ? s.LastBuffer : [];

    /// <summary>Pump one rendered frame. Never blocks on the helper: session start and restart run
    /// on the threadpool (<see cref="ScriptGameSession.Start"/> blocks for seconds on process spawn
    /// + pipe connect, and this is called from OnRender), and while a start is in flight the
    /// renderer gets an empty buffer, which it already tolerates on the first frames. Restarts a
    /// killed helper up to <see cref="MaxRestarts"/> times per open.</summary>
    public static IReadOnlyList<ScriptDrawCommand> Pump(ScriptArcadeGame game)
    {
        var (elapsedMs, input) = game.TakePendingFrame();   // drain even while starting — keeps the backlog bounded

        if (!_live.TryGetValue(game, out var live))
        {
            live = new Live();
            _live[game] = live;
            BeginStart(game, live, disposeFirst: null);
            return [];
        }
        if (live.GaveUp) return [];

        if (live.Starting is { } starting)
        {
            if (!starting.IsCompleted) return [];   // still spawning — blank field this frame
            live.Starting = null;
            var (session, error) = starting.Result;   // StartSession never throws (catch-all inside)
            if (session is null)
            {
                live.GaveUp = true;
                live.FaultNote = error;
                Trace.WriteLine($"[Arcade] script '{game.Id}' failed to start: {error}");
                return [];
            }
            if (_shutdown) { session.Dispose(); return []; }
            live.Session = session;
        }

        if (live.Session is not { } s) return [];

        var status = s.Pump(game, elapsedMs, input);
        if (status is ScriptGameSession.PumpStatus.Violation or ScriptGameSession.PumpStatus.Dead)
        {
            string note = s.LastFault ?? "helper died";
            live.Session = null;
            if (++live.Restarts > MaxRestarts)
            {
                _ = Task.Run(s.Dispose);   // Dispose waits on the killed process — not on this thread
                live.GaveUp = true;
                live.FaultNote = note;
                Trace.WriteLine($"[Arcade] script '{game.Id}' gave up after {MaxRestarts} restarts: {note}");
                return [];
            }
            Trace.WriteLine($"[Arcade] script '{game.Id}' restarting ({live.Restarts}/{MaxRestarts}): {note}");
            BeginStart(game, live, disposeFirst: s);   // kv restored from the host-side mirror
            return [];
        }
        return s.LastBuffer;
    }

    /// <summary>Kick a start (or dispose-then-restart) onto the threadpool. The dead session, if
    /// any, is disposed on the same background task BEFORE the new start, so two helper processes
    /// can never overlap for one game. Adoption back onto the <see cref="Live"/> happens in
    /// <see cref="Pump"/> on the UI thread — background code never touches <see cref="_live"/>.</summary>
    private static void BeginStart(ScriptArcadeGame game, Live live, ScriptGameSession? disposeFirst)
    {
        live.Starting = Task.Run(() =>
        {
            disposeFirst?.Dispose();
            lock (_startGate) return StartSession(game);
        });
    }

    /// <summary>Tear down the session for a game being dismissed or swapped. The kv mirror in the
    /// game object is the surviving state; the helper just dies (kill-on-job-close backstopped).</summary>
    public static void Release(ScriptArcadeGame game)
    {
        if (!_live.Remove(game, out var live)) return;
        if (live.Session is { } s) _ = Task.Run(s.Dispose);   // the 2 s exit wait stays off the UI thread
        // A start still in flight must not leak its helper — dispose the session the moment it exists.
        live.Starting?.ContinueWith(t => t.Result.Session?.Dispose(),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>App-shutdown teardown; also cleans the dev staging dir. Never throws.</summary>
    public static void ShutdownAll()
    {
        _shutdown = true;   // a start completing after this point disposes its own result (see Pump)
        try
        {
            foreach (var l in _live.Values)
            {
                l.Session?.Dispose();
                l.Starting?.ContinueWith(t => t.Result.Session?.Dispose(),
                    CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            }
            _live.Clear();
            _container?.Dispose();
            _container = null;
            if (_stagedThisRun && _stagedDir is not null)
                try { OwnedPayloadCleanup.RemoveMatchingFiles(_stagedDir, AppContext.BaseDirectory, HelperFiles); } catch { /* reused next run */ }
        }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] script shutdown: {ex.Message}"); }
    }

    private static (ScriptGameSession?, string?) StartSession(ScriptArcadeGame game)
    {
        try
        {
            string? script = ReadScript(game.Manifest, out string? readError);
            if (script is null) return (null, readError);

            _container ??= ScriptAppContainer.Create();
            string? exe = HelperPath(out string? pathError);
            if (exe is null) return (null, pathError);

            Span<byte> seedBytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(seedBytes);
            ulong seed = BitConverter.ToUInt64(seedBytes);

            return ScriptGameSession.Start(exe, _container, script, seed, game.Kv);
        }
        catch (Exception ex)
        {
            return (null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string? ReadScript(ScriptGameManifest manifest, out string? error)
    {
        error = null;
        try
        {
            string path = Path.Combine(manifest.DirPath, manifest.EntryFile);
            var info = new FileInfo(path);
            if (!info.Exists) { error = "the game's script file is missing"; return null; }
            if (info.Length > ScriptGameManifest.MaxEntryFileBytes) { error = "the game's script file is too large"; return null; }
            return File.ReadAllText(path);
        }
        catch (Exception ex) { error = $"couldn't read the game's script: {ex.Message}"; return null; }
    }

    /// <summary>Resolve the helper exe the AppContainer can actually run. Source is the
    /// self-contained <c>ArcadeHost\</c> subfolder when the payload ships one (installer layout),
    /// else the app dir's flat dev layout. Run in place when the app dir is container-readable;
    /// otherwise the staged copy.</summary>
    private static string? HelperPath(out string? error)
    {
        error = null;
        string appDir = AppContext.BaseDirectory;
        string subDir = Path.Combine(appDir, HelperSubdirName);
        bool folderLayout = File.Exists(Path.Combine(subDir, HelperExeName));
        string srcDir = folderLayout ? subDir : appDir;
        string local = Path.Combine(srcDir, HelperExeName);
        if (!File.Exists(local)) { error = "Radiata.ArcadeHost.exe is missing beside Radiata.exe"; return null; }

        if (IsProgramFilesPath(appDir))
        {
            Trace.WriteLine($"[Arcade] script helper launching from the install dir ({(folderLayout ? "ArcadeHost folder" : "flat")} layout)");
            return local;
        }

        // Staging path (see class summary) — everywhere outside Program Files, including the installed
        // %LOCALAPPDATA%\Programs\Radiata and any repo/portable location.
        try
        {
            string stage = StageDirFor(appDir);
            PrepareStageDir(stage);
            bool copiedAny = folderLayout ? SyncTree(srcDir, stage) : SyncHelperFiles(srcDir, stage);
            if (copiedAny || _stagedDir != stage)
            {
                _container!.GrantExecuteOnDir(stage);   // icacls (OI)(CI)/T — covers the whole copied tree
                Trace.WriteLine($"[Arcade] script helper staged to {stage} ({(folderLayout ? "self-contained folder" : "flat dev")} layout)");
            }
            _stagedDir = stage;
            // The flat dev stage is small and cleaned at shutdown; the self-contained folder stage is
            // ~70 MB, so it persists across runs (the per-file short-circuit skips unchanged copies)
            // and its removal belongs to uninstall cleanup (DeleteOwnedStagedCopy).
            _stagedThisRun = !folderLayout;
            return Path.Combine(stage, HelperExeName);
        }
        catch (Exception ex)
        {
            error = $"couldn't stage the sandbox helper: {ex.Message}";
            return null;
        }
    }

    /// <summary>Create (or validate) the stage dir with an explicit DACL. The system-drive root
    /// grants Authenticated Users create plus inherited Modify, so a plain CreateDirectory yields a
    /// helper-binary dir every local account can rewrite — and the length+mtime staleness check in
    /// the sync passes is forgeable by anyone who can write the files. Two sides: a new dir is born
    /// with inheritance off and only SYSTEM / Administrators / the current user (full) plus the
    /// container SID (read-execute); an existing dir is refused unless the current user or an admin
    /// identity owns it (the root's create grant means any account could have pre-created the name),
    /// then has the same protected DACL re-asserted — which also heals stages older builds created
    /// with inherited ACEs. <see cref="ScriptAppContainer.GrantExecuteOnDir"/> still runs after the
    /// copy to stamp the container grant onto every staged file.</summary>
    private static void PrepareStageDir(string stage)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user   = identity.User!;
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        var sec = new DirectorySecurity();
        sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { system, admins, user })
            sec.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        sec.AddAccessRule(new FileSystemAccessRule(_container!.Identifier,
            FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));

        var di = new DirectoryInfo(stage);
        if (!di.Exists)
        {
            di.Create(sec);
            return;
        }
        var owner = di.GetAccessControl(AccessControlSections.Owner)
                      .GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        if (owner is null || !(owner.Equals(user) || owner.Equals(admins) || owner.Equals(system)))
            throw new IOException($"stage dir '{stage}' exists but is owned by "
                + $"'{owner?.Value ?? "unknown"}' — refusing to stage the sandbox helper into it");
        di.SetAccessControl(sec);
    }

    /// <summary>Stage the flat dev layout: exactly <see cref="HelperFiles"/>, skipping up-to-date
    /// copies (same length + write time). Returns whether anything was copied.</summary>
    private static bool SyncHelperFiles(string srcDir, string dstDir)
    {
        bool copied = false;
        foreach (var name in HelperFiles)
        {
            string src = Path.Combine(srcDir, name);
            if (!File.Exists(src)) continue;   // deps.json set can vary; exe presence was checked
            string dst = Path.Combine(dstDir, name);
            var s = new FileInfo(src);
            var d = new FileInfo(dst);
            if (!d.Exists || d.Length != s.Length || d.LastWriteTimeUtc != s.LastWriteTimeUtc)
            { File.Copy(src, dst, overwrite: true); copied = true; }
        }
        return copied;
    }

    /// <summary>Stage the self-contained folder layout: every file under <paramref name="srcDir"/>,
    /// recursively, with the same up-to-date short-circuit per file. When anything changed, files no
    /// longer present in the source are removed from the stage so a layout/version change can't leave
    /// a mixed closure behind. Returns whether anything was copied.</summary>
    private static bool SyncTree(string srcDir, string dstDir)
    {
        bool copied = false;
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var src in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(srcDir, src);
            expected.Add(rel);
            string dst = Path.Combine(dstDir, rel);
            var s = new FileInfo(src);
            var d = new FileInfo(dst);
            if (!d.Exists || d.Length != s.Length || d.LastWriteTimeUtc != s.LastWriteTimeUtc)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);   // File.Copy carries the source write time
                copied = true;
            }
        }
        if (copied)
            foreach (var stale in Directory.EnumerateFiles(dstDir, "*", SearchOption.AllDirectories))
                if (!expected.Contains(Path.GetRelativePath(dstDir, stale)))
                    try { File.Delete(stale); } catch { /* best-effort; a survivor is harmless */ }
        return copied;
    }

    /// <summary>Remove matching helper payload files only from this account's install-specific stage.
    /// Unknown files, modified files, other installations, and legacy unscoped stages remain untouched.</summary>
    public static void DeleteOwnedStagedCopy()
    {
        try
        {
            string appDir = AppContext.BaseDirectory;
            string stage = StageDirFor(appDir);
            if (!Directory.Exists(stage)) return;
            string subDir = Path.Combine(appDir, HelperSubdirName);
            bool folderLayout = File.Exists(Path.Combine(subDir, HelperExeName));
            string source = folderLayout ? subDir : appDir;
            IEnumerable<string> files = folderLayout
                ? Directory.EnumerateFiles(source, "*", new EnumerationOptions
                    { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
                    .Select(file => Path.GetRelativePath(source, file)).ToArray()
                : HelperFiles;
            OwnedPayloadCleanup.RemoveMatchingFiles(stage, source, files);
        }
        catch (Exception ex) { Trace.WriteLine($"[Uninstall] owned stage cleanup: {ex.Message}"); }
    }

    /// <summary>Whether an AppContainer token can execute from <paramref name="dir"/> without a stage
    /// copy. Only Program Files (x64/x86) earns it — every other location must prove nothing, because
    /// the failure mode of guessing wrong is a helper that dies before connecting (5 s of connect
    /// timeout, then the give-up card). Unknown/unparseable → false → stage.</summary>
    private static bool IsProgramFilesPath(string dir)
    {
        try
        {
            string full = Path.GetFullPath(dir);
            foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string root = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(root)) continue;
                root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        catch { return false; }   // unknown → stage; the copy always works
    }
}
