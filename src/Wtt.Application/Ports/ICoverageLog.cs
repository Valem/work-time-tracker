using Wtt.Domain.Coverage;
using Wtt.Domain.Time;

namespace Wtt.Application.Ports;

public interface ICoverageLog
{
    Task ExtendAsync(DateTimeOffset now);

    Task<IReadOnlyList<CoverageSpan>> ReadAsync(Interval range);
}
