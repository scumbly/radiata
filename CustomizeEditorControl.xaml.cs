using System.Linq;
using System.Windows;
using System.Windows.Controls;   // bare ComboBox/Button/Grid resolve to WPF here
using CheckBox = System.Windows.Controls.CheckBox;   // disambiguate from WinForms.CheckBox (referenced app-wide)
using System.Windows.Controls.Primitives;   // UniformGrid (the tile grids)
using System.Windows.Media;                  // Brush / SolidColorBrush / geometry for the material + thickness tiles
using System.Windows.Media.Imaging;          // BitmapImage — local Salvage tile texture load

namespace ControllerWheel;

/// <summary>The Settings "Customize" tab — peer top-level sections: Material / Sound effects tile
/// selectors, the Button icons tile picker and the "Show labels on" picker (left column), then Slice
/// thickness, the trigger chord-builder, and the D-Pad ◀ ▶ picker (right column). Edits its own slice of
/// <see cref="SystemConfig"/> via <see cref="ApplyTo"/>; the host (SettingsWindow) layers it and the
/// Advanced tab onto the loaded config, so pass-through fields survive.
/// <para>⚠ No Accessibility section lives here — the whole block lives on Advanced
/// (<see cref="SystemEditorControl"/>), which owns every one of its fields. Advanced's ApplyTo runs last
/// over this tab's `with`, so writing any of them from here is silently discarded.</para></summary>
public partial class CustomizeEditorControl : UserControl
{
    public event EventHandler? Changed;

    /// <summary>The tab-level Help chip, top-right — SettingsWindow jumps to the Help topic.</summary>
    public event Action<string>? HelpRequested;
    private void HelpCustomize_Click(object sender, RoutedEventArgs e)      => HelpRequested?.Invoke("customize");
    private void HelpTriggers_Click(object sender, RoutedEventArgs e)       => HelpRequested?.Invoke("triggers");
    // D-Pad + "Show labels on" live here: both are choices about how the wheel looks and behaves.
    // Accessibility belongs to Settings ▸ Advanced.
    private void HelpShowLabels_Click(object sender, RoutedEventArgs e)     => HelpRequested?.Invoke("show-labels");
    private void HelpVolumeMixer_Click(object sender, RoutedEventArgs e)    => HelpRequested?.Invoke("volume-mixer");

    private bool _loading;
    private ControllerKind _kind = ControllerKind.DualSenseEdge;     // the detected controller this tab edits
    private Dictionary<string, List<string>> _triggerModes = new();  // per-kind trigger choices (other kinds preserved)
    private SystemConfig _loadedCfg = new();                          // base for Reset (keeps unrelated fields intact)

    /// <summary>Whether a pad is actually present. Only the Best-guess tile's preview reads it, and only to
    /// mirror App.ApplyGlyphSet: with no pad, "auto" resolves to Xbox, so the tile must not promise
    /// PlayStation shapes. Set before <see cref="Load"/>; SettingsWindow pushes live changes.</summary>
    public bool ControllerConnected { get; set; }

    public CustomizeEditorControl()
    {
        InitializeComponent();
        // A static event: subscribe only while on screen, or a closed Settings window stays reachable.
        Loaded   += (_, _) => { PackageInstallFlow.RegistryChanged -= OnPackageRegistryChanged;
                                PackageInstallFlow.RegistryChanged += OnPackageRegistryChanged;
                                OnPackageRegistryChanged(); };
        Unloaded += (_, _) => PackageInstallFlow.RegistryChanged -= OnPackageRegistryChanged;
        // The heading carries the D-pad arrow pair, which must stay left-to-right inside an Arabic heading.
        if (DpadHeadingRun.Parent is TextBlock heading)
        {
            var isolated = InlineMarkup.IsolateArrows(DpadHeadingRun);
            if (!ReferenceEquals(isolated, DpadHeadingRun))
            {
                heading.Inlines.InsertBefore(DpadHeadingRun, isolated);
                heading.Inlines.Remove(DpadHeadingRun);
            }
        }
    }

    public void Load(SystemConfig cfg, ControllerKind kind)
    {
        _loading = true;
        _kind = kind;
        _loadedCfg = cfg;
        _triggerModes = cfg.TriggerModes.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value));
        PopulateTriggerModes(cfg);
        BuildTiles();
        _thickness     = cfg.SliceThickness is "thin" or "medium" or "thick" ? cfg.SliceThickness! : "medium";
        _thickAutoDemoted = cfg.ThickAutoDemoted;
        _sliceMaterial = NormalizeMaterial(cfg.SliceMaterial); _materialPicked = false;
        _glyphs        = cfg.ButtonGlyphs is "auto" or "playstation" or "xbox" ? cfg.ButtonGlyphs! : "auto";
        SelectTile(ThicknessTiles,     _thickness);
        SelectMaterialTile(_sliceMaterial);
        RefreshSliceTiles();   // selection AND material (accent colour) may have changed
        SelectTile(ButtonGlyphsTiles,  _glyphs);
        RefreshGlyphTiles();   // selection AND the detected kind (Best guess's preview row) may have changed
        // D-Pad + "Show labels on" — this tab owns both fields.
        foreach (ComboBoxItem it in DpadModeBox.Items)
            if (string.Equals(it.Tag as string, cfg.DpadHorizontalMode, StringComparison.OrdinalIgnoreCase))
                { DpadModeBox.SelectedItem = it; break; }
        if (DpadModeBox.SelectedIndex < 0) DpadModeBox.SelectedIndex = 0;   // unknown token → Cycles Windows
        RefreshMixPairReadout();
        SelectShowSliceLabels(SliceLabelRule.Normalize(cfg.ShowSliceLabels));   // SliceLabelRule owns the tokens
        // Combined Sound-effects selection: Silent = SoundEffects off; else the theme (legacy tokens map).
        _sfx = !cfg.SoundEffects ? "none" : NormalizeTheme(cfg.SoundTheme, _sliceMaterial);
        // Seed what Silent preserves from the STORED theme, not the field default — otherwise a saved
        // Digital/Physical pick is silently rewritten to "material" by any save made while muted.
        _lastTheme = _sfx == "none" ? NormalizeTheme(cfg.SoundTheme, _sliceMaterial) : _sfx;
        SelectTile(SoundFxTiles, _sfx);
        RefreshSoundTiles();   // the selected tile's glyph carries its accent colour
        UpdateThicknessForSliceCount();
        _loading = false;
    }

    /// <summary>Fold this tab's fields into <paramref name="cfg"/> (a `with` copy — every field this tab
    /// doesn't own passes through untouched).</summary>
    public SystemConfig ApplyTo(SystemConfig cfg) => cfg with
    {
        SliceThickness    = _thickness,
        ThickAutoDemoted  = _thickAutoDemoted,
        SliceMaterial     = _sliceMaterial,
        GameGridMaterial  = _sliceMaterial,   // TEMPORARY: the grid follows the wheel (its tiles are disabled)
        HeldSliceMaterial    = _materialPicked ? null : cfg.HeldSliceMaterial,
        HeldGameGridMaterial = _materialPicked ? null : cfg.HeldGameGridMaterial,
        ButtonGlyphs      = _glyphs,
        // ⚠ The six Accessibility fields (SwapFnButtons / WheelIgnoresOppositeStick / AlwaysShowHub /
        // ReduceMotion / Narration / TriggerActivation) belong to Settings ▸ Advanced and must NOT be
        // written here — SystemEditorControl owns them and its ApplyTo runs LAST over this `with`, so a
        // write here would be silently discarded. D-Pad + Show labels are this tab's.
        DpadHorizontalMode = (DpadModeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "switcher",
        ShowSliceLabels   = (ShowSliceLabelsBox.SelectedItem as ComboBoxItem)?.Tag as string ?? SliceLabelRule.Default,
        TriggerModes      = TriggerModesForSave(),
        SoundEffects      = _sfx != "none",
        // "Silent" keeps the LAST theme picked this session (_lastTheme, not the window-open snapshot,
        // which would discard a mid-session theme change); else the selection IS the theme.
        SoundTheme        = _sfx is "material" or "physical" or "digital" ? _sfx : _lastTheme,
    };

    /// <summary>A Sound-effects tile click also PREVIEWS the pick's fire sound ("Themed" resolves to the
    /// selected material's own set; silent for "none") — the point of the picker is to hear the difference.
    /// An explicit Digital/Physical pick holds until the next MATERIAL change, which snaps the picker back
    /// to Themed (follow).</summary>
    private void SelectSound(string tag)
    {
        bool themeOwnsSounds = Materials.CustomFor(_sliceMaterial)?.Sounds is not null;
        // While a drop-in theme owns the sounds, only "Themed" (its own set) and "None" (mute) are
        // selectable. The override tiles are disabled, so this is the belt to that braces — a keyboard
        // invoke must not slip past it.
        if (tag is "digital" or "physical" && themeOwnsSounds) return;
        // "None" is a TOGGLE while the theme owns the sounds, or muting would be a one-way door out of
        // sound. Un-muting returns to Material — what plays for any event the theme doesn't override.
        if (tag == "none" && themeOwnsSounds && _sfx == "none")
            tag = "material";
        ApplySound(tag);
        Sfx.PreviewFire(tag == "none" ? "none" : Sfx.ResolveSet(tag, _sliceMaterial));
    }

    /// <summary>Shared by <see cref="SelectSound"/> and the Material auto-pairing.</summary>
    private void ApplySound(string tag)
    {
        _sfx = tag;
        if (tag is "material" or "physical" or "digital") _lastTheme = tag;   // "none" preserves this on save
        SelectTile(SoundFxTiles, tag);
        RefreshSoundTiles();
        Fire();
    }

    /// <summary>Map the stored theme (incl. the legacy tokens) onto the current picker: "material" /
    /// "digital" / "physical" / (anything else, incl. the retired kawaii tokens) → "material". An explicit
    /// override that matches what the material would resolve to anyway reads back as Material — that's how
    /// every pre-"material" config (whose theme was always re-paired to the material) lands on the new
    /// default tile instead of showing a phantom override.</summary>
    private static string NormalizeTheme(string? theme, string material)
    {
        string t = theme is "physical" or "tap" ? "physical"
                 : theme is "digital" or "classic" ? "digital"
                 : "material";
        return t != "material" && t == Materials.SoundThemeFor(material) ? "material" : t;
    }

    /// <summary>Re-seed ONLY the thickness selection from a just-written config — the host calls this after a
    /// save, because <see cref="SliceThicknessRule"/> can move thickness after <see cref="ApplyTo"/> ran.
    /// Deliberately not a full <see cref="Load"/>, which would stomp the trigger builder and every other
    /// in-flight field on this tab.</summary>
    public void ReloadThickness(SystemConfig cfg)
    {
        bool prev = _loading; _loading = true;
        _thickness = cfg.SliceThickness is "thin" or "medium" or "thick" ? cfg.SliceThickness! : "medium";
        _thickAutoDemoted = cfg.ThickAutoDemoted;
        SelectTile(ThicknessTiles, _thickness);
        RefreshSliceTiles();
        _loading = prev;
    }

    /// <summary>The host calls this on load and after every wheel edit. Past
    /// <see cref="SliceThicknessRule.ThickSliceLimit"/> the Thick tile can't apply, so it comes off the
    /// board.</summary>
    public void SetSliceCounts(int wheelA, int wheelB)
    {
        _sliceCountA = wheelA; _sliceCountB = wheelB;
        UpdateThicknessForSliceCount();
    }

    /// <summary>Past the Thick limit slices pack small enough that Thick reads as one solid ring, so the
    /// tile comes off the board entirely — don't replace it with an in-place explanatory message; that reads
    /// as a pseudo-button. The explanation lives in the "customize" Help topic's Slice Thickness bullet.
    /// A currently-Thick selection bumps to Medium and is recorded as OUR demotion, so shrinking the wheels
    /// back restores it — <see cref="SliceThicknessRule"/> owns that half, re-checked on every save.</summary>
    private void UpdateThicknessForSliceCount()
    {
        bool allowed = SliceThicknessRule.ThickAllowed(_sliceCountA, _sliceCountB);
        bool wasAllowed = _thickAllowed;
        _thickAllowed = allowed;

        if (!allowed && _thickness == "thick")
        {
            _thickness = "medium";
            _thickAutoDemoted = true;
            SelectTile(ThicknessTiles, "medium");
        }

        var thickTile = ThicknessTiles.Children.OfType<Border>().FirstOrDefault(b => (string?)b.Tag == "thick");
        if (thickTile is not null)
            thickTile.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
        if (allowed && !wasAllowed) RefreshSliceTiles();   // restore the MiniWheel preview once it's back
    }

    // ── Tile selectors (mirror onboarding's Material step) ──────────────────────
    private string _thickness     = "medium";
    private bool   _thickAutoDemoted;          // see SystemConfig.ThickAutoDemoted
    // Live per-wheel slice counts, pushed in by the host (SetSliceCounts). 0/0 until then, which allows
    // Thick — Load runs before the host can supply them and must not flicker the tile away.
    private int    _sliceCountA, _sliceCountB;
    // Mirrors SliceThicknessRule.ThickAllowed — set ONLY by UpdateThicknessForSliceCount, which also
    // collapses the Thick tile while this is false; RefreshSliceTiles skips a collapsed tile's content.
    private bool   _thickAllowed = true;
    private string _sliceMaterial = "pearl";
    private bool   _materialPicked;   // a deliberate pick releases SystemConfig.Held*Material
    private string _glyphs        = "auto";
    private string _sfx           = "material";   // "material" | "digital" | "physical" | "none"
    private string _lastTheme     = "material";   // last non-none theme (what "Silent" preserves on save)

    private static readonly Brush TileSel  = new SolidColorBrush(Color.FromRgb(0x1C, 0xA8, 0xC9));
    private static readonly Brush TileRest = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));
    private static readonly Brush TriggerPlusInk = MakeFrozen((Color)System.Windows.Application.Current.Resources["UiMutedInkColor"]);   // muted chrome ink
    private static Brush MakeFrozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    // A cool-white top sheen laid OVER the Gloss Dark swatch: the preview brush alone reads flat at swatch
    // size, because the live wheel adds a dome the preview omits.
    private static readonly Brush GlossDarkSheen = MakeGlossDarkSheen();
    private static Brush MakeGlossDarkSheen()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0.35, 0), EndPoint = new Point(0.5, 1) };
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x6E, 0xE4, 0xF5, 0xFF), 0.00));   // bright cool sheen at the top
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x22, 0xCF, 0xEC, 0xFF), 0.30));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.58));   // fade out before mid
        g.Freeze();
        return g;
    }
    /// <summary>The eight material tiles in GRID ORDER — the UniformGrid is 4-across, so entries 0-3 are the
    /// top row (lights + the two warm styled materials) and 4-7 the bottom (darks + the two dark styled
    /// ones). <see cref="OnboardingWindow"/>'s step-2 swatches carry the SAME order in its own 4-across grid
    /// — change both together.
    /// <para>Row 1's label shadow is drawn at 75% of the shared opacity: those four tiles are the pale ones,
    /// where the full-strength shadow reads as grime rather than a lift.</para></summary>
    private static readonly (string Tag, string Name)[] MaterialTiles =
        [
            // Row 1
            ("flat-light",  "Flat\nLight"),
            ("pearl",       "Pearl"),
            ("mesa",        "Mesa"),
            ("kawaii",      "Kawaii"),
            // Row 2
            ("flat-dark",   "Flat\nDark"),
            ("obsidian",    "Obsidian"),
            ("salvage",     "Salvage"),
            ("reactor",     "Reactor"),
        ];

    /// <summary>How many of <see cref="MaterialTiles"/>'s entries make up the grid's first row — the pale
    /// four, which take the dimmed label shadow. The array order is chosen so entry 0 is Simple's top tile
    /// and entries 1-3 are Deluxe's top row, so "index &lt; 4" is literally the top row of each box. The
    /// onboarding Look step splits the same array the same way for the same reason.</summary>
    private const int MaterialTileColumns = 4;

    /// <summary>Which of the two Material group boxes a token lives in: the two flat materials are Simple,
    /// everything else Deluxe. Deliberately WIDER than <see cref="Materials.IsPremium"/> — Pearl and
    /// Obsidian sit in Deluxe but have no styled look. Mirrors <c>OnboardingWindow.IsSimpleMaterial</c>;
    /// change both together.</summary>
    private static bool IsSimpleMaterial(string tag) => tag.StartsWith("flat", StringComparison.Ordinal);

    /// <summary>Highlight a material across BOTH group boxes (see the two-grid <see cref="SelectTile"/>).</summary>
    private void SelectMaterialTile(string tag) =>
        SelectTile(tag, liftLabel: true, SliceMaterialTiles, SliceMaterialTilesDeluxe, SliceMaterialTilesCustom);

    /// <summary>Row 1's label-shadow opacity as a fraction of the shared per-material value. Internal so the
    /// onboarding step's swatches, which share the same order, dim in step.</summary>
    internal const double MaterialTopRowShadowScale = 0.75;

    private void BuildTiles()
    {
        if (ThicknessTiles.Children.Count > 0) return;   // build once

        // A manual thickness pick is a deliberate decision: it clears ThickAutoDemoted, which is what stops
        // SliceThicknessRule from later restoring Thick over the top of it.
        foreach (var (tag, name, inner) in ThicknessOptions)
            ThicknessTiles.Children.Add(Tile(tag, PreviewTile(MiniWheel(inner), null), 56,
                () =>
                {
                    _thickness = tag; _thickAutoDemoted = false;
                    SelectTile(ThicknessTiles, tag); RefreshSliceTiles(); UpdateThicknessForSliceCount(); Fire();
                },
                accessibleName: Loc.F(UiText.Customize.SlicesOf, name)));

        for (int mi = 0; mi < MaterialTiles.Length; mi++)
        {
            var (tag, name) = MaterialTiles[mi];
            var host = IsSimpleMaterial(tag) ? SliceMaterialTiles : SliceMaterialTilesDeluxe;
            host.Children.Add(MaterialTile(tag, name, mi < MaterialTileColumns,
                () =>
                {
                    _sliceMaterial = tag; _materialPicked = true; SelectMaterialTile(tag); RefreshSliceTiles();
                    // The Sound-effects pick ALWAYS follows the new Material — a material and its sounds are
                    // one look; don't reintroduce a manual-override latch. The user can still override the
                    // sound afterwards, until the next material change.
                    ApplySound("material");
                    Fire();
                }));
        }

        BuildCustomMaterialTiles();

        // Button icons: tile contents are rebuilt on every pick/load via RefreshGlyphTiles.
        void Pick(string tag) { _glyphs = tag; SelectTile(ButtonGlyphsTiles, tag); RefreshGlyphTiles(); Fire(); }
        _autoTile = Tile("auto",        new Grid(), 56, () => Pick("auto"),        Loc.T(UiText.Customize.BestGuessIcons));
        _psTile   = Tile("playstation", new Grid(), 56, () => Pick("playstation"), Loc.T(UiText.Customize.PlayStationIcons));
        _xboxTile = Tile("xbox",        new Grid(), 56, () => Pick("xbox"),        Loc.T(UiText.Customize.XboxIcons));
        ButtonGlyphsTiles.Children.Add(_autoTile);
        ButtonGlyphsTiles.Children.Add(_psTile);
        ButtonGlyphsTiles.Children.Add(_xboxTile);
        RefreshGlyphTiles();

        // Sound effects: label-over-glyph tiles; MDI glyphs via PackIconHelper. Themed leads and is
        // the default — it plays whatever set the material selected above pairs with (its glyph
        // previews which set that is, in the material's own accent; see ThemedSoundLook).
        _materialTile = Tile("material", new Grid(), 56, () => SelectSound("material"), Loc.T(UiText.Customize.ThemedSfx));
        _digitalTile  = Tile("digital",  new Grid(), 56, () => SelectSound("digital"),  Loc.T(UiText.Customize.DigitalSfx));
        _physicalTile = Tile("physical", new Grid(), 56, () => SelectSound("physical"), Loc.T(UiText.Customize.PhysicalSfx));
        _noneTile     = Tile("none",     new Grid(), 56, () => SelectSound("none"),     Loc.T(UiText.Customize.SilentSfx));
        SoundFxTiles.Children.Add(_materialTile);
        SoundFxTiles.Children.Add(_digitalTile);
        SoundFxTiles.Children.Add(_physicalTile);
        SoundFxTiles.Children.Add(_noneTile);
        RefreshSoundTiles();

        RefreshSliceTiles();

        // (Game-grid material tiles TEMPORARILY removed — the grid follows the wheel material; see ApplyTo.)
    }

    // Shared by BuildTiles and RefreshSliceTiles so the tag/value pairing stays in one place.
    private static readonly (string Tag, string Name, double Inner)[] ThicknessOptions =
        [("thin", "Thin", 17.0), ("medium", "Medium", 14.0), ("thick", "Thick", 8.0)];

    // Xbox face buttons — coloured letters (a font glyph IS the intended shape here).
    private static readonly (string Glyph, Color Color)[] XboxGlyphs =
        [ ("A", Color.FromRgb(0x5A, 0xB5, 0x4A)), ("B", Color.FromRgb(0xD0, 0x48, 0x44)),
          ("X", Color.FromRgb(0x3F, 0x7F, 0xD0)), ("Y", Color.FromRgb(0xE0, 0xAE, 0x2E)) ];

    // Fields so RefreshGlyphTiles can rebuild their content.
    private Border? _autoTile, _psTile, _xboxTile;

    /// <summary>The SELECTED tile's glyphs render in their real colours, the rest in the tile-glyph grey;
    /// the Best-guess tile previews whatever AUTO resolves to for the detected pad — mirrors
    /// App.ApplyGlyphSet (PlayStation shapes only for a pad identified as Sony, Xbox letters otherwise,
    /// including when nothing is connected), so change both together. Called from BuildTiles, every Load,
    /// and each pick.</summary>
    private void RefreshGlyphTiles()
    {
        if (_autoTile is null || _psTile is null || _xboxTile is null) return;
        bool autoIsXbox = !ControllerConnected
            || _kind is ControllerKind.Xbox or ControllerKind.ExtraButtonPad;
        _autoTile.Child = autoIsXbox
            ? GlyphTile(Loc.T(UiText.Customize.BestGuess), XboxGlyphs, colored: _glyphs == "auto")
            : GlyphTilePs(Loc.T(UiText.Customize.BestGuess), colored: _glyphs == "auto");
        _psTile.Child   = GlyphTilePs("PlayStation", colored: _glyphs == "playstation");
        _xboxTile.Child = GlyphTile("Xbox", XboxGlyphs, colored: _glyphs == "xbox");
    }

    // PlayStation face buttons — VECTOR shapes, not font glyphs: all four must share one bounding box +
    // stroke to align (Segoe UI's □ sits low and small, forcing per-glyph size fudges).
    private static readonly Color PsGreen  = Color.FromRgb(0x5C, 0xC6, 0xA8);   // △
    private static readonly Color PsRed    = Color.FromRgb(0xE0, 0x6C, 0x7E);   // ○
    private static readonly Color PsBlue   = Color.FromRgb(0x6C, 0xA6, 0xE0);   // ✕
    private static readonly Color PsPurple = Color.FromRgb(0xD9, 0x8C, 0xC4);   // □

    private void Fire() { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    /// <summary>The "Custom" group under Simple/Deluxe: one tile per registered drop-in theme (approved
    /// packages only), collapsed when empty. Rebuilt whenever the registry changes (an approval, a
    /// drag-and-drop install, an Uninstall). PreviewFill/PreviewIsDark/ApplyPreviewLabelFace/PreviewDecoration
    /// are all registry-aware, so MaterialTile needs no special casing.</summary>
    private void BuildCustomMaterialTiles()
    {
        SliceMaterialTilesCustom.Children.Clear();
        foreach (var pack in Materials.Custom)
        {
            var tag = pack.Token;
            var tile = MaterialTile(tag, pack.Name, topRow: false,
                () =>
                {
                    _sliceMaterial = tag; _materialPicked = true; SelectMaterialTile(tag); RefreshSliceTiles();
                    ApplySound("material");
                    Fire();
                });
            // Right-click (or the Menu key / Shift+F10 on the focused tile) offers Uninstall.
            var menu = new ContextMenu();
            var uninstall = new MenuItem { Header = Loc.T(UiText.Customize.UninstallTheme) };
            uninstall.Click += (_, _) => UninstallTheme(pack);
            menu.Items.Add(uninstall);
            tile.ContextMenu = menu;
            SliceMaterialTilesCustom.Children.Add(tile);
        }
        CustomMaterialGroup.Visibility =
            SliceMaterialTilesCustom.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The registry changed under an open Settings window: rebuild the Custom group and keep the
    /// current pick highlighted. Before the first BuildTiles there is nothing to rebuild.</summary>
    private void OnPackageRegistryChanged()
    {
        if (ThicknessTiles.Children.Count == 0) return;
        BuildCustomMaterialTiles();
        SelectMaterialTile(_sliceMaterial);
    }

    /// <summary>Uninstall a drop-in theme: confirm, send its folder to the Recycle Bin, move the pick to Pearl
    /// if it was the live material, then drop it from the inventory and registry (which rebuilds the Custom
    /// group). The package's consent record stays, so restoring the unchanged folder loads it again without
    /// a prompt.</summary>
    private void UninstallTheme(MaterialPackage pack)
    {
        var owner = Window.GetWindow(this);
        bool active = _sliceMaterial == pack.Token;
        var prompt = Loc.F(UiText.Customize.UninstallThemeConfirm, pack.Name)
                   + (active ? Loc.T(UiText.Customize.UninstallThemeActive) : "");
        if (System.Windows.MessageBox.Show(owner, prompt, Loc.T(UiText.Customize.UninstallThemeCaption),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        if (PackageInstallFlow.Recycle(pack.DirPath) is { } error)
        {
            if (error.Length > 0)   // "" = the user dismissed Windows' own error dialog
                System.Windows.MessageBox.Show(owner, Loc.F(UiText.Customize.UninstallThemeFailed, pack.Name, error),
                    Loc.T(UiText.Customize.UninstallThemeCaption), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Off the token BEFORE unregistering it, so the saved config never names a theme that's gone.
        if (active)
        {
            _sliceMaterial = Materials.GlossLight; _materialPicked = true; SelectMaterialTile(_sliceMaterial); RefreshSliceTiles();
            ApplySound("material");
        }
        PackageInstallFlow.ForgetMaterial(pack);   // raises RegistryChanged → the Custom group rebuilds
        if (active) Fire();
    }

    // InvokableBorder keeps the tiles Borders — SelectTile's OfType<Border>, the Tag reads and the .Child
    // mutations all depend on that — while adding focus, Space/Enter, a UIA Invoke running the SAME onClick,
    // and an accessible name (the visual content is a rendering, not text).
    private static Border Tile(string tag, UIElement content, double height, Action onClick,
                               string? accessibleName = null)
    {
        var t = new InvokableBorder
        {
            Height = height, CornerRadius = new CornerRadius(8), Margin = new Thickness(4),
            Background = Brushes.White, BorderBrush = TileRest, BorderThickness = new Thickness(1),
            Tag = tag, Cursor = System.Windows.Input.Cursors.Hand, Child = content,
            Invoked = onClick,
        };
        t.MouseLeftButtonUp += (_, _) => onClick();
        System.Windows.Automation.AutomationProperties.SetName(t, accessibleName ?? tag);
        t.SetResourceReference(FrameworkElement.FocusVisualStyleProperty, "AccessFocusVisual");
        return t;
    }

    /// <summary>Bottom-right corner badge marking a theme that ships its own sound files: a white
    /// volume glyph on a black disc, sized to read at tile scale without crowding the name.</summary>
    private static UIElement OwnSoundsBadge()
    {
        var badge = new Grid
        {
            Width = 18, Height = 18,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 3, 7),
            IsHitTestVisible = false,
        };
        badge.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Fill = new SolidColorBrush(Color.FromArgb(0xE6, 0x0A, 0x0A, 0x0E)),
            Stroke = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1,
        });
        if (PackIconHelper.FromName("VolumeHigh", Brushes.White) is { } glyph)
            badge.Children.Add(new System.Windows.Controls.Image
            {
                Source = glyph, Width = 11, Height = 11,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });
        return badge;
    }

    /// <summary><paramref name="topRow"/> = this tile is in the Material grid's first row, which carries a
    /// lighter label shadow (see <see cref="MaterialTiles"/>).</summary>
    private static Border MaterialTile(string tag, string name, bool topRow, Action onClick)
    {
        var label = new TextBlock
        {
            Text = Loc.T(name), FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            Foreground = RadialMenuControl.PreviewIsDark(tag)
                ? new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)) : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
            // Lifts the name off the textured materials; row 1 takes it at 75%.
            Effect = RadialMenuControl.PreviewLabelShadow(tag, topRow ? MaterialTopRowShadowScale : 1.0),
        };
        // The name uses the material's OWN display face where it has one — see
        // RadialMenuControl.ApplyPreviewLabelFace. ⚠ Must come AFTER the initializer: it overrides
        // FontWeight, because none of the bundled faces ship a Bold.
        RadialMenuControl.ApplyPreviewLabelFace(label, tag);
        // The styled materials take their signature treatment from RadialMenuControl.PreviewDecoration
        // (shared with the onboarding tiles) — EXCEPT Salvage, whose shared crop reads as an unreadable
        // smear at tile size, so this file builds it locally (see SalvageTileDecoration).
        UIElement content = label;
        var decoration = tag == "obsidian"
            ? new Border { CornerRadius = new CornerRadius(8), Background = GlossDarkSheen, IsHitTestVisible = false }
            : tag == "salvage"
            ? SalvageTileDecoration(8)
            : RadialMenuControl.PreviewDecoration(tag, cornerRadius: 8);
        // A drop-in theme that brings its own sound files is badged, because picking it also takes over
        // the Sound-effects choice (see RefreshSoundTiles) — the badge is where that consequence is
        // visible at the moment of the decision.
        var soundBadge = Materials.CustomFor(tag)?.Sounds is not null ? OwnSoundsBadge() : null;
        if (decoration is not null || soundBadge is not null)
        {
            var grid = new Grid();
            if (decoration is not null) grid.Children.Add(decoration);
            grid.Children.Add(label);
            if (soundBadge is not null) grid.Children.Add(soundBadge);
            content = grid;
        }
        var t = new InvokableBorder
        {
            Height = 56, CornerRadius = new CornerRadius(8), Margin = new Thickness(4),
            Background = RadialMenuControl.PreviewFill(tag), BorderBrush = TileRest, BorderThickness = new Thickness(1),
            Tag = tag, Cursor = System.Windows.Input.Cursors.Hand, Child = content,
            Invoked = onClick,
        };
        t.MouseLeftButtonUp += (_, _) => onClick();
        System.Windows.Automation.AutomationProperties.SetName(t,
            Loc.F(UiText.Customize.MaterialName, name.Replace("\n", " ")) + (soundBadge is not null ? Loc.T(UiText.Customize.IncludesSounds) : ""));
        t.SetResourceReference(FrameworkElement.FocusVisualStyleProperty, "AccessFocusVisual");
        return t;
    }

    // ── Salvage material tile texture (Customize + Onboarding) ──────────────────
    // Built LOCALLY rather than via RadialMenuControl.PreviewDecoration: the shared crop reads as an
    // unreadable smear at swatch size. Same source photo the wheel uses, loaded independently because
    // RadialMenuControl.SalvageRustBitmap is private.
    //
    // Crop: keep in the 35–50% band — the shared renderer's ~16% zooms in so far the grain loses definition
    // at this size. Lighten: a white wash approximating the armed-slice plate's per-channel gain multiply
    // (a multiply needs pixel access this file doesn't have) — keep it faint or the deep charcoals wash out.
    internal const double SalvageTilePreviewCrop    = 0.40;
    internal const double SalvageTilePreviewLighten = 0.10;
    private static ImageSource? _salvageTileBitmap;
    private static bool _salvageTileTried;

    /// <summary>Internal so <see cref="OnboardingWindow"/>'s Material step, which builds its own tile row,
    /// can share this override instead of re-implementing the crop+lighten math.</summary>
    internal static UIElement SalvageTileDecoration(double cornerRadius)
    {
        var grid = new Grid { IsHitTestVisible = false };
        // Start from the shared decoration (rust texture + the lifted-plate bottom lip) so the lip edge
        // treatment stays in sync with the wheel's own — only the texture's crop/brightness is ours.
        if (RadialMenuControl.PreviewDecoration("salvage", cornerRadius) is Grid shared && shared.Children.Count >= 2)
        {
            var lip = shared.Children[1];   // PremiumTileEdge(...) — keep as-is
            shared.Children.RemoveAt(1);

            if (SalvageTileBitmap() is { } bmp)
            {
                shared.Children.RemoveAt(0);    // drop the shared renderer's texture Border; ours replaces it
                var texture = new ImageBrush(bmp)
                {
                    ViewboxUnits  = BrushMappingMode.RelativeToBoundingBox,
                    Viewbox       = new Rect((1 - SalvageTilePreviewCrop) / 2, (1 - SalvageTilePreviewCrop) / 2,
                                              SalvageTilePreviewCrop, SalvageTilePreviewCrop),
                    Opacity       = 0.85,   // must match RadialMenuControl.SalvageRustOpacity
                    TileMode      = TileMode.None,
                    ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                    Viewport      = new Rect(0, 0, 1, 1),
                    Stretch       = Stretch.UniformToFill,
                };
                texture.Freeze();
                grid.Children.Add(new Border { CornerRadius = new CornerRadius(cornerRadius), Background = texture });
                // The lighten wash, laid over the crop — see the section comment above.
                grid.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(cornerRadius),
                    Background = new SolidColorBrush(Color.FromArgb((byte)(SalvageTilePreviewLighten * 255), 255, 255, 255)),
                });
            }
            else
            {
                // Local bitmap unavailable — keep the SHARED renderer's texture Border rather than shipping
                // a bare charcoal tile. Don't drop it unconditionally: a load failure would then silently
                // erase the texture from both Customize and onboarding.
                var sharedTexture = shared.Children[0];
                shared.Children.RemoveAt(0);
                grid.Children.Add(sharedTexture);
            }
            grid.Children.Add(lip);
        }
        return grid;
    }

    private static ImageSource? SalvageTileBitmap()
    {
        if (_salvageTileTried) return _salvageTileBitmap;
        _salvageTileTried = true;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            // ⚠ FULL pack URI, never UriKind.Relative. On .NET 8 WPF a code-created BitmapImage resolves a
            // relative UriSource against the SITE OF ORIGIN (the exe's folder on disk), NOT
            // pack://application — and these images are compiled-in Resources with no loose copy under bin,
            // so a relative URI throws and the silent catch below ships a bare tile.
            bmp.UriSource = new Uri("pack://application:,,,/Assets/salvage-button-background.jpg");
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            _salvageTileBitmap = bmp;
        }
        catch { _salvageTileBitmap = null; }
        return _salvageTileBitmap;
    }

    private static readonly Color ButtonPuckFill = Color.FromRgb(0x3D, 0x40, 0x48);

    // Shrinks the SELECTED Button-icons tile's glyph+puck unit. In GlyphTilePs.Sym it must be applied to the
    // symbol scale and the puck diameter INDEPENDENTLY, each off its own pre-shrink value, or the puck
    // compounds both through the diameter formula's dependency on scale.
    private const double GlyphShrink = 0.95;

    // Unselected tile glyphs render in this grey (the thickness / sound glyph ink).
    // NOTE: declared BEFORE SoundGlyphInk — static initializers run in declaration order, and
    // MakeGlyphInk reads this field.
    private static readonly Color TileGlyphGrey = Color.FromRgb(0x44, 0x44, 0x48);
    private static readonly Brush SoundGlyphInk = MakeGlyphInk();
    private static Brush MakeGlyphInk() { var b = new SolidColorBrush(TileGlyphGrey); b.Freeze(); return b; }

    // Thickness mini-wheel glyphs: unselected tiles use this grey (the wheel's own ring colour); the
    // SELECTED tile's glyph takes an accent tied to the current Material pick. See RefreshSliceTiles.
    private static readonly Color SliceGlyphGrey         = Color.FromRgb(0x8E, 0x97, 0xA3);
    private static readonly Color SliceGlyphSelectedDark = Color.FromRgb(0x50, 0x54, 0x5A);
    private static readonly Color SliceGlyphSelectedBlue = Color.FromRgb(0x8F, 0xB2, 0xDE);   // any darker/more saturated blue reads heavy next to the light materials
    // Terra's selected glyph matches the darker orange terracotta behind the terra wheel
    // (RadialMenuControl.TerraBlobFill) — the generic light-material blue and the pale wedge cream both read
    // as washed-out accents against Terra's warm tone.
    private static readonly Color SliceGlyphSelectedTerra = Color.FromRgb(201, 106, 59);
    // Kawaii's mini-wheel accent is a pastel purple from its own Dream Sky palette — the generic
    // light-material blue reads as a foreign accent next to the pastel swatch.
    private static readonly Color SliceGlyphSelectedKawaii = RadialMenuControl.KawaiiAccent;   // single source: the wheel owns the hex
    private Color SelectedSliceGlyphColor() => MiniWheelInk(_sliceMaterial, selected: true);

    /// <summary>The ink for a slice-thickness tile's mini-wheel: the grey rest colour, or — when the tile is
    /// the selected thickness — an accent tied to the current material. Shared with the onboarding Look
    /// step, which uses the same "material retints the thickness illustrations" behaviour, so the two
    /// surfaces can't drift on the mapping.</summary>
    internal static Color MiniWheelInk(string? material, bool selected) => !selected ? SliceGlyphGrey : material switch
    {
        "mesa"   => SliceGlyphSelectedTerra,
        "kawaii" => SliceGlyphSelectedKawaii,
        _ when ControllerWheel.Materials.IsDark(material) => SliceGlyphSelectedDark,
        _ => SliceGlyphSelectedBlue,
    };

    /// <summary>Called from BuildTiles, Load, and every thickness / material pick — a material change must
    /// recolour the already-selected tile immediately, same as picking a new thickness.</summary>
    private void RefreshSliceTiles()
    {
        var accent = SelectedSliceGlyphColor();
        foreach (var b in ThicknessTiles.Children.OfType<Border>())
        {
            var tag = (string)b.Tag!;
            if (tag == "thick" && !_thickAllowed) continue;   // collapsed — UpdateThicknessForSliceCount owns its visibility
            double inner = ThicknessOptions.First(o => o.Tag == tag).Inner;
            bool sel = tag == _thickness;
            b.Child = PreviewTile(MiniWheel(inner, ringColor: sel ? accent : SliceGlyphGrey), null);
        }
    }

    // Fields so RefreshSoundTiles can rebuild their content.
    private Border? _materialTile, _digitalTile, _physicalTile, _noneTile;
    private static readonly Color SoundDigital  = Color.FromRgb(0x6B, 0x68, 0xC8);   // purple-blue
    private static readonly Color SoundPhysical = Color.FromRgb(0xA8, 0x60, 0x4A);   // reddish-brown
    private static readonly Color SoundNone     = Color.FromRgb(0x7C, 0x8C, 0xA0);   // greyish blue
    private static readonly Color SoundCustom   = Color.FromRgb(0x3E, 0x8E, 0x5A);   // green — drop-in themes

    /// <summary>Glyph + accent for the Themed tile, keyed on the selected MATERIAL: the glyph previews
    /// which sound set the material resolves to, the accent echoes the material's own look. Returned as
    /// one pair from one switch so the two can never drift apart.
    /// <para>⚠ The accent is inked on a WHITE tile, so these are the material's hue pulled DARK enough to
    /// read there — not the material's actual palette. Kawaii's cotton-candy pink and the flats' light
    /// blue in particular are deepened; the literal pastels vanish against white.</para></summary>
    private (string Glyph, Color Accent) ThemedSoundLook() =>
        Materials.CustomFor(_sliceMaterial) is not null ? ("Palette", SoundCustom)
        : Materials.Normalize(_sliceMaterial) switch
        {
            Materials.Terra      => ("Shovel",        Color.FromRgb(0xC0, 0x5B, 0x38)),   // terracotta
            Materials.Kawaii    => ("Creation",      Color.FromRgb(0xD6, 0x5C, 0x97)),   // cotton-candy pink, deepened
            Materials.Reactor    => ("Molecule",      Color.FromRgb(0x45, 0x45, 0x4A)),   // dark grey
            Materials.Salvage    => ("Biohazard",     Color.FromRgb(0x6A, 0x4A, 0x42)),   // liver-brown
            Materials.GlossLight => ("SpaceInvaders", Color.FromRgb(0x3A, 0x8D, 0xC4)),   // light blue
            Materials.GlossDark  => ("Brightness2",   Color.FromRgb(0x3A, 0x45, 0x50)),   // dark grey, blue tint
            Materials.FlatLight  => ("Gavel",         Color.FromRgb(0x3A, 0x8D, 0xC4)),   // light blue, as Pearl
            _                    => ("Gavel",         Color.FromRgb(0x3A, 0x45, 0x50)),   // Flat Dark: as Obsidian
        };

    /// <summary>The SELECTED tile's glyph renders in its accent colour, the rest in the tile grey. Called
    /// from BuildTiles, Load, and each pick.</summary>
    private void RefreshSoundTiles()
    {
        if (_materialTile is null || _digitalTile is null || _physicalTile is null || _noneTile is null) return;
        var themed = ThemedSoundLook();
        _materialTile.Child = SoundTile(themed.Glyph, Loc.T(UiText.Customize.Themed), _sfx == "material" ? themed.Accent : null);
        _digitalTile.Child  = SoundTile("SpaceInvaders", Loc.T(UiText.Customize.Digital),  _sfx == "digital"  ? SoundDigital  : null);
        _physicalTile.Child = SoundTile("Gavel",         Loc.T(UiText.Customize.Physical), _sfx == "physical" ? SoundPhysical : null);
        _noneTile.Child     = SoundTile("VolumeOff",     Loc.T(UiText.Customize.Silent),   _sfx == "none"     ? SoundNone     : null);

        // A drop-in theme carrying its own sounds owns the sound choice: the two override tiles go
        // inactive, and Themed (the theme's own set — what plays for anything it doesn't override)
        // and "Silent" stay live so the app can still follow the theme or be muted.
        bool locked = Materials.CustomFor(_sliceMaterial)?.Sounds is not null;
        foreach (var tile in new[] { _digitalTile, _physicalTile })
        {
            tile.IsEnabled = !locked;
            tile.Opacity   = locked ? 0.45 : 1.0;
            tile.Cursor    = locked ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand;
        }
    }

    /// <summary>The option label over a Material Design glyph (MahApps IconPacks). <paramref name="accent"/>
    /// tints the glyph on the selected tile, else the thickness-tile grey. Falls back to label-only if the
    /// icon name won't resolve.</summary>
    private static UIElement SoundTile(string iconName, string? label, Color? accent = null)
    {
        var glyphInk = accent is { } c ? new SolidColorBrush(c) : SoundGlyphInk;
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(label))
            sp.Children.Add(FitLabel(new TextBlock
            {
                Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x38)),
            }, new Thickness(0, 0, 0, 3)));
        if (PackIconHelper.FromName(iconName, glyphInk) is { } glyph)
            sp.Children.Add(new System.Windows.Controls.Image
            {
                Source = glyph, Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Center,
            });
        return sp;
    }

    private static UIElement PreviewTile(UIElement preview, string? label)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(preview);
        if (!string.IsNullOrEmpty(label))
            sp.Children.Add(FitLabel(new TextBlock
            {
                Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x48)),
            }, new Thickness(0, 3, 0, 0)));
        return sp;
    }

    /// <summary>A tile caption that shrinks rather than spilling past its tile. These captions sit on a
    /// FIXED-height tile with a glyph under them, so wrapping to a second line would clip vertically
    /// instead of horizontally — the scale is the only free dimension. DownOnly, so a caption that already
    /// fits is drawn at its authored size and English is untouched; a longer translation (German's
    /// "Zum Material passend" against "Themed") shrinks to the tile's width.</summary>
    private static UIElement FitLabel(TextBlock label, Thickness margin = default)
    {
        // ⚠ The Viewbox must be CENTRED but CAPPED, and neither alignment alone gives both: centred, it is
        // arranged at its child's desired size and is never handed a narrower slot to shrink into (the
        // caption spills exactly as before); stretched, it shrinks correctly but arranges the child at the
        // slot's origin, so every caption that already fits jumps to the left. Capping MaxWidth at the
        // host's own width does it — wider than the cell, the Viewbox scales down to the cap; narrower, it
        // keeps its authored size and centres.
        var host = new Grid
            { Margin = new Thickness(5 + margin.Left, margin.Top, 5 + margin.Right, margin.Bottom) };
        var box = new Viewbox
        {
            Child = label,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        box.SetBinding(FrameworkElement.MaxWidthProperty,
                       new System.Windows.Data.Binding(nameof(FrameworkElement.ActualWidth)) { Source = host });
        host.Children.Add(box);
        return host;
    }

    private static UIElement MiniWheel(double inner, int slices = 6, Color? ringColor = null)
    {
        const double c = 24, outer = 20;
        var canvas = new Canvas { Width = 48, Height = 48 };
        var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
        ring.Children.Add(new EllipseGeometry(new Point(c, c), outer, outer));
        ring.Children.Add(new EllipseGeometry(new Point(c, c), inner, inner));
        ring.Freeze();
        canvas.Children.Add(new System.Windows.Shapes.Path { Data = ring, Fill = new SolidColorBrush(ringColor ?? SliceGlyphGrey) });
        for (int i = 0; i < slices; i++)
        {
            // Seams offset by half a slice so a SLICE (not a seam) is centred on 12 o'clock —
            // matching how the real wheel orients.
            double step = 360.0 / slices;
            double ang = (i * step - 90 + step / 2) * Math.PI / 180;
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = c + (inner - 1.5) * Math.Cos(ang), Y1 = c + (inner - 1.5) * Math.Sin(ang),
                X2 = c + (outer + 1.0) * Math.Cos(ang), Y2 = c + (outer + 1.0) * Math.Sin(ang),
                Stroke = Brushes.White, StrokeThickness = 2.0,
            });
        }
        return canvas;
    }

    /// <summary>Xbox-letters tile content: the name over a row of face-button LETTERS.</summary>
    private static UIElement GlyphTile(string name, (string Glyph, Color Color)[] glyphs, bool colored)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(FitLabel(new TextBlock
        {
            Text = Loc.T(name), FontSize = 13, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x38)),
        }));
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0), Height = 22 };
        foreach (var (glyph, color) in glyphs)
        {
            var text = new TextBlock
            {
                Text = glyph, FontSize = 15 * GlyphShrink, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(colored ? color : TileGlyphGrey), VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            row.Children.Add(colored ? ButtonPuck(text, puckNudge: LetterPuckNudge) : Wrap(text));
        }
        sp.Children.Add(row);
        return sp;
    }

    // A capital's ink sits high in its text box — the box reserves descender space no capital uses — so a
    // puck centred on that box reads high of the letter. This shifts the disc down onto the caps instead.
    // Render-only, so row layout and spacing are untouched. Letters only: the PlayStation symbols are
    // vector shapes already centred in their own box.
    private const double LetterPuckNudge = 1.0;

    /// <summary>A face-button glyph over a dark-grey "puck" — SELECTED Button-icons tile only. Diameter is
    /// per-glyph-family (see call sites): a snug pad a few px larger than the glyph's own ink, not a coin.
    /// Default 17.1 = 18 × <see cref="GlyphShrink"/>.
    /// <paramref name="puckNudge"/> lowers the disc under a glyph whose ink is not box-centred.</summary>
    private static UIElement ButtonPuck(UIElement glyph, double diameter = 17.1, double puckNudge = 0)
    {
        var grid = new Grid { Width = diameter, Height = diameter, Margin = new Thickness(1, 0, 1, 0) };
        grid.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = diameter, Height = diameter, Fill = new SolidColorBrush(ButtonPuckFill),
            RenderTransform = new TranslateTransform(0, puckNudge),
        });
        grid.Children.Add(glyph);
        return grid;
    }

    // Unselected row-item spacing: wider than ButtonPuck's, because bare letters with no puck behind them
    // read as crowded at the same margin.
    private static UIElement Wrap(UIElement glyph)
    {
        var grid = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        grid.Children.Add(glyph);
        return grid;
    }

    /// <summary>PlayStation-shapes tile content: the name over the four face symbols drawn as vector
    /// shapes (△ ○ ✕ □) — one shared box + thick stroke so they align. Coloured when the tile is selected,
    /// tile-glyph grey otherwise.</summary>
    private static UIElement GlyphTilePs(string name, bool colored)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x38)),
        });
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0), Height = 22 };
        UIElement Sym(string kind, Color color, double scale)
        {
            var symbol = PsSymbol(kind, colored ? color : TileGlyphGrey, scale * GlyphShrink);
            // Puck diameter tracks the symbol's own scaled ink (box(18) * scale) plus a snug margin — the
            // square (scale .9) reads visually larger than △○✕ (scale .765), so its puck follows suit rather
            // than sharing one fixed coin size.
            return colored ? ButtonPuck(symbol, diameter: (18 * scale + 5) * GlyphShrink) : symbol;
        }
        row.Children.Add(Sym("triangle", PsGreen,  0.765));
        row.Children.Add(Sym("circle",   PsRed,    0.765));
        row.Children.Add(Sym("cross",    PsBlue,   0.765));
        row.Children.Add(Sym("square",   PsPurple, 0.9));
        sp.Children.Add(row);
        return sp;
    }

    /// <summary>One PlayStation face symbol as an outline shape, centred in a fixed box so the four line up
    /// exactly. No fill — matches the controller's own outline prompts.
    /// <paramref name="scale"/> shrinks the symbol within its box (centred, so alignment is unaffected).</summary>
    private static UIElement PsSymbol(string kind, Color color, double scale)
    {
        const double box = 18, t = 2.5;
        var brush = new SolidColorBrush(color);
        System.Windows.Shapes.Shape shape = kind switch
        {
            "triangle" => new System.Windows.Shapes.Polygon
            {
                Points = new PointCollection { new Point(9, 1.5), new Point(17, 16.5), new Point(1, 16.5) },
                Stroke = brush, StrokeThickness = t, StrokeLineJoin = PenLineJoin.Round,
            },
            "circle" => new System.Windows.Shapes.Ellipse
            {
                Width = 16, Height = 16, Margin = new Thickness(1), Stroke = brush, StrokeThickness = t,
            },
            "square" => new System.Windows.Shapes.Rectangle
            {
                Width = 14, Height = 14, RadiusX = 1.5, RadiusY = 1.5, Margin = new Thickness(2), Stroke = brush, StrokeThickness = t,
            },
            _ /* cross */ => new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 2,2 L 16,16 M 16,2 L 2,16"),
                Stroke = brush, StrokeThickness = t, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            },
        };
        if (scale != 1.0)
        {
            shape.RenderTransform = new ScaleTransform(scale, scale);
            shape.RenderTransformOrigin = new Point(0.5, 0.5);   // scale about the centre → stays aligned in the box
        }
        return new Border
        {
            // Negative side-margins pull the four glyphs closer (center-to-center 17.1px, not 19).
            Width = box, Height = box, Margin = new Thickness(-0.45, 0, -0.45, 0),
            Child = shape, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <param name="liftLabel">Material tiles only: the selected tile's label rides 2px north. The other
    /// tile grids pair their text with a glyph, which the nudge would knock out of alignment.</param>
    /// <summary>Highlight <paramref name="tag"/> across every grid a group is split over — the Material
    /// group spans two (Simple + Deluxe), so "deselect all the others" has to reach into both or the old
    /// pick keeps its ring when the new one is in the other box.</summary>
    private static void SelectTile(string tag, bool liftLabel, params UniformGrid[] grids)
    {
        foreach (var g in grids) SelectTile(g, tag, liftLabel);
    }

    private static void SelectTile(UniformGrid grid, string tag, bool liftLabel = false)
    {
        foreach (var b in grid.Children.OfType<Border>())
        {
            bool sel = (string?)b.Tag == tag;
            b.BorderBrush = sel ? TileSel : TileRest;
            b.BorderThickness = new Thickness(sel ? 3 : 1);
            if (liftLabel) RadialMenuControl.ApplyPreviewLabelLift(b, sel);
            // The thick ring is the only visual selection state — mirror it for screen readers.
            System.Windows.Automation.AutomationProperties.SetItemStatus(b, sel ? "selected" : "");
        }
    }

    private static string NormalizeMaterial(string? m)
    {
        if (m is not null && ControllerWheel.Materials.IsValid(m)) return m;
        return (m ?? "pearl") switch
        {
            "flat" or "flat-white" or "flat-light" => "flat-light",
            "flat-dark" => "flat-dark",
            "obsidian" or "gloss-dark" or "gloss-black-a" or "gloss-black-b" or "gloss-black-c" => "obsidian",
            "frost-light" or "frost-dark" => "kawaii",
            "sparkle" => "kawaii",
            "stencil" => "salvage",
            "mesa" or "terra" or "paper" => "mesa",
            _ => "pearl",                                 // also catches the legacy "gloss-light"
        };
    }

    // ── Trigger chord-builder ───────────────────────────────────────────────────
    private readonly List<string> _legacyTokens = new();   // loaded tokens with no builder row (kept on save)

    /// <summary>Build the chord-builder rows from the stored tokens for the detected controller kind, plus
    /// the activation checkbox. Each token becomes a (primary, modifier) row; a token with no builder form
    /// (e.g. the legacy View/Menu + L3/R3, still honoured at runtime) gets no row but is REMEMBERED and
    /// re-included on save — a Settings save must never silently drop a working gesture. Seeds a single
    /// default row if nothing maps (alongside any legacy tokens, which keep working).</summary>
    private void PopulateTriggerModes(SystemConfig cfg)
    {
        TriggerRows.Children.Clear();
        _legacyTokens.Clear();
        var primaries = ControllerWheel.TriggerModes.PrimariesFor(_kind);
        foreach (var token in cfg.TriggerModesFor(_kind))
            if (ControllerWheel.TriggerModes.TryDecompose(token, out var p, out var m) && primaries.Contains(p))
                AddTriggerRow(p, m);
            else
                _legacyTokens.Add(token);
        if (TriggerRows.Children.Count == 0)
        {
            var (p, m) = ControllerWheel.TriggerModes.DefaultCombo(_kind);
            AddTriggerRow(p, m);
        }
        UpdateTriggerRemoveButtons();

        // The chord builder stays here; "Wheels toggle on/off" and the rest of Accessibility live on
        // Settings ▸ Advanced.
    }

    /// <summary>Append a builder row: a [button] dropdown + a [combined-with] dropdown (its options depend on
    /// the button) + a ✕ to remove it. The modifier dropdown locks when the button has one pairing (Fn,
    /// Touchpad); choosing a new button re-fills the modifier dropdown to that button's options.</summary>
    private void AddTriggerRow(TriggerPrimary primary, TriggerModifier modifier)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Left };
        // Widths sized for the longest labels ("Touchpad" / "Select/Start") PLUS the themed template's
        // 28px right padding that keeps text clear of the drop arrow. Fixed, not Auto: the whole row has
        // to stay inside the right column, so the boxes fill these columns rather than setting them.
        var modCol = new ColumnDefinition { Width = new GridLength(108) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // plus-circle joiner
        row.ColumnDefinitions.Add(modCol);
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The shared ComboBox style floors every Settings drop-down at MinWidth 120 and left-aligns it;
        // these two live in fixed columns instead, so they opt out and fill the column they're given.
        var primaryBox = new ComboBox { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var p in ControllerWheel.TriggerModes.PrimariesFor(_kind))
            primaryBox.Items.Add(new ComboBoxItem { Content = ControllerWheel.TriggerModes.PrimaryLabel(p, _kind), Tag = p });
        var plus = new System.Windows.Controls.Image
        {
            Source = PackIconHelper.FromName("PlusCircle", TriggerPlusInk),
            Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3.5, 0, 3.5, 0),
        };
        var modBox = new ComboBox { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var removeBtn = new Button { Content = "✕", MinWidth = 28, Padding = new Thickness(6, 0, 6, 0), ToolTip = Loc.T(UiText.Customize.RemoveTrigger) };
        // A glyph-only button announces as "✕" to a screen reader; give it a real name. No row index is
        // baked in: rows are removed individually (no rebuild), so an index would go stale on first delete.
        System.Windows.Automation.AutomationProperties.SetName(removeBtn, Loc.T(UiText.Customize.RemoveTriggerName));

        // Fn / Touchpad have no real second half — the modifier dropdown stays visible but DISABLED,
        // showing a placeholder ("None" for Fn, "Edge Swipe" for Touchpad) so every row's ✕ button stays
        // vertically aligned (modCol never collapses). The plus joiner uses Hidden (not Collapsed) for the
        // same reason — it reserves its layout space even when not shown. Any primary with >1 modifier
        // option keeps the pair, fully interactive.
        void UpdateModVisibility()
        {
            var p = (TriggerPrimary)((ComboBoxItem)primaryBox.SelectedItem).Tag!;
            bool hasMod = ControllerWheel.TriggerModes.ModifiersFor(p, _kind).Count() > 1;
            plus.Visibility   = hasMod ? Visibility.Visible : Visibility.Hidden;
            modBox.Visibility = Visibility.Visible;
        }

        void FillModifiers(TriggerModifier want)
        {
            bool prev = _loading; _loading = true;
            modBox.Items.Clear();
            var p = (TriggerPrimary)((ComboBoxItem)primaryBox.SelectedItem).Tag!;
            foreach (var m in ControllerWheel.TriggerModes.ModifiersFor(p, _kind))
            {
                // The primary decides the wording for a few pairings ("(none)" instead of a dash, the
                // opposite-hand shoulders) — the Tag (used by TriggerModesForSave to compose the saved
                // token) is untouched.
                modBox.Items.Add(new ComboBoxItem
                { Content = ControllerWheel.TriggerModes.ModifierLabel(m, p), Tag = m });
            }
            modBox.SelectedItem = modBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (TriggerModifier)i.Tag! == want)
                                  ?? modBox.Items[0];
            modBox.IsEnabled = modBox.Items.Count > 1;   // single pairing (Fn/Touchpad) → locked
            UpdateModVisibility();
            _loading = prev;
        }

        primaryBox.SelectionChanged += (_, __) => { if (_loading) return; FillModifiers(TriggerModifier.None); OnTriggerRowsChanged(); };
        modBox.SelectionChanged     += (_, __) => { if (!_loading) OnTriggerRowsChanged(); };
        removeBtn.Click += (_, __) => { TriggerRows.Children.Remove(row); UpdateTriggerRemoveButtons(); OnTriggerRowsChanged(); };

        Grid.SetColumn(primaryBox, 0);
        Grid.SetColumn(plus, 1);
        Grid.SetColumn(modBox, 2);
        Grid.SetColumn(removeBtn, 4);
        row.Children.Add(primaryBox);
        row.Children.Add(plus);
        row.Children.Add(modBox);
        row.Children.Add(removeBtn);
        TriggerRows.Children.Add(row);

        bool loadingWas = _loading; _loading = true;
        primaryBox.SelectedItem = primaryBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (TriggerPrimary)i.Tag! == primary)
                                  ?? primaryBox.Items[0];
        FillModifiers(modifier);
        _loading = loadingWas;
    }

    private void AddTriggerRow_Click(object sender, RoutedEventArgs e)
    {
        if (TriggerRows.Children.OfType<Grid>().Count() >= MaxTriggerRows) return;
        var (p, m) = ControllerWheel.TriggerModes.DefaultCombo(_kind);
        AddTriggerRow(p, m);
        UpdateTriggerRemoveButtons();
        OnTriggerRowsChanged();
    }

    /// <summary>Hard cap on builder rows. More than a few live summon chords is a recipe for accidental
    /// invokes, and every one of them costs the game a button combination.</summary>
    private const int MaxTriggerRows = 3;

    /// <summary>Keep at least one row: disable every ✕ when only one remains (the wheels must stay reachable).
    /// Also gates "+ Add Another Trigger" at <see cref="MaxTriggerRows"/>. A config that already holds more
    /// rows than the cap is left intact — the button just stays off until the user removes some.</summary>
    private void UpdateTriggerRemoveButtons()
    {
        var rows = TriggerRows.Children.OfType<Grid>().ToList();
        foreach (var row in rows)
            if (row.Children.OfType<Button>().FirstOrDefault() is { } btn) btn.IsEnabled = rows.Count > 1;
        AddTriggerBtn.IsEnabled = rows.Count < MaxTriggerRows;
    }

    private void OnTriggerRowsChanged() { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    /// <summary>The per-kind trigger map to persist: other controller kinds' saved choices preserved, the
    /// detected kind's entry rebuilt from the builder rows (each row's (primary, modifier) composed to a
    /// token, in order, de-duplicated), plus any loaded LEGACY tokens the builder can't represent (they
    /// keep working at runtime; dropping them here would silently kill a configured gesture on save).
    /// Falls back to the kind's default if nothing else survives.</summary>
    private Dictionary<string, List<string>> TriggerModesForSave()
    {
        var map = new Dictionary<string, List<string>>(_triggerModes);
        var chosen = new List<string>();
        foreach (var row in TriggerRows.Children.OfType<Grid>())
        {
            var combos = row.Children.OfType<ComboBox>().ToList();
            if (combos.Count < 2 || combos[0].SelectedItem is not ComboBoxItem pi || combos[1].SelectedItem is not ComboBoxItem mi) continue;
            var token = ControllerWheel.TriggerModes.Compose((TriggerPrimary)pi.Tag!, (TriggerModifier)mi.Tag!);
            if (token != null && !chosen.Contains(token)) chosen.Add(token);
        }
        foreach (var t in _legacyTokens)
            if (!chosen.Contains(t)) chosen.Add(t);
        map[_kind.ToString()] = chosen.Count > 0 ? chosen : [ControllerWheel.TriggerModes.DefaultFor(_kind)];
        return map;
    }

    // ── D-Pad + "Show labels on" ──────────────────────────────────────────────────────────────────
    // The Toggle ⇄ Swap / Both-sticks soft-link logic lives in SystemEditorControl, beside the
    // Accessibility checkboxes it links.

    /// <summary>Read-only "Now mixing" line: the app pair the mixer would balance right now. Resolved
    /// off the UI thread (it enumerates audio sessions + the game library).</summary>
    private async void RefreshMixPairReadout()
    {
        // Only meaningful in "mixer" mode — a stale "Now mixing: …" line under e.g. Microphone Volume just
        // reads as a bug. "mixer" isn't selectable (see the picker XAML), so this stays collapsed; kept
        // wired for when the mixer is fixed and the option comes back.
        bool mixer = string.Equals((DpadModeBox.SelectedItem as ComboBoxItem)?.Tag as string, "mixer",
                                   StringComparison.OrdinalIgnoreCase);
        MixPairText.Visibility = mixer ? Visibility.Visible : Visibility.Collapsed;
        if (!mixer) return;
        try
        {
            var pair = await System.Threading.Tasks.Task.Run(AppVolumeMixer.ResolvePair);
            MixPairText.Text = pair is null
                ? Loc.T(UiText.Customize.NowMixingNothing)
                : Loc.F(UiText.Customize.NowMixingPair, pair.LeftLabel, pair.RightLabel);
        }
        catch { MixPairText.Text = Loc.T(UiText.Customize.NowMixingUnavailable); }
    }

    private void DpadModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // _loading guard: Load()'s own selection set raises this too, and Load already calls
        // RefreshMixPairReadout once — an unguarded second call meant two concurrent ResolvePair
        // enumerations racing to write MixPairText on every Settings open.
        if (!_loading) { RefreshMixPairReadout(); Fire(); }
    }

    /// <summary>Select the ComboBoxItem whose Tag matches <paramref name="mode"/> (already normalized by
    /// the caller via <see cref="SliceLabelRule.Normalize"/>); falls back to index 0 if nothing matches.</summary>
    private void SelectShowSliceLabels(string mode)
    {
        ShowSliceLabelsBox.SelectedItem =
            ShowSliceLabelsBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == mode)
            ?? ShowSliceLabelsBox.Items.OfType<ComboBoxItem>().First();
    }

    private void ShowSliceLabelsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Fire();
    }
}
