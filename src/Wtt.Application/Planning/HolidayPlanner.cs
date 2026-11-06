using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;
using Wtt.Domain.Time;

namespace Wtt.Application.Planning;

/// <summary>The year planner: every request reads the plan, lets the domain decide and answers with what it derived.</summary>
public interface IHolidayPlanner
{
    Task<YearPlan> GetYearAsync(int year);

    Task<DragPreview> PreviewAsync(int year, DateOnly anchor, DateOnly to);

    Task<PlanEdit> DragAsync(int year, DateOnly anchor, DateOnly to);

    Task<PlanEdit> ClickAsync(int year, DateOnly date);

    /// <summary>Ticks or unticks the date's offered public-holiday entry of the share — «Mein Jahr»'s tick box.</summary>
    Task<PlanEdit> TickAsync(int year, DateOnly date, DayPart share, bool ticked);

    /// <summary>Writes the reversing request of the last edit exactly as it came back.</summary>
    Task<YearPlan> RestoreAsync(int year, PlanChange reverse);

    Task<SettingsView> GetSettingsAsync(int year);

    Task<SettingsView> SaveSettingsAsync(int year, SettingsInput input);

    /// <summary>One request for the public holidays of the canton in force on the year's 1 January.</summary>
    Task<LoadState> ReloadAsync(int year);

    Task<IReadOnlyList<DayStatus>> StatusesAsync(DateOnly from, DateOnly to);
}

/// <exception cref="PlanRefusedException">A request outside what the planner takes; nothing was changed.</exception>
public sealed class HolidayPlanner(
    IPlanStore plans,
    IPublicHolidayStore holidays,
    IPublicHolidayLoads loads,
    IFactJournal journal,
    IPlanRules rules,
    TimeProvider clock) : IHolidayPlanner
{
    private const int MaxRestoredDays = 366;
    private static readonly HalfDays MaxYearlyDays = new(366 * 2);

    public async Task<YearPlan> GetYearAsync(int year)
    {
        var view = await ViewOfAsync(year);
        return await YearAsync(view, await YearFactsAsync(year));
    }

    public async Task<DragPreview> PreviewAsync(int year, DateOnly anchor, DateOnly to)
    {
        var view = await ViewOfAsync(year);
        RequireInYear(year, anchor, to);
        var facts = await YearFactsAsync(year);
        var change = rules.Drag(facts, anchor, to);
        var after = facts.With(change);
        return new DragPreview(
            rules.ModeOf(facts, anchor),
            change.Bookings.Select(booking => new PreviewDay(booking.Date, booking.After)).ToList(),
            rules.CountsOf(year, view.Today, after),
            Used(year, view.Today, after) - Used(year, view.Today, facts));
    }

    public async Task<PlanEdit> DragAsync(int year, DateOnly anchor, DateOnly to)
    {
        var view = await ViewOfAsync(year);
        RequireInYear(year, anchor, to);
        var facts = await YearFactsAsync(year);
        var change = rules.Drag(facts, anchor, to);
        var kind = change.IsEmpty ? EditKind.Nothing
            : rules.ModeOf(facts, anchor) == DragMode.Erase ? EditKind.Removed
            : EditKind.Booked;
        return await EditAsync(view, facts, change, kind);
    }

    public async Task<PlanEdit> ClickAsync(int year, DateOnly date)
    {
        var view = await ViewOfAsync(year);
        RequireInYear(year, date);
        var facts = await YearFactsAsync(year);
        var change = rules.Click(facts, date);
        return await EditAsync(view, facts, change, KindOf(change));
    }

    public async Task<PlanEdit> TickAsync(int year, DateOnly date, DayPart share, bool ticked)
    {
        var view = await ViewOfAsync(year);
        RequireInYear(year, date);
        var facts = await YearFactsAsync(year);
        var tick = new HolidayTick(date, share);
        RequireOffered(facts, tick);
        var change = rules.Tick(facts, tick, ticked);
        return await EditAsync(view, facts, change, KindOf(change));
    }

    public async Task<YearPlan> RestoreAsync(int year, PlanChange reverse)
    {
        var view = await ViewOfAsync(year);
        var bookingDates = reverse.Bookings.Select(booking => booking.Date).ToList();
        var ticks = reverse.Ticks.Select(change => change.Tick).ToList();
        if (bookingDates.Count > MaxRestoredDays || ticks.Count > MaxRestoredDays
            || bookingDates.Distinct().Count() != bookingDates.Count || ticks.Distinct().Count() != ticks.Count)
        {
            throw new PlanRefusedException(PlanRefusal.ValueOutOfRange, $"A restore holds each booking and tick at most once and at most {MaxRestoredDays} days.");
        }

        RequireInYear(year, bookingDates.Concat(ticks.Select(tick => tick.Date)).ToArray());
        var facts = await YearFactsAsync(year);
        foreach (var tick in ticks)
        {
            RequireOffered(facts, tick);
        }

        await plans.ApplyAsync(reverse);
        return await YearAsync(view, facts.With(reverse));
    }

    public async Task<SettingsView> GetSettingsAsync(int year)
    {
        await ViewOfAsync(year);
        var facts = await YearFactsAsync(year);
        var settings = facts.Settings.InForceOn(PlanSettings.StartOf(year));
        var offered = DatesOf(year)
            .Select(date => rules.StatusOn(date, facts).Holiday)
            .SelectMany(holiday => holiday?.Entries ?? Enumerable.Empty<HolidayOnDay>())
            .Where(onDay => onDay.Entry.Offered)
            .Select(onDay => new OfferedHoliday(onDay.Entry, onDay.Ticked))
            .ToList();
        return new SettingsView(
            year,
            settings.Pensum,
            settings.MondayToFriday,
            settings.YearlyDays,
            settings.PrefilledYearlyDays,
            settings.Canton,
            offered,
            await LoadStateAsync(year, facts),
            loads.Source);
    }

    public async Task<SettingsView> SaveSettingsAsync(int year, SettingsInput input)
    {
        await ViewOfAsync(year);
        RequireTakeable(input);
        var saved = new PlanSettings(PlanSettings.StartOf(year), input.Pensum, input.MondayToFriday, input.YearlyDays, input.Canton);
        var inputs = await plans.ReadAsync(saved.ValidFrom, DateOnly.MaxValue);
        var facts = new PlanFacts(new SettingsBook(inputs.Settings), [], new HolidayPlan(inputs.Bookings, inputs.Ticks));
        await plans.SaveSettingsAsync(saved, rules.TicksClearedBy(facts, saved));
        return await GetSettingsAsync(year);
    }

    public async Task<LoadState> ReloadAsync(int year)
    {
        await ViewOfAsync(year);
        var start = PlanSettings.StartOf(year);
        var canton = new SettingsBook((await plans.ReadAsync(start, start)).Settings).InForceOn(start).Canton
            ?? throw new PlanRefusedException(PlanRefusal.NoCanton, "No canton is chosen: there are no public holidays to load.");
        var state = await loads.ReloadAsync(canton, year);
        return state.Status == LoadStatus.OutOfRange
            ? throw new PlanRefusedException(PlanRefusal.YearOutOfRange, $"Public holidays are loaded for last, this and next year only, not for {year}.")
            : state;
    }

    public async Task<IReadOnlyList<DayStatus>> StatusesAsync(DateOnly from, DateOnly to)
    {
        var facts = await FactsAsync(from, to);
        var statuses = new List<DayStatus>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            statuses.Add(rules.StatusOn(date, facts));
        }

        return statuses;
    }

    private static EditKind KindOf(PlanChange change) => change switch
    {
        { Ticks: [var tick] } => tick.After ? EditKind.Ticked : EditKind.Unticked,
        { Bookings: [{ After: DayPart.Whole }] } => EditKind.BookedWhole,
        { Bookings: [{ After: DayPart.Half }] } => EditKind.BookedHalf,
        { Bookings: [{ After: null }] } => EditKind.Removed,
        _ => EditKind.Nothing,
    };

    private static void RequireInYear(int year, params DateOnly[] dates)
    {
        foreach (var date in dates)
        {
            if (date.Year != year)
            {
                throw new PlanRefusedException(PlanRefusal.DateNotInYear, $"{date:yyyy-MM-dd} is not in {year}.");
            }
        }
    }

    private static void RequireTakeable(SettingsInput input)
    {
        if (input.Pensum is < PlanSettings.MinPensum or > PlanSettings.MaxPensum)
        {
            throw new PlanRefusedException(PlanRefusal.ValueOutOfRange, $"The Pensum lies between {PlanSettings.MinPensum} and {PlanSettings.MaxPensum} %.");
        }

        if (input.YearlyDays is { } days && (days < HalfDays.Zero || days > MaxYearlyDays))
        {
            throw new PlanRefusedException(PlanRefusal.ValueOutOfRange, $"Yearly holiday days lie between 0 and {MaxYearlyDays}.");
        }

        if (input.MondayToFriday.Count != 5 || input.MondayToFriday.Any(share => share is not (WorkShare.Whole or WorkShare.None)))
        {
            throw new PlanRefusedException(PlanRefusal.ValueOutOfRange, "Each weekday Monday to Friday is working or free.");
        }
    }

    private static void RequireOffered(PlanFacts facts, HolidayTick tick)
    {
        if (!facts.HolidaysOn(tick.Date).Any(entry => entry.Offered && entry.Tick == tick))
        {
            throw new PlanRefusedException(
                PlanRefusal.NotOptionalHoliday,
                $"{tick.Date:yyyy-MM-dd} has no {(tick.Share == DayPart.Half ? "half" : "whole")}-day public holiday that does not count by itself, to tick.");
        }
    }

    private static IEnumerable<DateOnly> DatesOf(int year)
    {
        var start = PlanSettings.StartOf(year);
        return Enumerable.Range(0, start.AddYears(1).DayNumber - start.DayNumber).Select(start.AddDays);
    }

    private async Task<PlanEdit> EditAsync(PlannerView view, PlanFacts before, PlanChange change, EditKind kind)
    {
        if (!change.IsEmpty)
        {
            await plans.ApplyAsync(change);
        }

        var after = before.With(change);
        var dates = change.Bookings.Select(booking => booking.Date).Concat(change.Ticks.Select(tick => tick.Date)).Order().ToList();
        return new PlanEdit(
            kind,
            dates.Count > 0 ? dates[0] : null,
            dates.Count > 0 ? dates[^1] : null,
            Used(view.Year, view.Today, after) - Used(view.Year, view.Today, before),
            change.Inverse(),
            await YearAsync(view, after));
    }

    private HalfDays Used(int year, DateOnly today, PlanFacts facts)
    {
        var counts = rules.CountsOf(year, today, facts);
        return counts.Taken + counts.Planned;
    }

    private async Task<YearPlan> YearAsync(PlannerView view, PlanFacts facts)
    {
        var months = new List<MonthPlan>();
        for (var month = 1; month <= 12; month++)
        {
            var days = new List<DayPlan>();
            for (var date = new DateOnly(view.Year, month, 1); date.Month == month; date = date.AddDays(1))
            {
                days.Add(new DayPlan(rules.StatusOn(date, facts), Past: date < view.Today, Today: date == view.Today, OnClick: KindOf(rules.Click(facts, date))));
            }

            months.Add(new MonthPlan(month, days));
        }

        return new YearPlan(
            view.Year,
            view.Switch.Before(view.Year),
            view.Switch.After(view.Year),
            view.Today,
            await plans.LayoutAsync(),
            rules.CountsOf(view.Year, view.Today, facts),
            await LoadStateAsync(view.Year, facts),
            months);
    }

    private async Task<LoadState> LoadStateAsync(int year, PlanFacts facts) =>
        facts.Settings.InForceOn(PlanSettings.StartOf(year)).Canton is { } canton
            ? await loads.StateOfAsync(canton, year)
            : LoadState.NoCanton;

    /// <summary>Today and the years the switch reaches; refuses a year outside them.</summary>
    private async Task<PlannerView> ViewOfAsync(int year)
    {
        var today = clock.GetLocalNow().LocalDate();
        var oldest = await journal.EarliestAsync();
        var range = YearSwitch.For(today, oldest?.LocalDate());
        return range.Reaches(year)
            ? new PlannerView(year, today, range)
            : throw new PlanRefusedException(PlanRefusal.YearOutOfRange, $"The planner shows {range.First} to {range.Last}, not {year}.");
    }

    private Task<PlanFacts> YearFactsAsync(int year) => FactsAsync(PlanSettings.StartOf(year), new DateOnly(year, 12, 31));

    /// <summary>The facts of the dates and of their neighbours, which bridge days look at.</summary>
    private async Task<PlanFacts> FactsAsync(DateOnly from, DateOnly to)
    {
        var first = from.AddDays(-1);
        var last = to.AddDays(1);
        var inputs = await plans.ReadAsync(first, last);
        var settings = new SettingsBook(inputs.Settings);
        var years = new List<PublicHolidayYear>();
        foreach (var (canton, year) in settings.CantonYears(first, last))
        {
            if (await holidays.FindAsync(canton, year) is { } found)
            {
                years.Add(found);
            }
        }

        return new PlanFacts(settings, years, new HolidayPlan(inputs.Bookings, inputs.Ticks));
    }

    private sealed record PlannerView(int Year, DateOnly Today, YearSwitch Switch);
}
