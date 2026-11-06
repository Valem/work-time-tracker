using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;

namespace Wtt.Domain.Presence;

public interface IPresenceBuilder
{
    /// <param name="range">The instants the facts were read for; a use still open at its end closes there.</param>
    /// <param name="facts">Every fact inside <paramref name="range"/>.</param>
    /// <param name="coverage">The Recorder's coverage spans overlapping <paramref name="range"/>.</param>
    /// <param name="winlogonHistoryFrom">
    /// The oldest Winlogon history fact ever kept: before it Windows holds no lock/unlock record, so
    /// a person-reason standby exit may stand in for an unlock (2-minute test). Null when there is none.
    /// </param>
    PresenceTimeline Build(
        Interval range,
        IReadOnlyList<ActivityFact> facts,
        IReadOnlyList<CoverageSpan> coverage,
        DateTimeOffset? winlogonHistoryFrom);
}
