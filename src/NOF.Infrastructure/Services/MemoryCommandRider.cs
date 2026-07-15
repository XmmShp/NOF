using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryCommandRider : ICommandRider
{
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly IObjectSerializer _objectSerializer;

    public MemoryCommandRider(
        CommandHandlerRegistry commandHandlerRegistry,
        IServiceProvider serviceProvider,
        IObjectSerializer objectSerializer)
    {
        _commandHandlerRegistry = commandHandlerRegistry;
        _serviceProvider = serviceProvider;
        _objectSerializer = objectSerializer;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string commandTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var commandType = TypeResolver.Resolve(commandTypeName);
        var handlerType = _commandHandlerRegistry.GetHandlers(commandType).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route command '{commandType.Name}'. No matching local handler registered.");

        var messageId = ResolveMessageId(headers);
        await EnqueueAsync(
            messageId,
            InboxMessageType.Command,
            payload,
            payloadTypeName,
            TypeResolver.Register(handlerType),
            headers,
            cancellationToken);
    }

    private async Task<bool> EnqueueAsync(
        Guid messageId,
        InboxMessageType messageType,
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string handlerTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var dbContext = scope.ServiceProvider.GetService<IDbContext>();
        if (dbContext is null)
        {
            return true;
        }

        dbContext.Set<NOFInboxMessage>().Add(new NOFInboxMessage
        {
            Id = messageId,
            MessageType = messageType,
            PayloadType = payloadTypeName,
            HandlerType = handlerTypeName,
            Payload = payload.ToArray(),
            Headers = SerializeHeaders(headers)
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private string SerializeHeaders(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var dictionary = headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        return _objectSerializer.SerializeToText(dictionary, typeof(Dictionary<string, string?>));
    }

    private static Guid ResolveMessageId(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var messageIdValue = headers?.FirstOrDefault(static kvp => kvp.Key == NOFAbstractionConstants.Transport.Headers.MessageId).Value;
        return Guid.TryParse(messageIdValue, out var messageId) ? messageId : Guid.NewGuid();
    }
}
