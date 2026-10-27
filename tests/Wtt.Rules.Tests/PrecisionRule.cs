using Wtt.Domain.Facts;
using Wtt.Domain.Workdays;
using static Wtt.Application.Recording.LiveSignalKind;
using static Wtt.Rules.Tests.Support.RecorderRig;
using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class PrecisionRule
{
    [Fact]
    public async Task BR_011_history_outside_coverage_marks_the_day_rebuilt()
    {
        var rule = new RuleScenarios();

        await rule.CheckAsync("a day the Recorder covered whole is not rebuilt", async () =>
        {
            using var rig = await LockedOvernightAsync();
            await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
            await rig.InputAsync("2026-10-20 16:59:59");
            await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

            Assert.False((await rig.DayAsync("2026-10-20")).IsRebuilt);
        });

        await rule.CheckAsync("a day whose use comes partly from history filling a recording gap is rebuilt", async () =>
        {
            using var rig = await LockedOvernightAsync();
            await rig.SignalAsync(Unlocked, "2026-10-20 08:00:00");
            await rig.RunUntilAsync("2026-10-20 10:00:00");
            rig.TimePassesUnseen("2026-10-20 10:30:00");
            rig.WindowsLogs(FactKind.Lock, "2026-10-20 10:10:00");
            rig.WindowsLogs(FactKind.Unlock, "2026-10-20 10:20:00");
            await rig.StartRecorderAsync("1", signedInAt: "2026-10-19 17:59:00");
            await rig.InputAsync("2026-10-20 16:59:59");
            await rig.SignalAsync(Locked, "2026-10-20 17:00:00");

            var day = await rig.DayAsync("2026-10-20");
            Assert.Contains(day.Pauses, pause => (pause.Start, pause.End) == (At("2026-10-20 10:10:00"), At("2026-10-20 10:20:00")));
            Assert.True(day.IsRebuilt);
        });

        await rule.CheckAsync("a Recorder starting exactly 5:00 after the sign-in: the stretch from sign-in to its start is filled from history unmarked", async () =>
        {
            var day = await SignedInAt0810WithHistoryLockAsync(recorderStarts: "2026-10-20 08:15:00");
            Assert.Equal(At("2026-10-20 08:10:00"), day.Start);
            Assert.Contains(day.Pauses, pause => (pause.Start, pause.End) == (At("2026-10-20 08:11:00"), At("2026-10-20 08:12:00")));
            Assert.False(day.IsRebuilt);
        });

        await rule.CheckAsync("a Recorder starting 5:01 after the sign-in: that stretch's use comes from history outside coverage and the day is rebuilt", async () =>
        {
            var day = await SignedInAt0810WithHistoryLockAsync(recorderStarts: "2026-10-20 08:15:01");
            Assert.Contains(day.Pauses, pause => (pause.Start, pause.End) == (At("2026-10-20 08:11:00"), At("2026-10-20 08:12:00")));
            Assert.True(day.IsRebuilt);
        });

        rule.Check("history outside coverage that adds no use marks nothing", () =>
        {
            var day = new Scenario()
                .Covered("2026-10-20 07:59", "2026-10-20 12:01")
                .SignIn("2026-10-20 07:59", FactSource.Live)
                .Unlock("2026-10-20 08:00", FactSource.Live)
                .Lock("2026-10-20 12:00", FactSource.Live, EndCause.UserLock)
                .StandbyEnter("2026-10-20 12:05")
                .WindowsWake("2026-10-20 12:30", "2026-10-20 12:31")
                .Covered("2026-10-20 13:00", "2026-10-20 17:30")
                .Unlock("2026-10-20 13:00", FactSource.Live)
                .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock);

            var workday = day.Day("2026-10-20", Example);
            Assert.Equal([day.At("2026-10-20 12:30")], Assert.Single(workday.Pauses).IgnoredWakes);
            Assert.False(workday.IsRebuilt);
        });

        rule.AssertAllHold();
    }

    /// <summary>Signed in at 08:10, locked 08:11–08:12 as only Windows recorded it, the Recorder started then, used until 17:00.</summary>
    private static async Task<Workday> SignedInAt0810WithHistoryLockAsync(string recorderStarts)
    {
        using var rig = new RecorderRig("2026-10-20 08:09:00");
        rig.TimePassesUnseen(recorderStarts);
        rig.WindowsLogs(FactKind.SignIn, "2026-10-20 08:10:00", FactSource.SystemHistory);
        rig.WindowsLogs(FactKind.Lock, "2026-10-20 08:11:00");
        rig.WindowsLogs(FactKind.Unlock, "2026-10-20 08:12:00");
        await rig.StartRecorderAsync("2", signedInAt: "2026-10-20 08:10:00");
        await rig.InputAsync("2026-10-20 16:59:59");
        await rig.SignalAsync(Locked, "2026-10-20 17:00:00");
        return await rig.DayAsync("2026-10-20");
    }

    /// <summary>Signed in the evening before, locked and in standby overnight with the Recorder running.</summary>
    private static async Task<RecorderRig> LockedOvernightAsync()
    {
        var rig = new RecorderRig("2026-10-19 17:59:30");
        rig.Desktop.LastInputAt = At("2026-10-19 17:59:20");
        await rig.StartRecorderAsync("1", signedInAt: "2026-10-19 17:59:00");
        await rig.InputAsync("2026-10-19 18:04:59");
        await rig.SignalAsync(Locked, "2026-10-19 18:05:00");
        await rig.SignalAsync(Suspending, "2026-10-19 18:06:00");
        rig.TimePassesUnseen("2026-10-20 07:59:50");
        await rig.SignalAsync(Resumed, "2026-10-20 07:59:50");
        return rig;
    }
}
