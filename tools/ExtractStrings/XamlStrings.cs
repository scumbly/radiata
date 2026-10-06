using System.Xml;
using System.Xml.Linq;

namespace Radiata.ExtractStrings;

/// <summary>One localizable XAML attribute value: the decoded English text and where it came from.</summary>
public sealed record XamlString(string File, int Line, string Text, bool Rich, XAttribute Attr);

/// <summary>
/// The XAML string enumeration shared by tools/ExtractStrings and tools/copy-deck: the {loc:T …} markup
/// extension (bare or quoted form) and loc:LocRich.Source attributes, in file/element/attribute order.
/// </summary>
public static class XamlStrings
{
    /// <summary>Top-level folders that hold no shell XAML: the catalog covers the app's own windows and
    /// controls, wherever the shell folders put them, and never tools/, build output or published copies.</summary>
    private static readonly string[] NonShellDirs =
        ["tools", "bin", "obj", "publish", "docs", "packaging", "drivers", "Assets", "Core", "ArcadeHost"];

    /// <summary>Every shell *.xaml (the root and each shell folder), in the order the catalog is generated:
    /// by file name, so moving a file between folders never reorders the catalog.</summary>
    public static IEnumerable<string> EnumerateFiles(string root)
    {
        var files = Directory.EnumerateFiles(root, "*.xaml", SearchOption.TopDirectoryOnly).ToList();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith('.') || NonShellDirs.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            files.AddRange(Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories)
                .Where(p => !IsBuildOutput(Path.GetRelativePath(root, p))));
        }
        return files.OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ThenBy(p => p, StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string rel)
    {
        var segments = rel.Split('\\', '/');
        return segments.Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase)
                              || s.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Walks one XAML file. Unparseable files and grammar violations are appended to <paramref name="problems"/>;
    /// a value that fails a rule is still returned when the rule only warns (matching the generator).
    /// </summary>
    public static List<XamlString> Enumerate(string path, List<string> problems)
    {
        var name = Path.GetFileName(path);
        XDocument doc;
        try { doc = XDocument.Load(path, LoadOptions.SetLineInfo); }
        catch (Exception ex) { problems.Add($"{name}: not parseable as XML — {ex.Message}"); return []; }
        return Walk(name, doc, problems);
    }

    /// <summary>The same walk over XAML text held in memory rather than on disk.</summary>
    public static List<XamlString> EnumerateXml(string name, string xml, List<string> problems)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml, LoadOptions.SetLineInfo); }
        catch (Exception ex) { problems.Add($"{name}: not parseable as XML — {ex.Message}"); return []; }
        return Walk(name, doc, problems);
    }

    private static List<XamlString> Walk(string name, XDocument doc, List<string> problems)
    {
        var found = new List<XamlString>();

        foreach (var el in doc.Descendants())
            foreach (var attr in el.Attributes())
            {
                string v = attr.Value;
                // The quoted form {loc:T 'a, b'} is allowed; an UNQUOTED value containing the grammar's own
                // delimiters is an error because at runtime it either fails to parse or truncates silently.
                if (v.StartsWith("{loc:T ", StringComparison.Ordinal) && v.EndsWith("}", StringComparison.Ordinal))
                {
                    string body = Unwrap(v);
                    string inner = v[7..^1].Trim();
                    bool quoted = inner.Length >= 2 && inner[0] == '\'' && inner[^1] == '\'';
                    if (!quoted && inner.IndexOfAny([',', '=', '{', '}']) >= 0)
                        problems.Add($"{name}:{Line(attr)}: unquoted {{loc:T}} value contains , = {{ or }} — quote it or use LocRich.Source: {Excerpt(inner)}");
                    Add(name, attr, body, rich: false);
                }
                // Prose with inline markup; any prefix bound to the ControllerWheel namespace.
                else if (IsRichAttribute(attr))
                    Add(name, attr, v, rich: true);
            }

        void Add(string file, XAttribute attr, string s, bool rich)
        {
            if (s.Length == 0) { problems.Add($"{file}:{Line(attr)}: empty localizable value"); return; }
            string? why = AttributeValueProblem(s);
            if (why is not null) problems.Add($"{file}:{Line(attr)}: {why}: {Excerpt(s)}");
            found.Add(new XamlString(file, Line(attr), s, rich, attr));
        }
        return found;
    }

    public static bool IsRichAttribute(XAttribute attr) =>
        attr.Name.LocalName == "LocRich.Source" && attr.Name.NamespaceName.Contains("ControllerWheel", StringComparison.Ordinal);

    /// <summary>True for an attribute value of the form {loc:T …}.</summary>
    public static bool IsLocT(string v) =>
        v.StartsWith("{loc:T ", StringComparison.Ordinal) && v.EndsWith("}", StringComparison.Ordinal);

    /// <summary>The English text inside a {loc:T …} value (bare or quoted with \' escapes).</summary>
    public static string Unwrap(string v)
    {
        string body = v[7..^1].Trim();
        if (body.Length >= 2 && body[0] == '\'' && body[^1] == '\'') body = body[1..^1].Replace("\\'", "'");
        return body;
    }

    /// <summary>The {loc:T …} form for a value: bare when the grammar allows it, quoted with \' escapes otherwise.</summary>
    public static string Quote(string v) =>
        v.IndexOfAny([',', '\'', '=']) >= 0 ? "{loc:T '" + v.Replace("'", "\\'") + "'}" : "{loc:T " + v + "}";

    /// <summary>Non-null when XML attribute normalization would make the runtime key differ from the authored text.</summary>
    public static string? AttributeValueProblem(string s) =>
        s.Contains('\n') || s.Contains('\t') || s.Contains("  ")
            ? "value contains a newline, tab or doubled space — XML attribute normalization makes the runtime key differ from the authored one"
            : null;

    public static int Line(XAttribute a) => a is IXmlLineInfo li && li.HasLineInfo() ? li.LineNumber : 0;

    public static string Excerpt(string s) => s.Length <= 70 ? s : s[..70] + "…";
}
