using System.Diagnostics;
using System.Windows.Threading;

namespace ControllerWheel;

public partial class App
{
    // ── Controller wiring ─────────────────────────────────────────────────────

    private CoalescedInput<(float Lx, float Ly, float Rx, float Ry)>? _stickDispatch;
    private void StartController()
    {
        _stickDispatch = new CoalescedInput<(float, float, float, float)>(
            action => Dispatcher.InvokeAsync(action), ApplyStick);
        // Register boundaries before UI/gesture subscribers. Future stick packets cannot change the
        // queued aim preceding a release, touch gesture, or disconnect.
        _controller.Connected += _ => _stickDispatch.Seal();
        _controller.TriggerLeftChanged += _ => _stickDispatch.Seal();
        _controller.TriggerRightChanged += _ => _stickDispatch.Seal();
        _controller.CrossChanged += _ => _stickDispatch.Seal();
        _controller.CircleChanged += _ => _stickDispatch.Seal();
        _controller.TriangleChanged += _ => _stickDispatch.Seal();
        _controller.SquareChanged += _ => _stickDispatch.Seal();
        _controller.L1Changed += _ => _stickDispatch.Seal();
        _controller.R1Changed += _ => _stickDispatch.Seal();
        _controller.L2Changed += _ => _stickDispatch.Seal();
        _controller.R2Changed += _ => _stickDispatch.Seal();
        _controller.L3Changed += _ => _stickDispatch.Seal();
        _controller.R3Changed += _ => _stickDispatch.Seal();
        _controller.PsChanged += _ => _stickDispatch.Seal();
        _controller.CreateChanged += _ => _stickDispatch.Seal();
        _controller.OptionsChanged += _ => _stickDispatch.Seal();
        _controller.DPadChanged += _ => _stickDispatch.Seal();
        _controller.TouchpadChanged += (_, _, _) => _stickDispatch.Seal();
        _controller.Touchpad2Changed += (_, _, _) => _stickDispatch.Seal();
        _controller.XInputSelectionBlocked += blocked => Dispatcher.InvokeAsync(() => OnXInputSelectionBlocked(blocked));
        // Zombie BT link (docs/CONTROLLERS.md): the reader has exhausted in-app recovery and no controller
        // input can arrive, so surface the only fix the user can apply — cycling the Bluetooth radio.
        // The event is edge-triggered, so this fires once per dead episode; the recovery edge needs no UI.
        _controller.BtLinkDead += dead => Dispatcher.InvokeAsync(() => OnBtLinkDead(dead));

        _controller.Connected += OnControllerConnected;

        _controller.BatteryChanged += (pct, charging) => Dispatcher.InvokeAsync(() => OnBatteryChanged(pct, charging));

        _controller.TriggerLeftChanged += pressed => Dispatcher.InvokeAsync(() => OnTriggerLeft(pressed));

        _controller.TriggerRightChanged += pressed => Dispatcher.InvokeAsync(() => OnTriggerRight(pressed));

        _controller.StickUpdate += OnStickUpdate;

        _controller.DPadChanged += nibble => Dispatcher.InvokeAsync(() => OnDPad(nibble));

        _controller.CrossChanged += pressed => Dispatcher.InvokeAsync(() => OnCross(pressed));

        _controller.CircleChanged += pressed => Dispatcher.InvokeAsync(() => OnCircle(pressed));

        // △ → add a slice in edit mode; toggle FAVORITE in the browser (art cycling lives on
        // Select/Start so the face buttons carry the personalization features).
        _controller.TriangleChanged += pressed => Dispatcher.InvokeAsync(() => OnTriangle(pressed));

        // □ → hold to delete the selected slice in edit mode; HOLD to hide the selected game in the
        // grid (I1.B — hold-to-confirm, so a stray tap can't vanish a game).
        _controller.SquareChanged += pressed => Dispatcher.InvokeAsync(() => OnSquare(pressed));

        // Select/Start (Create/Options) → art cycling in the grid, and on a plain open wheel Select cycles
        // the material (withheld from a public release, ReleaseGates.WheelMaterialCycle) while Start opens
        // Settings — under EVERY gesture configuration. A press that belongs
        // to an enabled Select/Start summon chord right now (its bumper/trigger/stick is held, or a viewmenu
        // gesture owns the wheel — TriggerInterpreter.SelectStartChordEngaged) goes to the chord instead.
        // In the arcade: Select is the dev level-skip (ArcadeDebug.LevelSkip, compiled out of a public release), START the
        // pause menu, and the chord's HOLD pair (bumpers, or triggers when the hold is the triggers) moves the game's
        // window between its three positions. Nothing reaches the surface behind the game.
        _controller.CreateChanged += pressed => Dispatcher.InvokeAsync(() => OnCreate(pressed));
        _controller.OptionsChanged += pressed => Dispatcher.InvokeAsync(() => OnOptions(pressed));

        _controller.L1Changed += pressed => Dispatcher.InvokeAsync(() => OnL1(pressed));
        _controller.R1Changed += pressed => Dispatcher.InvokeAsync(() => OnR1(pressed));

        // The triggers move the arcade window when THEY are the chord's hold; otherwise they stay the interpreter's.
        _controller.L2Changed += pressed => Dispatcher.InvokeAsync(() => OnL2(pressed));
        _controller.R2Changed += pressed => Dispatcher.InvokeAsync(() => OnR2(pressed));

        // L3/R3 (click the aiming stick) while a wheel is up → enter in-wheel edit mode. The aiming
        // stick is the opposite hand from the Fn that opened the wheel (Left Fn → right stick → R3).
        _controller.L3Changed += pressed => Dispatcher.InvokeAsync(() => OnL3(pressed));
        _controller.R3Changed += pressed => Dispatcher.InvokeAsync(() => OnR3(pressed));

        if (_overlay is not null) _overlay.EditStructureChanged += PersistEdited;

        // Alternate wheel triggers (chords / touchpad). Fn mode stays handled by the Fn handlers above;
        // the interpreter owns every other mode, opening/closing via the same overlay lifecycle.
        _triggerInterpreter = new TriggerInterpreter(_controller, Dispatcher, new TriggerInterpreter.Callbacks(
            WheelsEnabled: () => _wheelsEnabled,
            Busy: InterpreterBusy,
            OverlayVisible: () => _overlayVisible,
            // The OPEN wheel's style — set per-invoke from its gesture (InvokeWheel), so a toggle-opened
            // wheel reports toggle even when this interpreter's configured chords are hold-style.
            OverlayToggleStyle: () => _toggleStyle,
            OpenWheel: InterpreterOpenWheel,
            CloseAndFire: InterpreterCloseAndFire,
            Cancel: InterpreterCancel,
            ToggleEnabled: HandleChord));
        ApplyTriggerConfig();

        // Onboarding practice (J5): forward raw chord-relevant button state so the wizard's Hold/Tap
        // halves can light live. Null callback = the whole path is a no-op outside the practice step.
        _controller.L1Changed      += p => ForwardChordButton("l1", p);
        _controller.R1Changed      += p => ForwardChordButton("r1", p);
        _controller.L2Changed      += p => ForwardChordButton("l2", p);
        _controller.R2Changed      += p => ForwardChordButton("r2", p);
        _controller.L3Changed      += p => ForwardChordButton("l3", p);
        _controller.R3Changed      += p => ForwardChordButton("r3", p);
        _controller.PsChanged      += p => ForwardChordButton("ps", p);
        _controller.CreateChanged  += p => ForwardChordButton("create", p);
        _controller.OptionsChanged += p => ForwardChordButton("options", p);
        _controller.DPadChanged    += n => ForwardChordButton("dpad", n is 6 or 2);   // the invocation directions

        // Let the XInput backend skip our own virtual pad (XBox Mode's virtual Xbox 360 is itself an
        // XInput device) so it never reads its own tail.
        _controller.VirtualOutput = () => _emulator.XInputIdentity;
        _emulator.SelectedSourceSlot = () => _controller.XInputSlot;

        _controller.Start();
    }

    private void OnXInputSelectionBlocked(bool blocked)
    {
        // The stuck-root card names the cure for the same outage; this one would replace it in the single slot.
        if (blocked && !StuckRootEpisodeActive) ShowCornerToast(Loc.T(UiText.Settings.ControllerSelectionPaused),
            Loc.T(UiText.Settings.ControllerSelectionPausedBody), holdMs: 12000);
    }

    private void OnBtLinkDead(bool dead)
    {
        if (!dead) return;
        if (!_config.Current.System.BluetoothDropAlert)
        {
            Trace.WriteLine("[BT] link dead — card suppressed by the Bluetooth-drop setting");
            return;
        }
        ShowCornerToast(Loc.T(UiText.Toasts.SignalLost),
            Loc.T(UiText.Toasts.SignalLostBody),
            holdMs: 10000);
    }

    /// <summary>Reader thread: snapshots the kind, then applies the connection edge on the UI thread.</summary>
    private void OnControllerConnected(bool on)
    {
        // Snapshot on the reader thread: Kind can reset before this UI callback is dispatched.
        var connectedKind = _controller.Kind;
        Dispatcher.InvokeAsync(() => ApplyControllerConnected(on, connectedKind));
    }

    private void ApplyControllerConnected(bool on, ControllerKind connectedKind)
    {
        if (on) { _captureContinuity.ObserveConnected(connectedKind); _connectEpoch++; }
        _controllerConnected = on;   // live state for the onboarding wizard's detection step
        // A pad only reports its battery on a CHANGE, so a stale level would otherwise follow a
        // swapped-in pad until its charge next moved.
        if (!on) { _batteryPercent = -1; _batteryCharging = false; }
        _settingsWindow?.SetControllerConnected(on, PadTransport());   // Current Controller readout (grey when not seen)
        _settingsWindow?.SetBattery(_batteryPercent, _batteryCharging);
        RefreshTrayText();
        ApplyGlyphSet();   // "Best guess" follows presence, not just kind — a lost pad falls back to Xbox
        Trace.WriteLine($"[Controller] {(on ? $"connected — {_controller.Kind}" : "disconnected")}");
        if (!on)
        {
            OnControllerLost();
            // The sentry is NOT told here: this edge also fires for a yield to another backend, a
            // failed read or a manual reset, all of which leave the devnode — and Steam's handle
            // to it — alive. Only an observed devnode removal clears it (OnCloakedDevnodeDeparted).
            // Don't drop the virtual pad on the spot — a Bluetooth stall looks identical to a real
            // unplug for a moment (see project_overlay_dismiss_bt_stall) and a pad that vanishes and
            // reappears mid-game makes some titles re-detect controllers, worse than a briefly idle
            // pad. Only tear the pad down if the controller is STILL gone ~10s from now.
            if (_controllerPresentForPad && _padGraceTimer is null)
            {
                Trace.WriteLine($"[Capture] controller disconnected — pad grace timer started ({PadDisconnectGraceSeconds}s)");
                _padGraceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(PadDisconnectGraceSeconds) };
                _padGraceTimer.Tick += (_, _) =>
                {
                    _padGraceTimer?.Stop();
                    _padGraceTimer = null;
                    if (!_controllerConnected)
                    {
                        _controllerPresentForPad = false;
                        Trace.WriteLine("[Capture] controller still absent after grace — virtual pad torn down");
                        UpdateInputCapture();
                    }
                };
                _padGraceTimer.Start();
            }
        }
        else
        {
            // Cancel any pending teardown from a transient drop and let this pass's UpdateInputCapture
            // below do the actual pad+cloak reconciliation — UpdateInputCaptureCore's own internal
            // ordering (cloak-before-present on the Sony path) is unchanged by this flag.
            if (_padGraceTimer is not null) { _padGraceTimer.Stop(); _padGraceTimer = null; }
            bool wasAbsent = !_controllerPresentForPad;
            _controllerPresentForPad = true;
            if (wasAbsent) Trace.WriteLine("[Capture] controller (re)connected — virtual pad restored");
            // (Re)connect: re-run capture so THIS device instance gets cloaked — a pad that was off
            // at startup, or reconnected on a new transport (BT⇄USB), would otherwise never be
            // cloaked and games would read it directly (bleed-through).
            UpdateInputCapture();
            _controller.ReplayLatestState(_emulator.Submit);
            // The connected pad may be a DIFFERENT kind than last seen (controller swapped) — apply
            // that kind's trigger gesture set + Fn/chord routing. (The glyph set is re-applied for
            // both connect and disconnect above.)
            ApplyTriggerConfig();
            // Tell the user if the controller kind changed (swap / different-than-last-run) or is new,
            // so the silent gesture + glyph switch above isn't a mystery.
            AnnounceControllerKind();
            // An open Settings window edits triggers PER KIND — retarget its builder at the new pad.
            _settingsWindow?.SetDetectedKind(DetectedControllerKind);
        }
    }

    private void OnBatteryChanged(int pct, bool charging)
    {
        _batteryPercent = pct; _batteryCharging = charging;   // cached to seed a Settings window opened later
        _overlay?.SetBattery(pct, charging);
        _settingsWindow?.SetBattery(pct, charging);
    }

    private void OnTriggerLeft(bool pressed)
    {
        _fnLeftDown = pressed;
        // Arcade owns input: no wheel invoke and no enable/disable chord while a game is up. Leaving
        // the chord live would let a summon gesture disable the wheels out from under an open game.
        // ○ / Esc / a click outside is how you leave.
        if (_arcadeOpen) { if (pressed) TraceSurfaceSwallow("the arcade", "Fn press"); return; }
        if (_editPickGame) { if (pressed) TraceSurfaceSwallow("the game picker", "Fn press"); return; }
        if (Editing)                                        // Fn also saves + exits edit
        {
            if (pressed) { Trace.WriteLine("[Wheel] Fn press consumed by edit mode — saving and exiting edit"); ExitEdit(); }
            return;
        }
        if (pressed)
        {
            // Both-Fn = enable/disable — pressed TOGETHER only: with a wheel already up past the
            // grace window, the second Fn does not elevate the held invoke into the chord.
            // In practice, only when the wizard's CHOSEN chord is Fn.
            if (_fnRightDown) { FnChord(); return; }
            FnInvoke(leftHanded: true);
        }
        else
        {
            if (_holdFires) _silentWheel = null;   // hold-style: the silent (empty-wheel) window ends with the trigger
            if (_overlayVisible && (_oobePracticeAll ? _oobeFnLive : _fnOpensWheels) && _holdFires) CloseOverlay();   // Toggle: ✕/○ close instead
        }
    }

    private void OnTriggerRight(bool pressed)
    {
        _fnRightDown = pressed;
        if (_arcadeOpen) { if (pressed) TraceSurfaceSwallow("the arcade", "Fn press"); return; }   // see the left-hand twin above
        if (_editPickGame) { if (pressed) TraceSurfaceSwallow("the game picker", "Fn press"); return; }
        if (Editing)                                        // Fn also saves + exits edit
        {
            if (pressed) { Trace.WriteLine("[Wheel] Fn press consumed by edit mode — saving and exiting edit"); ExitEdit(); }
            return;
        }
        if (pressed)
        {
            // Both-Fn = enable/disable — pressed TOGETHER only (see the left-hand twin above).
            if (_fnLeftDown) { FnChord(); return; }
            FnInvoke(leftHanded: false);
        }
        else
        {
            if (_holdFires) _silentWheel = null;   // hold-style: the silent (empty-wheel) window ends with the trigger
            if (_overlayVisible && (_oobePracticeAll ? _oobeFnLive : _fnOpensWheels) && _holdFires) CloseOverlay();   // Toggle: ✕/○ close instead
        }
    }

    private void ApplyStick((float Lx, float Ly, float Rx, float Ry) axes)
    {
        var (lx, ly, rx, ry) = axes;
        // Arcade owns the stick outright while it's up — either stick, whichever is deflected more, so
        // it doesn't matter which hand summoned the wheel it came from. No wheel deadzone/EMA here:
        // the game applies its own (the wheel's 0.38 exists to make accidental ARMING hard, which is
        // the opposite of what a game wants).
        if (_arcadeOpen)
        {
            bool arcRight = rx * rx + ry * ry >= lx * lx + ly * ly;
            _overlay?.ArcadeStick(arcRight ? rx : lx, arcRight ? ry : ly);
            return;
        }
        if (_browserOpen) { BrowserStickNav(lx, ly); return; }   // left stick navigates the Game Grid
        if (!_overlayVisible) return;
        // Either stick aims, in every mode — the more-deflected one wins. "Wheel ignores other
        // stick" (Accessibility) is the opt-OUT that confines aiming to _aimWithRightStick's side,
        // and it applies to edit mode as well as to an open wheel.
        bool oneStick = _config.Current.System.WheelIgnoresOppositeStick;
        if (Editing)   // in-wheel edit = a single wheel: the stick drives focus / add-placement
        {
            bool useRightEdit = !oneStick ? rx * rx + ry * ry >= lx * lx + ly * ly : _aimWithRightStick;
            _overlay?.UpdateStick(useRightEdit ? rx : lx, useRightEdit ? ry : ly);
            AnnounceEditArmed();   // edit/picker selection narration (no-op when unchanged)
            return;
        }
        bool useRight = !oneStick ? rx * rx + ry * ry >= lx * lx + ly * ly : _aimWithRightStick;
        float sx = useRight ? rx : lx, sy = useRight ? ry : ly;
        _overlay?.UpdateStick(sx, sy);

        // When the armed slice changes, show the toggle's current state in the hub (and tick —
        // arming only, not the return to centre).
        int armed = _overlay?.ArmedIndex ?? -1;
        if (armed != _previewArmed)
        {
            // A GUARDED slice isn't armed yet — it's only started its dwell — so its tick waits for
            // dwell completion (the ConfirmReady hook), which is the ONLY sound it makes.
            // Deliberately NO focus cue for guarded slices: the dwell's own visuals already say
            // "you're on it", and a soft tick muddies the moment the real tick lands. The empty
            // branch is kept — not folded into the condition above — as the hook for whatever
            // replaces it (see Sfx.SliceArmedSoft, retained for the same reason).
            if (armed >= 0 && !(_overlay?.ArmedRequiresConfirm ?? false)) Sfx.SliceArmed();
            else if (armed >= 0) { /* guarded-slice focus cue: intentionally silent (narration covers it via AnnounceArmed) */ }
            // Guarded stage 3 of 3: the previous slice's hold was abandoned before completing.
            // Selection-kind on purpose — moving to ANOTHER slice replaces this with that slice's
            // own announcement (which already implies the old hold ended); only a return to
            // centre actually speaks it.
            if (_previewArmed >= 0 && _prevArmedGuarded && !_confirmReadySeen)
                _announcer.Announce(Loc.T(UiText.Narration.HoldCancelled), AnnouncementKind.Selection);
            _previewArmed = armed;
            _prevArmedGuarded = _overlay?.ArmedRequiresConfirm ?? false;
            _confirmReadySeen = false;
            UpdateArmedPreview(armed);
        }
    }

    /// <summary>Reader thread: queues the latest stick sample while a surface that reads the stick is up.</summary>
    private void OnStickUpdate(float lx, float ly, float rx, float ry)
    {
        if (!_overlayVisible && !_browserOpen && !_arcadeOpen) return;
        _stickDispatch!.Submit((lx, ly, rx, ry));
    }

    private void OnDPad(int nibble)
    {
        // Arcade: the d-pad goes to the GAME (Kabloom's grid cursor wants it). The
        // wheel's own d-pad modes stay suppressed while a game is up; forwarding to a game and ALSO
        // scrubbing volume or Alt-Tabbing behind it would be the worst of both.
        if (_arcadeOpen)
        {
            StopDpadVolumeRepeat(); StopDpadMixRepeat(); StopDpadMicRepeat();
            _overlay?.ArcadeDPad(nibble);
            return;
        }
        if (EditingNav)
        {
            // Two rounds of code-reading guessed wrong about why ◀▶ reorder did nothing; this makes
            // the next report self-diagnosing. Edge-triggered, so it's one line per press.
            Trace.WriteLine($"[EditNudge] nibble={nibble} phase={_overlay?.EditPhase} " +
                            $"armed={_overlay?.ArmedIndex} n={_overlay?.EditCurrentSlices.Length}");
            if (_overlay?.EditPhase == WheelStateMachine.EditPhase.Selecting)
            {
                // Fold the diagonals in, exactly as the volume path below does. The hat is ONE 8-way
                // value, not independent bits, so a thumb a few degrees off centre on the rocker
                // reads 7/5 (left) or 1/3 (right) — and DPadChanged is edge-triggered, so a
                // 8→7→6 roll delivers a 7 that a cardinals-only test drops on the floor.
                int dir = nibble is 5 or 6 or 7 ? -1 : nibble is 1 or 2 or 3 ? +1 : 0;
                if (dir != 0)
                {
                    if (_overlay.EditNudge(dir))
                    {
                        // Reordering moves the slice WITH the focus, so the name doesn't change —
                        // only the position does, which is the whole point of the action.
                        var sl = _overlay.EditCurrentSlices;
                        int at = _overlay.ArmedIndex;
                        _editArmed = at;   // suppress the focus announcement this frame would cause
                        if ((uint)at < (uint)sl.Length)
                            _announcer.Announce(Loc.F(UiText.Narration.MovedTo, Loc.DefaultLabel(sl[at].Label), at + 1, sl.Length),
                                                AnnouncementKind.Result);
                    }
                }
            }
            return;
        }
        if (_browserOpen)
        {
            switch (nibble)
            {
                case 0: NavBrowser(0, -1); break; // Up
                case 2: NavBrowser(+1, 0); break; // Right
                case 4: NavBrowser(0, +1); break; // Down
                case 6: NavBrowser(-1, 0); break; // Left
            }
            return;
        }
        if (!_overlayVisible)
        {
            StopDpadVolumeRepeat(); StopDpadMixRepeat(); StopDpadMicRepeat();
            // The first-run wizard's Material step takes the cardinals (the wizard ignores the d-pad on
            // every other step, where it is an invocation chord). Diagonals are dropped so a thumb roll
            // across the rocker moves one tile, not two.
            if (_onboardingWindow is { IsLoaded: true } wiz)
            {
                switch (nibble)
                {
                    case 0: wiz.ControllerDpad(0, -1); break;
                    case 2: wiz.ControllerDpad(+1, 0); break;
                    case 4: wiz.ControllerDpad(0, +1); break;
                    case 6: wiz.ControllerDpad(-1, 0); break;
                }
            }
            return;
        }

        // Up (incl. diagonals) = +, Down (incl. diagonals) = −, Left/Right/centre = release.
        if (!_config.Current.System.DpadVolumeWhileOpen) StopDpadVolumeRepeat();   // disabled in Advanced
        else
        {
            int volDir = nibble is 0 or 1 or 7 ? +1 : nibble is 3 or 4 or 5 ? -1 : 0;
            if (volDir == 0) StopDpadVolumeRepeat();
            else if (volDir != _dpadVolDir)   // newly pressed / switched direction: step once now, then arm the repeat
            {
                _dpadVolDir = volDir;
                AdjustVolume(volDir * 0.05f);
                StartDpadVolumeRepeat();
            }
        }

        // Left/Right (pure, no diagonals — those are already claimed by volume above): what
        // they do is the DpadHorizontalMode setting (G1.A) — Alt-Tab task switching (the
        // SystemConfig default), the app Volume Mixer (pair resolved
        // live per gesture; also the fallback for an unknown/blank token), mic volume,
        // virtual-desktop switching, or track skip. Mixer and mic auto-repeat while held (they scrub a level);
        // the other three are single-fire per press — repeating a desktop switch, track skip or
        // window advance while held would run away.
        int lrDir = nibble == 6 ? -1 : nibble == 2 ? +1 : 0;
        string lrMode = _config.Current.System.DpadHorizontalMode?.ToLowerInvariant() ?? "mixer";
        // A Settings change mid-hold dispatches the RELEASE to the new mode's arm, which only stops
        // its own tracker — the old mode's repeat timer would keep scrubbing (mic to 0%, or the mix
        // to one side) for as long as the wheel stayed up. Stop whatever this mode doesn't own first.
        if (lrMode == "mic") StopDpadMixRepeat(); else StopDpadMicRepeat();
        switch (lrMode)
        {
            case "mic":
                if (lrDir == 0) StopDpadMicRepeat();
                else if (lrDir != _dpadMicDir)
                {
                    _dpadMicDir = lrDir;
                    // Only arm the auto-repeat when a capture device actually answered — with no
                    // mic, repeating would hammer MMDeviceEnumerator + a swallowed COM throw every
                    // 80 ms on the dispatcher for the whole hold, for zero feedback.
                    if (StepMicVolume(lrDir)) StartDpadMicRepeat();
                }
                break;
            case "switcher":
            {
                bool newPress = lrDir != 0 && lrDir != _dpadMixDir;
                StopDpadMixRepeat();
                if (newPress) StepTaskSwitcher(lrDir);
                _dpadMixDir = lrDir;
                break;
            }
            case "desktop":
            {
                bool newPress = lrDir != 0 && lrDir != _dpadMixDir;   // BEFORE Stop zeroes the edge tracker
                StopDpadMixRepeat();
                if (newPress) KeypressSender.TrySend(lrDir < 0 ? "Win+Ctrl+Left" : "Win+Ctrl+Right");
                _dpadMixDir = lrDir;
                break;
            }
            case "song":
            {
                bool newPress = lrDir != 0 && lrDir != _dpadMixDir;
                StopDpadMixRepeat();
                if (newPress)
                {
                    _platform.MediaKey(lrDir < 0 ? "prev" : "next");
                    // A skip has no level to scrub, so the hub shows a big directional glyph for a
                    // beat instead — same 800 ms hide timer as the volume/mic scrubber, which owns
                    // the same hub slot.
                    _overlay?.ShowHubGlyph(lrDir < 0 ? "SkipBackward" : "SkipForward");
                    RestartVolumeHideTimer();
                    // The glyph is the only feedback a skip has — there's no level to scrub.
                    _announcer.Announce(Loc.T(lrDir < 0 ? UiText.Narration.PreviousTrack : UiText.Narration.NextTrack),
                                        AnnouncementKind.Result);
                }
                _dpadMixDir = lrDir;
                break;
            }
            default:   // "mixer"
                if (lrDir == 0) StopDpadMixRepeat();
                else if (lrDir != _dpadMixDir)
                {
                    _dpadMixDir = lrDir;
                    if (BeginMixGesture())   // resolve the live app pair for THIS gesture
                    {
                        StepMixBalance(lrDir * 5);
                        StartDpadMixRepeat();
                    }
                }
                break;
        }
    }

    private void OnCross(bool pressed)
    {
        if (_arcadeOpen) { _overlay?.ArcadeCross(pressed); return; }   // ✕ = the game's action button
        if (EditingNav) { if (pressed) EditCross(); return; }    // ✕ pick up / drop
        if (_browserOpen && pressed) { LaunchSelectedGame(); return; }
        // Toggle activation: ✕ confirms (fires the armed slice) on a toggle-open wheel.
        if (pressed && _overlayVisible && _toggleStyle && !_browserOpen)
            { CloseOverlay(); return; }
        // Nothing consumed it → the onboarding wizard's Next (✕/A advances any step).
        if (pressed && !_overlayVisible && _onboardingWindow is { IsLoaded: true } w) w.ControllerNext();
    }

    private void OnCircle(bool pressed)
    {
        // ○ is RESERVED for dismiss and is checked before every other consumer — that's what makes
        // "you can always get out of a game with one button" true of every game in the set. A game
        // never sees it (see ArcadeInput's remarks).
        if (_arcadeOpen) { ArcadeCircle(pressed); return; }
        if (EditingNav) { if (pressed) EditCircle(); return; }   // ○ cancel carry / save + exit
        if (_editPickGame) { if (pressed) CancelGamePickForEdit(); return; }   // ○ cancels the pick → back to edit
        // ○ answers the storefront hide-confirm toast (Cancel) before it means "close the grid".
        if (_browserOpen && pressed) { if (_overlay?.GamesCancelHideConfirm() != true) CloseGameBrowser(); return; }
        // Toggle activation: ○ cancels (closes without firing) a toggle-open wheel.
        if (pressed && _overlayVisible && _toggleStyle && !_browserOpen)
            { CancelOverlay(); return; }
        // Nothing consumed it → the onboarding wizard's Back (○/B goes back on any step).
        if (pressed && !_overlayVisible && _onboardingWindow is { IsLoaded: true } w) w.ControllerBack();
    }

    private void OnTriangle(bool pressed)
    {
        // The sentry alert owns △ while it's up — its whole contract is "persistent until the user
        // picks", so it outranks every other consumer.
        if (_sentryAlert is not null) { if (pressed) SentryChoseRelaunch(); return; }
        // △ in the arcade is HOW TO PLAY — a host button, like ○, meaning the same thing in every
        // game present and future. It routes through the control rather than being handled here,
        // because only the control knows whether the guard card is up (where △ is the
        // hold-to-play-anyway override) or the how-to is already showing.
        if (_arcadeOpen)
        {
            _overlay?.ArcadeTriangle(pressed);
            if (pressed) _overlay?.ArcadeToggleHowTo();
            return;
        }
        if (EditingNav) { if (pressed) EditAdd(); return; }
        if (pressed && _browserOpen) { _overlay?.GamesToggleFavorite(); return; }
        // Nothing consumed it → the onboarding wizard's Material step toggles Accessibility.
        if (pressed && !_overlayVisible && _onboardingWindow is { IsLoaded: true } w) w.ControllerTriangle();
    }

    private void OnSquare(bool pressed)
    {
        if (_sentryAlert is not null) { if (pressed) SentryChoseSafeMode(); return; }
        if (_arcadeOpen) { _overlay?.ArcadeSquare(pressed); return; }   // □ = the game's third button
        if (EditingNav) { EditDeleteHeld(pressed); return; }
        if (_browserOpen) _overlay?.GamesSetHideHeld(pressed);
    }

    private void OnCreate(bool pressed)
    {
        if (_arcadeOpen) { if (pressed && ArcadeDebug.LevelSkip) _overlay?.ArcadeSkip(); return; }
        if (!pressed || EditingNav) return;
        if (_browserOpen) { _overlay?.CycleCover(); return; }
        if (ReleaseGates.WheelMaterialCycle && _overlayVisible && _triggerInterpreter?.SelectStartChordEngaged() != true) CycleSliceMaterial();
    }

    private void OnOptions(bool pressed)
    {
        // START pauses an arcade game (the pause menu owns Reset).
        // Checked before the grid's logo cycle for the same reason ○ is checked before everything:
        // while a game owns the screen, its bindings win.
        if (_arcadeOpen) { if (pressed) _overlay?.ArcadeTogglePause(); return; }
        if (!pressed || EditingNav) return;
        if (_browserOpen) { _overlay?.CycleLogo(); return; }
        if (_overlayVisible && _triggerInterpreter?.SelectStartChordEngaged() != true) OpenSettings();
    }

    private void OnL1(bool pressed)
    {
        if (_arcadeOpen) { if (pressed && !HoldIsTrigger()) ArcadeSlide(-1); return; }
        if (EditingNav) { if (pressed && _overlay?.EditPhase == WheelStateMachine.EditPhase.Selecting) EditUndoRedo(undo: true); return; }
        if (pressed && _browserOpen) _overlay?.CycleLauncherFilter(-1);
    }

    private void OnR1(bool pressed)
    {
        if (_arcadeOpen) { if (pressed && !HoldIsTrigger()) ArcadeSlide(+1); return; }
        if (EditingNav) { if (pressed && _overlay?.EditPhase == WheelStateMachine.EditPhase.Selecting) EditUndoRedo(undo: false); return; }
        if (pressed && _browserOpen) _overlay?.CycleLauncherFilter(+1);
    }

    private void OnL2(bool pressed) { if (_arcadeOpen && pressed && HoldIsTrigger()) ArcadeSlide(-1); }
    private void OnR2(bool pressed) { if (_arcadeOpen && pressed && HoldIsTrigger()) ArcadeSlide(+1); }

    private void OnL3(bool pressed)
    {
        if (!pressed) return;
        if (_arcadeOpen) return;   // a stick click must not drop into the wheel editor under a game
        if (EditingNav)
        {
            // Click again → save + exit, but ignore mid-carry so it can't bail a move (○ cancels a carry).
            if (_overlay?.EditPhase == WheelStateMachine.EditPhase.Selecting) ExitEdit();
            return;
        }
        if (SilentEditReady) { EnterEditFromSilent(); return; }   // empty-wheel invoke + stick click
        // Either stick is an aiming stick, so either click enters edit. "Wheel ignores opposite stick"
        // narrows that to the wheel's own aiming stick — the one the hub names in its edit hint.
        if (_overlayVisible && !_browserOpen && (!_config.Current.System.WheelIgnoresOppositeStick || !_aimWithRightStick)) EnterEdit();
    }

    private void OnR3(bool pressed)
    {
        if (!pressed) return;
        if (_arcadeOpen) return;   // a stick click must not drop into the wheel editor under a game
        if (EditingNav)
        {
            // Click again → save + exit, but ignore mid-carry so it can't bail a move (○ cancels a carry).
            if (_overlay?.EditPhase == WheelStateMachine.EditPhase.Selecting) ExitEdit();
            return;
        }
        if (SilentEditReady) { EnterEditFromSilent(); return; }   // empty-wheel invoke + stick click
        if (_overlayVisible && !_browserOpen && (!_config.Current.System.WheelIgnoresOppositeStick || _aimWithRightStick)) EnterEdit();
    }

    private bool InterpreterBusy() => Editing || _editPickGame || _browserOpen || _arcadeOpen;

    private void InterpreterOpenWheel(bool leftHanded)
    {
        _lastTrigger = _triggerInterpreter?.LastInvokeToken;   // which gesture (practice check-off)
        InvokeWheel(leftHanded);
    }

    // In edit mode the wheel must stay until an explicit dismiss (○ / Fn) — releasing the invoke
    // chord must NOT tear it down (mirrors the Fn-release gate). Entering edit via a stick-click
    // while a HOLD chord is held would otherwise dismiss on release.
    private void InterpreterCloseAndFire() { if (!Editing) CloseOverlay(); }

    // Re-trigger/re-swipe "dismiss" must not tear down the wheel SUSPENDED behind the edit
    // game-pick grid (OverlayVisible stays true there): cancelling would kill the edit session
    // while the grid stayed up, and the eventual pick would resume a painted-but-unsuppressed
    // ghost wheel. The grid owns input there — ○ backs out of the pick.
    private void InterpreterCancel() { if (_editPickGame) return; CancelOverlay(); }

    private void ForwardChordButton(string token, bool pressed)
    {
        if (_oobeChordButtons is null) return;
        Dispatcher.InvokeAsync(() => _oobeChordButtons?.Invoke(token, pressed));
    }
}
