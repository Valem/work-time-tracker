using Wtt.Application.Ports;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;

namespace Wtt.Application.Tests;

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");

    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

internal sealed class InMemoryFactJournal : IFactJournal
{
    private readonly Dictionary<string, ActivityFact> facts = [];

    public Task<int> AppendAsync(IReadOnlyList<ActivityFact> newFacts) =>
        Task.FromResult(newFacts.Count(fact => facts.TryAdd(fact.SourceKey, fact)));

    public Task<IReadOnlyList<ActivityFact>> ReadAsync(Interval range) =>
        Task.FromResult<IReadOnlyList<ActivityFact>>(facts.Values.Where(fact => range.Contains(fact.At)).OrderBy(fact => fact.At).ToList());

    public Task<DateTimeOffset?> EarliestAsync(FactSource? source = null)
    {
        var matching = facts.Values.Where(fact => source is null || fact.Source == source).ToList();
        return Task.FromResult<DateTimeOffset?>(matching.Count == 0 ? null : matching.Min(fact => fact.At));
    }
}

internal sealed class InMemoryCoverageLog : ICoverageLog
{
    public List<CoverageSpan> Spans { get; } = [];

    public Task BeginAsync(DateTimeOffset signedInAt) => throw new NotSupportedException();

    public Task ExtendAsync(DateTimeOffset now) => throw new NotSupportedException();

    public Task ResumeAsync(DateTimeOffset suspendedAt, DateTimeOffset resumedAt) => throw new NotSupportedException();

    public Task<IReadOnlyList<CoverageSpan>> ReadAsync(Interval range) =>
        Task.FromResult<IReadOnlyList<CoverageSpan>>(Spans.Where(span => span.To >= range.Start && span.From < range.End).ToList());
}

internal sealed class FixedLunchRules(LunchRule rule) : ILunchRules
{
    public Task<LunchRule> InForceOnAsync(DateOnly date) => Task.FromResult(rule);

    public Task<LunchRule> SaveAsync(TimeRange window, IReadOnlyList<BreakTier> tiers) => throw new NotSupportedException();
}

internal sealed class FakeHistorySource(IReadOnlyList<ActivityFact> facts) : IHistorySource
{
    public List<HistoryCursor> CursorsSeen { get; } = [];

    public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user)
    {
        CursorsSeen.Add(cursor);
        var since = facts.Where(fact => cursor.ReadFrom is null || fact.At >= cursor.ReadFrom).ToList();
        var next = since.Count == 0 ? cursor : new HistoryCursor(since.Max(fact => fact.At), "1");
        return Task.FromResult(new HistoryBatch(since, next));
    }
}

internal sealed class InMemoryCursorStore : IHistoryCursorStore
{
    public HistoryCursor Cursor { get; private set; } = HistoryCursor.Beginning;

    public Task<HistoryCursor> LoadAsync() => Task.FromResult(Cursor);

    public Task SaveAsync(HistoryCursor cursor)
    {
        Cursor = cursor;
        return Task.CompletedTask;
    }
}
