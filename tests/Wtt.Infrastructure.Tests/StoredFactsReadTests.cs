using System.Globalization;
using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Application.Queries;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Storage;
using static Wtt.Infrastructure.Tests.SyntheticEvent;
using static Wtt.Infrastructure.Tests.Users;

namespace Wtt.Infrastructure.Tests;

/// <summary>Stored facts read with this version's names, whatever names the version that stored them wrote.</summary>
public sealed class StoredFactsReadTests : IDisposable
{
    private static readonly TimeZoneInfo CentralEurope = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeSpan Summer = TimeSpan.FromHours(2);

    private readonly TempDatabase file = new();
    private readonly TempDatabase earlierVersionFile = new();
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(2)));

    public void Dispose()
    {
        file.Dispose();
        earlierVersionFile.Dispose();
    }

    [Fact]
    public async Task AC_2_12_kernel_power_reason_44_is_imported_as_a_fingerprint_wake()
    {
        var batch = await ReadHistory(FingerprintEvening("2026-08-04T20:43:00Z"));

        var exit = Assert.Single(batch.Facts, fact => fact.Detail.RawReason == "44");

        Assert.Equal(WakeReason.Fingerprint, exit.Detail.Reason);
        Assert.True(WakeReason.IsPerson(exit.Detail.Reason));
    }

    [Fact]
    public async Task AC_2_12_a_reason_44_stored_as_unmapped_by_an_earlier_version_reads_as_fingerprint_and_its_row_is_unchanged()
    {
        var database = earlierVersionFile.Open(clock);
        var key = await StoreAsEarlierVersion(database, FingerprintEvening("2026-08-04T20:43:00Z"));
        var storedBefore = StoredDetail(database, key);

        var read = (await new SqliteFactJournal(database).ReadAsync(Interval.All)).Single(fact => fact.SourceKey == key);

        Assert.Equal(WakeReason.Fingerprint, read.Detail.Reason);
        Assert.Equal("44", read.Detail.RawReason);
        Assert.Contains("\"reason\":\"Unmapped:KernelPower 44\"", storedBefore, StringComparison.Ordinal);
        Assert.Equal(storedBefore, StoredDetail(database, key));
    }

    [Theory]
    [InlineData("2026-08-04T20:43:00Z", "8:33")]
    [InlineData("2026-08-04T20:42:00Z", "8:30")]
    public async Task AC_2_12_a_data_file_from_an_earlier_version_gives_the_same_days(string shutdownUtc, string expectedTotal)
    {
        var events = FingerprintEvening(shutdownUtc);
        var current = file.Open(clock);
        await Import(current, events);
        var earlier = earlierVersionFile.Open(clock);
        await StoreAsEarlierVersion(earlier, events);

        var today = await DayAsync(current, new DateOnly(2026, 8, 4));
        var fromEarlierFile = await DayAsync(earlier, new DateOnly(2026, 8, 4));

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 8, 0, 0, Summer), today.Start);
        Assert.Equal(new DateTimeOffset(2026, 8, 4, 17, 30, 0, Summer), today.End);
        Assert.Equal(TimeSpan.ParseExact(expectedTotal, @"h\:mm", CultureInfo.InvariantCulture), today.Total);
        Assert.True(today.IsRebuilt);
        Assert.Equal(
            (today.Start, today.End, today.Break, today.Total, today.IsRebuilt),
            (fromEarlierFile.Start, fromEarlierFile.End, fromEarlierFile.Break, fromEarlierFile.Total, fromEarlierFile.IsRebuilt));
    }

    [Fact]
    public async Task Every_reason_the_import_names_reads_back_with_the_same_name()
    {
        var database = file.Open(clock);
        var batch = await ReadHistory([
            SignIn(1, "2026-10-20T05:00:00Z", Me, 1),
            StandbyExit(2, "2026-10-20T06:00:00Z", 1),
            StandbyExit(3, "2026-10-20T06:01:00Z", 44),
            StandbyExit(4, "2026-10-20T06:02:00Z", 16777220),
            StandbyExit(5, "2026-10-20T06:03:00Z", 53),
            StandbyEnter(6, "2026-10-20T06:04:00Z", 12),
            WakeFromSleep(7, "2026-10-20T06:05:10Z", "2026-10-20T06:05:00Z", 1, "Power Button"),
            WakeFromSleep(8, "2026-10-20T06:06:10Z", "2026-10-20T06:06:00Z", 3, "Timer - Windows Update"),
            WakeFromSleep(9, "2026-10-20T06:07:10Z", "2026-10-20T06:07:00Z", 2, "Device -HID | Keyboard"),
        ]);
        await new SqliteFactJournal(database).AppendAsync(batch.Facts);

        var read = await new SqliteFactJournal(database).ReadAsync(Interval.All);

        Assert.Equal(
            batch.Facts.Select(fact => (fact.SourceKey, fact.Detail.Reason)),
            read.Select(fact => (fact.SourceKey, fact.Detail.Reason)));
    }

    [Fact]
    public async Task A_live_facts_reason_is_read_as_stored()
    {
        var journal = new SqliteFactJournal(file.Open(clock));
        var live = new ActivityFact(clock.Now, FactKind.StandbyExit, FactSource.Live, "live#1", new FactDetail(Reason: "LidOpened", RawReason: "44"));

        await journal.AppendAsync([live]);

        Assert.Equal(live, Assert.Single(await journal.ReadAsync(Interval.All)));
    }

    [Fact]
    public async Task AC_2_13_an_end_cause_written_by_a_newer_recorder_reads_as_unknown_and_ends_use_at_its_own_time()
    {
        var database = file.Open(clock);
        var day = new DateOnly(2026, 10, 20);
        InsertRaw(database, At(day, 8, 0), "Unlock", "live#1", "{}");
        InsertRaw(database, At(day, 11, 0), "LastInput", "live#2", "{}");
        InsertRaw(database, At(day, 12, 8), "Lock", "live#3", "{\"cause\":\"PresenceSensorLock\",\"timerSeconds\":600}");
        InsertRaw(database, At(day, 13, 0), "Unlock", "live#4", "{}");
        InsertRaw(database, At(day, 17, 0), "Lock", "live#5", "{\"cause\":\"UserLock\"}");

        var facts = await new SqliteFactJournal(database).ReadAsync(Interval.All);
        var workday = await DayAsync(database, day);
        var weeks = await new WeekListing(new SqliteFactJournal(database), Queries(database), clock).AllWeeksAsync();

        var newerEnd = Assert.Single(facts, fact => fact.SourceKey == "live#3");
        Assert.Equal(EndCause.Unknown, newerEnd.Detail.Cause);
        Assert.Equal(At(day, 12, 8), workday.Pauses[0].Start);
        Assert.Equal(new TimeSpan(8, 8, 0), workday.Total);
        var week = Assert.Single(weeks);
        Assert.Equal(new DateOnly(2026, 10, 19), week.Monday);
        Assert.Equal(workday.Total, week.Total);
    }

    /// <summary>
    /// A history day beyond the Winlogon log: use 08:00–12:00 and 13:00–17:30 from person-reason
    /// standby exits, then a reason-44 exit at 22:40 and a shutdown (local time UTC+2).
    /// </summary>
    private static string[] FingerprintEvening(string shutdownUtc) =>
    [
        SignIn(1, "2026-08-03T16:00:00Z", Me, 1),
        StandbyEnter(2, "2026-08-03T16:30:00Z", 12),
        StandbyExit(3, "2026-08-04T06:00:00Z", 31),
        StandbyEnter(4, "2026-08-04T10:00:00Z", 12),
        StandbyExit(5, "2026-08-04T11:00:00Z", 32),
        StandbyEnter(6, "2026-08-04T15:30:00Z", 12),
        StandbyExit(7, "2026-08-04T20:40:00Z", 44),
        Record(HistoryChannels.System, HistoryChannels.KernelGeneral, 13, 8, shutdownUtc, "S-1-5-18"),
    ];

    private static Task<HistoryBatch> ReadHistory(IEnumerable<string> events) =>
        new EventLogHistorySource(new FixtureEventRecordReader(events), CentralEurope).ReadSinceAsync(HistoryCursor.Beginning, Me);

    private static Task<HistoryImportResult> Import(SqliteDatabase database, IEnumerable<string> events) =>
        new HistoryImport(
            new EventLogHistorySource(new FixtureEventRecordReader(events), CentralEurope),
            new SqliteFactJournal(database),
            new SqliteHistoryCursorStore(database))
        .ImportAsync(Me);

    /// <summary>Stores the facts as the version before the fingerprint was named wrote them; returns the reason-44 fact's key.</summary>
    private static async Task<string> StoreAsEarlierVersion(SqliteDatabase database, IEnumerable<string> events)
    {
        var batch = await ReadHistory(events);
        var asWritten = batch.Facts
            .Select(fact => fact.Detail.RawReason == "44"
                ? fact with { Detail = fact.Detail with { Reason = WakeReason.Unmapped("KernelPower 44") } }
                : fact)
            .ToList();
        await new SqliteFactJournal(database).AppendAsync(asWritten);
        return asWritten.Single(fact => fact.Detail.RawReason == "44").SourceKey;
    }

    private static string StoredDetail(SqliteDatabase database, string sourceKey)
    {
        using var connection = database.Connect();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT detail FROM facts WHERE source_key = $key;";
        query.Parameters.AddWithValue("$key", sourceKey);
        return (string)query.ExecuteScalar()!;
    }

    private static void InsertRaw(SqliteDatabase database, DateTimeOffset at, string kind, string sourceKey, string detail)
    {
        using var connection = database.Connect();
        using var insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO facts (at_utc_ticks, utc_offset_minutes, kind, source, source_key, detail) VALUES ($at, $offset, $kind, 'Live', $key, $detail);";
        insert.Parameters.AddWithValue("$at", at.UtcTicks);
        insert.Parameters.AddWithValue("$offset", (int)at.Offset.TotalMinutes);
        insert.Parameters.AddWithValue("$kind", kind);
        insert.Parameters.AddWithValue("$key", sourceKey);
        insert.Parameters.AddWithValue("$detail", detail);
        insert.ExecuteNonQuery();
    }

    private static DateTimeOffset At(DateOnly date, int hour, int minute) =>
        new(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(2));

    private WorkTimeQueries Queries(SqliteDatabase database)
    {
        var journal = new SqliteFactJournal(database);
        var coverage = new SqliteCoverageLog(database);
        return new WorkTimeQueries(
            new PresenceReader(journal, coverage, new PresenceBuilder(), clock),
            coverage,
            new SqliteLunchRules(database, clock),
            new WorkdayCalculator(),
            clock);
    }

    private Task<Workday> DayAsync(SqliteDatabase database, DateOnly date) => Queries(database).GetDayAsync(date);
}
