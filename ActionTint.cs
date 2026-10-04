using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// Per-action-type tint for wheel-slice glyph icons: muted, mid-dark hues so action types read as
/// distinct at a glance. Colours are user-overridable from Settings (persisted in config as
/// type → "#RRGGBB"); anything unset uses the built-in <see cref="Defaults"/>. A fixed alpha softens
/// the tint on the slice. Applied only to monochrome glyphs, never full-colour extracted app icons.
/// </summary>
public static class ActionTint
{
    /// <summary>Alpha every tinted glyph is painted at. Public so a preview surface (the Settings icon well)
    /// can match the wheel exactly — a fully-opaque preview reads noticeably harder than the real slice, which
    /// is the difference most visible on light materials like Kawaii.</summary>
    public const byte Alpha = 215;

    /// <summary>Ordered action types with a friendly label and built-in default colour. Drives both
    /// the icon tint and the Settings colour-picker list (so every type is always findable) — and, via
    /// <see cref="CustomColorStore"/>, seeds the swatch palette, so these are the palette's base tones.
    /// <para>Every baked base colour belongs in HSL lightness ≈0.25–0.45. That isn't cosmetic:
    /// <see cref="InvertLightness"/> — which produces both the dark-material variant and the light half of
    /// every paired swatch — is <c>l = max(1 - L, 0.75)</c>. A base at L ≥ 0.25 lands exactly on the 0.75
    /// floor, so the whole set lightens to one consistent tone; a base darker than that overshoots
    /// (L .21 → variant .79, visibly paler than its neighbours), and one much lighter barely changes at all
    /// and reads weakly on a light material. If you add or retune a colour, check it lands in the band and
    /// adjust lightness only, keeping hue and saturation.</para></summary>
    public static readonly (string Type, string Label, Color Default)[] Defaults =
    [
        ("launch",         "Launch app",         Rgb(32,  96,  50)),   // green
        ("exit-app",       "Exit app",           Rgb(140, 58,  58)),   // brick red
        ("installed-game", "Installed game",     Rgb(35,  85, 140)),   // blue
        ("url",            "Open URL",           Rgb(22,  98, 116)),   // cyan
        ("system",         "System command",     Rgb(50, 101, 169)),   // blue
        ("script",         "Run script",         Rgb(90,  56, 120)),   // violet
        ("keypress",       "Keypress",           Rgb(134, 92,  22)),   // amber
        ("switch-audio",   "Switch audio",       Rgb(60, 110, 116)),   // teal — audio routing, not a power action
        ("disable-wheels", "Disable wheels",     Rgb(117, 74,  68)),   // muted brown (matches Settings)
        ("safe-mode",      "Toggle Passthru Mode",   Rgb(122, 88,  52)),   // warm ochre — caution-adjacent sibling of Disable Wheels, its neighbour in the Radiata group
        ("obs",            "OBS Studio",         Rgb(59,  66,  79)),   // OBS slate (L .27, inside the base band)
        ("discord-launch", "Discord launch",     Rgb(35,  85, 140)),   // blue
        ("discord-join",   "Discord join",       Rgb(35,  85, 140)),   // blue
        ("discord-mute",   "Discord mute",       Rgb(50, 101, 169)),   // blue
        ("discord-deafen", "Discord deafen",     Rgb(35,  85, 140)),   // blue
        ("steam-mute",     "Steam mic mute",     Rgb(42,  71,  94)),   // steam slate-blue (brand navy, lifted into the base band)
        ("text-chat",      "Text Chat",          Rgb(50, 101, 169)),   // same blue family as System/Discord — a chat-adjacent action
        ("game-browser",   "Game Grid",          Rgb(84,  64, 140)),   // indigo
        // Arcade: the palette's brick red (Exit app's swatch), distinct from Game Grid's indigo beside it.
        // Per-game tints come from ArcadeCatalog; this is the type-level fallback and the Launcher slice's colour.
        ("arcade",         "Arcade",             Rgb(140, 58,  58)),
        ("sequence",       "Sequence",           Rgb(60, 110, 116)),   // teal-slate
        ("xbox-emulation",     "Xbox emulation",      Rgb(46, 104,  46)),   // xbox green
        ("dualshock-emulation","DualShock emulation", Rgb(40,  80, 150)),   // playstation blue
        ("settings",       "Settings / other",   Rgb(60,  62,  74)),   // slate
    ];

    private static readonly Dictionary<string, Color> DefaultMap =
        Defaults.ToDictionary(d => d.Type, d => d.Default, StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-system-command default tints (baked from the tuned defaults). Commands not listed
    /// fall back to the System type colour. Used by <see cref="EffectiveColor(string?,string?)"/> and the
    /// Defaults editor's per-command rows.</summary>
    private static readonly Dictionary<string, Color> SystemCommandDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["reboot"]  = Rgb(136, 48, 44), ["shutdown"] = Rgb(136, 48, 44),
        ["logout"]  = Rgb(136, 48, 44),                                         // warm red — power / exit
        ["lock"]    = Rgb(60, 62, 74),                                          // slate — locking isn't destructive like the power reds
        ["show-desktop"] = Rgb(70, 82, 96),                                     // blue-grey — window management, distinct from the power reds
        ["volume"]  = Rgb(60, 62, 74),  ["mic-mute"] = Rgb(60, 62, 74),         // slate — audio mutes
        ["media-play-pause"] = Rgb(118, 122, 134), ["media-next"] = Rgb(118, 122, 134),
        ["media-prev"]       = Rgb(118, 122, 134), ["media-mute"] = Rgb(118, 122, 134),   // soft slate-gray — media keys (subtler than the audio mutes)
        // display-extend/clone/external/internal and every nvidia-*/amd-* command are removed from the
        // offered taxonomy (display-toggle replaced them; no migration) — no baked default exists for them;
        // SystemCommandDefault falls back to the plain System type colour for a legacy slice still using
        // one. Their Categories entries stay (as Hidden) purely so such a slice's type can't be silently
        // rewritten on the next edit.
        ["display-toggle"]   = Rgb(60, 110, 116),                               // teal — display
        ["hdr-toggle"]       = Rgb(60, 110, 116),                               // teal — display
        ["focus-assist"]     = Rgb(86, 82, 140),                                // night indigo — Do Not Disturb
        ["gamebar-open"]        = Rgb(45, 108, 62), ["gamebar-screenshot"]  = Rgb(45, 108, 62),
        ["gamebar-record"]      = Rgb(45, 108, 62), ["gamebar-record-last"] = Rgb(45, 108, 62),
        ["gamebar-mic"]         = Rgb(45, 108, 62),                             // Xbox green — Game Bar
        ["steam-chat-open"]     = Rgb(42, 71, 94),                              // steam slate-blue — matches the steam-mute type
        ["xbox-party-open"]     = Rgb(45, 108, 62), ["xbox-party-mute"] = Rgb(45, 108, 62),   // Xbox green — party chat lives in Game Bar
        ["empty-recycle-bin"]   = Rgb(136, 48, 44),                            // warm red — destructive
        ["power-plan"]          = Rgb(150, 110, 30),                           // amber — power
    };

    /// <summary>Effective default colour for a system command: its per-command baked default, else the
    /// System type colour.</summary>
    public static Color SystemCommandDefault(string command) =>
        SystemCommandDefaults.TryGetValue(command, out var c) ? c : DefaultMap["system"];

    private static Dictionary<string, Color> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Color, Brush> BrushCache = new();

    /// <summary>Apply user colour overrides (type → "#RRGGBB"); blank/invalid entries are ignored.</summary>
    public static void SetOverrides(IReadOnlyDictionary<string, string>? colors)
    {
        var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        if (colors is not null)
            foreach (var kv in colors)
                if (TryParseHex(kv.Value, out var c)) map[kv.Key] = c;
        _overrides = map;
    }

    /// <summary>The user's colour override for an arbitrary key (e.g. "category:Launch"), or null if
    /// unset — so callers with their own built-in default can apply override-else-default.</summary>
    public static Color? OverrideColor(string? key) =>
        key is not null && _overrides.TryGetValue(key, out var c) ? c : null;

    /// <summary>Effective opaque colour for an action type (override → default → slate). For swatches.</summary>
    public static Color EffectiveColor(string? type)
    {
        var key = type ?? "settings";
        if (_overrides.TryGetValue(key, out var o)) return o;
        return DefaultMap.TryGetValue(key, out var d) ? d : DefaultMap["settings"];
    }

    /// <summary>As <see cref="EffectiveColor(string?)"/> but honours the per-launcher brand colour
    /// (special-cased) for the "launcher" type, keyed by its <paramref name="command"/>.</summary>
    public static Color EffectiveColor(string? type, string? command)
    {
        if (string.Equals(type, "launcher", StringComparison.OrdinalIgnoreCase)
            && LauncherCatalog.Find(command) is { } li)
        {
            if (_overrides.TryGetValue("launcher:" + li.Key, out var o)) return o;   // per-launcher override
            return li.Color;                                                          // per-launcher default
        }
        // System commands take a per-command tint: the user's "system:<command>" override if set, else the
        // per-command baked default (SystemCommandDefault), else the System type colour.
        if (string.Equals(type, "system", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(command))
        {
            var cmd = command!.ToLowerInvariant();
            if (_overrides.TryGetValue("system:" + cmd, out var sc)) return sc;
            if (SystemCommandDefaults.TryGetValue(cmd, out var d)) return d;
        }
        // An arcade slice naming a game takes that game's catalog tint (same source MonoIcons takes its
        // glyph from, so colour and glyph can't drift). A blank/unknown command is the picker slice and
        // keeps the Arcade type colour. No "arcade:<id>" override key exists — per-game recolouring is a
        // per-slice colour, like every other payload-level tint that isn't a launcher or system command.
        if (string.Equals(type, "arcade", StringComparison.OrdinalIgnoreCase)
            && ArcadeCatalog.Find(command) is { } game && TryParseHex(game.TintHex, out var gc))
            return gc;
        return EffectiveColor(type);
    }

    /// <summary>Frozen, alpha-softened tint brush for a slice: its per-slice IconColor override if
    /// set, otherwise the action-type colour.</summary>
    public static Brush BrushFor(WheelSlice slice) => BrushFor(slice, lightenDefaults: false);

    /// <summary>Which default-tint set the current slice material wants for built-in (non-user-set)
    /// glyph/logo colours: <see cref="Light"/> = the base defaults as-is (light materials);
    /// <see cref="Dark"/> = brightness-inverted near-white (dark materials); <see cref="Terra"/> = the
    /// in-between set for the terra material — close to the Dark set's brightness but keeping most of the
    /// base set's saturation, so tints read on warm cream without washing out to grey.</summary>
    public enum TintSet { Light, Dark, Terra }

    /// <summary>Material → the tint set its built-in glyph defaults should use.</summary>
    public static TintSet TintSetFor(string? material) =>
        // "terra"/"paper" are Mesa's two pre-rename tokens; an un-migrated config must still get the
        // Terra tint set, so match all three.
        material is "mesa" or "terra" or "paper" ? TintSet.Terra
            : Materials.IsDark(material) ? TintSet.Dark : TintSet.Light;

    /// <summary>As <see cref="BrushFor(WheelSlice)"/>, but on a dark slice material the app's built-in
    /// (non-user-set) tints get their brightness inverted (HSL lightness flipped, hue + saturation kept)
    /// so glyphs/logos read on a dark slice. User-set colours (per-slice or per-type/launcher overrides)
    /// are left exactly as chosen.</summary>
    public static Brush BrushFor(WheelSlice slice, bool lightenDefaults) =>
        BrushFor(slice, lightenDefaults ? TintSet.Dark : TintSet.Light);

    /// <summary>Tri-state variant: <see cref="TintSet.Terra"/> applies <see cref="TerraEquivalent"/> — to
    /// swatch picks and built-in defaults alike — instead of the dark-material near-white transform.</summary>
    public static Brush BrushFor(WheelSlice slice, TintSet set)
    {
        var rgb = DisplayColor(slice, set);

        var c = Color.FromArgb(Alpha, rgb.R, rgb.G, rgb.B);
        if (!BrushCache.TryGetValue(c, out var b))
        {
            var s = new SolidColorBrush(c);
            s.Freeze();
            BrushCache[c] = b = s;
        }
        return b;
    }

    /// <summary>The exact opaque colour the wheel paints this slice's glyph with, on a light or dark
    /// material: the slice's authored per-slice colour, else the type/launcher/command colour, either way
    /// given the material's variant unless the authored colour is an exact (typed) one. The Settings icon
    /// preview uses this so it always matches the wheel.</summary>
    public static Color DisplayColor(WheelSlice slice, bool darkMaterial) =>
        DisplayColor(slice, darkMaterial ? TintSet.Dark : TintSet.Light);

    /// <summary>Tri-state variant. The model:
    /// <b>one authored colour per slice, always material-neutral, plus one flag saying whether it's exact.</b>
    /// <para><see cref="WheelSlice.IconColor"/> is either a palette swatch's base tone or the hex the user
    /// typed — never a material variant, so it can be re-derived for any material and round-trips through a
    /// save unchanged. <see cref="WheelSlice.IconColorExact"/> says which: typed hex = exact intent,
    /// painted verbatim on every material, no transform at all — ask for #3265A9 and you get #3265A9, not
    /// terra's brightened cousin. A swatch pick is not exact and takes the material variant, exactly as a
    /// built-in default would.</para>
    /// <para><c>IconColorLight</c> is read-only legacy (a retired second field that stored the
    /// dark-material half); see <see cref="IsExactColor"/> for how an old slice's appearance is
    /// preserved. Never store a material variant back into <c>IconColor</c>.</para>
    /// <para><b>Type-derived</b> colours (the action type's own colour) are always a base tone that gets
    /// the material-appropriate variant, exactly as a swatch does — a Color Default behaves identically
    /// whether it came from the table or the dialog.</para></summary>
    public static Color DisplayColor(WheelSlice slice, TintSet set)
    {
        var authored = AuthoredColor(slice, out bool perSlice);
        // perSlice gates exactness only — only a per-slice colour can be a typed hex; MaterialVariant
        // alone decides which transform applies.
        return MaterialVariant(authored, set, exact: perSlice && IsExactColor(slice));
    }

    /// <summary>The slice's authored, material-neutral colour — before any material variant: its per-slice
    /// <see cref="WheelSlice.IconColor"/> if it carries one, else the type/launcher/command colour. This is
    /// the value config stores and the one safe to derive from (see <see cref="MesaRimColor"/>); the colour
    /// actually painted is <see cref="DisplayColor(WheelSlice, TintSet)"/>.</summary>
    public static Color AuthoredColor(WheelSlice slice) => AuthoredColor(slice, out _);

    /// <summary>As <see cref="AuthoredColor(WheelSlice)"/>, also reporting whether the colour came from the
    /// slice itself (<paramref name="perSlice"/> true) or was inherited from its action type — which is what
    /// decides whether <see cref="IsExactColor"/> can apply at all, since only a per-slice colour can have
    /// been typed.</summary>
    public static Color AuthoredColor(WheelSlice slice, out bool perSlice)
    {
        if (slice.IconColor is not null && TryParseHex(slice.IconColor, out var oc))
        {
            perSlice = true;
            return oc;
        }
        perSlice = false;
        return EffectiveColor(slice.Action?.Type?.ToLowerInvariant(), slice.Action?.Command);
    }

    /// <summary>Below this HSL saturation an authored colour has no usable hue to match, so
    /// <see cref="MesaRimColor"/> declines and the caller keeps Mesa's fixed umber. Without the guard a grey
    /// pick (#808080 → S 0, and <see cref="ToHsl"/> reports hue 0 for any achromatic colour) would come back
    /// as a dark red rim — a hue the user never chose.</summary>
    private const double MesaRimMinSaturation = 0.06;

    /// <summary>Mesa's per-slice icon-rim colour: the authored colour's own hue at a fixed dark-umber
    /// lightness and saturation, so every slice's rim belongs to its own action type.
    /// <para>Deliberately driven by <see cref="AuthoredColor(WheelSlice)"/>, not by the painted
    /// <see cref="DisplayColor(WheelSlice, TintSet)"/>: terra's transform floors lightness at 0.76, so
    /// deriving from the painted colour would feed back the pastel's already-lifted L and every rim would
    /// converge on the same tone. Hue survives both transforms unchanged, which is why taking it from the
    /// authored value costs nothing.</para>
    /// <para>Returns null for an achromatic colour — see <see cref="MesaRimMinSaturation"/>.</para></summary>
    public static Color? MesaRimColor(Color authored)
    {
        var (h, s, _) = ToHsl(authored);
        if (s < MesaRimMinSaturation) return null;
        return FromHsl(255, h, MesaRimSaturation, MesaRimLightness);
    }

    /// <summary>The fixed S/L the Mesa rim hue is rendered at: dark enough to read as an outline on the
    /// cream wedge, saturated enough that the action type's hue is unmistakable.</summary>
    private const double MesaRimSaturation = 0.62, MesaRimLightness = 0.18;

    /// <summary>Whether a slice's authored colour is exact (typed into the hex box) and must therefore paint
    /// verbatim on every material. The one place this is decided — never inline the expression.
    /// <para>Legacy read (no migration pass — old slices keep their appearance): a slice written before
    /// <see cref="WheelSlice.IconColorExact"/> existed is judged by whether it carries the retired
    /// <c>IconColorLight</c> pairing — present = it was a swatch pick, absent = it was a typed hex.</para></summary>
    public static bool IsExactColor(WheelSlice slice) =>
        slice.IconColorExact ?? (slice.IconColor is not null && slice.IconColorLight is null);

    /// <summary>Map an authored, material-neutral colour to the colour the wheel actually paints on
    /// <paramref name="set"/> — the single implementation of the variant table, shared by
    /// <see cref="DisplayColor(WheelSlice, TintSet)"/> and by preview surfaces (the Settings icon well) that
    /// have a colour but no slice.
    /// <para><paramref name="exact"/> short-circuits everything: a typed hex is intent, not a starting point.
    /// <para><b>Mesa applies one transform to every non-exact colour</b> (<see cref="TerraEquivalent"/>),
    /// whether it came from a swatch or from an action type's built-in default — a swatch pick is a base
    /// tone, exactly like a default; a typed hex paints verbatim.</para>
    /// <para>Never write the result back to config: it's a transform, so round-tripping it through a save
    /// would re-apply itself every time.</para></summary>
    public static Color MaterialVariant(Color authored, TintSet set, bool exact)
    {
        if (exact) return authored;
        return set switch
        {
            TintSet.Dark  => InvertLightness(authored),
            TintSet.Terra => TerraEquivalent(authored),
            _             => authored,   // Light — the base tones are authored for light materials
        };
    }

    /// <summary>The "light equivalent" of a colour for dark backgrounds — the exact transform used to
    /// lighten built-in glyph tints on dark slice materials (a light-grey wash that keeps a whisper of the
    /// original hue). Exposed for the colour picker's light-swatch row so dark-mode users can pick glyph
    /// tints that read on a dark slice.</summary>
    public static Color LightEquivalent(Color c) => InvertLightness(c);

    /// <summary>Lighten a colour toward white by <paramref name="amount"/> (0..1) while keeping hue and
    /// saturation exactly as-is — unlike <see cref="InvertLightness"/>, which also desaturates. Used by
    /// Salvage's armed icon/logo lighten, where the glyph should read brighter but stay recognisably the
    /// same colour, not wash toward grey.</summary>
    public static Color Lighten(Color c, double amount)
    {
        // Memoized: the wheel resolves this per armed slice per frame (salvage wash), and the HSL
        // round-trip is pure. Inputs are authored colours × a handful of fixed amounts — tiny key space.
        if (LightenCache.TryGetValue((c, amount), out var hit)) return hit;
        var (h, s, l) = ToHsl(c);
        l = l + (1 - l) * Math.Clamp(amount, 0, 1);
        return LightenCache[(c, amount)] = FromHsl(c.A, h, s, l);
    }
    private static readonly Dictionary<(Color c, double amt), Color> LightenCache = new();

    /// <summary>Invert a colour's brightness (HSL lightness → 1−L) while preserving hue, then push it
    /// toward near-white: raise lightness to a high floor and cut saturation hard so a dark default tint
    /// becomes very light grey with only a whisper of its hue. On dark materials the built-in
    /// per-action-type default glyphs must read as near-white with a hue whisper — not a lighter version
    /// of the saturated colour; the hue is kept so each action type still carries its identity.</summary>
    private static Color InvertLightness(Color c)
    {
        if (InvertCache.TryGetValue(c, out var hit)) return hit;   // memoized — per-frame callers
        var (h, s, l) = ToHsl(c);
        // Light grey with a hue tint: a fairly high lightness floor (L ≈ 0.75) so tints read as light,
        // and a moderate saturation cut (×0.5, clamped ≤ 0.40) so the hue is still visible.
        l = Math.Max(1 - l, 0.75);   // invert brightness, then clamp up to a light-grey floor
        s = Math.Min(s * 0.5, 0.40);
        return InvertCache[c] = FromHsl(c.A, h, s, l);
    }
    private static readonly Dictionary<Color, Color> InvertCache = new();

    /// <summary>The Terra material's default-tint transform — the third set between Light and Dark:
    /// brightness close to the Dark set's (L floor 0.76 vs its 0.75) but keeping most of the base set's
    /// saturation (×0.95, cap 0.85, vs Dark's ×0.5 ≤ 0.40) — candy-pastel tints that read on the warm
    /// cream wedges without washing out to grey. Don't amplify further: ×1.12/cap 0.90 reads darker and
    /// over-saturated.</summary>
    private static Color TerraEquivalent(Color c)
    {
        if (TerraCache.TryGetValue(c, out var hit)) return hit;   // memoized — per-frame callers
        var (h, s, l) = ToHsl(c);
        l = Math.Max(1 - l, 0.76);
        s = Math.Min(s * 0.95, 0.85);
        return TerraCache[c] = FromHsl(c.A, h, s, l);
    }
    private static readonly Dictionary<Color, Color> TerraCache = new();

    // Don't reintroduce a provenance-dependent second Mesa transform: a swatch
    // pick and a built-in default are both base tones, and which one a slice happens to hold is invisible to
    // the person looking at the wheel. A multiplicative lightness boost also undershoots on the deliberately
    // dark Defaults band (L ≈ 0.25–0.45) — only an inverting transform with a floor stays pastel on Mesa.
    private static (double h, double s, double l) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0, h = 0, s = 0, d = max - min;
        if (d > 1e-6)
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if      (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else               h = (r - g) / d + 4;
            h /= 6;
        }
        return (h, s, l);
    }

    private static Color FromHsl(byte a, double h, double s, double l)
    {
        if (s <= 1e-6)
        {
            byte v = (byte)Math.Round(l * 255);
            return Color.FromArgb(a, v, v, v);
        }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        return Color.FromArgb(a,
            (byte)Math.Round(HueToRgb(p, q, h + 1.0 / 3) * 255),
            (byte)Math.Round(HueToRgb(p, q, h)           * 255),
            (byte)Math.Round(HueToRgb(p, q, h - 1.0 / 3) * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Strictly #RGB / #RRGGBB / #AARRGGBB. Deliberately not a pass-through to
    /// <c>ColorConverter.ConvertFromString</c>, which also accepts named colours, <c>sc#</c> float syntax,
    /// and <c>ContextColor &lt;uri&gt; …</c> — that last one names an ICC profile to load, so a colour field in a
    /// hand-edited or restored config could point WPF at a URL or a UNC share during icon tinting, on the UI
    /// thread. Every colour Radiata writes is plain hex (see <see cref="ToHex"/>), so nothing legitimate
    /// needs the wider grammar.</summary>
    private static readonly Regex HexColorRe =
        new(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

    public static bool TryParseHex(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var t = hex.Trim();
        // Memoized (hits and misses both): AuthoredColor re-parses each slice's stored hex on every
        // resolve, which the wheel does several times per slice per frame. Concurrent — config
        // sanitize paths can run off the UI thread.
        if (HexCache.TryGetValue(t, out var hit))
        {
            color = hit ?? default;
            return hit is not null;
        }
        if (!HexColorRe.IsMatch(t)) { HexCache[t] = null; return false; }
        try { color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(t); }
        catch { HexCache[t] = null; return false; }
        HexCache[t] = color;
        return true;
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Color?> HexCache = new();

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);
}
