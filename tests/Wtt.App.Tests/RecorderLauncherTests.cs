using Wtt.App.Recording;

namespace Wtt.App.Tests;

public sealed class RecorderLauncherTests : IDisposable
{
    private readonly TempFolder program = new();
    private readonly TempFolder data = new();
    private readonly List<(string Executable, IReadOnlyList<string> Arguments)> started = [];

    public void Dispose()
    {
        program.Dispose();
        data.Dispose();
    }

    [Fact]
    public void A_running_Recorder_is_left_alone()
    {
        program.CreateFile(RecorderLauncher.ExecutableName);

        var outcome = Launcher(isRunning: true).EnsureRunning();

        Assert.Equal(RecorderLaunch.AlreadyRunning, outcome);
        Assert.Empty(started);
    }

    [Fact]
    public void A_stopped_Recorder_is_started_from_the_apps_own_folder()
    {
        program.CreateFile(RecorderLauncher.ExecutableName);

        var outcome = Launcher(isRunning: false).EnsureRunning();

        Assert.Equal(RecorderLaunch.Started, outcome);
        var (executable, arguments) = Assert.Single(started);
        Assert.Equal(Path.Combine(program.Path, "wtt-recorder.exe"), executable);
        Assert.Empty(arguments);
        Assert.False(File.Exists(ErrorLogFile));
    }

    [Fact]
    public void A_Recorder_started_by_an_app_on_another_data_folder_records_there()
    {
        program.CreateFile(RecorderLauncher.ExecutableName);

        var outcome = Launcher(isRunning: false, dataFolder: data.Path).EnsureRunning();

        Assert.Equal(RecorderLaunch.Started, outcome);
        var (_, arguments) = Assert.Single(started);
        Assert.Equal(["--data-dir", data.Path], arguments);
    }

    [Fact]
    public void A_missing_Recorder_is_logged_and_the_app_goes_on()
    {
        var outcome = Launcher(isRunning: false).EnsureRunning();

        Assert.Equal(RecorderLaunch.Missing, outcome);
        Assert.Empty(started);
        Assert.Contains("wtt-recorder.exe does not exist", File.ReadAllText(ErrorLogFile));
    }

    [Fact]
    public void A_Recorder_that_cannot_start_is_logged_and_the_app_goes_on()
    {
        program.CreateFile(RecorderLauncher.ExecutableName);
        var launcher = new RecorderLauncher(
            () => false,
            (_, _) => throw new System.ComponentModel.Win32Exception(5, "Access is denied"),
            program.Path,
            null,
            Logs.At(ErrorLogFile));

        var outcome = launcher.EnsureRunning();

        Assert.Equal(RecorderLaunch.Failed, outcome);
        Assert.Contains("Access is denied", File.ReadAllText(ErrorLogFile));
    }

    [Fact]
    public void The_Recorder_is_found_by_the_shared_mutex_name()
    {
        Assert.Equal(@"Local\WorkTimeTracker.Recorder", RecorderLauncher.MutexName);
    }

    private string ErrorLogFile => Path.Combine(data.Path, "errors.log");

    private RecorderLauncher Launcher(bool isRunning, string? dataFolder = null) =>
        new(() => isRunning, (executable, arguments) => started.Add((executable, arguments)), program.Path, dataFolder, Logs.At(ErrorLogFile));
}

public sealed class NamedMutexTests
{
    [Fact]
    public void A_held_mutex_exists_and_a_released_one_does_not()
    {
        var name = $@"Local\WorkTimeTracker.Tests.{Guid.NewGuid():N}";
        Assert.False(NamedMutex.Exists(name));

        using (new Mutex(initiallyOwned: false, name))
        {
            Assert.True(NamedMutex.Exists(name));
        }

        Assert.False(NamedMutex.Exists(name));
    }
}
