using Wtt.Infrastructure.Diagnostics;
using Wtt.Infrastructure.Storage;

namespace Wtt.Infrastructure;

/// <summary>
/// The folder that holds the product's data — the data file, the errors-only log and the web view's profile —
/// as the App, the Recorder and the installer all expect it.
/// </summary>
public sealed record DataFolder
{
    public const string DataFileName = "wtt.db";
    public const string WebViewFolderName = "WebView2";

    public DataFolder(string path) => Path = System.IO.Path.GetFullPath(path);

    /// <summary>The person's own: <c>%LocalAppData%\WorkTimeTracker</c>.</summary>
    public static DataFolder Default { get; } = new(SqliteDatabase.DefaultDirectory);

    public string Path { get; }

    public string DataFile => System.IO.Path.Combine(Path, DataFileName);

    public string ErrorLogFile => System.IO.Path.Combine(Path, ErrorLog.FileName);

    public string WebViewFolder => System.IO.Path.Combine(Path, WebViewFolderName);
}
