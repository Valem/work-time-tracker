namespace Wtt.Domain.Facts;

public enum FactKind
{
    Unlock,
    Lock,
    SignIn,
    SignOut,
    StandbyEnter,
    StandbyExit,
    Boot,
    Shutdown,
    LastInput,
    IdleTimeout,
}
