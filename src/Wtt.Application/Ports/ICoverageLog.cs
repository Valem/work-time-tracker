using Wtt.Domain.Coverage;
using Wtt.Domain.Time;

namespace Wtt.Application.Ports;

/// <summary>When the Recorder ran: spans from the session's sign-in, extended by its heartbeat.</summary>
public interface ICoverageLog
{
    /// <summary>A heartbeat later than this after the previous one starts a new span: the Recorder was not running.</summary>
    public static readonly TimeSpan MaxHeartbeatGap = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A first start later than this after the sign-in was not the autostart at sign-in (first install, Run entry
    /// off, a restart by the App): the stretch before it stays uncovered, for history to fill as rebuilt.
    /// </summary>
    public static readonly TimeSpan MaxAutostartDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The Recorder starts in a session signed in at <paramref name="signedInAt"/>. On its first start in that
    /// session, coverage reaches back to the sign-in when the start came within <see cref="MaxAutostartDelay"/> of
    /// it, and begins now otherwise. When this session was covered before, only a gap shorter than
    /// <see cref="MaxHeartbeatGap"/> is bridged, and a longer one stays uncovered for history to fill.
    /// </summary>
    /// <returns>Where the span this start opens or continues begins.</returns>
    Task<DateTimeOffset> BeginAsync(DateTimeOffset signedInAt);

    Task ExtendAsync(DateTimeOffset now);

    /// <summary>Bridges a standby whose start and end the Recorder saw.</summary>
    Task ResumeAsync(DateTimeOffset suspendedAt, DateTimeOffset resumedAt);

    Task<IReadOnlyList<CoverageSpan>> ReadAsync(Interval range);
}
