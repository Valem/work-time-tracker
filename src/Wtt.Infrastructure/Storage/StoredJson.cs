using System.Text.Json;
using System.Text.Json.Serialization;
using Wtt.Domain.Facts;
using Wtt.Domain.Rules;

namespace Wtt.Infrastructure.Storage;

internal sealed record StoredDetail(string? Cause, long? TimerSeconds, string? Reason, string? Session, string? RawReason);

internal sealed record StoredTier(int OverMinutes, int MinimumMinutes);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StoredDetail))]
[JsonSerializable(typeof(List<StoredTier>))]
internal sealed partial class StoredJsonContext : JsonSerializerContext;

/// <summary>The text forms of a fact's detail and of break tiers in the data file.</summary>
internal static class StoredJson
{
    public static string Encode(FactDetail detail) => JsonSerializer.Serialize(
        new StoredDetail(detail.Cause?.ToString(), (long?)detail.Timer?.TotalSeconds, detail.Reason, detail.Session, detail.RawReason),
        StoredJsonContext.Default.StoredDetail);

    public static FactDetail DecodeDetail(string json)
    {
        var stored = JsonSerializer.Deserialize(json, StoredJsonContext.Default.StoredDetail)
            ?? throw new InvalidDataException($"Unreadable fact detail: {json}");
        return new FactDetail(
            stored.Cause is null ? null : Enum.Parse<EndCause>(stored.Cause),
            stored.TimerSeconds is null ? null : TimeSpan.FromSeconds(stored.TimerSeconds.Value),
            stored.Reason,
            stored.Session,
            stored.RawReason);
    }

    public static string Encode(IEnumerable<BreakTier> tiers) => JsonSerializer.Serialize(
        tiers.Select(tier => new StoredTier((int)tier.Over.TotalMinutes, (int)tier.Minimum.TotalMinutes)).ToList(),
        StoredJsonContext.Default.ListStoredTier);

    public static IReadOnlyList<BreakTier> DecodeTiers(string json) =>
        (JsonSerializer.Deserialize(json, StoredJsonContext.Default.ListStoredTier) ?? [])
        .Select(tier => new BreakTier(TimeSpan.FromMinutes(tier.OverMinutes), TimeSpan.FromMinutes(tier.MinimumMinutes)))
        .ToList();
}
