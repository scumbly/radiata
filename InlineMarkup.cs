using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ControllerWheel;

/// <summary>The one inline-markup vocabulary, rendered into WPF inlines: <c>**bold**</c>, <c>*italic*</c>,
/// <c>`code`</c>, <c>[[topic-id|Label]]</c> Help cross-links, <c>[Label](url)</c> web links, and the two-character
/// literal <c>\n</c> as a line break. Help topics, onboarding cards, setup wizards and toasts all author one
/// translatable string per sentence with this markup instead of assembling a sentence from styled
/// <c>&lt;Run&gt;</c> fragments — word order is the translator's, not the layout's. Unterminated markers render
/// literally.
/// <para>Lives in the shell, not <c>Core</c>: it returns <see cref="Inline"/>s. <c>Core/HelpLocalization</c>
/// validates the same grammar (<c>MarkupProblems</c>) and <c>Core/HelpHtmlExport</c> renders it to HTML.</para>
/// <para>⚠ Only AUTHORED strings (UiText, HelpContent) ever reach this parser. A slice label, a game name, a
/// package name or anything else a user or package supplies renders as plain text — a <c>[Label](url)</c> in
/// it would otherwise become a clickable link.</para></summary>
internal static class InlineMarkup
{
    /// <summary>Per-site rendering choices, so one parser serves Help, cards and toasts without forking.
    /// <paramref name="Navigate"/> null renders a <c>[[cross-link]]</c> as plain bold rather than a link that
    /// goes nowhere. <paramref name="BoldDecoration"/> is a property of the CARD (a click-through card
    /// underlines its lead-in as an affordance), never of the sentence. <paramref name="Chip"/> draws a
    /// <c>{pad:Triangle}</c> token as a button glyph; null names the button in text instead.</summary>
    internal sealed record Look(
        FontWeight BoldWeight,
        Brush LinkInk,
        Brush LinkHoverInk,
        Brush CodeInk,
        string? LinkToolTip = null,
        Action<string>? Navigate = null,
        TextDecorationCollection? BoldDecoration = null,
        Func<PadButton, Inline>? Chip = null);

    /// <summary>Body text outside Help: semibold emphasis, the site's navy for links and code.</summary>
    internal static readonly Look Body = new(
        FontWeights.SemiBold,
        Frozen(Color.FromRgb(0x33, 0x70, 0x8C)),
        Frozen(Color.FromRgb(0x3D, 0x40, 0x5B)),
        Frozen(Color.FromRgb(0x3D, 0x40, 0x5B)));

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Body inks with full bold weight, so a <c>[Label](url)</c> reads bold rather than semibold.</summary>
    internal static readonly Look BodyBold = Body with { BoldWeight = FontWeights.Bold };

    /// <summary>Card prose: full bold emphasis and the navy link ink the cards were authored with.</summary>
    internal static readonly Look Card = Body with { BoldWeight = FontWeights.Bold, LinkInk = Body.LinkHoverInk, LinkHoverInk = Body.LinkInk };

    /// <summary>A click-through card: the bold lead-in is underlined as the affordance.</summary>
    internal static readonly Look CardLink = Card with { BoldDecoration = Loc.Lang == "ar" ? null : TextDecorations.Underline };   // an underline sits in Arabic descenders; colour and weight carry the affordance there

    /// <summary>How a <c>[Label](url)</c> marks itself out: bold and underlined, because ink alone is not an
    /// affordance — the site inks are close enough to body text that a colour-only link reads as prose. A
    /// <c>[[cross-link]]</c> stays bold without the rule; it moves within the window rather than leaving it.
    /// ⚠ No underline under Arabic — it sits in the descenders; weight and colour carry it there.</summary>
    private static readonly TextDecorationCollection? WebLinkRule =
        Loc.Lang == "ar" ? null : TextDecorations.Underline;

    /// <summary>Looks addressable by name from <c>loc:LocRich.LookKey</c>, for a site with no resource
    /// dictionary to hold one. Unknown names render as <see cref="Body"/> — never nothing.</summary>
    internal static Look Named(string key) => key switch { "Card" => Card, "BodyBold" => BodyBold, "CardLink" => CardLink, _ => Body };

    /// <summary>Replace a TextBlock's inlines with the parsed text. Guarded: an empty result must still leave
    /// the block valid — a throw here on a Settings-hosted block takes the UI thread down.</summary>
    internal static void Fill(TextBlock tb, string text, Look look)
    {
        tb.Inlines.Clear();
        foreach (var inline in Parse(text, look)) tb.Inlines.Add(inline);
    }

    /// <summary>Markup stripped to plain prose (for narration and automation names): markers removed, links
    /// reduced to their labels, chips spoken by name (ControllerButtons.Spoken), and <c>\n</c> to a space.</summary>
    internal static string Flatten(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var inline in Parse(text, Body with { Chip = b => new Run(ControllerButtons.Spoken(b)) }))
            switch (inline)
            {
                case Run r: sb.Append(r.Text); break;
                case Hyperlink h: foreach (var i in h.Inlines) if (i is Run hr) sb.Append(hr.Text); break;
                case LineBreak: sb.Append(' '); break;
            }
        return sb.ToString();
    }

    /// <summary>The D-pad's left/right glyph pair. It names PHYSICAL buttons, so it is never mirrored: every
    /// language, Arabic included, stores and displays it in this order.</summary>
    internal const string ArrowPair = "🡄 🡆";

    /// <summary>Wraps each <see cref="ArrowPair"/> in a left-to-right island, the markup form of bidi isolation.
    /// Without it the two neutral glyphs take the direction of whatever surrounds them, so inside Arabic they
    /// reverse next to Arabic words but not next to Latin ones — no stored order is right everywhere. Locale
    /// values may not carry bidi control characters (docs/LOCALIZATION.md), so the island is built here.
    /// Harmless in a left-to-right paragraph.</summary>
    internal static Inline IsolateArrows(Inline inline)
    {
        if (inline is Hyperlink link)
        {
            foreach (var child in link.Inlines.ToList())
            {
                var isolated = IsolateArrows(child);
                if (!ReferenceEquals(isolated, child)) { link.Inlines.InsertBefore(child, isolated); link.Inlines.Remove(child); }
            }
            return link;
        }
        if (inline is not Run run || !run.Text.Contains(ArrowPair, StringComparison.Ordinal)) return inline;

        var span = new Span();
        // Carry the run's own formatting to the span so every piece inherits it.
        foreach (var dp in new DependencyProperty[] { TextElement.FontWeightProperty, TextElement.FontStyleProperty,
                     TextElement.FontFamilyProperty, TextElement.FontSizeProperty, TextElement.ForegroundProperty,
                     Inline.TextDecorationsProperty })
            if (run.ReadLocalValue(dp) != DependencyProperty.UnsetValue) span.SetValue(dp, run.GetValue(dp));

        var parts = run.Text.Split(ArrowPair);
        for (int p = 0; p < parts.Length; p++)
        {
            if (parts[p].Length > 0) span.Inlines.Add(new Run(parts[p]));
            if (p < parts.Length - 1)
                span.Inlines.Add(new Span(new Run(ArrowPair)) { FlowDirection = System.Windows.FlowDirection.LeftToRight });
        }
        return span;
    }

    internal static IEnumerable<Inline> Parse(string text, Look look) => ParseRaw(text, look).Select(IsolateArrows);

    private static IEnumerable<Inline> ParseRaw(string text, Look look)
    {
        int i = 0;
        while (i < text.Length)
        {
            int bold = text.IndexOf("**", i, StringComparison.Ordinal);
            int ital = text.IndexOf('*', i);
            int code = text.IndexOf('`', i);
            int link = text.IndexOf("[[", i, StringComparison.Ordinal);
            int web  = text.IndexOf('[', i);              // may coincide with `link` — tie broken below
            int brk  = text.IndexOf("\\n", i, StringComparison.Ordinal);
            int chip = text.IndexOf("{pad:", i, StringComparison.Ordinal);   // {pad:Triangle} — a controller-button chip
            if (ital == bold) ital = -1;                  // a ** pair is bold, never two italics
            int next = new[] { bold, ital, code, link, web, brk, chip }.Where(n => n >= 0).DefaultIfEmpty(-1).Min();
            if (next < 0) { yield return new Run(text[i..]); yield break; }
            if (next > i) yield return new Run(text[i..next]);

            if (next == brk)
            {
                yield return new LineBreak();
                i = brk + 2;
            }
            else if (next == chip)
            {
                // Drawn by the site (Look.Chip) or named in text; a malformed token renders literally.
                int end = text.IndexOf('}', chip + 5);
                if (end < 0 || !Enum.TryParse<PadButton>(text[(chip + 5)..end], out var button))
                { yield return new Run(text.Substring(chip, 5)); i = chip + 5; continue; }
                yield return look.Chip?.Invoke(button) ?? new Run(ControllerButtons.Label(button));
                i = end + 1;
            }
            else if (next == link)
            {
                int end = text.IndexOf("]]", link + 2, StringComparison.Ordinal);
                if (end < 0) { yield return new Run(text[link..]); yield break; }
                var body  = text[(link + 2)..end];
                int pipe  = body.IndexOf('|');
                var id    = pipe >= 0 ? body[..pipe] : body;
                var label = pipe >= 0 ? body[(pipe + 1)..] : body;
                if (look.Navigate is null)
                    yield return new Run(label) { FontWeight = look.BoldWeight };
                else
                {
                    var navigate = look.Navigate;
                    yield return Link(new Run(label) { FontWeight = look.BoldWeight }, look, look.LinkToolTip, () => navigate(id));
                }
                i = end + 2;
            }
            else if (next == bold)
            {
                int end = text.IndexOf("**", bold + 2, StringComparison.Ordinal);
                if (end < 0) { yield return new Run(text[bold..]); yield break; }
                yield return new Run(text[(bold + 2)..end]) { FontWeight = look.BoldWeight, TextDecorations = look.BoldDecoration };
                i = end + 2;
            }
            else if (next == ital)
            {
                // *italic*: a closing single star that isn't the start of a ** pair; a lone star is literal.
                int end = text.IndexOf('*', ital + 1);
                if (end < 0 || end == ital + 1 || (end + 1 < text.Length && text[end + 1] == '*'))
                { yield return new Run("*"); i = ital + 1; continue; }
                yield return new Run(text[(ital + 1)..end]) { FontStyle = FontStyles.Italic };
                i = end + 1;
            }
            else if (next == web)
            {
                // [Label](url) — a "[" not followed by "](" renders as itself, so a bracket in prose is safe.
                int closeBracket = text.IndexOf(']', web + 1);
                if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
                { yield return new Run(text.Substring(web, 1)); i = web + 1; continue; }
                int closeParen = text.IndexOf(')', closeBracket + 2);
                if (closeParen < 0)
                { yield return new Run(text.Substring(web, 1)); i = web + 1; continue; }
                var label = text[(web + 1)..closeBracket];
                var url   = text[(closeBracket + 2)..closeParen];
                yield return Link(new Run(label) { FontWeight = look.BoldWeight }, look, url,
                                  () => OpenWebLink(url), WebLinkRule);
                i = closeParen + 1;
            }
            else
            {
                int end = text.IndexOf('`', code + 1);
                if (end < 0) { yield return new Run(text[code..]); yield break; }
                yield return new Run(text[(code + 1)..end]) { FontFamily = new FontFamily("Consolas"), Foreground = look.CodeInk };
                i = end + 1;
            }
        }
    }

    /// <summary><paramref name="rule"/> null suppresses the Hyperlink's own underline; pass one to keep it.</summary>
    private static Hyperlink Link(Run content, Look look, string? tooltip, Action click,
                                  TextDecorationCollection? rule = null)
    {
        var h = new Hyperlink(content)
        {
            Foreground = look.LinkInk,
            TextDecorations = rule,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = tooltip,
        };
        h.Click      += (_, _) => click();
        h.MouseEnter += (_, _) => h.Foreground = look.LinkHoverInk;   // suppress the default red hover
        h.MouseLeave += (_, _) => h.Foreground = look.LinkInk;
        return h;
    }

    private static void OpenWebLink(string url)
    {
        // Only web links leave the process. Every caller today hands this authored constants, but the shell
        // would happily run a file: or javascript: target if a future caller ever fed it package or config text.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        { System.Diagnostics.Trace.WriteLine($"[Help] web link refused (not http/https): {url}"); return; }
        // The tip-jar link is authored once in English; the site keeps a page per language, so it lands there.
        if (url is "https://getradiata.app/tip" or "https://getradiata.app/tip/") url = Loc.TipUrl;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Help] web link open failed: {ex.Message}"); }
    }
}
