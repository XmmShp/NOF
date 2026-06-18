using Microsoft.Extensions.Logging;

namespace NOF.Infrastructure;

internal sealed class NoOpInboxMessageStore(ILogger<NoOpInboxMessageStore> logger) : IInboxMessageStore
{
    public Task<bool> EnqueueAsync(
        Guid messageId,
        InboxMessageType messageType,
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string handlerTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "No persistence provider registered. Inbox message {MessageId} for handler {HandlerType} will not be persisted.",
            messageId,
            handlerTypeName);
        return Task.FromResult(true);
    }
}
