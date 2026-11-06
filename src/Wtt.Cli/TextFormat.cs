using System.Globalization;
using Wtt.Domain.Time;

namespace Wtt.Cli;

internal static class TextFormat
{
    public static string Day(DateOnly date) => date.ToString("ddd yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A time on <paramref name="day"/>; the following midnight reads 24:00.</summary>
    public static string ClockTime(DateTimeOffset? time, DateOnly day) => time switch
    {
        null => "-",
        { } at when at.LocalDate() > day => "24:00",
        { } at => at.ToString("HH:mm", CultureInfo.InvariantCulture),
    };

    public static string Duration(TimeSpan duration) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours}:{duration.Minutes:00}");

    public static string SignedMinutes(TimeSpan difference) =>
        string.Create(CultureInfo.InvariantCulture, $"{(difference < TimeSpan.Zero ? "-" : "+")}{(int)Math.Abs(difference.TotalMinutes)} min");
}
