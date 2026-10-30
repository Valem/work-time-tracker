using Wtt.Domain.Planning;
using Wtt.PublicHolidays;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class PublicHolidayCountsRule
{
    /// <summary>
    /// A synthetic answer in OpenHolidays' shape for Aargau 2027: kinds Public, Optional and one this program does not know;
    /// reaching the country, the canton among others, or only districts of the canton.
    /// </summary>
    private const string Aargau2027 = """
        [
          {"id":"1","startDate":"2027-01-01","endDate":"2027-01-01","type":"Public","name":[{"language":"DE","text":"Neujahrstag"}],
           "regionalScope":"National","temporalScope":"FullDay","nationwide":true},
          {"id":"2","startDate":"2027-01-02","endDate":"2027-01-02","type":"Optional","name":[{"language":"DE","text":"Berchtoldstag"}],
           "temporalScope":"FullDay","nationwide":false,"subdivisions":[{"code":"CH-AG","shortName":"AG"}]},
          {"id":"3","startDate":"2027-03-26","endDate":"2027-03-26","type":"Public","name":[{"language":"DE","text":"Karfreitag"}],
           "temporalScope":"FullDay","nationwide":false,"subdivisions":[{"code":"CH-ZH"},{"code":"CH-AG"},{"code":"CH-BE"}]},
          {"id":"4","startDate":"2027-03-29","endDate":"2027-03-29","type":"Public","name":[{"language":"DE","text":"Ostermontag"}],
           "temporalScope":"FullDay","nationwide":false,"subdivisions":[{"code":"CH-ZH"},{"code":"CH-AG-RF-MA"},{"code":"CH-AG-LB"}]},
          {"id":"5","startDate":"2027-11-11","endDate":"2027-11-11","type":"Bank","name":[{"language":"DE","text":"Bankfeiertag"}],
           "temporalScope":"FullDay","nationwide":false,"subdivisions":[{"code":"CH-AG"}]}
        ]
        """;

    [Fact]
    public async Task BR_014_public_holiday_that_counts()
    {
        var rule = new RuleScenarios();
        var aargau = await ReadAnswerAsync(Aargau2027, Canton.AG, 2027);
        var plan = new PlanScenario(Settings(2027, canton: Canton.AG)).Loaded(aargau, Zurich2027);

        rule.Check("a public holiday reaching the whole country counts by itself", () =>
            Assert.Equal((true, false), Counts(plan, "2027-01-01")));

        rule.Check("a public holiday reaching the whole canton in force counts by itself", () =>
            Assert.Equal((true, false), Counts(plan, "2027-03-26")));

        rule.Check("a public holiday reaching only part of the canton is offered and counts only once ticked", () =>
        {
            Assert.Equal((false, true), Counts(plan, "2027-03-29"));
            plan.Tick("2027-03-29");
            Assert.Equal((true, true), Counts(plan, "2027-03-29"));
        });

        rule.Check("an optional holiday is offered and counts only once ticked", () =>
        {
            Assert.Equal((false, true), Counts(plan, "2027-01-02"));
            plan.Tick("2027-01-02");
            Assert.Equal((true, true), Counts(plan, "2027-01-02"));
        });

        rule.Check("a holiday of a kind not known is offered and counts only once ticked", () =>
        {
            Assert.Equal((false, true), Counts(plan, "2027-11-11"));
            plan.Tick("2027-11-11");
            Assert.Equal((true, true), Counts(plan, "2027-11-11"));
        });

        rule.Check("the holidays of the canton in force count, not those of another canton loaded", () =>
        {
            var zurich = new PlanScenario(Settings(2027, canton: Canton.ZH)).Loaded(aargau, Zurich2027);
            Assert.Equal((true, false), Counts(zurich, "2027-03-29"));
            Assert.Null(zurich.Status("2027-11-11").Holiday);

            var bern = new PlanScenario(Settings(2027, canton: Canton.BE)).Loaded(aargau, Zurich2027);
            Assert.Null(bern.Status("2027-03-26").Holiday);
            Assert.Null(new PlanScenario().Loaded(aargau).Status("2027-03-26").Holiday);
        });

        rule.AssertAllHold();
    }

    /// <summary>Whether a public holiday counts on the date, and whether one is offered there to tick.</summary>
    private static (bool Counts, bool Offered) Counts(PlanScenario plan, string date) =>
        plan.Status(date).Holiday is { } holiday ? (holiday.Counts, holiday.Offered) : (false, false);

    /// <summary>Reads the answer through the OpenHolidays translation, as a saved answer replayed by wtt-cli import-holidays.</summary>
    private static async Task<PublicHolidayYear> ReadAnswerAsync(string answer, Canton canton, int year)
    {
        var folder = Path.Combine(Path.GetTempPath(), "wtt-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "answer.json");
            await File.WriteAllTextAsync(path, answer);
            return await new PublicHolidayFile(path, TimeProvider.System).ReadAsync(canton, year, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
