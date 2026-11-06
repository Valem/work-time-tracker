using System.Diagnostics.Eventing.Reader;
using System.Xml;
using Wtt.Application.Ports;

namespace Wtt.Infrastructure.History;

/// <summary>
/// History from the Winlogon/Operational and System event logs (ADR-003). Both logs are read in full
/// before anything is returned: they share one cursor, so a failure in either imports nothing.
/// </summary>
public sealed class EventLogHistorySource : IHistorySource
{
    private readonly IEventRecordReader reader;
    private readonly HistoryTranslator translator;

    public EventLogHistorySource(TimeZoneInfo zone)
        : this(new WindowsEventRecordReader(), zone)
    {
    }

    /// <summary>
    /// History from a file of Windows event XML instead of the live logs, imported exactly as the live logs are: for
    /// replaying synthetic history through the product's own importer (developer CLI). The file is read and checked here.
    /// </summary>
    /// <exception cref="EventFileException">The file cannot be read or is not event XML.</exception>
    public static EventLogHistorySource FromXmlFile(string path, TimeZoneInfo zone) =>
        new(XmlFileEventRecordReader.Load(path), zone);

    internal EventLogHistorySource(IEventRecordReader reader, TimeZoneInfo zone)
    {
        this.reader = reader;
        translator = new HistoryTranslator(zone);
    }

    public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user)
    {
        var events = HistoryChannels.All
            .SelectMany(channel => ReadChannel(channel, cursor.ReadFrom))
            .ToList();
        return Task.FromResult(translator.Translate(events, cursor, user));
    }

    private List<RawEvent> ReadChannel(HistoryChannel channel, DateTimeOffset? since)
    {
        try
        {
            return reader.Read(channel.Name, channel.Selectors, since).Select(RawEvent.Parse).ToList();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or EventLogException or InvalidDataException or XmlException or FormatException)
        {
            throw new HistoryReadException(channel.Name, exception);
        }
    }
}
