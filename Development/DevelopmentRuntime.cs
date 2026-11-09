#if DEBUG
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;

namespace ShadeFlow.Development;

/// <summary>Debug-only, local-file control channel. Never interrupts an operation or an editor.</summary>
public sealed class DevelopmentRuntime : IDisposable
{
    private readonly MainWindow _window;
    private readonly DevelopmentOptions _options;
    private readonly Action _theme;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private string _handled = "";
    private JsonNode? _closingRequest;

    public DevelopmentRuntime(MainWindow window, DevelopmentOptions options, Action theme)
    {
        _window = window; _options = options; _theme = theme;
        Directory.CreateDirectory(options.ControlDirectory);
        _timer.Tick += Poll;
        _window.Closed += Closed;
    }

    public void Start()
    {
        _window.EnableDevelopment(_options.Hidden);
        if (File.Exists(_options.StateFile))
        {
            try
            {
                var state = JsonSerializer.Deserialize<DevelopmentViewState>(File.ReadAllText(_options.StateFile));
                if (state is not null) _window.RestoreDevelopmentState(state);
            }
            catch (Exception error) { _window.DevelopmentMessage("Não foi possível restaurar a tela: " + error.Message); }
        }
        if (_options.Hidden) _window.EnableDevelopment(true);
        Write("ready.json", new { revision = _options.Revision, pid = Environment.ProcessId, status = _window.DevelopmentStatus() });
        _timer.Start();
    }

    private void Poll(object? sender, EventArgs args)
    {
        if (_closingRequest is not null && _window.CanApplyDevelopmentChange)
        {
            Reply(_closingRequest, false, "O encerramento não foi concluído. Confira a janela do ShadeFlow.");
            _closingRequest = null;
        }
        JsonNode? request;
        try
        {
            var file = Path.Combine(_options.ControlDirectory, "command.json");
            if (!File.Exists(file)) return;
            request = JsonNode.Parse(File.ReadAllText(file));
        }
        catch (IOException) { return; }
        catch (JsonException) { return; }
        if (request is not JsonObject) return;
        if (Read(request, "revision") != _options.Revision) return;
        var id = Read(request, "id");
        if (id.Length == 0 || id == _handled) return;
        var command = Read(request, "command");
        if (command == "status") { _handled = id; Reply(request, true); return; }
        if (!_window.CanApplyDevelopmentChange) return;
        _handled = id;
        try
        {
            switch (command)
            {
                case "styles":
                    DevelopmentStyles.Reload(_options.SourceRoot, _theme);
                    Reply(request, true); break;
                case "restart":
                case "stop":
                    WriteState(); _closingRequest = request;
                    _window.ExitApplication(); break;
                default: Reply(request, false, "Comando de desenvolvimento desconhecido."); break;
            }
        }
        catch (Exception error)
        {
            _closingRequest = null;
            _window.DevelopmentMessage("Atualização não aplicada. Confira o terminal.");
            Reply(request, false, error.Message);
            _timer.Start();
        }
    }

    private void WriteState()
    {
        Directory.CreateDirectory(_options.ProfileDirectory);
        AtomicWrite(_options.StateFile, _window.CaptureDevelopmentState());
    }

    private void Closed(object? sender, EventArgs args)
    {
        try { WriteState(); if (_closingRequest is not null) Reply(_closingRequest, true); }
        finally { Dispose(); }
    }

    private static string Read(JsonNode? node, string key) => node?[key]?.ToString() ?? "";

    private void Reply(JsonNode? request, bool ok, string? error = null) =>
        Write("reply.json", new { id = Read(request, "id"), ok, error, status = _window.DevelopmentStatus() });

    private void Write(string name, object value) => AtomicWrite(Path.Combine(_options.ControlDirectory, name), value);
    private static void AtomicWrite(string path, object value)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value)); File.Move(temporary, path, true);
    }

    public void Dispose() { _timer.Stop(); _timer.Tick -= Poll; _window.Closed -= Closed; }
}
#endif
