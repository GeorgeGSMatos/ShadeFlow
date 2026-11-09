using System.Collections.ObjectModel;
using System.Windows;
using ShadeFlow.Models;
using ShadeFlow.Services;
using ShadeFlow.Views;

namespace ShadeFlow;

public partial class MainWindow
{
    private readonly ObservableCollection<BrightnessPreset> _presets = [];
    private readonly PresetSession _presetSession = new();

    private void InitializePresets()
    {
        PresetList.ItemsSource = _presets;
        ReloadPresets();
    }

    private void ReloadPresets()
    {
        _presets.Clear();
        foreach (var preset in _configuration.GetPresets()) _presets.Add(preset);
    }

    private bool SavePresets(IEnumerable<BrightnessPreset> presets)
    {
        if (!_configuration.TrySetPresets(presets))
        { ShowMessage(Status, "Não foi possível salvar: nomes devem ser únicos e o limite é de 24 presets. Verifique se a configuração permite gravação.", InterfaceTone.Danger); return false; }
        ReloadPresets();
        ShowMessage(Status, "Presets salvos.", InterfaceTone.Success);
        return true;
    }

    private void AddPresetClick(object sender, RoutedEventArgs e)
    {
        if (!_configuration.CanWrite) { ShowMessage(Status, "Esta execução não permite gravar presets.", InterfaceTone.Attention); return; }
        var dialog = new PresetDialog(_overlays.Displays, _presets) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Preset is { } preset) SavePresets(_presets.Append(preset));
    }

    private void EditPresetClick(object sender, RoutedEventArgs e)
    {
        if (!_configuration.CanWrite || (sender as FrameworkElement)?.DataContext is not BrightnessPreset preset) return;
        var dialog = new PresetDialog(_overlays.Displays, _presets, preset) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Preset is { } edited)
            SavePresets(_presets.Select(p => p.Id == edited.Id ? edited : p));
    }

    private void UpdatePresetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BrightnessPreset preset) return;
        var updated = preset.Copy();
        foreach (var display in _overlays.Displays) updated.Displays[display.Display.StableId] = Math.Round(display.Brightness);
        SavePresets(_presets.Select(p => p.Id == updated.Id ? updated : p));
    }

    private void DeletePresetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BrightnessPreset preset || !_configuration.CanWrite) return;
        if (!Views.ConfirmationDialog.Confirm(this, "Excluir preset", $"Excluir o preset ‘{preset.Name}’?", "Excluir", destructive: true)) return;
        if (SavePresets(_presets.Where(p => p.Id != preset.Id)) && _presetSession.Id == preset.Id)
        {
            _presetSession.Clear();
            if (_scheduler is not null) UpdateScheduleStatus(_scheduler.Snapshot);
        }
    }

    private void ApplyPresetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BrightnessPreset preset) ApplyPreset(preset);
    }

    private void ApplyPreset(BrightnessPreset preset)
    {
        if (_resourcesDisposed || !preset.IsValid) return;
        if (!_overlays.Displays.Any(d => preset.Displays.ContainsKey(d.Display.StableId)))
        { ShowMessage(Status, "Nenhum monitor deste preset está conectado.", InterfaceTone.Attention); _quickPanel?.ShowPresetMessage(Status.Text); return; }
        _transition.Cancel();
        _presetSession.Apply(preset);
        _manualOverride = _scheduler?.Snapshot.Enabled == true;
        _overlays.CachePresetTargets(preset.Displays);
        _transition.Start(preset.Displays, _preferences.TransitionMilliseconds);
        _overlayDiagnostics?.CaptureNow("preset-applied");
        _overlayDiagnostics?.RequestCheck("preset-applied");
        if (_scheduler is not null) UpdateScheduleStatus(_scheduler.Snapshot);
        ShowMessage(Status, $"Preset ‘{preset.Name}’ aplicado. Valores individuais preservados, mesmo com monitores vinculados.", InterfaceTone.Success);
        LogServiceWrite("Brightness preset applied.");
    }

    private void OnPresetTransitionCompleted(IReadOnlyDictionary<string, double> targets)
    {
        _overlays.CachePresetTargets(targets);
        _overlayDiagnostics?.CaptureNow("preset-transition-completed");
        _overlayDiagnostics?.RequestCheck("preset-transition-completed");
    }
    private void OnQuickPanelPresetRequested(BrightnessPreset preset) => ApplyPreset(preset);
    private void OnQuickPanelPresetsRequested(object? sender, EventArgs e)
    {
        ShowMainWindow();
        PresetsNavigation.IsChecked = true;
    }
}
