using Wtt.Domain.Facts;
using static Wtt.Rules.Tests.Support.SyntheticEvent;
using static Wtt.Rules.Tests.Support.Users;

namespace Wtt.Rules.Tests;

public sealed class SourcesRule
{
    [Fact]
    public void BR_004_inside_coverage_only_live_facts_count_outside_it_this_users_history()
    {
        var rule = new RuleScenarios();

        rule.Check("inside coverage a history lock and unlock after the Recorder's start are ignored", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-20 07:59", "2026-10-20 17:30")
                .SignIn("2026-10-20 07:59", FactSource.Live)
                .Unlock("2026-10-20 08:00", FactSource.Live)
                .Lock("2026-10-20 10:00")
                .Unlock("2026-10-20 10:30")
                .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock),
            ("2026-10-20 07:59", "2026-10-20 17:00")));

        rule.Check("outside coverage history facts count", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-20 07:59", "2026-10-20 10:00")
                .SignIn("2026-10-20 07:59", FactSource.Live)
                .Unlock("2026-10-20 08:00", FactSource.Live)
                .Lock("2026-10-20 12:00")
                .Unlock("2026-10-20 13:00")
                .Covered("2026-10-20 14:00", "2026-10-20 17:30")
                .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock),
            ("2026-10-20 07:59", "2026-10-20 12:00"),
            ("2026-10-20 13:00", "2026-10-20 17:00")));

        rule.Check("the stretch from sign-in to the Recorder's start is filled from history inside coverage", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-20 08:00", "2026-10-20 17:30")
                .SignIn("2026-10-20 08:00", FactSource.Live)
                .Lock("2026-10-20 08:01")
                .Unlock("2026-10-20 08:02")
                .IdleTimeout("2026-10-20 08:03")
                .Lock("2026-10-20 10:00")
                .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock),
            ("2026-10-20 08:00", "2026-10-20 08:01"),
            ("2026-10-20 08:02", "2026-10-20 17:00")));

        var othersAndMachineEvents = History.Read(
            SignIn(1, "2026-10-20T06:00:00Z", Me, 1),
            Winlogon811(1, "2026-10-20T10:00:00Z", Me, History.SensLock),
            Winlogon811(2, "2026-10-20T10:30:00Z", Other, History.SensUnlock),
            Winlogon811(3, "2026-10-20T11:00:00Z", Me, History.SensUnlock),
            SignOut(2, "2026-10-20T15:00:00Z", Me, 1),
            SignIn(3, "2026-10-20T16:00:00Z", Other, 2),
            Winlogon811(4, "2026-10-20T16:00:30Z", Other, History.SensUnlock),
            StandbyEnter(4, "2026-10-20T17:00:00Z", 12),
            StandbyExit(5, "2026-10-20T17:30:00Z", 31),
            StandbyEnter(6, "2026-10-20T18:00:00Z", 12),
            SignOut(7, "2026-10-20T19:00:00Z", Other, 2));

        rule.Check("history keeps only facts inside this user's own sign-in sessions", () => Assert.Equal(
            ["08:00:00 SignIn", "12:00:00 Lock", "13:00:00 Unlock", "17:00:00 SignOut"],
            othersAndMachineEvents.Select(History.Describe)));

        rule.Check("another user's unlock while this user is locked starts no use", () => Expect.Uses(
            new Scenario().With(othersAndMachineEvents),
            ("2026-10-20 08:00", "2026-10-20 12:00"),
            ("2026-10-20 13:00", "2026-10-20 17:00")));

        rule.AssertAllHold();
    }
}
