using System.Text.Json.Serialization;

namespace ControllerWheel;

/// <summary>One action slot in a radial wheel. Label drives rendering; Action drives execution.</summary>
public sealed class WheelSlice
{
    /// <summary>Longest label kept (the editor's own box stops at 40). The wheel re-measures the whole
    /// string with FormattedText twice per slice per frame at ~125 Hz, and the hub chip and toasts wrap
    /// without a height limit — an unbounded label from a hand-edited or hostile config freezes the UI
    /// thread behind a topmost click-through overlay.</summary>
    public const int MaxLabelLength = 200;

    private readonly string _label = "";

    /// <summary>Slice text. Never null and never absurdly long, whatever the JSON said: a
    /// <c>"label": null</c> throws inside OnRender (FormattedText / ToUpperInvariant) and the bad slice is
    /// persisted, so it is a crash loop on every wheel open. System.Text.Json assigns null straight over a
    /// property initializer, so the guard has to live in the accessor.</summary>
    public string Label
    {
        get => _label;
        init
        {
            var v = value ?? "";
            _label = v.Length > MaxLabelLength ? v[..MaxLabelLength] : v;
        }
    }
    public string?       IconName { get; init; }   // Material icon name, e.g. "Controller"
    public string?       IconPath { get; init; }   // image file (e.g. game cover art); wins over IconName
    /// <summary>Per-slice tint override "#RRGGBB"; wins over the action-type tint. Always the authored,
    /// material-neutral colour — a swatch's base tone or the typed hex, never a material variant — so it
    /// round-trips through a save whatever material was active in the editor. The renderer derives the
    /// per-material variant (ActionTint.DisplayColor).</summary>
    public string?       IconColor { get; init; }

    /// <summary>True when <see cref="IconColor"/> was typed into the hex box: painted verbatim on every
    /// material, no transform (not even Mesa's boost). False = a palette swatch, which takes the material
    /// variant like a built-in default. Null on slices predating the field — <c>ActionTint.IsExactColor</c>
    /// infers it from <see cref="IconColorLight"/> so those keep the appearance they already have.</summary>
    public bool?         IconColorExact { get; init; }

    /// <summary>Legacy, read-only: the light variant of <see cref="IconColor"/> for a paired-palette pick.
    /// Nothing writes it; <c>ActionTint.IsExactColor</c> reads its presence/absence as the "swatch vs typed
    /// hex" marker for slices predating <see cref="IconColorExact"/>. Copy paths must carry it forward —
    /// dropping it silently reinterprets an old slice's colour.</summary>
    public string?       IconColorLight { get; init; }
    public string?       LogoPath  { get; init; }  // transparent game logo (assigned games); rendered tinted on the wheel
    public bool?         ShowLabel { get; init; }  // per-slice text-label visibility; null = default (off for custom-logo games, on otherwise)
    public ActionConfig? Action   { get; init; }

    /// <summary>A copy with <see cref="ShowLabel"/> forced to false — every newly-created slice is seeded
    /// label-off, logo or not. Unconditional on purpose: under <c>SliceLabelRule</c>'s "Slices I Choose"
    /// mode only a slice's own stored value counts, and <c>null</c> shows the editor's "Show label"
    /// checkbox as an indeterminate dash. Copies only the persisted fields; the UI-layer render fields
    /// (Icon/LogoImage) are recomputed downstream.</summary>
    public WheelSlice WithLabelHidden() => new()
    {
        Label = Label, IconName = IconName, IconPath = IconPath, IconColor = IconColor,
        IconColorExact = IconColorExact, IconColorLight = IconColorLight,
        LogoPath = LogoPath, ShowLabel = false, Action = Action,
    };

    /// <summary>A copy with a different <see cref="LogoPath"/>, every other persisted field carried across.
    /// Both re-pointers (startup logo self-heal, backup restore) go through here instead of hand-rolling a
    /// clone — an init-only class can't use `with`, and a clone that drops <see cref="IconColorLight"/> or
    /// <see cref="IconColorExact"/> quietly changes the slice's colour on some materials. Every colour
    /// field travels together; keeping the copy beside the field list is what catches the next one added.</summary>
    public WheelSlice WithLogoPath(string? logoPath) => new()
    {
        Label = Label, IconName = IconName, IconPath = IconPath, IconColor = IconColor,
        IconColorExact = IconColorExact, IconColorLight = IconColorLight,
        LogoPath = logoPath, ShowLabel = ShowLabel, Action = Action,
    };

    /// <summary>Resolved render icon, set by the UI layer. Typed as object so Core stays WPF-free
    /// (the WPF shell stores/casts a System.Windows.Media.ImageSource here).</summary>
    [JsonIgnore]
    public object? Icon { get; set; }

    /// <summary>True when <see cref="Icon"/> was loaded from <see cref="IconPath"/> (cover art):
    /// rendered cropped-to-fill rather than as a centred square glyph.</summary>
    [JsonIgnore]
    public bool IconIsCover { get; set; }

    /// <summary>Resolved transparent logo image (from <see cref="LogoPath"/>), set by the UI layer.
    /// Typed as object so Core stays WPF-free. When present, the wheel renders it tinted instead of the
    /// glyph + label.</summary>
    [JsonIgnore]
    public object? LogoImage { get; set; }
}
