using System.Globalization;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;

namespace Wtt.Rules.Tests.Support;

internal static class TestRules
{
    /// <summary>The rule the concept's examples use (§6): window 12:00–13:30, tiers over 5:30 → 0:15, over 7:00 → 0:30, over 9:00 → 1:00.</summary>
    public static readonly LunchRule Example = new(
        DateOnly.MinValue,
        new TimeRange(new TimeOnly(12, 0), new TimeOnly(13, 30)),
        [
            new BreakTier(Hm("5:30"), Hm("0:15")),
            new BreakTier(Hm("7:00"), Hm("0:30")),
            new BreakTier(Hm("9:00"), Hm("1:00")),
        ]);

    public static TimeSpan Hm(string hoursAndMinutes) =>
        TimeSpan.ParseExact(hoursAndMinutes, @"h\:mm", CultureInfo.InvariantCulture);
}
