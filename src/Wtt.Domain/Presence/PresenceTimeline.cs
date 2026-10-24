namespace Wtt.Domain.Presence;

public enum Precision
{
    Exact,
    Rebuilt,
}

/// <summary>A stretch during which the person used the laptop.</summary>
public sealed record UseSpan(DateTimeOffset Start, DateTimeOffset End, Precision Precision)
{
    public TimeSpan Duration => End - Start;
}

/// <summary>The whole gap between two use spans, with the Windows wakes inside it that did not count.</summary>
public sealed record Pause(DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<DateTimeOffset> IgnoredWakes)
{
    public TimeSpan Duration => End - Start;
}

public sealed record PresenceTimeline(IReadOnlyList<UseSpan> Uses, IReadOnlyList<Pause> Pauses)
{
    public static readonly PresenceTimeline Empty = new([], []);
}
