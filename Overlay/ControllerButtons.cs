using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>Which controller's glyph set the on-screen button prompts use.</summary>
public enum ControllerGlyphSet { PlayStation, Xbox }

/// <summary>Logical controller buttons we render as on-screen prompts.</summary>
public enum PadButton { Cross, Circle, Square, Triangle, L1, R1, L2, R2, L3, R3, Fn, Select, Start }

/// <summary>Renders on-screen controller-button prompts as small glossy-dark buttons (the wheel's Gloss
/// Dark look) regardless of the chosen material. The PlayStation face symbols (✕ ○ □ △) are drawn
/// GEOMETRICALLY — vector strokes, not Unicode — so they don't vary with font glyph coverage; the Xbox
/// set uses A/B/X/Y letters. One renderer is shared by the XAML <see cref="ControllerButton"/> element
/// and the wheel's drawn edit-mode legend so they match exactly.</summary>
public static class ControllerButtons
{
    private static ControllerGlyphSet _set = ControllerGlyphSet.PlayStation;

    /// <summary>Active glyph set (PlayStation ✕○□△ vs Xbox A/B/X/Y). Set from the detected controller +
    /// the user's Advanced "Button icons" override via <c>App.ApplyGlyphSet</c>. Changing it raises
    /// <see cref="Changed"/> so on-screen prompts redraw.</summary>
    public static ControllerGlyphSet Set
    {
        get => _set;
        set { if (_set == value) return; _set = value; Changed?.Invoke(); }
    }

    /// <summary>Fires when <see cref="Set"/> changes — glyph consumers subscribe to InvalidateVisual.</summary>
    public static event Action? Changed;

    // Shoulder buttons (and the Fn wheel-trigger chip) render as wider pills; everything else is round.
    // Select/Start are wider pills still: they're spelled out rather than glyphed, because neither pad
    // family has a recognisable symbol for them (Create/Options vs View/Menu).
    public static bool IsPill(PadButton b) =>
        b is PadButton.L1 or PadButton.R1 or PadButton.L2 or PadButton.R2
           or PadButton.Fn or PadButton.Select or PadButton.Start;
    public static double Aspect(PadButton b) => b is PadButton.Select or PadButton.Start ? 2.5
                                              : IsPill(b) ? 1.7 : 1.0;          // width / height
    public static double Width(PadButton b, double height) => height * Aspect(b);

    private static Brush Frozen(Brush b) { b.Freeze(); return b; }

    // ── Glossy-dark button background (matches the Gloss Dark material via the shared palette) ──
    private static readonly Brush ButtonFill = MakeButtonFill();
    private static Brush MakeButtonFill()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(GlossDarkPalette.Sheen,        235), 0.00));   // top sheen
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(GlossDarkPalette.AboveHorizon, 246), 0.46));
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(GlossDarkPalette.DarkCut,      252), 0.52));   // horizon
        g.GradientStops.Add(new GradientStop(GlossDarkPalette.A(GlossDarkPalette.Bounce,       246), 1.00));   // bottom bounce
        g.Freeze();
        return g;
    }
    private static readonly Pen RimPen = FrozenPen(GlossDarkPalette.A(GlossDarkPalette.Edge, 64), 1.0);   // faint cool rim
    private static Pen FrozenPen(Color c, double w) { var p = new Pen(new SolidColorBrush(c), w); p.Freeze(); return p; }

    // ── Symbol colours ──
    private static readonly Color PsBlue   = Color.FromRgb(0x8F, 0xB6, 0xFF);  // ✕ cross
    private static readonly Color PsRed    = Color.FromRgb(0xFF, 0x8A, 0x8A);  // ○ circle
    private static readonly Color PsPurple = Color.FromRgb(0xC9, 0xA6, 0xFF);  // □ square
    private static readonly Color PsGreen  = Color.FromRgb(0x8F, 0xE0, 0x8F);  // △ triangle
    private static readonly Color XbGreen  = Color.FromRgb(0x6C, 0xC2, 0x4A);  // A
    private static readonly Color XbRed    = Color.FromRgb(0xE0, 0x45, 0x4B);  // B
    private static readonly Color XbBlue   = Color.FromRgb(0x3B, 0x9D, 0xE0);  // X
    private static readonly Color XbAmber  = Color.FromRgb(0xF2, 0xC8, 0x4B);  // Y
    private static readonly Color LabelInk = Color.FromRgb(0xCF, 0xE3, 0xFF);  // shoulder/stick labels (cool white)

    private static readonly Typeface LabelFace =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    /// <summary>The cool-white ink used for shoulder/stick button labels — exposed so callers drawing their
    /// own button-styled chips (e.g. the wheel hub title) can match the L1/R1 look exactly.</summary>
    public static readonly Brush LabelInkBrush = Frozen(new SolidColorBrush(LabelInk));

    /// <summary>Draw just the glossy-dark "physical button" background — the rounded-rect gradient fill
    /// (top sheen → horizon → bottom bounce) plus the faint cool rim — filling <paramref name="bounds"/>.
    /// The same look as the L1/R1 pill, factored out so other UI can sit content on an identical chip.</summary>
    public static void DrawPill(DrawingContext dc, Rect bounds)
    {
        double corner = bounds.Height * 0.32;   // rounded rectangle, not a full stadium/pill
        dc.DrawRoundedRectangle(ButtonFill, RimPen,
            new Rect(bounds.X + 0.5, bounds.Y + 0.5, bounds.Width - 1, bounds.Height - 1), corner, corner);
    }

    /// <summary>Draw the button (glossy background + symbol) filling <paramref name="bounds"/>.</summary>
    public static void Draw(DrawingContext dc, Rect bounds, PadButton b, double ppd = 1.0)
    {
        double r = bounds.Height / 2.0;
        if (IsPill(b))
            DrawPill(dc, bounds);
        else
            dc.DrawEllipse(ButtonFill, RimPen, new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
                r - 0.5, r - 0.5);

        bool psShape = Set == ControllerGlyphSet.PlayStation
                       && b is PadButton.Cross or PadButton.Circle or PadButton.Square or PadButton.Triangle;
        if (psShape) DrawPsShape(dc, bounds, b);
        else         DrawLabel(dc, bounds, LabelFor(b), ColourFor(b), ppd);
    }

    // Geometric PlayStation face symbols — stroked, centred, ~half the button.
    private static void DrawPsShape(DrawingContext dc, Rect b, PadButton btn)
    {
        double cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2;
        double half = b.Height * 0.26;
        var pen = new Pen(new SolidColorBrush(ColourFor(btn)), Math.Max(1.4, b.Height * 0.085))
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        switch (btn)
        {
            case PadButton.Cross:
                dc.DrawLine(pen, new Point(cx - half, cy - half), new Point(cx + half, cy + half));
                dc.DrawLine(pen, new Point(cx + half, cy - half), new Point(cx - half, cy + half));
                break;
            case PadButton.Circle:
                dc.DrawEllipse(null, pen, new Point(cx, cy), half, half);
                break;
            case PadButton.Square:
                dc.DrawRectangle(null, pen, new Rect(cx - half, cy - half, half * 2, half * 2));
                break;
            case PadButton.Triangle:
                double t = half * 1.08;
                var fig = new PathFigure { StartPoint = new Point(cx, cy - t), IsClosed = true, IsFilled = false };
                fig.Segments.Add(new LineSegment(new Point(cx + t, cy + t * 0.78), true));
                fig.Segments.Add(new LineSegment(new Point(cx - t, cy + t * 0.78), true));
                var geo = new PathGeometry(); geo.Figures.Add(fig); geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
                break;
        }
    }

    /// <summary>The centre buttons' generic names. Every language reads the Latin loanwords except Japanese, where
    /// セレクト/スタート are the names players use. Not catalog keys: "Select" the verb already owns that key.</summary>
    private static string SelectName => Loc.Lang == "ja" ? "セレクト" : "Select";
    private static string StartName  => Loc.Lang == "ja" ? "スタート" : "Start";

    private static void DrawLabel(DrawingContext dc, Rect b, string text, Color colour, double ppd)
    {
        // Spelled-out labels (Select/Start) have to come down or they'd overrun even the 2.5-aspect pill.
        double fs = b.Height * (text.Length >= 4 ? 0.42 : text.Length >= 2 ? 0.50 : 0.52);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
            LabelFace, fs, new SolidColorBrush(colour), ppd);
        dc.DrawText(ft, new Point(b.X + (b.Width - ft.Width) / 2, b.Y + (b.Height - ft.Height) / 2));
    }

    /// <summary>A face button as INLINE TEXT, following the user's "Button icons" choice — <c>A</c> on Xbox,
    /// <c>✕</c> on PlayStation.
    ///
    /// <para>For prompts drawn as part of a run of text, where the glossy <see cref="ControllerButton"/>
    /// element can't be embedded — the arcade games' end cards, which are one `DrawCentered` string each.
    /// Never hard-code both spellings at once ("A / ✕  NEXT"): it names the wrong pad half the time.</para>
    ///
    /// <para>⚠ Consumers must redraw on <see cref="Changed"/>, or they'll keep last session's glyph. The
    /// arcade renderers get this for free: they redraw every frame.</para></summary>
    public static string Text(PadButton b) => Set == ControllerGlyphSet.Xbox
        ? b switch
        {
            PadButton.Cross => "A", PadButton.Circle => "B", PadButton.Square => "X", PadButton.Triangle => "Y",
            _ => LabelFor(b),
        }
        : b switch
        {
            PadButton.Cross => "✕", PadButton.Circle => "○", PadButton.Square => "□", PadButton.Triangle => "△",
            _ => LabelFor(b),
        };

    /// <summary>The button's name as SPEECH, for narration — "cross" / "A button", "L1" / "LB".
    ///
    /// <para>⚠ Narration must use THIS, never <see cref="Text"/>: on the PlayStation set Text returns the
    /// glyph character <c>✕</c>, which a speech engine reads as nothing useful, and on the Xbox set it
    /// returns a bare <c>"A"</c>, which is ambiguous mid-sentence ("A drops it"). Hence "A button" here and
    /// the plain face-symbol names on PlayStation.</para>
    ///
    /// <para>Never hard-code a button name in an announcement string — that is the bug this exists to
    /// close, and <c>T_Glyphs</c>' spoken-word pass fails the build for it.</para>
    ///
    /// <para><paramref name="capitalize"/> for the start of a clause ("Cross confirms, circle cancels").
    /// Only the first letter changes; "L1"/"LB" are already upper-case either way.</para></summary>
    public static string Spoken(PadButton b, bool capitalize = false)
    {
        string s = Set == ControllerGlyphSet.Xbox
            ? b switch
            {
                // "the" is load-bearing, not style: SAPI reads a bare leading "A" as the article
                // ("uh button picks up"). A determiner can't precede an article, so "the A button"
                // forces the letter reading; B/X/Y carry it for consistency.
                PadButton.Cross => Loc.T(UiText.Buttons.TheA), PadButton.Circle => Loc.T(UiText.Buttons.TheB),
                PadButton.Square => Loc.T(UiText.Buttons.TheX), PadButton.Triangle => Loc.T(UiText.Buttons.TheY),
                PadButton.L3 => Loc.T(UiText.Buttons.LeftStickClick), PadButton.R3 => Loc.T(UiText.Buttons.RightStickClick),
                _ => LabelFor(b),
            }
            : b switch
            {
                PadButton.Cross => Loc.T(UiText.Buttons.Cross), PadButton.Circle => Loc.T(UiText.Buttons.Circle),
                PadButton.Square => Loc.T(UiText.Buttons.Square), PadButton.Triangle => Loc.T(UiText.Buttons.Triangle),
                PadButton.L3 => Loc.T(UiText.Buttons.LeftStickClick), PadButton.R3 => Loc.T(UiText.Buttons.RightStickClick),
                _ => LabelFor(b),
            };
        return capitalize && s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
    }

    /// <summary>The button's short printed label ("A"/"LB" on Xbox, "L1" on PlayStation). Public for
    /// on-screen text that isn't a drawn glyph prompt — e.g. the hub's "Click LS to edit" notice, which
    /// must follow the chosen set like every other prompt. Face buttons return "" on the PlayStation set:
    /// there they are drawn geometrically, so use <see cref="Text"/> or <see cref="Spoken"/> instead.</summary>
    public static string Label(PadButton b) => LabelFor(b);

    private static string LabelFor(PadButton b) => Set == ControllerGlyphSet.Xbox
        ? b switch
        {
            PadButton.Cross => "A", PadButton.Circle => "B", PadButton.Square => "X", PadButton.Triangle => "Y",
            PadButton.L1 => "LB", PadButton.R1 => "RB", PadButton.L2 => "LT", PadButton.R2 => "RT",
            PadButton.L3 => "LS", PadButton.R3 => "RS",
            PadButton.Fn => "Fn", PadButton.Select => SelectName, PadButton.Start => StartName, _ => "",
        }
        : b switch   // PlayStation labels (face symbols are drawn geometrically, not labelled)
        {
            PadButton.L1 => "L1", PadButton.R1 => "R1", PadButton.L2 => "L2", PadButton.R2 => "R2",
            PadButton.L3 => "L3", PadButton.R3 => "R3",
            PadButton.Fn => "Fn", PadButton.Select => SelectName, PadButton.Start => StartName, _ => "",
        };

    private static Color ColourFor(PadButton b) => Set == ControllerGlyphSet.Xbox
        ? b switch
        {
            PadButton.Cross => XbGreen, PadButton.Circle => XbRed, PadButton.Square => XbBlue, PadButton.Triangle => XbAmber,
            _ => LabelInk,
        }
        : b switch
        {
            PadButton.Cross => PsBlue, PadButton.Circle => PsRed, PadButton.Square => PsPurple, PadButton.Triangle => PsGreen,
            _ => LabelInk,
        };
}
