using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Wtt.App.Web;
using Wtt.Infrastructure.Diagnostics;
using Wtt.Infrastructure.Storage;

namespace Wtt.App.Bridge;

/// <summary>The answer to send back, and whether the data file failed, which the window shows as its own problem.</summary>
internal sealed record BridgeReply(string Json, bool DataFileFailed);

/// <summary>
/// Reads a message from the page and answers it. Only the app's own page is answered, and only for the contract's message
/// types; a payload is read strictly and checked before anything runs. A refusal carries a code and nothing else — the
/// details, exception included, go to the error log only.
/// </summary>
internal sealed class BridgeHost(BridgeHandlers handlers, ErrorLog log)
{
    private static readonly Dictionary<string, BridgeMessage> MessagesByType =
        BridgeContract.Messages.ToDictionary(message => message.Type, StringComparer.Ordinal);

    /// <param name="source">The address of the document that sent the message.</param>
    /// <param name="json">The message as the page posted it.</param>
    /// <returns>The reply, or null for a message from anywhere but the app's own pages, which is not answered.</returns>
    public async Task<BridgeReply?> AnswerAsync(string? source, string json)
    {
        if (!LocalContent.IsAppContent(source))
        {
            log.Write("Bridge: a message from outside the app's pages was ignored");
            return null;
        }

        BridgeRequest? request = null;
        try
        {
            request = Read<BridgeRequest>(json);
            var message = MessagesByType.GetValueOrDefault(request.Type) ?? throw new BridgeRefusalException(RefusalCode.UnknownType);
            var payload = ReadPayload(request.Payload, message.Request);
            var result = await handlers.AnswerAsync(payload);
            return new BridgeReply(JsonSerializer.Serialize(new BridgeAnswer(request.Id, result), BridgeContract.Json), DataFileFailed: false);
        }
        catch (Exception failure)
        {
            var code = CodeFor(failure);
            log.Write(
                $"Bridge: refused {request?.Type ?? "an unreadable message"} (id {request?.Id.ToString(CultureInfo.InvariantCulture) ?? "none"}) with {code}",
                failure is BridgeRefusalException { InnerException: null } ? null : failure);
            var refusal = new BridgeRefusal(request?.Id, new Refusal(code));
            return new BridgeReply(JsonSerializer.Serialize(refusal, BridgeContract.Json), code == RefusalCode.DataFileUnreadable);
        }
    }

    private static T Read<T>(string json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, BridgeContract.Json) ?? throw new BridgeRefusalException(RefusalCode.Malformed);
        }
        catch (JsonException malformed)
        {
            throw new BridgeRefusalException(RefusalCode.Malformed, malformed);
        }
    }

    private static object ReadPayload(JsonElement payload, Type type)
    {
        try
        {
            return payload.Deserialize(type, BridgeContract.Json) ?? throw new BridgeRefusalException(RefusalCode.Malformed);
        }
        catch (JsonException malformed)
        {
            throw new BridgeRefusalException(RefusalCode.Malformed, malformed);
        }
    }

    private static RefusalCode CodeFor(Exception failure) => failure switch
    {
        BridgeRefusalException refusal => refusal.Code,
        DataFileException or SqliteException or InvalidDataException or IOException or UnauthorizedAccessException => RefusalCode.DataFileUnreadable,
        _ => RefusalCode.Failed,
    };
}
