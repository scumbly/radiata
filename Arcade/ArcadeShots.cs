using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

internal enum ShotKind { Live, Bundled, None }

/// <summary>What the picker shows on a cabinet's screen for one game. <see cref="ShotKind.Live"/> is a
/// capture of the player's own last board, <see cref="ShotKind.Bundled"/> the shipped still, and
/// <see cref="ShotKind.None"/> means the picker draws its placeholder plate instead.</summary>
internal sealed record ArcadeShot(BitmapSource? Image, ShotKind Kind)
{
    public static readonly ArcadeShot Empty = new(null, ShotKind.None);
}

/// <summary>Screenshots of frozen games for the picker: captured when a game is dismissed or swapped,
/// kept in memory for this session and written to <c>%APPDATA%\Radiata\arcade-shots\&lt;id&gt;.png</c> for
/// the next one. Falls back to the game's seeded still — a drop-in package's own <c>preview</c> file, or the
/// bundled <c>Assets/arcade/&lt;id&gt;-preview.png</c> for a built-in — then to nothing.
///
/// <para>Every disk path is hostile-tolerant and every failure is swallowed and traced: a corrupt or
/// missing shot must cost the picker a picture, never a frame. Decoding happens at open (<see cref="Get"/>
/// / <see cref="Preload"/>), never on the render pump.</para></summary>
internal static class ArcadeShots
{
    private const int MaxCached = 16;

    /// <summary>Shot id for the launcher itself, so an Arcade Launcher slice's hub shows the carousel as it
    /// was last left. Cannot collide with a game: no built-in is called this, and every drop-in id carries
    /// <see cref="ScriptGameManifest.TokenPrefix"/>.</summary>
    public const string LauncherId = "launcher";

    private static readonly Dictionary<string, ArcadeShot> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Order = [];
    private static Task _pending = Task.CompletedTask;

    /// <summary>The shot for a game, resolving disk → bundled → none on first ask.</summary>
    public static ArcadeShot Get(string gameId)
    {
        if (Cache.TryGetValue(gameId, out var hit)) return hit;
        var shot = Resolve(gameId);
        Put(gameId, shot);
        return shot;
    }

    public static void Preload(IEnumerable<string> gameIds)
    {
        foreach (var id in gameIds) Get(id);
    }

    /// <summary>Render the live game into a square, field-only, disc-clipped bitmap. Visible to the picker
    /// immediately; the PNG is encoded off-thread. A game that has nothing drawable yet (a script helper
    /// still starting) keeps whatever shot it had.</summary>
    public static void Capture(string gameId, IArcadeRenderer renderer, IArcadeGame game)
    {
        if (renderer is ScriptGameRenderer scriptRenderer && game is ScriptArcadeGame script)
        {
            // Replays the last validated buffer. Pumping the session here would tick the helper from
            // outside the frame pump.
            if (ScriptSessionCoordinator.LastBuffer(script).Count == 0) return;
            Capture(gameId, (dc, c, field) => scriptRenderer.DrawLast(dc, c, field, script, 1.0));
            return;
        }
        Capture(gameId, (dc, c, field) => renderer.Draw(dc, c, field, game, 1.0));
    }

    /// <summary>Render any field-sized surface — a game, or the launcher's carousel — into the shot for
    /// <paramref name="id"/>. <paramref name="draw"/> gets the centre and the field radius, already clipped
    /// to the disc and on the arcade's field colour.</summary>
    public static void Capture(string id, Action<DrawingContext, Point, double> draw)
    {
        int px = Math.Clamp(ArcadePickerTuning.ShotPx, 64, 2048);
        var c = new Point(px / 2.0, px / 2.0);
        double field = px / 2.0;

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawEllipse(ArcadeChrome.Field, null, c, field, field);
            var clip = new EllipseGeometry(c, field, field);
            clip.Freeze();
            dc.PushClip(clip);
            draw(dc, c, field);
            dc.Pop();
        }

        var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        Put(id, new ArcadeShot(rtb, ShotKind.Live));

        // The encoder gets a plain pixel copy, not the render target: a RenderTargetBitmap is tied to the
        // rendering thread even once frozen, and 1 MB copied here is cheaper than finding that out later.
        int stride = px * 4;
        var pixels = new byte[stride * px];
        rtb.CopyPixels(pixels, stride, 0);
        var plain = BitmapSource.Create(px, px, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        plain.Freeze();

        string path = ArcadeStore.ShotPath(id);
        _pending = _pending.ContinueWith(_ => Encode(plain, path), TaskScheduler.Default);
    }

    /// <summary>Bounded wait for in-flight encodes, for app teardown. A timeout leaves the previous file in
    /// place — the write is atomic.</summary>
    public static void WaitPending(int maxMs)
    {
        try { _pending.Wait(maxMs); } catch { /* faulted encodes already traced themselves */ }
    }

    private static void Put(string gameId, ArcadeShot shot)
    {
        if (!Cache.ContainsKey(gameId))
        {
            Order.Add(gameId);
            while (Order.Count > MaxCached)
            {
                Cache.Remove(Order[0]);
                Order.RemoveAt(0);
            }
        }
        Cache[gameId] = shot;
    }

    private static ArcadeShot Resolve(string gameId)
    {
        try
        {
            string path = ArcadeStore.ShotPath(gameId);
            if (File.Exists(path))
            {
                var live = ArcadeArt.LoadFile(path, ArcadePickerTuning.ShotPx * 2, $"arcade shot ({gameId})");
                if (live is not null) return new ArcadeShot(live, ShotKind.Live);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] shot lookup failed for {gameId}: {ex.Message}");
        }

        // A package's preview is consented content decoded the same capped way as a live shot (its bytes are in
        // the consent hash, and the folder caps bound the file).
        if (ArcadeCatalog.Find(gameId)?.PreviewPath is { } seeded)
        {
            var own = ArcadeArt.LoadFile(seeded, ArcadePickerTuning.ShotPx * 2, $"package preview ({gameId})");
            if (own is not null) return new ArcadeShot(own, ShotKind.Bundled);
        }

        var still = ArcadeArt.LoadPacked($"Assets/arcade/{gameId}-preview.png", $"arcade preview ({gameId})");
        return still is null ? ArcadeShot.Empty : new ArcadeShot(still, ShotKind.Bundled);
    }

    private static void Encode(BitmapSource bmp, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var mem = new MemoryStream();
            enc.Save(mem);
            AtomicFile.WriteAllBytes(path, mem.ToArray());
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Arcade] shot save failed: {ex.Message}");
        }
    }
}
