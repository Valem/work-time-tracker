using Wtt.App.Recording;
using Wtt.Infrastructure;

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
    public void A_Recorder_on_one_data_folder_is_never_taken_for_one_on_another()
    {
        var trial = new DataFolder(data.Path);
        var other = new DataFolder(program.Path);

        using (new Mutex(initiallyOwned: false, trial.RecorderMutexName))
        {
            Assert.True(RecorderLauncher.IsRunning(trial));
            Assert.False(RecorderLauncher.IsRunning(other));
        }

        Assert.False(RecorderLauncher.IsRunning(trial));
    }

    [Fact]
    public void The_persons_Recorder_holds_the_shared_name_and_a_trial_one_a_name_of_its_own()
    {
        var trial = new DataFolder(data.Path);

        Assert.Equal(@"Local\WorkTimeTracker.Recorder", DataFolder.Default.RecorderMutexName);
        Assert.NotEqual(DataFolder.Default.RecorderMutexName, trial.RecorderMutexName);
        Assert.Equal(trial.RecorderMutexName, new DataFolder(data.Path.ToUpperInvariant() + Path.DirectorySeparatorChar).RecorderMutexName);
        Assert.Equal(
            DataFolder.Default.RecorderMutexName,
            new DataFolder(DataFolder.Default.Path.ToLowerInvariant() + Path.DirectorySeparatorChar).RecorderMutexName);
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
