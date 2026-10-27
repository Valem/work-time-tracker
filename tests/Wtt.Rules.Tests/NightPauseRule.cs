using Wtt.Domain.Presence;
using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class NightPauseRule
{
    [Fact]
    public void BR_006_night_pause_only_after_use_carried_over_midnight()
    {
        var rule = new RuleScenarios();

        rule.Check("a span crossing 00:00 carries use over midnight: the day starts at 00:00 and the longest pause before the window is not work", () =>
        {
            var days = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 00:40")
                .WindowsWake("2026-10-20 03:10", "2026-10-20 03:11")
                .Use("2026-10-20 08:15", "2026-10-20 12:00")
                .Use("2026-10-20 12:45", "2026-10-20 17:00");
            Assert.Equal(days.At("2026-10-20 00:00"), days.Day("2026-10-19", Example).End);
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-20 00:00"), tuesday.Start);
            AssertPause(days, tuesday.NightPause, "2026-10-20 00:40", "2026-10-20 08:15");
            Assert.Equal(Hm("8:40"), tuesday.Total);
        });

        rule.Check("use resuming 20 minutes after the previous day's last use: the pause around 00:00 is work on both days", () =>
        {
            var days = new Scenario()
                .Use("2026-10-21 21:00", "2026-10-21 23:50")
                .Use("2026-10-22 00:10", "2026-10-22 00:40")
                .Use("2026-10-22 08:15", "2026-10-22 12:00")
                .Use("2026-10-22 12:45", "2026-10-22 17:00");
            var wednesday = days.Day("2026-10-21", Example);
            var thursday = days.Day("2026-10-22", Example);
            Assert.Equal(days.At("2026-10-22 00:00"), wednesday.End);
            Assert.Equal(Hm("3:00"), wednesday.Total);
            Assert.Equal(days.At("2026-10-22 00:00"), thursday.Start);
            AssertPause(days, thursday.NightPause, "2026-10-22 00:40", "2026-10-22 08:15");
            Assert.Equal(Hm("8:40"), thursday.Total);
        });

        rule.Check("use resuming 59 minutes after the previous day's last use carries over", () =>
        {
            var days = ResumingAfterMidnight("00:29");
            Assert.Equal(days.At("2026-10-20 00:00"), days.Day("2026-10-19", Example).End);
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-20 00:00"), tuesday.Start);
            Assert.NotNull(tuesday.NightPause);
        });

        rule.Check("use resuming exactly 60 minutes after the previous day's last use does not carry over: the first use starts the day", () =>
        {
            var days = ResumingAfterMidnight("00:30");
            Assert.Equal(days.At("2026-10-19 23:30"), days.Day("2026-10-19", Example).End);
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-20 00:30"), tuesday.Start);
            Assert.Null(tuesday.NightPause);
        });

        rule.Check("of two pauses after 00:00 ending before the window opens, the longest is the night pause and the other is work", () =>
        {
            var days = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 01:00")
                .Use("2026-10-20 01:30", "2026-10-20 02:00")
                .Use("2026-10-20 06:00", "2026-10-20 11:30")
                .Use("2026-10-20 12:30", "2026-10-20 16:00");
            var tuesday = days.Day("2026-10-20", Example);
            AssertPause(days, tuesday.NightPause, "2026-10-20 02:00", "2026-10-20 06:00");
            Assert.Equal(Hm("12:00"), tuesday.Attendance);
            Assert.Equal(Hm("11:00"), tuesday.Total);
        });

        rule.Check("when no pause ends before the window opens, the longest that starts before it is the night pause", () =>
        {
            var days = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 00:40")
                .Use("2026-10-20 12:10", "2026-10-20 12:40")
                .Use("2026-10-20 13:10", "2026-10-20 17:00");
            var tuesday = days.Day("2026-10-20", Example);
            AssertPause(days, tuesday.NightPause, "2026-10-20 00:40", "2026-10-20 12:10");
            Assert.Equal(Hm("0:30"), tuesday.Break);
            Assert.Equal(Hm("5:00"), tuesday.Total);
        });

        rule.Check("on a day without carried-over use the first use starts the day and the last use ends it, with no night pause", () =>
        {
            var days = new Scenario()
                .Use("2026-10-19 08:00", "2026-10-19 23:00")
                .Use("2026-10-20 05:00", "2026-10-20 05:05")
                .Use("2026-10-20 08:00", "2026-10-20 12:15")
                .Use("2026-10-20 12:45", "2026-10-20 17:00");
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-20 05:00"), tuesday.Start);
            Assert.Equal(days.At("2026-10-20 17:00"), tuesday.End);
            Assert.Null(tuesday.NightPause);
        });

        rule.Check("on a day without carried-over use the evening pause ends the day", () =>
        {
            var days = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 12:00")
                .Use("2026-10-20 13:00", "2026-10-20 17:30")
                .Use("2026-10-20 22:40", "2026-10-20 22:42");
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-20 08:00"), tuesday.Start);
            Assert.Equal(days.At("2026-10-20 17:30"), tuesday.End);
            Assert.Null(tuesday.NightPause);
        });

        rule.AssertAllHold();
    }

    /// <summary>Monday 15:00–23:30, Tuesday from the given time to 01:00, then 08:00–17:00.</summary>
    private static Scenario ResumingAfterMidnight(string resume) => new Scenario()
        .Use("2026-10-19 15:00", "2026-10-19 23:30")
        .Use($"2026-10-20 {resume}", "2026-10-20 01:00")
        .Use("2026-10-20 08:00", "2026-10-20 17:00");

    private static void AssertPause(Scenario scenario, Pause? pause, string start, string end)
    {
        Assert.NotNull(pause);
        Assert.Equal((scenario.At(start), scenario.At(end)), (pause.Start, pause.End));
    }
}
