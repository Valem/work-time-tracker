namespace Wtt.Domain.Planning;

public sealed class PlanRules : IPlanRules
{
    public DayStatus StatusOn(DateOnly date, PlanFacts facts)
    {
        var day = Day(date, facts);
        var booking = facts.Plan.BookingOn(date);
        return new DayStatus(date, day.Kind, day.Share, booking, Counted(booking, day.Share), IsBridge(day, booking, facts), day.Holiday);
    }

    public YearLedger CountsOf(int year, DateOnly today, PlanFacts facts)
    {
        var yearly = facts.Settings.InForceOn(PlanSettings.StartOf(year)).YearlyDaysOrPrefilled;
        var taken = HalfDays.Zero;
        var planned = HalfDays.Zero;
        foreach (var (date, booking) in facts.Plan.Bookings.Where(booking => booking.Key.Year == year))
        {
            var counted = Counted(booking, Day(date, facts).Share);
            if (date < today)
            {
                taken += counted;
            }
            else
            {
                planned += counted;
            }
        }

        return new YearLedger(yearly, taken, planned, yearly - taken - planned);
    }

    /// <summary>A drag erases when its anchor holds a booking that counts, and books otherwise.</summary>
    public DragMode ModeOf(PlanFacts facts, DateOnly anchor) =>
        Counted(facts.Plan.BookingOn(anchor), Day(anchor, facts).Share) > HalfDays.Zero ? DragMode.Erase : DragMode.Book;

    /// <summary>
    /// Covers the dates from the anchor to the end, both included: erasing removes the bookings on the bookable ones, booking
    /// books each bookable one whole; a date that cannot be booked keeps what it holds.
    /// </summary>
    public PlanChange Drag(PlanFacts facts, DateOnly anchor, DateOnly to)
    {
        DayPart? after = ModeOf(facts, anchor) == DragMode.Erase ? null : DayPart.Whole;
        var (first, last) = anchor <= to ? (anchor, to) : (to, anchor);
        var changes = new List<BookingChange>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var before = facts.Plan.BookingOn(date);
            if (Day(date, facts).Share != WorkShare.None && before != after)
            {
                changes.Add(new BookingChange(date, before, after));
            }
        }

        return new PlanChange(changes, []);
    }

    /// <summary>
    /// Ticks or unticks an optional or part-canton public holiday on a date without a counting booking; otherwise cycles a
    /// bookable date none → whole → half → none (none → half → none where its share is a half); anywhere else nothing.
    /// </summary>
    public PlanChange Click(PlanFacts facts, DateOnly date)
    {
        var day = Day(date, facts);
        var booking = facts.Plan.BookingOn(date);
        if (day.Holiday is { } holiday
            && holiday.Entries.Any(entry => entry.IsOptionalOrPartCanton)
            && Counted(booking, day.Share) == HalfDays.Zero)
        {
            var ticked = facts.Plan.IsTicked(date);
            return PlanChange.OfTick(date, ticked, !ticked);
        }

        DayPart? next = (day.Share, booking) switch
        {
            (WorkShare.Whole, null) => DayPart.Whole,
            (WorkShare.Whole, DayPart.Whole) => DayPart.Half,
            (WorkShare.Half, null) => DayPart.Half,
            (WorkShare.None, _) => booking,
            _ => null,
        };
        return PlanChange.OfBooking(date, booking, next);
    }

    public PlanChange Tick(PlanFacts facts, DateOnly date, bool ticked) =>
        facts.HolidaysOn(date).Any(entry => !entry.CountsByItself)
            ? PlanChange.OfTick(date, facts.Plan.IsTicked(date), ticked)
            : PlanChange.None;

    /// <summary>
    /// Saving another canton than the one in force on the settings' start clears the ticks on the dates the new settings
    /// apply to: from their start until later saved settings take over.
    /// </summary>
    public PlanChange TicksClearedBy(PlanFacts facts, PlanSettings saved)
    {
        if (facts.Settings.InForceOn(saved.ValidFrom).Canton == saved.Canton)
        {
            return PlanChange.None;
        }

        var until = facts.Settings.NextStartAfter(saved.ValidFrom);
        return new PlanChange(
            [],
            [.. facts.Plan.Ticks
                .Where(date => date >= saved.ValidFrom && (until is null || date < until))
                .Order()
                .Select(date => new TickChange(date, Before: true, After: false))]);
    }

    /// <summary>A booking counts what it books, never more than the date's work share.</summary>
    private static HalfDays Counted(DayPart? booking, WorkShare share) =>
        booking is { } part ? HalfDays.Min(part.Amount(), share.Amount()) : HalfDays.Zero;

    /// <summary>
    /// A bookable, unbooked date of whole work share whose two neighbours both have no work share, at least one of them
    /// because of a public holiday that counts — a neighbour whose weekday has a share that the holiday takes.
    /// </summary>
    private static bool IsBridge(DayShare day, DayPart? booking, PlanFacts facts)
    {
        if (day.Share != WorkShare.Whole || booking is not null)
        {
            return false;
        }

        var before = Day(day.Date.AddDays(-1), facts);
        var after = Day(day.Date.AddDays(1), facts);
        return before.Share == WorkShare.None
            && after.Share == WorkShare.None
            && (before.OffForPublicHoliday || after.OffForPublicHoliday);
    }

    /// <summary>
    /// A date's work share: its weekday's share in the settings in force — none on Saturday and Sunday — less the share of a
    /// public holiday that counts on it, never below none.
    /// </summary>
    private static DayShare Day(DateOnly date, PlanFacts facts)
    {
        var weekday = facts.Settings.InForceOn(date).ShareOn(date.DayOfWeek);
        var entries = facts.HolidaysOn(date);
        if (entries.Count == 0)
        {
            return new DayShare(date, weekday, WorkShare.None, null);
        }

        var ticked = facts.Plan.IsTicked(date);
        var counting = entries.Where(entry => entry.CountsByItself || ticked).ToList();
        var offered = entries.Any(entry => !entry.CountsByItself);
        var holiday = new DayHoliday(
            entries,
            Counts: counting.Count > 0,
            Offered: offered,
            Ticked: offered && ticked,
            Share: entries.Max(entry => entry.Share));
        var taken = counting.Count == 0 ? WorkShare.None : counting.Max(entry => entry.Share).AsShare();
        return new DayShare(date, weekday, taken, holiday);
    }

    private sealed record DayShare(DateOnly Date, WorkShare Weekday, WorkShare TakenByHoliday, DayHoliday? Holiday)
    {
        public WorkShare Share => Weekday.Less(TakenByHoliday);

        public bool OffForPublicHoliday => Weekday != WorkShare.None && Share == WorkShare.None;

        public DayKind Kind =>
            Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? DayKind.Weekend
            : Weekday == WorkShare.None ? DayKind.FreeDay
            : TakenByHoliday != WorkShare.None ? DayKind.PublicHoliday
            : DayKind.Workday;
    }
}
