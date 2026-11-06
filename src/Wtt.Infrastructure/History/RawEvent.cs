using System.Globalization;
using System.Xml.Linq;

namespace Wtt.Infrastructure.History;

/// <summary>One event-log record, as far as the history import needs it.</summary>
internal sealed record RawEvent(
    string Channel,
    long RecordId,
    string Provider,
    int EventId,
    DateTimeOffset TimeCreated,
    string? UserId,
    IReadOnlyDictionary<string, string> Data,
    string? Binary = null)
{
    public string? this[string name] => Data.GetValueOrDefault(name);

    /// <summary>Reads the XML Windows renders for a record (<c>EventRecord.ToXml()</c>).</summary>
    public static RawEvent Parse(string xml)
    {
        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        var root = XElement.Parse(xml);
        var system = root.Element(ns + "System") ?? throw new InvalidDataException("Event without System element.");

        var data = root.Element(ns + "EventData")?.Elements(ns + "Data")
            .Where(element => element.Attribute("Name") is not null)
            .GroupBy(element => element.Attribute("Name")!.Value)
            .ToDictionary(group => group.Key, group => group.First().Value)
            ?? [];

        return new RawEvent(
            Required(system, ns + "Channel").Value,
            long.Parse(Required(system, ns + "EventRecordID").Value, CultureInfo.InvariantCulture),
            Required(system, ns + "Provider").Attribute("Name")?.Value ?? string.Empty,
            int.Parse(Required(system, ns + "EventID").Value, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(
                Required(system, ns + "TimeCreated").Attribute("SystemTime")?.Value ?? throw new InvalidDataException("Event without time."),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            system.Element(ns + "Security")?.Attribute("UserID")?.Value,
            data,
            root.Element(ns + "EventData")?.Element(ns + "Binary")?.Value);
    }

    private static XElement Required(XElement parent, XName name) =>
        parent.Element(name) ?? throw new InvalidDataException($"Event without {name.LocalName}.");
}
