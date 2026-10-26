using Wtt.Infrastructure.Diagnostics;

namespace Wtt.App.Tests;

internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wtt-app-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string name, string content = "")
    {
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, content);
        return file;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Synthetic", now.Offset, "Synthetic", "Synthetic");
}

internal static class Logs
{
    public static readonly DateTimeOffset SyntheticNow = new(2026, 3, 2, 9, 15, 0, TimeSpan.FromHours(1));

    public static ErrorLog At(string path) => new(path, "wtt", new FixedClock(SyntheticNow));
}
