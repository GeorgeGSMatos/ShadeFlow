// Deterministic adapters for logic checks on Linux. These do NOT validate WPF or native Windows events.
namespace ShadeFlow.Views
{
    public sealed class OverlayWindow(ShadeFlow.Models.DisplayBounds bounds)
    {
        public static List<OverlayWindow> Created { get; } = new();
        public event Action<Exception>? UpdateFailed;
        public double Opacity { get; set; }
        public bool Closed { get; private set; }
        public bool ShownAtInitialBounds { get; private set; }
        public double OpacityAtShow { get; private set; }
        public ShadeFlow.Models.DisplayBounds Bounds { get; private set; } = bounds;
        internal nint NativeHandle => 0;
        internal ShadeFlow.Models.DisplayBounds PhysicalBounds => Bounds;
        public void Show() { OpacityAtShow = Opacity; ShownAtInitialBounds = Bounds.Width > 0 && Bounds.Height > 0; Created.Add(this); }
        public void Place(ShadeFlow.Models.DisplayBounds value) => Bounds = value;
        public void SetBrightness(double brightness) => Opacity = 1 - brightness / 100;
        public void Close() => Closed = true;
        public void Fail() => UpdateFailed?.Invoke(new InvalidOperationException("Simulated overlay failure"));
    }

}

namespace System.Windows.Threading
{
    public enum DispatcherPriority { Render }
    public sealed class Dispatcher { public void VerifyAccess() { } }
    public sealed class DispatcherTimer
    {
        public static DispatcherTimer? Last { get; private set; }
        public DispatcherTimer(DispatcherPriority priority, Dispatcher dispatcher) => Last = this;
        public TimeSpan Interval { get; set; }
        public bool Enabled { get; private set; }
        public event EventHandler? Tick;
        public void Start() => Enabled = true;
        public void Stop() => Enabled = false;
        public void Fire() { if (Enabled) Tick?.Invoke(this, EventArgs.Empty); }
        public EventHandler? CaptureQueuedCallback() => Tick;
        public bool HasSubscribers => Tick is not null;
    }
}

namespace Microsoft.Win32
{
    public enum PowerModes { Resume, Suspend, StatusChange }
    public sealed class PowerModeChangedEventArgs(PowerModes mode) : EventArgs
    {
        public PowerModes Mode { get; } = mode;
    }
    public static class SystemEvents
    {
        public static event EventHandler? TimeChanged;
        public static event EventHandler<PowerModeChangedEventArgs>? PowerModeChanged;
        public static void ClockChanged() => TimeChanged?.Invoke(null, EventArgs.Empty);
        public static void Resume() => PowerModeChanged?.Invoke(null, new(PowerModes.Resume));
        public static bool HasSubscribers => TimeChanged is not null || PowerModeChanged is not null;
    }
}
