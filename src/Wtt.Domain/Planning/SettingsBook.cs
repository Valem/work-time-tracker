namespace Wtt.Domain.Planning;

/// <summary>Every «Mein Jahr» save, in the order saved, and which of them applies on a date.</summary>
public sealed class SettingsBook(IReadOnlyList<PlanSettings> savedInOrder)
{
    public static SettingsBook Empty { get; } = new([]);

    public IReadOnlyList<PlanSettings> SavedInOrder => savedInOrder;

    /// <summary>
    /// The settings with the latest start on or before the date — of several with one start, the last saved — else those
    /// before any save.
    /// </summary>
    public PlanSettings InForceOn(DateOnly date)
    {
        PlanSettings? inForce = null;
        foreach (var settings in savedInOrder)
        {
            if (settings.ValidFrom <= date && (inForce is null || settings.ValidFrom >= inForce.ValidFrom))
            {
                inForce = settings;
            }
        }

        return inForce ?? PlanSettings.BeforeAnySave;
    }

    /// <summary>The earliest start of saved settings after the date: where later settings take over.</summary>
    public DateOnly? NextStartAfter(DateOnly date)
    {
        DateOnly? next = null;
        foreach (var settings in savedInOrder)
        {
            if (settings.ValidFrom > date && (next is null || settings.ValidFrom < next))
            {
                next = settings.ValidFrom;
            }
        }

        return next;
    }

    /// <summary>
    /// Each canton in force somewhere from <paramref name="from"/> to <paramref name="to"/>, with the year of those dates: the
    /// canton changes only where saved settings start, the year only on 1 January.
    /// </summary>
    public IReadOnlyList<(Canton Canton, int Year)> CantonYears(DateOnly from, DateOnly to)
    {
        var turns = savedInOrder.Select(settings => settings.ValidFrom)
            .Concat(Enumerable.Range(from.Year + 1, Math.Max(0, to.Year - from.Year)).Select(PlanSettings.StartOf))
            .Where(date => date > from && date <= to)
            .Prepend(from);
        var cantonYears = new List<(Canton, int)>();
        foreach (var date in turns)
        {
            if (InForceOn(date).Canton is { } canton && !cantonYears.Contains((canton, date.Year)))
            {
                cantonYears.Add((canton, date.Year));
            }
        }

        return cantonYears;
    }

    public SettingsBook With(PlanSettings saved) => new([.. savedInOrder, saved]);
}
