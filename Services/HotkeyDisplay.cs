using System.Windows.Input;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public static class HotkeyDisplay
{
    public static string Modifiers(uint modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        return string.Join(" + ", parts);
    }

    public static string Format(HotkeyBinding binding)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)binding.VirtualKey);
        var name = binding.VirtualKey is >= 0x30 and <= 0x39 ? ((char)binding.VirtualKey).ToString() : key switch
        {
            Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→",
            Key.Return => "Enter", Key.Space => "Espaço", Key.Back => "Backspace", Key.Delete => "Del",
            Key.OemPlus => "Mais", Key.OemMinus => "Menos",
            _ => key.ToString()
        };
        var modifiers = Modifiers(binding.Modifiers);
        return modifiers.Length == 0 ? name : modifiers + " + " + name;
    }
}
