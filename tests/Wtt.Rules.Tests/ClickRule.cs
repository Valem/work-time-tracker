using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class ClickRule
{
    [Fact]
    public void BR_019_click()
    {
        var rule = new RuleScenarios();
        var plan = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);

        rule.Check("on a bookable date of whole share clicks go none → whole → half → none", () => Assert.Equal(
            [DayPart.Whole, DayPart.Half, null],
            Enumerable.Range(0, 3).Select(_ =>
            {
                plan.Click("2027-04-20");
                return plan.Status("2027-04-20").Booking;
            })));

        rule.Check("on an optional, part-canton holiday without a counting booking a click ticks it, a second unticks it", () =>
        {
            plan.Click("2027-04-19");
            Assert.Equal((true, (DayPart?)null), Ticked(plan, "2027-04-19"));
            plan.Click("2027-04-19");
            Assert.Equal((false, (DayPart?)null), Ticked(plan, "2027-04-19"));
        });

        rule.Check("a holiday of a kind not known reaching the whole canton does not count by itself: a click ticks it, a second unticks it", () =>
        {
            var otherKind = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Year(
                Canton.ZH,
                2027,
                Holiday("2027-01-01", "Neujahrstag", reach: HolidayReach.Country),
                Holiday("2027-11-11", "Bankfeiertag", HolidayKind.Other)));
            otherKind.Click("2027-11-11");
            Assert.Equal((true, (DayPart?)null), Ticked(otherKind, "2027-11-11"));
            otherKind.Click("2027-11-11");
            Assert.Equal((false, (DayPart?)null), Ticked(otherKind, "2027-11-11"));
        });

        rule.Check("of a half and a whole holiday on one date a click ticks and unticks the half one", () =>
        {
            var twoEntries = new PlanScenario(Settings(2026, canton: Canton.SO)).Loaded(Year(
                Canton.SO,
                2026,
                Holiday("2026-01-01", "Neujahrstag", reach: HolidayReach.Country),
                Holiday("2026-05-01", "Tag der Arbeit", reach: HolidayReach.PartOfCanton, share: DayPart.Half),
                Holiday("2026-05-01", "Tag der Arbeit", reach: HolidayReach.PartOfCanton)));
            twoEntries.Click("2026-05-01");
            Assert.Equal(
                [true, false],
                twoEntries.Status("2026-05-01").Holiday!.Entries.OrderBy(entry => entry.Entry.Share).Select(entry => entry.Ticked));
            twoEntries.Click("2026-05-01");
            Assert.False(twoEntries.Status("2026-05-01").Holiday!.Ticked);
        });

        rule.Check("where the work share is a half: none → half → none", () =>
        {
            var halfWednesday = new PlanScenario(Settings(2027, half: "Wed"));
            halfWednesday.Click("2027-04-21");
            Assert.Equal(DayPart.Half, halfWednesday.Status("2027-04-21").Booking);
            halfWednesday.Click("2027-04-21");
            Assert.Null(halfWednesday.Status("2027-04-21").Booking);
        });

        rule.Check("where the work share is a half a whole booking goes to none", () =>
        {
            var sechselaeuten = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);
            sechselaeuten.Drag("2027-04-19", "2027-04-19");
            sechselaeuten.Tick("2027-04-19");
            sechselaeuten.Click("2027-04-19");
            Assert.Equal((true, (DayPart?)null), Ticked(sechselaeuten, "2027-04-19"));
        });

        rule.Check("on an optional holiday whose booking counts a click cycles the booking and leaves the tick", () =>
        {
            var booked = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);
            booked.Drag("2027-04-19", "2027-04-19");
            booked.Click("2027-04-19");
            Assert.Equal((false, (DayPart?)DayPart.Half), Ticked(booked, "2027-04-19"));
        });

        rule.Check("on an optional holiday whose booking counts 0 a click ticks it", () =>
        {
            var freeMonday = new PlanScenario(Settings(2027, free: "Mon", canton: Canton.ZH)).Loaded(Zurich2027).Booked(DayPart.Whole, "2027-09-13");
            freeMonday.Click("2027-09-13");
            Assert.Equal((true, (DayPart?)DayPart.Whole), Ticked(freeMonday, "2027-09-13"));
        });

        rule.Check("anywhere else a click changes nothing: a weekend, a free day, a public holiday that counts", () =>
        {
            var freeFriday = new PlanScenario(Settings(2027, free: "Fri", canton: Canton.ZH)).Loaded(Zurich2027);
            foreach (var date in new[] { "2027-04-24", "2027-04-25", "2027-04-23", "2027-03-29", "2027-05-06" })
            {
                Assert.Equal((date, true), (date, freeFriday.Click(date).IsEmpty));
            }
        });

        rule.AssertAllHold();
    }

    private static (bool Ticked, DayPart? Booking) Ticked(PlanScenario plan, string date)
    {
        var status = plan.Status(date);
        return (status.Holiday?.Ticked ?? false, status.Booking);
    }
}
