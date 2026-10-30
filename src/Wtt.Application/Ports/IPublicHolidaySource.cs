using Wtt.Domain.Planning;

namespace Wtt.Application.Ports;

/// <summary>Why a canton's public holidays of a year are not loaded.</summary>
public enum LoadFailure
{
    /// <summary>The source could not be reached or gave no complete answer in time.</summary>
    Unreachable,

    /// <summary>A proxy on the way asks for a sign-in; no credentials are ever sent.</summary>
    ProxySignIn,

    /// <summary>The answer was refused whole: not what the source answers, or not plausible.</summary>
    Invalid,
}

/// <summary>A read of public holidays failed; the message holds the reason only, never the answer's content.</summary>
public sealed class PublicHolidayReadException(LoadFailure reason, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public LoadFailure Reason { get; } = reason;
}

/// <summary>Where a canton's public holidays of a year come from. It translates the answer into the domain's terms and judges nothing.</summary>
public interface IPublicHolidaySource
{
    HolidaySource Source { get; }

    /// <exception cref="PublicHolidayReadException">The source failed, or its answer was refused whole.</exception>
    Task<PublicHolidayYear> ReadAsync(Canton canton, int year, CancellationToken cancel);
}

/// <summary>The public-holiday years loaded, per canton and year.</summary>
public interface IPublicHolidayStore
{
    Task<PublicHolidayYear?> FindAsync(Canton canton, int year);

    /// <summary>Stores the year whole, in place of what was stored for its canton and year.</summary>
    Task ReplaceAsync(PublicHolidayYear year);
}
