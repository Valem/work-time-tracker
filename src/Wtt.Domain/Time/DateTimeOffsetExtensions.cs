namespace Wtt.Domain.Time;

public static class DateTimeOffsetExtensions
{
    /// <summary>The calendar date at the offset the instant was recorded with.</summary>
    public static DateOnly LocalDate(this DateTimeOffset instant) => DateOnly.FromDateTime(instant.DateTime);

    /// <summary>The midnight after the instant's local date, at the given offset.</summary>
    public static DateTimeOffset NextMidnight(this DateTimeOffset instant, TimeSpan offset) =>
        new(instant.Date.AddDays(1), offset);

    public static DateTimeOffset RoundToMinute(this DateTimeOffset instant)
    {
        var minutes = (instant.Ticks + (TimeSpan.TicksPerMinute / 2)) / TimeSpan.TicksPerMinute;
        return new DateTimeOffset(minutes * TimeSpan.TicksPerMinute, instant.Offset);
    }

    public static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;

    public static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
