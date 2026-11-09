using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using ShadeFlow.Models;
using ShadeFlow.Views;

namespace ShadeFlow.Services;

public sealed class OverlayManager : IDisposable
{
    internal readonly record struct DiagnosticTarget(string DisplayId, nint Handle,
        DisplayBounds ExpectedBounds, double Opacity);
    private readonly Dictionary<string, OverlayWindow> _overlays = new();
    private readonly Dictionary<string, double> _brightnessByStableId = new();
    private bool _restoring;
    private readonly bool _previewOnly;

    public OverlayManager(bool previewOnly = false) => _previewOnly = previewOnly;
    public ObservableCollection<DisplayState> Displays { get; } = new();
    public event Action<string>? Error;

    public void Reconcile(IReadOnlyList<DisplayInfo> displays, double defaultBrightness = 100,
        bool preferDefaultForNewDisplay = false, Func<string, double?>? initialBrightnessForDisplay = null)
    {
        // Device notifications can concern unrelated USB devices. Ignore unchanged topology.
        if (displays.Count == Displays.Count && displays.All(d =>
                Displays.Any(s => s.Display == d)) && (_previewOnly || _overlays.Count == displays.Count))
            return;
        foreach (var current in Displays)
            _brightnessByStableId[current.Display.StableId] = current.Brightness;

        var ids = displays.Select(d => d.Id).ToHashSet();
        foreach (var state in Displays.Where(s => !ids.Contains(s.Display.Id)).ToArray())
        {
            RemoveOverlay(state.Display.Id);
            state.PropertyChanged -= OnBrightnessChanged;
            Displays.Remove(state);
        }
        foreach (var display in displays)
        {
            var state = Displays.FirstOrDefault(s => s.Display.Id == display.Id);
            if (state is not null &&
                state.Display.HasStableHardwareIdentity &&
                display.HasStableHardwareIdentity &&
                state.Display.StableId != display.StableId)
            {
                // Windows reassigned the same DISPLAYx to different known hardware.
                state.PropertyChanged -= OnBrightnessChanged;
                Displays.Remove(state);
                state = null;
            }
            if (state is null)
            {
                var initialBrightness = initialBrightnessForDisplay?.Invoke(display.StableId) ?? (!preferDefaultForNewDisplay &&
                    _brightnessByStableId.TryGetValue(display.StableId, out var cached)
                    ? cached
                    : defaultBrightness);
                state = new DisplayState(display, initialBrightness);
                state.PropertyChanged += OnBrightnessChanged;
                Displays.Add(state);
            }
            else state.Display = display;
            if (_previewOnly) continue;
            try
            {
                if (!_overlays.TryGetValue(display.Id, out var overlay))
                {
                    overlay = new OverlayWindow(display.Bounds);
                    overlay.UpdateFailed += FailSafe;
                    _overlays.Add(display.Id, overlay); // Track before Show so failed creation is cleaned up.
                    overlay.SetBrightness(state.Brightness);
                    overlay.Show();
                }
                overlay.Place(display.Bounds);
                overlay.SetBrightness(state.Brightness);
            }
            catch (Exception ex) { FailSafe(ex); return; }
        }
    }

    private void OnBrightnessChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DisplayState.Brightness) || sender is not DisplayState state) return;
        _brightnessByStableId[state.Display.StableId] = state.Brightness;
        if (_restoring || _previewOnly) return;
        try
        {
            if (!_overlays.TryGetValue(state.Display.Id, out var overlay))
            {
                overlay = new OverlayWindow(state.Display.Bounds);
                overlay.UpdateFailed += FailSafe;
                _overlays.Add(state.Display.Id, overlay);
                overlay.SetBrightness(state.Brightness);
                overlay.Show();
                overlay.Place(state.Display.Bounds);
            }
            overlay.SetBrightness(state.Brightness);
        }
        catch (Exception ex) { FailSafe(ex); }
    }

    public void RestoreAll()
    {
        InvalidateDisconnectedBrightness();
        _restoring = true;
        try
        {
            foreach (var pair in _overlays.ToArray())
            {
                try { pair.Value.SetBrightness(100); }
                catch (Exception ex) { Trace.TraceError("{0}", ex); RemoveOverlay(pair.Key); }
            }
            foreach (var state in Displays) state.Brightness = 100;
        }
        finally { _restoring = false; }
    }

    public void SetAllBrightness(double brightness)
    {
        var safeBrightness = double.IsFinite(brightness) ? Math.Clamp(brightness, 0, 100) : 100;
        foreach (var state in Displays)
            state.Brightness = safeBrightness;
    }

    // A global command supersedes overrides even for monitors currently unplugged.
    public void InvalidateDisconnectedBrightness() => _brightnessByStableId.Clear();

    public void CachePresetTargets(IReadOnlyDictionary<string, double> targets)
    {
        foreach (var pair in targets) _brightnessByStableId[pair.Key] = Math.Clamp(pair.Value, 0, 100);
    }

    internal IReadOnlyList<DiagnosticTarget> GetDiagnosticTargets() => _overlays
        .Select(pair => new DiagnosticTarget(pair.Key, pair.Value.NativeHandle,
            pair.Value.PhysicalBounds, pair.Value.Opacity))
        .ToArray();

    public void FailSafe(Exception ex)
    {
        Trace.TraceError("Overlay failure: {0}", ex);
        LogService.Current?.Error("Overlay failure; restoring all displays.", ex);
        RestoreAll();
        foreach (var id in _overlays.Keys.ToArray()) RemoveOverlay(id);
        Error?.Invoke("Falha no overlay. Todos os monitores foram restaurados para 100%.");
    }

    private void RemoveOverlay(string id)
    {
        if (!_overlays.Remove(id, out var overlay)) return;
        overlay.UpdateFailed -= FailSafe;
        try { overlay.Opacity = 0; }
        catch (Exception ex) { Trace.TraceError("{0}", ex); }
        try { overlay.Close(); }
        catch (Exception ex) { Trace.TraceError("{0}", ex); }
    }

    public void Dispose()
    {
        RestoreAll();
        foreach (var id in _overlays.Keys.ToArray()) RemoveOverlay(id);
        foreach (var state in Displays) state.PropertyChanged -= OnBrightnessChanged;
        Displays.Clear();
        _brightnessByStableId.Clear();
    }
}
