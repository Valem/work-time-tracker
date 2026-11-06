using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Time;

namespace Wtt.Application.Queries;

public enum HistoryView
{
    /// <summary>Every fact kept.</summary>
    AsRecorded,

    /// <summary>Without the Winlogon facts, as days older than the Winlogon log are rebuilt.</summary>
    AsIfBeyondWinlogonLog,
}

/// <summary>
/// Reads the facts around a run of dates and builds their presence. The margins let a day see the
/// use before it (midnight carry-over) and after it, whatever the UTC offsets; reading stops at now.
/// </summary>
public sealed class PresenceReader(
    IFactJournal journal,
    ICoverageLog coverage,
    IPresenceBuilder builder,
    TimeProvider clock)
{
    private static readonly TimeSpan MarginBefore = TimeSpan.FromDays(7);
    private static readonly TimeSpan MarginAfter = TimeSpan.FromDays(3);

    public async Task<PresenceTimeline> AroundAsync(DateOnly first, DateOnly last, HistoryView view = HistoryView.AsRecorded)
    {
        var start = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) - MarginBefore;
        var end = DateTimeOffsetExtensions.Min(
            new DateTimeOffset(last.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) + MarginAfter,
            clock.GetUtcNow());
        if (end <= start)
        {
            return PresenceTimeline.Empty;
        }

        var range = new Interval(start, end);
        var facts = await journal.ReadAsync(range);
        var spans = await coverage.ReadAsync(range);
        if (view == HistoryView.AsIfBeyondWinlogonLog)
        {
            var withoutWinlogon = facts.Where(fact => fact.Source != FactSource.WinlogonHistory).ToList();
            return builder.Build(range, withoutWinlogon, spans, winlogonHistoryFrom: null);
        }

        var winlogonFrom = await journal.EarliestAsync(FactSource.WinlogonHistory);
        return builder.Build(range, facts, spans, winlogonFrom);
    }
}
