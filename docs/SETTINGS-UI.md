# Settings window & UI chrome

**Read this when touching:** `SettingsWindow`, any `*EditorControl`, `SettingsTheme.xaml`, `App.xaml`
resources, or the icon/colour pickers.

## ⚠ A green build does NOT prove the Settings UI opens

XAML resource lookups resolve when the markup is **parsed** (at runtime), so a bad `{StaticResource}`
compiles fine and then throws the moment the user opens Settings. **That shipped once** (Aug 2 2026): a new
`HelpChip` style was added to the window-level `SettingsTheme.xaml` but referenced with `StaticResource` from
the editor UserControls, which are parsed *before* that dictionary is in scope. Every build was green;
opening Settings threw `Cannot find resource named 'HelpChip'` and took the window down.

**Rule: window-level styles must be `{DynamicResource}`.** `SubTabItem` documents the same trap.

After touching Settings XAML, shared styles, or `SettingsTheme.xaml`, run the headless smoke probe:

```bash
dotnet build ControllerWheel.csproj -c Debug && dotnet run --project tools\SettingsSmokeProbe
```

Exit 0 = pass. It loads App.xaml's resources, merges `SettingsTheme.xaml` at window level like the real
window, constructs every Settings editor control, shows the window off-screen and forces a layout pass, then
asserts the shared styles resolve. It's markup-only — no config, audio, controller, or disk writes — and is
excluded from the app build (`tools\**` is removed from the root csproj's globs).

**When adding a Settings-hosted control or a shared window-level style key, add it to the two arrays at the
top of the probe's `Program.cs`** (`EditorControls` / `WindowLevelStyles`).

## Placement: always centred on the PRIMARY display

`WindowStartupLocation` is **Manual**; the constructor centres the window in
`SystemParameters.WorkArea` (the primary display's working area). `CenterScreen` — which centres on
whichever monitor holds the CURSOR — once opened the window on a
connected-but-dark second display and it looked like Settings silently failed to open. **Deliberately
no remembered placement**: position, user moves, and resizes are not persisted.

## Tabs

| Tab | Contents |
| --- | --- |
| **Left Wheel** / **Right Wheel** | `WheelEditorControl` — the slice list + slice editor (shared inline `IconColorPickerControl`; auto-save). |
| **Customize** | Left column: material tiles (in two labelled group boxes, **Simple** = the two flats / **Deluxe** = the other six, mirroring the onboarding Look step) + sound-effect tiles + button-icon tiles. Right column: slice thickness, **D-Pad 🡄 🡆**, **Show labels on**, and **Triggers** (chord builder). |
| **Passthru Mode** | `ExceptionsEditorControl`. Global Anticheat Passthru Mode checkbox + the auto-passthru-mode app list. (⚠ The tab label is "Passthru Mode"; the control class is still `ExceptionsEditorControl`.) See [INPUT-CAPTURE.md](INPUT-CAPTURE.md). |
| **Advanced** | `SystemEditorControl`. Left column: Current Controller · Integrations (SteamGridDB + Discord + the shared OBS WebSocket connection, each behind a Configure… pane) · Game Grid. Right column: **Accessibility** (Wheel ignores opposite stick, Wheels toggle on/off ▸ Swap left/right, Reduce motion ▸ Always show hub, Narration — the ▸ pairs are the two drawn soft-link trees) · System (backup/restore, start-with-Windows, Troubleshooting, Quit). |
| **Help** | `HelpEditorControl` — read-only topic reader with a language dropdown. See [LOCALIZATION.md](LOCALIZATION.md). |
| **About** | Credits, trademark disclaimer, and the embedded open-source-licenses viewer. |

Lower-traffic knobs, per-type **Color Defaults**, and Tools (Controller Setup / HID Diagnostics / open config
folder) live in a **Developer Settings window** opened from Advanced — not a tab.

**Browsing off a wheel tab settles that tab's unsaved slice draft.** `Tabs.SelectionChanged` runs
`WheelEditorControl.TryCommitOrDiscard` on the tab being LEFT (`e.RemovedItems[0]`, matched by its content
type — no `x:Name` on the two wheel tabs), so the Save / Discard / Cancel prompt belongs to the edit being
left, not to closing the window several tabs and minutes later. `TabControl` has already switched by the time
the event fires, so Cancel is served by reselecting the old tab behind `_tabRevert`, which suppresses the
re-entrant event. The window's `Closing` handler keeps the same call for **both** editors — it is the backstop
for the tab you are still on when you close. **Discarding a slice that was never saved removes it** from the
wheel (`DropNeverSavedSlice`, on this path and on the slice-list selection change), so no empty placeholder
is left behind.

**The Test buttons flush the debounced save first** (`SettingsWindow.FlushPendingSave`): Test Left/Right Wheel
opens the overlay from the live config, which lags the editors by the 500 ms save timer.

**A deliberate action-type change clears every payload field** (`WheelEditorControl.ClearPayloadOnTypeChange`):
the hidden controls keep their text, and `ReadEditorInto` reads them back, so without the clear a Launch App
path or an Open URI would ride into whatever type is picked next. A sub-option pick inside the same type, and
a slice load, leave stored values alone.

The window is a normal resizable mouse/keyboard surface — an "at-my-desk" action — opened from a Settings
slice or the tray.

## Each tab owns its fields: `ApplyTo(cfg)`

There is no combined `GetConfig`. Every editor control exposes `ApplyTo(SystemConfig)` returning a `with`
over **only the fields it owns**, and `SettingsWindow.Save` layers them:

```
ExceptionsEditor.ApplyTo( SystemEditor.ApplyTo( CustomizeEditor.ApplyTo(_systemBase) ) )
```

Two consequences that have each caused a data-loss bug:

1. **Order matters — later wins.** `SystemEditor` (Advanced) runs *after* `CustomizeEditor`, so **whichever
   tab a field moves to, the OTHER tab must stop writing it** — and if the loser is Customize the symptom is
   silent (its value is simply overwritten), which is why every one of these moves leaves a comment at both
   sites naming the owning tab.

   The two tabs **swapped sections on Aug 9 2026**: the whole **Accessibility** block went
   Customize → Advanced (`TriggerActivation`, `SwapFnButtons`, `WheelIgnoresOppositeStick`, `AlwaysShowHub`,
   `ReduceMotion`, `Narration` — plus the Toggle ⇄ Swap and Reduce-motion ⇄ Always-show-hub soft-link state, which has to live
   beside the checkboxes it drives), and **D-Pad 🡄 🡆** + **Show labels on** went Advanced → Customize
   (`DpadHorizontalMode`, `ShowSliceLabels`). Before that, `AlwaysShowHub` / `ReduceMotion` had moved to
   Customize on Aug 3 2026 and `ShowSliceLabels` to Advanced on Aug 6.

   The shared **OBS connection** (`ObsPort` / `ObsPassword` / `ObsConfigured`) follows the same rule from the
   other direction: **Advanced ▸ Integrations is its only writer** (`ObsSetupWindow` → `SystemEditor.ApplyTo`).
   The wheel editors hold no OBS connection fields — an unconfigured OBS slice just shows a Configure button
   that routes there (`ObsSetupRequested` → `SystemEditor.OpenObsSetup()`), and `SettingsWindow` pushes the
   configured flag back to both slice editors so their prompt clears without a reopen.
2. **The baseline can go stale.** A live change made while the window is open (or by another writer) can be
   reverted by the next save layering `ApplyTo` onto the window-open snapshot. `Save` rebuilds the baseline
   from the live config; every `SystemConfig` field a tab shows, `Language` included (Advanced owns it), comes
   back through that tab's `ApplyTo`.
3. **A hot reload refreshes the open controls.** `OnConfigReloadedExternal` (skipped while a save is pending)
   layers the tabs' `ApplyTo` chain onto the reloaded `SystemConfig`; if that differs from it, the controls are
   stale and `LoadSystemTabs` re-`Load`s them. Every tab's `Load` is `_loading`-guarded, so the refresh raises no
   `Changed` and never writes; Settings' own save echo matches and is left alone.

## Chrome colours are centralized — edit App.xaml only

All 21 chrome values live as named `Ui*` / `HeadingInk` `Color` + `Brush` resources in **`App.xaml`**, and
every chrome surface references them — including `OnboardingWindow`, which used to re-declare the whole
palette. The wheels/overlay are a separate palette (`GlossDarkPalette` et al.) and are never affected.

**Retones = edit the Color values in `App.xaml` only. Never re-introduce inline chrome hex.**

Two complete tone sets exist and are interchangeable one-for-one — **warm khaki** and **silvery
blue-grey** (current, and the values in `App.xaml`); each pair keeps the
same lightness, so swapping sets changes hue only and leaves contrast/hierarchy identical.

Section headings are 15.5 px (since Jul 16 2026).

## Drop-downs size to their content

Every Settings `ComboBox` sizes horizontally to its **current selection**, not to a
number written in XAML. The retemplate's content border already measured the selection plus the padding; the
style now adds `HorizontalAlignment=Left` (which turns that desired width into the actual one, instead of the
inherited Stretch filling the row), `MinWidth=120` so a one-word selection isn't a stub, and `MaxWidth=380` so
a long installed-game title can't push past the editor panel. Picking a longer entry visibly widens the box —
intended; a fixed width either clipped the long entries or left the short ones trailing dead space.

⚠ **Don't put `Width=` back on a Settings combo.** Nine of them carried one and all nine were removed. A local
floor is fine where the layout needs it (`TypeBox`'s hand-tuned `MinWidth=98`, `SubTypeBox`'s `MinWidth=138`,
`GameBox`'s `MinWidth=240` — 2× the style floor, because installed-game titles are long).
⚠ **Two combos are container-sized, not selection-sized**, and opt out with `MinWidth=0` +
`HorizontalAlignment=Stretch`: Customize ▸ Triggers' chord dropdowns (fixed 98/23/108px columns — the row has
to end inside the tab's right column, or the ✕ rides off the edge) and Help's `LanguageBox` (DockPanel last
child, filling whatever the back button + flag leave).
⚠ **A local `Padding` REPLACES the style's outright**, including its 28px right gutter clearing the drop
arrow — only safe on a box wide enough to have slack, like those two.

## Other conventions here

- Empty required fields **disable Save** rather than drawing a red border (the border was retired Jul 2026).
- **Esc dismisses**; clicking outside the wheel or Game Grid card dismisses it (a global `WH_MOUSE_LL` hook,
  scoped to overlay-up only). Windows silently drops a low-level hook whose thread misses the hook timeout and
  reports nothing, so `App.RearmMouseWatch` unhooks and reinstalls it on the 10 s emulator-watchdog tick while the
  wheel, Game Grid or Arcade is up; a failed install traces `[Input] mouse hook install failed (Win32 error N)`.
- `SystemConfig.PracticeWhileSettingsOpen` — **default flipped to false and the UI hidden Aug 3 2026**:
  opening Settings no longer changes what a fired slice does. Still honoured from config, just not
  authorable. The first-run wizard always practices regardless.
