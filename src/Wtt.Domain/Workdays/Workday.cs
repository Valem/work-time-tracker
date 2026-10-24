using Wtt.Domain.Presence;

namespace Wtt.Domain.Workdays;

public sealed record WorkBlock(DateTimeOffset Start, DateTimeOffset End);

public enum LunchBasis
{
    /// <summary>No break was deducted.</summary>
    None,

    /// <summary>The lunch pause was deducted whole.</summary>
    PauseWhole,

    /// <summary>The tier minimum was larger than the lunch pause (or there was none).</summary>
    TierMinimum,
}

public sealed record LunchDeduction(Pause? Pause, TimeSpan Deducted, LunchBasis Basis)
{
    public static readonly LunchDeduction None = new(null, TimeSpan.Zero, LunchBasis.None);
}

/// <summary>
/// One local date's work, derived from presence and the rule in force — never stored.
/// Blocks run from start to end and split only at the night pause; the lunch deduction lies inside them.
/// </summary>
public sealed record Workday(
    DateOnly Date,
    IReadOnlyList<WorkBlock> Blocks,
    IReadOnlyList<Pause> Pauses,
    Pause? NightPause,
    LunchDeduction Lunch,
    TimeSpan Attendance,
    TimeSpan Total,
    Precision Precision)
{
    public static Workday WithoutUse(DateOnly date) =>
        new(date, [], [], null, LunchDeduction.None, TimeSpan.Zero, TimeSpan.Zero, Precision.Exact);

    public bool HasUse => Blocks.Count > 0;

    public DateTimeOffset? Start => HasUse ? Blocks[0].Start : null;

    /// <summary>The day's end; the next midnight (24:00) when work carried over into the following day.</summary>
    public DateTimeOffset? End => HasUse ? Blocks[^1].End : null;

    public TimeSpan Break => Lunch.Deducted;

    public bool IsRebuilt => Precision == Precision.Rebuilt;
}
