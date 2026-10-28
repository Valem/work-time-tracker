namespace Wtt.App.Bridge;

/// <summary>
/// One message type bound once to the record its payload reads into, the record it answers with, and the handler
/// that answers it. The compiler holds the three together: a handler taking another request or returning another
/// result does not build.
/// </summary>
internal sealed class BridgeRoute
{
    private readonly Func<BridgeHandlers, object, Task<object>> answer;

    private BridgeRoute(BridgeMessage message, Func<BridgeHandlers, object, Task<object>> answer)
    {
        Message = message;
        this.answer = answer;
    }

    public BridgeMessage Message { get; }

    public static BridgeRoute For<TRequest, TResult>(string type, Func<BridgeHandlers, Func<TRequest, Task<TResult>>> handler)
        where TRequest : class
        where TResult : class =>
        new(
            new BridgeMessage(type, typeof(TRequest), typeof(TResult)),
            async (handlers, request) => await handler(handlers)((TRequest)request));

    /// <param name="request">The payload, read into <see cref="BridgeMessage.Request"/>.</param>
    /// <returns>The result, to be written as <see cref="BridgeMessage.Result"/>.</returns>
    public Task<object> AnswerAsync(BridgeHandlers handlers, object request) => answer(handlers, request);
}
