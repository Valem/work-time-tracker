namespace Wtt.Domain.Facts;

public enum EndCause
{
    Unknown,
    UserLock,
    Lid,
    PowerButton,
    SignOut,
    Shutdown,
    IdleLock,
    ScreenSaverLock,
    DisplayOffStandby,
    MachineInactivityLimit,
}

public static class EndCauseExtensions
{
    public static bool IsTimer(this EndCause cause) => cause is
        EndCause.IdleLock or
        EndCause.ScreenSaverLock or
        EndCause.DisplayOffStandby or
        EndCause.MachineInactivityLimit;
}
