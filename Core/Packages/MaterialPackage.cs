using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// A validated, palette-only custom material loaded from a drop-in package (material.json).
/// Data only — colors as raw ARGB, font as a system family name (resolved, and silently
/// fallen back, WPF-side). No code, no file references, no texture paths: the v1 format is
/// deliberately incapable of expressing anything that could execute, fetch, or decode.
/// Colors render through the flat-material default paths; a package can never reach the
/// styled materials' procedural branches.
/// </summary>
public sealed record MaterialPackage
{
    /// <summary>Config token: "custom-" + manifest id. The prefix guarantees no collision with
    /// the eight built-ins or any legacy alias in <see cref="Materials.Normalize"/>.</summary>
    public required string Token { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    /// <summary>Flips label/hub ink to light and selects the dark flat fallbacks, via Materials.IsDark.</summary>
    public required bool Dark { get; init; }

    // ARGB (0xAARRGGBB). Optional entries carry the documented default when absent.
    public required uint RestingArgb { get; init; }
    public required uint ArmedArgb { get; init; }
    public required uint ConfirmArgb { get; init; }        // default: ArmedArgb
    public required uint? LabelArgb { get; init; }         // default: ink by Dark
    public required uint? OutlineArgb { get; init; }       // default: the standard resting pen

    /// <summary>System-installed font family name, or null for the default label face. The shell
    /// resolves it; an uninstalled name falls back silently — never an error surface.</summary>
    public required string? LabelFont { get; init; }
    /// <summary>The resolved sound set this theme pairs with — one of <see cref="SoundSets"/>. A manifest
    /// may name either a set directly or any built-in material to borrow its pairing (see
    /// <see cref="ResolveSoundTheme"/>); either way what lands here is already a set name. Anything else
    /// was rejected at parse, so <c>Materials.SoundThemeFor</c> can return this unchecked.</summary>
    public required string SoundTheme { get; init; }

    /// <summary>SHA-256 of manifest bytes; consent identity. Content change ⇒ consent re-prompt.</summary>
    public required string ContentHash { get; init; }
    /// <summary>Package folder name (for diagnostics and the consent dialog), not trusted for identity.</summary>
    public required string FolderName { get; init; }

    // ── Format 2 — richer looks, still pure data (see docs/PACKAGES.md ▸ Format roadmap) ─────────
    // Every block is optional; absent = format-1 behavior. All numerics arrive CLAMPED — the shell
    // renders whatever is here without re-validating.
    public FillSpec? RestingFill { get; init; }
    public FillSpec? ArmedFill { get; init; }
    public FillSpec? ConfirmFill { get; init; }
    /// <summary>Kawaii-style per-slice hue walk; when present it overrides Resting/Armed fills.</summary>
    public HueWalkSpec? HueWalk { get; init; }
    public OutlineSpec? Outline { get; init; }         // resting outline (format-1 outline color folds in)
    public OutlineSpec? ArmedOutline { get; init; }
    public MotionSpec? Motion { get; init; }           // armed lift/scale (static state treatment — same Reduce Motion class as Mesa/Kawaii's, kept)
    public GlyphSpec? Glyph { get; init; }             // icon/logo treatments
    public bool LabelUpper { get; init; }              // Salvage-style ALL-CAPS labels
    public double LabelSizeMul { get; init; } = 1.0;   // clamped 0.8–1.3
    public double? GapPx { get; init; }                // absolute inter-slice gap, clamped 0–14
    public TileSpec? Tile { get; init; }               // Customize tile treatment

    // ── Format 3 — textures + sounds (first decoder surface; caps enforced at scan) ──────────────
    // File references are BARE file names inside the package folder (no paths — traversal is
    // impossible by grammar, not by checking). Existence/extension/size are verified by
    // PackageStore.Scan, which also sets DirPath; decoding is shell-side, fail-quiet, capped.
    public TextureSpec? SliceTexture { get; init; }    // drawn over the wedge fill, clipped to it
    public TextureSpec? HubTexture { get; init; }      // drawn over the hub disc
    public TextureSpec? Backdrop { get; init; }        // wheel-wide underlay behind the slices
    /// <summary>Event → wav file name. Keys: armed, fired, enableWheels, disableWheels.</summary>
    public IReadOnlyDictionary<string, string>? Sounds { get; init; }
    /// <summary>Package folder absolute path (set by the scanner; "" during a bare parse).</summary>
    public string DirPath { get; init; } = "";

    public sealed record TextureSpec(string File, bool Tile, double Opacity);

    /// <summary>"solid" (Color), "bowed" (Stops → the Pearl/Obsidian glass ramp), or "linear"
    /// (Stops + Angle degrees).</summary>
    public sealed record FillSpec(string Type, uint Color, IReadOnlyList<(double At, uint Argb)> Stops, double AngleDeg);
    /// <summary>Per-slice HSL walk by slice angle. Sat/Light 0–1; armed variant saturates.</summary>
    public sealed record HueWalkSpec(double Sat, double Light, double ArmedSat, double ArmedLight, double HueOffsetDeg);
    public sealed record OutlineSpec(uint Argb, double Width, IReadOnlyList<double>? Dash);
    public sealed record MotionSpec(double LiftPx, double NorthPx, double Scale);
    /// <summary>Edge: "none" | "inner" | "outer". GlowArgb null + GlowSliceTint=true → glow takes the
    /// slice's own tint color (Salvage/Reactor behavior); strength 0–1 scales the halo alphas.</summary>
    public sealed record GlyphSpec(string Edge, uint EdgeArgb, double EdgeWidth, bool EdgeShadow,
                                   uint? GlowArgb, bool GlowSliceTint, double GlowStrength,
                                   bool CastShadow, bool ArmedWash);
    public sealed record TileSpec(uint? EdgeArgb, bool Sheen, bool Lifted, TextureSpec? Texture = null);

    public const int FormatVersion = 1;
    public const int MaxFormatVersion = 3;
    public static readonly string[] SoundEvents = ["armed", "fired", "enableWheels", "disableWheels"];

    /// <summary>Every sound set a theme may pair with — the resolved names <c>Sfx.SetFor</c> understands.
    /// Grows whenever a built-in material gains a set of its own; a manifest naming the material instead
    /// keeps working across such a change, which is why <see cref="ResolveSoundTheme"/> accepts both.</summary>
    public static readonly string[] SoundSets =
        ["physical", "digital", "kawaii", "mesa", "salvage", "reactor", "obsidian"];

    /// <summary>Manifest <c>soundTheme</c> → the sound set it means, or null if the value is neither a
    /// set name nor a built-in material token. Naming a material (e.g. "mesa", or "pearl" for the digital
    /// set) borrows whatever that material currently pairs with, so a theme tracks the built-in it was
    /// styled after. ⚠ Validate before resolving — <c>Materials.SoundThemeFor</c> is total and silently
    /// answers "digital" for junk, so piping unchecked input through it would accept anything. Custom
    /// tokens are deliberately not accepted: a theme must not pair off another theme.</summary>
    public static string? ResolveSoundTheme(string? value) =>
        value is null                                  ? null
        : Array.IndexOf(Materials.All, value) >= 0     ? Materials.SoundThemeFor(value)
        : Array.IndexOf(SoundSets, value) >= 0         ? value
        : null;
    public const string ManifestFileName = "material.json";
    public const string TokenPrefix = "custom-";

    /// <summary>
    /// Parse + validate a manifest. Returns null with a reason instead of throwing: a bad package
    /// must never crash or degrade the app — it is listed as "couldn't load" and skipped.
    /// Every string is length-bounded and control-character-stripped; every color must parse;
    /// unknown JSON properties are ignored (forward compatibility), unknown format versions are not.
    /// </summary>
    public static MaterialPackage? TryParse(string manifestJson, string folderName, string contentHash, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(manifestJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 8,
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "manifest is not a JSON object"; return null; }

            if (!TryGetInt(root, "format", out int format) || format is < FormatVersion or > MaxFormatVersion)
            { error = $"unsupported or missing format (this Radiata reads formats {FormatVersion}-{MaxFormatVersion})"; return null; }

            var id = GetString(root, "id");
            if (id is null || !IdIsValid(id)) { error = "id must be 2-31 chars of a-z, 0-9, '-', starting alphanumeric"; return null; }

            var name = Clean(GetString(root, "name"), 24);
            if (string.IsNullOrWhiteSpace(name)) { error = "name is required (≤24 chars)"; return null; }

            var author = Clean(GetString(root, "author"), 64) ?? "(unknown)";

            // The two remaining required root fields are reported together: one-at-a-time errors send a
            // package author around the fix-relaunch loop once per field, and "dark is required" against a
            // manifest whose real omission is colors reads like a wrong error.
            bool hasDark = root.TryGetProperty("dark", out var darkEl) &&
                           darkEl.ValueKind is JsonValueKind.True or JsonValueKind.False;
            bool hasColors = root.TryGetProperty("colors", out var colors) &&
                             colors.ValueKind == JsonValueKind.Object;
            if (!hasDark || !hasColors)
            {
                error = "missing required: " + (hasDark ? "" : "dark (true/false)")
                      + (!hasDark && !hasColors ? ", " : "")
                      + (hasColors ? "" : "colors (object)");
                return null;
            }
            bool dark = darkEl.GetBoolean();

            if (!TryColor(colors, "resting", out uint resting, required: true, out error)) return null;
            if (!TryColor(colors, "armed", out uint armed, required: true, out error)) return null;
            if (!TryColor(colors, "confirm", out uint confirm, required: false, out error)) return null;
            if (error is not null) return null;
            if (!TryColorOpt(colors, "label", out uint? label, out error)) return null;
            if (!TryColorOpt(colors, "outline", out uint? outline, out error)) return null;
            bool hasConfirm = colors.TryGetProperty("confirm", out _);

            var font = Clean(GetString(root, "labelFont"), 64);
            var sound = ResolveSoundTheme(GetString(root, "soundTheme") ?? "physical");
            if (sound is null)
            {
                error = "soundTheme must be a sound set (" + string.Join(", ", SoundSets)
                      + ") or a built-in material to borrow the pairing of (" + string.Join(", ", Materials.All) + ")";
                return null;
            }

            // ── Format 2 blocks (all optional; each validates or the whole package is rejected —
            // a half-loaded theme would be worse than a skipped one) ──
            FillSpec? restingFill = null, armedFill = null, confirmFill2 = null;
            HueWalkSpec? hueWalk = null;
            OutlineSpec? outlineSpec = null, armedOutline = null;
            MotionSpec? motion = null;
            GlyphSpec? glyph = null;
            TileSpec? tile = null;
            bool labelUpper = false;
            double labelSizeMul = 1.0;
            double? gapPx = null;

            if (format >= 2)
            {
                if (root.TryGetProperty("fills", out var fills) && fills.ValueKind == JsonValueKind.Object)
                {
                    if (!TryFill(fills, "resting", out restingFill, out error)) return null;
                    if (!TryFill(fills, "armed", out armedFill, out error)) return null;
                    if (!TryFill(fills, "confirm", out confirmFill2, out error)) return null;
                }
                if (root.TryGetProperty("hueWalk", out var hw) && hw.ValueKind == JsonValueKind.Object)
                {
                    hueWalk = new HueWalkSpec(
                        Sat: Num(hw, "sat", 0.30, 0, 1),
                        Light: Num(hw, "light", 0.88, 0.05, 0.98),
                        ArmedSat: Num(hw, "armedSat", 0.75, 0, 1),
                        ArmedLight: Num(hw, "armedLight", 0.60, 0.05, 0.98),
                        HueOffsetDeg: Num(hw, "hueOffset", 0, -360, 360));
                }
                if (!TryOutline(root, "outline", out outlineSpec, out error)) return null;
                if (!TryOutline(root, "armedOutline", out armedOutline, out error)) return null;
                if (root.TryGetProperty("armed", out var am) && am.ValueKind == JsonValueKind.Object)
                {
                    motion = new MotionSpec(
                        LiftPx: Num(am, "liftPx", 0, 0, 24),
                        NorthPx: Num(am, "northPx", 0, -12, 12),
                        Scale: Num(am, "scale", 1.0, 1.0, 1.15));
                }
                if (root.TryGetProperty("glyph", out var gl) && gl.ValueKind == JsonValueKind.Object)
                {
                    var edge = GetString(gl, "edge") ?? "none";
                    if (edge is not ("none" or "inner" or "outer"))
                    { error = "glyph.edge must be none, inner, or outer"; return null; }
                    uint edgeArgb = 0xFFFFFFFF;
                    if (gl.TryGetProperty("edgeColor", out var ec))
                    {
                        if (ec.ValueKind != JsonValueKind.String || !TryParseColor(ec.GetString(), out edgeArgb))
                        { error = "glyph.edgeColor must be #RRGGBB or #AARRGGBB"; return null; }
                    }
                    uint? glowArgb = null; bool glowSlice = false; double glowStrength = 0;
                    if (gl.TryGetProperty("glow", out var gw) && gw.ValueKind == JsonValueKind.Object)
                    {
                        glowStrength = Num(gw, "strength", 0.6, 0.05, 1.0);
                        var gc = GetString(gw, "color");
                        if (gc == "slice") glowSlice = true;
                        else if (gc is not null && TryParseColor(gc, out uint g)) glowArgb = g;
                        else { error = "glyph.glow.color must be \"slice\" or a hex color"; return null; }
                    }
                    glyph = new GlyphSpec(edge, edgeArgb,
                        EdgeWidth: Num(gl, "edgeWidth", 3.0, 1.0, 6.0),
                        EdgeShadow: GetBool(gl, "edgeShadow"),
                        glowArgb, glowSlice, glowStrength,
                        CastShadow: GetBool(gl, "castShadow"),
                        ArmedWash: GetBool(gl, "armedWash"));
                }
                if (root.TryGetProperty("label", out var lb) && lb.ValueKind == JsonValueKind.Object)
                {
                    labelUpper = GetString(lb, "case") == "upper";
                    labelSizeMul = Num(lb, "sizeMul", 1.0, 0.8, 1.3);
                }
                if (root.TryGetProperty("gapPx", out var gp) && gp.ValueKind == JsonValueKind.Number)
                    gapPx = Math.Clamp(gp.GetDouble(), 0.0, 14.0);
                if (root.TryGetProperty("tile", out var tl) && tl.ValueKind == JsonValueKind.Object)
                {
                    uint? tileEdge = null;
                    if (tl.TryGetProperty("edgeColor", out var te))
                    {
                        if (te.ValueKind != JsonValueKind.String || !TryParseColor(te.GetString(), out uint t))
                        { error = "tile.edgeColor must be #RRGGBB or #AARRGGBB"; return null; }
                        tileEdge = t;
                    }
                    tile = new TileSpec(tileEdge, Sheen: GetBool(tl, "sheen"), Lifted: GetBool(tl, "lifted"));
                }
            }

            // ── Format 3 blocks ──
            TextureSpec? sliceTex = null, hubTex = null, backdropTex = null;
            Dictionary<string, string>? sounds = null;
            if (format >= 3)
            {
                if (root.TryGetProperty("textures", out var tex) && tex.ValueKind == JsonValueKind.Object)
                {
                    if (!TryTexture(tex, "slice", out sliceTex, out error)) return null;
                    if (!TryTexture(tex, "hub", out hubTex, out error)) return null;
                    if (!TryTexture(tex, "backdrop", out backdropTex, out error)) return null;
                }
                if (root.TryGetProperty("sounds", out var snd) && snd.ValueKind == JsonValueKind.Object)
                {
                    sounds = [];
                    foreach (var p in snd.EnumerateObject())
                    {
                        if (Array.IndexOf(SoundEvents, p.Name) < 0)
                        { error = $"sounds.{p.Name} is not a known event (armed/fired/enableWheels/disableWheels)"; return null; }
                        if (p.Value.ValueKind != JsonValueKind.String ||
                            !FileNameIsValid(p.Value.GetString(), ".wav"))
                        { error = $"sounds.{p.Name} must be a bare .wav file name inside the package folder"; return null; }
                        sounds[p.Name] = p.Value.GetString()!;
                    }
                    if (sounds.Count == 0) sounds = null;
                }
                // Tile texture is a format-3 decoder feature, but "tile" itself is parsed above (format 2)
                // — so it's layered on here rather than duplicating the whole tile block.
                if (tile is not null && root.TryGetProperty("tile", out var tl2) &&
                    tl2.ValueKind == JsonValueKind.Object)
                {
                    if (!TryTexture(tl2, "texture", out var tileTex, out error)) return null;
                    if (tileTex is not null) tile = tile with { Texture = tileTex };
                }
            }

            return new MaterialPackage
            {
                Token = TokenPrefix + id,
                Id = id,
                Name = name!,
                Author = author,
                Dark = dark,
                RestingArgb = resting,
                ArmedArgb = armed,
                ConfirmArgb = hasConfirm ? confirm : armed,
                LabelArgb = label,
                OutlineArgb = outline,
                LabelFont = string.IsNullOrWhiteSpace(font) ? null : font,
                SoundTheme = sound,
                ContentHash = contentHash,
                FolderName = folderName,
                RestingFill = restingFill,
                ArmedFill = armedFill,
                ConfirmFill = confirmFill2,
                HueWalk = hueWalk,
                Outline = outlineSpec,
                ArmedOutline = armedOutline,
                Motion = motion,
                Glyph = glyph,
                LabelUpper = labelUpper,
                LabelSizeMul = labelSizeMul,
                GapPx = gapPx,
                Tile = tile,
                SliceTexture = sliceTex,
                HubTexture = hubTex,
                Backdrop = backdropTex,
                Sounds = sounds,
            };
        }
        catch (JsonException ex) { error = "invalid JSON: " + ex.Message; return null; }
        catch (Exception ex) { error = ex.Message; return null; }
    }

    private static bool IdIsValid(string id)
    {
        if (id.Length is < 2 or > 31) return false;
        if (id[0] == '-' || id[^1] == '-') return false;
        foreach (var c in id)
            if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')) return false;
        return true;
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>Clamped numeric property with a default — format-2 numbers can never leave range.</summary>
    private static double Num(JsonElement el, string prop, double fallback, double min, double max) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? Math.Clamp(v.GetDouble(), min, max) : fallback;

    /// <summary>Parse one fills.* entry: {"type":"solid","color":"#.."} or
    /// {"type":"bowed"|"linear","stops":[{"at":0,"color":"#.."},…],"angle":90}. Absent = null+true.</summary>
    private static bool TryFill(JsonElement fills, string prop, out FillSpec? spec, out string? error)
    {
        spec = null; error = null;
        if (!fills.TryGetProperty(prop, out var f)) return true;
        if (f.ValueKind != JsonValueKind.Object) { error = $"fills.{prop} must be an object"; return false; }
        var type = GetString(f, "type") ?? "solid";
        if (type is not ("solid" or "bowed" or "linear"))
        { error = $"fills.{prop}.type must be solid, bowed, or linear"; return false; }

        uint color = 0;
        var stops = new List<(double, uint)>();
        if (type == "solid")
        {
            if (!f.TryGetProperty("color", out var c) || c.ValueKind != JsonValueKind.String ||
                !TryParseColor(c.GetString(), out color))
            { error = $"fills.{prop}.color is required for solid"; return false; }
        }
        else
        {
            if (!f.TryGetProperty("stops", out var st) || st.ValueKind != JsonValueKind.Array ||
                st.GetArrayLength() is < 2 or > 8)
            { error = $"fills.{prop}.stops must be an array of 2-8 entries"; return false; }
            double lastAt = -1;
            foreach (var s in st.EnumerateArray())
            {
                if (s.ValueKind != JsonValueKind.Object ||
                    !s.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.Number ||
                    !s.TryGetProperty("color", out var sc) || sc.ValueKind != JsonValueKind.String ||
                    !TryParseColor(sc.GetString(), out uint stopColor))
                { error = $"fills.{prop}.stops entries need at (0-1) and color"; return false; }
                double a = Math.Clamp(at.GetDouble(), 0.0, 1.0);
                if (a < lastAt) { error = $"fills.{prop}.stops must be in ascending at order"; return false; }
                lastAt = a;
                stops.Add((a, stopColor));
            }
        }
        spec = new FillSpec(type, color, stops, Num(f, "angle", 90.0, 0.0, 360.0));
        return true;
    }

    /// <summary>Bare file name only — no separators, no "..", no leading dot, sane charset, and the
    /// required extension. Traversal is impossible by grammar rather than by checking paths.</summary>
    internal static bool FileNameIsValid(string? name, params string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64) return false;
        if (name[0] is '.' or ' ' || name[^1] is '.' or ' ') return false;
        foreach (var c in name)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ' ')) return false;
        if (name.Contains("..", StringComparison.Ordinal)) return false;
        foreach (var ext in allowedExtensions)
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool TryTexture(JsonElement textures, string role, out TextureSpec? spec, out string? error)
    {
        spec = null; error = null;
        if (!textures.TryGetProperty(role, out var t)) return true;
        if (t.ValueKind != JsonValueKind.Object) { error = $"textures.{role} must be an object"; return false; }
        var file = GetString(t, "file");
        if (!FileNameIsValid(file, ".png", ".jpg", ".jpeg"))
        { error = $"textures.{role}.file must be a bare .png/.jpg file name inside the package folder"; return false; }
        spec = new TextureSpec(file!, Tile: GetBool(t, "tile"), Opacity: Num(t, "opacity", 0.35, 0.05, 1.0));
        return true;
    }

    private static bool TryOutline(JsonElement root, string prop, out OutlineSpec? spec, out string? error)
    {
        spec = null; error = null;
        if (!root.TryGetProperty(prop, out var o)) return true;
        if (o.ValueKind != JsonValueKind.Object) { error = $"{prop} must be an object"; return false; }
        if (!o.TryGetProperty("color", out var c) || c.ValueKind != JsonValueKind.String ||
            !TryParseColor(c.GetString(), out uint argb))
        { error = $"{prop}.color is required"; return false; }
        List<double>? dash = null;
        if (o.TryGetProperty("dash", out var d))
        {
            if (d.ValueKind != JsonValueKind.Array || d.GetArrayLength() is < 1 or > 4)
            { error = $"{prop}.dash must be 1-4 numbers"; return false; }
            dash = [];
            foreach (var seg in d.EnumerateArray())
            {
                if (seg.ValueKind != JsonValueKind.Number) { error = $"{prop}.dash must be numbers"; return false; }
                dash.Add(Math.Clamp(seg.GetDouble(), 0.5, 20.0));
            }
        }
        spec = new OutlineSpec(argb, Num(o, "width", 1.5, 0.5, 6.0), dash);
        return true;
    }

    private static bool TryGetInt(JsonElement el, string prop, out int value)
    {
        value = 0;
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out value);
    }

    /// <summary>Strip control chars, trim, cap length. Null-in → null-out.</summary>
    private static string? Clean(string? s, int max)
    {
        if (s is null) return null;
        var chars = new System.Text.StringBuilder(Math.Min(s.Length, max));
        foreach (var c in s)
        {
            // Control (Cc) AND Format (Cf): the bidi controls — RLO/LRO, the isolates, the marks — are Format
            // characters, and a name carrying one would visually reorder itself in the consent dialog once a
            // right-to-left language is honoured. A package name never needs them.
            if (char.IsControl(c) || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
            chars.Append(c);
            if (chars.Length >= max) break;
        }
        return chars.ToString().Trim();
    }

    private static bool TryColor(JsonElement colors, string prop, out uint argb, bool required, out string? error)
    {
        argb = 0; error = null;
        if (!colors.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.String)
        {
            if (required) { error = $"colors.{prop} is required"; return false; }
            return true;
        }
        if (!TryParseColor(v.GetString(), out argb)) { error = $"colors.{prop} must be #RRGGBB or #AARRGGBB"; return false; }
        return true;
    }

    private static bool TryColorOpt(JsonElement colors, string prop, out uint? argb, out string? error)
    {
        argb = null; error = null;
        if (!colors.TryGetProperty(prop, out var v)) return true;
        if (v.ValueKind != JsonValueKind.String || !TryParseColor(v.GetString(), out uint parsed))
        { error = $"colors.{prop} must be #RRGGBB or #AARRGGBB"; return false; }
        argb = parsed;
        return true;
    }

    /// <summary>#RGB, #RRGGBB, or #AARRGGBB (leading '#' required). Missing alpha = opaque.</summary>
    internal static bool TryParseColor(string? s, out uint argb)
    {
        argb = 0;
        if (s is null || s.Length is not (4 or 7 or 9) || s[0] != '#') return false;
        var hex = s.AsSpan(1);
        foreach (var c in hex) if (!char.IsAsciiHexDigit(c)) return false;
        if (hex.Length == 3)
        {
            uint r = Nibble(hex[0]), g = Nibble(hex[1]), b = Nibble(hex[2]);
            argb = 0xFF000000u | (r * 17) << 16 | (g * 17) << 8 | (b * 17);
            return true;
        }
        uint value = uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        argb = hex.Length == 6 ? 0xFF000000u | value : value;
        return true;
    }

    private static uint Nibble(char c) => (uint)System.Convert.ToInt32(c.ToString(), 16);
}
