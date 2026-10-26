using System.Text;

namespace Wtt.App.Diagnostics;

/// <summary>
/// Errors only, kept in the data folder and never sent anywhere. When the file would grow past its cap,
/// the oldest entries are dropped so that the newest half of the cap remains.
/// </summary>
internal sealed class ErrorLog(string path, string source, TimeProvider clock, long capBytes = ErrorLog.DefaultCapBytes)
{
    public const long DefaultCapBytes = 1024 * 1024;

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly Lock gate = new();

    public string Path => path;

    /// <summary>Appends one entry; a failure to write is swallowed, since logging must never stop the program.</summary>
    public void Write(string what, Exception? failure = null)
    {
        var entry = Utf8.GetBytes(Clamp(Format(what, failure)));
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (file.Length + entry.Length > capBytes)
                {
                    KeepNewest(file, capBytes / 2 - entry.Length);
                }

                file.Seek(0, SeekOrigin.End);
                file.Write(entry);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private string Format(string what, Exception? failure)
    {
        var at = clock.GetLocalNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        var text = failure is null ? what : $"{what}: {failure}";
        return $"{at} [{source}] {text}{Environment.NewLine}";
    }

    private string Clamp(string entry)
    {
        var limit = (int)Math.Min(int.MaxValue, capBytes / 4);
        return Utf8.GetByteCount(entry) <= limit ? entry : entry[..(limit / 4)] + Environment.NewLine;
    }

    private static void KeepNewest(FileStream file, long budget)
    {
        var content = new byte[file.Length];
        file.Seek(0, SeekOrigin.Begin);
        file.ReadExactly(content);

        var start = (int)Math.Max(0, content.Length - Math.Max(0, budget));
        while (start < content.Length && start > 0 && content[start - 1] != (byte)'\n')
        {
            start++;
        }

        file.SetLength(0);
        file.Write(content, start, content.Length - start);
    }
}
