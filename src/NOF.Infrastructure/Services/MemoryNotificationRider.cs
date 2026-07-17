using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryNotificationRider : INotificationRider
{
    private readonly NotificationHandlerRegistry _notificationHandlerRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly IObjectSerializer _objectSerializer;

    public MemoryNotificationRider(
        NotificationHandlerRegistry notificationHandlerRegistry,
        IServiceProvider serviceProvider,
        IObjectSerializer objectSerializer)
    {
        _notificationHandlerRegistry = notificationHandlerRegistry;
        _serviceProvider = serviceProvider;
        _objectSerializer = objectSerializer;
    }

    public async Task PublishAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        IReadOnlyCollection<string> dispatchRoutes,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var messageId = ResolveMessageId(headers);

        // Deduplicate per handler, because each handler is a separate reliable processing unit.
        var seenHandlerTypeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dispatchRoute in dispatchRoutes)
        {
            foreach (var handlerTypeName in ResolveHandlerTypeNames(dispatchRoute))
            {
                if (!seenHandlerTypeNames.Add(handlerTypeName))
                {
                    continue;
                }

                await EnqueueAsync(
                    messageId,
                    InboxMessageType.Notification,
                    payload,
                    payloadTypeName,
                    handlerTypeName,
                    headers,
                    cancellationToken);
            }
        }
    }

    private IReadOnlyCollection<string> ResolveHandlerTypeNames(string dispatchRoute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchRoute);

        if (_notificationHandlerRegistry.TryGetHandlerType(dispatchRoute, out var handlerType))
        {
            return [handlerType.DisplayName];
        }

        return [.. _notificationHandlerRegistry.GetHandlers(dispatchRoute).Select(static handlerType => handlerType.DisplayName)];
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
