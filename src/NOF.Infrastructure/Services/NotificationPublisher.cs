using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly INotificationOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOutboxMessageRepository _outboxRepository;
    private readonly IObjectSerializer _objectSerializer;

    public NotificationPublisher(
        INotificationRider rider,
        INotificationOutboundPipelineExecutor outboundPipeline,
        IExecutionContext executionContext,
        IServiceProvider serviceProvider,
        IOutboxMessageRepository outboxRepository,
        IObjectSerializer objectSerializer)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
        _serviceProvider = serviceProvider;
        _outboxRepository = outboxRepository;
        _objectSerializer = objectSerializer;
    }

    public void DeferPublish(object notification, Type[] notificationTypes)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _executionContext)
        {
            headers[kvp.Key] = kvp.Value;
        }

        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var payloadTypeName = TypeRegistry.Register(notification.GetType());
        var dispatchTypeNames = JsonSerializer.Serialize(notificationTypes.Select(TypeRegistry.Register).ToArray());

        _outboxRepository.Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Notification,
            PayloadType = payloadTypeName,
            DispatchTypes = dispatchTypeNames,
            Payload = _objectSerializer.Serialize(notification).ToArray(),
            Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
            ParentTracingInfo = currentActivity is null ? null : new TracingInfo(currentActivity.TraceId.ToString(), currentActivity.SpanId.ToString())
        });
    }

    public async Task PublishAsync(object notification, Type[] notificationTypes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        var context = new NotificationOutboundContext
        {
            Message = notification,
            Services = _serviceProvider
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.PublishAsync(notification, notificationTypes, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
