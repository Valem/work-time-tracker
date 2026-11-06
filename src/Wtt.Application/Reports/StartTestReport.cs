using Wtt.Application.Ports;
using Wtt.Application.Queries;
using Wtt.Domain.Comparison;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;

namespace Wtt.Application.Reports;

/// <param name="UnmappedWakeReasons">
/// Wake reasons kept in the journal that have no name and so never count as a person, with how often
/// each occurs — a high count means the 2-minute test may miss starts on this laptop.
/// </param>
public sealed record StartTestResult(
    DateOnly? From,
    DateOnly? To,
    StartAgreementResult Agreement,
    IReadOnlyDictionary<string, int> UnmappedWakeReasons);

/// <summary>
/// Over the days both the Winlogon log and the System log still cover, compares each workday's
/// unlock-based start with the start the 2-minute test finds when the Winlogon facts are left out —
/// which is how days older than the Winlogon log are rebuilt.
/// </summary>
public sealed class StartTestReport(
    IFactJournal journal,
    PresenceReader presence,
    ILunchRules rules,
    IWorkdayCalculator calculator,
    TimeProvider clock)
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public async Task<StartTestResult> RunAsync()
    {
        var unmapped = await UnmappedWakeReasonsAsync();
        var noOverlap = new StartTestResult(null, null, new StartAgreementResult([], Tolerance), unmapped);

        var winlogonFrom = await journal.EarliestAsync(FactSource.WinlogonHistory);
        var systemFrom = await journal.EarliestAsync(FactSource.SystemHistory);
        if (winlogonFrom is null || systemFrom is null)
        {
            return noOverlap;
        }

        // The oldest day of each log is cut off part way; the comparison starts on the first full day.
        var first = DateTimeOffsetExtensions.Max(winlogonFrom.Value, systemFrom.Value).LocalDate().AddDays(1);
        var last = clock.GetLocalNow().LocalDate();
        if (first > last)
        {
            return noOverlap;
        }

        var unlockBased = await presence.AroundAsync(first, last);
        var testBased = await presence.AroundAsync(first, last, HistoryView.AsIfBeyondWinlogonLog);

        var unlockDays = new List<Workday>();
        var testDays = new List<Workday>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var rule = await rules.InForceOnAsync(date);
            unlockDays.Add(calculator.Calculate(date, unlockBased, rule));
            testDays.Add(calculator.Calculate(date, testBased, rule));
        }

        return new StartTestResult(first, last, StartAgreement.Compare(unlockDays, testDays, Tolerance), unmapped);
    }

    private async Task<IReadOnlyDictionary<string, int>> UnmappedWakeReasonsAsync()
    {
        var facts = await journal.ReadAsync(Interval.All);
        return facts
            .Where(fact => fact.Kind == FactKind.StandbyExit && WakeReason.IsUnmapped(fact.Detail.Reason))
            .GroupBy(fact => fact.Detail.Reason!)
            .OrderByDescending(group => group.Count())
            .ToDictionary(group => group.Key, group => group.Count());
    }
}
