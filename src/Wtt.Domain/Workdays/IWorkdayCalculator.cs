using Wtt.Domain.Presence;
using Wtt.Domain.Rules;

namespace Wtt.Domain.Workdays;

public interface IWorkdayCalculator
{
    Workday Calculate(DateOnly date, PresenceTimeline presence, LunchRule rule);
}
