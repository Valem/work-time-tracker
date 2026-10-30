namespace Wtt.Domain.Planning;

/// <summary>What takes a date's work share: nothing, the weekend, a free weekday, or a public holiday that counts.</summary>
public enum DayKind
{
    Workday,
    Weekend,
    FreeDay,
    PublicHoliday,
}

/// <summary>
/// The public holidays on a date: whether one counts on it, whether one is offered to tick because it does not count by
/// itself, whether the person ticked it, and the largest share any of them takes.
/// </summary>
public sealed record DayHoliday(IReadOnlyList<PublicHolidayEntry> Entries, bool Counts, bool Offered, bool Ticked, DayPart Share);

/// <summary>One date as the plan makes it — derived, never stored.</summary>
public sealed record DayStatus(
    DateOnly Date,
    DayKind Kind,
    WorkShare Share,
    DayPart? Booking,
    HalfDays Counted,
    bool Bridge,
    DayHoliday? Holiday)
{
    public bool Bookable => Share != WorkShare.None;
}

/// <summary>A year's holiday days: the yearly days, those counted before today, those from today on, and what is left.</summary>
public sealed record YearLedger(HalfDays Yearly, HalfDays Taken, HalfDays Planned, HalfDays Left)
{
    public int TakenPermille => Permille(Taken);

    public int PlannedPermille => Permille(Planned);

    private int Permille(HalfDays part) =>
        Yearly.Value == 0 ? 0 : (int)Math.Round(part.Value * 1000m / Yearly.Value, MidpointRounding.AwayFromZero);
}

/// <summary>What a drag does to the bookable dates it covers.</summary>
public enum DragMode
{
    Book,
    Erase,
}
