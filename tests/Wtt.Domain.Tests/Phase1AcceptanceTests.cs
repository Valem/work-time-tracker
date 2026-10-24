using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Rules;
using Wtt.Domain.Workdays;
using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

/// <summary>Phase-1 acceptance criteria encoded exactly as approved (concept v1.2.1, rule of §6).</summary>
public sealed class Phase1AcceptanceTests
{
    [Fact]
    public void AC_1_1_tuesday_after_work_past_midnight_starts_at_00_00_and_ignores_the_windows_wake()
    {
        var week = MidnightWeek();

        var tuesday = week.Day("2026-10-20", Ac);

        AssertBlocks(week, tuesday, ("2026-10-20 00:00", "2026-10-20 00:40"), ("2026-10-20 08:15", "2026-10-20 17:00"));
        AssertPause(week, tuesday.NightPause, "2026-10-20 00:40", "2026-10-20 08:15");
        Assert.Equal(Hm("0:45"), tuesday.Break);
        Assert.Equal(Hm("8:40"), tuesday.Total);
        Assert.Contains(week.At("2026-10-20 03:10"), tuesday.NightPause!.IgnoredWakes);
    }

    [Fact]
    public void AC_1_1_monday_last_block_ends_at_24_00()
    {
        var week = MidnightWeek();

        var monday = week.Day("2026-10-19", Ac);

        Assert.Equal(week.At("2026-10-20 00:00"), monday.End);
        Assert.Equal(week.At("2026-10-19 22:00"), monday.Start);
    }

    [Fact]
    public void AC_1_1_wednesday_ends_at_24_00_when_thursday_resumes_within_the_hour()
    {
        var week = MidnightWeek();

        var wednesday = week.Day("2026-10-21", Ac);

        Assert.Equal(week.At("2026-10-22 00:00"), wednesday.End);
        var lastUseBasedTotal = week.At("2026-10-21 23:50") - week.At("2026-10-21 21:00");
        Assert.Equal(lastUseBasedTotal + Hm("0:10"), wednesday.Total);
    }

    [Fact]
    public void AC_1_1_thursday_starts_at_00_00_with_the_bridged_pause_as_work()
    {
        var week = MidnightWeek();

        var thursday = week.Day("2026-10-22", Ac);

        AssertBlocks(week, thursday, ("2026-10-22 00:00", "2026-10-22 00:40"), ("2026-10-22 08:15", "2026-10-22 17:00"));
        AssertPause(week, thursday.NightPause, "2026-10-22 00:40", "2026-10-22 08:15");
        Assert.Equal(Hm("8:40"), thursday.Total);
    }

    [Fact]
    public void AC_1_2_a_lunch_with_standby_and_a_windows_wake_inside_is_one_pause()
    {
        var day = new Scenario()
            .Unlock("2026-10-20 08:00")
            .Lock("2026-10-20 12:00")
            .StandbyEnter("2026-10-20 12:05")
            .WindowsWake("2026-10-20 12:30", "2026-10-20 12:32")
            .Unlock("2026-10-20 13:00")
            .Lock("2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        var pause = Assert.Single(workday.Pauses);
        Assert.Equal(Hm("1:00"), pause.Duration);
        Assert.Equal([day.At("2026-10-20 12:30")], pause.IgnoredWakes);
        Assert.Equal(Hm("1:00"), workday.Break);
        Assert.Equal(Hm("8:00"), workday.Total);
    }

    [Fact]
    public void AC_1_2_a_lunch_crossing_the_window_edge_is_deducted_whole()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 11:50")
            .Use("2026-10-20 12:40", "2026-10-20 17:00")
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm("0:50"), workday.Break);
        Assert.Equal(LunchBasis.PauseWhole, workday.Lunch.Basis);
        Assert.Equal(Hm("8:10"), workday.Total);
    }

    [Theory]
    [InlineData("13:45", "5:30")]
    [InlineData("17:00", "8:30")]
    [InlineData("17:01", "8:31")]
    [InlineData("17:45", "9:00")]
    [InlineData("18:31", "9:31")]
    public void AC_1_3_without_a_pause_the_tier_minimum_is_deducted_on_the_net_time(string end, string expectedTotal)
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", $"2026-10-20 {end}")
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm(expectedTotal), workday.Total);
    }

    [Fact]
    public void AC_1_4_early_use_without_midnight_work_starts_the_day_and_the_tier_raises_the_break()
    {
        var workday = new Scenario()
            .Use("2026-10-20 05:00", "2026-10-20 05:05")
            .Use("2026-10-20 08:00", "2026-10-20 12:15")
            .Use("2026-10-20 12:45", "2026-10-20 17:00")
            .Day("2026-10-20", Ac);

        Assert.Equal(new Scenario().At("2026-10-20 05:00"), workday.Start);
        Assert.Null(workday.NightPause);
        Assert.Equal(Hm("1:00"), workday.Break);
        Assert.Equal(LunchBasis.TierMinimum, workday.Lunch.Basis);
        Assert.Equal(Hm("11:00"), workday.Total);
    }

    [Fact]
    public void AC_1_4_a_coffee_pause_outside_the_lunch_window_is_work()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 10:00")
            .Use("2026-10-20 10:20", "2026-10-20 12:15")
            .Use("2026-10-20 12:45", "2026-10-20 17:00")
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm("8:30"), workday.Total);
    }

    [Fact]
    public void AC_1_5_twenty_night_standby_exits_without_unlock_never_start_or_extend_use()
    {
        var night = TwentyNightWakes();

        var presence = night.Presence();
        Assert.Equal(2, presence.Uses.Count);
        Assert.Equal(20, presence.Pauses.Single().IgnoredWakes.Count);
        Assert.Equal(night.At("2026-10-20 08:00"), night.Day("2026-10-20", Ac).Start);
    }

    [Fact]
    public void AC_1_5_beyond_the_winlogon_logs_reach_the_same_night_wakes_still_never_start_use()
    {
        var night = TwentyNightWakes().WinlogonHistoryFrom(null);

        var presence = night.Presence();
        Assert.Equal(2, presence.Uses.Count);
        Assert.Equal(20, presence.Pauses.Single().IgnoredWakes.Count);
        Assert.Equal(night.At("2026-10-20 08:00"), night.Day("2026-10-20", Ac).Start);
    }

    private static Scenario TwentyNightWakes()
    {
        var night = new Scenario().Use("2026-10-19 08:00", "2026-10-19 17:00");
        var wake = new DateTime(2026, 10, 19, 22, 0, 0);
        for (var i = 0; i < 20; i++)
        {
            var reason = i % 2 == 0 ? WakeReason.InputKeyboard : WakeReason.PowerButton;
            night.WindowsWake(Format(wake), Format(wake.AddMinutes(1)), reason);
            wake = wake.AddMinutes(25);
        }

        return night.Use("2026-10-20 08:00", "2026-10-20 17:00");
    }

    [Fact]
    public void AC_1_6_beyond_winlogon_a_person_wake_followed_by_2_01_awake_starts_use()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-03 08:00:00", WakeReason.InputKeyboard)
            .StandbyEnter("2026-08-03 08:02:01")
            .WinlogonHistoryFrom(null);

        var use = Assert.Single(day.Presence().Uses);

        Assert.Equal(day.At("2026-08-03 08:00:00"), use.Start);
        Assert.Equal(day.At("2026-08-03 08:02:01"), use.End);
    }

    [Fact]
    public void AC_1_6_beyond_winlogon_a_person_wake_back_in_standby_after_1_59_does_not_start_use()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-03 08:00:00", WakeReason.PowerButton)
            .StandbyEnter("2026-08-03 08:01:59")
            .WinlogonHistoryFrom(null);

        Assert.Empty(day.Presence().Uses);
    }

    [Fact]
    public void AC_1_8_inside_coverage_an_idle_lock_ends_use_at_the_last_input()
    {
        var workday = CoveredIdleLockDay(lastInput: "2026-10-20 11:58").Day("2026-10-20", Ac);

        Assert.Equal(new Scenario().At("2026-10-20 11:58"), workday.Pauses[0].Start);
        Assert.False(workday.IsRebuilt);
    }

    [Fact]
    public void AC_1_8_inside_coverage_a_display_held_on_ends_use_at_lock_minus_the_idle_timeout()
    {
        var workday = CoveredIdleLockDay(lastInput: "2026-10-20 11:00").Day("2026-10-20", Ac);

        Assert.Equal(new Scenario().At("2026-10-20 11:58"), workday.Pauses[0].Start);
        Assert.False(workday.IsRebuilt);
    }

    [Fact]
    public void AC_1_8_outside_coverage_the_same_lock_ends_use_at_its_own_time_and_marks_the_day_rebuilt()
    {
        var workday = new Scenario()
            .Unlock("2026-10-20 08:00")
            .Lock("2026-10-20 12:08", FactSource.WinlogonHistory, EndCause.IdleLock, TimeSpan.FromMinutes(10))
            .Use("2026-10-20 13:00", "2026-10-20 17:00")
            .Day("2026-10-20", Ac);

        Assert.Equal(new Scenario().At("2026-10-20 12:08"), workday.Pauses[0].Start);
        Assert.True(workday.IsRebuilt);
    }

    [Theory]
    [InlineData("2026-10-25", 1)]
    [InlineData("2027-03-28", 2)]
    public void AC_1_9_on_daylight_saving_days_08_00_to_17_00_is_nine_hours_attendance(string date, int offsetHours)
    {
        var workday = new Scenario()
            .WithOffset(TimeSpan.FromHours(offsetHours))
            .Use($"{date} 08:00", $"{date} 17:00")
            .Day(date, Ac);

        Assert.Equal(Hm("9:00"), workday.Attendance);
    }

    [Fact]
    public void AC_1_9_facts_recorded_at_utc_plus_1_keep_their_dates_after_the_zone_changes_to_utc_minus_5()
    {
        var journey = new Scenario()
            .WithOffset(TimeSpan.FromHours(1))
            .Use("2026-11-03 02:00", "2026-11-03 04:00")
            .Use("2026-11-03 08:00", "2026-11-03 17:00")
            .WithOffset(TimeSpan.FromHours(-5))
            .Use("2026-11-04 08:00", "2026-11-04 17:00");

        var before = journey.Day("2026-11-02", Ac);
        var recordedAtPlusOne = journey.Day("2026-11-03", Ac);
        var recordedAtMinusFive = journey.Day("2026-11-04", Ac);

        Assert.False(before.HasUse);
        Assert.Equal(Scenario.Local("2026-11-03 02:00", TimeSpan.FromHours(1)), recordedAtPlusOne.Start);
        Assert.Equal(Scenario.Local("2026-11-03 17:00", TimeSpan.FromHours(1)), recordedAtPlusOne.End);
        Assert.Equal(Scenario.Local("2026-11-04 08:00", TimeSpan.FromHours(-5)), recordedAtMinusFive.Start);
    }

    [Fact]
    public void AC_1_10_a_first_saved_rule_changes_days_before_install_and_from_its_date_on_only()
    {
        var installedOn = new DateOnly(2026, 10, 12);
        var days = RuleScenario();
        var prefilledResults = TotalsUnder(days, new LunchRuleBook(installedOn, []));

        var firstRule = FirstSavedRule(new DateOnly(2026, 10, 19));
        var afterFirstSave = TotalsUnder(days, new LunchRuleBook(installedOn, [firstRule]));

        Assert.All(prefilledResults.Values, total => Assert.Equal(Hm("8:30"), total));
        Assert.Equal(Hm("8:40"), afterFirstSave[new DateOnly(2026, 9, 21)]);
        foreach (var date in Dates(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18)))
        {
            Assert.Equal(prefilledResults[date], afterFirstSave[date]);
        }

        foreach (var date in Dates(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)))
        {
            Assert.Equal(Hm("8:40"), afterFirstSave[date]);
        }
    }

    [Fact]
    public void AC_1_13_a_second_saved_rule_changes_wednesday_to_sunday_of_week_43_only()
    {
        var installedOn = new DateOnly(2026, 10, 12);
        var days = RuleScenario();
        var firstRule = FirstSavedRule(new DateOnly(2026, 10, 19));
        var afterFirstSave = TotalsUnder(days, new LunchRuleBook(installedOn, [firstRule]));

        var secondRule = SecondSavedRule(new DateOnly(2026, 10, 21));
        var afterSecondSave = TotalsUnder(days, new LunchRuleBook(installedOn, [firstRule, secondRule]));

        foreach (var date in afterFirstSave.Keys.Where(date => date < new DateOnly(2026, 10, 21)))
        {
            Assert.Equal(afterFirstSave[date], afterSecondSave[date]);
        }

        foreach (var date in Dates(new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 25)))
        {
            Assert.Equal(Hm("8:15"), afterSecondSave[date]);
            Assert.NotEqual(afterFirstSave[date], afterSecondSave[date]);
        }
    }

    [Fact]
    public void AC_1_11_the_week_lists_monday_to_sunday_and_its_total_is_the_sum_of_the_day_totals()
    {
        var days = MidnightWeek();
        var monday = new DateOnly(2026, 10, 19);
        var calculated = WorkWeek.DaysOf(monday)
            .Select(date => new WorkdayCalculator().Calculate(date, days.Presence(), Ac))
            .ToList();

        var week = WorkWeek.Of(monday, calculated);

        Assert.Equal(
            ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
            week.Days.Select(day => day.Date.DayOfWeek.ToString()));
        Assert.Equal(week.Days.Aggregate(TimeSpan.Zero, (sum, day) => sum + day.Total), week.Total);
        Assert.Equal(Hm("2:00") + Hm("8:40") + Hm("3:00") + Hm("8:40"), week.Total);
    }

    /// <summary>Mon–Thu of AC-1.1: work past midnight into Tuesday, Wednesday ending 23:50 and Thursday resuming at 00:10.</summary>
    private static Scenario MidnightWeek() => new Scenario()
        .Use("2026-10-19 22:00", "2026-10-20 00:40")
        .WindowsWake("2026-10-20 03:10", "2026-10-20 03:11")
        .Use("2026-10-20 08:15", "2026-10-20 12:00")
        .Use("2026-10-20 12:45", "2026-10-20 17:00")
        .Use("2026-10-21 21:00", "2026-10-21 23:50")
        .Use("2026-10-22 00:10", "2026-10-22 00:40")
        .WindowsWake("2026-10-22 03:10", "2026-10-22 03:11")
        .Use("2026-10-22 08:15", "2026-10-22 12:00")
        .Use("2026-10-22 12:45", "2026-10-22 17:00");

    private static Scenario CoveredIdleLockDay(string lastInput) => new Scenario()
        .Covered("2026-10-20 07:59", "2026-10-20 17:30")
        .SignIn("2026-10-20 07:59", FactSource.Live)
        .Unlock("2026-10-20 08:00", FactSource.Live)
        .LastInput(lastInput)
        .Lock("2026-10-20 12:08", FactSource.Live, EndCause.IdleLock, TimeSpan.FromMinutes(10))
        .Unlock("2026-10-20 13:00", FactSource.Live)
        .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

    /// <summary>A rebuilt day before install and the days of weeks 42 and 43: 08:00–17:00 with a 20-minute lunch.</summary>
    private static (Scenario Facts, IReadOnlyList<DateOnly> Dates) RuleScenario()
    {
        var dates = Dates(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25))
            .Prepend(new DateOnly(2026, 9, 21))
            .ToList();
        var facts = new Scenario();
        foreach (var date in dates)
        {
            facts.Use($"{date:yyyy-MM-dd} 08:00", $"{date:yyyy-MM-dd} 12:10")
                .Use($"{date:yyyy-MM-dd} 12:30", $"{date:yyyy-MM-dd} 17:00");
        }

        return (facts, dates);
    }

    private static LunchRule FirstSavedRule(DateOnly savedOn) => new(
        savedOn,
        Ac.Window,
        [new BreakTier(Hm("5:30"), Hm("0:15")), new BreakTier(Hm("7:00"), Hm("0:20")), new BreakTier(Hm("9:00"), Hm("0:45"))]);

    private static LunchRule SecondSavedRule(DateOnly savedOn) => new(
        savedOn,
        Ac.Window,
        [new BreakTier(Hm("6:00"), Hm("0:45"))]);

    private static Dictionary<DateOnly, TimeSpan> TotalsUnder((Scenario Facts, IReadOnlyList<DateOnly> Dates) days, LunchRuleBook rules)
    {
        var presence = days.Facts.Presence();
        var calculator = new WorkdayCalculator();
        return days.Dates.ToDictionary(date => date, date => calculator.Calculate(date, presence, rules.InForceOn(date)).Total);
    }

    private static IEnumerable<DateOnly> Dates(DateOnly first, DateOnly last)
    {
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static string Format(DateTime local) => local.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertBlocks(Scenario scenario, Workday day, params (string Start, string End)[] expected)
    {
        Assert.Equal(
            expected.Select(block => new WorkBlock(scenario.At(block.Start), scenario.At(block.End))),
            day.Blocks);
    }

    private static void AssertPause(Scenario scenario, Pause? pause, string start, string end)
    {
        Assert.NotNull(pause);
        Assert.Equal(scenario.At(start), pause.Start);
        Assert.Equal(scenario.At(end), pause.End);
    }
}
