using Wtt.Domain.Presence;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;

namespace Wtt.Domain.Workdays;

public sealed class WorkdayCalculator : IWorkdayCalculator
{
    /// <summary>Use resuming within this time after the previous day's last use carries work over midnight.</summary>
    public static readonly TimeSpan CarryOverLimit = TimeSpan.FromMinutes(60);

    /// <summary>A last pause at least this long ends the day where it starts; use after it adds only its own minutes.</summary>
    public static readonly TimeSpan EveningPauseMinimum = TimeSpan.FromHours(2);

    public Workday Calculate(DateOnly date, PresenceTimeline presence, LunchRule rule)
    {
        var pieces = DayPiece.SplitAtMidnight(presence.Uses);
        var today = pieces.Where(piece => piece.Date == date).ToList();
        var windowsWakes = presence.WindowsWakes.Where(wake => wake.LocalDate() == date).ToList();
        if (today.Count == 0)
        {
            return Workday.WithoutUse(date, windowsWakes);
        }

        var previous = pieces.LastOrDefault(piece => piece.Date < date);
        var next = pieces.FirstOrDefault(piece => piece.Date > date);
        var carriedIn = previous is not null && today[0].Start - previous.End < CarryOverLimit;
        var carriedOut = next is not null && next.Start - today[^1].End < CarryOverLimit;

        var start = carriedIn ? Midnight(date, today[0].Start.Offset) : today[0].Start;
        var lastUseEnd = carriedOut ? Midnight(date.AddDays(1), next!.Start.Offset) : today[^1].End;

        var pauses = PausesBetween(today, presence.Pauses);
        var nightPause = carriedIn ? NightPause(date, pauses, rule) : null;
        var lunchPause = LunchPause(date, pauses.Where(pause => !ReferenceEquals(pause, nightPause)), rule);
        var eveningPause = EveningPause(date, pauses, nightPause, lunchPause, rule);

        var end = eveningPause?.Start ?? lastUseEnd;
        var eveningUse = eveningPause is null ? TimeSpan.Zero : lastUseEnd - eveningPause.End;
        var attendance = end - start - (nightPause?.Duration ?? TimeSpan.Zero) + eveningUse;
        var lunch = Deduction(rule, attendance, lunchPause);
        var precision = today.Any(piece => piece.Precision == Precision.Rebuilt) ? Precision.Rebuilt : Precision.Exact;

        return new Workday(
            date,
            Blocks(start, end, nightPause),
            pauses,
            nightPause,
            lunch,
            attendance,
            attendance - lunch.Deducted,
            precision,
            windowsWakes,
            eveningPause,
            eveningUse);
    }

    private static DateTimeOffset Midnight(DateOnly date, TimeSpan offset) =>
        new(date.ToDateTime(TimeOnly.MinValue), offset);

    private static List<Pause> PausesBetween(IReadOnlyList<DayPiece> pieces, IReadOnlyList<Pause> presencePauses)
    {
        var ignoredWakes = presencePauses.SelectMany(pause => pause.IgnoredWakes).ToList();
        var pauses = new List<Pause>();
        for (var i = 1; i < pieces.Count; i++)
        {
            var start = pieces[i - 1].End;
            var end = pieces[i].Start;
            if (end > start)
            {
                pauses.Add(new Pause(start, end, ignoredWakes.Where(wake => wake >= start && wake <= end).ToList()));
            }
        }

        return pauses;
    }

    /// <summary>
    /// The longest pause after midnight that ends before the lunch window opens; only when there is
    /// none, the longest that starts before it (the night running into the window).
    /// </summary>
    private static Pause? NightPause(DateOnly date, IReadOnlyList<Pause> pauses, LunchRule rule)
    {
        var windowOpens = date.ToDateTime(rule.Window.Start);
        return Longest(pauses.Where(pause => pause.End.DateTime < windowOpens))
            ?? Longest(pauses.Where(pause => pause.Start.DateTime < windowOpens));
    }

    /// <summary>The longest pause touching the lunch window.</summary>
    private static Pause? LunchPause(DateOnly date, IEnumerable<Pause> pauses, LunchRule rule)
    {
        var windowOpens = date.ToDateTime(rule.Window.Start);
        var windowCloses = date.ToDateTime(rule.Window.End);
        return Longest(pauses.Where(pause => pause.Start.DateTime <= windowCloses && pause.End.DateTime >= windowOpens));
    }

    /// <summary>
    /// The day's last pause, when it is long enough and neither the lunch nor the night pause, which are
    /// chosen first. A pause ending before the lunch window opens is a morning pause, never an evening one.
    /// </summary>
    private static Pause? EveningPause(DateOnly date, IReadOnlyList<Pause> pauses, Pause? nightPause, Pause? lunchPause, LunchRule rule)
    {
        var last = pauses.LastOrDefault();
        var windowOpens = date.ToDateTime(rule.Window.Start);
        return last is not null
            && last.Duration >= EveningPauseMinimum
            && last.End.DateTime >= windowOpens
            && !ReferenceEquals(last, nightPause)
            && !ReferenceEquals(last, lunchPause)
                ? last
                : null;
    }

    private static Pause? Longest(IEnumerable<Pause> pauses) =>
        pauses.Aggregate((Pause?)null, (longest, pause) =>
            longest is null || pause.Duration > longest.Duration ? pause : longest);

    private static LunchDeduction Deduction(LunchRule rule, TimeSpan attendance, Pause? lunchPause)
    {
        var pauseLength = lunchPause?.Duration ?? TimeSpan.Zero;
        var deducted = rule.BreakFor(attendance, pauseLength);
        var basis = deducted == TimeSpan.Zero ? LunchBasis.None
            : lunchPause is not null && deducted == pauseLength ? LunchBasis.PauseWhole
            : LunchBasis.TierMinimum;
        return new LunchDeduction(lunchPause, deducted, basis);
    }

    private static IReadOnlyList<WorkBlock> Blocks(DateTimeOffset start, DateTimeOffset end, Pause? nightPause) =>
        nightPause is null
            ? [new WorkBlock(start, end)]
            : [new WorkBlock(start, nightPause.Start), new WorkBlock(nightPause.End, end)];
}
