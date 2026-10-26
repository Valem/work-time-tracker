using Wtt.Domain.Coverage;
using Wtt.Domain.Time;

namespace Wtt.Application.Ports;

/// <summary>When the Recorder ran: spans from the session's sign-in, extended by its heartbeat.</summary>
public interface ICoverageLog
{
    /// <summary>A heartbeat later than this after the previous one starts a new span: the Recorder was not running.</summary>
    public static readonly TimeSpan MaxHeartbeatGap = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The Recorder starts in a session signed in at <paramref name="signedInAt"/>. Coverage reaches back to that
    /// sign-in, unless this session was covered before: then only a gap shorter than <see cref="MaxHeartbeatGap"/>
    /// is bridged, and a longer one stays uncovered for history to fill.
    /// </summary>
    Task BeginAsync(DateTimeOffset signedInAt);

    Task ExtendAsync(DateTimeOffset now);

    /// <summary>Bridges a standby whose start and end the Recorder saw.</summary>
    Task ResumeAsync(DateTimeOffset suspendedAt, DateTimeOffset resumedAt);

    Task<IReadOnlyList<CoverageSpan>> ReadAsync(Interval range);
}
