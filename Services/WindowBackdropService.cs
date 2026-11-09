using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Runtime.InteropServices;

namespace ShadeFlow.Services;

public enum WindowBackdrop { MainWindow, TransientWindow }

/// <summary>Best-effort Windows 11 material. A failed DWM call always leaves normal WPF rendering usable.</summary>
public static class WindowBackdropService
{
    public static readonly DependencyProperty TransientProperty = DependencyProperty.RegisterAttached(
        "Transient", typeof(bool), typeof(WindowBackdropService), new PropertyMetadata(false, TransientChanged));
    public static void SetTransient(DependencyObject target, bool value) => target.SetValue(TransientProperty, value);
    public static bool GetTransient(DependencyObject target) => (bool)target.GetValue(TransientProperty);

    private static void TransientChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Window window) return;
        window.SourceInitialized -= TransientInitialized;
        window.Loaded -= TransientLoaded;
        if (!(bool)args.NewValue) return;
        window.SourceInitialized += TransientInitialized;
        window.Loaded += TransientLoaded;
    }
    private static void TransientInitialized(object? sender, EventArgs args)
    { if (sender is Window window) Apply(window, WindowBackdrop.TransientWindow, ThemeService.DetectDarkTheme()); }
    private static void TransientLoaded(object sender, RoutedEventArgs args)
    { if (sender is Window window) Apply(window, WindowBackdrop.TransientWindow, ThemeService.DetectDarkTheme()); }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwcpRound = 2;
    private const int DwmsbtMainWindow = 2;
    private const int DwmsbtTransientWindow = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

    public static void Apply(Window window, WindowBackdrop backdrop, bool darkMode)
    {
        // Keep an opaque, themed fallback until native composition has actually succeeded.
        window.SetResourceReference(Window.BackgroundProperty, "ShadeBackgroundBrush");
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == 0) return;
            var dark = darkMode ? 1 : 0;
            _ = NativeMethods.DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
            var rounded = DwcpRound;
            _ = NativeMethods.DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref rounded, sizeof(int));
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) || SystemParameters.HighContrast || !DropdownSurface.TransparencyEnabled()) return;
            var type = backdrop == WindowBackdrop.MainWindow ? DwmsbtMainWindow : DwmsbtTransientWindow;
            if (NativeMethods.DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref type, sizeof(int)) < 0) return;
            // Background="Transparent" alone leaves the HWND rendering surface opaque in WPF.
            // WindowChrome owns the frame on the main window; extend it directly for the flyout.
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            if (WindowChrome.GetWindowChrome(window) is null && DwmExtendFrameIntoClientArea(handle, ref margins) < 0) return;
            if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
            {
                target.BackgroundColor = Colors.Transparent;
                window.Background = System.Windows.Media.Brushes.Transparent;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            LogService.Current?.Warn("Windows backdrop is unavailable; using WPF fallback.");
        }
    }
}
