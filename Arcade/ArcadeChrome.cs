using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// The shared look every arcade game inherits: the bezel, the type scale, the HUD conventions, the guard
/// card. Consistency here is structural — a game can't accidentally re-decide the frame, so the set reads as
/// one studio's work while each game brings only its own accent and motion language.
///
/// <para>Deliberately independent of the wheel materials: Arcade doesn't restyle itself for Pearl or
/// Salvage. That would multiply eight materials by every future game, and the games are supposed to look
/// like games, not like wheel skins.</para>
///
/// <para>No XAML and no resource dictionary — everything here is drawn in code with frozen brushes. That
/// sidesteps this repo's most-repeated crash class (a <c>StaticResource</c>/<c>FindResource</c> miss at
/// runtime, see docs/SETTINGS-UI.md) on a surface that renders 60 times a second.</para>
/// </summary>
internal static class ArcadeChrome
{
    private static Brush B(byte r, byte g, byte b, byte a = 255)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }

    // ── The shared palette ramp ───────────────────────────────────────────────
    // Cool, dim, slightly blue — so a game's accent is the only saturated thing on screen and reads at
    // couch distance without being lit like a slot machine over someone's paused game.

    public static readonly Brush Void      = B(0x0A, 0x0D, 0x14);   // the well / deep background
    public static readonly Brush Field     = B(0x12, 0x17, 0x21);   // playfield disc
    public static readonly Brush BezelBody = B(0x1B, 0x21, 0x2E);
    public static readonly Brush BezelEdge = B(0x39, 0x44, 0x59);
    public static readonly Brush Ink       = B(0xEC, 0xF1, 0xF7);
    public static readonly Brush InkDim    = B(0x8B, 0x97, 0xAA);
    public static readonly Brush InkFaint  = B(0x4A, 0x54, 0x66);
    public static readonly Brush Danger    = B(0xD8, 0x51, 0x4A);
    public static readonly Brush Scrim     = B(0x06, 0x08, 0x0D, 0xD8);   // behind the guard card
    /// <summary>The selected row's backing bar in the START pause menu. Light enough to read as "this one"
    /// against the scrim without competing with the row's own text.</summary>
    public static readonly Brush Panel     = B(0x2A, 0x33, 0x45, 0xEE);
    /// <summary>A <see cref="Panel"/> turned down — for a control that is present but can't be used (the
    /// window-move cue for a direction clamped at an end). Darkened rather than made transparent: the games
    /// behind it are bright and varied, so a translucent plate gets louder on some boards, not quieter.</summary>
    public static readonly Brush PanelDim  = B(0x15, 0x1A, 0x24, 0xEE);

    private static Pen P(Brush b, double w) { var p = new Pen(b, w); p.Freeze(); return p; }

    public static readonly Pen BezelPen  = P(BezelEdge, 3);
    public static readonly Pen SpokePen  = P(B(0x22, 0x2A, 0x39), 1);
    public static readonly Pen FaintPen  = P(InkFaint, 1);
    /// <summary>The rim that goes with <see cref="PanelDim"/>.</summary>
    public static readonly Pen FaintDimPen = P(B(0x2A, 0x31, 0x3D), 1);

    /// <summary>Bezel thickness as a fraction of the window radius. The playfield is what's left inside it.</summary>
    public const double BezelFrac = 0.055;

    /// <summary>An overlaid-UI text size, scaled by <see cref="ArcadeTuning.HudTextScale"/>; <see cref="UiArt"/>
    /// is the same for the pictograms beside that text. Every readout, shout, prompt and card goes through
    /// these; nothing in play does.</summary>
    public static double Ui(double size)    => size * ArcadeTuning.HudTextScale;
    public static double UiArt(double size) => size * ArcadeTuning.HudArtScale;

    // ── Type ──────────────────────────────────────────────────────────────────

    // Lazy, never a static field initializer: pack:// isn't registered until WPF's Application exists, so
    // building this Uri during static init would throw a TypeInitializationException and take the class
    // down with it (the same trap RadialMenuControl's ReactorHubFace documents).
    private static Typeface? _face;
    /// <summary>Share Tech (OFL 1.1, embedded — THIRD-PARTY-LICENSES.md §7). Regular only; the family ships
    /// no Bold and a synthesized one smears its hard corners. Reused from Reactor's hub so the arcade sits in
    /// the same typographic family as the app rather than importing a ninth voice.</summary>
    public static Typeface Face => _face ??= new Typeface(
        new FontFamily(new Uri("pack://application:,,,/"), "./Assets/fonts/#Share Tech, Segoe UI, Tahoma, Arial"),
        FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static FormattedText Text(string s, double size, Brush ink, double ppd,
                                     TextAlignment align = TextAlignment.Center, double maxWidth = 0) =>
        // Fully qualified: System.Windows.Forms is referenced app-wide (the tray icon) and has its own
        // FlowDirection.
        new(s, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, Face, size, ink, ppd)
        { TextAlignment = align, MaxTextWidth = maxWidth > 0 ? maxWidth : 0 };

    /// <summary>Draw <paramref name="text"/> centred on its ink at <paramref name="centre"/>. A FormattedText's
    /// layout box carries bearing and ascender space, which differs between digits, capitals and glyphs, so
    /// only the glyph geometry's bounds centre what is actually drawn. Nothing is drawn for text with no
    /// ink.</summary>
    public static void DrawInkCentered(DrawingContext dc, FormattedText text, Point centre) =>
        DrawInkCentered(dc, text, centre, text.BuildGeometry(new Point()).Bounds);

    /// <param name="ink">The text's glyph-geometry bounds, from a caller that already measured them.</param>
    public static void DrawInkCentered(DrawingContext dc, FormattedText text, Point centre, Rect ink)
    {
        if (ink.IsEmpty) return;
        dc.DrawText(text, new Point(centre.X - ink.X - ink.Width / 2, centre.Y - ink.Y - ink.Height / 2));
    }

    /// <summary>Draw text centred horizontally on <paramref name="cx"/> with its top at
    /// <paramref name="top"/>. Returns the height drawn, so callers can stack.</summary>
    public static double DrawCentered(DrawingContext dc, string s, double size, Brush ink,
                                      double cx, double top, double ppd, double maxWidth = 0)
    {
        if (TryPrompt(s, out string button, out string verb))
        {
            Size box = PromptSize(button, verb, size, ppd);
            DrawPrompt(dc, button, verb, size, ink, cx - box.Width / 2, top, ppd);
            return box.Height;
        }
        var ft = Text(s, size, ink, ppd, TextAlignment.Center, maxWidth);
        // Centred on the ink, not the layout box: with no MaxTextWidth a centre-aligned FormattedText's
        // glyphs do not sit where `Width` implies, and the two drift apart by a fraction of the line.
        // Measuring the geometry gives one box for both (the same move Kabloom's plaques make).
        Rect ink_ = ft.BuildGeometry(new Point()).Bounds;
        double x = ink_.IsEmpty ? cx - ft.Width / 2 : cx - (ink_.X + ink_.Width / 2);
        dc.DrawText(ft, new Point(x, top));
        return ft.Height;
    }

    // ── Button prompts ────────────────────────────────────────────────────────
    // "<button>  <verb>" — a face button's text, two spaces, what it does. Every prompt in the arcade is
    // written that way (TestHarness glyphs enforces the token), so the button can be recognised here and set
    // on a badge: a disc in a colour contrasting the ink, ringed in the ink, the glyph in the ink on it.

    private static readonly PadButton[] FaceButtons =
        [PadButton.Cross, PadButton.Circle, PadButton.Square, PadButton.Triangle];

    /// <summary>Split a prompt into its button text and its verb; false for any other string.</summary>
    public static bool TryPrompt(string s, out string button, out string verb)
    {
        button = verb = "";
        int i = s.IndexOf("  ", StringComparison.Ordinal);
        if (i <= 0 || i + 2 >= s.Length) return false;
        string head = s[..i];
        // START is a word, not a glyph, but it is a button all the same: it badges as a pill so the footer reads
        // as two buttons, not a button and a caption.
        if (head == StartWord) { button = head; verb = s[(i + 2)..]; return true; }
        foreach (PadButton b in FaceButtons)
        {
            if (!string.Equals(ControllerButtons.Text(b), head, StringComparison.Ordinal)) continue;
            button = head;
            verb = s[(i + 2)..];
            return true;
        }
        return false;
    }

    /// <summary>Badge diameter for a prompt of <paramref name="size"/>; the verb sits <see cref="PromptGap"/>
    /// of the size after it.</summary>
    private static double BadgeDiameter(double size) => size * 1.55;
    private const string StartWord = "START";
    /// <summary>A badge is a disc for a single glyph and a pill for a word — START, and the shoulder labels
    /// (L1 / LB) the window-move cues carry.</summary>
    private static bool IsWord(string button) => button.Length > 1;
    /// <summary>A word pill's width: its word at <see cref="WordBadgeScale"/> of the size plus the side padding.</summary>
    private static double BadgeWidth(string button, double size, Brush ink, double ppd) =>
        IsWord(button) ? BadgeGlyph(button, size * WordBadgeScale, ink, ppd).Ink.Width + size * 0.9 : BadgeDiameter(size);

    /// <summary>A badge's height — a disc's diameter, and a pill's height, so a caller laying out its own row
    /// of badges (the arcade's window-move cues) can size a plate around one.</summary>
    public static double BadgeHeight(double size) => BadgeDiameter(size);
    private const double WordBadgeScale = 0.62;
    private const double PromptGap = 0.40;

    /// <summary>The box a prompt occupies: badge, gap and verb across; the taller of badge and verb down.</summary>
    public static Size PromptSize(string button, string verb, double size, double ppd)
    {
        var ft = PromptVerb(verb, size, Ink, ppd);
        double d = BadgeDiameter(size), w = BadgeWidth(button, size, Ink, ppd);
        return new Size(w + size * PromptGap + ft.Width, Math.Max(d, ft.Height));
    }

    /// <summary>Draw a prompt with its top-left at (<paramref name="left"/>, <paramref name="top"/>): the
    /// button on its badge, then the verb, both centred on one line. Returns the box drawn.</summary>
    public static Size DrawPrompt(DrawingContext dc, string button, string verb, double size, Brush ink,
                                  double left, double top, double ppd)
    {
        Size box = PromptSize(button, verb, size, ppd);
        double d = BadgeDiameter(size), w = BadgeWidth(button, size, ink, ppd);
        DrawBadge(dc, button, size, ink, new Point(left + w / 2, top + box.Height / 2), d, ppd);
        var ft = PromptVerb(verb, size, ink, ppd);
        dc.DrawText(ft, new Point(left + w + size * PromptGap, top + (box.Height - ft.Height) / 2));
        return box;
    }

    /// <summary>Prompt text laid out once per (string, size, ink, dpi). The △ how-to hint draws under live
    /// play on every frame until a game's card has been opened, and every pause-menu row and card footer goes
    /// through <see cref="DrawCenteredRow"/>, which measures and then draws — two layouts per part per frame.
    /// The strings are a handful of fixed labels, so the table stays small; capped in case a locale grows it.</summary>
    private static readonly Dictionary<(string, int, Brush, int), FormattedText> PromptTexts = new();

    private static FormattedText PromptVerb(string verb, double size, Brush ink, double ppd)
    {
        var key = (verb, (int)Math.Round(size * 4), ink, (int)Math.Round(ppd * 100));
        if (PromptTexts.TryGetValue(key, out var hit)) return hit;
        var ft = Text(verb, size, ink, ppd, TextAlignment.Left);
        if (PromptTexts.Count >= 512) PromptTexts.Clear();
        PromptTexts[key] = ft;
        return ft;
    }

    /// <summary>A badge's glyph with its ink bounds — the bounds cost a glyph-geometry build, once per key
    /// rather than once per frame.</summary>
    private static readonly Dictionary<(string, int, Brush, int), (FormattedText Text, Rect Ink)> BadgeGlyphs = new();

    private static (FormattedText Text, Rect Ink) BadgeGlyph(string button, double size, Brush ink, double ppd)
    {
        var key = (button, (int)Math.Round(size * 4), ink, (int)Math.Round(ppd * 100));
        if (BadgeGlyphs.TryGetValue(key, out var hit)) return hit;
        var glyph = Text(button, size * 0.95, ink, ppd, TextAlignment.Left);
        hit = (glyph, glyph.BuildGeometry(new Point()).Bounds);
        if (BadgeGlyphs.Count >= 512) BadgeGlyphs.Clear();
        BadgeGlyphs[key] = hit;
        return hit;
    }

    private static readonly Dictionary<(Brush, int), Pen> BadgeRings = new();

    private static Pen BadgeRing(Brush ink, double thickness)
    {
        var key = (ink, (int)Math.Round(thickness * 4));
        if (BadgeRings.TryGetValue(key, out var hit)) return hit;
        var ring = new Pen(ink, Math.Max(0.25, key.Item2 / 4.0));
        ring.Freeze();
        if (BadgeRings.Count >= 256) BadgeRings.Clear();
        BadgeRings[key] = ring;
        return ring;
    }

    /// <summary>The badge itself: a disc of <paramref name="diameter"/> contrasting the ink and ringed in it
    /// (a light prompt gets a dark disc, a dark one — Kabloom's plaques — a pale disc), the button's glyph
    /// centred on it by its ink bounds rather than its layout box, since the four shapes and the four letters
    /// carry very different side bearings.</summary>
    public static void DrawBadge(DrawingContext dc, string button, double size, Brush ink, Point centre,
                                 double diameter, double ppd)
    {
        double r = diameter / 2;
        bool lightInk = LightInk(ink);
        var ring = BadgeRing(ink, Math.Max(1, size * 0.08));
        if (IsWord(button))
        {
            // A pill, ringed like the discs, wide enough for the word.
            double w = BadgeWidth(button, size, ink, ppd), hr = r - ring.Thickness / 2;
            var pill = new Rect(centre.X - w / 2 + ring.Thickness / 2, centre.Y - hr, w - ring.Thickness, hr * 2);
            dc.DrawRoundedRectangle(lightInk ? BadgeDark : BadgeLight, ring, pill, hr, hr);
            var (word, wb) = BadgeGlyph(button, size * WordBadgeScale, ink, ppd);
            DrawInkCentered(dc, word, centre, wb);
            return;
        }
        dc.DrawEllipse(lightInk ? BadgeDark : BadgeLight, ring, centre, r - ring.Thickness / 2, r - ring.Thickness / 2);

        var (glyph, gb) = BadgeGlyph(button, size, ink, ppd);
        DrawInkCentered(dc, glyph, centre, gb);
    }

    /// <summary>The dark plate a light ink sits on, for a caller picking its own (see <see cref="DrawShoulderChip"/>).</summary>
    public static Brush BadgePlate => BadgeDark;

    /// <summary>Whether a brush is light enough that a badge under it needs a dark plate.</summary>
    private static bool LightInk(Brush ink) =>
        ink is not SolidColorBrush sc || 0.299 * sc.Color.R + 0.587 * sc.Color.G + 0.114 * sc.Color.B > 128;

    /// <summary>A shoulder chip's proportions — a real bumper is a wide rounded rectangle, not a stadium, so
    /// this one is too; <c>ControllerButtons.DrawPill</c> sets the same 0.32 corner on the app's own chips.</summary>
    private const double ShoulderAspect = 1.7;
    private const double ShoulderCorner = 0.32;
    /// <summary>How much tighter the chip is than a face badge's disc. It crops the plate only — the label
    /// keeps <see cref="WordBadgeScale"/>, so the chip closes in on its text rather than shrinking with it.</summary>
    private const double ShoulderPlate = 0.72;

    /// <summary>A shoulder chip's width for a badge of <paramref name="size"/>.</summary>
    public static double ShoulderChipWidth(double size) => BadgeHeight(size) * ShoulderPlate * ShoulderAspect;

    /// <summary>The shoulder button as a chip — the badge look (ink on a plate, ringed in the ink) at a
    /// bumper's proportions, carrying L1 / LB. Drawn by the window-move cues on the bezel.
    ///
    /// <para>The <paramref name="plate"/> is the caller's, not auto-contrasted off the ink like
    /// <see cref="DrawBadge"/>'s: a chip that is deliberately dimmed has to keep its dark plate, and
    /// auto-contrast would flip it pale exactly as the ink goes down.</para></summary>
    public static void DrawShoulderChip(DrawingContext dc, string label, double size, Brush ink, Brush plate,
                                        Point centre, double ppd)
    {
        double h = BadgeHeight(size) * ShoulderPlate, w = ShoulderChipWidth(size);
        var ring = BadgeRing(ink, Math.Max(1, size * 0.08));
        var box = new Rect(centre.X - w / 2 + ring.Thickness / 2, centre.Y - h / 2 + ring.Thickness / 2,
                           w - ring.Thickness, h - ring.Thickness);
        double corner = h * ShoulderCorner;
        dc.DrawRoundedRectangle(plate, ring, box, corner, corner);
        var (word, wb) = BadgeGlyph(label, size * WordBadgeScale, ink, ppd);
        DrawInkCentered(dc, word, centre, wb);
    }

    // ── Inline badges in running text ─────────────────────────────────────────
    // A how-to sentence may carry {cross} and friends mid-line. A FormattedText cannot hold a drawing, so the
    // token is replaced by a run of no-break spaces wide enough for a badge (no-break, so the badge can never
    // straddle a wrap), the text is laid out as usual, and the badge is drawn afterwards over the run wherever
    // the wrap put it. The badge is taller than the line and simply overlaps its neighbours' line boxes —
    // the line height is never grown for it.

    /// <summary>A badge's slot in a laid-out string: the run it replaced, and the button to draw there.</summary>
    public readonly record struct InlineBadge(int Index, int Length, string Button);

    private static readonly (string Token, PadButton Button)[] Tokens =
        [("{cross}", PadButton.Cross), ("{circle}", PadButton.Circle), ("{square}", PadButton.Square), ("{triangle}", PadButton.Triangle)];

    /// <summary>Inline badges draw at this share of a prompt badge: a little under the line's own height
    /// plus the overlap, so a sentence keeps reading as a sentence.</summary>
    private const double InlineBadgeShare = 0.88;

    /// <summary>Swap every button token in <paramref name="s"/> for a no-break run the badge will cover, and
    /// record where each landed. Lay the result out at <paramref name="size"/> and hand it, with the slots,
    /// to <see cref="DrawInlineBadges"/>.</summary>
    public static string ReserveBadges(string s, double size, double ppd, List<InlineBadge> slots)
    {
        if (!s.Contains('{')) return s;
        // Two spaces of slack: a badge is wider than the run of blanks that stands for it, so the surrounding
        // words are held off it on both sides.
        double d = BadgeDiameter(size) * InlineBadgeShare;
        double nbsp = Math.Max(0.5, Text(" ", size, Ink, ppd, TextAlignment.Left).WidthIncludingTrailingWhitespace);
        int count = Math.Max(1, (int)Math.Ceiling((d + size * 0.5) / nbsp));
        string run = new(' ', count);

        var sb = new System.Text.StringBuilder(s.Length + 16);
        int i = 0;
        while (i < s.Length)
        {
            bool hit = false;
            foreach (var (token, button) in Tokens)
            {
                if (string.CompareOrdinal(s, i, token, 0, token.Length) != 0) continue;
                slots.Add(new InlineBadge(sb.Length, count, ControllerButtons.Text(button)));
                sb.Append(run);
                i += token.Length;
                hit = true;
                break;
            }
            if (!hit) sb.Append(s[i++]);
        }
        return sb.ToString();
    }

    /// <summary>Draw the badges reserved by <see cref="ReserveBadges"/> over <paramref name="ft"/>, which
    /// was drawn at <paramref name="origin"/>. Each badge is centred on the box its run occupies after
    /// wrapping.</summary>
    public static void DrawInlineBadges(DrawingContext dc, FormattedText ft, Point origin,
                                        IReadOnlyList<InlineBadge> slots, double size, Brush ink, double ppd)
    {
        if (slots.Count == 0) return;
        double d = BadgeDiameter(size) * InlineBadgeShare;
        foreach (var slot in slots)
        {
            Geometry? run = ft.BuildHighlightGeometry(origin, slot.Index, slot.Length);
            if (run is null || run.Bounds.IsEmpty) continue;
            Rect b = run.Bounds;
            DrawBadge(dc, slot.Button, size, ink, new Point(b.X + b.Width / 2, b.Y + b.Height / 2), d, ppd);
        }
    }

    private static readonly Brush BadgeDark  = B(0x0A, 0x0D, 0x14);
    private static readonly Brush BadgeLight = B(0xEC, 0xF1, 0xF7);

    /// <summary>The width and height <see cref="DrawCenteredRow"/> would occupy, without drawing. A caller
    /// placing the row inside the round window needs this: the disc narrows towards its edge, so how low a
    /// row may sit depends on how wide it is — and its width is the translation's, not the author's.</summary>
    public static Size MeasureCenteredRow(string[] parts, double size, double ppd, double gutterEm = 1.5)
    {
        double gutter = size * gutterEm, total = gutter * (parts.Length - 1), h = 0;
        foreach (var part in parts)
        {
            var box = TryPrompt(part, out string b, out string v)
                ? PromptSize(b, v, size, ppd)
                : Text(part, size, Ink, ppd, TextAlignment.Left) is var ft ? new Size(ft.Width, ft.Height) : default;
            total += box.Width;
            h = Math.Max(h, box.Height);
        }
        return new Size(total, h);
    }

    /// <summary>A prompt row — several short parts laid out with a fixed gutter between them, centred on
    /// <paramref name="cx"/>. The gutter is layout here, not spaces inside the strings, so each part stays a
    /// bare phrase and the columns survive a language whose words run longer or read right-to-left.</summary>
    public static double DrawCenteredRow(DrawingContext dc, string[] parts, double size, Brush ink,
                                         double cx, double top, double ppd, double gutterEm = 1.5)
    {
        double gutter = size * gutterEm, total = gutter * (parts.Length - 1), h = 0;
        var boxes = new Size[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            boxes[i] = TryPrompt(parts[i], out string b, out string v)
                ? PromptSize(b, v, size, ppd)
                : Text(parts[i], size, ink, ppd, TextAlignment.Left) is var ft ? new Size(ft.Width, ft.Height) : default;
            total += boxes[i].Width;
            h = Math.Max(h, boxes[i].Height);
        }
        double x = cx - total / 2;
        for (int i = 0; i < parts.Length; i++)
        {
            // Every part is centred on the row's height, so a badge and a bare phrase share one baseline band.
            double y = top + (h - boxes[i].Height) / 2;
            if (TryPrompt(parts[i], out string b, out string v)) DrawPrompt(dc, b, v, size, ink, x, y, ppd);
            else dc.DrawText(Text(parts[i], size, ink, ppd, TextAlignment.Left), new Point(x, y));
            x += boxes[i].Width + gutter;
        }
        return h;
    }

    // ── Shouts ────────────────────────────────────────────────────────────────

    /// <summary>How a shout's arrival ring is painted. <see cref="Quantised"/> takes the round-capped
    /// <see cref="ArcadePalette.Stroke"/> cache (Petalpop, Internode); <see cref="Exact"/> takes the exact alpha
    /// with a butt-capped pen and a slightly fainter, heavier ring (Connate).</summary>
    public enum ShoutPaint { Quantised, Exact }

    /// <summary>One big poster-lettered word over a board — a shockwave ring on arrival, then the word as
    /// <see cref="Poster"/> text in <paramref name="ink"/>, centred on its face at <paramref name="centre"/>.
    /// Every size is a fraction of <paramref name="scale"/>, the field radius. <paramref name="age"/> ≥ 1
    /// suppresses the ring, which a second line of the same shout must not draw again. <paramref name="hud"/>
    /// takes the HUD text scale, for a shout over a field nothing is being played on.
    ///
    /// <para>The lettering is built once at the shout's resting size; <paramref name="pop"/> and
    /// <paramref name="alpha"/> are applied to the cached drawing per frame, so an animating shout builds no
    /// geometry.</para></summary>
    public static void DrawShout(DrawingContext dc, Point centre, double scale, string message, Color ink,
                                 double age, double pop, double alpha, double sizeFraction, double ppd,
                                 ShoutPaint paint, bool hud = false)
    {
        bool exact = paint == ShoutPaint.Exact;
        double burst = Math.Clamp(age / 0.22, 0, 1);
        if (burst < 1)
        {
            double r = scale * (0.16 + burst * 0.42);
            if (exact)
                dc.DrawEllipse(null, ArcadePalette.ExactPen(ink, (byte)(200 * (1 - burst)), Math.Max(2, scale * 0.020 * (1 - burst))),
                               centre, r, r);
            else DrawRing(dc, centre, r, scale * 0.020 * (1 - burst), ink, 0.8 * (1 - burst));
        }

        double size = Math.Max(12, scale * sizeFraction);
        PosterText poster = Poster(message, hud ? Ui(size) : size, ink, ppd, maxWidth: scale * ShoutMaxBody);
        DrawPosterCentred(dc, poster, centre, pop, alpha);
    }

    /// <summary>The widest a shout's body (face plus hatching) may be, in field radii; a longer word shrinks to it.</summary>
    private const double ShoutMaxBody = 0.95;

    // ── Poster text ───────────────────────────────────────────────────────────

    /// <summary>A word set as poster lettering: bold geometric caps in a solid face colour, a thin rule round
    /// the glyphs, and a depth extruded down-left that is drawn only as diagonal hatch lines in a darker shade.
    /// Built once into a frozen <see cref="Drawing"/> in glyph space (origin at the text's layout origin).</summary>
    /// <param name="Face">The glyph geometry's bounds — the ink the lettering is centred and placed by.</param>
    /// <param name="Body">The face plus its extrusion — what must clear anything beside it.</param>
    public sealed record PosterText(Drawing Drawing, Rect Face, Rect Body);

    /// <summary>The rule round the face when a caller passes none.</summary>
    private static readonly Color PosterRuleDefault = Color.FromRgb(0xF2, 0xEE, 0xE3);
    /// <summary>Each channel of a face colour that has no explicit shade is scaled by this to get its shade.</summary>
    private const double PosterShadeFactor = 0.72;
    private const int PosterCacheLimit = 64;
    private static readonly Dictionary<(string, double, Color, Color, double, double, Color), PosterText> _posters = [];

    /// <summary>The geometric bold face: falls back through Windows' own heavy faces. A comma list, so a machine
    /// missing one lands on the next rather than on a default.</summary>
    private static Typeface? _posterFace;
    private static Typeface PosterFace => _posterFace ??= new Typeface(
        new FontFamily("Century Gothic, Futura, Segoe UI"),
        FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    /// <summary>The shade a face colour takes when none is given: each channel × <see cref="PosterShadeFactor"/>.</summary>
    public static Color PosterShade(Color face) => Color.FromRgb(
        (byte)Math.Round(face.R * PosterShadeFactor), (byte)Math.Round(face.G * PosterShadeFactor),
        (byte)Math.Round(face.B * PosterShadeFactor));

    /// <summary>A brush's colour, for the poster entry points; a non-solid brush reads as white.</summary>
    public static Color ColourOf(Brush brush) =>
        brush is SolidColorBrush s ? Color.FromRgb(s.Color.R, s.Color.G, s.Color.B) : Colors.White;

    /// <summary>The cached poster lettering for <paramref name="text"/> at <paramref name="size"/> (em height in
    /// device-independent pixels). <paramref name="maxWidth"/> &gt; 0 shrinks the size until the body fits that
    /// width. The cache is keyed on everything that shapes the drawing and is emptied when it fills, so a
    /// shout that changes text each frame cannot grow it.</summary>
    public static PosterText Poster(string text, double size, Color face, double ppd, Color? shade = null,
                                    double maxWidth = 0, Color? rule = null)
    {
        face = Color.FromRgb(face.R, face.G, face.B);
        Color shaded = shade ?? PosterShade(face);
        Color ruled = rule ?? PosterRuleDefault;
        var key = (text, Math.Round(size, 2), face, shaded, ppd, Math.Round(maxWidth, 1), ruled);
        if (_posters.TryGetValue(key, out var hit)) return hit;
        PosterText built = BuildPoster(text, size, face, shaded, ppd, ruled);
        if (maxWidth > 0 && built.Body.Width > maxWidth)
            built = BuildPoster(text, size * maxWidth / built.Body.Width, face, shaded, ppd, ruled);
        if (_posters.Count >= PosterCacheLimit) _posters.Clear();
        _posters[key] = built;
        return built;
    }

    private static PosterText BuildPoster(string message, double size, Color face, Color shade, double ppd, Color rule)
    {
        var text = new FormattedText(message, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                                     PosterFace, size, Brushes.Black, ppd);
        Geometry glyphs = text.BuildGeometry(new Point());
        Rect faceBounds = glyphs.Bounds;
        var group = new DrawingGroup();
        if (faceBounds.IsEmpty) { group.Freeze(); return new PosterText(group, Rect.Empty, Rect.Empty); }

        double depth = size * 0.16;         // extrusion, down-left
        Vector dir = new(-0.55, 1.0);

        // The extrusion's footprint: the glyphs swept along `dir` by `depth`, as a union of copies about a
        // pixel apart. Only its hatching is drawn — the lines alone carry the depth.
        int steps = Math.Max(2, (int)Math.Ceiling(depth));
        Geometry block = glyphs;
        for (int s = 1; s <= steps; s++)
        {
            var copy = glyphs.Clone();
            copy.Transform = new TranslateTransform(dir.X * depth * s / steps, dir.Y * depth * s / steps);
            block = Geometry.Combine(block, copy, GeometryCombineMode.Union, null);
        }
        block.Freeze();
        Rect body = block.Bounds;

        using (var g = group.Open())
        {
            Pen Flat(Color c, double w)
            {
                var brush = new SolidColorBrush(c);
                brush.Freeze();
                var p = new Pen(brush, w) { LineJoin = PenLineJoin.Miter };
                p.Freeze();
                return p;
            }

            // The extrusion: diagonal hatching clipped to the footprint, and nothing else.
            g.PushClip(block);
            double hatchPitch = Math.Max(2, size * 0.045);
            Pen hatch = Flat(shade, Math.Max(0.8, size * 0.018));
            for (double x = body.Left - body.Height; x < body.Right; x += hatchPitch)
                g.DrawLine(hatch, new Point(x, body.Bottom), new Point(x + body.Height, body.Top));
            g.Pop();

            // The face: a thin rule parting it from the hatching, then the solid colour on top so only the
            // rule's outer half shows.
            var fill = new SolidColorBrush(face);
            fill.Freeze();
            g.DrawGeometry(null, Flat(rule, size * 0.06), glyphs);
            g.DrawGeometry(fill, null, glyphs);
        }
        group.Freeze();
        return new PosterText(group, faceBounds, body);
    }

    /// <summary>Draw <paramref name="poster"/> with its glyph-space origin moved to <paramref name="origin"/>.</summary>
    public static void DrawPoster(DrawingContext dc, PosterText poster, Point origin)
    {
        if (poster.Face.IsEmpty) return;
        dc.PushTransform(new TranslateTransform(origin.X, origin.Y));
        dc.DrawDrawing(poster.Drawing);
        dc.Pop();
    }

    /// <summary>Draw <paramref name="poster"/> with its face centred on <paramref name="centre"/>, scaled by
    /// <paramref name="scale"/> about that point and faded as one layer by <paramref name="alpha"/> (the rule,
    /// face and hatching overlap, so a per-brush fade would show the seams).</summary>
    public static void DrawPosterCentred(DrawingContext dc, PosterText poster, Point centre, double scale = 1, double alpha = 1)
    {
        if (poster.Face.IsEmpty || alpha <= 0.004) return;
        bool fade = alpha < 0.996;
        if (fade) dc.PushOpacity(alpha);
        dc.PushTransform(new ScaleTransform(scale, scale, centre.X, centre.Y));
        DrawPoster(dc, poster, new Point(centre.X - poster.Face.X - poster.Face.Width / 2,
                                         centre.Y - poster.Face.Y - poster.Face.Height / 2));
        dc.Pop();
        if (fade) dc.Pop();
    }

    /// <summary>An end-card headline in poster lettering in <paramref name="ink"/>, centred on
    /// <paramref name="cx"/>, shrunk to <paramref name="maxWidth"/> if it would run wider. Placed where a
    /// <see cref="DrawCentered"/> line of the same <paramref name="size"/> and <paramref name="top"/> would put
    /// its caps. Returns the height of that line, so callers can stack.</summary>
    public static double DrawPosterLine(DrawingContext dc, string s, double size, Brush ink, double cx, double top,
                                        double ppd, double maxWidth, Color? rule = null)
    {
        PosterText poster = Poster(s, size, ColourOf(ink), ppd, maxWidth: maxWidth, rule: rule);
        DrawPosterCentred(dc, poster, new Point(cx, top + size * PosterCapCentre));
        return size * 1.2;
    }

    /// <summary>Where the caps of a <see cref="Face"/> line centre below its layout top, in em.</summary>
    private const double PosterCapCentre = 0.6;

    /// <summary>A fading ring from the quantised <see cref="ArcadePalette.Stroke"/> cache, at least a pixel
    /// wide; skipped once it is too faint or too thin to see.</summary>
    public static void DrawRing(DrawingContext dc, Point p, double radius, double width, Color colour, double alpha)
    {
        if (alpha <= 0.01 || width <= 0.2) return;
        dc.DrawEllipse(null, ArcadePalette.Stroke(colour, Math.Max(1, width), alpha), p, radius, radius);
    }

    // ── Plates ────────────────────────────────────────────────────────────────

    /// <summary>A HUD plate that hangs in from the top of the playfield: <paramref name="body"/> is the part meant
    /// to be seen, and the plate is extended <paramref name="above"/> past its top so the top edge and corners fall
    /// outside the disc (the field clip takes them) and the plate reads as hanging rather than floating. The
    /// corner radius follows the body, so the visible bottom corners keep their curve at any hang.
    /// <paramref name="radiusHeight"/> pins the radius to a height other than the body's own, so a plate that
    /// grows a temporary row keeps the corners of its resting size.</summary>
    public static Rect DrawHangingPlate(DrawingContext dc, Rect body, double above, Brush fill, Pen pen, double radiusHeight = 0)
    {
        var plate = new Rect(body.X, body.Y - above, body.Width, body.Height + above);
        double radius = Math.Min((radiusHeight > 0 ? radiusHeight : body.Height) * 0.30, plate.Width / 2);
        dc.DrawRoundedRectangle(fill, pen, plate, radius, radius);
        return plate;
    }

    /// <summary>How far past the rim a hanging plate extends, in field radii — enough that no top corner shows
    /// at any plate width the HUDs reach.</summary>
    public const double PlateHang = 0.20;

    // ── The frame ─────────────────────────────────────────────────────────────

    /// <summary>Bezel + playfield, drawn under everything. Returns the playfield radius — the radius games
    /// lay themselves out in, which is the window radius less the bezel.</summary>
    public static double DrawFrame(DrawingContext dc, Point c, double radius, Brush accent, bool fieldCovered = false)
    {
        double bezel = radius * BezelFrac;
        double field = radius - bezel;

        // Outer ring first, then the field on top, so the ring's inner edge is clean at any DPI.
        dc.DrawEllipse(BezelBody, BezelPen, c, radius - BezelPen.Thickness / 2, radius - BezelPen.Thickness / 2);
        // A game that paints its own opaque ground over the whole field (IArcadeRenderer.CoversField) gets
        // neither the field fill nor the vignette: both landed under its ground, unseen, every frame.
        if (fieldCovered) return field;
        dc.DrawEllipse(Field, null, c, field, field);

        // Vignette: a soft dark falloff toward the rim so the disc reads as a bowl rather than a sticker.
        dc.DrawEllipse(Vignette, null, c, field, field);
        return field;
    }

    /// <summary>Every coordinate is relative to the bounding box (the default mapping mode), so one frozen
    /// brush fits every radius.</summary>
    private static readonly RadialGradientBrush Vignette = BuildVignette();

    private static RadialGradientBrush BuildVignette()
    {
        var vig = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center         = new Point(0.5, 0.5),
            RadiusX = 0.5, RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55),
                new GradientStop(Color.FromArgb(0x66, 0, 0, 0), 0.88),
                new GradientStop(Color.FromArgb(0xB4, 0, 0, 0), 1.0),
            },
        };
        vig.Freeze();
        return vig;
    }

    // ── The guard card ────────────────────────────────────────────────────────

    /// <summary>The "not while your controller isn't isolated" card, drawn inside the round window so it
    /// reads as part of the arcade rather than as an error dialog. Copy comes from
    /// <see cref="Arcade.Guard"/> so the card and the Help topic can't drift apart.
    ///
    /// <para><paramref name="overrideProgress"/> 0..1 fills a ring as △ is held (the play-anyway escape).
    /// Pass a negative value when no override is offered.</para></summary>
    public static void DrawGuard(DrawingContext dc, Point c, double radius, Arcade.GuardCopy copy,
                                 double overrideProgress, double ppd)
    {
        dc.DrawEllipse(Scrim, null, c, radius, radius);

        double w  = radius * 1.50;                 // a chord across the disc, not the full diameter
        double top = c.Y - radius * 0.62;
        double gapTitle = radius * 0.05, gapBody = radius * 0.04;

        // The band between the first line and the footer is fixed, so the three blocks are measured first
        // and, when they would run under the footer, scaled down together — a long game name or a language
        // that runs long must not push the last line out of the card. Scaling all three keeps the hierarchy.
        double titleSize = Ui(Math.Max(13, radius * 0.085));
        double bodySize  = Ui(Math.Max(10, radius * 0.058));
        double room = c.Y + radius * 0.62 - radius * 0.06 - top;
        for (int i = 0; i < 10; i++)
        {
            double h = Text(copy.Title, titleSize, Ink, ppd, TextAlignment.Center, w).Height + gapTitle
                     + Text(copy.Body,  bodySize,  Ink, ppd, TextAlignment.Center, w).Height + gapBody
                     + Text(copy.Fix,   bodySize,  Ink, ppd, TextAlignment.Center, w).Height;
            if (h <= room) break;
            titleSize *= 0.92; bodySize *= 0.92;
        }

        double y = top;
        y += DrawCentered(dc, copy.Title, titleSize, Ink, c.X, y, ppd, w) + gapTitle;
        y += DrawCentered(dc, copy.Body, bodySize, InkDim, c.X, y, ppd, w) + gapBody;
        y += DrawCentered(dc, copy.Fix, bodySize, Ink, c.X, y, ppd, w);

        // Footer: how to leave, and (if offered) how to override.
        // ⚠ Never spell a glyph out — on an Xbox pad a literal would make the one card whose whole job is
        // explaining a blocked feature name two buttons the reader does not have.
        double footY = c.Y + radius * 0.62;
        string close = $"{ControllerButtons.Text(PadButton.Circle)}  {Loc.T(UiText.Arcade.Close)}";
        if (overrideProgress >= 0)
            DrawCenteredRow(dc, [close, $"{ControllerButtons.Text(PadButton.Triangle)}  {Loc.T(UiText.Arcade.HoldToPlayAnyway)}"],
                            Ui(Math.Max(9, radius * 0.05)), InkFaint, c.X, footY, ppd);
        else
            DrawCentered(dc, close, Ui(Math.Max(9, radius * 0.05)), InkFaint, c.X, footY, ppd, w);

        if (overrideProgress > 0)
        {
            // Progress arc around the rim — the same "hold is happening" language the wheel's guarded
            // slices use, so the gesture doesn't need explaining twice.
            double rr = radius * 0.92;
            var fig = new PathFigure { StartPoint = new Point(c.X, c.Y - rr) };
            double sweep = Math.Min(1, overrideProgress) * Math.PI * 2;
            // Two half-arcs: a single ArcSegment can't express a sweep of ≥180° unambiguously.
            fig.Segments.Add(new ArcSegment(
                new Point(c.X + Math.Sin(sweep / 2) * rr, c.Y - Math.Cos(sweep / 2) * rr),
                new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new ArcSegment(
                new Point(c.X + Math.Sin(sweep) * rr, c.Y - Math.Cos(sweep) * rr),
                new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
            var geo = new PathGeometry([fig]);
            geo.Freeze();
            dc.DrawGeometry(null, P(Ink, Math.Max(2, radius * 0.014)), geo);
        }
    }

    /// <summary>The resume beat: the frozen game is already painted underneath, dimmed — so you can see what
    /// you're walking back into instead of being dropped into it.
    ///
    /// <para>Scrim and word fade out together over the tail of the beat
    /// (<see cref="ArcadeTuning.ResumeReadyFadeFrac"/>) rather than switching off at zero. The fade is the
    /// hand-back: the board arrives before control does, so the last thing you see is the game, not the
    /// word.</para></summary>
    public static void DrawReady(DrawingContext dc, Point c, double radius, double secondsLeft, double ppd)
    {
        double total = Math.Max(1e-4, ArcadeTuning.ResumeReadySeconds);
        double fade  = Math.Clamp(ArcadeTuning.ResumeReadyFadeFrac, 0, 1) * total;
        // 1 while held, ramping to 0 across the fade window at the end of the beat.
        double k = fade <= 0 ? 1 : Math.Clamp(secondsLeft / fade, 0, 1);

        dc.DrawEllipse(B(0x06, 0x08, 0x0D, (byte)(0x9E * k)), null, c, radius, radius);
        DrawCentered(dc, Loc.T(UiText.Arcade.Ready), Ui(Math.Max(14, radius * 0.11)),
                     B(0xEC, 0xF1, 0xF7, (byte)(0xFF * k)), c.X, c.Y - radius * 0.14, ppd);
    }
}
