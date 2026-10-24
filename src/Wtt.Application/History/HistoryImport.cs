using Wtt.Application.Ports;

namespace Wtt.Application.History;

public sealed record HistoryImportResult(int Read, int Added);

/// <summary>Imports what Windows keeps since the cursor; importing again adds nothing twice.</summary>
public sealed class HistoryImport(IHistorySource source, IFactJournal journal, IHistoryCursorStore cursors)
{
    public async Task<HistoryImportResult> ImportAsync(UserSid user)
    {
        var cursor = await cursors.LoadAsync();
        var batch = await source.ReadSinceAsync(cursor, user);
        var added = await journal.AppendAsync(batch.Facts);
        await cursors.SaveAsync(batch.Next);
        return new HistoryImportResult(batch.Facts.Count, added);
    }
}
