using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>
/// The Arcade picker: a rotary carousel of arcade cabinets on a dark wireframe floor, drawn inside the
/// round window. One instance per <see cref="ArcadeControl"/>; it owns the swing animation and every cache
/// the picker needs, and <see cref="Release"/> drops all of it when the arcade closes (zero cost when
/// closed).
///
/// <para><b>Direction convention.</b> Cabinet <c>i</c> stands at ring angle <c>angle + i·step</c> and is
/// frontmost at 0. Pushing right selects the cabinet standing to the right of the front one (positive
/// sin), so the ring angle decreases by one step and every cabinet slides left across the screen while the
/// right-hand neighbour arrives at the centre.</para>
///
/// <para><b>Each game brings its own cabinet art</b> (<see cref="ArcadeArt.CabinetFor"/>), falling back to a
/// blank machine. Nothing here recolours it: depth dims it and that is all. A frame is <c>DrawImage</c> calls
/// only — no opacity masks on the per-frame path — and a dark silhouette plate goes down first so a cabinet
/// behind cannot show through art that is transparent at its edges.</para>
///
/// <para>Frozen brushes only, no XAML — the same rule as the rest of the arcade chrome.</para>
/// </summary>
internal sealed class ArcadePickerRenderer
{
    // ── Swing state ───────────────────────────────────────────────────────────
    private int    _count;
    private double _angle, _target, _vel;
    private double _drift;   // floor-row phase, 0..1
    private bool   _reduceMotion;

    // ── Floor sparks: the Reactor material's charge, riding the grid lines ──────
    private sealed class FloorSpark
    {
        public bool   Row;     // a floor row (horizontal) rather than a fan column
        public int    Trace;   // row or column index
        public double T;       // 0..1 along the trace
        public double Speed;   // trace lengths per second
        public int    Dir = 1;
        public int    Color;
    }
    private FloorSpark?[] _sparks = [];
    private readonly Random _rng = new();

    private const int    SparkTailSteps  = 8;
    private const double SparkTailStepPx = 8.0;
    private const int    SparkColorCount = 3;
    /// <summary>Tail pens indexed [colour][step from the head]; alpha and width taper. The colours are the
    /// wheel's own charge colours so a floor spark and a Reactor spark are the same spark.</summary>
    private static readonly Pen[][] SparkTailPens = BuildSparkTailPens();
    private static readonly Brush[] SparkGlow = Enumerable.Range(0, SparkColorCount)
        .Select(i => Frozen(WithAlpha(RadialMenuControl.ReactorSparkTailColor(i), 70))).ToArray();
    private static Pen[][] BuildSparkTailPens()
    {
        var byColor = new Pen[SparkColorCount][];
        for (int c = 0; c < SparkColorCount; c++)
        {
            var col = RadialMenuControl.ReactorSparkTailColor(c);
            var pens = new Pen[SparkTailSteps];
            for (int q = 0; q < SparkTailSteps; q++)
            {
                byte a = (byte)(210 - q * (200.0 / SparkTailSteps));
                var p = new Pen(Frozen(WithAlpha(col, a)), 1.6 - q * 0.12)
                    { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                p.Freeze();
                pens[q] = p;
            }
            byColor[c] = pens;
        }
        return byColor;
    }

    private double Pitch => _count > 0 ? Math.PI * 2 / _count : 0;

    // ── Caches keyed by the field the picker was last drawn at ────────────────
    private double _field = -1;
    private Point  _center;
    private Brush?  _sky;
    private double  _skyTile = 1;      // one tile's edge in px, from the field
    private double  _skyX, _skyY;      // drift, in px; wrapped to a tile at draw time

    private static double Wrap(double v, double period)
    {
        if (period <= 0) return 0;
        double r = v % period;
        return r < 0 ? r + period : r;
    }
    /// <summary>Darkens the floor toward the horizon, laid over the grid AND its sparks so the whole ground
    /// recedes into the dark rather than the lines alone thinning out.</summary>
    private static readonly Brush FloorShade = BuildFloorShade();
    private static Brush BuildFloorShade()
    {
        var b = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(WithAlpha(VoidColor, 0xE6), 0.00),
                new GradientStop(WithAlpha(VoidColor, 0x90), 0.22),
                new GradientStop(WithAlpha(VoidColor, 0x30), 0.55),
                new GradientStop(WithAlpha(VoidColor, 0x00), 1.00),
            },
        };
        b.Freeze();
        return b;
    }
    private StreamGeometry? _fan;
    private double _fanDx = double.NaN;

    private readonly Dictionary<string, Brush> _accents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Color, Brush>  _horizonGlow = [];
    private readonly Dictionary<Color, Brush>  _screenGlow  = [];
    private readonly Dictionary<Color, Pen>    _screenRim   = [];
    private readonly Dictionary<(string, int, int), (FormattedText Text, Rect Ink)> _titles = [];   // (id, plate height, ink kind + badge) → fitted title and its glyph bounds

    // ── Frozen statics ────────────────────────────────────────────────────────
    private static readonly Color InkColor  = Color.FromRgb(0xEC, 0xF1, 0xF7);
    private static readonly Color VoidColor = Color.FromRgb(0x0A, 0x0D, 0x14);

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>The sky's black wash, rebuilt only when the tuning knob moves.</summary>
    private static Brush? _skyWash;
    private static double _skyWashFor = -1;
    private static Brush SkyWash(double darken)
    {
        if (_skyWash is null || Math.Abs(darken - _skyWashFor) > 1e-6)
        {
            _skyWash = Frozen(Color.FromArgb((byte)Math.Round(255 * darken), 0, 0, 0));
            _skyWashFor = darken;
        }
        return _skyWash;
    }
    private static Pen   FrozenPen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }

    private readonly Dictionary<int, Brush> _silhouettes = [];

    private static readonly Pen[] RowPens =
    [
        FrozenPen(Color.FromArgb(0x30, 0x39, 0x44, 0x59), 1),   // far
        FrozenPen(Color.FromArgb(0x60, 0x39, 0x44, 0x59), 1),   // mid
        FrozenPen(Color.FromArgb(0x90, 0x39, 0x44, 0x59), 1),   // near
    ];

    private static readonly Brush Shadow = BuildShadow();
    private static Brush BuildShadow()
    {
        var b = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(0xFF * Math.Clamp(ArcadePickerTuning.ShadowAlpha, 0, 1)), 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
            },
        };
        b.Freeze();
        return b;
    }

    /// <summary>Static scanlines over a lit screen: a 3 px vertical tile, half of it a faint dark band.</summary>
    private static readonly Brush Scanlines = BuildScanlines();
    private static Brush BuildScanlines()
    {
        var band = new GeometryDrawing(Frozen(Color.FromArgb(0x1C, 0, 0, 0)), null,
                                       new RectangleGeometry(new Rect(0, 1.5, 3, 1.5)));
        band.Freeze();
        var b = new DrawingBrush(band)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 3, 3),
            ViewboxUnits  = BrushMappingMode.Absolute, Viewbox  = new Rect(0, 0, 3, 3),
        };
        b.Freeze();
        return b;
    }

    /// <summary>The cabinet's outer outline in art coordinates (x of width, y of height), traced against the
    /// template every cabinet PNG is drawn on. Filled dark under the art so a cabinet behind this one cannot
    /// show through wherever the art is transparent.
    ///
    /// <para>⚠ This is why per-game art must keep the template's silhouette. Art that reaches outside these
    /// bounds loses its backing plate there; art that falls well inside it gets a dark halo.</para></summary>
    private static readonly Point[] Outline =
    [
        new(  3 / 386.0,  35 / 720.0), new( 85 / 386.0,   0 / 720.0), new(283 / 386.0,  22 / 720.0),
        new(300 / 386.0,  30 / 720.0), new(300 / 386.0,  95 / 720.0), new(268 / 386.0, 120 / 720.0),
        new(298 / 386.0, 200 / 720.0), new(298 / 386.0, 300 / 720.0), new(386 / 386.0, 338 / 720.0),
        new(386 / 386.0, 350 / 720.0), new(360 / 386.0, 365 / 720.0), new(350 / 386.0, 425 / 720.0),
        new(330 / 386.0, 447 / 720.0), new(330 / 386.0, 612 / 720.0), new(175 / 386.0, 718 / 720.0),
        new( 28 / 386.0, 640 / 720.0),
    ];
    private static readonly StreamGeometry OutlineGeometry = BuildFigure(Outline);

    private static StreamGeometry BuildFigure(Point[] pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
            for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], true, false);
        }
        g.Freeze();
        return g;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Picker opened: the ring snaps so <paramref name="selected"/> is frontmost.</summary>
    public void Reset(int selected, int count)
    {
        _count  = Math.Max(0, count);
        _angle  = _target = -selected * Pitch;
        _vel    = 0;
        _drift  = 0;
    }

    /// <summary>+1 brings the right-hand neighbour to the front, -1 the left-hand one.</summary>
    public void Rotate(int direction)
    {
        if (_count == 0 || direction == 0) return;
        _target -= Math.Sign(direction) * Pitch;
    }

    public void Step(double dt, bool reduceMotion)
    {
        dt = Math.Clamp(dt, 0, 1.0 / 30);
        if (_count > 0)
        {
            if (reduceMotion)
            {
                _angle = _target;
                _vel   = 0;
            }
            else
            {
                double w = Math.PI * 2 * ArcadePickerTuning.SwingSpringHz;
                double a = -2 * w * _vel - w * w * (_angle - _target);
                _vel   += a * dt;
                _angle += _vel * dt;
                if (Math.Abs(_angle - _target) < 1e-3 && Math.Abs(_vel) < 1e-3) { _angle = _target; _vel = 0; }
            }
            // Keep the accumulated angle small; both values move together so nothing visible changes.
            if (Math.Abs(_angle) > Math.PI * 8)
            {
                double turns = Math.Round(_angle / (Math.PI * 2)) * Math.PI * 2;
                _angle -= turns; _target -= turns;
            }
        }
        _reduceMotion = reduceMotion;
        if (!reduceMotion)
        {
            _drift = (_drift + dt * ArcadePickerTuning.GridDriftPerSec) % 1.0;
            if (_drift < 0) _drift += 1.0;

            // The sky slides on its own heading. In field radii per second, so it reads the same at any disc
            // size; wrapped at draw time, so these can accumulate without growing unbounded within a session.
            double a = ArcadePickerTuning.SkyDriftAngleDeg * Math.PI / 180;
            double v = ArcadePickerTuning.SkyDriftPerSec * _field * dt;
            _skyX = Wrap(_skyX + Math.Cos(a) * v, _skyTile);
            _skyY = Wrap(_skyY + Math.Sin(a) * v, _skyTile);

            StepSparks(dt);
        }
    }

    private void StepSparks(double dt)
    {
        int n = Math.Clamp(ArcadePickerTuning.SparkCount, 0, 32);
        if (_sparks.Length != n) _sparks = new FloorSpark?[n];
        for (int k = 0; k < n; k++)
        {
            var s = _sparks[k] ??= NewSpark();
            s.T += s.Speed * dt * s.Dir;
            if (_rng.NextDouble() < 0.004) s.Dir = -s.Dir;   // the occasional double-back, like the wheel's
            if (s.T < 0 || s.T > 1) _sparks[k] = NewSpark();
        }
    }

    private FloorSpark NewSpark()
    {
        bool row = _rng.NextDouble() < 0.35;
        int traces = row ? Math.Clamp(ArcadePickerTuning.GridRows, 1, 64) : Math.Clamp(ArcadePickerTuning.GridColumns, 2, 64);
        int dir = row ? (_rng.Next(2) == 0 ? 1 : -1) : 1;   // columns run toward the viewer, rows either way
        double lo = ArcadePickerTuning.SparkSpeedMin, hi = Math.Max(lo, ArcadePickerTuning.SparkSpeedMax);
        return new FloorSpark
        {
            Row   = row,
            Trace = _rng.Next(traces),
            Dir   = dir,
            T     = row ? (dir > 0 ? 0 : 1) : 0,
            Speed = lo + _rng.NextDouble() * (hi - lo),
            Color = _rng.Next(SparkColorCount),
        };
    }

    public void Release()
    {
        _titles.Clear();
        _silhouettes.Clear();
        _fan   = null;
        _fanDx = double.NaN;
        _sky   = null;
        _field = -1;
    }

    // ── Accents ───────────────────────────────────────────────────────────────

    /// <summary>The colour a game's cabinet lights up in — the horizon glow, the bezel and the screen glow
    /// while it is frontmost. The dominant colour of the game's own cabinet art when it has one, so the light
    /// agrees with the machine it falls on (<see cref="ArcadeArt.DominantColor"/>); otherwise the renderer's
    /// accent; a drop-in game (whose renderer answers plain ink) gets its catalog tint, lifted toward ink so it
    /// still reads as lines.</summary>
    public Brush? Accent(ArcadeCatalog.Entry? entry)
    {
        if (entry is null) return null;
        if (_accents.TryGetValue(entry.Id, out var hit)) return hit;

        Brush? own = ArcadeRenderers.For(entry.Id)?.Accent;
        Brush accent;
        if (ArcadeArt.DominantColor(entry.Id) is { } painted) accent = Frozen(Lerp(painted, InkColor, 0.12));
        else if (own is not null && !ReferenceEquals(own, ArcadeChrome.Ink)) accent = own;
        else accent = Frozen(Lerp(ParseHex(entry.CabinetTintHex ?? entry.TintHex, Color.FromRgb(0x8B, 0x97, 0xAA)), InkColor, 0.35));
        _accents[entry.Id] = accent;
        return accent;
    }

    private static Color ParseHex(string hex, Color fallback)
    {
        try { return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static Color Lerp(Color a, Color b, double t) => ArcadePalette.LerpRgb(a, b, t);

    private static Color ColorOf(Brush b) => b is SolidColorBrush s ? s.Color : InkColor;

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    // ── Draw ──────────────────────────────────────────────────────────────────

    /// <summary>The front cabinet's screen as of the last frame: the ellipse a game grows out of when ✕
    /// opens it, and shrinks back into when ○ leaves it.</summary>
    private Point _frontScreen;
    private double _frontRx, _frontRy;

    /// <summary>A game handing back to the cabinets: its last board, drawn over the carousel, shrinking from
    /// the whole disc into the front cabinet's screen — where the same shot is already showing — and
    /// thinning out over the last stretch so the panel cut and the scanlines take over without a seam.
    /// Call after <see cref="Draw"/> for the same frame, so the screen it lands on is this frame's.</summary>
    public void DrawArrival(DrawingContext dc, Point c, double field, ArcadeShot shot, double progress) =>
        DrawMerge(dc, c, field, shot, SmoothStep(progress), Math.Clamp((1 - progress) / 0.25, 0, 1));

    /// <summary>The same motion run backwards for ✕: the chosen cabinet's screen picture lifts off the
    /// monitor, straightens, and grows to fill the disc, fading in over the first stretch since it starts
    /// exactly on the screen's own picture. The game then appears where it lands. No fade to black: the
    /// board the player is about to play is what carries them in.</summary>
    public void DrawDeparture(DrawingContext dc, Point c, double field, ArcadeShot shot, double progress) =>
        DrawMerge(dc, c, field, shot, 1 - SmoothStep(progress), Math.Clamp(progress / 0.25, 0, 1));

    /// <summary><paramref name="t"/> 0 = the whole disc, 1 = the front cabinet's screen; the picture — or
    /// the screen's void when the game has none — is clipped to the ellipse between the two and leans as the
    /// screen does by the same share.</summary>
    private void DrawMerge(DrawingContext dc, Point c, double field, ArcadeShot shot, double t, double fade)
    {
        if (_frontRx < 1 || _frontRy < 1 || _frontScreen == default || fade <= 0.001) return;

        // From: the playfield as the game draws it — the shot is the field, a disc on a square.
        // To:   the screen's shot rect, with the zoom the screen itself gives that kind of shot.
        double zoom = shot.Kind == ShotKind.Bundled ? ArcadePickerTuning.BundledZoom : ArcadePickerTuning.LiveZoom;
        double cx = c.X + (_frontScreen.X - c.X) * t;
        double cy = c.Y + (_frontScreen.Y - c.Y) * t;
        double hx = field + (_frontRx * zoom - field) * t;
        double hy = field + (_frontRy * zoom - field) * t;
        double ax = field + (_frontRx - field) * t;   // the aperture, without the zoom's slack
        double ay = field + (_frontRy - field) * t;
        double lean = -ArcadePickerTuning.ScreenShotTiltDeg * t;

        var tilt = new RotateTransform(lean, cx, cy);
        tilt.Freeze();
        var aperture = new EllipseGeometry(new Point(cx, cy), ax, ay);
        aperture.Freeze();

        if (fade < 0.999) dc.PushOpacity(fade);
        dc.PushClip(aperture);
        if (shot.Image is { } img)
        {
            dc.PushTransform(tilt);
            dc.DrawImage(img, new Rect(cx - hx, cy - hy, 2 * hx, 2 * hy));
            dc.Pop();
        }
        else
        {
            dc.DrawEllipse(ArcadeChrome.Void, null, new Point(cx, cy), ax, ay);
        }
        dc.Pop();
        if (fade < 0.999) dc.Pop();
    }

    /// <summary>The carousel at rest or mid-swing. A launch or a return draws over this afterwards
    /// (<see cref="DrawDeparture"/> / <see cref="DrawArrival"/>); the carousel itself never changes for
    /// either.</summary>
    public void Draw(DrawingContext dc, Point c, double field, double radius, double ppd,
                     ArcadeCatalog.Entry[] games, int selected)
    {
        EnsureLayout(c, field);

        var clip = new EllipseGeometry(c, field, field);
        clip.Freeze();
        dc.PushClip(clip);

        var selEntry = games.Length > 0 ? games[Math.Clamp(selected, 0, games.Length - 1)] : null;
        var accent   = Accent(selEntry) ?? ArcadeChrome.Ink;
        var accentColor = ColorOf(accent);

        // Vanishing point slides with the swing's remaining travel, and is exactly centred once settled.
        double parallax = _count > 0
            ? (_angle - _target) / Pitch * field * ArcadePickerTuning.GridParallaxFrac : 0;
        DrawBackdrop(dc, c, field, accentColor, parallax);

        if (games.Length == 0)
        {
            ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.NoGames), ArcadeChrome.Ui(Math.Max(10, field * 0.06)),
                                      ArcadeChrome.InkDim, c.X, c.Y - field * 0.06, ppd);
        }
        else
        {
            DrawCabinets(dc, c, field, ppd, games, selected);
        }

        // ── Text layer ──
        ArcadeChrome.DrawCentered(dc, Loc.T(UiText.Arcade.ArcadeTitle), ArcadeChrome.Ui(Math.Max(8, field * 0.045)),
                                  ArcadeChrome.InkFaint, c.X, c.Y - field * 0.97, ppd);

        if (selEntry is not null)
        {
            // Above the cabinet, under the arcade title: the name of the machine the eye is about to drop to.
            // ⚠ Not the catalog's Subtitle — a one-line description of a game whose cabinet art and marquee
            // are right there reads as a caption for something the player can already see. Stacked rather
            // than placed at two fixed heights, so a name that wraps pushes the best score down instead of
            // landing under it. The chord is measured at the top, where the disc is narrowest of the two
            // lines — the conservative width.
            double y = c.Y - field * 0.86;
            double chord = 2 * Math.Sqrt(Math.Max(0, field * field - (y - c.Y) * (y - c.Y))) - field * 0.12;
            y += ArcadeChrome.DrawCentered(dc, selEntry.Title, ArcadeChrome.Ui(Math.Max(9, field * 0.05)), ArcadeChrome.InkDim,
                                           c.X, y, ppd, chord) + field * 0.015;
            int best = ArcadeStore.LoadHighScore(selEntry.Id);
            if (best > 0)
                ArcadeChrome.DrawCentered(dc, Loc.F(UiText.Arcade.Best, best.ToString("N0", CultureInfo.InvariantCulture)),
                                          ArcadeChrome.Ui(Math.Max(9, field * 0.05)), ArcadeChrome.Ink, c.X, y, ppd);
        }

        // ⚠ Never spell a glyph out — a literal names the wrong pad for everyone on the Xbox set.
        // TestHarness.exe glyphs enforces it. Double spaces separate a button from its verb.
        string close = $"{ControllerButtons.Text(PadButton.Circle)}  {Loc.T(UiText.Arcade.Close)}";
        string[] footer = games.Length == 0
            ? [close]
            : [$"{ControllerButtons.Text(PadButton.Cross)}  {Loc.T(UiText.Arcade.Play)}", close];
        // As low as the disc's width at this row's width allows; it rides over the front cabinet's ground
        // shadow. ⚠ The limit is not a constant: the window is round, so the lower the row sits the less of
        // it fits, and the row's width belongs to whatever language is running, so a fixed fraction clips
        // the longer translations (German's "Spielen / Schließen" is the widest). Solve the circle for the
        // lowest baseline whose outer bottom corners stay inside it, and never go below the authored spot.
        double footSize = ArcadeChrome.Ui(Math.Max(9, field * 0.05));
        var footBox = ArcadeChrome.MeasureCenteredRow(footer, footSize, ppd);
        // The row wears a plaque, the same one a game's START cue sits on: the cabinets are bright, lit and
        // busy, and a bare row landed on whichever one had rolled to the front.
        double padX = footSize * 0.45, padY = footSize * 0.18;
        double plateW = footBox.Width + padX * 2, plateH = footBox.Height + padY * 2;
        double edge   = field * 0.93;                                     // keep clear of the bezel
        double halfW  = Math.Min(plateW / 2, edge);
        double lowest = Math.Sqrt(Math.Max(0, edge * edge - halfW * halfW)) - plateH;
        double top    = c.Y + Math.Min(field * 0.81, lowest);
        dc.DrawRoundedRectangle(ArcadeChrome.Panel, ArcadeChrome.FaintPen,
                                new Rect(c.X - plateW / 2, top, plateW, plateH), plateH / 2, plateH / 2);
        ArcadeChrome.DrawCenteredRow(dc, footer, footSize, ArcadeChrome.InkDim, c.X, top + padY, ppd);

        dc.Pop();   // clip
    }

    // ── The backdrop ──────────────────────────────────────────────────────────

    private void EnsureLayout(Point c, double field)
    {
        if (field == _field && c == _center) return;
        _field  = field;
        _center = c;
        _fan    = null;
        _fanDx  = double.NaN;

        // The sky is Internode's own starfield, tiled. Taken through ArcadeSprites so a player who replaces
        // that sprite gets the same sky in both places; it tiles on both axes, which is what lets it drift.
        _skyTile = Math.Max(8, field * ArcadePickerTuning.SkyTileFrac);
        _sky = null;
        if (ArcadeSprites.Get(ArcadeSprites.Slot.InternodeSky) is { } art)
        {
            var sky = new ImageBrush(art)
            {
                TileMode      = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport      = new Rect(0, 0, _skyTile, _skyTile),
                Stretch       = Stretch.Fill,
            };
            sky.Freeze();
            _sky = sky;
        }
    }

    private void DrawBackdrop(DrawingContext dc, Point c, double field, Color accent, double parallaxDx)
    {
        double yh = c.Y + field * ArcadePickerTuning.HorizonYFrac;
        double top = c.Y - field, bottom = c.Y + field;

        if (_sky is not null)
        {
            // Clipped to above the horizon: the ground is a solid surface, not a window onto the same sky.
            var skyClip = new RectangleGeometry(new Rect(c.X - field, top, 2 * field, Math.Max(0, yh - top)));
            skyClip.Freeze();
            dc.PushClip(skyClip);
            // The brush stays frozen and the drawing moves: translate by the drift, wrapped to one tile, and
            // draw a rect inflated by a tile so the slide never exposes an edge. (Animating the brush's own
            // Viewport would mean an unfrozen brush on the per-frame path.)
            double t = _skyTile;
            var slide = new TranslateTransform(-Wrap(_skyX, t), -Wrap(_skyY, t));
            slide.Freeze();
            dc.PushTransform(slide);
            dc.DrawRectangle(_sky, null, new Rect(c.X - field - t, top - t, 2 * field + 2 * t, 2 * field + 2 * t));
            dc.Pop();
            // The wash sits inside the same clip, over the whole sky band.
            double darken = Math.Clamp(ArcadePickerTuning.SkyDarken, 0, 1);
            if (darken > 0.002)
                dc.DrawRectangle(SkyWash(darken), null, new Rect(c.X - field, top, 2 * field, Math.Max(0, yh - top)));
            dc.Pop();   // sky clip
        }

        // The ground itself, opaque, under everything the floor carries.
        dc.DrawRectangle(ArcadeChrome.Field, null, new Rect(c.X - field, yh, 2 * field, Math.Max(0, bottom - yh)));

        // Horizon glow: the one place the selected game's colour touches the room.
        if (!_horizonGlow.TryGetValue(accent, out var glow))
        {
            var g = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = { new GradientStop(WithAlpha(accent, 0x00), 0), new GradientStop(WithAlpha(accent, 0x48), 1) },
            };
            g.Freeze();
            _horizonGlow[accent] = glow = g;
        }
        double glowH = field * 0.12;
        dc.DrawRectangle(glow, null, new Rect(c.X - field, yh - glowH, 2 * field, glowH));
        var horizonPen = new Pen(Frozen(WithAlpha(accent, 0x90)), 1);
        horizonPen.Freeze();
        dc.DrawLine(horizonPen, new Point(c.X - field, yh), new Point(c.X + field, yh));

        // Floor fan from the vanishing point, rebuilt only when the point has moved.
        if (_fan is null || Math.Abs(parallaxDx - _fanDx) > 0.5)
        {
            var fan = new StreamGeometry();
            using (var ctx = fan.Open())
            {
                int cols = Math.Clamp(ArcadePickerTuning.GridColumns, 2, 64);
                var vp = new Point(c.X + parallaxDx, yh);
                for (int k = 0; k < cols; k++)
                {
                    double u = cols == 1 ? 0 : k / (double)(cols - 1) * 2 - 1;
                    ctx.BeginFigure(vp, false, false);
                    ctx.LineTo(new Point(c.X + u * field * 1.8, bottom + field * 0.05), true, false);
                }
            }
            fan.Freeze();
            _fan   = fan;
            _fanDx = parallaxDx;
        }
        dc.DrawGeometry(null, ArcadeChrome.SpokePen, _fan);

        // Floor rows bunch toward the horizon and drift toward the viewer.
        int rows = Math.Clamp(ArcadePickerTuning.GridRows, 1, 64);
        for (int k = 0; k < rows; k++)
        {
            double u = (k + _drift) / rows;
            if (u < 0.02) continue;
            double y = yh + (bottom - yh) * u * u;
            var pen = u < 0.35 ? RowPens[0] : u < 0.7 ? RowPens[1] : RowPens[2];
            dc.DrawLine(pen, new Point(c.X - field, y), new Point(c.X + field, y));
        }

        if (!_reduceMotion) DrawSparks(dc, c, field, yh, bottom, parallaxDx);

        // The ground falls into the dark at the horizon: over the grid and the sparks, under the cabinets.
        dc.DrawRectangle(FloorShade, null, new Rect(c.X - field, yh, 2 * field, bottom - yh));
    }

    /// <summary>Sparks on the floor grid, drawn exactly as the wheel's Reactor material draws its charge:
    /// a white-hot head with a tight fake glow and a tapering coloured tail walked back along the trace.
    /// Every trace here is a straight line, so the tail is a clamped walk along its unit vector.</summary>
    private void DrawSparks(DrawingContext dc, Point c, double field, double yh, double bottom, double parallaxDx)
    {
        int rows = Math.Clamp(ArcadePickerTuning.GridRows, 1, 64);
        int cols = Math.Clamp(ArcadePickerTuning.GridColumns, 2, 64);
        var vp = new Point(c.X + parallaxDx, yh);

        foreach (var s in _sparks)
        {
            if (s is null) continue;
            Point start, end; double depth;
            if (s.Row)
            {
                if (s.Trace >= rows) continue;
                double u = (s.Trace + _drift) / rows;
                if (u < 0.02) continue;
                double y = yh + (bottom - yh) * u * u;
                start = new Point(c.X - field, y);
                end   = new Point(c.X + field, y);
                depth = u;
            }
            else
            {
                if (s.Trace >= cols) continue;
                double u = s.Trace / (double)(cols - 1) * 2 - 1;
                start = vp;
                end   = new Point(c.X + u * field * 1.8, bottom + field * 0.05);
                depth = s.T;
            }

            double dx = end.X - start.X, dy = end.Y - start.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) continue;
            double ux = dx / len, uy = dy / len;
            // Columns run toward the viewer, so the spark's progress is squared like the rows' spacing:
            // it crawls near the horizon and rushes past the front.
            double p = s.Row ? s.T : s.T * s.T;
            double headD = p * len;
            var head = new Point(start.X + ux * headD, start.Y + uy * headD);

            var tail = SparkTailPens[s.Color];
            var prev = head;
            for (int q = 0; q < SparkTailSteps; q++)
            {
                double d = Math.Clamp(headD - s.Dir * SparkTailStepPx * (q + 1), 0, len);
                var pt = new Point(start.X + ux * d, start.Y + uy * d);
                dc.DrawLine(tail[q], prev, pt);
                if (pt == prev) break;
                prev = pt;
            }
            double size = 0.5 + 0.7 * depth;   // smaller toward the horizon
            dc.DrawEllipse(SparkGlow[s.Color], null, head, 2.0 * size, 2.0 * size);
            dc.DrawEllipse(RadialMenuControl.ReactorSparkHead, null, head, 0.8 * size, 0.8 * size);
        }
    }

    // ── The cabinets ──────────────────────────────────────────────────────────

    private readonly record struct Slot(int Index, double Theta, double Z);

    private void DrawCabinets(DrawingContext dc, Point c, double field, double ppd,
                              ArcadeCatalog.Entry[] games, int selected)
    {
        int n = games.Length;
        if (_count != n) Reset(selected, n);   // the catalog changed under an open picker

        var slots = new List<Slot>(n);
        for (int i = 0; i < n; i++)
        {
            double theta = _angle + i * Pitch;
            double z = n == 1 ? 1.0 : Math.Cos(theta);
            if (z < ArcadePickerTuning.CullBehindZ) continue;
            slots.Add(new Slot(i, theta, z));
        }
        // Nearest first to pick the visible set, then back-to-front to draw.
        slots.Sort((a, b) => b.Z.CompareTo(a.Z));
        int keep = Math.Clamp(ArcadePickerTuning.MaxVisibleCabinets, 1, 64);
        if (slots.Count > keep) slots.RemoveRange(keep, slots.Count - keep);
        slots.Reverse();

        double R = ArcadePickerTuning.RingRadiusFrac, D = ArcadePickerTuning.CameraDistanceFrac;
        double pFront = D / Math.Max(0.05, D - R);

        foreach (var s in slots)
        {
            // Depth, 1 at the front and 0 at the very back, and the two end weights the near/far treatments
            // ride on.
            //
            // ⚠ These must stay smooth functions of the ring angle — plain polynomials in z, which is
            // cos(theta). A thresholded ramp (SmoothStep about a z cutoff) holds a cabinet flat for most of
            // the lap and then moves it all at once near the end, which reads as a duck rather than as
            // travel. Everything below is smooth, so a cabinet's path around the ring is one closed oval.
            double k     = (s.Z + 1) / 2;
            double front = k * k;
            double rear  = (1 - k) * (1 - k);

            double p  = D / Math.Max(0.05, D - s.Z * R);
            double sc = Math.Max(ArcadePickerTuning.RearScaleFloor, p / pFront);
            // Depth shrink beyond the perspective: 1 at the front, (1 − RearShrink) at the very back.
            sc *= 1 - Math.Clamp(ArcadePickerTuning.RearShrink, 0, 0.9) * (1 - k);
            // The far end tucks in further still, weighted to the back.
            sc *= 1 - Math.Clamp(ArcadePickerTuning.RearTuck, 0, 0.9) * rear;
            double h  = ArcadePickerTuning.CabinetHeightFrac * field * sc;
            double w  = h * ArcadeArt.CabinetAspect;
            double x  = n == 1 ? c.X : c.X + Math.Sin(s.Theta) * R * field * p;

            double dim = ArcadePickerTuning.DimBack + (1 - ArcadePickerTuning.DimBack) * k;
            double lit = SmoothStep((s.Z - ArcadePickerTuning.ScreenLitZ) / Math.Max(1e-3, 1 - ArcadePickerTuning.ScreenLitZ));

            // Height on the floor: the ring's own rise and fall, with both ends pulled down. The three terms
            // together stay monotonic in depth, so the path reads as an oval seen from slightly above rather
            // than as a cabinet bobbing.
            double baseY = c.Y + field * (ArcadePickerTuning.FloorBaseFrac + ArcadePickerTuning.FloorDepthFrac * s.Z)
                         + h * ArcadePickerTuning.FrontDropFrac * front
                         + field * ArcadePickerTuning.RearSettleFrac * rear;
            var rect = new Rect(x - w / 2, baseY - h, w, h);

            var entry  = games[s.Index];
            var accent = Accent(entry) ?? ArcadeChrome.Ink;

            // Ground shadow.
            dc.DrawEllipse(Shadow, null, new Point(x, baseY),
                           w * ArcadePickerTuning.ShadowWidthFrac, h * ArcadePickerTuning.ShadowHeightFrac);

            // Silhouette, so nothing behind draws through.
            var fit = new MatrixTransform(w, 0, 0, h, rect.X, rect.Y);
            fit.Freeze();
            dc.PushTransform(fit);
            dc.DrawGeometry(Silhouette(k), null, OutlineGeometry);
            dc.Pop();

            DrawScreen(dc, rect, entry, accent, lit, ppd);
            if (s.Z >= slots[^1].Z)
            {
                _frontScreen = new Point(rect.X + rect.Width * ArcadePickerTuning.ScreenCxFrac,
                                         rect.Y + rect.Height * ArcadePickerTuning.ScreenCyFrac);
                _frontRx = rect.Width  * ArcadePickerTuning.ScreenRxFrac;
                _frontRy = rect.Height * ArcadePickerTuning.ScreenRyFrac;
            }

            // The machine itself: the game's own art, or the blank cabinet when it has none. Depth is the
            // only thing done to it — nothing recolours the art.
            if (ArcadeArt.CabinetFor(entry.Id, entry.CabinetTintHex) is { } cabinet)
            {
                bool partial = dim < 0.999;
                if (partial) dc.PushOpacity(dim);
                dc.DrawImage(cabinet, rect);
                if (partial) dc.Pop();
            }

            // Art of its own names the game on its marquee; the blank cabinet cannot, so a machine wearing it
            // gets the title printed on its nameplate. Without this a drop-in game is unnamed everywhere on
            // this screen.
            if (!ArcadeArt.HasOwnCabinet(entry.Id))
                DrawNameplate(dc, rect, entry, dim, ppd);
        }
    }

    private static double SmoothStep(double t) => ArcadeMath.Smoothstep(t);

    /// <summary>The plate behind a cabinet's art, in the template's own silhouette: it stops the machine
    /// behind showing through wherever the art is transparent. Neutral at every depth — the art carries all
    /// the colour — and darker toward the back so a far cabinet still recedes.</summary>
    private Brush Silhouette(double k)
    {
        int step = (int)Math.Round(Math.Clamp(k, 0, 1) * 8);
        if (_silhouettes.TryGetValue(step, out var hit)) return hit;
        return _silhouettes[step] = Frozen(Lerp(VoidColor, Color.FromRgb(0x17, 0x1E, 0x2A), step / 8.0));
    }

    /// <summary>The screen's clip (the ellipse intersected with the region above the panel's back edge) and
    /// its tilt, cached on the cabinet's quantised rect. Every input is a pure function of the rect, and the
    /// boolean intersect was the single most expensive thing on the picker's frame — built per visible
    /// cabinet per frame, byte-identical at rest. Quantised to a quarter pixel; the swing passes through a
    /// bounded set of positions, and the table is capped.</summary>
    private readonly Dictionary<(int, int, int, int), (Geometry Clip, Transform Tilt)> _screenMasks = new();

    private (Geometry Clip, Transform Tilt) ScreenMask(Rect rect, Point centre, double rx, double ry)
    {
        var key = ((int)Math.Round(rect.X * 4), (int)Math.Round(rect.Y * 4),
                   (int)Math.Round(rect.Width * 4), (int)Math.Round(rect.Height * 4));
        if (_screenMasks.TryGetValue(key, out var hit)) return hit;

        var ellipse = new EllipseGeometry(centre, rx, ry);
        double cutL = rect.Y + rect.Height * ArcadePickerTuning.ScreenCutLeftFrac;
        double cutR = rect.Y + rect.Height * ArcadePickerTuning.ScreenCutRightFrac;
        var above = new StreamGeometry();
        using (var ctx = above.Open())
        {
            ctx.BeginFigure(new Point(rect.X - 1, rect.Y - 1), true, true);
            ctx.LineTo(new Point(rect.Right + 1, rect.Y - 1), true, false);
            ctx.LineTo(new Point(rect.Right + 1, cutR), true, false);
            ctx.LineTo(new Point(rect.X - 1, cutL), true, false);
        }
        var tilt = new RotateTransform(-ArcadePickerTuning.ScreenShotTiltDeg, centre.X, centre.Y);
        tilt.Freeze();
        var clip = new CombinedGeometry(GeometryCombineMode.Intersect, ellipse, above);
        clip.Freeze();

        if (_screenMasks.Count >= 2048) _screenMasks.Clear();
        _screenMasks[key] = (clip, tilt);
        return (clip, tilt);
    }

    private void DrawScreen(DrawingContext dc, Rect rect, ArcadeCatalog.Entry entry, Brush accent, double lit, double ppd)
    {
        var centre = new Point(rect.X + rect.Width * ArcadePickerTuning.ScreenCxFrac,
                               rect.Y + rect.Height * ArcadePickerTuning.ScreenCyFrac);
        double rx = rect.Width * ArcadePickerTuning.ScreenRxFrac, ry = rect.Height * ArcadePickerTuning.ScreenRyFrac;
        if (rx < 1 || ry < 1) return;

        var accentColor = ColorOf(accent);
        bool isLit = lit > 0.001;
        bool partial = isLit && lit < 0.98;

        if (isLit)
        {
            if (!_screenGlow.TryGetValue(accentColor, out var glow))
            {
                var g = new RadialGradientBrush
                {
                    GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops = { new GradientStop(WithAlpha(accentColor, 0x70), 0.55), new GradientStop(WithAlpha(accentColor, 0x00), 1) },
                };
                g.Freeze();
                _screenGlow[accentColor] = glow = g;
            }
            if (partial) dc.PushOpacity(lit);
            dc.DrawEllipse(glow, null, centre, rx * 1.35, ry * 1.35);
            if (partial) dc.Pop();
        }

        var shot = ArcadeShots.Get(entry.Id);

        // The visible screen is the ellipse above the control panel's back edge — the panel crosses the
        // bottom of the monitor in the art, so the shot is masked there rather than drawn over it.
        // Everything ON the screen leans together — the picture and the scanlines over it are one monitor
        // set into the hood, so they share this one transform.
        var (clip, tilt) = ScreenMask(rect, centre, rx, ry);
        dc.PushClip(clip);
        dc.DrawEllipse(ArcadeChrome.Void, null, centre, rx, ry);
        if (shot.Image is { } img)
        {
            // The shot is a round board on a square; squashed into the ellipse it reads as a circle seen in
            // the cabinet's own perspective.
            double zoom = shot.Kind == ShotKind.Bundled ? ArcadePickerTuning.BundledZoom : ArcadePickerTuning.LiveZoom;
            var dest = new Rect(centre.X - rx * zoom, centre.Y - ry * zoom, 2 * rx * zoom, 2 * ry * zoom);
            dc.PushTransform(tilt);
            // Desaturation: the grey copy underneath, the colour on top at the surviving saturation. Off the
            // front the colour nearly vanishes; it returns as the cabinet swings forward.
            double saturation = 1 - Math.Clamp(ArcadePickerTuning.RearScreenDesaturate, 0, 1) * (1 - lit);
            if (saturation < 0.98)
            {
                dc.DrawImage(ArcadeArt.GrayOf(img), dest);
                if (saturation > 0.01)
                {
                    dc.PushOpacity(saturation);
                    dc.DrawImage(img, dest);
                    dc.Pop();
                }
            }
            else
            {
                dc.DrawImage(img, dest);
            }
            dc.Pop();   // tilt
        }
        else
        {
            // Placeholder plate: the game's initial in its own colour.
            string initial = entry.Title.Length > 0 ? entry.Title[..1].ToUpperInvariant() : "?";
            var ft = ArcadeChrome.Text(initial, ry * 1.1, accent, ppd);
            dc.DrawText(ft, new Point(centre.X - ft.Width / 2, centre.Y - ft.Height / 2));
        }
        // Scanlines lean with the picture. Drawn as a rotated rect rather than the screen ellipse: the
        // aperture is already the clip, so the rect only has to be big enough to cover it once turned.
        double cover = Math.Sqrt(rx * rx + ry * ry) + 2;
        dc.PushTransform(tilt);
        dc.DrawRectangle(Scanlines, null, new Rect(centre.X - cover, centre.Y - cover, cover * 2, cover * 2));
        dc.Pop();
        // Every screen shows its game; the ones not at the front sit under a darkening that lifts as the
        // cabinet swings forward.
        double darken = Math.Clamp(ArcadePickerTuning.RearScreenDarken, 0, 1) * (1 - lit);
        if (darken > 0.002)
            dc.DrawEllipse(Frozen(Color.FromArgb((byte)(255 * darken), 0, 0, 0)), null, centre, rx, ry);

        // The rim is masked with the picture: below the panel's edge there is no screen to ring.
        if (isLit)
        {
            if (!_screenRim.TryGetValue(accentColor, out var rim))
                _screenRim[accentColor] = rim = FrozenPen(WithAlpha(accentColor, 0xA0), 1.2);
            if (partial) dc.PushOpacity(lit);
            dc.DrawEllipse(null, rim, centre, rx, ry);
            if (partial) dc.Pop();
        }
        else
        {
            dc.DrawEllipse(null, ArcadeChrome.FaintPen, centre, rx, ry);
        }
        dc.Pop();   // clip
    }

    /// <summary>The title printed into the blank cabinet's own nameplate band, the way a built-in's art carries
    /// its name on the same band — no plate of its own. Ink is chosen against the nameplate as the tint leaves
    /// it (dark on the plain or a light tint, light on a deep one), and the title recedes with the machine.</summary>
    private void DrawNameplate(DrawingContext dc, Rect rect, ArcadeCatalog.Entry entry, double dim, double ppd)
    {
        var plate = new Rect(rect.X + rect.Width * ArcadePickerTuning.NameplateLeftFrac,
                             rect.Y + rect.Height * ArcadePickerTuning.NameplateTopFrac,
                             rect.Width * (ArcadePickerTuning.NameplateRightFrac - ArcadePickerTuning.NameplateLeftFrac),
                             rect.Height * (ArcadePickerTuning.NameplateBottomFrac - ArcadePickerTuning.NameplateTopFrac));
        if (plate.Height < 5) return;

        // The title's lane: the whole band, or what the badge leaves of it.
        double inset = plate.Width * ArcadePickerTuning.NameplateInsetFrac;
        double laneL = plate.X + inset, laneR = plate.Right - inset;
        Rect? badgeRect = null;
        if (entry.BadgePath is { } badgePath && ArcadeArt.PackageImage(badgePath) is { } badge && badge.PixelHeight > 0)
        {
            double bh = plate.Height * ArcadePickerTuning.NameplateBadgeHeightFrac;
            double bw = bh * badge.PixelWidth / badge.PixelHeight;
            double bx = plate.X + plate.Width * ArcadePickerTuning.NameplateBadgeLeftFrac;
            double by = plate.Y + plate.Height / 2 - plate.Height * ArcadePickerTuning.NameplateBadgeRiseFrac - bh / 2;
            badgeRect = new Rect(bx, by, bw, bh);
            laneL = Math.Min(laneR - plate.Width * 0.3, bx + bw - plate.Width * ArcadePickerTuning.NameplateBadgeOverlapFrac);
        }

        bool darkInk = Luminance(ArcadeArt.NameplateColor(entry.CabinetTintHex)) > 0.42;
        double laneW = laneR - laneL;
        var key = (entry.Id, (int)Math.Round(plate.Height * 4), (darkInk ? 1 : 0) + (badgeRect is null ? 0 : 2));
        if (!_titles.TryGetValue(key, out var fitted))
        {
            fitted = FitNameplateTitle(entry.Title.ToUpperInvariant(), darkInk ? DarkInk : ArcadeChrome.Ink,
                                       laneW, plate.Height, ppd);
            if (_titles.Count > 64) _titles.Clear();
            _titles[key] = fitted;
        }

        bool partial = dim < 0.999;
        if (partial) dc.PushOpacity(dim);
        // Centred on the glyphs' own bounds, both ways: the line box carries ascender and descender space an
        // all-caps title never inks, so centring the box sits the word off the band's middle.
        var ink = fitted.Ink;
        dc.DrawText(fitted.Text, new Point(laneL + (laneW - ink.Width) / 2 - ink.X,
                                           plate.Y + (plate.Height - ink.Height) / 2 - ink.Y));
        if (badgeRect is { } br) dc.DrawImage(ArcadeArt.PackageImage(entry.BadgePath!)!, br);
        if (partial) dc.Pop();
    }

    /// <summary>Fit a title into the nameplate lane, computed once per cabinet size and cached: one line at the
    /// full size when it fits; else one line shrunk to fit, down to <see cref="SingleLineFloor"/> of full size;
    /// else two (or more) tighter-set lines broken between words, shrinking until the widest word fits the lane
    /// and the block the band's height. Only past the smallest size does a word split or a line end in an ellipsis. The manifest caps a
    /// title at 24 characters, so this is about long words and long names, never an essay.</summary>
    private static (FormattedText Text, Rect Ink) FitNameplateTitle(string title, Brush ink, double laneW, double plateH, double ppd)
    {
        double full = Math.Max(6, plateH * ArcadePickerTuning.NameplateTextFrac);
        double maxInkH = plateH * 0.86;
        FormattedText Make(double size, double maxWidth)
        {
            var ft = new FormattedText(title, System.Globalization.CultureInfo.InvariantCulture,
                                       System.Windows.FlowDirection.LeftToRight, NameplateFace, size, ink, ppd)
            // FormattedText trims with an ellipsis by default, silently, and its geometry is then the trimmed
            // text's — so a width check passes on a word it cut short. Only the last resort below trims.
            { TextAlignment = TextAlignment.Left, Trimming = TextTrimming.None };
            if (maxWidth > 0) { ft.MaxTextWidth = maxWidth; ft.LineHeight = size * 0.92; ft.TextAlignment = TextAlignment.Center; }
            return ft;
        }
        static (FormattedText, Rect) Measured(FormattedText ft) => (ft, ft.BuildGeometry(new Point(0, 0)).Bounds);

        // One line: as it is, or shrunk to the lane.
        var one = Make(full, 0);
        double natural = one.WidthIncludingTrailingWhitespace;
        if (natural <= laneW) return Measured(one);
        double shrunk = full * laneW / natural;
        if (shrunk >= full * SingleLineFloor) return Measured(Make(shrunk, 0));

        // Wrapped at word breaks: shrink until the widest word fits the lane (so no word is ever split) and the
        // block fits the band's height.
        double widestAtFull = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Max(w => new FormattedText(w, System.Globalization.CultureInfo.InvariantCulture,
                      System.Windows.FlowDirection.LeftToRight, NameplateFace, full, ink, ppd).Width);
        double minSize = Math.Max(5, plateH * 0.22);
        for (double size = Math.Min(full * SingleLineFloor, full * (laneW - 2) / widestAtFull); size >= minSize; size *= 0.94)
        {
            var ft = Make(size, laneW);
            var (_, bounds) = Measured(ft);
            if (bounds.Height <= maxInkH && bounds.Width <= laneW + 0.5) return (ft, bounds);
        }
        // Past the smallest size: a word may break mid-word, and a block still too tall ends in an ellipsis.
        var last = Make(minSize, laneW);
        last.MaxLineCount = Math.Max(1, (int)((maxInkH - minSize * 0.7) / (minSize * 0.92)) + 1);
        last.Trimming = TextTrimming.CharacterEllipsis;
        return Measured(last);
    }

    /// <summary>How far a title shrinks on one line before it wraps instead.</summary>
    private const double SingleLineFloor = 0.6;

    /// <summary>The nameplate lettering: bold condensed caps, like the painted names on the built-in cabinets.
    /// Bahnschrift ships with Windows 10 and 11; the fallbacks keep a readable bold face if it is missing.</summary>
    private static readonly Typeface NameplateFace = new(
        new FontFamily("Bahnschrift, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Condensed);

    private static readonly Brush DarkInk = Frozen(Color.FromRgb(0x0A, 0x0D, 0x14));

    /// <summary>Relative luminance, 0..1, for choosing a readable ink over a plate colour.</summary>
    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
