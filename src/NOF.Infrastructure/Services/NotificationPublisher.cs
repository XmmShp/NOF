using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

/// <summary>
/// Notification publisher implementation.
/// Runs the outbound pipeline (which handles tracing, headers, etc.) then dispatches via the rider.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly IOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOutboxMessageRepository _outboxRepository;
    private readonly IObjectSerializer _objectSerializer;

    public NotificationPublisher(
        INotificationRider rider,
        IOutboundPipelineExecutor outboundPipeline,
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

    public void DeferPublish(INotification notification)
    {
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>();
        foreach (var (k, v) in _executionContext)
        {
            headers[k] = v;
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

    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = notification,
            Services = _serviceProvider
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.PublishAsync(notification, _executionContext, ct);
        }, cancellationToken);
    }
}
