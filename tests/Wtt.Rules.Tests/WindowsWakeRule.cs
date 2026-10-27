using System.Globalization;
using Wtt.Domain.Facts;
using static Wtt.Rules.Tests.Support.TestRules;

namespace Wtt.Rules.Tests;

public sealed class WindowsWakeRule
{
    private static readonly string[] WindowsReasons =
        ["BatteryDrainBudgetExceeded", "ResumeFromSleep", "ResumeFromHibernate", WakeReason.Unmapped("WakeSource 3 Timer - Windows Update")];

    [Fact]
    public void BR_002_windows_waking_itself_never_counts()
    {
        var rule = new RuleScenarios();

        rule.Check("twenty night wakes inside the Winlogon log add no use", () => AssertNightAddsNoUse(TwentyNightWakes()));

        rule.Check("the same twenty night wakes beyond the Winlogon log add no use", () => AssertNightAddsNoUse(TwentyNightWakes().WinlogonHistoryFrom(null)));

        rule.Check("inside the Winlogon log a keyboard wake awake 30 minutes without an unlock adds no use", () => Expect.Uses(
            new Scenario()
                .Use("2026-10-19 08:00", "2026-10-19 17:00")
                .StandbyExit("2026-10-20 07:00", WakeReason.InputKeyboard)
                .StandbyEnter("2026-10-20 07:30"),
            ("2026-10-19 08:00", "2026-10-19 17:00")));

        foreach (var reason in WindowsReasons)
        {
            rule.Check($"beyond the Winlogon log a wake for '{reason}' awake an hour adds no use", () => Expect.Uses(
                new Scenario()
                    .StandbyExit("2026-08-03 03:00", reason)
                    .StandbyEnter("2026-08-03 04:00")
                    .WinlogonHistoryFrom(null)));
        }

        rule.Check("a live resume without input before the next standby adds no use", () => Expect.Uses(
            LiveNight(night => night
                .StandbyExit("2026-10-21 03:00", source: FactSource.Live)
                .StandbyEnter("2026-10-21 03:30", FactSource.Live)),
            ("2026-10-20 08:00", "2026-10-20 17:00"),
            ("2026-10-21 08:00:30", "2026-10-21 12:00")));

        rule.Check("a live resume whose only input is followed by standby within 2 minutes adds no use", () => Expect.Uses(
            LiveNight(night => night
                .StandbyExit("2026-10-21 03:40", source: FactSource.Live)
                .LastInput("2026-10-21 03:40:10")
                .StandbyEnter("2026-10-21 03:41:30", FactSource.Live)),
            ("2026-10-20 08:00", "2026-10-20 17:00"),
            ("2026-10-21 08:00:30", "2026-10-21 12:00")));

        rule.AssertAllHold();
    }

    private static void AssertNightAddsNoUse(Scenario night)
    {
        var presence = night.Presence();
        Assert.Equal(2, presence.Uses.Count);
        Assert.Equal(20, Assert.Single(presence.Pauses).IgnoredWakes.Count);
        Assert.Equal(night.At("2026-10-20 08:00"), night.Day("2026-10-20", Example).Start);
    }

    /// <summary>Monday 08:00–17:00, twenty wakes of one minute every 25 minutes from 22:00, Tuesday 08:00–17:00.</summary>
    private static Scenario TwentyNightWakes()
    {
        var night = new Scenario().Use("2026-10-19 08:00", "2026-10-19 17:00");
        var wake = new DateTime(2026, 10, 19, 22, 0, 0);
        for (var i = 0; i < 20; i++)
        {
            var reason = i % 2 == 0 ? WakeReason.InputKeyboard : WakeReason.PowerButton;
            night.WindowsWake(Format(wake), Format(wake.AddMinutes(1)), reason);
            wake = wake.AddMinutes(25);
        }

        return night.Use("2026-10-20 08:00", "2026-10-20 17:00");
    }

    /// <summary>A recorded evening ending in standby without a lock, the given night, and a morning resumed at 08:00 with input at 08:00:30.</summary>
    private static Scenario LiveNight(Func<Scenario, Scenario> night) => night(new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-21 12:30")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Ends(FactKind.StandbyEnter, "2026-10-20 17:00", FactSource.Live, EndCause.Lid))
        .StandbyExit("2026-10-21 08:00", source: FactSource.Live)
        .LastInput("2026-10-21 08:00:30")
        .Lock("2026-10-21 12:00", FactSource.Live, EndCause.UserLock);

    private static string Format(DateTime local) => local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
