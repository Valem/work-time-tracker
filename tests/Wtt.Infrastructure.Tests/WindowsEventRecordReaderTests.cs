using Wtt.Infrastructure.History;

namespace Wtt.Infrastructure.Tests;

/// <summary>
/// Checks the query mechanics against the machine's own System log. Nothing read is kept or
/// printed: records are only parsed and matched against the selectors.
/// </summary>
public sealed class WindowsEventRecordReaderTests
{
    [Fact]
    public void The_system_log_query_returns_only_selected_records_that_parse()
    {
        var system = HistoryChannels.All.Single(channel => channel.Name == HistoryChannels.System);

        var records = new WindowsEventRecordReader()
            .Read(system.Name, system.Selectors, DateTimeOffset.UtcNow.AddDays(-30))
            .Take(200)
            .Select(RawEvent.Parse)
            .ToList();

        Assert.All(records, record => Assert.Contains(
            system.Selectors,
            selector => selector.Provider == record.Provider && selector.EventIds.Contains(record.EventId)));
    }

    [Fact]
    public void A_missing_channel_reads_as_empty()
    {
        var records = new WindowsEventRecordReader().Read("Wtt-Missing-Channel/Operational", [new EventSelector("X", [1])], null);

        Assert.Empty(records);
    }

    [Fact]
    public void The_query_selects_providers_events_and_time()
    {
        var xpath = WindowsEventRecordReader.XPath(
            [new EventSelector("Microsoft-Windows-Winlogon", [7001, 7002])],
            new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(
            "*[System[((Provider[@Name='Microsoft-Windows-Winlogon'] and (EventID=7001 or EventID=7002))) and TimeCreated[@SystemTime>='2026-10-20T06:00:00.000Z']]]",
            xpath);
    }
}
