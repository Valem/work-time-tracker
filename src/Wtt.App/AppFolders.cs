using Wtt.Infrastructure.Storage;

namespace Wtt.App;

/// <summary>Where the app reads its program files and keeps its data.</summary>
internal sealed record AppFolders(string DataFolder, string ProgramFolder)
{
    public string ErrorLogFile => Path.Combine(DataFolder, "errors.log");

    public string WebViewDataFolder => Path.Combine(DataFolder, "WebView2");

    public string ContentFolder => Path.Combine(ProgramFolder, "wwwroot");

    public static AppFolders For(AppOptions options) =>
        new(options.DataFolder ?? SqliteDatabase.DefaultDirectory, AppContext.BaseDirectory);
}
