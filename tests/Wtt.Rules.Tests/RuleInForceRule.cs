using System.Globalization;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using Wtt.Infrastructure.Storage;

namespace Wtt.Rules.Tests;

public sealed class RuleInForceRule
{
    private static readonly TimeRange Window = new(new TimeOnly(12, 0), new TimeOnly(13, 30));
    private static readonly TimeRange MistypedWindow = new(new TimeOnly(12, 0), new TimeOnly(12, 30));

    [Fact]
    public async Task BR_009_rule_in_force()
    {
        var rule = new RuleScenarios();
        using var file = new TempDatabase();
        var clock = new FakeClock(At("2026-10-12 08:00"));
        var rules = new SqliteLunchRules(file.Open(clock), clock);
        var prefilled = Describe(LunchRule.Prefilled);

        await rule.CheckAsync("until the first save every day, before and after install, uses the prefilled rule", async () =>
        {
            foreach (var date in new[] { "2026-09-21", "2026-10-11", "2026-10-12", "2026-10-19", "2027-01-04" })
            {
                Assert.Equal((date, prefilled), (date, await InForceAsync(rules, date)));
            }
        });

        clock.Now = At("2026-10-19 09:00");
        await rules.SaveAsync(MistypedWindow, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(30))]);
        clock.Now = At("2026-10-19 09:01");
        var first = Describe(await rules.SaveAsync(Window, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(30))]));

        await rule.CheckAsync("after the first save the days before install use the last rule saved on the first save's day", async () =>
        {
            Assert.Equal(first, await InForceAsync(rules, "2026-09-21"));
            Assert.Equal(first, await InForceAsync(rules, "2026-10-11"));
        });

        await rule.CheckAsync("the days from install to the first save's day keep the prefilled rule", async () =>
        {
            Assert.Equal(prefilled, await InForceAsync(rules, "2026-10-12"));
            Assert.Equal(prefilled, await InForceAsync(rules, "2026-10-18"));
        });

        await rule.CheckAsync("the first saved rule applies from its own date", async () =>
        {
            Assert.Equal(first, await InForceAsync(rules, "2026-10-19"));
            Assert.Equal(first, await InForceAsync(rules, "2027-01-04"));
        });

        clock.Now = At("2026-10-21 10:00");
        var second = Describe(await rules.SaveAsync(Window, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(45))]));

        await rule.CheckAsync("each saved rule applies from its own date until the next", async () =>
        {
            Assert.Equal(first, await InForceAsync(rules, "2026-10-19"));
            Assert.Equal(first, await InForceAsync(rules, "2026-10-20"));
            Assert.Equal(second, await InForceAsync(rules, "2026-10-21"));
            Assert.Equal(second, await InForceAsync(rules, "2026-10-25"));
        });

        await rule.CheckAsync("a later save changes neither the days before install nor those keeping the prefilled rule", async () =>
        {
            Assert.Equal(first, await InForceAsync(rules, "2026-09-21"));
            Assert.Equal(prefilled, await InForceAsync(rules, "2026-10-15"));
        });

        rule.AssertAllHold();
    }

    private static DateTimeOffset At(string localTime) =>
        new(DateTime.ParseExact(localTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), TimeSpan.FromHours(2));

    private static async Task<string> InForceAsync(SqliteLunchRules rules, string date) =>
        Describe(await rules.InForceOnAsync(DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)));

    private static string Describe(LunchRule rule) =>
        $"{rule.Window.Start:HH\\:mm}–{rule.Window.End:HH\\:mm} " + string.Join(", ", rule.Tiers.Select(tier => $"over {tier.Over:h\\:mm} → {tier.Minimum:h\\:mm}"));
}
