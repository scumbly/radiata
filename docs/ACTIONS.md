# Slice actions

A slice is a label, an icon, a tint, an action and a position (array order). `Core/ActionExecutor.cs` runs
the action; `Settings/WheelEditorControl.xaml.cs` (`Categories`) decides what the editor and the in-wheel Add
picker offer. The JSON fields of every action type are in [../CONFIG.md](../CONFIG.md); this page is the
code-side view.

## The offered tree

`WheelEditorControl.Categories` is a list of categories, each holding type entries (a leaf, or a group of
options). The categories are Games & Apps, Chat & Streaming, System, Custom and Radiata. The Add picker
and the editor's tabs are built from the same tree.

- A category's `Key` is its identity: `AddMenuIcons` (`CategoryGlyphs`, `CategoryColors`) is keyed by it, so
  change a key in both places or the Add picker loses its glyph. Only `Header` and each `Display` are
  translated.
- Every `type` and `command` token and every `"category:"` / `"group:"` override key is an identifier and never
  translates. An in-wheel Add copies the English display name into the slice's `label`, and `Loc.DefaultLabel`
  translates it on display while the config keeps the English ([LOCALIZATION.md](LOCALIZATION.md)).
- `App.AddedSliceLabel` can give an added slice a different label than the picker entry (a menu option reads
  "Toggle DualShock Mode"; the slice reads "DualShock Mode"). Its labels must be `UiText.DefaultLabels`
  constants.
- The Radiata category offers Game Grid, Open Settings, Disable Wheels and an Arcade group
  ([ARCADE.md](ARCADE.md)).
- `SelectTypeOption` resolves a stored slice by an exact match over `Categories`, so every action type and
  system command the executor runs has an entry or option there; `TestHarness pairs` fails when one has none.
- `TypeOption.Alias` marks a second listing of an action whose canonical entry lives elsewhere (Mic Mute
  appears under System, Discord and Steam Chat). The exact-match pass skips aliases, so a stored slice always
  lands on its canonical entry rather than on whichever category comes first.
- The Arcade Launcher option stays first in its group: it is the fallback for any arcade command that names
  no catalogued game.

## Editor behaviour that protects data

- A deliberate action-type change clears every payload field (`ClearPayloadOnTypeChange`); collapsed controls
  keep their text and `ReadEditorInto` reads them back, so without the clear one type's path would ride into
  the next. A sub-option pick inside the same type and a slice load leave stored values alone.
- A slice that cannot run opens the thing that is missing. `App.NeedsDiscordIntegration` is the one
  predicate for the Discord RPC actions (`discord-join`, `discord-deafen`), which run over
  Discord's local RPC pipe and need the user's own application credentials; there is no keystroke fallback.

## Launch and focus

- Browse for an `.exe`, or pick from `InstalledApps.Scan` (the shell's AppsFolder, which includes Store apps
  stored as `shell:AppsFolder\<AUMID>`). Dropping an `.exe` or `.lnk` onto the slice list adds launch slices.
- "Already running" is matched by process name first (`ActionExecutor.ResolveProcessName`), then by any process
  whose image path lies inside the app's install folder (`ProcessIdsUnderAppDir`). Image paths come from
  `QueryFullProcessImageName`, not `Process.MainModule`. The folder match uses a trailing separator and refuses
  folders that unrelated software shares (`TooBroadForFolderMatch`). Store apps have no folder and cannot be
  detected as running.
- Toggle may only kill what it can account for: `MatchingProcessIds` limits matches to this sign-in
  and, when the slice carries a rooted launch path, to that exact image. The close is graceful first
  (`CloseMainWindow`, then a force-kill after a grace period). `IsProcessRunning` uses the same filters so
  the On/Off readout describes what the toggle would act on.

## Text Chat

`text-chat` opens the foreground game's text chat and types a preset message. It is the one action that injects
arbitrary text plus Enter, so its guards are load-bearing:

- "Try Game Default" refuses unless an installed game owns the foreground and the game is a confirmed entry in
  `GameChatButtons.Map` ([PC_GAME_TEXT_CHAT_BUTTONS.md](PC_GAME_TEXT_CHAT_BUTTONS.md)). A game that is not in the
  table, or is in `GameChatButtons.NoTextChat`, sends nothing; there is no Enter fallback.
- The foreground is captured before the chat key and re-checked before the text and before Enter.
- A 15 s cooldown (`ChatThrottleMs`) applies to every send, checked before any detection or input. It is a constant,
  not a setting, and it is stamped when a send is committed, not when it succeeds.
- The message body is never traced, only its length.

## Other executor notes

- Volume set and adjust write the endpoint volume directly; the standalone `media-*` commands send real media
  keys and have no readable state.
- `reboot` uses `shutdown.exe /g` (restart with automatic sign-on, when policy allows) unless
  `logInAfterReboot` is false, and falls back to `/r`.
- `power-plan` reports the activated plan's name or `Failed`, and toggles between `powerPlan` and `powerPlanB`.
- `hdr-toggle` reads `Unavailable` when display state cannot be queried (for example over Remote Desktop).
