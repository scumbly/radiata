using System.Security.Cryptography;

namespace ControllerWheel;

/// <summary>Remove byte-identical staged payload copies, preserving unknown/modified files and links.</summary>
public static class OwnedPayloadCleanup
{
    public static void RemoveMatchingFiles(string directory, string source, IEnumerable<string> relativeFiles)
    {
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if (root == Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar))
            throw new IOException("A payload stage cannot be a drive root.");
        AssertNoLinks(root);
        var candidates = new List<(string Destination, string Source)>();
        foreach (string relative in relativeFiles)
        {
            if (Path.IsPathRooted(relative)) throw new IOException("Expected a relative payload path.");
            string destination = Path.GetFullPath(Path.Combine(root, relative));
            string original = Path.GetFullPath(Path.Combine(source, relative));
            if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Payload path escaped its stage.");
            AssertNoLinks(destination);
            AssertNoLinks(original);
            candidates.Add((destination, original));
        }
        // Validate the entire plan before removing the first file.
        foreach (var (destination, original) in candidates)
        {
            if (!File.Exists(destination) || !File.Exists(original)) continue;
            using var staged = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var shipped = File.OpenRead(original);
            if (staged.Length == shipped.Length && SHA256.HashData(staged).AsSpan().SequenceEqual(SHA256.HashData(shipped)))
                File.Delete(destination);
        }
        foreach (string child in candidates.Select(x => Path.GetDirectoryName(x.Destination)!)
                     .Append(root).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
        {
            string? cursor = child;
            while (cursor is not null && (cursor == root || cursor.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                AssertNoLinks(cursor);
                if (Directory.Exists(cursor) && !Directory.EnumerateFileSystemEntries(cursor).Any())
                    Directory.Delete(cursor, recursive: false);
                if (cursor == root) break;
                cursor = Path.GetDirectoryName(cursor);
            }
        }
    }

    private static void AssertNoLinks(string path)
    {
        for (string? cursor = path; cursor is not null; cursor = Path.GetDirectoryName(cursor))
            if ((File.Exists(cursor) || Directory.Exists(cursor))
                && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing a linked payload path.");
    }
}
