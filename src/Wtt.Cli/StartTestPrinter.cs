using System.Globalization;
using Wtt.Application.Reports;
using static Wtt.Cli.TextFormat;

namespace Wtt.Cli;

internal static class StartTestPrinter
{
    private const string Row = "  {0,-16} {1,6}  {2,6}  {3,8}  {4}";

    public static void Print(StartTestResult result, TextWriter output)
    {
        if (result.From is { } from && result.To is { } to)
        {
            PrintComparison(result, from, to, output);
        }
        else
        {
            output.WriteLine("No days where both the Winlogon log and the System log reach: nothing to compare.");
        }

        output.WriteLine();
        PrintUnmappedWakeReasons(result.UnmappedWakeReasons, output);
    }

    private static void PrintComparison(StartTestResult result, DateOnly from, DateOnly to, TextWriter output)
    {
        var agreement = result.Agreement;
        output.WriteLine(
            $"Start test: 2-minute test against unlock, {Day(from)} to {Day(to)}, tolerance {(int)agreement.Tolerance.TotalMinutes} min");
        output.WriteLine(Format("Day", "Unlock", "2-min", "Diff", string.Empty));
        foreach (var day in agreement.Days)
        {
            output.WriteLine(Format(
                Day(day.Date),
                ClockTime(day.UnlockStart, day.Date),
                ClockTime(day.TestStart, day.Date),
                day.Difference is { } difference ? SignedMinutes(difference) : "none",
                day.Agrees ? string.Empty : "outside"));
        }

        output.WriteLine();
        var share = agreement.Share is { } value ? value.ToString("P1", CultureInfo.InvariantCulture) : "-";
        output.WriteLine($"Workdays: {agreement.Workdays}   within {(int)agreement.Tolerance.TotalMinutes} min: {agreement.Agreeing}   share: {share}   (AC-1.7 asks for at least 90 %)");
    }

    private static void PrintUnmappedWakeReasons(IReadOnlyDictionary<string, int> unmapped, TextWriter output)
    {
        if (unmapped.Count == 0)
        {
            output.WriteLine("Unmapped wake reasons: none");
            return;
        }

        output.WriteLine("Unmapped wake reasons (never taken for a person):");
        foreach (var (reason, count) in unmapped)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {count,6}  {reason}"));
        }
    }

    private static string Format(string day, string unlock, string test, string difference, string mark) =>
        string.Format(CultureInfo.InvariantCulture, Row, day, unlock, test, difference, mark).TrimEnd();
}
