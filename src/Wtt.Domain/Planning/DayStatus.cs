namespace Wtt.Domain.Planning;

/// <summary>
/// What a date is: a public holiday that counts — also on a weekend or a free weekday; else the weekend; else a free
/// weekday; else a workday.
/// </summary>
public enum DayKind
{
    Workday,
    Weekend,
    FreeDay,
    PublicHoliday,
}

/// <summary>A public-holiday entry on a date: whether it counts — by itself or ticked — and whether the person ticked it.</summary>
public sealed record HolidayOnDay(PublicHolidayEntry Entry, bool Counts, bool Ticked);

/// <summary>The public-holiday entries on a date, as the plan judges each.</summary>
public sealed record DayHoliday(IReadOnlyList<HolidayOnDay> Entries)
{
    public bool Counts => Entries.Any(entry => entry.Counts);

    public bool Offered => Entries.Any(entry => entry.Entry.Offered);

    public bool Ticked => Entries.Any(entry => entry.Ticked);

    /// <summary>The largest share any entry on the date names, counting or not.</summary>
    public DayPart Share => Entries.Max(entry => entry.Entry.Share);

    public IReadOnlyList<PublicHolidayEntry> Counting => Entries.Where(entry => entry.Counts).Select(entry => entry.Entry).ToList();

    /// <summary>The share the entries that count take: the largest of theirs, none when none counts.</summary>
    public WorkShare Takes => Entries.Where(entry => entry.Counts).Select(entry => entry.Entry.Share.AsShare()).DefaultIfEmpty(WorkShare.None).Max();
}

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

    /// <summary>The share of the public holidays that count on the date — a half or a whole public holiday — none when none counts.</summary>
    public WorkShare PublicHolidayShare => Holiday?.Takes ?? WorkShare.None;

    /// <summary>The public holidays that count on the date, whose names it shows.</summary>
    public IReadOnlyList<PublicHolidayEntry> CountingHolidays => Holiday?.Counting ?? [];
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
