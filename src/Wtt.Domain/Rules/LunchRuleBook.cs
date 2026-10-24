namespace Wtt.Domain.Rules;

/// <summary>
/// Which lunch rule a day is calculated with. Before any save every day uses the prefilled rule.
/// The rule in force at the end of the first save's day also covers the days before install, so a
/// correction made that same day reaches them; each saved rule applies from its own date until the
/// next one, so days already typed into a timesheet keep their result.
/// </summary>
public sealed class LunchRuleBook
{
    private readonly DateOnly installedOn;
    private readonly IReadOnlyList<LunchRule> savedInOrder;

    public LunchRuleBook(DateOnly installedOn, IReadOnlyList<LunchRule> savedInOrder)
    {
        this.installedOn = installedOn;
        this.savedInOrder = savedInOrder;
    }

    public LunchRule InForceOn(DateOnly date)
    {
        if (savedInOrder.Count == 0)
        {
            return LunchRule.Prefilled;
        }

        if (date < installedOn)
        {
            var firstSaveDay = savedInOrder[0].ValidFrom;
            return savedInOrder.Last(rule => rule.ValidFrom == firstSaveDay);
        }

        return savedInOrder.LastOrDefault(rule => rule.ValidFrom <= date) ?? LunchRule.Prefilled;
    }
}
