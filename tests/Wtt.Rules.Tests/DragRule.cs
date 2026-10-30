using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class DragRule
{
    [Fact]
    public void BR_018_drag()
    {
        var rule = new RuleScenarios();
        var plan = new PlanScenario(Settings(2027, pensum: 80, free: "Fri", canton: Canton.ZH)).Loaded(Zurich2027);

        rule.Check("from an unbooked anchor a drag books every bookable date from anchor to end whole; Karfreitag, the weekend and Ostermontag keep what they hold", () =>
        {
            plan.Drag("2027-03-22", "2027-03-31");
            Assert.Equal(
                ["2027-03-22 Whole", "2027-03-23 Whole", "2027-03-24 Whole", "2027-03-25 Whole", "2027-03-30 Whole", "2027-03-31 Whole"],
                plan.BookingsBetween("2027-03-01", "2027-04-30"));
            Assert.Equal(Days(6), plan.Counts(2027, "2027-01-01").Planned);
        });

        rule.Check("from an anchor whose booking counts a drag removes the bookings on the bookable dates it covers", () =>
        {
            plan.Drag("2027-03-23", "2027-03-24");
            Assert.Equal(["2027-03-22 Whole", "2027-03-25 Whole", "2027-03-30 Whole", "2027-03-31 Whole"], plan.BookingsBetween("2027-03-01", "2027-04-30"));
            Assert.Equal(Days(4), plan.Counts(2027, "2027-01-01").Planned);
        });

        rule.Check("the anchor may lie after the end: the dates between both are covered", () =>
        {
            plan.Drag("2027-03-31", "2027-03-30");
            Assert.Equal(["2027-03-22 Whole", "2027-03-25 Whole"], plan.BookingsBetween("2027-03-01", "2027-04-30"));
        });

        rule.Check("booking makes a half booking whole", () =>
        {
            var half = new PlanScenario(Settings(2027)).Booked(DayPart.Half, "2027-06-08");
            half.Drag("2027-06-07", "2027-06-09");
            Assert.Equal(["2027-06-07 Whole", "2027-06-08 Whole", "2027-06-09 Whole"], half.BookingsBetween("2027-06-01", "2027-06-30"));
        });

        rule.Check("an anchor whose booking counts 0 books; dates that cannot be booked keep their booking either way", () =>
        {
            var stale = new PlanScenario(Settings(2027, pensum: 80, free: "Fri", canton: Canton.ZH)).Loaded(Zurich2027)
                .Booked(DayPart.Whole, "2027-03-26");
            stale.Drag("2027-03-26", "2027-03-30");
            Assert.Equal(["2027-03-26 Whole", "2027-03-30 Whole"], stale.BookingsBetween("2027-03-01", "2027-04-30"));
            stale.Drag("2027-03-30", "2027-03-25");
            Assert.Equal(["2027-03-26 Whole"], stale.BookingsBetween("2027-03-01", "2027-04-30"));
        });

        rule.Check("a half booking that counts makes the drag remove", () =>
        {
            var half = new PlanScenario(Settings(2027)).Booked(DayPart.Half, "2027-06-07").Booked(DayPart.Whole, "2027-06-08");
            half.Drag("2027-06-07", "2027-06-09");
            Assert.Empty(half.BookingsBetween("2027-06-01", "2027-06-30"));
        });

        rule.AssertAllHold();
    }
}
