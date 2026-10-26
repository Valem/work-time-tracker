using Wtt.Application.Ports;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Workdays;
using static Wtt.Application.Recording.LiveSignalKind;
using static Wtt.Infrastructure.Tests.RecorderRig;

namespace Wtt.Infrastructure.Tests;

/// <summary>
/// Phase-2 criteria on scripted days: the Recorder's live facts and coverage on a real data file, read back
/// through the domain core. Hands-on parts (a real laptop, powercfg, CPU over 8 h) are not covered here.
/// </summary>
public sealed class Phase2RecorderAcceptanceTests : IDisposable
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly IdleTimers DisplayOffAfterFiveMinutes = new(DisplayOff: TimeSpan.FromMinutes(5));

    private readonly RecorderRig rig = new(At("2026-10-19 17:59:30"));

    public void Dispose() => rig.Dispose();

    [Fact]
    public async Task AC_2_1_start_is_the_first_unlock_end_is_the_last_real_input_after_an_idle_auto_lock_and_a_45_minute_lunch_lock_is_a_45_minute_pause()
    {
        rig.Desktop.Timers = DisplayOffAfterFiveMinutes;
        await LockedOvernightAsync();

        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:20");
        await rig.InputAsync("2026-10-20 11:59:58");
        await rig.SignalAsync(Locked, "2026-10-20 12:00:00");
        await rig.SignalAsync(Unlocked, "2026-10-20 12:45:00");
        await rig.InputAsync("2026-10-20 17:26:00");
        await rig.SignalAsync(Locked, "2026-10-20 17:31:00");
        await rig.RunUntilAsync("2026-10-20 18:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Within(At("2026-10-20 08:00:20"), day.Start);
        Within(At("2026-10-20 17:26:00"), day.End);
        var lunch = Assert.Single(day.Pauses);
        Within(TimeSpan.FromMinutes(45), lunch.Duration);
        Assert.False(day.IsRebuilt);
    }

    [Fact]
    public async Task AC_2_2_after_a_video_held_the_display_on_an_idle_lock_ends_use_one_idle_timeout_before_it()
    {
        rig.Desktop.Timers = DisplayOffAfterFiveMinutes;
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 13:00:00");
        await rig.InputAsync("2026-10-20 14:00:00");

        await rig.SignalAsync(Locked, "2026-10-20 14:35:00");
        await rig.RunUntilAsync("2026-10-20 15:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Within(At("2026-10-20 14:35:00") - TimeSpan.FromMinutes(5), day.End);
    }

    [Fact]
    public async Task AC_2_2_the_lid_closed_after_a_video_without_input_ends_use_at_the_lid()
    {
        rig.Desktop.Timers = DisplayOffAfterFiveMinutes;
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 13:00:00");
        await rig.InputAsync("2026-10-20 14:00:00");

        await rig.SignalAsync(LidClosed, "2026-10-20 14:30:00");
        await rig.SignalAsync(Suspending, "2026-10-20 14:30:02");
        rig.TimePassesUnseen("2026-10-20 18:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Within(At("2026-10-20 14:30:00"), day.End);
    }

    [Fact]
    public async Task AC_2_3_after_a_cold_boot_the_day_starts_at_sign_in_its_coverage_begins_there_and_the_lunch_standby_marks_nothing()
    {
        rig.TimePassesUnseen("2026-10-20 08:10:40");
        rig.WindowsLogs(FactKind.Boot, "2026-10-20 08:09:10", FactSource.SystemHistory);
        rig.WindowsLogs(FactKind.SignIn, "2026-10-20 08:10:00", FactSource.SystemHistory);
        rig.Desktop.LastInputAt = At("2026-10-20 08:10:30");
        await rig.StartRecorderAsync("2", signedInAt: "2026-10-20 08:10:00");

        await rig.InputAsync("2026-10-20 11:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 12:00:00");
        await rig.SignalAsync(Suspending, "2026-10-20 12:01:00");
        rig.TimePassesUnseen("2026-10-20 12:45:00");
        rig.WindowsLogs(FactKind.StandbyEnter, "2026-10-20 12:01:00", FactSource.SystemHistory, "Sleep");
        rig.WindowsLogs(FactKind.StandbyExit, "2026-10-20 12:45:00", FactSource.SystemHistory, "PowerButton");
        await rig.SignalAsync(Resumed, "2026-10-20 12:45:00");
        await rig.SignalAsync(Unlocked, "2026-10-20 12:46:00");
        await rig.InputAsync("2026-10-20 17:00:00");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:01");

        var day = await rig.DayAsync("2026-10-20");
        Within(At("2026-10-20 08:10:00"), day.Start);
        Assert.False(day.IsRebuilt);
        Assert.Contains(day.Pauses, pause => pause.Start == At("2026-10-20 12:00:00") && pause.End == At("2026-10-20 12:46:00"));
        Assert.Equal(new CoverageSpan(At("2026-10-20 08:10:00"), At("2026-10-20 17:00:00")), Assert.Single(await rig.SpansAsync()));
    }

    [Fact]
    public async Task AC_2_3_after_overnight_standby_a_sign_out_and_a_restart_coverage_resumes_from_sign_in_without_any_action()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:20");
        await rig.SignalAsync(SigningOut, "2026-10-20 12:00:00");

        rig.TimePassesUnseen("2026-10-20 12:30:20");
        await rig.StartRecorderAsync("3", signedInAt: "2026-10-20 12:30:00");
        await rig.SignalAsync(ShuttingDown, "2026-10-20 17:00:00");

        rig.TimePassesUnseen("2026-10-21 07:55:30");
        await rig.StartRecorderAsync("4", signedInAt: "2026-10-21 07:55:00");
        await rig.RunUntilAsync("2026-10-21 08:30:00");

        var spans = await rig.SpansAsync();
        Assert.Equal(
            [
                (At("2026-10-19 17:59:00"), At("2026-10-20 12:00:00")),
                (At("2026-10-20 12:30:00"), At("2026-10-20 17:00:00")),
                (At("2026-10-21 07:55:00"), At("2026-10-21 08:30:00")),
            ],
            spans.Select(span => (span.From, span.To)));
        Assert.False((await rig.DayAsync("2026-10-20")).IsRebuilt);
        Within(At("2026-10-21 07:55:00"), (await rig.DayAsync("2026-10-21")).Start);
    }

    [Fact]
    public async Task AC_2_4_without_sign_in_after_sleep_use_restarts_at_the_first_input_and_Windows_own_wakes_add_no_use()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
        await rig.InputAsync("2026-10-20 11:59:58");
        await rig.SignalAsync(Suspending, "2026-10-20 12:00:00");
        rig.TimePassesUnseen("2026-10-20 12:40:00");
        await rig.SignalAsync(Resumed, "2026-10-20 12:40:00");
        await rig.InputAsync("2026-10-20 12:41:00");
        await rig.InputAsync("2026-10-20 16:59:00");
        await rig.SignalAsync(LidClosed, "2026-10-20 17:00:00");
        await rig.SignalAsync(Suspending, "2026-10-20 17:00:02");

        rig.TimePassesUnseen("2026-10-21 03:10:00");
        await rig.SignalAsync(Resumed, "2026-10-21 03:10:00");
        await rig.SignalAsync(Suspending, "2026-10-21 03:11:30");
        rig.TimePassesUnseen("2026-10-21 08:00:00");
        await rig.SignalAsync(Resumed, "2026-10-21 08:00:00");
        await rig.InputAsync("2026-10-21 08:00:30");
        await rig.InputAsync("2026-10-21 11:00:00");
        await rig.SignalAsync(Locked, "2026-10-21 11:00:01");

        var monday = await rig.DayAsync("2026-10-20");
        Assert.Contains(monday.Pauses, pause => pause.Start == At("2026-10-20 12:00:00") && Near(At("2026-10-20 12:41:00"), pause.End));
        Within(At("2026-10-20 17:00:02"), monday.End);

        var tuesday = await rig.DayAsync("2026-10-21");
        Within(At("2026-10-21 08:00:30"), tuesday.Start);
        Assert.Null(tuesday.NightPause);
        Assert.Single(tuesday.Blocks);
    }

    [Fact]
    public async Task AC_2_5_a_Recorder_stopped_for_30_minutes_has_the_gap_filled_from_history_on_its_next_start_and_the_day_marked_rebuilt()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
        await rig.RunUntilAsync("2026-10-20 10:00:00");

        rig.TimePassesUnseen("2026-10-20 10:30:00");
        rig.WindowsLogs(FactKind.Lock, "2026-10-20 10:10:00");
        rig.WindowsLogs(FactKind.Unlock, "2026-10-20 10:20:00");
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-19 17:59:00");
        await rig.InputAsync("2026-10-20 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Assert.True(day.IsRebuilt);
        Assert.Contains(day.Pauses, pause => pause.Start == At("2026-10-20 10:10:00") && pause.End == At("2026-10-20 10:20:00"));
        var spans = await rig.SpansAsync();
        Assert.Equal(At("2026-10-20 10:00:00"), spans[0].To);
        Assert.Equal(At("2026-10-20 10:30:00"), spans[1].From);
        Assert.Empty(rig.Errors);
    }

    [Fact]
    public async Task A_first_start_days_after_the_sign_in_leaves_every_day_since_the_sign_in_rebuilt()
    {
        rig.TimePassesUnseen("2026-10-22 10:00:00");
        rig.WindowsLogs(FactKind.SignIn, "2026-10-20 07:58:00", FactSource.SystemHistory);
        rig.WindowsLogs(FactKind.Lock, "2026-10-20 17:00:00");
        rig.WindowsLogs(FactKind.Unlock, "2026-10-21 08:00:00");
        rig.WindowsLogs(FactKind.Lock, "2026-10-21 17:00:00");
        rig.WindowsLogs(FactKind.Unlock, "2026-10-22 08:00:00");
        rig.Desktop.LastInputAt = At("2026-10-22 09:59:50");
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-20 07:58:00");
        await rig.InputAsync("2026-10-22 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-22 17:00:00");

        Assert.True((await rig.DayAsync("2026-10-20")).IsRebuilt);
        Assert.True((await rig.DayAsync("2026-10-21")).IsRebuilt);
        Assert.True((await rig.DayAsync("2026-10-22")).IsRebuilt);
        Assert.Equal(At("2026-10-22 10:00:00"), Assert.Single(await rig.SpansAsync()).From);
    }

    [Fact]
    public async Task A_start_by_the_App_hours_after_a_sign_in_the_Run_entry_missed_marks_the_day_rebuilt()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
        await rig.SignalAsync(ShuttingDown, "2026-10-20 12:00:00");

        rig.TimePassesUnseen("2026-10-20 15:00:00");
        rig.WindowsLogs(FactKind.SignIn, "2026-10-20 13:00:00", FactSource.SystemHistory);
        rig.WindowsLogs(FactKind.Lock, "2026-10-20 14:00:00");
        rig.WindowsLogs(FactKind.Unlock, "2026-10-20 14:10:00");
        rig.Desktop.LastInputAt = At("2026-10-20 14:59:50");
        await rig.StartRecorderAsync("2", signedInAt: "2026-10-20 13:00:00");
        await rig.InputAsync("2026-10-20 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Assert.True(day.IsRebuilt);
        Assert.Contains(day.Pauses, pause => pause.Start == At("2026-10-20 14:00:00") && pause.End == At("2026-10-20 14:10:00"));
        Assert.Equal(At("2026-10-20 15:00:00"), (await rig.SpansAsync())[^1].From);
    }

    [Fact]
    public async Task A_start_by_the_App_hours_after_a_sign_in_the_Run_entry_missed_marks_the_day_rebuilt_during_continuous_use()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
        await rig.SignalAsync(ShuttingDown, "2026-10-20 12:00:00");

        rig.TimePassesUnseen("2026-10-20 15:00:00");
        rig.WindowsLogs(FactKind.SignIn, "2026-10-20 13:00:00", FactSource.SystemHistory);
        rig.Desktop.LastInputAt = At("2026-10-20 14:59:50");
        await rig.StartRecorderAsync("2", signedInAt: "2026-10-20 13:00:00");
        await rig.InputAsync("2026-10-20 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Assert.True(day.IsRebuilt);
        Assert.Equal(At("2026-10-20 15:00:00"), (await rig.SpansAsync())[^1].From);
    }

    [Fact]
    public async Task A_first_install_hours_after_the_sign_in_the_same_day_marks_the_day_rebuilt_during_continuous_use()
    {
        rig.TimePassesUnseen("2026-10-20 10:00:00");
        rig.WindowsLogs(FactKind.SignIn, "2026-10-20 08:00:00", FactSource.SystemHistory);
        rig.Desktop.LastInputAt = At("2026-10-20 09:59:50");
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-20 08:00:00");
        await rig.InputAsync("2026-10-20 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Assert.True(day.IsRebuilt);
        Within(At("2026-10-20 08:00:00"), day.Start);
        Assert.Equal(At("2026-10-20 10:00:00"), Assert.Single(await rig.SpansAsync()).From);
    }

    [Fact]
    public async Task A_Recorder_restarted_after_a_resume_without_a_lock_screen_starts_use_at_the_first_input_after_its_start()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
        await rig.InputAsync("2026-10-20 11:59:58");
        await rig.SignalAsync(Suspending, "2026-10-20 12:00:00");

        rig.TimePassesUnseen("2026-10-20 13:00:00");
        rig.WindowsLogs(FactKind.Lock, "2026-10-19 18:05:00");
        rig.WindowsLogs(FactKind.Unlock, "2026-10-20 08:00:00");
        rig.WindowsLogs(FactKind.StandbyEnter, "2026-10-20 12:00:00", FactSource.SystemHistory, "Sleep");
        rig.WindowsLogs(FactKind.StandbyExit, "2026-10-20 12:40:00", FactSource.SystemHistory, "PowerButton");
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-19 17:59:00");
        await rig.InputAsync("2026-10-20 13:00:30");
        await rig.InputAsync("2026-10-20 16:59:00");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

        var day = await rig.DayAsync("2026-10-20");
        Within(At("2026-10-20 17:00:00"), day.End);
        Assert.Contains(day.Pauses, pause => pause.Start == At("2026-10-20 12:00:00") && Near(At("2026-10-20 13:00:30"), pause.End));
    }

    [Fact]
    public async Task A_day_the_Recorder_covered_whole_is_not_marked_rebuilt()
    {
        await LockedOvernightAsync();
        await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
        await rig.InputAsync("2026-10-20 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

        Assert.False((await rig.DayAsync("2026-10-20")).IsRebuilt);
    }

    /// <summary>Signed in the evening before, locked and in standby overnight with the Recorder running.</summary>
    private async Task LockedOvernightAsync()
    {
        rig.Desktop.LastInputAt = At("2026-10-19 17:59:20");
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-19 17:59:00");
        await rig.InputAsync("2026-10-19 18:04:59");
        await rig.SignalAsync(Locked, "2026-10-19 18:05:00");
        await rig.SignalAsync(Suspending, "2026-10-19 18:06:00");
        rig.TimePassesUnseen("2026-10-20 07:59:50");
        await rig.SignalAsync(Resumed, "2026-10-20 07:59:50");
    }

    private static bool Near(DateTimeOffset expected, DateTimeOffset actual) => (actual - expected).Duration() <= Minute;

    private static void Within(DateTimeOffset expected, DateTimeOffset? actual)
    {
        Assert.NotNull(actual);
        Assert.True(Near(expected, actual.Value), $"Expected {expected:HH:mm:ss} ± 60 s, was {actual:HH:mm:ss}.");
    }

    private static void Within(TimeSpan expected, TimeSpan actual) =>
        Assert.True((actual - expected).Duration() <= Minute, $"Expected {expected} ± 60 s, was {actual}.");
}
