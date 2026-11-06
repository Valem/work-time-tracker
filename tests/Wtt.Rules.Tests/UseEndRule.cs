using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Domain.Presence;
using Wtt.Domain.Time;
using static Wtt.Application.Recording.LiveSignalKind;
using static Wtt.Rules.Tests.Support.SyntheticEvent;
using static Wtt.Rules.Tests.Support.Users;

namespace Wtt.Rules.Tests;

public sealed class UseEndRule
{
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static readonly (FactKind Kind, EndCause Cause)[] TimerEnds =
    [
        (FactKind.Lock, EndCause.IdleLock),
        (FactKind.Lock, EndCause.ScreenSaverLock),
        (FactKind.Lock, EndCause.MachineInactivityLimit),
        (FactKind.StandbyEnter, EndCause.DisplayOffStandby),
    ];

    private static readonly (FactKind Kind, EndCause Cause)[] OwnTimeEnds =
    [
        (FactKind.Lock, EndCause.UserLock),
        (FactKind.Lock, EndCause.Lid),
        (FactKind.StandbyEnter, EndCause.Lid),
        (FactKind.StandbyEnter, EndCause.PowerButton),
        (FactKind.SignOut, EndCause.SignOut),
        (FactKind.Shutdown, EndCause.Shutdown),
        (FactKind.Lock, EndCause.Unknown),
        (FactKind.StandbyEnter, EndCause.Unknown),
    ];

    private static readonly FactKind[] EndKinds = [FactKind.Lock, FactKind.StandbyEnter, FactKind.SignOut, FactKind.Shutdown];

    [Fact]
    public async Task BR_003_a_use_span_ends_at_a_lock_standby_sign_out_or_shutdown()
    {
        var rule = new RuleScenarios();

        await rule.CheckAsync("each live end fact stores its cause", EachLiveEndStoresItsCauseAsync);

        foreach (var (kind, cause) in TimerEnds)
        {
            rule.Check($"live {kind} by {cause}: input after the event's time minus the timer — ends at that last input", () =>
                AssertFirstUseEnds(LiveEnd(kind, cause, TenMinutes, lastInput: "2026-10-20 12:03"), "2026-10-20 12:03"));

            rule.Check($"live {kind} by {cause}: no input since long before — ends at the event's time minus the timer", () =>
                AssertFirstUseEnds(LiveEnd(kind, cause, TenMinutes, lastInput: "2026-10-20 11:00"), "2026-10-20 11:58"));
        }

        foreach (var (kind, cause) in OwnTimeEnds)
        {
            var timer = cause == EndCause.Unknown ? TenMinutes : (TimeSpan?)null;
            rule.Check($"live {kind} by {cause} ends at its own time", () =>
                AssertFirstUseEnds(LiveEnd(kind, cause, timer, lastInput: "2026-10-20 11:00"), "2026-10-20 12:08"));
        }

        foreach (var kind in EndKinds)
        {
            rule.Check($"a history {kind} ends use at its own time, marked approximate", () =>
            {
                var day = new Scenario()
                    .Unlock("2026-10-20 08:00")
                    .Ends(kind, "2026-10-20 12:00", kind == FactKind.Lock ? FactSource.WinlogonHistory : FactSource.SystemHistory)
                    .Use("2026-10-20 13:00", "2026-10-20 17:00");
                AssertFirstUseEnds(day, "2026-10-20 12:00", Precision.Rebuilt);
            });
        }

        rule.Check("a history lock naming an idle timer still ends use at its own time, marked approximate", () => AssertFirstUseEnds(
            new Scenario()
                .Unlock("2026-10-20 08:00")
                .Lock("2026-10-20 12:08", FactSource.WinlogonHistory, EndCause.IdleLock, TenMinutes)
                .Use("2026-10-20 13:00", "2026-10-20 17:00"),
            "2026-10-20 12:08",
            Precision.Rebuilt));

        rule.Check("a boot finding use open ends it at the stop time Windows logs for the unexpected shutdown (6008)", () =>
        {
            var facts = History.Read(
                SignIn(1, "2026-10-20T05:59:00Z", Me, 1),
                Winlogon811(1, "2026-10-20T10:00:00Z", Me, History.SensLock),
                Winlogon811(2, "2026-10-20T10:45:00Z", Me, History.SensUnlock),
                Boot(2, "2026-10-20T13:05:00Z"),
                UnexpectedShutdown(3, "2026-10-20T13:05:20Z", new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero)),
                SignIn(4, "2026-10-20T13:07:00Z", Me, 1),
                Winlogon811(3, "2026-10-20T15:00:00Z", Me, History.SensLock));
            Expect.Uses(
                new Scenario().With(facts),
                ("2026-10-20 07:59", "2026-10-20 12:00"),
                ("2026-10-20 12:45", "2026-10-20 15:00"),
                ("2026-10-20 15:07", "2026-10-20 17:00"));
        });

        rule.Check("without a logged stop time, a boot finding use open ends it at the last fact before the boot", () => Expect.Uses(
            new Scenario()
                .Unlock("2026-10-20 09:30")
                .StandbyExit("2026-10-20 11:00", "Unmapped:KernelPower 53")
                .Boot("2026-10-20 11:20")
                .SignIn("2026-10-20 11:22")
                .Lock("2026-10-20 12:00"),
            ("2026-10-20 09:30", "2026-10-20 11:00"),
            ("2026-10-20 11:22", "2026-10-20 12:00")));

        rule.Check("idle time without a lock stays use", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-20 07:59", "2026-10-20 17:30")
                .Unlock("2026-10-20 08:00", FactSource.Live)
                .LastInput("2026-10-20 08:30")
                .IdleTimeout("2026-10-20 10:00")
                .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock),
            ("2026-10-20 08:00", "2026-10-20 17:00")));

        rule.AssertAllHold();
    }

    /// <summary>The Recorder on a scripted day with each kind of end; every end fact it writes names its cause.</summary>
    private static async Task EachLiveEndStoresItsCauseAsync()
    {
        using var rig = new RecorderRig("2026-10-20 07:59:30");
        rig.Desktop.Timers = new IdleTimers(DisplayOff: TimeSpan.FromMinutes(5));
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-20 07:59:00");
        await rig.InputAsync("2026-10-20 09:59:58");
        await rig.SignalAsync(Locked, "2026-10-20 10:00:00");
        await rig.SignalAsync(Unlocked, "2026-10-20 10:10:00");
        await rig.InputAsync("2026-10-20 10:20:00");
        await rig.SignalAsync(Locked, "2026-10-20 10:25:00");
        await rig.SignalAsync(Unlocked, "2026-10-20 10:30:00");
        await rig.InputAsync("2026-10-20 10:40:00");
        await rig.SignalAsync(LidClosed, "2026-10-20 10:45:00");
        await rig.SignalAsync(Suspending, "2026-10-20 10:45:02");
        rig.TimePassesUnseen("2026-10-20 11:00:00");
        await rig.SignalAsync(Resumed, "2026-10-20 11:00:00");
        await rig.InputAsync("2026-10-20 11:10:00");
        await rig.SignalAsync(Suspending, "2026-10-20 11:15:00");
        rig.TimePassesUnseen("2026-10-20 11:30:00");
        await rig.SignalAsync(Resumed, "2026-10-20 11:30:00");
        await rig.InputAsync("2026-10-20 11:31:00");
        await rig.SignalAsync(Suspending, "2026-10-20 11:32:00");
        rig.TimePassesUnseen("2026-10-20 12:00:00");
        await rig.SignalAsync(Resumed, "2026-10-20 12:00:00");
        await rig.InputAsync("2026-10-20 12:01:00");
        await rig.SignalAsync(SigningOut, "2026-10-20 12:30:00");
        rig.TimePassesUnseen("2026-10-20 12:40:30");
        await rig.StartRecorderAsync("2", signedInAt: "2026-10-20 12:40:00");
        await rig.InputAsync("2026-10-20 16:59:00");
        await rig.SignalAsync(ShuttingDown, "2026-10-20 17:00:00");

        var ends = (await rig.Journal.ReadAsync(Interval.All))
            .Where(fact => fact.IsLive && EndKinds.Contains(fact.Kind))
            .Select(fact => $"{fact.At:HH:mm:ss} {fact.Kind} {fact.Detail.Cause?.ToString() ?? "no cause"}{(fact.Detail.Timer is { } timer ? $" {timer.TotalMinutes} min" : string.Empty)}");

        Assert.Equal(
            [
                "10:00:00 Lock UserLock",
                "10:25:00 Lock IdleLock 5 min",
                "10:45:02 StandbyEnter Lid",
                "11:15:00 StandbyEnter DisplayOffStandby 5 min",
                "11:32:00 StandbyEnter Unknown",
                "12:30:00 SignOut SignOut",
                "17:00:00 Shutdown Shutdown",
            ],
            ends);
    }

    /// <summary>Recorded use from 08:00, the last real input, the given end at 12:08, then 13:00–17:00.</summary>
    private static Scenario LiveEnd(FactKind kind, EndCause cause, TimeSpan? timer, string lastInput) => new Scenario()
        .Covered("2026-10-20 07:59", "2026-10-20 18:00")
        .Unlock("2026-10-20 08:00", FactSource.Live)
        .LastInput(lastInput)
        .Ends(kind, "2026-10-20 12:08", FactSource.Live, cause, timer)
        .Unlock("2026-10-20 13:00", FactSource.Live)
        .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

    private static void AssertFirstUseEnds(Scenario day, string end, Precision precision = Precision.Exact)
    {
        var first = day.Presence().Uses[0];
        Assert.Equal(day.At(end), first.End);
        Assert.Equal(precision, first.Precision);
    }
}
