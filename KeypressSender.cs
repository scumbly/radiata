using System.Runtime.InteropServices;

namespace ControllerWheel;

/// <summary>
/// Parses a key-combo string like "Win+D" or "Ctrl+Shift+Esc" and fires it via SendInput.
/// </summary>
internal static class KeypressSender
{
    // ── Key map ───────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, ushort> KeyMap =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Letters
        ["A"]=0x41,["B"]=0x42,["C"]=0x43,["D"]=0x44,["E"]=0x45,["F"]=0x46,
        ["G"]=0x47,["H"]=0x48,["I"]=0x49,["J"]=0x4A,["K"]=0x4B,["L"]=0x4C,
        ["M"]=0x4D,["N"]=0x4E,["O"]=0x4F,["P"]=0x50,["Q"]=0x51,["R"]=0x52,
        ["S"]=0x53,["T"]=0x54,["U"]=0x55,["V"]=0x56,["W"]=0x57,["X"]=0x58,
        ["Y"]=0x59,["Z"]=0x5A,
        // Digits
        ["0"]=0x30,["1"]=0x31,["2"]=0x32,["3"]=0x33,["4"]=0x34,
        ["5"]=0x35,["6"]=0x36,["7"]=0x37,["8"]=0x38,["9"]=0x39,
        // Function keys
        ["F1"]=0x70,["F2"]=0x71,["F3"]=0x72,["F4"]=0x73,["F5"]=0x74,
        ["F6"]=0x75,["F7"]=0x76,["F8"]=0x77,["F9"]=0x78,["F10"]=0x79,
        ["F11"]=0x7A,["F12"]=0x7B,
        // Navigation / editing
        ["Enter"]=0x0D,["Return"]=0x0D,
        ["Esc"]=0x1B,  ["Escape"]=0x1B,
        ["Tab"]=0x09,
        ["Del"]=0x2E,  ["Delete"]=0x2E,
        ["Backspace"]=0x08,["BS"]=0x08,
        ["Space"]=0x20,
        ["Home"]=0x24, ["End"]=0x23,
        ["PgUp"]=0x21, ["PageUp"]=0x21,
        ["PgDn"]=0x22, ["PageDown"]=0x22,["PgDown"]=0x22,
        ["Up"]=0x26,   ["Down"]=0x28,["Left"]=0x25,["Right"]=0x27,
        ["Insert"]=0x2D,
        ["Pause"]=0x13,["Break"]=0x13,
        ["PrintScreen"]=0x2C,["PrtSc"]=0x2C,
        ["`"]=0xC0, ["Grave"]=0xC0, ["Backtick"]=0xC0,   // VK_OEM_3 (Discord overlay default: Shift+`)
        ["/"]=0xBF, ["Slash"]=0xBF,   // VK_OEM_2 (US layout) — Roblox/Arma's default text-chat open key
        // Media
        ["VolUp"]=0xAF,    ["VolumeUp"]=0xAF,
        ["VolDown"]=0xAE,  ["VolumeDown"]=0xAE,
        ["Mute"]=0xAD,
        ["PlayPause"]=0xB3,
        ["Next"]=0xB0,     ["NextTrack"]=0xB0,
        ["Prev"]=0xB1,     ["PrevTrack"]=0xB1,
        ["Stop"]=0xB2,     ["StopMedia"]=0xB2,
    };

    private static readonly Dictionary<string, ushort> ModVk =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"]=0xA2,["Control"]=0xA2,
        ["Alt"]=0xA4,
        ["Shift"]=0xA0,
        ["Win"]=0x5B,["Windows"]=0x5B,
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True if <paramref name="keys"/> would be recognised by <see cref="TrySend"/> (its last
    /// token is a known key and every preceding token is a modifier). Does not send anything.</summary>
    public static bool CanParse(string? keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return false;
        var parts = keys.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !KeyMap.ContainsKey(parts[^1]) || !parts[..^1].All(ModVk.ContainsKey)) return false;
        // SendInput cannot synthesize Windows' secure attention sequence.
        return !(KeyMap[parts[^1]] == 0x2E && parts[..^1].Any(p => ModVk[p] == 0xA2)
            && parts[..^1].Any(p => ModVk[p] == 0xA4));
    }

    public static bool TrySend(string? keys)
    {
        if (!TryBuildCombo(keys, TargetLayout(), IsDown, out var inputs, out var releases)) return false;
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent == inputs.Length) return true;
        // Only release keys this call introduced. Never release a modifier already held by the user.
        releases = ReleasesForPrefix(inputs, sent);
        if (releases.Length > 0)
            NativeMethods.SendInput((uint)releases.Length, releases, Marshal.SizeOf<NativeMethods.INPUT>());
        System.Diagnostics.Trace.WriteLine($"[Keys] SendInput injected {sent}/{inputs.Length} events — incomplete");
        return false;
    }

    // Only keys whose down event entered the stream and whose up event did not need cleanup.
    private static NativeMethods.INPUT[] ReleasesForPrefix(NativeMethods.INPUT[] inputs, uint sent)
    {
        var held = new List<NativeMethods.INPUT>();
        foreach (var input in inputs.Take((int)Math.Min(sent, (uint)inputs.Length)))
        {
            var key = input.Data.Keyboard;
            int index = held.FindLastIndex(i => i.Data.Keyboard.wVk == key.wVk
                && i.Data.Keyboard.wScan == key.wScan
                && (i.Data.Keyboard.dwFlags & ~NativeMethods.KEYEVENTF_KEYUP) == (key.dwFlags & ~NativeMethods.KEYEVENTF_KEYUP));
            if ((key.dwFlags & NativeMethods.KEYEVENTF_KEYUP) != 0)
            {
                if (index >= 0) held.RemoveAt(index);
            }
            else held.Add(input);
        }
        held.Reverse();
        return held.Select(input =>
        {
            input.Data.Keyboard.dwFlags |= NativeMethods.KEYEVENTF_KEYUP;
            return input;
        }).ToArray();
    }

    private static readonly (ushort Left, ushort Right)[] ModifierPairs =
        [(0xA2, 0xA3), (0xA4, 0xA5), (0xA0, 0xA1), (0x5B, 0x5C)];

    private static bool TryBuildCombo(string? keys, IntPtr layout, Func<ushort, bool> down,
        out NativeMethods.INPUT[] inputs, out NativeMethods.INPUT[] releases)
    {
        inputs = releases = [];
        if (!CanParse(keys)) return false;
        var parts = keys!.Split('+', StringSplitOptions.TrimEntries);
        var mods = parts[..^1].Select(m => ModVk[m]).ToHashSet();
        var token = parts[^1];
        ushort main = KeyMap[token];
        // These tokens name characters, not a US OEM key position.
        char symbol = token.Equals("Slash", StringComparison.OrdinalIgnoreCase) || token == "/" ? '/'
            : token.Equals("Grave", StringComparison.OrdinalIgnoreCase) || token.Equals("Backtick", StringComparison.OrdinalIgnoreCase) || token == "`" ? '`' : '\0';
        if (symbol != '\0')
        {
            short mapped = VkKeyScanEx(symbol, layout);
            if (mapped == -1 || ((mapped >> 8) & ~7) != 0) return false;
            main = (ushort)(mapped & 255);
            if ((mapped & 0x100) != 0) mods.Add(0xA0);
            if ((mapped & 0x200) != 0) mods.Add(0xA2);
            if ((mapped & 0x400) != 0) mods.Add(0xA4);
        }
        if (down(main)) return false;
        var introduced = new List<ushort>();
        foreach (var pair in ModifierPairs)
        {
            bool held = down(pair.Left) || down(pair.Right);
            if (held && !mods.Contains(pair.Left)) return false;
            if (!held && mods.Contains(pair.Left)) introduced.Add(pair.Left);
        }
        var list = introduced.Select(vk => Key(vk, false, layout)).ToList();
        list.Add(Key(main, false, layout));
        list.Add(Key(main, true, layout));
        list.AddRange(introduced.AsEnumerable().Reverse().Select(vk => Key(vk, true, layout)));
        inputs = list.ToArray();
        releases = new[] { Key(main, true, layout) }.Concat(
            introduced.AsEnumerable().Reverse().Select(vk => Key(vk, true, layout))).ToArray();
        return true;
    }

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanEx(char character, IntPtr layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint MapVirtualKeyEx(uint key, uint mapType, IntPtr layout);
    private static bool IsDown(ushort vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    private static IntPtr TargetLayout() => GetKeyboardLayout(
        NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _));

    /// <summary>Type arbitrary Unicode text (e.g. a chat message) via SendInput's KEYEVENTF_UNICODE path —
    /// unlike <see cref="TrySend"/>'s scan-code combos, this needs no VK/keyboard-layout mapping at all,
    /// so it works for any character the target control can render, not just what's in <see cref="KeyMap"/>.
    /// One down+up INPUT pair per UTF-16 code unit (surrogate pairs go as their two halves — Windows' own
    /// Unicode SendInput handles them naturally). False on incomplete injection, so a chat caller must
    /// not send Enter after a partial message.</summary>
    public static bool SendText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        if (ModifierPairs.Any(p => IsDown(p.Left) || IsDown(p.Right))) return false;
        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);
        foreach (var ch in text)   // char = one UTF-16 code unit; surrogate halves ride through individually
        {
            inputs.Add(UnicodeKey(ch, up: false));
            inputs.Add(UnicodeKey(ch, up: true));
        }
        var arr = inputs.ToArray();
        uint sent = NativeMethods.SendInput((uint)arr.Length, arr, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != arr.Length)
        {
            var cleanup = ReleasesForPrefix(arr, sent);
            if (cleanup.Length > 0)
                NativeMethods.SendInput((uint)cleanup.Length, cleanup, Marshal.SizeOf<NativeMethods.INPUT>());
            System.Diagnostics.Trace.WriteLine(
                $"[Keys] SendText injected {sent}/{arr.Length} unicode events for a {text.Length}-char string — blocked (elevated foreground app?)");
        }
        return sent == arr.Length;
    }

    // KEYEVENTF_UNICODE (0x0004): wVk must be 0 and wScan carries the UTF-16 code unit directly — Windows
    // synthesizes the character with no keyboard-layout/VK mapping involved.
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private static NativeMethods.INPUT UnicodeKey(char ch, bool up) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        Data = new()
        {
            Keyboard = new()
            {
                wVk     = 0,
                wScan   = ch,
                dwFlags = KEYEVENTF_UNICODE | (up ? NativeMethods.KEYEVENTF_KEYUP : 0),
            },
        },
    };

    // ⚠ VKs whose hardware scan code carries the E0 prefix — every E0 key emitted here must be listed.
    // Two reasons: games/RawInput need the extended-key flag or nav keys decode to their numpad twins, and
    // because Key() sets KEYEVENTF_SCANCODE, Windows derives the key from the scan code alone (wVk is
    // ignored), so a missing entry doesn't degrade the key, it changes which key is sent. MapVirtualKey
    // drops the prefix, e.g. VolumeUp (VK 0xAF) → scan 0x30 = the letter B; Win → a dead scan code, so
    // Win+G arrives as bare G.
    private static readonly HashSet<ushort> ExtendedVks = new()
    {
        0x21, // PageUp
        0x22, // PageDown
        0x23, // End
        0x24, // Home
        0x25, // Left
        0x26, // Up
        0x27, // Right
        0x28, // Down
        0x2C, // PrintScreen (E0 37 — un-extended 0x37 is numpad-*)
        0x2D, // Insert
        0x2E, // Delete
        0x5B, // Left Win (E0 5B)
        0xAD, // Volume Mute  (E0 20 — un-extended 0x20 is D)
        0xAE, // Volume Down  (E0 2E — un-extended 0x2E is C)
        0xAF, // Volume Up    (E0 30 — un-extended 0x30 is B)
        0xB0, // Next Track
        0xB1, // Prev Track
        0xB2, // Stop Media
        0xB3, // Play/Pause
    };

    // With SCANCODE, Windows ignores wVk. Use the target layout's mapping and explicit E0 flag.
    private static NativeMethods.INPUT Key(ushort vk, bool up, IntPtr layout)
    {
        uint mapped = MapVirtualKeyEx(vk, NativeMethods.MAPVK_VK_TO_VSC_EX, layout);
        // Pause uses an E1 sequence, which KEYEVENTF_EXTENDEDKEY cannot describe. Use its virtual key.
        bool scanInput = mapped != 0 && (mapped & 0xFF00) != 0xE100;
        uint flags = scanInput ? NativeMethods.KEYEVENTF_SCANCODE : 0;
        if (up) flags |= NativeMethods.KEYEVENTF_KEYUP;
        if (scanInput && ((mapped & 0xFF00) == 0xE000 || ExtendedVks.Contains(vk)))
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        return new()
        {
            type = NativeMethods.INPUT_KEYBOARD,
            Data = new() { Keyboard = new() { wVk = scanInput ? (ushort)0 : vk,
                wScan = scanInput ? (ushort)(mapped & 255) : (ushort)0, dwFlags = flags } },
        };
    }
}
