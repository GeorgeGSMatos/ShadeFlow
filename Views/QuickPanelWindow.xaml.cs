using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ShadeFlow.Models;
using ShadeFlow.Services;

namespace ShadeFlow.Views;

public sealed record QuickPanelAutomationState(string Title, string Detail, bool CanResume = false, InterfaceTone Tone = InterfaceTone.Neutral);

public partial class QuickPanelWindow : Window
{
    // Start with an eligible standard HWND so Windhawk sees it during creation.
    // Before first display, retain WS_POPUPWINDOW eligibility while removing the
    // caption/minimize/maximize/resize styles. WindowChrome owns the client frame.
    private const int GwlStyle = -16;
    private const long PopupWindowStyle = 0x80880000L; // WS_POPUP | WS_BORDER | WS_SYSMENU
    private readonly IEnumerable<DisplayState> _displays;
    private readonly Func<QuickPanelAutomationState> _automation;
    private bool _sliderInteraction;
    private bool _hideAfterInteraction;

    public QuickPanelWindow(IEnumerable<DisplayState> displays, Func<QuickPanelAutomationState> automation)
    {
        _displays = displays;
        _automation = automation;
        InitializeComponent();
        DisplayList.ItemsSource = displays;
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? RestoreRequested;
    public event EventHandler? AutomationRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? PresetsRequested;
    public event Action<BrightnessPreset>? PresetRequested;
    public void SetPresets(IEnumerable<BrightnessPreset> presets) => PresetButtons.ItemsSource = presets;
    public void ShowPresetMessage(string message) => NextAdjustment.Text = message;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PrepareFlyoutFrame();
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(FlyoutMessage);
    }

    private nint FlyoutMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0x0112) return 0; // WM_SYSCOMMAND
        var command = wParam.ToInt64() & 0xFFF0;
        if (command == 0xF060) // SC_CLOSE: dismiss, do not destroy the cached panel.
        { Hide(); handled = true; }
        else if (command == 0xF100) handled = true; // No Alt+Space menu on a flyout.
        return 0;
    }

    private void PrepareFlyoutFrame()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        var style = NativeMethods.GetWindowLongPtr(handle, GwlStyle).ToInt64();
        var desired = (style & ~(0x00C00000L | 0x00070000L)) | PopupWindowStyle;
        if (style != desired) NativeMethods.SetWindowLongPtr(handle, GwlStyle, new nint(desired));
    }

    public void ShowAtCursor()
    {
        UpdateAutomationState();
        Show();
        UpdateLayout();
        PrepareFlyoutFrame();
        try
        {
            var workArea = QuickPanelPositioning.GetCursorWorkArea();
            var source = PresentationSource.FromVisual(this);
            var dpi = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
            var bounds = QuickPanelPositioning.BottomRight(workArea,
                (int)Math.Ceiling(ActualWidth * dpi), (int)Math.Ceiling(ActualHeight * dpi));
            var handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopmost, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                NativeMethods.SwpFrameChanged);
        }
        catch (Exception ex)
        {
            LogService.Current?.Warn("Quick Panel positioning fallback: " + ex.Message);
        }
        // Reapply after SWP_FRAMECHANGED, including when an existing panel reopens.
        RefreshTheme();
        Activate();
        Focus();
    }

    public void UpdateAutomationState()
    {
        var state = _automation();
        AutomationState.Text = state.Title;
        InterfacePresentation.Apply(AutomationState, state.Tone, AutomationIcon);
        NextAdjustment.Text = state.Detail;
        ResumeAutomationButton.IsEnabled = state.CanResume;
    }

    public void RefreshTheme() => WindowBackdropService.Apply(this, WindowBackdrop.TransientWindow, ThemeService.DetectDarkTheme());

    private void WindowLoaded(object sender, RoutedEventArgs e) => RefreshTheme();
    private void SettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    private void ManageAutomationClick(object sender, RoutedEventArgs e) => AutomationRequested?.Invoke(this, EventArgs.Empty);
    private void RestoreClick(object sender, RoutedEventArgs e) => RestoreRequested?.Invoke(this, EventArgs.Empty);
    private void ResumeClick(object sender, RoutedEventArgs e) => ResumeRequested?.Invoke(this, EventArgs.Empty);
    private void PresetsClick(object sender, RoutedEventArgs e) => PresetsRequested?.Invoke(this, EventArgs.Empty);
    private void PresetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BrightnessPreset preset) PresetRequested?.Invoke(preset);
    }

    private void WindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Hide();
        e.Handled = true;
    }

    private void SliderInteractionStarted(object sender, MouseButtonEventArgs e) => _sliderInteraction = true;
    private void SliderInteractionFinished(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _sliderInteraction = false;
        if (_hideAfterInteraction && !IsActive) Hide();
        _hideAfterInteraction = false;
    }

    private void WindowDeactivated(object? sender, EventArgs e)
    {
        if (_sliderInteraction || Mouse.Captured is Slider)
        {
            _hideAfterInteraction = true;
            return;
        }
        Hide();
    }
}
