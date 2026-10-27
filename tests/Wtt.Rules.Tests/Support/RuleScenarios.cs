namespace Wtt.Rules.Tests.Support;

/// <summary>
/// Runs every scenario of one business rule and fails once at the end, naming each scenario that broke it,
/// so one test covers every clause of its rule and still says which clause failed.
/// </summary>
internal sealed class RuleScenarios
{
    private readonly List<string> broken = [];

    public void Check(string scenario, Action assertion)
    {
        try
        {
            assertion();
        }
        catch (Exception failure)
        {
            broken.Add($"{scenario}: {failure.Message}");
        }
    }

    public async Task CheckAsync(string scenario, Func<Task> assertion)
    {
        try
        {
            await assertion();
        }
        catch (Exception failure)
        {
            broken.Add($"{scenario}: {failure.Message}");
        }
    }

    public void AssertAllHold()
    {
        if (broken.Count > 0)
        {
            var separator = Environment.NewLine + "- ";
            Assert.Fail($"{broken.Count} scenario(s) broke the rule:{separator}{string.Join(separator, broken)}");
        }
    }
}
