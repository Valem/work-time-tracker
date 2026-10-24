using Wtt.Domain.Presence;
using Wtt.Domain.Time;

namespace Wtt.Domain.Workdays;

/// <summary>The part of a use span that falls on one local date, at whole-minute precision.</summary>
internal sealed record DayPiece(DateOnly Date, DateTimeOffset Start, DateTimeOffset End, Precision Precision)
{
    private static readonly TimeSpan ClockChangeFrom = TimeSpan.FromHours(2);
    private static readonly TimeSpan ClockChangeUntil = TimeSpan.FromHours(3);

    /// <summary>Splits each use at the local midnights it crosses.</summary>
    public static IReadOnlyList<DayPiece> SplitAtMidnight(IEnumerable<UseSpan> uses)
    {
        var pieces = new List<DayPiece>();
        foreach (var use in uses)
        {
            var spanStart = use.Start.RoundToMinute();
            var end = DateTimeOffsetExtensions.Max(spanStart, use.End.RoundToMinute());
            var start = spanStart;
            for (var midnight = MidnightAfter(start, spanStart, end.Offset); end > midnight; midnight = MidnightAfter(start, spanStart, end.Offset))
            {
                pieces.Add(new DayPiece(start.LocalDate(), start, midnight, use.Precision));
                start = midnight;
            }

            pieces.Add(new DayPiece(start.LocalDate(), start, end, use.Precision));
        }

        return pieces;
    }

    /// <summary>
    /// The midnight after <paramref name="pieceStart"/>. When the span's start and end offsets differ,
    /// the clock change is taken to happen in the first local 02:00–03:00 (at the start's offset) after
    /// the span's start: a midnight before it keeps the start's offset, a midnight after it takes the end's.
    /// </summary>
    private static DateTimeOffset MidnightAfter(DateTimeOffset pieceStart, DateTimeOffset spanStart, TimeSpan endOffset)
    {
        var localMidnight = pieceStart.Date.AddDays(1);
        var offset = spanStart.Offset == endOffset || localMidnight < ClockChangeAfter(spanStart)
            ? spanStart.Offset
            : endOffset;
        var midnight = new DateTimeOffset(localMidnight, offset);

        // After a large time-zone change (travel) the end's midnight can lie before the piece's start.
        return midnight > pieceStart ? midnight : new DateTimeOffset(localMidnight, pieceStart.Offset);
    }

    /// <summary>The local start (at the span start's offset) of the first 02:00–03:00 not already over at <paramref name="spanStart"/>.</summary>
    private static DateTime ClockChangeAfter(DateTimeOffset spanStart)
    {
        var sameDay = spanStart.Date + ClockChangeFrom;
        return spanStart.TimeOfDay < ClockChangeUntil ? sameDay : sameDay.AddDays(1);
    }
}
