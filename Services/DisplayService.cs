using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public sealed class DisplayService : IDisposable
{
    private readonly HwndSource _source;
    private readonly DispatcherTimer _debounce;
    private readonly DisplayIdentityService _identityService = new();
    private bool _disposed;

    public DisplayService(HwndSource source)
    {
        _source = source;
        // Coalesce rapid cable, dock, resolution and scale notifications; no polling.
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounce.Tick += OnTick;
        _source.AddHook(WindowMessage);
    }

    public event EventHandler? DisplaysChanged;

    public IReadOnlyList<DisplayInfo> Enumerate()
    {
        var displays = new List<DisplayInfo>();
        IReadOnlyDictionary<string, DisplayIdentity> identities;
        try
        {
            identities = _identityService.GetActiveIdentities();
        }
        catch (Exception ex)
        {
            // Identity is optional metadata. Enumeration and overlays must remain available.
            System.Diagnostics.Trace.TraceError("Display identity query failed: {0}", ex);
            LogService.Current?.Error("Display identity query failed; using GDI fallback.", ex);
            identities = new Dictionary<string, DisplayIdentity>();
        }
        int error = 0;
        bool Visit(nint monitor, nint hdc, ref NativeMethods.Rect rect, nint data)
        {
            var info = new NativeMethods.MonitorInfo
            {
                Size = Marshal.SizeOf<NativeMethods.MonitorInfo>(), Device = string.Empty
            };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }
            var b = info.Monitor;
            var deviceName = info.Device.TrimEnd('\0');
            if (!identities.TryGetValue(deviceName, out var identity))
                identity = DisplayIdentityService.CreateFallback(deviceName);
            var bounds = new DisplayBounds(b.Left, b.Top, b.Right - b.Left, b.Bottom - b.Top);
            displays.Add(new DisplayInfo(
                // Preserve the validated runtime overlay key. StableId is metadata/persistence only.
                deviceName,
                identity.StableId,
                deviceName,
                identity.FriendlyName,
                identity.MonitorDevicePath,
                bounds,
                new DisplayResolution(bounds.Width, bounds.Height),
                (info.Flags & 1) != 0,
                identity.HasStableHardwareIdentity));
            return true;
        }
        if (!NativeMethods.EnumDisplayMonitors(0, 0, Visit, 0))
            throw new Win32Exception(error != 0 ? error : Marshal.GetLastWin32Error());
        return displays.OrderByDescending(d => d.Primary).ThenBy(d => d.DeviceName).ToArray();
    }

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // WM_DISPLAYCHANGE / WM_DEVICECHANGE. No polling or hardware brightness queries.
        if (msg is 0x007E or 0x0219)
        {
            _debounce.Stop();
            _debounce.Start();
            LogService.Current?.Info("Display topology change received; refresh debounced.");
        }
        return 0;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (_disposed) return;
        DisplaysChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        _debounce.Tick -= OnTick;
        _source.RemoveHook(WindowMessage);
    }
}
