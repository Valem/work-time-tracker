namespace Wtt.App.Tests;

public sealed class AppFoldersTests
{
    [Fact]
    public void By_default_the_data_lives_in_the_users_local_profile()
    {
        var folders = AppFolders.For(AppOptions.Parse([]));

        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkTimeTracker");
        Assert.Equal(expected, folders.DataFolder);
        Assert.Equal(Path.Combine(expected, "errors.log"), folders.ErrorLogFile);
        Assert.Equal(Path.Combine(expected, "WebView2"), folders.WebViewDataFolder);
    }

    [Fact]
    public void Another_data_folder_can_be_named()
    {
        var folders = AppFolders.For(AppOptions.Parse(["--data", @"C:\synthetic\data"]));

        Assert.Equal(@"C:\synthetic\data", folders.DataFolder);
        Assert.Equal(@"C:\synthetic\data\WebView2", folders.WebViewDataFolder);
    }

    [Fact]
    public void Pages_load_from_the_program_folder()
    {
        var folders = AppFolders.For(AppOptions.Parse([]));

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "wwwroot"), folders.ContentFolder);
    }

    [Fact]
    public void Unknown_arguments_are_ignored()
    {
        Assert.Null(AppOptions.Parse(["--unknown", "--data"]).DataFolder);
    }
}
