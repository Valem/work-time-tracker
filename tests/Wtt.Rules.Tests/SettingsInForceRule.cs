using Wtt.Application.Planning;
using Wtt.Domain.Planning;
using Wtt.Infrastructure.Storage;
using static Wtt.Rules.Tests.Support.Plans;

namespace Wtt.Rules.Tests;

public sealed class SettingsInForceRule
{
    private const string BeforeAnySave = "100 %, Mon Tue Wed Thu Fri working, yearly 20 prefilled, no canton";

    [Fact]
    public async Task BR_013_settings_in_force()
    {
        var rule = new RuleScenarios();
        using var file = new TempDatabase();
        var clock = new FakeClock(new DateTimeOffset(2027, 3, 1, 9, 0, 0, TimeSpan.FromHours(2)));
        var database = file.Open(clock);
        var plans = new SqlitePlanStore(database, clock);
        var holidays = new SqlitePublicHolidayStore(database);
        await holidays.ReplaceAsync(Zurich2027);
        await holidays.ReplaceAsync(Year(
            Canton.ZH,
            2028,
            Holiday("2028-01-01", "Neujahrstag", reach: HolidayReach.Country),
            Holiday("2028-04-24", "Sechseläuten", HolidayKind.Optional, HolidayReach.PartOfCanton, DayPart.Half)));
        var planner = Planner(database, clock);

        await rule.CheckAsync("before any save every date has 100 %, Monday to Friday working, yearly days prefilled and no canton", async () =>
        {
            foreach (var date in new[] { "2026-06-01", "2027-03-01", "2029-12-31" })
            {
                Assert.Equal((date, BeforeAnySave), (date, await InForceAsync(plans, date)));
            }
        });

        await planner.SaveSettingsAsync(2028, Input(100, canton: Canton.ZH));
        await planner.SaveSettingsAsync(2027, Input(80, free: "Fri", yearlyDays: 25, canton: Canton.ZH));

        await rule.CheckAsync("settings saved for a year apply from its 1 January and carry into later years until one has its own", async () =>
        {
            Assert.Equal("80 %, Mon Tue Wed Thu working Fri free, yearly 25, ZH", await InForceAsync(plans, "2027-01-01"));
            Assert.Equal("80 %, Mon Tue Wed Thu working Fri free, yearly 25, ZH", await InForceAsync(plans, "2027-12-31"));
            Assert.Equal("100 %, Mon Tue Wed Thu Fri working, yearly 20 prefilled, ZH", await InForceAsync(plans, "2028-01-01"));
            Assert.Equal("100 %, Mon Tue Wed Thu Fri working, yearly 20 prefilled, ZH", await InForceAsync(plans, "2029-07-01"));
        });

        await rule.CheckAsync("a date before the earliest start keeps the settings before any save", async () =>
        {
            Assert.Equal(BeforeAnySave, await InForceAsync(plans, "2026-12-31"));
            var settings = await planner.GetSettingsAsync(2026);
            Assert.Equal((100, (Canton?)null, (HalfDays?)null), (settings.Pensum, settings.Canton, settings.YearlyDays));
            Assert.Equal(Week(), settings.MondayToFriday);
        });

        await planner.TickAsync(2027, D("2027-04-19"), ticked: true);
        await planner.TickAsync(2028, D("2028-04-24"), ticked: true);
        await planner.SaveSettingsAsync(2028, Input(100, canton: Canton.BE));

        await rule.CheckAsync("saving another canton clears the ticks on the dates those settings apply to, and only those", async () =>
        {
            Assert.Equal(["2027-04-19"], await TicksAsync(plans));
            Assert.Equal("80 %, Mon Tue Wed Thu working Fri free, yearly 25, ZH", await InForceAsync(plans, "2027-06-01"));
            Assert.Equal("100 %, Mon Tue Wed Thu Fri working, yearly 20 prefilled, BE", await InForceAsync(plans, "2028-06-01"));
        });

        await planner.SaveSettingsAsync(2027, Input(90, canton: Canton.ZH));

        await rule.CheckAsync("of several settings with one start the last saved applies, and saving the same canton keeps the ticks", async () =>
        {
            Assert.Equal("90 %, Mon Tue Wed Thu Fri working, yearly 18 prefilled, ZH", await InForceAsync(plans, "2027-06-01"));
            Assert.Equal(["2027-04-19"], await TicksAsync(plans));
        });

        await planner.SaveSettingsAsync(2028, Input(100, canton: Canton.ZH));
        await planner.TickAsync(2028, D("2028-04-24"), ticked: true);
        await planner.SaveSettingsAsync(2027, Input(90, canton: Canton.BE));

        await rule.CheckAsync("the dates those settings apply to end where later saved settings start", async () =>
            Assert.Equal(["2028-04-24"], await TicksAsync(plans)));

        rule.AssertAllHold();
    }

    private static SettingsInput Input(int pensum, string free = "", decimal? yearlyDays = null, Canton? canton = null) =>
        new(pensum, Week(free), yearlyDays is { } days ? Days(days) : null, canton);

    private static async Task<string> InForceAsync(SqlitePlanStore plans, string date)
    {
        var inputs = await plans.ReadAsync(D(date), D(date));
        var settings = new SettingsBook(inputs.Settings).InForceOn(D(date));
        string[] weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri"];
        var working = string.Join(" ", weekdays.Where((_, day) => settings.MondayToFriday[day] == WorkShare.Whole));
        var free = string.Join(" ", weekdays.Where((_, day) => settings.MondayToFriday[day] == WorkShare.None));
        var week = free.Length == 0 ? $"{working} working" : $"{working} working {free} free";
        var yearly = settings.YearlyDays is { } days ? $"{days}" : $"{settings.PrefilledYearlyDays} prefilled";
        return $"{settings.Pensum} %, {week}, yearly {yearly}, {settings.Canton?.ToString() ?? "no canton"}";
    }

    private static async Task<IReadOnlyList<string>> TicksAsync(SqlitePlanStore plans) =>
        [.. (await plans.ReadAsync(D("2000-01-01"), D("2099-12-31"))).Ticks.Order().Select(date => $"{date:yyyy-MM-dd}")];
}
