using System.Globalization;
using Wtt.Application.Ports;

namespace Wtt.Infrastructure.Storage;

public sealed class SqliteHistoryCursorStore(SqliteDatabase database) : IHistoryCursorStore
{
    private const string ReadFromKey = "history.read_from";
    private const string OpenSessionKey = "history.open_session";

    public Task<HistoryCursor> LoadAsync()
    {
        using var connection = database.Connect();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT key, value FROM meta WHERE key IN ($readFrom, $openSession);";
        query.Parameters.AddWithValue("$readFrom", ReadFromKey);
        query.Parameters.AddWithValue("$openSession", OpenSessionKey);

        DateTimeOffset? readFrom = null;
        string? openSession = null;
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(0) == ReadFromKey)
            {
                readFrom = DateTimeOffset.ParseExact(reader.GetString(1), "O", CultureInfo.InvariantCulture);
            }
            else
            {
                openSession = reader.GetString(1);
            }
        }

        return Task.FromResult(new HistoryCursor(readFrom, openSession));
    }

    public Task SaveAsync(HistoryCursor cursor)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction();
        Write(ReadFromKey, cursor.ReadFrom?.ToString("O", CultureInfo.InvariantCulture));
        Write(OpenSessionKey, cursor.OpenSession);
        transaction.Commit();
        return Task.CompletedTask;

        void Write(string key, string? value)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = value is null
                ? "DELETE FROM meta WHERE key = $key;"
                : "INSERT OR REPLACE INTO meta (key, value) VALUES ($key, $value);";
            command.Parameters.AddWithValue("$key", key);
            if (value is not null)
            {
                command.Parameters.AddWithValue("$value", value);
            }

            command.ExecuteNonQuery();
        }
    }
}
