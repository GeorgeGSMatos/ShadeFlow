using System.Runtime.InteropServices;

namespace ShadeFlow.Services;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const long WsExTopmost = 0x8, WsExTransparent = 0x20, WsExToolWindow = 0x80,
        WsExLayered = 0x80000, WsExNoActivate = 0x08000000;
    internal const uint GwHwndNext = 2, GwHwndPrev = 3, GaRoot = 2;
    internal const uint EventSystemForeground = 0x0003, EventObjectReorder = 0x8004,
        WineventOutOfContext = 0x0000;
    internal const uint SwpNoActivate = 0x0010, SwpFrameChanged = 0x0020,
        SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoOwnerZOrder = 0x0200;
    internal static readonly nint HwndTopmost = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; }

    internal delegate bool MonitorEnumProc(nint monitor, nint hdc, ref Rect rect, nint data);
    internal delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int objectId,
        int childId, uint eventThread, uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern nint GetTopWindow(nint hwnd);

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hwnd, uint command);

    [DllImport("user32.dll")]
    internal static extern nint GetAncestor(nint hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint hwnd, [Out] char[] className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module,
        WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hwnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string message);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
