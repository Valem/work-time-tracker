namespace Wtt.Infrastructure.History;

/// <summary>A Windows event log could not be read or understood; nothing of that read was imported.</summary>
public sealed class HistoryReadException(string channel, Exception inner)
    : Exception($"The Windows log '{channel}' could not be read: {inner.Message}", inner)
{
    public string Channel { get; } = channel;
}
