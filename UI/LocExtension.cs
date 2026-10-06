using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ControllerWheel;

/// <summary><c>{loc:T Some label}</c> — the XAML form of <see cref="Loc.T"/> for a short label on a real
/// dependency property (<c>Text</c>, <c>Content</c>, <c>ToolTip</c>, <c>Header</c>, an automation name). Returns
/// the resolved string: the language is fixed for the run (<see cref="Loc.Init"/>), so nothing needs to rebind.
/// <para>⚠ Markup-extension grammar splits on <c>,</c> and <c>=</c> and nests on <c>{ }</c>. A label containing
/// any of those — or an apostrophe, which the quoted form <c>{loc:T 'a, b'}</c> then needs escaped — belongs on
/// <see cref="LocRich.SourceProperty"/> instead, whose plain attribute needs only XML escaping.
/// <c>tools/ExtractStrings</c> refuses an unquoted value carrying those characters.</para></summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string text) => Text = text;

    /// <summary>The English text — the translation key, byte for byte.</summary>
    [ConstructorArgument("text")]
    public string Text { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);
}

/// <summary><c>loc:LocRich.Source="**Bold** and `code`…"</c> — prose with inline markup on a
/// <see cref="TextBlock"/>. An attached property rather than a markup extension because
/// <see cref="TextBlock.Inlines"/> is not a dependency property (nothing can bind to it) and because prose
/// carries the commas, apostrophes and braces the extension grammar cannot. The value is the English source
/// and the translation key; it is translated with <see cref="Loc.T"/> and rendered through
/// <see cref="InlineMarkup"/> once — the language is fixed for the run, so there is no subscription to leak.
/// <para>⚠ Never wrap a <c>Source</c> attribute across source lines: XML attribute normalization turns the
/// line break and indentation into a single space, so the runtime key would differ from the authored one and
/// the string would silently render in English while the checker reports it translated.</para></summary>
public static class LocRich
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(string), typeof(LocRich), new FrameworkPropertyMetadata(null, OnChanged));

    /// <summary>Optional resource key of an <see cref="InlineMarkup.Look"/> in scope (a card's underlined
    /// lead-in, a toast's inks). Unset = <see cref="InlineMarkup.Body"/>.</summary>
    public static readonly DependencyProperty LookKeyProperty = DependencyProperty.RegisterAttached(
        "LookKey", typeof(string), typeof(LocRich), new FrameworkPropertyMetadata(null, OnChanged));

    public static string? GetSource(DependencyObject d) => (string?)d.GetValue(SourceProperty);
    public static void SetSource(DependencyObject d, string? value) => d.SetValue(SourceProperty, value);
    public static string? GetLookKey(DependencyObject d) => (string?)d.GetValue(LookKeyProperty);
    public static void SetLookKey(DependencyObject d, string? value) => d.SetValue(LookKeyProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        var source = GetSource(tb);
        if (string.IsNullOrEmpty(source)) { tb.Inlines.Clear(); return; }
        var look = GetLookKey(tb) is { } key ? tb.TryFindResource(key) as InlineMarkup.Look ?? InlineMarkup.Named(key) : InlineMarkup.Body;
        InlineMarkup.Fill(tb, Loc.T(source), look);
    }
}

/// <summary>The WPF side of <see cref="Loc"/>: what <c>Core</c> cannot express because it is WPF-free.
/// <para><see cref="ApplyTo"/> gives a window the language's reading direction. It is called once per window,
/// right after <c>InitializeComponent</c>, never re-applied (the language is fixed per run). ⚠ The overlay,
/// the wheel, the Arcade, the controller-button chips and the wheel miniatures are NOT windows this touches:
/// they pin <c>LeftToRight</c> themselves, because their aim math and drawn geometry do not mirror
/// (docs/OVERLAY.md ▸ Reading direction). Inside a mirrored window, anything that draws geometry or carries a
/// hand-computed layout — a flag, a hex prefix, an animation canvas — pins itself the same way.</para></summary>
internal static class LocWpf
{
    public static System.Windows.FlowDirection Flow => Loc.IsRtl ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

    public static void ApplyTo(FrameworkElement root) => root.FlowDirection = Flow;

    /// <summary>The language's own font chain (<see cref="HelpLanguage.FontFamily"/>) as a WPF family — what
    /// the wheel, hub and Customize previews draw in when <see cref="Loc.UsesSystemFace"/> says the material's
    /// display face cannot carry the script. Resolved once, after <see cref="Loc.Init"/>.</summary>
    public static System.Windows.Media.FontFamily LanguageFamily =>
        _languageFamily ??= new System.Windows.Media.FontFamily(HelpLocalization.Language(Loc.Lang).FontFamily);
    private static System.Windows.Media.FontFamily? _languageFamily;
}
