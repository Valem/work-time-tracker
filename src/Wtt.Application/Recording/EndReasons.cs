using Wtt.Application.Ports;
using Wtt.Domain.Facts;

namespace Wtt.Application.Recording;

/// <summary>Why a use ended, and the length of the timer when a timer ended it.</summary>
public sealed record EndReason(EndCause Cause, TimeSpan? Timer = null);

/// <summary>
/// Names what ended a use from what the Recorder saw at that moment: a lid closed just before, or how long
/// the session had been without input against the timers in force. Windows does not say which timer fired,
/// so the one whose length matches the idle time is taken; when none matches — the display was held on, say
/// by a video — the shortest timer already due is, as it is the first to fire once the display is released.
/// </summary>
public static class EndReasons
{
    /// <summary>How far the idle time may lie off a timer's length and still be that timer firing.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(30);

    /// <summary>A lid closed this shortly before a lock or a standby caused it.</summary>
    public static readonly TimeSpan LidWindow = TimeSpan.FromSeconds(30);

    public static EndReason ForLock(TimeSpan idle, IdleTimers timers, bool lidJustClosed) =>
        lidJustClosed
            ? new EndReason(EndCause.Lid)
            : FiredTimer(idle, LockTimers(timers)) ?? new EndReason(EndCause.UserLock);

    /// <summary>A standby neither a lid nor a timer explains was asked for — power button or Sleep — and is not told apart.</summary>
    public static EndReason ForStandby(TimeSpan idle, IdleTimers timers, bool lidJustClosed) =>
        lidJustClosed
            ? new EndReason(EndCause.Lid)
            : FiredTimer(idle, StandbyTimers(timers)) ?? new EndReason(EndCause.Unknown);

    /// <summary>The idle lock timeout in force: the shortest timer that locks the session, or null when none does.</summary>
    public static EndReason? IdleLockInForce(IdleTimers timers) =>
        LockTimers(timers).OrderBy(reason => reason.Timer).FirstOrDefault();

    private static EndReason? FiredTimer(TimeSpan idle, IEnumerable<EndReason> candidates)
    {
        var due = candidates.Where(reason => reason.Timer!.Value - Tolerance <= idle).ToList();
        if (due.Count == 0)
        {
            return null;
        }

        var matching = due
            .Where(reason => (idle - reason.Timer!.Value).Duration() <= Tolerance)
            .OrderBy(reason => (idle - reason.Timer!.Value).Duration())
            .FirstOrDefault();
        return matching ?? due.OrderBy(reason => reason.Timer).First();
    }

    private static IEnumerable<EndReason> LockTimers(IdleTimers timers) => Present(
        (EndCause.ScreenSaverLock, timers.ScreenSaverLock),
        (EndCause.MachineInactivityLimit, timers.MachineInactivityLimit),
        (EndCause.IdleLock, timers.DisplayOff),
        (EndCause.IdleLock, timers.Standby));

    private static IEnumerable<EndReason> StandbyTimers(IdleTimers timers) => Present(
        (EndCause.DisplayOffStandby, timers.DisplayOff),
        (EndCause.DisplayOffStandby, timers.Standby));

    private static IEnumerable<EndReason> Present(params (EndCause Cause, TimeSpan? Timer)[] timers) =>
        timers
            .Where(timer => timer.Timer is { } length && length > TimeSpan.Zero)
            .Select(timer => new EndReason(timer.Cause, timer.Timer));
}
