using System.Diagnostics.CodeAnalysis;

namespace Wtt.Cli;

internal enum CliCommand
{
    Help,
    Weeks,
    StartTest,
}

internal sealed record CliOptions(CliCommand Command, string? DatabasePath, bool Import)
{
    public static bool TryParse(string[] args, [NotNullWhen(true)] out CliOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        if (args.Length == 0)
        {
            options = new CliOptions(CliCommand.Help, null, Import: false);
            return true;
        }

        CliCommand? command = args[0] switch
        {
            "weeks" => CliCommand.Weeks,
            "start-test" => CliCommand.StartTest,
            "help" or "--help" or "-h" or "/?" => CliCommand.Help,
            _ => null,
        };
        if (command is null)
        {
            error = $"Unknown command '{args[0]}'.";
            return false;
        }

        string? databasePath = null;
        var import = true;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db" when i + 1 < args.Length:
                    databasePath = args[++i];
                    break;
                case "--no-import":
                    import = false;
                    break;
                default:
                    error = $"Unknown option '{args[i]}'.";
                    return false;
            }
        }

        options = new CliOptions(command.Value, databasePath, import);
        return true;
    }
}
