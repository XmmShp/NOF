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

    public void DeferPublish(object notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _executionContext)
        {
            headers[kvp.Key] = kvp.Value;
        }

        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var typeName = TypeRegistry.Register(notification.GetType());

        _outboxRepository.Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Notification,
            PayloadType = typeName,
            Payload = _objectSerializer.Serialize(notification).ToArray(),
            Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
            ParentTracingInfo = currentActivity is null ? null : new TracingInfo(currentActivity.TraceId.ToString(), currentActivity.SpanId.ToString())
        });
    }

    public async Task PublishAsync(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var context = new NotificationOutboundContext
        {
            Message = notification,
            Services = _serviceProvider
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.PublishAsync(notification, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
