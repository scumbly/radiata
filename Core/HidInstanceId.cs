using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ControllerWheel;

/// <summary>Extracts the USB vendor/product id pair from a PnP HID instance id, across the two
/// shapes Windows produces for the same physical controller:
///
///   USB     hid\vid_054c&amp;pid_0df2&amp;mi_03\8&amp;198b44e5&amp;0&amp;0000
///   BT      hid\{00001124-0000-1000-8000-00805f9b34fb}_vid&amp;0002054c_pid&amp;0df2\9&amp;...
///
/// In the Bluetooth shape the vid token is 8 hex digits — a 4-digit "vendor id source" prefix
/// (0001 = Bluetooth SIG, 0002 = USB-IF) ahead of the 4-digit vid proper — so only the LAST four
/// digits are the vid. Matching a pad across transports (its USB devnode vs its BT devnode) must
/// go through this parser; comparing raw instance-id substrings silently never matches.</summary>
public static partial class HidInstanceId
{
    // USB shape: vid_XXXX ... pid_XXXX (separators vary; don't assume '&').
    [GeneratedRegex(@"vid_([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex UsbVid();
    [GeneratedRegex(@"pid_([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex UsbPid();

    // BT shape: vid&SSSSXXXX / pid&XXXX (SSSS = vendor-id-source prefix, dropped).
    [GeneratedRegex(@"vid&[0-9a-f]{4}([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex BtVid();
    [GeneratedRegex(@"pid&([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex BtPid();

    /// <summary>Vid/pid from either instance-id shape. False (vid = pid = 0) when the string has
    /// neither — callers must treat that as "unknown device", never as a match.</summary>
    public static bool TryGetVidPid(string? instanceId, out int vid, out int pid)
    {
        vid = 0; pid = 0;
        if (string.IsNullOrEmpty(instanceId)) return false;

        var v = UsbVid().Match(instanceId);
        var p = UsbPid().Match(instanceId);
        if (!v.Success || !p.Success)
        {
            v = BtVid().Match(instanceId);
            p = BtPid().Match(instanceId);
        }
        if (!v.Success || !p.Success) return false;

        vid = int.Parse(v.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        pid = int.Parse(p.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>The device instance id inside a device-interface symbolic link:
    /// <c>\\?\HID#VID_054C&amp;PID_0CE6&amp;MI_03#8&amp;2a&amp;0&amp;0003#{4d1e55b2-…}</c> →
    /// <c>HID\VID_054C&amp;PID_0CE6&amp;MI_03\8&amp;2a&amp;0&amp;0003</c>. Null when the string lacks the
    /// enumerator#device#instance shape. A link's letter case can differ from the instance id's, so
    /// compare the result case-insensitively. The one link-to-instance-id converter: the reader's cloak
    /// ids, the ViGEm skip, the elevated restart sweep and the removal sentry must all name a devnode
    /// identically.</summary>
    /// <param name="requireFullLink">Accept only a complete Win32 link as SetupAPI returns it: the
    /// <c>\\?\</c> prefix and the trailing interface-class segment. A caller that hands the id to a
    /// device-changing command sets it, so a bare or truncated string never names a devnode.</param>
    public static string? FromInterfacePath(string? path, bool requireFullLink = false)
    {
        if (string.IsNullOrEmpty(path)) return null;
        bool win32 = path.StartsWith(@"\\?\", StringComparison.Ordinal);
        if (requireFullLink && !win32) return null;
        var s = win32 || path.StartsWith(@"\\.\", StringComparison.Ordinal) ? path[4..] : path;
        var parts = s.Split('#');
        return parts.Length < (requireFullLink ? 4 : 3) || parts[0].Length == 0
            ? null : string.Join("\\", parts[0], parts[1], parts[2]);
    }

    /// <summary>True when both ids parse and name the same vendor/product — i.e. the same pad
    /// model, possibly on different transports. Unparseable ids never match anything.</summary>
    public static bool SameModel(string? a, string? b) =>
        TryGetVidPid(a, out int av, out int ap) &&
        TryGetVidPid(b, out int bv, out int bp) &&
        av == bv && ap == bp;
}
