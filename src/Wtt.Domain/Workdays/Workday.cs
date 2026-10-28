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
/// An evening pause ends the day where it starts: the use after it is no block and adds only
/// <see cref="EveningUse"/> to the attendance. <see cref="WindowsWakes"/> are the wakes Windows did alone on this
/// local date, before, inside or after the day's work.
/// </summary>
public sealed record Workday(
    DateOnly Date,
    IReadOnlyList<WorkBlock> Blocks,
    IReadOnlyList<Pause> Pauses,
    Pause? NightPause,
    LunchDeduction Lunch,
    TimeSpan Attendance,
    TimeSpan Total,
    Precision Precision,
    IReadOnlyList<DateTimeOffset> WindowsWakes,
    Pause? EveningPause = null,
    TimeSpan EveningUse = default)
{
    public static Workday WithoutUse(DateOnly date, IReadOnlyList<DateTimeOffset> windowsWakes) =>
        new(date, [], [], null, LunchDeduction.None, TimeSpan.Zero, TimeSpan.Zero, Precision.Exact, windowsWakes);

    public bool HasUse => Blocks.Count > 0;

    public DateTimeOffset? Start => HasUse ? Blocks[0].Start : null;

    /// <summary>
    /// The day's end: where the evening pause starts, else the end of the last use — the next midnight
    /// (24:00) when work carried over into the following day.
    /// </summary>
    public DateTimeOffset? End => HasUse ? Blocks[^1].End : null;

    public TimeSpan Break => Lunch.Deducted;

    public bool IsRebuilt => Precision == Precision.Rebuilt;

    /// <summary>
    /// The blocks without the pauses inside them — the lunch pause and the pauses counted as work — in time order: the
    /// stretches between start and end that no pause covers.
    /// </summary>
    public IReadOnlyList<WorkBlock> WorkSpans => [.. Blocks.SelectMany(block => Uncovered(block, Pauses))];

    private static List<WorkBlock> Uncovered(WorkBlock block, IReadOnlyList<Pause> pauses)
    {
        var spans = new List<WorkBlock>();
        var from = block.Start;
        foreach (var pause in pauses.Where(pause => pause.Start < block.End && pause.End > block.Start))
        {
            if (pause.Start > from)
            {
                spans.Add(new WorkBlock(from, pause.Start));
            }

            if (pause.End > from)
            {
                from = pause.End;
            }
        }

        if (block.End > from)
        {
            spans.Add(new WorkBlock(from, block.End));
        }

        return spans;
    }
}
