using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Threading;

namespace ShadeFlow.Services;

/// <summary>
/// Event-driven diagnostics and bounded recovery of observed overlay occlusion.
/// Uses documented HWND_TOPMOST without activation or changing brightness/bounds.
/// </summary>
internal sealed class OverlayDiagnosticsService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<IReadOnlyList<OverlayManager.DiagnosticTarget>> _targets;
    private readonly DispatcherTimer _debounce;
    private readonly NativeMethods.WinEventProc _callback;
    private readonly HashSet<string> _pendingReasons = new(StringComparer.Ordinal);
    private nint _foregroundHook;
    private nint _reorderHook;
    private string? _lastSnapshot;
    private bool _disposed;
    private readonly OverlayRecoveryBudget _budget = new();
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private int _captureQueued;

    internal OverlayDiagnosticsService(Dispatcher dispatcher,
        Func<IReadOnlyList<OverlayManager.DiagnosticTarget>> targets)
    {
        _dispatcher = dispatcher;
        _targets = targets;
        _callback = OnWinEvent;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _debounce.Tick += OnDebounce;

        _foregroundHook = NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground, 0, _callback, 0, 0, NativeMethods.WineventOutOfContext);
        _reorderHook = NativeMethods.SetWinEventHook(NativeMethods.EventObjectReorder,
            NativeMethods.EventObjectReorder, 0, _callback, 0, 0, NativeMethods.WineventOutOfContext);
        if (_foregroundHook == 0 || _reorderHook == 0)
            LogService.Current?.Warn("OVERLAY-DIAG one or more WinEvent hooks could not be registered.");
        CaptureNow("diagnostics-started");
    }

    internal void CaptureNow(string reason)
    {
        if (_disposed) return;
        if (!_dispatcher.CheckAccess())
        {
            try { _dispatcher.BeginInvoke(new Action(() => Capture(reason, true))); }
            catch (InvalidOperationException) { }
            return;
        }
        Capture(reason, true);
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId,
        int childId, uint eventThread, uint eventTime)
    {
        if (_disposed || hwnd == 0) return;
        // OBJID_WINDOW is zero. Other accessibility objects would add noise without
        // describing top-level Z-order.
        if (eventType == NativeMethods.EventObjectReorder && objectId != 0) return;
        // Repairs and transitions must never generate their own repair loop.
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == (uint)Environment.ProcessId) return;
        if (Interlocked.Exchange(ref _captureQueued, 1) != 0) return;
        var reason = eventType == NativeMethods.EventSystemForeground ? "foreground" : "z-order-reordered";
        try
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref _captureQueued, 0);
                RequestCheck(reason);
            }));
        }
        catch (InvalidOperationException) { Interlocked.Exchange(ref _captureQueued, 0); }
    }

    internal void RequestCheck(string reason)
    {
        if (_disposed) return;
        _pendingReasons.Add(reason);
        // A bounded coalescing window, not a restartable debounce that can starve
        // forever when Explorer emits a stream of reorder notifications.
        if (!_debounce.IsEnabled) _debounce.Start();
    }

    private void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();
        var reason = _pendingReasons.Count == 0 ? "event" : string.Join("+", _pendingReasons.Order());
        _pendingReasons.Clear();
        Capture(reason, false);
        RecoverOccludedOverlays(reason);
    }

    private void RecoverOccludedOverlays(string reason)
    {
        if (_disposed) return;
        try
        {
            var targets = _targets();
            var order = VisibleWindowOrder();
            var foreground = NativeMethods.GetAncestor(NativeMethods.GetForegroundWindow(), NativeMethods.GaRoot);
            // Foreground plus taskbars: ignore invisible desktop infrastructure, tooltips
            // and windows on other monitors instead of treating every HWND as an obstruction.
            var obstructions = order.Where(hwnd => hwnd == foreground ||
                WindowClass(hwnd) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd").Where(hwnd =>
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid) != 0 && pid != (uint)Environment.ProcessId &&
                NativeMethods.DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int)) >= 0 && cloaked == 0 &&
                WindowClass(hwnd) is not ("Progman" or "WorkerW")).ToArray();
            var affected = targets.Where(target => obstructions.Any(hwnd =>
                NativeMethods.GetWindowRect(hwnd, out var rect) &&
                OverlayRecoveryPolicy.NeedsRecovery(order.IndexOf(target.Handle), order.IndexOf(hwnd),
                    target.Opacity, target.ExpectedBounds,
                    new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)))).ToArray();
            if (affected.Length == 0) return;
            var blockedBefore = _budget.BlockedUntil;
            if (!_budget.TryAcquire(_uptime.Elapsed))
            {
                if (_budget.BlockedUntil != blockedBefore)
                    LogService.Current?.Warn("OVERLAY-RECOVERY paused for 60 seconds after repeated occlusion; avoiding a Z-order fight.");
                return;
            }

            var overlayHandles = targets.Select(t => t.Handle).ToHashSet();
            // Preserve Quick Panel and existing topmost ShadeFlow dialogs above the filter.
            var ownTopmost = order.Where(hwnd => !overlayHandles.Contains(hwnd) &&
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid) != 0 && pid == (uint)Environment.ProcessId &&
                (NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTopmost) != 0).ToArray();
            foreach (var target in affected)
            {
                if (!NativeMethods.SetWindowPos(target.Handle, NativeMethods.HwndTopmost, 0, 0, 0, 0,
                    NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoOwnerZOrder))
                    LogService.Current?.Warn($"OVERLAY-RECOVERY failed display={target.DisplayId} error={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
            }
            foreach (var hwnd in ownTopmost.Reverse())
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HwndTopmost, 0, 0, 0, 0,
                    NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoOwnerZOrder);
            LogService.Current?.Info($"OVERLAY-RECOVERY attempted reason={reason} overlays={affected.Length}.");
            Capture("after-recovery", true);
        }
        catch (Exception ex) { LogService.Current?.Error("OVERLAY-RECOVERY inspection failed.", ex); }
    }

    private static string WindowClass(nint hwnd)
    {
        var buffer = new char[128];
        var length = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private void Capture(string reason, bool force)
    {
        if (_disposed) return;
        try
        {
            var snapshot = BuildSnapshot();
            if (!force && string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal)) return;
            _lastSnapshot = snapshot;
            LogService.Current?.Info($"OVERLAY-DIAG reason={reason} {snapshot}");
        }
        catch (Exception ex)
        {
            LogService.Current?.Error("OVERLAY-DIAG snapshot failed.", ex);
        }
    }

    private string BuildSnapshot()
    {
        var order = VisibleWindowOrder();
        var foreground = NativeMethods.GetAncestor(NativeMethods.GetForegroundWindow(), NativeMethods.GaRoot);
        var foregroundIndex = order.IndexOf(foreground);
        var builder = new StringBuilder()
            .Append("foreground=").Append(DescribeWindow(foreground))
            .Append(" foregroundZ=").Append(foregroundIndex);

        var targets = _targets();
        builder.Append(" overlayCount=").Append(targets.Count);
        foreach (var target in targets)
        {
            var styles = NativeMethods.GetWindowLongPtr(target.Handle, NativeMethods.GwlExStyle).ToInt64();
            var visible = NativeMethods.IsWindowVisible(target.Handle);
            var overlayIndex = order.IndexOf(target.Handle);
            var above = FirstVisibleAbove(target.Handle);
            var actual = NativeMethods.GetWindowRect(target.Handle, out var rect)
                ? $"{rect.Left},{rect.Top},{rect.Right - rect.Left}x{rect.Bottom - rect.Top}"
                : "unavailable";
            var expected = target.ExpectedBounds;
            var relation = foregroundIndex >= 0 && overlayIndex >= 0 && foregroundIndex < overlayIndex
                ? "FOREGROUND_ABOVE_OVERLAY"
                : "overlay-not-below-foreground";
            builder.Append(" | display=").Append(target.DisplayId)
                .Append(" hwnd=0x").Append(target.Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture))
                .Append(" visible=").Append(visible)
                .Append(" topmostStyle=").Append((styles & NativeMethods.WsExTopmost) != 0)
                .Append(" exStyle=0x").Append(styles.ToString("X", CultureInfo.InvariantCulture))
                .Append(" z=").Append(overlayIndex)
                .Append(" relation=").Append(relation)
                .Append(" above=").Append(DescribeWindow(above))
                .Append(" rect=").Append(actual)
                .Append(" expected=").Append(expected.X).Append(',').Append(expected.Y).Append(',')
                .Append(expected.Width).Append('x').Append(expected.Height)
                .Append(" opacity=").Append(target.Opacity.ToString("0.000", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    private static List<nint> VisibleWindowOrder()
    {
        var result = new List<nint>();
        var current = NativeMethods.GetTopWindow(0);
        for (var guard = 0; current != 0 && guard < 2048; guard++)
        {
            if (NativeMethods.IsWindowVisible(current)) result.Add(current);
            current = NativeMethods.GetWindow(current, NativeMethods.GwHwndNext);
        }
        return result;
    }

    private static nint FirstVisibleAbove(nint hwnd)
    {
        var current = NativeMethods.GetWindow(hwnd, NativeMethods.GwHwndPrev);
        for (var guard = 0; current != 0 && guard < 2048; guard++)
        {
            if (NativeMethods.IsWindowVisible(current)) return current;
            current = NativeMethods.GetWindow(current, NativeMethods.GwHwndPrev);
        }
        return 0;
    }

    private static string DescribeWindow(nint hwnd)
    {
        if (hwnd == 0) return "none";
        var classBuffer = new char[128];
        var length = NativeMethods.GetClassName(hwnd, classBuffer, classBuffer.Length);
        var className = length > 0 ? new string(classBuffer, 0, length) : "unknown";
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        var processName = "unknown";
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return $"0x{hwnd.ToInt64():X}/{processName}/{className}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        _debounce.Tick -= OnDebounce;
        if (_foregroundHook != 0) NativeMethods.UnhookWinEvent(_foregroundHook);
        if (_reorderHook != 0) NativeMethods.UnhookWinEvent(_reorderHook);
        _foregroundHook = 0;
        _reorderHook = 0;
        _pendingReasons.Clear();
        _uptime.Stop();
    }
}
