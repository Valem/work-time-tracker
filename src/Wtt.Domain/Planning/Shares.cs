namespace Wtt.Domain.Planning;

/// <summary>How much of a date is work, in half days.</summary>
public enum WorkShare
{
    None = 0,
    Half = 1,
    Whole = 2,
}

/// <summary>A whole or a half day: what a booking holds and what a public holiday takes.</summary>
public enum DayPart
{
    Half = 1,
    Whole = 2,
}

public static class Shares
{
    public static HalfDays Amount(this WorkShare share) => new((int)share);

    public static HalfDays Amount(this DayPart part) => new((int)part);

    /// <summary>The share left of <paramref name="share"/> once <paramref name="taken"/> is off it, never below none.</summary>
    public static WorkShare Less(this WorkShare share, WorkShare taken) => (WorkShare)Math.Max(0, (int)share - (int)taken);

    public static WorkShare AsShare(this DayPart part) => (WorkShare)(int)part;
}
