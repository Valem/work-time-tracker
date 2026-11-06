using Wtt.Application.Ports;
using Wtt.Application.PublicHolidays;
using Wtt.Domain.Planning;

namespace Wtt.App.Data;

/// <summary>
/// When the App host needs a canton's public holidays: for the dates a screen shows, the year «Mein Jahr» saved a canton
/// for, and today at start. A need is passed on only while the canton-year is not loaded, so a stored year never shows
/// as loading; the loads decide whether a request goes — never for a year outside last to next, while one runs, or
/// within an hour of a failure in this run.
/// </summary>
internal sealed class PublicHolidayNeeds(IPlanStore plans, IPublicHolidayLoads loads)
{
    /// <summary>Needs each canton-year in force somewhere from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public async Task NeedAsync(DateOnly from, DateOnly to)
    {
        var settings = new SettingsBook((await plans.ReadAsync(from, from)).Settings);
        foreach (var (canton, year) in settings.CantonYears(from, to))
        {
            if ((await loads.StateOfAsync(canton, year)).Status == LoadStatus.NotLoaded)
            {
                loads.Need(canton, year);
            }
        }
    }
}
