using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class EveningPauseRule
{
    [Fact]
    public void BR_012_evening_pause_ends_the_day_where_it_starts()
    {
        var rule = new RuleScenarios();

        rule.Check("a last pause of 5:10 ends the day where it starts; the evening use adds only its own minutes", () =>
        {
            var day = EveningVisit("22:40", "22:42");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 17:30"), workday.End);
            Assert.Equal(Hm("0:02"), workday.EveningUse);
            Assert.Equal(Hm("9:32"), workday.Attendance);
            Assert.Equal(Hm("1:00"), workday.Break);
            Assert.Equal(Hm("8:32"), workday.Total);
        });

        rule.Check("a last pause of exactly 2:00 is an evening pause", () =>
        {
            var day = EveningVisit("19:30", "19:32");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 17:30"), workday.End);
            Assert.Equal(Hm("8:32"), workday.Total);
        });

        rule.Check("a last pause of 1:59 changes nothing", () =>
        {
            var day = EveningVisit("19:29", "19:31");
            var workday = day.Day("2026-10-20", Example);
            Assert.Null(workday.EveningPause);
            Assert.Equal(day.At("2026-10-20 19:31"), workday.End);
            Assert.Equal(Hm("10:31"), workday.Total);
        });

        rule.Check("only the day's last pause can be the evening pause; an earlier long pause stays work", () =>
        {
            var day = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 12:00")
                .Use("2026-10-20 12:30", "2026-10-20 15:00")
                .Use("2026-10-20 18:00", "2026-10-20 19:00")
                .Use("2026-10-20 22:00", "2026-10-20 22:10");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 19:00"), workday.End);
            Assert.Equal(Hm("0:10"), workday.EveningUse);
            Assert.Equal(Hm("11:10"), workday.Attendance);
        });

        rule.Check("a long last pause that is the lunch pause is no evening pause", () =>
        {
            var day = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 12:15")
                .Use("2026-10-20 15:00", "2026-10-20 17:00");
            var workday = day.Day("2026-10-20", Example);
            Assert.Null(workday.EveningPause);
            Assert.Equal(day.At("2026-10-20 17:00"), workday.End);
            Assert.Equal(Hm("2:45"), workday.Break);
            Assert.Equal(Hm("6:15"), workday.Total);
        });

        rule.Check("a long last pause that is the night pause is no evening pause", () =>
        {
            var day = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 00:30")
                .Use("2026-10-20 08:00", "2026-10-20 11:00");
            var tuesday = day.Day("2026-10-20", Example);
            Assert.NotNull(tuesday.NightPause);
            Assert.Null(tuesday.EveningPause);
            Assert.Equal(day.At("2026-10-20 11:00"), tuesday.End);
            Assert.Equal(Hm("3:30"), tuesday.Total);
        });

        rule.Check("a long last pause ending before the lunch window opens is a morning pause and stays work", () =>
        {
            var day = new Scenario()
                .Use("2026-10-20 05:00", "2026-10-20 05:05")
                .Use("2026-10-20 08:00", "2026-10-20 11:00");
            var workday = day.Day("2026-10-20", Example);
            Assert.Null(workday.EveningPause);
            Assert.Equal(day.At("2026-10-20 05:00"), workday.Start);
            Assert.Equal(day.At("2026-10-20 11:00"), workday.End);
            Assert.Equal(Hm("6:00"), workday.Attendance);
        });

        rule.Check("evening use running past midnight counts to 24:00 and the next day starts at 00:00", () =>
        {
            var days = new Scenario()
                .Use("2026-10-19 08:00", "2026-10-19 12:00")
                .Use("2026-10-19 12:30", "2026-10-19 17:00")
                .Use("2026-10-19 23:30", "2026-10-20 00:20")
                .Use("2026-10-20 08:00", "2026-10-20 12:00")
                .Use("2026-10-20 12:30", "2026-10-20 16:00");
            var monday = days.Day("2026-10-19", Example);
            var tuesday = days.Day("2026-10-20", Example);
            Assert.Equal(days.At("2026-10-19 17:00"), monday.End);
            Assert.Equal(Hm("0:30"), monday.EveningUse);
            Assert.Equal(Hm("9:00"), monday.Total);
            Assert.Equal(days.At("2026-10-20 00:00"), tuesday.Start);
            Assert.Equal(days.At("2026-10-20 08:00"), tuesday.NightPause?.End);
            Assert.Equal(Hm("7:50"), tuesday.Total);
        });

        rule.Check("the evening minutes count toward the tier", () =>
        {
            var day = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 15:00")
                .Use("2026-10-20 20:00", "2026-10-20 20:20");
            var workday = day.Day("2026-10-20", Example);
            Assert.Equal(day.At("2026-10-20 15:00"), workday.End);
            Assert.Equal(Hm("0:20"), workday.Break);
            Assert.Equal(Hm("7:00"), workday.Total);
        });

        rule.AssertAllHold();
    }

    /// <summary>08:00–12:00 and 13:00–17:30, then an evening visit: an unlock and a shutdown.</summary>
    private static Scenario EveningVisit(string from, string to) => new Scenario()
        .Use("2026-10-20 08:00", "2026-10-20 12:00")
        .Use("2026-10-20 13:00", "2026-10-20 17:30")
        .Unlock($"2026-10-20 {from}")
        .Shutdown($"2026-10-20 {to}");
}
