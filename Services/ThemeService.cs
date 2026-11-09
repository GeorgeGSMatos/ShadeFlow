using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using SystemColors = System.Windows.SystemColors;

namespace ShadeFlow.Services;

public sealed class ThemeService : IDisposable
{
    private bool _subscribed;
    public bool IsDark => DetectDarkTheme();
    public event EventHandler? ThemeChanged;

    public void Start()
    {
        if (_subscribed) return;
        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _subscribed = true;
        }
        catch (Exception) { }
    }

    public void Apply(System.Windows.Application application, bool? darkMode = null)
    {
        var dark = darkMode ?? IsDark;
        foreach (var role in new[] { "Background", "Surface", "SolidSurface", "SurfaceHover", "Text", "MutedText", "Border", "Track", "Navigation", "QuickTint", "Error", "Danger", "Manual", "Success", "Warning", "Accent", "AccentText", "Selection", "InteractionOutline", "InteractionFill", "MenuTint", "DangerHover", "AccentHover", "SelectionText" })
        {
            var color = (System.Windows.Media.Color)application.FindResource($"{(dark ? "Dark" : "Light")}{role}Color");
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            application.Resources[$"Shade{role}Brush"] = brush;
        }
        if (SystemParameters.HighContrast)
        {
            foreach (var name in new[] { "ShadeBackgroundBrush", "ShadeSurfaceBrush", "ShadeSolidSurfaceBrush", "ShadeNavigationBrush", "ShadeQuickTintBrush", "ShadeMenuTintBrush" })
                application.Resources[name] = SystemColors.WindowBrush;
            foreach (var name in new[] { "ShadeTextBrush", "ShadeMutedTextBrush", "ShadeBorderBrush", "ShadeTrackBrush", "ShadeErrorBrush", "ShadeDangerBrush", "ShadeManualBrush", "ShadeSuccessBrush", "ShadeWarningBrush", "ShadeInteractionOutlineBrush" })
                application.Resources[name] = SystemColors.WindowTextBrush;
            foreach (var name in new[] { "ShadeSurfaceHoverBrush", "ShadeInteractionFillBrush", "ShadeDangerHoverBrush", "ShadeAccentHoverBrush" })
                application.Resources[name] = SystemColors.WindowBrush;
            application.Resources["ShadeSelectionTextBrush"] = SystemColors.HighlightTextBrush;
            application.Resources["ShadeAccentBrush"] = SystemColors.HighlightBrush;
            application.Resources["ShadeSelectionBrush"] = SystemColors.HighlightBrush;
            application.Resources["ShadeAccentTextBrush"] = SystemColors.HighlightTextBrush;
        }
    }

    public static bool DetectDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (key?.GetValue("AppsUseLightTheme") as int? ?? 1) == 0;
        }
        catch { return false; }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) => ThemeChanged?.Invoke(this, EventArgs.Empty);
    public void Dispose()
    {
        if (!_subscribed) return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _subscribed = false;
    }
}
