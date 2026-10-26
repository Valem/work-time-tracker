using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

public sealed class EveningPauseTests
{
    [Fact]
    public void The_day_names_its_evening_pause_and_the_minutes_after_it()
    {
        var day = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 12:00")
            .Use("2026-10-20 13:00", "2026-10-20 17:30")
            .Use("2026-10-20 22:40", "2026-10-20 22:42");

        var workday = day.Day("2026-10-20", Ac);

        Assert.NotNull(workday.EveningPause);
        Assert.Equal(day.At("2026-10-20 17:30"), workday.EveningPause.Start);
        Assert.Equal(day.At("2026-10-20 22:40"), workday.EveningPause.End);
        Assert.Equal(Hm("0:02"), workday.EveningUse);
        Assert.Equal(Hm("9:32"), workday.Attendance);
        Assert.Contains(workday.EveningPause, workday.Pauses);
        Assert.Equal(day.At("2026-10-20 17:30"), Assert.Single(workday.Blocks).End);
    }

    [Fact]
    public void A_long_pause_that_is_not_the_last_stays_work()
    {
        var workday = new Scenario()
            .Use("2026-10-20 06:00", "2026-10-20 07:00")
            .Use("2026-10-20 09:30", "2026-10-20 12:00")
            .Use("2026-10-20 12:30", "2026-10-20 16:00")
            .Day("2026-10-20", Ac);

        Assert.Null(workday.EveningPause);
        Assert.Equal(Hm("10:00"), workday.Attendance);
    }

    [Fact]
    public void Only_the_last_of_two_long_afternoon_pauses_ends_the_day()
    {
        var day = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 12:00")
            .Use("2026-10-20 12:30", "2026-10-20 15:00")
            .Use("2026-10-20 18:00", "2026-10-20 19:00")
            .Use("2026-10-20 22:00", "2026-10-20 22:10");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 19:00"), workday.End);
        Assert.Equal(Hm("0:10"), workday.EveningUse);
        Assert.Equal(Hm("11:10"), workday.Attendance);
    }

    [Fact]
    public void A_night_pause_that_is_the_last_pause_is_never_an_evening_pause()
    {
        var day = new Scenario()
            .Use("2026-10-19 22:00", "2026-10-20 00:30")
            .Use("2026-10-20 08:00", "2026-10-20 11:00");

        var tuesday = day.Day("2026-10-20", Ac);

        Assert.NotNull(tuesday.NightPause);
        Assert.Null(tuesday.EveningPause);
        Assert.Equal(day.At("2026-10-20 11:00"), tuesday.End);
        Assert.Equal(Hm("3:30"), tuesday.Total);
    }

    [Fact]
    public void A_long_last_pause_before_the_lunch_window_is_a_morning_pause_and_stays_work()
    {
        var day = new Scenario()
            .Use("2026-10-20 05:00", "2026-10-20 05:05")
            .Use("2026-10-20 08:00", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Null(workday.EveningPause);
        Assert.Equal(day.At("2026-10-20 05:00"), workday.Start);
        Assert.Equal(day.At("2026-10-20 17:00"), workday.End);
        Assert.Equal(Hm("12:00"), workday.Attendance);
    }

    [Fact]
    public void A_day_without_an_evening_pause_has_no_evening_use()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 17:00")
            .Day("2026-10-20", Ac);

        Assert.Null(workday.EveningPause);
        Assert.Equal(TimeSpan.Zero, workday.EveningUse);
    }
}
