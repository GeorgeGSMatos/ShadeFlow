using System.IO;
using Microsoft.Win32;

namespace ShadeFlow.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShadeFlow";

    private static string Command
    {
        get
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable) ||
                !string.Equals(Path.GetFileName(executable), "ShadeFlow.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Execute ShadeFlow.exe para configurar o início com Windows.");
            return $"\"{executable}\" --startup";
        }
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        var command = Command;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
