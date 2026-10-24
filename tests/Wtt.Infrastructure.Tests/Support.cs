using System.Xml.Linq;
using Wtt.Application.Ports;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Storage;

namespace Wtt.Infrastructure.Tests;

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public static readonly TimeZoneInfo Utc2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");

    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone => Utc2;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>A data file in its own temporary folder, deleted afterwards.</summary>
internal sealed class TempDatabase : IDisposable
{
    private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wtt-tests", Guid.NewGuid().ToString("N"));

    public string Path => System.IO.Path.Combine(folder, "wtt.db");

    public SqliteDatabase Open(TimeProvider clock) => SqliteDatabase.Open(Path, clock);

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

/// <summary>Serves synthetic event XML the way the Windows reader serves the live logs: by channel, selector and time.</summary>
internal sealed class FixtureEventRecordReader(IEnumerable<string> records) : IEventRecordReader
{
    private readonly List<string> records = records.ToList();

    public static FixtureEventRecordReader FromFile(string fileName)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return new FixtureEventRecordReader(XElement.Load(path).Elements().Select(element => element.ToString()));
    }

    public IEnumerable<string> Read(string channel, IReadOnlyList<EventSelector> selectors, DateTimeOffset? since) =>
        records.Where(xml =>
        {
            var record = RawEvent.Parse(xml);
            return record.Channel == channel
                && selectors.Any(selector => selector.Provider == record.Provider && selector.EventIds.Contains(record.EventId))
                && (since is null || record.TimeCreated >= since);
        });
}

internal static class Users
{
    public static readonly UserSid Me = new("S-1-5-21-1000000000-2000000000-3000000000-1001");
    public static readonly UserSid Other = new("S-1-5-21-1000000000-2000000000-3000000000-1002");
}

/// <summary>Builds single synthetic records in the XML shape Windows renders.</summary>
internal static class SyntheticEvent
{
    private const string Ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    public static string Winlogon811(long recordId, string utc, UserSid user, int code, string subscriber = "Sens") =>
        Record(HistoryChannels.WinlogonOperational, HistoryChannels.Winlogon, 811, recordId, utc, user.Value, ("Event", code.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("SubscriberName", subscriber));

    public static string SignIn(long recordId, string utc, UserSid user, int session) =>
        Record(HistoryChannels.System, HistoryChannels.Winlogon, 7001, recordId, utc, "S-1-5-18", ("TSId", $"{session}"), ("UserSid", user.Value));

    public static string SignOut(long recordId, string utc, UserSid user, int session) =>
        Record(HistoryChannels.System, HistoryChannels.Winlogon, 7002, recordId, utc, "S-1-5-18", ("TSId", $"{session}"), ("UserSid", user.Value));

    public static string StandbyEnter(long recordId, string utc, int reason) =>
        Record(HistoryChannels.System, HistoryChannels.KernelPower, 506, recordId, utc, "S-1-5-18", ("Reason", $"{reason}"));

    public static string StandbyExit(long recordId, string utc, int reason) =>
        Record(HistoryChannels.System, HistoryChannels.KernelPower, 507, recordId, utc, "S-1-5-18", ("Reason", $"{reason}"));

    public static string Sleep(long recordId, string utc) =>
        Record(HistoryChannels.System, HistoryChannels.KernelPower, 42, recordId, utc, null, ("TargetState", "5"), ("Reason", "2"));

    public static string ResumeFromSleep(long recordId, string utc) =>
        Record(HistoryChannels.System, HistoryChannels.KernelPower, 107, recordId, utc, null, ("TargetState", "5"));

    public static string WakeFromSleep(long recordId, string utc, string wakeTimeUtc, int sourceType, string sourceText) =>
        Record(HistoryChannels.System, HistoryChannels.PowerTroubleshooter, 1, recordId, utc, "S-1-5-19",
            ("WakeTime", wakeTimeUtc), ("WakeSourceType", $"{sourceType}"), ("WakeSourceText", sourceText));

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

    public static string Boot(long recordId, string utc) =>
        Record(HistoryChannels.System, HistoryChannels.KernelGeneral, 12, recordId, utc, "S-1-5-18");

    public static string Record(string channel, string provider, int eventId, long recordId, string utc, string? userId, params (string Name, string Value)[] data)
    {
        var security = userId is null ? "<Security/>" : $"<Security UserID='{userId}'/>";
        var eventData = string.Concat(data.Select(item => $"<Data Name='{item.Name}'>{item.Value}</Data>"));
        return $"<Event xmlns='{Ns}'><System><Provider Name='{provider}'/><EventID>{eventId}</EventID>"
            + $"<TimeCreated SystemTime='{utc}'/><EventRecordID>{recordId}</EventRecordID><Channel>{channel}</Channel>"
            + $"<Computer>SYNTHETIC</Computer>{security}</System><EventData>{eventData}</EventData></Event>";
    }
}
