using System.Globalization;
using Microsoft.Data.Sqlite;
using Wtt.Application.Ports;
using Wtt.Domain.Rules;
using Wtt.Domain.Time;

namespace Wtt.Infrastructure.Storage;

/// <summary>Saved lunch rules are superseded, never edited; the install date comes from the data file's creation.</summary>
public sealed class SqliteLunchRules(SqliteDatabase database, TimeProvider clock) : ILunchRules
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    public Task<LunchRule> InForceOnAsync(DateOnly date)
    {
        using var connection = database.Connect();
        return Task.FromResult(LoadBook(connection).InForceOn(date));
    }

    public Task<LunchRule> SaveAsync(TimeRange window, IReadOnlyList<BreakTier> tiers)
    {
        var rule = new LunchRule(clock.GetLocalNow().LocalDate(), window, tiers);

        using var connection = database.Connect();
        using var insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO lunch_rules (valid_from, window_start, window_end, tiers, saved_at) VALUES ($from, $start, $end, $tiers, $savedAt);";
        insert.Parameters.AddWithValue("$from", rule.ValidFrom.ToString(DateFormat, CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$start", rule.Window.Start.ToString(TimeFormat, CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$end", rule.Window.End.ToString(TimeFormat, CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$tiers", StoredJson.Encode(rule.Tiers));
        insert.Parameters.AddWithValue("$savedAt", clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        insert.ExecuteNonQuery();
        return Task.FromResult(rule);
    }

    private static LunchRuleBook LoadBook(SqliteConnection connection)
    {
        using var installed = connection.CreateCommand();
        installed.CommandText = "SELECT value FROM meta WHERE key = 'installed_on';";
        var installedOn = DateOnly.ParseExact((string)installed.ExecuteScalar()!, DateFormat, CultureInfo.InvariantCulture);

        using var query = connection.CreateCommand();
        query.CommandText = "SELECT valid_from, window_start, window_end, tiers FROM lunch_rules ORDER BY id;";
        var saved = new List<LunchRule>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            saved.Add(new LunchRule(
                DateOnly.ParseExact(reader.GetString(0), DateFormat, CultureInfo.InvariantCulture),
                new TimeRange(
                    TimeOnly.ParseExact(reader.GetString(1), TimeFormat, CultureInfo.InvariantCulture),
                    TimeOnly.ParseExact(reader.GetString(2), TimeFormat, CultureInfo.InvariantCulture)),
                StoredJson.DecodeTiers(reader.GetString(3))));
        }

        return new LunchRuleBook(installedOn, saved);
    }
}
