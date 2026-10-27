namespace Wtt.Infrastructure.History;

/// <summary>A file of Windows event XML could not be read or holds something other than event records.</summary>
public sealed class EventFileException(string path, Exception inner)
    : Exception($"The event file '{path}' could not be read: {inner.Message}", inner)
{
    public string Path { get; } = path;
}
