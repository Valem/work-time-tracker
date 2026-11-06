namespace Wtt.Domain.Planning;

/// <summary>A booking on one date before and after a change; null where there is none.</summary>
public sealed record BookingChange(DateOnly Date, DayPart? Before, DayPart? After);

/// <summary>The tick of one public-holiday entry — its date and share — before and after a change.</summary>
public sealed record TickChange(DateOnly Date, DayPart Share, bool Before, bool After)
{
    public HolidayTick Tick => new(Date, Share);
}

/// <summary>The bookings and ticks a gesture changes, each with its state before and after; its inverse undoes it.</summary>
public sealed record PlanChange(IReadOnlyList<BookingChange> Bookings, IReadOnlyList<TickChange> Ticks)
{
    public static PlanChange None { get; } = new([], []);

    public bool IsEmpty => Bookings.Count == 0 && Ticks.Count == 0;

    public static PlanChange OfBooking(DateOnly date, DayPart? before, DayPart? after) =>
        before == after ? None : new([new BookingChange(date, before, after)], []);

    public static PlanChange OfTick(HolidayTick tick, bool before, bool after) =>
        before == after ? None : new([], [new TickChange(tick.Date, tick.Share, before, after)]);

    public PlanChange Inverse() => new(
        [.. Bookings.Select(change => change with { Before = change.After, After = change.Before })],
        [.. Ticks.Select(change => change with { Before = change.After, After = change.Before })]);
}

/// <summary>The person's bookings — whole or half, one per date — and the offered public-holiday entries they ticked.</summary>
public sealed class HolidayPlan(IReadOnlyDictionary<DateOnly, DayPart> bookings, IReadOnlySet<HolidayTick> ticks)
{
    public static HolidayPlan Empty { get; } = new(new Dictionary<DateOnly, DayPart>(), new HashSet<HolidayTick>());

    public IReadOnlyDictionary<DateOnly, DayPart> Bookings => bookings;

    public IReadOnlySet<HolidayTick> Ticks => ticks;

    public DayPart? BookingOn(DateOnly date) => bookings.TryGetValue(date, out var part) ? part : null;

    public bool IsTicked(HolidayTick tick) => ticks.Contains(tick);

    /// <summary>The plan with each changed booking and tick set to its state after the change.</summary>
    public HolidayPlan With(PlanChange change)
    {
        var changedBookings = new Dictionary<DateOnly, DayPart>(bookings);
        foreach (var booking in change.Bookings)
        {
            if (booking.After is { } part)
            {
                changedBookings[booking.Date] = part;
            }
            else
            {
                changedBookings.Remove(booking.Date);
            }
        }

        var changedTicks = new HashSet<HolidayTick>(ticks);
        foreach (var tick in change.Ticks)
        {
            if (tick.After)
            {
                changedTicks.Add(tick.Tick);
            }
            else
            {
                changedTicks.Remove(tick.Tick);
            }
        }

        return new HolidayPlan(changedBookings, changedTicks);
    }
}
