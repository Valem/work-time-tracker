using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using static Wtt.App.Bridge.BridgeValues;

namespace Wtt.App.Bridge;

/// <summary>Reads a lunch rule the UI sends, refusing it with one code before anything is saved.</summary>
internal static class LunchRuleInputs
{
    /// <exception cref="BridgeRefusalException">The rule cannot be saved; the code says why.</exception>
    public static (TimeRange Window, IReadOnlyList<BreakTier> Tiers) Read(LunchRuleInput input)
    {
        if (input.Tiers.Any(tier => tier is null))
        {
            throw new BridgeRefusalException(RefusalCode.Malformed);
        }

        if (input.Tiers.Count > BridgeContract.MaxTiers)
        {
            throw new BridgeRefusalException(RefusalCode.TooManyTiers);
        }

        var start = TimeOfDay(input.Window.Start);
        var end = TimeOfDay(input.Window.End);
        var tiers = input.Tiers.Select(tier => new BreakTier(Duration(tier.Over), Duration(tier.Minimum))).ToList();
        if (end <= start)
        {
            throw new BridgeRefusalException(RefusalCode.WindowNotAfterStart);
        }

        if (tiers.Select(tier => tier.Over).Distinct().Count() != tiers.Count)
        {
            throw new BridgeRefusalException(RefusalCode.DuplicateThreshold);
        }

        try
        {
            return (new TimeRange(start, end), tiers);
        }
        catch (ArgumentException refused)
        {
            throw new BridgeRefusalException(RefusalCode.InvalidRule, refused);
        }
    }

    /// <summary>A time of day: 00:00 to 23:59.</summary>
    private static TimeOnly TimeOfDay(decimal minutes)
    {
        var whole = WholeMinutes(minutes);
        return whole >= MinutesPerDay
            ? throw new BridgeRefusalException(RefusalCode.ValueOutOfRange)
            : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(whole));
    }

    /// <summary>A duration: 0:00 to 24:00.</summary>
    private static TimeSpan Duration(decimal minutes) => TimeSpan.FromMinutes(WholeMinutes(minutes));

    private static int WholeMinutes(decimal minutes)
    {
        if (decimal.Truncate(minutes) != minutes)
        {
            throw new BridgeRefusalException(RefusalCode.NotWholeMinutes);
        }

        return minutes is < 0 or > MinutesPerDay ? throw new BridgeRefusalException(RefusalCode.ValueOutOfRange) : (int)minutes;
    }
}
