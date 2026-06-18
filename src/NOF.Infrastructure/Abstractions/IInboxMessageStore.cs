namespace NOF.Infrastructure;

public interface IInboxMessageStore
{
    Task<bool> EnqueueAsync(
        Guid messageId,
        InboxMessageType messageType,
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string handlerTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
