using System.Globalization;
using System.Windows;
using ShadeFlow.Models;

namespace ShadeFlow.Views;

public partial class PresetDialog : Window
{
    public sealed class MonitorInput
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Value { get; set; } = "100";
    }
    private readonly BrightnessPreset _draft;
    private readonly List<MonitorInput> _inputs;
    private readonly HashSet<string> _otherNames;
    public BrightnessPreset? Preset { get; private set; }

    public PresetDialog(IEnumerable<DisplayState> displays, IEnumerable<BrightnessPreset> presets, BrightnessPreset? existing = null)
    {
        InitializeComponent();
        _draft = existing?.Copy() ?? new BrightnessPreset();
        _otherNames = presets.Where(p => p.Id != _draft.Id).Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _inputs = displays.Select(d => new MonitorInput
        {
            Id = d.Display.StableId, Name = d.DisplayName,
            Value = Math.Round(_draft.Displays.GetValueOrDefault(d.Display.StableId, d.Brightness)).ToString(CultureInfo.InvariantCulture)
        }).ToList();
        NameInput.Text = _draft.Name;
        IconChoice.ItemsSource = BrightnessPreset.Icons;
        IconChoice.SelectedValue = _draft.Icon;
        HoldToggle.IsChecked = _draft.HoldUntilResume;
        MonitorValues.ItemsSource = _inputs;
        Loaded += (_, _) => NameInput.Focus();
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var name = NameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || _otherNames.Contains(name))
        { ErrorText.Text = "Escolha um nome único para este preset."; return; }
        foreach (var input in _inputs)
        {
            if (!int.TryParse(input.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value is < 0 or > 100)
            { ErrorText.Text = "O brilho de cada monitor deve ser um inteiro entre 0 e 100."; return; }
            _draft.Displays[input.Id] = value;
        }
        _draft.Name = name;
        _draft.Icon = IconChoice.SelectedValue as string ?? "game";
        _draft.HoldUntilResume = HoldToggle.IsChecked == true;
        if (!_draft.IsValid) { ErrorText.Text = "Conecte pelo menos um monitor para criar um preset."; return; }
        Preset = _draft;
        DialogResult = true;
    }
}
