using System.Globalization;
using Microsoft.Data.Sqlite;
using Wtt.Application.Ports;
using Wtt.Domain.Planning;

namespace Wtt.Infrastructure.Storage;

/// <summary>
/// The person's plan as entered: «Mein Jahr» saves in the order saved — superseded, never edited — one booking per date,
/// the ticked dates and the layout. Nothing derived is stored.
/// </summary>
public sealed class SqlitePlanStore(SqliteDatabase database, TimeProvider clock) : IPlanStore
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string LayoutKey = "planner.layout";

    public Task<PlanInputs> ReadAsync(DateOnly from, DateOnly to)
    {
        using var connection = database.Connect();
        return Task.FromResult(new PlanInputs(ReadSettings(connection), ReadBookings(connection, from, to), ReadTicks(connection, from, to)));
    }

    public Task ApplyAsync(PlanChange change)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction(deferred: false);
        Apply(connection, transaction, change);
        transaction.Commit();
        return Task.CompletedTask;
    }

    public Task SaveSettingsAsync(PlanSettings settings, PlanChange ticksCleared)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var insert = Command(
            connection,
            transaction,
            """
            INSERT INTO plan_settings (valid_from, pensum, monday, tuesday, wednesday, thursday, friday, yearly_half_days, canton, saved_at)
            VALUES ($from, $pensum, $monday, $tuesday, $wednesday, $thursday, $friday, $yearly, $canton, $savedAt);
            """,
            ("$from", Text(settings.ValidFrom)),
            ("$pensum", settings.Pensum),
            ("$monday", (int)settings.MondayToFriday[0]),
            ("$tuesday", (int)settings.MondayToFriday[1]),
            ("$wednesday", (int)settings.MondayToFriday[2]),
            ("$thursday", (int)settings.MondayToFriday[3]),
            ("$friday", (int)settings.MondayToFriday[4]),
            ("$yearly", settings.YearlyDays is { } days ? days.Value : DBNull.Value),
            ("$canton", settings.Canton is { } canton ? canton.ToString() : DBNull.Value),
            ("$savedAt", clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)));
        insert.ExecuteNonQuery();
        Apply(connection, transaction, ticksCleared);
        transaction.Commit();
        return Task.CompletedTask;
    }

    public Task<YearLayout> LayoutAsync()
    {
        using var connection = database.Connect();
        using var query = Command(connection, null, "SELECT value FROM meta WHERE key = $key;", ("$key", LayoutKey));
        return Task.FromResult(Enum.TryParse<YearLayout>(query.ExecuteScalar() as string, out var layout) ? layout : YearLayout.Columns);
    }

    public Task SaveLayoutAsync(YearLayout layout)
    {
        using var connection = database.Connect();
        using var upsert = Command(
            connection,
            null,
            "INSERT OR REPLACE INTO meta (key, value) VALUES ($key, $value);",
            ("$key", LayoutKey),
            ("$value", layout.ToString()));
        upsert.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private static List<PlanSettings> ReadSettings(SqliteConnection connection)
    {
        using var query = Command(
            connection,
            null,
            "SELECT valid_from, pensum, monday, tuesday, wednesday, thursday, friday, yearly_half_days, canton FROM plan_settings ORDER BY id;");
        var settings = new List<PlanSettings>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            settings.Add(new PlanSettings(
                Date(reader.GetString(0)),
                reader.GetInt32(1),
                [.. Enumerable.Range(2, 5).Select(column => Share(reader.GetInt32(column)))],
                reader.IsDBNull(7) ? null : new HalfDays(reader.GetInt32(7)),
                reader.IsDBNull(8) ? null : Cantons.Parse(reader.GetString(8)) ?? throw new InvalidDataException("Unknown canton in the plan settings.")));
        }

        return settings;
    }

    private static Dictionary<DateOnly, DayPart> ReadBookings(SqliteConnection connection, DateOnly from, DateOnly to)
    {
        using var query = Command(
            connection,
            null,
            "SELECT date, part FROM plan_bookings WHERE date >= $from AND date <= $to;",
            ("$from", Text(from)),
            ("$to", Text(to)));
        var bookings = new Dictionary<DateOnly, DayPart>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            bookings[Date(reader.GetString(0))] = Enum.Parse<DayPart>(reader.GetString(1));
        }

        return bookings;
    }

    private static HashSet<DateOnly> ReadTicks(SqliteConnection connection, DateOnly from, DateOnly to)
    {
        using var query = Command(
            connection,
            null,
            "SELECT date FROM plan_ticks WHERE date >= $from AND date <= $to;",
            ("$from", Text(from)),
            ("$to", Text(to)));
        var ticks = new HashSet<DateOnly>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            ticks.Add(Date(reader.GetString(0)));
        }

        return ticks;
    }

    private static void Apply(SqliteConnection connection, SqliteTransaction transaction, PlanChange change)
    {
        foreach (var booking in change.Bookings)
        {
            using var command = booking.After is { } part
                ? Command(connection, transaction, "INSERT OR REPLACE INTO plan_bookings (date, part) VALUES ($date, $part);", ("$date", Text(booking.Date)), ("$part", part.ToString()))
                : Command(connection, transaction, "DELETE FROM plan_bookings WHERE date = $date;", ("$date", Text(booking.Date)));
            command.ExecuteNonQuery();
        }

        foreach (var tick in change.Ticks)
        {
            using var command = Command(
                connection,
                transaction,
                tick.After ? "INSERT OR IGNORE INTO plan_ticks (date) VALUES ($date);" : "DELETE FROM plan_ticks WHERE date = $date;",
                ("$date", Text(tick.Date)));
            command.ExecuteNonQuery();
        }
    }

    private static WorkShare Share(int halves) =>
        Enum.IsDefined((WorkShare)halves) ? (WorkShare)halves : throw new InvalidDataException("Unknown work share in the plan settings.");

    private static string Text(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static DateOnly Date(string text) => DateOnly.ParseExact(text, DateFormat, CultureInfo.InvariantCulture);

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
