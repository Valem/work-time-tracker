namespace Wtt.Domain.Facts;

/// <summary>
/// One thing Windows or the Recorder observed. <see cref="At"/> carries both the instant and the
/// local UTC offset in force when it happened, so a later time-zone change never moves its date.
/// </summary>
public sealed record ActivityFact(
    DateTimeOffset At,
    FactKind Kind,
    FactSource Source,
    string SourceKey,
    FactDetail Detail)
{
    public ActivityFact(DateTimeOffset at, FactKind kind, FactSource source, string sourceKey)
        : this(at, kind, source, sourceKey, FactDetail.None)
    {
    }

    public bool IsLive => Source == FactSource.Live;
}
