using Wtt.Domain.Facts;

namespace Wtt.Application.Ports;

/// <summary>The Windows account whose sessions count.</summary>
public sealed record UserSid(string Value);

/// <summary>
/// Where the next history read starts: a time (read again with an overlap, so a cleared log or
/// late records lose nothing) and whether this user's sign-in session was open at that time.
/// </summary>
public sealed record HistoryCursor(DateTimeOffset? ReadFrom, string? OpenSession)
{
    public static readonly HistoryCursor Beginning = new(null, null);
}

public sealed record HistoryBatch(IReadOnlyList<ActivityFact> Facts, HistoryCursor Next);

/// <summary>What Windows still keeps about past use: the facts of this user's own sessions.</summary>
public interface IHistorySource
{
    Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user);
}

public interface IHistoryCursorStore
{
    Task<HistoryCursor> LoadAsync();

    Task SaveAsync(HistoryCursor cursor);
}
