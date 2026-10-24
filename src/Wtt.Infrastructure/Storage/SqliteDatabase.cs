using Microsoft.Data.Sqlite;
using Wtt.Domain.Time;

namespace Wtt.Infrastructure.Storage;

/// <summary>
/// The one data file (ADR-004). Opening it applies any pending forward-only migration under SQLite's
/// write lock, so whichever process opens it first migrates it.
/// </summary>
public sealed class SqliteDatabase
{
    private readonly string connectionString;

    private SqliteDatabase(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkTimeTracker");

    /// <param name="busyTimeout">How long to wait for another process holding the file's write lock (default 30 s).</param>
    /// <exception cref="DataFileException">The file is locked, damaged, unreachable or newer than this program.</exception>
    public static SqliteDatabase Open(string path, TimeProvider clock, TimeSpan? busyTimeout = null)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = Math.Max(1, (int)(busyTimeout ?? TimeSpan.FromSeconds(30)).TotalSeconds),
            }.ToString();

            var database = new SqliteDatabase(connectionString);
            using var connection = database.Connect();
            Migrations.Apply(connection, clock.GetLocalNow().LocalDate());
            return database;
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            throw new DataFileException(fullPath, exception);
        }
    }

    internal SqliteConnection Connect()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
