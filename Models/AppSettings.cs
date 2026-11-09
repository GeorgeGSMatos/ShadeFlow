namespace ShadeFlow.Models;

public sealed class AppSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; }
    public Dictionary<string, DisplaySettings>? Displays { get; set; } = new();
    public ScheduleSettings? Schedule { get; set; } = new();
    public Preferences? Preferences { get; set; } = new();
    public List<BrightnessPreset>? Presets { get; set; } = [];
}

public sealed class DisplaySettings
{
    public string? Alias { get; set; }
}

public sealed class ScheduleSettings
{
    public bool Enabled { get; set; }
    public List<ScheduleEntrySettings>? Entries { get; set; } = [];
}

public sealed class ScheduleEntrySettings
{
    public string Time { get; set; } = string.Empty;
    public int Brightness { get; set; }
}
