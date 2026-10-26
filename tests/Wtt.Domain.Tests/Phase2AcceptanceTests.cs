using Wtt.Domain.Facts;
using Wtt.Domain.Workdays;
using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

/// <summary>Phase-2 domain acceptance criteria encoded exactly as approved (concept v1.3.0, rule of §6).</summary>
public sealed class Phase2AcceptanceTests
{
    [Fact]
    public void AC_2_8_a_visit_after_a_long_last_pause_ends_the_day_at_the_pause_and_adds_only_its_own_minutes()
    {
        var day = EveningVisit("22:40", "22:42");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 08:00"), workday.Start);
        Assert.Equal(day.At("2026-10-20 17:30"), workday.End);
        Assert.Equal(Hm("1:00"), workday.Break);
        Assert.Equal(Hm("8:32"), workday.Total);
    }

    [Fact]
    public void AC_2_8_a_last_pause_of_exactly_2_00_is_an_evening_pause()
    {
        var day = EveningVisit("19:30", "19:32");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 17:30"), workday.End);
        Assert.Equal(Hm("8:32"), workday.Total);
    }

    [Fact]
    public void AC_2_8_a_last_pause_of_1_59_changes_nothing()
    {
        var day = EveningVisit("19:29", "19:31");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 19:31"), workday.End);
        Assert.Equal(Hm("10:31"), workday.Total);
    }

    [Fact]
    public void AC_2_9_a_last_pause_touching_the_lunch_window_is_the_lunch_pause_not_an_evening_pause()
    {
        var day = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 12:15")
            .Use("2026-10-20 15:00", "2026-10-20 17:00");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 17:00"), workday.End);
        Assert.Equal(Hm("2:45"), workday.Break);
        Assert.Equal(Hm("6:15"), workday.Total);
    }

    [Fact]
    public void AC_2_10_without_an_evening_visit_the_tier_minimum_is_0_15()
    {
        var workday = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 15:00")
            .Day("2026-10-20", Ac);

        Assert.Equal(Hm("0:15"), workday.Break);
        Assert.Equal(Hm("6:45"), workday.Total);
    }

    [Fact]
    public void AC_2_10_the_evening_minutes_count_toward_the_tier()
    {
        var day = new Scenario()
            .Use("2026-10-20 08:00", "2026-10-20 15:00")
            .Use("2026-10-20 20:00", "2026-10-20 20:20");

        var workday = day.Day("2026-10-20", Ac);

        Assert.Equal(day.At("2026-10-20 15:00"), workday.End);
        Assert.Equal(Hm("0:20"), workday.Break);
        Assert.Equal(Hm("7:00"), workday.Total);
    }

    [Fact]
    public void AC_2_11_evening_use_running_past_midnight_counts_to_24_00_and_tuesday_starts_at_00_00()
    {
        var days = EveningIntoTheNight(monday => monday
            .Unlock("2026-10-19 23:30")
            .Lock("2026-10-20 00:20"));

        AssertEveningIntoTheNight(days);
    }

    [Fact]
    public void AC_2_11_evening_use_bridged_over_midnight_gives_the_same_two_days()
    {
        var days = EveningIntoTheNight(monday => monday
            .Unlock("2026-10-19 23:30")
            .Lock("2026-10-19 23:50")
            .Unlock("2026-10-20 00:10")
            .Lock("2026-10-20 00:20"));

        AssertEveningIntoTheNight(days);
    }

    [Fact]
    public void AC_2_12_beyond_winlogon_a_fingerprint_wake_followed_by_2_01_awake_starts_use()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-04 08:00:00", WakeReason.Fingerprint)
            .StandbyEnter("2026-08-04 08:02:01")
            .WinlogonHistoryFrom(null);

        var use = Assert.Single(day.Presence().Uses);

        Assert.Equal(day.At("2026-08-04 08:00:00"), use.Start);
        Assert.Equal(day.At("2026-08-04 08:02:01"), use.End);
    }

    [Fact]
    public void AC_2_12_beyond_winlogon_a_fingerprint_wake_back_in_standby_after_1_59_does_not_start_use()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-04 08:00:00", WakeReason.Fingerprint)
            .StandbyEnter("2026-08-04 08:01:59")
            .WinlogonHistoryFrom(null);

        Assert.Empty(day.Presence().Uses);
    }

    [Theory]
    [InlineData("22:43", "8:33")]
    [InlineData("22:42", "8:30")]
    public void AC_2_12_a_history_day_with_a_fingerprint_wake_in_the_evening(string shutdown, string expectedTotal)
    {
        var day = new Scenario()
            .StandbyExit("2026-08-04 08:00", WakeReason.InputKeyboard)
            .StandbyEnter("2026-08-04 12:00")
            .StandbyExit("2026-08-04 13:00", WakeReason.InputMouse)
            .StandbyEnter("2026-08-04 17:30")
            .StandbyExit("2026-08-04 22:40", WakeReason.Fingerprint)
            .Shutdown($"2026-08-04 {shutdown}")
            .WinlogonHistoryFrom(null);

        var workday = day.Day("2026-08-04", Ac);

        Assert.Equal(day.At("2026-08-04 17:30"), workday.End);
        Assert.Equal(Hm(expectedTotal), workday.Total);
        Assert.True(workday.IsRebuilt);
    }

    /// <summary>Unlock 08:00, lock 12:00–13:00, lock 17:30, then an evening visit: an unlock and a shutdown.</summary>
    private static Scenario EveningVisit(string from, string to) => new Scenario()
        .Use("2026-10-20 08:00", "2026-10-20 12:00")
        .Use("2026-10-20 13:00", "2026-10-20 17:30")
        .Unlock($"2026-10-20 {from}")
        .Shutdown($"2026-10-20 {to}");

    /// <summary>Monday 08:00–17:00 with lunch, the given evening use, then Tuesday 08:00–16:00 with lunch.</summary>
    private static Scenario EveningIntoTheNight(Func<Scenario, Scenario> evening) => evening(new Scenario()
            .Use("2026-10-19 08:00", "2026-10-19 12:00")
            .Use("2026-10-19 12:30", "2026-10-19 17:00"))
        .Use("2026-10-20 08:00", "2026-10-20 12:00")
        .Use("2026-10-20 12:30", "2026-10-20 16:00");

    private static void AssertEveningIntoTheNight(Scenario days)
    {
        var monday = days.Day("2026-10-19", Ac);
        var tuesday = days.Day("2026-10-20", Ac);

        Assert.Equal(days.At("2026-10-19 17:00"), monday.End);
        Assert.Equal(Hm("0:30"), monday.Break);
        Assert.Equal(Hm("9:00"), monday.Total);

        Assert.Equal(days.At("2026-10-20 00:00"), tuesday.Start);
        Assert.NotNull(tuesday.NightPause);
        Assert.Equal(days.At("2026-10-20 00:20"), tuesday.NightPause.Start);
        Assert.Equal(days.At("2026-10-20 08:00"), tuesday.NightPause.End);
        Assert.Equal(Hm("0:30"), tuesday.Break);
        Assert.Equal(Hm("7:50"), tuesday.Total);
    }
}
