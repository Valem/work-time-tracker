using Wtt.Domain.Time;

namespace Wtt.Domain.Rules;

/// <summary>The lunch window and minimum-break tiers, applying from <see cref="ValidFrom"/>.</summary>
public sealed class LunchRule
{
    public LunchRule(DateOnly validFrom, TimeRange window, IEnumerable<BreakTier> tiers)
    {
        var ordered = tiers.OrderBy(tier => tier.Over).ToList();
        if (ordered.Any(tier => tier.Over < TimeSpan.Zero || tier.Minimum < TimeSpan.Zero))
        {
            throw new ArgumentException("Break tiers cannot be negative.", nameof(tiers));
        }

        if (ordered.Select(tier => tier.Over).Distinct().Count() != ordered.Count)
        {
            throw new ArgumentException("Two break tiers cannot share a threshold.", nameof(tiers));
        }

        ValidFrom = validFrom;
        Window = window;
        Tiers = ordered;
    }

    /// <summary>The rule a fresh install shows before the person saves one (P1).</summary>
    public static LunchRule Prefilled { get; } = new(
        DateOnly.MinValue,
        new TimeRange(new TimeOnly(11, 30), new TimeOnly(14, 0)),
        [
            new BreakTier(new TimeSpan(5, 30, 0), TimeSpan.FromMinutes(15)),
            new BreakTier(new TimeSpan(7, 0, 0), TimeSpan.FromMinutes(30)),
            new BreakTier(new TimeSpan(9, 0, 0), TimeSpan.FromHours(1)),
        ]);

    public DateOnly ValidFrom { get; }

    public TimeRange Window { get; }

    public IReadOnlyList<BreakTier> Tiers { get; }

    /// <summary>The minimum break of the highest tier whose threshold <paramref name="netWorked"/> strictly exceeds.</summary>
    public TimeSpan TierMinimumFor(TimeSpan netWorked) =>
        Tiers.LastOrDefault(tier => netWorked > tier.Over)?.Minimum ?? TimeSpan.Zero;

    /// <summary>
    /// The smallest break ≥ <paramref name="lunchPause"/> whose tier, judged on the time worked after
    /// that break, is no larger than the break itself — so one more minute never lowers the total.
    /// </summary>
    public TimeSpan BreakFor(TimeSpan attendance, TimeSpan lunchPause)
    {
        // The smallest such break is where the condition first turns true: at the lunch pause, where
        // the net time falls onto a threshold, or where the break reaches a tier's minimum.
        var candidates = Tiers
            .SelectMany(tier => new[] { attendance - tier.Over, tier.Minimum })
            .Append(lunchPause)
            .Where(candidate => candidate >= lunchPause)
            .Order();

        return candidates.First(candidate => TierMinimumFor(attendance - candidate) <= candidate);
    }
}
