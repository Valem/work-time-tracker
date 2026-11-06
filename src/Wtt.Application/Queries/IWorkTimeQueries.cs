using Wtt.Domain.Workdays;

namespace Wtt.Application.Queries;

public sealed record RecordingStatus(bool IsRecording, DateTimeOffset? LastRecorded);

public interface IWorkTimeQueries
{
    Task<WorkWeek> GetWeekAsync(DateOnly monday);

    Task<Workday> GetDayAsync(DateOnly date);

    Task<RecordingStatus> GetRecordingStatusAsync();
}
