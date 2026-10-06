using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControllerWheel.ArcadeHost;

/// <summary>The <c>--xinput-count</c> probe mode: report how many XInput pads THIS process can see.
///
/// Why it lives in the helper: Radiata gates the Bluetooth Xbox cloak on an OBSERVED isolation check —
/// a cloak that trusted HidHide's own success return would ship double input to every Bluetooth user
/// the day a Windows or HidHide change broke the behaviour. Radiata.exe is HidHide-allow-listed, so its
/// own XInput view is NOT what a game sees; this exe is a different image and is not allow-listed, so
/// its view is exactly the consumer's. No jail, no pipe, no script engine — four XInputGetState polls.
///
/// Exit code: 10 + the maximum simultaneous pad count seen (10–14). Anything else is an error — the
/// 10-offset keeps counts unambiguous next to the pipe mode's 1/2 failure codes.</summary>
internal static class XInputCount
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint index, out XINPUT_STATE state);

    public static int Run()
    {
        int max = 0;
        var sw = Stopwatch.StartNew();
        // Several samples over ~600ms: XInput's device list can lag a PnP change by a beat, and a
        // single instant of 0 must not be mistaken for a proven cloak.
        while (sw.ElapsedMilliseconds < 600)
        {
            int n = 0;
            for (uint i = 0; i < 4; i++)
            {
                try { if (XInputGetState(i, out _) == 0) n++; }
                catch { return 1; }   // xinput1_4.dll missing/unloadable — an error, not a count
            }
            if (n > max) max = n;
            Thread.Sleep(50);
        }
        return 10 + max;
    }
}
