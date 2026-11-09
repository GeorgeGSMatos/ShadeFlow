using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public sealed class BrightnessSchedulerService : IDisposable
{
    private readonly object _gate = new();
    private readonly IClock _clock;
    private ScheduleEntry[] _entries = [];
    private System.Threading.Timer? _timer;
    private ScheduleSnapshot _snapshot = new(false, null, null);
    private long _generation;
    private bool _enabled;
    private bool _disposed;
    private bool _timeChangedSubscribed;
    private bool _powerModeChangedSubscribed;
    private DateTime? _scheduledFor;

    public BrightnessSchedulerService() : this(new SystemClock(), subscribeSystemEvents: true)
    {
    }

    internal BrightnessSchedulerService(IClock clock, bool subscribeSystemEvents)
    {
        _clock = clock;
        if (!subscribeSystemEvents) return;

        try
        {
            SystemEvents.TimeChanged += OnTimeChanged;
            _timeChangedSubscribed = true;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _powerModeChangedSubscribed = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            Trace.TraceError("System schedule events are unavailable: {0}", ex);
        }
    }

    public event Action<ScheduleUpdate>? Updated;

    public ScheduleSnapshot Snapshot
    {
        get { lock (_gate) return _snapshot; }
    }

    public bool IsCurrentGeneration(long generation)
    {
        lock (_gate) return !_disposed && generation == _generation;
    }

    public void UpdateSchedule(bool enabled, IEnumerable<ScheduleEntry> entries, bool applyCurrent)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _enabled = enabled;
            _entries = entries.OrderBy(entry => entry.Time).ToArray();
        }
        Recalculate(applyCurrent);
    }

    public void Recalculate(bool applyCurrent) => Recalculate(applyCurrent, null);

    private void Recalculate(bool applyCurrent, long? expectedGeneration,
        ScheduleUpdateReason reason = ScheduleUpdateReason.Normal)
    {
        ScheduleSnapshot snapshot;
        int? brightness = null;
        long generation;

        lock (_gate)
        {
            if (_disposed || (expectedGeneration.HasValue && expectedGeneration.Value != _generation))
                return;

            _generation++;
            _timer?.Dispose();
            _timer = null;
            _scheduledFor = null;

            var now = _clock.Now;
            var plan = _enabled ? ScheduleCalculator.Calculate(_entries, now) : null;
            snapshot = new ScheduleSnapshot(
                _enabled,
                plan?.CurrentOccurrence.Entry,
                plan?.NextOccurrence);
            _snapshot = snapshot;
            generation = _generation;

            if (plan is not null)
            {
                if (applyCurrent)
                    brightness = plan.CurrentOccurrence.Entry.Brightness;

                var due = plan.NextOccurrence.OccursAt - now;
                if (due <= TimeSpan.Zero) due = TimeSpan.FromMilliseconds(100);
                _scheduledFor = plan.NextOccurrence.OccursAt;
                _timer = new System.Threading.Timer(
                    OnTimer,
                    generation,
                    due,
                    Timeout.InfiniteTimeSpan);
            }
        }

        try
        {
            Updated?.Invoke(new ScheduleUpdate(generation, snapshot, brightness, reason));
            if (brightness.HasValue)
                LogService.Current?.Info($"Schedule trigger ({reason}): {brightness.Value}%.");
        }
        catch (Exception ex)
        {
            // A ThreadPool callback must not terminate the process if a consumer is shutting down.
            Trace.TraceError("Schedule update notification failed: {0}", ex);
        }
    }

    internal BrightnessSchedulerService(Func<DateTime> now, bool subscribeSystemEvents)
        : this(new DelegateClock(now), subscribeSystemEvents) { }

    private sealed class DelegateClock(Func<DateTime> now) : IClock
    {
        public DateTime Now => now();
    }

    private void OnTimer(object? state)
    {
        if (state is not long generation) return;
        lock (_gate)
        {
            if (_disposed || generation != _generation) return;
            if (_scheduledFor is { } dueAt && _clock.Now < dueAt)
            {
                // ThreadPool timers can fire a few milliseconds early. Do not apply the
                // previous circular entry; rearm exactly enough to cross the boundary.
                var remaining = dueAt - _clock.Now;
                _timer?.Change(remaining < TimeSpan.FromMilliseconds(25)
                    ? TimeSpan.FromMilliseconds(25) : remaining, Timeout.InfiniteTimeSpan);
                return;
            }
        }
        Recalculate(applyCurrent: true, generation);
    }

    private void OnTimeChanged(object? sender, EventArgs e) =>
        Recalculate(true, null, ScheduleUpdateReason.TimeChanged);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Recalculate(true, null, ScheduleUpdateReason.Resume);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            _timer?.Dispose();
            _timer = null;
            _scheduledFor = null;
        }

        if (_timeChangedSubscribed)
        {
            SystemEvents.TimeChanged -= OnTimeChanged;
            _timeChangedSubscribed = false;
        }
        if (_powerModeChangedSubscribed)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _powerModeChangedSubscribed = false;
        }
    }
}
