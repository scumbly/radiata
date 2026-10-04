# Radiata

[![tests](https://github.com/scumbly/radiata/actions/workflows/tests.yml/badge.svg)](https://github.com/scumbly/radiata/actions/workflows/tests.yml)
[![latest release](https://img.shields.io/github/v/release/scumbly/radiata)](https://github.com/scumbly/radiata/releases)
[![license: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)](LICENSE)

<p align="center">
  <a href="https://youtu.be/U_yRv8BtqE0">
    <img src="https://img.youtube.com/vi/U_yRv8BtqE0/maxresdefault.jpg" alt="Introducing Radiata (video on YouTube)" width="800">
  </a>
  <br>
  <em>▶ Watch <a href="https://youtu.be/U_yRv8BtqE0">Introducing Radiata</a> on YouTube</em>
</p>

<p align="center">
  <img src="https://getradiata.app/assets/screenshots/radiata-main.webp" alt="A Radiata wheel open over the desktop beside the Settings window" width="800">
</p>

Radiata is a feature-rich, controller-based radial menu utility for a Windows gaming PC. Launch a game, join the Discord call, swap to headphones, start your stream, all from a controller.

> **Status:** pre-1.0. Windows 10 and 11, x64 (Intel/AMD) only. Windows on ARM isn't supported: Radiata
> ships as an x64 build, and the HidHide installer it needs to hide your controller is x64-only.

### ☕ Support Radiata ([**Tip Jar**](https://getradiata.app/tip))

Radiata is free and GPLv3, built by one person. If it earns a place on your gaming PC, a tip at
[**getradiata.app/tip**](https://getradiata.app/tip) keeps it going. There is no paid tier. Tipping is thanks, not a purchase.

## Features

- **Two radial wheels**, summoned by a fully configurable controller chord. Hold to open, tilt to aim, release to
  fire; release while centred to cancel. Up to 12 slices per wheel.
- **Config-driven slices**: launch or focus apps, send key combos, open URIs, switch audio devices and
  set or mute volume, toggle HDR and Extend/Clone display mode, sleep, reboot, lock or shut down, drive
  Xbox Game Bar and OBS Studio, type into a game's text chat, and control Discord (join or leave a
  voice channel, mute, deafen).
- **Game Grid**: a controller-scrollable launcher for every installed game across Steam, Epic, GOG, Xbox, Playnite, 
  Battle.net, Amazon, itch.io, Ubisoft and EA, with real cover art (optional SteamGridDB integration)
  and optional Playnite library support.
- **In-wheel editing**: add, move, delete and reorder slices from the couch with the controller alone.
  Edits save live.
- **Clean input isolation**: games are given a virtual gamepad in place of your controller, and that pad
  is held neutral while a wheel is open, so the game underneath never sees your menu input. No game hooks
  or injection.
- **Passthru Mode**: a global or per-game bypass for strict kernel-anticheat titles.
- **Looks and sounds**: eight built-in wheel materials and matching sound sets.
- **Accessibility**: Reduce motion, narration of the wheel and Game Grid, swap-sides and toggle
  activation, and a choice of one-stick or either-stick aiming. The whole app and its Help are available
  in English, Spanish, German, Japanese and Arabic.
- **A small built-in Arcade** of games that open in a round window where the wheel was.

## Install

1. Download the latest `Radiata-<version>-setup.exe` from
   [**Releases**](https://github.com/scumbly/radiata/releases) or [**getradiata.app**](https://getradiata.app).
   Download Radiata only from those two places.
2. Run it. Radiata installs per-user, so the app itself needs no admin rights; the only UAC prompts come
   from the driver step, so leave **Install drivers (recommended)** checked and approve them.
3. First-run setup opens automatically and walks you through the drivers and a controller check.

Release installers are code-signed (see [`SECURITY.md`](SECURITY.md) for how to verify one). Windows
SmartScreen can still show "Windows protected your PC" for a newly released or rarely downloaded file
until its publisher has built up reputation. If it does, confirm the file came from one of the two places
above, then choose **More info**, **Run anyway**. After that, updates are one button from inside the app
(**Settings ▸ Advanced ▸ Check for Updates**).

## Controllers

- **DualSense Edge, DualSense and DualShock 4** (Bluetooth or USB): full support with clean isolation.
  The Edge's Fn buttons are the reference summon.
- **Xbox pads over USB or a wireless dongle**: isolated with the same virtual pad and cloak, presented to
  the game as a virtual Xbox 360 pad.
- **Xbox pads over Bluetooth**: the wheel works, and Radiata attempts the same isolation. The cloak is
  only trusted after a live check confirms other apps really lost the pad; when that can't be confirmed,
  it falls back automatically. The worst case is fallback mode, never a dead controller. Most third-party
  Xbox-style pads present as a DualShock 4 over Bluetooth and take the full path.
- **Pads with extra paddles or buttons** (e.g. 8BitDo Ultimate 2C): over Bluetooth the
  extra L4/R4 buttons can be chosen as summon buttons in **Settings ▸ Customize ▸ Triggers**.
- **Two or more Xbox pads at once**: isolation is off by design. Radiata can't yet tell which pad it is
  standing in for, and cloaking the wrong one would make a second player's controller disappear, so it
  leaves both visible.

In fallback mode nothing breaks and nothing is hidden from you: the wheel opens, aims and fires as
normal, but the game *also* sees your stick and button input while a wheel is open. Radiata's tray
tooltip says which mode you're in.

## How input isolation works

With the optional drivers installed, Radiata presents games with a virtual gamepad (ViGEmBus) and hides
your physical controller from them (HidHide), then holds the virtual pad neutral while a wheel, the Game
Grid or an editor is on screen, so menu input never reaches the game. Nothing is hooked into or injected
into any game; without the drivers, or in Passthru Mode, games keep seeing your controller and Radiata
simply watches alongside it.

## Drivers

Radiata bundles two open-source Nefarius drivers, installed during first-run setup or later from
**Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**:
**[ViGEmBus](https://github.com/nefarius/ViGEmBus)** 1.22.0 (virtual gamepad; retired upstream, 1.22.0
is its final release) and **[HidHide](https://github.com/nefarius/HidHide)** 1.5.230 (hides the physical
pad). Without them the wheel still works; games just keep seeing your controller while a wheel is open.
Both are shared system components other controller tools may use, so uninstalling Radiata leaves them in
place unless you opt in to removing them.

## Build from source

Requires Windows and the .NET 8 SDK. Close any running `Radiata.exe` first; a running copy locks the
output DLLs.

```
# Debug build (normal multi-file output in bin\Debug\net8.0-windows):
dotnet build ControllerWheel.csproj -c Debug

# The three checks CI runs on every push and pull request:
dotnet run --project tools/TestHarness -c Debug -- all
dotnet run --project tools/SettingsSmokeProbe -c Debug
bin\Debug\net8.0-windows\Radiata.exe --check-locales locales.txt
```

`TestHarness` is the test suite. `SettingsSmokeProbe` constructs the Settings editors for real, which
catches XAML resource errors that compile cleanly and throw at runtime (exit code 0 means pass).
`--check-locales` writes a localization parity report to `locales.txt`; it should list no `MARKUP`,
`PLURAL` or `STALE` entries.

A release build is a self-contained single-file x64 publish:

```
dotnet publish ControllerWheel.csproj -c Release -r win-x64 --self-contained true -o publish/Radiata
```

`tools/build-installer.ps1` wraps that publish into `Radiata-<version>-setup.exe` (it needs Inno Setup 6),
and `.github/workflows/release.yml` runs it, and signs the result, when a `v<version>` tag is pushed.

The Arcade's licensed sound effects are not in this repository (see
[`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md)), so a build from source has a silent Arcade.

## Configuration

Radiata keeps one hot-reloaded JSON file, `%APPDATA%\Radiata\config.json`. Everything in it is editable
from Settings; [`CONFIG.md`](CONFIG.md) documents every key and every action type and its fields. Edits
made outside the app may need a restart to be picked up reliably.

## Help

Help lives inside the app (**Settings ▸ Help**, where chord names and button glyphs follow your own
configuration) and is also published, searchable and in all five languages, at
[**getradiata.app/help**](https://getradiata.app/help/). It covers installing, opening a wheel, every
action type, controller support, input isolation and troubleshooting.

## Privacy

Radiata has no analytics, no advertising, no account and no persistent identifier. It contacts the
project's server for two things only: an update check that sends just the app version, and crash reports
that are sent only with your approval. See [`PRIVACY.md`](PRIVACY.md) for the details.

## Contributing

Contributions are welcome. Open an issue before starting anything beyond a small fix, and read
[`CONTRIBUTING.md`](CONTRIBUTING.md). Contributions require agreement to the [CLA](CLA.md).

## Security

Please report vulnerabilities privately; [`SECURITY.md`](SECURITY.md) says how, what is in scope, and how
to verify that an installer is genuine.

## License

Radiata is free software under the **GNU General Public License, version 3 (GPLv3)**, with additional
terms permitted by GPLv3 §7 (attribution preservation and a trademark reservation); see
[`LICENSE`](LICENSE). Third-party components and their licenses are listed in
[`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md).

## Trademarks

**"Radiata"™ and the Radiata flower mark™ are trademarks of Jesse Tarter-Holden (né Jesse Holden)** and are not licensed by the
GPL. Forks must use a different name and mark; see [`TRADEMARK.md`](TRADEMARK.md).

Steam, Xbox, GOG Galaxy, Ubisoft Connect, Epic Games, Battle.net, itch.io, Playnite, and Discord are
trademarks of their respective owners. Radiata is an independent tool and is not affiliated with, endorsed
by, or sponsored by any of them.
