namespace ShadeFlow.Services;

public enum LaunchView { QuickPanel, Settings, Hidden }

public sealed record LaunchOptions(bool AutomaticStartup, bool SafeMode, bool ShowSettings,
    IReadOnlyList<string> UnknownArguments)
{
    public LaunchView InitialView => AutomaticStartup ? LaunchView.Hidden
        : SafeMode || ShowSettings ? LaunchView.Settings : LaunchView.QuickPanel;

    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        var startup = false;
        var safeMode = false;
        var settings = false;
        var unknown = new List<string>();
        foreach (var argument in args)
        {
            if (string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase)) startup = true;
            else if (string.Equals(argument, "--safe-mode", StringComparison.OrdinalIgnoreCase)) safeMode = true;
            else if (string.Equals(argument, "--settings", StringComparison.OrdinalIgnoreCase)) settings = true;
            else unknown.Add(argument);
        }
        return new LaunchOptions(startup, safeMode, settings, unknown);
    }
}
