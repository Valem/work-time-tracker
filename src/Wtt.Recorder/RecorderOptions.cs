using Wtt.Infrastructure;

namespace Wtt.Recorder;

/// <summary>
/// The Recorder runs without arguments. <c>--data-dir &lt;folder&gt;</c> points it at another data folder,
/// for a trial run that must not touch the person's data.
/// </summary>
internal sealed record RecorderOptions(DataFolder Data)
{
    public static bool TryParse(IReadOnlyList<string> args, out RecorderOptions options)
    {
        options = new RecorderOptions(DataFolder.Default);
        if (args.Count == 0)
        {
            return true;
        }

        if (args.Count == 2 && args[0] == "--data-dir" && !string.IsNullOrWhiteSpace(args[1]))
        {
            options = new RecorderOptions(new DataFolder(args[1]));
            return true;
        }

        return false;
    }
}
