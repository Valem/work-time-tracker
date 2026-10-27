using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Wtt.Application.Ports;

namespace Wtt.Cli;

internal enum CliCommand
{
    Help,
    Weeks,
    StartTest,
    ImportXml,
}

/// <param name="EventFile">import-xml: the file of Windows event XML to import.</param>
/// <param name="User">import-xml: the account whose sessions count.</param>
internal sealed partial record CliOptions(CliCommand Command, string? DatabasePath, bool Import, string? EventFile = null, UserSid? User = null)
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
            "import-xml" => CliCommand.ImportXml,
            "help" or "--help" or "-h" or "/?" => CliCommand.Help,
            _ => null,
        };
        if (command is null)
        {
            error = $"Unknown command '{args[0]}'.";
            return false;
        }

        var importsXml = command == CliCommand.ImportXml;
        string? databasePath = null;
        string? eventFile = null;
        string? user = null;
        var import = !importsXml;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db" when i + 1 < args.Length:
                    databasePath = args[++i];
                    if (string.IsNullOrWhiteSpace(databasePath))
                    {
                        error = "--db needs a file name.";
                        return false;
                    }

                    break;
                case "--no-import" when !importsXml:
                    import = false;
                    break;
                case "--user" when importsXml && i + 1 < args.Length:
                    user = args[++i];
                    break;
                case var file when importsXml && eventFile is null && !file.StartsWith("--", StringComparison.Ordinal):
                    eventFile = file;
                    break;
                default:
                    error = $"Unknown option '{args[i]}'.";
                    return false;
            }
        }

        if (importsXml && !TryCheckImportXml(eventFile, databasePath, user, out error))
        {
            return false;
        }

        options = new CliOptions(command.Value, databasePath, import, eventFile, user is null ? null : new UserSid(user));
        return true;
    }

    private static bool TryCheckImportXml(string? eventFile, string? databasePath, string? user, out string error)
    {
        error = string.IsNullOrWhiteSpace(eventFile) ? "import-xml needs the file of event XML to read."
            : databasePath is null ? "import-xml needs --db <file>: it never falls back to a data file of real use."
            : user is null || !Sid().IsMatch(user) ? "import-xml needs --user <sid>: the account whose sessions count, such as a fixture's synthetic SID."
            : string.Empty;
        return error.Length == 0;
    }

    [GeneratedRegex(@"^S-1-\d+(-\d+)+$", RegexOptions.IgnoreCase)]
    private static partial Regex Sid();
}
