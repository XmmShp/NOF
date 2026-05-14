using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryNotificationRider : INotificationRider
{
    private readonly NotificationHandlerRegistry _notificationHandlerRegistry;
    private readonly InboxMessageStore _inboxMessageStore;

    public MemoryNotificationRider(
        NotificationHandlerRegistry notificationHandlerRegistry,
        InboxMessageStore inboxMessageStore)
    {
        _notificationHandlerRegistry = notificationHandlerRegistry;
        _inboxMessageStore = inboxMessageStore;
    }

    public async Task PublishAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        IReadOnlyCollection<string> notificationTypeNames,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        var messageId = ResolveMessageId(headers);

        // Deduplicate per handler, because each handler is a separate reliable processing unit.
        var seenHandlerTypes = new HashSet<Type>();
        foreach (var notificationTypeName in notificationTypeNames)
        {
            var notificationType = TypeRegistry.Resolve(notificationTypeName);
            foreach (var handlerType in _notificationHandlerRegistry.GetHandlers(notificationType))
            {
                if (!seenHandlerTypes.Add(handlerType))
                {
                    continue;
                }

                await _inboxMessageStore.EnqueueAsync(
                    messageId,
                    InboxMessageType.Notification,
                    payload,
                    payloadTypeName,
                    TypeRegistry.Register(handlerType),
                    headers,
                    cancellationToken);
            }
        }
    }

    private static Guid ResolveMessageId(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var messageIdValue = headers?.FirstOrDefault(static kvp => kvp.Key == NOFAbstractionConstants.Transport.Headers.MessageId).Value;
        return Guid.TryParse(messageIdValue, out var messageId) ? messageId : Guid.NewGuid();
    }
}
