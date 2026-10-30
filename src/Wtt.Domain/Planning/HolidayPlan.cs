namespace Wtt.Domain.Planning;

/// <summary>A booking on one date before and after a change; null where there is none.</summary>
public sealed record BookingChange(DateOnly Date, DayPart? Before, DayPart? After);

/// <summary>A tick on one date before and after a change.</summary>
public sealed record TickChange(DateOnly Date, bool Before, bool After);

/// <summary>The bookings and ticks a gesture changes, each with its state before and after; its inverse undoes it.</summary>
public sealed record PlanChange(IReadOnlyList<BookingChange> Bookings, IReadOnlyList<TickChange> Ticks)
{
    public static PlanChange None { get; } = new([], []);

    public bool IsEmpty => Bookings.Count == 0 && Ticks.Count == 0;

    public static PlanChange OfBooking(DateOnly date, DayPart? before, DayPart? after) =>
        before == after ? None : new([new BookingChange(date, before, after)], []);

    public static PlanChange OfTick(DateOnly date, bool before, bool after) =>
        before == after ? None : new([], [new TickChange(date, before, after)]);

    public PlanChange Inverse() => new(
        [.. Bookings.Select(change => change with { Before = change.After, After = change.Before })],
        [.. Ticks.Select(change => change with { Before = change.After, After = change.Before })]);
}

/// <summary>The person's bookings — whole or half, one per date — and the dates whose offered public holiday they ticked.</summary>
public sealed class HolidayPlan(IReadOnlyDictionary<DateOnly, DayPart> bookings, IReadOnlySet<DateOnly> ticks)
{
    public static HolidayPlan Empty { get; } = new(new Dictionary<DateOnly, DayPart>(), new HashSet<DateOnly>());

    public IReadOnlyDictionary<DateOnly, DayPart> Bookings => bookings;

    public IReadOnlySet<DateOnly> Ticks => ticks;

    public DayPart? BookingOn(DateOnly date) => bookings.TryGetValue(date, out var part) ? part : null;

    public bool IsTicked(DateOnly date) => ticks.Contains(date);

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

        var changedTicks = new HashSet<DateOnly>(ticks);
        foreach (var tick in change.Ticks)
        {
            if (tick.After)
            {
                changedTicks.Add(tick.Date);
            }
            else
            {
                changedTicks.Remove(tick.Date);
            }
        }

        return new HolidayPlan(changedBookings, changedTicks);
    }
}
