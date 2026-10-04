namespace ControllerWheel;

/// <summary>Outcome of one dwell <see cref="WheelStateMachine.Tick"/>: whether the renderer should
/// repaint, and whether an assign-mode dwell completed (auto-commit) on a given slice.</summary>
public readonly record struct WheelTickResult(bool Invalidate);

/// <summary>
/// Platform-agnostic wheel selection logic: stick → armed slice (with a centre deadzone, EMA
/// smoothing and a sticky grace window), the hold-to-confirm dwell, the edit-mode hold-□ delete
/// dwell, and assign-mode dwell. No WPF — the WPF control owns one of these, feeds it stick input on a render
/// timer, and reads the state back to draw.
/// </summary>
public sealed class WheelStateMachine
{
    // ── Tunables ──────────────────────────────────────────────────────────────
    public const float Deadzone = 0.38f;   // deliberately wide: makes accidental arming harder
    // EMA (alpha=0.20) damps single-frame spikes to ≤0.20, comfortably under the deadzone —
    // prevents false arming. An intentional hold crosses the deadzone within 2 frames (~16 ms @125Hz).
    private const float  Alpha          = 0.20f;
    private const double ConfirmHoldMs  = 800;
    public  const double ConfirmTickMs  = 30;   // render-timer cadence the dwell math assumes
    public  const double ConfirmArmFloor = 0.3;   // (render uses it for the arming opacity ramp)

    /// <summary>Keep the last armed slice this long after the stick recentres, so releasing Fn and
    /// the stick together still fires instead of cancelling. Set from SystemConfig.</summary>
    public long StickyMs { get; set; } = 150;

    /// <summary>The millisecond clock every timed read here uses (sticky grace, reflow, landing). Null reads
    /// <c>Environment.TickCount64</c>; the test harness pins it so those windows can be stepped exactly.</summary>
    internal long? PinnedClockMs = null;
    private long NowMs => PinnedClockMs ?? Environment.TickCount64;

    // ── State ─────────────────────────────────────────────────────────────────
    private IReadOnlyList<WheelSlice> _slices = [];
    private int   _armedIndex = -1;
    // A d-pad reorder owns the armed index until the aiming stick returns to centre, so a still-tilted
    // stick can't drag the focus back to the slot the slice was just moved out of. Edit mode only.
    private bool  _armedPinned;
    private int   _stickyArmed = -1;
    private long  _stickyTick;
    private float _stickX, _stickY;
    private float _smoothX, _smoothY;    // EMA-filtered stick for rendering + arming

    private int    _confirmIndex = -1;   // slice currently being held to confirm
    private double _confirmProgress;     // 0..1 dwell fill
    private bool   _confirmDone;         // dwell complete → release will fire (does not auto-fire)

    // ── Edit mode (V2 in-wheel editing) ─────────────────────────────────────────
    public  const double ReflowMs        = 200;  // whole-ring re-spacing animation when the slice count changes

    /// <summary>Hold □ this long (while a slice is armed) to delete it. Host-settable because narration
    /// needs it LONGER: the spoken "Hold to delete &lt;slice&gt;" has to finish before the dwell completes,
    /// or the warning lands after the deletion it was warning about. See <c>App.NarrationDwellScale</c>.</summary>
    public double DeleteConfirmMs { get; set; } = 500;

    /// <summary>Whether the Add picker keeps its armed entry when the stick recentres. Off, the picker arms
    /// like a live wheel (centre = nothing armed). The host turns it on while narration is on, so a listener
    /// can let go of the stick after hearing an entry and still ✕ it.</summary>
    public bool PickerLatchesAtCentre { get; set; }

    public enum EditPhase { Selecting, Carrying, Picking }
    private bool      _editMode;
    private EditPhase _editPhase = EditPhase.Selecting;
    private List<WheelSlice> _editList = new();
    private int       _carryIndex  = -1;   // origin index of the slice being carried (-1 = none)
    private int       _carryTarget = -1;   // current drop-target index while carrying
    // Live carry preview: the render list during Carrying is a PROVISIONAL ordering = the other slices
    // (in committed order, fixed at pick-up) with the carried slice inserted at the hovered slot, so the
    // wheel shows exactly what dropping will produce. _editList stays the committed source of truth until
    // DropCarried commits the provisional order.
    private WheelSlice?      _carrySlice;            // the slice in hand (identified by ref while drawing)
    private List<WheelSlice> _carryBase = new();     // committed order minus the carried slice (fixed)
    private List<WheelSlice> _carryProvisional = new();
    private bool      _deleteHeld;
    private double    _deleteProgress;     // 0..1 hold-□ dwell
    private readonly Stack<WheelSlice[]> _undo = new();
    private readonly Stack<WheelSlice[]> _redo = new();

    // Structural-change animation (state-machine-driven; the renderer interpolates). Cleared when reflow settles.
    private Dictionary<WheelSlice, double>? _prevCenter;  // pre-change centre angle per surviving slice (by ref)
    private long       _reflowStart;
    private bool       _reflowActive;
    private WheelSlice? _ghost;            // a just-deleted slice, fading out
    private double     _ghostCenterDeg;
    private int        _ghostCount;        // slice count at the moment of deletion (sizes the ghost wedge)
    private int        _popInIndex = -1;   // a just-added slice popping in (-1 = none)

    // A brief settle "pop" on the slice just placed (moved or added) so the drop reads as landing in
    // place before the wheel despawns. Time-based and independent of the reflow clock.
    public  const double LandingMs = 220;
    private int        _landingIndex = -1;
    private long       _landingStart;

    // ── Configuration ──────────────────────────────────────────────────────────
    public IReadOnlyList<WheelSlice> Slices { get => _slices; set => _slices = value ?? []; }
    public int  SliceCount => _slices.Count;

    /// <summary>When true, UpdateStick records the stick position but does not recompute the armed index.</summary>
    public bool FreezeArmed { get; set; }


    // ── Read-back state (renderer + App consume these) ─────────────────────────
    public int    ArmedIndex      => _armedIndex;
    public float  StickX          => _stickX;
    public float  StickY          => _stickY;
    public double ConfirmProgress => _confirmProgress;
    public bool   ConfirmDone     => _confirmDone;

    /// <summary>Armed index for fire-on-release: the live armed slice, or the most recent one if the
    /// stick recentred within the last <see cref="StickyMs"/> ms (sticky grace window).</summary>
    public int StickyArmedIndex =>
        _armedIndex >= 0
            ? _armedIndex
            : (_stickyArmed >= 0 && NowMs - _stickyTick <= StickyMs) ? _stickyArmed : -1;

    /// <summary>True when the armed slice requires confirm AND its hold dwell has completed, so
    /// releasing Fn while it's still selected should fire it. The dwell no longer auto-fires.</summary>
    public bool ArmedConfirmReady =>
        _armedIndex >= 0 && _confirmIndex == _armedIndex && _confirmDone;


    public bool RequiresConfirm(int i)
    {
        if ((uint)i >= (uint)_slices.Count) return false;
        return _slices[i].Action?.RequireConfirm == true;
    }

    // ── Stick → armed slice ─────────────────────────────────────────────────────
    public void UpdateStick(float x, float y)
    {
        _smoothX += (x - _smoothX) * Alpha;
        _smoothY += (y - _smoothY) * Alpha;

        float mag = MathF.Sqrt(_smoothX * _smoothX + _smoothY * _smoothY);
        bool active = mag >= Deadzone;

        // Dot snaps to centre below the deadzone so stick drift stays invisible.
        _stickX = active ? _smoothX : 0f;
        _stickY = active ? _smoothY : 0f;

        // While carrying a slice in edit mode, the stick chooses the drop-target slot; the armed index
        // stays pinned to the carried slice's origin.
        if (_editMode && _editPhase == EditPhase.Carrying)
        {
            if (active && _carryProvisional.Count > 0)
            {
                int t = AngleToIndex(_smoothX, _smoothY, _carryProvisional.Count);
                if (t != _carryTarget)
                {
                    CapturePrev(_carryProvisional);   // snapshot current slots, then reflow to the new layout
                    _carryTarget = t;
                    RebuildProvisional();
                    BeginReflowClock();               // animate-only: no StructureVersion bump (no persist)
                }
            }
            return;
        }

        if (!FreezeArmed)
        {
            int newArmed = (_slices.Count > 0 && active) ? AngleToIndex(_smoothX, _smoothY, _slices.Count) : -1;
            // Returning to centre releases a d-pad nudge's claim on the armed index (see _armedPinned).
            if (!active) _armedPinned = false;
            // Edit mode LATCHES the armed slice — letting the stick recentre must not disarm it. The d-pad
            // reorder shares a thumb with the aiming stick whenever the left stick is the aiming hand, so
            // requiring a live tilt makes ◀▶ unreachable by construction. Safe only here: edit mode has no
            // release-to-fire, whereas on a live wheel disarming at centre IS the cancel gesture.
            // The Add picker has no d-pad reorder, so it latches only when the host asks (PickerLatchesAtCentre).
            bool latchedAtCentre = _editMode && newArmed < 0 && _armedIndex >= 0
                                   && (_editPhase != EditPhase.Picking || PickerLatchesAtCentre);
            // ⚠ And the reorder's OTHER half: a nudge MOVES the slice, so the armed index must travel with
            // it. This loop runs at poll rate (~250 Hz), and while the stick is still tilted AngleToIndex
            // returns the slot under the thumb — which would snap the armed index straight back to the fixed
            // screen position the nudge just moved the slice out of. The swap then reads as "nothing
            // happened", and pressing again swaps it back. Hold the nudge's claim until the stick recentres.
            bool pinnedByNudge = _editMode && _armedPinned && active;
            if (!latchedAtCentre && !pinnedByNudge) _armedIndex = newArmed;
            if (newArmed >= 0) { _stickyArmed = newArmed; _stickyTick = NowMs; }
        }
    }

    /// <summary>Slice index a stick/click vector points at (slice 0 centered at 12 o'clock).</summary>
    public int IndexAtAngle(double x, double y) =>
        _slices.Count > 0 ? AngleToIndex(x, y, _slices.Count) : -1;

    private static int AngleToIndex(double x, double y, int count)
    {
        if (count <= 0) return -1;   // no slices → nothing to arm (guards integer % / ÷ by zero)
        double deg = Math.Atan2(y, x) * 180 / Math.PI + 90;
        if (deg < 0)    deg += 360;
        if (deg >= 360) deg -= 360;
        double sd = 360.0 / count;
        return (int)((deg + sd / 2.0) / sd) % count;
    }

    // ── Edit-mode read-back (renderer + App) ────────────────────────────────────

    /// <summary>Where the carried slice would LAND right now — its index in the provisional list, which is
    /// what <see cref="DropCarried"/> commits and what the wheel is previewing. -1 when not carrying.
    /// <para>⚠ Narration must use this, not <c>ArmedIndex</c>, for anything that names a carry position.
    /// While carrying, <c>UpdateStick</c> returns early and <c>_armedIndex</c> stays pinned to the slice's
    /// ORIGIN slot — so a "position N" built from it reports where the slice came from, never where the
    /// user is aiming it.</para></summary>
    public int CarrySlot => _editPhase == EditPhase.Carrying && _carrySlice is not null
        ? _carryProvisional.IndexOf(_carrySlice)
        : -1;

    public bool      EditMode       => _editMode;
    public EditPhase Phase          => _editPhase;
    public int       CarryTarget    => _carryTarget;
    /// <summary>The slice currently in hand (Carrying), identified by reference so the renderer can lift
    /// it at its provisional slot. Null when not carrying.</summary>
    public WheelSlice? CarriedSlice => _editPhase == EditPhase.Carrying ? _carrySlice : null;
    public double    DeleteProgress => _deleteProgress;
    public bool      CanUndo        => _undo.Count > 0;
    public bool      CanRedo        => _redo.Count > 0;

    public double ReflowProgress =>
        _reflowActive ? Math.Clamp((NowMs - _reflowStart) / ReflowMs, 0, 1) : 1.0;
    public bool        Reflowing      => _reflowActive;
    public IReadOnlyDictionary<WheelSlice, double>? PrevCenter => _prevCenter;
    public WheelSlice? Ghost          => _ghost;
    public double      GhostCenterDeg => _ghostCenterDeg;
    public int         GhostCount     => _ghostCount;
    public double      GhostFade      => _reflowActive && _ghost is not null ? 1.0 - ReflowProgress : 0.0;
    public int         PopInIndex     => _popInIndex;

    /// <summary>Index of the slice playing its landing settle, or -1.</summary>
    public int    LandingIndex    => _landingIndex;
    public bool   Landing         => _landingIndex >= 0 && NowMs - _landingStart <= LandingMs;
    public double LandingProgress => Math.Clamp((NowMs - _landingStart) / LandingMs, 0, 1);
    /// <summary>Any in-flight edit animation worth holding the wheel open for before despawning.</summary>
    public bool   Settling        => Landing || _reflowActive;

    /// <summary>Bumped on every structural change (move/delete/add/undo) so the host can persist.</summary>
    public int StructureVersion { get; private set; }

    /// <summary>Centre angle (degrees; 0 = up / 12 o'clock, clockwise) of slice i in an n-slice wheel.</summary>
    public static double SliceCenterDeg(int i, int n) => n > 0 ? 360.0 / n * i - 90.0 : -90.0;

    // ── Edit-mode operations (host drives these; persist CurrentSlices after each) ───────────────
    /// <summary>Enter edit mode on a working copy of the given slices.</summary>
    public void BeginEdit(IEnumerable<WheelSlice> slices)
    {
        _editList  = new List<WheelSlice>(slices);
        _slices    = _editList;
        _editMode  = true;
        _editPhase = EditPhase.Selecting;
        _carryIndex = _carryTarget = -1;
        _carrySlice = null;
        _landingIndex = -1;
        _deleteHeld = false; _deleteProgress = 0;
        _undo.Clear();
        _redo.Clear();
        ClearAnim();
        FreezeArmed = false;
    }

    /// <summary>Leave edit mode and return the final slice array (for the host to persist).</summary>
    public WheelSlice[] EndEdit()
    {
        var result = _editList.ToArray();
        _editMode  = false;
        _editPhase = EditPhase.Selecting;
        _slices    = _editList;
        _carryIndex = _carryTarget = -1;
        _carrySlice = null;
        _landingIndex = -1;
        _deleteHeld = false; _deleteProgress = 0;
        ClearAnim();
        return result;
    }

    /// <summary>Current working slices (snapshot) — persist this after each edit op for live save.</summary>
    public WheelSlice[] CurrentSlices => _editList.ToArray();

    /// <summary>Show an Add-picker menu (categories / types) over the wheel. The edit working list is
    /// untouched; selection arms over the menu. Use for both opening and descending a level.</summary>
    public void SetPicker(IReadOnlyList<WheelSlice> menu)
    {
        _slices       = menu;                 // arming + render run over the menu; _editList kept intact
        _editPhase    = EditPhase.Picking;
        _armedIndex   = -1;
        _carryIndex   = _carryTarget = -1;
        FreezeArmed   = false;
    }

    /// <summary>Leave the Add picker, restoring the edit working list.</summary>
    public void EndPicker()
    {
        _slices       = _editList;
        _editPhase    = EditPhase.Selecting;
        _armedIndex   = -1;
    }

    /// <summary>The current carry is an ADD (a slice appended and carried to its spot — game assign /
    /// Add picker) rather than a re-position of an existing slice. Drives the hub title (Add vs Moving).</summary>
    public bool CarryIsAdd { get; private set; }

    /// <summary>Pick up the armed slice to carry it. No-op unless selecting with a slice armed.</summary>
    public void PickUpArmed()
    {
        if (!_editMode || _editPhase != EditPhase.Selecting) return;
        if ((uint)_armedIndex >= (uint)_editList.Count) return;
        _carryIndex = _carryTarget = _armedIndex;
        _carrySlice = _editList[_armedIndex];
        _carryBase  = new List<WheelSlice>(_editList);
        _carryBase.RemoveAt(_armedIndex);
        RebuildProvisional();                 // provisional == original order (carried back in its slot)
        _editPhase  = EditPhase.Carrying;
        CarryIsAdd  = false;
    }

    /// <summary>Rebuild the provisional render list: the fixed base with the carried slice inserted at the
    /// hovered slot. Insert index matches <see cref="DropCarried"/> so the preview equals the commit.</summary>
    private void RebuildProvisional()
    {
        if (_carrySlice is null) return;
        int at = Math.Clamp(_carryTarget, 0, _carryBase.Count);
        _carryProvisional = new List<WheelSlice>(_carryBase);
        _carryProvisional.Insert(at, _carrySlice);
        _slices = _carryProvisional;
    }

    /// <summary>Drop the carried slice at the current target slot. Returns true if the order changed.</summary>
    public bool DropCarried()
    {
        if (!_editMode || _editPhase != EditPhase.Carrying || _carrySlice is null) return false;
        int finalIndex = _carryProvisional.IndexOf(_carrySlice);
        bool changed   = !RefSequenceEqual(_carryProvisional, _editList);
        if (changed) PushUndo();
        // The provisional ordering IS the result — commit it wholesale (preview == commit).
        _editList   = new List<WheelSlice>(_carryProvisional);
        _slices     = _editList;
        _armedIndex = finalIndex;
        _editPhase  = EditPhase.Selecting;
        _carryIndex = _carryTarget = -1;
        _carrySlice = null;
        StartLanding(finalIndex);             // brief settle on the placed slice (moved or added)
        if (changed) StructureVersion++;      // persist only when the order actually changed
        return changed;
    }

    /// <summary>Abort an in-progress carry. A MOVE returns the slice to its home slot; an ADD (the slice
    /// was appended when the carry began) is fully backed out — the un-placed slice is removed so cancel
    /// leaves the wheel exactly as it was before Add.</summary>
    public void CancelCarry()
    {
        if (_editPhase != EditPhase.Carrying) return;
        bool wasAdd = CarryIsAdd;
        CapturePrev(_carryProvisional);       // animate from the preview layout back to the committed order
        if (wasAdd && _carrySlice is not null)
        {
            _editList.Remove(_carrySlice);    // undo the append — don't strand the un-placed slice on the wheel
            StructureVersion++;               // persist the removal
        }
        _slices     = _editList;              // discard the provisional ordering
        _armedIndex = _editList.Count == 0 ? -1 : Math.Min(_carryIndex, _editList.Count - 1);
        _carryIndex = _carryTarget = -1;
        _carrySlice = null;
        CarryIsAdd  = false;
        _editPhase  = EditPhase.Selecting;
        BeginReflowClock();                   // settle back home (MOVE) / reflow after the removal (ADD)
    }

    /// <summary>Swap the armed slice with a neighbour (dir -1 = CCW, +1 = CW). Returns true if moved.</summary>
    public bool NudgeSelected(int dir)
    {
        if (!_editMode || _editPhase != EditPhase.Selecting) return false;
        if (_armedIndex < 0 || _editList.Count < 2) return false;
        int n = _editList.Count, from = _armedIndex;
        int to = ((from + dir) % n + n) % n;
        PushUndo(); CapturePrev();
        (_editList[from], _editList[to]) = (_editList[to], _editList[from]);
        _armedIndex = to;
        _armedPinned = true;   // the moved slice keeps the focus until the stick recentres — see UpdateStick
        StartReflow();
        return true;
    }

    /// <summary>Host feeds the □ button state; a sustained hold (while a slice is armed) deletes it.</summary>
    public void SetDeleteHeld(bool held)
    {
        if (held == _deleteHeld) return;
        _deleteHeld = held;
        if (!held) _deleteProgress = 0;   // released early → cancel
    }

    /// <summary>Append a new slice and immediately carry it so the user places it. Returns its index.</summary>
    public int AddSliceAndCarry(WheelSlice slice)
    {
        if (!_editMode) return -1;
        PushUndo();
        int at = _editList.Count;
        _editList.Add(slice);
        _armedIndex = at;
        _carryIndex = _carryTarget = at;
        _carrySlice = slice;
        _carryBase  = new List<WheelSlice>(_editList);
        _carryBase.RemoveAt(at);              // base = the pre-existing slices
        RebuildProvisional();                 // carried sits at the end (the slot we hover first)
        _editPhase  = EditPhase.Carrying;
        CarryIsAdd  = true;                   // hub title reads "Add" (vs "Moving" for a re-position)
        StructureVersion++;                   // persist the appended slice
        return at;
    }

    /// <summary>Undo the last structural change. Returns true if something was restored.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        CapturePrev();
        _redo.Push(_editList.ToArray());
        _editList = new List<WheelSlice>(_undo.Pop());
        _slices   = _editList;
        _editPhase = EditPhase.Selecting;
        _carryIndex = _carryTarget = -1;
        if (_armedIndex >= _editList.Count) _armedIndex = _editList.Count - 1;
        StartReflow();
        return true;
    }

    /// <summary>Redo the last undone change. Returns true if something was reapplied.</summary>
    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        CapturePrev();
        _undo.Push(_editList.ToArray());
        _editList = new List<WheelSlice>(_redo.Pop());
        _slices   = _editList;
        _editPhase = EditPhase.Selecting;
        _carryIndex = _carryTarget = -1;
        if (_armedIndex >= _editList.Count) _armedIndex = _editList.Count - 1;
        StartReflow();
        return true;
    }

    private void DeleteAt(int index)
    {
        if ((uint)index >= (uint)_editList.Count) return;
        PushUndo(); CapturePrev();
        _ghost          = _editList[index];
        _ghostCount     = _editList.Count;
        _ghostCenterDeg = SliceCenterDeg(index, _ghostCount);
        _editList.RemoveAt(index);
        if (_armedIndex >= _editList.Count) _armedIndex = _editList.Count - 1;
        StartReflow();
    }

    private void PushUndo() { _undo.Push(_editList.ToArray()); _redo.Clear(); }   // a new op invalidates redo
    private void CapturePrev() => CapturePrev(_editList);
    private void CapturePrev(IReadOnlyList<WheelSlice> list)
    {
        int n = list.Count;
        _prevCenter = new Dictionary<WheelSlice, double>(n, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < n; i++) _prevCenter[list[i]] = SliceCenterDeg(i, n);
    }
    private void StartReflow()      { StructureVersion++; BeginReflowClock(); }
    // Animate the reflow WITHOUT marking a structural change — used by carry-preview moves, which must
    // flow the wheel but must NOT trip live-save (only the committed DropCarried persists).
    // Reduce Motion (MotionPolicy): structural edits become instant state changes — the reflow/landing
    // clocks never start, so ReflowProgress reads 1.0, Settling stays false (no despawn hold), and the
    // ghost / pop-in treatments never draw. The edit itself is identical either way.
    private void BeginReflowClock()
    {
        if (MotionPolicy.Reduce) { ClearAnim(); return; }
        _reflowStart = NowMs; _reflowActive = true;
    }
    private void StartLanding(int index)
    {
        if (MotionPolicy.Reduce) return;
        _landingIndex = index; _landingStart = NowMs;
    }
    // Note: landing is NOT cleared here — it self-expires by time, and ClearAnim fires when the reflow
    // clock settles (which can be MID-landing). It's reset only at edit-session boundaries.
    private void ClearAnim()   { _prevCenter = null; _reflowActive = false; _ghost = null; _popInIndex = -1; }

    private static bool RefSequenceEqual(IReadOnlyList<WheelSlice> a, IReadOnlyList<WheelSlice> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }

    // ── Hold-to-confirm dwell (one render-timer tick) ───────────────────────────
    public WheelTickResult Tick(bool isVisible)
    {
        bool invalidate = false;

        if (!isVisible)
        {
            if (_confirmIndex != -1 || _confirmProgress > 0 || _confirmDone)
                { _confirmIndex = -1; _confirmProgress = 0; _confirmDone = false; }
            // Also drop a held-□ delete dwell: while hidden no □-release arrives, so a partial dwell would
            // otherwise RESUME on re-show and could delete a slice the user didn't intend (e.g. the wheel
            // parked/hidden mid-hold). The carry ("in hand") state is intentionally left — a transient hide
            // shouldn't drop a slice the user is placing; it resumes visibly.
            if (_deleteHeld || _deleteProgress > 0) { _deleteHeld = false; _deleteProgress = 0; }
            return new WheelTickResult(false);
        }

        // Edit mode runs its own tick (delete dwell + reflow); it never fires actions or hold-to-delete.
        if (_editMode)
        {
            bool inval = false;
            if (_editPhase == EditPhase.Selecting && _deleteHeld && _armedIndex >= 0)
            {
                _deleteProgress += ConfirmTickMs / DeleteConfirmMs;
                if (_deleteProgress >= 1.0) { _deleteProgress = 0; _deleteHeld = false; DeleteAt(_armedIndex); }
                inval = true;
            }
            else if (_deleteProgress != 0) { _deleteProgress = 0; inval = true; }

            if (_reflowActive)
            {
                if (NowMs - _reflowStart >= ReflowMs) ClearAnim();
                inval = true;
            }
            if (Landing) inval = true;        // keep repainting through the landing settle
            return new WheelTickResult(inval);
        }

        int armed = _armedIndex;

        if (armed >= 0 && RequiresConfirm(armed))
        {
            if (_confirmIndex != armed) { _confirmIndex = armed; _confirmProgress = 0; _confirmDone = false; }
            if (!_confirmDone)
            {
                // In normal mode reaching 1.0 only UNLOCKS the slice (fires on Fn release —
                // see ArmedConfirmReady). In assign mode there's no Fn, so completing AUTO-FIRES.
                _confirmProgress += ConfirmTickMs / ConfirmHoldMs;
                if (_confirmProgress >= 1.0)
                {
                    _confirmProgress = 1.0;
                    _confirmDone = true;
                }
                invalidate = true;
            }
        }
        else if (_confirmIndex != -1 || _confirmProgress > 0 || _confirmDone)
        {
            _confirmIndex = -1; _confirmProgress = 0; _confirmDone = false;
            invalidate = true;
        }

        return new WheelTickResult(invalidate);
    }

    public void Reset()
    {
        _stickX = _stickY = 0;
        _smoothX = _smoothY = 0;
        _armedIndex = -1;
        _armedPinned = false;
        _stickyArmed = -1;
        _confirmIndex = -1;
        _confirmProgress = 0;
        _confirmDone = false;
        _editMode = false;
        _editPhase = EditPhase.Selecting;
        _carryIndex = _carryTarget = -1;
        _deleteHeld = false;
        _deleteProgress = 0;
        _undo.Clear();
        _redo.Clear();
        ClearAnim();
        FreezeArmed = false;
    }
}
