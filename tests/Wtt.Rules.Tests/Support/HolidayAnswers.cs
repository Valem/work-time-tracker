using Wtt.Domain.Planning;
using Wtt.PublicHolidays;

namespace Wtt.Rules.Tests.Support;

/// <summary>Synthetic answers in OpenHolidays' shape, read through the product's own translation.</summary>
internal static class HolidayAnswers
{
    /// <summary>Solothurn's shape on Friday 01.05.2026: a half day for most districts and a whole day for one place, both part-canton.</summary>
    public const string Solothurn2026 = """
        [
          {"id":"1","startDate":"2026-01-01","endDate":"2026-01-01","type":"Public","name":[{"language":"DE","text":"Neujahrstag"}],
           "temporalScope":"FullDay","nationwide":true},
          {"id":"2","startDate":"2026-05-01","endDate":"2026-05-01","type":"Public","name":[{"language":"DE","text":"Tag der Arbeit"}],
           "temporalScope":"HalfDay","nationwide":false,"subdivisions":[{"code":"CH-SO-BE"},{"code":"CH-SO-LE"}]},
          {"id":"3","startDate":"2026-05-01","endDate":"2026-05-01","type":"Public","name":[{"language":"DE","text":"Tag der Arbeit"}],
           "temporalScope":"FullDay","nationwide":false,"subdivisions":[{"code":"CH-SO-TH-AD"}]}
        ]
        """;

    /// <summary>Reads the answer through the OpenHolidays translation, as a saved answer replayed by wtt-cli import-holidays.</summary>
    public static async Task<PublicHolidayYear> ReadAsync(string answer, Canton canton, int year)
    {
        var folder = Path.Combine(Path.GetTempPath(), "wtt-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "answer.json");
            await File.WriteAllTextAsync(path, answer);
            return await new PublicHolidayFile(path, TimeProvider.System).ReadAsync(canton, year, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
