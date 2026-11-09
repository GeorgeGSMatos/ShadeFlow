using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using ShadeFlow.Models;
using ShadeFlow.Services;

namespace ShadeFlow;

public partial class MainWindow
{
    private BrightnessTransitionService _transition = null!;
    private readonly HashSet<DisplayState> _observedDisplays = new();
    private int _programmatic;
    private bool _manualOverride;
    private double _effectiveGlobal = 100;

    private void InitializeBrightnessControls()
    {
        _transition = new BrightnessTransitionService(Dispatcher, () => _overlays.Displays,
            (display, brightness) =>
            {
                if (!_transition.HasIndividualTargets) _effectiveGlobal = brightness;
                Programmatic(() => display.Brightness = brightness);
            });
        _transition.Completed += OnTransitionCompleted;
        _transition.IndividualCompleted += OnPresetTransitionCompleted;
        _overlays.Displays.CollectionChanged += OnDisplayCollectionChanged;
    }

    private void OnTransitionCompleted(double target)
    {
        _overlayDiagnostics?.RequestCheck("transition-completed");
        _effectiveGlobal = target;
        _overlays.InvalidateDisconnectedBrightness();
    }

    private void OnDisplayCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var old in _observedDisplays.Except(_overlays.Displays).ToArray())
        {
            old.PropertyChanged -= OnDisplayBrightnessChanged;
            _observedDisplays.Remove(old);
        }
        foreach (var display in _overlays.Displays)
            if (_observedDisplays.Add(display)) display.PropertyChanged += OnDisplayBrightnessChanged;
    }

    private void OnDisplayBrightnessChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_resourcesDisposed || _programmatic > 0 || e.PropertyName != nameof(DisplayState.Brightness) ||
            sender is not DisplayState display) return;
        _transition.Cancel();
        _presetSession.ManualAdjustment();
        if (_preferences.LinkDisplays)
        {
            _overlays.InvalidateDisconnectedBrightness();
            _effectiveGlobal = display.Brightness;
            Programmatic(() => _overlays.SetAllBrightness(display.Brightness));
        }
        MarkManualOverride();
    }

    private void Programmatic(Action action)
    {
        ++_programmatic;
        try { action(); }
        finally { --_programmatic; }
    }

    private void MarkManualOverride()
    {
        _overlayDiagnostics?.RequestCheck("manual-brightness");
        _manualOverride = _scheduler?.Snapshot.Enabled == true;
        if (_manualOverride) LogServiceWrite("Manual brightness override started.");
        if (_scheduler is not null) UpdateScheduleStatus(_scheduler.Snapshot);
    }

    private void ChangeBrightness(int direction)
    {
        if (_resourcesDisposed) return;
        _transition.Cancel();
        _presetSession.ManualAdjustment();
        var delta = direction * _preferences.BrightnessStep;
        Programmatic(() =>
        {
            if (_preferences.LinkDisplays)
            {
                var current = _overlays.Displays.FirstOrDefault()?.Brightness ?? _effectiveGlobal;
                _effectiveGlobal = BrightnessMath.Step(current, delta);
                _overlays.InvalidateDisconnectedBrightness();
                _overlays.SetAllBrightness(_effectiveGlobal);
            }
            else
            {
                foreach (var display in _overlays.Displays)
                    display.Brightness = BrightnessMath.Step(display.Brightness, delta);
            }
        });
        MarkManualOverride();
    }

    private void ResumeAutomation()
    {
        if (_resourcesDisposed) return;
        if (_scheduler?.Snapshot is not { Enabled: true, CurrentEntry: not null }) return;
        _presetSession.Clear();
        _scheduler.Recalculate(applyCurrent: true);
    }

    private void ResumeAutomationClick(object sender, RoutedEventArgs e) => ResumeAutomation();
    private void OnIncreaseRequested(object? sender, EventArgs e) => ChangeBrightness(1);
    private void OnDecreaseRequested(object? sender, EventArgs e) => ChangeBrightness(-1);
    private void OnResumeRequested(object? sender, EventArgs e) => ResumeAutomation();
    private void OnToggleAutomationRequested(object? sender, EventArgs e) =>
        AutomationToggle.IsChecked = AutomationToggle.IsChecked != true;

    private void OnHotkeyAction(HotkeyAction action)
    {
        var capture = _hotkeyInputs.Values.FirstOrDefault(input => input.IsCapturing);
        if (capture is not null)
        {
            if (_hotkey is not null && _hotkey.CurrentSettings.Bindings().TryGetValue(action, out var binding))
                capture.Accept(binding);
            return;
        }
        switch (action)
        {
            case HotkeyAction.IncreaseBrightness: ChangeBrightness(1); break;
            case HotkeyAction.DecreaseBrightness: ChangeBrightness(-1); break;
            case HotkeyAction.RestoreBrightness: Restore(); break;
            case HotkeyAction.ResumeAutomation: ResumeAutomation(); break;
        }
    }

    private void DetachBrightnessControls()
    {
        _transition.Completed -= OnTransitionCompleted;
        _transition.IndividualCompleted -= OnPresetTransitionCompleted;
        _overlays.Displays.CollectionChanged -= OnDisplayCollectionChanged;
        foreach (var display in _observedDisplays) display.PropertyChanged -= OnDisplayBrightnessChanged;
        _observedDisplays.Clear();
    }
}
