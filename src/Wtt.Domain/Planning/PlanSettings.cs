namespace Wtt.Domain.Planning;

/// <summary>
/// «Mein Jahr»: the Pensum, the work share of each weekday Monday to Friday, the yearly holiday days (none: prefilled from
/// the Pensum) and the canton, applying from <see cref="ValidFrom"/>. Superseded by a later save, never edited.
/// </summary>
public sealed class PlanSettings
{
    public const int MinPensum = 1;
    public const int MaxPensum = 100;

    private static readonly WorkShare[] FullWeek = [WorkShare.Whole, WorkShare.Whole, WorkShare.Whole, WorkShare.Whole, WorkShare.Whole];

    public PlanSettings(DateOnly validFrom, int pensum, IReadOnlyList<WorkShare> mondayToFriday, HalfDays? yearlyDays, Canton? canton)
    {
        if (pensum is < MinPensum or > MaxPensum)
        {
            throw new ArgumentOutOfRangeException(nameof(pensum), pensum, $"The Pensum lies between {MinPensum} and {MaxPensum} %.");
        }

        if (mondayToFriday.Count != 5 || mondayToFriday.Any(share => !Enum.IsDefined(share)))
        {
            throw new ArgumentException("Monday to Friday each have a work share: none, half or whole.", nameof(mondayToFriday));
        }

        if (yearlyDays is { Value: < 0 })
        {
            throw new ArgumentOutOfRangeException(nameof(yearlyDays), yearlyDays, "Yearly holiday days cannot be negative.");
        }

        ValidFrom = validFrom;
        Pensum = pensum;
        MondayToFriday = [.. mondayToFriday];
        YearlyDays = yearlyDays;
        Canton = canton;
    }

    /// <summary>Before any save: 100 %, Monday to Friday working, yearly days prefilled, no canton.</summary>
    public static PlanSettings BeforeAnySave { get; } = new(DateOnly.MinValue, MaxPensum, FullWeek, null, null);

    public DateOnly ValidFrom { get; }

    public int Pensum { get; }

    public IReadOnlyList<WorkShare> MondayToFriday { get; }

    /// <summary>The yearly holiday days saved, or null where they are prefilled.</summary>
    public HalfDays? YearlyDays { get; }

    public Canton? Canton { get; }

    /// <summary>20 days × the Pensum, to the nearest half day.</summary>
    public HalfDays PrefilledYearlyDays => new(((4 * Pensum) + 5) / 10);

    public HalfDays YearlyDaysOrPrefilled => YearlyDays ?? PrefilledYearlyDays;

    /// <summary>Settings saved for a year apply from its 1 January.</summary>
    public static DateOnly StartOf(int year) => new(year, 1, 1);

    public WorkShare ShareOn(DayOfWeek day) =>
        day is DayOfWeek.Saturday or DayOfWeek.Sunday ? WorkShare.None : MondayToFriday[(int)day - 1];
}
