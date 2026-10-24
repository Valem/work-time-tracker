using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;

namespace Wtt.Domain.Presence;

internal sealed record EffectiveFact(ActivityFact Fact, bool Rebuilt);

/// <summary>
/// Picks the facts that count: live facts always; history facts outside the Recorder's coverage
/// (they make a day rebuilt); and history facts between a coverage span's sign-in and the Recorder's
/// own first fact, which fill that stretch without marking the day.
/// </summary>
internal static class FactSelection
{
    public static IReadOnlyList<EffectiveFact> Select(
        IEnumerable<ActivityFact> facts,
        IReadOnlyList<CoverageSpan> coverage)
    {
        var factList = facts.ToList();
        var spans = coverage
            .Select(span => (Span: span, RecorderStart: RecorderStart(span, factList)))
            .ToList();

        return factList
            .Select(fact => Classify(fact, spans))
            .OfType<EffectiveFact>()
            .OrderBy(effective => effective.Fact.At)
            .ThenBy(effective => effective.Fact.IsLive ? 0 : 1)
            .ToList();
    }

    private static EffectiveFact? Classify(
        ActivityFact fact,
        IReadOnlyList<(CoverageSpan Span, DateTimeOffset RecorderStart)> spans)
    {
        if (fact.IsLive)
        {
            return new EffectiveFact(fact, Rebuilt: false);
        }

        var covering = spans.Where(entry => entry.Span.Contains(fact.At)).ToList();
        if (covering.Count == 0)
        {
            return new EffectiveFact(fact, Rebuilt: true);
        }

        var beforeRecorderStarted = covering.All(entry => fact.At < entry.RecorderStart);
        return beforeRecorderStarted ? new EffectiveFact(fact, Rebuilt: false) : null;
    }

    /// <summary>
    /// A coverage span starts at the session's sign-in, which the Recorder back-dates; its own first
    /// fact other than that sign-in marks when it actually ran.
    /// </summary>
    private static DateTimeOffset RecorderStart(CoverageSpan span, IReadOnlyList<ActivityFact> facts)
    {
        var ownFacts = facts
            .Where(fact => fact.IsLive && fact.Kind != FactKind.SignIn && span.Contains(fact.At))
            .Select(fact => fact.At)
            .ToList();
        return ownFacts.Count == 0 ? span.To : ownFacts.Min();
    }
}
