using Wtt.Domain.Facts;
using Wtt.Domain.Time;

namespace Wtt.Domain.Presence;

/// <summary>
/// Walks the effective facts in time order and turns them into use spans.
/// Use starts at an unlock or a sign-in. Where no lock screen proves the person — a live resume
/// without one, or history older than the Winlogon log — a person-looking signal starts use only if
/// the laptop then stays awake more than <see cref="ProofDelay"/>. Use ends at a lock, standby,
/// sign-out or shutdown; a live end fired by a timer is moved back to when the person left.
/// </summary>
internal sealed class UseTracker(DateTimeOffset? winlogonHistoryFrom)
{
    public static readonly TimeSpan ProofDelay = TimeSpan.FromMinutes(2);

    private readonly List<UseSpan> uses = [];
    private OpenUse? open;
    private Candidate? pending;
    private bool locked;
    private bool resumedSinceLastEnd;
    private DateTimeOffset? lastFactAt;

    public void Apply(EffectiveFact effective)
    {
        var fact = effective.Fact;
        ConfirmPendingIfProven(fact.At);

        switch (fact.Kind)
        {
            case FactKind.Unlock:
            case FactKind.SignIn:
                locked = false;
                pending = null;
                Start(fact.At, effective.Rebuilt);
                break;
            case FactKind.Lock:
                locked = true;
                pending = null;
                End(effective);
                break;
            case FactKind.StandbyEnter:
            case FactKind.SignOut:
            case FactKind.Shutdown:
                pending = null;
                End(effective);
                break;
            case FactKind.Boot:
                pending = null;
                EndAfterUnexpectedStop(effective);
                break;
            case FactKind.StandbyExit:
                OnStandbyExit(effective);
                break;
            case FactKind.LastInput:
                OnInput(effective);
                break;
            case FactKind.IdleTimeout:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effective), fact.Kind, "Unknown fact kind.");
        }

        lastFactAt = fact.At;
    }

    public IReadOnlyList<UseSpan> Finish(DateTimeOffset rangeEnd)
    {
        ConfirmPendingIfProven(rangeEnd);
        if (open is { } use)
        {
            var end = DateTimeOffsetExtensions.Max(use.Start, rangeEnd).ToOffset(use.Start.Offset);
            uses.Add(new UseSpan(use.Start, end, use.Precision));
            open = null;
        }

        return uses;
    }

    private void ConfirmPendingIfProven(DateTimeOffset now)
    {
        if (pending is { } candidate && now - candidate.At > ProofDelay)
        {
            pending = null;
            Start(candidate.At, candidate.Rebuilt);
        }
    }

    private void Start(DateTimeOffset at, bool rebuilt)
    {
        open ??= new OpenUse(at, rebuilt ? Precision.Rebuilt : Precision.Exact, LastInput: null);
    }

    private void End(EffectiveFact effective)
    {
        if (open is not { } use)
        {
            return;
        }

        Close(use, EndTime(effective.Fact, use), effective.Rebuilt);
    }

    /// <summary>
    /// A boot while still in use means Windows stopped without a shutdown and without event 6008 naming
    /// when (which would have ended the use already); use ends at the last sign of life.
    /// </summary>
    private void EndAfterUnexpectedStop(EffectiveFact effective)
    {
        if (open is not { } use)
        {
            return;
        }

        Close(use, DateTimeOffsetExtensions.Max(use.Start, lastFactAt ?? use.Start), effective.Rebuilt);
    }

    private void Close(OpenUse use, DateTimeOffset end, bool endRebuilt)
    {
        var precision = use.Precision == Precision.Rebuilt || endRebuilt ? Precision.Rebuilt : Precision.Exact;
        uses.Add(new UseSpan(use.Start, end, precision));
        open = null;
        resumedSinceLastEnd = false;
    }

    private static DateTimeOffset EndTime(ActivityFact fact, OpenUse use)
    {
        if (!fact.IsLive || !fact.Detail.IsTimerEnd)
        {
            return fact.At;
        }

        var leftAccordingToTimer = fact.At - fact.Detail.Timer!.Value;
        var lastInput = use.LastInput ?? use.Start;
        var end = DateTimeOffsetExtensions.Max(leftAccordingToTimer, lastInput);
        return DateTimeOffsetExtensions.Min(DateTimeOffsetExtensions.Max(end, use.Start), fact.At);
    }

    private void OnStandbyExit(EffectiveFact effective)
    {
        if (open is not null)
        {
            return;
        }

        resumedSinceLastEnd = true;
        var fact = effective.Fact;
        if (!fact.IsLive && IsBeyondWinlogonHistory(fact.At) && WakeReason.IsPerson(fact.Detail.Reason))
        {
            pending ??= new Candidate(fact.At, effective.Rebuilt);
        }
    }

    private void OnInput(EffectiveFact effective)
    {
        var fact = effective.Fact;
        if (open is { } use)
        {
            var latest = use.LastInput is { } previous ? DateTimeOffsetExtensions.Max(previous, fact.At) : fact.At;
            open = use with { LastInput = latest };
            return;
        }

        if (fact.IsLive && !locked && resumedSinceLastEnd)
        {
            pending ??= new Candidate(fact.At, effective.Rebuilt);
        }
    }

    private bool IsBeyondWinlogonHistory(DateTimeOffset at) =>
        winlogonHistoryFrom is not { } from || at < from;

    private sealed record OpenUse(DateTimeOffset Start, Precision Precision, DateTimeOffset? LastInput);

    private sealed record Candidate(DateTimeOffset At, bool Rebuilt);
}
