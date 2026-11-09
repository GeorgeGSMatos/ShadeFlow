using System.Windows;
using System.Windows.Controls;

namespace ShadeFlow.Services;

public enum InterfaceTone { Neutral, Automatic, Manual, Attention, Success, Danger }

public static class InterfacePresentation
{
    public static string BrushKey(InterfaceTone tone) => tone switch
    {
        InterfaceTone.Neutral => "ShadeMutedTextBrush",
        InterfaceTone.Automatic => "ShadeAccentBrush",
        InterfaceTone.Manual => "ShadeManualBrush",
        InterfaceTone.Attention => "ShadeWarningBrush",
        InterfaceTone.Success => "ShadeSuccessBrush",
        InterfaceTone.Danger => "ShadeDangerBrush",
        _ => throw new ArgumentOutOfRangeException(nameof(tone))
    };

    public static string Glyph(InterfaceTone tone) => tone switch
    {
        InterfaceTone.Automatic => AppIcons.Play,
        InterfaceTone.Manual => AppIcons.Pause,
        InterfaceTone.Attention or InterfaceTone.Danger => AppIcons.Warning,
        InterfaceTone.Success => AppIcons.Check,
        _ => AppIcons.Info
    };

    public static void Apply(TextBlock text, InterfaceTone tone, TextBlock? icon = null)
    {
        text.SetResourceReference(TextBlock.ForegroundProperty, BrushKey(tone));
        if (icon is null) return;
        icon.Text = Glyph(tone);
        icon.SetResourceReference(TextBlock.ForegroundProperty, BrushKey(tone));
    }
}
