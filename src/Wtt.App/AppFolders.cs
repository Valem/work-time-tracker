using Wtt.Infrastructure;

namespace Wtt.App;

/// <summary>Where the app reads its program files and keeps its data.</summary>
internal sealed record AppFolders(DataFolder Data, string ProgramFolder)
{
    public string ErrorLogFile => Data.ErrorLogFile;

    public string WebViewDataFolder => Data.WebViewFolder;

    public string ContentFolder => Path.Combine(ProgramFolder, "wwwroot");

    public static AppFolders For(AppOptions options) =>
        new(options.DataFolder is null ? DataFolder.Default : new DataFolder(options.DataFolder), AppContext.BaseDirectory);
}
