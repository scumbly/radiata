using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>
/// On-screen button prompts — nothing spells a face glyph out.
///
/// <para>Every prompt must resolve through <see cref="ControllerButtons.Text"/>, which follows the player's
/// Settings ▸ Advanced ▸ Button icons choice. A literal cross/circle/square/triangle in a drawn string names
/// the <b>wrong pad</b> for everyone on the Xbox set — and it is invisible to whoever wrote it, because it
/// looks perfectly correct on the pad they happen to be holding.</para>
///
/// <para>That is exactly why this is a test rather than a code-review habit. Two sites shipped with it
/// (the Arcade picker and the bleed-through guard card) and survived several passes over the same files: the
/// only way to SEE the bug is to change a setting and re-open a surface most people never re-open.</para>
///
/// <para>Two tiers. The arcade surface is held to <b>no exceptions at all</b> — it is the code that got this
/// wrong, and every string it draws is a prompt. Everywhere else is a tripwire against an ALLOW-LIST: a new
/// glyph literal fails until someone either routes it through ControllerButtons or writes down why it is
/// legitimate. The allow-list is checked for rot in both directions, so an entry whose glyph has since been
/// removed fails too rather than quietly licensing a future one.</para>
///
/// <para>⚠ Source scan, not a reflection check. That is the point — the compiled form of a correct call and
/// a hard-coded string are indistinguishable once they reach a <c>DrawText</c>. It needs the repo, so it
/// SKIPS rather than fails when run from a published build.</para>
/// </summary>
internal static class T_Glyphs
{
    /// <summary>The face glyphs to hunt for.
    ///
    /// <para>⚠ CHAR literals, deliberately — the scanner only reports STRING literals and skips these, so
    /// this table stays out of its own results without the file having to special-case itself by name. Put
    /// them in a <c>string</c> here and the test fails on itself.</para></summary>
    private static readonly char[] FaceGlyphs =
    [
        '✕',   // cross
        '○',   // circle
        '□',   // white square  — what ControllerButtons draws
        '☐',   // ballot box    — what Settings and the CONTROLS.md export draw
        '△',   // triangle
    ];

    /// <summary>Files permitted to contain a face glyph in a string literal, and why.
    ///
    /// <para>⚠ By FILE, not by line: line numbers churn on every edit and would make this a maintenance tax
    /// rather than a guard. The trade is that a bad prompt added to an already-listed file would pass — so
    /// nothing that DRAWS a game surface is on this list, and the arcade can't be added to it at all.</para></summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Overlay/ControllerButtons.cs"] =
            "the resolver itself — this is where the glyphs are supposed to live",
        ["Settings/SettingsWindow.xaml.cs"] =
            "the Button icons preview, and it is already a per-set conditional",
        ["Settings/CustomizeEditorControl.xaml.cs"] =
            "a Settings remove-row button; a UI close affordance, not a pad prompt",
        ["Settings/ExceptionsEditorControl.xaml.cs"] =
            "same — a Settings remove-row button",
        ["Settings/SettingsTheme.xaml"] =
            "the shared close-button ControlTemplate; a UI close affordance, not a pad prompt",
        ["Core/HelpContent.cs"] =
            "Help prose about the Button icons setting, plus the CONTROLS.md token resolver, which exports "
            + "generic defaults on purpose (the doc is a build artifact, not a per-pad document)",
        ["Core/HelpTextEs.cs"] = "translation of the Button icons Help text",
        ["Core/HelpTextDe.cs"] = "translation of the Button icons Help text",
        ["Core/HelpTextJa.cs"] = "translation of the Button icons Help text",
        ["Core/HelpTextAr.cs"] = "translation of the Button icons Help text",
    };

    /// <summary>The arcade draws nothing but prompts and game text, so it gets no allow-list.</summary>
    private static bool IsArcadeSurface(string rel) =>
        rel.StartsWith("Arcade/", StringComparison.OrdinalIgnoreCase);

    private readonly record struct Hit(string Rel, int Line, string Literal);

    public static void Run()
    {
        H.Group("Button glyphs — no on-screen prompt spells one out");

        Mechanism();

        var root = H.RepoRoot();
        if (root is null)
        {
            H.Skip("source scan", "no repo beside this binary — nothing to scan (a published build)");
            return;
        }

        var hits = new List<Hit>();
        int scanned = 0;
        foreach (var path in SourceFiles(root))
        {
            scanned++;
            string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException ex) { H.Fail($"could not read {rel}", ex.Message); continue; }

            // XAML has no C# string grammar to walk, so anything left after the comments are removed counts:
            // markup is either an attribute value or displayed text, and both are player-visible.
            //
            // ⚠ Stripping the comments is NOT optional. The first run of this test failed on two XAML
            // comments — one in GameBrowserControl explaining which dot row belongs to which button, one in
            // SystemEditorControl describing a removed Storefronts section. Allow-listing those files would
            // have been the easy fix and the wrong one: GameBrowserControl draws the Game Grid, so a real
            // prompt added to it later would then have passed.
            if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                string markup = BlankXamlComments(text);
                int at = markup.IndexOfAny(FaceGlyphs);
                if (at >= 0) hits.Add(new Hit(rel, LineOf(text, at), "(xaml markup)"));
                continue;
            }

            foreach (var (line, literal) in StringLiterals(text))
                if (literal.IndexOfAny(FaceGlyphs) >= 0)
                    hits.Add(new Hit(rel, line, literal));
        }

        H.Pass("scanned the tree", $"{scanned} source files, {hits.Count} glyph literal(s) found");

        var arcade = hits.Where(h => IsArcadeSurface(h.Rel)).ToList();
        H.Check("the arcade spells out no glyph (no allow-list here, ever)", arcade.Count == 0,
                arcade.Count == 0 ? "picker, guard card and every renderer go through ControllerButtons"
                                  : Describe(arcade));

        var unlisted = hits.Where(h => !IsArcadeSurface(h.Rel) && !Allowed.ContainsKey(h.Rel)).ToList();
        H.Check("every other glyph literal is on the allow-list", unlisted.Count == 0,
                unlisted.Count == 0
                    ? $"{Allowed.Count} file(s) listed, each with a reason"
                    : Describe(unlisted) + "  → route it through ControllerButtons.Text, or add the file to "
                                         + "T_Glyphs.Allowed with a reason");

        // The list must not outlive what it excuses: an entry with nothing behind it is a licence sitting
        // open for the next literal added to that file.
        var stale = Allowed.Keys.Where(k => !hits.Any(h => h.Rel.Equals(k, StringComparison.OrdinalIgnoreCase)))
                                .ToList();
        H.Check("no stale allow-list entries", stale.Count == 0,
                stale.Count == 0 ? null
                                 : "these no longer contain a glyph and should be removed from the list: "
                                   + string.Join(", ", stale));

        SpokenWords(root);
    }

    /// <summary>The SPOKEN twin of the scan above. Narration said "cross picks up, triangle adds…" on every
    /// pad, so an Xbox user was told to press buttons their controller doesn't have — the same bug class as a
    /// drawn ✕, and invisible to the glyph scan because the literal holds no glyph character.
    ///
    /// <para>⚠ Scoped to narration call sites, not the whole tree, and that is deliberate: "circle" and
    /// "cross" are legitimate GEOMETRY words elsewhere (ConnateProbe's assert messages, Kabloom, the
    /// "cross-link" in Help prose). A repo-wide word scan would be false-positive soup and would get
    /// allow-listed into uselessness. Anything within a few lines of an Announce/Say call is narration;
    /// anything else is prose.</para></summary>
    private static void SpokenWords(string root)
    {
        // Built from the enum names so this table can't drift from PadButton, and so T_Glyphs.cs doesn't
        // trip its own scan by spelling the words out as literals.
        string[] words =
        [
            nameof(PadButton.Cross).ToLowerInvariant(), nameof(PadButton.Circle).ToLowerInvariant(),
            nameof(PadButton.Square).ToLowerInvariant(), nameof(PadButton.Triangle).ToLowerInvariant(),
        ];

        var hits = new List<Hit>();
        foreach (var path in SourceFiles(root))
        {
            if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) continue;   // markup is drawn, not spoken
            string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException) { continue; }
            if (!text.Contains("Announce", StringComparison.Ordinal) &&
                !text.Contains("Say(", StringComparison.Ordinal)) continue;

            var narrationLines = NarrationLines(text);
            foreach (var (line, literal) in StringLiterals(text))
            {
                if (!narrationLines.Contains(line)) continue;
                // Only the SPOKEN text counts. An interpolation hole is code, and the correct fix itself
                // reads `{ControllerButtons.Spoken(PadButton.Cross)}` — matching inside the braces would
                // fail the very pattern this test exists to require.
                string spoken = StripInterpolations(literal);
                foreach (var w in words)
                    if (ContainsWord(spoken, w)) { hits.Add(new Hit(rel, line, literal)); break; }
            }
        }

        H.Check("no narration string spells a button name out", hits.Count == 0,
                hits.Count == 0
                    ? "spoken prompts resolve through ControllerButtons.Spoken"
                    : Describe(hits) + "  → route it through ControllerButtons.Spoken(PadButton.…), which "
                                     + "follows Settings ▸ Advanced ▸ Button icons");

        // The resolver must actually differ per set, or every fixed site above is quietly wrong again.
        var restore = ControllerButtons.Set;
        try
        {
            ControllerButtons.Set = ControllerGlyphSet.Xbox;
            string x = ControllerButtons.Spoken(PadButton.Cross);
            ControllerButtons.Set = ControllerGlyphSet.PlayStation;
            string p = ControllerButtons.Spoken(PadButton.Cross);
            H.Check("Spoken() differs between the sets", x != p, $"xbox=\"{x}\" playstation=\"{p}\"");
            // A TTS engine reads a glyph as nothing useful — that's the whole reason Spoken exists apart
            // from Text, so guard the distinction rather than trusting it.
            H.Check("Spoken() never returns a glyph character",
                    x.IndexOfAny(FaceGlyphs) < 0 && p.IndexOfAny(FaceGlyphs) < 0, $"xbox=\"{x}\" ps=\"{p}\"");
            H.Check("the Xbox answer isn't a bare letter", x.Length > 1, $"xbox=\"{x}\"");
        }
        finally { ControllerButtons.Set = restore; }
    }

    /// <summary>Line numbers within a few lines of an <c>Announce</c>/<c>Say</c> call — the announcement
    /// window. Announcements here routinely span several lines, so a window rather than the call's own line.</summary>
    private static HashSet<int> NarrationLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var set = new HashSet<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("Announce", StringComparison.Ordinal) &&
                !lines[i].Contains("Say(", StringComparison.Ordinal) &&
                !lines[i].Contains("AnnouncementKind", StringComparison.Ordinal)) continue;
            for (int j = Math.Max(0, i - 4); j <= Math.Min(lines.Length - 1, i + 4); j++) set.Add(j + 1);
        }
        return set;
    }

    /// <summary>Blank the <c>{…}</c> interpolation holes, leaving only the text that is actually spoken.
    /// Brace-depth counted so a nested expression closes correctly; <c>{{</c>/<c>}}</c> escapes are literal
    /// braces and are skipped.</summary>
    private static string StripInterpolations(string s)
    {
        var buf = new System.Text.StringBuilder(s.Length);
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (depth == 0 && s[i] == '{' && i + 1 < s.Length && s[i + 1] == '{') { buf.Append("  "); i++; continue; }
            if (depth == 0 && s[i] == '}' && i + 1 < s.Length && s[i + 1] == '}') { buf.Append("  "); i++; continue; }
            if (s[i] == '{') { depth++; buf.Append(' '); continue; }
            if (s[i] == '}') { if (depth > 0) depth--; buf.Append(' '); continue; }
            buf.Append(depth > 0 ? ' ' : s[i]);
        }
        return buf.ToString();
    }

    /// <summary>Whole-word, case-insensitive. Rejects hyphenated compounds so "cross-link" in Help prose and
    /// "cross-family" in probe messages don't read as button names.</summary>
    private static bool ContainsWord(string s, string word)
    {
        for (int i = s.IndexOf(word, StringComparison.OrdinalIgnoreCase); i >= 0;
             i = s.IndexOf(word, i + 1, StringComparison.OrdinalIgnoreCase))
        {
            char before = i == 0 ? ' ' : s[i - 1];
            int after = i + word.Length;
            char next = after >= s.Length ? ' ' : s[after];
            if (!char.IsLetter(before) && before != '-' && !char.IsLetter(next) && next != '-') return true;
        }
        return false;
    }

    /// <summary>The behaviour every fixed site now depends on: the resolver really does answer differently
    /// per glyph set. A source scan alone would still pass if <c>Text</c> returned the PlayStation glyph
    /// unconditionally.</summary>
    private static void Mechanism()
    {
        var restore = ControllerButtons.Set;
        try
        {
            ControllerButtons.Set = ControllerGlyphSet.Xbox;
            string xCross = ControllerButtons.Text(PadButton.Cross);
            string xCircle = ControllerButtons.Text(PadButton.Circle);
            string xTri = ControllerButtons.Text(PadButton.Triangle);
            H.Check("Xbox set answers with letters", xCross == "A" && xCircle == "B" && xTri == "Y",
                    $"cross={xCross} circle={xCircle} triangle={xTri}");

            ControllerButtons.Set = ControllerGlyphSet.PlayStation;
            string pCross = ControllerButtons.Text(PadButton.Cross);
            H.Check("PlayStation set answers with shapes",
                    pCross.Length > 0 && pCross.IndexOfAny(FaceGlyphs) >= 0,
                    $"cross=U+{(int)pCross[0]:X4}");

            H.Check("the two sets actually differ", pCross != xCross);
        }
        finally { ControllerButtons.Set = restore; }
    }

    private static string Describe(List<Hit> hits) =>
        string.Join("; ", hits.Take(6).Select(h => $"{h.Rel}:{h.Line} \"{Trim(h.Literal)}\""))
        + (hits.Count > 6 ? $" (+{hits.Count - 6} more)" : "");

    private static string Trim(string s) =>
        s.Length <= 40 ? s : s.Substring(0, 40) + "…";

    /// <summary>Blank out <c>&lt;!-- --&gt;</c> bodies, keeping the string the SAME LENGTH and keeping its
    /// newlines — so an index into the result still names the right line of the original file.</summary>
    private static string BlankXamlComments(string s)
    {
        var buf = s.ToCharArray();
        int i = 0;
        while (i < buf.Length)
        {
            int open = s.IndexOf("<!--", i, StringComparison.Ordinal);
            if (open < 0) break;
            int close = s.IndexOf("-->", open + 4, StringComparison.Ordinal);
            int end = close < 0 ? buf.Length : close + 3;      // unterminated: treat the rest as comment
            for (int j = open; j < end; j++) if (buf[j] != '\n' && buf[j] != '\r') buf[j] = ' ';
            i = end;
        }
        return new string(buf);
    }

    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < text.Length; i++) if (text[i] == '\n') line++;
        return line;
    }

    /// <summary>Every .cs and .xaml the checkout at <paramref name="root"/> owns — the one source-tree filter
    /// the harness's source scans share.</summary>
    internal static IEnumerable<string> SourceFiles(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) continue;
            string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            // Build output, VCS and hidden dot-directories: not sources, and a nested checkout would report
            // every hit several times over. ⚠ Test the RELATIVE path: if the root itself lies under a
            // dot-directory, an absolute-path test drops every file.
            if (rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith(".vs/", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith(".claude/", StringComparison.OrdinalIgnoreCase)) continue;
            yield return path;
        }
    }

    /// <summary>Every string literal in a C# file, with the line it opened on.
    ///
    /// <para>A small hand-rolled scanner rather than a regex: the whole job is telling a literal from a
    /// COMMENT, and this repo's comments discuss these glyphs constantly. It handles line and block
    /// comments, regular and verbatim and interpolated strings, and char literals.</para>
    ///
    /// <para>⚠ It decodes <c>\uXXXX</c> inside a literal, so writing the escape instead of the character is
    /// not a way around this check. That is also why the glyph table above is char literals — the scanner
    /// skips those, so this file stays out of its own results.</para></summary>
    private static List<(int Line, string Text)> StringLiterals(string s)
    {
        var found = new List<(int, string)>();
        var sb = new StringBuilder();
        int line = 1, litLine = 1;
        const int Code = 0, LineComment = 1, BlockComment = 2, Str = 3, Verbatim = 4, Chr = 5;
        int state = Code;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            char n = i + 1 < s.Length ? s[i + 1] : '\0';

            if (c == '\n')
            {
                line++;
                if (state == LineComment) state = Code;
                // An unterminated regular string can't span a line; recover rather than swallow the file.
                else if (state == Str) { state = Code; sb.Clear(); }
                if (state != Verbatim) continue;
            }

            switch (state)
            {
                case Code:
                    if (c == '/' && n == '/') { state = LineComment; i++; }
                    else if (c == '/' && n == '*') { state = BlockComment; i++; }
                    else if (c == '\'') state = Chr;
                    else if (c == '"') { state = Str; sb.Clear(); litLine = line; }
                    else if ((c == '@' || c == '$') && n == '"')
                    { state = c == '@' ? Verbatim : Str; sb.Clear(); litLine = line; i++; }
                    else if ((c == '@' || c == '$') && (n == '$' || n == '@')
                             && i + 2 < s.Length && s[i + 2] == '"')
                    { state = Verbatim; sb.Clear(); litLine = line; i += 2; }
                    break;

                case LineComment:
                    break;

                case BlockComment:
                    if (c == '*' && n == '/') { state = Code; i++; }
                    break;

                case Chr:
                    if (c == '\\') i++;
                    else if (c == '\'') state = Code;
                    break;

                case Str:
                    if (c == '\\')
                    {
                        if (n == 'u' && i + 5 < s.Length
                            && int.TryParse(s.AsSpan(i + 2, 4), NumberStyles.HexNumber,
                                            CultureInfo.InvariantCulture, out int cp))
                        { sb.Append((char)cp); i += 5; }
                        else i++;
                    }
                    else if (c == '"') { found.Add((litLine, sb.ToString())); state = Code; }
                    else sb.Append(c);
                    break;

                case Verbatim:
                    if (c == '"' && n == '"') { sb.Append('"'); i++; }
                    else if (c == '"') { found.Add((litLine, sb.ToString())); state = Code; }
                    else sb.Append(c);
                    break;
            }
        }
        return found;
    }
}
