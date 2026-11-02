using Wtt.Domain.Planning;
using Wtt.Rules.Tests.Support;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class WorkShareRule
{
    [Fact]
    public async Task BR_015_work_share()
    {
        var rule = new RuleScenarios();
        var plan = new PlanScenario(Settings(2027, pensum: 80, free: "Fri", half: "Wed", canton: Canton.ZH)).Loaded(Zurich2027);
        var solothurn = await HolidayAnswers.ReadAsync(HolidayAnswers.Solothurn2026, Canton.SO, 2026);

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

        rule.Check("a free weekday with a public holiday that counts is that public holiday, its share none either way", () =>
        {
            var karfreitag = plan.Status("2027-03-26");
            Assert.Equal(
                (DayKind.PublicHoliday, WorkShare.None, WorkShare.Whole, "Karfreitag"),
                (karfreitag.Kind, karfreitag.Share, karfreitag.PublicHolidayShare, karfreitag.CountingHolidays.Single().NameIn("DE")));
            Assert.Equal((DayKind.FreeDay, WorkShare.None), (plan.Status("2027-04-02").Kind, plan.Status("2027-04-02").PublicHolidayShare));
        });

        rule.Check("a weekend with a public holiday that counts is that public holiday, its share none and nothing to book", () =>
        {
            var tagDerArbeit = plan.Status("2027-05-01");
            Assert.Equal(
                (DayKind.PublicHoliday, WorkShare.None, WorkShare.Whole, "Tag der Arbeit", false),
                (tagDerArbeit.Kind, tagDerArbeit.Share, tagDerArbeit.PublicHolidayShare, tagDerArbeit.CountingHolidays.Single().NameIn("DE"), tagDerArbeit.Bookable));
            Assert.Equal((DayKind.Weekend, WorkShare.None), (plan.Status("2027-05-02").Kind, plan.Status("2027-05-02").Share));
        });

        rule.Check("a half public holiday shows whole where no work share is left beside it — a Saturday, a free Monday — and half beside half a day's work; what bookings count is unchanged", () =>
        {
            var halves = new PlanScenario(Settings(2027, free: "Mon", canton: Canton.ZH)).Loaded(Year(
                Canton.ZH,
                2027,
                Holiday("2027-01-01", "Neujahrstag", reach: HolidayReach.Country),
                Holiday("2027-04-19", "Halber Montag", share: DayPart.Half),
                Holiday("2027-04-24", "Halber Samstag", share: DayPart.Half),
                Holiday("2027-04-27", "Halber Dienstag", share: DayPart.Half)));
            halves.Booked(DayPart.Whole, "2027-04-19", "2027-04-24", "2027-04-27");
            Assert.Equal(
                [
                    (DayKind.PublicHoliday, WorkShare.Whole, WorkShare.None, HalfDays.Zero),
                    (DayKind.PublicHoliday, WorkShare.Whole, WorkShare.None, HalfDays.Zero),
                    (DayKind.PublicHoliday, WorkShare.Half, WorkShare.Half, Days(0.5m)),
                ],
                new[] { "2027-04-19", "2027-04-24", "2027-04-27" }
                    .Select(halves.Status)
                    .Select(status => (status.Kind, status.PublicHolidayShare, status.Share, status.Counted)));
        });

        rule.Check("of several public holidays that count on one date, the largest share is taken: SO 01.05.2026 with its half and its whole entry ticked leaves none", () =>
        {
            var both = new PlanScenario(Settings(2026, canton: Canton.SO)).Loaded(solothurn);
            both.Tick("2026-05-01", share: DayPart.Half);
            both.Tick("2026-05-01", share: DayPart.Whole);
            var tagDerArbeit = both.Status("2026-05-01");
            Assert.Equal((WorkShare.Whole, WorkShare.None), (tagDerArbeit.PublicHolidayShare, tagDerArbeit.Share));
        });

        rule.Check("the share a public holiday that counts takes shows on the day: ticked, Sechseläuten is a half public holiday", () =>
        {
            var sechselaeuten = plan.Status("2027-04-19");
            Assert.Equal((DayKind.PublicHoliday, WorkShare.Half), (sechselaeuten.Kind, sechselaeuten.PublicHolidayShare));
            Assert.Equal((DayKind.Workday, WorkShare.None), (plan.Status("2027-04-20").Kind, plan.Status("2027-04-20").PublicHolidayShare));
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
