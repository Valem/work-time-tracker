using System.ComponentModel;
using System.Globalization;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.System.RemoteDesktop;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtt.Application.Ports;

namespace Wtt.Infrastructure.Live;

/// <summary>Asks Windows about the Recorder's own session: its sign-in, its last input and the timers in force.</summary>
public sealed class WindowsDesktop(TimeProvider clock) : IDesktop
{
    private const string PolicyKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const int WTS_SESSIONSTATE_LOCK = 0;

    public SignInSession CurrentSession()
    {
        if (!PInvoke.ProcessIdToSessionId((uint)Environment.ProcessId, out var sessionId))
        {
            throw new Win32Exception();
        }

        var (signedInAt, locked) = SessionInfo(sessionId);
        return new SignInSession(sessionId.ToString(CultureInfo.InvariantCulture), signedInAt ?? clock.GetUtcNow(), locked);
    }

    public DateTimeOffset LastInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>() };
        if (!PInvoke.GetLastInputInfo(ref info))
        {
            throw new Win32Exception();
        }

        var now = clock.GetUtcNow();
        var elapsed = unchecked((uint)Environment.TickCount - info.dwTime);
        return now - TimeSpan.FromMilliseconds(elapsed);
    }

    public IdleTimers TimersInForce()
    {
        var (displayOff, standby) = PowerPolicyTimers();
        return IdleTimerSettings.From(
            displayOff,
            standby,
            ScreenSaverParameter(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSCREENSAVEACTIVE) != 0,
            ScreenSaverParameter(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSCREENSAVESECURE) != 0,
            ScreenSaverParameter(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSCREENSAVETIMEOUT),
            MachineInactivityLimitSeconds());
    }

    private static int MachineInactivityLimitSeconds()
    {
        using var policy = Registry.LocalMachine.OpenSubKey(PolicyKey);
        return policy?.GetValue("InactivityTimeoutSecs") as int? ?? 0;
    }

    private static unsafe (DateTimeOffset? SignedInAt, bool Locked) SessionInfo(uint sessionId)
    {
        if (!PInvoke.WTSQuerySessionInformation(HANDLE.Null, sessionId, WTS_INFO_CLASS.WTSSessionInfoEx, out var buffer, out _))
        {
            throw new Win32Exception();
        }

        try
        {
            var info = (WTSINFOEXW*)buffer.Value;
            var level1 = info->Data.WTSInfoExLevel1;
            DateTimeOffset? signedInAt = level1.LogonTime > 0 ? DateTimeOffset.FromFileTime(level1.LogonTime).ToUniversalTime() : null;
            return (signedInAt, level1.SessionFlags == WTS_SESSIONSTATE_LOCK);
        }
        finally
        {
            PInvoke.WTSFreeMemory(buffer.Value);
        }
    }

    private static unsafe (uint DisplayOffSeconds, uint StandbySeconds) PowerPolicyTimers()
    {
        SYSTEM_POWER_POLICY policy;
        var status = PInvoke.CallNtPowerInformation(
            POWER_INFORMATION_LEVEL.SystemPowerPolicyCurrent, null, 0, &policy, (uint)sizeof(SYSTEM_POWER_POLICY));
        if (status != 0)
        {
            return (0, 0);
        }

        var standby = policy.Idle.Action == POWER_ACTION.PowerActionNone ? 0 : policy.IdleTimeout;
        return (policy.VideoTimeout, standby);
    }

    private static unsafe int ScreenSaverParameter(SYSTEM_PARAMETERS_INFO_ACTION action)
    {
        var value = 0;
        return PInvoke.SystemParametersInfo(action, 0, &value, 0) ? value : 0;
    }
}

/// <summary>Turns the timer settings Windows reports, in seconds with 0 for off, into the timers in force.</summary>
internal static class IdleTimerSettings
{
    public static IdleTimers From(
        uint displayOffSeconds,
        uint standbySeconds,
        bool screenSaverActive,
        bool screenSaverSecure,
        int screenSaverSeconds,
        int inactivityLimitSeconds) => new(
            Seconds(displayOffSeconds),
            Seconds(standbySeconds),
            screenSaverActive && screenSaverSecure ? Seconds(screenSaverSeconds) : null,
            Seconds(inactivityLimitSeconds));

    private static TimeSpan? Seconds(long seconds) => seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
}
