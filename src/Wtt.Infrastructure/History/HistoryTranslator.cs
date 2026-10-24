using System.Globalization;
using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;
using static Wtt.Infrastructure.History.HistoryChannels;

namespace Wtt.Infrastructure.History;

/// <summary>
/// Turns event-log records into facts of this user's own sessions. Lock and unlock carry the
/// user's SID; the System log is machine-wide, so its events count only while this user's sign-in
/// session is open (from their sign-in to their sign-out or the next boot or shutdown).
/// The facts are stored without judging them: whether a wake was the person is the domain's call.
/// </summary>
internal sealed class HistoryTranslator(TimeZoneInfo zone)
{
    /// <summary>How far back each read starts before the newest record of the previous one.</summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromMinutes(10);

    private const int SensLock = 4;
    private const int SensUnlock = 5;

    public HistoryBatch Translate(IEnumerable<RawEvent> events, HistoryCursor cursor, UserSid user)
    {
        var ordered = events
            .OrderBy(record => record.TimeCreated)
            .ThenBy(record => record.Channel, StringComparer.Ordinal)
            .ThenBy(record => record.RecordId)
            .ToList();
        if (ordered.Count == 0)
        {
            return new HistoryBatch([], cursor);
        }

        var nextReadFrom = (ordered[^1].TimeCreated - Overlap).ToUniversalTime();
        if (cursor.ReadFrom is { } readFrom && readFrom > nextReadFrom)
        {
            nextReadFrom = readFrom;
        }

        var session = new SessionState(cursor.OpenSession);
        string? sessionAtNextRead = null;
        var snapshotTaken = false;
        var facts = new List<ActivityFact>();
        foreach (var record in ordered)
        {
            if (!snapshotTaken && record.TimeCreated >= nextReadFrom)
            {
                sessionAtNextRead = session.Open;
                snapshotTaken = true;
            }

            if (Translate(record, user, session) is { } fact)
            {
                facts.Add(fact);
            }
        }

        return new HistoryBatch(facts, new HistoryCursor(nextReadFrom, snapshotTaken ? sessionAtNextRead : session.Open));
    }

    private ActivityFact? Translate(RawEvent record, UserSid user, SessionState session) =>
        (record.Provider, record.EventId) switch
        {
            (Winlogon, 811) => SensLockOrUnlock(record, user),
            (Winlogon, 7001) => SignIn(record, user, session),
            (Winlogon, 7002) => SignOut(record, user, session),
            (KernelGeneral, 12) => Boot(record, session),
            (EventLog, 6008) => UnexpectedShutdown(record, session),
            (KernelGeneral, 13) => EndOfSession(record, session, FactKind.Shutdown, "OsShutdown"),
            (User32, 1074) => InSession(record, session, FactKind.Shutdown, "RestartRequested"),
            (KernelPower, 506) => InSession(record, session, FactKind.StandbyEnter, StandbyReasons.KernelPower(record["Reason"]), record["Reason"]),
            (KernelPower, 507) => InSession(record, session, FactKind.StandbyExit, StandbyReasons.KernelPower(record["Reason"]), record["Reason"]),
            (KernelPower, 42) => InSession(record, session, FactKind.StandbyEnter, "Sleep"),
            (KernelPower, 107) => InSession(record, session, FactKind.StandbyExit, "ResumeFromSleep"),
            (PowerTroubleshooter, 1) => WakeFromSleep(record, session),
            _ => null,
        };

    /// <summary>The S3/hibernate resume, at the wake time it reports, with the wake source it names.</summary>
    private ActivityFact? WakeFromSleep(RawEvent record, SessionState session)
    {
        var type = record["WakeSourceType"];
        var text = record["WakeSourceText"];
        var wokeAt = DateTimeOffset.TryParse(record["WakeTime"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var wakeTime)
            ? wakeTime
            : record.TimeCreated;
        return InSession(record, session, FactKind.StandbyExit, StandbyReasons.WakeSource(type, text), $"{type}|{text}", wokeAt);
    }

    private ActivityFact? SensLockOrUnlock(RawEvent record, UserSid user)
    {
        if (record.Channel != WinlogonOperational || record["SubscriberName"] != "Sens" || !IsUser(record.UserId, user))
        {
            return null;
        }

        return int.TryParse(record["Event"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) switch
        {
            true when code == SensLock => Fact(record, FactKind.Lock, FactSource.WinlogonHistory, FactDetail.None),
            true when code == SensUnlock => Fact(record, FactKind.Unlock, FactSource.WinlogonHistory, FactDetail.None),
            _ => null,
        };
    }

    private ActivityFact? SignIn(RawEvent record, UserSid user, SessionState session)
    {
        if (!IsUser(record["UserSid"], user))
        {
            return null;
        }

        session.Open = record["TSId"];
        return Fact(record, FactKind.SignIn, FactSource.SystemHistory, new FactDetail(Session: session.Open));
    }

    private ActivityFact? SignOut(RawEvent record, UserSid user, SessionState session)
    {
        if (!IsUser(record["UserSid"], user))
        {
            return null;
        }

        var signedOut = record["TSId"];
        if (session.Open == signedOut)
        {
            session.Open = null;
        }

        return Fact(record, FactKind.SignOut, FactSource.SystemHistory, new FactDetail(Session: signedOut));
    }

    /// <summary>A boot finding this user's session still open means Windows stopped without shutting down.</summary>
    private ActivityFact? Boot(RawEvent record, SessionState session)
    {
        session.InterruptedByBoot = session.Open;
        return EndOfSession(record, session, FactKind.Boot, reason: null);
    }

    /// <summary>
    /// Event 6008, written while Windows starts, names when the previous run stopped unexpectedly;
    /// it counts when the boot it follows found this user's session open.
    /// </summary>
    private ActivityFact? UnexpectedShutdown(RawEvent record, SessionState session)
    {
        var interrupted = session.InterruptedByBoot;
        session.InterruptedByBoot = null;
        if (interrupted is null || UnexpectedShutdownTime.FromBinary(record.Binary) is not { } stoppedAt)
        {
            return null;
        }

        return Fact(
            record,
            FactKind.Shutdown,
            FactSource.SystemHistory,
            new FactDetail(Reason: "UnexpectedShutdown", Session: interrupted),
            stoppedAt);
    }

    /// <summary>No session outlives a boot or a shutdown.</summary>
    private ActivityFact? EndOfSession(RawEvent record, SessionState session, FactKind kind, string? reason)
    {
        var fact = InSession(record, session, kind, reason);
        session.Open = null;
        return fact;
    }

    private ActivityFact? InSession(
        RawEvent record,
        SessionState session,
        FactKind kind,
        string? reason,
        string? rawReason = null,
        DateTimeOffset? at = null) =>
        session.Open is null
            ? null
            : Fact(record, kind, FactSource.SystemHistory, new FactDetail(Reason: reason, Session: session.Open, RawReason: rawReason), at);

    private ActivityFact Fact(RawEvent record, FactKind kind, FactSource source, FactDetail detail, DateTimeOffset? at = null)
    {
        var utc = (at ?? record.TimeCreated).ToUniversalTime();
        var local = utc.ToOffset(zone.GetUtcOffset(utc.UtcDateTime));
        return new ActivityFact(local, kind, source, SourceKey(record), detail);
    }

    /// <summary>Channel, record id and time: record ids restart when a log is cleared.</summary>
    private static string SourceKey(RawEvent record) =>
        string.Create(CultureInfo.InvariantCulture, $"{record.Channel}#{record.RecordId}@{record.TimeCreated.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}");

    private static bool IsUser(string? sid, UserSid user) =>
        string.Equals(sid, user.Value, StringComparison.OrdinalIgnoreCase);

    private sealed class SessionState(string? open)
    {
        public string? Open { get; set; } = open;

        public string? InterruptedByBoot { get; set; }
    }
}
