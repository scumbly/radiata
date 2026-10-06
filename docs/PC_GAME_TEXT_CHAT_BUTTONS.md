# PC game text-chat buttons

The data behind the Text Chat slice action in "Try Game Default" mode ([ACTIONS.md](ACTIONS.md)): which key
opens a game's in-game text chat box. It lives in `Core/GameChatButtons.cs` (`Map`, `NoTextChat`,
`Lookup`, `HasNoTextChat`) and is read by `RunTextChat` in `Core/ActionExecutor.cs`.

## Entry rules

- `Map` is a game display name to one key. Names are unique and matched case-insensitively.
- Every value must be a token `KeypressSender` can parse: `Enter`, a letter such as `T` or `Y`, the literal `/`
  (mapped to `VK_OEM_2`) or a backtick (`VK_OEM_3`), or a chord such as `Ctrl+T`.
- An entry is added only when the key is individually confirmed against a primary source (the game's official
  documentation, a platform wiki, or consistent community guides). A guessed key must not be added: the action
  presses it before typing the message.
- `NoTextChat` lists games known to have no keyboard-opened chat box: voice, emote or preset-phrase chat only,
  chat behind a mouse click, or no multiplayer at all.
- A game in neither set is unconfirmed and is treated like a `NoTextChat` game: nothing is sent. There is no
  Enter fallback, because Enter is bound to something in every game and a wrong guess would press that
  binding before the message follows as raw keystrokes.

## Matching

`Lookup` normalizes the name and accepts an exact match first, then a containment match. `HasNoTextChat`
uses the same containment idea but only between names of at least `RelaxedMinimumLength` (8) characters, so a
short entry such as "Peak" cannot disable the slice for every unrelated title that contains it. `Lookup` is
consulted before `HasNoTextChat`, so a confirmed mapping always wins.

## Guards

The action's own guards (installed game must own the foreground, foreground re-checked before the text and
before Enter, a 15 s cooldown, message body never traced) are in [ACTIONS.md](ACTIONS.md) and must not be
relaxed from this side.
