using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;
using System.Windows.Interop;

namespace ShadeFlow.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _open;
    private readonly Forms.ToolStripMenuItem _restore;
    private readonly Forms.ToolStripMenuItem _exit;
    private readonly Forms.ToolStripMenuItem _increase;
    private readonly Forms.ToolStripMenuItem _decrease;
    private readonly Forms.ToolStripMenuItem _resume;
    private readonly Forms.ToolStripMenuItem _automation;
    private HwndSource? _source;
    private readonly uint _taskbarCreatedMessage;
    private bool _disposed;

    public TrayService()
    {
        var title = new Forms.ToolStripMenuItem("ShadeFlow") { Enabled = false };
        _open = new Forms.ToolStripMenuItem("Abrir configurações");
        _restore = new Forms.ToolStripMenuItem("Restaurar brilho para 100%");
        _exit = new Forms.ToolStripMenuItem("Sair");
        _increase = new Forms.ToolStripMenuItem("Aumentar brilho");
        _decrease = new Forms.ToolStripMenuItem("Diminuir brilho");
        _resume = new Forms.ToolStripMenuItem("Retomar automação");
        _automation = new Forms.ToolStripMenuItem("Ativar automação");

        _open.Click += OnOpenClick;
        _restore.Click += OnRestoreClick;
        _exit.Click += OnExitClick;
        _increase.Click += OnIncreaseClick;
        _decrease.Click += OnDecreaseClick;
        _resume.Click += OnResumeClick;
        _automation.Click += OnAutomationClick;

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.AddRange([
            title,
            new Forms.ToolStripSeparator(),
            _open,
            _increase,
            _decrease,
            _restore,
            new Forms.ToolStripSeparator(),
            _resume,
            _automation,
            new Forms.ToolStripSeparator(),
            _exit
        ]);

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = LoadShadeFlowIcon(),
            Text = "ShadeFlow\nSoftware Display Dimming"
        };
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        try
        {
            _notifyIcon.Visible = true;
            _notifyIcon.MouseClick += OnMouseClick;
            _notifyIcon.MouseDoubleClick += OnMouseDoubleClick;
        }
        catch
        {
            _notifyIcon.Dispose();
            _menu.Dispose();
            throw;
        }
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? QuickPanelRequested;
    public event EventHandler? RestoreBrightnessRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? IncreaseRequested;
    public event EventHandler? DecreaseRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? ToggleAutomationRequested;

    public event EventHandler? Recreated;

    private static Icon LoadShadeFlowIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ShadeFlow.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    public void AttachToWindow(HwndSource source)
    {
        if (_disposed) return;
        if (ReferenceEquals(_source, source)) return;
        _source?.RemoveHook(WindowMessage);
        _source = source;
        _source.AddHook(WindowMessage);
    }

    public void UpdateAutomation(bool enabled, bool canResume)
    {
        if (_disposed) return;
        _automation.Text = enabled ? "Desativar automação" : "Ativar automação";
        _resume.Enabled = canResume;
    }

    private void OnIncreaseClick(object? sender, EventArgs e) => IncreaseRequested?.Invoke(this, EventArgs.Empty);
    private void OnDecreaseClick(object? sender, EventArgs e) => DecreaseRequested?.Invoke(this, EventArgs.Empty);
    private void OnResumeClick(object? sender, EventArgs e) => ResumeRequested?.Invoke(this, EventArgs.Empty);
    private void OnAutomationClick(object? sender, EventArgs e) => ToggleAutomationRequested?.Invoke(this, EventArgs.Empty);

    private void OnOpenClick(object? sender, EventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);
    private void OnRestoreClick(object? sender, EventArgs e) =>
        RestoreBrightnessRequested?.Invoke(this, EventArgs.Empty);
    private void OnExitClick(object? sender, EventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void OnMouseDoubleClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            QuickPanelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            QuickPanelRequested?.Invoke(this, EventArgs.Empty);
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (!_disposed && _taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage)
        {
            try
            {
                // Explorer discards notification-area icons on restart; toggling visibility
                // causes NotifyIcon to register the existing icon/menu again without duplicates.
                _notifyIcon.Visible = false;
                _notifyIcon.Visible = true;
                Recreated?.Invoke(this, EventArgs.Empty);
            }
            catch { /* Tray recovery remains best effort. */ }
        }
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _source?.RemoveHook(WindowMessage);
        _source = null;

        _notifyIcon.MouseDoubleClick -= OnMouseDoubleClick;
        _notifyIcon.MouseClick -= OnMouseClick;
        _open.Click -= OnOpenClick;
        _restore.Click -= OnRestoreClick;
        _exit.Click -= OnExitClick;
        _increase.Click -= OnIncreaseClick;
        _decrease.Click -= OnDecreaseClick;
        _resume.Click -= OnResumeClick;
        _automation.Click -= OnAutomationClick;
        try { _notifyIcon.Visible = false; }
        finally
        {
            try { _notifyIcon.Dispose(); }
            finally { _menu.Dispose(); }
        }
    }
}
