using Microsoft.Data.Sqlite;
using Wtt.Application.History;
using Wtt.Application.Ports;
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
               wtt-cli import-xml <events.xml> --db <file> --user <sid>

        Commands:
          weeks        Every week from the oldest event Windows keeps to today:
                       each day's start, end, break and total, rebuilt days marked, and the week total.
          start-test   Where both Windows logs reach back: the start the 2-minute test finds
                       against the unlock-based start, per workday, and the share within 5 minutes.
          import-xml   Imports a file of Windows event XML (as EventRecord.ToXml renders each record)
                       through the same history import as the Windows logs, into the data file --db names.
                       Read it afterwards with: weeks --db <file> --no-import

        Options:
          --db <file>   The data file to use (default: %LocalAppData%\WorkTimeTracker\wtt-cli.db).
                        import-xml needs it and never writes into %LocalAppData%\WorkTimeTracker.
          --no-import   Do not read Windows history first; use what the data file holds.
          --user <sid>  import-xml: the account whose sessions count, such as a fixture's synthetic SID.
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
        if (options.Command == CliCommand.ImportXml && MayReachDataFolder(databasePath))
        {
            await output.WriteLineAsync($"import-xml never writes into '{SqliteDatabase.DefaultDirectory}': imported facts must not mix with real use. Name a data file elsewhere.");
            return 2;
        }

        try
        {
            await RunCommandAsync(options, databasePath, output, environment);
            return 0;
        }
        catch (EventFileException failure)
        {
            await output.WriteLineAsync($"{failure.Message} Nothing was imported; the data file is unchanged.");
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
        if (options is { Command: CliCommand.ImportXml, EventFile: { } eventFile, User: { } user })
        {
            await ImportXmlAsync(eventFile, user, databasePath, output, environment);
            return;
        }

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

    /// <summary>
    /// The file is read and checked before the data file is opened, then imported as the Windows logs are: since the
    /// data file's history cursor, at this machine's time zone, keeping only the given account's sessions.
    /// </summary>
    private static async Task ImportXmlAsync(string eventFile, UserSid user, string databasePath, TextWriter output, CliEnvironment environment)
    {
        var history = EventLogHistorySource.FromXmlFile(eventFile, environment.Clock.LocalTimeZone);
        var services = new CliServices(SqliteDatabase.Open(databasePath, environment.Clock, environment.DataFileBusyTimeout), environment.Clock);
        var imported = await new HistoryImport(history, services.Journal, services.Cursors).ImportAsync(user);
        await output.WriteLineAsync($"Event file: {imported.Read} facts of {user.Value}'s sessions read, {imported.Added} new, in '{databasePath}'.");
    }

    /// <summary>
    /// Whether the path is or may be inside the product's data folder. A path starting with \\ (device or UNC spelling,
    /// which can name the same disk) counts as inside; otherwise junctions and symbolic links on the way are followed
    /// to their final targets before the folders are compared.
    /// </summary>
    private static bool MayReachDataFolder(string path)
    {
        if (Path.GetFullPath(path).StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        var folder = Path.TrimEndingDirectorySeparator(Resolved(SqliteDatabase.DefaultDirectory)) + Path.DirectorySeparatorChar;
        return Resolved(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The full path with each existing folder or file on it replaced by the final target of its junction or link.</summary>
    private static string Resolved(string path)
    {
        var full = Path.GetFullPath(path);
        var resolved = Path.GetPathRoot(full)!;
        foreach (var part in full[resolved.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(resolved, part);
            var target = Directory.Exists(next) ? Directory.ResolveLinkTarget(next, returnFinalTarget: true)
                : File.Exists(next) ? File.ResolveLinkTarget(next, returnFinalTarget: true)
                : null;
            resolved = target is null ? next : Path.GetFullPath(target.FullName);
        }

        return resolved;
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
