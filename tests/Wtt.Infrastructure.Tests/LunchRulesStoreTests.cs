using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using Wtt.Infrastructure.Storage;

namespace Wtt.Infrastructure.Tests;

/// <summary>BR-009 through the data file: the install date is the file's creation, a save applies from its own date.</summary>
public sealed class LunchRulesStoreTests : IDisposable
{
    private static readonly TimeRange Window = new(new TimeOnly(12, 0), new TimeOnly(13, 30));

    private readonly TempDatabase file = new();
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.FromHours(2)));

    public void Dispose() => file.Dispose();

    [Fact]
    public async Task AC_1_10_installed_monday_2026_10_12_with_a_first_rule_saved_monday_2026_10_19()
    {
        var rules = new SqliteLunchRules(file.Open(clock), clock);
        Assert.Same(LunchRule.Prefilled, await rules.InForceOnAsync(new DateOnly(2026, 9, 21)));
        Assert.Same(LunchRule.Prefilled, await rules.InForceOnAsync(new DateOnly(2026, 10, 19)));

        clock.Now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(2));
        var first = await rules.SaveAsync(Window, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(30))]);

        Assert.Equal(new DateOnly(2026, 10, 19), first.ValidFrom);
        Assert.Equal(first.Tiers, (await rules.InForceOnAsync(new DateOnly(2026, 9, 21))).Tiers);
        Assert.Same(LunchRule.Prefilled, await rules.InForceOnAsync(new DateOnly(2026, 10, 12)));
        Assert.Same(LunchRule.Prefilled, await rules.InForceOnAsync(new DateOnly(2026, 10, 18)));
        Assert.Equal(first.Tiers, (await rules.InForceOnAsync(new DateOnly(2026, 10, 19))).Tiers);
    }

    [Fact]
    public async Task CR_006_the_last_save_of_the_first_save_day_covers_the_days_before_install()
    {
        var rules = new SqliteLunchRules(file.Open(clock), clock);
        clock.Now = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(2));
        await rules.SaveAsync(new TimeRange(new TimeOnly(12, 0), new TimeOnly(12, 30)), [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(30))]);
        clock.Now = clock.Now.AddMinutes(1);
        var corrected = await rules.SaveAsync(Window, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(30))]);

        var beforeInstall = await rules.InForceOnAsync(new DateOnly(2026, 9, 21));

        Assert.Equal(corrected.Window, beforeInstall.Window);
    }

    [Fact]
    public async Task AC_1_13_a_second_rule_saved_wednesday_2026_10_21_applies_from_wednesday_only()
    {
        var rules = new SqliteLunchRules(file.Open(clock), clock);
        clock.Now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(2));
        var first = await rules.SaveAsync(Window, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(30))]);
        clock.Now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.FromHours(2));
        var second = await rules.SaveAsync(Window, [new BreakTier(TimeSpan.FromHours(6), TimeSpan.FromMinutes(45))]);

        Assert.Equal(first.Tiers, (await rules.InForceOnAsync(new DateOnly(2026, 9, 21))).Tiers);
        Assert.Equal(first.Tiers, (await rules.InForceOnAsync(new DateOnly(2026, 10, 20))).Tiers);
        foreach (var day in Enumerable.Range(21, 5))
        {
            Assert.Equal(second.Tiers, (await rules.InForceOnAsync(new DateOnly(2026, 10, day))).Tiers);
        }
    }
}
