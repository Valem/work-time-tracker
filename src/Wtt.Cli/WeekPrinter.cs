using System.Globalization;
using Wtt.Domain.Workdays;
using static Wtt.Cli.TextFormat;

namespace Wtt.Cli;

internal static class WeekPrinter
{
    private const string Row = "  {0,-16} {1,5}  {2,5}  {3,5}  {4,6}  {5}";

    public static void Print(IReadOnlyList<WorkWeek> weeks, TextWriter output)
    {
        if (weeks.Count == 0)
        {
            output.WriteLine("No events found.");
            return;
        }

        foreach (var week in weeks)
        {
            var weekNumber = ISOWeek.GetWeekOfYear(week.Monday.ToDateTime(TimeOnly.MinValue));
            output.WriteLine($"Week {weekNumber}  {Day(week.Monday)} to {Day(week.Monday.AddDays(6))}");
            output.WriteLine(Format("Day", "Start", "End", "Break", "Total", string.Empty));
            foreach (var day in week.Days)
            {
                output.WriteLine(day.HasUse
                    ? Format(Day(day.Date), ClockTime(day.Start, day.Date), ClockTime(day.End, day.Date), Duration(day.Break), Duration(day.Total), day.IsRebuilt ? "rebuilt" : string.Empty)
                    : Format(Day(day.Date), "-", "-", "-", Duration(TimeSpan.Zero), string.Empty));
            }

            output.WriteLine(Format("Week total", string.Empty, string.Empty, string.Empty, Duration(week.Total), string.Empty));
            output.WriteLine();
        }
    }

    private static string Format(string day, string start, string end, string breakTime, string total, string mark) =>
        string.Format(CultureInfo.InvariantCulture, Row, day, start, end, breakTime, total, mark).TrimEnd();
}
