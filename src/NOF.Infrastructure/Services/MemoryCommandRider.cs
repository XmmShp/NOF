namespace NOF.Infrastructure;

public sealed class MemoryCommandRider : ICommandRider
{
    private readonly InboundMessageDispatcher _dispatcher;

    public MemoryCommandRider(InboundMessageDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task SendAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string commandTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
        => _dispatcher.DispatchCommandAsync(payload, payloadTypeName, commandTypeName, headers, cancellationToken);
}
