using System.Diagnostics;
using System.Security.Cryptography;

namespace ControllerWheel;

/// <summary>
/// The drop-in package system's discovery layer. Two folders under %APPDATA%\Radiata\Packages —
/// Materials (data-only theme packs) and "Arcade Games" (script games, run by the jailed helper —
/// see docs/PACKAGES.md).
///
/// Scan happens once at startup, before config load. After that the registries change only by a
/// user's approval (adding a package: the startup prompt or a drag-and-drop install, PackageInstaller)
/// or an Uninstall (removing one), and a token's content never changes within a run — the renderer's
/// resting-slice bake key keys on the token string. Editing a package in place requires an app
/// restart — documented in each folder's README.
///
/// Every failure path is fail-quiet: a bad package is reported in the scan result (and traced),
/// never thrown. Nothing here executes, decodes, or fetches package content.
/// </summary>
public static class PackageStore
{
    public static readonly string PackagesDir = Path.Combine(AppPaths.AppDataDir, "Packages");
    public static readonly string MaterialsDir = Path.Combine(PackagesDir, "Materials");
    public static readonly string ArcadeDir = Path.Combine(PackagesDir, "Arcade Games");

    /// <summary>One discovered package folder, valid or not.</summary>
    public sealed record Discovered(
        string Kind,                    // "material" | "arcade"
        string FolderName,
        string FolderPath,
        string ContentHash,             // SHA-256 over the manifest bytes ("" if unreadable)
        MaterialPackage? Material,      // parsed spec (materials only, null if invalid)
        string? Error,                  // why it didn't validate (null = valid)
        ScriptGameManifest? ArcadeGame = null);  // parsed spec (arcade only, null if invalid)

    /// <summary>Create the drop-in folders + READMEs for the package kinds this build offers
    /// (<see cref="ReleaseGates.MaterialPackages"/> / <see cref="ReleaseGates.ArcadePackages"/>); a public
    /// release creates nothing, not even <see cref="PackagesDir"/>. A folder or README left behind by an
    /// earlier build is never removed. Never throws (best-effort, traced).</summary>
    public static void EnsureFolders()
    {
        try
        {
            PackageInstaller.CleanStaging();
            if (ReleaseGates.MaterialPackages)
            {
                Directory.CreateDirectory(MaterialsDir);
                WriteReadme(Path.Combine(MaterialsDir, "README.txt"), MaterialsReadme);
            }
            if (ReleaseGates.ArcadePackages)
            {
                Directory.CreateDirectory(ArcadeDir);
                WriteReadme(Path.Combine(ArcadeDir, "README.txt"), ArcadeReadme);
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[Packages] EnsureFolders failed: {ex.Message}"); }
    }

    /// <summary>
    /// Inventory the offered folders. Each immediate subfolder is one package candidate; loose files are
    /// ignored. A gated kind is not scanned even if its folder exists, so nothing downstream
    /// (<c>Materials.RegisterCustom</c>, <c>ArcadeCatalog.RegisterScripts</c>, the consent prompt) ever
    /// sees a package of that kind - this is the single gate for both package features. Never throws.
    /// </summary>
    public static IReadOnlyList<Discovered> Scan()
    {
        var found = new List<Discovered>();
        if (ReleaseGates.MaterialPackages) ScanKind(found, MaterialsDir, "material");
        if (ReleaseGates.ArcadePackages) ScanKind(found, ArcadeDir, "arcade");
        return found;
    }

    private static void ScanKind(List<Discovered> found, string dir, string kind)
    {
        string[] subdirs;
        try { subdirs = Directory.Exists(dir) ? Directory.GetDirectories(dir) : []; }
        catch (Exception ex) { Trace.WriteLine($"[Packages] scan {kind} failed: {ex.Message}"); return; }

        foreach (var sub in subdirs) found.Add(Inspect(sub, kind));
    }

    /// <summary>Whether <paramref name="dir"/> is a real package folder directly under the Materials or Arcade
    /// Games folder — the precondition for sending it to the Recycle Bin (Uninstall, or Replace on a
    /// drag-and-drop install). A junction or symlink is refused: removing one could reach a folder outside
    /// Packages. Never throws.</summary>
    public static bool IsPackageDir(string dir)
    {
        try
        {
            static string Norm(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar);
            var full = Norm(dir);
            var parent = Path.GetDirectoryName(full);
            return parent is not null
                && (string.Equals(parent, Norm(MaterialsDir), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parent, Norm(ArcadeDir), StringComparison.OrdinalIgnoreCase))
                && Directory.Exists(full)
                && (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0;
        }
        catch { return false; }
    }

    /// <summary>Validate one package folder exactly as <see cref="Scan"/> does — the harness checks the
    /// committed sample packages through this, so a sample passes only if a user's scan would load it.
    /// Ignores the release gates (it reads the folder it is given). Never throws.</summary>
    public static Discovered Inspect(string sub, string kind)
    {
        var folder = Path.GetFileName(sub);
        try
        {
            if (kind == "material")
            {
                var manifest = Path.Combine(sub, MaterialPackage.ManifestFileName);
                if (!File.Exists(manifest))
                    return new(kind, folder, sub, "", null, $"no {MaterialPackage.ManifestFileName}");

                var bytes = File.ReadAllBytes(manifest);
                if (bytes.Length > 64 * 1024)
                    return new(kind, folder, sub, "", null, "manifest over 64 KB");

                // Consent identity = a hash over every top-level file (name + bytes), so a texture or
                // sound swap re-triggers the consent gate exactly like a manifest edit. The same pass
                // enforces the folder-shape caps: ≤32 files, ≤4 MB each, ≤16 MB total, no subfolders'
                // content counted (referenced files must be top-level anyway — bare names by grammar).
                if (HashFolder(sub, out var hash) is { } capError)
                    return new(kind, folder, sub, "", null, capError);

                // Strip a UTF-8 BOM: Notepad and Windows PowerShell both write one, and a rejected
                // manifest over three invisible bytes would be this feature's #1 support question.
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                if (json.Length > 0 && json[0] == '﻿') json = json[1..];
                var spec = MaterialPackage.TryParse(json, folder, hash, out var error);
                if (spec is not null)
                {
                    spec = spec with { DirPath = sub };
                    error = VerifyReferencedFiles(spec);
                    if (error is not null) spec = null;
                }
                return new(kind, folder, sub, hash, spec, error);
            }
            else
            {
                // Arcade: a script-game package. Consent identity is the whole-folder hash (same as
                // materials) — a script swap must re-prompt exactly like a manifest edit. The script
                // itself is never read here; its text is read at session start and shipped over the
                // pipe to the jailed helper. See docs/PACKAGES.md ▸ the Arcade engine milestone.
                var manifest = Path.Combine(sub, ScriptGameManifest.ManifestFileName);
                if (!File.Exists(manifest))
                    return new(kind, folder, sub, "", null, $"no {ScriptGameManifest.ManifestFileName}");

                var bytes = File.ReadAllBytes(manifest);
                if (bytes.Length > 64 * 1024)
                    return new(kind, folder, sub, "", null, "manifest over 64 KB");

                if (HashFolder(sub, out var hash) is { } capError)
                    return new(kind, folder, sub, "", null, capError);

                var json = System.Text.Encoding.UTF8.GetString(bytes);
                if (json.Length > 0 && json[0] == '﻿') json = json[1..];   // Notepad BOM
                var spec = ScriptGameManifest.TryParse(json, folder, hash, out var error);
                if (spec is not null)
                {
                    spec = spec with { DirPath = sub };
                    var entry = Path.Combine(sub, spec.EntryFile);
                    if (!File.Exists(entry))
                    { error = $"entry: {spec.EntryFile} not found in the package folder"; spec = null; }
                    else if (new FileInfo(entry).Length > ScriptGameManifest.MaxEntryFileBytes)
                    { error = $"entry: {spec.EntryFile} is over 256 KB"; spec = null; }
                    else if (new[] { ("preview", spec.PreviewFile), ("badge", spec.BadgeFile), ("glyph", spec.GlyphFile) }
                             .FirstOrDefault(f => f.Item2 is not null && !File.Exists(Path.Combine(sub, f.Item2))) is { Item2: { } missing } named)
                    { error = $"{named.Item1}: {missing} not found in the package folder"; spec = null; }
                }
                return new(kind, folder, sub, hash, null, error, spec);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Packages] {kind} '{folder}': {ex.Message}");
            return new(kind, folder, sub, "", null, ex.Message);
        }
    }

    /// <summary>Whole-folder content hash + shape caps. Returns an error string (hash "" is then never
    /// consentable), or null with the hex hash. Deterministic: files sorted by lowercased name.</summary>
    private static string? HashFolder(string dir, out string hash)
    {
        hash = "";
        var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
        if (files.Length > 32) return "over 32 files in the package folder";
        Array.Sort(files, (a, b) => string.CompareOrdinal(
            Path.GetFileName(a).ToLowerInvariant(), Path.GetFileName(b).ToLowerInvariant()));
        long total = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            if (info.Length > 4 * 1024 * 1024) return $"{info.Name} is over 4 MB";
            total += info.Length;
            if (total > 16 * 1024 * 1024) return "package folder is over 16 MB";
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(info.Name.ToLowerInvariant() + "\n"));
            sha.AppendData(File.ReadAllBytes(f));
        }
        hash = System.Convert.ToHexString(sha.GetHashAndReset());
        return null;
    }

    /// <summary>Every file the manifest references must exist in the package folder within its role's
    /// cap (textures ≤4 MB — the folder pass already enforced it; sounds ≤1 MB). Returns the first
    /// problem, or null. A missing file rejects the package — a theme silently missing its texture
    /// would look like a bug forever.</summary>
    private static string? VerifyReferencedFiles(MaterialPackage spec)
    {
        foreach (var (role, tex) in new[]
                 { ("slice", spec.SliceTexture), ("hub", spec.HubTexture), ("backdrop", spec.Backdrop),
                   ("tile", spec.Tile?.Texture) })
        {
            if (tex is null) continue;
            var p = Path.Combine(spec.DirPath, tex.File);
            if (!File.Exists(p)) return $"textures.{role}: {tex.File} not found in the package folder";
        }
        if (spec.Sounds is not null)
            foreach (var (evt, file) in spec.Sounds)
            {
                var p = Path.Combine(spec.DirPath, file);
                if (!File.Exists(p)) return $"sounds.{evt}: {file} not found in the package folder";
                if (new FileInfo(p).Length > 1024 * 1024) return $"sounds.{evt}: {file} is over 1 MB";
            }
        return null;
    }

    // The READMEs are Radiata's documentation, not user content — kept current on every launch so a
    // format upgrade reaches existing installs (rewrite only on drift; steady-state is a read).
    private static void WriteReadme(string path, string content)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != content) File.WriteAllText(path, content);
    }

    private const string MaterialsReadme =
@"Radiata - custom Material theme packages
=========================================

Drop each theme package into its OWN folder here, then restart Radiata.
The first time Radiata sees a new (or changed) package, it will ask you to
confirm before anything is loaded. You are responsible for content you
install from third parties.

Or drag the theme's folder, or a ZIP of it, onto Radiata's Settings window
(or onto Radiata.exe): it is copied here and you confirm it - no restart.
To remove a theme, right-click its tile in Settings > Customize and choose
Uninstall theme (the folder goes to the Recycle Bin).

A package is one folder containing a material.json. Minimal (format 1):

  {
    ""format"": 1,
    ""id"": ""lava"",
    ""name"": ""Lava"",
    ""author"": ""Your Name"",
    ""dark"": true,
    ""colors"": {
      ""resting"": ""#2A1008"",
      ""armed"":   ""#FF6A00"",
      ""confirm"": ""#FFC800"",
      ""label"":   ""#FFE8D0"",
      ""outline"": ""#40FFFFFF""
    },
    ""labelFont"": ""Cascadia Code"",
    ""soundTheme"": ""physical""
  }

Format 2 adds optional blocks for richer looks (any may be omitted):

  ""fills"": {
    ""resting"": { ""type"": ""bowed"",
      ""stops"": [ {""at"": 0, ""color"": ""#3A1810""},
                 {""at"": 0.5, ""color"": ""#2A1008""},
                 {""at"": 1, ""color"": ""#200A05""} ] },
    ""armed"":  { ""type"": ""linear"", ""angle"": 90,
      ""stops"": [ {""at"": 0, ""color"": ""#FF8A20""}, {""at"": 1, ""color"": ""#E05500""} ] }
  },
  ""hueWalk"": { ""sat"": 0.35, ""light"": 0.88,
               ""armedSat"": 0.8, ""armedLight"": 0.6 },
  ""outline"":      { ""color"": ""#40FFFFFF"", ""width"": 2, ""dash"": [4, 2] },
  ""armedOutline"": { ""color"": ""#FFFFFFFF"", ""width"": 3 },
  ""armed"": { ""liftPx"": 12, ""northPx"": 4, ""scale"": 1.08 },
  ""glyph"": { ""edge"": ""outer"", ""edgeColor"": ""#FFFFFF"", ""edgeWidth"": 3,
             ""edgeShadow"": true,
             ""glow"": { ""color"": ""slice"", ""strength"": 0.7 },
             ""castShadow"": true, ""armedWash"": true },
  ""label"": { ""case"": ""upper"", ""sizeMul"": 1.1 },
  ""gapPx"": 9,
  ""tile"":  { ""edgeColor"": ""#FF6A00"", ""sheen"": true, ""lifted"": true,
             ""texture"": { ""file"": ""grain.png"", ""opacity"": 0.5 } }

Format 3 adds textures and sounds — files that live IN the package folder,
referenced by bare file name (no paths):

  ""textures"": {
    ""slice"":    { ""file"": ""grain.png"", ""tile"": true, ""opacity"": 0.45 },
    ""hub"":      { ""file"": ""grain.png"", ""tile"": true, ""opacity"": 0.3 },
    ""backdrop"": { ""file"": ""ground.jpg"", ""opacity"": 0.35 }
  },
  ""sounds"": { ""armed"": ""tick.wav"", ""fired"": ""thud.wav"",
              ""enableWheels"": ""on.wav"", ""disableWheels"": ""off.wav"" }

- Textures: PNG or JPG, max 4 MB each; oversized images are decoded down to
  2048px wide. slice/hub paint over the fill; backdrop draws behind the
  whole wheel; tile.texture paints the Settings > Customize swatch button
  (over the theme's own resting color). tile=true (slice/hub only) repeats
  the image at its natural size instead of stretching it.
- Sounds: WAV only, max 1 MB / 3 seconds each; each event you provide
  replaces that theme sound, the rest keep the theme's. Events: armed,
  fired, enableWheels, disableWheels.
- Package limits: at most 32 files, 4 MB per file, 16 MB total. ANY file
  change (not just material.json) re-asks for your confirmation.

Notes:
- id: lowercase letters, digits, hyphens (2-31 chars). The theme appears in
  Settings > Customize under ""Custom"", and in config.json as ""custom-<id>"".
- Colors are #RRGGBB or #AARRGGBB. Numbers are clamped to safe ranges.
- fills types: solid (color), bowed (the Pearl/Obsidian glass ramp), linear
  (angle in degrees). hueWalk gives every slice its own hue around the ring
  (like Kawaii) and overrides resting/armed fills.
- glyph.glow.color can be ""slice"" (each slice's own accent color) or a hex.
- labelFont: optional; must be a font installed on this PC, or it's ignored.
  Font FILES inside packages are not supported, deliberately.
- soundTheme: which sounds the wheel makes on your theme. Either name a sound
  set directly - physical, digital, kawaii, mesa, salvage, reactor, obsidian -
  or name a BUILT-IN MATERIAL to borrow whatever that one uses (so ""pearl""
  gives you the digital set, ""flat-dark"" the physical set). Naming the
  material is the safer choice: your theme keeps matching that look even if
  its sounds are retuned later. Defaults to physical.
- Themes are data only. They cannot run code or reach the network, and the
  only files they can use are the images and sounds in their own folder -
  by design.
- Comments (// ...) and trailing commas are allowed in material.json.
- Why a theme didn't load: search %APPDATA%\Radiata\radiata-trace.log
  for ""[Packages] skipped material"" - the line names the problem.
- Changes are picked up on the next Radiata restart (you'll be asked to
  confirm the changed package again).
";

    private const string ArcadeReadme =
@"Radiata - Arcade game packages
===============================

Drop each game into its OWN folder here, then restart Radiata. Because a
game package contains CODE (a JavaScript file), Radiata asks for your
explicit confirmation before it ever runs, and asks again if ANY file in
the package changes. You are responsible for code you install from third
parties. Games appear in the Arcade once confirmed.

Or drag the game's folder, or a ZIP of it, onto Radiata's Settings window
(or onto Radiata.exe): it is copied here and you confirm it - no restart.

A package is one folder containing a game.json and one .js file:

  {
    ""format"": 1,
    ""id"": ""firefly"",
    ""title"": ""Firefly"",
    ""entry"": ""firefly.js"",
    ""tint"": ""#5B8DEF"",
    ""preview"": ""preview.png"",
    ""badge"": ""badge.png"",
    ""glyph"": ""glyph.png"",
    ""howTo"": [ ""Fly with the stick."", ""Hold {cross} to boost."" ]
  }

- id: 1-32 chars of a-z, 0-9, '-'. Shown in config as ""pkg-<id>"".
- title: up to 24 chars. entry: a bare .js file name (no paths), max 256 KB.
- howTo: optional, up to 5 lines of 80 chars - becomes the in-game help
  card. {cross} {circle} {square} {triangle} become the player's button
  glyphs. No lines = no help card.
- tint: optional, #RGB or #RRGGBB - your cabinet's colour in the Arcade
  Launcher. The title is printed on the cabinet's nameplate either way.
- preview: optional, a bare .png/.jpg file name in this folder - the picture
  on your cabinet's screen until the game has been played. Make it square,
  with the round playfield filling it. Once played, the cabinet shows the
  player's own last board instead, saved as
  %APPDATA%\Radiata\arcade-shots\pkg-<id>.png - the easiest way to make a
  preview is to play your game, close it, and copy that file in.
- badge: optional, a bare .png file name - an illustration drawn on the
  cabinet's nameplate, left of the title, like the built-in cabinets' art.
  Transparent background, its own colours; it overflows the nameplate.
- glyph: optional, a bare .png file name - your game's icon on its wheel
  slices. Its transparency is the shape; the wheel colours it like any
  other slice icon, so draw it in one colour on a transparent background.
- Package limits: at most 32 files, 4 MB per file, 16 MB total.

HOW A GAME RUNS
The script runs in a locked-down interpreter inside a sandboxed helper
process: no files, no network, no clipboard, no other processes - only
the drawing/input/saving functions below exist. It is stepped 120 times
a second and asked to draw once per screen frame, with a hard per-frame
time and instruction budget; a script that overruns is paused or stopped,
never your PC. Memory is capped at 128 MB by the operating system.

THE PLAYFIELD IS A DISC. Everything is drawn in polar coordinates:
r = 0 (center) .. 1 (rim), angle in DEGREES, 0 = 12 o'clock, increasing
clockwise. Radiata does all the math and clips to the circle. Colors are
numbers in 0xAARRGGBB form (alpha first): 0xFFFF0000 = opaque red.

YOUR SCRIPT MUST DEFINE
  function tick(dt) { }   called for every fixed 1/120 s step
  function draw()   { }   called once per screen frame; issue draw calls

DRAWING (only valid during draw(); max 1024 commands per frame)
  arc(r0, r1, a0, a1, color)      ring segment between radii r0..r1,
                                  angles a0..a1
  ring(r, width, color, edge)     full circle outline centered on radius r
  dot(r, a, size, color)          filled circle; size 0..0.5 of the field
  line(r0, a0, r1, a1, w, color)  segment; width w 0..0.1
  poly([r,a, r,a, ...], color)    filled polygon, 3..16 points
  text(r, a, size, ""str"", color)  text up to 64 chars; size 0..0.3

INPUT (read-only globals, updated every frame)
  stickX, stickY      -1..1, y positive DOWNWARD (screen orientation)
  crossDown, squareDown           held
  crossPressed, squarePressed     true for one frame per press
  dpadUp, dpadRight, dpadDown, dpadLeft   true for one frame per press
  (O closes the game and TRIANGLE opens the help card - Radiata owns
  those buttons; your script never sees them.)

SAVING (the ONLY state that survives closing the game)
  kvSet(""key"", ""value"")   strings only; key <=64 chars, value <=1024,
                          4 KB total per game
  kvGet(""key"")            returns the value or null
  Write the reserved key ""hiscore"" (an integer as text) to publish a
  best score to the Arcade picker.
  Everything else resets when the game is dismissed - design for it.

OTHER
  rand()        deterministic random 0..1 (seeded per session)
  cue(""name"")   play a built-in sound: fire, tick, good, denied, kill,
                zap, hurt, clear, gameover (anything else is silent)

GOTCHAS
- The script runs in strict mode: an undeclared variable is an error.
  There is no console, eval, setTimeout, import or require.
- Keep every draw value in range (radius 0..1, sizes as above, angles
  within +/-3600). An out-of-range call is treated as a broken game: the
  script is restarted, and after three restarts the game shows a problem
  card. Clamp your numbers.
- JavaScript's bit operators (| & << >>) make SIGNED numbers, and a
  negative colour draws as nothing. Build colours with arithmetic, or end
  the expression with >>> 0.
- Input is read once per drawn frame, but tick() can run several times in
  that frame, and each of those ticks sees the same Pressed flag. Count a
  press once (the Firefly sample shows a pattern).
- Per drawn frame: 2,000,000 statements and 8 ms for every tick plus the
  draw, at most 8 cue() calls and 16 kvSet() writes. kvSet() throws when
  a key or value is too long or the 4 KB store is full.
- tick() and draw() are called a few times while the game loads, with no
  input, to warm the engine up.
- Script errors are written to %APPDATA%\Radiata\radiata-trace.log:
  search for ""[Arcade] script"".

Changes are picked up on the next Radiata restart (you'll be asked to
confirm the changed package again).
";
}
