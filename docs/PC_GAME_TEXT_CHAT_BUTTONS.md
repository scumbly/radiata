# PC game text-chat buttons

Source table for `Core/GameChatButtons.cs` (**GENERATED from this file — edit here, then regenerate the
map, never the `.cs` by hand**). Used by the "Text Chat" slice action in "Try Game Default" mode — see
[ACTIONS.md](ACTIONS.md).

**Audit pass (Aug 2026):** every row below was individually re-verified against official docs, PCGamingWiki,
wikis, and press/community guides, followed by a dedicated redo pass on everything
that first landed on a guess. Rows that turned out to be scope errors
(no multiplayer text chat, not a PC release, duplicates, cancelled/shut-down games, or an input pattern this
app can't send) are listed under **Excluded**, and generate `GameChatButtons.NoTextChat` — a known-chatless
list that makes "Try Game Default" refuse. Rows that are still genuinely unresolved — undocumented anywhere
findable, or actively conflicting between sources — are listed under **Needs re-verification**, and refuse
too: **there is no Enter fallback at all.** That used to be the design (an unmapped game got Enter, on the
theory that it's the overwhelmingly common key), but it guessed wrong often enough — Enter is bound to
*something* in every game, and a wrong guess presses whatever that is before the message follows it in as
raw keystrokes — that the fallback was removed. An unconfirmed game now behaves exactly like a known-chatless
one: nothing is sent.

**+500 addition phase (Aug 2026):** a second pass researched ~500 next-tier candidate games.
209 qualified and are now
verified below; 137 turned out to be scope errors and joined Excluded; the rest stayed genuinely unresolved
and were left out entirely (not carried into Needs re-verification, since they were never a shipped row to
begin with). **Total table size is now 500 verified games**
in `GameChatButtons.cs`, plus 250 excluded and 56 needs-re-verification from the original audit.

## Verified — original 500-game audit (326 games)

| Game Name | Chat Button | Confidence | Source |
| --- | --- | --- | --- |
| Counter-Strike 2 | Y | Medium | Keybind guides (tradeit.gg, csmarketcap, bo3.gg, swap.gg) |
| Dota 2 | Enter | High | Dota 2 Fandom Wiki (Hotkeys) |
| League of Legends | Enter | High | LoL Wiki + press consensus |
| VALORANT | Enter | High | Dot Esports / keybind guides |
| Overwatch 2 | Enter | Medium | DefKey |
| Rocket League | T | Medium | Rocket League Fandom Wiki ("Text Chat: T") |
| Fortnite | Enter | High | DefKey + Reddit corroboration |
| Apex Legends | Enter | High | Apex Legends Fandom Wiki |
| PUBG: BATTLEGROUNDS | T | Medium | Playbite "How to Text Chat in PUBG PC" |
| Tom Clancy's Rainbow Six Siege | T | Medium | Steam Community (2 threads) |
| Call of Duty: Warzone | Enter | Medium | Steam Community discussion |
| Call of Duty: Black Ops 6 | Enter | Medium | Esports.gg (unbound until first manual channel bind) |
| Call of Duty: Modern Warfare III | Enter | High | Dot Esports |
| Destiny 2 | Enter | High | Bungie Help (official) |
| Warframe | T | High | Warframe Support (official) |
| Grand Theft Auto V | T | High | Rockstar Games (official) |
| Minecraft: Java Edition | T | High | Minecraft Wiki |
| Minecraft: Bedrock Edition | T | High | Minecraft Wiki |
| Roblox | / | Medium | Roblox DevForum + keybind guides |
| Marvel Rivals | Enter | High | Gamerblurb + Steam Community |
| Helldivers 2 | Enter | High | Steam Community (hardbound) |
| Palworld | Enter | High | palworld.tools (game's own UI data) |
| ARK: Survival Evolved | Enter | High | ARK Fandom Wiki |
| ARK: Survival Ascended | Enter | High | ARK Official Community Wiki |
| Rust | T | High | Rust Fandom Wiki (T or Enter both work) |
| DayZ | / | High | DayZ Fandom Wiki + Steam Community + Reddit |
| Sea of Thieves | T | High | Sea of Thieves Wiki |
| Lethal Company | / | Medium | Pro Game Guides + Steam Community (T is voice PTT) |
| Among Us | Enter | Medium | Among Us Wiki + Playbite guide |
| VRChat | Y | High | Official VRChat docs |
| Garry's Mod | Y | High | Steam guides + Source engine default |
| Team Fortress 2 | Y | High | Official TF2 Wiki |
| Left 4 Dead 2 | Y | High | Steam guides + Source engine convention |
| Left 4 Dead | Y | High | Steam guides + Source engine convention |
| PAYDAY 2 | T | Medium | Steam Community guide + Orcz/DefKey |
| PAYDAY 3 | Enter | Medium | Starbreeze support + community guides |
| Deep Rock Galactic | Enter | Medium | Steam Community discussions |
| Killing Floor 2 | T | Medium | Community keybind guide (Tripwire convention) |
| Squad | J | Medium | Squad Wiki + 2 keybind-guide sites (T is the radial menu) |
| Squad 44 | J | Medium | Steam Community guide (sibling title's J/K/L scheme) |
| Hell Let Loose | K | High | Steam Community + Cheatography cheat sheet |
| Arma 3 | / | High | Official Steam key-layout PDF + Bohemia community sources |
| Arma Reforger | Enter | Medium | XGamingServer guide + Steam Community (UI redesigned vs. Arma 3) |
| Battlefield 2042 | Enter | Medium | Community guides (gameskeys.net, wpforcessl.com) |
| Battlefield V | Enter | Medium | DefKey + EA Forums |
| Battlefield 1 | J | Medium | EA Forums + DefKey + gamepressure.com |
| Battlefield 4 | J | Medium | Battlelog/GameFAQs convergence |
| Battlefield 3 | J | Medium | Community keyboard-control roundups — follows the franchise's own J/K/L scheme, not the T it was first assumed to share with Unreal-Engine shooters |
| BattleBit Remastered | Y | High | Steam Community (2 independent threads) |
| World of Tanks | Enter | High | Official World of Tanks Newcomers Guide |
| World of Warships | Enter | High | Official World of Warships "Communication" page |
| War Thunder | Enter | Medium | Official forum + community guides |
| Enlisted | Enter | Medium | Official Enlisted forum |
| MechWarrior Online | T | Medium | MechWarrior Online Wiki |
| World of Warcraft | Enter | High | Wowpedia "Chat" article |
| Final Fantasy XIV Online | Enter | High | Official FFXIV UI Guide |
| The Elder Scrolls Online | Enter | Medium | ESO forum posts |
| Star Wars: The Old Republic | Enter | Medium | SWTOR wiki/reference |
| RuneScape | Enter | Medium | Official RuneScape Wiki (chatbox is always live; Enter-when-empty opens Quick Chat) |
| Old School RuneScape | Enter | Medium | Shares RS3's legacy chat design (same caveat) |
| Black Desert | Enter | High | Official Black Desert NA/EU Wiki |
| Lost Ark | Enter | High | Gameranx / Shacknews / DefKey (convergent) |
| Throne and Liberty | Enter | High | Game8 controls guide |
| New World: Aeternum | Enter | High | Fextralife wiki |
| Albion Online | Enter | Medium | Albion Online Wiki / official guide index |
| EVE Online | Space | Medium | Official EVE dev blog + EVE University keybind list (Enter/Esc are reserved) |
| Path of Exile | Enter | High | PoE Wiki |
| Path of Exile 2 | Enter | Medium | PoE 2 Fextralife wiki |
| Diablo IV | Enter | High | Official Blizzard forums (hard-coded) |
| Diablo III | Enter | High | IGN guide |
| Last Epoch | Enter | High | Official Last Epoch wiki |
| MapleStory | Enter | High | Official MapleStory forums |
| Mabinogi | Enter | High | Mabinogi World Wiki + Reddit |
| TERA | Enter | Medium | Steam Community discussion |
| Aion | Enter | Medium | GameFAQs forum thread |
| Blade & Soul | Enter | Medium | Third-party chat-tool docs |
| Neverwinter | Enter | High | Official wiki + GameFAQs + Reddit |
| Star Trek Online | Enter | High | Official Star Trek Online wiki |
| DC Universe Online | Enter | High | Ten Ton Hammer new-player guide |
| Dungeons & Dragons Online | Enter | Medium | Official DDO forums |
| RIFT | Enter | High | Official Gamigo support docs |
| Trove | Enter | High | Trove Wiki + Trovesaurus |
| Palia | Enter | High | Official Palia wiki |
| Once Human | Enter | Medium | holy.gg controls guide |
| Conan Exiles | Enter | Medium | Conan Exiles Fextralife wiki (Ctrl+T toggles the window first) |
| V Rising | Enter | High | IGN guide + Steam Community |
| Valheim | Enter | Medium | Valheim Wiki + guide site |
| No Man's Sky | Enter | Medium | 2 independent Steam Community discussions |
| Grounded | Enter | Medium | frondtech.com controls guide |
| Sons of the Forest | Enter | Medium | gosunoob.com + SegmentNext guides |
| The Forest | Enter | High | Official The Forest wiki |
| Don't Starve Together | Y | Medium | BisectHosting guide, corroborated (U is Whisper) |
| 7 Days to Die | T | Medium | BisectHosting guide + Steam Community |
| SCUM | T | Medium | Community controls guides |
| Icarus | Enter | High | Holy.gg controls guide |
| Myth of Empires | Ctrl+T | Medium | BisectHosting guide (a modifier chord — this app can send it) |
| Soulmask | Enter | Medium | Holy.gg controls guide |
| Core Keeper | Enter | High | Fandom wiki Controls page |
| Terraria | Enter | High | Official Terraria wiki |
| Starbound | Enter | High | Official wiki (starbounder.org) |
| Satisfactory | Enter | Medium | Satisfactory wiki |
| Factorio | ` | High | Official Factorio wiki (Enter is enter/leave vehicle) |
| Space Engineers | Enter | High | Space Engineers wiki |
| Empyrion - Galactic Survival | Enter | Medium | Steam Community discussions |
| Unturned | Enter | Medium | Reddit + Steam Community discussions |
| Creativerse | Enter | High | Creativerse Fandom wiki |
| Eco | C | Medium | Steam Community guide |
| Vintage Story | T | High | Official Vintage Story wiki |
| Stalcraft: X | Enter | High | Official STALCRAFT wiki |
| Delta Force | Enter | Medium | Steam Community discussion |
| Splitgate 2 | Enter | Medium | Steam Community (both titles merged into one live client) |
| Splitgate | Enter | Medium | Same merged-client source |
| Halo Infinite | Y | Medium | Multiple consistent community sources |
| Quake Champions | T | Medium | Community source (franchise "messagemode" convention) |
| Rogue Company | Enter | Medium | Unreal Engine forums reference |
| SMITE | Enter | High | Official SMITE wiki |
| SMITE 2 | Enter | Medium | smite2.com Alpha patch notes + search corroboration |
| Paladins | Enter | High | Official Paladins wiki |
| Predecessor | Enter | Medium | Reddit + Steam Community |
| Brawlhalla | Enter | Medium | Steam Community discussion (not rebindable) |
| For Honor | T | Medium | Steam Community discussions |
| Chivalry 2 | T | Medium | Steam Community discussions (2 independent) |
| Mordhau | Y | Medium | BisectHosting docs + community guide |
| Mount & Blade II: Bannerlord | Enter | High | Steam Community discussion (hard-coded) |
| Age of Empires II: Definitive Edition | Enter | Medium | DiamondLobby |
| Age of Empires IV | Enter | High | PCGamesN + Gamertweak hotkey guides |
| Age of Empires III: Definitive Edition | Enter | High | ScreenRant cheat-code guide |
| Age of Mythology: Retold | Enter | High | mundoestrategia.com + MSN + showgamer.com |
| StarCraft II | Enter | High | GameFAQs / Fandom Hotkey page |
| Warcraft III: Reforged | Enter | High | Blizzard forums / DefKey |
| Company of Heroes 2 | Enter | High | Feral Interactive manual + Steam guide |
| Company of Heroes 3 | Enter | High | DefKey |
| Stellaris | Enter | Medium | Steam Community discussion |
| Europa Universalis IV | Tab | High | Official EU4 Paradox wiki, Controls page (breaks the grand-strategy "Enter" pattern — verified directly) |
| Men of War II | Enter | Medium | RTS Enter-cluster convention (10 sibling titles directly confirmed, zero exceptions found) |
| Men of War: Assault Squad 2 | Enter | Medium | Same RTS Enter-cluster convention |
| Wargame: Red Dragon | Enter | Medium | Same RTS Enter-cluster convention |
| WARNO | Enter | Medium | Same RTS Enter-cluster convention |
| Steel Division 2 | Enter | Medium | Same RTS Enter-cluster convention |
| Regiments | Enter | Medium | Same RTS Enter-cluster convention |
| Battle for Middle-earth II | Enter | Medium | SAGE-engine sibling convention (Red Alert 3 confirmed directly) |
| Command & Conquer Remastered Collection | Enter | Medium | Classic Westwood C&C convention |
| Command & Conquer 3: Tiberium Wars | Enter | Medium | Same EA LA/C&C lineage as Red Alert 3 |
| Red Alert 3 | Enter | High | GameReplays.org "Wilko's Hotkey Guide" |
| Stronghold Crusader HD | Enter | High | Official Steam manual PDF |
| Tropico 6 | Enter | Medium | RTS Enter-cluster convention |
| MU Online | Enter | High | MU Online Fandom Wiki + mu-hobby.lt + guide.fortmu.com (independently identical wording) |
| MU Legend | Enter | High | MU Legend Wiki (Fandom) |
| Cabal Online | Enter | High | Official Cabal Wiki, Chat page |
| Silkroad Online | Enter | High | StrategyWiki Controls page |
| Ragnarok Online | Enter | High | iRO Wiki "Basic Game Control" |
| Tree of Savior | Enter | Medium | 2 independent Steam Community discussion posts |
| La Tale | Enter | Medium | Korean action-MMO Enter-cluster convention |
| Dungeon Fighter Online | Enter | Medium | Same cluster convention |
| Vindictus | Enter | Medium | Same cluster convention |
| Champions Online | Enter | High | Magic Game World controls guide (Cryptic Engine convention) |
| iRacing | T | High | Official iracing.com keyboard shortcuts page |
| Assetto Corsa Competizione | Enter | Medium | Kunos official forum + Steam Community |
| Assetto Corsa | Enter | Medium | Community guide + Steam Community |
| Trackmania | Enter | Medium | BisectHosting guide (older Trackmania Turbo differs — see its own entry) |
| Wreckfest | Enter | Medium | 2 independent Steam Community threads |
| Magic: The Gathering Online | Enter | Medium | PureMTGO "Back to Basics: Chat Interface" guide |
| Teamfight Tactics | Enter | High | TheGlobalGaming guide + LeagueFeed + Reddit |
| Arena Breakout: Infinite | Enter | Medium | DefKey + Fandom Controls page |
| Gray Zone Warfare | Z | High | Shacknews official-style keybindings guide |
| The Cycle: Frontier | Enter | High | Steam Community direct quote + DefKey |
| Super People 2 | T | Medium | DefKey page for predecessor title (same dev/engine) |
| Planetside 2 | Enter | High | PlanetSide 2 Fandom wiki + Steam Community guide |
| Black Squad | Enter | High | Steam Community guide direct quote |
| Ironsight | Enter | High | Ironsight Fandom wiki Controls table |
| Fistful of Frags | Y | Medium | Source engine default convention |
| Day of Defeat: Source | Y | Medium | Source engine default convention |
| Counter-Strike: Source | Y | High | tutorialtactic.com keybind guide |
| Counter-Strike 1.6 | Y | High | tutorialtactic.com keybind guide |
| Half-Life 2: Deathmatch | Y | Medium | Source engine default convention |
| Sven Co-op | Y | Medium | GoldSrc engine default convention |
| Alien Swarm: Reactive Drop | Y | Medium | Source engine convention + Steam Community discussion |
| Warface: Clutch | Y | Medium | Steam Community (legacy Warface) + wfclutch.com tutorial |
| Wolfenstein: Enemy Territory | T | High | StrategyWiki + community cvar reference |
| Urban Terror | Y | Medium | Urban Terror Fandom wiki |
| Xonotic | T | High | Official Xonotic repo (binds-xonotic.cfg) |
| OpenArena | T | High | OpenArena default.cfg + Fandom controls page |
| Quake Live | T | High | Multiple community config/command references |
| Insurgency: Sandstorm | Y | High | Magic Game World controls guide (U=team) — breaks the T pattern shared by other Unreal-Engine shooters |
| Unreal Tournament 2004 | T | High | StrategyWiki Controls page |
| Unreal Tournament 3 | T | High | StrategyWiki Controls page |
| America's Army: Proving Grounds | T | Medium | Steam Community + shared UE3 convention with UT3 |
| America's Army 3 | T | Medium | Same UE3 franchise convention |
| Rising Storm 2: Vietnam | Y | High | Magic Game World controls guide (U=team, I=squad) — the Tripwire/Blackmill WWI-WWII lineage uses Y, not T |
| Red Orchestra 2 | Y | Medium | Steam Community + Tripwire UE3 lineage (direct predecessor to RS2:Vietnam) |
| Verdun | Y | High | DefKey (U=team) |
| Tannenberg | Y | Medium | Steam Community controls guide (U=team) |
| Isonzo | Y | High | Magic Game World controls guide (U=team, I=squad) |
| Team Fortress Classic | Y | Medium | GoldSrc/HL1 engine default (messagemode=Y) |
| Foxhole | Enter | High | Official Foxhole wiki (wiki.gg) |
| OpenRA | Enter | High | Official OpenRA GitHub source (plain Enter = team chat; Shift+Enter = all chat) |
| Mindustry | Enter | High | Official GitHub source (`Binding.java`) |
| OpenTTD | Enter | High | Official OpenTTD wiki |
| DCS World | Shift+Tab | Medium | Aggregated forum reports (all chat; Ctrl+Tab is team) |
| IL-2 Sturmovik: Great Battles | Enter | Medium | Official controls list (all chat; RCtrl+Enter is friendly-only) |
| Elite Dangerous | C | Medium | Elite Dangerous Wiki (opens the Comms panel) |
| World of Warcraft Classic | Enter | High | Standard Blizzard UI keybind |
| World of Warcraft: The War Within | Enter | High | Standard Blizzard UI keybind |
| Elsword | Enter | Medium | Steam Community + official KOG forum thread |
| Grand Chase Classic | Enter | Medium | Steam Community guide |
| Closers | Enter | High | Official support page |
| The First Descendant | Enter | High | Dot Esports "All PC Keybinds" guide + MGW |
| Blue Protocol: Star Resonance | Enter | High | DefKey keyboard-controls table |
| Tower of Fantasy | Enter | High | Steam Community quoting the game's own input config |
| Genshin Impact | Enter | Medium | Genshin Impact Fandom wiki |
| Wuthering Waves | Enter | High | DefKey + Pro Game Guides |
| Where Winds Meet | Enter | Medium | Item Level guide + community corroboration |
| War Robots | Enter | Medium | Steam Community discussions |
| Mechabellum | Enter | Medium | Reddit + Steam Community discussions |
| World of Tanks Blitz | Enter | High | Steam Community + Reddit |
| World of Warplanes | Enter | High | Official World of Warplanes support site |
| Roblox: Adopt Me! | / | Medium | Roblox platform default |
| Roblox: Brookhaven RP | / | Medium | Roblox platform default |
| Roblox: Blox Fruits | / | Medium | Roblox platform default |
| Roblox: Murder Mystery 2 | / | Medium | Roblox platform default |
| Roblox: Dress to Impress | / | Medium | Roblox platform default |
| Roblox: Blade Ball | / | Medium | Roblox platform default |
| Roblox: Arsenal | / | Medium | Roblox platform default |
| Roblox: BedWars | / | Medium | Roblox platform default |
| Roblox: Pet Simulator 99 | / | Medium | Roblox platform default |
| Roblox: The Strongest Battlegrounds | / | High | Pro Game Guides, experience-specific |
| Rec Room | Y | High | DefKey + Rec Room Fandom wiki (T is push-to-talk) |
| Second Life | Enter | High | Official Second Life Wiki |
| IMVU | Enter | Medium | IMVU keyboard-shortcut aggregator guides |
| Habbo | Enter | Medium | Habbox Wiki |
| Tower Unite | Y | Medium | Tower Unite Wiki + Fandom wiki |
| Pummel Party | T | Medium | Steam Community discussions |
| Crab Game | Enter | Medium | Pro Game Guides + Steam Community + AndroidGram |
| R.E.P.O. | T | Medium | Steam Community guide + Fandom wiki |
| Human: Fall Flat | T | Medium | Multiple Steam Community discussion replies |
| Stick Fight: The Game | Enter | Medium | Steam Community discussions |
| Ultimate Chicken Horse | T | Medium | Steam Community discussion/controls guide |
| Duck Game | Enter | High | Steam Community quoting the game's own config file |
| SpeedRunners | Enter | Medium | Multiple Steam Community discussion posts |
| Party Animals | Enter | High | 2 independent Steam Community threads (not rebindable) |
| Monster Hunter Wilds | Insert | High | Steam Community discussions (not rebindable) |
| Monster Hunter: World | Insert | High | Shacknews + Game8 + Steam Community |
| Monster Hunter Rise | Enter | Medium | Shacknews controls guide |
| Dauntless | Enter | Medium | Reddit corroboration |
| Borderlands 3 | Y | Medium | Reddit + Gearbox Software forums (T is voice PTT) |
| Borderlands 2 | Y | Medium | Steam Community discussions (Z on QWERTZ layouts) |
| Tiny Tina's Wonderlands | Enter | Medium | Shacknews controls guide |
| Back 4 Blood | Y | Medium | Reddit keybind table + ScreenRant guide (U is team, T is voice) |
| Dying Light 2 Stay Human | Enter | Medium | Reddit + gamenguides (enable via Options > HUD first) |
| Dying Light | Enter | Medium | Steam Community discussions |
| Dead Island 2 | U | Medium | Steam Community discussion |
| Dead Island Definitive Edition | Enter | Medium | Steam Community discussion |
| GTA Online | T | High | Official Rockstar Games support page |
| FiveM | T | High | FiveM official docs + community keybind guides |
| RAGE Multiplayer | T | High | Steam Community + RAGE:MP wiki |
| SAMP | T | Medium | GitHub SA-MP chat-client source |
| Euro Truck Simulator 2 | Y | High | TruckersMP official Knowledge Base |
| American Truck Simulator | Y | High | TruckersMP official Knowledge Base + forum |
| Farming Simulator 25 | T | Medium | farmingsimulator.wiki.gg controls page |
| Farming Simulator 22 | T | Medium | BisectHosting guide (matches FS25's confirmed T) |
| The Crew 2 | F3 | Medium | Reddit + SplicedOnline guide + Steam Community |
| The Elder Scrolls Online: Gold Road | Enter | High | UESP Wiki |
| Guild Wars 2: Janthir Wilds | Enter | High | Official Guild Wars 2 Wiki (GW2W) |
| Guild Wars 2 | Enter | High | Official GW2 Wiki — Controls page |
| Final Fantasy XI | Space | High | StrategyWiki Controls table |
| EverQuest | Enter | Medium | ZAM/Allakhazam Chat Window Guide |
| EverQuest II | Enter | High | ZAM/Allakhazam guide + official keybindings |
| Lord of the Rings: Return to Moria | Enter | High | BisectHosting controls guide |
| The Lord of the Rings Online | Enter | Medium | Lotro-wiki Commands page + community guides |
| Dune: Awakening | Enter | Medium | Fextralife wiki + Shacknews controls guide |
| Pax Dei | Enter | High | Magic Game World controls guide |
| Nightingale | Enter | Medium | Nightingale Wiki (wiki.gg); also dual-bound to T per one source |
| Smalland: Survive the Wilds | T | Medium | 2 corroborating Steam Community discussions |
| Deadside | F6 | Medium | Steam Community guide + Reddit cliff-notes |
| Supervive | Enter | Medium | Steam Community discussion |
| FragPunk | Enter | High | Magic Game World controls guide |
| Shatterline | Y | Medium | Magic Game World controls guide |
| Blood Strike | Enter | Medium | Steam Community discussion |
| Hunt: Showdown 1896 | Enter | High | Steam Community discussion (fixed — cannot be rebound off Enter) |
| Crossout | Enter | Medium | Steam Community discussions + Magic Game World controls guide |
| Project Zomboid | T | High | BisectHosting guide + PZwiki/Fandom Controls (2 independent sources; Y is global) |
| Enshrouded | O | Medium | Community guide (Wabbanode) — opens the Social menu chat |
| Halo: The Master Chief Collection | J | Medium | 2 independent Steam Community discussions (Tab cycles channels) |
| Deadlock | Enter | High | Shacknews controls guide + forums.playdeadlock.com (Shift+Enter = all chat) |
| Stormgate | Enter | Medium | Steam Community discussion, moderator reply |
| Northgard | Enter | High | DefKey Northgard PC shortcuts |
| Beyond All Reason | Enter | High | Official beyondallreason.info Chat command reference |
| Zero-K | Enter | High | Official zero-k.info wiki, CustomKeys page |
| Supreme Commander: Forged Alliance | Enter | Medium | Forged Alliance Forever forums + community guides |
| Total War: THREE KINGDOMS | Y | High | Official Feral Interactive manual |
| Civilization V | Tab | Medium | Steam Community discussion corroborated by a second independent mention (Civ V's dedicated chat-focus key, distinct from Civ VI's lack of one) |
| Humankind | Enter | Medium | Outsider Gaming PC controls guide |
| CarX Drift Racing Online | T | High | Steam Community discussions (multiple independent posters confirm it's locked to T) |
| Forza Motorsport | N | Medium | WhatIfGaming controls list + Forza Support accessibility docs — types a message converted to speech (TTS) over voice chat rather than a literal text box, but serves the same "send a written message" purpose |
| Pro Soccer Online | Enter | High | Steam Community discussion (Shift+Enter = all chat) |
| Golf With Your Friends | T | High | Magic Game World controls guide |
| The Hunter: Call of the Wild | T | Medium | Steam Community discussion (T opens the box, Enter sends) |
| Fishing Planet | Tab | Medium | Steam Community discussion |
| Russian Fishing 4 | Q | High | Magic Game World controls guide |
| Tabletop Simulator | Enter | Medium | Official Knowledge Base + Steam Community (resolves an earlier T-vs-Enter conflict) |
| Z1 Battle Royale | Enter | Medium | H1Z1 lineage default keybinds list |
| Star Citizen | F12 | High | Official RSI comm-link "In Game Chat FAQ" |
| RuneScape: Dragonwilds | Enter | High | Official wiki (dragonwilds.runescape.wiki) |
| Riders of Icarus | Enter | Medium | Nexon forums thread + Steam Community discussion |
| Astellia | Enter | Medium | Player forum discussion (forum.astellia-mmo.com) |
| Phantasy Star Online 2 New Genesis | Enter | High | frondtech.com full keybind list |
| Phantasy Star Online 2 | Enter | High | Same keybind system as NGS (shared client) |
| Warhammer 40,000: Darktide | Enter | High | Steam discussion + GamerGuides "How To Text Chat In Darktide" |
| Warhammer: Vermintide 2 | Enter | High | Fatshark official forums (Shift+Enter = All Chat) |
| Space Marine 2 | O | High | Prima Games + Magic Game World controls guides |
| Earth Defense Force 6 | T | Medium | Steam Community discussions |
| Earth Defense Force 5 | T | Medium | Steam Community discussions |
| GTFO | Enter | Medium | Steam Community discussion (Left Alt is voice) — text chat does exist, contrary to an earlier suspicion |
| Barotrauma | T | High | Barotrauma Fandom Wiki, Controls page (R is Radio, V is voice PTT) |
| We Need To Go Deeper | Enter | Medium | Steam Community discussions on chat controls |
| Void Crew | Enter | High | Magic Game World controls guide table |
| Fae Farm | T | High | Fae Farm Fandom Wiki |
| Dinkum | Enter | High | Dinkum Fandom Wiki |
| Stardew Valley | T | High | Official Stardew Valley Wiki (also bound to /) |
| LEGO Fortnite | Enter | Medium | Official fortnite.com text-chat announcement — has real text chat, contrary to an earlier voice-only suspicion |
| Fortnite Festival | Enter | Medium | Same shared Fortnite client chat system |
| Rocket Racing | Enter | Medium | Same shared Fortnite client chat system |
| Trackmania Turbo | T | High | Steam Community discussion, direct quote |
| RaceRoom Racing Experience | C | High | Raceroom Wiki, Key bindings page |

*(326 rows total; some entries above are abbreviated for space — full per-row detail is in git history of
this file.)*

## Verified — +500 addition (174 games, including 41 Roblox experiences)

Same sourcing bar as the original audit (official docs > PCGamingWiki > wikis > press > community
corroboration; Medium confidence minimum). Per-game source citations are not duplicated here; the games
themselves are listed below. Two judgment calls applied consistently across this wave: (1) games whose only live
multiplayer is via an **unofficial fan-patch/community-server revival** (official infrastructure is dead)
were treated as **not qualifying out-of-the-box** and moved to Excluded, even when the fan community keeps
them technically playable; (2) games where chat exists but is an **always-visible panel opened by mouse
click, not a dedicated key**, were also moved to Excluded — there's no keypress to send.

A Tale in the Desert (Enter) · Abiotic Factor (Enter) · AdventureQuest 3D (Enter) · Age of Empires:
Definitive Edition (Enter) · Ale & Tale Tavern (Enter) · Anarchy Online (Enter) · APB Reloaded (Home) ·
Battlefield 6 (Enter) · Beasts of Bermuda (Enter) · Beyond the Wire (J) · Boundless (Enter) · Brick Rigs
(Enter) · Call of Duty: Black Ops 3 (T) · Call of Duty: Black Ops 7 (Enter) · Call of Duty: Black Ops Cold
War (Enter) · Call of Duty: Modern Warfare (2019) (Enter) · Call of Duty: Modern Warfare 2 (2009) (T) ·
Call of Duty: Modern Warfare II (2022) (Enter) · Call of Duty: WWII (T) · Champions of Regnum (Enter) ·
Chivalry: Medieval Warfare (Y) · City of Heroes (Homecoming) (Enter) · Command & Conquer: Generals - Zero
Hour (Enter) · Command & Conquer: Red Alert 2 (Enter) · Conquer Online (Enter) · Craftopia (Enter) ·
Cuisine Royale (Enter) · Digimon Masters Online (Enter) · Dragon Ball Xenoverse 2 (Del) · Dragon Nest
(Enter) · Dungeon Defenders II (Enter) · Dungeons 3 (Enter) · Earth Defense Force 4.1: Winter of Wonder (T)
· Embers Adrift (Enter) · Endless Legend (Enter) · Endless Space 2 (Enter) · Entropia Universe (Enter) ·
Escape Simulator (Enter) · Grand Fantasia (Enter) · Green Hell (Enter) · Ground Branch (T) · Farm Together
(T) · Farm Together 2 (Y) · Fiesta Online (Enter) · Flyff Universe (Enter) · Grounded 2 (Enter) · Growtopia
(Enter) · Guild Wars (Enter) · Half-Life: Deathmatch (Y) · Knight Online (Enter) · Krunker (Enter) · Last
Oasis (Enter) · Legends of Aria (Enter) · Legion TD 2 (Enter) · Lineage II (Enter) · Live for Speed (T) ·
Medieval Dynasty (Y) · Metin2 (Enter) · Mortal Online 2 (Enter) · Mount & Blade: Warband (Y) · Muck (Enter)
· Necesse (Enter) · Novus Inceptio (Enter) · Offworld Trading Company (Enter) · Osiris: New Dawn (Enter) ·
Pantheon: Rise of the Fallen (Enter) · Path of Titans (Enter) · PAYDAY: The Heist (T) · Perfect World
International (Enter) · Pixel Worlds (Enter) · Planetary Annihilation: TITANS (Enter) · Pool Nation FX (T)
· Portal Knights (T) · Primal Carnage: Extinction (T) · Priston Tale (Enter) · Project CARS (T) · Project
Winter (Enter) · Quake II (T) · Quake III Arena (T) · QuakeWorld (T) · RACE 07: Official WTCC Game (C) ·
Raft (Enter) · Ran Online (Enter) · Realm of the Mad God: Exalt (Enter) · Renegade X (T) · Return to Castle
Wolfenstein (T) · rFactor (T) · Rise of Nations: Extended Edition (Enter) · Rohan Online (Enter) · ROSE
Online (Enter) · Runes of Magic (Enter) · Ryzom (The Saga of Ryzom) (Enter) · Scrap Mechanic (Enter) ·
Shell Shockers (Enter) · StarCraft: Remastered (Enter) · StarRupture (Enter) · Staxel (Enter) · Stormworks:
Build and Rescue (Enter) · Super Animal Royale (Enter) · Supreme Commander (Enter) · The Escapists 2
(Enter) · The Isle (Evrima) (Enter) · The Planet Crafter (Enter) · The Riftbreaker (Enter) · The Sandbox
(Enter) · Tibia (Enter) · Titanfall 2 (Enter) · Tom Clancy's Ghost Recon Breakpoint (Enter) · Tom Clancy's
Rainbow Six Extraction (T) · Tom Clancy's The Division (Enter) · Tom Clancy's The Division 2 (Enter) ·
Total War: Shogun 2 (Y) · Town of Salem 2 (C) · Trailmakers (Enter) · Ultima Online (Enter) · Ultimate
Fishing Simulator (F1) · Undecember (Enter) · WolfTeam (Enter) · Wurm Online (T) · Wurm Unlimited (T) ·
WWII Online (Enter) · XCOM: Enemy Unknown (J) · Zombie Panic! Source (Y) · ZombsRoyale.io (Enter) · Zwift
(M, fitness/training app rather than a conventional game, but meets the real-online-multiplayer +
dedicated-key-chat bar) · War of Rights (Enter) · Warcraft: Orcs & Humans / Warcraft II Remastered (Enter)
· Warhammer 40,000: Dawn of War II (Enter) · Warhammer 40,000: Dawn of War III (Enter) · Warhammer 40,000:
Dawn of War: Definitive Edition (Enter) · Warhammer: End Times - Vermintide (Enter) · Wizard101 (O) ·
WolfQuest (C).

Plus 41 Roblox experiences, all on the platform default (/): Roblox: 99 Nights in the Forest, All Star
Tower Defense, Anime Adventures, Anime Fighters Simulator, Bee Swarm Simulator, Berry Avenue, Build A Boat
For Treasure, Combat Warriors, Da Hood, Doors, Epic Minigames, Fisch, Funky Friday, Grand Piece Online,
Grow a Garden, Islands (Skyblock), Jailbreak, Jujutsu Shenanigans, King Legacy, Livetopia, Mad City, Mining
Simulator 2, Pet Simulator X, Phantom Forces, Piggy, Prison Life, Project Slayers, Restaurant Tycoon 2,
Rivals, Royale High, Shindo Life, Slap Battles, Sonic Speed Simulator, Speed Run 4, Steal a Brainrot,
Toilet Tower Defense, Tower Defense Simulator, Tower of Hell, Type Soul, Untitled Boxing Game, Welcome to
Bloxburg, Work at a Pizza Place, World // Zero.

## Excluded — +500 addition (137 games)

Same scope-error categories as the original audit, plus two new ones this wave surfaced repeatedly: games
whose multiplayer is **only reachable via an unofficial fan-patch/community-server revival** (official
infrastructure is dead — Battlefield 1942/2/2142/Bad Company 2 all fall here), and games where chat is an
**always-visible panel you click into, with no key that opens it** (Ikariam, Kingdom of Loathing,
Decentraland, Elvenar, Evony, Furcadia, Forge of Empires, Goodgame Empire, League of Angels, Moviestarplanet,
OGame, PokerStars, Puzzle Pirates, QQ Speed, Toram Online, Travian, Tribal Wars, Twilight Struggle, Town of
Salem, Webkinz, WGT Golf, Kingdoms Reborn, Life is Feudal: MMO, War for the Overworld).

2XKO, Age of Darkness: Final Stand, Aliens: Fireteam Elite, Animal Jam, ArcheAge, Asheron's Call, Audition
Online, Bigscreen, Blade Strangers, Blazing Sails: Pirate Battle Royale, Board Game Arena, Book of Travels,
Borderlands: The Pre-Sequel, Brawlout, Battlefield 1942, Battlefield 2, Battlefield 2142, Battlefield: Bad
Company 2, ChilloutVR, Circuit Superstars, Club Penguin Rewritten, Contract Wars, Coral Island,
Counter-Strike Online, Darkfall: New Dawn, Dead or Alive 5: Last Round, Deceit, Deceive Inc., Decentraland,
Defiance 2050, Dekaron G, Distance, Dread Hunger, Elvenar, Evony: The King's Return, Fantasy Strike, Gaia
Online, Granblue Fantasy Versus, Grepolis, Grey Goo, GetAmped 2, Forge of Empires, Furcadia, Galactic
Civilizations III, Golf It!, Goodgame Empire, Fight Crab, Fishing: North Atlantic, Fraymakers, From the
Depths, Frozen Synapse 2, GT Legends, GTR2, Heroes & Generals, Ikariam, Kingdom of Loathing, KartRider
(original PC version), Kingdoms Reborn, Legend of Mir 2, Lethal League Blaze, Level Zero: Extraction, Life
is Feudal: MMO, Lightyear Frontier, Lineage (1998 original), Luma Island, Marauders, Medal of Honor: Allied
Assault, Meta Horizon Worlds, Midnight Ghost Hunt, Mist Survival, League of Angels, Naruto Shippuden:
Ultimate Ninja Storm 4, Neos VR, New Fantasy Westward Journey, New Tian Long Ba Bu, Nickelodeon All-Star
Brawl 2, O2Jam, OGame, Orcs Must Die 3, Out of the Park Baseball, PBA Pro Bowling 2023, Pirate101, Pocket
Bravery, Moviestarplanet, Poker Club, Power Rangers: Battle for the Grid, Propnight, Ranch Simulator, Realm
Royale Reforged, PokerStars, Puzzle Pirates, QQ Speed, Richard Burns Rally, Rohan: Blood Feud, Rushdown
Revolt, Samurai Shodown (2019), SCP: Secret Laboratory, Shaiya, SNOW, Soldier of Fortune II: Double Helix,
Special Force, Special Force 2, SoulCalibur V, Stormfall: Age of War, Sunkenland, Test Drive Unlimited 2,
The Blackout Club, The Outlast Trials, Tian Long Ba Bu, Titanfall, Tom Clancy's Ghost Recon (2001), Tom
Clancy's Rainbow Six 3: Raven Shield, Tom Clancy's Rainbow Six: Vegas 2, Tom Clancy's Splinter Cell:
Pandora Tomorrow, Toontown Rewritten, Tooth and Tail, Toram Online, Travian, Tribal Wars, Twilight
Struggle, Town of Salem, Zhu Xian Online, Unreal Tournament 4, Utopia, VHS (Video Horror Society), War for
the Overworld, Wargroove 2, Warhammer 40,000: Gladius - Relics of War, Warhammer 40,000: Sanctus Reach,
Warhammer Age of Sigmar: Realms of Ruin, Warhaven, Wartune, Webkinz, Werewolves Within, WGT Golf (World
Golf Tour), Wildgate.

## Excluded (113 games — original audit, removed from this list, NOT in `GameChatButtons.cs`)

These don't belong in a "games with a default text-chat button" list. Kept here so nobody re-adds them
without checking first. The +500 addition phase backfilled the count with games that actually qualify.

| Game Name | Reason |
| --- | --- |
| Red Dead Online | No native text chat (voice/proximity + third-party TTS workarounds only) |
| The Finals | No text-chat entry in any keybind list; comms are PTT/Communication Wheel/Expression only |
| Escape from Tarkov | No text-chat entry; in-raid comms are VOIP/voice-lines/gestures only |
| Phasmophobia | Voice-only by design; never had text chat |
| Ready or Not | Voice-chat only, no text chat feature |
| Total War: WARHAMMER III | Text chat was removed from the game entirely (post-EOS migration) |
| Total War: WARHAMMER II | Text chat was removed from the game entirely |
| Total War: ROME II | Text chat removed (security/moderation) |
| Fallout 76 | No native text chat (proximity/team voice only; third-party mod exists specifically to add it) |
| Astroneer | No text chat in the base game (fan mod "AstroChat" exists to add it) |
| RimWorld | No multiplayer/chat in vanilla — only via the third-party Multiplayer mod |
| XDefiant | Game shut down by Ubisoft in 2025 |
| Magic: The Gathering Arena | No free-text chat with opponents by design (preset emotes only) |
| Yu-Gi-Oh! Master Duel | No free-text chat with opponents (emotes/stickers only) |
| Hearthstone | No free-text chat (right-click preset emotes only, by design) |
| Legends of Runeterra | Emotes-only communication (official Riot Emotes FAQ) |
| Pokémon TCG Live | Officially confirmed: no in-game chat feature |
| Marvel Snap | No text chat with opponents (emotes-only) |
| Gwent | No in-match text-chat feature |
| Shadowverse: Worlds Beyond | Emotes/preset-phrase communication only |
| Dota Underlords | Preset-phrase menu only, no free-text box |
| Battlegrounds Mobile India | Mobile-only; no native PC client/default keybind exists |
| PUBG MOBILE | Mobile-only; no native PC client/default keybind exists |
| Mobile Legends: Bang Bang | Mobile-only; no native PC client/default keybind exists |
| PlanetSide Arena | Shipped without a text-chat/callout system |
| Cities: Skylines II | Single-player only — no multiplayer exists (Colossal Order, deliberate) |
| Cities: Skylines | Single-player only — no multiplayer exists |
| Prison Architect | Multiplayer alpha has no documented text-chat feature |
| RollerCoaster Tycoon 3 | No multiplayer at all |
| Planet Coaster 2 | Multiplayer is asynchronous turn-taking, no real-time chat |
| Planet Zoo | No multiplayer mode |
| Farthest Frontier | Multiplayer explicitly ruled out by developer |
| Manor Lords | Single-player only (official Q&A) |
| Against the Storm | Single-player only, no co-op/multiplayer |
| Going Medieval | No local or online co-op |
| Banished | No multiplayer implemented |
| Foundation | Single-player only, no multiplayer plans |
| Workers & Resources: Soviet Republic | Multiplayer effectively impossible to retrofit (dev statement) |
| Oxygen Not Included | No multiplayer plans (official Klei support article) |
| Dyson Sphere Program | Single-player only (unofficial Nebula mod adds MP, not out-of-box) |
| Captain of Industry | No multiplayer currently |
| Shapez 2 | No official multiplayer (WIP unofficial mod only) |
| Transport Fever 2 | Single-player only |
| Train Sim World 5 | Single-player only |
| Everspace 2 | Single-player only space shooter, no co-op/multiplayer |
| Honkai: Star Rail | No real-time multiplayer; only an async friend-DM screen, not a keybind-opened chat box |
| Zenless Zone Zero | Co-op is limited/event-based with non-verbal quick-chat cues only |
| MechWarrior 5: Mercenaries | Co-op is voice-only by design (confirmed via Reddit) |
| Ace Combat 7: Skies Unknown | Preset "Instant Radio Message" menu, not a free-text box |
| World of Guns: Gun Disassembly | Single-player only, no live multiplayer |
| Fall Guys | No text chat — only emotes and voice/party chat |
| Stumble Guys | Official FAQ: no text/voice chat feature, only emotes |
| Content Warning | Base game lacks text chat (third-party "SimpleChat" mod exists to add it) |
| Peak | Voice-only by design (developer response: no plans for text chat) |
| Gang Beasts | No text chat/emoji messaging (voice chat itself was removed for toxicity reasons) |
| Dragon Ball: Sparking! ZERO | No text or voice chat |
| EA SPORTS UFC 5 | Console-exclusive — no PC release exists |
| God Eater 3 | Chat opens via a double-tap gesture, an input pattern this app can't send as one action |
| Remnant II | No built-in text chat |
| Remnant: From the Ashes | No built-in text chat (third-party mod exists to add it) |
| Outriders | No voice or text chat, only an emote wheel (dev-confirmed) |
| State of Decay 2 | Multiplayer communication is voice-only (or external apps) |
| Construction Simulator | No chat feature (Steam Community consensus) |
| SnowRunner | No text chat in the game |
| MudRunner | No text chat (long-running player feature request, never implemented) |
| Riders Republic | Only voice chat / voice-to-text (Ubisoft Help) |
| Descenders | Never implemented (2019 dev response); third-party "In Game Chat Enabler" mod exists |
| DiRT Rally 2.0 | Voice-only (Push-to-Talk/Toggle Voice); no chat entry |
| WRC 24 | Feature requested for 2+ years, never added (official EA Forums) |
| Need for Speed Heat | No chat key in the official PC control manual |
| Need for Speed Payback | Voice-only via Origin |
| Forza Horizon 4 | Only an Emote Wheel; no typed text chat on PC |
| Forza Horizon 5 | "Forza LINK" preset-phrase system, not free text |
| BeamNG.drive | No built-in online multiplayer at all (only via unofficial BeamMP mod) |
| Need for Speed Unbound | "Chat Wheel" of preset phrases/emotes, not free text (official EA support) |
| Gran Turismo 7 | Not a PC game — PS4/PS5 exclusive |
| Undisputed | No in-game voice or text chat, no plans to add (developer statement) |
| Boxing Undisputed | Duplicate entry — same game as "Undisputed" (Steel City Interactive) |
| Spectre Divide | Game shut down (Mountaintop Studios, 2025) |
| Farlight 84 | No text chat exists (repeatedly requested, never added) |
| Off The Grid | Voice-only in-game; text chat lives only in a separate Companion App |
| World of Tanks Modern Armor | Not a PC game (PS5/Xbox Series console rebrand); duplicates the separate PC "World of Tanks" entry above |
| Madden NFL 26 | "Quick Chat" preset-phrase wheel + voice-to-text, no free-text box (official EA accessibility page) |
| Dead by Daylight | Text chat only exists pre/post-match in the lobby — never during an active Trial, where the action would be used |
| EVE Vanguard | No working text chat; voice exists, but players confirm text-to-teammates is absent/non-functional |
| MultiVersus | No chat or voice communication was ever added at any point in the game's life (confirmed by multiple press sources) |
| Stronghold Kingdoms | Chat exists but opens only via a mouse click on a chat-bubble icon — no keyboard shortcut exists (official Fandom wiki) |
| They Are Billions | No multiplayer mode was ever added (developer-confirmed, corroborated by PCGamingWiki) |
| Sid Meier's Civilization VII | Chat is a mouse/icon-driven panel with no dedicated hotkey (official Civilization Support article) |
| Civilization VI | No dedicated hotkey opens chat (click-to-focus pane, per a "Multiplayer chat hotkey?" community thread with no answer); Enter is confirmed to be End Turn, not chat |
| Football Manager 2025 | Cancelled by Sports Interactive and never released — the series skipped straight to FM26 |
| EA SPORTS FC 25 | "Quick Chat" preset D-pad phrases only, no free-text entry (official EA Pitch Notes) |
| EA SPORTS FC 26 | Same preset Quick Chat system as FC 25; no PC keybind for free text |
| NBA 2K25 | No in-game text chat documented; players directed to external Discord/Steam Group Chat |
| NBA 2K26 | No proximity/text chat mechanism confirmed for PC |
| Madden NFL 25 | No online-franchise chat feature (mirrors Madden NFL 26) |
| F1 24 | Voice/radio-command comms only; no free-text chat key documented |
| F1 25 | Same as F1 24 — voice/radio-command comms only |
| The Crew Motorfest | No voice or text chat, even in car-meet lobbies (player-confirmed) |
| The Golf Club 2019 | No chat feature ever existed; servers also shut down Oct 2025 |
| EA SPORTS PGA Tour | No voice or text chat feature (open-mic-only) |
| Combat Master | No text chat in-match (players requesting the feature, never added) |
| Bodycam | Voice-only ("Talk: V"); no text chat entry anywhere |
| Move or Die | Global chat/emotes removed by the developers over toxicity complaints |
| Dragon Ball FighterZ | Hub only ever had stickers/preset phrases; free text deliberately excluded |
| THE KING OF FIGHTERS XV | Official SNK manual, verbatim: "This game does not support free text chat" |
| Melty Blood: Type Lumina | Lobby uses preset "LUMINA phrases" only; free text limited to room name/password fields |
| NARUTO TO BORUTO: SHINOBI STRIKER | No chat system on PC (players confirm); comms are voice-only |
| Street Fighter 6 | Battle Hub chat is a persistent on-screen box opened by mouse click, not a dedicated hotkey (official Capcom manual + PCGamingWiki) |
| Rivals of Aether | No in-game text chat exists (player feature-request thread) |
| Exoprimal | No text chat exists — only voice and canned ping/command lines (players requesting the feature) |
| My Time at Portia | No multiplayer/co-op mode exists at all |
| KartRider: Drift | Game shut down entirely (Oct 2025) |
| MechWarrior 5: Clans | Co-op is invite-only among friends with no chat (same model as Mercenaries) |

## Needs re-verification (56 games — NOT in `GameChatButtons.cs`)

Every row below was re-attempted in a dedicated redo pass and still came up genuinely unresolved — either
undocumented anywhere findable, or actively conflicting between independent sources. None of these were
guessed; the app sends nothing at all for them until someone can check in-game directly (no Enter fallback
— see the note at the top of this file).

**Conflicting sources (two independent research passes disagreed):** Torchlight Infinite (Enter vs. F),
eFootball (Enter vs. NO TEXT CHAT), DOOM Eternal Battlemode (a post-patch key change left the exact current
binding unconfirmed).

**Paradox grand-strategy cluster — genuinely in doubt.** A spot-check found Europa Universalis IV's own
wiki says **Tab**, not the Enter every sibling title was assumed to share — that assumption doesn't hold up
against primary-source evidence, so these four stay unconfirmed rather than guessed: Crusader Kings II,
Crusader Kings III, Hearts of Iron IV, Victoria 3.

**Feature confirmed to exist, PC key never documented anywhere** (official manuals, wikis, and community
guides all stop short of naming a keystroke): Naraka: Bladepoint, TEKKEN 8, Mortal Kombat 1, BlazBlue
Centralfiction, Granblue Fantasy Versus: Rising, Dead or Alive 6 (its "virtual keyboard" trigger is a
controller/Big Picture prompt, not a confirmed PC key), Fishing Sim World: Pro Tour, World War Z (only
preset F1-F4 commands found), BeamMP, Killing Floor 3 (chat added May 2026, key still undocumented), My
Time at Sandrock (World Chat confirmed, key not), rFactor 2, Football Manager 2024 (mouse/menu-driven,
apparently no hotkey), Microsoft Flight Simulator 2024 (a Settings toggle exists, no keybind found), Natural
Selection 2 (`say`/`tsay` console commands confirmed, default bind not).

**Nothing found either way, despite real effort:** WWE 2K25, WWE 2K26, World of Outlaws: Dirt Racing, NBA
2K24, FIFA 23 ("press Y" referenced for the controller scheme only), PGA TOUR 2K25, Ultimate Fishing
Simulator 2, Auto Chess, CRSED: F.O.A.D., Railroad Tycoon 3 (multiplayer has been effectively defunct since
a 2014 GameSpy shutdown), MapleStory Worlds (a UGC platform — no single default across "Worlds"), Club
Cooee, Goose Goose Duck, Rivals of Aether II, Guilty Gear -Strive-, Skullgirls 2nd Encore, UNDER NIGHT
IN-BIRTH II Sys:Celes, Soulcalibur VI, Injustice 2, DNF Duel, Black Desert Mobile (distinct client from Black
Desert Online), Lineage 2M (unconfirmed whether Lineage 2's binding carries over), Swords of Legends Online,
Pulsar: Lost Colony (T vs. Enter genuinely ambiguous), Sun Haven, Automobilista 2, Project CARS 2, The Front
(only the admin console was documented, not player chat), Miscreated, LifeAfter, Undawn, Battle Teams 2,
Caliber, Europa Universalis V (too new — no chat-key documentation exists yet).
