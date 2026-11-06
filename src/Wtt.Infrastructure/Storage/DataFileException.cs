namespace Wtt.Infrastructure.Storage;

/// <summary>The data file could not be opened: locked, damaged, unreachable, or written by a newer program.</summary>
public sealed class DataFileException(string path, Exception inner)
    : Exception($"The data file '{path}' could not be opened: {inner.Message}", inner)
{
    public string Path { get; } = path;
}
