# Architecture

Radiata is one Windows app made of a WPF-free core library and a WPF shell, plus a small helper
executable. All three share one hot-reloaded JSON config.

## Projects

| Project | Output | Notes |
| --- | --- | --- |
| `ControllerWheel.csproj` | `Radiata.exe` (`net8.0-windows`, WPF + WinForms tray) | The shell: overlay, Settings, input capture, setup, platform calls. |
| `Core/ControllerWheel.Core.csproj` | `Radiata.Core.dll` (`net8.0`) | No WPF, no WinForms, no audio library. Config model, wheel state logic, action executor, localization, game logic. |
| `ArcadeHost/ArcadeHost.csproj` | `Radiata.ArcadeHost.exe` (x64, no UI) | A separate process. Its `--xinput-count` mode is the isolation probe described in [INPUT-CAPTURE.md](INPUT-CAPTURE.md). |
| `tools/TestHarness`, `tools/SettingsSmokeProbe`, `tools/ExtractStrings` | Test and build tooling | See *Build and test* below. |

The product and executable are named Radiata. The C# namespace is `ControllerWheel` in every project, and the
shell's assembly name is `Radiata`.

## Folder layout

The folders are navigation only: types stay in the flat `ControllerWheel` namespace.

| Folder | Holds |
| --- | --- |
| root | `App.xaml`, `App.xaml.cs` and its `App.*.cs` partials (tray, controller wiring, overlay lifecycle, capture engine), `GlobalUsings.cs` |
| `Core/` | The WPF-free library (see below) |
| `Overlay/` | `OverlayWindow`, `RadialMenuControl` and the per-material renderers |
| `Input/` | Raw-HID reader, controller profiles, trigger interpreter, XInput, ViGEm and HidHide management |
| `Settings/` | The Settings window, its editor controls and pickers |
| `Setup/` | First-run wizard, driver install, updater, crash reporting |
| `Platform/` | Windows integration: native calls, platform actions, startup entry, logon task |
| `Sound/` | Sound-effect engine, the xylophone melodies, audio-device and mixer helpers |
| `GameLibrary/` | Storefront scanning, the Game Grid, cover art |
| `Integrations/` | The Discord clients and the Discord and OBS setup windows (the OBS client is `Core/ObsClient.cs`) |
| `UI/` | Shared UI helpers: icons, tints, markup, localization extensions |
| `Arcade/` | The WPF side of the arcade: frame pump, renderers, chrome |
| `ArcadeHost/` | The helper executable |
| `packaging/`, `drivers/` | Installer script and bundled driver installers |

## The Core / shell boundary

`Core` must stay OS-agnostic: no WPF or WinForms types, no Windows-only calls.

- The config model (`AppConfig`, `WheelSlice`, `ActionConfig`, `SystemConfig`) and `ConfigLoader`, which
  sanitizes and canonicalizes on load.
- `WheelStateMachine`: stick to armed slice, deadzone, sticky grace, confirm dwell, and in-wheel edit logic.
- `ActionExecutor` behind `IPlatformActions`; the shell implements the interface in `WindowsPlatformActions`.
- Rules the renderer and the editors must agree on (`Materials`, `SliceThicknessRule`, `SliceLabelRule`).
- Help text and translations, the UI string catalog (`Loc`, `UiText`), `MotionPolicy`.
- Arcade simulations in `Core/Arcade/`: pure, deterministic, no clock, no drawing, no audio.

If two surfaces must agree on a rule, the rule belongs in `Core`. A local copy in each surface drifts.

## Config

`%APPDATA%\Radiata\config.json` is watched by a `FileSystemWatcher`; keys are camelCase. The schema is in
[../CONFIG.md](../CONFIG.md). Writes are atomic (a unique temp file, then one `File.Move`), so a crash cannot leave a
truncated file. When the live file cannot be parsed the loader uses `config.json.bak` (a copy taken after the
last clean parse) and keeps the unreadable file aside as `config-corrupt-<timestamp>.json`. A snapshot goes to
`backups\` when the app version changes; the newest 10 are kept.

## Build and test

```
dotnet build ControllerWheel.csproj -c Debug
dotnet run --project tools\TestHarness -c Debug -- all
dotnet run --project tools\SettingsSmokeProbe -c Debug
```

Build the app first: the harness and the probe reference its output folder. Stop any running `Radiata.exe`
before building. The harness takes group names (`all` is the default suite); `SettingsSmokeProbe` exits 0 when
every Settings editor constructs and the shared styles resolve ([SETTINGS-UI.md](SETTINGS-UI.md)).
`.github/workflows/tests.yml` runs the build, the harness, the smoke probe and the locale-parity check on every push.
