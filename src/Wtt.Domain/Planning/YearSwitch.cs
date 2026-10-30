namespace Wtt.Domain.Planning;

/// <summary>The years the planner shows: from last year — or the oldest kept fact's year, if earlier — to next year.</summary>
public sealed record YearSwitch(int First, int Last)
{
    public static YearSwitch For(DateOnly today, DateOnly? oldestFact) =>
        new(Math.Min(today.Year - 1, oldestFact?.Year ?? int.MaxValue), today.Year + 1);

    public bool Reaches(int year) => year >= First && year <= Last;

    public int? Before(int year) => year > First ? year - 1 : null;

    public int? After(int year) => year < Last ? year + 1 : null;
}
