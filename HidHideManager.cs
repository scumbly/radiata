using System.Diagnostics;
using System.IO;
using Nefarius.Drivers.HidHide;

namespace ControllerWheel;

/// <summary>Cloaks the physical DualSense from other apps via the HidHide filter driver — while a
/// virtual pad is up (games/Steam see only the emulated pad: a DualShock 4 in native mode, an Xbox 360
/// pad in XBox Mode) and/or while a Radiata overlay is up
/// (so wheel/browser input doesn't pass through to the game). This is the one thing that defeats the
/// double-input problem — exclusive HID open can't, because Steam and
/// Windows.Gaming.Input read via Raw Input, which ignores file-handle locks. Radiata is
/// whitelisted so it still reads the pad. Driven through the IOCTL library, NOT the flaky CLI.
///
/// Lifecycle guarantee: whatever we cloak, we un-cloak — on <see cref="Unhide"/>, on app exit, and
/// defensively at startup — so a crash can never leave the controller hidden/dead at boot. We only
/// ever touch instance IDs WE added, and only drop the global cloak when nothing is left blocked.
///
/// ⚠ TWO HARD-WON GOTCHAS:
///
/// 1. LIST WRITES CAN FAIL SILENTLY → SELF-LOCKOUT. HidHide 1.5.x accepts unelevated writes to the driver
///    lists (AddApplicationPath / AddBlockedInstanceId / IsActive); older versions throw "Access denied
///    (0x0005)" non-elevated, swallowed by the try/catch below. The trap: if the
///    DualSense is already cloaked (e.g. from a prior session) and OUR exe isn't on the allow-list, we
///    can neither add ourselves nor lift the cloak — so HidHide hides the pad from US too and we get
///    ZERO HID reports (dead Fn, blank diagnostics; even other apps like PS Accessories go blind).
///    Renaming/moving the exe re-triggers this: the allow-list matches the exact path. The cure is the
///    one-time ELEVATED self-registration — App.WhitelistSelf relaunches the exe as the
///    --hidhide-whitelist one-shot (releasing the control device first — see #2), run from onboarding's
///    drivers step and Settings ▸ Advanced ▸ Install/Repair Drivers.
///
/// 2. THE CONTROL DEVICE IS EXCLUSIVE (single owner, kernel-enforced: WdfDeviceInitSetExclusive in the
///    driver). Only one process can hold it open at a time — but RADIATA IS NOT A HOLDER: the Nefarius
///    wrapper opens the device per call and closes it before returning (verified against v3.4.0 source;
///    its README calls the no-blocking behaviour deliberate). The HidHide Configuration Client is the
///    opposite — it opens the device at startup and holds it for its WHOLE LIFETIME — so while the client
///    (or a stuck HidHideCLI.exe) runs, every cloak call here throws HidHideDriverAccessFailedException.
///    Handled by <see cref="CloakFailure.Contended"/> + the backoff; the cure is closing the holder, never
///    driver repair. Do NOT reintroduce "Radiata owns the device while running" reasoning — it was never
///    true, and designs built on it fail exactly when the client is open.
///
/// 3. BLOCKING FILTERS *OPENS*, NOT EXISTING HANDLES. The driver checks the blocklist only in its
///    file-create path; nothing revokes a handle that is already open (verified in driver source, Logic.c).
///    A cloak that "succeeds" therefore means "future opens are denied" — a process that opened the pad
///    during any un-cloaked window (Steam does, process-wide, at pad arrival) keeps reading it until IT
///    closes the handle or the devnode restarts. This is why the wheels toggle must stay routing-only and
///    why "isolated" must never be inferred from a successful Hide alone. See
///    docs/INPUT-CAPTURE.md ▸ "The cloak blocks future opens only".</summary>
public sealed class HidHideManager : IDisposable
{
    private sealed class DriverService : IHidHideService
    {
        private readonly HidHideControlService _driver = new();
        public bool IsInstalled => _driver.IsInstalled;
        public bool IsActive { get => _driver.IsActive; set => _driver.IsActive = value; }
        public bool IsAppListInverted { get => _driver.IsAppListInverted; set => _driver.IsAppListInverted = value; }
        public IReadOnlyList<string> ApplicationPaths => _driver.ApplicationPaths;
        public IReadOnlyList<string> BlockedInstanceIds => _driver.BlockedInstanceIds;
        public void AddApplicationPath(string path) => _driver.AddApplicationPath(path);
        public void RemoveApplicationPath(string path) => _driver.RemoveApplicationPath(path);
        public void AddBlockedInstanceId(string id) => _driver.AddBlockedInstanceId(id);
        public void RemoveBlockedInstanceId(string id) => _driver.RemoveBlockedInstanceId(id);
    }

    private readonly IHidHideService? _testService;
    public HidHideManager() { }
    public HidHideManager(IHidHideService service) => _testService = service ?? throw new ArgumentNullException(nameof(service));
    private IHidHideService? _svc;
    private readonly List<string>  _hidden = new();   // instance IDs WE blocked (so we remove only ours)
    // Serialises _hidden + the cloak-state file: CrashSafeUncloak → Unhide can fire on the FAULTING thread
    // (incl. the HID reader thread, via AppDomain.UnhandledException) concurrently with a UI-thread Hide()/
    // Unhide(). Without this, the List enumeration/mutation tears and two File.WriteAllLines interleave into
    // a truncated cloaked-ids.txt — corrupting the very record the crash-recovery scheme depends on. Monitor
    // is reentrant, so Hide/Unhide can call PersistHidden while already holding it.
    private readonly object _gate = new();

    // Crash backstop: the instance IDs WE currently have cloaked, mirrored to a small file. HidHide's block
    // list persists in the DRIVER across process death, so a hard kill / power loss (which skips Unhide,
    // Dispose AND the crash hooks) would otherwise leave the DualSense hidden from every app — and the
    // next launch can't un-cloak it if the controller is disconnected then (nothing to re-enumerate).
    // Reading this file at startup lets us lift that stale cloak regardless. See PersistedCloakedIds.
    private static readonly string CloakStatePath = Path.Combine(AppPaths.AppDataDir, "cloaked-ids.txt");

    private IHidHideService? Service
    {
        get
        {
            try { return _testService ?? (_svc ??= new DriverService()); }
            catch (Exception ex) { Trace.WriteLine($"[HidHide] service unavailable: {ex.Message}"); return null; }
        }
    }

    /// <summary>Why the last cloak attempt failed. <see cref="CloakFailure.Contended"/> is the one the
    /// caller must treat differently: the driver is fine and the fix is "close the other app", not
    /// "install/repair drivers" — sending a contended user to driver repair wastes their time on a
    /// working driver. Everything else collapses to Other.</summary>
    public enum CloakFailure { None, DriverMissing, Contended, Other }

    /// <summary>Classification of the most recent <see cref="Hide"/> outcome. Reset to None on success.</summary>
    public CloakFailure LastFailure { get; private set; }

    // Contended-cloak backoff. The control device is kernel-enforced single-owner (WdfDeviceInitSetExclusive
    // in the driver), so while another process holds it — the HidHide config client holds it for its whole
    // LIFETIME — every open here fails and retrying sooner cannot win. Without this, capture's three retry
    // triggers (watchdog, retry timer, per-gesture reconcile) hammered the driver 12-66 times/min. Lives
    // HERE so every caller gets it — a per-call-site backoff already missed the Xbox path once.
    private const long ContendedBackoffMs = 15_000;
    private long _contendedUntil;

    /// <summary>The single-owner control device rejects every opener but the current holder with
    /// ERROR_ACCESS_DENIED (5). The wrapper types this: <c>HidHideDriverAccessFailedException</c> maps
    /// exactly that Win32 code (SafeFileHandleExtensions.HaltAndCatchFireOnError). Match the TYPE — the
    /// exception message embeds a FormatMessage string that is locale-dependent. The NativeErrorCode check
    /// is belt-and-braces for any other wrapper exception carrying the same code.</summary>
    private static bool IsContention(Exception ex) =>
        ex is Nefarius.Drivers.HidHide.Exceptions.HidHideDriverAccessFailedException
        || ex is Nefarius.Drivers.HidHide.Exceptions.HidHideException { NativeErrorCode: 5 };

    /// <summary>True if the HidHide driver is installed and reachable.</summary>
    public bool Available { get { try { return Service?.IsInstalled == true; } catch { return false; } } }

    /// <summary>Detect the self-lockout fingerprint (gotcha #1): the driver is actively cloaking device(s)
    /// but THIS exe is NOT on the allow-list — so HidHide hides the pad from us too and we get zero HID
    /// reports (dead Fn / blank diagnostics). Reads the driver lists directly, so it needs no connected
    /// controller and no report-counting. The cure is the elevated self-whitelist (App.WhitelistSelf).
    /// Conservative: any uncertainty (driver unreachable, no exe path, exception) returns false — we only
    /// nudge when we're confident, never cry wolf.</summary>
    public bool DetectSelfLockout()
    {
        var svc = Service;
        if (svc is null) return false;
        try
        {
            if (!svc.IsInstalled || !svc.IsActive) return false;   // no cloak engaged → not locked out
            if (svc.BlockedInstanceIds.Count == 0) return false;   // nothing blocked → nothing hidden from us
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;
            return !svc.ApplicationPaths.Contains(exe);            // blocking, but we're not whitelisted = blind
        }
        catch { return false; }
    }

    /// <summary>Whitelist this process and cloak the given device instance IDs. No-op if the driver
    /// isn't installed (emulation still works, just without double-input protection).
    /// Returns TRUE only when the ids were actually written to the driver's block list — the caller's
    /// cloak bookkeeping must track OUTCOME, not intent: an empty list, a missing driver, or a swallowed
    /// access-denied must never latch "cloaked" while the pad stays fully visible to games (bleed-through).</summary>
    public bool Hide(IReadOnlyList<string> instanceIds)
    {
        if (instanceIds.Count == 0) { LastFailure = CloakFailure.None; return false; }
        if (Environment.TickCount64 < _contendedUntil) { LastFailure = CloakFailure.Contended; return false; }
        lock (_gate)
        try
        {
            var svc = Service;
            if (svc is null || !svc.IsInstalled) { LastFailure = CloakFailure.DriverMissing; return false; }
            // Inverse mode is machine-wide configuration owned by its operator. Never switch it silently.
            if (svc.IsAppListInverted) { LastFailure = CloakFailure.Other; return false; }
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) { LastFailure = CloakFailure.Other; return false; }
            if (!svc.ApplicationPaths.Contains(exe, StringComparer.OrdinalIgnoreCase)) svc.AddApplicationPath(exe);
            if (!svc.ApplicationPaths.Contains(exe, StringComparer.OrdinalIgnoreCase))
                throw new IOException("HidHide did not retain the application allow-list entry.");

            var prior = ReadRecordedIds(); // Failure must prevent any new block without a durable record.
            var blocked = svc.BlockedInstanceIds;
            var owned = instanceIds.Where(id => !blocked.Contains(id, StringComparer.OrdinalIgnoreCase)
                || _hidden.Contains(id, StringComparer.OrdinalIgnoreCase)
                || prior.Contains(id, StringComparer.OrdinalIgnoreCase)
                || instanceIds.Any(requested => HidInstanceId.SameModel(requested, id)))
                .Concat(blocked.Where(id => instanceIds.Any(requested => HidInstanceId.SameModel(requested, id))))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            // Same-model adoption recovers unrecorded blocks, including the other transport.
            // This is a recovery policy, not proof of physical identity or exclusive ownership.
            // Two cloak managers sharing these devices are unsupported.
            var intent = prior.Concat(_hidden).Concat(owned).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!PersistHidden(intent)) { LastFailure = CloakFailure.Other; return false; }
            // Retain all intent in memory before the first write, including when a later driver call fails.
            foreach (var id in owned) if (!_hidden.Contains(id, StringComparer.OrdinalIgnoreCase)) _hidden.Add(id);
            foreach (var id in owned)
                if (!blocked.Contains(id, StringComparer.OrdinalIgnoreCase)) svc.AddBlockedInstanceId(id);
            svc.IsActive = true;
            if (!VerifyCloak(instanceIds)) throw new IOException("HidHide capture configuration could not be verified.");
            LastFailure = CloakFailure.None;
            _contendedUntil = 0;
            return true;
        }
        catch (Exception ex)
        {
            LastFailure = IsContention(ex) ? CloakFailure.Contended : CloakFailure.Other;
            if (LastFailure == CloakFailure.Contended) _contendedUntil = Environment.TickCount64 + ContendedBackoffMs;
            Trace.WriteLine($"[HidHide] Hide failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Read back the current configuration, never trusting a cached cloak across another tool's
    /// edits. This confirms future-open filtering only; pre-existing device handles remain a separate risk.</summary>
    public bool VerifyCloak(IReadOnlyList<string> ids)
    {
        lock (_gate)
        try
        {
            var svc = Service;
            var exe = Environment.ProcessPath;
            return ids.Count > 0 && svc is not null && svc.IsInstalled && svc.IsActive
                && !svc.IsAppListInverted && exe is not null
                && svc.ApplicationPaths.Contains(exe, StringComparer.OrdinalIgnoreCase)
                && ids.All(id => svc.BlockedInstanceIds.Contains(id, StringComparer.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    /// <summary>Add an application to the HidHide allow-list (so it keeps seeing cloaked devices) and set
    /// allow-list semantics. Needs ELEVATION — call from the elevated self-registration helper (gotcha #1).
    /// Add-only, per Nefarius's integration guidance. Returns true if the path is on the list afterward.</summary>
    public bool WhitelistApplication(string exePath)
    {
        var svc = Service;
        if (svc is null || !svc.IsInstalled || string.IsNullOrEmpty(exePath)) return false;
        try
        {
            if (svc.IsAppListInverted) return false; // Preserve the operator's machine-wide mode.
            if (!svc.ApplicationPaths.Contains(exePath)) svc.AddApplicationPath(exePath);
            return svc.ApplicationPaths.Contains(exePath);
        }
        catch (Exception ex) { Trace.WriteLine($"[HidHide] whitelist failed: {ex.Message}"); return false; }
    }

    /// <summary>Remove an application from the HidHide allow-list (uninstall cleanup — the inverse of
    /// <see cref="WhitelistApplication"/>). Best-effort; needs the same access as adding. Returns true if
    /// the path is absent from the list afterward.</summary>
    public bool DewhitelistApplication(string exePath)
    {
        var svc = Service;
        if (svc is null || string.IsNullOrEmpty(exePath)) return false;
        try
        {
            if (!svc.IsInstalled) return true;   // driver gone → nothing to de-list
            if (svc.ApplicationPaths.Contains(exePath)) svc.RemoveApplicationPath(exePath);
            return !svc.ApplicationPaths.Contains(exePath);
        }
        catch (Exception ex) { Trace.WriteLine($"[HidHide] dewhitelist failed: {ex.Message}"); return false; }
    }

    /// <summary>Un-cloak ahead of a driver install/upgrade or an elevated helper run. Historically this
    /// also "released the exclusive control-device handle", but the wrapper holds no handle between calls
    /// (gotcha #2) — there is nothing to release, so this is just the un-cloak plus dropping the cached
    /// service instance. Kept as a named seam because every caller is a moment where the driver stack is
    /// about to change underneath us.</summary>
    public bool Release()
    {
        bool released = Unhide();
        _svc = null;   // recreated lazily on the next call; a driver upgrade may invalidate the old instance
        return released;
    }

    /// <summary>Un-cloak everything we hid (plus any extra IDs, e.g. a prior crashed session's stale
    /// blocks of this controller), preserving the global filter setting. Bookkeeping
    /// tracks OUTCOME, not intent (mirroring <see cref="Hide"/>): an id leaves <see cref="_hidden"/> and
    /// the crash-recovery file only once the driver confirms it removed / absent — a contended control
    /// device (gotcha #2, e.g. a stray HidHideCLI.exe at exit) must not wipe the record while the pad
    /// stays blocked in the driver, or the next-startup recovery has nothing to lift.</summary>
    public bool Unhide(IReadOnlyList<string>? alsoRemove = null) => UnhideCore(alsoRemove, allCurrent: true);

    /// <summary>Remove only the requested ids that our memory or recovery record proves we own.</summary>
    public bool UnhideIds(IReadOnlyList<string> ids) => UnhideCore(ids, allCurrent: false);

    /// <summary>Release the complete block list before uninstall, including unrecorded orphans.
    /// Call only when no other Radiata copy is running. Failed removals retain recovery intent.</summary>
    public bool UnhideForUninstall()
    {
        lock (_gate)
        try
        {
            var svc = Service;
            if (svc is null) return false;
            var intent = ReadRecordedIds().Concat(_hidden)
                .Concat(svc.IsInstalled ? svc.BlockedInstanceIds : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!PersistHidden(intent)) return false;
            if (!Unhide(intent)) return false;
            return !svc.IsInstalled || svc.BlockedInstanceIds.Count == 0;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[HidHide] uninstall recovery failed: {ex.Message}");
            return false;
        }
    }

    private bool UnhideCore(IReadOnlyList<string>? ids, bool allCurrent)
    {
        lock (_gate)
        try
        {
            var recorded = ReadRecordedIds();
            var owned = new HashSet<string>(recorded.Concat(_hidden), StringComparer.OrdinalIgnoreCase);
            var target = new HashSet<string>(allCurrent ? _hidden : Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (ids is not null) foreach (var id in ids) if (owned.Contains(id)) target.Add(id);
            if (target.Count == 0) return true; // An empty helper instance must not rewrite the primary's record.
            var svc = Service;
            if (svc is null) return false;
            bool installed = svc.IsInstalled; // An exception means unknown, never absent.
            foreach (var id in target)
            {
                try
                {
                    if (installed && svc.BlockedInstanceIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                        svc.RemoveBlockedInstanceId(id);
                    if (!installed || !svc.BlockedInstanceIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    {
                        owned.Remove(id);
                        _hidden.RemoveAll(h => string.Equals(h, id, StringComparison.OrdinalIgnoreCase));
                    }
                }
                catch (Exception ex) { Trace.WriteLine($"[HidHide] unhide '{id}' failed: {ex.Message}"); }
            }
            // Retain failed ids even when only some removals succeeded. Never disable another tool's
            // global filter switch, even if the list happens to be empty at this instant.
            bool persisted = PersistHidden(owned.ToArray());
            bool complete = persisted && !target.Any(owned.Contains);
            if (!complete) Trace.WriteLine("[HidHide] un-cloak incomplete; recovery record retained");
            return complete;
        }
        catch (Exception ex) { Trace.WriteLine($"[HidHide] Unhide failed; recovery record retained: {ex.Message}"); return false; }
    }

    private static IReadOnlyList<string> ReadRecordedIds() => File.Exists(CloakStatePath)
        ? File.ReadAllLines(CloakStatePath).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        : [];

    /// <summary>Persist recovery intent before driver writes. Failure is returned to the caller; it
    /// must never proceed to hide an unrecorded controller. Not <see cref="AtomicFile"/>: this flushes
    /// the temp file to disk before the move, which that shared helper doesn't do — losing this write to
    /// a crash mid-write would strand a controller cloaked with no recovery record.</summary>
    private bool PersistHidden(IReadOnlyList<string> ids)
    {
        lock (_gate)
        {
            string? tmp = null;
            try
            {
                if (ids.Count == 0)
                {
                    if (File.Exists(CloakStatePath)) File.Delete(CloakStatePath);
                    return true;
                }
                Directory.CreateDirectory(AppPaths.AppDataDir);
                tmp = CloakStatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
                    foreach (var id in ids) writer.WriteLine(id);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(tmp, CloakStatePath, overwrite: true);
                return true;
            }
            catch (Exception ex) { Trace.WriteLine($"[HidHide] persist cloak state failed: {ex.Message}"); return false; }
            finally { if (tmp is not null) try { File.Delete(tmp); } catch { } }
        }
    }

    /// <summary>Instance IDs a prior session recorded as cloaked (see <see cref="PersistHidden"/>). The
    /// caller passes these to <see cref="Unhide"/> at startup to lift a cloak a crash/kill left behind,
    /// since our in-memory list starts empty and the controller may be disconnected (un-enumerable) now.
    /// Empty if there's no state file.</summary>
    public static IReadOnlyList<string> PersistedCloakedIds()
    {
        try
        {
            return File.Exists(CloakStatePath)
                ? File.ReadAllLines(CloakStatePath).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                : [];
        }
        catch { return []; }
    }

    /// <summary>Restart pad HID devnodes (pnputil /restart-device) so a freshly installed HidHide CLASS
    /// filter actually enters their driver stacks. ⚠ GOTCHA #3: HidHide registers as a HIDClass upper
    /// filter, but a class filter only joins a device's stack when that device (re)starts — and our
    /// installer runs /norestart, so on a clean machine the cloak is fully configured (blocked + active +
    /// whitelisted) yet filters NOTHING until a reboot. Restarting the devnodes here attaches the filter
    /// immediately. Needs ELEVATION — call from the elevated one-shot (--hidhide-whitelist).
    /// <paramref name="instanceIds"/>: restart exactly these devnodes (the caller's PHYSICAL pad).
    /// Null/empty falls back to sweeping every present VID-0x054C device — the sweep excludes Radiata's
    /// own ViGEm virtual DS4 (VID 054C PID 05C4) via <see cref="ControllerProfile.ShouldSkipHidDevice"/> so it
    /// can't restart the virtual pad a game is bound to. Still intended only for a fresh HidHide
    /// install/upgrade. Returns how many devnodes restarted OK.</summary>
    public static int RestartPadDevices(IReadOnlyList<string>? instanceIds = null)
    {
        int restarted = 0;
        try
        {
            var ids = instanceIds is { Count: > 0 }
                ? instanceIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : HidSharp.DeviceList.Local.GetHidDevices(0x054C)   // fallback sweep: physical Sony pads only
                    .Where(d => !ControllerProfile.ShouldSkipHidDevice(d))   // never restart our own ViGEm virtual pad
                    // Supported controller PIDs only — Sony's VID covers other peripherals (headsets,
                    // remotes) that a repair sweep has no business restarting.
                    .Where(d => ControllerProfile.All.Any(p => p.ProductIds.Contains(d.ProductID)))
                    // Full links only: each id goes to an elevated pnputil /restart-device.
                    .Select(d => HidInstanceId.FromInterfacePath(d.DevicePath, requireFullLink: true))
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            foreach (var id in ids)
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo("pnputil.exe", $"/restart-device \"{id}\"")
                    { UseShellExecute = false, CreateNoWindow = true });
                    p?.WaitForExit(15000);
                    bool ok = p?.ExitCode == 0;
                    if (ok) restarted++;
                    Trace.WriteLine($"[HidHide] restart-device '{id}' → exit {p?.ExitCode}");
                }
                catch (Exception ex) { Trace.WriteLine($"[HidHide] restart-device '{id}' failed: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[HidHide] pad enumeration for restart failed: {ex.Message}"); }
        return restarted;
    }

    /// <summary>Exit-path teardown (App.TearDown): un-cloak and drop the service reference WITHOUT
    /// <see cref="Release"/>'s forced GC.Collect + WaitForPendingFinalizers — the process is about to
    /// die, so the OS frees the exclusive control-device handle anyway, and a wedged driver must not be
    /// able to hang session end in an unbounded finalizer wait (the same precaution CrashSafeUncloak
    /// takes). Use <see cref="Release"/> only when the process KEEPS RUNNING and something else needs
    /// the device now (installer / config client / elevated helper).</summary>
    public void Dispose()
    {
        Unhide();
        _svc = null;
    }
}
