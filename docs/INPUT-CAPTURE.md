# Input capture & emulation (HidHide + ViGEm)

**Read this when touching:** `App.xaml.cs`'s capture engine (`UpdateInputCapture`/`UpdateInputCaptureCore`),
`HidHideManager`, `GamepadEmulator`, `XboxDeviceTree`, `RecoveryTask`, the Passthru Mode tab, or anything that
changes which process can see the physical pad.

Radiata attempts to cloak the selected physical controller and present a virtual stand-in when capture is eligible. Passthru mode, missing drivers, failed recovery, multiple-controller capture guards, and unresolved isolation can leave the virtual target absent. A successful blocklist write does not prove that every game or remapper has lost an existing handle.

The orphan-recovery policy is that same-model blocks are adopted, and uninstall clears the full block list unless another Radiata copy is running. Failed cleanup retains recovery records and prevents removal. Two tools managing HidHide blocks for the same controller remain unsupported; unrelated blocks can be released by uninstall. Global inverse mode is preserved. XInput acquisition accepts multiple physical and foreign virtual sources, but slot counts do not identify hardware. Historical hardware results below are not tests of this build.

- **Native mode (default):** a permanent virtual **DualShock 4** (ViGEm) stands in for a Sony pad — keeps
  PlayStation button prompts.
- **Xbox Mode:** a permanent virtual **Xbox 360** pad instead (universal XInput).
- While any overlay (wheel / Game Grid / editor) is up, the virtual pad is held **neutral**, so the game
  underneath gets nothing. Suppression engages at the TOP of `ShowOverlay`, before icon/layout work.
- **HidHide is required, not optional, for exclusive input.** Steam and `Windows.Gaming.Input` read via
  **Raw Input**, which an exclusive HID open cannot block — only the HidHide filter driver removes the
  device from other apps. Radiata whitelists its own exe so it keeps reading the physical pad.

## Drivers

ViGEmBus + HidHide (both Nefarius, standalone installers, bundled loose in `drivers\`). No ViGEmBus → no
virtual pad and raw input passes through (graceful degradation).

**Supply chain (verified Jul 2026):** ViGEmBus is **retired** — archived Nov 2023 (trademark conflict);
bundled **1.22.0** is the final release and the safe one (it dropped the auto-updater whose dead domain was
the only security concern). Same bet the whole DS4Windows ecosystem makes; the only successor (Nefarius
VirtualPad) is commercial/partner-only, so there is **no vendor recourse** if a Windows driver-policy change
breaks it. HidHide is actively maintained; bundled **1.5.230** is current. ⚠ Never link testers to
`vigembus.com` / `vigembusdriver.com` — unofficial; GitHub + nefarius.at only.

## Foreign ViGEmBus forks (HP OMEN Gaming Hub, Oculus, Virtual Desktop)

**The trap.** Other programs ship their own ViGEmBus builds, and every one registers the same `ViGEmBus`
service name and the same device-interface GUID `{96e42b22-f5e9-42f8-b043-ed0f932f014f}`, so both of
Radiata's presence probes (`Services\ViGEmBus` key, `ViGEmClient()` connect) say "installed" and the client
binds to **whichever bus answers first**. Known forks, from one tester's OMEN laptop (Sep 17 2026):

| Program | Bus devnode | Notes |
| --- | --- | --- |
| **HP OMEN Gaming Hub** (OMEN Fusion) | `SWD\DRIVERENUM\{2abac65e-…}#HPIncVigemBusOmenFusionSoftware…`, parented to `ACPI\HPIC0004` | A **2018 fork of 1.4.3 that reports itself as 10.x**, so "is the latest bus present" checks pick it (nefarius/ViGEmBus#99). Its chain ends at `ROOT\ACPI_HAL` like a physical pad's. |
| Oculus | `ROOT\SYSTEM\000N`, service `Oculus_ViGEmBus` | |
| Virtual Desktop | `ROOT\SYSTEM\000N`, service `vdvge` | |

**What Nefarius says** (docs.nefarius.at ▸ ViGEm ▸ How-to-Install ▸ Troubleshooting, and #99): **no coexistence
is supported**; more than one "Virtual Gamepad Emulation Bus" under Device Manager ▸ *Devices by connection*
means conflicting installs. The remedy for HP is *disable HP's bus in Device Manager, delete its
`HKLM\SYSTEM\CurrentControlSet\Control\DeviceClasses\{96e42b22-…}` entry, keep it disabled* (or contact HP).
A full purge is manual: uninstall, reboot, remove every remaining bus in Device Manager, force-delete every
`vigembus.inf` from the driver store (Driver Store Explorer), reboot, verify — "no entries left in Device
Manager does not mean the system is rid of it." ViGEmBus is archived; none of this will change upstream.

**Radiata's policy — detect and name, never install over, never remove:**
- `DriverStatus.ForeignViGEmBusName()` names the fork from its bus devnode (`Enum\ROOT` + `Enum\SWD` scan for
  `Service=ViGEmBus`, no Nefarius Apps entry = foreign). The startup `[Drivers]` line reads
  `ViGEmBus: installed <file version> (Nefarius)` or `(foreign (HP OMEN Gaming Hub): SWD\…)`.
- **Onboarding** shows ⚠ instead of ✅ with Nefarius's switch steps, and the drivers column stays open.
- **`RunDriverEngineAsync` skips the ViGEmBus install** while a fork answers the probe. The bundled 1.22
  bundle beside HP's fork produced a stalled setup and would have left two buses.
- **Uninstall** reports a fork as *left in place, another program's* (`DriverSetup.UninstallOutcome.NoUninstaller`,
  exit code 0) — a present bus with no Nefarius uninstaller is foreign, not a failed removal.
- **The virtual-pad classifier does not depend on the bus's shape** — see *How it works* below — and the
  **churn breaker** keeps an unrecognised fork from turning into a disconnect loop.
- **Version ceiling.** ViGEmBus ended at **1.22.0**; `DriverStatus.VersionExceedsFinalNefarius` calls any
  higher file version a fork whatever its Apps entry says (HP's reports 10.x). The ceiling only condemns —
  a fork built from an older tree reports an honest 1.1x and still needs the Apps-entry check.
- **Sony pads on a fork.** The X360 target is the one call a 2018 bus is known to accept; if the DS4 target
  is refused, capture switches to an **Xbox 360 stand-in for the session** (`_ds4RefusedByBus`) and traces
  it — Xbox button prompts in games rather than no stand-in. Unverified on hardware until a Sony pad meets
  a fork.
- **One notice per install** after onboarding: a corner toast naming the program and Nefarius's switch
  steps, keyed on `SystemConfig.ForeignViGEmBusNoticed` (the origin string, so a different fork notices
  again). During onboarding the wizard's drivers row says the same thing instead.
- Radiata does **not** automate the Device Manager disable or the `DeviceClasses` delete: both break the owning program, and the second is a registry edit under
  `Control\DeviceClasses`. The user takes Nefarius's steps; Radiata names them.

## HidHide gotchas (each cost real time)

See the `HidHideManager` remarks.

1. **Not whitelisted while the pad is cloaked = blind to our own controller** (zero HID reports). The
   Capstan→Radiata rename triggered exactly this. Fixed by the one-time **elevated self-registration**
   (`--hidhide-whitelist`, handled before the single-instance mutex) in onboarding / driver repair. HidHide
   **1.5** accepts unelevated list writes, so elevation is strictly needed only for the device-restart step.
   `DetectSelfLockout` + a clickable startup corner notice surface the condition outside the install flow.
   ⚠ **On 1.5.230 the lockout self-heals once a pad is present, so staging it by removing the allow-list entry does
   not work** — measured on Windows 10 and again on Windows 11: with Radiata closed, an elevated
   `HidHideCLI --app-unreg` of the installed exe removed it from the allow-list, and the first cloak after
   relaunch put it straight back from a **non-elevated** process (`Hide()` calls `AddApplicationPath` before
   blocking), so `DetectSelfLockout` saw a whitelisted exe and no card appeared. The notice IS reachable when the
   startup check runs before the pad opens: a launch from a new exe path over a still-cloaked device fired
   `self-lockout detected at startup` and the card (Windows 11, Debug build, Sep 30 2026).
2. **The HidHide control device is exclusive — and Radiata is never the holder.** Kernel-enforced single
   open handle (`WdfDeviceInitSetExclusive` in the driver source). The Nefarius wrapper opens the device
   **per call** and closes it before returning (verified against v3.4.0 source), so Radiata holds nothing
   between calls — the old belief that "Radiata owns the device while running" was false; don't rebuild on
   it. The HidHide **Configuration Client** is the opposite: it holds the device for its whole lifetime, so
   while it (or a stuck `HidHideCLI.exe`) runs, *every* cloak call throws
   `HidHideDriverAccessFailedException` and capture degrades to raw passthrough — the game sees wheel
   input. Because the client is exactly what someone opens to diagnose a controller problem, this failure
   punishes investigation (Aug 11 2026: nine minutes of bleed-through). Handled by
   `HidHideManager.CloakFailure.Contended` (typed exception match — never message text, its Win32 half is
   locale-dependent), `DriverStatus.RunningHidHideTools()` to name the holder, a 15 s backoff **inside
   `Hide()`** (one policy for every caller — a per-call-site backoff missed the Xbox path once), and
   recovery within one backoff window of the holder closing. **Don't "fix" a contended cloak by sending
   the user to driver repair** — the driver is fine.
3. **A fresh HidHide install filters NOTHING until each device re-enumerates** (class filter +
   `/norestart` installer): the cloak reads as fully configured yet both pads stay visible in joy.cpl until
   a reboot. Found on the Jul 2026 clean installs. The elevated one-shot now restarts the pad devnodes
   (`pnputil /restart-device`) so the filter attaches immediately.
4. **The cloak blocks future opens only** — see the section of that name below. "Hide succeeded" must never
   be read as "no game can see the pad".

## The cloak blocks future opens only

Verified in the driver source (Aug 13 2026, `Logic.c`): the blocklist is consulted **only in the
file-create path** (`OnDeviceFileCreate` → `STATUS_ACCESS_DENIED`). There is no read/write filtering for
hidden devices and **no revocation of already-open handles** — nothing in HidHide can evict one. A process
that opened the pad during any un-cloaked window keeps reading it until *it* closes the handle or the
devnode restarts.

⚠ **This is about HID file handles, and it does NOT mean "a game already playing keeps working when you
cloak".** The two claims operate at different layers, and conflating them cost a real defect (Aug 29 2026): **XInput has no durable per-process handle** — it enumerates and polls slots — so hiding the devnode
cuts an XInput game off **immediately**, no handle revocation required. Measured on Overwatch: cloak
landed, game dead, probe saw zero pads. Whether a **DirectInput/HID** game survives the same treatment is
**assumed, never measured**; the prerequisite test has not been run.
Use this section for what it says (a leaked handle cannot be evicted), not as evidence
that cloaking mid-play is harmless.

**Steam makes this a first-class threat, not an edge case:** steam.exe opens every supported controller it
can see — process-wide, at device arrival, independent of any game — and never exits. Any un-cloak window
with Steam running hands it the physical pad; the re-cloak "succeeds" while Steam keeps forwarding the
physical input to Steam Input games alongside our virtual pad = permanent double input, invisible to every
check Radiata has (behaviourally confirmed Aug 13 2026: bleed survived focus cycles and cloak-success
readbacks; a fresh game process under the active cloak was clean; joy.cpl saw one controller throughout).

Consequences, each load-bearing:

- **The wheels toggle is routing-only.** The chord flips
  interception, never devices. The device-release paths are Passthru Mode, exit, and `UncloakForReset` — all
  deliberate, all understood to hand the pad to whatever is running until it re-enumerates.
- **The startup-order defect is MITIGATED (Aug 18 2026), not fixed — the handle still can't be revoked.**
  `SteamSentry` detects the condition (Steam's process predates the session AND the pad was present at
  launch AND the cloak confirmed — a heuristic, deliberately not handle enumeration) and cures it:
  silently relaunching Steam near startup — only within the first 10 minutes of the session, only when
  the desktop is genuinely idle (`DesktopIsIdle`: no foreground window, or Progman/WorkerW — NOT the
  taskbar, NOT Radiata's own windows), and only when no process is running from under a scanned game's
  install dir (`GameProcessRunning`, fail-closed) — a Steam relaunch kills Steam-launched games, and the
  old `PeekFrontmostApp() is null` gate read a tray click as an idle desktop and killed one (Aug 30
  2026). Otherwise: a persistent top-right "Unlock Controller"
  card offering pad power-cycle / △ relaunch Steam / □ Passthru Mode. The card stands down when a devnode
  Radiata holds cloaked is **removed** (`DBT_DEVICEREMOVECOMPLETE` naming an owned id — every handle to it
  died), an external Steam restart, Passthru Mode (which suspends the episode; detection resumes when it
  ends), or either button. ⚠ **Never on the reader's `Connected(false)`:** that edge also fires for a yield to
  another backend, a single failed XInput read and a manual reset, all of which leave the devnode and
  Steam's handle alive. A failed Steam process probe is "unknown", never "Steam gone". Do not re-diagnose the mechanism.
  **Widened beyond Steam:** other known plausible
  pre-cloak holders (Discord, GOG Galaxy, RivaTuner, DS4Windows) are detected by the same started-before-us
  heuristic and named quietly in the tray ("unverified (Discord)"); with no known match but the
  preconditions holding, the tray reads **"isolation unverified"** instead of asserting "isolated". Steam
  alone keeps the loud UX — its pad-grab is verified and unconditional. **Only the pad's departure lifts
  that downgrade:** a Steam restart (automatic, from the card, or user-opted) cures Steam's handle alone, so
  it ends the Steam episode and leaves "unverified (Discord)" standing (`SteamSentry`'s two latches).
  **A consented pre-emptive cure also exists** (Aug 26 2026): pre-checked "Restart Steam" opt-ins on OOBE
  step 1 and on the update prompt (the latter survives the installer swap as a marker file,
  `SteamRestartFlag`). Both queue `App.ArmUserSteamRestart`, which bounces Steam only once isolation is
  confirmed (10-minute window) — a bounce before the cloak is up would cure nothing. The user path
  (`SteamSentry.RestartSteamForUserAsync`) never fabricates sentry state: a failed user bounce must not
  raise the Unlock Controller card for a leak that was never detected. This is distinct from the rejected
  auto-kill of Steam — that was unconsented and mid-game; these are explicit
  one-shot opt-ins at moments the user already accepted disruption.
- **The only true take-back is a devnode restart** (elevated; the `--restart-pads` machinery exists), which
  severs every stale handle at the PnP layer. ⚠ Unverified over Bluetooth — the BT zombie-link history says
  the BT stack doesn't always respond to devnode-level surgery. A user-side equivalent: power-cycle the pad
  (its departure kills all handles; the re-open after reconnect hits the active cloak and is denied).
- **The cloak is retained while the pad is absent/unresolved** (`padGoneRetainCloak` in
  `UpdateInputCaptureCore`, Aug 13 2026): the block-list is keyed on the instance path, which is stable
  across reconnects, so a retained entry means the pad re-enumerates already cloaked and Steam never gets
  the device-arrival race (it won ~50/50 before). The failsafe un-cloak fires only for present-pad
  failures (ViGEm down, multi-pad guard); Passthru Mode / exit / reset still release explicitly. A failed
  device SWEEP counts as unresolved, not absent: it retains too, and keeps the watchdog armed.
- ⚠ **Retention only covers ids this session has already seen.** The first arrival of a transport not yet
  seen this session, such as an Xbox pad falling back from USB to Bluetooth mid-game, is an open window:
  the sweeps enumerate present devnodes only, so the Bluetooth `IG_` id can't be blocked before it
  exists. A process already polling XInput can open it first. Measured Sep 20 2026: a long-running
  observer had the pad 340–470 ms before the block landed, while a fresh observer afterwards saw only
  the virtual pad. The second fallback in the same session is born cloaked. The same window opens USB-first (confirmed Sep 28 2026): in a session started on Bluetooth, the first cable-in cloaks the composite root at once but its children only as they enumerate, ~2 s later. A related gap is covered: XInput can open a Bluetooth pad before PnP lists its `IG_` devnode (1.7 s apart, measured Sep 28 on Windows 10). The capture pass then keeps the existing Xbox target up but suppressed for up to 3 s (`ControllerCaptureContinuity.HoldNeutralForIdentity`), instead of removing the game's pad. Closing the first window
  would mean pre-blocking a remembered other-transport id, which is not done.
  Nothing groups the two transports: VID/PID differ (`0B12`/`0B13`), and so does PnP **ContainerId**
  (Bluetooth containers derive from the MAC; measured Sep 23 2026).
- **Manual Passthru Mode OFF is pending while a game is in the foreground**: during the hold **nothing** changes — no cloak, no
  virtual pad (cloaking mid-hold cuts an XInput game off instantly; measured) — and both land together at
  completion. There is **no latched target process**: completion = no game-like foreground app
  (`DetectRunningGameName`, 2 s poll in `PollPendingSafeModeOff`) or the 120 s deadline, whichever first;
  re-engaging Passthru Mode cancels. Surfaced via the corner toast ("Passthru Mode will turn off when you leave
  the game."), the "Passthru Mode Ending" hub chip, and the tray item — no app name anywhere. Auto passthru mode
  is exempt by construction — it already disengages at app exit, when the app's handles are gone.

## The cloak is the only thing that can outlive the process

**Design requirement: the controller must be 100% normal whenever Radiata isn't actively managing
it.** The ViGEm pad is process-owned and vanishes instantly on exit/crash/kill — no residue. Only the
HidHide cloak persists, so it is the entire risk surface. By state:

| State | Guarantee |
| --- | --- |
| Never launched | Untouched. |
| Clean quit | `TearDown` un-cloaks. |
| Managed crash | The three `CrashSafeUncloak` hooks un-cloak. |
| Hard kill (Task Manager, OOM, fast-fail) | Healed within seconds: a **ride-along watchdog** (`Radiata.exe --watchdog <pid> <startTime>`, self-spawned at startup, unprivileged, respawned up to 3× if it dies) waits on the primary's process handle and, on death, runs the same guard ladder as `--uncloak` (settle → mutex → process scan → persisted ids) before lifting only our ids. It detects death, not malfunction — wedged-state handling stays in-process. Watchdogs hold a `Local\RadiataWatchdog-<pid>` mutex so `AnotherRadiataRunning` never mistakes one for a live primary. |
| Power loss / BSOD (watchdog dies too) | Cloak stranded only until recovery: the startup self-heal lifts it on the next launch (verified), and the **logon task** (`RecoveryTask`, registered by the elevated driver-setup step) runs `Radiata.exe --logon` at every sign-in — with autostart on that launch *is* the app (its own self-heal lifts the cloak); with autostart off it lifts the persisted cloak ids and exits, mutex-guarded. See [INSTALLER.md](INSTALLER.md) ▸ Autostart. |
| Wheels **disabled** (both-Fn chord / tray / Disable Wheels slice) | **Routing-only (replacing the earlier "fully transparent" behaviour):** the pad and cloak stay exactly as they are; summons are ignored and input forwards 1:1 through the standing virtual pad, so games never see a device change. Rationale in "The cloak blocks future opens only" below. Native features (adaptive triggers, gyro) do **not** return on wheels-off — Passthru Mode is the path that hands games the real controller. |
| **Passthru Mode** (manual or auto) | No virtual pad, no cloak — the real controller, full features, zero kernel footprint. Now the ONLY user-facing device-level release short of quitting. |

## Cloak correctness — track outcome, not intent

Post-incident hardening, Jul 2026:

- `HidHideManager.Hide` **reports whether ids were actually blocked**; `App._cloakedIds` records only
  confirmed ids.
- Every capture pass re-attempts any connected id not yet cloaked, and the `Connected(true)` handler re-runs
  capture, so a late connect or a BT⇄USB transport change gets cloaked.
- The emulator retries a failed ViGEm start on a timer and **revives a dead ViGEm target** after consecutive
  submit failures.
- The elevated one-shot restarts only the **physical** pad devnodes passed to it. The VID-sweep fallback runs
  only on a fresh HidHide install/upgrade — the virtual DS4 shares Sony's VID and must never be restarted
  mid-game.
- The cloak record is written **before** the driver writes, so an interrupted pass can't strand ownership.
- **Same-model orphan recovery:** `Hide` adopts already-blocked IDs matching a requested controller's VID/PID, including absent other-transport IDs, without adding blocks for those absent devices. This recovery policy does not prove physical identity or ownership. Normal exit releases recorded/adopted IDs; uninstall releases the entire block list when no other Radiata copy is running. Failed removals retain recovery intent and abort uninstall. Concurrent same-controller cloak managers remain unsupported.

Aug 3 2026 (review triage) — three more, all in `UpdateInputCaptureCore`:

- ⚠ **"No ids" is not "no pad".** `ControllerReader.GetHidInstanceIds()` returns an empty list both when
  nothing is connected *and* when a pad we have open can't be resolved to a devnode (enumeration threw, paths
  failed to parse). The old code read both as "nothing to double" and **brought the virtual pad up
  uncloaked** — the exact double-input the cloak-first ordering exists to prevent, since
  `_controllerPresentForPad` is true in that state and doesn't veto the pad. Use
  **`TryGetHidInstanceIds`**, which returns `PadIdentity.NoPhysicalPad` / `Resolved` / `Unresolved`;
  only `NoPhysicalPad` may skip the cloak. **Don't reintroduce a bare `ids.Count == 0` test here.**
- ⚠ **Two or more physical Xbox pads = don't cloak at all.** `XboxDeviceTree` cloaks *every* Xbox-class pad
  (XInput hands out a slot, not a device identity), but only one is forwarded — so cloaking two made a second
  player's pad vanish. `GetPhysicalXboxInstanceIds(out int padCount)` now reports the pad count and capture
  refuses when it's `> 1`, staying in best-effort so both pads keep working. The real fix is device→slot
  identity (a planned controller picker). This did **not** change the shipped default: capture is still on.
- Capture outcome is surfaced to the user, not just the log: `NoteIsolationState` drives the **tray tooltip**
  ("Radiata — isolated" / "— not isolated (…)") and the self-drawn **corner card** (`ShowIsolationToast` — a
  roundrect since Aug 27 2026; the old duplicate tray balloon is removed), latched **per distinct reason** (not once per session — the reasons have different cures, so a
  second, different degradation must still warn). "Isolated" itself is downgraded to **"isolation
  unverified"** (naming a known plausible holder when one matches) while pre-cloak handles can't be ruled
  out — SteamSentry computes it; tooltip-only by design. README's controller section documents the same
  fallback cases.
- ⚠ **The tray is not a couch channel.** A tooltip needs a mouse hover, so it is structurally invisible in
  the session that needs it most — a controller-only user in a game is told nothing by it, and the corner
  card can't be counted on over every game surface either. The **hub notice on wheel open** ("Game Sees This
  Input") is the channel that actually reaches them, gated on `PeekFrontmostApp() is not null` so the bare
  desktop doesn't warn about a game that isn't there. It repeats per wheel by design: the leak is live
  *now*, and an hour-old warning doesn't say it still is. **Don't demote this to a once-per-session latch.**

**Diagnostics:** `%APPDATA%\Radiata\radiata-trace.log` (timestamped, rotates ~1 MB) carries the
`[Capture]` / `[HidHide]` / `[Emu]` / `[Controller]` lines.

Two lines exist specifically to make pad churn legible after the fact, because a game that binds a pad on
arrival can be left bound to one Radiata has since removed — input goes dead and toggling the wheels only
churns it again:

- `[Emu] ds4|x360 connected — pad generation N` / `… disconnected — pad generation N was up Xs`. Every pair
  is a HID arrival/removal delivered to the running game.
- The `[Capture] … virtual pad released` line names **which** `wantPad` conjunct failed (wheels off / safe
  mode / no controller / cloak not confirmed). It previously reported "wheels disabled" for all four, so a
  cloak failure logged as a deliberate user toggle — keep it attributing the real cause.

## Trade-off, accepted deliberately

While the wheels are **enabled**, native mode forfeits genuine-DualSense features (adaptive triggers,
haptics): the game sees a virtual DS4 and the physical pad stays cloaked. This is the always-on choice — it keeps wheels opening and closing with **zero device-swap blip**, at the cost of those
features during active play. They return in full the moment the wheels are disabled or Radiata exits. The
alternative (cloak only while an overlay is on screen — features during play, but a controller re-detect on
every wheel open) was considered and **declined** in favour of the no-blip feel.

## Xbox / XInput pads — capture SHIPS ON

⚠ **The old claim that HidHide "cannot hide an XInput pad, which is why reWASD ships its own kernel hider"
was WRONG** — an integration bug of ours generalised into a platform law. Do not restore it. HidHide
installs upper filters on **three** classes: `HIDClass`, **`XnaComposite`** (`{d61ca365-…}`, Xbox 360/XUSB)
and **`XboxComposite`** (`{05f5cfe2-…}`, Xbox One). Our Jul 2026 test fed it HID-class instance ids only,
but the devnode a game actually reads is the **XUSB function device**; per nefarius/HidHide#39 an XInput pad
needs the **composite parent AND its children** blocked.

**How it works:** `XboxDeviceTree.cs` walks the two Xbox setup classes and returns each physical pad's whole
subtree (parent + descendants). Virtual pads are excluded by **PnP ancestry** (`PnpAncestry.Classify`, shared
with the Sony path's `ControllerProfile.ShouldSkipHidDevice`) — VID/PID cannot work, because our ViGEm X360
enumerates as `USB\VID_045E&PID_028E\01`, byte-identical to a genuine Microsoft pad. Any one of four marks
a devnode virtual: an ancestor whose **`Service` contains `VIGEM`**, an ancestor **id containing `VIGEM`**, a
**direct parent under `ROOT\` or `SWD\`**, or a `ROOT\SYSTEM` ancestor. ⚠ Not `ROOT\SYSTEM` alone: HP OMEN's
fork sits under `SWD\DRIVERENUM` → `ACPI\HPIC0004` → `ROOT\ACPI_HAL`, and that single test called Radiata's
own stand-in physical, tripped the multi-pad guard on it and toggled the pad every 3 s (*Foreign ViGEmBus
forks* above). The `[XboxTree]` lines name each pad's parent and the matched fingerprint, so the next
misclassification reads off the trace. Capture is **cloak-first**: the tree is blocked before any virtual pad exists, and the pad
only comes up if `Hide()` confirms, so a game can never see two pads
(`UpdateInputCaptureCore`: `wantPad = … && (xinputXbox ? xboxCloaked : sonyCloakOk)`). A captured Xbox pad
always gets a virtual **Xbox 360** stand-in regardless of the Xbox Mode toggle — a virtual DS4 would flip
its button prompts. **Rumble forwards** (ViGEm `FeedbackReceived` → `XInputSetState` to the reader's live
slot; zero-vibration on loop exit so motors can't stick).

**Bluetooth Xbox pads have their own cloak path (Aug 27 2026; hardware-verified Aug 30 2026).** Over BT the pad
enumerates under **HIDClass via `xinputhid.sys`** — no XUSB devnode exists — so
`XboxDeviceTree.GetBluetoothXboxInstanceIds` sweeps HIDClass for the **`IG_` XInput-compatibility marker**
with **`BTH…` ancestry** (wired pads' IG_ children have USB ancestry and stay with the composite tree;
virtual pads fail the ancestry test — never select by VID/PID). Blocking that HID entry
**does** starve XInput for non-allow-listed processes — measured three ways with `XInputProbe` (development repository only, not in the public source) — 0 blocked /
1 unblocked / 1 blocked-but-allow-listed — which overturned the long-standing "BT can't be cloaked" prior.
⚠ **The virtual pad is gated on an OBSERVED isolation check, never on `Hide()` returning success**:
Radiata itself is HidHide-allow-listed, so its own XInput view proves nothing — after the
block lands, `XboxBtIsolationProbe` runs **`Radiata.ArcadeHost.exe --xinput-count`** (a different,
normally non-allow-listed image; its access policy can differ from a game's) and the stand-in comes up only on an observed **zero**.
Anything else — pads still visible, probe missing, probe crashed — **un-cloaks and stays best-effort**: a
blocked pad with no stand-in would be a dead controller, worse than bleed-through. The verdict is keyed to
the exact cloaked id set (a re-pair re-verifies) and held for the session. ⚠ **A count only counts for a
pad that was there:** an absent pad reads zero too, so `Core/BtObservationGate` accepts a verdict only when
every armed id is present (a direct `CM_Locate_DevNode` lookup before and after the probe — deliberately not
the 250 ms sweep memo) and still cloaked, and only from the still-pending observation; anything else resets to
`None` and re-observes. The deadline's `Refused` bumps the probe generation, so a late probe cannot overwrite
it. The multi-pad guard counts
**wired + BT pads together**.

⚠ **The gate is enforced by a cross-branch guard in `UpdateInputCaptureCore`, not by the BT branch
alone** (Aug 29 2026 — closes the Aug 28 fail-open race; **hardware-verified Aug 30 2026**). The
original wiring failed OPEN: on a USB→BT swap the **wired** branch (whose devnodes are still enumerable
for a moment) granted the pad while the BT ids sat cloaked-but-unverified, and the probe's
`_emulator.Active` bail-out silently reset to `None` and re-armed forever — no `OBSERVED` line, ever.
Now the pad is withheld whenever **any** cloaked BT id is unverified, whichever branch would have granted
it. This removes Radiata's own output from the observation; another remapper can still create input during the probe. The unverified hold is
hard-bounded (`BtObserveTimeoutMs`, 15 s): expiry, a stuck `Pending`, and a probe exception all resolve
to `Refused`/re-arm — never a silently-unverified pad, never a dead controller. A non-zero count gets
exactly **one** re-observation after a 1 s settle before `Refused` sticks (startup's cloak churn once
poisoned a single sample and cost a whole session's isolation); probe-unavailable never retries. The gate passed on hardware (every
clause: Aug 30 2026 on Windows 11 with a genuine Core pad, and the multi-pad-across-transports clause
Sep 15 2026 on Windows 10) — the hand-verification method below remains for future triage.

**Shipping state:** `SystemConfig.XInputCapture` defaults **true** — an Xbox pad
isolates exactly like a Sony one. Renamed from `experimentalXInputCapture`; the old key is ignored, so a
pre-rename config picks up the new default (deliberate — it was default-off and nobody had opted out). No
UI; setting it **false** in `config.json` is the escape hatch that forces best-effort mode.

**The fail-safe is what makes the remaining unknowns acceptable:** cloak-first means a pad whose tree won't
block — untested family, HidHide missing, a peer mapper holding it — never gets a virtual pad. It degrades
to best-effort (wheel works, input bleeds through while a wheel is open) rather than breaking. Onboarding
therefore treats every pad kind identically (same status, glyph, driver recommendation, skip warning),
because ViGEm + HidHide are load-bearing for Xbox pads too.

**Verified on hardware, wired/dongle.** The original cycle (GameSir, wired + 2.4 GHz dongle, Windows 10):
cloak deterministic across cold restarts; 64 fps in = 64 submits/sec out; in a real game — single input,
wheel-open suppression with zero bleed-through, clean handback; replug / disable-enable / hard-kill all
recover. **Genuine Microsoft pads, Aug 27–28
2026 (Windows 10):** the Series pad (`045E:0B12`) wired — cloak + wheel-open suppression over a real game
with zero bleed-through, arrival/removal re-cloak in 1–6 ms off the device
notification, probe overflow handling; and Series + a genuine wired Xbox 360 pad
(`045E:028E`) — the multi-pad guard with both pads usable in-game. The old "genuine Microsoft pad
(XUSB/GIP tier) unverified" caveat is closed for wired capture — don't restore it.

**Multi-pad semantics:** we cloak **every** physical Xbox-class pad present — XInput hands out a slot, not a
device identity (selecting a pad by device would need GameInput; a controller picker is planned). When another
controller takes over (a Sony pad preempts by priority), the Xbox tree is **un-cloaked** so the idle pad
reverts to an ordinary visible controller; verified with an Edge + GameSir both usable at once in Steam Big
Picture. **Regression watch:** getting this wrong strands a **dead pad** (the Aug 2 bug).

**The guard waits 5 s before un-cloaking an established capture** (`ControllerCaptureContinuity.HoldForMultiPad`,
`MultiPadConfirmMs`). A USB↔Bluetooth switch lists
BOTH transports of one pad for up to ~3 s (measured on Windows 10, Xbox Series pad, Bluetooth→USB). Nothing in PnP links them:
VID/PID and ContainerId both differ. Un-cloaking there handed the pad to Steam, wiped the retained ids, and
re-created the stand-in mid-churn, which set off "Controller selection paused". During the wait, the owned cloak and
the stand-in stay, and the new listing is not cloaked (it may be a second player's pad). Input is forwarded only
while the reader is on the source it had when the second pad appeared; after a reconnect, it's held neutral. A
wait is not a guard trip for the churn breaker. A genuine second pad reaches today's un-cloak 5 s later. A wedged
Bluetooth link whose listing never leaves outlasts the wait, and the guard trips (CONTROLLERS.md ▸ the Xbox
Bluetooth wedge).

**Known behaviours:** a dongle pad **sleeps on idle** and re-enumerates on wake (capture re-engages via the
replug path); child devnode ids are **not** stable across a replug, so ids are re-enumerated every pass and
never cached; there is a sub-second **cloak → un-cloak → re-cloak churn** at startup (pre-existing, also on
Sony pads, cosmetic).

**The pad-count churn breaker** (`App.PadCountChurnBreaker`, a thin adapter over the Core rule
`PadCountChurnRule`, pinned by the harness group `churn`): capture's own action — plugging the stand-in —
changes the input to its own decision (the physical pad count) whenever a software bus goes unrecognised, and
nothing else damps that loop. The multi-pad guard tripping **3 times inside 20 s** latches best-effort for
**60 s** (no cloak, no stand-in, watchdog stood down, note `not isolated (pad count unstable)`, its own toast
and Arcade-guard card); a **second latch in the session holds** until every Xbox-class pad is gone, Passthru
Mode toggles or the app restarts. The latch line names the transient ids. A genuinely flapping second pad
gets the same treatment — best-effort with a card beats a controller that connects and disconnects every
3 s. The multi-pad **toast** waits 8 s (other causes 3 s) so a transport switch's brief overlap does not name
itself. ⚠ A trip is a guard-ON edge, and the transport-switch wait above means the guard turns on only if the
second pad is still listed at the wait's expiry scan — so staging the latch needs a second pad present ≳6 s
and absent ≲3 s on a ≤10 s cycle, three times. Hand plugging cannot hold that, a pad with a live wireless
link never leaves, and the XnaComposite node refuses a live disable. ⚠ A GameSir pad plugged in by cable while its dongle link is still up enumerates **twice** (tester,
Sep 17 2026: the dongle alone, pad off, enumerates nothing — the second devnode is the live link, not the
idle dongle), so the guard holds for as long as both links stay up and the toast is then correct; it says so.

**Still unverified, shipping on the fail-safe:** deliberate **Steam Input coexistence**. Wired capture on
genuine Microsoft pads is verified (above), and so is the **BT cloak path end-to-end on hardware**: the
HidHide behaviour it rests on is measured on the genuine Series pad over BT (the three-way probe:
0 blocked / 1 unblocked / 1 allow-listed) and the shipped gate passed its end-to-end run (above). Steam
Input coexistence is the only remaining gap.

**Still true:** no user-mode exclusive XInput grab exists, so a filter driver stays mandatory. An Xbox-class
pad over **Bluetooth** that emulates a DualShock 4 (most, including GameSir) enumerates as HID
(`Kind=PlayStationOther`) and takes the Sony path with no flag needed — still the zero-config route, at the
cost of PlayStation button prompts.

### Stuck root: the pad's USB root is present but its input children never appear

A wired Xbox pad's composite root (`USB\VID_045E&PID_0B12\…`, class XboxComposite) normally grows its
`IG_00` XInput and HID children within about a second. After a devnode restart or a cable replug the root
can enumerate `OK` and never create them (seen on a battery-powered Xbox Core pad; only a power-cycle of the
controller restored them). `XboxStuckRootTracker` classifies each childless root:

- **Settling** (childless under 10 s, timed from the first sweep that saw it childless): not a pad. It is not
  cloaked, not counted in `padCount`, no card.
- **Stuck** (childless 10 s or more): still not a pad, plus `XboxDeviceTree.StuckRoots` reports it.
  `UpdateInputCaptureCore` writes `[Capture] Xbox root <id> has no input child after <n>s — controller needs a
  power-cycle; not cloaked` ONCE per episode and shows the `Controller needs a restart` card (turn the
  controller off and on, then plug it in). The `Controller selection paused` card is suppressed while the
  episode is active; both use the single notice slot.
- **Episode end:** the root gaining a child, or leaving the tree, resets the once-latch silently and closes the
  card; the normal connect path takes over. A root that loses its children again starts a new 10 s window.

Excluding the root leaves `padCount` at 0, so the pass takes the no-devnode branch and
`padGoneRetainCloak` still holds the cloak on the pad's earlier REAL devnodes. A failed sweep leaves the
previous stuck verdict standing. Tested by `TestHarness` group `stuckroot`; the live provocation is an elevated
`pnputil /restart-device` on the root, which is not a supported recovery (it strands the children).

## The connect-time phantom report (fixed Aug 2 2026 — the trap is worth knowing)

A freshly `Connect()`-ed ViGEm target's OS-visible report is a fixed non-neutral deflection
(`LX≈-3356 LY≈-1869 RX≈-3255 RY≈-848`, packet 1), and **submitting a neutral report does NOT clear it**.
Measured: ~130 real zero-value submits over 6 s never moved the packet number, because the client only
pushes when a report differs from what it believes is current — and its post-connect shadow already reads as
neutral. `ConnectLocked` now submits a **non-zero sentinel then neutral** (two real diffs), landing zeroes
synchronously before `Connect()` returns.

Measured on the **X360** branch (an XInput device, so `XInputGetState` can observe it). The same sentinel
was applied to the **DS4** branch by symmetry — identical `AutoSubmitReport=false` + `Connect()` shape — but
whether a virtual DS4 shows the symptom is **unverified** (it isn't an XInput device, so checking needs a
DirectInput/HID probe). Trade-off: a full-deflection sentinel exists for microseconds pre-return; a game
cannot realistically sample it.

## Anticheat Passthru Mode

Settings ▸ **Passthru Mode** tab (the control class is `ExceptionsEditorControl`; the tab label is "Passthru Mode").

- **Global** `SystemConfig.CaptureSafeMode` checkbox: no virtual pad, no cloak, so a strict
  kernel-anticheat title runs clean.
- **Per-app auto list** `SystemConfig.SafeModeApps` — HidHide-style full-path entries, each flagged
  *running* vs *frontmost-only*. App's 2 s `PollSafeModeApps` watcher
  (`ConfigureSafeModeWatcher`) drives a **runtime-only** `_autoSafeMode` flag.
- `SafeModeActive` = manual **OR** auto, and gates the pad + cloak. **Automation never writes the manual
  toggle** — that separation is the point.

Posture: we do **no** injection or hooking (clean on the ban line), and virtual-pad emulation is
historically tolerated by EAC/BattlEye — but the trend is tightening and **Vanguard (Valorant) / CoD
Ricochet are strict** (they flag low-level drivers and block emulation), with **HidHide the riskier
component**. Never market a hiding/evasion angle.

## Verifying and provoking the stack by hand (learned the hard way, Aug 28 2026)

**Is the cloak ACTUALLY working?** Run the same probe the BT gate uses, directly:

```bash
bin/Debug/net8.0-windows/Radiata.ArcadeHost.exe --xinput-count
```

**Exit code is `10 + the pad count` (10–14); anything else is an error** (the 10-offset keeps counts
unambiguous next to the pipe mode's 1/2 failure codes). So `11` = one pad visible. This is the honest
consumer's-eye reading precisely because `Radiata.ArcadeHost.exe` is a **different image and is not
HidHide-allow-listed** — `Radiata.exe` is, so its own XInput view proves nothing. Read it against what
should be up: with a virtual pad presented, `11` means only the virtual pad is visible (cloak effective);
`12` means a physical pad is leaking. With no virtual pad, the gate's own criterion is `10` (zero pads).
⚠ Keep `XInputProbe` and `ArcadeHost` **out of the allow-list** or every such measurement lies —
`HidHideCLI.exe --app-list` to check.

**ViGEmBus cannot be taken down on demand.** Both routes fail, so don't plan a test around them:

- `Stop-Service ViGEmBus` → *"cannot be stopped"*, even elevated. It is a kernel bus driver with
  `StartType: System`; `CanStop` reports true and lies. Task Manager hits the same SCM path — there is no
  user-mode process to end.
- `Disable-PnpDevice` on the bus's devnode (FriendlyName *"Nefarius Virtual Gamepad Emulation Bus"*; find it by
  `Service -eq 'ViGEmBus'` — the instance number is per-machine: `ROOT\SYSTEM\0001` on one Windows 10 machine,
  `ROOT\SYSTEM\0003` on one Windows 11 machine) → *"Generic failure"* **while Radiata is running**, even with no pad attached
  and no virtual pad up: Radiata holds the ViGEm **client** connection open for its whole lifetime, not
  just while a target is plugged.

**So to stage a ViGEm outage:** stop Radiata → `Disable-PnpDevice` (succeeds; Status becomes `Error`) →
start Radiata → attach a pad, which makes a virtual pad genuinely expected against a broken bus.
`Enable-PnpDevice` restores it, and `StartType: System` means a reboot would too. ⚠ This stages the failure
*before* the pad arrives; a live pad being pulled out from under a running virtual pad is **not
reproducible**. Verified behaviour when it happens: `emulator start failed: VigemBusNotFoundException` →
`[HidHide] un-cloaked` (fails safe — never a blocked pad with no stand-in), five bounded start retries
about 2 s apart, then `[Emu] virtual-pad start retries EXHAUSTED (5)` and, because the bus is ABSENT,
`[Emu] watchdog: ViGEmBus driver missing — standing down until it is installed …`. No outage episode
opens and no heartbeat runs while standing down. The isolation card still fires and names the missing
driver. When the driver returns, the device-arrival capture pass usually brings the virtual pad back
before the watchdog's next tick (~1.5 s observed); the watchdog's own `driver present again — resuming`
line is the fallback.

📌 **A ViGEm outage no longer churns HidHide while the bus is MISSING.** Each capture pass used to do a
full `cloaking N instance(s)` → failed start → `un-cloaked` cycle, so a burst of device notifications made a
burst of driver writes. The first start failure now asks the driver directly (`ProbeDriverInstalled`) and
latches `App._busMissing`; while that is up the pass **skips both cloak branches entirely**, so there is one
lift and then nothing. It clears from a read-only probe at the TOP of the next pass, before any cloak
decision, so the pass that finds the bus back still does the normal cloak-then-start in full order — the
probe is the only cost, and only while the latch is up.

⚠ **`busMissing` is NOT `padDownByDesign`, and it is tested ABOVE both cloak-failure branches in the
isolation-note ladder.** Skipping the cloak is what this state does, so in the ladder's natural order the
note would read as the generic Xbox-fallback or cloak-failed wording and the card that names the actual cure
— *"Input Isolation Blocked / The ViGEmBus driver is missing — reinstall from Settings ▸ Advanced ▸
Install/Repair Drivers"* — would be lost exactly when it is needed. That card is the reason the obvious
version of this fix (a guard beside the multi-pad one, setting `padDownByDesign`) is wrong on its own;
anyone touching the ladder re-verifies the whole ViGEm-outage card matrix.

With the bus PRESENT but the start failing, the watchdog still re-runs capture every 10 s and the churn is
sustained — unchanged, and correct: there a cloak is exactly what the next successful start needs.

## Pad identity — what you cannot infer

- **VID/PID never proves a pad is physical.** Our ViGEm X360 enumerates as `USB\VID_045E&PID_028E\01`,
  byte-identical to a genuine Microsoft pad but for the **instance id**. `XboxDeviceTree` excludes virtual
  pads by a `ROOT\SYSTEM` ancestor test for exactly this reason.
- ⚠ **Steam can present controllers of its own** (Steam Input). A
  Steam-presented pad can make the **multi-pad guard fire with one physical pad attached**, and can make a
  probe's count read high. Since a Steam virtual pad would plausibly also be `045E:028E`, this is the same
  trap as above wearing a different hat. Before trusting any pad count, check what is actually present:
  the trace's `[XI] probe:` line plus
  `Get-PnpDevice | Where InstanceId -match 'VID_045E|VID_28DE|IG_'`.
- **One physical pad changes identity across transports.** The Series pad is `045E:0B12` over USB and
  `045E:0B13` over Bluetooth — and it will **switch to BT on its own within ~2 s of a USB unplug** if it
  is powered on and paired. "Unplugged" is not "disconnected": to remove it, power the pad down (hold the
  Xbox button ~6 s) or turn off the PC's BT radio.
- **When the Xbox pads go, a virtual DS4 stands up for the 10 s disconnect grace** (`ds4 connected — pad
  generation N`) before teardown — kind detection falling back with no Xbox pad present. A game bound to
  the pad briefly sees an Xbox controller become a PlayStation one.

## Steam restart consent timing

The update dialog tracks Steam presence while awaiting Update Now, retaining the user's checked choice
through disappearance/reappearance. Acceptance snapshots the displayed choice and checks that Steam is
still running; polling and editing stop during download. No offer first discovered during download may
be accepted implicitly. Failed/cancelled downloads restore the offer. The stored consent, not a later
checkbox read, controls the existing post-download restart marker.

A pending restart has a dedicated ten-minute expiry timer. Successful isolation before expiry consumes
the request and stops the timer; expiry clears it even with no new capture event. Reevaluation follows
ordinary detection and warning deferrals, so expiration alone cannot diagnose a Steam leak. The automatic
startup cure and its idle/no-game guards are unchanged; the consented path remains distinct.
