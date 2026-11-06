namespace Wtt.Domain.Time;

/// <summary>A span of local clock time within one day, such as the lunch window.</summary>
public sealed record TimeRange
{
    public TimeRange(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentException($"The range must end after it starts ({start:HH\\:mm}–{end:HH\\:mm}).");
        }

        Start = start;
        End = end;
    }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }
}
