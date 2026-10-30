using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class YearCountsRule
{
    [Fact]
    public void BR_017_year_counts()
    {
        var rule = new RuleScenarios();

        rule.Check("without yearly days saved, 20 × the Pensum in force on 1 January, to the nearest half day", () =>
        {
            Assert.Equal(Days(16), new PlanScenario(Settings(2027, pensum: 80, free: "Fri")).Counts(2027, "2027-06-15").Yearly);
            Assert.Equal(Days(16.5m), new PlanScenario(Settings(2027, pensum: 83)).Counts(2027, "2027-06-15").Yearly);
            Assert.Equal(Days(11.5m), new PlanScenario(Settings(2027, pensum: 57)).Counts(2027, "2027-06-15").Yearly);
            Assert.Equal(Days(20), new PlanScenario().Counts(2027, "2027-06-15").Yearly);
        });

        rule.Check("the yearly days saved and in force on 1 January", () =>
        {
            Assert.Equal(Days(25), new PlanScenario(Settings(2027, pensum: 80, yearlyDays: 25)).Counts(2027, "2027-06-15").Yearly);
            var midYear = new PlanScenario(Settings(2027, pensum: 80))
                .Saved(new PlanSettings(D("2027-07-01"), 100, Week(), Days(30), null));
            Assert.Equal(Days(16), midYear.Counts(2027, "2027-06-15").Yearly);
            Assert.Equal(Days(30), midYear.Counts(2028, "2027-06-15").Yearly);
        });

        var plan = new PlanScenario(Settings(2027, pensum: 80, free: "Fri"))
            .Booked(DayPart.Whole, "2027-01-18", "2027-01-19", "2027-01-20", "2027-01-21", "2027-06-15")
            .Booked(DayPart.Half, "2027-09-14");

        rule.Check("taken counts the days before today, planned those from today on, left is yearly less both", () =>
        {
            Assert.Equal(Ledger(16, 4, 1.5m, 10.5m), plan.Counts(2027, "2027-06-15"));
            Assert.Equal(Ledger(16, 5.5m, 0, 10.5m), plan.Counts(2027, "2028-01-01"));
            Assert.Equal(Ledger(16, 0, 5.5m, 10.5m), plan.Counts(2027, "2026-12-01"));
        });

        rule.Check("what counts is each booking's counted days, and only the year's", () =>
        {
            plan.Booked(DayPart.Whole, "2027-01-23", "2027-02-05", "2026-12-31", "2028-01-03");
            Assert.Equal(Ledger(16, 4, 1.5m, 10.5m), plan.Counts(2027, "2027-06-15"));
        });

        rule.Check("left may be negative", () =>
        {
            var over = new PlanScenario(Settings(2027, yearlyDays: 2)).Booked(DayPart.Whole, "2027-03-01", "2027-03-02", "2027-03-03");
            Assert.Equal(Ledger(2, 0, 3, -1), over.Counts(2027, "2027-01-04"));
        });

        rule.AssertAllHold();
    }

    private static YearLedger Ledger(decimal yearly, decimal taken, decimal planned, decimal left) =>
        new(Days(yearly), Days(taken), Days(planned), Days(left));
}
