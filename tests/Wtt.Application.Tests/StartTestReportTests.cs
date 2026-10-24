using Wtt.Application.Queries;
using Wtt.Application.Reports;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Workdays;
using static Wtt.Application.Tests.Facts;

namespace Wtt.Application.Tests;

public sealed class StartTestReportTests
{
    private readonly InMemoryFactJournal journal = new();
    private readonly FakeClock clock = new(At("2026-10-16 20:00"));

    [Fact]
    public async Task AC_1_7_reports_the_share_of_workdays_whose_two_minute_start_is_within_five_minutes_of_the_unlock()
    {
        await journal.AppendAsync([StandbyEnter("2026-10-04 23:00"), .. Use("2026-10-04 23:10", "2026-10-04 23:20")]);
        var wakeBeforeUnlock = new[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 6 };
        for (var i = 0; i < wakeBeforeUnlock.Length; i++)
        {
            var date = new DateOnly(2026, 10, 5).AddDays(i);
            var unlock = new DateTime(date, new TimeOnly(8, 0));
            var day = $"{date:yyyy-MM-dd}";
            await journal.AppendAsync([
                StandbyExit($"{unlock.AddMinutes(-wakeBeforeUnlock[i]):yyyy-MM-dd HH:mm}", WakeReason.InputKeyboard),
                .. Use($"{day} 08:00", $"{day} 16:55"),
                StandbyEnter($"{day} 17:00"),
            ]);
        }

        var result = await Report().RunAsync();

        Assert.Equal(new DateOnly(2026, 10, 5), result.From);
        Assert.Equal(new DateOnly(2026, 10, 16), result.To);
        Assert.Equal(10, result.Agreement.Workdays);
        Assert.Equal(9, result.Agreement.Agreeing);
        Assert.Equal(0.9, result.Agreement.Share);
    }

    [Fact]
    public async Task Without_both_logs_there_is_nothing_to_compare()
    {
        await journal.AppendAsync([.. Use("2026-10-05 08:00", "2026-10-05 17:00")]);

        var result = await Report().RunAsync();

        Assert.Null(result.From);
        Assert.Equal(0, result.Agreement.Workdays);
    }

    [Fact]
    public async Task CR_002_the_report_counts_wake_reasons_that_have_no_name()
    {
        await journal.AppendAsync([
            StandbyExit("2026-10-05 03:00", "Unmapped:KernelPower 53"),
            StandbyExit("2026-10-06 03:00", "Unmapped:KernelPower 53"),
            StandbyExit("2026-10-07 03:00", WakeReason.InputKeyboard),
        ]);

        var result = await Report().RunAsync();

        Assert.Equal(new Dictionary<string, int> { ["Unmapped:KernelPower 53"] = 2 }, result.UnmappedWakeReasons);
    }

    private StartTestReport Report()
    {
        var coverage = new InMemoryCoverageLog();
        return new StartTestReport(
            journal,
            new PresenceReader(journal, coverage, new PresenceBuilder(), clock),
            new FixedLunchRules(WorkTimeQueriesTests.Rule),
            new WorkdayCalculator(),
            clock);
    }
}
