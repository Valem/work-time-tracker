using Wtt.Domain.Comparison;
using Wtt.Domain.Facts;
using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

public sealed class StartAgreementTests
{
    [Fact]
    public void Counts_workdays_whose_two_minute_test_start_lies_within_the_tolerance()
    {
        var unlockBased = new Scenario()
            .Use("2026-10-19 08:00", "2026-10-19 17:00")
            .Use("2026-10-20 08:00", "2026-10-20 17:00")
            .Use("2026-10-21 08:00", "2026-10-21 17:00");
        var testBased = new Scenario()
            .StandbyExit("2026-10-19 07:55", WakeReason.InputKeyboard).StandbyEnter("2026-10-19 17:00")
            .StandbyExit("2026-10-20 07:54", WakeReason.InputKeyboard).StandbyEnter("2026-10-20 17:00")
            .WinlogonHistoryFrom(null);
        var dates = new[] { "2026-10-19", "2026-10-20", "2026-10-21", "2026-10-22" };

        var result = StartAgreement.Compare(
            dates.Select(date => unlockBased.Day(date, Ac)),
            dates.Select(date => testBased.Day(date, Ac)),
            TimeSpan.FromMinutes(5));

        Assert.Equal(3, result.Workdays);
        Assert.Equal(1, result.Agreeing);
        Assert.Equal([true, false, false], result.Days.Select(day => day.Agrees));
        Assert.Null(result.Days[2].TestStart);
        Assert.Equal(1.0 / 3, result.Share);
    }
}
