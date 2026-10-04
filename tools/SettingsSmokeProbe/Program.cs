using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Radiata.SettingsSmokeProbe;

/// <summary>
/// Headless smoke test for the Settings window's XAML.
///
/// WHY THIS EXISTS. A resource-lookup mistake in XAML compiles perfectly and only blows up when the markup
/// is PARSED at runtime — i.e. the first time the user opens Settings. A style added to SettingsTheme.xaml
/// (merged at the WINDOW level) but referenced from an editor UserControl with {StaticResource}, which
/// resolves at parse time before the window's dictionary is in scope, compiles clean and throws "Cannot
/// find resource named '...'" the moment Settings opens. `dotnet build` cannot catch this class of bug —
/// only realizing the visual tree can.
///
/// WHAT IT DOES. Reproduces the real hosting arrangement as closely as a headless run allows:
///   1. loads Radiata's App.xaml resources (HeadingInk, UiGhostInk, … — the app-level dictionary),
///   2. merges SettingsTheme.xaml at the WINDOW level, exactly as SettingsWindow does,
///   3. constructs every Settings editor UserControl (this is where a StaticResource miss throws),
///   4. shows the window off-screen and forces a layout pass, so templates are applied and DynamicResource
///      references actually resolve,
///   5. asserts the shared styles it expects really are reachable from the window.
///
/// WHAT IT DOES NOT DO. It never calls the controls' Load(config)/data paths — no config, no audio devices,
/// no controller, no disk writes. It is a markup/resource check, not a functional test.
///
/// USAGE.  dotnet build ControllerWheel.csproj -c Debug       (the probe references that output)
///         dotnet run --project tools\SettingsSmokeProbe
/// Exit code 0 = pass; otherwise the number of failures. Diagnostics go to stderr.
///
/// EXTENDING. Add new Settings-hosted UserControls to <see cref="EditorControls"/> and any new shared
/// window-level style key to <see cref="WindowLevelStyles"/>. Keep it dependency-free and fast.
/// </summary>
internal static class Program
{
    /// <summary>Every UserControl the Settings window hosts — each one gets constructed and realized.</summary>
    private static readonly string[] EditorControls =
    [
        "SystemEditorControl",
        "WheelEditorControl",
        "ExceptionsEditorControl",
        "CustomizeEditorControl",
        "HelpEditorControl",
    ];

    /// <summary>Styles that live in SettingsTheme.xaml (window level) and MUST be reachable from a window
    /// that merges it. A missing entry here means a control referencing it would fail at runtime.</summary>
    private static readonly (string Key, Type Target)[] WindowLevelStyles =
    [
        ("HelpChip", typeof(Button)),   // the circled "?" that opens a Help topic
        // Keyboard-focus outline for the focusable Borders (tiles / Help rows / icon well) — referenced
        // via DynamicResource, so a missing key silently reverts to the dotted default; assert it here.
        ("AccessFocusVisual", typeof(Control)),
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        // Isolate AppPaths.AppDataDir before any type that reads it initializes (same seam TestHarness
        // uses), so a dialog that pre-loads stored state on construction (DiscordSetupWindow) finds
        // nothing and never touches the user's real config or secrets.
        var testData = Path.Combine(Path.GetTempPath(), "radiata-settings-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testData);
        AppContext.SetData("Radiata.TestDataDirectory", testData);

        int failures = 0;

        // `--lang <code>`: run the whole probe in that language (the pseudo-locales qps / qps-rtl are the point —
        // a resource that only breaks once a language is applied, or a mirrored window that throws, shows here).
        if (Array.IndexOf(args, "--lang") is int langIdx and >= 0 && langIdx + 1 < args.Length)
        {
            ControllerWheel.Loc.Init(args[langIdx + 1]);
            Log("language: " + ControllerWheel.Loc.Lang + (ControllerWheel.Loc.IsRtl ? " (right-to-left)" : ""));
        }

        // 1. App-level resources. Constructing Radiata's own App and calling the generated
        //    InitializeComponent populates Application.Current.Resources from App.xaml without running
        //    OnStartup (no tray icon, no controller, no config). Application's constructor queues its Startup
        //    callback, and App.OnStartup is the real launch: the first dispatcher pump would run it — beside
        //    a running Radiata it signals that instance to show Settings and Shutdown()s every window here,
        //    otherwise it boots the tray app. The callback is aborted before anything can pump.
        ControllerWheel.App app;
        try
        {
            var queuedByCtor = new List<System.Windows.Threading.DispatcherOperation>();
            void Queued(object sender, System.Windows.Threading.DispatcherHookEventArgs e) => queuedByCtor.Add(e.Operation);
            var hooks = System.Windows.Threading.Dispatcher.CurrentDispatcher.Hooks;
            hooks.OperationPosted += Queued;
            try { app = new ControllerWheel.App(); }
            finally { hooks.OperationPosted -= Queued; }
            if (queuedByCtor.Count(op => op.Abort()) == 0)
                throw new InvalidOperationException("Application queued no Startup callback to abort; a dispatcher pump may run App.OnStartup");
            app.InitializeComponent();
            Log("app.xaml resources loaded");
        }
        catch (Exception ex)
        {
            Log("FAIL: could not load App.xaml resources -> " + Flatten(ex));
            return 1;
        }

        // 2. A window that merges SettingsTheme.xaml, mirroring SettingsWindow's own setup.
        var window = new Window
        {
            Left = -32000, Top = -32000, Width = 1000, Height = 800,
            ShowInTaskbar = false, ShowActivated = false,
        };
        try
        {
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("pack://application:,,,/Radiata;component/SettingsTheme.xaml") });
            Log("SettingsTheme.xaml merged at window level");
        }
        catch (Exception ex)
        {
            Log("FAIL: could not merge SettingsTheme.xaml -> " + Flatten(ex));
            return 1;
        }

        var panel = new StackPanel();
        window.Content = panel;

        // 3. Construct each editor control. A {StaticResource} pointing at a window-level key throws HERE.
        var assembly = typeof(ControllerWheel.App).Assembly;
        foreach (var name in EditorControls)
        {
            try
            {
                var type = assembly.GetType("ControllerWheel." + name, throwOnError: true)!;
                panel.Children.Add((UIElement)Activator.CreateInstance(type)!);
                Log($"  ok    {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Log($"  FAIL  {name} -> {Flatten(ex)}");
            }
        }

        // 4. Realize the tree: applies templates and resolves {DynamicResource} references for real.
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Close();
            Log("layout pass completed");
        }
        catch (Exception ex)
        {
            failures++;
            Log("FAIL: layout pass -> " + Flatten(ex));
        }

        // 5. Shared window-level styles must be findable from the window.
        foreach (var (key, target) in WindowLevelStyles)
        {
            if (window.TryFindResource(key) is Style style && style.TargetType == target)
                Log($"  ok    style '{key}' -> {style.TargetType.Name}");
            else
            {
                failures++;
                Log($"  FAIL  style '{key}' not found (or wrong TargetType) from a theme-merged window");
            }
        }

        // 5b. Setup dialogs opened FROM Settings. Their markup is parsed the first time the user presses the
        //     button, so they carry the same runtime-only resource risk the editors do. DiscordSetupWindow
        //     pre-loads stored credentials on construction; the isolated AppDataDir set at the top of Main
        //     keeps that read off the user's real config, so it can be realized here too.
        try
        {
            var dlg = new ControllerWheel.ObsSetupWindow(4455, null);
            dlg.Close();
            Log("  ok    ObsSetupWindow");
        }
        catch (Exception ex)
        {
            failures++;
            Log("  FAIL  ObsSetupWindow -> " + Flatten(ex));
        }
        try
        {
            var dlg = new ControllerWheel.DiscordSetupWindow();
            dlg.Close();
            Log("  ok    DiscordSetupWindow");
        }
        catch (Exception ex)
        {
            failures++;
            Log("  FAIL  DiscordSetupWindow -> " + Flatten(ex));
        }

        // The icon grid's tile template resolves its styles only when a row is REALIZED, so this one is
        // shown off-screen and laid out: it must open unfiltered with tiles built and the current glyph's
        // row scrolled into view.
        try
        {
            var dlg = new ControllerWheel.IconPickerWindow("Star")
            {
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000,
                ShowActivated = false,
            };
            dlg.Show();
            dlg.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            dlg.UpdateLayout();
            var search = (TextBox)dlg.FindName("SearchBox");
            var sv = Descendants(dlg).OfType<ScrollViewer>().First(s => s.Name == "Scroller");
            var tileButtons = Descendants(dlg).OfType<Button>().Where(b => b.Tag is string).ToList();
            int tiles = tileButtons.Count;
            // "In view" is judged by where the current tile lands, not by the scroll offset: on a tall enough
            // viewport an early row is centred at offset 0, so the offset depends on the display, the tile's
            // visibility does not.
            var star = tileButtons.FirstOrDefault(b => string.Equals((string)b.Tag, "Star", StringComparison.OrdinalIgnoreCase));
            double top = star is null ? double.NaN : star.TransformToAncestor(sv).Transform(new Point(0, 0)).Y;
            bool inView = star is not null && top >= -0.5 && top + star.ActualHeight <= sv.ViewportHeight + 0.5;
            bool ok = search.Text.Length == 0 && tiles > 0 && inView;
            dlg.Close();
            if (!ok) throw new Exception($"search='{search.Text}' tiles={tiles} starTop={top:0.#} viewport={sv.ViewportHeight:0.#} offset={sv.VerticalOffset:0.#}");
            Log($"  ok    IconPickerWindow (tiles realized={tiles}, offset={sv.VerticalOffset:0})");
        }
        catch (Exception ex)
        {
            failures++;
            Log("  FAIL  IconPickerWindow -> " + Flatten(ex));
        }

        // 6. FUNCTIONAL: the Customize tiles and Help topic rows. These are Border+MouseLeftButtonUp
        //    surfaces whose selection plumbing (SelectTile/_rows) finds children BY TYPE — a conversion
        //    to real controls would turn those lookups into silent no-ops that steps 1-5 cannot see
        //    (build green, probe green, highlights simply stop). Every check here drives the SHIPPING
        //    click path programmatically and asserts both the visual selection and the persisted value,
        //    so that class of regression — and any future retemplating — fails loudly instead.
        failures += FunctionalChecks(panel);

        Log(failures == 0 ? "PROBE PASSED" : $"PROBE FAILED ({failures})");
        return failures;
    }

    /// <summary>Raise the same event the mouse would, on the same element — the shipping handler runs,
    /// not a copy of its logic.</summary>
    private static void Click(UIElement el) =>
        el.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonUpEvent, Source = el });

    private static int FunctionalChecks(StackPanel panel)
    {
        int failures = 0;

        void Check(string what, bool ok, string detail = "")
        {
            if (ok) Log($"  ok    {what}");
            else { failures++; Log($"  FAIL  {what}{(detail.Length > 0 ? " — " + detail : "")}"); }
        }
        // Tile clicks preview their sound theme — keep the probe silent (and endpoint-independent).
        // Sfx is internal to the app assembly, hence reflection; a rename fails the run loudly below.
        try
        {
            typeof(ControllerWheel.App).Assembly.GetType("ControllerWheel.Sfx", throwOnError: true)!
                .GetProperty("Enabled")!.SetValue(null, false);
        }
        catch (Exception ex) { failures++; Log("  FAIL  could not silence Sfx -> " + Flatten(ex)); return failures; }

        // ── Customize tiles: click every tile, assert the highlight moved AND ApplyTo reflects it ──
        try
        {
            var cust = panel.Children.OfType<ControllerWheel.CustomizeEditorControl>().FirstOrDefault();
            Check("CustomizeEditorControl present", cust is not null);
            if (cust is not null)
            {
                cust.Load(new ControllerWheel.SystemConfig(), ControllerWheel.ControllerKind.DualSenseEdge);

                // (grid names, expected tile Tags, extractor: what ApplyTo must report after clicking that
                // Tag). A group may span SEVERAL grids: Material is split across the Simple and Deluxe
                // boxes, and the "everything else deselects" assertion has to see both.
                (string[] Grids, string[] Tags, Func<ControllerWheel.SystemConfig, string, bool> Applied)[] groups =
                [
                    (["ThicknessTiles"], ["thin", "medium", "thick"],
                        (cfg, tag) => cfg.SliceThickness == tag),
                    (["SliceMaterialTiles", "SliceMaterialTilesDeluxe"],
                        ["flat-light", "pearl", "flat-dark", "obsidian", "kawaii", "salvage", "mesa", "reactor"],
                        (cfg, tag) => cfg.SliceMaterial == tag && cfg.GameGridMaterial == tag),
                    (["SoundFxTiles"], ["material", "digital", "physical", "none"],
                        (cfg, tag) => tag == "none" ? !cfg.SoundEffects : cfg.SoundEffects && cfg.SoundTheme == tag),
                    (["ButtonGlyphsTiles"], ["auto", "playstation", "xbox"],
                        (cfg, tag) => cfg.ButtonGlyphs == tag),
                ];

                // The keyboard/UIA path: every tile must be a focusable InvokableBorder whose stored action
                // does what the click does. Run it on one tile — the per-tag loop below already proves each
                // tag's click; this proves the second entry point reaches the same handler.
                if (cust.FindName("ThicknessTiles") is Panel tgrid
                    && tgrid.Children.OfType<ControllerWheel.InvokableBorder>().ToList() is { } itiles)
                {
                    Check("tiles are focusable InvokableBorders with a wired action",
                          itiles.Count == 3 && itiles.All(t => t.Focusable && t.Invoked is not null),
                          $"count={itiles.Count}");
                    var medium = itiles.FirstOrDefault(t => (string?)t.Tag == "medium");
                    if (medium?.Invoked is { } act)
                    {
                        act();   // the exact action Space/Enter and UIA Invoke run
                        Check("keyboard/UIA invoke selects like a click",
                              medium.BorderThickness.Left == 3
                              && cust.ApplyTo(new ControllerWheel.SystemConfig()).SliceThickness == "medium");
                    }
                }
                else Check("ThicknessTiles are InvokableBorders", false);

                foreach (var (gridNames, tags, applied) in groups)
                {
                    string gridName = string.Join("+", gridNames);
                    var panels = gridNames.Select(n => cust.FindName(n) as Panel).ToList();
                    if (panels.Any(p => p is null))
                    { Check($"{gridName} found", false); continue; }

                    // The type filter the shipping SelectTile uses — if this stops matching, so does it.
                    var tiles = panels.SelectMany(p => p!.Children.OfType<System.Windows.Controls.Border>()).ToList();
                    Check($"{gridName}: {tags.Length} Border tiles with the expected Tags",
                          tiles.Count == tags.Length
                          && tags.All(t => tiles.Any(b => (string?)b.Tag == t)),
                          $"found {tiles.Count}: {string.Join(",", tiles.Select(b => b.Tag))}");

                    foreach (var tag in tags)
                    {
                        var tile = tiles.FirstOrDefault(b => (string?)b.Tag == tag);
                        if (tile is null) continue;
                        Click(tile);
                        bool highlighted = tile.BorderThickness.Left == 3
                            && tiles.Where(b => !ReferenceEquals(b, tile)).All(b => b.BorderThickness.Left == 1);
                        var after = cust.ApplyTo(new ControllerWheel.SystemConfig());
                        Check($"{gridName}[{tag}]: click selects + persists",
                              highlighted && applied(after, tag),
                              $"highlight={highlighted} applied={applied(after, tag)}");
                    }
                }
            }
        }
        catch (Exception ex) { failures++; Log("  FAIL  Customize tile checks threw -> " + Flatten(ex)); }

        // ── Help topic rows: click a row, assert it selects and renders its topic ──
        try
        {
            var help = panel.Children.OfType<ControllerWheel.HelpEditorControl>().FirstOrDefault();
            Check("HelpEditorControl present", help is not null);
            if (help is not null && help.FindName("TopicList") is StackPanel topics)
            {
                var rows = topics.Children.OfType<System.Windows.Controls.Border>().ToList();
                Check("Help topic rows exist (Borders in TopicList)", rows.Count > 10, $"found {rows.Count}");
                if (rows.Count > 1)
                {
                    var target = rows[1];
                    Click(target);
                    bool selected = target.Background != System.Windows.Media.Brushes.Transparent
                        && rows.Count(r => r.Background != System.Windows.Media.Brushes.Transparent) == 1;
                    Check("clicking a row selects exactly that row", selected,
                          $"highlighted={rows.Count(r => r.Background != System.Windows.Media.Brushes.Transparent)}");
                    bool bodyFilled = help.FindName("TopicBody") is Panel body && body.Children.Count > 0;
                    Check("the clicked row rendered its topic body", bodyFilled);
                    CheckHelpTextIsSelectable(help, rows, Check);
                }
            }
        }
        catch (Exception ex) { failures++; Log("  FAIL  Help row checks threw -> " + Flatten(ex)); }

        return failures;
    }

    /// <summary>Every line of a Help topic must be drag-selectable, INCLUDING the ones carrying cross-links —
    /// SelectableText once skipped those, which silently made whole paragraphs uncopyable. The observable
    /// signature of an attached editor is the I-beam cursor.</summary>
    private static void CheckHelpTextIsSelectable(ControllerWheel.HelpEditorControl help,
                                                  List<System.Windows.Controls.Border> rows,
                                                  Action<string, bool, string> check)
    {
        // Walk rows until one renders a topic containing a cross-link, so the check actually covers them.
        var linked = new List<TextBlock>();
        foreach (var row in rows)
        {
            Click(row);
            if (help.FindName("TopicBody") is not Panel b) continue;
            var blocks = Descendants(b).OfType<TextBlock>().ToList();
            if (blocks.Any(HasHyperlink)) { linked = blocks; break; }
        }
        check("a Help topic with cross-links was found to test", linked.Count > 0, "");
        if (linked.Count == 0) return;

        var dead = linked.Where(t => t.Cursor != System.Windows.Input.Cursors.IBeam).ToList();
        check("every line of the topic is selectable", dead.Count == 0,
              dead.Count == 0 ? "" : $"{dead.Count}/{linked.Count} unselectable, first: \"{Snippet(dead[0])}\"");
        check("the cross-linked lines are selectable too",
              linked.Where(HasHyperlink).All(t => t.Cursor == System.Windows.Input.Cursors.IBeam), "");
    }

    private static bool HasHyperlink(TextBlock tb) =>
        tb.Inlines.OfType<System.Windows.Documents.Hyperlink>().Any();

    /// <summary>TextBlock.Text comes back empty for mixed inline content (the failing case here), so walk the
    /// inlines instead — otherwise a failure reports an unidentifiable blank line.</summary>
    private static string Snippet(TextBlock tb)
    {
        var sb = new System.Text.StringBuilder();
        void Walk(System.Windows.Documents.InlineCollection inlines)
        {
            foreach (var i in inlines)
            {
                if (i is System.Windows.Documents.Run r) sb.Append(r.Text);
                else if (i is System.Windows.Documents.Span s) Walk(s.Inlines);
                if (sb.Length >= 50) return;
            }
        }
        Walk(tb.Inlines);
        return sb.ToString(0, Math.Min(50, sb.Length));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    private static void Log(string message) => Console.Error.WriteLine(message);

    /// <summary>Full inner-exception chain — the useful part of a XamlParseException is always innermost
    /// ("Cannot find resource named 'X'"), which a bare .Message hides.</summary>
    private static string Flatten(Exception ex)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (var e = ex; e is not null; e = e.InnerException) parts.Add($"{e.GetType().Name}: {e.Message}");
        return string.Join("  <<<  ", parts);
    }
}
