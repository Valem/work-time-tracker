using Wtt.Application.Planning;
using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;
using Wtt.Domain.Time;
using static Wtt.App.Bridge.BridgeValues;

namespace Wtt.App.Bridge;

/// <summary>
/// What the planner derived, as the bridge carries it: every status, count and share is the domain's, renamed into the
/// bridge's terms with day amounts in half days and public-holiday names in the app's language; and a restore the UI sent
/// back, read into the change it reverses.
/// </summary>
internal static class PlanViews
{
    public static YearView Year(YearPlan plan, TimeZoneInfo zone) => new(
        plan.Year,
        plan.Previous,
        plan.Next,
        Day(plan.Today),
        LayoutOf(plan.Layout),
        Counts(plan.Counts),
        Load(plan.Load, zone),
        [.. plan.Months.Select(month => new MonthView(month.Month, [.. month.Days.Select(PlanDayOf)]))]);

    /// <param name="anchor">Where the drag began.</param>
    /// <param name="to">Where the pointer is now.</param>
    public static PreviewView Preview(DragPreview preview, DateOnly anchor, DateOnly to) => new(
        preview.Mode == DragMode.Erase ? DragKind.Erase : DragKind.Book,
        Day(anchor <= to ? anchor : to),
        Day(anchor <= to ? to : anchor),
        [.. preview.Days.Select(day => new PreviewDay(Text(day.Date), PartOf(day.Booking)))],
        Counts(preview.CountsAfter),
        preview.Change.Value);

    public static EditView Edit(PlanEdit edit, TimeZoneInfo zone) => new(
        OutcomeOf(edit.Kind),
        edit.From is { } from ? Day(from) : null,
        edit.To is { } to ? Day(to) : null,
        edit.Change.Value,
        TickedName(edit),
        Restore(edit.Year.Year, edit.Reverse),
        Year(edit.Year, zone));

    public static HolidayLoadView Load(LoadState state, TimeZoneInfo zone) => new(
        state.Status switch
        {
            LoadStatus.NoCanton => HolidayLoad.NoCanton,
            LoadStatus.Loading => HolidayLoad.Loading,
            LoadStatus.Loaded => HolidayLoad.Loaded,
            LoadStatus.NotLoaded => HolidayLoad.NotLoaded,
            LoadStatus.OutOfRange => HolidayLoad.OutOfRange,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Status, "Unknown load status."),
        },
        state.LoadedAt is { } at ? Day(TimeZoneInfo.ConvertTime(at, zone).LocalDate()) : null,
        state.Reason switch
        {
            null => null,
            LoadFailure.Unreachable => LoadReason.Unreachable,
            LoadFailure.ProxySignIn => LoadReason.ProxySignIn,
            LoadFailure.Invalid => LoadReason.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Reason, "Unknown load failure."),
        });

    public static PlannerSettingsView Settings(SettingsView view, TimeZoneInfo zone) => new(
        view.Year,
        view.Pensum,
        [.. view.MondayToFriday.Select(share => share == WorkShare.None ? WeekdayUse.Free : WeekdayUse.Working)],
        view.YearlyDays?.Value,
        view.PrefilledYearlyDays.Value,
        view.Canton,
        Cantons.All,
        [.. view.Offered.Select(offered => new OfferedHolidayView(Day(offered.Entry.Date), PartOf(offered.Entry.Share), NameOf(offered.Entry), offered.Ticked))],
        Load(view.Load, zone),
        new HolidaySourceView(view.Source.Name, view.Source.Licence));

    public static SettingsInput Input(PlannerSettingsInput input) => new(
        input.Pensum,
        [.. input.Weekdays.Select(use => use == WeekdayUse.Free ? WorkShare.None : WorkShare.Whole)],
        input.YearlyDays is { } halfDays ? new HalfDays(halfDays) : null,
        input.Canton);

    /// <summary>The marks of what counts on a date: a public holiday or a free day, and a booking as it counts — none where it counts 0.</summary>
    public static IReadOnlyList<DayMark> Marks(DayStatus status)
    {
        var marks = new List<DayMark>();
        if (status.Kind == DayKind.PublicHoliday)
        {
            var kind = status.PublicHolidayShare == WorkShare.Whole ? MarkKind.PublicHoliday : MarkKind.HalfPublicHoliday;
            marks.Add(new DayMark(kind, [.. status.CountingHolidays.Select(NameOf).Distinct(StringComparer.Ordinal)]));
        }
        else if (status.Kind == DayKind.FreeDay)
        {
            marks.Add(new DayMark(MarkKind.FreeDay, []));
        }

        if (status.Counted > HalfDays.Zero)
        {
            marks.Add(new DayMark(status.Counted >= DayPart.Whole.Amount() ? MarkKind.Holiday : MarkKind.HalfHoliday, []));
        }

        return marks;
    }

    /// <exception cref="BridgeRefusalException">A date that does not read, or a day or tick missing.</exception>
    public static PlanChange Change(RestoreRequest request)
    {
        if (request.Days.Any(day => day is null) || request.Ticks.Any(tick => tick is null))
        {
            throw new BridgeRefusalException(RefusalCode.Malformed);
        }

        return new PlanChange(
            [.. request.Days.Select(day => new BookingChange(Date(day.Date), DayPartOf(day.Before), DayPartOf(day.After)))],
            [.. request.Ticks.Select(tick => new TickChange(Date(tick.Date), DayPartOf(tick.Share), tick.Before, tick.After))]);
    }

    public static MonthLayout LayoutOf(YearLayout layout) => layout == YearLayout.Rows ? MonthLayout.Rows : MonthLayout.Columns;

    public static YearLayout LayoutOf(MonthLayout layout) => layout == MonthLayout.Rows ? YearLayout.Rows : YearLayout.Columns;

    private static PlanDay PlanDayOf(DayPlan day)
    {
        var status = day.Status;
        return new PlanDay(
            Day(status.Date),
            status.Kind switch
            {
                DayKind.Workday => DayType.Workday,
                DayKind.Weekend => DayType.Weekend,
                DayKind.FreeDay => DayType.FreeDay,
                DayKind.PublicHoliday => DayType.PublicHoliday,
                _ => throw new ArgumentOutOfRangeException(nameof(day), status.Kind, "Unknown day kind."),
            },
            ShareOf(status.Share),
            PartOf(status.Booking),
            status.Counted.Value,
            ShareOf(status.PublicHolidayShare),
            [.. (status.Holiday?.Entries ?? []).Select(onDay => new PublicHolidayView(
                NameOf(onDay.Entry), PartOf(onDay.Entry.Share), onDay.Counts, onDay.Entry.Offered, onDay.Ticked))],
            status.Bridge,
            day.Past,
            day.Today,
            OutcomeOf(day.OnClick));
    }

    private static YearCounts Counts(YearLedger counts) => new(
        counts.Yearly.Value, counts.Taken.Value, counts.Planned.Value, counts.Left.Value, counts.TakenPermille, counts.PlannedPermille);

    private static RestoreRequest Restore(int year, PlanChange reverse) => new(
        year,
        [.. reverse.Bookings.Select(booking => new RestoreDay(Text(booking.Date), PartOf(booking.Before), PartOf(booking.After)))],
        [.. reverse.Ticks.Select(tick => new RestoreTick(Text(tick.Date), PartOf(tick.Share), tick.Before, tick.After))]);

    /// <summary>The name of the entry a tick edit ticked or unticked, as the year after the edit holds it.</summary>
    private static string? TickedName(PlanEdit edit)
    {
        if (edit.Kind is not (EditKind.Ticked or EditKind.Unticked) || edit.Reverse.Ticks is not [var tick])
        {
            return null;
        }

        var entry = edit.Year.Months
            .SelectMany(month => month.Days)
            .Where(day => day.Status.Date == tick.Date)
            .SelectMany(day => day.Status.Holiday?.Entries ?? [])
            .FirstOrDefault(onDay => onDay.Entry.Share == tick.Share);
        return entry is null ? null : NameOf(entry.Entry);
    }

    private static string NameOf(PublicHolidayEntry entry) => entry.NameIn(AppLocale.HolidayLanguage);

    private static Share ShareOf(WorkShare share) => share switch
    {
        WorkShare.None => Share.None,
        WorkShare.Half => Share.Half,
        WorkShare.Whole => Share.Whole,
        _ => throw new ArgumentOutOfRangeException(nameof(share), share, "Unknown work share."),
    };

    private static Part PartOf(DayPart part) => part == DayPart.Half ? Part.Half : Part.Whole;

    private static Part? PartOf(DayPart? part) => part is { } some ? PartOf(some) : null;

    private static DayPart DayPartOf(Part part) => part == Part.Half ? DayPart.Half : DayPart.Whole;

    private static DayPart? DayPartOf(Part? part) => part is { } some ? DayPartOf(some) : null;

    private static EditOutcome OutcomeOf(EditKind kind) => kind switch
    {
        EditKind.Nothing => EditOutcome.Nothing,
        EditKind.Booked => EditOutcome.Booked,
        EditKind.BookedWhole => EditOutcome.BookedWhole,
        EditKind.BookedHalf => EditOutcome.BookedHalf,
        EditKind.Removed => EditOutcome.Removed,
        EditKind.Ticked => EditOutcome.Ticked,
        EditKind.Unticked => EditOutcome.Unticked,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown edit kind."),
    };
}
