using Wtt.App.Bridge;
using Wtt.App.Data;
using Wtt.App.Recording;
using Wtt.Domain.Time;
using Wtt.Infrastructure.Diagnostics;
using WindowsApplication = System.Windows.Forms.Application;

namespace Wtt.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = AppOptions.Parse(args);
        var folders = AppFolders.For(options);
        var log = new ErrorLog(folders.ErrorLogFile, "wtt");

        AppLocale.ApplyToHost();
        ApplicationConfiguration.Initialize();
        WindowsApplication.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        WindowsApplication.ThreadException += (_, e) => log.Write("Unhandled error in the window", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write("Unhandled error", e.ExceptionObject as Exception);

        RecorderLauncher.ForThisSession(folders.ProgramFolder, options.DataFolder, log).EnsureRunning();

        var clock = TimeProvider.System;
        using var stopping = new CancellationTokenSource();
        bool RecorderRunning() => RecorderLauncher.IsRunning(folders.Data);
        var data = Task.Run(() => AppServices.OpenAsync(folders.Data, RecorderRunning, log, clock, stopping.Token));
        _ = NeedThisYearAsync(data, clock, log);
        var bridge = new BridgeHost(new BridgeHandlers(data, RecorderRunning, clock), log);

        WindowsApplication.Run(new MainForm(folders, log, bridge, data));
        stopping.Cancel();
    }

    /// <summary>App start needs this year's public holidays; a data file that failed to open is the window's to show.</summary>
    private static async Task NeedThisYearAsync(Task<AppServices> data, TimeProvider clock, ErrorLog log)
    {
        await ((Task)data).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!data.IsCompletedSuccessfully)
        {
            return;
        }

        try
        {
            var today = clock.GetLocalNow().LocalDate();
            await data.Result.Needs.NeedAsync(today, today);
        }
        catch (Exception failure)
        {
            log.Write("Asking for this year's public holidays at start", failure);
        }
    }
}
