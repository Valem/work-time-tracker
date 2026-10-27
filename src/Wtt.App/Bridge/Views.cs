using Wtt.Domain.Presence;
using Wtt.Domain.Rules;
using Wtt.Domain.Workdays;
using static Wtt.App.Bridge.BridgeValues;

namespace Wtt.App.Bridge;

/// <summary>What the domain derived, as the bridge carries it: every number is the domain's, only re-expressed in minutes.</summary>
internal static class Views
{
    public static DayRow Row(Workday day) => new(
        Day(day.Date),
        day.Start is { } start ? MinutesOn(start, day.Date) : null,
        day.End is { } end ? MinutesOn(end, day.Date) : null,
        Minutes(day.Break),
        Minutes(day.Total),
        day.IsRebuilt);

    public static DayView Of(Workday day) => new(
        Day(day.Date),
        Day(WorkWeek.MondayOf(day.Date)),
        day.Start is { } start ? MinutesOn(start, day.Date) : null,
        day.End is { } end ? MinutesOn(end, day.Date) : null,
        day.Blocks.Select(block => Span(block.Start, block.End, day.Date)).ToList(),
        day.Pauses.Select(pause => Pause(pause, RoleOf(pause, day), day.Date)).ToList(),
        SpanOf(day.NightPause, day.Date),
        new LunchView(SpanOf(day.Lunch.Pause, day.Date), Minutes(day.Lunch.Deducted), BasisOf(day.Lunch.Basis)),
        SpanOf(day.EveningPause, day.Date),
        EveningUse(day),
        Minutes(day.Break),
        Minutes(day.Total),
        day.IsRebuilt);

    public static LunchRuleView Of(LunchRule rule)
    {
        var prefilled = ReferenceEquals(rule, LunchRule.Prefilled);
        return new LunchRuleView(
            new LunchWindow(Minutes(rule.Window.Start.ToTimeSpan()), Minutes(rule.Window.End.ToTimeSpan())),
            rule.Tiers.Select(tier => new LunchTier(Minutes(tier.Over), Minutes(tier.Minimum))).ToList(),
            prefilled ? null : Day(rule.ValidFrom),
            prefilled);
    }

    /// <summary>The use after the evening pause: from the pause's end, as many minutes as the domain counted.</summary>
    private static DaySpan? EveningUse(Workday day) =>
        day.EveningPause is { } evening && day.EveningUse > TimeSpan.Zero
            ? Span(evening.End, evening.End + day.EveningUse, day.Date)
            : null;

    private static PauseView Pause(Pause pause, PauseRole role, DateOnly date) => new(
        MinutesOn(pause.Start, date),
        MinutesOn(pause.End, date),
        Minutes(pause.Duration),
        role,
        pause.IgnoredWakes.Select(wake => MinutesOn(wake, date)).ToList());

    private static PauseRole RoleOf(Pause pause, Workday day) =>
        ReferenceEquals(pause, day.NightPause) ? PauseRole.Night
        : ReferenceEquals(pause, day.Lunch.Pause) ? PauseRole.Lunch
        : ReferenceEquals(pause, day.EveningPause) ? PauseRole.Evening
        : PauseRole.Work;

    private static DaySpan? SpanOf(Pause? pause, DateOnly date) => pause is null ? null : Span(pause.Start, pause.End, date);

    private static DeductionBasis BasisOf(LunchBasis basis) => basis switch
    {
        LunchBasis.None => DeductionBasis.None,
        LunchBasis.PauseWhole => DeductionBasis.PauseWhole,
        LunchBasis.TierMinimum => DeductionBasis.TierMinimum,
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Unknown lunch basis."),
    };
}
