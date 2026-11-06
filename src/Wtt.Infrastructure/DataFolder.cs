using System.Security.Cryptography;
using System.Text;
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

    private const string RecorderMutexBase = @"Local\WorkTimeTracker.Recorder";

    public DataFolder(string path) => Path = System.IO.Path.GetFullPath(path);

    /// <summary>The person's own: <c>%LocalAppData%\WorkTimeTracker</c>.</summary>
    public static DataFolder Default { get; } = new(SqliteDatabase.DefaultDirectory);

    public string Path { get; }

    public string DataFile => System.IO.Path.Combine(Path, DataFileName);

    public string ErrorLogFile => System.IO.Path.Combine(Path, ErrorLog.FileName);

    public string WebViewFolder => System.IO.Path.Combine(Path, WebViewFolderName);

    public bool IsDefault => Normalized(Path) == Normalized(Default.Path);

    /// <summary>
    /// One Recorder per user session and data folder: the person's own holds the shared name, a Recorder on any
    /// other folder (a trial run) a name of its own, so neither ever takes the other for running. tools/run.ps1
    /// computes the same name.
    /// </summary>
    public string RecorderMutexName => IsDefault ? RecorderMutexBase : $"{RecorderMutexBase}.{Fingerprint(Path)}";

    /// <summary>The first 16 hex digits of the SHA-256 of the normalized path in UTF-8.</summary>
    private static string Fingerprint(string path) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalized(path))))[..16];

    private static string Normalized(string path) =>
        System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).ToUpperInvariant();
}
