using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace ControllerWheel;

/// <summary>An AppContainer profile with zero declared capabilities — the OS-level access wall
/// around the Arcade script helper (the Job Object supplies memory/process limits, this supplies
/// file/registry/network denial). Profile is created once and reused across sessions; the SID is
/// what the pipe ACL and any staging-directory grant name — only the derived SID, never
/// ALL APPLICATION PACKAGES.</summary>
internal sealed class ScriptAppContainer : IDisposable
{
    public const string ProfileName = "RadiataArcadeHost";

    public nint Sid { get; }
    public SecurityIdentifier Identifier { get; }
    public string SidString { get; }

    private bool _disposed;

    private ScriptAppContainer(nint sid)
    {
        Sid = sid;
        Identifier = new SecurityIdentifier(sid);
        SidString = Identifier.Value;
    }

    /// <summary>Creates (or reuses) the profile and derives its SID. Zero capabilities.</summary>
    public static ScriptAppContainer Create()
    {
        int hr = CreateAppContainerProfile(ProfileName, "Radiata Arcade Host",
            "Sandbox for Radiata drop-in arcade games", IntPtr.Zero, 0, out nint createdSid);
        const int ERROR_ALREADY_EXISTS = unchecked((int)0x800700B7);
        if (hr == 0) return new ScriptAppContainer(createdSid);
        if (hr == ERROR_ALREADY_EXISTS)
        {
            int dhr = DeriveAppContainerSidFromAppContainerName(ProfileName, out nint derivedSid);
            if (dhr != 0) throw new Win32Exception(dhr, $"DeriveAppContainerSid: 0x{dhr:X8}");
            return new ScriptAppContainer(derivedSid);
        }
        throw new Win32Exception(hr, $"CreateAppContainerProfile: 0x{hr:X8}");
    }

    /// <summary>Grant the derived SID read+execute on a directory (the dev-build staging dir —
    /// the AppContainer token is access-checked against EVERY ancestor of the exe's path, so a
    /// user-profile install can't host the helper; see ScriptSessionCoordinator.HelperPath).</summary>
    public void GrantExecuteOnDir(string dir)
        => Icacls($"\"{dir}\" /grant \"*{SidString}:(OI)(CI)(RX)\" /T /C /Q");

    private static void Icacls(string args)
    {
        var psi = new ProcessStartInfo("icacls.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        // Both streams drained concurrently: reading one to its end while the child fills the other's pipe
        // is the classic redirect deadlock, and /T /C over the staged helper tree can produce bulk stderr.
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(15_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException("icacls did not finish within 15 s");
        }
        System.Threading.Tasks.Task.WaitAll(new[] { outTask, errTask }, 2_000);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // The profile itself is deliberately not deleted — it is stable per machine and recreating
        // it every session would churn per-container storage. Only the SID allocation is freed.
        if (Sid != 0) FreeSid(Sid);
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int CreateAppContainerProfile(string appContainerName, string displayName,
        string description, IntPtr capabilities, int capabilityCount, out nint sid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int DeriveAppContainerSidFromAppContainerName(string appContainerName, out nint sid);

    [DllImport("advapi32.dll")]
    private static extern nint FreeSid(nint sid);
}

/// <summary>Launches the helper suspended inside the AppContainer, assigns it to the Job Object
/// while suspended, then resumes it — jailed before its first instruction.</summary>
internal static class ScriptContainerLauncher
{
    private const uint CREATE_SUSPENDED = 0x00000004;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private static readonly nint PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES = 0x00020009;

    public static Process Launch(string exePath, string commandLine, ScriptAppContainer ac, ScriptJobObject job)
    {
        var secCaps = new SECURITY_CAPABILITIES
        {
            AppContainerSid = ac.Sid,
            Capabilities = IntPtr.Zero,
            CapabilityCount = 0,
            Reserved = 0,
        };

        nint attrList = IntPtr.Zero;
        nint capsMem = IntPtr.Zero;
        var pi = default(PROCESS_INFORMATION);
        try
        {
            nint size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attrList = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList");

            capsMem = Marshal.AllocHGlobal(Marshal.SizeOf<SECURITY_CAPABILITIES>());
            Marshal.StructureToPtr(secCaps, capsMem, false);
            if (!UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,
                    capsMem, (nint)Marshal.SizeOf<SECURITY_CAPABILITIES>(), IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute");

            var si = new STARTUPINFOEX();
            si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            si.lpAttributeList = attrList;

            var cmd = new StringBuilder(commandLine);
            // The child inherits nothing usable as a CWD (the host's CWD is outside the container's
            // reach), so pin it to the helper's own dir — otherwise the .NET apphost's path
            // resolution fails with CurHostFindFailure (0x80008085).
            string workingDir = Path.GetDirectoryName(exePath)!;
            bool ok = CreateProcess(exePath, cmd, IntPtr.Zero, IntPtr.Zero, false,
                    CREATE_SUSPENDED | CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero, workingDir, ref si, out pi);
            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess (AppContainer)");

            try
            {
                job.AssignHandle(pi.hProcess);
                if (ResumeThread(pi.hThread) == unchecked((uint)-1))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "ResumeThread");
            }
            catch
            {
                // A child created suspended and never resumed would otherwise outlive us until reboot,
                // holding the container token, with the job never having claimed it.
                TerminateProcess(pi.hProcess, 1);
                throw;
            }

            return Process.GetProcessById(pi.dwProcessId);
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) NativeMethods.CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero) NativeMethods.CloseHandle(pi.hProcess);
            if (attrList != IntPtr.Zero) { DeleteProcThreadAttributeList(attrList); Marshal.FreeHGlobal(attrList); }
            if (capsMem != IntPtr.Zero) Marshal.FreeHGlobal(capsMem);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_CAPABILITIES
    {
        public nint AppContainerSid;
        public nint Capabilities;
        public int CapabilityCount;
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public nint lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute,
        nint value, nint size, nint prevValue, nint returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string? applicationName, StringBuilder commandLine,
        nint procAttrs, nint threadAttrs, bool inheritHandles, uint creationFlags, nint environment,
        string? currentDirectory, ref STARTUPINFOEX startupInfo, out PROCESS_INFORMATION processInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(nint process, uint exitCode);
}
