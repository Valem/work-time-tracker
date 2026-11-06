using Wtt.Domain.Facts;
using Wtt.Domain.Time;

namespace Wtt.Application.Ports;

/// <summary>The append-only fact journal, deduplicated by source key.</summary>
public interface IFactJournal
{
    /// <returns>How many of the facts were new.</returns>
    Task<int> AppendAsync(IReadOnlyList<ActivityFact> facts);

    Task<IReadOnlyList<ActivityFact>> ReadAsync(Interval range);

    /// <summary>The instant of the oldest fact kept, of one source or of any.</summary>
    Task<DateTimeOffset?> EarliestAsync(FactSource? source = null);
}
