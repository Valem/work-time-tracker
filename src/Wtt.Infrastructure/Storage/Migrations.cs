using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Wtt.Infrastructure.Storage;

/// <summary>Forward-only schema steps; the file's <c>user_version</c> says which have run.</summary>
internal static class Migrations
{
    private static readonly string[] Steps =
    [
        """
        CREATE TABLE meta (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE facts (
            id                 INTEGER PRIMARY KEY,
            source_key         TEXT    NOT NULL UNIQUE,
            at_utc_ticks       INTEGER NOT NULL,
            utc_offset_minutes INTEGER NOT NULL,
            kind               TEXT    NOT NULL,
            source             TEXT    NOT NULL,
            detail             TEXT    NOT NULL
        );
        CREATE INDEX ix_facts_at ON facts (at_utc_ticks);
        CREATE TABLE coverage (
            id             INTEGER PRIMARY KEY,
            from_utc_ticks INTEGER NOT NULL,
            to_utc_ticks   INTEGER NOT NULL
        );
        CREATE TABLE lunch_rules (
            id           INTEGER PRIMARY KEY,
            valid_from   TEXT NOT NULL,
            window_start TEXT NOT NULL,
            window_end   TEXT NOT NULL,
            tiers        TEXT NOT NULL,
            saved_at     TEXT NOT NULL
        );
        """,
    ];

    public static int Latest => Steps.Length;

    public static void Apply(SqliteConnection connection, DateOnly today)
    {
        Execute(connection, null, "PRAGMA journal_mode = WAL;");

        using var transaction = connection.BeginTransaction(deferred: false);
        var version = VersionOf(connection, transaction);
        ThrowIfNewer(version);

        for (var step = version; step < Latest; step++)
        {
            Execute(connection, transaction, Steps[step]);
        }

        Execute(connection, transaction, $"PRAGMA user_version = {Latest};");
        Execute(
            connection,
            transaction,
            "INSERT OR IGNORE INTO meta (key, value) VALUES ('installed_on', $today);",
            ("$today", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        transaction.Commit();
    }

    /// <summary>The schema version the file is at: 0 for a new, empty file.</summary>
    public static int VersionOf(SqliteConnection connection, SqliteTransaction? transaction = null) =>
        Convert.ToInt32(Scalar(connection, transaction, "PRAGMA user_version;"), CultureInfo.InvariantCulture);

    public static void ThrowIfNewer(int version)
    {
        if (version > Latest)
        {
            throw new InvalidOperationException(
                $"The data file has schema version {version}, newer than this program knows ({Latest}). Use the newer program.");
        }
    }

    private static object? Scalar(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }
}
