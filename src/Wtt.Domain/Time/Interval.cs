namespace Wtt.Domain.Time;

/// <summary>A half-open span of instants [Start, End).</summary>
public readonly record struct Interval(DateTimeOffset Start, DateTimeOffset End)
{
    public static readonly Interval All = new(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;
}
