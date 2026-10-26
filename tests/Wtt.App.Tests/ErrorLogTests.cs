using System.Text;
using Wtt.App.Diagnostics;

namespace Wtt.App.Tests;

public sealed class ErrorLogTests : IDisposable
{
    private readonly TempFolder data = new();

    public void Dispose() => data.Dispose();

    [Fact]
    public void An_entry_carries_its_time_with_offset_source_and_failure()
    {
        var file = Path.Combine(data.Path, "nested", "errors.log");

        Logs.At(file).Write("Something failed", new InvalidOperationException("synthetic failure"));

        var text = File.ReadAllText(file);
        Assert.StartsWith("2026-03-02T09:15:00.000+01:00 [wtt] Something failed: System.InvalidOperationException: synthetic failure", text);
    }

    [Fact]
    public void The_file_never_grows_past_its_cap_and_keeps_the_newest_entries()
    {
        const long cap = 4096;
        var file = Path.Combine(data.Path, "errors.log");
        var log = Logs.At(file, cap);

        for (var i = 0; i < 500; i++)
        {
            log.Write($"entry {i:D4}");
            Assert.True(new FileInfo(file).Length <= cap, $"length {new FileInfo(file).Length} after entry {i}");
        }

        var lines = File.ReadAllLines(file);
        Assert.EndsWith("entry 0499", lines[^1]);
        Assert.All(lines, line => Assert.StartsWith("2026-03-02T09:15:00.000+01:00 [wtt] entry ", line));
        Assert.True(lines.Length > 10);
    }

    [Fact]
    public void The_default_cap_is_one_megabyte()
    {
        Assert.Equal(1024 * 1024, ErrorLog.DefaultCapBytes);
    }

    [Fact]
    public void An_entry_larger_than_the_cap_is_shortened()
    {
        const long cap = 4096;
        var file = Path.Combine(data.Path, "errors.log");

        Logs.At(file, cap).Write(new string('x', 20_000));

        Assert.True(new FileInfo(file).Length <= cap);
    }

    [Fact]
    public void A_log_that_cannot_be_written_never_throws()
    {
        var occupied = data.CreateFile("not-a-folder");
        var log = Logs.At(Path.Combine(occupied, "errors.log"));

        var failure = Record.Exception(() => log.Write("lost"));

        Assert.Null(failure);
    }

    [Fact]
    public void Entries_are_utf8_without_a_byte_order_mark()
    {
        var file = Path.Combine(data.Path, "errors.log");

        Logs.At(file).Write("Zürich");

        var bytes = File.ReadAllBytes(file);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("Zürich", Encoding.UTF8.GetString(bytes));
    }
}
