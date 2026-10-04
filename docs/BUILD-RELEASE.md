# Build, versioning, packaging, and release

**Read this when:** bumping a milestone, cutting a ZIP, changing the csproj, or touching the uninstaller.

## Everyday loop

```bash
dotnet build ControllerWheel.csproj -c Debug
```

- **Kill any running `Radiata.exe` first** — building or launching over a live instance fails.
- After touching Settings XAML or shared styles, run the smoke probe — see
  [SETTINGS-UI.md](SETTINGS-UI.md).

## Versioning

`0.<ReleaseMinor>.<build.minor>.<YYMMDD>` (pre-1.0; bumps to `1.0` at public launch).

- **`ReleaseMinor`** lives in `ControllerWheel.csproj` and is **hand-bumped at milestones only**.
- **`build.minor`** lives in the tracked `build.minor` file and is bumped **once per commit** by
  `.git/hooks/pre-commit` (which stages it; a local hook, development repository only, not in the public
  source). The build only reads it, via the `SetBuildVersion` target, so a build made before a commit
  reports the previous number. `tools\build-installer.ps1` passes the value it read as `/p:PinnedBuildMinor`
  so a commit landing mid-build cannot split the payload's version from the setup filename.
- The 4-part string is `InformationalVersion`; `AssemblyVersion`/`FileVersion` stay numerically valid as
  `<Major>.<Minor>.<build>.0` (the date field would exceed the 16-bit field limit).

### The milestone ritual

**`App.OobeCurrentVersion` does not move with a release or a `ReleaseMinor` change.** It changes only on
an explicit decision to re-run first-run setup for every existing user, which a flow tweak, a copy pass or
a milestone on its own does not justify.

### Release naming — Option A, no counter reset

**The release is named after the EXACT version the app reports.** There is no separate "marketing" number
and no counter reset: a release ZIP is just a snapshot of whatever HEAD's version is, so dev iteration and
published release always carry the same number.

`<ver>` = `0.<ReleaseMinor>.<build.minor>` — the `.<YYMMDD>` field is dropped from the folder/ZIP name
(e.g. `Radiata-0.9.21`). At a milestone, bump `ReleaseMinor` **only**; `build.minor` keeps climbing and
never rewinds.

## Packaging

**Packaging does not wait for a Help translation pass.** Translations are batched and run only when the
maintainer asks for one (see [LOCALIZATION.md](LOCALIZATION.md)); the es/de/ja/ar maps are usually behind the
English text by packaging time, and untranslated strings fall back to English. `--check-help-locales` reports
the drift when a pass is requested.

### Build flavours — one define, three channels

| Flavour | How it is cut | `PUBLIC_RELEASE` | What differs |
| --- | --- | --- | --- |
| **Dev** | `dotnet build -c Debug` | off | Everything, plus the Debug-only switches (`--lang`, `RADIATA_DEV_FEED`). |
| **Tester** | `tools\build-installer.ps1` with NO `-Public` | off | Everything; the arcade level-skip aid is in. |
| **Public** | `tools\build-installer.ps1 -Public` (what `release.yml` runs) | **on** | `Core\ReleaseGates.cs`: **Internode, drop-in Arcade games and drop-in Material themes are withheld** (not registered, not created, dropped from Help and the exports), as are the tray's Show in Explorer and the open wheel's Select material cycle; `ArcadeDebug.LevelSkip` is off. Step (f2) of the script fails the cut if the public Help export still names a withheld feature. |

`ReleaseGates` is the only reader of the define; the withheld features' code and assets stay compiled in
every flavour, and a public user whose config still names one degrades on the existing paths (Launcher /
Pearl). Reintroducing a feature is a const flip in that file. Any new surface of a withheld feature (a Help line,
a picker entry, a folder, a tray item) consults the gate. ⚠ The public Help site is exported from the
**`-Public` staged exe**, not the Debug build ([LOCALIZATION.md](LOCALIZATION.md) §2).

**The installer is the ONLY published artifact** — `Radiata-<ver>-setup.exe` via
`tools\build-installer.ps1`. The portable ZIP below is a dev/tester artifact, cut only when explicitly
asked and never published (the in-app updater installs the setup exe regardless of how the running copy
got there, so a ZIP install that takes an update converts to an installer install).

```bash
dotnet publish ControllerWheel.csproj -c Release -r win-x64 --self-contained true -o publish\Radiata-<ver>
```

Then: drop the `*.pdb`, drop in `README - Install Radiata.txt` (substituting `{VERSION}` — the setup-exe
filename mention in the install steps is the one place it appears), and `Compress-Archive` the folder to
`publish\Radiata-<ver>-win-x64.zip`. `publish/` is git-ignored.

**Publish is single-file** — `PublishSingleFile` / `IncludeNativeLibrariesForSelfExtract` /
`EnableCompressionInSingleFile`, all **RID-gated** so a plain `dotnet build` is untouched (normal multi-file
Debug for fast local iteration). **No trimming: WPF isn't trim-safe.**

The portable ZIP must include the full publish output: `Radiata.exe`, the loose `drivers\` folder,
`Uninstall Radiata.cmd`, the README, the complete `ArcadeHost\` subfolder, and
`Radiata.helper-files.txt`. The helper is self-contained when the app is published self-contained;
do not replace it with the six-file development build. Driver installers remain beside the executable,
outside the single-file bundle.

The installer uses the same published helper layout. `tools\build-installer.ps1` strips PDBs, updates
the helper hash manifest, removes the portable uninstall command, then compiles `packaging\radiata.iss`.
It preserves running applications and creates a new staging directory per invocation.
`-StageOnly` stops after the PDB strip and prints the staging path (also written to
`publish\staging-path.txt`); `-FromStaging <dir>` runs everything after the strip on that folder, with the
version the first run recorded in it. `release.yml` uses the pair so it can sign the inner executables
between them; a plain run does both halves, and the local ritual stays a plain run. See
[INSTALLER.md](INSTALLER.md) ▸ Signing and for the contracts. Review root payload changes against
`Core/PortableUninstall.cs`; helper entries are generated in the hash manifest.

The `README - Install Radiata.txt` template is **BOM-less UTF-8** — read/write it with .NET
`ReadAllText`/`WriteAllText` or em-dashes mojibake. Source: `packaging\README-Install-Radiata.txt`.

**Not code-signed yet** → testers get SmartScreen ("More info → Run anyway").

## Uninstaller

`Uninstall Radiata.cmd` runs the app's `--uninstall` mode: a confirm dialog → an elevated `--uninstall-run`
that removes the Run-key entry, the HidHide allow-list entry and cloak, and the recovery task; offers the
shared drivers + settings as **OFF-by-default** opt-ins; then self-deletes its files (via a
random-named temp script — a fixed `%TEMP%\radiata-uninstall.cmd` would be a race).

⚠ **The self-delete removes a MANIFEST, never a recursive wipe.** A recursive `rmdir /s /q` of the whole
install directory is the shape to avoid. `IsDeletableAppFolder` blocks the catastrophic shapes of that —
drive roots, the profile, Documents — but it cannot block the realistic one: extract the ZIP's *contents* into
a folder that already holds other things (`C:\Games` being the obvious one), and `Radiata.exe` sits beside
them satisfying every guard while the wipe takes the lot. So `ScheduleFolderDelete` deletes
the explicit root-file list in `Core/PortableUninstall.cs` and hash-matching helper files from `Radiata.helper-files.txt`. An encoded PowerShell worker treats paths as literal data, checks the captured manifest hash and reparse boundaries, and removes directories only when empty. Unknown or modified helper files survive. Add newly shipped root files to the explicit list; helper publish files enter the generated manifest. `IsDeletableAppFolder` remains defense in depth. Locked files or a failed deferred worker can leave files behind; inspect the folder after uninstall.

**Driver removal verifies its postcondition.** `RunRegisteredUninstall` matches the ARP entry on DisplayName
**and `Publisher` containing "Nefarius"** (we launch the matched `UninstallString` *elevated*, so matching a
fork or repackaged bundle is not a cosmetic error), waits a bounded 120 s instead of forever (a Burn bundle
that shows UI under `/quiet` never exits), and checks `DriverStatus` afterwards instead of trusting the exit
code alone for the user-facing per-driver line. It returns a `DriverSetup.UninstallOutcome`, and **a present
driver with no Nefarius entry is `NoUninstaller` — another program's fork (HP OMEN Gaming Hub ships one),
reported as left in place, not as a failure** ([INPUT-CAPTURE.md](INPUT-CAPTURE.md) ▸ *Foreign ViGEmBus forks*).

⚠ **The check is applied asymmetrically, and that's deliberate — don't "tidy" it into a symmetric one.** Both
probes are registry reads (HidHide's version key, ViGEmBus's driver *service* key), and a kernel driver's
service key can sit in pending-delete until reboot while the uninstall genuinely succeeded. So:

| Exit | Driver still detected? | Result |
| --- | --- | --- |
| `0` | yes | **success**, with a loud trace line — failing here would tell users a working uninstall failed |
| `0` | no | success |
| `3010` / `1605` | not checked | success (reboot-staged / was never installed — `1605` *is* the goal state) |
| other | no | success — the outcome is what was promised |
| other | yes | **FAIL** (this is the case that used to pass) |
| timeout | either | decided by the probe; the process is never killed — a half-killed driver uninstall is worse than a slow one |

## Command-line switches

| Switch | Purpose |
| --- | --- |
| `--logon` | What the logon scheduled task runs. Autostart on (Run key present) → this launch is the app, seconds after sign-in; off → lift an orphaned HidHide cloak and exit. Honours Windows' Startup-apps toggle and the quit marker. See [INSTALLER.md](INSTALLER.md) ▸ Autostart. |
| `--autostart` | What the Run-key entry runs (Explorer reaches it minutes after logon). Exits silently, with no show-request, when the task already started the app, when autostart is off, or when the user quit Radiata earlier in this logon session. |
| `--uncloak` | Lift an orphaned HidHide cloak. No-ops if the app is running (mutex-guarded). Kept for logon tasks registered before `--logon`; they re-register on the next elevated whitelist pass. |
| `--hidhide-whitelist [--restart-pads <base64 ids\|sweep\|none>]` | Elevated self-registration; handled before the single-instance mutex. |
| `--install-drivers` | The setup wizard's optional drivers task, also runnable by hand: installs or updates ViGEmBus and HidHide behind a small progress window, with no tray app and no config read or written. Relaunches itself elevated (one UAC prompt) when it is not. When both drivers are already current it skips the install but still runs the elevated pass: the HidHide allow-list entry and the logon task. Exit `0` = current or installed, `1` = incomplete or failed, `3` = elevation declined. See [INSTALLER.md](INSTALLER.md) ▸ The drivers task. |
| `--owner-sid <sid>` | Modifier for the elevated passes (`--install-drivers`, `--hidhide-whitelist`, `--remove-recovery-task`): names the Windows account the logon recovery task belongs to, so an elevated copy that runs under a different account still acts for the invoking user. Must be a local/AD (`S-1-5-21`) or Entra (`S-1-12-1`) account SID. The app passes it to its own elevated copy; never type it by hand. |
| `--remove-recovery-task` | Remove this exe's logon recovery task for the account and nothing else (no registry, driver, process or file cleanup). It never elevates itself; the app launches it elevated, with `--owner-sid`, when it cannot delete an admin-created task. Exit `0` = removed or absent, `1` = failed or the task is not this exe's. |
| `--export-controls [path]` | Export the in-app Help as Markdown from `Core/HelpContent.cs` (defaults to `CONTROLS.md` in the cwd; the committed copy is kept in the development repository only, and the in-app Help is also published at getradiata.app/help). Exits immediately. |
| `--check-help-locales [path]` | Report MISSING / STALE Help translations per language. See [LOCALIZATION.md](LOCALIZATION.md). |
| `--check-locales [path]` | Both parity reports in one file, Help maps then UI maps (defaults to `locales.txt` in the cwd). The UI half needs string sets only the shell can enumerate, so the shell passes them in. Exits immediately. See [LOCALIZATION.md](LOCALIZATION.md) §2. |
| `--dump-help-locale <code> [path]` | Regenerate one language's Help map as C# source in `SourceStrings()` order: translations whose English key still exists are kept, the rest are blank TODOs (defaults to `help-locale-<code>.cs`, UTF-8 with BOM). Copy it over `Core\HelpText<Xx>.cs` once filled. Exits immediately. See [LOCALIZATION.md](LOCALIZATION.md) §2. |
| `--dump-ui-locale <code> [path]` | The same skeleton for the UI maps, in catalog order and including the shell-side strings (defaults to `ui-locale-<code>.cs`; target `Core\UiText<Xx>.cs`). Exits immediately. See [LOCALIZATION.md](LOCALIZATION.md) §2. |
| `--lang <code>` | Debug builds only: override the saved language for this run. `qps` / `qps-rtl` are the pseudo-locales; `SettingsSmokeProbe -- --lang <code>` runs the probe in a language. See [LOCALIZATION.md](LOCALIZATION.md) §3. |
| env `RADIATA_DEV_FEED=<url>` | Debug builds only (an environment variable, not a switch): the update checker fetches this feed instead of `getradiata.app/update/latest.json`. Accepts https, or plain http to `127.0.0.1` so the prompt → download → verify → silent-install flow can run against a local server. ⚠ The feed supplies its own sha256, so "Update Now" installs whatever the feed names; a developer who runs the Debug build daily should never leave this set in the user environment. Release ignores it entirely. |
| `--export-help-html [dir] [--css-version=N]` | Maintainer process: regenerate the getradiata.app/help site — all languages, illustrations included (defaults to `site-help\`). The maintainer exports into `packaging\webhost\help` (the development repository's snapshot of the live site, not in the public source, so the `?v=` sniff below reads the site's real root page) and uploads it as described in `packaging\webhost\DEPLOY.md` (also development repository only). The `styles.css?v=` cache-buster comes from `--css-version`, else from the site's root `index.html` when `dir` sits one level under it, else `HelpHtmlExport.SiteStylesVersion`. The site contract the pages must honour is in [LOCALIZATION.md](LOCALIZATION.md) ▸ *Web Help site contract*. |
| `--oobe` | Force the first-run wizard. |
| `--settings` | Open Settings once startup settles. The app's own restart after a Restore passes it, so it returns with Settings open instead of a bare tray icon. First-run setup wins: while the wizard is pending, only the wizard opens. |
| `--updated` | Passed by the installer's silent-update relaunch [Run] entry only: marks this launch as the post-update process, which quiets the Steam sentry **card** for a 90 s window (the update itself manufactures the leak condition; cures and tray text are unaffected). Never launch by hand except to test that window. |
| `--uninstall` / `--uninstall-run [--drivers] [--appdata]` | The uninstall flow above. |
| `--uninstall-cleanup [--silent]` / `--uninstall-cleanup-run [--drivers] [--appdata]` | The installer's uninstall hook (Inno runs it from `[Code]` and waits). It removes the Windows footprint, never touches application files, and a failure aborts before Inno's file pass. `--uninstall-cleanup` shows the opt-in prompt for the shared drivers and settings; with `--silent` it asks nothing and removes neither. `--uninstall-cleanup-run` skips the prompt and takes the opt-ins from `--drivers` / `--appdata`. See [INSTALLER.md](INSTALLER.md) ▸ Uninstall ownership split. |
| `--scan-library [path]` | Dump what the storefront scanners see on this machine — resolved launcher exes, per-store games with launch URLs, the Playnite side, plus the scanners' trace lines. See [GAME-LIBRARY.md](GAME-LIBRARY.md). |
| `--watchdog <pid> <startFileTimeUtc>` | Internal, self-spawned: the ride-along hard-kill un-cloak watchdog (see [INPUT-CAPTURE.md](INPUT-CAPTURE.md)). Never launch by hand. |
| `--export-picker`, `--hdr-probe` | Dev/diagnostic helpers. |
| `--export-mypicks [--only="A;B"]` | Dev only: bake the maintainer's own Game Grid Select/Start picks, for games outside the Picker's Top-100, into `CuratedArtOwner.g.cs` so they ship as defaults. `--only` limits it to the named games (semicolon-separated, case and punctuation ignored); without it every saved pick is baked, and shipped entries with no current pick are dropped and named in the output. Offline: it reads the on-disk candidate-URL caches and never calls SteamGridDB. |
| `<path> ...` (no switch) | Package folders or `.zip` files handed to the launch (a drop onto the exe or a shortcut). They install after startup through the consent gate, and a second launch hands them to the running instance. Withheld from the public build, where nothing is offered ([PACKAGES.md](PACKAGES.md)). |
| `Radiata.ArcadeHost.exe --xinput-count` | The helper exe's switch, not `Radiata.exe`'s: counts the XInput pads that process can see, for the Bluetooth Xbox isolation check. Exit code is `10` + the count. Any other first argument is a jailed script session's pipe name. Never launch by hand. |

## Distribution

**GPLv3** (deters closed-competitor code-lifting; the moat is polish + community) + a **tip jar** (GitHub
Sponsors / Ko-fi). That is the whole monetization plan — the earlier paid-hosted-tier idea is dropped.

**Not the Microsoft Store:** MSIX can't ship or install kernel drivers (ViGEm/HidHide). Distribute via
**GitHub Releases** (optionally itch.io).

**Signing: Azure Artifact Signing** ($9.99/mo, individual identity validation, publisher = the maintainer's legal name).
SignPath Foundation was rejected: it admits executables only with an established, verifiable reputation.
Setup steps: the header comment of `.github/workflows/release.yml` (the maintainer's longer walkthrough,
`packaging/artifact-signing-setup.md`, is development repository only, not in the public source). The
workflow signs twice, each file verified: the inner `Radiata.exe` and `ArcadeHost\Radiata.ArcadeHost.exe`
in the staging folder before the Inno compile, then the setup exe ([INSTALLER.md](INSTALLER.md) ▸ Signing).
**This is the last 1.0 gate.**

Windows on ARM is **not** supported. Be precise about why, because "both drivers are x64-only" is wrong:
the bundled **ViGEmBus 1.22 installer is `x64_x86_arm64`** and does cover ARM64. The
actual blockers are the **HidHide installer (`HidHide_1.5.230_x64.exe`, x64-only)** and our own
**`-r win-x64` self-contained publish**. Without a cloak there is no isolation, so ARM64 would need an ARM64
HidHide build plus a `win-arm64` publish target — not just a re-zip.

## Repo docs that are GENERATED — never hand-edit

- **The Markdown export of the in-app Help** (`CONTROLS.md`, kept in the development repository only; the in-app
  Help is also published at getradiata.app/help) ← `Core/HelpContent.cs`, via `--export-controls`.
- **`packaging/webhost/help/*.html`** (development repository only, not in the public source) ← the same
  source plus the `HelpText*` maps, via `--export-help-html`. Re-export **after** a localization pass,
  never before.
- **`Core/GameChatButtons.cs`** ← `docs/PC_GAME_TEXT_CHAT_BUTTONS.md`.
- `THIRD-PARTY-LICENSES.md` is an **embedded resource** (the About tab's licenses viewer reads it), so it must
  stay at the repo root.
