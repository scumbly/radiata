using System.Text;

namespace ControllerWheel;

/// <summary>Crash-safe file replacement: write to a unique temp sibling in the same directory, then
/// <see cref="File.Move(string, string, bool)"/> it over the destination in one step (atomic on the
/// same NTFS volume, so the destination is only ever the whole old file or the whole new one). On any
/// exception the temp file is deleted best-effort and the exception rethrown; callers that need to
/// swallow a write failure wrap the call themselves. Does not flush to disk before the move — a site
/// that needs that extra crash/power-loss guarantee keeps its own FileStream + Flush(flushToDisk: true)
/// implementation instead of calling this helper.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents, Encoding? encoding = null)
    {
        string tmp = TempPath(path);
        try
        {
            if (encoding is null) File.WriteAllText(tmp, contents);
            else File.WriteAllText(tmp, contents, encoding);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        string tmp = TempPath(path);
        try
        {
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    private static string TempPath(string path) => path + "." + Guid.NewGuid().ToString("N") + ".tmp";
}
