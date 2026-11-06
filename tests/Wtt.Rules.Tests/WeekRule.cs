using System.Globalization;
using Wtt.Application.Queries;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure.Storage;
using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class WeekRule
{
    private static readonly DateOnly Monday = new(2026, 10, 12);

    [Fact]
    public async Task BR_010_week_runs_monday_to_sunday_and_totals_its_days()
    {
        var rule = new RuleScenarios();
        using var file = new TempDatabase();
        var clock = new FakeClock(At("2026-10-21 12:00"));
        var database = file.Open(clock);
        var journal = new SqliteFactJournal(database);
        var coverage = new SqliteCoverageLog(database, clock);
        await journal.AppendAsync([
            .. Use("2026-10-11 09:00", "2026-10-11 11:00"),
            .. Use("2026-10-12 08:00", "2026-10-12 12:00"), .. Use("2026-10-12 12:30", "2026-10-12 17:00"),
            .. Use("2026-10-14 07:45", "2026-10-14 16:10"),
            .. Use("2026-10-17 09:00", "2026-10-17 11:00"),
            .. Use("2026-10-18 10:00", "2026-10-18 11:00"),
            .. Use("2026-10-19 08:00", "2026-10-19 09:00"),
        ]);
        var queries = new WorkTimeQueries(
            new PresenceReader(journal, coverage, new PresenceBuilder(), clock),
            coverage,
            new SqliteLunchRules(database, clock),
            new WorkdayCalculator(),
            clock);

        var week = await queries.GetWeekAsync(Monday);

        rule.Check("the week of Monday 2026-10-12 holds Monday 12 to Sunday 18, in order", () => Assert.Equal(
            ["Monday 2026-10-12", "Tuesday 2026-10-13", "Wednesday 2026-10-14", "Thursday 2026-10-15", "Friday 2026-10-16", "Saturday 2026-10-17", "Sunday 2026-10-18"],
            week.Days.Select(day => $"{day.Date.DayOfWeek} {day.Date:yyyy-MM-dd}")));

        rule.Check("Saturday's and Sunday's use belong to the week, the Sunday before and the Monday after do not", () => Assert.Equal(
            [Hm("8:30"), TimeSpan.Zero, Hm("7:55"), TimeSpan.Zero, TimeSpan.Zero, Hm("2:00"), Hm("1:00")],
            week.Days.Select(day => day.Total)));

        rule.Check("the week total is the sum of the day totals", () =>
        {
            Assert.Equal(week.Days.Aggregate(TimeSpan.Zero, (sum, day) => sum + day.Total), week.Total);
            Assert.Equal(Hm("19:25"), week.Total);
        });

        rule.Check("every date from Monday to Sunday belongs to the week starting that Monday", () =>
        {
            foreach (var date in WorkWeek.DaysOf(Monday))
            {
                Assert.Equal((date, Monday), (date, WorkWeek.MondayOf(date)));
            }

            Assert.Equal(new DateOnly(2026, 10, 5), WorkWeek.MondayOf(new DateOnly(2026, 10, 11)));
            Assert.Equal(new DateOnly(2026, 10, 19), WorkWeek.MondayOf(new DateOnly(2026, 10, 19)));
        });

        rule.AssertAllHold();
    }

    private static DateTimeOffset At(string localTime) =>
        new(DateTime.ParseExact(localTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), TimeSpan.FromHours(2));

    private static ActivityFact[] Use(string from, string to) =>
    [
        new(At(from), FactKind.Unlock, FactSource.WinlogonHistory, $"synthetic#unlock@{from}"),
        new(At(to), FactKind.Lock, FactSource.WinlogonHistory, $"synthetic#lock@{to}"),
    ];
}
