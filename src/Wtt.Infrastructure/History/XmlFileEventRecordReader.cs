using System.Xml;
using System.Xml.Linq;

namespace Wtt.Infrastructure.History;

/// <summary>
/// Serves the records of a file of Windows event XML — each record as <c>EventRecord.ToXml()</c> renders it, under one
/// root element or none — the way the Windows reader serves the live logs: by channel, selector and time.
/// </summary>
internal sealed class XmlFileEventRecordReader : IEventRecordReader
{
    private readonly IReadOnlyList<(RawEvent Record, string Xml)> records;

    private XmlFileEventRecordReader(IReadOnlyList<(RawEvent Record, string Xml)> records)
    {
        this.records = records;
    }

    /// <summary>Reads and checks every record up front; a DTD is refused and nothing outside the file is resolved.</summary>
    /// <exception cref="EventFileException">The file cannot be read or is not event XML.</exception>
    public static XmlFileEventRecordReader Load(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ConformanceLevel = ConformanceLevel.Fragment,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, settings);
            var records = new List<(RawEvent Record, string Xml)>();
            reader.MoveToContent();
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Event")
                {
                    var xml = XNode.ReadFrom(reader).ToString(SaveOptions.DisableFormatting);
                    records.Add((RawEvent.Parse(xml), xml));
                }
                else
                {
                    reader.Read();
                }
            }

            return records.Count > 0
                ? new XmlFileEventRecordReader(records)
                : throw new InvalidDataException("It holds no Windows event records.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or InvalidDataException or FormatException or OverflowException)
        {
            throw new EventFileException(path, exception);
        }
    }

    public IEnumerable<string> Read(string channel, IReadOnlyList<EventSelector> selectors, DateTimeOffset? since) =>
        records
            .Where(entry => entry.Record.Channel == channel
                && selectors.Any(selector => selector.Provider == entry.Record.Provider && selector.EventIds.Contains(entry.Record.EventId))
                && (since is null || entry.Record.TimeCreated >= since))
            .Select(entry => entry.Xml);
}
