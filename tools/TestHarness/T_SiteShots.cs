using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Renders site screenshots from the app's own drawing code, for when the picture is of a surface the
/// app draws itself and a live capture would need the app running on a desktop. Not a test and not in
/// the default set: <c>TestHarness launcher-shot</c>.
///
/// <para>The Arcade Launcher is drawn exactly as <c>ArcadeControl.RenderCore</c> draws its picker frame — the
/// bezel (<c>ArcadeChrome.DrawFrame</c>), then the carousel (<c>ArcadePickerRenderer.Draw</c>) — at rest on the
/// chosen cabinet, onto a transparent square. Firefly is registered from its sample folder, so its cabinet
/// shows the sample's own tint and seeded preview (the harness's isolated app data has no live capture).
/// Internode is left out of the ring: a public build withholds it and the pictures are for the public
/// site.</para></summary>
internal static class T_SiteShots
{
    public static void LauncherShot() => LauncherShot(null, null);

    /// <summary>The same render with Firefly retitled, written to <paramref name="outOverride"/> — for checking how
    /// the nameplate fits a long name (<c>TestHarness launcher-shot-long</c>, output in the temp folder).</summary>
    public static void LauncherShot(string titleOverride, string outOverride)
    {
        H.Group("Site shot — the Arcade Launcher with Firefly in front");
        var root = H.RepoRoot();
        if (root is null) { H.Fail("repo root not found"); return; }
        // Reads the sample package and writes into the site docroot, neither of which the public snapshot carries.
        if (!H.DevRepoOnly("site shot of the Arcade Launcher (packaging/sample-arcade-package, packaging/webhost)", root)) return;
        string outPath = outOverride ?? Path.Combine(root, "packaging", "webhost", "assets", "screenshots", "workshop-arcade-launcher.png");

        // pack:// art and fonts live in Radiata.dll, which is not the entry assembly here.
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // ResourceAssembly locks to the entry assembly on first read, and the harness has read it by now; this
        // render-only group repoints the backing field so pack:// finds the app's own art.
        try { Application.ResourceAssembly = H.App; }
        catch (InvalidOperationException)
        {
            typeof(Application).GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, H.App);
        }
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        string probe;
        try { probe = Application.GetResourceStream(new Uri("pack://application:,,,/" + "Assets/arcade/sprites/sprites-finished/cabinet-blank.png")) is null ? "null" : "ok"; }
        catch (Exception ex) { probe = ex.GetType().Name + ": " + ex.Message; }
        H.Check("launcher shot: the app's packed art resolves", probe == "ok", $"ResourceAssembly={Application.ResourceAssembly?.GetName().Name} probe={probe}");

        var firefly = PackageStore.Inspect(Path.Combine(root, "packaging", "sample-arcade-package", "firefly"), "arcade");
        if (firefly.ArcadeGame is null) { H.Fail("firefly sample loads", firefly.Error); return; }
        ArcadeCatalog.RegisterScripts([titleOverride is null ? firefly.ArcadeGame : firefly.ArcadeGame with { Title = titleOverride }]);

        var games = ArcadeCatalog.Games.Where(g => !string.Equals(g.Id, "internode", StringComparison.OrdinalIgnoreCase)).ToArray();
        typeof(ArcadeCatalog).GetField("_games", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, games);
        int index = Array.FindIndex(games, g => g.Id == firefly.ArcadeGame.Token);
        H.Check("launcher shot: Firefly is in the ring, Internode is not",
                index >= 0 && games.All(g => g.Id != "internode"), string.Join(",", games.Select(g => g.Id)));

        H.InvokeStatic(H.AppType("ArcadeShots"), "Preload", games.Select(g => g.Id));
        var pickerType = H.AppType("ArcadePickerRenderer");
        var picker = Activator.CreateInstance(pickerType, nonPublic: true);
        pickerType.GetMethod("Reset")!.Invoke(picker, [index, games.Length]);
        var accent = pickerType.GetMethod("Accent")!.Invoke(picker, [games[index]]) as Brush
                     ?? (Brush)H.GetStatic(H.AppType("ArcadeChrome"), "Ink");

        const double logical = 480, scale = 1.5;
        var c = new Point(logical / 2, logical / 2);
        double radius = logical / 2 - 2;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double field = (double)H.AppType("ArcadeChrome").GetMethod("DrawFrame")!.Invoke(null, [dc, c, radius, accent, false])!;
            pickerType.GetMethod("Draw")!.Invoke(picker, [dc, c, field, radius, scale, games, index]);
        }
        int px = (int)(logical * scale);
        var rtb = new RenderTargetBitmap(px, px, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = File.Create(outPath)) enc.Save(fs);
        H.Pass("launcher shot written", $"{outPath} ({px}x{px}, {new FileInfo(outPath).Length:N0} bytes)");
    }
}
