using System.Globalization;
using System.Text;
using Wtt.Application.Ports;

namespace Wtt.Infrastructure.Diagnostics;

/// <summary>
/// Errors only, as text in the data folder; never sent anywhere. When the file outgrows its cap, its older
/// half is dropped, so it never holds more than <see cref="MaxBytes"/>. Writing it never throws: a log that
/// cannot be written must not stop the program it serves.
/// </summary>
public sealed class ErrorLog(string path, TimeProvider? clock = null, long maxBytes = ErrorLog.DefaultMaxBytes) : IErrorLog
{
    public const string FileName = "errors.log";
    public const long DefaultMaxBytes = 1024 * 1024;

    private static readonly Lock Gate = new();
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public long MaxBytes { get; } = maxBytes;

    public void Write(string context, Exception error)
    {
        var entry = string.Create(
            CultureInfo.InvariantCulture,
            $"{clock.GetLocalNow():yyyy-MM-dd HH:mm:ss zzz} {context}: {error}{Environment.NewLine}");
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                Append(Encoding.UTF8.GetBytes(entry));
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Nowhere left to report it.
        }
    }

    private void Append(byte[] entry)
    {
        using var file = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        if (entry.Length >= MaxBytes)
        {
            entry = entry[^(int)(MaxBytes / 2)..];
        }

        if (file.Length + entry.Length > MaxBytes)
        {
            KeepNewestHalf(file);
        }

        file.Seek(0, SeekOrigin.End);
        file.Write(entry);
    }

    /// <summary>Drops everything before the first line that starts in the newest half of the file.</summary>
    private void KeepNewestHalf(FileStream file)
    {
        var keep = (int)Math.Min(file.Length, MaxBytes / 2);
        var tail = new byte[keep];
        file.Seek(-keep, SeekOrigin.End);
        file.ReadExactly(tail);

        var lineStart = Array.IndexOf(tail, (byte)'\n') + 1;
        var kept = lineStart > 0 && lineStart < tail.Length ? tail[lineStart..] : [];
        file.SetLength(0);
        file.Write(kept);
    }
}
