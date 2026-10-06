using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>Windows Job Object jailing the Arcade script helper. The Job is the hard memory cap
/// and the kill switch — Jint's LimitMemory is only a burst tripwire. Closing the
/// handle kills the helper (KILL_ON_JOB_CLOSE), so host death of any kind reclaims the child, and
/// ACTIVE_PROCESS_LIMIT=1 is what actually denies child-process spawning (System32 is
/// container-readable — AppContainer alone would let cmd.exe launch).</summary>
internal sealed class ScriptJobObject : IDisposable
{
    private nint _handle;

    public const long ProcessMemoryLimitBytes = 128L * 1024 * 1024;

    private const uint JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x0008;
    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x0100;
    private const uint JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x0400;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    public ScriptJobObject()
    {
        _handle = CreateJobObject(0, null);
        if (_handle == 0) throw new Win32Exception();

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_PROCESS_MEMORY
                           | JOB_OBJECT_LIMIT_ACTIVE_PROCESS
                           | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                           | JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION,
                ActiveProcessLimit = 1,
            },
            ProcessMemoryLimit = (nuint)ProcessMemoryLimitBytes,
        };

        int size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        nint mem = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, mem, false);
            if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformation, mem, (uint)size))
                throw new Win32Exception();
        }
        finally { Marshal.FreeHGlobal(mem); }
    }

    /// <summary>Assign by raw process handle — the child is created suspended, jailed here, then
    /// resumed, so it is inside the job before its first instruction runs.</summary>
    public void AssignHandle(nint processHandle)
    {
        if (!AssignProcessToJobObject(_handle, processHandle)) throw new Win32Exception();
    }

    public void Dispose()
    {
        nint h = Interlocked.Exchange(ref _handle, 0);
        if (h != 0) NativeMethods.CloseHandle(h);   // KILL_ON_JOB_CLOSE terminates the helper here
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateJobObject(nint attrs, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint job, int infoClass, nint info, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
