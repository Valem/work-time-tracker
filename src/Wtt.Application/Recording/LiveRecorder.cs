using System.Globalization;
using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Domain.Facts;

namespace Wtt.Application.Recording;

/// <summary>
/// Turns what Windows tells the Recorder into live facts and coverage, without judging them: whether a
/// signal is use is the domain's call. Calls must come one at a time; the host serializes them.
/// </summary>
public sealed class LiveRecorder(
    IFactJournal journal,
    ICoverageLog coverage,
    HistoryImport history,
    UserSid user,
    IDesktop desktop,
    IErrorLog errors,
    TimeProvider clock)
{
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);

    /// <summary>How often the last input is read while a resume without a lock screen waits for the person.</summary>
    public static readonly TimeSpan InputPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>A heartbeat this soon after a suspend notification may still run before Windows actually suspends.</summary>
    private static readonly TimeSpan SuspendSettling = TimeSpan.FromSeconds(10);

    private SignInSession? session;
    private DateTimeOffset startedAt;
    private DateTimeOffset lastBeat;
    private DateTimeOffset? suspendedAt;
    private bool resumeSeen;
    private bool locked;
    private DateTimeOffset? lidClosedAt;
    private DateTimeOffset? awaitingInputSince;
    private volatile bool awaitsInput;
    private EndReason? idleLockWritten;

    /// <summary>True while a resume without a lock screen waits for the first real input; read from any thread.</summary>
    public bool AwaitsInput => awaitsInput;

    private SignInSession Session => session ?? throw new InvalidOperationException("The Recorder has not started.");

    /// <summary>
    /// Coverage (from the sign-in when started right after it), the sign-in and the idle lock timeout in force;
    /// <see cref="CatchUpAsync"/> follows.
    /// </summary>
    public async Task StartAsync()
    {
        var now = clock.GetUtcNow();
        session = desktop.CurrentSession();
        locked = session.Locked;
        startedAt = now;
        lastBeat = now;

        await coverage.BeginAsync(session.SignedInAt);
        idleLockWritten = EndReasons.IdleLockInForce(desktop.TimersInForce());
        await journal.AppendAsync([
            Fact(FactKind.SignIn, session.SignedInAt),
            IdleTimeoutFact(now, idleLockWritten),
        ]);
    }

    /// <summary>Imports what Windows kept since the cursor: the days before the first start and any gap since.</summary>
    public Task CatchUpAsync() => ImportHistoryAsync();

    public async Task HeartbeatAsync()
    {
        var now = clock.GetUtcNow();
        if (suspendedAt is { } suspended)
        {
            if (now - suspended > SuspendSettling)
            {
                await OnResumedAsync(now);
            }

            return;
        }

        var gap = now - lastBeat > ICoverageLog.MaxHeartbeatGap;
        await coverage.ExtendAsync(now);
        lastBeat = now;
        await RecordIdleLockIfChangedAsync(now);
        if (gap)
        {
            await ImportHistoryAsync();
        }
    }

    /// <summary>Records the first real input after a resume without a lock screen.</summary>
    public async Task PollInputAsync()
    {
        if (awaitingInputSince is not { } since)
        {
            return;
        }

        var lastInput = desktop.LastInput();
        if (lastInput <= since)
        {
            return;
        }

        StopAwaitingInput();
        await journal.AppendAsync([Fact(FactKind.LastInput, lastInput)]);
    }

    public Task OnSignalAsync(LiveSignal signal) => signal.Kind switch
    {
        LiveSignalKind.Locked => OnLockedAsync(signal.At),
        LiveSignalKind.Unlocked => OnUnlockedAsync(signal.At),
        LiveSignalKind.Suspending => OnSuspendingAsync(signal.At),
        LiveSignalKind.Resumed => OnResumedAsync(signal.At),
        LiveSignalKind.LidClosed => OnLidAsync(signal.At),
        LiveSignalKind.LidOpened => OnLidAsync(closedAt: null),
        LiveSignalKind.SigningOut => OnSessionEndAsync(FactKind.SignOut, EndCause.SignOut, signal.At),
        LiveSignalKind.ShuttingDown => OnSessionEndAsync(FactKind.Shutdown, EndCause.Shutdown, signal.At),
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal.Kind, "Unknown signal."),
    };

    private Task OnLockedAsync(DateTimeOffset at)
    {
        locked = true;
        StopAwaitingInput();
        return EndUseAsync(FactKind.Lock, at, idle => EndReasons.ForLock(idle, desktop.TimersInForce(), LidJustClosed(at)));
    }

    private Task OnUnlockedAsync(DateTimeOffset at)
    {
        locked = false;
        StopAwaitingInput();
        return journal.AppendAsync([Fact(FactKind.Unlock, at)]);
    }

    private async Task OnSuspendingAsync(DateTimeOffset at)
    {
        if (suspendedAt is not null)
        {
            return;
        }

        StopAwaitingInput();
        await EndUseAsync(FactKind.StandbyEnter, at, idle => EndReasons.ForStandby(idle, desktop.TimersInForce(), LidJustClosed(at)));
        await coverage.ExtendAsync(at);
        suspendedAt = at;
        resumeSeen = false;
        lastBeat = at;
    }

    /// <summary>
    /// Windows announces a wake the person caused twice (automatic, then suspend resume); only the first counts.
    /// A resume without a suspend seen before it is a gap in coverage.
    /// </summary>
    private async Task OnResumedAsync(DateTimeOffset at)
    {
        var gap = false;
        if (suspendedAt is { } from)
        {
            await coverage.ResumeAsync(from, at);
            suspendedAt = null;
        }
        else if (resumeSeen)
        {
            return;
        }
        else
        {
            gap = at - lastBeat > ICoverageLog.MaxHeartbeatGap;
            await coverage.ExtendAsync(at);
        }

        resumeSeen = true;
        lastBeat = at;
        await journal.AppendAsync([Fact(FactKind.StandbyExit, at)]);
        if (!locked)
        {
            awaitingInputSince = at;
            awaitsInput = true;
        }

        if (gap)
        {
            await ImportHistoryAsync();
        }
    }

    private Task OnLidAsync(DateTimeOffset? closedAt)
    {
        lidClosedAt = closedAt;
        return Task.CompletedTask;
    }

    private async Task OnSessionEndAsync(FactKind kind, EndCause cause, DateTimeOffset at)
    {
        StopAwaitingInput();
        await EndUseAsync(kind, at, _ => new EndReason(cause));
        await coverage.ExtendAsync(at);
        lastBeat = at;
    }

    /// <summary>The session's last input goes with every end, so a timer end can fall back to it (BR-003).</summary>
    private Task EndUseAsync(FactKind kind, DateTimeOffset at, Func<TimeSpan, EndReason> reasonAfterIdle)
    {
        var lastInput = desktop.LastInput();
        var idle = at > lastInput ? at - lastInput : TimeSpan.Zero;
        var reason = reasonAfterIdle(idle);

        var facts = new List<ActivityFact>();
        if (lastInput >= startedAt && lastInput <= at)
        {
            facts.Add(Fact(FactKind.LastInput, lastInput));
        }

        facts.Add(Fact(kind, at, new FactDetail(reason.Cause, reason.Timer, Session: Session.Id)));
        return journal.AppendAsync(facts);
    }

    private async Task RecordIdleLockIfChangedAsync(DateTimeOffset now)
    {
        var inForce = EndReasons.IdleLockInForce(desktop.TimersInForce());
        if (inForce != idleLockWritten)
        {
            idleLockWritten = inForce;
            await journal.AppendAsync([IdleTimeoutFact(now, inForce)]);
        }
    }

    private async Task ImportHistoryAsync()
    {
        try
        {
            await history.ImportAsync(user);
        }
        catch (Exception failure)
        {
            errors.Write("Importing Windows history", failure);
        }
    }

    private bool LidJustClosed(DateTimeOffset at) =>
        lidClosedAt is { } closed && at - closed <= EndReasons.LidWindow && at >= closed;

    private void StopAwaitingInput()
    {
        awaitingInputSince = null;
        awaitsInput = false;
    }

    private ActivityFact IdleTimeoutFact(DateTimeOffset at, EndReason? idleLock) =>
        Fact(FactKind.IdleTimeout, at, new FactDetail(idleLock?.Cause, idleLock?.Timer, Session: Session.Id));

    private ActivityFact Fact(FactKind kind, DateTimeOffset at, FactDetail? detail = null)
    {
        var local = TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone);
        return new ActivityFact(local, kind, FactSource.Live, SourceKey(kind, local), detail ?? new FactDetail(Session: Session.Id));
    }

    /// <summary>Kind, session and instant: the same sign-in or last input noted twice is stored once.</summary>
    private string SourceKey(FactKind kind, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"live:{kind}:{Session.Id}@{at.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
}
