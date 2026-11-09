using System.Runtime.InteropServices;
using System.Text;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

public static class DiagnosticsService
{
    public static string Build(string version, string settingsPath, string logsPath,
        IEnumerable<DisplayState> displays, ScheduleSnapshot schedule, bool overrideActive,
        Preferences preferences, bool safeMode)
    {
        var builder = new StringBuilder()
            .AppendLine($"ShadeFlow: {version}")
            .AppendLine($"OS: {RuntimeInformation.OSDescription}")
            .AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}")
            .AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}")
            .AppendLine($"Settings: {settingsPath}")
            .AppendLine($"Logs: {logsPath}")
            .AppendLine($"Safe mode: {(safeMode ? "Yes" : "No")}")
            .AppendLine($"Displays: {displays.Count()}");
        foreach (var state in displays)
        {
            var display = state.Display;
            builder.AppendLine("Display:")
                .AppendLine($"Alias: {state.Alias ?? "(none)"}")
                .AppendLine($"Friendly name: {display.FriendlyName ?? "(unavailable)"}")
                .AppendLine($"Device name: {display.DeviceName}")
                .AppendLine($"Resolution: {display.Resolution.Width}x{display.Resolution.Height}")
                .AppendLine($"Primary: {display.Primary}")
                .AppendLine($"StableId: {TruncateStableId(display.StableId)}");
        }
        builder.AppendLine($"Automation: {(schedule.Enabled ? "Enabled" : "Disabled")}")
            .AppendLine($"Override: {(overrideActive ? "Yes" : "No")}")
            .AppendLine($"Transition: {preferences.TransitionMilliseconds} ms")
            .AppendLine($"Linked displays: {preferences.LinkDisplays}");
        return builder.ToString();
    }

    internal static string TruncateStableId(string stableId) => stableId.Length <= 18
        ? stableId : stableId[..18] + "…";
}
