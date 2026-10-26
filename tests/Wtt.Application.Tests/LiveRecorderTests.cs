using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Application.Recording;
using Wtt.Domain.Coverage;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;
using static Wtt.Application.Tests.Facts;

namespace Wtt.Application.Tests;

public sealed class LiveRecorderTests
{
    private static readonly UserSid Me = new("S-1-5-21-1000000000-2000000000-3000000000-1001");

    private readonly FakeClock clock = new(At("2026-10-20 08:10"));
    private readonly InMemoryFactJournal journal = new();
    private readonly CallLog coverage = new();
    private readonly FakeDesktop desktop = new();
    private readonly ListErrorLog errors = new();
    private IHistorySource history = new FakeHistorySource([]);

    public LiveRecorderTests()
    {
        desktop.Session = new SignInSession("2", At("2026-10-20 08:10").AddSeconds(-25), Locked: false);
        desktop.LastInputAt = At("2026-10-20 08:10");
        desktop.Timers = new IdleTimers(DisplayOff: TimeSpan.FromMinutes(5), ScreenSaverLock: TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Starting_begins_coverage_at_the_sign_in_and_records_the_logon_time_and_the_idle_lock_timeout()
    {
        await NewRecorder().StartAsync();

        Assert.Equal(["Begin 08:09:35"], coverage.Calls);
        var facts = await Recorded();
        Assert.Equal([FactKind.SignIn, FactKind.IdleTimeout], facts.Select(fact => fact.Kind));
        Assert.All(facts, fact => Assert.Equal(FactSource.Live, fact.Source));
        Assert.Equal(desktop.Session.SignedInAt, facts[0].At);
        Assert.Equal("2", facts[0].Detail.Session);
        Assert.Equal(new FactDetail(EndCause.IdleLock, TimeSpan.FromMinutes(5), Session: "2"), facts[1].Detail);
    }

    [Fact]
    public async Task Live_facts_carry_the_utc_offset_in_force_when_they_happened()
    {
        clock.Now = clock.Now.ToUniversalTime();
        await NewRecorder().StartAsync();

        Assert.All(await Recorded(), fact => Assert.Equal(TimeSpan.FromHours(2), fact.At.Offset));
    }

    [Fact]
    public async Task Starting_again_in_the_same_session_records_its_sign_in_once()
    {
        await NewRecorder().StartAsync();
        clock.Now = clock.Now.AddHours(2);
        await NewRecorder().StartAsync();

        Assert.Single(await Recorded(), fact => fact.Kind == FactKind.SignIn);
    }

    [Fact]
    public async Task Catching_up_imports_this_users_history_since_the_cursor()
    {
        history = new FakeHistorySource([Unlock("2026-10-19 08:00"), Lock("2026-10-19 17:00")]);
        var recorder = NewRecorder();
        await recorder.StartAsync();

        await recorder.CatchUpAsync();

        Assert.Equal(2, (await Recorded()).Count(fact => !fact.IsLive));
    }

    [Fact]
    public async Task A_history_read_that_fails_is_logged_and_recording_goes_on()
    {
        history = new FailingHistorySource();
        var recorder = NewRecorder();
        await recorder.StartAsync();

        await recorder.CatchUpAsync();
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Locked, clock.Now.AddMinutes(1)));

        Assert.Equal("Importing Windows history", Assert.Single(errors.Entries).Context);
        Assert.Contains(await Recorded(), fact => fact.Kind == FactKind.Lock);
    }

    [Fact]
    public async Task A_lock_by_the_person_records_the_last_input_and_the_lock()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 12:00").AddSeconds(-1);

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Locked, At("2026-10-20 12:00")));

        var ending = (await Recorded()).TakeLast(2).ToList();
        Assert.Equal([FactKind.LastInput, FactKind.Lock], ending.Select(fact => fact.Kind));
        Assert.Equal(desktop.LastInputAt, ending[0].At);
        Assert.Equal(new FactDetail(EndCause.UserLock, Session: "2"), ending[1].Detail);
    }

    [Fact]
    public async Task An_idle_lock_records_the_timer_that_fired()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 17:26");

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Locked, At("2026-10-20 17:31")));

        var lockFact = (await Recorded()).Single(fact => fact.Kind == FactKind.Lock);
        Assert.Equal(new FactDetail(EndCause.IdleLock, TimeSpan.FromMinutes(5), Session: "2"), lockFact.Detail);
    }

    [Fact]
    public async Task A_lock_recorded_after_the_persons_next_input_keeps_the_last_input_and_timers_from_its_arrival()
    {
        var recorder = await StartedAsync();
        var arrived = new LiveSignal(
            LiveSignalKind.Locked,
            At("2026-10-20 17:31"),
            new SignalContext(At("2026-10-20 17:26"), desktop.Timers));
        desktop.LastInputAt = At("2026-10-20 17:40");
        desktop.Timers = IdleTimers.None;

        await recorder.OnSignalAsync(arrived);

        var ending = (await Recorded()).TakeLast(2).ToList();
        Assert.Equal([FactKind.LastInput, FactKind.Lock], ending.Select(fact => fact.Kind));
        Assert.Equal(At("2026-10-20 17:26"), ending[0].At);
        Assert.Equal(new FactDetail(EndCause.IdleLock, TimeSpan.FromMinutes(5), Session: "2"), ending[1].Detail);
    }

    [Fact]
    public async Task A_standby_keeps_the_utc_offset_in_force_when_it_arrived_after_the_time_zone_changed()
    {
        var recorder = await StartedAsync();
        var beforeTravel = TimeSpan.FromHours(1);
        var arrived = new LiveSignal(
            LiveSignalKind.Suspending,
            At("2026-10-20 18:00").ToOffset(beforeTravel),
            new SignalContext(At("2026-10-20 17:59"), desktop.Timers));

        await recorder.OnSignalAsync(arrived);

        var ending = (await Recorded()).TakeLast(2).ToList();
        Assert.Equal([FactKind.LastInput, FactKind.StandbyEnter], ending.Select(fact => fact.Kind));
        Assert.All(ending, fact => Assert.Equal(beforeTravel, fact.At.Offset));
    }

    [Fact]
    public async Task A_last_input_from_before_the_Recorder_started_is_not_recorded()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 07:00");

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Locked, At("2026-10-20 08:30")));

        Assert.DoesNotContain(await Recorded(), fact => fact.Kind == FactKind.LastInput);
    }

    [Fact]
    public async Task Closing_the_lid_makes_the_standby_that_follows_a_lid_end()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 09:00");

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.LidClosed, At("2026-10-20 09:30")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 09:30").AddSeconds(2)));

        var standby = (await Recorded()).Single(fact => fact.Kind == FactKind.StandbyEnter);
        Assert.Equal(EndCause.Lid, standby.Detail.Cause);
    }

    [Fact]
    public async Task A_lid_opened_again_no_longer_explains_a_later_standby()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 09:31");

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.LidClosed, At("2026-10-20 09:30")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.LidOpened, At("2026-10-20 09:30").AddSeconds(5)));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 09:30").AddSeconds(20)));

        Assert.Equal(EndCause.Unknown, (await Recorded()).Single(fact => fact.Kind == FactKind.StandbyEnter).Detail.Cause);
    }

    [Fact]
    public async Task A_standby_the_Recorder_saw_begin_and_end_is_bridged_in_coverage_and_its_resume_recorded_once()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 12:00");

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 12:00")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 12:45")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 12:45").AddSeconds(1)));

        Assert.Equal(["Begin 08:09:35", "Extend 12:00:00", "Resume 12:00:00-12:45:00"], coverage.Calls);
        Assert.Single(await Recorded(), fact => fact.Kind == FactKind.StandbyExit);
    }

    [Fact]
    public async Task A_resume_without_a_lock_screen_waits_for_the_first_real_input_and_records_it_once()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 12:00");
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 12:00")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 12:40")));

        Assert.True(recorder.AwaitsInput);
        await recorder.PollInputAsync();
        Assert.True(recorder.AwaitsInput);

        desktop.LastInputAt = At("2026-10-20 12:41").AddSeconds(3);
        await recorder.PollInputAsync();
        desktop.LastInputAt = At("2026-10-20 12:42");
        await recorder.PollInputAsync();

        Assert.False(recorder.AwaitsInput);
        var input = Assert.Single(await Recorded(), fact => fact.Kind == FactKind.LastInput && fact.At > At("2026-10-20 12:30"));
        Assert.Equal(At("2026-10-20 12:41").AddSeconds(3), input.At);
    }

    [Fact]
    public async Task Starting_in_an_unlocked_session_waits_for_the_first_real_input_after_the_start()
    {
        var recorder = await StartedAsync();
        Assert.True(recorder.AwaitsInput);

        await recorder.PollInputAsync();
        Assert.True(recorder.AwaitsInput);

        desktop.LastInputAt = At("2026-10-20 08:12");
        await recorder.PollInputAsync();

        Assert.False(recorder.AwaitsInput);
        Assert.Contains(await Recorded(), fact => fact.Kind == FactKind.LastInput && fact.At == At("2026-10-20 08:12"));
    }

    [Fact]
    public async Task Starting_in_a_locked_session_waits_for_the_unlock_not_for_input()
    {
        desktop.Session = desktop.Session with { Locked = true };

        var recorder = await StartedAsync();

        Assert.False(recorder.AwaitsInput);
    }

    [Fact]
    public async Task A_resume_after_a_missed_suspend_notification_is_a_wake_of_its_own_and_waits_for_input()
    {
        var recorder = await StartedAsync();
        desktop.LastInputAt = At("2026-10-20 12:00");
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 12:00")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 12:45")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 12:45").AddSeconds(2)));
        desktop.LastInputAt = At("2026-10-20 12:46");
        await recorder.PollInputAsync();

        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 15:00")));

        var exits = (await Recorded()).Where(fact => fact.Kind == FactKind.StandbyExit).Select(fact => fact.At);
        Assert.Equal([At("2026-10-20 12:45"), At("2026-10-20 15:00")], exits);
        Assert.True(recorder.AwaitsInput);
    }

    [Fact]
    public async Task A_resume_into_a_locked_session_waits_for_the_unlock_not_for_input()
    {
        var recorder = await StartedAsync();
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Locked, At("2026-10-20 12:00")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 12:01")));
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Resumed, At("2026-10-20 12:40")));

        Assert.False(recorder.AwaitsInput);
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Unlocked, At("2026-10-20 12:41")));
        Assert.Contains(await Recorded(), fact => fact.Kind == FactKind.Unlock && fact.At == At("2026-10-20 12:41"));
    }

    [Fact]
    public async Task A_heartbeat_extends_coverage_and_one_after_a_gap_imports_history()
    {
        var source = new FakeHistorySource([]);
        history = source;
        var recorder = await StartedAsync();

        clock.Now = clock.Now.AddMinutes(1);
        await recorder.HeartbeatAsync();
        Assert.Empty(source.CursorsSeen);

        clock.Now = clock.Now.AddMinutes(30);
        await recorder.HeartbeatAsync();

        Assert.Equal(["Begin 08:09:35", "Extend 08:11:00", "Extend 08:41:00"], coverage.Calls);
        Assert.Single(source.CursorsSeen);
    }

    [Fact]
    public async Task A_heartbeat_while_a_suspend_is_pending_is_the_missed_resume()
    {
        var recorder = await StartedAsync();
        await recorder.OnSignalAsync(new LiveSignal(LiveSignalKind.Suspending, At("2026-10-20 12:00")));

        clock.Now = At("2026-10-20 12:00").AddSeconds(3);
        await recorder.HeartbeatAsync();
        Assert.DoesNotContain(await Recorded(), fact => fact.Kind == FactKind.StandbyExit);

        clock.Now = At("2026-10-20 13:00");
        await recorder.HeartbeatAsync();

        Assert.Equal("Resume 12:00:00-13:00:00", coverage.Calls[^1]);
        Assert.Contains(await Recorded(), fact => fact.Kind == FactKind.StandbyExit && fact.At == At("2026-10-20 13:00"));
    }

    [Fact]
    public async Task A_changed_idle_lock_timeout_is_recorded_once_at_the_next_heartbeat()
    {
        var recorder = await StartedAsync();
        desktop.Timers = new IdleTimers(DisplayOff: TimeSpan.FromMinutes(3));

        clock.Now = clock.Now.AddMinutes(1);
        await recorder.HeartbeatAsync();
        clock.Now = clock.Now.AddMinutes(1);
        await recorder.HeartbeatAsync();

        var timeouts = (await Recorded()).Where(fact => fact.Kind == FactKind.IdleTimeout).ToList();
        Assert.Equal([TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(3)], timeouts.Select(fact => fact.Detail.Timer!.Value));
    }

    [Theory]
    [InlineData(LiveSignalKind.SigningOut, FactKind.SignOut, EndCause.SignOut)]
    [InlineData(LiveSignalKind.ShuttingDown, FactKind.Shutdown, EndCause.Shutdown)]
    public async Task The_end_of_the_session_is_recorded_with_its_cause_and_coverage_runs_up_to_it(LiveSignalKind signal, FactKind kind, EndCause cause)
    {
        var recorder = await StartedAsync();

        await recorder.OnSignalAsync(new LiveSignal(signal, At("2026-10-20 17:00")));

        Assert.Equal(cause, (await Recorded()).Single(fact => fact.Kind == kind).Detail.Cause);
        Assert.Equal("Extend 17:00:00", coverage.Calls[^1]);
    }

    private async Task<LiveRecorder> StartedAsync()
    {
        var recorder = NewRecorder();
        await recorder.StartAsync();
        return recorder;
    }

    private LiveRecorder NewRecorder() => new(
        journal,
        coverage,
        new HistoryImport(history, journal, new InMemoryCursorStore()),
        Me,
        desktop,
        errors,
        clock);

    private Task<IReadOnlyList<ActivityFact>> Recorded() => journal.ReadAsync(Interval.All);

    private sealed class CallLog : ICoverageLog
    {
        public List<string> Calls { get; } = [];

        public Task BeginAsync(DateTimeOffset signedInAt) => Note($"Begin {Time(signedInAt)}");

        public Task ExtendAsync(DateTimeOffset now) => Note($"Extend {Time(now)}");

        public Task ResumeAsync(DateTimeOffset suspendedAt, DateTimeOffset resumedAt) =>
            Note($"Resume {Time(suspendedAt)}-{Time(resumedAt)}");

        public Task<IReadOnlyList<CoverageSpan>> ReadAsync(Interval range) => throw new NotSupportedException();

        private static string Time(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(2)).ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

        private Task Note(string call)
        {
            Calls.Add(call);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHistorySource : IHistorySource
    {
        public Task<HistoryBatch> ReadSinceAsync(HistoryCursor cursor, UserSid user) =>
            throw new UnauthorizedAccessException("The System log could not be read.");
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

internal sealed class ListErrorLog : IErrorLog
{
    public List<(string Context, Exception Error)> Entries { get; } = [];

    public void Write(string context, Exception error) => Entries.Add((context, error));
}
