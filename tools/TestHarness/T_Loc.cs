using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Localization invariants no other gate sees.
///
/// The drawn surfaces are pinned left-to-right on purpose: their geometry is the stick's, and the aim math
/// in <c>Core/WheelStateMachine</c> never mirrors — so a right-to-left ancestor that flipped the drawing
/// would invert aiming (push right, arm the slice that appears left) while a button glyph's letters would
/// render backwards. A right-to-left language shapes its TEXT runs inside the drawing code instead. This
/// suite fails the moment any of those pins is removed or a new RTL language lands without the table
/// agreeing. Construction is reflective so the harness needs no WPF types of its own.</summary>
internal static class T_Loc
{
    public static void Run()
    {
        H.Group("Localization — drawn surfaces pinned left-to-right; language table consistent");

        var app = typeof(ControllerWheel.App).Assembly;
        foreach (var name in new[] { "RadialMenuControl", "ArcadeControl", "ControllerButton", "WheelMiniature" })
        {
            var t = app.GetType("ControllerWheel." + name);
            if (t is null) { H.Fail($"{name} not found in the app assembly"); continue; }
            object obj;
            try { obj = Activator.CreateInstance(t, nonPublic: true); }
            catch (Exception ex) { H.Fail($"{name} could not be constructed", ex.GetBaseException().Message); continue; }
            var flow = t.GetProperty("FlowDirection")?.GetValue(obj)?.ToString();
            H.Check($"{name} is pinned LeftToRight", flow == "LeftToRight", $"FlowDirection = {flow ?? "(none)"}");
        }

        // A pinned child under a right-to-left parent is flipped about its arranged slot, and WPF composes
        // that flip with a RenderTransform in a way that moves the content OUT of the slot (the toast
        // flower mark landed over its text). Scaling must be layout-side (LayoutTransform / Viewbox).
        foreach (var (name, ctorArgs) in new (string, object[])[] { ("FlowerMarkControl", new object[] { 44.0 }), ("AboutFlowerControl", Array.Empty<object>()) })
        {
            var t = app.GetType("ControllerWheel." + name);
            if (t is null) { H.Fail($"{name} not found in the app assembly"); continue; }
            object obj;
            try { obj = Activator.CreateInstance(t, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ctorArgs, null)!; }
            catch (Exception ex) { H.Fail($"{name} could not be constructed", ex.GetBaseException().Message); continue; }
            var flow = t.GetProperty("FlowDirection")?.GetValue(obj)?.ToString();
            H.Check($"{name} is pinned LeftToRight", flow == "LeftToRight", $"FlowDirection = {flow ?? "(none)"}");
            var rt = t.GetProperty("RenderTransform")?.GetValue(obj);
            bool identity = rt is null || (bool)(rt.GetType().GetProperty("Value")?.GetValue(rt)?.GetType().GetProperty("IsIdentity")?.GetValue(rt.GetType().GetProperty("Value")!.GetValue(rt)) ?? true);
            H.Check($"{name} scales by layout, not RenderTransform", identity, $"RenderTransform = {rt}");
        }

        // The window is XAML; constructing it would size to the primary screen, so read the source instead.
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "OverlayWindow.xaml"));
        H.Check("OverlayWindow.xaml pins FlowDirection=\"LeftToRight\" on its root",
                xaml.Contains("FlowDirection=\"LeftToRight\"", StringComparison.Ordinal));

        // Language table: every RTL language names a font chain, and the RTL set is exactly what we ship.
        var rtl = HelpLocalization.Languages.Where(l => l.Rtl && !l.Code.StartsWith("qps", StringComparison.Ordinal)).Select(l => l.Code).ToArray();
        H.Check("exactly the shipped RTL languages are flagged Rtl", rtl.SequenceEqual(new[] { "ar" }),
                string.Join(",", rtl));
        H.Check("every language names a font chain",
                HelpLocalization.Languages.All(l => !string.IsNullOrWhiteSpace(l.FontFamily)));
        H.Check("every language has a translation map or is the source",
                HelpLocalization.Languages.All(l => l.Code == HelpLocalization.DefaultCode || l.Code.StartsWith("qps", StringComparison.Ordinal)
                                                    || HelpLocalization.Text(l.Code, HelpLocalization.Chrome.Contents)
                                                       != HelpLocalization.Chrome.Contents));

        // Offering: English always; another language only once its UI catalog is complete. Help alone being
        // translated must not surface a language in the picker or through Windows-language detection.
        H.Check("English is always offered", Loc.Offered(HelpLocalization.DefaultCode));
        H.Check("an unknown code is not offered", !Loc.Offered("kl"));

        // Sequence figures mirror for right-to-left; spatial figures never do (L1 is on the left in every language).
        var seq = HelpFigures.All.Where(f => f.Sequence).Select(f => f.Id).Order().ToArray();
        H.Check("exactly the reading-order figures mirror",
                seq.SequenceEqual(new[] { "edit-move", "package-folder", "startup-order", "workshop-loop" }),
                string.Join(", ", seq));
        // The script coordinate system and the combo string are spatial whatever the language.
        H.Check("the playfield and key-combo figures never mirror",
                HelpFigures.ById("polar-playfield") is { Sequence: false } && HelpFigures.ById("key-combo-anatomy") is { Sequence: false });
        // A label's position is its box centre, which a mirror reflects to Width - centre.
        static double Centre(FigPart p) => p.Anchor == FigAnchor.TopLeft ? p.X + p.W / 2 : p.X;
        foreach (var f in HelpFigures.All)
        {
            var partsL = f.PartsFor(false); var partsR = f.PartsFor(true);
            H.Check($"{f.Id}: left-to-right draws the authored parts", ReferenceEquals(partsL, f.Parts));
            H.Check($"{f.Id}: every label's wrap box lies inside the canvas",
                    partsL.Where(p => p.Shape == FigShape.Text).All(p => Centre(p) - p.W / 2 >= -0.01 && Centre(p) + p.W / 2 <= f.Width + 0.01));
            if (!f.Sequence) { H.Check($"{f.Id}: spatial, so right-to-left draws the same parts", ReferenceEquals(partsR, f.Parts)); continue; }
            // Arrows and a tree's elbows alike: every horizontal stroke runs the other way once mirrored.
            static bool Horizontal(FigPart p) => p.Shape is FigShape.Arrow or FigShape.Line && p.Y == p.Y2 && p.X != p.X2;
            var strokesL = partsL.Where(Horizontal).ToArray();
            var strokesR = partsR.Where(Horizontal).ToArray();
            H.Check($"{f.Id}: horizontal arrows and strokes run the other way when mirrored",
                    strokesL.Length > 0 && strokesL.Zip(strokesR).All(t => Math.Sign(t.First.X2 - t.First.X) == -Math.Sign(t.Second.X2 - t.Second.X)));
            var labelsL = partsL.Where(p => p.Shape == FigShape.Text).OrderBy(Centre).ThenBy(p => p.Y).Select(p => p.Text).ToArray();
            var labelsR = partsR.Where(p => p.Shape == FigShape.Text).OrderBy(p => -Centre(p)).ThenBy(p => p.Y).Select(p => p.Text).ToArray();
            H.Check($"{f.Id}: labels swap ends when mirrored", labelsL.SequenceEqual(labelsR));
            H.Check($"{f.Id}: every label stays inside the canvas when mirrored",
                    partsR.Where(p => p.Shape == FigShape.Text).All(p => Centre(p) - p.W / 2 >= -0.01 && Centre(p) + p.W / 2 <= f.Width + 0.01));
            H.Check($"{f.Id}: mirroring keeps every part inside the canvas",
                    partsR.All(p => p.X >= -0.01 && p.X <= f.Width + 0.01 && (p.Shape is not (FigShape.Line or FigShape.Arrow) || (p.X2 >= -0.01 && p.X2 <= f.Width + 0.01))));
            H.Check($"{f.Id}: mirroring changes no translatable text",
                    partsL.Where(p => p.Text is not null).Select(p => p.Text).OrderBy(t => t).SequenceEqual(partsR.Where(p => p.Text is not null).Select(p => p.Text).OrderBy(t => t)));
        }
        H.Check("a hub readout translates only status words and defaults", Loc.Readout(UiText.Status.NoDevice) == "No Device" && Loc.Readout("Notepad") == "Notepad" && Loc.Readout(null) == "");
        H.Check("a plural with extra arguments formats count first", Loc.P("{1} deleted, {0} slice left", "{1} deleted, {0} slices left", 2, "Mute") == "Mute deleted, 2 slices left" && Loc.P("{0} game", "{0} games", 1) == "1 game");
        // Counted sentences enter the catalog as their plural key, never as the two halves — a translator sees one
        // row with all the forms the language needs, and the checker can count them.
        var src = Loc.SourceStrings().ToHashSet(StringComparer.Ordinal);
        H.Check("a One/Other pair enumerates as its plural key", src.Contains("plural:" + UiText.Onboarding.GameOne + "|" + UiText.Onboarding.GameOther)
                                                                    && !src.Contains(UiText.Onboarding.GameOne) && !src.Contains(UiText.Onboarding.GameOther));
        H.Check("the skeleton carries every catalog key and a per-language plural row",
                Loc.Skeleton("ar", ["Only in the shell"]).Contains("[\"Only in the shell\"] = \"TODO\"")
                && Loc.Skeleton("ar", ["plural:x one|x other"]).Contains("TODO|TODO|TODO|TODO|TODO|TODO")
                && Loc.Skeleton("ja", ["plural:x one|x other"]).Contains("[\"plural:x one|x other\"] = \"TODO\",")
                && Loc.Skeleton("es", ["plural:x one|x other"]).Contains("= \"TODO|TODO\","));

#if DEBUG
        // Pseudo-localization (dev builds): the transform must keep every placeholder, marker, id, URL and chip
        // token intact — a pseudoized "{0}" or "[[id|" would break formatting or links rather than reveal layout.
        string ps = Loc.Pseudoize("Open [the portal](https://x.y/z) and **Save {0}** in [[edit-mode|Edit]] with {pad:Cross}.\\n");
        H.Check("pseudoize keeps placeholders, links, ids and chips",
                ps.Contains("{0}") && ps.Contains("(https://x.y/z)") && ps.Contains("[[edit-mode|") && ps.Contains("{pad:Cross}") && ps.Contains("**") && ps.EndsWith("]") && ps.StartsWith("[") && ps.Contains("\\n"), ps);
        H.Check("pseudoize lengthens and marks the text", Loc.Pseudoize("Settings").Length > "Settings".Length + 2 && Loc.Pseudoize("Settings") != "Settings");
#endif

        // Default slice labels translate on display against a NARROW set: authored defaults only, so a game
        // named like a UI word is never rewritten and a product name never enters the catalog.
        H.Check("an authored default label is recognised", Loc.IsDefaultLabel(UiText.DefaultLabels.Mute) && Loc.IsDefaultLabel("Game Grid"));
        H.Check("a game name that is also an English word is not a default label", !Loc.IsDefaultLabel("Control") && !Loc.IsDefaultLabel("Journey"));
        H.Check("product names seeded as labels are not default labels", !Loc.IsDefaultLabel("Steam") && !Loc.IsDefaultLabel("Playnite"));
        H.Check("a user label passes through DefaultLabel untouched", Loc.DefaultLabel("My Stuff") == "My Stuff" && Loc.DefaultLabel(null) == "");
        H.Check("every AppConfig.Default label is a default or a product name",
                AppConfig.Default.WheelA.Concat(AppConfig.Default.WheelB).All(s => Loc.IsDefaultLabel(s.Label) || s.Label is "Steam" or "Playnite" or "Moonlight"));
        // Names the language AND the strings it is short of: "not offered" on its own sends the reader
        // hunting through four 1100-row maps, and the answer is usually a handful of keys someone added
        // English-only. A language whose map hasn't landed at all reports as such rather than listing
        // every string in the catalog.
        var shipped = HelpLocalization.Languages.Where(l => !l.Code.StartsWith("qps", StringComparison.Ordinal)).ToList();
        var shortfall = new List<string>();
        foreach (var l in shipped)
        {
            if (Loc.Offered(l.Code)) continue;
            var map = H.StaticMethod(typeof(Loc), "MapFor", 1)?.Invoke(null, new object[] { l.Code })
                      as IReadOnlyDictionary<string, string>;
            if (map is null) { shortfall.Add($"{l.Code}: no UI map"); continue; }
            var gaps = Loc.SourceStrings()
                          .Where(s => !map.TryGetValue(s, out var v) || string.IsNullOrWhiteSpace(v))
                          .ToList();
            shortfall.Add($"{l.Code}: {gaps.Count} untranslated — "
                          + string.Join(" | ", gaps.Take(6).Select(s => s.Length <= 60 ? s : s[..60] + "…")));
        }
        H.Check("every shipped language has a complete UI catalog and is offered",
                shortfall.Count == 0, shortfall.Count == 0 ? null : string.Join("; ", shortfall));

        // The dev-only exemption, from both ends. A [DevOnlyString] const is out of the catalog — without it
        // one untranslated dev string makes EVERY language incomplete, which withdraws all four from
        // Loc.Offered and silently costs every non-English user their language. The second half is what stops
        // the attribute becoming a general licence: a const reached by a shipping surface stays in.
        H.Check("a [DevOnlyString] const is out of the translation catalog",
                !src.Contains(UiText.Arcade.PhraseTester), UiText.Arcade.PhraseTester);
        H.Check("an ordinary const beside it is still in", src.Contains(UiText.Arcade.Checkpoint));
        var marked = typeof(UiText).GetNestedTypes().SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
                                   .Where(f => f.IsLiteral && f.IsDefined(typeof(DevOnlyStringAttribute), false))
                                   .Select(f => (string)f.GetRawConstantValue()).ToList();
        H.Check($"every exempted string is genuinely absent from the catalog ({marked.Count} marked)",
                marked.All(s => !src.Contains(s)), string.Join(" | ", marked));

        HelpLocaleOrphans();

        // Globalization must never be stripped from a shipping build: InvariantGlobalization=true replaces ICU
        // with invariant mode, and Arabic/Japanese culture behaviour dies silently (CultureInfo("ar") still
        // constructs). tools/ArcadePoc sets it and is the nearest csproj to copy from.
        foreach (var proj in new[] { "ControllerWheel.csproj", @"Core\ControllerWheel.Core.csproj", @"ArcadeHost\ArcadeHost.csproj" })
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), proj));
            H.Check($"{proj} does not set InvariantGlobalization=true",
                    !text.Contains("<InvariantGlobalization>true", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Every key in a HelpText{Es,De,Ja,Ar}.cs map must resolve to a string the current build still
    /// asks to translate — the same English-source set <c>HelpLocalization.SourceStrings()</c> collects from
    /// <see cref="HelpContent"/>, <see cref="HelpFigures"/> and its own <see cref="HelpLocalization.Chrome"/>.
    /// A key with no source is an orphan: an English edit or removal left it behind, and
    /// <c>--check-help-locales</c> reports it as STALE, and this check is what enforces deleting it.</summary>
    private static void HelpLocaleOrphans()
    {
        var source = HelpLocalization.SourceStrings().ToHashSet(StringComparer.Ordinal);
        var mapFor = H.StaticMethod(typeof(HelpLocalization), "MapFor", 1);
        if (mapFor is null) { H.Fail("HelpLocalization.MapFor not found", "renamed?"); return; }

        foreach (var code in new[] { "es", "de", "ja", "ar" })
        {
            var map = mapFor.Invoke(null, new object[] { code }) as IReadOnlyDictionary<string, string>;
            if (map is null) { H.Skip($"{code}: Help map orphans", "catalog seam missing"); continue; }
            var orphans = map.Keys.Where(k => !source.Contains(k)).ToList();
            H.Check($"{code}: every HelpText key resolves to a current English source string ({map.Count} rows)",
                    orphans.Count == 0,
                    orphans.Count == 0 ? null
                                       : $"{orphans.Count} orphaned: " + string.Join(" | ", orphans.Take(7).Select(k => k.Length <= 70 ? k : k[..70] + "…")));
        }
    }

    // Throws (unlike H.RepoRoot()'s null): every caller here builds a path from it immediately with no
    // null check, so a missing repo would otherwise surface as a confusing downstream NullReferenceException.
    private static string RepoRoot() =>
        H.RepoRoot() ?? throw new InvalidOperationException("repo root not found from " + AppContext.BaseDirectory);
}
