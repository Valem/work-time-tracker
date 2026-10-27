using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Wtt.App.Bridge;

/// <summary>
/// Why the host refused a message; nothing was saved. The UI shows one text per code, and the error log holds the details.
/// </summary>
public enum RefusalCode
{
    /// <summary>Not a request the host reads: not JSON, or a field missing, extra or of the wrong type.</summary>
    Malformed,
    UnknownType,

    /// <summary>A date outside 2000–2099.</summary>
    DateOutOfRange,
    NotMonday,
    WindowNotAfterStart,
    TooManyTiers,

    /// <summary>A time or duration below 0:00 or over 24:00; a lunch window ends by 23:59.</summary>
    ValueOutOfRange,
    NotWholeMinutes,
    DuplicateThreshold,

    /// <summary>The domain refused the lunch rule for a reason the host does not check itself.</summary>
    InvalidRule,
    DataFileUnreadable,
    Failed,
}

/// <summary>What the UI sends: an id it chose, one of the message types and that type's payload.</summary>
public sealed record BridgeRequest(int Id, string Type, JsonElement Payload);

/// <summary>The answer to the request with that id.</summary>
public sealed record BridgeAnswer(int Id, object Result);

/// <summary>A refusal; the id is null when the request's own id could not be read.</summary>
public sealed record BridgeRefusal(int? Id, Refusal Error);

public sealed record Refusal(RefusalCode Code);

/// <summary>A message type the UI may send, with the record its payload reads into and the record it is answered with.</summary>
public sealed record BridgeMessage(string Type, Type Request, Type Result);

/// <summary>
/// The contract between the UI and the host, defined once: the message types, their records, the refusal codes, and the
/// one serializer that writes and reads them. Wtt.BridgeTypes writes the UI's declarations from these.
/// </summary>
public static class BridgeContract
{
    public const int MaxTiers = 10;

    public static JsonSerializerOptions Json { get; } = CreateJson();

    public static IReadOnlyList<BridgeMessage> Messages { get; } =
    [
        new("week.get", typeof(WeekRequest), typeof(WeekView)),
        new("day.get", typeof(DayRequest), typeof(DayView)),
        new("status.get", typeof(StatusRequest), typeof(StatusView)),
        new("lunchRule.get", typeof(LunchRuleRequest), typeof(LunchRuleView)),
        new("lunchRule.save", typeof(LunchRuleInput), typeof(LunchRuleView)),
    ];

    public static IReadOnlyList<Type> Envelopes { get; } = [typeof(BridgeRequest), typeof(BridgeAnswer), typeof(BridgeRefusal)];

    /// <summary>
    /// Web defaults (camelCase), enums as camelCase strings, and strict reading: a missing, extra, null or mistyped field
    /// makes the whole message malformed.
    /// </summary>
    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }
}
