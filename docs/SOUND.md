# Sound

Every sound goes through one mixed bus, `Sound/SfxEngine.cs`. Related code: `Sound/Sfx.cs` (the wheel's sound
sets), `Sound/KawaiiXylophone.cs`, `Arcade/ArcadeSfx.cs`, `Arcade/ArcadeMusic.cs`,
`Core/Arcade/ArcadeSfxTuning.cs`.

## One mixed bus

`SfxEngine` decodes each WAV resource once to 44.1 kHz stereo float, mixes `Voice` providers that resample for
pitch, and ends in a hard master limiter (`MasterLimiter`) and a clipping 16-bit conversion, so a stacked
burst ducks instead of getting loud or distorting. Sounds overlap; a cap of 16 voices (`MaxVoices`) stops a
frantic scrub from piling up.

Volume is set in these places only:

| Knob | Value | Scope |
| --- | --- | --- |
| `SfxEngine.MasterGain` | 0.75 | App-wide, applied at the bus so nothing can bypass it |
| `SfxEngine.Ceiling` | 0.82 | The limiter's peak; keep it in proportion to `MasterGain` |
| `Sfx.PhysicalGain` | 1.2 | The tap/selected pair |
| `KawaiiXylophone.MasterGain` | 0.125 | Melody notes; stacks with the bus gain |

## Voice bookkeeping

`MixingSampleProvider` drops a source the moment it returns fewer samples than requested, not only on a zero
read. A voice must release its slot on any short read; miscounting leaks the voice count until it pins at the
cap and every later sound is silently dropped. The engine counts voices itself rather than reading
`MixerInputs`, which exposes the live list the audio thread mutates.

## Device lifetime

The output device is opened on the first sound and released after 20 s of silence (`IdleCloseMs`). A tray
app must not hold the audio endpoint: it would keep a TV or receiver awake and list Radiata in the volume mixer
permanently. Every wheel open primes the device (`Sfx.WheelOpened`) and `TearDown` releases it. A cold prime
also queues a short sub-audible wake voice so downstream hardware that unmutes slowly has a signal to wake on.

## Wheel sound sets

`Sfx.ResolveSet` turns `system.soundTheme` (`material`, `digital`, `physical`) plus the material into one of
the sets below; `Sfx.Theme` always holds the resolved name. `soundEffects: false` is the Silent choice.

| Set | Used by |
| --- | --- |
| `digital` | Pearl |
| `obsidian` | Obsidian (Digital's samples played at 0.84 speed) |
| `physical` | The flat materials |
| `kawaii`, `mesa`, `salvage`, `reactor` | The material of the same name |

A sample is named `<set>-<event>.wav` for the set that plays it. Shared samples (`tap`, `selected`,
`enable-wheels`, `disable-wheels`) are unprefixed. When a set is pointed at another set's recording, name the
file to match. A set is a `Sfx.SoundSet` record: a sample name per event plus a by-ear gain that defaults to 1.0.

Kawaii's armed sound is a melody: `KawaiiXylophone` plays the next note of a tune on each arm, so scrubbing
the ring plays the song. Each wheel open rotates to the next of five songs in a fixed order and restarts it at
note 1. The editor and the Game Grid are not opens. A 50 ms debounce swallows boundary flutter, and a
debounced arm neither plays nor advances the tune. The notes are eight xylophone strikes (C6 to C7, white keys)
resampled to the pitch needed.

## Arcade

The arcade does not follow the wheel material or sound theme. Each game has its own sample bank and the
host chrome (picker, pause menu, confirm prompts, READY, guard card) shares one vocabulary, through
`ArcadeSfx` on the same bus, limiter and voice cap, and follows `Sfx.Enabled`.

- A `Bank` is one or more takes (resource stems under `Assets\sfx\arcade\`), a per-take gain trim and a pitch
  jitter. A bank may name another bank's stem; gains are only portable between takes normalized to the same
  peak.
- Ladders (`Play(bank, ladder)`) climb `ArcadeSfxTuning.LadderSemitones` per step on a fact the simulation
  exposes, capped at `LadderMaxSteps`. Layers (`Layer(first, second, delayMs)`) play two banks a few tens of
  milliseconds apart for the big moments.
- Simulations stay audio-blind: they raise cue flags and expose read-only facts, and nothing about sound
  enters a snapshot. Cues are read once per rendered frame, so several simulation steps coalesce into one
  sound.
- `ArcadeSfx.Opened()` wakes the device on every arcade open. Feel numbers are in `ArcadeSfxTuning`.
- The arcade sample files are licensed for embedded use only and are not in this repository. A build without
  them has a silent arcade: a missing resource decodes to an empty buffer and playing it is a no-op.
  Credits are in [../THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md).

## Music

Each game can play a looping music bed (`ArcadeMusic`), on by default. One choice covers the whole arcade: a
MUSIC row the host appends to a game's pause menu flips a single flag, stored as `MusicOff` in
`arcade-state.json`, so no simulation knows music exists. A game with no track present is offered no row.

- A bed is a stream, not a sample. `SfxEngine.PlayMusic` mixes a `MusicStream` into the same bus but decodes
  it as it plays (a four-minute track is about 90 MB decoded). The beds are mono AAC (`Assets\music\*.m4a`).
- `MusicStream` must never return a short read before its deliberate end (see *Voice bookkeeping*); it pads
  with silence. It does not count against the 16-voice cap.
- While music plays, `BumpIdle` arms no idle countdown, so the device is held open; stopping the bed re-arms
  it, and the bed stops when the arcade is dismissed.
- The bed ducks to `ArcadeSfxTuning.MusicDuck` under any menu, card, prompt or READY beat; `MusicGain` is its
  level.
- Kabloom plays a two-track playlist and Connate and Petalpop one looping track each. A playlist hand-over is
  one request, not one per frame: `SfxEngine.MusicEnded` reads false while a start is still decoding. A bed
  resumes where it was left when its game is reopened in the same process.
- The tracks are by Dylan Ribb under CC BY-SA 4.0, not the GPL; the credit and track list are in
  [../THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md), section 4d.
