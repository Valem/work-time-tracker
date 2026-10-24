namespace Wtt.Domain.Workdays;

/// <summary>Monday to Sunday; the total is the sum of the day totals.</summary>
public sealed record WorkWeek(DateOnly Monday, IReadOnlyList<Workday> Days, TimeSpan Total)
{
    public static WorkWeek Of(DateOnly monday, IReadOnlyList<Workday> days)
    {
        if (monday.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException($"{monday:yyyy-MM-dd} is not a Monday.", nameof(monday));
        }

        if (days.Count != 7 || days.Where((day, i) => day.Date != monday.AddDays(i)).Any())
        {
            throw new ArgumentException("A week holds its seven days from Monday to Sunday, in order.", nameof(days));
        }

        var total = days.Aggregate(TimeSpan.Zero, (sum, day) => sum + day.Total);
        return new WorkWeek(monday, days, total);
    }

    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static IEnumerable<DateOnly> DaysOf(DateOnly monday) =>
        Enumerable.Range(0, 7).Select(monday.AddDays);
}
