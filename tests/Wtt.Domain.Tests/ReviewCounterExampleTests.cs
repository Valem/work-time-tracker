using Wtt.Domain.Facts;
using Wtt.Domain.Workdays;
using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

/// <summary>Days the Phase-1 reviewers probed (review round 1), pinned with their expected results.</summary>
public sealed class ReviewCounterExampleTests
{
    [Theory]
    [InlineData("15:15", "0:15", "7:00")]
    [InlineData("15:20", "0:20", "7:00")]
    [InlineData("15:30", "0:30", "7:00")]
    [InlineData("15:31", "0:30", "7:01")]
    [InlineData("17:30", "0:30", "9:00")]
    [InlineData("18:00", "1:00", "9:00")]
    [InlineData("18:01", "1:00", "9:01")]
    public void Between_tiers_the_total_holds_at_the_threshold_and_never_drops(string end, string expectedBreak, string expectedTotal)
    {
        var workday = new Scenario().Use("2026-10-20 08:00", $"2026-10-20 {end}").Day("2026-10-20", Ac);

        Assert.Equal(Hm(expectedBreak), workday.Break);
        Assert.Equal(Hm(expectedTotal), workday.Total);
    }

    [Fact]
    public void A_short_lunch_is_raised_to_the_tier_minimum()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 12:00")
            .Use("2026-10-20 12:10", "2026-10-20 15:20")
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm("0:20"), workday.Break);
        Assert.Equal(LunchBasis.TierMinimum, workday.Lunch.Basis);
        Assert.Equal(Hm("7:00"), workday.Total);
    }

    [Fact]
    public void A_pause_across_the_window_edge_beats_a_shorter_one_inside_it()
    {
        var day = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 11:10")
            .Use("2026-10-20 12:05", "2026-10-20 12:30")
            .Use("2026-10-20 12:50", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 11:10"), workday.Lunch.Pause!.Start);
        Assert.Equal(Hm("8:05"), workday.Total);
    }

    [Fact]
    public void After_a_night_pause_into_the_window_without_another_pause_no_break_is_due_on_5_30()
    {
        var day = new Scenario()
            .Use("2026-10-19 22:00", "2026-10-20 00:40")
            .Use("2026-10-20 12:10", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 12:10"), workday.NightPause!.End);
        Assert.Equal(TimeSpan.Zero, workday.Break);
        Assert.Equal(Hm("5:30"), workday.Total);
    }

    [Fact]
    public void Inside_coverage_a_keyboard_wake_on_the_lock_screen_does_not_split_the_lunch()
    {
        var workday = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:01")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Lock("2026-10-20 12:00", FactSource.Live, EndCause.UserLock)
            .StandbyEnter("2026-10-20 12:05", FactSource.Live)
            .StandbyExit("2026-10-20 12:30", WakeReason.InputKeyboard, FactSource.Live)
            .LastInput("2026-10-20 12:30:30")
            .StandbyEnter("2026-10-20 12:35", FactSource.Live)
            .Unlock("2026-10-20 13:00", FactSource.Live)
            .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock)
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm("1:00"), workday.Break);
        Assert.Equal(Hm("8:00"), workday.Total);
        Assert.False(workday.IsRebuilt);
    }

    [Theory]
    [InlineData("12:31:59", "1:00", "8:00")]
    [InlineData("12:32:01", "0:30", "8:30")]
    public void Beyond_the_winlogon_log_a_lunch_wake_counts_only_after_more_than_two_minutes(string backInStandby, string expectedBreak, string expectedTotal)
    {
        var workday = new Scenario()
            .SignIn("2026-10-20 08:00")
            .StandbyEnter("2026-10-20 12:00")
            .StandbyExit("2026-10-20 12:30", WakeReason.InputKeyboard)
            .StandbyEnter($"2026-10-20 {backInStandby}")
            .StandbyExit("2026-10-20 13:00", WakeReason.PowerButton)
            .StandbyEnter("2026-10-20 17:00")
            .WinlogonHistoryFrom(null)
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm(expectedBreak), workday.Break);
        Assert.Equal(Hm(expectedTotal), workday.Total);
    }

    [Fact]
    public void Beyond_the_winlogon_log_sleep_resumes_without_a_person_reason_start_nothing()
    {
        var day = new Scenario()
            .SignIn("2026-10-12 08:00")
            .StandbyEnter("2026-10-19 18:00", reason: "Sleep")
            .StandbyExit("2026-10-20 08:00", "ResumeFromSleep")
            .StandbyEnter("2026-10-20 12:00", reason: "Sleep")
            .StandbyExit("2026-10-20 13:00", "ResumeFromSleep")
            .StandbyEnter("2026-10-20 17:00", reason: "Sleep")
            .WinlogonHistoryFrom(null);

        Assert.False(day.Day("2026-10-20", Ac).HasUse);
    }

    [Fact]
    public void Beyond_the_winlogon_log_a_power_button_resume_from_hibernate_starts_the_morning()
    {
        var workday = new Scenario()
            .SignIn("2026-10-19 08:00")
            .StandbyEnter("2026-10-19 18:00")
            .StandbyExit("2026-10-20 02:00", "BatteryDrainBudgetExceeded")
            .StandbyEnter("2026-10-20 02:00:30", reason: "Sleep")
            .StandbyExit("2026-10-20 08:00", WakeReason.PowerButton)
            .StandbyExit("2026-10-20 08:00:05", "ResumeFromSleep")
            .StandbyExit("2026-10-20 08:00:06", "ResumeFromHibernate")
            .StandbyEnter("2026-10-20 12:00")
            .StandbyExit("2026-10-20 13:00", WakeReason.PowerButton)
            .StandbyEnter("2026-10-20 17:00")
            .WinlogonHistoryFrom(null)
            .Day("2026-10-20", Ac);

        Assert.Equal(new Scenario().At("2026-10-20 08:00"), workday.Start);
        Assert.Equal(Hm("1:00"), workday.Break);
        Assert.Equal(Hm("8:00"), workday.Total);
    }
}
