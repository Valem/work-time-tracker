namespace Wtt.App.Bridge;

// The messages between the UI and the host. Dates cross as yyyy-MM-dd with their ISO weekday; times as minutes from that
// day's midnight at the offset stored with each fact (1440 = 24:00); durations as whole minutes. Wtt.BridgeTypes
// writes the UI's declarations from these records, so a change here reaches the UI's type check.

/// <summary>A calendar date and its ISO weekday, 1 (Monday) to 7 (Sunday).</summary>
public sealed record CalendarDay(string Date, int Weekday);

public sealed record IsoWeek(int Year, int Number);

/// <summary>A stretch of one day, from and to minutes after its midnight, and how many minutes it lasts.</summary>
public sealed record DaySpan(int Start, int End, int Minutes);

/// <summary>Without a Monday, the current week.</summary>
public sealed record WeekRequest(string? Monday = null);

/// <summary>A week from Monday to Sunday; previous and next are null at the oldest kept fact's week and at the current week.</summary>
public sealed record WeekView(IsoWeek Week, CalendarDay? Previous, CalendarDay? Next, IReadOnlyList<DayRow> Days, int Total);

/// <summary>One day of the week; start and end are null on a day without use.</summary>
public sealed record DayRow(CalendarDay Day, int? Start, int? End, int Break, int Total, bool Rebuilt);

public sealed record DayRequest(string Date);

/// <summary>What a pause of the day is: work (inside the day, counted), the lunch, the night or the evening pause.</summary>
public enum PauseRole
{
    Work,
    Lunch,
    Night,
    Evening,
}

public sealed record PauseView(int Start, int End, int Minutes, PauseRole Role);

/// <summary>Why the break is what it is: nothing deducted, the lunch pause whole, or the tier minimum above it.</summary>
public enum DeductionBasis
{
    None,
    PauseWhole,
    TierMinimum,
}

public sealed record LunchView(DaySpan? Pause, int Deducted, DeductionBasis Basis);

/// <summary>A day; its Windows wakes are the times on this date when Windows woke alone and produced no use.</summary>
public sealed record DayView(
    CalendarDay Day,
    CalendarDay Monday,
    int? Start,
    int? End,
    IReadOnlyList<DaySpan> Blocks,
    IReadOnlyList<PauseView> Pauses,
    IReadOnlyList<int> WindowsWakes,
    DaySpan? NightPause,
    LunchView Lunch,
    DaySpan? EveningPause,
    DaySpan? EveningUse,
    int Break,
    int Total,
    bool Rebuilt);

public sealed record StatusRequest;

/// <summary>An instant as the laptop's clock reads it now: the date and minutes after its midnight at the current offset.</summary>
public sealed record ClockReading(CalendarDay Day, int Minutes);

/// <summary>Recording: this session's Recorder holds its mutex and its coverage reached within 2 minutes.</summary>
public sealed record StatusView(bool Recording, ClockReading? LastRecorded);

public sealed record LunchRuleRequest;

public sealed record LunchWindow(int Start, int End);

public sealed record LunchTier(int Over, int Minimum);

/// <summary>The rule in force today; a prefilled rule has no date it applies from.</summary>
public sealed record LunchRuleView(LunchWindow Window, IReadOnlyList<LunchTier> Tiers, CalendarDay? ValidFrom, bool Prefilled);

/// <summary>
/// A rule to save. Values arrive as JSON numbers and are checked here, so a fraction of a minute is refused with its own
/// code rather than as a malformed message.
/// </summary>
public sealed record LunchRuleInput(LunchWindowInput Window, IReadOnlyList<LunchTierInput> Tiers);

public sealed record LunchWindowInput(decimal Start, decimal End);

public sealed record LunchTierInput(decimal Over, decimal Minimum);
