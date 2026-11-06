using System.Globalization;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;

namespace Wtt.Rules.Tests.Support;

/// <summary>Builds a fact journal in local clock time (UTC+2 unless changed) and calculates presence and days from it.</summary>
internal sealed class Scenario
{
    private static readonly string[] Formats = ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];

    private readonly List<ActivityFact> facts = [];
    private readonly List<CoverageSpan> coverage = [];
    private TimeSpan offset = TimeSpan.FromHours(2);
    private bool winlogonHorizonOverridden;
    private DateTimeOffset? winlogonHorizon;

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

    /// <summary>A fact of any kind that can end use, with the cause and timer it stores.</summary>
    public Scenario Ends(FactKind kind, string at, FactSource source, EndCause? cause = null, TimeSpan? timer = null) =>
        Add(at, kind, source, new FactDetail(cause, timer));

    public Scenario SignIn(string at, FactSource source = FactSource.SystemHistory) =>
        Add(at, FactKind.SignIn, source);

    public Scenario Boot(string at) => Add(at, FactKind.Boot, FactSource.SystemHistory);

    public Scenario Shutdown(string at) => Add(at, FactKind.Shutdown, FactSource.SystemHistory);

    public Scenario StandbyEnter(string at, FactSource source = FactSource.SystemHistory, string reason = "IdleTimeout") =>
        Add(at, FactKind.StandbyEnter, source, new FactDetail(Reason: reason));

    public Scenario StandbyExit(string at, string? reason = null, FactSource source = FactSource.SystemHistory) =>
        Add(at, FactKind.StandbyExit, source, new FactDetail(Reason: reason));

    /// <summary>Windows wakes itself (reporting it as keyboard input, as observed) and goes back to standby.</summary>
    public Scenario WindowsWake(string from, string to, string reason = WakeReason.InputKeyboard) =>
        StandbyExit(from, reason).StandbyEnter(to);

    public Scenario LastInput(string at) => Add(at, FactKind.LastInput, FactSource.Live);

    public Scenario IdleTimeout(string at) => Add(at, FactKind.IdleTimeout, FactSource.Live);

    /// <summary>Facts made elsewhere, such as the history import, kept as they are.</summary>
    public Scenario With(IEnumerable<ActivityFact> made)
    {
        facts.AddRange(made);
        return this;
    }

    public Scenario Covered(string from, string to)
    {
        coverage.Add(new CoverageSpan(At(from), At(to)));
        return this;
    }

    /// <summary>The oldest Winlogon fact kept; by default the oldest Winlogon fact of the scenario.</summary>
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
