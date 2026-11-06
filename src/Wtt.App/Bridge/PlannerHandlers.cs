using Wtt.App.Data;
using Wtt.Domain.Planning;
using Wtt.Domain.Time;
using static Wtt.App.Bridge.BridgeValues;

namespace Wtt.App.Bridge;

/// <summary>
/// Answers the year planner's messages from the holiday planner; the host keeps nothing between them. Showing a year and
/// saving a canton for one are needs for that year's public holidays, passed on before the answer, so a load they start
/// already shows in it.
/// </summary>
internal sealed class PlannerHandlers(Task<AppServices> services, TimeProvider clock)
{
    public async Task<YearView> YearAsync(YearRequest request)
    {
        var year = Year(request.Year ?? clock.GetLocalNow().LocalDate().Year);
        var app = await services;
        await app.Needs.NeedAsync(PlanSettings.StartOf(year), new DateOnly(year, 12, 31));
        return PlanViews.Year(await app.Planner.GetYearAsync(year), clock.LocalTimeZone);
    }

    public async Task<PreviewView> PreviewAsync(DragRequest request)
    {
        var (anchor, to) = (Date(request.Anchor), Date(request.To));
        var app = await services;
        return PlanViews.Preview(await app.Planner.PreviewAsync(request.Year, anchor, to), anchor, to);
    }

    public async Task<EditView> DragAsync(DragRequest request)
    {
        var (anchor, to) = (Date(request.Anchor), Date(request.To));
        var app = await services;
        return PlanViews.Edit(await app.Planner.DragAsync(request.Year, anchor, to), clock.LocalTimeZone);
    }

    public async Task<EditView> ClickAsync(ClickRequest request)
    {
        var date = Date(request.Date);
        var app = await services;
        return PlanViews.Edit(await app.Planner.ClickAsync(request.Year, date), clock.LocalTimeZone);
    }

    public async Task<EditView> TickAsync(TickRequest request)
    {
        var date = Date(request.Date);
        var share = request.Share == Part.Half ? DayPart.Half : DayPart.Whole;
        var app = await services;
        return PlanViews.Edit(await app.Planner.TickAsync(request.Year, date, share, request.Ticked), clock.LocalTimeZone);
    }

    public async Task<YearView> RestoreAsync(RestoreRequest request)
    {
        var reverse = PlanViews.Change(request);
        var app = await services;
        return PlanViews.Year(await app.Planner.RestoreAsync(request.Year, reverse), clock.LocalTimeZone);
    }

    public async Task<PlannerSettingsView> SettingsAsync(PlannerSettingsRequest request)
    {
        var app = await services;
        return PlanViews.Settings(await app.Planner.GetSettingsAsync(request.Year), clock.LocalTimeZone);
    }

    /// <summary>Saves «Mein Jahr», then needs the saved canton's public holidays of the year; the answer shows that load.</summary>
    public async Task<PlannerSettingsView> SaveSettingsAsync(PlannerSettingsInput input)
    {
        var app = await services;
        await app.Planner.SaveSettingsAsync(input.Year, PlanViews.Input(input));
        var start = PlanSettings.StartOf(input.Year);
        await app.Needs.NeedAsync(start, start);
        return PlanViews.Settings(await app.Planner.GetSettingsAsync(input.Year), clock.LocalTimeZone);
    }

    /// <summary>Answers once the reload's one request has ended — or the one already running for that canton and year.</summary>
    public async Task<HolidayLoadView> ReloadAsync(ReloadRequest request)
    {
        var app = await services;
        return PlanViews.Load(await app.Planner.ReloadAsync(request.Year), clock.LocalTimeZone);
    }

    public async Task<LayoutView> SaveLayoutAsync(LayoutInput input)
    {
        var app = await services;
        await app.Plans.SaveLayoutAsync(PlanViews.LayoutOf(input.Layout));
        return new LayoutView(PlanViews.LayoutOf(await app.Plans.LayoutAsync()));
    }
}
