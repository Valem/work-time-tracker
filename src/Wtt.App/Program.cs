using Wtt.App.Bridge;
using Wtt.App.Data;
using Wtt.App.Recording;
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
        bool RecorderRunning() => RecorderLauncher.IsRunning(folders.Data);
        var data = Task.Run(() => AppServices.OpenAsync(folders.Data, RecorderRunning, clock));
        var bridge = new BridgeHost(new BridgeHandlers(data, RecorderRunning, clock), log);

        WindowsApplication.Run(new MainForm(folders, log, bridge, data));
    }
}
