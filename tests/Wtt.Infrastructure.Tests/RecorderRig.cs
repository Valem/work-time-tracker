using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Application.Queries;
using Wtt.Application.Recording;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure.Storage;

namespace Wtt.Infrastructure.Tests;

/// <summary>
/// The Recorder on a real data file, driven by a scripted day: Windows' notifications, the session's last
/// input, the minute heartbeat, and a Windows history that only holds what has happened by the clock's now.
/// </summary>
internal sealed class RecorderRig : IDisposable
{
    public static readonly TimeSpan Local = TimeSpan.FromHours(2);

    private readonly TempDatabase file = new();
    private readonly SqliteDatabase database;
    private readonly ScriptedHistory history;

    public RecorderRig(DateTimeOffset now)
    {
        Clock = new FakeClock(now);
        database = file.Open(Clock);
        history = new ScriptedHistory(Clock);
        Journal = new SqliteFactJournal(database);
        Coverage = new SqliteCoverageLog(database, Clock);
    }

    public FakeClock Clock { get; }

    public FakeDesktop Desktop { get; } = new();

    public SqliteFactJournal Journal { get; }

    public SqliteCoverageLog Coverage { get; }

    public List<(string Context, Exception Error)> Errors { get; } = [];

    public LiveRecorder? Recorder { get; private set; }

    public static DateTimeOffset At(string localTime) =>
        new(DateTime.ParseExact(localTime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), Local);

    public void Dispose() => file.Dispose();

    /// <summary>Windows logs this; the Recorder sees it only when it imports history.</summary>
    public void WindowsLogs(FactKind kind, string at, FactSource source = FactSource.WinlogonHistory, string? reason = null) =>
        history.Add(new ActivityFact(At(at), kind, source, $"{source}#{history.Count + 1}", new FactDetail(Reason: reason, Session: Desktop.Session.Id)));

    /// <summary>The Recorder process starts (Run key, App or a restart) in the given session.</summary>
    public async Task StartRecorderAsync(string session, string signedInAt)
    {
        Desktop.Session = new SignInSession(session, At(signedInAt), Locked: false);
        Recorder = new LiveRecorder(
            Journal,
            Coverage,
            new HistoryImport(history, Journal, new SqliteHistoryCursorStore(database)),
            new UserSid("S-1-5-21-1000000000-2000000000-3000000000-1001"),
            Desktop,
            new RigErrorLog(Errors),
            Clock);
        await Recorder.StartAsync();
        await Recorder.CatchUpAsync();
    }

    /// <summary>The Recorder runs until then: a heartbeat every minute, the input read every 5 s while a resume waits for it.</summary>
    public async Task RunUntilAsync(string until)
    {
        var end = At(until);
        var beat = LiveRecorder.HeartbeatInterval.Ticks;
        while (Clock.Now < end)
        {
            var nextBeat = new DateTimeOffset((Clock.Now.UtcTicks / beat + 1) * beat, TimeSpan.Zero);
            var nextPoll = Recorder!.AwaitsInput ? Clock.Now + LiveRecorder.InputPollInterval : DateTimeOffset.MaxValue;
            var next = nextBeat < nextPoll ? nextBeat : nextPoll;
            if (next > end)
            {
                Clock.Now = end;
                break;
            }

            Clock.Now = next;
            if (next == nextPoll)
            {
                await Recorder.PollInputAsync();
            }

            if (next == nextBeat)
            {
                await Recorder.HeartbeatAsync();
            }
        }
    }

    /// <summary>The person's last real input happens then.</summary>
    public async Task InputAsync(string at)
    {
        await RunUntilAsync(at);
        Desktop.LastInputAt = At(at);
    }

    /// <summary>Windows sends a notification; as the Recorder's window does, the session is read on arrival.</summary>
    public async Task SignalAsync(LiveSignalKind kind, string at)
    {
        await RunUntilAsync(at);
        var context = new SignalContext(Desktop.LastInput(), Desktop.TimersInForce());
        await Recorder!.OnSignalAsync(new LiveSignal(kind, At(at), context));
    }

    /// <summary>Standby, Recorder stopped or the laptop off: time passes and the Recorder sees none of it.</summary>
    public void TimePassesUnseen(string until) => Clock.Now = At(until);

    public Task<IReadOnlyList<CoverageSpan>> SpansAsync() => Coverage.ReadAsync(Interval.All);

    public Task<Workday> DayAsync(string date)
    {
        var queries = new WorkTimeQueries(
            new PresenceReader(Journal, Coverage, new PresenceBuilder(), Clock),
            Coverage,
            new SqliteLunchRules(database, Clock),
            new WorkdayCalculator(),
            Clock);
        return queries.GetDayAsync(DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class ScriptedHistory(TimeProvider clock) : IHistorySource
    {
        private readonly List<ActivityFact> facts = [];

        public int Count => facts.Count;

        public void Add(ActivityFact fact) => facts.Add(fact);

        public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user)
        {
            var now = clock.GetUtcNow();
            var since = facts.Where(fact => fact.At <= now && (cursor.ReadFrom is null || fact.At >= cursor.ReadFrom)).ToList();
            var next = since.Count == 0 ? cursor : new HistoryCursor(since.Max(fact => fact.At), null);
            return Task.FromResult(new HistoryBatch(since, next));
        }
    }

    private sealed class RigErrorLog(List<(string Context, Exception Error)> entries) : IErrorLog
    {
        public void Write(string context, Exception error) => entries.Add((context, error));
    }
}

internal sealed class FakeDesktop : IDesktop
{
    public SignInSession Session { get; set; } = new("1", DateTimeOffset.MinValue, Locked: false);

    public DateTimeOffset LastInputAt { get; set; }

    public IdleTimers Timers { get; set; } = IdleTimers.None;

    public SignInSession CurrentSession() => Session;

    public DateTimeOffset LastInput() => LastInputAt;

    public IdleTimers TimersInForce() => Timers;
}
