using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ShadeFlow.Models;
using ShadeFlow.Services;

namespace ShadeFlow.Views;

public partial class OverlayWindow : Window
{
    public event Action<Exception>? UpdateFailed;
    private nint _handle;
    private HwndSource? _source;
    private DisplayBounds _bounds;

    internal nint NativeHandle => _handle;
    internal DisplayBounds PhysicalBounds => _bounds;

    public OverlayWindow(DisplayBounds bounds)
    {
        _bounds = bounds;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WindowMessage);
        var styles = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GwlExStyle).ToInt64();
        Marshal.SetLastPInvokeError(0);
        var previous = NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GwlExStyle,
            new nint(styles | NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate |
                NativeMethods.WsExToolWindow | NativeMethods.WsExLayered));
        if (previous == 0 && Marshal.GetLastWin32Error() != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        // The manager assigned opacity before Show(). Place again while the HWND is being
        // initialized so the first composited frame is already at its physical monitor bounds.
        Place(_bounds);
    }

    public void Place(DisplayBounds bounds)
    {
        _bounds = bounds;
        // SetWindowPos uses physical pixels under PerMonitorV2, avoiding WPF DIP rounding.
        if (!NativeMethods.SetWindowPos(_handle, NativeMethods.HwndTopmost,
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void SetBrightness(double brightness) => Opacity = 1 - Math.Clamp(brightness, 0, 100) / 100;

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 0x0021) { handled = true; return 3; } // WM_MOUSEACTIVATE: MA_NOACTIVATE
        if (msg == 0x0084) { handled = true; return -1; } // WM_NCHITTEST: HTTRANSPARENT
        if (msg == 0x02E0)
        {
            // Let WPF process DPI changes, then enforce exact desktop bounds again.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible) return;
                try { Place(_bounds); }
                catch (Exception ex) { UpdateFailed?.Invoke(ex); }
            }));
        }
        return 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(WindowMessage);
        base.OnClosed(e);
    }
}
