# Installer, update feed, and crash reporting

**Read this when:** touching `packaging\radiata.iss`, `tools\build-installer.ps1`,
`packaging\webhost\` (only `latest.json.sample` is in the public source; the rest is the maintainer's
snapshot of the website, development repository only), the updater/crash-report app code, or anything that
changes what ships in the payload. The installer is the only published artifact; the portable ZIP flow is a
separate, dev/tester-only ritual — that's [BUILD-RELEASE.md](BUILD-RELEASE.md).

## The identity that must never change

`AppId = {FC9B183F-8F24-4343-82FC-7D8CE9914300}` in `radiata.iss`. It is the upgrade and
uninstall identity: Windows matches the HKCU uninstall key
`…\Uninstall\{FC9B183F-8F24-4343-82FC-7D8CE9914300}_is1` on it, and the setup's `IsUpgrade()`
[Code] check reads that same key to tell fresh installs from upgrades. **Change it and every
future release installs beside the old one instead of over it.** Version churn is fine; the GUID
is forever.

## Shape of the install

Per-user, no UAC: `{localappdata}\Programs\Radiata`, `PrivilegesRequired=lowest`, registration
under HKCU, a Start menu shortcut, and — **fresh installs only** (`Check: not IsUpgrade`) — the HKCU
**Run key** (value `Radiata`, `"{app}\Radiata.exe" --autostart`, the exact format `StartupManager.Format`
writes; see *Autostart* below), so
abandoning the OOBE wizard still leaves an app that comes back after a reboot. The gate is load-bearing:
the installer runs on every silent update, and without it an update would resurrect autostart for a user
who turned it off. OOBE's finish step and the Settings/tray checkboxes own the setting afterwards;
removal stays `--uninstall-cleanup`'s alone (no `uninsdeletevalue` — see the ownership split below).
x64 only, Windows 10+. No directory page, no program-group page, **no license page** — GPL is a
distribution license, not an EULA, and presenting it as a click-through misstates it.

## Autostart — two routes, one switch

Explorer runs `HKCU\Run` entries **serially, in creation order, waiting for each app to go idle**; Radiata's
entry is the newest on any machine, so via the Run key alone it starts minutes after logon and always
*after* Steam — the exact "Steam predates Radiata" condition the sentry exists to fight. So autostart is
two routes that agree on one switch:

| Route | Fires | Launch | Exists when |
| --- | --- | --- | --- |
| **Logon scheduled task** `RadiataControllerRecovery-<scope hash>` (`RecoveryTask`) | seconds after sign-in, independent of Explorer's queue | `Radiata.exe --logon` | any elevated pass has run: setup's `--install-drivers` (**including when both drivers are already current** — that exit registers the task and refreshes the HidHide allow-list rather than returning early, because a reinstall always lands there and it is the only elevated pass a fresh install gets), the OOBE drivers step, Install/Repair Drivers, or the lockout card. A logon trigger needs elevation to create. |
| **Run key** (value `Radiata`) | when Explorer reaches it, minutes later | `"…\Radiata.exe" --autostart` | the installer seeded it (fresh install) or the checkbox is on |

**The Run key is the ON/OFF switch for both.** The task reads it at launch: entry present and pointing at
this exe → the task launch *is* the app; absent → the task lifts a stranded cloak (the old `--uncloak`
job) and exits. That is why the Settings / tray / OOBE checkbox needs no elevation, why the task survives
Wipe App Data, and why a machine without drivers (no task) still autostarts via the Run key.
**Windows' own Startup-apps toggle governs both**: Explorer honours it for the Run key; the task asks
`StartupApproved\Run` and treats "disabled" as off.

Both routes exit **before tracing or the mutex, with no show-request** when autostart is off, when a
primary is already up (the Run-key launch after the task has started the app), or when
`%APPDATA%\Radiata\quit-marker.txt` names the current logon session — `ExitApp` writes it, so a user who
closes Radiata is not surprised by the Run-key launch three minutes later. The marker is keyed on the
logon session's authentication id and expires by itself at the next sign-in; a crash writes none, so the
late Run-key launch then restarts the app. A manual launch carries neither switch and always starts.

The task name includes a hash of the originating account SID and canonical executable path. Registration/deletion verify the sole principal, trigger, executable and arguments through Task Scheduler COM; a foreign legacy task is preserved and only a matching legacy task is migrated.

The task is registered from XML through COM, never from `schtasks` switches: the switch defaults would stop the app on
battery, refuse to start it on battery, kill it after 72 hours and run it below normal priority.
`TestHarness package` asserts the XML and the Run-key format; the two are also asserted against
`radiata.iss` `[Registry]`, which must seed `StartupManager.Format`'s exact string.

**Theme:** the wizard is recoloured to the OOBE palette (App.xaml `Ui*`/`HeadingInk`) via
`ApplyRadiataTheme` in `radiata.iss` `[Code]`, plus flower wizard bitmaps in
`packaging\assets\` (compile-time only, not payload). Regenerate the BMPs with
`tools\make-wizard-bmps.ps1` (development repository only, not in the public source) if the flower mark or
palette changes. ⚠ Those BMPs are drawn on
`RadiataWindowBg` so they carry no background of their own; the value is duplicated in the generator
and must move with the `radiata.iss` constant. Keep the
TColor constants in `radiata.iss` in sync with App.xaml (VCL colours are `$BBGGRR`, byte-reversed
from XAML hex). Buttons stay native Windows controls on purpose: skinning DLLs (VCL Styles) were
rejected as a supply-chain/signing risk for a cosmetic gain.

## Payload layout — and the dual-maintenance trap

The app's publish target produces the single-file Radiata executable, drivers, and the complete
ArcadeHost payload in an `ArcadeHost\` subfolder. A self-contained app publish also publishes the helper
self-contained. The installer script publishes once, strips PDBs, refreshes
`Radiata.helper-files.txt` with the hashes of the remaining helper files, and removes the portable-only
`Uninstall Radiata.cmd`.

Each installer build creates a new `installer-staging-<guid>` folder. The script does not stop running
Radiata instances or recursively delete a previous staging folder. If another process locks compiler
output, the build fails for the operator to resolve. `-OutputDirectory <path>` puts staging, the setup
executable, and latest.json under a chosen directory; the default remains `publish`.

- Root payload additions still require review of the portable cleanup list in `Core/PortableUninstall.cs`.
  Helper-subfolder additions are recorded automatically in the hashed manifest. Cleanup preserves
  unknown or modified helper files and refuses unsafe paths.
- Development builds retain the loose helper layout; published builds use the subfolder. Discovery
  prefers `<install dir>\ArcadeHost\Radiata.ArcadeHost.exe` and falls back to the loose development
  helper. A published self-contained helper must include its local hostfxr/coreclr runtime; packaging
  fails if these files are missing. Validate startup on a machine without an installed runtime before release.

- ⚠ **ArcadeHost ships in the PUBLIC payload too, although a public build offers no script games**
  (`Core\ReleaseGates.ArcadePackages`): `XboxBtIsolationProbe` runs `Radiata.ArcadeHost.exe
  --xinput-count` for the Bluetooth Xbox cloak gate, so the helper, Jint and Acornima (and their
  THIRD-PARTY-LICENSES rows) stay. It is never launched for a game there because none can register.
- ⚠ The install dir is under the user profile, which an AppContainer token cannot execute from —
  so the coordinator's stage-copy to `C:\Radiata-ArcadeHost-<hash>` is the **production**
  mechanism for installed builds, not a dev shim. The stage must
  carry the helper's whole closure — for the installed layout that is the entire self-contained
  `ArcadeHost\` subfolder, for a dev build the `ScriptSessionCoordinator.HelperFiles` list; a
  change to either closure must be reflected in the coordinator's stage-copy.
- ⚠ Inno never deletes files the previous version shipped and the new one doesn't. If a payload
  file is ever *removed* across versions, add an `[InstallDelete]` entry for it or upgrades keep
  serving the stale copy.

## The silent-update contract

The in-app updater launches the downloaded setup with exactly:

```
/SILENT /CLOSEAPPLICATIONS /NORESTART /RADIATARELAUNCH=1
```

Per-user + `lowest` means no UAC; `CloseApplications=yes` lets it replace the running tray
instance; the `RADIATARELAUNCH=1` param is read by a [Code] check
(`{param:RADIATARELAUNCH|0}`) that gates a `nowait` [Run] entry restarting `Radiata.exe` **with
`--updated`** — that entry is the only way the tray comes back after a silent upgrade, and the flag is
load-bearing: it marks the relaunched instance as the post-update process, one of the three signals
(with the consumed Restart-Steam marker and `ConfigLoader.VersionChangedAtLoad`) that quiet the sentry
card for the 90 s swap window.
`RestartApplications=no` on purpose: Radiata doesn't register for Restart Manager restart (WER
restart was rejected as a mechanism), so Inno's own mechanism can't do it.
The interactive "Launch Radiata" checkbox is `postinstall skipifsilent` and never fires on the
silent path — don't merge the two entries.

## Closing a running Radiata before files are replaced

`radiata.iss` `[Code]` `PrepareToInstall` runs before any file is copied. If `Radiata.exe` or
`ArcadeHost\Radiata.ArcadeHost.exe` under `{app}` cannot be opened for write (a running image holds it),
it signals the named event `Local\Radiata.QuitRequest` (via `OpenEventW`/`SetEvent`; the name is duplicated
from `App.xaml.cs` `QuitEventName`, and `TestHarness package` asserts both sides). The tray's wait handle
runs `ExitApp` (the same clean path as tray Exit, so an unsaved-wheel-edits prompt can still appear), then
setup polls up to ~15 s for the images to release. Restart Manager (`CloseApplications=yes`) stays the
fallback, and the step is a no-op when nothing is running (silent-update path: the app has already exited)
and is not part of uninstall. A build that predates the event ignores the signal, so the first hop from such
a build still closes via Restart Manager.

Why: an installed 0.16 process closed by Restart Manager mid-copy logged an unhandled `DllNotFoundException`
from a native CRT uninitializer (`__scrt_uninitialize_type_info`, called from
`<CrtImplementationDetails>.ModuleUninitializer.SingletonDomainUnload`) during shutdown, and the crash
reporter persisted it as a report offered on next launch. No mixed-mode (C++/CLI) assembly ships in our
payload (scanned Debug output and the staged publish for the CRT module-uninitializer symbols), so that
module is not ours; whatever loads it, its teardown must not run against a half-replaced install.
`CrashReporter.TryWritePending` therefore also ignores exceptions once `MarkShuttingDown()` has run
(`TearDown`, `OnExit`) or `Environment.HasShutdownStarted` is true. The in-app update path
(`UpdateService.LaunchInstallerAndExit` starts setup, then `ExitForUpdate` = `ExitApp`) already exits
cleanly, and the setup-side wait now covers the window where setup starts before the exit finishes.

## Uninstall ownership split

Two owners, no overlap:

| Owner | Removes |
| --- | --- |
| Inno's generated uninstaller | files under `{app}`, the Start menu shortcut, the HKCU Apps registration |
| `Radiata.exe --uninstall-cleanup` (Exec'd from `[Code]` at `usUninstall`, wait-until-terminated) | Windows/controller state: Run key, HidHide allow-list + cloak, recovery task, and the opt-in shared-driver / `%APPDATA%` removal |

⚠ **The cleanup hook is deliberately NOT an `[UninstallRun]` entry:**
that section executes *after* `usUninstall`, where `TryDeleteRadiataExe` had already deleted the exe
whenever the app wasn't running — `CreateProcess failed (2)`, and the entire cleanup silently skipped
(no options dialog, an orphaned Run key, the drivers opt-in never offered). It only ever worked when a
running tray instance kept the exe locked past the early delete, which is why an uninstall run against a
live copy passed while one against a closed app did not. The `[Code]` Exec runs deterministically before the
delete. Don't reintroduce an `[UninstallRun]` cleanup entry.

`--uninstall-cleanup` must exit **without touching `{app}`** — the files are the uninstaller's.
Drivers are retained by default on uninstall (other tools may depend on ViGEmBus/HidHide);
removal stays an explicit opt-in inside the cleanup flow, with its postcondition checks intact
([BUILD-RELEASE.md](BUILD-RELEASE.md) ▸ Driver removal).

**Driver messaging belongs to the hook, not the script.** When the drivers opt-in is taken, the elevated
worker shows **one** dialog built per driver from `DriverSetup.UninstallOutcome` — removed (restart finishes
it) / not installed / *left in place, another program's copy* (a present bus with no Nefarius Apps entry, e.g.
HP OMEN Gaming Hub's fork — exit code 0) / could not be removed (exit code 5, the Settings ▸ Apps remedy).
`radiata.iss` logs the hook's exit code and shows **no** driver line: its old `DeleteFlag` probe announced
"the drivers are uninstalled" right after the hook had said ViGEmBus could not be removed. Don't reintroduce a
`DriversReboot`-style message. The worker also closes its own `radiata-trace-elevated.log` before an opt-in
app-data wipe — the open handle used to make the recursive delete fail on that one file.

Cleanup lifts the driver's **entire** HidHide block list, not just Radiata's recorded ids: the
cloak manager is leaving, and any id left blocked is a device invisible to every app with nothing
around to notice (an unrecorded orphan survived exactly this path in the first live uninstall run). This also releases other tools' blocks: concurrent cloak management is unsupported,
and those tools may need reconfiguration or relaunch. When another Radiata copy is running, shared
blocks and drivers are left to it. Cleanup first persists recovery intent; a failed removal aborts
uninstall before files/settings are removed, leaving a retry path.

`Radiata.exe` deletion gets **four** attempts: a **timed retry** in
`CurUninstallStepChanged(usUninstall)` (20 × 500 ms, `TryDeleteRadiataExe`), the log-based pass,
a narrow `[UninstallDelete]` entry, and a `usPostUninstall` backstop (same retry, then
`RemoveDir`). The AV on-exit scan of the just-terminated 100 MB image holds its section for
several seconds; the log pass and `[UninstallDelete]` land within ~1 s of each other and both
failed in two live runs, while a timed retry closed it (13/13 at 0.14.828). The **usUninstall**
retry is the one that keeps the UI honest: Inno's "some elements could not be removed" flag
latches on the **first** failed delete and never clears, so the exe must be gone **before** the
log pass or the user sees a partial-success message over a clean disk. If files genuinely remain
after everything, `usPostUninstall` enumerates them (`ListLeftovers`, skipping `unins000.*`) and
shows an explicit list instead of leaving only Inno's vague warning.

**The closing message itself is corrected too.** The recurring false alarm tracked the
**drivers opt-in** exactly (three controlled runs): a kernel driver mid-removal cannot finish without a
reboot — its service key sits at `DeleteFlag=1` until then — so at dialog time something genuinely
wasn't finished, and Inno's stock advice ("These can be removed manually") named the one remedy the
user cannot perform. Two changes: `usPostUninstall` probes `Services\ViGEmBus`/`HidHide` for
`DeleteFlag=1` (read-only, unelevated-safe) and, when pending, says a **restart** completes driver
removal; and `[Messages] UninstalledMost` replaces the stock text with the same reboot guidance (it
cannot be conditionally suppressed from `[Code]`, so the static text has to be accurate for both real
cases — driver pending-reboot, and a briefly AV-held file the later passes delete anyway). Keep all four; do not widen
`[UninstallDelete]` to `filesandordirs` on `{app}` — the Inno docs warn against that for good
reasons. **No `AppMutex`**: Setup enforces it too, and the silent upgrade runs while the tray app
is deliberately still alive (`CloseApplications` owns that) — an `AppMutex` would abort every
one-button update. The elevated-watchdog lock case is covered in code instead:
`KillOtherRadiata` falls back to `QueryFullProcessImageName` when `MainModule` is denied across
an elevation boundary. All three `runas` self-spawns set `WorkingDirectory` to `%TEMP%` so no
child holds `{app}` as its CWD while the folder is being deleted.

## The drivers task

`[Tasks] drivers` — "Install drivers (recommended)", **checked by
default, shown on fresh installs only and never on the updater's silent run**
(`Check: (not IsUpgrade) and (not RelaunchRequested)`). The second clause matters when the running
copy is portable (ZIP / dev): to Inno that update IS a fresh install, and without the clause the
"silent" update stalls on the drivers task's UAC prompt. It runs
`{app}\Radiata.exe --install-drivers` via `shellexec` + `Verb: runas`, `waituntilterminated`:
**the elevation happens on Radiata.exe, never on the installer** — the setup process stays
unelevated for its whole life, which is what keeps the silent update UAC-free. OOBE remains the
fallback for anyone who unchecks it.
⚠ **A reinstall almost always finds both drivers already installed** (they are machine-wide and a Radiata
uninstall keeps them), so that "nothing to install" path is the common one, not the rare one. It still does
the elevated half — HidHide allow-list + `RecoveryTask.EnsureRegistered` — while it holds the elevation;
returning early there left fresh installs with no logon task at all.

## Update feed + release ritual

Static file `https://getradiata.app/update/latest.json`, exactly:

```json
{"version":"0.13.87","url":"https://github.com/scumbly/radiata/releases/download/v0.13.87/Radiata-0.13.87-setup.exe","sha256":"<lowercase hex>","notesUrl":"https://github.com/scumbly/radiata/releases/tag/v0.13.87"}
```

The app requests it as `latest.json?v=<running version>` (access logs double as the
only version telemetry, see [../PRIVACY.md](../PRIVACY.md)) and verifies the download against
`sha256` before running it; a mismatch is a hard reject. **The `url` must sit under
`https://github.com/scumbly/radiata/releases/download/`** (`UpdateFeed.TrustedDownloadPrefix`): the
feed and the binary are deliberately held behind two unrelated accounts, so a compromised web host
alone cannot redirect installs to a foreign exe. Moving the release host means changing that constant
AND `release.yml` together, and shipping the app change one release BEFORE the feed moves.

⚠ **The pin strands old installs, so widen to an allow-list and never swap.** An install predating the
constant change enforces the OLD prefix, so it rejects a feed pointing at the new host as malformed —
and having rejected it, it can never receive the app update that would teach it the new host. Any move
must keep BOTH hosts accepted, and keep the old host's assets published for as long as any older version
is plausibly in the wild. Note also that `update/` is the wrong home for a self-hosted setup exe: it is
the one directory the crash-report endpoint (`crash.php`) writes into, so a self-hosted binary belongs in a
dedicated `/download/` directory PHP never touches, with `AddType application/octet-stream .exe` added to
the site's `docroot.htaccess` (which today declares no `.exe` MIME or cache rule; both files live in the
maintainer's site snapshot, development repository only).

**Ritual:** `tools\build-installer.ps1` (produces the setup exe + `publish\latest.json`) → attach
the setup exe to the GitHub release tagged `v<ver>` → upload `latest.json` to the site's
`update/` directory, **in that order** — the feed points at the GitHub URL, so uploading the feed
first advertises a 404. The public release workflow, `.github/workflows/release.yml`, runs the same script
with `-Public` on a `v<ver>` tag, checks that the tag equals the source version, and attaches both files to
a draft release; it never uploads the feed. Its build is the split run described under *Signing*: stage,
sign the inner executables, compile the setup exe, sign it, verify, then rewrite `latest.json` from the
final bytes. Web-host specifics are maintainer process
(`packaging\webhost\DEPLOY.md`, development repository only). Crash reports POST to
`https://getradiata.app/update/crash.php` (plain text, ≤64 KB, opt-in: per event, or automatically once the user ticks "Remember this choice" on Send).

**The Arcade's sound set travels separately.** `Assets\sfx\arcade\*.wav` is licensed material that is never
in the public repository (THIRD-PARTY-LICENSES.md §4c), and `release.yml` builds from the public repository —
so it fetches the folder at build time from the repo secret `ARCADE_SFX_URL` and verifies the zip against the
repo variable `ARCADE_SFX_SHA256`. **Whenever a sample changes** (maintainer process: a manifest edit plus a
`tools\SfxIngest` run): run `tools\pack-arcade-assets.ps1`, upload `publish\arcade-assets.zip` to the private
URL, and update the variable with the printed hash — before tagging (`tools\SfxIngest`,
`tools\pack-arcade-assets.ps1` and `tools\MusicIngest` are development repository only, not in the public
source). A stale hash fails the build loudly; an unset `ARCADE_SFX_SHA256` variable skips the step and ships
games without sound effects. The local `build-installer.ps1` ritual needs none of this.

The music beds, `Assets\music\*.m4a`, are CC BY-SA 4.0 (§4d) and **are** in the public repository — the
maintainer's export script (`tools\export-public.ps1`, development repository only) lists them, so a new
track needs only a `tools\MusicIngest` run and a commit. The creator's `Assets\music\source\*.mp3` stay in
the development tree only; they are never built or published.

⚠ **The music is ~12 MB of the setup exe** (mono 64 kbps AAC, re-encoded by `tools\MusicIngest`). That is the
cost of embedding it; the beds play by default, so nearly every player hears it, and every download carries it.

## Wizard languages

`[Languages]` lists English plus Inno's official Spanish, German and Japanese translations;
`ShowLanguageDialog=yes` always shows the picker (Inno suppresses it under `/SILENT`, so a silent update never
asks); don't switch it back to `auto`, whose detection can land on a non-English default with no dialog and no
way to change it. Arabic uses Inno's official `Arabic.isl`, which declares `RightToLeft=yes`, so Inno mirrors
the wizard pages itself; the eight custom strings are machine-translated and the mirrored layout is unverified until
someone runs the Arabic installer. ⚠ Every
`[Messages]` override and every string a user reads must carry a per-language form (`spanish.FinishedLabel=`,
`{cm:InstallDrivers}`, `CustomMessage('LeftoversIntro')` in `[Code]`); a bare entry shows English inside a
Spanish wizard, which is exactly what the two original `[Messages]` blocks and the `[Tasks]`/`[Run]`
descriptions did. The install README in `packaging/` is not part of the payload and stays English.

## Signing

Still the 1.0 gate, unchanged, and it covers **both** artifacts: the inner executables and the outer
`Radiata-<ver>-setup.exe`, via Azure Artifact Signing. Signing only the inner executables leaves the file
users actually double-click flagged as an unknown publisher; signing only the setup exe leaves the
installed `Radiata.exe` unsigned. The bundled Nefarius driver installers are already signed.

`release.yml` runs `tools\build-installer.ps1` in two halves with the inner signing between them. The
signing and verification steps are gated on the `SIGNING_ENABLED` repo variable; with it unset every
other step still runs and the setup exe ships unsigned, carrying the same payload a one-shot run packs.

1. **Stage:** `-Public -StageOnly` derives `<ver>`, publishes into `publish\installer-staging-<guid>`,
   strips PDBs, records the version and flavour in `staging-version.txt` inside that folder, prints the
   folder and writes it to `publish\staging-path.txt`. It does not refresh the helper manifest (the
   publish target's own copy is still in the folder) and writes no `latest.json`.
2. **Sign the inner executables** in the staging folder: `Radiata.exe` and
   `ArcadeHost\Radiata.ArcadeHost.exe` (one `azure/artifact-signing-action` step, several `files:` lines).
3. **Verify both** with `tools\verify-signature.ps1`: status `Valid` plus an RFC 3161 timestamp. An unsigned
   or untimestamped inner exe fails the run before it is packed.
4. **Compile:** `-Public -FromStaging <folder>` refreshes `Radiata.helper-files.txt`, removes the ZIP-only
   files, runs the public Help silo check, compiles `radiata.iss` from the folder, hashes the setup exe and
   writes `latest.json`.
5. **Sign the setup exe** and verify it.
6. **Rewrite `latest.json`** from the signed setup exe's SHA-256.

Constraints that make the order load-bearing:

- **`latest.json` is rewritten after step 5** because signing changes the setup exe's bytes, so the hash
  step 4 wrote no longer matches the file users download, and the in-app updater would reject it. The
  rewrite also runs when signing is off, where it reproduces the same hash.
- **The helper manifest is refreshed in step 4, never in step 1.** `Radiata.helper-files.txt` hashes the
  ArcadeHost files as shipped and signing rewrites the helper exe, so a manifest taken before signing
  describes bytes that never ship. The portable cleanup removes helper files only on a hash match.
- **The version comes from the staging record, not from a fresh derive.** `-FromStaging` reads it back,
  so a commit that bumps `build.minor` between the halves cannot give the setup exe a different name
  from the payload's own version (the publish itself is pinned with `/p:PinnedBuildMinor`). It also
  requires `-Public` to match the flavour the folder was staged with.
- **`staging-version.txt` is not payload.** The compile step moves it out of the folder while ISCC packs
  it and puts it back, so a failed compile can be re-run from the same folder.
- Only the two executables are signed here. The .NET runtime files beside the helper arrive signed;
  `Radiata.ArcadeHost.dll`, the script-engine assemblies and the loose `drivers\` folder keep whatever
  signature they arrived with.

A local run is a plain `tools\build-installer.ps1`, which does both halves back to back and signs
nothing; to sign by hand, run the halves separately and sign the two executables in between. Until
signing is enabled, testers get SmartScreen on the setup exe exactly as they do on the ZIP.
