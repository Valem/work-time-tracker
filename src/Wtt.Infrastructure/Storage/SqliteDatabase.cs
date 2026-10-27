using System.Diagnostics.CodeAnalysis;
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
            var database = new SqliteDatabase(ConnectionString(fullPath, SqliteOpenMode.ReadWriteCreate, busyTimeout));
            using var connection = database.Connect();
            Migrations.Apply(connection, clock.GetLocalNow().LocalDate());
            return database;
        }
        catch (Exception exception) when (IsDataFileFailure(exception))
        {
            throw new DataFileException(fullPath, exception);
        }
    }

    /// <summary>
    /// Opens the file only when it exists at this program's schema version, and never creates or migrates it: a
    /// program that reads beside a running Recorder leaves the schema to that Recorder.
    /// </summary>
    /// <returns>False when the file does not exist yet or is at an older schema version.</returns>
    /// <exception cref="DataFileException">The file is locked, damaged, unreachable or newer than this program.</exception>
    public static bool TryOpenCurrent(string path, TimeSpan busyTimeout, [NotNullWhen(true)] out SqliteDatabase? database)
    {
        database = null;
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var opened = new SqliteDatabase(ConnectionString(fullPath, SqliteOpenMode.ReadWrite, busyTimeout));
            using var connection = opened.Connect();
            var version = Migrations.VersionOf(connection);
            Migrations.ThrowIfNewer(version);
            if (version < Migrations.Latest)
            {
                return false;
            }

            database = opened;
            return true;
        }
        catch (Exception exception) when (IsDataFileFailure(exception))
        {
            throw new DataFileException(fullPath, exception);
        }
    }

    /// <summary>What opening the file can fail with: SQLite, a newer schema, the file system, access rights.</summary>
    private static bool IsDataFileFailure(Exception exception) =>
        exception is SqliteException or InvalidOperationException or IOException or UnauthorizedAccessException;

    private static string ConnectionString(string fullPath, SqliteOpenMode mode, TimeSpan? busyTimeout) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = mode,
            Pooling = false,
            DefaultTimeout = Math.Max(1, (int)(busyTimeout ?? TimeSpan.FromSeconds(30)).TotalSeconds),
        }.ToString();

    internal SqliteConnection Connect()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
