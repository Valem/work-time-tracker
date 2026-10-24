using Wtt.Application.History;
using Wtt.Application.Ports;
using static Wtt.Application.Tests.Facts;

namespace Wtt.Application.Tests;

public sealed class HistoryImportTests
{
    private static readonly UserSid User = new("S-1-5-21-1000000000-2000000000-3000000000-1001");

    [Fact]
    public async Task Importing_twice_adds_nothing_the_second_time_and_reads_from_the_saved_cursor()
    {
        var source = new FakeHistorySource([.. Use("2026-10-20 08:00", "2026-10-20 17:00")]);
        var journal = new InMemoryFactJournal();
        var cursors = new InMemoryCursorStore();
        var import = new HistoryImport(source, journal, cursors);

        var first = await import.ImportAsync(User);
        var second = await import.ImportAsync(User);

        Assert.Equal(new HistoryImportResult(2, 2), first);
        Assert.Equal(0, second.Added);
        Assert.Equal(HistoryCursor.Beginning, source.CursorsSeen[0]);
        Assert.Equal(At("2026-10-20 17:00"), source.CursorsSeen[1].ReadFrom);
    }
}
