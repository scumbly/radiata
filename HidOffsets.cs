using System.IO;
using System.Text.Json;

namespace ControllerWheel;

/// <summary>
/// Discovered HID byte offsets for the connected controller.
/// Saved to AppData after the setup wizard runs; loaded on startup.
/// </summary>
public sealed class HidOffsets
{
    // Defaults = the VERIFIED DualSense Edge layout (USB-aligned index; raw BT byte = index+1):
    // the Fn buttons live in r[10] bits 4/5 — alongside PS 0x01 / touchpad-click 0x02 / mute 0x04.
    // Raw byte 11 reads 0x10 (Fn1) / 0x20 (Fn2) / 0x30 (both) in HID Diagnostics.
    // ⚠ These defaults must stand alone: on a fresh app-data (OOBE) there is no hid_offsets.json to
    // override them, so a wrong default here is simply dead Fn buttons.
    public int FnLeftByte  { get; set; } = 10;
    public int FnLeftMask  { get; set; } = 0x10;
    public int FnRightByte { get; set; } = 10;
    public int FnRightMask { get; set; } = 0x20;
    public int DPadByte    { get; set; } = 8;

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(AppPaths.AppDataDir, "hid_offsets.json");

    public static HidOffsets Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return (JsonSerializer.Deserialize<HidOffsets>(File.ReadAllText(FilePath)) ?? new()).Sanitized();
        }
        catch { }
        return new();
    }

    public HidOffsets Sanitized()
    {
        static bool Mask(int value) => value is > 0 and <= 255 && (value & (value - 1)) == 0;
        if (FnLeftByte is < 0 or >= 64 || FnRightByte is < 0 or >= 64 || DPadByte is < 0 or >= 64
            || !Mask(FnLeftMask) || !Mask(FnRightMask)) return new();
        return new HidOffsets { FnLeftByte = FnLeftByte, FnLeftMask = FnLeftMask,
            FnRightByte = FnRightByte, FnRightMask = FnRightMask, DPadByte = DPadByte };
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        // Atomic write (temp + move): an interrupted write can't truncate the calibration file.
        AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts));
    }
}
