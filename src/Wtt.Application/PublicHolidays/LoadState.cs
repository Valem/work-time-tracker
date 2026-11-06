using Wtt.Application.Ports;

namespace Wtt.Application.PublicHolidays;

public enum LoadStatus
{
    /// <summary>No canton is chosen, so there is nothing to load.</summary>
    NoCanton,

    /// <summary>A request for this canton and year is under way.</summary>
    Loading,

    /// <summary>Stored, with the time it was loaded and, where the last request of this run failed, why.</summary>
    Loaded,

    /// <summary>Not stored; with the reason where a load failed in this run.</summary>
    NotLoaded,

    /// <summary>Not stored, and outside last, this and next year, so never requested.</summary>
    OutOfRange,
}

/// <summary>
/// Whether a canton's public holidays of a year are there to count with, and why not; or, stored, why the last request of
/// this run for them failed — what is stored stays.
/// </summary>
public sealed record LoadState(LoadStatus Status, DateTimeOffset? LoadedAt = null, LoadFailure? Reason = null)
{
    public static LoadState NoCanton { get; } = new(LoadStatus.NoCanton);

    public static LoadState Loading { get; } = new(LoadStatus.Loading);

    public static LoadState OutOfRange { get; } = new(LoadStatus.OutOfRange);

    /// <param name="lastFailure">Why the last request of this run failed, where it did; what it requested is stored all the same.</param>
    public static LoadState Loaded(DateTimeOffset at, LoadFailure? lastFailure = null) => new(LoadStatus.Loaded, LoadedAt: at, Reason: lastFailure);

    public static LoadState NotLoaded(LoadFailure? reason) => new(LoadStatus.NotLoaded, Reason: reason);
}
