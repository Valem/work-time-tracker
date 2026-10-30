using Wtt.Application.Ports;
using Wtt.Domain.Planning;
using static Wtt.PublicHolidays.OpenHolidays;

namespace Wtt.PublicHolidays;

/// <summary>
/// An OpenHolidays answer saved in a file, read as if the source had just sent it for the canton and year asked for: the
/// same size cap and the same checks, so a synthetic answer proves what the live one would meet.
/// </summary>
public sealed class PublicHolidayFile(string path, TimeProvider clock) : IPublicHolidaySource
{
    public HolidaySource Source => OpenHolidays.Source;

    public async Task<PublicHolidayYear> ReadAsync(Canton canton, int year, CancellationToken cancel)
    {
        byte[] answer;
        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaxAnswerBytes)
            {
                throw Refused($"The answer is larger than {MaxAnswerBytes / 1024} KiB.");
            }

            answer = new byte[file.Length];
            await file.ReadExactlyAsync(answer, cancel);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            throw new PublicHolidayReadException(LoadFailure.Unreachable, $"The file could not be read: {failure.Message}", failure);
        }

        return OpenHolidaysAnswer.Translate(answer, canton, year, clock.GetUtcNow());
    }
}
