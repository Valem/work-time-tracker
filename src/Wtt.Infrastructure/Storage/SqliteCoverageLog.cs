using Wtt.Application.Ports;
using Wtt.Domain.Coverage;
using Wtt.Domain.Time;

namespace Wtt.Infrastructure.Storage;

public sealed class SqliteCoverageLog(SqliteDatabase database) : ICoverageLog
{
    /// <summary>A heartbeat later than this after the last one starts a new span: the Recorder was not running.</summary>
    public static readonly TimeSpan MaxHeartbeatGap = TimeSpan.FromMinutes(2);

    public Task ExtendAsync(DateTimeOffset now)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction();
        using var latest = connection.CreateCommand();
        latest.Transaction = transaction;
        latest.CommandText = "SELECT id, to_utc_ticks FROM coverage ORDER BY to_utc_ticks DESC LIMIT 1;";

        long? extendId = null;
        using (var reader = latest.ExecuteReader())
        {
            if (reader.Read())
            {
                var to = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
                if (now >= to && now - to <= MaxHeartbeatGap)
                {
                    extendId = reader.GetInt64(0);
                }
            }
        }

        using var write = connection.CreateCommand();
        write.Transaction = transaction;
        write.CommandText = extendId is null
            ? "INSERT INTO coverage (from_utc_ticks, to_utc_ticks) VALUES ($now, $now);"
            : "UPDATE coverage SET to_utc_ticks = $now WHERE id = $id;";
        write.Parameters.AddWithValue("$now", now.UtcTicks);
        if (extendId is not null)
        {
            write.Parameters.AddWithValue("$id", extendId.Value);
        }

        write.ExecuteNonQuery();
        transaction.Commit();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CoverageSpan>> ReadAsync(Interval range)
    {
        using var connection = database.Connect();
        using var query = connection.CreateCommand();
        query.CommandText =
            "SELECT from_utc_ticks, to_utc_ticks FROM coverage WHERE to_utc_ticks >= $start AND from_utc_ticks < $end ORDER BY from_utc_ticks;";
        query.Parameters.AddWithValue("$start", range.Start.UtcTicks);
        query.Parameters.AddWithValue("$end", range.End.UtcTicks);

        var spans = new List<CoverageSpan>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            spans.Add(new CoverageSpan(
                new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero),
                new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero)));
        }

        return Task.FromResult<IReadOnlyList<CoverageSpan>>(spans);
    }
}
