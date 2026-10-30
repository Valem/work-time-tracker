using Wtt.Application.Planning;
using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;
using Wtt.Infrastructure.Diagnostics;
using Wtt.Infrastructure.Storage;
using Wtt.PublicHolidays;

namespace Wtt.Cli;

/// <summary>
/// holidays, import-holidays and plan. Only holidays sends a request, and only when run by hand; import-holidays replays a
/// saved answer through the same checks, and plan applies gestures through the planner as the app does.
/// </summary>
internal static class PlanningCommands
{
    public static Task<int> RunAsync(CliOptions options, string? databasePath, TextWriter output, CliEnvironment environment)
    {
        var planning = options.Planning ?? throw new InvalidOperationException("A planning command without its options.");
        return options.Command switch
        {
            CliCommand.Holidays => HolidaysAsync(planning, databasePath, output, environment),
            CliCommand.ImportHolidays => LoadIntoAsync(new PublicHolidayFile(planning.AnswerFile!, environment.Clock), planning, databasePath!, output, environment),
            _ => PlanAsync(planning, databasePath!, output, environment),
        };
    }

    private static async Task<int> HolidaysAsync(PlanningOptions planning, string? databasePath, TextWriter output, CliEnvironment environment)
    {
        var canton = planning.Canton!.Value;
        if (planning.DryRun)
        {
            await output.WriteLineAsync(OpenHolidaysSource.Describe(canton, planning.Year));
            return 0;
        }

        var source = new OpenHolidaysSource(environment.Clock);
        if (databasePath is not null)
        {
            return await LoadIntoAsync(source, planning, databasePath, output, environment);
        }

        try
        {
            PlanPrinter.PrintPublicHolidays(await source.ReadAsync(canton, planning.Year, CancellationToken.None), output);
            return 0;
        }
        catch (PublicHolidayReadException failure)
        {
            await output.WriteLineAsync($"Not loaded ({PlanPrinter.Code(failure.Reason)}): {failure.Message}");
            return 1;
        }
    }

    /// <summary>Loads through the product's own loader: stored whole, or refused with its reason in the error log beside the data file.</summary>
    private static async Task<int> LoadIntoAsync(IPublicHolidaySource source, PlanningOptions planning, string databasePath, TextWriter output, CliEnvironment environment)
    {
        var database = SqliteDatabase.Open(databasePath, environment.Clock, environment.DataFileBusyTimeout);
        var errors = ErrorLogBeside(databasePath, environment);
        var loader = new PublicHolidayLoader(source, new SqlitePublicHolidayStore(database), errors);
        var load = await loader.LoadAsync(planning.Canton!.Value, planning.Year, CancellationToken.None);
        if (load.Year is { } year)
        {
            PlanPrinter.PrintPublicHolidays(year, output);
            await output.WriteLineAsync($"Stored in '{databasePath}'.");
            return 0;
        }

        await output.WriteLineAsync(
            $"Refused ({PlanPrinter.Code(load.Failure!.Value)}): {load.Reason} What '{databasePath}' holds for CH-{planning.Canton} {planning.Year} is unchanged; the reason is in '{errors.Path}'.");
        return 1;
    }

    private static async Task<int> PlanAsync(PlanningOptions planning, string databasePath, TextWriter output, CliEnvironment environment)
    {
        var database = SqliteDatabase.Open(databasePath, environment.Clock, environment.DataFileBusyTimeout);
        var clock = planning.Today is { } today ? new TodayClock(today, environment.Clock.LocalTimeZone) : environment.Clock;
        var holidays = new SqlitePublicHolidayStore(database);
        var errors = ErrorLogBeside(databasePath, environment);
        var loads = new PublicHolidayLoads(new PublicHolidayLoader(new NoRequests(), holidays, errors), holidays, errors, clock, CancellationToken.None);
        var planner = new HolidayPlanner(new SqlitePlanStore(database, environment.Clock), holidays, loads, new SqliteFactJournal(database), new PlanRules(), clock);
        var year = planning.Year;
        try
        {
            if (planning.Settings is { } settings)
            {
                await planner.SaveSettingsAsync(year, settings);
                await output.WriteLineAsync($"Saved Mein Jahr for {year}.");
            }

            PlanChange? undo = null;
            foreach (var step in planning.Steps)
            {
                undo = await ApplyAsync(planner, year, step, undo, output);
            }

            await output.WriteLineAsync();
            var days = planning.Days is var (from, to) ? await planner.StatusesAsync(from, to) : null;
            PlanPrinter.PrintPlan(await planner.GetYearAsync(year), await planner.GetSettingsAsync(year), days, output);
            return 0;
        }
        catch (PlanRefusedException refusal)
        {
            await output.WriteLineAsync($"Refused ({PlanPrinter.Code(refusal.Refusal)}): {refusal.Message} That request changed nothing.");
            return 1;
        }
    }

    /// <returns>The request that reverses the step, or null when nothing is left to undo.</returns>
    private static async Task<PlanChange?> ApplyAsync(IHolidayPlanner planner, int year, PlanStep step, PlanChange? undo, TextWriter output)
    {
        switch (step)
        {
            case DragStep drag:
                var dragged = await planner.DragAsync(year, drag.Anchor, drag.To);
                await output.WriteLineAsync($"drag {drag.Anchor:yyyy-MM-dd}..{drag.To:yyyy-MM-dd}: {PlanPrinter.Describe(dragged)}");
                return dragged.Reverse;
            case ClickStep click:
                var clicked = await planner.ClickAsync(year, click.Date);
                await output.WriteLineAsync($"click {click.Date:yyyy-MM-dd}: {PlanPrinter.Describe(clicked)}");
                return clicked.Reverse;
            case TickStep tick:
                var ticked = await planner.TickAsync(year, tick.Date, tick.Ticked);
                await output.WriteLineAsync($"{(tick.Ticked ? "tick" : "untick")} {tick.Date:yyyy-MM-dd}: {PlanPrinter.Describe(ticked)}");
                return ticked.Reverse;
            default:
                if (undo is null)
                {
                    await output.WriteLineAsync("undo: nothing to undo.");
                    return null;
                }

                await planner.RestoreAsync(year, undo);
                await output.WriteLineAsync($"undo: {undo.Bookings.Count} booking(s) and {undo.Ticks.Count} tick(s) restored.");
                return null;
        }
    }

    private static ErrorLog ErrorLogBeside(string databasePath, CliEnvironment environment) =>
        new(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, ErrorLog.FileName), "wtt-cli", environment.Clock);

    /// <summary>The date given as today, at noon in this machine's time zone.</summary>
    private sealed class TodayClock(DateOnly today, TimeZoneInfo zone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => zone;

        public override DateTimeOffset GetUtcNow()
        {
            var noon = today.ToDateTime(new TimeOnly(12, 0));
            return new DateTimeOffset(noon, zone.GetUtcOffset(noon)).ToUniversalTime();
        }
    }

    /// <summary>plan reads what is stored and never requests: holidays and import-holidays load.</summary>
    private sealed class NoRequests : IPublicHolidaySource
    {
        public HolidaySource Source => OpenHolidays.Source;

        public Task<PublicHolidayYear> ReadAsync(Canton canton, int year, CancellationToken cancel) =>
            throw new InvalidOperationException("plan never requests public holidays; load them with holidays or import-holidays.");
    }
}
