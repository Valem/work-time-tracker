using Microsoft.Data.Sqlite;
using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;
using Wtt.Infrastructure.Storage;

namespace Wtt.Infrastructure.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly TempDatabase file = new();
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.FromHours(2)));

    public void Dispose() => file.Dispose();

    [Fact]
    public async Task A_fact_keeps_its_instant_offset_and_detail()
    {
        var journal = new SqliteFactJournal(file.Open(clock));
        var fact = new ActivityFact(
            new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.FromHours(1)),
            FactKind.Lock,
            FactSource.Live,
            "live#1",
            new FactDetail(EndCause.IdleLock, TimeSpan.FromMinutes(10), "Reason", "1"));

        await journal.AppendAsync([fact]);
        var read = Assert.Single(await journal.ReadAsync(Interval.All));

        Assert.Equal(fact, read);
        Assert.Equal(TimeSpan.FromHours(1), read.At.Offset);
    }

    [Fact]
    public async Task AC_1_9_facts_recorded_at_utc_plus_1_keep_their_offset_next_to_facts_at_utc_minus_5()
    {
        var journal = new SqliteFactJournal(file.Open(clock));
        var early = new ActivityFact(new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.FromHours(1)), FactKind.Unlock, FactSource.WinlogonHistory, "a");
        var later = new ActivityFact(new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.FromHours(-5)), FactKind.Unlock, FactSource.WinlogonHistory, "b");

        await journal.AppendAsync([early, later]);
        var read = await journal.ReadAsync(Interval.All);

        Assert.Equal([new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4)], read.Select(fact => fact.At.LocalDate()));
        Assert.Equal([TimeSpan.FromHours(1), TimeSpan.FromHours(-5)], read.Select(fact => fact.At.Offset));
    }

    [Fact]
    public async Task Appending_a_known_source_key_again_adds_nothing()
    {
        var journal = new SqliteFactJournal(file.Open(clock));
        var fact = new ActivityFact(clock.Now, FactKind.Unlock, FactSource.WinlogonHistory, "same");

        Assert.Equal(1, await journal.AppendAsync([fact]));
        Assert.Equal(0, await journal.AppendAsync([fact with { At = clock.Now.AddMinutes(5) }]));
        Assert.Single(await journal.ReadAsync(Interval.All));
    }

    [Fact]
    public async Task Reading_a_range_returns_its_facts_in_time_order_and_the_earliest_is_found_per_source()
    {
        var journal = new SqliteFactJournal(file.Open(clock));
        var start = clock.Now;
        await journal.AppendAsync([
            new ActivityFact(start.AddHours(3), FactKind.Lock, FactSource.WinlogonHistory, "3"),
            new ActivityFact(start.AddHours(1), FactKind.Unlock, FactSource.WinlogonHistory, "1"),
            new ActivityFact(start, FactKind.SignIn, FactSource.SystemHistory, "0"),
        ]);

        var middle = await journal.ReadAsync(new Interval(start.AddMinutes(1), start.AddHours(3)));

        Assert.Equal(["1"], middle.Select(fact => fact.SourceKey));
        Assert.Equal(start, await journal.EarliestAsync());
        Assert.Equal(start.AddHours(1), await journal.EarliestAsync(FactSource.WinlogonHistory));
        Assert.Null(await journal.EarliestAsync(FactSource.Live));
    }

    [Fact]
    public async Task Coverage_heartbeats_extend_a_span_and_a_gap_starts_a_new_one()
    {
        var coverage = new SqliteCoverageLog(file.Open(clock));
        var start = clock.Now;

        await coverage.ExtendAsync(start);
        await coverage.ExtendAsync(start.AddMinutes(1));
        await coverage.ExtendAsync(start.AddMinutes(2));
        await coverage.ExtendAsync(start.AddMinutes(30));
        var spans = await coverage.ReadAsync(Interval.All);

        Assert.Equal(2, spans.Count);
        Assert.Equal((start, start.AddMinutes(2)), (spans[0].From, spans[0].To));
        Assert.Equal(start.AddMinutes(30), spans[1].From);
    }

    [Fact]
    public async Task The_history_cursor_survives_reopening_the_file()
    {
        var cursor = new HistoryCursor(new DateTimeOffset(2026, 10, 20, 9, 50, 0, TimeSpan.Zero), "1");
        await new SqliteHistoryCursorStore(file.Open(clock)).SaveAsync(cursor);

        var loaded = await new SqliteHistoryCursorStore(file.Open(clock)).LoadAsync();

        Assert.Equal(cursor, loaded);
        await new SqliteHistoryCursorStore(file.Open(clock)).SaveAsync(cursor with { OpenSession = null });
        Assert.Null((await new SqliteHistoryCursorStore(file.Open(clock)).LoadAsync()).OpenSession);
    }

    [Fact]
    public void Opening_migrates_a_new_file_once_and_keeps_its_install_date()
    {
        file.Open(clock);
        clock.Now = clock.Now.AddDays(10);
        file.Open(clock);

        Assert.Equal(1L, Scalar("PRAGMA user_version;"));
        Assert.Equal("2026-10-12", Scalar("SELECT value FROM meta WHERE key = 'installed_on';"));
        Assert.Equal("wal", Scalar("PRAGMA journal_mode;"));
    }

    [Fact]
    public void A_file_written_by_a_newer_program_is_refused()
    {
        file.Open(clock);
        Execute("PRAGMA user_version = 99;");

        var refused = Assert.Throws<DataFileException>(() => file.Open(clock));

        Assert.Contains("newer", refused.Message, StringComparison.Ordinal);
    }

    private object? Scalar(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={file.Path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={file.Path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
