using Wtt.Application.History;
using Wtt.Application.Ports;
using Wtt.Application.Queries;
using Wtt.Domain.Presence;
using Wtt.Domain.Time;
using Wtt.Domain.Workdays;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Storage;
using static Wtt.Infrastructure.Tests.SyntheticEvent;
using static Wtt.Infrastructure.Tests.Users;

namespace Wtt.Infrastructure.Tests;

/// <summary>Synthetic event logs replayed through the importer into a real data file (AC-1.12).</summary>
public sealed class HistoryImportReplayTests : IDisposable
{
    private static readonly TimeZoneInfo CentralEurope = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private readonly TempDatabase file = new();
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(2)));

    public void Dispose() => file.Dispose();

    [Fact]
    public async Task AC_1_12_importing_twice_adds_no_duplicate()
    {
        var database = file.Open(clock);
        var reader = FixtureEventRecordReader.FromFile("two-users-day.xml");

        var first = await Import(database, reader);
        var second = await Import(database, reader);
        var everything = await new SqliteFactJournal(database).ReadAsync(Interval.All);

        Assert.Equal(10, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(10, everything.Count);
    }

    [Fact]
    public async Task AC_1_12_importing_again_after_the_log_was_cleared_adds_no_duplicate_and_keeps_older_facts()
    {
        var database = file.Open(clock);
        await Import(database, new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T06:00:00Z", Me, 1),
            Winlogon811(1, "2026-10-20T06:00:30Z", Me, 5),
            Winlogon811(2, "2026-10-20T15:00:00Z", Me, 4),
        ]));
        var clearedLog = new FixtureEventRecordReader([
            Winlogon811(1, "2026-10-21T06:00:30Z", Me, 5),
            Winlogon811(2, "2026-10-21T06:30:00Z", Me, 4),
        ]);

        var afterClearing = await Import(database, clearedLog);
        var again = await Import(database, clearedLog);
        var everything = await new SqliteFactJournal(database).ReadAsync(Interval.All);

        Assert.Equal(2, afterClearing.Added);
        Assert.Equal(0, again.Added);
        Assert.Equal(5, everything.Count);
        Assert.Equal(everything.Count, everything.Select(fact => fact.SourceKey).Distinct().Count());
    }

    [Fact]
    public async Task AC_1_12_another_users_sessions_on_the_same_laptop_add_nothing()
    {
        var database = file.Open(clock);
        var othersEvening = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T16:00:00Z", Other, 2),
            Winlogon811(1, "2026-10-20T16:00:30Z", Other, 5),
            StandbyEnter(2, "2026-10-20T17:00:00Z", 12),
            StandbyExit(3, "2026-10-20T18:00:00Z", 31),
            Winlogon811(2, "2026-10-20T18:00:10Z", Other, 4),
            SignOut(4, "2026-10-20T19:00:00Z", Other, 2),
        ]);

        var result = await Import(database, othersEvening, Me);

        Assert.Equal(0, result.Added);
        Assert.False((await DayAsync(database, new DateOnly(2026, 10, 20))).HasUse);
    }

    [Fact]
    public async Task The_replayed_day_reads_as_the_person_used_the_laptop()
    {
        var database = file.Open(clock);
        await Import(database, FixtureEventRecordReader.FromFile("two-users-day.xml"));

        var tuesday = await DayAsync(database, new DateOnly(2026, 10, 20));

        var local = TimeSpan.FromHours(2);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 7, 56, 0, local), tuesday.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 17, 1, 0, local), tuesday.End);
        var lunch = Assert.Single(tuesday.Pauses);
        Assert.Equal(TimeSpan.FromHours(1), lunch.Duration);
        Assert.Equal(2, lunch.IgnoredWakes.Count);
        Assert.True(tuesday.IsRebuilt);
    }

    [Fact]
    public async Task CR_002_beyond_winlogon_a_power_button_resume_from_hibernate_starts_the_day()
    {
        var database = file.Open(clock);
        await Import(database, new FixtureEventRecordReader([
            SignIn(1, "2026-08-03T15:00:00Z", Me, 1),
            StandbyEnter(2, "2026-08-03T16:00:00Z", 12),
            StandbyExit(3, "2026-08-04T00:00:00Z", 55),
            Sleep(4, "2026-08-04T00:00:30Z"),
            WakeFromSleep(5, "2026-08-04T06:00:10Z", "2026-08-04T06:00:00Z", 1, "Power Button"),
            ResumeFromSleep(6, "2026-08-04T06:00:02Z"),
            StandbyExit(7, "2026-08-04T06:00:03Z", 25),
            StandbyEnter(8, "2026-08-04T10:00:00Z", 12),
            StandbyExit(9, "2026-08-04T11:00:00Z", 1),
            StandbyEnter(10, "2026-08-04T15:00:00Z", 12),
        ]));

        var tuesday = await DayAsync(database, new DateOnly(2026, 8, 4));

        var local = TimeSpan.FromHours(2);
        Assert.Equal(new DateTimeOffset(2026, 8, 4, 8, 0, 0, local), tuesday.Start);
        Assert.Equal(new DateTimeOffset(2026, 8, 4, 17, 0, 0, local), tuesday.End);
        Assert.Equal(TimeSpan.FromHours(1), tuesday.Break);
        Assert.Equal(TimeSpan.FromHours(8), tuesday.Total);
    }

    [Fact]
    public async Task CR_003_after_a_crash_the_afternoon_is_kept_until_windows_stopped()
    {
        var database = file.Open(clock);
        await Import(database, new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T05:59:00Z", Me, 1),
            Winlogon811(1, "2026-10-20T10:00:00Z", Me, 4),
            Winlogon811(2, "2026-10-20T10:45:00Z", Me, 5),
            Boot(2, "2026-10-20T13:05:00Z"),
            UnexpectedShutdown(3, "2026-10-20T13:05:20Z", new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero)),
            SignIn(4, "2026-10-20T13:07:00Z", Me, 1),
            Winlogon811(3, "2026-10-20T15:00:00Z", Me, 4),
        ]));

        var tuesday = await DayAsync(database, new DateOnly(2026, 10, 20));

        var local = TimeSpan.FromHours(2);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 12, 0, 0, local), tuesday.Lunch.Pause!.Start);
        Assert.Equal(TimeSpan.FromMinutes(45), tuesday.Break);
        Assert.Contains(tuesday.Pauses, pause => pause.Start == new DateTimeOffset(2026, 10, 20, 15, 0, 0, local));
        Assert.Equal(new TimeSpan(8, 16, 0), tuesday.Total);
        Assert.True(tuesday.IsRebuilt);
    }

    private static Task<HistoryImportResult> Import(SqliteDatabase database, IEventRecordReader reader, UserSid? user = null) =>
        new HistoryImport(
            new EventLogHistorySource(reader, CentralEurope),
            new SqliteFactJournal(database),
            new SqliteHistoryCursorStore(database))
        .ImportAsync(user ?? Me);

    private Task<Workday> DayAsync(SqliteDatabase database, DateOnly date)
    {
        var journal = new SqliteFactJournal(database);
        var coverage = new SqliteCoverageLog(database);
        var queries = new WorkTimeQueries(
            new PresenceReader(journal, coverage, new PresenceBuilder(), clock),
            coverage,
            new SqliteLunchRules(database, clock),
            new WorkdayCalculator(),
            clock);
        return queries.GetDayAsync(date);
    }
}
