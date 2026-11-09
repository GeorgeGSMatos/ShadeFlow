using System.Text.Json.Serialization;

namespace ShadeFlow.Models;

public sealed record PresetIcon(string Id, string Name, string Glyph);

public sealed class BrightnessPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "game";
    public bool HoldUntilResume { get; set; }
    public Dictionary<string, double> Displays { get; set; } = new();

    public static IReadOnlyList<PresetIcon> Icons { get; } = new[]
    {
        new PresetIcon("game", "Jogo", "\uE7FC"), new("work", "Trabalho", "\uE821"),
        new("movie", "Filme", "\uE714"), new("night", "Noite", "\uE708"),
        new("reading", "Leitura", "\uE82D"), new("sun", "Dia", "\uE706"),
        new("code", "Código", "\uE943"), new("star", "Favorito", "\uE734")
    };

    [JsonIgnore] public string Glyph => Icons.FirstOrDefault(i => i.Id == Icon)?.Glyph ?? Icons[0].Glyph;
    [JsonIgnore] public string Description => $"{Name} · " +
        (HoldUntilResume ? "Até retomar automação" : "Até o próximo horário") + $" · {Displays.Count} monitor(es)";
    [JsonIgnore] public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Id.Length <= 80 &&
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 40 && Icons.Any(i => i.Id == Icon) &&
        Displays is { Count: > 0 and <= 32 } && Displays.All(p =>
            !string.IsNullOrWhiteSpace(p.Key) && p.Key.Length <= 1024 && double.IsFinite(p.Value) && p.Value is >= 0 and <= 100);

    public BrightnessPreset Copy() => new()
    {
        Id = Id, Name = Name, Icon = Icon, HoldUntilResume = HoldUntilResume,
        Displays = new Dictionary<string, double>(Displays)
    };
}

// Session-only state. Pausing automatic application never stops the scheduler's clock.
public sealed class PresetSession
{
    public string? Id { get; private set; }
    public string? Name { get; private set; }
    public bool Hold { get; private set; }
    public bool Active => Id is not null;
    public void Apply(BrightnessPreset preset) { Id = preset.Id; Name = preset.Name; Hold = preset.HoldUntilResume; }
    public void ManualAdjustment() { Id = null; Name = null; /* Explicit hold survives slider/restore. */ }
    public void Clear() { Id = null; Name = null; Hold = false; }
    public bool AcceptAutomaticAdjustment()
    {
        if (Hold) return false;
        Clear();
        return true;
    }
}
