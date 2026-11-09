using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace ShadeFlow.Services;

/// <summary>Layered dropdown tint matching Organiza; alpha is not a native blur effect.</summary>
public static class DropdownSurface
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(DropdownSurface), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not System.Windows.Controls.ComboBox choice) return;
        choice.DropDownOpened -= Opened;
        if ((bool)args.NewValue) choice.DropDownOpened += Opened;
    }

    private static void Opened(object? sender, EventArgs args)
    {
        if (sender is not System.Windows.Controls.ComboBox choice) return;
        choice.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!choice.IsDropDownOpen || choice.Template.FindName("PART_Popup", choice) is not Popup { Child: Border surface }) return;
            surface.SetResourceReference(Border.BackgroundProperty, TransparencyEnabled() ? "ShadeMenuTintBrush" : "ShadeSolidSurfaceBrush");
            if (UiMotion.IsEnabled && Window.GetWindow(choice) is { } owner && UiMotion.GetScope(owner))
                surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(UiMotion.ControlMilliseconds)));
            else surface.BeginAnimation(UIElement.OpacityProperty, null);
        }));
    }

    internal static bool TransparencyEnabled()
    {
        if (SystemParameters.HighContrast) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException)
        {
            LogService.Current?.Error("Cannot read Windows transparency preference; using a solid dropdown.", error);
            return false;
        }
    }
}
