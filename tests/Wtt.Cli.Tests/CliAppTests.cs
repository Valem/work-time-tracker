using System.Globalization;
using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Infrastructure.History;

namespace Wtt.Cli.Tests;

public sealed class CliAppTests : IDisposable
{
    private static readonly TimeSpan Local = TimeSpan.FromHours(2);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "wtt-cli-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 24, 15, 0, 0, Local));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AC_1_12_weeks_prints_every_day_from_the_oldest_event_to_today_and_a_second_run_adds_nothing()
    {
        var history = new ListHistorySource([
            Fact("2026-09-09 08:00", FactKind.Unlock, "1"), Fact("2026-09-09 17:00", FactKind.Lock, "2"),
            Fact("2026-10-20 07:30", FactKind.Unlock, "3"), Fact("2026-10-20 16:00", FactKind.Lock, "4"),
        ]);

        var first = await Run(history, "weeks");
        var second = await Run(history, "weeks");

        Assert.Contains("4 facts of your sessions read, 4 new.", first.Output, StringComparison.Ordinal);
        Assert.Contains(", 0 new.", second.Output, StringComparison.Ordinal);
        for (var date = new DateOnly(2026, 9, 9); date <= new DateOnly(2026, 10, 24); date = date.AddDays(1))
        {
            Assert.Contains(date.ToString("ddd yyyy-MM-dd", CultureInfo.InvariantCulture), first.Output, StringComparison.Ordinal);
        }

        Assert.Contains("Tue 2026-10-20   07:30  16:00   0:30    8:00  rebuilt", first.Output, StringComparison.Ordinal);
        Assert.Contains("Week 43  Mon 2026-10-19 to Sun 2026-10-25", first.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_import_the_cli_reads_only_the_data_file()
    {
        var history = new ListHistorySource([Fact("2026-10-20 07:30", FactKind.Unlock, "1")]);

        var result = await Run(history, "weeks", "--no-import");

        Assert.Equal(0, history.Reads);
        Assert.Contains("No events found.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_test_without_both_logs_says_there_is_nothing_to_compare()
    {
        var result = await Run(new ListHistorySource([]), "start-test");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("nothing to compare", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CR_005_a_windows_log_that_cannot_be_read_is_named_and_nothing_is_imported()
    {
        var history = new FailingHistorySource(new HistoryReadException("System", new UnauthorizedAccessException("Access is denied.")));

        var failed = await Run(history, "weeks");
        var afterwards = await Run(history, "weeks", "--no-import");

        Assert.Equal(1, failed.ExitCode);
        Assert.Contains("The Windows log 'System' could not be read: Access is denied.", failed.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was imported", failed.Output, StringComparison.Ordinal);
        Assert.Contains("No events found.", afterwards.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CR_005_a_damaged_data_file_is_named()
    {
        var path = Path.Combine(folder, "damaged.db");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(path, "not a database, but long enough to look like a file header.....");

        var result = await Run(new ListHistorySource([]), "weeks", "--db", path);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"The data file '{path}' could not be opened", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CR_005_a_data_file_locked_by_another_program_is_named()
    {
        var path = Path.Combine(folder, "wtt-cli.db");
        await Run(new ListHistorySource([]), "weeks");
        using var holder = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False");
        holder.Open();
        using var exclusive = holder.CreateCommand();
        exclusive.CommandText = "BEGIN EXCLUSIVE;";
        exclusive.ExecuteNonQuery();

        var result = await Run(new ListHistorySource([]), "weeks");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"The data file '{path}' could not be opened", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("weekz")]
    [InlineData("weeks", "--verbose")]
    [InlineData("weeks", "--db")]
    public async Task Unknown_commands_and_options_show_the_usage(params string[] args)
    {
        var result = await Run(new ListHistorySource([]), args);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: wtt-cli", result.Output, StringComparison.Ordinal);
    }

    private async Task<(int ExitCode, string Output)> Run(IHistorySource history, params string[] args)
    {
        var output = new StringWriter();
        var environment = new CliEnvironment(clock, new UserSid("S-1-5-21-1-2-3-1001"), history, Path.Combine(folder, "wtt-cli.db"), TimeSpan.FromSeconds(1));
        var exitCode = await CliApp.RunAsync(args, output, environment);
        return (exitCode, output.ToString());
    }

    private static ActivityFact Fact(string localTime, FactKind kind, string key) => new(
        new DateTimeOffset(DateTime.ParseExact(localTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Local),
        kind,
        FactSource.WinlogonHistory,
        key);

    private sealed class FailingHistorySource(Exception failure) : IHistorySource
    {
        public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user) => Task.FromException<HistoryBatch>(failure);
    }

    private sealed class ListHistorySource(IReadOnlyList<ActivityFact> facts) : IHistorySource
    {
        public int Reads { get; private set; }

        public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user)
        {
            Reads++;
            return Task.FromResult(new HistoryBatch(facts, cursor));
        }
    }
}
