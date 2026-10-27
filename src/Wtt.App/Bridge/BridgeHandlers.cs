using Wtt.App.Data;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;
using static Wtt.App.Bridge.BridgeValues;

namespace Wtt.App.Bridge;

/// <summary>
/// Answers each message type from the application's queries. The host supplies the navigation and the calendar — the
/// current week, previous and next Mondays, ISO weeks — so the UI never steps a date or reads a clock.
/// </summary>
internal sealed class BridgeHandlers(Task<AppServices> services, Func<bool> recorderRunning, TimeProvider clock)
{
    public async Task<object> AnswerAsync(object request) => request switch
    {
        WeekRequest week => await WeekAsync(week),
        DayRequest day => await DayAsync(day),
        StatusRequest => await StatusAsync(),
        LunchRuleRequest => await LunchRuleAsync(),
        LunchRuleInput input => await SaveLunchRuleAsync(input),
        _ => throw new BridgeRefusalException(RefusalCode.UnknownType),
    };

    private async Task<WeekView> WeekAsync(WeekRequest request)
    {
        var current = WorkWeek.MondayOf(Today());
        var monday = request.Monday is null ? current : Monday(request.Monday);
        var app = await services;
        var week = await app.Queries.GetWeekAsync(monday);
        var oldest = await app.Journal.EarliestAsync() is { } earliest ? WorkWeek.MondayOf(earliest.LocalDate()) : current;
        return new WeekView(
            WeekOf(monday),
            monday > oldest ? Day(monday.AddDays(-7)) : null,
            monday < current ? Day(monday.AddDays(7)) : null,
            week.Days.Select(Views.Row).ToList(),
            Minutes(week.Total));
    }

    private async Task<DayView> DayAsync(DayRequest request)
    {
        var date = Date(request.Date);
        var app = await services;
        return Views.Of(await app.Queries.GetDayAsync(date));
    }

    private async Task<StatusView> StatusAsync()
    {
        var app = await services;
        var status = await app.Queries.GetRecordingStatusAsync();
        var lastRecorded = status.LastRecorded is { } at ? Reading(TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone)) : null;
        return new StatusView(status.IsRecording && recorderRunning(), lastRecorded);
    }

    private async Task<LunchRuleView> LunchRuleAsync()
    {
        var app = await services;
        return Views.Of(await app.Rules.InForceOnAsync(Today()));
    }

    private async Task<LunchRuleView> SaveLunchRuleAsync(LunchRuleInput input)
    {
        var (window, tiers) = LunchRuleInputs.Read(input);
        var app = await services;
        LunchRule saved;
        try
        {
            saved = await app.Rules.SaveAsync(window, tiers);
        }
        catch (ArgumentException refused)
        {
            throw new BridgeRefusalException(RefusalCode.InvalidRule, refused);
        }

        return Views.Of(saved);
    }

    /// <summary>Today is the laptop's date now; every other date comes from the facts' own offsets.</summary>
    private DateOnly Today() => clock.GetLocalNow().LocalDate();
}
