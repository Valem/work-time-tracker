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

/// <summary>
/// Starts the Recorder from the app's own folder when this session has none running. When the app was pointed
/// at another data folder, the Recorder it starts records there too, so a trial run never touches the
/// person's data.
/// </summary>
internal sealed class RecorderLauncher(
    Func<bool> isRunning,
    Action<string, IReadOnlyList<string>> start,
    string programFolder,
    string? dataFolder,
    ErrorLog log)
{
    public const string MutexName = @"Local\WorkTimeTracker.Recorder";
    public const string ExecutableName = "wtt-recorder.exe";
    public const string DataFolderOption = "--data-dir";

    public static RecorderLauncher ForThisSession(string programFolder, string? dataFolder, ErrorLog log) =>
        new(() => NamedMutex.Exists(MutexName), StartProcess, programFolder, dataFolder, log);

    public IReadOnlyList<string> Arguments => dataFolder is null ? [] : [DataFolderOption, dataFolder];

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
            start(executable, Arguments);
            return RecorderLaunch.Started;
        }
        catch (Exception failure)
        {
            log.Write($"The Recorder could not be started from {executable}", failure);
            return RecorderLaunch.Failed;
        }
    }

    private static void StartProcess(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
    }
}
