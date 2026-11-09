using ShadeFlow.Models;

namespace ShadeFlow.Services;

public static class ScheduleCalculator
{
    public static SchedulePlan? Calculate(IEnumerable<ScheduleEntry> entries, DateTime now)
    {
        var ordered = entries.OrderBy(entry => entry.Time).ToArray();
        if (ordered.Length == 0) return null;

        var current = ordered.LastOrDefault(entry => entry.Time <= TimeOnly.FromDateTime(now));
        var currentDate = now.Date;
        if (current is null)
        {
            current = ordered[^1];
            currentDate = currentDate.AddDays(-1);
        }

        var next = ordered.FirstOrDefault(entry => entry.Time > TimeOnly.FromDateTime(now));
        var nextDate = now.Date;
        if (next is null)
        {
            next = ordered[0];
            nextDate = nextDate.AddDays(1);
        }

        return new SchedulePlan(
            new ScheduledOccurrence(current, currentDate.Add(current.Time.ToTimeSpan())),
            new ScheduledOccurrence(next, nextDate.Add(next.Time.ToTimeSpan())));
    }
}
