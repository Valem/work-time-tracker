using System.Diagnostics;
using Wtt.App.Diagnostics;

namespace Wtt.App.Recording;

internal enum RecorderLaunch
{
    AlreadyRunning,
    Started,
    Missing,
    Failed,
}

/// <summary>Starts the Recorder from the app's own folder when this session has none running.</summary>
internal sealed class RecorderLauncher(Func<bool> isRunning, Action<string> start, string programFolder, ErrorLog log)
{
    public const string MutexName = @"Local\WorkTimeTracker.Recorder";
    public const string ExecutableName = "wtt-recorder.exe";

    public static RecorderLauncher ForThisSession(string programFolder, ErrorLog log) =>
        new(() => NamedMutex.Exists(MutexName), StartProcess, programFolder, log);

    public RecorderLaunch EnsureRunning()
    {
        if (isRunning())
        {
            return RecorderLaunch.AlreadyRunning;
        }

        var executable = Path.Combine(programFolder, ExecutableName);
        if (!File.Exists(executable))
        {
            log.Write($"The Recorder is not running and {executable} does not exist");
            return RecorderLaunch.Missing;
        }

        try
        {
            start(executable);
            return RecorderLaunch.Started;
        }
        catch (Exception failure)
        {
            log.Write($"The Recorder could not be started from {executable}", failure);
            return RecorderLaunch.Failed;
        }
    }

    private static void StartProcess(string executable)
    {
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
        });
    }
}
