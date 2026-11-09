#if DEBUG
using System.Windows;
using RadioButton = System.Windows.Controls.RadioButton;
using Mouse = System.Windows.Input.Mouse;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using ShadeFlow.Development;
using ShadeFlow.Services;

namespace ShadeFlow;

public partial class MainWindow
{
    internal bool CanApplyDevelopmentChange => !_resourcesDisposed && !_transition.IsRunning && !_hotkeyInputs.Values.Any(input => input.IsCapturing) &&
        Mouse.Captured is null && Mouse.LeftButton != MouseButtonState.Pressed &&
        IsWindowEnabled(new WindowInteropHelper(this).Handle) &&
        !System.Windows.Application.Current.Windows.Cast<Window>().Any(window => window.Owner == this && window.IsVisible && window != _quickPanel);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint handle);

    internal void EnableDevelopment(bool hidden)
    {
        Title = "ShadeFlow · Desenvolvimento";
        StartupToggle.IsEnabled = false;
        StartupToggle.ToolTip = "A inicialização do Windows é configurada na versão de uso normal.";
        SafeModeBanner.Text = "DESENVOLVIMENTO · Perfil separado, sem escurecimento real ou atalhos globais.";
        SafeModeBanner.Visibility = Visibility.Visible;
        ShowMessage(HotkeyStatus, "Atalhos globais não são registrados no modo de desenvolvimento.", InterfaceTone.Neutral);
        if (hidden) Hide();
    }

    internal void DevelopmentMessage(string message) => ShowMessage(Status, message, InterfaceTone.Neutral);
    internal object DevelopmentStatus() => new { pid = Environment.ProcessId, title = Title, idle = CanApplyDevelopmentChange,
        profile = _configuration.SettingsPath, previewOnly = _development, spacing = TryFindResource("Space8")?.ToString(), overlays = _overlays.GetDiagnosticTargets().Count,
        page = NavigationButtons().FirstOrDefault(button => button.IsChecked == true)?.Tag?.ToString() ?? "Displays" };

    internal DevelopmentViewState CaptureDevelopmentState() => new()
    {
        Width = Width, Height = Height, Left = Left, Top = Top,
        Page = NavigationButtons().FirstOrDefault(button => button.IsChecked == true)?.Tag?.ToString() ?? "Displays"
    };

    internal void RestoreDevelopmentState(DevelopmentViewState state)
    {
        if (double.IsFinite(state.Width)) Width = Math.Clamp(state.Width, MinWidth, Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
        if (double.IsFinite(state.Height)) Height = Math.Clamp(state.Height, MinHeight, Math.Max(MinHeight, SystemParameters.VirtualScreenHeight));
        var bounds = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (double.IsFinite(state.Left) && double.IsFinite(state.Top) && bounds.IntersectsWith(new Rect(state.Left, state.Top, Width, Height)))
        { Left = state.Left; Top = state.Top; }
        var button = NavigationButtons().FirstOrDefault(button => button.Tag?.ToString() == (state.Page == "About" ? "Preferences" : state.Page));
        if (button is not null) button.IsChecked = true;
    }

    private IEnumerable<RadioButton> NavigationButtons() => VisualDescendants(this).OfType<RadioButton>().Where(button => button.GroupName == "Navigation");
    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var nested in VisualDescendants(child)) yield return nested;
        }
    }
}
#endif
