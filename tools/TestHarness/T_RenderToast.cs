using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>The toast half of the render matrix (<see cref="T_Render"/>) — the PIECES only.
///
/// <para>A whole toast cannot be rendered offscreen: <c>ToastPresenter.BuildAndShowToast</c> and
/// <c>ToastPresenter.ShowRectToast</c> compose the card inline and <c>Show()</c> it as a top-level topmost Window
/// in the same method, read the live App's config and onboarding state through the presenter's delegates, and
/// animate Kawaii's stars on WPF clocks. What CAN be rendered without a window or a constructed App is what
/// those methods are built from: <c>ToastPresenter.ToastBrushes</c> per material (static — it reads no
/// presenter state), <c>ToastPresenter.BuildToastRim</c>, and the ring and star geometry
/// <c>RadialMenuControl</c> lends the toasts. The composition itself stays uncovered.</para></summary>
internal static class T_RenderToast
{
    private const double Size = RadialMenuControl.InnerRadius * 2;

    public static IEnumerable<RenderEntry> Entries()
    {
        var presenterType = H.AppType("ToastPresenter");
        foreach (var mat in Materials.All)
            yield return new($"toast/brushes/{mat}", () => Brushes(presenterType, mat));
        yield return new("toast/rim", Rim);
        yield return new("toast/ring/mesa", () => Ring(Geo("BuildHubToastRing", C, Size / 2 - 2.2, 2.2, 8.0, false),
                                                      (Brush)Static("TerraShadow")!, null));
        yield return new("toast/ring/salvage", () => Ring(Geo("BuildHubToastRing", C, Size / 2 - 1.1, 1.1, 3.5, true),
                                                         (Brush)Static("SalvageFill")!,
                                                         (Brush)H.InvokeStatic(typeof(RadialMenuControl), "SalvageBoxTexture", Size)!,
                                                         (Pen)Static("SalvageHubLipPen")!));
        yield return new("toast/ring/kawaii-cloud", () => Ring(Geo("BuildCloudToastRing", C, Size / 2 - 2.0),
                                                              new SolidColorBrush(Color.FromRgb(0xFD, 0xF6, 0xFA)), null));
        yield return new("toast/stars", Stars);
    }

    private static readonly Point C = new(Size / 2, Size / 2);

    private static object? Static(string name) =>
        H.StaticField(typeof(RadialMenuControl), name)?.GetValue(null)
        ?? typeof(RadialMenuControl).GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(null)
        ?? throw new MissingMemberException(nameof(RadialMenuControl), name);

    private static Geometry Geo(string method, params object[] args) =>
        (Geometry)H.InvokeStatic(typeof(RadialMenuControl), method, args)!;

    /// <summary>The fill, rim and ink <c>ToastBrushes</c> answers for a material, as a disc and an ink chip.</summary>
    private static RenderedFrame Brushes(Type presenterType, string mat)
    {
        var t = ((Brush fill, Brush ink, Brush edge, double edgeW, bool drawRim))
                H.InvokeStatic(presenterType, "ToastBrushes", mat)!;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var pen = t.edgeW > 0 ? new Pen(t.edge, t.edgeW) : null;
            dc.DrawEllipse(t.fill, pen, C, Size / 2 - t.edgeW / 2, Size / 2 - t.edgeW / 2);
            dc.DrawRectangle(t.ink, null, new Rect(Size + 12, 12, 40, 40));
            if (t.drawRim) dc.DrawRectangle(System.Windows.Media.Brushes.Black, null, new Rect(Size + 12, 60, 40, 8));
        }
        return T_Render.Capture(dv, Size + 64, Size, T_RenderWheel.DpiScale);
    }

    private static RenderedFrame Rim()
    {
        var rim = T_Render.Unscaled((FrameworkElement)H.InvokeStatic(H.AppType("ToastPresenter"), "BuildToastRim", Size)!);
        rim.Measure(new System.Windows.Size(Size, Size));
        rim.Arrange(new Rect(0, 0, Size, Size));
        rim.UpdateLayout();
        return T_Render.Capture(rim, Size, Size, T_RenderWheel.DpiScale);
    }

    private static RenderedFrame Ring(Geometry ring, Brush fill, Brush? texture, Pen? lip = null)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawGeometry(fill, null, ring);
            if (texture is not null) dc.DrawGeometry(texture, null, ring);
            if (lip is not null) { dc.PushClip(ring); dc.DrawGeometry(null, lip, ring); dc.Pop(); }
        }
        return T_Render.Capture(dv, Size, Size, T_RenderWheel.DpiScale);
    }

    private static RenderedFrame Stars()
    {
        var ink = (Brush)Static("KawaiiTwinkleInk")!;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            for (int k = 0; k < 6; k++)
            {
                var at = new Point(20 + k * 24, 20 + (k % 2) * 16);
                dc.DrawGeometry(ink, new Pen(System.Windows.Media.Brushes.Plum, 2) { LineJoin = PenLineJoin.Round },
                                Geo("KawaiiStarGeometry", at, 4.5 + k * 1.4, k * 17.0));
            }
        return T_Render.Capture(dv, 170, 50, T_RenderWheel.DpiScale);
    }
}
