namespace NOF.Infrastructure;

public sealed class MemoryNotificationRider : INotificationRider
{
    private readonly InboundMessageDispatcher _dispatcher;

    public MemoryNotificationRider(InboundMessageDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task PublishAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        IReadOnlyCollection<string> notificationTypeNames,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
        => _dispatcher.DispatchNotificationAsync(payload, payloadTypeName, notificationTypeNames, headers, cancellationToken);
}
