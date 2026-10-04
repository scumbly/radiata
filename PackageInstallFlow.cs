using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ControllerWheel;

/// <summary>
/// The live side of drop-in packages: the run's package inventory, the approval prompt, registration,
/// drag-and-drop install (Settings window, or paths handed to Radiata.exe) and Recycle-Bin removal.
/// Core's <see cref="PackageInstaller"/> does the copying and validating; this owns the dialogs and the
/// registries. See docs/PACKAGES.md ▸ Installing and removing.
///
/// ⚠ A token's content never changes within a run — the renderer and the Arcade caches key on the token.
/// A package that would re-register a token with different content is installed on disk and waits for a
/// restart (<see cref="_registeredHash"/>).
/// </summary>
internal static class PackageInstallFlow
{
    private static readonly List<PackageStore.Discovered> _known = [];
    // Every token registered this run → the content hash it was registered with. Kept after an Uninstall,
    // because the render caches keep the old content for the rest of the run.
    private static readonly Dictionary<string, string> _registeredHash = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<(string Path, Window? Owner)> _queue = new();
    private static bool _draining;

    /// <summary>Raised after the registered set changes, so an open Customize tab rebuilds its Custom group.</summary>
    public static event Action? RegistryChanged;

    /// <summary>Whether this build offers either package kind (the release gates).</summary>
    public static bool Offered => ReleaseGates.MaterialPackages || ReleaseGates.ArcadePackages;

    private static bool KindOffered(string kind) =>
        kind == "material" ? ReleaseGates.MaterialPackages : ReleaseGates.ArcadePackages;

    // ── inventory + registration ─────────────────────────────────────────────────────────────────────

    /// <summary>Adopt the startup scan and register every package with a recorded approval. Runs BEFORE
    /// the ConfigLoader is constructed (Sanitize degrades an unregistered custom token to Pearl).</summary>
    public static void LoadStartupScan(IReadOnlyList<PackageStore.Discovered> scan)
    {
        _known.Clear();
        _known.AddRange(scan);
        foreach (var d in scan.Where(d => d.Error is not null))
            Trace.WriteLine($"[Packages] skipped {(d.Kind == "arcade" ? "arcade game" : "material")} '{d.FolderName}': {d.Error}");
        RegisterAccepted();
    }

    /// <summary>Re-register both registries from the approved inventory. Registration is independent of the
    /// Arcade opt-in: the catalog entry existing is what keeps an authored slice resolvable;
    /// Arcade.Available gates all offering.</summary>
    private static void RegisterAccepted()
    {
        var accepted = _known
            .Where(d => (d.Material is not null || d.ArcadeGame is not null) &&
                        PackageConsent.Check(d.Kind, d.FolderName, d.ContentHash) == PackageConsent.Verdict.Accepted)
            .ToList();
        Materials.RegisterCustom(accepted.Where(d => d.Material is not null).Select(d => d.Material!));
        ArcadeCatalog.RegisterScripts(accepted.Where(d => d.ArcadeGame is not null).Select(d => d.ArcadeGame!));
        foreach (var d in accepted)
            if (PackageInstaller.TokenOf(d) is { } token) _registeredHash.TryAdd(token, d.ContentHash);
        RegistryChanged?.Invoke();
    }

    /// <summary>The stern approval gate for packages found at startup with no recorded verdict for their
    /// current content (declines are remembered; a content change asks again). Runs once the dispatcher
    /// is idle. Accepting registers immediately.</summary>
    public static void PromptForNewPackages()
    {
        try
        {
            var pending = _known
                .Where(d => (d.Material is not null || d.ArcadeGame is not null) &&
                            PackageConsent.Check(d.Kind, d.FolderName, d.ContentHash) == PackageConsent.Verdict.Unknown)
                .ToList();
            bool any = false;
            foreach (var d in pending)
            {
                bool accepted = AskApproval(d);
                PackageConsent.Record(d.Kind, d.FolderName, d.ContentHash, accepted);
                Trace.WriteLine($"[Packages] {d.Kind} '{d.FolderName}' {(accepted ? "ACCEPTED" : "declined")} by user");
                any |= accepted;
            }
            if (any) RegisterAccepted();
        }
        catch (Exception ex) { Trace.WriteLine($"[Packages] consent flow failed: {ex.Message}"); }
    }

    /// <summary>Arcade packages get the sterner code-worded copy — they contain a script that will RUN
    /// (sandboxed). Materials stay on the data-only wording.</summary>
    private static bool AskApproval(PackageStore.Discovered d)
    {
        var dlg = d.ArcadeGame is { } game
            ? new PackageConsentWindow("Arcade game", game.Title, game.Author, d.FolderName, d.ContentHash, runsCode: true)
            : new PackageConsentWindow("Material theme", d.Material!.Name, d.Material.Author, d.FolderName, d.ContentHash);
        dlg.ShowDialog();
        return dlg.Accepted;
    }

    // ── removal ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Send a package folder to the Recycle Bin. Null on success, "" when the user dismissed
    /// Windows' own error dialog, otherwise a readable reason. Refuses anything
    /// <see cref="PackageStore.IsPackageDir"/> rejects.</summary>
    public static string? Recycle(string dir)
    {
        try
        {
            if (!PackageStore.IsPackageDir(dir)) return "its folder isn't a package folder inside Radiata's Packages folder";
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(dir,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            Trace.WriteLine($"[Packages] '{Path.GetFileName(dir)}' sent to the Recycle Bin");
            return null;
        }
        catch (OperationCanceledException) { return ""; }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Packages] recycling '{dir}' failed: {ex.Message}");
            return ex.Message;
        }
    }

    /// <summary>Drop an uninstalled theme from the inventory and the registry. The caller has already moved
    /// the config off its token. Its approval record stays, so restoring the unchanged folder loads it
    /// again without a prompt.</summary>
    public static void ForgetMaterial(MaterialPackage pack)
    {
        _known.RemoveAll(d => d.Material is { } m && m.Token == pack.Token && SamePath(d.FolderPath, pack.DirPath));
        Materials.UnregisterCustom(pack.Token);
        RegistryChanged?.Invoke();
    }

    // ── drag-and-drop install ────────────────────────────────────────────────────────────────────────

    /// <summary>Install every package-shaped path (a folder or a .zip), one at a time, each through the
    /// approval prompt. Drops that arrive while one is running join the queue. No-op in a build that
    /// offers neither kind.</summary>
    public static void Install(IEnumerable<string> paths, Window? owner)
    {
        if (!Offered) return;
        foreach (var p in paths)
            if (PackageInstaller.IsCandidate(p)) _queue.Enqueue((p, owner));
        if (!_draining && _queue.Count > 0) _ = DrainAsync();
    }

    private static async Task DrainAsync()
    {
        _draining = true;
        int tried = 0, installed = 0;
        Window? owner = null;
        try
        {
            while (_queue.Count > 0)
            {
                var (path, o) = _queue.Dequeue();
                owner = o;
                tried++;
                try { if (await InstallOneAsync(path, o)) installed++; }
                catch (Exception ex) { Trace.WriteLine($"[Packages] drop-install '{path}' failed: {ex}"); }
            }
        }
        finally { _draining = false; }
        if (tried > 1)
            Box(owner, Loc.F(UiText.Dialogs.InstallSummary, installed, tried), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static async Task<bool> InstallOneAsync(string path, Window? owner)
    {
        var label = Path.GetFileName(path.TrimEnd('\\', '/'));
        string? error = null;
        var staged = await Task.Run(() => PackageInstaller.Stage(path, KindOffered, out error));
        if (staged is null)
        {
            Trace.WriteLine($"[Packages] drop-install '{label}' rejected: {error}");
            Box(owner, Loc.F(UiText.Dialogs.InstallRejected, label, error ?? "unknown error"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        try
        {
            var name = staged.DisplayName;
            // The disk is the truth for what is installed: _known is the run's registered set and goes stale
            // when a replace waits for a restart.
            var onDisk = await Task.Run(PackageStore.Scan);
            var conflicts = PackageInstaller.FindConflicts(staged, onDisk);
            if (conflicts.Count > 0
                && Box(owner, Loc.F(UiText.Dialogs.InstallReplace, name), MessageBoxButton.YesNo,
                       MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return false;

            // Approve BEFORE anything reaches the kind folder. A decline installs nothing and records
            // nothing, so the same package dropped in by hand later still asks.
            bool approved = PackageConsent.Check(staged.Kind, staged.FolderName, staged.Package.ContentHash)
                                == PackageConsent.Verdict.Accepted
                            || AskApproval(staged.Package);
            if (!approved)
            {
                Trace.WriteLine($"[Packages] drop-install '{staged.FolderName}' declined by user");
                return false;
            }

            foreach (var old in conflicts)
                if (Recycle(old) is { } recycleError)
                {
                    if (recycleError.Length > 0)
                        Box(owner, Loc.F(UiText.Dialogs.InstallFailed, name, recycleError), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

            var committed = PackageInstaller.Commit(staged, out var commitError);
            if (committed is null)
            {
                Trace.WriteLine($"[Packages] drop-install '{staged.FolderName}' failed at commit: {commitError}");
                Box(owner, Loc.F(UiText.Dialogs.InstallFailed, name, commitError ?? "unknown error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            PackageConsent.Record(committed.Kind, committed.FolderName, committed.ContentHash, accepted: true);
            Trace.WriteLine($"[Packages] {committed.Kind} '{committed.FolderName}' installed by drag-and-drop");

            var token = staged.Token;
            if (_registeredHash.TryGetValue(token, out var liveHash)
                && !string.Equals(liveHash, committed.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                // Same token, new content: the inventory keeps the entry it registered, so the live
                // registry never swaps content under the render caches. The next start loads the new copy.
                var restart = Box(owner, Loc.F(UiText.Dialogs.InstallRestart, name), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (restart == MessageBoxResult.Yes) SettingsWindow.RestartApp(reopenSettings: owner is { IsVisible: true });
                return true;
            }
            _known.RemoveAll(d => conflicts.Any(c => SamePath(d.FolderPath, c)) || !Directory.Exists(d.FolderPath));
            _known.Add(committed);
            RegisterAccepted();
            return true;
        }
        finally { PackageInstaller.Discard(staged); }
    }

    // ── paths handed to Radiata.exe (a drop onto the exe or a shortcut to it) ────────────────────────

    public const string InstallEventName = @"Local\Radiata.InstallRequest";
    private static string RequestsDir => Path.Combine(PackageStore.PackagesDir, ".requests");

    /// <summary>The command-line arguments that name a package-shaped path. Empty in a build that offers
    /// neither kind.</summary>
    public static IReadOnlyList<string> PathsFromArgs(string[] args)
    {
        if (!Offered) return [];
        var list = new List<string>();
        foreach (var a in args)
        {
            if (a.StartsWith("--", StringComparison.Ordinal)) continue;
            try { if (PackageInstaller.IsCandidate(a)) list.Add(Path.GetFullPath(a)); }
            catch { /* not a path */ }
        }
        return list;
    }

    /// <summary>Second launch: pass the paths to the running instance. False if it couldn't be reached.</summary>
    public static bool HandOff(IReadOnlyList<string> paths)
    {
        string? file = null;
        try
        {
            if (!System.Threading.EventWaitHandle.TryOpenExisting(InstallEventName, out var ev)) return false;
            using (ev)
            {
                Directory.CreateDirectory(RequestsDir);
                file = Path.Combine(RequestsDir, Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllLines(file + ".tmp", paths);
                File.Move(file + ".tmp", file);
                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);   // the approval prompt should come to the front
                ev.Set();
            }
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Packages] install hand-off failed: {ex.Message}");
            try { if (file is not null) { File.Delete(file); File.Delete(file + ".tmp"); } } catch { }
            return false;
        }
    }

    /// <summary>Primary instance, at startup: drop request files a crashed run never read.</summary>
    public static void ClearRequests()
    {
        try { if (Directory.Exists(RequestsDir)) Directory.Delete(RequestsDir, recursive: true); }
        catch (Exception ex) { Trace.WriteLine($"[Packages] request cleanup failed: {ex.Message}"); }
    }

    /// <summary>Primary instance, on the install event: read and delete every request, then install.</summary>
    public static void TakeRequests(Window? owner)
    {
        var paths = new List<string>();
        try
        {
            if (!Directory.Exists(RequestsDir)) return;
            foreach (var f in Directory.GetFiles(RequestsDir, "*.txt"))
            {
                try { paths.AddRange(File.ReadAllLines(f).Where(l => l.Length > 0)); }
                finally { try { File.Delete(f); } catch { } }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Packages] reading install requests failed: {ex.Message}"); }
        Trace.WriteLine($"[Packages] install request from a second launch: {paths.Count} path(s)");
        Install(paths, owner);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                                 Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                                 StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>A message box that can't open behind another app: with no visible owner (a drop onto the
    /// exe while Settings is closed) it borrows a topmost off-screen host window.</summary>
    private static MessageBoxResult Box(Window? owner, string text, MessageBoxButton buttons, MessageBoxImage image,
                                        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var caption = Loc.T(UiText.Dialogs.InstallCaption);
        if (owner is { IsVisible: true })
            return System.Windows.MessageBox.Show(owner, text, caption, buttons, image, defaultResult);
        var host = new Window
        {
            Width = 1, Height = 1, Left = -10000, Top = -10000, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false, Topmost = true, ShowActivated = true,
        };
        host.Show();
        try
        {
            host.Activate();
            return System.Windows.MessageBox.Show(host, text, caption, buttons, image, defaultResult);
        }
        finally { host.Close(); }
    }
}
