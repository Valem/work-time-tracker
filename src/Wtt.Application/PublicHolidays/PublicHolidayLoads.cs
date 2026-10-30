using Wtt.Application.Ports;
using Wtt.Domain.Planning;
using Wtt.Domain.Time;

namespace Wtt.Application.PublicHolidays;

/// <summary>When a canton's public holidays of a year are requested, and where each request stands.</summary>
public interface IPublicHolidayLoads
{
    HolidaySource Source { get; }

    Task<LoadState> StateOfAsync(Canton canton, int year);

    /// <summary>
    /// Loads in the background unless the year is stored, loading, outside last, this and next year, or failed in this run
    /// less than <see cref="PublicHolidayLoads.RetryAfter"/> ago.
    /// </summary>
    void Need(Canton canton, int year);

    /// <summary>One request, whatever is stored; none while that canton and year is loading — it waits for that one instead.</summary>
    Task<LoadState> ReloadAsync(Canton canton, int year);
}

/// <summary>
/// The public-holiday loads of one running program. Requests go only for last, this and next year; a failure is
/// remembered for this run only, so the next start may try again at once.
/// </summary>
public sealed class PublicHolidayLoads(PublicHolidayLoader loader, IPublicHolidayStore store, IErrorLog errors, TimeProvider clock, CancellationToken stopping)
    : IPublicHolidayLoads
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    private readonly Lock gate = new();
    private readonly Dictionary<(Canton, int), Task<LoadState>> running = [];
    private readonly Dictionary<(Canton, int), (LoadFailure Reason, DateTimeOffset At)> failures = [];

    public HolidaySource Source => loader.Source;

    public async Task<LoadState> StateOfAsync(Canton canton, int year)
    {
        lock (gate)
        {
            if (running.ContainsKey((canton, year)))
            {
                return LoadState.Loading;
            }
        }

        if (await store.FindAsync(canton, year) is { } stored)
        {
            return LoadState.Loaded(stored.LoadedAt);
        }

        lock (gate)
        {
            if (failures.TryGetValue((canton, year), out var failure))
            {
                return LoadState.NotLoaded(failure.Reason);
            }
        }

        return IsRequestable(year) ? LoadState.NotLoaded(null) : LoadState.OutOfRange;
    }

    public void Need(Canton canton, int year)
    {
        if (!IsRequestable(year))
        {
            return;
        }

        lock (gate)
        {
            var failedRecently = failures.TryGetValue((canton, year), out var failure) && clock.GetUtcNow() - failure.At < RetryAfter;
            if (!running.ContainsKey((canton, year)) && !failedRecently)
            {
                Start((canton, year), unlessStored: true);
            }
        }
    }

    public Task<LoadState> ReloadAsync(Canton canton, int year)
    {
        if (!IsRequestable(year))
        {
            return Task.FromResult(LoadState.OutOfRange);
        }

        lock (gate)
        {
            return running.TryGetValue((canton, year), out var load) ? load : Start((canton, year), unlessStored: false);
        }
    }

    /// <summary>Called under the gate: the load removes itself under the gate once it ends, so it is listed before it can.</summary>
    private Task<LoadState> Start((Canton Canton, int Year) key, bool unlessStored)
    {
        var load = Task.Run(() => LoadAsync(key, unlessStored));
        running[key] = load;
        return load;
    }

    private async Task<LoadState> LoadAsync((Canton Canton, int Year) key, bool unlessStored)
    {
        try
        {
            if (unlessStored && await store.FindAsync(key.Canton, key.Year) is { } stored)
            {
                return LoadState.Loaded(stored.LoadedAt);
            }

            var load = await loader.LoadAsync(key.Canton, key.Year, stopping);
            Remember(key, load.Failure);
            return load.State;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return LoadState.NotLoaded(null);
        }
        catch (Exception failure)
        {
            errors.Write($"Loading the public holidays of {key.Canton} {key.Year}", failure);
            Remember(key, LoadFailure.Invalid);
            return LoadState.NotLoaded(LoadFailure.Invalid);
        }
        finally
        {
            lock (gate)
            {
                running.Remove(key);
            }
        }
    }

    private void Remember((Canton, int) key, LoadFailure? failure)
    {
        lock (gate)
        {
            if (failure is { } reason)
            {
                failures[key] = (reason, clock.GetUtcNow());
            }
            else
            {
                failures.Remove(key);
            }
        }
    }

    private bool IsRequestable(int year)
    {
        var thisYear = clock.GetLocalNow().LocalDate().Year;
        return year >= thisYear - 1 && year <= thisYear + 1;
    }
}
