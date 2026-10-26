using Microsoft.Data.Sqlite;
using Wtt.Application.Ports;
using Wtt.Domain.Coverage;
using Wtt.Domain.Time;

namespace Wtt.Infrastructure.Storage;

public sealed class SqliteCoverageLog(SqliteDatabase database, TimeProvider? clock = null) : ICoverageLog
{
    public static readonly TimeSpan MaxHeartbeatGap = ICoverageLog.MaxHeartbeatGap;

    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public Task BeginAsync(DateTimeOffset signedInAt)
    {
        var now = clock.GetUtcNow();
        var signedIn = DateTimeOffsetExtensions.Min(signedInAt, now);
        Write((latest, span) =>
        {
            if (latest is not { } covered || covered.To < signedIn)
            {
                var startedAtSignIn = now - signedIn <= ICoverageLog.MaxAutostartDelay;
                span.Insert(startedAtSignIn ? signedIn : now, now);
            }
            else if (IsHeartbeatAfter(covered, now))
            {
                span.Extend(covered, now);
            }
            else
            {
                span.Insert(now, now);
            }
        });
        return Task.CompletedTask;
    }

    public Task ExtendAsync(DateTimeOffset now)
    {
        Write((latest, span) =>
        {
            if (latest is { } covered && IsHeartbeatAfter(covered, now))
            {
                span.Extend(covered, now);
            }
            else
            {
                span.Insert(now, now);
            }
        });
        return Task.CompletedTask;
    }

    public Task ResumeAsync(DateTimeOffset suspendedAt, DateTimeOffset resumedAt)
    {
        Write((latest, span) =>
        {
            if (latest is { } covered && covered.To >= suspendedAt - MaxHeartbeatGap && covered.To <= resumedAt)
            {
                span.Extend(covered, resumedAt);
            }
            else
            {
                span.Insert(resumedAt, resumedAt);
            }
        });
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
            spans.Add(new CoverageSpan(Instant(reader.GetInt64(0)), Instant(reader.GetInt64(1))));
        }

        return Task.FromResult<IReadOnlyList<CoverageSpan>>(spans);
    }

    private static bool IsHeartbeatAfter(StoredSpan span, DateTimeOffset now) =>
        now >= span.To && now - span.To <= MaxHeartbeatGap;

    /// <summary>Reads the latest span and changes the spans in one write transaction, so two writers never interleave.</summary>
    private void Write(Action<StoredSpan?, SpanWriter> change)
    {
        using var connection = database.Connect();
        using var transaction = connection.BeginTransaction(deferred: false);
        change(Latest(connection, transaction), new SpanWriter(connection, transaction));
        transaction.Commit();
    }

    private static StoredSpan? Latest(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var latest = connection.CreateCommand();
        latest.Transaction = transaction;
        latest.CommandText = "SELECT id, from_utc_ticks, to_utc_ticks FROM coverage ORDER BY to_utc_ticks DESC, id DESC LIMIT 1;";
        using var reader = latest.ExecuteReader();
        return reader.Read()
            ? new StoredSpan(reader.GetInt64(0), Instant(reader.GetInt64(1)), Instant(reader.GetInt64(2)))
            : null;
    }

    private static DateTimeOffset Instant(long utcTicks) => new(utcTicks, TimeSpan.Zero);

    private sealed record StoredSpan(long Id, DateTimeOffset From, DateTimeOffset To);

    private sealed class SpanWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        public void Insert(DateTimeOffset from, DateTimeOffset to) => Execute(
            "INSERT INTO coverage (from_utc_ticks, to_utc_ticks) VALUES ($from, $to);",
            ("$from", from.UtcTicks),
            ("$to", to.UtcTicks));

        public void Extend(StoredSpan span, DateTimeOffset to) => Execute(
            "UPDATE coverage SET to_utc_ticks = $to WHERE id = $id;",
            ("$to", to.UtcTicks),
            ("$id", span.Id));

        private void Execute(string sql, params (string Name, long Value)[] parameters)
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
}
