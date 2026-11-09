using ShadeFlow.Models;

namespace ShadeFlow.Services;

// Small testable transaction around RegisterHotKey. Unchanged registrations stay in place.
public sealed class HotkeyRegistrationSet : IDisposable
{
    private readonly Func<int, HotkeyBinding, bool> _register;
    private readonly Func<int, bool> _unregister;
    private readonly Dictionary<HotkeyAction, (int Id, HotkeyBinding Binding)> _active = new();
    private int _nextId = 0x5346;
    private bool _disposed;

    public HotkeyRegistrationSet(Func<int, HotkeyBinding, bool> register, Func<int, bool> unregister)
    {
        _register = register;
        _unregister = unregister;
    }

    public HotkeyAction? Resolve(int id) => _active
        .Where(pair => pair.Value.Id == id).Select(pair => (HotkeyAction?)pair.Key).FirstOrDefault();

    public HotkeySettings CurrentSettings => new()
    {
        IncreaseBrightness = Get(HotkeyAction.IncreaseBrightness),
        DecreaseBrightness = Get(HotkeyAction.DecreaseBrightness),
        RestoreBrightness = Get(HotkeyAction.RestoreBrightness),
        ResumeAutomation = Get(HotkeyAction.ResumeAutomation)
    };

    private HotkeyBinding? Get(HotkeyAction action) =>
        _active.TryGetValue(action, out var value) ? value.Binding : null;

    public bool TryReplace(HotkeySettings settings, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        error = null;
        if (!settings.IsValid())
        {
            error = "Atalhos inválidos ou duplicados. Escolha uma tecla principal diferente para cada ação.";
            return false;
        }
        var desired = settings.Bindings();
        var previous = _active.ToDictionary(pair => pair.Key, pair => pair.Value.Binding);
        if (Apply(desired)) return true;
        var restored = Apply(previous);
        error = restored
            ? "Não foi possível registrar o atalho. Ele pode estar em uso por outro programa. Atalhos anteriores mantidos."
            : "Falha ao registrar e recuperar um ou mais atalhos. Use Restaurar 100% na janela ou bandeja e revise os atalhos.";
        return false;
    }

    private bool Apply(Dictionary<HotkeyAction, HotkeyBinding> desired)
    {
        foreach (var pair in _active.ToArray())
        {
            if (desired.TryGetValue(pair.Key, out var binding) && binding == pair.Value.Binding) continue;
            if (!_unregister(pair.Value.Id)) return false;
            _active.Remove(pair.Key);
        }
        foreach (var pair in desired)
        {
            if (_active.ContainsKey(pair.Key)) continue;
            // Unique IDs also discard any queued WM_HOTKEY from an old configuration.
            if (_nextId > 0xbfff) return false;
            var id = _nextId++;
            if (!_register(id, pair.Value)) return false;
            _active.Add(pair.Key, (id, pair.Value));
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var registration in _active.Values) _unregister(registration.Id);
        _active.Clear();
    }
}
