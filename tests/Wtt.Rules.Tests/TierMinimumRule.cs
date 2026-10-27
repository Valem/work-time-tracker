using System.Globalization;
using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class TierMinimumRule
{
    /// <summary>Use from 08:00 without a pause until the end; the break and total the rule gives.</summary>
    private static readonly (string End, string Break, string Total)[] WithoutPause =
    [
        ("13:30", "0:00", "5:30"),
        ("13:31", "0:01", "5:30"),
        ("13:45", "0:15", "5:30"),
        ("15:00", "0:15", "6:45"),
        ("15:15", "0:15", "7:00"),
        ("15:20", "0:20", "7:00"),
        ("15:31", "0:30", "7:01"),
        ("17:00", "0:30", "8:30"),
        ("17:45", "0:45", "9:00"),
        ("18:01", "1:00", "9:01"),
    ];

    [Fact]
    public void BR_008_tier_minimum_on_the_net_time()
    {
        var rule = new RuleScenarios();

        rule.Check("attendance = end − start − night pause + evening use", () =>
        {
            var tuesday = new Scenario()
                .Use("2026-10-19 22:00", "2026-10-20 00:40")
                .Use("2026-10-20 08:00", "2026-10-20 12:00")
                .Use("2026-10-20 13:00", "2026-10-20 17:00")
                .Use("2026-10-20 20:00", "2026-10-20 20:30")
                .Day("2026-10-20", Example);
            Assert.Equal(Hm("10:10"), tuesday.Attendance);
            Assert.Equal(Hm("1:00"), tuesday.Break);
            Assert.Equal(Hm("9:10"), tuesday.Total);
        });

        foreach (var (end, expectedBreak, expectedTotal) in WithoutPause)
        {
            rule.Check($"08:00–{end} without a pause: the smallest break whose tier on the net time is no larger, {expectedBreak}, leaves {expectedTotal}", () =>
            {
                var workday = new Scenario().Use("2026-10-20 08:00", $"2026-10-20 {end}").Day("2026-10-20", Example);
                Assert.Equal((Hm(expectedBreak), Hm(expectedTotal)), (workday.Break, workday.Total));
            });
        }

        rule.Check("the break is never below the lunch pause", () =>
        {
            var workday = new Scenario()
                .Use("2026-10-20 08:00", "2026-10-20 12:00")
                .Use("2026-10-20 13:10", "2026-10-20 17:00")
                .Day("2026-10-20", Example);
            Assert.Equal((Hm("1:10"), Hm("7:50")), (workday.Break, workday.Total));
        });

        rule.Check("with no pause, more attendance never lowers the total", () =>
            AssertTotalNeverDrops(end => new Scenario().Use("2026-10-20 08:00", end)));

        rule.Check("with the same 20-minute lunch, more attendance never lowers the total", () =>
            AssertTotalNeverDrops(end => new Scenario().Use("2026-10-20 08:00", "2026-10-20 12:00").Use("2026-10-20 12:20", end)));

        rule.AssertAllHold();
    }

    /// <summary>Ends the day one minute later at a time, 12:21 to 22:00, and requires the total never to fall.</summary>
    private static void AssertTotalNeverDrops(Func<string, Scenario> dayEndingAt)
    {
        var previous = TimeSpan.Zero;
        for (var end = new DateTime(2026, 10, 20, 12, 21, 0); end.Hour < 22; end = end.AddMinutes(1))
        {
            var total = dayEndingAt(end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Day("2026-10-20", Example).Total;
            Assert.True(total >= previous, $"Ending at {end:HH:mm} gave {total}, less than {previous} a minute earlier.");
            previous = total;
        }
    }
}
