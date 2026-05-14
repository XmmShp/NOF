using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryCommandRider : ICommandRider
{
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly InboxMessageStore _inboxMessageStore;

    public MemoryCommandRider(
        CommandHandlerRegistry commandHandlerRegistry,
        InboxMessageStore inboxMessageStore)
    {
        _commandHandlerRegistry = commandHandlerRegistry;
        _inboxMessageStore = inboxMessageStore;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string commandTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var commandType = TypeRegistry.Resolve(commandTypeName);
        var handlerType = _commandHandlerRegistry.GetHandlers(commandType).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route command '{commandType.Name}'. No matching local handler registered.");

        var messageId = ResolveMessageId(headers);
        await _inboxMessageStore.EnqueueAsync(
            messageId,
            InboxMessageType.Command,
            payload,
            payloadTypeName,
            TypeRegistry.Register(handlerType),
            headers,
            cancellationToken);
    }

    private static Guid ResolveMessageId(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var messageIdValue = headers?.FirstOrDefault(static kvp => kvp.Key == NOFAbstractionConstants.Transport.Headers.MessageId).Value;
        return Guid.TryParse(messageIdValue, out var messageId) ? messageId : Guid.NewGuid();
    }
}
