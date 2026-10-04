using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>One offscreen frame: a Pbgra32 pixel buffer and the size it was rendered at.</summary>
internal sealed record RenderedFrame(int Width, int Height, byte[] Pixels);

/// <summary>One matrix entry. <see cref="Render"/> runs when the entry is reached, in matrix order: the
/// renderers keep caches keyed on image identity and first-baked size, so an entry's pixels may depend on
/// what rendered before it. Snapshot and compare therefore always run the WHOLE matrix, never a subset.</summary>
internal readonly record struct RenderEntry(string Name, Func<RenderedFrame> Render);

/// <summary>The pixel-identity guard for refactors of the drawing code (<c>render:snapshot</c> /
/// <c>render:compare</c>). Not a test of how anything LOOKS and not in the default set: it proves a
/// change left every frame of a fixed matrix bit-identical.
///
/// <para><c>render:snapshot &lt;dir&gt;</c> renders the matrix (<see cref="T_RenderWheel"/>,
/// <see cref="T_RenderArcade"/>, <see cref="T_RenderToast"/>) offscreen and writes one PNG per entry plus
/// <c>manifest.json</c>: entry name → SHA-256 of the raw Pbgra32 buffer (never the PNG bytes).
/// <c>render:compare &lt;baselineDir&gt; [outDir]</c> re-renders and hashes; an entry that differs or is
/// missing FAILS with its differing-pixel count and largest channel delta, and gets an
/// <c>.actual.png</c> and a <c>.diff.png</c> in outDir (default <c>%TEMP%\radiata-render-compare</c>).</para>
///
/// <para>⚠ A baseline is only valid on the box and build configuration that took it: font rasterisation and
/// the installed system fonts reach the pixels. (The display scale does not: every root visual is pinned to
/// 96 DPI with <see cref="Unscaled{T}"/>, and a baseline taken at another scale is reported as such.) Snapshot
/// on this machine, refactor, compare on this machine.</para>
///
/// <para>Determinism: the wheel's animation clock (<c>RadialMenuControl.PinnedClockUtc</c>), its particle
/// roll (<c>FxRng</c>) and the arcade sprite clock (<c>ArcadeSprites.PinnedTime</c>) are pinned before every
/// entry; the game sims are deterministic by contract; the launcher's floor-spark roll is reseeded per entry.
/// Nothing here pumps the dispatcher while an entry is being drawn.</para></summary>
internal static class T_Render
{
    /// <summary>The fixed instant the pinned clocks read at the start of every entry.</summary>
    public static readonly DateTime Epoch = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    public const int FxSeed = 20260924;
    public const double SpriteEpochSeconds = 12.345;

    private const string ManifestName = "manifest.json";

    public static bool Handles(string[] args) =>
        args.Length > 0 && (Is(args[0], "render:snapshot") || Is(args[0], "render:compare"));

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static void Run(string[] args)
    {
        if (Is(args[0], "render:snapshot"))
        {
            if (args.Length < 2) { H.Fail("render:snapshot", "usage: render:snapshot <dir>"); return; }
            Snapshot(args[1]);
        }
        else
        {
            if (args.Length < 2) { H.Fail("render:compare", "usage: render:compare <baselineDir> [outDir]"); return; }
            Compare(args[1], args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "radiata-render-compare"));
        }
    }

    // ── The matrix ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Every entry, in the one order both modes use.</summary>
    private static IEnumerable<RenderEntry> Matrix()
    {
        foreach (var e in T_RenderWheel.Entries()) yield return e;
        foreach (var e in T_RenderToast.Entries()) yield return e;
        foreach (var e in T_RenderArcade.Entries()) yield return e;
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────────────────────────────

    private static void Snapshot(string dir)
    {
        H.Group("render:snapshot — the pixel-identity baseline");
        dir = Path.GetFullPath(dir);
        // A baseline is the whole matrix, so an older one's PNGs are cleared first — which is only safe in a folder
        // that holds nothing BUT a baseline.
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any()
            && !File.Exists(Path.Combine(dir, ManifestName)))
        {
            H.Fail("render:snapshot", $"{dir} is not empty and holds no {ManifestName}; give an empty or baseline folder");
            return;
        }
        if (!Prepare()) return;
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories)) File.Delete(old);
        File.Delete(Path.Combine(dir, ManifestName));

        var sw = Stopwatch.StartNew();
        var entries = new JsonObject();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int written = 0, roundTripMismatch = 0;
        foreach (var entry in Matrix())
        {
            if (!seen.Add(entry.Name)) { H.Fail($"duplicate entry name {entry.Name}"); continue; }
            var frame = RenderEntrySafely(entry);
            if (frame is null) continue;
            string png = PngPath(dir, entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(png)!);
            File.WriteAllBytes(png, EncodePng(frame));
            // The diff reads the baseline back from this PNG as straight BGRA, so it must hold exactly the
            // straight conversion of the buffer that was hashed.
            if (!DecodeStraight(png, out int w, out int h, out var back) || w != frame.Width || h != frame.Height
                || !back.AsSpan().SequenceEqual(Straight(frame)))
                roundTripMismatch++;
            entries[entry.Name] = new JsonObject
            {
                ["sha256"] = Sha(frame.Pixels),
                ["width"] = frame.Width,
                ["height"] = frame.Height,
            };
            written++;
        }

        var manifest = new JsonObject
        {
            ["format"] = 1,
            ["box"] = Environment.MachineName,
            ["dpiScale"] = T_RenderWheel.DpiScale,
            ["entryCount"] = written,
            ["entries"] = entries,
        };
        File.WriteAllText(Path.Combine(dir, ManifestName),
                          manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        H.Check("every PNG reads back as the straight form of the buffer it was hashed from",
                roundTripMismatch == 0, roundTripMismatch == 0 ? null : $"{roundTripMismatch} mismatched");
        H.Check($"render:snapshot wrote {written} entries", written > 0,
                $"{dir} in {sw.Elapsed.TotalSeconds:F1} s (dpiScale {T_RenderWheel.DpiScale})");
        Finish();
    }

    // ── Compare ───────────────────────────────────────────────────────────────────────────────────────

    private static void Compare(string baselineDir, string outDir)
    {
        H.Group("render:compare — pixel identity against a baseline");
        baselineDir = Path.GetFullPath(baselineDir);
        string manifestPath = Path.Combine(baselineDir, ManifestName);
        if (!File.Exists(manifestPath)) { H.Fail("baseline manifest", $"no {manifestPath}"); return; }
        if (!Prepare()) return;

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var baseline = manifest["entries"]!.AsObject();
        double baseDpi = manifest["dpiScale"]?.GetValue<double>() ?? 1.0;
        if (Math.Abs(baseDpi - T_RenderWheel.DpiScale) > 1e-9)
            H.Fail("baseline display scale matches this process",
                   $"baseline {baseDpi}, now {T_RenderWheel.DpiScale} — every entry will differ");

        outDir = Path.GetFullPath(outDir);
        if (Directory.Exists(outDir))
            foreach (var old in Directory.EnumerateFiles(outDir, "*.png", SearchOption.AllDirectories))
                if (old.EndsWith(".actual.png", StringComparison.OrdinalIgnoreCase)
                    || old.EndsWith(".diff.png", StringComparison.OrdinalIgnoreCase))
                    File.Delete(old);

        var sw = Stopwatch.StartNew();
        var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int same = 0, differ = 0, missingFromBaseline = 0, failedRender = 0;
        foreach (var entry in Matrix())
        {
            if (!rendered.Add(entry.Name)) { H.Fail($"duplicate entry name {entry.Name}"); continue; }
            var frame = RenderEntrySafely(entry);
            if (frame is null) { failedRender++; continue; }
            if (baseline[entry.Name] is not JsonObject want)
            {
                missingFromBaseline++;
                H.Fail(entry.Name, "missing from the baseline (a new entry — snapshot again before the change)");
                WriteArtifacts(outDir, entry.Name, frame, null);
                continue;
            }
            string sha = Sha(frame.Pixels);
            int bw = want["width"]!.GetValue<int>(), bh = want["height"]!.GetValue<int>();
            if (sha == want["sha256"]!.GetValue<string>() && bw == frame.Width && bh == frame.Height)
            {
                same++;
                H.Pass(entry.Name);
                continue;
            }
            differ++;
            H.Fail(entry.Name, Describe(baselineDir, entry.Name, frame, bw, bh, out var basePx));
            WriteArtifacts(outDir, entry.Name, frame, basePx);
        }

        int missingFromMatrix = 0;
        foreach (var kv in baseline)
            if (!rendered.Contains(kv.Key))
            {
                missingFromMatrix++;
                H.Fail(kv.Key, "in the baseline but no longer in the matrix");
            }

        Console.WriteLine();
        Console.WriteLine($"  render:compare — {same} identical, {differ} differ, {missingFromBaseline} new, "
                          + $"{missingFromMatrix} gone, {failedRender} failed to render "
                          + $"(baseline {baseline.Count}, rendered {rendered.Count}) in {sw.Elapsed.TotalSeconds:F1} s"
                          + (differ + missingFromBaseline > 0 ? $"; diffs in {outDir}" : ""));
        Finish();
    }

    /// <summary>What changed, in the terms a reviewer needs: how many pixels and by how much. Counted on the
    /// straight (un-premultiplied) form, which is exactly what the baseline PNG holds and is one-to-one with
    /// the premultiplied buffer; the delta is reported back in premultiplied units, which is what reaches
    /// the screen.</summary>
    private static string Describe(string baselineDir, string name, RenderedFrame frame, int bw, int bh,
                                   out byte[]? basePx)
    {
        basePx = null;
        string png = PngPath(baselineDir, name);
        if (!DecodeStraight(png, out int w, out int h, out var baseStraight))
            return $"hash differs; baseline PNG {png} unreadable";
        if (w != frame.Width || h != frame.Height)
            return $"size changed: baseline {bw}x{bh}, now {frame.Width}x{frame.Height}";
        basePx = baseStraight;
        var now = Straight(frame);
        int pixels = 0, maxDelta = 0;
        for (int i = 0; i < now.Length; i += 4)
        {
            bool diff = false;
            for (int c = 0; c < 4; c++) if (now[i + c] != baseStraight[i + c]) { diff = true; break; }
            if (!diff) continue;
            pixels++;
            int an = now[i + 3], ab = baseStraight[i + 3];
            maxDelta = Math.Max(maxDelta, Math.Abs(an - ab));
            for (int c = 0; c < 3; c++)
                maxDelta = Math.Max(maxDelta, Math.Abs(Premul(now[i + c], an) - Premul(baseStraight[i + c], ab)));
        }
        return pixels == 0
            ? "hash differs but no pixel does (the buffer changed below the PNG's precision)"
            : $"{pixels} px differ ({100.0 * pixels / (frame.Width * frame.Height):F3}%), max channel delta {maxDelta}";
    }

    private static int Premul(byte c, int a) => (int)Math.Round(c * a / 255.0);

    private static void WriteArtifacts(string outDir, string name, RenderedFrame frame, byte[]? baseStraight)
    {
        string actual = Path.Combine(outDir, name.Replace('/', Path.DirectorySeparatorChar) + ".actual.png");
        Directory.CreateDirectory(Path.GetDirectoryName(actual)!);
        File.WriteAllBytes(actual, EncodePng(frame));
        if (baseStraight is null) return;
        // The actual frame dimmed to a grey ghost on black, differing pixels in solid red.
        var now = Straight(frame);
        var diff = new byte[now.Length];
        for (int i = 0; i < now.Length; i += 4)
        {
            bool d = false;
            for (int c = 0; c < 4; c++) if (now[i + c] != baseStraight[i + c]) { d = true; break; }
            if (d) { diff[i] = 0; diff[i + 1] = 0; diff[i + 2] = 255; }
            else
            {
                byte g = (byte)((now[i] + now[i + 1] + now[i + 2]) / 3 * now[i + 3] / 255 * 35 / 100);
                diff[i] = diff[i + 1] = diff[i + 2] = g;
            }
            diff[i + 3] = 255;
        }
        var bmp = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, diff, frame.Width * 4);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(Path.Combine(outDir, name.Replace('/', Path.DirectorySeparatorChar) + ".diff.png"));
        enc.Save(fs);
    }

    // ── Shared plumbing ───────────────────────────────────────────────────────────────────────────────

    private static RenderedFrame? RenderEntrySafely(RenderEntry entry)
    {
        try
        {
            PinClocks();
            var frame = entry.Render();
            Drain();
            return frame;
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
            H.Fail(entry.Name, $"render threw {inner.GetType().Name}: {inner.Message}");
            try { Drain(); } catch { }
            return null;
        }
    }

    /// <summary>The WPF environment the app's drawing code expects: an Application (pack:// is registered by
    /// it) whose resource assembly is Radiata.dll, so packed art and fonts resolve. Then the seams: the
    /// wheel's and the arcade's clocks, and the particle roll.</summary>
    internal static bool Prepare()
    {
        // With no display device — a disconnected Remote Desktop session is one — WPF renders NOTHING and every
        // RenderTargetBitmap comes back transparent. This switch makes it rasterise anyway. It is read when
        // WPF first builds its media context, so it has to be set before anything here draws.
        AppContext.SetSwitch("Switch.System.Windows.Media.ShouldRenderEvenWhenNoDisplayDevicesAreAvailable", true);
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var probe = new DrawingVisual();
        using (var dc = probe.RenderOpen()) dc.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 4, 4));
        if (Capture(probe, 4, 4, 1).Pixels.All(b => b == 0))
        {
            H.Fail("render: WPF rasterises offscreen in this session", "a red test square rendered transparent");
            return false;
        }
        try { Application.ResourceAssembly = H.App; }
        catch (InvalidOperationException)
        {
            typeof(Application).GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, H.App);
        }
        if (!ReferenceEquals(Application.ResourceAssembly, H.App))
        {
            H.Fail("render: Application.ResourceAssembly points at Radiata.dll", "pack:// art would not resolve");
            return false;
        }
        MotionPolicy.UserSetting = false;
        MotionPolicy.SystemPrefersReduced = false;
        foreach (var (type, field) in new[]
                 { (typeof(RadialMenuControl), "PinnedClockUtc"), (typeof(RadialMenuControl), "FxRng"),
                   (H.AppType("ArcadeSprites"), "PinnedTime") })
            if (H.StaticField(type, field) is null)
            {
                H.Fail($"render: determinism seam {type.Name}.{field} exists", "MISSING — renamed or removed");
                return false;
            }
        PinClocks();
        return T_RenderWheel.Init();
    }

    /// <summary>Pin a parentless visual to 96 DPI, so what it reads through <c>VisualTreeHelper.GetDpi</c> — the
    /// wheel's bake and shadow supersampling, every pixels-per-dip handed to FormattedText — is the same whatever
    /// scale the session's displays run at.</summary>
    public static T Unscaled<T>(T v) where T : Visual
    {
        VisualTreeHelper.SetRootDpi(v, new DpiScale(1, 1));
        return v;
    }

    /// <summary>Every entry starts from the same instant and the same roll.</summary>
    public static void PinClocks()
    {
        H.StaticField(typeof(RadialMenuControl), "PinnedClockUtc")!.SetValue(null, (DateTime?)Epoch);
        H.StaticField(typeof(RadialMenuControl), "FxRng")!.SetValue(null, new Random(FxSeed));
        H.StaticField(H.AppType("ArcadeSprites"), "PinnedTime")!.SetValue(null, (double?)SpriteEpochSeconds);
    }

    public static void AdvanceClock(double ms)
    {
        var f = H.StaticField(typeof(RadialMenuControl), "PinnedClockUtc")!;
        f.SetValue(null, (DateTime?)(((DateTime?)f.GetValue(null) ?? Epoch).AddMilliseconds(ms)));
    }

    private static void Finish()
    {
        // Put the real clocks back, so nothing that runs after this group inherits a frozen one.
        H.StaticField(typeof(RadialMenuControl), "PinnedClockUtc")!.SetValue(null, null);
        H.StaticField(H.AppType("ArcadeSprites"), "PinnedTime")!.SetValue(null, null);
    }

    /// <summary>Run whatever the entry queued on the dispatcher (the wheel's pre-warm and bake follow-ups),
    /// AFTER its frame is captured, so nothing queued piles up — and holds its bakes alive — across hundreds
    /// of entries.</summary>
    public static void Drain() =>
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

    /// <summary>Rasterise a visual onto a transparent canvas of <paramref name="w"/>×<paramref name="h"/>
    /// DIPs at <paramref name="scale"/> device pixels per DIP.</summary>
    public static RenderedFrame Capture(Visual v, double w, double h, double scale)
    {
        int pw = (int)Math.Round(w * scale), ph = (int)Math.Round(h * scale);
        var rtb = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(v);
        return FromBitmap(rtb);
    }

    /// <summary>Any bitmap as a Pbgra32 frame, pixel for pixel.</summary>
    public static RenderedFrame FromBitmap(BitmapSource bmp)
    {
        BitmapSource src = bmp.Format == PixelFormats.Pbgra32 ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Pbgra32, null, 0);
        int stride = src.PixelWidth * 4;
        var px = new byte[stride * src.PixelHeight];
        src.CopyPixels(px, stride, 0);
        return new RenderedFrame(src.PixelWidth, src.PixelHeight, px);
    }

    private static string Sha(byte[] px) => Convert.ToHexString(SHA256.HashData(px)).ToLowerInvariant();

    internal static string PngPath(string dir, string name) =>
        Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar) + ".png");

    private static BitmapSource AsBitmap(RenderedFrame f) =>
        BitmapSource.Create(f.Width, f.Height, 96, 96, PixelFormats.Pbgra32, null, f.Pixels, f.Width * 4);

    internal static byte[] EncodePng(RenderedFrame f)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(AsBitmap(f)));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>The frame un-premultiplied to BGRA — the same conversion the PNG encoder applies.</summary>
    private static byte[] Straight(RenderedFrame f)
    {
        var conv = new FormatConvertedBitmap(AsBitmap(f), PixelFormats.Bgra32, null, 0);
        var px = new byte[f.Width * f.Height * 4];
        conv.CopyPixels(px, f.Width * 4, 0);
        return px;
    }

    private static bool DecodeStraight(string png, out int w, out int h, out byte[] px)
    {
        w = h = 0; px = [];
        if (!File.Exists(png)) return false;
        try
        {
            using var fs = File.OpenRead(png);
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource f = dec.Frames[0];
            if (f.Format != PixelFormats.Bgra32) f = new FormatConvertedBitmap(f, PixelFormats.Bgra32, null, 0);
            w = f.PixelWidth; h = f.PixelHeight;
            px = new byte[w * h * 4];
            f.CopyPixels(px, w * 4, 0);
            return true;
        }
        catch { return false; }
    }

    // ── Reflection helpers the matrix files share ────────────────────────────────────────────────────

    private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>An instance member by name, or a MissingMemberException naming it — a rename must fail the
    /// entry loudly, never quietly render something else.</summary>
    public static MethodInfo Method(Type t, string name) =>
        t.GetMethod(name, Inst) ?? throw new MissingMethodException(t.Name, name);

    public static object? Call(object target, string name, params object?[] args) =>
        Method(target.GetType(), name).Invoke(target, args);

    public static FieldInfo Field(Type t, string name) =>
        t.GetField(name, Inst) ?? throw new MissingFieldException(t.Name, name);

    public static object? Get(object target, string name) => Field(target.GetType(), name).GetValue(target);

    public static void Set(object target, string name, object? value) => Field(target.GetType(), name).SetValue(target, value);

    public static string RepoRoot() =>
        H.RepoRoot() ?? throw new DirectoryNotFoundException("repo root (ControllerWheel.csproj) not found above the harness");
}
