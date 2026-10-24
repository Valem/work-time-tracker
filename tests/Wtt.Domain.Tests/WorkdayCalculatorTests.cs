using Wtt.Domain.Rules;
using Wtt.Domain.Workdays;
using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

public sealed class WorkdayCalculatorTests
{
    [Fact]
    public void One_more_minute_never_lowers_the_total()
    {
        var previous = TimeSpan.Zero;
        var end = new DateTime(2026, 10, 20, 8, 1, 0);
        while (end.Hour < 22)
        {
            var total = new Scenario()
                .Use("2026-10-20 08:00", end.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture))
                .Day("2026-10-20", Ac)
                .Total;

            Assert.True(total >= previous, $"Ending at {end:HH:mm} gave {total}, less than {previous}.");
            previous = total;
            end = end.AddMinutes(1);
        }
    }

    [Fact]
    public void A_pause_ending_exactly_when_the_window_opens_touches_it_and_is_the_lunch()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 11:15")
            .Use("2026-10-20 12:00", "2026-10-20 17:00")
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm("0:45"), workday.Break);
        Assert.Equal(LunchBasis.PauseWhole, workday.Lunch.Basis);
    }

    [Fact]
    public void The_longest_pause_touching_the_window_is_the_lunch()
    {
        var day = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 11:50")
            .Use("2026-10-20 12:10", "2026-10-20 12:30")
            .Use("2026-10-20 13:05", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 12:30"), workday.Lunch.Pause!.Start);
        Assert.Equal(Hm("0:35"), workday.Break);
    }

    [Fact]
    public void A_short_day_without_pause_has_no_break()
    {
        var workday = new Scenario().Use("2026-10-20 08:00", "2026-10-20 12:00").Day("2026-10-20", Ac);

        Assert.Equal(TimeSpan.Zero, workday.Break);
        Assert.Equal(LunchBasis.None, workday.Lunch.Basis);
        Assert.Equal(Hm("4:00"), workday.Total);
    }

    [Fact]
    public void CR_001_a_night_pause_starting_before_the_window_and_running_into_it_is_not_work()
    {
        var day = new Scenario()
            .Use("2026-10-19 22:00", "2026-10-20 00:40")
            .Use("2026-10-20 12:10", "2026-10-20 12:40")
            .Use("2026-10-20 13:10", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 00:40"), workday.NightPause!.Start);
        Assert.Equal(day.At("2026-10-20 12:10"), workday.NightPause.End);
        Assert.Equal(Hm("0:30"), workday.Break);
        Assert.Equal(Hm("5:00"), workday.Total);
    }

    [Fact]
    public void CR_001_the_lunch_is_the_pause_touching_the_window_after_a_night_pause_into_it()
    {
        var day = new Scenario()
            .Use("2026-10-19 23:30", "2026-10-20 00:30")
            .Use("2026-10-20 12:10", "2026-10-20 13:00")
            .Use("2026-10-20 13:30", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 00:30"), workday.NightPause!.Start);
        Assert.Equal(day.At("2026-10-20 12:10"), workday.NightPause.End);
        Assert.Equal(day.At("2026-10-20 13:00"), workday.Lunch.Pause!.Start);
        Assert.Equal(Hm("0:30"), workday.Break);
    }

    [Fact]
    public void CR_001_a_lunch_starting_before_the_window_is_not_taken_for_a_shorter_night_pause()
    {
        var day = new Scenario()
            .Use("2026-10-19 22:00", "2026-10-20 05:00")
            .Use("2026-10-20 05:30", "2026-10-20 11:40")
            .Use("2026-10-20 12:30", "2026-10-20 15:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 05:00"), workday.NightPause!.Start);
        Assert.Equal(day.At("2026-10-20 05:30"), workday.NightPause.End);
        Assert.Equal(day.At("2026-10-20 11:40"), workday.Lunch.Pause!.Start);
        Assert.Equal(Hm("1:00"), workday.Break);
        Assert.Equal(LunchBasis.TierMinimum, workday.Lunch.Basis);
        Assert.Equal(Hm("13:30"), workday.Total);
    }

    [Fact]
    public void A_pause_starting_exactly_when_the_window_opens_is_never_the_night_pause()
    {
        var day = new Scenario()
            .Use("2026-10-19 23:00", "2026-10-20 00:20")
            .Use("2026-10-20 00:50", "2026-10-20 12:00")
            .Use("2026-10-20 13:30", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 00:20"), workday.NightPause!.Start);
        Assert.Equal(Hm("1:30"), workday.Break);
    }

    [Fact]
    public void CR_007_a_span_over_the_autumn_change_splits_at_the_midnight_shown_after_it()
    {
        var span = new Scenario()
            .WithOffset(TimeSpan.FromHours(2)).Unlock("2026-10-25 01:00")
            .WithOffset(TimeSpan.FromHours(1)).Lock("2026-10-26 00:30");

        var sunday = span.Day("2026-10-25", Ac);
        var monday = span.Day("2026-10-26", Ac);

        Assert.Equal(Scenario.Local("2026-10-26 00:00", TimeSpan.FromHours(1)), sunday.End);
        Assert.Equal(TimeSpan.FromHours(24), sunday.Attendance);
        Assert.Equal(Scenario.Local("2026-10-26 00:00", TimeSpan.FromHours(1)), monday.Start);
        Assert.Equal(Hm("0:30"), monday.Attendance);
    }

    [Fact]
    public void CR_007_a_span_over_midnight_and_then_the_autumn_change_splits_at_the_midnight_before_it()
    {
        var night = new Scenario()
            .WithOffset(TimeSpan.FromHours(2)).Unlock("2026-10-24 22:00")
            .WithOffset(TimeSpan.FromHours(1)).Lock("2026-10-25 04:00");

        var saturday = night.Day("2026-10-24", Ac);
        var sunday = night.Day("2026-10-25", Ac);

        Assert.Equal(Hm("2:00"), saturday.Attendance);
        Assert.Equal(Scenario.Local("2026-10-25 00:00", TimeSpan.FromHours(2)), sunday.Start);
        Assert.Equal(Hm("5:00"), sunday.Attendance);
    }

    [Fact]
    public void CR_007_a_span_over_midnight_and_then_the_spring_change_splits_at_the_midnight_before_it()
    {
        var night = new Scenario()
            .WithOffset(TimeSpan.FromHours(1)).Unlock("2027-03-27 22:00")
            .WithOffset(TimeSpan.FromHours(2)).Lock("2027-03-28 04:00");

        var saturday = night.Day("2027-03-27", Ac);
        var sunday = night.Day("2027-03-28", Ac);

        Assert.Equal(Hm("2:00"), saturday.Attendance);
        Assert.Equal(Scenario.Local("2027-03-28 00:00", TimeSpan.FromHours(1)), sunday.Start);
        Assert.Equal(Hm("3:00"), sunday.Attendance);
    }

    [Fact]
    public void Work_carried_into_the_autumn_change_night_counts_real_elapsed_time()
    {
        var acrossTheChange = new Scenario()
            .WithOffset(TimeSpan.FromHours(2)).Unlock("2026-10-24 22:00")
            .WithOffset(TimeSpan.FromHours(1)).Lock("2026-10-25 04:00")
            .Use("2026-10-25 08:00", "2026-10-25 17:00");

        Assert.Equal(Hm("2:00"), acrossTheChange.Day("2026-10-24", Ac).Attendance);
        Assert.Equal(Hm("14:00"), acrossTheChange.Day("2026-10-25", Ac).Attendance);

        var days = new Scenario()
            .WithOffset(TimeSpan.FromHours(2)).Use("2026-10-24 20:00", "2026-10-25 02:30")
            .WithOffset(TimeSpan.FromHours(1)).Use("2026-10-25 08:00", "2026-10-25 17:00");

        var saturday = days.Day("2026-10-24", Ac);
        var sunday = days.Day("2026-10-25", Ac);

        Assert.Equal(Scenario.Local("2026-10-25 00:00", TimeSpan.FromHours(2)), saturday.End);
        Assert.Equal(Hm("4:00"), saturday.Attendance);
        Assert.Equal(Scenario.Local("2026-10-25 00:00", TimeSpan.FromHours(2)), sunday.Start);
        Assert.Equal(Hm("11:30"), sunday.Attendance);
    }

    [Theory]
    [InlineData("00:29", true)]
    [InlineData("00:31", false)]
    public void Use_resuming_59_minutes_after_the_last_use_carries_over_and_61_does_not(string resume, bool carried)
    {
        var days = new Scenario()
            .Use("2026-10-19 15:00", "2026-10-19 23:30")
            .Use($"2026-10-20 {resume}", "2026-10-20 01:00")
            .Use("2026-10-20 08:00", "2026-10-20 17:00");

        var monday = days.Day("2026-10-19", Ac);
        var tuesday = days.Day("2026-10-20", Ac);

        Assert.Equal(carried ? days.At("2026-10-20 00:00") : days.At("2026-10-19 23:30"), monday.End);
        Assert.Equal(carried ? days.At("2026-10-20 00:00") : days.At($"2026-10-20 {resume}"), tuesday.Start);
        Assert.Equal(carried, tuesday.NightPause is not null);
    }

    [Fact]
    public void Use_resuming_an_hour_after_the_previous_days_last_use_does_not_carry_over()
    {
        var day = new Scenario()
            .Use("2026-10-19 21:00", "2026-10-19 23:30")
            .Use("2026-10-20 00:30", "2026-10-20 01:00")
            .Use("2026-10-20 08:00", "2026-10-20 12:00");

        var monday = day.Day("2026-10-19", Ac);
        var tuesday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-19 23:30"), monday.End);
        Assert.Equal(day.At("2026-10-20 00:30"), tuesday.Start);
        Assert.Null(tuesday.NightPause);
    }

    [Fact]
    public void Seconds_are_rounded_to_the_nearest_minute_so_the_totals_add_up_on_screen()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00:29", "2026-10-20 12:00:30")
            .Day("2026-10-20", Ac);

        Assert.Equal(new Scenario().At("2026-10-20 08:00"), workday.Start);
        Assert.Equal(new Scenario().At("2026-10-20 12:01"), workday.End);
        Assert.Equal(Hm("4:01"), workday.Total);
    }

    [Fact]
    public void A_day_without_use_has_no_start_and_no_total()
    {
        var workday = new Scenario().Use("2026-10-20 08:00", "2026-10-20 12:00").Day("2026-10-21", Ac);

        Assert.False(workday.HasUse);
        Assert.Null(workday.Start);
        Assert.Equal(TimeSpan.Zero, workday.Total);
    }

    [Fact]
    public void A_week_must_start_on_monday_and_hold_its_seven_days()
    {
        var tuesday = new DateOnly(2026, 10, 20);
        var days = Enumerable.Range(0, 7).Select(i => Workday.WithoutUse(tuesday.AddDays(i))).ToList();

        Assert.Throws<ArgumentException>(() => WorkWeek.Of(tuesday, days));
        Assert.Equal(new DateOnly(2026, 10, 19), WorkWeek.MondayOf(new DateOnly(2026, 10, 25)));
        Assert.Equal(new DateOnly(2026, 10, 19), WorkWeek.MondayOf(new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void The_prefilled_rule_is_window_11_30_to_14_00_with_the_brief_s_example_tiers()
    {
        var rule = LunchRule.Prefilled;

        Assert.Equal(new TimeOnly(11, 30), rule.Window.Start);
        Assert.Equal(new TimeOnly(14, 0), rule.Window.End);
        Assert.Equal(
            [new BreakTier(Hm("5:30"), Hm("0:15")), new BreakTier(Hm("7:00"), Hm("0:30")), new BreakTier(Hm("9:00"), Hm("1:00"))],
            rule.Tiers);
    }
}
