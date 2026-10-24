using Wtt.Application.Reports;
using Wtt.Domain.Comparison;
using Wtt.Domain.Presence;
using Wtt.Domain.Workdays;

namespace Wtt.Cli.Tests;

public sealed class PrinterTests
{
    private static readonly TimeSpan Local = TimeSpan.FromHours(2);

    [Fact]
    public void A_day_ending_at_the_next_midnight_reads_24_00_and_week_totals_go_past_24_hours()
    {
        var monday = new DateOnly(2026, 10, 19);
        var days = Enumerable.Range(0, 7).Select(i => Day(monday.AddDays(i), 8, i == 0 ? 24 : 17)).ToList();
        var output = new StringWriter();

        WeekPrinter.Print([WorkWeek.Of(monday, days)], output);

        var text = output.ToString();
        Assert.Contains("Mon 2026-10-19   08:00  24:00   0:00   16:00", text, StringComparison.Ordinal);
        Assert.Contains("Week total                             70:00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_start_test_lists_each_workday_and_the_share()
    {
        var day = new DateOnly(2026, 10, 19);
        var unlock = new DateTimeOffset(2026, 10, 19, 8, 0, 0, Local);
        var agreement = new StartAgreementResult(
            [new StartComparison(day, unlock, unlock.AddMinutes(-3), true), new StartComparison(day.AddDays(1), unlock.AddDays(1), null, false)],
            TimeSpan.FromMinutes(5));
        var output = new StringWriter();

        var unmapped = new Dictionary<string, int> { ["Unmapped:KernelPower 53"] = 3 };

        StartTestPrinter.Print(new StartTestResult(day, day.AddDays(1), agreement, unmapped), output);

        var text = output.ToString();
        Assert.Contains("Mon 2026-10-19    08:00   07:57    -3 min", text, StringComparison.Ordinal);
        Assert.Contains("Tue 2026-10-20    08:00       -      none  outside", text, StringComparison.Ordinal);
        Assert.Contains("Workdays: 2   within 5 min: 1   share: 50.0 %", text, StringComparison.Ordinal);
        Assert.Contains("       3  Unmapped:KernelPower 53", text, StringComparison.Ordinal);
    }

    private static Workday Day(DateOnly date, int startHour, int endHour)
    {
        var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(startHour, 0)), Local);
        var end = start.AddHours(endHour - startHour);
        var length = end - start;
        return new Workday(date, [new WorkBlock(start, end)], [], null, LunchDeduction.None, length, length, Precision.Exact);
    }
}
