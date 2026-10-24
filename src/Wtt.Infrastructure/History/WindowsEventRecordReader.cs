using System.Diagnostics.Eventing.Reader;
using System.Globalization;

namespace Wtt.Infrastructure.History;

/// <summary>Reads the live Windows event logs, read-only; both channels are readable without admin rights.</summary>
internal sealed class WindowsEventRecordReader : IEventRecordReader
{
    public IEnumerable<string> Read(string channel, IReadOnlyList<EventSelector> selectors, DateTimeOffset? since)
    {
        using var reader = OpenOrNull(new EventLogQuery(channel, PathType.LogName, XPath(selectors, since)));
        if (reader is null)
        {
            yield break;
        }

        for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
        {
            using (record)
            {
                yield return record.ToXml();
            }
        }
    }

    internal static string XPath(IReadOnlyList<EventSelector> selectors, DateTimeOffset? since)
    {
        var events = string.Join(
            " or ",
            selectors.Select(selector =>
                $"(Provider[@Name='{selector.Provider}'] and ({string.Join(" or ", selector.EventIds.Select(id => $"EventID={id}"))}))"));
        var time = since is { } from
            ? string.Create(CultureInfo.InvariantCulture, $" and TimeCreated[@SystemTime>='{from.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}']")
            : string.Empty;
        return $"*[System[({events}){time}]]";
    }

    private static EventLogReader? OpenOrNull(EventLogQuery query)
    {
        try
        {
            return new EventLogReader(query);
        }
        catch (EventLogNotFoundException)
        {
            return null;
        }
    }
}
