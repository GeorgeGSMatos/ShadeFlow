using System.Diagnostics;
using System.Windows.Threading;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

// Dispatcher-owned; a stopped timer never has permission to mutate a later generation.
public sealed class BrightnessTransitionService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _clock = new();
    private readonly Func<double> _elapsedMilliseconds;
    private readonly Func<IEnumerable<DisplayState>> _displays;
    private readonly Action<DisplayState, double> _apply;
    private readonly Dictionary<DisplayState, (double Start, double Progress)> _starts = new();
    private DispatcherTimer? _timer;
    private EventHandler? _tick;
    private long _generation;
    private bool _disposed;
    private double _target;
    private int _duration;
    private Dictionary<string, double>? _targets;
    private readonly Dictionary<string, double> _presetStarts = new();
    public bool HasIndividualTargets => _targets is not null;
    public event Action<double>? Completed;
    public event Action<IReadOnlyDictionary<string, double>>? IndividualCompleted;
    public bool IsRunning => _timer is not null;

    public BrightnessTransitionService(Dispatcher dispatcher, Func<IEnumerable<DisplayState>> displays,
        Action<DisplayState, double> apply)
        : this(dispatcher, displays, apply, null) { }

    internal BrightnessTransitionService(Dispatcher dispatcher, Func<IEnumerable<DisplayState>> displays,
        Action<DisplayState, double> apply, Func<double>? elapsedMilliseconds)
    {
        _dispatcher = dispatcher;
        _displays = displays;
        _apply = apply;
        _elapsedMilliseconds = elapsedMilliseconds ?? (() => _clock.Elapsed.TotalMilliseconds);
    }

    public void Start(double target, int milliseconds)
        => StartCore(target, milliseconds, null);

    public void Start(IReadOnlyDictionary<string, double> targets, int milliseconds)
        => StartCore(100, milliseconds, targets);

    // Resolve the visual value before a reconnect's first Show, not after a timer frame.
    public double? GetJoiningBrightness(string stableId)
    {
        if (_targets is null || !_targets.TryGetValue(stableId, out var target)) return null;
        var start = _presetStarts.GetValueOrDefault(stableId, target);
        var progress = _duration <= 0 ? 1 : Math.Clamp(_elapsedMilliseconds() / _duration, 0, 1);
        return BrightnessMath.Interpolate(start, target, progress);
    }

    private double TargetFor(DisplayState display) => _targets is null ? _target :
        _targets.GetValueOrDefault(display.Display.StableId, display.Brightness);

    private void StartCore(double target, int milliseconds, IReadOnlyDictionary<string, double>? targets)
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel();
        _targets = targets?.ToDictionary(p => p.Key, p => Math.Clamp(p.Value, 0, 100));
        _target = Math.Clamp(target, 0, 100);
        _duration = milliseconds;
        foreach (var display in _displays())
        {
            _starts[display] = (display.Brightness, 0);
            _presetStarts[display.Display.StableId] = display.Brightness;
        }
        var generation = _generation;
        if (milliseconds <= 0)
        {
            foreach (var display in _displays().ToArray())
            {
                if (_disposed || generation != _generation) return;
                _apply(display, TargetFor(display));
            }
            _starts.Clear();
            if (generation == _generation)
            {
                var targetsCompleted = _targets;
                _targets = null;
                _presetStarts.Clear();
                if (targetsCompleted is null) Completed?.Invoke(_target);
                else IndividualCompleted?.Invoke(targetsCompleted);
            }
            return;
        }
        _clock.Restart();
        var timer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30)
        };
        _timer = timer;
        _tick = Tick;
        timer.Tick += _tick;
        timer.Start();

        void Tick(object? sender, EventArgs e)
        {
            if (_disposed || generation != _generation) return;
            var progress = Math.Clamp(_elapsedMilliseconds() / _duration, 0, 1);
            var active = _displays().ToArray();
            foreach (var stale in _starts.Keys.Except(active).ToArray()) _starts.Remove(stale);
            foreach (var display in active)
            {
                if (_disposed || generation != _generation) break;
                if (!_starts.TryGetValue(display, out var start))
                    _starts[display] = start = (display.Brightness, progress);
                var local = progress >= 1 ? 1 : (progress - start.Progress) / (1 - start.Progress);
                _apply(display, BrightnessMath.Interpolate(start.Start, TargetFor(display), local));
            }
            if (progress >= 1 && generation == _generation)
            {
                var targetsCompleted = _targets;
                Cancel();
                if (targetsCompleted is null) Completed?.Invoke(_target);
                else IndividualCompleted?.Invoke(targetsCompleted);
            }
        }
    }

    public void Cancel()
    {
        _dispatcher.VerifyAccess();
        ++_generation;
        _timer?.Stop();
        if (_timer is not null && _tick is not null) _timer.Tick -= _tick;
        _tick = null;
        _timer = null;
        _clock.Stop();
        _starts.Clear();
        _targets = null;
        _presetStarts.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
        Completed = null;
        IndividualCompleted = null;
    }
}
