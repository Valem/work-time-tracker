using System.Globalization;
using Microsoft.Data.Sqlite;
using Wtt.Application.Ports;
using Wtt.Domain.Planning;

namespace Wtt.Infrastructure.Storage;

/// <summary>
/// Loaded public-holiday years in the domain's terms — date, share, kind, reach and names by language — with their source
/// and load time; never a source's answer as it came. A year is replaced only whole.
/// </summary>
public sealed class SqlitePublicHolidayStore(SqliteDatabase database) : IPublicHolidayStore
{
    private const string DateFormat = "yyyy-MM-dd";

    public Task<PublicHolidayYear?> FindAsync(Canton canton, int year)
    {
        using var connection = database.Connect();
        if (Header(connection, canton, year) is not { } header)
        {
            return Task.FromResult<PublicHolidayYear?>(null);
        }

        var (source, loadedAt) = header;
        using var query = Command(
            connection,
            null,
            "SELECT date, share, kind, reach, names FROM public_holidays WHERE canton = $canton AND year = $year ORDER BY id;",
            ("$canton", canton.ToString()),
            ("$year", year));
        var entries = new List<PublicHolidayEntry>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new PublicHolidayEntry(
                DateOnly.ParseExact(reader.GetString(0), DateFormat, CultureInfo.InvariantCulture),
                Enum.Parse<DayPart>(reader.GetString(1)),
                Enum.Parse<HolidayKind>(reader.GetString(2)),
                Enum.Parse<HolidayReach>(reader.GetString(3)),
                StoredJson.DecodeNames(reader.GetString(4))));
        }

        return Task.FromResult<PublicHolidayYear?>(new PublicHolidayYear(canton, year, source, loadedAt, entries));
    }

    public Task ReplaceAsync(PublicHolidayYear year)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction(deferred: false);
        var canton = year.Canton.ToString();
        Execute(connection, transaction, "DELETE FROM public_holidays WHERE canton = $canton AND year = $year;", ("$canton", canton), ("$year", year.Year));
        Execute(
            connection,
            transaction,
            "INSERT OR REPLACE INTO public_holiday_years (canton, year, source, source_licence, loaded_at) VALUES ($canton, $year, $source, $licence, $loadedAt);",
            ("$canton", canton),
            ("$year", year.Year),
            ("$source", year.Source.Name),
            ("$licence", year.Source.Licence),
            ("$loadedAt", year.LoadedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        foreach (var entry in year.Entries)
        {
            Execute(
                connection,
                transaction,
                "INSERT INTO public_holidays (canton, year, date, share, kind, reach, names) VALUES ($canton, $year, $date, $share, $kind, $reach, $names);",
                ("$canton", canton),
                ("$year", year.Year),
                ("$date", entry.Date.ToString(DateFormat, CultureInfo.InvariantCulture)),
                ("$share", entry.Share.ToString()),
                ("$kind", entry.Kind.ToString()),
                ("$reach", entry.Reach.ToString()),
                ("$names", StoredJson.Encode(entry.Names)));
        }

        transaction.Commit();
        return Task.CompletedTask;
    }

    private static (HolidaySource Source, DateTimeOffset LoadedAt)? Header(SqliteConnection connection, Canton canton, int year)
    {
        using var header = Command(
            connection,
            null,
            "SELECT source, source_licence, loaded_at FROM public_holiday_years WHERE canton = $canton AND year = $year;",
            ("$canton", canton.ToString()),
            ("$year", year));
        using var reader = header.ExecuteReader();
        return reader.Read()
            ? (new HolidaySource(reader.GetString(0), reader.GetString(1)), DateTimeOffset.ParseExact(reader.GetString(2), "O", CultureInfo.InvariantCulture))
            : null;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        command.ExecuteNonQuery();
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }
}
