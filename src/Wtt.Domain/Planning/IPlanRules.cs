namespace Wtt.Domain.Planning;

/// <summary>The planning rules: every date's status and a year's counts, and what each gesture changes. Pure.</summary>
public interface IPlanRules
{
    DayStatus StatusOn(DateOnly date, PlanFacts facts);

    YearLedger CountsOf(int year, DateOnly today, PlanFacts facts);

    DragMode ModeOf(PlanFacts facts, DateOnly anchor);

    PlanChange Drag(PlanFacts facts, DateOnly anchor, DateOnly to);

    PlanChange Click(PlanFacts facts, DateOnly date);

    /// <summary>Ticks or unticks the offered public-holiday entry the tick names; changes nothing where none is offered.</summary>
    PlanChange Tick(PlanFacts facts, HolidayTick tick, bool ticked);

    /// <summary>The ticks that saving <paramref name="saved"/> clears.</summary>
    PlanChange TicksClearedBy(PlanFacts facts, PlanSettings saved);
}
