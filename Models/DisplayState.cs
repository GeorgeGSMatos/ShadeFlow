using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShadeFlow.Models;

public sealed class DisplayState : INotifyPropertyChanged
{
    private double _brightness = 100;
    private string? _alias;
    private DisplayInfo _display;

    public DisplayState(DisplayInfo display, double brightness = 100)
    {
        _display = display;
        _brightness = double.IsFinite(brightness) ? Math.Clamp(brightness, 0, 100) : 100;
    }

    public DisplayInfo Display
    {
        get => _display;
        internal set
        {
            _display = value;
            Changed();
            Changed(nameof(DisplayName));
            Changed(nameof(OriginalFriendlyName));
            Changed(nameof(Description));
            Changed(nameof(ResolutionDescription));
        }
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(Alias)
        ? Alias
        : !string.IsNullOrWhiteSpace(Display.FriendlyName) ? Display.FriendlyName : Display.DeviceName;

    public string OriginalFriendlyName => Display.FriendlyName;

    public string ResolutionDescription => $"{Display.Resolution.Width} × {Display.Resolution.Height}" +
        (Display.Primary ? " · Principal" : "");

    public string Description => $"{Display.DeviceName} • {Display.Resolution.Width} × {Display.Resolution.Height}" +
        (Display.Primary ? " • Principal" : "");

    public string? Alias
    {
        get => _alias;
        set
        {
            if (_alias == value) return;
            _alias = value;
            Changed();
            Changed(nameof(DisplayName));
        }
    }

    public double Brightness
    {
        get => _brightness;
        set
        {
            var safeValue = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 100;
            if (_brightness == safeValue) return;
            _brightness = safeValue;
            Changed();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
