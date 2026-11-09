using System.Globalization;

namespace ShadeFlow.Models;

public sealed record ScheduleEntry(TimeOnly Time, int Brightness)
{
    public string TimeText => Time.ToString("HH:mm", CultureInfo.InvariantCulture);
}

public sealed record ScheduleConfiguration(bool Enabled, IReadOnlyList<ScheduleEntry> Entries);

public sealed record ScheduledOccurrence(ScheduleEntry Entry, DateTime OccursAt);

public sealed record SchedulePlan(
    ScheduledOccurrence CurrentOccurrence,
    ScheduledOccurrence NextOccurrence);

public sealed record ScheduleSnapshot(
    bool Enabled,
    ScheduleEntry? CurrentEntry,
    ScheduledOccurrence? NextOccurrence);

public sealed record ScheduleUpdate(
    long Generation,
    ScheduleSnapshot Snapshot,
    int? Brightness,
    ScheduleUpdateReason Reason = ScheduleUpdateReason.Normal);

public enum ScheduleUpdateReason { Normal, Resume, TimeChanged }
