using Wtt.App.Diagnostics;
using Wtt.App.Recording;
using WindowsApplication = System.Windows.Forms.Application;

namespace Wtt.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var options = AppOptions.Parse(args);
        var folders = AppFolders.For(options);
        var log = new ErrorLog(folders.ErrorLogFile, "wtt", TimeProvider.System);

        ApplicationConfiguration.Initialize();
        WindowsApplication.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        WindowsApplication.ThreadException += (_, e) => log.Write("Unhandled error in the window", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write("Unhandled error", e.ExceptionObject as Exception);

        RecorderLauncher.ForThisSession(folders.ProgramFolder, options.DataFolder, log).EnsureRunning();

        WindowsApplication.Run(new MainForm(folders, log));
    }
}
