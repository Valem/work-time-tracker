using Wtt.Application.Ports;
using Wtt.Domain.Planning;

namespace Wtt.Application.PublicHolidays;

/// <summary>The outcome of one load: the year stored, or the failure and its reason with what is stored kept.</summary>
public sealed record PublicHolidayLoad(PublicHolidayYear? Year, LoadFailure? Failure, string? Reason)
{
    public LoadState State => Year is { } year ? LoadState.Loaded(year.LoadedAt) : LoadState.NotLoaded(Failure);
}

/// <summary>
/// Loads one canton's public holidays of a year from a source: an answer taken whole replaces what was stored; a failure
/// keeps it and writes its reason — never the answer — to the error log.
/// </summary>
public sealed class PublicHolidayLoader(IPublicHolidaySource source, IPublicHolidayStore store, IErrorLog errors)
{
    public HolidaySource Source => source.Source;

    public async Task<PublicHolidayLoad> LoadAsync(Canton canton, int year, CancellationToken cancel)
    {
        try
        {
            var loaded = await source.ReadAsync(canton, year, cancel);
            await store.ReplaceAsync(loaded);
            return new PublicHolidayLoad(loaded, null, null);
        }
        catch (PublicHolidayReadException failure)
        {
            errors.Write($"Loading the public holidays of {canton} {year} from {source.Source.Name}", failure);
            return new PublicHolidayLoad(null, failure.Reason, failure.Message);
        }
    }
}
