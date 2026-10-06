using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerWheel;

/// <summary>
/// Extracts and caches icons from executable paths.
/// Call <see cref="Clear"/> when config hot-reloads so stale paths are evicted.
/// </summary>
public static class IconCache
{
    private static readonly Dictionary<string, BitmapSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Get(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;

        if (_cache.TryGetValue(exePath, out var cached)) return cached;

        // shell:AppsFolder\<AUMID> launch slices (the Installed Apps picker): no exe on disk to
        // extract from — ask the shell item itself for its icon instead.
        if (exePath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            var shellIcon = ExtractShellIcon(exePath);
            _cache[exePath] = shellIcon;
            return shellIcon;
        }

        BitmapSource? result = null;
        try
        {
            // SHDefExtractIcon serves the exe's LARGEST icon frame (modern apps ship 256px PNG frames)
            // as an HICON — the HICON route keeps the alpha channel intact, unlike
            // CreateBitmapSourceFromHBitmap. ExtractAssociatedIcon only ever yields the 32px frame.
            IntPtr hLarge = IntPtr.Zero, hSmall = IntPtr.Zero;
            try
            {
                if (SHDefExtractIcon(exePath, 0, 0, out hLarge, out hSmall, JumboSize) == 0 && hLarge != IntPtr.Zero)
                {
                    result = Imaging.CreateBitmapSourceFromHIcon(
                        hLarge, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    result.Freeze();
                }
            }
            finally
            {
                if (hLarge != IntPtr.Zero) DestroyIcon(hLarge);
                if (hSmall != IntPtr.Zero) DestroyIcon(hSmall);
            }

            if (result is null)   // shell refused (odd formats) → the old 32px associated-icon fallback
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    result = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    result.Freeze();
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Icon] Failed to extract from {exePath}: {ex.Message}");
        }

        _cache[exePath] = result;
        return result;
    }

    // ── Shell-item icons (UWP/Store apps via shell:AppsFolder — Kando-inspired, MIT, see THIRD-PARTY-LICENSES.md §5) ──
    // SHCreateItemFromParsingName resolves the virtual path to a shell item; IShellItemImageFactory
    // serves its icon as a 32bpp HBITMAP. CreateBitmapSourceFromHBitmap drops the alpha channel, so
    // the pixels are read out with GetDIBits and rebuilt as a Bgra32 BitmapSource (same trick Kando's
    // native addon uses before PNG-encoding). Stateless + frozen result → safe to call off the UI
    // thread (the app picker loads its list icons on a background thread through this).

    /// <summary>Icon for a shell item path (e.g. <c>shell:AppsFolder\&lt;AUMID&gt;</c>), alpha intact,
    /// frozen. Null on any failure — never throws.</summary>
    public static BitmapSource? ExtractShellIcon(string shellPath, int size = 128)
    {
        IntPtr hBmp = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(shellPath, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null)
                return null;
            try
            {
                if (factory.GetImage(new SIZE { cx = size, cy = size },
                                     SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hBmp) != 0 || hBmp == IntPtr.Zero)
                    return null;
                return BitmapFromHBitmapWithAlpha(hBmp);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(factory);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Icon] Shell icon failed for {shellPath}: {ex.Message}");
            return null;
        }
        finally
        {
            if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
        }
    }

    private static BitmapSource? BitmapFromHBitmapWithAlpha(IntPtr hBmp)
    {
        if (GetObject(hBmp, System.Runtime.InteropServices.Marshal.SizeOf<BITMAP>(), out var bmp) == 0 ||
            bmp.bmWidth <= 0 || bmp.bmHeight <= 0)
            return null;

        var bi = new BITMAPINFOHEADER
        {
            biSize     = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth    = bmp.bmWidth,
            biHeight   = -bmp.bmHeight,   // negative = top-down
            biPlanes   = 1,
            biBitCount = 32,
        };

        var pixels = new byte[bmp.bmWidth * bmp.bmHeight * 4];
        var hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return null;
        try
        {
            if (GetDIBits(hdc, hBmp, 0, (uint)bmp.bmHeight, pixels, ref bi, 0 /* DIB_RGB_COLORS */) == 0)
                return null;
        }
        finally { ReleaseDC(IntPtr.Zero, hdc); }

        var src = BitmapSource.Create(bmp.bmWidth, bmp.bmHeight, 96, 96,
                                      PixelFormats.Bgra32, null, pixels, bmp.bmWidth * 4);
        src.Freeze();
        return src;
    }

    private const uint SIIGBF_BIGGERSIZEOK = 0x1;
    private const uint SIIGBF_ICONONLY    = 0x4;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [System.Runtime.InteropServices.ComImport,
     System.Runtime.InteropServices.Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [System.Runtime.InteropServices.PreserveSig]
        int GetImage(SIZE size, uint flags, out IntPtr phbm);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid,
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hBmp, uint start, uint lines,
        byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    // LOWORD = requested large size, HIWORD = small size (unused — 16 keeps the call happy).
    private const uint JumboSize = 256 | (16u << 16);

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string iconFile, int iconIndex, uint flags,
                                               out IntPtr hIconLarge, out IntPtr hIconSmall, uint iconSize);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static void Clear() => _cache.Clear();
}
