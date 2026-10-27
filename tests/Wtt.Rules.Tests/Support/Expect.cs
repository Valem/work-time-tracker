namespace Wtt.Rules.Tests.Support;

internal static class Expect
{
    /// <summary>The use spans the scenario's presence holds, in order, as local times of the scenario.</summary>
    public static void Uses(Scenario scenario, params (string Start, string End)[] expected) =>
        Assert.Equal(
            expected.Select(use => (scenario.At(use.Start), scenario.At(use.End))),
            scenario.Presence().Uses.Select(use => (use.Start, use.End)));
}
