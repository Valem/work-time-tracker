using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;

namespace Wtt.Domain.Presence;

internal sealed record EffectiveFact(ActivityFact Fact, bool Rebuilt);

/// <summary>
/// Picks the facts that count: live facts always; history facts outside the Recorder's coverage
/// (they make a day rebuilt); and history facts between a coverage span's sign-in and the Recorder's
/// own first fact, which fill that stretch without marking the day. A sign-in the Recorder recorded
/// counts once, as its live fact.
/// </summary>
internal static class FactSelection
{
    /// <summary>
    /// Windows logs a sign-in in its history by one clock and reports the session's logon time, which the Recorder
    /// records and starts coverage from, by another: the history record comes tens of milliseconds earlier. A history
    /// sign-in this close to a live one is that same sign-in, not use before coverage.
    /// </summary>
    private static readonly TimeSpan SameSignIn = TimeSpan.FromSeconds(1);

    public static IReadOnlyList<EffectiveFact> Select(
        IEnumerable<ActivityFact> facts,
        IReadOnlyList<CoverageSpan> coverage)
    {
        var factList = facts.ToList();
        var spans = coverage
            .Select(span => (Span: span, RecorderStart: RecorderStart(span, factList)))
            .ToList();
        var liveSignIns = factList
            .Where(fact => fact.IsLive && fact.Kind == FactKind.SignIn)
            .Select(fact => fact.At)
            .ToList();

        return factList
            .Where(fact => !IsRecordedLive(fact, liveSignIns))
            .Select(fact => Classify(fact, spans))
            .OfType<EffectiveFact>()
            .OrderBy(effective => effective.Fact.At)
            .ThenBy(effective => effective.Fact.IsLive ? 0 : 1)
            .ToList();
    }

    private static bool IsRecordedLive(ActivityFact fact, IReadOnlyList<DateTimeOffset> liveSignIns) =>
        !fact.IsLive
        && fact.Kind == FactKind.SignIn
        && liveSignIns.Any(live => (live - fact.At).Duration() <= SameSignIn);

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
