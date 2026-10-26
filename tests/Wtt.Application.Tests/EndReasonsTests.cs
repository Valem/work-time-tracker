using Wtt.Application.Ports;
using Wtt.Application.Recording;
using Wtt.Domain.Facts;

namespace Wtt.Application.Tests;

public sealed class EndReasonsTests
{
    private static readonly IdleTimers Managed = new(
        DisplayOff: TimeSpan.FromMinutes(5),
        Standby: TimeSpan.FromMinutes(15),
        ScreenSaverLock: TimeSpan.FromMinutes(10),
        MachineInactivityLimit: TimeSpan.FromMinutes(20));

    [Fact]
    public void A_lock_right_after_input_is_the_person_locking()
    {
        Assert.Equal(new EndReason(EndCause.UserLock), EndReasons.ForLock(TimeSpan.FromSeconds(1), Managed, lidJustClosed: false));
    }

    [Theory]
    [InlineData(5 * 60, EndCause.IdleLock, 5)]
    [InlineData(10 * 60 + 20, EndCause.ScreenSaverLock, 10)]
    [InlineData(20 * 60 - 25, EndCause.MachineInactivityLimit, 20)]
    public void A_lock_whose_idle_time_matches_a_timer_is_that_timer(int idleSeconds, EndCause cause, int timerMinutes)
    {
        var reason = EndReasons.ForLock(TimeSpan.FromSeconds(idleSeconds), Managed, lidJustClosed: false);

        Assert.Equal(new EndReason(cause, TimeSpan.FromMinutes(timerMinutes)), reason);
    }

    [Fact]
    public void A_lock_long_after_every_timer_was_due_is_the_shortest_timer_firing_once_the_display_was_released()
    {
        var reason = EndReasons.ForLock(TimeSpan.FromMinutes(68), Managed, lidJustClosed: false);

        Assert.Equal(new EndReason(EndCause.IdleLock, TimeSpan.FromMinutes(5)), reason);
    }

    [Fact]
    public void A_lid_closed_just_before_ends_use_by_the_lid_whatever_the_idle_time()
    {
        Assert.Equal(new EndReason(EndCause.Lid), EndReasons.ForLock(TimeSpan.FromMinutes(30), Managed, lidJustClosed: true));
        Assert.Equal(new EndReason(EndCause.Lid), EndReasons.ForStandby(TimeSpan.FromMinutes(30), Managed, lidJustClosed: true));
    }

    [Fact]
    public void A_standby_after_the_display_or_standby_timeout_is_display_off_standby()
    {
        Assert.Equal(
            new EndReason(EndCause.DisplayOffStandby, TimeSpan.FromMinutes(5)),
            EndReasons.ForStandby(TimeSpan.FromMinutes(5), Managed, lidJustClosed: false));
        Assert.Equal(
            new EndReason(EndCause.DisplayOffStandby, TimeSpan.FromMinutes(15)),
            EndReasons.ForStandby(TimeSpan.FromMinutes(15), Managed, lidJustClosed: false));
    }

    [Fact]
    public void A_standby_no_lid_or_timer_explains_is_unknown_and_so_ends_at_its_own_time()
    {
        Assert.Equal(new EndReason(EndCause.Unknown), EndReasons.ForStandby(TimeSpan.FromMinutes(2), Managed, lidJustClosed: false));
    }

    [Fact]
    public void Without_timers_every_lock_is_the_person_and_no_idle_lock_is_in_force()
    {
        Assert.Equal(new EndReason(EndCause.UserLock), EndReasons.ForLock(TimeSpan.FromHours(3), IdleTimers.None, lidJustClosed: false));
        Assert.Null(EndReasons.IdleLockInForce(IdleTimers.None));
        Assert.Null(EndReasons.IdleLockInForce(new IdleTimers(DisplayOff: TimeSpan.Zero)));
    }

    [Fact]
    public void The_idle_lock_in_force_is_the_shortest_timer_that_locks()
    {
        var timers = Managed with { DisplayOff = null, Standby = null };

        Assert.Equal(new EndReason(EndCause.ScreenSaverLock, TimeSpan.FromMinutes(10)), EndReasons.IdleLockInForce(timers));
    }
}
