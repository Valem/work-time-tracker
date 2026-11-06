using Wtt.Domain.Planning;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class BridgeDayRule
{
    [Fact]
    public void BR_020_bridge_day()
    {
        var rule = new RuleScenarios();
        var zurich = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027);

        rule.Check("Friday 07.05 between Auffahrt and Saturday is a bridge day; its neighbours and the next Friday are not", () => Assert.Equal(
            [false, true, false, false],
            new[] { "2027-05-06", "2027-05-07", "2027-05-08", "2027-05-14" }.Select(date => zurich.Status(date).Bridge)));

        rule.Check("booked, it is no bridge day", () =>
        {
            var booked = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(Zurich2027).Booked(DayPart.Half, "2027-05-07");
            Assert.False(booked.Status("2027-05-07").Bridge);
        });

        rule.Check("with Friday free it is a free day and no bridge day", () =>
        {
            var freeFriday = new PlanScenario(Settings(2027, pensum: 80, free: "Fri", canton: Canton.ZH)).Loaded(Zurich2027);
            Assert.Equal((DayKind.FreeDay, false), (freeFriday.Status("2027-05-07").Kind, freeFriday.Status("2027-05-07").Bridge));
        });

        rule.Check("a date of half work share is no bridge day", () =>
        {
            var halfFriday = new PlanScenario(Settings(2027, half: "Fri", canton: Canton.ZH)).Loaded(Zurich2027);
            Assert.False(halfFriday.Status("2027-05-07").Bridge);
        });

        var fronleichnam = Year(
            Canton.ZH,
            2027,
            Holiday("2027-01-01", "Neujahrstag", reach: HolidayReach.Country),
            Holiday("2027-05-27", "Fronleichnam", HolidayKind.Optional));

        rule.Check("a neighbour's holiday must count: unticked, the Friday after it is no bridge day; ticked, it is", () =>
        {
            var optional = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(fronleichnam);
            Assert.False(optional.Status("2027-05-28").Bridge);
            optional.Tick("2027-05-27");
            Assert.True(optional.Status("2027-05-28").Bridge);
        });

        rule.Check("two neighbours without work share, neither because of a public holiday that counts, make no bridge day", () =>
        {
            var freeThursday = new PlanScenario(Settings(2027, free: "Thu", canton: Canton.ZH)).Loaded(Zurich2027);
            Assert.False(freeThursday.Status("2027-05-14").Bridge);
            Assert.False(freeThursday.Status("2027-04-30").Bridge);
        });

        rule.Check("a Monday before a Tuesday holiday that counts is a bridge day", () =>
        {
            var tuesday = new PlanScenario(Settings(2027, canton: Canton.ZH))
                .Loaded(Year(Canton.ZH, 2027, Holiday("2027-01-01", "Neujahrstag", reach: HolidayReach.Country), Holiday("2027-06-22", "Synthetischer Feiertag")));
            Assert.Equal([false, true, false], new[] { "2027-06-18", "2027-06-21", "2027-06-23" }.Select(date => tuesday.Status(date).Bridge));
        });

        rule.AssertAllHold();
    }
}
