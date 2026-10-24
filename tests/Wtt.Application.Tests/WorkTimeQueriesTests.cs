using Wtt.Application.Queries;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Rules;
using Wtt.Domain.Workdays;
using static Wtt.Application.Tests.Facts;

namespace Wtt.Application.Tests;

public sealed class WorkTimeQueriesTests
{
    private readonly InMemoryFactJournal journal = new();
    private readonly InMemoryCoverageLog coverage = new();
    private readonly FakeClock clock = new(At("2026-10-24 15:00"));

    [Fact]
    public async Task AC_1_11_the_week_of_monday_2026_10_19_lists_monday_to_sunday_with_the_sum_as_total()
    {
        await journal.AppendAsync([
            .. Use("2026-10-19 08:00", "2026-10-19 12:00"), .. Use("2026-10-19 12:30", "2026-10-19 17:00"),
            .. Use("2026-10-21 07:45", "2026-10-21 16:10"),
            .. Use("2026-10-23 09:00", "2026-10-23 11:00"),
        ]);

        var week = await Queries().GetWeekAsync(new DateOnly(2026, 10, 19));

        Assert.Equal(Enumerable.Range(19, 7).Select(day => new DateOnly(2026, 10, day)), week.Days.Select(day => day.Date));
        Assert.Equal(week.Days.Aggregate(TimeSpan.Zero, (sum, day) => sum + day.Total), week.Total);
        Assert.Equal(new TimeSpan(8, 30, 0) + new TimeSpan(7, 55, 0) + new TimeSpan(2, 0, 0), week.Total);
    }

    [Fact]
    public async Task Today_while_in_use_runs_until_now_and_later_days_are_empty()
    {
        await journal.AppendAsync([Unlock("2026-10-24 08:00")]);

        var week = await Queries().GetWeekAsync(new DateOnly(2026, 10, 19));

        var saturday = week.Days[5];
        Assert.Equal(At("2026-10-24 15:00"), saturday.End);
        Assert.Equal(TimeSpan.FromHours(2), saturday.End!.Value.Offset);
        Assert.False(week.Days[6].HasUse);
    }

    [Fact]
    public async Task Work_carried_over_from_sunday_starts_monday_at_midnight()
    {
        await journal.AppendAsync([.. Use("2026-10-18 23:00", "2026-10-19 00:30"), .. Use("2026-10-19 08:00", "2026-10-19 12:00")]);

        var monday = await Queries().GetDayAsync(new DateOnly(2026, 10, 19));

        Assert.Equal(At("2026-10-19 00:00"), monday.Start);
        Assert.NotNull(monday.NightPause);
    }

    [Fact]
    public async Task The_oldest_winlogon_fact_in_the_journal_bounds_the_two_minute_test_even_for_a_quiet_week()
    {
        await journal.AppendAsync([
            .. Use("2026-09-01 08:00", "2026-09-01 17:00"),
            StandbyExit("2026-10-20 08:00", WakeReason.InputKeyboard), StandbyEnter("2026-10-20 08:30"),
        ]);

        var tuesday = await Queries().GetDayAsync(new DateOnly(2026, 10, 20));

        Assert.False(tuesday.HasUse);
    }

    [Fact]
    public async Task Recording_status_reports_the_end_of_the_latest_coverage_span()
    {
        coverage.Spans.Add(new CoverageSpan(At("2026-10-24 08:00"), At("2026-10-24 14:59")));

        var status = await Queries().GetRecordingStatusAsync();

        Assert.True(status.IsRecording);
        Assert.Equal(At("2026-10-24 14:59"), status.LastRecorded);
    }

    [Fact]
    public async Task Recording_status_says_not_recording_when_coverage_is_stale_or_missing()
    {
        Assert.Equal(new RecordingStatus(false, null), await Queries().GetRecordingStatusAsync());

        coverage.Spans.Add(new CoverageSpan(At("2026-10-24 08:00"), At("2026-10-24 14:50")));
        Assert.False((await Queries().GetRecordingStatusAsync()).IsRecording);
    }

    [Fact]
    public async Task AC_1_12_the_listing_holds_every_day_from_the_oldest_kept_fact_to_today()
    {
        await journal.AppendAsync([.. Use("2026-09-09 08:00", "2026-09-09 17:00"), .. Use("2026-10-22 08:00", "2026-10-22 17:00")]);

        var weeks = await new WeekListing(journal, Queries(), clock).AllWeeksAsync();

        var days = weeks.SelectMany(week => week.Days).Select(day => day.Date).ToList();
        for (var date = new DateOnly(2026, 9, 9); date <= new DateOnly(2026, 10, 24); date = date.AddDays(1))
        {
            Assert.Contains(date, days);
        }

        Assert.Equal(new DateOnly(2026, 9, 7), weeks[0].Monday);
        Assert.Equal(new DateOnly(2026, 10, 19), weeks[^1].Monday);
    }

    [Fact]
    public async Task Without_any_fact_the_listing_is_empty()
    {
        Assert.Empty(await new WeekListing(journal, Queries(), clock).AllWeeksAsync());
    }

    private WorkTimeQueries Queries() => new(
        new PresenceReader(journal, coverage, new PresenceBuilder(), clock),
        coverage,
        new FixedLunchRules(Rule),
        new WorkdayCalculator(),
        clock);

    internal static LunchRule Rule { get; } = new(
        DateOnly.MinValue,
        new Domain.Time.TimeRange(new TimeOnly(12, 0), new TimeOnly(13, 30)),
        [new BreakTier(new TimeSpan(5, 30, 0), TimeSpan.FromMinutes(15)), new BreakTier(new TimeSpan(7, 0, 0), TimeSpan.FromMinutes(30)), new BreakTier(new TimeSpan(9, 0, 0), TimeSpan.FromHours(1))]);
}
