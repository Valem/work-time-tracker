using Wtt.Domain.Facts;
using Wtt.Domain.Presence;

namespace Wtt.Domain.Tests;

public sealed class PresenceBuilderTests
{
    [Fact]
    public void Inside_the_winlogon_log_a_long_person_wake_without_unlock_is_no_use()
    {
        var day = new Scenario()
            .Use("2026-10-19 08:00", "2026-10-19 17:00")
            .StandbyExit("2026-10-20 07:00", WakeReason.InputKeyboard)
            .StandbyEnter("2026-10-20 07:30");

        Assert.Single(day.Presence().Uses);
    }

    [Fact]
    public void Beyond_the_winlogon_log_a_wake_of_exactly_two_minutes_is_no_use()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-03 08:00:00", WakeReason.Lid)
            .StandbyEnter("2026-08-03 08:02:00")
            .WinlogonHistoryFrom(null);

        Assert.Empty(day.Presence().Uses);
    }

    [Fact]
    public void Beyond_the_winlogon_log_a_wake_for_a_windows_reason_never_starts_use()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-03 08:00", "BatteryDrainBudgetExceeded")
            .StandbyEnter("2026-08-03 09:00")
            .WinlogonHistoryFrom(null);

        Assert.Empty(day.Presence().Uses);
    }

    [Fact]
    public void Beyond_the_winlogon_log_only_wakes_before_the_oldest_winlogon_fact_use_the_two_minute_test()
    {
        var days = new Scenario()
            .StandbyExit("2026-08-03 08:00", WakeReason.InputKeyboard)
            .StandbyEnter("2026-08-03 12:00")
            .Use("2026-08-04 07:00", "2026-08-04 07:30")
            .StandbyExit("2026-08-04 08:00", WakeReason.InputKeyboard)
            .StandbyEnter("2026-08-04 12:00");

        var uses = days.Presence().Uses;

        Assert.Equal(
            [(days.At("2026-08-03 08:00"), days.At("2026-08-03 12:00")), (days.At("2026-08-04 07:00"), days.At("2026-08-04 07:30"))],
            uses.Select(use => (use.Start, use.End)));
    }

    [Fact]
    public void CR_002_beyond_the_winlogon_log_a_resume_from_hibernate_alone_is_no_person()
    {
        var day = new Scenario()
            .StandbyExit("2026-08-03 08:00", "ResumeFromHibernate")
            .StandbyEnter("2026-08-03 12:00")
            .WinlogonHistoryFrom(null);

        Assert.Empty(day.Presence().Uses);
    }

    [Fact]
    public void TD_001_a_resume_reported_three_times_is_one_ignored_wake()
    {
        var night = new Scenario()
            .Use("2026-10-19 08:00", "2026-10-19 17:00")
            .StandbyEnter("2026-10-19 17:01", reason: "Sleep")
            .StandbyExit("2026-10-20 03:00:00", "Unmapped:WakeSource 3 Timer")
            .StandbyExit("2026-10-20 03:00:02", "ResumeFromSleep")
            .StandbyExit("2026-10-20 03:00:03", "ResumeFromHibernate")
            .StandbyEnter("2026-10-20 03:01", reason: "Sleep")
            .Use("2026-10-20 08:00", "2026-10-20 17:00");

        var pause = Assert.Single(night.Presence().Pauses);

        Assert.Equal([night.At("2026-10-20 03:00")], pause.IgnoredWakes);
    }

    [Fact]
    public void A_sign_in_starts_use_and_a_sign_out_or_shutdown_ends_it()
    {
        var days = new Scenario()
            .SignIn("2026-10-20 08:00").SignOut("2026-10-20 12:00")
            .SignIn("2026-10-20 13:00").Shutdown("2026-10-20 17:00");

        Assert.Equal(
            [(days.At("2026-10-20 08:00"), days.At("2026-10-20 12:00")), (days.At("2026-10-20 13:00"), days.At("2026-10-20 17:00"))],
            days.Presence().Uses.Select(use => (use.Start, use.End)));
    }

    [Fact]
    public void Idle_time_without_a_lock_stays_use()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:30")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .IdleTimeout("2026-10-20 10:00")
            .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

        var use = Assert.Single(day.Presence().Uses);
        Assert.Equal(day.At("2026-10-20 17:00"), use.End);
    }

    [Fact]
    public void A_boot_while_in_use_ends_use_at_the_last_fact_before_it()
    {
        var day = new Scenario()
            .Unlock("2026-10-20 08:00")
            .StandbyEnter("2026-10-20 09:00")
            .Unlock("2026-10-20 09:30")
            .StandbyExit("2026-10-20 11:00", "Code53")
            .Boot("2026-10-20 11:20")
            .SignIn("2026-10-20 11:22")
            .Lock("2026-10-20 12:00");

        Assert.Equal(
            [(day.At("2026-10-20 08:00"), day.At("2026-10-20 09:00")), (day.At("2026-10-20 09:30"), day.At("2026-10-20 11:00")), (day.At("2026-10-20 11:22"), day.At("2026-10-20 12:00"))],
            day.Presence().Uses.Select(use => (use.Start, use.End)));
    }

    [Fact]
    public void CR_003_a_crash_reported_by_windows_ends_the_use_at_its_stop_time_and_keeps_the_afternoon()
    {
        var day = new Scenario()
            .SignIn("2026-10-20 07:59")
            .Use("2026-10-20 08:00", "2026-10-20 12:00")
            .Unlock("2026-10-20 12:45")
            .Shutdown("2026-10-20 15:00")
            .Boot("2026-10-20 15:05")
            .SignIn("2026-10-20 15:07")
            .Lock("2026-10-20 17:00");

        var workday = day.Day("2026-10-20", TestRules.Ac);

        Assert.Equal(TestRules.Hm("0:45"), workday.Break);
        Assert.Equal(day.At("2026-10-20 15:00"), workday.Pauses[1].Start);
        Assert.Equal(TestRules.Hm("8:16"), workday.Total);
    }

    [Fact]
    public void A_live_resume_without_lock_screen_starts_use_at_the_first_input_after_two_minutes_awake()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:30")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .StandbyEnter("2026-10-20 12:00", FactSource.Live, reason: "Lid")
            .StandbyExit("2026-10-20 12:50", WakeReason.Lid, FactSource.Live)
            .LastInput("2026-10-20 12:51")
            .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

        Assert.Equal(
            [(day.At("2026-10-20 08:00"), day.At("2026-10-20 12:00")), (day.At("2026-10-20 12:51"), day.At("2026-10-20 17:00"))],
            day.Presence().Uses.Select(use => (use.Start, use.End)));
    }

    [Fact]
    public void A_live_input_on_the_lock_screen_does_not_start_use()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:30")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Lock("2026-10-20 12:00", FactSource.Live, EndCause.UserLock)
            .StandbyEnter("2026-10-20 12:01", FactSource.Live)
            .StandbyExit("2026-10-20 12:50", WakeReason.InputKeyboard, FactSource.Live)
            .LastInput("2026-10-20 12:50")
            .StandbyEnter("2026-10-20 13:30", FactSource.Live);

        Assert.Single(day.Presence().Uses);
    }

    [Fact]
    public void A_history_lock_inside_coverage_is_ignored_because_live_facts_win()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:30")
            .SignIn("2026-10-20 07:59", FactSource.Live)
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Lock("2026-10-20 10:00")
            .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

        var use = Assert.Single(day.Presence().Uses);
        Assert.Equal(day.At("2026-10-20 17:00"), use.End);
        Assert.Equal(Precision.Exact, use.Precision);
    }

    [Fact]
    public void History_fills_the_stretch_from_sign_in_to_the_recorder_start_without_marking_it_rebuilt()
    {
        var day = new Scenario()
            .Covered("2026-10-20 08:00", "2026-10-20 17:30")
            .SignIn("2026-10-20 08:00", FactSource.Live)
            .Lock("2026-10-20 08:01")
            .Unlock("2026-10-20 08:02")
            .LastInput("2026-10-20 08:03")
            .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

        var uses = day.Presence().Uses;

        Assert.Equal(
            [(day.At("2026-10-20 08:00"), day.At("2026-10-20 08:01")), (day.At("2026-10-20 08:02"), day.At("2026-10-20 17:00"))],
            uses.Select(use => (use.Start, use.End)));
        Assert.All(uses, use => Assert.Equal(Precision.Exact, use.Precision));
    }

    [Fact]
    public void A_recording_gap_is_filled_from_history_and_the_use_is_marked_rebuilt()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 10:00")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Lock("2026-10-20 12:00")
            .Unlock("2026-10-20 13:00")
            .Covered("2026-10-20 14:00", "2026-10-20 17:30")
            .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

        var uses = day.Presence().Uses;

        Assert.Equal(2, uses.Count);
        Assert.All(uses, use => Assert.Equal(Precision.Rebuilt, use.Precision));
    }

    [Fact]
    public void A_live_end_by_user_lock_falls_at_its_own_time_even_with_older_input()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:30")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .LastInput("2026-10-20 11:00")
            .Lock("2026-10-20 12:08", FactSource.Live, EndCause.Lid);

        Assert.Equal(day.At("2026-10-20 12:08"), Assert.Single(day.Presence().Uses).End);
    }

    [Fact]
    public void A_timer_end_never_falls_before_the_use_started()
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 17:30")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Lock("2026-10-20 08:05", FactSource.Live, EndCause.IdleLock, TimeSpan.FromMinutes(10));

        var use = Assert.Single(day.Presence().Uses);
        Assert.Equal(use.Start, use.End);
    }
}
