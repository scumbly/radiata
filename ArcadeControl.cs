using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// The round arcade window: the frame pump, the input funnel, freeze/resume, and the bleed-through guard.
/// One control hosts every game — games bring a sim (<c>Core/Arcade</c>) and a renderer (<c>Arcade\</c>), not
/// a window.
///
/// <para><b>Code-only, no XAML.</b> Deliberate: a resource lookup that misses at runtime is this repo's
/// most-repeated crash class, and this surface paints 60 times a second over someone's game. Everything it
/// draws comes from <see cref="ArcadeChrome"/>'s frozen brushes.</para>
///
/// <para><b>Zero cost when closed.</b> The <see cref="CompositionTarget.Rendering"/> hook is attached in
/// <see cref="Open"/> and detached in <see cref="Close"/> — never left ticking.</para>
/// </summary>
internal sealed class ArcadeControl : FrameworkElement
{
    private IArcadeGame?     _game;
    private IArcadeRenderer? _renderer;
    private bool _running;

    // Input, written by App on controller events and consumed by the pump.
    private float _stickX, _stickY;
    private bool  _crossDown, _triangleDown, _squareDown;
    private bool  _crossEdge, _triangleEdge, _squareEdge, _skipEdge;
    private ArcadeInput.DPad _dpadHeld, _dpadEdges;
    /// <summary>Stick directions currently past the menu threshold, so the pause layer can take the stick as
    /// presses without a game's continuous aiming leaking into it.</summary>
    private ArcadeInput.DPad _stickHeld;

    private readonly Stopwatch _clock = new();
    private double _accumulator;
    private double _readyLeft;          // rendered-but-not-simulated resume beat

    // ── The Arcade picker (an "Arcade" slice with no game chosen) ─────────────
    private bool _picker;
    private int  _pickerIndex;
    private readonly ArcadePickerRenderer _carousel = new();
    private readonly ArcadeCarouselNav    _nav      = new();
    /// <summary>The arcade was opened by the Arcade Launcher, so the cabinets sit UNDER the game as a
    /// back-out layer: ○ returns to them, and ○ again leaves. A game-specific slice opens that game with
    /// nothing beneath it, and ○ leaves outright.</summary>
    private bool _fromLauncher;

    /// <summary>The board a game left behind when ○ returned to the cabinets, while it shrinks into its
    /// cabinet's screen (<see cref="ArcadePickerRenderer.DrawArrival"/>); null once it has landed.</summary>
    private ArcadeShot? _arriveShot;
    private double _arriveLeft;

    /// <summary>The cabinet ✕ chose, while its screen grows out to become the game; the game is made live
    /// when it lands.</summary>
    private ArcadeCatalog.Entry? _launchEntry;
    private double _launchLeft;
    private double LaunchProgress => _launchEntry is null ? 0
        : 1 - Math.Clamp(_launchLeft / Math.Max(1e-3, ArcadePickerTuning.LaunchSeconds), 0, 1);

    // ── The disc's size ───────────────────────────────────────────────────────
    // The control is laid out for a GAME; the launcher draws inside it at 1 / ArcadeTuning.GameDiscScale.
    // The factor eases between the two, so the window itself visibly grows into the game a cabinet opens and
    // shrinks back when ○ returns to the cabinets. The wheel hub behind it follows through DiscRadiusChanged
    // — frame-locked to this, never on a clock of its own.
    private double _disc = 1, _discFrom = 1, _discTo = 1, _discLeft, _discSeconds;
    private static double LauncherDisc => 1 / Math.Max(1, ArcadeTuning.GameDiscScale);
    /// <summary>The disc's current radius in DIPs, raised every frame it changes.</summary>
    public event Action<double>? DiscRadiusChanged;
    private double MaxRadius => Math.Min(ActualWidth, ActualHeight) / 2;
    private double Radius    => MaxRadius * _disc;

    private void SetDisc(double to)
    {
        _disc = _discFrom = _discTo = to;
        _discLeft = 0;
        // Raised even for a snap: under Reduce Motion this IS the transition, and the hub must still land.
        if (MaxRadius > 0) DiscRadiusChanged?.Invoke(Radius);
    }

    private void AnimateDisc(double to, double seconds)
    {
        if (ReduceMotion || seconds <= 0) { SetDisc(to); return; }
        _discFrom = _disc;
        _discTo = to;
        _discLeft = _discSeconds = seconds;
    }

    private void StepDisc(double dt)
    {
        if (_discLeft <= 0) return;
        _discLeft = Math.Max(0, _discLeft - dt);
        double t = 1 - _discLeft / Math.Max(1e-3, _discSeconds);
        t = t * t * (3 - 2 * t);   // smoothstep: it leaves and arrives at rest
        _disc = _discFrom + (_discTo - _discFrom) * t;
        if (MaxRadius > 0) DiscRadiusChanged?.Invoke(Radius);
    }

    /// <summary>Reduce Motion: the picker's carousel snaps to its target and its backdrop holds still.
    /// Game content is out of scope (docs/MOTION-INVENTORY.md); this covers the arcade's own chrome.
    /// Pushed by <c>App.ApplyMotionPolicy</c>.</summary>
    public bool ReduceMotion { get; set; }

    /// <summary>Which of the three window positions a game's disc sits at — 0 left, 1 centre, 2 right. The
    /// host owns the value (it owns the slide); this side only fades the cue for a direction that is clamped.</summary>
    public int WindowPosition { get; set; } = 1;

    /// <summary>Whether the summon chord's hold is the TRIGGER pair, which is what moves the window then —
    /// so the cues name L2/R2 rather than the bumpers. See <c>App.HoldIsTrigger</c>.</summary>
    public bool SlideOnTriggers { get; set; }

    // ── The START pause menu ─────────────────────────────────────────────────
    // The sim is not stepped while it's up, so "paused" is literal rather than a frozen-looking overlay.
    private bool _paused;
    private int  _pauseIndex;
    /// <summary>The rows above the game's own options. Reset is destructive, so it is never the landing row;
    /// HOW TO PLAY sits between them, so a thumb walking down from Resume meets the harmless row first.
    ///
    /// <para>⚠ DERIVED, not a constant — the how-to row exists only for a game that has a card, so the count
    /// changes per game. It was <c>const int PauseFixedRows = 2</c>, and every index in the menu was measured
    /// against it; hard-coding 3 instead would have silently mapped RESET onto a missing row for any future
    /// game without a how-to.</para></summary>
    private FixedRow[] FixedRows => HowToCard is null
        ? [FixedRow.Resume, FixedRow.Reset]
        : [FixedRow.Resume, FixedRow.HowTo, FixedRow.Reset];

    private enum FixedRow { Resume, HowTo, Reset }
    /// <summary>The confirm key the Reset row parks behind; no game option can be named this.</summary>
    private const string ResetKey = "host:reset";

    private static string FixedRowLabel(FixedRow row) => row switch
    {
        FixedRow.Resume => Loc.T(UiText.Arcade.Resume),
        FixedRow.HowTo  => Loc.T(UiText.Arcade.HowToPlay),
        _               => Loc.T(UiText.Arcade.Reset),
    };

    // ── The how-to-play card ─────────────────────────────────────────────────
    // △ during play, or the pause menu's HOW TO PLAY row. A LAYER above both: opened from the menu it backs
    // out to the menu, opened from play it backs out to play, and the sim is stepped in neither case.
    private bool _howTo;
    /// <summary>The one on-screen button prompt: a △ cue, retired PER GAME the first time that game's card
    /// is opened. Mirrors <see cref="ArcadeStore.LoadHowToSeen"/> for the LIVE game so the render path never
    /// touches the disk cache 60 times a second.</summary>
    private bool _howToSeen;

    // ── The confirm prompt ────────────────────────────────────────────────────
    // An option a game declares destructive (IArcadeGame.ConfirmPauseOption) parks here instead of applying.
    // ⚠ NOTHING is changed while this is up — the option row still shows the live value, and cancelling
    // leaves the run untouched. That is the whole point: "back out at any point before this".
    private string? _confirmPrompt;
    private string  _confirmKey = "";
    private int     _confirmChoice;
    private bool    _confirmYes;   // the highlighted answer; lands on NO

    // Guard card state.
    private Arcade.GuardCopy? _guard;
    private bool   _guardOverridable;
    private double _guardHeld;          // seconds △ has been held on the card
    /// <summary>The play-anyway override, for the rest of this app session only — never persisted. The
    /// control lives as long as the overlay window, so the field's lifetime IS the session.</summary>
    private bool _overrideAccepted;

    public ArcadeControl()
    {
        IsHitTestVisible = false;   // the overlay is click-through; the mouse is only used for click-outside
        // Pinned: the disc's angles are the stick's, so an RTL ancestor must not mirror it (see RadialMenuControl).
        FlowDirection = System.Windows.FlowDirection.LeftToRight;
    }

    /// <summary>True while a game (or its guard card) is on screen.</summary>
    public bool IsOpen => _running;

    /// <summary>True when the guard card is up instead of a game — App uses this to keep △/□/stick away from
    /// a game that isn't running.</summary>
    public bool GuardShowing => _guard is not null;

    /// <summary>The session override has been granted, so a later open shouldn't ask again.</summary>
    public bool OverrideAccepted => _overrideAccepted;

    /// <summary>Raised when the card's play-anyway hold completes, so App can re-open properly (it owns the
    /// decision of which game and whether to re-check anything).</summary>
    public event Action? OverrideGranted;

    /// <summary>Raised (one dispatcher turn later) after a frame-pump or render throw made the control close
    /// itself. The host owns <c>_arcadeOpen</c>, the mouse watch, the hotkeys and the pad suppression; without
    /// this it would keep swallowing every button into a collapsed control until ○ was pressed.</summary>
    public event Action? Failed;

    /// <summary>The one exit for a throw on the render path — pump or paint. A game is never worth the app:
    /// bail out of the whole feature for this session, and tell the host so it tears down its half.</summary>
    private void Fail(string where, Exception ex)
    {
        Trace.WriteLine($"[Arcade] {where} failed, closing: {ex}");
        try { Close(); }
        catch (Exception inner) { Trace.WriteLine($"[Arcade] close after failure also threw: {inner.Message}"); }
        Dispatcher.BeginInvoke(() => Failed?.Invoke());
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Bring up <paramref name="gameId"/>, resuming its frozen state if there is one — or the
    /// <b>Arcade picker</b> when the id is blank or unknown (an "Arcade" slice with no game chosen, or a
    /// config from a newer build naming a game this one doesn't have; neither is a dead end).
    /// <paramref name="guard"/> non-null shows the bleed-through card INSTEAD — it covers the whole surface,
    /// picker included, since picker input bleeds exactly like game input.</summary>
    public void Open(string? gameId, Arcade.GuardCopy? guard, bool overridable, bool fromLauncher = false)
    {
        _fromLauncher = fromLauncher;
        // Re-read the dev tuning override on EVERY open — that's what makes "edit arcade-tuning.json,
        // re-open the wheel" true (the live game object survives across opens, so a construction-time read
        // would happen once per process). Absent file = the compiled defaults, restored.
        ArcadeTuning.LoadOverrides();
        // Same story for drop-in sprite art: forget what was resolved so a PNG added to the override folder
        // since the last open is picked up. Nothing re-probes from the render pump.
        ArcadeSprites.Reload();
        ArcadeSfx.Opened();

        var entry = ArcadeCatalog.Find(gameId);
        if (entry is null) EnterPicker(ArcadeStore.LoadLastCabinet());
        else
        {
            _picker = false;
            EnsureGame(entry);
        }
        // The host grew the hub to whichever surface comes up first; the disc starts there, no travel.
        SetDisc(_picker ? LauncherDisc : 1);

        _guard            = guard;
        _guardOverridable = overridable;
        _guardHeld        = 0;
        // Resume behind a "ready" beat so you don't die to an enemy that was one pixel away when you left.
        // The picker needs no countdown (nothing is running), nor does a guard card.
        _readyLeft   = 0;
        if (guard is null && !_picker) { _readyLeft = ArcadeTuning.ResumeReadySeconds; ArcadeSfx.Chrome.Ready(); }
        _accumulator = 0;
        ClearInput();

        Visibility = Visibility.Visible;
        if (!_running)
        {
            _running = true;
            _clock.Restart();
            CompositionTarget.Rendering += OnFrame;
        }
        InvalidateVisual();
    }

    /// <summary>Show the cabinet carousel. The cursor lands on <paramref name="cabinetId"/> when the caller
    /// names one (the launcher resuming on the cabinet that was frontmost at the last dismiss), else on the
    /// live (frozen) game when there is one — "back to what I was playing" is the likeliest pick, and it is
    /// where ○ from a game arrives.
    ///
    /// <para>Screenshots are decoded HERE rather than on the render pump: a corrupt or slow file must cost a
    /// picture, never a frame.</para></summary>
    private void EnterPicker(string? cabinetId = null)
    {
        _picker = true;
        var games = ArcadeCatalog.Games;
        string? landOn = cabinetId ?? _game?.Id;
        _pickerIndex = landOn is null ? 0
            : Math.Max(0, Array.FindIndex(games,
                g => string.Equals(g.Id, landOn, StringComparison.OrdinalIgnoreCase)));
        ArcadeShots.Preload(games.Select(g => g.Id));
        _carousel.Reset(_pickerIndex, games.Length);
        _nav.Reset();
        _launchEntry = null;
        _arriveShot  = null;
        _readyLeft   = 0;   // nothing is running behind the cabinets
    }

    /// <summary>Make <paramref name="entry"/>'s game the live one. Re-opening the SAME game keeps the live
    /// object — that's the freeze-between-invocations promise, and in-process it costs nothing. A different
    /// game is built fresh (resumed from disk), and the PREVIOUS game is frozen to disk first so switching
    /// games through the picker never silently discards a run.</summary>
    private void EnsureGame(ArcadeCatalog.Entry entry)
    {
        // This game's sample bank decodes off-thread now, not on the pump at the first play of each take —
        // the heaviest (a level fanfare) otherwise lands on the very frame it exists for.
        ArcadeSfx.Warm(entry.Id);
        if (_game is not null && string.Equals(_game.Id, entry.Id, StringComparison.OrdinalIgnoreCase))
        {
            _renderer ??= ArcadeRenderers.For(entry.Id);
            RestartIfFinished();
            return;
        }
        SettleOutcome();
        CaptureShot();
        Persist();   // no-op when _game is null
        if (_game is ScriptArcadeGame outgoing) ScriptSessionCoordinator.Release(outgoing);
        _game     = entry.Create();
        _renderer = ArcadeRenderers.For(entry.Id);
        _howToSeen = ArcadeStore.LoadHowToSeen(entry.Id);
        _musicOn   = ArcadeStore.LoadMusicOn();
        _game.SeedHighScore(ArcadeStore.LoadHighScore(entry.Id));
        // Settings BEFORE state: restoring a snapshot must be able to see the game's own preferences (and a
        // setting must never be clobbered by a snapshot that predates it).
        if (ArcadeStore.LoadSettings(entry.Id) is { Length: > 0 } prefs) _game.RestoreSettings(prefs);
        if (ArcadeStore.LoadState(entry.Id) is { Length: > 0 } saved) _game.Restore(saved);
        RestartIfFinished();
    }

    /// <summary>Never resume anyone into a game-over screen — start the next round instead.
    ///
    /// <para>Sits here rather than in each game so the rule holds for games not yet written. Both EnsureGame
    /// paths funnel through it: the LIVE-object path (a run dismissed while dead keeps its object for the
    /// session) and the restored path (a snapshot written finished by an older build or hand-edited).</para></summary>
    private void RestartIfFinished()
    {
        if (_game is null || !_game.IsGameOver) return;
        try { _game.Restart(); }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] restart failed: {ex.Message}"); }
    }

    /// <summary>Freeze past an outcome screen, not on it. Runs right before a game is shot and persisted, so
    /// both record the next playable board: a dead run restarts, and a run whose "game over" is a step in a
    /// campaign (<see cref="IArcadeGame.AcknowledgeOutcome"/>) takes that step now. Dismissing a STUNG card is
    /// the player saying they have read it.</summary>
    private void SettleOutcome()
    {
        if (_game is null) return;
        try
        {
            if (_game.IsGameOver) _game.Restart();
            else _game.AcknowledgeOutcome();
        }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] outcome settle failed: {ex.Message}"); }
    }

    /// <summary>START, while a game is up: open or close the pause menu.
    ///
    /// <para>Refused over the picker and the guard card — neither has a game to pause, and pausing a card
    /// that already blocks play would just be two overlays deep.</para></summary>
    public void TogglePause(bool quiet = false)
    {
        if (_game is null || _picker || _guard is not null) return;
        // START drops the how-to card first and then does its normal job. So from the card over the MENU it
        // lands on the board (both layers gone in one press), and from the card over PLAY it lands on the
        // menu — which is exactly what "I pressed pause" should give you either way. ○ is the button that
        // walks back one layer at a time.
        _howTo = false;
        _paused = !_paused;
        if (!quiet) { if (_paused) ArcadeSfx.Chrome.PauseOpen(); else ArcadeSfx.Chrome.PauseClose(); }
        // Land on Resume, never on Reset: the most likely intent, and the destructive row should never be
        // one careless ✕ away from where the cursor starts.
        _pauseIndex = 0;
        // START while a prompt is up is a way out of it, and an unanswered question must never survive to the
        // next pause — it would reappear over a board the player has since changed their mind about.
        _confirmPrompt = null;
        ClearInput();
        if (!_paused) _readyLeft = ArcadeTuning.ResumeReadySeconds;
        InvalidateVisual();
    }

    public bool Paused => _paused;

    /// <summary>The game's own option rows, plus the HOST's music row for a game that has a bed. Music is
    /// host-owned on purpose: the switch, its per-game memory and the track cycling all live outside the sims,
    /// which know nothing about audio, and a new game inherits the row by having a bed at all.</summary>
    private IReadOnlyList<ArcadePauseOption> PauseOptions
    {
        get
        {
            var own = _game?.PauseOptions ?? [];
            if (_game is null || !ArcadeMusic.HasBed(_game.Id)) return own;
            var row = new ArcadePauseOption(MusicKey, Loc.T(UiText.Arcade.Music),
                                            [Loc.T(UiText.Arcade.Off), Loc.T(UiText.Arcade.On)], _musicOn ? 1 : 0);
            return [.. own, row];
        }
    }

    /// <summary>Reserved pause-row key for the host music switch. A game may not use it.</summary>
    private const string MusicKey = "music";
    private bool _musicOn;

    /// <summary>This game's how-to card, or null if it has none — in which case neither △ nor the menu row
    /// offers one (no-offer contract: nothing advertises a route that dead-ends).</summary>
    private ArcadeHowTo? HowToCard => _game?.HowTo;

    /// <summary>△: open the how-to card, or put it away if it is already up.
    ///
    /// <para>Refused over the picker and the guard card — the picker has no game to explain, and on the guard
    /// card △ is the play-anyway hold, which <see cref="StepGuard"/> owns. Refused too when the game has no
    /// card, which is what keeps △ from being a button that sometimes does nothing.</para>
    ///
    /// <para>⚠ It must TOGGLE, not just open. <see cref="ShowHowTo"/> consumes the input on the way in
    /// (<see cref="ClearInput"/>), so a press arriving while the card is up would be eaten and the card would
    /// have no way out through the button that opened it — the asymmetry a player notices immediately.</para></summary>
    public void ToggleHowTo()
    {
        if (_game is null || _picker || _guard is not null || HowToCard is null) return;
        if (_howTo) BackOut(); else ShowHowTo();
    }

    private void ShowHowTo()
    {
        _howTo = true;
        ArcadeSfx.Chrome.HowToOpen();
        // Reading it is what retires the hint — and it is written through to disk NOW rather than at the next
        // dismiss, so a hard kill can't bring the hint back. See ArcadeStore.MarkHowToSeen.
        if (!_howToSeen)
        {
            _howToSeen = true;
            ArcadeStore.MarkHowToSeen(_game!.Id);
        }
        ClearInput();
        InvalidateVisual();
    }

    /// <summary>○: step back one layer, innermost first. Returns false when nothing here consumed it — which
    /// is the host's signal to close the arcade outright.
    ///
    /// <para>One method rather than the host querying each layer's flag in turn: the PRECEDENCE is the whole
    /// decision, and it belongs next to the state it orders. ○ still never reaches a GAME (see
    /// <c>ArcadeInput</c>) — every layer it walks back is host chrome.</para></summary>
    public bool BackOut()
    {
        if (_howTo)
        {
            _howTo = false;
            ArcadeSfx.Chrome.HowToClose();
            // Back to a game, not to the menu → re-arm the ready beat, exactly as leaving the pause menu
            // does. A card can be up for a while and the board underneath is live the instant it goes.
            if (!_paused) _readyLeft = ArcadeTuning.ResumeReadySeconds;
            ClearInput();
            InvalidateVisual();
            return true;
        }
        if (_paused) { TogglePause(); return true; }
        // The cabinets are a layer under a game the LAUNCHER opened: ○ goes back to them, and the ○ after
        // that leaves. A game opened by its own slice has nothing beneath it and falls through to the host.
        if (_fromLauncher && !_picker && _game is not null)
        {
            // Freeze on the way out, exactly as a dismiss would: the cabinet screen then shows the board
            // just left rather than a stale one — and past any outcome card, which ○ here acknowledges the
            // same as leaving does.
            SettleOutcome();
            CaptureShot();
            Persist();
            EnterPicker();
            AnimateDisc(LauncherDisc, ArcadeTuning.DiscShrinkSeconds);
            SurfaceChanging?.Invoke(false);
            // The board merges into its cabinet's screen on the same clock as the disc shrinks around it.
            // Reduce Motion: neither travels, so nothing to merge.
            _arriveShot = ReduceMotion ? null : ArcadeShots.Get(_game.Id);
            _arriveLeft = _arriveShot is null ? 0 : ArcadeTuning.DiscShrinkSeconds;
            ClearInput();
            InvalidateVisual();
            return true;
        }
        return false;
    }

    /// <summary>Ask, or just do it. Returns true when the change was parked behind a prompt.</summary>
    private bool RequestPauseOption(string key, int choice)
    {
        // The music row is the host's, not the game's: never offer it to the sim, and never let a game's
        // confirm prompt park it.
        if (string.Equals(key, MusicKey, StringComparison.OrdinalIgnoreCase))
        {
            _musicOn = choice == 1;
            ArcadeStore.SaveMusicOn(_musicOn);
            ArcadeSfx.Chrome.OptionChange();
            return false;
        }
        string? prompt = _game?.ConfirmPauseOption(key, choice);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            _game!.ApplyPauseOption(key, choice);
            ArcadeSfx.Chrome.OptionChange();
            // Written straight away rather than at close: a preference is worth more than a wave counter, and
            // a hard kill should not be able to lose one.
            Persist();
            return false;
        }
        ArcadeSfx.Chrome.ConfirmOpen();
        _confirmPrompt = prompt;
        _confirmKey    = key;
        _confirmChoice = choice;
        _confirmYes    = false;
        return true;
    }

    private void StepConfirm()
    {
        _dpadEdges |= StickEdges();
        // Left/right pick the answer; up/down do too, so neither thumb habit is wrong on a two-item choice.
        if ((_dpadEdges & (ArcadeInput.DPad.Left | ArcadeInput.DPad.Up)) != 0) { if (_confirmYes) ArcadeSfx.Chrome.PauseNav(); _confirmYes = false; }
        if ((_dpadEdges & (ArcadeInput.DPad.Right | ArcadeInput.DPad.Down)) != 0) { if (!_confirmYes) ArcadeSfx.Chrome.PauseNav(); _confirmYes = true; }

        if (_crossEdge)
        {
            if (!_confirmYes) ArcadeSfx.Chrome.ConfirmNo();
            else if (_confirmKey == ResetKey) { ArcadeSfx.Chrome.Reset(); RestartLiveGame(); _confirmPrompt = null; TogglePause(quiet: true); }
            else { _game!.ApplyPauseOption(_confirmKey, _confirmChoice); Persist(); ArcadeSfx.Chrome.ConfirmYes(); }
            _confirmPrompt = null;
        }

        _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
        _dpadEdges = ArcadeInput.DPad.None;
        InvalidateVisual();
    }

    /// <summary>The card is a read, not a menu — there is nothing on it to aim at, so nothing moves a cursor.
    /// ✕ puts it away as well as ○ and △: ✕ is the arcade's "yes, done" everywhere else, and a card with only
    /// one exit is a card people press every button on.</summary>
    private void StepHowTo()
    {
        if (_crossEdge) BackOut();
        _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
        _dpadEdges = ArcadeInput.DPad.None;
        InvalidateVisual();
    }

    private void StepPause()
    {
        if (_confirmPrompt is not null) { StepConfirm(); return; }

        // △ is NOT read here. ToggleHowTo already handled it at the host edge — it works from the menu as
        // well as from play, so the row is the discoverable route and the button stays the fast one.
        var options = PauseOptions;
        var fixedRows = FixedRows;
        int rows = fixedRows.Length + options.Count;

        // D-pad or stick for the cursor. The stick contributes EDGES past a deep threshold (StickEdges), so a
        // resting thumb never walks the menu; on a pause screen precision beats speed.
        _dpadEdges |= StickEdges();
        if ((_dpadEdges & ArcadeInput.DPad.Up) != 0) { _pauseIndex = (_pauseIndex - 1 + rows) % rows; ArcadeSfx.Chrome.PauseNav(); }
        if ((_dpadEdges & ArcadeInput.DPad.Down) != 0) { _pauseIndex = (_pauseIndex + 1) % rows; ArcadeSfx.Chrome.PauseNav(); }

        int step = ((_dpadEdges & ArcadeInput.DPad.Right) != 0 ? 1 : 0)
                 - ((_dpadEdges & ArcadeInput.DPad.Left) != 0 ? 1 : 0);
        if (step != 0 && _pauseIndex >= fixedRows.Length)
        {
            ArcadePauseOption option = options[_pauseIndex - fixedRows.Length];
            int next = Math.Clamp(option.Selected + step, 0, option.Choices.Length - 1);
            if (next != option.Selected && RequestPauseOption(option.Key, next))
            {
                // The prompt owns the frame from here — consume the edges so the ✕ that is still down (or the
                // next d-pad tap) can't answer a question that only just appeared.
                _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
                _dpadEdges = ArcadeInput.DPad.None;
                InvalidateVisual();
                return;
            }
        }

        if (_crossEdge)
        {
            if (_pauseIndex < fixedRows.Length)
            {
                switch (fixedRows[_pauseIndex])
                {
                    case FixedRow.Resume: ArcadeSfx.Chrome.Select(); TogglePause(); break;
                    // The card is a LAYER on the menu, not a replacement for it: backing out of it with ○
                    // returns here rather than to the board, so reading the rules doesn't also un-pause you.
                    case FixedRow.HowTo:  ArcadeSfx.Chrome.Select(); ShowHowTo(); break;
                    // Reset throws the run away, so it asks first like any destructive option; the prompt's YES
                    // path in StepConfirm does the restart.
                    case FixedRow.Reset:
                        ArcadeSfx.Chrome.ConfirmOpen();
                        _confirmPrompt = Loc.T(UiText.Arcade.EndsRun);
                        _confirmKey    = ResetKey;
                        _confirmChoice = 0;
                        _confirmYes    = false;
                        break;
                }
            }
            else
            {
                // ✕ on an option row cycles it, so the menu is usable without discovering left/right.
                ArcadePauseOption option = options[_pauseIndex - fixedRows.Length];
                if (RequestPauseOption(option.Key, (option.Selected + 1) % option.Choices.Length))
                {
                    _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
                    _dpadEdges = ArcadeInput.DPad.None;
                    InvalidateVisual();
                    return;
                }
            }
        }

        _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
        _dpadEdges = ArcadeInput.DPad.None;
        InvalidateVisual();
    }

    /// <summary>Start the current game's round over without leaving.
    ///
    /// <para>Refuses while the picker or a guard card is up: neither has a round to restart, and a hold there
    /// would silently reset whatever game happened to be live behind them. The ready beat is re-armed so a
    /// fresh board doesn't take input from the thumb still finishing the hold.</para></summary>
    public void RestartLiveGame()
    {
        if (_game is null || _picker || _guard is not null) return;
        try { _game.Restart(); }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] restart failed: {ex.Message}"); return; }
        _readyLeft = ArcadeTuning.ResumeReadySeconds;
        _accumulator = 0;
        ClearInput();
        InvalidateVisual();
    }

    /// <summary>Swap a running game for the guard card (isolation lost mid-session). The game freezes exactly
    /// where it was.</summary>
    public void ShowGuard(Arcade.GuardCopy copy, bool overridable)
    {
        if (!_running) return;
        _guard = copy;
        ArcadeSfx.Chrome.GuardShow();
        _guardOverridable = overridable;
        _guardHeld = 0;
        ClearInput();
        InvalidateVisual();
    }

    /// <summary>Isolation came back — drop the card and resume behind the ready beat.</summary>
    public void ClearGuard(bool fromOverride = false)
    {
        if (_guard is null) return;
        if (fromOverride) ArcadeSfx.Chrome.GuardOverride(); else ArcadeSfx.Chrome.GuardClear();
        _guard     = null;
        _guardHeld = 0;
        _readyLeft = ArcadeTuning.ResumeReadySeconds;
        ClearInput();
        InvalidateVisual();
    }

    /// <summary>Freeze and leave. Detaches the pump and persists the snapshot — dismissing IS the save
    /// checkpoint (see <see cref="ArcadeStore"/>).</summary>
    public void Close()
    {
        if (_running)
        {
            _running = false;
            CompositionTarget.Rendering -= OnFrame;
            // Close runs from inside OnFrame's catch: nothing here may throw past it.
            try { ArcadeSfx.Chrome.Close(); } catch (Exception ex) { Trace.WriteLine($"[Arcade] close chime failed: {ex.Message}"); }
            try { ArcadeMusic.Stop(); }       catch (Exception ex) { Trace.WriteLine($"[Arcade] music stop failed: {ex.Message}"); }
            _clock.Stop();
        }
        Visibility = Visibility.Collapsed;
        // Never leave the pause menu or the how-to card armed behind a closed game — re-opening would land
        // straight back in it, over a board the player expected to be playing.
        _paused = false;
        _howTo = false;
        _pauseIndex = 0;
        _confirmPrompt = null;
        SettleOutcome();
        CaptureShot();   // before Release: a script game's buffer lives in its session
        CaptureLauncherShot();
        Persist();
        // Where the Arcade Launcher resumes to next time: this game, or the cabinets if that's what was up.
        // ⚠ Only a LAUNCHER session writes it. A game reached from its own slice is a trip to that one game,
        // not a move around the arcade, and must not redirect where the launcher comes back to.
        if (_fromLauncher) ArcadeStore.SaveLastSurface(_picker ? null : _game?.Id, _picker ? SelectedEntry()?.Id : _game?.Id);
        // A script game's helper dies with the arcade: kv (just persisted) is the only state that
        // survives a dismiss — reopening starts a fresh session and restores it. The live object is
        // kept like any other game's, so the coordinator relaunches lazily on the next frame.
        if (_game is ScriptArcadeGame sg)
        {
            try { ScriptSessionCoordinator.Release(sg); }
            catch (Exception ex) { Trace.WriteLine($"[Arcade] script session release failed: {ex.Message}"); }
        }
        ClearInput();
        _carousel.Release();
    }

    /// <summary>Screenshot the live game for the picker's cabinet screen. Only from the two places a game is
    /// frozen (dismiss, and the swap in <see cref="EnsureGame"/>) — never from <see cref="Persist"/>, which
    /// also runs on every pause-menu change. A finished run keeps its previous shot rather than saving the
    /// game-over screen, matching the snapshot rule in <see cref="Persist"/>.</summary>
    private void CaptureShot()
    {
        if (_game is null || _renderer is null || _picker || _game.IsGameOver) return;
        try { ArcadeShots.Capture(_game.Id, _renderer, _game); }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] shot capture failed: {ex.Message}"); }
    }

    /// <summary>Screenshot the carousel for the Arcade Launcher slice's hub — the cabinets as they were
    /// left, the way a game's slice shows its board. Taken whenever the launcher is left: for a game, or
    /// on dismiss. The carousel alone, so the hub never carries a launch or a return in flight.</summary>
    private void CaptureLauncherShot()
    {
        if (!_picker) return;
        var games = ArcadeCatalog.Games;
        int index = _pickerIndex;
        try
        {
            ArcadeShots.Capture(ArcadeShots.LauncherId, (dc, c, field) =>
                _carousel.Draw(dc, c, field, field / (1 - ArcadeChrome.BezelFrac), 1.0, games, index));
        }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] launcher shot failed: {ex.Message}"); }
    }

    /// <summary>Write the frozen state out. Called on close and at app teardown.</summary>
    public void Persist()
    {
        if (_game is null) return;
        try
        {
            // A finished run has nothing worth resuming — clear the snapshot but keep the best score, so the
            // next open starts a clean game instead of re-showing GAME OVER.
            ArcadeStore.Save(_game.Id, _game.IsGameOver ? null : _game.Serialize(), _game.HighScore,
                             _game.SerializeSettings());
        }
        catch (Exception ex) { Trace.WriteLine($"[Arcade] persist failed: {ex.Message}"); }
    }

    private void ClearInput()
    {
        _stickX = _stickY = 0;
        _crossDown = _triangleDown = _squareDown = false;
        _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
        _dpadHeld = _dpadEdges = ArcadeInput.DPad.None;
        _stickHeld = ArcadeInput.DPad.None;
    }

    // ── Input funnel (called from App's controller handlers) ───────────────────

    public void SetStick(float x, float y) { _stickX = x; _stickY = y; }

    /// <summary>The stick as d-pad presses for the pause layer: a direction arms past 0.60 of deflection and
    /// re-arms only after falling under 0.35, so one push is one step whatever the thumb does in between. Screen
    /// convention: StickY grows downward (ArcadeInput.ScreenAngle).</summary>
    private ArcadeInput.DPad StickEdges()
    {
        const float on = 0.60f, off = 0.35f;
        var next = ArcadeInput.DPad.None;
        if (-_stickY >= on || (-_stickY >= off && (_stickHeld & ArcadeInput.DPad.Up)    != 0)) next |= ArcadeInput.DPad.Up;
        if ( _stickY >= on || ( _stickY >= off && (_stickHeld & ArcadeInput.DPad.Down)  != 0)) next |= ArcadeInput.DPad.Down;
        if (-_stickX >= on || (-_stickX >= off && (_stickHeld & ArcadeInput.DPad.Left)  != 0)) next |= ArcadeInput.DPad.Left;
        if ( _stickX >= on || ( _stickX >= off && (_stickHeld & ArcadeInput.DPad.Right) != 0)) next |= ArcadeInput.DPad.Right;
        var edges = next & ~_stickHeld;
        _stickHeld = next;
        return edges;
    }

    /// <summary>D-pad, from the controller's raw NIBBLE (0 = up, then clockwise in eighths; 8 = centred).
    /// Diagonals deliberately raise BOTH of their directions — on a grid that's the natural reading, and a
    /// game that only wants one can take whichever it cares about.
    ///
    /// <para>Converted to edges here rather than in a game: the nibble is level state, and every game would
    /// otherwise reimplement the same press-detection. Auto-repeat is likewise a game's business — Kabloom
    /// has its own, tuned to its grid.</para></summary>
    public void SetDPad(int nibble)
    {
        var next = nibble switch
        {
            0 => ArcadeInput.DPad.Up,
            1 => ArcadeInput.DPad.Up    | ArcadeInput.DPad.Right,
            2 => ArcadeInput.DPad.Right,
            3 => ArcadeInput.DPad.Right | ArcadeInput.DPad.Down,
            4 => ArcadeInput.DPad.Down,
            5 => ArcadeInput.DPad.Down  | ArcadeInput.DPad.Left,
            6 => ArcadeInput.DPad.Left,
            7 => ArcadeInput.DPad.Left  | ArcadeInput.DPad.Up,
            _ => ArcadeInput.DPad.None,
        };
        // Newly-pressed directions only: what's down now and wasn't before.
        _dpadEdges |= next & ~_dpadHeld;
        _dpadHeld = next;
    }

    public void SetCross(bool down)    { if (down && !_crossDown)    _crossEdge    = true; _crossDown    = down; }
    public void SetTriangle(bool down) { if (down && !_triangleDown) _triangleEdge = true; _triangleDown = down; }
    public void SetSquare(bool down)   { if (down && !_squareDown)   _squareEdge   = true; _squareDown   = down; }
    /// <summary>The Select press for <see cref="ArcadeDebug.LevelSkip"/>; press-only, no level tracked.</summary>
    public void SetSkip() { _skipEdge = true; }

    /// <summary>True while the cabinets are up rather than a game — the window position is a GAME's setting.</summary>
    public bool IsPicker => _picker;

    /// <summary>Raised as the disc starts travelling between surfaces: true = a cabinet was picked and the game's
    /// disc is growing (ArcadePickerTuning.LaunchSeconds), false = a game is shrinking back to the cabinets
    /// (ArcadeTuning.DiscShrinkSeconds). The shell slides the window on the same clock.</summary>
    public event Action<bool>? SurfaceChanging;

    // ── Frame pump ────────────────────────────────────────────────────────────

    private void OnFrame(object? sender, EventArgs e)
    {
        // The picker steps with no game live: a fresh process can land here straight from a slice.
        if (!_running || (_game is null && !_picker)) return;
        try
        {
            // Clamped once here for every consumer: a multi-second stall (shader compile, display-mode change)
            // must not teleport the disc or credit the △ guard hold with time nobody held it.
            double dt = Math.Min(_clock.Elapsed.TotalSeconds, ArcadeTuning.StepSeconds * ArcadeTuning.MaxStepsPerFrame);
            _clock.Restart();

            // The bed follows the game on screen and ducks under anything that is not play. Synced before the
            // branch so a paused game keeps its music and a finished playlist track still hands over.
            SyncMusic();
            StepDisc(dt);

            // Repaint only when something on screen moves. The pause menu, the how-to card and an idle
            // guard card are static pictures whose every change already calls InvalidateVisual() at the
            // transition — repainting them at display rate re-ran the whole game renderer over a running
            // game to produce the same pixels. The disc transition and the ready fade are motion.
            bool moving = _discLeft > 0;
            if (_guard is not null) { StepGuard(dt); moving |= _guardHeld > 0; }
            else if (_howTo) StepHowTo();
            else if (_paused) StepPause();
            else if (_picker) { StepPicker(dt); moving = true; }
            else if (_readyLeft > 0)
            {
                _readyLeft = Math.Max(0, _readyLeft - dt);
                // Presses made during the beat must not fire on the handover frame — the beat exists so the
                // player can read the board, and a ✕ mashed to hurry it is the same surprise from the other side.
                _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
                _dpadEdges = ArcadeInput.DPad.None;
                moving = true;
            }
            else { StepGame(dt); moving = true; }

            if (moving) InvalidateVisual();
        }
        catch (Exception ex) { Fail("frame", ex); }
    }

    /// <summary>Keep the music bed in step with what is on screen: which game it is, whether the player
    /// wants music, and whether to duck.
    ///
    /// <para>⚠ NO PROGRESSION GOES IN HERE. The bed cycles as the run goes, decoupled from the game's stage:
    /// there is nothing about a run for this to read. Both remaining policies are self-driving — a Loop
    /// repeats and a Playlist hands over when a track ends.</para></summary>
    private void SyncMusic()
    {
        if (_picker || _game is null) { ArcadeMusic.Stop(); return; }
        bool ducked = _paused || _howTo || _confirmPrompt is not null || _guard is not null || _readyLeft > 0;
        ArcadeMusic.Sync(_game.Id, _musicOn, ducked);
    }

    private void StepGuard(double dt)
    {
        if (!_guardOverridable) { _guardHeld = 0; return; }
        if (!_triangleDown) { _guardHeld = 0; _triangleEdge = false; return; }

        _guardHeld += dt;
        if (_guardHeld * 1000 < Arcade.OverrideHoldMs) return;

        _overrideAccepted = true;
        _guardHeld = 0;
        _triangleEdge = false;
        ClearGuard(fromOverride: true);
        OverrideGranted?.Invoke();
    }

    /// <summary>The picker: a carousel of cabinets stepped LEFT/RIGHT by the stick's horizontal axis or the
    /// d-pad, one cabinet per flick with auto-repeat while held (<see cref="ArcadeCarouselNav"/>). ✕ launches
    /// the cabinet the badge names, even mid-swing — the index is authoritative the instant a step lands. ○
    /// never reaches here (the host owns it).</summary>
    private void StepPicker(double dt)
    {
        var games = ArcadeCatalog.Games;
        _carousel.Step(dt, ReduceMotion);
        if (_arriveShot is not null)
        {
            _arriveLeft -= dt;
            if (_arriveLeft <= 0) _arriveShot = null;
        }
        if (games.Length == 0)
        {
            _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
            _dpadEdges = ArcadeInput.DPad.None;
            return;
        }

        if (_launchEntry is { } launching)
        {
            // The screen is growing into the game; input is ignored until it is live. Reduce Motion skips it.
            _launchLeft -= dt;
            if (_launchLeft <= 0 || ReduceMotion) CompleteLaunch(launching);
            _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
            _dpadEdges = ArcadeInput.DPad.None;
            return;
        }

        int d = _nav.Step(_stickX,
                          (_dpadHeld & ArcadeInput.DPad.Left)  != 0,
                          (_dpadHeld & ArcadeInput.DPad.Right) != 0, dt);
        if (d != 0)
        {
            _pickerIndex = ((_pickerIndex + d) % games.Length + games.Length) % games.Length;
            _carousel.Rotate(d);
            ArcadeSfx.Chrome.PickerNav();
        }
        _dpadEdges = ArcadeInput.DPad.None;

        if (!_crossEdge) { _triangleEdge = _squareEdge = false; return; }
        _crossEdge = false;

        var entry = games[Math.Clamp(_pickerIndex, 0, games.Length - 1)];
        _launchEntry = entry;
        _launchLeft  = ArcadePickerTuning.LaunchSeconds;
        // The hub's picture is the cabinets at rest, taken before the screen lifts off.
        CaptureLauncherShot();
        // The window grows to the game's size on the same clock, so the two arrive together.
        AnimateDisc(1, ArcadePickerTuning.LaunchSeconds);
        SurfaceChanging?.Invoke(true);
        ClearInput();
        ArcadeSfx.Chrome.PickerSelect();
    }

    /// <summary>The screen has filled the disc: make the chosen game live behind its ready beat.</summary>
    private void CompleteLaunch(ArcadeCatalog.Entry entry)
    {
        _launchEntry = null;
        SetDisc(1);   // lands the grow exactly, whatever the frame timing left it at
        EnsureGame(entry);
        _picker    = false;
        _readyLeft = ArcadeTuning.ResumeReadySeconds;
        ClearInput();
        ArcadeSfx.Chrome.Ready();
        Trace.WriteLine($"[Arcade] picker → {entry.Id}");
    }

    private void StepGame(double dt)
    {
        _accumulator += dt;
        double step = ArcadeTuning.StepSeconds;

        // A backlog longer than the cap is DROPPED, not simulated. After the overlay is starved (a game
        // finishing a shader compile, a display mode change), catching up honestly would fast-forward the
        // player through several seconds of enemies they never saw.
        int budget = ArcadeTuning.MaxStepsPerFrame;
        if (_accumulator > step * budget) _accumulator = step * budget;

        int steps = 0;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        while (_accumulator >= step && budget-- > 0)
        {
            _game!.Step(BuildInput(), step);
            _accumulator -= step;
            steps++;
            // Edges are consumed by the FIRST step of the frame only — otherwise one press would register on
            // every step in the backlog (up to 12 shots from one tap).
            _crossEdge = _triangleEdge = _squareEdge = _skipEdge = false;
            _dpadEdges = ArcadeInput.DPad.None;
        }
        _lastSteps  = steps;
        _lastStepMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

        // Cues are read once per RENDERED frame, so several sim steps' worth coalesce into one sound. At
        // 120 Hz the alternative pushes a stack of identical samples into the mixer's 16-voice cap — after
        // which every later sound is silently dropped (see docs/SOUND.md).
        PlayGameCues();
    }

    /// <summary>Dispatch the frame's cues to sounds, per game. Every game keeps its own cue vocabulary
    /// (the sim must not know audio exists) and its own sample bank in <see cref="ArcadeSfx"/>; the host
    /// chrome shares one vocabulary across all of them. Within a game, cues group into FAMILIES where the
    /// heaviest speaks and the rest stay quiet — a game over is not also a life lost, a cascade is not also
    /// a merge. Deliberately silent: Connate.Move (per frame while steering — a 60 Hz tick storm; its
    /// feedback is visual), Internode.Land and Internode.Miss (the greyed orb carries it).</summary>
    private void PlayGameCues()
    {
        switch (_game)
        {
            case Kabloom k:          PlayKabloom(k, k.TakeCues()); break;
            case Connate c:          PlayConnate(c, c.TakeCues()); break;
            case PetalPop p:         PlayPetalPop(p, p.TakeCues()); break;
            case Internode f:        PlayInternode(f, f.TakeCues()); break;
            case ScriptArcadeGame s:
                // Drop-in games speak a fixed nine-name cue vocabulary onto the GENERIC bank. The names were
                // VALIDATED at the pipe boundary (count + length); an unknown name is silent, never an error.
                foreach (var cue in s.TakeCues()) ArcadeSfx.Generic.Cue(cue);
                break;
        }
    }

    private static void PlayKabloom(Kabloom k, Kabloom.Cue c)
    {
        if (c == Kabloom.Cue.None) return;
        // Outcome family: the campaign's end outranks a board clear, which outranks the bee.
        if ((c & Kabloom.Cue.CampaignComplete) != 0) ArcadeSfx.Kabloom.CampaignComplete();
        else if ((c & Kabloom.Cue.Cleared) != 0) ArcadeSfx.Kabloom.Cleared();
        else if ((c & Kabloom.Cue.Mine) != 0) ArcadeSfx.Kabloom.Mine();
        // Reveal family: the cascade is Kabloom's big moment and scales with how much it opened.
        if ((c & Kabloom.Cue.Bloom) != 0) ArcadeSfx.Kabloom.Bloom(k.LastCascadeSize);
        else if ((c & Kabloom.Cue.Reveal) != 0) ArcadeSfx.Kabloom.Reveal();
        // Progression family: the ordinary advance, or the fall-back after a bee.
        if ((c & Kabloom.Cue.LevelUp) != 0) ArcadeSfx.Kabloom.LevelUp();
        else if ((c & Kabloom.Cue.Continue) != 0) ArcadeSfx.Kabloom.Continue();
        if ((c & Kabloom.Cue.Growth) != 0) ArcadeSfx.Kabloom.Growth();
        if ((c & Kabloom.Cue.BeeDeparts) != 0) ArcadeSfx.Kabloom.BeeDeparts();
        // A level's gems land one after another; the run of them climbs.
        if ((c & Kabloom.Cue.Diamond) != 0) ArcadeSfx.Kabloom.Diamond(k.LevelDiamondsCollected);
        if ((c & Kabloom.Cue.NewBest) != 0) ArcadeSfx.Kabloom.NewBest();
        if ((c & Kabloom.Cue.Flag) != 0) ArcadeSfx.Kabloom.Flag();
        else if ((c & Kabloom.Cue.Unflag) != 0) ArcadeSfx.Kabloom.Unflag();
        if ((c & Kabloom.Cue.Guarded) != 0) ArcadeSfx.Kabloom.Guarded();
        // Kabloom.Cue.Focus: deliberately silent — moving the reticle is not an action, and its click is
        // now what OPENING a petal sounds like.
    }

    private static void PlayConnate(Connate c, Connate.Cue cc)
    {
        if (cc == Connate.Cue.None) return;
        if ((cc & Connate.Cue.GameOver) != 0) ArcadeSfx.Connate.GameOver();
        else if ((cc & Connate.Cue.Warning) != 0) ArcadeSfx.Connate.Warning();
        // Payoff family: an emptied board outranks a bomb, which outranks the cascade it caused.
        if ((cc & Connate.Cue.BoardClear) != 0) ArcadeSfx.Connate.BoardClear();
        else if ((cc & Connate.Cue.BombExplode) != 0) ArcadeSfx.Connate.BombExplode();
        else if ((cc & Connate.Cue.Chain) != 0) ArcadeSfx.Connate.Chain(c.ChainDepth);
        else if ((cc & Connate.Cue.Merge) != 0) ArcadeSfx.Connate.Merge();
        if ((cc & Connate.Cue.Fire) != 0) ArcadeSfx.Connate.Fire();
        if ((cc & Connate.Cue.BombEarned) != 0) ArcadeSfx.Connate.BombEarned();
        if ((cc & Connate.Cue.NewBest) != 0) ArcadeSfx.Connate.NewBest();
        if ((cc & Connate.Cue.Recover) != 0) ArcadeSfx.Connate.Recover();
        if ((cc & Connate.Cue.ComboStep) != 0) ArcadeSfx.Connate.ComboStep(c.ComboCount);
        // Collecting is how Connate scores, so its blip is a till: the sim throttles arrivals to a countable
        // rate, and a long stream of them is what a big haul sounds like.
        if ((cc & Connate.Cue.Collect) != 0) ArcadeSfx.Connate.Collect();
        if ((cc & Connate.Cue.GarbageCleared) != 0) ArcadeSfx.Connate.GarbageCleared();
        else if ((cc & Connate.Cue.GarbageArrive) != 0) ArcadeSfx.Connate.GarbageArrive();
        if ((cc & Connate.Cue.Guarded) != 0) ArcadeSfx.Connate.Guarded();
        // Connate.Cue.Move: deliberately silent (see the method remark).
    }

    private static void PlayPetalPop(PetalPop p, PetalPop.Cue pc)
    {
        if (pc == PetalPop.Cue.None) return;
        // Loss family: the run, a life, or just a ball.
        if ((pc & PetalPop.Cue.GameOver) != 0) ArcadeSfx.PetalPop.GameOver();
        else if ((pc & PetalPop.Cue.LifeLost) != 0) ArcadeSfx.PetalPop.LifeLost();
        else if ((pc & PetalPop.Cue.Lost) != 0) ArcadeSfx.PetalPop.Lost();
        // Progress family: the win, a level (the gold core is its payoff), a stage graduation.
        if ((pc & PetalPop.Cue.Win) != 0) ArcadeSfx.PetalPop.Win();
        else if ((pc & PetalPop.Cue.LevelClear) != 0) ArcadeSfx.PetalPop.LevelClear();
        else if ((pc & PetalPop.Cue.ExtraLife) != 0) ArcadeSfx.PetalPop.ExtraLife();
        // Impact family: the smash and the split are the two big moments; a pop climbs with the combo.
        // Cue.Powerup always rides with Split and is deliberately not mapped on its own.
        if ((pc & PetalPop.Cue.Smash) != 0) ArcadeSfx.PetalPop.Smash();
        else if ((pc & PetalPop.Cue.Split) != 0) ArcadeSfx.PetalPop.Split();
        else if ((pc & PetalPop.Cue.Brick) != 0) ArcadeSfx.PetalPop.Pop(p.Combo);
        else if ((pc & PetalPop.Cue.BrickTough) != 0) ArcadeSfx.PetalPop.Chip();
        // Nine balls on eight paddles can bounce in one frame; the flag coalesces them and ArcadeSfx throttles.
        // A corner bumper is a paddle that never moves, so it speaks with the paddle.
        if ((pc & (PetalPop.Cue.PaddleHit | PetalPop.Cue.Bumper)) != 0 && (pc & PetalPop.Cue.Smash) == 0)
            ArcadeSfx.PetalPop.PaddleHit();
        if ((pc & PetalPop.Cue.CorePing) != 0) ArcadeSfx.PetalPop.CorePing();
        if ((pc & PetalPop.Cue.SmashArm) != 0) ArcadeSfx.PetalPop.SmashArm();
        if ((pc & PetalPop.Cue.Serve) != 0) ArcadeSfx.PetalPop.Serve();
        if ((pc & PetalPop.Cue.ComboShout) != 0) ArcadeSfx.PetalPop.ComboShout(p.Combo);
        if ((pc & PetalPop.Cue.NewBest) != 0) ArcadeSfx.PetalPop.NewBest();
    }

    private static void PlayInternode(Internode f, Internode.Cue fc)
    {
        if (fc == Internode.Cue.None) return;
        // Setback family: a fall or a missed gate holds the run at this section; a mine only costs orbs.
        if ((fc & Internode.Cue.Fell) != 0) ArcadeSfx.Internode.Fell();
        else if ((fc & Internode.Cue.CheckpointFail) != 0) ArcadeSfx.Internode.CheckpointFail();
        else if ((fc & Internode.Cue.Mine) != 0) ArcadeSfx.Internode.Mine();
        // Gate family: a new stage says more than a passed gate.
        if ((fc & Internode.Cue.StageUp) != 0) ArcadeSfx.Internode.StageUp();
        else if ((fc & Internode.Cue.Checkpoint) != 0) ArcadeSfx.Internode.Checkpoint();
        // The payout's flight is its own voice under whichever gate sound won above.
        if ((fc & Internode.Cue.BankFlight) != 0) ArcadeSfx.Internode.BankFlight();
        // Catch family: token planes are authored MinTokenSpacingSeconds apart, the sim-side throttle.
        if ((fc & Internode.Cue.Bonus) != 0) ArcadeSfx.Internode.Bonus();
        // The turn to gold rides the catch that armed it, in place of that catch's own tick: one sound per orb.
        else if ((fc & Internode.Cue.GoldTurn) != 0) ArcadeSfx.Internode.GoldTurn();
        else if ((fc & Internode.Cue.TokenElevated) != 0) ArcadeSfx.Internode.TokenElevated();
        else if ((fc & Internode.Cue.Token) != 0) ArcadeSfx.Internode.Token();
        // Air family.
        if ((fc & Internode.Cue.Jump) != 0) ArcadeSfx.Internode.Jump();
        if ((fc & Internode.Cue.RimExit) != 0) ArcadeSfx.Internode.RimExit();
        if ((fc & Internode.Cue.Respawn) != 0) ArcadeSfx.Internode.Respawn();
        if ((fc & Internode.Cue.Begin) != 0) ArcadeSfx.Internode.Begin();
        if ((fc & Internode.Cue.NewBest) != 0) ArcadeSfx.Internode.NewBest();
        // Internode.Cue.Land, Miss and OverTopLand: deliberately silent — a landing needs no announcement.
    }

    private ArcadeInput BuildInput() => new(
        _stickX, _stickY, _crossDown, _triangleDown, _squareDown,
        _crossEdge, _triangleEdge, _squareEdge, (int)_dpadEdges, _skipEdge);

    // ── Render ────────────────────────────────────────────────────────────────

    /// <summary>The paint half of the pump gets the same guard as the step half: every game renderer, the
    /// picker and the chrome run here, 60 times a second over someone's game, and an unhandled throw from
    /// <c>OnRender</c> reaches the dispatcher and takes the app down — which lifts the cloak.</summary>
    protected override void OnRender(DrawingContext dc)
    {
        if (!_running || ActualWidth <= 0) return;
        if (_game is null && !_picker) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { RenderCore(dc); }
        catch (Exception ex) { Fail("render", ex); }
        finally
        {
            // The arcade's equivalent of the wheel's `[Render] slow frame` line: a frame whose PAINT took
            // longer than a 60 Hz budget, with the sim's share of the same frame beside it, so "should this
            // game's board be baked" is a number read off a couch session's log rather than a guess. One line
            // a second at most; the values named are what decides where the cost is.
            double paintMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            if (paintMs > SlowFrameMs && Environment.TickCount64 - _slowFrameTraceTick > 1000)
            {
                _slowFrameTraceTick = Environment.TickCount64;
                string what = _picker ? "picker" : _game?.Id ?? "?";
                string state = _guard is not null ? "guard" : _howTo ? "howto" : _paused ? "paused"
                             : _readyLeft > 0 ? "ready" : _picker ? "idle" : "live";
                Trace.WriteLine($"[Arcade] slow frame {paintMs:F1}ms paint, {_lastStepMs:F1}ms sim ({_lastSteps} steps) " +
                                $"game={what} state={state} disc={Radius:F0}px");
            }
            _lastSteps = 0; _lastStepMs = 0;   // the sim's share belongs to the frame it ran in
        }
    }

    private const double SlowFrameMs = 18.0;
    private long _slowFrameTraceTick;
    private int _lastSteps;
    private double _lastStepMs;

    private void RenderCore(DrawingContext dc)
    {
        double radius = Radius;
        var c   = new Point(ActualWidth / 2, ActualHeight / 2);
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var accent = (_picker ? _carousel.Accent(SelectedEntry()) : _renderer?.Accent) ?? ArcadeChrome.Ink;
        bool covered = !_picker && _game is not null && (_renderer?.CoversField ?? false);
        double field = ArcadeChrome.DrawFrame(dc, c, radius, accent, covered);

        if (_picker)
        {
            _carousel.Draw(dc, c, field, radius, ppd, ArcadeCatalog.Games, _pickerIndex);
            if (_launchEntry is { } launching)
                _carousel.DrawDeparture(dc, c, field, ArcadeShots.Get(launching.Id), LaunchProgress);
            else if (_arriveShot is { } arriving)
            {
                double progress = 1 - Math.Clamp(_arriveLeft / Math.Max(1e-3, ArcadeTuning.DiscShrinkSeconds), 0, 1);
                _carousel.DrawArrival(dc, c, field, arriving, progress);
            }
        }
        else if (_game is not null)
        {
            // Clip to the playfield so nothing a game draws can spill over the bezel — the round window is a
            // hard boundary, not a suggestion.
            var clip = new EllipseGeometry(c, field, field);
            clip.Freeze();
            dc.PushClip(clip);

            // The disc TWISTS (and punches) on impact. A linear shake in a round window reads as the window
            // being broken; rotation and a scale kick read as the disc itself taking the hit. Both values are
            // the game's own (IArcadeGame.DiscTwist / DiscPunch), decayed by the sim to exactly 0 / 1.
            double twist = _game.DiscTwist, punch = _game.DiscPunch;
            bool kicked = Math.Abs(twist) > 1e-4 || Math.Abs(punch - 1) > 1e-4;
            if (kicked)
            {
                var kick = new TransformGroup();
                kick.Children.Add(new ScaleTransform(punch, punch, c.X, c.Y));
                kick.Children.Add(new RotateTransform(twist * 180 / Math.PI, c.X, c.Y));
                kick.Freeze();
                dc.PushTransform(kick);
            }

            _renderer?.Draw(dc, c, field, _game, ppd);

            if (kicked) dc.Pop();
            dc.Pop();   // clip
        }

        if (_guard is { } g)
            ArcadeChrome.DrawGuard(dc, c, radius, g,
                _guardOverridable ? _guardHeld * 1000 / Arcade.OverrideHoldMs : -1, ppd);
        else if (_howTo && HowToCard is { } card) DrawHowTo(dc, c, field, accent, card, ppd);
        else if (_paused) DrawPause(dc, c, field, accent, ppd);
        else if (!_picker && _readyLeft > 0)
            ArcadeChrome.DrawReady(dc, c, radius, _readyLeft, ppd);
        // The one surviving on-screen button prompt, drawn LAST and outside the playfield clip so it can sit in
        // the bezel — see DrawHowToHint. △ HOW TO PLAY until the card has been opened once, START MENU after.
        else if (!_picker && HowToCard is not null) DrawHowToHint(dc, c, radius, ppd, _howToSeen);
    }

    /// <summary>A how-to line laid out with its button tokens ({cross} and friends) reserved as badge slots
    /// (<see cref="ArcadeChrome.ReserveBadges"/>): the text to draw, and the badges to draw over it. The sim
    /// writes tokens rather than glyphs for the same reason <c>HelpContent</c> does: <c>Core</c> is WPF-free
    /// and knows nothing about which pad is plugged in, and the setting can change while a game sits frozen.
    /// Resolved per draw, which is free — the renderers redraw every frame anyway.</summary>
    internal readonly record struct HowToLine(FormattedText Text, ArcadeChrome.InlineBadge[] Badges);

    private static HowToLine LayOutLine(string s, double size, Brush ink, double ppd, TextAlignment align, double maxWidth)
    {
        var slots = new List<ArcadeChrome.InlineBadge>();
        string reserved = ArcadeChrome.ReserveBadges(s, size, ppd, slots);
        return new HowToLine(ArcadeChrome.Text(reserved, size, ink, ppd, align, maxWidth), [.. slots]);
    }

    /// <summary>Where a how-to card's parts land, for a playfield of <paramref name="field"/> radius.
    /// <see cref="Top"/> and <see cref="Bottom"/> are offsets from the disc's CENTRE.
    ///
    /// <para>Split out from the drawing so the fit check can measure the card the app actually draws.
    /// The bullets are author-editable copy with no scrolling and no second page behind them, so "does it
    /// still fit in the circle" has to be answerable mechanically — a harness that re-derived these numbers
    /// would drift from the layout the moment either moved.</para></summary>
    internal readonly record struct HowToLayout(
        double TitleSize, double RowSize, double Art, double Gutter, double ContentWidth,
        HowToLine Title, HowToLine[] Lines, double Top, double Bottom);

    /// <summary>Lay a card out without drawing it. Rows are measured and stacked from the real text height
    /// rather than a fixed pitch, so a bullet that wraps pushes the rest down instead of overlapping them;
    /// the block is then centred, which is what makes a four-bullet and a five-bullet card both look
    /// deliberate in a round window.</summary>
    internal static HowToLayout MeasureHowTo(ArcadeHowTo card, double field, double ppd)
    {
        // The copy is the author's and it is not trimmed to fit: the card SHRINKS instead. Measured at full scale
        // first; while the block would run into the footer (0.72 of the radius) everything steps down by 6%,
        // to a floor a couch still reads. TestHarness arcade (HowToFits) gates the result.
        double fit = 1;
        HowToLayout layout = MeasureHowToAt(card, field, ppd, fit);
        while (layout.Bottom > field * HowToFooterFrac && fit > HowToMinFit)
        {
            fit *= 0.94;
            layout = MeasureHowToAt(card, field, ppd, fit);
        }
        return layout;
    }

    /// <summary>Where the ○ BACK footer sits, in field radii; the card's block has to end above it.</summary>
    private const double HowToFooterFrac = 0.72;
    /// <summary>How far the card may shrink to fit its copy before it is simply too long to ship.</summary>
    private const double HowToMinFit = 0.62;

    private static HowToLayout MeasureHowToAt(ArcadeHowTo card, double field, double ppd, double fit)
    {
        // The card takes its own text scale, not the HUD's: five illustrated bullets have to fit one disc
        // with no scrolling.
        double titleSize = Math.Max(11, field * 0.082) * ArcadeTuning.HowToTextScale * fit;
        double rowSize   = Math.Max(8, field * 0.049) * ArcadeTuning.HowToTextScale * fit;
        double art       = field * 0.150 * fit;           // the illustration box, square
        double gutter    = field * 0.055 * fit;
        // A chord across the disc rather than its diameter: the corners of a rectangle inscribed in a circle
        // are the first thing to fall off the edge, and text is drawn from its top-left.
        double textWidth = field * 1.18 - art - gutter;

        var lines = new HowToLine[card.Lines.Length];
        double block = 0;
        for (int i = 0; i < card.Lines.Length; i++)
        {
            lines[i] = LayOutLine(card.Lines[i].Text, rowSize, ArcadeChrome.Ink, ppd, TextAlignment.Left, textWidth);
            block += Math.Max(art, lines[i].Text.Height) + field * 0.030;
        }

        double top = -(block + titleSize * 1.9) / 2;
        // The drawn stack: the title's own height, the gap under it, then the rows (whose gaps are in block).
        // Measured exactly as DrawHowTo draws it — badges reserved, centred, wrapped at field * 1.3 — or a title
        // that wraps is measured as one line and Bottom under-reports by a full line (the fit gate then passes a
        // card that visibly overflows).
        var title = LayOutLine(card.Title, titleSize, ArcadeChrome.Ink, ppd, TextAlignment.Center, field * 1.3);
        double bottom = top + title.Text.Height + field * 0.055 + block;
        return new HowToLayout(titleSize, rowSize, art, gutter, art + gutter + textWidth, title, lines, top, bottom);
    }

    /// <summary>The how-to-play card: a title, about five illustrated bullets, and the way out. Laid out by
    /// the HOST so every game's card has the same shape — the game supplies strings and names its pictures,
    /// and the renderer draws those into boxes it is handed.</summary>
    private void DrawHowTo(DrawingContext dc, Point c, double field, Brush accent, ArcadeHowTo card, double ppd)
    {
        dc.DrawEllipse(ArcadeChrome.Scrim, null, c, field, field);

        HowToLayout L = MeasureHowTo(card, field, ppd);
        double left = c.X - L.ContentWidth / 2;

        double y = c.Y + L.Top;
        // The title was measured with the same accent-free ink; only its colour differs here.
        L.Title.Text.SetForegroundBrush(accent);
        var titleAt = new Point(c.X - field * 1.3 / 2, y);
        dc.DrawText(L.Title.Text, titleAt);
        ArcadeChrome.DrawInlineBadges(dc, L.Title.Text, titleAt, L.Title.Badges, L.TitleSize, accent, ppd);
        y += L.Title.Text.Height + field * 0.055;

        double rowSize = L.RowSize;
        for (int i = 0; i < card.Lines.Length; i++)
        {
            FormattedText text = L.Lines[i].Text;
            double h = Math.Max(L.Art, text.Height);
            var box = new Rect(left, y + (h - L.Art) / 2, L.Art, L.Art);
            // A renderer that doesn't know this key draws nothing — the bullet still reads, it just loses
            // its picture. Guarded anyway: this is the render pump, where a throw closes the arcade.
            if (!string.IsNullOrEmpty(card.Lines[i].Art))
            {
                try { _renderer?.DrawHowToArt(dc, box, card.Lines[i].Art, ppd); }
                catch (Exception ex) { Trace.WriteLine($"[Arcade] how-to art '{card.Lines[i].Art}': {ex.Message}"); }
            }
            var at = new Point(left + L.Art + L.Gutter, y + (h - text.Height) / 2);
            dc.DrawText(text, at);
            ArcadeChrome.DrawInlineBadges(dc, text, at, L.Lines[i].Badges, rowSize, ArcadeChrome.Ink, ppd);
            y += h + field * 0.030;
        }

        ArcadeChrome.DrawCentered(dc,
            $"{ControllerButtons.Text(PadButton.Circle)}  {Loc.T(UiText.Arcade.Back)}",
            ArcadeChrome.Ui(Math.Max(7, field * 0.040)), ArcadeChrome.InkDim, c.X, c.Y + field * 0.72, ppd, field * 1.6);
    }

    /// <summary>The △ cue for the how-to card — the only on-screen button prompt any game carries, and gone
    /// for good on that game once its card has been opened.
    ///
    /// <para>Drawn by the HOST so a new game inherits both the hint and its disappearance without
    /// implementing anything, and in the BEZEL because that is the only band a round window leaves free:
    /// both games occupy the bottom of the playfield (Kabloom's seed count, Connate's craft orbit), and
    /// covering a HUD to advertise a card is a worse trade than the prompt it replaces.</para></summary>
    private void DrawHowToHint(DrawingContext dc, Point c, double radius, double ppd, bool cardSeen)
    {
        double size = ArcadeChrome.Ui(Math.Max(7, radius * 0.038)) * StartPlaqueScale;
        string button = cardSeen ? "START" : ControllerButtons.Text(PadButton.Triangle);
        string verb   = Loc.T(cardSeen ? UiText.Arcade.Menu : UiText.Arcade.HowToPlay);
        Size box = ArcadeChrome.PromptSize(button, verb, size, ppd);
        if (box.Width <= 0) return;

        // Centred on the middle of the bezel ring, so the pill straddles it rather than intruding on the
        // playfield.
        double cy = c.Y + radius - radius * ArcadeChrome.BezelFrac / 2;
        double padX = size * 0.45, padY = size * 0.18;
        var plate = new Rect(c.X - box.Width / 2 - padX, cy - box.Height / 2 - padY,
                             box.Width + padX * 2, box.Height + padY * 2);
        // Under the plate, so they read as sliding out from behind it.
        DrawSlideCues(dc, plate, size, ppd);
        dc.DrawRoundedRectangle(ArcadeChrome.Panel, ArcadeChrome.FaintPen, plate,
                                plate.Height / 2, plate.Height / 2);
        ArcadeChrome.DrawPrompt(dc, button, verb, size, ArcadeChrome.InkDim, c.X - box.Width / 2, cy - box.Height / 2, ppd);
    }

    /// <summary>The window-move cues: a plaque either side of the START plaque, tucked behind it, naming the
    /// shoulder pair that slides a game's window between its three positions (docs/ARCADE.md ▸ Where the
    /// window sits). The labels follow the glyph set AND the summon chord — L1/R1 (LB/RB on Xbox), or L2/R2
    /// (LT/RT) when the chord's hold is the trigger pair and the bumpers do nothing.
    ///
    /// <para>Level, not rotated onto the ring, and TOP-aligned with the plaque they slide out of: the bezel
    /// falls away fast enough at this width that a plaque following it — or merely centred on it — loses its
    /// outer corners off the bottom of the disc.</para>
    ///
    /// <para>The side with nowhere left to go DARKENS — plate, rim, arrow and chip together. Not transparency
    /// (the boards behind are bright and varied, so a translucent plate gets louder on some of them), and not
    /// a fainter ink alone, which would flip the chip's auto-contrasted plate pale and light the dead cue up
    /// instead of putting it out.</para></summary>
    private void DrawSlideCues(DrawingContext dc, Rect plate, double hintSize, double ppd)
    {
        if (_picker) return;

        // Sized off the plaque's UN-shrunk metrics: the plaque is deliberately smaller than the band it sits
        // in (StartPlaqueScale), and the cues are not to come down with it.
        double size = hintSize / StartPlaqueScale * SlideCueScale;
        double h = plate.Height / StartPlaqueScale * SlideCueScale;
        string left  = ControllerButtons.Label(SlideOnTriggers ? PadButton.L2 : PadButton.L1);
        string right = ControllerButtons.Label(SlideOnTriggers ? PadButton.R2 : PadButton.R1);
        double arrow = size * 0.60, gap = size * 0.32, padX = size * 0.42;
        double chip = ArcadeChrome.ShoulderChipWidth(size);
        // What STICKS OUT past the plaque — the arrow, the chip and their padding. Everything is laid out in
        // this band alone, so nothing a cue carries can end up under the plaque covering it.
        double shown = padX * 2 + arrow + gap + chip;
        // How far it runs on UNDER the plaque: its own cap radius plus the plaque's, which is how far that
        // stadium's edge has receded by the time it reaches the top edge the two share. At anything less the
        // cue's inner cap curves into view and dips the top edge just before it disappears.
        double tuck = (h + plate.Height) / 2 + 1;

        for (int s = -1; s <= 1; s += 2)
        {
            bool stop = s < 0 ? WindowPosition <= 0 : WindowPosition >= 2;
            Brush ink = stop ? ArcadeChrome.InkFaint : ArcadeChrome.InkDim;

            double edge = s < 0 ? plate.X : plate.Right;              // the plaque side this one leaves from
            var slab = new Rect(s < 0 ? edge - shown : edge - tuck, plate.Y, shown + tuck, h);
            dc.DrawRoundedRectangle(stop ? ArcadeChrome.PanelDim : ArcadeChrome.Panel,
                                    stop ? ArcadeChrome.FaintDimPen : ArcadeChrome.FaintPen,
                                    slab, h / 2, h / 2);
            // The arrowhead is drawn, not typed: the Unicode arrowheads are outside the embedded face's
            // coverage and would fall back to whatever the machine has.
            double my = slab.Y + h / 2;
            double ax = s < 0 ? edge - shown + padX : edge + shown - padX - arrow;
            DrawArrowhead(dc, ax, my, arrow, s < 0, ink);
            double bx = s < 0 ? edge - padX - chip / 2 : edge + padX + chip / 2;
            ArcadeChrome.DrawShoulderChip(dc, s < 0 ? left : right, size, ink, ArcadeChrome.BadgePlate,
                                          new Point(bx, my), ppd);
        }
    }

    /// <summary>The window-move cues sit at this share of the START plaque they slide out of.</summary>
    private const double SlideCueScale = 2.0 / 3.0;
    /// <summary>The START plaque is drawn this much under the size its bezel band allows, which is what leaves
    /// the window-move cues either side of it room to read as a set rather than as three equal plaques.</summary>
    private const double StartPlaqueScale = 0.85;

    /// <summary>A solid arrowhead of <paramref name="width"/>, centred vertically on <paramref name="cy"/>,
    /// with its point at the left or right end of <paramref name="x"/>..x+width. ⚠ Cached per placement: this
    /// draws twice on every frame of play, and the placement only moves when the disc does, so building the
    /// figure each call is four heap objects a frame for nothing.</summary>
    private static void DrawArrowhead(DrawingContext dc, double x, double cy, double width, bool pointsLeft, Brush ink)
    {
        var key = (Math.Round(x, 1), Math.Round(cy, 1), Math.Round(width, 1), pointsLeft);
        if (!ArrowCache.TryGetValue(key, out var geo))
        {
            if (ArrowCache.Count >= 16) ArrowCache.Clear();   // a few placements ever coexist; bounded, not LRU
            double h = width * 0.62;
            double tip = pointsLeft ? x : x + width, back = pointsLeft ? x + width : x;
            var fig = new PathFigure { StartPoint = new Point(tip, cy), IsClosed = true, IsFilled = true };
            fig.Segments.Add(new LineSegment(new Point(back, cy - h), true));
            fig.Segments.Add(new LineSegment(new Point(back, cy + h), true));
            geo = new PathGeometry();
            geo.Figures.Add(fig);
            geo.Freeze();
            ArrowCache[key] = geo;
        }
        dc.DrawGeometry(ink, null, geo);
    }
    private static readonly Dictionary<(double, double, double, bool), PathGeometry> ArrowCache = new();

    /// <summary>The START pause menu, drawn over the frozen game. Uses the live game's own accent so it reads
    /// as part of that game rather than as system chrome dropped on top of it.</summary>
    private void DrawPause(DrawingContext dc, Point c, double field, Brush accent, double ppd)
    {
        // Dim the board hard: the menu has to win, and a half-visible game behind it invites reading both.
        dc.DrawEllipse(ArcadeChrome.Scrim, null, c, field, field);

        if (_confirmPrompt is { } prompt) { DrawConfirm(dc, c, field, accent, prompt, ppd); return; }

        var options = PauseOptions;
        var fixedRows = FixedRows;
        int rows = fixedRows.Length + options.Count;
        double titleSize = ArcadeChrome.Ui(Math.Max(11, field * 0.088));
        double rowSize = ArcadeChrome.Ui(Math.Max(9, field * 0.058));
        double gap = rowSize * 1.85;

        double y = c.Y - (rows - 1) * gap / 2 - field * 0.26;
        y += ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.Paused), titleSize, accent, c.X, y, ppd, field * 1.3)
             + field * 0.085;

        for (int i = 0; i < rows; i++)
        {
            bool on = i == _pauseIndex;
            Brush ink = on ? ArcadeChrome.Ink : ArcadeChrome.InkDim;
            string text = i < fixedRows.Length
                ? FixedRowLabel(fixedRows[i])
                : OptionRowText(options[i - fixedRows.Length]);
            // The cursor is a filled bar behind the row rather than a caret beside it — at couch distance a
            // small marker beside text is the first thing that stops being visible.
            if (on)
            {
                var bar = new Rect(c.X - field * 0.72, y - rowSize * 0.30, field * 1.44, rowSize * 1.60);
                dc.DrawRoundedRectangle(ArcadeChrome.Panel, null, bar, rowSize * 0.42, rowSize * 0.42);
            }
            ArcadeChrome.DrawCentered(dc, text, rowSize, ink, c.X, y, ppd, field * 1.5);
            y += gap;
        }

        ArcadeChrome.DrawCenteredRow(dc,
            [$"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Choose)}", Loc.T(UiText.Arcade.StartResume)],
            ArcadeChrome.Ui(Math.Max(7, field * 0.040)), ArcadeChrome.InkDim, c.X, c.Y + field * 0.72, ppd);
    }

    /// <summary>An option row: its label, then the current choice between chevrons that point only where a
    /// step can go — left/right clamp at the ends, so a chevron at an end would promise a choice that is not
    /// there. The blank stands in for a missing chevron so the choice does not shift as it changes.</summary>
    private static string OptionRowText(ArcadePauseOption option)
    {
        int last = option.Choices.Length - 1;
        int sel = Math.Clamp(option.Selected, 0, last);
        string left  = sel > 0    ? "‹ " : "  ";
        string right = sel < last ? " ›" : "  ";
        return $"{option.Label}   {left}{option.Choices[sel]}{right}";
    }

    /// <summary>The confirm prompt, INSTEAD of the menu rows rather than over them.
    ///
    /// <para>Replacing the menu is what makes the question unmissable: a dialog floating over a still-readable
    /// list invites answering the list. NO is highlighted and sits on the left, so the safe answer is both the
    /// default and the one a thumb reaches first.</para></summary>
    private void DrawConfirm(DrawingContext dc, Point c, double field, Brush accent, string prompt, double ppd)
    {
        double titleSize = ArcadeChrome.Ui(Math.Max(11, field * 0.082));
        double answerSize = ArcadeChrome.Ui(Math.Max(10, field * 0.070));

        double y = c.Y - field * 0.42;
        y += ArcadeChrome.DrawCentered(dc, prompt, titleSize, accent, c.X, y, ppd, field * 1.6) + field * 0.05;
        ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.EndsRun), ArcadeChrome.Ui(Math.Max(8, field * 0.045)),
            ArcadeChrome.InkDim, c.X, y, ppd, field * 1.6);

        double answerY = c.Y + field * 0.20;
        for (int i = 0; i < 2; i++)
        {
            bool yes = i == 1;
            bool on = yes == _confirmYes;
            double x = c.X + (yes ? field * 0.30 : -field * 0.30);
            if (on)
            {
                var bar = new Rect(x - field * 0.24, answerY - answerSize * 0.32,
                                   field * 0.48, answerSize * 1.64);
                dc.DrawRoundedRectangle(ArcadeChrome.Panel, null, bar, answerSize * 0.42, answerSize * 0.42);
            }
            ArcadeChrome.DrawCentered(dc, Loc.T(yes ? UiText.Arcade.Yes : UiText.Arcade.No), answerSize,
                on ? ArcadeChrome.Ink : ArcadeChrome.InkDim, x, answerY, ppd, field * 0.48);
        }

        // ⚠ No "D-PAD CHOOSE" here. Aiming is the stick AND the d-pad on every arcade menu, so naming one of
        // them taught the wrong half; and the highlight already says which answer is armed. Don't put it back.
        ArcadeChrome.DrawCenteredRow(dc,
            [$"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Confirm)}", Loc.T(UiText.Arcade.StartBack)],
            ArcadeChrome.Ui(Math.Max(7, field * 0.040)), ArcadeChrome.InkDim, c.X, c.Y + field * 0.72, ppd);
    }

    /// <summary>The catalog entry under the picker's cursor, or null with an empty catalog.</summary>
    private ArcadeCatalog.Entry? SelectedEntry()
    {
        var games = ArcadeCatalog.Games;
        return games.Length == 0 ? null : games[Math.Clamp(_pickerIndex, 0, games.Length - 1)];
    }

    /// <summary>True when a physical-screen-pixel point lands outside the round window — click-outside
    /// dismiss, mirroring the wheel and the Game Grid.</summary>
    public bool IsPointOutside(int screenPxX, int screenPxY)
    {
        try
        {
            var local = PointFromScreen(new Point(screenPxX, screenPxY));
            double radius = Radius;
            double dx = local.X - ActualWidth / 2, dy = local.Y - ActualHeight / 2;
            double edge = radius + 6;   // a small margin, so a click right on the bezel still counts as "on it"
            return dx * dx + dy * dy > edge * edge;
        }
        catch { return false; }   // not rendered / bad transform → don't dismiss
    }
}
