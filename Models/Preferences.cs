using System.Text.Json.Serialization;

namespace ShadeFlow.Models;

public enum HotkeyAction { IncreaseBrightness, DecreaseBrightness, RestoreBrightness, ResumeAutomation }

// Native MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8. NoRepeat is added at registration.
public sealed record HotkeyBinding(uint Modifiers, uint VirtualKey)
{
    [JsonIgnore]
    public bool IsValid => Modifiers <= 15 && VirtualKey is >= 8 and <= 254 &&
        VirtualKey is not (16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165);
}

public sealed record HotkeySettings
{
    public HotkeyBinding? IncreaseBrightness { get; init; } = new(3, 0x26);
    public HotkeyBinding? DecreaseBrightness { get; init; } = new(3, 0x28);
    public HotkeyBinding? RestoreBrightness { get; init; } = new(3, 0x30);
    public HotkeyBinding? ResumeAutomation { get; init; } = new(3, 0x41);

    public Dictionary<HotkeyAction, HotkeyBinding> Bindings() => new Dictionary<HotkeyAction, HotkeyBinding?>
    {
        [HotkeyAction.IncreaseBrightness] = IncreaseBrightness,
        [HotkeyAction.DecreaseBrightness] = DecreaseBrightness,
        [HotkeyAction.RestoreBrightness] = RestoreBrightness,
        [HotkeyAction.ResumeAutomation] = ResumeAutomation
    }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!);

    public bool IsValid()
    {
        var values = Bindings().Values.ToArray();
        return values.All(binding => binding.IsValid) && values.Distinct().Count() == values.Length;
    }
}

public sealed record Preferences
{
    public int TransitionMilliseconds { get; init; } = 2000;
    public bool LinkDisplays { get; init; }
    public bool StartWithWindows { get; init; }
    public int BrightnessStep { get; init; } = 5;
    public HotkeySettings Hotkeys { get; init; } = new();

    public Preferences Normalize() => this with
    {
        TransitionMilliseconds = TransitionMilliseconds is 0 or 500 or 2000 or 5000 or 15000 or 30000
            ? TransitionMilliseconds : 2000,
        BrightnessStep = BrightnessStep is 1 or 2 or 5 or 10 ? BrightnessStep : 5,
        Hotkeys = Hotkeys?.IsValid() == true ? Hotkeys : new HotkeySettings()
    };
}
