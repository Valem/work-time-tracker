using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class WorkShareRule
{
    [Fact]
    public void BR_015_work_share()
    {
        var rule = new RuleScenarios();
        var plan = new PlanScenario(Settings(2027, pensum: 80, free: "Fri", half: "Wed", canton: Canton.ZH)).Loaded(Zurich2027);

        rule.Check("a date's share is its weekday's share in the settings in force, none on Saturday and Sunday", () => Assert.Equal(
            [WorkShare.Whole, WorkShare.Whole, WorkShare.Half, WorkShare.Whole, WorkShare.None, WorkShare.None, WorkShare.None],
            Shares(plan, "2027-03-15", "2027-03-21")));

        rule.Check("a public holiday that counts takes its share: Ostermontag leaves Monday 29.03 none", () =>
            Assert.Equal(WorkShare.None, plan.Status("2027-03-29").Share));

        rule.Check("a holiday that does not count takes nothing; ticked, a half one takes a half", () =>
        {
            Assert.Equal(WorkShare.Whole, plan.Status("2027-04-19").Share);
            plan.Tick("2027-04-19");
            Assert.Equal(WorkShare.Half, plan.Status("2027-04-19").Share);
        });

        rule.Check("never below none: a whole holiday on a half day, and on a free day, leave none", () =>
        {
            var halfMonday = new PlanScenario(Settings(2027, half: "Mon", canton: Canton.ZH)).Loaded(Zurich2027);
            Assert.Equal(WorkShare.None, halfMonday.Status("2027-03-29").Share);
            Assert.Equal(WorkShare.None, plan.Status("2027-03-26").Share);
        });

        rule.Check("a half holiday that counts on a half day leaves none", () =>
        {
            var halfMonday = new PlanScenario(Settings(2027, half: "Mon", canton: Canton.ZH)).Loaded(Zurich2027);
            halfMonday.Tick("2027-09-13");
            Assert.Equal(WorkShare.None, halfMonday.Status("2027-09-13").Share);
        });

        rule.Check("the settings in force on the date give the weekday's share", () =>
        {
            var changed = new PlanScenario(Settings(2027, free: "Fri"), Settings(2028));
            Assert.Equal(WorkShare.None, changed.Status("2027-12-31").Share);
            Assert.Equal(WorkShare.Whole, changed.Status("2028-01-07").Share);
        });

        rule.Check("only a date with a share above none can be booked", () =>
        {
            Assert.Equal(
                [true, true, true, true, false, false, false],
                StatusesOf(plan, "2027-03-15", "2027-03-21").Select(status => status.Bookable));
            Assert.False(plan.Status("2027-03-29").Bookable);
            plan.Drag("2027-03-25", "2027-03-30");
            Assert.Equal(["2027-03-25 Whole", "2027-03-30 Whole"], plan.BookingsBetween("2027-03-25", "2027-03-30"));
        });

        rule.AssertAllHold();
    }

    private static IEnumerable<DayStatus> StatusesOf(PlanScenario plan, string first, string last)
    {
        var statuses = new List<DayStatus>();
        for (var date = D(first); date <= D(last); date = date.AddDays(1))
        {
            statuses.Add(plan.Status($"{date:yyyy-MM-dd}"));
        }

        return statuses;
    }

    private static IEnumerable<WorkShare> Shares(PlanScenario plan, string first, string last) =>
        StatusesOf(plan, first, last).Select(status => status.Share);
}
