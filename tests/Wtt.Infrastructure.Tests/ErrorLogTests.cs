using Wtt.Infrastructure.Diagnostics;

namespace Wtt.Infrastructure.Tests;

public sealed class ErrorLogTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "wtt-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(2)));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void An_error_is_written_with_its_time_context_and_exception_into_errors_log_in_the_data_folder()
    {
        var log = new ErrorLog(Path.Combine(folder, ErrorLog.FileName), clock);

        log.Write("Heartbeat", new InvalidOperationException("data file locked"));

        var text = File.ReadAllText(log.Path);
        Assert.StartsWith("2026-10-20 09:00:00 +02:00 Heartbeat: System.InvalidOperationException: data file locked", text, StringComparison.Ordinal);
        Assert.Equal("errors.log", Path.GetFileName(log.Path));
    }

    [Fact]
    public void The_log_never_grows_past_its_cap_and_keeps_the_newest_entries()
    {
        var log = new ErrorLog(Path.Combine(folder, ErrorLog.FileName), clock, maxBytes: 4096);

        for (var i = 0; i < 500; i++)
        {
            log.Write($"entry {i:D3}", new InvalidOperationException(new string('x', 40)));
            Assert.True(new FileInfo(log.Path).Length <= log.MaxBytes);
        }

        var text = File.ReadAllText(log.Path);
        Assert.Contains("entry 499", text, StringComparison.Ordinal);
        Assert.DoesNotContain("entry 000", text, StringComparison.Ordinal);
        Assert.StartsWith("2026-10-20", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_cap_is_one_megabyte() => Assert.Equal(1024 * 1024, ErrorLog.DefaultMaxBytes);

    [Fact]
    public void A_log_that_cannot_be_written_never_throws()
    {
        Directory.CreateDirectory(folder);
        var blocked = Path.Combine(folder, "blocked");
        Directory.CreateDirectory(blocked);

        new ErrorLog(blocked, clock).Write("anything", new InvalidOperationException("lost"));
    }
}
