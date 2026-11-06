namespace Wtt.App;

/// <summary>What the command line may change: <c>--data &lt;folder&gt;</c> points the app at another data folder.</summary>
internal sealed record AppOptions(string? DataFolder)
{
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        string? dataFolder = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--data", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                dataFolder = Path.GetFullPath(args[++i]);
            }
        }

        return new AppOptions(dataFolder);
    }
}
