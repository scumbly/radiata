using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ControllerWheel;

/// <summary>Per-user, per-install logon recovery. A task is replaced or removed only when its
/// principal, trigger, and sole action match this installation. Elevation must retain the originating SID.</summary>
public static class RecoveryTask
{
    private const string LegacyTaskName = "RadiataControllerRecovery";
    public static string CurrentSid
    {
        get { using var identity = WindowsIdentity.GetCurrent(); return identity.User?.Value
            ?? throw new InvalidOperationException("The current Windows account has no SID."); }
    }

    public static string OwnerSid
    {
        get
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--owner-sid");
            if (index < 0) return CurrentSid;
            if (index + 1 >= args.Length) throw new ArgumentException("Missing owner SID.");
            var sid = new SecurityIdentifier(args[index + 1]);
            // IsAccountSid() is true only for S-1-5-21 (local / AD) accounts; Entra-joined users carry S-1-12-1
            // SIDs and must pass too. Both forms are user SIDs the scheduler accepts as a principal.
            bool entra = sid.Value.StartsWith("S-1-12-1-", StringComparison.Ordinal);
            if (!sid.IsAccountSid() && !entra) throw new ArgumentException("Owner must be an account SID.");
            return sid.Value;
        }
    }

    private static string CanonicalPath(string path) => Path.GetFullPath(path).TrimEnd('\\').ToUpperInvariant();
    private static string ScopedName(string exePath, string sid) => LegacyTaskName + "-" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sid + "\n" + CanonicalPath(exePath))))[..24];

    private static bool OwnsXml(string xml, string exePath, string sid)
    {
        try
        {
            var root = XDocument.Parse(xml).Root;
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            if (root?.Name != ns + "Task") return false;
            var principals = root.Element(ns + "Principals")?.Elements().ToArray();
            var actions = root.Element(ns + "Actions")?.Elements().ToArray();
            var triggers = root.Element(ns + "Triggers")?.Elements().ToArray();
            if (principals?.Length != 1 || actions?.Length != 1 || triggers?.Length != 1) return false;
            var action = actions[0];
            var trigger = triggers[0];
            return principals[0].Element(ns + "UserId")?.Value == sid
                && action.Name == ns + "Exec"
                && CanonicalPath(action.Element(ns + "Command")?.Value ?? "") == CanonicalPath(exePath)
                && action.Element(ns + "Arguments")?.Value.Trim() == "--logon"
                && string.IsNullOrWhiteSpace(action.Element(ns + "WorkingDirectory")?.Value)
                && trigger.Name == ns + "LogonTrigger"
                && AccountMatches(trigger.Element(ns + "UserId")?.Value, sid);
        }
        catch { return false; }
    }

    private static bool AccountMatches(string? account, string sid)
    {
        if (account == sid) return true;
        if (string.IsNullOrWhiteSpace(account)) return false;
        try { return new NTAccount(account).Translate(typeof(SecurityIdentifier)).Value == sid; }
        catch { return false; }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }

    // ⚠ Scheduler HRESULTs do NOT surface as COMException: the runtime maps 0x80070002 to FileNotFoundException
    // and 0x80070005 to UnauthorizedAccessException. Match on HResult, never on the exception type, or the
    // "no task yet" and "needs elevation" branches below are dead and every first registration fails.
    private static string? ReadXml(dynamic folder, string name)
    {
        object? task = null;
        try { task = folder.GetTask(name); return (string)((dynamic)task).Xml; }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070002)) { return null; }
        finally { Release(task); }
    }

    public static bool EnsureRegistered(string exePath)
    {
        object? service = null, folder = null, registered = null;
        try
        {
            string sid = OwnerSid;
            string name = ScopedName(exePath, sid);
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!);
            ((dynamic)service!).Connect();
            folder = ((dynamic)service).GetFolder("\\");
            string? previous = ReadXml((dynamic)folder!, name);
            if (previous is not null && !OwnsXml(previous, exePath, sid)) return false;
            // TASK_CREATE or TASK_UPDATE; never overwrite an unrelated task on name collision.
            registered = ((dynamic)folder).RegisterTask(name, TaskXml(exePath, sid, sid),
                previous is null ? 2 : 4, sid, null, 3, null);
            string actual = (string)((dynamic)registered).Xml;
            if (!OwnsXml(actual, exePath, sid)) return false;
            string? legacy = ReadXml((dynamic)folder, LegacyTaskName);
            if (legacy is not null && OwnsXml(legacy, exePath, sid))
                ((dynamic)folder).DeleteTask(LegacyTaskName, 0);
            return true;
        }
        catch (Exception ex) { Trace.WriteLine($"[Recovery] task registration failed: {ex.Message}"); return false; }
        finally { Release(registered); Release(folder); Release(service); }
    }

    /// <summary>Remove only matching tasks. A narrow elevated helper can remove an admin-created task;
    /// it receives the originating SID and does no registry, driver, process, or file cleanup.</summary>
    public static bool Remove(bool allowElevation = true, string? ownerSid = null)
    {
        object? service = null, folder = null;
        string sid = ownerSid ?? CurrentSid;
        string exe = Environment.ProcessPath ?? "";
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!);
            ((dynamic)service!).Connect();
            folder = ((dynamic)service).GetFolder("\\");
            foreach (string name in new[] { ScopedName(exe, sid), LegacyTaskName })
            {
                string? xml = ReadXml((dynamic)folder!, name);
                if (xml is null) continue;
                if (!OwnsXml(xml, exe, sid))
                {
                    if (name == LegacyTaskName) continue;
                    return false;
                }
                ((dynamic)folder).DeleteTask(name, 0);
            }
            return true;
        }
        catch (Exception ex) when (allowElevation && ex.HResult == unchecked((int)0x80070005))
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(exe,
                    $"--remove-recovery-task --owner-sid {sid}")
                    { UseShellExecute = true, Verb = "runas", WorkingDirectory = Environment.SystemDirectory });
                if (process is null) return false;
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception error) { Trace.WriteLine($"[Recovery] task removal elevation failed: {error.Message}"); return false; }
        }
        catch (Exception ex) { Trace.WriteLine($"[Recovery] task removal failed: {ex.Message}"); return false; }
        finally { Release(folder); Release(service); }
    }

    private static string TaskXml(string exePath, string sid, string userName) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Starts Radiata at sign-in, or lifts a stranded controller cloak when autostart is off.</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>
              <UserId>{SecurityElement.Escape(sid)}</UserId>
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{sid}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>LeastPrivilege</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <AllowHardTerminate>false</AllowHardTerminate>
            <StartWhenAvailable>true</StartWhenAvailable>
            <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
            <IdleSettings>
              <StopOnIdleEnd>false</StopOnIdleEnd>
              <RestartOnIdle>false</RestartOnIdle>
            </IdleSettings>
            <AllowStartOnDemand>true</AllowStartOnDemand>
            <Enabled>true</Enabled>
            <Hidden>false</Hidden>
            <RunOnlyIfIdle>false</RunOnlyIfIdle>
            <WakeToRun>false</WakeToRun>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <Priority>5</Priority>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{SecurityElement.Escape(exePath)}</Command>
              <Arguments>--logon</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;

}
