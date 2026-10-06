using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ControllerWheel;

/// <summary>Launches the bundled Nefarius driver installers elevated (UAC) for first-run / OOBE setup.
/// The ViGEmBus + HidHide installers are WiX Burn bundles → <c>/passive</c> shows a progress bar,
/// <c>/norestart</c> defers reboots. Exit codes: 0 = ok, 3010 = ok-but-reboot-needed; anything else
/// (installer error, or 1223/user-declined-UAC) is a failure. The installers ship in
/// <c>&lt;appdir&gt;\drivers\</c> (csproj Content copy). Detection is in <see cref="DriverStatus"/> +
/// <see cref="GamepadEmulator.ProbeDriverInstalled"/>; the sequencing (which to run, ViGEmBus twice-run,
/// HidGuardian cleanup first) lives in App, which owns the probes.</summary>
internal static class DriverSetup
{
    public const string ViGEmBusExe    = "ViGEmBus_1.22.0_x64_x86_arm64.exe";
    public const string HidHideExe     = "HidHide_1.5.230_x64.exe";
    public const string LegacinatorExe = "Legacinator.exe";

    /// <summary>Version of the bundled HidHide installer, parsed from its filename (single source of
    /// truth — swap in a newer bundled exe and this follows). Used to offer an in-place UPGRADE when the
    /// installed HidHide is older. (ViGEmBus exposes no comparably reliable installed-version read, so it
    /// stays presence-gated.)</summary>
    public static readonly Version BundledHidHideVersion =
        Version.TryParse(System.Text.RegularExpressions.Regex.Match(HidHideExe, @"\d+(\.\d+)+").Value, out var v)
            ? v : new Version(0, 0);

    private static bool SupportsArchitecture(System.Runtime.InteropServices.Architecture architecture) =>
        architecture == System.Runtime.InteropServices.Architecture.X64;
    public static bool SupportedOS => OperatingSystem.IsWindowsVersionAtLeast(10)
        && SupportsArchitecture(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);

    public static string DriversDir => Path.Combine(AppContext.BaseDirectory, "drivers");
    public static bool   Bundled(string exe) => File.Exists(Path.Combine(DriversDir, exe));

    /// <summary>SHA-256 of each bundled installer, pinned at build time. The drivers\ folder ships LOOSE
    /// beside the exe and these installers run ELEVATED — a swapped/corrupted file there must not be
    /// launched with admin rights on our say-so (supply-chain hardening; ViGEmBus upstream is archived,
    /// so its 1.22.0 binary is frozen forever). Swap a bundled installer → update its hash here.</summary>
    private static readonly Dictionary<string, string> BundledSha256 = new(StringComparer.OrdinalIgnoreCase)
    {
        [ViGEmBusExe]    = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A",
        [HidHideExe]     = "F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6",
        [LegacinatorExe] = "D278EB441C7DAFFE2C53A57020FB52CBD2652B872E2296D6FE5B7717E66E06AD",
    };

    /// <summary>True if the bundled file matches its pinned SHA-256. Unknown filenames are refused; a read failure fails closed — we're about to elevate it.</summary>
    private static bool VerifyBundledHash(string exe, string path)
    {
        if (!BundledSha256.TryGetValue(exe, out var expected)) return false;
        try
        {
            using var fs = File.OpenRead(path);
            var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs));
            bool ok = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            if (!ok) Trace.WriteLine($"[DriverSetup] {exe} SHA-256 MISMATCH — refusing to run it elevated (expected {expected}, got {actual})");
            return ok;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DriverSetup] {exe} hash check failed ({ex.Message}) — refusing to run it elevated");
            return false;
        }
    }

    /// <summary>WiX Burn quiet-with-progress install, reboots deferred.</summary>
    private const string BurnArgs = "/passive /norestart";

    /// <summary>Run a bundled installer elevated and wait for it. True on success (exit 0 or 3010). False if
    /// the file's missing, the user declined the UAC prompt, or the installer reported an error.
    /// <para>Must AWAIT the exit, never block on WaitForExit: the caller is the UI thread, and stalling the
    /// message pump for the length of a driver install paints "Not Responding" over the first-run wizard
    /// exactly while the user is being asked to approve UAC prompts. Awaiting keeps thread affinity
    /// unchanged — the caller still runs entirely on the UI thread.</para></summary>
    public static async Task<bool> RunElevatedAsync(string exe, string args)
    {
        if (!SupportedOS) { Trace.WriteLine("[DriverSetup] bundled driver setup requires native x64 Windows."); return false; }
        var path = Path.Combine(DriversDir, exe);
        if (!File.Exists(path)) { Trace.WriteLine($"[DriverSetup] bundled installer missing: {path}"); return false; }
        if (!VerifyBundledHash(exe, path)) return false;   // never elevate a tampered/corrupted installer
        try
        {
            using var p = Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true, Verb = "runas" });
            if (p is null) return false;
            await p.WaitForExitAsync().ConfigureAwait(true);
            bool ok = p.ExitCode is 0 or 3010;
            Trace.WriteLine($"[DriverSetup] {exe} exit={p.ExitCode} ({(ok ? "ok" : "FAIL")})");
            return ok;
        }
        catch (Exception ex)   // includes the user declining the UAC elevation prompt
        {
            Trace.WriteLine($"[DriverSetup] {exe} launch failed: {ex.Message}");
            return false;
        }
    }

    public static Task<bool> InstallViGEmBusAsync() => RunElevatedAsync(ViGEmBusExe, BurnArgs);
    public static Task<bool> InstallHidHideAsync()  => RunElevatedAsync(HidHideExe,  BurnArgs);

    /// <summary>What a driver removal actually did. Consumers word the closing dialog per driver from this,
    /// and decide whether the user has anything left to do (<see cref="NeedsNoUserAction"/>).</summary>
    public enum UninstallOutcome
    {
        /// <summary>Gone, and the probe confirms it.</summary>
        Removed,
        /// <summary>The uninstaller succeeded; Windows finishes the removal at the next restart.</summary>
        PendingReboot,
        /// <summary>Was not installed (or only a stale Apps entry remained).</summary>
        NotInstalled,
        /// <summary>The driver is present but no Nefarius uninstaller is registered — it was installed by
        /// another program (HP OMEN Gaming Hub, Oculus, Virtual Desktop, a DS4Windows-era setup) and is
        /// that program's to remove, never ours. Left in place on purpose.</summary>
        NoUninstaller,
        /// <summary>The uninstaller ran and the driver is still there (in use, or a pending restart).</summary>
        StillPresent,
        /// <summary>Could not be run at all (unparseable command, UAC declined, exception).</summary>
        Failed,
    }

    /// <summary>True when the outcome leaves the user nothing to do by hand.</summary>
    public static bool NeedsNoUserAction(UninstallOutcome o) =>
        o is UninstallOutcome.Removed or UninstallOutcome.PendingReboot
          or UninstallOutcome.NotInstalled or UninstallOutcome.NoUninstaller;

    /// <summary>Uninstall the drivers via THEIR OWN registered uninstaller (the ARP
    /// QuietUninstallString/UninstallString each driver's installer wrote). Never re-run our bundled
    /// installer copy with guessed args — a Burn bundle exe from our folder doesn't reliably match the
    /// installed product. These are SHARED drivers (DS4Windows / reWASD use them too), so removal is opt-in.
    /// Stop Radiata + free the HidHide control device first (HidHideManager.Release / the pad devnode). A
    /// driver that isn't installed is treated as success (nothing to remove). </summary>
    // ⚠ ViGEmBus's real ARP DisplayName is "ViGEm Bus Driver" — with a space, matching NEITHER "ViGEmBus"
    // nor "Virtual Gamepad Emulation". The loose "ViGEm" needle is the one that finds it; without it the
    // lookup misses and the uninstall reports the "not registered = already removed" success having done
    // nothing. The Publisher gate below is what keeps the loose needle safe.
    public static readonly string[] ViGEmBusDisplayNames = ["ViGEm", "ViGEmBus", "Virtual Gamepad Emulation"];
    public static readonly string[] HidHideDisplayNames  = ["HidHide"];

    public static UninstallOutcome UninstallViGEmBus() =>
        RunRegisteredUninstall("ViGEmBus", () => !DriverStatus.ViGEmBusInstalled(), ViGEmBusDisplayNames);
    public static UninstallOutcome UninstallHidHide()  =>
        RunRegisteredUninstall("HidHide",  () => !DriverStatus.HidHideInstalled(),  HidHideDisplayNames);

    /// <summary>Whether a Nefarius-published Apps entry exists for the product — i.e. whether the driver on
    /// this PC is one the bundled installer put there. Read-only; the origin diagnostic and the uninstall
    /// share the same match rule.</summary>
    public static bool NefariusUninstallerRegistered(params string[] displayNameAny) =>
        FindUninstallCommand(displayNameAny) is not null;

    /// <summary>Run Legacinator (removes legacy Nefarius drivers, incl. the HidGuardian that can't coexist
    /// with HidHide). It's a packed tool with no confirmed silent CLI, so we launch it elevated and let the
    /// user drive its window. Returns whether it launched.</summary>
    public static Task<bool> RunLegacinatorAsync() => RunElevatedAsync(LegacinatorExe, "");

    // ── Registered-uninstaller path (the drivers' own uninstall tooling) ─────────

    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    /// <summary>Both bundled drivers are Nefarius products, and the ARP Publisher value is the cheapest thing
    /// that distinguishes them from anything else whose DisplayName happens to contain "HidHide" or
    /// "ViGEmBus" — a wrapper, a fork, a repackaged bundle. We LAUNCH the matched UninstallString ELEVATED,
    /// so matching the wrong product is not a cosmetic error.</summary>
    private const string ExpectedPublisher = "Nefarius";

    /// <summary>Find a product's ARP uninstall command by DisplayName (any of <paramref name="displayNameAny"/>
    /// matched as a substring) AND <see cref="ExpectedPublisher"/>. Prefers QuietUninstallString; falls back
    /// to UninstallString. Null if the product isn't registered (i.e. not installed).
    /// <para>A name match with an unexpected publisher is traced and skipped, never silently used — if
    /// Nefarius ever renames itself, the log says so instead of the removal reporting "not installed".</para></summary>
    private static string? FindUninstallCommand(params string[] displayNameAny)
    {
        foreach (var root in UninstallRoots)
        {
            using var key = Registry.LocalMachine.OpenSubKey(root);
            if (key is null) continue;
            foreach (var subName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subName);
                if (sub?.GetValue("DisplayName") is not string name) continue;
                bool match = false;
                foreach (var needle in displayNameAny)
                    if (name.Contains(needle, StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                if (!match) continue;

                var publisher = sub.GetValue("Publisher") as string ?? "";
                if (!publisher.Contains(ExpectedPublisher, StringComparison.OrdinalIgnoreCase))
                {
                    Trace.WriteLine($"[DriverSetup] skipping ARP entry '{name}' — publisher '{publisher}' "
                                    + $"is not '{ExpectedPublisher}' (not the driver we installed)");
                    continue;
                }

                if (sub.GetValue("QuietUninstallString") as string is { Length: > 0 } q) return q;
                if (sub.GetValue("UninstallString")      as string is { Length: > 0 } u) return u;
            }
        }
        return null;
    }

    /// <summary>How long to wait for a silent driver uninstall before giving up on it. Never wait
    /// unbounded: a Burn bundle that decides to show UI under <c>/quiet</c> — a repair prompt, a reboot nag,
    /// a "files in use" dialog — never exits, and this is called from the Dispatcher. Generous, because a
    /// real driver removal does take a while.</summary>
    private const int UninstallTimeoutMs = 120_000;

    /// <summary>Uninstall a product through its registered uninstaller, silently, and CONFIRM it went.
    /// MSI products (msiexec) are re-expressed as <c>msiexec /x {GUID} /quiet /norestart</c>; a Burn/other exe
    /// command gets <c>/quiet /norestart</c> appended if absent.
    ///
    /// <para><paramref name="verifyRemoved"/> is the postcondition — these return values drive a user-facing
    /// "shared drivers were removed" message, and an exit code alone doesn't earn that claim. It is applied
    /// ASYMMETRICALLY on purpose: a failure code with the driver still present fails, a failure code with it
    /// actually gone passes, and a mismatch on the exit-0 path is logged rather than acted on (see the
    /// exit-code handling below for why a clean exit is still trusted).</para>
    /// <para>3010 (reboot required) and 1605 ("unknown product" — already absent) are both success and neither
    /// is verifiable at that moment.</para></summary>
    private static UninstallOutcome RunRegisteredUninstall(string label, Func<bool> verifyRemoved, params string[] displayNameAny)
    {
        var raw = FindUninstallCommand(displayNameAny);
        if (raw is null)
        {
            // No Nefarius Apps entry with the driver still present = another program's copy (the bundled
            // installer always registers one). Not ours to remove, and not a failure: the caller's copy says
            // it was left in place. Never fall back to Device Manager-style removal here — some machines run
            // HP OMEN Gaming Hub's fork of the driver, which that suite depends on.
            if (!SafeVerify(verifyRemoved, label))
            {
                Trace.WriteLine($"[DriverSetup] {label}: present but NO Nefarius uninstaller is registered — "
                                + "installed by another program; left in place");
                return UninstallOutcome.NoUninstaller;
            }
            Trace.WriteLine($"[DriverSetup] {label}: no registered uninstaller (already removed)");
            return UninstallOutcome.NotInstalled;
        }
        if (SafeVerify(verifyRemoved, label))
        {
            Trace.WriteLine($"[DriverSetup] {label}: driver already absent (stale ARP entry) — nothing to uninstall");
            return UninstallOutcome.NotInstalled;
        }

        string cmd;
        var guid = System.Text.RegularExpressions.Regex.Match(raw, @"\{[0-9A-Fa-f-]{36}\}").Value;
        if (raw.Contains("msiexec", StringComparison.OrdinalIgnoreCase) && guid.Length > 0)
            cmd = $"msiexec.exe /x {guid} /quiet /norestart";
        else
        {
            cmd = raw;
            if (!cmd.Contains("/quiet", StringComparison.OrdinalIgnoreCase) &&
                !cmd.Contains("/passive", StringComparison.OrdinalIgnoreCase) &&
                !cmd.Contains("/silent", StringComparison.OrdinalIgnoreCase))
                cmd += " /quiet";
            if (!cmd.Contains("/norestart", StringComparison.OrdinalIgnoreCase)) cmd += " /norestart";
        }

        if (!TrySplitCommand(cmd, out var exe, out var args))
        {
            Trace.WriteLine($"[DriverSetup] {label}: unparseable uninstall command '{cmd}'");
            return UninstallOutcome.Failed;
        }
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, Verb = "runas" });
            if (p is null) return UninstallOutcome.Failed;
            if (!p.WaitForExit(UninstallTimeoutMs))
            {
                // Don't kill it — a half-killed driver uninstall is worse than a slow one. Let it run on and
                // judge by the postcondition instead.
                Trace.WriteLine($"[DriverSetup] {label} uninstall still running after {UninstallTimeoutMs / 1000}s "
                                + "— no longer waiting; checking whether it's gone");
                bool goneAnyway = SafeVerify(verifyRemoved, label);
                Trace.WriteLine($"[DriverSetup] {label} uninstall timed out ({(goneAnyway ? "but the driver IS gone — ok" : "driver still present — FAIL")})");
                return goneAnyway ? UninstallOutcome.Removed : UninstallOutcome.StillPresent;
            }

            int code = p.ExitCode;
            if (code is 3010 or 1605)
            {
                // 3010 = reboot required (staged removal the probe can't see yet); 1605 = already absent.
                Trace.WriteLine($"[DriverSetup] {label} uninstall exit={code} (ok — "
                                + (code == 3010 ? "reboot required to complete)" : "was not installed)"));
                return code == 3010 ? UninstallOutcome.PendingReboot : UninstallOutcome.NotInstalled;
            }

            bool removed = SafeVerify(verifyRemoved, label);
            if (code == 0)
            {
                // ⚠ A clean exit is trusted even when the probe still sees the driver — deliberate asymmetry.
                // Both probes are registry checks (HidHide's version key, ViGEmBus's driver SERVICE key) and
                // a kernel driver's service key can sit in pending-delete until reboot while the uninstall
                // genuinely succeeded; failing here would tell users a successful removal failed. The
                // mismatch is logged so a real failure is still diagnosable.
                if (!removed)
                    Trace.WriteLine($"[DriverSetup] {label} uninstall exit=0 but still detected — "
                                    + "reporting success (likely pending reboot); re-check after restarting");
                return removed ? UninstallOutcome.Removed : UninstallOutcome.PendingReboot;
            }

            // Any other code: the postcondition decides, not the code.
            Trace.WriteLine($"[DriverSetup] {label} uninstall exit={code} removed={removed} ({(removed ? "ok" : "FAIL")})");
            return removed ? UninstallOutcome.Removed : UninstallOutcome.StillPresent;
        }
        catch (Exception ex)   // includes the user declining the (rare) nested UAC prompt
        {
            Trace.WriteLine($"[DriverSetup] {label} uninstall failed: {ex.Message}");
            return UninstallOutcome.Failed;
        }
    }

    /// <summary>Run a postcondition probe without letting it throw into the uninstall flow. A probe that
    /// can't answer returns false — "we could not confirm removal" must never read as "removed".</summary>
    private static bool SafeVerify(Func<bool> verifyRemoved, string label)
    {
        try { return verifyRemoved(); }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DriverSetup] {label}: removal check failed ({ex.Message}) — treating as NOT removed");
            return false;
        }
    }

    /// <summary>Split a command line into its executable and argument tail, honoring a leading quoted path.
    /// <para>⚠ The unquoted branch is the classic unquoted-service-path shape: `C:\Program Files\X\uninst.exe /S`
    /// split at the first space yields `C:\Program`, which ShellExecute may resolve to `C:\Program.exe` — and
    /// we launch the result ELEVATED. An UninstallString is third-party data, so never assume it quotes a
    /// spaced path: walk the candidate prefixes and take the first that actually EXISTS on disk.</para></summary>
    private static bool TrySplitCommand(string cmd, out string exe, out string args)
    {
        exe = ""; args = "";
        cmd = cmd.Trim();
        if (cmd.Length == 0) return false;
        if (cmd[0] == '"')
        {
            int end = cmd.IndexOf('"', 1);
            if (end < 0) return false;
            exe  = cmd.Substring(1, end - 1);
            args = cmd[(end + 1)..].Trim();
            return exe.Length > 0;
        }

        // Unquoted: try "C:\Program", then "C:\Program Files\X\uninst.exe", … taking the first that is a
        // real file. A bare command with no path (e.g. "msiexec.exe /x …") has no space-prefix that exists
        // on disk, so it falls through to the first-space split, which is correct for those.
        for (int sp = cmd.IndexOf(' '); sp >= 0; sp = cmd.IndexOf(' ', sp + 1))
        {
            string candidate = cmd[..sp];
            if (File.Exists(candidate))
            {
                exe  = candidate;
                args = cmd[(sp + 1)..].Trim();
                return true;
            }
        }
        if (File.Exists(cmd)) { exe = cmd; return true; }   // whole string is a path, spaces and all

        int first = cmd.IndexOf(' ');
        if (first < 0) { exe = cmd; }
        else { exe = cmd[..first]; args = cmd[(first + 1)..].Trim(); }
        return exe.Length > 0;
    }
}
