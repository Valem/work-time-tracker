namespace Wtt.Application.Planning;

public enum PlanRefusal
{
    /// <summary>The year lies outside the switch, or a reload outside last, this and next year.</summary>
    YearOutOfRange,

    /// <summary>A date the gesture names is not in the year it names.</summary>
    DateNotInYear,

    /// <summary>A tick on a date without an offered public holiday.</summary>
    NotOptionalHoliday,

    /// <summary>A reload while no canton is chosen.</summary>
    NoCanton,

    /// <summary>A Pensum, yearly days or weekday share outside what «Mein Jahr» takes.</summary>
    ValueOutOfRange,
}

/// <summary>The planner refused a request and changed nothing.</summary>
public sealed class PlanRefusedException(PlanRefusal refusal, string message) : Exception(message)
{
    public PlanRefusal Refusal { get; } = refusal;
}
