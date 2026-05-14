using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryCommandRider : ICommandRider
{
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly InboxMessageStore _inboxMessageStore;
    private readonly TypeResolver _typeResolver;

    public MemoryCommandRider(
        CommandHandlerRegistry commandHandlerRegistry,
        InboxMessageStore inboxMessageStore,
        TypeResolver typeResolver)
    {
        _commandHandlerRegistry = commandHandlerRegistry;
        _inboxMessageStore = inboxMessageStore;
        _typeResolver = typeResolver;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string commandTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var commandType = _typeResolver.Resolve(commandTypeName);
        var handlerType = _commandHandlerRegistry.GetHandlers(commandType).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route command '{commandType.Name}'. No matching local handler registered.");

        var messageId = ResolveMessageId(headers);
        await _inboxMessageStore.EnqueueAsync(
            messageId,
            InboxMessageType.Command,
            payload,
            payloadTypeName,
            _typeResolver.Register(handlerType),
            headers,
            cancellationToken);
    }

    private static Guid ResolveMessageId(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var messageIdValue = headers?.FirstOrDefault(static kvp => kvp.Key == NOFAbstractionConstants.Transport.Headers.MessageId).Value;
        return Guid.TryParse(messageIdValue, out var messageId) ? messageId : Guid.NewGuid();
    }
}
