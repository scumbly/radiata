# Radiata — developer docs

Topic docs, split so you can read only the one for the area you are working in. Start with
[ARCHITECTURE.md](ARCHITECTURE.md) for orientation, then [CODE-ATLAS.md](CODE-ATLAS.md) to find which file
owns a behaviour. The project overview is [../README.md](../README.md).

## How it is built

| Doc | Covers |
| --- | --- |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Orientation: where new logic belongs, the `Core` ↔ WPF-shell boundary, stack rationale, config locations, naming. |
| [CODE-ATLAS.md](CODE-ATLAS.md) | The flat per-file map: which file owns which behaviour. Update its row when a file is added, deleted, renamed, or repurposed. |
| [INPUT-CAPTURE.md](INPUT-CAPTURE.md) | HidHide + ViGEm, the cloak lifecycle, Xbox/XInput capture, Passthru Mode. |
| [CONTROLLERS.md](CONTROLLERS.md) | Controller profiles, raw-HID / Bluetooth parsing, trigger chords, kind detection. |
| [OVERLAY.md](OVERLAY.md) | Overlay window, z-order, display targeting, selection + dwell, hub readout, d-pad modes, in-wheel edit mode. |
| [MATERIALS.md](MATERIALS.md) | The eight wheel materials, tokens + legacy aliases, per-material render costs. |
| [SOUND.md](SOUND.md) | `SfxEngine`, gain knobs, the Kawaii xylophone melodies. |
| [GAME-LIBRARY.md](GAME-LIBRARY.md) | Storefront scanning, the Game Grid, cover art + logo resolution, storefront hiding. |
| [ACTIONS.md](ACTIONS.md) | The authored slice-action taxonomy, the hidden-but-supported contract, the removals ledger. |
| [ARCADE.md](ARCADE.md) | The Arcade action category: the round game window, drop-in script games, the isolation guard, editing the games' on-screen text. |
| [PACKAGES.md](PACKAGES.md) | Drop-in packages: custom Material themes (data-only, consent-gated), the reserved Arcade-games folder, the security posture. |
| [SETTINGS-UI.md](SETTINGS-UI.md) | The Settings window, the `ApplyTo(cfg)` pattern, XAML resource traps, chrome colours. |
| [ONBOARDING.md](ONBOARDING.md) | The first-run wizard and starter-wheel seeding. |
| [MOTION-INVENTORY.md](MOTION-INVENTORY.md) | Every animation, classified for Reduce Motion. Update it when you add motion. |
| [PC_GAME_TEXT_CHAT_BUTTONS.md](PC_GAME_TEXT_CHAT_BUTTONS.md) | The 500-row game → chat-key table. `Core/GameChatButtons.cs` is generated from it. |

## Build, release, localization

| Doc | Covers |
| --- | --- |
| [BUILD-RELEASE.md](BUILD-RELEASE.md) | The everyday build loop, versioning and the milestone ritual, build flavours, packaging, CLI switches, signing. |
| [INSTALLER.md](INSTALLER.md) | The Setup EXE: `radiata.iss` contracts, payload layout, the silent-update protocol, the uninstall ownership split, the update feed. |
| [LOCALIZATION.md](LOCALIZATION.md) | What is translated, the Help and UI translation layers, translation conventions, and the batched maintenance loop (English edits ship alone). |

[../CONFIG.md](../CONFIG.md) documents the `config.json` schema, every key and every action type with its
fields. [../CONTRIBUTING.md](../CONTRIBUTING.md) documents the contribution workflow.

The development repository also keeps process ledgers (a decision log, test sheets, a work queue) that are
not published, so a reference to one of them in the source means "the maintainers' internal record".
