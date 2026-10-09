# Installer, update feed, and release workflow

The installer is an Inno Setup script, `packaging/radiata.iss`, built by `tools/build-installer.ps1`. The setup
EXE (`Radiata-<ver>-setup.exe`) is the only published artifact. Related code: `Setup/UpdateService.cs`,
`Core/UpdateFeed.cs`, `Platform/StartupManager.cs`, `Platform/RecoveryTask.cs`, `Core/PortableUninstall.cs`.

## The identity that must never change

`AppId = {FC9B183F-8F24-4343-82FC-7D8CE9914300}` in `radiata.iss`. Windows matches the upgrade and uninstall
registration on it, and the script's `IsUpgrade` check reads the same key. Change it and every later release
installs beside the old one instead of over it.

## Shape of the install

Per-user, no UAC: `{localappdata}\Programs\Radiata`, `PrivilegesRequired=lowest`, no directory, program-group or
license page (the GPL is a distribution license, not a click-through). x64, Windows 10 and later. On a fresh
install only (`Check: not IsUpgrade`) the script seeds the HKCU Run entry `Radiata`
(`"{app}\Radiata.exe" --autostart`, the exact string `StartupManager.Format` writes). The check is load-bearing:
the installer runs on every silent update, and without the check an update would re-enable autostart for a user
who turned it off.

## Autostart

Two routes, one switch. Explorer runs Run-key entries serially, so the Run key alone starts Radiata minutes
after sign-in; a logon scheduled task (`RadiataControllerRecovery-<hash>`, `RecoveryTask`, running
`Radiata.exe --logon`) fires seconds after sign-in.

- **The Run key is the on/off switch for both.** The task reads it at launch: entry present and pointing at this
  exe, the task launch is the app; absent, it lifts a stranded HidHide cloak and exits. The checkboxes in
  Settings, the tray and onboarding therefore need no elevation.
- Both routes exit before tracing or the single-instance mutex when autostart is off, when a primary instance is
  already running, or when `%APPDATA%\Radiata\quit-marker.txt` names the current sign-in (`ExitApp` writes
  it, so closing Radiata is not undone by a late Run-key launch).
- The task is registered from XML through the Task Scheduler COM API, never `schtasks` switches (their defaults
  stop the app on battery). Its name hashes the account SID and exe path, and registration and deletion verify
  the sole principal, trigger, executable and arguments, so another install's task is left alone. Any elevated
  pass, including `--install-drivers` with both drivers already current, registers it.

## Payload layout

The publish target produces the single-file `Radiata.exe`, `drivers\` and a self-contained `ArcadeHost\`
subfolder. `build-installer.ps1` strips PDBs, refreshes `Radiata.helper-files.txt` (hashes of the helper files)
and deletes the portable-only `Uninstall Radiata.cmd`.

- A root payload addition must also go into the portable cleanup list in `Core/PortableUninstall.cs`; helper
  additions are recorded in the hashed manifest automatically.
- Inno never deletes a file an older version shipped and the new one does not: a payload file dropped from the
  build needs an `[InstallDelete]` entry.

## The silent-update contract

The in-app updater starts the downloaded setup with exactly:

```
/SILENT /CLOSEAPPLICATIONS /NORESTART /RADIATARELAUNCH=1
```

`CloseApplications=yes` lets setup replace the running tray instance. `{param:RADIATARELAUNCH|0}` controls a
`nowait` `[Run]` entry that restarts `Radiata.exe --updated`; that entry is the only way the tray returns after a
silent upgrade, and `--updated` marks the process as post-update. `RestartApplications=no`, because Radiata does
not register for Restart Manager restart. The interactive "Launch Radiata" checkbox is `postinstall skipifsilent`;
keep the two entries separate.

`PrepareToInstall` first signals the named event `Local\Radiata.QuitRequest` (the name is duplicated from
`App.xaml.cs` `QuitEventName`, and the harness asserts both sides) when the exe or the helper cannot be
opened for write, then waits for the images to release; Restart Manager is the fallback. There is no `AppMutex`:
the silent upgrade runs while the tray app is alive.

The drivers task is shown on fresh installs only, never on the updater's silent run (`Check: (not IsUpgrade) and
(not RelaunchRequested)`). It runs `Radiata.exe --install-drivers` elevated: the elevation is on `Radiata.exe`,
never on the installer, which keeps the silent update UAC-free.

## Uninstall ownership split

| Component | Deletes |
| --- | --- |
| Inno's generated uninstaller | Files under `{app}`, the Start menu shortcut, the registration |
| `Radiata.exe --uninstall-cleanup` | The Run key, the HidHide allow-list and cloak, the logon task, and the opt-in shared drivers and `%APPDATA%` |

The cleanup hook is `Exec`'d from `[Code]` at `usUninstall`, not an `[UninstallRun]` entry: `[UninstallRun]` runs
after the exe may already be deleted and the cleanup would silently not run. Do not add one.
`--uninstall-cleanup` must never touch `{app}`. Drivers are kept unless the user opts in, and the result comes
from `DriverSetup.UninstallOutcome`. The cleanup lifts the driver's whole HidHide block list when no other Radiata
copy is running, and a failed cleanup aborts the uninstall before any file is deleted.

## Update feed

`https://getradiata.app/update/latest.json`, exactly:

```json
{"version":"0.17.2150","url":"https://github.com/scumbly/radiata/releases/download/v0.17.2150/Radiata-0.17.2150-setup.exe","sha256":"<lowercase hex>","notesUrl":"https://github.com/scumbly/radiata/releases/tag/v0.17.2150"}
```

The app requests `latest.json?v=<running version>` (the only thing sent; see [../PRIVACY.md](../PRIVACY.md)) and
verifies the download against `sha256`; a mismatch is a hard reject. The `url` must sit under
https://github.com/scumbly/radiata/releases/download/ (`UpdateFeed.TrustedDownloadPrefix`), so a feed served
from a compromised web host cannot redirect installs to a foreign exe. Moving the release host means changing
that constant and `release.yml` together, and an installed copy enforces the prefix it shipped with, so a move
must keep both hosts accepted while older versions are in use.

## The release ritual

`.github/workflows/release.yml` runs on a `v<ver>` tag, checks that the tag equals the source version, runs
`tools\build-installer.ps1 -Public`, and attaches `Radiata-<ver>-setup.exe` and `latest.json` to a draft
release, after `tests.yml` has passed on the same commit. The feed is uploaded separately, after the release is
published: it points at the release asset, so advertising it first would 404. The release build adds licensed sound files that are not in this repository
([../THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md), section 4c); a build without them has no arcade sound
effects.

## Signing

Signing covers the inner executables (`Radiata.exe`, `ArcadeHost\Radiata.ArcadeHost.exe`) and the setup EXE, and
is mandatory for a release: the workflow's first job fails the run unless the `SIGNING_ENABLED` repository
variable is `true`. A manual run may tick `allow_unsigned` for a rehearsal build, which stops after compiling the
unsigned setup EXE and publishes nothing, writes no `latest.json` and drafts no release. The script runs in two
halves so the inner signing can happen between them:

1. `-Public -StageOnly` publishes into `publish\installer-staging-<guid>` and records the version and flavour in
   `staging-version.txt`.
2. Sign and verify the inner executables (`tools\verify-signature.ps1`: status `Valid` plus a timestamp).
3. `-Public -FromStaging <folder>` refreshes the helper manifest, compiles `radiata.iss`, hashes the setup EXE and
   writes `latest.json`.
4. Sign and verify the setup EXE, then rewrite `latest.json` from the signed bytes.

The order is load-bearing: signing changes the setup EXE's bytes, so `latest.json` is rewritten after step 4, and
the helper manifest after step 2, never before. The version comes from the staging record, not a fresh derive. A
local run is a plain `tools\build-installer.ps1`, which does both halves and signs nothing.
