using Wtt.Application.Ports;
using Wtt.Application.Recording;
using Wtt.Infrastructure.Live;
using static Wtt.Infrastructure.Live.WindowsSignals;

namespace Wtt.Infrastructure.Tests;

public sealed class LiveSignalTests
{
    [Theory]
    [InlineData(WTS_SESSION_LOCK, LiveSignalKind.Locked)]
    [InlineData(WTS_SESSION_UNLOCK, LiveSignalKind.Unlocked)]
    public void Session_notifications_become_lock_and_unlock(uint code, LiveSignalKind expected) =>
        Assert.Equal(expected, FromSessionChange(code));

    [Theory]
    [InlineData(0x1u)]
    [InlineData(0x2u)]
    [InlineData(0x5u)]
    [InlineData(0x6u)]
    public void Other_session_notifications_are_ignored(uint code) => Assert.Null(FromSessionChange(code));

    [Theory]
    [InlineData(PBT_APMSUSPEND, LiveSignalKind.Suspending)]
    [InlineData(PBT_APMRESUMEAUTOMATIC, LiveSignalKind.Resumed)]
    [InlineData(PBT_APMRESUMESUSPEND, LiveSignalKind.Resumed)]
    [InlineData(PBT_APMRESUMECRITICAL, LiveSignalKind.Resumed)]
    public void Power_notifications_become_suspend_and_resume(uint eventId, LiveSignalKind expected) =>
        Assert.Equal(expected, FromPowerBroadcast(eventId));

    [Fact]
    public void A_power_status_change_is_ignored() => Assert.Null(FromPowerBroadcast(0xA));

    [Fact]
    public void The_lid_switch_setting_tells_closed_from_open()
    {
        Assert.Equal(LiveSignalKind.LidClosed, FromPowerSetting(LidSwitchStateChange, 0));
        Assert.Equal(LiveSignalKind.LidOpened, FromPowerSetting(LidSwitchStateChange, 1));
        Assert.Null(FromPowerSetting(Guid.NewGuid(), 0));
    }

    [Theory]
    [InlineData(true, 0x80000000L, nameof(SessionEnding.SigningOut))]
    [InlineData(true, 0x0L, nameof(SessionEnding.ShuttingDown))]
    [InlineData(true, 0x40000000L, nameof(SessionEnding.ShuttingDown))]
    [InlineData(true, 0x1L, nameof(SessionEnding.CloseRequested))]
    [InlineData(false, 0x80000000L, nameof(SessionEnding.None))]
    public void The_end_of_the_session_tells_sign_out_from_shutdown_from_an_installer_closing_the_program(bool ending, long flags, string expected) =>
        Assert.Equal(expected, FromEndSession(ending, flags).ToString());

    [Fact]
    public void Timer_settings_of_zero_are_off_and_the_screen_saver_locks_only_when_active_and_secure()
    {
        Assert.Equal(
            new IdleTimers(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20)),
            IdleTimerSettings.From(300, 900, screenSaverActive: true, screenSaverSecure: true, 600, 1200));
        Assert.Equal(IdleTimers.None, IdleTimerSettings.From(0, 0, screenSaverActive: true, screenSaverSecure: false, 600, 0));
        Assert.Equal(IdleTimers.None, IdleTimerSettings.From(0, 0, screenSaverActive: false, screenSaverSecure: true, 600, 0));
    }

    /// <summary>Mechanics only, on this machine's own session: nothing read is printed or kept.</summary>
    [Fact]
    public void The_live_desktop_answers_for_the_running_session()
    {
        var clock = TimeProvider.System;
        var desktop = new WindowsDesktop(clock);

        var session = desktop.CurrentSession();
        var timers = desktop.TimersInForce();

        Assert.Equal(System.Diagnostics.Process.GetCurrentProcess().SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture), session.Id);
        Assert.True(session.SignedInAt <= clock.GetUtcNow());
        Assert.NotNull(timers);
    }
}
