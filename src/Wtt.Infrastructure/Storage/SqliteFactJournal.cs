using Microsoft.Data.Sqlite;
using Wtt.Application.Ports;
using Wtt.Domain.Facts;
using Wtt.Domain.Time;

namespace Wtt.Infrastructure.Storage;

public sealed class SqliteFactJournal(SqliteDatabase database) : IFactJournal
{
    private const string Columns = "at_utc_ticks, utc_offset_minutes, kind, source, source_key, detail";

    public Task<int> AppendAsync(IReadOnlyList<ActivityFact> facts)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction();
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            $"INSERT OR IGNORE INTO facts ({Columns}) VALUES ($at, $offset, $kind, $source, $key, $detail);";
        var at = insert.Parameters.Add("$at", SqliteType.Integer);
        var offset = insert.Parameters.Add("$offset", SqliteType.Integer);
        var kind = insert.Parameters.Add("$kind", SqliteType.Text);
        var source = insert.Parameters.Add("$source", SqliteType.Text);
        var key = insert.Parameters.Add("$key", SqliteType.Text);
        var detail = insert.Parameters.Add("$detail", SqliteType.Text);

        var added = 0;
        foreach (var fact in facts)
        {
            at.Value = fact.At.UtcTicks;
            offset.Value = (int)fact.At.Offset.TotalMinutes;
            kind.Value = fact.Kind.ToString();
            source.Value = fact.Source.ToString();
            key.Value = fact.SourceKey;
            detail.Value = StoredJson.Encode(fact.Detail);
            added += insert.ExecuteNonQuery();
        }

        transaction.Commit();
        return Task.FromResult(added);
    }

    public Task<IReadOnlyList<ActivityFact>> ReadAsync(Interval range)
    {
        using var connection = database.Connect();
        using var query = connection.CreateCommand();
        query.CommandText =
            $"SELECT {Columns} FROM facts WHERE at_utc_ticks >= $start AND at_utc_ticks < $end ORDER BY at_utc_ticks, id;";
        query.Parameters.AddWithValue("$start", range.Start.UtcTicks);
        query.Parameters.AddWithValue("$end", range.End.UtcTicks);

        var facts = new List<ActivityFact>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            facts.Add(new ActivityFact(
                Instant(reader.GetInt64(0), reader.GetInt32(1)),
                Enum.Parse<FactKind>(reader.GetString(2)),
                Enum.Parse<FactSource>(reader.GetString(3)),
                reader.GetString(4),
                StoredJson.DecodeDetail(reader.GetString(5))));
        }

        return Task.FromResult<IReadOnlyList<ActivityFact>>(facts);
    }

    public Task<DateTimeOffset?> EarliestAsync(FactSource? source = null)
    {
        using var connection = database.Connect();
        using var query = connection.CreateCommand();
        query.CommandText = source is null
            ? "SELECT at_utc_ticks, utc_offset_minutes FROM facts ORDER BY at_utc_ticks LIMIT 1;"
            : "SELECT at_utc_ticks, utc_offset_minutes FROM facts WHERE source = $source ORDER BY at_utc_ticks LIMIT 1;";
        if (source is not null)
        {
            query.Parameters.AddWithValue("$source", source.Value.ToString());
        }

        using var reader = query.ExecuteReader();
        DateTimeOffset? earliest = reader.Read() ? Instant(reader.GetInt64(0), reader.GetInt32(1)) : null;
        return Task.FromResult(earliest);
    }

    private static DateTimeOffset Instant(long utcTicks, int offsetMinutes) =>
        new DateTimeOffset(utcTicks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
}
