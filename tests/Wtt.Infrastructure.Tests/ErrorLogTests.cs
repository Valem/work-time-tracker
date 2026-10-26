using System.Text;
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
    public void An_error_is_written_with_its_time_source_context_and_exception_into_errors_log_in_the_data_folder()
    {
        var log = new ErrorLog(Path.Combine(folder, "nested", ErrorLog.FileName), "wtt-recorder", clock);

        log.Write("Heartbeat", new InvalidOperationException("data file locked"));

        var text = File.ReadAllText(log.Path);
        Assert.StartsWith("2026-10-20 09:00:00 +02:00 [wtt-recorder] Heartbeat: System.InvalidOperationException: data file locked", text, StringComparison.Ordinal);
        Assert.Equal("errors.log", Path.GetFileName(log.Path));
    }

    [Fact]
    public void An_entry_without_an_exception_is_its_context_alone()
    {
        var log = new ErrorLog(Path.Combine(folder, ErrorLog.FileName), "wtt", clock);

        log.Write("The Recorder could not be started");

        Assert.Equal($"2026-10-20 09:00:00 +02:00 [wtt] The Recorder could not be started{Environment.NewLine}", File.ReadAllText(log.Path));
    }

    [Fact]
    public void Two_programs_writing_the_same_file_both_keep_their_entries_whole()
    {
        var file = Path.Combine(folder, ErrorLog.FileName);
        var app = new ErrorLog(file, "wtt", clock, maxBytes: 4096);
        var recorder = new ErrorLog(file, "wtt-recorder", clock, maxBytes: 4096);

        Parallel.For(0, 200, i => (i % 2 == 0 ? app : recorder).Write($"entry {i:D3}"));

        var bytes = File.ReadAllBytes(file);
        Assert.DoesNotContain((byte)0, bytes);
        Assert.True(bytes.Length <= 4096);
        Assert.All(
            File.ReadAllLines(file),
            line => Assert.Matches(@"^2026-10-20 09:00:00 \+02:00 \[(wtt|wtt-recorder)\] entry \d{3}$", line));
    }

    [Fact]
    public void The_log_never_grows_past_its_cap_and_keeps_the_newest_entries()
    {
        var log = new ErrorLog(Path.Combine(folder, ErrorLog.FileName), "wtt-recorder", clock, maxBytes: 4096);

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
    public void An_entry_larger_than_the_cap_is_shortened()
    {
        var log = new ErrorLog(Path.Combine(folder, ErrorLog.FileName), "wtt", clock, maxBytes: 4096);

        log.Write(new string('x', 20_000));

        Assert.True(new FileInfo(log.Path).Length <= log.MaxBytes);
    }

    [Fact]
    public void The_default_cap_is_one_megabyte() => Assert.Equal(1024 * 1024, ErrorLog.DefaultMaxBytes);

    [Fact]
    public void Entries_are_utf8_without_a_byte_order_mark()
    {
        var log = new ErrorLog(Path.Combine(folder, ErrorLog.FileName), "wtt", clock);

        log.Write("Zürich");

        var bytes = File.ReadAllBytes(log.Path);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("Zürich", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void A_log_that_cannot_be_written_never_throws()
    {
        Directory.CreateDirectory(folder);
        var blocked = Path.Combine(folder, "blocked");
        Directory.CreateDirectory(blocked);

        new ErrorLog(blocked, "wtt-recorder", clock).Write("anything", new InvalidOperationException("lost"));
    }
}
