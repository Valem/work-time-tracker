using System.Diagnostics.Eventing.Reader;
using Microsoft.Data.Sqlite;
using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Domain.Time;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Storage;
using static Wtt.Infrastructure.Tests.SyntheticEvent;
using static Wtt.Infrastructure.Tests.Users;

namespace Wtt.Infrastructure.Tests;

/// <summary>CR-005: a failing log or data file is named, and a failed import changes nothing.</summary>
public sealed class FailureTests : IDisposable
{
    private readonly TempDatabase file = new();
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(2)));

    public void Dispose() => file.Dispose();

    public static TheoryData<string, Exception> SystemLogFailures => new()
    {
        { "access denied", new UnauthorizedAccessException("Access is denied.") },
        { "log wrapped while reading", new EventLogException("The event log was cleared while reading.") },
    };

    [Theory]
    [MemberData(nameof(SystemLogFailures))]
    public async Task A_failing_log_is_named_and_the_import_adds_nothing_and_keeps_the_cursor(string _, Exception failure)
    {
        var database = file.Open(clock);
        var reader = new ChannelReader(
            winlogon: [Winlogon811(1, "2026-10-20T06:00:30Z", Me, 5)],
            system: Failing([SignIn(1, "2026-10-20T06:00:00Z", Me, 1)], failure));
        var journal = new SqliteFactJournal(database);
        var cursors = new SqliteHistoryCursorStore(database);

        var thrown = await Assert.ThrowsAsync<HistoryReadException>(() => Import(reader, journal, cursors));

        Assert.Equal(HistoryChannels.System, thrown.Channel);
        Assert.Same(failure, thrown.InnerException);
        Assert.Empty(await journal.ReadAsync(Interval.All));
        Assert.Equal(HistoryCursor.Beginning, await cursors.LoadAsync());
    }

    [Theory]
    [InlineData("<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'/>")]
    [InlineData("<Event")]
    public async Task A_record_that_cannot_be_read_names_its_log(string xml)
    {
        var reader = new ChannelReader(winlogon: [xml], system: []);

        var thrown = await Assert.ThrowsAsync<HistoryReadException>(() =>
            new EventLogHistorySource(reader, TimeZoneInfo.Utc).ReadSinceAsync(HistoryCursor.Beginning, Me));

        Assert.Equal(HistoryChannels.WinlogonOperational, thrown.Channel);
    }

    [Fact]
    public void A_damaged_data_file_is_named()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
        File.WriteAllText(file.Path, "this is not a database, it is a note that happens to have this name");

        var thrown = Assert.Throws<DataFileException>(() => file.Open(clock));

        Assert.Equal(file.Path, thrown.Path);
    }

    [Fact]
    public void A_data_file_locked_by_another_writer_is_named_after_the_wait()
    {
        file.Open(clock);
        using var holder = new SqliteConnection($"Data Source={file.Path};Pooling=False");
        holder.Open();
        using var exclusive = holder.CreateCommand();
        exclusive.CommandText = "BEGIN EXCLUSIVE;";
        exclusive.ExecuteNonQuery();

        var thrown = Assert.Throws<DataFileException>(() => SqliteDatabase.Open(file.Path, clock, TimeSpan.FromSeconds(1)));

        Assert.Equal(file.Path, thrown.Path);
        Assert.IsType<SqliteException>(thrown.InnerException);
    }

    private static Task<HistoryImportResult> Import(IEventRecordReader reader, IFactJournal journal, IHistoryCursorStore cursors) =>
        new HistoryImport(new EventLogHistorySource(reader, TimeZoneInfo.Utc), journal, cursors).ImportAsync(Me);

    private static IEnumerable<string> Failing(IEnumerable<string> before, Exception failure)
    {
        foreach (var record in before)
        {
            yield return record;
        }

        throw failure;
    }

    private sealed class ChannelReader(IEnumerable<string> winlogon, IEnumerable<string> system) : IEventRecordReader
    {
        public IEnumerable<string> Read(string channel, IReadOnlyList<EventSelector> selectors, DateTimeOffset? since) =>
            channel == HistoryChannels.System ? system : winlogon;
    }
}
