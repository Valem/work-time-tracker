using System.Globalization;
using Wtt.Domain.Time;

namespace Wtt.App.Bridge;

/// <summary>How dates, times and durations cross the bridge, and how a date the UI sends is read.</summary>
internal static class BridgeValues
{
    public const int MinutesPerDay = 24 * 60;

    private const string DateFormat = "yyyy-MM-dd";
    private const int FirstYear = 2000;
    private const int LastYear = 2099;

    public static CalendarDay Day(DateOnly date) =>
        new(date.ToString(DateFormat, CultureInfo.InvariantCulture), IsoWeekday(date));

    public static IsoWeek WeekOf(DateOnly date) => new(ISOWeek.GetYear(Midnight(date)), ISOWeek.GetWeekOfYear(Midnight(date)));

    /// <summary>
    /// An instant on <paramref name="day"/> as minutes after that day's midnight, read at the offset the instant carries;
    /// one on a later date (the next midnight, when work ran past it) is 1440, as the CLI prints 24:00.
    /// </summary>
    public static int MinutesOn(DateTimeOffset at, DateOnly day) =>
        at.LocalDate() > day ? MinutesPerDay : (at.Hour * 60) + at.Minute;

    public static int Minutes(TimeSpan duration) => (int)duration.TotalMinutes;

    public static DaySpan Span(DateTimeOffset start, DateTimeOffset end, DateOnly day) =>
        new(MinutesOn(start, day), MinutesOn(end, day), Minutes(end - start));

    /// <summary>The instant as the laptop's clock reads it at its current offset.</summary>
    public static ClockReading Reading(DateTimeOffset atCurrentOffset) =>
        new(Day(atCurrentOffset.LocalDate()), (atCurrentOffset.Hour * 60) + atCurrentOffset.Minute);

    /// <exception cref="BridgeRefusalException">Not a yyyy-MM-dd date, or one outside 2000–2099.</exception>
    public static DateOnly Date(string text)
    {
        if (!DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new BridgeRefusalException(RefusalCode.Malformed);
        }

        return date.Year is < FirstYear or > LastYear ? throw new BridgeRefusalException(RefusalCode.DateOutOfRange) : date;
    }

    /// <exception cref="BridgeRefusalException">Not a date in 2000–2099, or not a Monday.</exception>
    public static DateOnly Monday(string text)
    {
        var date = Date(text);
        return date.DayOfWeek == DayOfWeek.Monday ? date : throw new BridgeRefusalException(RefusalCode.NotMonday);
    }

    private static int IsoWeekday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    private static DateTime Midnight(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);
}
