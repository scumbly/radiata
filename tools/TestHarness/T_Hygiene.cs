using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Radiata.TestHarness;

/// <summary>Mechanical guards that don't belong to an existing group:
/// Settings checkbox wiring, the live-file contract on the development repository's test sheet and work queue, CONTROLS.md
/// staying in sync with its generator, the help chip's hand-set glyph nudge, history-narrating comments,
/// and stacked XML doc blocks. Text/XML scans plus one font measurement — no build, no window, no config
/// touched.
///
/// <para>The live-file contract and the CONTROLS.md sync read files the public snapshot does not carry, so
/// they run only in the development repository (<see cref="H.DevRepoOnly"/>); the rest read published
/// sources and run everywhere.</para></summary>
internal static class T_Hygiene
{
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Locale value tables carry translator-authored prose the hook doesn't own; a dated,
    /// attributed or narrating English source string ships as-is into every locale.</summary>
    private static readonly string[] TranslationMapFiles =
    {
        "HelpTextEs.cs", "HelpTextDe.cs", "HelpTextJa.cs", "HelpTextAr.cs",
        "UiTextEs.cs", "UiTextDe.cs", "UiTextJa.cs", "UiTextAr.cs",
    };

    public static void Run()
    {
        H.Group("Hygiene — Settings checkbox wiring, the live-file contract, CONTROLS.md sync, the help chip's glyph");

        var root = H.RepoRoot();
        if (root is null) { H.Fail("repo root not found"); return; }

        CheckBoxHandlers(root);
        LiveFileHygiene(root);
        ControlsMdInSync(root);
        HelpChipGlyphNudge(root);
        HistoryNarratingComments(root);
        StackedXmlDocComments(root);
    }

    // ── product-source enumeration, shared by the two comment guards below ──────────────────────────

    /// <summary>Root *.cs/*.xaml (non-recursive) plus the shell folders, Core/**, Arcade/**, ArcadeHost/** —
    /// the app's compiled surface. Excludes tools/ (a harness driver legitimately cites the ledgers it drives),
    /// generated *.g.cs, and the four-locale translation maps (translator prose, not authored comments).</summary>
    private static List<string> ProductSourceFiles(string root)
    {
        var files = new List<string>();
        files.AddRange(Directory.EnumerateFiles(root, "*.cs", SearchOption.TopDirectoryOnly));
        files.AddRange(Directory.EnumerateFiles(root, "*.xaml", SearchOption.TopDirectoryOnly));
        foreach (var sub in new[] { "Core", "Arcade", "ArcadeHost", "Settings", "Overlay", "Input", "Setup", "Platform",
                                    "GameLibrary", "Sound", "Integrations", "UI" })
        {
            var dir = Path.Combine(root, sub);
            if (!Directory.Exists(dir)) continue;
            foreach (var pattern in new[] { "*.cs", "*.xaml" })
                files.AddRange(Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
                    .Where(p => !IsUnderBuildOutput(p)));
        }
        return files
            .Where(p => !p.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => !TranslationMapFiles.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        static bool IsUnderBuildOutput(string path)
        {
            var norm = path.Replace('\\', '/');
            return norm.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || norm.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
        }
    }

    private readonly record struct CommentHit(int Line, string Text);

    /// <summary>Comment text on each line of a .cs file, string and char literals blanked out first so a
    /// URL, a data blob, or a plain apostrophe ("can't", "runner's") can never be misread as opening a
    /// literal that swallows a real // or */ and desyncs the rest of the scan. A char literal is
    /// recognised only by its strict shape (<c>'x'</c> or <c>'\x'</c>) — a lone apostrophe is left alone
    /// as prose, never treated as an unterminated literal.</summary>
    private static IEnumerable<CommentHit> ExtractCsComments(string[] lines)
    {
        bool inBlock = false;
        for (int ln = 0; ln < lines.Length; ln++)
        {
            string s = lines[ln];
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                if (inBlock)
                {
                    int end = s.IndexOf("*/", i, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(s, i, s.Length - i); i = s.Length; }
                    else { sb.Append(s, i, end - i); i = end + 2; inBlock = false; }
                    continue;
                }
                char c = s[i];
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    sb.Append(s, i, s.Length - i);
                    i = s.Length;
                    continue;
                }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    inBlock = true;
                    i += 2;
                    continue;
                }
                if (c == '@' && i + 1 < s.Length && s[i + 1] == '"')
                {
                    i += 2;
                    while (i < s.Length)
                    {
                        if (s[i] == '"')
                        {
                            if (i + 1 < s.Length && s[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    continue;
                }
                if (c == '"')
                {
                    int j = i + 1;
                    while (j < s.Length)
                    {
                        if (s[j] == '\\') { j += 2; continue; }
                        if (s[j] == '"') { j++; break; }
                        j++;
                    }
                    i = Math.Min(j, s.Length);
                    continue;
                }
                if (c == '\'')
                {
                    if (i + 2 < s.Length && s[i + 1] != '\\' && s[i + 2] == '\'') { i += 3; continue; }
                    if (i + 3 < s.Length && s[i + 1] == '\\' && s[i + 3] == '\'') { i += 4; continue; }
                    i++; // an apostrophe, not a char literal — leave it as prose
                    continue;
                }
                i++;
            }
            if (sb.Length > 0) yield return new CommentHit(ln + 1, sb.ToString());
        }
    }

    /// <summary>Same idea for XAML's <c>&lt;!-- --&gt;</c>, which cannot nest and needs no literal-blanking
    /// (an attribute value containing the delimiter itself would not parse as XML).</summary>
    private static IEnumerable<CommentHit> ExtractXamlComments(string[] lines)
    {
        bool inComment = false;
        for (int ln = 0; ln < lines.Length; ln++)
        {
            string s = lines[ln];
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                if (inComment)
                {
                    int end = s.IndexOf("-->", i, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(s, i, s.Length - i); i = s.Length; }
                    else { sb.Append(s, i, end - i); i = end + 3; inComment = false; }
                    continue;
                }
                int start = s.IndexOf("<!--", i, StringComparison.Ordinal);
                if (start < 0) break;
                i = start + 4;
                inComment = true;
            }
            if (sb.Length > 0) yield return new CommentHit(ln + 1, sb.ToString());
        }
    }

    private static IEnumerable<CommentHit> ExtractComments(string path) =>
        path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
            ? ExtractXamlComments(File.ReadAllLines(path))
            : ExtractCsComments(File.ReadAllLines(path));

    /// <summary>Comments state the current constraint, never the decision history. A dated decision, an
    /// owner attribution, or narration of a prior name or state in a comment is what the development
    /// repository's pre-commit hook blocks on ADDED lines — this re-checks the whole tree, catching what a
    /// merge commit's diff (which skips the hook) lets through. Uses the hook's patterns, extended with the
    /// bare "owner call"/"owner request"/"Owner:" phrasing and a general 20xx year. A ctor parameter named
    /// <c>owner</c>, a <c>.Owner</c> property, or prose like "single-owner control device" never match:
    /// only comment TEXT is scanned (literals blanked first), and the owner pattern requires "(owner"
    /// glued to a comma/close-paren, "owner," glued to a month, or "owner:" as its own label — not a bare
    /// word.</summary>
    private static void HistoryNarratingComments(string root)
    {
        var monthDateYear = new Regex(
            @"\b(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\.?\s+[0-9]{1,2},?\s+20\d{2}\b",
            RegexOptions.IgnoreCase);
        var ownerAttribution = new Regex(
            @"\(owner[,)]|\bowner call\b|\bowner request\b|\bowner:\s|\bowner,\s*(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)",
            RegexOptions.IgnoreCase);
        var historyNarration = new Regex(
            @"\bwas renamed\b|\bused to be\b|\bformerly\b|\bwas previously\b|\(previously\b|\bpreviously (named|called|known as)\b",
            RegexOptions.IgnoreCase);

        var files = ProductSourceFiles(root);
        var hits = new List<string>();
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var hit in ExtractComments(file))
            {
                var text = hit.Text;
                if (monthDateYear.IsMatch(text) || ownerAttribution.IsMatch(text) || historyNarration.IsMatch(text))
                    hits.Add($"{rel}:{hit.Line}: {text.Trim()}");
            }
        }
        H.Check($"no comment in product source carries a dated decision, an owner attribution, or history narration ({files.Count} files scanned)",
                hits.Count == 0,
                hits.Count == 0 ? null : string.Join(" | ", hits.Take(20)) + (hits.Count > 20 ? $" | +{hits.Count - 20} more" : ""));
    }

    /// <summary>A <c>///</c> block whose last line is immediately followed by another complete
    /// <c>&lt;summary&gt;</c>-bearing block, with no member declaration between them, means the first
    /// block documents nothing — its member was deleted, moved, or never matched, and the second block's
    /// doc silently doubles for both. Blank lines between the two do not rescue it: a blank line is not a
    /// member either. The preceding block must itself carry a <c>&lt;summary&gt;</c> to count — a lone
    /// <c>&lt;param&gt;</c>/<c>&lt;returns&gt;</c> fragment immediately above a <c>&lt;summary&gt;</c> is
    /// the SAME member's doc in non-standard tag order, not a second stacked block.</summary>
    private static void StackedXmlDocComments(string root)
    {
        var files = ProductSourceFiles(root).Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();
        var hits = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith("///") || !trimmed.Contains("<summary>")) continue;

                int j = i - 1;
                while (j >= 0 && lines[j].Trim().Length == 0) j--;
                if (j < 0 || !lines[j].TrimStart().StartsWith("///")) continue;

                int blockEnd = j;
                bool precedingBlockHasSummary = false;
                while (j >= 0 && lines[j].TrimStart().StartsWith("///"))
                {
                    if (lines[j].TrimStart().Contains("<summary>")) precedingBlockHasSummary = true;
                    j--;
                }
                if (precedingBlockHasSummary)
                    hits.Add($"{rel}:{i + 1}: /// <summary> immediately follows another /// <summary> block (ends line {blockEnd + 1}) with no member between them");
            }
        }
        H.Check($"no /// <summary> block is immediately followed by another with no member between them ({files.Count} files scanned)",
                hits.Count == 0,
                hits.Count == 0 ? null : string.Join(" | ", hits.Take(20)) + (hits.Count > 20 ? $" | +{hits.Count - 20} more" : ""));
    }

    /// <summary>Every sibling checkbox on these two tabs saves through the same route: Checked/Unchecked to
    /// the debounced save timer to ApplyTo(cfg). One checkbox missing either handler reverts silently the
    /// moment Settings closes with no other change pending to flush it.</summary>
    private static void CheckBoxHandlers(string root)
    {
        foreach (var rel in new[] { "Settings/SystemEditorControl.xaml", "Settings/CustomizeEditorControl.xaml" })
        {
            var file = Path.GetFileName(rel);
            var path = Path.Combine(root, rel);
            if (!File.Exists(path)) { H.Fail($"{file} exists", "missing"); continue; }

            XDocument doc;
            try { doc = XDocument.Load(path); }
            catch (Exception ex) { H.Fail($"{file} parses as XML", ex.Message); continue; }

            var boxes = doc.Descendants().Where(e => e.Name.LocalName == "CheckBox").ToList();
            foreach (var box in boxes)
            {
                var name = box.Attribute(XamlNs + "Name")?.Value ?? "(unnamed CheckBox)";
                bool hasChecked = box.Attribute("Checked") is not null;
                bool hasUnchecked = box.Attribute("Unchecked") is not null;
                H.Check($"{file}: CheckBox \"{name}\" wires both Checked and Unchecked",
                        hasChecked && hasUnchecked,
                        hasChecked && hasUnchecked ? null : $"Checked={hasChecked} Unchecked={hasUnchecked}");
            }
        }
    }

    /// <summary>The live files hold open work only: no checkmark, checked task box, or
    /// closed-status heading may survive in them. The scan starts after the file's first `---` rule so the
    /// header paragraph's own explanation of the contract — which quotes these exact marks and words — is
    /// never mistaken for a violation of it. Development repository only: docs/ is not in the public snapshot.</summary>
    private static void LiveFileHygiene(string root)
    {
        if (!H.DevRepoOnly("live-file hygiene (docs/TEST-CHECKLIST.md, docs/WORK-QUEUE.md)", root)) return;

        var doneHeading = new Regex(@"^#{2,3}\s.*\b(DONE|FIXED|CODE-COMPLETE|PASSED)\b");
        var checkedBox = new Regex(@"-\s*\[x\]", RegexOptions.IgnoreCase);
        // A ✅ that OPENS a list item or a `##` entry heading marks the whole item done; one inside a line marks
        // a passed sub-clause of a still-open item, which the live files allow, and
        // a `###` lettered group marker is kept on the live side by that file's own header.
        var closedItem = new Regex(@"^\s*(?:[-*]|\d+\.)\s*✅|^##\s.*✅");

        foreach (var file in new[] { "TEST-CHECKLIST.md", "WORK-QUEUE.md" })
        {
            var path = Path.Combine(root, "docs", file);
            if (!File.Exists(path)) { H.Fail($"docs/{file} exists", "missing"); continue; }
            var lines = File.ReadAllLines(path);
            int rule = Array.FindIndex(lines, l => l.Trim() == "---");
            int start = rule < 0 ? 0 : rule + 1;

            var hits = new List<string>();
            for (int i = start; i < lines.Length; i++)
            {
                var line = lines[i];
                if (closedItem.IsMatch(line)) hits.Add($"L{i + 1}: ✅ opens a list item or heading");
                else if (checkedBox.IsMatch(line)) hits.Add($"L{i + 1}: checked task box");
                else if (doneHeading.IsMatch(line)) hits.Add($"L{i + 1}: {line.Trim()}");
            }
            H.Check($"docs/{file}: no ✅ / checked box / DONE-FIXED-CODE-COMPLETE-PASSED heading past the header ({lines.Length - start} lines scanned)",
                    hits.Count == 0, hits.Count == 0 ? null : string.Join(" | ", hits.Take(6)));
        }
    }

    /// <summary>CONTROLS.md is generated and never hand-edited — a stale copy drifts silently
    /// the moment Core/HelpContent.cs changes and nobody re-runs <c>--export-controls</c>. Development
    /// repository only: the public snapshot does not carry CONTROLS.md.</summary>
    private static void ControlsMdInSync(string root)
    {
        if (!H.DevRepoOnly("CONTROLS.md sync with HelpContent.ExportControlsMarkdown()", root)) return;
        var path = Path.Combine(root, "CONTROLS.md");
        if (!File.Exists(path)) { H.Fail("CONTROLS.md exists", "missing"); return; }
        var onDisk = File.ReadAllText(path).Replace("\r\n", "\n");
        string generated;
        try { generated = ControllerWheel.HelpContent.ExportControlsMarkdown().Replace("\r\n", "\n"); }
        catch (Exception ex) { H.Fail("HelpContent.ExportControlsMarkdown() ran", ex.GetBaseException().Message); return; }
        H.Check("CONTROLS.md matches HelpContent.ExportControlsMarkdown(), line endings aside",
                onDisk == generated,
                onDisk == generated ? null : $"{onDisk.Length} vs {generated.Length} chars — regenerate with Radiata.exe --export-controls CONTROLS.md");
    }

    /// <summary>The help chip's "?" is optically centred by a hand-set <c>TranslateTransform</c>, in TWO
    /// hand-maintained copies of the style (SettingsTheme.xaml, OnboardingWindow.xaml). This guards each
    /// against silent drift; it does NOT force them to agree.
    /// <para>⚠ The font-derived value holds for the Settings chips and NOT for the wizard's, which renders
    /// the same glyph at the same size about 3px higher in its ring. Deriving one ideal and asserting it on
    /// both is what kept that chip broken: the measurement looked authoritative, so the rendered result was
    /// argued with instead of read. Each file therefore carries the value MEASURED ON SCREEN for its own
    /// surface, from an ink map of the rendered chip (ring centre vs glyph centre), and the derived figure is
    /// reported alongside as context rather than as the expected answer.</para>
    /// <para>If a chip looks wrong, measure that chip — do not re-derive from metrics and do not copy the
    /// other file's number.</para></summary>
    private static void HelpChipGlyphNudge(string root)
    {
        // The two sizes the chip is instantiated at (per-instance FontSize beside Width/Height 15 and 19).
        double[] sizes = [10, 13];
        double derived = sizes.Select(IdealNudge).Average();

        // Measured on screen, per surface. OnboardingWindow's is read off a screenshot's ink map: ring rows
        // 2..17 (centre 9.5) against "?" rows 4..9 (centre 6.5) = 3.0px high, applied to the -0.7 it carried.
        var expected = new (string File, double Y)[] { ("Settings/SettingsTheme.xaml", -0.7), ("Setup/OnboardingWindow.xaml", 2.3) };

        foreach (var (rel, want) in expected)
        {
            var file = Path.GetFileName(rel);
            var path = Path.Combine(root, rel);
            if (!File.Exists(path)) { H.Fail($"{file} exists", "missing"); continue; }
            var text = File.ReadAllText(path);
            // The chip's template is the only TranslateTransform in either dictionary; anchor on the "?"
            // TextBlock that precedes it so a future transform elsewhere can't be picked up by mistake.
            var m = Regex.Match(text, "Text=\"\\?\".*?<TranslateTransform Y=\"(-?[0-9.]+)\"",
                                RegexOptions.Singleline);
            if (!m.Success) { H.Fail($"{file}: HelpChip's \"?\" carries a TranslateTransform", "not found"); continue; }
            double y = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            // 0.25px: wider than the 0.10 spread between the two sizes' ideals, narrower than any visible slip.
            bool ok = Math.Abs(y - want) <= 0.25;
            H.Check($"{file}: HelpChip's \"?\" nudge is the one measured for THIS surface ({want:0.00})",
                    ok, ok ? $"Y={y}" : $"Y={y}, expected {want:0.00} (font-derived, for reference only: {derived:0.00})");
        }

        // Offset that centres the glyph's INK inside the line box WPF actually centres. Negative = up.
        static double IdealNudge(double fontSize)
        {
            var tf = new System.Windows.Media.Typeface(
                new System.Windows.Media.FontFamily("Segoe UI"),
                System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold,
                System.Windows.FontStretches.Normal);
            var ft = new System.Windows.Media.FormattedText(
                "?", System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight, tf, fontSize,
                System.Windows.Media.Brushes.Black, 1.0);
            var ink = ft.BuildGeometry(new System.Windows.Point(0, 0)).Bounds;
            return ft.Height / 2.0 - (ink.Top + ink.Height / 2.0);
        }
    }

}
