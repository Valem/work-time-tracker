using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Wtt.Application.Planning;
using Wtt.Domain.Planning;

namespace Wtt.Cli;

/// <summary>A gesture <c>plan</c> applies, in the order given on the command line.</summary>
internal abstract record PlanStep;

internal sealed record DragStep(DateOnly Anchor, DateOnly To) : PlanStep;

internal sealed record ClickStep(DateOnly Date) : PlanStep;

/// <summary>Ticks or unticks the offered entry of the share — or, without one, the entry a click would.</summary>
internal sealed record TickStep(DateOnly Date, DayPart? Share, bool Ticked) : PlanStep;

/// <summary>Sends back the reversing request of the last edit before it, as the app's «Rückgängig» does.</summary>
internal sealed record UndoStep : PlanStep;

/// <param name="Canton">holidays and import-holidays: the canton asked for.</param>
/// <param name="Year">The year asked for, or planned.</param>
/// <param name="DryRun">holidays: print the request instead of sending it.</param>
/// <param name="AnswerFile">import-holidays: the saved answer to replay.</param>
/// <param name="Today">plan: the date taken as today (default: this machine's).</param>
/// <param name="Settings">plan: «Mein Jahr» to save for the year before any gesture.</param>
/// <param name="Steps">plan: the gestures, in order.</param>
/// <param name="Days">plan: list every date from–to instead of the year's notable ones.</param>
internal sealed record PlanningOptions(
    Canton? Canton,
    int Year,
    bool DryRun,
    string? AnswerFile,
    DateOnly? Today,
    SettingsInput? Settings,
    IReadOnlyList<PlanStep> Steps,
    (DateOnly From, DateOnly To)? Days)
{
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] WeekdayNames = ["Mon", "Tue", "Wed", "Thu", "Fri"];

    public static bool TryParse(CliCommand command, string[] args, [NotNullWhen(true)] out PlanningOptions? options, out string? databasePath, out string error)
    {
        options = null;
        databasePath = null;
        var name = command switch
        {
            CliCommand.Holidays => "holidays",
            CliCommand.ImportHolidays => "import-holidays",
            _ => "plan",
        };
        var parser = new Parser(command, args);
        if (!parser.Run(out error))
        {
            error = $"{name}: {error}";
            return false;
        }

        databasePath = parser.DatabasePath;
        error = Missing(command, parser);
        if (error.Length > 0)
        {
            error = $"{name} {error}";
            return false;
        }

        SettingsInput? settings = parser.Pensum is { } pensum
            ? new SettingsInput(pensum, [.. WeekdayNames.Select(day => parser.FreeDays.Contains(day) ? WorkShare.None : WorkShare.Whole)], parser.YearlyDays, parser.Canton)
            : null;
        options = new PlanningOptions(parser.Canton, parser.Year!.Value, parser.DryRun, parser.AnswerFile, parser.Today, settings, parser.Steps, parser.Days);
        return true;
    }

    private static string Missing(CliCommand command, Parser parser) => command switch
    {
        _ when parser.Year is null => "needs --year <year>.",
        CliCommand.Holidays or CliCommand.ImportHolidays when parser.Canton is null => "needs --canton <code>, such as ZH.",
        CliCommand.ImportHolidays when parser.AnswerFile is null => "needs the file of the saved answer to read.",
        CliCommand.ImportHolidays or CliCommand.Plan when parser.DatabasePath is null => "needs --db <file>: it never falls back to a data file of real use.",
        CliCommand.Plan when parser.Pensum is null && (parser.FreeDays.Count > 0 || parser.YearlyDays is not null || parser.Canton is not null) =>
            "saves Mein Jahr only with --pensum: --free, --yearly-days and --canton go with it.",
        _ => string.Empty,
    };

    private sealed class Parser(CliCommand command, string[] args)
    {
        private int next;

        public string? DatabasePath { get; private set; }

        public Canton? Canton { get; private set; }

        public int? Year { get; private set; }

        public bool DryRun { get; private set; }

        public string? AnswerFile { get; private set; }

        public DateOnly? Today { get; private set; }

        public int? Pensum { get; private set; }

        public HashSet<string> FreeDays { get; } = new(StringComparer.Ordinal);

        public HalfDays? YearlyDays { get; private set; }

        public List<PlanStep> Steps { get; } = [];

        public (DateOnly From, DateOnly To)? Days { get; private set; }

        private bool Plans => command == CliCommand.Plan;

        public bool Run(out string error)
        {
            error = string.Empty;
            while (next < args.Length)
            {
                var option = args[next++];
                error = option switch
                {
                    "--db" => Value(option, path => DatabasePath = path),
                    "--year" => Value(option, text => Year = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1 and <= 9998 ? year : throw Invalid("a year such as 2027")),
                    "--canton" => Value(option, text => Canton = Cantons.Parse(text) ?? throw Invalid("a canton code such as ZH")),
                    "--dry-run" when command == CliCommand.Holidays => Flag(() => DryRun = true),
                    "--today" when Plans => Value(option, text => Today = Date(text)),
                    "--pensum" when Plans => Value(option, text => Pensum = int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var pensum) ? pensum : throw Invalid("a whole number of percent")),
                    "--free" when Plans => Value(option, Free),
                    "--yearly-days" when Plans => Value(option, text => YearlyDays = decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var days) && HalfDays.Exactly(days) is { } halves ? halves : throw Invalid("a number of days in halves, such as 25 or 16.5")),
                    "--drag" when Plans => Value(option, text => Steps.Add(Drag(text))),
                    "--click" when Plans => Value(option, text => Steps.Add(new ClickStep(Date(text)))),
                    "--tick" when Plans => Value(option, text => Steps.Add(Tick(text, ticked: true))),
                    "--untick" when Plans => Value(option, text => Steps.Add(Tick(text, ticked: false))),
                    "--undo" when Plans => Flag(() => Steps.Add(new UndoStep())),
                    "--days" when Plans => Value(option, text => Days = Range(text)),
                    var file when command == CliCommand.ImportHolidays && AnswerFile is null && !file.StartsWith("--", StringComparison.Ordinal) =>
                        Flag(() => AnswerFile = file),
                    _ => $"unknown option '{option}'.",
                };
                if (error.Length > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string Flag(Action set)
        {
            set();
            return string.Empty;
        }

        private static DateOnly Date(string text) =>
            DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : throw Invalid("a date such as 2027-03-22");

        private static (DateOnly From, DateOnly To) Range(string text) =>
            text.Split("..") is [var from, var to] ? (Date(from), Date(to)) : throw Invalid("dates from..to, such as 2027-03-22..2027-03-31");

        private static DragStep Drag(string text)
        {
            var (anchor, to) = Range(text);
            return new DragStep(anchor, to);
        }

        private static TickStep Tick(string text, bool ticked) => text.Split(':') switch
        {
            [var date] => new TickStep(Date(date), null, ticked),
            [var date, "half"] => new TickStep(Date(date), DayPart.Half, ticked),
            [var date, "whole"] => new TickStep(Date(date), DayPart.Whole, ticked),
            _ => throw Invalid("a date, optionally with :half or :whole, such as 2026-05-01:whole"),
        };

        private static FormatException Invalid(string expected) => new($"expects {expected}");

        private void Free(string text)
        {
            if (text == "none")
            {
                return;
            }

            foreach (var day in text.Split(','))
            {
                var known = WeekdayNames.FirstOrDefault(name => string.Equals(name, day, StringComparison.OrdinalIgnoreCase))
                    ?? throw Invalid("weekdays Mon, Tue, Wed, Thu or Fri, separated by commas, or none");
                FreeDays.Add(known);
            }
        }

        private string Value(string option, Action<string> set)
        {
            if (next >= args.Length || string.IsNullOrWhiteSpace(args[next]))
            {
                return $"{option} needs a value.";
            }

            try
            {
                set(args[next++]);
                return string.Empty;
            }
            catch (FormatException failure)
            {
                return $"{option} {failure.Message}.";
            }
        }
    }
}
