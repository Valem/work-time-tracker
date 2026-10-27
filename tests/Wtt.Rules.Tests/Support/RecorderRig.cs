using System.Globalization;
using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Application.Queries;
using Wtt.Application.Recording;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure.Storage;

namespace Wtt.Rules.Tests.Support;

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public static readonly TimeZoneInfo Utc2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");

    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone => Utc2;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>A data file in its own temporary folder, deleted afterwards; never a real data file.</summary>
internal sealed class TempDatabase : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "wtt-tests", Guid.NewGuid().ToString("N"));

    public SqliteDatabase Open(TimeProvider clock) => SqliteDatabase.Open(Path.Combine(folder, "wtt.db"), clock);

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

/// <summary>
/// The Recorder on a real data file in a temporary folder, driven by a scripted day: Windows' notifications, the
/// session's last input, the minute heartbeat, and a Windows history that only holds what has happened by the clock's now.
/// </summary>
internal sealed class RecorderRig : IDisposable
{
    public static readonly TimeSpan Local = TimeSpan.FromHours(2);

    private readonly TempDatabase file = new();
    private readonly SqliteDatabase database;
    private readonly ScriptedHistory history;
    private LiveRecorder? recorder;

    public RecorderRig(string now)
    {
        Clock = new FakeClock(At(now));
        database = file.Open(Clock);
        history = new ScriptedHistory(Clock);
        Journal = new SqliteFactJournal(database);
        Coverage = new SqliteCoverageLog(database, Clock);
    }

    public FakeClock Clock { get; }

    public FakeDesktop Desktop { get; } = new();

    public SqliteFactJournal Journal { get; }

    public SqliteCoverageLog Coverage { get; }

    private LiveRecorder Recorder => recorder ?? throw new InvalidOperationException("Start the Recorder first.");

    public static DateTimeOffset At(string localTime) =>
        new(DateTime.ParseExact(localTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), Local);

    public void Dispose() => file.Dispose();

    /// <summary>Windows logs this; the Recorder sees it only when it imports history.</summary>
    public void WindowsLogs(FactKind kind, string at, FactSource source = FactSource.WinlogonHistory) =>
        history.Add(new ActivityFact(At(at), kind, source, $"{source}#{history.Count + 1}", new FactDetail(Session: Desktop.Session.Id)));

    /// <summary>The Recorder process starts (Run key, App or a restart) in the given session.</summary>
    public async Task StartRecorderAsync(string session, string signedInAt)
    {
        Desktop.Session = new SignInSession(session, At(signedInAt), Locked: false);
        recorder = new LiveRecorder(
            Journal,
            Coverage,
            new HistoryCatchUp(new HistoryImport(history, Journal, new SqliteHistoryCursorStore(database)), Users.Me, new ThrowingErrorLog()),
            Desktop,
            Clock);
        await recorder.StartAsync();
        await recorder.CatchUpAsync();
    }

    /// <summary>The Recorder runs until then: a heartbeat every minute, the input read every 5 s while a resume waits for it.</summary>
    public async Task RunUntilAsync(string until)
    {
        var end = At(until);
        var beat = LiveRecorder.HeartbeatInterval.Ticks;
        while (Clock.Now < end)
        {
            var nextBeat = new DateTimeOffset(((Clock.Now.UtcTicks / beat) + 1) * beat, TimeSpan.Zero);
            var nextPoll = Recorder.AwaitsInput ? Clock.Now + LiveRecorder.InputPollInterval : DateTimeOffset.MaxValue;
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
        await Recorder.OnSignalAsync(new LiveSignal(kind, At(at), context));
    }

    /// <summary>Standby, Recorder stopped or the laptop off: time passes and the Recorder sees none of it.</summary>
    public void TimePassesUnseen(string until) => Clock.Now = At(until);

    public Task<Workday> DayAsync(string date)
    {
        var queries = new WorkTimeQueries(
            new PresenceReader(Journal, Coverage, new PresenceBuilder(), Clock),
            Coverage,
            new SqliteLunchRules(database, Clock),
            new WorkdayCalculator(),
            Clock);
        return queries.GetDayAsync(DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture));
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

    private sealed class ThrowingErrorLog : IErrorLog
    {
        public void Write(string context, Exception error) => throw new InvalidOperationException(context, error);
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
