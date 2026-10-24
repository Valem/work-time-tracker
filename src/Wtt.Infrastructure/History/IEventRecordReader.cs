namespace Wtt.Infrastructure.History;

internal interface IEventRecordReader
{
    /// <summary>The XML of each matching record created at or after <paramref name="since"/>, oldest first.</summary>
    IEnumerable<string> Read(string channel, IReadOnlyList<EventSelector> selectors, DateTimeOffset? since);
}
