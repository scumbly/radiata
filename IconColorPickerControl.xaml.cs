using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfColor = System.Windows.Media.Color;

namespace ControllerWheel;

/// <summary>Combined per-slice / per-default icon + tint editor: pick the glyph AND the color for one
/// action, with an editable hex field, a live preview, the shared swatch palette (paired chips: a dark base
/// over its light equivalent, which dark slice materials use), and Reset to Default. Confirmation is the
/// HOST's job — the dialog wraps it with OK/Cancel; the slice editor hosts it inline and commits via Save
/// Slice.</summary>
public partial class IconColorPickerControl : UserControl
{
    /// <summary>The AUTHORED colour — material-neutral, exactly what the host writes to config: a palette
    /// swatch's BASE tone or the hex in the box. NEVER a material variant, whatever material the well is
    /// currently previewing, or the stored colour would depend on the editor's material.</summary>
    public WpfColor SelectedColor { get; private set; }
    public string?  SelectedIcon  { get; private set; }

    /// <summary>The user changed the color or icon (host: live-apply + recompute dirty).</summary>
    public event EventHandler? Changed;
    /// <summary>The user clicked Fetch Logo (only visible when the host opts in via Configure).</summary>
    public event EventHandler? FetchLogoClicked;
    /// <summary>The user clicked Reset to Default (host also resets the slice label to its default).</summary>
    public event EventHandler? ResetRequested;
    /// <summary>The user stepped the ◀ ▶ logo cycle; the argument is the newly chosen logo's file path.</summary>
    public event EventHandler<string>? LogoCycled;

    private WpfColor _defColor;
    private string?  _defIcon;
    private bool _sync;   // suppress Changed + hex echo while setting values programmatically

    private static readonly Brush IconWhite = MakeWhite();
    private static Brush MakeWhite() { var b = new SolidColorBrush(Colors.White); b.Freeze(); return b; }

    // Icon-well border: white-ish for light slice materials, a dark slate outline for dark ones — kept
    // separate from the fill, which mirrors the Customize material tile swatch (see SetMaterial).
    private static readonly Brush WellBorderLight = MakeFrozen((WpfColor)System.Windows.Application.Current.Resources["UiChipBorderColor"]);
    private static readonly Brush WellBorderDark  = MakeFrozen(WpfColor.FromRgb(0x4A, 0x50, 0x60));
    private static Brush MakeFrozen(WpfColor c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Paint the icon-preview well to match the wheel's CURRENT slice material — same fill as the
    /// Customize ▸ Material tile swatch (<see cref="RadialMenuControl.PreviewFill"/>), so the glyph preview
    /// sits on the exact tone it'll actually render against (Pearl vs. Kawaii vs. Reactor, etc.,
    /// not just a light/dark binary). It also re-derives the glyph's PREVIEW colour for the new material.
    /// It must never touch the AUTHORED <see cref="SelectedColor"/>: the stored colour is material-neutral
    /// and only the display transform depends on the material.</summary>
    public void SetMaterial(string? material)
    {
        _material = material;
        _dark = RadialMenuControl.PreviewIsDark(material);
        ApplyWellFill();
        IconWell.BorderBrush = _dark ? WellBorderDark : WellBorderLight;
        UpdatePreview();
    }

    /// <summary>Tell the well WHICH slice it's previewing (position in the wheel + how many slices there
    /// are). Kawaii's hue walks the ring, so its well shows that slice's OWN colour rather than one
    /// stand-in pink; every other material ignores this. Pass index &lt; 0 when nothing is selected.</summary>
    public void SetSlicePosition(int index, int count)
    {
        if (index == _sliceIndex && count == _sliceCount) return;
        _sliceIndex = index; _sliceCount = count;
        ApplyWellFill();
    }

    private void ApplyWellFill() =>
        IconWell.Background = RadialMenuControl.PreviewFillForSlice(_material, _sliceIndex, _sliceCount);

    private string? _material;
    private int _sliceIndex = -1, _sliceCount;
    private bool _dark;

    // Don't add per-material icon constants here: local copies go stale the moment the wheel's treatment
    // changes and the well draws something the wheel doesn't. The material→treatment mapping lives once in
    // RadialMenuControl.RenderPreviewIcon / RenderPreviewLogo, which this calls.

    /// <summary>True when <see cref="SelectedColor"/> was TYPED into the hex box — exact intent, so the wheel
    /// paints it verbatim on every material. False after a swatch click or Reset, which are base tones that
    /// take the material variant. The host persists this as <c>WheelSlice.IconColorExact</c>. Don't infer it
    /// from "is there a light half?" — that is material-entangled and disagrees with this flag as soon as a
    /// swatch pick is followed by a hex nudge.</summary>
    public bool IsExactColor { get; private set; }
    private readonly ImageSource? _refreshIcon = PackIconHelper.FromName("Refresh", IconWhite);
    private readonly ImageSource? _checkIcon   = PackIconHelper.FromName("Check", IconWhite);
    // Material vectors, not "◀"/"▶" text: at 12px on a 16px strip a font glyph's ascent/descent padding
    // makes it sit off-centre and read at a different weight than the reload icon beside it.
    private readonly ImageSource? _prevIcon    = PackIconHelper.FromName("ChevronLeft", IconWhite);
    private readonly ImageSource? _nextIcon    = PackIconHelper.FromName("ChevronRight", IconWhite);

    public IconColorPickerControl()
    {
        InitializeComponent();
        BuildPalette();
        FetchGlyph.Source = _refreshIcon;
        PrevGlyph.Source  = _prevIcon;
        NextGlyph.Source  = _nextIcon;
        // Keyboard/UIA path for the well — the SAME action the MouseLeftButtonUp handler runs.
        IconWell.Invoked = OpenIconGrid;
    }

    // Shows a generic "Custom Logo" placeholder instead of a glyph when the real logo can't be drawn.
    private bool _logoPlaceholder;

    // The app's own icon, for a launch slice that wears it on the wheel instead of a glyph. Drawn full
    // colour through the ICON path (not the logo path): the wheel doesn't tint an extracted app icon.
    private ImageSource? _appIcon;

    /// <summary>(Re)initialize the editor for a slice/default. Never raises <see cref="Changed"/>.
    /// <para><paramref name="color"/> and <paramref name="defColor"/> are both AUTHORED, material-neutral
    /// values — the well applies the material variant itself for display (see <see cref="UpdatePreview"/>).
    /// <paramref name="exact"/> seeds <see cref="IsExactColor"/>: pass what the slice stored, or re-opening a
    /// slice silently changes whether its colour is treated as exact.</para></summary>
    public void Configure(WpfColor color, string? icon, WpfColor defColor, string? defIcon,
                          bool showFetchLogo, bool logoPlaceholder = false, bool exact = false,
                          ImageSource? appIcon = null)
    {
        _sync = true;
        _defColor = defColor;
        _defIcon  = defIcon;
        SelectedIcon = icon;
        IsExactColor = exact;
        _logoPlaceholder = logoPlaceholder;
        _appIcon = appIcon;
        // Clear any previous slice's logo + cycle — the host calls SetLogo right after Configure when this
        // slice has one. Without this, switching to a glyph slice keeps showing the old logo.
        _logoImage = null; _logoSet = []; _logoIndex = -1;
        LogoPrevBtn.Visibility = LogoNextBtn.Visibility = Visibility.Collapsed;
        FetchLogoBtn.Visibility = showFetchLogo ? Visibility.Visible : Visibility.Collapsed;
        UpdateStripVisibility();
        SetColor(color, updateHex: true);
        UpdateIconUi();
        _sync = false;
    }

    // ── Logo preview + the ◀ ▶ cycle ────────────────────────────────────────────────────────────────
    // The well shows the REAL logo, tinted exactly as the wheel paints it — a placeholder would make the
    // cycle unusable, because pressing ▶ would change nothing on screen. The stripe is the "this is a logo,
    // not a glyph" cue.

    private ImageSource? _logoImage;                        // the current logo, already loaded (null = glyph mode)
    private IReadOnlyList<string> _logoSet = [];             // every downloaded logo for this game, stable order
    private int _logoIndex = -1;                             // which of _logoSet is current (-1 = not in the set)

    /// <summary>Show a real game logo instead of a glyph, and offer the ◀ ▶ cycle over <paramref name="set"/>
    /// (the logos already on disk for this game). Null image / empty set = glyph mode. Must never raise
    /// <see cref="LogoCycled"/> — this direction is host→picker only.</summary>
    public void SetLogo(ImageSource? logo, IReadOnlyList<string>? set = null, int index = -1)
    {
        _logoImage = logo;
        _logoSet   = set ?? [];
        _logoIndex = index;
        // A cycle needs two or more DISTINCT logos plus a known current position to step from.
        bool canCycle = _logoImage is not null && _logoSet.Count > 1 && _logoIndex >= 0;
        LogoPrevBtn.Visibility = LogoNextBtn.Visibility =
            canCycle ? Visibility.Visible : Visibility.Collapsed;
        UpdateStripVisibility();
        UpdatePreview();
    }

    /// <summary>The bottom strip is only drawn when it has something on it — otherwise an empty coloured bar
    /// sits across every icon well in the app, including the Colour Defaults dialog, which has neither
    /// control.</summary>
    private void UpdateStripVisibility() =>
        LogoToolStrip.Visibility =
            LogoPrevBtn.Visibility == Visibility.Visible || FetchLogoBtn.Visibility == Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;

    // ── Drop your own image on the well ─────────────────────────────────────────────────────────────
    // Off by default: the per-action-type Color Defaults dialog hosts this same control, and a TYPE has no
    // logo to set. The slice editor opts in.

    /// <summary>Host opt-in: accept an image dropped on the well (raises <see cref="LogoFileDropped"/>).</summary>
    public bool AllowLogoDrop { get; set; }

    /// <summary>An image file was dropped on the well; the argument is its full path. The HOST imports it
    /// (so the "couldn't read that" / "no transparency" messages belong to the window, not this control).</summary>
    public event EventHandler<string>? LogoFileDropped;

    // Extensions WPF's imaging stack can decode. The drop is validated for real at import time — this is
    // just the cheap filter that decides whether the cursor shows "copy" while hovering.
    private static readonly string[] DroppableImageExts =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico"];

    private static string? DroppedImage(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths) return null;
        // First image wins — a multi-file drop on a single-image target has no sensible "all of them".
        return paths.FirstOrDefault(p => DroppableImageExts.Contains(
            System.IO.Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));
    }

    private void Well_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        bool ok = AllowLogoDrop && DroppedImage(e) is not null;
        e.Effects = ok ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        // Always handled: the well is its own drop surface and must not let a stray drop bubble to the
        // slice list's .exe/.lnk handler behind it.
        e.Handled = true;
        if (ok) IconWell.BorderBrush = DropTargetBrush;
    }

    private void Well_DragLeave(object sender, System.Windows.DragEventArgs e) => RestoreWellBorder();

    private void Well_Drop(object sender, System.Windows.DragEventArgs e)
    {
        e.Handled = true;
        RestoreWellBorder();
        if (!AllowLogoDrop) return;
        if (DroppedImage(e) is { } path) LogoFileDropped?.Invoke(this, path);
    }

    private void RestoreWellBorder() => IconWell.BorderBrush = _dark ? WellBorderDark : WellBorderLight;

    // Bright, unmissable drop cue — a muted tone here reads as "nothing happening".
    private static readonly Brush DropTargetBrush = MakeFrozen(WpfColor.FromRgb(0x2E, 0xE6, 0x6E));

    private void LogoPrev_Click(object sender, RoutedEventArgs e) => StepLogo(-1);
    private void LogoNext_Click(object sender, RoutedEventArgs e) => StepLogo(+1);

    /// <summary>Wrap around the downloaded set — with only a handful of logos, a dead-ended arrow is worse
    /// than a wrap (you'd have to know which end you were on).</summary>
    private void StepLogo(int delta)
    {
        if (_logoSet.Count < 2 || _logoIndex < 0) return;
        int next = ((_logoIndex + delta) % _logoSet.Count + _logoSet.Count) % _logoSet.Count;
        if (next == _logoIndex) return;
        _logoIndex = next;
        LogoCycled?.Invoke(this, _logoSet[next]);   // host loads it + re-Configures us, which repaints the well
    }

    private void RaiseChanged() { if (!_sync) Changed?.Invoke(this, EventArgs.Empty); }

    private void SetColor(WpfColor c, bool updateHex)
    {
        bool prev = _sync; _sync = true;
        SelectedColor = c;
        if (updateHex) HexBox.Text = $"{c.R:X2}{c.G:X2}{c.B:X2}";   // the '#' lives outside the field
        _sync = prev;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        // The PREVIEW brush is NOT SelectedColor raw — two corrections keep it matching the wheel:
        //  (1) alpha at ActionTint.Alpha; a fully-opaque preview reads harder than the real slice.
        //  (2) the MATERIAL VARIANT via the one table the wheel uses (ActionTint.MaterialVariant), skipped
        //      for an exact (typed) colour, which paints verbatim everywhere.
        // A colour equal to _defColor is stored as null (inherit) and renders as a BUILT-IN default, which
        // cannot be exact — hence the `inherits` guard.
        // ⚠ SelectedColor itself must stay the AUTHORED value: the host writes it to config, so a display
        // transform baked into it would re-apply itself on every save.
        bool inherits = SelectedColor == _defColor;
        var previewRgb = ActionTint.MaterialVariant(SelectedColor, ActionTint.TintSetFor(_material),
                                                   exact: !inherits && IsExactColor);
        var brush = new SolidColorBrush(WpfColor.FromArgb(ActionTint.Alpha,
                                                         previewRgb.R, previewRgb.G, previewRgb.B));
        // The stripe is permanent; only its wording changes.
        LogoStripeText.Text = Loc.T(_logoPlaceholder ? UiText.IconPicker.CustomLogo : UiText.IconPicker.Edit);

        // A logo slice paints the ACTUAL logo, aspect-fitted and tinted the way the wheel does, including the
        // material's icon treatment. Only when the file couldn't be loaded does it fall back to the generic
        // "ImageFrame" placeholder, so a broken/missing logo still reads as "logo, not glyph".
        if (_logoImage is not null)
        {
            // SelectedColor (not previewRgb) is the rim base: Mesa derives its rim from the AUTHORED colour,
            // so feeding it the already-transformed preview tone would flatten every rim onto terra's
            // lightness floor — see ActionTint.MesaRimColor.
            PreviewIcon.Source = RadialMenuControl.RenderPreviewLogo(_logoImage, 67, brush, _material, SelectedColor);
            return;
        }

        var glyph = _logoPlaceholder ? "ImageFrame" : SelectedIcon;
        PreviewIcon.Source = _appIcon
                             ?? (glyph is not null ? PackIconHelper.FromName(glyph, brush) : null);

        // Per-material icon treatment, baked through the wheel's OWN drawing helpers — the
        // material→treatment mapping lives there, so this stays correct as materials change.
        if (PreviewIcon.Source is { } src)
            PreviewIcon.Source = RadialMenuControl.RenderPreviewIcon(src, 67, _material, SelectedColor);
    }

    private void UpdateIconUi() => UpdatePreview();

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_sync) return;
        // The '#' is shown outside the box; strip any the user typed or pasted (dedupe) before parsing.
        var text = HexBox.Text;
        var digits = text.TrimStart('#');
        if (!string.Equals(digits, text, StringComparison.Ordinal))
        {
            _sync = true;
            HexBox.Text = digits;
            HexBox.CaretIndex = digits.Length;
            _sync = false;
        }
        if (ActionTint.TryParseHex("#" + digits, out var c))
        {
            IsExactColor = true;   // a typed hex is EXACT intent — no material variant, on any material
            SetColor(c, updateHex: false);
            RaiseChanged();
        }
    }

    private void Preview_Click(object sender, MouseButtonEventArgs e) => OpenIconGrid();

    private void OpenIconGrid()
    {
        var dlg = new IconPickerWindow(SelectedIcon) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            SelectedIcon = dlg.SelectedIconName;
            UpdateIconUi();
            RaiseChanged();
        }
    }

    // ── Fetch-logo spinner ──────────────────────────────────────────────────────
    private readonly Stopwatch _logoBusyClock = new();
    private bool _logoBusy;

    private void FetchLogo_Click(object sender, RoutedEventArgs e)
    {
        if (_logoBusy) return;
        _logoBusy = true;
        _logoBusyClock.Restart();
        FetchGlyph.Source = _refreshIcon;
        if (!MotionPolicy.Reduce)   // Reduce Motion: no spin — the ✓ that follows still confirms completion
            FetchRotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(650))
                { RepeatBehavior = RepeatBehavior.Forever });
        FetchLogoClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Host calls this when its fetch completes: keep the spin visible for a minimum of ~0.5s,
    /// then show a checkmark for a beat and restore the refresh glyph.</summary>
    public async Task FinishLogoBusyAsync()
    {
        if (!_logoBusy) return;
        long elapsed = _logoBusyClock.ElapsedMilliseconds;
        if (elapsed < 500) await Task.Delay((int)(500 - elapsed));
        FetchRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        FetchRotate.Angle = 0;
        FetchGlyph.Source = _checkIcon;
        await Task.Delay(750);
        FetchGlyph.Source = _refreshIcon;
        _logoBusy = false;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SelectedIcon = _defIcon;
        IsExactColor = false;   // back to the type default, which is a base tone that takes the material variant
        SetColor(_defColor, updateHex: true);
        UpdateIconUi();
        RaiseChanged();
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuildPalette()
    {
        Palette.Children.Clear();
        // Distinct base colors from the shared store, trimmed of the 5 that sit closest to a neighbor so the
        // survivors are well-spaced.
        var bases = new List<WpfColor>();
        foreach (var hex in CustomColorStore.ToHex())
            if (ActionTint.TryParseHex(hex, out var c) && !bases.Contains(c)) bases.Add(c);
        RemoveClosest(bases, 5);

        // One row of PAIRED chips: dark base over its light equivalent. Clicking picks BOTH — the wheel
        // renders whichever variant matches its material.
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var c in bases) row.Children.Add(MakePairChip(c));
        Palette.Children.Add(row);
    }

    /// <summary>Drop the <paramref name="count"/> colors that sit closest to a neighbor, one at a time
    /// (recomputing after each), so the survivors are the most visually distinct.</summary>
    private static void RemoveClosest(List<WpfColor> colors, int count)
    {
        for (int k = 0; k < count && colors.Count > 2; k++)
        {
            int drop = -1; double best = double.MaxValue;
            for (int i = 0; i < colors.Count; i++)
            {
                double nearest = double.MaxValue;
                for (int j = 0; j < colors.Count; j++)
                    if (i != j) nearest = Math.Min(nearest, Dist(colors[i], colors[j]));
                if (nearest < best) { best = nearest; drop = i; }
            }
            if (drop >= 0) colors.RemoveAt(drop);
        }
    }

    private static double Dist(WpfColor a, WpfColor b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
    }

    private Button MakePairChip(WpfColor baseColor)
    {
        // The halves must sum EXACTLY to the button's Height (21+21=42). Any shortfall leaves a transparent
        // sliver below the light half, inside the outline, that shows through as a white gap.
        var light = ActionTint.LightEquivalent(baseColor);
        var halves = new StackPanel { SnapsToDevicePixels = true, UseLayoutRounding = true };
        halves.Children.Add(new Border { Height = 21, Background = new SolidColorBrush(baseColor) });
        halves.Children.Add(new Border { Height = 21, Background = new SolidColorBrush(light) });
        var b = new Button
        {
            Width = 22, Height = 42, Margin = new Thickness(1.5, 1, 1.5, 1),
            Content = halves,
            Style = (Style)Resources["PairSwatch"],
            SnapsToDevicePixels = true, UseLayoutRounding = true,
        };
        // A swatch has no text content at all — name it with a human colour description (a hex code
        // read aloud is noise); the hex stays available in the tooltip.
        System.Windows.Automation.AutomationProperties.SetName(b, Loc.T(ColorNamer.Describe(baseColor)));
        b.ToolTip = $"#{baseColor.R:X2}{baseColor.G:X2}{baseColor.B:X2}";
        b.Click += (_, _) =>
        {
            // The BASE tone regardless of the current material — the authored value is material-neutral and
            // the well/wheel derive the light half for dark materials themselves. Never store the light half
            // here: a subsequent hex nudge would persist that near-white as an exact colour on every
            // material, so the same gesture would mean different things per material.
            IsExactColor = false;
            SetColor(baseColor, updateHex: true);
            RaiseChanged();
        };
        return b;
    }
}
