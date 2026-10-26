using System.Diagnostics;
using Wtt.Infrastructure;
using Wtt.Infrastructure.Diagnostics;

namespace Wtt.App.Recording;

internal enum RecorderLaunch
{
    AlreadyRunning,
    Started,
    Missing,
    Failed,
}

/// <summary>
/// Starts the Recorder from the app's own folder when this session has none running on the app's data folder.
/// When the app was pointed at another data folder, the Recorder it starts records there and holds a mutex of
/// that folder, so a trial run never touches the person's data or recording.
/// </summary>
internal sealed class RecorderLauncher(
    Func<bool> isRunning,
    Action<string, IReadOnlyList<string>> start,
    string programFolder,
    string? dataFolder,
    ErrorLog log)
{
    public const string ExecutableName = "wtt-recorder.exe";
    public const string DataFolderOption = "--data-dir";

    public static RecorderLauncher ForThisSession(string programFolder, string? dataFolder, ErrorLog log)
    {
        var data = dataFolder is null ? DataFolder.Default : new DataFolder(dataFolder);
        return new(() => IsRunning(data), StartProcess, programFolder, dataFolder, log);
    }

    /// <summary>Whether a Recorder on <paramref name="data"/> runs in this session.</summary>
    public static bool IsRunning(DataFolder data) => NamedMutex.Exists(data.RecorderMutexName);

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
