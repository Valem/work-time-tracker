using System.Security.Principal;
using Wtt.Application.Ports;
using Wtt.Infrastructure.History;
using Wtt.Infrastructure.Storage;

namespace Wtt.Cli;

/// <summary>What the CLI takes from the machine it runs on.</summary>
internal sealed record CliEnvironment(
    TimeProvider Clock,
    UserSid User,
    IHistorySource History,
    string DefaultDatabasePath,
    TimeSpan? DataFileBusyTimeout = null)
{
    /// <summary>
    /// The CLI keeps its own data file next to where the app's will live, so its first run is never
    /// taken for the app's install date.
    /// </summary>
    public static CliEnvironment ThisWindowsUser() => new(
        TimeProvider.System,
        new UserSid(WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("No Windows user.")),
        new EventLogHistorySource(TimeZoneInfo.Local),
        Path.Combine(SqliteDatabase.DefaultDirectory, "wtt-cli.db"));
}
