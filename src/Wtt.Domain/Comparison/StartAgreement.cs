using Wtt.Domain.Workdays;

namespace Wtt.Domain.Comparison;

public sealed record StartComparison(DateOnly Date, DateTimeOffset UnlockStart, DateTimeOffset? TestStart, bool Agrees)
{
    public TimeSpan? Difference => TestStart - UnlockStart;
}

public sealed record StartAgreementResult(IReadOnlyList<StartComparison> Days, TimeSpan Tolerance)
{
    public int Workdays => Days.Count;

    public int Agreeing => Days.Count(day => day.Agrees);

    /// <summary>The share of workdays whose 2-minute-test start agrees, 0 to 1; null without workdays.</summary>
    public double? Share => Workdays == 0 ? null : (double)Agreeing / Workdays;
}

/// <summary>
/// Checks how well the 2-minute test, which stands in for unlocks beyond the Winlogon log, finds the
/// start that unlocks give over days where both are known. A workday is a day with an unlock-based start.
/// </summary>
public static class StartAgreement
{
    public static StartAgreementResult Compare(
        IEnumerable<Workday> unlockBased,
        IEnumerable<Workday> testBased,
        TimeSpan tolerance)
    {
        var testStarts = testBased.ToDictionary(day => day.Date, day => day.Start);
        var days = unlockBased
            .Where(day => day.Start is not null)
            .Select(day =>
            {
                var testStart = testStarts.GetValueOrDefault(day.Date);
                var agrees = testStart is { } found && (found - day.Start!.Value).Duration() <= tolerance;
                return new StartComparison(day.Date, day.Start!.Value, testStart, agrees);
            })
            .ToList();
        return new StartAgreementResult(days, tolerance);
    }
}
