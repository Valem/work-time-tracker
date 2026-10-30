using System.Globalization;
using Wtt.Application.Planning;
using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;
using static Wtt.Cli.TextFormat;

namespace Wtt.Cli;

internal static class PlanPrinter
{
    private const string HolidayRow = "  {0,-16} {1,-5}  {2,-7}  {3,-8}  {4,-18}  {5}";
    private const string DayRow = "  {0,-16} {1,-14}  {2,-5}  {3,-7}  {4,7}  {5}";
    private const string Language = PublicHolidayEntry.FallbackLanguage;

    private static readonly string[] Weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri"];

    /// <summary>Every public holiday of the year, with its share and whether it counts by itself or is offered to tick.</summary>
    public static void PrintPublicHolidays(PublicHolidayYear year, TextWriter output)
    {
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Public holidays of CH-{year.Canton} {year.Year} from {year.Source.Name} ({year.Source.Licence}), loaded {year.LoadedAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC: {year.Entries.Count} days"));
        output.WriteLine(Row(HolidayRow, "Day", "Share", "Counts", "Kind", "Reach", "Name"));
        foreach (var entry in year.Entries)
        {
            output.WriteLine(Row(HolidayRow, Day(entry.Date), Share(entry.Share), entry.CountsByItself ? "counts" : "offered", Kind(entry.Kind), Reach(entry.Reach), entry.NameIn(Language)));
        }
    }

    /// <summary>«Mein Jahr», the load state, the counts and the dates listed — by default each one booked, with a public holiday or a bridge day.</summary>
    public static void PrintPlan(YearPlan plan, SettingsView settings, IReadOnlyList<DayStatus>? days, TextWriter output)
    {
        output.WriteLine($"Plan {plan.Year}, today {Day(plan.Today)}");
        output.WriteLine(Settings(settings));
        output.WriteLine($"Public holidays: {Load(settings, plan.Load)}");
        foreach (var offered in settings.Offered)
        {
            output.WriteLine($"  offered {Day(offered.Date)}: {string.Join("; ", offered.Entries.Select(entry => entry.NameIn(Language)))} — {(offered.Ticked ? "ticked" : "not ticked")}");
        }

        var counts = plan.Counts;
        output.WriteLine($"Yearly {counts.Yearly}  taken {counts.Taken}  planned {counts.Planned}  left {counts.Left}");
        output.WriteLine();

        var listed = days ?? [.. plan.Months.SelectMany(month => month.Days).Select(day => day.Status)
            .Where(status => status.Booking is not null || status.Holiday is not null || status.Bridge)];
        output.WriteLine(Row(DayRow, "Day", "Kind", "Share", "Booking", "Counted", "Marks"));
        foreach (var status in listed)
        {
            output.WriteLine(Row(
                DayRow,
                Day(status.Date),
                DayKindText(status.Kind),
                WorkShareText(status.Share),
                status.Booking is { } booking ? Share(booking) : "-",
                status.Counted.ToString(),
                Marks(status)));
        }
    }

    public static string Describe(PlanEdit edit)
    {
        var what = edit.Kind switch
        {
            EditKind.Booked => "booked",
            EditKind.BookedWhole => "booked whole",
            EditKind.BookedHalf => "booked half",
            EditKind.Removed => "removed",
            EditKind.Ticked => "ticked",
            EditKind.Unticked => "unticked",
            _ => "nothing changed",
        };
        var dates = edit.From is { } from && edit.To is { } to && from != to ? $" {from:yyyy-MM-dd} to {to:yyyy-MM-dd}" : string.Empty;
        var change = edit.Change.Value > 0 ? $"+{edit.Change}" : edit.Change.ToString();
        return $"{what}{dates}, {change} → yearly {edit.Year.Counts.Yearly}  taken {edit.Year.Counts.Taken}  planned {edit.Year.Counts.Planned}  left {edit.Year.Counts.Left}";
    }

    /// <summary>The camel-case code the bridge names a refusal or a load failure by.</summary>
    public static string Code(Enum value)
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Settings(SettingsView settings)
    {
        var shares = settings.MondayToFriday
            .Select((share, day) => (share, day: Weekdays[day]))
            .GroupBy(entry => entry.share)
            .OrderByDescending(group => group.Key)
            .Select(group => $"{string.Join(" ", group.Select(entry => entry.day))} {(group.Key switch { WorkShare.Whole => "working", WorkShare.Half => "half", _ => "free" })}");
        var yearly = settings.YearlyDays is { } days ? $"{days}" : $"{settings.PrefilledYearlyDays} (prefilled)";
        var canton = settings.Canton is { } code ? code.ToString() : "none";
        return $"Mein Jahr {settings.Year}: Pensum {settings.Pensum} %, {string.Join(", ", shares)}, yearly days {yearly}, canton {canton}";
    }

    private static string Load(SettingsView settings, LoadState load)
    {
        var what = settings.Canton is { } canton ? $"CH-{canton} {settings.Year} " : string.Empty;
        return load.Status switch
        {
            LoadStatus.NoCanton => "no canton chosen",
            LoadStatus.Loading => $"{what}loading",
            LoadStatus.Loaded => string.Create(CultureInfo.InvariantCulture, $"{what}loaded {load.LoadedAt!.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC from {settings.Source.Name} ({settings.Source.Licence})"),
            LoadStatus.OutOfRange => $"{what}not loaded, outside last, this and next year; counts may change",
            _ => $"{what}not loaded{(load.Reason is { } reason ? $" ({Code(reason)})" : string.Empty)}; counts may change",
        };
    }

    private static string Marks(DayStatus status)
    {
        var marks = new List<string>();
        if (status.Bridge)
        {
            marks.Add("bridge day");
        }

        if (status.Holiday is { } holiday)
        {
            marks.AddRange(holiday.Entries.Select(entry =>
                $"{entry.NameIn(Language)} ({Kind(entry.Kind)}, {Reach(entry.Reach)}, {Share(entry.Share)}, {(entry.CountsByItself ? "counts" : holiday.Ticked ? "offered, ticked" : "offered")})"));
        }

        return string.Join("; ", marks);
    }

    private static string Share(DayPart part) => part == DayPart.Whole ? "whole" : "half";

    private static string WorkShareText(WorkShare share) => share switch
    {
        WorkShare.Whole => "whole",
        WorkShare.Half => "half",
        _ => "none",
    };

    private static string DayKindText(DayKind kind) => kind switch
    {
        DayKind.Weekend => "weekend",
        DayKind.FreeDay => "free day",
        DayKind.PublicHoliday => "public holiday",
        _ => "workday",
    };

    private static string Kind(HolidayKind kind) => kind switch
    {
        HolidayKind.Public => "public",
        HolidayKind.Optional => "optional",
        _ => "other kind",
    };

    private static string Reach(HolidayReach reach) => reach switch
    {
        HolidayReach.Country => "country",
        HolidayReach.Canton => "canton",
        _ => "part of the canton",
    };

    private static string Row(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values).TrimEnd();
}
