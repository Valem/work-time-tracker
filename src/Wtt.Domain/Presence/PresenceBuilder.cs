using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;

namespace Wtt.Domain.Presence;

public sealed class PresenceBuilder : IPresenceBuilder
{
    public PresenceTimeline Build(
        Interval range,
        IReadOnlyList<ActivityFact> facts,
        IReadOnlyList<CoverageSpan> coverage,
        DateTimeOffset? winlogonHistoryFrom)
    {
        var effective = FactSelection.Select(facts.Where(fact => range.Contains(fact.At)), coverage);

        var tracker = new UseTracker(winlogonHistoryFrom);
        foreach (var fact in effective)
        {
            tracker.Apply(fact);
        }

        var uses = tracker.Finish(range.End);
        return new PresenceTimeline(uses, PausesBetween(uses, Wakes(effective)));
    }

    /// <summary>
    /// One wake per return from standby: Windows may report the same resume several times
    /// (Kernel-Power 107 and 507, Power-Troubleshooter 1), so only the first exit after a standby counts.
    /// </summary>
    private static List<DateTimeOffset> Wakes(IEnumerable<EffectiveFact> effective)
    {
        var wakes = new List<DateTimeOffset>();
        var awake = false;
        foreach (var fact in effective.Select(effective => effective.Fact))
        {
            if (fact.Kind == FactKind.StandbyEnter)
            {
                awake = false;
            }
            else if (fact.Kind == FactKind.StandbyExit && !awake)
            {
                wakes.Add(fact.At);
                awake = true;
            }
        }

        return wakes;
    }

    private static List<Pause> PausesBetween(IReadOnlyList<UseSpan> uses, IReadOnlyList<DateTimeOffset> wakes)
    {
        var pauses = new List<Pause>();
        for (var i = 1; i < uses.Count; i++)
        {
            var start = uses[i - 1].End;
            var end = uses[i].Start;
            if (end <= start)
            {
                continue;
            }

            var ignored = wakes.Where(wake => wake > start && wake < end).ToList();
            pauses.Add(new Pause(start, end, ignored));
        }

        return pauses;
    }
}
