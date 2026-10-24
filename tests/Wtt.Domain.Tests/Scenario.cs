using System.Globalization;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;

namespace Wtt.Domain.Tests;

/// <summary>Builds a fact journal in local clock time and calculates days from it.</summary>
internal sealed class Scenario
{
    private static readonly string[] Formats = ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];

    private readonly List<ActivityFact> facts = [];
    private readonly List<CoverageSpan> coverage = [];
    private TimeSpan offset = TimeSpan.FromHours(2);
    private bool winlogonHorizonOverridden;
    private DateTimeOffset? winlogonHorizon;

    public IReadOnlyList<ActivityFact> Facts => facts;

    public static DateTimeOffset Local(string localTime, TimeSpan offset) =>
        new(DateTime.ParseExact(localTime, Formats, CultureInfo.InvariantCulture), offset);

    public DateTimeOffset At(string localTime) => Local(localTime, offset);

    public Scenario WithOffset(TimeSpan utcOffset)
    {
        offset = utcOffset;
        return this;
    }

    public Scenario Unlock(string at, FactSource source = FactSource.WinlogonHistory) =>
        Add(at, FactKind.Unlock, source);

    public Scenario Lock(string at, FactSource source = FactSource.WinlogonHistory, EndCause? cause = null, TimeSpan? timer = null) =>
        Add(at, FactKind.Lock, source, new FactDetail(cause, timer));

    /// <summary>Unlock at <paramref name="from"/> and lock at <paramref name="to"/>.</summary>
    public Scenario Use(string from, string to, FactSource source = FactSource.WinlogonHistory) =>
        Unlock(from, source).Lock(to, source);

    public Scenario SignIn(string at, FactSource source = FactSource.SystemHistory) =>
        Add(at, FactKind.SignIn, source);

    public Scenario SignOut(string at, FactSource source = FactSource.SystemHistory) =>
        Add(at, FactKind.SignOut, source);

    public Scenario Boot(string at) => Add(at, FactKind.Boot, FactSource.SystemHistory);

    public Scenario Shutdown(string at) => Add(at, FactKind.Shutdown, FactSource.SystemHistory);

    public Scenario StandbyEnter(string at, FactSource source = FactSource.SystemHistory, string reason = "IdleTimeout") =>
        Add(at, FactKind.StandbyEnter, source, new FactDetail(Reason: reason));

    public Scenario StandbyExit(string at, string reason, FactSource source = FactSource.SystemHistory) =>
        Add(at, FactKind.StandbyExit, source, new FactDetail(Reason: reason));

    /// <summary>Windows wakes itself (reporting it as keyboard input, as observed) and goes back to standby.</summary>
    public Scenario WindowsWake(string from, string to, string reason = WakeReason.InputKeyboard) =>
        StandbyExit(from, reason).StandbyEnter(to);

    public Scenario LastInput(string at) => Add(at, FactKind.LastInput, FactSource.Live);

    public Scenario IdleTimeout(string at) => Add(at, FactKind.IdleTimeout, FactSource.Live);

    public Scenario Covered(string from, string to)
    {
        coverage.Add(new CoverageSpan(At(from), At(to)));
        return this;
    }

    public Scenario WinlogonHistoryFrom(DateTimeOffset? from)
    {
        winlogonHorizonOverridden = true;
        winlogonHorizon = from;
        return this;
    }

    public PresenceTimeline Presence()
    {
        var first = facts.Min(fact => fact.At);
        var last = facts.Max(fact => fact.At);
        var range = new Interval(first.AddDays(-1), last.AddDays(1));
        return new PresenceBuilder().Build(range, facts, coverage, WinlogonHorizon());
    }

    public Workday Day(string date, LunchRule rule) =>
        new WorkdayCalculator().Calculate(DateOnly.Parse(date, CultureInfo.InvariantCulture), Presence(), rule);

    private DateTimeOffset? WinlogonHorizon()
    {
        if (winlogonHorizonOverridden)
        {
            return winlogonHorizon;
        }

        var winlogon = facts.Where(fact => fact.Source == FactSource.WinlogonHistory).ToList();
        return winlogon.Count == 0 ? null : winlogon.Min(fact => fact.At);
    }

    private Scenario Add(string at, FactKind kind, FactSource source, FactDetail? detail = null)
    {
        facts.Add(new ActivityFact(At(at), kind, source, $"{source}#{facts.Count}", detail ?? FactDetail.None));
        return this;
    }
}
