using Wtt.Infrastructure.Storage;

namespace Wtt.Recorder.Tests;

public sealed class RecorderOptionsTests
{
    [Fact]
    public void Without_arguments_the_Recorder_writes_wtt_db_in_the_local_data_folder()
    {
        Assert.True(RecorderOptions.TryParse([], out var options));

        Assert.Equal(SqliteDatabase.DefaultDirectory, options.DataFolder);
        Assert.Equal(Path.Combine(SqliteDatabase.DefaultDirectory, "wtt.db"), options.DataFile);
        Assert.EndsWith(@"\WorkTimeTracker", options.DataFolder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_data_folder_can_be_named_for_a_trial_run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wtt-trial");

        Assert.True(RecorderOptions.TryParse(["--data-dir", folder], out var options));

        Assert.Equal(Path.Combine(folder, "wtt.db"), options.DataFile);
    }

    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--verbose")]
    [InlineData("--data-dir", " ")]
    [InlineData("--data-dir", "a", "b")]
    public void Anything_else_is_refused(params string[] args) =>
        Assert.False(RecorderOptions.TryParse(args, out _));

    [Fact]
    public void The_single_instance_mutex_is_per_user_session_and_named_as_agreed_with_the_App() =>
        Assert.Equal(@"Local\WorkTimeTracker.Recorder", RecorderApp.InstanceMutexName);
}
