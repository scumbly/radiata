using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>HidInstanceId's vid/pid extraction across the USB and Bluetooth instance-id shapes.
/// The orphan-adoption pass in HidHideManager.Hide keys on SameModel, and the two shapes never
/// match as raw substrings — a parser regression silently re-opens the orphaned-cloak hole.
/// Pure string work, no driver, safe in the default set.</summary>
internal static class T_HidInstanceId
{
    // Real ids from the reference DualSense Edge (0x054C:0x0DF2) — the same pad on its two transports.
    private const string EdgeUsb = @"hid\vid_054c&pid_0df2&mi_03\8&198b44e5&0&0000";
    private const string EdgeBt  = @"hid\{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0df2\9&11081464&0&0000";

    public static void Run()
    {
        H.Group("HID instance-id vid/pid parse (Core)");

        H.Check("USB shape parses", HidInstanceId.TryGetVidPid(EdgeUsb, out int uv, out int up));
        H.Check("USB vid", uv == 0x054C, $"got 0x{uv:X4}");
        H.Check("USB pid", up == 0x0DF2, $"got 0x{up:X4}");

        H.Check("BT shape parses", HidInstanceId.TryGetVidPid(EdgeBt, out int bv, out int bp));
        H.Check("BT vid drops the 4-digit source prefix", bv == 0x054C, $"got 0x{bv:X4}");
        H.Check("BT pid", bp == 0x0DF2, $"got 0x{bp:X4}");

        H.Check("uppercase id parses",
                HidInstanceId.TryGetVidPid(EdgeUsb.ToUpperInvariant(), out int cv, out _) && cv == 0x054C);

        // The whole point: the same pad's USB and BT devnodes must match through the parser.
        H.Check("same pad matches across transports", HidInstanceId.SameModel(EdgeUsb, EdgeBt));
        H.Check("different pid does not match",
                !HidInstanceId.SameModel(EdgeUsb, @"hid\vid_054c&pid_0ce6\8&aaaa&0&0000"));
        H.Check("different vid does not match",
                !HidInstanceId.SameModel(EdgeUsb, @"hid\vid_045e&pid_0df2\8&aaaa&0&0000"));

        // Unknowns must fail closed — an unparseable id may never match anything.
        H.Check("null rejects", !HidInstanceId.TryGetVidPid(null, out _, out _));
        H.Check("empty rejects", !HidInstanceId.TryGetVidPid("", out _, out _));
        H.Check("no-vid string rejects", !HidInstanceId.TryGetVidPid(@"hid\some_device\1&2&3", out _, out _));
        H.Check("unparseable never matches unparseable", !HidInstanceId.SameModel("junk", "junk"));
        H.Check("unparseable never matches a real id", !HidInstanceId.SameModel("junk", EdgeUsb));

        // A vid token that only fits the BT shape must not be misread by the USB regex (and vice
        // versa): "vid&0002054c" contains no "vid_"; "vid_054c" contains no 8-digit run.
        H.Check("BT-only id ignored by USB path",
                HidInstanceId.TryGetVidPid(EdgeBt, out int xv, out _) && xv == 0x054C);

        // BT pid regex must not swallow the vid token's hex run.
        H.Check("BT pid not misread from vid token", bp == 0x0DF2);

        H.Group("HID interface link to instance id — the shapes FromInterfacePath's callers depend on (Core)");
        H.Try("interface link edges", InterfaceLinkEdges);
    }

    /// <summary>HidSharp's DevicePath is what the reader's cloak ids, the ViGEm skip and the elevated
    /// restart sweep convert; the sweep alone passes requireFullLink, and its ids must still equal the
    /// reader's for the same devnode.</summary>
    private static void InterfaceLinkEdges()
    {
        const string HidClass = "{4d1e55b2-f16f-11cf-88cb-001111000030}";
        string usbLink = @"\\?\hid#vid_054c&pid_0df2&mi_03#8&198b44e5&0&0000#" + HidClass;
        string btLink  = @"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0df2#9&11081464&0&0000#" + HidClass;

        H.Check("HidSharp USB link yields the devnode id, case kept", HidInstanceId.FromInterfacePath(usbLink) == EdgeUsb);
        H.Check("HidSharp BT link yields the BT devnode id", HidInstanceId.FromInterfacePath(btLink) == EdgeBt);
        H.Check("full-link mode names the same devnode as default mode",
            HidInstanceId.FromInterfacePath(usbLink, requireFullLink: true) == EdgeUsb
            && HidInstanceId.FromInterfacePath(btLink, requireFullLink: true) == EdgeBt);
        H.Check("reference-string suffix does not change the id",
            HidInstanceId.FromInterfacePath(usbLink + @"\kbd") == EdgeUsb
            && HidInstanceId.FromInterfacePath(usbLink + @"\kbd", requireFullLink: true) == EdgeUsb);

        string dotLink = @"\\.\" + usbLink[4..];
        string bare    = usbLink[4..];
        string noClass = usbLink[..usbLink.LastIndexOf('#')];
        H.Check(@"\\.\ prefix: default mode accepts", HidInstanceId.FromInterfacePath(dotLink) == EdgeUsb);
        H.Check("bare link: default mode accepts", HidInstanceId.FromInterfacePath(bare) == EdgeUsb);
        H.Check("no interface-class segment: default mode accepts", HidInstanceId.FromInterfacePath(noClass) == EdgeUsb);
        H.Check(@"full-link mode refuses \\.\, bare and class-less links",
            HidInstanceId.FromInterfacePath(dotLink, requireFullLink: true) is null
            && HidInstanceId.FromInterfacePath(bare, requireFullLink: true) is null
            && HidInstanceId.FromInterfacePath(noClass, requireFullLink: true) is null);
        H.Check("empty enumerator is null in both modes",
            HidInstanceId.FromInterfacePath(@"\\?\#a#b#" + HidClass) is null
            && HidInstanceId.FromInterfacePath(@"\\?\#a#b#" + HidClass, requireFullLink: true) is null);
        H.Check("null / empty is null in full-link mode",
            HidInstanceId.FromInterfacePath(null, requireFullLink: true) is null
            && HidInstanceId.FromInterfacePath("", requireFullLink: true) is null);
    }
}
