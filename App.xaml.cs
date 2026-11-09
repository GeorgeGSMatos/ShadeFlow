using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ShadeFlow.Services;
using ShadeFlow.Development;
using System.IO;

namespace ShadeFlow;

public partial class App : System.Windows.Application
{
    private LogService? _log;
    private SingleInstanceService? _singleInstance;
    private ThemeService? _theme;
    private bool _shutdownStarted;
#if DEBUG
    private DevelopmentRuntime? _development;
#endif

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        SessionEnding += OnSessionEnding;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
        try { _ = NativeMethods.SetCurrentProcessExplicitAppUserModelID("ShadeFlow.App"); }
        catch (Exception) { }

        var development = DevelopmentOptions.Parse(e.Args);
#if DEBUG
        _log = development is null ? new LogService() : new LogService(Path.Combine(development.ProfileDirectory, "Logs"), () => DateTime.Now);
#else
        _log = new LogService();
#endif
        LogService.Current = _log;
        _theme = new ThemeService();
        _theme.Apply(this);
        _theme.ThemeChanged += OnThemeChanged;
        _theme.Start();
        var options = LaunchOptions.Parse(e.Args);
        _log.Info($"Startup {GetVersion()} ({options.InitialView}{(options.SafeMode ? ", --safe-mode" : string.Empty)}).");
        _log.Info("Executable path: " + Environment.ProcessPath);
        foreach (var unknown in options.UnknownArguments) _log.Warn($"Ignoring unknown argument: {unknown}");

#if DEBUG
        if (development is not null)
        {
            _ = NativeMethods.SetCurrentProcessExplicitAppUserModelID("ShadeFlow.Development");
            var scope = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(development.SourceRoot)))[..24];
            _singleInstance = new SingleInstanceService("Development." + scope);
        }
        else
#endif
            _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire())
        {
            _log.Info(options.AutomaticStartup
                ? "Duplicate --startup invocation exits silently."
                : "Second instance detected; requesting activation of the first instance.");
            if (!options.AutomaticStartup)
            {
                var command = options.InitialView == LaunchView.Settings
                    ? SingleInstanceService.ShowSettingsCommand
                    : SingleInstanceService.ShowQuickPanelCommand;
                _singleInstance.TrySendCommandAsync(command, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown(0);
            return;
        }

        _singleInstance.CommandRequested += OnCommandRequested;
#if DEBUG
        var configuration = development is null ? null : new ConfigurationService(Path.Combine(development.ProfileDirectory, "settings.json"));
        if (development is not null) options = options with { ShowSettings = true, AutomaticStartup = development.Hidden };
        var window = new MainWindow(options, _log.DirectoryPath, DisposeSingleInstance, configuration, development is not null);
#else
        var window = new MainWindow(options, _log.DirectoryPath, DisposeSingleInstance);
#endif
        MainWindow = window;
        window.Start();
#if DEBUG
        if (development is not null)
        {
            _development = new DevelopmentRuntime(window, development, () => { _theme.Apply(this); window.RefreshVisualTheme(); });
            _development.Start();
        }
#endif
    }

    private void OnCommandRequested(string command)
    {
        if (_shutdownStarted || Dispatcher.HasShutdownStarted) return;
        _log?.Info("IPC command requested: " + command);
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_shutdownStarted || MainWindow is not MainWindow window) return;
                if (command == SingleInstanceService.ShowSettingsCommand) window.ShowSettingsFromIpc();
                else window.ShowQuickPanelFromIpc();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _theme?.Apply(this);
                if (MainWindow is MainWindow window) window.RefreshVisualTheme();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Trace.TraceError("Unhandled UI error: {0}", e.Exception);
        _log?.Error("Unhandled UI exception.", e.Exception);
        e.Handled = true;
        try
        {
            if (MainWindow is MainWindow window) window.RestoreBeforeFatalError();
            System.Windows.MessageBox.Show("O ShadeFlow encontrou um erro e será fechado. Os overlays serão removidos.",
                "ShadeFlow", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (MainWindow is MainWindow window)
                window.ExitApplication(1);
            else
                Shutdown(1);
        }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        _log?.Info("Windows session ending.");
        if (MainWindow is MainWindow window)
            window.ExitApplication();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _log?.Error("Unhandled process exception.", exception);
        TryEmergencyCleanup();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log?.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }

    private void TryEmergencyCleanup()
    {
        try { if (MainWindow is MainWindow window) window.RestoreBeforeFatalError(); }
        catch (Exception ex) { _log?.Error("Fatal cleanup failed.", ex); }
    }

    private void DisposeSingleInstance()
    {
        var instance = Interlocked.Exchange(ref _singleInstance, null);
        if (instance is null) return;
        instance.CommandRequested -= OnCommandRequested;
        instance.Dispose();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdownStarted = true;
#if DEBUG
        _development?.Dispose();
#endif
        try { DisposeSingleInstance(); }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            DispatcherUnhandledException -= OnUnhandledException;
            SessionEnding -= OnSessionEnding;
            _log?.Info($"Shutdown with exit code {e.ApplicationExitCode}.");
            if (_theme is not null)
            {
                _theme.ThemeChanged -= OnThemeChanged;
                _theme.Dispose();
                _theme = null;
            }
            _log?.Dispose();
            LogService.Current = null;
            base.OnExit(e);
        }
    }

    private static string GetVersion() => typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
}
