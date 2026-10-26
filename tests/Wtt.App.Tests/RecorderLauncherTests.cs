using Wtt.App.Recording;

namespace Wtt.App.Tests;

public sealed class RecorderLauncherTests : IDisposable
{
    private readonly TempFolder program = new();
    private readonly TempFolder data = new();
    private readonly List<string> started = [];

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
        Assert.Equal([Path.Combine(program.Path, "wtt-recorder.exe")], started);
        Assert.False(File.Exists(ErrorLogFile));
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
            _ => throw new System.ComponentModel.Win32Exception(5, "Access is denied"),
            program.Path,
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

    private RecorderLauncher Launcher(bool isRunning) =>
        new(() => isRunning, started.Add, program.Path, Logs.At(ErrorLogFile));
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
