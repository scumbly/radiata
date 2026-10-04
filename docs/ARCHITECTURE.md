# Architecture & codebase layout

**Read this when:** orienting in the codebase, deciding where new logic belongs, or touching the
`Core` ↔ WPF-shell boundary.

One app: a **WPF-free core** plus a **WPF shell**, sharing a single hot-reloaded JSON config.

For the flat per-file map underneath this shape — every file, its types, one-line purpose — see
[CODE-ATLAS.md](CODE-ATLAS.md).

## Stack, and why

**C# / .NET 8, WPF.** Everything the product needs is first-class: raw HID read (HidSharp) for the Fn
buttons; a transparent click-through topmost overlay *and* a native settings window in one process; `.exe`
icon extraction; system volume (NAudio / CoreAudio); ViGEmBus emulation + HidHide cloaking (Nefarius);
process control; app launch; tray icon.

Rejected: **Electron** (heavy runtime for a couch overlay, and it still needs native HID/audio modules) and
**pure AutoHotkey** as the trigger (XInput can't see the Fn buttons).

## `Core/` — `ControllerWheel.Core` (no WPF, no NAudio)

The seam a future macOS shell (aspirational) or a new controller reuses. **Keep it OS-agnostic.**

- Config model — `AppConfig` / `WheelSlice` / `ActionConfig` / `SystemConfig` — plus `ConfigLoader`
  (sanitize + canonicalize on load).
- `IController` (controller abstraction + per-device trigger), `TriggerConfig` / `TriggerModes`.
- `ActionExecutor` + `IPlatformActions`, `SequenceFormat`, `ObsClient`.
- `WheelStateMachine` — stick → armed / deadzone / sticky / confirm-dwell, plus the in-wheel edit logic and
  animation state.
- `Materials`, `SliceThicknessRule`, `SliceLabelRule` — vocabulary and rules that both the renderer and the
  editors must agree on.
- `HelpContent` (canonical English Help source) + `HelpLocalization` / `HelpText{Es,De,Ja,Ar}`.
- Game-library scan + Playnite read; `GameChatButtons`.

**Rule of thumb:** if two surfaces both need to agree on a rule (the renderer and the editor, onboarding and
Settings), the rule belongs in `Core`. Every time a copy was kept locally instead — the material→sound
pairing, the thickness demotion — the copies drifted.

## Root WPF app (AssemblyName `Radiata`)

- `App.xaml.cs` — tray, controller event wiring, overlay lifecycle, and the input-capture / emulation
  engine. The largest file; it is the orchestrator.
- `ControllerReader` (raw-HID Sony pads), `ControllerProfile`, `HidOffsets`, `XInputInterop`,
  `TriggerInterpreter`.
- `OverlayWindow` + `RadialMenuControl` (overlay and wheel rendering / animation), `GameBrowserControl`.
- `GamepadEmulator` (ViGEm), `HidHideManager`, `XboxDeviceTree`, `RecoveryTask`, `DriverSetup`.
- `SettingsWindow` + the editor controls, `OnboardingWindow`, the picker/dialog windows.
- `WindowsPlatformActions` (the `IPlatformActions` implementation), `NativeMethods`.
- Sound: `SfxEngine` / `Sfx` / `KawaiiXylophone`.
- `GameLibrary` / `PlayniteLibrary` / `GameArt` / `ArtPrefetcher` / `LauncherCatalog`.

## Two surfaces

1. A background **tray app** that draws the overlay on the summon gesture.
2. A normal resizable **Settings window** (mouse/keyboard) opened from a Settings slice or the tray.

`SystemConfig.ShowTrayIcon = false` drops the tray icon and makes the Settings window the taskbar app.

## Config

`%APPDATA%\Radiata\config.json`, watched by a `FileSystemWatcher`. Keys are camelCase; schema in
[../CONFIG.md](../CONFIG.md).

⚠ **Edits made outside the app may need a restart to pick up reliably** — the watcher misses some external
writes.

Writes are **atomic** (unique temp + single `File.Move`), so a crash or full disk can never leave a truncated
config. Three layers of recovery, in the order they kick in:

1. **`config.json.bak` — last known good.** A byte copy of `config.json` taken after it parses cleanly at
   startup, and preferred over shipped defaults when the live file won't parse. Without it, one corrupt file
   meant opening Radiata to empty wheels and hand-renaming a timestamped copy. A *copy*, not a
   re-serialization, so anything the current build doesn't model (a key from a newer version) survives the
   round trip. Pattern from Playnite (`PlayniteSettings.BackupSettings`, MIT).
2. **`config-corrupt-<timestamp>.json`** — the unreadable file is preserved before anything overwrites it.
3. **`backups\config-<version>-<timestamp>.json`** — snapshot taken when the app version changes, newest 10
   kept, so an upgrade can be walked back.

Other files in the same folder: `config.json.bak`, `hid_offsets.json`, `cover-overrides.json`,
`playnite-cache.json`, `discord-oauth.json` + `discord-token.json` (both DPAPI-encrypted), the art cache, and
`radiata-trace.log`.

## Naming

Product / exe = **Radiata** (`Radiata.exe`, app-data in `%APPDATA%\Radiata`). The C# namespace is still
`ControllerWheel` and the core library is `ControllerWheel.Core`; the csproj is `ControllerWheel.csproj`.

Renamed from **Capstan** (Jun 2026). `AppPaths` and `StartupManager` still migrate the old `Capstan`
app-data folder and Run-key entry forward — **those are the only places the old name intentionally
remains.**
