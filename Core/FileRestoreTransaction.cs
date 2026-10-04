using System.Diagnostics;

namespace ControllerWheel;

/// <summary>Replaces files under one application-data root and retains originals until the caller commits.
/// A failed restore rolls back every completed replacement. Incomplete rollback keeps its recovery copies.</summary>
public sealed class FileRestoreTransaction : IDisposable
{
    private sealed class Change(string path, string? backup)
    {
        public readonly string Path = path;
        public readonly string? Backup = backup;
        public bool Written;
    }

    private readonly string _root;
    private readonly string _journal;
    private readonly List<Change> _changes = [];
    private bool _committed;

    public FileRestoreTransaction(string root)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_root);
        CheckDirectories(_root.TrimEnd(Path.DirectorySeparatorChar));
        _journal = Path.Combine(_root, ".restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_journal);
    }

    public void WriteText(string path, string value) => WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(value));

    public void WriteBytes(string path, byte[] value)
    {
        if (_committed) throw new InvalidOperationException("Restore already committed.");
        path = Path.GetFullPath(path);
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Restore destination is outside application data.");
        var parent = Path.GetDirectoryName(path)!;
        CheckDirectories(parent);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Restore destination is a reparse point.");
        Directory.CreateDirectory(parent);
        var change = _changes.FirstOrDefault(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));
        if (change is null)
        {
            string? backup = null;
            if (File.Exists(path))
            {
                backup = Path.Combine(_journal, Guid.NewGuid().ToString("N"));
                File.Copy(path, backup, overwrite: false);
            }
            change = new Change(path, backup);
            _changes.Add(change);
            // Keep a durable path map so retained originals remain identifiable after a process exit.
            using var journal = new FileStream(Path.Combine(_journal, "manifest.jsonl"), FileMode.Append,
                FileAccess.Write, FileShare.Read);
            var record = System.Text.Json.JsonSerializer.Serialize(new { OriginalPath = path, BackupPath = backup }) + "\n";
            journal.Write(System.Text.Encoding.UTF8.GetBytes(record));
            journal.Flush(flushToDisk: true);
        }
        // Not AtomicFile: this flushes the temp file to disk before the move, which that shared helper
        // doesn't do — a restore is exactly the crash-mid-write case the flush guards against.
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(value);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
            change.Written = true;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void CheckDirectories(string path)
    {
        for (var dir = new DirectoryInfo(path); dir is not null; dir = dir.Parent)
        {
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Restore cannot follow a directory reparse point.");
            if (string.Equals(dir.FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                              _root, StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new IOException("Restore directory is outside application data.");
    }

    public void Commit() => _committed = true;

    public void Dispose()
    {
        var failures = new List<Exception>();
        if (!_committed)
            foreach (var change in _changes.AsEnumerable().Reverse())
            {
                if (!change.Written) continue;
                try
                {
                    CheckDirectories(Path.GetDirectoryName(change.Path)!);
                    if (change.Backup is not null) File.Move(change.Backup, change.Path, overwrite: true);
                    else File.Delete(change.Path);
                }
                catch (Exception ex) { failures.Add(ex); }
            }
        if (failures.Count > 0)
            throw new IOException($"Restore rollback incomplete. Previous files are retained at {_journal}.", failures[0]);
        try { Directory.Delete(_journal, recursive: true); }
        catch (Exception ex) { Trace.WriteLine($"[Restore] recovery-copy cleanup failed: {ex.Message}"); }
    }
}
