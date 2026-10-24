using System.Globalization;
using Wtt.Domain.Facts;

namespace Wtt.Application.Tests;

internal static class Facts
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);
    private static int sequence;

    public static DateTimeOffset At(string localTime) =>
        new(DateTime.ParseExact(localTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Offset);

    public static ActivityFact Unlock(string at) => Fact(at, FactKind.Unlock, FactSource.WinlogonHistory);

    public static ActivityFact Lock(string at) => Fact(at, FactKind.Lock, FactSource.WinlogonHistory);

    public static ActivityFact SignIn(string at) => Fact(at, FactKind.SignIn, FactSource.SystemHistory);

    public static ActivityFact StandbyEnter(string at) => Fact(at, FactKind.StandbyEnter, FactSource.SystemHistory);

    public static ActivityFact StandbyExit(string at, string reason) =>
        Fact(at, FactKind.StandbyExit, FactSource.SystemHistory, new FactDetail(Reason: reason));

    public static IEnumerable<ActivityFact> Use(string from, string to) => [Unlock(from), Lock(to)];

    private static ActivityFact Fact(string at, FactKind kind, FactSource source, FactDetail? detail = null) =>
        new(At(at), kind, source, $"test#{Interlocked.Increment(ref sequence)}", detail ?? FactDetail.None);
}
