using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ControllerWheel;

public partial class OverlayWindow : Window
{
    private DispatcherTimer? _topmostTimer;
    private IntPtr _hwnd;

    public OverlayWindow()
    {
        InitializeComponent();
        CoverPrimaryScreen();
        // The hub behind the arcade follows the disc as it grows into a game or shrinks back to the
        // cabinets — the same frame, the same easing, since it is driven by the disc's own value.
        Arcade.SurfaceChanging += toGame => ArcadeSurfaceChanging?.Invoke(toGame);
        Arcade.DiscRadiusChanged += r =>
        {
            if (Menu.ArcadeCollapsing) Menu.SetArcadeHubRadius(ArcadeHubRadiusFor(r, ArcadeHubOversize));
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int ArmedIndex                                    => Menu.ArmedIndex;
    /// <summary>Show / clear a live state preview in the hub for the armed toggle slice.</summary>
    public void ShowPreview(string line1, string line2, string? line2Next = null) => Menu.ShowStatus(line1, line2, line2Next);
    public void ClearPreview()                               => Menu.ClearStatus();
    /// <summary>Show a hub hint that the armed slice still needs setting up (missing required value).</summary>
    public void ShowConfigureNotice()                        => Menu.ShowNotice(Loc.T(UiText.Overlay.ConfigureInSettings));
    /// <summary>Show an arbitrary hub title-chip notice (e.g. onboarding's persistent "Practice").</summary>
    public void ShowHubNotice(string text, RadialMenuControl.NoticeKind kind = RadialMenuControl.NoticeKind.Plain)
                                                              => Menu.ShowNotice(text, kind);
    /// <summary>Armed slice for fire-on-release, with a short sticky grace window.</summary>
    public int StickyArmedIndex                              => Menu.StickyArmedIndex;
    /// <summary>Sticky grace window (ms) for fire-on-release. Driven by SystemConfig.</summary>
    public long StickyMs { get => Menu.StickyMs; set => Menu.StickyMs = value; }
    public void SetSlices(IReadOnlyList<WheelSlice> slices) => Menu.Slices = slices;
    /// <summary>Which wheel is on screen — salvage gives each wheel its own glyph-tilt table.</summary>
    public bool IsWheelB { get => Menu.IsWheelB; set => Menu.IsWheelB = value; }
    public void Reset()                                      => Menu.Reset();
    public void UpdateStick(float x, float y)               => Menu.UpdateStick(x, y);

    private const double IntroStartScale = 0.85;   // wheel grows from 85% → 100% on entry
    private double _introDx, _introDy, _introOriginX = 0.5;

    /// <summary>Reduce Motion: the wheel FADES in in place — no grow, no drift. The effective intro
    /// offsets/scale collapse to identity, so PlayIntro's transform animations become no-ops and only
    /// the opacity ramp (same duration) remains.</summary>
    private bool ReducedIntro => Menu.ReduceMotion;
    private double IntroScale => ReducedIntro ? 1.0 : IntroStartScale;

    /// <summary>Set the wheel's pre-show state (transparent, scaled down, offset toward centre).
    /// <paramref name="originX"/> (0..1) biases the scale origin so the wheel appears to bloom from
    /// that side. Call BEFORE Show() so the first presented frame is already invisible.</summary>
    public void PrepareIntro(double dx, double dy, double originX = 0.5)
    {
        if (ReducedIntro) { dx = 0; dy = 0; }   // fade in-place: no drift toward/from screen centre
        _introDx = dx; _introDy = dy; _introOriginX = originX;
        // Clear any in-flight fade and reset both layers, so a fresh pull starts clean even if
        // the previous wheel was mid fade-out / mid status-linger.
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.BeginAnimation(RadialMenuControl.RingOpacityProperty, null);
        Menu.BeginAnimation(RadialMenuControl.CenterOpacityProperty, null);
        Menu.BeginAnimation(RadialMenuControl.SelectedSliceOpacityProperty, null);
        Menu.RingOpacity = 1;
        Menu.CenterOpacity = 1;
        Menu.SelectedSliceOpacity = 1;
        Menu.ClearFiringSlice();
        Menu.ClearStatus();
        Menu.Opacity = 0;
        Menu.RenderTransformOrigin = new Point(originX, 0.5);   // bloom from the Fn-key side
        Menu.RenderTransform = new TransformGroup
        {
            Children = { new TranslateTransform(dx, dy), new ScaleTransform(IntroScale, IntroScale) },
        };
    }

    /// <summary>Fade the wheel in from transparent while it grows + drifts into place. Call AFTER
    /// Show(). The scale rides the same duration as the fade — no extra time.</summary>
    public void PlayIntro(int fadeMs)
    {
        NudgeTopmost();
        _parkGen++;   // void any pending delayed park (kawaii confetti hold) — this is a fresh show
        Menu.SetWheelLive(true);   // wheel is genuinely on screen now (drives kawaii's ambient twinkle)
        if (fadeMs <= 0)
        {
            Menu.BeginAnimation(UIElement.OpacityProperty, null);
            Menu.Opacity = 1;
            Menu.RenderTransform = null;
            return;
        }

        var dur  = TimeSpan.FromMilliseconds(fadeMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var group = Menu.RenderTransform as TransformGroup ?? new TransformGroup
        {
            Children = { new TranslateTransform(_introDx, _introDy), new ScaleTransform(IntroScale, IntroScale) },
        };
        Menu.RenderTransform = group;
        var tt = (TranslateTransform)group.Children[0];
        var st = (ScaleTransform)group.Children[1];

        Menu.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, dur));
        if (!ReducedIntro)   // Reduce Motion: opacity only — the transforms are already identity
        {
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(_introDx, 0, dur) { EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(_introDy, 0, dur) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(IntroStartScale, 1, dur) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(IntroStartScale, 1, dur) { EasingFunction = ease });
        }
    }

    // ── Onboarding practice stick art (OOBE step 2 only) ─────────────────────
    // The which-stick art draws INSIDE the hub, under the "Practice" pill (RadialMenuControl.DrawNotice).
    // App flips the gate via this hook, wired to the wizard's SetSampleWheels hook — true exactly for the
    // practice step's lifetime, so the art can't appear anywhere else.

    /// <summary>Gate the practice-step stick art on/off (App wires this to onboarding's practice step).</summary>
    public void SetOnboardingPracticeToast(bool on) => Menu.SetOnboardingPracticeArt(on);

    // "Toggle Bluetooth" dead-link notice lives in the TRAY, not here — App.StartController raises a
    // tray balloon on ControllerReader.BtLinkDead's dead edge. Don't draw it on this canvas.

    public event Action<int>? SliceClicked
    {
        add    => Menu.SliceClicked += value;
        remove => Menu.SliceClicked -= value;
    }

    /// <summary>True when the armed require-confirm slice's hold dwell is complete, so releasing
    /// Fn while it stays selected should fire it.</summary>
    public bool ArmedConfirmReady => Menu.ArmedConfirmReady;

    /// <summary>The armed slice is guarded, so its arm cue waits for the dwell (see
    /// RadialMenuControl.ArmedRequiresConfirm).</summary>
    public bool ArmedRequiresConfirm => Menu.ArmedRequiresConfirm;

    /// <summary>Raised when a guarded slice's hold dwell completes (see RadialMenuControl.ConfirmReady).</summary>
    public event Action? ConfirmReady
    {
        add    => Menu.ConfirmReady += value;
        remove => Menu.ConfirmReady -= value;
    }

    /// <summary>Mark the armed slice as the one being fired, so the fade holds it slightly longer
    /// than the rest of the wheel. Call before Reset() clears the armed state.</summary>
    public void MarkFiringSlice(bool celebrate = true) => Menu.MarkFiringSlice(celebrate);

    /// <summary>Spawn the fire celebration before the action runs (see RadialMenuControl.CelebrateFire).</summary>
    public void CelebrateFire() => Menu.CelebrateFire();

    /// <summary>
    /// When true, removes WS_EX_TRANSPARENT so mouse clicks reach the overlay.
    /// Transparent canvas areas still pass through — only the radial control area is hit-testable.
    /// </summary>
    public void SetClickable(bool clickable)
    {
        if (_hwnd == IntPtr.Zero) return;
        int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        if (clickable) ex &= ~NativeMethods.WS_EX_TRANSPARENT;
        else           ex |=  NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, ex);
    }

    /// <summary>Raised whenever the wheel parks fully hidden — the end of a summon, after any fade-out.</summary>
    public event Action? Parked;

    /// <summary>Park the wheel fully hidden and ready for the next intro: stop every fade,
    /// reset all layer opacities, clear the firing slice, and clear any status readout. Keeping
    /// the window shown avoids the DWM presenting the previous wheel's last frame on the next
    /// Show (which flickered the opposite wheel/position when switching A↔B).</summary>
    public void HideWheel()
    {
        Parked?.Invoke();
        _parkGen++;   // this park is happening now — cancel any pending delayed one
        Menu.SetWheelLive(false);   // parked: stop the ambient-twinkle frames
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.BeginAnimation(RadialMenuControl.RingOpacityProperty, null);
        Menu.BeginAnimation(RadialMenuControl.CenterOpacityProperty, null);
        Menu.BeginAnimation(RadialMenuControl.SelectedSliceOpacityProperty, null);
        Menu.Opacity = 0;
        Menu.RingOpacity = 1;
        Menu.CenterOpacity = 1;
        Menu.SelectedSliceOpacity = 1;
        Menu.ClearFiringSlice();
        Menu.ClearStatus();
        Scrim.BeginAnimation(UIElement.OpacityProperty, null);   // never leave the edit scrim up
        Scrim.Opacity = 0;
    }

    // ── Selection fade-out ─────────────────────────────────────────────────────

    // The just-selected slice holds at full opacity for this long, then fades, so it lingers
    // slightly longer than the rest of the wheel.
    private const int SelectedSliceLagMs = 160;

    /// <summary>Hold the selected slice at full opacity for <see cref="SelectedSliceLagMs"/>, then
    /// fade it over <paramref name="fadeMs"/>. Returns the animation so a caller can hook Completed.</summary>
    private DoubleAnimationUsingKeyFrames AnimateSelectedSlice(int fadeMs)
    {
        var sel = new DoubleAnimationUsingKeyFrames();
        sel.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        sel.KeyFrames.Add(new LinearDoubleKeyFrame(1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(SelectedSliceLagMs))));
        sel.KeyFrames.Add(new LinearDoubleKeyFrame(0,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(SelectedSliceLagMs + fadeMs))));
        return sel;
    }

    /// <summary>Quickly fade the whole wheel out, then park. Used for a cancel (Fn released with
    /// nothing selected) — no selected-slice lag, since nothing was picked.</summary>
    public void FadeOutCancel(int ms = 100)
    {
        Menu.RenderTransform = null;
        Menu.BeginAnimation(RadialMenuControl.RingOpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms)));
        var centre = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms));
        // Generation-guarded like FadeOutWheel: a Completed already QUEUED on the dispatcher still runs
        // after PrepareIntro removes the animation, so an unguarded HideWheel here could park a wheel
        // that was re-shown in the same instant — invisible but still open/functional (the rapid
        // re-invoke bug). The gen check makes the stale park a no-op.
        int gen = ++_parkGen;
        centre.Completed += (_, _) => { if (gen == _parkGen) HideWheel(); };
        Menu.BeginAnimation(RadialMenuControl.CenterOpacityProperty, centre);
    }

    /// <summary>Fade the wheel out over <paramref name="ms"/>, then park. The selected slice
    /// lingers slightly longer than the rest. Used for an ordinary (non-toggle) selection.</summary>
    public void FadeOutWheel(int ms = 100)
    {
        Menu.RenderTransform = null;
        Menu.BeginAnimation(RadialMenuControl.RingOpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms)));
        Menu.BeginAnimation(RadialMenuControl.CenterOpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms)));

        // Selected slice is the longest-lived element, so park when it finishes — unless kawaii's fire
        // confetti is still in flight (it outlives the ~200ms fade by design); then park when it drains.
        // The generation counter voids a pending delayed park if the wheel is re-shown in the meantime.
        var sel = AnimateSelectedSlice(ms);
        int gen = ++_parkGen;
        sel.Completed += (_, _) =>
        {
            if (gen != _parkGen) return;
            int hold = Menu.FireFxRemainingMs;
            if (hold <= 0) { HideWheel(); return; }
            // POLL rather than sleeping the full hold: that figure is the effect's worst-case backstop, but
            // both bursts normally drain early (confetti pieces leave as they fall off-screen, sparks as they
            // burn out), and parking on the backstop would hold an empty overlay up on every fire.
            var deadline = DateTime.UtcNow.AddMilliseconds(hold + 40);
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            t.Tick += (_, _) =>
            {
                if (Menu.FireFxRemainingMs > 0 && DateTime.UtcNow < deadline) return;
                t.Stop();
                if (gen == _parkGen) HideWheel();
            };
            t.Start();
        };
        Menu.BeginAnimation(RadialMenuControl.SelectedSliceOpacityProperty, sel);
    }

    // Bumped on every show/park/fade-out so a stale park — a pending confetti-hold timer OR an
    // animation-Completed that was already queued when a fresh show superseded its fade — can never
    // hide a freshly re-opened wheel. Every delayed HideWheel must check its captured gen first.
    private int _parkGen;

    /// <summary>Toggle selection: show the state readout in the centre, fade the ring out over
    /// <paramref name="ringMs"/> (selected slice lingering slightly longer), hold the centre for
    /// <paramref name="centreDelayMs"/>, then fade the centre over <paramref name="centreMs"/> and park.</summary>
    public void FadeRingHoldCenter(string line1, string line2,
        int ringMs = 100, int centreDelayMs = 500, int centreMs = 100, ImageSource? icon = null,
        string? caption = null)
    {
        Menu.ShowStatus(line1, line2, icon: icon, caption: caption);
        Menu.RenderTransform = null;

        Menu.BeginAnimation(RadialMenuControl.RingOpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ringMs)));
        Menu.BeginAnimation(RadialMenuControl.SelectedSliceOpacityProperty, AnimateSelectedSlice(ringMs));

        // Hold the centre at full opacity for the delay, then ramp it to 0; it outlives the ring,
        // so park when it finishes.
        var centre = new DoubleAnimationUsingKeyFrames();
        centre.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        centre.KeyFrames.Add(new LinearDoubleKeyFrame(1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(centreDelayMs))));
        centre.KeyFrames.Add(new LinearDoubleKeyFrame(0,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(centreDelayMs + centreMs))));
        int gen = ++_parkGen;   // same stale-park guard as FadeOutCancel — see the comment there
        centre.Completed += (_, _) => { if (gen == _parkGen) HideWheel(); };
        Menu.BeginAnimation(RadialMenuControl.CenterOpacityProperty, centre);
    }

    // ── Game browser ────────────────────────────────────────────────────────
    public event Action<InstalledGame>? GameChosen
    {
        add    => Games.GameChosen += value;
        remove => Games.GameChosen -= value;
    }

    public event Action<string>? LauncherChosen
    {
        add    => Games.LauncherChosen += value;
        remove => Games.LauncherChosen -= value;
    }

    /// <summary>Raised when a storefront card's hold-to-hide completes. Arg = the
    /// InstalledGame.Storefront name; App persists it.</summary>
    public event Action<string>? StorefrontHideRequested
    {
        add    => Games.StorefrontHideRequested += value;
        remove => Games.StorefrontHideRequested -= value;
    }

    public InstalledGame? SelectedGame => Games.SelectedGame;

    /// <summary>✕ in the browser: apply the selected launcher's filter, or launch the focused
    /// game / "Open …" tile (the control raises GameChosen / LauncherChosen as appropriate).</summary>
    public void BrowserActivate() => Games.Activate();

    /// <summary>Cycle the game-browser launcher filter (L1/R1).</summary>
    public void CycleLauncherFilter(int dir) => Games.CycleFilter(dir);

    /// <summary>Select/Start in the grid: cycle the selected game's cover art / logo.</summary>
    public void CycleCover() => Games.CycleCover();
    public void CycleLogo()  => Games.CycleLogo();

    /// <summary>△ in the grid: toggle the selected game's favorite pin.</summary>
    public void GamesToggleFavorite() => Games.ToggleFavorite();

    /// <summary>□ in the grid: hold-to-confirm hide of the selected game — or, on a storefront card, of
    /// that whole storefront (see GameBrowserControl.SetHideHeld).</summary>
    public void GamesSetHideHeld(bool pressed) => Games.SetHideHeld(pressed);

    /// <summary>○ in the grid: dismiss a pending storefront hide-confirm toast. Returns true when it
    /// consumed the press — the host only treats ○ as "close the grid" when this says no toast was up.</summary>
    public bool GamesCancelHideConfirm() => Games.CancelHideConfirm();

    /// <summary>Show the browser card (loading state). Fills the window; the card centres on screen at
    /// ~75%×80% of it.</summary>
    public void ShowGamesPanel()
    {
        NudgeTopmost();
        // Drop any completed-but-holding fade clock (PlayIntro / picker drill-in) first — a holding
        // animation masks the local value and the suspended wheel would stay visible under the browser.
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.SetWheelLive(false);   // wheel is behind the browser — no ambient-twinkle frames
        Menu.Opacity = 0; // hide the wheel while the browser is up
        // The window covers exactly the primary display, so the panel just fills it from the origin.
        var scr = NativeMethods.PrimaryScreenDips();
        System.Windows.Controls.Canvas.SetLeft(Games, 0);
        System.Windows.Controls.Canvas.SetTop(Games,  0);
        Games.Width  = scr.Width;
        Games.Height = scr.Height;
        Games.SetCardSize(scr.Width * 0.75, scr.Height * 0.80);
        Games.SetLoading();
        Games.Visibility = Visibility.Visible;
    }

    /// <summary>Apply the configured Game Grid material to the card.</summary>
    public void SetGamesMaterial(string? material) => Games.SetMaterial(material);

    /// <summary>Reduce Motion — pins the Game Grid's Reactor-material circuit-board parallax still;
    /// see GameBrowserControl.ReduceMotion.</summary>
    public void SetGamesReduceMotion(bool on) => Games.ReduceMotion = on;

    public void SetGames(IReadOnlyList<InstalledGame> games, IReadOnlyCollection<string>? disabledStores = null)
    {
        if (disabledStores is not null) Games.DisabledStorefronts = disabledStores;
        Games.Load(games);
    }
    public void HideGames()           => Games.Visibility = Visibility.Collapsed;

    /// <summary>Toggle the grid's footer hints for edit-mode game-pick (✕ "Add to Wheel", no △ assign).</summary>
    public void SetGamesPickMode(bool pick) => Games.SetPickMode(pick);

    public void ShowGamesTip()    => Games.ShowTip();
    public void DismissGamesTip() => Games.DismissTip();

    /// <summary>Bring the wheel layer back to full opacity after the game-pick grid closes — the wheel
    /// kept its (edit) state while hidden, so this just fades it back in.</summary>
    public void ResumeWheelLayer(int ms)
    {
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.RenderTransform = null;
        if (ms <= 0) { Menu.Opacity = 1; return; }
        Menu.Opacity = 0;
        Menu.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms)));
    }
    public void GamesMove(int dx, int dy) => Games.Move(dx, dy);

    /// <summary><paramref name="glyph"/> is the MaterialDesign icon captioning which level this is
    /// ("VolumeHigh" for system output, "Microphone" for the mic-volume D-pad ◀▶ mode) — see
    /// <see cref="RadialMenuControl.ScrubberGlyph"/>. The scrubber and the large hub glyph are the SAME
    /// momentary hub slot and share App's 800 ms hide timer, so this owns both: showing a scrub clears a
    /// standing skip glyph, and <c>active: false</c> (the hide timer, and the overlay teardown) clears
    /// whichever of the two is up.</summary>
    public void SetScrubber(bool active, float level, string? glyph = null)
    {
        Menu.ScrubberActive = active;
        Menu.ScrubberLevel  = level;
        Menu.ScrubberGlyph  = active ? glyph : null;
        Menu.HubGlyph       = null;
        Menu.InvalidateVisual();
    }

    /// <summary>Show a single large glyph in the hub (track skip) — see
    /// <see cref="RadialMenuControl.HubGlyph"/>. Null hides it.</summary>
    public void ShowHubGlyph(string? glyph)
    {
        Menu.HubGlyph = glyph;
        if (glyph is not null) { Menu.ScrubberActive = false; Menu.ScrubberGlyph = null; }
        Menu.InvalidateVisual();
    }

    /// <summary>Volume-mixer D-pad balance readout — see <see cref="RadialMenuControl.ShowMixIndicator"/>.
    /// Only drawn on the primary wheel (Menu), matching SetScrubber's pattern above.</summary>
    public void ShowMixIndicator(double balance01, string? leftName, string? rightName, bool noDevice = false) =>
        Menu.ShowMixIndicator(balance01, leftName, rightName, noDevice);

    public void HideMixIndicator() => Menu.HideMixIndicator();

    public void SetBattery(int percent, bool charging) => Menu.SetBattery(percent, charging);

    /// <summary>Slice-ring thickness ("thick"/"medium"/"thin").</summary>
    public void SetSliceThickness(string? thickness) => Menu.SetSliceThickness(thickness);

    /// <summary>Global slice-label mode ("all-except-logos"/"all"/"none"/"selected").</summary>
    public void SetShowSliceLabels(string? mode) => Menu.SetShowSliceLabels(mode);

    /// <summary>Resting-slice material ("flat-light"/"pearl"/"flat-dark"/"obsidian").</summary>
    public void SetSliceMaterial(string? material) => Menu.SetSliceMaterial(material);

    /// <summary>"Always show hub" — when true the hub circle is drawn on every material; when false, hub
    /// visibility is per-material (terra/reactor always; others only when the hub carries info).</summary>
    public void SetAlwaysShowHub(bool on) => Menu.AlwaysShowHub = on;

    /// <summary>Reduce Motion — pins the Reactor material's circuit-board parallax still;
    /// see RadialMenuControl.ReduceMotion.</summary>
    public void SetReduceMotion(bool on) => Menu.ReduceMotion = on;

    /// <summary>Reduce Motion for the arcade's own chrome (the picker carousel); see ArcadeControl.ReduceMotion.</summary>
    public void SetArcadeReduceMotion(bool on) => Arcade.ReduceMotion = on;


    // ── In-wheel editing (V2) — delegates to the active single wheel (Menu) ──────
    public bool EditMode => Menu.EditMode;
    public WheelStateMachine.EditPhase EditPhase => Menu.EditPhase;
    public bool EditCanUndo => Menu.EditCanUndo;
    public bool EditCanRedo => Menu.EditCanRedo;
    public int  EditMaxSlices { get => Menu.EditMaxSlices; set => Menu.EditMaxSlices = value; }
    public double EditDeleteHoldMs { get => Menu.EditDeleteHoldMs; set => Menu.EditDeleteHoldMs = value; }
    public bool EditPickerLatchesAtCentre { get => Menu.EditPickerLatchesAtCentre; set => Menu.EditPickerLatchesAtCentre = value; }
    public WheelSlice[] EditCurrentSlices => Menu.EditCurrentSlices;
    public int EditCarrySlot => Menu.EditCarrySlot;
    public event Action? EditStructureChanged
    {
        add    => Menu.EditStructureChanged += value;
        remove => Menu.EditStructureChanged -= value;
    }
    public void BeginEdit(IReadOnlyList<WheelSlice> slices) => Menu.BeginEdit(slices);
    public WheelSlice[] EndEdit()        => Menu.EndEdit();
    public void EditPickUp()             => Menu.EditPickUp();
    public bool EditDrop()               => Menu.EditDrop();
    public void EditCancelCarry()        => Menu.EditCancelCarry();
    public bool EditNudge(int dir)       => Menu.EditNudge(dir);
    public void EditSetDeleteHeld(bool held) => Menu.EditSetDeleteHeld(held);
    public int  EditAddAndCarry(WheelSlice s) => Menu.EditAddAndCarry(s);
    public bool EditUndo()               => Menu.EditUndo();
    public bool EditRedo()               => Menu.EditRedo();
    public void ShowPicker(IReadOnlyList<WheelSlice> menu) => Menu.SetPicker(menu);
    public void EndPicker()              => Menu.EndPicker();
    public bool EditSettling             => Menu.EditSettling;

    // Drill animation: each picker level grows in from the centre; backing out shrinks it back.
    private const double PickerZoom = 0.45;

    public void PickerDrillIn(int ms)
    {
        // Reduce Motion: drill-in loses its zoom — same fade the back-out path already uses.
        if (Menu.ReduceMotion) { PickerFadeIn(ms); return; }
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        if (ms <= 0) { Menu.RenderTransform = null; Menu.Opacity = 1; return; }
        Menu.RenderTransformOrigin = new Point(0.5, 0.5);
        var st = new ScaleTransform(PickerZoom, PickerZoom);
        Menu.RenderTransform = st;
        Menu.Opacity = 0;
        var dur = TimeSpan.FromMilliseconds(ms);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Menu.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, dur));
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(PickerZoom, 1, dur) { EasingFunction = ease });
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(PickerZoom, 1, dur) { EasingFunction = ease });
    }

    /// <summary>Fade a level in without scaling — used when backing OUT to a parent level (grow is
    /// reserved for drilling in).</summary>
    public void PickerFadeIn(int ms)
    {
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.RenderTransform = null;
        if (ms <= 0) { Menu.Opacity = 1; return; }
        Menu.Opacity = 0;
        Menu.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms)));
    }

    public void PickerDrillOut(int ms, Action onDone)
    {
        if (ms <= 0) { onDone(); return; }
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        var dur = TimeSpan.FromMilliseconds(ms);
        var op = new DoubleAnimation(1, 0, dur);
        op.Completed += (_, _) => onDone();
        if (Menu.ReduceMotion)   // Reduce Motion: opacity-only — no shrink on the way out
        {
            Menu.RenderTransform = null;
            Menu.BeginAnimation(UIElement.OpacityProperty, op);
            return;
        }
        Menu.RenderTransformOrigin = new Point(0.5, 0.5);
        var st = new ScaleTransform(1, 1);
        Menu.RenderTransform = st;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        Menu.BeginAnimation(UIElement.OpacityProperty, op);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, PickerZoom, dur) { EasingFunction = ease });
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, PickerZoom, dur) { EasingFunction = ease });
    }

    /// <summary>Slide the active wheel horizontally to the centre of the (primary) screen
    /// (on entering edit mode).</summary>
    public void SlideToCenter(int ms)
    {
        var scr = NativeMethods.PrimaryScreenDips();
        double targetX = scr.Width / 2.0 - Menu.Width / 2.0;
        double curX = System.Windows.Controls.Canvas.GetLeft(Menu);
        if (Menu.ReduceMotion) ms = 0;   // Reduce Motion: the wheel repositions instantly, no travel
        if (double.IsNaN(curX) || ms <= 0)
        {
            Menu.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, null);
            System.Windows.Controls.Canvas.SetLeft(Menu, targetX);
            return;
        }
        if (Math.Abs(curX - targetX) < 0.5) return;
        var anim = new DoubleAnimation(curX, targetX, TimeSpan.FromMilliseconds(ms))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Menu.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, anim);
    }

    // ── Arcade ──────────────────────────────────────────────────────────────

    /// <summary>Diameter the LAUNCHER's disc takes at 100% scale: the wheel's own visible disc. The wheel
    /// dissolves and the cabinets bloom in exactly its place, which is the whole conceit — so this is derived
    /// from <see cref="RadialMenuControl.OuterRadius"/> rather than being its own number. A game's disc is
    /// <see cref="ArcadeTuning.GameDiscScale"/> times this, and the window is laid out for the game.</summary>
    private const double ArcadeBaseDiameter = RadialMenuControl.OuterRadius * 2;

    /// <summary>How much bigger than the playfield the hub sits behind it (105%).</summary>
    public const double ArcadeHubOversize = 1.05;

    /// <summary>The arcade disc's size is fixed relative to the wheel: there is no user-facing scale setting.</summary>
    private const int ArcadeScalePercent = 100;

    /// <summary>Show the arcade, centred on <paramref name="screenX"/>/<paramref name="screenY"/> (pass the
    /// wheel's centre). <paramref name="guard"/> non-null shows the bleed-through card instead of the game.</summary>
    public void ShowArcade(string gameId, double screenX, double screenY,
                           Arcade.GuardCopy? guard, bool overridable, bool fromLauncher = false)
    {
        NudgeTopmost();
        // Same treatment the Game Grid gives the wheel layer: drop any holding fade animation first, or the
        // suspended wheel stays painted underneath.
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.SetWheelLive(false);
        // ⚠ NOT zeroed while the arcade collapse is holding: the grown hub is the game's backdrop and has
        // to keep painting underneath it for the whole session. The wheel is already dead to input by this
        // point — SetWheelLive(false) above — so what stays on screen is just the hub.
        Menu.Opacity = Menu.ArcadeCollapsing ? 1 : 0;

        double d = ArcadeDiameterFor();
        // Never larger than the primary display's short edge, whatever the scale says — a disc taller than
        // the screen would be clipped by the window and read as a broken frame.
        var scr = NativeMethods.PrimaryScreenDips();
        Arcade.Width  = d;
        Arcade.Height = d;
        // Keep the disc fully on the primary display even when the wheel it came from sits near an edge.
        double cx = Math.Clamp(screenX, d / 2, Math.Max(d / 2, scr.Width  - d / 2));
        double cy = Math.Clamp(screenY, d / 2, Math.Max(d / 2, scr.Height - d / 2));
        CenterControl(Arcade, cx, cy);
        // Move the (grown) hub to wherever the disc actually landed. Near a screen edge the clamp above
        // shifts the game off the wheel's centre, and a backdrop that stayed put would sit visibly askew.
        if (Menu.ArcadeCollapsing) CenterControl(Menu, cx, cy);

        Arcade.Open(gameId, guard, overridable, fromLauncher);
    }

    /// <summary>A GAME's disc diameter — also the size the arcade control is laid out at. Shared with the
    /// caller so the hub can be grown to match BEFORE the disc exists — see <see cref="BeginArcadeCollapse"/>.</summary>
    public double ArcadeDiameterFor()
    {
        double d = ArcadeBaseDiameter * ArcadeScalePercent / 100.0 * Math.Max(1, ArcadeTuning.GameDiscScale);
        // Never larger than the primary display's short edge — a disc taller than the screen would be
        // clipped by the window and read as a broken frame.
        var scr = NativeMethods.PrimaryScreenDips();
        return Math.Min(d, Math.Min(scr.Width, scr.Height) * 0.96);
    }

    /// <summary>The LAUNCHER's disc diameter: the game's over <see cref="ArcadeTuning.GameDiscScale"/>, so
    /// the ratio holds even when the screen cap has bitten the game's size.</summary>
    public double ArcadeLauncherDiameterFor() =>
        ArcadeDiameterFor() / Math.Max(1, ArcadeTuning.GameDiscScale);

    /// <summary>Sweep the wheel's slices in behind the hub and grow the hub to sit behind the game.
    /// <paramref name="onDone"/> fires when the hub has finished growing — that's when the host
    /// shows the disc.</summary>
    public void BeginArcadeCollapse(int index, double hubRadius, Action onDone)
    {
        Menu.BeginAnimation(UIElement.OpacityProperty, null);
        Menu.Opacity = 1;
        Menu.SetWheelLive(false);   // dead to input the instant the sweep starts, still painting
        // Firing a slice normally kicks off the wheel's dissolve — the ring, the centre and the chosen slice
        // each fade on their own clock. All three must be cancelled and pinned back to full here, or the
        // sweep plays out inside a wheel that is already fading and the slices never reach the hub visibly.
        foreach (DependencyProperty p in new[]
                 { RadialMenuControl.RingOpacityProperty,
                   RadialMenuControl.CenterOpacityProperty,
                   RadialMenuControl.SelectedSliceOpacityProperty })
        {
            Menu.BeginAnimation(p, null);
            Menu.SetValue(p, 1.0);
        }
        Menu.BeginArcadeCollapse(index, hubRadius, onDone);
    }

    public bool ArcadeCollapsing => Menu.ArcadeCollapsing;

    /// <summary>Hub radius that leaves the hub's SMALLEST silhouette clearing a playfield of
    /// <paramref name="playfieldRadius"/> by <paramref name="oversize"/>. On a scalloped material (kawaii's
    /// cloud) that is meaningfully larger than the plain product — see
    /// <see cref="RadialMenuControl.HubCoverageFraction"/>.</summary>
    public double ArcadeHubRadiusFor(double playfieldRadius, double oversize) =>
        playfieldRadius * oversize / Math.Max(0.5, Menu.HubCoverageFraction);

    /// <summary>Slide the arcade window (and the grown hub behind it, while the collapse holds) to a new centre
    /// over <paramref name="ms"/> with an ease in and out; Reduce Motion or 0 ms snaps. The centre is clamped to
    /// the primary display the same way <see cref="ShowArcade"/> clamps it.</summary>
    public void SlideArcade(double screenX, double screenY, int ms)
    {
        var scr = NativeMethods.PrimaryScreenDips();
        double d = Arcade.Width;
        double cx = Math.Clamp(screenX, d / 2, Math.Max(d / 2, scr.Width  - d / 2));
        double cy = Math.Clamp(screenY, d / 2, Math.Max(d / 2, scr.Height - d / 2));
        if (Menu.ReduceMotion || Arcade.ReduceMotion) ms = 0;
        MoveTo(Arcade, cx, cy, ms);
        if (Menu.ArcadeCollapsing) MoveTo(Menu, cx, cy, ms);
    }

    /// <summary>Animate a canvas child's centre to a screen point (canvas coordinates are screen coordinates).
    /// Any running move is replaced from wherever it had got to, so a second press mid-slide bends the path
    /// rather than jumping.</summary>
    private static void MoveTo(System.Windows.FrameworkElement c, double screenX, double screenY, int ms)
    {
        double toX = screenX - c.Width / 2, toY = screenY - c.Height / 2;
        double fromX = System.Windows.Controls.Canvas.GetLeft(c), fromY = System.Windows.Controls.Canvas.GetTop(c);
        c.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, null);
        c.BeginAnimation(System.Windows.Controls.Canvas.TopProperty, null);
        if (ms <= 0 || double.IsNaN(fromX) || double.IsNaN(fromY))
        {
            System.Windows.Controls.Canvas.SetLeft(c, toX);
            System.Windows.Controls.Canvas.SetTop(c, toY);
            return;
        }
        // Pin the start where the dropped animation left the element, then run to the target.
        System.Windows.Controls.Canvas.SetLeft(c, fromX);
        System.Windows.Controls.Canvas.SetTop(c, fromY);
        var dur = TimeSpan.FromMilliseconds(ms);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        c.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, new DoubleAnimation(fromX, toX, dur) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        c.BeginAnimation(System.Windows.Controls.Canvas.TopProperty,  new DoubleAnimation(fromY, toY, dur) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        // FillBehavior.Stop hands the value back to the local property at the end, so the landing is written
        // here rather than held by a completed animation that a later SetLeft could not override.
        System.Windows.Controls.Canvas.SetLeft(c, toX);
        System.Windows.Controls.Canvas.SetTop(c, toY);
    }

    public void HideArcade()
    {
        Arcade.Close();
        Menu.EndArcadeCollapse();   // restore the normal hub, or the next wheel opens with a giant one
    }
    /// <summary>Restart the live game in place.</summary>
    public void ArcadeRestart() => Arcade.RestartLiveGame();
    /// <summary>START: open/close the pause menu.</summary>
    public void ArcadeTogglePause() => Arcade.TogglePause();
    public bool ArcadePaused => Arcade.Paused;
    /// <summary>○ inside the arcade: step back one layer of chrome (how-to card, then pause menu). False =
    /// nothing consumed it, so the caller should close the arcade.</summary>
    public bool ArcadeBackOut() => Arcade.BackOut();
    /// <summary>△: open the live game's how-to card, or put it away again.</summary>
    public void ArcadeToggleHowTo() => Arcade.ToggleHowTo();
    public bool ArcadeGuardShowing => Arcade.GuardShowing;
    public bool ArcadeOverrideAccepted => Arcade.OverrideAccepted;
    public void ArcadeShowGuard(Arcade.GuardCopy copy, bool overridable) => Arcade.ShowGuard(copy, overridable);
    public void ArcadeClearGuard() => Arcade.ClearGuard();
    /// <summary>Flush the frozen snapshot to disk without closing — used at app teardown.</summary>
    public void ArcadePersist()   => Arcade.Persist();

    public void ArcadeStick(float x, float y) => Arcade.SetStick(x, y);
    public void ArcadeCross(bool down)        => Arcade.SetCross(down);
    public void ArcadeTriangle(bool down)     => Arcade.SetTriangle(down);
    public void ArcadeSquare(bool down)       => Arcade.SetSquare(down);
    public void ArcadeSkip()                  => Arcade.SetSkip();
    public bool ArcadeIsPicker                => Arcade.IsPicker;
    /// <summary>The window-move cues on the bezel: which of the three positions the disc is at, and whether the
    /// chord's hold puts the move on the triggers. See <see cref="ArcadeControl.WindowPosition"/>.</summary>
    public int  ArcadeWindowPosition          { set => Arcade.WindowPosition = value; }
    public bool ArcadeSlideOnTriggers         { set => Arcade.SlideOnTriggers = value; }
    /// <summary>The control's <see cref="ArcadeControl.SurfaceChanging"/>, for the host to slide the window on.</summary>
    public event Action<bool>? ArcadeSurfaceChanging;
    /// <summary>Raw d-pad nibble from the controller (0 = up, clockwise in eighths, 8 = centred) — the
    /// control converts it to per-direction edges.</summary>
    public void ArcadeDPad(int nibble)        => Arcade.SetDPad(nibble);

    /// <summary>Raised when the guard card's △-hold override completes.</summary>
    public event Action? ArcadeOverrideGranted
    {
        add    => Arcade.OverrideGranted += value;
        remove => Arcade.OverrideGranted -= value;
    }

    /// <summary>Raised after the arcade closed itself on a render-path throw; the host must tear down its half.</summary>
    public event Action? ArcadeFailed
    {
        add    => Arcade.Failed += value;
        remove => Arcade.Failed -= value;
    }

    /// <summary>True when a physical-screen-pixel point lands outside the round arcade window — dismiss on an
    /// outside click, mirroring the wheel and the grid.</summary>
    public bool IsPointOutsideArcade(int screenPxX, int screenPxY) => Arcade.IsPointOutside(screenPxX, screenPxY);

    // ── Window setup ──────────────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;

        // Click-through + non-activating + hidden from Alt-Tab
        int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_TRANSPARENT
            | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_NOACTIVATE
            | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, ex);

        // Re-assert topmost every 400 ms while a surface is up — last topmost window wins on Win10/11. It
        // must not run for the life of the process (150 SetWindowPos calls a minute over a game for a fully
        // transparent, never-drawn window). Every show path calls NudgeTopmost, which re-asserts at once and
        // starts the timer; the timer parks itself once nothing is visible.
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _topmostTimer.Tick += (_, _) =>
        {
            ReAssertTopmost();
            if (!AnySurfaceShown) _topmostTimer.Stop();
        };
        ReAssertTopmost();
    }

    /// <summary>Something is (or is about to be) drawn: wheel, Game Grid, arcade or the edit scrim.</summary>
    private bool AnySurfaceShown =>
        Menu.Opacity > 0 || Menu.ArcadeCollapsing
        || Games.Visibility == Visibility.Visible
        || Arcade.Visibility == Visibility.Visible
        || Scrim.Opacity > 0;

    /// <summary>Re-assert topmost now and keep re-asserting while a surface is up. Called from every show
    /// path BEFORE its first frame, so the bloom never lands under the game.</summary>
    private void NudgeTopmost()
    {
        ReAssertTopmost();
        if (_topmostTimer is { IsEnabled: false }) _topmostTimer.Start();
    }

    private void ReAssertTopmost()
    {
        if (_hwnd == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Place the wheel so its centre is at (x, y) in primary-screen coordinates.
    /// Must be called before or after Show() — the window covers exactly the primary display
    /// (canvas origin == screen origin), so we just offset the control on the Canvas.
    /// </summary>
    public void CenterAt(double screenX, double screenY)
    {
        Menu.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, null);   // drop any edit-mode slide hold
        CenterControl(Menu, screenX, screenY);
    }

    private static void CenterControl(System.Windows.FrameworkElement c, double screenX, double screenY)
    {
        // A slide still running would override the values set below until it ended.
        c.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, null);
        c.BeginAnimation(System.Windows.Controls.Canvas.TopProperty, null);
        // Canvas origin == primary-screen top-left (0,0); screen coordinates ARE canvas coordinates.
        double canvasX = screenX - c.Width  / 2;
        double canvasY = screenY - c.Height / 2;
        System.Windows.Controls.Canvas.SetLeft(c, canvasX);
        System.Windows.Controls.Canvas.SetTop (c, canvasY);
    }

    // ── Wheel-layer reset (the dismissal paths) ─────

    /// <summary>Defensive reset of the wheel layer on the dismissal paths: state machine, animations,
    /// transform and opacity, whatever was up.</summary>
    public void ResetWheelLayer()
    {
        Menu.Reset();
        Menu.BeginAnimation(OpacityProperty, null);
        Menu.RenderTransform = null;
        Menu.Opacity = 0;
    }

    /// <summary>Size the window to cover exactly the PRIMARY display (origin (0,0), so canvas
    /// coordinates == primary-screen DIPs — no offset math). Primary-only is deliberate: keeping the
    /// window off secondary monitors is what guarantees pixel-exact rendering on mixed-DPI rigs — a
    /// virtual-desktop-spanning window gets ONE DWM DPI assignment and can be bitmap-stretched
    /// (see NativeMethods.PrimaryScreenDips).</summary>
    private void CoverPrimaryScreen()
    {
        var scr = NativeMethods.PrimaryScreenDips();
        Left   = 0;
        Top    = 0;
        Width  = scr.Width;
        Height = scr.Height;
        Scrim.Width  = Width;     // dim the whole (primary) screen
        Scrim.Height = Height;
    }

    // ── Edit-mode scrim (dims the whole screen behind the wheel) ─────────────────
    private const double ScrimOpacity = 0.35;

    public void ShowScrim(int ms)
    {
        NudgeTopmost();
        Scrim.BeginAnimation(UIElement.OpacityProperty, null);
        if (ms <= 0) { Scrim.Opacity = ScrimOpacity; return; }
        Scrim.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(Scrim.Opacity, ScrimOpacity, TimeSpan.FromMilliseconds(ms)));
    }

    public void HideScrim(int ms)
    {
        Scrim.BeginAnimation(UIElement.OpacityProperty, null);
        if (ms <= 0) { Scrim.Opacity = 0; return; }
        Scrim.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(Scrim.Opacity, 0, TimeSpan.FromMilliseconds(ms)));
    }

    /// <summary>Re-apply the window bounds to the current virtual-screen size. Call after a screen
    /// resolution / monitor-layout change so the wheel canvas isn't sized to stale dimensions
    /// (which clips the wheel).</summary>
    public void RefreshScreenBounds() => CoverPrimaryScreen();

    /// <summary>True when a physical-screen-pixel point (e.g. from a low-level mouse hook) lands OUTSIDE
    /// the active wheel's outer disc — used to dismiss the wheel on an outside click. PointFromScreen
    /// handles the DPI conversion, so this stays exact on any scale.</summary>
    public bool IsPointOutsideWheel(int screenPxX, int screenPxY)
    {
        try
        {
            var local = Menu.PointFromScreen(new Point(screenPxX, screenPxY));
            var center = new Point(Menu.ActualWidth / 2, Menu.ActualHeight / 2);
            double dx = local.X - center.X, dy = local.Y - center.Y;
            // A small margin past OuterRadius so a click right on the rim still counts as "on the wheel".
            const double edge = RadialMenuControl.OuterRadius + 6;
            return (dx * dx + dy * dy) > edge * edge;
        }
        catch { return false; }   // not rendered / bad transform → don't dismiss
    }

    /// <summary>True when a physical-screen-pixel point lands outside the Game Grid card — dismiss on an
    /// outside click, mirroring the wheel.</summary>
    public bool IsPointOutsideGamesCard(int screenPxX, int screenPxY) =>
        Games.IsPointOutsideCard(new Point(screenPxX, screenPxY));
}
