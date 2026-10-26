using Wtt.Application.Recording;

namespace Wtt.Infrastructure.Live;

/// <summary>What the session ending means for the Recorder.</summary>
internal enum SessionEnding
{
    /// <summary>The end was cancelled, or this message does not end anything.</summary>
    None,

    SigningOut,
    ShuttingDown,

    /// <summary>An installer or Restart Manager asks the program to close; the session goes on.</summary>
    CloseRequested,
}

/// <summary>The window messages the Recorder listens to, and what each one tells it.</summary>
internal static class WindowsSignals
{
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_QUERYENDSESSION = 0x0011;
    public const uint WM_ENDSESSION = 0x0016;
    public const uint WM_TIMECHANGE = 0x001E;
    public const uint WM_POWERBROADCAST = 0x0218;
    public const uint WM_WTSSESSION_CHANGE = 0x02B1;

    public const uint WTS_SESSION_LOCK = 0x7;
    public const uint WTS_SESSION_UNLOCK = 0x8;

    public const uint PBT_APMSUSPEND = 0x4;
    public const uint PBT_APMRESUMECRITICAL = 0x6;
    public const uint PBT_APMRESUMESUSPEND = 0x7;
    public const uint PBT_APMRESUMEAUTOMATIC = 0x12;
    public const uint PBT_POWERSETTINGCHANGE = 0x8013;

    public const long ENDSESSION_CLOSEAPP = 0x1;
    public const long ENDSESSION_LOGOFF = 0x80000000;

    /// <summary>GUID_LIDSWITCH_STATE_CHANGE: data 0 = closed, 1 = open.</summary>
    public static readonly Guid LidSwitchStateChange = new("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

    public static LiveSignalKind? FromSessionChange(ulong code) => code switch
    {
        WTS_SESSION_LOCK => LiveSignalKind.Locked,
        WTS_SESSION_UNLOCK => LiveSignalKind.Unlocked,
        _ => null,
    };

    public static LiveSignalKind? FromPowerBroadcast(ulong eventId) => eventId switch
    {
        PBT_APMSUSPEND => LiveSignalKind.Suspending,
        PBT_APMRESUMEAUTOMATIC or PBT_APMRESUMESUSPEND or PBT_APMRESUMECRITICAL => LiveSignalKind.Resumed,
        _ => null,
    };

    public static LiveSignalKind? FromPowerSetting(Guid setting, uint data) =>
        setting == LidSwitchStateChange
            ? data == 0 ? LiveSignalKind.LidClosed : LiveSignalKind.LidOpened
            : null;

    public static SessionEnding FromEndSession(bool ending, long flags)
    {
        if (!ending)
        {
            return SessionEnding.None;
        }

        if ((flags & ENDSESSION_LOGOFF) != 0)
        {
            return SessionEnding.SigningOut;
        }

        return (flags & ENDSESSION_CLOSEAPP) != 0 ? SessionEnding.CloseRequested : SessionEnding.ShuttingDown;
    }
}
