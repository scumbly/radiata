namespace ControllerWheel;

/// <summary>
/// The one registry of arcade games. The slice editor, the in-wheel Add picker, the executor and the Arcade
/// picker all read it, so adding a game is one entry here plus its renderer.
/// </summary>
public static class ArcadeCatalog
{
    /// <summary><paramref name="Id"/> is the token a slice stores in <c>ActionConfig.Command</c> — shipped,
    /// so never change one. <paramref name="Glyph"/> is a MaterialDesign icon name and
    /// <paramref name="TintHex"/> the slice's default tint, so a game added from the couch needs no glyph
    /// picker. <paramref name="Subtitle"/> is the one-line genre blurb the Arcade picker shows under the
    /// selected title.</summary>
    /// <para><paramref name="Hidden"/> = still resolvable but never offered. A hidden game keeps working for
    /// anyone whose slice already names it, and stays constructible so its snapshot and renderer are reachable
    /// — it simply doesn't appear in the editor, the Add picker or the Arcade picker. Deleting the entry
    /// instead would make those slices resolve to nothing, which per docs/ACTIONS.md gets their type rewritten
    /// on the next auto-save.</para>
    /// <para><paramref name="CabinetTintHex"/>, <paramref name="PreviewPath"/>, <paramref name="BadgePath"/> and
    /// <paramref name="GlyphPath"/> are a drop-in game's own cabinet colour, seeded screen picture, nameplate
    /// illustration and slice glyph (absolute paths inside its package), all optional; a built-in game brings
    /// cabinet art, a packed preview and an MDI glyph instead and leaves them null.</para></summary>
    public sealed record Entry(string Id, string Title, string Subtitle, string Glyph, string TintHex,
                               Func<IArcadeGame> Create, bool Hidden = false,
                               string? CabinetTintHex = null, string? PreviewPath = null,
                               string? BadgePath = null, string? GlyphPath = null);

    /// <summary>The glyph name a drop-in game with its own glyph PNG carries (<c>ArcadePackage:pkg-id</c>). The
    /// icon resolver maps it back to <see cref="Entry.GlyphPath"/>, so every glyph consumer — the wheel, the Add
    /// picker, the slice editor — takes it as it takes an MDI name, and nothing stores a file path in config.</summary>
    public const string PackageGlyphPrefix = "ArcadePackage:";

    /// <summary>The Arcade Launcher's glyph name (<c>Assets/arcade/arcade-glyph.png</c>). The art carries its
    /// own "ARCADE" wordmark, so a slice drawing it counts as artwork for label defaults.</summary>
    public const string LauncherGlyphName = "RadiataJoystick";

    /// <summary>Whether a slice draws the Launcher glyph: an <c>arcade</c> slice naming no game on its default
    /// icon, or any slice explicitly set to <see cref="LauncherGlyphName"/>.</summary>
    public static bool IsLauncherGlyphSlice(string? iconName, string? actionType, string? command) =>
        string.Equals(iconName?.Trim(), LauncherGlyphName, StringComparison.OrdinalIgnoreCase)
        || (string.IsNullOrWhiteSpace(iconName)
            && string.Equals(actionType, "arcade", StringComparison.OrdinalIgnoreCase)
            && Resolve(command) is null);

    // Declared before BuiltIn: static initialisers run in textual order.
    private static readonly Entry InternodeEntry =
        new(Internode.GameId, "Internode", Loc.T(UiText.Arcade.InternodeBlurb), "WallSconceRoundVariant", "#56528C", () => new Internode());

    // ⚠ Deleting an Entry does not breach the no-offer/no-rewrite contract in
    // docs/ACTIONS.md. That contract is about the slice's type leaf (`arcade`), which still exists and still
    // resolves; a game id lives in Command, a free-text payload nothing rewrites. A slice naming a deleted
    // game resolves to the picker (Resolve returns null and the host opens the menu), so it opens something
    // playable instead of dead-ending. Deleting an Entry costs a slice its game, never its type.
    /// <summary>Every game, hidden ones included. Resolution goes through here; offering goes through
    /// <see cref="Games"/>. Nothing is hidden at present — the field pair is kept because parking a game
    /// (rather than deleting it) is the reversible move and should stay one flag away.
    /// <para>Internode is absent (not hidden) from a public release: <see cref="ReleaseGates.Internode"/> is the
    /// only registration point, so the picker, the Add picker, the slice editor and <c>OpenArcade</c> all lose it
    /// together, and a slice that names it resolves to the picker like any unknown id. Its code, assets
    /// and strings stay compiled.</para></summary>
    private static readonly Entry[] BuiltIn =
    [
        // Tints are the shared swatch palette's base tones, not free-hand hexes: Kabloom takes the teal
        // (ActionTint's `switch-audio`), Connate the brick red (`exit-app`), Petalpop the amber
        // (`power-plan`), Internode the night indigo (`focus-assist`). Staying on swatch bases keeps them
        // inside the base lightness band, so the dark-material variant lands with the rest; four different
        // hue families because arcade slices are likelier than most to sit side by side on one wheel.
        new(Kabloom.GameId,  "Kabloom",   Loc.T(UiText.Arcade.KabloomBlurb),  "BeeFlower",   "#3C6E74", () => new Kabloom()),
        new(Connate.GameId,  "Connate",   Loc.T(UiText.Arcade.ConnateBlurb),  "StarCircle",  "#8C3A3A", () => new Connate()),
        new(PetalPop.GameId, "Petalpop", Loc.T(UiText.Arcade.PetalPopBlurb), "FlowerPoppy", "#966E1E", () => new PetalPop()),
        .. ReleaseGates.Internode ? new[] { InternodeEntry } : [],
    ];

    /// <summary>Every built-in game's title, gated ones included, in every build flavour: the shell's translation
    /// source set enumerates these so a gated game's name stays translatable.</summary>
    public static IEnumerable<string> BuiltInTitles => BuiltIn.Append(InternodeEntry).Select(e => e.Title).Distinct();

    // Consented drop-in script games, registered at startup and re-registered only after an approval
    // (PackageInstallFlow), like Materials.RegisterCustom; a token's content never changes within a run.
    private static Entry[] _scripts = [];
    private static Entry[]? _all, _games;   // caches — the picker reads Games every frame

    /// <summary>Register the consented script games. Replaces the whole set, like
    /// <c>Materials.RegisterCustom</c> — callers re-pass the full accepted list. Ids arrive already
    /// prefixed (<see cref="ScriptGameManifest.TokenPrefix"/>), so a package can never shadow a built-in;
    /// entries whose token nonetheless collides are dropped.</summary>
    public static void RegisterScripts(IEnumerable<ScriptGameManifest> manifests)
    {
        _scripts = [.. manifests
            .Where(m => m.Token.StartsWith(ScriptGameManifest.TokenPrefix, StringComparison.Ordinal)
                        && !BuiltIn.Any(b => string.Equals(b.Id, m.Token, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(m => m.Token, StringComparer.OrdinalIgnoreCase)
            // Package games share one glyph unless they ship their own, and the neutral steel swatch base — a
            // slice tint should say "drop-in game", not impersonate a built-in's colour. The package's own tint
            // colours only its cabinet in the launcher.
            .Select(m => new Entry(m.Token, m.Title, Loc.T(UiText.Arcade.DropInBlurb),
                                   m.GlyphFile is not null && m.DirPath.Length > 0 ? PackageGlyphPrefix + m.Token : "ScriptText",
                                   "#48505E", () => new ScriptArcadeGame(m),
                                   CabinetTintHex: m.TintHex,
                                   PreviewPath: InPackage(m, m.PreviewFile),
                                   BadgePath: InPackage(m, m.BadgeFile),
                                   GlyphPath: InPackage(m, m.GlyphFile)))];
        _all = null; _games = null;
    }

    private static string? InPackage(ScriptGameManifest m, string? file) =>
        file is not null && m.DirPath.Length > 0 ? Path.Combine(m.DirPath, file) : null;

    public static Entry[] All => _all ??= _scripts.Length == 0 ? BuiltIn : [.. BuiltIn, .. _scripts];

    /// <summary>The games actually offered — registry order is the picker's display order. Put the
    /// friendliest first. Tints sit in ActionTint's base lightness band and are deliberately different hue
    /// families, since arcade slices are likelier than most to end up side by side on one wheel.</summary>
    public static Entry[] Games => _games ??= [.. All.Where(g => !g.Hidden)];

    /// <summary>Resolve an id against every game, hidden included — a parked game's existing slices have to
    /// keep working.</summary>
    public static Entry? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null
        : All.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Resolve a slice's <c>Command</c> to a game. A blank command means "the Arcade picker", and an
    /// unknown id (a config written by a newer build, or a deleted game) resolves the same way rather
    /// than dead-ending — the host opens the picker. Returns null in both cases.</summary>
    public static Entry? Resolve(string? command) => Find(command);

    /// <summary>Create a game by id, or null if the id isn't ours.</summary>
    public static IArcadeGame? Create(string? id) => Find(id)?.Create();
}
