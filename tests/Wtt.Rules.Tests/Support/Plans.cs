using System.Globalization;
using Wtt.Application.Planning;
using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;
using Wtt.Infrastructure.Storage;

namespace Wtt.Rules.Tests.Support;

/// <summary>Synthetic plans: settings, public-holiday years as the source gives them for 2027, bookings and ticks.</summary>
internal static class Plans
{
    public static readonly HolidaySource Synthetic = new("Synthetic", "none");

    private static readonly string[] Weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri"];

    /// <summary>Zurich's public holidays of 2027 as OpenHolidays lists them: kind, reach and share per day.</summary>
    public static PublicHolidayYear Zurich2027 { get; } = Year(
        Canton.ZH,
        2027,
        Holiday("2027-01-01", "Neujahrstag", reach: HolidayReach.Country),
        Holiday("2027-01-02", "Berchtoldstag", HolidayKind.Optional),
        Holiday("2027-03-26", "Karfreitag"),
        Holiday("2027-03-29", "Ostermontag"),
        Holiday("2027-04-19", "Sechseläuten", HolidayKind.Optional, HolidayReach.PartOfCanton, DayPart.Half),
        Holiday("2027-05-01", "Tag der Arbeit"),
        Holiday("2027-05-06", "Auffahrt", reach: HolidayReach.Country),
        Holiday("2027-05-17", "Pfingstmontag"),
        Holiday("2027-08-01", "Bundesfeiertag", reach: HolidayReach.Country),
        Holiday("2027-09-13", "Knabenschiessen", HolidayKind.Optional, HolidayReach.PartOfCanton, DayPart.Half),
        Holiday("2027-09-19", "Bettag"),
        Holiday("2027-12-25", "Weihnachten", reach: HolidayReach.Country),
        Holiday("2027-12-26", "Stephanstag"));

    public static DateOnly D(string date) => DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static PublicHolidayEntry Holiday(
        string date,
        string name,
        HolidayKind kind = HolidayKind.Public,
        HolidayReach reach = HolidayReach.Canton,
        DayPart share = DayPart.Whole) =>
        new(D(date), share, kind, reach, new Dictionary<string, string> { ["DE"] = name });

    public static PublicHolidayYear Year(Canton canton, int year, params PublicHolidayEntry[] entries) =>
        new(canton, year, Synthetic, DateTimeOffset.UnixEpoch, entries);

    /// <summary>Settings saved for a year; <paramref name="free"/> names free weekdays, <paramref name="half"/> half ones.</summary>
    public static PlanSettings Settings(int year, int pensum = 100, string free = "", string half = "", decimal? yearlyDays = null, Canton? canton = null) =>
        new(PlanSettings.StartOf(year), pensum, Week(free, half), yearlyDays is { } days ? HalfDays.Exactly(days) : null, canton);

    public static IReadOnlyList<WorkShare> Week(string free = "", string half = "") =>
        [.. Weekdays.Select(day => free.Contains(day, StringComparison.Ordinal) ? WorkShare.None
            : half.Contains(day, StringComparison.Ordinal) ? WorkShare.Half
            : WorkShare.Whole)];

    public static HalfDays Days(decimal days) => HalfDays.Exactly(days) ?? throw new ArgumentException($"{days} is not in half days.");

    /// <summary>The planner as the app composes it, on a data file; it never requests public holidays.</summary>
    public static HolidayPlanner Planner(SqliteDatabase database, TimeProvider clock)
    {
        var holidays = new SqlitePublicHolidayStore(database);
        var errors = new FailingErrorLog();
        var loads = new PublicHolidayLoads(new PublicHolidayLoader(new NoSource(), holidays, errors), holidays, errors, clock, CancellationToken.None);
        return new HolidayPlanner(new SqlitePlanStore(database, clock), holidays, loads, new SqliteFactJournal(database), new PlanRules(), clock);
    }

    private sealed class NoSource : IPublicHolidaySource
    {
        public HolidaySource Source => Synthetic;

        public Task<PublicHolidayYear> ReadAsync(Canton canton, int year, CancellationToken cancel) =>
            throw new InvalidOperationException("A rule test never requests public holidays.");
    }

    private sealed class FailingErrorLog : IErrorLog
    {
        public void Write(string context, Exception error) => throw new InvalidOperationException(context, error);
    }
}

/// <summary>A plan held in memory and judged by the domain's planning rules; gestures change it as the planner would.</summary>
internal sealed class PlanScenario
{
    private readonly PlanRules rules = new();
    private readonly List<PlanSettings> settings = [];
    private readonly List<PublicHolidayYear> years = [];
    private HolidayPlan plan = HolidayPlan.Empty;

    public PlanScenario(params PlanSettings[] saved) => settings.AddRange(saved);

    public PlanFacts Facts => new(new SettingsBook(settings), years, plan);

    public PlanScenario Loaded(params PublicHolidayYear[] loaded)
    {
        years.AddRange(loaded);
        return this;
    }

    public PlanScenario Saved(PlanSettings saved)
    {
        settings.Add(saved);
        return this;
    }

    public PlanScenario Booked(DayPart part, params string[] dates)
    {
        plan = plan.With(new PlanChange([.. dates.Select(date => new BookingChange(Plans.D(date), plan.BookingOn(Plans.D(date)), part))], []));
        return this;
    }

    public PlanScenario Ticked(params string[] dates)
    {
        plan = plan.With(new PlanChange([], [.. dates.Select(date => new TickChange(Plans.D(date), plan.IsTicked(Plans.D(date)), true))]));
        return this;
    }

    public DayStatus Status(string date) => rules.StatusOn(Plans.D(date), Facts);

    public YearLedger Counts(int year, string today) => rules.CountsOf(year, Plans.D(today), Facts);

    public PlanChange Drag(string anchor, string to) => Apply(rules.Drag(Facts, Plans.D(anchor), Plans.D(to)));

    public PlanChange Click(string date) => Apply(rules.Click(Facts, Plans.D(date)));

    public PlanChange Tick(string date, bool ticked = true) => Apply(rules.Tick(Facts, Plans.D(date), ticked));

    /// <summary>The bookings held from the first date to the last, as "date part" for each booked date.</summary>
    public IReadOnlyList<string> BookingsBetween(string first, string last) =>
        [.. plan.Bookings
            .Where(booking => booking.Key >= Plans.D(first) && booking.Key <= Plans.D(last))
            .OrderBy(booking => booking.Key)
            .Select(booking => $"{booking.Key:yyyy-MM-dd} {booking.Value}")];

    private PlanChange Apply(PlanChange change)
    {
        plan = plan.With(change);
        return change;
    }
}
