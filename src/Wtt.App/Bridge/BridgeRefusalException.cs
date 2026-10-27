namespace Wtt.App.Bridge;

/// <summary>The host refuses the message with this code; nothing is saved.</summary>
internal sealed class BridgeRefusalException(RefusalCode code, Exception? cause = null)
    : Exception($"Refused: {code}", cause)
{
    public RefusalCode Code { get; } = code;
}
