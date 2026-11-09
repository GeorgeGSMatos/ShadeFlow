using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using ShadeFlow.Models;
using ShadeFlow.Services;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ShadeFlow.Controls;

/// <summary>Captures a draft shortcut locally. RegisterHotKey remains an explicit Apply operation.</summary>
public sealed class HotkeyCaptureButton : Button
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(HotkeyBinding), typeof(HotkeyCaptureButton),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ValueChanged));
    private static readonly DependencyPropertyKey IsCapturingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsCapturing), typeof(bool), typeof(HotkeyCaptureButton), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCapturingProperty = IsCapturingPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ValidationMessageProperty = DependencyProperty.Register(
        nameof(ValidationMessage), typeof(string), typeof(HotkeyCaptureButton), new PropertyMetadata(""));
    private Window? _owner;
    private ForegroundShortcutCapture? _capture;

    public HotkeyBinding? Value { get => (HotkeyBinding?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsCapturing => (bool)GetValue(IsCapturingProperty);
    public string ValidationMessage { get => (string)GetValue(ValidationMessageProperty); private set => SetValue(ValidationMessageProperty, value); }
    public Func<HotkeyBinding, string?>? ValidateCandidate { get; set; }
    public event EventHandler? DraftChanged;
    public event EventHandler? CaptureStarted;
    public event EventHandler? CaptureCancelled;

    public HotkeyCaptureButton()
    {
        Loaded += LoadedControl;
        Unloaded += UnloadedControl;
        UpdateLabel();
    }
    private void LoadedControl(object sender, RoutedEventArgs args)
    {
        if (_owner is not null) _owner.Deactivated -= OwnerDeactivated;
        _owner = Window.GetWindow(this);
        if (_owner is not null) _owner.Deactivated += OwnerDeactivated;
    }
    private void UnloadedControl(object sender, RoutedEventArgs args)
    {
        if (_owner is not null) _owner.Deactivated -= OwnerDeactivated;
        _owner = null;
        CancelCapture();
    }
    private void OwnerDeactivated(object? sender, EventArgs args) => CancelCapture();
    private static void ValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var control = (HotkeyCaptureButton)target;
        control.UpdateLabel();
        control.DraftChanged?.Invoke(control, EventArgs.Empty);
    }
    protected override void OnClick()
    {
        base.OnClick();
        if (IsCapturing) { CancelCapture(); return; }
        ValidationMessage = "";
        SetValue(IsCapturingPropertyKey, true);
        Focus();
        Keyboard.Focus(this);
        Content = "Pressione as teclas…";
        ToolTip = "Pressione a combinação desejada. Esc cancela.";
        AutomationProperties.SetHelpText(this, "Pressione uma combinação. Esc cancela sem alterar o atalho anterior.");
        try
        {
            var owner = _owner ?? Window.GetWindow(this) ?? throw new InvalidOperationException("A janela de captura não está disponível.");
            _capture = new ForegroundShortcutCapture(new WindowInteropHelper(owner).Handle,
                binding => Accept(binding), ShowModifiers, CancelCapture, CaptureFailed);
            CaptureStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error) { CaptureFailed(error); }
    }
    public void CancelCapture()
    {
        if (!IsCapturing) return;
        StopCapture();
        SetValue(IsCapturingPropertyKey, false);
        ValidationMessage = "";
        UpdateLabel();
        CaptureCancelled?.Invoke(this, EventArgs.Empty);
    }
    private void StopCapture()
    {
        _capture?.Dispose();
        _capture = null;
    }
    private void CaptureFailed(Exception error)
    {
        CancelCapture();
        ValidationMessage = "Não foi possível capturar as teclas. Tente novamente.";
        AutomationProperties.SetHelpText(this, ValidationMessage);
        LogService.Current?.Error("Shortcut capture failed.", error);
    }
    private void ShowModifiers(uint modifiers)
    {
        if (!IsCapturing) return;
        var label = HotkeyDisplay.Modifiers(modifiers);
        Content = label.Length == 0 ? "Pressione as teclas…" : label + " + …";
    }
    public void ClearShortcut()
    {
        CancelCapture();
        ValidationMessage = "";
        SetCurrentValue(ValueProperty, null);
    }
    public bool Accept(HotkeyBinding candidate)
    {
        if (!IsCapturing) return false;
        var error = !candidate.IsValid ? "Escolha uma tecla principal além dos modificadores." : ValidateCandidate?.Invoke(candidate);
        if (error is not null)
        {
            ValidationMessage = error;
            AutomationProperties.SetHelpText(this, error);
            return false;
        }
        StopCapture();
        SetValue(IsCapturingPropertyKey, false);
        ValidationMessage = "";
        var unchanged = Value == candidate;
        SetCurrentValue(ValueProperty, candidate);
        UpdateLabel();
        if (unchanged) DraftChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    protected override void OnPreviewKeyDown(KeyEventArgs args)
    {
        if (!IsCapturing) { base.OnPreviewKeyDown(args); return; }
        args.Handled = true;
        if (args.IsRepeat) return;
        var key = args.Key == Key.System ? args.SystemKey : args.Key == Key.ImeProcessed ? args.ImeProcessedKey : args.Key;
        if (key == Key.Escape) { CancelCapture(); return; }
        var modifiers = CurrentModifiers();
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            Content = HotkeyDisplay.Modifiers(modifiers) + " + …";
            return;
        }
        Accept(new HotkeyBinding(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)));
    }
    protected override void OnPreviewKeyUp(KeyEventArgs args)
    {
        if (!IsCapturing) { base.OnPreviewKeyUp(args); return; }
        args.Handled = true;
        var modifiers = HotkeyDisplay.Modifiers(CurrentModifiers());
        Content = modifiers.Length == 0 ? "Pressione as teclas…" : modifiers + " + …";
    }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs args)
    {
        CancelCapture();
        base.OnLostKeyboardFocus(args);
    }
    private static uint CurrentModifiers()
    {
        var keys = Keyboard.Modifiers;
        uint modifiers = 0;
        if (keys.HasFlag(ModifierKeys.Alt)) modifiers |= 1;
        if (keys.HasFlag(ModifierKeys.Control)) modifiers |= 2;
        if (keys.HasFlag(ModifierKeys.Shift)) modifiers |= 4;
        if (keys.HasFlag(ModifierKeys.Windows)) modifiers |= 8;
        return modifiers;
    }
    private void UpdateLabel()
    {
        Content = Value is { } binding ? HotkeyDisplay.Format(binding) : "Sem atalho";
        ToolTip = Content;
        AutomationProperties.SetHelpText(this, $"Atalho atual: {Content}. Clique para alterar; Esc cancela a captura.");
    }
}
