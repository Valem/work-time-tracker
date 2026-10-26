using Wtt.Application.Ports;

namespace Wtt.Application.Recording;

public enum LiveSignalKind
{
    Locked,
    Unlocked,
    Suspending,
    Resumed,
    LidClosed,
    LidOpened,
    SigningOut,
    ShuttingDown,
}

/// <summary>
/// A session or power notification as Windows delivered it: the instant it arrived, with the UTC offset in force
/// then, and what the session looked like at that instant. The signal may be recorded later — queued behind an
/// import, or after Windows froze the process for standby — so nothing about the moment is read when recording.
/// </summary>
/// <param name="Context">Null when it could not be read on arrival; the Recorder then reads it when recording.</param>
public sealed record LiveSignal(LiveSignalKind Kind, DateTimeOffset At, SignalContext? Context = null);

/// <summary>The session's last real input and the timers in force when a signal arrived.</summary>
public sealed record SignalContext(DateTimeOffset LastInput, IdleTimers Timers);
