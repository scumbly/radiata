using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
// (no "using System.Windows.Shapes" — System.Drawing is referenced app-wide, so Rectangle would be
// ambiguous; the flag shapes below name System.Windows.Shapes.Rectangle explicitly, and the figure
// renderer goes through this alias.)
using Shapes = System.Windows.Shapes;

namespace ControllerWheel;

/// <summary>The Settings "Help" tab: renders <see cref="HelpContent"/>'s topics (the canonical
/// controls/feature reference — CONTROLS.md is GENERATED from the same source). Left = search + a
/// category-grouped topic list; right = the rendered topic. Tokens ({invoke}/{disable}/face glyphs)
/// resolve through a host-supplied resolver so chord names and button symbols always match the USER'S
/// live configuration and glyph set. Read-only — no config plumbing.</summary>
public partial class HelpEditorControl : UserControl
{
    private Func<string, string?> _resolve = HelpContent.ExportToken;   // generic until the host loads us
    private string? _selectedId;
    private readonly Dictionary<string, Border> _rows = new(StringComparer.OrdinalIgnoreCase);

    // ── Display language: topic TEXT only. This pane's chrome stays English like every other Settings tab,
    // which is why the translated topics name UI elements in English. ─────────
    private string _lang = HelpLocalization.DefaultCode;

    /// <summary>The topic set in the current language (English structure, translated text).</summary>
    private IReadOnlyList<HelpTopic> Topics => HelpLocalization.Topics(_lang);

    /// <summary>Font stack for the current language — Japanese needs a CJK-capable face.</summary>
    private FontFamily LangFont => new(HelpLocalization.Language(_lang).FontFamily);

    /// <summary>Raised when the user picks a language, with its code ("en" / "es" / "de" / "ja" / "ar"). The host
    /// persists it to <see cref="SystemConfig.Language"/> (Help re-renders now; the rest of the UI at next start).</summary>

    private static readonly Brush HeadInk  = (Brush)System.Windows.Application.Current.Resources["HeadingInk"];
    private static readonly Brush BodyInk  = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0));
    private static readonly Brush MutedInk = new SolidColorBrush(Color.FromArgb(0x88, 0, 0, 0));
    private static readonly Brush CardBg   = new SolidColorBrush(Color.FromArgb(0x0A, 0, 0, 0));
    private static readonly Brush WarnBg   = new SolidColorBrush(Color.FromArgb(0x14, 0xB0, 0x3A, 0x2E));
    private static readonly Brush SelBg    = new SolidColorBrush(Color.FromArgb(0x14, 0x1C, 0xA8, 0xC9));

    public HelpEditorControl()
    {
        InitializeComponent();
        // Back glyph: MDI arrow-bold-circle pointing toward "where you came from" — left in a left-to-right UI, right in
        // a right-to-left one. An Image does NOT mirror its bitmap under RTL flow, so the direction is chosen here.
        BackBtn.Content = PackIconHelper.FromName(Loc.IsRtl ? "ArrowRightBoldCircle" : "ArrowLeftBoldCircle",
                              (Brush)System.Windows.Application.Current.Resources["UiMutedInk"]) is { } src
            ? new Image { Source = src, Width = 22, Height = 22 }
            : (object)new TextBlock { Text = Loc.IsRtl ? "▶" : "◀", FontSize = 12 };
        BuildTopicList(Topics);
        Loaded += (_, _) => { if (_selectedId is null) SelectTopic(Topics[0].Id); };
    }

    /// <summary>Host wiring: <paramref name="resolveToken"/> supplies live values for {invoke}/{disable}/
    /// glyph tokens (falls back to the generic doc values for anything it returns null for), and
    /// <paramref name="languageCode"/> sets the Help language (null = leave as-is). Re-call
    /// on controller-kind or trigger changes; the current topic re-renders with the new bindings.</summary>
    public void Load(Func<string, string?> resolveToken, string? languageCode = null)
    {
        _resolve = token => resolveToken(token) ?? HelpContent.ExportToken(token);
        if (languageCode is not null) SetLanguage(languageCode);
        else if (_selectedId is not null) RenderTopic(_selectedId);
    }

    /// <summary>Set the Help language (the picker lives on the Advanced tab). Re-renders when it actually changes.</summary>
    public void SetLanguage(string? code)
    {
        var lang = HelpLocalization.Normalize(code);
        bool changed = lang != _lang;
        _lang = lang;
        if (changed) ApplyLanguage();
        else if (_selectedId is not null) RenderTopic(_selectedId);
    }

    /// <summary>Rebuild the contents list and re-render the open topic in the current language. Search text
    /// is deliberately KEPT: the query is often an English UI term, which every language's keyword set still
    /// matches (HelpLocalization.Topics unions them).</summary>
    private void ApplyLanguage()
    {
        ApplyChrome();
        BuildTopicList(HelpLocalization.Search(_lang, SearchBox.Text));
        if (_selectedId is not null) RenderTopic(_selectedId);
    }

    /// <summary>Localize this pane's own labels + tooltips (the one bit of UI chrome that follows the Help
    /// language — see <see cref="HelpLocalization.Chrome"/>).</summary>
    private void ApplyChrome()
    {
        string T(string s) => HelpLocalization.Text(_lang, s);
        var font = LangFont;
        // Reading direction follows the language for the pane's text and layout. The flag swatch and the
        // illustrations pin themselves back to left-to-right — a flag and a diagram are not text.
        FlowDirection = HelpLocalization.Language(_lang).Rtl
            ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
        ContentsHeading.Text       = T(HelpLocalization.Chrome.Contents);
        ContentsHeading.FontFamily = font;
        SearchBox.ToolTip          = T(HelpLocalization.Chrome.SearchTip);
        SearchBox.FontFamily       = font;
        BackBtn.ToolTip            = T(HelpLocalization.Chrome.BackTip);
        System.Windows.Automation.AutomationProperties.SetName(SearchBox, T(HelpLocalization.Chrome.SearchTip));
        System.Windows.Automation.AutomationProperties.SetName(BackBtn, T(HelpLocalization.Chrome.BackTip));
    }

    /// <summary>Deep-link entry (contextual "Learn more" links, the tray Help item).</summary>
    public void SelectTopic(string id)
    {
        var topic = Topics.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (topic is null) return;
        _selectedId = topic.Id;
        foreach (var (rid, row) in _rows)
            row.Background = rid.Equals(topic.Id, StringComparison.OrdinalIgnoreCase) ? SelBg : Brushes.Transparent;
        RenderTopic(topic.Id);
        TopicScroller.ScrollToTop();
        // The scroller carries the open topic's title so focusing it (FocusTopic — a Help-chip jump)
        // makes Narrator announce the topic, not an anonymous scroll region.
        System.Windows.Automation.AutomationProperties.SetName(TopicScroller, topic.Title);
    }

    /// <summary>Move keyboard focus to the rendered topic. A Help-chip jump calls this so a Narrator
    /// user lands on the answer they asked for, not on the pane chrome (language dropdown, search box,
    /// topic list). Deferred to Input priority — the tab switch needs a layout pass first.</summary>
    public void FocusTopic() =>
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => TopicScroller.Focus());

    // ── Topic list (left) ───────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        BuildTopicList(HelpLocalization.Search(_lang, SearchBox.Text));
        UpdateSearchGlyph();
    }

    // ── Back affordance + search watermark ─────────────────────────────────────

    /// <summary>Raised by the ◀ back button when there's no in-Help history left to pop; the host
    /// returns to the tab the Help chip came from.</summary>
    public event EventHandler? BackRequested;

    // Following an inline [[cross-link]] pushes the topic it left; ◀ pops back through these before
    // returning to the originating tab (_hostBack).
    private readonly Stack<string> _history = new();
    private bool _hostBack;

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count > 0) { SelectTopic(_history.Pop()); UpdateBackVisible(); }
        else BackRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cross-link navigation: remember where we came from so ◀ can walk back.</summary>
    private void NavigateToTopic(string id)
    {
        if (_selectedId is { } cur && !cur.Equals(id, StringComparison.OrdinalIgnoreCase)) _history.Push(cur);
        SelectTopic(id);
        UpdateBackVisible();
    }

    /// <summary>Host control of the ◀ affordance: true when a Help chip opened Help (back returns to that
    /// tab), false on a direct Help-tab visit (which also resets any old topic history).</summary>
    public void SetBackVisible(bool visible)
    {
        _hostBack = visible;
        if (!visible) _history.Clear();
        UpdateBackVisible();
    }

    private void UpdateBackVisible() =>
        BackBtn.Visibility = _hostBack || _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void SearchBox_FocusChanged(object sender, RoutedEventArgs e) => UpdateSearchGlyph();
    // Only TEXT hides the magnifying glass — focus alone keeps it.
    private void UpdateSearchGlyph() =>
        SearchGlyph.Visibility = SearchBox.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;

    private void BuildTopicList(IEnumerable<HelpTopic> topics)
    {
        TopicList.Children.Clear();
        _rows.Clear();
        var list = topics.ToList();
        foreach (var cat in HelpContent.CategoryOrder)
        {
            var inCat = list.Where(t => t.Category == cat).ToList();
            if (inCat.Count == 0) continue;
            TopicList.Children.Add(new TextBlock
            {
                // Grouping stays keyed on the English category name; only the heading is translated.
                Text = HelpLocalization.Category(_lang, cat), FontFamily = LangFont,
                FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = MutedInk,
                Margin = new Thickness(6, 10, 0, 3),
            });
            foreach (var t in inCat)
            {
                // InvokableBorder: Space/Enter and UIA Invoke run the SAME action as the click. Still a
                // Border, which _rows and the styling below depend on.
                var row = new InvokableBorder
                {
                    CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 4, 8, 4),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = t.Id == _selectedId ? SelBg : Brushes.Transparent,
                    Child = new TextBlock
                    {
                        Inlines = { InlineMarkup.IsolateArrows(new Run(t.Title)) },
                        FontFamily = LangFont, FontSize = 13, Foreground = BodyInk,
                        TextWrapping = TextWrapping.Wrap,
                    },
                };
                var id = t.Id;
                void Open() { _history.Clear(); SelectTopic(id); UpdateBackVisible(); }
                row.MouseLeftButtonUp += (_, _) => Open();
                row.Invoked = Open;
                System.Windows.Automation.AutomationProperties.SetName(row, t.Title);
                row.SetResourceReference(FocusVisualStyleProperty, "AccessFocusVisual");
                _rows[id] = row;
                TopicList.Children.Add(row);
            }
        }
        if (TopicList.Children.Count == 0)
            TopicList.Children.Add(new TextBlock
            {
                Text = HelpLocalization.Text(_lang, HelpLocalization.Chrome.NoMatch), FontFamily = LangFont,
                FontSize = 12, Foreground = MutedInk, Margin = new Thickness(6, 10, 0, 0),
            });
    }

    // ── Topic renderer (right) ──────────────────────────────────────────────────

    private void RenderTopic(string id)
    {
        var topic = Topics.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (topic is null) return;
        RenderTopicInto(TopicBody, topic, _resolve, _lang, NavigateToTopic);
    }

    /// <summary>Render one <see cref="HelpTopic"/> into <paramref name="host"/> — the SINGLE topic-rendering
    /// path in the app. The Help tab drives it for the pane on its right; <see cref="HelpTopicWindow"/>
    /// drives it for the standalone (?) pop-ups (the onboarding Accessibility card's, today), which is what
    /// makes those pop-ups synchronized with Help by construction rather than by remembering to copy text.
    /// <para><paramref name="navigate"/> handles [[cross-links]]; pass null and they render as plain bold
    /// (a surface with nowhere to navigate to shouldn't offer dead links).</para></summary>
    internal static void RenderTopicInto(System.Windows.Controls.Panel host, HelpTopic topic,
                                         Func<string, string?> resolve,
                                         string lang, Action<string>? navigate)
    {
        var font = new FontFamily(HelpLocalization.Language(lang).FontFamily);
        host.Children.Clear();
        host.Children.Add(new TextBlock
        {
            Inlines = { InlineMarkup.IsolateArrows(new Run(HelpContent.ResolveTokens(topic.Title, resolve))) },
            FontFamily = font, FontSize = 15.5, FontWeight = FontWeights.Bold,
            Foreground = HeadInk, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        foreach (var block in topic.Body)
        {
            switch (block.Kind)
            {
                case HelpBlockKind.Heading:
                    host.Children.Add(Styled(block, 14 + Bump, FontWeights.SemiBold, new Thickness(0, 10, 0, 2), resolve, lang, navigate));
                    break;
                case HelpBlockKind.Para:
                    host.Children.Add(Styled(block, 13 + Bump, FontWeights.Normal, new Thickness(0, 2, 0, 6), resolve, lang, navigate));
                    break;
                case HelpBlockKind.Bullet:
                    var tb = Styled(block, 13 + Bump, FontWeights.Normal, new Thickness(14 + block.Indent * 16, 1, 0, 3), resolve, lang, navigate);
                    PrefixRun(tb, new Run("•  ") { Foreground = MutedInk });
                    host.Children.Add(tb);
                    break;
                case HelpBlockKind.Figure:
                    host.Children.Add(BuildFigure(block, resolve, lang, navigate));
                    break;
                case HelpBlockKind.Tip:
                case HelpBlockKind.Warning:
                    var inner = Styled(block, 12 + Bump, FontWeights.Normal, new Thickness(0), resolve, lang, navigate);
                    PrefixRun(inner, new Run(block.Kind == HelpBlockKind.Tip ? "💡  " : "⚠  "));
                    host.Children.Add(new Border
                    {
                        Background = block.Kind == HelpBlockKind.Tip ? CardBg : WarnBg,
                        CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 7, 10, 7),
                        Margin = new Thickness(0, 6, 0, 6), Child = inner,
                    });
                    break;
            }
        }
        SelectableText.EnableWithin(host);   // help text is copy-worthy; fail-soft
    }

    /// <summary>Prepend a run to a TextBlock's inlines. InsertBefore(FirstInline, …) throws when the block
    /// parsed to NO inlines — tokens/localization can resolve a body to empty, and a throw here takes the
    /// whole Settings window's UI thread down.</summary>
    private static void PrefixRun(TextBlock tb, Run prefix)
    {
        if (tb.Inlines.FirstInline is { } first) tb.Inlines.InsertBefore(first, prefix);
        else tb.Inlines.Add(prefix);
    }

    /// <summary>Arabic and Japanese read small at Latin sizes; the Help body gets one point more in those languages.</summary>
    private static double Bump => Loc.UsesSystemFace ? 1 : 0;

    /// <summary>A TextBlock with the block's text, **bold** and `code` spans parsed into inlines.</summary>
    private static TextBlock Styled(HelpBlock block, double size, FontWeight weight, Thickness margin,
                                    Func<string, string?> resolve, string lang, Action<string>? navigate)
    {
        var tb = new TextBlock
        {
            FontFamily = new FontFamily(HelpLocalization.Language(lang).FontFamily),
            FontSize = size, FontWeight = weight, Foreground = BodyInk,
            TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.45, Margin = margin,
        };
        foreach (var inline in InlineMarkup.Parse(HelpContent.ResolveTokens(block.Text, resolve), HelpLook(lang, navigate)))
            tb.Inlines.Add(inline);
        return tb;
    }

    private static readonly Brush TopicLinkInk = new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0x8C));

    /// <summary>The Help pane's rendering of the shared markup: semibold emphasis, the topic-link ink, and
    /// a translated tooltip on cross-links. Web links show their URL.</summary>
    private static InlineMarkup.Look HelpLook(string lang, Action<string>? navigate) => new(
        FontWeights.SemiBold, TopicLinkInk, HeadInk, TopicLinkInk,
        LinkToolTip: HelpLocalization.Text(lang, HelpLocalization.Chrome.TopicLinkTip), Navigate: navigate);


    // ── Illustrations ───────────────────────────────────────────────────────────
    // HelpFigures declares each diagram as data (Core stays WPF-free); this is the interpreter. Nothing
    // here animates, so Reduce Motion has no say in it.

    private static readonly Brush FigFaint      = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));
    private static readonly Brush FigAccent     = new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0x8C));
    private static readonly Brush FigAccentSoft = new SolidColorBrush(Color.FromArgb(0x1E, 0x33, 0x70, 0x8C));
    private static readonly Brush FigWarn       = new SolidColorBrush(Color.FromRgb(0xB0, 0x3A, 0x2E));
    // Resolved defensively: a missing resource key throws from a STATIC initializer, which takes the whole
    // Settings window down from a click handler rather than showing a wrong colour.
    private static readonly Brush FigSurface =
        System.Windows.Application.Current?.TryFindResource("UiCardBg") as Brush
        ?? new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF9));

    /// <summary>Ink role → brush. A method, not a lookup table, so it can't run before the static brushes
    /// above are initialized.</summary>
    private static Brush? FigBrush(FigInk ink) => ink switch
    {
        FigInk.Body       => BodyInk,
        FigInk.Muted      => MutedInk,
        FigInk.Faint      => FigFaint,
        FigInk.Surface    => FigSurface,
        FigInk.Accent     => FigAccent,
        FigInk.AccentSoft => FigAccentSoft,
        FigInk.Warn       => FigWarn,
        _                 => null,      // FigInk.None — no stroke / no fill
    };

    private static readonly DoubleCollection FigDashes = [3, 3];

    /// <summary>A figure block: the drawing, scaled down to the reading column if it doesn't fit, over its
    /// caption. The caption is also the figure's accessible name — it's the one string that says what the
    /// picture means, and the shapes inside carry no names of their own (WPF has no XAML-only way to hide a
    /// decorative element from the automation tree, so they are left unnamed).</summary>
    private static UIElement BuildFigure(HelpBlock block, Func<string, string?> resolve, string lang,
                                         Action<string>? navigate)
    {
        var caption = Styled(block, 11.5, FontWeights.Normal, new Thickness(0, 8, 0, 0), resolve, lang, navigate);
        caption.Foreground = MutedInk;
        caption.FontStyle  = FontStyles.Italic;

        var stack = new StackPanel();
        if (HelpFigures.ById(block.Figure) is { } fig)
        {
            var canvas = new Canvas { Width = fig.Width, Height = fig.Height, FlowDirection = System.Windows.FlowDirection.LeftToRight };
            bool rtl = HelpLocalization.Language(lang).Rtl, mirrored = fig.Mirrored(rtl);
            foreach (var part in fig.PartsFor(rtl)) AddPart(canvas, part, resolve, lang, mirrored);
            stack.Children.Add(new Viewbox
            {
                Child = canvas, Stretch = Stretch.Uniform,
                // DownOnly: a small figure blown up to the column width would just look coarse.
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        stack.Children.Add(caption);

        var border = new Border
        {
            Background = CardBg, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 8, 0, 8),
            Child = stack,
        };
        System.Windows.Automation.AutomationProperties.SetName(
            border, HelpContent.ResolveTokens(block.Text, resolve));
        return border;
    }

    private static void AddPart(Canvas canvas, FigPart part, Func<string, string?> resolve, string lang, bool mirrored)
    {
        var stroke = FigBrush(part.Ink);
        var fill   = FigBrush(part.Fill);
        switch (part.Shape)
        {
            case FigShape.Rect:
                var rect = new Shapes.Rectangle
                {
                    Width = part.W, Height = part.H, RadiusX = part.R, RadiusY = part.R,
                    Stroke = stroke, StrokeThickness = stroke is null ? 0 : 1.2, Fill = fill,
                };
                if (part.Dashed) rect.StrokeDashArray = FigDashes;
                Place(canvas, rect, part.X, part.Y);
                break;

            case FigShape.Ellipse:
                var ellipse = new Shapes.Ellipse
                {
                    Width = part.W, Height = part.H,
                    Stroke = stroke, StrokeThickness = stroke is null ? 0 : 1.2, Fill = fill,
                };
                if (part.Dashed) ellipse.StrokeDashArray = FigDashes;
                Place(canvas, ellipse, part.X, part.Y);
                break;

            case FigShape.Wedge:
                var wedge = new Shapes.Path
                {
                    Data = WedgeGeometry(part.X, part.Y, part.R, part.W, part.From, part.Sweep),
                    Stroke = stroke, StrokeThickness = stroke is null ? 0 : 1.2, Fill = fill,
                };
                if (part.Dashed) wedge.StrokeDashArray = FigDashes;
                canvas.Children.Add(wedge);
                break;

            case FigShape.Line:
                canvas.Children.Add(Stroke(part.X, part.Y, part.X2, part.Y2, stroke, part.Dashed));
                break;

            case FigShape.Arrow:
                AddArrow(canvas, part, stroke);
                break;

            case FigShape.Text:
                AddText(canvas, part, resolve, lang, mirrored);
                break;
        }
    }

    private static void Place(Canvas canvas, UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }

    private static Shapes.Line Stroke(double x1, double y1, double x2, double y2, Brush? ink, bool dashed)
    {
        var line = new Shapes.Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = ink, StrokeThickness = 1.2, StrokeEndLineCap = PenLineCap.Round,
        };
        if (dashed) line.StrokeDashArray = FigDashes;
        return line;
    }

    private const double ArrowHead = 8;

    private static void AddArrow(Canvas canvas, FigPart part, Brush? ink)
    {
        double dx = part.X2 - part.X, dy = part.Y2 - part.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.01) return;                              // a zero-length arrow has no direction to point
        double ux = dx / len, uy = dy / len, px = -uy, py = ux;
        // Stop the shaft short of the tip so the stroke can't poke out of the filled head.
        canvas.Children.Add(Stroke(part.X, part.Y, part.X2 - ux * (ArrowHead - 1),
                                   part.Y2 - uy * (ArrowHead - 1), ink, part.Dashed));
        canvas.Children.Add(new Shapes.Polygon
        {
            Fill = ink,
            Points =
            [
                new Point(part.X2, part.Y2),
                new Point(part.X2 - ux * ArrowHead + px * 3.6, part.Y2 - uy * ArrowHead + py * 3.6),
                new Point(part.X2 - ux * ArrowHead - px * 3.6, part.Y2 - uy * ArrowHead - py * 3.6),
            ],
        });
    }

    private static void AddText(Canvas canvas, FigPart part, Func<string, string?> resolve, string lang, bool mirrored)
    {
        // Literal parts (key, file and code names) run left to right in every language — see FigPart.Literal.
        bool rtlText = HelpLocalization.Language(lang).Rtl && !part.Literal;
        // A TopLeft label hugs the edge of its box nearest what it labels: the authored left edge, or the right
        // edge once the figure is mirrored. TextAlignment is interpreted in the TextBlock's own flow direction,
        // so the physical edge is reached with Left or Right depending on the label's script.
        bool hugRight = mirrored;
        var align = part.Anchor != FigAnchor.TopLeft ? TextAlignment.Center
                  : hugRight != rtlText ? TextAlignment.Right : TextAlignment.Left;
        var tb = new TextBlock
        {
            // Literal parts (button and key names) are never translated; every other label is a translation key.
            Text = HelpContent.ResolveTokens(part.Literal ? part.Text ?? "" : HelpLocalization.Text(lang, part.Text ?? ""), resolve),
            FontFamily = new FontFamily(HelpLocalization.Language(lang).FontFamily),
            FontSize = part.Size, FontWeight = part.Bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = FigBrush(part.Ink) ?? BodyInk,
            // The canvas is pinned left-to-right (a figure mirrors only through PartsFor); each translated label
            // carries the language's own direction so Arabic shapes and wraps as text should within its box.
            FlowDirection = rtlText
                ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight,
            // Every label wraps: a translation runs longer than the English it replaced, and a figure's
            // canvas has no slack to absorb it.
            Width = part.W, TextWrapping = TextWrapping.Wrap, LineHeight = part.Size * 1.35,
            TextAlignment = align,
        };
        double left = part.Anchor == FigAnchor.TopLeft ? part.X : part.X - part.W / 2;
        double top  = part.Y;
        if (part.Anchor == FigAnchor.MiddleCenter)
        {
            tb.Measure(new Size(part.W, double.PositiveInfinity));
            top = part.Y - tb.DesiredSize.Height / 2;
        }
        Place(canvas, tb, left, top);
    }

    /// <summary>An annular sector — the wheel-slice shape. Angles are degrees CLOCKWISE from 12 o'clock,
    /// matching how the overlay itself lays slices out.</summary>
    private static Geometry WedgeGeometry(double cx, double cy, double inner, double outer,
                                          double fromDeg, double sweepDeg)
    {
        Point At(double r, double deg)
        {
            double a = deg * Math.PI / 180;
            return new Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a));
        }
        double toDeg = fromDeg + sweepDeg;
        bool large = Math.Abs(sweepDeg) > 180;
        var figure = new PathFigure { StartPoint = At(inner, fromDeg), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(At(outer, fromDeg), true));
        figure.Segments.Add(new ArcSegment(At(outer, toDeg), new Size(outer, outer), 0, large,
                                           SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(At(inner, toDeg), true));
        figure.Segments.Add(new ArcSegment(At(inner, fromDeg), new Size(inner, inner), 0, large,
                                           SweepDirection.Counterclockwise, true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    // ── Language flag swatch ────────────────────────────────────────────────────
    // ⚠ Drawn, never emoji: Windows 10's Segoe UI Emoji has no regional-indicator flag glyphs, so
    // 🇬🇧/🇪🇸/🇩🇪/🇯🇵 render as two-letter boxes ("GB"/"ES"/…) on this app's primary target. A hand-built
    // Border/Grid swatch has no font dependency and is pixel-identical everywhere.
    //
    // Fixed at 22×14 to match the flag hosts (Advanced tab, onboarding), which own the rounded-corner clip (borderless, so
    // nothing insets the content box below this size). Each flag is a flat, simplified rendition — legible
    // at this size, not heraldically exact.

    private const double FlagW = 22, FlagH = 14;

    /// <summary>Builds the flag content for <paramref name="langCode"/> ("en"/"es"/"de"/"ja"/"ar"); an unknown
    /// code falls back to the "en" swatch (matches <see cref="HelpLocalization.Normalize"/>'s fallback).</summary>
    internal static UIElement BuildFlagGlyph(string langCode) => langCode switch
    {
        "es" => BuildHorizontalBands((Brush)new SolidColorBrush(Color.FromRgb(0xAA, 0x15, 0x1B)),
                                      (Brush)new SolidColorBrush(Color.FromRgb(0xF1, 0xBF, 0x00)),
                                      (Brush)new SolidColorBrush(Color.FromRgb(0xAA, 0x15, 0x1B)),
                                      1, 2, 1),
        "de" => BuildHorizontalBands((Brush)Brushes.Black,
                                      (Brush)new SolidColorBrush(Color.FromRgb(0xD0, 0x0, 0x0)),
                                      (Brush)new SolidColorBrush(Color.FromRgb(0xFF, 0xCE, 0x00)),
                                      1, 1, 1),
        "ja" => BuildJapanFlag(),
        "ar" => BuildPalestineFlag(),
        _    => BuildUkFlag(),   // "en" and any unrecognized code
    };

    /// <summary>Three equal-ratio horizontal bands (Spain's are 1:2:1, Germany's 1:1:1 — the caller passes
    /// the ratio via <paramref name="r1"/>/<paramref name="r2"/>/<paramref name="r3"/>).</summary>
    private static UIElement BuildHorizontalBands(Brush top, Brush mid, Brush bottom, double r1, double r2, double r3)
    {
        var grid = new Grid { Width = FlagW, Height = FlagH };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(r1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(r2, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(r3, GridUnitType.Star) });
        var bands = new[] { top, mid, bottom };
        for (int i = 0; i < 3; i++)
        {
            var rect = new System.Windows.Shapes.Rectangle { Fill = bands[i] };
            Grid.SetRow(rect, i);
            grid.Children.Add(rect);
        }
        return grid;
    }

    /// <summary>The flag swatch's tooltip: the localized word "Language" for anyone who doesn't recognize a flag; the
    /// Palestinian flag carries its motto (Palestinian Arabic, the author's wording, shown in every UI language) on a
    /// second line laid out right-to-left whatever the window's direction.</summary>
    public static object FlagToolTip(string lang)
    {
        var word = HelpLocalization.Text(lang, HelpLocalization.Chrome.Language);
        if (lang != "ar") return word;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = word });
        panel.Children.Add(new TextBlock
        {
            Text = PalestineFlagMotto, FlowDirection = System.Windows.FlowDirection.RightToLeft,
            FontFamily = new FontFamily(HelpLocalization.Language("ar").FontFamily), Margin = new Thickness(0, 2, 0, 0),
        });
        return panel;
    }
    private const string PalestineFlagMotto = "من المية للمية";

    /// <summary>Three equal horizontal bands (black, white, green) with a red triangle at the hoist whose apex
    /// reaches a third of the way across — simplified like the others, legible at 22×14.</summary>
    private static UIElement BuildPalestineFlag()
    {
        var grid = (Grid)BuildHorizontalBands((Brush)Brushes.Black,
                                              (Brush)Brushes.White,
                                              (Brush)new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0x3D)),
                                              1, 1, 1);
        var hoist = new System.Windows.Shapes.Polygon
        {
            Points = new PointCollection { new Point(0, 0), new Point(FlagW / 3.0, FlagH / 2.0), new Point(0, FlagH) },
            Fill   = new SolidColorBrush(Color.FromRgb(0xCE, 0x11, 0x26)),
        };
        Grid.SetRowSpan(hoist, 3);
        grid.Children.Add(hoist);
        // The hoist stays on the left even while the pane reads right-to-left.
        grid.FlowDirection = System.Windows.FlowDirection.LeftToRight;
        return grid;
    }

    /// <summary>White field, red disc centred (simplified — the real Hinomaru sits very slightly toward
    /// the hoist, invisible at this size).</summary>
    private static UIElement BuildJapanFlag()
    {
        var grid = new Grid { Width = FlagW, Height = FlagH, Background = Brushes.White };
        var disc = new System.Windows.Shapes.Ellipse
        {
            Width = FlagH * 0.62, Height = FlagH * 0.62,
            Fill = new SolidColorBrush(Color.FromRgb(0xBC, 0x00, 0x2D)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(disc);
        return grid;
    }

    /// <summary>Union Jack, layered on a Canvas rather than a Grid because the saltire arms are DIAGONALS:
    /// navy field, white diagonal saltire, red diagonal saltire over it, then the white St George cross and
    /// the red cross on top. Simplified in two ways that only matter above ~40px: the red diagonals aren't
    /// counterchanged, and the crosses are centred rather than the real 3:5 proportions.</summary>
    private static UIElement BuildUkFlag()
    {
        var navy      = new SolidColorBrush(Color.FromRgb(0x01, 0x21, 0x69));
        var red       = new SolidColorBrush(Color.FromRgb(0xC8, 0x10, 0x2E));
        var canvas    = new Canvas { Width = FlagW, Height = FlagH, Background = navy, ClipToBounds = true };

        // Diagonals corner-to-corner, drawn twice: a wide white pair, then a narrower red pair over it.
        void Diagonal(Brush stroke, double thickness)
        {
            foreach (var (x1, y1, x2, y2) in new[] { (0.0, 0.0, FlagW, FlagH), (0.0, FlagH, FlagW, 0.0) })
                canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    Stroke = stroke, StrokeThickness = thickness,
                });
        }
        Diagonal(Brushes.White, 3.4);
        Diagonal(red, 1.6);

        // St George's cross: white bars first, red bars centred on top of them.
        void Cross(Brush fill, double arm)
        {
            canvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = FlagW, Height = arm, Fill = fill,
                RenderTransform = new TranslateTransform(0, (FlagH - arm) / 2),
            });
            canvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = arm, Height = FlagH, Fill = fill,
                RenderTransform = new TranslateTransform((FlagW - arm) / 2, 0),
            });
        }
        Cross(Brushes.White, 4.6);
        Cross(red, 2.6);
        return canvas;
    }
}
