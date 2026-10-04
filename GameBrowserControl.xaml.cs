using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ControllerWheel;

public partial class GameBrowserControl : UserControl
{
    // Always 3 tile columns. Stays a function because tile sizing (TileSizeConverter) and d-pad
    // navigation (Move) both read it and must always agree.
    internal static int GridColumns(double listWidth) => 3;

    // The d-pad only navigates the game grid; the launcher strip is a filter row cycled by L1/R1.
    private IReadOnlyList<InstalledGame> _allGames = [];
    private string? _filterKey;                        // active launcher filter, null = All

    // Cover-cycle dot indicator: only the selected tile cycles at a time, so ONE timer serves them all —
    // full opacity for DotHoldMs after the last Select press (each press resets the hold), then a fade.
    private readonly DispatcherTimer _dotFadeTimer;
    private GameTileVM? _dotVm;
    private long _dotFadeStart;
    private const double DotHoldMs = 1500;   // stay fully visible this long after the last press
    private const double DotFadeMs = 250;    // then fade out quickly

    /// <summary>Raised when a game is chosen (controller select or mouse double-click).</summary>
    public event Action<InstalledGame>? GameChosen;

    /// <summary>Raised when an "Open …" tile is chosen. Arg = LauncherCatalog key.</summary>
    public event Action<string>? LauncherChosen;

    public GameBrowserControl()
    {
        InitializeComponent();

        BuildLauncherStrip();
        Launchers.SelectionChanged += Launchers_SelectionChanged;   // highlight = filter
        List.MouseDoubleClick += (_, _) => ActivateGrid();
        List.SelectionChanged += (_, _) => SchedulePrefetch();      // warm Select/Start alternates for the focused game
        List.SelectionChanged += (_, _) => UpdateFavoriteCaption(); // △ tip flips to "Unfavorite" on a pinned game
        // Reactor's parallax follows the selection. MUST stay at Loaded priority: the container has to
        // exist and ScrollIntoView's layout pass to settle before the focused tile can be measured.
        List.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            UpdateBoardParallax();
            RetargetSelectionSpark();
        });
        // A closed grid must not keep animating: the star field, board shears and selection spark all
        // pause with the control.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _sparkleSb?.Begin(this, true);
                UpdateBoardParallax(animate: false);
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => RetargetSelectionSpark());
            }
            else
            {
                _sparkleSb?.Stop(this);
                StopSelectionSpark();
            }
        };

        _dotFadeTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _dotFadeTimer.Tick += DotFadeTick;

        _prefetchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _prefetchTimer.Tick += (_, _) =>
        {
            _prefetchTimer.Stop();
            if (List.SelectedItem is GameTileVM { IsLauncher: false, Game: not null } vm) _ = PrefetchAsync(vm);
        };
    }

    // ── Alternate-art prefetch: rest on a tile ~350ms and its Select/Start candidates warm in the background.
    //    Debounced by selection so scrolling fetches nothing; failures are silent (the cycle press
    //    falls back to its own fetch).
    private readonly DispatcherTimer _prefetchTimer;

    private void SchedulePrefetch()
    {
        _prefetchTimer.Stop();
        if (List.SelectedItem is GameTileVM { IsLauncher: false, Game: not null }) _prefetchTimer.Start();
    }

    private async Task PrefetchAsync(GameTileVM vm)
    {
        try
        {
            var game = vm.Game!;
            vm.DefaultCover ??= await GameArt.GetAutoCoverPathAsync(game) ?? "";
            vm.CoverUrls    ??= await GameArt.GetCoverCandidateUrlsAsync(game);
            vm.LogoUrls     ??= await GameArt.GetLogoCandidateUrlsAsync(game);
            // Image downloads only while the tile is still focused and no cycle is mid-fetch on the same
            // cache — a fast scroller must not queue a download per game passed over.
            if (!ReferenceEquals(List.SelectedItem, vm) || _cycleBusy) return;
            if (vm.CoverUrls.Count > 0) await GameArt.DownloadCandidateAsync(game, vm.CoverUrls[0]);
            if (!ReferenceEquals(List.SelectedItem, vm) || _cycleBusy) return;
            if (vm.LogoUrls.Count > 0) await GameArt.DownloadLogoCandidateAsync(game, vm.LogoUrls[0], 0);
        }
        catch (Exception ex) { Trace.WriteLine($"[Grid] prefetch failed: {ex.Message}"); }
    }

    private void DotFadeTick(object? sender, EventArgs e)
    {
        if (_dotVm is null) { _dotFadeTimer.Stop(); return; }
        double elapsed = Environment.TickCount64 - _dotFadeStart;
        if (elapsed < DotHoldMs) { _dotVm.DotsOpacity = 1.0; return; }   // hold full — no fade yet
        double f = (elapsed - DotHoldMs) / DotFadeMs;
        if (f >= 1.0) { _dotVm.DotsOpacity = 0; _dotVm = null; _dotFadeTimer.Stop(); return; }
        _dotVm.DotsOpacity = 1.0 - f;
    }

    /// <summary>Show the cycle dots on a tile at full opacity and (re)start their fade-out.
    /// <paramref name="top"/> = the LOGO (Start) row along the tile's top edge; false = the cover (Select) row
    /// along the bottom.</summary>
    private void ShowCycleDots(GameTileVM vm, int total, int current, bool top = false)
    {
        if (_dotVm is not null && !ReferenceEquals(_dotVm, vm)) _dotVm.DotsOpacity = 0;   // clear a prior tile's row
        vm.DotsAtTop = top;
        vm.UpdateCycleDots(total, current);
        vm.DotsOpacity = 1.0;
        _dotVm = vm;
        _dotFadeStart = Environment.TickCount64;
        if (!_dotFadeTimer.IsEnabled) _dotFadeTimer.Start();
    }

    /// <summary>Pick mode (choosing a game for the in-wheel editor) makes ✕ read "Add to Wheel"; normal
    /// browse mode makes ✕ read "Select" (launch). Don't reintroduce a wheel-invoke "add from the
    /// grid" gesture.</summary>
    public void SetPickMode(bool pick)
    {
        _pickMode = pick;   // also gates storefront hold-to-hide (see SetHideHeld)
        SelectCaption.Text = Loc.T(pick ? UiText.Grid.AddToWheel : UiText.Grid.Select);
    }

    private System.Windows.Threading.DispatcherTimer? _tipTimer;

    /// <summary>Show the one-time cover-art tip (first Game Grid open). Auto-hides after ~12 s; also hidden
    /// by <see cref="DismissTip"/> on the first navigation. The host gates it to once (GameGridTipSeen).</summary>
    public void ShowTip()
    {
        TipBanner.Visibility = Visibility.Visible;
        _tipTimer?.Stop();
        _tipTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _tipTimer.Tick += (_, _) => DismissTip();
        _tipTimer.Start();
    }

    public void DismissTip()
    {
        _tipTimer?.Stop();
        _tipTimer = null;
        TipBanner.Visibility = Visibility.Collapsed;
    }

    // Flat-colour cover options, cycled after any SteamGridDB art, so every game can always cycle.
    // Don't add a grey — it reads as a loading placeholder.
    private static readonly string[] FlatCovers =
        { "#15243F", "#15331E", "#5E330F", "#000000", "#FFFFFF" };   // blue, green, orange, black, white

    /// <summary>Select in the grid: step the selected game's COVER and persist it — [default] → [alt1] →
    /// … → [flat colours] → wrap. Covers only: the logo overlay is an independent axis cycled with Start
    /// (<see cref="CycleLogo"/>) whose state carries unchanged across cover changes.</summary>
    private bool _cycleBusy;   // a Select/Start press during an in-flight fetch is ignored (spinner already shows it landed)

    public async void CycleCover()
    {
        if (_cycleBusy) return;
        if (List.SelectedItem is not GameTileVM { IsLauncher: false, Game: { } game } vm) return;

        // Spinner only if the work actually stalls: a cache hit finishes before the timer ticks (no
        // flash), a network round-trip lights it so the Select press doesn't look dropped.
        var spinner = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        spinner.Tick += (_, _) => { spinner.Stop(); vm.IsLoading = true; };
        spinner.Start();
        _cycleBusy = true;
        try
        {
            await CycleCoverCore(game, vm).ConfigureAwait(true);
        }
        // async void: an escaping exception would hit the dispatcher and take the process down — contain it.
        catch (Exception ex) { Trace.WriteLine($"[Grid] cover cycle failed: {ex.Message}"); }
        finally { _cycleBusy = false; spinner.Stop(); vm.IsLoading = false; }
    }

    private async System.Threading.Tasks.Task CycleCoverCore(InstalledGame game, GameTileVM vm)
    {
        vm.DefaultCover ??= await GameArt.GetAutoCoverPathAsync(game) ?? "";   // "" = no source had art
        vm.CoverUrls    ??= await GameArt.GetCoverCandidateUrlsAsync(game);    // SGDB art, default excluded

        int defStops = vm.DefaultCover!.Length > 0 ? 1 : 0;
        int artCount = vm.CoverUrls!.Count;
        int total    = defStops + artCount + FlatCovers.Length;
        // Resume from the SAVED cover the first time this tile is cycled, or the first Select press walks to
        // stop 1 regardless of what's on screen. Match by cache path (URL-derived, so it survives a
        // re-ranked candidate list); -1 = not a candidate → stay at the default stop.
        if (!vm.CoverIndexResumed)
        {
            vm.CoverIndexResumed = true;
            int at = GameArt.CandidateIndexOf(game, vm.CoverUrls);
            if (at >= 0) vm.CoverIndex = defStops + at;
            else if (GameArt.SavedCoverPath(game) is { Length: > 0 } savedFlat)
            {
                int f = Array.FindIndex(FlatCovers, hex => string.Equals(
                    GameArt.FlatColorPath(hex), savedFlat, StringComparison.OrdinalIgnoreCase));
                if (f >= 0) vm.CoverIndex = defStops + artCount + f;
            }
        }
        // Advance into a LOCAL first: CoverIndex commits only once the stop applies, so a failed download
        // leaves the position where it was instead of silently skipping stops.
        int i = (vm.CoverIndex + 1) % total;

        string? path;
        bool flat = false;
        if (defStops == 1 && i == 0)                          // back to the auto-resolved default
        {
            path = vm.DefaultCover;
            GameMetadata.ClearCoverPath(game);
        }
        else
        {
            int j = i - defStops;                             // 0-based into [art candidates, then flats]
            flat = j >= artCount;
            path = !flat
                ? await GameArt.DownloadCandidateAsync(game, vm.CoverUrls[j])
                : GameArt.FlatColorPath(FlatCovers[j - artCount]);
            if (path is null) return;                         // fetch failed → don't commit the index
            GameMetadata.SetCoverPath(game, path);
        }

        vm.CoverIndex = i;
        // Flat-colour stops always show the logo/title (a bare colour tile reads as broken); the saved
        // Start logo-off choice is overridden only while the cycle sits on a flat.
        vm.ShowOverlay = flat || !GameMetadata.LogoHiddenFor(game);
        if (path is not null && GameArt.LoadFromFile(path) is { } img) vm.Art = img;

        ShowCycleDots(vm, total, i);   // position indicator along the cover's bottom edge
    }

    /// <summary>Start in the grid: cycle the selected game's LOGO overlay and persist it —
    /// [default logo] → [SGDB alt 1] → … → [SGDB alt N] → [OFF (raw cover)] → wrap. Games with no
    /// alternates (or no SGDB key) get a simple on/off toggle. Independent of the Select cover cycle.</summary>
    public async void CycleLogo()
    {
        if (_cycleBusy) return;
        if (List.SelectedItem is not GameTileVM { IsLauncher: false, Game: { } game } vm) return;

        var spinner = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        spinner.Tick += (_, _) => { spinner.Stop(); vm.IsLoading = true; };
        spinner.Start();
        _cycleBusy = true;
        try
        {
            await CycleLogoCore(game, vm).ConfigureAwait(true);
        }
        catch (Exception ex) { Trace.WriteLine($"[Grid] logo cycle failed: {ex.Message}"); }
        finally { _cycleBusy = false; spinner.Stop(); vm.IsLoading = false; }
    }

    private async System.Threading.Tasks.Task CycleLogoCore(InstalledGame game, GameTileVM vm)
    {
        vm.LogoUrls ??= await GameArt.GetLogoCandidateUrlsAsync(game);

        // Stops: 0..N-1 = logo shown (0 = default, i = SGDB alternate i), N = overlay OFF.
        int shownStops = 1 + vm.LogoUrls!.Count;
        int total      = shownStops + 1;
        // Resume from the saved logo's FINGERPRINT the first time this tile is cycled — its position in a
        // re-ranked list is not where it was when the pick was made (mirrors the cover cycle's resume).
        if (!vm.LogoIndexResumed)
        {
            vm.LogoIndexResumed = true;
            int at = GameArt.LogoCandidateIndexOf(game, vm.LogoUrls);
            if (at >= 0) vm.LogoIndex = at + 1;
        }
        int current    = vm.ShowOverlay ? Math.Min(vm.LogoIndex, shownStops - 1) : shownStops;
        int i          = (current + 1) % total;

        if (i == shownStops)                                   // overlay OFF — raw cover
        {
            vm.ShowOverlay = false;
            GameMetadata.SetLogoState(game, hideLogo: true, logoIndex: vm.LogoIndex);
        }
        else
        {
            string? path = i == 0
                ? await GameArt.GetLogoPathAsync(game)
                : await GameArt.DownloadLogoCandidateAsync(game, vm.LogoUrls[i - 1], i - 1);
            if (i != 0 && path is null) return;                // fetch failed → don't commit the stop
            vm.LogoIndex   = i;
            vm.ShowOverlay = true;                             // (path may be null at stop 0 → the tile
            vm.Logo        = path is null ? null : GameArt.LoadFromFile(path);   // shows its title instead)
            GameMetadata.SetLogoState(game, hideLogo: false, logoIndex: i,
                                 logoUrl: i == 0 ? null : vm.LogoUrls[i - 1]);
        }

        ShowCycleDots(vm, total, i == shownStops ? total - 1 : i, top: true);
    }

    /// <summary>Storefronts the user opted out of (SystemConfig.DisabledStorefronts) — set by the host
    /// before <see cref="Load"/>, so the launcher strip doesn't offer chips for disabled stores.</summary>
    public IReadOnlyCollection<string> DisabledStorefronts { get; set; } = Array.Empty<string>();

    /// <summary>(Re)build the launcher chips — on each Load, so Defaults-tab overrides (colour / glyph)
    /// show without an app restart. Playnite is left out (aggregator — nothing to filter to). A storefront
    /// shows when it has installed games OR its launcher is installed, unless the user disabled it.</summary>
    private void BuildLauncherStrip()
    {
        var present = _allGames.Select(g => g.Storefront).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> installed;
        try { installed = GameLibrary.InstalledLaunchers(); } catch { installed = new(); }
        var chips = new List<LauncherTileVM> { LauncherTileVM.AllChip() };
        chips.AddRange(LauncherCatalog.All
            .Where(l => l.Key != "playnite" && l.Storefront is { } s
                        && (present.Contains(s) || installed.Contains(s))
                        && !DisabledStorefronts.Contains(s, StringComparer.OrdinalIgnoreCase))
            .Select(l => new LauncherTileVM(l)));
        Launchers.ItemsSource = chips;
    }

    /// <summary>The focused game (controller), or null when on a launcher / "Open …" tile — so
    /// assign-to-wheel only ever targets a real game.</summary>
    public InstalledGame? SelectedGame =>
        List.SelectedItem is GameTileVM { IsLauncher: false } t ? t.Game : null;

    public void SetCardSize(double width, double height)
    {
        Card.Width  = width;
        Card.Height = height;
        RebuildMaterialLayers();   // the sparkle field / board planes are sized to the card
    }

    /// <summary>True when a physical-screen-pixel point lands OUTSIDE the grid card — used to dismiss the
    /// grid on an outside click.</summary>
    public bool IsPointOutsideCard(Point screenPx)
    {
        try
        {
            var p = Card.PointFromScreen(screenPx);
            return p.X < 0 || p.Y < 0 || p.X > Card.ActualWidth || p.Y > Card.ActualHeight;
        }
        catch { return false; }
    }

    // ── Panel material (the classic wheel materials, applied to the Game Grid card) ────────────────
    private static Brush Frozen(Brush b) { b.Freeze(); return b; }

    private string _gridMaterial = "obsidian";

    private static readonly Brush FlatLightCard = Frozen(new SolidColorBrush(Color.FromArgb(242, 242, 243, 245)));
    private static readonly Brush FlatDarkCard  = Frozen(new SolidColorBrush(Color.FromArgb(244,  18,  19,  25)));

    // A bowed "glass-button" horizon like the wheel slices: radial brush centred on the TOP edge.
    private static Brush HorizonGloss(double centerX, params (double o, Color c)[] stops)
    {
        var g = new RadialGradientBrush
        {
            MappingMode    = BrushMappingMode.RelativeToBoundingBox,
            Center         = new Point(centerX, 0.0),
            GradientOrigin = new Point(centerX, 0.0),
            RadiusX        = 2.4,   // wide → a gentle bow across the window
            RadiusY        = 1.0,   // ramp spans top (0) → bottom (1); horizon stop sits near the top
        };
        foreach (var (o, c) in stops) g.GradientStops.Add(new GradientStop(c, o));
        g.Freeze();
        return g;
    }
    private static readonly Brush GlossLightCard = HorizonGloss(0.5,
        (0.00, Color.FromArgb(250, 255, 255, 255)),   // bright sheen at the top
        (0.11, Color.FromArgb(248, 247, 250, 252)),
        (0.15, Color.FromArgb(244, 232, 238, 243)),   // just above the horizon
        (0.16, Color.FromArgb(246, 198, 208, 219)),   // gentle horizon step
        (0.60, Color.FromArgb(244, 208, 217, 226)),   // body
        (1.00, Color.FromArgb(244, 184, 196, 208)));  // faint bottom bounce
    // Sheen centred at x=0.357 to match the Gloss Dark WHEEL's 16°-left light, and colours MUST come from
    // the shared GlossDarkPalette or card and wheel stop reading as one material.
    private static readonly Brush GlossDarkCard = HorizonGloss(0.357,
        (0.00, GlossDarkPalette.A(GlossDarkPalette.Sheen,        250)),   // dull cool sheen near the top
        (0.11, GlossDarkPalette.A(GlossDarkPalette.UpperMid,     250)),
        (0.15, GlossDarkPalette.A(GlossDarkPalette.AboveHorizon, 248)),   // just above the horizon
        (0.16, GlossDarkPalette.A(GlossDarkPalette.DarkCut,      252)),   // hard horizon cut → the darkest
        (0.60, GlossDarkPalette.A(GlossDarkPalette.Body,         252)),   // body (muted dark teal)
        (1.00, GlossDarkPalette.A(GlossDarkPalette.Bounce,       246)));  // faint bottom bounce

    private static readonly Color LightInk = Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF);   // text on dark materials
    private static readonly Color DarkInk  = Color.FromArgb(0xDE, 0x1A, 0x1A, 0x22);   // text on light materials

    // Gloss Dark's "liquid glass" rim + outer glow.
    private static readonly Brush DefaultRim = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush GlassRim = MakeGlassRim();
    private static Brush MakeGlassRim()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0xE6, 0x9A, 0xE6, 0xF7), 0.0));  // bright cyan top edge
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x99, 0x46, 0xA8, 0xC6), 0.5));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 0x22, 0x6E, 0x88), 1.0));  // dim cyan bottom
        g.Freeze();
        return g;
    }
    private static readonly System.Windows.Media.Effects.DropShadowEffect GlassGlow = MakeGlassGlow();
    private static System.Windows.Media.Effects.DropShadowEffect MakeGlassGlow()
    {
        var e = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = GlossDarkPalette.Glow,   // muted teal halo (shared with the wheel)
            BlurRadius = 26, ShadowDepth = 0, Opacity = 0.6,
        };
        e.Freeze();
        return e;
    }

    // Gloss Dark's "lit glass lip", nested just inside the rim; cleared for every other material.
    private static readonly Brush ClearBrush = Frozen(new SolidColorBrush(Colors.Transparent));
    private static readonly System.Windows.Media.Effects.BlurEffect InnerEdgeBlur = MakeInnerEdgeBlur();
    private static System.Windows.Media.Effects.BlurEffect MakeInnerEdgeBlur()
    {
        var e = new System.Windows.Media.Effects.BlurEffect
        { Radius = 3, KernelType = System.Windows.Media.Effects.KernelType.Gaussian };
        e.Freeze();
        return e;
    }
    private static readonly Brush InnerEdgeBrush = MakeInnerEdge();
    private static Brush MakeInnerEdge()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(GlossDarkPalette.Edge, 0xDD), 0.0));  // bright cool-white top lip
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, 0x7F, 0xB8, 0xCE), 0.5));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x33, 0x4A, 0x86, 0x9C), 1.0));  // faint toward the bottom
        g.Freeze();
        return g;
    }

    // ── Premium material chrome (the premium wheels re-skin the grid card too) ─────────────────────

    // Kawaii card: the hub's cloud milk as a vertical wash, so the card doesn't read as one flat pink sheet.
    private static readonly Brush KawaiiCard = MakeKawaiiCard();
    private static Brush MakeKawaiiCard()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        g.GradientStops.Add(new GradientStop(Color.FromArgb(248, 253, 246, 250), 0.00));  // hub's milky cloud
        g.GradientStops.Add(new GradientStop(Color.FromArgb(248, 250, 233, 242), 0.55));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(248, 246, 221, 236), 1.00));  // pastel pink base
        g.Freeze();
        return g;
    }
    // Kawaii selection ring: the wheel's pastel-rainbow armed rim (same 4 stops as KawaiiRimPen).
    private static readonly Brush KawaiiSelect = MakeKawaiiSelect();
    private static Brush MakeKawaiiSelect()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        g.GradientStops.Add(new GradientStop(Color.FromRgb(255, 158, 201), 0.00));  // pink
        g.GradientStops.Add(new GradientStop(Color.FromRgb(255, 224, 122), 0.34));  // butter
        g.GradientStops.Add(new GradientStop(Color.FromRgb(143, 232, 208), 0.66));  // mint
        g.GradientStops.Add(new GradientStop(Color.FromRgb(185, 165, 245), 1.00));  // lilac
        g.Freeze();
        return g;
    }
    private static readonly Brush KawaiiRim     = Frozen(new SolidColorBrush(Color.FromArgb(255, 232, 213, 238)));   // hub's soft orchid
    private static readonly Color KawaiiGridInk = Color.FromArgb(0xDE, 63, 43, 79);                                  // wheel's deep plum
    // Divider strokes are 5px solid, so they carry near-full alpha and follow one rule: dark ink on light
    // materials, light ink on dark. Kawaii's is the label shadow's orchid.
    private static readonly Brush KawaiiDivider = Frozen(new SolidColorBrush(Color.FromArgb(0xCC, 0x9B, 0x6F, 0xC7)));
    private static readonly Brush KawaiiRule    = Frozen(new SolidColorBrush(Color.FromArgb(0x55, 0x9B, 0x6F, 0xC7)));

    // Terra card: cream cut-paper plate under the wheel's tiled dirt ground — keep tile size and opacity
    // matched to the wheel backdrop or the two surfaces stop reading as one. No rim; a hard offset shadow.
    private static readonly Brush TerraCard = Frozen(new SolidColorBrush(Color.FromArgb(248, 242, 237, 227)));
    private static readonly System.Windows.Media.Effects.DropShadowEffect TerraCutShadow = MakeTerraCutShadow();
    private static System.Windows.Media.Effects.DropShadowEffect MakeTerraCutShadow()
    {
        var e = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(110, 56, 30),   // dark burnt umber (TerraIconEdgeBrush's colour)
            BlurRadius = 0, ShadowDepth = 9, Direction = 315, Opacity = 0.45,   // hard cutout, no feather
        };
        e.Freeze();
        return e;
    }
    private static readonly Brush TerraDivider = Frozen(new SolidColorBrush(Color.FromArgb(0xCC, 0xC9, 0x6A, 0x3B)));  // terracotta (dark on cream)
    private static readonly Brush TerraRule    = Frozen(new SolidColorBrush(Color.FromArgb(0x44, 0x6E, 0x38, 0x1E)));

    // Salvage card: the charcoal plate under the FULL surface photo, scaled to fill. The grid runs darker
    // than the wheel via two GRID-ONLY levers, both here — a half-luminance plate and SalvageLayerOpacity.
    // The rust sheet is RadialMenuControl.SalvageSheetTexture(), shared with the wheel: never dim the grid
    // by editing that brush.
    private static readonly Brush SalvageCardFill = Frozen(new SolidColorBrush(Color.FromArgb(250, 12, 12, 15)));   // SalvageFill's charcoal, darkened for the card
    private const double SalvageLayerOpacity = 0.62;   // dims the shared rust sheet on the GRID only
    private static readonly Brush SalvageEdge     = Frozen(new SolidColorBrush(Color.FromArgb(200, 12, 12, 14)));   // plate-lip ink
    private static readonly Brush SalvageDivider  = Frozen(new SolidColorBrush(Color.FromArgb(0xB3, 0xD9, 0xA0, 0x66)));  // rusty seam (light on charcoal)
    // The armed crescent's halo at card scale. Sits BEHIND the tile so it spills around the plate, not
    // over the art.
    private static readonly System.Windows.Media.Effects.DropShadowEffect SalvageSelectGlow = MakeSalvageGlow();
    private static System.Windows.Media.Effects.DropShadowEffect MakeSalvageGlow()
    {
        var e = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(0xFF, 0xD2, 0x4A),   // the amber selection ring's own colour
            BlurRadius = 38, ShadowDepth = 0, Opacity = 0.75,
        };
        e.Freeze();
        return e;
    }

    // Reactor card: near-black plate plus the wheel's THREE parallax circuit planes behind the tiles,
    // built in card space at the wheel's own pen weights (see BuildReactorBoardsAsync).
    private static readonly Brush ReactorCard      = Frozen(new SolidColorBrush(Color.FromArgb(250, 14, 14, 18)));
    private static readonly Brush ReactorRim       = Frozen(new SolidColorBrush(Color.FromArgb(235, 240, 240, 240)));
    private static readonly Brush ReactorHairline  = Frozen(new SolidColorBrush(Color.FromArgb(158, 244, 246, 250)));   // ReactorTileHairline's colour
    // Reactor's selection ring is the STANDING STROKE the spark laps, so keep it a dim unpowered-trace
    // teal — brighten it toward the spark cyan and the spark has nothing to pop against.
    private static readonly Brush ReactorSelect    = Frozen(new SolidColorBrush(Color.FromArgb(255, 0x1D, 0x46, 0x52)));
    private static readonly Brush ReactorDivider   = Frozen(new SolidColorBrush(Color.FromArgb(0x99, 0xF4, 0xF6, 0xFA)));   // silkscreen pale (light on near-black)

    // Classic defaults for the accent resources (the XAML fallbacks, re-applied on material change).
    private static readonly Brush DefaultSelect  = Frozen(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xD2, 0x4A)));
    private static readonly Brush DefaultDivider = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));   // light ink for the dark classics
    private static readonly Brush LightMatDivider = Frozen(new SolidColorBrush(Color.FromArgb(0x59, 0x1A, 0x1A, 0x22)));  // dark ink for the light classics
    private static readonly Brush DefaultRule    = Frozen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));

    private static readonly Thickness SelectRingRest   = new(4);
    private static readonly Thickness SelectRingKawaii = new(8);
    private const double TerraSelectLiftPx = 8.0;                   // the wheel's card lift, card-scaled
    // The card thickness must be outlined at the SAME weight as the selection ring above it — a lighter
    // edge under the ring reads as a thinner, unrelated line.
    private static readonly Thickness TerraCardEdgeThickness = SelectRingRest;
    private static readonly Thickness NoCardEdge = new(0);
    // Kawaii's black liner: 2px butted just inside the rainbow ring, so it starts where the ring ends.
    private static readonly Brush KawaiiInnerStroke = Frozen(new SolidColorBrush(Colors.Black));
    private static readonly Thickness KawaiiInnerStrokeWidth = new(2);
    private const double KawaiiLinerRadiusBoost = 2.0;   // both liners round a touch more than concentric

    /// <summary>Apply a wheel material to the grid card + flip text/chip ink for legibility. The premium
    /// materials (Kawaii/Terra/Salvage/Reactor) carry their own card treatments built from the wheel's
    /// own brushes.</summary>
    public void SetMaterial(string? material)
    {
        // Same alias resolution the wheel uses (frost-*/sparkle → kawaii, stencil → salvage, paper → terra).
        _gridMaterial = RadialMenuControl.NormalizeMaterial(material);
        ApplyAppearance();
    }

    // ── Narration ────────────────────────────────────────────────────────────────
    // The grid speaks through App's Announcer; a static hook keeps OverlayWindow out of the plumbing
    // (same single-instance pattern as MotionPolicy). Null (or narration off) = every Say is a no-op.
    public static Action<string, AnnouncementKind>? AnnounceHook;
    private static void Say(string text, AnnouncementKind kind) => AnnounceHook?.Invoke(text, kind);

    /// <summary>Narrate the focused tile: title, position among selectable tiles (the band divider is
    /// skipped in both count and index), then storefront-card vs favorite state. Selection-kind, so
    /// d-pad repeats coalesce like stick sweeps.</summary>
    private void AnnounceFocused()
    {
        if (AnnounceHook is null) return;
        if (List.SelectedItem is not GameTileVM vm || vm.IsDivider) return;
        int pos = 0, total = 0;
        foreach (var item in List.Items)
        {
            if (item is not GameTileVM t || t.IsDivider) continue;
            total++;
            if (ReferenceEquals(t, vm)) pos = total;
        }
        string extra = vm.IsLauncher ? Loc.T(UiText.Grid.StorefrontSuffix)
                     : vm.FavoriteTile ? Loc.T(UiText.Grid.FavoriteSuffix)
                     : "";
        Say(Loc.F(UiText.Narration.SliceOf, vm.Title, pos, total) + extra, AnnouncementKind.Selection);
    }

    private bool _reduceMotion;

    /// <summary>Reduce Motion — the grid's slice of the product-wide policy (MotionPolicy). Suppresses the
    /// Kawaii sparkle field, the Reactor selection sparks, the Reactor board parallax (pinned centred) and
    /// the focus-shear travel. Set by App (OverlayWindow.SetGamesReduceMotion) with MotionPolicy's
    /// EFFECTIVE value; a live flip while the grid is open must stop or restore the running loops
    /// immediately rather than waiting for a reopen.</summary>
    public bool ReduceMotion
    {
        get => _reduceMotion;
        set
        {
            if (_reduceMotion == value) return;
            _reduceMotion = value;
            // A tile mid-fetch keeps its forever-spin storyboard otherwise — re-evaluate every VM's Spin
            // gate so the DataTrigger's exit action stops it now.
            if (List.ItemsSource is System.Collections.IEnumerable items)
                foreach (var it in items)
                    (it as GameTileVM)?.RefreshSpin();
            if (value)
            {
                ClearKawaiiSparkles();                        // stop the forever storyboard, not just hide it
                StopSelectionSpark();                         // stop the 33 ms comet timer
                UpdateBoardParallax(animate: false);          // re-seat the reactor boards centred right now
            }
            else
            {
                // Coming back off: restart whatever the current material owns. The reactor board bitmaps
                // are untouched — only the spark/parallax loops resume.
                double w = Card.Width, h = Card.Height;
                if (_gridMaterial == "kawaii" && w > 40 && h > 40 && !double.IsNaN(w) && !double.IsNaN(h))
                    BuildKawaiiSparkles(w, h);
                if (_gridMaterial == "reactor")
                {
                    RetargetSelectionSpark();
                    UpdateBoardParallax(animate: false);
                }
            }
        }
    }

    /// <summary>Set card background + ink + corners + rim/effect + texture layer + accent resources for
    /// the current material, then rebuild whatever animated layer that material owns. Every value is set
    /// on EVERY branch — omit one and the previous material bleeds through.</summary>
    private void ApplyAppearance()
    {
        var (brush, dark) = _gridMaterial switch
        {
            "flat-light"  => (FlatLightCard,  false),
            "pearl" => (GlossLightCard, false),
            "flat-dark"   => (FlatDarkCard,   true),
            "kawaii"      => (KawaiiCard,     false),
            "mesa"       => (TerraCard,      false),
            "salvage"     => (SalvageCardFill, true),
            "reactor"     => (ReactorCard,    true),
            // Drop-in custom themes: the grid keeps its flat card treatment and just follows the
            // theme's dark flag — without this arm a custom token would render the grid as Obsidian
            // even for a light theme (the wheel's own fallback is the FLAT pair, asymmetrically).
            _ when Materials.IsCustom(_gridMaterial) =>
                (Materials.IsDark(_gridMaterial) ? FlatDarkCard : FlatLightCard,
                 Materials.IsDark(_gridMaterial)),
            _             => (GlossDarkCard,  true),
        };
        Card.Background   = brush;
        Card.CornerRadius = new CornerRadius(14);

        // Ink: kawaii writes in the wheel's deep plum rather than the generic dark ink.
        var inkColor = _gridMaterial == "kawaii" ? KawaiiGridInk : dark ? LightInk : DarkInk;
        Resources["GridInk"] = new SolidColorBrush(inkColor);

        var (rim, rimW, fx, inner, layer, select, divider, rule) = _gridMaterial switch
        {
            "obsidian" => (ClearBrush, 0.0, (System.Windows.Media.Effects.Effect?)GlassGlow, InnerEdgeBrush,
                             (Brush?)null, DefaultSelect, DefaultDivider, DefaultRule),
            "kawaii"     => (KawaiiRim, 2.0, null, ClearBrush,
                             null, KawaiiSelect, KawaiiDivider, KawaiiRule),
            // The ground tile carries TerraGroundOpacity itself — the layer stays at 1.0 or the two compound.
            "mesa"      => (ClearBrush, 0.0, TerraCutShadow, ClearBrush,
                             (Brush?)RadialMenuControl.TerraGroundTileTexture(),
                             RadialMenuControl.TerraIconEdgeBrush, TerraDivider, TerraRule),
            // The sheet brush carries SalvageRustOpacity itself — the layer stays at 1.0 or the two compound.
            "salvage"    => (SalvageEdge, 1.5, null, ClearBrush,
                             (Brush?)RadialMenuControl.SalvageSheetTexture(),
                             DefaultSelect, SalvageDivider, DefaultRule),
            // Reactor's texture is the animated BoardLayer, not a brush — nothing goes on MaterialLayer.
            "reactor"    => (ReactorRim, 1.5, null, ReactorHairline,
                             null, ReactorSelect, ReactorDivider, DefaultRule),
            _            => (DefaultRim, 1.0, null, ClearBrush,
                             null, DefaultSelect, dark ? DefaultDivider : LightMatDivider, DefaultRule),
        };
        Card.BorderThickness      = new Thickness(rimW);
        Card.BorderBrush          = rim;
        Card.Effect               = fx;
        InnerEdge.BorderBrush     = inner;
        // The lit lip's feathering blur only exists for obsidian's glass edge — an Effect on the
        // full-card border forces an intermediate surface every frame even when the brush is transparent,
        // so drop it entirely on the other seven materials. (The XAML declares no Effect; this owns it.)
        InnerEdge.Effect          = ReferenceEquals(inner, InnerEdgeBrush) ? InnerEdgeBlur : null;
        MaterialLayer.Background  = layer ?? ClearBrush;
        // Salvage alone dims its texture layer; every other material paints its brush at full strength.
        MaterialLayer.Opacity     = _gridMaterial == "salvage" ? SalvageLayerOpacity : 1.0;
        Resources["GridSelect"]   = select;
        Resources["GridDivider"]  = divider;
        Resources["GridRule"]     = rule;

        Resources["GridSelectThickness"] = _gridMaterial == "kawaii" ? SelectRingKawaii : SelectRingRest;
        // Face lifts UP (negative Y); don't switch to an edge-down lift.
        Resources["GridSelectLift"] =
            new TranslateTransform(0, _gridMaterial == "mesa" ? -TerraSelectLiftPx : 0);
        // Terra's exposed thickness takes the same ink as its selection ring, so ring + edge read as one
        // cut plate rather than three colours.
        Resources["GridCardEdgeFill"] =
            _gridMaterial == "mesa" ? RadialMenuControl.TerraIconEdgeBrush : ClearBrush;
        Resources["GridCardEdgeStroke"] =
            _gridMaterial == "mesa" ? RadialMenuControl.TerraIconEdgeBrush : ClearBrush;
        Resources["GridCardEdgeThickness"] =
            _gridMaterial == "mesa" ? TerraCardEdgeThickness : NoCardEdge;
        Resources["GridSelectGlow"] =
            _gridMaterial == "salvage" ? SalvageSelectGlow : null;

        // The liner sits flush against the ring's inner edge: margin IS the ring's thickness, corner that
        // much less than the ring's 12 — concentric with the ring's inner curve.
        bool kawaii = _gridMaterial == "kawaii";
        double ringW = kawaii ? SelectRingKawaii.Top : SelectRingRest.Top;
        double linerW = KawaiiInnerStrokeWidth.Top;
        Resources["GridInnerStroke"]          = kawaii ? KawaiiInnerStroke : ClearBrush;
        // Inset one pixel SHORT of the ring's thickness and drawn a pixel thicker, so the extra pixel hides
        // beneath the ring's inner edge (the visible band is still `linerW`).
        Resources["GridInnerStrokeThickness"] = kawaii ? new Thickness(linerW + 1) : NoCardEdge;
        Resources["GridInnerStrokeMargin"]    = new Thickness(ringW - 1);
        Resources["GridInnerStrokeRadius"]    =
            new CornerRadius(Math.Max(0, 12 - (ringW - 1)) + KawaiiLinerRadiusBoost);
        // Outer twin: outset by its own weight, radius grown to match (plus the inner liner's rounding
        // boost, keeping the curves a matched pair), and likewise ONE PIXEL THICKER — that hidden pixel
        // is what closes the corner hairline.
        double outW = KawaiiInnerStrokeWidth.Top;
        Resources["GridOuterStrokeMargin"]    = new Thickness(-outW);
        Resources["GridOuterStrokeRadius"]    = new CornerRadius(12 + outW + KawaiiLinerRadiusBoost);
        Resources["GridOuterStrokeThickness"] = kawaii ? new Thickness(outW + 1) : NoCardEdge;

        RebuildMaterialLayers();
    }

    // ── Animated material layers: Kawaii's star field + Reactor's parallax boards ───────────────────
    // Rebuilt on a material change and a card resize, and torn down when their material isn't active —
    // a closed grid must not leave animations or a board bitmap alive.

    private void RebuildMaterialLayers()
    {
        double w = Card.Width, h = Card.Height;
        bool sized = w > 40 && h > 40 && !double.IsNaN(w) && !double.IsNaN(h);

        if (_gridMaterial == "kawaii" && sized && !ReduceMotion) BuildKawaiiSparkles(w, h);
        else ClearKawaiiSparkles();

        if (_gridMaterial == "reactor" && sized) BuildReactorBoards(w, h);
        else ClearReactorBoards();

        // The spark belongs to reactor only, and needs a laid-out container to trace.
        if (_gridMaterial == "reactor") Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => RetargetSelectionSpark());
        else StopSelectionSpark();
    }

    // Kawaii ambient sparkles: the hub cloud's twinkle scattered over the card, paced far slower
    // (background, not focal) and driven by WPF animations — the grid has no per-frame render loop.
    private const int KawaiiStarCount = 12;
    private static readonly double KawaiiSparklePeriodSec = RadialMenuControl.KawaiiTwinklePeriodSec * 2.6;
    private System.Windows.Media.Animation.Storyboard? _sparkleSb;

    private void BuildKawaiiSparkles(double w, double h)
    {
        ClearKawaiiSparkles();
        var sb = new System.Windows.Media.Animation.Storyboard();
        var rng = new Random(20260731);   // fixed seed: the field is the same every open, like the wheel's boards
        var edge = RadialMenuControl.KawaiiTwinkleEdge;

        for (int k = 0; k < KawaiiStarCount; k++)
        {
            double r   = 18.0 + rng.NextDouble() * 15.0;
            double rot = rng.NextDouble() * 90.0;
            // Kept off the very edges so a star never half-clips on the card's rounded corner.
            double x = 34 + rng.NextDouble() * Math.Max(1, w - 68);
            double y = 34 + rng.NextDouble() * Math.Max(1, h - 68);

            // Sparkles SCALE in from nothing rather than fading: opacity is fixed and the whole twinkle
            // rides the scale envelope, so a star pops into being at full ink.
            var scale = new ScaleTransform(0, 0);
            var star  = new Grid
            {
                Width = r * 2, Height = r * 2, Opacity = 0.95,
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = scale,
            };
            star.Children.Add(new System.Windows.Shapes.Path
            {
                Data = RadialMenuControl.KawaiiStarGeometry(new Point(r, r), r, rot),
                Fill = RadialMenuControl.KawaiiTwinkleInk,
                Stroke = edge.Brush, StrokeThickness = edge.Thickness,
                StrokeLineJoin = PenLineJoin.Round,   // a star's points spike under a miter
            });
            star.Children.Add(new System.Windows.Shapes.Path
            {
                Data = RadialMenuControl.KawaiiStarGeometry(new Point(r, r), r * 0.45, rot),
                Fill = RadialMenuControl.KawaiiTwinkleCore,   // white-hot centre
            });
            Canvas.SetLeft(star, x - r);
            Canvas.SetTop(star, y - r);
            SparkleLayer.Children.Add(star);

            // Staggered phase so ~2-3 stars are lit at any moment.
            var begin = TimeSpan.FromSeconds(KawaiiSparklePeriodSec * k / KawaiiStarCount);
            sb.Children.Add(SparkleTrack(scale, ScaleTransform.ScaleXProperty, begin, 0.0, 1.0, 0.0));
            sb.Children.Add(SparkleTrack(scale, ScaleTransform.ScaleYProperty, begin, 0.0, 1.0, 0.0));
        }

        _sparkleSb = sb;
        if (IsVisible) sb.Begin(this, true);
    }

    /// <summary>One star's animation on one property: dark → snap to <paramref name="peak"/> at 12% →
    /// ease back by 45% → hold dark for the rest of the cycle. Repeats forever from <paramref name="begin"/>.</summary>
    private static System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames SparkleTrack(
        DependencyObject target, DependencyProperty prop, TimeSpan begin, double rest, double peak, double end)
    {
        var a = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.FromSeconds(KawaiiSparklePeriodSec)),
            BeginTime = begin,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
        };
        a.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            rest, System.Windows.Media.Animation.KeyTime.FromPercent(0.0)));
        a.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            peak, System.Windows.Media.Animation.KeyTime.FromPercent(0.12),
            new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }));
        a.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            end, System.Windows.Media.Animation.KeyTime.FromPercent(0.45),
            new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }));
        a.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            end, System.Windows.Media.Animation.KeyTime.FromPercent(1.0)));
        System.Windows.Media.Animation.Storyboard.SetTarget(a, target);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(a, new PropertyPath(prop));
        return a;
    }

    private void ClearKawaiiSparkles()
    {
        if (_sparkleSb is { } sb)
        {
            try { sb.Stop(this); } catch (Exception ex) { Trace.WriteLine($"[Grid] sparkle stop: {ex.Message}"); }
            _sparkleSb = null;
        }
        SparkleLayer.Children.Clear();
    }

    // Reactor parallax boards: the wheel's three depth planes, in CARD space. Generate over the card's own
    // rect and draw 1:1 (Stretch=None) — scaling a smaller board up makes the traces read thick and
    // sparse. The planes shear apart as the SELECTION moves, so scrolling parallaxes the circuitry.
    private static readonly (double scale, double parallax, double dim, int seed)[] ReactorPlanes =
    {
        // Deepest first (painted behind, moves least), then back, then front.
        (RadialMenuControl.CircuitFarScale,  RadialMenuControl.CircuitFarZoomAmt,  RadialMenuControl.CircuitFarDim,  20260731 + 224737),
        (RadialMenuControl.CircuitBackScale, RadialMenuControl.CircuitBackZoomAmt, RadialMenuControl.CircuitBackDim, 20260731 + 104729),
        (1.0,                                1.0,                                 1.0,                              20260731),
    };
    /// <summary>How far a plane's shear travels at a full-corner selection, as a fraction of the card's
    /// short edge — the card-space stand-in for the wheel's InnerRadius × CircuitAnchorReach.</summary>
    private const double BoardShearFraction = 0.20;
    private TranslateTransform[]? _boardShear;
    private (double w, double h) _boardKey;
    private int _boardGen;   // bumped per request; a late async build with a stale token is dropped

    private void BuildReactorBoards(double w, double h)
    {
        var key = (Math.Round(w), Math.Round(h));
        if (_boardShear is not null && _boardKey == key) return;   // same card size — keep the built planes
        ClearReactorBoards();
        _boardKey = key;
        _ = BuildReactorBoardsAsync(w, h, ++_boardGen);
    }

    /// <summary>Generate the three boards OFF the UI thread — each is a few hundred ms of rejection-sampled
    /// routing at card size, far too much for the dispatcher — then hang them on the layer. The card shows
    /// its bare reactor plate until they land.</summary>
    private async Task BuildReactorBoardsAsync(double w, double h, int gen)
    {
        try
        {
            var jobs = ReactorPlanes.Select(p => Task.Run(() =>
            {
                // A plane drawn at `scale` covers less of the card, so generate it over a correspondingly
                // LARGER rect (plus shear slack) — reusing the front plane's layout would show the same
                // circuitry three times.
                double gw = w / p.scale + 260, gh = h / p.scale + 260;
                return RadialMenuControl.BuildReactorBoardFor(new Rect(0, 0, gw, gh), p.seed);
            })).ToArray();
            var boards = await Task.WhenAll(jobs).ConfigureAwait(true);

            // A material change / resize while we were generating wins — drop this build silently.
            if (gen != _boardGen || _gridMaterial != "reactor") return;

            var shears = new TranslateTransform[boards.Length];
            for (int i = 0; i < boards.Length; i++)
            {
                var p = ReactorPlanes[i];
                shears[i] = new TranslateTransform();
                var group = new TransformGroup();
                // Depth zoom is fixed per plane; the discrete selection drives the SHEAR, which is what
                // actually reads as parallax.
                double zoom = p.scale * (1.0 + RadialMenuControl.CircuitZoomAmt * p.parallax * 0.6);
                group.Children.Add(new ScaleTransform(zoom, zoom));
                group.Children.Add(shears[i]);
                BoardLayer.Children.Add(new Image
                {
                    Source = new DrawingImage(boards[i]),
                    Stretch = Stretch.None,                   // 1:1 — the whole point of card-space generation
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = RadialMenuControl.CircuitBoardOpacity * p.dim,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = group,
                    IsHitTestVisible = false,
                });
            }
            _boardShear = shears;
            UpdateBoardParallax(animate: false);   // seat the planes on the current selection
        }
        catch (Exception ex) { Trace.WriteLine($"[Grid] reactor board build failed: {ex.Message}"); }
    }

    private void ClearReactorBoards()
    {
        _boardGen++;               // invalidates any in-flight build
        _boardShear = null;
        _boardKey = default;
        BoardLayer.Children.Clear();
    }

    /// <summary>Shear the three planes toward the focused tile, each by its own depth response (far least,
    /// front most) and opposite the selection's offset from the card centre.</summary>
    private void UpdateBoardParallax(bool animate = true)
    {
        if (_boardShear is not { } shears) return;
        double cx = BoardLayer.ActualWidth / 2.0, cy = BoardLayer.ActualHeight / 2.0;
        if (cx < 1 || cy < 1) return;

        double dx = 0, dy = 0;
        // Reduce Motion: leave dx/dy at 0 so every plane shears by 0, i.e. holds centred. Boards still draw.
        try
        {
            if (!ReduceMotion && List.SelectedIndex >= 0
                && List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) is FrameworkElement fe
                && fe.IsDescendantOf(BoardLayer.Parent as Visual ?? this))
            {
                var c = fe.TransformToAncestor(this).Transform(new Point(fe.ActualWidth / 2, fe.ActualHeight / 2));
                var o = BoardLayer.TransformToAncestor(this).Transform(new Point(cx, cy));
                dx = Math.Clamp((c.X - o.X) / cx, -1, 1);
                dy = Math.Clamp((c.Y - o.Y) / cy, -1, 1);
            }
        }
        // A container that isn't realized/parented yet (virtualization, first layout) leaves the planes
        // centred — never worth an exception on a selection change.
        catch (Exception ex) { Trace.WriteLine($"[Grid] parallax anchor unavailable: {ex.Message}"); }

        double reach = Math.Min(BoardLayer.ActualWidth, BoardLayer.ActualHeight) * BoardShearFraction;
        for (int i = 0; i < shears.Length; i++)
        {
            double amt = reach * ReactorPlanes[i].parallax;
            // Reduce Motion: hard-set (dx/dy are already pinned 0 above) — no eased travel to get there.
            SetShear(shears[i], TranslateTransform.XProperty, -dx * amt, animate && !ReduceMotion);
            SetShear(shears[i], TranslateTransform.YProperty, -dy * amt, animate && !ReduceMotion);
        }
    }

    // ── Reactor selection spark ────────────────────────────────────────────────────────────────────
    // Comets lapping the focused tile's outline at different speeds (see SparkSpecs).
    //
    // The tail must stay CONNECTED SEGMENTS with ROUND caps, never a row of dots: at ~10px between centres
    // on a tile perimeter, ~4px dots can never fuse and read as separate lights however dense they get.
    //
    // Driven by a 30fps DispatcherTimer over a PRE-SAMPLED path, NOT WPF path animations: a restarted
    // MatrixAnimationUsingPath parks its geometry at the path origin for its stagger delay, visible as a
    // twitch at one corner on every d-pad step. The phase is deliberately NOT reset on re-target.
    private const int    SparkTailSegs = 10;     // × SparkTailGap = the tail's length, as a fraction of the lap
    private const double SparkTailGap  = 0.010;  // arc-length each segment spans
    private const int    SparkSamples  = 240;
    private const double SparkRingInset = 2.0;   // half the 4px selection ring — the spark rides its centre
    private const double SparkRingRadius = 12.0 - SparkRingInset;   // concentric with the tile's radius 12

    /// <summary>The sparks on the route, in paint order: a matched pair of AMBER sparks HALF A LAP apart,
    /// plus a faster BLUE one that overtakes both. The ambers hold their 180° pairing only while they share
    /// a lap time AND a direction, and they run COUNTER-CLOCKWISE against the blue so head-on passes read
    /// as two currents rather than one train of lights. (lapSec, head diameter, index into the wheel's
    /// charge colours [0 = blue, 2 = amber], start phase as a fraction of the lap, direction ±1.)</summary>
    private static readonly (double lapSec, double headPx, int color, double phase0, int dir)[] SparkSpecs =
    {
        (7.0, 4.6, 2, 0.00, -1),
        (4.2, 3.8, 0, 0.25, +1),
        (7.0, 4.6, 2, 0.50, -1),
    };

    /// <summary>One spark: a white-hot head plus the connected segments trailing it, and its own phase
    /// along the shared route. Each owns its visuals, so they run at unrelated speeds off one timer.</summary>
    private sealed class SelectionSpark
    {
        public required Ellipse Head;
        public required Line[]  Tail;      // segment 0 leaves the head; each one behind it thins and fades
        public required double  LapSec;
        public required double  HeadPx;
        public required int     Dir;       // +1 clockwise along the sampled route, -1 against it
        public double Phase;
    }

    private DispatcherTimer? _sparkTimer;
    private Point[]? _sparkPath;          // pre-sampled ring outline, control space — shared by both sparks
    private SelectionSpark[]? _sparks;

    private void EnsureSparkVisuals()
    {
        if (_sparks is not null) return;
        var sparks = new SelectionSpark[SparkSpecs.Length];

        for (int s = 0; s < SparkSpecs.Length; s++)
        {
            var (lapSec, headPx, colorIndex, phase0, dir) = SparkSpecs[s];
            var tint = RadialMenuControl.ReactorSparkTailColor(colorIndex);
            double lead = headPx - 0.4;   // the tail leaves the head at just under its width

            // Tail first, so this spark's head paints over its own leading cap.
            var segs = new Line[SparkTailSegs];
            for (int i = 0; i < segs.Length; i++)
            {
                // Alpha and width both taper behind the head on a deliberately shallower ramp than the
                // wheel's — this tail rides a tile outline over cover art, not the wheel's dark board.
                double t = i / (double)(segs.Length - 1);
                var b = new SolidColorBrush(Color.FromArgb((byte)(255 - 175 * t), tint.R, tint.G, tint.B));
                b.Freeze();
                segs[i] = new Line
                {
                    Stroke = b,
                    StrokeThickness = lead - lead * 0.62 * t,
                    StrokeStartLineCap = PenLineCap.Round,   // round caps are what make the joins seamless
                    StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false, Visibility = Visibility.Hidden,
                };
                SparkLayer.Children.Add(segs[i]);
            }

            var head = new Ellipse
            {
                Width = headPx, Height = headPx, Fill = RadialMenuControl.ReactorSparkHead,
                IsHitTestVisible = false, Visibility = Visibility.Hidden,
            };
            SparkLayer.Children.Add(head);

            sparks[s] = new SelectionSpark
            {
                Head = head, Tail = segs, LapSec = lapSec, HeadPx = headPx, Dir = dir, Phase = phase0,
            };
        }
        _sparks = sparks;
    }

    /// <summary>Re-sample the spark's route onto the focused tile's outline. Clears the route (hiding the
    /// spark) whenever there's nothing valid to trace — no reactor material, no selection, or an
    /// unrealized container.</summary>
    private void RetargetSelectionSpark()
    {
        // Reduce Motion: the selection sparks stop entirely (the static selection ring carries focus).
        if (_gridMaterial != "reactor" || !IsVisible || ReduceMotion) { StopSelectionSpark(); return; }
        try
        {
            if (List.SelectedIndex < 0
                || List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) is not FrameworkElement fe
                || fe.ActualWidth < 8 || fe.ActualHeight < 8
                || List.Items[List.SelectedIndex] is GameTileVM { IsDivider: true })
            { StopSelectionSpark(); return; }

            var origin = fe.TransformToAncestor(this).Transform(new Point(0, 0));
            // The container is TALLER than the card face: TileSizeConverter.TileHeadroom reserves a strip
            // at the TOP of every item so selection chrome escaping the face isn't sheared by the scroll
            // clip. Trace the raw container box and the spark's TOP edge lands a full headroom above the
            // visible ring, so drop and shorten the box to the FACE's outline. `lift` covers a material
            // that translates the face up into that strip (Terra's GridSelectLift) — 0 for Reactor, but
            // keeping it derived stops a future lift silently re-breaking this.
            double lift = _gridMaterial == "mesa" ? TerraSelectLiftPx : 0.0;
            double faceTop = origin.Y + TileSizeConverter.TileHeadroom - lift;
            double faceH   = fe.ActualHeight - TileSizeConverter.TileHeadroom;
            var box = new Rect(origin.X + SparkRingInset, faceTop + SparkRingInset,
                               Math.Max(1, fe.ActualWidth - SparkRingInset * 2),
                               Math.Max(1, faceH - SparkRingInset * 2));

            var path = PathGeometry.CreateFromGeometry(new RectangleGeometry(box, SparkRingRadius, SparkRingRadius));
            var pts = new Point[SparkSamples];
            for (int i = 0; i < SparkSamples; i++)
            {
                path.GetPointAtFractionLength(i / (double)SparkSamples, out var p, out _);
                pts[i] = p;
            }
            _sparkPath = pts;

            EnsureSparkVisuals();
            _sparkTimer ??= MakeSparkTimer();
            if (!_sparkTimer.IsEnabled) _sparkTimer.Start();
            PlaceSpark();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Grid] selection spark retarget failed: {ex.Message}");
            StopSelectionSpark();
        }
    }

    private DispatcherTimer MakeSparkTimer()
    {
        var t = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        t.Tick += (_, _) =>
        {
            if (_sparks is { } sparks)
                // Dir flips the travel; At() normalizes any phase (negatives included), and the % keeps the
                // accumulator bounded either way.
                foreach (var s in sparks) s.Phase = (s.Phase + s.Dir * 0.033 / s.LapSec) % 1.0;
            PlaceSpark();
        };
        return t;
    }

    private void PlaceSpark()
    {
        if (_sparkPath is not { } pts || _sparks is not { } sparks) return;

        // Interpolated, not snapped to the sample grid: at 30fps over a 7 s lap a spark advances barely
        // more than one sample per frame, so stepping between samples visibly stutters.
        Point At(double f)
        {
            f = ((f % 1.0) + 1.0) % 1.0;
            double x = f * pts.Length;
            int i = (int)x, j = (i + 1) % pts.Length;
            double u = x - i;
            i %= pts.Length;
            return new Point(pts[i].X + (pts[j].X - pts[i].X) * u, pts[i].Y + (pts[j].Y - pts[i].Y) * u);
        }

        foreach (var spark in sparks)
        {
            var hp = At(spark.Phase);
            Canvas.SetLeft(spark.Head, hp.X - spark.HeadPx / 2.0);
            Canvas.SetTop(spark.Head, hp.Y - spark.HeadPx / 2.0);
            spark.Head.Visibility = Visibility.Visible;

            var from = hp;
            for (int i = 0; i < spark.Tail.Length; i++)
            {
                var to = At(spark.Phase - spark.Dir * (i + 1) * SparkTailGap);   // always BEHIND the head
                spark.Tail[i].X1 = from.X; spark.Tail[i].Y1 = from.Y;
                spark.Tail[i].X2 = to.X;   spark.Tail[i].Y2 = to.Y;
                spark.Tail[i].Visibility = Visibility.Visible;
                from = to;   // next segment starts where this one ended — no gaps in the streak
            }
        }
    }

    private void StopSelectionSpark()
    {
        _sparkTimer?.Stop();
        _sparkPath = null;
        if (_sparks is not { } sparks) return;
        foreach (var spark in sparks)
        {
            spark.Head.Visibility = Visibility.Hidden;
            foreach (var s in spark.Tail) s.Visibility = Visibility.Hidden;
        }
    }

    private static void SetShear(TranslateTransform t, DependencyProperty prop, double to, bool animate)
    {
        if (!animate)
        {
            t.BeginAnimation(prop, null);   // drop any running animation before a hard set
            t.SetValue(prop, to);
            return;
        }
        var a = new System.Windows.Media.Animation.DoubleAnimation(to, new Duration(TimeSpan.FromMilliseconds(260)))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
            FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd,
        };
        t.BeginAnimation(prop, a);
    }

    public void SetLoading()
    {
        List.ItemsSource = null;
    }

    public void Load(IReadOnlyList<InstalledGame> games)
    {
        DismissHideConfirm();   // a fresh open must never inherit a previous session's pending confirm
        _allGames = games;
        BuildLauncherStrip();                                // refresh chips for any Defaults-tab overrides
        ApplyFilter(null);                                   // all games
        if (Launchers.Items.Count > 0) Launchers.SelectedIndex = 0;   // strip shows "All" as active
    }

    // ── Filtering ───────────────────────────────────────────────────────────────

    /// <summary>Grid order: favorites pinned first, then most-recently-launched (Radiata's own launches),
    /// then name; hidden games don't appear at all. Applied inside every filter view, so a favorite leads
    /// its storefront's filtered list too.</summary>
    private static IEnumerable<InstalledGame> OrderForGrid(IEnumerable<InstalledGame> games) =>
        games.Where(g => !GameMetadata.IsHidden(g))
             .OrderByDescending(GameMetadata.IsFavorite)
             .ThenByDescending(GameMetadata.LastLaunchedTicks)
             .ThenBy(g => g.Name, StringComparer.Create(Loc.Culture, ignoreCase: true));   // accented and non-Latin titles interleave where the language expects

    private void ApplyFilter(string? launcherKey)
    {
        _filterKey = launcherKey;

        var games = new List<GameTileVM>();
        var info  = launcherKey is null ? (LauncherInfo?)null : LauncherCatalog.Find(launcherKey);
        GameTileVM? open = null;
        if (info is { } li)
        {
            if (LauncherCatalog.IsPresent(li.Key))
                open = new GameTileVM(li);       // "Open <launcher>" — only when the launcher app is present
            if (li.Storefront is { } store)
                games.AddRange(OrderForGrid(_allGames
                        .Where(g => string.Equals(g.Storefront, store, StringComparison.OrdinalIgnoreCase)))
                    .Select(g => new GameTileVM(g)));
        }
        else
        {
            games.AddRange(OrderForGrid(_allGames).Select(g => new GameTileVM(g)));
        }

        // Two bands: pinned favorites at the base tile size, everything else one column tighter.
        // OrderForGrid already leads with the favorites, so splitting keeps their order. Keyed off
        // FavoriteTile, NOT IsLauncher, so a favoritable "Open <storefront>" tile sorts itself.
        var favs = games.Where(t => t.FavoriteTile).ToList();
        var rest = games.Where(t => !t.FavoriteTile).ToList();
        if (open is { FavoriteTile: true }) favs.Add(open);
        else if (open is not null)         rest.Insert(0, open);

        // Band divider: a full-row separator between the two, only when both exist. Its full-row width is
        // also what forces the WrapPanel to break between the bands.
        var tiles = new List<GameTileVM>(favs);
        if (favs.Count > 0 && rest.Count > 0) tiles.Add(GameTileVM.MakeDivider());
        tiles.AddRange(rest);

        List.ItemsSource = tiles;

        if (tiles.Count > 0)
        {
            List.SelectedIndex = 0;
            List.ScrollIntoView(List.SelectedItem);
        }
        _ = LoadArtAsync(tiles.Where(t => !t.IsLauncher && !t.IsDivider).ToList(), ++_artGen);
    }

    /// <summary>Bumped per <c>ApplyFilter</c>; a sweep still in flight for a previous filter sees a stale
    /// token and stops, instead of fetching art (SteamGridDB included) into view-models the grid isn't
    /// showing — cycling launcher chips must not leave a sweep per chip running. Same shape as <c>_boardGen</c>.</summary>
    private int _artGen;
    private static readonly SemaphoreSlim ArtGate = new(4);   // one cap across sweeps, not four per sweep

    private void Launchers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Launchers.SelectedItem is not LauncherTileVM vm) return;
        var key = vm.IsAll ? null : vm.Key;
        if (string.Equals(key, _filterKey, StringComparison.OrdinalIgnoreCase)) return;   // no change
        ApplyFilter(key);
        // Narrate the collection change (not plain reloads — those go through ApplyFilter directly).
        if (AnnounceHook is not null)
        {
            int n = 0;
            foreach (var item in List.Items)
                if (item is GameTileVM { IsDivider: false, IsLauncher: false }) n++;
            Say(Loc.P(UiText.Grid.FilterGamesOne, UiText.Grid.FilterGamesOther, n, vm.Name), AnnouncementKind.Context);
        }
    }

    // Fetch/cache key art in the background (a few at a time) and fill each tile as it arrives.
    private async Task LoadArtAsync(List<GameTileVM> vms, int gen)
    {
        var tasks = vms.Select(async vm =>
        {
            if (vm.Game is null) return;
            await ArtGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (gen != _artGen) return;   // the filter moved on while we queued
                // Persisted Start overlay-off pick — except on a flat-colour cover, which always shows the
                // logo/title (the saved choice resumes when the cover leaves the flats).
                bool hide = GameMetadata.LogoHiddenFor(vm.Game) && !GameMetadata.CoverIsFlat(vm.Game);
                int  lidx = GameMetadata.LogoIndexFor(vm.Game);        // persisted Start alternate-logo pick
                if (hide || lidx > 0)
                    await Dispatcher.InvokeAsync(() => { vm.ShowOverlay = !hide; vm.LogoIndex = lidx; });
                var art = await GameArt.GetAsync(vm.Game).ConfigureAwait(false);
                if (gen != _artGen) return;
                if (art is not null)
                    await Dispatcher.InvokeAsync(() => vm.Art = art);
                var logoPath = await GameArt.GetGridLogoPathAsync(vm.Game).ConfigureAwait(false);
                if (gen != _artGen) return;
                if (logoPath is not null && GameArt.LoadFromFile(logoPath) is { } logo)
                    await Dispatcher.InvokeAsync(() => vm.Logo = logo);
            }
            finally { ArtGate.Release(); }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    // ── Favorite / hide-from-grid ────────────────────────────────────────────────

    /// <summary>△: toggle the selected game's favorite pin. Favorites sort to the top of every view;
    /// pressing again un-favorites.</summary>
    public void ToggleFavorite()
    {
        if (SelectedGame is not { } g) return;
        bool nowFavorite = !GameMetadata.IsFavorite(g);
        GameMetadata.SetFavorite(g, nowFavorite);
        Sfx.SliceArmed();
        RefreshKeepingSelection(g);
        UpdateFavoriteCaption();   // the pin just flipped; so does the tip
        Say(Loc.F(nowFavorite ? UiText.Grid.Favorited : UiText.Grid.Unfavorited, g.Name), AnnouncementKind.Result);
    }

    /// <summary>The △ hint reads "Unfavorite" on an already-pinned game, "Favorite" otherwise. Must be
    /// called after a toggle as well as on selection change: <see cref="RefreshKeepingSelection"/> can
    /// re-select the same index, which raises no SelectionChanged.</summary>
    private void UpdateFavoriteCaption() =>
        FavoriteCaption.Text = SelectedGame is { } g && GameMetadata.IsFavorite(g) ? Loc.T(UiText.Grid.Unfavorite) : Loc.T(UiText.Grid.Favorite);

    // □ hold-to-confirm hide: a stray tap must not vanish a game. Releasing early cancels. Un-hide via
    // Settings ▸ Advanced ▸ Game Grid ▸ Reset Hidden Games.
    //
    // The same gesture on a STOREFRONT card ("Open <store>") hides that whole storefront. One dwell, one
    // spinner, one fire SFX for both; only the commit differs (GameArt's own JSON for a game, a config
    // write via the host for a storefront).
    private DispatcherTimer? _hideHoldTimer;
    private GameTileVM? _hideHoldVm;
    private InstalledGame? _hideHoldGame;
    private string? _hideHoldStore;      // set instead of _hideHoldGame when the held tile is a storefront card
    private const int HideHoldMs = 800;

    /// <summary>Raised when a storefront-card hold completes. Arg = the <see cref="InstalledGame.Storefront"/>
    /// name, NOT the LauncherCatalog key — the config list holds the storefront value App.LoadGames filters
    /// on. The host persists it into SystemConfig.DisabledStorefronts; writing config from the overlay
    /// would race the hot-reload watcher.</summary>
    public event Action<string>? StorefrontHideRequested;

    /// <summary>Pick mode (choosing a game for the in-wheel editor) suppresses hold-to-hide on a storefront
    /// card: vanishing the store mid-pick, with the config write landing behind it, is a trap.</summary>
    private bool _pickMode;

    // ── Storefront hide-confirm toast ───────────────────────────────────────────
    // A storefront hold does NOT commit on the dwell: the dwell raises this toast and only ✕ commits
    // (Activate routes here first). ○ cancels via CancelHideConfirm — the host asks before treating ○ as
    // "close the grid". A game-tile hold still commits on the dwell.
    private string? _confirmHideStore;

    /// <summary>The hide-confirm toast is up — ✕/○ belong to it until it's answered.</summary>
    public bool HideConfirmActive => _confirmHideStore is not null;

    private void ShowHideConfirm(string store)
    {
        _confirmHideStore      = store;
        HideConfirmTitle.Text  = Loc.F(UiText.Grid.HideStoreTitle, store);
        HideConfirmAction.Text = Loc.F(UiText.Grid.HideStore, store);
        HideConfirmToast.Visibility = Visibility.Visible;
        Sfx.SliceArmed();   // attention tick, not the fire — nothing is committed yet
        // Mirrors the toast, including which buttons answer it — nothing commits until ✕.
        Say(Loc.F(UiText.Grid.HideStoreSpoken, store,
                  ControllerButtons.Spoken(PadButton.Cross, capitalize: true), ControllerButtons.Spoken(PadButton.Circle)), AnnouncementKind.Result);
    }

    /// <summary>○ while the toast is up: dismiss without hiding. Returns true when it consumed the press
    /// (the host only closes the grid on ○ when this says no toast was up).</summary>
    public bool CancelHideConfirm()
    {
        if (_confirmHideStore is null) return false;
        DismissHideConfirm();
        Say(Loc.T(UiText.Narration.Cancelled), AnnouncementKind.Result);
        return true;
    }

    private void DismissHideConfirm()
    {
        _confirmHideStore = null;
        HideConfirmToast.Visibility = Visibility.Collapsed;
    }

    public void SetHideHeld(bool pressed)
    {
        if (HideConfirmActive) return;   // toast up: □ is dead until it's answered
        if (!pressed)
        {
            _hideHoldTimer?.Stop();
            if (_hideHoldVm is { } v)
            {
                v.IsLoading = false;
                Say(Loc.T(UiText.Narration.HoldCancelled), AnnouncementKind.Result);   // hide stage 3: released before the dwell
            }
            _hideHoldVm = null; _hideHoldGame = null; _hideHoldStore = null;
            return;
        }
        if (List.SelectedItem is not GameTileVM vm) return;
        _hideHoldGame = null; _hideHoldStore = null;
        if (vm.IsLauncher)
        {
            if (_pickMode) return;
            // The card is built from a LauncherInfo, whose Key ("steam") is NOT the storefront name
            // ("Steam") the config list is compared against — resolve it back through the catalog.
            if (LauncherCatalog.Find(vm.LauncherKey) is not { Storefront: { } store }) return;
            _hideHoldStore = store;
        }
        else if (SelectedGame is { } g) _hideHoldGame = g;
        else return;

        _hideHoldVm   = vm;
        vm.IsLoading  = true;
        _hideHoldTimer ??= MakeHideHoldTimer();
        _hideHoldTimer.Stop();
        _hideHoldTimer.Start();
        // Hide stage 1: this is a destructive edit — name the target before anything commits.
        Say(Loc.F(UiText.Grid.HoldToHide, _hideHoldStore is { } s ? s : _hideHoldGame?.Name ?? ""), AnnouncementKind.Result);
    }

    private DispatcherTimer MakeHideHoldTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HideHoldMs) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (_hideHoldVm is { } v) v.IsLoading = false;
            _hideHoldVm = null;
            // Storefront: the dwell only ASKS — the commit waits for ✕ on the confirm toast.
            if (_hideHoldStore is { } store) { _hideHoldStore = null; ShowHideConfirm(store); return; }
            if (_hideHoldGame is not { } g) return;
            _hideHoldGame = null;
            GameMetadata.SetHidden(g, true);
            Sfx.SliceFired();
            Say(Loc.F(UiText.Grid.Hidden, g.Name), AnnouncementKind.Result);
            ApplyFilter(_filterKey);   // the tile disappears; selection clamps to the list
        };
        return t;
    }

    /// <summary>Commit a storefront hide: persist it (host), then drop the store from this open grid — its
    /// games, its chip, and the filter itself. MUST fall back to "All": leaving <see cref="_filterKey"/>
    /// on a chipless filter makes the next L1/R1 cycle land somewhere the strip can't show.</summary>
    private void HideStorefront(string store)
    {
        StorefrontHideRequested?.Invoke(store);
        Sfx.SliceFired();
        Say(Loc.F(UiText.Grid.Hidden, store), AnnouncementKind.Result);

        var off = new List<string>(DisabledStorefronts) { store };
        DisabledStorefronts = off;
        _allGames = [.. _allGames.Where(g => !string.Equals(g.Storefront, store, StringComparison.OrdinalIgnoreCase))];
        BuildLauncherStrip();
        ApplyFilter(null);                                            // back to All
        if (Launchers.Items.Count > 0) Launchers.SelectedIndex = 0;    // …and the strip shows All as active
    }

    /// <summary>Re-run the current filter (order may have changed) and keep <paramref name="g"/> selected.</summary>
    private void RefreshKeepingSelection(InstalledGame g)
    {
        ApplyFilter(_filterKey);
        if (List.ItemsSource is not IReadOnlyList<GameTileVM> tiles) return;
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].Game is { } tg && tg.LaunchUrl == g.LaunchUrl)
            {
                List.SelectedIndex = i;
                List.ScrollIntoView(List.SelectedItem);
                return;
            }
    }

    // ── Navigation ────────────────────────────────────────────────────────────
    /// <summary>Move the grid selection by a delta. The launcher strip is never focused here — up
    /// from the top row just clamps.</summary>
    public void Move(int dx, int dy)
    {
        int n = List.Items.Count;
        if (n == 0) return;
        int prev = List.SelectedIndex;
        int i = prev < 0 ? 0 : prev;
        // Column count follows the band the CURRENT tile is in (non-favorites pack one column tighter),
        // so an up/down step lands roughly a visual row away in either band.
        int baseCols = GridColumns(List.ActualWidth);
        int cols = List.Items[i] is GameTileVM { FavoriteTile: false, IsDivider: false }
            ? baseCols + 1 : baseCols;
        i = Math.Clamp(i + dx + dy * cols, 0, n - 1);
        // Never land ON the divider — step past it in the direction of travel.
        if (List.Items[i] is GameTileVM { IsDivider: true })
            i = Math.Clamp(i + (dx + dy >= 0 ? 1 : -1), 0, n - 1);
        List.SelectedIndex = i;
        List.ScrollIntoView(List.SelectedItem);
        if (i != prev)
        {
            Sfx.SliceArmed();   // same tick as arming a slice (launch reuses the fire SFX via FireAction)
            AnnounceFocused();
        }
    }

    /// <summary>Cycle the launcher filter (L1/R1), wrapping. The selection drives ApplyFilter.</summary>
    public void CycleFilter(int dir)
    {
        int n = Launchers.Items.Count;
        if (n == 0) return;
        int i = Launchers.SelectedIndex < 0 ? 0 : Launchers.SelectedIndex;
        int next = ((i + dir) % n + n) % n;
        if (next != Launchers.SelectedIndex) Sfx.SliceArmed();   // same quiet tick as grid moves / arming
        Launchers.SelectedIndex = next;
    }

    /// <summary>✕ in the browser: confirm a pending storefront hide (toast up), else launch the focused
    /// game or "Open …" tile.</summary>
    public void Activate()
    {
        if (_confirmHideStore is { } store) { DismissHideConfirm(); HideStorefront(store); return; }
        ActivateGrid();
    }

    private void ActivateGrid()
    {
        if (List.SelectedItem is not GameTileVM tile) return;
        if (tile.IsLauncher) LauncherChosen?.Invoke(tile.LauncherKey!);
        else if (tile.Game is { } g) GameChosen?.Invoke(g);
    }
}

/// <summary>Top-strip launcher chip: brand glyph + name. The "All" chip clears the filter.</summary>
public sealed class LauncherTileVM
{
    public string       Key  { get; }
    public string       Name { get; }
    public ImageSource? Icon { get; }
    public bool         IsAll { get; }

    public LauncherTileVM(LauncherInfo info)
    {
        Key  = info.Key;
        Name = info.ShortName;
        Icon = info.BrightGlyph();
    }

    private LauncherTileVM()   // the "All" chip
    {
        Key = ""; IsAll = true; Name = Loc.T(UiText.Grid.All);
        var brush = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        brush.Freeze();
        Icon = PackIconHelper.FromName("DotsGrid", brush);
    }

    public static LauncherTileVM AllChip() => new();
}

/// <summary>Grid tile: a game, or an "Open &lt;launcher&gt;" entry shown first while filtering.</summary>
public sealed class GameTileVM : INotifyPropertyChanged
{
    public InstalledGame? Game { get; }
    public string?        LauncherKey { get; }
    public bool           IsLauncher => LauncherKey is not null;
    public string         Title { get; }
    public string         Subtitle { get; }

    private ImageSource? _art;
    public ImageSource? Art
    {
        get => _art;
        set { _art = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Art))); }
    }

    private ImageSource? _logo;
    public ImageSource? Logo
    {
        get => _logo;
        set { _logo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Logo))); }
    }

    public string?                DefaultCover { get; set; }   // auto-resolved cover (cycle candidate 0); "" = none
    public IReadOnlyList<string>? CoverUrls    { get; set; }   // SteamGridDB cover candidates (lazy)
    public int                    CoverIndex   { get; set; }   // current Select cycle index (default, art…, flats)
    public bool                   CoverIndexResumed { get; set; }   // CoverIndex has been synced to the SAVED pick once
    public bool                   LogoIndexResumed  { get; set; }   // same, for the logo cycle's fingerprinted pick
    public IReadOnlyList<string>? LogoUrls     { get; set; }   // SteamGridDB logo candidates (lazy, Start cycle)
    public int                    LogoIndex    { get; set; }   // current Start logo stop (0 = default resolution)

    private bool _showOverlay = true;
    public bool ShowOverlay   // false = show the raw cover, hiding our logo/title overlay
    {
        get => _showOverlay;
        set { _showOverlay = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowOverlay))); }
    }

    private bool _dotsAtTop;
    public bool DotsAtTop     // true = the dot row is the LOGO (Start) cycle, drawn along the tile's top edge
    {
        get => _dotsAtTop;
        set { _dotsAtTop = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DotsAtTop))); }
    }

    // Cover-cycle position indicator: one dot per cycle stop, the current one highlighted. Fades out
    // (DotsOpacity → 0) shortly after the last cycle; driven by the control's single fade timer.
    public ObservableCollection<CoverDot> Dots { get; } = new();

    private double _dotsOpacity;   // 0 = hidden; set to 1 on each cycle, then faded down
    public double DotsOpacity
    {
        get => _dotsOpacity;
        set { _dotsOpacity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DotsOpacity))); }
    }

    private bool _isLoading;   // true while a Select cover-cycle press is fetching art → tile shows a spinner
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Spin)));
        }
    }

    /// <summary>The spinner's ROTATION gate only. Under Reduce Motion the scrim + arc still show (IsLoading
    /// drives visibility — the loading state is essential); the arc just holds still.</summary>
    public bool Spin => _isLoading && !MotionPolicy.Reduce;
    public void RefreshSpin() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Spin)));

    /// <summary>Size the dot row to <paramref name="total"/> stops and highlight <paramref name="current"/>.</summary>
    public void UpdateCycleDots(int total, int current)
    {
        while (Dots.Count > total) Dots.RemoveAt(Dots.Count - 1);
        while (Dots.Count < total) Dots.Add(new CoverDot());
        for (int k = 0; k < Dots.Count; k++) Dots[k].IsActive = k == current;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Pinned-favorite tile — keeps the base tile size; non-favorites render smaller.</summary>
    public bool FavoriteTile { get; init; }

    /// <summary>The full-row band divider between favorites and the rest — never selectable.</summary>
    public bool IsDivider { get; private init; }

    public static GameTileVM MakeDivider() => new() { IsDivider = true };

    private GameTileVM() { Title = ""; Subtitle = ""; }

    public GameTileVM(InstalledGame game)
    {
        Game = game;
        // Don't prefix favorites with ★: the pinned band + bigger tile IS the signal, and the star
        // pollutes the title-as-logo rendering on tiles with no logo art.
        FavoriteTile = GameMetadata.IsFavorite(game);
        Title = game.Name;
        Subtitle = game.Storefront;
    }

    public GameTileVM(LauncherInfo info)   // "Open <launcher>" tile
    {
        LauncherKey = info.Key;
        Title = Loc.F(UiText.Grid.OpenLauncher, info.ShortName);
        Subtitle = "";
        _art = info.BrightGlyph();
    }
}

/// <summary>One dot in the cover-cycle position indicator. <see cref="IsActive"/> = the current stop.</summary>
public sealed class CoverDot : INotifyPropertyChanged
{
    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Picks the game vs. "Open launcher" grid template by tile kind.</summary>
public sealed class GameTileTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Game { get; set; }
    public DataTemplate? Launcher { get; set; }
    public DataTemplate? Divider { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        GameTileVM { IsDivider: true }  => Divider,
        GameTileVM { IsLauncher: true } => Launcher,
        _                               => Game,
    };
}

/// <summary>ImageSource → tile background: an UniformToFill ImageBrush, or a dark fallback.</summary>
public sealed class ArtBrushConverter : IValueConverter
{
    private static readonly Brush Fallback = Frozen(new SolidColorBrush(Color.FromArgb(255, 24, 24, 32)));

    // Frozen + cached per ImageSource: the binding re-converts on art cycling and material repaints, and
    // an unfrozen brush keeps a per-tile changed-handler subscription alive on the render thread.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource, ImageBrush>
        BrushCache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ImageSource img
            ? BrushCache.GetValue(img, static i =>
              { var b = new ImageBrush(i) { Stretch = Stretch.UniformToFill }; b.Freeze(); return b; })
            : Fallback;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
}

/// <summary>[ListBox width, tile VM] → per-tile size at a 2:1 aspect: favorites use the base column count
/// (GridColumns); everything in the second band — including an unfavorited "Open &lt;storefront&gt;" tile —
/// is one column narrower per row; the favorites divider spans the full row, which is what forces the wrap
/// between the bands. Default returns the width; ConverterParameter "h" the height.</summary>
public sealed class TileSizeConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        double listW = values.Length > 0 && values[0] is double d && d > 0 ? d : 0;
        var vm       = values.Length > 1 ? values[1] as GameTileVM : null;
        bool height  = (parameter as string) == "h";

        if (vm is { IsDivider: true })
            return height ? 22.0 + TileHeadroom : Math.Max(1, listW - 14.0);

        int cols = GameBrowserControl.GridColumns(listW);
        // Band membership, NOT tile kind: must agree with GameBrowserControl.Move's column count and with
        // how ApplyFilter places the tiles, or d-pad rows and visual rows drift apart.
        if (vm is { FavoriteTile: false, IsDivider: false }) cols += 1;   // the second band packs tighter
        double colW = Math.Max(1, (listW - cols * 12.0 - 2) / cols);       // room for each tile's 6px margins + slack
        // Height carries the template's in-item headroom on top of the 2:1 face (see the GameTile template).
        return height ? colW / 2.0 + TileHeadroom : colW;
    }

    /// <summary>The blank strip the GameTile template reserves at the top of every item so selection chrome
    /// that escapes the face (Terra's lift, Kawaii's outset stroke) stays inside the item's bounds — the
    /// ScrollViewer's content clip can never shear it. Must equal the template root's top margin.</summary>
    internal const double TileHeadroom = 10.0;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>[ActualWidth, ActualHeight] + radius parameter → a rounded <see cref="RectangleGeometry"/> for
/// an element's Clip. Border.CornerRadius rounds the border and its own background but does NOT clip child
/// content, so without this square key art overlaps a tile's rounded outline and cuts into it.</summary>
public sealed class RoundedClipConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double w || values[1] is not double h) return null;
        if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return null;
        double r = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 0;
        var g = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        g.Freeze();
        return g;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
