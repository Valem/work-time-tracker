namespace Wtt.Domain.Coverage;

/// <summary>A stretch during which the Recorder ran; it starts at the session's sign-in.</summary>
public sealed record CoverageSpan(DateTimeOffset From, DateTimeOffset To)
{
    public bool Contains(DateTimeOffset instant) => instant >= From && instant <= To;
}
