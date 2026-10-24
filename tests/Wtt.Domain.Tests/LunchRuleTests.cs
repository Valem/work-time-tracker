using Wtt.Domain.Rules;
using Wtt.Domain.Time;
using static Wtt.Domain.Tests.TestRules;

namespace Wtt.Domain.Tests;

public sealed class LunchRuleTests
{
    [Theory]
    [InlineData("5:30", "0:00")]
    [InlineData("5:31", "0:15")]
    [InlineData("7:00", "0:15")]
    [InlineData("7:01", "0:30")]
    [InlineData("9:01", "1:00")]
    public void A_tier_applies_when_its_threshold_is_strictly_exceeded(string net, string minimum)
    {
        Assert.Equal(Hm(minimum), Ac.TierMinimumFor(Hm(net)));
    }

    [Fact]
    public void A_lunch_pause_longer_than_the_tier_is_deducted_whole()
    {
        Assert.Equal(Hm("1:10"), Ac.BreakFor(Hm("10:00"), Hm("1:10")));
    }

    [Fact]
    public void Without_tiers_the_break_is_the_lunch_pause()
    {
        var rule = new LunchRule(DateOnly.MinValue, Ac.Window, []);

        Assert.Equal(Hm("0:20"), rule.BreakFor(Hm("10:00"), Hm("0:20")));
    }

    [Fact]
    public void Tiers_cannot_share_a_threshold_or_be_negative()
    {
        Assert.Throws<ArgumentException>(() => new LunchRule(DateOnly.MinValue, Ac.Window, [new BreakTier(Hm("5:00"), Hm("0:10")), new BreakTier(Hm("5:00"), Hm("0:20"))]));
        Assert.Throws<ArgumentException>(() => new LunchRule(DateOnly.MinValue, Ac.Window, [new BreakTier(TimeSpan.FromMinutes(-1), Hm("0:10"))]));
    }

    [Fact]
    public void A_window_must_end_after_it_starts()
    {
        Assert.Throws<ArgumentException>(() => new TimeRange(new TimeOnly(14, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void Before_any_save_every_day_uses_the_prefilled_rule()
    {
        var book = new LunchRuleBook(new DateOnly(2026, 10, 12), []);

        Assert.Same(LunchRule.Prefilled, book.InForceOn(new DateOnly(2020, 1, 1)));
        Assert.Same(LunchRule.Prefilled, book.InForceOn(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void A_later_save_on_the_same_day_supersedes_the_earlier_one()
    {
        var first = new LunchRule(new DateOnly(2026, 10, 19), Ac.Window, []);
        var second = new LunchRule(new DateOnly(2026, 10, 19), Ac.Window, Ac.Tiers);
        var book = new LunchRuleBook(new DateOnly(2026, 10, 12), [first, second]);

        Assert.Same(second, book.InForceOn(new DateOnly(2026, 10, 19)));
        Assert.Same(second, book.InForceOn(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void CR_006_a_rule_corrected_on_the_day_of_the_first_save_also_covers_the_days_before_install()
    {
        var typo = new LunchRule(new DateOnly(2026, 10, 19), new TimeRange(new TimeOnly(12, 0), new TimeOnly(12, 30)), Ac.Tiers);
        var corrected = new LunchRule(new DateOnly(2026, 10, 19), Ac.Window, Ac.Tiers);
        var book = new LunchRuleBook(new DateOnly(2026, 10, 12), [typo, corrected]);
        var rebuiltDay = new Scenario()
            .Use("2026-09-21 08:00", "2026-09-21 12:40")
            .Use("2026-09-21 13:30", "2026-09-21 17:00");

        var workday = rebuiltDay.Day("2026-09-21", book.InForceOn(new DateOnly(2026, 9, 21)));

        Assert.Equal(Hm("0:50"), workday.Break);
        Assert.Equal(Hm("8:10"), workday.Total);
    }

    [Fact]
    public void A_rule_saved_on_a_later_day_does_not_reach_the_days_before_install()
    {
        var first = new LunchRule(new DateOnly(2026, 10, 19), Ac.Window, Ac.Tiers);
        var later = new LunchRule(new DateOnly(2026, 10, 21), Ac.Window, []);
        var book = new LunchRuleBook(new DateOnly(2026, 10, 12), [first, later]);

        Assert.Same(first, book.InForceOn(new DateOnly(2026, 9, 21)));
    }
}
