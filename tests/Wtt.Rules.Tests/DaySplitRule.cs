using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class DaySplitRule
{
    [Fact]
    public void BR_005_day_and_midnight_split()
    {
        var rule = new RuleScenarios();

        rule.Check("start is the first use and end the last use of the local date", () =>
        {
            var day = new Scenario().Use("2026-10-20 07:45", "2026-10-20 12:00").Use("2026-10-20 12:30", "2026-10-20 16:10");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 07:45"), workday.Start);
            Assert.Equal(day.At("2026-10-20 16:10"), workday.End);
        });

        rule.Check("each fact's date is the local date at the UTC offset stored with it, whatever the zone later", () =>
        {
            var plusOne = TimeSpan.FromHours(1);
            var minusFive = TimeSpan.FromHours(-5);
            var journey = new Scenario()
                .WithOffset(plusOne)
                .Use("2026-11-03 00:30", "2026-11-03 04:00")
                .Use("2026-11-03 08:00", "2026-11-03 17:00")
                .WithOffset(minusFive)
                .Use("2026-11-04 08:00", "2026-11-04 17:00")
                .Use("2026-11-04 18:30", "2026-11-04 20:00");

            Assert.False(journey.Day("2026-11-02", Example).HasUse);
            var tuesday = journey.Day("2026-11-03", Example);
            Assert.Equal(Scenario.Local("2026-11-03 00:30", plusOne), tuesday.Start);
            Assert.Equal(Scenario.Local("2026-11-03 17:00", plusOne), tuesday.End);
            var wednesday = journey.Day("2026-11-04", Example);
            Assert.Equal(Scenario.Local("2026-11-04 08:00", minusFive), wednesday.Start);
            Assert.Equal(Scenario.Local("2026-11-04 20:00", minusFive), wednesday.End);
            Assert.False(journey.Day("2026-11-05", Example).HasUse);
        });

        rule.Check("the day ends where the evening pause starts", () =>
        {
            var day = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 12:00")
                .Use("2026-10-20 13:00", "2026-10-20 17:30")
                .Use("2026-10-20 22:40", "2026-10-20 22:42");
            Assert.Equal(day.At("2026-10-20 17:30"), day.Day("2026-10-20", Example).End);
        });

        rule.Check("a span crossing 00:00 is the day's last use up to 24:00 and continues the next day from 00:00", () =>
        {
            var days = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 00:40")
                .Use("2026-10-20 08:15", "2026-10-20 17:00");
            var monday = days.Day("2026-10-19", Example);
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-20 00:00"), monday.End);
            Assert.Equal(Hm("2:00"), monday.Attendance);
            Assert.Equal(days.At("2026-10-20 00:00"), tuesday.Start);
            Assert.Equal(days.At("2026-10-20 00:40"), tuesday.Blocks[0].End);
        });

        rule.Check("everything between start and end is work except the lunch: a coffee pause counts", () =>
        {
            var workday = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 10:00")
                .Use("2026-10-20 10:20", "2026-10-20 12:15")
                .Use("2026-10-20 12:45", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("9:00"), workday.Attendance);
            Assert.Equal(Hm("0:30"), workday.Break);
            Assert.Equal(Hm("8:30"), workday.Total);
        });

        rule.Check("everything between start and end is work except the night pause and the lunch", () =>
        {
            var tuesday = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 00:40")
                .Use("2026-10-20 08:15", "2026-10-20 10:00")
                .Use("2026-10-20 10:20", "2026-10-20 12:00")
                .Use("2026-10-20 12:45", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("9:25"), tuesday.Attendance);
            Assert.Equal(Hm("0:45"), tuesday.Break);
            Assert.Equal(Hm("8:40"), tuesday.Total);
        });

        rule.AssertAllHold();
    }
}
