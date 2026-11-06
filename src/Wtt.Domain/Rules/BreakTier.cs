namespace Wtt.Domain.Rules;

/// <summary>When the net time worked is strictly more than <see cref="Over"/>, the break is at least <see cref="Minimum"/>.</summary>
public sealed record BreakTier(TimeSpan Over, TimeSpan Minimum);
