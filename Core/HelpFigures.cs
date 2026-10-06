namespace ControllerWheel;

/// <summary>Ink roles a figure part draws in. Roles, not colours — <c>Core</c> stays WPF-free and the
/// renderer maps each one onto the host surface's palette, so a retone of App.xaml's Ui* colours carries
/// the diagrams with it.</summary>
public enum FigInk { None, Body, Muted, Faint, Surface, Accent, AccentSoft, Warn }

/// <summary>Primitives a figure is built from. Deliberately small: enough for the wheel, box-and-arrow
/// flows, and labelled key caps, and no more — a figure that needs a shape outside this set is a figure
/// that wants a real illustration instead.</summary>
public enum FigShape { Rect, Ellipse, Wedge, Line, Arrow, Text }

/// <summary>Where a text part's X/Y sits relative to the text: the box's top-centre / top-left, or its
/// middle (vertically centred on Y, which is what labels inside a shape need).</summary>
public enum FigAnchor { TopCenter, TopLeft, MiddleCenter }

/// <summary>One drawn element. Coordinates are abstract units with the origin at the top-left, y downward;
/// the renderer scales the whole figure to the reading column (never up past its natural size).
/// <list type="bullet">
/// <item><c>Rect</c> / <c>Ellipse</c> — X,Y,W,H is the bounding box; <c>R</c> is the corner radius.</item>
/// <item><c>Wedge</c> — X,Y is the centre, <c>W</c> the outer radius, <c>R</c> the inner radius,
///   <c>From</c>/<c>Sweep</c> in degrees clockwise from 12 o'clock (the wheel's own convention).</item>
/// <item><c>Line</c> / <c>Arrow</c> — X,Y to X2,Y2; an Arrow puts a head at the X2,Y2 end.</item>
/// <item><c>Text</c> — X,Y is the anchor, <c>W</c> the wrap width. Text wraps on purpose: German runs
///   ~30% longer than English and a figure label can't be allowed to shoot off the canvas.</item>
/// </list></summary>
public sealed record FigPart(FigShape Shape)
{
    public double X { get; init; }
    public double Y { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public double R { get; init; }
    public double From { get; init; }
    public double Sweep { get; init; }
    public string? Text { get; init; }
    public double Size { get; init; } = 11;
    public bool Bold { get; init; }
    public bool Dashed { get; init; }
    public FigInk Ink { get; init; } = FigInk.Muted;
    public FigInk Fill { get; init; } = FigInk.None;
    public FigAnchor Anchor { get; init; } = FigAnchor.TopCenter;

    /// <summary>True for text that must not be translated — button, key, file and code names, which appear on
    /// the hardware or on screen as written (<c>L1 + L2</c>, <c>Ctrl</c>, <c>material.json</c>, <c>90°</c>).
    /// Literal parts are kept out of the translatable string set entirely rather than shipped to a translator
    /// to copy back verbatim, and both renderers lay them out left to right in every language: under a
    /// right-to-left paragraph direction the bidi algorithm would move trailing punctuation to the front
    /// (<c>°90</c>, <c>(…dot(0.6</c>).</summary>
    public bool Literal { get; init; }
}

/// <summary>One illustration: an id a <see cref="HelpBlockKind.Figure"/> block names, and the parts to
/// draw inside a Width×Height canvas.</summary>
public sealed record HelpFigure(string Id, double Width, double Height, FigPart[] Parts)
{
    /// <summary>True for a figure laid out in reading order: steps read one after another, a time or progress
    /// axis, an indented outline such as a folder tree. Such a figure is mirrored for a right-to-left language
    /// so the eye meets step 1, time zero, or the outline's root first. Spatial figures never are: the
    /// hardware (L1 stays on the left whatever the language), the wheel, the input path, a combo written left
    /// to right, and any coordinate system a script uses. Default false — opt in per figure.</summary>
    public bool Sequence { get; init; }

    /// <summary>Whether <see cref="PartsFor"/> reflects this figure for the given direction. The renderers
    /// read it to pick which edge of its box a <see cref="FigAnchor.TopLeft"/> label hugs: the authored left
    /// edge, or the right edge once the drawing is reflected — whatever the label's own script direction.</summary>
    public bool Mirrored(bool rtl) => rtl && Sequence;

    /// <summary>The parts to draw for a reading direction: the authored parts, or — for a
    /// <see cref="Sequence"/> figure in a right-to-left language — every part reflected across the canvas
    /// width. Rect/Ellipse reflect by their left edge, Wedge by negating its clockwise angles about the
    /// centre, Line/Arrow by both endpoints (so an arrow points the other way), Text by its anchor.
    /// Both renderers (the WPF pane and the HTML export) draw from this, so they cannot disagree.</summary>
    public FigPart[] PartsFor(bool rtl)
    {
        if (!Mirrored(rtl)) return Parts;
        double w = Width;
        return Parts.Select(p => p.Shape switch
        {
            FigShape.Rect or FigShape.Ellipse => p with { X = w - p.X - p.W },
            FigShape.Wedge                    => p with { X = w - p.X, From = -(p.From + p.Sweep) },
            FigShape.Line or FigShape.Arrow   => p with { X = w - p.X, X2 = w - p.X2 },
            FigShape.Text                     => p with { X = p.Anchor == FigAnchor.TopLeft ? w - p.X - p.W : w - p.X },
            _                                 => p,
        }).ToArray();
    }
}

/// <summary>The Help tab's illustrations — the diagrams for the mechanics that cost a paragraph each to say
/// in words: spatial ones (where the drivers sit in the input path, the playfield's coordinates) and ordered
/// ones (a move in three beats, a startup order, a progress meter). A figure laid out in reading order sets
/// <see cref="HelpFigure.Sequence"/> and is mirrored for right-to-left languages; the spatial ones keep their
/// geometry in every language. Labels never carry a direction glyph (→): a drawn arrow mirrors with its
/// figure, a neutral glyph inside translated text does not.
///
/// <para>Declared as data here, drawn by the WPF Help pane, so <c>Core</c> keeps no UI dependency and the
/// labels flow through the same English-keyed translation map as the prose (see
/// <see cref="HelpLocalization"/>). A label's English text is its translation key, so editing one drops it
/// back to English rather than mistranslating it.</para>
///
/// <para>⚠ Every figure here must be referenced by a topic. <see cref="HelpLocalization.SourceStrings"/>
/// walks all of them, so an orphaned figure quietly demands translations for labels nobody can see.</para>
///
/// <para>Labels resolve {cross}/{circle}/{square}/{triangle} glyph tokens like body text does; the chord
/// tokens are deliberately not used in figures — they resolve to a phrase ("your chosen chords") that no
/// fixed-width label can hold.</para></summary>
public static class HelpFigures
{
    // ── Authoring shorthands ────────────────────────────────────────────────────
    private static FigPart Box(double x, double y, double w, double h, FigInk ink = FigInk.Muted,
                               FigInk fill = FigInk.Surface, double r = 6, bool dashed = false) =>
        new(FigShape.Rect) { X = x, Y = y, W = w, H = h, R = r, Ink = ink, Fill = fill, Dashed = dashed };

    private static FigPart Disc(double cx, double cy, double rad, FigInk ink = FigInk.Muted,
                                FigInk fill = FigInk.None, bool dashed = false) =>
        new(FigShape.Ellipse) { X = cx - rad, Y = cy - rad, W = rad * 2, H = rad * 2, Ink = ink, Fill = fill, Dashed = dashed };

    private static FigPart Wedge(double cx, double cy, double inner, double outer, double centreDeg,
                                 double sweep, FigInk ink = FigInk.Muted, FigInk fill = FigInk.Faint,
                                 bool dashed = false) =>
        new(FigShape.Wedge)
        {
            X = cx, Y = cy, R = inner, W = outer, From = centreDeg - sweep / 2, Sweep = sweep,
            Ink = ink, Fill = fill, Dashed = dashed,
        };

    private static FigPart Line(double x1, double y1, double x2, double y2, FigInk ink = FigInk.Muted,
                                bool dashed = false) =>
        new(FigShape.Line) { X = x1, Y = y1, X2 = x2, Y2 = y2, Ink = ink, Dashed = dashed };

    /// <summary>A "blocked" mark drawn as two crossed lines — not the cross-button glyph. Kept out of
    /// <see cref="FigShape.Text"/> on purpose: a literal "✕" character is indistinguishable from a button
    /// prompt to <c>T_Glyphs</c>' source scan, and this mark never means "press the cross button".</summary>
    private static IEnumerable<FigPart> Cross(double cx, double cy, double rad, FigInk ink = FigInk.Warn) =>
    [
        Line(cx - rad, cy - rad, cx + rad, cy + rad, ink),
        Line(cx - rad, cy + rad, cx + rad, cy - rad, ink),
    ];

    private static FigPart Arrow(double x1, double y1, double x2, double y2, FigInk ink = FigInk.Muted,
                                 bool dashed = false) =>
        new(FigShape.Arrow) { X = x1, Y = y1, X2 = x2, Y2 = y2, Ink = ink, Dashed = dashed };

    private static FigPart Txt(double x, double y, string text, double w, FigInk ink = FigInk.Body,
                               double size = 11, FigAnchor anchor = FigAnchor.TopCenter, bool bold = false) =>
        new(FigShape.Text) { X = x, Y = y, Text = text, W = w, Ink = ink, Size = size, Anchor = anchor, Bold = bold };

    /// <summary>Text that is never translated and always runs left to right: a button, key, file or code
    /// name. See <see cref="FigPart.Literal"/>.</summary>
    private static FigPart Lit(double x, double y, string text, double w, FigInk ink = FigInk.Muted,
                               double size = 10.5, FigAnchor anchor = FigAnchor.TopCenter, bool bold = false) =>
        Txt(x, y, text, w, ink, size, anchor, bold) with { Literal = true };

    /// <summary>A key/button cap: a rounded box with its name centred in it, never translated.</summary>
    private static FigPart[] Cap(double x, double y, double w, double h, string name,
                                 FigInk ink = FigInk.Muted, FigInk fill = FigInk.Surface) =>
    [
        Box(x, y, w, h, ink, fill),
        new(FigShape.Text)
        {
            X = x + w / 2, Y = y + h / 2, Text = name, W = w - 8, Size = 12, Bold = true,
            Ink = FigInk.Body, Anchor = FigAnchor.MiddleCenter, Literal = true,
        },
    ];

    /// <summary>A ring of <paramref name="count"/> evenly spaced slices: one may be armed, one the dashed
    /// target of a move, and one an empty dashed gap where a slice was lifted out.</summary>
    private static IEnumerable<FigPart> Ring(double cx, double cy, double inner, double outer, int count,
                                            int armed = -1, int target = -1, int hole = -1)
    {
        double step = 360.0 / count, sweep = step - 6;
        for (int i = 0; i < count; i++)
        {
            double centre = i * step;
            if (i == armed)       yield return Wedge(cx, cy, inner, outer, centre, sweep, FigInk.Accent, FigInk.AccentSoft);
            else if (i == target) yield return Wedge(cx, cy, inner, outer, centre, sweep, FigInk.Accent, FigInk.None, dashed: true);
            else if (i == hole)   yield return Wedge(cx, cy, inner, outer, centre, sweep, FigInk.Muted, FigInk.None, dashed: true);
            else                  yield return Wedge(cx, cy, inner, outer, centre, sweep);
        }
    }

    /// <summary>One row of a folder tree: a folder or file icon at (x, y) with its name after it. The name is
    /// a file-system name, so it is literal.</summary>
    private static FigPart[] TreeRow(double x, double y, string name, bool file = false, FigInk ink = FigInk.Body) =>
    [
        file ? Box(x + 1.5, y + 1, 10, 13, ink == FigInk.Body ? FigInk.Muted : ink, FigInk.Surface, r: 1.5)
             : Box(x, y + 3, 13, 10, FigInk.Muted, FigInk.Faint, r: 2),
        Lit(x + 19, y, name, 100, ink, 11, FigAnchor.TopLeft),
    ];

    /// <summary>The elbow from a parent row's icon down and across to its child's icon.</summary>
    private static FigPart[] TreeLink(double px, double py, double cx, double cy) =>
    [
        Line(px + 6.5, py + 13, px + 6.5, cy + 8),
        Line(px + 6.5, cy + 8, cx, cy + 8),
    ];

    // ── The figures ─────────────────────────────────────────────────────────────
    // ⚠ The Help reading column is about 375 px wide and a figure only ever scales down to fit it, so a canvas
    // much wider than ~400 units shrinks its labels below legibility. Lay a wide idea out vertically instead.

    private static readonly HelpFigure IsolationModes = new("isolation-modes", 400, 250,
    [
        // Left: isolated. Same four-row skeleton as the right column so the eye reads the difference —
        // the physical pad's route to the game is cut, and a virtual pad stands in for it.
        Txt(70, 4, "Isolated", 130, FigInk.Body, 11.5, bold: true),
        Box(15, 44, 110, 32),
        Txt(70, 60, "Your controller", 100, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(70, 76, 70, 98),
        Box(15, 98, 110, 32, FigInk.Accent, FigInk.AccentSoft),
        Lit(70, 114, "Radiata", 100, FigInk.Body, 11, FigAnchor.MiddleCenter, bold: true),
        Arrow(70, 130, 70, 152),
        Box(15, 152, 110, 32),
        Txt(70, 168, "Virtual pad", 100, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(70, 184, 70, 206),
        Box(15, 206, 110, 32),
        Txt(70, 222, "The game", 100, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Line(125, 60, 150, 60, FigInk.Warn, dashed: true),
        Line(150, 60, 150, 222, FigInk.Warn, dashed: true),
        Line(150, 222, 130, 222, FigInk.Warn, dashed: true),
        Disc(150, 141, 9, FigInk.Warn, FigInk.Surface),
        .. Cross(150, 141, 5),
        Txt(164, 134, "cloaked", 62, FigInk.Warn, 10.5, FigAnchor.TopLeft),
        // Right: no isolation. The virtual-pad row is empty on purpose.
        Txt(290, 4, "Passthru Mode, or no drivers", 190, FigInk.Body, 11.5, bold: true),
        Box(235, 44, 110, 32),
        Txt(290, 60, "Your controller", 100, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(290, 76, 290, 98),
        Box(235, 98, 110, 32, FigInk.Accent, FigInk.AccentSoft),
        Lit(290, 114, "Radiata", 100, FigInk.Body, 11, FigAnchor.MiddleCenter, bold: true),
        Txt(290, 168, "no virtual pad", 110, FigInk.Muted, 10.5, FigAnchor.MiddleCenter),
        Box(235, 206, 110, 32),
        Txt(290, 222, "The game", 100, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Line(345, 60, 372, 60),
        Line(372, 60, 372, 222),
        Arrow(372, 222, 350, 222),
    ]);

    private static readonly HelpFigure EditMove = new("edit-move", 400, 150,
    [
        .. Ring(58, 52, 16, 34, 6, armed: 0),
        Arrow(98, 52, 148, 52),
        // The carried slice rides just outside its target slot, and the slot it left stays an empty gap.
        .. Ring(200, 52, 16, 34, 6, target: 2, hole: 0),
        Wedge(206.1, 55.5, 16, 34, 120, 54, FigInk.Accent, FigInk.AccentSoft),
        Arrow(200, 52, 222.5, 65, FigInk.Accent),
        Arrow(246, 52, 300, 52),
        .. Ring(342, 52, 16, 34, 6, armed: 2),
        Txt(58, 98, "{cross} picks the slice up", 116, FigInk.Body, 11),
        Txt(200, 98, "Aim to the target slot", 116, FigInk.Body, 11),
        Txt(342, 98, "{cross} drops it — the ring reflows", 116, FigInk.Body, 11),
    ]) { Sequence = true };

    private static readonly HelpFigure SingleWheel = new("single-wheel", 400, 268,
    [
        Box(45, 4, 110, 24, r: 12),
        Txt(100, 16, "Its chord", 104, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(100, 28, 100, 44),
        .. Ring(100, 90, 22, 44, 6),
        Txt(100, 142, "Slices on it — this wheel opens.", 180, FigInk.Body, 11),
        // The chord's arrow runs straight through the empty wheel to the game: nothing intercepts it.
        Box(245, 4, 110, 24, r: 12),
        Txt(300, 16, "Its chord", 104, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Disc(300, 90, 44, FigInk.Muted, dashed: true),
        Disc(300, 90, 22, FigInk.Faint, dashed: true),
        Arrow(300, 28, 300, 166, FigInk.Accent),
        Box(250, 166, 100, 28),
        Txt(300, 180, "The game", 94, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Txt(300, 202, "No slices — this wheel draws nothing, so its chord reaches the game.", 190, FigInk.Body, 11),
    ]);

    // Spatial: the caps are in the order the combo string is written (Ctrl+Alt+Del), which is left to right in
    // every language.
    private static readonly HelpFigure KeyComboAnatomy = new("key-combo-anatomy", 300, 110,
    [
        .. Cap(16, 20, 76, 34, "Ctrl"),
        Lit(101, 37, "+", 18, FigInk.Muted, 12, FigAnchor.MiddleCenter),
        .. Cap(110, 20, 76, 34, "Alt"),
        Lit(195, 37, "+", 18, FigInk.Muted, 12, FigAnchor.MiddleCenter),
        .. Cap(204, 20, 76, 34, "Del", FigInk.Accent, FigInk.AccentSoft),
        Line(16, 62, 16, 68),
        Line(16, 68, 186, 68),
        Line(186, 68, 186, 62),
        Txt(101, 74, "Modifiers — held around it", 160, FigInk.Body, 11),
        Line(204, 62, 204, 68, FigInk.Accent),
        Line(204, 68, 280, 68, FigInk.Accent),
        Line(280, 68, 280, 62, FigInk.Accent),
        Txt(242, 74, "The key that's pressed", 100, FigInk.Accent, 11),
    ]);
    private static readonly HelpFigure StartupOrder = new("startup-order", 396, 122,
    [
        Box(4, 4, 80, 50, FigInk.Accent, FigInk.AccentSoft),
        Lit(44, 29, "Radiata", 72, FigInk.Body, 11, FigAnchor.MiddleCenter, bold: true),
        Arrow(86, 29, 104, 29),
        Box(106, 4, 80, 50),
        Txt(146, 29, "Your controller", 72, FigInk.Body, 10.5, FigAnchor.MiddleCenter),
        Arrow(188, 29, 206, 29),
        Box(208, 4, 80, 50),
        Txt(248, 29, "Steam or your launcher", 72, FigInk.Body, 10.5, FigAnchor.MiddleCenter),
        Arrow(290, 29, 308, 29),
        Box(310, 4, 80, 50),
        Txt(350, 29, "The game", 72, FigInk.Body, 10.5, FigAnchor.MiddleCenter),
        Txt(44, 62, "Game and other tools closed", 86, FigInk.Muted, 10),
        Txt(146, 62, "Wait for its status to settle", 100, FigInk.Muted, 10),
    ]) { Sequence = true };

    // Vertical so five steps fit the reading column; the loop back to the restart is the point of the figure.
    private static readonly HelpFigure WorkshopLoop = new("workshop-loop", 400, 224,
    [
        Box(60, 4, 180, 32),
        Txt(150, 20, "Make the folder", 170, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(150, 36, 150, 50),
        Box(60, 50, 180, 32),
        Txt(150, 66, "Write the manifest", 170, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(150, 82, 150, 96),
        Box(60, 96, 180, 32, FigInk.Accent, FigInk.AccentSoft),
        Txt(150, 112, "Restart Radiata", 170, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(150, 128, 150, 142),
        Box(60, 142, 180, 32),
        Txt(150, 158, "Accept the confirmation", 170, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Arrow(150, 174, 150, 188),
        Box(60, 188, 180, 32),
        Txt(150, 204, "Try it out", 170, FigInk.Body, 11, FigAnchor.MiddleCenter),
        Line(240, 204, 268, 204, FigInk.Accent),
        Line(268, 204, 268, 112, FigInk.Accent),
        Arrow(268, 112, 242, 112, FigInk.Accent),
        Txt(278, 150, "Change something", 114, FigInk.Accent, 11, FigAnchor.TopLeft),
    ]) { Sequence = true };

    // Spatial, and never mirrored in any language: these are the coordinates a game script passes, and the
    // angle really does run clockwise from 12 o'clock.
    private static readonly HelpFigure PolarPlayfield = new("polar-playfield", 372, 258,
    [
        Disc(170, 130, 100, FigInk.Muted, FigInk.Surface),
        Disc(170, 130, 50, FigInk.Muted, dashed: true),
        Line(170, 130, 170, 30),
        Line(170, 26, 170, 34),
        Line(266, 130, 274, 130),
        Line(170, 226, 170, 234),
        Line(66, 130, 74, 130),
        Disc(170, 130, 2.5, FigInk.Body, FigInk.Body),
        Lit(176, 36, "r = 1", 46, anchor: FigAnchor.TopLeft),
        Lit(176, 63, "r = 0.5", 46, anchor: FigAnchor.TopLeft),
        Lit(150, 134, "r = 0", 40),
        Lit(170, 8, "0°", 30),
        Lit(294, 130, "90°", 34, anchor: FigAnchor.MiddleCenter),
        Lit(170, 238, "180°", 40),
        Lit(44, 130, "270°", 40, anchor: FigAnchor.MiddleCenter),
        new(FigShape.Wedge) { X = 170, Y = 130, R = 114, W = 114, From = 16, Sweep = 46, Ink = FigInk.Accent, Fill = FigInk.None },
        Arrow(266.5, 68.6, 270.7, 76.5, FigInk.Accent),
        Txt(286, 40, "clockwise", 84, FigInk.Accent, 10.5, FigAnchor.TopLeft),
        Line(170, 130, 222, 160, FigInk.Accent, dashed: true),
        Disc(222, 160, 4.5, FigInk.Accent, FigInk.Accent),
        Line(226, 164, 262, 204),
        Lit(264, 204, "dot(0.6, 120, …)", 104, FigInk.Body, 10.5, FigAnchor.TopLeft),
    ]);

    // A folder tree is an indented outline, so it follows the reading direction like the Help page's own
    // nested bullets: in a right-to-left language it indents from the right, as File Explorer does there.
    private static readonly HelpFigure PackageFolder = new("package-folder", 400, 172,
    [
        Txt(100, 4, "Radiata finds it", 190, FigInk.Accent, 11.5, bold: true),
        .. TreeRow(20, 38, "Materials"),
        .. TreeLink(20, 38, 40, 62),
        .. TreeRow(40, 62, "Lava"),
        .. TreeLink(40, 62, 60, 86),
        .. TreeRow(60, 86, "material.json", file: true, ink: FigInk.Accent),
        Txt(300, 4, "One folder too many", 190, FigInk.Warn, 11.5, bold: true),
        .. TreeRow(220, 38, "Materials"),
        .. TreeLink(220, 38, 240, 62),
        .. TreeRow(240, 62, "Lava"),
        .. TreeLink(240, 62, 260, 86),
        .. TreeRow(260, 86, "Lava"),
        .. TreeLink(260, 86, 280, 110),
        .. TreeRow(280, 110, "material.json", file: true, ink: FigInk.Warn),
        Txt(300, 138, "Move the files up a level", 190, FigInk.Accent, 11),
    ]) { Sequence = true };

    /// <summary>Every figure, in no particular order. Keep this in step with the <c>Fig(...)</c> blocks in
    /// <see cref="HelpContent"/> — an entry nobody references still demands translations.</summary>
    public static readonly HelpFigure[] All =
    [
        IsolationModes, EditMove, SingleWheel, KeyComboAnatomy,
        StartupOrder, WorkshopLoop, PolarPlayfield, PackageFolder,
    ];

    /// <summary>The figure a <see cref="HelpBlockKind.Figure"/> block names, or null when the id is unknown
    /// (the renderer then draws the caption alone rather than a hole).</summary>
    public static HelpFigure? ById(string? id) =>
        id is null ? null : All.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every figure label that needs translating — i.e. not a <see cref="FigPart.Literal"/> button
    /// or key name. Feeds <see cref="HelpLocalization.SourceStrings"/>.</summary>
    public static IEnumerable<string> TranslatableText() =>
        All.SelectMany(f => f.Parts)
           .Where(p => p is { Shape: FigShape.Text, Literal: false } && !string.IsNullOrWhiteSpace(p.Text))
           .Select(p => p.Text!)
           .Distinct(StringComparer.Ordinal);
}
