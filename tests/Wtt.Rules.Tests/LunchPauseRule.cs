using Wtt.Domain.Workdays;
using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class LunchPauseRule
{
    [Fact]
    public void BR_007_the_longest_pause_touching_the_lunch_window_is_deducted_whole()
    {
        var rule = new RuleScenarios();

        rule.Check("a lunch with a lock, standby and a Windows wake inside is one pause, deducted whole", () =>
        {
            var day = new Scenario()
                .Unlock("2026-10-20 08:00")
                .Lock("2026-10-20 12:00")
                .StandbyEnter("2026-10-20 12:05")
                .WindowsWake("2026-10-20 12:30", "2026-10-20 12:32")
                .Unlock("2026-10-20 13:00")
                .Lock("2026-10-20 17:00");
            var workday = day.Day("2026-10-20", Example);
            var pause = Assert.Single(workday.Pauses);
            Assert.Equal(Hm("1:00"), pause.Duration);
            Assert.Equal([day.At("2026-10-20 12:30")], pause.IgnoredWakes);
            Assert.Equal(Hm("1:00"), workday.Break);
            Assert.Equal(Hm("8:00"), workday.Total);
        });

        rule.Check("a lunch with the laptop off inside is one pause, deducted whole", () =>
        {
            var workday = new Scenario()
                .Unlock("2026-10-20 08:00")
                .Lock("2026-10-20 12:00")
                .Shutdown("2026-10-20 12:01")
                .Boot("2026-10-20 12:50")
                .SignIn("2026-10-20 12:52")
                .Lock("2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("0:52"), Assert.Single(workday.Pauses).Duration);
            Assert.Equal(Hm("0:52"), workday.Break);
            Assert.Equal(Hm("8:08"), workday.Total);
        });

        rule.Check("of two pauses touching the window the longest is the lunch pause", () =>
        {
            var day = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 11:50")
                .Use("2026-10-20 12:10", "2026-10-20 12:30")
                .Use("2026-10-20 13:05", "2026-10-20 17:00");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 12:30"), workday.Lunch.Pause?.Start);
            Assert.Equal(Hm("0:35"), workday.Break);
            Assert.Equal(Hm("8:25"), workday.Total);
        });

        rule.Check("a pause crossing the window's opening touches it and is deducted whole", () =>
        {
            var workday = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 11:50")
                .Use("2026-10-20 12:40", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("0:50"), workday.Break);
            Assert.Equal(LunchBasis.PauseWhole, workday.Lunch.Basis);
            Assert.Equal(Hm("8:10"), workday.Total);
        });

        rule.Check("a pause ending exactly when the window opens touches it", () =>
        {
            var workday = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 11:15")
                .Use("2026-10-20 12:00", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("0:45"), workday.Break);
            Assert.Equal(LunchBasis.PauseWhole, workday.Lunch.Basis);
        });

        rule.Check("a pause starting exactly when the window closes touches it", () =>
        {
            var workday = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 13:30")
                .Use("2026-10-20 14:10", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("0:40"), workday.Break);
            Assert.Equal(LunchBasis.PauseWhole, workday.Lunch.Basis);
        });

        rule.Check("the night pause is never the lunch pause, even when it reaches into the window", () =>
        {
            var day = new Scenario()
                .Use("2026-10-19 23:30", "2026-10-20 00:30")
                .Use("2026-10-20 12:10", "2026-10-20 13:00")
                .Use("2026-10-20 13:30", "2026-10-20 17:00");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 12:10"), workday.NightPause?.End);
            Assert.Equal(day.At("2026-10-20 13:00"), workday.Lunch.Pause?.Start);
            Assert.Equal(Hm("0:30"), workday.Break);
        });

        rule.Check("a lunch pause shorter than the tier minimum is raised to it", () =>
        {
            var workday = new Scenario()
                .Use("2026-10-20 05:00", "2026-10-20 05:05")
                .Use("2026-10-20 08:00", "2026-10-20 12:15")
                .Use("2026-10-20 12:45", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("1:00"), workday.Break);
            Assert.Equal(LunchBasis.TierMinimum, workday.Lunch.Basis);
            Assert.Equal(Hm("11:00"), workday.Total);
        });

        rule.AssertAllHold();
    }
}
