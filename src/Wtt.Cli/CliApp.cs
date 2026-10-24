using Microsoft.Data.Sqlite;
using Wtt.Application.History;
using Wtt.Application.Queries;
using Wtt.Application.Reports;
using Wtt.Domain.Presence;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Storage;

namespace Wtt.Cli;

internal static class CliApp
{
    public const string Usage = """
        Usage: wtt-cli <command> [--db <file>] [--no-import]

        Commands:
          weeks        Every week from the oldest event Windows keeps to today:
                       each day's start, end, break and total, rebuilt days marked, and the week total.
          start-test   Where both Windows logs reach back: the start the 2-minute test finds
                       against the unlock-based start, per workday, and the share within 5 minutes.

        Options:
          --db <file>  The data file to use (default: %LocalAppData%\WorkTimeTracker\wtt-cli.db).
          --no-import  Do not read Windows history first; use what the data file holds.
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, CliEnvironment environment)
    {
        if (!CliOptions.TryParse(args, out var options, out var error))
        {
            await output.WriteLineAsync(error);
            await output.WriteLineAsync();
            await output.WriteLineAsync(Usage);
            return 2;
        }

        if (options.Command == CliCommand.Help)
        {
            await output.WriteLineAsync(Usage);
            return 0;
        }

        var databasePath = Path.GetFullPath(options.DatabasePath ?? environment.DefaultDatabasePath);
        try
        {
            await RunCommandAsync(options, databasePath, output, environment);
            return 0;
        }
        catch (HistoryReadException failure)
        {
            await output.WriteLineAsync($"{failure.Message} Nothing was imported; the data file is unchanged.");
        }
        catch (DataFileException failure)
        {
            await output.WriteLineAsync(failure.Message);
        }
        catch (SqliteException failure)
        {
            await output.WriteLineAsync($"The data file '{databasePath}' failed: {failure.Message}");
        }

        return 1;
    }

    private static async Task RunCommandAsync(CliOptions options, string databasePath, TextWriter output, CliEnvironment environment)
    {
        var services = new CliServices(SqliteDatabase.Open(databasePath, environment.Clock, environment.DataFileBusyTimeout), environment.Clock);
        if (options.Import)
        {
            var imported = await new HistoryImport(environment.History, services.Journal, services.Cursors).ImportAsync(environment.User);
            await output.WriteLineAsync($"Windows history: {imported.Read} facts of your sessions read, {imported.Added} new.");
            await output.WriteLineAsync();
        }

        switch (options.Command)
        {
            case CliCommand.Weeks:
                var weeks = await new WeekListing(services.Journal, services.Queries, environment.Clock).AllWeeksAsync();
                WeekPrinter.Print(weeks, output);
                break;
            case CliCommand.StartTest:
                var result = await services.StartTest.RunAsync();
                StartTestPrinter.Print(result, output);
                break;
        }
    }

    private sealed class CliServices
    {
        public CliServices(SqliteDatabase database, TimeProvider clock)
        {
            Journal = new SqliteFactJournal(database);
            Cursors = new SqliteHistoryCursorStore(database);
            var coverage = new SqliteCoverageLog(database);
            var rules = new SqliteLunchRules(database, clock);
            var presence = new PresenceReader(Journal, coverage, new PresenceBuilder(), clock);
            var calculator = new WorkdayCalculator();
            Queries = new WorkTimeQueries(presence, coverage, rules, calculator, clock);
            StartTest = new StartTestReport(Journal, presence, rules, calculator, clock);
        }

        public SqliteFactJournal Journal { get; }

        public SqliteHistoryCursorStore Cursors { get; }

        public WorkTimeQueries Queries { get; }

        public StartTestReport StartTest { get; }
    }
}
