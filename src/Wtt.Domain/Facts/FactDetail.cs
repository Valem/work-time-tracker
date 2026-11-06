namespace Wtt.Domain.Facts;

/// <summary>
/// What a fact says beyond its kind: why a use ended (and the timer that fired), why the laptop
/// woke or went to standby — named, and as Windows reported it — and the sign-in session it belongs to.
/// </summary>
public sealed record FactDetail(
    EndCause? Cause = null,
    TimeSpan? Timer = null,
    string? Reason = null,
    string? Session = null,
    string? RawReason = null)
{
    public static readonly FactDetail None = new();

    public bool IsTimerEnd => Cause is { } cause && cause.IsTimer() && Timer is not null;
}
