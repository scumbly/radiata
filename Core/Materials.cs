namespace ControllerWheel;

/// <summary>The slice-material vocabulary, in one place (config value <c>SystemConfig.SliceMaterial</c>).
/// Renderer looks live in RadialMenuControl; the Customize tiles, onboarding Material step, and every
/// dark/light-dependent consumer (glyph tinting, icon-well preview, Game Grid tone) query here so a new
/// material can't silently fall through someone's hardcoded <c>is "flat-dark" or "obsidian"</c> check.
/// <see cref="Normalize"/> is the one legacy-alias resolver and lives here (not in the WPF shell) so
/// <c>ConfigLoader</c> — which can't reference WPF — can canonicalize on load.</summary>
public static class Materials
{
    // ⚠ Stored tokens are not the member names or the UI wording: GlossLight = "pearl", GlossDark =
    // "obsidian", Terra = "mesa" (the C# names stay greppable against the old vocabulary). Older configs
    // carry legacy tokens ("gloss-light" / "gloss-dark" / "terra" / "paper"), canonicalized on load by
    // ConfigLoader.Sanitize via Normalize below — but the predicates here still accept them too, since a
    // material can arrive from somewhere Sanitize never saw (a SystemConfig built in code, a test).
    public const string FlatLight  = "flat-light";
    public const string GlossLight = "pearl";
    public const string FlatDark   = "flat-dark";
    public const string GlossDark  = "obsidian";
    public const string Kawaii    = "kawaii";       // pastel "dream sky" wedges (hue walks the ring), cloud hub, confetti fire
    public const string Salvage    = "salvage";       // solid charcoal, rusted-metal texture, stamped stepped-plate edge, armed icon glow + cast shadow
    public const string Terra      = "mesa";          // cream hand-cut wedges over a warm terracotta backdrop
    public const string Reactor    = "reactor";       // hollow outlined wedges; armed slice fuses with the hub (ripple)

    /// <summary>Every valid material, in Customize-tile display order (8 = a 4×2 grid).</summary>
    public static readonly string[] All =
        [FlatLight, GlossLight, FlatDark, GlossDark, Kawaii, Salvage, Terra, Reactor];

    // ── Custom material packages (drop-in themes; see Core\Packages) ────────────────────────────
    // Registered at startup, before the ConfigLoader is constructed. Later the set only grows by an
    // approval and shrinks by an Uninstall; a token's content never changes within a run (the
    // renderer's resting-slice bake key trusts the token string). Only consented packages are ever
    // registered, and tokens carry the "custom-" prefix, which no built-in or legacy alias uses, so
    // registration can never shadow a canonical token.
    private static Dictionary<string, MaterialPackage> _custom = new();

    /// <summary>Register the consented custom materials for this run. Call once, before config
    /// load; later calls replace the set (only <c>PackageInstallFlow</c>, after an approval — the
    /// renderer re-reads on the next SetSliceMaterial). Never re-register a token with different
    /// content in the same run.</summary>
    public static void RegisterCustom(IEnumerable<MaterialPackage> packages)
    {
        var map = new Dictionary<string, MaterialPackage>(StringComparer.Ordinal);
        foreach (var p in packages)
            if (p.Token.StartsWith(MaterialPackage.TokenPrefix, StringComparison.Ordinal))
                map[p.Token] = p;
        _custom = map;
    }

    /// <summary>The registered custom materials, in registration order (Customize appends these
    /// after the eight built-in tiles).</summary>
    public static IReadOnlyCollection<MaterialPackage> Custom => _custom.Values;

    /// <summary>The package behind a custom token, or null. Total and cheap — hot paths may call it.</summary>
    public static MaterialPackage? CustomFor(string? m) =>
        m is not null && _custom.TryGetValue(m, out var p) ? p : null;

    public static bool IsCustom(string? m) => m is not null && _custom.ContainsKey(m);

    /// <summary>Drop one custom material from the registry (Settings ▸ Customize's Uninstall). Swaps in a
    /// new map rather than mutating the live one, because render paths read it. The caller moves the
    /// config off the token first; anything still naming it then degrades to Pearl through
    /// <see cref="Normalize"/>, the same path as a package that failed to load.</summary>
    public static void UnregisterCustom(string token)
    {
        if (!_custom.ContainsKey(token)) return;
        var map = new Dictionary<string, MaterialPackage>(_custom, StringComparer.Ordinal);
        map.Remove(token);
        _custom = map;
    }

    /// <summary>Whether a material counts as dark for everything downstream of the wheel itself:
    /// built-in glyph-tint lightening, the slice editor's icon-well preview, and the Game Grid tone.
    /// The legacy tokens "stencil" (Salvage) and "gloss-dark" (Obsidian) must classify dark too, or an
    /// un-migrated config gets dark wedges with light-material glyph tints on them.</summary>
    public static bool IsDark(string? m) =>
        m is FlatDark or GlossDark or Salvage or Reactor or "stencil" or "gloss-dark"
        || (CustomFor(m) is { } c && c.Dark);

    /// <summary>⚠ Answers only for the styled four (Kawaii, Mesa, Salvage, Reactor) — the looks with their
    /// own textures, hubs, dwell animations and sound pairings — not "is this a premium material" in the
    /// wider sense docs/MATERIALS.md uses. Their preview tiles share a treatment (coloured bottom edge,
    /// lifted label). Legacy tokens count too, so an un-migrated config classifies correctly.</summary>
    public static bool IsPremium(string? m) =>
        m is Kawaii or Terra or Salvage or Reactor
          or "sparkle" or "terra" or "paper" or "stencil" or "frost-light" or "frost-dark";

    /// <summary>True for a known material string (case-sensitive — config values are canonical).
    /// Registered custom tokens are valid too; an unregistered (removed/unconsented/declined)
    /// custom token is not, so Normalize degrades it to Pearl exactly like a legacy alias.</summary>
    public static bool IsValid(string? m) =>
        (m is not null && Array.IndexOf(All, m) >= 0) || IsCustom(m);

    /// <summary>Map any stored material token — every legacy alias included — onto its canonical name.
    /// Total: unknown input falls back to Pearl, so a caller never has to null-check the result.
    /// <para>Lives in Core so <c>ConfigLoader.Sanitize</c> can run it on load (that file can't reference
    /// the WPF assembly): canonicalizing at the config boundary makes the dozens of raw <c>mat is "mesa"</c>
    /// comparisons downstream correct by construction instead of each needing its own normalize call.
    /// <c>RadialMenuControl.NormalizeMaterial</c> is a thin delegate to this.</para></summary>
    public static string Normalize(string? material)
    {
        var m = material?.Trim().ToLowerInvariant();
        return m switch
        {
            "flat" or "flat-white" or "flat-light" => "flat-light",
            "flat-dark"                            => "flat-dark",
            // "gloss-dark" and the gloss-black-* A/B/C previews are all Obsidian's legacy tokens.
            "obsidian" or "gloss-dark" or "gloss-black-a" or "gloss-black-b" or "gloss-black-c" => "obsidian",
            // frost-* and Kawaii's own "sparkle" resolve to Kawaii.
            "frost-light" or "frost-dark" or "sparkle" => "kawaii",
            "stencil"                              => "salvage",   // Salvage's legacy token
            "mesa" or "terra" or "paper"           => "mesa",      // "terra" / "paper" are Mesa's legacy tokens
            _ when IsValid(m)                      => m!,          // canonical tokens pass through
            // Pearl is the fallback, so it also catches its own pre-rename token "gloss-light".
            _                                      => GlossLight,
        };
    }

    /// <summary>The sound set a material pairs with — what config token "material" (follow the material)
    /// resolves to, and the only copy of that split: the onboarding Material step, Settings ▸ Customize,
    /// and Sfx's resolver all call here. Each styled material ships its own set (kawaii / mesa / salvage /
    /// reactor); Pearl takes "digital", Obsidian "obsidian" (Digital pitched down), the flats "physical".
    /// The return value is a resolved set name, not a storable SoundTheme token — config stores only
    /// "material" / "digital" / "physical".
    /// ⚠ Never test the token by shape (a <c>StartsWith("gloss")</c> prefix matches no canonical token and
    /// silently inverts the pairing); matching the <see cref="GlossLight"/>/<see cref="GlossDark"/>
    /// constants makes the next rename break the build, not the behaviour.</summary>
    public static string SoundThemeFor(string? material) => Normalize(material) switch
    {
        Kawaii                  => "kawaii",
        Terra                   => "mesa",
        Salvage                 => "salvage",
        Reactor                 => "reactor",
        GlossLight              => "digital",
        GlossDark               => "obsidian",   // Digital, pitched down (Sfx.SetObsidian)
        var t when CustomFor(t) is { } c => c.SoundTheme,   // already a resolved set name (validated at parse)
        _                       => "physical",
    };
}
