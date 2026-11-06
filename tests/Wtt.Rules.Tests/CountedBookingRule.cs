using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class CountedBookingRule
{
    [Fact]
    public void BR_016_counted_booking()
    {
        var rule = new RuleScenarios();
        var plan = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027)
            .Booked(DayPart.Whole, "2027-04-20")
            .Booked(DayPart.Half, "2027-04-21");

        rule.Check("a whole booking counts 1, a half 0.5", () =>
        {
            Assert.Equal(Days(1), plan.Status("2027-04-20").Counted);
            Assert.Equal(Days(0.5m), plan.Status("2027-04-21").Counted);
        });

        rule.Check("never more than the date's work share: Sechseläuten booked whole counts 1, ticked 0.5, unticked 1 again", () =>
        {
            plan.Drag("2027-04-19", "2027-04-19");
            Assert.Equal(Days(1), plan.Status("2027-04-19").Counted);
            plan.Tick("2027-04-19");
            Assert.Equal(Days(0.5m), plan.Status("2027-04-19").Counted);
            plan.Tick("2027-04-19", ticked: false);
            Assert.Equal(Days(1), plan.Status("2027-04-19").Counted);
        });

        rule.Check("on a date with no work share it counts 0 and stays booked: a booked Friday made free, then a workday again", () =>
        {
            var friday = new PlanScenario(Settings(2027, canton: Canton.ZH)).Booked(DayPart.Whole, "2027-06-11");
            Assert.Equal(Days(1), friday.Status("2027-06-11").Counted);
            friday.Saved(Settings(2027, pensum: 80, free: "Fri", yearlyDays: 25, canton: Canton.ZH));
            Assert.Equal((Days(0), (DayPart?)DayPart.Whole), (friday.Status("2027-06-11").Counted, friday.Status("2027-06-11").Booking));
            friday.Saved(Settings(2027, canton: Canton.ZH));
            Assert.Equal(Days(1), friday.Status("2027-06-11").Counted);
        });

        rule.Check("a booking on a day that a newly loaded public holiday takes counts 0 and stays booked", () =>
        {
            var late = new PlanScenario(Settings(2027, canton: Canton.ZH)).Booked(DayPart.Whole, "2027-05-17");
            Assert.Equal(Days(1), late.Status("2027-05-17").Counted);
            late.Loaded(Zurich2027);
            Assert.Equal((Days(0), (DayPart?)DayPart.Whole), (late.Status("2027-05-17").Counted, late.Status("2027-05-17").Booking));
        });

        rule.Check("a half booking on a half share counts 0.5, a whole booking there counts 0.5", () =>
        {
            var halfWednesday = new PlanScenario(Settings(2027, half: "Wed"))
                .Booked(DayPart.Half, "2027-04-21")
                .Booked(DayPart.Whole, "2027-04-28");
            Assert.Equal(Days(0.5m), halfWednesday.Status("2027-04-21").Counted);
            Assert.Equal(Days(0.5m), halfWednesday.Status("2027-04-28").Counted);
        });

        rule.AssertAllHold();
    }
}
