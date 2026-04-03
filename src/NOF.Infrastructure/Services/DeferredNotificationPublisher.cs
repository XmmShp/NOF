using NOF.Application;
using NOF.Contract;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

/// <summary>
/// Deferred notification publisher implementation.
/// </summary>
public sealed class DeferredNotificationPublisher : IDeferredNotificationPublisher
{
    private readonly IOutboxMessageRepository _repository;
    private readonly IExecutionContext _executionContext;
    private readonly IMessageSerializer _messageSerializer;

    public DeferredNotificationPublisher(IOutboxMessageRepository repository, IExecutionContext executionContext, IMessageSerializer messageSerializer)
    {
        _repository = repository;
        _executionContext = executionContext;
        _messageSerializer = messageSerializer;
    }

    public void Publish(INotification notification)
    {
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>();
        foreach (var (k, v) in _executionContext)
        {
            headers[k] = v;
        }
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var typeName = TypeRegistry.Register(notification.GetType());

        _repository.Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Notification,
            PayloadType = typeName,
            Payload = _messageSerializer.Serialize(notification),
            Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
            ParentTracingInfo = currentActivity is null ? null : new TracingInfo(currentActivity.TraceId.ToString(), currentActivity.SpanId.ToString())
        });
    }
}
