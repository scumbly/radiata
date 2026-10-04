using System.Runtime.InteropServices;

namespace ControllerWheel;

internal static class NativeMethods
{
    // ── SendInput ─────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int    dx, dy;
        public uint   mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT  Mouse;
        [FieldOffset(0)] public KEYBDINPUT  Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int        type;   // 0=mouse, 1=keyboard, 2=hardware
        public InputUnion Data;
    }

    public const int INPUT_KEYBOARD = 1;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetKeyNameText(int lParam, System.Text.StringBuilder lpString, int cchSize);

    public const uint MAPVK_VK_TO_VSC_EX = 0x04;

    /// <summary>Windows' human-readable name for a virtual-key code (e.g. 0x70 → "F1"), localised.
    /// Used for UI key hints so they stay in sync with whatever key is actually bound.</summary>
    public static string KeyName(uint vk)
    {
        uint sc = MapVirtualKey(vk, MAPVK_VK_TO_VSC_EX);
        int lParam = (int)((sc & 0xFF) << 16);
        if ((sc & 0xFF00) != 0) lParam |= 1 << 24;   // extended-key flag (arrows, Page Up/Down, etc.)
        var sb = new System.Text.StringBuilder(64);
        return GetKeyNameText(lParam, sb, sb.Capacity) > 0 ? sb.ToString() : $"0x{vk:X2}";
    }


    // ── Windows animation preference (Reduce Motion's OS-level signal) ────────────
    // "Show animations in Windows" (Settings ▸ Accessibility ▸ Visual effects). FALSE there means the
    // user asked the whole OS for reduced motion — MotionPolicy takes it as an initial signal alongside
    // Radiata's own setting. Re-probed on SystemEvents.UserPreferenceChanged.
    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    /// <summary>True when Windows' "Show animations" accessibility preference is OFF. A failed probe
    /// reports false (no OS signal) rather than forcing reduced motion on everyone.</summary>
    public static bool SystemAnimationsDisabled()
    {
        bool animate = true;
        return SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref animate, 0) && !animate;
    }

    public const int GWL_EXSTYLE       = -20;
    public const int WS_EX_LAYERED     = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE  = 0x08000000;
    public const int WS_EX_TOOLWINDOW  = 0x00000080;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOMOVE     = 0x0002;
    public const uint SWP_NOSIZE     = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const uint MOD_NONE  = 0x0000;
    public const uint VK_F1     = 0x70;
    public const uint VK_F2     = 0x71;
    public const uint VK_F3     = 0x72;
    public const uint VK_PRIOR  = 0x21; // Page Up
    public const uint VK_NEXT   = 0x22; // Page Down
    public const uint VK_LEFT   = 0x25;
    public const uint VK_UP     = 0x26;
    public const uint VK_RIGHT  = 0x27;
    public const uint VK_DOWN   = 0x28;
    public const uint VK_RETURN = 0x0D;
    public const uint VK_ESCAPE = 0x1B;
    public const int  WM_HOTKEY = 0x0312;
    public const int  WM_CLOSE  = 0x0010;

    public const byte VK_VOLUME_MUTE = 0xAD;
    public const byte VK_VOLUME_DOWN = 0xAE;
    public const byte VK_VOLUME_UP   = 0xAF;
    public const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    public const byte VK_MEDIA_PREV_TRACK = 0xB1;
    public const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    // Held-modifier task switching (D-pad ◀▶ "switcher" mode): Alt is pressed and HELD across several
    // Tab taps so the Alt-Tab switcher stays up and actually advances, then released to commit.
    public const byte VK_TAB   = 0x09;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_MENU  = 0x12;   // Alt
    public const uint KEYEVENTF_KEYUP       = 0x0002;
    public const uint KEYEVENTF_SCANCODE    = 0x0008;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    [DllImport("user32.dll")] public static extern int  GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern int  SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Window focus (used by launch executor)
    public const int SW_RESTORE  = 9;
    public const int SW_MAXIMIZE = 3;
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    // Force-foreground plumbing (launch/focus): Windows blocks SetForegroundWindow from a process that
    // doesn't own the foreground, and our overlay is no-activate — so grant ASFW + attach input queues.
    public const uint ASFW_ANY = 0xFFFFFFFF;
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint dwProcessId);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

    /// <summary>Best-effort bring a window to the foreground despite Windows' foreground lock and our
    /// no-activate topmost overlay: grant ASFW to any process, restore if minimised, and briefly attach our
    /// input queue to the current foreground thread so SetForegroundWindow is honoured.</summary>
    public static void ForceForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        AllowSetForegroundWindow(ASFW_ANY);
        ShowWindow(hWnd, SW_RESTORE);
        uint fgThread   = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint thisThread = GetCurrentThreadId();
        bool attached = fgThread != thisThread && AttachThreadInput(thisThread, fgThread, true);
        SetForegroundWindow(hWnd);
        BringWindowToTop(hWnd);
        if (attached) AttachThreadInput(thisThread, fgThread, false);
    }

    // ── Visible top-level windows by owning pid (the game-launch foreground watcher) ──────────────
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>pid → a visible top-level window it owns, for every process with one. Cheap enough to
    /// poll (one EnumWindows pass; no Process objects).</summary>
    public static Dictionary<uint, IntPtr> WindowedPids()
    {
        var map = new Dictionary<uint, IntPtr>();
        EnumWindows((h, _) =>
        {
            if (IsWindowVisible(h))
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid != 0 && !map.ContainsKey(pid)) map[pid] = h;
            }
            return true;
        }, IntPtr.Zero);
        return map;
    }

    // ── Find a window by its owning process's exe name (Kando-inspired, MIT — see THIRD-PARTY-LICENSES.md section 5) ────────
    // Title matching breaks on localized Windows ("Xbox" is only the English title); the process
    // image name never localizes. UWP apps are hosted by ApplicationFrameHost.exe, so the top-level
    // window's own pid is the host's — the real app owns a CHILD window (its CoreWindow), which is
    // why we also walk children when the top-level pid doesn't match.
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>First visible top-level window whose owning process image name (or, for UWP frame-hosted
    /// windows, a child window's owning image name) equals <paramref name="exeName"/> (case-insensitive,
    /// with or without ".exe"). Returns IntPtr.Zero if none. Never throws.</summary>
    public static IntPtr FindWindowByProcessExe(string exeName)
    {
        static bool Matches(uint pid, string wanted)
        {
            var path = pid == 0 ? null : ProcessImagePath((int)pid);
            if (path is null) return false;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            return string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase);
        }

        var wanted = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeName[..^4] : exeName;
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (Matches(pid, wanted)) { found = h; return false; }

            // UWP frame-hosted: the app's CoreWindow is a child owned by the app's own pid.
            var cls = new System.Text.StringBuilder(64);
            GetClassName(h, cls, cls.Capacity);
            if (cls.ToString() == "ApplicationFrameWindow")
            {
                EnumChildWindows(h, (child, _) =>
                {
                    GetWindowThreadProcessId(child, out uint childPid);
                    if (childPid != pid && Matches(childPid, wanted)) { found = h; return false; }
                    return true;
                }, IntPtr.Zero);
                if (found != IntPtr.Zero) return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // ── Process image path (Settings ▸ Exceptions watcher) ────────────────────────
    // QueryFullProcessImageName with PROCESS_QUERY_LIMITED_INFORMATION is fast and — unlike
    // Process.MainModule — works across 32/64-bit and doesn't throw on most user processes.
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint flags,
        System.Text.StringBuilder exeName, ref uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>TOKEN_QUERY-only access token handle for a process (uninstaller's owner-SID check).</summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr process, int desiredAccess, out IntPtr token);

    /// <summary>Full image path of a process by pid, or null if it can't be opened/queried (exited, or a
    /// protected/elevated process we lack rights to). Never throws.</summary>
    public static string? ProcessImagePath(int pid)
    {
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new System.Text.StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageNameW(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        catch { return null; }
        finally { CloseHandle(h); }
    }

    // Exit-app slice: identify + gracefully close the frontmost app's root window.
    public const uint GA_ROOTOWNER = 3;
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // ── Device arrival/removal notifications (input-capture re-evaluation) ────────────────────────
    // WM_DEVICECHANGE only reports interface-level arrivals/removals to a window that registered for
    // them; the blanket DBT_DEVNODES_CHANGED broadcast alone is not guaranteed for every PnP change a
    // hot-plugged pad produces, so the sink registers for all interface classes explicitly.
    public const int  WM_DEVICECHANGE          = 0x0219;
    public const int  DBT_DEVNODES_CHANGED     = 0x0007;
    public const int  DBT_DEVICEARRIVAL        = 0x8000;
    public const int  DBT_DEVICEREMOVECOMPLETE = 0x8004;
    public const uint DEVICE_NOTIFY_WINDOW_HANDLE         = 0x0;
    public const uint DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x4;
    private const int DBT_DEVTYP_DEVICEINTERFACE = 0x0005;

    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_DEVICEINTERFACE
    {
        public int  dbcc_size;
        public int  dbcc_devicetype;
        public int  dbcc_reserved;
        public Guid dbcc_classguid;
        public short dbcc_name;   // variable-length in the real struct; unused for registration
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr RegisterDeviceNotification(IntPtr recipient, ref DEV_BROADCAST_DEVICEINTERFACE filter, uint flags);
    [DllImport("user32.dll")] public static extern bool UnregisterDeviceNotification(IntPtr handle);

    public static IntPtr RegisterForAllDeviceInterfaceNotifications(IntPtr hwnd)
    {
        var filter = new DEV_BROADCAST_DEVICEINTERFACE
        {
            dbcc_size       = Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
            dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
        };
        return RegisterDeviceNotification(hwnd, ref filter,
            DEVICE_NOTIFY_WINDOW_HANDLE | DEVICE_NOTIFY_ALL_INTERFACE_CLASSES);
    }

    // Interface classes a pad can plausibly surface as: HID (everything, incl. BT/DS/GameSir) and
    // XUSB (Xbox-protocol devnodes, incl. the ViGEm virtual pad).
    public static readonly Guid GUID_DEVINTERFACE_HID  = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    public static readonly Guid GUID_DEVINTERFACE_XUSB = new("EC87F1E3-C13B-4100-B5F7-8B84D54260CB");

    /// <summary>True when a DBT_DEVICEARRIVAL / DBT_DEVICEREMOVECOMPLETE broadcast is about a
    /// pad-relevant interface (HID or XUSB). Every unreadable / unexpected payload returns true —
    /// the only dangerous outcome is a SKIPPED capture pass, so this fails open. Registration stays
    /// ALL_INTERFACE_CLASSES and DBT_DEVNODES_CHANGED (no payload) never routes here: the machine-wide
    /// catch-all is untouched. Don't switch to registering only these GUIDs — no single GUID is
    /// guaranteed for every arrival path a pad can take.</summary>
    public static bool IsPadInterfaceBroadcast(IntPtr lParam, out Guid classGuid)
    {
        classGuid = Guid.Empty;
        if (lParam == IntPtr.Zero) return true;
        try
        {
            var hdr = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
            if (hdr.dbcc_devicetype != DBT_DEVTYP_DEVICEINTERFACE) return true;
            classGuid = hdr.dbcc_classguid;
            return classGuid == GUID_DEVINTERFACE_HID
                || classGuid == GUID_DEVINTERFACE_XUSB;
        }
        catch { return true; }
    }

    /// <summary>The symbolic link a DBT_DEVTYP_DEVICEINTERFACE broadcast carries (read past the fixed
    /// header — <c>dbcc_name</c> is variable-length). False for any other payload.</summary>
    public static bool TryGetInterfacePath(IntPtr lParam, out string path)
    {
        path = "";
        if (lParam == IntPtr.Zero) return false;
        try
        {
            var hdr = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
            if (hdr.dbcc_devicetype != DBT_DEVTYP_DEVICEINTERFACE) return false;
            var name = Marshal.PtrToStringUni(lParam + (int)Marshal.OffsetOf<DEV_BROADCAST_DEVICEINTERFACE>(
                nameof(DEV_BROADCAST_DEVICEINTERFACE.dbcc_name)));
            if (string.IsNullOrEmpty(name)) return false;
            path = name;
            return true;
        }
        catch { return false; }
    }

    // Sleep / hibernate
    // Power schemes through the API rather than powercfg.exe: spawning that tool three times (read, set,
    // confirm) and waiting on each would stall a slice fire on the UI thread. These answer in microseconds
    // and need no elevation for the current user's scheme, exactly like `powercfg /setactive`.
    [DllImport("powrprof.dll")] public static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);
    [DllImport("powrprof.dll")] public static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);
    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    public static extern uint PowerReadFriendlyName(IntPtr rootPowerKey, ref Guid schemeGuid, IntPtr subGroup, IntPtr powerSetting,
                                                    byte[]? buffer, ref uint bufferSize);
    [DllImport("kernel32.dll")] public static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("powrprof.dll")] public static extern bool SetSuspendState(
        bool hibernate, bool forceCritical, bool disableWakeEvent);

    // Recycle Bin (empty, silent). Flags: 1=no confirm, 2=no progress UI, 4=no sound.
    public const uint SHERB_NOCONFIRMATION = 0x00000001;
    public const uint SHERB_NOPROGRESSUI   = 0x00000002;
    public const uint SHERB_NOSOUND        = 0x00000004;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    // ── Display topology query (display-toggle) ─────────────────────────────────
    // Reads the CURRENT topology (extend/clone/internal/external/…) so display-toggle can flip it, instead
    // of just blindly firing DisplaySwitch.exe /extend every time. QDC_DATABASE_CURRENT asks for exactly one
    // path array sized to the ACTIVE topology (vs QDC_ALL_PATHS, which returns every possible path and
    // doesn't tell you which topology is live) — GetDisplayConfigBufferSizes sizes the two buffers
    // QueryDisplayConfig needs; DISPLAYCONFIG_TOPOLOGY_ID comes back packed in the flags DWORD only for this
    // query type (see MSDN QueryDisplayConfig remarks). Path/mode array contents are unused here — only the
    // topology flag matters — so the structs are left as opaque byte blobs sized by the returned counts
    // rather than fully modeled (SetDisplayMode already does the actual mode switch via DisplaySwitch.exe).
    public const uint QDC_DATABASE_CURRENT = 0x00000004;
    public const int  QDC_ERROR_INSUFFICIENT_BUFFER = -122;   // ERROR_INSUFFICIENT_BUFFER (ships as ERROR_SUCCESS check below)

    public const uint DISPLAYCONFIG_TOPOLOGY_INTERNAL      = 0x00000001;
    public const uint DISPLAYCONFIG_TOPOLOGY_CLONE          = 0x00000002;
    public const uint DISPLAYCONFIG_TOPOLOGY_EXTEND         = 0x00000004;
    public const uint DISPLAYCONFIG_TOPOLOGY_EXTERNAL       = 0x00000008;

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(
        uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    // Opaque fixed-size stand-ins for DISPLAYCONFIG_PATH_INFO (72 bytes) / DISPLAYCONFIG_MODE_INFO (64
    // bytes) — real Win32 structs, sized exactly so QueryDisplayConfig has correctly-sized buffers to write
    // into, but with no fields modeled: nothing here reads path/mode info, only the topology id out-param.
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO { private long _b0, _b1, _b2, _b3, _b4, _b5, _b6, _b7, _b8; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_MODE_INFO { private long _b0, _b1, _b2, _b3, _b4, _b5, _b6, _b7; }

    // currentTopologyId is populated ONLY when flags == QDC_DATABASE_CURRENT (MSDN); must be null for
    // every other QDC_* flag, which this app never passes.
    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, out uint currentTopologyId);

    // ── Screen bounds (the overlay is PRIMARY-DISPLAY-ONLY by design — see docs/OVERLAY.md) ──

    /// <summary>The primary display as a WPF-DIP rect — origin (0,0) by definition. The app is
    /// System-DPI-aware and the system DPI IS the primary display's DPI, so these coordinates are exact
    /// with no conversion. That guarantee is WHY the overlay targets the primary exclusively: any
    /// follow-the-foreground-window placement divides a secondary monitor's physical rect by the
    /// PRIMARY's scale, so on mixed-DPI rigs the wheels land off-screen and the grid mis-scales.</summary>
    public static System.Windows.Rect PrimaryScreenDips() => new(0, 0,
        System.Windows.SystemParameters.PrimaryScreenWidth,
        System.Windows.SystemParameters.PrimaryScreenHeight);

    // ── Low-level mouse hook (dismiss the open wheel on a click outside it) ──────
    // The overlay is click-through (WS_EX_TRANSPARENT), so it never receives the click itself — a global
    // WH_MOUSE_LL hook is the way to notice a click anywhere while a wheel is up.
    public const int WH_MOUSE_LL   = 14;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public int    ptX;
        public int    ptY;
        public uint   mouseData;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ── Low-level keyboard hook (Esc while the Game Grid or Arcade is up) ────────
    // Both surfaces are non-activating overlay windows, so no WPF KeyDown ever reaches them. A WH_KEYBOARD_LL
    // hook sees the key wherever focus is, before RegisterHotKey processing, and can swallow it.
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN     = 0x0100;
    public const int WM_KEYUP       = 0x0101;
    public const int WM_SYSKEYDOWN  = 0x0104;
    public const int WM_SYSKEYUP    = 0x0105;
    public const uint LLKHF_ALTDOWN = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint   vkCode;
        public uint   scanCode;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static extern IntPtr SetWindowsKeyboardHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    // ── Tray-icon screen rect (onboarding's "here's the tray flower" highlight) ──────────────────
    // Shell_NotifyIconGetRect answers only for an icon THIS process owns (identified by the NotifyIcon's
    // message hwnd + callback id). The rect is in PHYSICAL screen pixels; callers convert to DIPs.

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONIDENTIFIER
    {
        public uint   cbSize;
        public IntPtr hWnd;
        public uint   uID;
        public Guid   guidItem;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    /// <summary>Screen rect (physical px) of a tray icon this process registered, or null when the shell
    /// can't locate it (icon hidden in the collapsed overflow, tray not present, or an OS refusal).
    /// Never throws.</summary>
    public static RECT? TrayIconRectPx(IntPtr messageHwnd, int iconId)
    {
        try
        {
            var ident = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd   = messageHwnd,
                uID    = (uint)iconId,
            };
            if (Shell_NotifyIconGetRect(ref ident, out var rc) != 0) return null;   // S_OK == 0
            if (rc.Right <= rc.Left || rc.Bottom <= rc.Top) return null;            // degenerate = not shown
            return rc;
        }
        catch { return null; }
    }

    /// <summary>Resolve an indirect string — the <c>@{PackageFullName?ms-resource://…}</c> form an MSIX
    /// package uses instead of a literal display name — against the package's own resource map. Returns
    /// S_OK (0) on success. Used to name Xbox/Game Pass titles whose AppxManifest DisplayName is a
    /// <c>ms-resource:</c> reference (see GameLibrary.ResolveIndirectString).</summary>
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern int SHLoadIndirectString(
        string pszSource, System.Text.StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

    // ── cfgmgr32 devnode walk (PnP ancestry — virtual-pad fingerprint, Bluetooth battery) ────────
    // Shared by PnpAncestry and XboxDeviceTree, which both walk a devnode's parent chain by instance id.
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Locate_DevNodeW")]
    public static extern uint CM_Locate_DevNode(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    public static extern uint CM_Get_Parent(out uint parent, uint devInst, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_Device_IDW")]
    public static extern uint CM_Get_Device_ID(uint devInst, System.Text.StringBuilder buffer, int bufferLen, uint flags);
}
