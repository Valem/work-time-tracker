using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class ClickRule
{
    [Fact]
    public void BR_019_click()
    {
        var rule = new RuleScenarios();

        rule.Check("on a bookable date of whole share clicks go none → whole → half → none", () =>
        {
            var plan = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);
            Assert.Equal([DayPart.Whole, DayPart.Half, null], Clicks(plan, "2027-04-20", 3));
        });

        rule.Check("where the work share is a half: none → half → none", () =>
        {
            var halfWednesday = new PlanScenario(Settings(2027, half: "Wed"));
            Assert.Equal([DayPart.Half, null], Clicks(halfWednesday, "2027-04-21", 2));
        });

        rule.Check("a ticked half public holiday leaves half a day: clicks go none → half → none and the tick stays", () =>
        {
            var sechselaeuten = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);
            sechselaeuten.Tick("2027-04-19");
            Assert.Equal([DayPart.Half, null], Clicks(sechselaeuten, "2027-04-19", 2));
            Assert.True(sechselaeuten.Status("2027-04-19").Holiday!.Ticked);
        });

        rule.Check("where the work share is a half a whole booking goes to none, the tick staying", () =>
        {
            var sechselaeuten = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);
            sechselaeuten.Drag("2027-04-19", "2027-04-19");
            sechselaeuten.Tick("2027-04-19");
            sechselaeuten.Click("2027-04-19");
            Assert.Equal((true, (DayPart?)null), Ticked(sechselaeuten, "2027-04-19"));
        });

        rule.Check("an offered holiday not ticked is a workday for the click: none → whole → half → none, never ticked", () =>
        {
            var offered = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);
            Assert.Equal([DayPart.Whole, DayPart.Half, null], Clicks(offered, "2027-04-19", 3));
            Assert.False(offered.Status("2027-04-19").Holiday!.Ticked);
        });

        rule.Check("a click never changes a tick: a holiday of a kind not known, and two entries on one date, are booked, not ticked", () =>
        {
            var otherKind = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Year(
                Canton.ZH,
                2027,
                Holiday("2027-01-01", "Neujahrstag", reach: HolidayReach.Country),
                Holiday("2027-11-11", "Bankfeiertag", HolidayKind.Other)));
            Assert.Empty(otherKind.Click("2027-11-11").Ticks);
            Assert.Equal((false, (DayPart?)DayPart.Whole), Ticked(otherKind, "2027-11-11"));

            var twoEntries = new PlanScenario(Settings(2026, canton: Canton.SO)).Loaded(Year(
                Canton.SO,
                2026,
                Holiday("2026-01-01", "Neujahrstag", reach: HolidayReach.Country),
                Holiday("2026-05-01", "Tag der Arbeit", reach: HolidayReach.PartOfCanton, share: DayPart.Half),
                Holiday("2026-05-01", "Tag der Arbeit", reach: HolidayReach.PartOfCanton)));
            twoEntries.Tick("2026-05-01", share: DayPart.Whole);
            Assert.Empty(twoEntries.Click("2026-05-01").Ticks);
            Assert.Equal(
                [false, true],
                twoEntries.Status("2026-05-01").Holiday!.Entries.OrderBy(entry => entry.Entry.Share).Select(entry => entry.Ticked));
        });

        rule.Check("anywhere else a click changes nothing: a weekend, a free day, a public holiday that counts, a booking on a free day", () =>
        {
            var freeFriday = new PlanScenario(Settings(2027, free: "Fri", canton: Canton.ZH)).Loaded(Zurich2027).Booked(DayPart.Whole, "2027-04-30");
            foreach (var date in new[] { "2027-04-24", "2027-04-25", "2027-04-23", "2027-03-29", "2027-05-06", "2027-04-30" })
            {
                Assert.Equal((date, true), (date, freeFriday.Click(date).IsEmpty));
            }
        });

        rule.AssertAllHold();
    }

    /// <summary>The booking the date holds after each of a number of clicks.</summary>
    private static IReadOnlyList<DayPart?> Clicks(PlanScenario plan, string date, int clicks) =>
        [.. Enumerable.Range(0, clicks).Select(_ =>
        {
            plan.Click(date);
            return plan.Status(date).Booking;
        })];

    private static (bool Ticked, DayPart? Booking) Ticked(PlanScenario plan, string date)
    {
        var status = plan.Status(date);
        return (status.Holiday?.Ticked ?? false, status.Booking);
    }
}
