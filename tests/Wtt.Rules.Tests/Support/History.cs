using System.Globalization;
using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Infrastructure.History;

namespace Wtt.Rules.Tests.Support;

internal static class Users
{
    public static readonly UserSid Me = new("S-1-5-21-1000000000-2000000000-3000000000-1001");
    public static readonly UserSid Other = new("S-1-5-21-1000000000-2000000000-3000000000-1002");
}

/// <summary>Synthetic event-log records read through the history import, as it reads the live Windows logs.</summary>
internal static class History
{
    public const int SensLock = 4;
    public const int SensUnlock = 5;

    /// <summary>The facts the import keeps for <see cref="Users.Me"/> from the records, at UTC+2.</summary>
    public static IReadOnlyList<ActivityFact> Read(params string[] records) =>
        new EventLogHistorySource(new FixtureEventRecordReader(records), FakeClock.Utc2)
            .ReadSinceAsync(HistoryCursor.Beginning, Users.Me)
            .GetAwaiter()
            .GetResult()
            .Facts;

    public static string Describe(ActivityFact fact) =>
        $"{fact.At:HH:mm:ss} {fact.Kind}{(fact.Detail.Reason is { } reason ? " " + reason : string.Empty)}";
}

/// <summary>Serves synthetic event XML the way the Windows reader serves the live logs: by channel, selector and time.</summary>
internal sealed class FixtureEventRecordReader(IEnumerable<string> records) : IEventRecordReader
{
    private readonly List<string> records = records.ToList();

    public IEnumerable<string> Read(string channel, IReadOnlyList<EventSelector> selectors, DateTimeOffset? since) =>
        records.Where(xml =>
        {
            var record = RawEvent.Parse(xml);
            return record.Channel == channel
                && selectors.Any(selector => selector.Provider == record.Provider && selector.EventIds.Contains(record.EventId))
                && (since is null || record.TimeCreated >= since);
        });
}

/// <summary>Builds single synthetic records in the XML shape Windows renders. Invented SIDs, times and record ids.</summary>
internal static class SyntheticEvent
{
    private const string Ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    public static string Winlogon811(long recordId, string utc, UserSid user, int code) =>
        Record(HistoryChannels.WinlogonOperational, HistoryChannels.Winlogon, 811, recordId, utc, user.Value, ("Event", code.ToString(CultureInfo.InvariantCulture)), ("SubscriberName", "Sens"));

    public static string SignIn(long recordId, string utc, UserSid user, int session) =>
        Record(HistoryChannels.System, HistoryChannels.Winlogon, 7001, recordId, utc, "S-1-5-18", ("TSId", $"{session}"), ("UserSid", user.Value));

    public static string SignOut(long recordId, string utc, UserSid user, int session) =>
        Record(HistoryChannels.System, HistoryChannels.Winlogon, 7002, recordId, utc, "S-1-5-18", ("TSId", $"{session}"), ("UserSid", user.Value));

    public static string StandbyEnter(long recordId, string utc, int reason) =>
        Record(HistoryChannels.System, HistoryChannels.KernelPower, 506, recordId, utc, "S-1-5-18", ("Reason", $"{reason}"));

    public static string StandbyExit(long recordId, string utc, int reason) =>
        Record(HistoryChannels.System, HistoryChannels.KernelPower, 507, recordId, utc, "S-1-5-18", ("Reason", $"{reason}"));

    public static string Boot(long recordId, string utc) =>
        Record(HistoryChannels.System, HistoryChannels.KernelGeneral, 12, recordId, utc, "S-1-5-18");

    /// <summary>Event 6008: localized text fields, then the stop time as local and UTC SYSTEMTIME in the binary data.</summary>
    public static string UnexpectedShutdown(long recordId, string utc, DateTimeOffset stoppedAt)
    {
        var binary = SystemTime(stoppedAt.ToOffset(TimeSpan.FromHours(2)).DateTime) + SystemTime(stoppedAt.UtcDateTime) + "DC0500003C000000";
        return $"<Event xmlns='{Ns}'><System><Provider Name='EventLog'/><EventID Qualifiers='32768'>6008</EventID>"
            + $"<TimeCreated SystemTime='{utc}'/><EventRecordID>{recordId}</EventRecordID><Channel>System</Channel>"
            + "<Computer>SYNTHETIC</Computer><Security/></System><EventData><Data>3:00:00 PM</Data><Data>10/20/2026</Data>"
            + $"<Data></Data><Data></Data><Data>1234</Data><Data></Data><Data></Data><Binary>{binary}</Binary></EventData></Event>";
    }

    private static string SystemTime(DateTime time)
    {
        ushort[] fields = [(ushort)time.Year, (ushort)time.Month, (ushort)time.DayOfWeek, (ushort)time.Day, (ushort)time.Hour, (ushort)time.Minute, (ushort)time.Second, (ushort)time.Millisecond];
        return string.Concat(fields.Select(field => $"{field & 0xFF:X2}{field >> 8:X2}"));
    }

    private static string Record(string channel, string provider, int eventId, long recordId, string utc, string? userId, params (string Name, string Value)[] data)
    {
        var security = userId is null ? "<Security/>" : $"<Security UserID='{userId}'/>";
        var eventData = string.Concat(data.Select(item => $"<Data Name='{item.Name}'>{item.Value}</Data>"));
        return $"<Event xmlns='{Ns}'><System><Provider Name='{provider}'/><EventID>{eventId}</EventID>"
            + $"<TimeCreated SystemTime='{utc}'/><EventRecordID>{recordId}</EventRecordID><Channel>{channel}</Channel>"
            + $"<Computer>SYNTHETIC</Computer>{security}</System><EventData>{eventData}</EventData></Event>";
    }
}
