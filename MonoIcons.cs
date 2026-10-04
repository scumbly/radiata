using System.Windows;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// Built-in monochrome 24×24 icons for action types that don't supply an exe path.
/// All images are frozen DrawingImages — safe to share across threads.
/// </summary>
public static class MonoIcons
{
    // Declared as field initializers so they run before the icon-field initializers below.
    private static readonly SolidColorBrush Ink       = MakeBrush(Color.FromArgb(215, 20, 20, 30));
    private static readonly Pen             StrokeThin = MakePen(Ink, 1.6);
    private static readonly Pen             StrokeBold = MakePen(Ink, 2.2);

    private static SolidColorBrush MakeBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    private static Pen             MakePen(Brush br, double w) { var p = new Pen(br, w); p.Freeze(); return p; }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    public static ImageSource? ForSlice(WheelSlice slice) => slice.Action?.Type switch
    {
        "settings"      => Settings,
        "launch"        => Launch,
        "script"        => Script,
        "toggle"        => Toggle,
        "xbox-emulation" => Toggle,
        "dualshock-emulation" => Toggle,
        "launcher"      => Launch,   // last-resort fallback if the Material glyph name fails to resolve
        "url"           => Url,
        "sequence"      => Script,
        "system"        => SystemIcon(slice.Action.Command),
        _               => null,
    };

    /// <summary>Default *Material* glyph name for an action type when the slice has no explicit
    /// IconName. The caller resolves + tints it via <see cref="PackIconHelper"/>; null means fall
    /// back to the built-in vector shape (<see cref="ForSlice"/>). Precursor to the V2 "Defaults" tab.</summary>
    public static string? DefaultMaterialGlyph(ActionConfig? action) => action?.Type switch
    {
        "launch"         => "ApplicationExport",
        "exit-app"       => "LocationExit",
        "script"         => "ScriptTextOutline",
        "toggle"         => "ToggleSwitchOutline",
        "keypress"       => "KeyboardVariant",
        "switch-audio"   => "AudioInputRca",
        "obs"            => "Broadcast",
        "discord-launch" => "MicrosoftXboxController",
        "discord-join"   => "MicrophoneMessage",
        "discord-mute"   => "MicrophoneOff",
        "discord-deafen" => "HeadphonesOff",
        "steam-mute"     => "MicrophoneOff",
        "installed-game" => "GamepadVariant",
        "game-browser"   => "DotsGrid",
        // A specific game brings its own glyph (ArcadeCatalog); the Arcade Launcher gets its wordmark glyph.
        "arcade"         => ArcadeCatalog.Find(action.Command)?.Glyph ?? PackIconHelper.JoystickName,
        "disable-wheels" => "CircleOffOutline",
        "safe-mode"      => "ShieldHalfFull",   // Anticheat Passthru Mode — partial protection, deliberately
        "url"            => "Web",
        "sequence"       => "PlaylistPlay",
        "text-chat"      => "MessageText",
        "settings"       => "Cog",
        "xbox-emulation"      => "ControllerOff",
        "dualshock-emulation" => "ControllerOff",   // same glyph as XBox Mode; tinted blue via ActionTint
        "launcher"       => LauncherCatalog.Find(action.Command)?.Glyph,
        // Recognised system commands get a tailored Material glyph; the generic system default (and any
        // unrecognised command) is ProgressWrench — the type-level default chosen in the Color Defaults window.
        "system"         => action.Command?.ToLowerInvariant() switch
        {
            "sleep"            => "Sleep",
            "hibernate"        => "Snowflake",
            "reboot"           => "Restart",
            "shutdown"         => "Power",
            "logout"           => "Logout",
            "lock"             => "AccountLock",
            "show-desktop"     => "Monitor",
            "volume"           => "VolumeMute",
            "mic-mute"         => "MicrophoneOff",
            "volume-set"       => "VolumeMedium",
            "media-play-pause" => "PlayPause",
            "media-next"       => "SkipNext",
            "media-prev"       => "SkipPrevious",
            "media-mute"       => "VolumeOff",
            // display-toggle: one glyph, the action reads/flips the current topology.
            // display-extend/clone/external/internal and every nvidia-*/amd-* command are removed from the
            // offered taxonomy (no migration) — deliberately no glyph entry exists for them; a legacy slice
            // still using one falls through to the generic "ProgressWrench" default below. Only the
            // Categories entries stay (as Hidden) to keep such a slice's type from being silently rewritten
            // on the next edit — see WheelEditorControl.Categories.
            "display-toggle"   => "MonitorMultiple",
            "hdr-toggle"       => "MonitorShimmer",
            "focus-assist"     => "ShieldMoon",
            "steam-chat-open"     => "Steam",
            "xbox-party-open"     => "HeadsetDock",
            "xbox-party-mute"     => "MicrophoneOff",
            "gamebar-open"        => "MicrosoftXbox",
            "gamebar-screenshot"  => "Camera",
            "gamebar-record"      => "RecordRec",   // matches the vendor record glyphs
            "gamebar-record-last" => "History",
            "gamebar-mic"         => "Microphone",
            "empty-recycle-bin"   => "Recycle",
            "power-plan"          => "PowerSettings",
            _                  => "ProgressWrench",
        },
        _ => null,
    };

    private static ImageSource SystemIcon(string? cmd) => cmd switch
    {
        "sleep"    => Sleep,
        "volume"   => Volume,
        "mic-mute" => Mic,
        _          => Settings,
    };

    // ── Icons (public so callers can use them directly) ────────────────────────

    public static readonly ImageSource Settings = BuildSettings();
    public static readonly ImageSource Sleep    = BuildSleep();
    public static readonly ImageSource Volume   = BuildVolume();
    public static readonly ImageSource Launch   = BuildLaunch();
    public static readonly ImageSource Script   = BuildScript();
    public static readonly ImageSource Url      = BuildUrl();
    public static readonly ImageSource Toggle   = BuildToggle();
    public static readonly ImageSource Mic      = BuildMic();

    // ── Icon definitions (24×24 coordinate space) ─────────────────────────────

    private static ImageSource BuildSettings()
    {
        // Three horizontal slider/EQ lines with circle handles at different positions
        var g = new DrawingGroup();
        g.Children.Add(HLine(4, 7,  20)); g.Children.Add(Dot(9,  7));
        g.Children.Add(HLine(4, 12, 20)); g.Children.Add(Dot(15, 12));
        g.Children.Add(HLine(4, 17, 20)); g.Children.Add(Dot(10, 17));
        return Img(g);
    }

    private static ImageSource BuildSleep()
    {
        // Crescent moon via geometry exclusion
        var outer    = new EllipseGeometry(new Point(11.5, 13), 8.0, 8.0);
        var cutout   = new EllipseGeometry(new Point(14.5, 10), 7.0, 7.0);
        outer.Freeze(); cutout.Freeze();
        var crescent = new CombinedGeometry(GeometryCombineMode.Exclude, outer, cutout);
        crescent.Freeze();
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(Ink, null, crescent));
        return Img(g);
    }

    private static ImageSource BuildVolume()
    {
        // Filled speaker cone + two arc sound waves
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(Ink, null, G("M 4,9 L 4,15 L 8,15 L 13,20 L 13,4 L 8,9 Z")));
        g.Children.Add(ArcGD(16, 12, 3.5, -55, 110));
        g.Children.Add(ArcGD(16, 12, 6.2, -55, 110));
        return Img(g);
    }

    private static ImageSource BuildLaunch()
    {
        // Arrow pointing upper-right
        var g = new DrawingGroup();
        g.Children.Add(GD(null, StrokeBold, G("M 6,18 L 18,6 M 10,6 L 18,6 L 18,14")));
        return Img(g);
    }

    private static ImageSource BuildScript()
    {
        // Document with corner fold + three content lines
        var g = new DrawingGroup();
        g.Children.Add(GD(null, StrokeThin, G("M 5,2 L 5,22 L 19,22 L 19,7 L 14,2 Z M 14,2 L 14,7 L 19,7")));
        g.Children.Add(HLine(8, 11, 16));
        g.Children.Add(HLine(8, 14, 16));
        g.Children.Add(HLine(8, 17, 13));
        return Img(g);
    }

    private static ImageSource BuildUrl()
    {
        // Globe: circle + equator line + meridian oval
        var g = new DrawingGroup();
        var sphere   = new EllipseGeometry(new Point(12, 12), 9, 9);
        var meridian = new EllipseGeometry(new Point(12, 12), 4.5, 9);
        sphere.Freeze(); meridian.Freeze();
        g.Children.Add(GD(null, StrokeThin, sphere));
        g.Children.Add(GD(null, StrokeThin, meridian));
        g.Children.Add(HLine(3, 12, 21));
        return Img(g);
    }

    private static ImageSource BuildToggle()
    {
        // Power ring: 120° gap at top (arc runs upper-right to upper-left), plus a vertical line.
        var g = new DrawingGroup();
        g.Children.Add(GD(null, StrokeBold, G("M 18.93,8 A 8,8 0 1 1 5.07,8")));
        g.Children.Add(GD(null, StrokeBold, G("M 12,2 L 12,13")));
        return Img(g);
    }

    private static ImageSource BuildMic()
    {
        // Capsule body + pickup arc + stand + base
        var g = new DrawingGroup();
        g.Children.Add(GD(null, StrokeThin, G("M 9,3 L 15,3 A 3,5 0 0 1 15,13 L 9,13 A 3,5 0 0 1 9,3 Z")));
        g.Children.Add(GD(null, StrokeThin, G("M 6,11 A 6,6 0 0 0 18,11")));
        g.Children.Add(GD(null, StrokeThin, G("M 12,17 L 12,21 M 8,21 L 16,21")));
        return Img(g);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Wrap in a DrawingGroup with a transparent 24×24 background to lock bounds,
    // then freeze everything and return as a DrawingImage.
    private static ImageSource Img(DrawingGroup content)
    {
        var bg = new RectangleGeometry(new Rect(0, 0, 24, 24));
        bg.Freeze();
        var bgDrawing = new GeometryDrawing(Brushes.Transparent, null, bg);

        var wrapper = new DrawingGroup();
        wrapper.Children.Add(bgDrawing);
        content.Freeze();
        wrapper.Children.Add(content);
        wrapper.Freeze();

        var img = new DrawingImage(wrapper);
        img.Freeze();
        return img;
    }

    private static GeometryDrawing GD(Brush? fill, Pen? stroke, Geometry g) =>
        new GeometryDrawing(fill, stroke, g);

    private static Geometry G(string path)
    {
        var g = Geometry.Parse(path);
        g.Freeze();
        return g;
    }

    private static GeometryDrawing HLine(double x1, double y, double x2)
    {
        var g = new LineGeometry(new Point(x1, y), new Point(x2, y));
        g.Freeze();
        return new GeometryDrawing(null, StrokeThin, g);
    }

    private static GeometryDrawing Dot(double cx, double cy)
    {
        var g = new EllipseGeometry(new Point(cx, cy), 2.8, 2.8);
        g.Freeze();
        return new GeometryDrawing(Ink, null, g);
    }

    private static GeometryDrawing ArcGD(double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        double Rad(double d) => d * Math.PI / 180;
        var p0 = new Point(cx + r * Math.Cos(Rad(startDeg)),            cy + r * Math.Sin(Rad(startDeg)));
        var p1 = new Point(cx + r * Math.Cos(Rad(startDeg + sweepDeg)), cy + r * Math.Sin(Rad(startDeg + sweepDeg)));

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(p0, isFilled: false, isClosed: false);
            ctx.ArcTo(p1, new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        sg.Freeze();
        return new GeometryDrawing(null, StrokeThin, sg);
    }
}
