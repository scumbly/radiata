# Build, versioning, packaging

## Everyday loop

```
dotnet build ControllerWheel.csproj -c Debug
```

Stop any running `Radiata.exe` first: building or launching over a live instance fails. After touching Settings
XAML or shared styles, run the smoke probe ([SETTINGS-UI.md](SETTINGS-UI.md)); the test commands are in
[ARCHITECTURE.md](ARCHITECTURE.md).

## Versioning

`0.<ReleaseMinor>.<build.minor>.<YYMMDD>`, which the app reports as its version and records in `savedByVersion`.

- `ReleaseMajor` and `ReleaseMinor` live in `ControllerWheel.csproj` and change by hand.
- `build.minor` is a counter in the tracked `build.minor` file. The build only reads it (the `SetBuildVersion`
  target), so a build made before a commit reports the counter as it stood at build time.
  `tools\build-installer.ps1` passes the value it read as `/p:PinnedBuildMinor`, so a commit during a build
  cannot split the payload's version from the setup file's name.
- `<YYMMDD>` is the build date. It exceeds the 16-bit field limit of `AssemblyVersion`/`FileVersion`, so the
  full four-part string is the `InformationalVersion` and the numeric versions are `<Major>.<Minor>.<build>.0`.
- A release is named after the exact version the app reports: `<ver>` is `0.<ReleaseMinor>.<build.minor>`.
- `App.OobeCurrentVersion` does not move with a release or a `ReleaseMinor` change; raising it re-runs
  first-run setup for every existing user ([ONBOARDING.md](ONBOARDING.md)).

## Packaging

The installer is the only published artifact ([INSTALLER.md](INSTALLER.md)); `tools\build-installer.ps1 -Public`
builds it. The publish target is single-file (`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`,
`EnableCompressionInSingleFile`) and applies only when a runtime identifier is set so a plain `dotnet build` stays multi-file for fast iteration. There is no trimming, because
WPF is not trim-safe. Driver installers stay beside the executable, outside the single-file bundle, and the
helper is published self-contained in the `ArcadeHost\` subfolder.

```
dotnet publish ControllerWheel.csproj -c Release -r win-x64 --self-contained true -o publish\Radiata-<ver>
```

A portable folder must carry the full publish output: `Radiata.exe`, `drivers\`, `Uninstall Radiata.cmd`, the
complete `ArcadeHost\` subfolder and `Radiata.helper-files.txt`. Windows on ARM is not supported: the HidHide
installer and the `win-x64` publish are x64 only.

## Uninstaller (portable folder)

`Uninstall Radiata.cmd` runs the app's `--uninstall` mode: a confirmation dialog, then an elevated
`--uninstall-run` that deletes the Run-key entry, the HidHide allow-list entry and cloak, and the logon task. The
shared drivers and settings are offered as off-by-default opt-ins. The folder cleanup deletes an explicit
manifest of files (`Core/PortableUninstall.cs`, plus hash-matching helper files from
`Radiata.helper-files.txt`), never a recursive wipe: a ZIP extracted into a folder that already holds other
files must not take them with it.

Driver uninstall verifies its postcondition. `RunRegisteredUninstall` matches the uninstall entry on its name
and on a Nefarius publisher (the matched `UninstallString` runs elevated), waits a bounded time, and checks
`DriverStatus` afterwards instead of trusting the exit code alone. The check is asymmetric on purpose: a kernel
driver's service key can stay pending-delete until reboot after a successful uninstall, so exit code 0 with the
driver still detected is a success, while a failing exit code with the driver still detected is a failure.
A present driver with no Nefarius entry is another program's copy and is reported as left in place.

## Command-line switches

| Switch | Purpose |
| --- | --- |
| `--logon` | What the logon task runs: if autostart is on, this launch is the app; otherwise lift an orphaned HidHide cloak and exit ([INSTALLER.md](INSTALLER.md)). |
| `--autostart` | What the Run-key entry runs. Exits silently when autostart is off, when a primary instance is up, or when the user quit earlier since signing in. |
| `--uncloak` | Lift an orphaned HidHide cloak. No-ops when the app is running (mutex-guarded). |
| `--watchdog <pid> <startFileTimeUtc>` | Self-spawned by the app: the hard-kill un-cloak watchdog ([INPUT-CAPTURE.md](INPUT-CAPTURE.md)). Never launch by hand. |
| `--hidhide-whitelist [--restart-pads <ids\|sweep\|none>]` | Elevated self-registration in HidHide, handled before the single-instance mutex. |
| `--install-drivers` | Install or update ViGEmBus and HidHide behind a small progress window. Relaunches itself elevated when it is not. |
| `--remove-recovery-task` | Delete this exe's logon task for the account and nothing else. |
| `--uninstall`, `--uninstall-run [--drivers] [--appdata]` | The portable uninstall flow. |
| `--uninstall-cleanup [--silent]`, `--uninstall-cleanup-run [--drivers] [--appdata]` | The installer's uninstall hook: clears the Windows footprint, never touches application files. |
| `--oobe` | Force the first-run wizard. |
| `--settings` | Open Settings once startup settles (first-run setup wins while pending). |
| `--updated` | Passed only by the installer's silent-update relaunch entry. |
| `--export-controls [path]` | Export the in-app Help as Markdown from `Core/HelpContent.cs`; exits immediately. |
| `--export-help-html [dir] [--css-version=N]` | Render the Help for the website, all languages. |
| `--check-help-locales [path]`, `--check-locales [path]` | Translation parity reports ([LOCALIZATION.md](LOCALIZATION.md)). |
| `--dump-help-locale <code> [path]`, `--dump-ui-locale <code> [path]` | Regenerate one language's map skeleton. |
| `--scan-library [path]` | Dump what the storefront scanners see: launcher executables, per-store games and launch URLs. |
| `Radiata.ArcadeHost.exe --xinput-count` | The helper's switch, not `Radiata.exe`'s: counts the XInput pads that process can see; exit code is `10` plus the count. |

## Generated files

- `Core/UiStrings.g.cs` is generated by `tools/ExtractStrings` from the XAML.
- `Core/GameChatButtons.cs` is generated data ([PC_GAME_TEXT_CHAT_BUTTONS.md](PC_GAME_TEXT_CHAT_BUTTONS.md)).
- `THIRD-PARTY-LICENSES.md` is an embedded resource (the About tab's licenses viewer reads it), so it must stay
  at the repository root.
