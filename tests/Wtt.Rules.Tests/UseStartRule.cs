using Wtt.Domain.Facts;
using static Wtt.Domain.Facts.WakeReason;

namespace Wtt.Rules.Tests;

public sealed class UseStartRule
{
    private static readonly string[] PersonReasons =
        [UserInput, InputKeyboard, InputMouse, InputTouchpad, InputPen, InputHid, InputTouch, Fingerprint, PowerButton, Lid];

    [Fact]
    public void BR_001_use_starts_at_an_unlock_a_sign_in_or_a_proven_resume()
    {
        var rule = new RuleScenarios();

        rule.Check("an unlock starts use", () => Expect.Uses(
            new Scenario().Unlock("2026-10-20 08:00").Lock("2026-10-20 12:00"),
            ("2026-10-20 08:00", "2026-10-20 12:00")));

        rule.Check("a sign-in starts use", () => Expect.Uses(
            new Scenario().SignIn("2026-10-20 08:00").Lock("2026-10-20 12:00"),
            ("2026-10-20 08:00", "2026-10-20 12:00")));

        rule.Check("after a resume without lock screen, use starts at the first real input when the laptop then stays awake and unlocked 2:01", () => Expect.Uses(
            ResumeWithoutLockScreen("2026-10-20 12:53:01", "2026-10-20 12:51:00"),
            ("2026-10-20 08:00", "2026-10-20 12:00"),
            ("2026-10-20 12:51:00", "2026-10-20 12:53:01")));

        rule.Check("after a resume without lock screen, input followed by exactly 2:00 awake starts no use", () => Expect.Uses(
            ResumeWithoutLockScreen("2026-10-20 12:53:00", "2026-10-20 12:51:00"),
            ("2026-10-20 08:00", "2026-10-20 12:00")));

        rule.Check("after a resume without lock screen, use starts at the first of several inputs, not at a later one", () => Expect.Uses(
            ResumeWithoutLockScreen("2026-10-20 12:53:30", "2026-10-20 12:51:00", "2026-10-20 12:52:00"),
            ("2026-10-20 08:00", "2026-10-20 12:00"),
            ("2026-10-20 12:51:00", "2026-10-20 12:53:30")));

        rule.Check("after a resume without lock screen, a lock within 2 minutes of the first input cancels that start; the unlock starts use", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-19 17:59", "2026-10-20 12:30")
                .Unlock("2026-10-19 18:00", FactSource.Live)
                .Ends(FactKind.StandbyEnter, "2026-10-19 18:30", FactSource.Live, EndCause.Lid)
                .StandbyExit("2026-10-20 07:58", source: FactSource.Live)
                .LastInput("2026-10-20 07:58:10")
                .Lock("2026-10-20 07:59", FactSource.Live, EndCause.UserLock)
                .Unlock("2026-10-20 08:30", FactSource.Live)
                .Lock("2026-10-20 12:00", FactSource.Live, EndCause.UserLock),
            ("2026-10-19 18:00", "2026-10-19 18:30"),
            ("2026-10-20 08:30", "2026-10-20 12:00")));

        rule.Check("input after a resume onto the lock screen starts no use; the unlock does", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-20 07:59", "2026-10-20 18:00")
                .Unlock("2026-10-20 08:00", FactSource.Live)
                .Lock("2026-10-20 12:00", FactSource.Live, EndCause.UserLock)
                .StandbyEnter("2026-10-20 12:01", FactSource.Live)
                .StandbyExit("2026-10-20 12:50", source: FactSource.Live)
                .LastInput("2026-10-20 12:50:30")
                .StandbyEnter("2026-10-20 13:30", FactSource.Live)
                .Unlock("2026-10-20 14:00", FactSource.Live)
                .Lock("2026-10-20 17:00", FactSource.Live, EndCause.UserLock),
            ("2026-10-20 08:00", "2026-10-20 12:00"),
            ("2026-10-20 14:00", "2026-10-20 17:00")));

        rule.Check("real input in an unlocked session extends use up to that input", () => Expect.Uses(
            new Scenario()
                .Covered("2026-10-20 07:59", "2026-10-20 18:00")
                .Unlock("2026-10-20 08:00", FactSource.Live)
                .LastInput("2026-10-20 12:03")
                .Lock("2026-10-20 12:08", FactSource.Live, EndCause.IdleLock, TimeSpan.FromMinutes(10)),
            ("2026-10-20 08:00", "2026-10-20 12:03")));

        foreach (var reason in PersonReasons)
        {
            rule.Check($"beyond the Winlogon log a {reason} standby exit followed by 2:01 awake counts as an unlock", () => Expect.Uses(
                Wake(reason, backInStandby: "2026-08-03 08:02:01").WinlogonHistoryFrom(null),
                ("2026-08-03 08:00:00", "2026-08-03 08:02:01")));

            rule.Check($"beyond the Winlogon log a {reason} standby exit followed by exactly 2:00 awake starts no use", () => Expect.Uses(
                Wake(reason, backInStandby: "2026-08-03 08:02:00").WinlogonHistoryFrom(null)));
        }

        rule.Check("a person-reason standby exit older than the oldest Winlogon fact kept is beyond the log", () => Expect.Uses(
            Wake(PowerButton, backInStandby: "2026-08-03 09:00:00").WinlogonHistoryFrom(Scenario.Local("2026-09-14 08:00", RecorderRig.Local)),
            ("2026-08-03 08:00:00", "2026-08-03 09:00:00")));

        rule.Check("inside the Winlogon log a person-reason standby exit never counts as an unlock", () => Expect.Uses(
            Wake(PowerButton, backInStandby: "2026-08-03 09:00:00").WinlogonHistoryFrom(Scenario.Local("2026-08-01 08:00", RecorderRig.Local))));

        rule.Check("beyond the Winlogon log a standby exit without a person reason never counts as an unlock", () => Expect.Uses(
            Wake("BatteryDrainBudgetExceeded", backInStandby: "2026-08-03 09:00:00").WinlogonHistoryFrom(null)));

        rule.AssertAllHold();
    }

    /// <summary>Unlocked use, the lid closed at 12:00 without a lock, back at 12:50 with the given inputs, then standby again.</summary>
    private static Scenario ResumeWithoutLockScreen(string backInStandby, params string[] inputs)
    {
        var day = new Scenario()
            .Covered("2026-10-20 07:59", "2026-10-20 18:00")
            .Unlock("2026-10-20 08:00", FactSource.Live)
            .Ends(FactKind.StandbyEnter, "2026-10-20 12:00", FactSource.Live, EndCause.Lid)
            .StandbyExit("2026-10-20 12:50", source: FactSource.Live);
        foreach (var input in inputs)
        {
            day.LastInput(input);
        }

        return day.StandbyEnter(backInStandby, FactSource.Live);
    }

    private static Scenario Wake(string reason, string backInStandby) => new Scenario()
        .StandbyExit("2026-08-03 08:00:00", reason)
        .StandbyEnter(backInStandby);
}
