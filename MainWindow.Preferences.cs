using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ShadeFlow.Models;
using ShadeFlow.Services;
using ShadeFlow.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ShadeFlow;

public partial class MainWindow
{
    private Preferences _preferences = new();
    private readonly StartupService _startup = new();
    private bool _updatingPreferences;
    private Dictionary<HotkeyAction, HotkeyCaptureButton> _hotkeyInputs = new();
    private sealed record Choice(int Value, string Label);

    private void InitializePreferences()
    {
        _updatingPreferences = true;
        TransitionChoice.ItemsSource = new[]
        {
            new Choice(0, "Instantâneo"), new Choice(500, "500 ms"), new Choice(2000, "2 segundos"),
            new Choice(5000, "5 segundos"), new Choice(15000, "15 segundos"), new Choice(30000, "30 segundos")
        };
        StepChoice.ItemsSource = new[] { 1, 2, 5, 10 };
        _hotkeyInputs = new()
        {
            [HotkeyAction.IncreaseBrightness] = IncreaseHotkey,
            [HotkeyAction.DecreaseBrightness] = DecreaseHotkey,
            [HotkeyAction.RestoreBrightness] = RestoreHotkey,
            [HotkeyAction.ResumeAutomation] = ResumeHotkey
        };
        foreach (var input in _hotkeyInputs.Values)
        {
            input.IsEnabled = !_safeMode;
            input.ValidateCandidate = candidate => ValidateHotkeyDraft(input, candidate);
            input.CaptureStarted += (_, _) =>
            {
                foreach (var other in _hotkeyInputs.Values.Where(other => other != input)) other.CancelCapture();
                ShowMessage(HotkeyStatus, "Pressione a combinação desejada. Esc cancela sem alterar o atalho.", InterfaceTone.Automatic);
            };
            input.CaptureCancelled += (_, _) =>
            {
                if (!_updatingPreferences) ShowMessage(HotkeyStatus, "Captura cancelada. O atalho anterior foi mantido.", InterfaceTone.Neutral);
            };
            input.DraftChanged += (_, _) =>
            {
                if (_updatingPreferences) return;
                ShowMessage(HotkeyStatus, RestoreHotkey.Value is null
                    ? "O atalho de recuperação foi removido. A alteração só entra em vigor em Aplicar atalhos."
                    : "Alterações pendentes. Clique em Aplicar atalhos para salvar.",
                    RestoreHotkey.Value is null ? InterfaceTone.Attention : InterfaceTone.Neutral);
            };
        }
        _preferences = _configuration.GetPreferences();
        UpdatePreferencesUi();
        if (!_safeMode && !_development) SyncStartupState();
    }

    private void UpdatePreferencesUi()
    {
        _updatingPreferences = true;
        try
        {
            LinkToggle.IsChecked = _preferences.LinkDisplays;
            StartupToggle.IsChecked = _preferences.StartWithWindows;
            TransitionChoice.SelectedValue = _preferences.TransitionMilliseconds;
            StepChoice.SelectedItem = _preferences.BrightnessStep;
            var bindings = _preferences.Hotkeys.Bindings();
            foreach (var pair in _hotkeyInputs)
                pair.Value.Value = bindings.GetValueOrDefault(pair.Key);
            RestoreHint.Text = _preferences.Hotkeys.RestoreBrightness is { } restore
                ? $"Restaurar imediatamente: {FormatHotkey(restore)}"
                : "Aviso: sem atalho de restauração. Use o botão Restaurar 100% ou a bandeja.";
        }
        finally { _updatingPreferences = false; }
    }

    private void QuickPreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (_safeMode || _updatingPreferences || TransitionChoice.SelectedValue is not int duration ||
            StepChoice.SelectedItem is not int step) return;
        var next = _preferences with
        {
            TransitionMilliseconds = duration, BrightnessStep = step, LinkDisplays = LinkToggle.IsChecked == true
        };
        if (_configuration.TrySetPreferences(next))
        {
            _preferences = next;
            ShowMessage(Status, "Preferências salvas.", InterfaceTone.Success);
        }
        else
        {
            UpdatePreferencesUi();
            ShowMessage(Status, "Não foi possível salvar as preferências.", InterfaceTone.Danger);
        }
    }

    private void SettingsExpanded(object sender, RoutedEventArgs e)
    {
        if (!_safeMode && !_development && !_updatingPreferences) SyncStartupState();
    }

    private void SyncStartupState()
    {
        try
        {
            var enabled = _startup.IsEnabled();
            if (_preferences.StartWithWindows != enabled)
            {
                _preferences = _preferences with { StartWithWindows = enabled };
                if (!_configuration.TrySetPreferences(_preferences))
                    ShowMessage(StartupStatus, "Estado real do Windows detectado, mas não foi possível sincronizar o JSON.", InterfaceTone.Danger);
            }
            _updatingPreferences = true;
            StartupToggle.IsChecked = enabled;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Startup state read failed: {0}", ex);
            ShowMessage(StartupStatus, "Não foi possível consultar início com Windows: " + ex.Message, InterfaceTone.Danger);
        }
        finally { _updatingPreferences = false; }
    }

    private void StartupToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_safeMode || _development || _updatingPreferences) return;
        try
        {
            if (StartupToggle.IsChecked == true) _startup.Enable();
            else _startup.Disable();
            ShowMessage(StartupStatus, "Preferência aplicada ao Windows para este executável.", InterfaceTone.Neutral);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Startup change failed: {0}", ex);
            ShowMessage(StartupStatus, "Não foi possível alterar início com Windows: " + ex.Message, InterfaceTone.Danger);
        }
        SyncStartupState();
    }

    private void SaveHotkeysClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode)
        {
            ShowMessage(HotkeyStatus, "Safe Mode não altera atalhos salvos.", InterfaceTone.Attention);
            return;
        }
        try
        {
            var next = new HotkeySettings
            {
                IncreaseBrightness = IncreaseHotkey.Value,
                DecreaseBrightness = DecreaseHotkey.Value,
                RestoreBrightness = RestoreHotkey.Value,
                ResumeAutomation = ResumeHotkey.Value
            };
            if (!next.IsValid()) throw new ArgumentException("Atalhos inválidos ou repetidos. Escolha uma combinação diferente para cada ação.");
            if (_development)
            {
                var draftPreferences = _preferences with { Hotkeys = next };
                if (!_configuration.TrySetPreferences(draftPreferences)) throw new InvalidOperationException("Não foi possível salvar os atalhos no perfil de desenvolvimento.");
                _preferences = draftPreferences;
                UpdatePreferencesUi();
                ShowMessage(HotkeyStatus, "Atalhos salvos no perfil de desenvolvimento. O registro global e os conflitos com outros programas são verificados na versão normal.", InterfaceTone.Attention);
                return;
            }
            if (_hotkey is null)
                throw new InvalidOperationException("Serviço de atalhos indisponível nesta execução.");
            var previous = _hotkey.CurrentSettings;
            if (!_hotkey.TryConfigure(next, out var error))
                throw new InvalidOperationException(error);
            var preferences = _preferences with { Hotkeys = next };
            if (!_configuration.TrySetPreferences(preferences))
            {
                _hotkey.TryConfigure(previous, out var rollbackError);
                throw new InvalidOperationException("Não foi possível salvar os atalhos. " + rollbackError);
            }
            _preferences = preferences;
            UpdatePreferencesUi();
            ShowMessage(HotkeyStatus, next.RestoreBrightness is null
                ? "Aviso: restauração sem hotkey. O botão e a bandeja continuam disponíveis."
                : "Atalhos registrados e salvos.", next.RestoreBrightness is null ? InterfaceTone.Attention : InterfaceTone.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowMessage(HotkeyStatus, ex.Message, InterfaceTone.Attention);
        }
    }

    private static string FormatHotkey(HotkeyBinding binding) => HotkeyDisplay.Format(binding);

    private string? ValidateHotkeyDraft(HotkeyCaptureButton control, HotkeyBinding candidate)
    {
        var duplicate = _hotkeyInputs.FirstOrDefault(pair => pair.Value != control && pair.Value.Value == candidate);
        if (duplicate.Value is null) return null;
        var action = duplicate.Key switch
        {
            HotkeyAction.IncreaseBrightness => "Aumentar brilho",
            HotkeyAction.DecreaseBrightness => "Diminuir brilho",
            HotkeyAction.RestoreBrightness => "Restaurar 100%",
            HotkeyAction.ResumeAutomation => "Retomar automação",
            _ => throw new ArgumentOutOfRangeException(nameof(duplicate))
        };
        return $"Essa combinação já está em {action}. Escolha outra ou limpe o atalho anterior.";
    }

    private void ClearHotkeyClick(object sender, RoutedEventArgs args)
    {
        if (_safeMode) return;
        if (sender is WpfButton { Tag: string name } && FindName(name) is HotkeyCaptureButton control)
            control.ClearShortcut();
    }

    private void ExportConfigurationClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode)
        {
            ShowMessage(Status, "Safe Mode não exporta nem altera a configuração.", InterfaceTone.Attention);
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar configuração do ShadeFlow",
            Filter = "Arquivo JSON|*.json",
            FileName = "shadeflow-settings.json",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        if (_configuration.TryExport(dialog.FileName, out var error))
        {
            ShowMessage(Status, "Configuração exportada.", InterfaceTone.Success);
            LogServiceWrite("Configuration exported.");
        }
        else
        {
            Views.ConfirmationDialog.Inform(this, "Falha ao exportar", "Não foi possível exportar a configuração: " + error, InterfaceTone.Danger);
        }
    }

    private void ImportConfigurationClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode)
        {
            ShowMessage(Status, "Safe Mode não importa nem altera a configuração.", InterfaceTone.Attention);
            return;
        }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importar configuração do ShadeFlow",
            Filter = "Arquivo JSON|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!Views.ConfirmationDialog.Confirm(this, "Importar configuração",
            "Importar esta configuração substituirá nomes de monitores, presets, automação, transição e atalhos salvos. " +
            "O estado de iniciar com Windows desta máquina será preservado.", "Importar")) return;

        bool actualStartup;
        try { actualStartup = !_development && _startup.IsEnabled(); }
        catch (Exception ex)
        {
            Views.ConfirmationDialog.Inform(this, "Falha ao consultar preferência", "Não foi possível consultar início com Windows: " + ex.Message, InterfaceTone.Danger);
            return;
        }

        if (!_configuration.TryImport(dialog.FileName, actualStartup, out var error))
        {
            Views.ConfirmationDialog.Inform(this, "Configuração inválida", "Arquivo de configuração inválido ou incompatível: " + error, InterfaceTone.Danger);
            return;
        }
        ApplyImportedConfiguration();
        ShowMessage(Status, "Configuração importada.", InterfaceTone.Success);
        LogServiceWrite("Configuration imported.");
    }

    private void ResetConfigurationClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode)
        {
            ShowMessage(Status, "Safe Mode não restaura configurações.", InterfaceTone.Attention);
            return;
        }
        if (!Views.ConfirmationDialog.Confirm(this, "Restaurar padrões",
            "Restaurar nomes de monitores, presets, automação, preferências e atalhos padrão? Os logs serão preservados.",
            "Restaurar", destructive: true)) return;
        try { if (!_development) _startup.Disable(); }
        catch (Exception ex)
        {
            Views.ConfirmationDialog.Inform(this, "Falha ao restaurar", "Não foi possível remover início com Windows: " + ex.Message, InterfaceTone.Danger);
            return;
        }
        if (!_configuration.TryReset(out var error))
        {
            Views.ConfirmationDialog.Inform(this, "Falha ao restaurar", "Não foi possível restaurar as configurações: " + error, InterfaceTone.Danger);
            return;
        }
        ApplyImportedConfiguration();
        Restore();
        ShowMessage(Status, "Configurações padrão restauradas.", InterfaceTone.Success);
        LogServiceWrite("Configuration reset to defaults.");
    }

    private void ApplyImportedConfiguration()
    {
        _transition.Cancel();
        _presetSession.Clear();
        ReloadPresets();
        var nextPreferences = _configuration.GetPreferences();
        if (_hotkey is not null && !_hotkey.TryConfigure(nextPreferences.Hotkeys, out var hotkeyError))
            ShowMessage(HotkeyStatus, "Configuração importada, mas alguns atalhos não puderam ser registrados: " + hotkeyError, InterfaceTone.Attention);
        _preferences = nextPreferences;
        UpdatePreferencesUi();
        var schedule = _configuration.GetSchedule();
        ReplaceScheduleEntries(schedule.Entries);
        _updatingScheduleUi = true;
        AutomationToggle.IsChecked = schedule.Enabled;
        _updatingScheduleUi = false;
        _manualOverride = false;
        _scheduler?.UpdateSchedule(schedule.Enabled, schedule.Entries, applyCurrent: schedule.Enabled);
        ApplySavedAliases();
    }

    private void AboutClick(object sender, RoutedEventArgs e)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.2";
        var snapshot = _scheduler?.Snapshot ?? new ScheduleSnapshot(false, null, null);
        var diagnostics = DiagnosticsService.Build(version, _configuration.SettingsPath, _logsPath,
            _overlays.Displays, snapshot, _manualOverride, _preferences, _safeMode);
        new Views.AboutWindow(version, _configuration.SettingsPath, _logsPath, diagnostics) { Owner = this }.ShowDialog();
    }
}
