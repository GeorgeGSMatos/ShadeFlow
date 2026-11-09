using System.ComponentModel;
using System.Runtime.InteropServices;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public readonly record struct WorkArea(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class QuickPanelPositioning
{
    internal const int MarginPixels = 14;

    public static DisplayBounds BottomRight(WorkArea workArea, int width, int height, int margin = MarginPixels) => new(
        Math.Max(workArea.Left + margin, workArea.Right - width - margin),
        Math.Max(workArea.Top + margin, workArea.Bottom - height - margin), width, height);

    public static WorkArea GetCursorWorkArea()
    {
        if (!NativeMethods.GetCursorPos(out var cursor)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var monitor = NativeMethods.MonitorFromPoint(cursor, 2); // MONITOR_DEFAULTTONEAREST
        if (monitor == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>(), Device = string.Empty };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new WorkArea(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
    }
}
