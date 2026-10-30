using Wtt.Application.Planning;
using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Application.Queries;
using Wtt.Domain.Planning;
using Wtt.Domain.Presence;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure;
using Wtt.Infrastructure.Storage;
using Wtt.PublicHolidays;

namespace Wtt.App.Data;

/// <summary>
/// What the app reads and the few things it writes — lunch rules, «Mein Jahr», the plan and the public holidays it loads —
/// over the data file. Facts, coverage and the history cursor are the Recorder's: the app only reads them. The app is the
/// only program that requests public holidays, and only through its loads.
/// </summary>
internal sealed class AppServices
{
    private static readonly TimeSpan BusyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How long the app waits for a running Recorder to create or update the data file.</summary>
    private static readonly TimeSpan RecorderMigrationWait = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    private AppServices(SqliteDatabase database, IErrorLog errors, TimeProvider clock, CancellationToken stopping)
    {
        Journal = new SqliteFactJournal(database);
        var coverage = new SqliteCoverageLog(database, clock);
        Rules = new SqliteLunchRules(database, clock);
        Queries = new WorkTimeQueries(new PresenceReader(Journal, coverage, new PresenceBuilder(), clock), coverage, Rules, new WorkdayCalculator(), clock);

        var plans = new SqlitePlanStore(database, clock);
        var holidays = new SqlitePublicHolidayStore(database);
        var loads = new PublicHolidayLoads(new PublicHolidayLoader(new OpenHolidaysSource(clock), holidays, errors), holidays, errors, clock, stopping);
        Planner = new HolidayPlanner(plans, holidays, loads, Journal, new PlanRules(), clock);
        Needs = new PublicHolidayNeeds(plans, loads);
        Plans = plans;
    }

    public IFactJournal Journal { get; }

    public ILunchRules Rules { get; }

    public IWorkTimeQueries Queries { get; }

    public IHolidayPlanner Planner { get; }

    public PublicHolidayNeeds Needs { get; }

    public IPlanStore Plans { get; }

    /// <summary>
    /// Opens the data file once it is at this program's schema version. While a Recorder runs, the schema is that
    /// Recorder's to create or update: the app waits for it, so it never migrates a file under an older Recorder after
    /// an upgrade. With no Recorder running, nothing else writes the file and the app brings it up to date itself.
    /// </summary>
    /// <param name="stopping">Cancelled when the app closes; a public-holiday request still running then ends unfinished.</param>
    /// <exception cref="DataFileException">
    /// The file is newer, damaged or locked, or a running Recorder left it at an older schema version.
    /// </exception>
    public static async Task<AppServices> OpenAsync(DataFolder data, Func<bool> recorderRunning, IErrorLog errors, TimeProvider clock, CancellationToken stopping)
    {
        var waitingSince = clock.GetUtcNow();
        while (true)
        {
            if (SqliteDatabase.TryOpenCurrent(data.DataFile, BusyTimeout, out var current))
            {
                return new AppServices(current, errors, clock, stopping);
            }

            if (!recorderRunning())
            {
                return new AppServices(SqliteDatabase.Open(data.DataFile, clock, BusyTimeout), errors, clock, stopping);
            }

            if (clock.GetUtcNow() - waitingSince > RecorderMigrationWait)
            {
                throw new DataFileException(
                    data.DataFile,
                    new InvalidOperationException("The running Recorder has not brought the data file to this program's schema version; an older Recorder may still run."));
            }

            await Task.Delay(Poll);
        }
    }
}
