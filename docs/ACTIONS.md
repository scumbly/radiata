# Slice actions — the authored taxonomy

**Read this when touching:** `WheelEditorControl.Categories`, `Core/ActionExecutor.cs`, the in-wheel Add
picker, `AddMenuIcons`, or `ActionIcons`/`ActionTint`.
The **JSON schema** for every type and its fields is [../CONFIG.md](../CONFIG.md) — this doc is the
code-side view: what the editor *offers*, the hidden-but-supported contract, and the removals ledger.

A slice = label + icon + tint + action type + payload + position (array order). `Core/ActionExecutor` runs
them. Per-action-type **default icon + tint** overrides live in `config.json` (`actionIcons`, `actionColors`;
no Settings control writes them — see [../CONFIG.md](../CONFIG.md)); per-slice overrides win.

## The offered tree (`WheelEditorControl.Categories`)

Category headers are also keys in `AddMenuIcons` (`CategoryGlyphs`/`CategoryColors`) — **rename a header in
both places or the Add picker loses its glyph.**

**Identity vs display.** The tuple's `Key`, every `type`/`command` token and the `"category:"`/`"group:"`
override keys are identifiers and never translate. `Header` and each `Display` are shown through `Loc.T` at
the display sites (the editor's tabs and type/subtype combos, the in-wheel Add picker). An in-wheel Add
copies the ENGLISH display name into the new slice's `label`; `Loc.DefaultLabel` translates it on the wheel
and in the editor list while config keeps the English (docs/LOCALIZATION.md §3).

**Games & Apps** — Installed Game · Game Grid · Storefront · Launch App · Exit Current App · *Game Bar*
(Open, Screenshot, Start/Stop Recording, Record Last 30 sec, Toggle Mic While Recording). Game Bar lives
with games because it captures gameplay — users look for it here, not under System.

**Chat & Streaming** (renamed from "Voice" Aug 2 2026, when OBS moved in from Advanced) —
*Discord* (Launch, Join/Leave Voice Channel, Mute Me, Deafen) · *Steam Chat* (Open Steam Chat, Mic Mute /
Push-to-Talk) · *OBS Studio* (Toggle Streaming, Toggle Recording,
Save Replay Buffer, Switch Scene…, Toggle Source Mute…) · Text Chat.

**System** — *Controller* (Toggle Xbox Mode, Toggle DualShock Mode) · *Audio* (Switch Audio Output, Toggle
Mute, Mic Mute, Set Volume To…, Play/Pause, Next Track, Previous Track) · *Display* (Toggle Extend/Clone,
Toggle HDR) · *Windows* (Show Desktop, Empty Recycle Bin) · *Power* (Sleep, Hibernate,
Reboot, Shut Down, Log Out, Lock Screen, Toggle Power Plan… — renamed from "Set Power Plan…" Aug 6 2026,
token `power-plan` unchanged: it now flips between two chosen plans, `powerPlan` ⇄ `powerPlanB`).

**Custom** (named "Advanced" until Aug 9 2026) — Open URI · Key Combo.

**Radiata** — Game Grid (also under Games & Apps) · Open Settings · Disable Wheels · **Toggle Passthru Mode**
(added Aug 9 2026 — flips `SystemConfig.CaptureSafeMode` through the host's own `ToggleSafeMode`, so the
slice, the tray item and the Settings ▸ Passthru Mode checkbox are one path. ⚠ **Offered only when
`App.SafeModeAvailable`** — the same drivers-installed gate that decides whether the Passthru Mode TAB exists —
and `Hidden`, not absent, otherwise, so a slice authored on a machine with the drivers can't have its type
rewritten on one without them) · *Arcade* (the picker +
one option per game in `ArcadeCatalog.Games` — a child GROUP of Radiata since Aug 6 2026, never its own
tab). ⚠ **The Arcade group is offered only when `Arcade.Available`** — i.e. the build wasn't compiled
without it (Arcade left beta Aug 26 2026; the tray opt-in is gone). Otherwise the SAME entry stays under
Radiata as `Hidden` so an existing arcade slice can't have its type rewritten; the tab strip's spare
`TabItem` stays collapsed. Because the harness flips availability at runtime, `Categories` is a **cached
property, not a static field**. Full contract in [ARCADE.md](ARCADE.md).

## Platform limits that are PERMANENT — don't re-propose

Steam offers **no** Discord-style IPC, so its group carries only what the platform allows: open the UI + mic
mute. (Xbox Party Chat was removed from the offer entirely Sep 16 2026 — see the removals ledger.) **Join/leave voice and deafen are platform impossibilities.** The Steam CEF-debug backdoor
and the undocumented Xbox Live endpoints were evaluated and **ruled out** on security posture / ToS grounds.
Documented in the `steam-xbox-voice` Help topic.

Notes on what the offered actions actually do:
- **Reboot uses `shutdown.exe /g`, not `/r`** (Aug 3 2026). `/g` is a restart plus **ARSO**, which *when
  available* signs the user back in and restores registered apps — what the Start-menu restart does. A plain
  `/r` can land the user at a sign-in screen, and Radiata cannot type a PIN (secure desktop), so on a couch
  with no keyboard that's a dead end. Falls back to `/r` if `/g` is refused —
  `TryStartChecked` treats "still running after 2 s" as success so a shutdown already underway is never
  requested twice. Playnite avoids `shutdown.exe` here for the same class of reason (their #3947) and passes
  `EWX_ARSO` to `ExitWindowsEx`.
  **`/g` is best-effort, not a guarantee**: ARSO is gated by Windows policy and by
  BitLocker/credential configuration, and when it's unavailable `/g` simply behaves like `/r` and the user
  lands at the sign-in screen. Windows **auto-login + no-wake-password** is what actually makes couch reboot
  safe; `/g` is the improvement on top, not the mechanism. Deliberately *not*
  promised in the UI — the slice is labelled plainly "Reboot", with no sign-in claim attached.
- **Steam chat:** `steam://open/friends` is the one reliable chat URI in the post-2018 client.
- **Text Chat** opens the running game's in-game **text** chat and types a preset message. "Try Game Default"
  resolves the key from `Core/GameChatButtons.cs` at **fire** time (re-detected every fire); "Custom" sends a
  chosen key instead. `GameChatButtons` is generated from
  [PC_GAME_TEXT_CHAT_BUTTONS.md](PC_GAME_TEXT_CHAT_BUTTONS.md) — **500 individually source-verified entries**
  (Aug 2026 audit + redo pass + "+500" addition phase). **An installed game not yet in the table sends
  nothing at all** — this used to fall back to Enter (the common case), but that guessed wrong often enough
  in testing that the fallback was removed entirely: an unconfirmed game may not bind Enter to chat, and a
  wrong guess presses whatever Enter *is* bound to before the message follows it in as raw keystrokes. This
  now covers both the doc's **Excluded** tables (`GameChatButtons.NoTextChat` — games known to have no
  keyboard-opened chat box: voice/emote/preset-phrase only, chat behind a mouse click, or no multiplayer at
  all) and the **Needs re-verification** table (games where the key just isn't confirmed yet) identically:
  neither sends a keystroke. The trace/spoken message still distinguishes the two cases for diagnosability.

  ⚠ `HasNoTextChat`'s relaxed (containment) match is floored at 8 characters, unlike `Lookup`'s. That list
  holds names as short as "Peak"; a four-letter substring would silently disable the slice under every
  unrelated title containing it. It is also checked **after** `Lookup`, so a verified mapping always wins.

  ⚠ **This is the only action that injects arbitrary text + Enter into the general input stream, so its
  guards are load-bearing** — don't relax any without reading `RunTextChat`'s
  comment:
  - **"Try Game Default" refuses to fire unless an installed game owns the foreground**, and refuses again
    if that game's chat key isn't a confirmed entry in `GameChatButtons.Map`. Both refusals type nothing —
    typing into a browser/DM/terminal/sign-in field, or pressing an unconfirmed game's real Enter binding
    before the message follows it in, are each worse than doing nothing.
  - **The no-chat-key refusal is the one text-chat outcome with a HUB READOUT** — the game's name over
    "No chat key default found. Configure in Settings", held `NoChatKeyLingerMs` (2.6 s) rather than the
    500 ms a state word gets, via `ActionStatus.LingerMs`. It's the only refusal the user can act on (set
    the slice to Custom and pick a key), and the only one with a game to name. It deliberately does **not**
    open Settings: the wheel was fired at a running game, and yanking the user out to a window mid-game
    costs more than the message did. Known-chatless and merely-unverified games show the *same* readout —
    only the trace and narration distinguish them. The other refusals (no game in front, no readable
    window) stay narration-only, per `Announce`'s note.
  - **The foreground is captured before the chat key and re-checked before the text and before Enter.**
    Anything can steal focus inside the 150 ms chat-box delay; the message must not follow it.
  - **A 15 s throttle (`ChatThrottleMs`) gates every send**, checked before any detection or input. A slice
    is one button press, so without it the action is a held-button chat flood — a bannable nuisance in most
    multiplayer titles and not something Radiata should make easy. Deliberately a **constant, not a
    setting**: it is an abuse guardrail rather than a preference, and a configurable one is just a spam
    switch. The refusal both speaks and takes a hub readout ("Cooldown, Ns"), since silence is
    indistinguishable from a dead input.
    ⚠ The stamp is taken when a send is **committed**, not when one succeeds. That is deliberate twice over:
    an aborted send may still have typed text somewhere, and `TypeChatAsync` is async-void — stamping only
    on success would let rapid fires interleave two sends inside the chat-box delay.
  - **The message body is never traced** (length only). The trace log is the file users are asked to send with a bug report.

## Launch / focus apps

Two ways to pick:

- **Browse for App…** — an `.exe`; icon auto-extracted via `SHDefExtractIcon`.
- **Installed Apps…** — `AppPickerWindow` over `InstalledApps.Scan`, the shell's **AppsFolder**: everything
  with a Start-menu entry, **including Store/UWP apps**, stored as `shell:AppsFolder\<AUMID>` and icon'd via
  `IconCache.ExtractShellIcon`.

Dropping an `.exe`/`.lnk` — or a Store app dragged from the Start menu — onto the slice list adds launch
slices directly.

**The "Process Name (if different)" field was REMOVED Jul 31 2026.** The running-instance name
is derived from the picked exe's filename (`ActionExecutor.ResolveProcessName`), which is right virtually
everywhere, and the field could never work for **Store apps** at all — their window belongs to
ApplicationFrameHost, so `LaunchOrFocus`'s `MainWindowHandle != 0` check can't match them no matter what name
is stored. **No migration:** the executor still HONOURS a `process` override in an existing config, and
`ReadEditorInto` deliberately doesn't write `ProcessName`, so editing a legacy slice preserves it — it just
can't be authored any more.

### Detecting "already running": name first, then INSTALL FOLDER (Aug 3 2026)

The process-name check is cheap and right for the common case, so it stays first. On a miss, Run / Toggle /
the On-Off readout now also match **any process whose executable lives inside the app's own install folder**
(`ProcessIdsUnderAppDir` — Playnite's `MonitorDirectory` pattern). That closes the documented stub-launcher
blind spot: an app started through an updater or launcher stub runs under a *different* exe name, so Toggle
used to report "not running", start a **second** copy, and say "On" while nothing had changed; Run likewise
launched a duplicate instead of focusing the copy on screen.

Guards, because a folder match is broader than a name match:
- Image paths come from `QueryFullProcessImageName` (`NativeMethods.ProcessImagePath`), **not**
  `Process.MainModule` — MainModule throws across a 32/64-bit boundary and on access-denied, which is exactly
  the population being inspected. Session 0 (services) is skipped.
- The folder is matched with a **trailing separator**, so `C:\Fallout` cannot match `C:\Fallout 2\…`.
- A folder that unrelated software shares is **refused outright** (`TooBroadForFolderMatch`): a drive root,
  Program Files, Windows/System32, `%LOCALAPPDATA%`, `%APPDATA%`, `%USERPROFILE%` and its Desktop/Downloads/
  Documents, `%PROGRAMDATA%`, and `steamapps\common` (the parent of every Steam game, not one game's folder).
  An exe sitting directly in one of those gets name-matching only.
- A `shell:AppsFolder\…` AUMID has no folder, so **Store apps are unchanged** — still not detectable as
  running. That limit is real and stays in the `app-slices` Help topic.

Residual limits, documented in the `app-slices` Help topic: a Store app can't be detected as
already-running (Run re-opens it, which Windows turns into a focus) and Toggle won't reliably kill one; an
app launched via an updater/launcher **stub** may run under a different name, so Toggle may miss it — pick
the real `.exe`.

### What Toggle is allowed to kill (Aug 3 2026)

The **name** branch used to be a bare `Process.GetProcessesByName(name).Kill()` — every accessible process
sharing the name, force-killed, before the configured path was even considered. A generic, stale, imported or
hand-edited action (`"process": "java"`, `"node"`, `"launcher"`) could therefore take out unrelated software,
including another signed-in user's copy. `MatchingProcessIds` now applies two filters, and
`IsProcessRunning` uses **the same** ones so the On/Off readout describes what the toggle would actually act
on (a scoped toggle whose readout still says "On" just looks broken):

- **Session** — this logon session only. An elevated Radiata could otherwise reach across users.
- **Image path** — when the slice carries a *rooted* launch path, the running exe must **be** that exe. A
  path-less slice can't be checked this way and keeps name+session matching, or every legacy Toggle breaks.
  Returning nothing here isn't a dead end: the caller falls through to the install-folder match above, which
  is the designed answer for a stub-launched app running under another name.

Killing is now **graceful-first**: `CloseThenKill` sends `CloseMainWindow`, waits ~3 s on a background task
(the wheel release must not block), then force-kills survivors. Windowless processes are killed immediately —
there's nothing to ask. An app with unsaved work gets its "save changes?" prompt, and is still killed after
the grace period, so the toggle stays honest.

## The "hidden but supported" contract (`TypeEntry.Hidden`)

Some types/commands are **kept selectable but not offered**. This is not cosmetic — it's the thing that stops
a legacy slice being silently rewritten:

`SelectTypeOption`'s exact-match loop reads `Categories` **unfiltered**, so a legacy slice's (type, command)
pair still resolves and type-locks correctly. If an entry were deleted outright, that loop would find
nothing, drop to tab 0 with nothing selected, and the **next auto-save's `ReadEditorInto` would read tab 0's
default type and rewrite the slice's type outright.**

Currently hidden:

| Hidden | Why it must stay |
| --- | --- |
| `script` ("Run Script") | Un-offered Aug 2 2026, but the **executor case must also stay**: the hidden-but-working `sequence` type's `script:` steps call the same `RunFile` path, so removing it would silently break every sequence with a script step. |
| `sequence` | Un-offered Aug 1 2026. Fully wired — executor, `SequenceFormat`, editor fields, Help topic — so existing sequence slices keep working. Re-offer by flipping the flag. |
| `display-extend` / `-clone` / `-external` / `-internal` | Offer removed Aug 2 2026 with the `display-toggle` replacement; **executor cases removed**, so a surviving slice no-ops via the unknown-command default while keeping its Command unrewritten. |
| `nvidia-overlay` / `amd-overlay`, and the whole `Recording` group (NVIDIA/AMD record, replay-save, screenshot) | Removed entirely Aug 2 2026 (no migration); executor cases removed too. Same no-offer/no-rewrite contract. |
| `toggle` | The older spelling of `launch` with `quitIfRunning`. Never authored by the editor; the executor, `SequenceFormat` and the `sequence` step text form still use it. A `Hidden` Custom-tab entry, so a stored `toggle` slice keeps its type, `process` and `path` through an auto-save. |
| `media-mute` | Executor still supports it for existing configs; not offered. A `Hidden` option in the Audio group, so a stored slice keeps its command instead of falling to the first `system` entry. || `safe-mode` ("Toggle Passthru Mode") | Un-offered Aug 30 2026. **Fully wired — executor case, tint, glyph, label-map row and the `App.SafeModeAvailable` plumbing all stay**, so an existing slice keeps working; re-offer by flipping the `hidden` flag. Un-offered because a wheel slice is the path that most reliably flips Passthru Mode OFF with the game still foregrounded (the overlay is `WS_EX_NOACTIVATE`), which is what arms the pending passthru-mode-off hold. ⚠ Whether the tray and Settings paths can arm it too is **unmeasured**; don't state that they complete immediately. Passthru Mode itself is unchanged and still toggles from the tray and Settings ▸ Passthru Mode. |
| `focus-assist` ("Do Not Disturb") | Removed Aug 13 2026 — Windows orphaned the legacy Focus Assist WNF state the action wrote to, so it changed nothing. **Executor case, platform methods and `FocusAssistState` are gone**; the entry, its tint, glyph and label-map row stay so a surviving slice keeps its type and stays readable, and no-ops via the unknown-command default. Same precedent as `display-extend`. |
| `discord-mute` ("Mute Me") / `steam-mute` | Un-offered Aug 10 2026 in favour of the plain OS **Mic Mute**, which both groups list as an `Alias`. **Both executor cases stay** and existing slices keep working. ⚠ They no longer work the same way: `discord-mute` toggles Discord's own switch over RPC (Sep 19 2026) and its `keys` is inert; `steam-mute` is still a keystroke mirror, because Steam offers no control channel. |
| The whole **Xbox Party Chat** group — `xbox-party-open` ("Open Chat"), `xbox-party-mute` | Removed Sep 16 2026 — the group is `Hidden` in `Categories`, so neither the Settings slice editor nor the in-wheel Add picker offers it. **Executor cases and the hub readout removed**; a surviving slice keeps its type and no-ops via the unknown-command default. Tint, glyph, label-map row and the `AddMenuIcons` group entry stay, per the `focus-assist` precedent, so it stays readable. |

`TestHarness pairs` guards this list: every action type and every system command `ActionExecutor` runs must have a
`Categories` entry or option, offered or `Hidden`. Add the executor case and the entry together.

### `TypeOption.Alias` — the exact opposite

`Alias` means **offered here, never resolved to here**: a second listing of an action whose canonical entry
lives in another category (System ▸ Audio ▸ Mic Mute, also offered inside Chat & Streaming's Discord and Steam
Chat groups). `SelectTypeOption`'s exact-match pass skips aliases, so loading an existing slice always
lands on the one canonical entry. ⚠ Without that skip the winner would be decided by **category order** —
the scan is positional and first-match-wins, so a plain Mic Mute slice would open under Discord.

## Removals ledger (no migration in any case)

Old slices of these types/commands still **load** — firing them traces and no-ops via the executor's
unknown-type / unknown-command fallback.

- **Jul 30 2026:** `powershell`, `mumble-*`, `ts3-join`, `wol`, `http` — PowerShell, Mumble,
  TeamSpeak, Wake-on-LAN, and HTTP-request actions.
- **Aug 2 2026:** `display-extend` / `display-clone` / `display-external` / `display-internal`
  (→ `display-toggle`, which reads the current topology and flips Extend⇄Clone) and the NVIDIA/AMD
  capture/overlay commands.
- **Aug 13 2026:** `focus-assist` ("Do Not Disturb"). Windows moved the setting to the
  `windows.data.donotdisturb.*` CloudStore state and orphaned the legacy Focus Assist WNF state the action
  published to. ⚠ **The failure was silent**: the orphaned WNF state still accepts writes and reads back the
  written value, so the action's own read-back verification passed while nothing changed. Verified on the reference
  machine — the CloudStore blob read `Microsoft.QuietHours.Profile.Unrestricted` immediately after a toggle the
  app reported as success, and no `windows.data.notifications.quiethours*` key exists any more. There is no
  supported API for DnD and the new store is an undocumented binary blob. **Don't reintroduce this action
  without a mechanism the shell actually reads.**
- **Aug 10 2026:** the app-specific mic mutes `discord-mute`, `steam-mute` and `xbox-party-mute` left
  the offer (see the hidden table above).
- **Sep 16 2026:** the whole **Xbox Party Chat** group — `xbox-party-open` and `xbox-party-mute` —
  removed from both editors, executor cases included. Help no longer mentions it.
- The **DS4Windows** toggle slice + its elevated scheduled task were removed at 1.21, superseded by built-in
  ViGEm emulation.

## Executor notes worth knowing

- **The three Discord actions run over Discord's local RPC pipe, never over a keystroke.** `discord-join`,
  `discord-deafen` and `discord-mute` all need the user's own Discord application credentials, and
  `App.NeedsDiscordIntegration` is the ONE predicate that says so — the armed hub's needs-setup notice, the
  open-the-wizard-on-fire branch and the editor's Configure prompt all read it, so a fourth RPC action is
  added there and nowhere else. ⚠ Deafen and Mute Me used to send `Ctrl+Shift+D` / `Ctrl+Shift+M`, which are
  Discord DEFAULTS that fire only while Discord is focused: the slice did nothing mid-game and handed the
  combo to the game as well. Don't reintroduce a keystroke fallback — there is nothing for it to fall back
  to. A `keys` on an existing slice of either type is accepted and ignored (the hidden-but-supported
  contract above), and the editor no longer offers the field.
- **Volume set/adjust writes the endpoint scalar directly.** It used to **synthesize** a run of
  volume-up/down media keys to walk the level over, which typed a stream of letters into the focused app
  whenever the scancode translation went wrong (media keys are E0-extended). ⚠ Older docs claimed
  the media-key synthesis was the current behaviour; it is not, and it should not be reintroduced. The
  standalone `media-*` commands *do* still send real media keys, deliberately: they're fire-and-forget to
  whatever player owns media focus, with no readable state and so no hub readout.
- **`hdr-toggle`** reads `Unavailable` when the display state can't be queried (e.g. over RDP). The
  implementation is dual-generation (24H2 / ACM-proof).
- **`power-plan` reports the outcome, not the attempt** (Aug 2 2026): the activated plan's name
  on success, `Failed` when the switch didn't take. `powercfg` can no-op silently on a policy-locked scheme
  or a missing exe, and a power-plan slice otherwise gives no feedback at all.
- **`power-plan` is a TOGGLE since Aug 6 2026** (UI: "Toggle Power Plan"): with `powerPlanB` present, firing
  flips active == `powerPlan` → `powerPlanB`, else → `powerPlan` (`IPlatformActions.ActivePowerPlanGuid`
  reads the live scheme). A legacy single-plan config (no `powerPlanB`) keeps the old plain "set plan A";
  the editor loads it with plan A = stored, plan B defaulted to Balanced.
- **`reboot` honours `logInAfterReboot`** (Aug 6 2026, default true = the `/g` ARSO path above); false =
  a deliberate traditional `shutdown /r` — the "Log In after Reboot" checkbox unticked.
- **A slice that can't run opens the thing that is missing, not always the slice editor.** The fire path's
  configure-on-fire branch (`App.FireArmed`) routes by what is absent: a Discord RPC slice (`discord-join`,
  `discord-deafen`, `discord-mute`) without the user's own Discord credentials opens `DiscordSetupWindow`
  (`App.NeedsDiscordIntegration`; Launch Discord just starts the app and needs no credentials).
  Credentials come FIRST when the
  channel URL is missing too — the slice editor replaces its URL row with the same "Configure Discord
  Integration" prompt until they exist, so landing there would only be a second click to this window — and a
  slice still incomplete afterwards falls through to the editor. Everything else (`NeedsConfiguration`, an
  OBS slice with no connection) still opens the slice in Settings. The ARMED hub shows the same
  needs-setup notice for all of them. The slice editor's **Test Action** takes the same route for the
  RPC Discord types (`WheelEditorControl.BtnTest_Click` opens the wizard instead of firing); the trace line
  a bare fire leaves points at Settings ▸ Advanced ▸ Discord.

### The label an ADDED slice carries can differ from the picker entry

`App.AddedSliceLabel`. A picker entry names an option in a menu of options ("Toggle DualShock Mode"); a slice
on the wheel names a thing ("DualShock Mode") — which is also what the Settings editor's `InferLabel` gives
the same type, so both add routes land on one label. Currently: the Arcade Launcher (→ "Arcade"), and the
two controller-mode toggles (→ "Xbox Mode" / "DualShock Mode"). ⚠ Entries must be `UiText.DefaultLabels`
constants: `Loc.DefaultLabel` resolves an untouched default against that set at display time and config
always holds the English, so a literal would language-lock the slice.

## Label-only renames (tokens unchanged, zero legacy impact)

"Open Party Chat"/"Party Mic Mute" → **"Open Chat"/"Mic Mute"**; "Key Press" → **"Key Combo"**; Focus Assist
→ **"Do Not Disturb"** (Windows 11's own name, the friendlier label); `launcher` → **"Storefront"**.
Deferred value-picker names are locked as **Launcher / Cycle Audio / Set Volume** (not built).
