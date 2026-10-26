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

/// <summary>A session or power notification as Windows delivered it, at the instant it arrived.</summary>
public sealed record LiveSignal(LiveSignalKind Kind, DateTimeOffset At);
