# Sound (rebuilt Aug 2 2026)

**Read this when touching:** `SfxEngine`, `Sfx`, `MasterLimiter`, `KawaiiXylophone`, or anything that plays
or gates a sound.

## One mixed bus

**Every sound goes through `SfxEngine`.** `System.Media.SoundPlayer` is gone: winmm's PlaySound allowed
exactly one sound process-wide, so a fire chopped off the armed tick and a fast scrub stuttered. Sounds now
**overlap**.

The bus decodes each WAV resource once to 44.1 kHz stereo float (mono widened, odd rates resampled at load —
only the 24 kHz xylophone set needs it), mixes `Voice` providers that resample for pitch, and ends in a
**hard master limiter** (`MasterLimiter`) plus a clipping 16-bit conversion, so a stacked burst ducks rather
than getting loud or distorting. A **16-voice cap** stops a frantic scrub piling up at all.

## Gain knobs — the only places to change volume

All tuned by ear Aug 2 2026.

| Knob | Value | Scope |
| --- | --- | --- |
| `SfxEngine.MasterGain` | **0.75** | App-wide, applied at the bus so nothing can bypass it. |
| `SfxEngine.Ceiling` | **0.82** | Limiter. Deliberately kept **in proportion** to MasterGain (0.6 at master 0.55 → 0.82 at 0.75) so the voice count that triggers limiting doesn't drift. It can't track much higher without losing 16-bit headroom. |
| `Sfx.PhysicalGain` | **1.2** | The tap/selected pair, recorded quieter. |
| `KawaiiXylophone.MasterGain` | **0.125** | Melody notes; stacks with the bus gain. |

## ⚠ Voice bookkeeping trap (cost a real bug)

`MixingSampleProvider` drops a source the moment it returns **fewer** samples than asked for — **not** only
on a zero read. A voice must therefore release its slot on any **short** read. Getting this wrong leaked the
count until it pinned at the cap, and every later sound was silently dropped, **permanently**.

The engine also counts voices itself rather than reading `MixerInputs` — that property hands out the live
source list the audio thread mutates. The idle timer tears the device down unconditionally, so any future
drift self-heals.

## Device lifetime

Opened on the first sound, released after **20 s of silence**. A tray app must not sit on the audio
endpoint: it would keep a TV/receiver awake and park Radiata in the volume mixer forever. Every wheel open
**primes** it (`Sfx.WheelOpened`), and the wheels-toggle chord primes at chord time so its 100/175 ms
animation delay doubles as lead time; `TearDown` releases it outright.

**Wake-ahead (cold opens).** Downstream hardware (TV/AVR HDMI lock, soundbar/DAC auto-standby, codec amp
power-down) unmutes ~100 ms–2 s after samples resume and swallows whatever plays inside that window — the
"first sound after a silence is dropped" symptom. A **cold** `Prime` therefore also queues a 300 ms
sub-audible wake voice (40 Hz at −54 dBFS in the samples, ≈ −56.5 dBFS after `MasterGain`): the mixer's ReadFully zeros wake stream-triggered sinks at
open, but level-triggered standby detectors need a non-silent signal. Real sounds arriving 300 ms+ after
the prime land on an awake chain; an *instant* first tick on a slow AVR can still clip — the complete
cure would be a keep-alive stream, rejected per the endpoint rule above (revisit as an opt-in knob if
the wake-ahead proves insufficient on hardware).

## The sound sets (Aug 29 2026)

Six sets, resolved from `SystemConfig.SoundTheme` ("material" / "digital" / "physical") + the material by
`Sfx.ResolveSet`; `Sfx.Theme` always holds the RESOLVED set name. The Customize picker is **Themed**
(default — stored token `"material"`: follow the material's own set via `Materials.SoundThemeFor`; its
tile glyph previews which set that is, in the material's accent — `ThemedSoundLook`) / Digital / Physical / **Silent**
(= `SoundEffects` off). A material change writes `soundTheme` back to `"material"`.

| Set | Armed | Fired | Wheels on/off |
| --- | --- | --- | --- |
| digital (Pearl) | slice-armed | slice-fired | shared pair |
| **obsidian** | *Digital's samples at 0.84 speed* | *same* | shared pair, also 0.84 |
| physical (the flats) | tap (±3% jitter) | selected | shared pair |
| kawaii | xylophone melody | kawaii-slice-fired | enable: kawaii-enable-wheels (×0.27) / disable: shared |
| mesa | tap — Physical's, jitter and all | mesa-slice-fired | shared pair |
| salvage | salvage-slice-armed | salvage-slice-fired | own pair |
| reactor | reactor-slice-armed | reactor-slice-fired | own pair |

**Naming contract:** a sample is named `<set>-<event>.wav` for the set that PLAYS it, so the table above
reads back as its own filenames. The only unprefixed samples are the genuinely shared ones — `tap` /
`selected` (Physical's pair, and Mesa borrows the tap with physical's gain and jitter) and
`enable-wheels` / `disable-wheels` (the fallback pair), and **Obsidian**, which is a pitched VARIANT of
Digital rather than a set with recordings of its own. **When a set is re-pointed at another set's
recording, rename the file to follow it** rather than leaving a name that lies about who plays it; the
sounds have been reshuffled by ear several times and the names are the only durable map.

Provenance is now decoupled from the names: the mesa/salvage/reactor recordings and Kawaii's on/off
material originate in the maintainer's picks under `Contenders\` (`Alternates\` holds the runners-up),
converted to 44.1 kHz stereo 16-bit, then reshuffled between roles — so **don't infer a file's history
from its current name**; git history is the record.

**`Assets\SFX-Edit\` is the round-trip surface** (development repository only, not in the public source) for maintainer audio edits: every played sound under a
readable `<Set> - <Event>` name, grouped per set, with a README carrying the asset mapping and each
sound's in-app gain. Hand it out, take it back, re-ingest. Mesa's fire and all eight Salvage/Reactor
samples went through one such pass (trimmed shorter, peaks down but RMS flat-to-higher) — the sfx
folder is authoritative, `SFX-Edit` is a working copy that goes stale the moment an asset changes.

A set is a `Sfx.SoundSet` record — sample name per event plus a by-ear gain, defaulting to 1.0 so each
entry names only its deviations. Current trims: digital arm 0.54 (Obsidian inherits it); physical arm/fire 1.2 (Mesa's borrowed
arm carries the same); mesa fire 1.0; kawaii fire 0.85 / **enable 0.27**; salvage arm 0.75 / fire 1.0 / **on-off pair
0.69 — both halves deliberately equal** (the wheels chord should not change loudness with
direction); reactor arm 0.38 / fire 0.67. A set that falls back to the **shared** on/off pair plays it at
unity; Mesa's borrowed tap is the one sample that carries the lending set's gain.

## Kawaii's ARMED sound is a melody, not a chime

`KawaiiXylophone`. Arming plays the **next note of a tune**, so scrubbing the ring plays the song.
**Slice order is irrelevant** — the sequence is purely "how many times have you armed something".

- Each **wheel open** rotates to the next of **five** songs **in fixed order** — Nerv → Moon → Run →
  Twinkle → Sunshine — and rewinds it to note 1. (`_song` starts at −1 so the first open lands on Nerv.)
- The editor and the Game Grid are **not** opens; the Grid's own nav ticks continue the current song.
- A **50 ms debounce** swallows boundary flutter: aiming at a slice edge can arm two slices within a frame
  or two. A debounced arm neither plays **nor advances**, so the melody never silently skips.
- `kawaii-slice-armed.wav` is consequently unused for arming.

## The samples

Eight xylophone strikes, **C6–C7 white keys only** (`Assets\sfx\xylophone`) — no accidentals — so every
note is played by resampling the nearest sample (the pitch ratio also scales length).

The strikes and the four `kawaii-*.wav` cues derive from freesound "Xylophone" by mooncubedesign, **CC0** —
no attribution obligation. Recorded in `THIRD-PARTY-LICENSES.md` §4b.

Songs are stored at **written pitch** with a per-song `Octaves` field. Each shift is the **whole-octave**
move that lands the song's mean pitch nearest MIDI 90 (F♯6, mid-band), so all five sit in one register while
staying recognisable as transcribed. Transposed songs stray a few semitones outside the sampled range at
their extremes (Nerv tops at F7, Sunshine bottoms at G5), which a mallet strike takes cleanly.

**Recompute a song's shift if a transcription changes enough to move its mean by half an octave.**

## Arcade (Sep 2026) — bespoke banks, shared chrome, licensed samples

**Read this when touching:** `ArcadeSfx`, `Core\Arcade\ArcadeSfxTuning`, `ArcadeControl.PlayGameCues`,
`tools\SfxIngest` (development repository only, not in the public source), or anything under
`Assets\sfx\arcade\` (licensed samples; development repository only, not in the public source).

The Arcade no longer borrows the wheel's blips. **Each game has its own sample bank** (Kabloom soft and
papery, Connate glassy with a coin till, Petalpop poppy and punchy, Internode airy and electronic) and the
**host chrome shares one vocabulary** (picker nav/select/close — the cabinets appear silently —
pause open/close/nav/option, confirm
open/yes/no, Reset, how-to open/close, READY, guard show/clear/override). Drop-in script games keep the
nine-name generic vocabulary, now on a `generic-*` bank, reached via `ArcadeSfx.Generic.Cue(string)`. Arcade
still does **not** follow the wheel material or sound theme.

### The layer — `ArcadeSfx` (shell)

Built on `SfxEngine.LoadResource`/`Play` exactly as `KawaiiXylophone` is; same bus, same limiter, same
16-voice cap, gated on `Sfx.Enabled`. A role is a **`Bank`**: one or more takes (resource stems under
`Assets\sfx\arcade\`), a per-take gain trim, and a jitter.

- **One take per role**: several takes for one event read as several events. `Bank` still supports
  rotation among takes (random among the OTHERS, never the same twice running) but nothing ships with more than one.
- **A bank may name any stem, including another bank's.** A stem resolves under `Assets\sfx\arcade\`
  whatever the bank's family, so two roles can share one take (Internode's elevated catch is the token's take
  higher; every game's new best is Internode's) while each keeps its own family trim and gain — what is shared
  is the sample, never the mix position. ⚠ A gain is only portable across a swap when both takes were
  normalised to the SAME peak target; check `normalizeDbfs` in the manifest before trusting one.
  ⚠ **The take a bank stops naming must leave the exe**: mark its manifest entry `"alt": true`, which keeps
  the record and the `SFX-Edit` audition copy but stops the shipped WAV, or `TestHarness sfx` fails it as an
  unreferenced resource. A cue with no take for now keeps its vocabulary method as a no-op, so the sim and the
  dispatcher never learn a sound went missing.
- **Jitter:** ± uniform pitch per play (typically 2–5%) is the only per-play variation, so a repeated hit isn't a stamp.
- **Ladders:** `Play(bank, ladderStep)` climbs `ArcadeSfxTuning.LadderSemitones` per step, capped at
  `LadderMaxSteps`, on a fact the sim exposes — Kabloom `LastCascadeSize`, Connate
  `ChainDepth` / `ComboCount`, Petalpop `Combo`, Kabloom's per-level gem count. Internode's catch is
  deliberately flat: `TokenStreak` is a score fact, not a ladder.
  Resampling shortens the clip with the pitch, which is why the cap exists.
- **Layers:** `Layer(first, second, delayMs)` plays two banks a few tens of ms apart for the big moments (smash
  then shards, explosion then glass, fanfare then payout, buzz then thud). Async, contained, re-checks `Enabled`.
- **Throttles:** wall-clock, host-side, from `ArcadeSfxTuning` — Kabloom gem bursts, Petalpop paddle/bumper
  storms, ComboShout. The sim-side throttles (Connate collect cadence, Internode token spacing) stay where they
  were.
- **Prime:** `ArcadeSfx.Opened()` wakes the device on every arcade open (the arcade never primed before, so a
  cold open ate the first tick) and decodes the chrome off the UI thread.
- **Families in the dispatcher:** `ArcadeControl.Play<Game>` groups cues so the heaviest speaks (game over ≻
  life lost ≻ ball lost; board clear ≻ bomb ≻ chain ≻ merge; smash ≻ split ≻ pop ≻ chip). Silent by design:
  `Kabloom.Focus` (moving the reticle is not an action; its click became the REVEAL), `Connate.Move`,
  `Internode.Land`, `Internode.Miss`, `Internode.OverTopLand`.

**Sims stay audio-blind.** They raise cue flags and expose read-only facts; nothing about sound enters a
snapshot. New cues this pass: Kabloom `LevelUp`/`CampaignComplete`/`Growth`/`BeeDeparts`; Connate
`BoardClear`/`BombEarned`/`BombExplode`/`GarbageArrive`/`GarbageCleared`/`ComboStep` (`NewBest` used to
mean three different things and `Chain` doubled as the bomb); Petalpop `Bumper`; Internode
`OverTopLand`/`Respawn` (the `DoubleJump` take retired with the double jump, Sep 11 2026). Kabloom `NewBest` fires once per run, on the gem that first passes the
best the run started with. **One new-best voice across the arcade**: every game's `NewBest` bank
names `internode-newbest`; the games' own former takes are audition-only in the manifest.

### Tuning

`Core\Arcade\ArcadeSfxTuning` is in `ArcadeTuning.TuningTypes`, so `%APPDATA%\Radiata\arcade-tuning.json`
moves it live on the next open: `SfxMasterGain` (0.8) and the five family gains, `LadderSemitones` /
`LadderMaxSteps`, the four `*MinIntervalMs` throttles, `BloomLayerAt`. Field names are deliberately distinct
from every other tuning class (bare keys resolve first-match). Per-take trims are on the banks in code.

### The samples and the licence boundary

`Assets\sfx\arcade\*.wav` (own csproj `<Resource>` glob, non-recursive) are cut from **purchased packs** —
Tao & Sound *Buttons SFX Library* + *Puzzle Audio Bundle*, GameDevMarket *Inventory* + *Magic Spells* — whose
terms allow embedded use only and forbid redistributing the files (THIRD-PARTY-LICENSES.md §4c, the credit).
Consequences, all deliberate:

- **Never in the public mirror.** `tools\export-public.ps1` (development repository only, not in the public source) omits the folder; the mirror compiles (empty
  glob) with a silent Arcade. A missing resource is an empty buffer and `Play` on an empty buffer is a no-op.
- **The release build fetches them privately:** `release.yml` downloads a zip from the repo secret
  `ARCADE_SFX_URL` and verifies it against the repo variable `ARCADE_SFX_SHA256`;
  `tools\pack-arcade-assets.ps1` (development repository only, not in the public source) makes the zip (this folder only; the music is mirrored) and prints the hash.
  Ritual in docs/INSTALLER.md.
- **`TestHarness sfx`** proves every bank take is an embedded resource, no take is empty or over 2 s, and no
  shipped WAV is unreferenced — and SKIPS in a build that carries none (the mirror's own CI).

### Ingest — `tools\SfxIngest`

(Development repository only, not in the public source.) `manifest.json` is the durable record of what every sample IS: pack file → stem, trim (to the measured active
span), fades, peak target (−3 dBFS default), gain, optional speed (a resample, like the engine's pitch) and
reverse (Internode's rewind is a reversed metal slide). `dotnet run --project tools\SfxIngest -- --source
<sounds folder>` decodes WAV (any depth) or MP3 (Media Foundation), resamples to 44.1 kHz stereo 16-bit — the
engine's native format, so nothing resamples at load — writes the shipped WAV, a readable copy under
`Assets\SFX-Edit\08 Arcade`, and regenerates that folder's README table. Idempotent via sidecar
fingerprints; `--force`, `--only <name>`, `--dry`. Entries marked `"alt": true` are audition-only.
**To swap a take: edit the manifest line, re-run, rebuild.** Don't hand-edit the WAVs — the manifest is the
source of truth and the next ingest would overwrite them.

**Sizing:** ~140 shipped samples, all under 1.8 s, ~14 MB on disk (`Assets/sfx/arcade`); the engine's
forever-cache holds a decoded sample at ~353 KB per second, so a full session of samples tops out around 15 MB.
The seven music beds are mono 64 kbps AAC in `Assets/music/*.m4a`, ~12 MB embedded; a bed decodes on demand
and only the playing one is held.

## Arcade music (Sep 2026) — one bed per game, one switch for the whole arcade, on by default

**Read this when touching:** `ArcadeMusic`, `SfxEngine`'s music channel, or anything under `Assets\music\`.

Each game can play a looping music bed. **On by default and one choice for the whole arcade**, switched by a
**MUSIC** row the HOST appends to that game's pause menu — flipping it in any game turns every game's bed on
or off, so no sim knows music exists, and a new game gets the row by having a bed at all. The choice lives in
`ArcadeStore` as a top-level flag (`File1.MusicOff`), not a field on any game's slot (a fact about the player,
not the run, so it survives a finished run and a Reset), written the moment it is toggled. ⚠ It is stored as
the OFF choice, so the defaulted false reads as on for a fresh install and for an older `arcade-state.json`
alike; a pre-global file's per-game `Slot.MusicOff` folds up into the flag once on first load, adopting OFF if
any game had it off.

### It is a STREAM, not a sample

`SfxEngine.PlayMusic` mixes a `MusicStream` into the same bus as every cue — one audio path, one limiter — but
unlike a cue it is **decoded as it plays** (`StreamMediaFoundationReader` over the resource's compressed
bytes). A four-minute track is ~90 MB as mix-format float, so buffering one the way `LoadResource` buffers a
cue is not an option; only the 1–2 MB of AAC stays resident (cached per track for the process, so a resume or
a playlist wrap skips the copy). The start — the resource read and the decoder's index — is built on a worker
behind a generation counter, so a hand-over never lands inside the frame pump.

**The beds ship as mono AAC (`*.m4a`, 44.1 kHz, 64 kbps)**, re-encoded from the creator's MP3s by
`tools\MusicIngest` (development repository only, not in the public source); the originals stay in `Assets\music\source\`, which the non-recursive glob never embeds.
Mono because they play under a game's own audio at a quarter of the cue level, where width buys nothing and
the embedded size is paid by every build and every update: 23.5 MB became 12.0 MB. `MusicStream` widens mono
to the stereo bus. AAC over MP3 because Windows Media Foundation carries the AAC encoder on every edition and
an MP3 sink writer on none we could rely on.

Three details the channel depends on:

- ⚠ **It must never return a short read.** A short read is precisely how a finished `Voice` tells
  `MixingSampleProvider` to drop it (see the trap above), so a bed that ran dry for one buffer would vanish
  for the rest of the track. `MusicStream` pads any shortfall with silence and returns 0 exactly once — after
  its fade-out — which is the one deliberate self-removal.
- **It does not count against the 16-voice cap.** A game's cues must never be dropped because a bed is up.
- **It holds the idle teardown off.** `BumpIdle` arms no countdown while music plays, or the 20 s window would
  close the device mid-track; stopping the bed re-arms it. The bed stops on dismiss, so the tray app still
  never sits on the endpoint (docs/SOUND.md ▸ Device lifetime).

Gain, fades and ducking are one mechanism: a per-frame ramp toward a target, so nothing clicks.
`ArcadeSfxTuning.MusicGain` (0.20) is the bed's level and `MusicDuck` (0.35) the fraction it drops to while a
menu, a card, a confirm prompt or the READY beat is up — both live in `arcade-tuning.json`.

### The beds

`ArcadeMusic.Beds` is the whole mapping, and its **order is the cycle**:

| Game | Tracks | Policy |
| --- | --- | --- |
| Petalpop | *I Miss Toonami* | loops |
| Connate | *ARTIFICIAL INCOHERENCE* | loops |
| Kabloom | *A NEON RAIN THAT NEVER ENDS*, *Eventual Consistency* | **playlist**: each plays out in full, then the other, wrapping. Not tied to levels — a board change never interrupts a track. |
| Internode | *Eudaimonia*, *P01s0n.p1ll*, *decoupl.3d* | **playlist**, like Kabloom: each plays out in full, then the next, wrapping. ⚠ It was keyed to the STAGE and then to the flavour rotation, and the bed now does not track progression at all (since Sep 9 2026) — so a stage change never interrupts a track, and `Policy.Keyed` is gone rather than left unused. |

⚠ **A playlist hand-over is one request, not one per frame.** `SfxEngine.MusicEnded` reads FALSE while a
start is still decoding off the UI thread. Without that, every frame between "track ended" and "next track
landed" saw the ended track, advanced the index again and issued another start that cancelled the last, so
the track that finally played depended on how many frames the decode took — with three tracks it could be the
same one every time, and Internode's playlist sounded like one song looping.

**A bed resumes where it was left.** `ArcadeMusic.Stop` records, per game, the track and how far into it
playback was (`SfxEngine.StopMusic` returns the position; `MusicStream.PositionSeconds` reads the decoder),
and the next `Sync` for that game starts the same track from that point (`PlayMusic`'s `startSeconds`, a seek
on the `Mp3FileReader`) behind `ArcadeSfxTuning.MusicResumeFadeMs` (700 ms) rather than the ordinary
`MusicStartFadeMs` (400 ms). A track that had run out hands the spot to the next one from its top. The remembered position
is process-scoped, like the live game objects — a fresh process starts every bed from the top. Switching the
MUSIC row off and on goes through the same path, so that resumes too.

`ArcadeControl.SyncMusic` runs once per frame before the state branch, so a paused game keeps its bed and a
finished playlist track still hands over. It reads nothing off any sim now: both surviving policies are
self-driving, so no game hands the host a progression number and none should.

### The tracks are CC BY-SA 4.0

`Assets\music\*.m4a` are by **Dylan Ribb**, included with the creator's express permission and under **CC BY-SA 4.0**
(THIRD-PARTY-LICENSES.md §4d, the credit and track list; Settings ▸ About carries the credit too). They are
**not** GPL. Unlike the cues they **are** in the public mirror (`export-public.ps1` lists `*.m4a`; the
originals under `source\` and `spare\` stay out). A new track must be added to §4d's track list. A build
without them simply has no music — a game whose track is missing does not even offer the row. **`TestHarness sfx`** proves every track
a bed names is an embedded resource, and skips when the build carries none.
