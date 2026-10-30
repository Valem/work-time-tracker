using System.Globalization;
using Wtt.Application.Ports;
using Wtt.Domain.Planning;

namespace Wtt.PublicHolidays;

/// <summary>The OpenHolidays source: what is asked for, and how much of an answer is taken.</summary>
public static class OpenHolidays
{
    /// <summary>Far above the 7–20 KB a canton's year takes.</summary>
    public const int MaxAnswerBytes = 256 * 1024;

    public static readonly HolidaySource Source = new("OpenHolidays", "ODbL 1.0");

    public static readonly TimeSpan AnswerWithin = TimeSpan.FromSeconds(15);

    /// <summary>The request names only the country, the canton and the year.</summary>
    public static Uri RequestUri(Canton canton, int year)
    {
        var from = PlanSettings.StartOf(year).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = new DateOnly(year, 12, 31).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Uri($"https://openholidaysapi.org/PublicHolidays?countryIsoCode=CH&subdivisionCode=CH-{canton}&validFrom={from}&validTo={to}");
    }

    internal static PublicHolidayReadException Refused(string reason) => new(LoadFailure.Invalid, reason);
}
