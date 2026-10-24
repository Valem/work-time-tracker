using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Infrastructure.History;
using static Wtt.Infrastructure.Tests.SyntheticEvent;
using static Wtt.Infrastructure.Tests.Users;

namespace Wtt.Infrastructure.Tests;

public sealed class HistoryTranslatorTests
{
    private static readonly TimeZoneInfo CentralEurope = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void The_synthetic_day_yields_only_this_users_facts_with_local_offsets()
    {
        var batch = Read(FixtureEventRecordReader.FromFile("two-users-day.xml"), HistoryCursor.Beginning, Me);

        Assert.Equal(
            [
                "07:56:00 SignIn", "07:56:30 Unlock", "12:00:00 Lock", "12:05:00 StandbyEnter IdleTimeout",
                "12:30:00 StandbyExit InputKeyboard", "12:31:00 StandbyEnter BatteryDrainBudgetExceeded",
                "12:59:30 StandbyExit PowerButton", "13:00:00 Unlock", "17:01:00 Shutdown RestartRequested",
                "17:01:30 SignOut",
            ],
            batch.Facts.Select(Describe));
        Assert.All(batch.Facts, fact => Assert.Equal(TimeSpan.FromHours(2), fact.At.Offset));
    }

    [Fact]
    public void Another_users_sessions_add_nothing()
    {
        var batch = Read(FixtureEventRecordReader.FromFile("two-users-day.xml"), HistoryCursor.Beginning, new UserSid("S-1-5-21-9-9-9-9999"));

        Assert.Empty(batch.Facts);
    }

    [Fact]
    public void Only_the_sens_subscribers_lock_and_unlock_codes_count()
    {
        var reader = new FixtureEventRecordReader([
            Winlogon811(1, "2026-10-20T06:00:00Z", Me, 4),
            Winlogon811(2, "2026-10-20T06:00:00Z", Me, 4, "TermSrv"),
            Winlogon811(3, "2026-10-20T06:01:00Z", Me, 12),
            Winlogon811(4, "2026-10-20T06:02:00Z", Me, 5),
        ]);

        var batch = Read(reader, HistoryCursor.Beginning, Me);

        Assert.Equal([FactKind.Lock, FactKind.Unlock], batch.Facts.Select(fact => fact.Kind));
        Assert.All(batch.Facts, fact => Assert.Equal(FactSource.WinlogonHistory, fact.Source));
    }

    [Fact]
    public void Offsets_follow_daylight_saving_at_each_events_own_instant()
    {
        var reader = new FixtureEventRecordReader([
            Winlogon811(1, "2026-10-25T00:30:00Z", Me, 5),
            Winlogon811(2, "2026-10-25T07:00:00Z", Me, 4),
        ]);

        var facts = Read(reader, HistoryCursor.Beginning, Me).Facts;

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2)), facts[0].At);
        Assert.Equal(TimeSpan.FromHours(2), facts[0].At.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.FromHours(1)), facts[1].At);
        Assert.Equal(TimeSpan.FromHours(1), facts[1].At.Offset);
    }

    [Fact]
    public void The_next_cursor_reads_again_with_an_overlap_and_remembers_the_open_session()
    {
        var reader = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T06:00:00Z", Me, 1),
            StandbyEnter(2, "2026-10-20T10:00:00Z", 12),
        ]);

        var next = Read(reader, HistoryCursor.Beginning, Me).Next;

        Assert.Equal(DateTimeOffset.Parse("2026-10-20T09:50:00Z", System.Globalization.CultureInfo.InvariantCulture), next.ReadFrom);
        Assert.Equal("1", next.OpenSession);
    }

    [Fact]
    public void A_session_open_at_the_cursor_lets_machine_events_count_without_seeing_the_sign_in()
    {
        var reader = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T06:00:00Z", Me, 1),
            StandbyEnter(2, "2026-10-20T10:00:00Z", 12),
        ]);
        var cursor = new HistoryCursor(DateTimeOffset.Parse("2026-10-20T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "1");

        var batch = Read(reader, cursor, Me);

        Assert.Equal(FactKind.StandbyEnter, Assert.Single(batch.Facts).Kind);
    }

    [Fact]
    public void Machine_events_before_this_users_first_sign_in_and_after_a_boot_do_not_count()
    {
        var reader = new FixtureEventRecordReader([
            StandbyExit(1, "2026-10-20T05:00:00Z", 31),
            SignIn(2, "2026-10-20T06:00:00Z", Me, 1),
            StandbyEnter(3, "2026-10-20T10:00:00Z", 12),
            Boot(4, "2026-10-20T11:00:00Z"),
            StandbyExit(5, "2026-10-20T11:05:00Z", 31),
        ]);

        var batch = Read(reader, HistoryCursor.Beginning, Me);

        Assert.Equal(["08:00:00 SignIn", "12:00:00 StandbyEnter IdleTimeout", "13:00:00 Boot"], batch.Facts.Select(Describe));
    }

    [Fact]
    public void CR_003_the_real_6008_binary_holds_the_stop_time_in_utc_after_the_local_one()
    {
        var stoppedAt = UnexpectedShutdownTime.FromBinary(
            "EA070A0006000A000600070017008F03EA070A0006000A000400070017008F03DC0500003C00000001000000DC050000010000001E00000001000000F3FFFFFF");

        Assert.Equal(new DateTimeOffset(2026, 10, 10, 4, 7, 23, 911, TimeSpan.Zero), stoppedAt);
        Assert.Null(UnexpectedShutdownTime.FromBinary("EA07"));
        Assert.Null(UnexpectedShutdownTime.FromBinary(null));
    }

    [Fact]
    public void CR_003_an_unexpected_shutdown_ends_this_users_session_at_its_stop_time()
    {
        var reader = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T05:59:00Z", Me, 1),
            Boot(2, "2026-10-20T13:05:00Z"),
            UnexpectedShutdown(3, "2026-10-20T13:05:20Z", new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero)),
            SignIn(4, "2026-10-20T13:07:00Z", Me, 1),
        ]);

        var facts = Read(reader, HistoryCursor.Beginning, Me).Facts;

        Assert.Equal(["07:59:00 SignIn", "15:00:00 Shutdown UnexpectedShutdown", "15:05:00 Boot", "15:07:00 SignIn"], facts.OrderBy(fact => fact.At).Select(Describe));
    }

    [Fact]
    public void CR_003_an_unexpected_shutdown_while_this_user_was_signed_out_adds_nothing()
    {
        var reader = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T05:59:00Z", Me, 1),
            SignOut(2, "2026-10-20T12:00:00Z", Me, 1),
            Boot(3, "2026-10-20T13:05:00Z"),
            UnexpectedShutdown(4, "2026-10-20T13:05:20Z", new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero)),
        ]);

        var facts = Read(reader, HistoryCursor.Beginning, Me).Facts;

        Assert.DoesNotContain(facts, fact => fact.Detail.Reason == "UnexpectedShutdown");
    }

    [Fact]
    public void Reading_again_from_the_next_cursor_decides_the_overlap_exactly_as_the_first_read()
    {
        var reader = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T06:00:00Z", Me, 1),
            StandbyEnter(2, "2026-10-20T10:58:00Z", 12),
            Boot(3, "2026-10-20T11:00:00Z"),
            StandbyExit(4, "2026-10-20T11:01:00Z", 31),
            SignIn(5, "2026-10-20T11:02:00Z", Me, 2),
            StandbyEnter(6, "2026-10-20T11:05:00Z", 12),
        ]);
        var first = Read(reader, HistoryCursor.Beginning, Me);

        var again = Read(reader, first.Next, Me);

        Assert.Equal(
            first.Facts.Where(fact => fact.At >= first.Next.ReadFrom).Select(fact => fact.SourceKey),
            again.Facts.Select(fact => fact.SourceKey));
        Assert.Equal(first.Next, again.Next);
    }

    [Fact]
    public void Source_keys_combine_channel_record_id_and_time_so_a_cleared_log_never_collides()
    {
        var before = Read(new FixtureEventRecordReader([Winlogon811(7, "2026-10-20T06:00:00Z", Me, 5)]), HistoryCursor.Beginning, Me);
        var afterClearing = Read(new FixtureEventRecordReader([Winlogon811(7, "2026-10-27T06:00:00Z", Me, 5)]), HistoryCursor.Beginning, Me);

        Assert.Equal("Microsoft-Windows-Winlogon/Operational#7@2026-10-20T06:00:00.0000000Z", before.Facts[0].SourceKey);
        Assert.NotEqual(before.Facts[0].SourceKey, afterClearing.Facts[0].SourceKey);
    }

    [Theory]
    [InlineData(1, "PowerButton")]
    [InlineData(15, "Lid")]
    [InlineData(31, "InputKeyboard")]
    [InlineData(32, "InputMouse")]
    [InlineData(33, "InputTouchpad")]
    [InlineData(55, "BatteryDrainBudgetExceeded")]
    [InlineData(4, "UserInput")]
    [InlineData(34, "InputPen")]
    [InlineData(36, "InputHid")]
    [InlineData(54, "InputTouch")]
    [InlineData(16777220, "UserInput")]
    [InlineData(16777228, "IdleTimeout")]
    [InlineData(53, "Unmapped:KernelPower 53")]
    public void CR_002_standby_reason_codes_are_named_from_their_low_16_bits(int code, string name)
    {
        var reader = new FixtureEventRecordReader([SignIn(1, "2026-10-20T06:00:00Z", Me, 1), StandbyExit(2, "2026-10-20T07:00:00Z", code)]);

        var exit = Read(reader, HistoryCursor.Beginning, Me).Facts[1];

        Assert.Equal(name, exit.Detail.Reason);
        Assert.Equal(code.ToString(System.Globalization.CultureInfo.InvariantCulture), exit.Detail.RawReason);
    }

    [Theory]
    [InlineData("Power Button", "PowerButton")]
    [InlineData("Device -ACPI Lid", "Lid")]
    [InlineData("Device -HID Keyboard Device", "InputKeyboard")]
    [InlineData("Device -HID-compliant mouse", "InputMouse")]
    [InlineData("Device -Precision Touch Pad", "InputTouchpad")]
    [InlineData("Timer - Windows Update", "Unmapped:WakeSource 3 Timer - Windows Update")]
    public void CR_002_a_sleep_resume_takes_its_reason_and_time_from_the_wake_source(string text, string name)
    {
        var reader = new FixtureEventRecordReader([
            SignIn(1, "2026-10-20T06:00:00Z", Me, 1),
            WakeFromSleep(2, "2026-10-20T07:00:09Z", "2026-10-20T07:00:00.5Z", 3, text),
        ]);

        var wake = Read(reader, HistoryCursor.Beginning, Me).Facts[1];

        Assert.Equal(FactKind.StandbyExit, wake.Kind);
        Assert.Equal(name, wake.Detail.Reason);
        Assert.Equal($"3|{text}", wake.Detail.RawReason);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 9, 0, 0, 500, TimeSpan.FromHours(2)), wake.At);
    }

    private static HistoryBatch Read(IEventRecordReader reader, HistoryCursor cursor, UserSid user) =>
        new EventLogHistorySource(reader, CentralEurope).ReadSinceAsync(cursor, user).GetAwaiter().GetResult();

    private static string Describe(ActivityFact fact) =>
        $"{fact.At:HH:mm:ss} {fact.Kind}{(fact.Detail.Reason is { } reason ? " " + reason : string.Empty)}";
}
