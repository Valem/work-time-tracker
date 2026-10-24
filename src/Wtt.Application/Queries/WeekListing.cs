using Wtt.Application.Ports;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;

namespace Wtt.Application.Queries;

/// <summary>Every week from the one holding the oldest kept fact to the current one.</summary>
public sealed class WeekListing(IFactJournal journal, IWorkTimeQueries queries, TimeProvider clock)
{
    public async Task<IReadOnlyList<WorkWeek>> AllWeeksAsync()
    {
        var oldest = await journal.EarliestAsync();
        if (oldest is null)
        {
            return [];
        }

        var today = clock.GetLocalNow().LocalDate();
        var weeks = new List<WorkWeek>();
        for (var monday = WorkWeek.MondayOf(oldest.Value.LocalDate()); monday <= today; monday = monday.AddDays(7))
        {
            weeks.Add(await queries.GetWeekAsync(monday));
        }

        return weeks;
    }
}
