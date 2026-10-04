# Game library, Game Grid, and cover art

**Read this when touching:** `GameLibrary`, `PlayniteLibrary`, `LauncherCatalog`, `GameArt`,
`ArtPrefetcher`, `GameBrowserControl`, or storefront launching in `WindowsPlatformActions`.

## Storefronts

Ten `LauncherCatalog` entries — `steam`, `playnite`, `xbox`, `gog`, `epic`, `ubisoft`, `itch`,
`battlenet`, `ea`, `amazon` — each carrying a display string, default MDI glyph, and brand tint. Only Steam
(Big Picture URI) and Playnite (fullscreen exe) have a true big-picture mode on Win10; Xbox gets
window-maximize; the rest just open, and the display text says so.

Nine live scanners run in `GameLibrary` (Steam, Epic, GOG, Xbox, itch, Battle.net, Ubisoft, EA, Amazon), deduped by
`LaunchUrl`. **Playnite is suggested, never required** — its LiteDB `games.db` read is dead on Playnite 9+,
so direct scanning is the default and Playnite is detected and used only when present.

**`Radiata.exe --scan-library [path]` dumps what the scanners actually see on this machine** — resolved
launcher exes, `InstalledLaunchers` / `ScannableStores`, every game with its storefront + launch URL +
install dir, the Playnite side, and the scanners' own trace lines (which is where the *absences* show up: a
skipped soundtrack, a stale GOG entry, an unresolved `ms-resource:` name). A scanner is only ever as correct
as the machine it ran on, so use this rather than opening the Grid and squinting.

### Per-store notes worth not rediscovering (as of Aug 3 2026)

Each of these came from reading Playnite's equivalent plugin (MIT, credited) against ours:

- **Steam:** appid **228980** ("Steamworks Common Redistributables") is fully installed on most machines and
  passed every filter — it drew a launchable grid tile. Excluded via `SteamNonGameAppIds` (it was present
  *twice* on the reference machine, once per library folder). **Soundtracks** install under
  `steamapps\music`, not `common`; those are dropped, since they can't be launched. A game whose folder is in
  neither place still lists (the manifest says installed) with a null `InstallDir`.
- **Epic:** launch URLs carry **`&silent=true`** so the Epic launcher's own window doesn't take the screen
  and the controller focus while the game loads. `InstallLocation` is cross-checked against Epic's own
  `LauncherInstalled.dat`, which **wins** — a manifest keeps a stale path when an install is re-pointed by
  hand.
- **GOG:** the legacy `GOG.com\Games` registry key can outlive an uninstall, so entries whose folder is
  *verifiably* gone are skipped (`PathVerifiablyGone` — drive present, folder absent; an unplugged external
  never reads as uninstalled). `GOGPACK*` bundle keys are skipped. If the key yields **nothing**, the scan
  now falls through to `galaxy-2.0.db` instead of giving up.
- **Xbox:** a package whose manifest `DisplayName` is an **`ms-resource:` reference** used to be skipped
  outright — invisible in the Grid, no trace. It's now resolved via `SHLoadIndirectString`
  (`ResolveIndirectString`, ported from Playnite); only a resolve *failure* skips. No such package exists on
  the reference machine, so the resolver itself is **unverified on hardware**.
- **itch:** `caves.game_id` now JOINs `games` for the **title** (a `.itch/receipt.json` is written at install
  time and never updated — that's the "Beleth" stale-name case, which then matched the wrong SGDB art) and
  the **classification**. itch hosts assets, books, comics, soundtracks and tools alongside games; only
  `game`/`tool` are listed. Unknown/NULL classification is allowed through so a new itch category can't
  silently empty a library. ⚠ `butler.db` uses **WAL** — a copy of the main file alone is a stale snapshot.
- **Ubisoft:** Connect's `Installs` key carries no title, so games were named after the install **folder**
  ("RainbowSix"), which also went to SteamGridDB and matched nothing. The real title comes from the uninstall
  hive, matched on install path (`UninstallTitlesByLocation`). Playnite instead parses Connect's
  `configurations` protobuf-of-YAML for names *and* official art — richer, but it needs two undocumented
  layouts parsed to fix a name the registry already has; deliberately not built.
- **Amazon Games** (added Aug 3 2026): the cheapest scanner of the lot — the client keeps a plain SQLite
  list at `%LOCALAPPDATA%\Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite` (`DbSet`, one row per game:
  `Id` / `ProductTitle` / `InstallDirectory` / `Installed`). **`Id` is the same value Playnite's Amazon plugin
  stores as its GameId**, so Amazon is *id-keyed* in `MatchKey` — unlike EA. Launch is
  `amazon-games://play/<id>`. Verified on the reference machine (one Prime Gaming title).
  ⚠ **This is Amazon *Games* — the desktop client that installs Prime Gaming titles locally — not Amazon
  *Luna*, the cloud-streaming service.** Luna streams; nothing installs, so there is no local library to
  scan and no launch URI to build. It is out of scope by construction, not an omission.
  Known limitation: like Battle.net's, the `amazon-games://` handler wants the client already **running** to
  act on a play request; cold it may just open the client. No cold-launch dance was built (unverifiable and
  the Battle.net one took real hardware iteration to get right).
- **Uninstall-hive sweeps** (Ubisoft legacy titles, EA custom locations, the title lookup) go through
  `UninstallRegistry`: **HKLM *and* HKCU × both registry views**. They were HKLM-only, which cannot see a
  per-user install.
- **Third-party files are read with `FileShare.ReadWrite` + a short retry** (`ReadTextShared`,
  `ReadBlobCapped`). The default `FileShare.Read` fails against a process holding the file with write access
  — Steam does that to an `.acf` mid-update, and losing `libraryfolders.vdf` costs the **whole** Steam scan
  via `Scan`'s per-store guard, not one game.

### `ScannableStores` vs `InstalledLaunchers` — a distinction that has bitten us

- **`ScannableStores`** answers *"is this store's game LIST authoritative?"* — i.e. can an empty scan be
  trusted as "zero games". Only **Ubisoft** (registry `Launcher\InstallDir`) and **Battle.net** (the Agent's
  `product.db`) qualify. Deliberately excluded: **Steam** (libraries span offline/external drives, so an
  empty scan may be incomplete), **GOG** (deferred until verified against a live Galaxy install), and
  **Epic / Xbox / itch / EA** (name-keyed, so enforcing risks false-dropping a game whose live folder name
  differs from its Playnite title).
- **`InstalledLaunchers`** answers *"is the launcher app installed?"* — used by onboarding's storefront
  checklist and the Grid's launcher cards, so an installed-but-empty launcher still lists as present.
  **Every store is resolved through the same exe-path helper the launch path uses**, so *offered ==
  launchable* holds by construction. The reason: the Agent's `product.db` and the games outlive
  an uninstalled client, which used to offer a storefront slice that could only ever log "Battle.net.exe not
  found".

  One helper per store, each **registry-first with fixed Program Files paths as the fallback**:
  `SteamExePath`, `EpicLauncherExePath`, `GogGalaxyExePath`, `UbisoftConnectExePath`, `BattleNetExePath`,
  `EaAppExePath`. Extended from Battle.net/EA to all six on **Aug 3 2026**, because fixed-path probes were
  missing real installs: a launcher on another drive (all of them record their location in the registry), and
  Epic specifically when only the **Win32** binary is present — Playnite prefers Win32 too (their #1552) and
  the old probe looked only at Win64, so such an install read as "Epic not installed". Verified on the
  reference machine: Epic resolves via Win32, Ubisoft via `UbisoftConnect.exe` (we probed only the legacy
  `upc.exe`).

### Launching a storefront

`WindowsPlatformActions.LaunchStorefront`. Only Steam and Playnite have a real big-picture mode; the rest
just open.

**The Xbox app is the special case:** it has no Win10 big-picture mode, so Radiata launches it and then
maximizes its window. The window is found by its **owning process image** —
`NativeMethods.FindWindowByProcessExe("GamingApp")`, walking `ApplicationFrameWindow` children because the
top-level window belongs to ApplicationFrameHost — polling up to ~15 s for it to appear. The old
`FindWindow("ApplicationFrameWindow", "Xbox")` title match was **English-only** (the audited "breaks on
localized Windows" bug) and could also be spoofed by any process naming a window "Xbox". `GamingApp.exe`
never localizes. **Fixed Jul 31 2026 — don't reintroduce a title match.**

**Launched games are pushed to the foreground aggressively**: a new-window watcher runs for 45 s
with at most 3 pushes, generation-guarded, on every launch shape — a plain `AllowSetForegroundWindow` +
short poll outlives neither slow startups nor launcher→child-process chains.

### ⚠ Playnite's uninstaller leaves the whole library behind

`PlayniteLibrary.IsAvailable` was a `File.Exists` on `%APPDATA%\Playnite\library\games.db` alone. Uninstalling
Playnite **does not remove `%APPDATA%\Playnite`** — extensions, themes, cache, config and `games.db` all stay —
so that test reported a long-gone Playnite as installed indefinitely. Reported Aug 10 2026: the onboarding
Assets step still read "Playnite is installed and can provide default artwork" after an uninstall *and a
reboot*. Same wrong answer reached `LauncherCatalog.IsPresent("playnite")` (offering a storefront slice that
opens a missing exe) and Advanced's Install-Playnite button.

`IsAvailable` is now **`IsInstalled && games.db exists`**. ⚠ Both halves are load-bearing: the install probe
alone is equally wrong, because a fresh install has no library to read yet. `IsInstalled` looks for
`Playnite.DesktopApp.exe` / `Playnite.FullscreenApp.exe` under `%LOCALAPPDATA%\Playnite` first, then walks the
uninstall hives (`UninstallRegistry`) for a custom or all-users location — cached ~30 s, like
`LauncherCatalog`'s storefront probes, since a registry sweep runs on every Game Grid open and Settings render.

### ⚠ Playnite running = you are reading a STALE CACHE

`PlayniteLibrary.TryReadDb` reads `games.db` by **copying** it first, and that copy **fails while Playnite is
open** (it holds the DB exclusively). `GetInstalledGames` then silently falls back to `playnite-cache.json`,
which can be arbitrarily old. There is no error and no visible difference.

**This invalidates any Playnite-related test run with Playnite open.** It cost a full false result on Aug 3
2026: a freshly-installed EA library plugin appeared to dedupe perfectly, when in fact our reader was serving
a 3-hour-old cache that predated the plugin, so nothing was deduped at all. **Quit Playnite, then confirm the
expected storefront bucket appears in `--scan-library` *before* judging anything downstream.**

By design, not a bug — but it also means a user who adds a Playnite library plugin won't see those games in
Radiata until Playnite is closed and we get a fresh read.

### Duplicate rows within the Playnite list

Playnite can hold **two rows for one install** (a plugin re-import, or an Origin entry migrating to the EA-app
plugin — same store, same name, same `InstallDirectory`, different Playnite GUID). `GameLibrary.DedupePlaynite`
collapses them inside `Reconcile`, right after the ghost filter so the `DirectLaunchUrl` enrichment runs on the
collapsed list.

Key = normalised `InstallDir` when present, else `MatchKey`; **both storefront-scoped**. The row with cover art
wins a tie. Drops trace as `[Library] Playnite duplicate collapsed: …`.

Before this existed, both rows reached the grid *and* the enrichment gave them the same launch URL, so the
tiles were indistinguishable. Note the asymmetry it corrects: the live side was always deduped (`Scan()` does
`DistinctBy(g => g.LaunchUrl)`), while the Playnite side rested on an unstated assumption of internal
uniqueness. **`App.LoadGamesUnfiltered`'s guards (`have` / `DriftDuplicate` / `SamePathAsPlaynite`) only ever
protect the live-scan side** — don't mistake them for whole-list dedupe.

### Reconciliation (kills uninstall ghosts)

Playnite gives unified names and local cover art, but its `IsInstalled` flag only refreshes when Playnite
itself runs — so a storefront uninstall while Playnite is closed leaves a ghost. `Reconcile` keeps Playnite
as the list (covers/curation intact) and drops any entry the live scan contradicts, plus a Playnite
`InstallDir` ghost check. It also **enriches** Epic/Xbox/itch entries with the live scan's direct launch
URLs — Playnite is the curated-list source, not a launch dependency.

### Battle.net — ⚠ TWO id spaces, not interchangeable

Playnite's MIT catalog is ported into `GameLibrary.BattleNetCatalog`. As of **Aug 3 2026** it carries **both**
of Blizzard's ids per product, because collapsing them into one "code" was a real bug:

| | What it is | Where it's used |
| --- | --- | --- |
| **Uid** (internal id) | What Blizzard writes on disk | leading token of a game's `.product.db`, `--uid=` in its uninstall string, `<gameExe> -launch -uid <uid>` |
| **ProductId** | The launch alias | `battlenet://<ProductId>`, `Battle.net.exe --exec="launch <ProductId>"`, and Playnite's `GameId` |

For most titles they're the same word (`s1`, `s2`, `wow`, `w3`, `osi`…). For **seven** they are not —
Hearthstone `hs_beta`/`WTCG`, Overwatch `prometheus`/`Pro`, Diablo III `diablo3`/`D3`, Heroes
`heroes`/`Hero`, BO4 `viper`/`VIPR`, MW2CR `lazarus`/`LAZR`, MW `odin`/`ODIN`. The scanner reads the uid off
disk but emitted `battlenet://<uid>` and looked the name up in a ProductId-keyed table, so for those titles
the URL was one the client doesn't recognise **and** the name fell back to the install-folder name.
`MatchKey` also disagreed with the Playnite side, so `Reconcile` dropped the curated Playnite entry as a
ghost and re-added our folder-named copy.

Resolve through `BattleNetProductIdFor` / `BattleNetUidFor` (both accept either space) — never interpolate a
raw code. **Why this hid for so long:** the only Blizzard game on the reference machine is StarCraft, where
uid `s1` == ProductId `S1`.

Also fixed: `--exec="launch <ProductId>"` is **quoted** (`StartBattleNetExec`). `launch S1` is one argument
to `--exec`; unquoted it reached the client as two argv entries. Playnite quotes it.

**Still unverified on hardware** (no Blizzard game on the reference machine but StarCraft): whether any of the seven divergent
titles now launch. The direct-exe path (`-launch -uid`) is unchanged and still the primary when the exe name
matches the install folder.

### EA (added Aug 3 2026)

- **Client discovery is via the REGISTRY** (`WindowsPlatformActions.EaAppExePath`, shared with
  `InstalledLaunchers`) because EA's on-disk layout defeats a fixed path: the real install is a versioned
  folder (`…\EA Desktop\13.759.2.6273\EA Desktop\`) behind a sibling **directory junction** EA retargets on
  every update.
- **Games are live-scanned** (`GameLibrary.ScanEa`) — no Playnite plugin needed. EA's own index
  (`%ProgramData%\EA Desktop\<sha256>\IS`) is encrypted, so the scanner reads the per-game manifest EA drops
  **inside each install**: `<installDir>\__Installer\installerdata.xml` (UTF-16) → `<title>` per locale plus
  the `<contentID>` list. Reading it from the install folder makes presence **self-proving** — there's no
  stale central entry to dir-check.
- **Roots:** `machine.ini`'s `machine.downloadinplacedir` (default `C:\Program Files\EA Games\`) + both
  default `EA Games` folders + an uninstall-hive sweep for custom locations, deduped by dir.
- **⚠ Launch = `origin2://game/launch/?offerIds=` + ALL contentIDs, comma-separated.** This is the
  load-bearing, non-guessable detail: a single platform id, or the `Origin.SFT.*` id from `InstallData`,
  makes the EA app open and launch **nothing** (lutris/lutris#4996). Big titles carry a dozen ids and want
  them all. **Verified on hardware Aug 3 2026 at both ends:** Bejeweled 3 (1 id) → `offerIds=71715` launched
  in ~3 s, and **The Sims 4 (14 ids) launches from the Game Grid** — so the comma-joined multi-id form is
  observed behaviour now, not an inference from the Lutris issue.
- **⚠ `installerdata.xml` contains a DECOY `contentID` — don't parse it with `//contentID`.** The real list
  is the 14-odd children of the single `<contentIDs>` block, but there is also an **empty, self-closing
  `<contentID />`** further down inside a `<launcher>` element. An XPath of `//contentID` picks up both, and
  the empty one joins into the URI as a trailing comma (`…,1015224,`) — which is exactly the shape that makes
  EA launch nothing. `ReadEaGame`'s regex `<contentID>\s*([^<\s]+)\s*</contentID>` is immune by construction:
  it needs a paired open/close tag *and* a non-empty capture, so the self-closing decoy can't match. Verified
  against a real 14-id manifest (The Sims 4, Aug 3 2026) — 14 ids, no trailing comma. **If anyone ever
  "cleans this up" into an XML/XPath read, filter empties and scope to `contentIDs/contentID`.**
- **The multi-id reference case is The Sims 4** (14 contentIDs, installed on the reference machine). Both halves are
  confirmed: `ReadEaGame`'s regex yields exactly those 14 in document order with no trailing comma, and the
  resulting URI launches the game. Use it as the regression case for any change to EA parsing or launching.
- EA is **name-keyed** in `MatchKey` — our contentIDs and Playnite's `Origin.OFR.*` offer id can never
  produce a common id — so it stays **out** of `ScannableStores` but **is** in `App`'s `addSafe` set.

## The Game Grid (`game-browser` slice)

A controller-scrollable view of installed games, always the centred ~75%×80% card
(`GameGridFullScreen` retired Jul 31 2026).

| Button | Browse mode | Pick mode |
| --- | --- | --- |
| **✕** | launch | pick the game and carry it back to the editor |
| **Select** | cycle the cover art (default → top-rated SGDB covers → flat colours) | same |
| **Start** | toggle/cycle the logo overlay (default logo → SGDB alternates → off/raw) | same |
| **△** | favourite (pins to the top of every view) | — |
| **☐ (hold)** | hide this game — or, on an **Open &lt;store&gt;** card, hide the whole storefront | suppressed |
| **L1/R1** | filter by storefront | filter by storefront |

Art cycling moved off the face buttons onto Select/Start, which freed △/☐ for favourite and
hold-to-hide. The hint strip spells Select/Start out via `PadButton.Select`/`Start` pills, because neither
pad family has a recognisable symbol for them. Both art picks persist per-game in `cover-overrides.json`.
Grid order: favourites first (at base tile size), then most-recently-launched, one column tighter; the
"Open &lt;storefront&gt;" tile leads the second band.

**Adding a game to a wheel is done ONLY from the in-wheel editor's Add picker → Installed Game**, which
opens the grid in pick mode. Invoking a wheel over a standalone grid does nothing — the
"invoke to add/replace from the grid" gesture was **removed Jul 2026** for simplicity.
Don't reintroduce it without maintainer sign-off.

A one-time art tip shows on the first standalone grid open (`SystemConfig.GameGridTipSeen`).

## Storefront opt-outs live in the GRID, not Settings

The Settings ▸ Advanced ▸ Storefronts include/exclude list is **deleted**. Instead:
filter to a store with **L1/R1** and **hold ☐** on its **Open &lt;store&gt;** launcher card
(`GameBrowserControl.StorefrontHideRequested` → App writes `SystemConfig.DisabledStorefronts`). Same
dwell/spinner/SFX as hiding a game; **suppressed in pick mode** so a mid-pick write can't vanish the store
being browsed. **Reset Hidden Games** un-hides storefronts as well as games.

`DisabledStorefronts` is unchanged and still the persisted truth. Note it holds
**`InstalledGame.Storefront` names** (`"Steam"`), **not `LauncherCatalog` keys** (`"steam"`), so the hold
resolves the key back through the catalog.

**Known gap:** a library Radiata can't NAME has no card, so it can't be hidden this way — it lands with
`Storefront = ""`. EA was that gap and is now closed. **Narrowed further Aug 3 2026:** `PlayniteLibrary.Stores`
now maps every *built-in* Playnite library-plugin GUID, adding **Amazon**, **Rockstar** and **Humble** (read
from each plugin's own `Guid.Parse`, MIT). Those games get a name and a filter chip — but there's still no
`LauncherCatalog` entry for them, so no **Open &lt;store&gt;** card and no storefront-level hold-☐; hiding
those is per-game. What remains genuinely unnamed: a **hand-added** Playnite game (PluginId `Guid.Empty`) and
any third-party community plugin.

## Cover art and logos

`SystemConfig.SteamGridDbKey` is the user's **own** free SteamGridDB key — never bundle one (ToS). With it
set, tiles for stores with no built-in art source (Epic/GOG/Xbox/itch) fetch portrait covers; Steam already
has art without a key. `preferPlayniteCovers` flips the order (Playnite → SGDB → Steam vs SGDB → Playnite →
Steam). Fetched art is **display-only**.

### ⚠ The Steam CDN logo asset is `logo.png`, not `library_logo.png`

The no-key logo fallback (`SteamLogoPathAsync`) originally fetched
`…/steam/apps/<appid>/library_logo.png`, **which does not exist on the CDN** (verified Aug 6 2026: 404 for
every appid; the transparent library-hero logo is served as `logo.png`, while covers really are
`library_600x900.jpg`). Since this is the *only* automatic logo source without a SteamGridDB key, fresh
installs showed covers everywhere and almost no logos, and every game wrote a poisoned 14-day
`logo_steam_*.png.miss`. The fix renamed the cache file to `logo_steam2_<appid>.png` so those markers are
skipped without a cache purge (same trick as the GOG mis-key fix). Don't "simplify" the name back.

### Only WPF-decodable formats are requested — and the `mimes` enum is PER ENDPOINT

Every SteamGridDB asset request carries a `mimes=` filter, added centrally in `SgdbJsonAsync` (the one
method all SGDB calls pass through) rather than in the dozen query strings that build those URLs.
**Windows 10 cannot decode WebP** without the Store's "Webp Image Extensions", nor AVIF — and such a file
would download fine, be written into the cache under its cover/logo name, then fail to decode **forever**,
because a cached file that exists is a hit and never re-fetched.

⚠ **The allowed values differ per endpoint** (SGDB `openapi.yml`, checked Aug 6 2026): grids/heroes take
`png|jpeg|webp`, but **/logos take only `png|webp` and /icons only `png|ico`**. `mimes` is *validated* — an
invalid value is a **400 on every request**. The original one-list-for-everything filter sent `image/jpeg`
to `/logos`, so with a perfectly good key **every logo request 400'd** while covers succeeded: 100% logo
loss across all storefronts, each game writing a 14-day `.miss` plus an *empty* `urls_logos_*.json` (which
non-refresh reads accept at any age). Three guards now exist: `WithSgdbMimes` picks the mimes per path, a
400 is treated as **transient** (our malformed request must never cache a "this game has no art"), and
`GameArt.PurgePoisonedLogoDataOnce` (rider on the prefetch sweep, `.logofix-v1` marker) deletes the
poisoned markers/lists once per cache.

A **fourth** guard landed Aug 9 2026, because the harness had been asserting the *bug*: its check predated
the per-endpoint split and demanded png+jpeg on all four paths, so the group failed for two days on the
fixed code. `TestHarness art` now drives off `GameArt.SgdbAssetPaths` and checks two rules rather than a
literal request shape — what we ask for must be **⊆ the endpoint's enum** and **⊆ what WPF decodes on
Win10**. A per-endpoint difference stays free; a jpeg-to-/logos regression fails loudly.

⚠ **Both narrowed endpoints ask for less than they could**, which the harness now prints (`asks png;
endpoint takes png+ico`). `/logos/` drops `webp` deliberately. **`/icons/` drops `ico` for no recorded
reason, and WPF decodes ICO fine** — so an ICO-only icon is invisible to us. Open.

### Title variants tried against SteamGridDB

`SearchVariants` yields several query forms per game, and **each is a separate query whose results must still
match that variant EXACTLY** (`SgdbSearchOnceAsync`). That's what makes adding forms safe: a new variant can
only turn "no art" into "art", never "right art" into "wrong art" — the Beleth → Beethoven mismatch came from
a *loose compare*, which none of this touches. Forms: verbatim · de-camelCased · **trailing article restored**
· **bracketed metadata stripped** · trailing edition/trial qualifier dropped · trailing year dropped.

The article forms (Aug 3 2026, from Playnite's `GameNameMatcher.ToGameKey`, MIT) exist because **GOG
catalogues titles as `"Witcher 3: Wild Hunt, The"`** and SteamGridDB does not — the exact-match compare
rejected every candidate, so those games got no art at all. Both shapes are handled: `"<title>, The"` and the
mid-title `"Legend of Zelda, The: Breath of the Wild"`. Bracket stripping keeps the input when removing the
brackets would empty the name (a game legitimately titled "(Description)" must stay searchable).

### ⚠ Wheel slices resolve a DIFFERENT logo than the grid

A slice's content box is roughly square, so a 12:1 wordmark shrinks to a sliver there.
`GameArt.GetSliceLogoPathAsync` (cached as `logosq_*`) therefore takes SGDB's own **best-ranked** candidate
(score, then API order) **as long as its aspect ratio is within 4:1 … 1:2**. Outside that band it takes the
best-ranked candidate that IS in band; only if the whole list is out of band does it fall back to the
squarest. **English/untagged candidates rank first**, non-English only if that set is empty — "squarest"
once put an Arabic wordmark on a slice. The grid keeps `GetLogoPathAsync` and its Start-button pick; **the
two never share a cache file.**

### itch.io covers come from itch's own database, not SteamGridDB

itch games are the one storefront with **no other art source**: the Steam CDN is gated to
`Storefront == "Steam"`, Playnite's local covers need Playnite installed, and SteamGridDB — which matches by
**exact title** — simply has no entry for most itch titles. Result: an itch tile was blank unless SGDB
happened to carry the game.

itch's client already caches the answer locally. `%APPDATA%\itch\db\butler.db`'s `games` table — the one the
scanner **already** `LEFT JOIN`s for the title — carries `cover_url` and `still_cover_url`, so the official
cover costs one more column and no credential, no API call, and no itch login. (Playnite's plugin gets the
same field over the itch.io Web API using the profile's `api_key`; reading butler's cache is strictly less
invasive and works offline.) The receipt fallback path reads the same fields from `.itch/receipt.json`, where
they're camelCase.

- ⚠ **`NULLIF` is load-bearing.** butler stores an absent `still_cover_url` as **`''`, not `NULL`**, and
  `COALESCE` takes the empty string — which silently reads as "this game has no cover" for every game whose
  cover isn't animated (i.e. nearly all of them). It must be
  `COALESCE(NULLIF(still_cover_url,''), NULLIF(cover_url,''))`.
- ⚠ **The query stays scoped to `caves` + `games`.** The same DB holds `profiles.api_key` — the user's itch
  API credential. Never widen the FROM clause to reach it.
- `still_cover_url` is preferred because an itch cover may be an **animated GIF**; that column is the static
  frame itch itself publishes, and WPF can't animate one anyway.
- The URL carries the game's own extension inconsistently (MicroVore's ends `.jpg` and serves `image/png`).
  Harmless — decoders sniff content, and the cache filename is ours.

The URL rides on `InstalledGame.SourceCoverUrl` (distinct from `CoverPath`, which is a file already on disk)
and downloads through `UrlFileAsync`, so it gets the same `.miss` discipline and the same 512 MB quota as
every other fetched image. **Chain position: after SteamGridDB, before the Steam CDN.** SGDB art is portrait
600×900 — the tile's own shape — while an itch cover is landscape **315×250** and centre-crops in the tile's
`UniformToFill` brush, so this can only turn "no art" into "art", never displace a better-shaped cover. It's
also consulted regardless of an SGDB `.miss`, so a game that missed SGDB gets its itch cover immediately
rather than waiting out the 14-day marker.

### ⚠ One storefront-inference map: `GameLibrary.StorefrontFromUrl`

The art cache keys on **`Storefront + "_" + Name`** for every non-Steam game (`LogoCacheId`, `SgdbCacheId`),
so a *slice* — which carries only a launch URL, not a scanned `InstalledGame` — has to infer the storefront,
and **every inference must spell the store exactly as the scanner that owns it does**. There used to be two
private copies of that map (App's logo heal and the slice editor's logo fetch) that disagreed with each other
*and* with the scanners: one returned `""` for anything but Steam/GOG/Epic, the other defaulted to `"Game"`
and invented a `"Playnite"` store. The same Ubisoft game could therefore hold **three** key sets —
`Ubisoft_Name` (grid), `Game_Name`, `_Name` — with three separate `.miss` marker sets, so a slice re-fetched
art the Game Grid already had on disk.

`GameLibrary.StorefrontFromUrl` is now the single implementation; both call sites use it and neither keeps a
local copy. It covers every scheme the scanners emit (Steam, GOG, Epic, Battle.net, Ubisoft, EA, Amazon,
itch.io, Xbox `shell:AppsFolder`). **Unknown returns `""`** — the scanners' own unnamed-store value.
A `playnite://` URL lands there **deliberately**: it routes to some real storefront that isn't readable off
the URL, and Playnite is an aggregator with no `LauncherCatalog` storefront, so a `"Playnite"` store would
only key those games away from their own art. `T_GameLib.StorefrontInference` pins one URL per scanner.

**No cache migration.** The new keys are the *grid's* keys, so a previously-mis-keyed slice now hits art the
grid already downloaded instead of re-fetching. The orphaned files under the old keys are ordinary cached
images and are evicted oldest-first by the 512 MB quota; the orphaned `.miss` markers are never trimmed
(by design, above) but are zero-byte and expire from *use* at 14 days anyway. Renaming them would cost a
full-cache rewrite to save kilobytes.

### ⚠ Picks are pinned by URL FINGERPRINT, never by position

SteamGridDB re-ranks, and both candidate lists refetch every 7 days (`UrlCacheStaleAfter`), so a pick stored
as "candidate #2" silently becomes a different image. Covers were fingerprinted first
(`sgdbalt_<id>_<sha256(url)[..16]>.jpg`, `CandidateCachePath`); **logos followed** —
`logoalt_<id>_<fingerprint>.png` via `LogoCandidateCachePath`, with the chosen logo's fingerprint saved as
`CoverPick.LogoKey`.

- `LogoIndex` still exists, but it is only the **cycle's position** for the dot row. `LogoKey` is the pick.
  `GetGridLogoPathAsync` asks the fingerprint first; `LogoCandidateIndexOf` is how the Start cycle resumes
  from the artwork on screen rather than from a position that may have moved.
- **Legacy `logoalt_<id>_<n>.png` files stay readable.** `DownloadLogoCandidateAsync` adopts one by copying
  it to the fingerprinted name (identical bytes, no re-download), and the old name ages out with the quota.
- **The exporter verifies before baking.** A fingerprinted pick needs no verification — the key *is* the
  URL's identity. A legacy positional pick is byte-compared against the candidates (positional first, then
  every other), and an unverifiable one is reported, never guessed. Baking an unverified guess would ship
  the wrong artwork as a curated default for every future user.
- Shipped `CuratedArt` entries were never exposed to this: they pin explicit content-addressed CDN URLs.

### ⚠ A curated `HideLogo` must blank the whole overlay, not just the logo image

When a tile has no logo image it draws the **game's title as text** instead ("treat the title like a logo").
So a curated `HideLogo` that only made `GetLogoPathAsync` return null produced a *text* wordmark over art
curated to carry none — the opposite of the intent, on all 35 curated hidden-logo entries. The tile therefore
asks **`GameArt.LogoHiddenFor`**, which folds the curated entry in, not `CoverHidesLogo`, which is the user's
own pick alone.

Precedence, highest first: a flat-colour cover always shows the overlay (a bare colour tile reads as broken)
→ the user's Start pick, in **both** directions → the curated entry → shown. The both-directions part needs
`CoverPick.LogoUnhidden`: `HideLogo = false` is also the no-opinion default, so without a separate flag a
user could never turn a curated-hidden overlay back on. `SetLogoState` writes it only when the game is
curated-hidden, and it keeps the record from being pruned as "all defaults".

`OwnerPicksExport` asks `LogoHiddenFor` too, so a re-bake is idempotent: a hide in the shipped curated table has no local
pick to read and would otherwise be dropped on every regeneration, while an explicit un-hide still reads
through.

### Overriding the pick

- The slice editor's icon well carries **◀ ▶ arrows (bottom-left)** cycling every logo already downloaded
  for that game (`GameArt.GetDownloadedLogos` — deduped by content, no network). The well previews the real
  logo tinted, so shapes are comparable.
- Because of that, the **startup logo heal re-resolves ONLY a MISSING logo** — re-resolving a working one
  would overwrite the user's choice on the next launch.
- **Dropping an image on the well** sets it as the logo of ANY slice (`GameArt.ImportUserLogo` → copied into
  the cache as `usericon_<hash>.png`, PNG-normalised, long edge ≤1024). A **fully opaque image is REJECTED**
  before anything is written — a slice masks by alpha, so an opaque image could only paint as a solid block.
- `usericon_*` is the one thing **Clear Game Art Cache keeps** (it can't be re-downloaded):
  `ClearCache(keepPaths)` keeps the referenced ones and still sweeps orphans.

Tiles render the game's transparent logo over a darkened cover. `ArtPrefetcher` warms the cache after the
onboarding Assets step.

### ⚠ The art cache has a budget (Aug 3 2026)

`ArtPrefetcher` sweeps a **whole library**, caching several images per game (cover candidates + logo flavours
for the Select/Start cycles), and nothing used to bound the total — a big Steam/GOG/Playnite library grew
`%APPDATA%\Radiata\artcache` until the profile disk complained, and low disk on the system volume degrades far
more than Radiata. Both limits are enforced in **`ReserveCacheSpace`**, called from `WriteCacheFileAsync` —
the documented single choke point every cached download already passed through, so no source can bypass them:

- **512 MB quota.** Crossing it trims oldest-first down to 80% (so a full cache doesn't evict on every
  subsequent write). Trimming keys on last-**write** time, not last-access: NTFS last-access updates are
  disabled by default on Windows, so an access-based LRU would silently be a *random* eviction.
- **500 MB free-space floor.** Below it we stop caching entirely. Losing a cache entry costs a spinner and a
  re-fetch — the correct thing to give up here.
- `.miss` markers are **never** trimmed (deleting them triggers a re-fetch storm — the opposite of relieving
  pressure), nor are `.tmp` files owned by a concurrent writer or the dotfile state markers.
- Any bulk change to the directory made outside `WriteCacheFileAsync` (`ClearCache`, the normalise sweep) must
  call **`InvalidateCacheSizeEstimate()`** or the running total we budget against goes stale.

Per-file size is a separate, older guard: `NormalizeArtBytes` caps each image's pixel count, so the quota is
about file **count**, not one rogue image.
