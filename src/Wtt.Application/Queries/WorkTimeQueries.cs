using Wtt.Application.Ports;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;

namespace Wtt.Application.Queries;

public sealed class WorkTimeQueries(
    PresenceReader presence,
    ICoverageLog coverage,
    ILunchRules rules,
    IWorkdayCalculator calculator,
    TimeProvider clock) : IWorkTimeQueries
{
    private static readonly TimeSpan RecordingGrace = TimeSpan.FromMinutes(2);

    public async Task<WorkWeek> GetWeekAsync(DateOnly monday)
    {
        var timeline = await presence.AroundAsync(monday, monday.AddDays(6));
        var days = new List<Workday>();
        foreach (var date in WorkWeek.DaysOf(monday))
        {
            days.Add(calculator.Calculate(date, timeline, await rules.InForceOnAsync(date)));
        }

        return WorkWeek.Of(monday, days);
    }

    public async Task<Workday> GetDayAsync(DateOnly date)
    {
        var timeline = await presence.AroundAsync(date, date);
        return calculator.Calculate(date, timeline, await rules.InForceOnAsync(date));
    }

    public async Task<RecordingStatus> GetRecordingStatusAsync()
    {
        var now = clock.GetUtcNow();
        var spans = await coverage.ReadAsync(new Interval(DateTimeOffset.MinValue, now.AddMinutes(1)));
        if (spans.Count == 0)
        {
            return new RecordingStatus(IsRecording: false, LastRecorded: null);
        }

        var lastRecorded = spans.Max(span => span.To);
        return new RecordingStatus(now - lastRecorded <= RecordingGrace, lastRecorded);
    }
}
