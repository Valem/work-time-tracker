using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;

namespace Wtt.Application.Planning;

/// <summary>A date of the year as the planner shows it.</summary>
public sealed record DayPlan(DayStatus Status, bool Past, bool Today);

public sealed record MonthPlan(int Month, IReadOnlyList<DayPlan> Days);

/// <summary>
/// One year of the plan: the years the switch reaches before and after it (null at its ends), today, the layout, the counts,
/// whether the canton's public holidays of the year are loaded, and every date month by month.
/// </summary>
public sealed record YearPlan(
    int Year,
    int? Previous,
    int? Next,
    DateOnly Today,
    YearLayout Layout,
    YearLedger Counts,
    LoadState Load,
    IReadOnlyList<MonthPlan> Months);

/// <summary>A date a drag would change, with the booking it would hold.</summary>
public sealed record PreviewDay(DateOnly Date, DayPart? Booking);

/// <summary>What a drag would do: book or erase, the dates it changes, the counts after it and the change in counted half days.</summary>
public sealed record DragPreview(DragMode Mode, IReadOnlyList<PreviewDay> Days, YearLedger CountsAfter, HalfDays Change);

public enum EditKind
{
    Nothing,
    Booked,
    BookedWhole,
    BookedHalf,
    Removed,
    Ticked,
    Unticked,
}

/// <summary>
/// What an edit changed — its kind, the first and last date changed, the change in counted half days — with the request
/// that reverses it and the year after it.
/// </summary>
public sealed record PlanEdit(EditKind Kind, DateOnly? From, DateOnly? To, HalfDays Change, PlanChange Reverse, YearPlan Year);

/// <summary>An offered public holiday — optional, part of the canton or of a kind not known — and whether it is ticked.</summary>
public sealed record OfferedHoliday(DateOnly Date, IReadOnlyList<PublicHolidayEntry> Entries, bool Ticked);

/// <summary>
/// «Mein Jahr» of a year: the settings in force on its 1 January — yearly days null where prefilled — the offered public
/// holidays of the year with their ticks, the load state, and the source with its licence.
/// </summary>
public sealed record SettingsView(
    int Year,
    int Pensum,
    IReadOnlyList<WorkShare> MondayToFriday,
    HalfDays? YearlyDays,
    HalfDays PrefilledYearlyDays,
    Canton? Canton,
    IReadOnlyList<OfferedHoliday> Offered,
    LoadState Load,
    HolidaySource Source);

/// <summary>«Mein Jahr» as entered: yearly days null to keep them prefilled, canton null for none.</summary>
public sealed record SettingsInput(int Pensum, IReadOnlyList<WorkShare> MondayToFriday, HalfDays? YearlyDays, Canton? Canton);
