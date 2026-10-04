using System.Windows.Media;

namespace ControllerWheel;

/// <summary>
/// Shared, persistent palette for the WinForms ColorDialog's 16 custom-colour slots. Seeded from
/// the action-type palette on first use; every picker (per-type and per-slice) reads
/// <see cref="Slots"/> into its dialog and writes additions back via <see cref="Capture"/>, and the
/// set round-trips through config (<see cref="ToHex"/> / <see cref="Load"/>) so it survives restarts.
/// </summary>
public static class CustomColorStore
{
    private const int White = 0xFFFFFF;
    private static int[] _slots = SeedFromPalette();

    /// <summary>16 COLORREF (0x00BBGGRR) slots — assign to <c>ColorDialog.CustomColors</c>.</summary>
    public static int[] Slots => (int[])_slots.Clone();

    public static void Load(IEnumerable<string>? hexes)
    {
        var list = new List<int>(16);
        if (hexes is not null)
            foreach (var h in hexes)
                if (ActionTint.TryParseHex(h, out var c) && list.Count < 16) list.Add(ToColorRef(c));
        _slots = list.Count == 0 ? SeedFromPalette() : Pad(list);
    }

    public static void Capture(int[]? dialogColors)
    {
        if (dialogColors is null) return;
        _slots = Pad([.. dialogColors.Take(16)]);
    }

    /// <summary>Add a colour to the front of the palette (deduped), for the custom WPF picker.</summary>
    public static void Add(Color c)
    {
        int cr = ToColorRef(c);
        var list = _slots.Where(s => s != White && s != cr).ToList();
        list.Insert(0, cr);
        _slots = Pad(list);
    }

    /// <summary>Non-white slots as "#RRGGBB", for persistence in config.</summary>
    public static string[] ToHex() => [.. _slots.Where(c => c != White).Select(FromColorRef)];

    private static int[] SeedFromPalette()
    {
        var list = new List<int>(16);
        foreach (var (type, _, _) in ActionTint.Defaults)
        {
            int cr = ToColorRef(ActionTint.EffectiveColor(type));
            if (!list.Contains(cr)) list.Add(cr);
        }
        return Pad(list);
    }

    private static int[] Pad(List<int> list)
    {
        while (list.Count < 16) list.Add(White);
        return [.. list.GetRange(0, 16)];
    }

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
    private static string FromColorRef(int cr) =>
        $"#{(byte)(cr & 0xFF):X2}{(byte)((cr >> 8) & 0xFF):X2}{(byte)((cr >> 16) & 0xFF):X2}";
}
