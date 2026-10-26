namespace Wtt.Application.Ports;

/// <summary>The Windows sign-in session the Recorder runs in.</summary>
public sealed record SignInSession(string Id, DateTimeOffset SignedInAt, bool Locked);

/// <summary>
/// The timers in force that can end use without the person acting; null where a timer is off.
/// Display-off and standby follow the current power source.
/// </summary>
public sealed record IdleTimers(
    TimeSpan? DisplayOff = null,
    TimeSpan? Standby = null,
    TimeSpan? ScreenSaverLock = null,
    TimeSpan? MachineInactivityLimit = null)
{
    public static readonly IdleTimers None = new();
}

/// <summary>What the Recorder asks Windows about its own session while it runs.</summary>
public interface IDesktop
{
    SignInSession CurrentSession();

    /// <summary>The instant of the session's last real keyboard, mouse, pen or touch input.</summary>
    DateTimeOffset LastInput();

    IdleTimers TimersInForce();
}

/// <summary>Where a background process notes what went wrong; nothing is ever sent anywhere.</summary>
public interface IErrorLog
{
    void Write(string context, Exception error);
}
