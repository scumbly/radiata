# Settings window and UI chrome

The Settings window is a normal resizable mouse-and-keyboard window opened from a Settings slice or the tray.
Code: `Settings/SettingsWindow.xaml(.cs)`, the `*EditorControl` controls, `Settings/SettingsTheme.xaml`, and the
chrome resources in `App.xaml`.

## A green build does not prove the Settings UI opens

XAML resource lookups resolve when the markup is parsed, at runtime. A bad `{StaticResource}` compiles and
then throws the moment Settings opens. The editor `UserControl`s are parsed before the window-level
`SettingsTheme.xaml` dictionary is in scope, so a style defined there and referenced with `StaticResource` from
an editor fails at runtime.

**Window-level styles must be referenced with `{DynamicResource}`.** Code-built windows use
`TryFindResource` so a miss cannot throw. After touching Settings XAML, shared styles or `SettingsTheme.xaml`,
run the headless check:

```
dotnet build ControllerWheel.csproj -c Debug
dotnet run --project tools\SettingsSmokeProbe -c Debug
```

Exit 0 is a pass. The probe merges `SettingsTheme.xaml` at window level like the real window, constructs every
editor control, shows the window off-screen, forces a layout pass and asserts the shared styles resolve. It is
markup-only (no config, audio, controller or disk writes). A new Settings-hosted control or shared
window-level style key must be added to the `EditorControls` / `WindowLevelStyles` arrays at the top of the
probe's `Program.cs`.

## Placement

`WindowStartupLocation` is Manual and the constructor centres the window in `SystemParameters.WorkArea`, the
primary display's working area. `CenterScreen` centres on the monitor that holds the cursor, which can be a
connected-but-dark display. Position and size are not persisted.

## Tabs

Left Wheel and Right Wheel (`WheelEditorControl`: the slice list and slice editor, auto-saved), Customize
(materials, sound, button glyphs, thickness, D-pad mode, label mode, Triggers builder), Passthru Mode
(`ExceptionsEditorControl`, see [INPUT-CAPTURE.md](INPUT-CAPTURE.md)), Advanced (`SystemEditorControl`:
controller, integrations, Game Grid, accessibility, language, updates), Help (`HelpEditorControl`, see
[LOCALIZATION.md](LOCALIZATION.md)) and About.

- Leaving a wheel tab settles its unsaved slice draft first (`WheelEditorControl.TryCommitOrDiscard`); Cancel
  reselects the old tab behind `_tabRevert`. Discarding a slice that was never saved deletes it.
- The Test buttons call `FlushPendingSave` first, because the overlay opens from the live config and the
  editors lag it by the save timer.
- Empty required fields disable Save instead of drawing a red border.

## Each tab owns its fields: `ApplyTo(cfg)`

There is no combined `GetConfig`. Each editor exposes `ApplyTo(SystemConfig)` that returns a `with` over only the
fields it owns, and `SettingsWindow.Save` layers them:

```
ExceptionsEditor.ApplyTo( SystemEditor.ApplyTo( CustomizeEditor.ApplyTo(_systemBase) ) )
```

1. **Order matters: the later layer wins.** Whichever tab owns a field, no other tab may write it, or its
   value is silently overwritten. Leave a comment at both sites naming the owning tab. The OBS connection
   fields (`obsPort`, `obsPassword`, `obsConfigured`) are written only by Advanced.
2. **The baseline can go stale.** A live change made while the window is open can be reverted by a save that
   layers `ApplyTo` onto the window-open snapshot, so `Save` rebuilds the baseline from the live config.
3. **A hot reload refreshes the open controls.** `OnConfigReloadedExternal` layers the `ApplyTo` chain onto the
   reloaded config; if it differs, or an editor's `ChangedOutside` reports a mirrored field (rule 4),
   `LoadSystemTabs` reloads the controls. Every tab's `Load` is guarded by `_loading`, so a refresh raises no
   `Changed` and writes nothing.
4. **A control that mirrors a field something outside Settings also writes sends it only when the user moved
   it.** The Passthru Mode checkbox shares `captureSafeMode` with the tray item, and the Advanced and Customize
   tabs share language, the crash-reporting choices, narration, the SteamGridDB key, the summon gesture, the
   material, thickness and sound picks with the setup wizard, the crash window and the open wheel. Each editor
   registers such a field once with `MirroredFields` (`Core/MirroredFields.cs`) and builds `ApplyTo` on
   `_mirror.ApplyTo(cfg)`, which writes a field only when its control now reports something other than it
   did at the last `Load` or save. `SettingsWindow.Save` calls each editor's `NoteSaved`, and the hot-reload
   check asks each editor's `ChangedOutside`. When the user and an outside writer both changed a field before
   the save, the user's value wins.

A deliberate action-type change clears every payload field ([ACTIONS.md](ACTIONS.md)).

## Chrome colours live in `App.xaml`

The chrome palette is a set of named `Ui*` / `HeadingInk` `Color` and `Brush` resources in `App.xaml`, and every
chrome surface (including the onboarding window) references them. Retoning means editing those values only;
do not add inline chrome hex. The wheel and overlay use a separate palette (`GlossDarkPalette` and the
per-material renderers) that these resources never affect.

## Drop-downs size to their content

Every Settings `ComboBox` sizes to its current selection: the style sets `HorizontalAlignment=Left`,
`MinWidth=120` and `MaxWidth=380`. Do not put `Width=` on a Settings combo; use a local `MinWidth` where the
layout needs a floor. Combos that must fill their container opt out with `MinWidth=0` and
`HorizontalAlignment=Stretch`. A local `Padding` replaces the style's padding outright, including the right
gutter that clears the drop arrow.

## Dismissal

Esc dismisses, and clicking outside the wheel or Game Grid card dismisses it through a global `WH_MOUSE_LL`
hook that exists only while an overlay is up. Windows silently drops a low-level hook whose thread misses the
hook timeout, so `App.RearmMouseWatch` unhooks and reinstalls it on the emulator-watchdog tick while a wheel,
the Game Grid or the arcade is up.
