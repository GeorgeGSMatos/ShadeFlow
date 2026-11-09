using System.Diagnostics;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Input;
using System.Threading;
using ShadeFlow.Models;
using ShadeFlow.Services;
using ShadeFlow.Views;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ShadeFlow;

public partial class MainWindow : Window
{
    private readonly OverlayManager _overlays;
    private readonly bool _development;
    private readonly ConfigurationService _configuration;
    private readonly ObservableCollection<ScheduleEntry> _scheduleEntries = [];
    private DisplayService? _displays;
    private GlobalHotkeyService? _hotkey;
    private TrayService? _tray;
    private QuickPanelWindow? _quickPanel;
    private BrightnessSchedulerService? _scheduler;
    private OverlayDiagnosticsService? _overlayDiagnostics;
    private bool _updatingScheduleUi;
    private bool _exitRequested;
    private bool _resourcesDisposed;
    private readonly bool _automaticStartup;
    private readonly LaunchOptions _launchOptions;
    private readonly bool _safeMode;
    private readonly Action? _disposeSingleInstance;
    private readonly string _logsPath;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _refreshQueued;
    private bool _initializingScheduler;

    public MainWindow(LaunchOptions options, string logsPath, Action? disposeSingleInstance = null, ConfigurationService? configuration = null, bool development = false)
    {
        _development = development;
        _overlays = new OverlayManager(previewOnly: development);
        _launchOptions = options;
        _automaticStartup = options.AutomaticStartup;
        _safeMode = options.SafeMode;
        _logsPath = logsPath;
        _disposeSingleInstance = disposeSingleInstance;
        _configuration = configuration ?? new ConfigurationService(_safeMode);
        InitializeComponent();
        DisplayList.ItemsSource = _overlays.Displays;
        ScheduleList.ItemsSource = _scheduleEntries;
        _overlays.Error += OnOverlayError;
        InitializePreferences();
        InitializeBrightnessControls();
        InitializePresets();
    }

    public void Start()
    {
        // Create the message HWND/hotkeys/tray without showing an unnecessary settings window.
        if (_automaticStartup) ShowInTaskbar = false;
        new WindowInteropHelper(this).EnsureHandle();
        if (_automaticStartup) return;
        if (_safeMode || _launchOptions.ShowSettings || _tray is null) ShowMainWindow();
        else ShowQuickPanel();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        WindowBackdropService.Apply(this, WindowBackdrop.MainWindow, ThemeService.DetectDarkTheme());

        if (!_development)
        {
            try
            {
                _tray = new TrayService();
                _tray.AttachToWindow(source);
                _tray.OpenRequested += OnOpenRequested;
                _tray.QuickPanelRequested += OnQuickPanelRequested;
                _tray.RestoreBrightnessRequested += OnRestoreRequested;
                _tray.ExitRequested += OnExitRequested;
                _tray.IncreaseRequested += OnIncreaseRequested;
                _tray.DecreaseRequested += OnDecreaseRequested;
                _tray.ResumeRequested += OnResumeRequested;
                _tray.ToggleAutomationRequested += OnToggleAutomationRequested;
                _tray.Recreated += OnTrayRecreated;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Tray initialization failed: {0}", ex);
                LogError("Tray initialization failed.", ex);
                ShowMessage(Status, "O ícone da bandeja não pôde ser criado. Ctrl+Alt+0 continua disponível; o X encerrará o aplicativo com segurança.", InterfaceTone.Attention);
            }

            try
            {
                _hotkey = new GlobalHotkeyService(source);
                _hotkey.ActionRequested += OnHotkeyAction;
                var hotkeys = _safeMode ? new HotkeySettings
                {
                    IncreaseBrightness = null, DecreaseBrightness = null, ResumeAutomation = null,
                    RestoreBrightness = new HotkeyBinding(3, 0x30)
                } : _preferences.Hotkeys;
                if (!_hotkey.TryConfigure(hotkeys, out var error))
                {
                    // A conflicting optional shortcut must not prevent the emergency shortcut.
                    var restoreOnly = new HotkeySettings
                    {
                        IncreaseBrightness = null, DecreaseBrightness = null, ResumeAutomation = null,
                        RestoreBrightness = hotkeys.RestoreBrightness
                    };
                    _hotkey.TryConfigure(restoreOnly, out var fallbackError);
                    ShowMessage(HotkeyStatus, error + " " + fallbackError + " Revise e aplique os atalhos em Configurações.", InterfaceTone.Attention);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0}", ex);
                LogError("Global hotkey initialization failed.", ex);
                ShowMessage(HotkeyStatus, "Atalhos indisponíveis. Use Restaurar 100% na janela ou bandeja. " + ex.Message, InterfaceTone.Attention);
            }
        }
        _displays = new DisplayService(source);
        _displays.DisplaysChanged += OnDisplaysChanged;
        try { _overlayDiagnostics = new OverlayDiagnosticsService(Dispatcher, _overlays.GetDiagnosticTargets); }
        catch (Exception ex) { LogError("Overlay recovery initialization failed; dimming remains available.", ex); }
        QueueRefreshDisplays();
        DisplayList.IsEnabled = true;
        InitializeScheduler();
        EnsureQuickPanel();
        if (_safeMode) SafeModeBanner.Visibility = Visibility.Visible;
        LogConfigurationLoadState();
        if (_tray is not null)
            ShowMessage(Status, "Ativo. Hotplug preserva o brilho efetivo de cada monitor.", InterfaceTone.Neutral);
        else
            ShowMessage(Status, "Sem ícone da bandeja. Ctrl+Alt+0 continua disponível; o X encerrará o aplicativo com segurança.", InterfaceTone.Attention);
    }

    private void OnDisplaysChanged(object? sender, EventArgs e) => QueueRefreshDisplays();
    private void OnOverlayError(string message)
    {
        _transition.Cancel();
        _presetSession.ManualAdjustment();
        _effectiveGlobal = 100;
        MarkManualOverride();
        ShowMessage(Status, message, InterfaceTone.Danger);
    }

    private void QueueRefreshDisplays()
    {
        _ = RefreshDisplaysAsync();
    }

    private async Task RefreshDisplaysAsync()
    {
        if (_resourcesDisposed) return;
        bool entered;
        try { entered = await _refreshGate.WaitAsync(0); }
        catch (ObjectDisposedException) { return; }
        if (!entered)
        {
            _refreshQueued = true;
            return;
        }
        try
        {
            do
            {
                _refreshQueued = false;
                if (_resourcesDisposed || _displays is null) return;
                ShowMessage(Status, "Atualizando monitores…", InterfaceTone.Automatic);
                Programmatic(() => _overlays.Reconcile(_displays.Enumerate(),
                    GetNewDisplayDefaultBrightness(), !_presetSession.Active && (_preferences.LinkDisplays || _transition.IsRunning),
                    _transition.GetJoiningBrightness));
                ApplySavedAliases();
                LogServiceWrite($"Displays detected: {_overlays.Displays.Count}.");
                _overlayDiagnostics?.CaptureNow("topology-refreshed");
                _overlayDiagnostics?.RequestCheck("topology-refreshed");
            }
            while (_refreshQueued && !_resourcesDisposed);
        }
        catch (Exception ex) { _overlays.FailSafe(ex); LogError("Display topology refresh failed.", ex); }
        finally
        {
            try { _refreshGate.Release(); }
            catch (ObjectDisposedException) { }
        }
    }

    private void OnRestoreRequested(object? sender, EventArgs e) => Restore();
    private void OnOpenRequested(object? sender, EventArgs e) => ShowMainWindow();
    private void OnQuickPanelRequested(object? sender, EventArgs e) => ShowQuickPanel();
    private void OnExitRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() => ExitApplication()));
    private void RestoreAllClick(object sender, RoutedEventArgs e) => Restore();
    private void Restore()
    {
        if (_resourcesDisposed) return;
        _transition.Cancel();
        _presetSession.ManualAdjustment();
        Programmatic(_overlays.RestoreAll);
        _effectiveGlobal = 100;
        MarkManualOverride();

        if (_hotkey is not null) ShowMessage(Status, "Todos os monitores em 100%.", InterfaceTone.Success);
    }

    private double GetNewDisplayDefaultBrightness()
    {
        var currentValues = _overlays.Displays
            .Select(display => display.Brightness)
            .Distinct()
            .Take(2)
            .ToArray();
        if (currentValues.Length == 1) return currentValues[0];
        return _effectiveGlobal;
    }

    private void InitializeScheduler()
    {
        var configuration = _configuration.GetSchedule();
        ReplaceScheduleEntries(configuration.Entries);

        _updatingScheduleUi = true;
        AutomationToggle.IsChecked = !_safeMode && configuration.Enabled;
        _updatingScheduleUi = false;

        if (_safeMode)
        {
            AutomationToggle.IsEnabled = false;
            ResumeButton.IsEnabled = false;
            ScheduleStatus.Text = "Safe Mode: automação não é aplicada e a configuração não é alterada.";
            return;
        }

        _scheduler = new BrightnessSchedulerService();
        _scheduler.Updated += OnScheduleUpdated;
        _initializingScheduler = true;
        try { _scheduler.UpdateSchedule(configuration.Enabled, configuration.Entries, applyCurrent: true); }
        finally { _initializingScheduler = false; }
    }

    private void OnScheduleUpdated(ScheduleUpdate update) => RunOnUiThread(() =>
    {
        if (_scheduler?.IsCurrentGeneration(update.Generation) != true) return;
        if (update.Brightness.HasValue && _presetSession.AcceptAutomaticAdjustment())
        {
            _manualOverride = false;
            var duration = _preferences.TransitionMilliseconds;
            if (_initializingScheduler && _automaticStartup) duration = 0;
            else if (update.Reason is ScheduleUpdateReason.Resume or ScheduleUpdateReason.TimeChanged)
                duration = Math.Min(duration, 2000);
            _overlays.InvalidateDisconnectedBrightness();
            _transition.Start(update.Brightness.Value, duration);
            _overlayDiagnostics?.RequestCheck("schedule-applied");
            LogServiceWrite($"Schedule {update.Reason}: {update.Brightness.Value}%.");

            ShowMessage(Status, $"Automação aplicou {update.Brightness.Value}% em todos os monitores.", InterfaceTone.Success);
        }
        UpdateScheduleStatus(update.Snapshot);
    });

    private void UpdateScheduleStatus(ScheduleSnapshot snapshot)
    {
        DrawScheduleTimeline();
        _quickPanel?.UpdateAutomationState();
        var tone = _presetSession.Hold || _manualOverride || _presetSession.Active ? InterfaceTone.Manual
            : snapshot.Enabled ? snapshot.CurrentEntry is not null ? InterfaceTone.Automatic : InterfaceTone.Attention : InterfaceTone.Neutral;
        InterfacePresentation.Apply(AutomationState, tone, AutomationIcon);
        var canResume = snapshot.Enabled && snapshot.CurrentEntry is not null;
        ResumeButton.IsEnabled = canResume;
        _tray?.UpdateAutomation(snapshot.Enabled, canResume);
        AutomationState.Text = _presetSession.Hold ? (_presetSession.Name is { } name ? $"Preset {name} · " : "") + "Automação pausada até retomar"
            : !snapshot.Enabled ? "Automação desativada"
            : _manualOverride ? snapshot.NextOccurrence is { } next
                ? $"Ajuste manual até {next.Entry.TimeText}" : "Ajuste manual"
            : snapshot.CurrentEntry is null ? "Automação aguardando" : "Automação ativa";
        if (!snapshot.Enabled)
        {
            ScheduleStatus.Text = _scheduleEntries.Count == 0 ? "Nenhum horário configurado" : "";
            return;
        }

        ScheduleStatus.Text = _presetSession.Hold ? "Horários suspensos. Clique em Retomar automação para voltar."
            : snapshot.NextOccurrence is null
            ? "Nenhum horário configurado"
            : $"Próximo ajuste: {snapshot.NextOccurrence.Entry.TimeText} → " +
              $"{snapshot.NextOccurrence.Entry.Brightness}%";
    }

    private void AutomationToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_safeMode || _updatingScheduleUi || _scheduler is null) return;
        var enabled = AutomationToggle.IsChecked == true;
        if (_configuration.TrySetSchedule(enabled, _scheduleEntries))
        {
            _presetSession.Clear();
            _transition.Cancel();
            _manualOverride = false;
            _scheduler.UpdateSchedule(enabled, _scheduleEntries, applyCurrent: enabled);
            ShowMessage(Status, enabled ? "Automação diária ativada." : "Automação diária desativada.", enabled ? InterfaceTone.Automatic : InterfaceTone.Neutral);
            return;
        }

        _updatingScheduleUi = true;
        AutomationToggle.IsChecked = _scheduler.Snapshot.Enabled;
        _updatingScheduleUi = false;
        ShowMessage(Status, $"Não foi possível salvar em {_configuration.SettingsPath}.", InterfaceTone.Danger);
    }

    private void AddScheduleClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode) { ShowMessage(Status, "Safe Mode não altera a configuração.", InterfaceTone.Attention); return; }
        var dialog = new ScheduleEntryDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Entry is not null)
            TryReplaceScheduleEntry(null, dialog.Entry);
    }

    private void EditScheduleClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode) { ShowMessage(Status, "Safe Mode não altera a configuração.", InterfaceTone.Attention); return; }
        if ((sender as FrameworkElement)?.DataContext is not ScheduleEntry current) return;
        var dialog = new ScheduleEntryDialog(current) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Entry is not null)
            TryReplaceScheduleEntry(current, dialog.Entry);
    }

    private void DeleteScheduleClick(object sender, RoutedEventArgs e)
    {
        if (_safeMode) { ShowMessage(Status, "Safe Mode não altera a configuração.", InterfaceTone.Attention); return; }
        if ((sender as FrameworkElement)?.DataContext is not ScheduleEntry current) return;
        var result = Views.ConfirmationDialog.Confirm(this, "Excluir horário", $"Excluir o ajuste das {current.TimeText}?", "Excluir", destructive: true);
        if (!result) return;

        SaveScheduleEntries(_scheduleEntries.Where(entry => !ReferenceEquals(entry, current)));
    }

    private void TryReplaceScheduleEntry(ScheduleEntry? current, ScheduleEntry replacement)
    {
        if (_scheduleEntries.Any(entry =>
                !ReferenceEquals(entry, current) && entry.Time == replacement.Time))
        {
            Views.ConfirmationDialog.Inform(this, "Horário já cadastrado", $"Já existe um ajuste às {replacement.TimeText}. Escolha outro horário ou edite o existente.");
            return;
        }

        var entries = current is null
            ? _scheduleEntries.Append(replacement)
            : _scheduleEntries.Select(entry => ReferenceEquals(entry, current) ? replacement : entry);
        SaveScheduleEntries(entries);
    }

    private void SaveScheduleEntries(IEnumerable<ScheduleEntry> entries)
    {
        if (_safeMode) return;
        var ordered = entries.OrderBy(entry => entry.Time).ToArray();
        var enabled = AutomationToggle.IsChecked == true;
        if (!_configuration.TrySetSchedule(enabled, ordered))
        {
            ShowMessage(Status, $"Não foi possível salvar em {_configuration.SettingsPath}.", InterfaceTone.Danger);
            return;
        }

        ReplaceScheduleEntries(ordered);
        _transition.Cancel();
        // Editing the schedule only rearms the timer; keep all current manual values.
        _scheduler?.UpdateSchedule(enabled, ordered, applyCurrent: false);
        ShowMessage(Status, "Alterações salvas. Brilho atual mantido.", InterfaceTone.Success);
    }

    private void ReplaceScheduleEntries(IEnumerable<ScheduleEntry> entries)
    {
        _scheduleEntries.Clear();
        foreach (var entry in entries.OrderBy(entry => entry.Time))
            _scheduleEntries.Add(entry);
    }

    private void RunOnUiThread(Action action)
    {
        if (_resourcesDisposed || Dispatcher.HasShutdownStarted) return;
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_resourcesDisposed) action();
            }));
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceWarning("UI dispatcher rejected a schedule update: {0}", ex);
        }
    }

    private void ApplySavedAliases()
    {
        foreach (var state in _overlays.Displays)
            state.Alias = _configuration.GetDisplayAlias(state.Display.StableId);
    }

    private void AliasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CommitAlias(sender as WpfTextBox);

    private void AliasKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitAlias(sender as WpfTextBox);
        e.Handled = true;
        (sender as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private void CommitAlias(WpfTextBox? textBox)
    {
        if (_safeMode) return;
        if (textBox?.DataContext is not Models.DisplayState state) return;
        textBox.GetBindingExpression(WpfTextBox.TextProperty)?.UpdateSource();

        if (_configuration.TrySetDisplayAlias(state.Display.StableId, state.Alias))
        {
            state.Alias = _configuration.GetDisplayAlias(state.Display.StableId);
            ShowMessage(Status, string.IsNullOrEmpty(state.Alias)
                ? "Nome personalizado removido."
                : $"Nome personalizado salvo: {state.Alias}.", InterfaceTone.Success);
        }
        else
        {
            state.Alias = _configuration.GetDisplayAlias(state.Display.StableId);
            textBox.GetBindingExpression(WpfTextBox.TextProperty)?.UpdateTarget();
            ShowMessage(Status, $"Não foi possível salvar em {_configuration.SettingsPath}.", InterfaceTone.Danger);
        }
    }

    private void WindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _tray is not null)
            HideMainWindow();
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        if (_development || _tray is null)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(new Action(() => ExitApplication()));
            return;
        }
        e.Cancel = true;
        HideMainWindow();
    }

    private void HideMainWindow()
    {
        if (_tray is null)
        {
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            ShowMessage(Status, "A bandeja não está disponível; use o X para sair com segurança.", InterfaceTone.Neutral);
            return;
        }

        ShowInTaskbar = false;
        Hide();
    }

    private void ShowMainWindow()
    {
        if (_resourcesDisposed) return;
        _quickPanel?.Hide();
        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private void EnsureQuickPanel()
    {
        if (_quickPanel is not null || _resourcesDisposed) return;
        _quickPanel = new QuickPanelWindow(_overlays.Displays, GetQuickPanelAutomationState);
        _quickPanel.SettingsRequested += OnQuickPanelSettingsRequested;
        _quickPanel.RestoreRequested += OnQuickPanelRestoreRequested;
        _quickPanel.AutomationRequested += OnQuickPanelAutomationRequested;
        _quickPanel.ResumeRequested += OnResumeRequested;
        _quickPanel.PresetRequested += OnQuickPanelPresetRequested;
        _quickPanel.PresetsRequested += OnQuickPanelPresetsRequested;
        _quickPanel.SetPresets(_presets);
    }

    private void ShowQuickPanel()
    {
        if (_exitRequested || _resourcesDisposed || _safeMode) { ShowMainWindow(); return; }
        EnsureQuickPanel();
        _quickPanel?.ShowAtCursor();
        LogServiceWrite("Quick Panel shown.");
    }

    private QuickPanelAutomationState GetQuickPanelAutomationState()
    {
        var snapshot = _scheduler?.Snapshot;
        var canResume = snapshot is { Enabled: true, CurrentEntry: not null };
        if (_presetSession.Hold)
            return new(_presetSession.Name is { } name ? $"Preset {name}" : "Ajuste manual",
                "Automação pausada até retomar", canResume, InterfaceTone.Manual);
        if (_presetSession.Active)
            return new($"Preset {_presetSession.Name}", snapshot?.NextOccurrence is { } nextPreset
                ? $"Até {nextPreset.Entry.TimeText}" : "Automação desativada", canResume, InterfaceTone.Manual);
        if (snapshot is null || !snapshot.Enabled) return new("Automação desativada", "Ajuste o brilho livremente");
        if (_manualOverride)
            return new("Ajuste manual", snapshot.NextOccurrence is { } next ? $"Até {next.Entry.TimeText}" : "", canResume, InterfaceTone.Manual);
        if (snapshot.NextOccurrence is { } occurrence)
            return new("Automação ativa", $"Próximo: {occurrence.Entry.TimeText} → {occurrence.Entry.Brightness}%", canResume, InterfaceTone.Automatic);
        return new("Automação aguardando", "Nenhum horário configurado", Tone: InterfaceTone.Attention);
    }

    private void OnQuickPanelSettingsRequested(object? sender, EventArgs e) => ShowMainWindow();
    private void OnQuickPanelRestoreRequested(object? sender, EventArgs e) => Restore();
    private void OnQuickPanelAutomationRequested(object? sender, EventArgs e)
    {
        ShowMainWindow();
        AutomationNavigation.IsChecked = true;
    }

    public void ShowQuickPanelFromIpc()
    {
        if (_exitRequested || _resourcesDisposed) return;
        ShowQuickPanel();
    }

    public void ShowSettingsFromIpc()
    {
        if (_exitRequested || _resourcesDisposed) return;
        ShowMainWindow();
        LogServiceWrite("Settings shown from IPC.");
    }

    public void RefreshVisualTheme()
    {
        WindowBackdropService.Apply(this, WindowBackdrop.MainWindow, ThemeService.DetectDarkTheme());
        _quickPanel?.RefreshTheme();
        DrawScheduleTimeline();
    }

    public void ExitApplication(int exitCode = 0)
    {
        if (_exitRequested) return;
        _exitRequested = true;

        CleanupResources();
        System.Windows.Application.Current.Shutdown(exitCode);
    }

    public void RestoreBeforeFatalError()
    {
        // Do not leave a dark overlay behind a modal fatal-error dialog.
        _scheduler?.Dispose();
        _transition.Cancel();
        Programmatic(_overlays.RestoreAll);
    }

    private void CleanupResources()
    {
        if (_resourcesDisposed) return;
        _resourcesDisposed = true;

        if (_overlayDiagnostics is not null)
        {
            TryCleanup(_overlayDiagnostics.Dispose, "overlay diagnostics cleanup");
            _overlayDiagnostics = null;
        }

        TryCleanup(_transition.Dispose, "transition cleanup");
        DetachBrightnessControls();

        // Each cleanup is attempted even if an earlier resource reports an error.
        if (_scheduler is not null)
        {
            _scheduler.Updated -= OnScheduleUpdated;
            TryCleanup(_scheduler.Dispose, "scheduler cleanup");
            _scheduler = null;
        }

        _overlays.Error -= OnOverlayError;
        TryCleanup(() => _overlays.Dispose(), "overlay cleanup");

        if (_displays is not null)
        {
            _displays.DisplaysChanged -= OnDisplaysChanged;
            TryCleanup(_displays.Dispose, "display service cleanup");
            _displays = null;
        }

        if (_hotkey is not null)
        {
            _hotkey.ActionRequested -= OnHotkeyAction;
            TryCleanup(_hotkey.Dispose, "hotkey cleanup");
            _hotkey = null;
        }

        if (_tray is not null)
        {
            _tray.OpenRequested -= OnOpenRequested;
            _tray.QuickPanelRequested -= OnQuickPanelRequested;
            _tray.RestoreBrightnessRequested -= OnRestoreRequested;
            _tray.ExitRequested -= OnExitRequested;
            _tray.IncreaseRequested -= OnIncreaseRequested;
            _tray.DecreaseRequested -= OnDecreaseRequested;
            _tray.ResumeRequested -= OnResumeRequested;
            _tray.ToggleAutomationRequested -= OnToggleAutomationRequested;
            _tray.Recreated -= OnTrayRecreated;
            TryCleanup(_tray.Dispose, "tray cleanup");
            _tray = null;
        }

        if (_quickPanel is not null)
        {
            _quickPanel.SettingsRequested -= OnQuickPanelSettingsRequested;
            _quickPanel.RestoreRequested -= OnQuickPanelRestoreRequested;
            _quickPanel.AutomationRequested -= OnQuickPanelAutomationRequested;
            _quickPanel.ResumeRequested -= OnResumeRequested;
            _quickPanel.PresetRequested -= OnQuickPanelPresetRequested;
            _quickPanel.PresetsRequested -= OnQuickPanelPresetsRequested;
            TryCleanup(_quickPanel.Close, "Quick Panel cleanup");
            _quickPanel = null;
        }

        TryCleanup(_refreshGate.Dispose, "topology refresh gate cleanup");
        if (_disposeSingleInstance is not null) TryCleanup(_disposeSingleInstance, "single instance cleanup");
    }

    private static void TryCleanup(Action cleanup, string operation)
    {
        try { cleanup(); }
        catch (Exception ex) { Trace.TraceError("{0} failed: {1}", operation, ex); }
    }

    protected override void OnClosed(EventArgs e)
    {
        CleanupResources();
        base.OnClosed(e);
    }

    private void OnTrayRecreated(object? sender, EventArgs e)
    {
        _overlayDiagnostics?.RequestCheck("taskbar-recreated");
        LogServiceWrite("Tray icon recreated after Explorer/taskbar restart.");
        ShowMessage(Status, "Ícone da bandeja restaurado.", InterfaceTone.Success);
    }

    private void LogConfigurationLoadState()
    {
        var message = _configuration.LoadState switch
        {
            ConfigurationLoadState.Migrated => "Settings migrated to schema V3.",
            ConfigurationLoadState.RecoveredFromBackup => "Settings recovered from backup.",
            ConfigurationLoadState.FutureVersion => "Future settings version preserved; using safe in-memory defaults.",
            ConfigurationLoadState.Defaults => "Using default settings.",
            _ => "Settings loaded."
        };
        LogServiceWrite(message);
        if (_configuration.LoadState == ConfigurationLoadState.FutureVersion)
            ShowMessage(Status, "Configuração de versão futura preservada; preferências padrão estão ativas nesta execução.", InterfaceTone.Attention);
    }

    private static void LogServiceWrite(string message)
    {
        Trace.TraceInformation(message);
        LogService.Current?.Info(message);
    }

    private static void LogError(string message, Exception exception)
    {
        Trace.TraceError("{0} {1}", message, exception);
        LogService.Current?.Error(message, exception);
    }
}
