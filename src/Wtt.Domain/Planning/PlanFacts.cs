namespace Wtt.Domain.Planning;

/// <summary>What a date's status is derived from: the settings saved, the public-holiday years loaded and the person's plan.</summary>
public sealed class PlanFacts
{
    private readonly Dictionary<(Canton, int), PublicHolidayYear> byCantonYear;

    public PlanFacts(SettingsBook settings, IReadOnlyList<PublicHolidayYear> publicHolidays, HolidayPlan plan)
    {
        Settings = settings;
        PublicHolidays = publicHolidays;
        Plan = plan;
        byCantonYear = publicHolidays
            .GroupBy(year => (year.Canton, year.Year))
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public SettingsBook Settings { get; }

    public IReadOnlyList<PublicHolidayYear> PublicHolidays { get; }

    public HolidayPlan Plan { get; }

    /// <summary>The public holidays on the date in the canton in force then; none without a canton or a loaded year.</summary>
    public IReadOnlyList<PublicHolidayEntry> HolidaysOn(DateOnly date) =>
        Settings.InForceOn(date).Canton is { } canton && byCantonYear.TryGetValue((canton, date.Year), out var year)
            ? year.On(date)
            : [];

    public PlanFacts With(PlanChange change) => new(Settings, PublicHolidays, Plan.With(change));
}
