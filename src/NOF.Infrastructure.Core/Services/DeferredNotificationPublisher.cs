using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Abstraction;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Deferred notification publisher implementation.
/// </summary>
public sealed class DeferredNotificationPublisher : IDeferredNotificationPublisher
{
    private readonly IOutboxMessageRepository _repository;
    private readonly IInvocationContext _invocationContext;
    private readonly IMessageSerializer _messageSerializer;

    public DeferredNotificationPublisher(IOutboxMessageRepository repository, IInvocationContext invocationContext, IMessageSerializer messageSerializer)
    {
        _repository = repository;
        _invocationContext = invocationContext;
        _messageSerializer = messageSerializer;
    }

    public void Publish(INotification notification)
    {
        var currentActivity = Activity.Current;
        var tenantId = _invocationContext.TenantId;

        var headers = new Dictionary<string, string?>
        {
            [NOFInfrastructureCoreConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString(),
            [NOFInfrastructureCoreConstants.Transport.Headers.TenantId] = tenantId
        };
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var typeName = TypeRegistry.Register(notification.GetType());

        _repository.Add(new NOFOutboxMessage
        {
            MessageType = OutboxMessageType.Notification,
            PayloadType = typeName,
            Payload = _messageSerializer.Serialize(notification),
            DestinationEndpointName = null,
            Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
            TraceId = currentActivity?.TraceId.ToString(),
            SpanId = currentActivity?.SpanId.ToString()
        });
    }
}
