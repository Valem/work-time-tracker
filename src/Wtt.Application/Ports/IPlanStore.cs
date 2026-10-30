using Wtt.Domain.Planning;

namespace Wtt.Application.Ports;

/// <summary>Every «Mein Jahr» save in the order saved, and the bookings and ticks on the dates read.</summary>
public sealed record PlanInputs(IReadOnlyList<PlanSettings> Settings, IReadOnlyDictionary<DateOnly, DayPart> Bookings, IReadOnlySet<DateOnly> Ticks);

/// <summary>How the planner lays out the year's months.</summary>
public enum YearLayout
{
    Columns,
    Rows,
}

/// <summary>The person's plan: «Mein Jahr» settings, bookings, ticks and the chosen layout.</summary>
public interface IPlanStore
{
    /// <summary>Every saved setting, and the bookings and ticks from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    Task<PlanInputs> ReadAsync(DateOnly from, DateOnly to);

    /// <summary>Sets each changed booking and tick to its state after the change.</summary>
    Task ApplyAsync(PlanChange change);

    /// <summary>Saves the settings and, in the same write, the change the save makes to ticks.</summary>
    Task SaveSettingsAsync(PlanSettings settings, PlanChange ticksCleared);

    Task<YearLayout> LayoutAsync();

    Task SaveLayoutAsync(YearLayout layout);
}
