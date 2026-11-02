using Wtt.Domain.Planning;

namespace Wtt.App.Bridge;

// The year planner's messages. Day amounts cross as whole half days (33 = 16.5 days), years as numbers, dates as in the
// week's messages; the host supplies every month, day, status, count and share, so the UI only posts gestures as dates and
// formats the answers.

/// <summary>What a date is: a public holiday that counts — also on a weekend or a free weekday — the weekend, a free weekday or a workday.</summary>
public enum DayType
{
    Workday,
    Weekend,
    FreeDay,
    PublicHoliday,
}

/// <summary>How much of a date is work, or how much of it a public holiday takes.</summary>
public enum Share
{
    None,
    Half,
    Whole,
}

/// <summary>A whole or a half day: what a booking holds and what a public-holiday entry takes.</summary>
public enum Part
{
    Whole,
    Half,
}

/// <summary>The year's months laid out as columns or as rows.</summary>
public enum MonthLayout
{
    Columns,
    Rows,
}

/// <summary>Whether the canton's public holidays of a year are there to count with.</summary>
public enum HolidayLoad
{
    NoCanton,
    Loading,
    Loaded,
    NotLoaded,
    OutOfRange,
}

/// <summary>Why a load failed in this run: the source unreachable, a proxy asking for a sign-in, or an answer refused whole.</summary>
public enum LoadReason
{
    Unreachable,
    ProxySignIn,
    Invalid,
}

/// <summary>What a drag does to the bookable dates it covers.</summary>
public enum DragKind
{
    Book,
    Erase,
}

/// <summary>What an edit changed.</summary>
public enum EditOutcome
{
    Nothing,
    Booked,
    BookedWhole,
    BookedHalf,
    Removed,
    Ticked,
    Unticked,
}

/// <summary>What a click on a day would change: its booking, or nothing — a click never ticks.</summary>
public enum ClickOutcome
{
    Nothing,
    BookedWhole,
    BookedHalf,
    Removed,
}

/// <summary>«Mein Jahr» offers each weekday Monday to Friday as working or free only.</summary>
public enum WeekdayUse
{
    Working,
    Free,
}

/// <summary>Without a year, the current one.</summary>
public sealed record YearRequest(int? Year = null);

/// <summary>A year's holiday days in half days: yearly, taken, planned and left, taken and planned also in ‰ of yearly.</summary>
public sealed record YearCounts(int Yearly, int Taken, int Planned, int Left, int TakenPermille, int PlannedPermille);

/// <summary>The load state of the canton's public holidays of a year: the date they were loaded, or why they are not.</summary>
public sealed record HolidayLoadView(HolidayLoad Status, CalendarDay? LoadedOn, LoadReason? Reason);

/// <summary>A public-holiday entry on a date: its name in the app's language, its share, whether it counts, is offered to tick, is ticked.</summary>
public sealed record PublicHolidayView(string Name, Part Share, bool Counts, bool Offered, bool Ticked);

/// <summary>
/// A date of the planner's year: its kind and work share, the booking and the half days it counts, the share of the
/// public holidays that count, every public-holiday entry on it, whether it is a bridge day, past or today, and what a
/// click on it would change.
/// </summary>
public sealed record PlanDay(
    CalendarDay Day,
    DayType Kind,
    Share Share,
    Part? Booking,
    int Counted,
    Share PublicHolidayShare,
    IReadOnlyList<PublicHolidayView> PublicHolidays,
    bool Bridge,
    bool Past,
    bool Today,
    ClickOutcome OnClick);

public sealed record MonthView(int Month, IReadOnlyList<PlanDay> Days);

/// <summary>A year of the planner; previous and next are null at the ends of the year switch.</summary>
public sealed record YearView(
    int Year,
    int? Previous,
    int? Next,
    CalendarDay Today,
    MonthLayout Layout,
    YearCounts Counts,
    HolidayLoadView Load,
    IReadOnlyList<MonthView> Months);

/// <summary>A drag from its anchor to the date the pointer reached; for a preview, the date it reaches now.</summary>
public sealed record DragRequest(int Year, string Anchor, string To);

/// <summary>A date a drag would change, with the booking it would then hold.</summary>
public sealed record PreviewDay(string Date, Part? Booking);

/// <summary>What a drag would do: book or erase, from the earlier of its dates to the later, the dates it changes, the counts after it and the change in half days.</summary>
public sealed record PreviewView(DragKind Mode, CalendarDay From, CalendarDay To, IReadOnlyList<PreviewDay> Days, YearCounts CountsAfter, int Change);

public sealed record ClickRequest(int Year, string Date);

/// <summary>«Mein Jahr»'s tick box: the date's entry of the share, ticked or not.</summary>
public sealed record TickRequest(int Year, string Date, Part Share, bool Ticked);

/// <summary>A booking a restore sets: as it is before the restore and as the restore leaves it.</summary>
public sealed record RestoreDay(string Date, Part? Before, Part? After);

/// <summary>A tick a restore sets, of the date's entry of the share.</summary>
public sealed record RestoreTick(string Date, Part Share, bool Before, bool After);

/// <summary>The request that reverses an edit, exactly the bookings and ticks it changed; the UI sends it back unchanged.</summary>
public sealed record RestoreRequest(int Year, IReadOnlyList<RestoreDay> Days, IReadOnlyList<RestoreTick> Ticks);

/// <summary>
/// What an edit changed — from the first to the last date changed, the change in counted half days and, for a tick, the
/// holiday's name — the request that reverses it, and the year after it.
/// </summary>
public sealed record EditView(EditOutcome Outcome, CalendarDay? From, CalendarDay? To, int Change, string? Holiday, RestoreRequest Reverse, YearView Year);

public sealed record PlannerSettingsRequest(int Year);

/// <summary>A public-holiday entry of the year that does not count by itself, to tick in «Mein Jahr».</summary>
public sealed record OfferedHolidayView(CalendarDay Day, Part Share, string Name, bool Ticked);

/// <summary>Where the public holidays come from, and the licence of its data.</summary>
public sealed record HolidaySourceView(string Name, string Licence);

/// <summary>
/// «Mein Jahr» of a year: the settings in force on its 1 January — yearly days null where prefilled — every canton to
/// choose from, the entries to tick, the load state, and the source with its licence.
/// </summary>
public sealed record PlannerSettingsView(
    int Year,
    int Pensum,
    IReadOnlyList<WeekdayUse> Weekdays,
    int? YearlyDays,
    int PrefilledYearlyDays,
    Canton? Canton,
    IReadOnlyList<Canton> Cantons,
    IReadOnlyList<OfferedHolidayView> Offered,
    HolidayLoadView Load,
    HolidaySourceView Source);

/// <summary>«Mein Jahr» to save for a year: Monday to Friday, yearly days in half days or null to keep them prefilled, canton or null for none.</summary>
public sealed record PlannerSettingsInput(int Year, int Pensum, IReadOnlyList<WeekdayUse> Weekdays, int? YearlyDays = null, Canton? Canton = null);

public sealed record ReloadRequest(int Year);

public sealed record LayoutInput(MonthLayout Layout);

public sealed record LayoutView(MonthLayout Layout);
