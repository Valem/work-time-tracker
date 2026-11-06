using System.Security.Principal;
using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Application.Recording;
using Wtt.Infrastructure.Diagnostics;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Live;
using Wtt.Infrastructure.Storage;

namespace Wtt.Recorder;

/// <summary>
/// The headless per-user Recorder: one per Windows user session, started at sign-in by the Run key or by
/// the App. It records live facts and coverage and catches up on Windows history; it knows nothing about days.
/// </summary>
internal static class RecorderApp
{
    /// <summary>Windows gives a program a few seconds at sign-out or shutdown; the last facts must be written in them.</summary>
    private static readonly TimeSpan EndSessionWait = TimeSpan.FromSeconds(4);

    /// <summary>Windows may freeze the process soon after announcing a suspend; the standby is written first.</summary>
    private static readonly TimeSpan SuspendWait = TimeSpan.FromSeconds(1.5);

    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(10);

    /// <summary>An import still running at the end is left unfinished: its facts commit whole or not at all, and the next start reads again.</summary>
    private static readonly TimeSpan ImportStopWait = TimeSpan.FromSeconds(1);

    public static int Run(string[] args)
    {
        if (!RecorderOptions.TryParse(args, out var options))
        {
            return 2;
        }

        var errors = new ErrorLog(options.Data.ErrorLogFile, "wtt-recorder");
        AppDomain.CurrentDomain.UnhandledException += (_, failure) =>
            errors.Write("Recorder failed", failure.ExceptionObject as Exception ?? new InvalidOperationException("Unknown failure."));

        using var instance = new Mutex(initiallyOwned: true, options.Data.RecorderMutexName, out var firstInstance);
        if (!firstInstance)
        {
            return 0;
        }

        try
        {
            Record(options, errors);
            return 0;
        }
        catch (Exception failure)
        {
            errors.Write("Recorder stopped", failure);
            return 1;
        }
        finally
        {
            instance.ReleaseMutex();
        }
    }

    private static void Record(RecorderOptions options, IErrorLog errors)
    {
        var clock = TimeProvider.System;
        var desktop = new WindowsDesktop(clock);
        var imports = new RecorderWorker(errors);
        var recorder = Compose(options, errors, desktop, clock, imports);
        recorder.StartAsync().GetAwaiter().GetResult();
        recorder.CatchUpAsync().GetAwaiter().GetResult();

        var worker = new RecorderWorker(errors);

        SignalWindow? window = null;
        window = new SignalWindow(
            onSignal: signal => Dispatch(worker, recorder, signal),
            onCloseRequested: () => window?.Close(),
            onError: failure => errors.Write("Reading a Windows notification", failure),
            desktop,
            clock);

        using var heartbeat = new Timer(
            _ => worker.Post("Heartbeat", recorder.HeartbeatAsync),
            null,
            LiveRecorder.HeartbeatInterval,
            LiveRecorder.HeartbeatInterval);
        using var inputPoll = new Timer(
            _ =>
            {
                if (recorder.AwaitsInput)
                {
                    worker.Post("Reading the last input", recorder.PollInputAsync);
                }
            },
            null,
            LiveRecorder.InputPollInterval,
            LiveRecorder.InputPollInterval);

        window.Run();
        worker.Post("Closing", recorder.HeartbeatAsync);
        worker.Stop(StopWait);
        imports.Stop(ImportStopWait);
    }

    /// <summary>
    /// At sign-out and shutdown the window waits for the write, as Windows ends the process once it returns; at a
    /// suspend it waits briefly, as Windows may freeze the process.
    /// </summary>
    private static void Dispatch(RecorderWorker worker, LiveRecorder recorder, LiveSignal signal)
    {
        var context = $"Recording {signal.Kind}";
        var wait = signal.Kind switch
        {
            LiveSignalKind.SigningOut or LiveSignalKind.ShuttingDown => EndSessionWait,
            LiveSignalKind.Suspending => SuspendWait,
            _ => TimeSpan.Zero,
        };
        if (wait > TimeSpan.Zero)
        {
            worker.PostAndWait(context, () => recorder.OnSignalAsync(signal), wait);
        }
        else
        {
            worker.Post(context, () => recorder.OnSignalAsync(signal));
        }
    }

    private static LiveRecorder Compose(RecorderOptions options, IErrorLog errors, IDesktop desktop, TimeProvider clock, RecorderWorker imports)
    {
        var database = SqliteDatabase.Open(options.Data.DataFile, clock);
        var journal = new SqliteFactJournal(database);
        var user = new UserSid(WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("No Windows user."));
        var history = new HistoryImport(new HistoryInCurrentZone(), journal, new SqliteHistoryCursorStore(database));
        var catchUp = new QueuedCatchUp(new HistoryCatchUp(history, user, errors), imports);
        return new LiveRecorder(journal, new SqliteCoverageLog(database, clock), catchUp, desktop, clock);
    }

    /// <summary>The Recorder runs for days: each read takes the time zone in force then, not the one at start.</summary>
    private sealed class HistoryInCurrentZone : IHistorySource
    {
        public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user) =>
            new EventLogHistorySource(TimeZoneInfo.Local).ReadSinceAsync(cursor, user);
    }
}
