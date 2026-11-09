using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly HotkeyRegistrationSet _registrations;
    private bool _disposed;

    public GlobalHotkeyService(HwndSource source)
    {
        _source = source;
        _registrations = new HotkeyRegistrationSet(
            (id, binding) => NativeMethods.RegisterHotKey(source.Handle, id,
                binding.Modifiers | 0x4000, binding.VirtualKey),
            id =>
            {
                var removed = NativeMethods.UnregisterHotKey(source.Handle, id);
                if (!removed) Trace.TraceWarning("UnregisterHotKey failed: {0}", Marshal.GetLastWin32Error());
                return removed;
            });
        source.AddHook(WindowMessage);
    }

    public event Action<HotkeyAction>? ActionRequested;
    public HotkeySettings CurrentSettings => _registrations.CurrentSettings;

    public bool TryConfigure(HotkeySettings settings, out string? error) =>
        TryConfigureCore(settings, out error);

    private bool TryConfigureCore(HotkeySettings settings, out string? error)
    {
        var configured = _registrations.TryReplace(settings, out error);
        if (!configured) LogService.Current?.Warn("RegisterHotKey failed: " + error);
        return configured;
    }

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (!_disposed && msg == 0x0312 && _registrations.Resolve((int)wParam) is { } action)
        {
            ActionRequested?.Invoke(action);
            handled = true;
        }
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.RemoveHook(WindowMessage);
        _registrations.Dispose();
    }
}
