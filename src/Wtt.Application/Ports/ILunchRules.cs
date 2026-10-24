using Wtt.Domain.Rules;
using Wtt.Domain.Time;

namespace Wtt.Application.Ports;

public interface ILunchRules
{
    Task<LunchRule> InForceOnAsync(DateOnly date);

    /// <summary>Saves a rule that applies from today; the first one also covers the days before install.</summary>
    Task<LunchRule> SaveAsync(TimeRange window, IReadOnlyList<BreakTier> tiers);
}
