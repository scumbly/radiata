using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ControllerWheel;

namespace Radiata.TestHarness;

internal static class T_Payload
{
    public static void Run()
    {
        H.Group("Published helper removal — isolated copy only");
        string source = Environment.GetEnvironmentVariable("RADIATA_TEST_PAYLOAD");
        if (string.IsNullOrWhiteSpace(source)) { H.Fail("payload source supplied", "Set RADIATA_TEST_PAYLOAD to the staged publish directory."); return; }
        string root = Path.Combine(AppPaths.AppDataDir, "published-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string helperSource = Path.Combine(Path.GetFullPath(source), "ArcadeHost");
        foreach (var file in Directory.EnumerateFiles(helperSource, "*", SearchOption.AllDirectories))
        {
            for (var entry = new FileInfo(file) as FileSystemInfo; entry is not null; entry = entry is FileInfo f ? f.Directory : ((DirectoryInfo)entry).Parent)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Payload source contains a link.");
                if (string.Equals(entry.FullName, helperSource, StringComparison.OrdinalIgnoreCase)) break;
            }
            string destination = Path.Combine(root, "ArcadeHost", Path.GetRelativePath(helperSource, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination);
        }
        File.Copy(Path.Combine(source, "Radiata.helper-files.txt"), Path.Combine(root, "Radiata.helper-files.txt"));
        File.WriteAllText(Path.Combine(root, "Radiata.exe"), "isolated main payload sentinel");
        File.WriteAllText(Path.Combine(root, "owner-notes.txt"), "keep");
        int fileCount = Directory.GetFiles(Path.Combine(root, "ArcadeHost"), "*", SearchOption.AllDirectories).Length;
        H.Check("copied a full runtime rather than a six-file development helper", fileCount > 100);
        RunWorker(PortableUninstall.Script(root, int.MaxValue, 0), expectedSuccess: true);
        H.Check("complete published helper and all empty culture folders are removed", !Directory.Exists(Path.Combine(root, "ArcadeHost")));
        H.Check("completed helper removal also removes its manifest", !File.Exists(Path.Combine(root, "Radiata.helper-files.txt")));
        H.Check("unrelated file survives full payload cleanup", File.ReadAllText(Path.Combine(root, "owner-notes.txt")) == "keep");

        string tampered = Path.Combine(AppPaths.AppDataDir, "tampered-plan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tampered);
        File.WriteAllText(Path.Combine(tampered, "Radiata.exe"), "must survive");
        string manifest = Path.Combine(tampered, "Radiata.helper-files.txt");
        File.WriteAllText(manifest, "");
        string script = PortableUninstall.Script(tampered, int.MaxValue, 0);
        File.WriteAllText(manifest, new string('0', 64) + "|ArcadeHost\\changed.dll");
        RunWorker(script, expectedSuccess: false);
        H.Check("changed manifest aborts before any main payload removal", File.ReadAllText(Path.Combine(tampered, "Radiata.exe")) == "must survive");
    }

    private static void RunWorker(string script, bool expectedSuccess)
    {
        using var worker = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
        });
        var errors = worker.StandardError.ReadToEndAsync();
        bool exited = worker.WaitForExit(15000);
        H.Check(expectedSuccess ? "published cleanup worker succeeds" : "tampered plan worker refuses cleanup",
            exited && (worker.ExitCode == 0) == expectedSuccess, exited ? errors.GetAwaiter().GetResult() : "timed out");
    }
}
