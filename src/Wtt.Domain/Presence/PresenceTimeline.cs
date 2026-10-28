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

/// <summary>The whole gap between two use spans, with the wakes inside it that Windows did alone.</summary>
public sealed record Pause(DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<DateTimeOffset> IgnoredWakes)
{
    public TimeSpan Duration => End - Start;
}

/// <param name="WindowsWakes">Every wake Windows did alone, which produced no use, whether inside a pause or not.</param>
public sealed record PresenceTimeline(IReadOnlyList<UseSpan> Uses, IReadOnlyList<Pause> Pauses, IReadOnlyList<DateTimeOffset> WindowsWakes)
{
    public static readonly PresenceTimeline Empty = new([], [], []);
}
