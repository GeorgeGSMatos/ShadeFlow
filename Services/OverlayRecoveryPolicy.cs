using ShadeFlow.Models;

namespace ShadeFlow.Services;

// Pure decisions: smaller Z means nearer the viewer. Do not repair transparent overlays,
// windows on another monitor, or an unknown/stale window-order sample.
internal static class OverlayRecoveryPolicy
{
    internal static bool NeedsRecovery(int overlayZ, int obstructionZ, double opacity,
        DisplayBounds overlay, DisplayBounds obstruction) =>
        opacity > 0 && overlayZ >= 0 && obstructionZ >= 0 && obstructionZ < overlayZ &&
        overlay.Width > 0 && overlay.Height > 0 && obstruction.Width > 0 && obstruction.Height > 0 &&
        (long)overlay.X < (long)obstruction.X + obstruction.Width &&
        (long)obstruction.X < (long)overlay.X + overlay.Width &&
        (long)overlay.Y < (long)obstruction.Y + obstruction.Height &&
        (long)obstruction.Y < (long)overlay.Y + overlay.Height;
}

internal sealed class OverlayRecoveryBudget
{
    private readonly Queue<TimeSpan> _attempts = new();
    private TimeSpan? _lastAttempt;
    internal TimeSpan BlockedUntil { get; private set; }

    internal bool TryAcquire(TimeSpan now)
    {
        if (now < BlockedUntil || _lastAttempt is { } last && now - last < TimeSpan.FromSeconds(2)) return false;
        while (_attempts.TryPeek(out var first) && now - first >= TimeSpan.FromSeconds(30)) _attempts.Dequeue();
        if (_attempts.Count >= 3)
        {
            BlockedUntil = now + TimeSpan.FromSeconds(60);
            _attempts.Clear();
            return false;
        }
        _attempts.Enqueue(now);
        _lastAttempt = now;
        return true;
    }
}
